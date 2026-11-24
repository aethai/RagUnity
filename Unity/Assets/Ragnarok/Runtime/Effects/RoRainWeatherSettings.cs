using System;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ragnarok.Effects {
    [Serializable]
    public sealed class RoRainWeatherSettings : ISerializationCallbackReceiver {
        private const int CurrentSerializedVersion = 5;
        private const float LegacyDefaultWetness = 0.75f;
        private const float LegacyDefaultDarkening = 0.22f;
        private const float LegacyDefaultHighlight = 0.2f;
#if UNITY_EDITOR
        private const string DefaultRainLoopClipAssetPath = "Assets/Sound/rain.wav";
#endif

        public const float DefaultIntensity = 1f;
        public const float DefaultRampUpSeconds = 8f;
        public const float DefaultRampDownSeconds = 4f;
        public const float DefaultParticleRateScale = 1f;
        public const float DefaultParticleAreaScale = 1f;
        public const float DefaultRainLoopVolume = 0.55f;
        public const float DefaultWetness = 0.8f;
        public const float DefaultDarkening = 1f;
        public const float DefaultHighlight = 0.1f;
        public const float DefaultPuddleStrength = 0.55f;
        public const float DefaultModelWetness = DefaultWetness;
        public const float DefaultModelDarkening = DefaultDarkening;
        public const float DefaultModelHighlight = DefaultHighlight;
        public const float DefaultModelStreakStrength = DefaultPuddleStrength;
        public const float DefaultGroundWetness = DefaultWetness;
        public const float DefaultGroundDarkening = DefaultDarkening;
        public const float DefaultGroundHighlight = DefaultHighlight;
        public const float DefaultGroundPuddleStrength = DefaultPuddleStrength;
        public static readonly Vector3 DefaultCameraLocalOffset = new Vector3(0f, 0f, 14f);

        [SerializeField, HideInInspector] private int serializedVersion;
        [FormerlySerializedAs("windDirection")]
        [SerializeField, HideInInspector] private Vector2 legacyWindDirection;
        [FormerlySerializedAs("windStrength")]
        [SerializeField, HideInInspector] private float legacyWindStrength;
        [SerializeField, HideInInspector] private bool legacyWindMigrated;

        public bool enabled;
        [Range(0f, 1f)] public float intensity = DefaultIntensity;
        [Min(0f)] public float rampUpSeconds = DefaultRampUpSeconds;
        [Min(0f)] public float rampDownSeconds = DefaultRampDownSeconds;
        [Min(0f)] public float particleRateScale = DefaultParticleRateScale;
        [Min(0f)] public float particleAreaScale = DefaultParticleAreaScale;
        public Vector3 cameraLocalOffset = DefaultCameraLocalOffset;
        public AudioClip rainLoopClip;
        [Range(0f, 1f)] public float rainLoopVolume = DefaultRainLoopVolume;
        public bool affectModels = true;
        public bool affectGround = true;
        [Range(0f, 1f)] public float modelWetness = DefaultModelWetness;
        [FormerlySerializedAs("groundWetness")]
        [Range(0f, 1f)] public float wetness = DefaultWetness;
        [FormerlySerializedAs("groundDarkening")]
        [Range(0f, 1f)] public float darkening = DefaultDarkening;
        [FormerlySerializedAs("groundHighlight")]
        [Range(0f, 1f)] public float highlight = DefaultHighlight;
        [FormerlySerializedAs("groundPuddleStrength")]
        [Range(0f, 1f)] public float puddleStrength = DefaultPuddleStrength;

        public float TargetIntensity => Mathf.Clamp01(intensity);
        public float RampUpSeconds => Mathf.Max(0f, rampUpSeconds);
        public float RampDownSeconds => Mathf.Max(0f, rampDownSeconds);
        public Vector3 EffectiveCameraLocalOffset =>
            cameraLocalOffset.sqrMagnitude > 0.0001f ? cameraLocalOffset : DefaultCameraLocalOffset;
        public float ModelWetnessAt(float normalizedRainIntensity) =>
            affectModels ? Mathf.Clamp01(normalizedRainIntensity) * Mathf.Clamp01(modelWetness) : 0f;
        public float GroundWetnessAt(float normalizedRainIntensity) =>
            affectGround ? Mathf.Clamp01(normalizedRainIntensity) * Mathf.Clamp01(wetness) : 0f;
        public float Darkening => Mathf.Clamp01(darkening);
        public float Highlight => Mathf.Clamp01(highlight);
        public float PuddleStrength => Mathf.Clamp01(puddleStrength);

        public void OnBeforeSerialize() {
            if (serializedVersion <= 0) {
                serializedVersion = CurrentSerializedVersion;
            }
        }

        public void OnAfterDeserialize() {
            EnsureCurrentDefaults();
        }

        public void EnsureCurrentDefaults() {
            if (serializedVersion < 5 && Approximately(modelWetness, 0f)) {
                modelWetness = wetness > 0f ? wetness : DefaultModelWetness;
            }

            if (serializedVersion < CurrentSerializedVersion) {
                if (Approximately(wetness, LegacyDefaultWetness)) {
                    wetness = DefaultWetness;
                }

                if (Approximately(darkening, LegacyDefaultDarkening)) {
                    darkening = DefaultDarkening;
                }

                if (Approximately(highlight, LegacyDefaultHighlight)) {
                    highlight = DefaultHighlight;
                }
            }

            serializedVersion = CurrentSerializedVersion;
            intensity = Mathf.Clamp01(intensity);
            modelWetness = Mathf.Clamp01(modelWetness);
            wetness = Mathf.Clamp01(wetness);
            darkening = Mathf.Clamp01(darkening);
            highlight = Mathf.Clamp01(highlight);
            puddleStrength = Mathf.Clamp01(puddleStrength);
            rampUpSeconds = Mathf.Max(0f, rampUpSeconds);
            rampDownSeconds = Mathf.Max(0f, rampDownSeconds);
            particleRateScale = Mathf.Max(0f, particleRateScale);
            particleAreaScale = Mathf.Max(0f, particleAreaScale);
            rainLoopVolume = Mathf.Clamp01(rainLoopVolume);
        }

#if UNITY_EDITOR
        public void EnsureEditorDefaults() {
            EnsureCurrentDefaults();
            if (rainLoopClip == null) {
                rainLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultRainLoopClipAssetPath);
            }
        }
#endif

        public bool TryApplyLegacyWindSettings(RoWeatherWindSettings windSettings) {
            if (legacyWindMigrated || windSettings == null) {
                return false;
            }

            legacyWindMigrated = true;
            bool hasLegacyWind = legacyWindDirection.sqrMagnitude > 0.0001f || legacyWindStrength > 0f;
            if (!hasLegacyWind) {
                return false;
            }

            windSettings.direction = legacyWindDirection.sqrMagnitude > 0.0001f
                ? legacyWindDirection
                : RoWeatherWindSettings.DefaultDirection;
            windSettings.strength = legacyWindStrength > 0f
                ? legacyWindStrength
                : RoWeatherWindSettings.DefaultStrength;
            windSettings.EnsureCurrentDefaults();
            return true;
        }

        public RoWeatherParticleSpec BuildRainSpec(RoWeatherWindSettings windSettings = null) {
            EnsureCurrentDefaults();
            RoWeatherParticleSpec spec = RoWeatherParticleSpec.Rain();
            float areaScale = Mathf.Max(0.01f, particleAreaScale);
            spec.Width *= areaScale;
            spec.Depth *= areaScale;
            spec.RateOverTime *= Mathf.Max(0f, particleRateScale);
            if (windSettings != null) {
                windSettings.EnsureCurrentDefaults();
                spec.WindVelocity = windSettings.Velocity;
            } else {
                spec.WindVelocity = RoWeatherWindSettings.DefaultDirection * RoWeatherWindSettings.DefaultStrength;
            }

            return spec;
        }

        private static bool Approximately(float a, float b) {
            return Mathf.Abs(a - b) <= 0.0001f;
        }
    }
}
