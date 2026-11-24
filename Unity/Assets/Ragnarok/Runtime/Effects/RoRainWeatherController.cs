using System;
using System.Collections.Generic;
using Ragnarok.Audio;
using UnityEngine;

namespace Ragnarok.Effects {
    [DisallowMultipleComponent]
    public sealed class RoRainWeatherController : MonoBehaviour {
        private const float AudioListenerLookupRetrySeconds = 1f;
        private static readonly int RainWetnessId = Shader.PropertyToID("_RoRainWetness");
        private static readonly int RainDarkeningId = Shader.PropertyToID("_RoRainDarkening");
        private static readonly int RainHighlightId = Shader.PropertyToID("_RoRainHighlight");
        private static readonly int RainStreakStrengthId = Shader.PropertyToID("_RoRainStreakStrength");
        private static readonly int RainPuddleStrengthId = Shader.PropertyToID("_RoRainPuddleStrength");

        private readonly List<Material> modelMaterials = new List<Material>(64);
        private readonly List<Material> groundMaterials = new List<Material>(4);
        private readonly List<MeshRenderer> materialRendererScratch = new List<MeshRenderer>(128);
        private readonly List<Material> sharedMaterialScratch = new List<Material>(8);
        private readonly HashSet<Material> materialScratch = new HashSet<Material>();
        private RoRainWeatherSettings settings;
        private RoWeatherWindSettings windSettings;
        private RoEffectManager effectManager;
        private RoAudioSettings audioSettings;
        private UnityEngine.Camera renderCamera;
        private Transform followTarget;
        private RoWeatherParticleLayer rainLayer;
        private AudioSource rainAudioSource;
        private AudioListener cachedAudioListener;
        private float nextAudioListenerLookupTime;
        private float currentIntensity;
        private float targetIntensity;
        private int lastSettingsSignature;
        private int lastWindSignature;
        private bool initialized;

        public float CurrentIntensity => currentIntensity;
        public float TargetIntensity => targetIntensity;
        public bool IsRaining => targetIntensity > 0.0001f;
        public int ModelMaterialCount => modelMaterials.Count;
        public int GroundMaterialCount => groundMaterials.Count;
        public AudioSource RainAudioSource => rainAudioSource;

        private void Update() {
            if (!initialized) {
                return;
            }

            float rampSeconds = targetIntensity > currentIntensity
                ? settings.RampUpSeconds
                : settings.RampDownSeconds;
            float nextIntensity = MoveIntensity(currentIntensity, targetIntensity, Time.deltaTime, rampSeconds);
            if (Mathf.Approximately(nextIntensity, currentIntensity)) {
                return;
            }

            ApplyIntensity(nextIntensity);
        }

        private void OnDisable() {
            StopRainAudio();
            ResetModelWetness();
        }

        private void OnDestroy() {
            if (rainLayer != null) {
                rainLayer.Stop();
                rainLayer = null;
            }

            StopRainAudio();
            ResetModelWetness();
        }

        public void Initialize(
            RoEffectManager manager,
            GameObject groundRoot,
            GameObject modelRoot,
            UnityEngine.Camera camera,
            RoRainWeatherSettings rainSettings,
            RoAudioSettings audio = null,
            RoWeatherWindSettings weatherWindSettings = null
        ) {
            effectManager = manager;
            audioSettings = audio;
            renderCamera = camera;
            followTarget = camera != null ? camera.transform : null;
            settings = rainSettings ?? new RoRainWeatherSettings();
            windSettings = weatherWindSettings ?? new RoWeatherWindSettings();
#if UNITY_EDITOR
            settings.EnsureEditorDefaults();
#else
            settings.EnsureCurrentDefaults();
#endif
            windSettings.EnsureCurrentDefaults();
            CacheGroundMaterials(groundRoot);
            CacheModelMaterials(modelRoot);
            currentIntensity = 0f;
            targetIntensity = 0f;
            lastSettingsSignature = ResolveSettingsSignature(settings);
            lastWindSignature = ResolveWindSignature(windSettings);
            initialized = true;

            ApplyIntensity(0f);
            SetRainEnabled(settings.enabled, immediate: false);
        }

        public void StopRain(bool immediate = false) {
            targetIntensity = 0f;
            if (immediate) {
                currentIntensity = 0f;
                rainLayer?.SetIntensity(0f);
                rainLayer?.Stop();
                rainLayer = null;
                StopRainAudio();
                ResetModelWetness();
            } else {
                ApplyRainAudio();
            }
        }

