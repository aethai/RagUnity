using System.Collections.Generic;
using Ragnarok.Maps;
using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using Unity.Profiling;
using UnityEngine;

namespace Ragnarok.Entities {
    public enum RoActorMapLightProbeSamplingMode {
        Continuous,
        OnMoveOrInitial,
        Disabled
    }

    public sealed class RoActorMapLightProbe : MonoBehaviour, IRoActorLateTick, IRoActorTickBudget {
        private const float RayStartHeight = 25f;
        private const float RayDistance = 80f;
        private const float SampleIntervalSeconds = 0.35f;
        private const float EnhancedSampleIntervalSeconds = 1f / 30f;
        private const float EnhancedCrowdSampleIntervalSeconds = 1f / 15f;
        private const float EnhancedHeavyCrowdSampleIntervalSeconds = 0.125f;
        private const float EnhancedTintHalfLifeSeconds = 0.08f;
        private const float EnhancedTintBoostScale = 0.08f;
        private const float EnhancedTintMaxBoost = 0.35f;
        // Lamp glow is routed to the additive channel so it survives the night darkening (a lamp lights up
        // a character standing in the dark). Tunable globally via RoEnhancedShadowSettings.mapEmissionLightInfluence.
        private const float EnhancedLampAddScale = 0.16f;
        private const float EnhancedLampAddMaxGlow = 0.85f;
        private const float EnhancedTintMaxMultiply = 1.35f;
        private const float ImmediateLightApplyMoveThresholdSqr = 16f;
        private const float CrowdSampleIntervalSeconds = 1.25f;
        private const float HeavyCrowdSampleIntervalSeconds = 2f;
        private const float SampleMoveThresholdSqr = 0.0625f;
        private const int CrowdProbeThreshold = 32;
        private const int HeavyCrowdProbeThreshold = 80;
        private const int SamplesPerFrame = 4;
        private const int CrowdSamplesPerFrame = 2;
        private const int HeavyCrowdSamplesPerFrame = 1;
        private const float MapControllerLookupRetrySeconds = 0.5f;

        private static readonly int LightmapId = Shader.PropertyToID("_Lightmap");
        private static readonly int UseLightmapId = Shader.PropertyToID("_UseLightmap");
        private static readonly Dictionary<int, CachedMeshLightData> MeshLightDataCache = new Dictionary<int, CachedMeshLightData>(32);
        private static readonly ProfilerMarker SampleMarker = new ProfilerMarker("ActorMapLightProbe.Sample");
        private static int activeProbeCount;
        private static int sampleBudgetFrame = -1;
        private static int samplesThisFrame;

        [SerializeField] private Collider groundCollider;
        [SerializeField] private bool sampleInEditMode;
        [SerializeField] private bool sampleBakedShadows = true;
        [SerializeField] private RoActorMapLightProbeSamplingMode samplingMode = RoActorMapLightProbeSamplingMode.Continuous;

        private GND.Mesh compiledGround;
        private RoActorLayeredVisual layeredVisual;
        private RoActorSpriteVisual spriteVisual;
        private RoRuntimeMapController mapController;
        private float nextMapControllerLookupTime;
        private bool mapControllerLookupAttempted;
        private float nextSampleTime;
        private Vector3 lastSampledWorldPosition;
        private bool hasSampledWorldPosition;
        // Global day/night darkening applied to every actor's map-light multiply (white = no change).
        // Set by the day/night controller so actors dim and tint with the world at night; the additive
        // channel is left untouched so point lights / lanterns still brighten actors after dark.
        public static Color GlobalActorDayNightMultiply = Color.white;

        // Bumped by the day/night controller whenever night toggles, so stationary actors re-sample their
        // lamp glow at the transition (the global multiply alone only re-pushes the base darkening).
        public static int DayNightLampEpoch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobalActorDayNightState() {
            GlobalActorDayNightMultiply = Color.white;
            DayNightLampEpoch = 0;
        }

