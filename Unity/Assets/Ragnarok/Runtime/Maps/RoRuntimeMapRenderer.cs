using System;
using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Effects;
using Ragnarok.Sprites;
using ROIO.Loaders;
using ROIO.Models.FileTypes;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;

namespace Ragnarok.Maps {
    public enum RoLightingPerformanceTier {
        ClassicFast,
        EnhancedBalanced,
        EnhancedUltra
    }

    public enum RoSpriteRenderingMode {
        MeshRenderers,
        InstancedExperimental
    }

    public enum RoPointerTargetingMode {
        AlphaExact,
        ColliderFirst,
        ColliderOnlyFast
    }

    public enum RoEffectCrowdBudgetMode {
        Off,
        Balanced,
        Aggressive
    }

    public enum RoCameraAntialiasingMode {
        None,
        Fxaa,
        SmaaLow,
        SmaaMedium,
        SmaaHigh
    }

    public readonly struct RoCameraAntialiasingRuntimeSettings {
        public RoCameraAntialiasingRuntimeSettings(
            bool requiresPostProcessing,
            int antialiasingValue,
            int antialiasingQualityValue,
            bool usesAntialiasingQuality
        ) {
            RequiresPostProcessing = requiresPostProcessing;
            AntialiasingValue = antialiasingValue;
            AntialiasingQualityValue = antialiasingQualityValue;
            UsesAntialiasingQuality = usesAntialiasingQuality;
        }

        public bool RequiresPostProcessing { get; }
        public int AntialiasingValue { get; }
        public int AntialiasingQualityValue { get; }
        public bool UsesAntialiasingQuality { get; }
    }

    public static class RoLightingPerformanceBudget {
        public static RoEnhancedShadowSettings Resolve(RoEnhancedShadowSettings source, RoLightingPerformanceTier tier) {
            RoEnhancedShadowSettings resolved = Copy(source ?? new RoEnhancedShadowSettings());
            switch (tier) {
                case RoLightingPerformanceTier.ClassicFast:
                    ApplyClassicFast(resolved);
                    break;
                case RoLightingPerformanceTier.EnhancedBalanced:
                    ApplyEnhancedBalanced(resolved);
                    break;
                case RoLightingPerformanceTier.EnhancedUltra:
                    break;
                default:
                    ApplyEnhancedBalanced(resolved);
                    break;
            }

            return resolved;
        }

        private static void ApplyClassicFast(RoEnhancedShadowSettings settings) {
            settings.enableRswPointLights = false;
            settings.maxRswPointLights = 0;
            settings.rswPointLightShadows = false;
            settings.enableModelLampPointLights = false;
            settings.maxModelLampPointLights = 0;
            settings.modelLampPointLightShadows = false;
            settings.additionalLightsPerObjectLimit = 0;
            settings.minimumShadowDistance = Mathf.Min(settings.minimumShadowDistance, 60f);
            settings.maximumShadowDistance = Mathf.Min(settings.maximumShadowDistance, 90f);
            settings.mainLightShadowmapResolution = Mathf.Min(settings.mainLightShadowmapResolution, 1024);
            settings.shadowAtlasResolution = Mathf.Min(settings.shadowAtlasResolution, 512);
            settings.shadowCascadeCount = 1;
        }

        private static void ApplyEnhancedBalanced(RoEnhancedShadowSettings settings) {
            settings.maxRswPointLights = Mathf.Min(settings.maxRswPointLights, 8);
            settings.rswPointLightShadows = false;
            settings.maxModelLampPointLights = Mathf.Min(settings.maxModelLampPointLights, 12);
            settings.modelLampPointLightRange = Mathf.Min(settings.modelLampPointLightRange, 14f);
            settings.modelLampPointLightIntensity = Mathf.Min(settings.modelLampPointLightIntensity, 6f);
            settings.modelLampPointLightShadows = false;
            settings.additionalLightsPerObjectLimit = Mathf.Min(settings.additionalLightsPerObjectLimit, 2);
            settings.minimumShadowDistance = Mathf.Min(settings.minimumShadowDistance, 80f);
            settings.maximumShadowDistance = Mathf.Min(settings.maximumShadowDistance, 140f);
            settings.mainLightShadowmapResolution = Mathf.Min(settings.mainLightShadowmapResolution, 2048);
            settings.shadowAtlasResolution = Mathf.Min(settings.shadowAtlasResolution, 1024);
            settings.shadowCascadeCount = 1;
        }

