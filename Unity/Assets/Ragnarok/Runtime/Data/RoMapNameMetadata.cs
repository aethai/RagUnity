using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public sealed class RoMapNameMetadataEntry {
        public RoMapNameMetadataEntry(
            string mapName,
            string displayName,
            bool notifyEnter,
            string subTitle,
            string mainTitle,
            string backgroundBmp,
            string sourcePath
        ) {
            MapId = RoWorldMapDataUtility.NormalizeMapId(mapName);
            DisplayName = NormalizeText(displayName);
            NotifyEnter = notifyEnter;
            SubTitle = NormalizeText(subTitle);
            MainTitle = NormalizeText(mainTitle);
            BackgroundBmp = NormalizeBackgroundName(backgroundBmp);
            BackgroundTexturePath = RoMapNameMetadataTable.BuildBackgroundTexturePath(BackgroundBmp);
            SourcePath = sourcePath ?? string.Empty;
        }

        public string MapId { get; }
        public string DisplayName { get; }
        public bool NotifyEnter { get; }
        public string SubTitle { get; }
        public string MainTitle { get; }
        public string BackgroundBmp { get; }
        public string BackgroundTexturePath { get; }
        public string SourcePath { get; }
        public bool HasSignName => !string.IsNullOrEmpty(SubTitle) || !string.IsNullOrEmpty(MainTitle);

        internal static string NormalizeText(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            string withoutNulls = value.Replace("\0", string.Empty);
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(withoutNulls);
            return string.IsNullOrEmpty(decoded) ? withoutNulls : decoded;
        }

        private static string NormalizeBackgroundName(string value) {
            string normalized = NormalizeText(value).Trim().Replace('\\', '/').TrimStart('/');
            int slash = normalized.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < normalized.Length) {
                normalized = normalized.Substring(slash + 1);
            }

            return normalized;
        }
    }

    public sealed class RoMapNameMetadataTable {
        private static readonly ConditionalWeakTable<RoAssetDatabase, CacheEntry> Cache =
            new ConditionalWeakTable<RoAssetDatabase, CacheEntry>();

        private static readonly string[] CandidatePaths = {
            // the client reads System\mapInfo_true.lub (CMapInfoMgr 0x6567b0)
            "System/mapInfo_true.lub",
            "system/mapInfo_true.lub",
            "data/System/mapInfo.lub",
            "data/System/mapInfo.lua",
            "data/system/mapInfo.lub",
            "data/system/mapInfo.lua",
            "data/system/mapinfo.lub",
            "data/system/mapinfo.lua",
            "System/mapInfo.lub",
            "System/mapInfo.lua",
            "system/mapInfo.lub",
            "system/mapInfo.lua"
        };

        private static readonly string[] SuffixCandidates = {
            "System/mapInfo.lub",
            "System/mapInfo.lua",
            "system/mapInfo.lub",
            "system/mapInfo.lua",
            "system/mapinfo.lub",
            "system/mapinfo.lua",
            "mapInfo.lub",
            "mapInfo.lua",
            "mapinfo.lub",
            "mapinfo.lua"
        };

        public static readonly RoMapNameMetadataTable Empty = new RoMapNameMetadataTable(
            Array.Empty<RoMapNameMetadataEntry>(),
            string.Empty,
            Array.Empty<string>());

        private readonly Dictionary<string, RoMapNameMetadataEntry> entriesByMap =
            new Dictionary<string, RoMapNameMetadataEntry>(StringComparer.OrdinalIgnoreCase);

        public RoMapNameMetadataTable(
            IReadOnlyList<RoMapNameMetadataEntry> entries,
            string sourcePath,
            IReadOnlyList<string> diagnostics
        ) {
            Entries = entries ?? Array.Empty<RoMapNameMetadataEntry>();
            SourcePath = sourcePath ?? string.Empty;
            Diagnostics = diagnostics ?? Array.Empty<string>();
            for (int i = 0; i < Entries.Count; i++) {
                RoMapNameMetadataEntry entry = Entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.MapId)) {
                    entriesByMap[entry.MapId] = entry;
                }
            }
        }

        public IReadOnlyList<RoMapNameMetadataEntry> Entries { get; }
        public string SourcePath { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsLoaded => Entries.Count > 0;

        public static RoMapNameMetadataTable Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return new RoMapNameMetadataTable(
                    Array.Empty<RoMapNameMetadataEntry>(),
                    string.Empty,
                    new[] { "RO asset database is not initialized; System/mapInfo cannot be loaded." });
            }

            return Cache.GetValue(assets, key => new CacheEntry(LoadUncached(key))).Table;
        }

        public static RoMapNameMetadataTable FromText(string text, string sourcePath = "mapInfo.lua") {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            var file = new RoWorldMapLuaFile(sourcePath ?? "mapInfo.lua", bytes);
            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(new[] { file });
            return BuildFromContext(context, file.Path);
        }

        public bool TryGet(string mapName, out RoMapNameMetadataEntry entry) {
            string mapId = RoWorldMapDataUtility.NormalizeMapId(mapName);
            return entriesByMap.TryGetValue(mapId, out entry);
        }

        public static string BuildBackgroundTexturePath(string backgroundBmp) {
            if (string.IsNullOrWhiteSpace(backgroundBmp)) {
                return string.Empty;
            }

            string relative = backgroundBmp.Trim().Replace('\\', '/').TrimStart('/');
            if (!relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                !relative.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) {
                relative += ".png";
            }

            return RoSignBoardMetadataTable.BuildInterfaceTexturePath("display_mapname/" + relative);
        }

        private static RoMapNameMetadataTable LoadUncached(RoAssetDatabase assets) {
            if (TryLoadFirstUsableMapInfoFile(assets, out RoMapNameMetadataTable table, out string diagnostic)) {
                return table;
            }

            return new RoMapNameMetadataTable(
                Array.Empty<RoMapNameMetadataEntry>(),
                string.Empty,
                string.IsNullOrEmpty(diagnostic) ? Array.Empty<string>() : new[] { diagnostic });
        }

        private static RoMapNameMetadataTable BuildFromContext(RoWorldMapLuaExecutionContext context, string sourcePath) {
            var diagnostics = new List<string>();
            if (context != null) {
                diagnostics.AddRange(context.Diagnostics);
            }

            var builders = new Dictionary<string, EntryBuilder>(StringComparer.OrdinalIgnoreCase);
            AddMapTableEntries(context, builders);

            IReadOnlyList<IReadOnlyList<LuaValue>> displayCalls = context != null
                ? context.GetFunctionCalls("AddMapDisplayName")
                : Array.Empty<IReadOnlyList<LuaValue>>();
            for (int i = 0; i < displayCalls.Count; i++) {
                IReadOnlyList<LuaValue> args = displayCalls[i];
                string mapName = ValueToString(args, 0);
                if (string.IsNullOrWhiteSpace(mapName)) {
                    continue;
                }

                EntryBuilder builder = GetBuilder(builders, mapName);
                builder.DisplayName = ValueToString(args, 1);
                builder.NotifyEnter = ValueToBool(args, 2);
            }

            IReadOnlyList<IReadOnlyList<LuaValue>> signCalls = context != null
                ? context.GetFunctionCalls("AddMapSignName")
                : Array.Empty<IReadOnlyList<LuaValue>>();
            for (int i = 0; i < signCalls.Count; i++) {
                IReadOnlyList<LuaValue> args = signCalls[i];
                string mapName = ValueToString(args, 0);
                if (string.IsNullOrWhiteSpace(mapName)) {
                    continue;
                }

                EntryBuilder builder = GetBuilder(builders, mapName);
                builder.SubTitle = ValueToString(args, 1);
                builder.MainTitle = ValueToString(args, 2);
            }

            IReadOnlyList<IReadOnlyList<LuaValue>> backgroundCalls = context != null
                ? context.GetFunctionCalls("AddMapBackgroundBmp")
                : Array.Empty<IReadOnlyList<LuaValue>>();
            for (int i = 0; i < backgroundCalls.Count; i++) {
                IReadOnlyList<LuaValue> args = backgroundCalls[i];
                string mapName = ValueToString(args, 0);
                if (string.IsNullOrWhiteSpace(mapName)) {
                    continue;
                }

                EntryBuilder builder = GetBuilder(builders, mapName);
                string background = ValueToString(args, 1);
                builder.BackgroundBmp = string.IsNullOrWhiteSpace(background) ? "field" : background;
            }

            var entries = new List<RoMapNameMetadataEntry>(builders.Count);
            foreach (EntryBuilder builder in builders.Values) {
                if (string.IsNullOrWhiteSpace(builder.MapName)) {
                    continue;
                }

                entries.Add(builder.Build(sourcePath));
            }

            if (entries.Count == 0) {
                diagnostics.Add("System/mapInfo parsed, but no mapTbl entries or AddMapDisplayName entries were recognized.");
            }

            return new RoMapNameMetadataTable(entries, sourcePath, diagnostics);
        }

        private static void AddMapTableEntries(
            RoWorldMapLuaExecutionContext context,
            Dictionary<string, EntryBuilder> builders
        ) {
            if (context == null || builders == null) {
                return;
            }

            LuaValue tableValue = context.GetGlobal("mapTbl");
            if (tableValue.Kind != LuaValueKind.Table || tableValue.Table == null) {
                return;
            }

            foreach (KeyValuePair<string, LuaValue> pair in tableValue.Table.Fields) {
                if (string.IsNullOrWhiteSpace(pair.Key) ||
                    pair.Value == null ||
                    pair.Value.Kind != LuaValueKind.Table ||
                    pair.Value.Table == null) {
                    continue;
                }

                LuaTableValue mapInfo = pair.Value.Table;
                EntryBuilder builder = GetBuilder(builders, pair.Key);
                string displayName = mapInfo.Get("displayName").ToDisplayString();
                if (!string.IsNullOrWhiteSpace(displayName)) {
                    builder.DisplayName = displayName;
                }

                LuaValue notifyEnter = mapInfo.Get("notifyEnter");
                if (notifyEnter.Kind != LuaValueKind.Nil) {
                    builder.NotifyEnter = ValueToBool(notifyEnter);
                }

                LuaValue signName = mapInfo.Get("signName");
                if (signName.Kind == LuaValueKind.Table && signName.Table != null) {
                    string subTitle = signName.Table.Get("subTitle").ToDisplayString();
                    string mainTitle = signName.Table.Get("mainTitle").ToDisplayString();
                    if (!string.IsNullOrWhiteSpace(subTitle)) {
                        builder.SubTitle = subTitle;
                    }

                    if (!string.IsNullOrWhiteSpace(mainTitle)) {
                        builder.MainTitle = mainTitle;
                    }
                }

                string background = mapInfo.Get("backgroundBmp").ToDisplayString();
                if (!string.IsNullOrWhiteSpace(background)) {
                    builder.BackgroundBmp = background;
                }
            }
        }

        private static EntryBuilder GetBuilder(Dictionary<string, EntryBuilder> builders, string mapName) {
            string key = RoWorldMapDataUtility.NormalizeMapId(mapName);
            if (!builders.TryGetValue(key, out EntryBuilder builder)) {
                builder = new EntryBuilder(mapName);
                builders[key] = builder;
            }

            return builder;
        }

        private static bool TryLoadFirstUsableMapInfoFile(
            RoAssetDatabase assets,
            out RoMapNameMetadataTable table,
            out string diagnostic
        ) {
            var diagnostics = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool foundAnyFile = false;

            for (int i = 0; i < CandidatePaths.Length; i++) {
                string candidate = CandidatePaths[i];
                if (!assets.TryResolvePath(candidate, out string resolvedPath)) {
                    continue;
                }

                foundAnyFile = true;
                if (TryLoadMapInfoTable(assets, resolvedPath ?? candidate, seenPaths, diagnostics, out table)) {
                    diagnostic = null;
                    return true;
                }
            }

            for (int i = 0; i < SuffixCandidates.Length; i++) {
                string suffix = SuffixCandidates[i];
                if (!assets.TryFindFirstPathEndingWith(suffix, out string resolvedPath)) {
                    continue;
                }

                foundAnyFile = true;
                if (TryLoadMapInfoTable(assets, resolvedPath, seenPaths, diagnostics, out table)) {
                    diagnostic = null;
                    return true;
                }
            }

            table = Empty;
            if (!foundAnyFile) {
                diagnostic = "No System/mapInfo Lua/LUB file was found in the active RO content profile.";
            } else if (diagnostics.Count > 0) {
                diagnostic = string.Join("; ", diagnostics);
            } else {
                diagnostic = "System/mapInfo files were found, but none produced map-name entries.";
            }

            return false;
        }

        private static bool TryLoadMapInfoTable(
            RoAssetDatabase assets,
            string resolvedPath,
            HashSet<string> seenPaths,
            List<string> diagnostics,
            out RoMapNameMetadataTable table
        ) {
            table = Empty;
            if (string.IsNullOrWhiteSpace(resolvedPath) || !seenPaths.Add(resolvedPath)) {
                return false;
            }

            byte[] bytes = assets.ReadBytes(resolvedPath);
            if (bytes == null || bytes.Length == 0) {
                diagnostics?.Add($"System/mapInfo candidate '{resolvedPath}' resolved but had no bytes.");
                return false;
            }

            var file = new RoWorldMapLuaFile(resolvedPath, bytes);
            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(new[] { file });
            table = BuildFromContext(context, file.Path);
            if (table.IsLoaded) {
                return true;
            }

            diagnostics?.Add(
                $"System/mapInfo candidate '{resolvedPath}' produced no map-name entries: " +
                string.Join(", ", table.Diagnostics));
            return false;
        }

        private static string ValueToString(IReadOnlyList<LuaValue> args, int index) {
            if (args == null || index < 0 || index >= args.Count) {
                return string.Empty;
            }

            LuaValue value = args[index];
            return value == null ? string.Empty : value.ToDisplayString();
        }

        private static bool ValueToBool(IReadOnlyList<LuaValue> args, int index) {
            if (args == null || index < 0 || index >= args.Count) {
                return false;
            }

            return ValueToBool(args[index]);
        }

        private static bool ValueToBool(LuaValue value) {
            if (value == null || value.Kind == LuaValueKind.Nil) {
                return false;
            }

            if (value.Kind == LuaValueKind.Boolean) {
                return value.Boolean;
            }

            if (value.Kind == LuaValueKind.Number) {
                return Math.Abs(value.Number) > double.Epsilon;
            }

            string text = value.ToDisplayString();
            return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "1", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class EntryBuilder {
            public EntryBuilder(string mapName) {
                MapName = mapName ?? string.Empty;
            }

            public string MapName { get; }
            public string DisplayName { get; set; }
            public bool NotifyEnter { get; set; }
            public string SubTitle { get; set; }
            public string MainTitle { get; set; }
            public string BackgroundBmp { get; set; }

            public RoMapNameMetadataEntry Build(string sourcePath) {
                return new RoMapNameMetadataEntry(
                    MapName,
                    DisplayName,
                    NotifyEnter,
                    SubTitle,
                    MainTitle,
                    BackgroundBmp,
                    sourcePath);
            }
        }

        private sealed class CacheEntry {
            public CacheEntry(RoMapNameMetadataTable table) {
                Table = table ?? Empty;
            }

            public RoMapNameMetadataTable Table { get; }
        }
    }
}
