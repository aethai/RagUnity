using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Ragnarok.Assets;
using Ragnarok.Audio;
using Ragnarok.Camera;
using Ragnarok.Content;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Effects;
using Ragnarok.Gameplay;
using Ragnarok.Input;
using Ragnarok.Network;
using Ragnarok.Sprites;
using Ragnarok.UI;
using Ragnarok.World;
using ROIO.Loaders;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Profiling;

namespace Ragnarok.Maps {
    public sealed class RoRuntimeMapController : MonoBehaviour {
        private const float ActorGroundOffset = 0f;
        private const float WeatherRuntimeSyncIntervalSeconds = 0.25f;
        private const string UniversalAdditionalCameraDataTypeName = "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData";
        private static readonly ProfilerMarker WeatherRuntimeSyncMarker = new ProfilerMarker("RO Map.WeatherRuntimeSync");
        private static readonly ProfilerMarker PendingMapEffectsMarker = new ProfilerMarker("RO Map.PendingMapEffects");
        private static readonly List<Light> LightTraversalScratch = new List<Light>(128);
        private static readonly List<RoActorLayeredVisual> LayeredVisualTraversalScratch = new List<RoActorLayeredVisual>(8);
        private static readonly List<RoActorSpriteVisual> SpriteVisualTraversalScratch = new List<RoActorSpriteVisual>(8);
        private static readonly List<RoActorShadowVisual> ShadowVisualTraversalScratch = new List<RoActorShadowVisual>(8);
        private static readonly List<MeshRenderer> LightingDiagnosticsRendererScratch = new List<MeshRenderer>(4096);
        private static int lightingDiagnosticsEnabledCache;
        private static int disableRealtimeShadowReceiveCache;
        private static int disableDayNightRuntimeCache;
        private static int disableRswLightingCache;
        private static int disableMapLightmapCache;
        private static int disableMapTintCache;
        private static int forceTextureOnlyMapCache;
        private static int debugRealtimeShadowsCache;

        [Header("Content")]
        [SerializeField] private RagnarokContentSettings contentSettings;
        [SerializeField] private bool useSavedContentProfile = true;
        [SerializeField] private bool initializeContentOnStart = true;

        [Header("Map")]
        [SerializeField] private string mapName = "prontera.rsw";
        [SerializeField] private bool loadOnStart;
        [SerializeField] private RoRuntimeMapRenderSettings renderSettings = new RoRuntimeMapRenderSettings();
        [SerializeField] private bool enableClassicRoFog = true;
        [SerializeField] private bool showSkyAndClouds = true;

        [Header("Camera")]
        [SerializeField] private bool configureCamera = true;
        [SerializeField] private bool applyViewpointTableCameraPreset = false;
        [SerializeField] private bool disableCameraPostProcessingForGravityParity = true;

        [Header("Local Player")]
        [SerializeField] private bool createLocalPlayer = true;
        [SerializeField] private bool createGridSelector = true;
        [SerializeField] private bool useLayeredPlayerVisual = true;
        [SerializeField] private bool showPlayerShadow = true;
        [SerializeField] private bool useDynamicLocalPlayerSorting = true;
        [SerializeField] private int localPlayerSortingOrderBase;
        [SerializeField] private int localPlayerSortingBucketCount = RoOnlineActorPresenter.DefaultSortingBucketCount;
        [SerializeField] private bool useSpawnCellOverride;
        [SerializeField] private Vector2Int spawnCell = new Vector2Int(100, 100);

        [Header("Input Actions")]
        [SerializeField] private InputActionReference pointerPositionAction;
        [SerializeField] private InputActionReference primaryClickAction;
        [SerializeField] private InputActionReference secondaryClickAction;
        [SerializeField] private InputActionReference headLookModifierAction;
        [SerializeField] private bool createCursorStateController = true;

        [Header("Audio")]
        [SerializeField] private bool enableMapAudio = true;
        [SerializeField] private bool enableMapBgm = true;
        [SerializeField] private bool enableMapAmbientSounds = true;
        [SerializeField] private int maxAmbientSoundEmitters = 64;
        /// <summary>The client's audio settings (one for the whole session: <see cref="RoAudioSystem"/>).</summary>
        private static RoAudioSettings audioSettings => RoAudioSystem.Settings;

        [Header("Effects")]
        [SerializeField] private bool createEffectManager = true;
        [SerializeField] private RoWeatherRuntimeController weatherController;
        [SerializeField, HideInInspector] private RoWeatherWindSettings weatherWind = new RoWeatherWindSettings();
        [SerializeField, HideInInspector] private RoRainWeatherSettings rainWeather = new RoRainWeatherSettings();
        [SerializeField, HideInInspector] private RoDayNightCycleSettings dayNightCycle = new RoDayNightCycleSettings();
        [SerializeField, HideInInspector] private RoCloudShadowSettings cloudShadows = new RoCloudShadowSettings();
        [SerializeField, HideInInspector] private RoAmbientParticleSettings ambientParticles = new RoAmbientParticleSettings();

        private RagnarokContentService content;
        private GameObject currentMap;
        private GND.Mesh compiledGround;
        private RSW.LightInfo currentMapLightInfo;
        private Collider groundCollider;
        private GameObject currentModelRoot;
        private GameObject currentPointLightRoot;
        private GameObject currentModelLampLightRoot;
        private RoGridSelector gridSelector;
        private RoCursorStateController cursorStateController;
        private RoEffectManager effectManager;
        private RoWeatherRuntimeController activeWeatherController;
        private RoOnlineWorldController onlineWorldController;
        private RoEnhancedShadowRuntimeState enhancedShadowRuntime;
        private Transform localPlayer;
        private RoActorLayeredVisual localPlayerLayeredVisual;
        private RoActorSpriteVisual localPlayerSpriteVisual;
        private RoRentalCompanionVisual localPlayerFalconCompanion;
        private RoActorResourceBarOverlay localPlayerResourceBars;
        private RoPointerTargetMarker localPlayerPointerMarker;
        private RoOnlinePointerTargetMarker localPlayerOnlinePointerMarker;
        private RoSpritePointerTargetShape localPlayerPointerShape;
        /// <summary>The local player's aura as last given to the bridge (aid, state key; 0 = none).</summary>
        private uint localPlayerAuraActorId;
        private int localPlayerAuraKey;
        private RoLocalPlayerOcclusionFader localPlayerOcclusionFader;
        /// <summary>The own character's status render state (the world controller's status presenter keeps it).</summary>
        private Ragnarok.Combat.RoActorStatusLook localPlayerStatusLook = Ragnarok.Combat.RoActorStatusLook.Default;
        private bool hasAppliedLocalPlayerStatusLook;
        private Ragnarok.Combat.RoActorStatusLook appliedLocalPlayerStatusLook;
        private RoActorAppearanceRequest localPlayerAppearance;
        private RoOnlineActorSnapshot localPlayerRentalSnapshot;
        private readonly List<Light> enhancedActorTintLights = new List<Light>(32);
        private readonly List<RoMapPortalIndicator> currentMapPortalIndicators = new List<RoMapPortalIndicator>(16);
        private readonly List<RoMapEffectEmitter> currentMapEffectEmitters = new List<RoMapEffectEmitter>(16);
        private UnityEngine.Camera cachedMainCamera;
        private float nextWeatherRuntimeSyncTime;
        private UnityEngine.Camera lastAppliedCameraAntialiasingCamera;
        private bool hasAppliedCameraAntialiasing;
        private RoCameraAntialiasingMode lastAppliedCameraAntialiasingMode = (RoCameraAntialiasingMode)(-1);
        private bool lastAppliedCameraPostProcessingParityDisabled;
        private bool loggedCameraAntialiasingParitySkip;
        private int lastLocalPlayerSortingOrder = int.MinValue;
        private bool hasLocalPlayerResourceStatus;
        private int localPlayerHp;
        private int localPlayerMaxHp;
        private int localPlayerSp;
        private int localPlayerMaxSp;
        private bool hasLocalPlayerBaseLevel;
        private int localPlayerBaseLevel;
        private bool hasLocalPlayerJob;
        private int localPlayerJob;
        private bool hasLocalPlayerOnlineActorState;
        private bool localPlayerOnlineActorDead;
        private bool hasLocalPlayerAttackRange;
        private int localPlayerAttackRange;
        private bool hasAppliedLocalPlayerAppearance;
        private int appliedLocalPlayerAppearanceHash;
        private bool hasLocalPlayerRentalSnapshot;
        private int loadVersion;
        private bool hasNextLocalPlayerSpawnCell;
        private Vector2Int nextLocalPlayerSpawnCell;
        private Task contentPreloadTask;
        private string contentPreloadReport;

        public RagnarokContentService Content => content;
        public Task ContentPreloadTask => contentPreloadTask;
        public GameObject CurrentMap => currentMap;
        public GND.Mesh CompiledGround => compiledGround;
        public Collider GroundCollider => groundCollider;
        public GameObject CurrentPointLightRoot => currentPointLightRoot;
        public GameObject CurrentModelLampLightRoot => currentModelLampLightRoot;
        public IReadOnlyList<Light> EnhancedActorTintLights => enhancedActorTintLights;
        public Transform LocalPlayer => localPlayer;
        public RoGridSelector GridSelector => gridSelector;
        public RoEffectManager Effects => effectManager;
        public RoOnlineWorldController OnlineWorldController => onlineWorldController;
        public string MapName => mapName;
        public bool IsLoadedMapIndoor => IsIndoorMap(mapName);
        public bool IsLoadedMapSkyless => IsSkylessMap(mapName);
        public IReadOnlyList<RoMapPortalIndicator> CurrentMapPortalIndicators => currentMapPortalIndicators;
        public IReadOnlyList<RoMapEffectEmitter> CurrentMapEffectEmitters => currentMapEffectEmitters;
        public RoRuntimeMapRenderSettings RenderSettings => renderSettings;
        public RoShadowRenderMode ShadowRenderMode => renderSettings != null ? renderSettings.shadowMode : RoShadowRenderMode.Original;
        /// <summary>
        /// The render settings' enhanced shadows resolved for their performance tier. Resolving copies the whole settings
        /// object, and actors read this on every sync, so the result is kept until the settings object, its shadow
        /// settings or the tier change (or the inspector edits them: OnValidate).
        /// </summary>
        public RoEnhancedShadowSettings EnhancedShadowSettings {
            get {
                RoEnhancedShadowSettings source = renderSettings?.EnhancedShadows;
                RoLightingPerformanceTier tier = renderSettings != null ? renderSettings.lightingPerformanceTier : RoLightingPerformanceTier.EnhancedBalanced;
                if (resolvedEnhancedShadows == null ||
                    resolvedEnhancedShadowsFrom != renderSettings ||
                    resolvedEnhancedShadowsSource != source ||
                    resolvedEnhancedShadowsTier != tier) {
                    resolvedEnhancedShadows = renderSettings != null
                        ? renderSettings.ResolvedEnhancedShadows
                        : RoLightingPerformanceBudget.Resolve(null, RoLightingPerformanceTier.EnhancedBalanced);
                    resolvedEnhancedShadowsFrom = renderSettings;
                    resolvedEnhancedShadowsSource = source;
                    resolvedEnhancedShadowsTier = tier;
                }

                return resolvedEnhancedShadows;
            }
        }

        private RoEnhancedShadowSettings resolvedEnhancedShadows;
        private RoRuntimeMapRenderSettings resolvedEnhancedShadowsFrom;
        private RoEnhancedShadowSettings resolvedEnhancedShadowsSource;
        private RoLightingPerformanceTier resolvedEnhancedShadowsTier;
        public RoWeatherRuntimeController Weather => activeWeatherController != null ? activeWeatherController : weatherController;
        public RoRainWeatherSettings RainWeatherSettings {
            get {
                RoRainWeatherSettings settings = Weather != null ? Weather.RainSettings : rainWeather ??= new RoRainWeatherSettings();
#if UNITY_EDITOR
                settings.EnsureEditorDefaults();
#else
                settings.EnsureCurrentDefaults();
#endif
                return settings;
            }
        }
        public RoWeatherWindSettings WeatherWindSettings {
            get {
                RoWeatherWindSettings settings = Weather != null ? Weather.WindSettings : weatherWind ??= new RoWeatherWindSettings();
                settings.EnsureCurrentDefaults();
                return settings;
            }
        }
        public RoRainWeatherController RainWeather => Weather != null ? Weather.RainRuntime : null;
        public RoDayNightCycleSettings DayNightCycleSettings {
            get {
                RoDayNightCycleSettings settings = Weather != null ? Weather.DayNightSettings : dayNightCycle ??= new RoDayNightCycleSettings();
                settings.EnsureCurrentDefaults();
                return settings;
            }
        }
        public RoDayNightCycleController DayNightCycle => Weather != null ? Weather.DayNightRuntime : null;
        public RoCloudShadowSettings CloudShadowSettings {
            get {
                RoCloudShadowSettings settings = Weather != null ? Weather.CloudShadowSettings : cloudShadows ??= new RoCloudShadowSettings();
                settings.EnsureCurrentDefaults();
                return settings;
            }
        }
        public RoAmbientParticleSettings AmbientParticleSettings {
            get {
                RoAmbientParticleSettings settings = Weather != null ? Weather.AmbientParticleSettings : ambientParticles ??= new RoAmbientParticleSettings();
                settings.EnsureCurrentDefaults();
                return settings;
            }
        }
        public RoCloudShadowController CloudShadows => Weather != null ? Weather.CloudShadowRuntime : null;

        public void SetCameraAntialiasingMode(
            RoCameraAntialiasingMode mode,
            bool enablePostProcessingForAntialiasing = false
        ) {
            renderSettings ??= new RoRuntimeMapRenderSettings();
            renderSettings.cameraAntialiasing = mode;
            if (mode != RoCameraAntialiasingMode.None && enablePostProcessingForAntialiasing) {
                disableCameraPostProcessingForGravityParity = false;
            }

            UnityEngine.Camera camera = ResolveMainCamera();
            if (camera != null) {
                ApplyGravityParityCameraDefaults(camera);
                ApplyRuntimeCameraAntialiasing(camera, force: true);
            }
        }

        /// <summary>
        /// Applies the server-driven day/night position (from the EFST_SKE-anchored clock) to the
        /// active weather runtime. <paramref name="cycleSeconds"/> is a position in [0, total cycle).
        /// Safe to call every frame; no-ops until the weather runtime exists.
        /// </summary>
        public void ApplyServerDayNightCycle(
            float cycleSeconds,
            float dayMinutes,
            float nightMinutes,
            float transitionFraction
        ) {
            if (ShouldDisableDayNightRuntime()) {
                Weather?.ClearServerDrivenDayNight(restoreLighting: true);
                return;
            }

            RoWeatherRuntimeController weather = Weather;
            if (weather == null) {
                return;
            }

            weather.ApplyServerDrivenDayNight(cycleSeconds, dayMinutes, nightMinutes, transitionFraction);
        }

        /// <summary>Back to the map's own RSW light (no day / night): a map with no sky has no night in RO.</summary>
        public void ClearServerDayNightCycle() {
            Weather?.ClearServerDrivenDayNight(restoreLighting: false);
            // whatever the cycle set while the map was loading (a night sun, a night ambient) goes: the map's own RSW
            // sun and ambient, as the enhanced runtime applies them on load
            if (currentRswLight == null) return;
            RoMapLightingState lighting = RoMapLighting.FromWorld(currentRswLight);
            Light sun = UnityEngine.RenderSettings.sun;
            if (sun != null) {
                Vector3 direction = lighting.Direction.sqrMagnitude > 0.000001f ? lighting.Direction.normalized : Vector3.up;
                sun.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
                sun.color = lighting.Diffuse;
                sun.intensity = 1f;
            }

            UnityEngine.RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            UnityEngine.RenderSettings.ambientLight = lighting.Ambient;
        }

