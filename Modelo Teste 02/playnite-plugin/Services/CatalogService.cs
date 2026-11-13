using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BibliotecaRA.Models;
using Newtonsoft.Json;

namespace BibliotecaRA.Services
{
    public class CatalogService
    {
        private const int PageSize = 500;

        private readonly ApiClient api;
        private readonly Func<RaSettings> getSettings;
        private readonly string userLibraryPath;
        private readonly string catalogPath;

        public CatalogService(ApiClient api, Func<RaSettings> getSettings, string dataPath)
        {
            this.api = api;
            this.getSettings = getSettings;
            userLibraryPath = Path.Combine(dataPath, "user_library.json");
            catalogPath = Path.Combine(dataPath, "catalog.json");
        }

        public async Task<RaUserLibraryCache> GetUserLibraryAsync(
            bool forceRefresh,
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
            var cache = LoadCache<RaUserLibraryCache>(userLibraryPath);
            if (!forceRefresh && cache != null && IsFresh(cache.SavedAtUtc) &&
                string.Equals(cache.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                onProgress?.Invoke("Usando biblioteca da conta (" + cache.Games.Count + " jogos).");
                return cache;
            }

            cache = await DownloadUserLibraryAsync(username, onProgress, ct).ConfigureAwait(false);
            SaveCache(userLibraryPath, cache);
            return cache;
        }

        public async Task<RaCatalogCache> GetCatalogAsync(
            bool forceRefresh,
            Action<string> onProgress,
            CancellationToken ct)
        {
            var cache = LoadCache<RaCatalogCache>(catalogPath);
            if (!forceRefresh && cache != null && IsFresh(cache.SavedAtUtc))
            {
                onProgress?.Invoke("Usando cache do catálogo (" + cache.GamesByConsole.Count + " sistemas).");
                return cache;
            }

            cache = await DownloadCatalogAsync(cache, onProgress, ct).ConfigureAwait(false);
            SaveCache(catalogPath, cache);
            return cache;
        }

        public void ClearCache()
        {
            DeleteIfExists(userLibraryPath);
            DeleteIfExists(catalogPath);
        }

        public bool CacheExists()
        {
            return File.Exists(userLibraryPath) || File.Exists(catalogPath);
        }

        public DateTime? GetCacheDate()
        {
            var userLib = LoadCache<RaUserLibraryCache>(userLibraryPath);
            if (userLib != null)
            {
                return userLib.SavedAtUtc;
            }

            return LoadCache<RaCatalogCache>(catalogPath)?.SavedAtUtc;
        }

        private bool IsFresh(DateTime savedAtUtc)
        {
            var ttl = TimeSpan.FromHours(Math.Max(1, getSettings().CacheTtlHours));
            return DateTime.UtcNow - savedAtUtc < ttl;
        }

        private async Task<RaUserLibraryCache> DownloadUserLibraryAsync(
            string username,
            Action<string> onProgress,
            CancellationToken ct)
        {
            var cache = new RaUserLibraryCache { Username = username };
            var offset = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                onProgress?.Invoke(
                    "Jogos da conta: " + cache.Games.Count + " baixados...");

                var body = await api.GetStringAsync(
                    "API_GetUserCompletionProgress.php",
                    new Dictionary<string, string>
                    {
                        { "u", username },
                        { "c", PageSize.ToString() },
                        { "o", offset.ToString() }
                    },
                    ct).ConfigureAwait(false);

                var page = JsonConvert.DeserializeObject<RaUserCompletionProgress>(body);
                if (page?.Results == null || page.Results.Count == 0)
                {
                    break;
                }

                cache.Games.AddRange(page.Results);
                offset += page.Results.Count;

                if (page.Total > 0 && cache.Games.Count >= page.Total)
                {
                    break;
                }

                if (page.Results.Count < PageSize)
                {
                    break;
                }
            }

            onProgress?.Invoke("Biblioteca da conta: " + cache.Games.Count + " jogos.");
            cache.SavedAtUtc = DateTime.UtcNow;
            return cache;
        }

        private async Task<RaCatalogCache> DownloadCatalogAsync(
            RaCatalogCache existing,
            Action<string> onProgress,
            CancellationToken ct)
        {
            var cache = existing ?? new RaCatalogCache();

            onProgress?.Invoke("Buscando lista de sistemas...");
            var consolesBody = await api.GetStringAsync(
                "API_GetConsoleIDs.php",
                new Dictionary<string, string> { { "a", "1" }, { "g", "1" } },
                ct).ConfigureAwait(false);
            cache.Consoles = JsonConvert.DeserializeObject<List<RaConsole>>(consolesBody)
                             ?? new List<RaConsole>();

            cache.GamesByConsole = new Dictionary<string, List<RaGameListItem>>();
            var consoleCount = cache.Consoles.Count;
            var consoleIndex = 0;

            foreach (var console in cache.Consoles)
            {
                ct.ThrowIfCancellationRequested();
                consoleIndex++;
                onProgress?.Invoke(
                    "Hashes — sistema " + consoleIndex + "/" + consoleCount + ": " + console.Name);

                var games = new List<RaGameListItem>();
                var offset = 0;

                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    var pageBody = await api.GetStringAsync(
                        "API_GetGameList.php",
                        new Dictionary<string, string>
                        {
                            { "i", console.Id.ToString() },
                            { "h", "1" },
                            { "o", offset.ToString() },
                            { "c", PageSize.ToString() }
                        },
                        ct).ConfigureAwait(false);

                    var page = JsonConvert.DeserializeObject<List<RaGameListItem>>(pageBody)
                               ?? new List<RaGameListItem>();
                    if (page.Count == 0)
                    {
                        break;
                    }

                    games.AddRange(page);
                    offset += page.Count;
                    if (page.Count < PageSize)
                    {
                        break;
                    }
                }

                cache.GamesByConsole[console.Id.ToString()] = games;
            }

            cache.SavedAtUtc = DateTime.UtcNow;
            return cache;
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
                System.Diagnostics.Debug.WriteLine("Falha ao ler cache: " + e.Message);
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
