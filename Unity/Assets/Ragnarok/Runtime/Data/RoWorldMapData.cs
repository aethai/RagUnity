using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Ragnarok.Assets;
using UnityEngine;

namespace Ragnarok.Data {
    public sealed class RoWorldMapCatalog {
        public static readonly RoWorldMapCatalog Empty = new RoWorldMapCatalog(
            Array.Empty<RoWorldMapDefinition>(),
            Array.Empty<RoWorldMapCategory>(),
            Array.Empty<string>());

        public RoWorldMapCatalog(
            IReadOnlyList<RoWorldMapDefinition> worlds,
            IReadOnlyList<RoWorldMapCategory> categories,
            IReadOnlyList<string> diagnostics
        ) {
            Worlds = worlds ?? Array.Empty<RoWorldMapDefinition>();
            Categories = categories ?? Array.Empty<RoWorldMapCategory>();
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        public IReadOnlyList<RoWorldMapDefinition> Worlds { get; }
        public IReadOnlyList<RoWorldMapCategory> Categories { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool HasWorlds => Worlds.Count > 0;

        public bool TryFindLocation(string mapId, out RoWorldMapDefinition world, out RoWorldMapLocation location) {
            string normalized = RoWorldMapDataUtility.NormalizeMapId(mapId);
            for (int i = 0; i < Worlds.Count; i++) {
                RoWorldMapDefinition candidateWorld = Worlds[i];
                for (int j = 0; j < candidateWorld.Locations.Count; j++) {
                    RoWorldMapLocation candidate = candidateWorld.Locations[j];
                    if (string.Equals(candidate.MapId, normalized, StringComparison.OrdinalIgnoreCase)) {
                        world = candidateWorld;
                        location = candidate;
                        return true;
                    }
                }
            }

            world = null;
            location = null;
            return false;
        }
    }

    public sealed class RoWorldMapDefinition {
        public RoWorldMapDefinition(
            string id,
            string name,
            string backgroundAsset,
            double episodeFrom,
            double episodeTo,
            string tableKey,
            IReadOnlyList<RoWorldMapLocation> locations
        ) {
            Id = string.IsNullOrWhiteSpace(id) ? backgroundAsset ?? string.Empty : id.Trim();
            Name = string.IsNullOrWhiteSpace(name) ? Id : name.Trim();
            BackgroundAsset = string.IsNullOrWhiteSpace(backgroundAsset) ? Id : backgroundAsset.Trim();
            EpisodeFrom = episodeFrom;
            EpisodeTo = episodeTo;
            TableKey = tableKey ?? string.Empty;
            Locations = locations ?? Array.Empty<RoWorldMapLocation>();
        }

        public string Id { get; }
        public string Name { get; }
        public string BackgroundAsset { get; }
        public double EpisodeFrom { get; }
        public double EpisodeTo { get; }
        public string TableKey { get; }
        public IReadOnlyList<RoWorldMapLocation> Locations { get; }
    }

    public sealed class RoWorldMapLocation {
        public RoWorldMapLocation(
            int index,
            string id,
            string displayName,
            float left,
            float top,
            float width,
            float height,
            string mobLevel,
            int type,
            double episodeFrom,
            double episodeTo
        ) {
            Index = index;
            MapId = RoWorldMapDataUtility.NormalizeMapId(id);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? MapId : displayName.Trim();
            Left = left;
            Top = top;
            Width = width;
            Height = height;
            MobLevel = mobLevel ?? string.Empty;
            Type = type;
            EpisodeFrom = episodeFrom;
            EpisodeTo = episodeTo;
        }

        public int Index { get; }
        public string MapId { get; }
        public string DisplayName { get; }
        public float Left { get; }
        public float Top { get; }
        public float Width { get; }
        public float Height { get; }
        public string MobLevel { get; }
        public int Type { get; }
        public double EpisodeFrom { get; }
        public double EpisodeTo { get; }
        public bool IsDungeon => Type == 1;
    }

    public sealed class RoWorldMapCategory {
        public RoWorldMapCategory(string id, string name, string tableKey) {
            Id = id ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(name) ? Id : name.Trim();
            TableKey = tableKey ?? string.Empty;
        }

        public string Id { get; }
        public string Name { get; }
        public string TableKey { get; }
    }

    public static class RoWorldMapDataLoader {
        private static readonly ConditionalWeakTable<RoAssetDatabase, CatalogCacheEntry> CatalogCache = new ConditionalWeakTable<RoAssetDatabase, CatalogCacheEntry>();

        private static readonly string[] LogicalFiles = {
            "worldviewdata_language",
            "worldviewdata_list",
            "worldviewdata_table"
        };

        private static readonly string[] DirectoryCandidates = {
            "data/luafiles514/lua files/worldviewdata",
            "data/LuaFiles514/Lua Files/worldviewdata",
            "data/lua files/worldviewdata",
            "data/Lua Files/worldviewdata",
            "worldviewdata",
            "System/worldviewdata"
        };

        private static readonly string[] ExtensionCandidates = {
            ".lub",
            ".lua"
        };

        public static RoWorldMapCatalog Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return new RoWorldMapCatalog(
                    Array.Empty<RoWorldMapDefinition>(),
                    Array.Empty<RoWorldMapCategory>(),
                    new[] { "RO asset database is not initialized; World Map data cannot be loaded." });
            }

            return CatalogCache.GetValue(assets, key => new CatalogCacheEntry(LoadUncached(key))).Catalog;
        }

