using System.Collections.Generic;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Maps;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;

namespace Ragnarok.Effects {
    [DisallowMultipleComponent]
    public sealed class RoDayNightCycleController : MonoBehaviour {
        private const string RuntimeLightName = "RO Day Night Directional Light";
        private static readonly int UseLightingId = Shader.PropertyToID("_RoUseLighting");
        private static readonly int LightDirectionId = Shader.PropertyToID("_RoLightDirection");
        private static readonly int LightAmbientId = Shader.PropertyToID("_RoLightAmbient");
        private static readonly int LightDiffuseId = Shader.PropertyToID("_RoLightDiffuse");
        private static readonly int LightOpacityId = Shader.PropertyToID("_RoLightOpacity");
        private static readonly int LightingStrengthId = Shader.PropertyToID("_RoLightingStrength");
        private const float MinimumRuntimeApplyIntervalSeconds = 0.25f;
        private const float ActorNightBrightnessFloor = 0.22f;
        private const float SampleDirectionEpsilonSqr = 0.00001f;
        private const float SampleColorEpsilon = 0.0025f;
        private const float SampleIntensityEpsilon = 0.0025f;
        private static readonly ProfilerMarker MaterialApplyMarker = new ProfilerMarker("RO DayNight.MaterialApply");
        private static readonly ProfilerMarker NightLightApplyMarker = new ProfilerMarker("RO DayNight.NightLights");

        private readonly Dictionary<Material, MaterialLightingSnapshot> materialSnapshots =
            new Dictionary<Material, MaterialLightingSnapshot>();
        private readonly List<RoMapNightLight> cachedNightLights = new List<RoMapNightLight>(128);
        private readonly List<RoMapNightLight> nightLightScratch = new List<RoMapNightLight>(128);
        private readonly List<MeshRenderer> materialRendererScratch = new List<MeshRenderer>(256);
        private readonly List<Material> materialScratch = new List<Material>(8);

        [SerializeField] private bool useOptimizedLightingApply = true;

        private RoDayNightCycleSettings settings;
        private GameObject groundRoot;
        private GameObject modelRoot;
        private Light directionalLight;
        private bool createdDirectionalLight;
        private bool renderStateCaptured;
        private bool materialSnapshotsCaptured;
        private bool nightLightsCached;
        private Light previousSun;
        private AmbientMode previousAmbientMode;
        private Color previousAmbientLight;
        private float elapsedSeconds;
        private float nextApplyTime;
        private int lastSettingsSignature;
        private bool runtimeAllowed;
        private bool running;
        private bool serverDriven;
        private bool hasServerDrivenCycleSeconds;
        private float serverDrivenCycleSeconds;
        private bool hasLastIsNight;
        private bool lastIsNight;
        private RoDayNightCycleSample currentSample;
        private RoDayNightCycleSample lastAppliedSample;
        private bool hasAppliedSample;

        public bool IsRuntimeAllowed => runtimeAllowed;
        public bool IsRunning => running;
        public bool IsServerDriven => serverDriven;
        public float ElapsedSeconds => elapsedSeconds;
        public float CurrentDaylightFactor { get; private set; } = 1f;
        public RoDayNightCycleSample CurrentSample => currentSample;
        public int CachedMaterialCount => materialSnapshots.Count;
        public int CachedNightLightCount => cachedNightLights.Count;

        private void Update() {
            if (serverDriven) {
                UpdateServerDriven();
                return;
            }

            // Day/night is driven exclusively by the server. Never run a local free cycle (it would start
            // at dawn and drift independently of the server). Stay dormant / restored to authored lighting.
            if (running) {
                StopCycle(restoreLighting: true);
            }
        }

        private void UpdateServerDriven() {
            if (settings == null || !hasServerDrivenCycleSeconds) {
                return;
            }

            settings.EnsureCurrentDefaults();
            elapsedSeconds = serverDrivenCycleSeconds;
            ApplyCurrentSample(force: false);
        }

