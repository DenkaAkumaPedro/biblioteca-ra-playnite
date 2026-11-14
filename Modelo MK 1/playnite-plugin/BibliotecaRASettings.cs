using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using BibliotecaRA.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;

namespace BibliotecaRA
{
    public class BibliotecaRASettingsViewModel : ObservableObject, ISettings
    {
        private readonly BibliotecaRA plugin;
        private RaSettings editingClone;
        private RaSettings settings;
        private string statusText = string.Empty;
        private bool busy;

        public RaSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> Folders { get; } = new ObservableCollection<string>();

        public string StatusText
        {
            get => statusText;
            set => SetValue(ref statusText, value);
        }

        public bool Busy
        {
            get => busy;
            set => SetValue(ref busy, value);
        }

        public BibliotecaRASettingsViewModel(BibliotecaRA plugin)
        {
            this.plugin = plugin;
            var saved = plugin.LoadPluginSettings<RaSettings>();
            settings = saved ?? new RaSettings();
            SyncFolders();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
            SyncFolders();
        }

        public void EndEdit()
        {
            Settings.RomFolders = Folders.ToList();
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (!string.IsNullOrWhiteSpace(Settings.ApiKey) &&
                Settings.ApiKey.Trim().Length < 16)
            {
                errors.Add("A Web API Key parece inválida (muito curta).");
            }

            if (string.IsNullOrWhiteSpace(Settings.ApiKey) &&
                !string.IsNullOrWhiteSpace(Settings.Username))
            {
                errors.Add("Informe a Web API Key para usar com o usuário do RetroAchievements.");
            }

            if (!string.IsNullOrWhiteSpace(Settings.ApiKey) &&
                string.IsNullOrWhiteSpace(Settings.Username))
            {
                errors.Add("Informe o usuário do RetroAchievements (nome no site).");
            }

            if (Settings.CacheTtlHours <= 0)
            {
                errors.Add("O TTL do cache deve ser maior que zero.");
            }

            foreach (var folder in Folders)
            {
                if (!System.IO.Directory.Exists(folder))
                {
                    errors.Add("A pasta de ROM não existe: " + folder);
                }
            }

            return errors.Count == 0;
        }

        public void AddFolder()
        {
            var folder = plugin.PlayniteApi.Dialogs.SelectFolder();
            if (!string.IsNullOrWhiteSpace(folder) && !Folders.Contains(folder))
            {
                Folders.Add(folder);
            }
        }

        public void ImportGameFolders()
        {
            var added = 0;
            var already = 0;
            var seen = new HashSet<string>(Folders, StringComparer.OrdinalIgnoreCase);

            foreach (var game in plugin.PlayniteApi.Database.Games)
            {
                var paths = new List<string>();

                if (game.Roms != null)
                {
                    foreach (var rom in game.Roms)
                    {
                        if (!string.IsNullOrWhiteSpace(rom.Path))
                        {
                            paths.Add(rom.Path);
                        }
                    }
                }

                if (game.GameActions != null)
                {
                    foreach (var action in game.GameActions)
                    {
                        // Só Type.File (a ROM/arquivo jogável); ações de emulador
                        // apontam para o executável, não para a pasta de ROMs.
                        if (action.Type == GameActionType.File &&
                            !string.IsNullOrWhiteSpace(action.Path))
                        {
                            paths.Add(action.Path);
                        }
                    }
                }

                foreach (var path in paths)
                {
                    try
                    {
                        var directory = System.IO.Path.GetDirectoryName(path);
                        if (string.IsNullOrWhiteSpace(directory) ||
                            !System.IO.Directory.Exists(directory))
                        {
                            continue;
                        }

                        if (!seen.Add(directory))
                        {
                            already++;
                            continue;
                        }

                        Folders.Add(directory);
                        added++;
                    }
                    catch (ArgumentException)
                    {
                        // Caminho com caracteres inválidos; ignora.
                    }
                }
            }

            StatusText = added > 0
                ? " " + added + " pastas importadas dos jogos do Playnite" +
                  (already > 0 ? " (" + already + " já estavam na lista)." : ".")
                : "Nenhuma pasta nova encontrada nos jogos do Playnite" +
                  (already > 0 ? " (" + already + " já na lista)." : ".");
        }

        public void RemoveFolder(string folder)
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Folders.Remove(folder);
            }
        }

        public async void TestConnection()
        {
            if (Busy)
            {
                return;
            }

            Busy = true;
            StatusText = "Testando conexão com o RetroAchievements...";
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                {
                    await plugin.TestConnectionAsync(cts.Token);
                }

                StatusText = "Conexão OK — Web API Key" +
                             (string.IsNullOrWhiteSpace(Settings.Username)
                                 ? "."
                                 : " e usuário \"" + Settings.Username.Trim() + "\".");
            }
            catch (Exception e)
            {
                StatusText = "Falha no teste: " + e.Message;
            }
            finally
            {
                Busy = false;
            }
        }

        public async void ForceLibraryUpdate()
        {
            if (Busy)
            {
                return;
            }

            Busy = true;
            StatusText = "Atualizando a biblioteca do RetroAchievements...";
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
                {
                    var message = await plugin.RefreshLibraryAsync(
                        text => StatusText = text, cts.Token);
                    StatusText = message;
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "Atualização cancelada.";
            }
            catch (Exception e)
            {
                StatusText = "Falha na atualização: " + e.Message;
            }
            finally
            {
                Busy = false;
            }
        }

        public void ClearCache()
        {
            plugin.ClearCache();
            StatusText = "Cache da biblioteca e do catálogo limpo.";
        }

        private void SyncFolders()
        {
            Folders.Clear();
            if (Settings?.RomFolders == null)
            {
                return;
            }

            foreach (var folder in Settings.RomFolders)
            {
                Folders.Add(folder);
            }
        }
    }
}