        private static RoWorldMapCatalog LoadUncached(RoAssetDatabase assets) {
            var files = new List<RoWorldMapLuaFile>();
            var diagnostics = new List<string>();
            for (int i = 0; i < LogicalFiles.Length; i++) {
                if (TryLoadLogicalFile(assets, LogicalFiles[i], out RoWorldMapLuaFile file, out string diagnostic)) {
                    files.Add(file);
                } else if (!string.IsNullOrEmpty(diagnostic)) {
                    diagnostics.Add(diagnostic);
                }
            }

            if (files.Count == 0) {
                diagnostics.Add("No worldviewdata Lua/LUB files were found in the active RO content profile.");
                return new RoWorldMapCatalog(Array.Empty<RoWorldMapDefinition>(), Array.Empty<RoWorldMapCategory>(), diagnostics);
            }

            RoWorldMapLuaParseResult parsed = RoWorldMapLuaParser.ParseFiles(files);
            diagnostics.AddRange(parsed.Diagnostics);
            return new RoWorldMapCatalog(parsed.Worlds, parsed.Categories, diagnostics);
        }

        private sealed class CatalogCacheEntry {
            public CatalogCacheEntry(RoWorldMapCatalog catalog) {
                Catalog = catalog ?? RoWorldMapCatalog.Empty;
            }

            public RoWorldMapCatalog Catalog { get; }
        }

        private static bool TryLoadLogicalFile(
            RoAssetDatabase assets,
            string logicalName,
            out RoWorldMapLuaFile file,
            out string diagnostic
        ) {
            for (int d = 0; d < DirectoryCandidates.Length; d++) {
                for (int e = 0; e < ExtensionCandidates.Length; e++) {
                    string candidate = DirectoryCandidates[d] + "/" + logicalName + ExtensionCandidates[e];
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
            }

            for (int e = 0; e < ExtensionCandidates.Length; e++) {
                string suffix = "worldviewdata/" + logicalName + ExtensionCandidates[e];
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
            diagnostic = "Missing worldviewdata file: " + logicalName + ".lub/.lua";
            return false;
        }
    }

    public sealed class RoWorldMapLuaFile {
        public RoWorldMapLuaFile(string path, byte[] bytes) {
            Path = path ?? string.Empty;
            Bytes = bytes ?? Array.Empty<byte>();
        }

        public string Path { get; }
        public byte[] Bytes { get; }
        public bool IsBinaryLua => Bytes.Length >= 4 && Bytes[0] == 0x1b && Bytes[1] == (byte) 'L' && Bytes[2] == (byte) 'u' && Bytes[3] == (byte) 'a';
    }

    public sealed class RoWorldMapLuaParseResult {
        public RoWorldMapLuaParseResult(
            IReadOnlyList<RoWorldMapDefinition> worlds,
            IReadOnlyList<RoWorldMapCategory> categories,
            IReadOnlyList<string> diagnostics
        ) {
            Worlds = worlds ?? Array.Empty<RoWorldMapDefinition>();
            Categories = categories ?? Array.Empty<RoWorldMapCategory>();
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        public IReadOnlyList<RoWorldMapDefinition> Worlds { get; }
        public IReadOnlyList<RoWorldMapCategory> Categories { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }

    public static class RoWorldMapLuaParser {
        public static RoWorldMapLuaParseResult ParseFiles(IEnumerable<RoWorldMapLuaFile> files) {
            RoWorldMapLuaExecutionContext context = ParseContext(files);
            context.BuildCatalogFromGlobals();
            return new RoWorldMapLuaParseResult(context.Worlds, context.Categories, context.Diagnostics);
        }

        internal static RoWorldMapLuaExecutionContext ParseContext(IEnumerable<RoWorldMapLuaFile> files) {
            var context = new RoWorldMapLuaExecutionContext();
            if (files != null) {
                foreach (RoWorldMapLuaFile file in files) {
                    if (file == null || file.Bytes == null || file.Bytes.Length == 0) {
                        continue;
                    }

                    try {
                        if (file.IsBinaryLua) {
                            RoWorldMapLuaBytecodeExtractor.Apply(file.Path, file.Bytes, context);
                        } else {
                            string text = RoTextEncoding.Decode(file.Bytes);
                            new RoWorldMapLuaTextParser(file.Path, text, context).Parse();
                        }
                    } catch (Exception ex) {
                        context.Diagnostics.Add($"Could not parse World Map data file '{file.Path}': {ex.Message}");
                    }
                }
            }

            return context;
        }

        public static RoWorldMapLuaParseResult ParseText(string path, string text) {
            return ParseFiles(new[] {
                new RoWorldMapLuaFile(path ?? "worldviewdata.lua", System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty))
            });
        }

        internal static RoWorldMapLuaExecutionContext ParseTextContext(string path, string text) {
            return ParseContext(new[] {
                new RoWorldMapLuaFile(path ?? "data.lua", System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty))
            });
        }
    }

    public static class RoWorldMapDataUtility {
        public static string NormalizeMapId(string mapName) {
            if (string.IsNullOrWhiteSpace(mapName)) {
                return string.Empty;
            }

            string normalized = mapName.Trim().Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < normalized.Length) {
                normalized = normalized.Substring(slash + 1);
            }

            if (normalized.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".gat", StringComparison.OrdinalIgnoreCase)) {
                normalized = normalized.Substring(0, normalized.Length - 4);
            }

            return normalized.ToLowerInvariant();
        }

        public static string NormalizeSearchText(string value) {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }

    internal sealed class RoWorldMapLuaExecutionContext {
        public const string NavigationRowsGlobalPrefix = "__navi_rows_";

        private readonly Dictionary<string, LuaValue> globals = new Dictionary<string, LuaValue>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<IReadOnlyList<LuaValue>>> functionCalls =
            new Dictionary<string, List<IReadOnlyList<LuaValue>>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<DirectWorld> directWorlds = new List<DirectWorld>();

        public List<RoWorldMapDefinition> Worlds { get; } = new List<RoWorldMapDefinition>();
        public List<RoWorldMapCategory> Categories { get; } = new List<RoWorldMapCategory>();
        public List<string> Diagnostics { get; } = new List<string>();

        public LuaValue GetGlobal(string name) {
            if (!string.IsNullOrEmpty(name) && globals.TryGetValue(name, out LuaValue value)) {
                return value;
            }

            return LuaValue.Nil;
        }

        public IEnumerable<KeyValuePair<string, LuaValue>> Globals => globals;