        /// <summary>
        /// Drives the cycle from an externally supplied position (the server day/night clock) instead
        /// of the free-running local timer. The supplied seconds are a position inside the whole cycle,
        /// so the day/night window lengths in <paramref name="cycleSeconds"/>' source must match the
        /// settings' day/night minutes.
        /// </summary>
        public void SetServerDrivenCycleSeconds(float cycleSeconds) {
            settings ??= new RoDayNightCycleSettings();
            settings.EnsureCurrentDefaults();
            settings.enabled = true;
            serverDriven = true;
            runtimeAllowed = true;
            running = true;
            serverDrivenCycleSeconds = cycleSeconds;
            hasServerDrivenCycleSeconds = true;
            elapsedSeconds = cycleSeconds;
            ApplyCurrentSample(force: false);
        }

        public void ClearServerDriven(bool restoreLighting) {
            if (!serverDriven) {
                return;
            }

            serverDriven = false;
            hasServerDrivenCycleSeconds = false;
            if (restoreLighting) {
                StopCycle(restoreLighting: true);
            }
        }

        private void OnDisable() {
            StopCycle(restoreLighting: true);
        }

        public void Initialize(RoDayNightCycleSettings cycleSettings, GameObject ground, GameObject models, bool allowRuntime) {
            settings = cycleSettings ?? new RoDayNightCycleSettings();
            settings.EnsureCurrentDefaults();
            runtimeAllowed = allowRuntime;
            bool rootsChanged = groundRoot != ground || modelRoot != models;
            if (rootsChanged) {
                RestoreNightLights();
            }

            groundRoot = ground;
            modelRoot = models;
            if (rootsChanged) {
                RestoreMaterialSnapshots();
                InvalidateNightLightCache();
            }
            lastSettingsSignature = ResolveSettingsSignature(settings);
            RequestImmediateApply();

            // The cycle renders only when server-driven (SetServerDrivenCycleSeconds). At init we are not
            // server-driven yet, so stay dormant / restored -- never start a local free cycle from dawn.
            if (!serverDriven) {
                StopCycle(restoreLighting: true);
            }
        }

        public void SyncSettings(RoDayNightCycleSettings cycleSettings, GameObject ground, GameObject models, bool allowRuntime) {
            if (cycleSettings != null && !ReferenceEquals(settings, cycleSettings)) {
                settings = cycleSettings;
            }

            settings ??= new RoDayNightCycleSettings();
            settings.EnsureCurrentDefaults();
            int settingsSignature = ResolveSettingsSignature(settings);
            bool settingsChanged = settingsSignature != lastSettingsSignature;
            lastSettingsSignature = settingsSignature;
            runtimeAllowed = allowRuntime;
            bool rootsChanged = groundRoot != ground || modelRoot != models;
            if (rootsChanged) {
                RestoreNightLights();
            }

            groundRoot = ground;
            modelRoot = models;
            if (rootsChanged) {
                RestoreMaterialSnapshots();
                InvalidateNightLightCache();
            }

            if (settingsChanged || rootsChanged) {
                RequestImmediateApply();
            }

            if (serverDriven) {
                // Server-driven rendering is handled by SetServerDrivenCycleSeconds / UpdateServerDriven;
                // roots and allowed-state were already refreshed above.
                return;
            }

            // The cycle is server-only; never auto-start a local free cycle. Stay dormant / restored.
            if (running) {
                StopCycle(restoreLighting: true);
            }
        }

        public void StartCycle(bool reset) {
            settings ??= new RoDayNightCycleSettings();
            settings.EnsureCurrentDefaults();
            settings.enabled = true;
            if (reset || !running) {
                elapsedSeconds = settings.StartSeconds;
            }

            if (!runtimeAllowed) {
                running = false;
                return;
            }

            running = true;
            RequestImmediateApply();
            ApplyCurrentSample(force: true);
        }

        public void ResetCycle() {
            settings ??= new RoDayNightCycleSettings();
            settings.EnsureCurrentDefaults();
            elapsedSeconds = settings.StartSeconds;
            if (settings.enabled && runtimeAllowed) {
                running = true;
                RequestImmediateApply();
                ApplyCurrentSample(force: true);
            }
        }

