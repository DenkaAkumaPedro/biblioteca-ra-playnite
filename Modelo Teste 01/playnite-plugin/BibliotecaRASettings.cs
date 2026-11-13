using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using BibliotecaRA.Models;
using Playnite.SDK;
using Playnite.SDK.Data;

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

                StatusText = "Conexão OK — a Web API Key funciona.";
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

        public async void ForceCatalogUpdate()
        {
            if (Busy)
            {
                return;
            }

            Busy = true;
            StatusText = "Baixando o catálogo do RetroAchievements...";
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
                {
                    var cache = await plugin.RefreshCatalogAsync(
                        message => StatusText = message, cts.Token);
                    var gameCount = cache.GamesByConsole.Values.Sum(list => list.Count);
                    StatusText = "Catálogo atualizado: " + gameCount + " jogos em " +
                                 cache.GamesByConsole.Count + " sistemas.";
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
            StatusText = "Cache do catálogo limpo.";
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
