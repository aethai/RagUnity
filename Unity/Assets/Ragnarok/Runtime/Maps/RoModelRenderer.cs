using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Ragnarok.Assets;
using Ragnarok.Effects;
using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Maps {
    public readonly struct RoModelRendererCacheStats {
        public RoModelRendererCacheStats(
            long meshHits,
            long meshMisses,
            long meshBuilds,
            int meshEntries,
            long templateHits,
            long templateMisses,
            long templateBuilds,
            int templateEntries
        ) {
            MeshHits = meshHits;
            MeshMisses = meshMisses;
            MeshBuilds = meshBuilds;
            MeshEntries = meshEntries;
            TemplateHits = templateHits;
            TemplateMisses = templateMisses;
            TemplateBuilds = templateBuilds;
            TemplateEntries = templateEntries;
        }

        public long MeshHits { get; }
        public long MeshMisses { get; }
        public long MeshBuilds { get; }
        public int MeshEntries { get; }
        public long TemplateHits { get; }
        public long TemplateMisses { get; }
        public long TemplateBuilds { get; }
        public int TemplateEntries { get; }
    }

    public static class RoModelRenderer {
        public enum ModelUvDebugMode {
            Default,
            NoVFlip
        }

        public enum ModelTextureWrapDebugMode {
            Repeat,
            Clamp
        }

        public enum MaterialMode {
            Opaque,
            Cutout,
            AlphaBlend
        }

        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ModelMeshCacheKey, Mesh> MeshCache = new Dictionary<ModelMeshCacheKey, Mesh>();
        private static readonly Dictionary<ModelTemplateCacheKey, GameObject> TemplateCache = new Dictionary<ModelTemplateCacheKey, GameObject>();
        private static readonly HashSet<string> ReportedMissingTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // the parts of a map's model build (first visits: ~1300 instances of ~60 templates in Prontera)
        private static readonly Unity.Profiling.ProfilerMarker TemplateMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Template");
        private static readonly Unity.Profiling.ProfilerMarker InstantiateMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Instantiate");
        private static readonly Unity.Profiling.ProfilerMarker PlaceMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Place");
        private static readonly Unity.Profiling.ProfilerMarker SurfacesMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Surfaces");
        private static readonly Unity.Profiling.ProfilerMarker ActivateMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Activate");
        private static readonly Unity.Profiling.ProfilerMarker ParentMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Parent");
        private static readonly Unity.Profiling.ProfilerMarker AnimatedMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Animated");
        private static readonly Unity.Profiling.ProfilerMarker NodeMeshMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Node.Mesh");
        private static readonly Unity.Profiling.ProfilerMarker NodeMaterialMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Node.Material");
        private static readonly Unity.Profiling.ProfilerMarker NodeSurfaceMarker = new Unity.Profiling.ProfilerMarker("Map.Models.Node.Surface");

        /// <summary>
        /// A model texture ready for its materials: black-keyed / dilated once (its own copy, destroyed with the caches)
        /// and its alpha known. Before, every material variant of a texture (alpha, two-sided, ...) cloned, keyed, dilated
        /// and scanned it again, and each copy stayed in memory after the map was left.
        /// </summary>
        private sealed class PreparedTexture {
            public Texture2D Texture;
            public TextureAlphaInfo Alpha;
            public bool Owned;
        }

        private static readonly Dictionary<string, PreparedTexture> PreparedTextureCache = new Dictionary<string, PreparedTexture>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<RoModelSurfaceInfo> SurfaceScratch = new List<RoModelSurfaceInfo>(512);
        private const float ModelCutoutAlphaCutoff = 0.5f;
        private const float ModelTransparentAlphaCutoff = 0.01f;
        private const float PronteraFountainWaterFlowStrength = 0.75f;
        private const float PronteraFountainWaterFlowSpeed = 0.9f;
        private static readonly RSW.ModelDescriptor TemplateDescriptor = new RSW.ModelDescriptor {
            position = new[] { 0f, 0f, 0f },
            rotation = new[] { 0f, 0f, 0f },
            scale = new[] { 1f, 1f, 1f }
        };
        private static long meshCacheHits;
        private static long meshCacheMisses;
        private static long meshCacheBuilds;
        private static long templateCacheHits;
        private static long templateCacheMisses;
        private static long templateCacheBuilds;

        // M-9: Captures all debug-override statics at the start of BuildModels so that
        // every mesh and material in a single build call uses a consistent set of settings,
        // regardless of whether another thread changes the statics mid-build.
        private readonly struct RenderSnapshot {
            public readonly ModelUvDebugMode UvDebugMode;
            public readonly ModelTextureWrapDebugMode TextureWrapDebugMode;
            public readonly bool DisableBlackKey;
            public readonly RoShadowRenderMode ShadowMode;
            public readonly float RealtimeShadowStrength;
            public readonly float ModelTopLightStrength;

            public RenderSnapshot(
                ModelUvDebugMode uv,
                ModelTextureWrapDebugMode wrap,
                bool noBlackKey,
                RoShadowRenderMode shadowMode,
                float realtimeShadowStrength,
                float modelTopLightStrength
            ) {
                UvDebugMode = uv;
                TextureWrapDebugMode = wrap;
                DisableBlackKey = noBlackKey;
                ShadowMode = shadowMode;
                RealtimeShadowStrength = realtimeShadowStrength;
                ModelTopLightStrength = modelTopLightStrength;
            }

            public static RenderSnapshot Capture(RoShadowRenderMode shadowMode, RoEnhancedShadowSettings enhancedShadows) {
                RoEnhancedShadowSettings resolvedSettings = enhancedShadows ?? new RoEnhancedShadowSettings();
                return new RenderSnapshot(
                    RoModelRenderer.UvDebugMode,
                    RoModelRenderer.TextureWrapDebugMode,
                    RoModelRenderer.DisableBlackKey,
                    shadowMode,
                    Mathf.Clamp01(resolvedSettings.modelRealtimeShadowStrength),
                    Mathf.Clamp01(resolvedSettings.modelTopLightStrength));
            }
        }

        public static ModelUvDebugMode UvDebugMode { get; set; }
        public static ModelTextureWrapDebugMode TextureWrapDebugMode { get; set; }
        public static bool DisableBlackKey { get; set; }

        // Automatyczne czyszczenie cache przy każdym wejściu w Play Mode (domain reload).
        // Bez tego materiały z poprzedniej sesji edytora przeżywają reload i mogą
        // być serwowane dla nowej mapy — powodując stare tekstury na nowych modelach.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() {
            DestroyPreparedTextures();
            DestroyCachedMaterials();
            DestroyCachedMeshes();
            DestroyCachedTemplates();
            MaterialCache.Clear();
            MeshCache.Clear();
            TemplateCache.Clear();
            ReportedMissingTextures.Clear();
            Interlocked.Exchange(ref meshCacheHits, 0);
            Interlocked.Exchange(ref meshCacheMisses, 0);
            Interlocked.Exchange(ref meshCacheBuilds, 0);
            Interlocked.Exchange(ref templateCacheHits, 0);
            Interlocked.Exchange(ref templateCacheMisses, 0);
            Interlocked.Exchange(ref templateCacheBuilds, 0);
            UvDebugMode = ModelUvDebugMode.Default;
            TextureWrapDebugMode = ModelTextureWrapDebugMode.Repeat;
            DisableBlackKey = false;
        }

        public static void ClearCaches() {
            DestroyPreparedTextures();
            DestroyCachedMaterials();
            DestroyCachedMeshes();
            DestroyCachedTemplates();
            MaterialCache.Clear();
            MeshCache.Clear();
            TemplateCache.Clear();
            ReportedMissingTextures.Clear();
            Interlocked.Exchange(ref meshCacheHits, 0);
            Interlocked.Exchange(ref meshCacheMisses, 0);
            Interlocked.Exchange(ref meshCacheBuilds, 0);
            Interlocked.Exchange(ref templateCacheHits, 0);
            Interlocked.Exchange(ref templateCacheMisses, 0);
            Interlocked.Exchange(ref templateCacheBuilds, 0);
        }

        public static RoModelRendererCacheStats SnapshotCacheStats() =>
            new RoModelRendererCacheStats(
                Interlocked.Read(ref meshCacheHits),
                Interlocked.Read(ref meshCacheMisses),
                Interlocked.Read(ref meshCacheBuilds),
                MeshCache.Count,
                Interlocked.Read(ref templateCacheHits),
                Interlocked.Read(ref templateCacheMisses),
                Interlocked.Read(ref templateCacheBuilds),
                TemplateCache.Count);

        private static void DestroyPreparedTextures() {
            foreach (PreparedTexture prepared in PreparedTextureCache.Values) {
                if (!prepared.Owned || prepared.Texture == null) {
                    continue;
                }

                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(prepared.Texture);
                } else {
                    UnityEngine.Object.DestroyImmediate(prepared.Texture);
                }
            }

            PreparedTextureCache.Clear();
        }

        private static void DestroyCachedMaterials() {
            foreach (Material mat in MaterialCache.Values) {
                if (mat == null) {
                    continue;
                }

                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(mat);
                } else {
                    UnityEngine.Object.DestroyImmediate(mat);
                }
            }
        }

        private static void DestroyCachedMeshes() {
            foreach (Mesh mesh in MeshCache.Values) {
                if (mesh == null) {
                    continue;
                }

                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(mesh);
                } else {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        private static void DestroyCachedTemplates() {
            foreach (GameObject template in TemplateCache.Values) {
                if (template == null) {
                    continue;
                }

                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(template);
                } else {
                    UnityEngine.Object.DestroyImmediate(template);
                }
            }

#if UNITY_EDITOR
            // after a domain reload the cache is a new, empty dictionary: templates built before it (HideAndDontSave, in
            // no scene) are reachable only through the object list and used to pile up with every Play session
            foreach (GameObject orphan in Resources.FindObjectsOfTypeAll<GameObject>()) {
                if (orphan == null || orphan.transform.parent != null || (orphan.hideFlags & HideFlags.DontSave) == 0 ||
                    !orphan.name.StartsWith("RO Model Template - ", StringComparison.Ordinal) || TemplateCache.ContainsValue(orphan)) {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(orphan);
            }
#endif
        }

        public static int CountInstances(RSM.CompiledModel[] models) {
            if (models == null) {
                return 0;
            }

            int count = 0;
            foreach (RSM.CompiledModel model in models) {
                count += model.rsm.instances.Count;
            }

            return count;
        }

        /// <summary>Map load timing (main thread only): time in new meshes and in model textures since the last reset.</summary>
        internal static long BuildMeshTicks, BuildTextureTicks;

        public static GameObject BuildModels(
            RSM.CompiledModel[] models,
            int maxInstances,
            RoAssetDatabase assets,
            RoShadowRenderMode shadowMode = RoShadowRenderMode.Original,
            RoEnhancedShadowSettings enhancedShadows = null
        ) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            RenderSnapshot snap = RenderSnapshot.Capture(shadowMode, enhancedShadows);
            var root = new GameObject("RO Models");
            int created = 0;
            if (InstantiateInBatches) {
                BuildModelsInBatches(models, maxInstances, assets, snap, root.transform);
                return root;
            }

            foreach (RSM.CompiledModel model in models) {
                foreach (RSW.ModelDescriptor descriptor in model.rsm.instances) {
                    if (maxInstances > 0 && created >= maxInstances) {
                        return root;
                    }

                    GameObject instance = BuildModelInstance(model, descriptor, created, assets, snap, root.transform);
                    if (instance.transform.parent != root.transform) {
                        using (ParentMarker.Auto()) instance.transform.SetParent(root.transform, false);
                    }
                    created++;
                }
            }

            return root;
        }

        /// <summary>Diagnostics (!modelasync): off = one Instantiate per static instance as before.</summary>
        public static bool InstantiateInBatches = true;

        private readonly struct PendingInstance {
            public PendingInstance(RSW.ModelDescriptor descriptor, int index, string fileName) {
                Descriptor = descriptor;
                Index = index;
                FileName = fileName;
            }

            public readonly RSW.ModelDescriptor Descriptor;
            public readonly int Index;
            public readonly string FileName;
        }

        /// <summary>
        /// The static instances of each template made in one InstantiateAsync (a map has ~60 templates for ~1300 instances
        /// in Prontera): Unity copies the hierarchies in bulk, partly on worker threads, instead of one Instantiate call per
        /// instance; the animated ones are built as before. Same objects, transforms and surface data as one by one.
        /// </summary>
        private static void BuildModelsInBatches(RSM.CompiledModel[] models, int maxInstances, RoAssetDatabase assets, RenderSnapshot snap, Transform root) {
            var groups = new Dictionary<GameObject, List<PendingInstance>>();
            var order = new List<GameObject>();
            int created = 0;
            foreach (RSM.CompiledModel model in models) {
                foreach (RSW.ModelDescriptor descriptor in model.rsm.instances) {
                    if (maxInstances > 0 && created >= maxInstances) break;
                    int index = created++;
                    if (RoModelAnimationController.ShouldAnimate(model.rsm, descriptor)) {
                        GameObject animated;
                        using (AnimatedMarker.Auto()) animated = BuildModelInstanceUncached(model, descriptor, index, assets, snap, hasAnimation: true);
                        using (ParentMarker.Auto()) animated.transform.SetParent(root, false);
                        continue;
                    }

                    GameObject template;
                    using (TemplateMarker.Auto()) template = GetOrBuildStaticModelTemplate(model, assets, snap);
                    if (!groups.TryGetValue(template, out List<PendingInstance> list)) {
                        list = new List<PendingInstance>();
                        groups.Add(template, list);
                        order.Add(template);
                    }

                    list.Add(new PendingInstance(descriptor, index, model.rsm.filename));
                }
            }

            var operations = new List<(AsyncInstantiateOperation<GameObject> Operation, List<PendingInstance> Instances)>(order.Count);
            using (InstantiateMarker.Auto()) {
                foreach (GameObject template in order) {
                    List<PendingInstance> list = groups[template];
                    var positions = new Vector3[list.Count];
                    var rotations = new Quaternion[list.Count];
                    for (int i = 0; i < list.Count; i++) {
                        positions[i] = InstancePosition(list[i].Descriptor);
                        rotations[i] = InstanceRotation(list[i].Descriptor);
                    }

                    operations.Add((UnityEngine.Object.InstantiateAsync(template, list.Count, root, new ReadOnlySpan<Vector3>(positions), new ReadOnlySpan<Quaternion>(rotations)), list));
                }

                foreach ((AsyncInstantiateOperation<GameObject> operation, List<PendingInstance> _) in operations) {
                    operation.WaitForCompletion();
                }
            }

            foreach ((AsyncInstantiateOperation<GameObject> operation, List<PendingInstance> list) in operations) {
                GameObject[] instances = operation.Result;
                for (int i = 0; i < instances.Length; i++) {
                    GameObject instance = instances[i];
                    PendingInstance pending = list[i];
                    instance.name = $"{pending.FileName}[{pending.Index}]";
                    instance.hideFlags = HideFlags.None;
                    using (PlaceMarker.Auto()) {
                        // the parent is at the origin while the map builds: set locally as well, whatever space the copy took
                        instance.transform.SetLocalPositionAndRotation(InstancePosition(pending.Descriptor), InstanceRotation(pending.Descriptor));
                        instance.transform.localScale = InstanceScale(pending.Descriptor);
                    }

                    using (SurfacesMarker.Auto()) UpdateSurfaceInstances(instance, pending.Descriptor, pending.Index);
                    using (ActivateMarker.Auto()) instance.SetActive(true);
                }
            }
        }

        private static GameObject BuildModelInstance(RSM.CompiledModel model, RSW.ModelDescriptor descriptor, int index, RoAssetDatabase assets, RenderSnapshot snap, Transform parent = null) {
            bool hasAnimation = RoModelAnimationController.ShouldAnimate(model.rsm, descriptor);
            if (!hasAnimation) {
                return InstantiateStaticModelTemplate(model, descriptor, index, assets, snap, parent);
            }

            using (AnimatedMarker.Auto()) return BuildModelInstanceUncached(model, descriptor, index, assets, snap, hasAnimation: true);
        }

        private static GameObject BuildModelInstanceUncached(RSM.CompiledModel model, RSW.ModelDescriptor descriptor, int index, RoAssetDatabase assets, RenderSnapshot snap, bool hasAnimation) {
            var instance = new GameObject($"{model.rsm.filename}[{index}]");
            ApplyInstanceTransform(instance.transform, descriptor);

            var nodeTransforms = new Dictionary<RSM.Node, Transform>();
            var nodeTransformsByName = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            int nodeIndex = 0;
            foreach (Dictionary<long, RSM.NodeMeshData> nodeData in model.nodesData) {
                Transform nodeRoot = null;
                foreach (KeyValuePair<long, RSM.NodeMeshData> meshByTexture in nodeData) {
                    RSM.NodeMeshData meshData = meshByTexture.Value;
                    if (meshData.vertices.Count == 0) {
                        continue;
                    }

                    if (nodeRoot == null) {
                        nodeRoot = BuildNodeRoot(meshData.node, nodeIndex);
                        nodeRoot.SetParent(instance.transform, false);
                        ApplyNodeTransform(nodeRoot, meshData.node);
                        nodeTransforms[meshData.node] = nodeRoot;

                        if (!string.IsNullOrEmpty(meshData.node.name) && !nodeTransformsByName.ContainsKey(meshData.node.name)) {
                            nodeTransformsByName.Add(meshData.node.name, nodeRoot);
                        }
                    }

                    GameObject nodeObject = BuildNodeObject(model, descriptor, index, meshByTexture.Key, meshData, nodeIndex, assets, snap);
                    nodeObject.transform.SetParent(nodeRoot, false);
                }

                nodeIndex++;
            }

            if (hasAnimation) {
                instance.AddComponent<RoModelAnimationController>().Initialize(model.rsm, nodeTransforms, descriptor);
            } else if (!model.rsm.isRsm2) {
                // RSM2 node transforms stay flat under the instance: their matrices are absolute (model space).
                RebuildNodeHierarchy(nodeTransforms, nodeTransformsByName, model.rsm.mainNode?.name);
            }

            return instance;
        }

        private static GameObject InstantiateStaticModelTemplate(
            RSM.CompiledModel model,
            RSW.ModelDescriptor descriptor,
            int index,
            RoAssetDatabase assets,
            RenderSnapshot snap,
            Transform parent = null
        ) {
            GameObject template;
            using (TemplateMarker.Auto()) template = GetOrBuildStaticModelTemplate(model, assets, snap);
            GameObject instance;
            using (InstantiateMarker.Auto()) {
                // made inactive (as the template) straight under its parent, then placed and activated there
                instance = parent != null ? UnityEngine.Object.Instantiate(template, parent, false) : UnityEngine.Object.Instantiate(template);
                instance.name = $"{model.rsm.filename}[{index}]";
                instance.hideFlags = HideFlags.None;
            }

            using (PlaceMarker.Auto()) ApplyInstanceTransform(instance.transform, descriptor);
            using (SurfacesMarker.Auto()) UpdateSurfaceInstances(instance, descriptor, index);
            using (ActivateMarker.Auto()) instance.SetActive(true);
            return instance;
        }

        private static GameObject GetOrBuildStaticModelTemplate(RSM.CompiledModel model, RoAssetDatabase assets, RenderSnapshot snap) {
            var key = new ModelTemplateCacheKey(model, snap);
            if (TemplateCache.TryGetValue(key, out GameObject cached)) {
                if (cached != null) {
                    Interlocked.Increment(ref templateCacheHits);
                    return cached;
                }

                TemplateCache.Remove(key);
            }

            Interlocked.Increment(ref templateCacheMisses);
            GameObject template = BuildModelInstanceUncached(
                model,
                TemplateDescriptor,
                -1,
                assets,
                snap,
                hasAnimation: false);
            template.name = $"RO Model Template - {model.rsm.filename}";
            template.hideFlags = HideFlags.HideAndDontSave;
            AttachTemplateSurfaceSet(template);
            template.SetActive(false);
            TemplateCache[key] = template;
            Interlocked.Increment(ref templateCacheBuilds);
            return template;
        }

        private static void AttachTemplateSurfaceSet(GameObject template) {
            if (template == null) {
                return;
            }

            RoModelTemplateSurfaceSet surfaceSet = template.FindComponent<RoModelTemplateSurfaceSet>();
            if (surfaceSet == null) {
                surfaceSet = template.AddComponent<RoModelTemplateSurfaceSet>();
            }

            SurfaceScratch.Clear();
            template.GetComponentsInChildren(true, SurfaceScratch);
            surfaceSet.Initialize(SurfaceScratch);
            SurfaceScratch.Clear();
        }

        private static void UpdateSurfaceInstances(GameObject instance, RSW.ModelDescriptor descriptor, int instanceIndex) {
            RoModelTemplateSurfaceSet surfaceSet = instance.FindComponent<RoModelTemplateSurfaceSet>();
            if (surfaceSet != null && surfaceSet.ReferencesInstance(instance.transform)) {
                surfaceSet.UpdateInstances(instanceIndex, descriptor.position, descriptor.rotation, descriptor.scale);
                return;
            }

            SurfaceScratch.Clear();
            instance.GetComponentsInChildren(true, SurfaceScratch);
            for (int i = 0; i < SurfaceScratch.Count; i++) {
                SurfaceScratch[i].UpdateInstance(instanceIndex, descriptor.position, descriptor.rotation, descriptor.scale);
            }

            SurfaceScratch.Clear();
        }

        private static Transform BuildNodeRoot(RSM.Node node, int nodeIndex) {
            string nodeName = string.IsNullOrEmpty(node.name) ? $"node_{nodeIndex}" : node.name;
            return new GameObject(nodeName).transform;
        }

        private static GameObject BuildNodeObject(RSM.CompiledModel model, RSW.ModelDescriptor descriptor, int instanceIndex, long textureId, RSM.NodeMeshData meshData, int nodeIndex, RoAssetDatabase assets, RenderSnapshot snap) {
            string nodeName = string.IsNullOrEmpty(meshData.node.name) ? $"node_{nodeIndex}" : meshData.node.name;
            string textureName = model.rsm.textures[textureId];
            var nodeObject = new GameObject($"{nodeName}:{Path.GetFileName(textureName)}");

            RoWindSwayCatalog.Profile windSwayProfile = ResolveWindSwayProfile(textureName, meshData.node);
            ModelWaterFlowProfile waterFlowProfile = ResolveModelWaterFlowProfile(textureName);
            Mesh mesh;
            using (NodeMeshMarker.Auto()) mesh = GetOrBuildMesh(meshData, $"{model.rsm.filename}:{nodeName}", snap, windSwayProfile);
            var filter = nodeObject.AddComponent<MeshFilter>();
            var renderer = nodeObject.AddComponent<MeshRenderer>();

            filter.sharedMesh = mesh;
            Material material;
            using (NodeMaterialMarker.Auto()) material = GetMaterial(model, textureId, meshData.twoSided, assets, snap, windSwayProfile, waterFlowProfile);
            renderer.sharedMaterial = material;
            ApplyRendererShadowState(renderer, meshData.twoSided, snap);
            using var surfaceScope = NodeSurfaceMarker.Auto();
            // the index count, not mesh.triangles (a copy of the whole index buffer for each surface)
            long indexCount = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) indexCount += mesh.GetIndexCount(sub);
            nodeObject.AddComponent<RoModelSurfaceInfo>().Initialize(
                model.rsm.filename,
                nodeName,
                textureName,
                meshData.twoSided,
                mesh.vertexCount,
                (int) (indexCount / 3),
                instanceIndex,
                descriptor.position,
                descriptor.rotation,
                descriptor.scale,
                mesh.bounds,
                material
            );
            return nodeObject;
        }

        public static string BuildMaterialSummary(GameObject root) {
            if (root == null) {
                return "materials unavailable";
            }

            SurfaceScratch.Clear();
            root.GetComponentsInChildren(true, SurfaceScratch);
            string summary = BuildMaterialSummary(SurfaceScratch);
            SurfaceScratch.Clear();
            return summary;
        }

        private static string BuildMaterialSummary(IReadOnlyList<RoModelSurfaceInfo> surfaces) {
            int opaque = 0;
            int cutout = 0;
            int alphaBlend = 0;
            int twoSided = 0;

            for (int i = 0; i < surfaces.Count; i++) {
                if (surfaces[i].TwoSided) {
                    twoSided++;
                }

                switch (surfaces[i].MaterialMode) {
                    case MaterialMode.Opaque:
                        opaque++;
                        break;
                    case MaterialMode.Cutout:
                        cutout++;
                        break;
                    case MaterialMode.AlphaBlend:
                        alphaBlend++;
                        break;
                }
            }

            return $"materials opaque={opaque}, cutout={cutout}, alphaBlend={alphaBlend}, twoSided={twoSided}";
        }

        public static List<WindSwayCandidateBucket> CollectWindSwayCandidateBuckets(GameObject root) {
            if (root == null) {
                return new List<WindSwayCandidateBucket>();
            }

            SurfaceScratch.Clear();
            root.GetComponentsInChildren(true, SurfaceScratch);
            List<WindSwayCandidateBucket> buckets = CollectWindSwayCandidateBuckets(SurfaceScratch);
            SurfaceScratch.Clear();
            return buckets;
        }

        public static List<WindSwayCandidateBucket> CollectWindSwayCandidateBuckets(IReadOnlyList<RoModelSurfaceInfo> surfaces) {
            var buckets = new Dictionary<string, WindSwayCandidateBucket>(StringComparer.OrdinalIgnoreCase);
            if (surfaces == null) {
                return new List<WindSwayCandidateBucket>();
            }

            for (int i = 0; i < surfaces.Count; i++) {
                RoModelSurfaceInfo surface = surfaces[i];
                if (!IsWindSwayCandidateSurface(surface)) {
                    continue;
                }

                string key = NormalizeWindSwayTextureBucketKey(surface.TextureName);
                if (string.IsNullOrEmpty(key)) {
                    continue;
                }

                if (!buckets.TryGetValue(key, out WindSwayCandidateBucket bucket)) {
                    bucket = new WindSwayCandidateBucket(surface);
                    buckets.Add(key, bucket);
                } else {
                    bucket.Add(surface);
                }
            }

            var result = new List<WindSwayCandidateBucket>(buckets.Values);
            result.Sort((a, b) => {
                int countCompare = b.SurfaceCount.CompareTo(a.SurfaceCount);
                return countCompare != 0
                    ? countCompare
                    : string.Compare(a.TextureName, b.TextureName, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        public static string BuildMaterialAudit(GameObject root, int maxItems = 24) {
            if (root == null) {
                return "RO model material audit unavailable: no model root.";
            }

            SurfaceScratch.Clear();
            root.GetComponentsInChildren(true, SurfaceScratch);
            var builder = new StringBuilder();
            builder.AppendLine("RO model material audit:");
            builder.AppendLine(BuildMaterialSummary(SurfaceScratch));
            builder.AppendLine("surfaces=" + SurfaceScratch.Count);

            var buckets = new Dictionary<string, MaterialAuditBucket>(StringComparer.OrdinalIgnoreCase);
            var flatCutouts = new Dictionary<string, SurfaceAuditBucket>(StringComparer.OrdinalIgnoreCase);
            var alphaBlend = new Dictionary<string, SurfaceAuditBucket>(StringComparer.OrdinalIgnoreCase);
            var twoSidedCutouts = new Dictionary<string, SurfaceAuditBucket>(StringComparer.OrdinalIgnoreCase);
            int flatCutoutCount = 0;
            int alphaBlendCount = 0;
            int twoSidedCutoutCount = 0;

            for (int i = 0; i < SurfaceScratch.Count; i++) {
                RoModelSurfaceInfo surface = SurfaceScratch[i];
                string bucketKey = $"{surface.MaterialMode}|q={surface.MaterialRenderQueue}|cull={surface.MaterialCull}|z={surface.MaterialZWrite}|{surface.TextureName}";
                if (!buckets.TryGetValue(bucketKey, out MaterialAuditBucket bucket)) {
                    bucket = new MaterialAuditBucket(surface.TextureName, surface.MaterialMode, surface.MaterialRenderQueue, surface.MaterialCull, surface.MaterialZWrite);
                    buckets.Add(bucketKey, bucket);
                }

                bucket.Count++;

                if (surface.MaterialMode == MaterialMode.AlphaBlend) {
                    alphaBlendCount++;
                    AddSurfaceBucket(alphaBlend, surface);
                }

                if (IsWindSwayCandidateSurface(surface)) {
                    twoSidedCutoutCount++;
                    AddSurfaceBucket(twoSidedCutouts, surface);
                }

                MeshRenderer renderer = surface.FindComponent<MeshRenderer>();
                if (surface.MaterialMode == MaterialMode.Cutout && renderer != null && LooksLikeFlatOverlay(renderer.bounds)) {
                    flatCutoutCount++;
                    AddSurfaceBucket(flatCutouts, surface);
                }
            }

            var bucketList = new List<MaterialAuditBucket>(buckets.Values);
            bucketList.Sort((a, b) => b.Count.CompareTo(a.Count));

            builder.AppendLine();
            builder.AppendLine("Top material buckets:");
            for (int i = 0; i < Mathf.Min(maxItems, bucketList.Count); i++) {
                MaterialAuditBucket bucket = bucketList[i];
                builder.AppendLine(
                    $"- count={bucket.Count}, mode={bucket.Mode}, queue={bucket.Queue}, cull={bucket.Cull:0.###}, zWrite={bucket.ZWrite:0.###}, texture='{bucket.TextureName}'"
                );
            }

            builder.AppendLine();
            builder.AppendLine("Findings:");
            builder.AppendLine(alphaBlendCount == 0
                ? "- no alpha-blended RSM surfaces found."
                : $"- alpha-blended RSM surfaces: {alphaBlendCount} surface(s) in {alphaBlend.Count} bucket(s); inspect only if they visibly sort incorrectly.");
            builder.AppendLine(flatCutoutCount == 0
                ? "- no flat cutout overlay candidates found."
                : $"- flat cutout overlay candidates: {flatCutoutCount} surface(s) in {flatCutouts.Count} bucket(s); expected for floor trim/decal RSMs.");
            builder.AppendLine(twoSidedCutoutCount == 0
                ? "- no two-sided cutout surfaces found."
                : $"- two-sided cutout surfaces: {twoSidedCutoutCount} surface(s) in {twoSidedCutouts.Count} bucket(s); expected for foliage, signs, banners, and thin props.");

            AppendSurfaceBuckets(builder, "Alpha blend surface buckets", alphaBlend, alphaBlendCount, maxItems);
            AppendSurfaceBuckets(builder, "Flat cutout overlay buckets", flatCutouts, flatCutoutCount, maxItems);
            AppendSurfaceBuckets(builder, "Two-sided cutout buckets", twoSidedCutouts, twoSidedCutoutCount, maxItems);
            string audit = builder.ToString().TrimEnd();
            SurfaceScratch.Clear();
            return audit;
        }

        private static void RebuildNodeHierarchy(
            Dictionary<RSM.Node, Transform> nodeTransforms,
            Dictionary<string, Transform> nodeTransformsByName,
            string mainNodeName
        ) {
            foreach (KeyValuePair<RSM.Node, Transform> entry in nodeTransforms) {
                RSM.Node node = entry.Key;
                if (!IsChildNode(node, mainNodeName)) {
                    continue;
                }

                if (nodeTransformsByName.TryGetValue(node.parentName, out Transform parent)) {
                    entry.Value.SetParent(parent, true);
                }
            }
        }

        private static bool IsChildNode(RSM.Node node, string mainNodeName) {
            return node != null
                && !string.IsNullOrEmpty(node.parentName)
                && !string.Equals(node.parentName, mainNodeName, StringComparison.OrdinalIgnoreCase);
        }

        private static Mesh GetOrBuildMesh(RSM.NodeMeshData meshData, string name, RenderSnapshot snap, RoWindSwayCatalog.Profile windSwayProfile) {
            var key = new ModelMeshCacheKey(meshData, snap.UvDebugMode, windSwayProfile);
            if (MeshCache.TryGetValue(key, out Mesh cached)) {
                if (cached != null) {
                    Interlocked.Increment(ref meshCacheHits);
                    return cached;
                }

                MeshCache.Remove(key);
            }

            Interlocked.Increment(ref meshCacheMisses);
            long meshStart = System.Diagnostics.Stopwatch.GetTimestamp();
            Mesh mesh = BuildMesh(meshData, name, snap, windSwayProfile);
            BuildMeshTicks += System.Diagnostics.Stopwatch.GetTimestamp() - meshStart;
            MeshCache[key] = mesh;
            Interlocked.Increment(ref meshCacheBuilds);
            return mesh;
        }

        private static Mesh BuildMesh(RSM.NodeMeshData meshData, string name, RenderSnapshot snap, RoWindSwayCatalog.Profile windSwayProfile) {
            int triangleCount = meshData.vertices.Count / 3;
            var triangles = new int[triangleCount * 3];
            for (int i = 0; i < triangles.Length; i++) {
                triangles[i] = i;
            }

            var mesh = new Mesh {
                name = name,
                indexFormat = meshData.vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(meshData.vertices);
            mesh.SetUVs(0, GetMeshUvs(meshData.uv, snap));
            if (windSwayProfile != null) {
                mesh.colors32 = BakeWindSwayColors(meshData.vertices, windSwayProfile);
            }

            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color32[] BakeWindSwayColors(IReadOnlyList<Vector3> vertices, RoWindSwayCatalog.Profile profile) {
            if (vertices == null || vertices.Count == 0) {
                return Array.Empty<Color32>();
            }

            Vector3 boundsMin = vertices[0];
            Vector3 boundsMax = vertices[0];
            float minAxis = RoWindSwayMath.GetAxisCoordinate(vertices[0], profile.Axis);
            float maxAxis = minAxis;
            for (int i = 1; i < vertices.Count; i++) {
                Vector3 vertex = vertices[i];
                boundsMin = Vector3.Min(boundsMin, vertex);
                boundsMax = Vector3.Max(boundsMax, vertex);
                float value = RoWindSwayMath.GetAxisCoordinate(vertex, profile.Axis);
                minAxis = Mathf.Min(minAxis, value);
                maxAxis = Mathf.Max(maxAxis, value);
            }

            var colors = new Color32[vertices.Count];
            for (int i = 0; i < vertices.Count; i++) {
                Vector3 vertex = vertices[i];
                float weight;
                if (profile.Anchor == RoWindSwayAnchor.Radial) {
                    weight = RoWindSwayMath.ComputeRadialWeight(vertex, boundsMin, boundsMax);
                } else {
                    float coordinate = RoWindSwayMath.GetAxisCoordinate(vertex, profile.Axis);
                    weight = RoWindSwayMath.ComputeAnchorWeight(coordinate, minAxis, maxAxis, profile.Anchor, profile.AnchorHold);
                }

                colors[i] = new Color32(
                    (byte) Mathf.RoundToInt(weight * 255f),
                    RoWindSwayMath.ComputePhaseSeed(vertex),
                    0,
                    255);
            }

            return colors;
        }

        private readonly struct ModelMeshCacheKey : IEquatable<ModelMeshCacheKey> {
            public ModelMeshCacheKey(RSM.NodeMeshData meshData, ModelUvDebugMode uvDebugMode, RoWindSwayCatalog.Profile windSwayProfile) {
                MeshData = meshData;
                UvDebugMode = uvDebugMode;
                WindSwayProfileIdentity = windSwayProfile?.Identity ?? 0;
            }

            private RSM.NodeMeshData MeshData { get; }
            private ModelUvDebugMode UvDebugMode { get; }
            private int WindSwayProfileIdentity { get; }

            public bool Equals(ModelMeshCacheKey other) =>
                ReferenceEquals(MeshData, other.MeshData) &&
                UvDebugMode == other.UvDebugMode &&
                WindSwayProfileIdentity == other.WindSwayProfileIdentity;

            public override bool Equals(object obj) => obj is ModelMeshCacheKey other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    int hash = RuntimeHelpers.GetHashCode(MeshData);
                    hash = (hash * 397) ^ (int) UvDebugMode;
                    hash = (hash * 397) ^ WindSwayProfileIdentity;
                    return hash;
                }
            }
        }

        private readonly struct ModelTemplateCacheKey : IEquatable<ModelTemplateCacheKey> {
            public ModelTemplateCacheKey(RSM.CompiledModel model, RenderSnapshot snap) {
                NodesData = model?.nodesData;
                Filename = model?.rsm?.filename ?? string.Empty;
                UvDebugMode = snap.UvDebugMode;
                TextureWrapDebugMode = snap.TextureWrapDebugMode;
                DisableBlackKey = snap.DisableBlackKey;
                ShadowMode = snap.ShadowMode;
                RealtimeShadowStrength = snap.RealtimeShadowStrength;
                ModelTopLightStrength = snap.ModelTopLightStrength;
                WindSwayCatalogSignature = RoWindSwayCatalog.Load().Signature;
            }

            private Dictionary<long, RSM.NodeMeshData>[] NodesData { get; }
            private string Filename { get; }
            private ModelUvDebugMode UvDebugMode { get; }
            private ModelTextureWrapDebugMode TextureWrapDebugMode { get; }
            private bool DisableBlackKey { get; }
            private RoShadowRenderMode ShadowMode { get; }
            private float RealtimeShadowStrength { get; }
            private float ModelTopLightStrength { get; }
            private int WindSwayCatalogSignature { get; }

            public bool Equals(ModelTemplateCacheKey other) =>
                ReferenceEquals(NodesData, other.NodesData) &&
                string.Equals(Filename, other.Filename, StringComparison.OrdinalIgnoreCase) &&
                UvDebugMode == other.UvDebugMode &&
                TextureWrapDebugMode == other.TextureWrapDebugMode &&
                DisableBlackKey == other.DisableBlackKey &&
                ShadowMode == other.ShadowMode &&
                RealtimeShadowStrength.Equals(other.RealtimeShadowStrength) &&
                ModelTopLightStrength.Equals(other.ModelTopLightStrength) &&
                WindSwayCatalogSignature == other.WindSwayCatalogSignature;

            public override bool Equals(object obj) => obj is ModelTemplateCacheKey other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    int hash = RuntimeHelpers.GetHashCode(NodesData);
                    hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Filename);
                    hash = (hash * 397) ^ (int) UvDebugMode;
                    hash = (hash * 397) ^ (int) TextureWrapDebugMode;
                    hash = (hash * 397) ^ (DisableBlackKey ? 1 : 0);
                    hash = (hash * 397) ^ (int) ShadowMode;
                    hash = (hash * 397) ^ RealtimeShadowStrength.GetHashCode();
                    hash = (hash * 397) ^ ModelTopLightStrength.GetHashCode();
                    hash = (hash * 397) ^ WindSwayCatalogSignature;
                    return hash;
                }
            }
        }

        private static List<Vector2> GetMeshUvs(List<Vector2> source, RenderSnapshot snap) {
            if (snap.UvDebugMode == ModelUvDebugMode.Default) {
                return source;
            }

            var output = new List<Vector2>(source.Count);
            for (int i = 0; i < source.Count; i++) {
                Vector2 uv = source[i];
                if (snap.UvDebugMode == ModelUvDebugMode.NoVFlip) {
                    uv.y = -uv.y;
                }

                output.Add(uv);
            }

            return output;
        }

        private static Vector3 InstancePosition(RSW.ModelDescriptor descriptor) =>
            new Vector3(descriptor.position[0], -descriptor.position[1], descriptor.position[2]);

        /// <summary>
        /// ApplyInstanceTransform's three Space.Self rotations (z, then x, then y) as one quaternion: Rotate(axis, angle,
        /// Self) is rotation * AngleAxis(angle, axis).
        /// </summary>
        public static Quaternion InstanceRotation(RSW.ModelDescriptor descriptor) =>
            Quaternion.AngleAxis(-descriptor.rotation[2], Vector3.forward) *
            Quaternion.AngleAxis(-descriptor.rotation[0], Vector3.right) *
            Quaternion.AngleAxis(descriptor.rotation[1], Vector3.up);

        private static Vector3 InstanceScale(RSW.ModelDescriptor descriptor) =>
            new Vector3(descriptor.scale[0], -descriptor.scale[1], descriptor.scale[2]);

        private static void ApplyInstanceTransform(Transform transform, RSW.ModelDescriptor descriptor) {
            transform.localPosition = new Vector3(descriptor.position[0], -descriptor.position[1], descriptor.position[2]);
            transform.localRotation = Quaternion.identity;
            transform.Rotate(Vector3.forward, -descriptor.rotation[2], Space.Self);
            transform.Rotate(Vector3.right, -descriptor.rotation[0], Space.Self);
            transform.Rotate(Vector3.up, descriptor.rotation[1], Space.Self);
            transform.localScale = new Vector3(descriptor.scale[0], -descriptor.scale[1], descriptor.scale[2]);
        }

        private static void ApplyNodeTransform(Transform transform, RSM.Node node) {
            if (node.model != null && node.model.isRsm2) {
                ROIO.Utils.Rsm2Pose.Decompose(node.GetPositionMatrix(), out Vector3 position, out Quaternion rotation, out Vector3 scale);
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;
                return;
            }

            Matrix4x4 matrix = node.GetPositionMatrix();
            transform.localPosition = matrix.GetColumn(3);
            transform.localRotation = ExtractRotation(matrix);
            transform.localScale = ExtractScale(matrix);
        }

        private static Quaternion ExtractRotation(Matrix4x4 matrix) {
            Vector3 forward = new Vector3(matrix.m02, matrix.m12, matrix.m22);
            Vector3 upwards = new Vector3(matrix.m01, matrix.m11, matrix.m21);
            return Quaternion.LookRotation(forward, upwards);
        }

        private static Vector3 ExtractScale(Matrix4x4 matrix) {
            return new Vector3(
                new Vector3(matrix.m00, matrix.m10, matrix.m20).magnitude,
                new Vector3(matrix.m01, matrix.m11, matrix.m21).magnitude,
                new Vector3(matrix.m02, matrix.m12, matrix.m22).magnitude
            );
        }

        private static Material GetMaterial(
            RSM.CompiledModel model,
            long textureId,
            bool twoSided,
            RoAssetDatabase assets,
            RenderSnapshot snap,
            RoWindSwayCatalog.Profile windSwayProfile,
            ModelWaterFlowProfile waterFlowProfile
        ) {
            string textureName = model.rsm.textures[textureId];
            int windSwayIdentity = windSwayProfile?.Identity ?? 0;
            string cacheKey = $"{textureName}|{model.rsm.alpha}|{twoSided}|uv={snap.UvDebugMode}|wrap={snap.TextureWrapDebugMode}|black={snap.DisableBlackKey}|shadow={snap.ShadowMode}|top={snap.ModelTopLightStrength:0.###}|sway={windSwayIdentity}|flow={waterFlowProfile.Identity}";
            if (MaterialCache.TryGetValue(cacheKey, out Material cached)) {
                return cached;
            }

            Shader shader = ResolveModelShader(snap.ShadowMode == RoShadowRenderMode.Enhanced);

            var material = new Material(shader) {
                name = $"RO Model {textureName}",
                color = new Color(1f, 1f, 1f, model.rsm.alpha)
            };

            long textureStart = System.Diagnostics.Stopwatch.GetTimestamp();
            PreparedTexture prepared = LoadPreparedModelTexture(textureName, assets, snap);
            Texture2D texture = prepared?.Texture;
            BuildTextureTicks += System.Diagnostics.Stopwatch.GetTimestamp() - textureStart;
            if (texture != null) {
                material.mainTexture = texture;
                SetTextureIfPropertyExists(material, "_BaseMap", texture);
                SetTextureIfPropertyExists(material, "_MainTex", texture);
            }

            string materialFileName = Path.GetFileNameWithoutExtension(textureName);
            bool blackKeyedFoliage = IsBlackKeyedFoliageTexture(materialFileName);
            ConfigureMaterial(material, model.rsm.alpha, texture != null ? prepared.Alpha : default, twoSided, blackKeyedFoliage);
            ApplyMaterialShadowState(material, snap);
            ApplyMaterialWindSwayState(material, windSwayProfile);
            ApplyMaterialWaterFlowState(material, waterFlowProfile);

            MaterialCache[cacheKey] = material;
            return material;
        }

        // Shader.Find is a lookup by name over every shader: once per shader, not two to four times per new material
        private static Shader enhancedModelShader, originalModelShader;

        private static Shader ResolveModelShader(bool enhanced) {
            Shader cached = enhanced ? enhancedModelShader : originalModelShader;
            if (cached != null) return cached;
            Shader shader = Shader.Find(enhanced ? "Ragnarok/RO Model Enhanced" : "Ragnarok/RO Model Unlit AlphaClip");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (enhanced) enhancedModelShader = shader;
            else originalModelShader = shader;
            return shader;
        }

        private static RoWindSwayCatalog.Profile ResolveWindSwayProfile(string textureName, RSM.Node node) {
            RoWindSwayCatalog catalog = RoWindSwayCatalog.Load();
            if (!catalog.TryFindProfile(textureName, out RoWindSwayCatalog.Profile profile)) {
                return null;
            }

            return NodeHasKeyframeAnimation(node) && !profile.AllowAnimatedNode
                ? null
                : profile;
        }

        private static bool NodeHasKeyframeAnimation(RSM.Node node) {
            if (node == null) {
                return false;
            }

            return RoWindSwayMath.HasAnimatedKeyframes(
                node.rotKeyframes?.Count ?? 0,
                node.posKeyframes?.Count ?? 0,
                node.scaleKeyframes?.Count ?? 0);
        }

        private static ModelWaterFlowProfile ResolveModelWaterFlowProfile(string textureName) {
            string fileName = Path.GetFileNameWithoutExtension(textureName?.Replace('\\', '/') ?? string.Empty);
            if (string.Equals(fileName, "dong-d-3", StringComparison.OrdinalIgnoreCase)) {
                return new ModelWaterFlowProfile(1, ModelWaterFlowMode.Full, PronteraFountainWaterFlowStrength, PronteraFountainWaterFlowSpeed);
            }

            return default;
        }

        internal static Texture2D LoadModelTexture(string textureName, RoAssetDatabase assets) =>
            LoadModelTexture(
                textureName,
                assets,
                new RenderSnapshot(
                    ModelUvDebugMode.Default,
                    ModelTextureWrapDebugMode.Repeat,
                    false,
                    RoShadowRenderMode.Original,
                    RoEnhancedShadowSettings.DefaultModelRealtimeShadowStrength,
                    RoEnhancedShadowSettings.DefaultModelTopLightStrength));

        private static Texture2D LoadModelTexture(string textureName, RoAssetDatabase assets, RenderSnapshot snap) =>
            LoadPreparedModelTexture(textureName, assets, snap)?.Texture;

        private static PreparedTexture LoadPreparedModelTexture(string textureName, RoAssetDatabase assets, RenderSnapshot snap) {
            string cacheKey = PreparedKey(textureName, snap.TextureWrapDebugMode, snap.DisableBlackKey);
            if (PreparedTextureCache.TryGetValue(cacheKey, out PreparedTexture cachedPrepared) &&
                (cachedPrepared.Texture != null || ReferenceEquals(cachedPrepared, MissingPreparedTexture))) {
                return cachedPrepared.Texture != null ? cachedPrepared : null;
            }

            PreparedTexture prepared = PrepareModelTexture(textureName, assets, snap);
            PreparedTextureCache[cacheKey] = prepared ?? MissingPreparedTexture;
            return prepared;
        }

        private static readonly PreparedTexture MissingPreparedTexture = new PreparedTexture();

        /// <summary>The paths a model texture is looked for at, in order (the first that loads wins).</summary>
        private static string[] ModelTextureCandidates(string textureName, out bool blackKeyedFoliage) {
            string normalized = textureName.Replace('\\', '/');
            string directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(normalized);
            blackKeyedFoliage = IsBlackKeyedFoliageTexture(fileName);
            return new[] {
                $"data/texture/{normalized}",
                $"data/texture/{directory}/{fileName}.bmp",
                $"data/texture/{directory}/{fileName}.png",
                $"data/texture/{directory}/{fileName.ToLowerInvariant()}.bmp",
                $"data/texture/{directory}/{fileName.ToLowerInvariant()}.png",
                $"data/texture/{fileName}.bmp",
                $"data/texture/{fileName}.png"
            };
        }

        // ---- texture prefetch: the archive reads, the decode and the pixel work of a map's model textures on worker
        // threads while the main thread builds the ground; the main thread then only uploads ----

        private sealed class PrefetchedPixels {
            public string Path;
            public int Width, Height;
            public Color32[] Pixels;
            public TextureAlphaInfo Alpha;
        }

        /// <summary>Diagnostics (!texprefetch): off = every model texture loads on the main thread as before.</summary>
        public static bool TexturePrefetchEnabled = true;
        /// <summary>Diagnostics (!texclear): the model caches cleared at every map load, for repeatable first-visit timings.</summary>
        public static bool ForceClearCachesOnMapLoad;

        private static readonly Dictionary<string, PrefetchedPixels> Prefetched = new Dictionary<string, PrefetchedPixels>(StringComparer.OrdinalIgnoreCase);
        private static System.Threading.Tasks.Task prefetchTask;
        private static RoAssetDatabase prefetchAssets;

        /// <summary>
        /// Starts reading and preparing every texture of <paramref name="models"/> that is not prepared yet (all of them when
        /// <paramref name="cachesWillBeCleared"/>) on worker threads. <see cref="BuildModels"/> takes the results; a texture
        /// the prefetch could not do (another image format, a decode problem) loads on the main thread as before.
        /// </summary>
        public static void BeginTexturePrefetch(IEnumerable<RSM.CompiledModel> models, RoAssetDatabase assets, bool cachesWillBeCleared) {
            if (models == null || assets == null || !TexturePrefetchEnabled) return;
            WaitTexturePrefetch();
            Prefetched.Clear();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RSM.CompiledModel model in models) {
                string[] textures = model?.rsm?.textures;
                if (textures == null) continue;
                foreach (string name in textures) {
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!cachesWillBeCleared && PreparedTextureCache.ContainsKey(PreparedKey(name, ModelTextureWrapDebugMode.Repeat, false))) continue;
                    names.Add(name);
                }
            }

            if (names.Count == 0) return;
            prefetchAssets = assets;
            var work = new List<string>(names);
            var results = new PrefetchedPixels[work.Count];
            int workers = Math.Max(1, Math.Min(work.Count, Environment.ProcessorCount - 1));
            prefetchTask = System.Threading.Tasks.Task.Run(() => {
                System.Threading.Tasks.Parallel.For(0, work.Count, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = workers },
                    i => results[i] = PrefetchOne(work[i], assets));
                lock (Prefetched) {
                    for (int i = 0; i < work.Count; i++) {
                        if (results[i] != null) Prefetched[work[i]] = results[i];
                    }
                }
            });
        }

        private static void WaitTexturePrefetch() {
            System.Threading.Tasks.Task task = prefetchTask;
            if (task == null) return;
            prefetchTask = null;
            try {
                task.Wait();
            } catch (Exception ex) {
                Debug.LogWarning($"Model texture prefetch failed, the textures load on the main thread: {ex.GetBaseException().Message}");
                lock (Prefetched) Prefetched.Clear();
            }
        }

        /// <summary>Worker thread: the first candidate that exists, decoded and prepared (black key off, the material default).</summary>
        private static PrefetchedPixels PrefetchOne(string textureName, RoAssetDatabase assets) {
            string[] candidates = ModelTextureCandidates(textureName, out bool blackKeyedFoliage);
            foreach (string candidate in candidates) {
                string path = candidate.Replace("//", "/");
                byte[] data = assets.ReadBytesQuiet(path);
                if (data == null) continue;
                bool tga = path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase);
                if (!tga && !path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) return null;
                if (!ROIO.Loaders.RoFastImageTexture.TryDecodePixels(data, tga, out int width, out int height, out Color32[] pixels)) return null;
                Color32[] processed = PrepareModelPixels(pixels, width, height, blackKeyedFoliage, out TextureAlphaInfo alpha);
                return new PrefetchedPixels { Path = path, Width = width, Height = height, Pixels = processed ?? pixels, Alpha = alpha };
            }

            return null;
        }

        /// <summary>Main thread: the prefetched texture, uploaded (null when the prefetch did not do it).</summary>
        private static PreparedTexture TakePrefetched(string textureName, RoAssetDatabase assets, RenderSnapshot snap) {
            if (snap.DisableBlackKey || !ReferenceEquals(assets, prefetchAssets)) return null;
            WaitTexturePrefetch();
            PrefetchedPixels pixels;
            lock (Prefetched) {
                if (!Prefetched.TryGetValue(textureName, out pixels)) return null;
                Prefetched.Remove(textureName);
            }

            // mipmapped (the pixels are dilated into the keyed-out texels, so the mips keep the edge colour): far models
            // and roofs stopped shimmering while the camera moves
            var texture = new Texture2D(pixels.Width, pixels.Height, TextureFormat.RGBA32, true, linear: false) { name = pixels.Path, anisoLevel = 4 };
            texture.SetPixels32(pixels.Pixels);
            texture.Apply(true, false);
            return new PreparedTexture { Texture = texture, Alpha = pixels.Alpha, Owned = true };
        }

        /// <summary>
        /// Diagnostics (!texverify): every model texture prepared so far done again both ways — the worker path (archive
        /// bytes, fast decode, pixel work on arrays) and the main-thread path (asset texture, PrepareLoadedTexture) — and
        /// compared pixel for pixel, alpha flags included.
        /// </summary>
        public static string VerifyTexturePrefetch(RoAssetDatabase assets) {
            if (assets == null) return "no assets";
            int same = 0, differ = 0, skipped = 0;
            var report = new StringBuilder();
            var names = new List<string>();
            foreach (string key in PreparedTextureCache.Keys) {
                string name = key.Substring(0, key.IndexOf('|'));
                if (!names.Contains(name)) names.Add(name);
            }

            foreach (string name in names) {
                PrefetchedPixels worker = PrefetchOne(name, assets);
                string[] candidates = ModelTextureCandidates(name, out bool blackKey);
                Texture2D source = null;
                foreach (string candidate in candidates) {
                    source = assets.LoadTexture(candidate.Replace("//", "/"));
                    if (source != null) break;
                }

                if (worker == null || source == null) {
                    skipped++;
                    continue;
                }

                PreparedTexture main = PrepareLoadedTexture(source, blackKey);
                Color32[] mainPixels = main.Texture.GetPixels32();
                bool equal = mainPixels.Length == worker.Pixels.Length && main.Alpha.HasTransparentPixels == worker.Alpha.HasTransparentPixels &&
                    main.Alpha.HasPartialAlphaPixels == worker.Alpha.HasPartialAlphaPixels;
                for (int i = 0; equal && i < mainPixels.Length; i++) {
                    Color32 a = mainPixels[i], b = worker.Pixels[i];
                    equal = a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
                }

                if (main.Owned) UnityEngine.Object.Destroy(main.Texture);
                if (equal) {
                    same++;
                } else {
                    differ++;
                    if (differ <= 8) report.Append(" | ").Append(name);
                }
            }

            return $"texverify: {same} identical, {differ} different, {skipped} not prefetchable" + report;
        }

        private static string PreparedKey(string textureName, ModelTextureWrapDebugMode wrap, bool disableBlackKey) =>
            textureName + "|" + (int) wrap + "|" + (disableBlackKey ? "1" : "0");

        private static PreparedTexture PrepareModelTexture(string textureName, RoAssetDatabase assets, RenderSnapshot snap) {
            // the HD pack holds the prepared RGBA (keyed, dilated) upscaled: it loads as it is
            if (RoHdTextures.TryLoad("data/texture/" + textureName, out Texture2D hd, out RoHdTextures.Entry hdEntry)) {
                hd.wrapMode = snap.TextureWrapDebugMode == ModelTextureWrapDebugMode.Clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                return new PreparedTexture {
                    Texture = hd,
                    Alpha = new TextureAlphaInfo { HasTransparentPixels = hdEntry.HasTransparentPixels, HasPartialAlphaPixels = hdEntry.HasPartialAlphaPixels },
                    Owned = true
                };
            }

            PreparedTexture fromPrefetch = TakePrefetched(textureName, assets, snap);
            if (fromPrefetch != null) {
                fromPrefetch.Texture.wrapMode = snap.TextureWrapDebugMode == ModelTextureWrapDebugMode.Clamp
                    ? TextureWrapMode.Clamp
                    : TextureWrapMode.Repeat;
                fromPrefetch.Texture.filterMode = FilterMode.Trilinear;
                return fromPrefetch;
            }

            string[] candidates = ModelTextureCandidates(textureName, out bool blackKeyedFoliage);
            foreach (string candidate in candidates) {
                string path = candidate.Replace("//", "/");
                try {
                    Texture2D texture = LoadTexture(path, assets);
                    if (texture != null) {
                        PreparedTexture prepared = PrepareLoadedTexture(texture, blackKeyedFoliage && !snap.DisableBlackKey);
                        prepared.Texture.wrapMode = snap.TextureWrapDebugMode == ModelTextureWrapDebugMode.Clamp
                            ? TextureWrapMode.Clamp
                            : TextureWrapMode.Repeat;
                        prepared.Texture.filterMode = FilterMode.Bilinear;
                        return prepared;
                    }
                } catch (Exception ex) {
                    Debug.LogWarning($"Could not load RO model texture candidate '{candidate}' for '{textureName}': {ex.Message}");
                    // Missing model textures are expected while path normalization is still being refined.
                }
            }

            if (ReportedMissingTextures.Add(textureName)) {
                Debug.LogWarning($"Missing RO model texture '{textureName}'. Tried: {string.Join(", ", candidates)}");
            }

            return null;
        }

        private static bool IsBlackKeyedFoliageTexture(string fileName) {
            return string.Equals(fileName, "pron-flo-bloc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "pron-flo-f", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The black key (foliage) and the dilation of the transparent pixels' colours on one copy of the pixels, uploaded
        /// once; the alpha found on the way serves the materials. The same pixels as cloning, keying and dilating the
        /// texture step by step (each step a GetPixels32 / SetPixels32 / Apply).
        /// </summary>
        private static PreparedTexture PrepareLoadedTexture(Texture2D texture, bool blackKey) {
            Color32[] pixels;
            try {
                pixels = texture.GetPixels32();
            } catch (Exception ex) {
                Debug.LogWarning($"Could not read texture '{texture.name}' for keying / dilation: {ex.Message}");
                return new PreparedTexture { Texture = texture };
            }

            Color32[] processed = PrepareModelPixels(pixels, texture.width, texture.height, blackKey, out TextureAlphaInfo alpha);
            if (processed == null) {
                return new PreparedTexture { Texture = texture, Alpha = alpha };
            }

            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, true, linear: false) {
                name = texture.name,
                wrapMode = texture.wrapMode,
                filterMode = texture.filterMode == FilterMode.Bilinear ? FilterMode.Trilinear : texture.filterMode,
                anisoLevel = 4
            };
            copy.SetPixels32(processed);
            copy.Apply(true, false);
            return new PreparedTexture { Texture = copy, Alpha = alpha, Owned = true };
        }

        /// <summary>
        /// Pure pixel work (no Unity object, any thread): the alpha of the texture after the black key and, when the
        /// pixels must change (black key, or any transparent pixel to dilate), the changed copy; null = use the texture as is.
        /// </summary>
        internal static Color32[] PrepareModelPixels(Color32[] pixels, int width, int height, bool blackKey, out TextureAlphaInfo alpha) {
            alpha = default;
            Color32[] work = null;
            if (blackKey) {
                work = (Color32[]) pixels.Clone();
                for (int i = 0; i < work.Length; i++) {
                    Color32 pixel = work[i];
                    if (pixel.r <= 4 && pixel.g <= 4 && pixel.b <= 4) {
                        work[i] = new Color32(0, 0, 0, 0);
                    }
                }
            }

            Color32[] scan = work ?? pixels;
            for (int i = 0; i < scan.Length; i++) {
                byte a = scan[i].a;
                if (a == 0) {
                    alpha.HasTransparentPixels = true;
                } else if (a < 250) {
                    alpha.HasTransparentPixels = true;
                    alpha.HasPartialAlphaPixels = true;
                }
            }

            if (!alpha.HasTransparentPixels) {
                return work;
            }

            return DilatePixels(scan, width, height);
        }

        /// <summary>DilateTransparentPixelColors on an array: each fully transparent pixel takes the mean colour of its opaque 8-neighbours.</summary>
        private static Color32[] DilatePixels(Color32[] source, int width, int height) {
            var output = (Color32[]) source.Clone();
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int index = x + y * width;
                    if (source[index].a != 0) {
                        continue;
                    }

                    int r = 0, g = 0, b = 0, count = 0;
                    for (int oy = -1; oy <= 1; oy++) {
                        int ny = y + oy;
                        if (ny < 0 || ny >= height) {
                            continue;
                        }

                        for (int ox = -1; ox <= 1; ox++) {
                            if (ox == 0 && oy == 0) {
                                continue;
                            }

                            int nx = x + ox;
                            if (nx < 0 || nx >= width) {
                                continue;
                            }

                            Color32 neighbor = source[nx + ny * width];
                            if (neighbor.a == 0) {
                                continue;
                            }

                            r += neighbor.r;
                            g += neighbor.g;
                            b += neighbor.b;
                            count++;
                        }
                    }

                    if (count > 0) {
                        output[index] = new Color32((byte) (r / count), (byte) (g / count), (byte) (b / count), 0);
                    }
                }
            }

            return output;
        }

        private static Texture2D LoadTexture(string path, RoAssetDatabase assets) {
            return assets.LoadTexture(path);
        }

        private static void ConfigureMaterial(Material material, float modelAlpha, TextureAlphaInfo alphaInfo, bool twoSided, bool forceAlphaClip) {
            SetColorIfPropertyExists(material, "_BaseColor", new Color(1f, 1f, 1f, modelAlpha));
            SetColorIfPropertyExists(material, "_Color", new Color(1f, 1f, 1f, modelAlpha));

            bool textureHasTransparentPixels = alphaInfo.HasTransparentPixels;
            bool alphaBlend = modelAlpha < 0.999f || alphaInfo.HasPartialAlphaPixels;
            bool alphaClip = forceAlphaClip || textureHasTransparentPixels || alphaBlend;

            SetFloatIfPropertyExists(material, "_Cull", twoSided ? (float) CullMode.Off : (float) CullMode.Back);

            if (alphaBlend) {
                SetAlphaBlend(material, alphaClip ? ModelTransparentAlphaCutoff : 0f);
                return;
            }

            if (alphaClip) {
                SetAlphaClip(material, ModelCutoutAlphaCutoff);
                return;
            }

            SetOpaque(material);
        }

        private static void ApplyRendererShadowState(MeshRenderer renderer, bool twoSided, RenderSnapshot snap) {
            if (renderer == null) {
                return;
            }

            if (snap.ShadowMode == RoShadowRenderMode.Enhanced) {
                bool alphaBlend = GetMaterialMode(renderer.sharedMaterial) == MaterialMode.AlphaBlend;
                renderer.shadowCastingMode = twoSided || alphaBlend ? ShadowCastingMode.TwoSided : ShadowCastingMode.On;
                renderer.receiveShadows = true;
                return;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void ApplyMaterialShadowState(Material material, RenderSnapshot snap) {
            SetFloatIfPropertyExists(material, "_RoReceiveRealtimeShadows", snap.ShadowMode == RoShadowRenderMode.Enhanced ? 1f : 0f);
            SetFloatIfPropertyExists(material, "_RoRealtimeShadowStrength", snap.RealtimeShadowStrength);
            SetFloatIfPropertyExists(material, "_RoModelTopLightStrength", snap.ModelTopLightStrength);
            SetFloatIfPropertyExists(material, "_RoRainWetness", 0f);
            SetFloatIfPropertyExists(material, "_RoRainDarkening", RoRainWeatherSettings.DefaultDarkening);
            SetFloatIfPropertyExists(material, "_RoRainHighlight", RoRainWeatherSettings.DefaultHighlight);
            SetFloatIfPropertyExists(material, "_RoRainPuddleStrength", RoRainWeatherSettings.DefaultPuddleStrength);
            SetFloatIfPropertyExists(material, "_RoRainStreakStrength", 0f);
        }

        private static void ApplyMaterialWindSwayState(Material material, RoWindSwayCatalog.Profile windSwayProfile) {
            if (windSwayProfile == null) {
                SetFloatIfPropertyExists(material, "_RoWindSwayMode", 0f);
                SetFloatIfPropertyExists(material, "_RoWindSwayAmplitude", 0f);
                SetFloatIfPropertyExists(material, "_RoWindSwayFrequency", 0f);
                return;
            }

            SetFloatIfPropertyExists(material, "_RoWindSwayMode", (float) windSwayProfile.Mode);
            SetFloatIfPropertyExists(material, "_RoWindSwayAmplitude", windSwayProfile.Amplitude);
            SetFloatIfPropertyExists(material, "_RoWindSwayFrequency", windSwayProfile.Frequency);
        }

        private static void ApplyMaterialWaterFlowState(Material material, ModelWaterFlowProfile waterFlowProfile) {
            if (!waterFlowProfile.Enabled) {
                SetFloatIfPropertyExists(material, "_RoModelWaterFlowMode", 0f);
                SetFloatIfPropertyExists(material, "_RoModelWaterFlowStrength", 0f);
                SetFloatIfPropertyExists(material, "_RoModelWaterFlowSpeed", 0f);
                return;
            }

            SetFloatIfPropertyExists(material, "_RoModelWaterFlowMode", (float) waterFlowProfile.Mode);
            SetFloatIfPropertyExists(material, "_RoModelWaterFlowStrength", waterFlowProfile.Strength);
            SetFloatIfPropertyExists(material, "_RoModelWaterFlowSpeed", waterFlowProfile.Speed);
        }

        internal static bool MaterialUsesWindSway(Material material) =>
            GetFloatIfPropertyExists(material, "_RoWindSwayMode", 0f) > 0.5f;

        private readonly struct ModelWaterFlowProfile {
            public ModelWaterFlowProfile(int identity, ModelWaterFlowMode mode, float strength, float speed) {
                Identity = identity;
                Mode = mode;
                Strength = Mathf.Clamp01(strength);
                Speed = Mathf.Max(0f, speed);
            }

            public int Identity { get; }
            public ModelWaterFlowMode Mode { get; }
            public float Strength { get; }
            public float Speed { get; }
            public bool Enabled => Identity != 0 && Mode != ModelWaterFlowMode.Off && Strength > 0f && Speed > 0f;
        }

        private enum ModelWaterFlowMode {
            Off = 0,
            Full = 1,
            MaskedCenter = 2
        }

        private static void SetOpaque(Material material) {
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = RoMapRenderQueues.ModelOpaque;
            material.SetOverrideTag("RenderType", "Opaque");
            SetFloatIfPropertyExists(material, "_UseAlphaClip", 0f);
            SetFloatIfPropertyExists(material, "_ZWrite", 1f);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.One);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.Zero);
        }

        private static void SetAlphaClip(Material material, float cutoff) {
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = RoMapRenderQueues.ModelCutout;
            material.SetOverrideTag("RenderType", "TransparentCutout");
            SetFloatIfPropertyExists(material, "_AlphaClip", 1f);
            SetFloatIfPropertyExists(material, "_UseAlphaClip", 1f);
            SetFloatIfPropertyExists(material, "_Cutoff", cutoff);
            SetFloatIfPropertyExists(material, "_ZWrite", 1f);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.One);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.Zero);
        }

        private static void SetAlphaBlend(Material material, float cutoff) {
            if (cutoff > 0f) {
                material.EnableKeyword("_ALPHATEST_ON");
            } else {
                material.DisableKeyword("_ALPHATEST_ON");
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = RoMapRenderQueues.ModelTransparent;
            material.SetOverrideTag("RenderType", "Transparent");
            SetFloatIfPropertyExists(material, "_Surface", 1f);
            SetFloatIfPropertyExists(material, "_AlphaClip", cutoff > 0f ? 1f : 0f);
            SetFloatIfPropertyExists(material, "_UseAlphaClip", cutoff > 0f ? 1f : 0f);
            SetFloatIfPropertyExists(material, "_Cutoff", cutoff);
            SetFloatIfPropertyExists(material, "_ZWrite", 1f);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
        }

        internal struct TextureAlphaInfo {
            public bool HasTransparentPixels;
            public bool HasPartialAlphaPixels;
        }

        private static void SetColorIfPropertyExists(Material material, string property, Color color) {
            if (material.HasProperty(property)) {
                material.SetColor(property, color);
            }
        }

        private static void SetFloatIfPropertyExists(Material material, string property, float value) {
            if (material.HasProperty(property)) {
                material.SetFloat(property, value);
            }
        }

        private static void SetTextureIfPropertyExists(Material material, string property, Texture texture) {
            if (material.HasProperty(property)) {
                material.SetTexture(property, texture);
            }
        }

        private static bool LooksLikeFlatOverlay(Bounds bounds) {
            Vector3 size = bounds.size;
            float minAxis = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return minAxis <= 0.35f && maxAxis >= 2f;
        }

        private static bool IsWindSwayCandidateSurface(RoModelSurfaceInfo surface) {
            return surface != null && surface.MaterialMode == MaterialMode.Cutout && surface.TwoSided;
        }

        private static string NormalizeWindSwayTextureBucketKey(string textureName) {
            return string.IsNullOrWhiteSpace(textureName)
                ? string.Empty
                : textureName.Trim().Replace('\\', '/');
        }

        private static void AddSurfaceBucket(Dictionary<string, SurfaceAuditBucket> buckets, RoModelSurfaceInfo surface) {
            string key = $"{surface.MaterialMode}|q={surface.MaterialRenderQueue}|cull={surface.MaterialCull}|z={surface.MaterialZWrite}|{surface.TextureName}|{surface.ModelFile}|{surface.NodeName}|two={surface.TwoSided}";
            if (!buckets.TryGetValue(key, out SurfaceAuditBucket bucket)) {
                bucket = new SurfaceAuditBucket(surface);
                buckets.Add(key, bucket);
            }

            bucket.Count++;
        }

        private static void AppendSurfaceBuckets(StringBuilder builder, string title, Dictionary<string, SurfaceAuditBucket> buckets, int totalCount, int maxItems) {
            builder.AppendLine();
            builder.AppendLine($"{title}: {totalCount} surface(s), {buckets.Count} bucket(s)");
            var list = new List<SurfaceAuditBucket>(buckets.Values);
            list.Sort((a, b) => b.Count.CompareTo(a.Count));

            for (int i = 0; i < Mathf.Min(maxItems, list.Count); i++) {
                SurfaceAuditBucket bucket = list[i];
                builder.AppendLine(
                    $"- count={bucket.Count}, mode={bucket.Mode}, queue={bucket.Queue}, cull={bucket.Cull:0.###}, zWrite={bucket.ZWrite:0.###}, texture='{bucket.TextureName}', model='{bucket.ModelFile}', node='{bucket.NodeName}', twoSided={bucket.TwoSided}, bounds={bucket.BoundsSize}"
                );
            }
        }

        internal static MaterialMode GetMaterialMode(Material material) {
            if (material == null) {
                return MaterialMode.Opaque;
            }

            if (material.renderQueue >= RoMapRenderQueues.ModelTransparent) {
                return MaterialMode.AlphaBlend;
            }

            if (GetFloatIfPropertyExists(material, "_UseAlphaClip", 0f) > 0.5f) {
                return MaterialMode.Cutout;
            }

            return MaterialMode.Opaque;
        }

        internal static float GetFloatIfPropertyExists(Material material, string property, float fallback = 0f) {
            return material != null && material.HasProperty(property) ? material.GetFloat(property) : fallback;
        }

        public sealed class WindSwayCandidateBucket {
            internal WindSwayCandidateBucket(RoModelSurfaceInfo surface) {
                TextureName = surface.TextureName;
                ExampleModelFile = surface.ModelFile;
                ExampleNodeName = surface.NodeName;
                Add(surface);
            }

            public string TextureName { get; }
            public string ExampleModelFile { get; }
            public string ExampleNodeName { get; }
            public int SurfaceCount { get; private set; }
            public int TotalVertexCount { get; private set; }

            internal void Add(RoModelSurfaceInfo surface) {
                if (surface == null) {
                    return;
                }

                SurfaceCount++;
                TotalVertexCount += Mathf.Max(0, surface.VertexCount);
            }
        }

        private sealed class MaterialAuditBucket {
            public MaterialAuditBucket(string textureName, MaterialMode mode, int queue, float cull, float zWrite) {
                TextureName = textureName;
                Mode = mode;
                Queue = queue;
                Cull = cull;
                ZWrite = zWrite;
            }

            public string TextureName { get; }
            public MaterialMode Mode { get; }
            public int Queue { get; }
            public float Cull { get; }
            public float ZWrite { get; }
            public int Count { get; set; }
        }

        private sealed class SurfaceAuditBucket {
            public SurfaceAuditBucket(RoModelSurfaceInfo surface) {
                TextureName = surface.TextureName;
                ModelFile = surface.ModelFile;
                NodeName = surface.NodeName;
                Mode = surface.MaterialMode;
                Queue = surface.MaterialRenderQueue;
                Cull = surface.MaterialCull;
                ZWrite = surface.MaterialZWrite;
                TwoSided = surface.TwoSided;
                BoundsSize = surface.LocalMeshBounds.size;
            }

            public string TextureName { get; }
            public string ModelFile { get; }
            public string NodeName { get; }
            public MaterialMode Mode { get; }
            public int Queue { get; }
            public float Cull { get; }
            public float ZWrite { get; }
            public bool TwoSided { get; }
            public Vector3 BoundsSize { get; }
            public int Count { get; set; }
        }
    }

    public sealed class RoModelSurfaceInfo : MonoBehaviour {
        [SerializeField] private string modelFile;
        [SerializeField] private string nodeName;
        [SerializeField] private string textureName;
        [SerializeField] private bool twoSided;
        [SerializeField] private int vertexCount;
        [SerializeField] private int triangleCount;
        [SerializeField] private int instanceIndex;
        [SerializeField] private Vector3 descriptorPosition;
        [SerializeField] private Vector3 descriptorRotation;
        [SerializeField] private Vector3 descriptorScale;
        [SerializeField] private Bounds localMeshBounds;
        [SerializeField] private RoModelRenderer.MaterialMode materialMode;
        [SerializeField] private int materialRenderQueue;
        [SerializeField] private float materialCull;
        [SerializeField] private float materialZWrite;
        [SerializeField] private float materialSrcBlend;
        [SerializeField] private float materialDstBlend;
        [SerializeField] private float materialCutoff;
        [SerializeField] private float materialUseAlphaClip;
        [SerializeField] private float materialAlpha;

        public string ModelFile => modelFile;
        public string NodeName => nodeName;
        public string TextureName => textureName;
        public bool TwoSided => twoSided;
        public int VertexCount => vertexCount;
        public int TriangleCount => triangleCount;
        public int InstanceIndex => instanceIndex;
        public Vector3 DescriptorPosition => descriptorPosition;
        public Vector3 DescriptorRotation => descriptorRotation;
        public Vector3 DescriptorScale => descriptorScale;
        public Bounds LocalMeshBounds => localMeshBounds;
        public RoModelRenderer.MaterialMode MaterialMode => materialMode;
        public int MaterialRenderQueue => materialRenderQueue;
        public float MaterialCull => materialCull;
        public float MaterialZWrite => materialZWrite;
        public float MaterialSrcBlend => materialSrcBlend;
        public float MaterialDstBlend => materialDstBlend;
        public float MaterialCutoff => materialCutoff;
        public float MaterialUseAlphaClip => materialUseAlphaClip;
        public float MaterialAlpha => materialAlpha;

        public void Initialize(
            string modelFile,
            string nodeName,
            string textureName,
            bool twoSided,
            int vertexCount,
            int triangleCount,
            int instanceIndex,
            float[] descriptorPosition,
            float[] descriptorRotation,
            float[] descriptorScale,
            Bounds localMeshBounds,
            Material material
        ) {
            this.modelFile = modelFile;
            this.nodeName = nodeName;
            this.textureName = textureName;
            this.twoSided = twoSided;
            this.vertexCount = vertexCount;
            this.triangleCount = triangleCount;
            this.instanceIndex = instanceIndex;
            this.descriptorPosition = ToVector3(descriptorPosition);
            this.descriptorRotation = ToVector3(descriptorRotation);
            this.descriptorScale = ToVector3(descriptorScale);
            this.localMeshBounds = localMeshBounds;
            materialMode = RoModelRenderer.GetMaterialMode(material);
            materialRenderQueue = material != null ? material.renderQueue : 0;
            materialCull = RoModelRenderer.GetFloatIfPropertyExists(material, "_Cull", 0f);
            materialZWrite = RoModelRenderer.GetFloatIfPropertyExists(material, "_ZWrite", 0f);
            materialSrcBlend = RoModelRenderer.GetFloatIfPropertyExists(material, "_SrcBlend", 0f);
            materialDstBlend = RoModelRenderer.GetFloatIfPropertyExists(material, "_DstBlend", 0f);
            materialCutoff = RoModelRenderer.GetFloatIfPropertyExists(material, "_Cutoff", 0f);
            materialUseAlphaClip = RoModelRenderer.GetFloatIfPropertyExists(material, "_UseAlphaClip", 0f);
            materialAlpha = material != null ? material.color.a : 1f;
        }

        public void UpdateInstance(
            int instanceIndex,
            float[] descriptorPosition,
            float[] descriptorRotation,
            float[] descriptorScale
        ) {
            this.instanceIndex = instanceIndex;
            this.descriptorPosition = ToVector3(descriptorPosition);
            this.descriptorRotation = ToVector3(descriptorRotation);
            this.descriptorScale = ToVector3(descriptorScale);
        }

        private static Vector3 ToVector3(float[] values) {
            if (values == null || values.Length < 3) {
                return Vector3.zero;
            }

            return new Vector3(values[0], values[1], values[2]);
        }
    }

}
