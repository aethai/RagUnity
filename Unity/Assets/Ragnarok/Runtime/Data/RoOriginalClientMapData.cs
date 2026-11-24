using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ragnarok.Assets;
using UnityEngine;

namespace Ragnarok.Data {
    public sealed class RoMapSkyDataCatalog {
        public static readonly RoMapSkyDataCatalog Empty = new RoMapSkyDataCatalog(
            Array.Empty<RoMapSkyDataEntry>(),
            Array.Empty<string>());

        private readonly Dictionary<string, RoMapSkyDataEntry> byMap =
            new Dictionary<string, RoMapSkyDataEntry>(StringComparer.OrdinalIgnoreCase);

        public RoMapSkyDataCatalog(IReadOnlyList<RoMapSkyDataEntry> entries, IReadOnlyList<string> diagnostics) {
            Entries = entries ?? Array.Empty<RoMapSkyDataEntry>();
            Diagnostics = diagnostics ?? Array.Empty<string>();
            for (int i = 0; i < Entries.Count; i++) {
                RoMapSkyDataEntry entry = Entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.MapId)) {
                    byMap[entry.MapId] = entry;
                }
            }
        }

        public IReadOnlyList<RoMapSkyDataEntry> Entries { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public bool TryGet(string mapName, out RoMapSkyDataEntry entry) {
            string normalized = NormalizeMapWithExtension(mapName);
            return byMap.TryGetValue(normalized, out entry);
        }

        internal static string NormalizeMapWithExtension(string value) {
            string id = RoWorldMapDataUtility.NormalizeMapId(value);
            return string.IsNullOrEmpty(id) ? string.Empty : id + ".rsw";
        }
    }

    public sealed class RoMapSkyDataEntry {
        public RoMapSkyDataEntry(string mapId, Color skyColor, Color cloudColor, string sourcePath) {
            MapId = RoMapSkyDataCatalog.NormalizeMapWithExtension(mapId);
            SkyColor = skyColor;
            CloudColor = cloudColor;
            SourcePath = sourcePath ?? string.Empty;
        }

        public string MapId { get; }
        public Color SkyColor { get; }
        public Color CloudColor { get; }
        public string SourcePath { get; }
    }

    public static class RoMapSkyDataLoader {
        private static readonly ConditionalWeakTable<RoAssetDatabase, RoMapSkyDataCatalog> Cache =
            new ConditionalWeakTable<RoAssetDatabase, RoMapSkyDataCatalog>();

        private static readonly string[] CandidatePaths = {
            "data/luafiles514/lua files/mapskydata/mapskydata.lub",
            "data/luafiles514/lua files/mapskydata/mapskydata.lua",
            "data/LuaFiles514/Lua Files/mapskydata/mapskydata.lub",
            "data/LuaFiles514/Lua Files/mapskydata/mapskydata.lua",
            "data/lua files/mapskydata/mapskydata.lub",
            "data/lua files/mapskydata/mapskydata.lua",
            "mapskydata/mapskydata.lub",
            "mapskydata/mapskydata.lua"
        };

        public static RoMapSkyDataCatalog Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return RoMapSkyDataCatalog.Empty;
            }

            return Cache.GetValue(assets, LoadUncached);
        }

        public static RoMapSkyDataCatalog ParseText(string path, string text) {
            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseTextContext(path, text);
            return BuildCatalog(context, path ?? "mapskydata.lua");
        }

        private static RoMapSkyDataCatalog LoadUncached(RoAssetDatabase assets) {
            var files = new List<RoWorldMapLuaFile>();
            var diagnostics = new List<string>();
            for (int i = 0; i < CandidatePaths.Length; i++) {
                if (!assets.TryResolvePath(CandidatePaths[i], out string resolvedPath)) {
                    continue;
                }

                byte[] bytes = assets.ReadBytes(CandidatePaths[i]);
                if (bytes == null || bytes.Length == 0) {
                    continue;
                }

                files.Add(new RoWorldMapLuaFile(resolvedPath ?? CandidatePaths[i], bytes));
                break;
            }

            if (files.Count == 0) {
                string[] suffixes = {
                    "mapskydata/mapskydata.lub",
                    "mapskydata/mapskydata.lua"
                };
                for (int i = 0; i < suffixes.Length; i++) {
                    if (!assets.TryFindFirstPathEndingWith(suffixes[i], out string suffixPath)) {
                        continue;
                    }

                    byte[] bytes = assets.ReadBytes(suffixPath);
                    if (bytes != null && bytes.Length > 0) {
                        files.Add(new RoWorldMapLuaFile(suffixPath, bytes));
                        break;
                    }
                }
            }

            if (files.Count == 0) {
                diagnostics.Add("Missing original client mapskydata.lub/.lua.");
                return new RoMapSkyDataCatalog(Array.Empty<RoMapSkyDataEntry>(), diagnostics);
            }

            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(files);
            diagnostics.AddRange(context.Diagnostics);
            RoMapSkyDataCatalog catalog = BuildCatalog(context, files[0].Path);
            if (diagnostics.Count == 0) {
                return catalog;
            }

            var mergedDiagnostics = new List<string>(catalog.Diagnostics);
            mergedDiagnostics.AddRange(diagnostics);
            return new RoMapSkyDataCatalog(catalog.Entries, mergedDiagnostics);
        }

        private static RoMapSkyDataCatalog BuildCatalog(RoWorldMapLuaExecutionContext context, string sourcePath) {
            var entries = new List<RoMapSkyDataEntry>();
            var diagnostics = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, LuaValue> global in context.Globals) {
                CollectSkyEntries(global.Value, global.Key, sourcePath, entries, seen, 0);
            }

            if (entries.Count == 0) {
                diagnostics.Add("mapskydata parsed, but no map sky entries were recognized.");
            }

            return new RoMapSkyDataCatalog(entries, diagnostics);
        }

        private static void CollectSkyEntries(
            LuaValue value,
            string keyHint,
            string sourcePath,
            List<RoMapSkyDataEntry> entries,
            HashSet<string> seen,
            int depth
        ) {
            if (value == null || value.Kind != LuaValueKind.Table || depth > 4) {
                return;
            }

            LuaTableValue table = value.Table;
            string mapId = TryResolveMapId(keyHint, table);
            if (!string.IsNullOrEmpty(mapId) &&
                TryReadColors(table, out Color sky, out Color cloud)) {
                string normalized = RoMapSkyDataCatalog.NormalizeMapWithExtension(mapId);
                if (seen.Add(normalized)) {
                    entries.Add(new RoMapSkyDataEntry(normalized, sky, cloud, sourcePath));
                }
            }

            int index = 0;
            foreach (LuaValue child in table.ArrayValues) {
                index++;
                CollectSkyEntries(child, index.ToString(System.Globalization.CultureInfo.InvariantCulture), sourcePath, entries, seen, depth + 1);
            }

            foreach (KeyValuePair<string, LuaValue> field in table.Fields) {
                CollectSkyEntries(field.Value, field.Key, sourcePath, entries, seen, depth + 1);
            }
        }

        private static string TryResolveMapId(string keyHint, LuaTableValue table) {
            if (LooksLikeMapName(keyHint)) {
                return keyHint;
            }

            string[] fieldNames = { "map", "mapfile", "mapname", "rsw", "filename" };
            for (int i = 0; i < fieldNames.Length; i++) {
                string value = table.Get(fieldNames[i]).ToDisplayString();
                if (LooksLikeMapName(value)) {
                    return value;
                }
            }

            for (int i = 0; i < table.ArrayValues.Count; i++) {
                string value = table.ArrayValues[i].ToDisplayString();
                if (LooksLikeMapName(value)) {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool TryReadColors(LuaTableValue table, out Color sky, out Color cloud) {
            if (TryReadNamedColor(table, "skyColor", out sky) ||
                TryReadNamedColor(table, "skycolor", out sky) ||
                TryReadNamedColor(table, "sky", out sky)) {
                if (TryReadNamedColor(table, "cloudColor", out cloud) ||
                    TryReadNamedColor(table, "cloudcolor", out cloud) ||
                    TryReadNamedColor(table, "cloud", out cloud)) {
                    return true;
                }
            }

            var numbers = new List<float>();
            CollectTopLevelNumbers(table, numbers);
            if (numbers.Count >= 7) {
                sky = BuildColor(numbers[0], numbers[1], numbers[2], numbers[3]);
                cloud = BuildColor(numbers[4], numbers[5], numbers[6], 1f);
                return true;
            }

            if (numbers.Count >= 6) {
                sky = BuildColor(numbers[0], numbers[1], numbers[2], 1f);
                cloud = BuildColor(numbers[3], numbers[4], numbers[5], 1f);
                return true;
            }

            sky = default;
            cloud = default;
            return false;
        }

        private static bool TryReadNamedColor(LuaTableValue table, string field, out Color color) {
            LuaValue value = table.Get(field);
            if (value.Kind != LuaValueKind.Table) {
                color = default;
                return false;
            }

            var numbers = new List<float>();
            CollectTopLevelNumbers(value.Table, numbers);
            if (numbers.Count < 3) {
                color = default;
                return false;
            }

            color = BuildColor(numbers[0], numbers[1], numbers[2], numbers.Count >= 4 ? numbers[3] : 1f);
            return true;
        }

        private static void CollectTopLevelNumbers(LuaTableValue table, List<float> numbers) {
            foreach (LuaValue value in table.ArrayValues) {
                if (value.Kind == LuaValueKind.Number) {
                    numbers.Add((float) value.Number);
                }
            }

            foreach (KeyValuePair<string, LuaValue> field in table.Fields) {
                if (field.Value.Kind == LuaValueKind.Number) {
                    numbers.Add((float) field.Value.Number);
                }
            }
        }

        private static Color BuildColor(float r, float g, float b, float a) {
            float max = Mathf.Max(Mathf.Abs(r), Mathf.Abs(g), Mathf.Abs(b), Mathf.Abs(a));
            if (max > 1.5f) {
                r /= 255f;
                g /= 255f;
                b /= 255f;
                a /= 255f;
            }

            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));
        }

        private static bool LooksLikeMapName(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            string normalized = value.Trim().Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < normalized.Length) {
                normalized = normalized.Substring(slash + 1);
            }

            return normalized.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith(".gat", StringComparison.OrdinalIgnoreCase) ||
                   (normalized.IndexOf(' ') < 0 &&
                    normalized.IndexOf('.') < 0 &&
                    normalized.Length >= 3 &&
                    normalized.Length <= 32 &&
                    normalized.IndexOf('_') >= 0);
        }
    }

    public sealed class RoKafraMoveServiceCatalog {
        public static readonly RoKafraMoveServiceCatalog Empty = new RoKafraMoveServiceCatalog(
            Array.Empty<RoKafraMoveServiceEntry>(),
            Array.Empty<string>());

        private readonly Dictionary<string, List<RoKafraMoveServiceEntry>> bySource =
            new Dictionary<string, List<RoKafraMoveServiceEntry>>(StringComparer.OrdinalIgnoreCase);

        public RoKafraMoveServiceCatalog(IReadOnlyList<RoKafraMoveServiceEntry> entries, IReadOnlyList<string> diagnostics) {
            Entries = entries ?? Array.Empty<RoKafraMoveServiceEntry>();
            Diagnostics = diagnostics ?? Array.Empty<string>();
            for (int i = 0; i < Entries.Count; i++) {
                RoKafraMoveServiceEntry entry = Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.SourceMapId)) {
                    continue;
                }

                if (!bySource.TryGetValue(entry.SourceMapId, out List<RoKafraMoveServiceEntry> list)) {
                    list = new List<RoKafraMoveServiceEntry>();
                    bySource.Add(entry.SourceMapId, list);
                }

                list.Add(entry);
            }
        }

        public IReadOnlyList<RoKafraMoveServiceEntry> Entries { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public IReadOnlyList<RoKafraMoveServiceEntry> GetDestinations(string sourceMapId) {
            string normalized = RoWorldMapDataUtility.NormalizeMapId(sourceMapId);
            if (bySource.TryGetValue(normalized, out List<RoKafraMoveServiceEntry> list)) {
                return list;
            }

            return Array.Empty<RoKafraMoveServiceEntry>();
        }
    }

    public sealed class RoKafraMoveServiceEntry {
        public RoKafraMoveServiceEntry(string sourceMapId, string destinationMapId, string displayName, int cost, string sourcePath) {
            SourceMapId = RoWorldMapDataUtility.NormalizeMapId(sourceMapId);
            DestinationMapId = RoWorldMapDataUtility.NormalizeMapId(destinationMapId);
            DisplayName = displayName ?? string.Empty;
            Cost = cost;
            SourcePath = sourcePath ?? string.Empty;
        }

        public string SourceMapId { get; }
        public string DestinationMapId { get; }
        public string DisplayName { get; }
        public int Cost { get; }
        public string SourcePath { get; }
        public bool IsFlightService => SourcePath.IndexOf("_f", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static class RoKafraMoveServiceDataLoader {
        private static readonly ConditionalWeakTable<RoAssetDatabase, RoKafraMoveServiceCatalog> Cache =
            new ConditionalWeakTable<RoAssetDatabase, RoKafraMoveServiceCatalog>();

        private static readonly string[] CandidatePaths = {
            "data/luafiles514/lua files/datainfo/kaframovemapservicelist.lub",
            "data/luafiles514/lua files/datainfo/kaframovemapservicelist.lua",
            "data/luafiles514/lua files/datainfo/kaframovemapservicelist_f.lub",
            "data/luafiles514/lua files/datainfo/kaframovemapservicelist_f.lua",
            "data/LuaFiles514/Lua Files/datainfo/kaframovemapservicelist.lub",
            "data/LuaFiles514/Lua Files/datainfo/kaframovemapservicelist_f.lub",
            "data/lua files/datainfo/kaframovemapservicelist.lub",
            "data/lua files/datainfo/kaframovemapservicelist_f.lub"
        };

        public static RoKafraMoveServiceCatalog Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return RoKafraMoveServiceCatalog.Empty;
            }

            return Cache.GetValue(assets, LoadUncached);
        }

        public static RoKafraMoveServiceCatalog ParseText(string path, string text) {
            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseTextContext(path, text);
            return BuildCatalog(context, path ?? "kaframovemapservicelist.lua");
        }

        private static RoKafraMoveServiceCatalog LoadUncached(RoAssetDatabase assets) {
            var files = new List<RoWorldMapLuaFile>();
            var diagnostics = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < CandidatePaths.Length; i++) {
                if (!assets.TryResolvePath(CandidatePaths[i], out string resolvedPath)) {
                    continue;
                }

                byte[] bytes = assets.ReadBytes(CandidatePaths[i]);
                if (bytes == null || bytes.Length == 0) {
                    continue;
                }

                string path = resolvedPath ?? CandidatePaths[i];
                if (seenPaths.Add(path)) {
                    files.Add(new RoWorldMapLuaFile(path, bytes));
                }
            }

            string[] suffixes = {
                "datainfo/kaframovemapservicelist.lub",
                "datainfo/kaframovemapservicelist.lua",
                "datainfo/kaframovemapservicelist_f.lub",
                "datainfo/kaframovemapservicelist_f.lua"
            };
            for (int i = 0; i < suffixes.Length; i++) {
                if (!assets.TryFindFirstPathEndingWith(suffixes[i], out string suffixPath) ||
                    !seenPaths.Add(suffixPath)) {
                    continue;
                }

                byte[] bytes = assets.ReadBytes(suffixPath);
                if (bytes != null && bytes.Length > 0) {
                    files.Add(new RoWorldMapLuaFile(suffixPath, bytes));
                }
            }

            if (files.Count == 0) {
                diagnostics.Add("Missing original client kaframovemapservicelist.lub/.lua.");
                return new RoKafraMoveServiceCatalog(Array.Empty<RoKafraMoveServiceEntry>(), diagnostics);
            }

            var entries = new List<RoKafraMoveServiceEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Count; i++) {
                RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(new[] { files[i] });
                diagnostics.AddRange(context.Diagnostics);
                RoKafraMoveServiceCatalog partial = BuildCatalog(context, files[i].Path);
                diagnostics.AddRange(partial.Diagnostics);
                for (int e = 0; e < partial.Entries.Count; e++) {
                    RoKafraMoveServiceEntry entry = partial.Entries[e];
                    string key = entry.SourceMapId + ">" + entry.DestinationMapId + ">" + entry.Cost +
                        (entry.IsFlightService ? ">flight" : ">ground");
                    if (seen.Add(key)) {
                        entries.Add(entry);
                    }
                }
            }

            return new RoKafraMoveServiceCatalog(entries, diagnostics);
        }

        private static RoKafraMoveServiceCatalog BuildCatalog(RoWorldMapLuaExecutionContext context, string sourcePath) {
            var entries = new List<RoKafraMoveServiceEntry>();
            var diagnostics = new List<string>();
            foreach (KeyValuePair<string, LuaValue> global in context.Globals) {
                CollectEntries(global.Value, global.Key, sourcePath, entries, 0);
            }

            if (entries.Count == 0) {
                diagnostics.Add("kaframovemapservicelist parsed, but no Kafra move entries were recognized.");
            }

            return new RoKafraMoveServiceCatalog(entries, diagnostics);
        }

        private static void CollectEntries(
            LuaValue value,
            string sourceHint,
            string sourcePath,
            List<RoKafraMoveServiceEntry> entries,
            int depth
        ) {
            if (value == null || value.Kind != LuaValueKind.Table || depth > 5) {
                return;
            }

            LuaTableValue table = value.Table;
            if (TryBuildFlatSourceEntries(table, sourceHint, sourcePath, entries)) {
                return;
            }

            if (TryBuildEntry(table, sourceHint, sourcePath, out RoKafraMoveServiceEntry entry)) {
                entries.Add(entry);
            }

            int index = 0;
            foreach (LuaValue child in table.ArrayValues) {
                index++;
                CollectEntries(child, sourceHint, sourcePath, entries, depth + 1);
            }

            foreach (KeyValuePair<string, LuaValue> field in table.Fields) {
                CollectEntries(field.Value, field.Key, sourcePath, entries, depth + 1);
            }
        }

        private static bool TryBuildFlatSourceEntries(
            LuaTableValue table,
            string sourceHint,
            string sourcePath,
            List<RoKafraMoveServiceEntry> entries
        ) {
            if (table == null || !LooksLikePlainMapId(sourceHint) || table.ArrayValues.Count == 0) {
                return false;
            }

            bool hasNestedTable = false;
            for (int i = 0; i < table.ArrayValues.Count; i++) {
                if (table.ArrayValues[i] != null && table.ArrayValues[i].Kind == LuaValueKind.Table) {
                    hasNestedTable = true;
                    break;
                }
            }

            if (hasNestedTable) {
                return false;
            }

            var parsed = new List<RoKafraMoveServiceEntry>();
            int index = 0;
            while (index < table.ArrayValues.Count) {
                LuaValue destinationValue = table.ArrayValues[index++];
                if (destinationValue == null || destinationValue.Kind != LuaValueKind.String ||
                    !LooksLikePlainMapId(destinationValue.Text)) {
                    continue;
                }

                string destination = destinationValue.Text;
                string displayName = string.Empty;
                int cost = 0;

                if (index < table.ArrayValues.Count) {
                    LuaValue maybeName = table.ArrayValues[index];
                    bool followedByCost = index + 1 < table.ArrayValues.Count &&
                                          table.ArrayValues[index + 1] != null &&
                                          table.ArrayValues[index + 1].Kind == LuaValueKind.Number;
                    if (maybeName != null && maybeName.Kind == LuaValueKind.String &&
                        (followedByCost || !LooksLikePlainMapId(maybeName.Text))) {
                        displayName = maybeName.Text;
                        index++;
                    }
                }

                if (index < table.ArrayValues.Count) {
                    LuaValue maybeCost = table.ArrayValues[index];
                    if (maybeCost != null && maybeCost.Kind == LuaValueKind.Number) {
                        cost = maybeCost.ToInt();
                        index++;
                    }
                }

                if (!string.Equals(
                        RoWorldMapDataUtility.NormalizeMapId(destination),
                        RoWorldMapDataUtility.NormalizeMapId(sourceHint),
                        StringComparison.OrdinalIgnoreCase)) {
                    parsed.Add(new RoKafraMoveServiceEntry(sourceHint, destination, displayName, cost, sourcePath));
                }
            }

            if (parsed.Count == 0) {
                return false;
            }

            entries.AddRange(parsed);
            return true;
        }

        private static bool TryBuildEntry(LuaTableValue table, string sourceHint, string sourcePath, out RoKafraMoveServiceEntry entry) {
            var strings = new List<string>();
            var numbers = new List<int>();
            CollectTopLevelValues(table, strings, numbers);

            string source = LooksLikePlainMapId(sourceHint) ? sourceHint : string.Empty;
            string destination = string.Empty;
            string displayName = string.Empty;

            for (int i = 0; i < strings.Count; i++) {
                string value = strings[i];
                if (LooksLikePlainMapId(value)) {
                    if (string.IsNullOrEmpty(source)) {
                        source = value;
                    } else if (string.IsNullOrEmpty(destination) &&
                               !string.Equals(RoWorldMapDataUtility.NormalizeMapId(value), RoWorldMapDataUtility.NormalizeMapId(source), StringComparison.OrdinalIgnoreCase)) {
                        destination = value;
                    }
                } else if (string.IsNullOrEmpty(displayName)) {
                    displayName = value;
                }
            }

            if (string.IsNullOrEmpty(destination)) {
                string fieldDestination = table.Get("map").ToDisplayString();
                if (LooksLikePlainMapId(fieldDestination)) {
                    destination = fieldDestination;
                }
            }

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination)) {
                entry = null;
                return false;
            }

            int cost = numbers.Count > 0 ? numbers[numbers.Count - 1] : 0;
            entry = new RoKafraMoveServiceEntry(source, destination, displayName, cost, sourcePath);
            return true;
        }

        private static void CollectTopLevelValues(LuaTableValue table, List<string> strings, List<int> numbers) {
            foreach (LuaValue value in table.ArrayValues) {
                if (value.Kind == LuaValueKind.String) {
                    strings.Add(value.Text);
                } else if (value.Kind == LuaValueKind.Number) {
                    numbers.Add(value.ToInt());
                }
            }

            foreach (KeyValuePair<string, LuaValue> field in table.Fields) {
                if (field.Value.Kind == LuaValueKind.String) {
                    strings.Add(field.Value.Text);
                } else if (field.Value.Kind == LuaValueKind.Number) {
                    numbers.Add(field.Value.ToInt());
                }
            }
        }

        private static bool LooksLikePlainMapId(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            for (int i = 0; i < value.Length; i++) {
                if (char.IsUpper(value[i])) {
                    return false;
                }
            }

            string normalized = RoWorldMapDataUtility.NormalizeMapId(value);
            if (normalized.Length < 3 || normalized.Length > 32 || normalized.IndexOf(' ') >= 0) {
                return false;
            }

            for (int i = 0; i < normalized.Length; i++) {
                char c = normalized[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '@')) {
                    return false;
                }
            }

            return true;
        }
    }
}
