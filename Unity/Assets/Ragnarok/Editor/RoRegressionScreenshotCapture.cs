using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ragnarok.Assets;
using Ragnarok.Content;
using Ragnarok.Maps;
using ROIO.Loaders;
using ROIO.Models.FileTypes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Editor {
    public static class RoRegressionScreenshotCapture {
        private const string OutputDirectoryEnv = "RO_SCREENSHOT_OUTPUT_DIR";
        private const string ScreenshotNameEnv = "RO_SCREENSHOT_NAME";
        private const string MapsEnv = "RO_SCREENSHOT_MAPS";
        private const string ModesEnv = "RO_SCREENSHOT_MODES";
        private const string ShotsEnv = "RO_SCREENSHOT_SHOTS";
        private const string CachePassesEnv = "RO_SCREENSHOT_CACHE_PASSES";
        private const string WidthEnv = "RO_SCREENSHOT_WIDTH";
        private const string HeightEnv = "RO_SCREENSHOT_HEIGHT";
        private const string ModelLimitEnv = "RO_SCREENSHOT_MAX_MODEL_INSTANCES";
        private const string DefaultOutputDirectory = "Temp/RegressionScreenshots";

        private static readonly string[] DefaultMaps = {
            "prontera.rsw",
            "geffen.rsw",
            "payon.rsw",
            "izlude.rsw",
            "prt_in.rsw",
            "pay_dun00.rsw"
        };

        private static readonly CaptureMode[] DefaultModes = {
            CaptureMode.Normal,
            CaptureMode.GroundOnly
        };

        private static readonly CaptureShot[] DefaultShots = {
            CaptureShot.Overview,
            CaptureShot.CenterClose
        };

        private enum CaptureMode {
            Normal,
            GroundOnly,
            TintGroundAndModels
        }

        private enum CaptureShot {
            Overview,
            CenterClose,
            NorthClose,
            SouthClose,
            EastClose,
            WestClose
        }

        [MenuItem("Ragnarok/Regression/Capture Game View Screenshot")]
        public static void CaptureCurrentGameViewFromMenu() {
            CaptureCurrentGameView();
        }

        [MenuItem("Ragnarok/Regression/Capture Baseline Map Screenshots")]
        public static void CaptureBaselineMapScreenshotsFromMenu() {
            CaptureBaselineMapScreenshots();
        }

        public static string CaptureCurrentGameView() {
            string outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryEnv);
            if (string.IsNullOrWhiteSpace(outputDirectory)) {
                outputDirectory = DefaultOutputDirectory;
            }

            string screenshotName = Environment.GetEnvironmentVariable(ScreenshotNameEnv);
            if (string.IsNullOrWhiteSpace(screenshotName)) {
                screenshotName = "gameview-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".png";
            }

            if (!screenshotName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) {
                screenshotName += ".png";
            }

            Directory.CreateDirectory(outputDirectory);
            string screenshotPath = Path.GetFullPath(Path.Combine(outputDirectory, screenshotName));
            ScreenCapture.CaptureScreenshot(screenshotPath);
            Debug.Log($"Queued RO regression screenshot capture: {screenshotPath}");
            return screenshotPath;
        }

        public static void CaptureBaselineMapScreenshots() {
            CaptureSettings settings = CaptureSettings.FromEnvironment();
            Directory.CreateDirectory(settings.OutputDirectory);

            using RoAssetDatabase assets = RoRegressionContentSource.CreateAssetDatabaseOrThrow();
            var loader = new RoMapDataLoader(assets);
            int total = settings.Maps.Length * settings.Modes.Length * settings.Shots.Length * settings.CachePasses.Length;
            int completed = 0;
            try {
                foreach (string mapName in settings.Maps) {
                    Debug.Log($"Loading RO regression screenshot map: {mapName}");
                    AsyncMapLoader.GameMapData mapData = LoadMapDataWithoutUnityContext(loader, mapName);
                    foreach (CaptureMode mode in settings.Modes) {
                        foreach (CaptureShot shot in settings.Shots) {
                            foreach (string cachePass in settings.CachePasses) {
                                if (string.Equals(cachePass, "cold", StringComparison.OrdinalIgnoreCase)) {
                                    assets.ClearTextureCache();
                                    RoModelRenderer.ClearCaches();
                                }

                                string title = $"RO regression screenshots ({completed + 1}/{total})";
                                string info = $"{mapName} {shot} {mode} {cachePass}";
                                EditorUtility.DisplayProgressBar(title, info, total > 0 ? completed / (float) total : 0f);

                                string screenshotPath = CaptureMap(mapData, assets, settings, mode, shot, cachePass);
                                completed++;
                                Debug.Log($"Captured RO regression screenshot: {screenshotPath}");
                            }
                        }
                    }
                }
            } finally {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }

        private static AsyncMapLoader.GameMapData LoadMapDataWithoutUnityContext(RoMapDataLoader loader, string mapName) {
            return Task.Run(async () => await loader.Load(mapName)).GetAwaiter().GetResult();
        }

        private static string CaptureMap(AsyncMapLoader.GameMapData mapData, RoAssetDatabase assets, CaptureSettings settings, CaptureMode mode, CaptureShot shot, string cachePass) {
            GameObject root = null;
            GameObject cameraObject = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = null;
            Texture2D screenshot = null;

            try {
                root = BuildMapObject(mapData, assets, settings, mode);
                Bounds bounds = CalculateBounds(root);
                cameraObject = new GameObject("RO Regression Screenshot Camera");
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                ConfigureCamera(camera, bounds, settings.Width, settings.Height, shot);

                target = RenderTexture.GetTemporary(settings.Width, settings.Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                screenshot = new Texture2D(settings.Width, settings.Height, TextureFormat.RGBA32, false);
                screenshot.ReadPixels(new Rect(0, 0, settings.Width, settings.Height), 0, 0);
                screenshot.Apply(false, false);

                string fileName = BuildScreenshotFileName(mapData.Name, mode, shot, cachePass);
                string path = Path.GetFullPath(Path.Combine(settings.OutputDirectory, fileName));
                File.WriteAllBytes(path, screenshot.EncodeToPNG());
                return path;
            } finally {
                RenderTexture.active = previousActive;
                if (target != null) {
                    RenderTexture.ReleaseTemporary(target);
                }

                if (screenshot != null) {
                    UnityEngine.Object.DestroyImmediate(screenshot);
                }

                if (cameraObject != null) {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }

                if (root != null) {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static GameObject BuildMapObject(AsyncMapLoader.GameMapData mapData, RoAssetDatabase assets, CaptureSettings settings, CaptureMode mode) {
            var root = new GameObject("RO Regression Map - " + mapData.Name);
            Mesh groundMesh = RoGroundMeshBuilder.Build(mapData.CompiledGround);
            Bounds renderedGroundBounds = RoMapCoordinateSystem.RenderedGroundLocalBounds(groundMesh);

            GameObject ground = new GameObject("RO Ground");
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = RoMapCoordinateSystem.GroundLocalPosition;
            ground.transform.localScale = RoMapCoordinateSystem.GroundLocalScale;
            ground.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            var groundRenderer = ground.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = mode == CaptureMode.TintGroundAndModels
                ? CreateDebugMaterial("RO Debug Ground", new Color(0.1f, 0.45f, 1f, 1f))
                : CreateGroundMaterial(mapData.CompiledGround, assets);

            if (mode != CaptureMode.GroundOnly) {
                GameObject models = BuildModels(mapData, assets, settings.ModelInstanceLimit);
                models.transform.SetParent(root.transform, false);
                models.transform.localPosition = RoMapCoordinateSystem.ModelRootLocalPosition(mapData.CompiledGround);
                if (mode == CaptureMode.TintGroundAndModels) {
                    ApplyDebugMaterial(models, CreateDebugMaterial("RO Debug Models", new Color(1f, 0.15f, 0.85f, 1f)));
                }

                if (mode == CaptureMode.Normal) {
                    GameObject water = BuildWater(mapData.CompiledGround, mapData.World.water, assets);
                    if (water != null) {
                        water.transform.SetParent(root.transform, false);
                    }
                }
            }

            root.transform.localPosition = RoMapCoordinateSystem.MapRootLocalPosition(renderedGroundBounds);
            return root;
        }

        private static GameObject BuildModels(AsyncMapLoader.GameMapData mapData, RoAssetDatabase assets, int modelInstanceLimit) {
            RoModelRenderer.UvDebugMode = RoModelRenderer.ModelUvDebugMode.Default;
            RoModelRenderer.TextureWrapDebugMode = RoModelRenderer.ModelTextureWrapDebugMode.Repeat;
            RoModelRenderer.DisableBlackKey = false;
            RoModelRenderer.ClearCaches();
            return RoModelRenderer.BuildModels(mapData.CompiledModels, modelInstanceLimit, assets);
        }

        private static GameObject BuildWater(GND.Mesh compiledGround, RSW.WaterInfo waterInfo, RoAssetDatabase assets) {
            Mesh mesh = RoWaterMeshBuilder.Build(compiledGround);
            if (mesh == null) {
                return null;
            }

            var water = new GameObject("RO Water");
            water.transform.localScale = RoMapCoordinateSystem.WaterLocalScale;
            water.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = water.AddComponent<MeshRenderer>();
            Material material = CreateWaterMaterial();
            renderer.sharedMaterial = material;
            water.AddComponent<RoWaterAnimator>().Initialize(waterInfo, material, assets);
            return water;
        }

        private static Material CreateGroundMaterial(GND.Mesh compiledGround, RoAssetDatabase assets) {
            Shader shader = Shader.Find("Ragnarok/RO Ground Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) {
                name = "RO Regression Ground Material",
                color = Color.white
            };

            Texture2D atlas = RoGroundTextureAtlasBuilder.Build(compiledGround, assets);
            material.mainTexture = atlas;
            SetTextureIfPropertyExists(material, "_BaseMap", atlas);
            SetTextureIfPropertyExists(material, "_MainTex", atlas);
            SetTextureIfPropertyExists(material, "_Lightmap", CreateRoImageTexture(compiledGround.lightmap, "RO Regression Ground Lightmap", FilterMode.Bilinear));
            SetTextureIfPropertyExists(material, "_Tintmap", CreateRoImageTexture(compiledGround.tileColor, "RO Regression Ground Tintmap", FilterMode.Bilinear));
            SetFloatIfPropertyExists(material, "_Cutoff", 0.01f);
            SetFloatIfPropertyExists(material, "_UseLightmap", 1f);
            SetFloatIfPropertyExists(material, "_UseTint", 1f);
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Back);
            return material;
        }

        private static Texture2D CreateRoImageTexture(RoImage image, string name, FilterMode filterMode) {
            if (image.data == null || image.data.Length == 0 || image.width <= 0 || image.height <= 0) {
                return Texture2D.whiteTexture;
            }

            Texture2D texture = image.ConvertToTexture2D(linear: true);
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = filterMode;
            return texture;
        }

        private static Material CreateWaterMaterial() {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) {
                name = "RO Regression Water Material",
                color = new Color(1f, 1f, 1f, 0.78f)
            };
            SetColorIfPropertyExists(material, "_BaseColor", material.color);
            SetColorIfPropertyExists(material, "_Color", material.color);
            SetFloatIfPropertyExists(material, "_Cull", 0f);
            SetTransparentIfSupported(material);
            return material;
        }

        private static Material CreateDebugMaterial(string name, Color color) {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) {
                name = name,
                color = color
            };
            SetColorIfPropertyExists(material, "_BaseColor", color);
            SetColorIfPropertyExists(material, "_Color", color);
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Back);
            return material;
        }

        private static void ConfigureCamera(UnityEngine.Camera camera, Bounds bounds, int width, int height, CaptureShot shot) {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.43f, 0.39f, 1f);
            camera.orthographic = true;
            camera.aspect = width / (float) height;
            camera.nearClipPlane = 0.1f;

            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float distance = Mathf.Max(80f, maxSize * 1.8f);
            Vector3 viewDirection = Quaternion.Euler(55f, 45f, 0f) * Vector3.forward;
            Vector3 focus = GetShotFocus(bounds, shot);
            camera.transform.position = focus - viewDirection * distance;
            camera.transform.LookAt(focus);
            camera.farClipPlane = distance + maxSize * 3f;

            if (shot == CaptureShot.Overview) {
                Vector3[] corners = GetBoundsCorners(bounds);
                float maxLocalX = 0f;
                float maxLocalY = 0f;
                for (int i = 0; i < corners.Length; i++) {
                    Vector3 local = camera.transform.InverseTransformPoint(corners[i]);
                    maxLocalX = Mathf.Max(maxLocalX, Mathf.Abs(local.x));
                    maxLocalY = Mathf.Max(maxLocalY, Mathf.Abs(local.y));
                }

                camera.orthographicSize = Mathf.Max(maxLocalY, maxLocalX / camera.aspect) * 1.08f;
            } else {
                camera.orthographicSize = Mathf.Clamp(maxSize * 0.16f, 28f, 90f);
            }
        }

        private static Bounds CalculateBounds(GameObject root) {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) {
                return new Bounds(root.transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static Vector3[] GetBoundsCorners(Bounds bounds) {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new[] {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
        }

        private static Vector3 GetShotFocus(Bounds bounds, CaptureShot shot) {
            Vector3 center = bounds.center;
            switch (shot) {
                case CaptureShot.CenterClose:
                    return center;
                case CaptureShot.NorthClose:
                    return center + new Vector3(0f, 0f, bounds.size.z * 0.24f);
                case CaptureShot.SouthClose:
                    return center - new Vector3(0f, 0f, bounds.size.z * 0.24f);
                case CaptureShot.EastClose:
                    return center + new Vector3(bounds.size.x * 0.24f, 0f, 0f);
                case CaptureShot.WestClose:
                    return center - new Vector3(bounds.size.x * 0.24f, 0f, 0f);
                default:
                    return center;
            }
        }

        private static string BuildScreenshotFileName(string mapName, CaptureMode mode, CaptureShot shot, string cachePass) {
            string name = Path.GetFileNameWithoutExtension(mapName).ToLowerInvariant();
            return $"{name}-{shot.ToString().ToLowerInvariant()}-{mode.ToString().ToLowerInvariant()}-{cachePass.ToLowerInvariant()}.png";
        }

        private static void ApplyDebugMaterial(GameObject root, Material material) {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                renderers[i].sharedMaterial = material;
            }
        }

        private static void SetTransparentIfSupported(Material material) {
            SetFloatIfPropertyExists(material, "_Surface", 1f);
            SetFloatIfPropertyExists(material, "_Blend", 0f);
            material.renderQueue = (int) RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
        }

        private static void SetColorIfPropertyExists(Material target, string property, Color value) {
            if (target.HasProperty(property)) {
                target.SetColor(property, value);
            }
        }

        private static void SetTextureIfPropertyExists(Material target, string property, Texture value) {
            if (target.HasProperty(property)) {
                target.SetTexture(property, value);
            }
        }

        private static void SetFloatIfPropertyExists(Material target, string property, float value) {
            if (target.HasProperty(property)) {
                target.SetFloat(property, value);
            }
        }

        private sealed class CaptureSettings {
            public string OutputDirectory { get; private set; }
            public string[] Maps { get; private set; }
            public CaptureMode[] Modes { get; private set; }
            public CaptureShot[] Shots { get; private set; }
            public string[] CachePasses { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public int ModelInstanceLimit { get; private set; }

            public static CaptureSettings FromEnvironment() {
                return new CaptureSettings {
                    OutputDirectory = GetString(OutputDirectoryEnv, DefaultOutputDirectory),
                    Maps = SplitString(MapsEnv, DefaultMaps),
                    Modes = SplitModes(Environment.GetEnvironmentVariable(ModesEnv), DefaultModes),
                    Shots = SplitShots(Environment.GetEnvironmentVariable(ShotsEnv), DefaultShots),
                    CachePasses = SplitString(CachePassesEnv, new[] { "cold" }),
                    Width = GetInt(WidthEnv, 1280),
                    Height = GetInt(HeightEnv, 720),
                    ModelInstanceLimit = GetInt(ModelLimitEnv, 0)
                };
            }
        }

        private static string GetString(string envName, string fallback) {
            string value = Environment.GetEnvironmentVariable(envName);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static int GetInt(string envName, int fallback) {
            string value = Environment.GetEnvironmentVariable(envName);
            return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
        }

        private static string[] SplitString(string envName, string[] fallback) {
            string value = Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }

            return value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static CaptureMode[] SplitModes(string value, CaptureMode[] fallback) {
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }

            string[] parts = value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var modes = new List<CaptureMode>(parts.Length);
            for (int i = 0; i < parts.Length; i++) {
                if (Enum.TryParse(parts[i], true, out CaptureMode mode)) {
                    modes.Add(mode);
                } else {
                    Debug.LogWarning($"Unknown RO screenshot mode '{parts[i]}'.");
                }
            }

            return modes.Count > 0 ? modes.ToArray() : fallback;
        }

        private static CaptureShot[] SplitShots(string value, CaptureShot[] fallback) {
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }

            string[] parts = value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var shots = new List<CaptureShot>(parts.Length);
            for (int i = 0; i < parts.Length; i++) {
                if (Enum.TryParse(parts[i], true, out CaptureShot shot)) {
                    shots.Add(shot);
                } else {
                    Debug.LogWarning($"Unknown RO screenshot shot '{parts[i]}'.");
                }
            }

            return shots.Count > 0 ? shots.ToArray() : fallback;
        }

        private static class RoRegressionContentSource {
            private const string SettingsAssetPath = "Assets/LocalRagnarokContent.asset";
            private const string ProfilePathEnv = "RO_TEST_PROFILE_PATH";
            private const string ContentRootEnv = "RO_TEST_CONTENT_ROOT";
            private const string GrfFilesEnv = "RO_TEST_GRF_FILES";

            public static RoAssetDatabase CreateAssetDatabaseOrThrow() {
                if (TryCreateFromJsonProfile(out RoAssetDatabase profileAssets)) {
                    return profileAssets;
                }

                if (TryCreateFromEnvironment(out RoAssetDatabase environmentAssets)) {
                    return environmentAssets;
                }

                RagnarokContentSettings settings = AssetDatabase.LoadAssetAtPath<RagnarokContentSettings>(SettingsAssetPath);
                if (settings == null) {
                    throw new InvalidOperationException(
                        $"Missing {SettingsAssetPath}. Configure legal RO content there, or set {ProfilePathEnv}, or set {ContentRootEnv}/{GrfFilesEnv}."
                    );
                }

                return CreateAssetDatabase(settings.RootPath, settings.GrfFiles, SettingsAssetPath);
            }

            private static bool TryCreateFromJsonProfile(out RoAssetDatabase assets) {
                assets = null;
                string profilePath = Environment.GetEnvironmentVariable(ProfilePathEnv);
                if (string.IsNullOrWhiteSpace(profilePath)) {
                    return false;
                }

                string fullPath = Path.GetFullPath(profilePath);
                if (!File.Exists(fullPath)) {
                    throw new FileNotFoundException($"{ProfilePathEnv} points to a missing file.", fullPath);
                }

                RegressionContentProfile profile = JsonUtility.FromJson<RegressionContentProfile>(File.ReadAllText(fullPath));
                if (profile == null) {
                    throw new InvalidOperationException($"Could not parse regression profile '{fullPath}'.");
                }

                assets = CreateAssetDatabase(profile.rootPath, profile.grfFiles, fullPath);
                return true;
            }

            private static bool TryCreateFromEnvironment(out RoAssetDatabase assets) {
                assets = null;
                string rootPath = Environment.GetEnvironmentVariable(ContentRootEnv);
                string grfFiles = Environment.GetEnvironmentVariable(GrfFilesEnv);
                if (string.IsNullOrWhiteSpace(rootPath) && string.IsNullOrWhiteSpace(grfFiles)) {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(grfFiles)) {
                    throw new InvalidOperationException($"Set both {ContentRootEnv} and {GrfFilesEnv} to use environment content.");
                }

                assets = CreateAssetDatabase(rootPath, SplitGrfFiles(grfFiles), "environment variables");
                return true;
            }

            private static RoAssetDatabase CreateAssetDatabase(string rootPath, System.Collections.Generic.IReadOnlyList<string> grfFiles, string sourceName) {
                if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) {
                    throw new DirectoryNotFoundException($"Configured RO content root from {sourceName} does not exist: '{rootPath}'.");
                }

                if (grfFiles == null || grfFiles.Count == 0) {
                    throw new InvalidOperationException($"No GRF files configured in {sourceName}.");
                }

                foreach (string grfFile in grfFiles) {
                    if (string.IsNullOrWhiteSpace(grfFile)) {
                        continue;
                    }

                    string fullPath = Path.IsPathRooted(grfFile) ? grfFile : Path.Combine(rootPath, grfFile);
                    if (!File.Exists(fullPath)) {
                        throw new FileNotFoundException($"Configured GRF file from {sourceName} does not exist.", fullPath);
                    }
                }

                var assets = new RoAssetDatabase();
                try {
                    assets.Initialize(rootPath, grfFiles);
                    return assets;
                } catch {
                    assets.Dispose();
                    throw;
                }
            }

            private static string[] SplitGrfFiles(string grfFiles) {
                return grfFiles.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            }

            [Serializable]
            private sealed class RegressionContentProfile {
                public string name;
                public string rootPath;
                public string[] grfFiles;
            }
        }
    }
}
