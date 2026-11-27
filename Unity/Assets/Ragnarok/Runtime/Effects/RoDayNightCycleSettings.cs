using System;
using UnityEngine;

namespace Ragnarok.Effects {
    public readonly struct RoDayNightCycleSample {
        public RoDayNightCycleSample(
            float normalizedTime,
            bool isNight,
            Vector3 lightDirection,
            Color lightColor,
            Color ambientColor,
            float intensity
        ) {
            NormalizedTime = normalizedTime;
            IsNight = isNight;
            LightDirection = lightDirection;
            LightColor = lightColor;
            AmbientColor = ambientColor;
            Intensity = intensity;
        }

        public float NormalizedTime { get; }
        public bool IsNight { get; }
        public Vector3 LightDirection { get; }
        public Color LightColor { get; }
        public Color AmbientColor { get; }
        public float Intensity { get; }
    }

    [Serializable]
    public sealed class RoDayNightCycleSettings : ISerializationCallbackReceiver {
        private const int CurrentSerializedVersion = 3;

        public const float DefaultDayMinutes = 20f;
        public const float DefaultNightMinutes = 10f;
        public const float DefaultStartNormalizedTime = 0f;
        public const float DefaultUpdateIntervalSeconds = 0.5f;
        public const float DefaultMaxHorizontalLightX = 0.72f;
        public const float DefaultMinimumLightElevation = 0.28f;
        public const float DefaultNightTransitionFraction = 0.22f;
        public const float DefaultDayIntensity = 1f;
        public const float DefaultNightIntensity = 0.22f;
        public const float DefaultHorizonIntensity = 0.38f;
        public const float DefaultActorNightStrength = 0.35f;
        public static readonly Color DefaultDawnLightColor = new Color(1f, 0.55f, 0.32f, 1f);
        public static readonly Color DefaultDayLightColor = new Color(1f, 1f, 0.98f, 1f);
        public static readonly Color DefaultDuskLightColor = new Color(1f, 0.43f, 0.28f, 1f);
        public static readonly Color DefaultNightLightColor = new Color(0.22f, 0.32f, 0.62f, 1f);
        public static readonly Color DefaultDawnAmbientColor = new Color(0.28f, 0.22f, 0.2f, 1f);
        public static readonly Color DefaultDayAmbientColor = new Color(0.36f, 0.37f, 0.36f, 1f);
        public static readonly Color DefaultDuskAmbientColor = new Color(0.27f, 0.19f, 0.2f, 1f);
        public static readonly Color DefaultNightAmbientColor = new Color(0.22f, 0.25f, 0.40f, 1f);

        [SerializeField, HideInInspector] private int serializedVersion;

        public bool enabled;
        [Min(0.1f)] public float dayMinutes = DefaultDayMinutes;
        [Min(0.1f)] public float nightMinutes = DefaultNightMinutes;
        [Range(0f, 1f)] public float startNormalizedTime = DefaultStartNormalizedTime;
        [Min(0.02f)] public float updateIntervalSeconds = DefaultUpdateIntervalSeconds;
        [Range(0.05f, 0.95f)] public float maxHorizontalLightX = DefaultMaxHorizontalLightX;
        [Range(0.02f, 1f)] public float minimumLightElevation = DefaultMinimumLightElevation;
        [Range(0f, 0.49f)] public float nightTransitionFraction = DefaultNightTransitionFraction;
        [Min(0f)] public float dayIntensity = DefaultDayIntensity;
        [Min(0f)] public float nightIntensity = DefaultNightIntensity;
        [Min(0f)] public float horizonIntensity = DefaultHorizonIntensity;
        [Range(0f, 1f)] public float actorNightStrength = DefaultActorNightStrength;
        public Color dawnLightColor = DefaultDawnLightColor;
        public Color dayLightColor = DefaultDayLightColor;
        public Color duskLightColor = DefaultDuskLightColor;
        public Color nightLightColor = DefaultNightLightColor;
        public Color dawnAmbientColor = DefaultDawnAmbientColor;
        public Color dayAmbientColor = DefaultDayAmbientColor;
        public Color duskAmbientColor = DefaultDuskAmbientColor;
        public Color nightAmbientColor = DefaultNightAmbientColor;

        public float DaySeconds => Mathf.Max(0.1f, dayMinutes) * 60f;
        public float NightSeconds => Mathf.Max(0.1f, nightMinutes) * 60f;
        public float TotalSeconds => DaySeconds + NightSeconds;
        public float NormalizedStartTime => Mathf.Repeat(startNormalizedTime, 1f);
        public float StartSeconds => NormalizedStartTime * TotalSeconds;
        public float UpdateIntervalSeconds => Mathf.Max(0.02f, updateIntervalSeconds);
        public float MaxHorizontalLightX => Mathf.Clamp(maxHorizontalLightX, 0.05f, 0.95f);
        public float MinimumLightElevation => Mathf.Clamp(minimumLightElevation, 0.02f, 1f);

