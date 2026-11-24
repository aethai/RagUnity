using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.Effects {
    [Serializable]
    public sealed class RoAmbientParticleSettings : ISerializationCallbackReceiver {
        private const int CurrentSerializedVersion = 3;

        public const bool DefaultEnabled = true;
        public const float DefaultLeavesPerTreePerMinute = 48.485f;
        public const int DefaultMaxLeafParticles = 410;
        public const float DefaultEmitterActivationRadius = 55.53f;
        public const float DefaultLeafLifetimeSeconds = 2.652f;
        public const float DefaultLeafFallSpeed = 0.8f;
        public const float DefaultWindInfluence = 1.182f;

        [SerializeField, HideInInspector] private int serializedVersion;

        public bool enabled = DefaultEnabled;
        [Min(0f)] public float leavesPerTreePerMinute = DefaultLeavesPerTreePerMinute;
        [Min(0)] public int maxLeafParticles = DefaultMaxLeafParticles;
        [Min(0f)] public float emitterActivationRadius = DefaultEmitterActivationRadius;
        [Min(0.01f)] public float leafLifetimeSeconds = DefaultLeafLifetimeSeconds;
        [Min(0f)] public float leafFallSpeed = DefaultLeafFallSpeed;
        [Min(0f)] public float windInfluence = DefaultWindInfluence;

        public float LeavesPerTreePerMinute => Mathf.Max(0f, leavesPerTreePerMinute);
        public int MaxLeafParticles => Mathf.Max(1, maxLeafParticles);
        public float EmitterActivationRadius => Mathf.Max(0f, emitterActivationRadius);
        public float LeafLifetimeSeconds => Mathf.Max(0.01f, leafLifetimeSeconds);
        public float LeafFallSpeed => Mathf.Max(0f, leafFallSpeed);
        public float WindInfluence => Mathf.Max(0f, windInfluence);
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
            if (serializedVersion < 2) {
                if (leavesPerTreePerMinute <= 30f || Approximately(leavesPerTreePerMinute, 4f)) {
                    leavesPerTreePerMinute = DefaultLeavesPerTreePerMinute;
                }

                if (maxLeafParticles == 200) {
                    maxLeafParticles = DefaultMaxLeafParticles;
                }
            }

            if (serializedVersion < 3) {
                if (leavesPerTreePerMinute <= 30f || Approximately(leavesPerTreePerMinute, 90f)) {
                    leavesPerTreePerMinute = DefaultLeavesPerTreePerMinute;
                }

                if (maxLeafParticles == 400) {
                    maxLeafParticles = DefaultMaxLeafParticles;
                }

                if (Approximately(emitterActivationRadius, 40f)) {
                    emitterActivationRadius = DefaultEmitterActivationRadius;
                }

                if (Approximately(leafLifetimeSeconds, 6f)) {
                    leafLifetimeSeconds = DefaultLeafLifetimeSeconds;
                }

                if (Approximately(windInfluence, 1f)) {
                    windInfluence = DefaultWindInfluence;
                }
            }

            leavesPerTreePerMinute = Mathf.Max(0f, leavesPerTreePerMinute);
            maxLeafParticles = Mathf.Max(1, maxLeafParticles);
            emitterActivationRadius = Mathf.Max(0f, emitterActivationRadius);
            leafLifetimeSeconds = Mathf.Max(0.01f, leafLifetimeSeconds);
            leafFallSpeed = Mathf.Max(0f, leafFallSpeed);
            windInfluence = Mathf.Max(0f, windInfluence);
            serializedVersion = CurrentSerializedVersion;
        }

        public RoWeatherParticleSpec BuildLeavesSpec() {
            EnsureCurrentDefaults();
            RoWeatherParticleSpec spec = RoWeatherParticleSpec.Leaves();
            spec.MaxParticles = MaxLeafParticles;
            spec.StartLifetime = LeafLifetimeSeconds;
            spec.FallVelocity = -LeafFallSpeed;
            spec.RateOverTime = 0f;
            spec.ManualEmission = true;
            return spec;
        }

        public static int ResolveSignature(RoAmbientParticleSettings settings) {
            settings ??= new RoAmbientParticleSettings();
            unchecked {
                int hash = 17;
                hash = hash * 31 + (settings.enabled ? 1 : 0);
                hash = hash * 31 + Quantize(settings.leavesPerTreePerMinute);
                hash = hash * 31 + settings.maxLeafParticles;
                hash = hash * 31 + Quantize(settings.emitterActivationRadius);
                hash = hash * 31 + Quantize(settings.leafLifetimeSeconds);
                hash = hash * 31 + Quantize(settings.leafFallSpeed);
                hash = hash * 31 + Quantize(settings.windInfluence);
                return hash;
            }
        }

        private static int Quantize(float value) => Mathf.RoundToInt(value * 10000f);

        private static bool Approximately(float a, float b) =>
            Mathf.Abs(a - b) <= 0.0001f;
    }

    public readonly struct RoAmbientParticleEmitter {
        public RoAmbientParticleEmitter(Vector3 anchor, float radius, string textureName = null, string modelFile = null, int surfaceCount = 1) {
            Anchor = anchor;
            Radius = Mathf.Max(0.05f, radius);
            TextureName = textureName ?? string.Empty;
            ModelFile = modelFile ?? string.Empty;
            SurfaceCount = Mathf.Max(1, surfaceCount);
        }

        public Vector3 Anchor { get; }
        public float Radius { get; }
        public string TextureName { get; }
        public string ModelFile { get; }
        public int SurfaceCount { get; }
    }

    public readonly struct RoAmbientParticleEmitterSelection {
        public RoAmbientParticleEmitterSelection(int index, float sqrDistance) {
            Index = index;
            SqrDistance = sqrDistance;
        }

        public int Index { get; }
        public float SqrDistance { get; }
    }

    public static class RoAmbientParticleMath {
        public const int DefaultMaxActiveEmitters = 8;
        public const float GustPhaseScale = 0.05f;
        public const float GustFrequencyA = 0.31f;
        public const float GustFrequencyB = 0.53f;
        public const float GustWeightA = 0.7f;
        public const float GustWeightB = 0.3f;
        public const float GustPhaseMultiplierB = 1.7f;
        public const float GustAmplitude = 0.5f;
        public const float LeafHorizontalDragPerSecond = 1.2f;
        public const float MaxLeafHorizontalSpeed = 4f;

        public static bool ResolveEnabled(RoAmbientParticleSettings settings, bool skylessMap) =>
            settings != null && settings.enabled && !skylessMap;

        public static float ComputeCpuGustFactor(Vector3 worldPosition, Vector2 windDirection, float timeSeconds, float gustStrength) {
            Vector2 direction = windDirection.sqrMagnitude > 0.0001f
                ? windDirection.normalized
                : Vector2.right;
            float phase = Vector2.Dot(new Vector2(worldPosition.x, worldPosition.z), direction) * GustPhaseScale;
            float gust =
                GustWeightA * Mathf.Sin(timeSeconds * GustFrequencyA + phase) +
                GustWeightB * Mathf.Sin(timeSeconds * GustFrequencyB + phase * GustPhaseMultiplierB);
            return Mathf.Max(0f, 1f + Mathf.Clamp01(gustStrength) * GustAmplitude * gust);
        }

        public static float ClampEmissionBacklog(float accumulator, int maxBacklog) =>
            Mathf.Clamp(accumulator, 0f, Mathf.Max(0, maxBacklog));

        public static Vector3 ApplyLeafHorizontalDragAndClamp(
            Vector3 velocity,
            float deltaSeconds,
            float dragPerSecond = LeafHorizontalDragPerSecond,
            float maxHorizontalSpeed = MaxLeafHorizontalSpeed
        ) {
            if (deltaSeconds > 0f && dragPerSecond > 0f) {
                float damping = Mathf.Exp(-dragPerSecond * deltaSeconds);
                velocity.x *= damping;
                velocity.z *= damping;
            }

            float maxSpeed = Mathf.Max(0f, maxHorizontalSpeed);
            if (maxSpeed <= 0f) {
                velocity.x = 0f;
                velocity.z = 0f;
                return velocity;
            }

            Vector2 horizontal = new Vector2(velocity.x, velocity.z);
            float maxSpeedSq = maxSpeed * maxSpeed;
            if (horizontal.sqrMagnitude > maxSpeedSq) {
                horizontal = horizontal.normalized * maxSpeed;
                velocity.x = horizontal.x;
                velocity.z = horizontal.y;
            }

            return velocity;
        }

        public static void SelectNearestEmitters(
            IReadOnlyList<RoAmbientParticleEmitter> emitters,
            Vector3 origin,
            float activationRadius,
            int maxCount,
            List<RoAmbientParticleEmitterSelection> results
        ) {
            results?.Clear();
            if (emitters == null || results == null || maxCount <= 0 || activationRadius <= 0f) {
                return;
            }

            float radiusSq = activationRadius * activationRadius;
            for (int i = 0; i < emitters.Count; i++) {
                Vector3 delta = emitters[i].Anchor - origin;
                delta.y = 0f;
                float sqrDistance = delta.sqrMagnitude;
                if (sqrDistance > radiusSq) {
                    continue;
                }

                InsertSorted(results, new RoAmbientParticleEmitterSelection(i, sqrDistance), maxCount);
            }
        }

        private static void InsertSorted(List<RoAmbientParticleEmitterSelection> results, RoAmbientParticleEmitterSelection selection, int maxCount) {
            int index = results.Count;
            for (int i = 0; i < results.Count; i++) {
                if (selection.SqrDistance < results[i].SqrDistance) {
                    index = i;
                    break;
                }
            }

            if (index >= maxCount) {
                return;
            }

            results.Insert(index, selection);
            if (results.Count > maxCount) {
                results.RemoveAt(results.Count - 1);
            }
        }
    }
}
