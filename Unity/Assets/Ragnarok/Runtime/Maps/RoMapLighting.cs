using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Maps {
    public readonly struct RoMapLightingState {
        public RoMapLightingState(
            int longitude,
            int latitude,
            Vector3 direction,
            Color ambient,
            Color diffuse,
            float opacity
        ) {
            Longitude = longitude;
            Latitude = latitude;
            Direction = direction;
            Ambient = ambient;
            Diffuse = diffuse;
            Opacity = opacity;
        }

        public int Longitude { get; }
        public int Latitude { get; }
        public Vector3 Direction { get; }
        public Color Ambient { get; }
        public Color Diffuse { get; }
        public float Opacity { get; }
    }

    public static class RoMapLighting {
        private static readonly Vector3 DefaultDirection = new Vector3(0.5f, 0.7071068f, 0.5f).normalized;
        private const string PointLightRootName = "RO Point Lights";
        private const string ModelLampPointLightRootName = "RO Model Lamp Lights";
        private static readonly List<MeshRenderer> RendererScratch = new List<MeshRenderer>(512);
        private static readonly List<Material> MaterialScratch = new List<Material>(8);
        private static readonly List<RoModelSurfaceInfo> ModelSurfaceScratch = new List<RoModelSurfaceInfo>(256);
        private static readonly List<ModelLampLightCandidate> ModelLampCandidateScratch =
            new List<ModelLampLightCandidate>(128);

        public static RoMapLightingState FromWorld(RSW.LightInfo light) {
            if (light == null) {
                return new RoMapLightingState(
                    45,
                    45,
                    DefaultDirection,
                    new Color(0.3f, 0.3f, 0.3f, 1f),
                    Color.white,
                    1f
                );
            }

            Vector3 direction = ToUnityLightDirection(light.longitude, light.latitude);
            return new RoMapLightingState(
                light.longitude,
                light.latitude,
                direction,
                ToColor(light.ambient, new Color(0.3f, 0.3f, 0.3f, 1f)),
                ToColor(light.diffuse, Color.white),
                Mathf.Max(0f, light.intensity)
            );
        }

        public static Vector3 ToUnityLightDirection(int longitudeDegrees, int latitudeDegrees) {
            float longitude = longitudeDegrees * Mathf.Deg2Rad;
            float latitude = latitudeDegrees * Mathf.Deg2Rad;

            // roBrowser calculates the map-space light vector as a downward vector.
            // Unity map objects are Y-flipped, so shaders receive the opposite world-space direction.
            var roBrowserDirection = new Vector3(
                -Mathf.Cos(longitude) * Mathf.Sin(latitude),
                -Mathf.Cos(latitude),
                -Mathf.Sin(longitude) * Mathf.Sin(latitude)
            );

            Vector3 unityDirection = -roBrowserDirection;
            return unityDirection.sqrMagnitude > 0.000001f ? unityDirection.normalized : DefaultDirection;
        }

        public static void ApplyToRenderers(GameObject root, RoMapLightingState state, bool enabled, bool useGroundLightmapAlpha, float lightingStrength = 1f) {
            if (root == null) {
                return;
            }

            RendererScratch.Clear();
            root.GetComponentsInChildren(true, RendererScratch);
            for (int i = 0; i < RendererScratch.Count; i++) {
                RendererScratch[i].GetSharedMaterials(MaterialScratch);
                for (int j = 0; j < MaterialScratch.Count; j++) {
                    ApplyToMaterial(MaterialScratch[j], state, enabled, useGroundLightmapAlpha, lightingStrength);
                }

                MaterialScratch.Clear();
            }

            RendererScratch.Clear();
        }

        public static void ApplyToMaterial(Material material, RoMapLightingState state, bool enabled, bool useGroundLightmapAlpha, float lightingStrength = 1f) {
            if (material == null) {
                return;
            }

            SetFloatIfPropertyExists(material, "_RoUseLighting", enabled ? 1f : 0f);
            SetVectorIfPropertyExists(material, "_RoLightDirection", state.Direction);
            SetColorIfPropertyExists(material, "_RoLightAmbient", state.Ambient);
            SetColorIfPropertyExists(material, "_RoLightDiffuse", state.Diffuse);
            SetFloatIfPropertyExists(material, "_RoLightOpacity", state.Opacity);
            SetFloatIfPropertyExists(material, "_RoUseLightmapAlpha", useGroundLightmapAlpha ? 1f : 0f);
            SetFloatIfPropertyExists(material, "_RoLightingStrength", Mathf.Clamp01(lightingStrength));
        }

        public static void ApplyDynamicLightingToRenderers(GameObject root, RoMapLightingState state, bool enabled, float lightingStrength = 1f) {
            if (root == null) {
                return;
            }

            RendererScratch.Clear();
            root.GetComponentsInChildren(true, RendererScratch);
            for (int i = 0; i < RendererScratch.Count; i++) {
                RendererScratch[i].GetSharedMaterials(MaterialScratch);
                for (int j = 0; j < MaterialScratch.Count; j++) {
                    ApplyDynamicLightingToMaterial(MaterialScratch[j], state, enabled, lightingStrength);
                }

                MaterialScratch.Clear();
            }

            RendererScratch.Clear();
        }

        public static void ApplyDynamicLightingToMaterial(Material material, RoMapLightingState state, bool enabled, float lightingStrength = 1f) {
            if (material == null) {
                return;
            }

            SetFloatIfPropertyExists(material, "_RoUseLighting", enabled ? 1f : 0f);
            SetVectorIfPropertyExists(material, "_RoLightDirection", state.Direction);
            SetColorIfPropertyExists(material, "_RoLightAmbient", state.Ambient);
            SetColorIfPropertyExists(material, "_RoLightDiffuse", state.Diffuse);
            SetFloatIfPropertyExists(material, "_RoLightOpacity", state.Opacity);
            SetFloatIfPropertyExists(material, "_RoLightingStrength", Mathf.Clamp01(lightingStrength));
        }

        public static string BuildAudit(RSW.LightInfo light, bool enabled, bool useGroundLightmapAlpha, float lightingStrength = 1f) {
            RoMapLightingState state = FromWorld(light);
            var builder = new StringBuilder();
            builder.AppendLine("RO map lighting audit:");
            builder.AppendLine($"- enabled={enabled}, useGroundLightmapAlpha={useGroundLightmapAlpha}, strength={Mathf.Clamp01(lightingStrength):0.###}");
            builder.AppendLine($"- rsw longitude={state.Longitude}, latitude={state.Latitude}, opacity={state.Opacity:0.###}");
            builder.AppendLine($"- unityDirection={FormatVector3(state.Direction)}");
            builder.AppendLine($"- ambient={FormatColor(state.Ambient)}, diffuse={FormatColor(state.Diffuse)}");
            return builder.ToString().TrimEnd();
        }

        public static GameObject BuildPointLights(
            IReadOnlyList<RSW.Light> sourceLights,
            GND.Mesh ground,
            int maxLights,
            float rangeScale,
            float intensityScale
        ) => BuildPointLights(
            sourceLights,
            ground,
            maxLights,
            rangeScale,
            intensityScale,
            0f,
            float.PositiveInfinity,
            useFixedIntensity: false,
            fixedIntensity: 0f
        );

        public static GameObject BuildPointLights(
            IReadOnlyList<RSW.Light> sourceLights,
            GND.Mesh ground,
            int maxLights,
            float rangeScale,
            float intensityScale,
            float minimumRange,
            float maximumRange,
            bool useFixedIntensity,
            float fixedIntensity,
            bool castShadows = false,
            float shadowStrength = 1f
        ) {
            if (sourceLights == null || sourceLights.Count == 0 || ground == null || maxLights <= 0) {
                return null;
            }

            var root = new GameObject(PointLightRootName);
            float minRange = Mathf.Max(0f, minimumRange);
            float maxRange = Mathf.Max(minRange, maximumRange);
            var candidates = new List<RSW.Light>(sourceLights.Count);
            for (int i = 0; i < sourceLights.Count; i++) {
                if (sourceLights[i] != null) {
                    candidates.Add(sourceLights[i]);
                }
            }

            candidates.Sort(ComparePointLightImportance);

            int createdCount = 0;
            for (int i = 0; i < candidates.Count && createdCount < maxLights; i++) {
                RSW.Light source = candidates[i];
                var lightObject = new GameObject(string.IsNullOrWhiteSpace(source.name) ? $"RSW Light {createdCount}" : source.name);
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = ToUnityPointLightLocalPosition(source, ground);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = ToPointLightColor(source);
                light.range = Mathf.Clamp(ToPointLightRange(source, rangeScale), minRange, maxRange);
                light.intensity = useFixedIntensity
                    ? Mathf.Max(0f, fixedIntensity)
                    : ToPointLightIntensity(source, intensityScale);
                light.cullingMask = ~0;
                light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = Mathf.Clamp01(shadowStrength);
                light.shadowBias = 0.03f;
                light.shadowNormalBias = 0.2f;
                light.shadowNearPlane = 0.1f;
                light.renderMode = LightRenderMode.Auto;

                lightObject.AddComponent<RoMapPointLightInfo>().Initialize(
                    source.name,
                    ToVector3(source.pos),
                    lightObject.transform.localPosition,
                    light.color,
                    source.range,
                    light.range,
                    light.intensity
                );
                createdCount++;
            }

            if (createdCount == 0) {
                DestroyRuntimeObject(root);
                return null;
            }

            return root;
        }

        public static Vector3 ToUnityPointLightLocalPosition(RSW.Light source, GND.Mesh ground) {
            if (source == null || ground == null || source.pos == null || source.pos.Length < 3) {
                return Vector3.zero;
            }

            return new Vector3(
                source.pos[0] + ground.width,
                -source.pos[1],
                source.pos[2] + ground.height
            );
        }

        public static Color ToPointLightColor(RSW.Light source) {
            return source != null ? ToColor(source.color, Color.white) : Color.white;
        }

        public static float ToPointLightRange(RSW.Light source, float scale) {
            return source != null ? Mathf.Max(0f, source.range * Mathf.Max(0f, scale)) : 0f;
        }

        public static float ToPointLightIntensity(RSW.Light source, float scale) {
            return source != null ? Mathf.Max(0f, source.range * Mathf.Max(0f, scale)) : 0f;
        }

        /// <summary>RSW effect ids that are a burning fire: EF_TORCH 47 and the flat torch / dust flames 689 / 690 / 691 / 696.</summary>
        public static bool IsFireEffect(int effectId) => effectId == 47 || effectId == 689 || effectId == 690 || effectId == 691 || effectId == 696;

        public static readonly Color FireLightColor = new Color(1f, 0.48f, 0.2f);
        public const float FireLightRange = 9f;
        public const float FireLightIntensity = 0.9f;

        /// <summary>
        /// A point light at every fire effect record of the map (torches of Payon Cave, braziers, camp fires), placed like
        /// the RSW lights; they burn day and night. The look rig adds the flicker and the glow (RoMapFireLightInfo).
        /// </summary>
        public static GameObject BuildFireLights(IReadOnlyList<RSW.Effect> effects, GND.Mesh ground, int maxLights = 48) {
            if (effects == null || ground == null) return null;
            GameObject root = null;
            int created = 0;
            for (int i = 0; i < effects.Count && created < maxLights; i++) {
                RSW.Effect effect = effects[i];
                if (effect?.pos == null || effect.pos.Length < 3 || !IsFireEffect(effect.id)) continue;
                root ??= new GameObject("RO Fire Lights");
                var lightObject = new GameObject("Fire " + effect.id + " " + effect.name);
                lightObject.transform.SetParent(root.transform, false);
                // the flame sits a little above the record point; the light just above it
                lightObject.transform.localPosition = new Vector3(effect.pos[0] + ground.width, -effect.pos[1] + 2.0f, effect.pos[2] + ground.height);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = FireLightColor;
                light.range = FireLightRange;
                light.intensity = FireLightIntensity;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
                lightObject.AddComponent<RoMapFireLightInfo>();
                created++;
            }

            return root;
        }

        public static void EnableModelLampRenderingLayer(Renderer renderer) {
            if (renderer == null) {
                return;
            }

            renderer.renderingLayerMask |= RoEnhancedShadowSettings.DefaultModelLampRenderingLayerMask;
        }

        public static GameObject BuildModelLampPointLights(
            GameObject modelRoot,
            int maxLights,
            Color color,
            float range,
            float intensity,
            bool castShadows = false,
            float shadowStrength = 1f,
            float shadowBias = 0.12f,
            float shadowNormalBias = 0.8f,
            float shadowNearPlane = 0.25f,
            bool lampGlassCastsShadows = false,
            float verticalOffset = 0f,
            float dayIntensityScale = 0f,
            float nightIntensityScale = 1f,
            RoModelFeatureDatabase modelFeatureDatabase = null
        ) {
            if (modelRoot == null || maxLights <= 0) {
                return null;
            }

            ModelSurfaceScratch.Clear();
            modelRoot.GetComponentsInChildren(true, ModelSurfaceScratch);
            if (ModelSurfaceScratch.Count == 0) {
                return null;
            }

            ModelLampCandidateScratch.Clear();
            for (int i = 0; i < ModelSurfaceScratch.Count; i++) {
                RoModelSurfaceInfo surface = ModelSurfaceScratch[i];
                if (modelFeatureDatabase == null ||
                    !modelFeatureDatabase.TryFindPointLightEntry(surface, out RoModelFeatureDatabase.ModelPointLightEntry pointLightEntry) ||
                    pointLightEntry == null ||
                    !pointLightEntry.enabled) {
                    continue;
                }

                MeshRenderer renderer = surface.FindComponent<MeshRenderer>();
                if (renderer == null) {
                    continue;
                }

                ModelLampCandidateScratch.Add(new ModelLampLightCandidate(surface, renderer, pointLightEntry));
            }

            ModelSurfaceScratch.Clear();

            if (ModelLampCandidateScratch.Count == 0) {
                ModelLampCandidateScratch.Clear();
                return null;
            }

            ModelLampCandidateScratch.Sort(CompareModelLampImportance);

            var root = new GameObject(ModelLampPointLightRootName);
            root.transform.SetParent(modelRoot.transform, false);

            int createdCount = 0;
            for (int i = 0; i < ModelLampCandidateScratch.Count && createdCount < maxLights; i++) {
                ModelLampLightCandidate candidate = ModelLampCandidateScratch[i];
                RoModelSurfaceInfo surface = candidate.Surface;
                MeshRenderer renderer = candidate.Renderer;
                RoModelFeatureDatabase.ModelPointLightEntry pointLightEntry = candidate.Entry;
                if (!pointLightEntry.matchedSurfaceCastsShadows) {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                string lightName = string.IsNullOrWhiteSpace(pointLightEntry.displayName)
                    ? surface.NodeName
                    : pointLightEntry.displayName;
                var lightObject = new GameObject($"{lightName} Lamp Light");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = modelRoot.transform.InverseTransformPoint(
                    renderer.bounds.center + new Vector3(0f, pointLightEntry.verticalOffset, 0f));

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = pointLightEntry.color;
                light.range = Mathf.Max(0f, pointLightEntry.range);
                light.intensity = Mathf.Max(0f, pointLightEntry.intensity);
                light.cullingMask = ~0;
                light.renderingLayerMask = (int) RoEnhancedShadowSettings.DefaultModelLampRenderingLayerMask;
                light.shadows = pointLightEntry.castShadows ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = Mathf.Clamp01(pointLightEntry.shadowStrength);
                light.shadowBias = Mathf.Max(0f, pointLightEntry.shadowBias);
                light.shadowNormalBias = Mathf.Max(0f, pointLightEntry.shadowNormalBias);
                light.shadowNearPlane = Mathf.Max(0.01f, pointLightEntry.shadowNearPlane);
                light.renderMode = LightRenderMode.Auto;

                float resolvedIntensity = Mathf.Max(0f, pointLightEntry.intensity);
                lightObject.AddComponent<RoMapNightLight>().Initialize(
                    resolvedIntensity,
                    Mathf.Max(0f, pointLightEntry.dayIntensityScale),
                    Mathf.Max(0f, pointLightEntry.nightIntensityScale));
                lightObject.AddComponent<RoMapModelLampLightInfo>().Initialize(
                    surface.ModelFile,
                    surface.NodeName,
                    surface.TextureName,
                    lightObject.transform.localPosition,
                    pointLightEntry.color,
                    Mathf.Max(0f, pointLightEntry.range),
                    resolvedIntensity
                );
                createdCount++;
            }

            ModelLampCandidateScratch.Clear();

            if (createdCount == 0) {
                DestroyRuntimeObject(root);
                return null;
            }

            return root;
        }

        private static int ComparePointLightImportance(RSW.Light left, RSW.Light right) {
            float leftScore = left != null ? left.range : 0f;
            float rightScore = right != null ? right.range : 0f;
            return rightScore.CompareTo(leftScore);
        }

        private static int CompareModelLampImportance(ModelLampLightCandidate left, ModelLampLightCandidate right) {
            float leftScore = left.Entry != null ? Mathf.Max(0f, left.Entry.range) * Mathf.Max(0f, left.Entry.intensity) : 0f;
            float rightScore = right.Entry != null ? Mathf.Max(0f, right.Entry.range) * Mathf.Max(0f, right.Entry.intensity) : 0f;
            return rightScore.CompareTo(leftScore);
        }

        public static string BuildPointLightAudit(
            IReadOnlyList<RSW.Light> sourceLights,
            GND.Mesh ground,
            bool enabled,
            int maxLights,
            float rangeScale,
            float intensityScale,
            GameObject root,
            int maxItems = 16
        ) {
            int sourceCount = sourceLights != null ? sourceLights.Count : 0;
            int createdCount = root != null ? root.GetComponentsInChildren<RoMapPointLightInfo>(true).Length : 0;
            var builder = new StringBuilder();
            builder.AppendLine("RO map point light audit:");
            builder.AppendLine($"- enabled={enabled}, sourceLights={sourceCount}, createdLights={createdCount}, maxLights={maxLights}, rangeScale={rangeScale:0.###}, intensityScale={intensityScale:0.###}");

            if (sourceLights == null || ground == null || sourceLights.Count == 0) {
                return builder.ToString().TrimEnd();
            }

            int count = Math.Min(sourceLights.Count, Math.Min(maxItems, Math.Max(0, maxLights)));
            for (int i = 0; i < count; i++) {
                RSW.Light source = sourceLights[i];
                builder.AppendLine(
                    $"- index={i}, name='{source.name}', sourcePos={FormatVector3(ToVector3(source.pos))}, unityLocal={FormatVector3(ToUnityPointLightLocalPosition(source, ground))}, color={FormatColor(ToPointLightColor(source))}, sourceRange={source.range:0.###}, unityRange={ToPointLightRange(source, rangeScale):0.###}, unityIntensity={ToPointLightIntensity(source, intensityScale):0.###}"
                );
            }

            if (sourceLights.Count > count) {
                builder.AppendLine($"- omitted={sourceLights.Count - count}");
            }

            return builder.ToString().TrimEnd();
        }

        private static Color ToColor(float[] values, Color fallback) {
            if (values == null || values.Length < 3) {
                return fallback;
            }

            return new Color(values[0], values[1], values[2], 1f);
        }

        private static Vector3 ToVector3(float[] values) {
            if (values == null || values.Length < 3) {
                return Vector3.zero;
            }

            return new Vector3(values[0], values[1], values[2]);
        }

        private static void SetFloatIfPropertyExists(Material material, string property, float value) {
            if (material.HasProperty(property)) {
                material.SetFloat(property, value);
            }
        }

        private static void SetVectorIfPropertyExists(Material material, string property, Vector3 value) {
            if (material.HasProperty(property)) {
                material.SetVector(property, new Vector4(value.x, value.y, value.z, 0f));
            }
        }

        private static void SetColorIfPropertyExists(Material material, string property, Color value) {
            if (material.HasProperty(property)) {
                material.SetColor(property, value);
            }
        }

        private static string FormatVector3(Vector3 value) {
            return $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";
        }

        private static string FormatColor(Color value) {
            return $"({value.r:0.###},{value.g:0.###},{value.b:0.###},{value.a:0.###})";
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                UnityEngine.Object.Destroy(target);
            } else {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private readonly struct ModelLampLightCandidate {
            public ModelLampLightCandidate(
                RoModelSurfaceInfo surface,
                MeshRenderer renderer,
                RoModelFeatureDatabase.ModelPointLightEntry entry
            ) {
                Surface = surface;
                Renderer = renderer;
                Entry = entry;
            }

            public RoModelSurfaceInfo Surface { get; }
            public MeshRenderer Renderer { get; }
            public RoModelFeatureDatabase.ModelPointLightEntry Entry { get; }
        }
    }

    public sealed class RoMapPointLightInfo : MonoBehaviour {
        [SerializeField] private string sourceName;
        [SerializeField] private Vector3 sourcePosition;
        [SerializeField] private Vector3 unityLocalPosition;
        [SerializeField] private Color color;
        [SerializeField] private float sourceRange;
        [SerializeField] private float unityRange;
        [SerializeField] private float unityIntensity;

        public string SourceName => sourceName;
        public Vector3 SourcePosition => sourcePosition;
        public Vector3 UnityLocalPosition => unityLocalPosition;
        public Color Color => color;
        public float SourceRange => sourceRange;
        public float UnityRange => unityRange;
        public float UnityIntensity => unityIntensity;

        public void Initialize(
            string sourceName,
            Vector3 sourcePosition,
            Vector3 unityLocalPosition,
            Color color,
            float sourceRange,
            float unityRange,
            float unityIntensity
        ) {
            this.sourceName = sourceName;
            this.sourcePosition = sourcePosition;
            this.unityLocalPosition = unityLocalPosition;
            this.color = color;
            this.sourceRange = sourceRange;
            this.unityRange = unityRange;
            this.unityIntensity = unityIntensity;
        }
    }

    public sealed class RoMapNightLight : MonoBehaviour {
        [SerializeField] private float baseIntensity;
        [SerializeField] private float dayIntensityScale;
        [SerializeField] private float nightIntensityScale;

        private Light cachedLight;
        private bool shadowModeKnown;
        private LightShadows lampShadows;

        /// <summary>
        /// The loaded map is an interior (set by RoRuntimeMapController before the map is built): lamps cast shadows
        /// there at any hour, outdoors only at night (a lamp shadow in full daylight read as a ghost copy of the sprite).
        /// </summary>
        public static bool IndoorMap;

        public float BaseIntensity => baseIntensity;
        public float DayIntensityScale => dayIntensityScale;
        public float NightIntensityScale => nightIntensityScale;

        public void Initialize(float baseIntensity, float dayIntensityScale, float nightIntensityScale) {
            this.baseIntensity = Mathf.Max(0f, baseIntensity);
            this.dayIntensityScale = Mathf.Max(0f, dayIntensityScale);
            this.nightIntensityScale = Mathf.Max(0f, nightIntensityScale);
            Apply(isNight: false);
        }

        public void Apply(bool isNight) {
            if (this == null) {
                return;
            }

            Light light = ResolveLight();
            if (light == null) {
                return;
            }

            float scale = isNight ? nightIntensityScale : dayIntensityScale;
            float resolvedIntensity = baseIntensity * scale;
            light.intensity = resolvedIntensity;
            light.enabled = resolvedIntensity > 0.0001f;
            if (!shadowModeKnown) {
                lampShadows = light.shadows;
                shadowModeKnown = true;
            }

            light.shadows = isNight || IndoorMap ? lampShadows : LightShadows.None;
        }

        public void Restore() {
            Apply(isNight: false);
        }

        private Light ResolveLight() {
            if (cachedLight == null) {
                cachedLight = GetComponent<Light>();
            }

            return cachedLight;
        }
    }

    /// <summary>Marks a light made for a fire effect record of the map (RoMapLighting.BuildFireLights).</summary>
    public sealed class RoMapFireLightInfo : MonoBehaviour {
    }

    public sealed class RoMapModelLampLightInfo : MonoBehaviour {
        [SerializeField] private string modelFile;
        [SerializeField] private string nodeName;
        [SerializeField] private string textureName;
        [SerializeField] private Vector3 unityLocalPosition;
        [SerializeField] private Color color;
        [SerializeField] private float unityRange;
        [SerializeField] private float unityIntensity;

        public string ModelFile => modelFile;
        public string NodeName => nodeName;
        public string TextureName => textureName;
        public Vector3 UnityLocalPosition => unityLocalPosition;
        public Color Color => color;
        public float UnityRange => unityRange;
        public float UnityIntensity => unityIntensity;

        public void Initialize(
            string modelFile,
            string nodeName,
            string textureName,
            Vector3 unityLocalPosition,
            Color color,
            float unityRange,
            float unityIntensity
        ) {
            this.modelFile = modelFile;
            this.nodeName = nodeName;
            this.textureName = textureName;
            this.unityLocalPosition = unityLocalPosition;
            this.color = color;
            this.unityRange = unityRange;
            this.unityIntensity = unityIntensity;
        }
    }
}
