using System;
using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Audio;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Maps;
using ROIO.Models.FileTypes;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;

namespace Ragnarok.Effects {
    public sealed class RoEffectManager : MonoBehaviour {
        private const string EffectSpriteDirectory = "data/sprite/\uC774\uD329\uD2B8";
        private const float RuntimeAssetDatabaseLookupRetrySeconds = 0.5f;
        private static readonly ProfilerMarker SpawnMarker = new ProfilerMarker("RO.Effects.Spawn");
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("RO.Effects.Tick");

        [SerializeField] private int maxPooledSpriteEffects = 32;
        [SerializeField] private int maxPooledActorOverlays = 32;
        [SerializeField] private int maxPooledWeatherLayers = 4;
        [SerializeField] private RoEffectCrowdBudgetMode crowdBudgetMode = RoEffectCrowdBudgetMode.Balanced;

        [SerializeField, Min(1)] private int balancedMaxNewCrowdEffectsPerFrame = 6;
        [SerializeField, Min(1)] private int aggressiveMaxNewCrowdEffectsPerFrame = 3;
        [SerializeField, Min(0)] private int balancedMaxEffectObjectCreatesPerFrame = 2;
        [SerializeField, Min(0)] private int aggressiveMaxEffectObjectCreatesPerFrame = 1;
        [SerializeField] private bool logMissingEffectAssetDiagnostics = true;
        [SerializeField] private bool logResolvedEffectAssetDiagnostics;

        private readonly Stack<RoSpriteEffectRenderer> spriteEffectPool = new Stack<RoSpriteEffectRenderer>();
        private HashSet<RoSpriteEffectRenderer> activeSpriteEffects = new HashSet<RoSpriteEffectRenderer>();
        private readonly Stack<RoActorOverlayRenderer> actorOverlayPool = new Stack<RoActorOverlayRenderer>();
        private readonly HashSet<RoActorOverlayRenderer> activeActorOverlays = new HashSet<RoActorOverlayRenderer>();
        private readonly Stack<RoWeatherParticleLayer> weatherLayerPool = new Stack<RoWeatherParticleLayer>();
        private readonly HashSet<RoWeatherParticleLayer> activeWeatherLayers = new HashSet<RoWeatherParticleLayer>();
        private RoAssetDatabase assets;
        private RoRuntimeMapController cachedRuntimeMapController;
        private UnityEngine.Camera defaultRenderCamera;
        private int effectBudgetFrame = -1;
        private int newCrowdEffectsThisFrame;
        private int effectObjectCreateFrame = -1;
        private int effectObjectCreatesThisFrame;
        private float nextRuntimeAssetDatabaseLookupTime;

        public int ActiveSpriteEffectCount => activeSpriteEffects?.Count ?? 0;
        public int ActiveActorOverlayCount => activeActorOverlays.Count;
        public int PooledActorOverlayCount => actorOverlayPool.Count;
        public int ActiveWeatherLayerCount => activeWeatherLayers.Count;
        public int PooledWeatherLayerCount => weatherLayerPool.Count;
        public int ActiveEffectCount =>
            ActiveSpriteEffectCount +
            ActiveActorOverlayCount +
            ActiveWeatherLayerCount;
        public int MaxPooledActorOverlayCount => Math.Max(0, maxPooledActorOverlays);
        public int MaxPooledWeatherLayerCount => Math.Max(0, maxPooledWeatherLayers);
        public bool HasAssetDatabase => assets != null && assets.IsInitialized;


        public event Action<RoEffectAssetDiagnostic> AssetDiagnosticEmitted;

        private void OnDestroy() {
            cachedRuntimeMapController = null;
            nextRuntimeAssetDatabaseLookupTime = 0f;
        }

