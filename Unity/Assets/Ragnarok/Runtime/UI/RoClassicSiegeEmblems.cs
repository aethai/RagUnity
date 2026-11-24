using System.Collections.Generic;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UIEmblemWnd over every guild player's head on a siege map (guild-emblem-castle-spec A.6.3, vtable 0xf98e28): made by
    /// 0xbfb8f0 for a player with a guild (not while hidden or cloaked); OnProcess 0x7c10c0 moves it each tick to
    /// (sx − (W·10/640)·zoom, sy − (H·100/480)·zoom) from the actor's screen point; OnDraw 0x7be420 draws the emblem at (0, 0),
    /// or with the emblem border option (/frame, [0x015556bc]; the window is 28×28 then, 0x7c10c0) emblem_frame.bmp at (0, 0)
    /// and the emblem at (2, 2). Nothing while off a siege map or on a battlefield one (world+0x50).
    /// </summary>
    public sealed class RoClassicSiegeEmblems : IRoClassicUiProducer {
        private static RoClassicSiegeEmblems instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private sealed class Mark {
            public int Anchor;
            public string Key;
            public bool Seen;
        }

        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private readonly Dictionary<uint, Mark> marks = new Dictionary<uint, Mark>();
        private readonly Stack<int> freeAnchors = new Stack<int>();
        private readonly List<RoOnlineActorSnapshot> actors = new List<RoOnlineActorSnapshot>();
        private readonly List<uint> gone = new List<uint>();
        private RoOnlineWorldController world;
        private bool dirty;
        private bool drawnFrame;

        /// <summary>Per frame from the chat window's classic ticks.</summary>
        public static void Tick() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (instance == null || instance.system != system) {
                instance = new RoClassicSiegeEmblems { system = system };
                instance.layer = system.Renderer.CreateLayer("siege emblems");
                system.Renderer.SendToBack(instance.layer);
                system.Register(instance);
            }
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            // OnDraw 0x7be420 draws only on a siege map (world+0x4c) that is not a battlefield (world+0x50)
            if (!RoPvpState.IsSiege || RoPvpState.IsBattlefield) {
                if (marks.Count > 0) Clear();
                return;
            }

            if (world == null) world = Object.FindFirstObjectByType<RoOnlineWorldController>();
            if (world == null) return;
            if (drawnFrame != RoClassicCommandFlags.EmblemFrame) dirty = true;
            foreach (Mark m in marks.Values) m.Seen = false;
            world.State.SnapshotActors(actors);
            foreach (RoOnlineActorSnapshot actor in actors) {
                // 0xbfb8f0: players with a guild; hidden (0x02), cloaked (0x04) and chase-walking (0x4000) ones get none
                if (actor.ObjectType != Ragnarok.Network.RoMapActorObjectType.Player || actor.GuildId == 0 || (actor.EffectState & 0x4006) != 0) continue;
                string key = RoClassicGuildEmblems.Resolve(actor.GuildId);
                if (key == null) continue;
                if (!marks.TryGetValue(actor.ServerId, out Mark mark)) {
                    mark = new Mark { Anchor = freeAnchors.Count > 0 ? freeAnchors.Pop() : ui.Renderer.AllocateAnchor() };
                    marks.Add(actor.ServerId, mark);
                    dirty = true;
                }

                if (mark.Key != key) {
                    mark.Key = key;
                    dirty = true;
                }

                mark.Seen = true;
            }

            gone.Clear();
            foreach (KeyValuePair<uint, Mark> pair in marks) {
                if (!pair.Value.Seen) gone.Add(pair.Key);
            }

            foreach (uint id in gone) Remove(id);
            if (dirty) Rebuild(ui);
            Place(ui);
        }

        private void Remove(uint id) {
            Mark mark = marks[id];
            system.Renderer.SetAnchor(mark.Anchor, 0, 0, false);
            freeAnchors.Push(mark.Anchor);
            marks.Remove(id);
            dirty = true;
        }

        private void Clear() {
            gone.Clear();
            gone.AddRange(marks.Keys);
            foreach (uint id in gone) Remove(id);
            Rebuild(system);
        }

        private void Rebuild(RoClassicUiSystem ui) {
            dirty = false;
            drawnFrame = RoClassicCommandFlags.EmblemFrame;
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            RoClassicUiImage frame = default;
            bool framed = drawnFrame && ui.Images.TryGet("유저인터페이스\\emblem_frame.bmp", out frame);
            foreach (Mark mark in marks.Values) {
                if (!ui.Images.TryGet(mark.Key, out RoClassicUiImage image)) continue;
                q.Anchor = mark.Anchor;
                if (framed) q.Image(0, 0, frame, new Color32(255, 255, 255, 255));
                q.Image(framed ? 2 : 0, framed ? 2 : 0, image, new Color32(255, 255, 255, 255));
            }

            if (drawnFrame && !framed && marks.Count > 0) dirty = true;   // the frame bitmap not decoded yet: again next frame

            q.Anchor = 0;
            layer.MarkDirty();
        }

        /// <summary>OnProcess 0x7c10c0: (W·10/640)·zoom left and (H·100/480)·zoom up of the actor's screen point.</summary>
        private void Place(RoClassicUiSystem ui) {
            UnityEngine.Camera camera = ui.Camera;
            if (camera == null) return;
            float s = ui.Scale;
            int screenH = camera.pixelHeight, clientW = (int) (camera.pixelWidth / s), clientH = (int) (screenH / s);
            // a client pixel at 480 lines is 768 / (1.34 · H) sprite pixels (RoClassicSignBoards' lift); the camera's zoom is in the projection
            float perSpritePixel = 768f / (1.34f * clientH);
            Vector3 up = camera.transform.up * (100f * clientH / 480f * perSpritePixel / ROIO.Models.FileTypes.SPR.PIXELS_PER_UNIT);
            Vector3 right = camera.transform.right * (10f * clientW / 640f * perSpritePixel / ROIO.Models.FileTypes.SPR.PIXELS_PER_UNIT);
            foreach (KeyValuePair<uint, Mark> pair in marks) {
                if (!world.TryResolveActorAnchor(pair.Key, out Transform follow, out Vector3 at)) {
                    ui.Renderer.SetAnchor(pair.Value.Anchor, 0, 0, false);
                    continue;
                }

                if (follow != null) at = follow.position;
                Vector3 screen = camera.WorldToScreenPoint(at);
                if (screen.z <= 0f) {
                    ui.Renderer.SetAnchor(pair.Value.Anchor, 0, 0, false);
                    continue;
                }

                int sx = (int) (screen.x / s), sy = (int) ((screenH - screen.y) / s);
                int dy = (int) ((camera.WorldToScreenPoint(at + up).y - screen.y) / s);
                int dx = (int) ((camera.WorldToScreenPoint(at + right).x - screen.x) / s);
                ui.Renderer.SetAnchor(pair.Value.Anchor, Mathf.RoundToInt((sx - dx) * s), Mathf.RoundToInt((sy - dy) * s), true);
            }
        }

        internal static string Sweep() => instance == null ? "none" : "marks=" + instance.marks.Count + " siege=" + RoPvpState.IsSiege;
    }
}
