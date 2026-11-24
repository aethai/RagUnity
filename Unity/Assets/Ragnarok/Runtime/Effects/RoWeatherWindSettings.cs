using System;
using UnityEngine;

namespace Ragnarok.Effects {
    [Serializable]
    public sealed class RoWeatherWindSettings : ISerializationCallbackReceiver {
        private const int CurrentSerializedVersion = 1;

        private static readonly int WeatherWindDirectionId = Shader.PropertyToID("_RoWeatherWindDirection");
        private static readonly int WeatherWindStrengthId = Shader.PropertyToID("_RoWeatherWindStrength");

        public static readonly Vector2 DefaultDirection = new Vector2(-2.1f, -0.55f).normalized;
        public const float DefaultStrength = 2.170829f;

        [SerializeField, HideInInspector] private int serializedVersion;

        public Vector2 direction = DefaultDirection;
        [Min(0f)] public float strength = DefaultStrength;

        public Vector2 EffectiveDirection =>
            direction.sqrMagnitude > 0.0001f ? direction.normalized : DefaultDirection;
        public float Strength => Mathf.Max(0f, strength);
        public Vector2 Velocity => EffectiveDirection * Strength;

        public void OnBeforeSerialize() {
            if (serializedVersion <= 0) {
                serializedVersion = CurrentSerializedVersion;
            }
        }

        public void OnAfterDeserialize() {
            EnsureCurrentDefaults();
        }

        public void EnsureCurrentDefaults() {
            if (direction.sqrMagnitude <= 0.0001f) {
                direction = DefaultDirection;
            }

            strength = Mathf.Max(0f, strength);
            serializedVersion = CurrentSerializedVersion;
        }

        public void ApplyGlobals() {
            EnsureCurrentDefaults();
            Vector2 resolvedDirection = EffectiveDirection;
            Shader.SetGlobalVector(WeatherWindDirectionId, new Vector4(resolvedDirection.x, 0f, resolvedDirection.y, 0f));
            Shader.SetGlobalFloat(WeatherWindStrengthId, Strength);
        }

        public static void ApplyGlobals(RoWeatherWindSettings settings) {
            if (settings == null) {
                ResetGlobals();
                return;
            }

            settings.ApplyGlobals();
        }

        public static void ResetGlobals() {
            Vector2 direction = DefaultDirection;
            Shader.SetGlobalVector(WeatherWindDirectionId, new Vector4(direction.x, 0f, direction.y, 0f));
            Shader.SetGlobalFloat(WeatherWindStrengthId, 0f);
        }
    }
}
