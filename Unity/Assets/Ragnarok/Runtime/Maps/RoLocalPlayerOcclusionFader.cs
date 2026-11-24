using System.Collections.Generic;
using Ragnarok.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Maps {
    public sealed class RoLocalPlayerOcclusionFader : MonoBehaviour {
        private const float CandidateMinimumHeight = 1f;
        private const float CandidateMinimumFootprint = 0.5f;
        private const float CandidateMinimumTallThinHeight = 1.35f;
        private const float CandidateMinimumTallThinFootprint = 0.08f;
        private const float PlayerDepthEpsilon = 0.02f;
        private const float AlphaHitCutoffFallback = 0.05f;
        private const float RayTriangleEpsilon = 0.000001f;
        private const float MinimumBoundsRayThickness = 0.025f;
        /// <summary>
        /// A faded model draws after every actor layer and effect (the client draw order hands out 100 and up), below the
        /// weather (25000): actor parts behind it show through it steadily. Following the local player's own order made it
        /// change sides with each NPC as the screen-y orders moved while walking, so their parts behind it flickered.
        /// </summary>
        private const int FadedModelSortingOrder = 20000;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int UseAlphaClipId = Shader.PropertyToID("_UseAlphaClip");
        private static readonly int ShadowKeepsAlphaClipId = Shader.PropertyToID("_RoShadowKeepsAlphaClip");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly Vector2[] PlayerOcclusionSamplePoints = {
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.75f),
            new Vector2(0.5f, 0.25f),
            new Vector2(0.25f, 0.5f),
            new Vector2(0.75f, 0.5f)
        };
        private static readonly Vector2[] RendererOverlapOcclusionSamplePoints = {
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.2f),
            new Vector2(0.5f, 0.8f),
            new Vector2(0.2f, 0.5f),
            new Vector2(0.8f, 0.5f),
            new Vector2(0.2f, 0.2f),
            new Vector2(0.8f, 0.2f),
            new Vector2(0.2f, 0.8f),
            new Vector2(0.8f, 0.8f)
        };

        /// <summary>The map's fade candidates with their world bounds, taken once: map models do not move (animated models
        /// are no candidates), so the per-probe loop reads no renderer bounds.</summary>
        private readonly List<Candidate> candidates = new List<Candidate>(256);
        private readonly List<MeshRenderer> rendererScratch = new List<MeshRenderer>(256);
        /// <summary>The local player's visual renderers, collected once per probe.</summary>
        private readonly List<MeshRenderer> playerRenderers = new List<MeshRenderer>(8);
        /// <summary>Candidate mesh data read once (static meshes); the player's sprite meshes change and are read per ray.</summary>
        private readonly Dictionary<Mesh, CachedMesh> meshCache = new Dictionary<Mesh, CachedMesh>();
        private readonly HashSet<MeshRenderer> desiredFades = new HashSet<MeshRenderer>();
        private readonly List<MeshRenderer> activeScratch = new List<MeshRenderer>(32);
        // mesh data read per ray (Mesh.vertices / triangles / uv each copy a new array per call)
        private readonly List<Vector3> meshVertexScratch = new List<Vector3>(64);
        private readonly List<Vector2> meshUvScratch = new List<Vector2>(64);
        private readonly List<int> meshTriangleScratch = new List<int>(96);
        private readonly List<int> meshSubTriangleScratch = new List<int>(96);
        private readonly Dictionary<MeshRenderer, FadeEntry> activeFades = new Dictionary<MeshRenderer, FadeEntry>(32);
        private readonly Dictionary<Material, Material> fadeMaterialCache = new Dictionary<Material, Material>();
        private readonly Ray[] playerOcclusionRays = new Ray[PlayerOcclusionSamplePoints.Length + RendererOverlapOcclusionSamplePoints.Length];
        private readonly float[] playerOcclusionDepths = new float[PlayerOcclusionSamplePoints.Length + RendererOverlapOcclusionSamplePoints.Length];

        private Transform modelRoot;
        private Transform localPlayer;
        private UnityEngine.Camera renderCamera;
        private RoRuntimeMapRenderSettings renderSettings;
        private MaterialPropertyBlock propertyBlock;
        private float nextProbeTime;

        public void Configure(
            Transform modelRoot,
            Transform localPlayer,
            UnityEngine.Camera renderCamera,
            RoRuntimeMapRenderSettings renderSettings
        ) {
            bool rootChanged = this.modelRoot != modelRoot;
            this.modelRoot = modelRoot;
            this.localPlayer = localPlayer;
            this.renderCamera = renderCamera;
            this.renderSettings = renderSettings;
            if (rootChanged) {
                RebuildCandidates();
                nextProbeTime = 0f;
            }
        }

        public void Clear() {
            RestoreAll();
            candidates.Clear();
            meshCache.Clear();
            rendererScratch.Clear();
            desiredFades.Clear();
            modelRoot = null;
            localPlayer = null;
            renderCamera = null;
            renderSettings = null;
            nextProbeTime = 0f;
        }

        public static bool IsFadeCandidate(RoModelSurfaceInfo surface, MeshRenderer renderer) {
            if (surface == null || renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) {
                return false;
            }

            if (surface.MaterialMode != RoModelRenderer.MaterialMode.Opaque &&
                surface.MaterialMode != RoModelRenderer.MaterialMode.Cutout &&
                surface.MaterialMode != RoModelRenderer.MaterialMode.AlphaBlend) {
                return false;
            }

            if (renderer.GetComponentInParent<RoModelAnimationController>() != null) {
                return false;
            }

            Bounds bounds = renderer.bounds;
            Vector3 size = bounds.size;
            float footprint = Mathf.Max(size.x, size.z);
            return size.y >= CandidateMinimumHeight &&
                (footprint >= CandidateMinimumFootprint ||
                    (size.y >= CandidateMinimumTallThinHeight && footprint >= CandidateMinimumTallThinFootprint));
        }

        private void LateUpdate() {
            Tick(Time.deltaTime, Time.unscaledTime);
        }

        private void OnDisable() {
            RestoreAll();
        }

        private void OnDestroy() {
            RestoreAll();
            foreach (Material material in fadeMaterialCache.Values) {
                DestroyRuntimeObject(material);
            }

            fadeMaterialCache.Clear();
        }

        internal void TickForTests(float deltaSeconds, float nowSeconds) {
            Tick(deltaSeconds, nowSeconds);
        }

        internal bool IsRendererFadingForTests(MeshRenderer renderer) {
            return renderer != null && activeFades.ContainsKey(renderer);
        }

        private void Tick(float deltaSeconds, float nowSeconds) {
            if (!IsConfigured()) {
                RestoreAll();
                return;
            }

            float interval = Mathf.Max(0.01f, renderSettings.localPlayerOcclusionProbeIntervalSeconds);
            if (nowSeconds >= nextProbeTime) {
                ProbeOccluders();
                nextProbeTime = nowSeconds + interval;
            }

            AnimateFades(Mathf.Max(0f, deltaSeconds));
        }

        private bool IsConfigured() {
            return renderSettings != null &&
                renderSettings.enableLocalPlayerOcclusionFade &&
                modelRoot != null &&
                localPlayer != null &&
                renderCamera != null;
        }

        private void RebuildCandidates() {
            RestoreAll();
            candidates.Clear();
            meshCache.Clear();
            if (modelRoot == null) {
                return;
            }

            // candidates in the static model batches are drawn by their batch (their own renderer is off)
            RoStaticModelBatchSet batchSet = modelRoot.GetComponentInChildren<RoStaticModelBatchSet>(true);
            rendererScratch.Clear();
            modelRoot.GetComponentsInChildren(true, rendererScratch);
            for (int i = 0; i < rendererScratch.Count; i++) {
                MeshRenderer renderer = rendererScratch[i];
                if (batchSet != null && batchSet.TryGetMember(renderer, out RoStaticModelBatchSet.Member member)) {
                    candidates.Add(new Candidate(renderer, WorldBounds(renderer), member));
                    continue;
                }

                RoModelSurfaceInfo surface = renderer != null ? renderer.FindComponent<RoModelSurfaceInfo>() : null;
                if (IsFadeCandidate(surface, renderer)) {
                    candidates.Add(new Candidate(renderer, WorldBounds(renderer)));
                }
            }

            rendererScratch.Clear();
        }

        private void ProbeOccluders() {
            desiredFades.Clear();
            if (!TryGetLocalPlayerScreenRect(out Rect playerRect, out float playerDepth)) {
                return;
            }

            Transform playerVisualRoot = TryResolveLocalPlayerVisualRoot(out Transform resolvedVisualRoot)
                ? resolvedVisualRoot
                : null;
            playerRenderers.Clear();
            if (playerVisualRoot != null) {
                playerVisualRoot.GetComponentsInChildren(true, playerRenderers);
            }

            int playerSampleCount = BuildPlayerOcclusionSamples(playerRect, playerDepth, playerVisualRoot);
            if (playerSampleCount == 0) {
                return;
            }

            Rect playerVisualRect = CreatePlayerVisualSampleRect(playerRect);
            ResolveOcclusionPrism(playerRect, playerDepth, out Vector3 prismStart, out Vector3 prismEnd, out float prismRadius);
            int maxActive = Mathf.Max(1, renderSettings.localPlayerOcclusionMaxActiveRenderers);
            for (int i = 0; i < candidates.Count && desiredFades.Count < maxActive; i++) {
                Candidate candidate = candidates[i];
                MeshRenderer renderer = candidate.Renderer;
                Bounds rendererBounds = candidate.Bounds;
                if (!SegmentHitsBox(prismStart, prismEnd, rendererBounds.min - (Vector3.one * prismRadius), rendererBounds.max + (Vector3.one * prismRadius))) {
                    continue;
                }

                if (!IsCandidateShown(candidate)) {
                    continue;
                }

                if (!TryProjectBounds(rendererBounds, out Rect rendererRect, out _, out _)) {
                    continue;
                }

                if (!rendererRect.Overlaps(playerRect)) {
                    continue;
                }

                int sampleCount = AppendRendererOverlapOcclusionSamples(
                    playerVisualRect,
                    rendererRect,
                    playerDepth,
                    playerVisualRoot,
                    playerSampleCount);
                if (MeshOccludesPlayer(renderer, rendererBounds, sampleCount)) {
                    desiredFades.Add(renderer);
                    EnsureFadeEntry(candidate);
                }
            }
        }

        private bool TryGetLocalPlayerScreenRect(out Rect rect, out float depth) {
            Bounds bounds;
            if (!TryGetLocalPlayerVisualBounds(out bounds)) {
                bounds = new Bounds(
                    localPlayer.position + Vector3.up * 0.9f,
                    new Vector3(0.8f, 1.8f, 0.8f));
            }

            if (!TryProjectBounds(bounds, out rect, out _, out _)) {
                depth = 0f;
                return false;
            }

            float margin = Mathf.Max(0f, renderSettings.localPlayerOcclusionScreenMarginPixels);
            rect.xMin -= margin;
            rect.xMax += margin;
            rect.yMin -= margin;
            rect.yMax += margin;
            depth = renderCamera.WorldToScreenPoint(localPlayer.position).z;
            return depth > 0f;
        }

        private bool TryGetLocalPlayerVisualBounds(out Bounds bounds) {
            bounds = default;
            if (!TryResolveLocalPlayerVisualRoot(out Transform visualRoot)) {
                return false;
            }

            rendererScratch.Clear();
            visualRoot.GetComponentsInChildren(true, rendererScratch);
            bool hasBounds = false;
            for (int i = 0; i < rendererScratch.Count; i++) {
                MeshRenderer renderer = rendererScratch[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) {
                    continue;
                }

                if (!hasBounds) {
                    bounds = renderer.bounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            rendererScratch.Clear();
            return hasBounds;
        }

        private bool TryResolveLocalPlayerVisualRoot(out Transform visualRoot) {
            visualRoot = null;
            if (localPlayer == null) {
                return false;
            }

            RoActorLayeredVisual layeredVisual = localPlayer.GetComponentInChildren<RoActorLayeredVisual>(true);
            if (layeredVisual != null) {
                visualRoot = layeredVisual.transform;
                return true;
            }

            RoActorSpriteVisual spriteVisual = localPlayer.GetComponentInChildren<RoActorSpriteVisual>(true);
            if (spriteVisual != null) {
                visualRoot = spriteVisual.transform;
                return true;
            }

            return false;
        }

        private bool TryProjectBounds(Bounds bounds, out Rect rect, out float minDepth, out float maxDepth) {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            bool hasPoint = false;
            float minX = 0f;
            float minY = 0f;
            float maxX = 0f;
            float maxY = 0f;
            minDepth = float.PositiveInfinity;
            maxDepth = float.NegativeInfinity;

            for (int corner = 0; corner < 8; corner++) {
                var point = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 screen = renderCamera.WorldToScreenPoint(point);
                if (screen.z <= 0f || float.IsNaN(screen.x) || float.IsNaN(screen.y)) {
                    continue;
                }

                if (!hasPoint) {
                    minX = maxX = screen.x;
                    minY = maxY = screen.y;
                    hasPoint = true;
                } else {
                    minX = Mathf.Min(minX, screen.x);
                    minY = Mathf.Min(minY, screen.y);
                    maxX = Mathf.Max(maxX, screen.x);
                    maxY = Mathf.Max(maxY, screen.y);
                }

                minDepth = Mathf.Min(minDepth, screen.z);
                maxDepth = Mathf.Max(maxDepth, screen.z);
            }

            if (!hasPoint) {
                rect = default;
                minDepth = 0f;
                maxDepth = 0f;
                return false;
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private int BuildPlayerOcclusionSamples(Rect playerRect, float fallbackDepth, Transform visualRoot) {
            Rect sampleRect = CreatePlayerOcclusionSampleRect(playerRect);
            int sampleCount = 0;
            for (int i = 0; i < PlayerOcclusionSamplePoints.Length; i++) {
                Vector2 normalized = PlayerOcclusionSamplePoints[i];
                var screenPoint = new Vector2(
                    Mathf.Lerp(sampleRect.xMin, sampleRect.xMax, normalized.x),
                    Mathf.Lerp(sampleRect.yMin, sampleRect.yMax, normalized.y));
                Ray worldRay = renderCamera.ScreenPointToRay(screenPoint);
                if (TryResolveOcclusionSampleDepth(visualRoot, worldRay, fallbackDepth, out float visualDepth)) {
                    playerOcclusionRays[sampleCount] = worldRay;
                    playerOcclusionDepths[sampleCount] = visualDepth;
                    sampleCount++;
                }
            }

            return sampleCount;
        }

        private int AppendRendererOverlapOcclusionSamples(
            Rect playerVisualRect,
            Rect rendererRect,
            float fallbackDepth,
            Transform visualRoot,
            int sampleCount
        ) {
            Rect overlap = IntersectRects(playerVisualRect, rendererRect);
            if (overlap.width <= 0f || overlap.height <= 0f) {
                return sampleCount;
            }

            for (int i = 0; i < RendererOverlapOcclusionSamplePoints.Length && sampleCount < playerOcclusionRays.Length; i++) {
                Vector2 normalized = RendererOverlapOcclusionSamplePoints[i];
                var screenPoint = new Vector2(
                    Mathf.Lerp(overlap.xMin, overlap.xMax, normalized.x),
                    Mathf.Lerp(overlap.yMin, overlap.yMax, normalized.y));
                Ray worldRay = renderCamera.ScreenPointToRay(screenPoint);
                if (!TryResolveOcclusionSampleDepth(visualRoot, worldRay, fallbackDepth, out float visualDepth)) {
                    continue;
                }

                playerOcclusionRays[sampleCount] = worldRay;
                playerOcclusionDepths[sampleCount] = visualDepth;
                sampleCount++;
            }

            return sampleCount;
        }

        private bool TryResolveOcclusionSampleDepth(
            Transform visualRoot,
            Ray worldRay,
            float fallbackDepth,
            out float visualDepth
        ) {
            if (visualRoot != null && TryGetVisualDepthAtRay(visualRoot, worldRay, out visualDepth)) {
                return true;
            }

            if (fallbackDepth > 0f) {
                visualDepth = fallbackDepth;
                return true;
            }

            visualDepth = 0f;
            return false;
        }

        private bool TryGetVisualDepthAtRay(Transform visualRoot, Ray worldRay, out float depth) {
            depth = float.PositiveInfinity;
            if (visualRoot == null) {
                depth = 0f;
                return false;
            }

            for (int i = 0; i < playerRenderers.Count; i++) {
                MeshRenderer renderer = playerRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) {
                    continue;
                }

                if (!TryIntersectExpandedBounds(renderer.bounds, worldRay, out _)) {
                    continue;
                }

                if (TryGetClosestMeshDepthAtRay(renderer, worldRay, cached: false, out float rendererDepth)) {
                    depth = Mathf.Min(depth, rendererDepth);
                }
            }

            if (float.IsPositiveInfinity(depth)) {
                depth = 0f;
                return false;
            }

            return depth > 0f;
        }

        private bool TryGetClosestMeshDepthAtRay(MeshRenderer renderer, Ray worldRay, bool cached, out float depth) {
            depth = float.PositiveInfinity;
            if (!TryGetReadableMesh(renderer, out Mesh mesh)) {
                return false;
            }

            List<Vector3> vertices;
            List<Vector2> uvs;
            List<int> triangles;
            if (cached) {
                if (!meshCache.TryGetValue(mesh, out CachedMesh data)) {
                    data = new CachedMesh();
                    ReadMesh(mesh, data.Vertices, data.Uvs, data.Triangles);
                    meshCache.Add(mesh, data);
                }

                vertices = data.Vertices;
                uvs = data.Uvs;
                triangles = data.Triangles;
            } else {
                ReadMesh(mesh, meshVertexScratch, meshUvScratch, meshTriangleScratch);
                vertices = meshVertexScratch;
                uvs = meshUvScratch;
                triangles = meshTriangleScratch;
            }

            if (vertices.Count == 0 || triangles.Count < 3) {
                return false;
            }

            Transform rendererTransform = renderer.transform;
            Matrix4x4 worldToLocal = rendererTransform.worldToLocalMatrix;
            var localRay = new Ray(
                worldToLocal.MultiplyPoint(worldRay.origin),
                worldToLocal.MultiplyVector(worldRay.direction).normalized);
            if (!TryRayIntersectsMeshDepth(
                localRay,
                rendererTransform,
                vertices,
                uvs,
                triangles,
                renderer.sharedMaterial,
                out depth)) {
                depth = 0f;
                return false;
            }

            return depth > 0f;
        }

        private bool MeshOccludesPlayer(MeshRenderer renderer, Bounds rendererBounds, int playerSampleCount) {
            bool canUseMesh = TryGetReadableMesh(renderer, out _);
            for (int i = 0; i < playerSampleCount; i++) {
                Ray worldRay = playerOcclusionRays[i];
                if (!TryIntersectExpandedBounds(rendererBounds, worldRay, out float boundsDistance)) {
                    continue;
                }

                if (canUseMesh) {
                    if (TryGetClosestMeshDepthAtRay(renderer, worldRay, cached: true, out float hitDepth) &&
                        hitDepth < playerOcclusionDepths[i] - PlayerDepthEpsilon) {
                        return true;
                    }

                    continue;
                }

                Vector3 boundsHit = worldRay.GetPoint(boundsDistance);
                float boundsHitDepth = renderCamera.WorldToScreenPoint(boundsHit).z;
                if (boundsHitDepth > 0f &&
                    boundsHitDepth < playerOcclusionDepths[i] - PlayerDepthEpsilon) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Every triangle sub-mesh (like Mesh.triangles), the vertices and the first UV set into the lists.</summary>
        private void ReadMesh(Mesh mesh, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles) {
            mesh.GetVertices(vertices);
            mesh.GetUVs(0, uvs);
            triangles.Clear();
            for (int sub = 0; sub < mesh.subMeshCount; sub++) {
                if (mesh.GetTopology(sub) == MeshTopology.Triangles) {
                    mesh.GetTriangles(meshSubTriangleScratch, sub, applyBaseVertex: true);
                    triangles.AddRange(meshSubTriangleScratch);
                }
            }
        }

        /// <summary>
        /// The space a model must cross to hide the player: the segment from the camera to the player's rect centre at the
        /// player's depth (plus a unit beyond it), widened by the rect's half-diagonal there (the rect already carries the
        /// screen margin). The perspective frustum through the rect narrows toward the camera, so it lies inside this
        /// prism: a model that misses it cannot be one the screen-space tests would pick.
        /// </summary>
        private void ResolveOcclusionPrism(Rect playerRect, float playerDepth, out Vector3 start, out Vector3 end, out float radius) {
            Vector3 centre = renderCamera.ScreenToWorldPoint(new Vector3(playerRect.center.x, playerRect.center.y, playerDepth));
            radius = 0f;
            for (int corner = 0; corner < 4; corner++) {
                Vector3 point = renderCamera.ScreenToWorldPoint(new Vector3(
                    (corner & 1) == 0 ? playerRect.xMin : playerRect.xMax,
                    (corner & 2) == 0 ? playerRect.yMin : playerRect.yMax,
                    playerDepth));
                radius = Mathf.Max(radius, Vector3.Distance(point, centre));
            }

            Vector3 forward = renderCamera.transform.forward;
            start = renderCamera.orthographic ? centre - (forward * playerDepth) : renderCamera.transform.position;
            end = centre + forward;
        }

        /// <summary>The renderer's world bounds from its mesh: a batched candidate's own renderer is off, and a disabled
        /// renderer's bounds are not kept up to date.</summary>
        private static Bounds WorldBounds(MeshRenderer renderer) {
            MeshFilter filter = renderer.FindComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) {
                return renderer.bounds;
            }

            Bounds local = mesh.bounds;
            Matrix4x4 toWorld = renderer.localToWorldMatrix;
            Vector3 min = local.min, max = local.max;
            var bounds = new Bounds(toWorld.MultiplyPoint3x4(min), Vector3.zero);
            for (int corner = 1; corner < 8; corner++) {
                bounds.Encapsulate(toWorld.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z)));
            }

            return bounds;
        }

        /// <summary>Slab test: does the segment a→b touch the box?</summary>
        private static bool SegmentHitsBox(Vector3 a, Vector3 b, Vector3 min, Vector3 max) {
            float t0 = 0f, t1 = 1f;
            for (int axis = 0; axis < 3; axis++) {
                float origin = a[axis];
                float delta = b[axis] - origin;
                if (Mathf.Abs(delta) < 1e-6f) {
                    if (origin < min[axis] || origin > max[axis]) {
                        return false;
                    }

                    continue;
                }

                float inverse = 1f / delta;
                float near = (min[axis] - origin) * inverse;
                float far = (max[axis] - origin) * inverse;
                if (near > far) {
                    (near, far) = (far, near);
                }

                t0 = Mathf.Max(t0, near);
                t1 = Mathf.Min(t1, far);
                if (t0 > t1) {
                    return false;
                }
            }

            return true;
        }

        private static bool TryIntersectExpandedBounds(Bounds bounds, Ray ray, out float distance) {
            Vector3 extents = bounds.extents;
            extents.x = Mathf.Max(extents.x, MinimumBoundsRayThickness);
            extents.y = Mathf.Max(extents.y, MinimumBoundsRayThickness);
            extents.z = Mathf.Max(extents.z, MinimumBoundsRayThickness);
            bounds.extents = extents;
            return bounds.IntersectRay(ray, out distance);
        }

        private static bool TryGetReadableMesh(MeshRenderer renderer, out Mesh mesh) {
            MeshFilter filter = renderer != null ? renderer.FindComponent<MeshFilter>() : null;
            mesh = filter != null ? filter.sharedMesh : null;
            return mesh != null && mesh.isReadable;
        }

        private Rect CreatePlayerOcclusionSampleRect(Rect playerRect) {
            Rect sampleRect = CreatePlayerVisualSampleRect(playerRect);
            float insetX = Mathf.Max(0f, sampleRect.width * 0.15f);
            float insetY = Mathf.Max(0f, sampleRect.height * 0.08f);
            if (sampleRect.width > (insetX * 2f) + 1f && sampleRect.height > (insetY * 2f) + 1f) {
                sampleRect.xMin += insetX;
                sampleRect.xMax -= insetX;
                sampleRect.yMin += insetY;
                sampleRect.yMax -= insetY;
            }

            return sampleRect;
        }

        private Rect CreatePlayerVisualSampleRect(Rect playerRect) {
            float margin = renderSettings != null
                ? Mathf.Max(0f, renderSettings.localPlayerOcclusionScreenMarginPixels)
                : 0f;
            Rect sampleRect = playerRect;
            if (sampleRect.width > (margin * 2f) + 1f && sampleRect.height > (margin * 2f) + 1f) {
                sampleRect.xMin += margin;
                sampleRect.xMax -= margin;
                sampleRect.yMin += margin;
                sampleRect.yMax -= margin;
            }

            return sampleRect;
        }

        private static Rect IntersectRects(Rect first, Rect second) {
            float xMin = Mathf.Max(first.xMin, second.xMin);
            float yMin = Mathf.Max(first.yMin, second.yMin);
            float xMax = Mathf.Min(first.xMax, second.xMax);
            float yMax = Mathf.Min(first.yMax, second.yMax);
            if (xMax <= xMin || yMax <= yMin) {
                return default;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private bool TryRayIntersectsMeshDepth(
            Ray localRay,
            Transform rendererTransform,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Material material,
            out float depth
        ) {
            depth = float.PositiveInfinity;
            for (int triangle = 0; triangle <= triangles.Count - 3; triangle += 3) {
                int index0 = triangles[triangle];
                int index1 = triangles[triangle + 1];
                int index2 = triangles[triangle + 2];
                if (!RayIntersectsTriangle(
                    localRay,
                    vertices[index0],
                    vertices[index1],
                    vertices[index2],
                    out float distance,
                    out float barycentricU,
                    out float barycentricV)) {
                    continue;
                }

                if (!IsTextureHitVisible(material, uvs, index0, index1, index2, barycentricU, barycentricV)) {
                    continue;
                }

                Vector3 worldHit = rendererTransform.TransformPoint(localRay.origin + (localRay.direction * distance));
                float hitDepth = renderCamera.WorldToScreenPoint(worldHit).z;
                if (hitDepth > 0f) {
                    depth = Mathf.Min(depth, hitDepth);
                }
            }

            if (float.IsPositiveInfinity(depth)) {
                depth = 0f;
                return false;
            }

            return true;
        }

        private static bool RayIntersectsTriangle(
            Ray ray,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            out float distance
        ) {
            return RayIntersectsTriangle(ray, v0, v1, v2, out distance, out _, out _);
        }

        private static bool RayIntersectsTriangle(
            Ray ray,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            out float distance,
            out float barycentricU,
            out float barycentricV
        ) {
            distance = 0f;
            barycentricU = 0f;
            barycentricV = 0f;
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 p = Vector3.Cross(ray.direction, edge2);
            float determinant = Vector3.Dot(edge1, p);
            if (determinant > -RayTriangleEpsilon && determinant < RayTriangleEpsilon) {
                return false;
            }

            float inverseDeterminant = 1f / determinant;
            Vector3 t = ray.origin - v0;
            float u = Vector3.Dot(t, p) * inverseDeterminant;
            if (u < 0f || u > 1f) {
                return false;
            }

            Vector3 q = Vector3.Cross(t, edge1);
            float v = Vector3.Dot(ray.direction, q) * inverseDeterminant;
            if (v < 0f || u + v > 1f) {
                return false;
            }

            distance = Vector3.Dot(edge2, q) * inverseDeterminant;
            barycentricU = u;
            barycentricV = v;
            return distance > RayTriangleEpsilon;
        }

        private static bool IsTextureHitVisible(
            Material material,
            List<Vector2> uvs,
            int index0,
            int index1,
            int index2,
            float barycentricU,
            float barycentricV
        ) {
            Texture2D texture = ResolveMaterialTexture(material);
            if (texture == null || uvs == null || index0 >= uvs.Count || index1 >= uvs.Count || index2 >= uvs.Count) {
                return true;
            }

            float barycentricW = 1f - barycentricU - barycentricV;
            Vector2 uv = (uvs[index0] * barycentricW) + (uvs[index1] * barycentricU) + (uvs[index2] * barycentricV);
            try {
                float alpha = texture.GetPixelBilinear(Mathf.Repeat(uv.x, 1f), Mathf.Repeat(uv.y, 1f)).a;
                return alpha > ResolveAlphaHitCutoff(material);
            } catch (UnityException) {
                return true;
            }
        }

        private static Texture2D ResolveMaterialTexture(Material material) {
            if (material == null) {
                return null;
            }

            if (material.HasProperty(BaseMapId) && material.GetTexture(BaseMapId) is Texture2D baseMap) {
                return baseMap;
            }

            if (material.HasProperty(MainTexId) && material.GetTexture(MainTexId) is Texture2D mainTex) {
                return mainTex;
            }

            return material.mainTexture as Texture2D;
        }

        private static float ResolveAlphaHitCutoff(Material material) {
            if (material == null) {
                return AlphaHitCutoffFallback;
            }

            if (material.HasProperty(CutoffId)) {
                return Mathf.Max(AlphaHitCutoffFallback, material.GetFloat(CutoffId));
            }

            return AlphaHitCutoffFallback;
        }

        /// <summary>Drawn now: its own renderer, or (batched) its batch — or its own renderer while it fades.</summary>
        private bool IsCandidateShown(in Candidate candidate) {
            MeshRenderer renderer = candidate.Renderer;
            if (renderer == null) {
                return false;
            }

            if (candidate.Batched) {
                return RoStaticModelBatchSet.IsBatchVisible(candidate.Member);
            }

            return renderer.enabled && renderer.gameObject.activeInHierarchy;
        }

        private void EnsureFadeEntry(in Candidate candidate) {
            MeshRenderer renderer = candidate.Renderer;
            if (activeFades.ContainsKey(renderer)) {
                return;
            }

            if (candidate.Batched) {
                // out of the batch, drawn by its own renderer while it fades
                RoStaticModelBatchSet.Hide(candidate.Member);
                renderer.enabled = true;
            }

            Material[] originals = renderer.sharedMaterials;
            var variants = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++) {
                variants[i] = GetFadeMaterial(originals[i]);
            }

            var originalPropertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(originalPropertyBlock);
            int originalSortingOrder = renderer.sortingOrder;
            renderer.sharedMaterials = variants;
            ApplyFadeSortingOrder(renderer, originalSortingOrder);
            activeFades.Add(renderer, new FadeEntry(renderer, originals, variants, originalPropertyBlock, originalSortingOrder, candidate.Batched, candidate.Member));
        }

        private Material GetFadeMaterial(Material original) {
            if (original == null) {
                return null;
            }

            if (fadeMaterialCache.TryGetValue(original, out Material cached) && cached != null) {
                return cached;
            }

            var material = new Material(original) {
                name = $"{original.name} Occlusion Fade",
                hideFlags = HideFlags.DontSave,
                renderQueue = RoMapRenderQueues.ModelTransparent
            };
            // the shadow pass keeps cutting the texture out as before the fade; without this a faded cut-out model
            // (chandelier, tree) casts its whole quads
            bool clipped = original.IsKeywordEnabled("_ALPHATEST_ON") ||
                (original.HasProperty(UseAlphaClipId) && original.GetFloat(UseAlphaClipId) > 0.5f) ||
                (original.HasProperty(AlphaClipId) && original.GetFloat(AlphaClipId) > 0.5f);
            SetFloatIfPresent(material, ShadowKeepsAlphaClipId, clipped ? 1f : 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            SetFloatIfPresent(material, SurfaceId, 1f);
            SetFloatIfPresent(material, AlphaClipId, 0f);
            SetFloatIfPresent(material, UseAlphaClipId, 0f);
            SetFloatIfPresent(material, ZWriteId, 0f);
            SetFloatIfPresent(material, SrcBlendId, (float) BlendMode.SrcAlpha);
            SetFloatIfPresent(material, DstBlendId, (float) BlendMode.OneMinusSrcAlpha);
            fadeMaterialCache[original] = material;
            return material;
        }

        private void AnimateFades(float deltaSeconds) {
            if (activeFades.Count == 0) {
                return;
            }

            activeScratch.Clear();
            foreach (MeshRenderer renderer in activeFades.Keys) {
                activeScratch.Add(renderer);
            }

            float targetAlpha = Mathf.Clamp01(renderSettings.localPlayerOcclusionFadeAlpha);
            float fadeInSeconds = Mathf.Max(0.001f, renderSettings.localPlayerOcclusionFadeInSeconds);
            float fadeOutSeconds = Mathf.Max(0.001f, renderSettings.localPlayerOcclusionFadeOutSeconds);
            for (int i = 0; i < activeScratch.Count; i++) {
                MeshRenderer renderer = activeScratch[i];
                if (!activeFades.TryGetValue(renderer, out FadeEntry entry) || renderer == null) {
                    activeFades.Remove(renderer);
                    continue;
                }

                bool desired = desiredFades.Contains(renderer);
                float goal = desired ? targetAlpha : 1f;
                float duration = desired ? fadeInSeconds : fadeOutSeconds;
                float step = deltaSeconds / duration;
                entry.CurrentAlpha = Mathf.MoveTowards(entry.CurrentAlpha, goal, step);
                activeFades[renderer] = entry;
                ApplyFadeSortingOrder(renderer, entry.OriginalSortingOrder);
                ApplyAlpha(renderer, entry.CurrentAlpha);
                if (!desired && entry.CurrentAlpha >= 0.999f) {
                    RestoreEntry(renderer, entry);
                }
            }

            activeScratch.Clear();
        }

        private void ApplyAlpha(MeshRenderer renderer, float alpha) {
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            var color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void RestoreAll() {
            if (activeFades.Count == 0) {
                return;
            }

            activeScratch.Clear();
            foreach (MeshRenderer renderer in activeFades.Keys) {
                activeScratch.Add(renderer);
            }

            for (int i = 0; i < activeScratch.Count; i++) {
                MeshRenderer renderer = activeScratch[i];
                if (renderer != null && activeFades.TryGetValue(renderer, out FadeEntry entry)) {
                    RestoreEntry(renderer, entry);
                }
            }

            activeScratch.Clear();
            activeFades.Clear();
            desiredFades.Clear();
        }

        private void RestoreEntry(MeshRenderer renderer, FadeEntry entry) {
            if (renderer != null) {
                renderer.sharedMaterials = entry.OriginalMaterials;
                renderer.sortingOrder = entry.OriginalSortingOrder;
                renderer.SetPropertyBlock(entry.OriginalPropertyBlock);
                if (entry.Batched) {
                    renderer.enabled = false;
                }
            }

            if (entry.Batched) {
                RoStaticModelBatchSet.Show(entry.Member);
            }

            activeFades.Remove(renderer);
        }

        private void ApplyFadeSortingOrder(MeshRenderer renderer, int originalSortingOrder) {
            if (renderer == null) {
                return;
            }

            int fadeOrder = Mathf.Max(originalSortingOrder, FadedModelSortingOrder);
            if (renderer.sortingOrder != fadeOrder) {
                renderer.sortingOrder = fadeOrder;
            }
        }

        private static void SetFloatIfPresent(Material material, int propertyId, float value) {
            if (material.HasProperty(propertyId)) {
                material.SetFloat(propertyId, value);
            }
        }

        private static void DestroyRuntimeObject(Object obj) {
            if (obj == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(obj);
            } else {
                DestroyImmediate(obj);
            }
        }

        private readonly struct Candidate {
            public Candidate(MeshRenderer renderer, Bounds bounds) {
                Renderer = renderer;
                Bounds = bounds;
                Batched = false;
                Member = default;
            }

            public Candidate(MeshRenderer renderer, Bounds bounds, RoStaticModelBatchSet.Member member) {
                Renderer = renderer;
                Bounds = bounds;
                Batched = true;
                Member = member;
            }

            public MeshRenderer Renderer { get; }
            public Bounds Bounds { get; }
            public bool Batched { get; }
            public RoStaticModelBatchSet.Member Member { get; }
        }

        private sealed class CachedMesh {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        private struct FadeEntry {
            public FadeEntry(
                MeshRenderer renderer,
                Material[] originalMaterials,
                Material[] fadeMaterials,
                MaterialPropertyBlock originalPropertyBlock,
                int originalSortingOrder,
                bool batched,
                RoStaticModelBatchSet.Member member
            ) {
                Renderer = renderer;
                OriginalMaterials = originalMaterials;
                FadeMaterials = fadeMaterials;
                OriginalPropertyBlock = originalPropertyBlock;
                OriginalSortingOrder = originalSortingOrder;
                Batched = batched;
                Member = member;
                CurrentAlpha = 1f;
            }

            /// <summary>Drawn by a static batch when not fading: restoring puts its range back and turns it off.</summary>
            public bool Batched { get; }
            public RoStaticModelBatchSet.Member Member { get; }

            public MeshRenderer Renderer { get; }
            public Material[] OriginalMaterials { get; }
            public Material[] FadeMaterials { get; }
            public MaterialPropertyBlock OriginalPropertyBlock { get; }
            public int OriginalSortingOrder { get; }
            public float CurrentAlpha { get; set; }
        }
    }
}
