using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Entities {
    /// <summary>
    /// The client's UIRechargeGage (actor msg 0x52) over a casting actor: the cast timer and its overhead anchor; the
    /// classic gauges (RoClassicActorGauges) draw it. The skill-name shout is the actor's talk balloon (RoClassicChatBalloons).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoActorCastBarOverlay : MonoBehaviour, IRoActorLateTick, IRoActorTickBudget, IRoActorTickVisibility,
        Ragnarok.UI.Classic.IRoClassicAnchorSource {
        public const int BarWidthPixels = 60;
        public const int BarHeightPixels = 6;
        /// <summary>
        /// The cast bar hangs at a fixed 90 sprite pixels above the actor's position along the camera's up axis, whatever
        /// the sprite's size.
        /// </summary>
        public const float OverheadAnchorSpritePixels = 90f;
        /// <summary>The cast bar's top edge sits on the anchor.</summary>
        public const float DefaultScreenYOffsetPixels = 0f;

        private static UnityEngine.Camera cachedMainCamera;
        private static int cachedMainCameraFrame = -1;

        [SerializeField] private UnityEngine.Camera renderCamera;
        [SerializeField] private Vector2 screenOffsetPixels = new Vector2(0f, DefaultScreenYOffsetPixels);

        private Ragnarok.UI.Classic.RoClassicActorGauges.Gauge classicGauge;
        private float castStartTime;
        private float castDelaySeconds;
        private bool hasCast;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOverlayCache() {
            cachedMainCamera = null;
            cachedMainCameraFrame = -1;
        }

        public bool IsShowing => hasCast;

        public Vector2 ScreenOffsetPixels {
            get => screenOffsetPixels;
            set => screenOffsetPixels = value;
        }

        public void Initialize(UnityEngine.Camera camera) {
            renderCamera = camera;
        }

        public void ShowCast(uint delayTimeMs) {
            castDelaySeconds = delayTimeMs > 0 ? delayTimeMs / 1000f : 0.35f;
            castStartTime = Time.time;
            hasCast = true;
            classicGauge ??= Ragnarok.UI.Classic.RoClassicActorGauges.Instance.Create(this);
            classicGauge.SetRecharge(0, Mathf.Max(1, Mathf.RoundToInt(castDelaySeconds * 1000f)));
            if (!enabled) {
                enabled = true;
            }
        }

        public void HideCast() {
            classicGauge?.Hide();
            hasCast = false;
        }

        public void ClearAll() {
            HideCast();
            castDelaySeconds = 0f;
            if (enabled) {
                enabled = false;
            }
        }

        public void ActorLateTick(float now) {
            if (!hasCast) {
                return;
            }

            float elapsed = Time.time - castStartTime;
            if (castDelaySeconds <= 0f || elapsed >= castDelaySeconds) {
                HideCast();
                return;
            }

            int total = Mathf.Max(1, Mathf.RoundToInt(castDelaySeconds * 1000f));
            classicGauge?.SetRecharge(Mathf.Min(total, Mathf.RoundToInt(elapsed * 1000f)), total);
        }

        public bool ShouldRunActorTick(float now, bool lateTick) {
            return !lateTick || hasCast;
        }

        public RoActorTickVisibilityState ResolveActorTickVisibility(bool lateTick) {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || !hasCast) {
                return RoActorTickVisibilityState.Inactive;
            }

            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null) {
                return RoActorTickVisibilityState.Unknown;
            }

            return TryResolveScreenPosition(camera, out _)
                ? RoActorTickVisibilityState.Visible
                : RoActorTickVisibilityState.Offscreen;
        }

        private void OnEnable() {
            RoActorTickService.Register((IRoActorLateTick) this);
        }

        private void OnDisable() {
            RoActorTickService.Unregister((IRoActorLateTick) this);
            classicGauge?.Hide();
        }

        private void OnDestroy() {
            classicGauge?.Release();
            classicGauge = null;
        }

        /// <summary>Top-centre of the cast bar in integer pixels, origin top-left (classic UI anchor).</summary>
        public bool TryGetClassicAnchor(out int x, out int y) {
            x = y = 0;
            UnityEngine.Camera camera = isActiveAndEnabled && hasCast ? ResolveCamera() : null;
            if (camera == null || !TryResolveScreenPosition(camera, out Vector2 screen)) {
                return false;
            }

            x = (int) screen.x;
            y = RoClientDrawFrame.ForCamera(camera).PixelHeightInt - (int) screen.y;
            return true;
        }

        private bool TryResolveScreenPosition(UnityEngine.Camera camera, out Vector2 screenPosition) {
            screenPosition = default;
            if (camera == null) {
                return false;
            }

            RoClientDrawFrame frame = RoClientDrawFrame.ForCamera(camera);
            Vector3 worldAnchor = ResolveOverheadWorldAnchor(frame.CameraUp);
            Vector3 screen = frame.ScreenPoint(worldAnchor);
            if (screen.z <= 0f) {
                return false;
            }

            screenPosition = new Vector2(
                Mathf.Round(screen.x + screenOffsetPixels.x),
                Mathf.Round(screen.y + screenOffsetPixels.y));
            return IsScreenPositionInsideCamera(frame.PixelRect, screenPosition);
        }

        /// <summary>
        /// The overhead anchor: the actor's position plus <see cref="OverheadAnchorSpritePixels"/> sprite pixels along
        /// the camera's up axis (the sprites billboard to the camera), scaled with the actor. Independent of the
        /// sprite's bounds, so it neither dips into the hair nor moves with the camera's yaw or the current frame.
        /// </summary>
        private Vector3 ResolveOverheadWorldAnchor(Vector3 up) {
            float height = OverheadAnchorSpritePixels / SPR.PIXELS_PER_UNIT * Mathf.Abs(transform.lossyScale.y);
            return transform.position + up * height;
        }

        private static bool IsScreenPositionInsideCamera(Rect pixelRect, Vector2 screenPosition) {
            return screenPosition.x >= pixelRect.xMin - BarWidthPixels &&
                screenPosition.x <= pixelRect.xMax + BarWidthPixels &&
                screenPosition.y >= pixelRect.yMin - BarHeightPixels &&
                screenPosition.y <= pixelRect.yMax + BarHeightPixels;
        }

        private UnityEngine.Camera ResolveCamera() {
            if (renderCamera != null) {
                return renderCamera;
            }

            int frame = Time.frameCount;
            if (cachedMainCamera == null || cachedMainCameraFrame != frame) {
                cachedMainCamera = UnityEngine.Camera.main;
                cachedMainCameraFrame = frame;
            }

            renderCamera = cachedMainCamera;
            return renderCamera;
        }

#if UNITY_EDITOR
        /// <summary>Kept for the tests' teardown: nothing UGUI is created any more.</summary>
        public static void DestroyOverlayCanvasForTests() {
            ResetOverlayCache();
        }
#endif
    }
}
