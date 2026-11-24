using UnityEngine;

namespace Ragnarok.Sprites {
    /// <summary>
    /// Single source of truth for how an actor sprite is lit/shadowed, resolved from the active shadow
    /// render mode. Centralizes decisions that used to be hardcoded in several places
    /// (RoRuntimeMapController, RoOnlineActorPresenter, RoActSpriteAnimator, RoSpriteMaterialCache).
    /// </summary>
    public readonly struct RoActorLightingPolicy {
        public RoActorLightingPolicy(
            bool sampleBakedShadows,
            bool receiveRealtimeShadows,
            bool useAdditionalLights,
            float realtimeShadowStrength
        ) {
            SampleBakedShadows = sampleBakedShadows;
            ReceiveRealtimeShadows = receiveRealtimeShadows;
            UseAdditionalLights = useAdditionalLights;
            RealtimeShadowStrength = realtimeShadowStrength;
        }

        public bool SampleBakedShadows { get; }
        public bool ReceiveRealtimeShadows { get; }
        public bool UseAdditionalLights { get; }
        public float RealtimeShadowStrength { get; }

        public static RoActorLightingPolicy Resolve(RoShadowRenderMode mode, RoEnhancedShadowSettings settings) {
            float strength = Mathf.Clamp01(
                settings != null
                    ? settings.actorRealtimeShadowStrength
                    : RoEnhancedShadowSettings.DefaultActorRealtimeShadowStrength);

            // #2: in Enhanced the actor no longer samples legacy baked GND.shadowMap occlusion (phantom
            //     darkening). Original keeps it for RO parity.
            // #1: in Enhanced the actor receives the URP main-light realtime shadow per-fragment.
            bool sampleBakedShadows = mode != RoShadowRenderMode.Enhanced;
            bool receiveRealtimeShadows = mode == RoShadowRenderMode.Enhanced;
            bool useAdditionalLights = false;

            return new RoActorLightingPolicy(
                sampleBakedShadows,
                receiveRealtimeShadows,
                useAdditionalLights,
                strength);
        }
    }
}
