using System.Globalization;
using System.Text;
using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Source that currently drives an actor sprite's brightness/darkening. Used by the lighting debug
    /// overlay so an artist/engineer can immediately answer "why is this actor this bright?".
    /// </summary>
    public enum RoActorLightingSource {
        None,
        UnityMainLightShadowMap,
        UnityAdditionalLight,
        LegacyGndShadowMap,
        LegacyGroundLightmapAlpha,
        LegacyGroundMaterialLight,
        EnhancedPointLightTint,
        Ambient
    }

    /// <summary>
    /// Read-only breakdown of how an actor sprite is being lit, classified purely from the values the
    /// CPU side already holds (map-light multiply/add from <c>RoActorMapLightProbe</c>, shader policy
    /// flags from <c>RoActSpriteAnimator</c>). The realtime main-light shadow attenuation is sampled on
    /// the GPU and is not observable here, so <see cref="RealtimeShadowFactorKnown"/> stays false.
    /// </summary>
    public readonly struct RoActorLightingDebugState {
        public RoActorLightingDebugState(
            RoShadowRenderMode mode,
            Color mapLightMultiply,
            Color mapLightAdd,
            bool sampleBakedShadows,
            bool receivesRealtimeShadows,
            bool usesAdditionalLights,
            float realtimeShadowStrength,
            float mapLightLuminance,
            bool legacyBakedTintActive,
            RoActorLightingSource activeShadowSource,
            RoActorLightingSource activeLightingSource
        ) {
            Mode = mode;
            MapLightMultiply = mapLightMultiply;
            MapLightAdd = mapLightAdd;
            SampleBakedShadows = sampleBakedShadows;
            ReceivesRealtimeShadows = receivesRealtimeShadows;
            UsesAdditionalLights = usesAdditionalLights;
            RealtimeShadowStrength = realtimeShadowStrength;
            MapLightLuminance = mapLightLuminance;
            LegacyBakedTintActive = legacyBakedTintActive;
            ActiveShadowSource = activeShadowSource;
            ActiveLightingSource = activeLightingSource;
        }

        public RoShadowRenderMode Mode { get; }
        public Color MapLightMultiply { get; }
        public Color MapLightAdd { get; }
        public bool SampleBakedShadows { get; }
        public bool ReceivesRealtimeShadows { get; }
        public bool UsesAdditionalLights { get; }
        public float RealtimeShadowStrength { get; }

        /// <summary>Net luminance of the map-light multiply channel. 1 = no map darkening, &lt;1 = darkened, &gt;1 = brightened.</summary>
        public float MapLightLuminance { get; }

        /// <summary>
        /// True when the actor is being darkened by legacy baked <c>GND.shadowMap</c> data while in Enhanced
        /// mode (the Problem #2 alarm). In Original mode baked darkening is intended parity, so this stays false.
        /// </summary>
        public bool LegacyBakedTintActive { get; }

        /// <summary>The realtime main-light shadow attenuation is GPU-side and not observable on the CPU yet.</summary>
        public bool RealtimeShadowFactorKnown => false;

        public RoActorLightingSource ActiveShadowSource { get; }
        public RoActorLightingSource ActiveLightingSource { get; }
    }

    /// <summary>
    /// Pure classifier for <see cref="RoActorLightingDebugState"/>. No Unity object access, fully
    /// deterministic, so it is verifiable headless in EditMode tests.
    /// </summary>
    public static class RoActorLightingDebug {
        private const float TintEpsilon = 1e-3f;

        public static RoActorLightingDebugState Classify(
            RoShadowRenderMode mode,
            Color mapLightMultiply,
            Color mapLightAdd,
            bool sampleBakedShadows,
            bool receivesRealtimeShadows,
            bool usesAdditionalLights,
            float realtimeShadowStrength
        ) {
            bool darkened = IsBelowWhite(mapLightMultiply);
            bool brightened = IsAboveWhite(mapLightMultiply);
            bool hasAdditive = IsNonZero(mapLightAdd);

            bool legacyBakedTintActive = mode == RoShadowRenderMode.Enhanced && sampleBakedShadows && darkened;

            RoActorLightingSource shadowSource;
            if (mode == RoShadowRenderMode.Enhanced && receivesRealtimeShadows) {
                shadowSource = RoActorLightingSource.UnityMainLightShadowMap;
            } else if (darkened && sampleBakedShadows) {
                shadowSource = RoActorLightingSource.LegacyGndShadowMap;
            } else {
                shadowSource = RoActorLightingSource.None;
            }

            RoActorLightingSource lightingSource;
            if (usesAdditionalLights) {
                lightingSource = RoActorLightingSource.UnityAdditionalLight;
            } else if (brightened) {
                lightingSource = RoActorLightingSource.EnhancedPointLightTint;
            } else if (hasAdditive) {
                lightingSource = RoActorLightingSource.LegacyGroundMaterialLight;
            } else {
                lightingSource = RoActorLightingSource.Ambient;
            }

            return new RoActorLightingDebugState(
                mode,
                mapLightMultiply,
                mapLightAdd,
                sampleBakedShadows,
                receivesRealtimeShadows,
                usesAdditionalLights,
                realtimeShadowStrength,
                Luminance(mapLightMultiply),
                legacyBakedTintActive,
                shadowSource,
                lightingSource
            );
        }

        public static string Describe(RoActorLightingDebugState state) {
            var builder = new StringBuilder(256);
            builder.Append("Mode: ").Append(state.Mode).Append('\n');
            builder.Append("MapLight x: ").Append(FormatColor(state.MapLightMultiply))
                .Append("  lum ").Append(state.MapLightLuminance.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("MapLight +: ").Append(FormatColor(state.MapLightAdd)).Append('\n');
            builder.Append("Realtime shadows: ").Append(state.ReceivesRealtimeShadows ? "ON" : "OFF")
                .Append(" (strength ").Append(state.RealtimeShadowStrength.ToString("0.##", CultureInfo.InvariantCulture)).Append(")\n");
            builder.Append("Realtime factor: ").Append(state.RealtimeShadowFactorKnown ? "n/a" : "GPU-only (not sampled)").Append('\n');
            builder.Append("Additional lights: ").Append(state.UsesAdditionalLights ? "ON" : "OFF").Append('\n');
            builder.Append("Baked sampling: ").Append(state.SampleBakedShadows ? "ON" : "OFF").Append('\n');
            builder.Append("Shadow source: ").Append(state.ActiveShadowSource).Append('\n');
            builder.Append("Lighting source: ").Append(state.ActiveLightingSource).Append('\n');
            builder.Append("Legacy baked tint: ").Append(state.LegacyBakedTintActive ? "ACTIVE (!)" : "none");
            return builder.ToString();
        }

        private static bool IsBelowWhite(Color color) {
            return color.r < 1f - TintEpsilon || color.g < 1f - TintEpsilon || color.b < 1f - TintEpsilon;
        }

        private static bool IsAboveWhite(Color color) {
            return color.r > 1f + TintEpsilon || color.g > 1f + TintEpsilon || color.b > 1f + TintEpsilon;
        }

        private static bool IsNonZero(Color color) {
            return color.r > TintEpsilon || color.g > TintEpsilon || color.b > TintEpsilon;
        }

        private static float Luminance(Color color) {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }

        private static string FormatColor(Color color) {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.##},{1:0.##},{2:0.##})",
                color.r,
                color.g,
                color.b);
        }
    }
}
