using System;
using Ragnarok.Assets;
using Ragnarok.Sprites;
using Ragnarok.World;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Entities {
    [ExecuteAlways]
    public sealed class RoActorSpriteVisual : MonoBehaviour, IRoActorLateTick, IRoActorTickBudget, IRoActorActionVisual {
        private const float MissingCameraRetryIntervalSeconds = 0.25f;
        private const float BillboardRotationEpsilonDegrees = 0.1f;
        public const float GroundedVisualYOffset = 0f;

        [SerializeField] private bool faceCamera = true;
        [SerializeField] private bool useMotionActionMapping = true;
        [SerializeField] private float visualScale = 1f;
        [SerializeField] private RoSpriteMotionProfile motionProfile = RoSpriteMotionProfile.Player;
        [SerializeField] private bool actionDrivenByPrimary;
        [SerializeField] private int actionIndex;
        [SerializeField] private RoSpriteMotion motion = RoSpriteMotion.Idle;
        [SerializeField] private RoActorDirection facingDirection = RoActorDirection.South;
        [SerializeField] private RoShadowRenderMode shadowRenderMode = RoShadowRenderMode.Original;

        private RoActSpriteAnimator animator;
        private RoEnhancedShadowSettings enhancedShadowSettings;
        private RoActorDirection lastCameraDirection = RoActorDirection.None;
        private UnityEngine.Camera _camera;
        private float savedWalkElapsedSeconds = 0f;
        private bool hasRealtimeShadowCasterYOffsetOverride;
        private float realtimeShadowCasterYOffsetOverride;
        private bool hasAppliedBillboardRotation;
        private int appliedCameraRevision;
        /// <summary>Oriented by its owner (a layered actor), so it stays out of the per-frame late-tick loop.</summary>
        private bool billboardedByOwner;
        private Quaternion lastAppliedBillboardRotation = Quaternion.identity;
        private float nextMissingCameraRetryTime;
        private bool budgetRuntimeMeshBuilds;
        private bool budgetVisibleAnimationTicks;
        private bool useMutableRuntimeMesh;
        private UnityEngine.Camera GetCamera() => _camera != null ? _camera : (_camera = UnityEngine.Camera.main);

        public RoActSpriteAnimator Animator => animator;
        public RoSpriteMotionProfile MotionProfile => motionProfile;
        public bool ActionDrivenByPrimary => actionDrivenByPrimary;
        public int ActionIndex => actionIndex;
        public int FrameIndex => animator != null ? animator.FrameIndex : -1;
        public RoSpriteMotion Motion => motion;
        public RoActorDirection FacingDirection => facingDirection;

        public void SetFaceCamera(bool enabled) {
            faceCamera = enabled;
            _camera = null;
            hasAppliedBillboardRotation = false;
            appliedCameraRevision = 0;
        }

        public void SetRuntimeSpriteBudgets(bool meshBuilds, bool visibleAnimationTicks) {
            budgetRuntimeMeshBuilds = meshBuilds;
            budgetVisibleAnimationTicks = visibleAnimationTicks;
            animator?.SetRuntimeMeshBuildBudgeted(meshBuilds);
            animator?.SetVisibleAnimationTickBudgeted(visibleAnimationTicks);
        }

        public void SetUseMutableRuntimeMesh(bool enabled) {
            useMutableRuntimeMesh = enabled;
            animator?.SetUseMutableRuntimeMesh(enabled);
        }

        public int PrebuildAllFrameMeshes() {
            return animator != null ? animator.PrebuildAllFrameMeshes() : 0;
        }

        public bool Initialize(RoAssetDatabase assets, string basePath, int initialActionIndex = 0, byte[] paletteOverride = null) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (string.IsNullOrWhiteSpace(basePath)) {
                return false;
            }

            basePath = NormalizeBasePath(basePath);
            if (RoSpriteAtlasCache.TryGetOrBuild(assets, basePath, out RoSpriteAtlasCacheEntry cachedEntry, paletteOverride)) {
                Initialize(cachedEntry.Sprite, cachedEntry.Act, cachedEntry.Atlas, initialActionIndex);
                return true;
            }

            SPR spr = assets.Load<SPR>(RoSpritePairPath.SpritePath(basePath));
            ACT act = assets.Load<ACT>(RoSpritePairPath.ActionPath(basePath));
            if (spr == null || act == null) {
                return false;
            }

            Initialize(spr, act, initialActionIndex, paletteOverride);
            return true;
        }

        public bool Initialize(RoAssetDatabase assets, string basePath, string palettePath, int initialActionIndex = 0) {
            byte[] paletteOverride = null;
            if (assets != null && !string.IsNullOrWhiteSpace(palettePath)) {
                paletteOverride = RoSpriteAtlasCache.LoadPalette(assets, palettePath);
                if (paletteOverride == null || paletteOverride.Length == 0) {
                    Debug.LogWarning($"Could not load palette override '{palettePath}' for actor sprite '{basePath}'.", this);
                }
            }

            return Initialize(assets, basePath, initialActionIndex, paletteOverride);
        }

        public void Initialize(SPR spr, ACT act, int initialActionIndex = 0, byte[] paletteOverride = null) {
            if (spr == null) {
                throw new ArgumentNullException(nameof(spr));
            }

            if (act == null) {
                throw new ArgumentNullException(nameof(act));
            }

            actionIndex = Mathf.Max(0, initialActionIndex);
            animator = GetComponent<RoActSpriteAnimator>();
            if (animator == null) {
                animator = gameObject.AddComponent<RoActSpriteAnimator>();
            }

            animator.SetRuntimeMeshBuildBudgeted(budgetRuntimeMeshBuilds);
            animator.SetVisibleAnimationTickBudgeted(budgetVisibleAnimationTicks);
            animator.SetUseMutableRuntimeMesh(useMutableRuntimeMesh);
            animator.SetShadowRenderMode(shadowRenderMode, enhancedShadowSettings);
            animator.Initialize(spr, act, actionIndex, paletteOverride: paletteOverride);
            ApplyRealtimeShadowCasterYOffset();
            animator.SetFrameCompositionEnabled(ShouldUseFrameComposition(motionProfile));
            transform.localScale = Vector3.one * Mathf.Max(0.0001f, visualScale);
            if (ShouldApplyOwnMotionActionMapping()) {
                ApplyMappedAction(lastCameraDirection, restart: false);
            }
        }

        public void Initialize(SPR spr, ACT act, RoSpriteAtlas atlas, int initialActionIndex = 0) {
            if (spr == null) {
                throw new ArgumentNullException(nameof(spr));
            }

            if (act == null) {
                throw new ArgumentNullException(nameof(act));
            }

            if (atlas == null) {
                throw new ArgumentNullException(nameof(atlas));
            }

            actionIndex = Mathf.Max(0, initialActionIndex);
            animator = GetComponent<RoActSpriteAnimator>();
            if (animator == null) {
                animator = gameObject.AddComponent<RoActSpriteAnimator>();
            }

            animator.SetRuntimeMeshBuildBudgeted(budgetRuntimeMeshBuilds);
            animator.SetVisibleAnimationTickBudgeted(budgetVisibleAnimationTicks);
            animator.SetUseMutableRuntimeMesh(useMutableRuntimeMesh);
            animator.SetShadowRenderMode(shadowRenderMode, enhancedShadowSettings);
            animator.Initialize(spr, act, atlas, actionIndex);
            ApplyRealtimeShadowCasterYOffset();
            animator.SetFrameCompositionEnabled(ShouldUseFrameComposition(motionProfile));
            transform.localScale = Vector3.one * Mathf.Max(0.0001f, visualScale);
            if (ShouldApplyOwnMotionActionMapping()) {
                ApplyMappedAction(lastCameraDirection, restart: false);
            }
        }

        public void SetAction(int newActionIndex, bool restart = true) {
            actionIndex = Mathf.Max(0, newActionIndex);
            animator?.SetAction(actionIndex, restart);
        }

        public void SetMotionProfile(RoSpriteMotionProfile profile) {
            motionProfile = profile;
            animator?.SetFrameCompositionEnabled(ShouldUseFrameComposition(motionProfile));
            if (ShouldApplyOwnMotionActionMapping()) {
                ApplyMappedAction(lastCameraDirection, restart: false);
            }
        }

        public void SetActionDrivenByPrimary(bool driven) {
            actionDrivenByPrimary = driven;
            ApplyPrimaryDrivenFramePlayback();
            if (ShouldApplyOwnMotionActionMapping()) {
                ApplyMappedAction(lastCameraDirection, restart: false);
            }
        }

        public void SetRenderQueue(int renderQueue) {
            animator?.SetMaterialRenderQueue(renderQueue);
        }

        public void SetSpriteFeet(Vector3 feetOS) {
            animator?.SetSpriteFeet(feetOS);
        }

        public void SetSpriteDepthWrite(bool writes) {
            animator?.SetSpriteDepthWrite(writes);
        }

        public void SetSortingOrder(int sortingOrder) {
            animator?.SetRendererSortingOrder(sortingOrder);
        }

        public void SetFramePlayback(RoActFramePlaybackMode mode, int frameIndex = 0) {
            animator?.SetFramePlayback(mode, frameIndex);
        }

        public void SetMotionFirstFrameHeld(bool held) {
            if (animator == null || animator.FramePlaybackMode == RoActFramePlaybackMode.PrimarySynced) {
                return;
            }

            if (held) {
                animator.SetFramePlayback(RoActFramePlaybackMode.FixedFrame, 0);
            } else if (animator.FramePlaybackMode == RoActFramePlaybackMode.FixedFrame) {
                animator.SetFramePlayback(RoActFramePlaybackMode.Animated);
            }
        }

        public void SetMotionCurrentFrameHeld(bool held) {
            if (animator == null || animator.FramePlaybackMode == RoActFramePlaybackMode.PrimarySynced) {
                return;
            }

            if (held) {
                animator.SetFramePlayback(RoActFramePlaybackMode.FixedFrame, animator.FrameIndex);
            } else if (animator.FramePlaybackMode == RoActFramePlaybackMode.FixedFrame) {
                animator.SetFramePlayback(RoActFramePlaybackMode.Animated);
            }
        }

        public void SetFrameDelayScale(float scale) {
            animator?.SetFrameDelayScale(scale);
        }

        public void SetAbsoluteFrameSeconds(float perFrameSeconds) {
            animator?.SetAbsoluteFrameSeconds(perFrameSeconds);
        }

        public void SetLoop(bool enabled) {
            animator?.SetLoop(enabled);
        }

        public void SetPlayFrameSounds(bool enabled) {
            animator?.SetPlayFrameSounds(enabled);
        }

        public void SetOpacity(float alpha) {
            animator?.SetOpacity(alpha);
        }

        public void SetTintColor(Color color) {
            animator?.SetTintColor(color);
        }

        public void SetEffectTint(Color color) {
            animator?.SetEffectTint(color);
        }

        public void SetShadowRenderMode(RoShadowRenderMode mode, RoEnhancedShadowSettings settings = null) {
            shadowRenderMode = mode;
            enhancedShadowSettings = settings;
            animator?.SetShadowRenderMode(mode, settings);
            ApplyRealtimeShadowCasterYOffset();
        }

        public void SetCastsRealtimeShadow(bool value) {
            animator?.SetCastsRealtimeShadow(value);
        }

        public void SetRealtimeShadowCasterYOffset(float offset) {
            hasRealtimeShadowCasterYOffsetOverride = true;
            realtimeShadowCasterYOffsetOverride = NormalizeNonNegative(offset);
            ApplyRealtimeShadowCasterYOffset();
        }

        public void SetSharedActorShadowCard(bool enabled, Vector3 centerOS, float bottomLift) {
            animator?.SetSharedActorShadowCard(enabled, centerOS, bottomLift);
        }

        public void SetMapLight(Color multiply, Color add) {
            animator?.SetMapLight(multiply, add);
        }

        public bool TryGetCurrentActionDurationSeconds(out float durationSeconds) {
            durationSeconds = 0f;
            return animator != null && animator.TryGetActionDurationSeconds(actionIndex, out durationSeconds);
        }

        /// <summary>frames × ACT delay of <paramref name="motion"/>'s first direction at speed 1 (this layer's ACT).</summary>
        public bool TryGetMotionBaseDurationSeconds(RoSpriteMotion motion, out float durationSeconds) {
            durationSeconds = 0f;
            return animator != null &&
                animator.ActionCount > 0 &&
                animator.TryGetActionBaseDurationSeconds(
                    RoSpriteActionMap.GetMotionBaseAction(motionProfile, motion) % animator.ActionCount,
                    out durationSeconds);
        }

        public bool TryGetCurrentActionBaseDurationSeconds(out float durationSeconds) {
            durationSeconds = 0f;
            return animator != null && animator.TryGetActionBaseDurationSeconds(actionIndex, out durationSeconds);
        }

        public bool TryScaleCurrentActionsToDurationSeconds(float durationSeconds, float fallbackScale = 1f) {
            if (animator == null || durationSeconds <= 0f) {
                return false;
            }

            float scale = Mathf.Max(0.0001f, fallbackScale);
            if ((TryGetCurrentActionBaseDurationSeconds(out float baseDurationSeconds) ||
                    TryGetCurrentActionDurationSeconds(out baseDurationSeconds)) &&
                baseDurationSeconds > 0f) {
                scale = Mathf.Max(0.0001f, durationSeconds / baseDurationSeconds);
            }

            SetFrameDelayScale(scale);
            return true;
        }

        public bool HasFinishedCurrentAction() {
            return animator != null && animator.HasFinishedCurrentAction();
        }

        public void SetVerticalClipOffset(float offset) {
            animator?.SetVerticalClipOffset(offset);
        }

        public bool SetWalkFrameIndexFromDistance(float accumulatedDistanceCells) {
            return animator != null && animator.SetWalkFrameIndexFromDistance(accumulatedDistanceCells);
        }

        public bool SetWalkFrameIndexFromDistance(float accumulatedDistanceCells, RoMovementTuning tuning) {
            RoMovementTuning resolved = RoMovementTuning.Resolve(tuning);
            return animator != null && animator.SetWalkFrameIndexFromDistance(
                accumulatedDistanceCells,
                resolved.WalkUnitsPerCell,
                resolved.WalkPhaseScale);
        }

        public bool TryGetCurrentAnchor(out Vector2Int anchor) {
            if (animator != null) {
                return animator.TryGetCurrentAnchor(out anchor);
            }

            anchor = default;
            return false;
        }

        public bool TryGetAnchor(int requestedActionIndex, int requestedFrameIndex, out Vector2Int anchor) {
            if (animator != null) {
                return animator.TryGetAnchor(requestedActionIndex, requestedFrameIndex, out anchor);
            }

            anchor = default;
            return false;
        }

        public bool TryGetCurrentLayerPosition(out Vector2Int position) {
            if (animator != null) {
                return animator.TryGetCurrentLayerPosition(out position);
            }

            position = default;
            return false;
        }

        public bool TryGetLayerPosition(int requestedActionIndex, int requestedFrameIndex, out Vector2Int position) {
            if (animator != null) {
                return animator.TryGetLayerPosition(requestedActionIndex, requestedFrameIndex, out position);
            }

            position = default;
            return false;
        }

        public bool SetFrameIndexFromPrimary(int primaryFrameIndex) {
            return animator != null && animator.SetFrameIndexFromPrimary(primaryFrameIndex);
        }

        public bool SetActionAndFrameFromPrimary(int primaryActionIndex, int primaryFrameIndex) {
            if (animator == null || animator.ActionCount <= 0) {
                return false;
            }

            animator.SetFramePlayback(RoActFramePlaybackMode.PrimarySynced);
            actionIndex = PositiveModulo(primaryActionIndex, animator.ActionCount);
            animator.SetAction(actionIndex, restart: false);
            return animator.SetFrameIndexFromPrimary(primaryFrameIndex);
        }

        public void SetMotionAndFacing(RoSpriteMotion newMotion, RoActorDirection newFacingDirection, bool restart = true, bool force = false) {
            RoActorDirection resolvedFacingDirection = newFacingDirection == RoActorDirection.None ? facingDirection : newFacingDirection;
            bool motionChanged = motion != newMotion;
            bool leavingWalk  = motionChanged && motion    == RoSpriteMotion.Walk;
            bool enteringWalk = motionChanged && newMotion == RoSpriteMotion.Walk;
            bool changed = motionChanged || facingDirection != resolvedFacingDirection;

            // Save walk phase before leaving walk so we can resume it when walking again
            if (leavingWalk && animator != null) {
                savedWalkElapsedSeconds = animator.GetCurrentElapsedSeconds();
            }

            motion = newMotion;
            facingDirection = resolvedFacingDirection;
            ApplyPrimaryDrivenFramePlayback();
            if (animator != null &&
                animator.FramePlaybackMode != RoActFramePlaybackMode.PrimarySynced &&
                animator.FramePlaybackMode == RoActFramePlaybackMode.FixedFrame) {
                animator.SetFramePlayback(RoActFramePlaybackMode.Animated);
            }

            if (force || changed) {
                // When entering walk, resume the saved phase instead of snapping to frame 0
                // (roBrowser / Doddler behavior: smooth continuation after stop-to-walk)
                if (ShouldApplyOwnMotionActionMapping()) {
                    float resumeSeconds = (restart && enteringWalk) ? savedWalkElapsedSeconds : -1f;
                    ApplyMappedAction(lastCameraDirection, restart && (motionChanged || force), resumeSeconds);
                }
            }

            if (animator != null && motion != RoSpriteMotion.Walk &&
                animator.FramePlaybackMode == RoActFramePlaybackMode.DistanceSynced) {
                animator.SetFramePlayback(RoActFramePlaybackMode.Animated);
            }
        }

        private void OnEnable() {
            if (!billboardedByOwner) {
                RoActorTickService.Register((IRoActorLateTick) this);
            }
        }

        /// <summary>The owner orients this layer through <see cref="ApplyOwnerBillboard"/> instead of its own late tick.</summary>
        public void SetBillboardedByOwner(bool byOwner) {
            if (billboardedByOwner == byOwner) {
                return;
            }

            billboardedByOwner = byOwner;
            if (byOwner) {
                RoActorTickService.Unregister((IRoActorLateTick) this);
            } else if (isActiveAndEnabled) {
                RoActorTickService.Register((IRoActorLateTick) this);
            }
        }

        public bool FacesCamera => faceCamera;

        /// <summary>
        /// Driven by an owner whose layer pivot already faces the camera: only the action follows the camera direction.
        /// </summary>
        public void ApplyOwnerCameraDirection(RoActorDirection cameraDirection) {
            if (faceCamera && ShouldApplyOwnMotionActionMapping() && cameraDirection != lastCameraDirection) {
                ApplyMappedAction(cameraDirection, restart: false);
            }
        }

        /// <summary>What the late tick does, driven by the owner: face the camera and map the action to its direction.</summary>
        public void ApplyOwnerBillboard(Quaternion cameraRotation, RoActorDirection cameraDirection) {
            if (!faceCamera) {
                return;
            }

            Quaternion billboardRotation = CalculateBillboardRotation(cameraRotation);
            transform.rotation = billboardRotation;
            lastAppliedBillboardRotation = billboardRotation;
            hasAppliedBillboardRotation = true;
            if (ShouldApplyOwnMotionActionMapping() && cameraDirection != lastCameraDirection) {
                ApplyMappedAction(cameraDirection, restart: false);
            }
        }

        private void OnDisable() {
            RoActorTickService.Unregister((IRoActorLateTick) this);
        }

        public void ActorLateTick(float now) {
            if (!faceCamera) {
                return;
            }

            UnityEngine.Camera camera = GetCamera();
            if (camera == null) {
                return;
            }

            bool frameCamera = camera == RoActorTickService.FrameCamera;
            Quaternion billboardRotation = CalculateBillboardRotation(frameCamera ? RoActorTickService.FrameCameraRotation : camera.transform.rotation);
            transform.rotation = billboardRotation;
            lastAppliedBillboardRotation = billboardRotation;
            hasAppliedBillboardRotation = true;
            appliedCameraRevision = frameCamera ? RoActorTickService.CameraRevision : 0;
            transform.hasChanged = false;

            if (ShouldApplyOwnMotionActionMapping()) {
                RoActorDirection cameraDirection = RoSpriteActionMap.GetCameraDirectionFromYaw(camera.transform.eulerAngles.y);
                if (cameraDirection != lastCameraDirection) {
                    ApplyMappedAction(cameraDirection, restart: false);
                }
            }
        }

        public bool ShouldRunActorTick(float now, bool lateTick) {
            if (!lateTick) {
                return true;
            }

            if (!faceCamera) {
                return false;
            }

            UnityEngine.Camera camera = GetCamera();
            if (camera == null) {
                if (now < nextMissingCameraRetryTime) {
                    return false;
                }

                nextMissingCameraRetryTime = now + MissingCameraRetryIntervalSeconds;
                return true;
            }

            // the main camera has not turned and nothing moved this transform's rotation: nothing to re-orient
            if (hasAppliedBillboardRotation && appliedCameraRevision == RoActorTickService.CameraRevision &&
                camera == RoActorTickService.FrameCamera && !transform.hasChanged) {
                return false;
            }

            Quaternion billboardRotation = CalculateBillboardRotation(camera.transform.rotation);
            if (!hasAppliedBillboardRotation ||
                Quaternion.Angle(lastAppliedBillboardRotation, billboardRotation) > BillboardRotationEpsilonDegrees ||
                Quaternion.Angle(transform.rotation, billboardRotation) > BillboardRotationEpsilonDegrees) {
                return true;
            }

            if (ShouldApplyOwnMotionActionMapping()) {
                RoActorDirection cameraDirection = RoSpriteActionMap.GetCameraDirectionFromYaw(camera.transform.eulerAngles.y);
                return cameraDirection != lastCameraDirection;
            }

            // checked and still facing the camera: remember it, so the next frames skip the check
            if (camera == RoActorTickService.FrameCamera) {
                appliedCameraRevision = RoActorTickService.CameraRevision;
                transform.hasChanged = false;
            }

            return false;
        }

        private void OnTransformParentChanged() {
            ApplyRealtimeShadowCasterYOffset();
        }

        public static Quaternion CalculateBillboardRotation(Quaternion cameraRotation) {
            return cameraRotation;
        }

        public static float ResolveDefaultRealtimeShadowCasterYOffset(Transform spriteTransform) {
            if (spriteTransform == null) {
                return 0f;
            }

            Transform liftSource = ResolveLiftSourceTransform(spriteTransform);
            return ResolveWorldVerticalOffsetFromParent(liftSource);
        }

        private bool ShouldApplyOwnMotionActionMapping() {
            return useMotionActionMapping && !ShouldUsePrimaryActionDriver();
        }

        private bool ShouldUsePrimaryActionDriver() {
            return actionDrivenByPrimary &&
                motion != RoSpriteMotion.Idle &&
                motion != RoSpriteMotion.Sit;
        }

        private void ApplyPrimaryDrivenFramePlayback() {
            if (animator == null) {
                return;
            }

            if (ShouldUsePrimaryActionDriver()) {
                animator.SetFramePlayback(RoActFramePlaybackMode.PrimarySynced);
            } else if (animator.FramePlaybackMode == RoActFramePlaybackMode.PrimarySynced) {
                animator.SetFramePlayback(RoActFramePlaybackMode.Animated);
            }
        }

        private void ApplyRealtimeShadowCasterYOffset() {
            if (animator == null) {
                return;
            }

            float offset = hasRealtimeShadowCasterYOffsetOverride
                ? realtimeShadowCasterYOffsetOverride
                : ResolveDefaultRealtimeShadowCasterYOffset(transform);
            animator.SetRealtimeShadowCasterYOffset(offset);
        }

        private static Transform ResolveLiftSourceTransform(Transform spriteTransform) {
            Transform current = spriteTransform;
            while (current != null) {
                if (current.FindComponent<RoActorLayeredVisual>() != null) {
                    return current;
                }

                current = current.parent;
            }

            return spriteTransform;
        }

        private static float ResolveWorldVerticalOffsetFromParent(Transform liftedTransform) {
            if (liftedTransform == null) {
                return 0f;
            }

            Transform parent = liftedTransform.parent;
            if (parent == null) {
                return NormalizeNonNegative(liftedTransform.localPosition.y);
            }

            return NormalizeNonNegative(liftedTransform.position.y - parent.position.y);
        }

        private static float NormalizeNonNegative(float value) {
            if (float.IsNaN(value) || float.IsInfinity(value)) {
                return 0f;
            }

            return Mathf.Max(0f, value);
        }

        private static bool ShouldUseFrameComposition(RoSpriteMotionProfile profile) {
            // Runtime crowds cannot afford per-actor Texture2D composition on every new ACT frame.
            // Keep composition only for the uncommon action-NPC path where pre-blended frames may be needed.
            return profile == RoSpriteMotionProfile.ActionNpc;
        }

        private void ApplyMappedAction(RoActorDirection cameraDirection, bool restart, float resumeElapsedSeconds = -1f) {
            if (!useMotionActionMapping || animator == null || animator.ActionCount <= 0) {
                return;
            }

            if (cameraDirection == RoActorDirection.None) {
                cameraDirection = RoActorDirection.South;
            }

            int mappedAction = RoSpriteActionMap.GetActionIndex(motionProfile, motion, facingDirection, cameraDirection, animator.ActionCount);
            bool actionChanged = mappedAction != actionIndex;
            actionIndex = mappedAction;
            lastCameraDirection = cameraDirection;
            if (actionChanged || restart) {
                if (resumeElapsedSeconds >= 0f) {
                    // Resume walk cycle from saved phase rather than restarting at frame 0
                    animator.SetActionResuming(actionIndex, resumeElapsedSeconds);
                } else {
                    animator.SetAction(actionIndex, restart);
                }
            }
        }

        public static string NormalizeBasePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            path = path.Replace('\\', '/').Trim();
            if (path.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".act", StringComparison.OrdinalIgnoreCase)) {
                path = path.Substring(0, path.Length - 4);
            }

            return path;
        }

        private static int PositiveModulo(int value, int divisor) {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
