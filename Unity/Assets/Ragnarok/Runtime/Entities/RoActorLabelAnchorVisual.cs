using System.Collections.Generic;
using Ragnarok.Diagnostics;
using Ragnarok.Sprites;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;

namespace Ragnarok.Entities {
    public sealed class RoActorLabelAnchorVisual : MonoBehaviour, IRoActorLateTick, IRoActorTickBudget, IRoActorTickVisibility {
        private const float LabelTickIntervalSeconds = 1f / 15f;
        private const float CrowdLabelTickIntervalSeconds = 0.1f;
        private const float HeavyCrowdLabelTickIntervalSeconds = 0.125f;
        private const float OffscreenLabelTickIntervalSeconds = 0.5f;
        private const int CrowdLabelThreshold = 32;
        private const int HeavyCrowdLabelThreshold = 72;
        private static readonly ProfilerMarker NameplateUpdateMarker = new ProfilerMarker("RO.Actor.Nameplate.Update");
        private static Material sharedOverlayTextMaterial;
        private static int activeLabelCount;
        private static readonly List<Renderer> rendererBoundsScratch = new List<Renderer>(16);

        [SerializeField] private float namePadding = 0.18f;
        [SerializeField] private float statusPadding = 0.04f;
        [SerializeField] private float characterSize = 0.11f;
        [SerializeField] private string actorName = "Novice";
        [SerializeField, Min(0)] private int hp = 50;
        [SerializeField, Min(1)] private int maxHp = 50;

        private RoActorLayeredVisual actor;
        private UnityEngine.Camera _camera;
        private UnityEngine.Camera GetCamera() => _camera != null ? _camera : (_camera = UnityEngine.Camera.main);
        private Transform labelRoot;
        private Transform nameAnchor;
        private Transform statusAnchor;
        private TextMesh nameText;
        private TextMesh statusText;
        private Transform cachedBoundsRoot;
        private readonly List<Renderer> cachedBoundsRenderers = new List<Renderer>(8);
        private string lastRenderedName;
        private int lastRenderedHp = int.MinValue;
        private int lastRenderedMaxHp = int.MinValue;
        private bool hasLastNamePosition;
        private bool hasLastStatusPosition;
        private bool hasLastLabelRotation;
        private Vector3 lastNamePosition;
        private Vector3 lastStatusPosition;
        private Quaternion lastLabelRotation;
        private float nextLabelTickTime;
        private bool cachedBoundsRenderersValid;

        public Transform NameAnchor => nameAnchor;
        public Transform StatusAnchor => statusAnchor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState() {
            activeLabelCount = 0;
            sharedOverlayTextMaterial = null;
            rendererBoundsScratch.Clear();
        }

        public void Initialize(RoActorLayeredVisual actorVisual, string displayName, int currentHp, int maximumHp) {
            if (actor != actorVisual) {
                actor = actorVisual;
                cachedBoundsRoot = null;
                cachedBoundsRenderers.Clear();
                cachedBoundsRenderersValid = false;
            }

            actorName = string.IsNullOrWhiteSpace(displayName) ? "Novice" : displayName;
            hp = Mathf.Max(0, currentHp);
            maxHp = Mathf.Max(1, maximumHp);
            EnsureAnchors();
            UpdateText();
            ApplyAnchorPositions();
        }

        public void SetStatus(string displayName, int currentHp, int maximumHp) {
            actorName = string.IsNullOrWhiteSpace(displayName) ? actorName : displayName;
            hp = Mathf.Max(0, currentHp);
            maxHp = Mathf.Max(1, maximumHp);
            UpdateText();
        }

        private void OnEnable() {
            activeLabelCount++;
            RoActorTickService.Register((IRoActorLateTick) this);
        }

        private void OnDisable() {
            activeLabelCount = Mathf.Max(0, activeLabelCount - 1);
            RoActorTickService.Unregister((IRoActorLateTick) this);
        }

        public bool ShouldRunActorTick(float now, bool lateTick) {
            if (!lateTick) {
                return true;
            }

            if (now < nextLabelTickTime) {
                return false;
            }

            nextLabelTickTime = now + ResolveLabelTickInterval();
            return true;
        }

