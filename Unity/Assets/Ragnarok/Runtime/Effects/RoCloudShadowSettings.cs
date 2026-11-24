using System;
using UnityEngine;

namespace Ragnarok.Effects {
    [Serializable]
    public sealed class RoCloudShadowSettings : ISerializationCallbackReceiver {
        private const int CurrentSerializedVersion = 7;
        private const float MigrationEpsilon = 0.0001f;

        private const bool StageDefaultFollowWindDirection = true;
        private const float StageDefaultWorldStrength = 0.30f;
        private const float StageDefaultActorStrength = 0.20f;
        private const float StageDefaultScaleRepeatsPerWorldUnit = 0.01f;
        private const float StageDefaultSpeedWorldUnitsPerSecond = 0.8f;
        private const float StageDefaultDirectionDegrees = 20f;
        private const float StageDefaultCoverage = 0.60f;
        private const float StageDefaultSoftness = 0.20f;
        private const float StageDefaultSunlitMaskBlend = 0.0f;
        private const bool TunedDefaultFollowWindDirection = false;
        private const float TunedDefaultSpeedWorldUnitsPerSecond = 1.465f;
        private const float TunedDefaultSoftness = 0.10f;
        private const bool CorrectedDefaultFollowWindDirection = true;
        private const float CorrectedDefaultWorldStrength = 0.641f;
        private const float CorrectedDefaultActorStrength = 0.268f;
        private const float CorrectedDefaultScaleRepeatsPerWorldUnit = 0.045f;
        private const float CorrectedDefaultSpeedWorldUnitsPerSecond = 0.465f;
        private const float CorrectedDefaultCoverage = 0.675f;
        private const float CorrectedDefaultSoftness = 0.17f;
        private const bool LatestTunedDefaultFollowWindDirection = false;

        public const bool DefaultEnabled = true;
        public const bool DefaultFollowWindDirection = true;
        public const float DefaultWorldStrength = 0.782f;
        public const float DefaultActorStrength = 0.268f;
        public const float DefaultScaleRepeatsPerWorldUnit = 0.036f;
        public const float DefaultLayerBScaleMultiplier = 2.3f;
        public const float DefaultSpeedWorldUnitsPerSecond = 0.606f;
        public const float DefaultLayerBSpeedMultiplier = 1.6f;
        public const float DefaultDirectionDegrees = 360f;
        public const float DefaultLayerBDirectionOffsetDegrees = 30f;
        public const float DefaultCoverage = 0.599f;
        public const float DefaultSoftness = 0.10f;
        public const float DefaultSunlitMaskBlend = 1.0f;
        public const float DefaultLightmapInfluence = 1.0f;
        public const float DefaultRampSeconds = 3.0f;
        public const float DefaultOvercastCoverageShift = 0.15f;
        public const float DefaultOvercastStrengthMultiplier = 1.3f;

        [SerializeField, HideInInspector] private int serializedVersion;

        public bool enabled = DefaultEnabled;
        public bool followWindDirection = DefaultFollowWindDirection;
        [Range(0f, 1f)] public float worldStrength = DefaultWorldStrength;
        [Range(0f, 1f)] public float actorStrength = DefaultActorStrength;
        [Min(0.0001f)] public float scaleRepeatsPerWorldUnit = DefaultScaleRepeatsPerWorldUnit;
        [Min(0.0001f)] public float layerBScaleMultiplier = DefaultLayerBScaleMultiplier;
        [Min(0f)] public float speedWorldUnitsPerSecond = DefaultSpeedWorldUnitsPerSecond;
        [Min(0f)] public float layerBSpeedMultiplier = DefaultLayerBSpeedMultiplier;
        public float directionDegrees = DefaultDirectionDegrees;
        public float layerBDirectionOffsetDegrees = DefaultLayerBDirectionOffsetDegrees;
        [Range(0f, 1f)] public float coverage = DefaultCoverage;
        [Range(0.0001f, 1f)] public float softness = DefaultSoftness;
        [Range(0f, 1f)] public float sunlitMaskBlend = DefaultSunlitMaskBlend;
        [Range(0f, 1f)] public float lightmapInfluence = DefaultLightmapInfluence;
        [Min(0f)] public float rampSeconds = DefaultRampSeconds;
        [Range(0f, 0.5f)] public float overcastCoverageShift = DefaultOvercastCoverageShift;
        [Range(1f, 2f)] public float overcastStrengthMultiplier = DefaultOvercastStrengthMultiplier;