        /// <summary>
        /// The highest the sun climbs, in degrees (the look profile's "sunmax"; 90 = no limit): a noon sun straight above
        /// hides every shadow under its caster, a lower one keeps long diagonal shadows (depth) all day.
        /// </summary>
        public static float LookMaxElevationDegrees = 90f;
        public float NightTransitionFraction => Mathf.Clamp(nightTransitionFraction, 0f, 0.49f);

        public void OnBeforeSerialize() {
            if (serializedVersion <= 0) {
                serializedVersion = CurrentSerializedVersion;
            }
        }

        public void OnAfterDeserialize() {
            EnsureCurrentDefaults();
        }

        public void EnsureCurrentDefaults() {
            if (serializedVersion < 2) {
                // v2 neutralised the daytime light colour: the old warm default read as too yellow versus
                // the original client. This field is hidden from the inspector, so there is no user
                // customisation to preserve.
                dayLightColor = DefaultDayLightColor;
            }

            if (serializedVersion < 3) {
                // v3: the original RO has no dawn/dusk -- it switches straight between day and night with
                // nothing orange in between. Fold the dawn/dusk colours into the day look so the only blend
                // is a direct day<->night cross-fade.
                dawnLightColor = dayLightColor;
                duskLightColor = dayLightColor;
                dawnAmbientColor = dayAmbientColor;
                duskAmbientColor = dayAmbientColor;
            }

            serializedVersion = CurrentSerializedVersion;
            dayMinutes = Mathf.Max(0.1f, dayMinutes);
            nightMinutes = Mathf.Max(0.1f, nightMinutes);
            startNormalizedTime = Mathf.Repeat(startNormalizedTime, 1f);
            updateIntervalSeconds = Mathf.Max(0.02f, updateIntervalSeconds);
            maxHorizontalLightX = Mathf.Clamp(maxHorizontalLightX, 0.05f, 0.95f);
            minimumLightElevation = Mathf.Clamp(minimumLightElevation, 0.02f, 1f);
            nightTransitionFraction = Mathf.Clamp(nightTransitionFraction, 0f, 0.49f);
            dayIntensity = Mathf.Max(0f, dayIntensity);
            nightIntensity = Mathf.Max(0f, nightIntensity);
            horizonIntensity = Mathf.Max(0f, horizonIntensity);
            actorNightStrength = Mathf.Clamp01(actorNightStrength);
        }

        public RoDayNightCycleSample Evaluate(float elapsedSeconds) => Evaluate(this, elapsedSeconds);

        public static RoDayNightCycleSample Evaluate(RoDayNightCycleSettings settings, float elapsedSeconds) {
            settings ??= new RoDayNightCycleSettings();
            settings.EnsureCurrentDefaults();

            float totalSeconds = settings.TotalSeconds;
            float cycleSeconds = Mathf.Repeat(elapsedSeconds, totalSeconds);
            float normalizedTime = cycleSeconds / totalSeconds;
            float daySeconds = settings.DaySeconds;
            bool isNight = cycleSeconds >= daySeconds;

            return isNight
                ? EvaluateNight(settings, cycleSeconds - daySeconds, normalizedTime)
                : EvaluateDay(settings, cycleSeconds, normalizedTime);
        }

        private static RoDayNightCycleSample EvaluateDay(
            RoDayNightCycleSettings settings,
            float dayCycleSeconds,
            float normalizedTime
        ) {
            float dayProgress = Mathf.Clamp01(dayCycleSeconds / settings.DaySeconds);
            float height = Mathf.Sin(dayProgress * Mathf.PI);
            Vector3 direction = BuildClampedLightDirection(
                settings,
                Mathf.Lerp(-1f, 1f, dayProgress),
                Mathf.Max(0.08f, height),
                Mathf.Sin(dayProgress * Mathf.PI) * 0.24f
            );

            Color lightColor = EvaluateDayLightColor(settings, dayProgress);
            Color ambientColor = EvaluateDayAmbientColor(settings, dayProgress);
            // Brightness ramps up across the dawn window, holds at full through the day and ramps down at
            // dusk, so a fresh @day reaches full daylight within the (short) transition window instead of
            // only at noon. The sun direction still arcs slowly across the whole day, so shadows keep moving.
            float transition = Mathf.Clamp(settings.NightTransitionFraction, 0.001f, 0.49f);
            float intensityBlend = dayProgress < transition
                ? Mathf.SmoothStep(0f, 1f, dayProgress / transition)
                : (dayProgress > 1f - transition
                    ? Mathf.SmoothStep(0f, 1f, (1f - dayProgress) / transition)
                    : 1f);
            float intensity = Mathf.Lerp(settings.horizonIntensity, settings.dayIntensity, intensityBlend);
            return new RoDayNightCycleSample(
                normalizedTime,
                isNight: false,
                direction,
                lightColor,
                ambientColor,
                intensity
            );
        }