        private static RoEnhancedShadowSettings Copy(RoEnhancedShadowSettings source) {
            return new RoEnhancedShadowSettings {
                terrainRealtimeShadowStrength = source.terrainRealtimeShadowStrength,
                modelRealtimeShadowStrength = source.modelRealtimeShadowStrength,
                actorRealtimeShadowStrength = source.actorRealtimeShadowStrength,
                actorShadowCasterPullDistance = source.actorShadowCasterPullDistance,
                actorShadowCasterGroundClearance = source.actorShadowCasterGroundClearance,
                actorShadowCasterLiftPullScale = source.actorShadowCasterLiftPullScale,
                modelTopLightStrength = source.modelTopLightStrength,
                actorDirectionalLightInfluence = source.actorDirectionalLightInfluence,
                enableRswPointLights = source.enableRswPointLights,
                maxRswPointLights = source.maxRswPointLights,
                additionalLightsPerObjectLimit = source.additionalLightsPerObjectLimit,
                rswPointLightRangeScale = source.rswPointLightRangeScale,
                minimumRswPointLightRange = source.minimumRswPointLightRange,
                maximumRswPointLightRange = source.maximumRswPointLightRange,
                rswPointLightIntensity = source.rswPointLightIntensity,
                rswPointLightShadows = source.rswPointLightShadows,
                rswPointLightShadowStrength = source.rswPointLightShadowStrength,
                enableModelLampPointLights = source.enableModelLampPointLights,
                maxModelLampPointLights = source.maxModelLampPointLights,
                modelLampPointLightColor = source.modelLampPointLightColor,
                modelLampPointLightRange = source.modelLampPointLightRange,
                modelLampPointLightIntensity = source.modelLampPointLightIntensity,
                modelLampPointLightDayIntensityScale = source.modelLampPointLightDayIntensityScale,
                modelLampPointLightNightIntensityScale = source.modelLampPointLightNightIntensityScale,
                modelLampPointLightVerticalOffset = source.modelLampPointLightVerticalOffset,
                modelLampPointLightShadows = source.modelLampPointLightShadows,
                modelLampPointLightShadowStrength = source.modelLampPointLightShadowStrength,
                modelLampPointLightShadowBias = source.modelLampPointLightShadowBias,
                modelLampPointLightShadowNormalBias = source.modelLampPointLightShadowNormalBias,
                modelLampPointLightShadowNearPlane = source.modelLampPointLightShadowNearPlane,
                modelLampGlassCastsShadows = source.modelLampGlassCastsShadows,
                mapEmissionLightInfluence = source.mapEmissionLightInfluence,
                minimumShadowDistance = source.minimumShadowDistance,
                maximumShadowDistance = source.maximumShadowDistance,
                mainLightShadowmapResolution = source.mainLightShadowmapResolution,
                shadowAtlasResolution = source.shadowAtlasResolution,
                shadowCascadeCount = source.shadowCascadeCount
            };
        }
    }

