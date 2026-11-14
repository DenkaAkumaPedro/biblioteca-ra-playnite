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

            if (string.IsNullOrWhiteSpace(currentSettings.Username))
            {
                logger.Warn("Usuário do RetroAchievements não configurado; importação ignorada.");
                return games;
            }

            RaUserLibraryCache userLibrary;
            try
            {
                userLibrary = catalogService
                    .GetUserLibraryAsync(false, message => logger.Debug(message), ct)
                    .GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.Error(e, "Falha ao obter a biblioteca da conta do RetroAchievements.");
                PlayniteApi.Notifications.Add(
                    "Biblioteca RA",
                    "Falha ao consultar o RetroAchievements: " + e.Message,
                    NotificationType.Error);
                return games;
            }

            var installed = new Dictionary<int, string>();
            var catalogById = new Dictionary<int, RaGameListItem>();

            if (currentSettings.DetectInstalled)
            {
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
                    logger.Error(e, "Falha ao obter o catálogo de hashes do RetroAchievements.");
                    catalog = null;
                }

                if (catalog != null)
                {
                    try
                    {
                        var hashIndex = hashService.BuildHashIndex(catalog);
                        installed = hashService.DetectInstalled(
                            hashIndex, currentSettings, message => logger.Debug(message), ct);

                        foreach (var consoleGames in catalog.GamesByConsole.Values)
                        {
                            foreach (var game in consoleGames)
                            {
                                catalogById[game.Id] = game;
                            }
                        }
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
                }
            }

            var accountIds = new HashSet<int>();
            foreach (var game in userLibrary.Games)
            {
                ct.ThrowIfCancellationRequested();
                accountIds.Add(game.Id);
                games.Add(BuildMetadata(
                    game.Id, game.Title, game.ConsoleName, game.ImageIcon, installed));
            }

            foreach (var pair in installed)
            {
                ct.ThrowIfCancellationRequested();
                if (accountIds.Contains(pair.Key))
                {
                    continue;
                }

                if (!catalogById.TryGetValue(pair.Key, out var catalogGame))
                {
                    continue;
                }

                games.Add(BuildMetadata(
                    catalogGame.Id, catalogGame.Title, catalogGame.ConsoleName,
                    catalogGame.ImageIcon, installed));
            }

            logger.Info("Biblioteca RA: " + games.Count + " jogos importados (" +
                        userLibrary.Games.Count + " da conta, " +
                        installed.Count + " ROMs detectadas).");
            return games;
        }

        private static GameMetadata BuildMetadata(
            int id,
            string title,
            string consoleName,
            string icon,
            Dictionary<int, string> installed)
        {
            var metadata = new GameMetadata
            {
                GameId = id.ToString(),
                Name = title,
                Platforms = new HashSet<MetadataProperty>
                {
                    new MetadataNameProperty(consoleName)
                },
                Links = new List<Link>
                {
                    new Link
                    {
                        Name = "RetroAchievements",
                        Url = "https://retroachievements.org/game/" + id
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(icon))
            {
                metadata.Icon = new MetadataFile("https://retroachievements.org" + icon);
            }

            if (installed.TryGetValue(id, out var romPath))
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

        internal async Task<string> RefreshLibraryAsync(
            Action<string> onProgress, CancellationToken ct)
        {
            var userLibrary = await catalogService
                .GetUserLibraryAsync(true, onProgress, ct).ConfigureAwait(false);

            var message = "Biblioteca da conta: " + userLibrary.Games.Count + " jogos.";

            if (settings.Settings.DetectInstalled)
            {
                await catalogService
                    .GetCatalogAsync(true, onProgress, ct).ConfigureAwait(false);
                message += " Catálogo de hashes atualizado.";
            }

            return message;
        }

        internal void ClearCache()
        {
            catalogService.ClearCache();
        }
    }
}
