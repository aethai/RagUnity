using System.Collections.Generic;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Maps;
using UnityEngine;

namespace Ragnarok.Input {
    public sealed class RoSpritePointerTargetShape : MonoBehaviour {
        private const float AlphaThreshold = 0.05f;
        private const float BarycentricEpsilon = 0.0001f;
        private const float DefaultScreenPaddingPixels = 4f;
        private const int MaxCachedMeshes = 2048;
        private const int MaxCachedAlphaMasks = 2048;

        private static readonly List<RoSpritePointerTargetShape> ActiveShapeList = new List<RoSpritePointerTargetShape>();
        private static readonly Dictionary<int, MeshHitData> MeshHitDataCache = new Dictionary<int, MeshHitData>();
        private static readonly Dictionary<int, AlphaMaskData> AlphaMaskCache = new Dictionary<int, AlphaMaskData>();
        private static readonly Vector2[] PaddingSampleOffsets = {
            Vector2.zero,
            Vector2.right,
            Vector2.left,
            Vector2.up,
            Vector2.down,
            new Vector2(0.7071068f, 0.7071068f),
            new Vector2(-0.7071068f, 0.7071068f),
            new Vector2(0.7071068f, -0.7071068f),
            new Vector2(-0.7071068f, -0.7071068f),
            Vector2.right * 0.5f,
            Vector2.left * 0.5f,
            Vector2.up * 0.5f,
            Vector2.down * 0.5f
        };

        [SerializeField] private Transform visualRoot;
        [SerializeField] private float screenPaddingPixels = DefaultScreenPaddingPixels;
        [SerializeField] private bool alphaPerfectHitTest = true;

        private readonly List<Renderer> rendererScratch = new List<Renderer>(8);
        private readonly List<RendererEntry> rendererEntries = new List<RendererEntry>(8);
        private Transform cachedVisualRoot;
        private RoPointerTargetMarker cachedMarker;
        private bool rendererCacheDirty = true;
        private const int UnionRefreshFrames = 8;
        private const float UnionPaddingUnits = 1f;
        private int unionFrame = int.MinValue;
        private bool hasUnion;
        private Vector3 unionCenterFromRoot;
        private Vector3 unionSize;

        public static IReadOnlyList<RoSpritePointerTargetShape> ActiveShapes => ActiveShapeList;
        public static RoPointerTargetingMode GlobalTargetingMode { get; set; } = RoPointerTargetingMode.ColliderFirst;
        public static bool GlobalAlphaPerfectTargetingEnabled { get; set; } = true;
        public Transform VisualRoot => visualRoot;
        public float ScreenPaddingPixels => screenPaddingPixels;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState() {
            ActiveShapeList.Clear();
            MeshHitDataCache.Clear();
            AlphaMaskCache.Clear();
            GlobalTargetingMode = RoPointerTargetingMode.ColliderFirst;
            GlobalAlphaPerfectTargetingEnabled = true;
        }

        public void Configure(Transform root, float paddingPixels = DefaultScreenPaddingPixels, bool useAlphaPerfectHitTest = true) {
            visualRoot = root;
            screenPaddingPixels = Mathf.Max(0f, paddingPixels);
            alphaPerfectHitTest = useAlphaPerfectHitTest;
            rendererCacheDirty = true;
        }

        /// <summary>
        /// The broadphase of one shape; <paramref name="frame"/> holds the camera's matrices, taken once per pointer pass
        /// (projecting a renderer's 8 bounds corners with Camera.WorldToScreenPoint was 8 native calls per layer, ~1.3 ms
        /// per pass with 500 players on screen).
        /// </summary>
        public bool TryCalculateBroadphaseScore(
            in RoClientDrawFrame frame,
            Vector2 screenPosition,
            out int priority,
            out float depth
        ) {
            priority = 0;
            depth = float.MaxValue;
            if (frame.Camera == null || visualRoot == null) {
                return false;
            }

            EnsureRendererCache();
            RoPointerTargetMarker marker = cachedMarker;
            if (marker == null || marker.Kind == RoPointerTargetKind.None) {
                return false;
            }

            if (DiagnosticLegacyBroadphase) {
                return TryCalculateLegacyBroadphaseScore(frame.Camera, screenPosition, marker, out priority, out depth);
            }

            // most actors are nowhere near the pointer: one test against the actor's padded layer union (refreshed every
            // few frames, kept relative to the root so a walking actor carries it along) instead of every layer's bounds
            Vector3 rootPosition = visualRoot.position;
            if (unionFrame == int.MinValue || Time.frameCount - unionFrame >= UnionRefreshFrames) {
                RefreshLayerUnion(rootPosition);
            }

            if (hasUnion &&
                !IsScreenPointNearBounds(frame, new Bounds(rootPosition + unionCenterFromRoot, unionSize), screenPosition)) {
                return false;
            }

            bool hasCandidate = false;
            for (int i = 0; i < rendererEntries.Count; i++) {
                RendererEntry entry = rendererEntries[i];
                if (!ShouldUseRendererEntry(entry)) {
                    continue;
                }

                Bounds bounds = entry.Renderer.bounds;
                if (!IsScreenPointNearBounds(frame, bounds, screenPosition)) {
                    continue;
                }

                Vector3 screenCenter = frame.ScreenPoint(bounds.center);
                if (screenCenter.z <= 0f) {
                    continue;
                }

                depth = Mathf.Min(depth, screenCenter.z);
                hasCandidate = true;
            }

            priority = RoPointerTargetResolver.GetPriority(marker.Kind);
            return hasCandidate;
        }

