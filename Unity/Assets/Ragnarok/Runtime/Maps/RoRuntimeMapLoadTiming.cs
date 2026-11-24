using System.Diagnostics;
using Ragnarok.Assets;
using UnityEngine;

namespace Ragnarok.Maps {
    internal static class RoMapTiming {
        public static long Timestamp() => Stopwatch.GetTimestamp();

        public static long ElapsedMilliseconds(long startTimestamp) =>
            (long) ((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);

        public static bool ShouldLog(long totalMilliseconds) =>
            Application.isEditor || UnityEngine.Debug.isDebugBuild || totalMilliseconds > 100;
    }

    internal sealed class RoMapDataLoadTiming {
        public long WaitForLoadLockMilliseconds { get; set; }
        public long WorldMilliseconds { get; set; }
        public long AltitudeMilliseconds { get; set; }
        public long GroundMilliseconds { get; set; }
        public long CompileGroundMilliseconds { get; set; }
        public long ModelSpecsMilliseconds { get; set; }
        public long ModelsMilliseconds { get; set; }
        public long TotalMilliseconds { get; set; }
        public int ModelDescriptorCount { get; set; }
        public int UniqueModelCount { get; set; }
        public int LoadedModelCount { get; set; }

        public string BuildSummary() =>
            $"total={TotalMilliseconds} ms, wait={WaitForLoadLockMilliseconds} ms, " +
            $"rsw={WorldMilliseconds} ms, gat={AltitudeMilliseconds} ms, gnd={GroundMilliseconds} ms, " +
            $"groundCompile={CompileGroundMilliseconds} ms, " +
            $"modelSpecs={ModelSpecsMilliseconds} ms/{UniqueModelCount} unique from {ModelDescriptorCount} descriptors, " +
            $"models={ModelsMilliseconds} ms/{LoadedModelCount} loaded";
    }

    internal sealed class RoRuntimeMapRenderTiming {
        public long TotalMilliseconds { get; set; }
        public long ActorLightProbeClearMilliseconds { get; set; }
        public long RootMilliseconds { get; set; }
        public long GroundMeshMilliseconds { get; set; }
        public long GroundObjectMilliseconds { get; set; }
        public long ModelsMilliseconds { get; set; }
        public long ModelBuildObjectsMilliseconds { get; set; }
        public long ModelMeshMilliseconds { get; set; }
        public long ModelTextureMilliseconds { get; set; }
        public long ModelShadowStateMilliseconds { get; set; }
        public long ModelStaticBatchMilliseconds { get; set; }
        public long ModelSummaryMilliseconds { get; set; }
        public long ModelLightingMilliseconds { get; set; }
        public long WaterMilliseconds { get; set; }
        public long PointLightsMilliseconds { get; set; }
        public long ModelLampLightsMilliseconds { get; set; }
        public long RootPlacementMilliseconds { get; set; }

        public string BuildSummary() =>
            $"total={TotalMilliseconds} ms, clearLightProbe={ActorLightProbeClearMilliseconds} ms, " +
            $"root={RootMilliseconds} ms, groundMesh={GroundMeshMilliseconds} ms, " +
            $"groundObject={GroundObjectMilliseconds} ms, models={BuildModelSummary()}, " +
            $"modelLighting={ModelLightingMilliseconds} ms, water={WaterMilliseconds} ms, " +
            $"pointLights={PointLightsMilliseconds} ms, modelLampLights={ModelLampLightsMilliseconds} ms, " +
            $"rootPlacement={RootPlacementMilliseconds} ms";

        private string BuildModelSummary() {
            if (ModelsMilliseconds <= 0 ||
                (ModelBuildObjectsMilliseconds == 0 &&
                 ModelShadowStateMilliseconds == 0 &&
                 ModelStaticBatchMilliseconds == 0 &&
                 ModelSummaryMilliseconds == 0)) {
                return $"{ModelsMilliseconds} ms";
            }

            return $"{ModelsMilliseconds} ms (build={ModelBuildObjectsMilliseconds} ms [meshes {ModelMeshMilliseconds}, textures {ModelTextureMilliseconds}], " +
                $"shadow={ModelShadowStateMilliseconds} ms, " +
                $"batch={ModelStaticBatchMilliseconds} ms, summary={ModelSummaryMilliseconds} ms)";
        }
    }