        private Color smoothedMapLightMultiply = Color.white;
        private Color smoothedMapLightAdd = Color.clear;
        private float lastMapLightSmoothTime;
        private bool hasSmoothedMapLight;
        private Color lastPushedDayNightMultiply = Color.white;
        private bool hasPushedMapLight;
        private int lastSampledLampEpoch = -1;

        public Collider GroundCollider => groundCollider;
        public RoActorMapLightProbeSamplingMode SamplingMode => samplingMode;

        /// <summary>Read-only: whether this probe samples legacy baked <c>GND.shadowMap</c> occlusion. Diagnostics only.</summary>
        public bool SampleBakedShadows => sampleBakedShadows;

        /// <summary>Read-only smoothed map-light multiply currently pushed to the sprite. Diagnostics only.</summary>
        public Color SmoothedMapLightMultiply => smoothedMapLightMultiply;

        /// <summary>Read-only smoothed map-light add currently pushed to the sprite. Diagnostics only.</summary>
        public Color SmoothedMapLightAdd => smoothedMapLightAdd;

        public void Initialize(Collider collider) {
            Initialize(collider, null);
        }

        public void Initialize(Collider collider, GND.Mesh ground) {
            Initialize(collider, ground, sampleBakedShadows);
        }

        public void Initialize(Collider collider, GND.Mesh ground, bool enableBakedShadowSampling) =>
            Initialize(collider, ground, enableBakedShadowSampling, null);

        /// <param name="controller">The map controller when the caller has it: the probe otherwise finds it with a
        /// scene-wide search (~1 ms in a loaded map) the first time it samples.</param>
        public void Initialize(Collider collider, GND.Mesh ground, bool enableBakedShadowSampling, RoRuntimeMapController controller) {
            groundCollider = collider;
            compiledGround = ground;
            mapController = controller;
            mapControllerLookupAttempted = false;
            nextMapControllerLookupTime = 0f;
            sampleBakedShadows = enableBakedShadowSampling;
            InvalidateSamplePosition();
            ResolveVisuals();
            if (samplingMode != RoActorMapLightProbeSamplingMode.Disabled) {
                SampleAndApply();
            }
        }

        public void SetSamplingMode(RoActorMapLightProbeSamplingMode mode) {
            if (samplingMode == mode) {
                return;
            }

            samplingMode = mode;
            InvalidateSamplePosition();
        }

        public void SetBakedShadowSamplingEnabled(bool enabled) {
            if (sampleBakedShadows == enabled) {
                return;
            }

            sampleBakedShadows = enabled;
            InvalidateSamplePosition();
            if (samplingMode != RoActorMapLightProbeSamplingMode.Disabled) {
                SampleAndApply();
            }
        }

        public void SetGroundCollider(Collider collider) {
            groundCollider = collider;
            InvalidateSamplePosition();
        }

        public void SetGroundData(GND.Mesh ground) {
            compiledGround = ground;
            InvalidateSamplePosition();
        }

        private void OnEnable() {
            activeProbeCount++;
            RoActorTickService.Register((IRoActorLateTick) this);
        }

        private void OnDisable() {
            activeProbeCount = Mathf.Max(0, activeProbeCount - 1);
            RoActorTickService.Unregister((IRoActorLateTick) this);
        }

        public bool ShouldRunActorTick(float now, bool lateTick) {
            return ShouldRunActorTickAt(now, transform.position, lateTick);
        }

        public void ActorLateTick(float now) {
            if (!Application.isPlaying && !sampleInEditMode) {
                return;
            }

            if (samplingMode == RoActorMapLightProbeSamplingMode.Disabled) {
                return;
            }

            Vector3 worldPosition = transform.position;
            if (!ShouldSampleAt(now, worldPosition)) {
                // No new ground sample needed, but the global day/night multiply may have changed; keep
                // stationary actors (idle NPCs/mobs) in sync with the time of day.
                ReapplyGlobalDayNightIfChanged();
                return;
            }

            nextSampleTime = now + ResolveSampleInterval();
            SampleAndApply(worldPosition);
        }