        internal IReadOnlyList<IReadOnlyList<LuaValue>> GetFunctionCalls(string functionName) {
            if (!string.IsNullOrWhiteSpace(functionName) &&
                functionCalls.TryGetValue(functionName.Trim(), out List<IReadOnlyList<LuaValue>> calls)) {
                return calls;
            }

            return Array.Empty<IReadOnlyList<LuaValue>>();
        }

        public void SetGlobal(string name, LuaValue value) {
            if (string.IsNullOrWhiteSpace(name)) {
                return;
            }

            globals[name.Trim()] = value ?? LuaValue.Nil;
        }

        public LuaValue ResolvePath(IReadOnlyList<string> path) {
            if (path == null || path.Count == 0) {
                return LuaValue.Nil;
            }

            LuaValue current = GetGlobal(path[0]);
            for (int i = 1; i < path.Count; i++) {
                if (current == null || current.Kind != LuaValueKind.Table) {
                    return LuaValue.Nil;
                }

                current = current.Table.Get(path[i]);
            }

            return current ?? LuaValue.Nil;
        }

        public void AssignPath(IReadOnlyList<string> path, LuaValue value) {
            if (path == null || path.Count == 0) {
                return;
            }

            if (path.Count == 1) {
                SetGlobal(path[0], value);
                return;
            }

            LuaValue current = GetGlobal(path[0]);
            if (current.Kind != LuaValueKind.Table) {
                current = LuaValue.FromTable(new LuaTableValue());
                SetGlobal(path[0], current);
            }

            LuaTableValue table = current.Table;
            for (int i = 1; i < path.Count - 1; i++) {
                LuaValue next = table.Get(path[i]);
                if (next.Kind != LuaValueKind.Table) {
                    next = LuaValue.FromTable(new LuaTableValue());
                    table.Set(path[i], next);
                }

                table = next.Table;
            }

            table.Set(path[path.Count - 1], value ?? LuaValue.Nil);
        }

        public void AssignTableKey(IReadOnlyList<string> path, LuaValue key, LuaValue value) {
            if (path == null || path.Count == 0 || key == null) {
                return;
            }

            LuaValue current = ResolvePath(path);
            if (current.Kind != LuaValueKind.Table) {
                current = LuaValue.FromTable(new LuaTableValue());
                AssignPath(path, current);
            }

            current.Table.Set(key, value);
        }

        public void InvokeFunction(string functionName, IReadOnlyList<LuaValue> args) {
            RecordFunctionCall(functionName, args);

            if (TryRegisterNavigationRow(functionName, args)) {
                return;
            }

            if (string.Equals(functionName, "AddWorldMapCategory", StringComparison.OrdinalIgnoreCase)) {
                RegisterCategory(
                    ValueToString(args, 0),
                    ValueToString(args, 1),
                    ValueToString(args, 2));
                return;
            }

            if (string.Equals(functionName, "AddMapToWorld", StringComparison.OrdinalIgnoreCase)) {
                RegisterMap(
                    ValueToInt(args, 0),
                    ValueToString(args, 1),
                    ValueToString(args, 2),
                    ValueToFloat(args, 3),
                    ValueToFloat(args, 4),
                    ValueToFloat(args, 5),
                    ValueToFloat(args, 6),
                    ValueToString(args, 7),
                    ValueToString(args, 8),
                    ValueToInt(args, 9));
            }
        }

        private void RecordFunctionCall(string functionName, IReadOnlyList<LuaValue> args) {
            if (string.IsNullOrWhiteSpace(functionName)) {
                return;
            }

            string key = functionName.Trim();
            if (!functionCalls.TryGetValue(key, out List<IReadOnlyList<LuaValue>> calls)) {
                calls = new List<IReadOnlyList<LuaValue>>();
                functionCalls[key] = calls;
            }

            var copy = new LuaValue[args != null ? args.Count : 0];
            for (int i = 0; i < copy.Length; i++) {
                copy[i] = args[i] ?? LuaValue.Nil;
            }

            calls.Add(copy);
        }

        private bool TryRegisterNavigationRow(string functionName, IReadOnlyList<LuaValue> args) {
            if (string.IsNullOrWhiteSpace(functionName) ||
                !functionName.StartsWith("Navi_", StringComparison.OrdinalIgnoreCase) ||
                args == null ||
                args.Count == 0) {
                return false;
            }

            string bucketName = NavigationRowsGlobalPrefix + functionName.Trim();
            LuaValue tableValue = GetGlobal(bucketName);
            if (tableValue.Kind != LuaValueKind.Table) {
                tableValue = LuaValue.FromTable(new LuaTableValue());
                SetGlobal(bucketName, tableValue);
            }

            var row = new LuaTableValue();
            for (int i = 0; i < args.Count; i++) {
                row.AddArray(args[i]);
            }

            tableValue.Table.AddArray(LuaValue.FromTable(row));
            return true;
        }

        public void BuildCatalogFromGlobals() {
            Worlds.Clear();
            Categories.Clear();

            LuaValue worldList = GetGlobal("World_List");
            if (worldList.Kind == LuaValueKind.Table) {
                BuildFromWorldList(worldList.Table);
            }

            if (Worlds.Count == 0 && directWorlds.Count > 0) {
                BuildFromDirectCalls();
            }

            if (Worlds.Count == 0) {
                Diagnostics.Add("World Map parser did not find World_List or direct AddWorldMapCategory/AddMapToWorld calls.");
            }
        }

        private void BuildFromWorldList(LuaTableValue worldList) {
            for (int i = 1; i <= worldList.ArrayCount; i++) {
                LuaValue rowValue = worldList.Get(i);
                if (rowValue.Kind != LuaValueKind.Table) {
                    continue;
                }

                LuaTableValue row = rowValue.Table;
                string worldName = row.Get(1).ToDisplayString();
                string mainTable = row.Get(2).ToDisplayString();
                string dungeonTable = row.Get(3).ToDisplayString();
                string background = row.Get(4).ToDisplayString();
                if (string.IsNullOrWhiteSpace(mainTable) && string.IsNullOrWhiteSpace(background)) {
                    continue;
                }

                var locations = new List<RoWorldMapLocation>();
                AddMainTableLocations(mainTable, locations);
                AddDungeonTableLocations(dungeonTable, locations);

                Categories.Add(new RoWorldMapCategory(background, worldName, mainTable));
                Worlds.Add(new RoWorldMapDefinition(
                    background,
                    worldName,
                    background,
                    0,
                    99,
                    mainTable,
                    locations));
            }
        }

