using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Ragnarok.Assets;
using UnityEngine;

namespace Ragnarok.Data {
    public enum RoSignBoardItemType {
        None = 0,
        Bitmap = 1,
        Sprite = 2,
        SignBoard = 3
    }

    public sealed class RoSignBoardEntry {
        public RoSignBoardEntry(
            string mapId,
            int cellX,
            int cellY,
            float height,
            RoSignBoardItemType itemType,
            string assetPath,
            string displayText,
            Color32 textColor,
            string sourcePath
        ) {
            MapId = RoWorldMapDataUtility.NormalizeMapId(mapId);
            CellX = cellX;
            CellY = cellY;
            Height = height;
            ItemType = itemType;
            AssetPath = NormalizeText(assetPath);
            TexturePath = RoSignBoardMetadataTable.ResolveTexturePath(AssetPath);
            RawDisplayText = NormalizeText(displayText);
            DisplayText = NormalizeDisplayText(displayText);
            TextColor = textColor;
            SourcePath = sourcePath ?? string.Empty;
        }

        public string MapId { get; }
        public int CellX { get; }
        public int CellY { get; }
        public float Height { get; }
        public RoSignBoardItemType ItemType { get; }
        public string AssetPath { get; }
        public string TexturePath { get; }
        public string DisplayText { get; }

        /// <summary>The contents as the row has them: the client keeps the leading spaces that indent the text.</summary>
        public string RawDisplayText { get; }
        public Color32 TextColor { get; }
        public string SourcePath { get; }
        public bool HasDisplayText => !string.IsNullOrWhiteSpace(DisplayText);

        public static string NormalizeDisplayText(string value) {
            string normalized = NormalizeText(value);
            return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized.Trim();
        }

        private static string NormalizeText(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            string withoutNulls = value.Replace("\0", string.Empty);
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(withoutNulls);
            return string.IsNullOrEmpty(decoded) ? withoutNulls : decoded;
        }
    }

    public sealed class RoSignBoardMetadataTable {
        private const string DefaultTextColor = "#0x00FFFFFF";
        private const string InterfaceRootPrefix = "data/texture/";

        private static readonly ConditionalWeakTable<RoAssetDatabase, CacheEntry> Cache =
            new ConditionalWeakTable<RoAssetDatabase, CacheEntry>();

        private static readonly string[] CandidatePaths = {
            "data/luafiles514/lua files/signboardlist.lub",
            "data/luafiles514/lua files/signboardlist.lua",
            "data/LuaFiles514/Lua Files/signboardlist.lub",
            "data/LuaFiles514/Lua Files/signboardlist.lua",
            "data/lua files/signboardlist.lub",
            "data/lua files/signboardlist.lua",
            "data/Lua Files/signboardlist.lub",
            "data/Lua Files/signboardlist.lua"
        };

        private static readonly string[] SuffixCandidates = {
            "lua files/signboardlist.lub",
            "lua files/signboardlist.lua",
            "signboardlist.lub",
            "signboardlist.lua"
        };

        public static readonly RoSignBoardMetadataTable Empty = new RoSignBoardMetadataTable(
            Array.Empty<RoSignBoardEntry>(),
            string.Empty,
            Array.Empty<string>());

        private readonly Dictionary<string, IReadOnlyList<RoSignBoardEntry>> entriesByMap =
            new Dictionary<string, IReadOnlyList<RoSignBoardEntry>>(StringComparer.OrdinalIgnoreCase);

        public RoSignBoardMetadataTable(
            IReadOnlyList<RoSignBoardEntry> entries,
            string sourcePath,
            IReadOnlyList<string> diagnostics
        ) {
            Entries = entries ?? Array.Empty<RoSignBoardEntry>();
            SourcePath = sourcePath ?? string.Empty;
            Diagnostics = diagnostics ?? Array.Empty<string>();
            BuildMapIndex(Entries);
        }

        public IReadOnlyList<RoSignBoardEntry> Entries { get; }
        public string SourcePath { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsLoaded => Entries.Count > 0;

        public static RoSignBoardMetadataTable Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return new RoSignBoardMetadataTable(
                    Array.Empty<RoSignBoardEntry>(),
                    string.Empty,
                    new[] { "RO asset database is not initialized; SignBoardList cannot be loaded." });
            }

