using System;
using System.Collections.Generic;
using Ragnarok.Maps;
using UnityEngine;

namespace Ragnarok.Effects {
    [DisallowMultipleComponent]
    public sealed class RoAmbientParticleController : MonoBehaviour {
        private const int MaxActiveLeafEmitters = RoAmbientParticleMath.DefaultMaxActiveEmitters;
        private const int MaxEmitsPerFrame = 16;
        private const float SpawnRadiusScale = 0.72f;

        private readonly List<RoAmbientParticleEmitter> cachedEmitters = new List<RoAmbientParticleEmitter>(128);
        private readonly List<RoAmbientParticleEmitterSelection> activeSelections = new List<RoAmbientParticleEmitterSelection>(MaxActiveLeafEmitters);
        private readonly List<RoModelSurfaceInfo> surfaceScratch = new List<RoModelSurfaceInfo>(512);
        private readonly Dictionary<string, EmitterBuilder> emitterBuilders = new Dictionary<string, EmitterBuilder>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Random random = new System.Random(0x51EA57);

        private RoEffectManager effectManager;
        private GameObject modelRoot;
        private UnityEngine.Camera renderCamera;
        private RoAmbientParticleSettings settings;
        private RoWeatherWindSettings windSettings;
        private RoWeatherParticleLayer leafLayer;
        private ParticleSystem.Particle[] particleBuffer;
        private float emissionAccumulator;
        private int lastSettingsSignature;
        private int modelRootInstanceId;
        private bool skylessMap;
        private bool initialized;

        public RoAmbientParticleSettings Settings => settings;
        public bool IsSkylessMap => skylessMap;
        public bool IsRuntimeEnabled => RoAmbientParticleMath.ResolveEnabled(settings, skylessMap);
        public int CachedEmitterCount => cachedEmitters.Count;
        public int ActiveEmitterCount { get; private set; }
        public int LiveParticleCount => leafLayer != null ? leafLayer.LiveParticleCount : 0;

        private void Update() {
            if (!initialized) {
                return;
            }

            if (!IsRuntimeEnabled) {
                ActiveEmitterCount = 0;
                StopLeaves();
                return;
            }

            UnityEngine.Camera camera = RoEffectCameraCache.Resolve(renderCamera);
            if (camera == null || cachedEmitters.Count == 0) {
                ActiveEmitterCount = 0;
                return;
            }

            RoAmbientParticleMath.SelectNearestEmitters(
                cachedEmitters,
                camera.transform.position,
                settings.EmitterActivationRadius,
                MaxActiveLeafEmitters,
                activeSelections);
            ActiveEmitterCount = activeSelections.Count;

            if (leafLayer == null && ActiveEmitterCount > 0) {
                EnsureLeafLayer();
            }

            if (leafLayer == null) {
                return;
            }

            float deltaSeconds = Time.deltaTime;
            UpdateLeafVelocities(deltaSeconds);
            EmitLeaves(deltaSeconds);
        }

        private void OnDisable() {
            StopLeaves();
        }

        private void OnDestroy() {
            StopLeaves();
        }

        public void Initialize(
            RoEffectManager manager,
            GameObject models,
            UnityEngine.Camera camera,
            RoAmbientParticleSettings ambientSettings,
            bool skyless,
            RoWeatherWindSettings weatherWindSettings = null
        ) {
            effectManager = manager;
            modelRoot = models;
            renderCamera = RoEffectCameraCache.Resolve(camera);
            settings = ambientSettings ?? new RoAmbientParticleSettings();
            settings.EnsureCurrentDefaults();
            windSettings = weatherWindSettings ?? new RoWeatherWindSettings();
            windSettings.EnsureCurrentDefaults();
            skylessMap = skyless;
            initialized = true;
            emissionAccumulator = 0f;
            lastSettingsSignature = settings.Signature;
            RebuildEmitterCacheIfNeeded(force: true);
            if (!IsRuntimeEnabled) {
                StopLeaves();
            }
        }

