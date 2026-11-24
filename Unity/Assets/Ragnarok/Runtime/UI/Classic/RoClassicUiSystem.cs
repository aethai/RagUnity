using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// Owns the classic (1:1 original client) UI for the gameplay camera: the atlas, the GDI fonts and the renderer.
    /// Producers (overhead gauges, name balloons, windows...) register once and rewrite their layers in
    /// <see cref="IRoClassicUiProducer.ClassicUiTick"/>, which runs after every camera update of the frame; the system
    /// then uploads everything in one go. It is the game's only UI.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed partial class RoClassicUiSystem : MonoBehaviour {
        /// <summary>Always on: the classic UI is the only UI (kept while the last legacy branches are removed).</summary>
        public const bool Enabled = true;

        public static RoClassicUiSystem Instance { get; private set; }

        // statics survive play-mode changes when domain reload is off: never leak the switch into edit mode/tests
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            Instance = null;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookEditMode() {
            UnityEditor.EditorApplication.playModeStateChanged += state => {
                if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) ResetStatics();
            };
        }
#endif

        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("RO.ClassicUi.Tick");
        private static readonly ProfilerMarker WindowsMarker = new ProfilerMarker("RO.ClassicUi.Windows");
        private static readonly ProfilerMarker MessageBoxMarker = new ProfilerMarker("RO.ClassicUi.MessageBox");
        private static readonly ProfilerMarker TextInputMarker = new ProfilerMarker("RO.ClassicUi.TextInput");
        private static readonly ProfilerMarker RefreshMarker = new ProfilerMarker("RO.ClassicUi.Refresh");
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("RO.ClassicUi.Upload");

        private readonly List<IRoClassicUiProducer> producers = new List<IRoClassicUiProducer>();
        private readonly List<ProfilerMarker> producerMarkers = new List<ProfilerMarker>();
        private readonly Dictionary<RoClassicFontSpec, RoClassicGdiFont> fonts = new Dictionary<RoClassicFontSpec, RoClassicGdiFont>();

        public RoClassicUiAtlas Atlas { get; private set; }
        public RoClassicUiRenderer Renderer { get; private set; }

        /// <summary>UI bitmaps by client path, decoded from the GRFs into the atlas.</summary>
        public RoClassicUiImages Images { get; private set; }

        /// <summary>Classic windows: z-order, hit testing, buttons, dragging.</summary>
        public RoClassicWindowManager Windows { get; private set; }

        /// <summary>A solid white texel region for fills (gauges, boxes), sampled away from its edges.</summary>
        public RoClassicUiImage White { get; private set; }

        public UnityEngine.Camera Camera => Renderer != null ? Renderer.TargetCamera : null;

        /// <summary>The general UI scale (settings): client pixels to screen pixels.</summary>
        public float Scale => Renderer != null ? Renderer.Scale : 1f;

        /// <summary>The screen in client pixels, the space window layouts and positions live in.</summary>
        public Vector2Int ClientSize {
            get {
                UnityEngine.Camera camera = Camera;
                int w = camera != null ? camera.pixelWidth : Screen.width, h = camera != null ? camera.pixelHeight : Screen.height;
                return new Vector2Int((int) (w / Scale), (int) (h / Scale));
            }
        }

        /// <summary>A Unity screen point (bottom-left origin) in client pixels (top-left origin).</summary>
        public Vector2Int ScreenToClient(Vector2 screenPoint) {
            UnityEngine.Camera camera = Camera;
            int h = camera != null ? camera.pixelHeight : Screen.height;
            float s = Scale;
            return new Vector2Int(Mathf.FloorToInt(screenPoint.x / s), Mathf.FloorToInt((h - 1 - screenPoint.y) / s));
        }

        /// <summary>The general UI scale from the host (the game's settings), installed at startup; 1 when unset (tools).</summary>
        public static Func<float> UiScaleProvider { get; set; }

        /// <summary>
        /// Lua GetBeHaviorOfWindow(window id): the key bound to that window's toggle as the client names it ("Alt + Q"), or
        /// empty. The close buttons' balloons and the menu icons' tooltips show it.
        /// </summary>
        public static Func<int, string> WindowHotkeyText { get; set; }

        /// <summary>Returns the running system, creating it for <paramref name="camera"/> on first use.</summary>
        public static RoClassicUiSystem Ensure(UnityEngine.Camera camera) {
            if (Instance == null) {
                var go = new GameObject("RO classic UI");
                // edit-mode tests build windows too (the classic UI is the only UI); only Play keeps it across scenes
                if (Application.isPlaying) DontDestroyOnLoad(go);
                Instance = go.AddComponent<RoClassicUiSystem>();
                Instance.Initialize(camera);
            } else if (camera != null && Instance.Renderer.TargetCamera != camera) {
                Instance.Renderer.TargetCamera = camera;
            }

            return Instance;
        }

        private void Initialize(UnityEngine.Camera camera) {
            Atlas = new RoClassicUiAtlas();
            Images = new RoClassicUiImages(Atlas);
            Windows = new RoClassicWindowManager(this);
            var white = new Color32[16];
            for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
            Atlas.TryAdd(4, 4, white, out RoClassicUiImage image);
            float texel = 1f / RoClassicUiAtlas.PageSize;
            White = new RoClassicUiImage(
                new Vector4(image.Uv.x + texel, image.Uv.y - texel, image.Uv.z - texel, image.Uv.w + texel), image.Page, 4, 4);

            Shader shader = RoClassicUiRenderer.DefaultShader != null ? RoClassicUiRenderer.DefaultShader : Shader.Find("Hidden/RO/ClassicUi");
            Renderer = gameObject.AddComponent<RoClassicUiRenderer>();
            Renderer.Initialize(shader, Atlas, camera);
            Renderer.SetScale(1f);
        }

        /// <summary>
        /// The client's UI font: <c>CreateFontA(height + 3, 0, 0, 0, bold ? 700 : 400, ..., ANSI_CHARSET, ...,
        /// NONANTIALIASED_QUALITY, "Arial")</c> — most UI asks for height 12, i.e. a 15 px cell.
        /// </summary>
        public RoClassicGdiFont Font(int requestedHeight = 12, bool bold = false) =>
            Font(new RoClassicFontSpec("Arial", requestedHeight + 3, bold ? 700 : 400, 0, (int) RoClassicGdiFont.NonAntialiasedQuality));

        /// <summary>A font exactly as the client creates it (face, lfHeight, weight, charset, quality).</summary>
        public RoClassicGdiFont Font(RoClassicFontSpec spec) {
            if (!fonts.TryGetValue(spec, out RoClassicGdiFont font)) {
                font = new RoClassicGdiFont(Atlas, spec.Face, spec.LfHeight, spec.Weight, (uint) spec.Quality, (uint) spec.Charset);
                fonts.Add(spec, font);
            }

            return font;
        }

        public void Register(IRoClassicUiProducer producer) {
            if (producers.Contains(producer)) return;
            producers.Add(producer);
            producerMarkers.Add(new ProfilerMarker("RO.ClassicUi." + producer.GetType().Name));
        }

        public void Unregister(IRoClassicUiProducer producer) {
            int index = producers.IndexOf(producer);
            if (index < 0) return;
            producers.RemoveAt(index);
            producerMarkers.RemoveAt(index);
        }

        private void LateUpdate() {
            if (Renderer == null || !Renderer.IsReady) {
                return;
            }

            UnityEngine.Camera main = UnityEngine.Camera.main;
            if (main != null && Renderer.TargetCamera != main) Renderer.TargetCamera = main;
            using ProfilerMarker.AutoScope tick = TickMarker.Auto();
            float uiScale = UiScaleProvider != null ? UiScaleProvider() : 1f;
            Renderer.SetScale(uiScale);
            using (WindowsMarker.Auto()) Windows.Process();
            using (MessageBoxMarker.Auto()) RoClassicMessageBox.Process();
            using (TextInputMarker.Auto()) RoClassicTextInput.Process();
            using (RefreshMarker.Auto()) Windows.RefreshAll();
            for (int i = producers.Count - 1; i >= 0; i--) {
                if (i >= producers.Count) continue;   // a tick unregistered more than itself
                using (producerMarkers[i].Auto()) producers[i].ClassicUiTick(this);
            }

            using (UploadMarker.Auto()) Renderer.Upload();
        }

        private void OnDestroy() {
            foreach (RoClassicGdiFont font in fonts.Values) font.Dispose();
            fonts.Clear();
            Atlas?.Dispose();
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>Something that draws into classic UI layers; ticked once per frame after the camera settled.</summary>
    public interface IRoClassicUiProducer {
        void ClassicUiTick(RoClassicUiSystem system);
    }
}