    internal sealed class RoMapShowTiming {
        public long TotalMilliseconds { get; set; }
        public long ClearMilliseconds { get; set; }
        public long RenderMilliseconds { get; set; }
        public long ShadowMilliseconds { get; set; }
        public long AudioMilliseconds { get; set; }
        public long GridMilliseconds { get; set; }
        public long CursorMilliseconds { get; set; }
        public long PlayerMilliseconds { get; set; }
        public long CameraMilliseconds { get; set; }
        public long CameraDefaultsMilliseconds { get; set; }
        public long FogMilliseconds { get; set; }
        public long SkyMilliseconds { get; set; }
        public long EffectsMilliseconds { get; set; }
        public long WeatherMilliseconds { get; set; }
        public long PortalsMilliseconds { get; set; }
        public long MapEffectsMilliseconds { get; set; }
        public RoRuntimeMapRenderTiming RenderTiming { get; set; }

        public string BuildSummary() =>
            $"total={TotalMilliseconds} ms, clear={ClearMilliseconds} ms, render={RenderMilliseconds} ms" +
            (RenderTiming != null ? $" ({RenderTiming.BuildSummary()})" : string.Empty) +
            $", shadow={ShadowMilliseconds} ms, audio={AudioMilliseconds} ms, grid={GridMilliseconds} ms, " +
            $"cursor={CursorMilliseconds} ms, player={PlayerMilliseconds} ms, camera={CameraMilliseconds} ms, " +
            $"cameraDefaults={CameraDefaultsMilliseconds} ms, fog={FogMilliseconds} ms, sky={SkyMilliseconds} ms, " +
            $"effects={EffectsMilliseconds} ms, weather={WeatherMilliseconds} ms, portals={PortalsMilliseconds} ms, " +
            $"mapEffects={MapEffectsMilliseconds} ms";
    }

    internal static class RoMapCacheTimingFormatter {
        public static string FormatDelta(RoRuntimeMapAssetCacheStats before, RoRuntimeMapAssetCacheStats after) =>
            $"mapAssets {Delta(after.MapAssetHits, before.MapAssetHits)}/{Delta(after.MapAssetMisses, before.MapAssetMisses)} hit/miss, " +
            $"groundData {Delta(after.CompiledGroundHits, before.CompiledGroundHits)}/{Delta(after.CompiledGroundMisses, before.CompiledGroundMisses)} hit/miss, " +
            $"groundMesh {Delta(after.GroundMeshHits, before.GroundMeshHits)}/{Delta(after.GroundMeshMisses, before.GroundMeshMisses)} hit/miss, " +
            $"rawRsm {Delta(after.RawModelHits, before.RawModelHits)}/{Delta(after.RawModelMisses, before.RawModelMisses)} hit/miss, " +
            $"compiledRsm {Delta(after.CompiledModelHits, before.CompiledModelHits)}/{Delta(after.CompiledModelMisses, before.CompiledModelMisses)} hit/miss, " +
            $"modelBuilds={Delta(after.CompiledModelBuilds, before.CompiledModelBuilds)} ({Delta(after.CompiledModelBuildMilliseconds, before.CompiledModelBuildMilliseconds)} ms), " +
            $"groundAtlas {Delta(after.GroundAtlasHits, before.GroundAtlasHits)}/{Delta(after.GroundAtlasMisses, before.GroundAtlasMisses)} hit/miss, " +
            $"waterMesh {Delta(after.WaterMeshHits, before.WaterMeshHits)}/{Delta(after.WaterMeshMisses, before.WaterMeshMisses)} hit/miss, " +
            $"waterTextures {Delta(after.WaterHits, before.WaterHits)}/{Delta(after.WaterMisses, before.WaterMisses)} hit/miss, " +
            $"entries {before.Entries}->{after.Entries}";

        public static string FormatModelMeshDelta(RoModelRendererCacheStats before, RoModelRendererCacheStats after) =>
            $"modelMeshes {Delta(after.MeshHits, before.MeshHits)}/{Delta(after.MeshMisses, before.MeshMisses)} hit/miss, " +
            $"builds={Delta(after.MeshBuilds, before.MeshBuilds)}, entries {before.MeshEntries}->{after.MeshEntries}, " +
            $"templates {Delta(after.TemplateHits, before.TemplateHits)}/{Delta(after.TemplateMisses, before.TemplateMisses)} hit/miss, " +
            $"templateBuilds={Delta(after.TemplateBuilds, before.TemplateBuilds)}, templateEntries {before.TemplateEntries}->{after.TemplateEntries}";

        private static long Delta(long after, long before) => after - before;
    }
}
