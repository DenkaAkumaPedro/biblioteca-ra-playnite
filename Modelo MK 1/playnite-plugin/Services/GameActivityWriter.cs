using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Playnite.SDK;

namespace BibliotecaRA.Services
{
    public class PendingSession
    {
        public Guid GameId { get; set; }

        // Total acumulado no RA (nao o incremento): o delta que vira sessao nova
        // e calculado dentro do writer, contra o que ja esta gravado no GameActivity.
        public long TotalSeconds { get; set; }

        // Data do incremento: ultima vez jogado no RA.
        public DateTime Date { get; set; }
    }

    public static class GameActivityWriter
    {
        private const string GameActivityAddonType = "GameActivity.GameActivity";
        private const string SessionActionName = "RetroAchievements";

        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Fixo de proposito: e o que identifica as sessoes desta extensao dentro do
        // GameActivity. A soma delas faz o papel de baseline — na proxima sync entra
        // so o delta entre o total do RA e o que ja foi gravado, entao rodar a sync
        // dez vezes nao infla o tempo e as sessoes antigas ficam no lugar (datadas
        // no dia em que o tempo foi lançado).
        private static readonly Guid SessionSourceId = new Guid("b6e3d8a1-4c27-4f9e-9d15-2a8f0c7e6b43");

        private static bool warnedUnavailable;

        public static int WriteSessions(IPlayniteAPI api, List<PendingSession> sessions, ILogger logger)
        {
            if (sessions == null || sessions.Count == 0)
            {
                return 0;
            }

            try
            {
                var database = ResolveDatabase(api);
                if (database == null)
                {
                    WarnUnavailable(logger, "o plugin GameActivity não está instalado ou a estrutura dele mudou; as sessões não foram gravadas.");
                    return 0;
                }

                var databaseType = database.Database.GetType();
                var interfaceType = databaseType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == "IPluginDatabase`1");
                if (interfaceType == null)
                {
                    WarnUnavailable(logger, "o GameActivity expôs um banco sem a interface IPluginDatabase<T>; as sessões não foram gravadas.");
                    return 0;
                }

                // Os metodos genericos so existem na interface. A interface ja vem fechada
                // (IPluginDatabase<GameActivities>), entao Get/AddOrUpdate ja estao
                // materializados com GameActivities: nao ha MakeGenericMethod aqui.
                var itemType = interfaceType.GetGenericArguments()[0];
                var getMethod = interfaceType.GetMethods()
                    .FirstOrDefault(m => m.Name == "Get" && m.GetParameters().Length == 3 &&
                        m.GetParameters()[0].ParameterType == typeof(Guid));
                var addOrUpdate = interfaceType.GetMethods()
                    .FirstOrDefault(m => m.Name == "AddOrUpdate" && m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == itemType);
                if (getMethod == null || addOrUpdate == null)
                {
                    WarnUnavailable(logger, "as assinaturas Get/AddOrUpdate do GameActivity mudaram; as sessões não foram gravadas.");
                    return 0;
                }

                var written = 0;
                foreach (var session in sessions)
                {
                    var entry = getMethod.Invoke(database.Database, new object[] { session.GameId, false, false });
                    if (entry == null)
                    {
                        logger.Warn("GameActivity: nenhum registro para o jogo " + session.GameId + ", sessão ignorada.");
                        continue;
                    }

                    // Só grava quando há delta: jogo sem tempo novo deixa o banco
                    // do GameActivity intocado.
                    if (AppendSession(entry, itemType, session))
                    {
                        addOrUpdate.Invoke(database.Database, new object[] { entry });
                        written++;
                    }
                }

                if (written > 0)
                {
                    warnedUnavailable = false;
                }

                return written;
            }
            catch (Exception e)
            {
                // Falha aqui nunca pode derrubar a sync: o Playtime ja foi gravado no banco
                // do Playnite e as sessoes sao um extra opt-in.
                logger.Error(e, "Falha ao gravar sessões no GameActivity.");
                return 0;
            }
        }