        private static RoDayNightCycleSample EvaluateNight(
            RoDayNightCycleSettings settings,
            float nightCycleSeconds,
            float normalizedTime
        ) {
            float nightProgress = Mathf.Clamp01(nightCycleSeconds / settings.NightSeconds);
            float height = 0.18f + Mathf.Sin(nightProgress * Mathf.PI) * 0.12f;
            Vector3 direction = BuildClampedLightDirection(
                settings,
                Mathf.Lerp(1f, -1f, nightProgress),
                height,
                -Mathf.Sin(nightProgress * Mathf.PI) * 0.24f
            );

            Color lightColor = settings.nightLightColor;
            Color ambientColor = settings.nightAmbientColor;
            float intensity = settings.nightIntensity;
            float transition = settings.NightTransitionFraction;
            if (transition > 0.0001f && nightProgress < transition) {
                float blend = Mathf.SmoothStep(0f, 1f, nightProgress / transition);
                lightColor = Color.Lerp(settings.duskLightColor, settings.nightLightColor, blend);
                ambientColor = Color.Lerp(settings.duskAmbientColor, settings.nightAmbientColor, blend);
                intensity = Mathf.Lerp(settings.horizonIntensity, settings.nightIntensity, blend);
            } else if (transition > 0.0001f && nightProgress > 1f - transition) {
                float blend = Mathf.SmoothStep(0f, 1f, (nightProgress - (1f - transition)) / transition);
                lightColor = Color.Lerp(settings.nightLightColor, settings.dawnLightColor, blend);
                ambientColor = Color.Lerp(settings.nightAmbientColor, settings.dawnAmbientColor, blend);
                intensity = Mathf.Lerp(settings.nightIntensity, settings.horizonIntensity, blend);
            }

            return new RoDayNightCycleSample(
                normalizedTime,
                isNight: true,
                direction,
                lightColor,
                ambientColor,
                intensity
            );
        }

        private static Vector3 BuildClampedLightDirection(
            RoDayNightCycleSettings settings,
            float horizontalX,
            float elevationY,
            float forwardZ
        ) {
            float y = Mathf.Max(settings.MinimumLightElevation, elevationY);
            float normalizedX = Mathf.Clamp(horizontalX, -1f, 1f) * settings.MaxHorizontalLightX;
            float remaining = Mathf.Max(0.0001f, 1f - normalizedX * normalizedX);
            float x = Mathf.Sign(normalizedX) *
                Mathf.Sqrt((normalizedX * normalizedX) * (y * y + forwardZ * forwardZ) / remaining);
            Vector3 direction = new Vector3(x, y, forwardZ);
            if (LookMaxElevationDegrees < 89.9f) {
                // keep the sun's azimuth, stretch its horizontal part until the elevation is at most the limit
                var horizontal = new Vector2(x, forwardZ);
                if (horizontal.sqrMagnitude < 0.000001f) horizontal = new Vector2(0f, 1f);
                float minimumHorizontal = y / Mathf.Tan(Mathf.Clamp(LookMaxElevationDegrees, 5f, 89.9f) * Mathf.Deg2Rad);
                if (horizontal.magnitude < minimumHorizontal) {
                    horizontal = horizontal.normalized * minimumHorizontal;
                    direction = new Vector3(horizontal.x, y, horizontal.y);
                }
            }

            return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.up;
        }

        private static Color EvaluateDayLightColor(RoDayNightCycleSettings settings, float dayProgress) {
            float transition = Mathf.Clamp(settings.NightTransitionFraction, 0.001f, 0.49f);
            if (dayProgress < transition) {
                float blend = Mathf.SmoothStep(0f, 1f, dayProgress / transition);
                return Color.Lerp(settings.dawnLightColor, settings.dayLightColor, blend);
            }

            if (dayProgress > 1f - transition) {
                float blend = Mathf.SmoothStep(0f, 1f, (dayProgress - (1f - transition)) / transition);
                return Color.Lerp(settings.dayLightColor, settings.duskLightColor, blend);
            }

            return settings.dayLightColor;
        }

        private static Color EvaluateDayAmbientColor(RoDayNightCycleSettings settings, float dayProgress) {
            float transition = Mathf.Clamp(settings.NightTransitionFraction, 0.001f, 0.49f);
            if (dayProgress < transition) {
                float blend = Mathf.SmoothStep(0f, 1f, dayProgress / transition);
                return Color.Lerp(settings.dawnAmbientColor, settings.dayAmbientColor, blend);
            }

            if (dayProgress > 1f - transition) {
                float blend = Mathf.SmoothStep(0f, 1f, (dayProgress - (1f - transition)) / transition);
                return Color.Lerp(settings.dayAmbientColor, settings.duskAmbientColor, blend);
            }

            return settings.dayAmbientColor;
        }
    }
}
