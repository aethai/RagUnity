using UnityEngine;

namespace Ragnarok.Effects {
    [DisallowMultipleComponent]
    public sealed class RoCloudShadowController : MonoBehaviour {
        private const string DefaultNoiseResourcePath = "Weather/RoCloudShadowNoise";

        private static readonly int CloudShadowNoiseId = Shader.PropertyToID("_RoCloudShadowNoise");
        private static readonly int CloudShadowOffsetScaleAId = Shader.PropertyToID("_RoCloudShadowOffsetScaleA");
        private static readonly int CloudShadowOffsetScaleBId = Shader.PropertyToID("_RoCloudShadowOffsetScaleB");
        private static readonly int CloudShadowParamsId = Shader.PropertyToID("_RoCloudShadowParams");
        private static readonly int CloudShadowLightmapInfluenceId = Shader.PropertyToID("_RoCloudShadowLightmapInfluence");
        private static readonly int CloudShadowActorStrengthId = Shader.PropertyToID("_RoCloudShadowActorStrength");

        [SerializeField] private Texture2D noiseTextureOverride;

        private RoCloudShadowSettings settings;
        private Texture2D loadedNoiseTexture;
        private bool isSkylessMap;
        private bool initialized;
        private bool staticGlobalsDirty;
        private int lastSettingsSignature;
        private Vector2 offsetA;
        private Vector2 offsetB;
        private float currentFade;
        private float targetFade;
        private float daylightFactor = 1f;
        private float appliedDaylightFactor = 1f;
        private float overcastFactor;
        private float appliedOvercastFactor;
        private float baseDirectionDegrees = RoCloudShadowSettings.DefaultDirectionDegrees;

        public RoCloudShadowSettings Settings => settings;
        public float CurrentStrength => RoCloudShadowMath.ResolveWorldStrength(settings, currentFade, daylightFactor, overcastFactor);
        public float TargetStrength => RoCloudShadowMath.ResolveWorldStrength(settings, targetFade, daylightFactor, overcastFactor);
        public float CurrentFade => currentFade;
        public float TargetFade => targetFade;
        public float DaylightFactor => daylightFactor;
        public float OvercastFactor => overcastFactor;
        public float BaseDirectionDegrees => baseDirectionDegrees;
        public bool IsSkylessMap => isSkylessMap;

        private void Update() {
            if (!initialized) {
                return;
            }

            float deltaSeconds = Time.deltaTime;
            RoCloudShadowMath.AccumulateLayerOffsets(settings, baseDirectionDegrees, deltaSeconds, ref offsetA, ref offsetB);
            float nextFade = RoCloudShadowMath.MoveFade(
                currentFade,
                targetFade,
                deltaSeconds,
                settings != null ? settings.RampSeconds : RoCloudShadowSettings.DefaultRampSeconds);
            bool fadeChanged = !Mathf.Approximately(nextFade, currentFade);
            currentFade = nextFade;
            ApplyGlobals(staticGlobalsDirty || fadeChanged);
            staticGlobalsDirty = false;
        }

        private void OnEnable() {
            if (!initialized) {
                return;
            }

            ApplyNoiseTexture();
            staticGlobalsDirty = true;
            ApplyGlobals(forceStatic: true);
        }

        private void OnDisable() {
            ResetGlobals();
        }

        private void OnDestroy() {
            ResetGlobals();
        }

        public void Initialize(RoCloudShadowSettings cloudShadowSettings, bool skylessMap, float? windDirectionDegrees = null) {
            settings = cloudShadowSettings ?? new RoCloudShadowSettings();
            settings.EnsureCurrentDefaults();
            isSkylessMap = skylessMap;
            baseDirectionDegrees = RoCloudShadowMath.ResolveBaseDirectionDegrees(settings, windDirectionDegrees);
            offsetA = Vector2.zero;
            offsetB = Vector2.zero;
            currentFade = 0f;
            targetFade = RoCloudShadowMath.ResolveTargetFade(settings, isSkylessMap);
            daylightFactor = 1f;
            appliedDaylightFactor = 1f;
            overcastFactor = 0f;
            appliedOvercastFactor = 0f;
            lastSettingsSignature = settings.Signature;
            initialized = true;
            staticGlobalsDirty = true;
            ApplyNoiseTexture();
            ApplyGlobals(forceStatic: true);
        }

