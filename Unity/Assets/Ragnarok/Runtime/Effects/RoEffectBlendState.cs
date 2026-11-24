using UnityEngine.Rendering;

namespace Ragnarok.Effects {
    public readonly struct RoEffectBlendState {
        public readonly ulong SourceAlpha;
        public readonly ulong DestinationAlpha;
        public readonly BlendMode SourceBlend;
        public readonly BlendMode DestinationBlend;
        public readonly bool MultiplyAlpha;

        private RoEffectBlendState(
            ulong sourceAlpha,
            ulong destinationAlpha,
            BlendMode sourceBlend,
            BlendMode destinationBlend,
            bool multiplyAlpha
        ) {
            SourceAlpha = sourceAlpha;
            DestinationAlpha = destinationAlpha;
            SourceBlend = sourceBlend;
            DestinationBlend = destinationBlend;
            MultiplyAlpha = multiplyAlpha;
        }

        public static RoEffectBlendState FromD3dBlend(ulong sourceAlpha, ulong destinationAlpha, bool preserveD3dBlend = false) {
            ulong normalizedSource = sourceAlpha;
            ulong normalizedDestination = destinationAlpha;
            bool multiplyAlpha = false;

            // Some STR files use One/Zero while still relying on texture alpha.
            if (!preserveD3dBlend && normalizedSource == 2 && normalizedDestination == 1) {
                normalizedSource = 5;
                normalizedDestination = 6;
            }

            // Doddler handles SrcAlpha/DestAlpha STR layers as additive with alpha-premultiplied color.
            if (!preserveD3dBlend && normalizedSource == 5 && normalizedDestination == 7) {
                normalizedSource = 2;
                normalizedDestination = 2;
                multiplyAlpha = true;
            }

            return new RoEffectBlendState(
                sourceAlpha,
                destinationAlpha,
                ToUnityBlendMode(normalizedSource),
                ToUnityBlendMode(normalizedDestination),
                multiplyAlpha
            );
        }

        public static BlendMode ToUnityBlendMode(ulong d3dBlend) {
            return d3dBlend switch {
                1 => BlendMode.Zero,
                2 => BlendMode.One,
                3 => BlendMode.SrcColor,
                4 => BlendMode.OneMinusSrcColor,
                5 => BlendMode.SrcAlpha,
                6 => BlendMode.OneMinusSrcAlpha,
                7 => BlendMode.DstAlpha,
                8 => BlendMode.OneMinusDstAlpha,
                9 => BlendMode.DstColor,
                10 => BlendMode.OneMinusDstColor,
                11 => BlendMode.SrcAlphaSaturate,
                14 => BlendMode.One,
                15 => BlendMode.OneMinusSrcAlpha,
                _ => BlendMode.One
            };
        }
    }
}
