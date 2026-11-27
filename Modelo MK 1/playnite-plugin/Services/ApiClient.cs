using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BibliotecaRA.Models;

namespace BibliotecaRA.Services
{
    public class RaApiException : Exception
    {
        public RaApiException(string message) : base(message)
        {
        }
    }

    public class RaRateLimitException : RaApiException
    {
        public RaRateLimitException(string message) : base(message)
        {
        }
    }

    public class ApiClient : IDisposable
    {
        public const string BaseUrl = "https://retroachievements.org/API/";
        private const string UserAgent = "BibliotecaRA/0.1.3 (Playnite addon)";
        private const int BaseMinIntervalMs = 1000;
        private const int MaxIntervalMs = 10000;
        private const int MaxRetries = 3;
        private static readonly TimeSpan RateLimitDecay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan[] RateLimitBackoff =
        {
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(180)
        };

        private static readonly HttpClient http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly Func<RaSettings> getSettings;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private DateTime lastRequestUtc = DateTime.MinValue;
        private DateTime cooldownUntilUtc = DateTime.MinValue;
        private DateTime lastRateLimitUtc = DateTime.MinValue;
        private int minIntervalMs = BaseMinIntervalMs;

        public ApiClient(Func<RaSettings> getSettings)
        {
            this.getSettings = getSettings;
        }

        public async Task<string> GetStringAsync(
            string endpoint,
            IDictionary<string, string> parameters,
            CancellationToken ct)
        {
            var currentSettings = getSettings();
            if (string.IsNullOrWhiteSpace(currentSettings.ApiKey))
            {
                throw new RaApiException("Web API Key não configurada. Abra as configurações do addon e informe a chave do RetroAchievements.");
            }

            var allParameters = new Dictionary<string, string> { { "y", currentSettings.ApiKey.Trim() } };
            foreach (var pair in parameters)
            {
                allParameters[pair.Key] = pair.Value;
            }

            var url = BuildUrl(endpoint, allParameters);

            for (var attempt = 0; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                await ThrottleAsync(ct).ConfigureAwait(false);

                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
                        using (var response = await http.SendAsync(request, ct).ConfigureAwait(false))
                        {
                            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                            if ((int)response.StatusCode == 429)
                            {
                                var delay = GetRateLimitDelay(attempt, response);
                                RegisterRateLimit(delay);

                                if (attempt < MaxRetries - 1)
                                {
                                    await Task.Delay(delay, ct).ConfigureAwait(false);
                                    continue;
                                }

                                throw new RaRateLimitException(
                                    "O RetroAchievements respondeu 429 (limite de requisições) após várias tentativas. " +
                                    "Tente novamente mais tarde — a próxima sincronização continua de onde parou.");
                            }

                            if ((int)response.StatusCode >= 500)
                            {
                                if (attempt < MaxRetries - 1)
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), ct).ConfigureAwait(false);
                                    continue;
                                }

                                throw new RaApiException(
                                    "O RetroAchievements respondeu " + (int)response.StatusCode +
                                    " após várias tentativas. Tente novamente mais tarde.");
                            }

                            if (!response.IsSuccessStatusCode)
                            {
                                throw new RaApiException(
                                    "O RetroAchievements recusou a requisição (" + (int)response.StatusCode + "). Verifique a Web API Key.");
                            }

                            ThrowIfApiError(body);
                            return body;
                        }
                    }
                }
                catch (HttpRequestException) when (attempt < MaxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), ct).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < MaxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), ct).ConfigureAwait(false);
                }
            }
        }

        public async Task TestConnectionAsync(CancellationToken ct)
        {
            var body = await GetStringAsync(
                "API_GetConsoleIDs.php",
                new Dictionary<string, string> { { "a", "1" }, { "g", "1" } },
                ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith("["))
            {
                throw new RaApiException("Resposta inesperada da API do RetroAchievements.");
            }

            var username = getSettings().Username;
            if (!string.IsNullOrWhiteSpace(username))
            {
                var userBody = await GetStringAsync(
                    "API_GetUserCompletionProgress.php",
                    new Dictionary<string, string>
                    {
                        { "u", username.Trim() },
                        { "c", "1" },
                        { "o", "0" }
                    },
                    ct).ConfigureAwait(false);

                var trimmed = (userBody ?? string.Empty).TrimStart();
                if (!trimmed.StartsWith("{"))
                {
                    throw new RaApiException(
                        "Resposta inesperada ao consultar a biblioteca do usuário. Verifique o nome de usuário.");
                }
            }
        }

        private static string BuildUrl(string endpoint, IDictionary<string, string> parameters)
        {
            var parts = new List<string>();
            foreach (var pair in parameters)
            {
                parts.Add(pair.Key + "=" + Uri.EscapeDataString(pair.Value ?? string.Empty));
            }

            return BaseUrl + endpoint + "?" + string.Join("&", parts);
        }

        private void ThrowIfApiError(string body)
        {
            var trimmed = (body ?? string.Empty).TrimStart();
            if (trimmed.StartsWith("{") &&
                trimmed.IndexOf("\"success\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                trimmed.IndexOf("\"false\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new RaApiException(
                    "A API do RetroAchievements retornou erro. Verifique se a Web API Key está correta e se você não excedeu o limite de requisições.");
            }
        }

        private async Task ThrottleAsync(CancellationToken ct)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;

                // Volta devagar para a taxa base depois de um período sem 429.
                if (minIntervalMs > BaseMinIntervalMs &&
                    now - lastRateLimitUtc > RateLimitDecay)
                {
                    minIntervalMs = Math.Max(BaseMinIntervalMs, minIntervalMs / 2);
                    lastRateLimitUtc = now;
                }

                if (cooldownUntilUtc > now)
                {
                    await Task.Delay(cooldownUntilUtc - now, ct).ConfigureAwait(false);
                    now = DateTime.UtcNow;
                }

                var elapsed = now - lastRequestUtc;
                var wait = TimeSpan.FromMilliseconds(minIntervalMs) - elapsed;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, ct).ConfigureAwait(false);
                }

                lastRequestUtc = DateTime.UtcNow;
            }
            finally
            {
                gate.Release();
            }
        }

        private static TimeSpan GetRateLimitDelay(int attempt, HttpResponseMessage response)
        {
            // Se o servidor informar quanto esperar, ele manda.
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            {
                return delta;
            }

            if (retryAfter?.Date is DateTimeOffset date)
            {
                var until = date - DateTimeOffset.UtcNow;
                if (until > TimeSpan.Zero)
                {
                    return until;
                }
            }

            var index = Math.Min(attempt, RateLimitBackoff.Length - 1);
            return RateLimitBackoff[index];
        }

        private void RegisterRateLimit(TimeSpan delay)
        {
            var now = DateTime.UtcNow;
            if (now + delay > cooldownUntilUtc)
            {
                cooldownUntilUtc = now + delay;
            }

            // Cooldown global: dobra a taxa mínima até voltar ao normal.
            minIntervalMs = Math.Min(minIntervalMs * 2, MaxIntervalMs);
            lastRateLimitUtc = now;
        }

        public void Dispose()
        {
            gate.Dispose();
        }
    }
}