        public float WorldStrength => Mathf.Clamp01(worldStrength);
        public float ActorStrength => Mathf.Clamp01(actorStrength);
        public float ScaleRepeatsPerWorldUnit => Mathf.Max(0.0001f, scaleRepeatsPerWorldUnit);
        public float LayerBScaleMultiplier => Mathf.Max(0.0001f, layerBScaleMultiplier);
        public float SpeedWorldUnitsPerSecond => Mathf.Max(0f, speedWorldUnitsPerSecond);
        public float LayerBSpeedMultiplier => Mathf.Max(0f, layerBSpeedMultiplier);
        public float Coverage => Mathf.Clamp01(coverage);
        public float Softness => Mathf.Clamp(softness, 0.0001f, 1f);
        public float SunlitMaskBlend => Mathf.Clamp01(sunlitMaskBlend);
        public float LightmapInfluence => Mathf.Clamp01(lightmapInfluence);
        public float RampSeconds => Mathf.Max(0f, rampSeconds);
        public float OvercastCoverageShift => Mathf.Clamp(overcastCoverageShift, 0f, 0.5f);
        public float OvercastStrengthMultiplier => Mathf.Clamp(overcastStrengthMultiplier, 1f, 2f);
        public bool FollowWindDirection => followWindDirection;
        public int Signature => ResolveSignature(this);

        public void OnBeforeSerialize() {
            if (serializedVersion <= 0) {
                serializedVersion = CurrentSerializedVersion;
            }
        }

        public void OnAfterDeserialize() {
            EnsureCurrentDefaults();
        }

        public void EnsureCurrentDefaults() {
            int previousVersion = serializedVersion;

            if (previousVersion > 0 && previousVersion < 2) {
                followWindDirection = DefaultFollowWindDirection;
            }

            if (previousVersion > 0 && previousVersion < 3) {
                overcastCoverageShift = DefaultOvercastCoverageShift;
                overcastStrengthMultiplier = DefaultOvercastStrengthMultiplier;
            }

            if (previousVersion < 4) {
                ApplyTunedDefaultMigration();
            }

            if (previousVersion < 5) {
                ApplyCorrectedDefaultMigration();
            }

            if (previousVersion < 6) {
                ApplyLatestTunedDefaultMigration();
            }

            if (previousVersion < 7) {
                ApplyFollowWindDefaultMigration();
            }

            serializedVersion = CurrentSerializedVersion;
            worldStrength = Mathf.Clamp01(worldStrength);
            actorStrength = Mathf.Clamp01(actorStrength);
            scaleRepeatsPerWorldUnit = Mathf.Max(0.0001f, scaleRepeatsPerWorldUnit);
            layerBScaleMultiplier = Mathf.Max(0.0001f, layerBScaleMultiplier);
            speedWorldUnitsPerSecond = Mathf.Max(0f, speedWorldUnitsPerSecond);
            layerBSpeedMultiplier = Mathf.Max(0f, layerBSpeedMultiplier);
            coverage = Mathf.Clamp01(coverage);
            softness = Mathf.Clamp(softness, 0.0001f, 1f);
            sunlitMaskBlend = Mathf.Clamp01(sunlitMaskBlend);
            lightmapInfluence = Mathf.Clamp01(lightmapInfluence);
            rampSeconds = Mathf.Max(0f, rampSeconds);
            overcastCoverageShift = Mathf.Clamp(overcastCoverageShift, 0f, 0.5f);
            overcastStrengthMultiplier = Mathf.Clamp(overcastStrengthMultiplier, 1f, 2f);
        }

