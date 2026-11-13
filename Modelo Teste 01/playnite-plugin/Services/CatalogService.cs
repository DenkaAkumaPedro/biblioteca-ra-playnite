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
        private readonly string cachePath;

        public CatalogService(ApiClient api, Func<RaSettings> getSettings, string dataPath)
        {
            this.api = api;
            this.getSettings = getSettings;
            cachePath = Path.Combine(dataPath, "catalog.json");
        }

        public async Task<RaCatalogCache> GetCatalogAsync(
            bool forceRefresh,
            Action<string> onProgress,
            CancellationToken ct)
        {
            var cache = LoadCache();
            if (!forceRefresh && cache != null && IsFresh(cache))
            {
                onProgress?.Invoke("Usando cache do catálogo (" + cache.GamesByConsole.Count + " sistemas).");
                return cache;
            }

            cache = await DownloadCatalogAsync(cache, onProgress, ct).ConfigureAwait(false);
            SaveCache(cache);
            return cache;
        }

        public void ClearCache()
        {
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }

        public bool CacheExists()
        {
            return File.Exists(cachePath);
        }

        public DateTime? GetCacheDate()
        {
            var cache = LoadCache();
            return cache?.SavedAtUtc;
        }

        private bool IsFresh(RaCatalogCache cache)
        {
            var ttl = TimeSpan.FromHours(Math.Max(1, getSettings().CacheTtlHours));
            return DateTime.UtcNow - cache.SavedAtUtc < ttl;
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
                    "Sistema " + consoleIndex + "/" + consoleCount + ": " + console.Name);

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

        private RaCatalogCache LoadCache()
        {
            try
            {
                if (!File.Exists(cachePath))
                {
                    return null;
                }

                var json = File.ReadAllText(cachePath);
                return JsonConvert.DeserializeObject<RaCatalogCache>(json);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Falha ao ler cache do catálogo: " + e.Message);
                return null;
            }
        }

        private void SaveCache(RaCatalogCache cache)
        {
            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(cache);
            File.WriteAllText(cachePath, json);
        }
    }
}
