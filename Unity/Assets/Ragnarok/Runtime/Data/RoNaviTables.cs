using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    /// <summary>A navigation object's type, as the original's result rows and targets use it (0 cell, 1 map, 2 NPC, 3 mob).</summary>
    public enum RoNaviKind {
        Cell = 0,
        Map = 1,
        Npc = 2,
        Mob = 3,
    }

    /// <summary>Navi_Map row: { mapId, name, type (5001 field / 5003 indoor), width, height }.</summary>
    public sealed class RoNaviMap {
        public string Id;
        public string Name;
        public int Type, Width, Height;
    }

    /// <summary>Navi_Npc row: { map, npcIndex, type, spriteId, name, extra, x, y }.</summary>
    public sealed class RoNaviNpc {
        public string Map;
        public int Index, Type, Sprite;
        public string Name, Extra;
        public int X, Y;
    }

    /// <summary>Navi_Mob row: { map, index, type (300 normal / 301 boss), packedCount, name, spriteName, level, packedInfo }.</summary>
    public sealed class RoNaviMob {
        public string Map;
        public int Index, Type, Packed;
        public string Name, Sprite;
        public int Level, Info;

        /// <summary>The spawn count (the high word of the packed field; UNCONFIRMED decoding).</summary>
        public int Count => (Packed >> 16) & 0xffff;

        public bool IsBoss => Type == 301;
    }

    /// <summary>
    /// Navi_Link row: { map, linkId, type, npcId (99999 none), name, extra, x, y, destMap, destX, destY }; type 200 warp,
    /// 201..205 NPC services (204 Kafra and 202 / 203 need the zeny flag, 205 the airship flag), 400 an item step.
    /// </summary>
    public sealed class RoNaviLink {
        public string Map;
        public int Id, Type, NpcId;
        public string Name, Extra;
        public int X, Y;
        public string DestMap;
        public int DestX, DestY;

        public bool IsWarp => Type == 200;
        public bool IsItem => Type == 400;
        public bool IsAirship => Type == 205;
        public bool IsService => Type >= 201 && Type <= 204;
    }

    /// <summary>A search hit or a target: its kind, map and cell, name, and the row it came from.</summary>
    public sealed class RoNaviObject {
        public RoNaviKind Kind;
        public string Map;
        public int X, Y;
        public string Name;
        public RoNaviNpc Npc;
        public RoNaviMob Mob;
        public int Count;   // a mob's spawn count on the map (rows of one name merged)
    }

    /// <summary>One result row (DAT_01517c58[i]): the objects of one name; the row shows "[count]name of the first".</summary>
    public sealed class RoNaviGroup {
        public readonly List<RoNaviObject> Members = new List<RoNaviObject>();
        public RoNaviKind Kind => Members.Count > 0 ? Members[0].Kind : RoNaviKind.Map;
        public string Name => Members.Count > 0 ? Members[0].Name : string.Empty;
    }

    /// <summary>
    /// The navigation tables (navigation-spec §3): Navi_Map / Npc / Mob / Link, Navi_Distance (link to link cells: "P" to the
    /// links of the same map, "E" from the link's arrival to the links of its destination map) and Navi_NpcDistance (an
    /// arrival link to an NPC). Loaded once per asset database on a worker thread; the search splits the query on spaces
    /// ($$all, $$lvN, $$lvN~M), the route is a least-cost search over the links.
    /// </summary>
    public sealed class RoNaviTables {
        public readonly List<RoNaviMap> Maps = new List<RoNaviMap>();
        public readonly List<RoNaviNpc> Npcs = new List<RoNaviNpc>();
        public readonly List<RoNaviMob> Mobs = new List<RoNaviMob>();
        public readonly List<RoNaviLink> Links = new List<RoNaviLink>();
        private readonly Dictionary<string, RoNaviMap> mapsById = new Dictionary<string, RoNaviMap>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<RoNaviLink>> linksByMap = new Dictionary<string, List<RoNaviLink>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, RoNaviLink> linksById = new Dictionary<int, RoNaviLink>();
        private readonly Dictionary<int, List<(bool Pass, int Other, int Cells)>> distances = new Dictionary<int, List<(bool, int, int)>>();
        private readonly Dictionary<int, Dictionary<int, int>> npcDistances = new Dictionary<int, Dictionary<int, int>>();

        private static RoAssetDatabase loadingFor;
        private static Task<RoNaviTables> loading;

        /// <summary>The tables of <paramref name="assets"/>, starting their load on a worker thread the first time; null until ready.</summary>
        public static RoNaviTables Get(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) return null;
            if (!ReferenceEquals(loadingFor, assets) || loading == null) {
                loadingFor = assets;
                var files = new List<RoWorldMapLuaFile>();
                var diagnostics = new List<string>();
                foreach (string name in new[] { "navi_map_krpri", "navi_npc_krpri", "navi_mob_krpri", "navi_link_krpri", "navi_linkdistance_krpri", "navi_npcdistance_krpri" }) {
                    RoNavigationDataLoader.TryLoadLogicalFile(assets, name, files, diagnostics);
                }

                loading = Task.Run(() => Parse(files));
            }

            return loading.IsCompleted && !loading.IsFaulted ? loading.Result : null;
        }

        /// <summary>The load failed (no tables; navigation answers "No Information").</summary>
        public static bool Failed => loading != null && loading.IsFaulted;

        public static RoNaviTables ParseForTests(IReadOnlyList<RoWorldMapLuaFile> files) => Parse(files);

        internal static RoNaviTables Parse(IReadOnlyList<RoWorldMapLuaFile> files) {
            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(files);
            var t = new RoNaviTables();
            foreach (LuaTableValue row in Rows(context.GetGlobal("Navi_Map"))) {
                var map = new RoNaviMap { Id = Str(row, 0), Name = Str(row, 1), Type = Int(row, 2), Width = Int(row, 3), Height = Int(row, 4) };
                if (map.Id.Length == 0) continue;
                t.Maps.Add(map);
                t.mapsById[map.Id] = map;
            }

            foreach (LuaTableValue row in Rows(context.GetGlobal("Navi_Npc"))) {
                t.Npcs.Add(new RoNaviNpc {
                    Map = Str(row, 0), Index = Int(row, 1), Type = Int(row, 2), Sprite = Int(row, 3), Name = Str(row, 4), Extra = Str(row, 5),
                    X = Int(row, 6), Y = Int(row, 7),
                });
            }

            foreach (LuaTableValue row in Rows(context.GetGlobal("Navi_Mob"))) {
                t.Mobs.Add(new RoNaviMob {
                    Map = Str(row, 0), Index = Int(row, 1), Type = Int(row, 2), Packed = Int(row, 3), Name = Str(row, 4), Sprite = Str(row, 5),
                    Level = Int(row, 6), Info = Int(row, 7),
                });
            }

            foreach (LuaTableValue row in Rows(context.GetGlobal("Navi_Link"))) {
                var link = new RoNaviLink {
                    Map = Str(row, 0), Id = Int(row, 1), Type = Int(row, 2), NpcId = Int(row, 3), Name = Str(row, 4), Extra = Str(row, 5),
                    X = Int(row, 6), Y = Int(row, 7), DestMap = Str(row, 8), DestX = Int(row, 9), DestY = Int(row, 10),
                };
                t.Links.Add(link);
                t.linksById[link.Id] = link;
                if (!t.linksByMap.TryGetValue(link.Map, out List<RoNaviLink> list)) t.linksByMap[link.Map] = list = new List<RoNaviLink>();
                list.Add(link);
            }

            // Navi_Distance = { map, count, { { linkId, { "P"|"E", other, cells }, ..., { "NULL", 0, 0 } }, ... }, map, ... }
            foreach (LuaTableValue entry in MapBlocks(context.GetGlobal("Navi_Distance"))) {
                int from = Int(entry, 0);
                var list = new List<(bool, int, int)>();
                for (int i = 1; i < entry.ArrayCount; i++) {
                    LuaValue pair = entry.ArrayValues[i];
                    if (pair.Kind != LuaValueKind.Table) continue;
                    string kind = Str(pair.Table, 0);
                    if (kind == "P" || kind == "E") list.Add((kind == "P", Int(pair.Table, 1), Int(pair.Table, 2)));
                }

                t.distances[from] = list;
            }

            // Navi_NpcDistance = { map, count, { { npcIndex, { linkMap, linkId, cells }, ..., { "", 0, 0 } }, ... }, ... }
            foreach (LuaTableValue entry in MapBlocks(context.GetGlobal("Navi_NpcDistance"))) {
                int npc = Int(entry, 0);
                var byLink = new Dictionary<int, int>();
                for (int i = 1; i < entry.ArrayCount; i++) {
                    LuaValue pair = entry.ArrayValues[i];
                    if (pair.Kind != LuaValueKind.Table) continue;
                    int link = Int(pair.Table, 1);
                    if (link != 0) byLink[link] = Int(pair.Table, 2);
                }

                t.npcDistances[npc] = byLink;
            }

            return t;
        }

        private static IEnumerable<LuaTableValue> Rows(LuaValue value) {
            if (value == null || value.Kind != LuaValueKind.Table) yield break;
            foreach (LuaValue row in value.Table.ArrayValues) {
                if (row.Kind == LuaValueKind.Table) yield return row.Table;
            }
        }

        /// <summary>The per-link entries of a { map, count, { entries } , ... } list.</summary>
        private static IEnumerable<LuaTableValue> MapBlocks(LuaValue value) {
            if (value == null || value.Kind != LuaValueKind.Table) yield break;
            foreach (LuaValue block in value.Table.ArrayValues) {
                if (block.Kind != LuaValueKind.Table) continue;
                foreach (LuaValue entry in block.Table.ArrayValues) {
                    if (entry.Kind == LuaValueKind.Table) yield return entry.Table;
                }
            }
        }

        private static string Str(LuaTableValue row, int i) => i < row.ArrayCount ? row.ArrayValues[i].ToDisplayString() : string.Empty;

        private static int Int(LuaTableValue row, int i) => i < row.ArrayCount ? row.ArrayValues[i].ToInt() : 0;

        public bool TryGetMap(string id, out RoNaviMap map) => mapsById.TryGetValue(id ?? string.Empty, out map);

        public bool HasMap(string id) => id != null && mapsById.ContainsKey(id);

        public IReadOnlyList<RoNaviLink> LinksOn(string map) =>
            map != null && linksByMap.TryGetValue(map, out List<RoNaviLink> list) ? list : (IReadOnlyList<RoNaviLink>) Array.Empty<RoNaviLink>();

        public RoNaviNpc FindNpcAt(string map, int x, int y) {
            foreach (RoNaviNpc npc in Npcs) {
                if (npc.X == x && npc.Y == y && string.Equals(npc.Map, map, StringComparison.OrdinalIgnoreCase)) return npc;
            }

            return null;
        }

        // ---------------------------------------------------------------- search (0xac3d20 → 0xab0310)

        /// <summary>
        /// The groups for <paramref name="query"/> under the type filter (0 All, 1 Map, 2 Npc, 3 Mob): "$$all" everything,
        /// "$$lvN" / "$$lvN~M" monsters of that level, else every word (split on spaces) a case-insensitive part of the name
        /// (the exact match rule is UNCONFIRMED). One group per map, per NPC name and per monster name.
        /// </summary>
        public List<RoNaviGroup> Search(string query, int filter, Func<string, string> mapName) {
            var groups = new List<RoNaviGroup>();
            query = (query ?? string.Empty).Trim();
            if (query.Length == 0) return groups;
            bool all = false;
            int minLevel = -1, maxLevel = -1;
            string[] words = Array.Empty<string>();
            if (query.StartsWith("$$", StringComparison.Ordinal)) {
                string rest = query.Substring(2).Trim().ToLowerInvariant();
                if (rest == "all") {
                    all = true;
                } else if (rest.StartsWith("lv", StringComparison.Ordinal)) {
                    string[] range = rest.Substring(2).Split('~');
                    if (!int.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minLevel)) return groups;
                    maxLevel = minLevel;
                    if (range.Length > 1 && !int.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxLevel)) return groups;
                } else {
                    return groups;
                }
            } else {
                words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }

            bool Matches(string a, string b) {
                if (all) return true;
                foreach (string word in words) {
                    if ((a == null || a.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0) &&
                        (b == null || b.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)) return false;
                }

                return true;
            }

            bool levels = minLevel >= 0;
            if (!levels && (filter == 0 || filter == 1)) {
                foreach (RoNaviMap map in Maps) {
                    string shown = mapName != null ? mapName(map.Id) : map.Name;
                    if (!Matches(shown, map.Id)) continue;
                    var group = new RoNaviGroup();
                    group.Members.Add(new RoNaviObject { Kind = RoNaviKind.Map, Map = map.Id, Name = shown });
                    groups.Add(group);
                }
            }

            if (!levels && (filter == 0 || filter == 2)) {
                var byName = new Dictionary<string, RoNaviGroup>(StringComparer.Ordinal);
                foreach (RoNaviNpc npc in Npcs) {
                    if (string.IsNullOrEmpty(npc.Name) || !Matches(npc.Name, null)) continue;
                    if (!byName.TryGetValue(npc.Name, out RoNaviGroup group)) {
                        byName[npc.Name] = group = new RoNaviGroup();
                        groups.Add(group);
                    }

                    group.Members.Add(new RoNaviObject { Kind = RoNaviKind.Npc, Map = npc.Map, X = npc.X, Y = npc.Y, Name = npc.Name, Npc = npc });
                }
            }

            if (filter == 0 || filter == 3) {
                var byName = new Dictionary<string, RoNaviGroup>(StringComparer.Ordinal);
                foreach (RoNaviMob mob in Mobs) {
                    if (string.IsNullOrEmpty(mob.Name)) continue;
                    if (levels ? mob.Level < minLevel || mob.Level > maxLevel : !Matches(mob.Name, mob.Sprite)) continue;
                    if (!byName.TryGetValue(mob.Name, out RoNaviGroup group)) {
                        byName[mob.Name] = group = new RoNaviGroup();
                        groups.Add(group);
                    }

                    // several spawn rows of one monster on one map are one member
                    RoNaviObject member = group.Members.Find(m => string.Equals(m.Map, mob.Map, StringComparison.OrdinalIgnoreCase));
                    if (member == null) group.Members.Add(member = new RoNaviObject { Kind = RoNaviKind.Mob, Map = mob.Map, Name = mob.Name, Mob = mob });
                    member.Count += mob.Count;
                }
            }

            return groups;
        }

        // ---------------------------------------------------------------- route (ab11f0)

        /// <summary>A link may be used: warps always, Kafra-type services with the zeny flag, the airship with the plane flag, items with the scroll flag.</summary>
        public static bool LinkAllowed(RoNaviLink link, bool zeny, bool scroll, bool plane) {
            if (link.IsItem) return scroll;
            if (link.IsAirship) return plane;
            if (link.Type >= 202 && link.Type <= 204) return zeny;
            return true;
        }

        /// <summary>
        /// The least-cost link chain from (<paramref name="startMap"/>, sx, sy) to the target's map: seeds are the links of
        /// the start map (cost = the cells to them), each taken link reaches its destination map where "E" gives the cells
        /// to the next links; the final cost adds the arrival to the target (NpcDistance for an NPC, the cells for a cell,
        /// 0 for a map or a monster). False when no chain exists (−96 "Failed to find a path"). An empty chain means the
        /// target is on the start map.
        /// </summary>
        public bool FindRoute(string startMap, int sx, int sy, RoNaviObject target, bool zeny, bool scroll, bool plane, List<RoNaviLink> route) {
            route.Clear();
            if (target == null || string.IsNullOrEmpty(target.Map)) return false;
            if (string.Equals(startMap, target.Map, StringComparison.OrdinalIgnoreCase)) return true;

            // layered: the fewest map changes first (a map is entered once, at its first layer), then the fewest cells;
            // a key is hops · 2^20 + cells (the generated "E" rows hold some impossible distances, which a plain cell
            // cost would follow through loops of indoor maps)
            const long Layer = 1L << 20;
            var cost = new Dictionary<int, long>();
            var previous = new Dictionary<int, int>();
            var heap = new List<(long Cost, int Link)>();
            foreach (RoNaviLink link in LinksOn(startMap)) {
                if (!LinkAllowed(link, zeny, scroll, plane)) continue;
                Push(heap, cost, previous, link.Id, Layer + Math.Max(Math.Abs(link.X - sx), Math.Abs(link.Y - sy)), -1);
            }

            long best = long.MaxValue;
            int bestLink = -1;
            var closed = new HashSet<int>();
            var entered = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startMap };
            while (heap.Count > 0) {
                (long c, int id) = Pop(heap);
                if (c / Layer > best / Layer) break;
                if (!closed.Add(id) || !linksById.TryGetValue(id, out RoNaviLink link)) continue;
                if (string.Equals(link.DestMap, target.Map, StringComparison.OrdinalIgnoreCase)) {
                    long total = c + FinalCost(link, target);
                    if (total < best) {
                        best = total;
                        bestLink = id;
                    }

                    continue;
                }

                if (!entered.Add(link.DestMap)) continue;
                List<(bool Pass, int Other, int Cells)> next = null;
                distances.TryGetValue(id, out next);
                foreach (RoNaviLink to in LinksOn(link.DestMap)) {
                    if (closed.Contains(to.Id) || !LinkAllowed(to, zeny, scroll, plane) || entered.Contains(to.DestMap) && !string.Equals(to.DestMap, target.Map, StringComparison.OrdinalIgnoreCase)) continue;
                    // the "E" row's cells from this arrival to that link, else the straight distance
                    int cells = Math.Max(Math.Abs(to.X - link.DestX), Math.Abs(to.Y - link.DestY));
                    if (next != null) {
                        foreach ((bool pass, int other, int rowCells) in next) {
                            if (!pass && other == to.Id) {
                                cells = Math.Max(cells, rowCells);
                                break;
                            }
                        }
                    }

                    Push(heap, cost, previous, to.Id, c + Layer + cells, id);
                }
            }

            if (bestLink < 0) return false;
            for (int id = bestLink; id >= 0; id = previous.TryGetValue(id, out int p) ? p : -1) route.Add(linksById[id]);
            route.Reverse();
            return true;
        }

        private int FinalCost(RoNaviLink arrival, RoNaviObject target) {
            switch (target.Kind) {
                case RoNaviKind.Npc:
                    if (target.Npc != null && npcDistances.TryGetValue(target.Npc.Index, out Dictionary<int, int> byLink) && byLink.TryGetValue(arrival.Id, out int cells)) return cells;
                    return Math.Max(Math.Abs(target.X - arrival.DestX), Math.Abs(target.Y - arrival.DestY));
                case RoNaviKind.Cell:
                    return Math.Max(Math.Abs(target.X - arrival.DestX), Math.Abs(target.Y - arrival.DestY));
                default:
                    return 0;
            }
        }

        private static void Push(List<(long Cost, int Link)> heap, Dictionary<int, long> cost, Dictionary<int, int> previous, int link, long c, int from) {
            if (cost.TryGetValue(link, out long known) && known <= c) return;
            cost[link] = c;
            previous[link] = from;
            heap.Add((c, link));
            for (int i = heap.Count - 1; i > 0;) {
                int parent = (i - 1) / 2;
                if (heap[parent].Cost <= heap[i].Cost) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        private static (long Cost, int Link) Pop(List<(long Cost, int Link)> heap) {
            (long, int) top = heap[0];
            heap[0] = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            for (int i = 0; ;) {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < heap.Count && heap[l].Cost < heap[m].Cost) m = l;
                if (r < heap.Count && heap[r].Cost < heap[m].Cost) m = r;
                if (m == i) break;
                (heap[m], heap[i]) = (heap[i], heap[m]);
                i = m;
            }

            return top;
        }
    }
}
