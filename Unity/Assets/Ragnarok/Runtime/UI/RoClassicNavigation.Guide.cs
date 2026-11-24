using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Entities;
using Ragnarok.Maps;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.UI {
    /// <summary>
    /// The guidance (navigation-spec §3.3, §4): the target, the link route and the step solver 0xabcae0; the ground path
    /// 0xac0ff0 (every second path cell within 10 cells: navi_grid{set}_{dir}.tga at alpha 0x99, the cell whose
    /// index % 10 is the current 100-ms slot at 0xee; location1..4.tga on the last cell, a frame per 150 ms, light blue
    /// 0xee64aaff or white on the final map), re-pathed when no path cell is near; the messages 0xabffa0.
    /// </summary>
    public sealed partial class RoClassicNavigation {
        private const int StateIdle = 1, StateGuiding = 2;
        private const int Window = 10;
        private const float TileLift = 0.04f;

        private sealed class PendingStart {
            public string Map;
            public int Mode, Flags, X, Y, MobId;
            public bool Hide;
        }

        private int state;
        private bool hidden;
        private int iconSet = 1;
        private RoNaviObject target;
        private readonly List<RoNaviLink> route = new List<RoNaviLink>();
        private int step;
        private bool zeny = true, scroll, plane = true;
        private readonly List<Vector2Int> path = new List<Vector2Int>();
        private bool drawGround, onFinalMap;
        private string currentMap;
        private string lastMessage = string.Empty;
        private PendingStart pendingStart;
        private RoOnlineWorldController world;
        private int worldLookupFrame = -100;

        // ---------------------------------------------------------------- starting (0x1aa, ac0830)

        /// <summary>0x1aa: the selection becomes the target (−92 "Please specify target goals." without one); the box's zeny / plane flags.</summary>
        private void StartFromSelection() {
            RoNaviObject sel = Selected;
            if (sel == null) {
                Msg(-92);
                return;
            }

            target = sel;
            zeny = plane = service;
            scroll = false;
            BeginRoute();
        }

        /// <summary>
        /// ac0830(map, mode, flags, hide, x, y, mobId): mode 0 a cell, 1 a map, 2 the NPC at (x, y), 3 a monster of the map;
        /// flags bit0 zeny, bit1 scroll, bit2 plane; hide = no window. Unknown objects answer −94 "No Information".
        /// </summary>
        internal void Navigate(string map, int mode, int flags, bool hide, int x, int y, int mobId) {
            RoNaviTables tables = Tables();
            if (tables == null) {
                pendingStart = new PendingStart { Map = map, Mode = mode, Flags = flags, Hide = hide, X = x, Y = y, MobId = mobId };
                return;
            }

            map = (map ?? string.Empty).Trim().ToLowerInvariant();
            RoNaviObject next = null;
            switch (mode) {
                case 0:
                    if (tables.HasMap(map)) next = new RoNaviObject { Kind = RoNaviKind.Cell, Map = map, X = x, Y = y, Name = MapName(map) + " (" + x + "," + y + ")" };
                    break;
                case 1:
                    if (tables.HasMap(map)) next = new RoNaviObject { Kind = RoNaviKind.Map, Map = map, Name = MapName(map) };
                    break;
                case 2: {
                    RoNaviNpc npc = tables.FindNpcAt(map, x, y);
                    if (npc != null) next = new RoNaviObject { Kind = RoNaviKind.Npc, Map = npc.Map, X = npc.X, Y = npc.Y, Name = npc.Name, Npc = npc };
                    break;
                }
                case 3:
                    foreach (RoNaviMob mob in tables.Mobs) {
                        if (!string.Equals(mob.Map, map, StringComparison.OrdinalIgnoreCase)) continue;
                        next = new RoNaviObject { Kind = RoNaviKind.Mob, Map = mob.Map, Name = mob.Name, Mob = mob };
                        break;
                    }

                    break;
            }

            if (next == null) {
                Msg(-94);
                return;
            }

            target = next;
            zeny = (flags & 1) != 0;
            scroll = (flags & 2) != 0;
            plane = (flags & 4) != 0;
            hidden = hide;
            BeginRoute();
            if (!hide) Open();
        }

        /// <summary>abf3c0: within 1 cell of a target on this map → 0xb; else the link route (−96 when none), then the step solver.</summary>
        private void BeginRoute() {
            RoNaviTables tables = Tables();
            if (tables == null || target == null) return;
            string map = CurrentMapName();
            if (map == null || !TryPlayerCell(out Vector2Int me)) {
                Msg(-93);
                return;
            }

            currentMap = map;
            if (string.Equals(map, target.Map, StringComparison.OrdinalIgnoreCase) && (target.Kind == RoNaviKind.Cell || target.Kind == RoNaviKind.Npc) &&
                Math.Max(Math.Abs(me.x - target.X), Math.Abs(me.y - target.Y)) <= 1) {
                Msg(11);
                return;
            }

            if (!tables.FindRoute(map, me.x, me.y, target, zeny, scroll, plane, route)) {
                Msg(-96);
                return;
            }

            routeStamp++;

            state = StateGuiding;
            page = 1;
            Solve();
            Relayout();
        }

        // ---------------------------------------------------------------- the step solver (abcae0)

        private void Solve() {
            RoNaviTables tables = Tables();
            if (tables == null || target == null) return;
            path.Clear();
            if (!tables.HasMap(currentMap) || !TryPlayerCell(out Vector2Int me)) {
                drawGround = false;
                Msg(-93);
                return;
            }

            if (string.Equals(currentMap, target.Map, StringComparison.OrdinalIgnoreCase)) {
                if (target.Kind == RoNaviKind.Map) {
                    Arrive(6);
                    return;
                }

                if (target.Kind == RoNaviKind.Mob) {
                    Arrive(8);
                    return;
                }

                if (me.x == target.X && me.y == target.Y) {
                    Arrive(11);
                    return;
                }

                BuildPath(me, new Vector2Int(target.X, target.Y));
                drawGround = true;
                onFinalMap = true;
                state = StateGuiding;
                Msg(target.Kind == RoNaviKind.Cell ? 9 : 7);
                return;
            }

            // the route's link on this map, searched from the last step back; a map off the route routes again from here
            int k = -1;
            for (int i = route.Count - 1; i >= 0; i--) {
                if (string.Equals(route[i].Map, currentMap, StringComparison.OrdinalIgnoreCase)) {
                    k = i;
                    break;
                }
            }

            if (k < 0) {
                if (!tables.FindRoute(currentMap, me.x, me.y, target, zeny, scroll, plane, route) || route.Count == 0) {
                    drawGround = false;
                    Msg(-96);
                    return;
                }

                k = 0;
            }

            step = k;
            RoNaviLink link = route[k];
            BuildPath(me, new Vector2Int(link.X, link.Y));
            drawGround = true;
            onFinalMap = false;
            state = StateGuiding;
            Msg(link.IsItem ? 4 : 5, link);
        }

        /// <summary>ac8970: the target is cleared, page 0, idle; the code's message.</summary>
        private void Arrive(int code) {
            Msg(code);
            ClearTarget();
            page = 0;
            Relayout();
        }

        /// <summary>abc910: the guidance stops (state idle, the path cleared); the window stays.</summary>
        private void CancelGuidance() {
            ClearTarget();
        }

        private void ClearTarget() {
            routeStamp++;
            target = null;
            route.Clear();
            path.Clear();
            drawGround = false;
            onFinalMap = false;
            if (state == StateGuiding) state = StateIdle;
            HideTiles();
        }

        /// <summary>The walk path on the current map (through the portal cell when the target is one), else a straight segment.</summary>
        private static bool TryBuildCurrentMapPath(
            RoRuntimeMapController runtimeMap,
            Vector2Int currentCell,
            Vector2Int targetCell,
            List<Vector2Int> output
        ) {
            output.Clear();
            if (currentCell == targetCell) {
                output.Add(currentCell);
                return true;
            }

            if (runtimeMap != null &&
                runtimeMap.GridSelector != null &&
                runtimeMap.GridSelector.Altitude != null) {
                var altitude = runtimeMap.GridSelector.Altitude;
                if (RoClickToMoveController.TryResolvePortalMoveCell(altitude, currentCell, targetCell, null, output, out Vector2Int moveCell) &&
                    RoGridPathfinder.TryFindPath(altitude, currentCell, moveCell, output)) {
                    return true;
                }

                if (RoGridPathfinder.TryFindNearestWalkable(altitude, targetCell, 8, out Vector2Int walkableTarget) &&
                    RoGridPathfinder.TryFindPath(altitude, currentCell, walkableTarget, output)) {
                    return true;
                }
            }

            output.Add(currentCell);
            output.Add(targetCell);
            return false;
        }

        private void BuildPath(Vector2Int from, Vector2Int to) {
            path.Clear();
            RoRuntimeMapController runtime = World()?.RuntimeMapController;
            TryBuildCurrentMapPath(runtime, from, to, path);
        }

        /// <summary>Per frame: a map change re-solves (and shows the window unless hidden); the arrival; off-path re-pathing; the tiles.</summary>
        private void TickGuidance() {
            if (state != StateGuiding || target == null) {
                HideTiles();
                return;
            }

            string map = CurrentMapName();
            if (map == null) {
                HideTiles();
                return;
            }

            if (!string.Equals(map, currentMap, StringComparison.Ordinal)) {
                currentMap = map;
                foreach (Tile tile in tiles) tile.Cell = new Vector2Int(int.MinValue, int.MinValue);
                Solve();
                if (!hidden && target != null) Open();
                return;
            }

            if (!drawGround || !TryPlayerCell(out Vector2Int me)) {
                HideTiles();
                return;
            }

            if (onFinalMap && (target.Kind == RoNaviKind.Cell || target.Kind == RoNaviKind.Npc) &&
                Math.Max(Math.Abs(me.x - target.X), Math.Abs(me.y - target.Y)) <= 1) {
                Arrive(10);
                return;
            }

            bool near = false;
            foreach (Vector2Int c in path) {
                if (Math.Abs(c.x - me.x) < Window && Math.Abs(c.y - me.y) < Window) {
                    near = true;
                    break;
                }
            }

            if (!near && path.Count > 0) BuildPath(me, path[path.Count - 1]);
            DrawTiles(me);
        }

        // ---------------------------------------------------------------- messages (abffa0)

        private static readonly Dictionary<int, (int Id, string Text)> Messages = new Dictionary<int, (int, string)> {
            { 0, (0x8ff, "Found") }, { 1, (0x900, "Directions were started") }, { 2, (0x901, "") }, { 3, (0x902, "") },
            { 4, (0x903, "Please navigate using the item") }, { 5, (0x904, "To find the location, follow the arrows") },
            { 6, (0x905, "Arrived at the target map") }, { 7, (0x906, "") }, { 8, (0x907, "") },
            { 9, (0x909, "Please go to indicated direction.") }, { 10, (0x908, "You have reached your goal") },
            { 11, (0x90a, "The goal has been reached") },
            { -100, (0x8f6, "The purpose is unclear") }, { -99, (0x8f7, "") }, { -98, (0x8f8, "") },
            { -97, (0x8f9, "Failed to set info for location!") }, { -96, (0x8fa, "Failed to find a path") }, { -95, (0x8fb, "") },
            { -94, (0x8fc, "No Information") }, { -93, (0x8fd, "Map doesn't support directions") }, { -92, (0x8fe, "Please specify target goals.") },
        };

        /// <summary>
        /// abffa0(code, mode): the code's MsgStr as "Navigation >: %s" (MsgStr 0x90b) in the chat in #FFB062 (mode 0 / 1) and in
        /// a message box (mode 0 / 2); code 5 with a step adds "Talk to [npc]" (+ "Please move to [map] map") for a service,
        /// "Please move to the Airship" for an airship, else "Use the Warp Portal to move to the next area.".
        /// </summary>
        private void Msg(int code, RoNaviLink link = null, int mode = 1) {
            string text = Messages.TryGetValue(code, out (int Id, string Text) m) ? RoClassicUiHost.MsgStr(m.Id, m.Text.Length > 0 ? m.Text : "NoMsg.") : "NoMsg.";
            lastMessage = text;
            string line = Format(RoClassicUiHost.MsgStr(0x90b, "Navigation >: %s"), text);
            if (mode != 2) Chat(line);
            if (mode != 1) RoClassicMessageBox.Show(text, RoClassicMessageBox.TypeOk, true, _ => { }, true, "Message");
            if (code != 5 || link == null || mode == 2) return;
            if (link.IsService) {
                Chat(Format(RoClassicUiHost.MsgStr(0x90c, "Navigation >: Talk to [%s]"), link.Name));
                if (!string.IsNullOrEmpty(link.DestMap)) Chat(Format(RoClassicUiHost.MsgStr(0x90d, "Navigation >: Please move to [%s] map"), MapName(link.DestMap)));
            } else if (link.IsAirship) {
                Chat(Format(RoClassicUiHost.MsgStr(0x90c, "Navigation >: Talk to [%s]"), link.Name));
                Chat(RoClassicUiHost.MsgStr(0x90f, "Navigation >: Please move to the Airship"));
            } else {
                Chat(RoClassicUiHost.MsgStr(0x910, "Navigation >: Use the Warp Portal to move to the next area."));
            }
        }

        private static string Format(string pattern, string value) {
            int at = pattern.IndexOf("%s", StringComparison.Ordinal);
            return at < 0 ? pattern + value : pattern.Substring(0, at) + value + pattern.Substring(at + 2);
        }

        private static void Chat(string line) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(line, ChatColor);
        }

        // ---------------------------------------------------------------- chat commands (0xced690 → 0xc07a00)

        /// <summary>
        /// "/navi map x/y" (also /navigation; x and y in one token, each below 501) → a cell target with flags 5 and no window;
        /// "/navi2 map x/y a/b/c" (a scroll, b zeny, c plane, each 0 or 1); "/naviopen" opens the window. A wrong argument
        /// answers −100 "The purpose is unclear". False when the line is not one of them.
        /// </summary>
        public static bool TryCommand(string line) {
            if (string.IsNullOrEmpty(line) || line[0] != '/') return false;
            string[] t = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = t[0].ToLowerInvariant();
            RoClassicNavigation n;
            switch (cmd) {
                case "/naviopen":
                    if ((n = Ensure()) != null) n.Open();
                    return true;
                case "/navi":
                case "/navigation":
                    if (t.Length < 3 || (n = Ensure()) == null) return false;
                    if (!TryCoords(t[2], out int x, out int y)) n.Msg(-100);
                    else n.Navigate(t[1], 0, 5, true, x, y, 1002);
                    return true;
                case "/navi2":
                case "/navigation2":
                    if (t.Length < 4 || (n = Ensure()) == null) return false;
                    if (!TryCoords(t[2], out int x2, out int y2) || !TryFlags(t[3], out int flags)) n.Msg(-100);
                    else n.Navigate(t[1], 0, flags, true, x2, y2, 1002);
                    return true;
            }

            return false;
        }

        /// <summary>abb150: "x/y" split at the first '/', each atol'd and below 501.</summary>
        private static bool TryCoords(string token, out int x, out int y) {
            x = y = -1;
            int slash = token.IndexOf('/');
            if (slash < 0) return false;
            x = Atol(token.Substring(0, slash));
            y = Atol(token.Substring(slash + 1));
            return x >= 0 && x < 501 && y >= 0 && y < 501;
        }

        /// <summary>abaed0: "a/b/c", each 0 or 1 → (b ? 1 : 0) + (a ? 2 : 0) + (c ? 4 : 0).</summary>
        private static bool TryFlags(string token, out int flags) {
            flags = 0;
            string[] p = token.Split('/');
            if (p.Length < 3) return false;
            int a = Atol(p[0]), b = Atol(p[1]), c = Atol(p[2]);
            if ((a | b | c) > 1 || a < 0 || b < 0 || c < 0) return false;
            flags = (b != 0 ? 1 : 0) + (a != 0 ? 2 : 0) + (c != 0 ? 4 : 0);
            return true;
        }

        private static int Atol(string s) {
            int i = 0, v = 0;
            bool negative = false;
            s = s.TrimStart();
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) negative = s[i++] == '-';
            for (; i < s.Length && s[i] >= '0' && s[i] <= '9'; i++) v = v * 10 + (s[i] - '0');
            return negative ? -v : v;
        }

        /// <summary>
        /// A &lt;NAVI&gt; link's &lt;INFO&gt; (UITextViewer 0x1b2): "map,x,y" → ac0830(map, 2 NPC, 5, 0, x, y, 1002); "map,x,y,a,b,c,d" →
        /// b == −222 searches ("$$DB " + map when a == 3), else ac0830(map, a, flags of b's digits (hundreds zeny, tens scroll,
        /// units plane), no window when c == 0, x, y, d). Custom's "map,x,y,0,100,0,0" is a cell with Kafra and no window.
        /// </summary>
        public static void NavigateLink(string info) {
            RoClassicNavigation n = Ensure();
            if (n == null || string.IsNullOrEmpty(info)) return;
            string[] p = info.Split(',');
            string map = p[0].Trim();
            int x = p.Length > 1 ? Atol(p[1]) : 0, y = p.Length > 2 ? Atol(p[2]) : 0;
            if (p.Length <= 3) {
                n.Navigate(map, 2, 5, false, x, y, 1002);
                return;
            }

            int a = Atol(p[3]), b = p.Length > 4 ? Atol(p[4]) : 0;
            if (b == -222) {
                n.Open();
                n.edit.SetText(map);
                n.query = (a == 3 ? "$$DB " : string.Empty) + map;
                n.filter = 0;
                n.RunSearch();
                return;
            }

            int c = p.Length > 5 ? Atol(p[5]) : 0, d = p.Length > 6 ? Atol(p[6]) : 0;
            int flags = (b >= 100 ? 1 : 0) | (b % 100 >= 10 ? 2 : 0) | (b % 10 >= 1 ? 4 : 0);
            n.Navigate(map, a, flags, c == 0, x, y, d);
        }

        /// <summary>A minimap icon (0xac0830(current map, 0, 5, 1, x, y, 1002)): a cell of this map with Kafra and airship, no window.</summary>
        public static void NavigateHere(int x, int y) {
            RoClassicNavigation n = Ensure();
            string map = n?.CurrentMapName();
            if (map != null) n.Navigate(map, 0, 5, true, x, y, 1002);
        }

        /// <summary>The world map's search / map click (ac0ca0(name, 0, 0)): the name searched and the window shown.</summary>
        public static void SearchFromWorldMap(string mapName) {
            RoClassicNavigation n = Ensure();
            if (n == null || string.IsNullOrEmpty(mapName)) return;
            n.Open();
            n.edit.SetText(mapName);
            n.query = mapName;
            n.filter = 0;
            n.RunSearch();
        }

        /// <summary>
        /// The world map's own search (ac0ca0(text, 0, 1)): the results only as boxes on the world map; a minimized navigation
        /// window is hidden (DAT_01517be8).
        /// </summary>
        public static void SearchFromWorldMapHidden(string text) {
            RoClassicNavigation n = Ensure();
            if (n == null || string.IsNullOrEmpty(text)) return;
            if (n.window == null) n.Create();
            n.edit.SetText(text);
            n.query = text;
            n.filter = 0;
            n.hidden = true;
            if (n.window.Visible && n.minimized) n.window.Visible = false;
            n.RunSearchQuiet();
        }

        private RoNaviObject publishedSelection;
        private int publishedRoute = -1, routeStamp, publishedBoxes = -1, boxesStamp;

        /// <summary>0x89f2b0 / 0x89f050 / 0x89f200: the world map's boxes, route and selection follow the navigation.</summary>
        private void PublishWorldMapLayers() {
            bool changed = false;
            if (publishedBoxes != boxesStamp) {
                publishedBoxes = boxesStamp;
                RoClassicWorldMap.NaviBoxes.Clear();
                foreach (RoNaviGroup group in groups) {
                    foreach (RoNaviObject member in group.Members) {
                        RoClassicWorldMap.NaviBoxes.TryGetValue(member.Map, out int value);
                        if (member.Kind == RoNaviKind.Npc && value < 10000) value += 10000;
                        else if (member.Kind == RoNaviKind.Mob) value += member.Count;
                        RoClassicWorldMap.NaviBoxes[member.Map] = value;
                    }
                }

                changed = true;
            }

            int routeKey = state == StateGuiding ? routeStamp : -2;
            if (publishedRoute != routeKey) {
                publishedRoute = routeKey;
                RoClassicWorldMap.NaviRoute.Clear();
                if (state == StateGuiding && target != null) {
                    foreach (RoNaviLink link in route) {
                        if (!RoClassicWorldMap.NaviRoute.Contains(link.Map)) RoClassicWorldMap.NaviRoute.Add(link.Map);
                    }

                    if (!RoClassicWorldMap.NaviRoute.Contains(target.Map)) RoClassicWorldMap.NaviRoute.Add(target.Map);
                }

                changed = true;
            }

            RoNaviObject selected = Selected;
            if (!ReferenceEquals(selected, publishedSelection)) {
                publishedSelection = selected;
                RoClassicWorldMap.NaviSelected = selected?.Map;
                changed = true;
            }

            if (changed) RoClassicWorldMap.NaviVersion++;
        }

        // ---------------------------------------------------------------- the world

        private RoOnlineWorldController World() {
            if (world == null && Time.frameCount - worldLookupFrame > 30) {
                worldLookupFrame = Time.frameCount;
                world = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            }

            return world;
        }

        private string CurrentMapName() {
            string name = World()?.RuntimeMapController?.MapName;
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim().Replace('\\', '/');
            int slash = name.LastIndexOf('/');
            if (slash >= 0) name = name.Substring(slash + 1);
            int dot = name.LastIndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            return name.ToLowerInvariant();
        }

        private bool TryPlayerCell(out Vector2Int cell) {
            cell = default;
            RoOnlineWorldController w = World();
            if (w == null || w.OwnActorId == 0 || !w.State.TryGetActor(w.OwnActorId, out RoOnlineActorSnapshot self)) return false;
            cell = new Vector2Int(self.CellX, self.CellY);
            return true;
        }

        private bool MapSize(out int width, out int height) {
            GAT gat = World()?.GridSelector?.Altitude;
            width = gat != null ? (int) gat.width : 0;
            height = gat != null ? (int) gat.height : 0;
            return width > 0 && height > 0;
        }

        private string DescribeRoute() {
            var s = new StringBuilder();
            foreach (RoNaviLink link in route) s.Append(link.Map).Append('>').Append(link.DestMap).Append('(').Append(link.Type).Append(") ");
            return s.ToString().Trim();
        }

        // ---------------------------------------------------------------- the ground tiles (ac0ff0)

        private sealed class Tile {
            public GameObject Go;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public Vector2Int Cell = new Vector2Int(int.MinValue, int.MinValue);
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly List<Tile> tiles = new List<Tile>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Vector3[] quad = new Vector3[4];
        private static readonly Vector2[] QuadUv = { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };
        private static readonly int[] QuadTriangles = { 0, 1, 2, 2, 1, 3 };
        private MaterialPropertyBlock block;
        private GameObject tileRoot;

        /// <summary>
        /// Every second path cell (0, 2, 4, …) within 10 cells of the player: its direction's arrow (towards the next cell),
        /// the 100-ms chaser; the location marker on the last cell.
        /// </summary>
        private void DrawTiles(Vector2Int me) {
            RoGridSelector grid = World()?.GridSelector;
            GAT gat = grid != null ? grid.Altitude : null;
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (gat == null || assets == null || path.Count == 0) {
                HideTiles();
                return;
            }

            if (tileRoot == null) tileRoot = new GameObject("RO Classic Navigation Ground") { hideFlags = HideFlags.DontSave };
            if (block == null) block = new MaterialPropertyBlock();
            int now = Environment.TickCount;
            int chaser = (int) ((uint) now % 1000u / 100u);
            int used = 0;
            for (int i = 0; i < path.Count; i += 2) {
                Vector2Int c = path[i];
                if (Math.Abs(c.x - me.x) >= Window || Math.Abs(c.y - me.y) >= Window) continue;
                Vector2Int next = i + 1 < path.Count ? path[i + 1] : c;
                Vector2Int prev = i > 0 ? path[i - 1] : c;
                Vector2Int d = next != c ? next - c : c - prev;
                string texture = "navigation_interface3/navi_grid" + iconSet.ToString(CultureInfo.InvariantCulture) + "_" + Direction(d).ToString(CultureInfo.InvariantCulture) + ".tga";
                PlaceTile(used++, grid, gat, assets, c, texture, new Color32(255, 255, 255, (byte) (i % 10 == chaser ? 0xee : 0x99)));
            }

            Vector2Int last = path[path.Count - 1];
            if (Math.Abs(last.x - me.x) < Window && Math.Abs(last.y - me.y) < Window) {
                int frame = 1 + (int) ((uint) now / 150u % 4u);
                Color32 tint = onFinalMap ? new Color32(255, 255, 255, 0xee) : new Color32(0x64, 0xaa, 0xff, 0xee);
                PlaceTile(used++, grid, gat, assets, last, "navigation_interface3/location" + frame.ToString(CultureInfo.InvariantCulture) + ".tga", tint);
            }

            for (int i = used; i < tiles.Count; i++) {
                if (tiles[i].Go != null) tiles[i].Go.SetActive(false);
            }
        }

        /// <summary>
        /// The arrow texture of a step: navi_grid{set}_0..7 point S, SW, W, NW, N, NE, E, SE (the long tapered end is the
        /// tip; checked in the lab against a route), north being the cell's +y.
        /// </summary>
        private static int Direction(Vector2Int d) {
            int dx = Math.Sign(d.x), dy = Math.Sign(d.y);
            if (dx == 0) return dy >= 0 ? 4 : 0;
            if (dx > 0) return dy > 0 ? 5 : dy == 0 ? 6 : 7;
            return dy < 0 ? 1 : dy == 0 ? 2 : 3;
        }

        private void PlaceTile(int index, RoGridSelector grid, GAT gat, RoAssetDatabase assets, Vector2Int cell, string texture, Color32 color) {
            Material material = TileMaterial(assets, texture);
            if (material == null) return;
            while (tiles.Count <= index) tiles.Add(new Tile());
            Tile tile = tiles[index];
            if (tile.Go == null) {
                tile.Go = new GameObject("tile") { hideFlags = HideFlags.DontSave };
                tile.Go.transform.SetParent(tileRoot.transform, false);
                tile.Mesh = new Mesh { name = "RO navigation tile" };
                tile.Mesh.MarkDynamic();
                tile.Go.AddComponent<MeshFilter>().sharedMesh = tile.Mesh;
                tile.Renderer = tile.Go.AddComponent<MeshRenderer>();
                tile.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                tile.Renderer.receiveShadows = false;
                tile.Cell = new Vector2Int(int.MinValue, int.MinValue);
            }

            if (tile.Cell != cell) {
                tile.Cell = cell;
                if (cell.x < 0 || cell.y < 0 || cell.x >= gat.width || cell.y >= gat.height) return;
                Vector4 h = gat.cells[cell.x + cell.y * gat.width].Heights;
                Transform t = grid.transform;
                quad[0] = t.TransformPoint(RoMapCoordinateSystem.GatVertex(cell.x, cell.y + 1, h[0]) + Vector3.up * TileLift);
                quad[1] = t.TransformPoint(RoMapCoordinateSystem.GatVertex(cell.x + 1, cell.y + 1, h[1]) + Vector3.up * TileLift);
                quad[2] = t.TransformPoint(RoMapCoordinateSystem.GatVertex(cell.x, cell.y, h[2]) + Vector3.up * TileLift);
                quad[3] = t.TransformPoint(RoMapCoordinateSystem.GatVertex(cell.x + 1, cell.y, h[3]) + Vector3.up * TileLift);
                tile.Mesh.Clear();
                tile.Mesh.vertices = quad;
                tile.Mesh.uv = QuadUv;
                tile.Mesh.triangles = QuadTriangles;
                tile.Mesh.RecalculateBounds();
            }

            tile.Renderer.sharedMaterial = material;
            block.SetColor(ColorId, color);
            tile.Renderer.SetPropertyBlock(block);
            if (!tile.Go.activeSelf) tile.Go.SetActive(true);
        }

        private Material TileMaterial(RoAssetDatabase assets, string relative) {
            if (materials.TryGetValue(relative, out Material cached)) return cached;
            Texture2D texture = assets.LoadTexture(RoInterfacePath.Build(relative));
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material material = null;
            if (texture != null && shader != null) {
                texture.wrapMode = TextureWrapMode.Clamp;
                material = new Material(shader) { name = "RO navigation " + relative, mainTexture = texture, hideFlags = HideFlags.DontSave };
                material.renderQueue = (int) RenderQueue.Transparent;
            }

            materials[relative] = material;
            return material;
        }

        /// <summary>
        /// Diagnostics: the tip angle of each loaded navi_grid{set}_d texture as the GPU samples it (0 = +u, 90 = +v; the tip
        /// lies from the alpha centroid towards the bounding-box centre).
        /// </summary>
        internal string DescribeArrowTextures() {
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null) return "no assets";
            var s = new StringBuilder();
            for (int d = 0; d < 8; d++) {
                Texture2D t = assets.LoadTexture(RoInterfacePath.Build("navigation_interface3/navi_grid" + iconSet + "_" + d + ".tga"));
                if (t == null || !t.isReadable) {
                    s.Append(d).Append(":unreadable ");
                    continue;
                }

                Color32[] px = t.GetPixels32();
                float sx = 0, sy = 0, sa = 0;
                int minX = t.width, maxX = 0, minY = t.height, maxY = 0;
                for (int i = 0; i < px.Length; i++) {
                    if (px[i].a < 100) continue;
                    int x = i % t.width, y = i / t.width;
                    sx += x * px[i].a;
                    sy += y * px[i].a;
                    sa += px[i].a;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }

                float vx = (minX + maxX) / 2f - sx / sa, vy = (minY + maxY) / 2f - sy / sa;
                s.Append(d).Append(':').Append(Mathf.RoundToInt(Mathf.Atan2(vy, vx) * Mathf.Rad2Deg)).Append(' ');
            }

            return s.ToString().Trim();
        }

        private void HideTiles() {
            if (tileRoot == null) return;
            foreach (Tile tile in tiles) {
                if (tile.Go != null && tile.Go.activeSelf) tile.Go.SetActive(false);
            }
        }
    }
}