        private void AddMainTableLocations(string tableName, List<RoWorldMapLocation> locations) {
            LuaValue tableValue = GetGlobal(tableName);
            if (tableValue.Kind != LuaValueKind.Table) {
                return;
            }

            LuaTableValue table = tableValue.Table;
            for (int i = 1; i <= table.ArrayCount; i++) {
                LuaValue entryValue = table.Get(i);
                if (entryValue.Kind != LuaValueKind.Table) {
                    continue;
                }

                LuaTableValue entry = entryValue.Table;
                int index = entry.Get(1).ToInt();
                string mapId = entry.Get(2).ToDisplayString();
                float left = entry.Get(3).ToFloat();
                float top = entry.Get(4).ToFloat();
                float right = entry.Get(5).ToFloat();
                float bottom = entry.Get(6).ToFloat();
                string displayName = entry.Get(7).ToDisplayString();
                string level = entry.Get(8).ToDisplayString();
                locations.Add(new RoWorldMapLocation(
                    index,
                    mapId,
                    displayName,
                    left,
                    top,
                    right - left,
                    bottom - top,
                    level,
                    0,
                    0,
                    99));
            }
        }

        private void AddDungeonTableLocations(string tableName, List<RoWorldMapLocation> locations) {
            LuaValue tableValue = GetGlobal(tableName);
            if (tableValue.Kind != LuaValueKind.Table) {
                return;
            }

            LuaTableValue table = tableValue.Table;
            for (int i = 1; i <= table.ArrayCount; i++) {
                LuaValue entryValue = table.Get(i);
                if (entryValue.Kind != LuaValueKind.Table) {
                    continue;
                }

                LuaTableValue entry = entryValue.Table;
                int index = entry.Get(1).ToInt();
                string mapId = FindMainLocationMapId(locations, index);
                float left = entry.Get(2).ToFloat();
                float top = entry.Get(3).ToFloat();
                float right = entry.Get(4).ToFloat();
                float bottom = entry.Get(5).ToFloat();
                string displayName = entry.Get(6).ToDisplayString();
                string level = entry.Get(7).ToDisplayString();
                locations.Add(new RoWorldMapLocation(
                    index,
                    mapId,
                    displayName,
                    left,
                    top,
                    right - left,
                    bottom - top,
                    level,
                    1,
                    0,
                    99));
            }
        }

        private static string FindMainLocationMapId(List<RoWorldMapLocation> locations, int index) {
            for (int i = 0; i < locations.Count; i++) {
                RoWorldMapLocation location = locations[i];
                if (location.Index == index && location.Type == 0) {
                    return location.MapId;
                }
            }

            return string.Empty;
        }

        private void RegisterCategory(string id, string name, string tableKey) {
            var world = new DirectWorld(id, name, tableKey);
            directWorlds.Add(world);
        }

        private void RegisterMap(
            int index,
            string worldTableKey,
            string rswName,
            float left,
            float top,
            float right,
            float bottom,
            string name,
            string level,
            int type
        ) {
            DirectWorld world = null;
            for (int i = 0; i < directWorlds.Count; i++) {
                if (string.Equals(directWorlds[i].TableKey, worldTableKey, StringComparison.OrdinalIgnoreCase)) {
                    world = directWorlds[i];
                    break;
                }
            }

            if (world == null) {
                world = new DirectWorld(worldTableKey, worldTableKey, worldTableKey);
                directWorlds.Add(world);
            }

            string mapId = rswName;
            if (type == 1 && string.IsNullOrWhiteSpace(mapId)) {
                for (int i = 0; i < world.Locations.Count; i++) {
                    RoWorldMapLocation candidate = world.Locations[i];
                    if (candidate.Index == index && candidate.Type == 0) {
                        mapId = candidate.MapId;
                        break;
                    }
                }
            }

            world.Locations.Add(new RoWorldMapLocation(
                index,
                mapId,
                name,
                left,
                top,
                right - left,
                bottom - top,
                level,
                type,
                0,
                99));
        }

        private void BuildFromDirectCalls() {
            for (int i = 0; i < directWorlds.Count; i++) {
                DirectWorld direct = directWorlds[i];
                Categories.Add(new RoWorldMapCategory(direct.Id, direct.Name, direct.TableKey));
                Worlds.Add(new RoWorldMapDefinition(
                    direct.Id,
                    direct.Name,
                    direct.Id,
                    0,
                    99,
                    direct.TableKey,
                    direct.Locations));
            }
        }

        private static string ValueToString(IReadOnlyList<LuaValue> args, int index) {
            return args != null && index >= 0 && index < args.Count ? args[index].ToDisplayString() : string.Empty;
        }

        private static int ValueToInt(IReadOnlyList<LuaValue> args, int index) {
            return args != null && index >= 0 && index < args.Count ? args[index].ToInt() : 0;
        }

        private static float ValueToFloat(IReadOnlyList<LuaValue> args, int index) {
            return args != null && index >= 0 && index < args.Count ? args[index].ToFloat() : 0f;
        }

        private sealed class DirectWorld {
            public DirectWorld(string id, string name, string tableKey) {
                Id = id ?? string.Empty;
                Name = string.IsNullOrWhiteSpace(name) ? Id : name;
                TableKey = tableKey ?? string.Empty;
                Locations = new List<RoWorldMapLocation>();
            }

            public string Id { get; }
            public string Name { get; }
            public string TableKey { get; }
            public List<RoWorldMapLocation> Locations { get; }
        }
    }