    [Serializable]
    public sealed class RoRuntimeMapRenderSettings {
        public Material groundMaterial;
        public bool showModels = true;
        public bool showWater = true;
        public bool enableRswLighting = true;
        [Range(0f, 1f)] public float rswLightingStrength = 1f;
        public bool useGroundLightmapAlpha = true;
        public bool disableGroundLightmap;
        public bool disableGroundTint;
        public RoShadowRenderMode shadowMode = RoShadowRenderMode.Original;
        public RoCameraAntialiasingMode cameraAntialiasing = RoCameraAntialiasingMode.SmaaHigh;
        public RoEnhancedShadowSettings enhancedShadows = new RoEnhancedShadowSettings();
        public RoLightingPerformanceTier lightingPerformanceTier = RoLightingPerformanceTier.EnhancedUltra;
        public RoSpriteRenderingMode spriteRenderingMode = RoSpriteRenderingMode.MeshRenderers;
        public RoPointerTargetingMode pointerTargetingMode = RoPointerTargetingMode.ColliderFirst;
        public RoEffectCrowdBudgetMode effectCrowdBudgetMode = RoEffectCrowdBudgetMode.Balanced;
        public bool enableSpriteGeometryPrewarm = true;
        [Min(1)] public int spriteGeometryPrewarmPerFrame = 1;
        public bool enableEffectPoolPrewarm = true;
        [Min(0)] public int prewarmPrimitiveEffects = 4;
        [Min(0)] public int prewarmBillboardEffects = 4;
        [Min(0)] public int prewarmMapSmokeEffects = 4;
        [Min(0)] public int prewarmActorOverlayEffects = 4;
        [Min(0)] public int prewarmWeatherLayerEffects = 1;
        public bool clearModelCachesOnMapLoad;
        public bool combineStaticModelRenderers = true;
        public bool chunkStaticModelBatches = true;
        [Min(8f)] public float staticModelBatchChunkSize = 96f;
        [Min(256)] public int maxStaticModelBatchVertices = 120000;
        public bool enableLocalPlayerOcclusionFade = true;
        [Range(0f, 1f)] public float localPlayerOcclusionFadeAlpha = 0.42f;
        [Min(0.001f)] public float localPlayerOcclusionFadeInSeconds = 0.10f;
        [Min(0.001f)] public float localPlayerOcclusionFadeOutSeconds = 0.18f;
        [Min(0.01f)] public float localPlayerOcclusionProbeIntervalSeconds = 1f / 30f;
        [Min(0f)] public float localPlayerOcclusionScreenMarginPixels = 18f;
        [Min(1)] public int localPlayerOcclusionMaxActiveRenderers = 32;
        public RoModelFeatureDatabase modelFeatureDatabase;
        public FilterMode groundAtlasFilterMode = FilterMode.Bilinear;
        public float modelVerticalOffset;

        public bool UseEnhancedShadows => shadowMode == RoShadowRenderMode.Enhanced;
        public RoEnhancedShadowSettings EnhancedShadows => enhancedShadows ??= new RoEnhancedShadowSettings();
        public RoEnhancedShadowSettings ResolvedEnhancedShadows =>
            RoLightingPerformanceBudget.Resolve(EnhancedShadows, lightingPerformanceTier);
    }

    public sealed class RoRuntimeMapRenderResult {
        public GameObject Root { get; internal set; }
        public GameObject Ground { get; internal set; }
        public GameObject Models { get; internal set; }
        public GameObject Water { get; internal set; }
        public GameObject PointLights { get; internal set; }
        public GameObject ModelLampLights { get; internal set; }
        public Mesh GroundMesh { get; internal set; }
        public Collider GroundCollider { get; internal set; }
        public Bounds RenderedGroundLocalBounds { get; internal set; }
        internal RoRuntimeMapRenderTiming Timing { get; set; }
    }

    public static class RoRuntimeMapRenderer {
        private const string GeneratedMapPrefix = "RO Runtime Map - ";
        private static readonly ProfilerMarker ShowMapMarker = new ProfilerMarker("Map.ShowMap");
        private static readonly ProfilerMarker BuildModelsMarker = new ProfilerMarker("Map.BuildModels");
        private static readonly ProfilerMarker ClearModelCachesMarker = new ProfilerMarker("Map.Models.ClearCaches");
        private static readonly ProfilerMarker ModelShadowStateMarker = new ProfilerMarker("Map.Models.ShadowState");
        private static readonly ProfilerMarker StaticBatchMarker = new ProfilerMarker("Map.Models.StaticBatch");
        private static readonly List<MeshRenderer> ModelShadowRendererScratch = new List<MeshRenderer>(512);
        private static readonly List<Material> ModelShadowMaterialScratch = new List<Material>(8);

