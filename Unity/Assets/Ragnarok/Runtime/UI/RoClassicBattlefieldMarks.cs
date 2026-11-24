using System.Collections.Generic;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The enemy mark over the other team's players on a battlefield: ZC_BATTLEFIELD_NOTIFY_CAMPINFO (inline 0xc2f48f) stores
    /// the actor's team (+0xfb) and, when it differs from the own one (DAT_0154ebc4), 0xc002a0 makes a 24 × 24
    /// UIWordDisplayWnd (vtable 0xf98f00) for it (actor +0x18c) unless the actor is hidden (0x02), cloaked (0x04) or invisible
    /// (0x40) (0xd00a90 / 0xd00920 / 0xd00be0); a camp equal to the own one destroys the window. OnDraw 0x7bf910 draws
    /// 유저인터페이스\전장적표시.bmp at (0, 0); OnProcess 0x7c1300 moves it to (sx − (W·5/640)·zoom, sy − (H·100/480)·zoom)
    /// from the actor's screen point. Here the marks follow the actors' teams every frame, on a battlefield map only.
    /// </summary>
    public sealed class RoClassicBattlefieldMarks : IRoClassicUiProducer {
        public const string EnemyMarkPath = "유저인터페이스\\전장적표시.bmp";

        private static RoClassicBattlefieldMarks instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private sealed class Mark {
            public int Anchor;
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

        /// <summary>Per frame from the classic game UI's ticks.</summary>
        public static void Tick() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (instance == null || instance.system != system) {
                instance = new RoClassicBattlefieldMarks { system = system };
                instance.layer = system.Renderer.CreateLayer("battlefield marks");
                system.Renderer.SendToBack(instance.layer);
                system.Register(instance);
            }
        }

        /// <summary>0xc002a0's test: a player of another team, not hidden, cloaked or invisible.</summary>
        public static bool ShowsEnemyMark(RoOnlineActorSnapshot actor, int ownTeam) =>
            actor.ObjectType == Ragnarok.Network.RoMapActorObjectType.Player && !actor.IsSelf && actor.BattlefieldTeam != ownTeam &&
            (actor.EffectState & 0x46) == 0;

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (!RoPvpState.IsBattlefield) {
                if (marks.Count > 0) Clear();
                return;
            }

            if (world == null) world = Object.FindFirstObjectByType<RoOnlineWorldController>();
            if (world == null) return;
            foreach (Mark m in marks.Values) m.Seen = false;
            world.State.SnapshotActors(actors);
            int ownTeam = RoPickHostility.OwnBattlefieldTeam;
            foreach (RoOnlineActorSnapshot actor in actors) {
                if (!ShowsEnemyMark(actor, ownTeam)) continue;
                if (!marks.TryGetValue(actor.ServerId, out Mark mark)) {
                    mark = new Mark { Anchor = freeAnchors.Count > 0 ? freeAnchors.Pop() : ui.Renderer.AllocateAnchor() };
                    marks.Add(actor.ServerId, mark);
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
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            if (ui.Images.TryGet(EnemyMarkPath, out RoClassicUiImage image)) {
                foreach (Mark mark in marks.Values) {
                    q.Anchor = mark.Anchor;
                    q.Image(0, 0, image, new Color32(255, 255, 255, 255));
                }
            } else if (marks.Count > 0) {
                dirty = true;   // the bitmap not decoded yet: again next frame
            }

            q.Anchor = 0;
            layer.MarkDirty();
        }

        /// <summary>OnProcess 0x7c1300: (W·5/640)·zoom left and (H·100/480)·zoom up of the actor's screen point.</summary>
        private void Place(RoClassicUiSystem ui) {
            UnityEngine.Camera camera = ui.Camera;
            if (camera == null) return;
            float s = ui.Scale;
            int screenH = camera.pixelHeight, clientW = (int) (camera.pixelWidth / s), clientH = (int) (screenH / s);
            // a client pixel at 480 lines is 768 / (1.34 · H) sprite pixels (RoClassicSignBoards' lift); the camera's zoom is in the projection
            float perSpritePixel = 768f / (1.34f * clientH);
            Vector3 up = camera.transform.up * (100f * clientH / 480f * perSpritePixel / ROIO.Models.FileTypes.SPR.PIXELS_PER_UNIT);
            Vector3 right = camera.transform.right * (5f * clientW / 640f * perSpritePixel / ROIO.Models.FileTypes.SPR.PIXELS_PER_UNIT);
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

        internal static string Sweep() => instance == null
            ? "none"
            : "marks=" + instance.marks.Count + " bg=" + RoPvpState.IsBattlefield + " ownTeam=" + RoPickHostility.OwnBattlefieldTeam;
    }
}