        private void ApplyTunedDefaultMigration() {
            if (followWindDirection == StageDefaultFollowWindDirection) {
                followWindDirection = DefaultFollowWindDirection;
            }

            if (IsStageDefault(worldStrength, StageDefaultWorldStrength)) {
                worldStrength = DefaultWorldStrength;
            }

            if (IsStageDefault(actorStrength, StageDefaultActorStrength)) {
                actorStrength = DefaultActorStrength;
            }

            if (IsStageDefault(scaleRepeatsPerWorldUnit, StageDefaultScaleRepeatsPerWorldUnit)) {
                scaleRepeatsPerWorldUnit = DefaultScaleRepeatsPerWorldUnit;
            }

            if (IsStageDefault(speedWorldUnitsPerSecond, StageDefaultSpeedWorldUnitsPerSecond)) {
                speedWorldUnitsPerSecond = DefaultSpeedWorldUnitsPerSecond;
            }

            if (IsStageDefault(directionDegrees, StageDefaultDirectionDegrees)) {
                directionDegrees = DefaultDirectionDegrees;
            }

            if (IsStageDefault(coverage, StageDefaultCoverage)) {
                coverage = DefaultCoverage;
            }

            if (IsStageDefault(softness, StageDefaultSoftness)) {
                softness = DefaultSoftness;
            }

            if (IsStageDefault(sunlitMaskBlend, StageDefaultSunlitMaskBlend)) {
                sunlitMaskBlend = DefaultSunlitMaskBlend;
            }
        }

        private void ApplyCorrectedDefaultMigration() {
            if (followWindDirection == TunedDefaultFollowWindDirection) {
                followWindDirection = CorrectedDefaultFollowWindDirection;
            }

            if (IsStageDefault(speedWorldUnitsPerSecond, TunedDefaultSpeedWorldUnitsPerSecond)) {
                speedWorldUnitsPerSecond = CorrectedDefaultSpeedWorldUnitsPerSecond;
            }

            if (IsStageDefault(softness, TunedDefaultSoftness)) {
                softness = CorrectedDefaultSoftness;
            }
        }

        private void ApplyLatestTunedDefaultMigration() {
            if (followWindDirection == CorrectedDefaultFollowWindDirection) {
                followWindDirection = LatestTunedDefaultFollowWindDirection;
            }

            if (IsStageDefault(worldStrength, CorrectedDefaultWorldStrength)) {
                worldStrength = DefaultWorldStrength;
            }

            if (IsStageDefault(actorStrength, CorrectedDefaultActorStrength)) {
                actorStrength = DefaultActorStrength;
            }

            if (IsStageDefault(scaleRepeatsPerWorldUnit, CorrectedDefaultScaleRepeatsPerWorldUnit)) {
                scaleRepeatsPerWorldUnit = DefaultScaleRepeatsPerWorldUnit;
            }

            if (IsStageDefault(speedWorldUnitsPerSecond, CorrectedDefaultSpeedWorldUnitsPerSecond)) {
                speedWorldUnitsPerSecond = DefaultSpeedWorldUnitsPerSecond;
            }

            if (IsStageDefault(coverage, CorrectedDefaultCoverage)) {
                coverage = DefaultCoverage;
            }

            if (IsStageDefault(softness, CorrectedDefaultSoftness)) {
                softness = DefaultSoftness;
            }
        }

        private void ApplyFollowWindDefaultMigration() {
            if (followWindDirection == LatestTunedDefaultFollowWindDirection) {
                followWindDirection = DefaultFollowWindDirection;
            }
        }