        public void SyncSettings(
            RoRainWeatherSettings rainSettings,
            UnityEngine.Camera camera = null,
            RoAudioSettings audio = null,
            RoWeatherWindSettings weatherWindSettings = null
        ) {
            if (!initialized) {
                return;
            }

            settings = rainSettings ?? settings ?? new RoRainWeatherSettings();
            windSettings = weatherWindSettings ?? windSettings ?? new RoWeatherWindSettings();
#if UNITY_EDITOR
            settings.EnsureEditorDefaults();
#else
            settings.EnsureCurrentDefaults();
#endif
            windSettings.EnsureCurrentDefaults();
            int settingsSignature = ResolveSettingsSignature(settings);
            int windSignature = ResolveWindSignature(windSettings);
            bool settingsChanged = settingsSignature != lastSettingsSignature;
            bool windChanged = windSignature != lastWindSignature;
            bool audioChanged = audio != null && !ReferenceEquals(audioSettings, audio);
            lastSettingsSignature = settingsSignature;
            lastWindSignature = windSignature;
            if (audio != null) {
                audioSettings = audio;
            }

            if (camera != null && !ReferenceEquals(renderCamera, camera)) {
                renderCamera = camera;
                followTarget = camera.transform;
                if (rainLayer != null) {
                    rainLayer.SetFollowTarget(followTarget, settings.EffectiveCameraLocalOffset, renderCamera);
                }
            } else if (rainLayer != null) {
                rainLayer.SetFollowTarget(followTarget, settings.EffectiveCameraLocalOffset, renderCamera);
            }

            if (rainLayer != null && (settingsChanged || windChanged)) {
                rainLayer.SyncSpec(settings.BuildRainSpec(windSettings));
            }

            if (settingsChanged) {
                SetRainEnabled(settings.enabled);
                ApplyIntensity(currentIntensity);
            } else if (audioChanged) {
                ApplyRainAudio();
            }
        }

        public void SetRainEnabled(bool enabled, bool immediate = false) {
            if (!initialized) {
                return;
            }

            targetIntensity = enabled ? settings.TargetIntensity : 0f;
            if (targetIntensity > 0f) {
                EnsureRainLayer();
            }

            if (immediate) {
                ApplyIntensity(targetIntensity);
            }
        }

        public void SetTargetIntensity(float intensity, bool immediate = false) {
            if (!initialized) {
                return;
            }

            targetIntensity = Mathf.Clamp01(intensity);
            if (targetIntensity > 0f) {
                EnsureRainLayer();
            }

            if (immediate) {
                ApplyIntensity(targetIntensity);
            }
        }

        public static float MoveIntensity(float current, float target, float deltaSeconds, float rampSeconds) {
            current = Mathf.Clamp01(current);
            target = Mathf.Clamp01(target);
            if (rampSeconds <= 0f || deltaSeconds <= 0f) {
                return rampSeconds <= 0f ? target : current;
            }

            return Mathf.MoveTowards(current, target, deltaSeconds / rampSeconds);
        }

        private void EnsureRainLayer() {
            if (rainLayer != null || effectManager == null) {
                return;
            }

            rainLayer = effectManager.SpawnWeatherLayer(
                settings.BuildRainSpec(windSettings),
                transform.position,
                new RoEffectSpawnOptions {
                    Parent = transform,
                    FollowTarget = followTarget,
                    FollowLocalOffset = settings.EffectiveCameraLocalOffset,
                    RenderCamera = renderCamera
                });
            rainLayer.SetIntensity(currentIntensity);
        }

        private void ApplyIntensity(float intensity) {
            currentIntensity = Mathf.Clamp01(intensity);
            if (currentIntensity > 0f || targetIntensity > 0f) {
                EnsureRainLayer();
            }

            rainLayer?.SetIntensity(currentIntensity);
            ApplyModelWetness(settings.ModelWetnessAt(currentIntensity));
            ApplyGroundWetness(settings.GroundWetnessAt(currentIntensity));
            ApplyRainAudio();

            if (targetIntensity <= 0f && currentIntensity <= 0.0001f && rainLayer != null) {
                rainLayer.Stop();
                rainLayer = null;
            }
        }

