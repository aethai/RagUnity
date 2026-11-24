using System.Collections.Generic;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The talk balloons over actors (scratchpad balloons-spec: CGameActor::SendMsg 7 at 0xbdb2ef, UITransBalloonText 0x7b2c30)
    /// with the classic UI on: one per actor (actor +0x264) for chat 0x008d / 0x008e, NPC chat 0x02c1 and showscript 0x08b3;
    /// a new message replaces the text and restarts the 5000 ms life (0xbd3ef0), then the balloon goes without a fade.
    /// The text is the server's verbatim, wrapped at 35 bytes (after the last run of spaces, else hard); each line at
    /// (6, 4 + 19 i) in white (or the 0x02c1 colour, which then stays on the balloon) over a black (+1, +1) shadow, Arial
    /// cell 15; the box is the widest line + 12 by 4 + 19 n, black at alpha 0x66 with a rounded #5C5C5C outline. Placed
    /// every frame at x = feet x − w / 2, y = feet y − K · scale − h (K = H · 81 / 480 lines times the actor's scale over H · 1.34 / 768: about 96.7
    /// sprite pixels as the camera draws the actor), clamped to the screen with a 3 px margin; under
    /// every window (the client inserts the balloons at the bottom of its window list). Geometry is rebuilt only when a
    /// balloon comes, changes or goes; positions move through renderer anchors.
    /// </summary>
    public sealed class RoClassicChatBalloons : IRoClassicUiProducer {
        private const float LifeSeconds = 5f;
        private const int WrapBytes = 35, LineStep = 19, PadX = 6, PadY = 4, FontHeight = 12;
        private static readonly Color32 Box = new Color32(0, 0, 0, 0x66);
        private static readonly Color32 Border = new Color32(0x5c, 0x5c, 0x5c, 255);
        private static readonly Color32 Shadow = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private sealed class Balloon {
            public uint ActorId;
            public Transform Follow;
            public Vector3 World;
            public string Text = string.Empty;
            public Color32 Color = White;
            public float Born;
            public int Anchor;
            public int Width, Height;
            public readonly List<(int Start, int End)> Lines = new List<(int, int)>();
            /// <summary>The wrapped lines cut once per message (a rebuild redraws every balloon, not only the new one).</summary>
            public readonly List<string> LineTexts = new List<string>();
            /// <summary>The widest line in pixels, measured once per message (-1 = not yet).</summary>
            public int Widest = -1;
            public RoClassicGdiFont MeasuredWith;
        }

        private static RoClassicChatBalloons instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private readonly Dictionary<uint, Balloon> balloons = new Dictionary<uint, Balloon>();
        private readonly List<uint> expired = new List<uint>();
        private readonly Stack<int> freeAnchors = new Stack<int>();
        private bool dirty;

        private static RoClassicChatBalloons Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicChatBalloons { system = system };
                instance.layer = system.Renderer.CreateLayer("chat balloons");
                instance.layer.AboveOverlayUi = true;
                system.Renderer.SendToBack(instance.layer);
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>
        /// Msg 7 to an actor: <paramref name="text"/> in its balloon (made when it has none). <paramref name="color"/> is the
        /// 0x02c1 colour; null keeps the balloon's (white for a new one).
        /// </summary>
        public static void Show(uint actorId, Transform follow, Vector3 world, string text, Color32? color = null) {
            if (actorId == 0 || string.IsNullOrEmpty(text)) return;
            RoClassicChatBalloons b = Ensure();
            if (b == null) return;
            if (!b.balloons.TryGetValue(actorId, out Balloon balloon)) {
                balloon = new Balloon { ActorId = actorId, Anchor = b.freeAnchors.Count > 0 ? b.freeAnchors.Pop() : b.system.Renderer.AllocateAnchor() };
                b.balloons.Add(actorId, balloon);
            }

            balloon.Follow = follow;
            balloon.World = world;
            // case 7 (0xbdb302): ConvertItemLinks 0x87b120, the labels as plain balloon text cut to half the chat box (296 px)
            if (text != null && text.IndexOf("<ITEML>", System.StringComparison.Ordinal) >= 0) {
                text = RoClassicItemLink.Convert(text, RoClassicUiHost.GameAssets()?.GameData?.Items, b.system.Font(12), 296, null);
            }

            balloon.Text = text;
            if (color.HasValue) balloon.Color = color.Value;
            balloon.Born = Time.unscaledTime;
            RoClassicWindow.WrapBytes(text, WrapBytes, balloon.Lines);
            balloon.LineTexts.Clear();
            foreach ((int start, int end) in balloon.Lines) balloon.LineTexts.Add(text.Substring(start, end - start));
            balloon.Widest = -1;
            b.dirty = true;
        }

        /// <summary>The actor left (vanish): its balloon goes.</summary>
        public static void Clear(uint actorId) {
            if (instance == null || !instance.balloons.TryGetValue(actorId, out Balloon balloon)) return;
            instance.Remove(balloon);
        }

        /// <summary>A map change or a return to the login: every balloon goes.</summary>
        public static void ClearAll() {
            if (instance == null) return;
            instance.expired.Clear();
            instance.expired.AddRange(instance.balloons.Keys);
            foreach (uint id in instance.expired) instance.Remove(instance.balloons[id]);
            instance.expired.Clear();
        }

        private void Remove(Balloon balloon) {
            balloons.Remove(balloon.ActorId);
            system.Renderer.SetAnchor(balloon.Anchor, 0, 0, false);
            freeAnchors.Push(balloon.Anchor);
            dirty = true;
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (balloons.Count == 0 && !dirty) return;
            float now = Time.unscaledTime;
            expired.Clear();
            foreach (Balloon balloon in balloons.Values) {
                if (now - balloon.Born >= LifeSeconds) expired.Add(balloon.ActorId);
                // a destroyed actor object leaves the balloon where it was last seen until its time is up
                else if (balloon.Follow == null && !ReferenceEquals(balloon.Follow, null)) balloon.Follow = null;
            }

            foreach (uint id in expired) Remove(balloons[id]);
            expired.Clear();
            if (dirty) Rebuild(ui);
            Place(ui);
        }

        /// <summary>Every balloon's box, outline and lines, each on its own anchor (its top-left corner).</summary>
        private void Rebuild(RoClassicUiSystem ui) {
            dirty = false;
            RoClassicGdiFont font = ui.Font(FontHeight);
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            foreach (Balloon balloon in balloons.Values) {
                if (balloon.Widest < 0 || !ReferenceEquals(balloon.MeasuredWith, font)) {
                    balloon.MeasuredWith = font;
                    balloon.Widest = 0;
                    foreach (string line in balloon.LineTexts) balloon.Widest = Mathf.Max(balloon.Widest, font.Measure(line));
                }

                balloon.Width = balloon.Widest + 2 * PadX;
                balloon.Height = PadY + balloon.LineTexts.Count * LineStep;
                q.Anchor = balloon.Anchor;
                q.Image(0, 0, balloon.Width, balloon.Height, ui.White, Box);
                foreach (RectInt r in RoClassicWindow.RoundOutlineRects(0, 0, balloon.Width, balloon.Height)) q.Image(r.x, r.y, r.width, r.height, ui.White, Border);
                for (int i = 0; i < balloon.LineTexts.Count; i++) {
                    string line = balloon.LineTexts[i];
                    q.Text(font, PadX + 1, PadY + LineStep * i + 1, line, Shadow);
                    q.Text(font, PadX, PadY + LineStep * i, line, balloon.Color);
                }
            }

            q.Anchor = 0;
            layer.MarkDirty();
        }

        /// <summary>CGameActor's per-tick placement over the feet, clamped to the screen (in client pixels, anchors in screen pixels).</summary>
        private void Place(RoClassicUiSystem ui) {
            UnityEngine.Camera camera = ui.Camera;
            if (camera == null) return;
            float s = ui.Scale;
            int screenH = camera.pixelHeight;
            int clientW = (int) (camera.pixelWidth / s), clientH = (int) (screenH / s);
            Vector3 up = camera.transform.up * (RoClassicSignBoards.LiftSpritePixels(clientH) / ROIO.Models.FileTypes.SPR.PIXELS_PER_UNIT);
            foreach (Balloon balloon in balloons.Values) {
                if (balloon.Follow != null) balloon.World = balloon.Follow.position;
                Vector3 world = balloon.World;
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f) {
                    ui.Renderer.SetAnchor(balloon.Anchor, 0, 0, false);
                    continue;
                }

                int footX = (int) (screen.x / s), footY = (int) ((screenH - screen.y) / s);
                // K · actor scale: 81 sprite pixels (K at 480 lines) as the camera draws this actor's sprites now
                int lift = Mathf.Max(0, (int) ((camera.WorldToScreenPoint(world + up).y - screen.y) / s));
                int x = Mathf.Min(Mathf.Max(footX - balloon.Width / 2, -3), clientW - balloon.Width + 3);
                int y = Mathf.Min(Mathf.Max(footY - lift - balloon.Height, -3), clientH - balloon.Height + 3);
                ui.Renderer.SetAnchor(balloon.Anchor, Mathf.RoundToInt(x * s), Mathf.RoundToInt(y * s), true);
            }
        }
    }
}