        public void SyncSettings(
            RoAmbientParticleSettings ambientSettings,
            GameObject models = null,
            UnityEngine.Camera camera = null,
            bool? skyless = null,
            RoWeatherWindSettings weatherWindSettings = null
        ) {
            if (!initialized) {
                return;
            }

            settings = ambientSettings ?? settings ?? new RoAmbientParticleSettings();
            settings.EnsureCurrentDefaults();
            windSettings = weatherWindSettings ?? windSettings ?? new RoWeatherWindSettings();
            windSettings.EnsureCurrentDefaults();

            if (models != null && !ReferenceEquals(modelRoot, models)) {
                modelRoot = models;
            }

            if (camera != null) {
                renderCamera = RoEffectCameraCache.Resolve(camera);
            }

            if (skyless.HasValue) {
                skylessMap = skyless.Value;
            }

            RebuildEmitterCacheIfNeeded(force: false);

            int signature = settings.Signature;
            bool settingsChanged = signature != lastSettingsSignature;
            lastSettingsSignature = signature;

            if (!IsRuntimeEnabled) {
                ActiveEmitterCount = 0;
                emissionAccumulator = 0f;
                StopLeaves();
                return;
            }

            if (leafLayer != null && settingsChanged) {
                leafLayer.SyncSpec(settings.BuildLeavesSpec());
                EnsureParticleBuffer();
            }
        }

        public void ClearRuntime() {
            initialized = false;
            ActiveEmitterCount = 0;
            emissionAccumulator = 0f;
            cachedEmitters.Clear();
            activeSelections.Clear();
            modelRoot = null;
            modelRootInstanceId = 0;
            StopLeaves();
        }

        public void StopAmbient() {
            ActiveEmitterCount = 0;
            emissionAccumulator = 0f;
            StopLeaves();
        }

        public static List<RoAmbientParticleEmitter> DiscoverLeafEmitters(GameObject root, RoWindSwayCatalog catalog = null) {
            var result = new List<RoAmbientParticleEmitter>(64);
            if (root == null) {
                return result;
            }

            var surfaces = new List<RoModelSurfaceInfo>(512);
            root.GetComponentsInChildren(true, surfaces);
            DiscoverLeafEmitters(surfaces, catalog ?? RoWindSwayCatalog.Load(), result);
            return result;
        }

        public static void DiscoverLeafEmitters(
            IReadOnlyList<RoModelSurfaceInfo> surfaces,
            RoWindSwayCatalog catalog,
            List<RoAmbientParticleEmitter> results
        ) {
            results?.Clear();
            if (surfaces == null || catalog == null || results == null) {
                return;
            }

            var builders = new Dictionary<string, EmitterBuilder>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < surfaces.Count; i++) {
                RoModelSurfaceInfo surface = surfaces[i];
                if (!IsLeafEmitterSurface(surface, catalog)) {
                    continue;
                }

                string key = BuildEmitterKey(surface);
                if (!builders.TryGetValue(key, out EmitterBuilder builder)) {
                    builder = new EmitterBuilder(surface.ModelFile, surface.TextureName);
                    builders.Add(key, builder);
                }

                builder.Add(surface);
            }

            foreach (EmitterBuilder builder in builders.Values) {
                if (builder.TryBuild(out RoAmbientParticleEmitter emitter)) {
                    results.Add(emitter);
                }
            }
        }

        private void EnsureLeafLayer() {
            if (leafLayer != null || effectManager == null) {
                return;
            }

            leafLayer = effectManager.SpawnWeatherLayer(
                settings.BuildLeavesSpec(),
                Vector3.zero,
                new RoEffectSpawnOptions {
                    Parent = transform,
                    RenderCamera = renderCamera
                });
            leafLayer?.SetIntensity(1f);
            EnsureParticleBuffer();
        }

        private void StopLeaves() {
            if (leafLayer == null) {
                return;
            }

            leafLayer.Stop();
            leafLayer = null;
        }

