using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ROIO.Loaders;
using ROIO.Models;
using UnityEngine;

namespace Ragnarok.Assets {
    public sealed class RoAssetTables {
        public sealed class TableProbe {
            public TableProbe(
                string key,
                string purpose,
                bool requiredForPhase7,
                IReadOnlyList<string> candidatePaths,
                string resolvedPath,
                int byteCount,
                string error
            ) {
                Key = key;
                Purpose = purpose;
                RequiredForPhase7 = requiredForPhase7;
                CandidatePaths = candidatePaths ?? Array.Empty<string>();
                ResolvedPath = resolvedPath;
                ByteCount = byteCount;
                Error = error;
            }

            public string Key { get; }
            public string Purpose { get; }
            public bool RequiredForPhase7 { get; }
            public IReadOnlyList<string> CandidatePaths { get; }
            public string ResolvedPath { get; }
            public int ByteCount { get; }
            public string Error { get; }
            public bool IsAvailable => !string.IsNullOrEmpty(ResolvedPath) && string.IsNullOrEmpty(Error);
            public bool IsMissing => string.IsNullOrEmpty(ResolvedPath);
        }

        private sealed class TableProbeSpec {
            public TableProbeSpec(string key, string purpose, bool requiredForPhase7, string[] candidatePaths) {
                Key = key;
                Purpose = purpose;
                RequiredForPhase7 = requiredForPhase7;
                CandidatePaths = candidatePaths ?? Array.Empty<string>();
            }

            public string Key { get; }
            public string Purpose { get; }
            public bool RequiredForPhase7 { get; }
            public string[] CandidatePaths { get; }
        }

        private static readonly TableProbeSpec[] Phase7ProbeSpecs = {
            Required("jobidentity", "actor job ids and monster/NPC sprite ids", LuaDataInfo("jobidentity")),
            Required("jobname", "job and monster sprite/resource names", LuaDataInfo("jobname", "jobname_f")),
            Required("pcjobnamegender", "gender-specific PC body path rules", LuaDataInfo("pcjobnamegender", "pcjobnamegender_f")),
            Required("accessoryid", "headgear/accessory ids", LuaDataInfo("accessoryid")),
            Required("accname", "headgear/accessory sprite names", LuaDataInfo("accname", "accname_f")),
            Required("spriterobeid", "robe/garment ids", LuaDataInfo("spriterobeid")),
            Required("spriterobename", "robe/garment sprite names", LuaDataInfo("spriterobename", "spriterobename_f")),
            Required("weapontable", "weapon sprite path metadata", LuaDataInfo("weapontable", "weapontable_f")),
            Required("iteminfo", "item display names, icons, and class metadata", ItemInfoCandidates()),
            Optional("npcidentity", "NPC identity to sprite mapping", LuaDataInfo("npcidentity")),
            Optional("mob_db", "monster display metadata overlay", MobDbCandidates()),
            Optional("skillid", "skill ids", LuaSkillInfo("skillid")),
            Optional("skillinfolist", "skill display/icon metadata", LuaSkillInfo("skillinfolist", "skillinfo_f")),
            Optional("skilldescript", "skill descriptions", LuaSkillInfo("skilldescript")),
            Optional("stateiconinfo", "status icon metadata", LuaStateIcon("stateiconinfo", "stateiconinfo_f", "stateiconimginfo")),
            Optional("msgstring_kr", "Lua-side localized message strings", LuaRoot("msgstring_kr")),
        };

        private static readonly string[] SkylessDungeonMapPrefixes = {
            "orcsdun",
            "moc_pryd",
            "prt_sewb",
            "anthell",
            "treasure",
            "c_tower",
            "thor_v",
            "abyss_",
            "ra_san",
            "jupe_",
            "yggdrasil",
            "gl_cas",
            "gl_church",
            "gl_chyard",
            "gl_in",
            "gl_knt",
            "gl_prison",
            "gl_sew",
            "gl_step"
        };

        public static readonly RoAssetTables Empty = new RoAssetTables(
            new Dictionary<string, MapTableStruct>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, RoViewpointPreset>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new List<TableProbe>()
        );

        private readonly Dictionary<string, MapTableStruct> mapTable;
        private readonly Dictionary<string, string> resNameTable;
        private readonly Dictionary<string, string> msgStringTable;
        private readonly Dictionary<string, RoViewpointPreset> viewpointTable;
        private readonly HashSet<string> indoorMaps;
        private readonly List<TableProbe> tableProbes;