        private void Update() {
            long effectUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                using (RoProfilerMarkers.EffectSystemTick.Auto())
                using (TickMarker.Auto()) {
                }
            } finally {
                RoRuntimePerfCounters.AddEffectManagerUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(effectUpdateStartTicks));
            }
        }

        public void Initialize(RoAssetDatabase assetDatabase, UnityEngine.Camera renderCamera = null) {
            assets = assetDatabase;
            defaultRenderCamera = renderCamera;
            nextRuntimeAssetDatabaseLookupTime = 0f;
        }


        public void SetCrowdBudgetMode(RoEffectCrowdBudgetMode mode) {
            crowdBudgetMode = mode;
            effectBudgetFrame = -1;
            newCrowdEffectsThisFrame = 0;
            effectObjectCreateFrame = -1;
            effectObjectCreatesThisFrame = 0;
        }

        /// <summary>
        /// An effect id runs in the original client's engine (Classic/Core, verified against game.exe) or not at all: the
        /// roBrowser effect table is no fallback any more. The ids the port lacks are 3rd-job / later skills Custom does not
        /// have (reference unresolved-ids.txt); the sweep probe records them.
        /// </summary>
        public bool TrySpawnEffectId(int effectId, Vector3 worldPosition, RoEffectSpawnOptions options = default) {
            if (TrySpawnClassicEffect(effectId, worldPosition, options)) {
                return true;
            }

            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"unported {effectId}");
            return false;
        }

        /// <summary>
        /// Effect ids ported from the original client (Classic/Core, verified bit-exact against game.exe)
        /// run in the classic engine; everything else keeps the legacy path.
        /// </summary>
        /// <summary>
        /// Gives the classic engine the loaded map: its RO frame (units × 5, y down, origin at the map centre like
        /// the client) and the GAT for the engine's own GetHeight (bouncing / ground-hugging prims).
        /// </summary>
        public void ConfigureClassicMap(Transform mapRoot, GAT altitude) {
            // no asset check: the map is often installed before the content database is ready (the manager resolves
            // it lazily later), and the engine's ground (GetHeight, CGroundScope cells, lock-on) needs no assets
            if (mapRoot == null || altitude == null || altitude.cells == null) {
                return;
            }

            Classic.RoClassicEffectBridge bridge = Classic.RoClassicEffectBridge.GetOrCreate(transform);
            if (assets != null) {
                bridge.Initialize(assets, defaultRenderCamera);
            }
            int width = (int) altitude.width, height = (int) altitude.height;
            Vector3 centre = mapRoot.TransformPoint(new Vector3(width * 0.5f, 0f, height * 0.5f));
            bridge.SetMapFrame(centre);

            // the loader scales heights by 0.2 (1 cell = 5 RO units); the engine wants the file's raw values (y down)
            var corners = new float[width * height * 4];
            var types = new uint[width * height];
            for (int i = 0; i < width * height; i++) {
                GAT.Cell cell = altitude.cells[i];
                corners[i * 4] = cell.Heights[0] * 5f;
                corners[i * 4 + 1] = cell.Heights[1] * 5f;
                corners[i * 4 + 2] = cell.Heights[2] * 5f;
                corners[i * 4 + 3] = cell.Heights[3] * 5f;
                types[i] = cell.type;
            }

            bridge.SetGround(width, height, corners, types);
        }

        /// <summary>
        /// The map's RSW effect records into the classic engine (file order). The loader stores positions / 5 and the
        /// emit speed × 10; the engine wants the file's raw floats (the client's world frame, y down).
        /// </summary>
        public void SetClassicMapEffects(string mapName, ROIO.Models.FileTypes.RSW world) {
            var records = new List<Classic.RswEffectRecord>(world?.effects?.Count ?? 0);
            if (world?.effects != null) {
                foreach (ROIO.Models.FileTypes.RSW.Effect effect in world.effects) {
                    if (effect?.pos == null || effect.pos.Length < 3) {
                        continue;
                    }

                    float[] p = effect.param;
                    records.Add(new Classic.RswEffectRecord(
                        effect.name, effect.pos[0] * 5f, effect.pos[1] * 5f, effect.pos[2] * 5f, effect.id, effect.delay / 10f,
                        p != null && p.Length > 0 ? p[0] : 0f, p != null && p.Length > 1 ? p[1] : 0f,
                        p != null && p.Length > 2 ? p[2] : 0f, p != null && p.Length > 3 ? p[3] : 0f));
                }
            }

            Classic.RoClassicEffectBridge.GetOrCreate(transform).SetMapEffects(mapName, records);
        }

        /// <summary>The local player's account id, for effects that treat the player specially (DAT_0154f2b4 in the client).</summary>
        public void SetClassicLocalPlayer(uint accountId) {
            if (accountId != 0 && Classic.RoClassicEffectBridge.Instance != null) {
                Classic.RoClassicEffectBridge.Instance.LocalPlayerId = accountId;
            }
        }

        private bool TrySpawnClassicEffect(int effectId, Vector3 worldPosition, RoEffectSpawnOptions options) =>
            TrySpawnClassicEffect(effectId, worldPosition, options, out _);

        /// <summary>
        /// Launches <paramref name="effectId"/> in the original-client engine when it is ported; <paramref name="handle"/>
        /// then identifies it for <see cref="EndClassicEffect"/> (persistent unit effects, cast circles).
        /// </summary>
        public bool TrySpawnClassicEffect(int effectId, Vector3 worldPosition, RoEffectSpawnOptions options, out uint handle) {
            handle = 0;
            if (assets == null || !Classic.RoClassicEffectBridge.CanHandle(effectId)) {
                return false;
            }

            Classic.RoClassicEffectBridge bridge = Classic.RoClassicEffectBridge.GetOrCreate(transform);
            bridge.Initialize(assets, defaultRenderCamera);
            return bridge.TrySpawn(effectId, worldPosition, options, out handle);
        }

        /// <summary>Ends a classic-engine effect started with <see cref="TrySpawnClassicEffect(int, Vector3, RoEffectSpawnOptions, out uint)"/>.</summary>
        public bool EndClassicEffect(uint handle) =>
            handle != 0 && Classic.RoClassicEffectBridge.Instance != null && Classic.RoClassicEffectBridge.Instance.EndEffect(handle);

        public bool IsClassicEffectAlive(uint handle) =>
            handle != 0 && Classic.RoClassicEffectBridge.Instance != null && Classic.RoClassicEffectBridge.Instance.IsEffectAlive(handle);

        /// <summary>The actor's option bits for the classic engine's actor renderer (Chase Walk footprints ...).</summary>
        public void SetClassicActorOption(uint actorId, int option, Transform anchor) {
            if (assets == null || (option == 0 && Classic.RoClassicEffectBridge.Instance == null)) {
                return;
            }

            Classic.RoClassicEffectBridge bridge = Classic.RoClassicEffectBridge.GetOrCreate(transform);
            bridge.Initialize(assets, defaultRenderCamera);
            bridge.SetActorOption(actorId, option, anchor);
        }

        /// <summary>The actor's own tint in the classic engine (CRenderObject::SetARGB, −1 keeps a channel).</summary>
        public void SetClassicActorArgb(uint actorId, int a, int r, int g, int b, Transform anchor) {
            if (assets == null) {
                return;
            }

            Classic.RoClassicEffectBridge bridge = Classic.RoClassicEffectBridge.GetOrCreate(transform);
            bridge.Initialize(assets, defaultRenderCamera);
            bridge.SetActorArgb(actorId, a, r, g, b, anchor);
        }

        /// <summary>Ends the oldest classic effect of type <paramref name="effectId"/> on the actor (actor msg 0x6d).</summary>
        public bool RemoveOldestClassicEffectById(uint actorId, int effectId) =>
            Classic.RoClassicEffectBridge.Instance != null && Classic.RoClassicEffectBridge.Instance.RemoveEffectById(actorId, effectId);

        /// <summary>Ends every classic effect of type <paramref name="effectId"/> on the actor (actor msg 0x8f).</summary>
        public int RemoveClassicEffectsById(uint actorId, int effectId) =>
            Classic.RoClassicEffectBridge.Instance != null ? Classic.RoClassicEffectBridge.Instance.RemoveAllEffectsById(actorId, effectId) : 0;

        /// <summary>Whether a classic effect of type <paramref name="effectId"/> runs on the actor (actor msg 0x78).</summary>
        public bool HasClassicEffect(uint actorId, int effectId) =>
            Classic.RoClassicEffectBridge.Instance != null && Classic.RoClassicEffectBridge.Instance.HasEffectOnActor(actorId, effectId);

        /// <summary>The effect msg 0x2c(0xffff, sel) colour of a running classic effect (0..255 per channel).</summary>
        public bool SetClassicEffectColor(uint handle, float r, float g, float b) =>
            handle != 0 && Classic.RoClassicEffectBridge.Instance != null && Classic.RoClassicEffectBridge.Instance.SetEffectColor(handle, r, g, b);

        /// <summary>A shared classic-engine owner for a skill unit's effects (see <see cref="ReleaseClassicUnitOwner"/>).</summary>
        public uint CreateClassicUnitOwner(Vector3 worldPosition) {
            if (assets == null) {
                return 0;
            }

            Classic.RoClassicEffectBridge bridge = Classic.RoClassicEffectBridge.GetOrCreate(transform);
            bridge.Initialize(assets, defaultRenderCamera);
            return bridge.CreateSharedOwner(worldPosition);
        }

        /// <summary>The unit disappeared: every classic effect launched on its owner ends (the client's msg 0x18).</summary>
        public void ReleaseClassicUnitOwner(uint owner) {
            if (owner != 0 && Classic.RoClassicEffectBridge.Instance != null) {
                Classic.RoClassicEffectBridge.Instance.ReleaseSharedOwner(owner);
            }
        }

        /// <summary>The original client's CGroundScope for a ground-target cast (cell coordinates, cast time in ms).</summary>
        public bool TryLaunchGroundScope(int cellX, int cellY, int skillId, int delayMilliseconds, uint casterId) {
            if (assets == null) {
                return false;
            }

            Classic.RoClassicEffectBridge bridge = Classic.RoClassicEffectBridge.GetOrCreate(transform);
            bridge.Initialize(assets, defaultRenderCamera);
            return bridge.LaunchGroundScope(cellX, cellY, skillId, delayMilliseconds, casterId) != 0;
        }

        /// <summary>Lets homing classic effects track <paramref name="anchor"/> for actor <paramref name="actorId"/>.</summary>
        public void RegisterClassicActorAnchor(uint actorId, Transform anchor) {
            if (assets == null) {
                return;
            }

            Classic.RoClassicEffectBridge bridge = Classic.RoClassicEffectBridge.GetOrCreate(transform);
            bridge.Initialize(assets, defaultRenderCamera);
            bridge.RegisterActorAnchor(actorId, anchor);
        }

        public RoSpriteEffectRenderer SpawnSpriteEffect(string fileName, Vector3 worldPosition, RoEffectSpawnOptions options = default, bool loop = false) {
            if (!TryLoadSpriteEffectAtlas(fileName, out RoSpriteAtlasCacheEntry atlasEntry)) {
                return null;
            }

            return SpawnSpriteEffect(atlasEntry, worldPosition, options, loop);
        }

        public RoSpriteEffectRenderer SpawnSpriteEffect(RoSpriteAtlasCacheEntry atlasEntry, Vector3 worldPosition, RoEffectSpawnOptions options = default, bool loop = false) {
            using (SpawnMarker.Auto()) {
                EnsureEffectRuntimeCollections();
                if (!TryConsumeCrowdEffectBudget(activeSpriteEffects.Count, balancedActiveLimit: 96, aggressiveActiveLimit: 48)) {
                    return null;
                }

                var sprite = BorrowSpriteEffectRenderer("RO SPR Effect");
                if (sprite == null) {
                    return null;
                }

                sprite.Finished -= HandleSpriteEffectFinished;
                sprite.Finished += HandleSpriteEffectFinished;
                sprite.transform.SetParent(options.Parent != null ? options.Parent : transform, false);
                sprite.transform.position = worldPosition;
                sprite.transform.localScale = Vector3.one * options.EffectiveSkillVisualScale;
                sprite.Initialize(
                    atlasEntry,
                    worldPosition,
                    options.RenderCamera != null || defaultRenderCamera == null
                        ? options
                        : WithDefaultRenderCamera(options),
                    loop
                );
                activeSpriteEffects.Add(sprite);
                return sprite;
            }
        }

        private RoEffectSpawnOptions WithDefaultRenderCamera(RoEffectSpawnOptions options) {
            options.RenderCamera = defaultRenderCamera;
            return options;
        }


        public RoActorOverlayRenderer SpawnStatusTextOverlay(string text, Vector3 worldPosition, float durationSeconds = 2.2f, RoEffectSpawnOptions options = default) {
            return SpawnActorOverlay(RoActorOverlaySpec.StatusText(text, durationSeconds), worldPosition, options);
        }

        public RoActorOverlayRenderer SpawnActorOverlay(RoActorOverlaySpec spec, Vector3 worldPosition, RoEffectSpawnOptions options = default) {
            using (SpawnMarker.Auto()) {
                if (!TryConsumeCrowdEffectBudget(activeActorOverlays.Count, balancedActiveLimit: 64, aggressiveActiveLimit: 32)) {
                    return null;
                }

                RoActorOverlayRenderer renderer = BorrowActorOverlayRenderer();
                if (renderer == null) {
                    return null;
                }

                renderer.Finished -= HandleActorOverlayFinished;
                renderer.Finished += HandleActorOverlayFinished;
                renderer.transform.SetParent(options.Parent != null ? options.Parent : transform, false);
                renderer.Initialize(
                    spec,
                    worldPosition,
                    assets,
                    options.FollowTarget,
                    options.FollowLocalOffset,
                    options.RenderCamera != null ? options.RenderCamera : defaultRenderCamera
                );
                activeActorOverlays.Add(renderer);
                return renderer;
            }
        }

        public RoWeatherParticleLayer SpawnWeatherLayer(RoWeatherParticleSpec spec, Vector3 worldPosition = default, RoEffectSpawnOptions options = default) {
            using (SpawnMarker.Auto()) {
                RoWeatherParticleLayer layer = BorrowWeatherLayer();
                if (layer == null) {
                    return null;
                }

                layer.Finished -= HandleWeatherLayerFinished;
                layer.Finished += HandleWeatherLayerFinished;
                layer.transform.SetParent(options.Parent != null ? options.Parent : transform, false);
                layer.Initialize(
                    spec,
                    worldPosition,
                    options.FollowTarget,
                    options.FollowLocalOffset,
                    options.RenderCamera != null ? options.RenderCamera : defaultRenderCamera
                );
                activeWeatherLayers.Add(layer);
                return layer;
            }
        }

        public int WarmActorOverlayPool(int count) {
            if (count <= 0) {
                return 0;
            }

            int warmed = 0;
            while (warmed < count && actorOverlayPool.Count < maxPooledActorOverlays) {
                RoActorOverlayRenderer renderer = CreateActorOverlayRenderer();
                renderer.gameObject.SetActive(false);
                actorOverlayPool.Push(renderer);
                warmed++;
            }

            return warmed;
        }

        public int WarmWeatherLayerPool(int count) {
            if (count <= 0) {
                return 0;
            }

            int warmed = 0;
            while (warmed < count && weatherLayerPool.Count < maxPooledWeatherLayers) {
                RoWeatherParticleLayer layer = CreateWeatherLayer();
                layer.gameObject.SetActive(false);
                weatherLayerPool.Push(layer);
                warmed++;
            }

            return warmed;
        }

        private bool TryLoadSpriteEffectAtlas(string fileName, out RoSpriteAtlasCacheEntry atlasEntry) {
            atlasEntry = null;
            if (!HasAssetDatabase && !TryResolveRuntimeAssetDatabase()) {
                EmitAssetDiagnostic(new RoEffectAssetDiagnostic(
                    RoEffectAssetDiagnosticKind.AssetDatabaseMissing,
                    fileName,
                    nameof(SPR),
                    context: "sprite effect",
                    candidatePaths: BuildPrimitiveSpriteLookupCandidates(fileName)
                ));
                return false;
            }

            IReadOnlyList<string> spriteCandidates = BuildPrimitiveSpriteCandidates(fileName);
            string firstExistingSpritePath = string.Empty;
            for (int i = 0; i < spriteCandidates.Count; i++) {
                string candidate = spriteCandidates[i];
                if (!assets.TryGetManifestEntry(candidate, out RoAssetManifestEntry manifestEntry)) {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(firstExistingSpritePath)) {
                    firstExistingSpritePath = manifestEntry.Path;
                }

                if (RoSpriteAtlasCache.TryGetOrBuild(assets, manifestEntry.Path, out atlasEntry)) {
                    EmitAssetDiagnostic(new RoEffectAssetDiagnostic(
                        RoEffectAssetDiagnosticKind.TextureFound,
                        fileName,
                        nameof(SPR),
                        manifestEntry.Path,
                        "sprite effect",
                        BuildPrimitiveSpriteLookupCandidates(fileName)
                    ));
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(firstExistingSpritePath)) {
                EmitAssetDiagnostic(new RoEffectAssetDiagnostic(
                    RoEffectAssetDiagnosticKind.TextureLoadFailed,
                    fileName,
                    nameof(SPR),
                    firstExistingSpritePath,
                    "sprite effect atlas",
                    BuildPrimitiveSpriteLookupCandidates(fileName)
                ));
                return false;
            }

            EmitAssetDiagnostic(new RoEffectAssetDiagnostic(
                RoEffectAssetDiagnosticKind.TextureMissing,
                fileName,
                nameof(SPR),
                context: "sprite effect",
                candidatePaths: BuildPrimitiveSpriteLookupCandidates(fileName)
            ));
            return false;
        }

        private bool TryConsumeCrowdEffectBudget(int activeCount, int balancedActiveLimit, int aggressiveActiveLimit) {
            if (crowdBudgetMode == RoEffectCrowdBudgetMode.Off || !Application.isPlaying) {
                return true;
            }

            int frame = Application.isPlaying ? Time.frameCount : -1;
            if (frame != effectBudgetFrame) {
                effectBudgetFrame = frame;
                newCrowdEffectsThisFrame = 0;
            }

            int activeLimit = crowdBudgetMode == RoEffectCrowdBudgetMode.Aggressive
                ? Math.Max(1, aggressiveActiveLimit)
                : Math.Max(1, balancedActiveLimit);
            if (activeCount >= activeLimit) {
                RoRuntimePerfCounters.IncrementEffectPoolDropped();
                return false;
            }

            int newPerFrameLimit = crowdBudgetMode == RoEffectCrowdBudgetMode.Aggressive
                ? Math.Max(1, aggressiveMaxNewCrowdEffectsPerFrame)
                : Math.Max(1, balancedMaxNewCrowdEffectsPerFrame);
            if (newCrowdEffectsThisFrame >= newPerFrameLimit) {
                RoRuntimePerfCounters.IncrementEffectPoolDropped();
                return false;
            }

            newCrowdEffectsThisFrame++;
            return true;
        }

        private bool TryConsumeEffectObjectCreateBudget() {
            if (crowdBudgetMode == RoEffectCrowdBudgetMode.Off || !Application.isPlaying) {
                return true;
            }

            if (Time.unscaledDeltaTime > 0f && Time.unscaledDeltaTime > 1f / 45f) {
                RoRuntimePerfCounters.IncrementEffectPoolDropped();
                return false;
            }

            int frame = Time.frameCount;
            if (frame != effectObjectCreateFrame) {
                effectObjectCreateFrame = frame;
                effectObjectCreatesThisFrame = 0;
            }

            int createLimit = crowdBudgetMode == RoEffectCrowdBudgetMode.Aggressive
                ? Math.Max(0, aggressiveMaxEffectObjectCreatesPerFrame)
                : Math.Max(0, balancedMaxEffectObjectCreatesPerFrame);
            if (effectObjectCreatesThisFrame >= createLimit) {
                RoRuntimePerfCounters.IncrementEffectPoolDropped();
                return false;
            }

            effectObjectCreatesThisFrame++;
            return true;
        }

        private RoSpriteEffectRenderer BorrowSpriteEffectRenderer(string objectName) {
            while (spriteEffectPool.Count > 0) {
                RoSpriteEffectRenderer pooled = spriteEffectPool.Pop();
                if (pooled != null) {
                    pooled.name = objectName;
                    pooled.gameObject.SetActive(true);
                    RoRuntimePerfCounters.IncrementEffectPoolReused();
                    return pooled;
                }
            }

            if (!TryConsumeEffectObjectCreateBudget()) {
                return null;
            }

            return CreateSpriteEffectRenderer(objectName);
        }

        private RoSpriteEffectRenderer CreateSpriteEffectRenderer(string objectName) {
            var effectObject = new GameObject(objectName);
            effectObject.transform.SetParent(transform, false);
            RoRuntimePerfCounters.IncrementEffectPoolCreated();
            return effectObject.AddComponent<RoSpriteEffectRenderer>();
        }

        private RoActorOverlayRenderer BorrowActorOverlayRenderer() {
            while (actorOverlayPool.Count > 0) {
                RoActorOverlayRenderer pooled = actorOverlayPool.Pop();
                if (pooled != null) {
                    pooled.gameObject.SetActive(true);
                    RoRuntimePerfCounters.IncrementEffectPoolReused();
                    return pooled;
                }
            }

            if (!TryConsumeEffectObjectCreateBudget()) {
                return null;
            }

            return CreateActorOverlayRenderer();
        }

        private RoWeatherParticleLayer BorrowWeatherLayer() {
            while (weatherLayerPool.Count > 0) {
                RoWeatherParticleLayer pooled = weatherLayerPool.Pop();
                if (pooled != null) {
                    pooled.gameObject.SetActive(true);
                    RoRuntimePerfCounters.IncrementEffectPoolReused();
                    return pooled;
                }
            }

            if (!TryConsumeEffectObjectCreateBudget()) {
                return null;
            }

            return CreateWeatherLayer();
        }

        private RoActorOverlayRenderer CreateActorOverlayRenderer() {
            var effectObject = new GameObject("RO Actor Overlay");
            effectObject.transform.SetParent(transform, false);
            RoRuntimePerfCounters.IncrementEffectPoolCreated();
            return effectObject.AddComponent<RoActorOverlayRenderer>();
        }

        private RoWeatherParticleLayer CreateWeatherLayer() {
            var effectObject = new GameObject("RO Weather Particles");
            effectObject.transform.SetParent(transform, false);
            RoRuntimePerfCounters.IncrementEffectPoolCreated();
            return effectObject.AddComponent<RoWeatherParticleLayer>();
        }

        private bool TryResolveRuntimeAssetDatabase() {
            RoRuntimeMapController mapController = ResolveRuntimeMapControllerWithRetry();
            RoAssetDatabase runtimeAssets = mapController != null && mapController.Content != null && mapController.Content.IsInitialized
                ? mapController.Content.Assets
                : null;
            if (runtimeAssets == null || !runtimeAssets.IsInitialized) {
                return false;
            }

            Initialize(runtimeAssets, RoEffectCameraCache.Resolve(defaultRenderCamera));
            return true;
        }

        private RoRuntimeMapController ResolveRuntimeMapControllerWithRetry() {
            if (cachedRuntimeMapController != null) {
                return cachedRuntimeMapController;
            }

            float now = Time.unscaledTime;
            if (now < nextRuntimeAssetDatabaseLookupTime) {
                return null;
            }

            cachedRuntimeMapController = FindFirstObjectByType<RoRuntimeMapController>();
            nextRuntimeAssetDatabaseLookupTime = cachedRuntimeMapController != null
                ? 0f
                : now + RuntimeAssetDatabaseLookupRetrySeconds;
            return cachedRuntimeMapController;
        }

        public static IReadOnlyList<string> BuildPrimitiveSpriteCandidates(string texturePathOrName) {
            if (string.IsNullOrWhiteSpace(texturePathOrName)) {
                return Array.Empty<string>();
            }

            string path = texturePathOrName.Trim().Replace('\\', '/');
            path = StripPrimitiveSpriteFrameSuffix(path);
            if (path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".str", StringComparison.OrdinalIgnoreCase)) {
                return Array.Empty<string>();
            }

            if (path.EndsWith(".act", StringComparison.OrdinalIgnoreCase)) {
                path = path.Substring(0, path.Length - 4) + ".spr";
            } else if (!path.EndsWith(".spr", StringComparison.OrdinalIgnoreCase)) {
                path += ".spr";
            }

            if (path.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) {
                return new[] { RoAssetPath.Normalize(NormalizePathSegments(path)) };
            }

            if (path.IndexOf('/', StringComparison.Ordinal) < 0) {
                return new[] { RoAssetPath.Normalize(EffectSpriteDirectory + "/" + path) };
            }

            if (path.StartsWith("../", StringComparison.Ordinal) ||
                path.StartsWith("./", StringComparison.Ordinal)) {
                return new[] { RoAssetPath.Normalize(NormalizePathSegments(EffectSpriteDirectory + "/" + path)) };
            }

            return new[] {
                RoAssetPath.Normalize(NormalizePathSegments(path)),
                RoAssetPath.Normalize(NormalizePathSegments("data/sprite/" + path))
            };
        }

        public static IReadOnlyList<string> BuildPrimitiveSpriteLookupCandidates(string texturePathOrName) {
            IReadOnlyList<string> spriteCandidates = BuildPrimitiveSpriteCandidates(texturePathOrName);
            if (spriteCandidates.Count == 0) {
                return Array.Empty<string>();
            }

            var lookupCandidates = new List<string>();
            AddLookupCandidates(lookupCandidates, spriteCandidates);
            return lookupCandidates;
        }


        private static void AddLookupCandidates(List<string> lookupCandidates, IReadOnlyList<string> candidates) {
            for (int i = 0; i < candidates.Count; i++) {
                AddUniqueCandidate(lookupCandidates, candidates[i]);
                IReadOnlyList<string> expanded = RoAssetPath.GetLookupCandidates(candidates[i]);
                for (int j = 0; j < expanded.Count; j++) {
                    AddUniqueCandidate(lookupCandidates, expanded[j]);
                }
            }
        }

        private static string StripPrimitiveSpriteFrameSuffix(string texturePath) {
            if (string.IsNullOrWhiteSpace(texturePath)) {
                return string.Empty;
            }

            int marker = texturePath.LastIndexOf("#frame=", StringComparison.OrdinalIgnoreCase);
            return marker >= 0 ? texturePath.Substring(0, marker) : texturePath;
        }

        private static string NormalizePathSegments(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            string[] segments = path.Replace('\\', '/').Split('/');
            var normalized = new List<string>(segments.Length);
            for (int i = 0; i < segments.Length; i++) {
                string segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment) || segment == ".") {
                    continue;
                }

                if (segment == "..") {
                    if (normalized.Count > 0) {
                        normalized.RemoveAt(normalized.Count - 1);
                    }

                    continue;
                }

                normalized.Add(segment);
            }

            if (normalized.Count == 0) {
                return string.Empty;
            }

            string result = normalized[0];
            for (int i = 1; i < normalized.Count; i++) {
                result += "/" + normalized[i];
            }

            return result;
        }

        private static void AddUniqueCandidate(List<string> candidates, string candidate) {
            if (!string.IsNullOrWhiteSpace(candidate) && !candidates.Contains(candidate)) {
                candidates.Add(candidate);
            }
        }

        private void EmitAssetDiagnostic(RoEffectAssetDiagnostic diagnostic) {
            if (diagnostic == null) {
                return;
            }

            AssetDiagnosticEmitted?.Invoke(diagnostic);
            if (diagnostic.IsWarning && logMissingEffectAssetDiagnostics) {
                RoRuntimeDiagnostics.IncrementDebugLogLine();
                Debug.LogWarning(diagnostic.BuildMessage(), this);
            } else if (diagnostic.IsResolved &&
                logResolvedEffectAssetDiagnostics &&
                RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                RoRuntimeDiagnostics.IncrementDebugLogLine();
                Debug.Log(diagnostic.BuildMessage(), this);
            }
        }

        private void HandleSpriteEffectFinished(RoSpriteEffectRenderer renderer) {
            if (renderer == null) {
                return;
            }

            renderer.Finished -= HandleSpriteEffectFinished;
            activeSpriteEffects?.Remove(renderer);
            renderer.transform.SetParent(transform, false);
            renderer.gameObject.SetActive(false);
            if (spriteEffectPool.Count >= maxPooledSpriteEffects) {
                DestroyRuntimeObject(renderer.gameObject);
                return;
            }

            spriteEffectPool.Push(renderer);
        }

        private void HandleActorOverlayFinished(RoActorOverlayRenderer renderer) {
            if (renderer == null) {
                return;
            }

            renderer.Finished -= HandleActorOverlayFinished;
            activeActorOverlays.Remove(renderer);
            renderer.transform.SetParent(transform, false);
            renderer.gameObject.SetActive(false);
            if (actorOverlayPool.Count >= maxPooledActorOverlays) {
                DestroyRuntimeObject(renderer.gameObject);
                return;
            }

            actorOverlayPool.Push(renderer);
        }

        private void HandleWeatherLayerFinished(RoWeatherParticleLayer layer) {
            if (layer == null) {
                return;
            }

            layer.Finished -= HandleWeatherLayerFinished;
            activeWeatherLayers.Remove(layer);
            layer.transform.SetParent(transform, false);
            layer.gameObject.SetActive(false);
            if (weatherLayerPool.Count >= maxPooledWeatherLayers) {
                DestroyRuntimeObject(layer.gameObject);
                return;
            }

            weatherLayerPool.Push(layer);
        }


        private void EnsureEffectRuntimeCollections() {
            activeSpriteEffects ??= new HashSet<RoSpriteEffectRenderer>();
        }


        private static void DestroyRuntimeObject(UnityEngine.Object obj) {
            if (obj == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(obj);
            } else {
                DestroyImmediate(obj);
            }
        }

        private static void SetFloatIfPropertyExists(Material material, string property, float value) {
            if (material != null && material.HasProperty(property)) {
                material.SetFloat(property, value);
            }
        }

        private static void SetTextureIfPropertyExists(Material material, string property, Texture texture) {
            if (material != null && material.HasProperty(property)) {
                material.SetTexture(property, texture);
            }
        }
    }
}
