using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The navigation's side windows (navigation-spec §6): the route list 0x13a docked right of the window (150×338, the
    /// steps "map (x y)" with the start / route / target icons), the icon set 0x132 (158×102 under the window, eight
    /// arrow sets) and the help 0xe5 (310×200 at (100, 100), the instruction lines in a grey text viewer).
    /// </summary>
    public sealed partial class RoClassicNavigation {
        private RoClassicWindow routeList, iconWindow, helpWindow;
        private int routeFirst, helpFirst;
        private readonly List<string> helpLines = new List<string>();
        private static readonly int[] HelpIds = {
            0x8c1, -1, 0x8c2, 0x8c3, 0x8c4, -1, 0x8c5, 0x8c6, 0x8c7, -1, 0x8c8, -1, 0x8c9, -1, 0x8ca, -1, 0x8cb, -1, 0x8cc, -1,
            0x8cd, 0x8ce, -1, 0x8cf, 0x8d0, -1, 0xc90, -1, 0xc91, 0xc92,
        };

        /// <summary>A style-1 title bar of any width: titlebar_left, titlebar_mid tiles, titlebar_right.</summary>
        private static void DrawTitleBar(RoClassicWindow w, int width) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < width - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(width - 12, 0, Basic + "titlebar_right.bmp");
        }

        private RoClassicWindow SideWindow(string cls, int id, int w, int h, Action<RoClassicWindow> draw, Action close) {
            var layout = new RoClassicWindowLayout { Class = cls, WindowId = id, Width = w, Height = h };
            var x = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = w - 14, Y = 3, W = 11, H = 11, Tooltip = RoClassicUiHost.MsgStr(0x89a, "Exit") };
            x.States["normal"] = Basic + "sys_close_off.bmp";
            x.States["hover"] = Basic + "sys_close_on.bmp";
            x.States["pressed"] = Basic + "sys_close_on.bmp";
            layout.Children.Add(x);
            var window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += draw;
            window.Clicked += child => {
                if (child.Id == CloseId) close();
            };
            window.EscClose = () => {
                close();
                return true;
            };
            return window;
        }

        // ---------------------------------------------------------------- 0x13a route list

        /// <summary>0x1b6: toggles the route list; it is made only while the route has steps.</summary>
        private void ToggleRouteList() {
            if (routeList != null && routeList.Visible) {
                routeList.Visible = false;
                return;
            }

            if (route.Count == 0 && target == null) return;
            if (routeList == null) {
                routeList = SideWindow("UINavigationRuideWnd", 0x13a, 150, H, DrawRouteList, () => routeList.Visible = false);
                routeList.Wheeled += (w, local, delta) => {
                    routeFirst = Mathf.Clamp(routeFirst + (delta > 0 ? -6 : 6), 0, Math.Max(0, RouteRows().Count - 293 / RowH));
                    routeList.Invalidate();
                };
            }

            routeFirst = 0;
            routeList.MoveTo(window.Position.x + W, window.Position.y);
            routeList.Visible = true;
            system.Windows.BringToFront(routeList);
            routeList.Invalidate();
        }

        /// <summary>591a20: a row per step "map (x y)" (the first with the start icon, the others the route icon), the target last.</summary>
        private List<(string Icon, string Text)> RouteRows() {
            var rows = new List<(string, string)>();
            for (int i = 0; i < route.Count; i++) {
                RoNaviLink link = route[i];
                rows.Add((N3 + (i == 0 ? "icon_list_start.bmp" : "icon_list_riute.bmp"),
                    MapName(link.Map) + " (" + link.X.ToString(CultureInfo.InvariantCulture) + " " + link.Y.ToString(CultureInfo.InvariantCulture) + ")"));
            }

            if (target != null) {
                bool cell = target.Kind == RoNaviKind.Cell || target.Kind == RoNaviKind.Npc;
                rows.Add((N3 + "icon_list_target.bmp", cell
                    ? MapName(target.Map) + " (" + target.X.ToString(CultureInfo.InvariantCulture) + " " + target.Y.ToString(CultureInfo.InvariantCulture) + ")"
                    : MapName(target.Map)));
            }

            return rows;
        }

        private void DrawRouteList(RoClassicWindow w) {
            w.FillRect(0, 0, w.Width, w.Height, Line);
            DrawTitleBar(w, w.Width);
            w.TextOut(17, 4, RoClassicUiHost.MsgStr(0xc84, "navigation UI"), 0, 12, White);
            w.TextOut(16, 3, RoClassicUiHost.MsgStr(0xc84, "navigation UI"), 0, 12, Black);
            List<(string Icon, string Text)> rows = RouteRows();
            DrawList(w, 2, 20, w.Width - 4, 293, White, rows.Count, routeFirst, -1, i => rows[i]);
        }

        // ---------------------------------------------------------------- 0x132 icon set

        /// <summary>0xb9: toggles the icon set window under the navigation window.</summary>
        private void ToggleIconSet() {
            if (iconWindow != null && iconWindow.Visible) {
                iconWindow.Visible = false;
                return;
            }

            if (iconWindow == null) {
                iconWindow = SideWindow("UINavigationroadiconWnd", 0x132, 158, 102, DrawIconSet, () => iconWindow.Visible = false);
                iconWindow.Pressed += (w, local) => {
                    // 58fc10: rows y 25..55 / 63..91, columns x 11..41, 47..77, 83..113, 119..149 → set 1..4 / 5..8
                    int row = local.y >= 25 && local.y <= 55 ? 0 : local.y >= 63 && local.y <= 91 ? 1 : -1;
                    int col = -1;
                    for (int k = 0; k < 4; k++) {
                        if (local.x >= 11 + 36 * k && local.x <= 41 + 36 * k) col = k;
                    }

                    if (row < 0 || col < 0) return;
                    iconSet = 1 + row * 4 + col;
                    ApplyIconSetButton();
                    foreach (Tile tile in tiles) tile.Cell = new Vector2Int(int.MinValue, int.MinValue);
                    window.Invalidate();
                    iconWindow.Invalidate();
                };
            }

            iconWindow.MoveTo(window.Position.x + 113, window.Position.y + 211);
            iconWindow.Visible = true;
            system.Windows.BringToFront(iconWindow);
            iconWindow.Invalidate();
        }

        private void DrawIconSet(RoClassicWindow w) {
            DrawTitleBar(w, w.Width);
            w.TextOut(17, 4, RoClassicUiHost.MsgStr(0xc83, "Navigation guide icon set"), 0, 12, White);
            w.TextOut(16, 3, RoClassicUiHost.MsgStr(0xc83, "Navigation guide icon set"), 0, 12, Black);
            w.DrawBitmapRaw(0, 17, N3 + "bg_roadiocn_select.bmp");
            int k = iconSet;
            int x = k <= 4 ? 36 * k - 26 : 36 * (k - 4) - 26, y = k <= 4 ? 24 : 60;
            w.DrawBitmap(x, y, N3 + "img_roadiocn_bg_select.bmp");
        }

        // ---------------------------------------------------------------- 0xe5 help

        /// <summary>0x153: toggles the help window, the instruction lines (MsgStr 0x8c1..0x8d0, 0xc90..0xc92) wrapped to the viewer.</summary>
        private void ToggleHelp() {
            if (helpWindow != null && helpWindow.Visible) {
                helpWindow.Visible = false;
                return;
            }

            if (helpWindow == null) {
                helpWindow = SideWindow("UINavigationHelpWnd", 0xe5, 310, 200, DrawHelp, () => helpWindow.Visible = false);
                helpWindow.Wheeled += (w, local, delta) => {
                    helpFirst = Mathf.Clamp(helpFirst + (delta > 0 ? -3 : 3), 0, Math.Max(0, helpLines.Count - 173 / 14));
                    helpWindow.Invalidate();
                };
            }

            helpFirst = 0;
            helpLines.Clear();
            helpWindow.MoveTo(100, 100);
            helpWindow.Visible = true;
            system.Windows.BringToFront(helpWindow);
            helpWindow.Invalidate();
        }

        private void DrawHelp(RoClassicWindow w) {
            DrawTitleBar(w, w.Width);
            w.TextOut(17, 4, RoClassicUiHost.MsgStr(0x89d, "Help"), 0, 12, White);
            w.TextOut(16, 3, RoClassicUiHost.MsgStr(0x89d, "Help"), 0, 12, Black);
            w.FillRect(0, 17, w.Width, w.Height - 17, White);
            w.FillRect(5, 22, w.Width - 10, w.Height - 27, new Color32(210, 210, 210, 255));
            RoClassicGdiFont font = w.FontFor(0, 12);
            int width = w.Width - 10 - 17;
            if (helpLines.Count == 0) {
                foreach (int id in HelpIds) {
                    string text = id < 0 ? " " : RoClassicUiHost.MsgStr(id, string.Empty);
                    while (text.Length > 0) {
                        int n = text.Length;
                        while (n > 1 && font.Measure(text.Substring(0, n)) > width) n--;
                        int space = n < text.Length ? text.LastIndexOf(' ', n - 1, n) : -1;
                        int cut = space > 0 ? space : n;
                        helpLines.Add(text.Substring(0, cut));
                        text = text.Substring(cut).TrimStart(' ');
                    }
                }
            }

            int rows = 173 / 14;
            for (int r = 0; r < rows && helpFirst + r < helpLines.Count; r++) w.TextOut(8, 24 + 14 * r, helpLines[helpFirst + r], 0, 12, Black);
            if (helpLines.Count > rows) w.DrawScrollBar(w.Width - 18, 22, w.Height - 27, helpLines.Count - rows, helpLines.Count, rows, helpFirst);
        }
    }
}