        private static int ResolveSettingsSignature(RoRainWeatherSettings settings) {
            unchecked {
                int hash = 17;
                hash = hash * 31 + (settings.enabled ? 1 : 0);
                hash = hash * 31 + Quantize(settings.intensity);
                hash = hash * 31 + Quantize(settings.rampUpSeconds);
                hash = hash * 31 + Quantize(settings.rampDownSeconds);
                hash = hash * 31 + Quantize(settings.particleRateScale);
                hash = hash * 31 + Quantize(settings.particleAreaScale);
                hash = hash * 31 + Quantize(settings.cameraLocalOffset.x);
                hash = hash * 31 + Quantize(settings.cameraLocalOffset.y);
                hash = hash * 31 + Quantize(settings.cameraLocalOffset.z);
                hash = hash * 31 + (settings.rainLoopClip != null ? settings.rainLoopClip.GetInstanceID() : 0);
                hash = hash * 31 + Quantize(settings.rainLoopVolume);
                hash = hash * 31 + (settings.affectModels ? 1 : 0);
                hash = hash * 31 + (settings.affectGround ? 1 : 0);
                hash = hash * 31 + Quantize(settings.modelWetness);
                hash = hash * 31 + Quantize(settings.wetness);
                hash = hash * 31 + Quantize(settings.darkening);
                hash = hash * 31 + Quantize(settings.highlight);
                hash = hash * 31 + Quantize(settings.puddleStrength);
                return hash;
            }
        }

        private static int ResolveWindSignature(RoWeatherWindSettings settings) {
            Vector2 velocity = settings != null ? settings.Velocity : Vector2.zero;
            unchecked {
                int hash = 17;
                hash = hash * 31 + Quantize(velocity.x);
                hash = hash * 31 + Quantize(velocity.y);
                return hash;
            }
        }

        private static int Quantize(float value) => Mathf.RoundToInt(value * 10000f);

        private void ApplyRainAudio() {
            if (settings == null || settings.rainLoopClip == null) {
                StopRainAudio();
                return;
            }

            bool shouldPlay = targetIntensity > 0.0001f || currentIntensity > 0.0001f;
            EnsureRainAudioSource();
            rainAudioSource.clip = settings.rainLoopClip;
            rainAudioSource.outputAudioMixerGroup = audioSettings?.GetMixerGroup(RoAudioBus.Ambient);
            rainAudioSource.volume = ResolveRainAudioVolume();

            if (shouldPlay) {
                EnsureAudioListener();
                if (!rainAudioSource.isPlaying) {
                    rainAudioSource.Play();
                }
            } else {
                StopRainAudio();
            }
        }

