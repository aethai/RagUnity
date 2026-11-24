using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.Entities {
    [DisallowMultipleComponent]
    public sealed class RoActorResourceBarOverlay : MonoBehaviour, IRoActorLateTick, IRoActorTickBudget, IRoActorTickVisibility,
        Ragnarok.UI.Classic.IRoClassicAnchorSource {
        public const int WidthPixels = 60;
        public const int HpOnlyHeightPixels = 5;
        public const int HpSpHeightPixels = 9;
        public const int InnerWidthPixels = WidthPixels - 2;
        public const int BarHeightPixels = 3;
        public const int DefaultScreenYOffsetPixels = 4;

        private static readonly Color32 BorderColor = new Color32(0x10, 0x18, 0x9c, 0xff);
        private static readonly Color32 EmptyColor = new Color32(0x42, 0x42, 0x42, 0xff);
        private static readonly Color32 HpColor = new Color32(0x10, 0xef, 0x21, 0xff);
        private static readonly Color32 LowHpColor = new Color32(0xff, 0x00, 0x00, 0xff);
        private static readonly Color32 SpColor = new Color32(0x18, 0x63, 0xde, 0xff);

        private const float RobrowserLifeAnchorOffset = -0.5f;

        private static UnityEngine.Camera cachedMainCamera;
        private static int cachedMainCameraFrame = -1;
        private static int activeOverlayCount;

        [SerializeField] private UnityEngine.Camera renderCamera;
        [SerializeField] private Vector2 screenOffsetPixels = new Vector2(0f, DefaultScreenYOffsetPixels);

        private int hp = -1;
        private int maxHp = -1;
        private int sp = -1;
        private int maxSp = -1;
        private bool hasValues;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOverlayCache() {
            cachedMainCamera = null;
            cachedMainCameraFrame = -1;
            activeOverlayCount = 0;
        }

        private Ragnarok.UI.Classic.RoClassicActorGauges.Gauge classicGauge;

        /// <summary>Draw as the original's UIMonsterGage (else UIPcGage) when the classic UI renders the bars.</summary>
        public bool ClassicMonsterGauge { get; set; }

        public int CurrentHp => hp;
        public int CurrentMaxHp => maxHp;
        public int CurrentSp => sp;
        public int CurrentMaxSp => maxSp;
        public bool IsShowing => classicGauge != null && hasValues && isActiveAndEnabled && ShouldShowHp(hp, maxHp);
        public Vector2 ScreenOffsetPixels {
            get => screenOffsetPixels;
            set => screenOffsetPixels = value;
        }

        public void Initialize(UnityEngine.Camera camera) {
            renderCamera = camera;
        }

        public void SetValues(int currentHp, int currentMaxHp, int currentSp, int currentMaxSp) {
            hp = currentHp;
            maxHp = currentMaxHp;
            sp = currentSp;
            maxSp = currentMaxSp;
            hasValues = true;
            if (classicGauge == null) {
                classicGauge = Ragnarok.UI.Classic.RoClassicActorGauges.Instance.Create(this);
            }

            if (isActiveAndEnabled) {
                classicGauge.Set(ClassicMonsterGauge, hp, maxHp, sp, maxSp);
            }
        }

        public void ClearValues() {
            classicGauge?.Hide();
            hp = -1;
            maxHp = 0;
            sp = -1;
            maxSp = 0;
            hasValues = false;
        }

        public static int CalculateFillWidthPixels(int current, int maximum, int innerWidthPixels = InnerWidthPixels) {
            if (maximum <= 0 || current <= 0 || innerWidthPixels <= 0) {
                return 0;
            }

            float ratio = Mathf.Clamp01(current / (float) maximum);
            return Mathf.Clamp(Mathf.FloorToInt(innerWidthPixels * ratio + 0.5f), 0, innerWidthPixels);
        }

        public static int CalculateHeightPixels(bool hasSp) {
            return hasSp ? HpSpHeightPixels : HpOnlyHeightPixels;
        }

        public static bool ShouldShowHp(int currentHp, int currentMaxHp) {
            return currentHp >= 0 && currentMaxHp > 0;
        }

        public static bool ShouldShowSp(int currentSp, int currentMaxSp) {
            return currentSp >= 0 && currentMaxSp > 0;
        }

        public static Color32 ResolvePlayerHpColor(int currentHp, int currentMaxHp) {
            return currentMaxHp > 0 && currentHp / (float) currentMaxHp < 0.25f ? LowHpColor : HpColor;
        }

        public void ActorLateTick(float now) {
        }

        /// <summary>The classic gauges follow their anchors every frame on the GPU: no late tick.</summary>
        public bool ShouldRunActorTick(float now, bool lateTick) {
            return !lateTick;
        }

        public RoActorTickVisibilityState ResolveActorTickVisibility(bool lateTick) {
            if (!isActiveAndEnabled ||
                !gameObject.activeInHierarchy ||
                !hasValues ||
                !ShouldShowHp(hp, maxHp)) {
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
            activeOverlayCount++;
            RoActorTickService.Register((IRoActorLateTick) this);
            if (!hasValues) {
                return;
            }

            classicGauge?.Set(ClassicMonsterGauge, hp, maxHp, sp, maxSp);
        }

        private void OnDisable() {
            activeOverlayCount = Mathf.Max(0, activeOverlayCount - 1);
            RoActorTickService.Unregister((IRoActorLateTick) this);
            classicGauge?.Hide();
        }

        private void OnDestroy() {
            classicGauge?.Release();
            classicGauge = null;
        }

        /// <summary>Top-centre of the bar in integer pixels, origin top-left (classic UI anchor).</summary>
        public bool TryGetClassicAnchor(out int x, out int y) {
            x = y = 0;
            UnityEngine.Camera camera = isActiveAndEnabled ? ResolveCamera() : null;
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

            // the camera terms once a frame for every gauge (RoClientDrawFrame), not ~6 native camera calls per gauge
            RoClientDrawFrame frame = RoClientDrawFrame.ForCamera(camera);
            Vector3 worldAnchor = transform.position + frame.CameraUp * RobrowserLifeAnchorOffset;
            Vector3 screen = frame.ScreenPoint(worldAnchor);
            if (screen.z <= 0f) {
                return false;
            }

            screenPosition = new Vector2(
                Mathf.Round(screen.x + screenOffsetPixels.x),
                Mathf.Round(screen.y + screenOffsetPixels.y));
            return IsScreenPositionInsideCamera(frame.PixelRect, screenPosition);
        }

        private static bool IsScreenPositionInsideCamera(Rect pixelRect, Vector2 screenPosition) {
            float horizontalMargin = WidthPixels;
            float verticalMargin = HpSpHeightPixels;
            return screenPosition.x >= pixelRect.xMin - horizontalMargin &&
                screenPosition.x <= pixelRect.xMax + horizontalMargin &&
                screenPosition.y >= pixelRect.yMin - verticalMargin &&
                screenPosition.y <= pixelRect.yMax + verticalMargin;
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