        public void SampleAndApply() {
            SampleAndApply(transform.position);
        }

        private void SampleAndApply(Vector3 worldPosition) {
            using (SampleMarker.Auto()) {
                ResolveVisuals();
                if (layeredVisual == null && spriteVisual == null) {
                    return;
                }

                bool lampEpochChanged = lastSampledLampEpoch != DayNightLampEpoch;
                if (!TrySampleGroundAt(worldPosition, out Color multiply, out Color add)) {
                    multiply = Color.white;
                    add = Color.clear;
                }

                ApplyMapLight(worldPosition, multiply, add, lampEpochChanged);
                lastSampledWorldPosition = worldPosition;
                hasSampledWorldPosition = true;
                lastSampledLampEpoch = DayNightLampEpoch;
            }
        }

        private void ResolveVisuals() {
            if (layeredVisual == null) {
                layeredVisual = GetComponentInChildren<RoActorLayeredVisual>(true);
            }

            if (layeredVisual == null && spriteVisual == null) {
                spriteVisual = GetComponentInChildren<RoActorSpriteVisual>(true);
            }
        }

        private bool TrySampleGroundAt(Vector3 worldPosition, out Color multiply, out Color add) {
            multiply = Color.white;
            add = Color.clear;
            RoRuntimeMapController controller = ResolveMapController();
            bool useEnhancedActorTint = controller != null && controller.ShadowRenderMode == RoShadowRenderMode.Enhanced;

            if (!sampleBakedShadows) {
                return TrySampleEnhancedActorTint(controller, worldPosition, out multiply, out add);
            }

            bool sampledBaked = TrySampleBakedGroundAt(worldPosition, controller, out multiply, out add);
            if (useEnhancedActorTint &&
                TrySampleEnhancedActorTint(controller, worldPosition, out Color enhancedMultiply, out Color enhancedAdd)) {
                multiply = CombineMapLightMultiply(multiply, enhancedMultiply);
                add = ClampColor(add + enhancedAdd, Color.clear, 1f);
                return true;
            }

            return sampledBaked;
        }

        private bool TrySampleBakedGroundAt(
            Vector3 worldPosition,
            RoRuntimeMapController controller,
            out Color multiply,
            out Color add
        ) {
            multiply = Color.white;
            add = Color.clear;
            if (groundCollider == null || compiledGround == null) {
                if (groundCollider == null) {
                    groundCollider = controller != null ? controller.GroundCollider : null;
                }

                if (compiledGround == null) {
                    compiledGround = controller != null ? controller.CompiledGround : null;
                }
            }

            if (groundCollider == null) {
                return false;
            }

            if (compiledGround != null) {
                Vector3 directGroundLocalPoint = groundCollider.transform.InverseTransformPoint(worldPosition);
                if (TrySampleShadowMap(compiledGround, directGroundLocalPoint, out float directShadow)) {
                    multiply = ToMultiplyColor(directShadow);
                    return true;
                }
            }

            var ray = new Ray(worldPosition + Vector3.up * RayStartHeight, Vector3.down);
            if (!groundCollider.Raycast(ray, out RaycastHit hit, RayDistance)) {
                return false;
            }

            MeshCollider meshCollider = hit.collider as MeshCollider;
            Mesh mesh = meshCollider != null ? meshCollider.sharedMesh : null;
            Renderer renderer = hit.collider.FindComponent<Renderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            Vector3 groundLocalPoint = hit.collider.transform.InverseTransformPoint(hit.point);
            if (TrySampleShadowMap(compiledGround, groundLocalPoint, out float shadow)) {
                multiply = ToMultiplyColor(shadow);
                return true;
            }

            return TrySampleMapLight(mesh, hit.triangleIndex, hit.barycentricCoordinate, material, out multiply, out add);
        }