        private static bool AppendSession(object entry, Type itemType, PendingSession session)
        {
            var itemsProperty = itemType.GetProperty("Items", InstanceMembers);
            var items = itemsProperty?.GetValue(entry, null) as IList;
            if (items == null)
            {
                return false;
            }

            var activityType = items.GetType().GetGenericArguments()[0];
            var sourceProperty = activityType.GetProperty("SourceID", InstanceMembers);
            var elapsedProperty = activityType.GetProperty("ElapsedSeconds", InstanceMembers);
            var platformIdsProperty = activityType.GetProperty("PlatformIDs", InstanceMembers);

            // Sem SourceID/ElapsedSeconds nao da para saber o que ja foi gravado por
            // esta extensao; gravar assim seria inflar o tempo a cada sync.
            if (sourceProperty == null ||
                elapsedProperty == null ||
                elapsedProperty.PropertyType != typeof(ulong))
            {
                return false;
            }

            // Baseline: a soma das sessoes desta extensao ja presentes no jogo. O que
            // falta para bater com o total do RA e o incremento desta sync. As sessoes
            // antigas nunca sao removidas — e por isso que o tempo se acumula por data
            // em vez de ser substituido pela mais recente.
            long applied = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (sourceProperty.GetValue(items[i], null) is Guid candidateGuid &&
                    candidateGuid == SessionSourceId)
                {
                    applied += (long)(ulong)elapsedProperty.GetValue(items[i], null);
                }
            }

            var delta = session.TotalSeconds - applied;
            if (session.TotalSeconds <= 0 || delta <= 0)
            {
                return false;
            }

            var activity = Activator.CreateInstance(activityType);
            sourceProperty.SetValue(activity, SessionSourceId, null);

            // PlatformIDs e o que amarra a sessao ao jogo do Playnite.
            if (platformIdsProperty != null)
            {
                var platformIds = (IList)Activator.CreateInstance(platformIdsProperty.PropertyType);
                platformIds.Add(session.GameId);
                platformIdsProperty.SetValue(activity, platformIds, null);
            }

            activityType.GetProperty("GameActionName", InstanceMembers)?.SetValue(activity, SessionActionName, null);
            activityType.GetProperty("IdConfiguration", InstanceMembers)?.SetValue(activity, 0, null);
            activityType.GetProperty("DateSession", InstanceMembers)?.SetValue(activity, session.Date, null);
            elapsedProperty.SetValue(activity, (ulong)delta, null);

            var detailsProperty = activityType.GetProperty("Details", InstanceMembers);
            if (detailsProperty != null)
            {
                detailsProperty.SetValue(activity, Activator.CreateInstance(detailsProperty.PropertyType), null);
            }

            items.Add(activity);

            // SessionPlaytime nao e gravado aqui: e uma propriedade calculada, que o proprio
            // GameActivity soma a partir de Items, e nao tem setter.

            return true;
        }

        private static ResolvedDatabase ResolveDatabase(IPlayniteAPI api)
        {
            // api.Addons.Plugins traz as instancias reais dos plugins, tipadas pela classe
            // base do SDK. O casamento e pelo nome do tipo, e nao pelo Id do extension.yaml,
            // porque o Id e um GUID que muda entre versoes do GameActivity.
            var plugin = api?.Addons?.Plugins?
                .FirstOrDefault(p => p?.GetType().FullName == GameActivityAddonType);
            if (plugin == null)
            {
                return null;
            }

            // GameActivityMonitoring e internal e PluginDatabase e private: o unico caminho
            // e por reflexao, e por isso que a extensao nao referencia a DLL em tempo
            // de compilacao — assim ela continua funcionando sem o GameActivity instalado.
            var monitoring = plugin.GetType()
                .GetProperty("GameActivityMonitoring", InstanceMembers)?.GetValue(plugin, null);
            if (monitoring == null)
            {
                return null;
            }

            var database = monitoring.GetType()
                .GetProperty("PluginDatabase", InstanceMembers)?.GetValue(monitoring, null);
            return database == null ? null : new ResolvedDatabase(database);
        }

        private static void WarnUnavailable(ILogger logger, string reason)
        {
            if (warnedUnavailable)
            {
                return;
            }

            warnedUnavailable = true;
            logger.Warn("GameActivity: sessões não gravadas porque " + reason +
                " Ative a opção de gravar sessão e confira se o plugin GameActivity está instalado e carregado.");
        }

        private class ResolvedDatabase
        {
            public ResolvedDatabase(object database)
            {
                Database = database;
            }

            public object Database { get; }
        }
    }
}
