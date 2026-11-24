using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Maps;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The navigation (UINavigationV4Wnd, window id 0xcb; scratchpad navigation-spec): one window, 272×338 at
    /// NAVIGATIONWNDINFO (default the screen centre − (136, 169)), 134×61 minimized. Page 0 searches the navigation
    /// tables (the type combo All / Map / Npc / Mob, a 30-byte edit, "[n]name" result groups and their members with
    /// NPC cells and monster densities, the selection's map thumbnail); "Navigation" (0x1aa) computes the route over
    /// the links (the "service" box allows Kafra and airship) and starts the guidance: page 1 shows the map with the
    /// path dots, the destination and the player; the ground shows every second path cell within 10 cells as a
    /// navi_grid arrow with a chaser every 100 ms and the animated location marker (RoClassicNavigation.Guide.cs).
    /// Messages go to the chat as "Navigation >: …" in #FFB062. Openers: the menu icon, the world map, /navi map x/y,
    /// /navi2 map x/y a/b/c, /naviopen. Help 0xe5, the icon set 0x132 and the route list 0x13a are in
    /// RoClassicNavigation.Sub.cs; sharing a location, external data files and the private airship are not ported yet.
    /// </summary>
    public sealed partial class RoClassicNavigation : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string N3 = Ui + "navigation_interface3\\";
        private const string Plane = Ui + "private_airplane\\";
        private const string Prefs = "ro.ui.classic.navigation.";
        private const int W = 272, H = 338, MiniW = 134, MiniH = 61, RowH = 16;
        private const int CloseId = 0xc5, MiniId = 0xc6, MaxId = 0xc4, HelpId = 0x153, BackId = 0x118, SearchId = 0x13d, DataId = 0xbb;
        private const int ShareId = 0x15e, FindId = 0x1aa, MoveId = 0x1ef, IconSetId = 0xb9, RouteListId = 0x1b6;
        private const int ResultX = 1, ResultY = 70, ResultW = 138, ResultH = 244;
        private const int DetailX = 139, DetailY = 166, DetailW = 132, DetailH = 80;
        private static readonly string[] FilterNames = { "All", "Map", "Npc", "Mob" };   // MsgStr 0x89e..0x8a1
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Line = new Color32(0xcc, 0xcc, 0xcc, 255);
        private static readonly Color32 Selection = new Color32(0xca, 0xdf, 0xfb, 255);
        private static readonly Color32 DetailBack = new Color32(240, 244, 253, 255);
        private static readonly Color32 ListBorder = new Color32(0xde, 0xde, 0xeb, 255);
        private static readonly Color32 RouteDot = new Color32(0x0a, 0xf0, 0xf0, 255);
        private static readonly Color32 Hint = new Color32(0xc8, 0xc8, 0xc8, 255);
        internal const string ChatColor = "#FFB062";   // COLORREF 0x62b0ff

        private static RoClassicNavigation instance;
        private static RoLoginShellSession session;
        private static RoClassicChat chat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            session = null;
            chat = null;
        }

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private RoClassicWindow combo;
        private RoClassicEditField edit;
        private bool comboOpen;

        // the manager's state (client lifetime, not saved): +0x1254 minimized, +0x125c page, +0x1258 state, +0x1260 filter
        private bool minimized;
        private int page;
        private bool service = true;
        private int filter;
        private string query = string.Empty;
        private List<RoNaviGroup> groups = new List<RoNaviGroup>();
        private int groupSel = -1, groupFirst, memberSel = -1, memberFirst;
        private string pendingSearch;

        /// <summary>The session and chat the window works with (from the chat window's classic ticks).</summary>
        public static void Bind(RoLoginShellSession loginSession, RoClassicChat chatWindow) {
            session = loginSession;
            if (chatWindow != null) chat = chatWindow;
            RoClassicNavigation n = Ensure();
            if (n == null || loginSession == null) return;
            // ZC_NAVIGATION_ACTIVE (0xc92da0): the server's target, flags and hide byte
            (Ragnarok.Network.RoZcNavigationActive packet, int sequence) = loginSession.NavigationActiveSnapshot();
            if (n.navigationSequence < 0) n.navigationSequence = sequence;
            if (sequence == n.navigationSequence) return;
            n.navigationSequence = sequence;
            int f = packet.Flag;
            int flags = (f >= 100 ? 1 : 0) | (f % 100 >= 10 ? 2 : 0) | (f % 10 >= 1 ? 4 : 0);
            n.Navigate(packet.Map, packet.Type, flags, packet.HideWindow != 0, packet.X, packet.Y, packet.MobId);
        }

        private int navigationSequence = -1;

        private static RoClassicNavigation Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicNavigation { system = system };
                system.Register(instance);
                RoClassicWorldMap.NavigateRequested = SearchFromWorldMap;
                RoClassicWorldMap.NaviWindowOpen = () => instance != null && instance.window != null && instance.window.Visible && !instance.minimized;
                RoClassicWorldMap.NaviSearch = SearchFromWorldMapHidden;
                RoClassicWorldMap.NaviOpenWindow = () => {
                    instance.minimized = false;
                    instance.Open();
                };
            }

            return instance;
        }

        /// <summary>The menu icon 0x1aa: MakeWindow(0xcb) when absent, else the window's own close action.</summary>
        public static void Toggle() {
            RoClassicNavigation n = Ensure();
            if (n == null) return;
            if (n.window != null && n.window.Visible) n.CloseAction();
            else n.Open();
        }

        /// <summary>MakeWindow(0xcb): create-or-show at NAVIGATIONWNDINFO.</summary>
        private void Open() {
            if (window == null) Create();
            hidden = false;
            if (!window.Visible) {
                Vector2Int screen = system.ClientSize;
                int x = PlayerPrefs.GetInt(Prefs + "x", screen.x / 2 - 136), y = PlayerPrefs.GetInt(Prefs + "y", screen.y / 2 - 169);
                window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, screen.x - W)), Mathf.Clamp(y, 0, Math.Max(0, screen.y - H)));
                window.Visible = true;
            }

            Relayout();
            system.Windows.BringToFront(window);
        }

        private void Create() {
            var layout = new RoClassicWindowLayout { Class = "UINavigationV4Wnd", WindowId = 0xcb, Width = W, Height = H };
            layout.Children.Add(Button(CloseId, "close", W - 14, 3, 11, 11, Basic + "sys_close_off.bmp", Basic + "sys_close_on.bmp", Basic + "sys_close_on.bmp", 0xbe5, "Close"));
            layout.Children.Add(Button(MiniId, "mini", W - 28, 3, 11, 11, Basic + "sys_mini_off.bmp", Basic + "sys_mini_on.bmp", Basic + "sys_mini_on.bmp", 0x89c, "Change to simple UI"));
            layout.Children.Add(Button(HelpId, "help", W - 42, 3, 11, 11, Ui + "navigation_interface\\navihelp_off.bmp", Ui + "navigation_interface\\navihelp_on.bmp", Ui + "navigation_interface\\navihelp_on.bmp", 0x89d, "Help"));
            layout.Children.Add(Button(CloseId, "miniclose", MiniW - 15, 2, 14, 14, N3 + "btn_close_normal.bmp", N3 + "btn_close_over.bmp", N3 + "btn_close_press.bmp", 0xbe5, "Close"));
            layout.Children.Add(Button(MaxId, "max", MiniW - 32, 2, 14, 14, N3 + "btn_maximize_normal.bmp", N3 + "btn_maximize_over.bmp", N3 + "btn_maximize_press.bmp", 0x89b, "Change to basic UI"));
            layout.Children.Add(Button(BackId, "back", 4, 21, 22, 18, N3 + "bt_back.bmp", N3 + "bt_back_on.bmp", N3 + "bt_back_on.bmp", 0xc88, "Search & description"));
            layout.Children.Add(Button(SearchId, "search", 207, 21, 25, 18, N3 + "btn_searchbar_normal.bmp", N3 + "btn_searchbar_over.bmp", N3 + "btn_searchbar_press.bmp", 0x894, "Search"));
            layout.Children.Add(Button(DataId, "data", 236, 21, 30, 18, N3 + "btn_navidata_open_normal.bmp", N3 + "btn_navidata_open_over.bmp", N3 + "btn_navidata_open_press.bmp", 0x899, "Read external file path information"));
            layout.Children.Add(Button(ShareId, "share", 145, 263, 58, 20, Plane + "btn_share_normal.bmp", Plane + "btn_share_over.bmp", Plane + "btn_share_press.bmp", 0xc86, "Share current location"));
            layout.Children.Add(Button(FindId, "find", 207, 263, 58, 20, Plane + "btn_find_normal.bmp", Plane + "btn_find_over.bmp", Plane + "btn_find_press.bmp", 0x895, "Navigation"));
            layout.Children.Add(Button(MoveId, "move", 145, 288, 120, 20, Plane + "btn_move_normal.bmp", Plane + "btn_move_over.bmp", Plane + "btn_move_press.bmp", 0, null));
            layout.Children.Add(Button(RouteListId, "routelist", 203, 317, 26, 18, N3 + "btn_roadlistwnd_normal.bmp", N3 + "btn_roadlistwnd_over.bmp", N3 + "btn_roadlistwnd_press.bmp", 0xc85, "Navigation information"));
            layout.Children.Add(Button(IconSetId, "iconset", 237, 317, 26, 18, null, null, null, 0xc8b, "Show navigation icon setting"));
            window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += Draw;
            window.Clicked += OnClicked;
            window.Pressed += OnPressed;
            window.Wheeled += (w, local, delta) => {
                if (Inside(local, ResultX, ResultY, ResultW, ResultH)) groupFirst = Mathf.Clamp(groupFirst + (delta > 0 ? -6 : 6), 0, Math.Max(0, groups.Count - ResultH / RowH));
                else if (Inside(local, DetailX, DetailY, DetailW, DetailH)) memberFirst = Mathf.Clamp(memberFirst + (delta > 0 ? -6 : 6), 0, Math.Max(0, Members.Count - DetailH / RowH));
                window.Invalidate();
            };
            window.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "y", w.Position.y);
            };
            window.EscClose = () => {
                CloseAction();
                return true;
            };
            edit = new RoClassicEditField { Rect = new RectInt(97, 23, 107, 14), MaxBytes = 30 };
            edit.Submitted += _ => Search();
            window.AddEditField(edit);
            ApplyIconSetButton();
        }

        private static RoClassicChildLayout Button(int id, string path, int x, int y, int w, int h, string normal, string hover, string pressed, int tipId, string tip) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            if (normal != null) {
                button.States["normal"] = normal;
                button.States["hover"] = hover;
                button.States["pressed"] = pressed;
            }

            if (tip != null) button.Tooltip = RoClassicUiHost.MsgStr(tipId, tip);
            return button;
        }

        /// <summary>5927b0: the icon-set button shows the chosen set's bitmaps.</summary>
        private void ApplyIconSetButton() {
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                if (child.Path != "iconset") continue;
                string set = iconSet.ToString(CultureInfo.InvariantCulture);
                child.States["normal"] = N3 + "btn_roadiocn_select" + set + "_normal.bmp";
                child.States["hover"] = N3 + "btn_roadiocn_select" + set + "_over.bmp";
                child.States["pressed"] = N3 + "btn_roadiocn_select" + set + "_press.bmp";
            }
        }

        /// <summary>58d310: the minimized form (only its close and maximize), else the page's children.</summary>
        private void Relayout() {
            if (window == null) return;
            int w = minimized ? MiniW : W, h = minimized ? MiniH : H;
            window.Layout.Width = w;
            window.Layout.Height = h;
            window.SetHeight(h);
            window.HitArea = new RectInt(0, 0, w, h);
            bool search = page == 0;
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                bool visible;
                switch (child.Path) {
                    case "miniclose":
                    case "max":
                        visible = minimized;
                        break;
                    case "share":
                    case "find":
                    case "move":
                        visible = !minimized && search;
                        break;
                    default:
                        visible = !minimized;
                        break;
                }

                window.SetChildVisible(child, visible);
            }

            edit.Visible = !minimized;
            if (minimized && edit.HasFocus) RoClassicTextInput.Focus(null);
            CloseCombo();
            window.Invalidate();
        }

        private static bool Inside(Vector2Int p, int x, int y, int w, int h) => p.x >= x && p.x < x + w && p.y >= y && p.y < y + h;

        private List<RoNaviObject> Members => groupSel >= 0 && groupSel < groups.Count ? groups[groupSel].Members : EmptyMembers;
        private static readonly List<RoNaviObject> EmptyMembers = new List<RoNaviObject>();

        /// <summary>The selected object: the detail row, else the group's first member.</summary>
        private RoNaviObject Selected {
            get {
                List<RoNaviObject> members = Members;
                if (members.Count == 0) return null;
                return members[memberSel >= 0 && memberSel < members.Count ? memberSel : 0];
            }
        }

        private static string MapName(string map) => RoClassicUiHost.MapDisplayName(map);

        // ---------------------------------------------------------------- input (vf37 0x590790)

        private void OnClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case CloseId:
                    CloseAction();
                    break;
                case MiniId:
                    minimized = true;
                    Relayout();
                    break;
                case MaxId:
                    minimized = false;
                    hidden = false;
                    Relayout();
                    break;
                case SearchId:
                    Search();
                    break;
                case BackId:
                    Back();
                    break;
                case FindId:
                    StartFromSelection();
                    break;
                case HelpId:
                    ToggleHelp();
                    break;
                case IconSetId:
                    ToggleIconSet();
                    break;
                case RouteListId:
                    ToggleRouteList();
                    break;
                case ShareId:
                    Share();
                    break;
                case MoveId:
                    OpenAirship();
                    break;
                case DataId:
                    // not ported: Navigationdata*.txt (the token meaning is unknown)
                    break;
            }
        }

        private void OnPressed(RoClassicWindow w, Vector2Int local) {
            if (minimized) return;
            if (SharePressed(local)) return;
            if (page == 0 && Inside(local, 29, 21, 61, 18)) {
                if (comboOpen) CloseCombo();
                else OpenCombo();
                return;
            }

            if (page == 0 && Inside(local, ResultX, ResultY, ResultW - 7, ResultH)) {
                int k = groupFirst + (local.y - ResultY) / RowH;
                if (k >= 0 && k < groups.Count) SelectGroup(k);
                return;
            }

            if (page == 0 && Inside(local, DetailX, DetailY, DetailW - 7, DetailH)) {
                int k = memberFirst + (local.y - DetailY) / RowH;
                if (k >= 0 && k < Members.Count) {
                    memberSel = k;
                    window.Invalidate();
                }

                return;
            }

            // the "service" box (146, 247) and its label; the "Minimize" box (9, 321)
            if (page == 0 && Inside(local, 146, 247, 110, 12)) {
                service = !service;
                window.Invalidate();
            } else if (Inside(local, 9, 321, 110, 12)) {
                minimized = true;
                Relayout();
            }
        }

        /// <summary>0x14c: a result row fills the detail list with its members.</summary>
        private void SelectGroup(int k) {
            groupSel = k;
            memberSel = 0;
            memberFirst = 0;
            window.Invalidate();
        }

        /// <summary>0x13d / Enter: the edit's text searched under the filter; page 0.</summary>
        private void Search() {
            string text = edit.Text.Trim();
            if (text.Length == 0) return;
            query = text;
            RunSearch();
        }

        /// <summary>The search without touching the window (the world map's own search).</summary>
        private void RunSearchQuiet() {
            RoNaviTables tables = Tables();
            if (tables == null) return;
            groups = tables.Search(query, filter, MapName);
            groupSel = groups.Count > 0 ? 0 : -1;
            memberSel = groupSel >= 0 ? 0 : -1;
            groupFirst = memberFirst = 0;
            page = 0;
            boxesStamp++;
            window?.Invalidate();
        }

        private void RunSearch() {
            RoNaviTables tables = Tables();
            if (tables == null) {
                pendingSearch = query;
                return;
            }

            pendingSearch = null;
            boxesStamp++;
            groups = tables.Search(query, filter, MapName);
            groupSel = groups.Count > 0 ? 0 : -1;
            groupFirst = 0;
            memberSel = groupSel >= 0 ? 0 : -1;
            memberFirst = 0;
            page = 0;
            Relayout();
        }

        /// <summary>0x118: route view ⇄ search.</summary>
        private void Back() {
            page = page == 0 && target != null ? 1 : 0;
            Relayout();
        }

        /// <summary>
        /// 0xc5: while guiding, "Do you want to cancel navigation?" (MsgStr 0x8bf) — Yes stops it and the window stays;
        /// otherwise the results and the target are cleared and the window goes.
        /// </summary>
        private void CloseAction() {
            if (state == StateGuiding) {
                RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x8bf, "Do you want to cancel navigation?"), RoClassicMessageBox.TypeOkCancel, true, result => {
                    if (result != RoClassicMessageBox.IdYes) return;
                    CancelGuidance();
                    page = 0;
                    Relayout();
                });
                return;
            }

            page = 0;
            minimized = false;
            groups = new List<RoNaviGroup>();
            groupSel = memberSel = -1;
            ClearTarget();
            CloseCombo();
            if (window != null) {
                if (edit.HasFocus) RoClassicTextInput.Focus(null);
                window.Visible = false;
            }
        }

        // ---------------------------------------------------------------- the type combo (0x7ce510, items 0x89e..0x8a1)

        private void OpenCombo() {
            if (combo == null) {
                combo = new RoClassicWindow(system, new RoClassicWindowLayout { Class = "UIComboBoxWnd", WindowId = 0x1a, Width = 61, Height = 4 * RowH }) {
                    Visible = false,
                    Draggable = false,
                    ClickableAt = _ => true,
                };
                combo.DrawDynamic += w => {
                    w.FillRect(0, 0, w.Width, w.Height, White);
                    w.RoundOutline(0, 0, w.Width, w.Height, Line);
                    for (int i = 0; i < FilterNames.Length; i++) {
                        if (i == filter) w.FillRect(1, RowH * i + 1, w.Width - 2, RowH - 1, Selection);
                        w.TextOut(4, RowH * i + 2, RoClassicUiHost.MsgStr(0x89e + i, FilterNames[i]), 0, 12, Black);
                    }
                };
                combo.Pressed += (w, local) => {
                    int k = local.y / RowH;
                    if (k >= 0 && k < FilterNames.Length) {
                        filter = k;
                        if (query.Length > 0) RunSearch();
                    }

                    CloseCombo();
                    window.Invalidate();
                };
                combo.EscClose = () => {
                    CloseCombo();
                    return true;
                };
            }

            combo.MoveTo(window.Position.x + 29, window.Position.y + 39);
            combo.Visible = true;
            comboOpen = true;
            system.Windows.BringToFront(combo);
            combo.Invalidate();
        }

        private void CloseCombo() {
            comboOpen = false;
            if (combo != null) combo.Visible = false;
        }

        // ---------------------------------------------------------------- drawing (OnDraw 58edd0)

        private void Draw(RoClassicWindow w) {
            if (minimized) {
                DrawMinimized(w);
                return;
            }

            // the frame: title bar, body, button bar; the line under the search row; bg_down at the bottom
            DrawTitleBar(w, W);
            w.FillRect(0, 17, W, H - 17, White);
            w.RoundOutline(0, 17, W, H - 17, Line);
            w.TextOut(17, 4, RoClassicUiHost.MsgStr(0x8ad, "Navigation"), 0, 12, White);
            w.TextOut(16, 3, RoClassicUiHost.MsgStr(0x8ad, "Navigation"), 0, 12, Black);
            w.FillRect(0, 43, W, 1, Line);
            w.DrawBitmapRaw(0, H - 24, N3 + "bg_down.bmp");

            // the combo, the edit's background
            w.DrawBitmapRaw(29, 21, N3 + "bg_combo.bmp");
            w.DrawBitmap(72, 21, N3 + (comboOpen ? "dropdown_on_press.bmp" : "dropdown_on.bmp"));
            w.TextOut(33, 24, RoClassicUiHost.MsgStr(0x89e + filter, FilterNames[filter]), 0, 12, new Color32(100, 100, 100, 255));
            w.DrawBitmapRaw(94, 21, N3 + "bg_search.bmp");

            if (page == 0) DrawSearchPage(w);
            else if (page == 2) DrawSharePage(w);
            else DrawRoutePage(w);

            // the "Minimize" check box (9, 321)
            w.DrawBitmap(9, 322, Ui + "checkbox_0.bmp");
            w.TextOut(22, 321, RoClassicUiHost.MsgStr(0x89c, "Change to simple UI"), 0, 12, Black);
        }

        private void DrawSearchPage(RoClassicWindow w) {
            w.FillRect(138, 44, 1, 270, Line);
            w.FillRect(139, 242, 132, 1, Line);
            w.DrawBitmap(5, 46, N3 + "bg_dot.bmp");
            string count = RoClassicUiHost.MsgStr(0xc8a, "Search result[%d]").Replace("%d", groups.Count.ToString(CultureInfo.InvariantCulture));
            w.TextOut(20, 50, count, 0, 12, Black);

            // the selection (nav.abe1b0): an NPC as a 116² preview on anibackground tiles at (147, 45) with the map's 40²
            // thumbnail at (227, 120); otherwise the map picture in a black 100² square at (155, 60)
            RoNaviObject selected = Selected;
            if (selected != null && selected.Kind == RoNaviKind.Npc) {
                w.ClipTo(147, 45, 116, 116);
                for (int ty = 0; ty < 116; ty += 28) {
                    for (int tx = 0; tx < 116; tx += 28) w.DrawBitmapRaw(147 + tx, 45 + ty, Ui + "anibackground.bmp");
                }

                if (previewShown != null && previewFor == selected) w.DrawBitmap(147, 45, previewShown);
                w.ResetClip();
                w.FillRect(227, 120, 40, 40, Black);
                w.DrawStretched(227, 120, 40, 40, Ui + "map\\" + selected.Map + ".bmp", White);
            } else if (selected != null) {
                w.FillRect(155, 60, 100, 100, Black);
                w.DrawStretched(155, 60, 100, 100, Ui + "map\\" + selected.Map + ".bmp", White);
            }

            DrawList(w, ResultX, ResultY, ResultW, ResultH, White, groups.Count, groupFirst, groupSel, i => {
                RoNaviGroup g = groups[i];
                return (KindIcon(g.Kind), "[" + g.Members.Count.ToString(CultureInfo.InvariantCulture) + "]" + g.Name);
            });
            List<RoNaviObject> members = Members;
            DrawList(w, DetailX, DetailY, DetailW, DetailH, DetailBack, members.Count, memberFirst, memberSel, i => (KindIcon(members[i].Kind), MemberText(members[i])));

            // the "service" box: Kafra and airship moves (MsgStr 0xc8c)
            w.DrawBitmap(146, 248, Ui + (service ? "checkbox_1.bmp" : "checkbox_0.bmp"));
            w.TextOut(159, 247, RoClassicUiHost.MsgStr(0xc8c, "Path that uses zeny"), 0, 12, Black);
        }

        /// <summary>UIListBox_NaviSearch (58eb70): rows of 16 from the top row, the selection #CADFFB, an 11×11 kind icon, black text.</summary>
        private static void DrawList(RoClassicWindow w, int x, int y, int lw, int lh, Color32 back, int count, int first, int selected, Func<int, (string Icon, string Text)> row) {
            w.FillRect(x, y, lw, lh, back);
            w.FillRect(x + lw - 1, y, 1, lh, ListBorder);
            int rows = lh / RowH;
            RoClassicGdiFont font = w.FontFor(0, 12);
            for (int r = 0; r < rows && first + r < count; r++) {
                int i = first + r, ry = y + RowH * r;
                if (i == selected) w.FillRect(x, ry, lw - 1, RowH - 1, Selection);
                (string icon, string text) = row(i);
                int tx = x + 3;
                if (icon != null) {
                    w.DrawBitmap(x + 3, ry + 2, icon);
                    tx = x + 14;
                }

                w.TextOut(tx, ry + 2, Fit(font, text, x + lw - 8 - tx), 0, 12, Black);
            }

            if (count > rows) w.DrawScrollBar(x + lw - 13, y, lh, count - rows, count, rows, first);
        }

        private static string Fit(RoClassicGdiFont font, string text, int width) {
            if (font.Measure(text) <= width) return text;
            int n = text.Length;
            while (n > 0 && font.Measure(text.Substring(0, n)) > width) n--;
            return text.Substring(0, n);
        }

        private static string KindIcon(RoNaviKind kind) {
            switch (kind) {
                case RoNaviKind.Map: return N3 + "icon_list_map.bmp";
                case RoNaviKind.Npc: return N3 + "icon_list_npc.bmp";
                case RoNaviKind.Mob: return N3 + "icon_list_mob.bmp";
                default: return null;
            }
        }

        /// <summary>5920f0: an NPC "map [x,y]", a monster "map (density)" (0x991..0x995 by its count), a map its name.</summary>
        private static string MemberText(RoNaviObject o) {
            string map = MapName(o.Map);
            switch (o.Kind) {
                case RoNaviKind.Npc:
                    return map + " [" + o.X.ToString(CultureInfo.InvariantCulture) + "," + o.Y.ToString(CultureInfo.InvariantCulture) + "]";
                case RoNaviKind.Mob:
                    if (o.Count <= 0) return map;
                    int id = o.Count >= 100 ? 0x991 : o.Count >= 50 ? 0x992 : o.Count >= 20 ? 0x993 : o.Count >= 5 ? 0x994 : 0x995;
                    string[] names = { "Very High", "High", "Average", "Low", "Very Low" };
                    return map + " (" + RoClassicUiHost.MsgStr(id, names[id - 0x991]) + ")";
                default:
                    return map;
            }
        }

        /// <summary>Page 1: the current map scaled to 266² at (3, 46), the path dots, the destination and the player; the texts.</summary>
        private void DrawRoutePage(RoClassicWindow w) {
            w.FillRect(1, 44, 270, 270, Line);
            w.FillRect(3, 46, 266, 266, Black);
            string map = currentMap ?? string.Empty;
            w.DrawStretched(3, 46, 266, 266, Ui + "map\\" + map + ".bmp", White);
            if (MapSize(out int mw, out int mh)) {
                for (int i = 0; i < path.Count; i++) {
                    Vector2 p = MinimapPoint(path[i], mw, mh);
                    w.FillRect((int) p.x, (int) p.y, 2, 2, RouteDot);
                }

                if (path.Count > 0) {
                    Vector2 last = MinimapPoint(path[path.Count - 1], mw, mh);
                    w.DrawBitmap((int) last.x - 8, (int) last.y - 21, N3 + "ico_destination.bmp");
                }

                if (TryPlayerCell(out Vector2Int me)) {
                    Vector2 p = MinimapPoint(me, mw, mh);
                    w.DrawBitmap((int) p.x - 8, (int) p.y - 8, N3 + "ico_location.bmp");
                }
            }

            RoClassicGdiFont bold = w.FontFor(0, 12, true);
            if (target != null) {
                w.TextOut(10, 48, Fit(bold, target.Name ?? string.Empty, 250), 0, 12, White, true);
                w.TextOut(10, 68, Fit(bold, MapName(target.Map), 250), 0, 12, White, true);
            }

            if (!string.IsNullOrEmpty(lastMessage)) w.TextOut(10, H - 42, Fit(bold, lastMessage, 250), 0, 12, White, true);
        }

        /// <summary>A cell in the 266-pixel map view: the longer side fills it, the picture's y grows downwards.</summary>
        private static Vector2 MinimapPoint(Vector2Int cell, int mw, int mh) {
            float s = 266f / Math.Max(mw, mh);
            float ox = (266f - mw * s) / 2f, oy = (266f - mh * s) / 2f;
            return new Vector2(3 + ox + (cell.x + 0.5f) * s, 46 + oy + (mh - cell.y - 0.5f) * s);
        }

        /// <summary>The minimized form: a white box with a black inside, "Navigation", and while guiding the destination icon and two lines.</summary>
        private void DrawMinimized(RoClassicWindow w) {
            w.FillRect(0, 0, MiniW, MiniH, White);
            w.FillRect(1, 17, MiniW - 2, MiniH - 18, Black);
            w.TextOut(3, 3, RoClassicUiHost.MsgStr(0x8ad, "Navigation"), 0, 12, Black);
            if (target == null) return;
            RoClassicGdiFont bold = w.FontFor(0, 12, true);
            w.DrawBitmap(2, 18, N3 + "ico_destination.bmp");
            w.TextOut(19, 21, Fit(bold, target.Name ?? string.Empty, MiniW - 22), 0, 12, White, true);
            w.TextOut(19, 40, Fit(bold, MapName(target.Map), MiniW - 22), 0, 12, White, true);
        }

        // ---------------------------------------------------------------- per frame

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (pendingSearch != null && Tables() != null) RunSearch();
            if (pendingStart != null && Tables() != null) {
                PendingStart start = pendingStart;
                pendingStart = null;
                Navigate(start.Map, start.Mode, start.Flags, start.Hide, start.X, start.Y, start.MobId);
            }

            TickGuidance();
            UpdatePreview();
            PublishWorldMapLayers();
            if (window != null && window.Visible && page == 1 && !minimized && Time.frameCount % 10 == 0) window.Invalidate();
            // page 2: the cross-hair follows the mouse and the hint goes after 5 s
            if (window != null && window.Visible && page == 2 && !minimized && (ui.Windows.HoverWindow == window || Time.unscaledTime < shareHintUntil + 0.1f)) window.Invalidate();
        }

        private static RoNaviTables Tables() => RoNaviTables.Get(RoClassicUiHost.GameAssets());

        private RoNaviObject previewFor, previewRequested;
        private string previewShown;
        private int previewVersion;

        /// <summary>The selected NPC rendered once (its sprite id as the NPC job, standing south, feet at 100 of 116).</summary>
        private void UpdatePreview() {
            if (window == null || !window.Visible || minimized || page != 0) return;
            RoNaviObject selected = Selected;
            if (selected == null || selected.Kind != RoNaviKind.Npc || selected.Npc == null || ReferenceEquals(selected, previewRequested)) return;
            Ragnarok.Assets.RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null || !assets.IsInitialized) return;
            previewRequested = selected;
            int version = ++previewVersion;
            string key = "@navi-npc-preview-" + (version & 1).ToString(CultureInfo.InvariantCulture);
            var request = new Ragnarok.Entities.RoActorAppearanceRequest {
                kind = Ragnarok.Entities.RoActorAppearanceKind.Npc,
                classId = selected.Npc.Sprite,
                motionProfile = Ragnarok.Sprites.RoSpriteMotionProfile.Npc,
                motion = Ragnarok.Sprites.RoSpriteMotion.Idle,
                facingDirection = Ragnarok.Sprites.RoActorDirection.South,
            };
            bool started = RoClassicActorPortrait.Render(assets, request, key, 116, 116, 100, ok => {
                if (!ok || version != previewVersion) return;
                previewShown = key;
                previewFor = selected;
                window?.Invalidate();
            });
            if (!started) previewRequested = null;
        }

        // ---------------------------------------------------------------- diagnostics (RoSkillSweepRunner "!nav ...")

        /// <summary>open | close | search q | filter n | pick g [m] | start | service 0/1 | min | max | back | cmd /navi … | state.</summary>
        internal static string Sweep(string command) {
            RoClassicNavigation n = Ensure();
            if (n == null) return "no classic UI";
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            string arg = a.Length > 1 ? a[1] : string.Empty;
            switch (a[0]) {
                case "open":
                    n.Open();
                    break;
                case "close":
                    n.CloseAction();
                    break;
                case "search":
                    n.Open();
                    n.edit.SetText(arg);
                    n.Search();
                    break;
                case "filter":
                    n.filter = Mathf.Clamp(int.Parse(arg, CultureInfo.InvariantCulture), 0, 3);
                    if (n.query.Length > 0) n.RunSearch();
                    break;
                case "pick": {
                    string[] p = arg.Split(' ');
                    n.SelectGroup(int.Parse(p[0], CultureInfo.InvariantCulture));
                    if (p.Length > 1) n.memberSel = int.Parse(p[1], CultureInfo.InvariantCulture);
                    n.window?.Invalidate();
                    break;
                }
                case "start":
                    n.StartFromSelection();
                    break;
                case "service":
                    n.service = arg == "1";
                    break;
                case "min":
                    n.minimized = true;
                    n.Relayout();
                    break;
                case "max":
                    n.minimized = false;
                    n.Relayout();
                    break;
                case "back":
                    n.Back();
                    break;
                case "cmd":
                    TryCommand(arg);
                    break;
                case "tex":
                    return n.DescribeArrowTextures();
                case "link":
                    NavigateLink(arg);
                    break;
                case "share":
                    n.Share();
                    break;
                case "airshippress":
                    if (n.airship != null && n.airship.Visible && !n.airshipDisabled) n.RequestAirship(AirshipItems[int.Parse(arg, CultureInfo.InvariantCulture)]);
                    return "airship: msg='" + n.airshipMessage + "'";
                case "airship":
                    n.OpenAirship();
                    return "airship: msg='" + n.airshipMessage + "' disabled=" + n.airshipDisabled + " target=" + n.airshipTarget;
                case "sharepress": {
                    string[] sp = arg.Split(' ');
                    n.SharePressed(new Vector2Int(int.Parse(sp[0], CultureInfo.InvariantCulture), int.Parse(sp[1], CultureInfo.InvariantCulture)));
                    break;
                }
                case "talk": {
                    // CZ_CONTACTNPC to the nearest NPC (makes the NPC dialog for link tests)
                    RoOnlineWorldController w = n.World();
                    if (w == null || session == null || !n.TryPlayerCell(out Vector2Int here)) break;
                    var actors = new List<RoOnlineActorSnapshot>();
                    w.SnapshotActors(actors);
                    uint best = 0;
                    int bestD = int.MaxValue;
                    foreach (RoOnlineActorSnapshot actor in actors) {
                        if (actor.ObjectType != Ragnarok.Network.RoMapActorObjectType.Npc) continue;
                        int d = Math.Max(Math.Abs(actor.CellX - here.x), Math.Abs(actor.CellY - here.y));
                        if (d < bestD) {
                            bestD = d;
                            best = actor.ServerId;
                        }
                    }

                    if (best != 0) _ = session.BeginContactNpc(unchecked((int) best));
                    return "talk: npc " + best + " at " + bestD;
                }
            }

            RoNaviObject sel = n.Selected;
            return $"tables={(Tables() != null ? "ready" : RoNaviTables.Failed ? "failed" : "loading")} open={n.window != null && n.window.Visible} min={n.minimized} page={n.page} state={n.state} " +
                $"query='{n.query}' groups={n.groups.Count} sel={n.groupSel}/{n.memberSel} selected={(sel != null ? sel.Kind + ":" + sel.Name + "@" + sel.Map + "(" + sel.X + "," + sel.Y + ")" : "-")} " +
                $"target={(n.target != null ? n.target.Kind + ":" + n.target.Name + "@" + n.target.Map : "-")} route=[{n.DescribeRoute()}] step={n.step} path={n.path.Count}{(n.path.Count > 0 ? "[" + n.path[0] + ".." + n.path[n.path.Count - 1] + "]" : "")} me={(n.TryPlayerCell(out Vector2Int me) ? me.ToString() : "-")} ground={n.drawGround} map={n.currentMap} msg='{n.lastMessage}'";
        }
    }
}
