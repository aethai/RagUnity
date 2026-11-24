using System.Collections.Generic;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Sprites;
using Unity.Profiling;
using UnityEngine;

namespace Ragnarok.Input {
    public sealed class RoSpritePointerBoundsCollider : MonoBehaviour {
        private const float DefaultPaddingWorldUnits = 0.12f;
        private const float MinimumProxySizeWorldUnits = 0.02f;
        private const float BoundsEpsilon = 0.0001f;
        private const string LegacyColliderRootName = "RO Sprite Pointer Mesh Colliders";

        private static readonly List<RoSpritePointerBoundsCollider> ActiveColliders =
            new List<RoSpritePointerBoundsCollider>(256);
        private static readonly ProfilerMarker RefreshMarker = new ProfilerMarker("RO SpritePointer.BoundsCollider.Refresh");

        public static bool RuntimeCollidersEnabled { get; private set; } = true;

        [SerializeField] private Transform visualRoot;
        [SerializeField] private float paddingWorldUnits = DefaultPaddingWorldUnits;

        private readonly List<Renderer> rendererScratch = new List<Renderer>(8);
        private readonly List<RendererEntry> rendererEntries = new List<RendererEntry>(8);
        private Transform cachedVisualRoot;
        private BoxCollider proxyCollider;
        private bool rendererCacheDirty = true;
        private bool hasLastSignature;
        private Bounds lastLocalBounds;
        private Vector3 lastTargetPosition;
        private Quaternion lastTargetRotation;
        private Vector3 lastTargetScale;
        private int lastMeshSignature;
        private int lastRendererCount;

        public int ActiveMeshColliderCount { get; private set; }

        public void Configure(Transform root, float padding = DefaultPaddingWorldUnits) {
            visualRoot = root;
            paddingWorldUnits = Mathf.Max(0f, padding);
            rendererCacheDirty = true;
            hasLastSignature = false;
            ClearLegacyColliderRoot();
            if (isActiveAndEnabled && !ActiveColliders.Contains(this)) {
                ActiveColliders.Add(this);
            }

            RefreshNow();
            RoRuntimePerfCounters.SetActiveSpritePointerColliderCount(ActiveColliders.Count);
        }

