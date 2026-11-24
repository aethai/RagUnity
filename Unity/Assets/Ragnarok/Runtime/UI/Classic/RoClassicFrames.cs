using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// The frames the item windows draw in OnDraw (title bar, item columns, button bars, white fills;
    /// ui/RUNTIME-RULES-STORE-SHOP.md §3c/§3h/§4b–d), as layout ops for any size: a resized window swaps its ops and
    /// draws exactly like the oracle's capture at the default size.
    /// </summary>
    public static class RoClassicFrames {
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        public static void Bitmap(List<RoClassicOp> ops, string path, int x, int y, bool raw = true) =>
            ops.Add(new RoClassicOp { Kind = RoClassicOpKind.Bitmap, Path = path, X = x, Y = y, RawCopy = raw });

        public static void Fill(List<RoClassicOp> ops, int x, int y, int w, int h, Color32 color) {
            if (w <= 0 || h <= 0) return;
            ops.Add(new RoClassicOp { Kind = RoClassicOpKind.Fill, X = x, Y = y, W = w, H = h, Color = color });
        }

        private static int Ceil(int value, int step) => value <= 0 ? 0 : (value + step - 1) / step;

        /// <summary>TitleBar 0x82f2d0 without its text: left, right, the middle tiled from 12; style 1 adds sys_base_off at (3, 3).</summary>
        public static void TitleBar(List<RoClassicOp> ops, int w, bool sysBase) {
            Bitmap(ops, Basic + "titlebar_left.bmp", 0, 0);
            Bitmap(ops, Basic + "titlebar_right.bmp", w - 12, 0);
            for (int i = 0, n = Ceil(w - 24, 12); i < n; i++) Bitmap(ops, Basic + "titlebar_mid.bmp", 12 + 12 * i, 0);
            if (sysBase) Bitmap(ops, Basic + "sys_base_off.bmp", 3, 3, false);
        }

        /// <summary>
        /// DrawBody 0x82cc10 under a 17 px title bar: the #C8C8C8 rounded outline (RoundOutline 0x9ac670's rectangles), its
        /// top 5 rows filled, white inside.
        /// </summary>
        public static void Body(List<RoClassicOp> ops, int w, int h) {
            var grey = new Color32(0xc8, 0xc8, 0xc8, 255);
            Fill(ops, 3, 17, w - 6, 1, grey);
            Fill(ops, 3, h - 1, w - 6, 1, grey);
            Fill(ops, 0, 20, 1, h - 23, grey);
            Fill(ops, w - 1, 20, 1, h - 23, grey);
            Fill(ops, 1, 18, 2, 1, grey);
            Fill(ops, 1, h - 2, 2, 1, grey);
            Fill(ops, w - 3, 18, 2, 1, grey);
            Fill(ops, w - 3, h - 2, 2, 1, grey);
            Fill(ops, 1, 19, 1, 1, grey);
            Fill(ops, 1, h - 3, 1, 1, grey);
            Fill(ops, w - 2, 19, 1, 1, grey);
            Fill(ops, w - 2, h - 3, 1, 1, grey);
            Fill(ops, 0, 17, w, 5, grey);
            Fill(ops, 1, 17, w - 2, h - 19, White);
            Fill(ops, 2, h - 2, w - 4, 1, White);
        }

        /// <summary>
        /// The item list body of UIItemBaseWnd frames: itemwin_left / itemwin_right every 8 px down <paramref name="bodyH"/>,
        /// then white at (20, 17) (W − 60) % 32 wide and under the last whole 32 px row.
        /// </summary>
        public static void ItemColumns(List<RoClassicOp> ops, int w, int bodyH) {
            int n = Ceil(bodyH, 8);
            for (int k = 0; k < n; k++) Bitmap(ops, Basic + "itemwin_left.bmp", 0, 17 + 8 * k);
            for (int k = 0; k < n; k++) Bitmap(ops, Basic + "itemwin_right.bmp", w - 20, 17 + 8 * k);
            Fill(ops, 20, 17, (w - 60) % 32, bodyH, White);
            Fill(ops, 20, bodyH / 32 * 32 + 17, w - 24, bodyH % 32, White);
        }

        /// <summary>
        /// BtnBar: style 0 btnbar_left/right/mid (21 tall), 2 btnbar_*2 (29), 3 btnbar_*3 (57), at the bottom, the middle
        /// tiled 21 px from x 21.
        /// </summary>
        public static void ButtonBar(List<RoClassicOp> ops, int w, int h, int style) {
            string suffix = style == 0 ? string.Empty : style.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int barH = style == 3 ? 57 : style == 2 ? 29 : 21;
            Bitmap(ops, Basic + "btnbar_left" + suffix + ".bmp", 0, h - barH);
            Bitmap(ops, Basic + "btnbar_right" + suffix + ".bmp", w - 21, h - barH);
            for (int i = 0, n = Ceil(w - 42, 21); i < n; i++) Bitmap(ops, Basic + "btnbar_mid" + suffix + ".bmp", 21 + 21 * i, h - barH);
        }

        /// <summary>itemwin_mid, the icon cell, for <paramref name="rows"/> rows at (x, 17 + 32r).</summary>
        public static void IconColumn(List<RoClassicOp> ops, int x, int rows) {
            for (int r = 0; r < rows; r++) Bitmap(ops, Basic + "itemwin_mid.bmp", x, 17 + 32 * r);
        }

        /// <summary>
        /// Whether two op lists draw the same (kind, path, position, size, colour, raw flag): the generated frame against the
        /// oracle's capture at the default size.
        /// </summary>
        public static bool SameOps(List<RoClassicOp> a, List<RoClassicOp> b, out string firstDifference) {
            firstDifference = null;
            int n = Mathf.Max(a.Count, b.Count);
            for (int i = 0; i < n; i++) {
                RoClassicOp x = i < a.Count ? a[i] : null, y = i < b.Count ? b[i] : null;
                if (x == null || y == null) {
                    firstDifference = "op " + i + ": " + (x == null ? "missing" : Describe(x)) + " vs " + (y == null ? "missing" : Describe(y));
                    return false;
                }

                bool same = x.Kind == y.Kind && x.X == y.X && x.Y == y.Y && string.Equals(x.Path, y.Path, System.StringComparison.OrdinalIgnoreCase) &&
                    (x.Kind != RoClassicOpKind.Fill || (x.W == y.W && x.H == y.H && x.Color.Equals(y.Color))) &&
                    (x.Kind != RoClassicOpKind.Bitmap || x.RawCopy == y.RawCopy);
                if (!same) {
                    firstDifference = "op " + i + ": " + Describe(x) + " vs " + Describe(y);
                    return false;
                }
            }

            return true;
        }

        private static string Describe(RoClassicOp op) =>
            op.Kind + " " + (op.Path ?? string.Empty) + " " + op.X + "," + op.Y + " " + op.W + "x" + op.H + (op.RawCopy ? " raw" : string.Empty);
    }
}
