using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Maps {
    public readonly struct RoStaticModelBatchResult {
        public RoStaticModelBatchResult(
            int sourceRenderers,
            int batchedRenderers,
            int skippedRenderers,
            int batchGroups,
            int materialBuckets,
            int largestGroupSize
        ) {
            SourceRenderers = sourceRenderers;
            BatchedRenderers = batchedRenderers;
            SkippedRenderers = skippedRenderers;
            BatchGroups = batchGroups;
            MaterialBuckets = materialBuckets;
            LargestGroupSize = largestGroupSize;
        }

        public int SourceRenderers { get; }
        public int BatchedRenderers { get; }
        public int SkippedRenderers { get; }
        public int BatchGroups { get; }
        public int MaterialBuckets { get; }
        public int LargestGroupSize { get; }
    }

    internal static class RoStaticModelBatcher {
        private const int DefaultMaxBatchVertices = 120000;
        private const float DefaultChunkSize = 96f;
        private static readonly List<MeshRenderer> RendererScratch = new List<MeshRenderer>(2048);
        private static readonly HashSet<int> MaterialBucketScratch = new HashSet<int>();
        private static readonly Unity.Profiling.ProfilerMarker CollectMarker = new Unity.Profiling.ProfilerMarker("Map.Batch.Collect");
        private static readonly Unity.Profiling.ProfilerMarker BuildMarker = new Unity.Profiling.ProfilerMarker("Map.Batch.Build");
        private static readonly Unity.Profiling.ProfilerMarker FadeMarker = new Unity.Profiling.ProfilerMarker("Map.Batch.FadeMembers");

        public static RoStaticModelBatchResult LastResult { get; private set; }

        public static RoStaticModelBatchResult Combine(GameObject modelRoot, RoRuntimeMapRenderSettings settings) {
            RoStaticModelBatchChunkStats.ResetStats();
            LastResult = default;
            if (modelRoot == null || settings == null || !settings.combineStaticModelRenderers) {
                return default;
            }

            RendererScratch.Clear();
            MaterialBucketScratch.Clear();
            modelRoot.GetComponentsInChildren(true, RendererScratch);
            if (RendererScratch.Count == 0) {
                return default;
            }

            Transform rootTransform = modelRoot.transform;
            Matrix4x4 rootWorldToLocal = rootTransform.worldToLocalMatrix;
            bool useChunks = settings.chunkStaticModelBatches;
            float chunkSize = Mathf.Max(8f, settings.staticModelBatchChunkSize > 0f ? settings.staticModelBatchChunkSize : DefaultChunkSize);
            int maxBatchVertices = Mathf.Max(256, settings.maxStaticModelBatchVertices > 0 ? settings.maxStaticModelBatchVertices : DefaultMaxBatchVertices);
            bool preserveModelLampSurfaces = RoRuntimeMapRenderer.ShouldCreateEnhancedModelLampPointLights(settings);
            var batches = new Dictionary<BatchKey, BatchBuildState>();
            int sourceCount = 0;
            int skippedCount = 0;

            Unity.Profiling.ProfilerMarker.AutoScope collectScope = CollectMarker.Auto();
            for (int i = 0; i < RendererScratch.Count; i++) {
                MeshRenderer renderer = RendererScratch[i];
                if (!TryCreateSource(renderer, rootTransform, rootWorldToLocal, useChunks, chunkSize, preserveModelLampSurfaces, settings, out BatchKey key, out BatchSource source)) {
                    skippedCount++;
                    continue;
                }

                sourceCount++;
                MaterialBucketScratch.Add(source.Material.GetInstanceID());
                if (!batches.TryGetValue(key, out BatchBuildState batch)) {
                    batch = new BatchBuildState(source.Material, source.ShadowCastingMode, source.ReceiveShadows, source.RenderingLayerMask);
                    batches.Add(key, batch);
                }

                batch.Sources.Add(source);
            }

            collectScope.Dispose();

            RendererScratch.Clear();
            if (sourceCount == 0 || batches.Count == 0) {
                MaterialBucketScratch.Clear();
                LastResult = new RoStaticModelBatchResult(0, 0, skippedCount, 0, 0, 0);
                return LastResult;
            }

            var batchRoot = new GameObject("RO Static Model Batches");
            batchRoot.transform.SetParent(rootTransform, false);
            RoStaticModelBatchSet batchSet = batchRoot.AddComponent<RoStaticModelBatchSet>();
            int batchRendererCount = 0;
            int largestGroupSize = 0;
            foreach (KeyValuePair<BatchKey, BatchBuildState> entry in batches) {
                largestGroupSize = Mathf.Max(largestGroupSize, entry.Value.Sources.Count);
                using (BuildMarker.Auto()) batchRendererCount += BuildBatchRenderers(batchRoot.transform, entry.Value, maxBatchVertices, batchRendererCount, batchSet);
            }

            foreach (BatchBuildState batch in batches.Values) {
                for (int i = 0; i < batch.Sources.Count; i++) {
                    if (batch.Sources[i].Renderer != null) {
                        batch.Sources[i].Renderer.enabled = false;
                    }
                }
            }

            int materialBucketCount = MaterialBucketScratch.Count;
            MaterialBucketScratch.Clear();
            LastResult = new RoStaticModelBatchResult(
                sourceCount,
                batchRendererCount,
                skippedCount,
                batches.Count,
                materialBucketCount,
                largestGroupSize);
            return LastResult;
        }

        private static bool TryCreateSource(
            MeshRenderer renderer,
            Transform rootTransform,
            Matrix4x4 rootWorldToLocal,
            bool useChunks,
            float chunkSize,
            bool preserveModelLampSurfaces,
            RoRuntimeMapRenderSettings settings,
            out BatchKey key,
            out BatchSource source
        ) {
            key = default;
            source = default;
            if (renderer == null || !renderer.enabled || renderer.transform == rootTransform) {
                return false;
            }

            RoModelSurfaceInfo surface = renderer.FindComponent<RoModelSurfaceInfo>();
            if (surface == null ||
                surface.MaterialMode == RoModelRenderer.MaterialMode.AlphaBlend ||
                renderer.GetComponentInParent<RoModelAnimationController>() != null ||
                HasPreservedFeature(surface, settings.modelFeatureDatabase, preserveModelLampSurfaces)) {
                return false;
            }

            // a model that can hide the player is batched too: the fader takes its range out of the batch while it fades
            bool fadeCandidate = settings.enableLocalPlayerOcclusionFade && RoLocalPlayerOcclusionFader.IsFadeCandidate(surface, renderer);

            MeshFilter filter = renderer.FindComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            Material material = renderer.sharedMaterial;
            if (mesh == null || mesh.vertexCount == 0 || material == null) {
                return false;
            }

            Matrix4x4 sourceTransform = rootWorldToLocal * renderer.localToWorldMatrix;
            int chunkX = 0;
            int chunkZ = 0;
            if (useChunks) {
                Vector3 localCenter = sourceTransform.MultiplyPoint3x4(mesh.bounds.center);
                chunkX = Mathf.FloorToInt(localCenter.x / chunkSize);
                chunkZ = Mathf.FloorToInt(localCenter.z / chunkSize);
            }

            key = new BatchKey(
                material.GetInstanceID(),
                chunkX,
                chunkZ,
                renderer.shadowCastingMode,
                renderer.receiveShadows,
                renderer.renderingLayerMask);
            source = new BatchSource(
                renderer,
                mesh,
                material,
                sourceTransform,
                renderer.shadowCastingMode,
                renderer.receiveShadows,
                renderer.renderingLayerMask,
                fadeCandidate);
            return true;
        }

        private static bool HasPreservedFeature(
            RoModelSurfaceInfo surface,
            RoModelFeatureDatabase featureDatabase,
            bool preserveModelLampSurfaces
        ) {
            if (featureDatabase == null) {
                return false;
            }

            return preserveModelLampSurfaces &&
                featureDatabase.TryFindPointLightEntry(surface, out RoModelFeatureDatabase.ModelPointLightEntry lightEntry) &&
                lightEntry != null &&
                lightEntry.enabled;
        }

        private static int BuildBatchRenderers(Transform parent, BatchBuildState batch, int maxBatchVertices, int batchStartIndex, RoStaticModelBatchSet batchSet) {
            int created = 0;
            int sourceIndex = 0;
            while (sourceIndex < batch.Sources.Count) {
                int vertexCount = 0;
                int start = sourceIndex;
                while (sourceIndex < batch.Sources.Count) {
                    int sourceVertices = batch.Sources[sourceIndex].Mesh.vertexCount;
                    if (sourceIndex > start && vertexCount + sourceVertices > maxBatchVertices) {
                        break;
                    }

                    vertexCount += sourceVertices;
                    sourceIndex++;
                }

                int count = sourceIndex - start;
                if (count <= 0) {
                    break;
                }

                BuildBatchRenderer(parent, batch, start, count, batchStartIndex + created, batchSet);
                created++;
            }

            return created;
        }

        private static void BuildBatchRenderer(Transform parent, BatchBuildState batch, int start, int count, int batchIndex, RoStaticModelBatchSet batchSet) {
            var combines = new CombineInstance[count];
            int vertexCount = 0;
            int indexCount = 0;
            bool hasFadeMembers = false;
            for (int i = 0; i < count; i++) {
                BatchSource source = batch.Sources[start + i];
                combines[i] = new CombineInstance {
                    mesh = source.Mesh,
                    transform = source.Transform
                };
                vertexCount += source.Mesh.vertexCount;
                indexCount += (int) source.Mesh.GetIndexCount(0);
                hasFadeMembers |= source.FadeCandidate;
            }

            var mesh = new Mesh {
                name = $"RO Static Model Batch {batchIndex}",
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.CombineMeshes(combines, mergeSubMeshes: true, useMatrices: true, hasLightmapData: false);
            mesh.RecalculateBounds();

            var batchObject = new GameObject(mesh.name);
            batchObject.isStatic = true;
            batchObject.transform.SetParent(parent, false);
            var filter = batchObject.AddComponent<MeshFilter>();
            var renderer = batchObject.AddComponent<MeshRenderer>();
            batchObject.AddComponent<RoStaticModelBatchChunkStats>();
            // built anew at every map load: goes with the map
            RoOwnedRuntimeAssets.Own(batchObject, mesh);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = batch.Material;
            renderer.shadowCastingMode = batch.ShadowCastingMode;
            renderer.receiveShadows = batch.ReceiveShadows;
            renderer.renderingLayerMask = batch.RenderingLayerMask;
            if (hasFadeMembers) {
                using var fadeScope = FadeMarker.Auto();
                RegisterFadeMembers(batchSet, batch, start, count, mesh, renderer, indexCount);
            }
        }

        /// <summary>
        /// Where each fade candidate's triangles lie in the combined index buffer: CombineMeshes (merged sub-meshes)
        /// appends every instance's sub-mesh 0 in array order. The batch keeps its original indices to put a range back.
        /// </summary>
        private static void RegisterFadeMembers(
            RoStaticModelBatchSet batchSet,
            BatchBuildState batch,
            int start,
            int count,
            Mesh mesh,
            MeshRenderer renderer,
            int expectedIndexCount
        ) {
            int[] indices = mesh.GetIndices(0);
            if (indices.Length != expectedIndexCount) {
                // the ranges cannot be trusted: this batch's candidates stay in it and do not fade
                Debug.LogWarning($"[RoStaticModelBatcher] {mesh.name}: {indices.Length} indices, expected {expectedIndexCount}; its fade candidates will not fade");
                return;
            }

            var entry = new RoStaticModelBatchSet.Batch { Mesh = mesh, Renderer = renderer };
            if (mesh.indexFormat == IndexFormat.UInt16) {
                entry.Indices16 = new ushort[indices.Length];
                for (int i = 0; i < indices.Length; i++) {
                    entry.Indices16[i] = (ushort) indices[i];
                }
            } else {
                entry.Indices32 = indices;
            }

            int indexStart = 0;
            for (int i = 0; i < count; i++) {
                BatchSource source = batch.Sources[start + i];
                int sourceIndices = (int) source.Mesh.GetIndexCount(0);
                if (source.FadeCandidate) {
                    batchSet.Add(source.Renderer, new RoStaticModelBatchSet.Member(entry, indexStart, sourceIndices));
                }

                indexStart += sourceIndices;
            }
        }

        private readonly struct BatchKey : IEquatable<BatchKey> {
            public BatchKey(
                int materialId,
                int chunkX,
                int chunkZ,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows,
                uint renderingLayerMask
            ) {
                MaterialId = materialId;
                ChunkX = chunkX;
                ChunkZ = chunkZ;
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
                RenderingLayerMask = renderingLayerMask;
            }

            private int MaterialId { get; }
            private int ChunkX { get; }
            private int ChunkZ { get; }
            private ShadowCastingMode ShadowCastingMode { get; }
            private bool ReceiveShadows { get; }
            private uint RenderingLayerMask { get; }

            public bool Equals(BatchKey other) =>
                MaterialId == other.MaterialId &&
                ChunkX == other.ChunkX &&
                ChunkZ == other.ChunkZ &&
                ShadowCastingMode == other.ShadowCastingMode &&
                ReceiveShadows == other.ReceiveShadows &&
                RenderingLayerMask == other.RenderingLayerMask;

            public override bool Equals(object obj) => obj is BatchKey other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    int hash = MaterialId;
                    hash = (hash * 397) ^ ChunkX;
                    hash = (hash * 397) ^ ChunkZ;
                    hash = (hash * 397) ^ (int) ShadowCastingMode;
                    hash = (hash * 397) ^ (ReceiveShadows ? 1 : 0);
                    hash = (hash * 397) ^ (int) RenderingLayerMask;
                    return hash;
                }
            }
        }

        private readonly struct BatchSource {
            public BatchSource(
                MeshRenderer renderer,
                Mesh mesh,
                Material material,
                Matrix4x4 transform,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows,
                uint renderingLayerMask,
                bool fadeCandidate
            ) {
                Renderer = renderer;
                Mesh = mesh;
                Material = material;
                Transform = transform;
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
                RenderingLayerMask = renderingLayerMask;
                FadeCandidate = fadeCandidate;
            }

            public bool FadeCandidate { get; }
            public MeshRenderer Renderer { get; }
            public Mesh Mesh { get; }
            public Material Material { get; }
            public Matrix4x4 Transform { get; }
            public ShadowCastingMode ShadowCastingMode { get; }
            public bool ReceiveShadows { get; }
            public uint RenderingLayerMask { get; }
        }

        private sealed class BatchBuildState {
            public BatchBuildState(Material material, ShadowCastingMode shadowCastingMode, bool receiveShadows, uint renderingLayerMask) {
                Material = material;
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
                RenderingLayerMask = renderingLayerMask;
            }

            public Material Material { get; }
            public ShadowCastingMode ShadowCastingMode { get; }
            public bool ReceiveShadows { get; }
            public uint RenderingLayerMask { get; }
            public List<BatchSource> Sources { get; } = new List<BatchSource>(32);
        }
    }
}