        public RoActorTickVisibilityState ResolveActorTickVisibility(bool lateTick) {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) {
                return RoActorTickVisibilityState.Inactive;
            }

            if (HasVisibleActorRenderer()) {
                return RoActorTickVisibilityState.Visible;
            }

            return RoActorTickVisibilityState.Offscreen;
        }

        public void ActorLateTick(float now) {
            using (RoProfilerMarkers.NameplatesRefresh.Auto())
            using (NameplateUpdateMarker.Auto()) {
                EnsureAnchors();
                ApplyAnchorPositions();
                FaceCamera();
            }
        }

        private void EnsureAnchors() {
            if (labelRoot == null) {
                labelRoot = new GameObject("RO Actor Label Anchors").transform;
                labelRoot.SetParent(transform, false);
            }

            if (nameAnchor == null) {
                nameAnchor = new GameObject("Name Anchor").transform;
                nameAnchor.SetParent(labelRoot, false);
                nameText = CreateText(nameAnchor, "Name Label", Color.white);
            }

            if (statusAnchor == null) {
                statusAnchor = new GameObject("Status Anchor").transform;
                statusAnchor.SetParent(labelRoot, false);
                statusText = CreateText(statusAnchor, "Status Label", new Color(0.25f, 1f, 0.35f, 1f));
            }
        }

        private TextMesh CreateText(Transform parent, string objectName, Color color) {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = characterSize;
            text.fontSize = 32;
            text.color = color;

            var renderer = textObject.FindComponent<MeshRenderer>();
            if (renderer != null) {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = ResolveSharedOverlayTextMaterial(renderer.sharedMaterial);
            }

            return text;
        }

        private static Material ResolveSharedOverlayTextMaterial(Material baseMaterial) {
            if (baseMaterial == null) {
                return null;
            }

            if (sharedOverlayTextMaterial == null) {
                sharedOverlayTextMaterial = new Material(baseMaterial) {
                    name = "RO Actor Label Overlay Text",
                    renderQueue = (int) RenderQueue.Overlay
                };
            }

            return sharedOverlayTextMaterial;
        }

        private void UpdateText() {
            if (nameText != null && !string.Equals(lastRenderedName, actorName, System.StringComparison.Ordinal)) {
                nameText.text = actorName;
                lastRenderedName = actorName;
            }

            int clampedHp = Mathf.Clamp(hp, 0, maxHp);
            if (statusText != null && (lastRenderedHp != clampedHp || lastRenderedMaxHp != maxHp)) {
                statusText.text = clampedHp.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    "/" +
                    maxHp.ToString(System.Globalization.CultureInfo.InvariantCulture);
                lastRenderedHp = clampedHp;
                lastRenderedMaxHp = maxHp;
            }
        }

        private void ApplyAnchorPositions() {
            Transform boundsRoot = actor != null ? actor.transform : transform;
            if (!TryCalculateCachedRendererBounds(boundsRoot, out Bounds bounds)) {
                bounds = new Bounds(transform.position, Vector3.one);
            }

            UnityEngine.Camera labelCamera = GetCamera();
            Vector3 cameraUp = labelCamera != null ? labelCamera.transform.up : Vector3.up;
            CalculateAnchorPositions(bounds, cameraUp, namePadding, statusPadding, out Vector3 namePosition, out Vector3 statusPosition);
            if (!hasLastNamePosition || lastNamePosition != namePosition) {
                nameAnchor.position = namePosition;
                lastNamePosition = namePosition;
                hasLastNamePosition = true;
            }

            if (!hasLastStatusPosition || lastStatusPosition != statusPosition) {
                statusAnchor.position = statusPosition;
                lastStatusPosition = statusPosition;
                hasLastStatusPosition = true;
            }
        }

        private void FaceCamera() {
            UnityEngine.Camera camera = GetCamera();
            if (camera == null || labelRoot == null) {
                return;
            }

            Quaternion rotation = camera.transform.rotation;
            if (!hasLastLabelRotation || lastLabelRotation != rotation) {
                labelRoot.rotation = rotation;
                lastLabelRotation = rotation;
                hasLastLabelRotation = true;
            }
        }