        public void StopCycle(bool restoreLighting) {
            running = false;
            if (restoreLighting) {
                RestoreLighting();
            }
        }

        public void ClearRuntime() {
            running = false;
            RestoreLighting();
        }

        private void RequestImmediateApply() {
            nextApplyTime = 0f;
            hasAppliedSample = false;
        }

        private void ApplyCurrentSample(bool force) {
            settings ??= new RoDayNightCycleSettings();
            settings.EnsureCurrentDefaults();
            float now = Application.isPlaying ? Time.time : 0f;
            if (!force && now < nextApplyTime) {
                return;
            }

            currentSample = settings.Evaluate(elapsedSeconds);
            CurrentDaylightFactor = ComputeDaylightFactor(currentSample.AmbientColor, settings.dayAmbientColor);
            if (!force && hasAppliedSample && !HasMeaningfulSampleChange(lastAppliedSample, currentSample)) {
                nextApplyTime = now + ResolveRuntimeApplyIntervalSeconds();
                return;
            }

            CaptureRenderStateIfNeeded();
            CaptureMaterialSnapshotsIfNeeded();
            if (useOptimizedLightingApply) {
                CaptureNightLightsIfNeeded();
            }

            Light light = ResolveOrCreateDirectionalLight();
            if (light != null) {
                Vector3 lightDirection = currentSample.LightDirection.sqrMagnitude > 0.000001f
                    ? currentSample.LightDirection.normalized
                    : Vector3.up;
                light.enabled = true;
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.LookRotation(-lightDirection, Vector3.up);
                light.color = currentSample.LightColor;
                light.intensity = currentSample.Intensity;
                RenderSettings.sun = light;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = currentSample.AmbientColor;

            var lighting = new RoMapLightingState(
                longitude: 0,
                latitude: 0,
                currentSample.LightDirection,
                currentSample.AmbientColor,
                currentSample.LightColor,
                currentSample.Intensity
            );
            using (MaterialApplyMarker.Auto()) {
                ApplyDynamicLightingToCachedMaterials(lighting);
            }

            using (NightLightApplyMarker.Auto()) {
                ApplyNightLights(currentSample);
            }

            RoActorMapLightProbe.GlobalActorDayNightMultiply = ComputeActorDayNightMultiply(currentSample);
            if (!hasLastIsNight || lastIsNight != currentSample.IsNight) {
                // Night toggled: wake stationary actors so their lamp glow re-samples at the transition.
                hasLastIsNight = true;
                lastIsNight = currentSample.IsNight;
                RoActorMapLightProbe.DayNightLampEpoch++;
            }

            RoRuntimePerfCounters.SetDayNightCounts(materialSnapshots.Count, cachedNightLights.Count);
            lastAppliedSample = currentSample;
            hasAppliedSample = true;
            nextApplyTime = now + ResolveRuntimeApplyIntervalSeconds();
        }

        // Translates the world lighting sample into a per-actor map-light multiply: white in full day,
        // darkened and tinted toward the night ambient as the sun sets, floored so actors stay readable.
        private Color ComputeActorDayNightMultiply(RoDayNightCycleSample sample) {
            // Track the ambient (sky fill) brightness, not the directional sun: the ambient reaches its
            // full daytime level right after dawn, so actors brighten together with the world instead of
            // lagging until the sun peaks at noon.
            float brightness = ComputeDaylightFactor(sample.AmbientColor, settings.dayAmbientColor);
            float blend = Mathf.Clamp01((1f - brightness) * Mathf.Clamp01(settings.actorNightStrength));
            Color result = Color.Lerp(Color.white, sample.AmbientColor, blend);
            return new Color(
                Mathf.Max(result.r, ActorNightBrightnessFloor),
                Mathf.Max(result.g, ActorNightBrightnessFloor),
                Mathf.Max(result.b, ActorNightBrightnessFloor),
                1f);
        }

        public static float ComputeDaylightFactor(Color ambientColor, Color dayAmbientColor) {
            return Mathf.Clamp01(ambientColor.grayscale / Mathf.Max(dayAmbientColor.grayscale, 0.001f));
        }

        private static bool HasMeaningfulSampleChange(RoDayNightCycleSample previous, RoDayNightCycleSample next) {
            if (previous.IsNight != next.IsNight) {
                return true;
            }

            if ((previous.LightDirection - next.LightDirection).sqrMagnitude > SampleDirectionEpsilonSqr) {
                return true;
            }

            if (ColorDelta(previous.LightColor, next.LightColor) > SampleColorEpsilon ||
                ColorDelta(previous.AmbientColor, next.AmbientColor) > SampleColorEpsilon) {
                return true;
            }

            return Mathf.Abs(previous.Intensity - next.Intensity) > SampleIntensityEpsilon;
        }

        private static float ColorDelta(Color a, Color b) {
            return Mathf.Max(
                Mathf.Abs(a.r - b.r),
                Mathf.Abs(a.g - b.g),
                Mathf.Abs(a.b - b.b),
                Mathf.Abs(a.a - b.a));
        }

        private float ResolveRuntimeApplyIntervalSeconds() {
            return Mathf.Max(MinimumRuntimeApplyIntervalSeconds, settings?.UpdateIntervalSeconds ?? RoDayNightCycleSettings.DefaultUpdateIntervalSeconds);
        }

        private void CaptureRenderStateIfNeeded() {
            if (renderStateCaptured) {
                return;
            }

            previousSun = RenderSettings.sun;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientLight = RenderSettings.ambientLight;
            renderStateCaptured = true;
        }

        private void CaptureMaterialSnapshotsIfNeeded() {
            if (materialSnapshotsCaptured) {
                return;
            }

            CaptureMaterialSnapshots(groundRoot);
            CaptureMaterialSnapshots(modelRoot);
            materialSnapshotsCaptured = true;
        }

        public void MarkLightingCachesDirty() {
            RestoreMaterialSnapshots();
            InvalidateNightLightCache();
            RequestImmediateApply();
        }

        private void CaptureMaterialSnapshots(GameObject root) {
            if (root == null) {
                return;
            }

            materialRendererScratch.Clear();
            root.GetComponentsInChildren(true, materialRendererScratch);
            for (int i = 0; i < materialRendererScratch.Count; i++) {
                materialRendererScratch[i].GetSharedMaterials(materialScratch);
                for (int j = 0; j < materialScratch.Count; j++) {
                    Material material = materialScratch[j];
                    if (material == null || materialSnapshots.ContainsKey(material)) {
                        continue;
                    }

                    materialSnapshots.Add(material, new MaterialLightingSnapshot(material));
                }

                materialScratch.Clear();
            }

            materialRendererScratch.Clear();
        }

        private void ApplyDynamicLightingToCachedMaterials(RoMapLightingState state) {
            foreach (KeyValuePair<Material, MaterialLightingSnapshot> snapshot in materialSnapshots) {
                if (useOptimizedLightingApply) {
                    snapshot.Value.ApplyDynamicLighting(state);
                } else {
                    ApplyDynamicLightingToMaterial(snapshot.Key, state);
                }
            }

            RoRuntimePerfCounters.AddDayNightMaterialApplies(materialSnapshots.Count);
        }

        private static void ApplyDynamicLightingToMaterial(Material material, RoMapLightingState state) {
            if (material == null) {
                return;
            }

            if (material.HasProperty(UseLightingId)) {
                material.SetFloat(UseLightingId, 1f);
            }

            if (material.HasProperty(LightDirectionId)) {
                Vector3 direction = state.Direction;
                material.SetVector(LightDirectionId, new Vector4(direction.x, direction.y, direction.z, 0f));
            }

            if (material.HasProperty(LightAmbientId)) {
                material.SetColor(LightAmbientId, state.Ambient);
            }

            if (material.HasProperty(LightDiffuseId)) {
                material.SetColor(LightDiffuseId, state.Diffuse);
            }

            if (material.HasProperty(LightOpacityId)) {
                material.SetFloat(LightOpacityId, state.Opacity);
            }

            if (material.HasProperty(LightingStrengthId)) {
                material.SetFloat(LightingStrengthId, 1f);
            }
        }

        private Light ResolveOrCreateDirectionalLight() {
            if (directionalLight != null) {
                return directionalLight;
            }

            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional) {
                directionalLight = RenderSettings.sun;
                createdDirectionalLight = false;
                return directionalLight;
            }

            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++) {
                Light light = lights[i];
                if (light != null && light.enabled && light.type == LightType.Directional) {
                    directionalLight = light;
                    createdDirectionalLight = false;
                    return directionalLight;
                }
            }