        public static Color CombineMapLightMultiply(Color bakedMultiply, Color enhancedMultiply) {
            return ClampMapLightMultiply(new Color(
                bakedMultiply.r * enhancedMultiply.r,
                bakedMultiply.g * enhancedMultiply.g,
                bakedMultiply.b * enhancedMultiply.b,
                1f));
        }

        private void ApplyMapLight(Vector3 worldPosition, Color targetMultiply, Color targetAdd, bool forceImmediate = false) {
            float now = Application.isPlaying ? Time.realtimeSinceStartup : 0f;
            bool immediate =
                forceImmediate ||
                sampleBakedShadows ||
                !Application.isPlaying ||
                !hasSmoothedMapLight ||
                !hasSampledWorldPosition ||
                Vector3.SqrMagnitude(worldPosition - lastSampledWorldPosition) >= ImmediateLightApplyMoveThresholdSqr;

            targetMultiply = ClampMapLightMultiply(targetMultiply);
            targetAdd = ClampColor(targetAdd, Color.clear, 1f);
            if (immediate) {
                smoothedMapLightMultiply = targetMultiply;
                smoothedMapLightAdd = targetAdd;
            } else {
                float deltaSeconds = Mathf.Max(0f, now - lastMapLightSmoothTime);
                float factor = ResolveExponentialSmoothingFactor(deltaSeconds, EnhancedTintHalfLifeSeconds);
                smoothedMapLightMultiply = LerpColor(smoothedMapLightMultiply, targetMultiply, factor);
                smoothedMapLightAdd = LerpColor(smoothedMapLightAdd, targetAdd, factor);
            }

            lastMapLightSmoothTime = now;
            hasSmoothedMapLight = true;
            PushMapLight();
        }

        // Pushes the current smoothed map light to the visuals with the global day/night multiply folded
        // in. Kept separate from ground sampling so the day/night change can be re-applied to stationary
        // actors (NPCs, idle mobs) without re-running the expensive ground sample.
        private void PushMapLight() {
            ResolveVisuals();
            Color dayNight = GlobalActorDayNightMultiply;
            Color pushedMultiply = new Color(
                smoothedMapLightMultiply.r * dayNight.r,
                smoothedMapLightMultiply.g * dayNight.g,
                smoothedMapLightMultiply.b * dayNight.b,
                smoothedMapLightMultiply.a);
            layeredVisual?.SetMapLight(pushedMultiply, smoothedMapLightAdd);
            spriteVisual?.SetMapLight(pushedMultiply, smoothedMapLightAdd);
            lastPushedDayNightMultiply = dayNight;
            hasPushedMapLight = true;
        }

        private bool NeedsDayNightReapply() {
            return hasSmoothedMapLight &&
                (!hasPushedMapLight || !ColorApproximatelyEqual(GlobalActorDayNightMultiply, lastPushedDayNightMultiply));
        }

        private void ReapplyGlobalDayNightIfChanged() {
            if (NeedsDayNightReapply()) {
                PushMapLight();
            }
        }

        private static bool ColorApproximatelyEqual(Color a, Color b) {
            const float epsilon = 0.002f;
            return Mathf.Abs(a.r - b.r) < epsilon &&
                Mathf.Abs(a.g - b.g) < epsilon &&
                Mathf.Abs(a.b - b.b) < epsilon;
        }

        private bool TrySampleEnhancedActorTint(
            RoRuntimeMapController controller,
            Vector3 worldPosition,
            out Color multiply,
            out Color add
        ) {
            multiply = Color.white;
            add = Color.clear;
            float emissionInfluence = controller != null
                ? controller.EnhancedShadowSettings.mapEmissionLightInfluence
                : RoEnhancedShadowSettings.DefaultMapEmissionLightInfluence;

            // Lamp light goes to the additive channel: it is added on top of the (night-darkened) base, so a
            // lamp pools warm light onto a character in the dark. Lamps gate themselves to night via their
            // day/night intensity scale, so the additive contribution is naturally ~0 in daylight.
            TrySampleEnhancedLampAdd(
                controller?.EnhancedActorTintLights,
                worldPosition,
                emissionInfluence,
                out add);
            return true;
        }

