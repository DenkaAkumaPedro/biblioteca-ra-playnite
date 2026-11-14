using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using BibliotecaRA.Models;

namespace BibliotecaRA.Services
{
    public class HashService
    {
        private const int BufferSize = 81920;

        public Dictionary<string, List<int>> BuildHashIndex(RaCatalogCache cache)
        {
            var index = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            if (cache?.GamesByConsole == null)
            {
                return index;
            }

            foreach (var games in cache.GamesByConsole.Values)
            {
                foreach (var game in games)
                {
                    if (game.Hashes == null)
                    {
                        continue;
                    }

                    foreach (var hash in game.Hashes)
                    {
                        if (string.IsNullOrWhiteSpace(hash))
                        {
                            continue;
                        }

                        if (!index.TryGetValue(hash, out var ids))
                        {
                            ids = new List<int>();
                            index[hash] = ids;
                        }

                        if (!ids.Contains(game.Id))
                        {
                            ids.Add(game.Id);
                        }
                    }
                }
            }

            return index;
        }

        public Dictionary<int, string> DetectInstalled(
            Dictionary<string, List<int>> hashIndex,
            RaSettings settings,
            Action<string> onProgress,
            CancellationToken ct)
        {
            var installed = new Dictionary<int, string>();
            if (!settings.DetectInstalled || settings.RomFolders == null || settings.RomFolders.Count == 0)
            {
                return installed;
            }

            var extensions = ParseExtensions(settings.RomExtensions);
            var scanned = 0;

            foreach (var folder in settings.RomFolders)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in EnumerateFilesSafe(folder))
                {
                    ct.ThrowIfCancellationRequested();
                    if (extensions.Count > 0 && !extensions.Contains(Path.GetExtension(file)))
                    {
                        continue;
                    }

                    scanned++;
                    if (scanned % 200 == 0)
                    {
                        onProgress?.Invoke("Verificando ROMs (" + scanned + " arquivos)...");
                    }

                    string hash;
                    try
                    {
                        hash = ComputeMd5(file);
                    }
                    catch (IOException)
                    {
                        continue;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }

                    if (!hashIndex.TryGetValue(hash, out var ids))
                    {
                        continue;
                    }

                    foreach (var id in ids)
                    {
                        if (!installed.ContainsKey(id))
                        {
                            installed[id] = file;
                        }
                    }
                }
            }

            return installed;
        }

        private static IEnumerable<string> EnumerateFilesSafe(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                string[] files;
                try
                {
                    files = Directory.GetFiles(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }

                string[] dirs;
                try
                {
                    dirs = Directory.GetDirectories(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var dir in dirs)
                {
                    pending.Push(dir);
                }
            }
        }

        private static HashSet<string> ParseExtensions(string raw)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            foreach (var piece in raw.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var ext = piece.Trim();
                if (ext.Length == 0)
                {
                    continue;
                }

                if (!ext.StartsWith("."))
                {
                    ext = "." + ext;
                }

                result.Add(ext);
            }

            return result;
        }

        private static string ComputeMd5(string path)
        {
            using (var md5 = MD5.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
            {
                var hash = md5.ComputeHash(stream);
                var builder = new char[hash.Length * 2];
                const string hex = "0123456789abcdef";
                for (var i = 0; i < hash.Length; i++)
                {
                    builder[i * 2] = hex[hash[i] >> 4];
                    builder[i * 2 + 1] = hex[hash[i] & 0xF];
                }

                return new string(builder);
            }
        }
    }
}
