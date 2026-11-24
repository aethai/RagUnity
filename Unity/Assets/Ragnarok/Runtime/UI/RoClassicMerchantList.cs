using System;
using System.Collections.Generic;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>Player shop panes, matching reconstruction/apps/native_merchant_shop.cpp (0x2b/0x2c and 0xb1/0xb2).</summary>
    public static class RoClassicMerchantList {
        private const string Ui = "유저인터페이스\\";
        public const int Cell = 32;
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 Selected = new Color32(0x70, 0x9f, 0xed, 255);

        public static int Rows(int height) => Math.Max(1, (height - 38) / Cell);
        public static int HeightForItems(int count) => Math.Max(4, count) * Cell + 56;

        public static int RowAt(Vector2Int local, int width, int height, int first, int count) {
            if (local.x < 40 || local.x >= width - 20 || local.y < 17 || local.y >= 17 + Rows(height) * Cell) return -1;
            int row = first + (local.y - 17) / Cell;
            return row < count ? row : -1;
        }

        public static Vector2Int SnapSize(int width, int height, bool buyingOffers = false) => new Vector2Int(
            280 + (Mathf.Clamp(width, 232, buyingOffers ? 426 : 320) - 280) / Cell * Cell,
            120 + (Mathf.Clamp(height, 120, 336) - 120) / Cell * Cell);

        public static void BuildFrame(List<RoClassicOp> ops, int width, int height) {
            ops.Clear();
            RoClassicFrames.Fill(ops, 20, 17, width - 40, height - 38, White);
            RoClassicFrames.TitleBar(ops, width, false);
            RoClassicFrames.ItemColumns(ops, width, height - 38);
            RoClassicFrames.ButtonBar(ops, width, height, 2);
            RoClassicFrames.IconColumn(ops, 40, Rows(height));
            RoClassicFrames.Bitmap(ops, Ui + "btn_resize.bmp", width - 15, height - 15, false);
        }

        public static void ApplySize(RoClassicWindow window, int width, int height) {
            window.SetWidth(width);
            window.SetHeight(height);
            BuildFrame(window.Layout.Ops, width, height);
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                if (child.Class == "UIResizer") { child.X = width - 15; child.Y = height - 15; }
                else if (child.Class == "UIStaticText" && child.Y > 17) { child.X = 12; child.Y = height - 21; }
                else if (child.Class == "UIBitmapButton") { child.X = child.Id == 0xb4 ? width - 106 : width - 61; child.Y = height - 24; }
                else if (child.Class == "UIScrollBar") child.Visible = false;
            }
            window.Invalidate();
        }

        internal static void Configure(RoClassicWindow window, string prefs, bool buyingOffers, Action resized) {
            int width = PlayerPrefs.GetInt(prefs + ".w", window.Width);
            int height = PlayerPrefs.GetInt(prefs + ".h", window.Height);
            Vector2Int size = SnapSize(width, height, buyingOffers);
            ApplySize(window, size.x, size.y);
            window.ResizerModeAt = local => local.x >= window.Width - 15 && local.x < window.Width &&
                local.y >= window.Height - 15 && local.y < window.Height ? 7 : -1;
            window.ResizeRequested += (w, mode, requestedWidth, requestedHeight) => {
                if (mode != 7) return;
                Vector2Int snapped = SnapSize(requestedWidth, requestedHeight, buyingOffers);
                if (snapped.x == w.Width && snapped.y == w.Height) return;
                ApplySize(w, snapped.x, snapped.y);
                PlayerPrefs.SetInt(prefs + ".w", snapped.x);
                PlayerPrefs.SetInt(prefs + ".h", snapped.y);
                resized?.Invoke();
            };
        }

        internal static void SizeToItems(RoClassicWindow window, int count, Vector2Int client) {
            int height = HeightForItems(count);
            if (window.Height != height) ApplySize(window, window.Width, height);
            window.MoveTo(Mathf.Clamp(window.Position.x, 0, Math.Max(0, client.x - window.Width)),
                Mathf.Clamp(window.Position.y, 0, Math.Max(0, client.y - window.Height)));
        }

        /// <summary>
        /// One item row as the client draws it: the offer list 0x8e33e0 (0x2b / 0xb1) puts the price " %s " in its price
        /// colours before a black "Z" right-aligned at W − 18 and cuts the name at min(W − Z − price − 106, 130); the
        /// selection list 0x8e2ed0 (0x2c / 0xb2) has a 90 px name and " %s Zeny" right-aligned at W − 18, all on row + 29.
        /// </summary>
        internal static void DrawRow(RoClassicWindow window, int row, RoItemInstance item, RoItemMetadataTable metadata,
            int amount, long price, bool offer, bool selected) {
            int y = Cell * row;
            if (selected) window.FillRect(72, y + 19, 191, 28, Selected);
            RoClassicItemText.DrawIcon(window, 44, y + 21, item, metadata, amount);
            RoClassicGdiFont font = window.FontFor(0, 12);
            string priceText = " " + RoClassicItemText.Commas(price) + " ";
            int zWidth = font.Measure("Z"), priceWidth = font.Measure(priceText);
            int nameWidth = offer ? Math.Min(130, window.Width - priceWidth - zWidth - 106) : 90;
            window.ClipTo(76, y + 19, Math.Max(1, nameWidth), 28);
            RoClassicItemText.DrawName(window, 76, y + 29, Math.Max(1, nameWidth), item, metadata);
            window.ResetClip();
            if (offer) {
                DrawPrice(window, window.Width - 18 - zWidth - priceWidth, y + 29, price, priceText);
                window.TextOut(window.Width - 18 - zWidth, y + 29, "Z", 0, 12, Black);
            } else {
                priceText = " " + RoClassicItemText.Commas(price) + " Zeny";
                window.TextOut(window.Width - 18 - font.Measure(priceText), y + 29, priceText, 0, 12, Black);
            }
        }

        private static void DrawPrice(RoClassicWindow w, int x, int y, long price, string text) {
            uint first, second = 0;
            bool twice = true;
            if (price >= 1000000000) { first = 0x7f00ff; second = 0xff; }
            else if (price >= 100000000) first = 0x64c8c8;
            else if (price >= 10000000) { first = 0xff; twice = false; }
            else if (price >= 1000000) first = 0xff00;
            else if (price >= 100000) { first = 0xff0000; twice = false; }
            else if (price >= 10000) { first = 0xff1eff; twice = false; }
            else if (price >= 1000) { first = 0xffff; second = 0xff; }
            else if (price >= 100) { first = 0xffff00; second = 0xff0000; }
            else if (price >= 10) { first = 0xc800c8; second = 0xff0000; }
            else first = 0xffff00;
            w.TextOut(x, y, text, 0, 12, ColorRef(first));
            if (twice) w.TextOut(x - 1, y, text, 0, 12, ColorRef(second));
        }

        private static Color32 ColorRef(uint color) => new Color32((byte) color, (byte) (color >> 8), (byte) (color >> 16), 255);
    }
}