        public static RoRuntimeMapRenderResult Build(
            Transform parent,
            AsyncMapLoader.GameMapData mapData,
            RoAssetDatabase assets,
            RoRuntimeMapRenderSettings settings = null,
            UnityEngine.Object logContext = null
        ) {
            using (ShowMapMarker.Auto()) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            long totalStart = RoMapTiming.Timestamp();
            var timing = new RoRuntimeMapRenderTiming();
            settings ??= new RoRuntimeMapRenderSettings();
            if (settings.showModels) {
                // the model textures read, decoded and keyed on worker threads while the ground is built here
                RoModelRenderer.BeginTexturePrefetch(mapData.CompiledModels, assets, settings.clearModelCachesOnMapLoad || RoModelRenderer.ForceClearCachesOnMapLoad);
            }

            long phaseStart = RoMapTiming.Timestamp();
            RoActorMapLightProbe.ClearMeshLightDataCache();
            timing.ActorLightProbeClearMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            phaseStart = RoMapTiming.Timestamp();
            var root = new GameObject($"{GeneratedMapPrefix}{mapData.Name}");
            if (parent != null) {
                root.transform.SetParent(parent, false);
            }
            timing.RootMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            phaseStart = RoMapTiming.Timestamp();
            Mesh groundMesh = RoRuntimeMapAssetCache.GetOrBuildGroundMesh(
                assets,
                mapData.CompiledGround,
                RoGroundMeshBuilder.TopDiagonalMode,
                () => RoGroundMeshBuilder.Build(mapData.CompiledGround));
            Bounds renderedGroundLocalBounds = RoMapCoordinateSystem.RenderedGroundLocalBounds(groundMesh);
            timing.GroundMeshMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            phaseStart = RoMapTiming.Timestamp();
            GameObject ground = CreateGround(root.transform, groundMesh, mapData.CompiledGround, mapData.World.light, assets, settings);
            timing.GroundObjectMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            GameObject models = null;
            if (settings.showModels) {
                phaseStart = RoMapTiming.Timestamp();
                models = CreateModels(root.transform, mapData, assets, settings, timing, logContext);
                timing.ModelsMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

                phaseStart = RoMapTiming.Timestamp();
                RoMapLighting.ApplyToRenderers(
                    models,
                    RoMapLighting.FromWorld(mapData.World.light),
                    settings.enableRswLighting,
                    ShouldUseBakedLightmapShadows(settings),
                    settings.rswLightingStrength
                );
                timing.ModelLightingMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            }

            GameObject water = null;
            if (settings.showWater) {
                phaseStart = RoMapTiming.Timestamp();
                water = CreateWater(root.transform, mapData.CompiledGround, mapData.World.water, assets);
                timing.WaterMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            }

            phaseStart = RoMapTiming.Timestamp();
            GameObject pointLights = CreateEnhancedPointLights(root.transform, mapData, settings);
            timing.PointLightsMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            phaseStart = RoMapTiming.Timestamp();
            GameObject modelLampLights = CreateEnhancedModelLampPointLights(models, settings);
            if (ShouldCreateEnhancedPointLights(settings) && mapData.World?.effects != null && mapData.CompiledGround != null) {
                // the map's fires (RSW torch / flame effect records) as lights: the effect draws the flame, this lights the cave
                RoMapLighting.BuildFireLights(mapData.World.effects, mapData.CompiledGround)?.transform.SetParent(root.transform, false);
            }
            timing.ModelLampLightsMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);

            phaseStart = RoMapTiming.Timestamp();
            root.transform.localPosition = RoMapCoordinateSystem.MapRootLocalPosition(renderedGroundLocalBounds);
            timing.RootPlacementMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
            timing.TotalMilliseconds = RoMapTiming.ElapsedMilliseconds(totalStart);