        /// <summary>Diagnostics (!toggle pointerlegacy): the broadphase as before the layer union and the frame projection, for A/B.</summary>
        public static bool DiagnosticLegacyBroadphase { get; set; }

        private bool TryCalculateLegacyBroadphaseScore(
            UnityEngine.Camera camera,
            Vector2 screenPosition,
            RoPointerTargetMarker marker,
            out int priority,
            out float depth
        ) {
            depth = float.MaxValue;
            bool hasCandidate = false;
            float padding = Mathf.Max(0f, screenPaddingPixels);
            for (int i = 0; i < rendererEntries.Count; i++) {
                RendererEntry entry = rendererEntries[i];
                if (!ShouldUseRendererEntry(entry)) {
                    continue;
                }

                Bounds bounds = entry.Renderer.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                bool hasProjectedCorner = false;
                for (int corner = 0; corner < 8; corner++) {
                    Vector3 screen = camera.WorldToScreenPoint(new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z));
                    if (screen.z <= 0f) {
                        continue;
                    }

                    hasProjectedCorner = true;
                    minX = Mathf.Min(minX, screen.x);
                    minY = Mathf.Min(minY, screen.y);
                    maxX = Mathf.Max(maxX, screen.x);
                    maxY = Mathf.Max(maxY, screen.y);
                }

                if (!hasProjectedCorner ||
                    screenPosition.x < minX - padding || screenPosition.x > maxX + padding ||
                    screenPosition.y < minY - padding || screenPosition.y > maxY + padding) {
                    continue;
                }

                Vector3 screenCenter = camera.WorldToScreenPoint(entry.Renderer.bounds.center);
                if (screenCenter.z <= 0f) {
                    continue;
                }

                depth = Mathf.Min(depth, screenCenter.z);
                hasCandidate = true;
            }

            priority = RoPointerTargetResolver.GetPriority(marker.Kind);
            return hasCandidate;
        }

        /// <summary>
        /// The union of the enabled layers' bounds, relative to the root, grown by half its size and a cell so the frames
        /// of the next few ticks (a wider attack frame, a step) stay inside it; none while no layer is drawn.
        /// </summary>
        private void RefreshLayerUnion(Vector3 rootPosition) {
            unionFrame = Time.frameCount;
            hasUnion = false;
            Bounds union = default;
            for (int i = 0; i < rendererEntries.Count; i++) {
                RendererEntry entry = rendererEntries[i];
                if (!ShouldUseRendererEntry(entry)) {
                    continue;
                }

                if (hasUnion) {
                    union.Encapsulate(entry.Renderer.bounds);
                } else {
                    union = entry.Renderer.bounds;
                    hasUnion = true;
                }
            }

            if (hasUnion) {
                unionCenterFromRoot = union.center - rootPosition;
                unionSize = union.size * 1.5f + new Vector3(UnionPaddingUnits, UnionPaddingUnits, UnionPaddingUnits);
            }
        }

