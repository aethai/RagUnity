using System;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Input;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Pet taming (codex-ui-research RUNTIME-RULES-COMPANIONS-MAIL.md "pet taming 0x5b"): ZC_START_CAPTURE puts the game into
    /// capture targeting; an actor click opens UIPetTamingDeceiveWnd 0x5b (196 × 256, centred) with data\sprite\slotmachine.
    /// Its draw (vf20 0x84f880) is draw-count based: frame = counter / divisor of action 0 (reels + pointer); the one-shot
    /// left-down (vf25 0x853d40) switches to action 1, sets the stop frame 7 and sends CZ_TRYCAPTURE_MONSTER; the result
    /// (0x00c9a7e0) stops on frame 3 (failure) or 7 (success) with divisor and threshold 7; vf19 0x855760 deletes the window
    /// 2000 ms after the stop frame is first held.
    /// </summary>
    public sealed class RoClassicPetTaming : IRoClassicUiProducer {
        private const int Width = 196, Height = 256;
        private const string SpritePath = "data/sprite/slotmachine";

        private static RoClassicPetTaming instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicWindow window;
        private int seenStart = -1, seenResult = -1;
        private uint actorId;
        private int action, counter, divisor = 1, stopFrame = -1, threshold = 10000;
        private float holdStarted = -1f;
        private bool clicked;
        private ACT act;
        private RoAssetDatabase loadedFor;
        private readonly bool[] imageReady = new bool[16];

        public static void Tick(RoLoginShellSession session) {
            if (session == null) return;
            RoClassicPetTaming t = Ensure();
            if (t == null) return;
            t.session = session;
            (int start, int result, int resultSerial) = session.PetCaptureSnapshot();
            if (t.seenStart >= 0 && start != t.seenStart) t.StartTargeting();
            t.seenStart = start;
            if (t.seenResult >= 0 && resultSerial != t.seenResult) t.OnResult(result);
            t.seenResult = resultSerial;
        }

        private static RoClassicPetTaming Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicPetTaming { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>0x019e: targeting state 2 (no window yet).</summary>
        private void StartTargeting() {
            RoSkillTargetSelection.EnsureInScene().BeginCapture(session, Open);
        }

        /// <summary>Mode 0x45 with 0x2710: MakeWindow(0x5b) and Msg 0x50 (0, actor).</summary>
        private void Open(uint target) {
            if (!EnsureSprites()) return;
            if (window == null) {
                window = new RoClassicWindow(system, new RoClassicWindowLayout { Class = "UIPetTamingDeceiveWnd", WindowId = 0x5b, Width = Width, Height = Height }) {
                    Visible = false,
                    Draggable = false,
                    ClickableAt = _ => true,
                };
                window.DrawDynamic += Draw;
                window.Pressed += (w, local) => Click();
            }

            actorId = target;
            action = 0;
            counter = 0;
            divisor = 1;
            stopFrame = -1;
            threshold = 10000;
            holdStarted = -1f;
            clicked = false;
            Vector2Int client = system.ClientSize;
            window.MoveTo((client.x - Width) / 2, (client.y - Height) / 2);
            window.Visible = true;
            system.Windows.BringToFront(window);
            window.Invalidate();
        }

        /// <summary>vf25: once, while no other capture is pending: action 1, stop frame 7, mode 0x95 → CZ_TRYCAPTURE_MONSTER.</summary>
        private void Click() {
            if (clicked || action != 0 || session == null) return;
            clicked = true;
            action = 1;
            stopFrame = 7;
            _ = session.BeginLegacyTryCapture(actorId);
        }

        /// <summary>0x01a0 → Msg 0x50 (1, 3) for 0, (1, 7) otherwise; the hold timer is not restarted.</summary>
        private void OnResult(int result) {
            if (window == null || !window.Visible) return;
            stopFrame = result == 0 ? 3 : 7;
            divisor = 7;
            threshold = 7;
        }

        private int FrameCount => act != null && action < act.actions.Length ? Math.Max(1, act.actions[action].frames.Length) : 1;

        private bool Holding(int frame) => stopFrame >= 0 && frame == stopFrame && divisor <= threshold;

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (window == null || !window.Visible) return;
            // vf19: the window goes 2000 ms after the stop frame is first held
            int frame = counter / Math.Max(1, divisor);
            if (Holding(frame)) {
                if (holdStarted < 0f) holdStarted = Time.realtimeSinceStartup;
                else if (Time.realtimeSinceStartup - holdStarted >= 2f) {
                    window.Visible = false;
                    return;
                }
            }

            window.Invalidate();
        }

        /// <summary>One draw: the current frame's layers at (98 + x, 128 + y), then the counter step.</summary>
        private void Draw(RoClassicWindow w) {
            int frames = FrameCount;
            int frame = counter / Math.Max(1, divisor);
            int shown = frame < frames ? frame : 0;
            if (act != null && action < act.actions.Length && shown < act.actions[action].frames.Length) {
                ACT.Frame f = act.actions[action].frames[shown];
                foreach (ACT.Layer layer in f.layers ?? Array.Empty<ACT.Layer>()) {
                    if (layer.index < 0 || layer.index >= imageReady.Length || !imageReady[layer.index]) continue;
                    string key = Key(layer.index);
                    if (!system.Images.TryGet(key, out RoClassicUiImage image)) continue;
                    w.DrawBitmap(98 + layer.pos.x - image.Width / 2, 128 + layer.pos.y - image.Height / 2, key);
                }
            }

            // the wrap is tested after the draw: an out-of-range index shows frame 0, so frame 0 draws twice at the boundary
            if (!Holding(frame)) counter++;
            if (frame >= frames) {
                counter = 0;
                if (stopFrame >= 0) divisor++;
            }
        }

        private static string Key(int index) => "@slotmachine-" + index;

        /// <summary>slotmachine.spr / .act from data.grf, every palette image as a dynamic bitmap.</summary>
        private bool EnsureSprites() {
            var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            if (assets == null || !assets.IsInitialized) return false;
            if (ReferenceEquals(assets, loadedFor) && act != null) return true;
            SPR spr = assets.Load<SPR>(SpritePath + ".spr");
            act = assets.Load<ACT>(SpritePath + ".act");
            if (spr == null || act == null) return false;
            loadedFor = assets;
            Array.Clear(imageReady, 0, imageReady.Length);
            for (int i = 0; i < imageReady.Length && spr.TryGetFrame(i, out SPR.Frame image); i++) {
                if (image.type != SPR.TYPE_PAL) continue;
                int width = image.originalWidth > 0 ? image.originalWidth : image.width;
                int height = image.originalHeight > 0 ? image.originalHeight : image.height;
                var pixels = new Color32[width * height];
                for (int p = 0; p < pixels.Length && p < image.data.Length; p++) {
                    int c = image.data[p];
                    if (c != 0) pixels[p] = new Color32(spr.palette[c * 4], spr.palette[c * 4 + 1], spr.palette[c * 4 + 2], 255);
                }

                imageReady[i] = system.Images.SetDynamic(Key(i), width, height, pixels);
            }

            return true;
        }

        /// <summary>The sweep console: start | open aid | click | result n | state.</summary>
        public static string Sweep(string command) {
            RoClassicPetTaming t = Ensure();
            if (t == null || t.session == null) return "no classic ui or session yet";
            string[] parts = command.Trim().Split(' ');
            uint arg = parts.Length > 1 && uint.TryParse(parts[1], out uint v) ? v : 0;
            switch (parts[0]) {
                case "start":
                    t.StartTargeting();
                    break;
                case "open":
                    t.Open(arg);
                    break;
                case "click":
                    t.Click();
                    break;
                case "pick":
                    // an actor click through the targeting overlay (what RoClickToMoveController does for CastOnEntity)
                    foreach (var marker in UnityEngine.Object.FindObjectsByType<RoOnlinePointerTargetMarker>(FindObjectsSortMode.None)) {
                        if (marker.ServerId != arg) continue;
                        var target = new RoPointerTarget(RoPointerTargetKind.Monster, RoCursorState.Attack, 0, true, true, Vector2Int.zero, marker.transform.position, marker.gameObject, string.Empty);
                        RoSkillTargetSelection.Current?.TrySubmitEntityTarget(target, RoPointerModifierFlags.None);
                        break;
                    }

                    break;
                case "use":
                    // use the first inventory item with this id (the lure; the server answers ZC_START_CAPTURE)
                    foreach (RoItemInstance item in t.session.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>()) {
                        if (item.ItemId != (int) arg) continue;
                        _ = t.session.BeginLegacyUseInventoryItem(item.BagIndex);
                        break;
                    }

                    break;
                case "result":
                    t.OnResult((int) arg);
                    break;
            }

            RoSkillTargetSelection overlay = RoSkillTargetSelection.Current;
            return "window=" + (t.window != null && t.window.Visible) + " actor=" + t.actorId + " action=" + t.action + " counter=" + t.counter + " divisor=" + t.divisor +
                " stop=" + t.stopFrame + " threshold=" + t.threshold + " targeting=" + (overlay != null && overlay.CapturesTarget) + " sprites=" + (t.act != null);
        }
    }
}