        private void EmitLeaves(float deltaSeconds) {
            if (ActiveEmitterCount <= 0 || settings.LeavesPerTreePerMinute <= 0f || deltaSeconds <= 0f) {
                return;
            }

            float ratePerSecond = settings.LeavesPerTreePerMinute / 60f * ActiveEmitterCount;
            emissionAccumulator = RoAmbientParticleMath.ClampEmissionBacklog(
                emissionAccumulator + ratePerSecond * deltaSeconds,
                MaxEmitsPerFrame);
            int emitCount = Mathf.Min(Mathf.FloorToInt(emissionAccumulator), MaxEmitsPerFrame);
            int particleBudget = settings.MaxLeafParticles - LiveParticleCount;
            emitCount = Mathf.Min(emitCount, Mathf.Max(0, particleBudget));
            if (emitCount <= 0) {
                emissionAccumulator = RoAmbientParticleMath.ClampEmissionBacklog(emissionAccumulator, MaxEmitsPerFrame);
                return;
            }

            emissionAccumulator = RoAmbientParticleMath.ClampEmissionBacklog(emissionAccumulator - emitCount, MaxEmitsPerFrame);
            for (int i = 0; i < emitCount; i++) {
                RoAmbientParticleEmitter emitter = cachedEmitters[activeSelections[random.Next(activeSelections.Count)].Index];
                leafLayer.Emit(BuildLeafEmitParams(emitter), 1);
            }
        }