            var lightObject = new GameObject(RuntimeLightName);
            lightObject.transform.SetParent(transform, false);
            directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.shadows = LightShadows.Soft;
            createdDirectionalLight = true;
            return directionalLight;
        }

        private void RestoreLighting() {
            RoActorMapLightProbe.GlobalActorDayNightMultiply = Color.white;
            CurrentDaylightFactor = 1f;
            RoActorMapLightProbe.DayNightLampEpoch++;
            hasLastIsNight = false;
            RestoreNightLights();
            RestoreMaterialSnapshots();
            if (renderStateCaptured) {
                if (createdDirectionalLight) {
                    DestroyRuntimeObject(directionalLight != null ? directionalLight.gameObject : null);
                    RenderSettings.sun = previousSun;
                } else if (directionalLight != null && RenderSettings.sun == directionalLight) {
                    RenderSettings.sun = previousSun;
                }

                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
            }

            directionalLight = null;
            createdDirectionalLight = false;
            renderStateCaptured = false;
            hasAppliedSample = false;
            nextApplyTime = 0f;
        }

        private void ApplyNightLights(RoDayNightCycleSample sample) {
            if (!useOptimizedLightingApply) {
                ApplyNightLights(groundRoot, sample.IsNight);
                ApplyNightLights(modelRoot, sample.IsNight);
                return;
            }

            CaptureNightLightsIfNeeded();
            RefreshNightLightCacheIfStale();
            int appliedCount = ApplyCachedNightLights(sample.IsNight);
            RoRuntimePerfCounters.AddDayNightLightApplies(appliedCount);
        }