        private void OnValidate() {
            resolvedEnhancedShadows = null;
            weatherWind ??= new RoWeatherWindSettings();
            weatherWind.EnsureCurrentDefaults();

            rainWeather ??= new RoRainWeatherSettings();
#if UNITY_EDITOR
            rainWeather.EnsureEditorDefaults();
#else
            rainWeather.EnsureCurrentDefaults();
#endif

            dayNightCycle ??= new RoDayNightCycleSettings();
            dayNightCycle.EnsureCurrentDefaults();

            cloudShadows ??= new RoCloudShadowSettings();
            cloudShadows.EnsureCurrentDefaults();

            ambientParticles ??= new RoAmbientParticleSettings();
            ambientParticles.EnsureCurrentDefaults();
        }

        private async void Start() {
            if (initializeContentOnStart) {
                BeginContentPreload(logStart: true);
            }

            if (loadOnStart) {
                await LoadMapAsync();
            }
        }

        private void OnDestroy() {
            ++loadVersion;
            ClearCurrentMap();
            RoMapFog.ApplyDisabledGlobals();
            if (content != null && content.IsInitialized) {
                RoSpriteAtlasCache.Clear(content.Assets);
            }

            content?.Dispose();
            content = null;
            contentPreloadTask = null;
            contentPreloadReport = null;
        }

