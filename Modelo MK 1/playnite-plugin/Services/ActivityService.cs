using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BibliotecaRA.Models;
using Newtonsoft.Json;

namespace BibliotecaRA.Services
{
    public class ActivityService
    {
        private const int PageSize = 50;
        private static readonly TimeSpan FreshnessWindow = TimeSpan.FromMinutes(10);

        private readonly ApiClient api;
        private readonly Func<RaSettings> getSettings;
        private readonly string lastPlayedPath;
        private readonly string playtimePath;

        public ActivityService(ApiClient api, Func<RaSettings> getSettings, string dataPath)
        {
            this.api = api;
            this.getSettings = getSettings;
            lastPlayedPath = Path.Combine(dataPath, "activity_lastplayed.json");
            playtimePath = Path.Combine(dataPath, "activity_playtime.json");
        }

        public RaLastPlayedCache LoadLastPlayed()
        {
            return LoadCache<RaLastPlayedCache>(lastPlayedPath) ?? new RaLastPlayedCache();
        }

        public RaPlaytimeCache LoadPlaytime()
        {
            return LoadCache<RaPlaytimeCache>(playtimePath) ?? new RaPlaytimeCache();
        }

        public async Task<RaLastPlayedCache> GetLastPlayedAsync(
            bool force,
            Action<string> onProgress,
            CancellationToken ct)
        {
            var settings = getSettings();
            if (string.IsNullOrWhiteSpace(settings.Username))
            {
                throw new RaApiException(
                    "Usuário do RetroAchievements não configurado. Abra as configurações do addon e informe seu nome de usuário.");
            }

            var username = settings.Username.Trim();
            var cache = LoadCache<RaLastPlayedCache>(lastPlayedPath);
            if (!force &&
                cache != null &&
                string.Equals(cache.Username, username, StringComparison.OrdinalIgnoreCase) &&
                DateTime.UtcNow - cache.SavedAtUtc < FreshnessWindow)
            {
                onProgress?.Invoke(
                    "Atividade recente já atualizada (" + cache.Games.Count + " jogos).");
                return cache;
            }

            if (cache == null ||
                !string.Equals(cache.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                // Conta diferente (ou primeira vez): recomeça do zero.
                cache = new RaLastPlayedCache { Username = username };
            }

            var offset = 0;
            cache.Games.Clear();

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                onProgress?.Invoke(
                    "Atividade: " + cache.Games.Count + " jogos lidos...");

                var body = await api.GetStringAsync(
                    "API_GetUserRecentlyPlayedGames.php",
                    new Dictionary<string, string>
                    {
                        { "u", username },
                        { "c", PageSize.ToString() },
                        { "o", offset.ToString() }
                    },
                    ct).ConfigureAwait(false);

                var page = JsonConvert.DeserializeObject<List<RaRecentlyPlayedItem>>(body)
                           ?? new List<RaRecentlyPlayedItem>();
                if (page.Count == 0)
                {
                    break;
                }

                foreach (var item in page)
                {
                    var utc = ParseUtc(item.LastPlayed);
                    if (utc.HasValue)
                    {
                        cache.Games[item.Id.ToString(CultureInfo.InvariantCulture)] = utc.Value;
                    }
                }

                offset += page.Count;
                if (page.Count < PageSize)
                {
                    break;
                }
            }

            cache.SavedAtUtc = DateTime.UtcNow;
            SaveCache(lastPlayedPath, cache);
            onProgress?.Invoke(
                "Atividade: " + cache.Games.Count + " jogos com última vez jogado.");
            return cache;
        }

        public async Task SyncPlaytimeAsync(
            RaLastPlayedCache lastPlayed,
            bool force,
            Action<string> onProgress,
            CancellationToken ct)
        {
            var username = getSettings().Username.Trim();
            var cache = LoadCache<RaPlaytimeCache>(playtimePath);
            if (cache == null ||
                !string.Equals(cache.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                // Conta diferente (ou primeira vez): recomeça do zero.
                cache = new RaPlaytimeCache { Username = username };
            }

            // Só consulta o tempo de quem nunca foi consultado ou cuta última vez
            // jogado avançou desde a última consulta (a única forma do tempo mudar).
            var pending = new List<KeyValuePair<string, DateTime>>();
            foreach (var pair in lastPlayed.Games)
            {
                if (force ||
                    !cache.Games.TryGetValue(pair.Key, out var entry) ||
                    pair.Value > entry.FetchedAtUtc)
                {
                    pending.Add(pair);
                }
            }

            var total = pending.Count;
            var index = 0;
            try
            {
                foreach (var pair in pending)
                {
                    ct.ThrowIfCancellationRequested();
                    index++;
                    onProgress?.Invoke(
                        "Tempo de jogo: " + index + "/" + total + " (" + pair.Key + ")...");

                    var body = await api.GetStringAsync(
                        "API_GetGameInfoAndUserProgress.php",
                        new Dictionary<string, string>
                        {
                            { "g", pair.Key },
                            { "u", username }
                        },
                        ct).ConfigureAwait(false);

                    long seconds = 0;
                    var trimmed = (body ?? string.Empty).TrimStart();
                    if (trimmed.StartsWith("{"))
                    {
                        var info = JsonConvert.DeserializeObject<RaGameInfoAndUserProgress>(body);
                        seconds = info?.UserTotalPlaytime ?? 0;
                    }

                    cache.Games[pair.Key] = new RaPlaytimeEntry
                    {
                        Seconds = seconds,
                        FetchedAtUtc = DateTime.UtcNow
                    };

                    // Gravação incremental: se cancelar no meio, o progresso
                    // das consultas já feitas não se perde.
                    if (index % 10 == 0)
                    {
                        SaveCache(playtimePath, cache);
                    }
                }
            }
            finally
            {
                if (index > 0)
                {
                    SaveCache(playtimePath, cache);
                }
            }

            onProgress?.Invoke(
                total == 0
                    ? "Tempo de jogo: nada novo para consultar."
                    : "Tempo de jogo: " + total + " consultas concluídas.");
        }

        public void ClearCache()
        {
            DeleteIfExists(lastPlayedPath);
            DeleteIfExists(playtimePath);
        }

        public bool CacheExists()
        {
            return File.Exists(lastPlayedPath) || File.Exists(playtimePath);
        }

        public static DateTime? ParseUtc(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // AssumeUniversal: strings sem offset (LastPlayed) já vêm em UTC.
            // AdjustToUniversal: converte ISO8601 com offset (MostRecentAwardedDate).
            if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private T LoadCache<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Falha ao ler cache de atividade: " + e.Message);
                return null;
            }
        }

        private void SaveCache<T>(string path, T cache)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(cache);
            File.WriteAllText(path, json);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