        private void CaptureNightLightsIfNeeded() {
            if (nightLightsCached) {
                return;
            }

            cachedNightLights.Clear();
            // The map's lamp lights (RSW + model lamps) live under their own roots, not under ground/models,
            // so scanning only the snapshot roots would miss them and they would never light up at night.
            // Find every RoMapNightLight in the scene instead.
            RoMapNightLight[] sceneNightLights = FindObjectsByType<RoMapNightLight>(FindObjectsSortMode.None);
            for (int i = 0; i < sceneNightLights.Length; i++) {
                if (sceneNightLights[i] != null) {
                    cachedNightLights.Add(sceneNightLights[i]);
                }
            }

            nightLightsCached = true;
        }

        private void RefreshNightLightCacheIfStale() {
            if (!nightLightsCached) {
                return;
            }

            for (int i = 0; i < cachedNightLights.Count; i++) {
                if (cachedNightLights[i] == null) {
                    RebuildNightLightCache();
                    return;
                }
            }
        }

        private void RebuildNightLightCache() {
            cachedNightLights.Clear();
            nightLightsCached = false;
            CaptureNightLightsIfNeeded();
        }

        private int ApplyCachedNightLights(bool isNight) {
            int appliedCount = 0;
            bool foundDestroyedLight = false;
            for (int i = 0; i < cachedNightLights.Count; i++) {
                RoMapNightLight light = cachedNightLights[i];
                if (light == null) {
                    foundDestroyedLight = true;
                    continue;
                }

                light.Apply(isNight);
                appliedCount++;
            }

            if (!foundDestroyedLight) {
                return appliedCount;
            }

            RebuildNightLightCache();
            appliedCount = 0;
            for (int i = 0; i < cachedNightLights.Count; i++) {
                RoMapNightLight light = cachedNightLights[i];
                if (light == null) {
                    continue;
                }

                light.Apply(isNight);
                appliedCount++;
            }

            return appliedCount;
        }