        public static bool TrySampleEnhancedPointLights(
            IReadOnlyList<Light> lights,
            Vector3 worldPosition,
            float emissionLightInfluence,
            out Color multiply
        ) {
            multiply = Color.white;
            if (lights == null || lights.Count == 0) {
                return false;
            }

            Color weightedColor = Color.clear;
            float totalWeight = 0f;
            for (int i = 0; i < lights.Count; i++) {
                Light light = lights[i];
                if (light == null ||
                    !light.enabled ||
                    !light.gameObject.activeInHierarchy ||
                    light.type != LightType.Point) {
                    continue;
                }

                float range = Mathf.Max(0f, light.range);
                if (range <= 0.0001f) {
                    continue;
                }

                float distance = Vector3.Distance(worldPosition, light.transform.position);
                if (distance >= range) {
                    continue;
                }

                float proximity = Mathf.Clamp01(1f - distance / range);
                float attenuation = SmoothStep01(proximity);
                float weight = attenuation * Mathf.Max(0f, light.intensity);
                if (weight <= 0.0001f) {
                    continue;
                }

                Color color = light.color;
                weightedColor.r += Mathf.Max(0f, color.r) * weight;
                weightedColor.g += Mathf.Max(0f, color.g) * weight;
                weightedColor.b += Mathf.Max(0f, color.b) * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0.0001f) {
                return false;
            }

            float boost = Mathf.Min(
                totalWeight * Mathf.Max(0f, emissionLightInfluence) * EnhancedTintBoostScale,
                EnhancedTintMaxBoost);
            Color averageColor = new Color(
                Mathf.Clamp01(weightedColor.r / totalWeight),
                Mathf.Clamp01(weightedColor.g / totalWeight),
                Mathf.Clamp01(weightedColor.b / totalWeight),
                1f);
            multiply = ClampMapLightMultiply(new Color(
                1f + averageColor.r * boost,
                1f + averageColor.g * boost,
                1f + averageColor.b * boost,
                1f));
            return true;
        }

        // Additive variant: returns the lamp light to add on top of the sprite, so it survives the night
        // darkening (a lamp lights up a character standing in the dark). Naturally ~0 in daylight because
        // lamps gate their own intensity to night.
        public static bool TrySampleEnhancedLampAdd(
            IReadOnlyList<Light> lights,
            Vector3 worldPosition,
            float emissionLightInfluence,
            out Color add
        ) {
            add = Color.clear;
            if (lights == null || lights.Count == 0) {
                return false;
            }

            Color weightedColor = Color.clear;
            float totalWeight = 0f;
            for (int i = 0; i < lights.Count; i++) {
                Light light = lights[i];
                if (light == null ||
                    !light.enabled ||
                    !light.gameObject.activeInHierarchy ||
                    light.type != LightType.Point) {
                    continue;
                }

                float range = Mathf.Max(0f, light.range);
                if (range <= 0.0001f) {
                    continue;
                }

                float distance = Vector3.Distance(worldPosition, light.transform.position);
                if (distance >= range) {
                    continue;
                }

                float proximity = Mathf.Clamp01(1f - distance / range);
                float attenuation = SmoothStep01(proximity);
                float weight = attenuation * Mathf.Max(0f, light.intensity);
                if (weight <= 0.0001f) {
                    continue;
                }

                Color color = light.color;
                weightedColor.r += Mathf.Max(0f, color.r) * weight;
                weightedColor.g += Mathf.Max(0f, color.g) * weight;
                weightedColor.b += Mathf.Max(0f, color.b) * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0.0001f) {
                return false;
            }

            float glow = Mathf.Min(
                totalWeight * Mathf.Max(0f, emissionLightInfluence) * EnhancedLampAddScale,
                EnhancedLampAddMaxGlow);
            Color averageColor = new Color(
                Mathf.Clamp01(weightedColor.r / totalWeight),
                Mathf.Clamp01(weightedColor.g / totalWeight),
                Mathf.Clamp01(weightedColor.b / totalWeight),
                1f);
            add = new Color(
                averageColor.r * glow,
                averageColor.g * glow,
                averageColor.b * glow,
                1f);
            return true;
        }

