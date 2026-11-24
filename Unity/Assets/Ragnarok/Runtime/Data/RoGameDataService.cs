using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public sealed class RoGameDataService {
        private const int MaxCachedProfiles = 4;
        private static readonly object CacheSync = new object();
        private static readonly Dictionary<string, CachedLoad> CachedLoads =
            new Dictionary<string, CachedLoad>(StringComparer.Ordinal);

        public static readonly RoGameDataService Empty = new RoGameDataService(
            RoItemMetadataTable.Empty,
            RoActorAppearanceMetadata.Empty,
            RoJobMetadataTable.Empty,
            RoEntityDisplayMetadataTable.Empty,
            RoSkillMetadataTable.Empty,
            RoQuestMetadataTable.Empty,
            RoGameDataDiagnostics.Empty
        );

        private RoGameDataService(
            RoItemMetadataTable items,
            RoActorAppearanceMetadata actorAppearance,
            RoJobMetadataTable jobs,
            RoEntityDisplayMetadataTable entityDisplay,
            RoSkillMetadataTable skills,
            RoQuestMetadataTable quests,
            RoGameDataDiagnostics diagnostics,
            Func<RoGameDataDiagnostics> diagnosticsFactory = null
        ) {
            Items = items ?? RoItemMetadataTable.Empty;
            ActorAppearance = actorAppearance ?? RoActorAppearanceMetadata.Empty;
            Jobs = jobs ?? RoJobMetadataTable.Empty;
            EntityDisplay = entityDisplay ?? RoEntityDisplayMetadataTable.Empty;
            Skills = skills ?? RoSkillMetadataTable.Empty;
            Quests = quests ?? RoQuestMetadataTable.Empty;
            this.diagnostics = diagnostics;
            this.diagnosticsFactory = diagnosticsFactory;
        }

        private readonly Func<RoGameDataDiagnostics> diagnosticsFactory;
        private RoGameDataDiagnostics diagnostics;

        public RoItemMetadataTable Items { get; }
        public RoActorAppearanceMetadata ActorAppearance { get; }
        public RoJobMetadataTable Jobs { get; }
        public RoEntityDisplayMetadataTable EntityDisplay { get; }
        public RoSkillMetadataTable Skills { get; }
        public RoQuestMetadataTable Quests { get; }
        public RoGameDataDiagnostics Diagnostics {
            get {
                if (diagnostics != null) {
                    return diagnostics;
                }

                diagnostics = diagnosticsFactory != null
                    ? diagnosticsFactory()
                    : RoGameDataDiagnostics.Empty;
                return diagnostics;
            }
        }

        public static RoGameDataService Load(RoAssetDatabase assets) {
            return Load(assets, null);
        }

        internal static RoGameDataService Load(RoAssetDatabase assets, RoAssetStartupTiming timing) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            string cacheKey = BuildCacheKey(assets);
            if (!string.IsNullOrEmpty(cacheKey)) {
                lock (CacheSync) {
                    if (CachedLoads.TryGetValue(cacheKey, out CachedLoad cachedLoad)) {
                        return cachedLoad.CreateService(assets);
                    }
                }

                CachedLoad loaded = LoadUncached(assets, timing);

                lock (CacheSync) {
                    if (CachedLoads.TryGetValue(cacheKey, out CachedLoad cachedLoad)) {
                        return cachedLoad.CreateService(assets);
                    }

                    CachedLoads[cacheKey] = loaded;
                    TrimCacheLocked();
                }

                return loaded.CreateService(assets);
            }

            return LoadUncached(assets, timing).CreateService(assets);
        }

        private static CachedLoad LoadUncached(RoAssetDatabase assets, RoAssetStartupTiming timing) {
            Task<RoItemMetadataTable> itemsTask = Task.Run(() => {
                using (timing == null ? null : timing.MeasureGameDataItems()) {
                    return RoItemMetadataTable.Load(assets);
                }
            });
            Task<RoActorAppearanceMetadata> actorAppearanceTask = Task.Run(() => {
                using (timing == null ? null : timing.MeasureGameDataActorAppearance()) {
                    return RoActorAppearanceMetadata.Load(assets);
                }
            });
            Task<RoJobMetadataTable> jobsTask = Task.Run(() => {
                using (timing == null ? null : timing.MeasureGameDataJobs()) {
                    return RoJobMetadataTable.Load(assets);
                }
            });
            Task<RoEntityDisplayMetadataTable> entityDisplayTask = Task.Run(() => {
                using (timing == null ? null : timing.MeasureGameDataEntityDisplay()) {
                    return RoEntityDisplayMetadataTable.Load(assets);
                }
            });
            Task<RoSkillMetadataTable> skillsTask = Task.Run(() => {
                using (timing == null ? null : timing.MeasureGameDataSkills()) {
                    return RoSkillMetadataTable.Load(assets);
                }
            });
            Task<RoQuestMetadataTable> questsTask = Task.Run(() => {
                using (timing == null ? null : timing.MeasureGameDataQuests()) {
                    return RoQuestMetadataTable.Load(assets);
                }
            });

            try {
                Task.WaitAll(itemsTask, actorAppearanceTask, jobsTask, entityDisplayTask, skillsTask, questsTask);
            } catch (AggregateException ex) {
                ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions[0]).Throw();
                throw;
            }

            RoItemMetadataTable items = itemsTask.Result;
            RoActorAppearanceMetadata actorAppearance = actorAppearanceTask.Result;
            RoJobMetadataTable jobs = jobsTask.Result;
            RoEntityDisplayMetadataTable entityDisplay = entityDisplayTask.Result;
            RoSkillMetadataTable skills = skillsTask.Result;
            RoQuestMetadataTable quests = questsTask.Result;

            return new CachedLoad(
                items,
                actorAppearance,
                jobs,
                entityDisplay,
                skills,
                quests);
        }

        private static string BuildCacheKey(RoAssetDatabase assets) {
            string profileHash = assets?.DiskCache?.ProfileHash;
            return string.IsNullOrWhiteSpace(profileHash) ? null : profileHash;
        }

        private static void TrimCacheLocked() {
            if (CachedLoads.Count <= MaxCachedProfiles) {
                return;
            }

            string firstKey = null;
            foreach (string key in CachedLoads.Keys) {
                firstKey = key;
                break;
            }

            if (firstKey != null) {
                CachedLoads.Remove(firstKey);
            }
        }

        private sealed class CachedLoad {
            public CachedLoad(
                RoItemMetadataTable items,
                RoActorAppearanceMetadata actorAppearance,
                RoJobMetadataTable jobs,
                RoEntityDisplayMetadataTable entityDisplay,
                RoSkillMetadataTable skills,
                RoQuestMetadataTable quests) {
                Items = items ?? RoItemMetadataTable.Empty;
                ActorAppearance = actorAppearance ?? RoActorAppearanceMetadata.Empty;
                Jobs = jobs ?? RoJobMetadataTable.Empty;
                EntityDisplay = entityDisplay ?? RoEntityDisplayMetadataTable.Empty;
                Skills = skills ?? RoSkillMetadataTable.Empty;
                Quests = quests ?? RoQuestMetadataTable.Empty;
            }

            private RoItemMetadataTable Items { get; }
            private RoActorAppearanceMetadata ActorAppearance { get; }
            private RoJobMetadataTable Jobs { get; }
            private RoEntityDisplayMetadataTable EntityDisplay { get; }
            private RoSkillMetadataTable Skills { get; }
            private RoQuestMetadataTable Quests { get; }

            public RoGameDataService CreateService(RoAssetDatabase assets) {
                return new RoGameDataService(
                    Items,
                    ActorAppearance,
                    Jobs,
                    EntityDisplay,
                    Skills,
                    Quests,
                    null,
                    () => RoGameDataDiagnostics.Build(assets, assets.Tables, Items, Jobs, EntityDisplay, Skills));
            }
        }

        public string BuildSummary() {
            string actorSummary = $", actorAppearance=jobs:{ActorAppearance.JobBodyNameCount}+gender:{ActorAppearance.GenderedJobBodyNameCount}, hairRemaps:{ActorAppearance.HairStyleRemapCount}, headgear:{ActorAppearance.AccessoryNameCount}, robes:{ActorAppearance.RobeNameCount}, weapons:{ActorAppearance.WeaponSuffixCount}, shields:{ActorAppearance.ShieldNameCount}";
            string jobSummary = Jobs != null && Jobs.IsLoaded
                ? $", jobs={Jobs.PlayerJobCount}"
                : ", jobs=0";
            if (!string.IsNullOrEmpty(Jobs?.LoadError)) {
                jobSummary += ", jobsLoad=" + Jobs.LoadError;
            }

            string entitySummary = $", entityDisplay={EntityDisplay.Count}";
            if (EntityDisplay.MobDbCount > 0) {
                entitySummary += $", mobdb={EntityDisplay.MobDbCount} ('{EntityDisplay.MobDbSourcePath}')";
            } else if (!string.IsNullOrEmpty(EntityDisplay.LoadError)) {
                entitySummary += ", entityLoad=" + EntityDisplay.LoadError;
            }

            string itemDbSummary = Items != null && Items.HasDatabaseMetadata
                ? $", itemdb={Items.ItemDbCount} ('{Items.ItemDbSourcePath}')"
                : !string.IsNullOrEmpty(Items?.ItemDbLoadError)
                    ? ", itemdb=" + Items.ItemDbLoadError
                    : string.Empty;
            string skillSummary = Skills != null && Skills.IsLoaded
                ? $", skills={Skills.Count}"
                : ", skills=0";
            if (Skills != null && Skills.SkillDbCount > 0) {
                skillSummary += $", skilldb={Skills.SkillDbCount} ('{Skills.SkillDbSourcePath}')";
            } else if (!string.IsNullOrEmpty(Skills?.LoadError)) {
                skillSummary += ", skillsLoad=" + Skills.LoadError;
            }

            string questSummary = Quests != null && Quests.IsLoaded
                ? $", quests={Quests.Count} ('{Quests.SourcePath}')"
                : ", quests=0";
            if (!string.IsNullOrEmpty(Quests?.LoadError)) {
                questSummary += ", questsLoad=" + Quests.LoadError;
            }

            string diagnosticsSummary = Diagnostics != null
                ? ", " + Diagnostics.BuildSummary()
                : string.Empty;

            if (Items == null || !Items.IsLoaded) {
                string reason = !string.IsNullOrEmpty(Items?.LoadError) ? ", iteminfo=" + Items.LoadError : string.Empty;
                return "game data: items=0" + reason + itemDbSummary + actorSummary + jobSummary + entitySummary + skillSummary + questSummary + diagnosticsSummary;
            }

            string itemInfoSummary = !string.IsNullOrEmpty(Items.SourcePath) ? ", iteminfo='" + Items.SourcePath + "'" : ", iteminfo=none";
            return "game data: items=" + Items.Count + itemInfoSummary + itemDbSummary + actorSummary + jobSummary + entitySummary + skillSummary + questSummary + diagnosticsSummary;
        }
    }
}