    internal enum LuaValueKind {
        Nil,
        Boolean,
        Number,
        String,
        Table,
        FunctionName,
        FunctionPrototype
    }

    internal sealed class LuaValue {
        public static readonly LuaValue Nil = new LuaValue(LuaValueKind.Nil, null, 0, false, null, null, null);

        private LuaValue(
            LuaValueKind kind,
            string text,
            double number,
            bool boolean,
            LuaTableValue table,
            string functionName,
            LuaFunctionPrototype functionPrototype
        ) {
            Kind = kind;
            Text = text ?? string.Empty;
            Number = number;
            Boolean = boolean;
            Table = table;
            FunctionName = functionName ?? string.Empty;
            FunctionPrototype = functionPrototype;
        }

        public LuaValueKind Kind { get; }
        public string Text { get; }
        public double Number { get; }
        public bool Boolean { get; }
        public LuaTableValue Table { get; }
        public string FunctionName { get; }
        public LuaFunctionPrototype FunctionPrototype { get; }

        public static LuaValue FromBoolean(bool value) => new LuaValue(LuaValueKind.Boolean, null, 0, value, null, null, null);
        public static LuaValue FromNumber(double value) => new LuaValue(LuaValueKind.Number, null, value, false, null, null, null);
        public static LuaValue FromString(string value) => new LuaValue(LuaValueKind.String, value, 0, false, null, null, null);
        public static LuaValue FromTable(LuaTableValue value) => new LuaValue(LuaValueKind.Table, null, 0, false, value ?? new LuaTableValue(), null, null);
        public static LuaValue FromFunctionName(string value) => new LuaValue(LuaValueKind.FunctionName, null, 0, false, null, value, null);
        public static LuaValue FromFunctionPrototype(LuaFunctionPrototype value) => new LuaValue(LuaValueKind.FunctionPrototype, null, 0, false, null, null, value);

        public string ToDisplayString() {
            switch (Kind) {
                case LuaValueKind.String: return Text;
                case LuaValueKind.Number:
                    return Math.Abs(Number) < 0.000001
                        ? string.Empty
                        : Number.ToString("0.########", CultureInfo.InvariantCulture);
                case LuaValueKind.Boolean: return Boolean ? "true" : "false";
                default: return string.Empty;
            }
        }

        public int ToInt() {
            if (Kind == LuaValueKind.Number) {
                return (int) Math.Round(Number);
            }

            return int.TryParse(ToDisplayString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        public float ToFloat() {
            if (Kind == LuaValueKind.Number) {
                return (float) Number;
            }

            return float.TryParse(ToDisplayString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }
    }

    internal sealed class LuaTableValue {
        private readonly List<LuaValue> array = new List<LuaValue>();
        private readonly Dictionary<string, LuaValue> fields = new Dictionary<string, LuaValue>(StringComparer.OrdinalIgnoreCase);

        public int ArrayCount => array.Count;
        public IReadOnlyList<LuaValue> ArrayValues => array;
        public IEnumerable<KeyValuePair<string, LuaValue>> Fields => fields;

        public void AddArray(LuaValue value) {
            array.Add(value ?? LuaValue.Nil);
        }

        public void Set(double key, LuaValue value) {
            int index = (int) Math.Round(key);
            if (Math.Abs(index - key) < 0.000001 && index >= 1) {
                while (array.Count < index) {
                    array.Add(LuaValue.Nil);
                }

                array[index - 1] = value ?? LuaValue.Nil;
                return;
            }

            Set(key.ToString("0.########", CultureInfo.InvariantCulture), value);
        }

        public void Set(string key, LuaValue value) {
            if (string.IsNullOrEmpty(key)) {
                return;
            }

            fields[key] = value ?? LuaValue.Nil;
        }

        public LuaValue Get(int index) {
            return index >= 1 && index <= array.Count ? array[index - 1] : LuaValue.Nil;
        }

        public LuaValue Get(string key) {
            if (!string.IsNullOrEmpty(key) && fields.TryGetValue(key, out LuaValue value)) {
                return value;
            }

            if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)) {
                return Get(index);
            }

            return LuaValue.Nil;
        }

        public LuaValue Get(LuaValue key) {
            if (key == null) {
                return LuaValue.Nil;
            }

            return key.Kind == LuaValueKind.Number ? Get(key.ToInt()) : Get(key.ToDisplayString());
        }

        public void Set(LuaValue key, LuaValue value) {
            if (key == null) {
                return;
            }

            if (key.Kind == LuaValueKind.Number) {
                Set(key.Number, value);
            } else {
                Set(key.ToDisplayString(), value);
            }
        }
    }

    internal sealed class RoWorldMapLuaTextParser {
        private readonly string path;
        private readonly string text;
        private readonly RoWorldMapLuaExecutionContext context;
        private int index;

        public RoWorldMapLuaTextParser(string path, string text, RoWorldMapLuaExecutionContext context) {
            this.path = path ?? string.Empty;
            this.text = text ?? string.Empty;
            this.context = context;
        }

        public void Parse() {
            while (!IsEnd) {
                SkipTrivia();
                if (IsEnd) {
                    break;
                }

                if (TryConsumeKeyword("local")) {
                    ParseLocalStatement();
                    continue;
                }

                if (!TryReadPath(out List<string> pathParts)) {
                    index++;
                    continue;
                }

                SkipTrivia();
                if (TryConsume("[")) {
                    LuaValue key = ParseExpression();
                    SkipTrivia();
                    Consume("]");
                    SkipTrivia();
                    if (TryConsume("=")) {
                        LuaValue value = ParseExpression();
                        context.AssignTableKey(pathParts, key, value);
                    }

                    continue;
                }

                if (TryConsume("=")) {
                    LuaValue value = ParseExpression();
                    context.AssignPath(pathParts, value);
                    continue;
                }

                if (TryConsume("(")) {
                    List<LuaValue> args = ParseArgumentList();
                    context.InvokeFunction(pathParts.Count > 0 ? pathParts[pathParts.Count - 1] : string.Empty, args);
                }
            }
        }

