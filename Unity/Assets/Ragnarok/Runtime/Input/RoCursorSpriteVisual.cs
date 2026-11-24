using System;
using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Diagnostics;
using Ragnarok.Sprites;
using Ragnarok.UI;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.Input {
    /// <summary>
    /// The game cursor as the client draws it (DrawCursor 0xa02c70, after the UI): every layer of cursors.act [id][frame]
    /// placed like any screen ACT (0xa031c0) at the mouse point itself, vertex colour 0xFAFFFFFF. It is a classic UI layer
    /// on top of everything, moved by a GPU anchor, so a mouse move uploads nothing and a frame change a few quads.
    /// </summary>
    public sealed class RoCursorSpriteVisual : MonoBehaviour {
        public const string CursorBasePath = "data/sprite/cursors";
        public const string CursorSpritePath = "data/sprite/cursors.spr";
        public const string CursorActionPath = "data/sprite/cursors.act";
        public const int DefaultAction = 0;
        public const int TalkAction = 1;
        public const int ClickAction = 2;
        public const int LockAction = 3;
        public const int RotateAction = 4;
        public const int AttackAction = 5;
        public const int WarpAction = 7;
        public const int InvalidAction = 8;
        public const int PickupAction = 9;
        public const int TargetAction = 10;

        private static readonly Color32 CursorColor = new Color32(255, 255, 255, 0xfa);
        private static readonly List<RoCursorSpriteVisual> activeVisuals = new List<RoCursorSpriteVisual>();

        [SerializeField] private UnityEngine.Camera renderCamera;
        [SerializeField] private InputActionReference pointerPositionAction;
        [SerializeField] private bool hideSystemCursor = true;
        [SerializeField] private RoCursorState state = RoCursorState.Default;
        [SerializeField] private int actionIndex;
        [SerializeField] private int frameIndex;
        [SerializeField] private bool isReady;

        private RoAssetDatabase assets;
        private RoClassicActSprite sprite;
        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private int anchor;
        private int drawnAction = -1;
        private int drawnFrame = -1;
        private float actionStartTime;
        private bool ownsPointerPositionActionEnable;
        private bool hasAppliedSystemCursorVisibility;
        private bool lastSystemCursorVisible = true;

        public bool IsReady => isReady;
        public RoCursorState State => state;
        public int ActionIndex => actionIndex;
        public int FrameIndex => frameIndex;
        public static int ActiveVisualCount => activeVisuals.Count;

        public static RoCursorSpriteVisual GetActiveVisual(int index) {
            return index >= 0 && index < activeVisuals.Count ? activeVisuals[index] : null;
        }

        /// <summary>Takes the game's assets; the sprite itself is built into the classic atlas once the classic UI runs.</summary>
        public bool Initialize(
            RoAssetDatabase gameAssets,
            UnityEngine.Camera camera = null,
            InputActionReference pointerPosition = null
        ) {
            renderCamera = camera != null ? camera : renderCamera;
            SetPointerPositionAction(pointerPosition != null ? pointerPosition : pointerPositionAction);
            assets = gameAssets != null && gameAssets.IsInitialized ? gameAssets : null;
            sprite = null;
            isReady = assets != null;
            drawnAction = drawnFrame = -1;
            return isReady;
        }

        public void SetCamera(UnityEngine.Camera camera) {
            renderCamera = camera;
        }

        public void SetPointerPositionAction(InputActionReference actionReference) {
            DisablePointerPositionAction();
            pointerPositionAction = actionReference;
            EnablePointerPositionAction();
        }

        public void SetState(RoCursorState newState, bool force = false) {
            int nextAction = GetCursorActionIndex(newState);
            if (!force && state == newState && actionIndex == nextAction) {
                return;
            }

            // SetCursor 0xa04cc0: a new id restarts at frame 0 with the timer
            state = newState;
            actionIndex = nextAction;
            frameIndex = 0;
            actionStartTime = Application.isPlaying ? Time.realtimeSinceStartup : 0f;
        }

        private void OnEnable() {
            useGUILayout = false;
            if (!activeVisuals.Contains(this)) {
                activeVisuals.Add(this);
            }

            EnablePointerPositionAction();
        }

        private void OnDisable() {
            activeVisuals.Remove(this);
            DisablePointerPositionAction();
            if (layer != null) layer.Visible = false;
            if (hideSystemCursor && Application.isPlaying) {
                SetSystemCursorVisible(true);
            }
        }

        private void OnDestroy() {
            activeVisuals.Remove(this);
            ReleaseLayer();
            if (hideSystemCursor && Application.isPlaying) {
                SetSystemCursorVisible(true);
            }
        }

        private void LateUpdate() {
            long cursorLateUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                if (!isReady || !Application.isPlaying) {
                    return;
                }

                bool cursorVisible = EnsureLayer() && ApplyPointerPosition();
                if (hideSystemCursor) {
                    SetSystemCursorVisible(!cursorVisible);
                }
            } finally {
                RoRuntimePerfCounters.AddCursorVisualLateUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(cursorLateUpdateStartTicks));
            }
        }

        private void OnGUI() {
            long onGuiStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            Event current = Event.current;
            RoRuntimePerfCounters.IncrementOnGuiEvent(current != null ? (int)current.type : -1);
            try {
                if (IsPointerPositionActionActive()) {
                    return;
                }

                RoPointerScreenPosition.ReportGuiMousePosition(current);
            } finally {
                long elapsedTicks = RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(onGuiStartTicks);
                RoRuntimePerfCounters.AddOnGuiTicks(elapsedTicks);
                RoRuntimePerfCounters.AddCursorVisualOnGuiTicks(elapsedTicks);
            }
        }

        /// <summary>The cursor layer on top of the classic UI and the cursor sprite in its atlas; false until both exist.</summary>
        private bool EnsureLayer() {
            RoClassicUiSystem current = RoClassicUiSystem.Instance;
            if (current == null) {
                current = RoClassicUiSystem.Ensure(renderCamera != null ? renderCamera : UnityEngine.Camera.main);
                if (current == null) return false;
            }

            if (layer == null || system != current) {
                system = current;
                layer = current.Renderer.CreateLayer("cursor");
                layer.AboveOverlayUi = true;
                anchor = current.Renderer.AllocateAnchor();
                drawnAction = drawnFrame = -1;
            }

            if (sprite == null) {
                sprite = RoClassicActSprite.Get(CursorBasePath, assets);
                if (sprite == null) return false;
            }

            if (!sprite.Ready) return false;
            // drawn after the UI: above every window, box and fade that came to the front since
            current.Renderer.BringToFront(layer);
            layer.Visible = true;
            return true;
        }

        private void ReleaseLayer() {
            if (layer != null && system != null) {
                system.Renderer.RemoveLayer(layer);
                system.Renderer.FreeAnchor(anchor);
            }

            layer = null;
            system = null;
            anchor = 0;
        }

        private bool ApplyPointerPosition() {
            long applyPointerStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                UpdateFrame();
                RoClassicUiRenderer renderer = system.Renderer;
                if (!TryReadPointerPosition(out Vector2 screenPosition)) {
                    renderer.SetAnchor(anchor, 0, 0, false);
                    return false;
                }

                Vector2Int point = ClassicCursorPoint(screenPosition, PixelHeight());
                renderer.SetAnchor(anchor, point.x, point.y, true);
                return true;
            } finally {
                RoRuntimePerfCounters.AddCursorVisualApplyPointerTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(applyPointerStartTicks));
            }
        }

        /// <summary>A Unity screen point (bottom-left origin) as the top-left pixel the client draws the cursor at.</summary>
        public static Vector2Int ClassicCursorPoint(Vector2 screenPosition, int pixelHeight) {
            return new Vector2Int(Mathf.FloorToInt(screenPosition.x), pixelHeight - 1 - Mathf.FloorToInt(screenPosition.y));
        }

        private int PixelHeight() {
            UnityEngine.Camera camera = system.Camera != null ? system.Camera : renderCamera;
            return camera != null && camera.pixelHeight > 0 ? camera.pixelHeight : Screen.height;
        }

        /// <summary>The frame of the current id; the layer re-emits only when (id, frame) changes.</summary>
        private void UpdateFrame() {
            int frameCount = sprite.FrameCount(actionIndex);
            float now = Time.realtimeSinceStartup;
            // the "click" cursors (2, 9) animate only while the left button is held; released, frame 0 and the timer restarts
            bool leftDown = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if ((actionIndex == ClickAction || actionIndex == PickupAction) && !leftDown) actionStartTime = now;
            frameIndex = ClientCursorFrame(actionIndex, (int) ((now - actionStartTime) * 1000f), frameCount, leftDown);
            if (frameIndex == drawnFrame && actionIndex == drawnAction) {
                return;
            }

            drawnAction = actionIndex;
            drawnFrame = frameIndex;
            RoClassicUiQuads quads = layer.Quads;
            quads.Clear();
            quads.Anchor = anchor;
            // the anchor is the mouse point; layers sit at their ACT offsets from it, scaled with the UI like every window
            sprite.Emit(quads, actionIndex, frameIndex, 0f, 0f, 1f, CursorColor);
            quads.Anchor = 0;
            layer.MarkDirty();
        }

        /// <summary>
        /// Shows or hides the OS cursor. Hiding also sets a transparent hardware cursor: in a player Windows can bring the
        /// arrow back over the window (focus changes, the window border) while Cursor.visible stays false.
        /// </summary>
        private void SetSystemCursorVisible(bool visible) {
            if (hasAppliedSystemCursorVisibility && lastSystemCursorVisible == visible) {
                return;
            }

            UnityEngine.Cursor.visible = visible;
            UnityEngine.Cursor.SetCursor(visible ? null : TransparentCursor(), Vector2.zero, CursorMode.Auto);
            lastSystemCursorVisible = visible;
            hasAppliedSystemCursorVisibility = true;
        }

        private static Texture2D transparentCursor;

        private static Texture2D TransparentCursor() {
            if (transparentCursor != null) return transparentCursor;
            transparentCursor = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "RO transparent cursor", hideFlags = HideFlags.HideAndDontSave };
            transparentCursor.SetPixels32(new Color32[32 * 32]);
            transparentCursor.Apply(false, false);
            return transparentCursor;
        }

        private void OnApplicationFocus(bool focused) {
            // the OS may have restored its arrow while the window was in the background: apply the state again
            if (focused) hasAppliedSystemCursorVisibility = false;
        }

        private bool TryReadPointerPosition(out Vector2 position) {
            return RoPointerScreenPosition.TryRead(pointerPositionAction, out position);
        }

        private bool IsPointerPositionActionActive() {
            InputAction action = pointerPositionAction != null ? pointerPositionAction.action : null;
            return action != null && action.enabled;
        }

        private void EnablePointerPositionAction() {
            InputAction action = pointerPositionAction != null ? pointerPositionAction.action : null;
            if (action != null && !action.enabled) {
                action.Enable();
                ownsPointerPositionActionEnable = true;
            }
        }

        private void DisablePointerPositionAction() {
            InputAction action = pointerPositionAction != null ? pointerPositionAction.action : null;
            if (ownsPointerPositionActionEnable && action != null && action.enabled) {
                action.Disable();
            }

            ownsPointerPositionActionEnable = false;
        }

        /// <summary>
        /// DrawCursor 0xa02c70 (reference ui-static RUNTIME-RULES-STORE-EXTRA §5): t = elapsed ms / 24 and a fixed divisor
        /// per cursor id — the ACT delays are ignored. 1 plays once (4 ticks a frame) and holds; 2 and 9 do the same (4 / 1
        /// ticks) only while the left button is down, else frame 0; the others loop (5: 20 ticks, 7: 10, 10: 6, 11: 3,
        /// 13: 2, the rest 4).
        /// </summary>
        public static int ClientCursorFrame(int cursorId, int elapsedMilliseconds, int frameCount, bool leftButtonDown) {
            if (frameCount <= 1) {
                return 0;
            }

            int t = Math.Max(0, elapsedMilliseconds) / 24;
            switch (cursorId) {
                case TalkAction:
                    return Math.Min(t / 4, frameCount - 1);
                case ClickAction:
                    return leftButtonDown ? Math.Min(t / 4, frameCount - 1) : 0;
                case PickupAction:
                    return leftButtonDown ? Math.Min(t, frameCount - 1) : 0;
                default:
                    return t / ClientCursorTicksPerFrame(cursorId) % frameCount;
            }
        }

        public static int ClientCursorTicksPerFrame(int cursorId) {
            return cursorId switch {
                AttackAction => 20,
                WarpAction => 10,
                TargetAction => 6,
                11 => 3,
                13 => 2,
                _ => 4
            };
        }

        public static int GetCursorActionIndex(RoCursorState cursorState, int actionCount = int.MaxValue) {
            int action = cursorState switch {
                RoCursorState.Talk => TalkAction,
                RoCursorState.Click => ClickAction,
                RoCursorState.Lock => LockAction,
                RoCursorState.Rotate => RotateAction,
                RoCursorState.Attack => AttackAction,
                RoCursorState.Warp => WarpAction,
                RoCursorState.Invalid => InvalidAction,
                RoCursorState.Pickup => PickupAction,
                RoCursorState.Target => TargetAction,
                _ => DefaultAction
            };

            if (actionCount <= 0) {
                return 0;
            }

            return Mathf.Clamp(action, 0, actionCount - 1);
        }
    }
}