        public bool TryBuildTarget(
            UnityEngine.Camera camera,
            Vector2 screenPosition,
            RoPointerModifierFlags modifiers,
            out RoPointerTarget target,
            out float depth
        ) {
            target = RoPointerTarget.None;
            depth = float.MaxValue;
            if (camera == null || visualRoot == null) {
                return false;
            }

            EnsureRendererCache();
            RoPointerTargetMarker marker = cachedMarker;
            if (marker == null || marker.Kind == RoPointerTargetKind.None) {
                return false;
            }

            Vector3 bestWorldPoint = default;
            bool hasHit = false;
            for (int i = 0; i < rendererEntries.Count; i++) {
                RendererEntry entry = rendererEntries[i];
                if (!ShouldUseRendererEntry(entry)) {
                    continue;
                }

                if (TryHitRenderer(camera, entry, screenPosition, out float hitDepth, out Vector3 hitWorldPoint) &&
                    hitDepth < depth) {
                    depth = hitDepth;
                    bestWorldPoint = hitWorldPoint;
                    hasHit = true;
                }
            }

            if (!hasHit) {
                return false;
            }

            target = RoPointerTarget.WorldTarget(
                marker.Kind,
                bestWorldPoint,
                marker.gameObject,
                modifiers,
                marker.BlocksGroundClick,
                string.IsNullOrWhiteSpace(marker.Label) ? marker.gameObject.name : marker.Label);
            return true;
        }

        private void OnEnable() {
            if (!ActiveShapeList.Contains(this)) {
                ActiveShapeList.Add(this);
            }

            rendererCacheDirty = true;
        }

        private void OnDisable() {
            ActiveShapeList.Remove(this);
        }

        private void OnTransformChildrenChanged() {
            rendererCacheDirty = true;
        }

        public void MarkDirty() {
            rendererCacheDirty = true;
        }

        private void EnsureRendererCache() {
            // an empty cache is read again: a Granny model adds its renderers once its file has loaded, below the root,
            // where no OnTransformChildrenChanged reaches this component
            if (!rendererCacheDirty && cachedVisualRoot == visualRoot && rendererEntries.Count > 0) {
                return;
            }

            rendererEntries.Clear();
            rendererScratch.Clear();
            cachedVisualRoot = visualRoot;
            unionFrame = int.MinValue;
            cachedMarker = GetComponentInParent<RoPointerTargetMarker>();
            if (visualRoot == null) {
                rendererCacheDirty = false;
                return;
            }

            visualRoot.GetComponentsInChildren(true, rendererScratch);
            for (int i = 0; i < rendererScratch.Count; i++) {
                Renderer renderer = rendererScratch[i];
                if (ShouldUseRenderer(renderer, out MeshFilter meshFilter)) {
                    rendererEntries.Add(new RendererEntry(renderer, meshFilter));
                }
            }

            rendererScratch.Clear();
            rendererCacheDirty = false;
        }

        private static bool ShouldUseRenderer(Renderer renderer, out MeshFilter meshFilter) {
            meshFilter = null;
            if (renderer == null || !renderer.enabled) {
                return false;
            }

            if (renderer.GetComponentInParent<RoActorShadowVisual>() != null ||
                renderer.TryGetComponent(out TextMesh _)) {
                return false;
            }

            meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null && meshFilter.sharedMesh != null;
        }

        private static bool ShouldUseRendererEntry(RendererEntry entry) {
            return entry != null &&
                entry.Renderer != null &&
                entry.Renderer.enabled &&
                entry.MeshFilter != null &&
                entry.MeshFilter.sharedMesh != null;
        }

        private bool TryHitRenderer(
            UnityEngine.Camera camera,
            RendererEntry entry,
            Vector2 screenPosition,
            out float depth,
            out Vector3 worldPoint
        ) {
            depth = float.MaxValue;
            worldPoint = default;
            Renderer renderer = entry.Renderer;
            if (!IsScreenPointNearBounds(new RoClientDrawFrame(camera), renderer.bounds, screenPosition)) {
                return false;
            }

            MeshFilter meshFilter = entry.MeshFilter;
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null) {
                return false;
            }

            MeshHitData meshData = GetMeshHitData(mesh);
            if (meshData == null || !meshData.IsValid) {
                return false;
            }

            Texture2D texture = ResolveTexture(renderer);
            bool hasHit = false;
            float padding = Mathf.Max(0f, screenPaddingPixels);
            for (int sampleIndex = 0; sampleIndex < PaddingSampleOffsets.Length; sampleIndex++) {
                Vector2 samplePosition = screenPosition + PaddingSampleOffsets[sampleIndex] * padding;
                if (TryHitMesh(camera, meshFilter.transform, meshData, texture, samplePosition, out float sampleDepth, out Vector3 sampleWorldPoint) &&
                    sampleDepth < depth) {
                    depth = sampleDepth;
                    worldPoint = sampleWorldPoint;
                    hasHit = true;
                }
            }

