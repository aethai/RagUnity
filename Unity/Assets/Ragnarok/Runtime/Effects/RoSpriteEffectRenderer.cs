using System;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.Effects {
    public sealed class RoSpriteEffectRenderer : MonoBehaviour {
        private RoActSpriteAnimator animator;
        private Transform followTarget;
        private Vector3 followLocalOffset;
        private Vector3 worldPosition;
        private UnityEngine.Camera renderCamera;
        private bool faceCamera;
        private bool loop;
        private bool initialized;
        private bool finished;
        private bool hasLastRotation;
        private bool hasLastPosition;
        private Quaternion lastRotation;
        private Vector3 lastPosition;

        public event Action<RoSpriteEffectRenderer> Finished;

        public bool IsInitialized => initialized;
        public bool IsFinished => finished;
        public RoActSpriteAnimator Animator => animator;
        public Transform FollowTarget => followTarget;

        public void Initialize(
            RoSpriteAtlasCacheEntry atlasEntry,
            Vector3 spawnWorldPosition,
            RoEffectSpawnOptions options,
            bool shouldLoop
        ) {
            if (atlasEntry == null) {
                throw new ArgumentNullException(nameof(atlasEntry));
            }

            EnsureComponents();
            followTarget = options.FollowTarget;
            followLocalOffset = options.FollowLocalOffset;
            worldPosition = spawnWorldPosition;
            renderCamera = options.RenderCamera;
            faceCamera = options.FaceCamera;
            loop = shouldLoop;
            initialized = true;
            finished = false;
            hasLastRotation = false;
            hasLastPosition = false;

            animator.Initialize(atlasEntry.Sprite, atlasEntry.Act, atlasEntry.Atlas, initialActionIndex: Mathf.Max(0, options.SpriteActionIndex));
            animator.SetLoop(loop);
            animator.SetShadowRenderMode(RoShadowRenderMode.Original);
            animator.SetMaterialRenderQueue(options.OverrideRenderQueue
                ? options.RenderQueueBase
                : (int) UnityEngine.Rendering.RenderQueue.Transparent + 30);
            if (options.OverrideSortingOrder) {
                animator.SetRendererSortingOrder(options.SortingOrder);
            }

            ApplyPosition();
            ApplyBillboardRotation();
            gameObject.SetActive(true);
        }

        public void SetSortingOrder(int sortingOrder) {
            animator?.SetRendererSortingOrder(sortingOrder);
        }

        public void Stop() {
            if (finished) {
                return;
            }

            finished = true;
            initialized = false;
            gameObject.SetActive(false);
            Finished?.Invoke(this);
        }


        private void Update() {
            if (!initialized || finished) {
                return;
            }

            long updateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            using (RoProfilerMarkers.SpriteEffectUpdate.Auto()) {
                try {
                    ApplyPosition();
                    if (!loop && animator != null && animator.HasFinishedCurrentAction()) {
                        Stop();
                    }
                } finally {
                    RoRuntimePerfCounters.AddSpriteEffectUpdateTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(updateStartTicks));
                }
            }
        }

        private void LateUpdate() {
            if (!initialized || finished || !faceCamera) {
                return;
            }

            long lateUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            using (RoProfilerMarkers.SpriteEffectLateUpdate.Auto()) {
                try {
                    ApplyBillboardRotation();
                } finally {
                    RoRuntimePerfCounters.AddSpriteEffectLateUpdateTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(lateUpdateStartTicks));
                }
            }
        }

        private void EnsureComponents() {
            animator = GetComponent<RoActSpriteAnimator>();
            if (animator == null) {
                animator = gameObject.AddComponent<RoActSpriteAnimator>();
            }
        }

        private void ApplyPosition() {
            Vector3 position = followTarget != null
                ? followTarget.TransformPoint(followLocalOffset)
                : worldPosition;
            if (hasLastPosition && lastPosition == position) {
                return;
            }

            transform.position = position;
            lastPosition = position;
            hasLastPosition = true;
        }

        private void ApplyBillboardRotation() {
            UnityEngine.Camera camera = RoEffectCameraCache.Resolve(renderCamera);
            if (camera == null) {
                return;
            }

            Quaternion rotation = RoActorSpriteVisual.CalculateBillboardRotation(camera.transform.rotation);
            if (hasLastRotation && lastRotation == rotation) {
                return;
            }

            transform.rotation = rotation;
            lastRotation = rotation;
            hasLastRotation = true;
        }
    }
}