        public static bool RefreshActiveColliders() {
            long pointerColliderRefreshStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                bool changed = false;
                int activeCount = 0;
                for (int i = ActiveColliders.Count - 1; i >= 0; i--) {
                    RoSpritePointerBoundsCollider boundsCollider = ActiveColliders[i];
                    if (boundsCollider == null) {
                        ActiveColliders.RemoveAt(i);
                        continue;
                    }

                    if (boundsCollider.isActiveAndEnabled) {
                        activeCount++;
                        changed |= boundsCollider.RefreshNow();
                    }
                }

                RoRuntimePerfCounters.SetActiveSpritePointerColliderCount(activeCount);
                return changed;
            } finally {
                RoRuntimePerfCounters.AddPointerColliderRefreshTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(pointerColliderRefreshStartTicks));
            }
        }

        public static bool SetRuntimeCollidersEnabled(bool enabled) {
            if (RuntimeCollidersEnabled == enabled) {
                return false;
            }

            RuntimeCollidersEnabled = enabled;
            bool changed = true;
            int activeCount = 0;
            for (int i = ActiveColliders.Count - 1; i >= 0; i--) {
                RoSpritePointerBoundsCollider boundsCollider = ActiveColliders[i];
                if (boundsCollider == null) {
                    ActiveColliders.RemoveAt(i);
                    continue;
                }

                if (!boundsCollider.isActiveAndEnabled) {
                    continue;
                }

                activeCount++;
                changed |= boundsCollider.SetProxyRuntimeEnabled(enabled);
            }

            RoRuntimePerfCounters.SetActiveSpritePointerColliderCount(activeCount);
            return changed;
        }

        private void OnEnable() {
            if (!ActiveColliders.Contains(this)) {
                ActiveColliders.Add(this);
            }

            ClearLegacyColliderRoot();
            RefreshNow();
            RoRuntimePerfCounters.SetActiveSpritePointerColliderCount(ActiveColliders.Count);
        }

        private void OnDisable() {
            ActiveColliders.Remove(this);
            if (proxyCollider != null) {
                proxyCollider.enabled = false;
            }

            RoRuntimePerfCounters.SetActiveSpritePointerColliderCount(ActiveColliders.Count);
        }

        private void OnDestroy() {
            ActiveColliders.Remove(this);
            ClearGeneratedColliders();
            RoRuntimePerfCounters.SetActiveSpritePointerColliderCount(ActiveColliders.Count);
        }

        public bool RefreshNow() {
            long pointerRefreshNowStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                using (RefreshMarker.Auto()) {
                    RoRuntimePerfCounters.IncrementSpritePointerColliderRefresh();
                    ActiveMeshColliderCount = 0;

                    if (visualRoot == null) {
                        return SetProxyEnabled(false);
                    }

                    EnsureProxyCollider();
                    EnsureRendererCache();
                    if (!TryCalculateLocalBounds(out Bounds localBounds, out int meshSignature, out int rendererCount)) {
                        return SetProxyEnabled(false);
                    }

                    Vector3 targetPosition = transform.position;
                    Quaternion targetRotation = transform.rotation;
                    Vector3 targetScale = transform.lossyScale;
                    bool changed =
                        !hasLastSignature ||
                        proxyCollider.enabled != RuntimeCollidersEnabled ||
                        rendererCount != lastRendererCount ||
                        meshSignature != lastMeshSignature ||
                        !Approximately(localBounds, lastLocalBounds) ||
                        !Approximately(targetPosition, lastTargetPosition) ||
                        Quaternion.Angle(targetRotation, lastTargetRotation) > 0.001f ||
                        !Approximately(targetScale, lastTargetScale);

                    if (!changed) {
                        return false;
                    }

                    proxyCollider.isTrigger = true;
                    proxyCollider.center = localBounds.center;
                    proxyCollider.size = EnsureMinimumSize(localBounds.size);
                    proxyCollider.enabled = RuntimeCollidersEnabled;

                    hasLastSignature = true;
                    lastLocalBounds = localBounds;
                    lastTargetPosition = targetPosition;
                    lastTargetRotation = targetRotation;
                    lastTargetScale = targetScale;
                    lastMeshSignature = meshSignature;
                    lastRendererCount = rendererCount;
                    return true;
                }
            } finally {
                RoRuntimePerfCounters.AddPointerColliderRefreshNowTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(pointerRefreshNowStartTicks));
            }
        }

        public void ClearGeneratedColliders() {
            rendererScratch.Clear();
            rendererEntries.Clear();
            cachedVisualRoot = null;
            rendererCacheDirty = true;
            hasLastSignature = false;
            ActiveMeshColliderCount = 0;
            ClearLegacyColliderRoot();
            if (proxyCollider != null) {
                DestroyRuntimeObject(proxyCollider);
                proxyCollider = null;
            }
        }

        private void EnsureProxyCollider() {
            if (proxyCollider != null) {
                return;
            }

            proxyCollider = GetComponent<BoxCollider>();
            if (proxyCollider == null) {
                proxyCollider = gameObject.AddComponent<BoxCollider>();
            }

            proxyCollider.isTrigger = true;
        }

        private bool SetProxyEnabled(bool enabled) {
            EnsureProxyCollider();
            if (proxyCollider.enabled == enabled) {
                return false;
            }

            proxyCollider.enabled = enabled;
            if (!enabled) {
                hasLastSignature = false;
            }

            return true;
        }

        private bool SetProxyRuntimeEnabled(bool enabled) {
            if (proxyCollider == null) {
                return false;
            }

            bool shouldEnable = enabled && hasLastSignature;
            if (proxyCollider.enabled == shouldEnable) {
                return false;
            }

            proxyCollider.enabled = shouldEnable;
            return true;
        }

        private void EnsureRendererCache() {
            // an empty cache is read again: a Granny model adds its renderers once its file has loaded, below the root,
            // where no OnTransformChildrenChanged reaches this component
            if (!rendererCacheDirty && cachedVisualRoot == visualRoot && rendererEntries.Count > 0) {
                return;
            }

            rendererScratch.Clear();
            rendererEntries.Clear();
            cachedVisualRoot = visualRoot;
            if (visualRoot == null) {
                rendererCacheDirty = false;
                return;
            }

            visualRoot.GetComponentsInChildren(true, rendererScratch);
            for (int i = 0; i < rendererScratch.Count; i++) {
                Renderer renderer = rendererScratch[i];
                if (TryGetUsableMeshRenderer(renderer, out MeshFilter meshFilter)) {
                    rendererEntries.Add(new RendererEntry(renderer, meshFilter));
                }
            }

            rendererScratch.Clear();
            rendererCacheDirty = false;
        }

        private bool TryCalculateLocalBounds(out Bounds localBounds, out int meshSignature, out int rendererCount) {
            localBounds = default;
            meshSignature = 17;
            rendererCount = 0;
            bool hasBounds = false;

            for (int i = 0; i < rendererEntries.Count; i++) {
                RendererEntry entry = rendererEntries[i];
                if (!ShouldUseRendererEntry(entry)) {
                    continue;
                }

                if (!TryCalculateEntryLocalBounds(entry, out Bounds entryLocalBounds, out int entrySignature)) {
                    continue;
                }

                meshSignature = unchecked((meshSignature * 397) ^ entrySignature);
                rendererCount++;
                if (!hasBounds) {
                    localBounds = entryLocalBounds;
                    hasBounds = true;
                } else {
                    localBounds.Encapsulate(entryLocalBounds);
                }
            }

            if (!hasBounds) {
                return false;
            }

            float padding = Mathf.Max(0f, paddingWorldUnits);
            if (padding > 0f) {
                localBounds.Expand(padding * 2f);
            }

            return true;
        }

        private bool TryCalculateEntryLocalBounds(RendererEntry entry, out Bounds localBounds, out int signature) {
            localBounds = default;
            signature = 0;
            if (entry == null || entry.MeshFilter == null) {
                return false;
            }

            if (entry.Animator != null &&
                entry.Animator.TryGetCurrentActionStableLocalBounds(out Bounds animatorBounds, out int animatorSignature)) {
                localBounds = TransformLocalBoundsToLocal(animatorBounds, entry.Animator.transform, transform);
                signature = animatorSignature;
                return true;
            }

            Mesh mesh = entry.MeshFilter.sharedMesh;
            if (mesh == null) {
                return false;
            }

            localBounds = TransformLocalBoundsToLocal(mesh.bounds, entry.MeshFilter.transform, transform);
            signature = mesh.GetInstanceID();
            return true;
        }

        private static Bounds TransformLocalBoundsToLocal(Bounds sourceBounds, Transform sourceTransform, Transform targetTransform) {
            Vector3 min = sourceBounds.min;
            Vector3 max = sourceBounds.max;
            bool initialized = false;
            Bounds localBounds = default;

            for (int x = 0; x <= 1; x++) {
                for (int y = 0; y <= 1; y++) {
                    for (int z = 0; z <= 1; z++) {
                        Vector3 sourceCorner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 worldCorner = sourceTransform.TransformPoint(sourceCorner);
                        Vector3 localCorner = targetTransform.InverseTransformPoint(worldCorner);
                        if (!initialized) {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            initialized = true;
                        } else {
                            localBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }

            return localBounds;
        }

        private static Vector3 EnsureMinimumSize(Vector3 size) {
            return new Vector3(
                Mathf.Max(size.x, MinimumProxySizeWorldUnits),
                Mathf.Max(size.y, MinimumProxySizeWorldUnits),
                Mathf.Max(size.z, MinimumProxySizeWorldUnits));
        }

        private static bool TryGetUsableMeshRenderer(Renderer renderer, out MeshFilter meshFilter) {
            meshFilter = null;
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer is not MeshRenderer ||
                renderer.TryGetComponent(out TextMesh _) ||
                renderer.GetComponentInParent<RoActorShadowVisual>() != null ||
                !renderer.TryGetComponent(out meshFilter) ||
                meshFilter.sharedMesh == null ||
                meshFilter.sharedMesh.vertexCount == 0) {
                meshFilter = null;
                return false;
            }

            return true;
        }

        private static bool ShouldUseRendererEntry(RendererEntry entry) {
            return entry != null &&
                entry.Renderer != null &&
                entry.Renderer.enabled &&
                entry.Renderer.gameObject.activeInHierarchy &&
                entry.MeshFilter != null &&
                entry.MeshFilter.sharedMesh != null &&
                entry.MeshFilter.sharedMesh.vertexCount > 0;
        }

        private static bool Approximately(Bounds a, Bounds b) {
            return Approximately(a.center, b.center) && Approximately(a.size, b.size);
        }

        private static bool Approximately(Vector3 a, Vector3 b) {
            return Mathf.Abs(a.x - b.x) <= BoundsEpsilon &&
                Mathf.Abs(a.y - b.y) <= BoundsEpsilon &&
                Mathf.Abs(a.z - b.z) <= BoundsEpsilon;
        }

        private void ClearLegacyColliderRoot() {
            Transform legacyRoot = transform.Find(LegacyColliderRootName);
            if (legacyRoot != null) {
                DestroyRuntimeObject(legacyRoot.gameObject);
            }
        }

        private static void DestroyRuntimeObject(Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(target);
            } else {
                DestroyImmediate(target);
            }
        }

        private sealed class RendererEntry {
            public RendererEntry(Renderer renderer, MeshFilter meshFilter) {
                Renderer = renderer;
                MeshFilter = meshFilter;
                Animator = renderer != null ? renderer.GetComponent<RoActSpriteAnimator>() : null;
            }

            public Renderer Renderer { get; }
            public MeshFilter MeshFilter { get; }
            public RoActSpriteAnimator Animator { get; }
        }
    }
}
