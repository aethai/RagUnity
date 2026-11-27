using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    [Flags]
    public enum RoSkillTargetFlags {
        None = 0,
        Enemy = 1,
        Place = 2,
        Self = 4,
        Friend = 16,
        Trap = 32,
        Pet = 64,
        Homun = 128
    }

    public enum RoSkillTargetType {
        Unknown = 0,
        Passive = 1,
        Attack = 2,
        Ground = 3,
        Self = 4,
        Support = 5,
        Friend = 6,
        Trap = 7,
        Pet = 8,
        Any = 9
    }

    public readonly struct RoSkillRequirement {
        public RoSkillRequirement(int skillId, int level) {
            SkillId = Math.Max(0, skillId);
            Level = Math.Max(1, level);
        }

        public int SkillId { get; }
        public int Level { get; }
    }

    public sealed class RoSkillMetadata {
        public RoSkillMetadata(
            int skillId,
            string aegisName,
            string displayName,
            int maxLevel,
            bool canSelectLevel,
            IReadOnlyList<int> spCosts,
            IReadOnlyList<int> attackRanges,
            IReadOnlyList<RoSkillRequirement> prerequisites,
            string descriptionText,
            string skillType = null,
            RoSkillTargetType targetType = RoSkillTargetType.Unknown,
            IReadOnlyList<int> castTimesMs = null,
            IReadOnlyList<int> fixedCastTimesMs = null,
            IReadOnlyList<int> afterCastActDelaysMs = null,
            IReadOnlyList<int> cooldownsMs = null,
            string actionType = null,
            string iconName = null
        ) {
            SkillId = Math.Max(0, skillId);
            AegisName = NormalizeText(aegisName);
            DisplayName = NormalizeText(displayName);
            MaxLevel = Math.Max(0, maxLevel);
            CanSelectLevel = canSelectLevel;
            SpCosts = CopyIntList(spCosts, clampNegative: true);
            AttackRanges = CopyIntList(attackRanges, clampNegative: false);
            Prerequisites = CopyPrerequisites(prerequisites);
            DescriptionText = NormalizeText(descriptionText);
            SkillType = NormalizeText(skillType);
            TargetType = targetType;
            TargetFlags = ToTargetFlags(targetType);
            CastTimesMs = CopyIntList(castTimesMs, clampNegative: true);
            FixedCastTimesMs = CopyIntList(fixedCastTimesMs, clampNegative: true);
            AfterCastActDelaysMs = CopyIntList(afterCastActDelaysMs, clampNegative: true);
            CooldownsMs = CopyIntList(cooldownsMs, clampNegative: true);
            ActionType = NormalizeText(actionType);
            IconName = NormalizeIconName(iconName, AegisName, SkillId);
        }

        public int SkillId { get; }
        public string AegisName { get; }
        public string DisplayName { get; }
        public int MaxLevel { get; }
        public bool CanSelectLevel { get; }
        public IReadOnlyList<int> SpCosts { get; }
        public IReadOnlyList<int> AttackRanges { get; }
        public IReadOnlyList<RoSkillRequirement> Prerequisites { get; }
        public string DescriptionText { get; }
        public string SkillType { get; }
        public RoSkillTargetType TargetType { get; }
        public RoSkillTargetFlags TargetFlags { get; }
        public IReadOnlyList<int> CastTimesMs { get; }
        public IReadOnlyList<int> FixedCastTimesMs { get; }
        public IReadOnlyList<int> AfterCastActDelaysMs { get; }
        public IReadOnlyList<int> CooldownsMs { get; }
        public string ActionType { get; }
        public string IconName { get; }
        public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);
        public bool HasTimingMetadata => CastTimesMs.Count > 0 || FixedCastTimesMs.Count > 0 || AfterCastActDelaysMs.Count > 0 || CooldownsMs.Count > 0;

        public string GetDisplayName() {
            if (!string.IsNullOrEmpty(DisplayName)) {
                return DisplayName;
            }

            return string.IsNullOrEmpty(AegisName) ? "Unknown Skill" : AegisName;
        }

        public IReadOnlyList<string> GetDescriptionLines() {
            if (string.IsNullOrEmpty(DescriptionText)) {
                return Array.Empty<string>();
            }

            return DescriptionText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        public int GetSpCost(int level) {
            return ValueForLevel(SpCosts, level, 0);
        }

        public int GetAttackRange(int level) {
            return ValueForLevel(AttackRanges, level, 0);
        }

        public int GetCastTimeMs(int level) {
            return ValueForLevel(CastTimesMs, level, 0);
        }

        public int GetFixedCastTimeMs(int level) {
            return ValueForLevel(FixedCastTimesMs, level, 0);
        }

        public int GetAfterCastActDelayMs(int level) {
            return ValueForLevel(AfterCastActDelaysMs, level, 0);
        }

        public int GetCooldownMs(int level) {
            return ValueForLevel(CooldownsMs, level, 0);
        }

        public static RoSkillTargetFlags ToTargetFlags(RoSkillTargetType targetType) {
            switch (targetType) {
                case RoSkillTargetType.Passive:
                    return RoSkillTargetFlags.None;
                case RoSkillTargetType.Attack:
                    return RoSkillTargetFlags.Enemy;
                case RoSkillTargetType.Ground:
                    return RoSkillTargetFlags.Place;
                case RoSkillTargetType.Self:
                    return RoSkillTargetFlags.Self;
                case RoSkillTargetType.Support:
                case RoSkillTargetType.Friend:
                    return RoSkillTargetFlags.Friend;
                case RoSkillTargetType.Trap:
                    return RoSkillTargetFlags.Trap;
                case RoSkillTargetType.Pet:
                    return RoSkillTargetFlags.Pet;
                case RoSkillTargetType.Any:
                    return RoSkillTargetFlags.Enemy | RoSkillTargetFlags.Place | RoSkillTargetFlags.Friend | RoSkillTargetFlags.Trap;
                default:
                    return RoSkillTargetFlags.None;
            }
        }

        public static RoSkillTargetFlags FromServerSkillType(int type) {
            var flags = RoSkillTargetFlags.None;
            if ((type & 1) != 0) {
                flags |= RoSkillTargetFlags.Enemy;
            }

            if ((type & 2) != 0) {
                flags |= RoSkillTargetFlags.Place;
            }

            if ((type & 4) != 0) {
                flags |= RoSkillTargetFlags.Self;
            }

            if ((type & 16) != 0) {
                flags |= RoSkillTargetFlags.Friend;
            }

            if ((type & 32) != 0) {
                flags |= RoSkillTargetFlags.Trap;
            }

            if ((type & 64) != 0) {
                flags |= RoSkillTargetFlags.Pet;
            }

            if ((type & 128) != 0) {
                flags |= RoSkillTargetFlags.Homun;
            }

            return flags;
        }

        public static RoSkillTargetType ToLegacyTargetType(RoSkillTargetFlags flags) {
            if (flags == RoSkillTargetFlags.None) {
                return RoSkillTargetType.Passive;
            }

            if (flags == RoSkillTargetFlags.Self) {
                return RoSkillTargetType.Self;
            }

            if ((flags & RoSkillTargetFlags.Place) != 0) {
                return RoSkillTargetType.Ground;
            }

            if ((flags & RoSkillTargetFlags.Trap) != 0) {
                return RoSkillTargetType.Trap;
            }

            if ((flags & RoSkillTargetFlags.Friend) != 0 &&
                (flags & RoSkillTargetFlags.Enemy) == 0) {
                return RoSkillTargetType.Friend;
            }

            if ((flags & RoSkillTargetFlags.Pet) != 0 ||
                (flags & RoSkillTargetFlags.Homun) != 0) {
                return RoSkillTargetType.Pet;
            }

            if ((flags & RoSkillTargetFlags.Enemy) != 0 &&
                (flags & RoSkillTargetFlags.Friend) != 0) {
                return RoSkillTargetType.Any;
            }

            if ((flags & RoSkillTargetFlags.Enemy) != 0) {
                return RoSkillTargetType.Attack;
            }

            return RoSkillTargetType.Unknown;
        }

        public string GetIconPath() {
            return "data/texture/" + InterfaceSegment() + "/item/" + IconName + ".png";
        }

        public RoSkillMetadata WithDatabaseMetadata(RoSkillDbMetadata metadata) {
            if (metadata == null || metadata.SkillId != SkillId) {
                return this;
            }

            return new RoSkillMetadata(
                SkillId,
                string.IsNullOrEmpty(AegisName) ? metadata.AegisName : AegisName,
                string.IsNullOrEmpty(DisplayName) ? metadata.DisplayName : DisplayName,
                MaxLevel > 0 ? MaxLevel : metadata.MaxLevel,
                CanSelectLevel,
                SpCosts.Count > 0 ? SpCosts : metadata.SpCosts,
                AttackRanges.Count > 0 ? AttackRanges : metadata.AttackRanges,
                Prerequisites,
                string.IsNullOrEmpty(DescriptionText) ? metadata.DisplayName : DescriptionText,
                string.IsNullOrEmpty(SkillType) ? metadata.SkillType : SkillType,
                TargetType != RoSkillTargetType.Unknown ? TargetType : metadata.TargetType,
                CastTimesMs.Count > 0 ? CastTimesMs : metadata.CastTimesMs,
                FixedCastTimesMs.Count > 0 ? FixedCastTimesMs : metadata.FixedCastTimesMs,
                AfterCastActDelaysMs.Count > 0 ? AfterCastActDelaysMs : metadata.AfterCastActDelaysMs,
                CooldownsMs.Count > 0 ? CooldownsMs : metadata.CooldownsMs,
                ActionType,
                IconName
            );
        }

        public static RoSkillMetadata FromDatabaseMetadata(RoSkillDbMetadata metadata) {
            if (metadata == null) {
                return null;
            }

            return new RoSkillMetadata(
                metadata.SkillId,
                metadata.AegisName,
                metadata.DisplayName,
                metadata.MaxLevel,
                metadata.MaxLevel > 1,
                metadata.SpCosts,
                metadata.AttackRanges,
                Array.Empty<RoSkillRequirement>(),
                string.Empty,
                metadata.SkillType,
                metadata.TargetType,
                metadata.CastTimesMs,
                metadata.FixedCastTimesMs,
                metadata.AfterCastActDelaysMs,
                metadata.CooldownsMs
            );
        }

        private static int ValueForLevel(IReadOnlyList<int> values, int level, int fallback) {
            if (values == null || values.Count == 0) {
                return fallback;
            }

            int index = Math.Max(1, level) - 1;
            if (index >= values.Count) {
                index = values.Count - 1;
            }

            return values[index];
        }

        private static IReadOnlyList<int> CopyIntList(IReadOnlyList<int> values, bool clampNegative) {
            if (values == null || values.Count == 0) {
                return Array.Empty<int>();
            }

            var copy = new int[values.Count];
            for (int i = 0; i < values.Count; i++) {
                copy[i] = clampNegative ? Math.Max(0, values[i]) : values[i];
            }

            return copy;
        }

        private static IReadOnlyList<RoSkillRequirement> CopyPrerequisites(IReadOnlyList<RoSkillRequirement> values) {
            if (values == null || values.Count == 0) {
                return Array.Empty<RoSkillRequirement>();
            }

            var copy = new RoSkillRequirement[values.Count];
            for (int i = 0; i < values.Count; i++) {
                copy[i] = values[i];
            }

            return copy;
        }

        private static string NormalizeText(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(value);
            return string.IsNullOrEmpty(decoded) ? value : decoded;
        }

        private static string NormalizeIconName(string iconName, string aegisName, int skillId) {
            string value = string.IsNullOrWhiteSpace(iconName) ? aegisName : iconName;
            if (string.IsNullOrWhiteSpace(value)) {
                return "skill_" + Math.Max(0, skillId).ToString(CultureInfo.InvariantCulture);
            }

            value = value.Trim().Replace('\\', '/');
            int slash = value.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < value.Length) {
                value = value.Substring(slash + 1);
            }

            if (value.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(0, value.Length - 4);
            }

            return value.ToLowerInvariant();
        }

        private static string InterfaceSegment() {
            return LegacyKorean(0xC0, 0xAF, 0xC0, 0xFA, 0xC0, 0xCE, 0xC5, 0xCD, 0xC6, 0xE4, 0xC0, 0xCC, 0xBD, 0xBA);
        }

        private static string LegacyKorean(params int[] bytes) {
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) {
                chars[i] = (char) bytes[i];
            }

            string legacy = new string(chars);
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(legacy);
            return string.IsNullOrEmpty(decoded) ? legacy : decoded;
        }
    }

    public sealed class RoSkillMetadataTable {
        private const int SkillMetadataCacheMagic = 0x4C494B53; // SKIL
        private const int SkillMetadataCacheVersion = 1;
        private const int MaxCachedSkills = 1_000_000;
        private const int MaxCachedConstants = 1_000_000;
        private const int MaxCachedListEntries = 100_000;

        private static readonly Regex ConstantAssignmentPattern = new Regex(
            @"(?<key>[A-Za-z_][A-Za-z0-9_\.]*)\s*[:=]\s*(?<value>-?(?:0x[0-9A-Fa-f]+|\d+))",
            RegexOptions.Compiled
        );

        private static readonly Regex IndexedBlockStartPattern = new Regex(
            @"\[\s*(?<key>[^\]]+)\s*\]\s*=\s*\{",
            RegexOptions.Compiled
        );

        private static readonly Regex IndexedValueStartPattern = new Regex(
            @"\[\s*(?<key>[^\]]+)\s*\]\s*=\s*(?<open>[\{\[])",
            RegexOptions.Compiled
        );

        private static readonly Regex QuotedStringPattern = new Regex(
            @"(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>",
            RegexOptions.Compiled | RegexOptions.Singleline
        );

        private static readonly Regex SkillRequirementPattern = new Regex(
            @"[\{\[]\s*(?<skill>[A-Za-z_][A-Za-z0-9_\.]*|-?(?:0x[0-9A-Fa-f]+|\d+))\s*(?:,\s*(?<level>-?(?:0x[0-9A-Fa-f]+|\d+)))?",
            RegexOptions.Compiled
        );

        private readonly Dictionary<int, RoSkillMetadata> skills;
        private readonly Dictionary<string, int> constants;

        public static readonly RoSkillMetadataTable Empty = new RoSkillMetadataTable(
            new Dictionary<int, RoSkillMetadata>(),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            null,
            null,
            null,
            null,
            0,
            null,
            false
        );

        private RoSkillMetadataTable(
            Dictionary<int, RoSkillMetadata> skills,
            Dictionary<string, int> constants,
            string skillIdSourcePath,
            string skillInfoSourcePath,
            string skillDescriptionSourcePath,
            string skillDbSourcePath,
            int skillDbCount,
            string loadError,
            bool loadedFromDiskCache = false
        ) {
            this.skills = skills ?? new Dictionary<int, RoSkillMetadata>();
            this.constants = constants ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            SkillIdSourcePath = skillIdSourcePath;
            SkillInfoSourcePath = skillInfoSourcePath;
            SkillDescriptionSourcePath = skillDescriptionSourcePath;
            SkillDbSourcePath = skillDbSourcePath;
            SkillDbCount = Math.Max(0, skillDbCount);
            LoadError = loadError;
            LoadedFromDiskCache = loadedFromDiskCache;
        }

        public IReadOnlyDictionary<int, RoSkillMetadata> Skills => skills;
        public IReadOnlyDictionary<string, int> Constants => constants;
        public int Count => skills.Count;
        public int ConstantCount => constants.Count;
        public string SkillIdSourcePath { get; }
        public string SkillInfoSourcePath { get; }
        public string SkillDescriptionSourcePath { get; }
        public string SkillDbSourcePath { get; }
        public int SkillDbCount { get; }
        public string LoadError { get; }
        public bool IsLoaded => Count > 0;
        public bool HasDatabaseMetadata => SkillDbCount > 0;
        public bool LoadedFromDiskCache { get; }

        public static RoSkillMetadataTable Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            string error = null;
            string skillIdText = TryReadFirstTextTable(
                assets,
                GetSkillIdCandidatePaths(),
                out string skillIdSourcePath,
                out byte[] skillIdBytes,
                ref error);
            string skillInfoText = TryReadFirstTextTable(
                assets,
                GetSkillInfoCandidatePaths(),
                out string skillInfoSourcePath,
                out byte[] skillInfoBytes,
                ref error);
            string skillDescriptionText = TryReadFirstTextTable(
                assets,
                GetSkillDescriptionCandidatePaths(),
                out string skillDescriptionSourcePath,
                out byte[] skillDescriptionBytes,
                ref error);
            RoSkillDbMetadataTable skillDb = RoSkillDbMetadataTable.Load(
                assets,
                out string skillDbSourcePath,
                out byte[] skillDbBytes);

            if (TryReadSkillMetadataCache(
                    assets,
                    skillIdSourcePath,
                    skillIdBytes,
                    skillInfoSourcePath,
                    skillInfoBytes,
                    skillDescriptionSourcePath,
                    skillDescriptionBytes,
                    skillDbSourcePath,
                    skillDbBytes,
                    out RoSkillMetadataTable cachedTable)) {
                return cachedTable;
            }

            RoSkillMetadataTable table = FromText(skillIdText, skillInfoText, skillDescriptionText, skillIdSourcePath, skillInfoSourcePath, skillDescriptionSourcePath, error);
            table = table.WithDatabaseMetadata(skillDb);
            TryWriteSkillMetadataCache(
                assets,
                skillIdSourcePath,
                skillIdBytes,
                skillInfoSourcePath,
                skillInfoBytes,
                skillDescriptionSourcePath,
                skillDescriptionBytes,
                skillDbSourcePath,
                skillDbBytes,
                table);
            return table;
        }

        public static RoSkillMetadataTable FromText(
            string skillIdText,
            string skillInfoText,
            string skillDescriptionText,
            string skillIdSourcePath = null,
            string skillInfoSourcePath = null,
            string skillDescriptionSourcePath = null,
            string loadError = null
        ) {
            var constants = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            ParseConstants(skillIdText, constants);

            var descriptions = new Dictionary<int, string>();
            ParseDescriptions(skillDescriptionText, constants, descriptions);

            var skills = new Dictionary<int, RoSkillMetadata>();
            ParseSkillInfo(skillInfoText, constants, descriptions, skills);

            return new RoSkillMetadataTable(skills, constants, skillIdSourcePath, skillInfoSourcePath, skillDescriptionSourcePath, null, 0, loadError, false);
        }

        public bool TryGetSkill(int skillId, out RoSkillMetadata skill) {
            return skills.TryGetValue(skillId, out skill);
        }

        public bool TryGetSkill(string aegisName, out RoSkillMetadata skill) {
            skill = null;
            if (string.IsNullOrWhiteSpace(aegisName)) {
                return false;
            }

            if (!constants.TryGetValue(NormalizeLuaKey(aegisName), out int skillId)) {
                return false;
            }

            return skills.TryGetValue(skillId, out skill);
        }

        public RoSkillMetadata GetSkillOrFallback(int skillId) {
            if (skills.TryGetValue(skillId, out RoSkillMetadata skill)) {
                return skill;
            }

            return new RoSkillMetadata(
                skillId,
                "SKILL_" + Math.Max(0, skillId).ToString(CultureInfo.InvariantCulture),
                "Unknown Skill",
                0,
                false,
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<RoSkillRequirement>(),
                string.Empty
            );
        }

        public RoSkillMetadataTable WithDatabaseMetadata(RoSkillDbMetadataTable skillDb) {
            if (skillDb == null || skillDb.Count == 0) {
                return new RoSkillMetadataTable(
                    new Dictionary<int, RoSkillMetadata>(skills),
                    new Dictionary<string, int>(constants, StringComparer.OrdinalIgnoreCase),
                    SkillIdSourcePath,
                    SkillInfoSourcePath,
                    SkillDescriptionSourcePath,
                    skillDb?.SourcePath,
                    0,
                    skillDb?.LoadError ?? LoadError,
                    false
                );
            }

            var merged = new Dictionary<int, RoSkillMetadata>(skills);
            var mergedConstants = new Dictionary<string, int>(constants, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<int, RoSkillDbMetadata> entry in skillDb.Skills) {
                if (merged.TryGetValue(entry.Key, out RoSkillMetadata existing)) {
                    merged[entry.Key] = existing.WithDatabaseMetadata(entry.Value);
                } else {
                    RoSkillMetadata metadata = RoSkillMetadata.FromDatabaseMetadata(entry.Value);
                    if (metadata != null) {
                        merged[entry.Key] = metadata;
                    }
                }

                if (!string.IsNullOrEmpty(entry.Value.AegisName)) {
                    mergedConstants[NormalizeLuaKey(entry.Value.AegisName)] = entry.Key;
                }
            }

            return new RoSkillMetadataTable(
                merged,
                mergedConstants,
                SkillIdSourcePath,
                SkillInfoSourcePath,
                SkillDescriptionSourcePath,
                skillDb.SourcePath,
                skillDb.Count,
                string.IsNullOrEmpty(skillDb.LoadError) ? LoadError : skillDb.LoadError,
                false
            );
        }

        public static string[] GetSkillIdCandidatePaths() {
            return LuaSkillInfo("skillid");
        }

        public static string[] GetSkillInfoCandidatePaths() {
            return LuaSkillInfo("skillinfolist", "skillinfo_f");
        }

        public static string[] GetSkillDescriptionCandidatePaths() {
            return LuaSkillInfo("skilldescript");
        }

        private static void ParseConstants(string text, Dictionary<string, int> output) {
            if (string.IsNullOrWhiteSpace(text) || output == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match match in ConstantAssignmentPattern.Matches(stripped)) {
                string key = NormalizeLuaKey(match.Groups["key"].Value);
                if (string.IsNullOrEmpty(key) || !TryParseInteger(match.Groups["value"].Value, out int value)) {
                    continue;
                }

                output[key] = value;
            }
        }

        private static void ParseDescriptions(string text, IReadOnlyDictionary<string, int> constants, Dictionary<int, string> output) {
            if (string.IsNullOrWhiteSpace(text) || output == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match match in IndexedValueStartPattern.Matches(stripped)) {
                if (!TryResolveTableKey(match.Groups["key"].Value, constants, out int skillId)) {
                    continue;
                }

                int openIndex = stripped.IndexOf(match.Groups["open"].Value, match.Index + match.Length - 1, StringComparison.Ordinal);
                if (openIndex < 0) {
                    continue;
                }

                int closeIndex = FindMatching(stripped, openIndex);
                if (closeIndex <= openIndex) {
                    continue;
                }

                string body = stripped.Substring(openIndex + 1, closeIndex - openIndex - 1);
                var lines = new List<string>();
                foreach (Match quoted in QuotedStringPattern.Matches(body)) {
                    lines.Add(UnescapeLuaString(quoted.Groups["value"].Value));
                }

                if (lines.Count > 0) {
                    output[skillId] = string.Join("\n", lines);
                }
            }
        }

        private static void ParseSkillInfo(
            string text,
            IReadOnlyDictionary<string, int> constants,
            IReadOnlyDictionary<int, string> descriptions,
            Dictionary<int, RoSkillMetadata> output
        ) {
            if (string.IsNullOrWhiteSpace(text) || output == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match match in IndexedBlockStartPattern.Matches(stripped)) {
                if (!TryResolveTableKey(match.Groups["key"].Value, constants, out int skillId)) {
                    continue;
                }

                int openIndex = stripped.IndexOf('{', match.Index + match.Length - 1);
                if (openIndex < 0) {
                    continue;
                }

                int closeIndex = FindMatching(stripped, openIndex);
                if (closeIndex <= openIndex) {
                    continue;
                }

                string body = stripped.Substring(openIndex + 1, closeIndex - openIndex - 1);
                RoSkillMetadata skill = ParseSkillRecord(skillId, body, constants, descriptions);
                if (skill != null) {
                    output[skillId] = skill;
                }
            }
        }

        private static RoSkillMetadata ParseSkillRecord(
            int skillId,
            string body,
            IReadOnlyDictionary<string, int> constants,
            IReadOnlyDictionary<int, string> descriptions
        ) {
            string aegisName = ReadStringField(body, "Name");
            if (string.IsNullOrEmpty(aegisName)) {
                aegisName = ResolveAegisName(skillId, constants);
            }

            string displayName = ReadStringField(body, "SkillName");
            int maxLevel = ReadIntField(body, "MaxLv");
            if (maxLevel <= 0) {
                maxLevel = ReadIntField(body, "MaxLevel");
            }

            IReadOnlyList<int> spCosts = ReadIntArrayField(body, "SpAmount");
            IReadOnlyList<int> attackRanges = ReadIntArrayField(body, "AttackRange");
            bool canSelectLevel = ReadBoolField(body, "bSeperateLv") || ReadBoolField(body, "bSeparateLv");
            string actionType = ReadStringField(body, "ActionType");
            string skillType = ReadStringField(body, "Type");
            IReadOnlyList<RoSkillRequirement> prerequisites = ReadPrerequisites(body, constants);
            descriptions.TryGetValue(skillId, out string description);

            if (string.IsNullOrEmpty(aegisName) &&
                string.IsNullOrEmpty(displayName) &&
                maxLevel <= 0 &&
                spCosts.Count == 0 &&
                attackRanges.Count == 0 &&
                string.IsNullOrEmpty(description)) {
                return null;
            }

            return new RoSkillMetadata(
                skillId,
                aegisName,
                displayName,
                maxLevel,
                canSelectLevel,
                spCosts,
                attackRanges,
                prerequisites,
                description,
                skillType,
                RoSkillTargetType.Unknown,
                actionType: actionType
            );
        }

        private static IReadOnlyList<RoSkillRequirement> ReadPrerequisites(string body, IReadOnlyDictionary<string, int> constants) {
            string block = ReadCollectionBodyField(body, "_NeedSkillList");
            if (string.IsNullOrEmpty(block)) {
                return Array.Empty<RoSkillRequirement>();
            }

            var requirements = new List<RoSkillRequirement>();
            foreach (Match match in SkillRequirementPattern.Matches(block)) {
                if (!TryResolveTableKey(match.Groups["skill"].Value, constants, out int skillId) || skillId <= 0) {
                    continue;
                }

                int level = 1;
                if (match.Groups["level"].Success) {
                    TryParseInteger(match.Groups["level"].Value, out level);
                }

                requirements.Add(new RoSkillRequirement(skillId, level));
            }

            return requirements;
        }

        private static string ReadStringField(string body, string fieldName) {
            int valueStart = FindFieldValueStart(body, fieldName);
            if (valueStart < 0) {
                return string.Empty;
            }

            Match match = QuotedStringPattern.Match(body, valueStart);
            if (!match.Success || match.Index != valueStart) {
                return string.Empty;
            }

            return match.Success ? UnescapeLuaString(match.Groups["value"].Value) : string.Empty;
        }

        private static int ReadIntField(string body, string fieldName) {
            int valueStart = FindFieldValueStart(body, fieldName);
            return valueStart >= 0 && TryParseIntegerAt(body, valueStart, out int value) ? value : 0;
        }

        private static bool ReadBoolField(string body, string fieldName) {
            int valueStart = FindFieldValueStart(body, fieldName);
            return valueStart >= 0 && MatchesKeyword(body, valueStart, "true");
        }

        private static IReadOnlyList<int> ReadIntArrayField(string body, string fieldName) {
            string valueBody = ReadCollectionBodyField(body, fieldName);
            if (string.IsNullOrEmpty(valueBody)) {
                return Array.Empty<int>();
            }

            var values = new List<int>();
            for (int i = 0; i < valueBody.Length; i++) {
                char current = valueBody[i];
                if (current != '-' && !char.IsDigit(current)) {
                    continue;
                }

                if (TryParseIntegerAt(valueBody, i, out int value, out int endIndex)) {
                    values.Add(value);
                    i = endIndex - 1;
                }
            }

            return values;
        }

        private static string ReadCollectionBodyField(string body, string fieldName) {
            int valueStart = FindFieldValueStart(body, fieldName);
            if (valueStart < 0 || valueStart >= body.Length) {
                return null;
            }

            char open = body[valueStart];
            if (open != '{' && open != '[') {
                return null;
            }

            int closeIndex = FindMatching(body, valueStart);
            return closeIndex > valueStart ? body.Substring(valueStart + 1, closeIndex - valueStart - 1) : null;
        }

        private static int FindFieldValueStart(string body, string fieldName) {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(fieldName)) {
                return -1;
            }

            int searchIndex = 0;
            while (searchIndex < body.Length) {
                int fieldIndex = body.IndexOf(fieldName, searchIndex, StringComparison.Ordinal);
                if (fieldIndex < 0) {
                    return -1;
                }

                if (fieldIndex > 0 && IsIdentifierCharacter(body[fieldIndex - 1])) {
                    searchIndex = fieldIndex + fieldName.Length;
                    continue;
                }

                int index = fieldIndex + fieldName.Length;
                while (index < body.Length && char.IsWhiteSpace(body[index])) {
                    index++;
                }

                if (index < body.Length && (body[index] == ':' || body[index] == '=')) {
                    index++;
                    while (index < body.Length && char.IsWhiteSpace(body[index])) {
                        index++;
                    }

                    return index;
                }

                searchIndex = fieldIndex + fieldName.Length;
            }

            return -1;
        }

        private static bool TryParseIntegerAt(string text, int start, out int value) {
            return TryParseIntegerAt(text, start, out value, out _);
        }

        private static bool TryParseIntegerAt(string text, int start, out int value, out int endIndex) {
            value = 0;
            endIndex = start;
            if (string.IsNullOrEmpty(text) || start < 0 || start >= text.Length) {
                return false;
            }

            int index = start;
            if (text[index] == '-') {
                index++;
                if (index >= text.Length) {
                    return false;
                }
            }

            if (index + 1 < text.Length &&
                text[index] == '0' &&
                (text[index + 1] == 'x' || text[index + 1] == 'X')) {
                if (index != start) {
                    return false;
                }

                int hexStart = index + 2;
                int hexEnd = hexStart;
                while (hexEnd < text.Length && IsHexDigit(text[hexEnd])) {
                    hexEnd++;
                }

                endIndex = hexEnd;
                return hexEnd > hexStart &&
                    int.TryParse(text.Substring(hexStart, hexEnd - hexStart), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            int end = index;
            while (end < text.Length && char.IsDigit(text[end])) {
                end++;
            }

            endIndex = end;
            return end > index &&
                int.TryParse(text.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool MatchesKeyword(string text, int start, string keyword) {
            if (string.IsNullOrEmpty(text) ||
                string.IsNullOrEmpty(keyword) ||
                start < 0 ||
                start + keyword.Length > text.Length) {
                return false;
            }

            if (string.Compare(text, start, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0) {
                return false;
            }

            int end = start + keyword.Length;
            return end >= text.Length || !IsIdentifierCharacter(text[end]);
        }

        private static bool IsIdentifierCharacter(char value) {
            return (value >= 'A' && value <= 'Z') ||
                (value >= 'a' && value <= 'z') ||
                (value >= '0' && value <= '9') ||
                value == '_';
        }

        private static bool IsHexDigit(char value) {
            return (value >= '0' && value <= '9') ||
                (value >= 'A' && value <= 'F') ||
                (value >= 'a' && value <= 'f');
        }

        private static string ResolveAegisName(int skillId, IReadOnlyDictionary<string, int> constants) {
            if (constants != null) {
                foreach (KeyValuePair<string, int> entry in constants) {
                    if (entry.Value == skillId) {
                        return entry.Key;
                    }
                }
            }

            return "SKILL_" + Math.Max(0, skillId).ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryResolveTableKey(string rawKey, IReadOnlyDictionary<string, int> constants, out int id) {
            id = 0;
            string key = NormalizeLuaKey(rawKey);
            if (string.IsNullOrEmpty(key)) {
                return false;
            }

            if (TryParseInteger(key, out id)) {
                return true;
            }

            return constants != null && constants.TryGetValue(key, out id);
        }

        private static bool TryParseInteger(string text, out int value) {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) {
                return false;
            }

            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                return int.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string NormalizeLuaKey(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                return null;
            }

            key = key.Trim();
            if ((key.StartsWith("\"", StringComparison.Ordinal) && key.EndsWith("\"", StringComparison.Ordinal)) ||
                (key.StartsWith("'", StringComparison.Ordinal) && key.EndsWith("'", StringComparison.Ordinal))) {
                key = key.Substring(1, key.Length - 2);
            }

            int dotIndex = key.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex + 1 < key.Length) {
                key = key.Substring(dotIndex + 1);
            }

            return key.Trim();
        }

        private static int FindMatching(string text, int openIndex) {
            if (string.IsNullOrEmpty(text) || openIndex < 0 || openIndex >= text.Length) {
                return -1;
            }

            char open = text[openIndex];
            char close = open == '[' ? ']' : '}';
            int depth = 0;
            bool inString = false;
            char quote = '\0';
            for (int i = openIndex; i < text.Length; i++) {
                char current = text[i];
                if (inString) {
                    if (current == '\\') {
                        i++;
                        continue;
                    }

                    if (current == quote) {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"' || current == '\'') {
                    inString = true;
                    quote = current;
                    continue;
                }

                if (current == open) {
                    depth++;
                    continue;
                }

                if (current == close) {
                    depth--;
                    if (depth == 0) {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string StripLineComments(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                lines[i] = StripComment(lines[i], "--");
                lines[i] = StripComment(lines[i], "//");
            }

            return string.Join("\n", lines);
        }

        private static string StripComment(string line, string token) {
            if (string.IsNullOrEmpty(line)) {
                return string.Empty;
            }

            bool inString = false;
            char quote = '\0';
            for (int i = 0; i <= line.Length - token.Length; i++) {
                char current = line[i];
                if (inString) {
                    if (current == '\\') {
                        i++;
                        continue;
                    }

                    if (current == quote) {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"' || current == '\'') {
                    inString = true;
                    quote = current;
                    continue;
                }

                if (string.Compare(line, i, token, 0, token.Length, StringComparison.Ordinal) == 0) {
                    return line.Substring(0, i);
                }
            }

            return line;
        }

        private static string UnescapeLuaString(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            var chars = new List<char>(value.Length);
            for (int i = 0; i < value.Length; i++) {
                char current = value[i];
                if (current != '\\' || i + 1 >= value.Length) {
                    chars.Add(current);
                    continue;
                }

                char next = value[++i];
                switch (next) {
                    case '\\':
                    case '"':
                    case '\'':
                        chars.Add(next);
                        break;
                    case 'n':
                        chars.Add('\n');
                        break;
                    case 'r':
                        chars.Add('\r');
                        break;
                    case 't':
                        chars.Add('\t');
                        break;
                    case 'x':
                        if (i + 2 < value.Length &&
                            byte.TryParse(value.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte byteValue)) {
                            chars.Add((char) byteValue);
                            i += 2;
                        } else {
                            chars.Add(next);
                        }
                        break;
                    default:
                        chars.Add(next);
                        break;
                }
            }

            string unescaped = new string(chars.ToArray());
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(unescaped);
            return string.IsNullOrEmpty(decoded) ? unescaped : decoded;
        }

        private static string[] LuaSkillInfo(params string[] names) {
            var paths = new List<string>();
            for (int i = 0; i < names.Length; i++) {
                AddLuaPathVariants(paths, "data/luafiles514/lua files/skillinfoz", names[i]);
                AddLuaPathVariants(paths, "data/lua files/skillinfoz", names[i]);
            }

            return paths.ToArray();
        }

        private static void AddLuaPathVariants(List<string> paths, string root, string name) {
            paths.Add(root + "/" + name + ".lub");
            paths.Add(root + "/" + name + ".lua");
        }

        private static string TryReadFirstTextTable(
            RoAssetDatabase assets,
            IReadOnlyList<string> candidatePaths,
            out string sourcePath,
            out byte[] sourceBytes,
            ref string loadError
        ) {
            sourcePath = null;
            sourceBytes = null;
            string pendingError = null;
            if (assets == null || candidatePaths == null) {
                return null;
            }

            for (int i = 0; i < candidatePaths.Count; i++) {
                string candidate = candidatePaths[i];
                if (!assets.Exists(candidate)) {
                    continue;
                }

                byte[] bytes;
                try {
                    bytes = assets.ReadBytes(candidate);
                } catch (Exception ex) {
                    pendingError = ex.Message;
                    continue;
                }

                if (bytes == null || bytes.Length == 0) {
                    continue;
                }

                byte[] text = bytes;
                if (bytes[0] == 0x1B) {
                    text = RoLuaBytecodeSource.ToSource(assets, candidate, bytes, out string luaError);
                    if (text == null) {
                        pendingError = luaError;
                        continue;
                    }
                }

                sourcePath = candidate;
                sourceBytes = bytes;
                return RoTextEncoding.Decode(text);
            }

            if (!string.IsNullOrEmpty(pendingError)) {
                loadError = pendingError;
            }

            return null;
        }

        private static bool TryReadSkillMetadataCache(
            RoAssetDatabase assets,
            string skillIdSourcePath,
            byte[] skillIdBytes,
            string skillInfoSourcePath,
            byte[] skillInfoBytes,
            string skillDescriptionSourcePath,
            byte[] skillDescriptionBytes,
            string skillDbSourcePath,
            byte[] skillDbBytes,
            out RoSkillMetadataTable table
        ) {
            table = null;
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null || !HasAnySourceBytes(skillIdBytes, skillInfoBytes, skillDescriptionBytes, skillDbBytes)) {
                return false;
            }

            string cacheKey = BuildSkillMetadataCacheKey(
                skillIdSourcePath,
                skillIdBytes,
                skillInfoSourcePath,
                skillInfoBytes,
                skillDescriptionSourcePath,
                skillDescriptionBytes,
                skillDbSourcePath,
                skillDbBytes);
            try {
                if (!cache.TryReadBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", out byte[] data)) {
                    return false;
                }

                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadInt32() != SkillMetadataCacheMagic ||
                    reader.ReadInt32() != SkillMetadataCacheVersion) {
                    return false;
                }

                string cachedSkillIdSourcePath = reader.ReadString();
                string cachedSkillInfoSourcePath = reader.ReadString();
                string cachedSkillDescriptionSourcePath = reader.ReadString();
                string cachedSkillDbSourcePath = reader.ReadString();
                if (!IsSameSourcePath(cachedSkillIdSourcePath, skillIdSourcePath) ||
                    !IsSameSourcePath(cachedSkillInfoSourcePath, skillInfoSourcePath) ||
                    !IsSameSourcePath(cachedSkillDescriptionSourcePath, skillDescriptionSourcePath) ||
                    !IsSameSourcePath(cachedSkillDbSourcePath, skillDbSourcePath)) {
                    return false;
                }

                int skillDbCount = reader.ReadInt32();
                if (skillDbCount < 0 || skillDbCount > MaxCachedSkills) {
                    return false;
                }

                int constantCount = reader.ReadInt32();
                if (constantCount < 0 || constantCount > MaxCachedConstants) {
                    return false;
                }

                var constants = new Dictionary<string, int>(constantCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < constantCount; i++) {
                    string key = reader.ReadString();
                    int value = reader.ReadInt32();
                    if (!string.IsNullOrWhiteSpace(key)) {
                        constants[key] = value;
                    }
                }

                int skillCount = reader.ReadInt32();
                if (skillCount < 0 || skillCount > MaxCachedSkills) {
                    return false;
                }

                var skills = new Dictionary<int, RoSkillMetadata>(skillCount);
                for (int i = 0; i < skillCount; i++) {
                    RoSkillMetadata skill = ReadSkillMetadataCacheEntry(reader);
                    skills[skill.SkillId] = skill;
                }

                table = new RoSkillMetadataTable(
                    skills,
                    constants,
                    skillIdSourcePath,
                    skillInfoSourcePath,
                    skillDescriptionSourcePath,
                    skillDbSourcePath,
                    skillDbCount,
                    null,
                    true);
                return true;
            } catch {
                table = null;
                return false;
            }
        }

        private static void TryWriteSkillMetadataCache(
            RoAssetDatabase assets,
            string skillIdSourcePath,
            byte[] skillIdBytes,
            string skillInfoSourcePath,
            byte[] skillInfoBytes,
            string skillDescriptionSourcePath,
            byte[] skillDescriptionBytes,
            string skillDbSourcePath,
            byte[] skillDbBytes,
            RoSkillMetadataTable table
        ) {
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null ||
                table == null ||
                table.Count == 0 ||
                !HasAnySourceBytes(skillIdBytes, skillInfoBytes, skillDescriptionBytes, skillDbBytes)) {
                return;
            }

            string cacheKey = BuildSkillMetadataCacheKey(
                skillIdSourcePath,
                skillIdBytes,
                skillInfoSourcePath,
                skillInfoBytes,
                skillDescriptionSourcePath,
                skillDescriptionBytes,
                skillDbSourcePath,
                skillDbBytes);
            try {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                    writer.Write(SkillMetadataCacheMagic);
                    writer.Write(SkillMetadataCacheVersion);
                    writer.Write(skillIdSourcePath ?? string.Empty);
                    writer.Write(skillInfoSourcePath ?? string.Empty);
                    writer.Write(skillDescriptionSourcePath ?? string.Empty);
                    writer.Write(skillDbSourcePath ?? string.Empty);
                    writer.Write(table.SkillDbCount);
                    writer.Write(table.constants.Count);
                    foreach (KeyValuePair<string, int> constant in table.constants) {
                        writer.Write(constant.Key ?? string.Empty);
                        writer.Write(constant.Value);
                    }

                    writer.Write(table.skills.Count);
                    foreach (RoSkillMetadata skill in table.skills.Values) {
                        WriteSkillMetadataCacheEntry(writer, skill);
                    }
                }

                cache.WriteBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", stream.ToArray());
            } catch {
                // Parsed skill metadata cache is an optimization only; source Lua/YAML remains authoritative.
            }
        }

        private static RoSkillMetadata ReadSkillMetadataCacheEntry(BinaryReader reader) {
            return new RoSkillMetadata(
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                ReadIntList(reader),
                ReadIntList(reader),
                ReadSkillRequirementList(reader),
                reader.ReadString(),
                reader.ReadString(),
                (RoSkillTargetType) reader.ReadInt32(),
                ReadIntList(reader),
                ReadIntList(reader),
                ReadIntList(reader),
                ReadIntList(reader),
                reader.ReadString(),
                reader.ReadString());
        }

        private static void WriteSkillMetadataCacheEntry(BinaryWriter writer, RoSkillMetadata skill) {
            writer.Write(skill.SkillId);
            writer.Write(skill.AegisName ?? string.Empty);
            writer.Write(skill.DisplayName ?? string.Empty);
            writer.Write(skill.MaxLevel);
            writer.Write(skill.CanSelectLevel);
            WriteIntList(writer, skill.SpCosts);
            WriteIntList(writer, skill.AttackRanges);
            WriteSkillRequirementList(writer, skill.Prerequisites);
            writer.Write(skill.DescriptionText ?? string.Empty);
            writer.Write(skill.SkillType ?? string.Empty);
            writer.Write((int) skill.TargetType);
            WriteIntList(writer, skill.CastTimesMs);
            WriteIntList(writer, skill.FixedCastTimesMs);
            WriteIntList(writer, skill.AfterCastActDelaysMs);
            WriteIntList(writer, skill.CooldownsMs);
            writer.Write(skill.ActionType ?? string.Empty);
            writer.Write(skill.IconName ?? string.Empty);
        }

        private static IReadOnlyList<int> ReadIntList(BinaryReader reader) {
            int count = reader.ReadInt32();
            if (count <= 0) {
                return Array.Empty<int>();
            }

            if (count > MaxCachedListEntries) {
                throw new InvalidDataException("Cached skill metadata int list is too large.");
            }

            var values = new int[count];
            for (int i = 0; i < count; i++) {
                values[i] = reader.ReadInt32();
            }

            return values;
        }

        private static void WriteIntList(BinaryWriter writer, IReadOnlyList<int> values) {
            writer.Write(values?.Count ?? 0);
            if (values == null) {
                return;
            }

            for (int i = 0; i < values.Count; i++) {
                writer.Write(values[i]);
            }
        }

        private static IReadOnlyList<RoSkillRequirement> ReadSkillRequirementList(BinaryReader reader) {
            int count = reader.ReadInt32();
            if (count <= 0) {
                return Array.Empty<RoSkillRequirement>();
            }

            if (count > MaxCachedListEntries) {
                throw new InvalidDataException("Cached skill metadata requirement list is too large.");
            }

            var values = new RoSkillRequirement[count];
            for (int i = 0; i < count; i++) {
                values[i] = new RoSkillRequirement(reader.ReadInt32(), reader.ReadInt32());
            }

            return values;
        }

        private static void WriteSkillRequirementList(BinaryWriter writer, IReadOnlyList<RoSkillRequirement> values) {
            writer.Write(values?.Count ?? 0);
            if (values == null) {
                return;
            }

            for (int i = 0; i < values.Count; i++) {
                writer.Write(values[i].SkillId);
                writer.Write(values[i].Level);
            }
        }

        private static string BuildSkillMetadataCacheKey(
            string skillIdSourcePath,
            byte[] skillIdBytes,
            string skillInfoSourcePath,
            byte[] skillInfoBytes,
            string skillDescriptionSourcePath,
            byte[] skillDescriptionBytes,
            string skillDbSourcePath,
            byte[] skillDbBytes
        ) {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                WriteSourceBytesForCacheKey(writer, skillIdSourcePath, skillIdBytes);
                WriteSourceBytesForCacheKey(writer, skillInfoSourcePath, skillInfoBytes);
                WriteSourceBytesForCacheKey(writer, skillDescriptionSourcePath, skillDescriptionBytes);
                WriteSourceBytesForCacheKey(writer, skillDbSourcePath, skillDbBytes);
            }

            string sourcePathKey = string.Join(
                "|",
                skillIdSourcePath ?? string.Empty,
                skillInfoSourcePath ?? string.Empty,
                skillDescriptionSourcePath ?? string.Empty,
                skillDbSourcePath ?? string.Empty);
            return RoItemMetadataTable.BuildParsedSourceCacheKey("skill-metadata", sourcePathKey, stream.ToArray());
        }

        private static void WriteSourceBytesForCacheKey(BinaryWriter writer, string sourcePath, byte[] sourceBytes) {
            writer.Write(sourcePath ?? string.Empty);
            if (sourceBytes == null) {
                writer.Write(-1);
                return;
            }

            writer.Write(sourceBytes.Length);
            writer.Write(sourceBytes);
        }

        private static bool HasAnySourceBytes(params byte[][] sources) {
            for (int i = 0; i < sources.Length; i++) {
                if (sources[i] != null && sources[i].Length > 0) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameSourcePath(string cachedPath, string sourcePath) {
            return string.Equals(cachedPath ?? string.Empty, sourcePath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class RoSkillDbMetadata {
        public RoSkillDbMetadata(
            int skillId,
            string aegisName,
            string displayName,
            int maxLevel,
            string skillType,
            RoSkillTargetType targetType,
            IReadOnlyList<int> attackRanges,
            IReadOnlyList<int> spCosts,
            IReadOnlyList<int> castTimesMs,
            IReadOnlyList<int> fixedCastTimesMs,
            IReadOnlyList<int> afterCastActDelaysMs,
            IReadOnlyList<int> cooldownsMs
        ) {
            SkillId = Math.Max(0, skillId);
            AegisName = NormalizeText(aegisName);
            DisplayName = NormalizeText(displayName);
            MaxLevel = Math.Max(0, maxLevel);
            SkillType = NormalizeText(skillType);
            TargetType = targetType;
            AttackRanges = CopyIntList(attackRanges, clampNegative: false);
            SpCosts = CopyIntList(spCosts, clampNegative: true);
            CastTimesMs = CopyIntList(castTimesMs, clampNegative: true);
            FixedCastTimesMs = CopyIntList(fixedCastTimesMs, clampNegative: true);
            AfterCastActDelaysMs = CopyIntList(afterCastActDelaysMs, clampNegative: true);
            CooldownsMs = CopyIntList(cooldownsMs, clampNegative: true);
        }

        public int SkillId { get; }
        public string AegisName { get; }
        public string DisplayName { get; }
        public int MaxLevel { get; }
        public string SkillType { get; }
        public RoSkillTargetType TargetType { get; }
        public IReadOnlyList<int> AttackRanges { get; }
        public IReadOnlyList<int> SpCosts { get; }
        public IReadOnlyList<int> CastTimesMs { get; }
        public IReadOnlyList<int> FixedCastTimesMs { get; }
        public IReadOnlyList<int> AfterCastActDelaysMs { get; }
        public IReadOnlyList<int> CooldownsMs { get; }

        private static IReadOnlyList<int> CopyIntList(IReadOnlyList<int> values, bool clampNegative) {
            if (values == null || values.Count == 0) {
                return Array.Empty<int>();
            }

            var copy = new int[values.Count];
            for (int i = 0; i < values.Count; i++) {
                copy[i] = clampNegative ? Math.Max(0, values[i]) : values[i];
            }

            return copy;
        }

        private static string NormalizeText(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(value);
            return string.IsNullOrEmpty(decoded) ? value : decoded;
        }
    }

    public sealed class RoSkillDbMetadataTable {
        private static readonly Regex RecordStartPattern = new Regex(
            @"^\s*-\s+Id\s*:\s*(?<id>\d+)\s*$",
            RegexOptions.Compiled
        );

        private static readonly Regex FieldPattern = new Regex(
            @"^(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.*)$",
            RegexOptions.Compiled
        );

        private static readonly Regex LevelPattern = new Regex(
            @"^-\s+Level\s*:\s*(?<level>-?(?:0x[0-9A-Fa-f]+|\d+))\s*$",
            RegexOptions.Compiled
        );

        private readonly Dictionary<int, RoSkillDbMetadata> skills;

        public static readonly RoSkillDbMetadataTable Empty = new RoSkillDbMetadataTable(
            new Dictionary<int, RoSkillDbMetadata>(),
            null,
            null
        );

        private RoSkillDbMetadataTable(Dictionary<int, RoSkillDbMetadata> skills, string sourcePath, string loadError) {
            this.skills = skills ?? new Dictionary<int, RoSkillDbMetadata>();
            SourcePath = sourcePath;
            LoadError = loadError;
        }

        public IReadOnlyDictionary<int, RoSkillDbMetadata> Skills => skills;
        public int Count => skills.Count;
        public string SourcePath { get; }
        public string LoadError { get; }
        public bool IsLoaded => Count > 0;

        public static RoSkillDbMetadataTable Load(RoAssetDatabase assets) {
            return Load(assets, out _, out _);
        }

        internal static RoSkillDbMetadataTable Load(
            RoAssetDatabase assets,
            out string loadedSourcePath,
            out byte[] loadedSourceBytes
        ) {
            loadedSourcePath = null;
            loadedSourceBytes = null;
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            string lastError = null;
            string fallbackSourcePath = null;
            byte[] fallbackSourceBytes = null;
            string[][] candidateGroups = GetSkillDbCandidatePathGroups();
            for (int groupIndex = 0; groupIndex < candidateGroups.Length; groupIndex++) {
                var parsed = new Dictionary<int, RoSkillDbMetadata>();
                var loadedSources = new List<string>();
                using var sourceBytes = new MemoryStream();
                using var sourceWriter = new BinaryWriter(sourceBytes, Encoding.UTF8, leaveOpen: true);
                bool foundAnyCandidate = false;

                string[] group = candidateGroups[groupIndex];
                for (int i = 0; i < group.Length; i++) {
                    string candidate = group[i];
                    if (!assets.Exists(candidate)) {
                        continue;
                    }

                    foundAnyCandidate = true;
                    try {
                        byte[] bytes = assets.ReadBytes(candidate);
                        if (bytes == null || bytes.Length == 0) {
                            continue;
                        }

                        WriteSkillDbSourceBytesForCacheKey(sourceWriter, candidate, bytes);
                        RoSkillDbMetadataTable table = FromYaml(RoTextEncoding.Decode(bytes), candidate);
                        foreach (KeyValuePair<int, RoSkillDbMetadata> skill in table.Skills) {
                            parsed[skill.Key] = skill.Value;
                        }

                        loadedSources.Add(candidate);
                    } catch (Exception ex) {
                        lastError = ex.Message;
                    }
                }

                if (loadedSources.Count > 0 && fallbackSourcePath == null) {
                    fallbackSourcePath = string.Join("; ", loadedSources);
                    fallbackSourceBytes = sourceBytes.ToArray();
                }

                if (parsed.Count > 0) {
                    loadedSourcePath = string.Join("; ", loadedSources);
                    loadedSourceBytes = sourceBytes.ToArray();
                    return new RoSkillDbMetadataTable(parsed, loadedSourcePath, null);
                }

                if (foundAnyCandidate && string.IsNullOrEmpty(lastError)) {
                    lastError = "no skill db records parsed";
                }
            }

            loadedSourcePath = fallbackSourcePath;
            loadedSourceBytes = fallbackSourceBytes;
            return new RoSkillDbMetadataTable(new Dictionary<int, RoSkillDbMetadata>(), null, lastError);
        }

        public static RoSkillDbMetadataTable FromYaml(string text, string sourcePath = null) {
            var parsed = new Dictionary<int, RoSkillDbMetadata>();
            if (string.IsNullOrWhiteSpace(text)) {
                return new RoSkillDbMetadataTable(parsed, sourcePath, null);
            }

            foreach (string record in EnumerateBodyRecords(text)) {
                RoSkillDbMetadata skill = ParseRecord(record);
                if (skill != null) {
                    parsed[skill.SkillId] = skill;
                }
            }

            return new RoSkillDbMetadataTable(parsed, sourcePath, null);
        }

        public bool TryGetSkill(int skillId, out RoSkillDbMetadata skill) {
            return skills.TryGetValue(skillId, out skill);
        }

        public static string[][] GetSkillDbCandidatePathGroups() {
            return new[] {
                new[] { "db/pre-re/skill_db.yml", "db/pre-re/skill_db.yaml" },
                new[] { "db/skill_db.yml", "db/skill_db.yaml" },
                new[] { "skill_db.yml", "skill_db.yaml" },
                new[] { "db/re/skill_db.yml", "db/re/skill_db.yaml" },
            };
        }

        private static void WriteSkillDbSourceBytesForCacheKey(BinaryWriter writer, string sourcePath, byte[] sourceBytes) {
            writer.Write(sourcePath ?? string.Empty);
            if (sourceBytes == null) {
                writer.Write(-1);
                return;
            }

            writer.Write(sourceBytes.Length);
            writer.Write(sourceBytes);
        }

        private static IEnumerable<string> EnumerateBodyRecords(string text) {
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            bool inBody = false;
            StringBuilder current = null;

            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];
                if (!inBody) {
                    if (string.Equals(line.Trim(), "Body:", StringComparison.Ordinal)) {
                        inBody = true;
                    }

                    continue;
                }

                if (RecordStartPattern.IsMatch(line)) {
                    if (current != null && current.Length > 0) {
                        yield return current.ToString();
                    }

                    current = new StringBuilder();
                    current.AppendLine(line);
                    continue;
                }

                if (current != null) {
                    current.AppendLine(line);
                }
            }

            if (current != null && current.Length > 0) {
                yield return current.ToString();
            }
        }

        private static RoSkillDbMetadata ParseRecord(string record) {
            int id = 0;
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var series = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
            string activeSeries = null;
            int activeSeriesIndent = -1;
            int activeLevel = 0;

            string[] lines = record.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                string rawLine = StripComment(lines[i]);
                if (string.IsNullOrWhiteSpace(rawLine)) {
                    continue;
                }

                int indent = CountLeadingSpaces(rawLine);
                string trimmed = rawLine.Trim();
                Match recordStart = RecordStartPattern.Match(rawLine);
                if (recordStart.Success) {
                    TryParseInteger(recordStart.Groups["id"].Value, out id);
                    activeSeries = null;
                    activeSeriesIndent = -1;
                    activeLevel = 0;
                    continue;
                }

                if (!string.IsNullOrEmpty(activeSeries) && indent > activeSeriesIndent) {
                    Match levelMatch = LevelPattern.Match(trimmed);
                    if (levelMatch.Success) {
                        TryParseInteger(levelMatch.Groups["level"].Value, out activeLevel);
                        continue;
                    }

                    Match nestedField = FieldPattern.Match(trimmed);
                    if (nestedField.Success && activeLevel > 0 && IsSeriesValueField(nestedField.Groups["field"].Value)) {
                        if (TryParseInteger(CleanYamlScalar(nestedField.Groups["value"].Value), out int value)) {
                            EnsureSeries(series, activeSeries)[activeLevel] = value;
                        }

                        continue;
                    }
                }

                Match fieldMatch = FieldPattern.Match(trimmed);
                if (!fieldMatch.Success) {
                    continue;
                }

                string field = fieldMatch.Groups["field"].Value;
                string valueText = CleanYamlScalar(fieldMatch.Groups["value"].Value);

                if (string.Equals(field, "SpCost", StringComparison.OrdinalIgnoreCase) && indent > 4) {
                    if (TryParseInteger(valueText, out int spCost)) {
                        fields["SpCost"] = spCost.ToString(CultureInfo.InvariantCulture);
                        activeSeries = null;
                    } else {
                        activeSeries = "SpCost";
                        activeSeriesIndent = indent;
                        activeLevel = 0;
                    }

                    continue;
                }

                if (indent <= 4) {
                    if (IsSeriesField(field)) {
                        if (TryParseInteger(valueText, out int scalar)) {
                            fields[field] = scalar.ToString(CultureInfo.InvariantCulture);
                            activeSeries = null;
                        } else {
                            activeSeries = field;
                            activeSeriesIndent = indent;
                            activeLevel = 0;
                        }
                    } else {
                        fields[field] = valueText;
                        activeSeries = null;
                    }
                }
            }

            if (id <= 0) {
                return null;
            }

            return new RoSkillDbMetadata(
                id,
                ReadString(fields, "Name"),
                ReadString(fields, "Description"),
                ReadInt(fields, "MaxLevel"),
                ReadString(fields, "Type"),
                ParseTargetType(ReadString(fields, "TargetType")),
                ReadSeries(fields, series, "Range", allowNegative: true),
                ReadSeries(fields, series, "SpCost", allowNegative: false),
                ReadSeries(fields, series, "CastTime", allowNegative: false),
                ReadSeries(fields, series, "FixedCastTime", allowNegative: false),
                ReadSeries(fields, series, "AfterCastActDelay", allowNegative: false),
                ReadSeries(fields, series, "Cooldown", allowNegative: false)
            );
        }

        private static Dictionary<int, int> EnsureSeries(Dictionary<string, Dictionary<int, int>> series, string key) {
            if (!series.TryGetValue(key, out Dictionary<int, int> values)) {
                values = new Dictionary<int, int>();
                series[key] = values;
            }

            return values;
        }

        private static IReadOnlyList<int> ReadSeries(
            IReadOnlyDictionary<string, string> fields,
            IReadOnlyDictionary<string, Dictionary<int, int>> series,
            string key,
            bool allowNegative
        ) {
            if (fields.TryGetValue(key, out string scalarText) && TryParseInteger(scalarText, out int scalar)) {
                return new[] { allowNegative ? scalar : Math.Max(0, scalar) };
            }

            if (!series.TryGetValue(key, out Dictionary<int, int> values) || values.Count == 0) {
                return Array.Empty<int>();
            }

            int maxLevel = 0;
            foreach (int level in values.Keys) {
                maxLevel = Math.Max(maxLevel, level);
            }

            var output = new int[maxLevel];
            int last = 0;
            for (int level = 1; level <= maxLevel; level++) {
                if (values.TryGetValue(level, out int value)) {
                    last = value;
                }

                output[level - 1] = allowNegative ? last : Math.Max(0, last);
            }

            return output;
        }

        private static bool IsSeriesField(string field) {
            return string.Equals(field, "Range", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(field, "CastTime", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(field, "FixedCastTime", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(field, "AfterCastActDelay", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(field, "Cooldown", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSeriesValueField(string field) {
            return string.Equals(field, "Size", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(field, "Time", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(field, "Amount", StringComparison.OrdinalIgnoreCase);
        }

        private static RoSkillTargetType ParseTargetType(string value) {
            switch (NormalizeKey(value)) {
                case "passive":
                    return RoSkillTargetType.Passive;
                case "attack":
                case "enemy":
                    return RoSkillTargetType.Attack;
                case "ground":
                case "place":
                    return RoSkillTargetType.Ground;
                case "self":
                    return RoSkillTargetType.Self;
                case "support":
                    return RoSkillTargetType.Support;
                case "friend":
                    return RoSkillTargetType.Friend;
                case "trap":
                    return RoSkillTargetType.Trap;
                case "pet":
                    return RoSkillTargetType.Pet;
                case "any":
                    return RoSkillTargetType.Any;
                default:
                    return RoSkillTargetType.Unknown;
            }
        }

        private static string NormalizeKey(string value) {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();
        }

        private static string ReadString(IReadOnlyDictionary<string, string> fields, string key) {
            return fields.TryGetValue(key, out string value) ? value : string.Empty;
        }

        private static int ReadInt(IReadOnlyDictionary<string, string> fields, string key) {
            return fields.TryGetValue(key, out string value) && TryParseInteger(value, out int parsed) ? parsed : 0;
        }

        private static bool TryParseInteger(string text, out int value) {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) {
                return false;
            }

            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                return int.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string CleanYamlScalar(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            value = value.Trim();
            if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || value == "~") {
                return string.Empty;
            }

            if ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
                (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))) {
                value = value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static string StripComment(string line) {
            if (string.IsNullOrEmpty(line)) {
                return string.Empty;
            }

            bool inString = false;
            char quote = '\0';
            for (int i = 0; i < line.Length; i++) {
                char current = line[i];
                if (inString) {
                    if (current == '\\') {
                        i++;
                        continue;
                    }

                    if (current == quote) {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"' || current == '\'') {
                    inString = true;
                    quote = current;
                    continue;
                }

                if (current == '#') {
                    return line.Substring(0, i);
                }
            }

            return line;
        }

        private static int CountLeadingSpaces(string value) {
            int count = 0;
            while (count < value.Length && value[count] == ' ') {
                count++;
            }

            return count;
        }
    }
}
