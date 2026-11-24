using System;
using System.Collections.Generic;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The world map's navigation part (navi-share-worldmap-spec §2): the search edit (131, 0) 200×18 (60 bytes), Search
    /// 0x13d (NaviSearch_A/B/C at (335, 0)) and "Open navigation window" 0xc4 (scroll0down at (375, 3)), shown only while
    /// the navigation window is absent or minimized; and three layers under the faces — the search results' boxes on field
    /// maps (wv_Sreach* by the monster count, +10000 for an NPC), the route while guiding (#5555FF at 40 %, wv_Airf2 links
    /// 5 px wide, wv_End on the target) and the selected map (wv_Select). The navigation feeds them through the statics below.
    /// </summary>
    public sealed partial class RoClassicWorldMap {
        private const int NaviSearchId = 0x13d, NaviOpenId = 0xc4;

        /// <summary>Search-result maps and their value (monster count, +10000 when an NPC matched): 0x89f2b0.</summary>
        internal static readonly Dictionary<string, int> NaviBoxes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The guided route's maps in order (each step link's map, then the target's): 0x89f050.</summary>
        internal static readonly List<string> NaviRoute = new List<string>();

        /// <summary>The selected location's map (0x89f200), or null.</summary>
        internal static string NaviSelected;

        /// <summary>Bumped on every change of the three above (the base layer is redrawn).</summary>
        internal static int NaviVersion;

        /// <summary>True while the navigation window is open and not minimized (the controls are parked then).</summary>
        internal static Func<bool> NaviWindowOpen;

        /// <summary>Search 0x13d: ac0ca0(text, 0, 1) — results only as boxes, no window.</summary>
        internal static Action<string> NaviSearch;

        /// <summary>0xc4: the navigation window opened and maximized.</summary>
        internal static Action NaviOpenWindow;

        private static readonly Color32[] NaviPalette = {
            new Color32(0x9c, 0xbe, 0xe7, 0x1e), new Color32(0xef, 0xf7, 0xa5, 0x6e), new Color32(0x8c, 0xcb, 0x9c, 0x6e),
            new Color32(0xff, 0xe7, 0x08, 0x6e), new Color32(0xff, 0x96, 0x08, 0x6e), new Color32(0xff, 0x49, 0x00, 0x6e),
        };

        private static readonly Color32 NaviNpcBox = new Color32(0xad, 0xa6, 0xc6, 0x50);
        private static readonly Color32 NaviRun = new Color32(0x55, 0x55, 0xff, 0x66);
        private static readonly Color32 NaviLink = new Color32(255, 255, 255, 0x99);
        private static readonly Color32 NaviEnd = new Color32(255, 255, 255, 0xcc);
        private static readonly Color32 NaviSelect = new Color32(255, 255, 255, 0xee);

        private RoClassicEditField naviEdit;
        private int naviVersionDrawn = -1;
        private bool naviControlsShown = true;

        private void AddNaviControls(RoClassicWindowLayout layout) {
            var search = new RoClassicChildLayout { Class = "UIBitmapButton", Id = NaviSearchId, Path = "navisearch", X = 335, Y = 0, W = 33, H = 18 };
            search.States["normal"] = Ui + "navigation_interface\\navisearch_a.bmp";
            search.States["hover"] = Ui + "navigation_interface\\navisearch_b.bmp";
            search.States["pressed"] = Ui + "navigation_interface\\navisearch_c.bmp";
            search.Tooltip = RoClassicUiHost.MsgStr(0x894, "Search");
            layout.Children.Add(search);
            var open = new RoClassicChildLayout { Class = "UIBitmapButton", Id = NaviOpenId, Path = "naviopen", X = 375, Y = 3, W = 13, H = 13 };
            open.States["normal"] = open.States["hover"] = open.States["pressed"] = Ui + "scroll0down.bmp";
            open.Tooltip = RoClassicUiHost.MsgStr(0xc8d, "Open navigation window");
            layout.Children.Add(open);
        }

        private void AddNaviEdit() {
            naviEdit = new RoClassicEditField { Rect = new RectInt(133, 2, 196, 14), MaxBytes = 60 };
            naviEdit.Submitted += _ => NaviSearchClicked();
            window.AddEditField(naviEdit);
        }

        /// <summary>vf37 0x89a5a0: 0x13d searches the edit's text (none: nothing); 0xc4 opens the navigation window.</summary>
        private bool NaviClicked(RoClassicChildLayout child) {
            if (child.Id == NaviSearchId) {
                NaviSearchClicked();
                return true;
            }

            if (child.Id == NaviOpenId) {
                NaviOpenWindow?.Invoke();
                UpdateNaviControls(true);
                return true;
            }

            return false;
        }

        private void NaviSearchClicked() {
            string text = naviEdit != null ? naviEdit.Text.Trim() : string.Empty;
            if (text.Length > 0) NaviSearch?.Invoke(text);
        }

        /// <summary>0x87e5e0: the three controls at their places only without a normal navigation window, else parked.</summary>
        private void UpdateNaviControls(bool force = false) {
            bool shown = NaviWindowOpen == null || !NaviWindowOpen();
            if (shown == naviControlsShown && !force) return;
            naviControlsShown = shown;
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                if (child.Id == NaviSearchId || child.Id == NaviOpenId) window.SetChildVisible(child, shown);
            }

            if (naviEdit != null) naviEdit.Visible = shown;
            window.Invalidate();
        }

        /// <summary>Per frame: the controls' visibility and a redraw of the base when the navigation's layers changed.</summary>
        private void TickNavi() {
            UpdateNaviControls();
            if (naviVersionDrawn == NaviVersion) return;
            naviVersionDrawn = NaviVersion;
            Invalidate();
        }

        /// <summary>The edit's (232, 232, 232) fill and (200, 100, 100) border on the DIB.</summary>
        private void DrawNaviEdit(RoClassicWindow w) {
            if (!naviControlsShown || naviEdit == null) return;
            w.FillRect(131, 0, 200, 18, new Color32(200, 100, 100, 255));
            w.FillRect(132, 1, 198, 16, new Color32(232, 232, 232, 255));
        }

        /// <summary>The field map of a stem in the shown world (groups ≥ 100 only), or null.</summary>
        private MapEntry NaviFieldMap(World world, string stem) {
            foreach (Group group in world.Groups) {
                if (group.IsDungeon) continue;
                foreach (MapEntry map in group.Maps) {
                    if (string.Equals(map.Stem, stem, StringComparison.OrdinalIgnoreCase)) return map;
                }
            }

            return null;
        }

        /// <summary>0x878e50's first three layers: the search boxes, the route, the selected map.</summary>
        private void DrawNaviLayers(RoClassicUiQuads q, World world) {
            foreach (KeyValuePair<string, int> box in NaviBoxes) {
                MapEntry map = NaviFieldMap(world, box.Key);
                if (map == null) continue;
                int v = box.Value, count = v % 10000;
                bool npc = v >= 10000;
                int level = count <= 0 ? 0 : count < 5 ? 1 : count < 20 ? 2 : count < 50 ? 3 : count < 100 ? 4 : 5;
                Edges e = map.Rect;
                Fill(q, e.L + 1, e.T + 1, e.W - 2, e.H - 2, npc && level == 0 ? NaviNpcBox : NaviPalette[level]);
                string art = npc ? (level == 0 ? "wv_SreachNpc.bmp" : "wv_SreachNpc_Mob" + level + ".bmp") : level == 0 ? "wv_SreachMap.bmp" : "wv_SreachMob" + level + ".bmp";
                Stretched(q, e.L, e.T, e.W, e.H, Wv + art, White);
            }

            MapEntry previous = null;
            for (int i = 0; i < NaviRoute.Count; i++) {
                MapEntry map = NaviFieldMap(world, NaviRoute[i]);
                if (map == null) {
                    previous = null;
                    continue;
                }

                Edges e = map.Rect;
                Fill(q, e.L + 1, e.T + 1, e.W - 2, e.H - 2, NaviRun);
                if (previous != null) NaviConnector(q, previous.Rect, e);
                if (i == NaviRoute.Count - 1 && system.Images.TryGet(Wv + "wv_End.bmp", out RoClassicUiImage end)) {
                    q.Image(e.L + e.W / 2 - end.Width / 2, e.T + e.H / 2 - end.Height / 2, end.Width, end.Height, end, NaviEnd);
                }

                previous = map;
            }

            if (!string.IsNullOrEmpty(NaviSelected)) {
                MapEntry selected = NaviFieldMap(world, NaviSelected);
                if (selected != null) Stretched(q, selected.Rect.L, selected.Rect.T, selected.Rect.W, selected.Rect.H, Wv + "wv_Select.bmp", NaviSelect);
            }
        }

        /// <summary>0x52c360: a 5-px textured line (wv_Airf2) between two boxes' centres.</summary>
        private void NaviConnector(RoClassicUiQuads q, Edges a, Edges b) {
            if (!system.Images.TryGet(Wv + "wv_Airf2.bmp", out RoClassicUiImage line)) return;
            var p = new Vector2(a.L + a.W / 2f, a.T + a.H / 2f);
            var r = new Vector2(b.L + b.W / 2f, b.T + b.H / 2f);
            Vector2 d = r - p;
            float length = d.magnitude;
            if (length < 0.001f) return;
            Vector2 n = new Vector2(-d.y, d.x) / length * 2.5f;
            q.Strip(p - n, Uv00, p + n, Uv10, r - n, Uv01, r + n, Uv11, line, NaviLink);
        }
    }
}