            return Cache.GetValue(assets, key => new CacheEntry(LoadUncached(key))).Table;
        }

        public static RoSignBoardMetadataTable FromText(string text, string sourcePath = "signboardlist.lua") {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            var file = new RoWorldMapLuaFile(sourcePath ?? "signboardlist.lua", bytes);
            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(new[] { file });
            return BuildFromContext(context, file.Path);
        }

        public IReadOnlyList<RoSignBoardEntry> GetEntriesForMap(string mapName) {
            string mapId = RoWorldMapDataUtility.NormalizeMapId(mapName);
            return entriesByMap.TryGetValue(mapId, out IReadOnlyList<RoSignBoardEntry> entries)
                ? entries
                : Array.Empty<RoSignBoardEntry>();
        }

        public static string ResolveTexturePath(string rawPath) {
            string path = NormalizeAssetReference(rawPath);
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            if (path.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) {
                return RoAssetPath.Normalize(path);
            }

            if (path.StartsWith("texture/", StringComparison.OrdinalIgnoreCase)) {
                return RoAssetPath.Normalize("data/" + path);
            }

            string interfaceSegment = InterfaceSegment();
            if (path.StartsWith(interfaceSegment + "/", StringComparison.OrdinalIgnoreCase)) {
                path = path.Substring(interfaceSegment.Length + 1);
            }

            return BuildInterfaceTexturePath(path);
        }

