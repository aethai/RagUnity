using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Ragnarok.Assets;
using Ragnarok.Audio;
using Ragnarok.Camera;
using Ragnarok.Content;
using Ragnarok.Entities;
using Ragnarok.Input;
using Ragnarok.Sprites;
using Ragnarok.UI;
using ROIO.Loaders;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// M-1: This class is a development smoke test and must not ship in production builds.
// #if UNITY_EDITOR keeps it out of the Runtime assembly's compiled output for Player builds.
#if UNITY_EDITOR
namespace Ragnarok.Maps {
    public sealed class RoMapSmokeTest : MonoBehaviour {
        private const string GeneratedMapPrefix = "RO Map - ";
        private const string GeneratedClickTargetMarkerName = "Click Target Marker";
        private const float ActorGroundOffset = 0f;
        private const float DebugCapsuleCenterHeight = 1.8f;

        [SerializeField] private RagnarokContentSettings contentSettings;
        [SerializeField] private bool useSavedContentProfile = true;
        [SerializeField] private string mapName = "prontera.rsw";
        [SerializeField] private string[] testMapNames = {
            "prontera.rsw",
            "geffen.rsw",
            "payon.rsw",
            "izlude.rsw",
            "prt_in.rsw",
            "pay_dun00.rsw"
        };
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material wireframeMaterial;
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool configureCamera = true;
        [SerializeField] private bool useClassicRoCameraPreset = true;
        [SerializeField] private bool applyViewpointTableCameraPreset = false;
        [SerializeField] private float classicRoCameraDistance = RoCameraController.ClassicGravityDistance;
        [SerializeField] private bool enableClassicRoFog = true;
        [SerializeField] private bool logFogDiagnosticsOnLoad;
        [SerializeField] private bool disableCameraPostProcessingForGravityParity = true;
        [SerializeField] private bool showWireframe;
        [SerializeField] private bool disableGroundLightmap;
        [SerializeField] private bool disableGroundTint;
        [SerializeField] private RoGroundMeshBuilder.GroundTopDiagonalMode groundTopDiagonalMode;
        [SerializeField] private GroundAtlasFilterDebugMode groundAtlasFilterMode = GroundAtlasFilterDebugMode.Bilinear;
        [SerializeField] private bool enableRswLighting = true;
        [SerializeField, Range(0f, 1f)] private float rswLightingStrength = 1f;
        [SerializeField] private bool useGroundLightmapAlpha = true;
        [SerializeField] private bool logLightingDiagnosticsOnLoad;
        [SerializeField] private bool enableRswPointLights;
        [SerializeField] private int maxRswPointLights = 32;
        [SerializeField] private float rswPointLightRangeScale = 0.5f;
        [SerializeField] private float rswPointLightIntensityScale = 0.5f;
        [SerializeField] private bool logPointLightDiagnosticsOnLoad;
        [SerializeField] private bool showSkyAndClouds = true;
        [SerializeField] private bool showModels = true;
        [SerializeField] private bool forceModelCullOff;
        [SerializeField] private bool enableModelPickDiagnostics;
        [SerializeField] private RoModelRenderer.ModelUvDebugMode modelUvDebugMode;
        [SerializeField] private RoModelRenderer.ModelTextureWrapDebugMode modelTextureWrapMode;
        [SerializeField] private bool disableModelBlackKey;
        [SerializeField] private float modelVerticalOffset;
        [SerializeField] private bool limitModelInstancesForDebug;
        [SerializeField] private int maxModelInstances = 300;
        [SerializeField] private bool logModelMaterialAuditOnLoad;
        [SerializeField] private bool showWater = true;
        [SerializeField] private DebugRenderMode debugRenderMode;
        [SerializeField] private bool enableMapAudio = true;
        [SerializeField] private bool enableMapBgm = true;
        [SerializeField] private bool enableMapAmbientSounds = true;
        [SerializeField] private int maxAmbientSoundEmitters = 64;
        [SerializeField] private RoAudioSettings audioSettings = new RoAudioSettings();
        [SerializeField] private bool createPlayerPlaceholder = true;
        [SerializeField] private bool useSpritePlayerPlaceholder = true;
        [SerializeField] private bool useLayeredPlayerPlaceholder = true;
        [SerializeField] private string playerSpriteBasePath;
        [SerializeField] private string playerSpritePalettePath;
        [SerializeField] private string playerHeadSpriteBasePath;
        [SerializeField] private string playerHairSpriteBasePath;
        [SerializeField] private string playerHairPalettePath;
        [SerializeField] private string playerWeaponSpriteBasePath;
        [SerializeField] private string playerShieldSpriteBasePath;
        [SerializeField] private string playerGarmentSpriteBasePath;
        [SerializeField] private string playerHeadgearTopSpriteBasePath;
        [SerializeField] private string playerHeadgearMidSpriteBasePath;
        [SerializeField] private string playerHeadgearLowerSpriteBasePath;
        [SerializeField] private int playerSpriteActionIndex;
        [SerializeField] private bool showPlayerDebugCapsule;
        [SerializeField] private bool showPlayerShadow = true;
        [SerializeField] private bool showSpritePreview;
        [SerializeField] private string spritePreviewBasePath;
        [SerializeField] private int spritePreviewActionIndex;
        [SerializeField] private bool useSpawnCellOverride;
        [SerializeField] private Vector2Int spawnCell = new Vector2Int(100, 100);
#if ENABLE_INPUT_SYSTEM
        [Header("Input Actions")]
        [SerializeField] private InputActionReference pointerPositionAction;
        [SerializeField] private InputActionReference primaryClickAction;
        [SerializeField] private InputActionReference headLookModifierAction;
#endif
        [SerializeField] private float debugCapsuleCenterHeight = DebugCapsuleCenterHeight;
        [SerializeField] private bool preferRenderedGroundSpawn = true;
        [SerializeField] private bool logSpawnDiagnostics;

        private RagnarokContentService content;
        private GameObject currentMap;
        private Transform playerPlaceholder;
        private MeshCollider groundCollider;
        private RoGridSelector gridSelector;
        private RoCursorStateController cursorStateController;
        private GND currentGround;
        private GND.Mesh currentCompiledGround;
        private RSW.LightInfo currentLightInfo;
        private List<RSW.Light> currentPointLights;
        private bool hasLastHoverCell;
        private Vector2Int lastHoverCell;
        private int testMapIndex;
        private int loadVersion;

        public bool HasLastHoverCellForEditor => hasLastHoverCell;
        public Vector2Int LastHoverCellForEditor => lastHoverCell;
        public RagnarokContentService Content => content;
        public RoGridSelector GridSelector => gridSelector;

        public void RecordHoverCellFromEditorRay(Ray ray) {
            RecordHoverCellFromRay(ray);
        }

        public bool TryMovePlayerToCell(int x, int y) {
            if (playerPlaceholder == null || gridSelector == null) {
                return false;
            }

            var cell = new Vector2Int(x, y);
            if (!gridSelector.IsCellWalkable(cell)) {
                if (gridSelector.Altitude == null ||
                    !RoGridPathfinder.TryFindNearestWalkable(gridSelector.Altitude, cell, 16, out cell)) {
                    return false;
                }
            }

            playerPlaceholder.position = gridSelector.GetCellWorldCenter(cell, ActorGroundOffset);
            return true;
        }

        public void UseSpawnCellFromEditorRay(Ray ray, string sourceLabel) {
            if (TryGetSpawnCellFromRay(ray, sourceLabel, out Vector2Int cell)) {
                SetSpawnCellOverride(cell);
            }
        }

        public void PrintGroundTileFromEditorRay(Ray ray) {
            PrintGroundTileFromRay(ray);
        }

        public void PrintRendererFromEditorRay(Ray ray) {
            if (!enableModelPickDiagnostics) {
                Debug.LogWarning("F8 Scene view model pick is disabled. Enable Model Pick Diagnostics on RoMapSmokeTest first.", this);
                return;
            }

            PrintRendererFromRay(ray);
        }

        private enum DebugRenderMode {
            Normal,
            GroundOnly,
            GroundTextureOnly,
            ModelsOnly,
            TintGroundAndModels
        }

        private enum GroundAtlasFilterDebugMode {
            Bilinear,
            Point
        }

        private void Start() {
            if (loadOnStart) {
                QueueLoad("Start");
            }
        }

        private void Update() {
            RecordCurrentHoverCell();

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) {
                if (keyboard.f6Key.wasPressedThisFrame) {
                    UseMouseCellAsSpawn();
                }

                if (keyboard.f7Key.wasPressedThisFrame) {
                    PrintGroundTileUnderMouse();
                }

                if (enableModelPickDiagnostics && keyboard.f8Key.wasPressedThisFrame) {
                    PrintRendererUnderMouse();
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F6)) {
                UseMouseCellAsSpawn();
            }

            if (Input.GetKeyDown(KeyCode.F7)) {
                PrintGroundTileUnderMouse();
            }

            if (enableModelPickDiagnostics && Input.GetKeyDown(KeyCode.F8)) {
                PrintRendererUnderMouse();
            }
#endif
        }

        public async Task Load() {
            ApplyGravityParityLightingDefaults();
            int version = ++loadVersion;
            string requestedMapName = mapName;
            ClearGeneratedMaps();

            RagnarokContentService previousContent = content;
            RagnarokContentService loadContent = null;
            try {
                loadContent = new RagnarokContentService();
                (bool contentReady, string contentReport) = await TryInitializeContentAsync(loadContent);
                if (!contentReady) {
                    Debug.LogError(contentReport, this);
                    loadContent.Dispose();
                    return;
                }

                Debug.Log(contentReport, this);

                var loader = new RoMapDataLoader(loadContent.Assets);
                AsyncMapLoader.GameMapData mapData = await loader.Load(requestedMapName);
                if (version != loadVersion) {
                    loadContent.Dispose();
                    return;
                }

                content = loadContent;
                loadContent = null;
                previousContent?.Dispose();
                ShowMap(mapData);

                Debug.Log($"Loaded RO map '{mapData.Name}' with {mapData.Ground.width}x{mapData.Ground.height} cells.", this);
                content.Assets.LogMissingSummary(this);
            } catch (Exception ex) {
                loadContent?.Dispose();
                Debug.LogException(ex, this);
            }
        }

