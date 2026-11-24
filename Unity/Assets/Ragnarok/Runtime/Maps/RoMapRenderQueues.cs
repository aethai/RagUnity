using UnityEngine.Rendering;

namespace Ragnarok.Maps {
    public static class RoMapRenderQueues {
        public const int Ground = (int) RenderQueue.AlphaTest;
        public const int ModelOpaque = (int) RenderQueue.Geometry;
        public const int ModelCutout = (int) RenderQueue.AlphaTest;
        public const int ModelTransparent = (int) RenderQueue.Transparent;
        public const int Water = (int) RenderQueue.Transparent;
        public const int SkyClouds = (int) RenderQueue.Transparent;
        public const int DebugOverlay = (int) RenderQueue.Transparent;
    }
}
