using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public sealed class RoJobMetadata {
        public RoJobMetadata(
            int classId,
            string constantName,
            string resourceName,
            string femaleBodyName,
            string maleBodyName,
            RoItemEquipJob equipJob,
            RoItemEquipClass equipClass
        ) {
            ClassId = Math.Max(0, classId);
            ConstantName = NormalizeConstantName(constantName);
            ResourceName = NormalizeSpriteName(resourceName);
            FemaleBodyName = NormalizeSpriteName(femaleBodyName);
            MaleBodyName = NormalizeSpriteName(maleBodyName);
            EquipJob = equipJob;
            EquipClass = equipClass == RoItemEquipClass.None ? RoItemEquipClass.Normal : equipClass;
        }

        public int ClassId { get; }
        public string ConstantName { get; }
        public string ResourceName { get; }
        public string FemaleBodyName { get; }
        public string MaleBodyName { get; }
        public RoItemEquipJob EquipJob { get; }
        public RoItemEquipClass EquipClass { get; }
        public bool IsPlayerJob => EquipJob != RoItemEquipJob.None;
        public int MaxJobLevel => ResolveMaxJobLevel(ConstantName, EquipClass);

        public RoItemEquipContext CreateEquipContext(int baseLevel, bool isMale) {
            return RoItemEquipContext.ForJob(EquipJob, baseLevel, isMale, EquipClass);
        }

        public string GetBodySpriteBasePath(bool isMale) {
            string bodyName = isMale ? MaleBodyName : FemaleBodyName;
            if (string.IsNullOrEmpty(bodyName)) {
                bodyName = ResourceName;
            }

            if (string.IsNullOrEmpty(bodyName)) {
                return string.Empty;
            }

            string sex = isMale ? MaleSegment() : FemaleSegment();
            return $"data/sprite/{HumanRaceSegment()}/{BodySegment()}/{sex}/{bodyName}_{sex}";
        }

        internal static string NormalizeConstantName(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            value = value.Trim();
            int dotIndex = value.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex + 1 < value.Length) {
                value = value.Substring(dotIndex + 1);
            }

            if (value.StartsWith("JT_", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(3);
            }

            return value.Trim();
        }

        private static string NormalizeSpriteName(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            value = value.Replace('\\', '/').Trim();
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(value);
            if (!string.IsNullOrEmpty(decoded)) {
                value = decoded;
            }

            if (value.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".act", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(0, value.Length - 4);
            }

            int slashIndex = value.LastIndexOf('/');
            return slashIndex >= 0 ? value.Substring(slashIndex + 1) : value;
        }

        public static string FormatJobName(string constantName) {
            if (string.IsNullOrWhiteSpace(constantName)) {
                return string.Empty;
            }

            string value = constantName.Trim();
            if (value.StartsWith("JT_", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(3);
            }

            value = value.Replace('_', ' ').ToLowerInvariant();
            TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
            return textInfo.ToTitleCase(value);
        }

        public static int ResolveMaxJobLevel(string constantName, RoItemEquipClass equipClass) {
            string normalized = (constantName ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized.StartsWith("JT_", StringComparison.Ordinal)) {
                normalized = normalized.Substring(3);
            }

            if (normalized.Contains("NOVICE") && !normalized.Contains("SUPER")) {
                return 10;
            }

            bool third = equipClass == RoItemEquipClass.Third ||
                equipClass == RoItemEquipClass.ThirdUpper ||
                equipClass == RoItemEquipClass.ThirdBaby ||
                normalized.Contains("THIRD") ||
                normalized.StartsWith("RUNE_", StringComparison.Ordinal) ||
                normalized.StartsWith("WARLOCK", StringComparison.Ordinal) ||
                normalized.StartsWith("RANGER", StringComparison.Ordinal) ||
                normalized.StartsWith("ARCHBISHOP", StringComparison.Ordinal) ||
                normalized.StartsWith("MECHANIC", StringComparison.Ordinal) ||
                normalized.StartsWith("GUILLOTINE_", StringComparison.Ordinal) ||
                normalized.StartsWith("ROYAL_GUARD", StringComparison.Ordinal) ||
                normalized.StartsWith("SORCERER", StringComparison.Ordinal) ||
                normalized.StartsWith("MINSTREL", StringComparison.Ordinal) ||
                normalized.StartsWith("WANDERER", StringComparison.Ordinal) ||
                normalized.StartsWith("SURA", StringComparison.Ordinal) ||
                normalized.StartsWith("GENETIC", StringComparison.Ordinal) ||
                normalized.StartsWith("SHADOW_", StringComparison.Ordinal);
            return third ? 70 : 50;
        }

        private static string HumanRaceSegment() {
            return LegacyKorean(0xC0, 0xCE, 0xB0, 0xA3, 0xC1, 0xB7);
        }

        private static string BodySegment() {
            return LegacyKorean(0xB8, 0xF6, 0xC5, 0xEB);
        }

        private static string MaleSegment() {
            return LegacyKorean(0xB3, 0xB2);
        }

        private static string FemaleSegment() {
            return LegacyKorean(0xBF, 0xA9);
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

    public sealed class RoJobMetadataTable {
        private const int JobMetadataCacheMagic = 0x53424F4A; // JOBS
        private const int JobMetadataCacheVersion = 1;
        private const int MaxCachedJobs = 100_000;
        private const int MaxCachedConstants = 100_000;

        private struct GenderedName {
            public GenderedName(string female, string male) {
                Female = female;
                Male = male;
            }

            public string Female { get; }
            public string Male { get; }
        }

        private static readonly Regex ConstantAssignmentPattern = new Regex(
            @"(?<key>[A-Za-z_][A-Za-z0-9_\.]*)\s*[:=]\s*(?<value>-?(?:0x[0-9A-Fa-f]+|\d+))",
            RegexOptions.Compiled
        );

        private static readonly Regex IndexedStringPattern = new Regex(
            @"\[\s*(?<key>[^\]]+)\s*\]\s*=\s*(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>",
            RegexOptions.Compiled | RegexOptions.Singleline
        );

        private static readonly Regex BareStringPattern = new Regex(
            @"(?<![\w\.])(?<key>[A-Za-z_][A-Za-z0-9_\.]*)\s*=\s*(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>",
            RegexOptions.Compiled | RegexOptions.Singleline
        );

        private static readonly Regex IndexedBlockPattern = new Regex(
            @"\[\s*(?<key>[^\]]+)\s*\]\s*=\s*\{(?<body>[^{}]*)\}",
            RegexOptions.Compiled | RegexOptions.Singleline
        );

        private static readonly Regex QuotedStringPattern = new Regex(
            @"(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>",
            RegexOptions.Compiled | RegexOptions.Singleline
        );

        private readonly Dictionary<int, RoJobMetadata> jobs;
        private readonly Dictionary<string, int> constants;

        public static readonly RoJobMetadataTable Empty = new RoJobMetadataTable(
            new Dictionary<int, RoJobMetadata>(),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            null,
            null,
            null,
            null,
            false
        );

        private RoJobMetadataTable(
            Dictionary<int, RoJobMetadata> jobs,
            Dictionary<string, int> constants,
            string jobIdentitySourcePath,
            string jobNameSourcePath,
            string pcJobNameGenderSourcePath,
            string loadError,
            bool loadedFromDiskCache = false
        ) {
            this.jobs = jobs ?? new Dictionary<int, RoJobMetadata>();
            this.constants = constants ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            JobIdentitySourcePath = jobIdentitySourcePath;
            JobNameSourcePath = jobNameSourcePath;
            PcJobNameGenderSourcePath = pcJobNameGenderSourcePath;
            LoadError = loadError;
            LoadedFromDiskCache = loadedFromDiskCache;
        }

        public IReadOnlyDictionary<int, RoJobMetadata> Jobs => jobs;
        public IReadOnlyDictionary<string, int> Constants => constants;
        public int Count => jobs.Count;
        public int ConstantCount => constants.Count;
        public int PlayerJobCount => jobs.Count;
        public string JobIdentitySourcePath { get; }
        public string JobNameSourcePath { get; }
        public string PcJobNameGenderSourcePath { get; }
        public string LoadError { get; }
        public bool IsLoaded => Count > 0;
        public bool LoadedFromDiskCache { get; }

        public static RoJobMetadataTable Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            string error = null;
            string jobIdentityText = TryReadFirstTextTable(
                assets,
                LuaDataInfo("jobidentity"),
                out string jobIdentitySourcePath,
                out byte[] jobIdentityBytes,
                ref error);
            string jobNameText = TryReadFirstTextTable(
                assets,
                LuaDataInfo("jobname", "jobname_f"),
                out string jobNameSourcePath,
                out byte[] jobNameBytes,
                ref error);
            string pcJobNameGenderText = TryReadFirstTextTable(
                assets,
                LuaDataInfo("pcjobnamegender", "pcjobnamegender_f"),
                out string pcJobNameGenderSourcePath,
                out byte[] pcJobNameGenderBytes,
                ref error);

            if (TryReadJobMetadataCache(
                    assets,
                    jobIdentitySourcePath,
                    jobIdentityBytes,
                    jobNameSourcePath,
                    jobNameBytes,
                    pcJobNameGenderSourcePath,
                    pcJobNameGenderBytes,
                    out RoJobMetadataTable cachedTable)) {
                return cachedTable;
            }

            RoJobMetadataTable table = FromText(
                jobIdentityText,
                jobNameText,
                pcJobNameGenderText,
                jobIdentitySourcePath,
                jobNameSourcePath,
                pcJobNameGenderSourcePath,
                error);
            TryWriteJobMetadataCache(
                assets,
                jobIdentitySourcePath,
                jobIdentityBytes,
                jobNameSourcePath,
                jobNameBytes,
                pcJobNameGenderSourcePath,
                pcJobNameGenderBytes,
                table);
            return table;
        }

        public static RoJobMetadataTable FromText(
            string jobIdentityText = null,
            string jobNameText = null,
            string pcJobNameGenderText = null,
            string jobIdentitySourcePath = null,
            string jobNameSourcePath = null,
            string pcJobNameGenderSourcePath = null,
            string loadError = null
        ) {
            var constants = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var constantNamesById = new Dictionary<int, string>();
            ParseConstants(jobIdentityText, constants, constantNamesById);

            var resourceNames = new Dictionary<int, string>();
            ParseIndexedStringTable(jobNameText, constants, resourceNames);

            var genderedNames = new Dictionary<int, GenderedName>();
            ParseIndexedGenderTable(pcJobNameGenderText, constants, genderedNames);

            var jobs = new Dictionary<int, RoJobMetadata>();
            foreach (KeyValuePair<string, int> constant in constants) {
                string constantName = RoJobMetadata.NormalizeConstantName(constant.Key);
                if (!TryClassifyEquipJob(constantName, out RoItemEquipJob equipJob, out RoItemEquipClass equipClass)) {
                    continue;
                }

                int classId = constant.Value;
                resourceNames.TryGetValue(classId, out string resourceName);
                genderedNames.TryGetValue(classId, out GenderedName genderedName);
                jobs[classId] = new RoJobMetadata(
                    classId,
                    constantName,
                    resourceName,
                    genderedName.Female,
                    genderedName.Male,
                    equipJob,
                    equipClass
                );
            }

            foreach (KeyValuePair<int, string> resource in resourceNames) {
                if (jobs.ContainsKey(resource.Key) || !constantNamesById.TryGetValue(resource.Key, out string constantName)) {
                    continue;
                }

                constantName = RoJobMetadata.NormalizeConstantName(constantName);
                if (!TryClassifyEquipJob(constantName, out RoItemEquipJob equipJob, out RoItemEquipClass equipClass)) {
                    continue;
                }

                genderedNames.TryGetValue(resource.Key, out GenderedName genderedName);
                jobs[resource.Key] = new RoJobMetadata(
                    resource.Key,
                    constantName,
                    resource.Value,
                    genderedName.Female,
                    genderedName.Male,
                    equipJob,
                    equipClass
                );
            }

            return new RoJobMetadataTable(
                jobs,
                constants,
                jobIdentitySourcePath,
                jobNameSourcePath,
                pcJobNameGenderSourcePath,
                loadError,
                false
            );
        }

        public bool TryGetJob(int classId, out RoJobMetadata job) {
            return jobs.TryGetValue(classId, out job);
        }

        public bool TryGetJob(string constantName, out RoJobMetadata job) {
            job = null;
            return TryGetClassId(constantName, out int classId) && jobs.TryGetValue(classId, out job);
        }

        public bool TryGetClassId(string constantName, out int classId) {
            classId = 0;
            string key = RoJobMetadata.NormalizeConstantName(constantName);
            if (string.IsNullOrEmpty(key)) {
                return false;
            }

            return constants.TryGetValue(key, out classId) || constants.TryGetValue("JT_" + key, out classId);
        }

        public bool TryCreateEquipContext(int classId, int baseLevel, bool isMale, out RoItemEquipContext context) {
            if (jobs.TryGetValue(classId, out RoJobMetadata job)) {
                context = job.CreateEquipContext(baseLevel, isMale);
                return true;
            }

            context = default;
            return false;
        }

        public int ResolveJobMaxLevel(int classId) {
            return jobs.TryGetValue(classId, out RoJobMetadata job) && job != null
                ? job.MaxJobLevel
                : DefaultMaxJobLevel(classId);
        }

        public static int ResolveJobMaxLevel(int classId, RoJobMetadataTable jobs) {
            return jobs != null ? jobs.ResolveJobMaxLevel(classId) : DefaultMaxJobLevel(classId);
        }

        public static int DefaultMaxJobLevel(int classId) {
            return classId == 0 ? 10 : 50;
        }

        public static string DefaultName(int classId) {
            switch (classId) {
                case 0:
                    return "Novice";
                case 1:
                    return "Swordman";
                case 2:
                    return "Mage";
                case 3:
                    return "Archer";
                case 4:
                    return "Acolyte";
                case 5:
                    return "Merchant";
                case 6:
                    return "Thief";
                case 4218:
                    return "Summoner";
                case 4220:
                    return "Baby Summoner";
                case 4308:
                    return "Spirit Handler";
                default:
                    return string.Empty;
            }
        }

        private static void ParseConstants(string text, Dictionary<string, int> output, Dictionary<int, string> namesById) {
            if (string.IsNullOrWhiteSpace(text) || output == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match match in ConstantAssignmentPattern.Matches(stripped)) {
                string rawKey = NormalizeLuaKey(match.Groups["key"].Value);
                if (string.IsNullOrEmpty(rawKey) || !TryParseInteger(match.Groups["value"].Value, out int value)) {
                    continue;
                }

                output[rawKey] = value;
                string normalized = RoJobMetadata.NormalizeConstantName(rawKey);
                if (!string.IsNullOrEmpty(normalized)) {
                    output[normalized] = value;
                }

                if (namesById != null && !namesById.ContainsKey(value)) {
                    namesById[value] = normalized;
                }
            }
        }

        private static void ParseIndexedStringTable(string text, IReadOnlyDictionary<string, int> constants, Dictionary<int, string> output) {
            if (string.IsNullOrWhiteSpace(text) || output == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match match in IndexedStringPattern.Matches(stripped)) {
                if (TryResolveTableKey(match.Groups["key"].Value, constants, out int id)) {
                    output[id] = UnescapeLuaString(match.Groups["value"].Value);
                }
            }

            foreach (Match match in BareStringPattern.Matches(stripped)) {
                if (TryResolveTableKey(match.Groups["key"].Value, constants, out int id)) {
                    output[id] = UnescapeLuaString(match.Groups["value"].Value);
                }
            }
        }

        private static void ParseIndexedGenderTable(string text, IReadOnlyDictionary<string, int> constants, Dictionary<int, GenderedName> output) {
            if (string.IsNullOrWhiteSpace(text) || output == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match block in IndexedBlockPattern.Matches(stripped)) {
                if (!TryResolveTableKey(block.Groups["key"].Value, constants, out int id)) {
                    continue;
                }

                string body = block.Groups["body"].Value;
                string female = null;
                string male = null;
                foreach (Match entry in IndexedStringPattern.Matches(body)) {
                    string key = NormalizeLuaKey(entry.Groups["key"].Value);
                    string value = UnescapeLuaString(entry.Groups["value"].Value);
                    if (IsFemaleKey(key)) {
                        female = value;
                    } else if (IsMaleKey(key)) {
                        male = value;
                    }
                }

                if (string.IsNullOrEmpty(female) || string.IsNullOrEmpty(male)) {
                    var values = new List<string>();
                    foreach (Match valueMatch in QuotedStringPattern.Matches(body)) {
                        values.Add(UnescapeLuaString(valueMatch.Groups["value"].Value));
                    }

                    if (values.Count >= 2) {
                        female = string.IsNullOrEmpty(female) ? values[0] : female;
                        male = string.IsNullOrEmpty(male) ? values[1] : male;
                    } else if (values.Count == 1) {
                        female = string.IsNullOrEmpty(female) ? values[0] : female;
                        male = string.IsNullOrEmpty(male) ? values[0] : male;
                    }
                }

                if (!string.IsNullOrEmpty(female) || !string.IsNullOrEmpty(male)) {
                    output[id] = new GenderedName(female ?? male, male ?? female);
                }
            }
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

            string normalized = RoJobMetadata.NormalizeConstantName(key);
            return constants != null && (constants.TryGetValue(key, out id) || constants.TryGetValue(normalized, out id));
        }

        private static bool TryClassifyEquipJob(string constantName, out RoItemEquipJob equipJob, out RoItemEquipClass equipClass) {
            equipJob = RoItemEquipJob.None;
            equipClass = RoItemEquipClass.Normal;

            string key = NormalizeClassKey(constantName);
            if (string.IsNullOrEmpty(key)) {
                return false;
            }

            bool baby = key.EndsWith("_B", StringComparison.Ordinal) || key.Contains("_BABY");
            bool upper = key.EndsWith("_H", StringComparison.Ordinal) || key.StartsWith("HIGH_", StringComparison.Ordinal) || key.Contains("_HIGH");
            string baseKey = StripVariantMarkers(key);

            if (TryMapEquipJob(baseKey, out equipJob)) {
                equipClass = ResolveEquipClass(baseKey, upper, baby);
                return true;
            }

            return false;
        }

        private static RoItemEquipClass ResolveEquipClass(string baseKey, bool upper, bool baby) {
            if (IsFourthJob(baseKey)) {
                return RoItemEquipClass.Fourth;
            }

            if (IsThirdJob(baseKey)) {
                if (baby) {
                    return RoItemEquipClass.ThirdBaby;
                }

                return upper ? RoItemEquipClass.ThirdUpper : RoItemEquipClass.Third;
            }

            if (baby) {
                return RoItemEquipClass.Baby;
            }

            return upper || IsUpperJobAlias(baseKey) ? RoItemEquipClass.Upper : RoItemEquipClass.Normal;
        }

        private static bool IsUpperJobAlias(string key) {
            switch (key) {
                case "LORD_KNIGHT":
                case "HIGH_PRIEST":
                case "HIGH_WIZARD":
                case "WHITESMITH":
                case "MASTERSMITH":
                case "SNIPER":
                case "ASSASSIN_CROSS":
                case "PALADIN":
                case "CHAMPION":
                case "PROFESSOR":
                case "SCHOLAR":
                case "STALKER":
                case "CREATOR":
                case "BIOCHEMIST":
                case "CLOWN":
                case "GYPSY":
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryMapEquipJob(string key, out RoItemEquipJob job) {
            job = RoItemEquipJob.None;
            switch (key) {
                case "NOVICE":
                    job = RoItemEquipJob.Novice;
                    return true;
                case "SWORDMAN":
                case "SWORDSMAN":
                    job = RoItemEquipJob.Swordman;
                    return true;
                case "MAGICIAN":
                case "MAGE":
                    job = RoItemEquipJob.Mage;
                    return true;
                case "ARCHER":
                    job = RoItemEquipJob.Archer;
                    return true;
                case "ACOLYTE":
                    job = RoItemEquipJob.Acolyte;
                    return true;
                case "MERCHANT":
                    job = RoItemEquipJob.Merchant;
                    return true;
                case "THIEF":
                    job = RoItemEquipJob.Thief;
                    return true;
                case "KNIGHT":
                case "LORD_KNIGHT":
                case "RUNE_KNIGHT":
                case "DRAGON_KNIGHT":
                    job = RoItemEquipJob.Knight;
                    return true;
                case "PRIEST":
                case "HIGH_PRIEST":
                case "ARCHBISHOP":
                case "ARCH_BISHOP":
                case "CARDINAL":
                    job = RoItemEquipJob.Priest;
                    return true;
                case "WIZARD":
                case "HIGH_WIZARD":
                case "WARLOCK":
                case "ARCH_MAGE":
                    job = RoItemEquipJob.Wizard;
                    return true;
                case "BLACKSMITH":
                case "WHITESMITH":
                case "MASTERSMITH":
                case "MECHANIC":
                case "MEISTER":
                    job = RoItemEquipJob.Blacksmith;
                    return true;
                case "HUNTER":
                case "SNIPER":
                case "RANGER":
                case "WINDHAWK":
                    job = RoItemEquipJob.Hunter;
                    return true;
                case "ASSASSIN":
                case "ASSASSIN_CROSS":
                case "GUILLOTINE_CROSS":
                case "SHADOW_CROSS":
                    job = RoItemEquipJob.Assassin;
                    return true;
                case "CRUSADER":
                case "PALADIN":
                case "ROYAL_GUARD":
                case "IMPERIAL_GUARD":
                    job = RoItemEquipJob.Crusader;
                    return true;
                case "MONK":
                case "CHAMPION":
                case "SURA":
                case "INQUISITOR":
                    job = RoItemEquipJob.Monk;
                    return true;
                case "SAGE":
                case "PROFESSOR":
                case "SCHOLAR":
                case "SORCERER":
                case "ELEMENTAL_MASTER":
                    job = RoItemEquipJob.Sage;
                    return true;
                case "ROGUE":
                case "STALKER":
                case "SHADOW_CHASER":
                case "ABYSS_CHASER":
                    job = RoItemEquipJob.Rogue;
                    return true;
                case "ALCHEMIST":
                case "CREATOR":
                case "BIOCHEMIST":
                case "GENETIC":
                case "BIOLO":
                    job = RoItemEquipJob.Alchemist;
                    return true;
                case "BARD":
                case "DANCER":
                case "CLOWN":
                case "GYPSY":
                case "MINSTREL":
                case "WANDERER":
                case "TROUBADOUR":
                case "TROUVERE":
                    job = RoItemEquipJob.BardDancer;
                    return true;
                case "TAEKWON":
                case "TAEKWON_KID":
                    job = RoItemEquipJob.Taekwon;
                    return true;
                case "STAR":
                case "STAR_GLADIATOR":
                case "STAR_EMPEROR":
                case "SKY_EMPEROR":
                    job = RoItemEquipJob.StarGladiator;
                    return true;
                case "LINKER":
                case "SOUL_LINKER":
                case "SOUL_REAPER":
                    job = RoItemEquipJob.SoulLinker;
                    return true;
                case "GUNSLINGER":
                    job = RoItemEquipJob.Gunslinger;
                    return true;
                case "REBELLION":
                case "REBEL":
                case "NIGHT_WATCH":
                    job = RoItemEquipJob.Rebellion;
                    return true;
                case "NINJA":
                    job = RoItemEquipJob.Ninja;
                    return true;
                case "KAGEROU":
                case "OBORO":
                case "SHINKIRO":
                case "SHIRANUI":
                    job = RoItemEquipJob.KagerouOboro;
                    return true;
                case "SUPER_NOVICE":
                case "SUPERNOVICE":
                case "HYPER_NOVICE":
                    job = RoItemEquipJob.SuperNovice;
                    return true;
                case "DORAM":
                case "SUMMONER":
                    job = RoItemEquipJob.Summoner;
                    return true;
                case "SPIRIT_HANDLER":
                    job = RoItemEquipJob.SpiritHandler;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsThirdJob(string key) {
            switch (key) {
                case "RUNE_KNIGHT":
                case "ARCHBISHOP":
                case "ARCH_BISHOP":
                case "WARLOCK":
                case "MECHANIC":
                case "RANGER":
                case "GUILLOTINE_CROSS":
                case "ROYAL_GUARD":
                case "SURA":
                case "SORCERER":
                case "SHADOW_CHASER":
                case "GENETIC":
                case "MINSTREL":
                case "WANDERER":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsFourthJob(string key) {
            switch (key) {
                case "DRAGON_KNIGHT":
                case "CARDINAL":
                case "ARCH_MAGE":
                case "MEISTER":
                case "WINDHAWK":
                case "SHADOW_CROSS":
                case "IMPERIAL_GUARD":
                case "INQUISITOR":
                case "ELEMENTAL_MASTER":
                case "ABYSS_CHASER":
                case "BIOLO":
                case "TROUBADOUR":
                case "TROUVERE":
                case "SKY_EMPEROR":
                case "SOUL_REAPER":
                case "SHINKIRO":
                case "SHIRANUI":
                case "NIGHT_WATCH":
                case "HYPER_NOVICE":
                case "SPIRIT_HANDLER":
                    return true;
                default:
                    return false;
            }
        }

        private static string StripVariantMarkers(string key) {
            if (string.IsNullOrEmpty(key)) {
                return string.Empty;
            }

            string result = key;
            if (result.StartsWith("JT_", StringComparison.Ordinal)) {
                result = result.Substring(3);
            }

            if (result.EndsWith("_RIDING", StringComparison.Ordinal)) {
                result = result.Substring(0, result.Length - "_RIDING".Length);
            }

            if (result.EndsWith("_2ND", StringComparison.Ordinal)) {
                result = result.Substring(0, result.Length - "_2ND".Length);
            }

            if (result.EndsWith("_H", StringComparison.Ordinal) || result.EndsWith("_B", StringComparison.Ordinal)) {
                result = result.Substring(0, result.Length - 2);
            }

            if (result.EndsWith("2", StringComparison.Ordinal) && result.Length > 1) {
                result = result.Substring(0, result.Length - 1);
            }

            if (result.StartsWith("HIGH_", StringComparison.Ordinal)) {
                result = result.Substring("HIGH_".Length);
            }

            result = result.Replace("_BABY", string.Empty).Replace("_HIGH", string.Empty);
            return result;
        }

        private static string NormalizeClassKey(string value) {
            value = RoJobMetadata.NormalizeConstantName(value);
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("-", "_").Trim().ToUpperInvariant();
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

        private static string StripLineComments(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                int luaComment = lines[i].IndexOf("--", StringComparison.Ordinal);
                int slashComment = lines[i].IndexOf("//", StringComparison.Ordinal);
                int comment = -1;
                if (luaComment >= 0 && slashComment >= 0) {
                    comment = Math.Min(luaComment, slashComment);
                } else if (luaComment >= 0) {
                    comment = luaComment;
                } else if (slashComment >= 0) {
                    comment = slashComment;
                }

                if (comment >= 0) {
                    lines[i] = lines[i].Substring(0, comment);
                }
            }

            return string.Join("\n", lines);
        }

        private static bool IsFemaleKey(string key) {
            if (string.IsNullOrEmpty(key)) {
                return false;
            }

            key = key.ToLowerInvariant();
            return key == "0" || key.Contains("female") || key.Contains("woman") || key.Contains("\uC5EC");
        }

        private static bool IsMaleKey(string key) {
            if (string.IsNullOrEmpty(key)) {
                return false;
            }

            key = key.ToLowerInvariant();
            return key == "1" || key.Contains("male") || key.Contains("man") || key.Contains("\uB0A8");
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

                if (bytes[0] == 0x1B) {
                    pendingError = "compiled Lua bytecode is not parsed in this Phase 7 text slice";
                    continue;
                }

                sourcePath = candidate;
                sourceBytes = bytes;
                return RoTextEncoding.Decode(bytes);
            }

            if (!string.IsNullOrEmpty(pendingError)) {
                loadError = pendingError;
            }

            return null;
        }

        private static bool TryReadJobMetadataCache(
            RoAssetDatabase assets,
            string jobIdentitySourcePath,
            byte[] jobIdentityBytes,
            string jobNameSourcePath,
            byte[] jobNameBytes,
            string pcJobNameGenderSourcePath,
            byte[] pcJobNameGenderBytes,
            out RoJobMetadataTable table
        ) {
            table = null;
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null || !HasAnySourceBytes(jobIdentityBytes, jobNameBytes, pcJobNameGenderBytes)) {
                return false;
            }

            string cacheKey = BuildJobMetadataCacheKey(
                jobIdentitySourcePath,
                jobIdentityBytes,
                jobNameSourcePath,
                jobNameBytes,
                pcJobNameGenderSourcePath,
                pcJobNameGenderBytes);
            try {
                if (!cache.TryReadBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", out byte[] data)) {
                    return false;
                }

                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadInt32() != JobMetadataCacheMagic ||
                    reader.ReadInt32() != JobMetadataCacheVersion) {
                    return false;
                }

                string cachedJobIdentitySourcePath = reader.ReadString();
                string cachedJobNameSourcePath = reader.ReadString();
                string cachedPcJobNameGenderSourcePath = reader.ReadString();
                if (!IsSameSourcePath(cachedJobIdentitySourcePath, jobIdentitySourcePath) ||
                    !IsSameSourcePath(cachedJobNameSourcePath, jobNameSourcePath) ||
                    !IsSameSourcePath(cachedPcJobNameGenderSourcePath, pcJobNameGenderSourcePath)) {
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

                int jobCount = reader.ReadInt32();
                if (jobCount < 0 || jobCount > MaxCachedJobs) {
                    return false;
                }

                var jobs = new Dictionary<int, RoJobMetadata>(jobCount);
                for (int i = 0; i < jobCount; i++) {
                    RoJobMetadata job = ReadJobMetadataCacheEntry(reader);
                    jobs[job.ClassId] = job;
                }

                table = new RoJobMetadataTable(
                    jobs,
                    constants,
                    jobIdentitySourcePath,
                    jobNameSourcePath,
                    pcJobNameGenderSourcePath,
                    null,
                    true);
                return true;
            } catch {
                table = null;
                return false;
            }
        }

        private static void TryWriteJobMetadataCache(
            RoAssetDatabase assets,
            string jobIdentitySourcePath,
            byte[] jobIdentityBytes,
            string jobNameSourcePath,
            byte[] jobNameBytes,
            string pcJobNameGenderSourcePath,
            byte[] pcJobNameGenderBytes,
            RoJobMetadataTable table
        ) {
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null ||
                table == null ||
                table.Count == 0 ||
                !HasAnySourceBytes(jobIdentityBytes, jobNameBytes, pcJobNameGenderBytes)) {
                return;
            }

            string cacheKey = BuildJobMetadataCacheKey(
                jobIdentitySourcePath,
                jobIdentityBytes,
                jobNameSourcePath,
                jobNameBytes,
                pcJobNameGenderSourcePath,
                pcJobNameGenderBytes);
            try {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                    writer.Write(JobMetadataCacheMagic);
                    writer.Write(JobMetadataCacheVersion);
                    writer.Write(jobIdentitySourcePath ?? string.Empty);
                    writer.Write(jobNameSourcePath ?? string.Empty);
                    writer.Write(pcJobNameGenderSourcePath ?? string.Empty);
                    writer.Write(table.constants.Count);
                    foreach (KeyValuePair<string, int> constant in table.constants) {
                        writer.Write(constant.Key ?? string.Empty);
                        writer.Write(constant.Value);
                    }

                    writer.Write(table.jobs.Count);
                    foreach (RoJobMetadata job in table.jobs.Values) {
                        WriteJobMetadataCacheEntry(writer, job);
                    }
                }

                cache.WriteBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", stream.ToArray());
            } catch {
                // Parsed job metadata cache is an optimization only; source Lua remains authoritative.
            }
        }

        private static RoJobMetadata ReadJobMetadataCacheEntry(BinaryReader reader) {
            return new RoJobMetadata(
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                (RoItemEquipJob) reader.ReadUInt64(),
                (RoItemEquipClass) reader.ReadInt32());
        }

        private static void WriteJobMetadataCacheEntry(BinaryWriter writer, RoJobMetadata job) {
            writer.Write(job.ClassId);
            writer.Write(job.ConstantName ?? string.Empty);
            writer.Write(job.ResourceName ?? string.Empty);
            writer.Write(job.FemaleBodyName ?? string.Empty);
            writer.Write(job.MaleBodyName ?? string.Empty);
            writer.Write((ulong) job.EquipJob);
            writer.Write((int) job.EquipClass);
        }

        private static string BuildJobMetadataCacheKey(
            string jobIdentitySourcePath,
            byte[] jobIdentityBytes,
            string jobNameSourcePath,
            byte[] jobNameBytes,
            string pcJobNameGenderSourcePath,
            byte[] pcJobNameGenderBytes
        ) {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                WriteSourceBytesForCacheKey(writer, jobIdentitySourcePath, jobIdentityBytes);
                WriteSourceBytesForCacheKey(writer, jobNameSourcePath, jobNameBytes);
                WriteSourceBytesForCacheKey(writer, pcJobNameGenderSourcePath, pcJobNameGenderBytes);
            }

            string sourcePathKey = string.Join(
                "|",
                jobIdentitySourcePath ?? string.Empty,
                jobNameSourcePath ?? string.Empty,
                pcJobNameGenderSourcePath ?? string.Empty);
            return RoItemMetadataTable.BuildParsedSourceCacheKey("job-metadata", sourcePathKey, stream.ToArray());
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

        private static string[] LuaDataInfo(params string[] names) {
            var paths = new List<string>();
            for (int i = 0; i < names.Length; i++) {
                AddLuaPathVariants(paths, "data/luafiles514/lua files/datainfo", names[i]);
                AddLuaPathVariants(paths, "data/lua files/datainfo", names[i]);
            }

            return paths.ToArray();
        }

        private static void AddLuaPathVariants(List<string> paths, string root, string name) {
            paths.Add(root + "/" + name + ".lub");
            paths.Add(root + "/" + name + ".lua");
        }
    }
}