        public static bool TrySampleShadowMap(GND.Mesh ground, Vector3 groundLocalPoint, out float shadow) {
            return TrySampleShadowMap(ground, groundLocalPoint.x, groundLocalPoint.z, out shadow);
        }

        public static bool TrySampleShadowMap(GND.Mesh ground, float mapX, float mapY, out float shadow) {
            shadow = 1f;
            if (ground?.shadowMap == null ||
                ground.width == 0 ||
                ground.height == 0 ||
                float.IsNaN(mapX) ||
                float.IsNaN(mapY) ||
                float.IsInfinity(mapX) ||
                float.IsInfinity(mapY)) {
                return false;
            }

            int shadowWidth = checked((int) ground.width * 8);
            int shadowHeight = checked((int) ground.height * 8);
            if (ground.shadowMap.Length < shadowWidth * shadowHeight) {
                return false;
            }

            int centerX = ResolveShadowPixel(mapX);
            int centerY = ResolveShadowPixel(mapY);
            if (centerX < 0 || centerY < 0 || centerX >= shadowWidth || centerY >= shadowHeight) {
                return false;
            }

            int total = 0;
            int count = 0;
            for (int y = -3; y < 3; y++) {
                int sampleY = Mathf.Clamp(centerY + y, 0, shadowHeight - 1);
                for (int x = -3; x < 3; x++) {
                    int sampleX = Mathf.Clamp(centerX + x, 0, shadowWidth - 1);
                    total += ground.shadowMap[sampleX + sampleY * shadowWidth];
                    count++;
                }
            }

            shadow = count > 0 ? Mathf.Clamp01(total / (count * 255f)) : 1f;
            return true;
        }

        public static bool TrySampleMapLight(
            Mesh mesh,
            int triangleIndex,
            Vector3 barycentric,
            Material material,
            out Color multiply,
            out Color add
        ) {
            multiply = Color.white;
            add = Color.clear;
            if (mesh == null || material == null || triangleIndex < 0) {
                return false;
            }

            if (!TryGetMeshLightData(mesh, out CachedMeshLightData meshData)) {
                return false;
            }

            Vector2[] lightUvs = meshData.LightUvs;
            int[] triangles = meshData.Triangles;
            int triangleOffset = triangleIndex * 3;
            if (lightUvs == null || triangles == null || triangleOffset + 2 >= triangles.Length) {
                return false;
            }

            int i0 = triangles[triangleOffset];
            int i1 = triangles[triangleOffset + 1];
            int i2 = triangles[triangleOffset + 2];
            if (i0 < 0 || i0 >= lightUvs.Length || i1 < 0 || i1 >= lightUvs.Length || i2 < 0 || i2 >= lightUvs.Length) {
                return false;
            }

            Vector2 uv = lightUvs[i0] * barycentric.x +
                lightUvs[i1] * barycentric.y +
                lightUvs[i2] * barycentric.z;
            return TrySampleMaterialLight(material, uv, out multiply, out add);
        }

        public static void ClearMeshLightDataCache() {
            MeshLightDataCache.Clear();
        }

        private static bool TryGetMeshLightData(Mesh mesh, out CachedMeshLightData meshData) {
            meshData = null;
            if (mesh == null) {
                return false;
            }

            int meshId = mesh.GetInstanceID();
            if (MeshLightDataCache.TryGetValue(meshId, out meshData)) {
                return meshData.IsValid;
            }

            Vector2[] lightUvs = mesh.uv2;
            int[] triangles = mesh.triangles;
            meshData = new CachedMeshLightData(lightUvs, triangles);
            MeshLightDataCache[meshId] = meshData;
            return meshData.IsValid;
        }