        private ParticleSystem.EmitParams BuildLeafEmitParams(RoAmbientParticleEmitter emitter) {
            Vector2 windDirection = windSettings != null ? windSettings.EffectiveDirection : RoWeatherWindSettings.DefaultDirection;
            float windStrength = windSettings != null ? windSettings.Strength : 0f;
            float gustFactor = RoAmbientParticleMath.ComputeCpuGustFactor(
                emitter.Anchor,
                windDirection,
                Time.time,
                RoWindSwayGlobals.CurrentParams.w);

            float angle = NextFloat(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(NextFloat(0f, 1f)) * emitter.Radius * SpawnRadiusScale;
            Vector3 jitter = new Vector3(Mathf.Cos(angle) * radius, NextFloat(0.35f, 1.15f), Mathf.Sin(angle) * radius);
            Vector2 lateral = RandomUnitCircle() * NextFloat(0.02f, 0.16f);
            Vector2 windVelocity = windDirection * (windStrength * settings.WindInfluence * gustFactor * 0.18f);

            var emit = new ParticleSystem.EmitParams {
                position = emitter.Anchor + jitter,
                velocity = new Vector3(windVelocity.x + lateral.x, -settings.LeafFallSpeed * NextFloat(0.75f, 1.15f), windVelocity.y + lateral.y),
                startLifetime = settings.LeafLifetimeSeconds * NextFloat(0.75f, 1.2f),
                startSize = NextFloat(0.24f, 0.42f),
                startColor = new Color(1f, 0.96f, 0.78f, 1f),
                rotation = NextFloat(0f, Mathf.PI * 2f),
                angularVelocity = NextFloat(-4.5f, 4.5f),
                randomSeed = (uint)random.Next(1, int.MaxValue)
            };
            return emit;
        }

        private void UpdateLeafVelocities(float deltaSeconds) {
            if (deltaSeconds <= 0f || leafLayer == null || LiveParticleCount <= 0) {
                return;
            }

            EnsureParticleBuffer();
            int count = leafLayer.GetParticles(particleBuffer);
            if (count <= 0) {
                return;
            }

            Vector2 windDirection = windSettings != null ? windSettings.EffectiveDirection : RoWeatherWindSettings.DefaultDirection;
            float windStrength = windSettings != null ? windSettings.Strength : 0f;
            float gustStrength = RoWindSwayGlobals.CurrentParams.w;
            float time = Time.time;
            for (int i = 0; i < count; i++) {
                ParticleSystem.Particle particle = particleBuffer[i];
                float gustFactor = RoAmbientParticleMath.ComputeCpuGustFactor(particle.position, windDirection, time, gustStrength);
                Vector2 acceleration = windDirection * (windStrength * settings.WindInfluence * gustFactor);
                Vector3 velocity = particle.velocity;
                velocity.x += acceleration.x * deltaSeconds;
                velocity.z += acceleration.y * deltaSeconds;
                particle.velocity = RoAmbientParticleMath.ApplyLeafHorizontalDragAndClamp(velocity, deltaSeconds);
                particleBuffer[i] = particle;
            }

            leafLayer.SetParticles(particleBuffer, count);
        }

        private void EnsureParticleBuffer() {
            int count = settings != null ? settings.MaxLeafParticles : RoAmbientParticleSettings.DefaultMaxLeafParticles;
            if (particleBuffer == null || particleBuffer.Length < count) {
                particleBuffer = new ParticleSystem.Particle[count];
            }
        }

        private void RebuildEmitterCacheIfNeeded(bool force) {
            int instanceId = modelRoot != null ? modelRoot.GetInstanceID() : 0;
            if (!force && instanceId == modelRootInstanceId) {
                return;
            }

            modelRootInstanceId = instanceId;
            cachedEmitters.Clear();
            if (modelRoot == null) {
                return;
            }

            surfaceScratch.Clear();
            modelRoot.GetComponentsInChildren(true, surfaceScratch);
            DiscoverIntoCache(surfaceScratch, RoWindSwayCatalog.Load());
            surfaceScratch.Clear();
        }

        private void DiscoverIntoCache(IReadOnlyList<RoModelSurfaceInfo> surfaces, RoWindSwayCatalog catalog) {
            emitterBuilders.Clear();
            for (int i = 0; i < surfaces.Count; i++) {
                RoModelSurfaceInfo surface = surfaces[i];
                if (!IsLeafEmitterSurface(surface, catalog)) {
                    continue;
                }

                string key = BuildEmitterKey(surface);
                if (!emitterBuilders.TryGetValue(key, out EmitterBuilder builder)) {
                    builder = new EmitterBuilder(surface.ModelFile, surface.TextureName);
                    emitterBuilders.Add(key, builder);
                }

                builder.Add(surface);
            }

            foreach (EmitterBuilder builder in emitterBuilders.Values) {
                if (builder.TryBuild(out RoAmbientParticleEmitter emitter)) {
                    cachedEmitters.Add(emitter);
                }
            }

            emitterBuilders.Clear();
        }

        private static bool IsLeafEmitterSurface(RoModelSurfaceInfo surface, RoWindSwayCatalog catalog) =>
            surface != null &&
            catalog != null &&
            catalog.TryFindProfile(surface.TextureName, out RoWindSwayCatalog.Profile profile) &&
            profile.Mode == RoWindSwayMode.Foliage;

        private static string BuildEmitterKey(RoModelSurfaceInfo surface) =>
            $"{surface.InstanceIndex}|{surface.ModelFile}";

        private float NextFloat(float min, float max) =>
            Mathf.Lerp(min, max, (float)random.NextDouble());

        private Vector2 RandomUnitCircle() {
            float angle = NextFloat(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(NextFloat(0f, 1f));
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        private sealed class EmitterBuilder {
            private readonly string modelFile;
            private readonly string textureName;
            private Vector3 weightedAnchorSum;
            private float weightSum;
            private float maxRadius;
            private int surfaceCount;

            public EmitterBuilder(string modelFile, string textureName) {
                this.modelFile = modelFile ?? string.Empty;
                this.textureName = textureName ?? string.Empty;
            }

            public void Add(RoModelSurfaceInfo surface) {
                if (surface == null) {
                    return;
                }

                Bounds localBounds = surface.LocalMeshBounds;
                Vector3 localAnchor = new Vector3(localBounds.center.x, localBounds.max.y, localBounds.center.z);
                Vector3 worldAnchor = surface.transform.TransformPoint(localAnchor);
                MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
                Bounds worldBounds = renderer != null
                    ? renderer.bounds
                    : new Bounds(worldAnchor, Vector3.one);
                float radius = Mathf.Max(worldBounds.extents.x, worldBounds.extents.z);
                radius = Mathf.Max(radius, 0.35f);

                float weight = Mathf.Max(1f, radius);
                weightedAnchorSum += worldAnchor * weight;
                weightSum += weight;
                maxRadius = Mathf.Max(maxRadius, radius);
                surfaceCount++;
            }

            public bool TryBuild(out RoAmbientParticleEmitter emitter) {
                emitter = default;
                if (surfaceCount <= 0) {
                    return false;
                }

                Vector3 anchor = weightedAnchorSum / Mathf.Max(0.0001f, weightSum);
                emitter = new RoAmbientParticleEmitter(anchor, maxRadius, textureName, modelFile, surfaceCount);
                return true;
            }
        }
    }
}
