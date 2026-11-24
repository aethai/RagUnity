using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// The hover name balloon of the original client (<c>UINameBalloonText</c>): lines drawn by <c>TextOutOutline</c>
    /// (0x9b5bb0: the outline colour at (x±1, y) and (x, y±1), then the text) from (4, 4) with a 2 + 12 px line step,
    /// sized max(32, widest line + 8) × (lines × 14 + 6) (vf54 0x7c7560), placed by <c>FUN_00c003b0</c> under the
    /// actor: x = actor.x − w / 2, y = actor.y + (int)((screenH × 20 / 480) × actor scale), clamped to the screen with
    /// a 3 px margin. Default colours from the constructor (0x7b22f0): white text, black outline.
    /// </summary>
    public sealed class RoClassicNameBalloon : IRoClassicUiProducer {
        private const int FontRequestHeight = 12; // DAT_0116b974
        private const int LineStep = FontRequestHeight + 2;

        /// <summary>
        /// /showname ([0x01552da8] → 0xa029e0 → [0x014ef126]): the lines are measured (vf54 0x7c7560) and drawn (OnDraw
        /// 0x7be7b0, TextOutOutline's bold argument) in the bold font while it is on ("Normal Font", type 1), the regular
        /// one while it is off ("thin", type 2). Set by the game.
        /// </summary>
        public static bool Bold { get; set; } = true;

        /// <summary>
        /// /font: CGameMode+0x46c, the place 0xc003b0 gives the balloon of the own player (actor +0xa8, set only by
        /// CPlayer's init 0xcd1760): 0 under the actor (+screenH·20/480), 1 over it (−screenH·100/480), both centred;
        /// 2 / 3 left / right of it ((∓screenW·30/640)·scale − w/2, (−screenH·25/480)·scale − h/2; case 0x59 of 0xc07a00
        /// cycles 0..3, but no word of this build gives that type). Set by the game.
        /// </summary>
        public static int OwnNamePlacement { get; set; }

        private static RoClassicNameBalloon hover;
        private readonly List<string> lines = new List<string>(2);
        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private int anchorSlot;
        private Color32 textColor = new Color32(255, 255, 255, 255);
        private Color32 outlineColor = new Color32(0, 0, 0, 255);
        private bool shown;
        private bool geometryDirty;
        private int width;
        private int height;
        private int actorX;
        private int actorY;
        private float actorScale = 1f;
        private string emblem;   // + 0xa0, the emblem drawn (null: none or not cached yet)
        private bool emblemFrame;   // + 0xbd
        private bool ownPlayer;     // the actor's +0xa8 (the local player): /font moves its balloon
        private bool drawnBold = true;
        private readonly string[] pendingLines = new string[3];

        /// <summary>The single balloon shown for the actor under the cursor.</summary>
        public static RoClassicNameBalloon Hover {
            get {
                if (hover == null || hover.system == null) {
                    hover = new RoClassicNameBalloon();
                    hover.system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
                    hover.layer = hover.system.Renderer.CreateLayer("name balloon");
                    hover.system.Renderer.SendToBack(hover.layer);
                    hover.anchorSlot = hover.system.Renderer.AllocateAnchor();
                    hover.system.Register(hover);
                }

                return hover;
            }
        }

        /// <summary>Shows <paramref name="primary"/> (and an optional second line) for an actor at the given screen
        /// pixel (top-left origin; the actor's ground position as the client projects it); <paramref name="own"/> for the
        /// local player (its place follows /font).</summary>
        public void Show(string primary, string secondary, Color32 color, int screenX, int screenY, float scale = 1f, bool own = false) =>
            Show(primary, secondary, null, color, screenX, screenY, scale, own);

        /// <summary>
        /// Up to three lines (vf54 0x7c7560 adds each line of the name card that is not empty, the first always), top
        /// to bottom.
        /// </summary>
        public void Show(string first, string second, string third, Color32 color, int screenX, int screenY, float scale = 1f, bool own = false) {
            ownPlayer = own;
            if (drawnBold != Bold) {
                drawnBold = Bold;
                geometryDirty = true;
            }

            int count = 0;
            pendingLines[count++] = first ?? string.Empty;
            if (!string.IsNullOrEmpty(second)) pendingLines[count++] = second;
            if (!string.IsNullOrEmpty(third)) pendingLines[count++] = third;
            bool linesChanged = lines.Count != count;
            for (int i = 0; i < count && !linesChanged; i++) linesChanged = lines[i] != pendingLines[i];
            if (linesChanged) {
                lines.Clear();
                for (int i = 0; i < count; i++) lines.Add(pendingLines[i]);
                geometryDirty = true;
            }

            if (!textColor.Equals(color)) {
                textColor = color;
                geometryDirty = true;
            }

            if (!shown) geometryDirty = true;
            shown = true;
            actorX = screenX;
            actorY = screenY;
            actorScale = scale;
        }

        /// <summary>
        /// SetEmblem vf53 0x7c7050: the guild emblem's image key (null for none, or while it is not cached) and the siege
        /// frame. With an emblem the balloon is text width + 25 wide and at least 24 high (+ 29 / 28 with the frame, which
        /// also takes its room before the emblem lands); the emblem at (4, (H − 24) / 2), the text from x 32.
        /// </summary>
        public void SetEmblem(string key, bool frame) {
            if (key == emblem && frame == emblemFrame) return;
            emblem = key;
            emblemFrame = frame;
            geometryDirty = true;
        }

        public void Hide() {
            if (!shown) return;
            shown = false;
            system.Renderer.SetAnchor(anchorSlot, 0, 0, false);
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (!shown) return;
            RoClassicGdiFont font = ui.Font(FontRequestHeight, drawnBold);
            if (geometryDirty) {
                geometryDirty = false;
                width = 32;
                for (int i = 0; i < lines.Count; i++) width = Mathf.Max(width, font.Measure(lines[i]) + 8);
                height = lines.Count * LineStep + 6;
                RoClassicUiImage emblemImage = default;
                bool hasEmblem = emblem != null && ui.Images.TryGet(emblem, out emblemImage);
                if (emblemFrame) {
                    width += 29;
                    height = Mathf.Max(height, 28);
                } else if (hasEmblem) {
                    width += 25;
                    height = Mathf.Max(height, 24);
                }

                RoClassicUiQuads q = layer.Quads;
                q.Clear();
                q.Anchor = anchorSlot;
                // OnDraw vf20 0x7be7b0: the frame at (2, (H − 28) / 2), the emblem at (4, (H − 24) / 2), the text from x 32
                int textX = 4;
                if (emblemFrame && ui.Images.TryGet("유저인터페이스\\emblem_frame.bmp", out RoClassicUiImage frameImage)) {
                    q.Image(2, (height - 28) / 2, frameImage, new Color32(255, 255, 255, 255));
                }

                if (emblemFrame) textX = 32;
                if (hasEmblem) {
                    q.Image(4, (height - 24) / 2, emblemImage, new Color32(255, 255, 255, 255));
                    textX = 32;
                }

                for (int i = 0; i < lines.Count; i++) {
                    int x = textX, y = 4 + i * LineStep;
                    q.Text(font, x - 1, y, lines[i], outlineColor);
                    q.Text(font, x + 1, y, lines[i], outlineColor);
                    q.Text(font, x, y - 1, lines[i], outlineColor);
                    q.Text(font, x, y + 1, lines[i], outlineColor);
                    q.Text(font, x, y, lines[i], textColor);
                }

                q.Anchor = 0;
                layer.MarkDirty();
            }

            UnityEngine.Camera camera = ui.Camera;
            int screenW = camera != null ? camera.pixelWidth : Screen.width;
            int screenH = camera != null ? camera.pixelHeight : Screen.height;
            // 0xc003b0: mode +0x46c only for the own player; 0 under the actor, 1 (/font) over it, 2 / 3 beside it
            int offsetX = 0, offsetY, centreY = 0;
            switch (ownPlayer ? OwnNamePlacement : 0) {
                case 1:
                    offsetY = (int) ((screenH * -100 / 480) * actorScale);
                    break;
                case 2:
                case 3:
                    offsetX = (int) ((screenW * 15 * (OwnNamePlacement == 2 ? -2 : 2) / 640) * actorScale);
                    offsetY = (int) ((screenH * -25 / 480) * actorScale);
                    centreY = height / 2;
                    break;
                default:
                    offsetY = (int) ((screenH * 20 / 480) * actorScale);
                    break;
            }

            // the balloon is laid out in client pixels (screen / UI scale), its anchor stays in screen pixels
            float s = ui.Scale;
            int clientW = (int) (screenW / s), clientH = (int) (screenH / s);
            int bx = Mathf.Min(Mathf.Max((int) ((actorX + offsetX) / s) - width / 2, -3), clientW - width + 3);
            int by = Mathf.Min(Mathf.Max((int) ((actorY + offsetY) / s) - centreY, -3), clientH - height + 3);
            ui.Renderer.SetAnchor(anchorSlot, Mathf.RoundToInt(bx * s), Mathf.RoundToInt(by * s), true);
        }
    }
}
