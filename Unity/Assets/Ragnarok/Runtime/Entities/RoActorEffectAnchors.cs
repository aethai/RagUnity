using System;
using UnityEngine;

namespace Ragnarok.Entities {
    public sealed class RoActorEffectAnchors : MonoBehaviour {
        public const string RootName = "RO Actor Effect Anchors";
        public const string OverheadAnchorName = "RO Actor Overhead Anchor";
        public const float DefaultOverheadLocalHeight = 2.5f;

        private Transform root;
        private Transform overheadAnchor;

        public Transform Root => EnsureRoot();

        public static RoActorEffectAnchors EnsureOn(GameObject owner) {
            if (owner == null) {
                return null;
            }

            if (!owner.TryGetComponent(out RoActorEffectAnchors anchors)) {
                anchors = owner.AddComponent<RoActorEffectAnchors>();
            }

            anchors.EnsureRoot();
            return anchors;
        }

        public Transform EnsureChild(string childName) {
            if (string.IsNullOrWhiteSpace(childName)) {
                throw new ArgumentException("Anchor child name cannot be empty.", nameof(childName));
            }

            Transform parent = EnsureRoot();
            Transform child = parent.Find(childName);
            if (child == null) {
                child = new GameObject(childName).transform;
                child.SetParent(parent, false);
                ResetLocalTransform(child);
            }

            return child;
        }

        public Transform EnsureOverheadAnchor() {
            if (overheadAnchor == null) {
                Transform parent = EnsureRoot();
                overheadAnchor = parent.Find(OverheadAnchorName);
                if (overheadAnchor == null) {
                    overheadAnchor = new GameObject(OverheadAnchorName).transform;
                    overheadAnchor.SetParent(parent, false);
                    overheadAnchor.localPosition = Vector3.up * DefaultOverheadLocalHeight;
                }
            }

            ResetLocalRotationAndScale(overheadAnchor);
            return overheadAnchor;
        }

        public bool TryGetOverheadAnchor(out Transform anchor) {
            if (overheadAnchor == null) {
                Transform parent = root != null ? root : transform.Find(RootName);
                overheadAnchor = parent != null ? parent.Find(OverheadAnchorName) : null;
            }

            anchor = overheadAnchor;
            return anchor != null;
        }

        public Transform RefreshOverheadAnchor(Transform visualRoot) {
            Transform anchor = EnsureOverheadAnchor();
            Vector3 localPosition = TryResolveOverheadLocalPosition(visualRoot, out Vector3 resolvedLocalPosition)
                ? resolvedLocalPosition
                : Vector3.up * DefaultOverheadLocalHeight;
            anchor.localPosition = localPosition;
            ResetLocalRotationAndScale(anchor);
            return anchor;
        }

        private Transform EnsureRoot() {
            if (root == null) {
                root = transform.Find(RootName);
                if (root == null) {
                    root = new GameObject(RootName).transform;
                    root.SetParent(transform, false);
                }
            }

            ResetLocalTransform(root);
            return root;
        }

        private bool TryResolveOverheadLocalPosition(Transform visualRoot, out Vector3 localPosition) {
            localPosition = default;
            if (visualRoot == null) {
                return false;
            }

            Transform ignoredRoot = EnsureRoot();
            if (!RoActorLabelAnchorVisual.TryCalculateRendererBounds(visualRoot, ignoredRoot, out Bounds bounds)) {
                return false;
            }

            RoActorLabelAnchorVisual.CalculateAnchorPositions(
                bounds,
                Vector3.up,
                namePadding: 0f,
                statusPadding: 0f,
                out _,
                out Vector3 topPosition);
            localPosition = Vector3.up * Mathf.Max(0f, transform.InverseTransformPoint(topPosition).y);
            return true;
        }

        private static void ResetLocalTransform(Transform target) {
            if (target == null) {
                return;
            }

            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        private static void ResetLocalRotationAndScale(Transform target) {
            if (target == null) {
                return;
            }

            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }
    }
}