            return hasHit;
        }

        private bool TryHitMesh(
            UnityEngine.Camera camera,
            Transform meshTransform,
            MeshHitData meshData,
            Texture2D texture,
            Vector2 screenPosition,
            out float depth,
            out Vector3 worldPoint
        ) {
            RoRuntimePerfCounters.IncrementSpritePointerExactTest();
            depth = float.MaxValue;
            worldPoint = default;
            bool hasHit = false;

            List<int> triangles = meshData.Triangles;
            List<Vector3> vertices = meshData.Vertices;
            List<Vector2> uvs = meshData.Uvs;
            for (int i = 0; i + 2 < triangles.Count; i += 3) {
                int index0 = triangles[i];
                int index1 = triangles[i + 1];
                int index2 = triangles[i + 2];
                if (!meshData.IsValidVertexIndex(index0) ||
                    !meshData.IsValidVertexIndex(index1) ||
                    !meshData.IsValidVertexIndex(index2)) {
                    continue;
                }

                Vector3 world0 = meshTransform.TransformPoint(vertices[index0]);
                Vector3 world1 = meshTransform.TransformPoint(vertices[index1]);
                Vector3 world2 = meshTransform.TransformPoint(vertices[index2]);
                Vector3 screen0 = camera.WorldToScreenPoint(world0);
                Vector3 screen1 = camera.WorldToScreenPoint(world1);
                Vector3 screen2 = camera.WorldToScreenPoint(world2);
                if (screen0.z <= 0f || screen1.z <= 0f || screen2.z <= 0f) {
                    continue;
                }

                if (!TryCalculateBarycentric(
                        screenPosition,
                        new Vector2(screen0.x, screen0.y),
                        new Vector2(screen1.x, screen1.y),
                        new Vector2(screen2.x, screen2.y),
                        out Vector3 barycentric)) {
                    continue;
                }

                Vector2 uv = (uvs[index0] * barycentric.x) +
                    (uvs[index1] * barycentric.y) +
                    (uvs[index2] * barycentric.z);
                if (ShouldRunAlphaPerfectHitTest() && !IsOpaqueAt(texture, uv)) {
                    continue;
                }

                float sampleDepth = (screen0.z * barycentric.x) +
                    (screen1.z * barycentric.y) +
                    (screen2.z * barycentric.z);
                if (sampleDepth >= depth) {
                    continue;
                }

                depth = sampleDepth;
                worldPoint = (world0 * barycentric.x) + (world1 * barycentric.y) + (world2 * barycentric.z);
                hasHit = true;
            }

            return hasHit;
        }

        private bool ShouldRunAlphaPerfectHitTest() {
            if (!GlobalAlphaPerfectTargetingEnabled || !alphaPerfectHitTest) {
                return false;
            }

            return GlobalTargetingMode == RoPointerTargetingMode.AlphaExact;
        }

        private bool IsScreenPointNearBounds(in RoClientDrawFrame frame, Bounds bounds, Vector2 screenPosition) {
            RoRuntimePerfCounters.IncrementSpritePointerBroadphaseCheck();
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            bool hasProjectedCorner = false;
            for (int x = 0; x <= 1; x++) {
                for (int y = 0; y <= 1; y++) {
                    for (int z = 0; z <= 1; z++) {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 screen = frame.ScreenPoint(corner);
                        if (screen.z <= 0f) {
                            continue;
                        }

                        hasProjectedCorner = true;
                        minX = Mathf.Min(minX, screen.x);
                        minY = Mathf.Min(minY, screen.y);
                        maxX = Mathf.Max(maxX, screen.x);
                        maxY = Mathf.Max(maxY, screen.y);
                    }
                }
            }

            if (!hasProjectedCorner) {
                return false;
            }

            float padding = Mathf.Max(0f, screenPaddingPixels);
            return screenPosition.x >= minX - padding &&
                screenPosition.x <= maxX + padding &&
                screenPosition.y >= minY - padding &&
                screenPosition.y <= maxY + padding;
        }

        private static Texture2D ResolveTexture(Renderer renderer) {
            Material material = renderer != null ? renderer.sharedMaterial : null;
            return material != null ? material.mainTexture as Texture2D : null;
        }

        private static bool TryCalculateBarycentric(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            out Vector3 barycentric
        ) {
            barycentric = default;
            Vector2 v0 = b - a;
            Vector2 v1 = c - a;
            Vector2 v2 = point - a;
            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) <= 0.000001f) {
                return false;
            }

            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1f - v - w;
            if (u < -BarycentricEpsilon || v < -BarycentricEpsilon || w < -BarycentricEpsilon) {
                return false;
            }

            barycentric = new Vector3(u, v, w);
            return true;
        }

        private static bool IsOpaqueAt(Texture2D texture, Vector2 uv) {
            RoRuntimePerfCounters.IncrementSpritePointerAlphaTest();
            if (texture == null || !texture.isReadable) {
                return true;
            }

            AlphaMaskData mask = GetAlphaMaskData(texture);
            if (mask == null || !mask.IsValid) {
                return true;
            }

            int x = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(uv.x) * mask.Width), 0, mask.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(uv.y) * mask.Height), 0, mask.Height - 1);
            return mask.IsOpaque(x, y);
        }

        private static MeshHitData GetMeshHitData(Mesh mesh) {
            if (mesh == null) {
                return null;
            }

            int key = mesh.GetInstanceID();
            if (MeshHitDataCache.TryGetValue(key, out MeshHitData cached) &&
                cached.Mesh == mesh &&
                cached.VertexCount == mesh.vertexCount) {
                return cached;
            }

            if (MeshHitDataCache.Count > MaxCachedMeshes) {
                MeshHitDataCache.Clear();
            }

            MeshHitData data = new MeshHitData(mesh);
            MeshHitDataCache[key] = data;
            return data;
        }

        private static AlphaMaskData GetAlphaMaskData(Texture2D texture) {
            if (texture == null) {
                return null;
            }

            int key = texture.GetInstanceID();
            if (AlphaMaskCache.TryGetValue(key, out AlphaMaskData cached) &&
                cached.Texture == texture &&
                cached.Width == texture.width &&
                cached.Height == texture.height) {
                return cached;
            }

            if (AlphaMaskCache.Count > MaxCachedAlphaMasks) {
                AlphaMaskCache.Clear();
            }

            AlphaMaskData data = AlphaMaskData.Create(texture);
            AlphaMaskCache[key] = data;
            return data;
        }

        private sealed class RendererEntry {
            public RendererEntry(Renderer renderer, MeshFilter meshFilter) {
                Renderer = renderer;
                MeshFilter = meshFilter;
            }

            public Renderer Renderer { get; }
            public MeshFilter MeshFilter { get; }
        }

        private sealed class MeshHitData {
            public MeshHitData(Mesh mesh) {
                Mesh = mesh;
                VertexCount = mesh != null ? mesh.vertexCount : 0;
                if (mesh != null) {
                    mesh.GetVertices(Vertices);
                    mesh.GetUVs(0, Uvs);
                    mesh.GetTriangles(Triangles, 0);
                }
            }

            public Mesh Mesh { get; }
            public int VertexCount { get; }
            public List<Vector3> Vertices { get; } = new List<Vector3>(64);
            public List<Vector2> Uvs { get; } = new List<Vector2>(64);
            public List<int> Triangles { get; } = new List<int>(96);
            public bool IsValid => Vertices.Count > 0 && Triangles.Count >= 3 && Uvs.Count >= Vertices.Count;

            public bool IsValidVertexIndex(int index) {
                return index >= 0 && index < Vertices.Count && index < Uvs.Count;
            }
        }

        private sealed class AlphaMaskData {
            private readonly bool[] opaquePixels;

            private AlphaMaskData(Texture2D texture, int width, int height, bool[] opaquePixels) {
                Texture = texture;
                Width = width;
                Height = height;
                this.opaquePixels = opaquePixels;
            }

            public Texture2D Texture { get; }
            public int Width { get; }
            public int Height { get; }
            public bool IsValid => Texture != null && Width > 0 && Height > 0 && opaquePixels != null && opaquePixels.Length == Width * Height;

            public static AlphaMaskData Create(Texture2D texture) {
                if (texture == null || !texture.isReadable || texture.width <= 0 || texture.height <= 0) {
                    return new AlphaMaskData(texture, 0, 0, null);
                }

                try {
                    Color32[] pixels = texture.GetPixels32();
                    bool[] opaque = new bool[pixels.Length];
                    byte threshold = (byte) Mathf.Clamp(Mathf.RoundToInt(AlphaThreshold * 255f), 0, 255);
                    for (int i = 0; i < pixels.Length; i++) {
                        opaque[i] = pixels[i].a > threshold;
                    }

                    return new AlphaMaskData(texture, texture.width, texture.height, opaque);
                } catch (UnityException) {
                    return new AlphaMaskData(texture, 0, 0, null);
                }
            }

            public bool IsOpaque(int x, int y) {
                if (!IsValid) {
                    return true;
                }

                int index = Mathf.Clamp(y, 0, Height - 1) * Width + Mathf.Clamp(x, 0, Width - 1);
                return index >= 0 && index < opaquePixels.Length && opaquePixels[index];
            }
        }
    }
}
