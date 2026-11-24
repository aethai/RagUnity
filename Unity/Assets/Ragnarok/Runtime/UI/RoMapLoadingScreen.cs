using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UILoadingWnd 0x7 (codex-ui-research RUNTIME-RULES-PRE-GAME.md "Wait 0x6 and loading 0x7"): the loading JPEG
    /// (유저인터페이스\loadingNN.jpg, one of those in the GRFs, else loading00) over the whole screen under a 240 × 15
    /// UIBarGraph2 at (W / 2 − 100, H / 2 + 160): the #00FFFF frame, the #8E8E8E inset (1, 1, W − 2, H − 2), the fill
    /// (1, 1, trunc((W − 2) · percent / 100), H − 2) in colorchip.bmp (22, 2) and "%3ld%%" at (W / 2 − 20, H / 2 − 7) in yellow,
    /// font type 1 height 16 (0x7e8670). One classic layer above every window, redrawn only when the percentage moves;
    /// while it shows, the pointer does not reach the world (a global pointer blocker). The shown percentage eases
    /// towards the loading's progress.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoMapLoadingScreen : MonoBehaviour {
        private const float ProgressSmoothSpeed = 90f;
        private const float ProgressCatchupSpeed = 8f;
        private const int BarW = 240, BarH = 15;
        private const string ImageKey = "@map-loading";
        private static readonly Color32 Frame = new Color32(0x00, 0xff, 0xff, 255);
        private static readonly Color32 Inset = new Color32(0x8e, 0x8e, 0x8e, 255);
        private static readonly Color32 Fill = new Color32(0x42, 0x62, 0xa5, 255);
        private static readonly Color32 TextColor = new Color32(0xff, 0xff, 0x00, 255);
        private static readonly string[] PercentTexts = BuildPercentTexts();

        private readonly List<string> loadingCandidates = new List<string>(16);
        private float targetPercent;
        private float displayedPercent;
        private float shownAtUnscaledTime = -1f;
        private bool visible;
        private bool hasLoadingTexture;
        private RoClassicUiLayer layer;
        private RoClassicUiSystem layerSystem;
        private bool imageReady;
        private int drawnPercent = -1;
        private Vector2Int drawnFor;

        public bool IsVisible => visible;
        public bool HasLoadingTexture => hasLoadingTexture;
        public float TargetPercent => targetPercent;
        public float DisplayedPercent => displayedPercent;
        public float VisibleElapsedSeconds =>
            visible && shownAtUnscaledTime >= 0f
                ? Mathf.Max(0f, Time.unscaledTime - shownAtUnscaledTime)
                : 0f;

        public static RoMapLoadingScreen EnsureInScene() {
            RoMapLoadingScreen screen = FindFirstObjectByType<RoMapLoadingScreen>(FindObjectsInactive.Include);
            if (screen == null) {
                screen = new GameObject("RO Map Loading Screen").AddComponent<RoMapLoadingScreen>();
            }

            return screen;
        }

        private static string[] BuildPercentTexts() {
            // "%3ld%%" for 0..100, made once
            var texts = new string[101];
            for (int i = 0; i <= 100; i++) texts[i] = i.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "%";
            return texts;
        }

        /// <summary>Shows the screen (a new picture per loading, chosen once the assets exist; called again until it has one).</summary>
        public void Show(RoAssetDatabase assets) {
            if (!visible) {
                targetPercent = 0f;
                displayedPercent = 0f;
                shownAtUnscaledTime = Time.unscaledTime;
                hasLoadingTexture = false;
                imageReady = false;
            }

            visible = true;
            RoUiPointerCapture.SetGlobalBlocker(this, true);
            RoClassicUiSystem system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
            if (system == null) return;
            if (layer == null || layerSystem != system) {
                layerSystem = system;
                layer = system.Renderer.CreateLayer("map loading");
                layer.AboveOverlayUi = true;
            }

            if (!imageReady) {
                imageReady = LoadImage(system, assets);
                hasLoadingTexture = imageReady;
            }

            system.Renderer.BringToFront(layer);
            layer.Visible = true;
            drawnPercent = -1;
            Redraw();
        }

        public void SetProgress(float percent) {
            float clamped = Mathf.Clamp(percent, 0f, 100f);
            if (clamped < targetPercent) {
                return;
            }

            targetPercent = clamped;
            if (targetPercent >= 100f) {
                displayedPercent = 100f;
            }

            Redraw();
        }

        public void SetDisplayedProgress(float percent) {
            float clamped = Mathf.Clamp(percent, 0f, 100f);
            if (clamped < displayedPercent) {
                return;
            }

            targetPercent = Mathf.Max(targetPercent, clamped);
            displayedPercent = clamped;
            Redraw();
        }

        public void Hide() {
            if (!visible) {
                return;
            }

            targetPercent = 100f;
            displayedPercent = 100f;
            visible = false;
            shownAtUnscaledTime = -1f;
            RoUiPointerCapture.SetGlobalBlocker(this, false);
            if (layer != null) layer.Visible = false;
        }

        private void OnDestroy() {
            RoUiPointerCapture.SetGlobalBlocker(this, false);
        }

        private void Update() {
            if (!visible || displayedPercent >= targetPercent) {
                return;
            }

            float catchup = Mathf.Max(ProgressSmoothSpeed, (targetPercent - displayedPercent) * ProgressCatchupSpeed);
            displayedPercent = Mathf.Min(targetPercent, displayedPercent + catchup * Time.unscaledDeltaTime);
            Redraw();
        }

        /// <summary>Windows shown while the map loads (the HUD on entry) would come above it: the screen stays the top layer.</summary>
        private void LateUpdate() {
            if (visible && layer != null && layerSystem != null) layerSystem.Renderer.BringToFront(layer);
        }

        /// <summary>Redraws the layer when the shown percentage or the screen changed.</summary>
        private void Redraw() {
            if (!visible || layer == null) return;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            int percent = Mathf.Clamp((int) displayedPercent, 0, 100);
            Vector2Int client = system.ClientSize;
            if (percent == drawnPercent && client == drawnFor) return;
            drawnPercent = percent;
            drawnFor = client;

            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            if (imageReady && system.Images.TryGet(ImageKey, out RoClassicUiImage image)) {
                q.Image(0, 0, client.x, client.y, image, new Color32(255, 255, 255, 255));
            } else {
                q.Image(0, 0, client.x, client.y, system.White, new Color32(0, 0, 0, 255));
            }

            int x = client.x / 2 - 100, y = client.y / 2 + 160;
            q.Image(x, y, BarW, BarH, system.White, Frame);
            q.Image(x + 1, y + 1, BarW - 2, BarH - 2, system.White, Inset);
            int fill = (int) ((BarW - 2) * displayedPercent / 100f);
            if (fill > 0) q.Image(x + 1, y + 1, fill, BarH - 2, system.White, Fill);
            RoClassicGdiFont font = system.Font(new RoClassicFontSpec("Arial", 16, 400, 0, (int) RoClassicGdiFont.NonAntialiasedQuality));
            q.Text(font, x + BarW / 2 - 20, y + BarH / 2 - 7, PercentTexts[percent], TextColor);
            layer.MarkDirty();
        }

        /// <summary>One of the loadingNN.jpg the GRFs have (else loading00.jpg), decoded once per loading into a dynamic image.</summary>
        private bool LoadImage(RoClassicUiSystem system, RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) return false;
            loadingCandidates.Clear();
            for (int i = 0; i <= 10; i++) {
                string path = RoInterfacePath.Build("loading" + i.ToString("00", CultureInfo.InvariantCulture) + ".jpg");
                if (assets.Exists(path)) loadingCandidates.Add(path);
            }

            if (loadingCandidates.Count == 0) return false;
            byte[] data = assets.ReadBytes(loadingCandidates[UnityEngine.Random.Range(0, loadingCandidates.Count)]);
            if (data == null) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try {
                if (!texture.LoadImage(data)) return false;
                int w = texture.width, h = texture.height;
                Color32[] bottomUp = texture.GetPixels32();
                var pixels = new Color32[w * h];
                for (int row = 0; row < h; row++) Array.Copy(bottomUp, (h - 1 - row) * w, pixels, row * w, w);
                return system.Images.SetDynamic(ImageKey, w, h, pixels);
            } finally {
                Destroy(texture);
            }
        }
    }
}
