using System;
using System.Text;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Sharing a location (navi-share-worldmap-spec §1): "&lt;NAVIL&gt;" + map + B62(x) + B62(y) + "&lt;/NAVIL&gt;" (base 62 over
    /// 0-9a-zA-Z, at least two characters, a negative coordinate as 1000; 1000 / 1000 = the map only). Share (0x15e) puts an
    /// NPC's cell or a monster's map in the chat input as "&lt;map x,y&gt;", or opens page 2 where a click on the selected
    /// map shares that cell; the chat shows a received link as "&lt;map x,y&gt;" in #E1F282 and a click on it selects the
    /// location in the navigation (0xac5990: page 0, guidance only with "Navigation").
    /// </summary>
    public sealed partial class RoClassicNavigation {
        private const string Base62 = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string NaviOpen = "<NAVIL>", NaviClose = "</NAVIL>";
        private float shareHintUntil;

        /// <summary>0x795f40: base 62, most significant first, never fewer than two characters.</summary>
        public static string EncodeBase62(int v) {
            var s = new StringBuilder();
            do {
                s.Insert(0, Base62[v % 62]);
                v /= 62;
            } while (v >= 62);
            s.Insert(0, Base62[v]);
            return s.ToString();
        }

        /// <summary>0x795e30: positional base 62; a character outside the alphabet counts 0 but keeps its place.</summary>
        public static int DecodeBase62(string s) {
            int v = 0;
            foreach (char c in s) v = v * 62 + Math.Max(0, Base62.IndexOf(c));
            return v;
        }

        /// <summary>0x5928b0's payload.</summary>
        public static string NaviPayload(string map, int x, int y) =>
            NaviOpen + map + EncodeBase62(x < 0 ? 1000 : x) + EncodeBase62(y < 0 ? 1000 : y) + NaviClose;

        /// <summary>0x798560: the map and the last four characters before &lt;/NAVIL&gt; as x and y.</summary>
        public static bool TryParseNaviLink(string s, out string map, out int x, out int y) {
            map = null;
            x = y = 0;
            int close = s.IndexOf(NaviClose, StringComparison.Ordinal);
            if (close < 4) return false;
            x = DecodeBase62(s.Substring(close - 4, 2));
            y = DecodeBase62(s.Substring(close - 2, 2));
            string head = s.Substring(0, close - 4);
            int open = head.IndexOf(NaviOpen, StringComparison.Ordinal);
            if (open >= 0) map = head.Substring(open + NaviOpen.Length);
            return !string.IsNullOrEmpty(map);
        }

        /// <summary>0x7982d0: "map x,y" (or "map" when both are 1000 or more), in angle brackets when asked.</summary>
        public static string NaviLabel(string map, int x, int y, bool brackets) {
            string inner = x > 999 && y > 999 ? map : map + " " + x + "," + y;
            return brackets ? "<" + inner + ">" : inner;
        }

        /// <summary>
        /// 0x796220 for a received line: the first &lt;NAVIL&gt;…&lt;/NAVIL&gt; becomes its "&lt;label&gt;" (cut to
        /// <paramref name="maxWidth"/> px with "..."); false when there is none. A link without its closing tag blanks the line.
        /// </summary>
        public static bool ConvertNaviLine(string text, RoClassicGdiFont font, int maxWidth, out string line, out string label, out string payload) {
            line = text;
            label = payload = null;
            int open = text.IndexOf(NaviOpen, StringComparison.Ordinal);
            if (open < 0) return false;
            int close = text.IndexOf(NaviClose, open, StringComparison.Ordinal);
            if (close < 0) {
                line = string.Empty;
                return false;
            }

            payload = text.Substring(open, close + NaviClose.Length - open);
            if (!TryParseNaviLink(payload, out string map, out int x, out int y)) return false;
            string inner = NaviLabel(map, x, y, false);
            if (font != null && font.Measure(inner) > maxWidth) {
                int n = inner.Length;
                while (n > 0 && font.Measure(inner.Substring(0, n) + "...") > maxWidth) n--;
                inner = inner.Substring(0, n) + "...";
            }

            label = "<" + inner + ">";
            line = text.Substring(0, open) + label + text.Substring(close + NaviClose.Length);
            return true;
        }

        /// <summary>
        /// 0xac5990 (a chat link clicked): the map (x ≥ 1000) or the cell selected in the navigation window, page 0, shown;
        /// nothing when the map is not in Navi_Map.
        /// </summary>
        public static void SelectLocation(string payload) {
            RoClassicNavigation n = Ensure();
            RoNaviTables tables = Tables();
            if (n == null || tables == null || !TryParseNaviLink(payload, out string map, out int x, out int y) || !tables.HasMap(map)) return;
            var group = new RoNaviGroup();
            group.Members.Add(x >= 1000
                ? new RoNaviObject { Kind = RoNaviKind.Map, Map = map, Name = MapName(map) }
                : new RoNaviObject { Kind = RoNaviKind.Cell, Map = map, X = x, Y = y, Name = MapName(map) + " " + x + "," + y });
            n.groups = new System.Collections.Generic.List<RoNaviGroup> { group };
            n.groupSel = 0;
            n.memberSel = 0;
            n.groupFirst = n.memberFirst = 0;
            n.page = 0;
            n.minimized = false;
            n.Open();
        }

        /// <summary>0x15e: an NPC's cell or a monster's map goes to the chat input at once; a map or cell selection opens page 2.</summary>
        private void Share() {
            RoNaviObject sel = Selected;
            if (sel == null) return;
            if (sel.Kind == RoNaviKind.Npc) {
                InsertLink(sel.Map, sel.X, sel.Y);
            } else if (sel.Kind == RoNaviKind.Mob) {
                InsertLink(sel.Map, 1000, 1000);
            } else {
                page = 2;
                shareHintUntil = Time.unscaledTime + 5f;
                Relayout();
            }
        }

        private static void InsertLink(string map, int x, int y) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.InsertNaviLink(NaviLabel(map, x, y, true), NaviPayload(map, x, y));
        }

        /// <summary>The selected object's map size from Navi_Map.</summary>
        private bool SelectedMapSize(out string map, out int width, out int height) {
            map = Selected?.Map;
            width = height = 0;
            RoNaviTables tables = Tables();
            if (map == null || tables == null || !tables.TryGetMap(map, out RoNaviMap info)) return false;
            width = info.Width;
            height = info.Height;
            return width > 0 && height > 0;
        }

        /// <summary>Page 2 (58fa60): a click on the 266² map (3..268, 46..312) shares that cell of the selected map.</summary>
        private bool SharePressed(Vector2Int local) {
            if (page != 2 || local.x < 3 || local.x > 268 || local.y < 46 || local.y > 312) return false;
            if (!SelectedMapSize(out string map, out int w, out int h)) return true;
            float s = Math.Max(w, h) / 265f;
            float padX = w < h ? (h - w) / 2f : 0f, padY = h < w ? (w - h) / 2f : 0f;
            int cx = (int) ((local.x - 3) * s - padX);
            int cy = (int) ((Math.Max(w, h) - (local.y - 46) * s) - padY);
            if (cx >= 0 && cx < w && cy >= 0 && cy < h) InsertLink(map, cx, cy);
            return true;
        }

        /// <summary>
        /// Page 2: the selected map scaled to 266², its two names outlined at (9, 50) / (9, 65); for 5 s the hint (back.bmp at
        /// (12, 272), icon_mouse, MsgStr 0xc8f); the cross-hair at the mouse in #AA2200.
        /// </summary>
        private void DrawSharePage(RoClassicWindow w) {
            w.FillRect(1, 44, 270, 270, Line);
            w.FillRect(3, 46, 266, 266, Black);
            RoNaviObject sel = Selected;
            if (sel != null) {
                w.DrawStretched(3, 46, 266, 266, Ui + "map\\" + sel.Map + ".bmp", White);
                w.TextOutOutline(9, 50, sel.Name ?? string.Empty, 0, 11, White, Black, true);
                w.TextOutOutline(9, 65, MapName(sel.Map), 0, 11, White, Black, true);
            }

            if (Time.unscaledTime < shareHintUntil) {
                w.DrawStretched(12, 272, 251, 27, N3 + "back.bmp", new Color32(255, 255, 255, 0x66));
                w.DrawBitmap(18, 277, N3 + "icon_mouse.bmp");
                w.TextOutOutline(42, 280, RoClassicUiHost.MsgStr(0xc8f, "Click on the location to share it in a chat room"), 0, 11, White, Black);
            }

            RoClassicWindowManager windows = system.Windows;
            if (windows.HoverWindow != window) return;
            Vector2Int m = windows.Mouse - window.Position;
            var cross = new Color32(0xaa, 0x22, 0x00, 255);
            if (m.x > 3 && m.x < 269) w.FillRect(m.x, 46, 2, 266, cross);
            if (m.y >= 47 && m.y <= 311) w.FillRect(3, m.y, 266, 2, cross);
        }
    }
}