        public void SyncSettings(RoCloudShadowSettings cloudShadowSettings, bool skylessMap, float? windDirectionDegrees = null) {
            if (!initialized) {
                return;
            }

            settings = cloudShadowSettings ?? settings ?? new RoCloudShadowSettings();
            settings.EnsureCurrentDefaults();
            int settingsSignature = settings.Signature;
            bool settingsChanged = settingsSignature != lastSettingsSignature;
            bool skylessChanged = skylessMap != isSkylessMap;
            baseDirectionDegrees = RoCloudShadowMath.ResolveBaseDirectionDegrees(settings, windDirectionDegrees);
            lastSettingsSignature = settingsSignature;
            isSkylessMap = skylessMap;

            if (settingsChanged || skylessChanged) {
                targetFade = RoCloudShadowMath.ResolveTargetFade(settings, isSkylessMap);
                staticGlobalsDirty = true;
            }
        }

        public void SetEnabled(bool enabled, bool immediate = false) {
            if (!initialized || settings == null) {
                return;
            }

            settings.enabled = enabled;
            settings.EnsureCurrentDefaults();
            lastSettingsSignature = settings.Signature;
            targetFade = RoCloudShadowMath.ResolveTargetFade(settings, isSkylessMap);
            if (immediate) {
                currentFade = targetFade;
            }

            ApplyGlobals(forceStatic: true);
            staticGlobalsDirty = false;
        }

        public void SetDaylightFactor(float factor) {
            daylightFactor = Mathf.Clamp01(factor);
            if (RoCloudShadowMath.ShouldUpdateFactor(appliedDaylightFactor, daylightFactor)) {
                staticGlobalsDirty = true;
            }
        }

        public void SetOvercastFactor(float factor) {
            overcastFactor = Mathf.Clamp01(factor);
            if (RoCloudShadowMath.ShouldUpdateFactor(appliedOvercastFactor, overcastFactor)) {
                staticGlobalsDirty = true;
            }
        }

        public void ClearRuntime() {
            initialized = false;
            currentFade = 0f;
            targetFade = 0f;
            daylightFactor = 1f;
            appliedDaylightFactor = 1f;
            overcastFactor = 0f;
            appliedOvercastFactor = 0f;
            baseDirectionDegrees = RoCloudShadowSettings.DefaultDirectionDegrees;
            isSkylessMap = false;
            offsetA = Vector2.zero;
            offsetB = Vector2.zero;
            ResetGlobals();
        }

        public static void ResetGlobals() {
            Shader.SetGlobalVector(CloudShadowOffsetScaleAId, Vector4.zero);
            Shader.SetGlobalVector(CloudShadowOffsetScaleBId, Vector4.zero);
            Shader.SetGlobalVector(
                CloudShadowParamsId,
                new Vector4(
                    RoCloudShadowSettings.DefaultCoverage,
                    RoCloudShadowSettings.DefaultSoftness,
                    0f,
                    RoCloudShadowSettings.DefaultSunlitMaskBlend));
            Shader.SetGlobalFloat(CloudShadowLightmapInfluenceId, RoCloudShadowSettings.DefaultLightmapInfluence);
            Shader.SetGlobalFloat(CloudShadowActorStrengthId, 0f);
        }

        private void ApplyNoiseTexture() {
            loadedNoiseTexture = noiseTextureOverride != null
                ? noiseTextureOverride
                : loadedNoiseTexture != null
                    ? loadedNoiseTexture
                    : Resources.Load<Texture2D>(DefaultNoiseResourcePath);
            if (loadedNoiseTexture != null) {
                Shader.SetGlobalTexture(CloudShadowNoiseId, loadedNoiseTexture);
            }
        }

        private void ApplyGlobals(bool forceStatic) {
            RoCloudShadowSettings activeSettings = settings ?? new RoCloudShadowSettings();
            activeSettings.EnsureCurrentDefaults();
            float scaleA = activeSettings.ScaleRepeatsPerWorldUnit;
            float scaleB = scaleA * activeSettings.LayerBScaleMultiplier;
            Shader.SetGlobalVector(CloudShadowOffsetScaleAId, RoCloudShadowMath.PackOffsetScale(offsetA, scaleA));
            Shader.SetGlobalVector(CloudShadowOffsetScaleBId, RoCloudShadowMath.PackOffsetScale(offsetB, scaleB));

            if (!forceStatic) {
                return;
            }

            Shader.SetGlobalVector(CloudShadowParamsId, RoCloudShadowMath.PackParams(activeSettings, currentFade, daylightFactor, overcastFactor));
            Shader.SetGlobalFloat(CloudShadowLightmapInfluenceId, activeSettings.LightmapInfluence);
            Shader.SetGlobalFloat(CloudShadowActorStrengthId, RoCloudShadowMath.ResolveActorStrength(activeSettings, currentFade, daylightFactor, overcastFactor));
            appliedDaylightFactor = daylightFactor;
            appliedOvercastFactor = overcastFactor;
        }
    }
}
