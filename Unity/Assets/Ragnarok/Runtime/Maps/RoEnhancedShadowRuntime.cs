using System;
using System.Collections.Generic;
using System.Reflection;
using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ragnarok.Maps {
    public sealed class RoEnhancedShadowRuntimeState : IDisposable {
        private readonly Light directionalLight;
        private readonly bool createdDirectionalLight;
        private readonly LightSnapshot lightSnapshot;
        private readonly Light previousSun;
        private readonly AmbientMode previousAmbientMode;
        private readonly Color previousAmbientLight;
        private readonly float previousShadowDistance;
        private readonly ShadowResolution previousShadowResolution;
        private readonly PipelineMemberSnapshot[] pipelineSnapshots;
        private bool disposed;

        internal RoEnhancedShadowRuntimeState(
            Light directionalLight,
            bool createdDirectionalLight,
            LightSnapshot lightSnapshot,
            Light previousSun,
            AmbientMode previousAmbientMode,
            Color previousAmbientLight,
            float previousShadowDistance,
            ShadowResolution previousShadowResolution,
            PipelineMemberSnapshot[] pipelineSnapshots
        ) {
            this.directionalLight = directionalLight;
            this.createdDirectionalLight = createdDirectionalLight;
            this.lightSnapshot = lightSnapshot;
            this.previousSun = previousSun;
            this.previousAmbientMode = previousAmbientMode;
            this.previousAmbientLight = previousAmbientLight;
            this.previousShadowDistance = previousShadowDistance;
            this.previousShadowResolution = previousShadowResolution;
            this.pipelineSnapshots = pipelineSnapshots ?? Array.Empty<PipelineMemberSnapshot>();
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            if (createdDirectionalLight) {
                DestroyRuntimeObject(directionalLight != null ? directionalLight.gameObject : null);
            } else {
                lightSnapshot.Restore(directionalLight);
            }

            RenderSettings.sun = previousSun;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;
            QualitySettings.shadowDistance = previousShadowDistance;
            QualitySettings.shadowResolution = previousShadowResolution;

            for (int i = 0; i < pipelineSnapshots.Length; i++) {
                pipelineSnapshots[i].Restore();
            }
        }

        public bool EnsureActive() {
            if (disposed || directionalLight == null) {
                return false;
            }

            GameObject lightObject = directionalLight.gameObject;
            if (lightObject != null && !lightObject.activeSelf) {
                lightObject.SetActive(true);
            }

            directionalLight.enabled = true;
            directionalLight.type = LightType.Directional;
            if (directionalLight.shadows == LightShadows.None) {
                directionalLight.shadows = LightShadows.Soft;
            }

            RenderSettings.sun = directionalLight;
            return true;
        }

        private static void DestroyRuntimeObject(Object obj) {
            if (obj == null) {
                return;
            }

            if (Application.isPlaying) {
                Object.Destroy(obj);
            } else {
                Object.DestroyImmediate(obj);
            }
        }
    }

    public static class RoEnhancedShadowRuntime {
        private const float GndTileWorldSize = 2f;
        private const float ShadowCascadeBorder = 0f;
        private const int AdditionalLightsPerPixelMode = 1;
        private const float RequiredReceiverShadowPadding = 32f;
        private const float MultiCascadeShadowDistanceThreshold = 180f;
        private const int LongDistanceShadowCascadeCount = 2;

        public static RoEnhancedShadowRuntimeState Apply(
            GND.Mesh ground,
            RSW.LightInfo lightInfo,
            RoEnhancedShadowSettings settings,
            Object logContext = null
        ) {
            return Apply(ground, lightInfo, settings, 0f, logContext);
        }

        public static RoEnhancedShadowRuntimeState Apply(
            GND.Mesh ground,
            RSW.LightInfo lightInfo,
            RoEnhancedShadowSettings settings,
            float requiredReceiverDistance,
            Object logContext = null
        ) {
            settings ??= new RoEnhancedShadowSettings();
            Light directionalLight = ResolveOrCreateRuntimeDirectionalLight(out bool createdDirectionalLight);
            var state = new RoEnhancedShadowRuntimeState(
                directionalLight,
                createdDirectionalLight,
                LightSnapshot.Capture(directionalLight),
                RenderSettings.sun,
                RenderSettings.ambientMode,
                RenderSettings.ambientLight,
                QualitySettings.shadowDistance,
                QualitySettings.shadowResolution,
                CapturePipelineSnapshots(ResolveActiveRenderPipelineAsset())
            );

            if (directionalLight == null) {
                Debug.LogWarning("Enhanced RO shadows could not find or create a directional light.", logContext);
                return state;
            }

            RoMapLightingState lighting = RoMapLighting.FromWorld(lightInfo);
            Vector3 lightDirection = lighting.Direction.sqrMagnitude > 0.000001f
                ? lighting.Direction.normalized
                : Vector3.up;
            directionalLight.transform.rotation = Quaternion.LookRotation(-lightDirection, Vector3.up);
            directionalLight.enabled = true;
            directionalLight.type = LightType.Directional;
            directionalLight.color = lighting.Diffuse;
            directionalLight.intensity = 1f;
            directionalLight.cullingMask = ~0;
            directionalLight.shadows = LightShadows.Soft;
            directionalLight.shadowStrength = Mathf.Clamp01(settings.terrainRealtimeShadowStrength);
            directionalLight.shadowBias = 0.03f;
            directionalLight.shadowNormalBias = 0.2f;
            directionalLight.shadowNearPlane = 0.1f;
            directionalLight.useViewFrustumForShadowCasterCull = false;

            RenderSettings.sun = directionalLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = lighting.Ambient;

            float shadowDistance = ResolveRealtimeShadowDistance(ground, settings, requiredReceiverDistance);
            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.shadowResolution = ShadowResolution.High;
            ApplyRenderPipelineShadowSettings(shadowDistance, settings);
            return state;
        }

        public static float ResolveRealtimeShadowDistance(GND.Mesh ground, RoEnhancedShadowSettings settings = null) {
            return ResolveRealtimeShadowDistance(ground, settings, 0f);
        }

        public static float ResolveRealtimeShadowDistance(
            GND.Mesh ground,
            RoEnhancedShadowSettings settings,
            float requiredReceiverDistance
        ) {
            settings ??= new RoEnhancedShadowSettings();
            float minimum = Mathf.Max(0f, settings.minimumShadowDistance);
            float maximum = Mathf.Max(minimum, settings.maximumShadowDistance);
            float requiredMinimum = ResolveRequiredReceiverShadowDistance(requiredReceiverDistance);
            minimum = Mathf.Max(minimum, requiredMinimum);
            maximum = Mathf.Max(maximum, minimum);
            if (ground == null || ground.width == 0 || ground.height == 0) {
                return minimum;
            }

            float mapSpan = Mathf.Max(ground.width * GndTileWorldSize, ground.height * GndTileWorldSize);
            return Mathf.Clamp(mapSpan * 1.25f, minimum, maximum);
        }

        public static int ResolveShadowCascadeCount(float shadowDistance, RoEnhancedShadowSettings settings = null) {
            settings ??= new RoEnhancedShadowSettings();
            int configuredCascadeCount = Mathf.Max(1, settings.shadowCascadeCount);
            return shadowDistance >= MultiCascadeShadowDistanceThreshold
                ? Mathf.Max(configuredCascadeCount, LongDistanceShadowCascadeCount)
                : configuredCascadeCount;
        }

        public static bool IsEnhanced(RoShadowRenderMode mode) => mode == RoShadowRenderMode.Enhanced;

        public static bool ShouldUseFakeActorShadow(RoShadowRenderMode mode, bool enabled) =>
            enabled && mode == RoShadowRenderMode.Original;

        private static float ResolveRequiredReceiverShadowDistance(float requiredReceiverDistance) {
            if (requiredReceiverDistance <= 0f) {
                return 0f;
            }

            return requiredReceiverDistance + RequiredReceiverShadowPadding;
        }

        private static Light ResolveOrCreateRuntimeDirectionalLight(out bool created) {
            created = false;
            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional) {
                return RenderSettings.sun;
            }

            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++) {
                Light light = lights[i];
                if (light != null && light.enabled && light.type == LightType.Directional) {
                    return light;
                }
            }

            var lightObject = new GameObject("RO Enhanced Directional Light");
            Light createdLight = lightObject.AddComponent<Light>();
            createdLight.type = LightType.Directional;
            created = true;
            return createdLight;
        }

        private static void ApplyRenderPipelineShadowSettings(float shadowDistance, RoEnhancedShadowSettings settings) {
            RenderPipelineAsset pipelineAsset = ResolveActiveRenderPipelineAsset();
            if (pipelineAsset == null) {
                return;
            }

            bool additionalLightShadows =
                (settings.enableRswPointLights && settings.rswPointLightShadows) ||
                (settings.enableModelLampPointLights && settings.modelLampPointLightShadows);
            TrySetRenderPipelineValue(pipelineAsset, "shadowDistance", shadowDistance);
            TrySetRenderPipelineValue(pipelineAsset, "m_ShadowDistance", shadowDistance);
            TrySetRenderPipelineValue(pipelineAsset, "supportsMainLightShadows", true);
            TrySetRenderPipelineValue(pipelineAsset, "m_MainLightShadowsSupported", true);
            TrySetRenderPipelineValue(pipelineAsset, "m_AnyShadowsSupported", true);
            TrySetRenderPipelineValue(pipelineAsset, "supportsSoftShadows", true);
            TrySetRenderPipelineValue(pipelineAsset, "m_SoftShadowsSupported", true);
            TrySetRenderPipelineValue(pipelineAsset, "supportsAdditionalLightShadows", additionalLightShadows);
            TrySetRenderPipelineValue(pipelineAsset, "m_AdditionalLightShadowsSupported", additionalLightShadows);
            TrySetRenderPipelineValue(pipelineAsset, "mainLightShadowmapResolution", Mathf.Max(1, settings.mainLightShadowmapResolution));
            TrySetRenderPipelineValue(pipelineAsset, "m_MainLightShadowmapResolution", Mathf.Max(1, settings.mainLightShadowmapResolution));
            TrySetRenderPipelineValue(pipelineAsset, "m_ShadowAtlasResolution", Mathf.Max(1, settings.shadowAtlasResolution));
            TrySetRenderPipelineValue(pipelineAsset, "additionalLightsShadowmapResolution", Mathf.Max(1, settings.shadowAtlasResolution));
            TrySetRenderPipelineValue(pipelineAsset, "m_AdditionalLightsShadowmapResolution", Mathf.Max(1, settings.shadowAtlasResolution));
            TrySetRenderPipelineValue(pipelineAsset, "m_AdditionalLightsShadowResolutionTierHigh", Mathf.Max(1, settings.shadowAtlasResolution));
            TrySetRenderPipelineValue(pipelineAsset, "m_AdditionalLightsShadowResolutionTierMedium", Mathf.Max(1, settings.shadowAtlasResolution / 2));
            TrySetRenderPipelineValue(pipelineAsset, "m_AdditionalLightsShadowResolutionTierLow", Mathf.Max(1, settings.shadowAtlasResolution / 4));
            int shadowCascadeCount = ResolveShadowCascadeCount(shadowDistance, settings);
            TrySetRenderPipelineValue(pipelineAsset, "shadowCascadeCount", shadowCascadeCount);
            TrySetRenderPipelineValue(pipelineAsset, "m_ShadowCascadeCount", shadowCascadeCount);
            TrySetRenderPipelineValue(pipelineAsset, "cascadeBorder", ShadowCascadeBorder);
            TrySetRenderPipelineValue(pipelineAsset, "m_CascadeBorder", ShadowCascadeBorder);
            TrySetRenderPipelineValue(pipelineAsset, "conservativeEnclosingSphere", true);
            TrySetRenderPipelineValue(pipelineAsset, "m_ConservativeEnclosingSphere", true);
            TrySetRenderPipelineValue(pipelineAsset, "additionalLightsRenderingMode", AdditionalLightsPerPixelMode);
            TrySetRenderPipelineValue(pipelineAsset, "m_AdditionalLightsRenderingMode", AdditionalLightsPerPixelMode);
            TrySetRenderPipelineValue(pipelineAsset, "maxAdditionalLightsCount", Mathf.Clamp(settings.additionalLightsPerObjectLimit, 0, 32));
            TrySetRenderPipelineValue(pipelineAsset, "m_AdditionalLightsPerObjectLimit", Mathf.Clamp(settings.additionalLightsPerObjectLimit, 0, 32));
        }

        private static RenderPipelineAsset ResolveActiveRenderPipelineAsset() {
            if (QualitySettings.renderPipeline != null) {
                return QualitySettings.renderPipeline;
            }

            Type graphicsSettingsType = typeof(GraphicsSettings);
            return TryGetStaticRenderPipelineAsset(graphicsSettingsType, "currentRenderPipeline")
                ?? TryGetStaticRenderPipelineAsset(graphicsSettingsType, "renderPipelineAsset")
                ?? TryGetStaticRenderPipelineAsset(graphicsSettingsType, "defaultRenderPipeline");
        }

        private static RenderPipelineAsset TryGetStaticRenderPipelineAsset(Type ownerType, string propertyName) {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = ownerType.GetProperty(propertyName, flags);
            if (property == null || !typeof(RenderPipelineAsset).IsAssignableFrom(property.PropertyType)) {
                return null;
            }

            try {
                return property.GetValue(null) as RenderPipelineAsset;
            } catch (TargetInvocationException) {
                return null;
            }
        }

        private static PipelineMemberSnapshot[] CapturePipelineSnapshots(RenderPipelineAsset pipelineAsset) {
            if (pipelineAsset == null) {
                return Array.Empty<PipelineMemberSnapshot>();
            }

            string[] members = {
                "shadowDistance",
                "m_ShadowDistance",
                "supportsMainLightShadows",
                "m_MainLightShadowsSupported",
                "m_AnyShadowsSupported",
                "supportsSoftShadows",
                "m_SoftShadowsSupported",
                "supportsAdditionalLightShadows",
                "m_AdditionalLightShadowsSupported",
                "mainLightShadowmapResolution",
                "m_MainLightShadowmapResolution",
                "m_ShadowAtlasResolution",
                "additionalLightsShadowmapResolution",
                "m_AdditionalLightsShadowmapResolution",
                "m_AdditionalLightsShadowResolutionTierHigh",
                "m_AdditionalLightsShadowResolutionTierMedium",
                "m_AdditionalLightsShadowResolutionTierLow",
                "shadowCascadeCount",
                "m_ShadowCascadeCount",
                "cascadeBorder",
                "m_CascadeBorder",
                "conservativeEnclosingSphere",
                "m_ConservativeEnclosingSphere",
                "additionalLightsRenderingMode",
                "m_AdditionalLightsRenderingMode",
                "maxAdditionalLightsCount",
                "m_AdditionalLightsPerObjectLimit"
            };

            var snapshots = new List<PipelineMemberSnapshot>(members.Length);
            for (int i = 0; i < members.Length; i++) {
                if (TryCapturePipelineMember(pipelineAsset, members[i], out PipelineMemberSnapshot snapshot)) {
                    snapshots.Add(snapshot);
                }
            }

            return snapshots.ToArray();
        }

        private static bool TryCapturePipelineMember(object target, string memberName, out PipelineMemberSnapshot snapshot) {
            snapshot = default;
            Type targetType = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = targetType.GetProperty(memberName, flags);
            if (property != null && property.CanRead && property.CanWrite) {
                try {
                    snapshot = PipelineMemberSnapshot.ForProperty(target, property, property.GetValue(target));
                    return true;
                } catch (Exception exception) when (exception is ArgumentException || exception is TargetInvocationException || exception is MethodAccessException) {
                    return false;
                }
            }

            FieldInfo field = targetType.GetField(memberName, flags);
            if (field == null) {
                return false;
            }

            try {
                snapshot = PipelineMemberSnapshot.ForField(target, field, field.GetValue(target));
                return true;
            } catch (Exception exception) when (exception is ArgumentException || exception is TargetInvocationException || exception is MethodAccessException) {
                return false;
            }
        }

        private static bool TrySetRenderPipelineValue(object target, string memberName, object value) {
            if (target == null) {
                return false;
            }

            Type targetType = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = targetType.GetProperty(memberName, flags);
            if (property != null && property.CanWrite) {
                try {
                    property.SetValue(target, ConvertRenderPipelineValue(value, property.PropertyType));
                    return true;
                } catch (Exception exception) when (exception is ArgumentException ||
                    exception is InvalidCastException ||
                    exception is MethodAccessException ||
                    exception is TargetInvocationException) {
                }
            }

            FieldInfo field = targetType.GetField(memberName, flags);
            if (field == null) {
                return false;
            }

            try {
                field.SetValue(target, ConvertRenderPipelineValue(value, field.FieldType));
                return true;
            } catch (Exception exception) when (exception is ArgumentException ||
                exception is InvalidCastException ||
                exception is MethodAccessException ||
                exception is TargetInvocationException) {
                return false;
            }
        }

        private static object ConvertRenderPipelineValue(object value, Type targetType) {
            Type resolvedType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (resolvedType.IsEnum) {
                return value is string text
                    ? Enum.Parse(resolvedType, text)
                    : Enum.ToObject(resolvedType, value);
            }

            if (resolvedType.IsInstanceOfType(value)) {
                return value;
            }

            return Convert.ChangeType(value, resolvedType);
        }
    }

    internal readonly struct PipelineMemberSnapshot {
        private readonly object target;
        private readonly PropertyInfo property;
        private readonly FieldInfo field;
        private readonly object value;

        private PipelineMemberSnapshot(object target, PropertyInfo property, FieldInfo field, object value) {
            this.target = target;
            this.property = property;
            this.field = field;
            this.value = value;
        }

        public static PipelineMemberSnapshot ForProperty(object target, PropertyInfo property, object value) =>
            new PipelineMemberSnapshot(target, property, null, value);

        public static PipelineMemberSnapshot ForField(object target, FieldInfo field, object value) =>
            new PipelineMemberSnapshot(target, null, field, value);

        public void Restore() {
            try {
                if (property != null && property.CanWrite) {
                    property.SetValue(target, value);
                } else {
                    field?.SetValue(target, value);
                }
            } catch (Exception exception) when (exception is ArgumentException ||
                exception is InvalidCastException ||
                exception is MethodAccessException ||
                exception is TargetInvocationException) {
            }
        }
    }

    internal readonly struct LightSnapshot {
        private readonly bool hasLight;
        private readonly bool enabled;
        private readonly Quaternion rotation;
        private readonly Color color;
        private readonly float intensity;
        private readonly LightShadows shadows;
        private readonly float shadowStrength;
        private readonly float shadowBias;
        private readonly float shadowNormalBias;
        private readonly float shadowNearPlane;
        private readonly bool useViewFrustumForShadowCasterCull;
        private readonly int cullingMask;

        private LightSnapshot(Light light) {
            hasLight = light != null;
            enabled = light != null && light.enabled;
            rotation = light != null ? light.transform.rotation : Quaternion.identity;
            color = light != null ? light.color : Color.white;
            intensity = light != null ? light.intensity : 1f;
            shadows = light != null ? light.shadows : LightShadows.None;
            shadowStrength = light != null ? light.shadowStrength : 1f;
            shadowBias = light != null ? light.shadowBias : 0.05f;
            shadowNormalBias = light != null ? light.shadowNormalBias : 0.4f;
            shadowNearPlane = light != null ? light.shadowNearPlane : 0.2f;
            useViewFrustumForShadowCasterCull = light != null && light.useViewFrustumForShadowCasterCull;
            cullingMask = light != null ? light.cullingMask : ~0;
        }

        public static LightSnapshot Capture(Light light) => new LightSnapshot(light);

        public void Restore(Light light) {
            if (!hasLight || light == null) {
                return;
            }

            light.enabled = enabled;
            light.transform.rotation = rotation;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            light.shadowStrength = shadowStrength;
            light.shadowBias = shadowBias;
            light.shadowNormalBias = shadowNormalBias;
            light.shadowNearPlane = shadowNearPlane;
            light.useViewFrustumForShadowCasterCull = useViewFrustumForShadowCasterCull;
            light.cullingMask = cullingMask;
        }
    }
}