        public static bool TrySampleMaterialLight(Material material, Vector2 lightUv, out Color multiply, out Color add) {
            multiply = Color.white;
            add = Color.clear;
            if (material == null) {
                return false;
            }

            if (GetFloat(material, UseLightmapId, 1f) <= 0.5f) {
                return true;
            }

            Texture2D lightmap = material.HasProperty(LightmapId) ? material.GetTexture(LightmapId) as Texture2D : null;
            if (lightmap == null || lightmap == Texture2D.whiteTexture) {
                return true;
            }

            Color light;
            try {
                light = lightmap.GetPixelBilinear(Mathf.Clamp01(lightUv.x), Mathf.Clamp01(lightUv.y));
            } catch (UnityException) {
                return true;
            }

            multiply = ToMultiplyColor(light.a);
            return true;
        }

        private static Color ToMultiplyColor(float shadow) {
            shadow = Mathf.Clamp01(shadow);
            return new Color(
                shadow,
                shadow,
                shadow,
                1f
            );
        }

        private static Color ClampMapLightMultiply(Color color) {
            return ClampColor(color, Color.white, EnhancedTintMaxMultiply);
        }

        private static Color ClampColor(Color color, Color fallback, float maxRgb) {
            if (float.IsNaN(color.r) || float.IsNaN(color.g) || float.IsNaN(color.b) || float.IsNaN(color.a) ||
                float.IsInfinity(color.r) || float.IsInfinity(color.g) || float.IsInfinity(color.b) || float.IsInfinity(color.a)) {
                return fallback;
            }

            return new Color(
                Mathf.Clamp(color.r, 0f, maxRgb),
                Mathf.Clamp(color.g, 0f, maxRgb),
                Mathf.Clamp(color.b, 0f, maxRgb),
                Mathf.Clamp01(color.a)
            );
        }

