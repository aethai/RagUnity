using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Diagnostics {
    // Temporary diagnostic probe for the "no realtime shadows in player builds" investigation.
    // Enabled only with the -roShadowProbe command line argument (or RO_SHADOW_PROBE=1).
    // Reads back the URP main light shadow caster pass state via reflection (the Ragnarok
    // runtime assembly does not reference URP directly) so we can tell whether the player
    // renders a real shadowmap or URP's 1x1 "empty" fallback.
    internal sealed class RoShadowmapProbe : MonoBehaviour {
        private const int LogEveryFrames = 90;
        private int frameCounter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() {
            if (!IsEnabled()) {
                return;
            }

            var probeObject = new GameObject("RoShadowmapProbe");
            DontDestroyOnLoad(probeObject);
            probeObject.AddComponent<RoShadowmapProbe>();
            Debug.Log("[RoShadowmapProbe] enabled");
        }

        private static bool IsEnabled() {
            try {
                string[] args = Environment.GetCommandLineArgs();
                if (args != null) {
                    for (int i = 0; i < args.Length; i++) {
                        if (string.Equals(args[i], "-roShadowProbe", StringComparison.OrdinalIgnoreCase)) {
                            return true;
                        }
                    }
                }

                string env = Environment.GetEnvironmentVariable("RO_SHADOW_PROBE");
                return string.Equals(env, "1", StringComparison.OrdinalIgnoreCase);
            } catch {
                return false;
            }
        }

        private void LateUpdate() {
            frameCounter++;
            if (frameCounter % LogEveryFrames != 0) {
                return;
            }

            var builder = new StringBuilder(1024);
            builder.Append("[RoShadowmapProbe] frame=").Append(Time.frameCount);
            builder.Append(" kwShadows=").Append(Shader.IsKeywordEnabled("_MAIN_LIGHT_SHADOWS"));
            builder.Append(" kwCascade=").Append(Shader.IsKeywordEnabled("_MAIN_LIGHT_SHADOWS_CASCADE"));
            builder.Append(" kwSoft=").Append(Shader.IsKeywordEnabled("_SHADOWS_SOFT"));
            builder.Append(" mainLightPos=").Append(Shader.GetGlobalVector("_MainLightPosition"));
            builder.Append(" mainLightColor=").Append(Shader.GetGlobalVector("_MainLightColor"));
            builder.Append(" shadowParams=").Append(Shader.GetGlobalVector("_MainLightShadowParams"));

            Texture shadowTexture = Shader.GetGlobalTexture("_MainLightShadowmapTexture");
            builder.Append(" shadowTex=");
            if (shadowTexture != null) {
                builder.Append(shadowTexture.width).Append('x').Append(shadowTexture.height);
            } else {
                builder.Append("null");
            }

            AppendPassState(builder);

            Light sun = RenderSettings.sun;
            builder.Append(" sun=");
            if (sun != null) {
                builder.Append(sun.name)
                    .Append(" enabled=").Append(sun.enabled && sun.gameObject.activeInHierarchy)
                    .Append(" shadows=").Append(sun.shadows)
                    .Append(" strength=").Append(sun.shadowStrength.ToString("0.###"));
            } else {
                builder.Append("null");
            }

            Debug.Log(builder.ToString());
        }

        private static void AppendPassState(StringBuilder builder) {
            try {
                UnityEngine.Camera camera = UnityEngine.Camera.main;
                if (camera == null) {
                    builder.Append(" cam=null");
                    return;
                }

                builder.Append(" camFar=").Append(camera.farClipPlane.ToString("0.###"));

                object renderer = ResolveScriptableRenderer(camera);
                if (renderer == null) {
                    builder.Append(" renderer=null");
                    return;
                }

                FieldInfo passField = renderer.GetType().GetField(
                    "m_MainLightShadowCasterPass",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                object pass = passField != null ? passField.GetValue(renderer) : null;
                if (pass == null) {
                    builder.Append(" pass=null");
                    return;
                }

                Type passType = pass.GetType();
                builder.Append(" emptyShadowmap=").Append(ReadField(pass, passType, "m_CreateEmptyShadowmap"));
                builder.Append(" emptyKeyword=").Append(ReadField(pass, passType, "m_SetKeywordForEmptyShadowmap"));
                builder.Append(" rtSize=").Append(ReadField(pass, passType, "m_RenderTargetWidth"))
                    .Append('x').Append(ReadField(pass, passType, "m_RenderTargetHeight"));
            } catch (Exception exception) {
                builder.Append(" probeError=").Append(exception.GetType().Name).Append(':').Append(exception.Message);
            }
        }

        private static object ResolveScriptableRenderer(UnityEngine.Camera camera) {
            // Preferred: UniversalAdditionalCameraData.scriptableRenderer (public property).
            Component[] components = camera.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++) {
                Component component = components[i];
                if (component == null) {
                    continue;
                }

                Type type = component.GetType();
                if (type.FullName != "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData") {
                    continue;
                }

                PropertyInfo rendererProperty = type.GetProperty(
                    "scriptableRenderer",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object result = rendererProperty != null ? rendererProperty.GetValue(component) : null;
                if (result != null) {
                    return result;
                }
            }

            // Fallback: active pipeline asset GetRenderer(0).
            RenderPipelineAsset asset = QualitySettings.renderPipeline != null
                ? QualitySettings.renderPipeline
                : GraphicsSettings.defaultRenderPipeline;
            if (asset == null) {
                return null;
            }

            MethodInfo getRenderer = asset.GetType().GetMethod(
                "GetRenderer",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getRenderer == null) {
                return null;
            }

            try {
                return getRenderer.Invoke(asset, new object[] { 0 });
            } catch (TargetInvocationException) {
                return null;
            }
        }

        private static object ReadField(object target, Type type, string fieldName) {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(target) : "n/a";
        }
    }
}
