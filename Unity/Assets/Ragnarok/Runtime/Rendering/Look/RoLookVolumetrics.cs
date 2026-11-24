using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Ragnarok.Rendering {
    /// <summary>
    /// Volumetric air of the HD-2D look: height fog lit by the nearby map lamps and fires, marched at half resolution
    /// before post-processing and composited over the scene (Hidden/RO Look Volumetric). The pass is enqueued on the
    /// main camera from beginCameraRendering, so no renderer asset change is needed. Off at density 0.
    /// </summary>
    public sealed class RoLookVolumetrics : MonoBehaviour {
        const int MaxLights = 16;
        static readonly int LightPosId = Shader.PropertyToID("_RoVolLightPos");
        static readonly int LightColorId = Shader.PropertyToID("_RoVolLightColor");
        static readonly int LightCountId = Shader.PropertyToID("_RoVolLightCount");
        static readonly int FogId = Shader.PropertyToID("_RoVolFog");
        static readonly int AmbientId = Shader.PropertyToID("_RoVolAmbient");
        static readonly int ParamsId = Shader.PropertyToID("_RoVolParams");
        static readonly int VolumeTexId = Shader.PropertyToID("_RoVolumeTex");

        // density, height falloff, base offset below the camera target, max distance
        public Vector4 Fog = new Vector4(0f, 0.25f, -1f, 140f);
        // ambient in-scatter tint (times the scene ambient) and the light scattering scale
        public Vector4 Ambient = new Vector4(0.55f, 0.5f, 0.9f, 1.6f);
        // steps, anisotropy
        public Vector4 Params = new Vector4(24f, 0.35f, 0f, 0f);
        // how many of the lamps / fires nearest to the view cast soft shadows (point light cube shadows; 0 = none)
        public int ShadowedLights;
        public float ShadowStrength = 0.85f;
        readonly Dictionary<Light, LightShadows> originalShadows = new Dictionary<Light, LightShadows>();

        Material material;
        VolumetricPass pass;
        readonly Vector4[] lightPos = new Vector4[MaxLights];
        readonly Vector4[] lightColor = new Vector4[MaxLights];
        readonly List<Light> candidates = new List<Light>(64);
        float nextLightScan;
        Ragnarok.Camera.RoCameraController cameraController;

        public bool Active => Fog.x > 0f && material != null;

        void OnEnable() {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable() {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        void OnDestroy() {
            if (material != null) Destroy(material);
        }

        void OnBeginCamera(ScriptableRenderContext context, UnityEngine.Camera camera) {
            if (Fog.x <= 0f || camera != UnityEngine.Camera.main) return;
            if (material == null) {
                Shader shader = Shader.Find("Hidden/RO Look Volumetric");
                if (shader == null) return;
                material = new Material(shader) { name = "RO Look Volumetric" };
                pass = new VolumetricPass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            }

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;
            PushParameters(camera);
            data.scriptableRenderer.EnqueuePass(pass);
        }

        Vector3 ViewTarget(UnityEngine.Camera camera) {
            if (cameraController == null && Time.frameCount % 30 == 0) cameraController = FindFirstObjectByType<Ragnarok.Camera.RoCameraController>();
            float distance = cameraController != null ? cameraController.Distance : 70f;
            return camera.transform.position + camera.transform.forward * distance;
        }

        // the point lights of the map, nearest to the view's centre first
        void SortCandidates(Vector3 target) {
            if (Time.unscaledTime >= nextLightScan) {
                nextLightScan = Time.unscaledTime + 1f;
                candidates.Clear();
                foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                    if (light.type == LightType.Point) candidates.Add(light);
                }
            }

            candidates.RemoveAll(light => light == null);
            candidates.Sort((a, b) => (a.transform.position - target).sqrMagnitude.CompareTo((b.transform.position - target).sqrMagnitude));
        }

        void LateUpdate() {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null || (ShadowedLights <= 0 && originalShadows.Count == 0)) return;
            SortCandidates(ViewTarget(camera));
            // the nearest burning lights get soft shadows (characters and rocks cast moving shadows from the torches);
            // the rest go back to what the map gave them
            int shadowed = 0;
            for (int i = 0; i < candidates.Count; i++) {
                Light light = candidates[i];
                bool wants = shadowed < ShadowedLights && light.enabled && light.intensity > 0.001f;
                if (wants) {
                    if (!originalShadows.ContainsKey(light)) originalShadows[light] = light.shadows;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = ShadowStrength;
                    // the torch's own stand sits around the flame: casters this close are left out, or the stand
                    // would put its whole surroundings in shadow
                    light.shadowNearPlane = 1.4f;
                    light.shadowBias = 0.05f;
                    light.shadowNormalBias = 0.4f;
                    shadowed++;
                } else if (originalShadows.TryGetValue(light, out LightShadows original)) {
                    light.shadows = original;
                    originalShadows.Remove(light);
                }
            }
        }

        void PushParameters(UnityEngine.Camera camera) {
            Vector3 target = ViewTarget(camera);
            SortCandidates(target);
            int count = 0;
            for (int i = 0; i < candidates.Count && count < MaxLights; i++) {
                Light light = candidates[i];
                if (!light.enabled || !light.gameObject.activeInHierarchy || light.intensity <= 0.001f) continue;
                Vector3 p = light.transform.position;
                lightPos[count] = new Vector4(p.x, p.y, p.z, light.range);
                Color c = light.color.linear * light.intensity;
                lightColor[count] = new Vector4(c.r, c.g, c.b, 0f);
                count++;
            }

            Color ambient = RenderSettings.ambientLight.linear;
            material.SetVectorArray(LightPosId, lightPos);
            material.SetVectorArray(LightColorId, lightColor);
            material.SetInt(LightCountId, count);
            material.SetVector(FogId, new Vector4(Fog.x, Fog.y, target.y + Fog.z, Fog.w));
            material.SetVector(AmbientId, new Vector4(ambient.r * Ambient.x, ambient.g * Ambient.y, ambient.b * Ambient.z, Ambient.w));
            material.SetVector(ParamsId, new Vector4(Mathf.Clamp(Params.x, 4f, 64f), Mathf.Clamp(Params.y, -0.9f, 0.9f), 0f, 0f));
        }

        sealed class VolumetricPass : ScriptableRenderPass {
            readonly Material material;

            sealed class MarchData { public Material Material; }
            sealed class CompositeData { public Material Material; public TextureHandle Source; public TextureHandle Volume; }

            public VolumetricPass(Material material) {
                this.material = material;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid()) return;

                TextureDesc colorDesc = renderGraph.GetTextureDesc(resources.activeColorTexture);
                TextureDesc volumeDesc = colorDesc;
                volumeDesc.name = "RO Look Volume";
                volumeDesc.width = Mathf.Max(1, colorDesc.width / 2);
                volumeDesc.height = Mathf.Max(1, colorDesc.height / 2);
                volumeDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                volumeDesc.depthBufferBits = DepthBits.None;
                volumeDesc.msaaSamples = MSAASamples.None;
                volumeDesc.filterMode = FilterMode.Bilinear;
                volumeDesc.clearBuffer = false;
                TextureHandle volume = renderGraph.CreateTexture(volumeDesc);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("RO Look Volumetric March", out MarchData data)) {
                    data.Material = material;
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.SetRenderAttachment(volume, 0);
                    builder.SetRenderFunc((MarchData d, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), d.Material, 0));
                }

                TextureDesc outDesc = colorDesc;
                outDesc.name = "RO Look Volumetric Color";
                outDesc.clearBuffer = false;
                TextureHandle output = renderGraph.CreateTexture(outDesc);
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("RO Look Volumetric Composite", out CompositeData data)) {
                    data.Material = material;
                    data.Source = resources.activeColorTexture;
                    data.Volume = volume;
                    builder.UseTexture(data.Source);
                    builder.UseTexture(data.Volume);
                    builder.SetRenderAttachment(output, 0);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((CompositeData d, RasterGraphContext context) => {
                        context.cmd.SetGlobalTexture(VolumeTexId, d.Volume);
                        Blitter.BlitTexture(context.cmd, d.Source, new Vector4(1f, 1f, 0f, 0f), d.Material, 1);
                    });
                }

                resources.cameraColor = output;
            }
        }
    }
}
