using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BibliotecaRA.Models;
using BibliotecaRA.Services;
using Playnite.SDK;
using Playnite.SDK.Events;
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
        private readonly ActivityService activityService;
        private readonly SemaphoreSlim activityLock = new SemaphoreSlim(1, 1);

        public override Guid Id { get; } = Guid.Parse("46ec2715-329d-4e68-98ca-a35b7259cc5a");

        public override string Name => "Biblioteca RA";

        public override LibraryClient Client { get; } = new BibliotecaRAClient();

        public BibliotecaRA(IPlayniteAPI api) : base(api)
        {
            settings = new BibliotecaRASettingsViewModel(this);
            apiClient = new ApiClient(() => settings.Settings);
            catalogService = new CatalogService(apiClient, () => settings.Settings, GetPluginUserDataPath());
            hashService = new HashService();
            activityService = new ActivityService(apiClient, () => settings.Settings, GetPluginUserDataPath());
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

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            base.OnLibraryUpdated(args);

            var currentSettings = settings.Settings;
            if (!currentSettings.SyncActivity && !currentSettings.SyncPlaytime)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(currentSettings.ApiKey) ||
                string.IsNullOrWhiteSpace(currentSettings.Username))
            {
                return;
            }

            // Em segundo plano (padrão do addon AniList): não bloqueia o
            // término da atualização da biblioteca do Playnite. O resultado
            // vai para o log; erro vira notificação.
            Task.Run(async () =>
            {
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
                    {
                        var result = await RunActivitySyncAsync(
                            message => logger.Debug(message), false, cts.Token).ConfigureAwait(false);

                        if (result.Synced)
                        {
                            logger.Info("Biblioteca RA: " + result.Message);
                            if (result.Changes > 0)
                            {
                                PlayniteApi.Notifications.Add(
                                    "Biblioteca RA",
                                    result.Message,
                                    NotificationType.Info);
                            }
                        }
                        else
                        {
                            logger.Debug("Biblioteca RA: " + result.Message);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.Warn("Sincronização de atividade cancelada.");
                }
                catch (Exception e)
                {
                    logger.Error(e, "Falha na sincronização automática de atividade.");
                    PlayniteApi.Notifications.Add(
                        "Biblioteca RA",
                        "Falha ao sincronizar atividade: " + e.Message,
                        NotificationType.Error);
                }
            });
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

            var activity = await RunActivitySyncAsync(onProgress, true, ct).ConfigureAwait(false);
            message += " " + activity.Message;
            return message;
        }

        internal void ClearCache()
        {
            catalogService.ClearCache();
            activityService.ClearCache();
        }

        internal class ActivitySyncResult
        {
            public bool Synced { get; set; }
            public int Changes { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        private async Task<ActivitySyncResult> RunActivitySyncAsync(
            Action<string> onProgress, bool force, CancellationToken ct)
        {
            var currentSettings = settings.Settings;
            if (!currentSettings.SyncActivity && !currentSettings.SyncPlaytime)
            {
                return new ActivitySyncResult { Message = "Atividade: sincronização desligada." };
            }

            if (string.IsNullOrWhiteSpace(currentSettings.Username))
            {
                return new ActivitySyncResult
                {
                    Message = "Atividade: informe o usuário do RetroAchievements."
                };
            }

            if (!activityLock.Wait(0))
            {
                return new ActivitySyncResult { Message = "Atividade: já em andamento (aguarde)." };
            }

            try
            {
                var lastPlayed = await activityService
                    .GetLastPlayedAsync(force, onProgress, ct).ConfigureAwait(false);

                if (currentSettings.SyncPlaytime)
                {
                    await activityService
                        .SyncPlaytimeAsync(lastPlayed, force, onProgress, ct).ConfigureAwait(false);
                }

                var counts = ApplyActivityToGames(
                    currentSettings.SyncActivity, currentSettings.SyncPlaytime);

                return new ActivitySyncResult
                {
                    Synced = true,
                    Changes = counts.LastActivity + counts.Playtime,
                    Message = "Atividade sincronizada: " + lastPlayed.Games.Count +
                              " jogos com última vez jogado no RA; " +
                              counts.LastActivity + " mudanças de atividade e " +
                              counts.Playtime + " tempos atualizados."
                };
            }
            finally
            {
                activityLock.Release();
            }
        }

        private struct ActivityCounts
        {
            public int LastActivity;
            public int Playtime;
        }

        private ActivityCounts ApplyActivityToGames(bool syncActivity, bool syncPlaytime)
        {
            var counts = new ActivityCounts();
            var lastPlayed = activityService.LoadLastPlayed();
            var playtime = syncPlaytime ? activityService.LoadPlaytime() : null;

            // Fallback barato (sem requisição): a data da conquista mais recente
            // já vem no cache da biblioteca da conta, para jogos que não aparecem
            // na lista de recentes.
            Dictionary<string, RaUserGameItem> libraryById = null;
            if (syncActivity)
            {
                try
                {
                    var userLibrary = catalogService
                        .GetUserLibraryAsync(false, null, CancellationToken.None)
                        .GetAwaiter().GetResult();

                    libraryById = new Dictionary<string, RaUserGameItem>();
                    foreach (var item in userLibrary.Games)
                    {
                        libraryById[item.Id.ToString(CultureInfo.InvariantCulture)] = item;
                    }
                }
                catch (Exception e)
                {
                    logger.Warn("Fallback de atividade (conquista mais recente) indisponível: " +
                                e.Message);
                }
            }

            using (PlayniteApi.Database.BufferedUpdate())
            {
                foreach (var game in PlayniteApi.Database.Games)
                {
                    if (game.PluginId != Id || string.IsNullOrEmpty(game.GameId))
                    {
                        continue;
                    }

                    var changed = false;

                    if (syncActivity)
                    {
                        DateTime? candidate = null;
                        if (lastPlayed.Games.TryGetValue(game.GameId, out var lastPlayedUtc))
                        {
                            candidate = lastPlayedUtc;
                        }
                        else if (libraryById != null &&
                                 libraryById.TryGetValue(game.GameId, out var item))
                        {
                            candidate = ActivityService.ParseUtc(item.MostRecentAwardedDate);
                        }

                        if (candidate.HasValue)
                        {
                            var local = candidate.Value.ToLocalTime();
                            if (game.LastActivity == null || local > game.LastActivity.Value)
                            {
                                game.LastActivity = local;
                                counts.LastActivity++;
                                changed = true;
                            }

                            if (game.PlayCount == 0)
                            {
                                game.PlayCount = 1;
                                counts.LastActivity++;
                                changed = true;
                            }
                        }
                    }

                    if (syncPlaytime && playtime != null &&
                        playtime.Games.TryGetValue(game.GameId, out var entry))
                    {
                        // max(local, RA): só sobe, nunca diminui nem duplica.
                        if ((ulong)entry.Seconds > game.Playtime)
                        {
                            game.Playtime = (ulong)entry.Seconds;
                            counts.Playtime++;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PlayniteApi.Database.Games.Update(game);
                    }
                }
            }

            return counts;
        }
    }
}
