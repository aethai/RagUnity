using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// The item hover tooltip of the original client: one UITransBalloonText owned by the game mode
    /// (<c>ShowTooltip</c> 0xa03bf0, reference ui-static ui/RUNTIME-RULES-ITEMS.md §3). '_' reads as a space, the text
    /// wraps at 35 bytes (FUN_00915a50); the balloon is the widest line + 12 by 4 + lines × 19, each line at
    /// (6, 4 + 19i) in its colour over a black shadow at (+1, +1), framed by the rounded outline #5c5c5c over a
    /// translucent black box (0x88000000); placed at the requested client pixel clamped to the screen with 3 px of
    /// slack, above every window, and gone 100 ms after the last <see cref="Show"/> (0xa03e80).
    /// </summary>
    public sealed class RoClassicTooltip : IRoClassicUiProducer {
        private const int WrapBytes = 35;
        private const int LineStep = 19;
        private const float HideAfterSeconds = 0.1f;
        private static readonly Color32 Outline = new Color32(0x5c, 0x5c, 0x5c, 255);
        private static readonly Color32 Box = new Color32(0, 0, 0, 0x88);
        private static readonly Color32 Shadow = new Color32(0, 0, 0, 255);

        private static readonly Dictionary<string, RoClassicTooltip> named = new Dictionary<string, RoClassicTooltip>();
        private readonly List<(int Start, int End)> wrap = new List<(int, int)>(4);
        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private string text = string.Empty;
        private Color32 color;
        private int requestX;
        private int requestY;
        private int wrapBytes = WrapBytes;
        private int centreWidth;
        private float lastShown = -10f;
        private bool shown;
        private bool dirty;

        /// <summary>The game's tooltip balloon (the mode's, 0xa03bf0).</summary>
        public static RoClassicTooltip Shared => Named("tooltip");

        /// <summary>
        /// Another UITransBalloonText with its own layer above every window (e.g. the Shortcut Settings notice next to the
        /// hover tooltip); one per name.
        /// </summary>
        public static RoClassicTooltip Named(string name) {
            if (!named.TryGetValue(name, out RoClassicTooltip balloon) || balloon.system == null || balloon.system != RoClassicUiSystem.Instance) {
                balloon = new RoClassicTooltip { system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main) };
                balloon.layer = balloon.system.Renderer.CreateLayer(name);
                balloon.layer.AboveOverlayUi = true;
                balloon.layer.Visible = false;
                balloon.system.Register(balloon);
                named[name] = balloon;
            }

            return balloon;
        }

        /// <summary>Hides it now (instead of 100 ms after the last <see cref="Show"/>).</summary>
        public void Hide() {
            lastShown = -10f;
        }

        /// <summary>
        /// Shows (or keeps showing) <paramref name="value"/> at a client pixel; call it every frame of the hover. Other
        /// balloons of the same class wrap elsewhere (<paramref name="wrapAt"/> bytes) or centre themselves in a width from
        /// <paramref name="clientX"/> (<paramref name="centreIn"/>, e.g. the Shortcut Settings notice).
        /// </summary>
        public void Show(string value, Color32 textColor, int clientX, int clientY, int wrapAt = WrapBytes, int centreIn = 0) {
            value = (value ?? string.Empty).Replace('_', ' ');
            if (value != text || !textColor.Equals(color) || clientX != requestX || clientY != requestY || wrapAt != wrapBytes ||
                centreIn != centreWidth) {
                dirty = true;
            }

            text = value;
            color = textColor;
            requestX = clientX;
            requestY = clientY;
            wrapBytes = wrapAt;
            centreWidth = centreIn;
            lastShown = Time.unscaledTime;
            if (!shown) {
                shown = true;
                dirty = true;
                system.Renderer.BringToFront(layer);
            }
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (!shown) return;
            if (Time.unscaledTime - lastShown > HideAfterSeconds || text.Length == 0) {
                shown = false;
                layer.Visible = false;
                return;
            }

            if (!dirty) return;
            dirty = false;
            RoClassicGdiFont font = ui.Font(12);
            RoClassicText.ByteWrap(text, wrapBytes, wrap);
            int w = 0;
            foreach ((int start, int end) in wrap) w = Mathf.Max(w, font.Measure(text.Substring(start, end - start)));
            w += 12;
            int h = 4 + wrap.Count * LineStep;
            Vector2Int client = ui.ClientSize;
            int left = centreWidth > 0 ? requestX + (centreWidth - w) / 2 : requestX;
            int x = Mathf.Clamp(left, -3, client.x - w + 3), y = Mathf.Clamp(requestY, -3, client.y - h + 3);

            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            q.Image(x, y, w, h, ui.White, Box);
            foreach (RectInt r in RoClassicWindow.RoundOutlineRects(x, y, w, h)) q.Image(r.x, r.y, r.width, r.height, ui.White, Outline);
            for (int i = 0; i < wrap.Count; i++) {
                string line = text.Substring(wrap[i].Start, wrap[i].End - wrap[i].Start);
                q.Text(font, x + 7, y + 4 + LineStep * i + 1, line, Shadow);
                q.Text(font, x + 6, y + 4 + LineStep * i, line, color);
            }

            layer.Visible = true;
            layer.MarkDirty();
        }
    }
}
