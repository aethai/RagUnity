using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public sealed class RoGameDataDiagnostics {
        private const int MaxSamplesPerCategory = 6;

        public static readonly RoGameDataDiagnostics Empty = new RoGameDataDiagnostics(
            0,
            0,
            0,
            0,
            0,
            0,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        private RoGameDataDiagnostics(
            int missingRequiredTables,
            int tableSourceConflictCount,
            int missingItemIconCount,
            int missingSkillIconCount,
            int missingJobSpritePairCount,
            int missingEntitySpritePairCount,
            IReadOnlyList<string> tableSourceConflictSamples,
            IReadOnlyList<string> missingItemIconSamples,
            IReadOnlyList<string> missingSkillIconSamples,
            IReadOnlyList<string> missingJobSpritePairSamples,
            IReadOnlyList<string> missingEntitySpritePairSamples
        ) {
            MissingRequiredTables = Math.Max(0, missingRequiredTables);
            TableSourceConflictCount = Math.Max(0, tableSourceConflictCount);
            MissingItemIconCount = Math.Max(0, missingItemIconCount);
            MissingSkillIconCount = Math.Max(0, missingSkillIconCount);
            MissingJobSpritePairCount = Math.Max(0, missingJobSpritePairCount);
            MissingEntitySpritePairCount = Math.Max(0, missingEntitySpritePairCount);
            TableSourceConflictSamples = tableSourceConflictSamples ?? Array.Empty<string>();
            MissingItemIconSamples = missingItemIconSamples ?? Array.Empty<string>();
            MissingSkillIconSamples = missingSkillIconSamples ?? Array.Empty<string>();
            MissingJobSpritePairSamples = missingJobSpritePairSamples ?? Array.Empty<string>();
            MissingEntitySpritePairSamples = missingEntitySpritePairSamples ?? Array.Empty<string>();
        }

        public int MissingRequiredTables { get; }
        public int TableSourceConflictCount { get; }
        public int MissingItemIconCount { get; }
        public int MissingSkillIconCount { get; }
        public int MissingJobSpritePairCount { get; }
        public int MissingEntitySpritePairCount { get; }
        public IReadOnlyList<string> TableSourceConflictSamples { get; }
        public IReadOnlyList<string> MissingItemIconSamples { get; }
        public IReadOnlyList<string> MissingSkillIconSamples { get; }
        public IReadOnlyList<string> MissingJobSpritePairSamples { get; }
        public IReadOnlyList<string> MissingEntitySpritePairSamples { get; }
        public int TotalProblemCount =>
            MissingRequiredTables +
            TableSourceConflictCount +
            MissingItemIconCount +
            MissingSkillIconCount +
            MissingJobSpritePairCount +
            MissingEntitySpritePairCount;
        public bool HasProblems => TotalProblemCount > 0;

        public static RoGameDataDiagnostics Build(
            RoAssetDatabase assets,
            RoAssetTables tables,
            RoItemMetadataTable items,
            RoJobMetadataTable jobs,
            RoEntityDisplayMetadataTable entities,
            RoSkillMetadataTable skills
        ) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            return BuildCore(
                assets.Exists,
                path => assets.FindSourceEntries(path).Count,
                tables,
                items,
                jobs,
                entities,
                skills
            );
        }

        public static RoGameDataDiagnostics BuildForTests(
            Func<string, bool> exists,
            RoItemMetadataTable items,
            RoJobMetadataTable jobs,
            RoEntityDisplayMetadataTable entities,
            RoSkillMetadataTable skills
        ) {
            return BuildCore(exists, null, null, items, jobs, entities, skills);
        }

        public string BuildSummary() {
            if (!HasProblems) {
                return "diagnostics=ok";
            }

            var builder = new StringBuilder();
            builder.Append("diagnostics=");
            AppendCount(builder, "missingRequiredTables", MissingRequiredTables);
            AppendCount(builder, "tableSourceConflicts", TableSourceConflictCount);
            AppendCount(builder, "missingItemIcons", MissingItemIconCount);
            AppendCount(builder, "missingSkillIcons", MissingSkillIconCount);
            AppendCount(builder, "missingJobSprites", MissingJobSpritePairCount);
            AppendCount(builder, "missingEntitySprites", MissingEntitySpritePairCount);
            return builder.ToString();
        }

        private static RoGameDataDiagnostics BuildCore(
            Func<string, bool> exists,
            Func<string, int> sourceCount,
            RoAssetTables tables,
            RoItemMetadataTable items,
            RoJobMetadataTable jobs,
            RoEntityDisplayMetadataTable entities,
            RoSkillMetadataTable skills
        ) {
            exists ??= _ => false;

            int missingRequiredTables = tables?.MissingRequiredPhase7TableCount ?? 0;
            int tableConflicts = CountTableSourceConflicts(sourceCount, tables, items, jobs, entities, skills, out List<string> tableConflictSamples);
            int missingItemIcons = CountMissingItemIcons(exists, items, out List<string> itemIconSamples);
            int missingSkillIcons = CountMissingSkillIcons(exists, skills, out List<string> skillIconSamples);
            int missingJobSprites = CountMissingJobSprites(exists, jobs, out List<string> jobSpriteSamples);
            int missingEntitySprites = CountMissingEntitySprites(exists, entities, out List<string> entitySpriteSamples);

            return new RoGameDataDiagnostics(
                missingRequiredTables,
                tableConflicts,
                missingItemIcons,
                missingSkillIcons,
                missingJobSprites,
                missingEntitySprites,
                tableConflictSamples,
                itemIconSamples,
                skillIconSamples,
                jobSpriteSamples,
                entitySpriteSamples
            );
        }

        private static int CountMissingItemIcons(Func<string, bool> exists, RoItemMetadataTable items, out List<string> samples) {
            samples = new List<string>();
            if (items == null || items.Count == 0) {
                return 0;
            }

            int missing = 0;
            foreach (RoItemMetadata item in items.Items.Values) {
                if (item == null || string.IsNullOrEmpty(item.IdentifiedResourceName) || string.Equals(item.IdentifiedResourceName, "unknown", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string path = item.GetInventoryIconPath(true);
                if (!exists(path)) {
                    missing++;
                    AddSample(samples, $"{item.ItemId}:{path}");
                }
            }

            return missing;
        }

        private static int CountMissingSkillIcons(Func<string, bool> exists, RoSkillMetadataTable skills, out List<string> samples) {
            samples = new List<string>();
            if (skills == null || skills.Count == 0) {
                return 0;
            }

            int missing = 0;
            foreach (RoSkillMetadata skill in skills.Skills.Values) {
                if (skill == null) {
                    continue;
                }

                string path = skill.GetIconPath();
                if (!string.IsNullOrEmpty(path) && !exists(path)) {
                    missing++;
                    AddSample(samples, $"{skill.SkillId}:{path}");
                }
            }

            return missing;
        }

        private static int CountMissingJobSprites(Func<string, bool> exists, RoJobMetadataTable jobs, out List<string> samples) {
            samples = new List<string>();
            if (jobs == null || jobs.Count == 0) {
                return 0;
            }

            int missing = 0;
            foreach (RoJobMetadata job in jobs.Jobs.Values) {
                if (job == null || !job.IsPlayerJob) {
                    continue;
                }

                missing += CountMissingSpritePair(exists, job.GetBodySpriteBasePath(true), $"job:{job.ClassId}:male", samples);
                missing += CountMissingSpritePair(exists, job.GetBodySpriteBasePath(false), $"job:{job.ClassId}:female", samples);
            }

            return missing;
        }

        private static int CountMissingEntitySprites(Func<string, bool> exists, RoEntityDisplayMetadataTable entities, out List<string> samples) {
            samples = new List<string>();
            if (entities == null || entities.Count == 0) {
                return 0;
            }

            int missing = 0;
            foreach (RoEntityDisplayMetadata entity in entities.Entities.Values) {
                if (entity == null) {
                    continue;
                }

                missing += CountMissingSpritePair(exists, entity.GetSpriteBasePath(), $"entity:{entity.ClassId}", samples);
            }

            return missing;
        }

        private static int CountMissingSpritePair(Func<string, bool> exists, string basePath, string label, List<string> samples) {
            if (string.IsNullOrEmpty(basePath)) {
                return 0;
            }

            bool hasSprite = exists(basePath + ".spr");
            bool hasAction = exists(basePath + ".act");
            if (hasSprite && hasAction) {
                return 0;
            }

            AddSample(samples, $"{label}:{basePath}");
            return 1;
        }

        private static int CountTableSourceConflicts(
            Func<string, int> sourceCount,
            RoAssetTables tables,
            RoItemMetadataTable items,
            RoJobMetadataTable jobs,
            RoEntityDisplayMetadataTable entities,
            RoSkillMetadataTable skills,
            out List<string> samples
        ) {
            samples = new List<string>();
            if (sourceCount == null) {
                return 0;
            }

            int conflicts = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (tables != null) {
                foreach (RoAssetTables.TableProbe probe in tables.TableProbes) {
                    conflicts += CountTableSourceConflict(sourceCount, probe?.ResolvedPath, probe?.Key, seen, samples);
                }
            }

            conflicts += CountTableSourceConflict(sourceCount, items?.SourcePath, "iteminfo", seen, samples);
            conflicts += CountMultiPathSourceConflicts(sourceCount, items?.ItemDbSourcePath, "itemdb", seen, samples);
            conflicts += CountTableSourceConflict(sourceCount, jobs?.JobIdentitySourcePath, "jobidentity", seen, samples);
            conflicts += CountTableSourceConflict(sourceCount, jobs?.JobNameSourcePath, "jobname", seen, samples);
            conflicts += CountTableSourceConflict(sourceCount, jobs?.PcJobNameGenderSourcePath, "pcjobnamegender", seen, samples);
            conflicts += CountTableSourceConflict(sourceCount, entities?.MobDbSourcePath, "mobdb", seen, samples);
            conflicts += CountTableSourceConflict(sourceCount, skills?.SkillIdSourcePath, "skillid", seen, samples);
            conflicts += CountTableSourceConflict(sourceCount, skills?.SkillInfoSourcePath, "skillinfo", seen, samples);
            conflicts += CountTableSourceConflict(sourceCount, skills?.SkillDescriptionSourcePath, "skilldescript", seen, samples);
            conflicts += CountTableSourceConflict(sourceCount, skills?.SkillDbSourcePath, "skilldb", seen, samples);
            return conflicts;
        }

        private static int CountMultiPathSourceConflicts(Func<string, int> sourceCount, string paths, string label, HashSet<string> seen, List<string> samples) {
            if (string.IsNullOrWhiteSpace(paths)) {
                return 0;
            }

            int conflicts = 0;
            string[] split = paths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < split.Length; i++) {
                conflicts += CountTableSourceConflict(sourceCount, split[i].Trim(), label, seen, samples);
            }

            return conflicts;
        }

        private static int CountTableSourceConflict(Func<string, int> sourceCount, string path, string label, HashSet<string> seen, List<string> samples) {
            if (string.IsNullOrWhiteSpace(path) || !seen.Add(path)) {
                return 0;
            }

            int count = sourceCount(path);
            if (count <= 1) {
                return 0;
            }

            AddSample(samples, $"{label}:{path} x{count}");
            return 1;
        }

        private static void AddCount(StringBuilder builder, string name, int value) {
            if (value <= 0) {
                return;
            }

            if (builder[builder.Length - 1] != '=') {
                builder.Append(", ");
            }

            builder.Append(name);
            builder.Append(':');
            builder.Append(value);
        }

        private static void AppendCount(StringBuilder builder, string name, int value) {
            AddCount(builder, name, value);
        }

        private static void AddSample(List<string> samples, string value) {
            if (samples.Count < MaxSamplesPerCategory && !string.IsNullOrWhiteSpace(value)) {
                samples.Add(value);
            }
        }
    }
}