        private void ParseLocalStatement() {
            SkipTrivia();
            if (!TryReadIdentifier(out string name)) {
                return;
            }

            SkipTrivia();
            if (!TryConsume("=")) {
                return;
            }

            LuaValue value = ParseExpression();
            context.SetGlobal(name, value);
        }

        private LuaValue ParseExpression() {
            SkipTrivia();
            if (IsEnd) {
                return LuaValue.Nil;
            }

            char c = Current;
            if (c == '"' || c == '\'') {
                return LuaValue.FromString(ReadString());
            }

            if (c == '{') {
                return LuaValue.FromTable(ParseTable());
            }

            if (c == '-' || char.IsDigit(c)) {
                return LuaValue.FromNumber(ReadNumber());
            }

            if (TryConsumeKeyword("nil")) {
                return LuaValue.Nil;
            }

            if (TryConsumeKeyword("true")) {
                return LuaValue.FromBoolean(true);
            }

            if (TryConsumeKeyword("false")) {
                return LuaValue.FromBoolean(false);
            }

            if (TryReadPath(out List<string> pathParts)) {
                LuaValue resolved = context.ResolvePath(pathParts);
                if (resolved.Kind != LuaValueKind.Nil) {
                    return resolved;
                }

                return LuaValue.FromString(pathParts[pathParts.Count - 1]);
            }

            index++;
            return LuaValue.Nil;
        }

        private LuaTableValue ParseTable() {
            var table = new LuaTableValue();
            Consume("{");
            while (!IsEnd) {
                SkipTrivia();
                if (TryConsume("}")) {
                    break;
                }

                if (TryConsume("[")) {
                    LuaValue key = ParseExpression();
                    SkipTrivia();
                    Consume("]");
                    SkipTrivia();
                    Consume("=");
                    LuaValue value = ParseExpression();
                    table.Set(key, value);
                } else if (PeekIdentifierAssignment(out string fieldName)) {
                    TryReadIdentifier(out fieldName);
                    SkipTrivia();
                    Consume("=");
                    LuaValue value = ParseExpression();
                    table.Set(fieldName, value);
                } else {
                    LuaValue value = ParseExpression();
                    table.AddArray(value);
                }

                SkipTrivia();
                TryConsume(",");
                TryConsume(";");
            }

            return table;
        }

        private List<LuaValue> ParseArgumentList() {
            var args = new List<LuaValue>();
            while (!IsEnd) {
                SkipTrivia();
                if (TryConsume(")")) {
                    break;
                }

                args.Add(ParseExpression());
                SkipTrivia();
                TryConsume(",");
            }

            return args;
        }

        private bool PeekIdentifierAssignment(out string name) {
            int save = index;
            bool ok = TryReadIdentifier(out name);
            if (ok) {
                SkipTrivia();
                ok = !TryConsume(".") && TryConsume("=");
            }

            index = save;
            return ok;
        }

        private bool TryReadPath(out List<string> parts) {
            parts = new List<string>();
            if (!TryReadIdentifier(out string first)) {
                return false;
            }

            parts.Add(first);
            while (true) {
                int save = index;
                SkipTrivia();
                if (!TryConsume(".")) {
                    index = save;
                    break;
                }

                SkipTrivia();
                if (!TryReadIdentifier(out string next)) {
                    index = save;
                    break;
                }

                parts.Add(next);
            }

            return true;
        }

        private bool TryReadIdentifier(out string value) {
            SkipTrivia();
            if (IsEnd || !(char.IsLetter(Current) || Current == '_')) {
                value = null;
                return false;
            }

            int start = index;
            index++;
            while (!IsEnd && (char.IsLetterOrDigit(Current) || Current == '_')) {
                index++;
            }

            value = text.Substring(start, index - start);
            return true;
        }