        private bool TryCalculateCachedRendererBounds(Transform boundsRoot, out Bounds bounds) {
            if (boundsRoot == null) {
                bounds = default;
                return false;
            }

            RefreshBoundsRendererCache(boundsRoot);

            return TryCalculateRendererBounds(cachedBoundsRenderers, labelRoot, out bounds);
        }

        private float ResolveLabelTickInterval() {
            if (!HasVisibleActorRenderer()) {
                return OffscreenLabelTickIntervalSeconds;
            }

            if (activeLabelCount >= HeavyCrowdLabelThreshold) {
                return HeavyCrowdLabelTickIntervalSeconds;
            }

            return activeLabelCount >= CrowdLabelThreshold
                ? CrowdLabelTickIntervalSeconds
                : LabelTickIntervalSeconds;
        }

        private bool HasVisibleActorRenderer() {
            Transform boundsRoot = actor != null ? actor.transform : transform;
            RefreshBoundsRendererCache(boundsRoot);

            if (cachedBoundsRenderers.Count == 0) {
                return true;
            }

            for (int i = 0; i < cachedBoundsRenderers.Count; i++) {
                Renderer renderer = cachedBoundsRenderers[i];
                if (renderer == null ||
                    labelRoot != null && renderer.transform.IsChildOf(labelRoot)) {
                    continue;
                }

                if (renderer.isVisible) {
                    return true;
                }
            }

            return false;
        }

        private void RefreshBoundsRendererCache(Transform boundsRoot) {
            if (cachedBoundsRoot == boundsRoot && cachedBoundsRenderersValid) {
                return;
            }

            cachedBoundsRoot = boundsRoot;
            cachedBoundsRenderers.Clear();
            if (boundsRoot != null) {
                boundsRoot.GetComponentsInChildren(true, cachedBoundsRenderers);
            }

            cachedBoundsRenderersValid = true;
        }

        public static void CalculateAnchorPositions(
            Bounds bounds,
            Vector3 cameraUp,
            float namePadding,
            float statusPadding,
            out Vector3 namePosition,
            out Vector3 statusPosition
        ) {
            Vector3 up = cameraUp.sqrMagnitude > 0.0001f ? cameraUp.normalized : Vector3.up;
            Vector3 top = CalculateBoundsTop(bounds, up);
            statusPosition = top + up * Mathf.Max(0f, statusPadding);
            namePosition = statusPosition + up * Mathf.Max(0f, namePadding);
        }

        public static bool TryCalculateRendererBounds(Transform root, Transform ignoredRoot, out Bounds bounds) {
            bounds = default;
            if (root == null) {
                return false;
            }

            rendererBoundsScratch.Clear();
            root.GetComponentsInChildren(true, rendererBoundsScratch);
            bool hasBounds = TryCalculateRendererBounds(rendererBoundsScratch, ignoredRoot, out bounds);
            rendererBoundsScratch.Clear();
            return hasBounds;
        }

        private static bool TryCalculateRendererBounds(IReadOnlyList<Renderer> renderers, Transform ignoredRoot, out Bounds bounds) {
            bounds = default;
            if (renderers == null || renderers.Count == 0) {
                return false;
            }

            bool hasBounds = false;
            for (int i = 0; i < renderers.Count; i++) {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    ignoredRoot != null && renderer.transform.IsChildOf(ignoredRoot)) {
                    continue;
                }

                if (!hasBounds) {
                    bounds = renderer.bounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static Vector3 CalculateBoundsTop(Bounds bounds, Vector3 up) {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float maxDot = float.NegativeInfinity;
            for (int x = -1; x <= 1; x += 2) {
                for (int y = -1; y <= 1; y += 2) {
                    for (int z = -1; z <= 1; z += 2) {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        maxDot = Mathf.Max(maxDot, Vector3.Dot(corner - center, up));
                    }
                }
            }

            return center + up * maxDot;
        }
    }
}