        private void EnsureRainAudioSource() {
            if (rainAudioSource == null) {
                rainAudioSource = gameObject.GetComponent<AudioSource>();
                if (rainAudioSource == null) {
                    rainAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            rainAudioSource.playOnAwake = false;
            rainAudioSource.loop = true;
            rainAudioSource.spatialBlend = 0f;
            rainAudioSource.dopplerLevel = 0f;
            rainAudioSource.rolloffMode = AudioRolloffMode.Linear;
            rainAudioSource.minDistance = 1f;
            rainAudioSource.maxDistance = 500f;
        }

        private void EnsureAudioListener() {
            if (cachedAudioListener != null) {
                return;
            }

            float now = Time.unscaledTime;
            if (now < nextAudioListenerLookupTime) {
                return;
            }

            cachedAudioListener = FindFirstObjectByType<AudioListener>();
            if (cachedAudioListener != null) {
                nextAudioListenerLookupTime = 0f;
                return;
            }

            UnityEngine.Camera camera = RoEffectCameraCache.Resolve(renderCamera);
            if (camera != null) {
                cachedAudioListener = camera.gameObject.GetComponent<AudioListener>();
                if (cachedAudioListener == null) {
                    cachedAudioListener = camera.gameObject.AddComponent<AudioListener>();
                }

                nextAudioListenerLookupTime = 0f;
                return;
            }

            nextAudioListenerLookupTime = now + AudioListenerLookupRetrySeconds;
        }

        private float ResolveRainAudioVolume() {
            float sourceVolume = settings.rainLoopVolume * Mathf.Clamp01(currentIntensity);
            return audioSettings != null
                ? audioSettings.GetEffectiveVolume(RoAudioBus.Ambient, sourceVolume)
                : Mathf.Clamp01(sourceVolume);
        }

        private void StopRainAudio() {
            if (rainAudioSource == null) {
                return;
            }

            rainAudioSource.Stop();
            rainAudioSource.volume = 0f;
        }

        private void CacheModelMaterials(GameObject modelRoot) {
            modelMaterials.Clear();
            if (modelRoot == null) {
                return;
            }

            materialScratch.Clear();
            materialRendererScratch.Clear();
            modelRoot.GetComponentsInChildren(true, materialRendererScratch);
            for (int i = 0; i < materialRendererScratch.Count; i++) {
                materialRendererScratch[i].GetSharedMaterials(sharedMaterialScratch);
                for (int j = 0; j < sharedMaterialScratch.Count; j++) {
                    Material material = sharedMaterialScratch[j];
                    if (SupportsEnhancedRainWetness(material) && materialScratch.Add(material)) {
                        modelMaterials.Add(material);
                    }
                }

                sharedMaterialScratch.Clear();
            }

            materialRendererScratch.Clear();
            materialScratch.Clear();
        }

        private void CacheGroundMaterials(GameObject groundRoot) {
            groundMaterials.Clear();
            if (groundRoot == null) {
                return;
            }

            materialScratch.Clear();
            materialRendererScratch.Clear();
            groundRoot.GetComponentsInChildren(true, materialRendererScratch);
            for (int i = 0; i < materialRendererScratch.Count; i++) {
                materialRendererScratch[i].GetSharedMaterials(sharedMaterialScratch);
                for (int j = 0; j < sharedMaterialScratch.Count; j++) {
                    Material material = sharedMaterialScratch[j];
                    if (SupportsEnhancedGroundWetness(material) && materialScratch.Add(material)) {
                        groundMaterials.Add(material);
                    }
                }

                sharedMaterialScratch.Clear();
            }

            materialRendererScratch.Clear();
            materialScratch.Clear();
        }

        private static bool SupportsEnhancedRainWetness(Material material) {
            return material != null &&
                material.shader != null &&
                string.Equals(material.shader.name, "Ragnarok/RO Model Enhanced", StringComparison.Ordinal) &&
                material.HasProperty(RainWetnessId);
        }

        private static bool SupportsEnhancedGroundWetness(Material material) {
            return material != null &&
                material.shader != null &&
                string.Equals(material.shader.name, "Ragnarok/RO Ground Enhanced", StringComparison.Ordinal) &&
                material.HasProperty(RainWetnessId);
        }

        private void ApplyModelWetness(float wetness) {
            float resolvedWetness = Mathf.Clamp01(wetness);
            float darkening = settings.Darkening;
            float highlight = settings.Highlight;
            float puddleStrength = settings.PuddleStrength;
            for (int i = 0; i < modelMaterials.Count; i++) {
                Material material = modelMaterials[i];
                if (material == null) {
                    continue;
                }

                SetFloatIfPropertyExists(material, RainWetnessId, resolvedWetness);
                SetFloatIfPropertyExists(material, RainDarkeningId, darkening);
                SetFloatIfPropertyExists(material, RainHighlightId, highlight);
                SetFloatIfPropertyExists(material, RainStreakStrengthId, 0f);
                SetFloatIfPropertyExists(material, RainPuddleStrengthId, puddleStrength);
            }
        }

        private void ApplyGroundWetness(float wetness) {
            float resolvedWetness = Mathf.Clamp01(wetness);
            float darkening = settings.Darkening;
            float highlight = settings.Highlight;
            float puddleStrength = settings.PuddleStrength;
            for (int i = 0; i < groundMaterials.Count; i++) {
                Material material = groundMaterials[i];
                if (material == null) {
                    continue;
                }

                SetFloatIfPropertyExists(material, RainWetnessId, resolvedWetness);
                SetFloatIfPropertyExists(material, RainDarkeningId, darkening);
                SetFloatIfPropertyExists(material, RainHighlightId, highlight);
                SetFloatIfPropertyExists(material, RainPuddleStrengthId, puddleStrength);
            }
        }

        private void ResetModelWetness() {
            for (int i = 0; i < modelMaterials.Count; i++) {
                Material material = modelMaterials[i];
                if (material == null) {
                    continue;
                }

                SetFloatIfPropertyExists(material, RainWetnessId, 0f);
            }

            for (int i = 0; i < groundMaterials.Count; i++) {
                Material material = groundMaterials[i];
                if (material == null) {
                    continue;
                }

                SetFloatIfPropertyExists(material, RainWetnessId, 0f);
            }
        }

        private static void SetFloatIfPropertyExists(Material material, int propertyId, float value) {
            if (material.HasProperty(propertyId)) {
                material.SetFloat(propertyId, value);
            }
        }
    }
}