        public static int ResolveSignature(RoCloudShadowSettings settings) {
            if (settings == null) {
                return 0;
            }

            unchecked {
                int hash = 17;
                hash = hash * 31 + (settings.enabled ? 1 : 0);
                hash = hash * 31 + (settings.followWindDirection ? 1 : 0);
                hash = hash * 31 + Quantize(settings.worldStrength);
                hash = hash * 31 + Quantize(settings.actorStrength);
                hash = hash * 31 + Quantize(settings.scaleRepeatsPerWorldUnit);
                hash = hash * 31 + Quantize(settings.layerBScaleMultiplier);
                hash = hash * 31 + Quantize(settings.speedWorldUnitsPerSecond);
                hash = hash * 31 + Quantize(settings.layerBSpeedMultiplier);
                hash = hash * 31 + Quantize(settings.directionDegrees);
                hash = hash * 31 + Quantize(settings.layerBDirectionOffsetDegrees);
                hash = hash * 31 + Quantize(settings.coverage);
                hash = hash * 31 + Quantize(settings.softness);
                hash = hash * 31 + Quantize(settings.sunlitMaskBlend);
                hash = hash * 31 + Quantize(settings.lightmapInfluence);
                hash = hash * 31 + Quantize(settings.rampSeconds);
                hash = hash * 31 + Quantize(settings.overcastCoverageShift);
                hash = hash * 31 + Quantize(settings.overcastStrengthMultiplier);
                return hash;
            }
        }

        private static int Quantize(float value) => Mathf.RoundToInt(value * 10000f);
        private static bool IsStageDefault(float value, float stageDefault) => Mathf.Abs(value - stageDefault) <= MigrationEpsilon;
    }

    public static class RoCloudShadowMath {
        public static Vector2 AccumulateOffset(
            Vector2 currentOffset,
            float directionDegrees,
            float speedWorldUnitsPerSecond,
            float scaleRepeatsPerWorldUnit,
            float deltaSeconds
        ) {
            if (deltaSeconds <= 0f || speedWorldUnitsPerSecond <= 0f || scaleRepeatsPerWorldUnit <= 0f) {
                return currentOffset;
            }

            float radians = directionDegrees * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 offset = currentOffset - direction * (speedWorldUnitsPerSecond * scaleRepeatsPerWorldUnit * deltaSeconds);
            return new Vector2(Mathf.Repeat(offset.x, OffsetRepeatDomain), Mathf.Repeat(offset.y, OffsetRepeatDomain));
        }

        /// <summary>
        /// Where a drift offset wraps (uv units of the base scale). Not 1: the shader samples the noise at other scales too
        /// (the look's cloud size, the warp and macro fields), and a wrap at 1 lands mid-pattern there - a cloud jumped to
        /// another shape and place. 4096 wraps about once in two days of play and still leaves the offset a precision of
        /// ~0.0005 uv (hundredths of a world unit).
        /// </summary>
        public const float OffsetRepeatDomain = 4096f;

        public static void AccumulateLayerOffsets(
            RoCloudShadowSettings settings,
            float baseDirectionDegrees,
            float deltaSeconds,
            ref Vector2 offsetA,
            ref Vector2 offsetB
        ) {
            settings ??= new RoCloudShadowSettings();
            settings.EnsureCurrentDefaults();
            float scaleA = settings.ScaleRepeatsPerWorldUnit;
            float scaleB = scaleA * settings.LayerBScaleMultiplier;
            offsetA = AccumulateOffset(
                offsetA,
                baseDirectionDegrees,
                settings.SpeedWorldUnitsPerSecond,
                scaleA,
                deltaSeconds);
            offsetB = AccumulateOffset(
                offsetB,
                baseDirectionDegrees + settings.layerBDirectionOffsetDegrees,
                settings.SpeedWorldUnitsPerSecond * settings.LayerBSpeedMultiplier,
                scaleB,
                deltaSeconds);
        }

        public static float ResolveWindDirectionDegrees(Vector2 effectiveDirection) {
            if (effectiveDirection.sqrMagnitude <= 0.0001f) {
                return RoCloudShadowSettings.DefaultDirectionDegrees;
            }

            return Mathf.Atan2(effectiveDirection.y, effectiveDirection.x) * Mathf.Rad2Deg;
        }

