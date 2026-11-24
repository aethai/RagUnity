using System.Globalization;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Item drawing shared by the classic list windows (storage, shops; ui/RUNTIME-RULES-ITEMS.md §0/§2e):
    /// DrawItemIcon 0x809bc0, ItemName 0x82dff0 and the client's thousands separator 0xa21e30.
    /// </summary>
    internal static class RoClassicItemText {
        private const string Ui = "유저인터페이스\\";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Shadow = new Color32(0xe0, 0xe0, 0xe0, 255);
        private static readonly Color32[] NameColors = {
            new Color32(0, 0, 0, 255), new Color32(0x66, 0x99, 0x00, 255), new Color32(0x00, 0xbf, 0xff, 255),
            new Color32(0xff, 0xa5, 0x00, 255), new Color32(0x94, 0x00, 0xd3, 255),
        };

        /// <summary>0xa21e30: decimal with ',' every three digits.</summary>
        public static string Commas(long value) => value.ToString("#,0", CultureInfo.InvariantCulture);

        public static string IconPath(RoItemInstance item, RoItemMetadataTable table) {
            RoItemMetadata metadata = item.ResolveMetadata(table);
            if (metadata == null) return null;
            // one path per resource name: every repaint of an item window asked for each icon's path anew
            string resource = metadata.GetResourceName(item.IsIdentified) ?? string.Empty;
            if (!IconPaths.TryGetValue(resource, out string path)) {
                path = Ui + "item\\" + resource + ".bmp";
                IconPaths[resource] = path;
            }

            return path;
        }

        // the amounts under the icons as text, made once for the usual counts (every repaint of an item window drew them anew)
        private static readonly string[] SmallAmounts = new string[10000];

        private static string AmountText(int amount) {
            if (amount < 0 || amount >= SmallAmounts.Length) return amount.ToString(CultureInfo.InvariantCulture);
            return SmallAmounts[amount] ??= amount.ToString(CultureInfo.InvariantCulture);
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> IconPaths =
            new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);

        /// <summary>
        /// DrawItemIcon 0x809bc0: the keyed 24×24 icon, the amount (when given) in font 0 h11, black on a white 4-way
        /// outline, at x + 14 below 1000 (else x), y + 15; the grade icon 12 px lower.
        /// </summary>
        public static void DrawIcon(RoClassicWindow w, int x, int y, RoItemInstance item, RoItemMetadataTable table, int amount) =>
            DrawIcon(w, x, y, item, table, amount, false);

        /// <summary>
        /// DrawItemIcon 0x809bc0, or with <paramref name="grey"/> the sold-out icon 0x8098e0: the icon through TintBlit mode 1
        /// (v = max((r + g + b) / 3, 120)) with the same amount text.
        /// </summary>
        public static void DrawIcon(RoClassicWindow w, int x, int y, RoItemInstance item, RoItemMetadataTable table, int amount, bool grey) {
            string icon = IconPath(item, table);
            if (icon != null && grey) icon = RoClassicUiImages.Tinted(icon, 1);
            if (icon != null && !w.DrawBitmap(x, y, icon)) w.DrawBitmap(x, y, Ui + "item\\am_berserkpitcher.bmp");
            if (amount != 0 && amount != -1) {
                string text = AmountText(amount);
                int tx = x + (amount < 1000 ? 14 : 0);
                w.TextOut(tx + 1, y + 15, text, 0, 11, White);
                w.TextOut(tx - 1, y + 15, text, 0, 11, White);
                w.TextOut(tx, y + 16, text, 0, 11, White);
                w.TextOut(tx, y + 14, text, 0, 11, White);
                w.TextOut(tx, y + 15, text, 0, 11, Black);
            }

            if (item.EnchantGrade > 0) {
                w.DrawBitmap(x, y + 12, Ui + "grade_enchant\\grade_icon" + item.EnchantGrade.ToString(CultureInfo.InvariantCulture) + ".bmp");
            }
        }

        /// <summary>FUN_0065d2e0: the enchant count (cards past the item's own slots) picks the colour.</summary>
        public static Color32 NameColor(RoItemInstance item, RoItemMetadataTable table) {
            if (!item.IsIdentified) return Black;
            RoItemMetadata metadata = item.ResolveMetadata(table);
            int slots = metadata != null ? metadata.SlotCount : 0, enchants = 0;
            for (int i = slots; i < RoItemInstance.MaxCardSlots; i++) {
                if (item.GetCardId(i) > 0) enchants++;
            }

            return NameColors[Mathf.Clamp(enchants, 0, NameColors.Length - 1)];
        }

        /// <summary>
        /// ItemName 0x82dff0 → 0x86a880: font 0 h12 with an #e0e0e0 shadow at (+1, +1) in the enchant colour; one line
        /// when it fits, else up to two lines from 6 px higher, 13 px apart, the second cut with "..." when the rest does
        /// not fit.
        /// </summary>
        public static void DrawName(RoClassicWindow w, int x, int y, int width, RoItemInstance item, RoItemMetadataTable table) {
            string name = item.GetFullDisplayName(table) ?? string.Empty;
            Color32 color = NameColor(item, table);
            RoClassicGdiFont font = w.FontFor(0, 12);
            if (font.Measure(name) < width) {
                w.TextOut(x + 1, y + 1, name, 0, 12, Shadow);
                w.TextOut(x, y, name, 0, 12, color);
                return;
            }

            y -= 6;
            int line = 1, pen = 0;
            for (int i = 0; i < name.Length; i++) {
                string ch = name[i].ToString();
                int cw = font.Measure(ch);
                if (line == 2 && pen + cw >= width - 15 && pen + font.Measure(name.Substring(i)) > width) {
                    w.TextOut(x + pen + 1, y + 1, "...", 0, 12, Shadow);
                    w.TextOut(x + pen, y, "...", 0, 12, color);
                    return;
                }

                w.TextOut(x + pen + 1, y + 1, ch, 0, 12, Shadow);
                w.TextOut(x + pen, y, ch, 0, 12, color);
                pen += cw;
                if (pen > width) {
                    pen = 0;
                    y += 13;
                    if (++line > 2) return;
                }
            }
        }

        private static readonly Color32[] TooltipColors = {
            new Color32(255, 255, 255, 255), new Color32(0xad, 0xff, 0x2f, 255), new Color32(0x00, 0xbf, 0xff, 255),
            new Color32(0xff, 0xa5, 0x00, 255), new Color32(0x94, 0x00, 0xd3, 255),
        };

        /// <summary>FUN_00ce9980: unidentified #aaaaaa, else the enchant colour, white without.</summary>
        public static Color32 TooltipColor(RoItemInstance item, RoItemMetadataTable table) {
            if (!item.IsIdentified) return new Color32(0xaa, 0xaa, 0xaa, 255);
            RoItemMetadata metadata = item.ResolveMetadata(table);
            int slots = metadata != null ? metadata.SlotCount : 0, enchants = 0;
            for (int i = slots; i < RoItemInstance.MaxCardSlots; i++) {
                if (item.GetCardId(i) > 0) enchants++;
            }

            return TooltipColors[Mathf.Clamp(enchants, 0, TooltipColors.Length - 1)];
        }

        /// <summary>The number of random options on an identified item (the "[%d Option]" tooltips).</summary>
        public static int OptionCount(RoItemInstance item) {
            int options = 0;
            if (item.IsIdentified && item.HasRandomOptions) {
                foreach (RoItemRandomOption option in item.CopyRandomOptions()) {
                    if (option.Index != 0) options++;
                }
            }

            return options;
        }

        /// <summary>The window-local tooltip anchor of list rows: (40 + trunc32(x − 40), 1 + trunc32(y − 17)).</summary>
        public static Vector2Int RowTooltipAnchor(Vector2Int local) =>
            new Vector2Int(40 + (local.x - 40) / 32 * 32, 1 + (local.y - 17) / 32 * 32);
    }
}
