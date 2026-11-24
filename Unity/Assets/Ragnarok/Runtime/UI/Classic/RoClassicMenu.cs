using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// The client's context menu, UIMenuWnd 0x12 (reference ui-static ui/RUNTIME-RULES-PARTY.md §3a): white with the
    /// sysboxs corners, every item one 14 px row (the client adds it as two 7 px entries), a separator a 7 px line, the
    /// hovered row #709FED, the width the widest text + 6. A release on an item answers its index; a press anywhere else
    /// closes it, and so does the next menu.
    /// </summary>
    public sealed class RoClassicMenu : IRoClassicUiProducer {
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Separator = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static readonly Color32 Hover = new Color32(0x70, 0x9f, 0xed, 255);

        private static RoClassicMenu instance;

        // the 7 px entries as the client keeps them: an item twice, a separator once
        private readonly List<(string Text, Color32 Color, bool Separator)> entries = new List<(string, Color32, bool)>();
        private readonly RoClassicWindow window;
        private Action<int> onPick;
        private int hover = -1;
        private int openedFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicMenu(RoClassicUiSystem system) {
            var layout = new RoClassicWindowLayout { Class = "UIMenuWnd", WindowId = 0x12, Width = 4, Height = 4 };
            window = new RoClassicWindow(system, layout) { Visible = false, Draggable = false, FrameDragArea = _ => false };
            window.DrawDynamic += Draw;
            window.Released += (w, local) => Release(local);
            system.Register(this);
        }

        public static bool IsOpen => instance != null && instance.window.Visible;

        private static RoClassicMenu Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.window == null) instance = new RoClassicMenu(system);
            return instance;
        }

        /// <summary>DeleteWindow(0x12) + msg 0x4b: an empty menu (every builder starts with this).</summary>
        public static void Begin() {
            RoClassicMenu menu = Ensure();
            if (menu == null) return;
            menu.window.Visible = false;
            menu.entries.Clear();
            menu.onPick = null;
            menu.hover = -1;
        }

        /// <summary>msg 0x1f: an item (two 7 px entries in its colour).</summary>
        public static void Add(string text, Color32 color) {
            if (instance == null) return;
            instance.entries.Add((text ?? string.Empty, color, false));
            instance.entries.Add((text ?? string.Empty, color, false));
        }

        public static void Add(string text) => Add(text, new Color32(0, 0, 0, 255));

        /// <summary>msg 0x48: a separator (one 7 px entry).</summary>
        public static void AddSeparator() {
            instance?.entries.Add((string.Empty, Separator, true));
        }

        /// <summary>The number of items added (separators not counted).</summary>
        public static int ItemCount {
            get {
                if (instance == null) return 0;
                int n = 0;
                foreach (var e in instance.entries) {
                    if (!e.Separator) n++;
                }

                return n / 2;
            }
        }

        /// <summary>
        /// msg 0x1e + Move: sized to the widest text + 6 by 7 px per entry, at the client pixel (x, y), flipped up / left when
        /// it would leave the screen (vf04 0x836a20). Nothing opens without an item.
        /// </summary>
        public static void Open(int clientX, int clientY, Action<int> pick) {
            if (instance == null || ItemCount == 0) return;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            RoClassicGdiFont font = instance.window.FontFor(0, 12, false);
            int w = 20;
            foreach (var e in instance.entries) {
                if (!e.Separator) w = Math.Max(w, font.Measure(e.Text));
            }

            w += 6;
            int h = 7 * instance.entries.Count;
            instance.window.Layout.Width = w;
            instance.window.Layout.Height = h;
            instance.window.SetHeight(h);
            Vector2Int client = system.ClientSize;
            int x = clientX, y = clientY;
            if (y > client.y - h) y -= h + 2;
            if (x > client.x - w) x -= w + 2;
            instance.window.MoveTo(Math.Max(0, x), Math.Max(0, y));
            instance.onPick = pick;
            instance.hover = -1;
            instance.openedFrame = Time.frameCount;
            instance.window.Visible = true;
            instance.window.Invalidate();
            system.Windows.BringToFront(instance.window);
        }

        public static void Close() {
            if (instance == null) return;
            instance.window.Visible = false;
            instance.onPick = null;
        }

        private int EntryAt(Vector2Int local) => entries.Count == 0 ? -1 : Mathf.Clamp(local.y / 7, 0, entries.Count - 1);

        /// <summary>LBtnUp 0x853fc0: the item under the release (separators skipped), then the menu goes.</summary>
        private void Release(Vector2Int local) {
            int e = EntryAt(local);
            if (e < 0 || entries[e].Separator) return;
            int separators = 0;
            for (int i = 0; i < e; i++) {
                if (entries[i].Separator) separators++;
            }

            Action<int> pick = onPick;
            Close();
            pick?.Invoke((e - separators) / 2);
        }

        private void Draw(RoClassicWindow w) {
            int width = w.Width, height = w.Height;
            w.FillRect(0, 0, width, height, White);
            w.DrawBitmapRaw(0, 0, Basic + "sysboxs_lu.bmp");
            w.DrawBitmapRaw(0, height - 7, Basic + "sysboxs_ld.bmp");
            w.DrawBitmapRaw(width - 7, 0, Basic + "sysboxs_ru.bmp");
            w.DrawBitmapRaw(width - 7, height - 7, Basic + "sysboxs_rd.bmp");
            for (int e = 0; e < entries.Count; e++) {
                var entry = entries[e];
                if (entry.Separator) {
                    w.FillRect(0, 7 * e + 2, width, 1, Separator);
                    continue;
                }

                bool second = e > 0 && !entries[e - 1].Separator && entries[e - 1].Text == entry.Text;
                if (second) continue; // the item's other half: the same row
                bool hovered = hover == e || hover == e + 1;
                if (hovered) w.FillRect(3, 7 * e + 1, width - 6, 12, Hover);
                w.TextOut(3, 7 * e + 1, entry.Text, 0, 12, entry.Color);
            }
        }

        /// <summary>Per frame: the hovered entry, and a press outside the menu closes it.</summary>
        public void ClassicUiTick(RoClassicUiSystem system) {
            if (!window.Visible) return;
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null && Time.frameCount != openedFrame &&
                (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) && system.Windows.HoverWindow != window) {
                Close();
                return;
            }
#endif
            if (system.Windows.HoverWindow != window) return;
            int e = EntryAt(system.Windows.Mouse - window.Position);
            if (e != hover) {
                hover = e;
                window.Invalidate();
            }
        }

        /// <summary>Diagnostics: the items, the hovered one, open or not.</summary>
        public static string Describe() {
            if (instance == null) return "no menu";
            var s = new System.Text.StringBuilder();
            s.Append(IsOpen ? "open" : "closed").Append(" at ").Append(instance.window.Position).Append(':');
            for (int e = 0; e < instance.entries.Count; e++) {
                var entry = instance.entries[e];
                if (entry.Separator) s.Append(" |");
                else if (e == 0 || instance.entries[e - 1].Separator || instance.entries[e - 1].Text != entry.Text) s.Append(" [").Append(entry.Text).Append(']');
            }

            return s.ToString();
        }

        /// <summary>Diagnostics: answers item <paramref name="item"/> as if released on it.</summary>
        public static void PickForTests(int item) {
            if (instance == null || !IsOpen) return;
            Action<int> pick = instance.onPick;
            Close();
            pick?.Invoke(item);
        }
    }
}