        public static float ResolveBaseDirectionDegrees(RoCloudShadowSettings settings, float? windDirectionDegrees) {
            settings ??= new RoCloudShadowSettings();
            settings.EnsureCurrentDefaults();
            return settings.FollowWindDirection && windDirectionDegrees.HasValue
                ? windDirectionDegrees.Value
                : settings.directionDegrees;
        }

        public static Vector4 PackOffsetScale(Vector2 offset, float scaleRepeatsPerWorldUnit) {
            return new Vector4(offset.x, offset.y, Mathf.Max(0.0001f, scaleRepeatsPerWorldUnit), 0f);
        }

        public static Vector4 PackParams(RoCloudShadowSettings settings, float currentFade, float daylightFactor, float overcastFactor) {
            settings ??= new RoCloudShadowSettings();
            settings.EnsureCurrentDefaults();
            return new Vector4(
                ResolveEffectiveCoverage(settings, overcastFactor),
                settings.Softness,
                ResolveWorldStrength(settings, currentFade, daylightFactor, overcastFactor),
                settings.SunlitMaskBlend);
        }

        public static float ResolveEffectiveCoverage(RoCloudShadowSettings settings, float overcastFactor) {
            if (settings == null) {
                return RoCloudShadowSettings.DefaultCoverage;
            }

            settings.EnsureCurrentDefaults();
            return Mathf.Clamp01(settings.Coverage - settings.OvercastCoverageShift * Mathf.Clamp01(overcastFactor));
        }

        public static float ResolveWorldStrength(RoCloudShadowSettings settings, float fade, float daylightFactor, float overcastFactor = 0f) {
            if (settings == null) {
                return 0f;
            }

            settings.EnsureCurrentDefaults();
            float strength = settings.WorldStrength * Mathf.Clamp01(fade) * Mathf.Clamp01(daylightFactor);
            return Mathf.Clamp01(strength * ResolveOvercastStrengthMultiplier(settings, overcastFactor));
        }

        public static float ResolveActorStrength(RoCloudShadowSettings settings, float fade, float daylightFactor, float overcastFactor = 0f) {
            if (settings == null) {
                return 0f;
            }

            settings.EnsureCurrentDefaults();
            float strength = settings.ActorStrength * Mathf.Clamp01(fade) * Mathf.Clamp01(daylightFactor);
            return Mathf.Clamp01(strength * ResolveOvercastStrengthMultiplier(settings, overcastFactor));
        }

        private static float ResolveOvercastStrengthMultiplier(RoCloudShadowSettings settings, float overcastFactor) {
            return Mathf.Lerp(1f, settings.OvercastStrengthMultiplier, Mathf.Clamp01(overcastFactor));
        }

        public static float ResolveTargetFade(RoCloudShadowSettings settings, bool skylessMap) {
            if (settings == null || skylessMap) {
                return 0f;
            }

            settings.EnsureCurrentDefaults();
            return settings.enabled ? 1f : 0f;
        }

        public static float MoveFade(float current, float target, float deltaSeconds, float rampSeconds) {
            current = Mathf.Clamp01(current);
            target = Mathf.Clamp01(target);
            if (rampSeconds <= 0f || deltaSeconds <= 0f) {
                return rampSeconds <= 0f ? target : current;
            }

            return Mathf.MoveTowards(current, target, deltaSeconds / rampSeconds);
        }

        public static bool ShouldUpdateFactor(float lastApplied, float next, float threshold = 0.005f) {
            return Mathf.Abs(Mathf.Clamp01(next) - Mathf.Clamp01(lastApplied)) > Mathf.Max(0f, threshold);
        }

        public static bool ShouldUpdateDaylightFactor(float lastApplied, float next, float threshold = 0.005f) {
            return ShouldUpdateFactor(lastApplied, next, threshold);
        }
    }
}