        private void RestoreCachedNightLights() {
            bool foundDestroyedLight = false;
            for (int i = 0; i < cachedNightLights.Count; i++) {
                RoMapNightLight light = cachedNightLights[i];
                if (light == null) {
                    foundDestroyedLight = true;
                    continue;
                }

                light.Restore();
            }

            if (!foundDestroyedLight) {
                return;
            }

            RebuildNightLightCache();
            for (int i = 0; i < cachedNightLights.Count; i++) {
                RoMapNightLight light = cachedNightLights[i];
                if (light != null) {
                    light.Restore();
                }
            }
        }

        private void CaptureNightLights(GameObject root) {
            if (root == null) {
                return;
            }

            nightLightScratch.Clear();
            root.GetComponentsInChildren(true, nightLightScratch);
            for (int i = 0; i < nightLightScratch.Count; i++) {
                RoMapNightLight light = nightLightScratch[i];
                if (light != null && !cachedNightLights.Contains(light)) {
                    cachedNightLights.Add(light);
                }
            }

            nightLightScratch.Clear();
        }

        private void ApplyNightLights(GameObject root, bool isNight) {
            if (root == null) {
                return;
            }

            nightLightScratch.Clear();
            root.GetComponentsInChildren(true, nightLightScratch);
            for (int i = 0; i < nightLightScratch.Count; i++) {
                RoMapNightLight light = nightLightScratch[i];
                if (light != null) {
                    light.Apply(isNight);
                }
            }

            nightLightScratch.Clear();
        }

        private void RestoreNightLights() {
            if (useOptimizedLightingApply) {
                CaptureNightLightsIfNeeded();
                RefreshNightLightCacheIfStale();
                RestoreCachedNightLights();
                return;
            }

            RestoreNightLights(groundRoot);
            RestoreNightLights(modelRoot);
        }

        private void InvalidateNightLightCache() {
            cachedNightLights.Clear();
            nightLightsCached = false;
            RoRuntimePerfCounters.SetDayNightCounts(materialSnapshots.Count, 0);
        }

        private void RestoreNightLights(GameObject root) {
            if (root == null) {
                return;
            }

            nightLightScratch.Clear();
            root.GetComponentsInChildren(true, nightLightScratch);
            for (int i = 0; i < nightLightScratch.Count; i++) {
                RoMapNightLight light = nightLightScratch[i];
                if (light != null) {
                    light.Restore();
                }
            }

            nightLightScratch.Clear();
        }

        private static int ResolveSettingsSignature(RoDayNightCycleSettings settings) {
            unchecked {
                int hash = 17;
                hash = hash * 31 + (settings.enabled ? 1 : 0);
                hash = hash * 31 + Quantize(settings.dayMinutes);
                hash = hash * 31 + Quantize(settings.nightMinutes);
                hash = hash * 31 + Quantize(settings.startNormalizedTime);
                hash = hash * 31 + Quantize(settings.updateIntervalSeconds);
                hash = hash * 31 + Quantize(settings.maxHorizontalLightX);
                hash = hash * 31 + Quantize(settings.minimumLightElevation);
                hash = hash * 31 + Quantize(settings.nightTransitionFraction);
                hash = hash * 31 + Quantize(settings.dayIntensity);
                hash = hash * 31 + Quantize(settings.nightIntensity);
                hash = hash * 31 + Quantize(settings.horizonIntensity);
                hash = hash * 31 + Quantize(settings.dawnLightColor);
                hash = hash * 31 + Quantize(settings.dayLightColor);
                hash = hash * 31 + Quantize(settings.duskLightColor);
                hash = hash * 31 + Quantize(settings.nightLightColor);
                hash = hash * 31 + Quantize(settings.dawnAmbientColor);
                hash = hash * 31 + Quantize(settings.dayAmbientColor);
                hash = hash * 31 + Quantize(settings.duskAmbientColor);
                hash = hash * 31 + Quantize(settings.nightAmbientColor);
                return hash;
            }
        }