        public static string BuildInterfaceTexturePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return string.Empty;
            }

            string normalized = NormalizeAssetReference(relativePath);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return string.Empty;
            }

            if (normalized.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) {
                return RoAssetPath.Normalize(normalized);
            }

            string interfaceSegment = InterfaceSegment();
            if (normalized.StartsWith(interfaceSegment + "/", StringComparison.OrdinalIgnoreCase)) {
                normalized = normalized.Substring(interfaceSegment.Length + 1);
            }

            return RoAssetPath.Normalize(InterfaceRootPrefix + interfaceSegment + "/" + normalized);
        }

        public static bool TryParseColor(string value, out Color32 color) {
            color = new Color32(255, 255, 255, 255);
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            string hex = value.Trim();
            if (hex.StartsWith("#", StringComparison.Ordinal)) {
                hex = hex.Substring(1);
            }

            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                hex = hex.Substring(2);
            }

            if (hex.Length >= 8) {
                hex = hex.Substring(hex.Length - 6, 6);
            }

            if (hex.Length != 6 ||
                !byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
                !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
                !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b)) {
                return false;
            }

            color = new Color32(r, g, b, 255);
            return true;
        }

        private static RoSignBoardMetadataTable LoadUncached(RoAssetDatabase assets) {
            if (!TryLoadSignBoardFile(assets, out RoWorldMapLuaFile file, out string diagnostic)) {
                return new RoSignBoardMetadataTable(
                    Array.Empty<RoSignBoardEntry>(),
                    string.Empty,
                    string.IsNullOrEmpty(diagnostic) ? Array.Empty<string>() : new[] { diagnostic });
            }

            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(new[] { file });
            return BuildFromContext(context, file.Path);
        }

        private static RoSignBoardMetadataTable BuildFromContext(RoWorldMapLuaExecutionContext context, string sourcePath) {
            var diagnostics = new List<string>();
            if (context != null) {
                diagnostics.AddRange(context.Diagnostics);
            }

            LuaValue root = context != null ? context.GetGlobal("SignBoardList") : LuaValue.Nil;
            if (root.Kind != LuaValueKind.Table) {
                diagnostics.Add("SignBoardList table was not found in '" + (sourcePath ?? string.Empty) + "'.");
                return new RoSignBoardMetadataTable(Array.Empty<RoSignBoardEntry>(), sourcePath, diagnostics);
            }

            var entries = new List<RoSignBoardEntry>();
            IReadOnlyList<LuaValue> rows = root.Table.ArrayValues;
            for (int i = 0; i < rows.Count; i++) {
                LuaValue row = rows[i];
                if (row.Kind != LuaValueKind.Table) {
                    continue;
                }

                if (TryParseEntry(row.Table, sourcePath, out RoSignBoardEntry entry, out string entryDiagnostic)) {
                    entries.Add(entry);
                } else if (!string.IsNullOrEmpty(entryDiagnostic)) {
                    diagnostics.Add(entryDiagnostic);
                }
            }

            return new RoSignBoardMetadataTable(entries, sourcePath, diagnostics);
        }

        private static bool TryParseEntry(
            LuaTableValue row,
            string sourcePath,
            out RoSignBoardEntry entry,
            out string diagnostic
        ) {
            entry = null;
            diagnostic = null;
            if (row == null) {
                return false;
            }

            string mapName = ValueToString(row.Get(1));
            string assetPath = ValueToString(row.Get(6));
            if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(assetPath)) {
                diagnostic = "Skipped SignBoardList row with missing map or asset path in '" + (sourcePath ?? string.Empty) + "'.";
                return false;
            }

            TryParseColor(ValueToString(row.Get(8)), out Color32 color);
            if (string.IsNullOrWhiteSpace(ValueToString(row.Get(8)))) {
                TryParseColor(DefaultTextColor, out color);
            }

            entry = new RoSignBoardEntry(
                mapName,
                ValueToInt(row.Get(2)),
                ValueToInt(row.Get(3)),
                ValueToFloat(row.Get(4)),
                (RoSignBoardItemType) ValueToInt(row.Get(5)),
                assetPath,
                ValueToString(row.Get(7)),
                color,
                sourcePath);
            return true;
        }

        private static bool TryLoadSignBoardFile(
            RoAssetDatabase assets,
            out RoWorldMapLuaFile file,
            out string diagnostic
        ) {
            for (int i = 0; i < CandidatePaths.Length; i++) {
                string candidate = CandidatePaths[i];
                if (!assets.TryResolvePath(candidate, out string resolvedPath)) {
                    continue;
                }

                byte[] bytes = assets.ReadBytes(candidate);
                if (bytes == null || bytes.Length == 0) {
                    continue;
                }

                file = new RoWorldMapLuaFile(resolvedPath ?? candidate, bytes);
                diagnostic = null;
                return true;
            }

            for (int i = 0; i < SuffixCandidates.Length; i++) {
                string suffix = SuffixCandidates[i];
                if (!assets.TryFindFirstPathEndingWith(suffix, out string resolvedPath)) {
                    continue;
                }

                byte[] bytes = assets.ReadBytes(resolvedPath);
                if (bytes == null || bytes.Length == 0) {
                    continue;
                }

                file = new RoWorldMapLuaFile(resolvedPath, bytes);
                diagnostic = null;
                return true;
            }

            file = null;
            diagnostic = "No SignBoardList Lua/LUB file was found in the active RO content profile.";
            return false;
        }

        private void BuildMapIndex(IReadOnlyList<RoSignBoardEntry> entries) {
            var buckets = new Dictionary<string, List<RoSignBoardEntry>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++) {
                RoSignBoardEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.MapId)) {
                    continue;
                }

                if (!buckets.TryGetValue(entry.MapId, out List<RoSignBoardEntry> bucket)) {
                    bucket = new List<RoSignBoardEntry>();
                    buckets[entry.MapId] = bucket;
                }

                bucket.Add(entry);
            }

            foreach (KeyValuePair<string, List<RoSignBoardEntry>> pair in buckets) {
                entriesByMap[pair.Key] = pair.Value.ToArray();
            }
        }

        private static string NormalizeAssetReference(string rawPath) {
            if (string.IsNullOrWhiteSpace(rawPath)) {
                return string.Empty;
            }

            string path = rawPath.Trim().Replace('\\', '/').TrimStart('/');
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(path);
            if (!string.IsNullOrEmpty(decoded)) {
                path = decoded.Trim().Replace('\\', '/').TrimStart('/');
            }

            while (path.Contains("//")) {
                path = path.Replace("//", "/");
            }

            return path;
        }

        private static string ValueToString(LuaValue value) {
            return value == null ? string.Empty : value.ToDisplayString();
        }

        private static int ValueToInt(LuaValue value) {
            return value == null ? 0 : value.ToInt();
        }

        private static float ValueToFloat(LuaValue value) {
            return value == null ? 0f : value.ToFloat();
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

        private sealed class CacheEntry {
            public CacheEntry(RoSignBoardMetadataTable table) {
                Table = table ?? Empty;
            }

            public RoSignBoardMetadataTable Table { get; }
        }
    }
}
