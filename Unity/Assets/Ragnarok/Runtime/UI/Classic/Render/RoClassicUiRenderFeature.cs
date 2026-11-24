using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// Draws the classic UI at <see cref="RenderPassEvent.AfterRendering"/>: after every post-processing pass
    /// (FXAA/SMAA never touch UI pixels) and before URP renders the screen-space overlay UGUI, so legacy UGUI windows
    /// stay on top while they coexist with classic ones.
    /// </summary>
    public sealed class RoClassicUiRenderFeature : ScriptableRendererFeature {
        [SerializeField] private Shader shader;
        private ClassicUiPass pass;

        public override void Create() {
            if (shader != null) RoClassicUiRenderer.DefaultShader = shader;
            pass = new ClassicUiPass { renderPassEvent = RenderPassEvent.AfterRendering };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (RoClassicUiRenderer.ForCamera(renderingData.cameraData.camera) != null) renderer.EnqueuePass(pass);
        }

        private sealed class ClassicUiPass : ScriptableRenderPass {
            private sealed class PassData {
                public RoClassicUiRenderer Renderer;
                public TextureHandle Target;
                public int Width;
                public int Height;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                RoClassicUiRenderer ui = RoClassicUiRenderer.ForCamera(cameraData.camera);
                if (ui == null || !resources.activeColorTexture.IsValid()) return;

                using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("RO Classic UI", out PassData data)) {
                    data.Renderer = ui;
                    data.Target = resources.activeColorTexture;
                    data.Width = cameraData.camera.pixelWidth;
                    data.Height = cameraData.camera.pixelHeight;
                    builder.UseTexture(data.Target, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData d, UnsafeGraphContext context) => {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        d.Renderer.RecordDraw(cmd, d.Target, d.Width, d.Height, true);
                    });
                }
            }
        }
    }
}