        private static int Quantize(float value) => Mathf.RoundToInt(value * 10000f);

        private static int Quantize(Color color) {
            unchecked {
                int hash = 17;
                hash = hash * 31 + Quantize(color.r);
                hash = hash * 31 + Quantize(color.g);
                hash = hash * 31 + Quantize(color.b);
                hash = hash * 31 + Quantize(color.a);
                return hash;
            }
        }

        private void RestoreMaterialSnapshots() {
            foreach (KeyValuePair<Material, MaterialLightingSnapshot> snapshot in materialSnapshots) {
                snapshot.Value.Restore();
            }

            materialSnapshots.Clear();
            materialSnapshotsCaptured = false;
            RoRuntimePerfCounters.SetDayNightCounts(0, cachedNightLights.Count);
        }

        private static void DestroyRuntimeObject(Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(target);
            } else {
                DestroyImmediate(target);
            }
        }

        private sealed class MaterialLightingSnapshot {
            private readonly Material material;
            private readonly bool hasUseLighting;
            private readonly bool hasDirection;
            private readonly bool hasAmbient;
            private readonly bool hasDiffuse;
            private readonly bool hasOpacity;
            private readonly bool hasStrength;
            private readonly float useLighting;
            private readonly Vector4 direction;
            private readonly Color ambient;
            private readonly Color diffuse;
            private readonly float opacity;
            private readonly float strength;

            public MaterialLightingSnapshot(Material material) {
                this.material = material;
                hasUseLighting = material.HasProperty(UseLightingId);
                hasDirection = material.HasProperty(LightDirectionId);
                hasAmbient = material.HasProperty(LightAmbientId);
                hasDiffuse = material.HasProperty(LightDiffuseId);
                hasOpacity = material.HasProperty(LightOpacityId);
                hasStrength = material.HasProperty(LightingStrengthId);
                if (hasUseLighting) {
                    useLighting = material.GetFloat(UseLightingId);
                }

                if (hasDirection) {
                    direction = material.GetVector(LightDirectionId);
                }

                if (hasAmbient) {
                    ambient = material.GetColor(LightAmbientId);
                }

                if (hasDiffuse) {
                    diffuse = material.GetColor(LightDiffuseId);
                }

                if (hasOpacity) {
                    opacity = material.GetFloat(LightOpacityId);
                }

                if (hasStrength) {
                    strength = material.GetFloat(LightingStrengthId);
                }
            }

            public void Restore() {
                if (material == null) {
                    return;
                }

                if (hasUseLighting) {
                    material.SetFloat(UseLightingId, useLighting);
                }

                if (hasDirection) {
                    material.SetVector(LightDirectionId, direction);
                }

                if (hasAmbient) {
                    material.SetColor(LightAmbientId, ambient);
                }

                if (hasDiffuse) {
                    material.SetColor(LightDiffuseId, diffuse);
                }

                if (hasOpacity) {
                    material.SetFloat(LightOpacityId, opacity);
                }

                if (hasStrength) {
                    material.SetFloat(LightingStrengthId, strength);
                }
            }

            public void ApplyDynamicLighting(RoMapLightingState state) {
                if (material == null) {
                    return;
                }

                if (hasUseLighting) {
                    material.SetFloat(UseLightingId, 1f);
                }

                if (hasDirection) {
                    Vector3 direction = state.Direction;
                    material.SetVector(LightDirectionId, new Vector4(direction.x, direction.y, direction.z, 0f));
                }

                if (hasAmbient) {
                    material.SetColor(LightAmbientId, state.Ambient);
                }

                if (hasDiffuse) {
                    material.SetColor(LightDiffuseId, state.Diffuse);
                }

                if (hasOpacity) {
                    material.SetFloat(LightOpacityId, state.Opacity);
                }

                if (hasStrength) {
                    material.SetFloat(LightingStrengthId, 1f);
                }
            }
        }
    }
}