        private RoAssetTables(
            Dictionary<string, MapTableStruct> mapTable,
            Dictionary<string, string> resNameTable,
            Dictionary<string, string> msgStringTable,
            Dictionary<string, RoViewpointPreset> viewpointTable,
            HashSet<string> indoorMaps,
            List<TableProbe> tableProbes
        ) {
            this.mapTable       = mapTable;
            this.resNameTable   = resNameTable;
            this.msgStringTable = msgStringTable;
            this.viewpointTable = viewpointTable;
            this.indoorMaps     = indoorMaps;
            this.tableProbes    = tableProbes;
        }

        public IReadOnlyDictionary<string, MapTableStruct>       MapTable       => mapTable;
        public IReadOnlyDictionary<string, string>               ResNameTable   => resNameTable;
        public IReadOnlyDictionary<string, string>               MsgStringTable => msgStringTable;
        public IReadOnlyDictionary<string, RoViewpointPreset>    ViewpointTable => viewpointTable;
        public IReadOnlyCollection<string>                       IndoorMaps     => indoorMaps;
        public IReadOnlyList<TableProbe>                         TableProbes    => tableProbes;

        public int RequiredPhase7TableCount {
            get {
                int count = 0;
                for (int i = 0; i < tableProbes.Count; i++) {
                    if (tableProbes[i].RequiredForPhase7) {
                        count++;
                    }
                }

                return count;
            }
        }

        public int LoadedRequiredPhase7TableCount {
            get {
                int count = 0;
                for (int i = 0; i < tableProbes.Count; i++) {
                    TableProbe probe = tableProbes[i];
                    if (probe.RequiredForPhase7 && probe.IsAvailable) {
                        count++;
                    }
                }

                return count;
            }
        }

        public int MissingRequiredPhase7TableCount => RequiredPhase7TableCount - LoadedRequiredPhase7TableCount;

        public static RoAssetTables FromMapTable(IDictionary<string, MapTableStruct> mapEntries) {
            var mapTable = new Dictionary<string, MapTableStruct>(StringComparer.OrdinalIgnoreCase);
            if (mapEntries != null) {
                foreach (KeyValuePair<string, MapTableStruct> entry in mapEntries) {
                    mapTable[entry.Key] = entry.Value;
                }
            }

            return new RoAssetTables(
                mapTable,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, RoViewpointPreset>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new List<TableProbe>()
            );
        }

        public static RoAssetTables Load(RoAssetDatabase assets) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            var tables = new RoAssetTables(
                new Dictionary<string, MapTableStruct>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, RoViewpointPreset>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new List<TableProbe>()
            );

            tables.LoadMsgStringTable(assets);
            tables.LoadMp3NameTable(assets);
            tables.LoadMapNameTable(assets);
            tables.LoadResNameTable(assets);
            tables.LoadViewpointTable(assets);
            tables.LoadIndoorRswTable(assets);
            tables.LoadPhase7TableProbes(assets);
            return tables;
        }

        /// <summary>
        /// Returns true if the map has a fixed viewpoint entry in viewpointtable.txt.
        /// <paramref name="mapRsw"/> should be the RSW filename including extension, e.g. "prontera.rsw".
        /// </summary>
        public bool TryGetViewpoint(string mapRsw, out RoViewpointPreset preset) {
            if (!string.IsNullOrWhiteSpace(mapRsw) && viewpointTable.TryGetValue(mapRsw, out preset)) {
                return true;
            }

            preset = default;
            return false;
        }

        /// <summary>
        /// Returns true if the map is listed in indoorRswTable.txt (orbital rotations disabled).
        /// <paramref name="mapRsw"/> should be the RSW filename including extension.
        /// </summary>
        public bool IsIndoorMap(string mapRsw) {
            return !string.IsNullOrWhiteSpace(mapRsw) && indoorMaps.Contains(mapRsw);
        }

        public bool IsSkylessMap(string mapRsw) {
            string normalized = NormalizeMapRswName(mapRsw);
            return !string.IsNullOrEmpty(normalized) && (IsIndoorMap(normalized) || IsDungeonMap(normalized) || IsInteriorMapName(normalized));
        }

        /// <summary>
        /// Interiors indoorRswTable.txt leaves out (it lists the maps whose camera may not turn, not every room):
        /// "in_*" and "*_in&lt;digit&gt;*" houses (in_hunter, spl_in01, yuno_in02), the novice training castles
        /// new_1-2 … new_5-2, iz_int.
        /// </summary>
        public static bool IsInteriorMapName(string mapRsw) {
            string mapName = NormalizeMapRswName(mapRsw);
            if (string.IsNullOrEmpty(mapName)) {
                return false;
            }

            if (mapName.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase)) {
                mapName = mapName.Substring(0, mapName.Length - 4);
            }

