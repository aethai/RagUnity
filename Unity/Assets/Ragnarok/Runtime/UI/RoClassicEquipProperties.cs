using System;
using System.Collections.Generic;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// CUIEquipmentPropertiesWnd 0x2711 "Attributes" (item-windows-spec §4): 300 × 308 at (0, 0), the equip window's ability
    /// button 0x242 and Ctrl+Q toggle it. Eight vertical tabs at (3, 18 + 33·i) (Total, then the Order categories 1..7), the
    /// list (21, 17) 279 × (H − 38) of 18-px "value#label" rows: the value centred in 44 px, blue #002AFF or red #FF003C for a
    /// "~" line, the label from x 49 (cut with "..." and a tooltip); zebra #E3EEFE, the itemwin_left strip, the button bar,
    /// a #C3C3C3 outline. The resizer stretches it to 470 in 18-px steps.
    /// </summary>
    public sealed class RoClassicEquipProperties : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string Tabs = Ui + "equipmentpropertieswnd\\";
        private const int W = 300, MinH = 308, MaxH = 470, Row = 18, CloseId = 0xc5;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Blue = new Color32(0x00, 0x2a, 0xff, 255);
        private static readonly Color32 Red = new Color32(0xff, 0x00, 0x3c, 255);
        private static readonly Color32 Zebra = new Color32(0xe3, 0xee, 0xfe, 255);
        private static readonly Color32 Edge = new Color32(0xc0, 0xc0, 0xc0, 255);
        private static readonly Color32 Outline = new Color32(0xc3, 0xc3, 0xc3, 255);
        private static readonly Color32 Pale = new Color32(0xf7, 0xf7, 0xf7, 255);
        private static RoClassicEquipProperties instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private int tab, first, seenSerial = -1;
        private List<string> lines = new List<string>();

        /// <summary>Ctrl+Q (hotkey 161) and the equip window's 0x242: if (!DeleteWindow(0x2711)) MakeWindow(0x2711).</summary>
        public static bool Toggle() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return false;
            if (instance == null || instance.system != system) {
                instance = new RoClassicEquipProperties { system = system };
                system.Register(instance);
            }

            RoClassicEquipProperties p = instance;
            if (p.window != null && p.window.Visible) {
                p.window.Visible = false;
                return true;
            }

            p.Open();
            return true;
        }

        private void Open() {
            RoEquipProperties.Ensure();
            if (window == null) {
                var layout = new RoClassicWindowLayout { Class = "CUIEquipmentPropertiesWnd", WindowId = 0x2711, Width = W, Height = MinH };
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = 286, Y = 3, W = 11, H = 11 };
                close.States["normal"] = Basic + "sys_close_off.bmp";
                close.States["hover"] = close.States["pressed"] = Basic + "sys_close_on.bmp";
                layout.Children.Add(close);
                window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = p => p.y < 17 };
                window.DrawDynamic += Draw;
                window.Clicked += child => {
                    if (child.Id == CloseId) window.Visible = false;
                };
                window.EscClose = () => {
                    window.Visible = false;
                    return true;
                };
                window.Pressed += (w, p) => {
                    // the vertical tabs (3, 18 + 33·i) 18 × 34
                    if (p.x >= 3 && p.x < 21 && p.y >= 18 && p.y < 18 + 33 * 8 + 1) {
                        int t = Math.Min(7, (p.y - 18) / 33);
                        if (t != tab) {
                            tab = t;
                            first = 0;
                            Refill();
                        }
                    }
                };
                window.Wheeled += (w, p, delta) => {
                    int f = Mathf.Clamp(first + (delta > 0 ? -1 : 1), 0, Math.Max(0, lines.Count - VisibleRows));
                    if (f == first) return;
                    first = f;
                    w.Invalidate();
                };
                // the resizer (288, H − 13) 13 × 13: height only, 308..470 in 18-px steps
                window.ResizerModeAt = p => p.x >= W - 12 && p.x < W - 12 + 13 && p.y >= window.Height - 13 ? 7 : -1;
                window.ResizeRequested += (w, mode, width, height) => {
                    int h = Mathf.Clamp(MinH + (height - MinH) / Row * Row, MinH, MaxH);
                    if (h == w.Height) return;
                    w.SetHeight(h);
                    first = Mathf.Clamp(first, 0, Math.Max(0, lines.Count - VisibleRows));
                    w.Invalidate();
                };
                window.MoveTo(0, 0);
            }

            tab = 0;
            first = 0;
            Refill();
            window.Visible = true;
            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
        }

        private int ListH => window.Height - 38;
        private int VisibleRows => ListH / Row;

        /// <summary>0x95eac0: the tab's lines from GetLines (0 = Total, all categories).</summary>
        private void Refill() {
            lines = RoEquipProperties.Instance?.Lines(tab) ?? new List<string>();
            first = Mathf.Clamp(first, 0, Math.Max(0, lines.Count - VisibleRows));
            window?.Invalidate();
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (window == null || !window.Visible) return;
            // msg 0x3c from the manager
            if (seenSerial != RoEquipProperties.Serial || (RoEquipProperties.Instance != null && RoEquipProperties.Instance.Ready && lines.Count == 0 && seenSerial == -1)) {
                seenSerial = RoEquipProperties.Serial;
                Refill();
            }

            // a cut label shows its full text (CUI fit 1)
            if (ui.Windows.HoverWindow != window) return;
            Vector2Int p = ui.Windows.Mouse - window.Position;
            int r = (p.y - 17) / Row;
            if (p.x < 21 + 49 || p.x >= 21 + 49 + 216 || p.y < 17 || r >= VisibleRows || first + r >= lines.Count) return;
            (string _, string label) = Split(lines[first + r]);
            if (window.FontFor(0, 12).Measure(label) > 216) RoClassicTooltip.Shared.Show(label, White, ui.Windows.Mouse.x + 10, ui.Windows.Mouse.y + 10);
        }

        private static (string Value, string Label) Split(string line) {
            int hash = line.IndexOf('#');
            return hash < 0 ? (string.Empty, line) : (line.Substring(0, hash), line.Substring(hash + 1));
        }

        /// <summary>OnDraw 0x95e750, then the title bar, the rows, the tabs and the resizer.</summary>
        private void Draw(RoClassicWindow w) {
            int h = w.Height, listY = 17, listH = ListH;
            w.FillRect(1, 1, W - 2, h - 2, White);
            for (int k = 0; k < (listH + 7) / 8; k++) w.DrawBitmapRaw(0, listY + 8 * k, Basic + "itemwin_left.bmp");
            int rows = listH / Row, parity = (first * Row / Row) & 1;
            for (int i = 1; i <= rows; i++) {
                if ((i & 1) == parity) w.FillRect(21, Row * i, 279 - 2, Row, Zebra);
            }

            w.FillRect(21 + 44, listY, 1, listH, Zebra);
            w.FillRect(20, listY, 1, h - listY - 21, Edge);
            w.DrawBitmapRaw(0, h - 21, Basic + "btnbar_left.bmp");
            for (int x = 21; x < W - 21; x += 21) w.DrawBitmapRaw(x, h - 21, Basic + "btnbar_mid.bmp");
            w.DrawBitmapRaw(W - 21, h - 21, Basic + "btnbar_right.bmp");
            w.FillRect(18, h - 21, 2, 1, Pale);
            foreach (RectInt r in RoClassicWindow.RoundOutlineRects(0, 0, W, h)) w.FillRect(r.x, r.y, r.width, r.height, Outline);

            // the title bar: three-part bitmaps, "Attributes" black on a white shadow, the base icon
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            w.TextOut(19, 4, "Attributes", 0, 12, White);
            w.TextOut(18, 3, "Attributes", 0, 12, Black);

            // the rows: the value centred in 44 px (blue, or red for "~"), the label from x 49 cut with "..."
            RoClassicGdiFont font = w.FontFor(0, 12);
            w.ClipTo(21, listY, 279, listH);
            for (int r = 0; r < rows && first + r < lines.Count; r++) {
                string line = lines[first + r];
                bool penalty = line.StartsWith("~", StringComparison.Ordinal);
                (string value, string label) = Split(penalty ? line.Substring(1) : line);
                int y = listY + Row * r + 4;
                w.TextOut(21 + (44 - font.Measure(value)) / 2, y, value, 0, 12, penalty ? Red : Blue);
                if (font.Measure(label) > 216) {
                    int n = label.Length;
                    while (n > 0 && font.Measure(label.Substring(0, n) + "...") > 216) n--;
                    label = label.Substring(0, n) + "...";
                }

                w.TextOut(21 + 49, y, label, 0, 12, Black);
            }

            w.ResetClip();
            w.DrawScrollBar(21 + 279 - 13, listY, listH, Math.Max(0, lines.Count - rows), lines.Count, rows, first);
            string[] names = { "tab_total", "tab_category1", "tab_category2", "tab_category3", "tab_category4", "tab_category5", "tab_category6", "tab_category7" };
            for (int i = 0; i < names.Length; i++) w.DrawBitmapRaw(3, 18 + 33 * i, Tabs + names[i] + (i == tab ? "_on.bmp" : "_off.bmp"));
            w.DrawBitmap(W - 12, h - 13, Ui + "btn_resize.bmp");
        }

        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            if (t[0] == "toggle") Toggle();
            else if (t[0] == "tab" && instance?.window != null && t.Length > 1) {
                instance.tab = int.Parse(t[1]);
                instance.first = 0;
                instance.Refill();
            }

            RoClassicEquipProperties p = instance;
            return (p?.window != null && p.window.Visible ? "open tab=" + p.tab + " rows=" + p.lines.Count + " " : "closed ") + RoEquipProperties.Describe();
        }
    }
}
