using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BibliotecaRA.Models;
using BibliotecaRA.Services;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Windows.Controls;

namespace BibliotecaRA
{
    public class BibliotecaRA : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly BibliotecaRASettingsViewModel settings;
        private readonly ApiClient apiClient;
        private readonly CatalogService catalogService;
        private readonly HashService hashService;

        public override Guid Id { get; } = Guid.Parse("46ec2715-329d-4e68-98ca-a35b7259cc5a");

        public override string Name => "Biblioteca RA";

        public override LibraryClient Client { get; } = new BibliotecaRAClient();

        public BibliotecaRA(IPlayniteAPI api) : base(api)
        {
            settings = new BibliotecaRASettingsViewModel(this);
            apiClient = new ApiClient(() => settings.Settings);
            catalogService = new CatalogService(apiClient, () => settings.Settings, GetPluginUserDataPath());
            hashService = new HashService();
            Properties = new LibraryPluginProperties
            {
                HasSettings = true
            };
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var ct = args.CancelToken;
            var currentSettings = settings.Settings;
            var games = new List<GameMetadata>();

            if (string.IsNullOrWhiteSpace(currentSettings.ApiKey))
            {
                logger.Warn("Web API Key não configurada; importação do RetroAchievements ignorada.");
                return games;
            }

            RaCatalogCache catalog;
            try
            {
                catalog = catalogService
                    .GetCatalogAsync(false, message => logger.Debug(message), ct)
                    .GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.Error(e, "Falha ao obter o catálogo do RetroAchievements.");
                PlayniteApi.Notifications.Add(
                    "Biblioteca RA",
                    "Falha ao consultar o RetroAchievements: " + e.Message,
                    NotificationType.Error);
                return games;
            }

            Dictionary<int, string> installed;
            try
            {
                var hashIndex = hashService.BuildHashIndex(catalog);
                installed = hashService.DetectInstalled(
                    hashIndex, currentSettings, message => logger.Debug(message), ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.Error(e, "Falha ao detectar ROMs instaladas.");
                installed = new Dictionary<int, string>();
            }

            foreach (var consoleGames in catalog.GamesByConsole.Values)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var game in consoleGames)
                {
                    games.Add(BuildMetadata(game, installed));
                }
            }

            logger.Info("Catálogo do RetroAchievements: " + games.Count + " jogos, " +
                        installed.Count + " ROMs detectadas.");
            return games;
        }

        private static GameMetadata BuildMetadata(RaGameListItem game, Dictionary<int, string> installed)
        {
            var metadata = new GameMetadata
            {
                GameId = game.Id.ToString(),
                Name = game.Title,
                Platforms = new HashSet<MetadataProperty>
                {
                    new MetadataNameProperty(game.ConsoleName)
                },
                Links = new List<Link>
                {
                    new Link
                    {
                        Name = "RetroAchievements",
                        Url = "https://retroachievements.org/game/" + game.Id
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(game.ImageIcon))
            {
                metadata.Icon = new MetadataFile("https://retroachievements.org" + game.ImageIcon);
            }

            if (installed.TryGetValue(game.Id, out var romPath))
            {
                metadata.IsInstalled = true;
                metadata.GameActions = new List<GameAction>
                {
                    new GameAction
                    {
                        Type = GameActionType.File,
                        Path = romPath,
                        IsPlayAction = true
                    }
                };
            }

            return metadata;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new BibliotecaRASettingsView();
        }

        internal Task TestConnectionAsync(CancellationToken ct)
        {
            return apiClient.TestConnectionAsync(ct);
        }

        internal Task<RaCatalogCache> RefreshCatalogAsync(
            Action<string> onProgress, CancellationToken ct)
        {
            return catalogService.GetCatalogAsync(true, onProgress, ct);
        }

        internal void ClearCache()
        {
            catalogService.ClearCache();
        }
    }
}
