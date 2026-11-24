using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// A window drawn from its exported layout: the static ops once, dynamic slots from bound values, children in
    /// their current state. The whole window is one classic UI layer, re-emitted only when a value, the state of a
    /// child or the position changes (the original repaints its surface only when dirty too).
    /// </summary>
    public sealed class RoClassicWindow {
        private readonly RoClassicUiSystem system;
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> childStates = new Dictionary<int, string>(); // by child index
        private readonly Dictionary<string, int> slotX = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, Color32> slotColor = new Dictionary<string, Color32>(StringComparer.Ordinal);
        private readonly HashSet<string> hiddenSlots = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2Int> childPositions = new Dictionary<string, Vector2Int>(StringComparer.Ordinal);
        private readonly StringBuilder formatBuffer = new StringBuilder(64);
        private bool dirty = true;
        private byte alpha = 0xff;

        public RoClassicWindow(RoClassicUiSystem system, RoClassicWindowLayout layout) {
            this.system = system;
            Layout = layout;
            Layer = system.Renderer.CreateLayer(layout.Class + (layout.State != null ? "." + layout.State : string.Empty));
            Layer.AboveOverlayUi = true; // windows stay above the legacy UGUI while both coexist
            Position = layout.DefaultPosition;
            system.Windows.Add(this);
        }

        /// <summary>
        /// Draws what the oracle captured instead of bound data (conformance tests against the oracle's renders):
        /// text ops show their recorded string, and the fields are filled back from the samples (each format matched
        /// against the text it produced) so the native controls draw the seed's data.
        /// </summary>
        public bool ShowCapturedSamples {
            get => showCapturedSamples;
            set {
                if (showCapturedSamples == value) return;
                showCapturedSamples = value;
                if (value) BindSamples(Layout.Ops, Layout.Children);
                dirty = true;
            }
        }

        private bool showCapturedSamples;

        private void BindSamples(List<RoClassicOp> ops, List<RoClassicChildLayout> children) {
            foreach (RoClassicOp op in ops) {
                if (op.Kind == RoClassicOpKind.Text && op.Format != null) BindSample(op.Format, op.Text);
            }

            foreach (RoClassicChildLayout child in children) {
                if (child.Format != null && child.Props?["lines"] is Newtonsoft.Json.Linq.JArray lines && lines.Count > 0) {
                    // a text viewer: its stored lines give back the whole text (the samples only hold the visible ones)
                    var whole = new StringBuilder();
                    foreach (Newtonsoft.Json.Linq.JToken l in lines) whole.Append((string) l);
                    BindSample(child.Format, whole.ToString(), false);
                } else if (child.Format != null && child.SampleTexts != null && child.SampleTexts.Length > 0) {
                    BindSample(child.Format, child.SampleTexts[0]);
                }
                BindSamples(child.Ops, child.Children);
            }
        }

        /// <summary>Recovers the fields of a "{A}  /  {B:n0}"-style format from the text it produced.</summary>
        private void BindSample(string format, string text, bool trim = true) {
            var pattern = new StringBuilder("^");
            var fields = new List<string>();
            for (int i = 0; i < format.Length; i++) {
                int end = format[i] == '{' ? format.IndexOf('}', i + 1) : -1;
                if (end < 0) {
                    pattern.Append(System.Text.RegularExpressions.Regex.Escape(format[i].ToString()));
                    continue;
                }

                string token = format.Substring(i + 1, end - i - 1);
                int cut = token.IndexOfAny(new[] { ',', ':' });
                fields.Add(cut < 0 ? token : token.Substring(0, cut));
                pattern.Append("(.*?)");
                i = end;
            }

            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(text ?? string.Empty, pattern.Append('$').ToString(),
                System.Text.RegularExpressions.RegexOptions.Singleline);
            if (!m.Success) return;
            for (int f = 0; f < fields.Count; f++) {
                string v = trim ? m.Groups[f + 1].Value.Trim() : m.Groups[f + 1].Value;
                string digits = v.Replace(",", string.Empty);
                Set(fields[f], long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ? n.ToString(CultureInfo.InvariantCulture) : v);
            }
        }

        /// <summary>
        /// Skips the layout's data-driven ops (those with a slot): the owner draws that part itself in
        /// <see cref="DrawDynamic"/>, ported from the window's OnDraw when it has conditions a format cannot express.
        /// </summary>
        public bool SuppressBoundOps { get; set; }

        /// <summary>
        /// The data part of the window's OnDraw, ported from the client: called on every refresh after the layout's
        /// own ops and before the children; draw with <see cref="TextOut"/> / <see cref="TextOutRight"/> in window pixels.
        /// </summary>
        public event Action<RoClassicWindow> DrawDynamic;

        private RoClassicUiQuads drawQuads;
        private int drawX;
        private int drawY;

        /// <summary>
        /// Drawn under the window's surface (the renderer's DrawTransBox2 for windows whose GetTransBoxInfo2 is true, e.g.
        /// the chat's translucent boxes): holes the surface's raw copies punch never reach it.
        /// </summary>
        public event Action<RoClassicWindow> DrawUnderlay;

        private RectInt drawClip;
        private int surfaceFloor;

        /// <summary>Clips what is drawn next (window pixels) to a control's rectangle inside the window.</summary>
        public void ClipTo(int x, int y, int w, int h) {
            if (drawQuads == null) return;
            int x0 = Math.Max(drawClip.xMin, drawX + x), y0 = Math.Max(drawClip.yMin, drawY + y);
            int x1 = Math.Min(drawClip.xMax, drawX + x + w), y1 = Math.Min(drawClip.yMax, drawY + y + h);
            drawQuads.SetClip(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }

        /// <summary>Back to clipping at the window.</summary>
        public void ResetClip() {
            if (drawQuads != null) drawQuads.SetClip(drawClip.x, drawClip.y, drawClip.width, drawClip.height);
        }

        /// <summary>The client's font for (fontType, height), for measuring.</summary>
        public RoClassicGdiFont FontFor(int fontType, int height) => ClientFont(fontType, height);

        private readonly List<RoClassicEditField> editFields = new List<RoClassicEditField>();

        /// <summary>Text inputs of this window, drawn over its controls.</summary>
        public IReadOnlyList<RoClassicEditField> EditFields => editFields;

        public void AddEditField(RoClassicEditField field) {
            if (field == null || editFields.Contains(field)) return;
            field.Window = this;
            editFields.Add(field);
            dirty = true;
        }

        /// <summary>The visible text input under a client pixel, or null.</summary>
        public RoClassicEditField EditFieldAt(int x, int y) {
            int lx = x - Position.x, ly = y - Position.y;
            for (int i = editFields.Count - 1; i >= 0; i--) {
                if (!editFields[i].Visible || editFields[i].ReadOnly) continue;
                RectInt r = editFields[i].Rect;
                if (lx >= r.xMin && ly >= r.yMin && lx < r.xMax && ly < r.yMax) return editFields[i];
            }

            return null;
        }

        /// <summary>UIEditCtrl's font: fontType 0, height 12.</summary>
        internal RoClassicGdiFont EditFont => ClientFont(0, 12);

        /// <summary>Text with an explicit font at window pixels (edit fields).</summary>
        public void TextOutPlain(int x, int y, string text, RoClassicGdiFont font, Color32 color) {
            if (drawQuads == null || string.IsNullOrEmpty(text)) return;
            drawQuads.Text(font, drawX + x, drawY + y, text, Tint(color));
        }

        /// <summary>Re-emits the window on the next refresh (after the owner changed what DrawDynamic draws).</summary>
        public void Invalidate() => dirty = true;

        /// <summary>
        /// TextOut 0x9b47f0: the pen at (x, y). Under langtype 1 fontType 0 is Arial with lfHeight = height + 3 and
        /// fontType 1 Arial with lfHeight = height.
        /// </summary>
        public void TextOut(int x, int y, string text, int fontType, int height, Color32 color) {
            if (drawQuads == null || string.IsNullOrEmpty(text)) return;
            drawQuads.Text(ClientFont(fontType, height), drawX + x, drawY + y, text, Tint(color));
        }

        /// <summary>TextOut 0x9b47f0 with its bold flag (weight 700).</summary>
        public void TextOut(int x, int y, string text, int fontType, int height, Color32 color, bool bold) {
            if (drawQuads == null || string.IsNullOrEmpty(text)) return;
            drawQuads.Text(FontFor(fontType, height, bold), drawX + x, drawY + y, text, Tint(color));
        }

        /// <summary>TextOut 0x9b68d0: right-aligned, x is the right edge (measured with the same GDI font).</summary>
        public void TextOutRight(int x, int y, string text, int fontType, int height, Color32 color) {
            if (drawQuads == null || string.IsNullOrEmpty(text)) return;
            RoClassicGdiFont font = ClientFont(fontType, height);
            drawQuads.Text(font, drawX + x - font.Measure(text), drawY + y, text, Tint(color));
        }

        /// <summary>
        /// TextOut with a 1 px outline, right-aligned at x: the text four times at ±1 in the outline colour, then the
        /// fill (e.g. hotkey amounts: black on a white outline).
        /// </summary>
        public void TextOutRightOutlined(int x, int y, string text, int fontType, int height, Color32 fill, Color32 outline) {
            if (drawQuads == null || string.IsNullOrEmpty(text)) return;
            RoClassicGdiFont font = ClientFont(fontType, height);
            int left = drawX + x - font.Measure(text), top = drawY + y;
            drawQuads.Text(font, left - 1, top, text, Tint(outline));
            drawQuads.Text(font, left + 1, top, text, Tint(outline));
            drawQuads.Text(font, left, top - 1, text, Tint(outline));
            drawQuads.Text(font, left, top + 1, text, Tint(outline));
            drawQuads.Text(font, left, top, text, Tint(fill));
        }

        /// <summary>
        /// DrawBitmap (0x9ac110) keyed (noTrans 1), at window pixels; false when the bitmap does not exist. Its near-key
        /// pixels still turn into holes of the whole window, as the RGB555 upload makes them key.
        /// </summary>
        public bool DrawBitmap(int x, int y, string path) {
            if (drawQuads == null || !system.Images.TryGet(path, out RoClassicUiImage image)) return false;
            int opStart = drawQuads.VertexCount;
            drawQuads.Image(drawX + x, drawY + y, image, Tint(White));
            PunchBitmapHoles(drawQuads, path, drawX + x, drawY + y, false, surfaceFloor, opStart);
            return true;
        }

        /// <summary>DrawBitmap as a raw copy (noTrans 0): its exact 0xFF00FF pixels erase what the window drew there before.</summary>
        public bool DrawBitmapRaw(int x, int y, string path) {
            if (drawQuads == null || !system.Images.TryGet(path, out RoClassicUiImage image)) return false;
            int opStart = drawQuads.VertexCount;
            drawQuads.Image(drawX + x, drawY + y, image, Tint(White));
            PunchBitmapHoles(drawQuads, path, drawX + x, drawY + y, true, surfaceFloor, opStart);
            return true;
        }

        /// <summary>TextOut with "^RRGGBB" colour codes (0x9b4890), starting in black.</summary>
        public void TextOutColored(int x, int y, string text, int fontType, int height) {
            if (drawQuads == null || string.IsNullOrEmpty(text)) return;
            Color32 color = new Color32(0, 0, 0, 255);
            DrawColourCoded(drawQuads, ClientFont(fontType, height), drawX + x, drawY + y, text, 0, text.Length, ref color);
        }

        /// <summary>
        /// A text viewer line (0x9b52c0): <paramref name="text"/>[start, end) with "^RRGGBB" codes, in the colour the
        /// previous line ended with (<paramref name="color"/> carries over).
        /// </summary>
        public void TextOutColored(int x, int y, string text, int start, int end, int fontType, int height, ref Color32 color) {
            if (drawQuads == null || string.IsNullOrEmpty(text)) return;
            DrawColourCoded(drawQuads, ClientFont(fontType, height), drawX + x, drawY + y, text, start, end, ref color);
        }

        /// <summary>
        /// Frame 0x80a8b0: a 9-slice box of 14 px pieces <c>&lt;prefix&gt;lu/mu/ru/lm/bg/rm/ld/md/rd.bmp</c>, all raw
        /// copies; the centre tiles bg.bmp, or is white (colorchip(2,2)) when <paramref name="tiledCentre"/> is false.
        /// </summary>
        public void DrawSysBox(string prefix, int x, int y, int w, int h, bool tiledCentre) {
            if (drawQuads == null) return;
            int nx = (w - 28 + 13) / 14, ny = (h - 28 + 13) / 14;
            if (tiledCentre) {
                for (int j = 0; j < ny; j++) {
                    for (int i = 0; i < nx; i++) DrawBitmapRaw(x + 14 + 14 * i, y + 14 + 14 * j, prefix + "bg.bmp");
                }
            } else {
                FillRect(x + 14, y + 14, w - 28, h - 28, White);
            }

            for (int i = 0; i < nx; i++) DrawBitmapRaw(x + 14 + 14 * i, y, prefix + "mu.bmp");
            DrawBitmapRaw(x, y, prefix + "lu.bmp");
            DrawBitmapRaw(x + w - ImageWidth(prefix + "ru.bmp"), y, prefix + "ru.bmp");
            for (int j = 0; j < ny; j++) {
                DrawBitmapRaw(x, y + 14 + 14 * j, prefix + "lm.bmp");
                DrawBitmapRaw(x + w - ImageWidth(prefix + "rm.bmp"), y + 14 + 14 * j, prefix + "rm.bmp");
            }

            for (int i = 0; i < nx; i++) DrawBitmapRaw(x + 14 + 14 * i, y + h - ImageHeight(prefix + "md.bmp"), prefix + "md.bmp");
            DrawBitmapRaw(x, y + h - ImageHeight(prefix + "ld.bmp"), prefix + "ld.bmp");
            DrawBitmapRaw(x + w - ImageWidth(prefix + "rd.bmp"), y + h - ImageHeight(prefix + "rd.bmp"), prefix + "rd.bmp");
        }

        private int ImageWidth(string path) => system.Images.TryGet(path, out RoClassicUiImage image) ? image.Width : 14;
        private int ImageHeight(string path) => system.Images.TryGet(path, out RoClassicUiImage image) ? image.Height : 14;

        /// <summary>TextOutOutline 0x9b5bb0: the outline colour at (x ± 1, y) and (x, y ± 1), then the fill; optionally bold.</summary>
        public void TextOutOutline(int x, int y, string text, int fontType, int height, Color32 fill, Color32 outline, bool bold = false) {
            if (drawQuads == null || string.IsNullOrEmpty(text)) return;
            RoClassicGdiFont font = FontFor(fontType, height, bold);
            int left = drawX + x, top = drawY + y;
            drawQuads.Text(font, left - 1, top, text, Tint(outline));
            drawQuads.Text(font, left + 1, top, text, Tint(outline));
            drawQuads.Text(font, left, top - 1, text, Tint(outline));
            drawQuads.Text(font, left, top + 1, text, Tint(outline));
            drawQuads.Text(font, left, top, text, Tint(fill));
        }

        /// <summary>The client's font for (fontType, height), bold (weight 700) or regular.</summary>
        public RoClassicGdiFont FontFor(int fontType, int height, bool bold) =>
            system.Font(new RoClassicFontSpec("Arial", fontType == 0 ? height + 3 : height, bold ? 700 : 400, 0, (int) RoClassicGdiFont.NonAntialiasedQuality));

        /// <summary>The rectangles the client's rounded outline (0x9ac670) fills for a box (as the oracle records them).</summary>
        public static RoundOutlineRectList RoundOutlineRects(int x, int y, int w, int h) => new RoundOutlineRectList(x, y, w, h);

        /// <summary>
        /// The 12 rectangles of the rounded outline, enumerated without an array (the chat balloons rebuild every
        /// balloon's outline whenever a message comes, many times a second in a busy town).
        /// </summary>
        public readonly struct RoundOutlineRectList {
            private readonly int x, y, w, h;

            public RoundOutlineRectList(int x, int y, int w, int h) {
                this.x = x;
                this.y = y;
                this.w = w;
                this.h = h;
            }

            public int Count => 12;

            public RectInt this[int i] => i switch {
                0 => new RectInt(x + 3, y, w - 6, 1),
                1 => new RectInt(x + 3, y + h - 1, w - 6, 1),
                2 => new RectInt(x, y + 3, 1, h - 6),
                3 => new RectInt(x + w - 1, y + 3, 1, h - 6),
                4 => new RectInt(x + 1, y + 1, 2, 1),
                5 => new RectInt(x + 1, y + h - 2, 2, 1),
                6 => new RectInt(x + w - 3, y + 1, 2, 1),
                7 => new RectInt(x + w - 3, y + h - 2, 2, 1),
                8 => new RectInt(x + 1, y + 2, 1, 1),
                9 => new RectInt(x + 1, y + h - 3, 1, 1),
                10 => new RectInt(x + w - 2, y + 2, 1, 1),
                _ => new RectInt(x + w - 2, y + h - 3, 1, 1),
            };

            public Enumerator GetEnumerator() => new Enumerator(this);

            public struct Enumerator {
                private readonly RoundOutlineRectList list;
                private int index;

                public Enumerator(RoundOutlineRectList list) {
                    this.list = list;
                    index = -1;
                }

                public bool MoveNext() => ++index < 12;

                public RectInt Current => list[index];
            }
        }

        /// <summary>The rounded outline 0x9ac670 at window pixels.</summary>
        public void RoundOutline(int x, int y, int w, int h, Color32 color) {
            foreach (RectInt r in RoundOutlineRects(x, y, w, h)) FillRect(r.x, r.y, r.width, r.height, color);
        }

        private RoClassicChildLayout defaultScrollBar;

        /// <summary>
        /// A vertical UIScrollBar (0x7ef530) with the client's default bitmaps (scroll0*.bmp, arrow 13, thumb at least
        /// 8 px) at window pixels: nothing while <paramref name="range"/> is 0.
        /// </summary>
        public void DrawScrollBar(int x, int y, int h, int range, int total, int visible, int pos) {
            if (drawQuads == null || range <= 0) return;
            if (defaultScrollBar == null) {
                var bitmaps = new Newtonsoft.Json.Linq.JObject();
                foreach ((string role, string file) in new[] {
                    ("scroll0up", "scroll0up"), ("scroll0mid", "scroll0mid"), ("scroll0down", "scroll0down"),
                    ("up", "scroll0bar_up"), ("mid", "scroll0bar_mid"), ("down", "scroll0bar_down"),
                }) {
                    bitmaps[role] = new Newtonsoft.Json.Linq.JArray("유저인터페이스\\" + file + ".bmp");
                }

                defaultScrollBar = new RoClassicChildLayout {
                    Class = "UIScrollBar", W = 13,
                    Props = new Newtonsoft.Json.Linq.JObject { ["bitmaps"] = bitmaps, ["arrow"] = 13, ["minThumb"] = 1 },
                };
            }

            defaultScrollBar.H = h;
            EmitScrollBar(drawQuads, defaultScrollBar, drawX + x, drawY + y, range, total, visible, pos);
        }

        /// <summary>FillRect (0x9ac310) at window pixels.</summary>
        public void FillRect(int x, int y, int w, int h, Color32 color) {
            if (drawQuads == null || w <= 0 || h <= 0) return;
            drawQuads.Image(drawX + x, drawY + y, w, h, system.White, Tint(color));
        }

        /// <summary>
        /// A texture stretched over (x, y, w, h) at window pixels and modulated by <paramref name="color"/> (the direct
        /// pass's CTexture quads 0x52cdf0, drawn under the window's DIB); false when the image does not exist.
        /// </summary>
        public bool DrawStretched(int x, int y, int w, int h, string path, Color32 color) {
            if (drawQuads == null || w <= 0 || h <= 0 || !system.Images.TryGet(path, out RoClassicUiImage image)) return false;
            drawQuads.Image(drawX + x, drawY + y, w, h, image, Tint(color));
            return true;
        }

        /// <summary>
        /// FillRect with the key colour 0xFF00FF at window pixels: erases what the window's surface drew there so far (the
        /// underlay and what is drawn afterwards stay), e.g. the world map's preview punched into its DIB.
        /// </summary>
        public void PunchRect(int x, int y, int w, int h) {
            if (drawQuads == null || w <= 0 || h <= 0) return;
            drawQuads.Punch(surfaceFloor, drawQuads.VertexCount, drawX + x, drawY + y, drawX + x + w, drawY + y + h);
        }

        /// <summary>A 1 px line (0x52c360) between two window points.</summary>
        public void DrawLine(float x0, float y0, float x1, float y1, Color32 color) {
            if (drawQuads == null) return;
            float dx = x1 - x0, dy = y1 - y0;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f) return;
            // half a pixel either side of the segment
            float nx = -dy / length * 0.5f, ny = dx / length * 0.5f;
            var a = new Vector2(drawX + x0 + nx, drawY + y0 + ny);
            var b = new Vector2(drawX + x1 + nx, drawY + y1 + ny);
            var c = new Vector2(drawX + x1 - nx, drawY + y1 - ny);
            var d = new Vector2(drawX + x0 - nx, drawY + y0 - ny);
            drawQuads.QuadCorners(a, b, c, d, system.White.Uv, system.White.Page, Tint(color));
        }

        private RoClassicGdiFont ClientFont(int fontType, int height) =>
            system.Font(new RoClassicFontSpec("Arial", fontType == 0 ? height + 3 : height, 400, 0, (int) RoClassicGdiFont.NonAntialiasedQuality));

        /// <summary>Frame windows drag from their title bar (UIFrameWnd).</summary>
        public bool Draggable { get; set; } = true;

        /// <summary>
        /// What Esc does to this window while it heads the Esc list (mgr+0x18; see <see cref="RoClassicWindowManager.PushEsc"/>):
        /// true = Esc is used up. Null keeps the window out of the list (IsEscExcluded 0x9c71c0).
        /// </summary>
        public Func<bool> EscClose { get; set; }

        /// <summary>
        /// A hidden window that stays at the head of the Esc list and keeps taking Esc (the client's hidden-but-not-deleted
        /// windows, e.g. the deal choice 0x19 behind the buy/sell lists); others leave the list when hidden.
        /// </summary>
        public bool EscHoldsWhileHidden { get; set; }

        /// <summary>
        /// Layers drawn right under the window's own, in this order (a direct pass split from the window's surface, e.g. the
        /// world map's picture and its animated routes): they come to the front with the window. Their owner shows and hides them.
        /// </summary>
        public List<RoClassicUiLayer> UnderLayers { get; } = new List<RoClassicUiLayer>();

        /// <summary>Raised when a child (bitmap button...) is clicked: pressed and released over it.</summary>
        public event Action<RoClassicChildLayout> Clicked;

        public int Width => Layout.Width;
        public int Height => heightOverride ?? Layout.Height;

        private int? heightOverride;

        /// <summary>
        /// Shrinks the window to <paramref name="height"/> (null = the layout's): a minimized frame window keeps only its
        /// title bar (17 px) and draws and answers only there.
        /// </summary>
        public void SetHeight(int? height) {
            if (heightOverride == height) return;
            int oldHeight = Height;
            heightOverride = height;
            dirty = true;
            // 0x73ff60 after the item / equipment / status window's height changes: the windows docked to it follow
            if (Visible && Height != oldHeight) system.Windows.PushDockedOnHeightChange(this, oldHeight);
        }

        /// <summary>
        /// The window's alpha (the client drags frame windows at 0x80): the whole surface at once, as the client blits a
        /// window's surface (RoClassicUiLayer.GroupAlpha), not every bitmap and text on its own.
        /// </summary>
        public byte Alpha {
            get => alpha;
            set {
                if (alpha == value) return;
                alpha = value;
                Layer.GroupAlpha = value;
                foreach (RoClassicUiLayer under in UnderLayers) under.GroupAlpha = value;
            }
        }

        /// <summary>
        /// The part of the window that takes the mouse (window-local), when not the whole rectangle — e.g. a collapsed
        /// UIMenuIconWnd answers only on its strip (vf50 HitTest 0x7acf50).
        /// </summary>
        public RectInt? HitArea { get; set; }

        public bool Contains(int x, int y) {
            RectInt area = HitArea ?? new RectInt(0, 0, Layout.Width, Height);
            int lx = x - Position.x, ly = y - Position.y;
            return lx >= area.xMin && ly >= area.yMin && lx < area.xMax && ly < area.yMax;
        }

        /// <summary>Raised when the window changed position (dragged, docked, clamped).</summary>
        public event Action<RoClassicWindow> Moved;

        /// <summary>Shows or hides a control at runtime, like the client's SetVisible / parking.</summary>
        public void SetChildVisible(RoClassicChildLayout child, bool visible) {
            if (child == null || child.Visible == visible) return;
            child.Visible = visible;
            dirty = true;
        }

        /// <summary>Index of the interactive child (a control with state bitmaps) under a screen pixel, or -1.</summary>
        public int ChildAt(int x, int y) {
            int lx = x - Position.x, ly = y - Position.y;
            List<RoClassicChildLayout> children = Layout.Children;
            for (int i = children.Count - 1; i >= 0; i--) {
                RoClassicChildLayout c = children[i];
                Vector2Int p = PositionOf(c);
                if (c.Visible && c.States.Count > 0 && lx >= p.x && ly >= p.y && lx < p.x + c.W && ly < p.y + c.H) return i;
            }

            return -1;
        }

        /// <summary>A click on the child at this index (the manager's button release; F11 closing a window by its close button).</summary>
        public void RaiseClicked(int childIndex) {
            if (childIndex >= 0 && childIndex < Layout.Children.Count) Clicked?.Invoke(Layout.Children[childIndex]);
        }

        /// <summary>Where (window-local) a press starts dragging the window; null = the title bar of a draggable window.</summary>
        public Func<Vector2Int, bool> FrameDragArea { get; set; }

        /// <summary>What a press at a window-local point starts dragging (null = nothing; the frame drag rules apply).</summary>
        public Func<Vector2Int, IRoClassicDragPayload> DragSourceAt { get; set; }

        /// <summary>
        /// A child button's balloon text made when it is hovered (e.g. UIMenuIcon::vf27 0x7ae600: the name and the window's
        /// hotkey); null falls back to the child's <see cref="RoClassicChildLayout.Tooltip"/>.
        /// </summary>
        public Func<RoClassicChildLayout, string> ChildTooltip { get; set; }

        /// <summary>Controls the window draws itself that take a click (links, combo arrows...): the "click" cursor over them.</summary>
        public Func<Vector2Int, bool> ClickableAt { get; set; }

        /// <summary>
        /// The window's UIResizer under a window-local point: its mode (0..7, which edges follow the mouse), −1 elsewhere.
        /// A press there captures the mouse for <see cref="ResizeRequested"/>.
        /// </summary>
        public Func<Vector2Int, int> ResizerModeAt { get; set; }

        /// <summary>
        /// UIResizer OnMouseMove 0x7c0c80 → parent msg 0xe (mode, w, h): the size at the press plus the mouse delta, unclamped
        /// (the window clamps and snaps it), on every move while the resizer holds the mouse.
        /// </summary>
        public event Action<RoClassicWindow, int, int, int> ResizeRequested;

        internal void RaiseResizeRequested(int mode, int w, int h) => ResizeRequested?.Invoke(this, mode, w, h);

        /// <summary>The window's current width (layout width), what a resizer starts from.</summary>
        public void SetWidth(int width) {
            if (Layout.Width == width) return;
            Layout.Width = width;
            dirty = true;
        }

        /// <summary>Left button pressed at a window-local point outside the buttons and text inputs (the window's LBtnDown).</summary>
        public event Action<RoClassicWindow, Vector2Int> Pressed;

        /// <summary>Mouse wheel over the window at a window-local point: +1 per notch away from the user, -1 towards.</summary>
        public event Action<RoClassicWindow, Vector2Int, int> Wheeled;

        /// <summary>Left button released at a window-local point after a press on this window that started no drag (LBtnUp).</summary>
        public event Action<RoClassicWindow, Vector2Int> Released;

        internal void RaisePressed(Vector2Int local) => Pressed?.Invoke(this, local);
        internal void RaiseReleased(Vector2Int local) => Released?.Invoke(this, local);
        internal void RaiseWheeled(Vector2Int local, int delta) => Wheeled?.Invoke(this, local, delta);

        /// <summary>Double click at a window-local point outside the buttons.</summary>
        public event Action<RoClassicWindow, Vector2Int> DoubleClicked;

        /// <summary>Right button pressed at a window-local point.</summary>
        public event Action<RoClassicWindow, Vector2Int> RightClicked;

        /// <summary>A classic drag released over this window at a window-local point.</summary>
        public event Action<RoClassicWindow, Vector2Int, IRoClassicDragPayload> Dropped;

        /// <summary>A drag that started here was released outside every window.</summary>
        public event Action<RoClassicWindow, IRoClassicDragPayload> DraggedOutside;

        internal void RaiseDoubleClicked(Vector2Int local) => DoubleClicked?.Invoke(this, local);
        internal void RaiseRightClicked(Vector2Int local) => RightClicked?.Invoke(this, local);
        internal void RaiseDropped(Vector2Int local, IRoClassicDragPayload payload) => Dropped?.Invoke(this, local, payload);
        internal void RaiseDraggedOutside(IRoClassicDragPayload payload) => DraggedOutside?.Invoke(this, payload);

        public void Destroy() {
            system.Windows.Remove(this);
            system.Renderer.RemoveLayer(Layer);
        }

        public RoClassicWindowLayout Layout { get; private set; }
        public RoClassicUiLayer Layer { get; }
        public Vector2Int Position { get; private set; }

        public bool Visible {
            get => Layer.Visible;
            set {
                if (HiddenByCloseTabs) {
                    // a refresh does not undo F11's SetShow(0); an open does (BringToFront → CloseTabsSetShow)
                    if (value) return;
                    HiddenByCloseTabs = false;   // closed while hidden: gone from the window list (DeleteWindow)
                }

                Layer.Visible = value;
            }
        }

        /// <summary>
        /// F11 "Close Tabs" (0x9d6530) hid the window with SetShow(0): it is still in the window list (remembered, restored
        /// by the next F11), so the owner's own refreshes do not show it; closing it, or opening it again (MakeWindow, i.e.
        /// <see cref="RoClassicWindowManager.BringToFront"/>), ends that.
        /// </summary>
        public bool HiddenByCloseTabs { get; private set; }

        /// <summary>F11's SetShow (vf14 0x5920e0) on a window of the list: hide one shown, show one it hid.</summary>
        public void CloseTabsSetShow(bool show) {
            if (show) {
                if (!HiddenByCloseTabs) return;
                HiddenByCloseTabs = false;
                Layer.Visible = true;
                foreach (RoClassicUiLayer under in UnderLayers) under.Visible = true;
                return;
            }

            if (!Layer.Visible) return;
            HiddenByCloseTabs = true;
            Layer.Visible = false;
            foreach (RoClassicUiLayer under in UnderLayers) under.Visible = false;
        }

        public void SetLayout(RoClassicWindowLayout layout) {
            if (ReferenceEquals(layout, Layout)) return;
            Layout = layout;
            dirty = true;
        }

        public void MoveTo(int x, int y) {
            if (Position.x == x && Position.y == y) return;
            Position = new Vector2Int(x, y);
            dirty = true;
            Moved?.Invoke(this);
        }

        /// <summary>Sets a data field used by the dynamic ops' format templates (e.g. "Hp", "Zeny").</summary>
        public void Set(string field, string value) {
            if (values.TryGetValue(field, out string current) && current == value) return;
            values[field] = value;
            dirty = true;
        }

        public void Set(string field, long value) => Set(field, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// Anchor x of a dynamic text op whose position the client computes at draw time ("dyn" contains "x"),
        /// e.g. Basic Info's weight, placed left of the zeny text.
        /// </summary>
        public void SetSlotX(string slot, int x) {
            if (slotX.TryGetValue(slot, out int current) && current == x) return;
            slotX[slot] = x;
            dirty = true;
        }

        /// <summary>Text colour of a dynamic op chosen by the client at draw time (e.g. red when overweight).</summary>
        public void SetSlotColor(string slot, Color32 color) {
            if (slotColor.TryGetValue(slot, out Color32 current) && current.Equals(color)) return;
            slotColor[slot] = color;
            dirty = true;
        }

        /// <summary>Hides the dynamic ops of a slot the client skips under a condition (e.g. zero exp).</summary>
        public void SetSlotVisible(string slot, bool visible) {
            if (visible ? hiddenSlots.Remove(slot) : hiddenSlots.Add(slot)) dirty = true;
        }

        /// <summary>
        /// Moves a control the way the client does at runtime (it parks hidden ones off-window, e.g. at x = −200).
        /// Keyed by the owner member (<see cref="RoClassicChildLayout.Owner"/>) or else the export path.
        /// </summary>
        public void SetChildPosition(string key, int x, int y) {
            var p = new Vector2Int(x, y);
            if (childPositions.TryGetValue(key, out Vector2Int current) && current == p) return;
            childPositions[key] = p;
            dirty = true;
        }

        private Vector2Int PositionOf(RoClassicChildLayout child) =>
            (child.Owner != null && childPositions.TryGetValue(child.Owner, out Vector2Int p)) ||
            (child.Path != null && childPositions.TryGetValue(child.Path, out p))
                ? p
                : new Vector2Int(child.X, child.Y);

        /// <summary>Pixel width of a text slot as currently bound (the client's GetTextExtent on the same string).</summary>
        public int MeasureSlot(string slot) {
            foreach (RoClassicOp op in Layout.Ops) {
                if (op.Kind != RoClassicOpKind.Text || op.Slot != slot) continue;
                string text = op.Format != null ? Format(op.Format) : op.Text;
                return string.IsNullOrEmpty(text) ? 0 : FontOf(op).Measure(text);
            }

            return 0;
        }

        public void SetChildState(int id, string state) {
            if (childStates.TryGetValue(id, out string current) && current == state) return;
            childStates[id] = state;
            dirty = true;
        }

        /// <summary>Rewrites the layer if anything changed; called from the owner's classic UI tick.</summary>
        public void Refresh() {
            if (!dirty) return;
            dirty = false;
            RoClassicUiQuads q = Layer.Quads;
            q.Clear();
            int ox = Position.x, oy = Position.y;
            // everything is clipped to the window like the client's per-window surface (children parked at
            // negative positions stay invisible)
            var clip = new RectInt(ox, oy, Layout.Width, Height);
            q.SetClip(clip.x, clip.y, clip.width, clip.height);
            drawClip = clip;
            drawX = ox;
            drawY = oy;
            surfaceFloor = 0;
            if (DrawUnderlay != null) {
                drawQuads = q;
                DrawUnderlay(this);
                drawQuads = null;
                q.SetClip(clip.x, clip.y, clip.width, clip.height);
                surfaceFloor = q.VertexCount;
            }

            Emit(q, Layout.Ops, ox, oy, Layout.Width, Layout.Height, surfaceFloor);
            if (DrawDynamic != null) {
                drawQuads = q;
                DrawDynamic(this);
                drawQuads = null;
                q.SetClip(clip.x, clip.y, clip.width, clip.height);
            }

            barIndex = 0;
            EmitChildren(q, Layout.Children, ox, oy, clip, true);
            if (editFields.Count > 0) {
                drawQuads = q;
                foreach (RoClassicEditField field in editFields) {
                    if (!field.Visible) continue;
                    ClipTo(field.Rect.x, field.Rect.y, field.Rect.width, field.Rect.height); // the control's own surface
                    field.Draw(this, EditFont);
                }

                drawQuads = null;
            }

            q.ClearClip();
            Layer.MarkDirty();
        }

        private int barIndex;

        /// <summary>
        /// Children in composition order, each clipped to its own rectangle inside its parent's (the client draws a
        /// child into its own surface and blits that, keyed, into the parent's).
        /// </summary>
        private void EmitChildren(RoClassicUiQuads q, List<RoClassicChildLayout> children, int ox, int oy, RectInt parentClip, bool topLevel) {
            for (int index = 0; index < children.Count; index++) {
                RoClassicChildLayout child = children[index];
                if (!child.Visible) continue;
                Vector2Int at = topLevel ? PositionOf(child) : new Vector2Int(child.X, child.Y);
                int cx = ox + at.x, cy = oy + at.y;
                int x0 = Math.Max(parentClip.xMin, cx), y0 = Math.Max(parentClip.yMin, cy);
                int x1 = Math.Min(parentClip.xMax, cx + child.W), y1 = Math.Min(parentClip.yMax, cy + child.H);
                if (x1 <= x0 || y1 <= y0) continue; // parked off-window or empty
                var clip = new RectInt(x0, y0, x1 - x0, y1 - y0);
                q.SetClip(clip.x, clip.y, clip.width, clip.height);
                EmitChild(q, child, topLevel ? index : -1, cx, cy, clip);
                q.SetClip(parentClip.x, parentClip.y, parentClip.width, parentClip.height);
            }
        }

        /// <summary>A vertical scroll bar whose state the layout carries.</summary>
        private static bool HasScrollState(RoClassicChildLayout child) => child.H > child.W && child.Props?["range"] != null;

        private void EmitChild(RoClassicUiQuads q, RoClassicChildLayout child, int index, int cx, int cy, RectInt clip) {
            int surface = q.VertexCount; // the child draws into its own surface
            // controls implemented natively draw from bound data (in sample mode: the fields recovered from the seed)
            switch (child.Class) {
                case "UIBarGraphPlayer":
                    EmitBarGraphPlayer(q, child, cx, cy, barIndex++);
                    return;
                case "UIINT64BarGraph":
                    EmitInt64BarGraph(q, child, cx, cy);
                    return;
                case "UIStaticText" when child.Fields.Count > 0:
                    // a data-bound label: its ops hold only the static part (key clear), the text comes from the data
                    Emit(q, child.Ops, cx, cy, child.W, child.H, surface);
                    EmitStaticText(q, child, cx, cy);
                    return;
                case "UIScrollBar" when HasScrollState(child):
                    // the scroll state the oracle captured (+0x80 range, +0x84 pos, +0x98 total, +0x9c visible)
                    Emit(q, child.Ops, cx, cy, child.W, child.H, surface);
                    EmitScrollBar(q, child, cx, cy, (int) child.Props["range"], (int?) child.Props["total"] ?? 0,
                        (int?) child.Props["visible"] ?? 0, (int?) child.Props["pos"] ?? 0);
                    return;
                case "UITextViewer":
                    Emit(q, child.Ops, cx, cy, child.W, child.H, surface);
                    EmitTextViewer(q, child, cx, cy, clip);
                    return;
            }

            string statePath = null;
            if (child.States.Count > 0) {
                string state = index >= 0 && childStates.TryGetValue(index, out string s) ? s : "normal";
                if (child.States.TryGetValue(state, out statePath) || child.States.TryGetValue("normal", out statePath)) {
                    if (system.Images.TryGet(statePath, out RoClassicUiImage image)) {
                        int opStart = q.VertexCount;
                        q.Image(cx, cy, image, Tint(White));
                        PunchBitmapHoles(q, statePath, cx, cy, true, surface, opStart); // buttons blit with noTrans 0
                    }
                }
            }

            // a button's recorded ops include its normal bitmap: the current state's bitmap replaces it
            Emit(q, child.Ops, cx, cy, child.W, child.H, surface, statePath != null ? child.States : null);
            EmitChildren(q, child.Children, cx, cy, clip, false);
        }

        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private const string GaugeRoot = "유저인터페이스\\basic_interface\\gze";

        /// <summary>
        /// UIBarGraphPlayer::OnDraw (0x7e94f0): gze_bg1 background; when value &lt;= max and max != 0 the fill is
        /// gze{blue|red}_left + (int)((w − 8) × value / max) × 1 px _mid + _right (red below 25%), then
        /// "%3I64d  /  %3I64d" centred at y −2 in Arial 13 (TextOut_9b4c50).
        /// </summary>
        private void EmitBarGraphPlayer(RoClassicUiQuads q, RoClassicChildLayout child, int x, int y, int index) {
            string valueField = Field(child, "value", 0) ?? (index == 0 ? "Hp" : "Sp");
            string maxField = Field(child, "max", 1) ?? (index == 0 ? "MaxHp" : "MaxSp");
            if (valueField.StartsWith("Max", StringComparison.Ordinal)) (valueField, maxField) = (maxField, valueField);
            long value = GetLong(valueField), max = GetLong(maxField);
            if (value > max) return;
            if (system.Images.TryGet(GaugeRoot + "_bg1.bmp", out RoClassicUiImage bg)) q.Image(x, y, bg, Tint(White));
            if (max == 0) return;
            double ratio = (double) value / max;
            string prefix = GaugeRoot + (ratio < 0.25 ? "red" : "blue");
            int fill = (int) ((child.W - 8) * ratio);
            if (system.Images.TryGet(prefix + "_left.bmp", out RoClassicUiImage left)) q.Image(x, y, left, Tint(White));
            if (fill > 0 && system.Images.TryGet(prefix + "_mid.bmp", out RoClassicUiImage mid)) {
                q.Image(x + 4, y, fill, mid.Height, mid, Tint(White)); // fill × the 1 px mid bitmap, side by side
            }

            if (system.Images.TryGet(prefix + "_right.bmp", out RoClassicUiImage right)) q.Image(x + fill + 4, y, right, Tint(White));
            string text = Pad3(value) + "  /  " + Pad3(max);
            RoClassicGdiFont font = system.Font(new RoClassicFontSpec("Arial", 13, 400, 0, 3));
            q.Text(font, x + (child.W - font.Measure(text)) / 2, y - 2, text, Tint(new Color32(0, 0, 0, 255)));
        }

        /// <summary>
        /// UIINT64BarGraph::OnDraw (0x7ebf20): when value &lt;= max, ClearDC(border) + inner fill, then
        /// (int)((w − 2) × value / max) of the fill colour (red below 25%) from (1, 1). Colours: props.colors
        /// = [border, inner, fill] as captured from the client's skin colour table.
        /// </summary>
        private void EmitInt64BarGraph(RoClassicUiQuads q, RoClassicChildLayout child, int x, int y) {
            string valueField = Field(child, "value", 0), maxField = Field(child, "max", 1);
            if (valueField != null && valueField.EndsWith("Next", StringComparison.Ordinal)) (valueField, maxField) = (maxField, valueField);
            long value = GetLong(valueField), max = GetLong(maxField);
            if (value > max) return;
            Color32 border = PropColor(child, 0, new Color32(0xaf, 0xaf, 0xaf, 255));
            Color32 inner = PropColor(child, 1, new Color32(0xff, 0xff, 0xff, 255));
            Color32 fill = PropColor(child, 2, new Color32(0x56, 0x7f, 0xcf, 255));
            q.Image(x, y, child.W, child.H, system.White, Tint(border));
            q.Image(x + 1, y + 1, child.W - 2, child.H - 2, system.White, Tint(inner));
            if (max == 0) return;
            double ratio = (double) value / max;
            if (ratio == 0) return;
            if (ratio * 100.0 < 25.0) fill = new Color32(255, 0, 0, 255);
            int w = (int) ((child.W - 2) * ratio);
            if (w > 0) q.Image(x + 1, y + 1, w, child.H - 2, system.White, Tint(fill));
        }

        /// <summary>UIStaticText: its bound text (or the captured string) at its origin in its font and colour.</summary>
        private void EmitStaticText(RoClassicUiQuads q, RoClassicChildLayout child, int x, int y) {
            string field = Field(child, "text", 0);
            string text = field != null && values.TryGetValue(field, out string v) ? v : PropString(child, "strings");
            if (string.IsNullOrEmpty(text)) return;
            string fontKey = child.Props != null ? (string) child.Props["font"] : null;
            RoClassicFontSpec spec = fontKey != null && Layout.Fonts.TryGetValue(fontKey, out RoClassicFontSpec f) ? f : new RoClassicFontSpec("Arial", 15, 400, 0, 3);
            q.Text(system.Font(spec), x, y, text, Tint(PropColor(child, 1, new Color32(0, 0, 0, 255))));
        }

        /// <summary>Fallback when a layout lacks UITextViewer +0xb8 (18 as captured for message boxes).</summary>
        private const int ProvisionalViewerLineHeight = 18;

        /// <summary>SendMsg(0x1d) without a length wraps at 35 bytes (cmove at 0x7faf2d).</summary>
        private const int DefaultViewerWrapBytes = 35;


        /// <summary>
        /// UITextViewer::OnDraw (0x7f0d30): after its background, every visible line at (3, lineHeight·i + 4) through
        /// FUN_009b52c0, which honours ^RRGGBB colour codes (a colour carries over to the following lines). Lines are
        /// made when the text is set, by FUN_00915f60 (<see cref="WrapBytes"/>).
        /// </summary>
        private readonly List<(string Text, int Start, int End)> viewerLines = new List<(string, int, int)>();
        private readonly List<(int Start, int End)> wrapRanges = new List<(int, int)>();

        private void EmitTextViewer(RoClassicUiQuads q, RoClassicChildLayout child, int x, int y, RectInt clip) {
            string field = Field(child, "text", 0);
            string text = field != null && values.TryGetValue(field, out string bound) ? bound : null;
            if (text == null) {
                // no data behind it (text set once at creation): its recorded ops already hold the lines and the
                // scroll bars theirs
                EmitChildren(q, child.Children, x, y, clip, false);
                return;
            }

            RoClassicGdiFont font = FontOf(child.Props != null ? (string) child.Props["font"] : null);
            int lineHeight = ((int?) child.Props?["lineHeight"] ?? ProvisionalViewerLineHeight) - ((int?) child.Props?["fieldA8"] ?? 0);
            int scroll = (int?) child.Props?["scroll"] ?? 0;
            viewerLines.Clear();
            if (!string.IsNullOrEmpty(text)) {
                // the length the window passed with SendMsg(0x1d); message boxes are built with (int)(width / 7.5)
                // (0x807f00 → 0x808263) and replace their text later with 0x26
                int wrapBytes = (int?) child.Props?["wrapBytes"] ?? (Layout.Class == "UIMessageBox" ? (int) (Layout.Width / 7.5) : 0);
                if (wrapBytes == 0) wrapBytes = DefaultViewerWrapBytes;
                WrapBytes(text, wrapBytes, wrapRanges);
                foreach ((int start, int end) in wrapRanges) viewerLines.Add((text, start, end));
            }

            // colour codes of the lines scrolled past still apply (0x7f0d30 walks them first)
            Color32 color = PropColor(child, 1, new Color32(0, 0, 0, 255));
            int visible = lineHeight > 0 ? child.H / Math.Max(1, lineHeight) : 0;
            int last = Math.Min(viewerLines.Count, scroll + visible);
            for (int i = 0; i < last; i++) {
                (string s, int start, int end) = viewerLines[i];
                while (end > start && (s[end - 1] == '\n' || s[end - 1] == '\r')) end--;
                if (i < scroll) {
                    ScanColourCodes(s, start, end, ref color);
                    continue;
                }

                DrawColourCoded(q, font, x + 3, y + lineHeight * (i - scroll) + 4, s, start, end, ref color);
            }

            // the vertical scroll bar: range = lines beyond the view
            foreach (RoClassicChildLayout bar in child.Children) {
                if (!bar.Visible || bar.Class != "UIScrollBar") continue;
                int bx = x + bar.X, by = y + bar.Y;
                var barClip = new RectInt(Math.Max(clip.xMin, bx), Math.Max(clip.yMin, by), 0, 0);
                barClip.xMax = Math.Min(clip.xMax, bx + bar.W);
                barClip.yMax = Math.Min(clip.yMax, by + bar.H);
                if (barClip.width <= 0 || barClip.height <= 0) continue;
                q.SetClip(barClip.x, barClip.y, barClip.width, barClip.height);
                if (bar.H > bar.W) EmitScrollBar(q, bar, bx, by, Math.Max(0, viewerLines.Count - visible), viewerLines.Count, visible, scroll);
                q.SetClip(clip.x, clip.y, clip.width, clip.height);
            }
        }

        /// <summary>
        /// UIScrollBar::OnDraw (0x7ef530), vertical: nothing unless there is something to scroll; else the up arrow at
        /// (0, 1), 13 px mid tiles from b down to h − b, the down arrow at h − b − 1, and the thumb at
        /// b + pos·(h − 2b − len)/range drawn as bar_up, bar_mid every 4 px, bar_down; thumb length (vf53 0x7e2630)
        /// = (h − 2b)·visible/total. b (+0xa0) = the arrow size.
        /// </summary>
        private void EmitScrollBar(RoClassicUiQuads q, RoClassicChildLayout bar, int x, int y, int range, int total, int visible, int pos) {
            if (range <= 0) return;
            int b = (int?) bar.Props?["arrow"] ?? 13;
            int h = bar.H;
            ScrollBitmap(q, bar, "scroll0up", x, y + 1);
            int n = h - b - 14;
            n = n / 13 + (n % 13 != 0 ? 1 : 0);
            for (int i = 0; i < n; i++) ScrollBitmap(q, bar, "scroll0mid", x, y + b + 13 * i, y + h - b);
            ScrollBitmap(q, bar, "scroll0mid", x, y + h - b - 13, y + h - b);
            ScrollBitmap(q, bar, "scroll0down", x, y + h - b - 1);
            int len = total != 0 ? (h - 2 * b) * visible / total : h - 2 * b;
            if ((int?) bar.Props?["minThumb"] != 0 && len < 8) len = 8;
            int top = b + pos * (h - 2 * b - len) / range;
            ScrollBitmap(q, bar, "up", x, y + top);
            for (int t = top + 4; t < top + len - 4; t += 4) ScrollBitmap(q, bar, "mid", x, y + t);
            ScrollBitmap(q, bar, "down", x, y + top + len - 4);
        }

        /// <summary>A scroll bar bitmap by role; <paramref name="clipBottom"/> stands for the key fill that cuts the mid tiles.</summary>
        private void ScrollBitmap(RoClassicUiQuads q, RoClassicChildLayout bar, string role, int x, int y, int clipBottom = int.MaxValue) {
            string path = bar.Props?["bitmaps"]?[role] is Newtonsoft.Json.Linq.JArray a && a.Count > 0 ? (string) a[0] : null;
            if (path == null || !system.Images.TryGet(path, out RoClassicUiImage image)) return;
            if (y + image.Height <= clipBottom) {
                q.Image(x, y, image, Tint(White));
                return;
            }

            int h = clipBottom - y;
            if (h <= 0) return;
            float v = image.Uv.y + (image.Uv.w - image.Uv.y) * h / image.Height;
            q.Quad(x, y, image.Width, h, new Vector4(image.Uv.x, image.Uv.y, image.Uv.z, v), image.Page, Tint(White));
        }

        public static void ScanColourCodes(string s, int start, int end, ref Color32 color) {
            for (int i = start; i < end; i++) {
                if (!IsColourCode(s, i)) continue;
                uint rgb = uint.Parse(s.Substring(i + 1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                color = new Color32((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, 255);
                i += 6;
            }
        }

        private static bool IsColourCode(string s, int i) {
            if (s[i] != '^' || i + 7 > s.Length) return false;
            for (int k = 1; k <= 6; k++) {
                if (!Uri.IsHexDigit(s[i + k])) return false;
            }

            return true;
        }

        /// <summary>
        /// FUN_00915f60: splits a text into lines of at most <paramref name="maxBytes"/> bytes (one byte per character
        /// under langtype 1; ^RRGGBB codes do not count). A line ends at the start of the word that crosses the limit —
        /// so it keeps its trailing spaces — or mid-word when one word is longer than a line; '\n' ends a line and stays
        /// in it. A "[...]" group that would cross the limit moves to the next line whole.
        /// </summary>
        public static void WrapBytes(string text, int maxBytes, List<(int Start, int End)> lines) {
            maxBytes = Math.Max(1, maxBytes);
            lines.Clear();
            int n = text.Length, pos = 0, lineStart = 0, wordStart = 0, limit = maxBytes;
            while (pos < n) {
                if (IsColourCode(text, pos)) {
                    pos += 7;
                    limit += 7;
                    wordStart = pos;
                } else {
                    pos++;
                }

                while (pos < n && text[pos] == ' ') wordStart = ++pos;
                while (pos < n && text[pos] == ']') wordStart = ++pos;
                while (pos < n && text[pos] == '[') {
                    int close = text.IndexOf(']', pos);
                    int len = close < 0 ? n - pos : close + 1 - pos;
                    if (pos + len < limit) {
                        pos++;
                    } else {
                        if (maxBytes <= len) break;
                        limit -= len;
                        pos--;
                    }
                }

                char c = pos < n ? text[pos] : '\0';
                if (pos < limit && c != '\n') continue;
                int next = c == '\n' ? pos + 1 : pos;
                int end = c == '\n' ? pos + 1 : wordStart;
                if (lineStart < end) {
                    lines.Add((lineStart, end));
                    lineStart = end;
                } else {
                    if (lineStart != next) lines.Add((lineStart, next));
                    lineStart = next;
                }

                pos = next;
                wordStart = lineStart;
                limit = lineStart + maxBytes;
            }

            if (lineStart < n) lines.Add((lineStart, n));
        }

        private void DrawColourCoded(RoClassicUiQuads q, RoClassicGdiFont font, int x, int y, string s, int start, int end, ref Color32 color) {
            int pen = x, run = start;
            for (int i = start; i <= end; i++) {
                bool code = i < end && IsColourCode(s, i);
                if (i < end && !code) continue;
                if (i > run) pen += q.Text(font, pen, y, s.Substring(run, i - run), Tint(color));
                if (!code) break;
                uint rgb = uint.Parse(s.Substring(i + 1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                color = new Color32((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, 255);
                i += 6;
                run = i + 1;
            }
        }

        private static string Field(RoClassicChildLayout child, string role, int index) =>
            child.Fields.TryGetValue(role, out string f) ? f :
            child.Fields.TryGetValue(index.ToString(CultureInfo.InvariantCulture), out f) ? f : null;

        private static Color32 PropColor(RoClassicChildLayout child, int index, Color32 fallback) {
            if (child.Props?["colors"] is Newtonsoft.Json.Linq.JArray colors && index < colors.Count) {
                return RoClassicWindowLayout.ParseColor((string) colors[index]);
            }

            return fallback;
        }

        private static string PropString(RoClassicChildLayout child, string name) {
            Newtonsoft.Json.Linq.JToken token = child.Props?[name];
            return token is Newtonsoft.Json.Linq.JArray a ? (a.Count > 0 ? (string) a[0] : null) : (string) token;
        }

        private long GetLong(string field) =>
            field != null && values.TryGetValue(field, out string v) &&
            long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ? n : 0;

        private static string Pad3(long v) {
            string s = v.ToString(CultureInfo.InvariantCulture);
            return s.Length >= 3 ? s : new string(' ', 3 - s.Length) + s;
        }

        // the window alpha applies to the layer as one surface (Alpha), not per vertex
        private static Color32 Tint(Color32 c) => c;

        /// <param name="surfaceStart">
        /// First vertex of the surface these ops draw into (the window's, or a child's own): raw copies cut holes only
        /// into what that surface drew before them.
        /// </param>
        private void Emit(RoClassicUiQuads q, List<RoClassicOp> ops, int ox, int oy, int areaW, int areaH, int surfaceStart,
            Dictionary<string, string> replacedStates = null) {
            for (int i = 0; i < ops.Count; i++) {
                RoClassicOp op = ops[i];
                if (op.Slot != null && (SuppressBoundOps || (hiddenSlots.Count > 0 && hiddenSlots.Contains(op.Slot)))) continue;
                switch (op.Kind) {
                    case RoClassicOpKind.Bitmap:
                        if (replacedStates != null && IsStatePath(replacedStates, op.Path)) break;
                        if (system.Images.TryGet(op.Path, out RoClassicUiImage image)) {
                            // the client blits the bitmap at its own size unless the op records a stretch
                            int w = op.W > 0 ? op.W : image.Width, h = op.H > 0 ? op.H : image.Height;
                            if (!DynamicWidth(op, ref w)) break;
                            int opStart = q.VertexCount;
                            q.Image(ox + op.X, oy + op.Y, w, h, image, Tint(White));
                            if (w == image.Width && h == image.Height) PunchBitmapHoles(q, op.Path, ox + op.X, oy + op.Y, op.RawCopy, surfaceStart, opStart);
                        }

                        break;
                    case RoClassicOpKind.Fill: {
                        // a button's own key-colour clear (UIBmpTextButton) comes before its bitmap: the state bitmap is
                        // already that bitmap, so the clear must not erase it
                        if (op.Transparent && op.Whole && replacedStates != null) break;
                        if (op.Transparent) {
                            // a key-colour fill erases what the surface drew there before
                            if (q.VertexCount > surfaceStart) {
                                if (op.Whole) q.Punch(surfaceStart, q.VertexCount, ox, oy, ox + areaW, oy + areaH);
                                else q.Punch(surfaceStart, q.VertexCount, ox + op.X, oy + op.Y, ox + op.X + op.W, oy + op.Y + op.H);
                            }

                            break;
                        }

                        if (op.Whole) {
                            q.Image(ox, oy, areaW, areaH, system.White, Tint(op.Color));
                            break;
                        }

                        int w = op.W;
                        if (DynamicWidth(op, ref w)) q.Image(ox + op.X, oy + op.Y, w, op.H, system.White, Tint(op.Color));
                        break;
                    }
                    case RoClassicOpKind.Text:
                        EmitText(q, op, ox, oy);
                        break;
                }
            }
        }

        /// <summary>Whether a recorded bitmap is one of the button's state bitmaps (GRF names ignore case).</summary>
        private static bool IsStatePath(Dictionary<string, string> states, string path) {
            foreach (string value in states.Values) {
                if (string.Equals(value, path, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private void EmitText(RoClassicUiQuads q, RoClassicOp op, int ox, int oy) {
            bool bound = op.Slot != null && op.Format != null && !ShowCapturedSamples;
            if (op.Runs != null && !bound) {
                // a piecewise helper: every segment at its recorded pen, colour and font
                foreach (RoClassicTextRun run in op.Runs) {
                    if (!string.IsNullOrEmpty(run.Text)) q.Text(FontOf(run.Font), ox + run.X, oy + run.Y, run.Text, Tint(run.Color));
                }

                return;
            }

            string text = bound ? Format(op.Format) : op.Text;
            if (string.IsNullOrEmpty(text)) return;
            RoClassicGdiFont font = FontOf(op.Font);
            int x = ox + (op.Slot != null && slotX.TryGetValue(op.Slot, out int anchor) ? anchor : op.X), y = oy + op.Y;
            Color32 color = op.Slot != null && slotColor.TryGetValue(op.Slot, out Color32 c) ? c : op.Color;
            if (op.Align == RoClassicTextAlign.Right) {
                x -= font.Measure(text);
            } else if (op.Align == RoClassicTextAlign.Center) {
                // centred in [x, x + boxW), integer halving like the client
                int width = font.Measure(text);
                x += op.BoxW > 0 ? (op.BoxW - width) / 2 : -width / 2;
            }

            if (op.Passes != null) {
                foreach (RoClassicTextPass pass in op.Passes) q.Text(font, x + pass.Dx, y + pass.Dy, text, Tint(pass.Color));
            }

            q.Text(font, x, y, text, Tint(color));
        }

        /// <summary>
        /// The holes a bitmap just drawn at (x, y) leaves: its exact 0xFF00FF pixels when raw-copied, in the surface it
        /// drew into; its near-key pixels (key only after the RGB555 upload, e.g. 255,1,255) in the whole window, as no
        /// keyed blit skips them on the way up.
        /// </summary>
        private void PunchBitmapHoles(RoClassicUiQuads q, string path, int x, int y, bool rawCopy, int surfaceStart, int opStart) {
            if (rawCopy) opStart = PunchRects(q, system.Images.KeyRects(path), x, y, surfaceStart, opStart);
            PunchRects(q, system.Images.NearKeyRects(path), x, y, surfaceFloor, opStart);
        }

        private static int PunchRects(RoClassicUiQuads q, RectInt[] rects, int x, int y, int from, int until) {
            if (rects == null || until <= from) return until;
            foreach (RectInt r in rects) until += q.Punch(from, until, x + r.x, y + r.y, x + r.x + r.width, y + r.y + r.height);
            return until;
        }

        private RoClassicGdiFont FontOf(RoClassicOp op) => FontOf(op.Font);

        private RoClassicGdiFont FontOf(string fontKey) =>
            system.Font(Layout.Fonts.TryGetValue(fontKey ?? string.Empty, out RoClassicFontSpec spec)
                ? spec
                : new RoClassicFontSpec("Arial", 15, 400, 0, 3));

        /// <summary>
        /// Dynamic-width ops (gauges): the bound value of their slot is the width in pixels; unset keeps the recorded
        /// width, 0 or less draws nothing (the client skips empty fills). Returns false when nothing is drawn.
        /// </summary>
        private bool DynamicWidth(RoClassicOp op, ref int width) {
            if (op.Slot == null || op.Dynamic == null || Array.IndexOf(op.Dynamic.Split(','), "w") < 0) return true;
            if (!values.TryGetValue(op.Slot, out string value) ||
                !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w)) {
                return true;
            }

            width = w;
            return w > 0;
        }

        /// <summary>Expands "{Field}" / "{Field:n0}" with the bound values (missing fields stay empty).</summary>
        private string Format(string template) {
            StringBuilder sb = formatBuffer;
            sb.Clear();
            for (int i = 0; i < template.Length; i++) {
                char c = template[i];
                if (c != '{') {
                    sb.Append(c);
                    continue;
                }

                int end = template.IndexOf('}', i + 1);
                if (end < 0) {
                    sb.Append(template, i, template.Length - i);
                    break;
                }

                string token = template.Substring(i + 1, end - i - 1);
                int colon = token.IndexOf(':');
                string head = colon < 0 ? token : token.Substring(0, colon);
                string fmt = colon < 0 ? null : token.Substring(colon + 1);
                int comma = head.IndexOf(',');
                string field = comma < 0 ? head : head.Substring(0, comma);
                int pad = comma < 0 ? 0 : int.Parse(head.Substring(comma + 1), CultureInfo.InvariantCulture);
                if (values.TryGetValue(field, out string value)) {
                    if (fmt != null && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number)) {
                        value = number.ToString(fmt, CultureInfo.InvariantCulture);
                    }

                    if (value.Length < pad) sb.Append(' ', pad - value.Length);
                    sb.Append(value);
                }

                i = end;
            }

            return sb.ToString();
        }
    }
}