        private async Task<(bool Success, string Report)> TryInitializeContentAsync(RagnarokContentService service) {
            if (contentSettings != null) {
                try {
                    await service.BeginInitializeForStartup(contentSettings);
                    return (true, $"Loaded RO content from scene settings '{contentSettings.name}'.");
                } catch (Exception ex) {
                    return (false, $"Could not initialize RO content: {ex.GetBaseException().Message}");
                }
            }

            if (useSavedContentProfile &&
                RagnarokContentProfileStore.TryLoad(out RagnarokContentProfile profile, out string report)) {
                try {
                    await service.BeginInitializeForStartup(profile);
                    return (true, report);
                } catch (Exception ex) {
                    return (false, $"Could not initialize saved RO content profile: {ex.GetBaseException().Message}");
                }
            }

            return (false, "Assign RagnarokContentSettings or create a saved profile from Ragnarok/Content Setup.");
        }

        private bool TryInitializeContent(RagnarokContentService service, out string report) {
            if (contentSettings != null) {
                service.Initialize(contentSettings);
                report = $"Loaded RO content from scene settings '{contentSettings.name}'.";
                return true;
            }

            if (useSavedContentProfile && service.TryInitializeSavedProfile(out report)) {
                return true;
            }

            report = "Assign RagnarokContentSettings or create a saved profile from Ragnarok/Content Setup.";
            return false;
        }

        private void ApplyGravityParityLightingDefaults() {
            if (!enableRswLighting) {
                return;
            }

            rswLightingStrength = 1f;
            useGroundLightmapAlpha = true;
        }

        private void OnDestroy() {
            RoMapFog.ApplyDisabledGlobals();
            content?.Dispose();
        }

        [ContextMenu("Load Map")]
        private void ReloadMap() {
            QueueLoad("Load Map");
        }

        [ContextMenu("Load Next Test Map")]
        private void LoadNextTestMap() {
            mapName = GetNextTestMap(1);
            QueueLoad("Load Next Test Map");
        }

        [ContextMenu("Load Previous Test Map")]
        private void LoadPreviousTestMap() {
            mapName = GetNextTestMap(-1);
            QueueLoad("Load Previous Test Map");
        }

        private void QueueLoad(string source) {
            _ = LoadFromUnityCallback(source);
        }

        private async Task LoadFromUnityCallback(string source) {
            try {
                await Load();
            } catch (Exception ex) {
                Debug.LogException(new Exception($"RO map smoke-test load failed from {source}.", ex), this);
            }
        }

        [ContextMenu("Clear Generated Maps")]
        private void ClearGeneratedMapsFromMenu() {
            ++loadVersion;
            ClearGeneratedMaps();
        }

        [ContextMenu("Print Asset Database Summary")]
        private void PrintAssetDatabaseSummary() {
            if (content == null || !content.IsInitialized) {
                Debug.LogWarning("RO asset database is not initialized yet.", this);
                return;
            }

            content.Assets.LogSummary(this);
            content.Assets.LogMissingSummary(this);
        }

        [ContextMenu("Print Current Hover Cell")]
        private void PrintCurrentHoverCell() {
            if (!TryGetCurrentHoverCell(out Vector2Int cell, out bool isLiveHover)) {
                Debug.LogWarning("No valid RO hover cell is available. Enter Play mode and hover a walkable tile first.", this);
                return;
            }

            string hoverSource = isLiveHover ? "Current" : "Last remembered";
            Debug.Log($"{hoverSource} RO hover cell on '{mapName}': {cell}", this);
        }

        [ContextMenu("Use Current/Last Hover Cell As Spawn")]
        private void UseCurrentHoverCellAsSpawn() {
            if (!TryGetCurrentHoverCell(out Vector2Int cell, out bool isLiveHover)) {
                Debug.LogWarning("No valid RO hover cell is available. Enter Play mode and hover a walkable tile first.", this);
                return;
            }

            SetSpawnCellOverride(cell);
            if (!isLiveHover) {
                Debug.Log($"Used last remembered RO hover cell {cell} for '{mapName}'.", this);
            }
        }

        [ContextMenu("Use Mouse Cell As Spawn (F6)")]
        private void UseMouseCellAsSpawn() {
            if (!TryGetSpawnCellUnderMouse(out Vector2Int cell)) {
                return;
            }

            SetSpawnCellOverride(cell);
        }

        [ContextMenu("Use Current Player Cell As Spawn")]
        private void UseCurrentPlayerCellAsSpawn() {
            if (gridSelector == null || playerPlaceholder == null ||
                !gridSelector.TryGetCellAtWorld(playerPlaceholder.position, out Vector2Int cell)) {
                Debug.LogWarning("Could not resolve the current player cell. Load the map and keep the player placeholder active first.", this);
                return;
            }

            SetSpawnCellOverride(cell);
        }

        [ContextMenu("Print Renderer Under Mouse")]
        private void PrintRendererUnderMouseFromMenu() {
            PrintRendererUnderMouse();
        }

        [ContextMenu("Print Model Material Audit")]
        private void PrintModelMaterialAuditFromMenu() {
            PrintModelMaterialAudit();
        }

        [ContextMenu("Print Ground Material Audit")]
        private void PrintGroundMaterialAuditFromMenu() {
            PrintGroundMaterialAudit();
        }

        [ContextMenu("Print Map Lighting Audit")]
        private void PrintMapLightingAuditFromMenu() {
            PrintMapLightingAudit();
        }

        [ContextMenu("Print Map Point Light Audit")]
        private void PrintMapPointLightAuditFromMenu() {
            PrintMapPointLightAudit();
        }

        [ContextMenu("Print Actor Shadow Audit")]
        private void PrintActorShadowAuditFromMenu() {
            if (playerPlaceholder == null) {
                Debug.LogWarning("RO actor shadow audit: no player placeholder exists.", this);
                return;
            }

            var shadow = playerPlaceholder.GetComponentInChildren<RoActorShadowVisual>();
            if (shadow == null) {
                Debug.LogWarning("RO actor shadow audit: no actor shadow component exists under the player placeholder.", this);
                return;
            }

            Debug.Log(shadow.BuildDiagnosticReport(), shadow);
        }

        private string GetNextTestMap(int direction) {
            if (testMapNames == null || testMapNames.Length == 0) {
                return mapName;
            }

            int currentIndex = Array.FindIndex(testMapNames, name => string.Equals(name, mapName, StringComparison.OrdinalIgnoreCase));
            if (currentIndex >= 0) {
                testMapIndex = currentIndex;
            }

            testMapIndex = (testMapIndex + direction + testMapNames.Length) % testMapNames.Length;
            return testMapNames[testMapIndex];
        }

        private void ShowMap(AsyncMapLoader.GameMapData mapData) {
            ClearGeneratedMaps();
            currentGround = mapData.Ground;
            currentCompiledGround = mapData.CompiledGround;
            currentLightInfo = mapData.World.light;
            currentPointLights = mapData.World.lights;

            currentMap = new GameObject($"{GeneratedMapPrefix}{mapData.Name}");
            currentMap.transform.SetParent(transform, false);

            RoGroundMeshBuilder.TopDiagonalMode = groundTopDiagonalMode;
            Mesh mesh = RoGroundMeshBuilder.Build(mapData.CompiledGround);
            Bounds renderedGroundLocalBounds = RoMapCoordinateSystem.RenderedGroundLocalBounds(mesh);
            GameObject ground = CreateGround(mesh, mapData.CompiledGround, mapData.World.light);
            if (debugRenderMode == DebugRenderMode.ModelsOnly) {
                SetRenderersEnabled(ground, false);
            } else if (debugRenderMode == DebugRenderMode.TintGroundAndModels) {
                ApplyDebugMaterial(ground, CreateDebugMaterial("RO Debug Ground", new Color(0.1f, 0.45f, 1f, 1f)));
            }

            if (showWireframe && debugRenderMode != DebugRenderMode.ModelsOnly) {
                CreateWireframe(mapData.CompiledGround);
            }

            if (showModels && debugRenderMode != DebugRenderMode.GroundOnly && debugRenderMode != DebugRenderMode.GroundTextureOnly) {
                GameObject models = CreateModels(mapData);
                RoMapLighting.ApplyToRenderers(models, RoMapLighting.FromWorld(mapData.World.light), enableRswLighting, useGroundLightmapAlpha, rswLightingStrength);
                if (debugRenderMode == DebugRenderMode.TintGroundAndModels) {
                    ApplyDebugMaterial(models, CreateDebugMaterial("RO Debug Models", new Color(1f, 0.15f, 0.85f, 1f)));
                }

                if (logModelMaterialAuditOnLoad) {
                    Debug.Log(RoModelRenderer.BuildMaterialAudit(models), this);
                }
            }

            if (showWater && debugRenderMode == DebugRenderMode.Normal) {
                CreateWater(mapData.CompiledGround, mapData.World.water);
            }

            if (enableRswPointLights && debugRenderMode == DebugRenderMode.Normal) {
                CreatePointLights(mapData);
            }

            if (logLightingDiagnosticsOnLoad) {
                PrintMapLightingAudit();
            }

            if (logPointLightDiagnosticsOnLoad) {
                PrintMapPointLightAudit();
            }

            currentMap.transform.localPosition = RoMapCoordinateSystem.MapRootLocalPosition(renderedGroundLocalBounds);

            if (enableMapAudio) {
                CreateMapAudio(mapData);
            }

            CreateGridSelector(mapData.Altitude);
            CreateCursorStateController();

            if (createPlayerPlaceholder) {
                CreatePlayerPlaceholder(mesh.bounds, mapData.Altitude);
            }

            if (showSpritePreview) {
                CreateSpritePreview(mesh.bounds, mapData.Altitude);
            }

            if (configureCamera) {
                CenterCamera(renderedGroundLocalBounds, mapData.Name);
            }

            cursorStateController?.SetRenderCamera(UnityEngine.Camera.main);
            ApplyGravityParityCameraDefaults();
            ApplyClassicRoFog(mapData.Name);

            if (showSkyAndClouds && debugRenderMode == DebugRenderMode.Normal && !IsIndoorMap(mapData.Name)) {
                CreateSky(mapData.Name);
            }
        }

        private void ClearGeneratedMaps() {
            playerPlaceholder = null;
            gridSelector = null;
            cursorStateController = null;
            groundCollider = null;
            currentLightInfo = null;
            currentPointLights = null;
            hasLastHoverCell = false;

            var mapsToDestroy = new List<GameObject>();
            if (currentMap != null) {
                AddGeneratedMap(mapsToDestroy, currentMap);
            }

            Scene scene = gameObject.scene;
            if (scene.IsValid() && scene.isLoaded) {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++) {
                    CollectGeneratedMaps(roots[i].transform, mapsToDestroy);
                }
            }

            for (int i = 0; i < mapsToDestroy.Count; i++) {
                DestroyGeneratedObject(mapsToDestroy[i]);
            }