            if (mapName.StartsWith("in_", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mapName, "iz_int", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            int inIndex = mapName.IndexOf("_in", StringComparison.OrdinalIgnoreCase);
            if (inIndex > 0 && inIndex + 3 < mapName.Length && char.IsDigit(mapName[inIndex + 3])) {
                return true;
            }

            return mapName.Length >= 7 &&
                mapName.StartsWith("new_", StringComparison.OrdinalIgnoreCase) &&
                char.IsDigit(mapName[4]) &&
                mapName[5] == '-' &&
                mapName[6] == '2';
        }

        public bool IsDungeonMap(string mapRsw) {
            string normalized = NormalizeMapRswName(mapRsw);
            if (string.IsNullOrEmpty(normalized)) {
                return false;
            }

            string mapName = normalized.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(0, normalized.Length - 4)
                : normalized;
            if (mapName.IndexOf("_dun", StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }

            for (int i = 0; i < SkylessDungeonMapPrefixes.Length; i++) {
                if (mapName.StartsWith(SkylessDungeonMapPrefixes[i], StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        public bool TryResolveResourceName(string path, out string resolvedPath) {
            if (!string.IsNullOrEmpty(path) && resNameTable.TryGetValue(path, out resolvedPath)) {
                return true;
            }

            resolvedPath = path;
            return false;
        }

        public bool TryGetMapTable(string key, out MapTableStruct table) {
            if (!string.IsNullOrWhiteSpace(key) && mapTable.TryGetValue(key, out table)) {
                return true;
            }

            table = default;
            return false;
        }

        public bool TryGetTableProbe(string key, out TableProbe probe) {
            if (!string.IsNullOrWhiteSpace(key)) {
                for (int i = 0; i < tableProbes.Count; i++) {
                    if (string.Equals(tableProbes[i].Key, key, StringComparison.OrdinalIgnoreCase)) {
                        probe = tableProbes[i];
                        return true;
                    }
                }
            }

            probe = null;
            return false;
        }

        public string BuildSummary() {
            var builder = new StringBuilder();
            builder.Append("runtime tables: map=");
            builder.Append(mapTable.Count);
            builder.Append(", resname=");
            builder.Append(resNameTable.Count);
            builder.Append(", msgstring=");
            builder.Append(msgStringTable.Count);

            if (tableProbes.Count > 0) {
                builder.Append(", phase7 required=");
                builder.Append(LoadedRequiredPhase7TableCount);
                builder.Append('/');
                builder.Append(RequiredPhase7TableCount);

                int missing = MissingRequiredPhase7TableCount;
                if (missing > 0) {
                    builder.Append(" missing [");
                    AppendMissingRequiredProbeKeys(builder, 6);
                    builder.Append(']');
                }
            }

            return builder.ToString();
        }

        private void LoadResNameTable(RoAssetDatabase assets) {
            string data = TryReadText(assets, "data/resnametable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            foreach (object[] args in TableLoader.LoadTable(data, 2)) {
                resNameTable[Convert.ToString(args[1])] = Convert.ToString(args[2]);
            }
        }

        private void LoadMapNameTable(RoAssetDatabase assets) {
            string data = TryReadText(assets, "data/mapnametable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            foreach (object[] args in TableLoader.LoadTable(data, 2)) {
                string key = Convert.ToString(args[1]);
                if (!mapTable.TryGetValue(key, out MapTableStruct entry)) {
                    entry = new MapTableStruct();
                }

                entry.name = Convert.ToString(args[2]);
                mapTable[key] = entry;
            }
        }

        private void LoadMp3NameTable(RoAssetDatabase assets) {
            string data = TryReadText(assets, "data/mp3nametable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            foreach (object[] args in TableLoader.LoadTable(data, 2)) {
                string key = Convert.ToString(args[1]);
                if (!mapTable.TryGetValue(key, out MapTableStruct entry)) {
                    entry = new MapTableStruct();
                }

                entry.mp3 = Path.GetFileName(Convert.ToString(args[2]));
                mapTable[key] = entry;
            }
        }

        private void LoadMsgStringTable(RoAssetDatabase assets) {
            // Custom reads data\msgstringtable.csv: per line base64(MSI name),base64(text), line n = message n; the .txt
            // copy stops at 4 022 lines, so the newer messages (Game Settings...) exist only in the CSV
            if (LoadMsgStringCsv(TryReadText(assets, "data/msgstringtable.csv"))) {
                return;
            }

            string data = TryReadText(assets, "data/msgstringtable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            foreach (object[] args in TableLoader.LoadTable(data, 1)) {
                msgStringTable[Convert.ToString(args[0])] = Convert.ToString(args[1]);
            }
        }

        private bool LoadMsgStringCsv(string csv) {
            if (string.IsNullOrEmpty(csv)) {
                return false;
            }

            string[] lines = csv.Split('\n');
            int loaded = 0;
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i].TrimEnd('\r');
                int comma = line.IndexOf(',');
                if (comma < 0) {
                    continue;
                }

                try {
                    byte[] text = Convert.FromBase64String(line.Substring(comma + 1));
                    msgStringTable[i.ToString(System.Globalization.CultureInfo.InvariantCulture)] = RoTextEncoding.Decode(text);
                    loaded++;
                } catch (FormatException) {
                    // not a base64 table line
                }
            }

            return loaded > 0;
        }

        private void LoadPhase7TableProbes(RoAssetDatabase assets) {
            tableProbes.Clear();
            for (int i = 0; i < Phase7ProbeSpecs.Length; i++) {
                tableProbes.Add(ResolveProbe(assets, Phase7ProbeSpecs[i]));
            }
        }

        /// <summary>
        /// Parses data/viewpointtable.txt.
        /// Format per line (10 fields, '#'-separated, trailing '#' present):
        ///   mapname.rsw#range#scope#rangeIn#rotationFrom#rotationTo#rotationIn#altitudeFrom#altitudeTo#altitudeIn#
        /// Values in kRO world units / degrees.
        /// </summary>
        private void LoadViewpointTable(RoAssetDatabase assets) {
            string data = TryReadText(assets, "data/viewpointtable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            string[] lines = data.Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//")) {
                    continue;
                }

                string[] parts = line.Split('#');
                // Need at least 10 fields: key + 9 int values (trailing '#' produces an empty last element)
                if (parts.Length < 10) {
                    continue;
                }

                string key = parts[0].Trim();
                if (string.IsNullOrEmpty(key)) {
                    continue;
                }

                if (!int.TryParse(parts[1].Trim(), out int range))        { continue; }
                if (!int.TryParse(parts[2].Trim(), out int scope))        { continue; }
                if (!int.TryParse(parts[3].Trim(), out int rangeIn))      { continue; }
                if (!int.TryParse(parts[4].Trim(), out int rotFrom))      { continue; }
                if (!int.TryParse(parts[5].Trim(), out int rotTo))        { continue; }
                if (!int.TryParse(parts[6].Trim(), out int rotIn))        { continue; }
                if (!int.TryParse(parts[7].Trim(), out int altFrom))      { continue; }
                if (!int.TryParse(parts[8].Trim(), out int altTo))        { continue; }
                if (!int.TryParse(parts[9].Trim(), out int altIn))        { continue; }

                viewpointTable[key] = new RoViewpointPreset(
                    range, scope, rangeIn,
                    rotFrom, rotTo, rotIn,
                    altFrom, altTo, altIn
                );
            }
        }

        /// <summary>
        /// Parses data/indoorRswTable.txt.
        /// Format matches roBrowser table loading: first '#'-delimited field is the map filename.
        /// Maps in this list use roBrowser indoor camera constraints.
        /// </summary>
        private void LoadIndoorRswTable(RoAssetDatabase assets) {
            string data = TryReadText(assets, "data/indoorRswTable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            string[] lines = data.Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                string line = NormalizeIndoorRswTableEntry(lines[i]);
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }

                indoorMaps.Add(line);
            }
        }

        public static string NormalizeIndoorRswTableEntry(string rawLine) {
            if (string.IsNullOrWhiteSpace(rawLine)) {
                return null;
            }

            string line = rawLine.Trim();
            if (line.StartsWith("//")) {
                return null;
            }

            int separator = line.IndexOf('#');
            if (separator >= 0) {
                line = line.Substring(0, separator).Trim();
            }

            if (string.IsNullOrEmpty(line) || line.StartsWith("//")) {
                return null;
            }

            return line.EndsWith(".gat", StringComparison.OrdinalIgnoreCase)
                ? line.Substring(0, line.Length - 4) + ".rsw"
                : line;
        }

        private static string NormalizeMapRswName(string mapRsw) {
            if (string.IsNullOrWhiteSpace(mapRsw)) {
                return null;
            }

            string normalized = mapRsw.Trim().Replace('\\', '/');
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0) {
                normalized = normalized.Substring(slashIndex + 1);
            }

            if (normalized.EndsWith(".gat", StringComparison.OrdinalIgnoreCase)) {
                return normalized.Substring(0, normalized.Length - 4) + ".rsw";
            }

            if (normalized.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase)) {
                return normalized;
            }

