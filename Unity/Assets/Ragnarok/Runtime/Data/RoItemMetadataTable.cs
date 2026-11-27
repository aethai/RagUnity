using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public sealed class RoItemMetadataTable {
        private const int ItemInfoCacheMagic = 0x494D5449; // ITMI
        private const int ItemInfoCacheVersion = 1;
        private const int MaxCachedItems = 1_000_000;
        private static readonly Regex ItemEntryStartPattern = new Regex(
            @"\[\s*(?<id>\d+)\s*\]\s*=\s*\{",
            RegexOptions.Compiled
        );

        private static readonly Regex LuaStringPattern = new Regex(
            @"(?:(?<quote>[""'])(?<quoted>(?:\\.|(?!\k<quote>).)*)\k<quote>)|(?:\[(?<equals>=*)\[(?<long>.*?)\]\k<equals>\])",
            RegexOptions.Compiled | RegexOptions.Singleline
        );

        private readonly Dictionary<int, RoItemMetadata> items;

        public static readonly RoItemMetadataTable Empty = new RoItemMetadataTable(
            new Dictionary<int, RoItemMetadata>(),
            null,
            null,
            null,
            0,
            null
        );

        private RoItemMetadataTable(
            Dictionary<int, RoItemMetadata> items,
            string sourcePath,
            string loadError,
            string itemDbSourcePath,
            int itemDbCount,
            string itemDbLoadError
        ) {
            this.items = items ?? new Dictionary<int, RoItemMetadata>();
            SourcePath = sourcePath;
            LoadError = loadError;
            ItemDbSourcePath = itemDbSourcePath;
            ItemDbCount = Math.Max(0, itemDbCount);
            ItemDbLoadError = itemDbLoadError;
        }

        public IReadOnlyDictionary<int, RoItemMetadata> Items => items;
        public int Count => items.Count;
        public string SourcePath { get; }
        public string LoadError { get; }
        public string ItemDbSourcePath { get; }
        public int ItemDbCount { get; }
        public string ItemDbLoadError { get; }
        public bool IsLoaded => Count > 0;
        public bool HasDatabaseMetadata => ItemDbCount > 0;

        public static RoItemMetadataTable Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            RoItemMetadataTable itemInfo = LoadItemInfo(assets);
            return itemInfo
                .WithDatabaseMetadata(RoItemDbMetadataTable.Load(assets))
                .WithClientItemTables(assets);
        }

        private static RoItemMetadataTable LoadItemInfo(RoAssetDatabase assets) {
            string error = null;
            string[] candidates = GetItemInfoCandidatePaths();
            for (int i = 0; i < candidates.Length; i++) {
                string candidate = candidates[i];
                if (!assets.Exists(candidate)) {
                    continue;
                }

                string sourcePath = candidate;
                string fingerprintCacheKey = null;
                if (assets.TryGetAssetFingerprint(candidate, out string resolvedPath, out string fingerprint)) {
                    sourcePath = resolvedPath;
                    fingerprintCacheKey = BuildParsedSourceFingerprintCacheKey("item-info", sourcePath, fingerprint);
                    if (TryReadItemInfoCache(assets, sourcePath, fingerprintCacheKey, out RoItemMetadataTable fingerprintCachedTable)) {
                        return fingerprintCachedTable;
                    }
                }

                byte[] bytes;
                try {
                    bytes = assets.ReadBytes(candidate);
                } catch (Exception ex) {
                    error = ex.Message;
                    continue;
                }

                if (bytes == null || bytes.Length == 0) {
                    continue;
                }

                if (bytes[0] == 0x1B) {
                    bytes = RoLuaBytecodeSource.ToSource(assets, candidate, bytes, out string luaError);
                    if (bytes == null) {
                        error = luaError;
                        continue;
                    }
                }

                string sourceBytesCacheKey = BuildParsedSourceCacheKey("item-info", sourcePath, bytes);
                if (TryReadItemInfoCache(assets, sourcePath, sourceBytesCacheKey, out RoItemMetadataTable cachedTable)) {
                    if (!string.IsNullOrEmpty(fingerprintCacheKey)) {
                        TryWriteItemInfoCache(assets, sourcePath, fingerprintCacheKey, cachedTable);
                    }

                    return cachedTable;
                }

                RoItemMetadataTable table = FromText(RoTextEncoding.Decode(bytes), sourcePath);
                TryWriteItemInfoCache(
                    assets,
                    sourcePath,
                    fingerprintCacheKey ?? sourceBytesCacheKey,
                    table);
                if (table.Count > 0) {
                    return table;
                }

                error = string.IsNullOrEmpty(table.LoadError) ? "no item records parsed" : table.LoadError;
            }

            return new RoItemMetadataTable(new Dictionary<int, RoItemMetadata>(), null, error, null, 0, null);
        }

        public static RoItemMetadataTable FromText(string text, string sourcePath = null) {
            var parsed = new Dictionary<int, RoItemMetadata>(EstimateItemInfoCapacity(text));
            if (string.IsNullOrWhiteSpace(text)) {
                return new RoItemMetadataTable(parsed, sourcePath, null, null, 0, null);
            }

            string stripped = StripComments(text);
            foreach (Match match in ItemEntryStartPattern.Matches(stripped)) {
                if (!int.TryParse(match.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)) {
                    continue;
                }

                int openBraceIndex = stripped.IndexOf('{', match.Index + match.Length - 1);
                if (openBraceIndex < 0) {
                    continue;
                }

                int closeBraceIndex = FindMatchingBrace(stripped, openBraceIndex);
                if (closeBraceIndex <= openBraceIndex) {
                    continue;
                }

                string body = stripped.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
                RoItemMetadata item = ParseItem(itemId, body);
                if (item != null) {
                    parsed[itemId] = item;
                }
            }

            return new RoItemMetadataTable(parsed, sourcePath, null, null, 0, null);
        }

        public bool TryGetItem(int itemId, out RoItemMetadata item) {
            return items.TryGetValue(itemId, out item);
        }

        public RoItemMetadata GetItemOrFallback(int itemId) {
            if (items.TryGetValue(itemId, out RoItemMetadata item)) {
                return item;
            }

            return new RoItemMetadata(
                itemId,
                "Unknown Item",
                "unknown",
                new[] { "..." },
                "Unknown Item",
                "unknown",
                new[] { "..." },
                0,
                0,
                false
            );
        }

        public RoItemMetadataTable WithDatabaseMetadata(RoItemDbMetadataTable itemDb) {
            if (itemDb == null || itemDb.Count == 0) {
                return new RoItemMetadataTable(
                    CopyItems(items, 0),
                    SourcePath,
                    LoadError,
                    itemDb?.SourcePath,
                    0,
                    itemDb?.LoadError
                );
            }

            var merged = CopyItems(items, itemDb.Count);
            foreach (KeyValuePair<int, RoItemDbMetadata> entry in itemDb.Items) {
                if (merged.TryGetValue(entry.Key, out RoItemMetadata existing)) {
                    merged[entry.Key] = existing.WithDatabaseMetadata(entry.Value);
                } else {
                    RoItemMetadata metadata = RoItemMetadata.FromDatabaseMetadata(entry.Value);
                    if (metadata != null) {
                        merged[entry.Key] = metadata;
                    }
                }
            }

            return new RoItemMetadataTable(
                merged,
                SourcePath,
                LoadError,
                itemDb.SourcePath,
                itemDb.Count,
                itemDb.LoadError
            );
        }

        public static string[] GetItemInfoCandidatePaths() {
            return new[] {
                "System/itemInfo_true.lub",
                "System/itemInfo_true.lua",
                "System/itemInfo.lub",
                "System/itemInfo.lua",
                "System/itemInfo.lua.bak",
                "System/iteminfo.lua.bak",
                "itemInfo_true.lub",
                "itemInfo_true.lua",
                "itemInfo.lub",
                "itemInfo.lua",
                "itemInfo.lua.bak",
                "iteminfo.lua.bak",
                "data/luafiles514/lua files/datainfo/iteminfo.lub",
                "data/luafiles514/lua files/datainfo/iteminfo.lua",
                "data/lua files/datainfo/iteminfo.lub",
                "data/lua files/datainfo/iteminfo.lua",
            };
        }

        private static RoItemMetadata ParseItem(int itemId, string body) {
            return new RoItemMetadata(
                itemId,
                ReadStringField(body, "unidentifiedDisplayName"),
                ReadStringField(body, "unidentifiedResourceName"),
                ReadStringListField(body, "unidentifiedDescriptionName"),
                ReadStringField(body, "identifiedDisplayName"),
                ReadStringField(body, "identifiedResourceName"),
                ReadStringListField(body, "identifiedDescriptionName"),
                ReadIntField(body, "slotCount"),
                ReadIntField(body, "ClassNum"),
                ReadBoolField(body, "costume")
            );
        }

        public RoItemMetadataTable WithClientItemTables(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized || Count == 0) {
                return this;
            }

            var overlays = new Dictionary<int, ClientItemTableOverlay>();
            LoadDelimitedClientItemTable(assets, "data/num2cardillustnametable.txt", (itemId, value) => {
                ClientItemTableOverlay overlay = GetOverlay(overlays, itemId);
                overlay.CardIllustrationResourceName = value;
                overlays[itemId] = overlay;
            });
            LoadDelimitedClientItemTable(assets, "data/cardprefixnametable.txt", (itemId, value) => {
                ClientItemTableOverlay overlay = GetOverlay(overlays, itemId);
                overlay.CardPrefixName = value;
                overlays[itemId] = overlay;
            });
            LoadSingleColumnClientItemTable(assets, "data/cardpostfixnametable.txt", itemId => {
                ClientItemTableOverlay overlay = GetOverlay(overlays, itemId);
                overlay.HasCardPostfix = true;
                overlays[itemId] = overlay;
            });
            LoadItemMoveInfoTable(assets, overlays);

            if (overlays.Count == 0) {
                return this;
            }

            var merged = CopyItems(items, 0);
            foreach (KeyValuePair<int, ClientItemTableOverlay> entry in overlays) {
                RoItemMetadata existing = merged.TryGetValue(entry.Key, out RoItemMetadata item)
                    ? item
                    : GetItemOrFallback(entry.Key);
                ClientItemTableOverlay overlay = entry.Value;
                merged[entry.Key] = existing.WithClientItemTables(
                    overlay.CardPrefixName,
                    overlay.HasCardPostfix ? true : (bool?) null,
                    overlay.CardIllustrationResourceName,
                    overlay.HasMoveInfo ? overlay.MoveInfo : (RoItemMoveInfo?) null);
            }

            return new RoItemMetadataTable(
                merged,
                SourcePath,
                LoadError,
                ItemDbSourcePath,
                ItemDbCount,
                ItemDbLoadError
            );
        }

        private static string ReadStringField(string body, string fieldName) {
            int valueStart = FindFieldValueStart(body, fieldName);
            if (valueStart < 0) {
                return string.Empty;
            }

            Match value = LuaStringPattern.Match(body, valueStart);
            return value.Success && value.Index == valueStart
                ? DecodeLuaString(value)
                : string.Empty;
        }

        private static int ReadIntField(string body, string fieldName) {
            int valueStart = FindFieldValueStart(body, fieldName);
            return valueStart >= 0 && TryParseIntegerAt(body, valueStart, out int value) ? value : 0;
        }

        private static bool ReadBoolField(string body, string fieldName) {
            int valueStart = FindFieldValueStart(body, fieldName);
            return valueStart >= 0 && MatchesKeyword(body, valueStart, "true");
        }

        private static IReadOnlyList<string> ReadStringListField(string body, string fieldName) {
            if (!TryReadTableBodyRange(body, fieldName, out int start, out int length) || length <= 0) {
                return Array.Empty<string>();
            }

            var values = new List<string>();
            int end = start + length;
            for (Match match = LuaStringPattern.Match(body, start);
                 match.Success && match.Index < end;
                 match = match.NextMatch()) {
                if (match.Index + match.Length > end) {
                    break;
                }

                values.Add(DecodeLuaString(match));
            }

            return values;
        }

        private static bool TryReadTableBodyRange(string body, string fieldName, out int start, out int length) {
            start = 0;
            length = 0;
            int valueStart = FindFieldValueStart(body, fieldName);
            if (valueStart < 0 || valueStart >= body.Length || body[valueStart] != '{') {
                return false;
            }

            int closeBraceIndex = FindMatchingBrace(body, valueStart);
            if (closeBraceIndex <= valueStart) {
                return false;
            }

            start = valueStart + 1;
            length = closeBraceIndex - valueStart - 1;
            return true;
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

                if (index < body.Length && body[index] == '=') {
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
            value = 0;
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

                return hexEnd > hexStart &&
                    int.TryParse(text.Substring(hexStart, hexEnd - hexStart), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            int end = index;
            while (end < text.Length && char.IsDigit(text[end])) {
                end++;
            }

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

        private static string DecodeLuaString(Match match) {
            if (match == null || !match.Success) {
                return string.Empty;
            }

            Group longValue = match.Groups["long"];
            if (longValue.Success) {
                return NormalizeDecodedString(longValue.Value);
            }

            return DecodeLuaString(match.Groups["quoted"].Value);
        }

        private static string DecodeLuaString(string value) {
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
                            byte.TryParse(value.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte hexByte)) {
                            chars.Add((char) hexByte);
                            i += 2;
                        } else {
                            chars.Add(next);
                        }
                        break;
                    default:
                        if (char.IsDigit(next)) {
                            int start = i;
                            int length = 1;
                            while (length < 3 && i + 1 < value.Length && char.IsDigit(value[i + 1])) {
                                i++;
                                length++;
                            }

                            string digits = value.Substring(start, length);
                            if (byte.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte decimalByte)) {
                                chars.Add((char) decimalByte);
                            } else {
                                chars.Add(next);
                            }
                        } else {
                            chars.Add(next);
                        }
                        break;
                }
            }

            string unescaped = new string(chars.ToArray());
            return NormalizeDecodedString(unescaped);
        }

        private static string NormalizeDecodedString(string unescaped) {
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(unescaped);
            return string.IsNullOrEmpty(decoded) ? unescaped : decoded;
        }

        private static ClientItemTableOverlay GetOverlay(Dictionary<int, ClientItemTableOverlay> overlays, int itemId) {
            return overlays.TryGetValue(itemId, out ClientItemTableOverlay overlay) ? overlay : default;
        }

        private static void LoadDelimitedClientItemTable(
            RoAssetDatabase assets,
            string path,
            Action<int, string> apply
        ) {
            if (assets == null || !assets.Exists(path) || apply == null) {
                return;
            }

            string text;
            try {
                text = assets.ReadText(path);
            } catch {
                return;
            }

            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }

            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++) {
                string line = StripInlineComment(lines[i]).Trim();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                string[] parts = line.Split('#');
                if (parts.Length < 2 ||
                    !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)) {
                    continue;
                }

                string value = NormalizeDecodedString(parts[1].Trim());
                if (!string.IsNullOrEmpty(value)) {
                    apply(itemId, value);
                }
            }
        }

        private static void LoadSingleColumnClientItemTable(
            RoAssetDatabase assets,
            string path,
            Action<int> apply
        ) {
            if (assets == null || !assets.Exists(path) || apply == null) {
                return;
            }

            string text;
            try {
                text = assets.ReadText(path);
            } catch {
                return;
            }

            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }

            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++) {
                string line = StripInlineComment(lines[i]).Trim();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                string key = line.Split('#')[0].Trim();
                if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)) {
                    apply(itemId);
                }
            }
        }

        private static void LoadItemMoveInfoTable(
            RoAssetDatabase assets,
            Dictionary<int, ClientItemTableOverlay> overlays
        ) {
            const string path = "data/ItemMoveInfoV5.txt";
            if (assets == null || !assets.Exists(path) || overlays == null) {
                return;
            }

            string text;
            try {
                text = assets.ReadText(path);
            } catch {
                return;
            }

            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }

            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++) {
                string line = StripInlineComment(lines[i]).Trim();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                string[] columns = Regex.Split(line, @"\s+");
                if (columns.Length < 9 ||
                    !int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)) {
                    continue;
                }

                ClientItemTableOverlay overlay = GetOverlay(overlays, itemId);
                overlay.MoveInfo = new RoItemMoveInfo(
                    columns[1] == "1",
                    columns[2] == "1",
                    columns[3] == "1",
                    columns[4] == "1",
                    columns[5] == "1",
                    columns[6] == "1",
                    columns[7] == "1",
                    columns[8] == "1");
                overlay.HasMoveInfo = true;
                overlays[itemId] = overlay;
            }
        }

        private static string StripInlineComment(string line) {
            if (string.IsNullOrEmpty(line)) {
                return string.Empty;
            }

            int comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment >= 0 ? line.Substring(0, comment) : line;
        }

        private struct ClientItemTableOverlay {
            public string CardPrefixName;
            public bool HasCardPostfix;
            public string CardIllustrationResourceName;
            public bool HasMoveInfo;
            public RoItemMoveInfo MoveInfo;
        }

        private static Dictionary<int, RoItemMetadata> CopyItems(Dictionary<int, RoItemMetadata> source, int extraCapacity) {
            int capacity = checked((source?.Count ?? 0) + Math.Max(0, extraCapacity));
            var copy = capacity > 0
                ? new Dictionary<int, RoItemMetadata>(capacity)
                : new Dictionary<int, RoItemMetadata>();
            if (source == null) {
                return copy;
            }

            foreach (KeyValuePair<int, RoItemMetadata> entry in source) {
                copy[entry.Key] = entry.Value;
            }

            return copy;
        }

        private static int EstimateItemInfoCapacity(string text) {
            if (string.IsNullOrEmpty(text)) {
                return 0;
            }

            return Math.Min(100_000, Math.Max(0, text.Length / 450));
        }

        private static bool TryReadItemInfoCache(
            RoAssetDatabase assets,
            string sourcePath,
            string cacheKey,
            out RoItemMetadataTable table) {
            table = null;
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null || string.IsNullOrEmpty(cacheKey)) {
                return false;
            }

            try {
                if (!cache.TryReadBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", out byte[] data)) {
                    return false;
                }

                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadInt32() != ItemInfoCacheMagic ||
                    reader.ReadInt32() != ItemInfoCacheVersion) {
                    return false;
                }

                string cachedSourcePath = reader.ReadString();
                if (!string.Equals(cachedSourcePath, sourcePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }

                int count = reader.ReadInt32();
                if (count < 0 || count > MaxCachedItems) {
                    return false;
                }

                var parsed = new Dictionary<int, RoItemMetadata>(count);
                for (int i = 0; i < count; i++) {
                    RoItemMetadata item = ReadItemInfoCacheEntry(reader);
                    parsed[item.ItemId] = item;
                }

                table = new RoItemMetadataTable(parsed, sourcePath, null, null, 0, null);
                return true;
            } catch {
                table = null;
                return false;
            }
        }

        private static void TryWriteItemInfoCache(
            RoAssetDatabase assets,
            string sourcePath,
            string cacheKey,
            RoItemMetadataTable table) {
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null || string.IsNullOrEmpty(cacheKey) || table == null || table.Count == 0) {
                return;
            }

            try {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                    writer.Write(ItemInfoCacheMagic);
                    writer.Write(ItemInfoCacheVersion);
                    writer.Write(sourcePath ?? string.Empty);
                    writer.Write(table.items.Count);
                    foreach (RoItemMetadata item in table.items.Values) {
                        WriteItemInfoCacheEntry(writer, item);
                    }
                }

                cache.WriteBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", stream.ToArray());
            } catch {
                // Disk item metadata cache is only an optimization; source parsing stays authoritative.
            }
        }

        private static RoItemMetadata ReadItemInfoCacheEntry(BinaryReader reader) {
            int itemId = reader.ReadInt32();
            string unidentifiedDisplayName = reader.ReadString();
            string unidentifiedResourceName = reader.ReadString();
            IReadOnlyList<string> unidentifiedDescription = ReadStringList(reader);
            string identifiedDisplayName = reader.ReadString();
            string identifiedResourceName = reader.ReadString();
            IReadOnlyList<string> identifiedDescription = ReadStringList(reader);
            int slotCount = reader.ReadInt32();
            int classNumber = reader.ReadInt32();
            bool isCostume = reader.ReadBoolean();

            return new RoItemMetadata(
                itemId,
                unidentifiedDisplayName,
                unidentifiedResourceName,
                unidentifiedDescription,
                identifiedDisplayName,
                identifiedResourceName,
                identifiedDescription,
                slotCount,
                classNumber,
                isCostume
            );
        }

        private static void WriteItemInfoCacheEntry(BinaryWriter writer, RoItemMetadata item) {
            writer.Write(item.ItemId);
            writer.Write(item.UnidentifiedDisplayName ?? string.Empty);
            writer.Write(item.UnidentifiedResourceName ?? string.Empty);
            WriteStringList(writer, item.UnidentifiedDescription);
            writer.Write(item.IdentifiedDisplayName ?? string.Empty);
            writer.Write(item.IdentifiedResourceName ?? string.Empty);
            WriteStringList(writer, item.IdentifiedDescription);
            writer.Write(item.SlotCount);
            writer.Write(item.ClassNumber);
            writer.Write(item.IsCostume);
        }

        internal static string BuildParsedSourceCacheKey(string prefix, string sourcePath, byte[] sourceBytes) {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(sourceBytes ?? Array.Empty<byte>());
            var builder = new StringBuilder(prefix.Length + (sourcePath?.Length ?? 0) + 96);
            builder.Append(prefix);
            builder.Append("|v1|");
            builder.Append(sourcePath ?? string.Empty);
            builder.Append('|');
            builder.Append(sourceBytes?.Length ?? 0);
            builder.Append('|');
            for (int i = 0; i < hash.Length; i++) {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }

        internal static string BuildParsedSourceFingerprintCacheKey(string prefix, string sourcePath, string fingerprint) {
            byte[] bytes = Encoding.UTF8.GetBytes(fingerprint ?? string.Empty);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder(prefix.Length + (sourcePath?.Length ?? 0) + 96);
            builder.Append(prefix);
            builder.Append("|v2fp|");
            builder.Append(sourcePath ?? string.Empty);
            builder.Append('|');
            builder.Append(bytes.Length);
            builder.Append('|');
            for (int i = 0; i < hash.Length; i++) {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }

        internal static IReadOnlyList<string> ReadStringList(BinaryReader reader) {
            int count = reader.ReadInt32();
            if (count <= 0) {
                return Array.Empty<string>();
            }

            if (count > 100_000) {
                throw new InvalidDataException("Cached string list is too large.");
            }

            var values = new string[count];
            for (int i = 0; i < count; i++) {
                values[i] = reader.ReadString();
            }

            return values;
        }

        internal static void WriteStringList(BinaryWriter writer, IReadOnlyList<string> values) {
            if (values == null || values.Count == 0) {
                writer.Write(0);
                return;
            }

            writer.Write(values.Count);
            for (int i = 0; i < values.Count; i++) {
                writer.Write(values[i] ?? string.Empty);
            }
        }

        private static string StripComments(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            bool inString = false;
            char quote = '\0';
            for (int i = 0; i < text.Length; i++) {
                char current = text[i];
                if (inString) {
                    builder.Append(current);
                    if (current == '\\' && i + 1 < text.Length) {
                        builder.Append(text[++i]);
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
                    builder.Append(current);
                    continue;
                }

                if (current == '[' && TryFindLongBracketCloseIndex(text, i, out int closeIndex)) {
                    builder.Append(text, i, closeIndex - i + 1);
                    i = closeIndex;
                    continue;
                }

                if (current == '-' && i + 1 < text.Length && text[i + 1] == '-') {
                    i = SkipUntilNewline(text, i);
                    builder.Append('\n');
                    continue;
                }

                if (current == '/' && i + 1 < text.Length && text[i + 1] == '/') {
                    i = SkipUntilNewline(text, i);
                    builder.Append('\n');
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static int SkipUntilNewline(string text, int index) {
            while (index + 1 < text.Length && text[index + 1] != '\n') {
                index++;
            }

            return index;
        }

        private static bool TryFindLongBracketCloseIndex(string text, int openIndex, out int closeIndex) {
            closeIndex = -1;
            if (string.IsNullOrEmpty(text) || openIndex < 0 || openIndex >= text.Length || text[openIndex] != '[') {
                return false;
            }

            int index = openIndex + 1;
            while (index < text.Length && text[index] == '=') {
                index++;
            }

            if (index >= text.Length || text[index] != '[') {
                return false;
            }

            string terminator = "]" + new string('=', index - openIndex - 1) + "]";
            closeIndex = text.IndexOf(terminator, index + 1, StringComparison.Ordinal);
            if (closeIndex < 0) {
                return false;
            }

            closeIndex += terminator.Length - 1;
            return true;
        }

        private static int FindMatchingBrace(string text, int openBraceIndex) {
            int depth = 0;
            bool inString = false;
            char quote = '\0';
            for (int i = openBraceIndex; i < text.Length; i++) {
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

                if (current == '[' && TryFindLongBracketCloseIndex(text, i, out int closeIndex)) {
                    i = closeIndex;
                    continue;
                }

                if (current == '{') {
                    depth++;
                    continue;
                }

                if (current == '}') {
                    depth--;
                    if (depth == 0) {
                        return i;
                    }
                }
            }

            return -1;
        }
    }
}