            return new RoRuntimeMapRenderResult {
                Root = root,
                Ground = ground,
                Models = models,
                Water = water,
                PointLights = pointLights,
                ModelLampLights = modelLampLights,
                GroundMesh = groundMesh,
                GroundCollider = ground != null ? ground.FindComponent<Collider>() : null,
                RenderedGroundLocalBounds = renderedGroundLocalBounds,
                Timing = timing
            };
            }
        }

        private static GameObject CreateGround(
            Transform parent,
            Mesh mesh,
            GND.Mesh compiledGround,
            RSW.LightInfo lightInfo,
            RoAssetDatabase assets,
            RoRuntimeMapRenderSettings settings
        ) {
            var groundObject = new GameObject("RO Ground");
            groundObject.transform.SetParent(parent, false);
            groundObject.transform.localPosition = RoMapCoordinateSystem.GroundLocalPosition;
            groundObject.transform.localScale = RoMapCoordinateSystem.GroundLocalScale;

            var filter = groundObject.AddComponent<MeshFilter>();
            var renderer = groundObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;

            Material material = settings.groundMaterial != null
                ? new Material(settings.groundMaterial)
                : CreateGroundMaterial(compiledGround, lightInfo, assets, settings);
            renderer.sharedMaterial = material;
            // the material and its light / tint maps are made per map load (the atlas and the mesh are cached, not owned)
            RoOwnedRuntimeAssets.Own(groundObject, material);
            OwnGroundMap(groundObject, material, "_Lightmap");
            OwnGroundMap(groundObject, material, "_Tintmap");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = settings.UseEnhancedShadows;
            if (ShouldCreateEnhancedModelLampPointLights(settings)) {
                RoMapLighting.EnableModelLampRenderingLayer(renderer);
            }

            ApplyGroundMaterialRuntimeState(material, lightInfo, settings);

            RoGroundMeshBuilder.AddCollider(groundObject, mesh);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) {
                groundObject.layer = groundLayer;
            }

            return groundObject;
        }

        private static GameObject CreateModels(
            Transform parent,
            AsyncMapLoader.GameMapData mapData,
            RoAssetDatabase assets,
            RoRuntimeMapRenderSettings settings,
            RoRuntimeMapRenderTiming timing,
            UnityEngine.Object logContext
        ) {
            using (BuildModelsMarker.Auto()) {
                RoModelRenderer.UvDebugMode = RoModelRenderer.ModelUvDebugMode.Default;
                RoModelRenderer.TextureWrapDebugMode = RoModelRenderer.ModelTextureWrapDebugMode.Repeat;
                RoModelRenderer.DisableBlackKey = false;
                if (settings.clearModelCachesOnMapLoad || RoModelRenderer.ForceClearCachesOnMapLoad) {
                    using (ClearModelCachesMarker.Auto()) {
                        RoModelRenderer.ClearCaches();
                        assets.ClearTextureCache();
                    }
                }

                long phaseStart = RoMapTiming.Timestamp();
                RoModelRenderer.BuildMeshTicks = 0;
                RoModelRenderer.BuildTextureTicks = 0;
                GameObject models = RoModelRenderer.BuildModels(
                    mapData.CompiledModels,
                    0,
                    assets,
                    settings.shadowMode,
                    settings.ResolvedEnhancedShadows);
                if (timing != null) {
                    timing.ModelBuildObjectsMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
                    timing.ModelMeshMilliseconds = RoModelRenderer.BuildMeshTicks * 1000 / System.Diagnostics.Stopwatch.Frequency;
                    timing.ModelTextureMilliseconds = RoModelRenderer.BuildTextureTicks * 1000 / System.Diagnostics.Stopwatch.Frequency;
                }

                models.transform.SetParent(parent, false);
                models.transform.localPosition =
                    RoMapCoordinateSystem.ModelRootLocalPosition(mapData.CompiledGround) +
                    new Vector3(0f, settings.modelVerticalOffset, 0f);

                phaseStart = RoMapTiming.Timestamp();
                using (ModelShadowStateMarker.Auto()) ApplyModelShadowRuntimeState(models, settings);
                if (timing != null) {
                    timing.ModelShadowStateMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
                }

                phaseStart = RoMapTiming.Timestamp();
                RoStaticModelBatchResult batchResult;
                using (StaticBatchMarker.Auto()) batchResult = RoStaticModelBatcher.Combine(models, settings);
                if (timing != null) {
                    timing.ModelStaticBatchMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
                }

                if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                    phaseStart = RoMapTiming.Timestamp();
                    string materialSummary = RoModelRenderer.BuildMaterialSummary(models);
                    if (timing != null) {
                        timing.ModelSummaryMilliseconds = RoMapTiming.ElapsedMilliseconds(phaseStart);
                    }

                    RoRuntimeDiagnostics.IncrementDebugLogLine();
                    Debug.Log(
                        $"Loaded RO runtime models: {mapData.World.modelDescriptors.Count} descriptors, " +
                        $"{RoModelRenderer.CountInstances(mapData.CompiledModels)} instances, {materialSummary}, " +
                        $"static batches={batchResult.BatchedRenderers} from {batchResult.SourceRenderers} renderers, " +
                        $"mode={(settings.chunkStaticModelBatches ? $"chunked/{Mathf.Max(8f, settings.staticModelBatchChunkSize):0.#}" : "whole-map")}, " +
                        $"groups={batchResult.BatchGroups}, materials={batchResult.MaterialBuckets}, " +
                        $"largestGroup={batchResult.LargestGroupSize}, skipped={batchResult.SkippedRenderers}.",
                        logContext
                    );
                } else if (timing != null) {
                    timing.ModelSummaryMilliseconds = 0;
                }
                return models;
            }
        }

        private static GameObject CreateWater(
            Transform parent,
            GND.Mesh compiledGround,
            RSW.WaterInfo waterInfo,
            RoAssetDatabase assets
        ) {
            Mesh mesh = RoRuntimeMapAssetCache.GetOrBuildWaterMesh(
                assets,
                compiledGround,
                () => RoWaterMeshBuilder.Build(compiledGround));
            if (mesh == null) {
                return null;
            }

            var water = new GameObject("RO Water");
            water.transform.SetParent(parent, false);
            water.transform.localScale = RoMapCoordinateSystem.WaterLocalScale;

            var filter = water.AddComponent<MeshFilter>();
            var renderer = water.AddComponent<MeshRenderer>();
            Material material = CreateWaterMaterial();
            RoOwnedRuntimeAssets.Own(water, material);

            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;

            var animator = water.AddComponent<RoWaterAnimator>();
            animator.Initialize(waterInfo, material, assets);
            return water;
        }

        private static Material CreateGroundMaterial(
            GND.Mesh compiledGround,
            RSW.LightInfo lightInfo,
            RoAssetDatabase assets,
            RoRuntimeMapRenderSettings settings
        ) {
            Shader shader = Shader.Find(settings.UseEnhancedShadows
                ? "Ragnarok/RO Ground Enhanced"
                : "Ragnarok/RO Ground Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) {
                name = "RO Runtime Ground Material",
                color = Color.white,
                renderQueue = RoMapRenderQueues.Ground
            };

            Texture2D atlas = RoGroundTextureAtlasBuilder.Build(compiledGround, assets, settings.groundAtlasFilterMode);
            material.mainTexture = atlas;
            SetTextureIfPropertyExists(material, "_BaseMap", atlas);
            SetTextureIfPropertyExists(material, "_MainTex", atlas);
            SetTextureIfPropertyExists(material, "_Lightmap", CreateRoImageTexture(compiledGround.lightmap, "RO Ground Lightmap", FilterMode.Bilinear, true));
            SetTextureIfPropertyExists(material, "_Tintmap", CreateRoImageTexture(compiledGround.tileColor, "RO Ground Tintmap", FilterMode.Bilinear, true));
            SetFloatIfPropertyExists(material, "_Cutoff", 0.01f);
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Back);
            ApplyGroundMaterialRuntimeState(material, lightInfo, settings);
            return material;
        }

        private static void ApplyGroundMaterialRuntimeState(Material material, RSW.LightInfo lightInfo, RoRuntimeMapRenderSettings settings) {
            RoEnhancedShadowSettings enhanced = settings.ResolvedEnhancedShadows;
            SetFloatIfPropertyExists(material, "_UseLightmap", settings.disableGroundLightmap ? 0f : 1f);
            SetFloatIfPropertyExists(material, "_UseTint", settings.disableGroundTint ? 0f : 1f);
            RoMapLighting.ApplyToMaterial(
                material,
                RoMapLighting.FromWorld(lightInfo),
                settings.enableRswLighting,
                ShouldUseBakedLightmapShadows(settings),
                settings.rswLightingStrength
            );
            SetFloatIfPropertyExists(material, "_RoReceiveRealtimeShadows", settings.UseEnhancedShadows ? 1f : 0f);
            SetFloatIfPropertyExists(
                material,
                "_RoRealtimeShadowStrength",
                Mathf.Clamp01(enhanced.terrainRealtimeShadowStrength));
            SetFloatIfPropertyExists(
                material,
                "_RoEmissionLightInfluence",
                Mathf.Max(0f, enhanced.mapEmissionLightInfluence));
            SetFloatIfPropertyExists(material, "_RoRainWetness", 0f);
            SetFloatIfPropertyExists(material, "_RoRainDarkening", RoRainWeatherSettings.DefaultDarkening);
            SetFloatIfPropertyExists(material, "_RoRainHighlight", RoRainWeatherSettings.DefaultHighlight);
            SetFloatIfPropertyExists(material, "_RoRainPuddleStrength", RoRainWeatherSettings.DefaultPuddleStrength);
        }

        public static bool ShouldUseBakedLightmapShadows(RoRuntimeMapRenderSettings settings) {
            return settings != null &&
                settings.useGroundLightmapAlpha &&
                !settings.UseEnhancedShadows;
        }

        private static void ApplyModelShadowRuntimeState(GameObject models, RoRuntimeMapRenderSettings settings) {
            if (models == null) {
                return;
            }

            RoEnhancedShadowSettings enhancedSettings = settings.ResolvedEnhancedShadows;
            ModelShadowRendererScratch.Clear();
            models.GetComponentsInChildren(true, ModelShadowRendererScratch);
            for (int i = 0; i < ModelShadowRendererScratch.Count; i++) {
                MeshRenderer renderer = ModelShadowRendererScratch[i];
                if (renderer == null) {
                    continue;
                }

                bool enhanced = settings.UseEnhancedShadows;
                if (enhanced) {
                    bool twoSided = false;
                    RoModelSurfaceInfo surface = renderer.FindComponent<RoModelSurfaceInfo>();
                    if (surface != null) {
                        twoSided = surface.TwoSided || surface.MaterialMode == RoModelRenderer.MaterialMode.AlphaBlend;
                    }

                    renderer.shadowCastingMode = twoSided ? ShadowCastingMode.TwoSided : ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                } else {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                renderer.GetSharedMaterials(ModelShadowMaterialScratch);
                for (int j = 0; j < ModelShadowMaterialScratch.Count; j++) {
                    Material material = ModelShadowMaterialScratch[j];
                    SetFloatIfPropertyExists(material, "_RoReceiveRealtimeShadows", enhanced ? 1f : 0f);
                    SetFloatIfPropertyExists(
                        material,
                        "_RoRealtimeShadowStrength",
                        Mathf.Clamp01(enhancedSettings.modelRealtimeShadowStrength));
                    SetFloatIfPropertyExists(
                        material,
                        "_RoModelTopLightStrength",
                        Mathf.Clamp01(enhancedSettings.modelTopLightStrength));
                    SetFloatIfPropertyExists(
                        material,
                        "_RoEmissionLightInfluence",
                        Mathf.Max(0f, enhancedSettings.mapEmissionLightInfluence));
                }

                ModelShadowMaterialScratch.Clear();
            }

            ModelShadowRendererScratch.Clear();
        }

        public static bool ShouldCreateEnhancedPointLights(RoRuntimeMapRenderSettings settings) {
            return settings != null &&
                settings.UseEnhancedShadows &&
                settings.ResolvedEnhancedShadows.enableRswPointLights &&
                settings.ResolvedEnhancedShadows.maxRswPointLights > 0;
        }

        public static bool ShouldCreateEnhancedModelLampPointLights(RoRuntimeMapRenderSettings settings) {
            return settings != null &&
                settings.UseEnhancedShadows &&
                settings.ResolvedEnhancedShadows.enableModelLampPointLights &&
                settings.ResolvedEnhancedShadows.maxModelLampPointLights > 0;
        }

        private static GameObject CreateEnhancedPointLights(
            Transform parent,
            AsyncMapLoader.GameMapData mapData,
            RoRuntimeMapRenderSettings settings
        ) {
            if (!ShouldCreateEnhancedPointLights(settings) ||
                mapData.World?.lights == null ||
                mapData.CompiledGround == null) {
                return null;
            }

            RoEnhancedShadowSettings enhanced = settings.ResolvedEnhancedShadows;
            GameObject pointLights = RoMapLighting.BuildPointLights(
                mapData.World.lights,
                mapData.CompiledGround,
                Mathf.Clamp(enhanced.maxRswPointLights, 0, 64),
                Mathf.Max(0f, enhanced.rswPointLightRangeScale),
                0f,
                Mathf.Max(0f, enhanced.minimumRswPointLightRange),
                Mathf.Max(0f, enhanced.maximumRswPointLightRange),
                useFixedIntensity: true,
                fixedIntensity: enhanced.rswPointLightIntensity,
                castShadows: enhanced.rswPointLightShadows,
                shadowStrength: enhanced.rswPointLightShadowStrength
            );

            if (pointLights == null) {
                return null;
            }

            pointLights.transform.SetParent(parent, false);
            return pointLights;
        }

        private static GameObject CreateEnhancedModelLampPointLights(
            GameObject models,
            RoRuntimeMapRenderSettings settings
        ) {
            if (!ShouldCreateEnhancedModelLampPointLights(settings) || models == null) {
                return null;
            }

            RoEnhancedShadowSettings enhanced = settings.ResolvedEnhancedShadows;
            return RoMapLighting.BuildModelLampPointLights(
                models,
                Mathf.Clamp(enhanced.maxModelLampPointLights, 0, 128),
                enhanced.modelLampPointLightColor,
                Mathf.Max(0f, enhanced.modelLampPointLightRange),
                Mathf.Max(0f, enhanced.modelLampPointLightIntensity),
                castShadows: enhanced.modelLampPointLightShadows,
                shadowStrength: enhanced.modelLampPointLightShadowStrength,
                shadowBias: enhanced.modelLampPointLightShadowBias,
                shadowNormalBias: enhanced.modelLampPointLightShadowNormalBias,
                shadowNearPlane: enhanced.modelLampPointLightShadowNearPlane,
                lampGlassCastsShadows: enhanced.modelLampGlassCastsShadows,
                verticalOffset: enhanced.modelLampPointLightVerticalOffset,
                dayIntensityScale: Mathf.Max(0f, enhanced.modelLampPointLightDayIntensityScale),
                nightIntensityScale: Mathf.Max(0f, enhanced.modelLampPointLightNightIntensityScale),
                modelFeatureDatabase: settings.modelFeatureDatabase
            );
        }

        private static void OwnGroundMap(GameObject groundObject, Material material, string property) {
            if (!material.HasProperty(property)) {
                return;
            }

            Texture texture = material.GetTexture(property);
            if (texture != null && texture != Texture2D.whiteTexture && texture.name.StartsWith("RO Ground ", System.StringComparison.Ordinal)) {
                RoOwnedRuntimeAssets.Own(groundObject, texture);
            }
        }

        private static Texture2D CreateRoImageTexture(RoImage image, string name, FilterMode filterMode, bool linearData) {
            if (image.data == null || image.data.Length == 0 || image.width <= 0 || image.height <= 0) {
                return Texture2D.whiteTexture;
            }

            Texture2D texture = image.ConvertToTexture2D(linearData);
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = filterMode;
            return texture;
        }

        private static Material CreateWaterMaterial() {
            Shader shader = Shader.Find("Ragnarok/RO Water Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) {
                name = "RO Runtime Water Material",
                color = new Color(1f, 1f, 1f, 0.78f),
                renderQueue = RoMapRenderQueues.Water
            };
            SetColorIfPropertyExists(material, "_BaseColor", material.color);
            SetColorIfPropertyExists(material, "_Color", material.color);
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Off);
            SetFloatIfPropertyExists(material, "_ZWrite", 0f);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            SetFloatIfPropertyExists(material, "_WaterOpacity", RoWaterAnimator.DefaultWaterOpacity);
            SetTransparentIfSupported(material);
            return material;
        }

        private static void SetTransparentIfSupported(Material material) {
            if (material.HasProperty("_Surface")) {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_AlphaClip")) {
                material.SetFloat("_AlphaClip", 0f);
            }

            SetFloatIfPropertyExists(material, "_UseAlphaClip", 0f);
            SetFloatIfPropertyExists(material, "_ZWrite", 0f);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = RoMapRenderQueues.Water;
        }

        private static void SetTextureIfPropertyExists(Material material, string property, Texture texture) {
            if (material.HasProperty(property)) {
                material.SetTexture(property, texture);
            }
        }

        private static void SetFloatIfPropertyExists(Material material, string property, float value) {
            if (material.HasProperty(property)) {
                material.SetFloat(property, value);
            }
        }

        private static void SetColorIfPropertyExists(Material material, string property, Color color) {
            if (material.HasProperty(property)) {
                material.SetColor(property, color);
            }
        }
    }
}