            return normalized + ".rsw";
        }

        private void AppendMissingRequiredProbeKeys(StringBuilder builder, int maxKeys) {
            int appended = 0;
            int missing = 0;
            for (int i = 0; i < tableProbes.Count; i++) {
                TableProbe probe = tableProbes[i];
                if (!probe.RequiredForPhase7 || probe.IsAvailable) {
                    continue;
                }

                missing++;
                if (appended >= maxKeys) {
                    continue;
                }

                if (appended > 0) {
                    builder.Append(", ");
                }

                builder.Append(probe.Key);
                appended++;
            }

            if (missing > appended) {
                builder.Append(", +");
                builder.Append(missing - appended);
            }
        }

        private static string TryReadText(RoAssetDatabase assets, string path) {
            return assets.Exists(path) ? assets.ReadText(path) : null;
        }

        private static TableProbe ResolveProbe(RoAssetDatabase assets, TableProbeSpec spec) {
            for (int i = 0; i < spec.CandidatePaths.Length; i++) {
                string candidate = spec.CandidatePaths[i];
                if (!assets.Exists(candidate)) {
                    continue;
                }

                try {
                    byte[] bytes = assets.ReadBytes(candidate);
                    return new TableProbe(spec.Key, spec.Purpose, spec.RequiredForPhase7, spec.CandidatePaths, candidate, bytes?.Length ?? 0, null);
                } catch (Exception ex) {
                    return new TableProbe(spec.Key, spec.Purpose, spec.RequiredForPhase7, spec.CandidatePaths, candidate, 0, ex.Message);
                }
            }

            return new TableProbe(spec.Key, spec.Purpose, spec.RequiredForPhase7, spec.CandidatePaths, null, 0, null);
        }

        private static TableProbeSpec Required(string key, string purpose, string[] candidatePaths) {
            return new TableProbeSpec(key, purpose, true, candidatePaths);
        }

        private static TableProbeSpec Optional(string key, string purpose, string[] candidatePaths) {
            return new TableProbeSpec(key, purpose, false, candidatePaths);
        }

        private static string[] LuaDataInfo(params string[] names) {
            return LuaFolder("datainfo", names);
        }

        private static string[] LuaSkillInfo(params string[] names) {
            return LuaFolder("skillinfoz", names);
        }

        private static string[] LuaStateIcon(params string[] names) {
            return LuaFolder("stateicon", names);
        }

        private static string[] LuaRoot(params string[] names) {
            var paths = new List<string>();
            for (int i = 0; i < names.Length; i++) {
                AddLuaPathVariants(paths, "data/luafiles514/lua files", null, names[i]);
                AddLuaPathVariants(paths, "data/lua files", null, names[i]);
            }

            return paths.ToArray();
        }

        private static string[] LuaFolder(string folder, params string[] names) {
            var paths = new List<string>();
            for (int i = 0; i < names.Length; i++) {
                AddLuaPathVariants(paths, "data/luafiles514/lua files", folder, names[i]);
                AddLuaPathVariants(paths, "data/lua files", folder, names[i]);
            }

            return paths.ToArray();
        }

        private static string[] ItemInfoCandidates() {
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

        private static string[] MobDbCandidates() {
            return new[] {
                "db/re/mob_db.yml",
                "db/re/mob_db.yaml",
                "db/pre-re/mob_db.yml",
                "db/pre-re/mob_db.yaml",
                "db/mob_db.yml",
                "db/mob_db.yaml",
                "data/db/re/mob_db.yml",
                "data/db/re/mob_db.yaml",
                "data/db/mob_db.yml",
                "data/db/mob_db.yaml",
                "mob_db.yml",
                "mob_db.yaml",
            };
        }

        private static void AddLuaPathVariants(List<string> paths, string root, string folder, string name) {
            string prefix = string.IsNullOrEmpty(folder) ? root : root + "/" + folder;
            paths.Add(prefix + "/" + name + ".lub");
            paths.Add(prefix + "/" + name + ".lua");
        }
    }
}
