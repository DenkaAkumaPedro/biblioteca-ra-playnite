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

    public class ApiClient : IDisposable
    {
        public const string BaseUrl = "https://retroachievements.org/API/";
        private const string UserAgent = "BibliotecaRA/0.0.1 (Playnite addon)";
        private const int MinIntervalMs = 250;
        private const int MaxRetries = 3;

        private static readonly HttpClient http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly Func<RaSettings> getSettings;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private DateTime lastRequestUtc = DateTime.MinValue;

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

                            if ((int)response.StatusCode == 429 ||
                                (int)response.StatusCode >= 500)
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
                var elapsed = DateTime.UtcNow - lastRequestUtc;
                var wait = TimeSpan.FromMilliseconds(MinIntervalMs) - elapsed;
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

        public void Dispose()
        {
            gate.Dispose();
        }
    }
}
