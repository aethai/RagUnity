using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public readonly struct RoQuestRewardItem {
        public RoQuestRewardItem(uint itemId, int itemNum) {
            ItemId = itemId;
            ItemNum = Math.Max(0, itemNum);
        }

        public uint ItemId { get; }
        public int ItemNum { get; }
    }

    public sealed class RoQuestMetadata {
        public RoQuestMetadata(
            uint questId,
            string title = null,
            string summary = null,
            string iconName = null,
            string npcSpr = null,
            string npcNavi = null,
            int npcPosX = 0,
            int npcPosY = 0,
            int rewardExp = 0,
            int rewardJExp = 0,
            IReadOnlyList<RoQuestRewardItem> rewardItems = null,
            IReadOnlyList<string> descriptionLines = null,
            string group = null
        ) {
            QuestId = questId;
            Title = Clean(title);
            Summary = Clean(summary);
            IconName = string.IsNullOrWhiteSpace(iconName) ? "ico_nq.bmp" : iconName.Trim();
            NpcSpr = Clean(npcSpr);
            NpcNavi = Clean(npcNavi);
            NpcPosX = Math.Max(0, npcPosX);
            NpcPosY = Math.Max(0, npcPosY);
            RewardEXP = Math.Max(0, rewardExp);
            RewardJEXP = Math.Max(0, rewardJExp);
            RewardItems = CopyRewards(rewardItems);
            DescriptionLines = CopyLines(descriptionLines);
            Group = Clean(group);
        }

        public uint QuestId { get; }
        public string Title { get; }
        public string Summary { get; }
        public string IconName { get; }
        public string NpcSpr { get; }
        public string NpcNavi { get; }
        public int NpcPosX { get; }
        public int NpcPosY { get; }
        public int RewardEXP { get; }
        public int RewardJEXP { get; }
        public RoQuestRewardItem[] RewardItems { get; }
        public string[] DescriptionLines { get; }
        public string Group { get; }

        public bool HasDisplayText =>
            !string.IsNullOrWhiteSpace(Title) ||
            !string.IsNullOrWhiteSpace(Summary) ||
            DescriptionLines.Length > 0;

        public string DescriptionText => DescriptionLines.Length == 0
            ? string.Empty
            : string.Join("\n", DescriptionLines);

        public string GetDisplayName() {
            return !string.IsNullOrWhiteSpace(Title)
                ? Title
                : "Quest " + QuestId.ToString(CultureInfo.InvariantCulture);
        }

        public string GetSummary() {
            return Summary ?? string.Empty;
        }

        private static string Clean(string value) {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static RoQuestRewardItem[] CopyRewards(IReadOnlyList<RoQuestRewardItem> source) {
            if (source == null || source.Count == 0) {
                return Array.Empty<RoQuestRewardItem>();
            }

            var copy = new RoQuestRewardItem[source.Count];
            for (int i = 0; i < source.Count; i++) {
                copy[i] = source[i];
            }

            return copy;
        }

        private static string[] CopyLines(IReadOnlyList<string> source) {
            if (source == null || source.Count == 0) {
                return Array.Empty<string>();
            }

            var lines = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++) {
                string line = Clean(source[i]);
                if (!string.IsNullOrEmpty(line)) {
                    lines.Add(line);
                }
            }

            return lines.Count == 0 ? Array.Empty<string>() : lines.ToArray();
        }
    }

    public sealed class RoQuestMetadataTable {
        private const int QuestMetadataCacheMagic = 0x54534551; // QEST
        private const int QuestMetadataCacheVersion = 1;
        private const int MaxCachedQuestSources = 100_000;
        private const int MaxCachedQuests = 1_000_000;
        private const int MaxCachedQuestRewards = 100_000;
        private const int MaxCachedQuestDescriptionLines = 100_000;

        private static readonly string[] CandidatePaths = {
            "System/OngoingQuestInfoList.lub",
            "System/OngoingQuestInfoList.lua",
            "System/OngoingQuestInfoList_True.lub",
            "System/OngoingQuestInfoList_True.lua",
            "System/OngoingQuests.lub",
            "System/OngoingQuests.lua",
            "System/OngoingQuests_C.lub",
            "System/OngoingQuests_C.lua",
            "SystemEN/OngoingQuestInfoList.lub",
            "SystemEN/OngoingQuestInfoList.lua",
            "SystemEN/OngoingQuests.lub",
            "SystemEN/OngoingQuests.lua",
            "System/QuestInfoList.lub",
            "System/QuestInfoList.lua",
            "System/QuestList.lub",
            "System/QuestList.lua",
            "System/RecommendedQuestInfoList.lub",
            "System/RecommendedQuestInfoList.lua",
            "System/RecommendedQuestInfoList_True.lub",
            "System/RecommendedQuestInfoList_True.lua",
            "System/RecommendedQuestInfoList_Sakray.lub",
            "System/RecommendedQuestInfoList_Sakray.lua",
            "System/RecommendedQuests.lub",
            "System/RecommendedQuests.lua",
            "System/RecommendedQuests_basic.lub",
            "System/RecommendedQuests_basic.lua",
            "System/RecommendedQuests_C.lub",
            "System/RecommendedQuests_C.lua",
            "SystemEN/QuestInfoList.lub",
            "SystemEN/QuestInfoList.lua",
            "SystemEN/RecommendedQuestInfoList.lub",
            "SystemEN/RecommendedQuestInfoList.lua",
            "data/luafiles514/lua files/system/OngoingQuestInfoList.lub",
            "data/luafiles514/lua files/system/OngoingQuestInfoList.lua",
            "data/luafiles514/lua files/system/OngoingQuests.lub",
            "data/luafiles514/lua files/system/OngoingQuests.lua",
            "data/luafiles514/lua files/system/QuestInfoList.lub",
            "data/luafiles514/lua files/system/QuestInfoList.lua",
            "data/luafiles514/lua files/system/RecommendedQuestInfoList.lub",
            "data/luafiles514/lua files/system/RecommendedQuestInfoList.lua",
            "data/lua files/system/OngoingQuestInfoList.lub",
            "data/lua files/system/OngoingQuestInfoList.lua",
            "data/lua files/system/OngoingQuests.lub",
            "data/lua files/system/OngoingQuests.lua",
            "data/lua files/system/QuestInfoList.lub",
            "data/lua files/system/QuestInfoList.lua",
            "data/lua files/system/RecommendedQuestInfoList.lub",
            "data/lua files/system/RecommendedQuestInfoList.lua",
            "data/questid2display.txt",
            "data/luafiles514/lua files/datainfo/questid2display.txt",
            "data/lua files/datainfo/questid2display.txt"
        };

        public static readonly RoQuestMetadataTable Empty = new RoQuestMetadataTable(
            new Dictionary<uint, RoQuestMetadata>(),
            string.Empty,
            string.Empty);

        private readonly Dictionary<uint, RoQuestMetadata> quests;

        private RoQuestMetadataTable(
            Dictionary<uint, RoQuestMetadata> quests,
            string sourcePath,
            string loadError,
            bool loadedFromDiskCache = false
        ) {
            this.quests = quests ?? new Dictionary<uint, RoQuestMetadata>();
            SourcePath = sourcePath ?? string.Empty;
            LoadError = loadError ?? string.Empty;
            LoadedFromDiskCache = loadedFromDiskCache;
        }

        public int Count => quests.Count;
        public bool IsLoaded => Count > 0;
        public string SourcePath { get; }
        public string LoadError { get; }
        public bool LoadedFromDiskCache { get; }

        public bool TryGetQuest(uint questId, out RoQuestMetadata metadata) {
            return quests.TryGetValue(questId, out metadata);
        }

        public RoQuestMetadata GetQuestOrFallback(uint questId) {
            return quests.TryGetValue(questId, out RoQuestMetadata metadata)
                ? metadata
                : new RoQuestMetadata(questId);
        }

        public static RoQuestMetadataTable Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            var errors = new List<string>();

            List<string> candidatePaths = CollectCandidatePaths(assets);
            List<QuestSource> sources = CollectQuestSources(assets, candidatePaths, errors);
            if (TryReadQuestMetadataCache(assets, sources, out RoQuestMetadataTable cachedTable)) {
                return cachedTable;
            }

            sources = HydrateQuestSources(assets, sources, errors);
            var merged = new Dictionary<uint, RoQuestMetadata>();
            var loadedSources = new List<string>();
            for (int i = 0; i < sources.Count; i++) {
                QuestSource source = sources[i];
                if (!source.IsReadableText) {
                    continue;
                }

                string text = RoTextEncoding.Decode(source.Bytes);
                RoQuestMetadataTable parsed = source.Path.EndsWith("questid2display.txt", StringComparison.OrdinalIgnoreCase)
                    ? FromQuestIdDisplayText(text, source.Path)
                    : FromLuaText(text, source.Path);

                if (parsed.Count == 0) {
                    if (!string.IsNullOrWhiteSpace(parsed.LoadError)) {
                        errors.Add(parsed.LoadError);
                    }

                    continue;
                }

                foreach (RoQuestMetadata quest in parsed.quests.Values) {
                    if (merged.TryGetValue(quest.QuestId, out RoQuestMetadata existing)) {
                        merged[quest.QuestId] = MergeQuestMetadata(existing, quest);
                    } else {
                        merged[quest.QuestId] = quest;
                    }
                }

                loadedSources.Add(source.Path);
            }

            if (merged.Count > 0) {
                var table = new RoQuestMetadataTable(
                    merged,
                    string.Join(", ", loadedSources),
                    string.Join("; ", errors));
                TryWriteQuestMetadataCache(assets, sources, table);
                return table;
            }

            string loadError = errors.Count > 0
                ? string.Join("; ", errors)
                : "quest metadata files not found";
            return new RoQuestMetadataTable(new Dictionary<uint, RoQuestMetadata>(), string.Empty, loadError);
        }

        private static List<QuestSource> CollectQuestSources(
            RoAssetDatabase assets,
            IReadOnlyList<string> candidatePaths,
            List<string> errors
        ) {
            var sources = new List<QuestSource>();
            if (assets == null || candidatePaths == null) {
                return sources;
            }

            for (int i = 0; i < candidatePaths.Count; i++) {
                string path = candidatePaths[i];
                if (!assets.Exists(path)) {
                    continue;
                }

                if (assets.TryGetAssetFingerprint(path, out string resolvedPath, out string fingerprint)) {
                    sources.Add(new QuestSource(resolvedPath, fingerprint, null, false));
                    continue;
                }

                sources.Add(ReadQuestSource(assets, path, errors));
            }

            return sources;
        }

        private static List<QuestSource> HydrateQuestSources(
            RoAssetDatabase assets,
            IReadOnlyList<QuestSource> sources,
            List<string> errors
        ) {
            var hydrated = new List<QuestSource>(sources?.Count ?? 0);
            if (assets == null || sources == null) {
                return hydrated;
            }

            for (int i = 0; i < sources.Count; i++) {
                QuestSource source = sources[i];
                if (source.HasBytes) {
                    hydrated.Add(source);
                    continue;
                }

                hydrated.Add(ReadQuestSource(assets, source.Path, errors, source.Fingerprint));
            }

            return hydrated;
        }

        private static QuestSource ReadQuestSource(
            RoAssetDatabase assets,
            string path,
            List<string> errors,
            string fingerprint = null
        ) {
            byte[] bytes = assets.ReadBytes(path);
            if (bytes == null || bytes.Length == 0) {
                errors?.Add(path + ": empty");
                return new QuestSource(path, fingerprint, bytes ?? Array.Empty<byte>(), false);
            }

            if (bytes[0] == 0x1b) {
                errors?.Add(path + ": compiled Lua is not readable");
                return new QuestSource(path, fingerprint, bytes, false);
            }

            return new QuestSource(path, fingerprint, bytes, true);
        }

        private static List<string> CollectCandidatePaths(RoAssetDatabase assets) {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < CandidatePaths.Length; i++) {
                AddCandidate(result, seen, CandidatePaths[i]);
            }

            AddManifestQuestFiles(assets, result, seen, "lua");
            AddManifestQuestFiles(assets, result, seen, "lub");
            AddManifestQuestFiles(assets, result, seen, "txt");
            result.Sort((left, right) => {
                int priority = GetQuestMetadataPathPriority(left).CompareTo(GetQuestMetadataPathPriority(right));
                return priority != 0 ? priority : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private static void AddManifestQuestFiles(RoAssetDatabase assets, List<string> result, HashSet<string> seen, string extension) {
            List<string> files;
            try {
                files = assets.ListFilesWithExtension(extension);
            } catch (Exception ex) {
                UnityEngine.Debug.LogWarning("Quest metadata manifest scan failed for *." + extension + ": " + ex.Message);
                return;
            }

            for (int i = 0; i < files.Count; i++) {
                string path = files[i];
                if (IsQuestMetadataCandidate(path)) {
                    AddCandidate(result, seen, path);
                }
            }
        }

        private static void AddCandidate(List<string> result, HashSet<string> seen, string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return;
            }

            string normalized = path.Replace('\\', '/').Trim();
            if (seen.Add(normalized)) {
                result.Add(normalized);
            }
        }

        private static bool IsQuestMetadataCandidate(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }

            string lower = path.Replace('\\', '/').ToLowerInvariant();
            bool supportedExtension = lower.EndsWith(".lua", StringComparison.Ordinal) ||
                lower.EndsWith(".lub", StringComparison.Ordinal) ||
                lower.EndsWith(".txt", StringComparison.Ordinal);
            if (!supportedExtension) {
                return false;
            }

            if (lower.EndsWith("questid2display.txt", StringComparison.Ordinal)) {
                return true;
            }

            if (lower.EndsWith(".txt", StringComparison.Ordinal)) {
                return false;
            }

            if (!lower.Contains("quest")) {
                return false;
            }

            return lower.Contains("ongoing") ||
                lower.Contains("recommended") ||
                lower.Contains("questinfo") ||
                lower.Contains("quest_info") ||
                lower.Contains("questclassification") ||
                lower.EndsWith("/questlist.lua", StringComparison.Ordinal) ||
                lower.EndsWith("/questlist.lub", StringComparison.Ordinal);
        }

        private static int GetQuestMetadataPathPriority(string path) {
            string lower = (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            if (lower.Contains("ongoingquestinfolist")) {
                return 0;
            }

            if (lower.Contains("ongoingquests")) {
                return 1;
            }

            if (lower.Contains("questinfolist") || lower.Contains("quest_info")) {
                return 2;
            }

            if (lower.Contains("recommended")) {
                return 3;
            }

            if (lower.EndsWith("questid2display.txt", StringComparison.Ordinal)) {
                return 4;
            }

            return 10;
        }

        private static RoQuestMetadata MergeQuestMetadata(RoQuestMetadata existing, RoQuestMetadata incoming) {
            if (existing == null) {
                return incoming;
            }

            if (incoming == null) {
                return existing;
            }

            return new RoQuestMetadata(
                existing.QuestId,
                FirstNonEmpty(existing.Title, incoming.Title),
                FirstNonEmpty(existing.Summary, incoming.Summary),
                FirstNonDefaultIcon(existing.IconName, incoming.IconName),
                FirstNonEmpty(existing.NpcSpr, incoming.NpcSpr),
                FirstNonEmpty(existing.NpcNavi, incoming.NpcNavi),
                existing.NpcPosX != 0 ? existing.NpcPosX : incoming.NpcPosX,
                existing.NpcPosY != 0 ? existing.NpcPosY : incoming.NpcPosY,
                existing.RewardEXP != 0 ? existing.RewardEXP : incoming.RewardEXP,
                existing.RewardJEXP != 0 ? existing.RewardJEXP : incoming.RewardJEXP,
                existing.RewardItems.Length > 0 ? existing.RewardItems : incoming.RewardItems,
                existing.DescriptionLines.Length > 0 ? existing.DescriptionLines : incoming.DescriptionLines,
                FirstNonEmpty(existing.Group, incoming.Group));
        }

        private static string FirstNonEmpty(string preferred, string fallback) {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
        }

        private static string FirstNonDefaultIcon(string preferred, string fallback) {
            if (!string.IsNullOrWhiteSpace(preferred) &&
                !string.Equals(preferred, "ico_nq.bmp", StringComparison.OrdinalIgnoreCase)) {
                return preferred;
            }

            return !string.IsNullOrWhiteSpace(fallback) ? fallback : preferred;
        }

        public static RoQuestMetadataTable FromQuestIdDisplayText(string text, string sourcePath = "questid2display.txt") {
            var result = new Dictionary<uint, RoQuestMetadata>();
            if (string.IsNullOrWhiteSpace(text)) {
                return new RoQuestMetadataTable(result, sourcePath, sourcePath + ": empty");
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) {
                    continue;
                }

                string[] fields = line.Split('#');
                if (fields.Length < 2 || !uint.TryParse(fields[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint questId)) {
                    continue;
                }

                string title = fields.Length > 1 ? fields[1] : string.Empty;
                string group = fields.Length > 2 ? fields[2] : string.Empty;
                string image = fields.Length > 3 ? fields[3] : string.Empty;
                string description = fields.Length > 4 ? fields[4] : string.Empty;
                string summary = fields.Length > 5 ? fields[5] : string.Empty;
                result[questId] = new RoQuestMetadata(
                    questId,
                    title,
                    summary,
                    string.IsNullOrWhiteSpace(image) ? "ico_nq.bmp" : image,
                    descriptionLines: SplitDescription(description),
                    group: group);
            }

            return new RoQuestMetadataTable(
                result,
                sourcePath,
                result.Count == 0 ? sourcePath + ": no quest rows parsed" : string.Empty);
        }

        public static RoQuestMetadataTable FromLuaText(string text, string sourcePath = "OngoingQuestInfoList") {
            var result = new Dictionary<uint, RoQuestMetadata>();
            if (string.IsNullOrWhiteSpace(text)) {
                return new RoQuestMetadataTable(result, sourcePath, sourcePath + ": empty");
            }

            string clean = StripLuaComments(text);
            string[] tableNames = {
                "QuestInfoList",
                "QuestInfoList_C",
                "OngoingQuestInfoList",
                "OngoingQuestInfoList_C",
                "RecommendedQuestInfoList",
                "RecommendedQuestInfoList_C",
                "RecommendedQuests_C"
            };
            for (int i = 0; i < tableNames.Length; i++) {
                ParseQuestInfoList(clean, result, tableNames[i]);
                ParseQuestInfoAssignments(clean, result, tableNames[i]);
            }

            ParseAddQuestInfoCalls(clean, result);
            ParseAddQuestDescriptionCalls(clean, result);
            ParseAddQuestRewardCalls(clean, result);

            return new RoQuestMetadataTable(
                result,
                sourcePath,
                result.Count == 0 ? sourcePath + ": no quest metadata parsed" : string.Empty);
        }

        private static void ParseQuestInfoList(string text, Dictionary<uint, RoQuestMetadata> result, string tableName) {
            MatchCollection tableMatches = Regex.Matches(text, @"\b" + Regex.Escape(tableName) + @"\s*=\s*\{", RegexOptions.CultureInvariant);
            for (int tableIndex = 0; tableIndex < tableMatches.Count; tableIndex++) {
                Match tableMatch = tableMatches[tableIndex];
                int tableOpenBrace = text.IndexOf('{', tableMatch.Index + tableMatch.Length - 1);
                if (tableOpenBrace < 0 || !TryReadBraceBlock(text, tableOpenBrace, out string tableBlock, out _)) {
                    continue;
                }

                for (int i = 0; i < tableBlock.Length; i++) {
                    if (tableBlock[i] != '[') {
                        continue;
                    }

                    int idStart = i + 1;
                    int idEnd = idStart;
                    while (idEnd < tableBlock.Length && char.IsDigit(tableBlock[idEnd])) {
                        idEnd++;
                    }

                    if (idEnd == idStart ||
                        idEnd >= tableBlock.Length ||
                        tableBlock[idEnd] != ']' ||
                        !uint.TryParse(tableBlock.Substring(idStart, idEnd - idStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint questId)) {
                        continue;
                    }

                    int cursor = idEnd + 1;
                    while (cursor < tableBlock.Length && char.IsWhiteSpace(tableBlock[cursor])) {
                        cursor++;
                    }

                    if (cursor >= tableBlock.Length || tableBlock[cursor] != '=') {
                        continue;
                    }

                    cursor++;
                    while (cursor < tableBlock.Length && char.IsWhiteSpace(tableBlock[cursor])) {
                        cursor++;
                    }

                    if (cursor >= tableBlock.Length || tableBlock[cursor] != '{' || !TryReadBraceBlock(tableBlock, cursor, out string block, out int endIndex)) {
                        continue;
                    }

                    MergeParsedQuest(result, ParseQuestMetadataBlock(questId, block));
                    i = endIndex;
                }
            }
        }

        private static void ParseQuestInfoAssignments(string text, Dictionary<uint, RoQuestMetadata> result, string tableName) {
            MatchCollection matches = Regex.Matches(text, @"\b" + Regex.Escape(tableName) + @"\s*\[\s*(\d+)\s*\]\s*=\s*\{", RegexOptions.CultureInvariant);
            for (int i = 0; i < matches.Count; i++) {
                Match match = matches[i];
                if (!uint.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint questId)) {
                    continue;
                }

                int openBrace = text.IndexOf('{', match.Index + match.Length - 1);
                if (openBrace >= 0 && TryReadBraceBlock(text, openBrace, out string block, out _)) {
                    MergeParsedQuest(result, ParseQuestMetadataBlock(questId, block));
                }
            }
        }

        private static RoQuestMetadata ParseQuestMetadataBlock(uint questId, string block) {
            return new RoQuestMetadata(
                questId,
                ReadStringField(block, "Title"),
                ReadStringField(block, "Summary"),
                ReadStringField(block, "IconName"),
                ReadStringField(block, "NpcSpr"),
                ReadStringField(block, "NpcNavi"),
                ReadIntField(block, "NpcPosX"),
                ReadIntField(block, "NpcPosY"),
                ReadIntField(block, "RewardEXP"),
                ReadIntField(block, "RewardJEXP"),
                ReadRewardItems(block),
                ReadDescriptionLines(block));
        }

        private static void ParseAddQuestInfoCalls(string text, Dictionary<uint, RoQuestMetadata> result) {
            foreach (List<string> args in ReadFunctionCalls(text, "AddQuestInfo")) {
                if (args.Count < 10 || !uint.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint questId)) {
                    continue;
                }

                RoQuestMetadata previous = result.TryGetValue(questId, out RoQuestMetadata existing)
                    ? existing
                    : new RoQuestMetadata(questId);
                result[questId] = new RoQuestMetadata(
                    questId,
                    UnquoteLuaArgument(args[1]),
                    UnquoteLuaArgument(args[2]),
                    UnquoteLuaArgument(args[3]),
                    UnquoteLuaArgument(args[4]),
                    UnquoteLuaArgument(args[5]),
                    ParseInt(args[6]),
                    ParseInt(args[7]),
                    ParseInt(args[8]),
                    ParseInt(args[9]),
                    previous.RewardItems,
                    previous.DescriptionLines,
                    previous.Group);
            }
        }

        private static void ParseAddQuestDescriptionCalls(string text, Dictionary<uint, RoQuestMetadata> result) {
            foreach (List<string> args in ReadFunctionCalls(text, "AddQuestDescription")) {
                if (args.Count < 2 || !uint.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint questId)) {
                    continue;
                }

                RoQuestMetadata previous = result.TryGetValue(questId, out RoQuestMetadata existing)
                    ? existing
                    : new RoQuestMetadata(questId);
                var lines = new List<string>(previous.DescriptionLines);
                string line = UnquoteLuaArgument(args[1]);
                if (!string.IsNullOrWhiteSpace(line)) {
                    lines.Add(line);
                }

                result[questId] = new RoQuestMetadata(
                    questId,
                    previous.Title,
                    previous.Summary,
                    previous.IconName,
                    previous.NpcSpr,
                    previous.NpcNavi,
                    previous.NpcPosX,
                    previous.NpcPosY,
                    previous.RewardEXP,
                    previous.RewardJEXP,
                    previous.RewardItems,
                    lines,
                    previous.Group);
            }
        }

        private static void ParseAddQuestRewardCalls(string text, Dictionary<uint, RoQuestMetadata> result) {
            foreach (List<string> args in ReadFunctionCalls(text, "AddQuestRewardItem")) {
                if (args.Count < 3 || !uint.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint questId)) {
                    continue;
                }

                RoQuestMetadata previous = result.TryGetValue(questId, out RoQuestMetadata existing)
                    ? existing
                    : new RoQuestMetadata(questId);
                var rewards = new List<RoQuestRewardItem>(previous.RewardItems) {
                    new RoQuestRewardItem((uint) Math.Max(0, ParseInt(args[1])), ParseInt(args[2]))
                };

                result[questId] = new RoQuestMetadata(
                    questId,
                    previous.Title,
                    previous.Summary,
                    previous.IconName,
                    previous.NpcSpr,
                    previous.NpcNavi,
                    previous.NpcPosX,
                    previous.NpcPosY,
                    previous.RewardEXP,
                    previous.RewardJEXP,
                    rewards,
                    previous.DescriptionLines,
                    previous.Group);
            }
        }

        private static IReadOnlyList<string> ReadDescriptionLines(string block) {
            var lines = new List<string>();
            AddFieldLines(block, "Description", lines);

            if (lines.Count == 0) {
                AddFieldLines(block, "QuestInfo1", lines);
                AddFieldLines(block, "QuestInfo2", lines);
                AddFieldLines(block, "QuestInfo3", lines);
            }

            return lines.Count == 0
                ? Array.Empty<string>()
                : (IReadOnlyList<string>) lines;
        }

        private static void AddFieldLines(string block, string fieldName, List<string> lines) {
            if (lines == null) {
                return;
            }

            if (TryReadFieldBraceBlock(block, fieldName, out string fieldBlock)) {
                lines.AddRange(ReadLuaStrings(fieldBlock));
                return;
            }

            string line = ReadStringField(block, fieldName);
            if (!string.IsNullOrWhiteSpace(line)) {
                lines.AddRange(SplitDescription(line));
            }
        }

        private static IReadOnlyList<string> SplitDescription(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return Array.Empty<string>();
            }

            return value.Replace("\\n", "\n")
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static IReadOnlyList<RoQuestRewardItem> ReadRewardItems(string block) {
            if (!TryReadFieldBraceBlock(block, "RewardItemList", out string rewardsBlock)) {
                return Array.Empty<RoQuestRewardItem>();
            }

            var items = new List<RoQuestRewardItem>();
            MatchCollection matches = Regex.Matches(rewardsBlock, @"ItemI[Dd]\s*=\s*(\d+).*?ItemNum\s*=\s*(\d+)", RegexOptions.Singleline | RegexOptions.CultureInvariant);
            for (int i = 0; i < matches.Count; i++) {
                uint itemId = uint.Parse(matches[i].Groups[1].Value, CultureInfo.InvariantCulture);
                int amount = int.Parse(matches[i].Groups[2].Value, CultureInfo.InvariantCulture);
                items.Add(new RoQuestRewardItem(itemId, amount));
            }

            return items;
        }

        private static string ReadStringField(string block, string fieldName) {
            string pattern = FieldPattern(fieldName) + @"\s*=\s*(['""])";
            Match match = Regex.Match(block, pattern, RegexOptions.CultureInvariant);
            if (!match.Success) {
                return string.Empty;
            }

            int quoteIndex = match.Groups[1].Index;
            return TryReadLuaString(block, quoteIndex, out string value, out _) ? value : string.Empty;
        }

        private static int ReadIntField(string block, string fieldName) {
            string pattern = FieldPattern(fieldName) + @"\s*=\s*['""]?(-?\d+)['""]?";
            Match match = Regex.Match(block, pattern, RegexOptions.CultureInvariant);
            return match.Success ? ParseInt(match.Groups[1].Value) : 0;
        }

        private static string FieldPattern(string fieldName) {
            string escaped = Regex.Escape(fieldName);
            return @"(?:\b" + escaped + @"\b|\[\s*['""]" + escaped + @"['""]\s*\])";
        }

        private static bool TryReadFieldBraceBlock(string block, string fieldName, out string fieldBlock) {
            fieldBlock = string.Empty;
            string pattern = FieldPattern(fieldName) + @"\s*=\s*\{";
            Match match = Regex.Match(block, pattern, RegexOptions.CultureInvariant);
            if (!match.Success) {
                return false;
            }

            int openBrace = block.IndexOf('{', match.Index + match.Length - 1);
            return openBrace >= 0 && TryReadBraceBlock(block, openBrace, out fieldBlock, out _);
        }

        private static List<string> ReadLuaStrings(string text) {
            var values = new List<string>();
            for (int i = 0; i < text.Length; i++) {
                if ((text[i] == '"' || text[i] == '\'') && TryReadLuaString(text, i, out string value, out int nextIndex)) {
                    values.Add(value);
                    i = nextIndex;
                }
            }

            return values;
        }

        private static IEnumerable<List<string>> ReadFunctionCalls(string text, string functionName) {
            string pattern = @"\b" + Regex.Escape(functionName) + @"\s*\(";
            MatchCollection matches = Regex.Matches(text, pattern, RegexOptions.CultureInvariant);
            for (int i = 0; i < matches.Count; i++) {
                Match match = matches[i];
                int openParen = text.IndexOf('(', match.Index + functionName.Length);
                if (openParen < 0 || !TryReadParenBlock(text, openParen, out string argsBlock, out _)) {
                    continue;
                }

                yield return SplitLuaArguments(argsBlock);
            }
        }

        private static List<string> SplitLuaArguments(string text) {
            var args = new List<string>();
            int start = 0;
            int depth = 0;
            bool inString = false;
            char quote = '\0';

            for (int i = 0; i < text.Length; i++) {
                char ch = text[i];
                if (inString) {
                    if (ch == '\\') {
                        i++;
                    } else if (ch == quote) {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"' || ch == '\'') {
                    inString = true;
                    quote = ch;
                    continue;
                }

                if (ch == '{' || ch == '(') {
                    depth++;
                } else if (ch == '}' || ch == ')') {
                    depth = Math.Max(0, depth - 1);
                } else if (ch == ',' && depth == 0) {
                    args.Add(text.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            if (start <= text.Length) {
                string tail = text.Substring(start).Trim();
                if (tail.Length > 0) {
                    args.Add(tail);
                }
            }

            return args;
        }

        private static string UnquoteLuaArgument(string value) {
            value = value != null ? value.Trim() : string.Empty;
            if (value.Length == 0 || value == "nil") {
                return string.Empty;
            }

            if ((value[0] == '"' || value[0] == '\'') && TryReadLuaString(value, 0, out string parsed, out _)) {
                return parsed;
            }

            return value;
        }

        private static void MergeParsedQuest(Dictionary<uint, RoQuestMetadata> result, RoQuestMetadata metadata) {
            if (metadata == null) {
                return;
            }

            if (result.TryGetValue(metadata.QuestId, out RoQuestMetadata existing)) {
                result[metadata.QuestId] = MergeQuestMetadata(existing, metadata);
            } else {
                result[metadata.QuestId] = metadata;
            }
        }

        private static int ParseInt(string value) {
            return int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        }

        private static bool TryReadBraceBlock(string text, int openBrace, out string block, out int endIndex) {
            return TryReadDelimitedBlock(text, openBrace, '{', '}', out block, out endIndex);
        }

        private static bool TryReadParenBlock(string text, int openParen, out string block, out int endIndex) {
            return TryReadDelimitedBlock(text, openParen, '(', ')', out block, out endIndex);
        }

        private static bool TryReadDelimitedBlock(string text, int openIndex, char open, char close, out string block, out int endIndex) {
            block = string.Empty;
            endIndex = openIndex;
            if (openIndex < 0 || openIndex >= text.Length || text[openIndex] != open) {
                return false;
            }

            int depth = 0;
            bool inString = false;
            char quote = '\0';
            for (int i = openIndex; i < text.Length; i++) {
                char ch = text[i];
                if (inString) {
                    if (ch == '\\') {
                        i++;
                    } else if (ch == quote) {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"' || ch == '\'') {
                    inString = true;
                    quote = ch;
                    continue;
                }

                if (ch == open) {
                    depth++;
                } else if (ch == close) {
                    depth--;
                    if (depth == 0) {
                        block = text.Substring(openIndex + 1, i - openIndex - 1);
                        endIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryReadLuaString(string text, int quoteIndex, out string value, out int endIndex) {
            value = string.Empty;
            endIndex = quoteIndex;
            if (quoteIndex < 0 || quoteIndex >= text.Length || (text[quoteIndex] != '"' && text[quoteIndex] != '\'')) {
                return false;
            }

            char quote = text[quoteIndex];
            var builder = new StringBuilder();
            for (int i = quoteIndex + 1; i < text.Length; i++) {
                char ch = text[i];
                if (ch == '\\' && i + 1 < text.Length) {
                    char next = text[++i];
                    switch (next) {
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case '\\':
                            builder.Append('\\');
                            break;
                        case '"':
                            builder.Append('"');
                            break;
                        case '\'':
                            builder.Append('\'');
                            break;
                        default:
                            builder.Append(next);
                            break;
                    }

                    continue;
                }

                if (ch == quote) {
                    value = builder.ToString();
                    endIndex = i;
                    return true;
                }

                builder.Append(ch);
            }

            return false;
        }

        private static bool TryReadQuestMetadataCache(
            RoAssetDatabase assets,
            IReadOnlyList<QuestSource> sources,
            out RoQuestMetadataTable table
        ) {
            table = null;
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null || !HasAnySourceBytes(sources)) {
                return false;
            }

            string cacheKey = BuildQuestMetadataCacheKey(sources);
            try {
                if (!cache.TryReadBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", out byte[] data)) {
                    return false;
                }

                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadInt32() != QuestMetadataCacheMagic ||
                    reader.ReadInt32() != QuestMetadataCacheVersion) {
                    return false;
                }

                int sourceCount = reader.ReadInt32();
                if (sourceCount < 0 || sourceCount > MaxCachedQuestSources || sources == null || sourceCount != sources.Count) {
                    return false;
                }

                for (int i = 0; i < sourceCount; i++) {
                    if (!IsSameSourcePath(reader.ReadString(), sources[i].Path)) {
                        return false;
                    }
                }

                string sourcePath = reader.ReadString();
                string loadError = reader.ReadString();
                int questCount = reader.ReadInt32();
                if (questCount < 0 || questCount > MaxCachedQuests) {
                    return false;
                }

                var quests = new Dictionary<uint, RoQuestMetadata>(questCount);
                for (int i = 0; i < questCount; i++) {
                    RoQuestMetadata quest = ReadQuestMetadataCacheEntry(reader);
                    quests[quest.QuestId] = quest;
                }

                table = new RoQuestMetadataTable(quests, sourcePath, loadError, true);
                return true;
            } catch {
                table = null;
                return false;
            }
        }

        private static void TryWriteQuestMetadataCache(
            RoAssetDatabase assets,
            IReadOnlyList<QuestSource> sources,
            RoQuestMetadataTable table
        ) {
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null || table == null || table.Count == 0 || !HasAnySourceBytes(sources)) {
                return;
            }

            string cacheKey = BuildQuestMetadataCacheKey(sources);
            try {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                    writer.Write(QuestMetadataCacheMagic);
                    writer.Write(QuestMetadataCacheVersion);
                    writer.Write(sources?.Count ?? 0);
                    if (sources != null) {
                        for (int i = 0; i < sources.Count; i++) {
                            writer.Write(sources[i].Path ?? string.Empty);
                        }
                    }

                    writer.Write(table.SourcePath ?? string.Empty);
                    writer.Write(table.LoadError ?? string.Empty);
                    writer.Write(table.quests.Count);
                    foreach (RoQuestMetadata quest in table.quests.Values) {
                        WriteQuestMetadataCacheEntry(writer, quest);
                    }
                }

                cache.WriteBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", stream.ToArray());
            } catch {
                // Parsed quest metadata cache is an optimization only; source Lua/text files remain authoritative.
            }
        }

        private static RoQuestMetadata ReadQuestMetadataCacheEntry(BinaryReader reader) {
            uint questId = reader.ReadUInt32();
            string title = reader.ReadString();
            string summary = reader.ReadString();
            string iconName = reader.ReadString();
            string npcSpr = reader.ReadString();
            string npcNavi = reader.ReadString();
            int npcPosX = reader.ReadInt32();
            int npcPosY = reader.ReadInt32();
            int rewardExp = reader.ReadInt32();
            int rewardJExp = reader.ReadInt32();
            IReadOnlyList<RoQuestRewardItem> rewardItems = ReadQuestRewardItems(reader);
            IReadOnlyList<string> descriptionLines = ReadQuestDescriptionLines(reader);
            string group = reader.ReadString();
            return new RoQuestMetadata(
                questId,
                title,
                summary,
                iconName,
                npcSpr,
                npcNavi,
                npcPosX,
                npcPosY,
                rewardExp,
                rewardJExp,
                rewardItems,
                descriptionLines,
                group);
        }

        private static void WriteQuestMetadataCacheEntry(BinaryWriter writer, RoQuestMetadata quest) {
            writer.Write(quest.QuestId);
            writer.Write(quest.Title ?? string.Empty);
            writer.Write(quest.Summary ?? string.Empty);
            writer.Write(quest.IconName ?? string.Empty);
            writer.Write(quest.NpcSpr ?? string.Empty);
            writer.Write(quest.NpcNavi ?? string.Empty);
            writer.Write(quest.NpcPosX);
            writer.Write(quest.NpcPosY);
            writer.Write(quest.RewardEXP);
            writer.Write(quest.RewardJEXP);
            WriteQuestRewardItems(writer, quest.RewardItems);
            WriteQuestDescriptionLines(writer, quest.DescriptionLines);
            writer.Write(quest.Group ?? string.Empty);
        }

        private static IReadOnlyList<RoQuestRewardItem> ReadQuestRewardItems(BinaryReader reader) {
            int count = reader.ReadInt32();
            if (count <= 0) {
                return Array.Empty<RoQuestRewardItem>();
            }

            if (count > MaxCachedQuestRewards) {
                throw new InvalidDataException("Cached quest reward list is too large.");
            }

            var values = new RoQuestRewardItem[count];
            for (int i = 0; i < count; i++) {
                values[i] = new RoQuestRewardItem(reader.ReadUInt32(), reader.ReadInt32());
            }

            return values;
        }

        private static void WriteQuestRewardItems(BinaryWriter writer, IReadOnlyList<RoQuestRewardItem> values) {
            writer.Write(values?.Count ?? 0);
            if (values == null) {
                return;
            }

            for (int i = 0; i < values.Count; i++) {
                writer.Write(values[i].ItemId);
                writer.Write(values[i].ItemNum);
            }
        }

        private static IReadOnlyList<string> ReadQuestDescriptionLines(BinaryReader reader) {
            int count = reader.ReadInt32();
            if (count <= 0) {
                return Array.Empty<string>();
            }

            if (count > MaxCachedQuestDescriptionLines) {
                throw new InvalidDataException("Cached quest description list is too large.");
            }

            var values = new string[count];
            for (int i = 0; i < count; i++) {
                values[i] = reader.ReadString();
            }

            return values;
        }

        private static void WriteQuestDescriptionLines(BinaryWriter writer, IReadOnlyList<string> values) {
            writer.Write(values?.Count ?? 0);
            if (values == null) {
                return;
            }

            for (int i = 0; i < values.Count; i++) {
                writer.Write(values[i] ?? string.Empty);
            }
        }

        private static string BuildQuestMetadataCacheKey(IReadOnlyList<QuestSource> sources) {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                writer.Write(sources?.Count ?? 0);
                if (sources != null) {
                    for (int i = 0; i < sources.Count; i++) {
                        WriteSourceForCacheKey(writer, sources[i]);
                    }
                }
            }

            return RoItemMetadataTable.BuildParsedSourceCacheKey(
                "quest-metadata",
                BuildQuestSourcePathKey(sources),
                stream.ToArray());
        }

        private static string BuildQuestSourcePathKey(IReadOnlyList<QuestSource> sources) {
            if (sources == null || sources.Count == 0) {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < sources.Count; i++) {
                if (i > 0) {
                    builder.Append('|');
                }

                builder.Append(sources[i].Path ?? string.Empty);
            }

            return builder.ToString();
        }

        private static void WriteSourceForCacheKey(BinaryWriter writer, QuestSource source) {
            writer.Write(source.Path ?? string.Empty);
            if (!string.IsNullOrEmpty(source.Fingerprint)) {
                writer.Write(source.Fingerprint);
                writer.Write(-2);
                return;
            }

            writer.Write(string.Empty);
            if (source.Bytes == null) {
                writer.Write(-1);
                return;
            }

            writer.Write(source.Bytes.Length);
            writer.Write(source.Bytes);
        }

        private static bool HasAnySourceBytes(IReadOnlyList<QuestSource> sources) {
            if (sources == null) {
                return false;
            }

            for (int i = 0; i < sources.Count; i++) {
                if (!string.IsNullOrEmpty(sources[i].Fingerprint) ||
                    (sources[i].Bytes != null && sources[i].Bytes.Length > 0)) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameSourcePath(string cachedPath, string sourcePath) {
            return string.Equals(cachedPath ?? string.Empty, sourcePath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private readonly struct QuestSource {
            public QuestSource(string path, string fingerprint, byte[] bytes, bool isReadableText) {
                Path = path ?? string.Empty;
                Fingerprint = fingerprint ?? string.Empty;
                Bytes = bytes;
                IsReadableText = isReadableText;
            }

            public string Path { get; }
            public string Fingerprint { get; }
            public byte[] Bytes { get; }
            public bool HasBytes => Bytes != null;
            public bool IsReadableText { get; }
        }

        private static string StripLuaComments(string text) {
            var builder = new StringBuilder(text.Length);
            bool inString = false;
            char quote = '\0';

            for (int i = 0; i < text.Length; i++) {
                char ch = text[i];
                if (inString) {
                    builder.Append(ch);
                    if (ch == '\\' && i + 1 < text.Length) {
                        builder.Append(text[++i]);
                    } else if (ch == quote) {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"' || ch == '\'') {
                    inString = true;
                    quote = ch;
                    builder.Append(ch);
                    continue;
                }

                if (ch == '-' && i + 1 < text.Length && text[i + 1] == '-') {
                    i += 2;
                    while (i < text.Length && text[i] != '\n' && text[i] != '\r') {
                        i++;
                    }

                    if (i < text.Length) {
                        builder.Append(text[i]);
                    }

                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }
    }
}