        private string ReadString() {
            char quote = Current;
            index++;
            var builder = new System.Text.StringBuilder();
            while (!IsEnd) {
                char c = Current;
                index++;
                if (c == quote) {
                    break;
                }

                if (c == '\\' && !IsEnd) {
                    char escaped = Current;
                    index++;
                    switch (escaped) {
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case '\\': builder.Append('\\'); break;
                        case '"': builder.Append('"'); break;
                        case '\'': builder.Append('\''); break;
                        default: builder.Append(escaped); break;
                    }
                } else {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private double ReadNumber() {
            int start = index;
            if (Current == '-') {
                index++;
            }

            while (!IsEnd && (char.IsDigit(Current) || Current == '.')) {
                index++;
            }

            string number = text.Substring(start, index - start);
            return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
        }

        private void SkipTrivia() {
            bool moved;
            do {
                moved = false;
                while (!IsEnd && char.IsWhiteSpace(Current)) {
                    index++;
                    moved = true;
                }

                if (Peek("--")) {
                    index += 2;
                    if (Peek("[[")) {
                        index += 2;
                        while (!IsEnd && !Peek("]]")) {
                            index++;
                        }

                        if (Peek("]]")) {
                            index += 2;
                        }
                    } else {
                        while (!IsEnd && Current != '\n') {
                            index++;
                        }
                    }

                    moved = true;
                }
            } while (moved);
        }

        private bool TryConsumeKeyword(string keyword) {
            SkipTrivia();
            if (!Peek(keyword)) {
                return false;
            }

            int end = index + keyword.Length;
            if (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) {
                return false;
            }

            index = end;
            return true;
        }

        private bool TryConsume(string value) {
            SkipTrivia();
            if (!Peek(value)) {
                return false;
            }

            index += value.Length;
            return true;
        }

        private void Consume(string value) {
            if (!TryConsume(value)) {
                context.Diagnostics.Add($"World Map Lua parser expected '{value}' in '{path}' near offset {index}.");
            }
        }

        private bool Peek(string value) {
            if (string.IsNullOrEmpty(value) || index + value.Length > text.Length) {
                return false;
            }

            for (int i = 0; i < value.Length; i++) {
                if (text[index + i] != value[i]) {
                    return false;
                }
            }

            return true;
        }

        private bool IsEnd => index >= text.Length;
        private char Current => text[index];
    }

    internal static class RoWorldMapLuaBytecodeExtractor {
        private const int BitRk = 1 << 8;
        private const int FieldsPerFlush = 50;

        public static void Apply(string path, byte[] bytes, RoWorldMapLuaExecutionContext context) {
            var reader = new LuaBinaryReader(path, bytes);
            LuaFunctionPrototype prototype = reader.ReadChunk();
            ExecutePrototype(prototype, context);
            LuaValue main = context.GetGlobal("main");
            if (main.Kind == LuaValueKind.FunctionPrototype && main.FunctionPrototype != null) {
                ExecutePrototype(main.FunctionPrototype, context);
            }
        }

        private static LuaValue ExecutePrototype(LuaFunctionPrototype prototype, RoWorldMapLuaExecutionContext context) {
            var registers = new LuaValue[Mathf.Max(256, prototype.MaxStackSize + 32)];
            for (int i = 0; i < registers.Length; i++) {
                registers[i] = LuaValue.Nil;
            }

            for (int pc = 0; pc < prototype.Code.Count; pc++) {
                uint instruction = prototype.Code[pc];
                int op = (int) (instruction & 0x3f);
                int a = (int) ((instruction >> 6) & 0xff);
                int c = (int) ((instruction >> 14) & 0x1ff);
                int b = (int) ((instruction >> 23) & 0x1ff);
                int bx = (int) ((instruction >> 14) & 0x3ffff);

                switch (op) {
                    case 0:
                        registers[a] = GetRegister(registers, b);
                        break;
                    case 1:
                        registers[a] = GetConstant(prototype, bx);
                        break;
                    case 2:
                        registers[a] = LuaValue.FromBoolean(b != 0);
                        if (c != 0) {
                            pc++;
                        }
                        break;
                    case 3:
                        for (int r = a; r <= b && r < registers.Length; r++) {
                            registers[r] = LuaValue.Nil;
                        }
                        break;
                    case 5:
                        string globalName = GetConstant(prototype, bx).ToDisplayString();
                        LuaValue global = context.GetGlobal(globalName);
                        registers[a] = global.Kind == LuaValueKind.Nil ? LuaValue.FromFunctionName(globalName) : global;
                        break;
                    case 6:
                        LuaValue table = GetRegister(registers, b);
                        registers[a] = table.Kind == LuaValueKind.Table ? table.Table.Get(ReadRk(prototype, registers, c)) : LuaValue.Nil;
                        break;
                    case 7:
                        context.SetGlobal(GetConstant(prototype, bx).ToDisplayString(), GetRegister(registers, a));
                        break;
                    case 9:
                        LuaValue target = GetRegister(registers, a);
                        if (target.Kind == LuaValueKind.Table) {
                            target.Table.Set(ReadRk(prototype, registers, b), ReadRk(prototype, registers, c));
                        }
                        break;
                    case 10:
                        registers[a] = LuaValue.FromTable(new LuaTableValue());
                        break;
                    case 28:
                        InvokeCall(context, registers, a, b);
                        break;
                    case 30:
                        return GetRegister(registers, a);
                    case 34:
                        ApplySetList(prototype, registers, ref pc, a, b, c);
                        break;
                    case 36:
                        registers[a] = LuaValue.FromFunctionPrototype(GetPrototype(prototype, bx));
                        LuaFunctionPrototype closure = registers[a].FunctionPrototype;
                        if (closure != null) {
                            pc += closure.UpvalueCount;
                        }

                        break;
                }
            }

            return LuaValue.Nil;
        }

        private static void InvokeCall(RoWorldMapLuaExecutionContext context, LuaValue[] registers, int a, int b) {
            LuaValue function = GetRegister(registers, a);
            if (function.Kind == LuaValueKind.FunctionPrototype && function.FunctionPrototype != null) {
                ExecutePrototype(function.FunctionPrototype, context);
                return;
            }

            if (function.Kind != LuaValueKind.FunctionName) {
                return;
            }

            int argCount = b > 0 ? b - 1 : 0;
            var args = new List<LuaValue>(argCount);
            for (int i = 0; i < argCount; i++) {
                args.Add(GetRegister(registers, a + 1 + i));
            }

            context.InvokeFunction(function.FunctionName, args);
        }

        private static void ApplySetList(
            LuaFunctionPrototype prototype,
            LuaValue[] registers,
            ref int pc,
            int a,
            int b,
            int c
        ) {
            LuaValue target = GetRegister(registers, a);
            if (target.Kind != LuaValueKind.Table) {
                return;
            }

            if (c == 0 && pc + 1 < prototype.Code.Count) {
                pc++;
                c = (int) prototype.Code[pc];
            }

            int count = b == 0 ? registers.Length - a - 1 : b;
            int start = (c - 1) * FieldsPerFlush;
            for (int i = 1; i <= count && a + i < registers.Length; i++) {
                target.Table.Set(start + i, GetRegister(registers, a + i));
            }
        }

        private static LuaValue ReadRk(LuaFunctionPrototype prototype, LuaValue[] registers, int operand) {
            return (operand & BitRk) != 0
                ? GetConstant(prototype, operand & ~BitRk)
                : GetRegister(registers, operand);
        }

        private static LuaValue GetRegister(LuaValue[] registers, int index) {
            return index >= 0 && index < registers.Length ? registers[index] ?? LuaValue.Nil : LuaValue.Nil;
        }

        private static LuaValue GetConstant(LuaFunctionPrototype prototype, int index) {
            return index >= 0 && index < prototype.Constants.Count ? prototype.Constants[index] ?? LuaValue.Nil : LuaValue.Nil;
        }

        private static LuaFunctionPrototype GetPrototype(LuaFunctionPrototype prototype, int index) {
            return prototype != null && index >= 0 && index < prototype.Prototypes.Count
                ? prototype.Prototypes[index]
                : null;
        }
    }

    internal sealed class LuaFunctionPrototype {
        public List<uint> Code { get; } = new List<uint>();
        public List<LuaValue> Constants { get; } = new List<LuaValue>();
        public List<LuaFunctionPrototype> Prototypes { get; } = new List<LuaFunctionPrototype>();
        public byte UpvalueCount { get; set; }
        public byte MaxStackSize { get; set; }
    }

    internal sealed class LuaBinaryReader {
        private readonly string path;
        private readonly byte[] bytes;
        private int index;
        private bool littleEndian = true;
        private int intSize = 4;
        private int sizeTSize = 4;
        private int instructionSize = 4;
        private int numberSize = 8;

        public LuaBinaryReader(string path, byte[] bytes) {
            this.path = path ?? string.Empty;
            this.bytes = bytes ?? Array.Empty<byte>();
        }

        public LuaFunctionPrototype ReadChunk() {
            ReadHeader();
            return ReadPrototype();
        }

        private void ReadHeader() {
            Require(12);
            if (bytes[0] != 0x1b || bytes[1] != (byte) 'L' || bytes[2] != (byte) 'u' || bytes[3] != (byte) 'a') {
                throw new InvalidDataException("Not a Lua binary chunk.");
            }

            if (bytes[4] != 0x51) {
                throw new InvalidDataException("World Map LUB parser supports Lua 5.1 chunks only.");
            }

            littleEndian = bytes[6] != 0;
            intSize = bytes[7];
            sizeTSize = bytes[8];
            instructionSize = bytes[9];
            numberSize = bytes[10];
            index = 12;
        }

        private LuaFunctionPrototype ReadPrototype() {
            ReadString();
            ReadInt();
            ReadInt();
            var prototype = new LuaFunctionPrototype();
            prototype.UpvalueCount = ReadByte();
            ReadByte();
            ReadByte();
            prototype.MaxStackSize = ReadByte();

            int codeCount = ReadInt();
            for (int i = 0; i < codeCount; i++) {
                prototype.Code.Add(ReadInstruction());
            }

            int constantCount = ReadInt();
            for (int i = 0; i < constantCount; i++) {
                prototype.Constants.Add(ReadConstant());
            }

            int protoCount = ReadInt();
            for (int i = 0; i < protoCount; i++) {
                prototype.Prototypes.Add(ReadPrototype());
            }

            SkipDebugInfo();
            return prototype;
        }

        private LuaValue ReadConstant() {
            byte type = ReadByte();
            switch (type) {
                case 0: return LuaValue.Nil;
                case 1: return LuaValue.FromBoolean(ReadByte() != 0);
                case 3: return LuaValue.FromNumber(ReadNumber());
                case 4: return LuaValue.FromString(ReadString());
                default: return LuaValue.Nil;
            }
        }

        private void SkipDebugInfo() {
            int lineInfoCount = ReadInt();
            Skip(lineInfoCount * intSize);

            int localCount = ReadInt();
            for (int i = 0; i < localCount; i++) {
                ReadString();
                ReadInt();
                ReadInt();
            }

            int upvalueCount = ReadInt();
            for (int i = 0; i < upvalueCount; i++) {
                ReadString();
            }
        }

        private string ReadString() {
            ulong size = ReadSizeT();
            if (size == 0) {
                return string.Empty;
            }

            if (size > int.MaxValue || index + (long) size > bytes.Length) {
                throw new InvalidDataException("Lua string extends beyond chunk length in " + path + ".");
            }

            int byteCount = (int) size - 1;
            byte[] data = new byte[Mathf.Max(0, byteCount)];
            Array.Copy(bytes, index, data, 0, data.Length);
            index += (int) size;
            return RoTextEncoding.Decode(data);
        }

        private double ReadNumber() {
            Require(numberSize);
            double value;
            if (numberSize == 8) {
                byte[] data = ReadEndianBytes(8);
                value = BitConverter.ToDouble(data, 0);
            } else if (numberSize == 4) {
                byte[] data = ReadEndianBytes(4);
                value = BitConverter.ToSingle(data, 0);
            } else {
                throw new InvalidDataException("Unsupported Lua number size " + numberSize + " in " + path + ".");
            }

            return value;
        }

        private uint ReadInstruction() {
            if (instructionSize != 4) {
                throw new InvalidDataException("Unsupported Lua instruction size " + instructionSize + " in " + path + ".");
            }

            return ReadUInt32();
        }

        private int ReadInt() {
            if (intSize == 4) {
                return unchecked((int) ReadUInt32());
            }

            if (intSize == 8) {
                return unchecked((int) ReadUInt64());
            }

            throw new InvalidDataException("Unsupported Lua int size " + intSize + " in " + path + ".");
        }

        private ulong ReadSizeT() {
            if (sizeTSize == 4) {
                return ReadUInt32();
            }

            if (sizeTSize == 8) {
                return ReadUInt64();
            }

            throw new InvalidDataException("Unsupported Lua size_t size " + sizeTSize + " in " + path + ".");
        }

        private uint ReadUInt32() {
            Require(4);
            byte[] data = ReadEndianBytes(4);
            return BitConverter.ToUInt32(data, 0);
        }

        private ulong ReadUInt64() {
            Require(8);
            byte[] data = ReadEndianBytes(8);
            return BitConverter.ToUInt64(data, 0);
        }

        private byte ReadByte() {
            Require(1);
            return bytes[index++];
        }

        private byte[] ReadEndianBytes(int count) {
            byte[] data = new byte[count];
            Array.Copy(bytes, index, data, 0, count);
            index += count;
            if (BitConverter.IsLittleEndian != littleEndian) {
                Array.Reverse(data);
            }

            return data;
        }

        private void Skip(int count) {
            Require(count);
            index += count;
        }

        private void Require(int count) {
            if (count < 0 || index + count > bytes.Length) {
                throw new EndOfStreamException("Unexpected end of Lua binary chunk in " + path + ".");
            }
        }
    }
}