        private void LateUpdate() {
            long runtimeMapLateUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                UpdateLocalPlayerVisualSorting();
                using (WeatherRuntimeSyncMarker.Auto()) {
                    SyncWeatherRuntime();
                }

                EnsureEnhancedShadowRuntimeActive();
                SyncRuntimeCameraAntialiasing();

                using (PendingMapEffectsMarker.Auto()) {
                }
            } finally {
                RoRuntimePerfCounters.AddRuntimeMapLateUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(runtimeMapLateUpdateStartTicks));
            }
        }

        [ContextMenu("Load Map")]
        public void LoadMap() {
            _ = LoadMapAsync();
        }

        public bool TryEnsureContentInitialized(out string report) {
            if (content != null && content.IsInitialized) {
                report = "RO content is already initialized.";
                return true;
            }

            if (contentPreloadTask != null && !contentPreloadTask.IsCompleted) {
                report = "RO content preload is still running.";
                return false;
            }

            if (contentPreloadTask != null && contentPreloadTask.IsFaulted) {
                report = $"Could not initialize RO content: {contentPreloadTask.Exception?.GetBaseException().Message}";
                contentPreloadTask = null;
                return false;
            }

            if (contentPreloadTask == null) {
                BeginContentPreload();
                report = "RO content preload has started.";
                return false;
            }

            report = content != null && content.IsInitialized
                ? "RO content preload completed."
                : "RO content is not initialized.";
            return content != null && content.IsInitialized;
        }

        public bool TryBeginContentPreloadForStartup(out string report) {
            BeginContentPreload();
            report = contentPreloadReport;
            return content != null;
        }

        private void BeginContentPreload(bool logStart = false) {
            if (content != null && content.IsInitialized) {
                contentPreloadReport = "RO content is already initialized.";
                if (logStart && ShouldLogRuntimeMapDiagnostics()) {
                    LogRuntimeMapDiagnostic(contentPreloadReport);
                }

                return;
            }

            if (contentPreloadTask != null && !contentPreloadTask.IsCompleted) {
                if (logStart && !string.IsNullOrEmpty(contentPreloadReport) && ShouldLogRuntimeMapDiagnostics()) {
                    LogRuntimeMapDiagnostic(contentPreloadReport);
                }

                return;
            }

            content?.Dispose();
            content = new RagnarokContentService();
            try {
                if (contentSettings != null) {
                    contentPreloadTask = content.BeginInitializeForStartup(contentSettings);
                    contentPreloadReport = $"Preloading RO content from scene settings '{contentSettings.name}'.";
                    if (logStart && ShouldLogRuntimeMapDiagnostics()) {
                        LogRuntimeMapDiagnostic(contentPreloadReport);
                    }

                    return;
                }

                if (RagnarokContentProfileStore.TryLoadLocalSettings(
                    out RagnarokContentSettings localSettings,
                    out string localSettingsReport)) {
                    contentPreloadTask = content.BeginInitializeForStartup(localSettings);
                    contentPreloadReport = localSettingsReport;
                    if (logStart && ShouldLogRuntimeMapDiagnostics()) {
                        LogRuntimeMapDiagnostic(contentPreloadReport);
                    }

                    return;
                }

                if (useSavedContentProfile &&
                    RagnarokContentProfileStore.TryLoad(out RagnarokContentProfile profile, out string report)) {
                    contentPreloadTask = content.BeginInitializeForStartup(profile);
                    contentPreloadReport = report;
                    if (logStart && ShouldLogRuntimeMapDiagnostics()) {
                        LogRuntimeMapDiagnostic("Preloading saved RO content profile. " + report);
                    }

                    return;
                }
            } catch (Exception ex) {
                contentPreloadTask = null;
                contentPreloadReport = $"Could not initialize RO content: {ex.Message}";
                Debug.LogWarning(contentPreloadReport, this);
                return;
            }

            content?.Dispose();
            content = null;
            contentPreloadTask = null;
            contentPreloadReport = "Assign RagnarokContentSettings or create a saved profile from Ragnarok/Content Setup.";
            if (logStart) {
                Debug.LogWarning(contentPreloadReport, this);
            }
        }

        public async Task LoadMapAsync() {
            await LoadMapAsync(mapName, throwOnError: false);
        }

        public async Task LoadMapAsync(string requestedMapName) {
            await LoadMapAsync(requestedMapName, throwOnError: true);
        }

        public void SetNextLocalPlayerSpawnCell(int x, int y) {
            nextLocalPlayerSpawnCell = new Vector2Int(x, y);
            hasNextLocalPlayerSpawnCell = true;
        }

        public void ClearNextLocalPlayerSpawnCell() {
            hasNextLocalPlayerSpawnCell = false;
            nextLocalPlayerSpawnCell = default;
        }

        public void BindOnlineWorldController(RoOnlineWorldController controller) {
            if (ReferenceEquals(onlineWorldController, controller)) {
                controller?.SetRuntimeMapController(this);
                EnsureLocalPlayerPointerTarget();
                return;
            }

            onlineWorldController?.ClearRuntimeMapController(this, gridSelector, effectManager);
            onlineWorldController = controller;
            onlineWorldController?.SetRuntimeMapController(this);
            EnsureLocalPlayerPointerTarget();
        }

        private async Task LoadMapAsync(string requestedMapName, bool throwOnError) {
            int version = ++loadVersion;
            string resolvedMapName = NormalizeMapResourceName(requestedMapName);
            long totalStart = RoMapTiming.Timestamp();
            long contentMilliseconds = 0;
            long skinMilliseconds = 0;
            long dataMilliseconds = 0;
            RoMapDataLoadTiming dataTiming = new RoMapDataLoadTiming();
            RoMapShowTiming showTiming = null;
            RoRuntimeMapAssetCacheStats cacheBefore = default;
            RoRuntimeMapAssetCacheStats cacheAfter = default;
            RoModelRendererCacheStats modelCacheBefore = default;
            RoModelRendererCacheStats modelCacheAfter = default;
            try {
                long phaseStart = RoMapTiming.Timestamp();
                (bool contentReady, string contentReport) = await TryEnsureContentAsync();
                contentMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
                if (!contentReady) {
                    if (throwOnError) {
                        throw new InvalidOperationException(contentReport);
                    }

                    Debug.LogError(contentReport, this);
                    return;
                }

                cacheBefore = RoRuntimeMapAssetCache.SnapshotStats(content.Assets);
                modelCacheBefore = RoModelRenderer.SnapshotCacheStats();

                phaseStart = RoMapTiming.Timestamp();
                skinMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

                var loader = new RoMapDataLoader(content.Assets);
                phaseStart = RoMapTiming.Timestamp();
                AsyncMapLoader.GameMapData mapData = await loader.Load(resolvedMapName, dataTiming);
                dataMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

                if (version != loadVersion) {
                    if (throwOnError) {
                        throw new InvalidOperationException($"Runtime RO map load for '{resolvedMapName}' was superseded.");
                    }

                    return;
                }

                mapName = resolvedMapName;
                RoMapNightLight.IndoorMap = IsIndoorMap(resolvedMapName);
                showTiming = ShowMap(mapData);
                RoClassicMapBanner.ShowForMap(content.Assets, resolvedMapName);
                cacheAfter = RoRuntimeMapAssetCache.SnapshotStats(content.Assets);
                modelCacheAfter = RoModelRenderer.SnapshotCacheStats();
                LogMapLoadTiming(
                    resolvedMapName,
                    RoMapTiming.ElapsedMilliseconds(totalStart),
                    contentMilliseconds,
                    skinMilliseconds,
                    dataMilliseconds,
                    dataTiming,
                    showTiming,
                    cacheBefore,
                    cacheAfter,
                    modelCacheBefore,
                    modelCacheAfter);
                LogMapCacheStats(resolvedMapName);
            } catch (Exception ex) {
                Debug.LogError($"Could not load runtime RO map '{resolvedMapName}': {ex}", this);
                if (throwOnError) {
                    throw;
                }
            }
        }

        public static string NormalizeMapResourceName(string requestedMapName) {
            if (string.IsNullOrWhiteSpace(requestedMapName)) {
                return "prontera.rsw";
            }

            string normalized = requestedMapName.Trim().Replace('\\', '/');
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0) {
                normalized = normalized.Substring(slashIndex + 1);
            }

            if (normalized.EndsWith(".gat", StringComparison.OrdinalIgnoreCase)) {
                return normalized.Substring(0, normalized.Length - 4) + ".rsw";
            }

            if (normalized.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase)) {
                return normalized;
            }

            return normalized + ".rsw";
        }

        private bool TryEnsureContent(out string report) {
            if (content != null && content.IsInitialized) {
                report = "RO content is already initialized.";
                return true;
            }

            content?.Dispose();
            content = new RagnarokContentService();

            try {
                if (contentSettings != null) {
                    content.Initialize(contentSettings);
                    report = $"Loaded RO content from scene settings '{contentSettings.name}'.";
                    return true;
                }

                if (RagnarokContentProfileStore.TryLoadLocalSettings(
                    out RagnarokContentSettings localSettings,
                    out string localSettingsReport)) {
                    content.Initialize(localSettings);
                    report = localSettingsReport;
                    return true;
                }

                if (useSavedContentProfile && content.TryInitializeSavedProfile(out report)) {
                    return true;
                }
            } catch (Exception ex) {
                report = $"Could not initialize RO content: {ex.Message}";
                return false;
            }

            report = "Assign RagnarokContentSettings or create a saved profile from Ragnarok/Content Setup.";
            return false;
        }

        private async Task<(bool Success, string Report)> TryEnsureContentAsync() {
            if (content != null && content.IsInitialized) {
                return (true, "RO content is already initialized.");
            }

            if (contentPreloadTask == null) {
                BeginContentPreload();
            }

            if (contentPreloadTask != null) {
                try {
                    await contentPreloadTask;
                } catch (Exception ex) {
                    string message = $"Could not initialize RO content: {ex.GetBaseException().Message}";
                    contentPreloadTask = null;
                    return (false, message);
                }

                if (content != null && content.IsInitialized) {
                    return (true, string.IsNullOrEmpty(contentPreloadReport)
                        ? "RO content preload completed."
                        : contentPreloadReport);
                }
            }

            return TryEnsureContent(out string report)
                ? (true, report)
                : (false, report);
        }

        private void LogMapLoadTiming(
            string resolvedMapName,
            long totalMilliseconds,
            long contentMilliseconds,
            long skinMilliseconds,
            long dataMilliseconds,
            RoMapDataLoadTiming dataTiming,
            RoMapShowTiming showTiming,
            RoRuntimeMapAssetCacheStats cacheBefore,
            RoRuntimeMapAssetCacheStats cacheAfter,
            RoModelRendererCacheStats modelCacheBefore,
            RoModelRendererCacheStats modelCacheAfter
        ) {
            // one line per map load (Editor / development build, or a slow load anywhere), without the verbose diagnostics:
            // the map change hitch is measured from it
            if (!RoMapTiming.ShouldLog(totalMilliseconds)) {
                return;
            }

            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log(
                $"RO map load timings: map={resolvedMapName}, total={totalMilliseconds} ms, " +
                $"content={contentMilliseconds} ms, skin={skinMilliseconds} ms, " +
                $"data={dataMilliseconds} ms ({dataTiming?.BuildSummary() ?? "n/a"}), " +
                $"show={showTiming?.BuildSummary() ?? "n/a"}, " +
                $"cacheDelta=[{RoMapCacheTimingFormatter.FormatDelta(cacheBefore, cacheAfter)}], " +
                $"rendererCacheDelta=[{RoMapCacheTimingFormatter.FormatModelMeshDelta(modelCacheBefore, modelCacheAfter)}].",
                this);
        }

        private void LogMapCacheStats(string resolvedMapName) {
            if (!ShouldLogRuntimeMapDiagnostics() || content == null || !content.IsInitialized) {
                return;
            }

            RoRuntimeMapAssetCacheStats stats = RoRuntimeMapAssetCache.SnapshotStats(content.Assets);
            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log(
                $"RO runtime map cache: map={resolvedMapName}, " +
                $"mapAssets {stats.MapAssetHits}/{stats.MapAssetMisses} hit/miss, " +
                $"ground {stats.CompiledGroundHits}/{stats.CompiledGroundMisses} hit/miss, " +
                $"groundMesh {stats.GroundMeshHits}/{stats.GroundMeshMisses} hit/miss, " +
                $"rawRsm {stats.RawModelHits}/{stats.RawModelMisses} hit/miss, " +
                $"compiledRsm {stats.CompiledModelHits}/{stats.CompiledModelMisses} hit/miss, " +
                $"modelBuilds={stats.CompiledModelBuilds} ({stats.CompiledModelBuildMilliseconds} ms), " +
                $"groundAtlas {stats.GroundAtlasHits}/{stats.GroundAtlasMisses} hit/miss, " +
                $"waterMesh {stats.WaterMeshHits}/{stats.WaterMeshMisses} hit/miss, " +
                $"water {stats.WaterHits}/{stats.WaterMisses} hit/miss, entries={stats.Entries}.",
                this);
        }

        /// <summary>The loaded map's GND texture names (the HD pack export reads them).</summary>
        public string[] CurrentGroundTextures { get; private set; }

        private ROIO.Models.FileTypes.RSW.LightInfo currentRswLight;

        private RoMapShowTiming ShowMap(AsyncMapLoader.GameMapData mapData) {
            CurrentGroundTextures = mapData.CompiledGround?.textures;
            currentRswLight = mapData.World?.light;
            long totalStart = RoMapTiming.Timestamp();
            var timing = new RoMapShowTiming();
            long phaseStart = RoMapTiming.Timestamp();
            ClearCurrentMap();
            if (Ragnarok.Assets.RoHdTextures.CachesStale) {
                // the HD pack changed: nothing draws with the cached textures any more, rebuild them in the new state
                Ragnarok.Assets.RoHdTextures.CachesStale = false;
                RoRuntimeMapAssetCache.ClearAll();
                RoModelRenderer.ClearCaches();
            }

            timing.ClearMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            RoPointerTargetingMode pointerMode = renderSettings != null
                ? renderSettings.pointerTargetingMode
                : RoPointerTargetingMode.ColliderFirst;
            if (renderSettings != null &&
                renderSettings.lightingPerformanceTier == RoLightingPerformanceTier.ClassicFast &&
                pointerMode == RoPointerTargetingMode.AlphaExact) {
                pointerMode = RoPointerTargetingMode.ColliderOnlyFast;
            }

            RoSpritePointerTargetShape.GlobalTargetingMode = pointerMode;
            RoSpritePointerTargetShape.GlobalAlphaPerfectTargetingEnabled = pointerMode == RoPointerTargetingMode.AlphaExact;
            RoSpriteFrameGeometryCache.PrewarmEnabled = renderSettings == null || renderSettings.enableSpriteGeometryPrewarm;
            RoSpriteFrameGeometryCache.MaxPrewarmBuildsPerFrame = renderSettings != null
                ? Mathf.Max(1, renderSettings.spriteGeometryPrewarmPerFrame)
                : 1;

            phaseStart = RoMapTiming.Timestamp();
            RoRuntimeMapRenderResult renderResult = RoRuntimeMapRenderer.Build(transform, mapData, content.Assets, renderSettings, this);
            timing.RenderMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            timing.RenderTiming = renderResult.Timing;
            currentMap = renderResult.Root;
            currentModelRoot = renderResult.Models;
            CacheEnhancedActorTintLights(renderResult.PointLights, renderResult.ModelLampLights);
            compiledGround = mapData.CompiledGround;
            currentMapLightInfo = mapData.World?.light;
            groundCollider = renderResult.GroundCollider;

            if (enableMapAudio) {
                phaseStart = RoMapTiming.Timestamp();
                CreateMapAudio(mapData);
                timing.AudioMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            }

            if (createGridSelector) {
                phaseStart = RoMapTiming.Timestamp();
                CreateGridSelector(mapData.Altitude);
                timing.GridMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            }

            if (createCursorStateController) {
                phaseStart = RoMapTiming.Timestamp();
                CreateCursorStateController();
                timing.CursorMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            }

            if (createLocalPlayer) {
                phaseStart = RoMapTiming.Timestamp();
                CreateLocalPlayer(renderResult.GroundMesh.bounds, mapData.Altitude);
                timing.PlayerMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            }

            UnityEngine.Camera camera = null;
            phaseStart = RoMapTiming.Timestamp();
            if (configureCamera) {
                camera = ConfigureCamera(renderResult.RenderedGroundLocalBounds, mapData.Name);
            } else {
                camera = UnityEngine.Camera.main;
            }
            timing.CameraMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            phaseStart = RoMapTiming.Timestamp();
            ApplyRuntimeShadowMode(mapData, camera);
            timing.ShadowMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            cursorStateController?.SetRenderCamera(camera);
            SyncLocalPlayerOcclusionFader(camera);
            phaseStart = RoMapTiming.Timestamp();
            ApplyGravityParityCameraDefaults(camera);
            ApplyRuntimeCameraAntialiasing(camera, force: true);
            timing.CameraDefaultsMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            phaseStart = RoMapTiming.Timestamp();
            ApplyClassicRoFog(mapData.Name);
            timing.FogMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            if (showSkyAndClouds && camera != null && !IsIndoorMap(mapData.Name)) {
                phaseStart = RoMapTiming.Timestamp();
                CreateSky(mapData.Name, camera);
                timing.SkyMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            }

            if (createEffectManager) {
                phaseStart = RoMapTiming.Timestamp();
                CreateEffectManager(camera);
                effectManager?.ConfigureClassicMap(currentMap.transform, mapData.Altitude);
                timing.EffectsMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            }

            phaseStart = RoMapTiming.Timestamp();
            CreateWeatherRuntime(renderResult.Ground, renderResult.Models, camera);
            timing.WeatherMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            ApplyMapMaterialDiagnosticOverrides(renderResult);

            LogLightingDiagnosticsIfRequested(mapData, renderResult, camera);

            phaseStart = RoMapTiming.Timestamp();
            CreateMapPortalIndicators(mapData);
            timing.PortalsMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            phaseStart = RoMapTiming.Timestamp();
            ScheduleMapEffects(mapData);
            timing.MapEffectsMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            timing.TotalMilliseconds = RoMapTiming.ElapsedMilliseconds(totalStart);
            return timing;
        }

        private void ClearCurrentMap() {
            ClearWeatherRuntime();
            ClearEnhancedShadowRuntime();
            onlineWorldController?.ClearRuntimeMapController(this, gridSelector, effectManager);
            DestroyLocalRentalCompanion(ref localPlayerFalconCompanion);
            ClearLocalPlayerMaxLevelAura();
            ClearLocalPlayerOcclusionFader();

            if (RoAudioSystem.ListenerAnchor == localPlayer) {
                RoAudioSystem.ListenerAnchor = null;
            }

            localPlayer = null;
            currentMapLightInfo = null;
            localPlayerLayeredVisual = null;
            localPlayerSpriteVisual = null;
            hasAppliedLocalPlayerStatusLook = false;
            hasLocalPlayerOnlineActorState = false;
            localPlayerOnlineActorDead = false;
            localPlayerResourceBars = null;
            localPlayerPointerMarker = null;
            localPlayerOnlinePointerMarker = null;
            localPlayerPointerShape = null;
            currentModelRoot = null;
            currentPointLightRoot = null;
            currentModelLampLightRoot = null;
            gridSelector = null;
            cursorStateController = null;
            effectManager = null;
            activeWeatherController = null;
            compiledGround = null;
            groundCollider = null;
            enhancedActorTintLights.Clear();
            currentMapPortalIndicators.Clear();
            currentMapEffectEmitters.Clear();
            hasAppliedLocalPlayerAppearance = false;
            appliedLocalPlayerAppearanceHash = 0;
            hasLocalPlayerRentalSnapshot = false;
            localPlayerRentalSnapshot = default;

            if (currentMap == null) {
                return;
            }

            DestroyRuntimeObject(currentMap);
            currentMap = null;
        }

        private void ApplyRuntimeShadowMode(AsyncMapLoader.GameMapData mapData, UnityEngine.Camera camera) {
            ClearEnhancedShadowRuntime();
            if (ShadowRenderMode != RoShadowRenderMode.Enhanced) {
                return;
            }

            enhancedShadowRuntime = RoEnhancedShadowRuntime.Apply(
                mapData.CompiledGround,
                mapData.World?.light,
                EnhancedShadowSettings,
                ResolveEnhancedShadowReceiverDistance(camera),
                this);
        }

        private void EnsureEnhancedShadowRuntimeActive() {
            if (ShadowRenderMode != RoShadowRenderMode.Enhanced || compiledGround == null) {
                return;
            }

            if (enhancedShadowRuntime != null && enhancedShadowRuntime.EnsureActive()) {
                return;
            }

            UnityEngine.Camera camera = ResolveMainCamera();
            ClearEnhancedShadowRuntime();
            enhancedShadowRuntime = RoEnhancedShadowRuntime.Apply(
                compiledGround,
                currentMapLightInfo,
                EnhancedShadowSettings,
                ResolveEnhancedShadowReceiverDistance(camera),
                this);
        }

        private static float ResolveEnhancedShadowReceiverDistance(UnityEngine.Camera camera) {
            if (camera == null) {
                return 0f;
            }

            RoCameraController controller = camera.FindComponent<RoCameraController>();
            return controller != null ? controller.MaxDistance : 0f;
        }

        private void ClearEnhancedShadowRuntime() {
            enhancedShadowRuntime?.Dispose();
            enhancedShadowRuntime = null;
        }

        private void ApplyMapMaterialDiagnosticOverrides(RoRuntimeMapRenderResult renderResult) {
            bool textureOnly = ShouldForceTextureOnlyMap();
            bool disableRealtimeShadowReceive = textureOnly || ShouldDisableRealtimeShadowReceive();
            bool disableRswLighting = textureOnly || ShouldDisableRswLighting();
            bool disableMapLightmap = textureOnly || ShouldDisableMapLightmap();
            bool disableMapTint = textureOnly || ShouldDisableMapTint();
            bool debugRealtimeShadows = ShouldDebugRealtimeShadows();
            if (!disableRealtimeShadowReceive &&
                !disableRswLighting &&
                !disableMapLightmap &&
                !disableMapTint &&
                !debugRealtimeShadows) {
                return;
            }

            int renderers = 0;
            int materials = 0;
            ApplyMapMaterialDiagnosticOverrides(
                renderResult?.Ground,
                disableRealtimeShadowReceive,
                disableRswLighting,
                disableMapLightmap,
                disableMapTint,
                debugRealtimeShadows,
                ref renderers,
                ref materials);
            ApplyMapMaterialDiagnosticOverrides(
                renderResult?.Models,
                disableRealtimeShadowReceive,
                disableRswLighting,
                disableMapLightmap,
                disableMapTint,
                debugRealtimeShadows,
                ref renderers,
                ref materials);
            ApplyMapMaterialDiagnosticOverrides(
                localPlayer != null ? localPlayer.gameObject : null,
                disableRealtimeShadowReceive,
                disableRswLighting,
                disableMapLightmap,
                disableMapTint,
                debugRealtimeShadows,
                ref renderers,
                ref materials);

            if (ShouldLogLightingDiagnostics()) {
                RoRuntimeDiagnostics.IncrementDebugLogLine();
                Debug.Log(
                    "[RoLightingDiagnostics] map material diagnostic override: " +
                    $"textureOnly={textureOnly} disableRealtimeShadowReceive={disableRealtimeShadowReceive} " +
                    $"disableRswLighting={disableRswLighting} disableMapLightmap={disableMapLightmap} disableMapTint={disableMapTint} " +
                    $"debugRealtimeShadows={debugRealtimeShadows} " +
                    $"renderers={renderers} materials={materials}",
                    this);
            }
        }

        private static void ApplyMapMaterialDiagnosticOverrides(
            GameObject root,
            bool disableRealtimeShadowReceive,
            bool disableRswLighting,
            bool disableMapLightmap,
            bool disableMapTint,
            bool debugRealtimeShadows,
            ref int renderers,
            ref int materials
        ) {
            if (root == null) {
                return;
            }

            LightingDiagnosticsRendererScratch.Clear();
            root.GetComponentsInChildren(true, LightingDiagnosticsRendererScratch);
            for (int i = 0; i < LightingDiagnosticsRendererScratch.Count; i++) {
                MeshRenderer renderer = LightingDiagnosticsRendererScratch[i];
                if (renderer == null) {
                    continue;
                }

                renderers++;
                Material material = renderer.sharedMaterial;
                if (material == null) {
                    continue;
                }

                bool changed = false;
                if (disableRealtimeShadowReceive && material.HasProperty("_RoReceiveRealtimeShadows")) {
                    material.SetFloat("_RoReceiveRealtimeShadows", 0f);
                    changed = true;
                }

                if (disableRswLighting) {
                    if (material.HasProperty("_RoUseLighting")) {
                        material.SetFloat("_RoUseLighting", 0f);
                        changed = true;
                    }

                    if (material.HasProperty("_RoLightingStrength")) {
                        material.SetFloat("_RoLightingStrength", 0f);
                        changed = true;
                    }
                }

                if (disableMapLightmap && material.HasProperty("_UseLightmap")) {
                    material.SetFloat("_UseLightmap", 0f);
                    changed = true;
                }

                if (disableMapTint && material.HasProperty("_UseTint")) {
                    material.SetFloat("_UseTint", 0f);
                    changed = true;
                }

                if (debugRealtimeShadows && material.HasProperty("_RoDebugRealtimeShadows")) {
                    material.SetFloat("_RoDebugRealtimeShadows", 1f);
                    changed = true;
                }

                if (changed) {
                    materials++;
                }
            }

            LightingDiagnosticsRendererScratch.Clear();
        }

        private void CacheEnhancedActorTintLights(GameObject pointLightRoot, GameObject modelLampLightRoot) {
            enhancedActorTintLights.Clear();
            currentPointLightRoot = pointLightRoot;
            currentModelLampLightRoot = modelLampLightRoot;
            if (ShadowRenderMode != RoShadowRenderMode.Enhanced) {
                return;
            }

            AddEnhancedActorTintLights(pointLightRoot);
            AddEnhancedActorTintLights(modelLampLightRoot);
        }

        private void AddEnhancedActorTintLights(GameObject root) {
            if (root == null) {
                return;
            }

            root.GetComponentsInChildren(true, LightTraversalScratch);
            for (int i = 0; i < LightTraversalScratch.Count; i++) {
                Light light = LightTraversalScratch[i];
                if (light != null && light.type == LightType.Point && !enhancedActorTintLights.Contains(light)) {
                    enhancedActorTintLights.Add(light);
                }
            }

            LightTraversalScratch.Clear();
        }

        private void CreateMapAudio(AsyncMapLoader.GameMapData mapData) {
            var audioRoot = new GameObject("RO Map Audio");
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

        private void CreateEffectManager(UnityEngine.Camera camera) {
            var effectObject = new GameObject("RO Effects");
            effectObject.transform.SetParent(currentMap.transform, false);
            effectManager = effectObject.AddComponent<RoEffectManager>();
            effectManager.SetCrowdBudgetMode(renderSettings != null ? renderSettings.effectCrowdBudgetMode : RoEffectCrowdBudgetMode.Balanced);
            effectManager.Initialize(content != null ? content.Assets : null, camera);
            WarmEffectPools(effectManager);

            RoOnlineWorldController onlineWorld = onlineWorldController != null
                ? onlineWorldController
                : FindFirstObjectByType<RoOnlineWorldController>();
            if (onlineWorld != null) {
                BindOnlineWorldController(onlineWorld);
            }
        }

        private void WarmEffectPools(RoEffectManager manager) {
            if (manager == null) {
                return;
            }

            RoRuntimeMapRenderSettings settings = renderSettings;
            if (settings != null && !settings.enableEffectPoolPrewarm) {
                return;
            }

            manager.WarmActorOverlayPool(settings != null ? settings.prewarmActorOverlayEffects : 4);
            manager.WarmWeatherLayerPool(settings != null ? settings.prewarmWeatherLayerEffects : 1);
        }

        private void CreateMapPortalIndicators(AsyncMapLoader.GameMapData mapData) {
            currentMapPortalIndicators.Clear();
            // the records stay as minimap / click hints; what they draw is the classic engine's (ScheduleMapEffects)
            RoMapPortalIndicator[] indicators = RoMapPortalEffectPlanner.Build(mapData.World);
            currentMapPortalIndicators.AddRange(indicators);
        }

        private void ScheduleMapEffects(AsyncMapLoader.GameMapData mapData) {
            currentMapEffectEmitters.Clear();
            RoMapEffectEmitter[] smokeEmitters = RoMapEffectPlanner.BuildSmokeEmitters(mapData.World, mapData.CompiledGround);
            currentMapEffectEmitters.AddRange(smokeEmitters);

            // every RSW effect record (fires, torches, waterfalls, chimney smoke ...) runs in the original's engine:
            // CEffectMaker emitters and the new-system 974 / 44 effects (reference packages/mapfx)
            effectManager?.SetClassicMapEffects(mapData.Name, mapData.World);
        }



#if UNITY_EDITOR
        public void SetCurrentMapPortalIndicatorsForTests(params RoMapPortalIndicator[] indicators) {
            currentMapPortalIndicators.Clear();
            if (indicators != null) {
                currentMapPortalIndicators.AddRange(indicators);
            }
        }

        public void SetCurrentMapEffectEmittersForTests(params RoMapEffectEmitter[] emitters) {
            currentMapEffectEmitters.Clear();
            if (emitters != null) {
                currentMapEffectEmitters.AddRange(emitters);
            }
        }

        public void SetEffectManagerForTests(RoEffectManager manager) {
            effectManager = manager;
            onlineWorldController?.SetRuntimeMapController(this);
        }

        public void ClearCurrentMapForTests() {
            ClearCurrentMap();
        }
#endif

        private void CreateGridSelector(GAT altitude) {
            var selectorObject = new GameObject("RO Grid Selector");
            selectorObject.transform.SetParent(currentMap.transform, false);
            gridSelector = selectorObject.AddComponent<RoGridSelector>();
            gridSelector.Initialize(UnityEngine.Camera.main, groundCollider, altitude, content.Assets);
            gridSelector.SetPointerPositionAction(pointerPositionAction);
            onlineWorldController?.SetRuntimeMapController(this);
        }

        private void CreateLocalPlayer(Bounds mapBounds, GAT altitude) {
            var playerObject = new GameObject("Local Player");
            playerObject.transform.SetParent(currentMap.transform, false);
            playerObject.transform.localPosition = GetInitialPlayerPosition(mapBounds, altitude);
            playerObject.AddComponent<RoPlayerPlaceholder>();

            RoActorAppearanceRequest appearance = localPlayerAppearance ?? RoActorAppearanceRequest.CreateDefaultPlayerProbe();
            bool createdVisual = useLayeredPlayerVisual
                ? TryCreateLayeredPlayerVisual(playerObject.transform, appearance)
                : TryCreateSinglePlayerVisual(playerObject.transform);
            if (!createdVisual) {
                CreateDebugCapsule(playerObject.transform);
            } else {
                hasAppliedLocalPlayerAppearance = true;
                appliedLocalPlayerAppearanceHash = CalculateLocalPlayerAppearanceHash(appearance);
            }

            ApplyLocalPlayerShadowRenderMode(playerObject.transform);
            if (RoEnhancedShadowRuntime.ShouldUseFakeActorShadow(ShadowRenderMode, showPlayerShadow)) {
                CreatePlayerShadow(playerObject.transform);
            }

            var clickToMove = playerObject.AddComponent<RoClickToMoveController>();
            clickToMove.Initialize(UnityEngine.Camera.main, groundCollider);
            clickToMove.SetGridSelector(gridSelector);
            clickToMove.SetInputActions(pointerPositionAction, primaryClickAction, headLookModifierAction, secondaryClickAction);
            clickToMove.SetCursorStateController(cursorStateController);
            clickToMove.SetOnlineWorldController(ResolveOnlineWorldController());
            clickToMove.SetLoginShell(FindFirstObjectByType<RoMinimalLoginShell>());
            if (hasLocalPlayerAttackRange) {
                clickToMove.SetAttackRangeCells(localPlayerAttackRange);
            }

            var layeredVisual = playerObject.GetComponentInChildren<RoActorLayeredVisual>();
            if (layeredVisual != null) {
                clickToMove.SetActorVisual(layeredVisual);
            } else {
                clickToMove.SetActorVisual(playerObject.GetComponentInChildren<RoActorSpriteVisual>());
            }

            localPlayer = playerObject.transform;
            RoAudioSystem.ListenerAnchor = localPlayer;
            RoActorEffectAnchors.EnsureOn(playerObject);
            localPlayerResourceBars = playerObject.AddComponent<RoActorResourceBarOverlay>();
            localPlayerResourceBars.Initialize(UnityEngine.Camera.main);
            if (hasLocalPlayerResourceStatus) {
                localPlayerResourceBars.SetValues(localPlayerHp, localPlayerMaxHp, localPlayerSp, localPlayerMaxSp);
            }

            CacheLocalPlayerVisuals(localPlayer);
            EnsureLocalPlayerPointerTarget();
            EnsureLocalPlayerMapLightProbe(localPlayer);
            UpdateLocalPlayerVisualSorting();
            SyncLocalPlayerMaxLevelAura();
            SyncLocalPlayerOcclusionFader(ResolveMainCamera());
            if (hasLocalPlayerRentalSnapshot) {
                SyncLocalRentalAttachments(localPlayerRentalSnapshot);
            }
        }

        private void CreateCursorStateController() {
            var cursorObject = new GameObject("RO Cursor State");
            cursorObject.transform.SetParent(currentMap.transform, false);
            cursorStateController = cursorObject.AddComponent<RoCursorStateController>();
            cursorStateController.Initialize(content != null ? content.Assets : null, UnityEngine.Camera.main, pointerPositionAction);
        }

        private bool TryCreateLayeredPlayerVisual(Transform parent, RoActorAppearanceRequest appearance = null) {
            var visualObject = new GameObject("RO Actor Layered Visual");
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = Vector3.up * RoActorSpriteVisual.GroundedVisualYOffset;

            var visual = visualObject.AddComponent<RoActorLayeredVisual>();
            try {
                visual.SetShadowRenderMode(ShadowRenderMode, EnhancedShadowSettings);
                appearance ??= RoActorAppearanceRequest.CreateDefaultPlayerProbe();
                if (visual.Initialize(content.Assets, appearance)) {
                    if (ShouldLogRuntimeMapDiagnostics()) {
                        LogRuntimeMapDiagnostic($"Created runtime layered player with {visual.LayerCount} layer(s).");
                    }
                    return true;
                }
            } catch (Exception ex) {
                DestroyRuntimeObject(visualObject);
                Debug.LogWarning($"Could not create runtime layered player: {ex.Message}", this);
                return false;
            }

            DestroyRuntimeObject(visualObject);
            return false;
        }

        private bool TryCreateSinglePlayerVisual(Transform parent) {
            if (!RoActorSpritePathResolver.TryFindFirstPlayerSpritePair(content.Assets, out string basePath)) {
                return false;
            }

            var visualObject = new GameObject($"RO Actor Sprite Visual - {basePath}");
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = Vector3.up * RoActorSpriteVisual.GroundedVisualYOffset;

            var visual = visualObject.AddComponent<RoActorSpriteVisual>();
            try {
                visual.SetShadowRenderMode(ShadowRenderMode, EnhancedShadowSettings);
                string palettePath = RoActorSpritePathResolver.TryFindPreferredPlayerBodyPalette(content.Assets, out string resolved)
                    ? resolved
                    : null;
                if (visual.Initialize(content.Assets, basePath, palettePath)) {
                    if (ShouldLogRuntimeMapDiagnostics()) {
                        LogRuntimeMapDiagnostic($"Created runtime player sprite from '{basePath}.spr/.act'.");
                    }
                    return true;
                }
            } catch (Exception ex) {
                DestroyRuntimeObject(visualObject);
                Debug.LogWarning($"Could not create runtime player sprite from '{basePath}.spr/.act': {ex.Message}", this);
                return false;
            }

            DestroyRuntimeObject(visualObject);
            return false;
        }

        private void CreatePlayerShadow(Transform parent) {
            var shadowObject = new GameObject("RO Actor Shadow");
            shadowObject.transform.SetParent(parent, false);
            var shadow = shadowObject.AddComponent<RoActorShadowVisual>();
            shadow.Initialize(content != null ? content.Assets : null, ActorGroundOffset);
        }

        private static void CreateDebugCapsule(Transform parent) {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Player Debug Capsule";
            capsule.transform.SetParent(parent, false);
            capsule.transform.localPosition = Vector3.up * 1.8f;
            capsule.transform.localScale = new Vector3(1.3f, 1.8f, 1.3f);
        }

        private Vector3 GetInitialPlayerPosition(Bounds mapBounds, GAT altitude) {
            if (TryGetNextLocalPlayerSpawn(altitude, out Vector3 onlineSpawn)) {
                return onlineSpawn;
            }

            if (TryGetSpawnOverride(altitude, out Vector3 overrideSpawn)) {
                return overrideSpawn;
            }

            if (TryGetRenderedGroundSpawn(out Vector3 renderedGroundSpawn)) {
                return renderedGroundSpawn;
            }

            if (altitude != null && altitude.cells != null) {
                var center = new Vector2Int((int) altitude.width / 2, (int) altitude.height / 2);
                int radius = Mathf.Max((int) altitude.width, (int) altitude.height);
                if (RoGridPathfinder.TryFindNearestWalkable(altitude, center, radius, out Vector2Int resolvedCell)) {
                    return RoGridPathfinder.CellToLocalCenter(altitude, resolvedCell, ActorGroundOffset);
                }
            }

            return new Vector3(mapBounds.center.x, -mapBounds.center.y + ActorGroundOffset, mapBounds.center.z);
        }

        private bool TryGetNextLocalPlayerSpawn(GAT altitude, out Vector3 position) {
            position = default;
            if (!hasNextLocalPlayerSpawnCell) {
                return false;
            }

            Vector2Int cell = nextLocalPlayerSpawnCell;
            ClearNextLocalPlayerSpawnCell();
            if (altitude == null || altitude.cells == null) {
                return false;
            }

            if (!RoGridPathfinder.IsWalkable(altitude, cell.x, cell.y) &&
                !RoGridPathfinder.TryFindNearestWalkable(altitude, cell, 16, out cell)) {
                return false;
            }

            position = RoGridPathfinder.CellToLocalCenter(altitude, cell, ActorGroundOffset);
            return true;
        }

        private bool TryGetSpawnOverride(GAT altitude, out Vector3 position) {
            position = default;
            if (!useSpawnCellOverride || altitude == null || altitude.cells == null) {
                return false;
            }

            Vector2Int resolvedCell = spawnCell;
            if (!RoGridPathfinder.IsWalkable(altitude, resolvedCell.x, resolvedCell.y) &&
                !RoGridPathfinder.TryFindNearestWalkable(altitude, resolvedCell, 16, out resolvedCell)) {
                return false;
            }

            position = RoGridPathfinder.CellToLocalCenter(altitude, resolvedCell, ActorGroundOffset);
            return true;
        }

        private bool TryGetRenderedGroundSpawn(out Vector3 position) {
            position = default;
            if (groundCollider == null || currentMap == null) {
                return false;
            }

            Bounds bounds = groundCollider.bounds;
            Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + 100f, bounds.center.z);
            var ray = new Ray(origin, Vector3.down);
            if (!groundCollider.Raycast(ray, out RaycastHit hit, bounds.size.y + 250f)) {
                return false;
            }

            position = currentMap.transform.InverseTransformPoint(hit.point) + Vector3.up * ActorGroundOffset;
            return true;
        }

        public bool TryMoveLocalPlayerToCell(int x, int y) {
            return TryMoveLocalPlayerToCell(x, y, null, 0);
        }

        public bool TryMoveLocalPlayerToCell(
            int x,
            int y,
            RoActorDirection? facing,
            uint placementServerTime = 0
        ) {
            if (localPlayer == null || gridSelector == null) {
                return false;
            }

            var cell = new Vector2Int(x, y);
            if (!gridSelector.IsCellWalkable(cell)) {
                if (gridSelector.Altitude == null ||
                    !RoGridPathfinder.TryFindNearestWalkable(gridSelector.Altitude, cell, 16, out cell)) {
                    return false;
                }
            }

            Vector3 worldPosition = gridSelector.GetCellWorldCenter(cell, ActorGroundOffset);
            if (localPlayer.TryGetComponent(out RoClickToMoveController clickToMove)) {
                clickToMove.SnapToAuthoritativeWorldPosition(worldPosition, facing, placementServerTime);
            } else {
                localPlayer.position = worldPosition;
            }

            return true;
        }

        public bool TryApplyLocalPlayerStopCell(
            int x,
            int y,
            RoActorDirection? facing,
            uint placementServerTime = 0,
            bool keepAction = false
        ) {
            if (localPlayer == null) {
                return false;
            }

            var cell = new Vector2Int(x, y);
            if (localPlayer.TryGetComponent(out RoClickToMoveController clickToMove)) {
                if (keepAction) {
                    clickToMove.ApplyAuthoritativeStopCellKeepingAction(cell);
                } else {
                    clickToMove.ApplyAuthoritativeStopCell(cell, facing, placementServerTime);
                }
            } else if (gridSelector != null && gridSelector.IsCellAvailable(cell)) {
                localPlayer.position = gridSelector.GetCellWorldCenter(cell, ActorGroundOffset);
            } else {
                localPlayer.position = new Vector3(x + 0.5f, ActorGroundOffset, y + 0.5f);
            }

            return true;
        }

        public bool TryRecordLocalTargetSkillUse(int skillId, int skillLevel, int targetAid, int rangeCells) {
            if (targetAid <= 0 || localPlayer == null || rangeCells < 0) {
                return false;
            }

            if (!localPlayer.TryGetComponent(out RoClickToMoveController clickToMove)) {
                return false;
            }

            clickToMove.RecordPendingTargetSkillUse(skillId, skillLevel, unchecked((uint) targetAid), rangeCells);
            return true;
        }

        public bool TrySubmitLocalTargetSkillUse(int skillId, int skillLevel, int targetAid) {
            int rangeCells = ResolveTargetSkillRangeCells(skillId, skillLevel);
            if (targetAid <= 0 || localPlayer == null || rangeCells < 0) {
                return false;
            }

            if (!localPlayer.TryGetComponent(out RoClickToMoveController clickToMove)) {
                return false;
            }

            return clickToMove.TrySubmitTargetSkillUse(skillId, skillLevel, unchecked((uint) targetAid), rangeCells);
        }

        private int ResolveTargetSkillRangeCells(int skillId, int skillLevel) {
            RoSkillMetadataTable skills = content?.Assets?.GameData?.Skills;
            if (skills != null && skills.TryGetSkill(skillId, out RoSkillMetadata skill) && skill != null) {
                return RoClickToMoveController.ResolveServerTargetSkillRangeCells(skill.GetAttackRange(skillLevel));
            }

            return -1;
        }

        public bool TryApplyLocalPlayerAppearance(RoCharacterInfo character) {
            if (character == null) {
                return false;
            }

            RoActorAppearanceRequest appearance = RoOnlineActorPresenter.BuildPlayerAppearanceRequest(character);
            SetLocalPlayerBaseLevel(character.BaseLevel);
            hasLocalPlayerJob = true;
            localPlayerJob = character.JobId;
            bool applied = TryApplyLocalPlayerAppearance(appearance);
            TryApplyLocalPlayerStatus(character.Hp, character.MaxHp, character.Sp, character.MaxSp);
            SyncLocalPlayerMaxLevelAura();
            return applied;
        }

        public bool TryApplyLocalPlayerAppearance(RoOnlineActorSnapshot actor) {
            if (actor.ServerId == 0 || actor.ObjectType != RoMapActorObjectType.Player) {
                return false;
            }

            localPlayerRentalSnapshot = actor;
            hasLocalPlayerRentalSnapshot = true;
            bool applied = TryApplyLocalPlayerAppearance(RoOnlineActorPresenter.BuildPlayerAppearanceRequest(actor));
            SyncLocalRentalAttachments(actor);
            ApplyLocalPlayerStatusVisual(actor);
            EnsureLocalPlayerPointerTarget(actor.ServerId);
            return applied;
        }

        public bool TryApplyLocalPlayerMotion(RoOnlineActorSnapshot actor) {
            if (actor.ServerId == 0 || actor.ObjectType != RoMapActorObjectType.Player || localPlayer == null) {
                return false;
            }

            if (!localPlayer.TryGetComponent(out RoClickToMoveController clickToMove)) {
                return false;
            }

            localPlayerRentalSnapshot = actor;
            hasLocalPlayerRentalSnapshot = true;
            bool applied = clickToMove.TryApplyOnlineActorMotion(actor);
            SyncLocalRentalAttachments(actor);
            ApplyLocalPlayerStatusVisual(actor);
            return applied;
        }

        public bool TryApplyLocalPlayerStatus(RoPlayerStatusSnapshot status) {
            if (status == null) {
                return false;
            }

            SetLocalPlayerBaseLevel(status.BaseLevel);
            bool applied = TryApplyLocalPlayerStatus(status.Hp, status.MaxHp, status.Sp, status.MaxSp, status.AttackRange);
            SyncLocalPlayerMaxLevelAura();
            return applied;
        }

        public bool TryApplyLocalPlayerStatus(int hp, int maxHp, int sp, int maxSp) {
            return TryApplyLocalPlayerStatus(hp, maxHp, sp, maxSp, localPlayerAttackRange);
        }

        public bool TryApplyLocalPlayerStatus(int hp, int maxHp, int sp, int maxSp, int attackRange) {
            hasLocalPlayerResourceStatus = true;
            localPlayerHp = hp;
            localPlayerMaxHp = maxHp;
            localPlayerSp = sp;
            localPlayerMaxSp = maxSp;
            hasLocalPlayerAttackRange = true;
            localPlayerAttackRange = Math.Max(0, attackRange);

            if (localPlayer != null && localPlayer.TryGetComponent(out RoClickToMoveController clickToMove)) {
                clickToMove.SetAttackRangeCells(localPlayerAttackRange);
            }

            RoActorResourceBarOverlay bars = EnsureLocalPlayerResourceBars();
            if (bars != null) {
                bars.SetValues(hp, maxHp, sp, maxSp);
            }

            SyncLocalPlayerMaxLevelAura();
            return bars != null;
        }

        public bool TryApplyLocalPlayerAppearance(RoActorAppearanceRequest appearance) {
            if (appearance == null) {
                return false;
            }

            localPlayerAppearance = appearance;
            if (localPlayer == null || content == null || !content.IsInitialized) {
                return false;
            }

            int hash = CalculateLocalPlayerAppearanceHash(appearance);
            if (hasAppliedLocalPlayerAppearance && appliedLocalPlayerAppearanceHash == hash) {
                return true;
            }

            bool rebuilt = RebuildLocalPlayerVisual(localPlayer, appearance);
            if (rebuilt) {
                hasAppliedLocalPlayerAppearance = true;
                appliedLocalPlayerAppearanceHash = hash;
            }

            EnsureLocalPlayerPointerTarget();

            return rebuilt;
        }

        private RoActorResourceBarOverlay EnsureLocalPlayerResourceBars() {
            if (localPlayer == null) {
                return null;
            }

            if (localPlayerResourceBars == null) {
                localPlayerResourceBars = localPlayer.FindComponent<RoActorResourceBarOverlay>();
                if (localPlayerResourceBars == null) {
                    localPlayerResourceBars = localPlayer.gameObject.AddComponent<RoActorResourceBarOverlay>();
                }
            }

            localPlayerResourceBars.Initialize(UnityEngine.Camera.main);
            return localPlayerResourceBars;
        }

        private bool RebuildLocalPlayerVisual(Transform parent, RoActorAppearanceRequest appearance) {
            if (parent == null) {
                return false;
            }

            ClearLocalPlayerMaxLevelAura();
            hasAppliedLocalPlayerStatusLook = false;
            DestroyLocalPlayerVisuals(parent);
            RoActorEffectAnchors.EnsureOn(parent.gameObject);
            bool createdVisual = useLayeredPlayerVisual
                ? TryCreateLayeredPlayerVisual(parent, appearance)
                : TryCreateSinglePlayerVisual(parent);
            if (!createdVisual) {
                CreateDebugCapsule(parent);
            }

            ApplyLocalPlayerShadowRenderMode(parent);
            if (RoEnhancedShadowRuntime.ShouldUseFakeActorShadow(ShadowRenderMode, showPlayerShadow) &&
                parent.GetComponentInChildren<RoActorShadowVisual>(true) == null) {
                CreatePlayerShadow(parent);
            } else if (ShadowRenderMode == RoShadowRenderMode.Enhanced) {
                DestroyFakeActorShadows(parent);
            }

            BindLocalPlayerVisualToMovement(parent);
            CacheLocalPlayerVisuals(parent);
            if (hasLocalPlayerRentalSnapshot) {
                ApplyLocalPlayerStatusVisual(localPlayerRentalSnapshot);
            }

            EnsureLocalPlayerPointerTarget();
            EnsureLocalPlayerMapLightProbe(parent);
            UpdateLocalPlayerVisualSorting();
            SyncLocalPlayerMaxLevelAura();
            SyncLocalPlayerOcclusionFader(ResolveMainCamera());
            return createdVisual;
        }

        private void SyncLocalRentalAttachments(RoOnlineActorSnapshot actor) {
            if (localPlayer == null || content == null || !content.IsInitialized || actor.ObjectType != RoMapActorObjectType.Player) {
                DestroyLocalRentalCompanion(ref localPlayerFalconCompanion);
                return;
            }

            short baseJob = RoOnlineActorPresenter.ResolveRentalAttachmentBaseJob(actor);
            bool hasFalcon = actor.HasFalcon && RoMapRentalVisualSemantics.CanUseFalconAttachmentVisual(baseJob);
            SyncLocalFalconAttachment(
                hasFalcon,
                actor.ServerId,
                RoMapRentalVisualSemantics.ResolveFalconBasePath(baseJob),
                RoOnlineActorPresenter.ResolveFacingDirection(actor),
                actor.Speed);
        }

        private void ApplyLocalPlayerStatusVisual(RoOnlineActorSnapshot actor) {
            if (localPlayer == null || actor.ObjectType != RoMapActorObjectType.Player) {
                if (localPlayer != null && localPlayerLayeredVisual == null && localPlayerSpriteVisual == null) {
                    CacheLocalPlayerVisuals(localPlayer);
                }

                ApplyLocalPlayerOpacity(1f);
                ClearLocalPlayerMaxLevelAura();
                hasAppliedLocalPlayerStatusLook = false;
                SetLocalPlayerPointerTargetActive(true);
                hasLocalPlayerOnlineActorState = false;
                localPlayerOnlineActorDead = false;
                return;
            }

            if (actor.BaseLevel > 0) {
                SetLocalPlayerBaseLevel(actor.BaseLevel);
            }

            hasLocalPlayerJob = true;
            localPlayerJob = actor.Job;

            hasLocalPlayerOnlineActorState = true;
            localPlayerOnlineActorDead =
                actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.Motion == RoOnlineActorMotion.Dead ||
                actor.IsPlayingDead;

            if (localPlayerLayeredVisual == null && localPlayerSpriteVisual == null) {
                CacheLocalPlayerVisuals(localPlayer);
            }

            ApplyLocalPlayerStatusLookToVisual();
            SyncLocalPlayerMaxLevelAura();
        }

        /// <summary>The own character's status render state changed (stone grey, poison, frame hold, Hiding ...).</summary>
        /// <summary>The local player's body ACT (null before its sprite is built).</summary>
        public ROIO.Models.FileTypes.ACT LocalPlayerBodyAct =>
            localPlayerLayeredVisual != null ? localPlayerLayeredVisual.BodyAct
            : localPlayerSpriteVisual != null ? localPlayerSpriteVisual.Animator?.Act
            : null;

        public void ApplyLocalPlayerStatusLook(in Ragnarok.Combat.RoActorStatusLook look) {
            localPlayerStatusLook = look;
            ApplyLocalPlayerStatusLookToVisual();
            SyncLocalPlayerMaxLevelAura();
        }

        private void ApplyLocalPlayerStatusLookToVisual() {
            if (localPlayerLayeredVisual == null && localPlayerSpriteVisual == null) {
                return;
            }

            Ragnarok.Combat.RoActorStatusLook look = localPlayerStatusLook;
            if (hasAppliedLocalPlayerStatusLook && appliedLocalPlayerStatusLook.Equals(look)) {
                return;
            }

            ApplyLocalPlayerOpacity(look.IsDrawn ? 1f : 0f);
            if (localPlayerLayeredVisual != null) {
                localPlayerLayeredVisual.SetTintColor(look.Tint);
                localPlayerLayeredVisual.SetMotionCurrentFrameHeld(look.FreezeFrame);
            } else {
                localPlayerSpriteVisual.SetTintColor(look.Tint);
                localPlayerSpriteVisual.SetMotionCurrentFrameHeld(look.FreezeFrame);
            }

            SetLocalPlayerPointerTargetActive(look.IsDrawn);
            appliedLocalPlayerStatusLook = look;
            hasAppliedLocalPlayerStatusLook = true;
        }

        private void ApplyLocalPlayerOpacity(float opacity) {
            if (localPlayerLayeredVisual != null) {
                localPlayerLayeredVisual.SetOpacity(opacity);
            } else {
                localPlayerSpriteVisual?.SetOpacity(opacity);
            }
        }

        private void SetLocalPlayerPointerTargetActive(bool active) {
            if (localPlayerPointerMarker != null) {
                localPlayerPointerMarker.Kind = active ? RoPointerTargetKind.Player : RoPointerTargetKind.None;
                localPlayerPointerMarker.BlocksGroundClick = active;
            }
        }

        private void SetLocalPlayerBaseLevel(int baseLevel) {
            if (baseLevel <= 0) {
                return;
            }

            hasLocalPlayerBaseLevel = true;
            localPlayerBaseLevel = baseLevel;
        }

        /// <summary>
        /// The local player's max-level aura (FUN_00bcf420, re-dispatched at map load, on its own ZC_PAR_CHANGE level and
        /// on hide / cloak toggles): the original's new-system aura on the player's shim while at exactly the max level of
        /// its job category and not hidden, cloaked or invisible.
        /// </summary>
        private void SyncLocalPlayerMaxLevelAura() {
            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            if (bridge == null) {
                return;
            }

            uint aid = hasLocalPlayerRentalSnapshot ? localPlayerRentalSnapshot.ServerId : bridge.LocalPlayerId;
            int main = 0, sub = 0;
            bool blocked = false;
            if (localPlayer != null && aid != 0 && hasLocalPlayerBaseLevel && hasLocalPlayerJob) {
                Ragnarok.Combat.RoOriginalCombatRules.LevelAuraIds(localPlayerJob, localPlayerBaseLevel, out main, out sub);
                int option = hasLocalPlayerRentalSnapshot ? localPlayerRentalSnapshot.EffectState : 0;
                blocked = Ragnarok.Combat.RoOriginalCombatRules.LevelAuraBlocked(option) || !localPlayerStatusLook.IsDrawn;
            }

            int key = main == 0 && sub == 0 ? 0 : (main << 17) ^ (sub << 1) ^ (blocked ? 1 : 0);
            if (localPlayerAuraActorId == aid && localPlayerAuraKey == key) {
                return;
            }

            if (localPlayerAuraActorId != 0 && localPlayerAuraActorId != aid) {
                bridge.SetLevelAura(localPlayerAuraActorId, null, 0, 0, false);
            }

            bridge.SetLevelAura(aid, localPlayer != null ? localPlayer.transform : null, main, sub, blocked);
            localPlayerAuraActorId = key != 0 ? aid : 0;
            localPlayerAuraKey = key;
        }

        private void ClearLocalPlayerMaxLevelAura() {
            if (localPlayerAuraActorId != 0) {
                Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance?.SetLevelAura(localPlayerAuraActorId, null, 0, 0, false);
            }

            localPlayerAuraActorId = 0;
            localPlayerAuraKey = 0;
        }

        private void SyncLocalPlayerOcclusionFader(UnityEngine.Camera camera) {
            if (renderSettings == null ||
                !renderSettings.enableLocalPlayerOcclusionFade ||
                currentModelRoot == null ||
                localPlayer == null ||
                camera == null) {
                ClearLocalPlayerOcclusionFader();
                return;
            }

            if (localPlayerOcclusionFader == null) {
                localPlayerOcclusionFader = GetComponent<RoLocalPlayerOcclusionFader>();
                if (localPlayerOcclusionFader == null) {
                    localPlayerOcclusionFader = gameObject.AddComponent<RoLocalPlayerOcclusionFader>();
                }
            }

            localPlayerOcclusionFader.Configure(currentModelRoot.transform, localPlayer, camera, renderSettings);
        }

        private void ClearLocalPlayerOcclusionFader() {
            if (localPlayerOcclusionFader == null) {
                return;
            }

            localPlayerOcclusionFader.Clear();
            DestroyRuntimeObject(localPlayerOcclusionFader);
            localPlayerOcclusionFader = null;
        }

        private void SyncLocalFalconAttachment(
            bool active,
            uint ownerId,
            string basePath,
            RoActorDirection facing,
            float ownerSpeedMilliseconds
        ) {
            if (!active || string.IsNullOrWhiteSpace(basePath)) {
                DestroyLocalRentalCompanion(ref localPlayerFalconCompanion);
                return;
            }

            if (localPlayerFalconCompanion == null) {
                var companionObject = new GameObject(RoRentalCompanionVisual.BuildFalconObjectName(ownerId));
                Transform parent = currentMap != null ? currentMap.transform : localPlayer.parent;
                companionObject.transform.SetParent(parent, false);
                localPlayerFalconCompanion = companionObject.AddComponent<RoRentalCompanionVisual>();
            } else {
                localPlayerFalconCompanion.name = RoRentalCompanionVisual.BuildFalconObjectName(ownerId);
            }

            int ownerRenderQueueBase = localPlayerLayeredVisual != null
                ? localPlayerLayeredVisual.RenderQueueBase
                : (int) UnityEngine.Rendering.RenderQueue.Transparent;
            if (!localPlayerFalconCompanion.SyncFalcon(
                    content.Assets,
                    ownerId,
                    localPlayer,
                    localPlayer.position,
                    basePath,
                    facing,
                    ownerSpeedMilliseconds,
                    ownerRenderQueueBase,
                    ResolveCurrentLocalPlayerSortingOrder(),
                    snapToOwner: false)) {
                DestroyLocalRentalCompanion(ref localPlayerFalconCompanion);
            }
        }

        public bool TryPlayLocalFalconSkillDash(ushort skillId, Vector3 targetWorldPosition) {
            if (!RoMapRentalVisualSemantics.IsFalconSkill(skillId) || localPlayerFalconCompanion == null) {
                return false;
            }

            return localPlayerFalconCompanion.DashTo(targetWorldPosition);
        }

        private void DestroyLocalRentalCompanion(ref RoRentalCompanionVisual companion) {
            if (companion == null) {
                return;
            }

            GameObject target = companion.gameObject;
            companion = null;
            DestroyRuntimeObject(target);
        }

        private void EnsureLocalPlayerPointerTarget(uint actorIdOverride = 0u) {
            if (localPlayer == null) {
                return;
            }

            if (localPlayerPointerMarker == null) {
                localPlayerPointerMarker = localPlayer.FindComponent<RoPointerTargetMarker>();
                if (localPlayerPointerMarker == null) {
                    localPlayerPointerMarker = localPlayer.gameObject.AddComponent<RoPointerTargetMarker>();
                }
            }

            localPlayerPointerMarker.Kind = RoPointerTargetKind.Player;
            localPlayerPointerMarker.BlocksGroundClick = true;
            localPlayerPointerMarker.Label = "Self";

            if (localPlayerPointerShape == null) {
                localPlayerPointerShape = localPlayer.FindComponent<RoSpritePointerTargetShape>();
                if (localPlayerPointerShape == null) {
                    localPlayerPointerShape = localPlayer.gameObject.AddComponent<RoSpritePointerTargetShape>();
                }
            }

            localPlayerPointerShape.Configure(localPlayer);

            uint actorId = ResolveLocalPlayerPointerActorId(actorIdOverride);
            if (actorId == 0u) {
                return;
            }

            if (localPlayerOnlinePointerMarker == null) {
                localPlayerOnlinePointerMarker = localPlayer.FindComponent<RoOnlinePointerTargetMarker>();
                if (localPlayerOnlinePointerMarker == null) {
                    localPlayerOnlinePointerMarker = localPlayer.gameObject.AddComponent<RoOnlinePointerTargetMarker>();
                }
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            localPlayerOnlinePointerMarker.Configure(controller, actorId, RoMapActorObjectType.Player);
        }

        private uint ResolveLocalPlayerPointerActorId(uint actorIdOverride) {
            if (actorIdOverride != 0u) {
                return actorIdOverride;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller != null && controller.OwnActorId != 0u) {
                return controller.OwnActorId;
            }

            return localPlayerAppearance != null && localPlayerAppearance.serverId > 0
                ? unchecked((uint) localPlayerAppearance.serverId)
                : 0u;
        }

        private RoOnlineWorldController ResolveOnlineWorldController() {
            if (onlineWorldController != null) {
                return onlineWorldController;
            }

            onlineWorldController = FindFirstObjectByType<RoOnlineWorldController>();
            return onlineWorldController;
        }

        private void EnsureLocalPlayerMapLightProbe(Transform parent) {
            if (parent == null) {
                return;
            }

            var probe = parent.FindComponent<RoActorMapLightProbe>();
            if (probe == null) {
                probe = parent.gameObject.AddComponent<RoActorMapLightProbe>();
            }

            probe.SetSamplingMode(RoActorMapLightProbeSamplingMode.Continuous);
            probe.Initialize(groundCollider, compiledGround, ShouldSampleBakedActorShadows());
        }

        private void ApplyLocalPlayerShadowRenderMode(Transform parent) {
            if (parent == null) {
                return;
            }

            parent.GetComponentsInChildren(true, LayeredVisualTraversalScratch);
            for (int i = 0; i < LayeredVisualTraversalScratch.Count; i++) {
                RoActorLayeredVisual visual = LayeredVisualTraversalScratch[i];
                if (visual == null) {
                    continue;
                }

                visual.SetShadowRenderMode(ShadowRenderMode, EnhancedShadowSettings);
            }

            LayeredVisualTraversalScratch.Clear();
            parent.GetComponentsInChildren(true, SpriteVisualTraversalScratch);
            for (int i = 0; i < SpriteVisualTraversalScratch.Count; i++) {
                RoActorSpriteVisual visual = SpriteVisualTraversalScratch[i];
                if (visual == null) {
                    continue;
                }

                visual.SetShadowRenderMode(ShadowRenderMode, EnhancedShadowSettings);
            }

            SpriteVisualTraversalScratch.Clear();
            if (ShadowRenderMode == RoShadowRenderMode.Enhanced) {
                DestroyFakeActorShadows(parent);
            }
        }

        private void CreateWeatherRuntime(GameObject groundRoot, GameObject modelRoot, UnityEngine.Camera camera) {
            if (currentMap == null) {
                return;
            }

            RoWeatherRuntimeController runtime = EnsureWeatherRuntimeController();
            runtime.Initialize(
                effectManager,
                groundRoot,
                modelRoot,
                camera,
                audioSettings,
                ShouldAllowDayNightRuntime(),
                IsLoadedMapSkyless);
        }

        private void SyncWeatherRuntime() {
            if (currentMap == null) {
                return;
            }

            UnityEngine.Camera camera = ResolveMainCamera();
            if (activeWeatherController == null) {
                CreateWeatherRuntime(groundCollider != null ? groundCollider.gameObject : null, currentModelRoot, camera);
                ScheduleNextWeatherRuntimeSync();
                return;
            }

            if (!ShouldRunWeatherRuntimeSync()) {
                return;
            }

            activeWeatherController.SyncRuntime(camera, audioSettings, ShouldAllowDayNightRuntime(), IsLoadedMapSkyless);
        }

        private bool ShouldAllowDayNightRuntime() {
            return ShadowRenderMode == RoShadowRenderMode.Enhanced &&
                !ShouldDisableDayNightRuntime() &&
                !ShouldForceTextureOnlyMap();
        }

        private RoWeatherRuntimeController EnsureWeatherRuntimeController() {
            if (activeWeatherController != null) {
                return activeWeatherController;
            }

            if (weatherController != null) {
                activeWeatherController = weatherController;
                return activeWeatherController;
            }

            var weatherObject = new GameObject("RO Weather");
            weatherObject.transform.SetParent(currentMap != null ? currentMap.transform : transform, false);
            activeWeatherController = weatherObject.AddComponent<RoWeatherRuntimeController>();
            activeWeatherController.SetWindSettingsReference(weatherWind);
            activeWeatherController.SetRainSettingsReference(rainWeather);
            activeWeatherController.SetDayNightSettingsReference(dayNightCycle);
            activeWeatherController.SetCloudShadowSettingsReference(cloudShadows);
            activeWeatherController.SetAmbientParticleSettingsReference(ambientParticles);
            return activeWeatherController;
        }

        private void ClearWeatherRuntime() {
            if (activeWeatherController == null) {
                return;
            }

            activeWeatherController.ClearRuntime();
        }

        private bool ShouldSampleBakedActorShadows() =>
            RoActorLightingPolicy.Resolve(ShadowRenderMode, EnhancedShadowSettings).SampleBakedShadows;

        private static void DestroyFakeActorShadows(Transform parent) {
            if (parent == null) {
                return;
            }

            parent.GetComponentsInChildren(true, ShadowVisualTraversalScratch);
            for (int i = 0; i < ShadowVisualTraversalScratch.Count; i++) {
                RoActorShadowVisual shadow = ShadowVisualTraversalScratch[i];
                if (shadow == null) {
                    continue;
                }

                shadow.gameObject.SetActive(false);
                DestroyRuntimeObject(shadow.gameObject);
            }

            ShadowVisualTraversalScratch.Clear();
        }

        private static void DestroyLocalPlayerVisuals(Transform parent) {
            if (parent == null) {
                return;
            }

            parent.GetComponentsInChildren(true, LayeredVisualTraversalScratch);
            for (int i = 0; i < LayeredVisualTraversalScratch.Count; i++) {
                RoActorLayeredVisual layeredVisual = LayeredVisualTraversalScratch[i];
                if (layeredVisual != null) {
                    layeredVisual.gameObject.SetActive(false);
                    DestroyRuntimeObject(layeredVisual.gameObject);
                }
            }

            LayeredVisualTraversalScratch.Clear();
            parent.GetComponentsInChildren(true, SpriteVisualTraversalScratch);
            for (int i = 0; i < SpriteVisualTraversalScratch.Count; i++) {
                RoActorSpriteVisual visual = SpriteVisualTraversalScratch[i];
                if (visual == null || visual.GetComponentInParent<RoActorLayeredVisual>(true) != null) {
                    continue;
                }

                visual.gameObject.SetActive(false);
                DestroyRuntimeObject(visual.gameObject);
            }

            SpriteVisualTraversalScratch.Clear();
            for (int i = parent.childCount - 1; i >= 0; i--) {
                Transform child = parent.GetChild(i);
                if (child != null && string.Equals(child.name, "Player Debug Capsule", StringComparison.Ordinal)) {
                    child.gameObject.SetActive(false);
                    DestroyRuntimeObject(child.gameObject);
                }
            }
        }

        private static void BindLocalPlayerVisualToMovement(Transform parent) {
            if (parent == null || !parent.TryGetComponent(out RoClickToMoveController clickToMove)) {
                return;
            }

            RoActorLayeredVisual layeredVisual = parent.GetComponentInChildren<RoActorLayeredVisual>(true);
            if (layeredVisual != null) {
                clickToMove.SetActorVisual(layeredVisual);
                return;
            }

            clickToMove.SetActorVisual(parent.GetComponentInChildren<RoActorSpriteVisual>(true));
        }

        private void CacheLocalPlayerVisuals(Transform parent) {
            localPlayerLayeredVisual = null;
            localPlayerSpriteVisual = null;
            lastLocalPlayerSortingOrder = int.MinValue;
            hasAppliedLocalPlayerStatusLook = false;
            if (parent == null) {
                return;
            }

            localPlayerLayeredVisual = parent.GetComponentInChildren<RoActorLayeredVisual>(true);
            if (localPlayerLayeredVisual == null) {
                localPlayerSpriteVisual = parent.GetComponentInChildren<RoActorSpriteVisual>(true);
            }
        }

        private void UpdateLocalPlayerVisualSorting() {
            if (!useDynamicLocalPlayerSorting || localPlayer == null) {
                return;
            }

            if (localPlayerLayeredVisual == null && localPlayerSpriteVisual == null) {
                CacheLocalPlayerVisuals(localPlayer);
            }

            int sortingOrder = CalculateLocalPlayerSortingOrder(
                localPlayer.position,
                ResolveMainCamera(),
                localPlayerSortingOrderBase,
                localPlayerSortingBucketCount,
                ResolveLocalPlayerSortingId());

            if (sortingOrder == lastLocalPlayerSortingOrder) {
                return;
            }

            lastLocalPlayerSortingOrder = sortingOrder;

            if (localPlayerLayeredVisual != null) {
                localPlayerLayeredVisual.SetSortingOrderBase(sortingOrder);
            } else {
                localPlayerSpriteVisual?.SetSortingOrder(sortingOrder);
            }

            localPlayerFalconCompanion?.Visual?.SetSortingOrderBase(
                RoOnlineActorPresenter.ResolveFalconAttachmentSortingOrder(sortingOrder));
            SyncLocalPlayerMaxLevelAura();
        }

        private int ResolveCurrentLocalPlayerSortingOrder() {
            if (lastLocalPlayerSortingOrder != int.MinValue) {
                return lastLocalPlayerSortingOrder;
            }

            if (localPlayer == null) {
                return localPlayerSortingOrderBase;
            }

            return CalculateLocalPlayerSortingOrder(
                localPlayer.position,
                ResolveMainCamera(),
                localPlayerSortingOrderBase,
                localPlayerSortingBucketCount,
                ResolveLocalPlayerSortingId());
        }

        public static int CalculateLocalPlayerSortingOrder(
            Vector3 worldPosition,
            UnityEngine.Camera camera,
            int baseOrder,
            int bucketCount,
            uint serverId
        ) {
            return RoOnlineActorPresenter.CalculateActorSortingOrder(
                worldPosition,
                camera,
                baseOrder,
                bucketCount,
                serverId);
        }

        private uint ResolveLocalPlayerSortingId() {
            if (localPlayerAppearance == null || localPlayerAppearance.serverId <= 0) {
                return 0;
            }

            return unchecked((uint) localPlayerAppearance.serverId);
        }

        private static int CalculateLocalPlayerAppearanceHash(RoActorAppearanceRequest appearance) {
            unchecked {
                int hash = appearance.classId;
                hash = (hash * 397) ^ appearance.baseClassId;
                hash = (hash * 397) ^ appearance.bodyStyleId;
                hash = (hash * 397) ^ appearance.isMale.GetHashCode();
                hash = (hash * 397) ^ appearance.headId;
                hash = (hash * 397) ^ appearance.hairId;
                hash = (hash * 397) ^ appearance.hairColorId;
                hash = (hash * 397) ^ appearance.bodyPaletteId;
                hash = (hash * 397) ^ appearance.weaponId;
                hash = (hash * 397) ^ appearance.shieldId;
                hash = (hash * 397) ^ appearance.robeId;
                hash = (hash * 397) ^ appearance.headgearTopId;
                hash = (hash * 397) ^ appearance.headgearMidId;
                hash = (hash * 397) ^ appearance.headgearLowerId;
                hash = (hash * 397) ^ appearance.hasCart.GetHashCode();
                hash = (hash * 397) ^ appearance.cartNum;
                return hash;
            }
        }

        private UnityEngine.Camera ConfigureCamera(Bounds bounds, string loadedMapName) {
            UnityEngine.Camera camera = GetOrCreateMainCamera();
            cachedMainCamera = camera;
            var controller = camera.FindComponent<RoCameraController>();
            if (controller == null) {
                controller = camera.gameObject.AddComponent<RoCameraController>();
            }

            controller.ApplyClassicGravityPreset();
            if (localPlayer != null) {
                controller.Follow(localPlayer, RoCameraController.ClassicGravityDistance);
            } else {
                controller.FrameBounds(new Bounds(Vector3.zero, bounds.size));
            }

            RoAssetTables tables = content?.Assets?.Tables;
            if (tables != null && !string.IsNullOrEmpty(loadedMapName)) {
                bool appliedViewpoint = false;
                if (applyViewpointTableCameraPreset &&
                    tables.TryGetViewpoint(loadedMapName, out ROIO.Models.RoViewpointPreset preset)) {
                    controller.ApplyViewpointPreset(preset);
                    appliedViewpoint = true;
                }

                bool indoor = tables.IsIndoorMap(loadedMapName);
                if (indoor) {
                    ApplyIndoorCameraBackground(camera);
                }

                if (indoor && !appliedViewpoint) {
                    controller.ApplyRobrowserIndoorPreset();
                } else {
                    controller.SetIndoorMode(indoor);
                }
            }

            return camera;
        }

        private UnityEngine.Camera ResolveMainCamera() {
            if (cachedMainCamera != null) {
                return cachedMainCamera;
            }

            cachedMainCamera = UnityEngine.Camera.main;
            return cachedMainCamera;
        }

        private bool ShouldRunWeatherRuntimeSync() {
            if (!Application.isPlaying) {
                return true;
            }

            float now = Time.unscaledTime;
            if (now < nextWeatherRuntimeSyncTime) {
                return false;
            }

            nextWeatherRuntimeSyncTime = now + WeatherRuntimeSyncIntervalSeconds;
            return true;
        }

        private void ScheduleNextWeatherRuntimeSync() {
            if (Application.isPlaying) {
                nextWeatherRuntimeSyncTime = Time.unscaledTime + WeatherRuntimeSyncIntervalSeconds;
            }
        }

        private bool IsIndoorMap(string loadedMapName) {
            RoAssetTables tables = content?.Assets?.Tables;
            return tables != null && !string.IsNullOrEmpty(loadedMapName) && tables.IsIndoorMap(loadedMapName);
        }

        public bool IsIndoorMapResource(string loadedMapName) {
            return IsIndoorMap(NormalizeMapResourceName(loadedMapName));
        }

        private bool IsSkylessMap(string loadedMapName) {
            RoAssetTables tables = content?.Assets?.Tables;
            return tables != null && !string.IsNullOrEmpty(loadedMapName) && tables.IsSkylessMap(loadedMapName);
        }

        public bool IsSkylessMapResource(string loadedMapName) {
            return IsSkylessMap(NormalizeMapResourceName(loadedMapName));
        }

        private static void ApplyIndoorCameraBackground(UnityEngine.Camera camera) {
            if (camera == null) {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        private static UnityEngine.Camera GetOrCreateMainCamera() {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera != null) {
                return camera;
            }

            var cameraObject = new GameObject("Main Camera") {
                tag = "MainCamera"
            };
            camera = cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        /// <summary>
        /// The Gravity parity camera (no post-processing, no HDR) is the Original mode's; the Enhanced look renders
        /// HDR and needs post-processing (tonemapping, bloom, grading, SMAA).
        /// </summary>
        private bool CameraParityDisablesPostProcessing =>
            disableCameraPostProcessingForGravityParity && ShadowRenderMode != RoShadowRenderMode.Enhanced;

        private void ApplyGravityParityCameraDefaults(UnityEngine.Camera camera) {
            if (camera == null) {
                return;
            }

            bool parity = CameraParityDisablesPostProcessing;
            if (!parity && ShadowRenderMode != RoShadowRenderMode.Enhanced) {
                return;
            }

            camera.allowHDR = !parity;
            Behaviour[] components = camera.GetComponents<Behaviour>();
            for (int i = 0; i < components.Length; i++) {
                Behaviour component = components[i];
                if (component == null) {
                    continue;
                }

                Type type = component.GetType();
                if (type.FullName != UniversalAdditionalCameraDataTypeName) {
                    continue;
                }

                SetBooleanMember(type, component, "renderPostProcessing", !parity);
                SetBooleanMember(type, component, "m_RenderPostProcessing", !parity);
                // HDR output (an HDR display swapchain) stays off: the classic UI composites into an SDR backbuffer
                SetBooleanMember(type, component, "allowHDROutput", false);
                SetBooleanMember(type, component, "m_AllowHDROutput", false);
                break;
            }
        }

        private void ApplyClassicRoFog(string loadedMapName) {
            if (!enableClassicRoFog || content == null || content.Assets == null) {
                if (ShouldLogRuntimeMapDiagnostics()) {
                    LogRuntimeMapDiagnostic($"[RoFog] SKIP: enableClassicRoFog={enableClassicRoFog} content={content != null} assets={content?.Assets != null}");
                }
                RoMapFog.ApplyDisabledGlobals();
                return;
            }

            if (RoMapFog.TryLoadSettings(content.Assets, loadedMapName, out RoMapFogSettings settings)) {
                if (ShouldLogRuntimeMapDiagnostics()) {
                    LogRuntimeMapDiagnostic(RoMapFog.BuildAudit(loadedMapName, settings));
                }
                RoMapFog.ApplyGlobals(settings);
                return;
            }

            if (ShouldLogRuntimeMapDiagnostics()) {
                LogRuntimeMapDiagnostic($"[RoFog] TryLoadSettings FAILED for '{loadedMapName}': tableExists={content.Assets.Exists(RoMapFog.FogTablePath)}");
            }
            RoMapFog.ApplyDisabledGlobals();
        }

        private bool ShouldLogRuntimeMapDiagnostics() {
            return RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics;
        }

        private void LogRuntimeMapDiagnostic(string message) {
            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log(message, this);
        }

        private void LogLightingDiagnosticsIfRequested(
            AsyncMapLoader.GameMapData mapData,
            RoRuntimeMapRenderResult renderResult,
            UnityEngine.Camera camera
        ) {
            if (!ShouldLogLightingDiagnostics()) {
                return;
            }

            var builder = new StringBuilder(2048);
            builder.Append("[RoLightingDiagnostics]");
            builder.Append(" map=").Append(mapData.Name ?? "unknown");
            builder.Append(" shadowMode=").Append(ShadowRenderMode);
            builder.Append(" dayNightDisabled=").Append(ShouldDisableDayNightRuntime());
            builder.Append(" textureOnlyMap=").Append(ShouldForceTextureOnlyMap());
            builder.Append(" disableRswLighting=").Append(ShouldDisableRswLighting());
            builder.Append(" disableMapLightmap=").Append(ShouldDisableMapLightmap());
            builder.Append(" disableMapTint=").Append(ShouldDisableMapTint());
            builder.Append(" classicFog=").Append(enableClassicRoFog);
            builder.Append(" unityFog=").Append(UnityEngine.RenderSettings.fog);
            builder.Append(" quality=").Append(QualitySettings.names[QualitySettings.GetQualityLevel()]);
            builder.Append(" shadows=").Append(QualitySettings.shadows);
            builder.Append(" shadowDistance=").Append(QualitySettings.shadowDistance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" shadowResolution=").Append(QualitySettings.shadowResolution);
            builder.Append(" renderPipeline=").Append(ResolveRenderPipelineName());
            builder.Append(" camera=").Append(camera != null ? camera.name : "none");
            builder.AppendLine();

            AppendRenderPipelineDiagnostics(builder);
            AppendShaderKeywordDiagnostics(builder);
            AppendLightDiagnostics(builder);
            AppendRendererDiagnostics(builder, "ground", renderResult?.Ground);
            AppendRendererDiagnostics(builder, "models", renderResult?.Models);
            AppendRendererDiagnostics(builder, "localPlayer", localPlayer != null ? localPlayer.gameObject : null);
            AppendMaterialDiagnostics(builder, "groundMaterial", renderResult?.Ground);
            AppendMaterialDiagnostics(builder, "modelMaterial", renderResult?.Models);
            AppendMaterialDiagnostics(builder, "localPlayerMaterial", localPlayer != null ? localPlayer.gameObject : null);

            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log(builder.ToString(), this);
        }

        private static bool ShouldLogLightingDiagnostics() {
            if (lightingDiagnosticsEnabledCache != 0) {
                return lightingDiagnosticsEnabledCache == 1;
            }

            bool enabled = false;
            try {
                string[] args = Environment.GetCommandLineArgs();
                if (args != null) {
                    for (int i = 0; i < args.Length; i++) {
                        string arg = args[i];
                        if (string.Equals(arg, "-roLightingDiagnostics", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "-ro-lighting-diagnostics", StringComparison.OrdinalIgnoreCase)) {
                            enabled = true;
                            break;
                        }
                    }
                }
            } catch {
                enabled = false;
            }

            if (!enabled) {
                try {
                    string env = Environment.GetEnvironmentVariable("RO_LIGHTING_DIAGNOSTICS");
                    enabled = string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "yes", StringComparison.OrdinalIgnoreCase);
                } catch {
                    enabled = false;
                }
            }

            lightingDiagnosticsEnabledCache = enabled ? 1 : 2;
            return enabled;
        }

        private static bool ShouldDisableRealtimeShadowReceive() {
            if (disableRealtimeShadowReceiveCache != 0) {
                return disableRealtimeShadowReceiveCache == 1;
            }

            bool enabled = false;
            try {
                string[] args = Environment.GetCommandLineArgs();
                if (args != null) {
                    for (int i = 0; i < args.Length; i++) {
                        string arg = args[i];
                        if (string.Equals(arg, "-roDisableRealtimeShadowReceive", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "-ro-disable-realtime-shadow-receive", StringComparison.OrdinalIgnoreCase)) {
                            enabled = true;
                            break;
                        }
                    }
                }
            } catch {
                enabled = false;
            }

            if (!enabled) {
                try {
                    string env = Environment.GetEnvironmentVariable("RO_DISABLE_REALTIME_SHADOW_RECEIVE");
                    enabled = string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "yes", StringComparison.OrdinalIgnoreCase);
                } catch {
                    enabled = false;
                }
            }

            disableRealtimeShadowReceiveCache = enabled ? 1 : 2;
            return enabled;
        }

        private static bool ShouldDisableDayNightRuntime() {
            if (disableDayNightRuntimeCache != 0) {
                return disableDayNightRuntimeCache == 1;
            }

            bool enabled = false;
            try {
                string[] args = Environment.GetCommandLineArgs();
                if (args != null) {
                    for (int i = 0; i < args.Length; i++) {
                        string arg = args[i];
                        if (string.Equals(arg, "-roDisableDayNightRuntime", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "-ro-disable-day-night-runtime", StringComparison.OrdinalIgnoreCase)) {
                            enabled = true;
                            break;
                        }
                    }
                }
            } catch {
                enabled = false;
            }

            if (!enabled) {
                try {
                    string env = Environment.GetEnvironmentVariable("RO_DISABLE_DAY_NIGHT_RUNTIME");
                    enabled = string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "yes", StringComparison.OrdinalIgnoreCase);
                } catch {
                    enabled = false;
                }
            }

            disableDayNightRuntimeCache = enabled ? 1 : 2;
            return enabled;
        }

        private static bool ShouldDisableRswLighting() {
            return ReadDiagnosticFlag(
                ref disableRswLightingCache,
                "RO_DISABLE_RSW_LIGHTING",
                "-roDisableRswLighting",
                "-ro-disable-rsw-lighting");
        }

        private static bool ShouldDisableMapLightmap() {
            return ReadDiagnosticFlag(
                ref disableMapLightmapCache,
                "RO_DISABLE_MAP_LIGHTMAP",
                "-roDisableMapLightmap",
                "-ro-disable-map-lightmap");
        }

        private static bool ShouldDisableMapTint() {
            return ReadDiagnosticFlag(
                ref disableMapTintCache,
                "RO_DISABLE_MAP_TINT",
                "-roDisableMapTint",
                "-ro-disable-map-tint");
        }

        private static bool ShouldForceTextureOnlyMap() {
            return ReadDiagnosticFlag(
                ref forceTextureOnlyMapCache,
                "RO_TEXTURE_ONLY_MAP",
                "-roTextureOnlyMap",
                "-ro-texture-only-map");
        }

        private static bool ShouldDebugRealtimeShadows() {
            return ReadDiagnosticFlag(
                ref debugRealtimeShadowsCache,
                "RO_DEBUG_REALTIME_SHADOWS",
                "-roDebugRealtimeShadows",
                "-ro-debug-realtime-shadows");
        }

        private static bool ReadDiagnosticFlag(
            ref int cache,
            string environmentVariable,
            string commandLineArgument,
            string alternateCommandLineArgument
        ) {
            if (cache != 0) {
                return cache == 1;
            }

            bool enabled = false;
            try {
                string[] args = Environment.GetCommandLineArgs();
                if (args != null) {
                    for (int i = 0; i < args.Length; i++) {
                        string arg = args[i];
                        if (string.Equals(arg, commandLineArgument, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, alternateCommandLineArgument, StringComparison.OrdinalIgnoreCase)) {
                            enabled = true;
                            break;
                        }
                    }
                }
            } catch {
                enabled = false;
            }

            if (!enabled) {
                try {
                    string env = Environment.GetEnvironmentVariable(environmentVariable);
                    enabled = string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "yes", StringComparison.OrdinalIgnoreCase);
                } catch {
                    enabled = false;
                }
            }

            cache = enabled ? 1 : 2;
            return enabled;
        }

        private static string ResolveRenderPipelineName() {
            UnityEngine.Rendering.RenderPipelineAsset asset = QualitySettings.renderPipeline;
            if (asset == null) {
                asset = TryGetGraphicsRenderPipelineAsset("currentRenderPipeline") ??
                    TryGetGraphicsRenderPipelineAsset("renderPipelineAsset") ??
                    TryGetGraphicsRenderPipelineAsset("defaultRenderPipeline");
            }

            return asset != null ? asset.name + " (" + asset.GetType().Name + ")" : "builtin/null";
        }

        private static UnityEngine.Rendering.RenderPipelineAsset TryGetGraphicsRenderPipelineAsset(string propertyName) {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = typeof(UnityEngine.Rendering.GraphicsSettings).GetProperty(propertyName, flags);
            if (property == null ||
                !typeof(UnityEngine.Rendering.RenderPipelineAsset).IsAssignableFrom(property.PropertyType)) {
                return null;
            }

            try {
                return property.GetValue(null) as UnityEngine.Rendering.RenderPipelineAsset;
            } catch (TargetInvocationException) {
                return null;
            }
        }

        private void SyncRuntimeCameraAntialiasing() {
            UnityEngine.Camera camera = ResolveMainCamera();
            ApplyRuntimeCameraAntialiasing(camera);
        }

        private void ApplyRuntimeCameraAntialiasing(UnityEngine.Camera camera, bool force = false) {
            if (camera == null) {
                return;
            }

            RoCameraAntialiasingMode mode = renderSettings != null
                ? renderSettings.cameraAntialiasing
                : RoCameraAntialiasingMode.SmaaHigh;
            bool parityDisablesPostProcessing = CameraParityDisablesPostProcessing;
            if (!force &&
                hasAppliedCameraAntialiasing &&
                lastAppliedCameraAntialiasingCamera == camera &&
                lastAppliedCameraAntialiasingMode == mode &&
                lastAppliedCameraPostProcessingParityDisabled == parityDisablesPostProcessing) {
                return;
            }

            hasAppliedCameraAntialiasing = true;
            lastAppliedCameraAntialiasingCamera = camera;
            lastAppliedCameraAntialiasingMode = mode;
            lastAppliedCameraPostProcessingParityDisabled = parityDisablesPostProcessing;

            if (parityDisablesPostProcessing) {
                if (mode != RoCameraAntialiasingMode.None) {
                    if (!loggedCameraAntialiasingParitySkip && ShouldLogRuntimeMapDiagnostics()) {
                        LogRuntimeMapDiagnostic(
                            $"[RoCameraAA] SKIP: disableCameraPostProcessingForGravityParity=true mode={mode}; URP anti-aliasing requires renderPostProcessing.");
                    }

                    loggedCameraAntialiasingParitySkip = true;
                } else {
                    loggedCameraAntialiasingParitySkip = false;
                }

                ApplyCameraAntialiasingToCameraData(camera, RoCameraAntialiasingMode.None);
                ApplyGravityParityCameraDefaults(camera);
                return;
            }

            loggedCameraAntialiasingParitySkip = false;
            ApplyCameraAntialiasingToCameraData(camera, mode);
        }

        private static bool ApplyCameraAntialiasingToCameraData(UnityEngine.Camera camera, RoCameraAntialiasingMode mode) {
            Behaviour[] components = camera.GetComponents<Behaviour>();
            for (int i = 0; i < components.Length; i++) {
                Behaviour component = components[i];
                if (component == null) {
                    continue;
                }

                if (TryApplyCameraAntialiasingToUniversalAdditionalCameraData(component.GetType(), component, mode)) {
                    return true;
                }
            }

            return false;
        }

        public static RoCameraAntialiasingRuntimeSettings ResolveCameraAntialiasingSettings(RoCameraAntialiasingMode mode) {
            switch (mode) {
                case RoCameraAntialiasingMode.Fxaa:
                    return new RoCameraAntialiasingRuntimeSettings(
                        requiresPostProcessing: true,
                        antialiasingValue: 1,
                        antialiasingQualityValue: 0,
                        usesAntialiasingQuality: false);
                case RoCameraAntialiasingMode.SmaaLow:
                    return new RoCameraAntialiasingRuntimeSettings(
                        requiresPostProcessing: true,
                        antialiasingValue: 2,
                        antialiasingQualityValue: 0,
                        usesAntialiasingQuality: true);
                case RoCameraAntialiasingMode.SmaaMedium:
                    return new RoCameraAntialiasingRuntimeSettings(
                        requiresPostProcessing: true,
                        antialiasingValue: 2,
                        antialiasingQualityValue: 1,
                        usesAntialiasingQuality: true);
                case RoCameraAntialiasingMode.SmaaHigh:
                    return new RoCameraAntialiasingRuntimeSettings(
                        requiresPostProcessing: true,
                        antialiasingValue: 2,
                        antialiasingQualityValue: 2,
                        usesAntialiasingQuality: true);
                case RoCameraAntialiasingMode.None:
                default:
                    return new RoCameraAntialiasingRuntimeSettings(
                        requiresPostProcessing: false,
                        antialiasingValue: 0,
                        antialiasingQualityValue: 0,
                        usesAntialiasingQuality: false);
            }
        }

        public static bool TryApplyCameraAntialiasingToUniversalAdditionalCameraData(
            Type type,
            object instance,
            RoCameraAntialiasingMode mode
        ) {
            if (type == null || instance == null || type.FullName != UniversalAdditionalCameraDataTypeName) {
                return false;
            }

            RoCameraAntialiasingRuntimeSettings settings = ResolveCameraAntialiasingSettings(mode);
            SetEnumMember(type, instance, "antialiasing", settings.AntialiasingValue);
            SetEnumMember(type, instance, "m_Antialiasing", settings.AntialiasingValue);
            SetEnumMember(type, instance, "antialiasingQuality", settings.AntialiasingQualityValue);
            SetEnumMember(type, instance, "m_AntialiasingQuality", settings.AntialiasingQualityValue);
            SetBooleanMember(type, instance, "renderPostProcessing", settings.RequiresPostProcessing);
            SetBooleanMember(type, instance, "m_RenderPostProcessing", settings.RequiresPostProcessing);
            return true;
        }

        private static void AppendRenderPipelineDiagnostics(StringBuilder builder) {
            UnityEngine.Rendering.RenderPipelineAsset asset = QualitySettings.renderPipeline;
            if (asset == null) {
                asset = TryGetGraphicsRenderPipelineAsset("currentRenderPipeline") ??
                    TryGetGraphicsRenderPipelineAsset("renderPipelineAsset") ??
                    TryGetGraphicsRenderPipelineAsset("defaultRenderPipeline");
            }

            builder.Append("  rpAsset: ");
            if (asset == null) {
                builder.AppendLine("none");
                return;
            }

            builder.Append(asset.name);
            builder.Append(" type=").Append(asset.GetType().FullName);
            AppendRenderPipelineMember(builder, asset, "m_MainLightRenderingMode");
            AppendRenderPipelineMember(builder, asset, "m_MainLightShadowsSupported");
            AppendRenderPipelineMember(builder, asset, "m_AnyShadowsSupported");
            AppendRenderPipelineMember(builder, asset, "m_SoftShadowsSupported");
            AppendRenderPipelineMember(builder, asset, "m_ShadowDistance");
            AppendRenderPipelineMember(builder, asset, "m_ShadowCascadeCount");
            AppendRenderPipelineMember(builder, asset, "m_MainLightShadowmapResolution");
            AppendRenderPipelineMember(builder, asset, "m_PrefilteringModeMainLightShadows");
            AppendRenderPipelineMember(builder, asset, "m_PrefilterSoftShadows");
            builder.AppendLine();
        }

        private static void AppendRenderPipelineMember(StringBuilder builder, object target, string memberName) {
            if (!TryReadMember(target, memberName, out object value)) {
                return;
            }

            builder.Append(' ').Append(memberName).Append('=').Append(value ?? "null");
        }

        private static bool TryReadMember(object target, string memberName, out object value) {
            value = null;
            if (target == null) {
                return false;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.CanRead) {
                try {
                    value = property.GetValue(target);
                    return true;
                } catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is MethodAccessException ||
                    exception is TargetInvocationException) {
                    return false;
                }
            }

            FieldInfo field = type.GetField(memberName, flags);
            if (field == null) {
                return false;
            }

            try {
                value = field.GetValue(target);
                return true;
            } catch (Exception exception) when (
                exception is ArgumentException ||
                exception is MethodAccessException ||
                exception is TargetInvocationException) {
                return false;
            }
        }

        private static void AppendShaderKeywordDiagnostics(StringBuilder builder) {
            builder.Append("  shaderKeywords:");
            AppendShaderKeyword(builder, "_MAIN_LIGHT_SHADOWS");
            AppendShaderKeyword(builder, "_MAIN_LIGHT_SHADOWS_CASCADE");
            AppendShaderKeyword(builder, "_MAIN_LIGHT_SHADOWS_SCREEN");
            AppendShaderKeyword(builder, "_SHADOWS_SOFT");
            AppendShaderKeyword(builder, "_ADDITIONAL_LIGHT_SHADOWS");
            builder.AppendLine();
        }

        private static void AppendShaderKeyword(StringBuilder builder, string keywordName) {
            bool enabled = false;
            try {
                UnityEngine.Rendering.GlobalKeyword keyword = UnityEngine.Rendering.GlobalKeyword.Create(keywordName);
                enabled = Shader.IsKeywordEnabled(keyword);
            } catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException) {
                enabled = false;
            }

            builder.Append(' ').Append(keywordName).Append('=').Append(enabled);
        }

        private static void AppendLightDiagnostics(StringBuilder builder) {
            Light sun = UnityEngine.RenderSettings.sun;
            builder.Append("  sun: ");
            if (sun == null) {
                builder.AppendLine("none");
            } else {
                AppendLight(builder, sun);
                builder.AppendLine();
            }

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int directional = 0;
            int enabledDirectional = 0;
            for (int i = 0; i < lights.Length; i++) {
                Light light = lights[i];
                if (light == null || light.type != LightType.Directional) {
                    continue;
                }

                directional++;
                if (light.enabled) {
                    enabledDirectional++;
                }
            }

            builder.Append("  directionalLights: total=").Append(directional)
                .Append(" enabled=").Append(enabledDirectional);
            for (int i = 0, logged = 0; i < lights.Length && logged < 4; i++) {
                Light light = lights[i];
                if (light == null || light.type != LightType.Directional) {
                    continue;
                }

                builder.AppendLine();
                builder.Append("    ");
                AppendLight(builder, light);
                logged++;
            }

            builder.AppendLine();
        }

        private static void AppendLight(StringBuilder builder, Light light) {
            builder.Append(light.name)
                .Append(" enabled=").Append(light.enabled)
                .Append(" type=").Append(light.type)
                .Append(" shadows=").Append(light.shadows)
                .Append(" strength=").Append(light.shadowStrength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" cullingMask=").Append(light.cullingMask)
                .Append(" direction=(")
                .Append(light.transform.forward.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(light.transform.forward.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(light.transform.forward.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(')');
        }

        private static void AppendRendererDiagnostics(StringBuilder builder, string label, GameObject root) {
            builder.Append("  ").Append(label).Append(": ");
            if (root == null) {
                builder.AppendLine("none");
                return;
            }

            LightingDiagnosticsRendererScratch.Clear();
            root.GetComponentsInChildren(true, LightingDiagnosticsRendererScratch);
            int total = LightingDiagnosticsRendererScratch.Count;
            int enabled = 0;
            int castOff = 0;
            int castOn = 0;
            int castTwoSided = 0;
            int castShadowsOnly = 0;
            int receive = 0;
            int roGroundEnhanced = 0;
            int roModelEnhanced = 0;
            int roSpriteEnhanced = 0;
            int otherShader = 0;
            int receivePropertyOn = 0;
            int receivePropertyOff = 0;

            for (int i = 0; i < LightingDiagnosticsRendererScratch.Count; i++) {
                MeshRenderer renderer = LightingDiagnosticsRendererScratch[i];
                if (renderer == null) {
                    continue;
                }

                if (renderer.enabled) {
                    enabled++;
                }

                switch (renderer.shadowCastingMode) {
                    case UnityEngine.Rendering.ShadowCastingMode.Off:
                        castOff++;
                        break;
                    case UnityEngine.Rendering.ShadowCastingMode.On:
                        castOn++;
                        break;
                    case UnityEngine.Rendering.ShadowCastingMode.TwoSided:
                        castTwoSided++;
                        break;
                    case UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly:
                        castShadowsOnly++;
                        break;
                }

                if (renderer.receiveShadows) {
                    receive++;
                }

                Material material = renderer.sharedMaterial;
                Shader shader = material != null ? material.shader : null;
                string shaderName = shader != null ? shader.name : string.Empty;
                if (string.Equals(shaderName, "Ragnarok/RO Ground Enhanced", StringComparison.Ordinal)) {
                    roGroundEnhanced++;
                } else if (string.Equals(shaderName, "Ragnarok/RO Model Enhanced", StringComparison.Ordinal)) {
                    roModelEnhanced++;
                } else if (string.Equals(shaderName, "Ragnarok/RO Sprite Enhanced", StringComparison.Ordinal)) {
                    roSpriteEnhanced++;
                } else {
                    otherShader++;
                }

                if (material != null && material.HasProperty("_RoReceiveRealtimeShadows")) {
                    if (material.GetFloat("_RoReceiveRealtimeShadows") > 0.5f) {
                        receivePropertyOn++;
                    } else {
                        receivePropertyOff++;
                    }
                }
            }

            LightingDiagnosticsRendererScratch.Clear();
            builder.Append("root=").Append(root.name)
                .Append(" total=").Append(total)
                .Append(" enabled=").Append(enabled)
                .Append(" castOff=").Append(castOff)
                .Append(" castOn=").Append(castOn)
                .Append(" castTwoSided=").Append(castTwoSided)
                .Append(" castShadowsOnly=").Append(castShadowsOnly)
                .Append(" receive=").Append(receive)
                .Append(" shaderGroundEnhanced=").Append(roGroundEnhanced)
                .Append(" shaderModelEnhanced=").Append(roModelEnhanced)
                .Append(" shaderSpriteEnhanced=").Append(roSpriteEnhanced)
                .Append(" shaderOther=").Append(otherShader)
                .Append(" receivePropOn=").Append(receivePropertyOn)
                .Append(" receivePropOff=").Append(receivePropertyOff);
            builder.AppendLine();
        }

        private static void AppendMaterialDiagnostics(StringBuilder builder, string label, GameObject root) {
            builder.Append("  ").Append(label).Append(": ");
            if (root == null) {
                builder.AppendLine("none");
                return;
            }

            Material material = FindFirstSharedMaterial(root, out MeshRenderer renderer);
            if (material == null) {
                builder.Append("root=").Append(root.name).AppendLine(" material=none");
                return;
            }

            builder.Append("root=").Append(root.name)
                .Append(" renderer=").Append(renderer != null ? renderer.name : "unknown")
                .Append(" material=").Append(material.name)
                .Append(" shader=").Append(material.shader != null ? material.shader.name : "none")
                .Append(" queue=").Append(material.renderQueue)
                .AppendLine();

            builder.Append("    state:");
            AppendMaterialFloat(builder, material, "_UseLightmap");
            AppendMaterialFloat(builder, material, "_UseTint");
            AppendMaterialFloat(builder, material, "_RoUseLighting");
            AppendMaterialFloat(builder, material, "_RoLightingStrength");
            AppendMaterialVector(builder, material, "_RoLightDirection");
            AppendMaterialColor(builder, material, "_RoLightAmbient");
            AppendMaterialColor(builder, material, "_RoLightDiffuse");
            AppendMaterialFloat(builder, material, "_RoLightOpacity");
            AppendMaterialFloat(builder, material, "_RoUseLightmapAlpha");
            AppendMaterialFloat(builder, material, "_RoReceiveRealtimeShadows");
            AppendMaterialFloat(builder, material, "_RoRealtimeShadowStrength");
            AppendMaterialColor(builder, material, "_BaseColor");
            builder.AppendLine();

            AppendMaterialTexture(builder, material, "_BaseMap");
            AppendMaterialTexture(builder, material, "_MainTex");
            AppendMaterialTexture(builder, material, "_Lightmap");
            AppendMaterialTexture(builder, material, "_Tintmap");
        }

        private static Material FindFirstSharedMaterial(GameObject root, out MeshRenderer renderer) {
            renderer = null;
            LightingDiagnosticsRendererScratch.Clear();
            root.GetComponentsInChildren(true, LightingDiagnosticsRendererScratch);
            for (int i = 0; i < LightingDiagnosticsRendererScratch.Count; i++) {
                MeshRenderer candidate = LightingDiagnosticsRendererScratch[i];
                if (candidate == null) {
                    continue;
                }

                Material material = candidate.sharedMaterial;
                if (material == null) {
                    continue;
                }

                renderer = candidate;
                LightingDiagnosticsRendererScratch.Clear();
                return material;
            }

            LightingDiagnosticsRendererScratch.Clear();
            return null;
        }

        private static void AppendMaterialFloat(StringBuilder builder, Material material, string propertyName) {
            builder.Append(' ').Append(propertyName).Append('=');
            if (material == null || !material.HasProperty(propertyName)) {
                builder.Append("<missing>");
                return;
            }

            builder.Append(material.GetFloat(propertyName).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void AppendMaterialVector(StringBuilder builder, Material material, string propertyName) {
            builder.Append(' ').Append(propertyName).Append('=');
            if (material == null || !material.HasProperty(propertyName)) {
                builder.Append("<missing>");
                return;
            }

            Vector4 value = material.GetVector(propertyName);
            builder.Append('(')
                .Append(value.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(value.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(value.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(value.w.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(')');
        }

        private static void AppendMaterialColor(StringBuilder builder, Material material, string propertyName) {
            builder.Append(' ').Append(propertyName).Append('=');
            if (material == null || !material.HasProperty(propertyName)) {
                builder.Append("<missing>");
                return;
            }

            Color value = material.GetColor(propertyName);
            builder.Append('(')
                .Append(value.r.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(value.g.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(value.b.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(value.a.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(')');
        }

        private static void AppendMaterialTexture(StringBuilder builder, Material material, string propertyName) {
            builder.Append("    ").Append(propertyName).Append(": ");
            if (material == null || !material.HasProperty(propertyName)) {
                builder.AppendLine("<missing property>");
                return;
            }

            Texture texture = material.GetTexture(propertyName);
            if (texture == null) {
                builder.AppendLine("<null>");
                return;
            }

            builder.Append("name=").Append(texture.name)
                .Append(" type=").Append(texture.GetType().Name)
                .Append(" size=").Append(texture.width).Append('x').Append(texture.height)
                .Append(" filter=").Append(texture.filterMode)
                .Append(" wrap=").Append(texture.wrapMode)
                .Append(" aniso=").Append(texture.anisoLevel);

            if (texture is Texture2D texture2D) {
                builder.Append(" format=").Append(texture2D.format)
                    .Append(" mipmaps=").Append(texture2D.mipmapCount);
            }

            string activeColorSpace = TryGetActiveTextureColorSpace(texture);
            if (!string.IsNullOrEmpty(activeColorSpace)) {
                builder.Append(" activeColorSpace=").Append(activeColorSpace);
            }

            builder.AppendLine();
        }

        private static string TryGetActiveTextureColorSpace(Texture texture) {
            if (texture == null) {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = typeof(Texture).GetProperty("activeTextureColorSpace", flags);
            if (property == null) {
                return null;
            }

            try {
                object value = property.GetValue(texture);
                return value != null ? value.ToString() : null;
            } catch (TargetInvocationException) {
                return null;
            } catch (ArgumentException) {
                return null;
            }
        }

        private void CreateSky(string loadedMapName, UnityEngine.Camera camera) {
            if (!RoMapSky.TryGetSkyPrefs(loadedMapName, out _)) {
                return;
            }

            var sky = new GameObject("RO Sky");
            sky.transform.SetParent(currentMap.transform, true);
            var mapSky = sky.AddComponent<RoMapSky>();
            mapSky.Initialize(loadedMapName, content.Assets, camera);
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

        private static void SetEnumMember(Type type, object instance, string name, int value) {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(name, Flags);
            if (property != null && property.CanWrite && TryCreateEnumMemberValue(property.PropertyType, value, out object propertyValue)) {
                property.SetValue(instance, propertyValue);
                return;
            }

            FieldInfo field = type.GetField(name, Flags);
            if (field != null && TryCreateEnumMemberValue(field.FieldType, value, out object fieldValue)) {
                field.SetValue(instance, fieldValue);
            }
        }

        private static bool TryCreateEnumMemberValue(Type memberType, int value, out object enumValue) {
            if (memberType == typeof(int)) {
                enumValue = value;
                return true;
            }

            if (!memberType.IsEnum) {
                enumValue = null;
                return false;
            }

            enumValue = Enum.ToObject(memberType, value);
            return true;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object obj) {
            if (obj == null) {
                return;
            }

            if (Application.isPlaying) {
                UnityEngine.Object.Destroy(obj);
            } else {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