            currentMap = null;
        }

        private static void CollectGeneratedMaps(Transform root, List<GameObject> mapsToDestroy) {
            if (root.name.StartsWith(GeneratedMapPrefix, StringComparison.Ordinal) ||
                string.Equals(root.name, GeneratedClickTargetMarkerName, StringComparison.Ordinal)) {
                AddGeneratedMap(mapsToDestroy, root.gameObject);
                return;
            }

            for (int i = 0; i < root.childCount; i++) {
                CollectGeneratedMaps(root.GetChild(i), mapsToDestroy);
            }
        }

        private static void AddGeneratedMap(List<GameObject> mapsToDestroy, GameObject map) {
            if (map != null && !mapsToDestroy.Contains(map)) {
                mapsToDestroy.Add(map);
            }
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(target);
            } else {
                DestroyImmediate(target);
            }
        }

        private GameObject CreateGround(Mesh mesh, GND.Mesh compiledGround, RSW.LightInfo lightInfo) {
            var groundObject = new GameObject("RO Ground");
            groundObject.transform.SetParent(currentMap.transform, false);
            ApplyGroundTransform(groundObject.transform);

            var filter = groundObject.AddComponent<MeshFilter>();
            var renderer = groundObject.AddComponent<MeshRenderer>();

            filter.sharedMesh = mesh;
            renderer.sharedMaterial = groundMaterial != null ? groundMaterial : CreateGroundMaterial(compiledGround, lightInfo);
            ApplyGroundMaterialRuntimeState(renderer.sharedMaterial, lightInfo);
            groundCollider = RoGroundMeshBuilder.AddCollider(groundObject, mesh);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) {
                groundObject.layer = groundLayer;
            }

            return groundObject;
        }

        private static void ApplyGroundTransform(Transform target) {
            target.localPosition = RoMapCoordinateSystem.GroundLocalPosition;
            target.localScale = RoMapCoordinateSystem.GroundLocalScale;
        }

        private GameObject CreateModels(AsyncMapLoader.GameMapData mapData) {
            int instanceLimit = limitModelInstancesForDebug ? maxModelInstances : 0;
            RoModelRenderer.UvDebugMode = modelUvDebugMode;
            RoModelRenderer.TextureWrapDebugMode = modelTextureWrapMode;
            RoModelRenderer.DisableBlackKey = disableModelBlackKey;
            RoModelRenderer.ClearCaches();
            content.Assets.ClearTextureCache();

            GameObject models = RoModelRenderer.BuildModels(mapData.CompiledModels, instanceLimit, content.Assets);
            models.transform.SetParent(currentMap.transform, false);
            models.transform.localPosition = RoMapCoordinateSystem.ModelRootLocalPosition(mapData.CompiledGround) + new Vector3(0f, modelVerticalOffset, 0f);
            if (forceModelCullOff) {
                ApplyCullMode(models, CullMode.Off);
            }
            Debug.Log($"Loaded RO models: {mapData.World.modelDescriptors.Count} descriptors, {RoModelRenderer.CountInstances(mapData.CompiledModels)} instances, limit {(instanceLimit > 0 ? instanceLimit.ToString() : "off")}, {RoModelRenderer.BuildMaterialSummary(models)}.", this);
            return models;
        }

        private void CreateWater(GND.Mesh compiledGround, RSW.WaterInfo waterInfo) {
            Mesh mesh = RoWaterMeshBuilder.Build(compiledGround);
            if (mesh == null) {
                return;
            }

            var water = new GameObject("RO Water");
            water.transform.SetParent(currentMap.transform, false);
            ApplyWaterTransform(water.transform);

            var filter = water.AddComponent<MeshFilter>();
            var renderer = water.AddComponent<MeshRenderer>();

            filter.sharedMesh = mesh;
            Material material = CreateWaterMaterial();
            renderer.sharedMaterial = material;

            var animator = water.AddComponent<RoWaterAnimator>();
            animator.Initialize(waterInfo, material, content.Assets);
        }

        private void CreatePointLights(AsyncMapLoader.GameMapData mapData) {
            GameObject pointLights = RoMapLighting.BuildPointLights(
                mapData.World.lights,
                mapData.CompiledGround,
                maxRswPointLights,
                rswPointLightRangeScale,
                rswPointLightIntensityScale
            );

            if (pointLights == null) {
                return;
            }

            pointLights.transform.SetParent(currentMap.transform, false);
        }

        private static void ApplyWaterTransform(Transform target) {
            target.localScale = RoMapCoordinateSystem.WaterLocalScale;
        }

        private void CreateSky(string loadedMapName) {
            if (!RoMapSky.TryGetSkyPrefs(loadedMapName, out _)) {
                return;
            }

            var sky = new GameObject("RO Sky");
            sky.transform.SetParent(currentMap.transform, true);
            var mapSky = sky.AddComponent<RoMapSky>();
            mapSky.Initialize(loadedMapName, content.Assets, UnityEngine.Camera.main);
        }

        private void CreateMapAudio(AsyncMapLoader.GameMapData mapData) {
            var audioRoot = new GameObject("RO Audio");
            audioRoot.transform.SetParent(currentMap.transform, false);

            var player = audioRoot.AddComponent<RoMapAudioPlayer>();
            player.Initialize(
                mapData,
                content.Assets,
                audioSettings,
                enableMapBgm,
                enableMapAmbientSounds,
                maxAmbientSoundEmitters,
                this
            );
        }

        private void CreatePlayerPlaceholder(Bounds mapBounds, GAT altitude) {
            GameObject player = new GameObject("Player Placeholder");
            player.transform.SetParent(currentMap.transform, false);
            player.transform.localPosition = GetInitialPlayerPosition(mapBounds, altitude);
            player.AddComponent<RoPlayerPlaceholder>();

            if (useSpritePlayerPlaceholder && TryCreatePlayerVisual(player.transform)) {
                if (showPlayerDebugCapsule) {
                    CreatePlayerDebugCapsule(player.transform);
                }
            } else {
                CreatePlayerDebugCapsule(player.transform);
            }

            if (showPlayerShadow) {
                CreatePlayerShadow(player.transform);
            }

            var clickToMove = player.AddComponent<RoClickToMoveController>();
            clickToMove.Initialize(UnityEngine.Camera.main, groundCollider);
            clickToMove.SetGridSelector(gridSelector);
#if ENABLE_INPUT_SYSTEM
            clickToMove.SetInputActions(pointerPositionAction, primaryClickAction, headLookModifierAction);
#endif
            clickToMove.SetCursorStateController(cursorStateController);
            var layeredVisual = player.GetComponentInChildren<RoActorLayeredVisual>();
            if (layeredVisual != null) {
                clickToMove.SetActorVisual(layeredVisual);
            } else {
                clickToMove.SetActorVisual(player.GetComponentInChildren<RoActorSpriteVisual>());
            }
            playerPlaceholder = player.transform;
        }

        private void CreatePlayerShadow(Transform parent) {
            var shadowObject = new GameObject("RO Actor Shadow");
            shadowObject.transform.SetParent(parent, false);
            var shadow = shadowObject.AddComponent<RoActorShadowVisual>();
            shadow.Initialize(content != null ? content.Assets : null, ActorGroundOffset);
        }

        private void CreatePlayerDebugCapsule(Transform parent) {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player Debug Capsule";
            player.transform.SetParent(parent, false);
            player.transform.localPosition = Vector3.up * Mathf.Max(0f, debugCapsuleCenterHeight);
            player.transform.localScale = new Vector3(1.3f, 1.8f, 1.3f);

            var renderer = player.FindComponent<MeshRenderer>();
            if (renderer != null) {
                renderer.sharedMaterial = CreatePlayerMaterial();
            }
        }

        private bool TryCreatePlayerVisual(Transform parent) {
            return useLayeredPlayerPlaceholder
                ? TryCreateLayeredPlayerSpriteVisual(parent)
                : TryCreatePlayerSpriteVisual(parent);
        }

        private bool TryCreateLayeredPlayerSpriteVisual(Transform parent) {
            if (content == null || !content.IsInitialized) {
                return false;
            }

            string bodyPath = ResolvePlayerBodySpriteBasePath();
            if (string.IsNullOrEmpty(bodyPath)) {
                Debug.LogWarning("Could not find a body .spr/.act pair for layered player placeholder.", this);
                return false;
            }

            RoActorSpriteLayerSpec[] layerSpecs = BuildPlayerLayerSpecs(bodyPath);
            var visualObject = new GameObject("RO Actor Layered Visual");
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = Vector3.up * RoActorSpriteVisual.GroundedVisualYOffset;

            var visual = visualObject.AddComponent<RoActorLayeredVisual>();
            try {
                if (visual.Initialize(content.Assets, layerSpecs)) {
                    Debug.Log($"Created layered player placeholder with {visual.LayerCount} layer(s), body='{bodyPath}.spr/.act'.", this);
                    return true;
                }
            } catch (Exception ex) {
                DestroyGeneratedObject(visualObject);
                Debug.LogWarning($"Could not create layered player placeholder: {ex.Message}", this);
                return false;
            }

            DestroyGeneratedObject(visualObject);
            Debug.LogWarning("Could not load any sprite layers for layered player placeholder.", this);
            return false;
        }

        private string ResolvePlayerBodySpriteBasePath() {
            string basePath = RoActorSpriteVisual.NormalizeBasePath(playerSpriteBasePath);
            if (TryUseConfiguredPlayerSpriteBasePath(basePath, "Player Sprite Base Path", out string resolvedBasePath)) {
                return resolvedBasePath;
            }

            string previewBasePath = RoActorSpriteVisual.NormalizeBasePath(spritePreviewBasePath);
            if (TryUseConfiguredPlayerSpriteBasePath(previewBasePath, "Sprite Preview Base Path", out resolvedBasePath)) {
                return resolvedBasePath;
            }

            return content != null &&
                content.IsInitialized &&
                RoActorSpritePathResolver.TryFindFirstPlayerSpritePair(content.Assets, out resolvedBasePath)
                ? resolvedBasePath
                : string.Empty;
        }

        private RoActorSpriteLayerSpec[] BuildPlayerLayerSpecs(string bodyPath) {
            RoActorAppearanceRequest appearance = RoActorAppearanceRequest.CreateDefaultPlayerProbe(playerSpriteActionIndex);
            appearance.bodyBasePath = bodyPath;
            appearance.bodyPalettePath = playerSpritePalettePath;
            appearance.headBasePath = playerHeadSpriteBasePath;
            appearance.hairBasePath = playerHairSpriteBasePath;
            appearance.hairPalettePath = playerHairPalettePath;
            appearance.weaponBasePath = playerWeaponSpriteBasePath;
            appearance.shieldBasePath = playerShieldSpriteBasePath;
            appearance.garmentBasePath = playerGarmentSpriteBasePath;
            appearance.headgearTopBasePath = playerHeadgearTopSpriteBasePath;
            appearance.headgearMidBasePath = playerHeadgearMidSpriteBasePath;
            appearance.headgearLowerBasePath = playerHeadgearLowerSpriteBasePath;
            return RoActorSpritePathResolver.BuildPlayerLayerSpecs(content.Assets, appearance, logContext: this);
        }

        private string ResolveOptionalPlayerLayerPath(string configuredPath, Func<RoAssetDatabase, string> fallbackResolver) {
            string basePath = RoActorSpriteVisual.NormalizeBasePath(configuredPath);
            if (!string.IsNullOrEmpty(basePath)) {
                if (content == null || !content.IsInitialized || PlayerSpritePairExists(basePath)) {
                    return basePath;
                }

                Debug.LogWarning($"Configured actor layer path '{basePath}' was not found as a .spr/.act pair. Falling back to auto-resolution.", this);
            }

            return content != null && content.IsInitialized ? fallbackResolver(content.Assets) ?? string.Empty : string.Empty;
        }

        private string ResolveOptionalPlayerAssetPath(string configuredPath, Func<RoAssetDatabase, string> fallbackResolver) {
            string path = RoActorSpritePathResolver.NormalizeOptionalAssetPath(configuredPath);
            if (!string.IsNullOrEmpty(path)) {
                if (content == null || !content.IsInitialized || content.Assets.Exists(path)) {
                    return path;
                }

                Debug.LogWarning($"Configured actor palette path '{path}' was not found. Falling back to auto-resolution.", this);
            }

            return content != null && content.IsInitialized ? fallbackResolver(content.Assets) ?? string.Empty : string.Empty;
        }

        private bool TryUseConfiguredPlayerSpriteBasePath(string basePath, string label, out string resolvedBasePath) {
            resolvedBasePath = string.Empty;
            if (string.IsNullOrEmpty(basePath)) {
                return false;
            }

            if (content != null && content.IsInitialized && !PlayerSpritePairExists(basePath)) {
                Debug.LogWarning($"{label} '{basePath}' was not found as a .spr/.act pair. Falling back to auto-resolution.", this);
                return false;
            }

            resolvedBasePath = basePath;
            return true;
        }

        private bool PlayerSpritePairExists(string basePath) {
            return content != null &&
                content.IsInitialized &&
                content.Assets.Exists($"{basePath}.spr") &&
                content.Assets.Exists($"{basePath}.act");
        }

        private bool TryCreatePlayerSpriteVisual(Transform parent) {
            if (content == null || !content.IsInitialized) {
                return false;
            }

            string basePath = ResolvePlayerBodySpriteBasePath();
            if (string.IsNullOrEmpty(basePath)) {
                Debug.LogWarning("Could not find any matching RO .spr/.act pair for player sprite placeholder.", this);
                return false;
            }

            var visualObject = new GameObject($"RO Actor Sprite Visual - {basePath}");
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = Vector3.up * RoActorSpriteVisual.GroundedVisualYOffset;
            var visual = visualObject.AddComponent<RoActorSpriteVisual>();
            try {
                string palettePath = ResolveOptionalPlayerAssetPath(
                    playerSpritePalettePath,
                    assets => RoActorSpritePathResolver.TryFindPreferredPlayerBodyPalette(assets, out string resolved) ? resolved : null
                );
                if (visual.Initialize(content.Assets, basePath, palettePath, playerSpriteActionIndex)) {
                    Debug.Log($"Created player sprite placeholder from '{basePath}.spr/.act' action={visual.ActionIndex}.", this);
                    return true;
                }
            } catch (Exception ex) {
                DestroyGeneratedObject(visualObject);
                Debug.LogWarning($"Could not create player sprite placeholder from '{basePath}.spr/.act': {ex.Message}", this);
                return false;
            }

            DestroyGeneratedObject(visualObject);
            Debug.LogWarning($"Could not load player sprite placeholder pair '{basePath}.spr/.act'.", this);
            return false;
        }

        [ContextMenu("Spawn Sprite Preview")]
        private void SpawnSpritePreviewFromMenu() {
            if (currentMap == null || currentGround == null) {
                Debug.LogWarning("Load a map before spawning a sprite preview.", this);
                return;
            }

            CreateSpritePreview(new Bounds(Vector3.zero, new Vector3(currentGround.width, 0f, currentGround.height)), null);
        }

        private void CreateSpritePreview(Bounds mapBounds, GAT altitude) {
            if (content == null || !content.IsInitialized) {
                return;
            }

            string basePath = RoActorSpriteVisual.NormalizeBasePath(spritePreviewBasePath);
            if (string.IsNullOrEmpty(basePath) && !TryFindFirstSpritePair(out basePath)) {
                Debug.LogWarning("Could not find any matching RO .spr/.act pair for sprite preview.", this);
                return;
            }

            SPR spr = content.Assets.Load<SPR>($"{basePath}.spr");
            ACT act = content.Assets.Load<ACT>($"{basePath}.act");
            if (spr == null || act == null) {
                Debug.LogWarning($"Could not load sprite preview pair '{basePath}.spr/.act'.", this);
                return;
            }

            var preview = new GameObject($"RO Sprite Preview - {basePath}");
            preview.transform.SetParent(currentMap.transform, false);
            Vector3 position = altitude != null ? GetInitialPlayerPosition(mapBounds, altitude) : Vector3.zero;
            preview.transform.localPosition = position + new Vector3(2f, 0f, 0f);

            var animator = preview.AddComponent<RoActSpriteAnimator>();
            animator.Initialize(spr, act, spritePreviewActionIndex);
            Debug.Log($"Spawned RO sprite preview '{basePath}' action={animator.ActionIndex}.", this);
        }

        private bool TryFindFirstSpritePair(out string basePath) {
            basePath = null;
            return content != null && content.IsInitialized && TryFindFirstSpritePair(content.Assets, out basePath);
        }

        private bool TryFindFirstPlayerSpritePair(out string basePath) {
            basePath = null;
            return content != null && content.IsInitialized && RoActorSpritePathResolver.TryFindFirstPlayerSpritePair(content.Assets, out basePath);
        }

        private static bool TryFindFirstSpritePair(RoAssetDatabase assets, out string basePath) {
            return TryFindFirstSpritePair(assets, null, out basePath);
        }

        private static bool TryFindFirstPlayerSpritePair(RoAssetDatabase assets, out string basePath) {
            return RoActorSpritePathResolver.TryFindFirstPlayerSpritePair(assets, out basePath);
        }

        private static bool TryFindPreferredPlayerBodySprite(RoAssetDatabase assets, out string basePath) {
            string humanRace = LegacyPathSegment(0xC0, 0xCE, 0xB0, 0xA3, 0xC1, 0xB7);
            string body = LegacyPathSegment(0xB8, 0xF6, 0xC5, 0xEB);
            string male = LegacyPathSegment(0xB3, 0xB2);
            string female = LegacyPathSegment(0xBF, 0xA9);
            string novice = LegacyPathSegment(0xC3, 0xCA, 0xBA, 0xB8, 0xC0, 0xDA);

            string maleNovice = $"data/sprite/{humanRace}/{body}/{male}/{novice}_{male}";
            if (assets.Exists($"{maleNovice}.spr") && assets.Exists($"{maleNovice}.act")) {
                basePath = maleNovice;
                return true;
            }

            string femaleNovice = $"data/sprite/{humanRace}/{body}/{female}/{novice}_{female}";
            if (assets.Exists($"{femaleNovice}.spr") && assets.Exists($"{femaleNovice}.act")) {
                basePath = femaleNovice;
                return true;
            }

            basePath = null;
            return false;
        }

        private static bool TryFindPreferredPlayerHeadSprite(RoAssetDatabase assets, out string basePath) {
            string humanRace = LegacyPathSegment(0xC0, 0xCE, 0xB0, 0xA3, 0xC1, 0xB7);
            string head = LegacyPathSegment(0xB8, 0xD3, 0xB8, 0xAE, 0xC5, 0xEB);
            string male = LegacyPathSegment(0xB3, 0xB2);
            string female = LegacyPathSegment(0xBF, 0xA9);

            string maleHead = $"data/sprite/{humanRace}/{head}/{male}/2_{male}";
            if (assets.Exists($"{maleHead}.spr") && assets.Exists($"{maleHead}.act")) {
                basePath = maleHead;
                return true;
            }

            string femaleHead = $"data/sprite/{humanRace}/{head}/{female}/2_{female}";
            if (assets.Exists($"{femaleHead}.spr") && assets.Exists($"{femaleHead}.act")) {
                basePath = femaleHead;
                return true;
            }

            return TryFindBestScoredSpritePair(assets, ScorePlayerHeadSpritePath, out basePath);
        }

        private static bool TryFindFirstSpritePair(RoAssetDatabase assets, Predicate<string> predicate, out string basePath) {
            basePath = null;
            List<string> sprites = assets.ListFilesWithExtension("spr");
            for (int i = 0; i < sprites.Count; i++) {
                string candidate = RoActorSpriteVisual.NormalizeBasePath(sprites[i]);
                if (string.IsNullOrEmpty(candidate)) {
                    continue;
                }

                if (predicate != null && !predicate(candidate)) {
                    continue;
                }

                if (assets.Exists($"{candidate}.act")) {
                    basePath = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindBestScoredSpritePair(RoAssetDatabase assets, Func<string, int> scorePath, out string basePath) {
            basePath = null;
            int bestScore = 0;
            List<string> sprites = assets.ListFilesWithExtension("spr");
            for (int i = 0; i < sprites.Count; i++) {
                string candidate = RoActorSpriteVisual.NormalizeBasePath(sprites[i]);
                if (string.IsNullOrEmpty(candidate) || !assets.Exists($"{candidate}.act")) {
                    continue;
                }

                int score = scorePath(candidate);
                if (score > bestScore) {
                    bestScore = score;
                    basePath = candidate;
                }
            }

            return !string.IsNullOrEmpty(basePath);
        }

        private static int ScorePlayerSpritePath(string basePath) {
            string path = basePath.Replace('\\', '/').ToLowerInvariant();
            if (!path.Contains("/sprite/") ||
                path.Contains("/book/") ||
                path.Contains("/effect/") ||
                path.Contains("/item/") ||
                path.Contains("/texture/")) {
                return 0;
            }

            string accessory = LegacyPathSegment(0xBE, 0xC7, 0xBC, 0xBC, 0xBB, 0xE7, 0xB8, 0xAE);
            string robe = LegacyPathSegment(0xB7, 0xCE, 0xBA, 0xEA);
            string humanRace = LegacyPathSegment(0xC0, 0xCE, 0xB0, 0xA3, 0xC1, 0xB7);
            string body = LegacyPathSegment(0xB8, 0xF6, 0xC5, 0xEB);
            string head = LegacyPathSegment(0xB8, 0xD3, 0xB8, 0xAE, 0xC5, 0xEB);
            string novice = LegacyPathSegment(0xC3, 0xCA, 0xBA, 0xB8, 0xC0, 0xDA);
            string male = LegacyPathSegment(0xB3, 0xB2);

            if (path.Contains($"/{accessory}/") || path.Contains($"/{robe}/")) {
                return 0;
            }

            int score = 1;
            if (path.Contains($"/{humanRace}/")) {
                score += 1000;
            }

            if (path.Contains($"/{body}/")) {
                score += 600;
            }

            if (path.Contains($"/{head}/")) {
                score -= 500;
            }

            if (path.Contains(novice)) {
                score += 300;
            }

            if (path.Contains($"/{male}/")) {
                score += 50;
            }

            if (path.Contains("/costume_")) {
                score -= 100;
            }

            if (path.Contains("_h_")) {
                score -= 100;
            }

            return Mathf.Max(0, score);
        }

        private static int ScorePlayerHeadSpritePath(string basePath) {
            string path = basePath.Replace('\\', '/').ToLowerInvariant();
            if (!path.Contains("/sprite/") ||
                path.Contains("/book/") ||
                path.Contains("/effect/") ||
                path.Contains("/item/") ||
                path.Contains("/texture/")) {
                return 0;
            }

            string accessory = LegacyPathSegment(0xBE, 0xC7, 0xBC, 0xBC, 0xBB, 0xE7, 0xB8, 0xAE);
            string humanRace = LegacyPathSegment(0xC0, 0xCE, 0xB0, 0xA3, 0xC1, 0xB7);
            string body = LegacyPathSegment(0xB8, 0xF6, 0xC5, 0xEB);
            string head = LegacyPathSegment(0xB8, 0xD3, 0xB8, 0xAE, 0xC5, 0xEB);
            string male = LegacyPathSegment(0xB3, 0xB2);

            if (path.Contains($"/{accessory}/") || path.Contains($"/{body}/")) {
                return 0;
            }

            int score = 1;
            if (path.Contains($"/{humanRace}/")) {
                score += 1000;
            }

            if (path.Contains($"/{head}/")) {
                score += 800;
            }

            if (path.EndsWith($"/2_{male}", StringComparison.OrdinalIgnoreCase)) {
                score += 200;
            }

            if (path.Contains($"/{male}/")) {
                score += 50;
            }

            if (path.Contains("/costume_")) {
                score -= 100;
            }

            return Mathf.Max(0, score);
        }

        private static string LegacyPathSegment(params int[] bytes) {
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) {
                chars[i] = (char) bytes[i];
            }

            return new string(chars);
        }

        private static List<string> FindSpritePairs(RoAssetDatabase assets, int maxCount, out int totalPairs) {
            int limit = Mathf.Max(0, maxCount);
            totalPairs = 0;
            var pairs = new List<string>(limit);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> sprites = assets.ListFilesWithExtension("spr");
            for (int i = 0; i < sprites.Count; i++) {
                string candidate = RoActorSpriteVisual.NormalizeBasePath(sprites[i]);
                if (string.IsNullOrEmpty(candidate) ||
                    !seen.Add(candidate) ||
                    !assets.Exists($"{candidate}.act")) {
                    continue;
                }

                totalPairs++;
                if (pairs.Count < limit) {
                    pairs.Add(candidate);
                }
            }

            return pairs;
        }

        private Vector3 GetInitialPlayerPosition(Bounds mapBounds, GAT altitude) {
            if (TryGetSpawnOverride(altitude, out Vector3 overrideSpawn)) {
                return overrideSpawn;
            }

            if (preferRenderedGroundSpawn && TryGetRenderedGroundSpawn(out Vector3 renderedGroundSpawn)) {
                return renderedGroundSpawn;
            }

            if (altitude != null && altitude.cells != null) {
                var center = new Vector2Int((int) altitude.width / 2, (int) altitude.height / 2);
                int radius = Mathf.Max((int) altitude.width, (int) altitude.height);
                if (RoGridPathfinder.TryFindNearestWalkable(altitude, center, radius, out Vector2Int resolvedCell)) {
                    if (logSpawnDiagnostics) {
                        Debug.Log($"RO spawn for '{mapName}' resolved from map center to GAT cell {resolvedCell}.", this);
                    }

                    return RoGridPathfinder.CellToLocalCenter(altitude, resolvedCell, ActorGroundOffset);
                }
            }

            if (logSpawnDiagnostics) {
                Debug.LogWarning($"RO spawn for '{mapName}' fell back to rendered map bounds center.", this);
            }

            return new Vector3(mapBounds.center.x, -mapBounds.center.y + ActorGroundOffset, mapBounds.center.z);
        }

        private bool TryGetSpawnOverride(GAT altitude, out Vector3 position) {
            position = default;
            if (!useSpawnCellOverride) {
                return false;
            }

            if (altitude == null || altitude.cells == null) {
                Debug.LogWarning($"Spawn cell override is enabled for '{mapName}', but this map has no GAT data.", this);
                return false;
            }

            Vector2Int resolvedCell = spawnCell;
            if (!RoGridPathfinder.IsWalkable(altitude, resolvedCell.x, resolvedCell.y)) {
                if (!RoGridPathfinder.TryFindNearestWalkable(altitude, resolvedCell, 16, out resolvedCell)) {
                    Debug.LogWarning($"Spawn cell override {spawnCell} for '{mapName}' is not walkable and no nearby walkable cell was found.", this);
                    return false;
                }

                Debug.LogWarning($"Spawn cell override {spawnCell} for '{mapName}' is not walkable. Using nearest walkable cell {resolvedCell}.", this);
            }

            if (logSpawnDiagnostics) {
                Debug.Log($"RO spawn for '{mapName}' uses explicit GAT cell {resolvedCell}.", this);
            }

            position = RoGridPathfinder.CellToLocalCenter(altitude, resolvedCell, ActorGroundOffset);
            return true;
        }

        private bool TryGetRenderedGroundSpawn(out Vector3 position) {
            position = default;
            if (groundCollider == null) {
                return false;
            }

            Bounds bounds = groundCollider.bounds;
            Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + 100f, bounds.center.z);
            var ray = new Ray(origin, Vector3.down);
            if (!groundCollider.Raycast(ray, out RaycastHit hit, bounds.size.y + 250f)) {
                return false;
            }

            Vector3 localHit = currentMap != null ? currentMap.transform.InverseTransformPoint(hit.point) : hit.point;
            position = localHit + Vector3.up * ActorGroundOffset;
            if (logSpawnDiagnostics) {
                Debug.Log($"RO spawn for '{mapName}' uses rendered ground raycast at local {position}.", this);
            }

            return true;
        }

        private bool TryGetCurrentHoverCell(out Vector2Int cell) {
            return TryGetCurrentHoverCell(out cell, out _);
        }

        private bool TryGetCurrentHoverCell(out Vector2Int cell, out bool isLiveHover) {
            if (gridSelector != null && gridSelector.HasCell) {
                cell = gridSelector.CurrentCell;
                isLiveHover = true;
                return true;
            }

            if (hasLastHoverCell) {
                cell = lastHoverCell;
                isLiveHover = false;
                return true;
            }

            cell = default;
            isLiveHover = false;
            return false;
        }

        private void RecordCurrentHoverCell() {
            if (gridSelector == null || !gridSelector.HasCell) {
                return;
            }

            lastHoverCell = gridSelector.CurrentCell;
            hasLastHoverCell = true;
        }

        private bool TryGetSpawnCellUnderMouse(out Vector2Int cell) {
            cell = default;
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) {
                Debug.LogWarning("Cannot set spawn from mouse: no main camera is available.", this);
                return false;
            }

            if (!TryGetPointerScreenPosition(out Vector2 screenPosition)) {
                Debug.LogWarning("Cannot set spawn from mouse: no pointer position is available.", this);
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            return TryGetSpawnCellFromRay(ray, "mouse", out cell);
        }

        private bool TryGetSpawnCellFromRay(Ray ray, string sourceLabel, out Vector2Int cell) {
            cell = default;
            if (gridSelector == null) {
                Debug.LogWarning($"Cannot set spawn from {sourceLabel}: load a map first so the GAT walk surface exists.", this);
                return false;
            }

            if (!gridSelector.TryGetPoint(ray, out Vector3 point)) {
                Debug.LogWarning($"Cannot set spawn from {sourceLabel}: pointer is not over the walk surface. Hover the Scene/Game view over the map, then press F6.", this);
                return false;
            }

            if (!gridSelector.TryGetCellAtWorld(point, out cell)) {
                Debug.LogWarning($"Cannot set spawn from {sourceLabel}: point {point} is outside the GAT grid.", this);
                return false;
            }

            if (gridSelector.IsCellWalkable(cell)) {
                return true;
            }

            if (RoGridPathfinder.TryFindNearestWalkable(gridSelector.Altitude, cell, 16, out Vector2Int resolvedCell)) {
                Debug.LogWarning($"Mouse spawn cell {cell} for '{mapName}' is not walkable. Using nearest walkable cell {resolvedCell}.", this);
                cell = resolvedCell;
                return true;
            }

            Debug.LogWarning($"Cannot set spawn from {sourceLabel}: cell {cell} is not walkable and no nearby walkable cell was found.", this);
            return false;
        }

        private void RecordHoverCellFromRay(Ray ray) {
            if (gridSelector == null) {
                return;
            }

            if (!gridSelector.TryGetPoint(ray, out Vector3 point)) {
                return;
            }

            if (!gridSelector.TryGetCellAtWorld(point, out Vector2Int cell) || !gridSelector.IsCellWalkable(cell)) {
                return;
            }

            lastHoverCell = cell;
            hasLastHoverCell = true;
        }

        private void SetSpawnCellOverride(Vector2Int cell) {
            spawnCell = cell;
            useSpawnCellOverride = true;
            ApplySpawnCellToPlayer(cell);
            string persistenceHint = Application.isPlaying
                ? "This was set in Play Mode; copy these X/Y values into the Inspector after stopping Play if you want to keep them."
                : "Save the scene to keep this spawn override.";
            Debug.Log($"Spawn cell override for '{mapName}' set to {spawnCell}. Press Load/Reload Map to apply it from a clean load. {persistenceHint}", this);
        }

        private void ApplySpawnCellToPlayer(Vector2Int cell) {
            if (playerPlaceholder == null || gridSelector == null) {
                return;
            }

            playerPlaceholder.position = gridSelector.GetCellWorldCenter(cell, ActorGroundOffset);
        }

        private void CreateGridSelector(GAT altitude) {
            var selectorObject = new GameObject("RO Grid Selector");
            selectorObject.transform.SetParent(currentMap.transform, false);
            gridSelector = selectorObject.AddComponent<RoGridSelector>();
            gridSelector.Initialize(UnityEngine.Camera.main, groundCollider, altitude, content.Assets);
#if ENABLE_INPUT_SYSTEM
            gridSelector.SetPointerPositionAction(pointerPositionAction);
#endif
        }

        private void CreateCursorStateController() {
            var cursorObject = new GameObject("RO Cursor State");
            cursorObject.transform.SetParent(currentMap.transform, false);
            cursorStateController = cursorObject.AddComponent<RoCursorStateController>();
#if ENABLE_INPUT_SYSTEM
            cursorStateController.Initialize(content != null ? content.Assets : null, UnityEngine.Camera.main, pointerPositionAction);
#else
            cursorStateController.Initialize(content != null ? content.Assets : null, UnityEngine.Camera.main);
#endif
        }

        private void CreateWireframe(GND.Mesh compiledGround) {
            var wireframe = new GameObject("Ground Wireframe");
            wireframe.transform.SetParent(currentMap.transform, false);
            ApplyGroundTransform(wireframe.transform);

            var filter = wireframe.AddComponent<MeshFilter>();
            var renderer = wireframe.AddComponent<MeshRenderer>();

            filter.sharedMesh = RoGroundMeshBuilder.BuildWireframe(compiledGround);
            renderer.sharedMaterial = wireframeMaterial != null ? wireframeMaterial : CreateWireframeMaterial();
        }

        private void CenterCamera(Bounds bounds, string mapRsw) {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) {
                return;
            }

            var controller = camera.FindComponent<RoCameraController>();
            if (controller == null) {
                controller = camera.gameObject.AddComponent<RoCameraController>();
            }

            if (useClassicRoCameraPreset) {
                // 1. Reset to RRL-verified classic defaults (zoom, pitch, yaw, FOV, near/far)
                controller.ApplyClassicGravityPreset();

                // 2. Set up follow target / frame bounds with classic default distance
                if (playerPlaceholder != null) {
                    classicRoCameraDistance = RoCameraController.ClassicGravityDistance;
                    controller.Follow(playerPlaceholder, RoCameraController.ClassicGravityDistance);
                } else {
                    controller.FrameBounds(new Bounds(Vector3.zero, bounds.size));
                }

                // 3. Keep viewpointtable.txt opt-in. The accepted player camera baseline
                //    uses fixed classic zoom/pitch/FOV values; table presets can carry
                //    very large map inspection ranges that are not default gameplay zoom.
                RoAssetTables tables = content?.Assets?.Tables;
                if (tables != null && !string.IsNullOrEmpty(mapRsw)) {
                    bool appliedViewpoint = false;
                    if (applyViewpointTableCameraPreset &&
                        tables.TryGetViewpoint(mapRsw, out ROIO.Models.RoViewpointPreset preset)) {
                        controller.ApplyViewpointPreset(preset);
                        classicRoCameraDistance = controller.TargetDistance;
                        appliedViewpoint = true;
                    }

                    bool indoor = tables.IsIndoorMap(mapRsw);
                    if (indoor && camera != null) {
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = Color.black;
                    }

                    if (indoor && !appliedViewpoint) {
                        controller.ApplyRobrowserIndoorPreset();
                        classicRoCameraDistance = controller.TargetDistance;
                    } else {
                        controller.SetIndoorMode(indoor);
                    }
                }
                // Near/far managed by RoCameraController.Awake() via ClassicGravityNearPlane/FarPlane
            } else {
                // Non-classic mode: position camera on bounds, size-based near/far
                float size = Mathf.Max(bounds.size.x, bounds.size.z);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = size * 5f;
                if (playerPlaceholder != null) {
                    controller.Follow(playerPlaceholder, 30f);
                } else {
                    controller.FrameBounds(new Bounds(Vector3.zero, bounds.size));
                }
            }
        }

        private bool IsIndoorMap(string mapRsw) {
            RoAssetTables tables = content?.Assets?.Tables;
            return tables != null && !string.IsNullOrEmpty(mapRsw) && tables.IsIndoorMap(mapRsw);
        }

        private void ApplyGravityParityCameraDefaults() {
            if (!disableCameraPostProcessingForGravityParity) {
                return;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) {
                return;
            }

            camera.allowHDR = false;
            Behaviour[] components = camera.GetComponents<Behaviour>();
            for (int i = 0; i < components.Length; i++) {
                Behaviour component = components[i];
                if (component == null) {
                    continue;
                }

                Type type = component.GetType();
                if (type.FullName != "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData") {
                    continue;
                }

                SetBooleanMember(type, component, "renderPostProcessing", false);
                SetBooleanMember(type, component, "allowHDROutput", false);
                SetBooleanMember(type, component, "m_RenderPostProcessing", false);
                SetBooleanMember(type, component, "m_AllowHDROutput", false);
                break;
            }
        }

        private void ApplyClassicRoFog(string loadedMapName) {
            if (!enableClassicRoFog || content == null || content.Assets == null) {
                Debug.Log($"[RoFog] SKIP: enableClassicRoFog={enableClassicRoFog} content={content != null} assets={content?.Assets != null}", this);
                RoMapFog.ApplyDisabledGlobals();
                return;
            }

            if (RoMapFog.TryLoadSettings(content.Assets, loadedMapName, out RoMapFogSettings settings)) {
                Debug.Log(RoMapFog.BuildAudit(loadedMapName, settings), this);
                RoMapFog.ApplyGlobals(settings);
                return;
            }

            string rawTable = content.Assets.ReadText(RoMapFog.FogTablePath);
            string preview = rawTable == null ? "<NULL>" : rawTable.Length == 0 ? "<EMPTY>" : rawTable.Substring(0, Mathf.Min(300, rawTable.Length)).Replace("\n", "↵").Replace("\r", "");
            Debug.Log($"[RoFog] TryLoadSettings FAILED for '{loadedMapName}': tableExists={content.Assets.Exists(RoMapFog.FogTablePath)} tableLen={rawTable?.Length ?? -1}\n{preview}", this);
            RoMapFog.ApplyDisabledGlobals();
        }

        private static void SetBooleanMember(Type type, object instance, string name, bool value) {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(name, Flags);
            if (property != null && property.CanWrite && property.PropertyType == typeof(bool)) {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo field = type.GetField(name, Flags);
            if (field != null && field.FieldType == typeof(bool)) {
                field.SetValue(instance, value);
            }
        }

        public bool TryCalculateCurrentMapWorldBounds(out Bounds bounds) {
            bounds = default;
            if (currentMap == null) {
                return false;
            }

            Renderer[] renderers = currentMap.GetComponentsInChildren<Renderer>();
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null) {
                    continue;
                }

                if (!hasBounds) {
                    bounds = renderer.bounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        [ContextMenu("Print Map Bounds Diagnostics")]
        private void PrintMapBoundsDiagnostics() {
            if (currentMap == null) {
                Debug.LogWarning("No generated RO map is loaded.", this);
                return;
            }

            bool hasBounds = TryCalculateCurrentMapWorldBounds(out Bounds bounds);
            Debug.Log(
                hasBounds
                    ? $"RO map bounds: rootLocal={currentMap.transform.localPosition}, worldCenter={bounds.center}, worldSize={bounds.size}, worldMin={bounds.min}, worldMax={bounds.max}"
                    : "RO map bounds unavailable: no renderers found.",
                this
            );
        }

        private Material CreateGroundMaterial(GND.Mesh compiledGround, RSW.LightInfo lightInfo) {
            Shader shader = Shader.Find("Ragnarok/RO Ground Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader);
            material.name = "RO Ground Material";
            material.color = Color.white;
            FilterMode atlasFilterMode = groundAtlasFilterMode == GroundAtlasFilterDebugMode.Point
                ? FilterMode.Point
                : FilterMode.Bilinear;
            Texture2D atlas = RoGroundTextureAtlasBuilder.Build(compiledGround, content.Assets, atlasFilterMode);
            material.mainTexture = atlas;
            SetTextureIfPropertyExists(material, "_BaseMap", atlas);
            SetTextureIfPropertyExists(material, "_MainTex", atlas);
            SetTextureIfPropertyExists(material, "_Lightmap", CreateRoImageTexture(compiledGround.lightmap, "RO Ground Lightmap", FilterMode.Bilinear, true));
            SetTextureIfPropertyExists(material, "_Tintmap", CreateRoImageTexture(compiledGround.tileColor, "RO Ground Tintmap", FilterMode.Bilinear, true));
            SetFloatIfPropertyExists(material, "_Cutoff", 0.01f);
            ApplyGroundMaterialRuntimeState(material, lightInfo);
            SetTextureIfPropertyExists(material, "_BaseMap", material.mainTexture);
            SetTextureIfPropertyExists(material, "_MainTex", material.mainTexture);
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Back);
            material.renderQueue = RoMapRenderQueues.Ground;
            return material;
        }

        private void ApplyGroundMaterialRuntimeState(Material material, RSW.LightInfo lightInfo) {
            bool textureOnly = debugRenderMode == DebugRenderMode.GroundTextureOnly;
            SetFloatIfPropertyExists(material, "_UseLightmap", !textureOnly && !disableGroundLightmap ? 1f : 0f);
            SetFloatIfPropertyExists(material, "_UseTint", !textureOnly && !disableGroundTint ? 1f : 0f);
            RoMapLighting.ApplyToMaterial(
                material,
                RoMapLighting.FromWorld(lightInfo),
                !textureOnly && enableRswLighting,
                !textureOnly && useGroundLightmapAlpha,
                textureOnly ? 0f : rswLightingStrength
            );
        }

        private static Texture2D CreateRoImageTexture(RoImage image, string name, FilterMode filterMode, bool linearData) {
            if (image.data == null || image.data.Length == 0 || image.width <= 0 || image.height <= 0) {
                return Texture2D.whiteTexture;
            }

            Texture2D texture = image.ConvertToTexture2D(linearData);
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = filterMode;
            return texture;
        }

        private static Material CreateWireframeMaterial() {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader);
            material.name = "RO Ground Wireframe Material";
            material.color = new Color(0.06f, 0.08f, 0.08f, 1f);
            material.SetFloat("_Cull", 0f);
            material.renderQueue = RoMapRenderQueues.DebugOverlay;
            return material;
        }

        private static Material CreateWaterMaterial() {
            Shader shader = Shader.Find("Ragnarok/RO Water Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader);
            material.name = "RO Water Material";
            material.color = new Color(1f, 1f, 1f, 0.78f);
            SetColorIfPropertyExists(material, "_BaseColor", material.color);
            SetColorIfPropertyExists(material, "_Color", material.color);
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Off);
            SetFloatIfPropertyExists(material, "_ZWrite", 0f);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            SetFloatIfPropertyExists(material, "_WaterOpacity", RoWaterAnimator.DefaultWaterOpacity);
            SetTransparentIfSupported(material);
            return material;
        }

        private static Material CreatePlayerMaterial() {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader);
            material.name = "RO Player Placeholder Material";
            material.color = new Color(1f, 0.2f, 0.12f, 1f);
            return material;
        }

        private static Material CreateDebugMaterial(string name, Color color) {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader);
            material.name = name;
            material.color = color;
            SetColorIfPropertyExists(material, "_BaseColor", color);
            SetColorIfPropertyExists(material, "_Color", color);
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Back);
            return material;
        }

        private static void ApplyDebugMaterial(GameObject root, Material material) {
            if (root == null || material == null) {
                return;
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                renderers[i].sharedMaterial = material;
            }
        }

        private static void SetRenderersEnabled(GameObject root, bool enabled) {
            if (root == null) {
                return;
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                renderers[i].enabled = enabled;
            }
        }

        private static void ApplyCullMode(GameObject root, CullMode cullMode) {
            if (root == null) {
                return;
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                Material[] materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++) {
                    SetFloatIfPropertyExists(materials[j], "_Cull", (float) cullMode);
                }
            }
        }

        public void UseCurrentOrLastHoverCellAsSpawnFromEditor() {
            UseCurrentHoverCellAsSpawn();
        }

        public bool TryUseFirstSpritePairAsPlayer(RoAssetDatabase assets, out string basePath) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (!RoActorSpritePathResolver.TryFindFirstPlayerSpritePair(assets, out basePath)) {
                return false;
            }

            playerSpriteBasePath = basePath;
            playerHeadSpriteBasePath = RoActorSpritePathResolver.TryFindPreferredPlayerHeadSprite(assets, out string headPath) ? headPath : string.Empty;
            playerHairSpriteBasePath = RoActorSpritePathResolver.TryFindPreferredPlayerHairSprite(assets, out string hairPath) ? hairPath : string.Empty;
            playerSpritePalettePath = RoActorSpritePathResolver.TryFindPreferredPlayerBodyPalette(assets, out string bodyPalettePath) ? bodyPalettePath : string.Empty;
            playerHairPalettePath = RoActorSpritePathResolver.TryFindPreferredPlayerHairPalette(assets, out string hairPalettePath) ? hairPalettePath : string.Empty;
            useSpritePlayerPlaceholder = true;
            useLayeredPlayerPlaceholder = true;
            return true;
        }

        public void PrintSpritePairsForEditor() {
            if (!TryGetDiagnosticAssets(out RoAssetDatabase assets, out RagnarokContentService temporaryContent, out string report)) {
                Debug.LogWarning(report, this);
                return;
            }

            try {
                const int maxPairs = 30;
                List<string> pairs = FindSpritePairs(assets, maxPairs, out int totalPairs);
                var builder = new StringBuilder();
                builder.AppendLine(report);
                builder.AppendLine($"RO sprite pairs found: {totalPairs}. Showing {pairs.Count}:");
                for (int i = 0; i < pairs.Count; i++) {
                    builder.AppendLine($"- {pairs[i]}");
                }

                if (pairs.Count == 0) {
                    builder.AppendLine("No matching .spr/.act pairs were found in the configured content.");
                } else {
                    builder.AppendLine("Paste one path into Player Sprite Base Path, or click 'Use First Sprite Pair As Player'.");
                }

                Debug.Log(builder.ToString(), this);
            } finally {
                temporaryContent?.Dispose();
            }
        }

        public bool TryGetDiagnosticAssets(out RoAssetDatabase assets, out RagnarokContentService temporaryContent, out string report) {
            temporaryContent = null;
            if (content != null && content.IsInitialized) {
                assets = content.Assets;
                report = "Using currently loaded RO content.";
                return true;
            }

            temporaryContent = new RagnarokContentService();
            try {
                if (TryInitializeContent(temporaryContent, out report)) {
                    assets = temporaryContent.Assets;
                    report = $"{report} Using temporary editor scan.";
                    return true;
                }
            } catch (Exception ex) {
                report = $"Could not initialize RO content for sprite scan: {ex.Message}";
            }

            temporaryContent.Dispose();
            temporaryContent = null;
            assets = null;
            return false;
        }

        private void PrintRendererUnderMouse() {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) {
                Debug.LogWarning("Cannot pick renderer: no main camera is available.", this);
                return;
            }

            if (!TryGetPointerScreenPosition(out Vector2 screenPosition)) {
                Debug.LogWarning("Cannot pick renderer: no pointer position is available.", this);
                return;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            PrintRendererFromRay(ray);
        }

        private void PrintRendererFromRay(Ray ray) {
            if (TryGetModelPickReferencePoint(ray, out Vector3 referencePoint)) {
                PrintNearestModelSurfaces(referencePoint);
                return;
            }

            Debug.LogWarning("RO pick: could not resolve a reference point under the mouse.", this);
        }

        private void PrintGroundTileUnderMouse() {
            if (currentGround == null || currentCompiledGround == null || groundCollider == null) {
                Debug.LogWarning("RO ground pick: no current ground data is available. Reload the map, then press F7 again.", this);
                return;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) {
                Debug.LogWarning("RO ground pick: no main camera is available.", this);
                return;
            }

            if (!TryGetPointerScreenPosition(out Vector2 screenPosition)) {
                Debug.LogWarning("RO ground pick: no pointer position is available.", this);
                return;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            PrintGroundTileFromRay(ray);
        }

        private void PrintGroundTileFromRay(Ray ray) {
            if (currentGround == null || currentCompiledGround == null || groundCollider == null) {
                Debug.LogWarning("RO ground pick: no current ground data is available. Reload the map, then press F7 again.", this);
                return;
            }

            if (!groundCollider.Raycast(ray, out RaycastHit hit, 10000f)) {
                Debug.LogWarning("RO ground pick: pointer is not over the ground collider.", this);
                return;
            }

            Vector3 local = groundCollider.transform.InverseTransformPoint(hit.point);
            int cellX = Mathf.FloorToInt(local.x / 2f);
            int cellY = Mathf.FloorToInt(local.z / 2f);
            if (cellX < 0 || cellY < 0 || cellX >= currentGround.width || cellY >= currentGround.height) {
                Debug.LogWarning($"RO ground pick: local point {local} is outside GND bounds {currentGround.width}x{currentGround.height}.", this);
                return;
            }

            GND.Surface surface = currentGround.surfaces[cellX + cellY * currentGround.width];
            var lines = new List<string> {
                $"RO ground pick at world={hit.point}, local={local}, cell=({cellX},{cellY}), triangle={hit.triangleIndex}",
                $"- surface heights={surface.height}, tileUp={surface.tileUp}, tileFront={surface.tileFront}, tileRight={surface.tileRight}"
            };

            AppendGroundTileInfo(lines, "up", surface.tileUp);
            AppendGroundTileInfo(lines, "front", surface.tileFront);
            AppendGroundTileInfo(lines, "right", surface.tileRight);

            Debug.Log(string.Join("\n", lines), this);
        }

        private void AppendGroundTileInfo(List<string> lines, string label, long tileIndex) {
            if (tileIndex < 0 || tileIndex >= currentGround.tiles.Length) {
                lines.Add($"- {label}: none");
                return;
            }

            GND.Tile tile = currentGround.tiles[tileIndex];
            string texture = tile.texture >= 0 && tile.texture < currentCompiledGround.textures.Length
                ? currentCompiledGround.textures[tile.texture]
                : "<texture index out of range>";
            lines.Add(
                $"- {label}: tile={tileIndex}, textureIndex={tile.texture}, texture='{texture}', light={tile.light}, color=({tile.color[0]},{tile.color[1]},{tile.color[2]},{tile.color[3]}), u={FormatVector4(tile.textureStart)}, v={FormatVector4(tile.textureEnd)}"
            );
        }

        private static string FormatVector4(Vector4 value) {
            return $"({value.x:0.####},{value.y:0.####},{value.z:0.####},{value.w:0.####})";
        }

        private static string FormatRenderFloat(float value) {
            return value.ToString("0.###");
        }

        private bool TryGetModelPickReferencePoint(Ray ray, out Vector3 point) {
            if (groundCollider != null && groundCollider.Raycast(ray, out RaycastHit groundHit, 10000f)) {
                point = groundHit.point;
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f);
            if (hits.Length > 0) {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                point = hits[0].point;
                return true;
            }

            var mapPlane = new Plane(Vector3.up, currentMap != null ? currentMap.transform.position : Vector3.zero);
            if (mapPlane.Raycast(ray, out float enter)) {
                point = ray.GetPoint(enter);
                return true;
            }

            point = default;
            return false;
        }

        private void PrintNearestModelSurfaces(Vector3 point) {
            if (currentMap == null) {
                Debug.LogWarning("RO pick: no current map is available.", this);
                return;
            }

            RoModelSurfaceInfo[] surfaces = currentMap.GetComponentsInChildren<RoModelSurfaceInfo>(true);
            if (surfaces.Length == 0) {
                Debug.LogWarning("RO pick: no RO model metadata found. Reload the map, then press F8 again.", this);
                return;
            }

            var candidates = new List<ModelPickCandidate>();
            for (int i = 0; i < surfaces.Length; i++) {
                MeshRenderer renderer = surfaces[i].FindComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) {
                    continue;
                }

                float distance = renderer.bounds.SqrDistance(point);
                candidates.Add(new ModelPickCandidate(surfaces[i], renderer, distance));
            }

            if (candidates.Count == 0) {
                Debug.LogWarning($"RO pick: found {surfaces.Length} RO model metadata component(s), but no active renderers near {point}.", this);
                return;
            }

            candidates.Sort((a, b) => a.SqrDistance.CompareTo(b.SqrDistance));
            int count = Mathf.Min(8, candidates.Count);
            var lines = new List<string> {
                $"RO pick nearest model surfaces around {point} (no direct model collider hit; showing {count}/{candidates.Count}):"
            };

            for (int i = 0; i < count; i++) {
                ModelPickCandidate candidate = candidates[i];
                RoModelSurfaceInfo info = candidate.Info;
                Material material = candidate.Renderer.sharedMaterial;
                Texture texture = material != null ? material.mainTexture : null;
                lines.Add(
                    $"- distance={Mathf.Sqrt(candidate.SqrDistance):0.###}, instance={info.InstanceIndex}, model='{info.ModelFile}', node='{info.NodeName}', texture='{info.TextureName}', material='{material?.name}', mainTexture='{texture?.name}', mode={info.MaterialMode}, queue={info.MaterialRenderQueue}, cull={FormatRenderFloat(info.MaterialCull)}, zWrite={FormatRenderFloat(info.MaterialZWrite)}, blend=({FormatRenderFloat(info.MaterialSrcBlend)},{FormatRenderFloat(info.MaterialDstBlend)}), cutoff={FormatRenderFloat(info.MaterialCutoff)}, useClip={FormatRenderFloat(info.MaterialUseAlphaClip)}, alpha={FormatRenderFloat(info.MaterialAlpha)}, twoSided={info.TwoSided}, vertices={info.VertexCount}, triangles={info.TriangleCount}, descriptorPos={info.DescriptorPosition}, descriptorRot={info.DescriptorRotation}, descriptorScale={info.DescriptorScale}, localBoundsCenter={info.LocalMeshBounds.center}, localBoundsSize={info.LocalMeshBounds.size}, worldBoundsCenter={candidate.Renderer.bounds.center}, worldBoundsSize={candidate.Renderer.bounds.size}, path='{BuildTransformPath(candidate.Renderer.transform)}'"
                );
            }

            Debug.Log(string.Join("\n", lines), this);
        }

        private void PrintModelMaterialAudit() {
            if (currentMap == null) {
                Debug.LogWarning("RO model material audit: no current map is loaded.", this);
                return;
            }

            Transform models = currentMap.transform.Find("RO Models");
            if (models == null) {
                Debug.LogWarning("RO model material audit: no RO Models root found on the current map.", this);
                return;
            }

            Debug.Log(RoModelRenderer.BuildMaterialAudit(models.gameObject), this);
        }

        private void PrintGroundMaterialAudit() {
            if (currentMap == null || currentGround == null || currentCompiledGround == null) {
                Debug.LogWarning("RO ground material audit: no current map is loaded.", this);
                return;
            }

            Transform ground = currentMap.transform.Find("RO Ground");
            MeshRenderer renderer = ground != null ? ground.FindComponent<MeshRenderer>() : null;
            MeshFilter filter = ground != null ? ground.FindComponent<MeshFilter>() : null;
            Material material = renderer != null ? renderer.sharedMaterial : null;
            Texture atlas = GetGroundAtlasTexture(material);

            var lines = new List<string> {
                "RO ground material audit:",
                $"- map='{mapName}', gnd={currentGround.width}x{currentGround.height}, textures={CountOrZero(currentCompiledGround.textures)}, tiles={CountOrZero(currentGround.tiles)}, surfaces={CountOrZero(currentGround.surfaces)}",
                $"- debugRenderMode={debugRenderMode}, topDiagonal={groundTopDiagonalMode}, atlasFilter={groundAtlasFilterMode}",
                $"- meshVerts={currentCompiledGround.meshVertCount}, meshQuads={currentCompiledGround.meshVertCount / 4}, waterVerts={currentCompiledGround.waterVertCount}, unityMeshVerts={(filter != null && filter.sharedMesh != null ? filter.sharedMesh.vertexCount : 0)}",
                $"- groundObject='{ground?.name ?? "<missing>"}', localPosition={(ground != null ? ground.localPosition.ToString() : "<missing>")}, localScale={(ground != null ? ground.localScale.ToString() : "<missing>")}",
                $"- material='{material?.name ?? "<missing>"}', shader='{(material != null && material.shader != null ? material.shader.name : "<missing>")}', queue={(material != null ? material.renderQueue.ToString() : "<missing>")}, cull={FormatMaterialFloat(material, "_Cull")}, cutoff={FormatMaterialFloat(material, "_Cutoff")}, useLightmap={FormatMaterialFloat(material, "_UseLightmap")}, useTint={FormatMaterialFloat(material, "_UseTint")}",
                $"- lighting use={FormatMaterialFloat(material, "_RoUseLighting")}, strength={FormatMaterialFloat(material, "_RoLightingStrength")}, direction={FormatMaterialVector(material, "_RoLightDirection")}, ambient={FormatMaterialColor(material, "_RoLightAmbient")}, diffuse={FormatMaterialColor(material, "_RoLightDiffuse")}, opacity={FormatMaterialFloat(material, "_RoLightOpacity")}, useLightmapAlpha={FormatMaterialFloat(material, "_RoUseLightmapAlpha")}",
                FormatTexture("atlas", atlas),
                FormatTexture("_BaseMap", GetTextureIfPropertyExists(material, "_BaseMap")),
                FormatTexture("_MainTex", GetTextureIfPropertyExists(material, "_MainTex")),
                FormatTexture("_Lightmap", GetTextureIfPropertyExists(material, "_Lightmap")),
                FormatTexture("_Tintmap", GetTextureIfPropertyExists(material, "_Tintmap")),
                $"- rawLightmap={FormatRoImage(currentCompiledGround.lightmap)}, rawTintmap={FormatRoImage(currentCompiledGround.tileColor)}, shadowMapBytes={CountOrZero(currentCompiledGround.shadowMap)}"
            };

            Debug.Log(string.Join("\n", lines), this);
        }

        private void PrintMapLightingAudit() {
            if (currentLightInfo == null) {
                Debug.LogWarning("RO map lighting audit: no current map lighting is loaded.", this);
                return;
            }

            Debug.Log(RoMapLighting.BuildAudit(currentLightInfo, enableRswLighting, useGroundLightmapAlpha, rswLightingStrength), this);
        }

        private void PrintMapPointLightAudit() {
            if (currentCompiledGround == null) {
                Debug.LogWarning("RO map point light audit: no current map is loaded.", this);
                return;
            }

            Transform pointLights = currentMap != null ? currentMap.transform.Find("RO Point Lights") : null;
            Debug.Log(
                RoMapLighting.BuildPointLightAudit(
                    currentPointLights,
                    currentCompiledGround,
                    enableRswPointLights,
                    maxRswPointLights,
                    rswPointLightRangeScale,
                    rswPointLightIntensityScale,
                    pointLights != null ? pointLights.gameObject : null
                ),
                this
            );
        }

        private readonly struct ModelPickCandidate {
            public ModelPickCandidate(RoModelSurfaceInfo info, MeshRenderer renderer, float sqrDistance) {
                Info = info;
                Renderer = renderer;
                SqrDistance = sqrDistance;
            }

            public RoModelSurfaceInfo Info { get; }
            public MeshRenderer Renderer { get; }
            public float SqrDistance { get; }
        }

        private static bool TryGetPointerScreenPosition(out Vector2 position) {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) {
                position = Mouse.current.position.ReadValue();
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            return true;
#else
            position = default;
            return false;
#endif
        }

        private static string BuildTransformPath(Transform transform) {
            if (transform == null) {
                return string.Empty;
            }

            var names = new List<string>();
            Transform current = transform;
            while (current != null) {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static Texture GetGroundAtlasTexture(Material material) {
            if (material == null) {
                return null;
            }

            if (material.mainTexture != null) {
                return material.mainTexture;
            }

            Texture baseMap = GetTextureIfPropertyExists(material, "_BaseMap");
            if (baseMap != null) {
                return baseMap;
            }

            return GetTextureIfPropertyExists(material, "_MainTex");
        }

        private static Texture GetTextureIfPropertyExists(Material material, string property) {
            return material != null && material.HasProperty(property) ? material.GetTexture(property) : null;
        }

        private static string FormatMaterialFloat(Material material, string property) {
            if (material == null || !material.HasProperty(property)) {
                return "<missing>";
            }

            return FormatRenderFloat(material.GetFloat(property));
        }

        private static string FormatMaterialVector(Material material, string property) {
            if (material == null || !material.HasProperty(property)) {
                return "<missing>";
            }

            Vector4 value = material.GetVector(property);
            return $"({value.x:0.###},{value.y:0.###},{value.z:0.###},{value.w:0.###})";
        }

        private static string FormatMaterialColor(Material material, string property) {
            if (material == null || !material.HasProperty(property)) {
                return "<missing>";
            }

            Color value = material.GetColor(property);
            return $"({value.r:0.###},{value.g:0.###},{value.b:0.###},{value.a:0.###})";
        }

        private static string FormatTexture(string label, Texture texture) {
            if (texture == null) {
                return $"- {label}: <missing>";
            }

            if (texture is Texture2D texture2D) {
                return $"- {label}: name='{texture2D.name}', type=Texture2D, size={texture2D.width}x{texture2D.height}, format={texture2D.format}, mipmaps={texture2D.mipmapCount}, filter={texture2D.filterMode}, wrap={texture2D.wrapMode}, aniso={texture2D.anisoLevel}";
            }

            return $"- {label}: name='{texture.name}', type={texture.GetType().Name}, size={texture.width}x{texture.height}, filter={texture.filterMode}, wrap={texture.wrapMode}, aniso={texture.anisoLevel}";
        }

        private static string FormatRoImage(RoImage image) {
            if (image.data == null || image.data.Length == 0 || image.width <= 0 || image.height <= 0) {
                return "<empty>";
            }

            return $"{image.width}x{image.height}, bytes={image.data.Length}";
        }

        private static int CountOrZero<T>(T[] values) {
            return values != null ? values.Length : 0;
        }

        private static int CountOrZero(byte[] values) {
            return values != null ? values.Length : 0;
        }

        private static void SetTransparentIfSupported(Material material) {
            if (material.HasProperty("_Surface")) {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_AlphaClip")) {
                material.SetFloat("_AlphaClip", 0f);
            }

            SetFloatIfPropertyExists(material, "_UseAlphaClip", 0f);
            SetFloatIfPropertyExists(material, "_ZWrite", 0f);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = RoMapRenderQueues.Water;
        }

        private static void SetTextureIfPropertyExists(Material material, string property, Texture texture) {
            if (material.HasProperty(property)) {
                material.SetTexture(property, texture);
            }
        }

        private static void SetFloatIfPropertyExists(Material material, string property, float value) {
            if (material.HasProperty(property)) {
                material.SetFloat(property, value);
            }
        }

        private static void SetColorIfPropertyExists(Material material, string property, Color color) {
            if (material.HasProperty(property)) {
                material.SetColor(property, color);
            }
        }
    }

}
#endif // UNITY_EDITOR
