using UnityEngine;

namespace Ragnarok.Maps {
    public static class RoWindSwayGlobals {
        public const float DefaultAmplitudeMultiplier = 1f;
        public const float DefaultFrequencyMultiplier = 1f;
        public const float DefaultBaseBreeze = 0.35f;
        public const float DefaultGustStrength = 0.5f;
        public const float DefaultEnvironmentScale = 1f;
        public const float SkylessEnvironmentScale = 0.15f;

        private static readonly int WindSwayGlobalParamsId = Shader.PropertyToID("_RoWindSwayGlobalParams");
        private static readonly int WindSwayEnvironmentScaleId = Shader.PropertyToID("_RoWindSwayEnvironmentScale");

        public static Vector4 CurrentParams { get; private set; } =
            new Vector4(DefaultAmplitudeMultiplier, DefaultFrequencyMultiplier, DefaultBaseBreeze, DefaultGustStrength);
        public static float CurrentEnvironmentScale { get; private set; } = DefaultEnvironmentScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetGlobals() {
            Apply(DefaultAmplitudeMultiplier, DefaultFrequencyMultiplier, DefaultBaseBreeze, DefaultGustStrength);
            ResetEnvironmentScale();
        }

        public static void Apply(float amplitudeMultiplier, float frequencyMultiplier, float baseBreeze) {
            Apply(amplitudeMultiplier, frequencyMultiplier, baseBreeze, DefaultGustStrength);
        }

        public static void Apply(float amplitudeMultiplier, float frequencyMultiplier, float baseBreeze, float gustStrength) {
            CurrentParams = new Vector4(
                Mathf.Max(0f, amplitudeMultiplier),
                Mathf.Max(0f, frequencyMultiplier),
                Mathf.Clamp01(baseBreeze),
                Mathf.Clamp01(gustStrength));
            Shader.SetGlobalVector(WindSwayGlobalParamsId, CurrentParams);
        }

        public static void ApplyEnvironmentScale(float scale) {
            CurrentEnvironmentScale = Mathf.Clamp01(scale);
            Shader.SetGlobalFloat(WindSwayEnvironmentScaleId, CurrentEnvironmentScale);
        }

        public static void ResetEnvironmentScale() {
            ApplyEnvironmentScale(DefaultEnvironmentScale);
        }
    }
}
