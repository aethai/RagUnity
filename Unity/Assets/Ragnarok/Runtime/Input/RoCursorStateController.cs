using Ragnarok.Assets;
using Ragnarok.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.Input {
    public sealed class RoCursorStateController : MonoBehaviour {
        private const string CursorSpritePath = RoCursorSpriteVisual.CursorSpritePath;
        private const string CursorActionPath = RoCursorSpriteVisual.CursorActionPath;

        [SerializeField] private RoCursorState currentState = RoCursorState.Default;
        [SerializeField] private bool logStateChanges;
        [SerializeField] private bool createSpriteVisual = true;
        [SerializeField] private RoCursorSpriteVisual spriteVisual;
        [SerializeField] private UnityEngine.Camera renderCamera;
        [SerializeField] private InputActionReference pointerPositionAction;
        [SerializeField] private string lastDiagnostic = "cursor=Default";
        [SerializeField] private bool cursorSpriteAssetsAvailable;

        public RoCursorState CurrentState => currentState;
        public string LastDiagnostic => lastDiagnostic;
        public bool CursorSpriteAssetsAvailable => cursorSpriteAssetsAvailable;
        public RoCursorSpriteVisual SpriteVisual => spriteVisual;
        public bool SpriteVisualReady => spriteVisual != null && spriteVisual.IsReady;

        public void Initialize(
            RoAssetDatabase assets,
            UnityEngine.Camera camera = null,
            InputActionReference pointerPosition = null
        ) {
            renderCamera = camera != null ? camera : renderCamera;
            pointerPositionAction = pointerPosition != null ? pointerPosition : pointerPositionAction;
            cursorSpriteAssetsAvailable = false;
            if (assets == null || !assets.IsInitialized) {
                return;
            }

            bool hasSprite = assets.Exists(CursorSpritePath);
            bool hasAction = assets.Exists(CursorActionPath);
            cursorSpriteAssetsAvailable = hasSprite && hasAction;
            if (!hasSprite) {
                assets.Diagnostics.RecordMissing(CursorSpritePath, "RO cursor sprite", RoAssetPath.GetLookupCandidates(CursorSpritePath));
            }

            if (!hasAction) {
                assets.Diagnostics.RecordMissing(CursorActionPath, "RO cursor action", RoAssetPath.GetLookupCandidates(CursorActionPath));
            }

            if (!createSpriteVisual) {
                DestroySpriteVisual();
                Cursor.visible = true;
                return;
            }

            if (cursorSpriteAssetsAvailable) {
                EnsureSpriteVisual();
                if (spriteVisual.Initialize(assets, renderCamera, pointerPositionAction)) {
                    spriteVisual.SetState(currentState, true);
                }
            }
        }

        public void SetRenderCamera(UnityEngine.Camera camera) {
            renderCamera = camera;
            if (spriteVisual != null) {
                spriteVisual.SetCamera(camera);
            }
        }

        public void SetPointerPositionAction(InputActionReference actionReference) {
            pointerPositionAction = actionReference;
            if (spriteVisual != null) {
                spriteVisual.SetPointerPositionAction(actionReference);
            }
        }

        public void SetPointerTarget(
            RoPointerTarget target,
            RoPointerModifierFlags modifiers,
            string source = null
        ) {
            RoCursorState state = target.CursorState;
            if (target.Kind == RoPointerTargetKind.None) {
                state = RoCursorState.Default;
            }

            // classic UI controls that take a click show the "click" hand (cursor 2)
            if (target.Kind == RoPointerTargetKind.Ui && Ragnarok.UI.Classic.RoClassicUiSystem.Instance != null && Ragnarok.UI.Classic.RoClassicUiSystem.Instance.Windows.HoverClickable) {
                state = RoCursorState.Click;
            }

            if (currentState == state && !ShouldLogStateChanges()) {
                return;
            }

            SetState(state, BuildDiagnostic(target, modifiers, source));
        }

        public void SetState(RoCursorState state, string diagnostic = null) {
            if (string.IsNullOrWhiteSpace(diagnostic)) {
                if (currentState == state) {
                    return;
                }

                diagnostic = "cursor=" + state;
            }

            if (currentState == state && string.Equals(lastDiagnostic, diagnostic, System.StringComparison.Ordinal)) {
                return;
            }

            currentState = state;
            lastDiagnostic = diagnostic;
            if (spriteVisual != null) {
                spriteVisual.SetState(state);
            }

            if (ShouldLogStateChanges()) {
                RoRuntimeDiagnostics.IncrementDebugLogLine();
                Debug.Log("RO cursor state: " + lastDiagnostic, this);
            }
        }

        private bool ShouldLogStateChanges() {
            return logStateChanges && RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics;
        }

        private void EnsureSpriteVisual() {
            if (spriteVisual != null) {
                return;
            }

            var visualObject = new GameObject("RO Cursor Sprite Visual");
            visualObject.transform.SetParent(transform, false);
            spriteVisual = visualObject.AddComponent<RoCursorSpriteVisual>();
        }

        private void DestroySpriteVisual() {
            if (spriteVisual == null) {
                return;
            }

            GameObject target = spriteVisual.gameObject;
            spriteVisual = null;
            if (Application.isPlaying) {
                Destroy(target);
            } else {
                DestroyImmediate(target);
            }
        }

        public static string BuildDiagnostic(
            RoPointerTarget target,
            RoPointerModifierFlags modifiers,
            string source = null
        ) {
            string prefix = string.IsNullOrWhiteSpace(source) ? "pointer" : source;
            return $"{prefix}: target={target.Kind} cursor={target.CursorState} priority={target.Priority} valid={target.IsValid} modifiers={modifiers} label='{target.Label}' cell={target.Cell}";
        }
    }
}
