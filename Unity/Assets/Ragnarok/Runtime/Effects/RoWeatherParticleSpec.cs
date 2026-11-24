using UnityEngine;

namespace Ragnarok.Effects {
    public struct RoWeatherParticleSpec {
        public RoWeatherParticleType Type;
        public float Width;
        public float Height;
        public float Depth;
        public float RateOverTime;
        public float StartLifetime;
        public float StartSize;
        public Vector2 WindVelocity;
        public float FallVelocity;
        public int MaxParticles;
        public Color StartColor;
        public bool FollowCamera;
        public int RenderQueue;
        public bool ManualEmission;

        public float EffectiveWidth => Width > 0f ? Width : 32f;
        public float EffectiveHeight => Height > 0f ? Height : 16f;
        public float EffectiveDepth => Depth > 0f ? Depth : 32f;
        public float EffectiveRateOverTime => RateOverTime > 0f ? RateOverTime : 80f;
        public float EffectiveStartLifetime => StartLifetime > 0f ? StartLifetime : 2f;
        public float EffectiveStartSize => StartSize > 0f ? StartSize : 0.08f;
        public int EffectiveMaxParticles => MaxParticles > 0 ? MaxParticles : 256;
        public int EffectiveRenderQueue => RenderQueue > 0 ? RenderQueue : 3100;
        public Color EffectiveStartColor => StartColor.a > 0f ? StartColor : Color.white;

        public static RoWeatherParticleSpec Rain() {
            return new RoWeatherParticleSpec {
                Type = RoWeatherParticleType.Rain,
                Width = 36f,
                Height = 18f,
                Depth = 36f,
                RateOverTime = 900f,
                StartLifetime = 1.35f,
                StartSize = 0.16f,
                FallVelocity = -24f,
                WindVelocity = new Vector2(-2.1f, -0.55f),
                MaxParticles = 1500,
                StartColor = Color.white,
                FollowCamera = true,
                RenderQueue = 3100
            };
        }

        public static RoWeatherParticleSpec Snow() {
            return new RoWeatherParticleSpec {
                Type = RoWeatherParticleType.Snow,
                Width = 34f,
                Height = 12f,
                Depth = 34f,
                RateOverTime = 45f,
                StartLifetime = 7f,
                StartSize = 0.14f,
                FallVelocity = -1.2f,
                WindVelocity = new Vector2(0.35f, 0.15f),
                MaxParticles = 384,
                StartColor = new Color(1f, 1f, 1f, 0.9f),
                FollowCamera = true,
                RenderQueue = 3100
            };
        }

        public static RoWeatherParticleSpec Leaves() {
            return new RoWeatherParticleSpec {
                Type = RoWeatherParticleType.Leaves,
                Width = 1f,
                Height = 1f,
                Depth = 1f,
                RateOverTime = 0f,
                StartLifetime = RoAmbientParticleSettings.DefaultLeafLifetimeSeconds,
                StartSize = 0.16f,
                FallVelocity = -RoAmbientParticleSettings.DefaultLeafFallSpeed,
                WindVelocity = Vector2.zero,
                MaxParticles = RoAmbientParticleSettings.DefaultMaxLeafParticles,
                StartColor = new Color(1f, 1f, 1f, 0.95f),
                FollowCamera = false,
                RenderQueue = 3100,
                ManualEmission = true
            };
        }
    }
}
