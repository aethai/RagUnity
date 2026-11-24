using System;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The map-name banner on entering a map (hud-efst-banner-spec §2): CMapInfoMgr::ShowBanner 0x6568c0 for a
    /// mapInfo_true.lub record with notifyEnter, its backgroundBmp (else "noname") from
    /// 유저인터페이스\display_mapname\&lt;bmp&gt;.png stretched to a band H·0.2 high at y = H·0.12 and centred; the subtitle
    /// in bold Arial (scale·16) at scale·72 and the main title (scale·44) at scale·95, centred, white, scale = band height
    /// over the image's (0x655b40). Drawn above the windows (0x656fa0), opaque for 5 s, then gone over 255 ms; no fade-in.
    /// </summary>
    public sealed class RoClassicMapBanner : IRoClassicUiProducer {
        private static bool showMapNamesEnabled = true;

        /// <summary>The map name option (Game Settings): off hides the banner and keeps it hidden.</summary>
        public static bool ShowMapNamesEnabled => showMapNamesEnabled;

        public static void SetShowMapNamesEnabled(bool enabled) {
            showMapNamesEnabled = enabled;
            if (!enabled) Hide();
        }

        /// <summary>The banner for a map entry, when the option is on.</summary>
        public static void ShowForMap(RoAssetDatabase assets, string mapName, bool showMapName = true, bool forceRestart = false) {
            if (showMapName && showMapNamesEnabled) Show(assets, mapName, forceRestart);
            else Hide();
        }

        private const string Folder = "유저인터페이스\\display_mapname\\";
        private static RoClassicMapBanner instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private string lastMap = string.Empty;
        private string imageKey;
        private int imageW, imageH;
        private string subTitle = string.Empty, mainTitle = string.Empty;
        private float startTime = -100f;
        private bool showing;
        private int shownAlpha = -1;
        private Vector2Int shownClient;

        /// <summary>Map init (0xc10ac0): shows the banner unless the same map comes again without a restart.</summary>
        public static void Show(RoAssetDatabase assets, string mapName, bool forceRestart) {
            // only a map entered in game: a map loaded behind the login / character screens has no banner
            if (RoClassicPreGame.CoversWorld) return;
            RoClassicMapBanner b = Ensure();
            if (b == null || assets == null || string.IsNullOrWhiteSpace(mapName)) return;
            string map = mapName.Trim();
            if (!forceRestart && string.Equals(map, b.lastMap, StringComparison.OrdinalIgnoreCase)) return;
            b.lastMap = map;
            b.Start(assets, map);
        }

        public static void Hide() {
            if (instance == null) return;
            instance.showing = false;
            instance.layer.Visible = false;
        }

        private static RoClassicMapBanner Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance != null && instance.system == system) return instance;
            instance = new RoClassicMapBanner { system = system };
            instance.layer = system.Renderer.CreateLayer("map banner");
            instance.layer.AboveOverlayUi = true;
            instance.layer.Visible = false;
            system.Register(instance);
            return instance;
        }

        private void Start(RoAssetDatabase assets, string map) {
            showing = false;
            layer.Visible = false;
            RoMapNameMetadataTable table = RoMapNameMetadataTable.Load(assets);
            if (!table.TryGet(map, out RoMapNameMetadataEntry entry) || entry == null || !entry.NotifyEnter) return;
            string bmp = string.IsNullOrWhiteSpace(entry.BackgroundBmp) ? "noname" : entry.BackgroundBmp.Trim();
            if (!LoadImage(bmp)) return;   // no picture, no banner
            subTitle = entry.SubTitle ?? string.Empty;
            mainTitle = entry.MainTitle ?? string.Empty;
            startTime = Time.unscaledTime;
            showing = true;
            shownAlpha = -1;
            layer.Visible = true;
            system.Renderer.BringToFront(layer);
        }

        /// <summary>The 500×190 RGBA PNG, decoded once per name into a top-down dynamic image.</summary>
        private bool LoadImage(string bmp) {
            string key = "@mapbanner:" + bmp.ToLowerInvariant();
            if (system.Images.TryGet(key, out RoClassicUiImage cached)) {
                imageKey = key;
                imageW = cached.Width;
                imageH = cached.Height;
                return true;
            }

            Func<string, byte[]> source = RoClassicUiImages.DefaultSourceProvider?.Invoke();
            byte[] data = source?.Invoke("data\\texture\\" + Folder + bmp + ".png");
            if (data == null) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try {
                if (!texture.LoadImage(data)) return false;
                int w = texture.width, h = texture.height;
                Color32[] bottomUp = texture.GetPixels32();
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++) Array.Copy(bottomUp, (h - 1 - y) * w, pixels, y * w, w);
                if (!system.Images.SetDynamic(key, w, h, pixels)) return false;
                imageKey = key;
                imageW = w;
                imageH = h;
                return true;
            } finally {
                UnityEngine.Object.Destroy(texture);
            }
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (!showing) return;
            if (RoClassicPreGame.CoversWorld) {
                Hide();
                return;
            }

            int t = (int) ((Time.unscaledTime - startTime) * 1000f);
            int alpha = t <= 5000 ? 255 : 5255 - t;
            if (alpha <= 0) {
                Hide();
                return;
            }

            Vector2Int client = ui.ClientSize;
            if (alpha == shownAlpha && client == shownClient) return;
            shownAlpha = alpha;
            shownClient = client;
            Build(ui, client, (byte) alpha);
        }

        /// <summary>0x655b40 (the text surface) and 0x656fa0 (the stretched picture, then the surface), both at H·0.12.</summary>
        private void Build(RoClassicUiSystem ui, Vector2Int client, byte alpha) {
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            int w = client.x, h = client.y;
            int bannerH = (ushort) (uint) (h * 0.2f);
            int top = (int) (h * 0.12f);
            var color = new Color32(255, 255, 255, alpha);
            if (imageH > 0 && system.Images.TryGet(imageKey, out RoClassicUiImage image)) {
                int drawW = (int) (imageW * bannerH / (float) imageH);
                q.FullColor = true;   // the PNG stays A8R8G8B8
                q.Image((int) ((w - drawW) * 0.5f), top, drawW, bannerH, image, color);
                q.FullColor = false;
            }

            float scale = imageH > 0 ? bannerH / (float) imageH : 1f;
            if (subTitle.Length > 0) {
                RoClassicGdiFont font = ui.Font((int) (scale * 16f), true);
                q.Text(font, (int) ((w - font.Measure(subTitle)) * 0.5f), top + (int) (scale * 72f), subTitle, color);
            }

            if (mainTitle.Length > 0) {
                RoClassicGdiFont font = ui.Font((int) (scale * 44f), true);
                q.Text(font, (int) ((w - font.Measure(mainTitle)) * 0.5f), top + (int) (scale * 95f), mainTitle, color);
            }

            layer.MarkDirty();
        }

        /// <summary>!banner show map|state for the sweep.</summary>
        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length >= 2 && t[0] == "show") {
                Show(RoClassicUiHost.GameAssets(), t[1], true);
            }

            RoClassicMapBanner b = instance;
            return b == null ? "no banner" : (b.showing ? "showing " : "hidden ") + b.lastMap + " '" + b.subTitle + "' / '" + b.mainTitle + "' " + b.imageKey + " " + b.imageW + "x" + b.imageH;
        }
    }
}