        private static float SmoothStep01(float value) {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static Color LerpColor(Color from, Color to, float factor) {
            factor = Mathf.Clamp01(factor);
            return new Color(
                Mathf.LerpUnclamped(from.r, to.r, factor),
                Mathf.LerpUnclamped(from.g, to.g, factor),
                Mathf.LerpUnclamped(from.b, to.b, factor),
                Mathf.LerpUnclamped(from.a, to.a, factor)
            );
        }

        private static float ResolveExponentialSmoothingFactor(float deltaSeconds, float halfLifeSeconds) {
            if (deltaSeconds <= 0f) {
                return 0f;
            }

            halfLifeSeconds = Mathf.Max(0.0001f, halfLifeSeconds);
            return 1f - Mathf.Pow(0.5f, deltaSeconds / halfLifeSeconds);
        }

        private static int ResolveShadowPixel(float mapCoordinate) {
            int whole = Mathf.FloorToInt(mapCoordinate);
            float fraction = Mathf.Clamp01(mapCoordinate - whole);
            int tileBase = Mathf.FloorToInt(mapCoordinate / 2f) * 8;
            int cellOffset = (whole & 1) != 0 ? 4 : 0;
            return tileBase + Mathf.Min(cellOffset + Mathf.FloorToInt(fraction * 4f), 6);
        }

        private static float GetFloat(Material material, int propertyId, float fallback) {
            return material.HasProperty(propertyId) ? material.GetFloat(propertyId) : fallback;
        }

        private RoRuntimeMapController ResolveMapController() {
            if (mapController != null) {
                return mapController;
            }

            float now = Application.isPlaying ? Time.realtimeSinceStartup : 0f;
            if (Application.isPlaying && mapControllerLookupAttempted && now < nextMapControllerLookupTime) {
                return null;
            }

            mapControllerLookupAttempted = true;
            mapController = FindFirstObjectByType<RoRuntimeMapController>();
            if (mapController == null && Application.isPlaying) {
                nextMapControllerLookupTime = now + MapControllerLookupRetrySeconds;
            }

            return mapController;
        }

        private void InvalidateSamplePosition() {
            hasSampledWorldPosition = false;
            hasSmoothedMapLight = false;
            smoothedMapLightMultiply = Color.white;
            smoothedMapLightAdd = Color.clear;
            lastMapLightSmoothTime = 0f;
            nextSampleTime = 0f;
            hasPushedMapLight = false;
        }

        private bool ShouldRunActorTickAt(float now, Vector3 worldPosition, bool lateTick) {
            if (!lateTick) {
                return true;
            }

            if (!Application.isPlaying && !sampleInEditMode) {
                return false;
            }

            if (samplingMode == RoActorMapLightProbeSamplingMode.Disabled) {
                return false;
            }

            if (!ShouldSampleAt(now, worldPosition)) {
                // Wake the tick (cheaply, no ground sample) only when the global day/night multiply needs
                // re-applying, so idle actors still follow the time of day without per-frame sampling.
                return NeedsDayNightReapply();
            }

            return !Application.isPlaying || TryReserveFrameSample();
        }

        private bool ShouldSampleAt(float now, Vector3 worldPosition) {
            if (samplingMode == RoActorMapLightProbeSamplingMode.Disabled) {
                return false;
            }

            if (samplingMode == RoActorMapLightProbeSamplingMode.OnMoveOrInitial) {
                return !hasSampledWorldPosition ||
                    lastSampledLampEpoch != DayNightLampEpoch ||
                    Vector3.SqrMagnitude(worldPosition - lastSampledWorldPosition) > SampleMoveThresholdSqr;
            }

            if (Application.isPlaying && now < nextSampleTime) {
                return false;
            }

            if (hasSampledWorldPosition &&
                sampleBakedShadows &&
                Vector3.SqrMagnitude(worldPosition - lastSampledWorldPosition) <= SampleMoveThresholdSqr) {
                return false;
            }

            return true;
        }

        private float ResolveSampleInterval() {
            if (!sampleBakedShadows) {
                if (activeProbeCount >= HeavyCrowdProbeThreshold) {
                    return EnhancedHeavyCrowdSampleIntervalSeconds;
                }

                return activeProbeCount >= CrowdProbeThreshold
                    ? EnhancedCrowdSampleIntervalSeconds
                    : EnhancedSampleIntervalSeconds;
            }

            if (activeProbeCount >= HeavyCrowdProbeThreshold) {
                return HeavyCrowdSampleIntervalSeconds;
            }

            return activeProbeCount >= CrowdProbeThreshold
                ? CrowdSampleIntervalSeconds
                : SampleIntervalSeconds;
        }

        private static bool TryReserveFrameSample() {
            int frame = Time.frameCount;
            if (sampleBudgetFrame != frame) {
                sampleBudgetFrame = frame;
                samplesThisFrame = 0;
            }

            int budget = ResolveFrameSampleBudget();
            if (samplesThisFrame >= budget) {
                return false;
            }

            samplesThisFrame++;
            return true;
        }

        private static int ResolveFrameSampleBudget() {
            if (activeProbeCount >= HeavyCrowdProbeThreshold) {
                return HeavyCrowdSamplesPerFrame;
            }

            return activeProbeCount >= CrowdProbeThreshold
                ? CrowdSamplesPerFrame
                : SamplesPerFrame;
        }

        private sealed class CachedMeshLightData {
            public CachedMeshLightData(Vector2[] lightUvs, int[] triangles) {
                LightUvs = lightUvs;
                Triangles = triangles;
            }

            public Vector2[] LightUvs { get; }
            public int[] Triangles { get; }
            public bool IsValid => LightUvs != null && LightUvs.Length > 0 && Triangles != null && Triangles.Length > 0;
        }
    }
}
