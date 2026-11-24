using System;
using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Network;
using Ragnarok.Sprites;
using Ragnarok.World;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Entities {
    public enum RoActorSpriteLayer {
        Body,
        Head,
        Hair,
        Weapon,
        Shield,
        Garment,
        HeadgearTop,
        HeadgearMid,
        HeadgearLower,
        CartShadow,
        Cart,
        WeaponTrail
    }

    [Serializable]
    public sealed class RoActorSpriteLayerSpec {
        public RoActorSpriteLayer layer;
        public string basePath;
        public string palettePath;
        public int initialActionIndex;
        public RoSpriteMotionProfile motionProfile = RoSpriteMotionProfile.Player;
        public Vector3 localOffset;
        public float scale = 1f;

        public RoActorSpriteLayerSpec() {
        }

        public RoActorSpriteLayerSpec(RoActorSpriteLayer layer, string basePath) {
            this.layer = layer;
            this.basePath = basePath;
        }
    }

    public sealed class RoActorLayeredVisual : MonoBehaviour, IRoActorUpdateTick, IRoActorLateTick, IRoActorTickBudget, IRoActorActionVisual {
        private const float AnchorCameraRotationEpsilonDegrees = 0.1f;
        private const float AnchorTransformPositionEpsilonSqr = 0.0000001f;
        private const float AnchorTransformScaleEpsilonSqr = 0.0000001f;
        private static readonly Comparison<RoActorSpriteLayerSpec> RenderOrderComparison = CompareLayerRenderOrder;
        public const int SortingOrderStride = 100;
        public const float CartFollowDistanceCells = 1f;
        /// <summary>Further than this from its owner the cart is placed behind it again (a spawn, a teleport), not pulled.</summary>
        public const float CartSnapDistanceCells = 3f;
        public const float CartFacingHysteresisDegrees = 8f;

        [SerializeField] private int renderQueueBase = (int) RenderQueue.Transparent;
        [SerializeField] private int sortingOrderBase;
        [SerializeField] private RoSpriteMotion motion = RoSpriteMotion.Idle;
        [SerializeField] private RoActorDirection facingDirection = RoActorDirection.South;
        [SerializeField, Range(0, 2)] private int headDirectionFrameIndex;
        [SerializeField] private bool holdCurrentMotionFirstFrame;
        [SerializeField] private bool holdCurrentMotionCurrentFrame;
        [SerializeField] private bool applyActAnchors = true;
        [SerializeField] private RoShadowRenderMode shadowRenderMode = RoShadowRenderMode.Original;

        private readonly List<RoActorLayerInstance> layers = new List<RoActorLayerInstance>();
        private readonly List<RoActorSpriteLayerSpec> sortedLayerSpecScratch = new List<RoActorSpriteLayerSpec>(8);
        private SortingGroup sortingGroup;
        private UnityEngine.Camera _camera;
        private bool hasAnchorDrivenLayers;
        private bool hasCameraOffsetLayers;
        /// <summary>Parent of every layer; faces the camera when all layers do (see <see cref="ApplyLayerBillboards"/>).</summary>
        private Transform layerPivot;
        private bool pivotFacesCamera;
        private bool anchorsDirty;
        private bool hasLastAnchorCameraRotation;
        private Quaternion lastAnchorCameraRotation = Quaternion.identity;
        private int lastAnchorBodyActionIndex = -1;
        private int lastAnchorBodyFrameIndex = -1;
        private bool hasLastAnchorTransformState;
        private Vector3 lastAnchorTransformPosition;
        private Quaternion lastAnchorTransformRotation = Quaternion.identity;
        private Vector3 lastAnchorTransformLossyScale = Vector3.one;
        private RoEnhancedShadowSettings enhancedShadowSettings;
        private HashSet<int> loggedAnchorDesyncKeys;
        private bool budgetRuntimeMeshBuilds;
        private bool budgetVisibleAnimationTicks;
        private bool useMutableRuntimeMeshes;
        private bool cartFollowInitialized;
        private Vector3 cartFollowWorldPosition;
        private RoActorDirection cartFollowFacingDirection = RoActorDirection.South;
        private float cartFollowAccumulatedDistanceCells;
        private int lastCartFollowFrame;
        private UnityEngine.Camera GetCamera() => _camera != null ? _camera : (_camera = UnityEngine.Camera.main);

        /// <summary>
        /// The camera whose axes carry the ACT anchor offsets (the main camera by default), with the anchors placed again
        /// at once: an off-screen render (a UI portrait) looks along its own camera, and a layer offset by its anchor (a
        /// Doram head sits 22 px under its own origin) would otherwise drift by the main camera's tilt.
        /// </summary>
        public void UseCamera(UnityEngine.Camera camera) {
            _camera = camera;
            anchorsDirty = true;
            ApplyLayerAnchorOffsets();
        }

        public RoActorSpriteVisual PrimaryVisual => layers.Count > 0 ? layers[0].Visual : null;
        public int LayerCount => layers.Count;

        /// <summary>The classic engine's actor colour on every layer (white = none).</summary>
        public void SetEffectTint(Color color) {
            for (int i = 0; i < layers.Count; i++) {
                layers[i].Visual?.SetEffectTint(color);
            }
        }

        public bool HasLayer(RoActorSpriteLayer kind) {
            for (int i = 0; i < layers.Count; i++) {
                if (layers[i].Layer == kind && layers[i].Visual != null) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Layer <paramref name="index"/> in draw order (effect looks copy each layer).</summary>
        public bool TryGetLayer(int index, out RoActorSpriteLayer kind, out RoActorSpriteVisual visual) {
            if (index < 0 || index >= layers.Count) {
                kind = default;
                visual = null;
                return false;
            }

            kind = layers[index].Layer;
            visual = layers[index].Visual;
            return true;
        }
        public RoSpriteMotion Motion => motion;

        /// <summary>Diagnostics: every layer — draw state and the angle between its facing and the camera's.</summary>
        public string DescribeLayers() {
            var text = new System.Text.StringBuilder();
            Quaternion camera = RoActorTickService.FrameCameraRotation;
            for (int i = 0; i < layers.Count; i++) {
                RoActorSpriteVisual visual = layers[i].Visual;
                text.Append(" [").Append(layers[i].Layer).Append(' ');
                if (visual == null) {
                    text.Append("destroyed]");
                    continue;
                }

                text.Append(visual.gameObject.activeInHierarchy ? "on" : "off")
                    .Append(" angle=").Append(Quaternion.Angle(visual.transform.rotation, camera).ToString("F0"))
                    .Append(' ').Append(visual.Animator != null ? visual.Animator.DescribeDrawState() : "no animator").Append(']');
            }

            return "layers=" + layers.Count + text;
        }

        /// <summary>Diagnostics: the body layer's ACT action and frame.</summary>
        public string DescribeBodyFrame() {
            RoActorSpriteVisual primary = PrimaryVisual;
            return primary == null ? "no body" : "act=" + primary.ActionIndex + " frame=" + primary.FrameIndex;
        }
        public RoActorDirection FacingDirection => facingDirection;
        public int HeadDirectionFrameIndex => headDirectionFrameIndex;
        public bool HoldCurrentMotionFirstFrame => holdCurrentMotionFirstFrame;
        public bool HoldCurrentMotionCurrentFrame => holdCurrentMotionCurrentFrame;
        public int RenderQueueBase => renderQueueBase;
        public int SortingOrderBase => sortingOrderBase;
        public RoShadowRenderMode ShadowRenderMode => shadowRenderMode;

        public bool Initialize(
            RoAssetDatabase assets,
            IReadOnlyList<RoActorSpriteLayerSpec> layerSpecs,
            RoSpriteMotion initialMotion = RoSpriteMotion.Idle,
            RoActorDirection initialFacingDirection = RoActorDirection.South
        ) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            ClearLayers();
            motion = initialMotion;
            facingDirection = initialFacingDirection;
            anchorsDirty = true;

            FillSortedSpecsForRendering(layerSpecs, sortedLayerSpecScratch);
            hasAnchorDrivenLayers = ContainsAnchorDrivenLayer(sortedLayerSpecScratch);
            hasCameraOffsetLayers = ContainsCameraOffsetLayer(sortedLayerSpecScratch);
            for (int i = 0; i < sortedLayerSpecScratch.Count; i++) {
                TryCreateLayer(assets, sortedLayerSpecScratch[i]);
            }

            sortedLayerSpecScratch.Clear();

            SetMotionAndFacing(motion, facingDirection, restart: false, force: true);
            ApplyLayerAnchorOffsets();
            return layers.Count > 0;
        }

        public bool Initialize(RoAssetDatabase assets, RoActorAppearanceRequest appearance) {
            if (appearance == null) {
                throw new ArgumentNullException(nameof(appearance));
            }

            RoActorSpriteLayerSpec[] layerSpecs = RoActorSpritePathResolver.BuildPlayerLayerSpecs(assets, appearance);
            robeId = appearance.robeId;
            robeBodyJob = appearance.classId;
            robeIsMale = appearance.isMale;
            if (robeId > 0) {
                RoRobeDrawRules.Ensure(assets);
            }

            imfDrawOrder = null;
            for (int i = 0; i < layerSpecs.Length; i++) {
                if (layerSpecs[i] != null && layerSpecs[i].layer == RoActorSpriteLayer.Body) {
                    imfDrawOrder = RoImfDrawOrder.ForBody(assets, layerSpecs[i].basePath);
                    break;
                }
            }

            bool initialized = Initialize(assets, layerSpecs, appearance.motion, appearance.facingDirection);
            SetHeadDirectionFrameIndex((int) appearance.headFacing);
            return initialized;
        }

        public void SetMotionAndFacing(RoSpriteMotion newMotion, RoActorDirection newFacingDirection, bool restart = true, bool force = false) {
            RoActorDirection resolvedDirection = newFacingDirection == RoActorDirection.None ? facingDirection : newFacingDirection;
            bool motionChanged = motion != newMotion;
            bool directionChanged = facingDirection != resolvedDirection;
            if (!force && !motionChanged && !directionChanged) {
                return;
            }

            bool shouldRestart = restart && (motionChanged || force);
            motion = newMotion;
            facingDirection = resolvedDirection;
            anchorsDirty = true;
            if (motion != RoSpriteMotion.Idle && motion != RoSpriteMotion.Sit) {
                headDirectionFrameIndex = 0;
            }

            for (int i = 0; i < layers.Count; i++) {
                RoActorLayerInstance layer = layers[i];
                layer.Visual?.SetMotionAndFacing(motion, facingDirection, shouldRestart, force);
                ApplyLayerRenderOrder(layer);
                ApplyLayerFramePolicy(layer);
            }

            ApplyLayerAnchorOffsets();
        }

        public void SetHeadDirectionFrameIndex(int frameIndex) {
            int clampedFrameIndex = Mathf.Clamp(frameIndex, 0, 2);
            if (headDirectionFrameIndex == clampedFrameIndex) {
                return;
            }

            headDirectionFrameIndex = clampedFrameIndex;
            anchorsDirty = true;
            ApplyLayerFramePolicies();
            ApplyLayerAnchorOffsets();
        }

        public void SetMotionFirstFrameHeld(bool held) {
            if (holdCurrentMotionFirstFrame == held) {
                return;
            }

            holdCurrentMotionFirstFrame = held;
            ApplyLayerFramePolicies();
            ApplyLayerAnchorOffsets();
        }

        public void SetMotionCurrentFrameHeld(bool held) {
            if (holdCurrentMotionCurrentFrame == held) {
                return;
            }

            holdCurrentMotionCurrentFrame = held;
            ApplyLayerFramePolicies();
            ApplyLayerAnchorOffsets();
        }

        public static int CalculateHeadDirectionFrameIndex(RoActorDirection bodyFacingDirection, Vector3 worldLookDelta) {
            worldLookDelta.y = 0f;
            if (worldLookDelta.sqrMagnitude <= 0.0001f) {
                return 0;
            }

            RoActorDirection lookDirection = RoSpriteActionMap.GetDirectionFromWorldDelta(worldLookDelta, bodyFacingDirection);
            return CalculateHeadDirectionFrameIndex(bodyFacingDirection, lookDirection);
        }

        public static int CalculateHeadDirectionFrameIndex(RoActorDirection bodyFacingDirection, RoActorDirection lookDirection) {
            return GetHeadDirectionFrameIndexForStep(CalculateShortestDirectionStep(bodyFacingDirection, lookDirection));
        }

        public static int GetHeadDirectionFrameIndexForStep(int directionStep) {
            if (directionStep == 0) {
                return 0;
            }

            return directionStep > 0 ? 1 : 2;
        }

        public static int CalculateShortestDirectionStep(RoActorDirection from, RoActorDirection to) {
            int delta = PositiveModulo(DirectionToOctantIndex(to) - DirectionToOctantIndex(from), 8);
            return delta > 4 ? delta - 8 : delta;
        }

        public static RoActorDirection StepFacingDirection(RoActorDirection from, int directionStep) {
            if (directionStep == 0) {
                return from;
            }

            int sign = directionStep > 0 ? 1 : -1;
            return OctantIndexToDirection(DirectionToOctantIndex(from) + sign);
        }

        public static Vector3 CalculateActAnchorOffset(Vector2Int bodyAnchor, Vector2Int layerAnchor) {
            Vector2Int diff = bodyAnchor - layerAnchor;
            return new Vector3(diff.x, -diff.y, 0f) / SPR.PIXELS_PER_UNIT;
        }

        public static Vector3 CalculateAnchoredLayerWorldPosition(
            Vector3 baseWorldPosition,
            Vector3 cameraRight,
            Vector3 cameraUp,
            Vector3 anchorOffset
        ) {
            return baseWorldPosition +
                cameraRight.normalized * anchorOffset.x +
                cameraUp.normalized * anchorOffset.y;
        }

        public static Vector3 CalculateSpritePixelOffsetWorldPosition(
            Vector3 baseWorldPosition,
            Vector3 cameraRight,
            Vector3 cameraUp,
            int offsetPixelsX,
            int offsetPixelsY
        ) {
            var pixelOffset = new Vector3(offsetPixelsX, -offsetPixelsY, 0f) / SPR.PIXELS_PER_UNIT;
            return CalculateAnchoredLayerWorldPosition(baseWorldPosition, cameraRight, cameraUp, pixelOffset);
        }

        public static RoActorSpriteLayerSpec[] SortSpecsForRendering(IReadOnlyList<RoActorSpriteLayerSpec> layerSpecs) {
            if (layerSpecs == null || layerSpecs.Count == 0) {
                return Array.Empty<RoActorSpriteLayerSpec>();
            }

            int validCount = 0;
            for (int i = 0; i < layerSpecs.Count; i++) {
                if (IsRenderableLayerSpec(layerSpecs[i])) {
                    validCount++;
                }
            }

            if (validCount == 0) {
                return Array.Empty<RoActorSpriteLayerSpec>();
            }

            var sorted = new RoActorSpriteLayerSpec[validCount];
            int writeIndex = 0;
            for (int i = 0; i < layerSpecs.Count; i++) {
                RoActorSpriteLayerSpec spec = layerSpecs[i];
                if (IsRenderableLayerSpec(spec)) {
                    sorted[writeIndex++] = spec;
                }
            }

            Array.Sort(sorted, RenderOrderComparison);
            return sorted;
        }

        private static void FillSortedSpecsForRendering(
            IReadOnlyList<RoActorSpriteLayerSpec> layerSpecs,
            List<RoActorSpriteLayerSpec> sorted
        ) {
            sorted.Clear();
            if (layerSpecs == null || layerSpecs.Count == 0) {
                return;
            }

            for (int i = 0; i < layerSpecs.Count; i++) {
                RoActorSpriteLayerSpec spec = layerSpecs[i];
                if (!IsRenderableLayerSpec(spec)) {
                    continue;
                }

                sorted.Add(spec);
            }

            sorted.Sort(RenderOrderComparison);
        }

        private static bool IsRenderableLayerSpec(RoActorSpriteLayerSpec spec) {
            return spec != null && !string.IsNullOrWhiteSpace(spec.basePath);
        }

        private static int CompareLayerRenderOrder(RoActorSpriteLayerSpec left, RoActorSpriteLayerSpec right) {
            return GetRenderOrder(left.layer).CompareTo(GetRenderOrder(right.layer));
        }

        public static int GetRenderOrder(RoActorSpriteLayer layer) {
            return layer switch {
                RoActorSpriteLayer.CartShadow => -20,
                RoActorSpriteLayer.Cart => -10,
                RoActorSpriteLayer.Body => 0,
                RoActorSpriteLayer.Head => 10,
                RoActorSpriteLayer.Hair => 20,
                RoActorSpriteLayer.Weapon => 30,
                RoActorSpriteLayer.WeaponTrail => 35,
                RoActorSpriteLayer.Shield => 40,
                RoActorSpriteLayer.Garment => 50,
                RoActorSpriteLayer.HeadgearLower => 60,
                RoActorSpriteLayer.HeadgearMid => 70,
                RoActorSpriteLayer.HeadgearTop => 80,
                _ => 100
            };
        }

        /// <summary>
        /// The layers the client draws with blend 0x101 into its opaque sprite list 0x180, the only sprite pixels that write
        /// depth (0x5e7900: slots 0, 1, 5, 6, 7 = body, head, weapon / shield; the robe reuses the body's flags);
        /// headgear (slots 2, 3, 4) and the weapon trail go to the translucent list without depth (packages/sprite-effect-depth).
        /// </summary>
        public static bool WritesSpriteDepth(RoActorSpriteLayer layer) {
            return layer switch {
                RoActorSpriteLayer.HeadgearTop or RoActorSpriteLayer.HeadgearMid or RoActorSpriteLayer.HeadgearLower => false,
                RoActorSpriteLayer.WeaponTrail or RoActorSpriteLayer.CartShadow => false,
                _ => true
            };
        }

        public static int GetRenderQueueForLayer(int baseQueue, RoActorSpriteLayer layer) {
            return baseQueue + GetRenderOrder(layer);
        }

        public static int GetSortingOrderForLayer(int baseOrder, RoActorSpriteLayer layer) {
            return baseOrder + GetRenderOrder(layer);
        }

        private int ResolveRenderOrderForLayer(RoActorSpriteLayer layer) {
            if (layer == RoActorSpriteLayer.CartShadow || layer == RoActorSpriteLayer.Cart) {
                return RoMapRentalVisualSemantics.IsCartInFrontOfBody(ResolveCartDirectionIndexForCartRendering()) ? 90 : GetRenderOrder(layer);
            }

            return GetRenderOrder(layer);
        }

        private int ResolveRenderQueueForLayer(int baseQueue, RoActorSpriteLayer layer) {
            // under the client's draw order the sorting order alone places a layer (RoClientDrawOrder.SharedRenderQueue)
            return RoClientDrawOrder.Enabled ? RoClientDrawOrder.SharedRenderQueue : baseQueue + ResolveRenderOrderForLayer(layer);
        }

        private int ResolveSortingOrderForLayer(int baseOrder, RoActorSpriteLayer layer) {
            return baseOrder + ResolveRenderOrderForLayer(layer);
        }

        private void ApplyLayerRenderOrder(RoActorLayerInstance layer) {
            if (layer.Visual == null) {
                return;
            }

            layer.Visual.SetRenderQueue(ResolveRenderQueueForLayer(renderQueueBase, layer.Layer));
            layer.Visual.SetSpriteDepthWrite(WritesSpriteDepth(layer.Layer));
            RoActSpriteAnimator animator = layer.Visual.Animator;
            if (animator != null) {
                animator.SortingOrderSetExternally = LayerOrderSetByClientDrawOrder;
            }

            layer.Visual.SetSortingOrder(ResolveSortingOrderForLayer(sortingOrderBase, layer.Layer));
        }

        private void ApplyLayerVisibility(RoActorLayerInstance layer) {
            if (layer.Visual == null) {
                return;
            }

            bool active = ShouldLayerBeActive(layer.Layer);
            GameObject layerObject = layer.Visual.gameObject;
            if (layerObject.activeSelf != active) {
                layerObject.SetActive(active);
            }
        }

        private bool ShouldLayerBeActive(RoActorSpriteLayer layer) {
            return layer != RoActorSpriteLayer.CartShadow ||
                shadowRenderMode == RoShadowRenderMode.Original;
        }

        private int ResolveCartDirectionIndex() {
            UnityEngine.Camera camera = GetCamera();
            return ResolveCartDirectionIndex(camera);
        }

        private int ResolveCartDirectionIndexForCartRendering() {
            UnityEngine.Camera camera = GetCamera();
            return ResolveCartDirectionIndexForCartRendering(camera);
        }

        private int ResolveCartDirectionIndex(UnityEngine.Camera camera) {
            return ResolveCartDirectionIndex(camera, facingDirection);
        }

        private int ResolveCartDirectionIndexForCartRendering(UnityEngine.Camera camera) {
            RoActorDirection cartDirection = cartFollowInitialized
                ? cartFollowFacingDirection
                : facingDirection;
            return ResolveCartDirectionIndex(camera, cartDirection);
        }

        private int ResolveCartDirectionIndex(UnityEngine.Camera camera, RoActorDirection actorDirection) {
            RoActorDirection cameraDirection = camera != null
                ? RoSpriteActionMap.GetCameraDirectionFromYaw(camera.transform.eulerAngles.y)
                : RoActorDirection.South;
            return ResolveCartDirectionIndex(actorDirection, cameraDirection);
        }

        private static int ResolveCartDirectionIndex(RoActorDirection actorDirection, RoActorDirection cameraDirection) {
            int actor = actorDirection == RoActorDirection.None ? 0 : (int) actorDirection;
            int camera = cameraDirection == RoActorDirection.None ? 0 : (int) cameraDirection;
            return PositiveModulo(actor + camera, 8);
        }

        public void SetRenderQueueBase(int baseQueue) {
            if (renderQueueBase == baseQueue) {
                return;
            }

            renderQueueBase = baseQueue;
            for (int i = 0; i < layers.Count; i++) {
                RoActorLayerInstance layer = layers[i];
                layer.Visual?.SetRenderQueue(ResolveRenderQueueForLayer(renderQueueBase, layer.Layer));
            }
        }

        public void SetSortingOrderBase(int baseOrder) {
            if (sortingOrderBase == baseOrder) {
                return;
            }

            sortingOrderBase = baseOrder;
            ApplySortingOrderBase();
        }

        private void ApplySortingOrderBase() {
            SortingGroup group = EnsureSortingGroup();
            // the client draws the layers of an actor in different lists (body before every effect, headgear among
            // them): no group may keep them together (RoClientDrawOrder); a disabled group's order is not written either
            bool groupWanted = !RoClientDrawOrder.Enabled;
            if (group != null) {
                if (!sortingGroupStateApplied || sortingGroupEnabledApplied != groupWanted) {
                    group.enabled = groupWanted;
                    sortingGroupEnabledApplied = groupWanted;
                    sortingGroupStateApplied = true;
                }

                if (groupWanted) {
                    group.sortingOrder = sortingOrderBase;
                }
            }

            for (int i = 0; i < layers.Count; i++) {
                RoActorLayerInstance layer = layers[i];
                layer.Visual?.SetSortingOrder(ResolveSortingOrderForLayer(sortingOrderBase, layer.Layer));
            }
        }

        private bool sortingGroupStateApplied;
        private bool sortingGroupEnabledApplied;
        /// <summary>The last frame RoClientDrawOrder collected this actor (its layers' orders are assigned there).</summary>
        private int clientDrawOrderFrame = -100;

        /// <summary>Whether this actor's layer orders come from RoClientDrawOrder (collected in the last frames).</summary>
        private bool LayerOrderSetByClientDrawOrder => RoClientDrawOrder.Enabled && Time.frameCount - clientDrawOrderFrame <= 2;

        /// <summary>RoClientDrawOrder went on or off: every actor puts its groups and layers in the matching state.</summary>
        public static void ReapplySortingOrderForAll() {
            foreach (RoActorLayeredVisual visual in FindObjectsByType<RoActorLayeredVisual>(FindObjectsSortMode.None)) {
                visual.sortingGroupStateApplied = false;
                visual.ApplySortingOrderBase();
                for (int i = 0; i < visual.layers.Count; i++) {
                    visual.ApplyLayerRenderOrder(visual.layers[i]);
                }
            }
        }

        public void SetFrameDelayScale(float scale) {
            for (int i = 0; i < layers.Count; i++) {
                layers[i].Visual?.SetFrameDelayScale(scale);
            }
        }

        public void SetAbsoluteFrameSeconds(float perFrameSeconds) {
            for (int i = 0; i < layers.Count; i++) {
                layers[i].Visual?.SetAbsoluteFrameSeconds(perFrameSeconds);
            }
        }

        public bool TryScaleCurrentActionsToDurationSeconds(float durationSeconds, float fallbackScale = 1f) {
            if (durationSeconds <= 0f || layers.Count == 0) {
                return false;
            }

            bool applied = false;
            for (int i = 0; i < layers.Count; i++) {
                RoActorSpriteVisual visual = layers[i].Visual;
                if (visual == null) {
                    continue;
                }

                float scale = Mathf.Max(0.0001f, fallbackScale);
                if ((visual.TryGetCurrentActionBaseDurationSeconds(out float baseDurationSeconds) ||
                    visual.TryGetCurrentActionDurationSeconds(out baseDurationSeconds)) &&
                    baseDurationSeconds > 0f) {
                    scale = Mathf.Max(0.0001f, durationSeconds / baseDurationSeconds);
                }

                visual.SetFrameDelayScale(scale);
                applied = true;
            }

            return applied;
        }

        public void SetLoop(bool enabled) {
            for (int i = 0; i < layers.Count; i++) {
                layers[i].Visual?.SetLoop(enabled);
            }
        }

        public void SetOpacity(float alpha) {
            for (int i = 0; i < layers.Count; i++) {
                layers[i].Visual?.SetOpacity(alpha);
            }
        }

        public void SetTintColor(Color color) {
            for (int i = 0; i < layers.Count; i++) {
                layers[i].Visual?.SetTintColor(color);
            }
        }

        public void SetMapLight(Color multiply, Color add) {
            for (int i = 0; i < layers.Count; i++) {
                layers[i].Visual?.SetMapLight(multiply, add);
            }
        }

        public void SetRuntimeSpriteBudgets(bool meshBuilds, bool visibleAnimationTicks) {
            budgetRuntimeMeshBuilds = meshBuilds;
            budgetVisibleAnimationTicks = visibleAnimationTicks;
            for (int i = 0; i < layers.Count; i++) {
                layers[i].Visual?.SetRuntimeSpriteBudgets(meshBuilds, visibleAnimationTicks);
            }
        }

        public void SetUseMutableRuntimeMeshes(bool enabled) {
            useMutableRuntimeMeshes = enabled;
            for (int i = 0; i < layers.Count; i++) {
                layers[i].Visual?.SetUseMutableRuntimeMesh(enabled);
            }
        }

        public int PrebuildAllFrameMeshes() {
            int built = 0;
            for (int i = 0; i < layers.Count; i++) {
                if (layers[i].Visual != null) {
                    built += layers[i].Visual.PrebuildAllFrameMeshes();
                }
            }

            return built;
        }

        public void SetShadowRenderMode(RoShadowRenderMode mode, RoEnhancedShadowSettings settings = null) {
            shadowRenderMode = mode;
            enhancedShadowSettings = settings;
            for (int i = 0; i < layers.Count; i++) {
                RoActorLayerInstance layer = layers[i];
                layer.Visual?.SetShadowRenderMode(mode, settings);
                layer.Visual?.SetCastsRealtimeShadow(mode == RoShadowRenderMode.Enhanced &&
                    ShouldLayerCastRealtimeShadow(layer.Layer));
                ApplyLayerVisibility(layer);
            }

            UpdateSharedActorShadowCards();
        }

        public bool TryGetCurrentActionDurationSeconds(out float durationSeconds) {
            durationSeconds = 0f;
            RoActorSpriteVisual primary = PrimaryVisual;
            return primary != null && primary.TryGetCurrentActionDurationSeconds(out durationSeconds);
        }

        /// <summary>The body layer's authored length of <paramref name="motion"/> (frames × ACT delay, speed 1).</summary>
        public bool TryGetMotionBaseDurationSeconds(RoSpriteMotion motion, out float durationSeconds) {
            durationSeconds = 0f;
            RoActorSpriteVisual primary = PrimaryVisual;
            return primary != null && primary.TryGetMotionBaseDurationSeconds(motion, out durationSeconds);
        }

        public bool TryGetCurrentActionBaseDurationSeconds(out float durationSeconds) {
            durationSeconds = 0f;
            RoActorSpriteVisual primary = PrimaryVisual;
            return primary != null && primary.TryGetCurrentActionBaseDurationSeconds(out durationSeconds);
        }

        /// <summary>
        /// Delegates to the primary (body) sprite layer so the presenter can wait for the attack
        /// animation to finish before handing off to Standby, matching roBrowser's frame-based
        /// completion check.
        /// </summary>
        public bool HasFinishedCurrentAction() {
            RoActorSpriteVisual primary = PrimaryVisual;
            return primary != null && primary.HasFinishedCurrentAction();
        }

        private bool TryCreateLayer(RoAssetDatabase assets, RoActorSpriteLayerSpec spec) {
            string basePath = RoActorSpriteVisual.NormalizeBasePath(spec.basePath);
            if (string.IsNullOrEmpty(basePath)) {
                return false;
            }

            var layerObject = new GameObject($"RO Actor Layer - {spec.layer} - {basePath}");
            layerObject.transform.SetParent(EnsureLayerPivot(), false);
            layerObject.transform.localPosition = spec.localOffset;

            var visual = layerObject.AddComponent<RoActorSpriteVisual>();
            visual.SetBillboardedByOwner(true);
            billboardCameraRevision = 0;
            try {
                visual.SetRuntimeSpriteBudgets(budgetRuntimeMeshBuilds, budgetVisibleAnimationTicks);
                visual.SetUseMutableRuntimeMesh(useMutableRuntimeMeshes);
                visual.SetShadowRenderMode(shadowRenderMode, enhancedShadowSettings);
                visual.SetCastsRealtimeShadow(shadowRenderMode == RoShadowRenderMode.Enhanced &&
                    ShouldLayerCastRealtimeShadow(spec.layer));
                visual.SetMotionProfile(spec.motionProfile);
                if (!visual.Initialize(assets, basePath, spec.palettePath, spec.initialActionIndex)) {
                    DestroyRuntimeObject(layerObject);
                    return false;
                }

                layerObject.transform.localScale = Vector3.one * Mathf.Max(0.0001f, spec.scale);
                visual.Animator?.SetTickedByOwner(true);
                visual.SetActionDrivenByPrimary(UsesBodyAnchor(spec.layer));
                // Only the body layer drives ACT frame sounds (attack swing, footsteps), so equipment
                // and head layers don't double-trigger the same sound.
                visual.SetPlayFrameSounds(spec.layer == RoActorSpriteLayer.Body);
                visual.SetRenderQueue(ResolveRenderQueueForLayer(renderQueueBase, spec.layer));
                visual.SetSpriteDepthWrite(WritesSpriteDepth(spec.layer));
                visual.SetSortingOrder(ResolveSortingOrderForLayer(sortingOrderBase, spec.layer));
                var layer = new RoActorLayerInstance(spec.layer, visual, spec.localOffset);
                layers.Add(layer);
                ApplyLayerFramePolicy(layer);
                ApplyLayerVisibility(layer);
                UpdateSharedActorShadowCards();
                return true;
            } catch (Exception ex) {
                DestroyRuntimeObject(layerObject);
                Debug.LogWarning($"Could not create actor layer '{spec.layer}' from '{basePath}.spr/.act': {ex.Message}", this);
                return false;
            }
        }

        private void OnDestroy() {
            ClearLayers();
        }

        private SortingGroup EnsureSortingGroup() {
            if (sortingGroup == null) {
                sortingGroup = GetComponent<SortingGroup>();
                if (sortingGroup == null) {
                    sortingGroup = gameObject.AddComponent<SortingGroup>();
                }

                sortingGroup.enabled = !RoClientDrawOrder.Enabled;
                sortingGroupEnabledApplied = sortingGroup.enabled;
                sortingGroupStateApplied = true;
            }

            return sortingGroup;
        }

        public void SetWalkAnimationDistance(float accumulatedDistanceCells) {
            SetWalkAnimationDistance(accumulatedDistanceCells, null);
        }

        public void SetWalkAnimationDistance(float accumulatedDistanceCells, RoMovementTuning tuning) {
            if (motion != RoSpriteMotion.Walk || layers.Count == 0) {
                return;
            }

            bool hasCart = false;
            if (walkFrameScratch.Length < layers.Count * 2) {
                walkFrameScratch = new int[layers.Count * 2];
            }

            for (int i = 0; i < layers.Count; i++) {
                RoActSpriteAnimator layerAnimator = layers[i].Visual != null ? layers[i].Visual.Animator : null;
                walkFrameScratch[i * 2] = layers[i].Visual != null ? layers[i].Visual.ActionIndex : -1;
                walkFrameScratch[i * 2 + 1] = layerAnimator != null ? layerAnimator.FrameIndex : -1;
                hasCart |= IsCartLayer(layers[i].Layer);
            }

            if (TryGetLayer(RoActorSpriteLayer.Body, out RoActorLayerInstance bodyLayer) &&
                bodyLayer.Visual != null &&
                bodyLayer.Visual.SetWalkFrameIndexFromDistance(accumulatedDistanceCells, tuning)) {
                int bodyActionIndex = bodyLayer.Visual.ActionIndex;
                int bodyFrameIndex = bodyLayer.Visual.Animator != null ? bodyLayer.Visual.Animator.FrameIndex : 0;
                for (int i = 0; i < layers.Count; i++) {
                    RoActorLayerInstance layer = layers[i];
                    if (layer.Visual == null || layer.Layer == RoActorSpriteLayer.Body) {
                        continue;
                    }

                    if (ShouldSyncFrameToBody(layer.Layer)) {
                        layer.Visual.SetActionAndFrameFromPrimary(bodyActionIndex, bodyFrameIndex);
                    } else if (IsCartLayer(layer.Layer)) {
                        ApplyCartWalkAnimationDistance(layer, tuning);
                    } else {
                        layer.Visual.SetWalkFrameIndexFromDistance(accumulatedDistanceCells, tuning);
                    }
                }
            } else {
                for (int i = 0; i < layers.Count; i++) {
                    RoActorLayerInstance layer = layers[i];
                    if (IsCartLayer(layer.Layer)) {
                        ApplyCartWalkAnimationDistance(layer, tuning);
                    } else {
                        layer.Visual?.SetWalkFrameIndexFromDistance(accumulatedDistanceCells, tuning);
                    }
                }
            }

            // the anchors follow the layers' frames: the late tick (before the client draw order reads the layers) applies
            // them once when a frame changed, instead of here at every presenter sync of every walker as well (the walk
            // frame changes every few frames); a cart keeps its follow update here
            if (hasCart) {
                ApplyLayerAnchorOffsets();
                return;
            }

            for (int i = 0; i < layers.Count; i++) {
                RoActSpriteAnimator layerAnimator = layers[i].Visual != null ? layers[i].Visual.Animator : null;
                if (walkFrameScratch[i * 2] != (layers[i].Visual != null ? layers[i].Visual.ActionIndex : -1) ||
                    walkFrameScratch[i * 2 + 1] != (layerAnimator != null ? layerAnimator.FrameIndex : -1)) {
                    anchorsDirty = true;
                    break;
                }
            }
        }

        private int[] walkFrameScratch = new int[16];

        private void OnEnable() {
            RoActorTickService.Register((IRoActorUpdateTick) this);
            RoActorTickService.Register((IRoActorLateTick) this);
            RoClientDrawOrder.Register(this);
        }

        private void OnDisable() {
            RoActorTickService.Unregister((IRoActorUpdateTick) this);
            RoActorTickService.Unregister((IRoActorLateTick) this);
            RoClientDrawOrder.Unregister(this);
        }

        /// <summary>
        /// This actor's layers for the client's render lists (<see cref="RoClientDrawOrder"/>): flags 0x101 (opaque list
        /// 0x180) for the layers that write depth while the actor is opaque, else 1 (translucent list 0x18c); key = the
        /// client's sprite rhw at the layer's top edge, 1 / (vz + 0.16 dy) with vz the ground point's view depth in RO units
        /// and dy the pixels from the ground point (y down), plus the AddSprite bias n · 2e-4 of the layer's submission
        /// position (vf09 0xbce6f0, AddSprite 0xbd7930).
        /// </summary>
        public void CollectClientDrawEntries(UnityEngine.Camera camera, in RoClientDrawFrame frame, List<RoClientDrawEntry> into) {
            if (!isActiveAndEnabled || layers.Count == 0) {
                return;
            }

            clientDrawOrderFrame = Time.frameCount;

            Vector3 feet = transform.position;
            float depth = frame.ViewDepth(feet);
            if (depth <= frame.NearClip) {
                return;
            }

            float feetY = frame.ScreenY(feet);
            float vzRo = depth * ClientRoUnitsPerUnity;
            // directions 2..5 face away from the camera: the client inserts the shield first (draw-order §1.4)
            int direction = ResolveCartDirectionIndex(facingDirection, frame.CameraDirection);
            bool facingAway = direction >= 2 && direction <= 5;
            RoRobeDrawRules.Pass robePass = ResolveRobePass();
            bool headFirst = ResolveHeadFirst();
            for (int i = 0; i < layers.Count; i++) {
                RoActorLayerInstance layer = layers[i];
                RoActSpriteAnimator animator = layer.Visual != null ? layer.Visual.Animator : null;
                MeshRenderer layerRenderer = animator != null ? animator.LayerRenderer : null;
                // the renderer and layer object states from what this actor last set (ApplyLayerVisibility, the
                // animator's SetRendererEnabled) instead of three native reads per layer per frame
                if (layerRenderer == null || !animator.RendererEnabledHint || !ShouldLayerBeActive(layer.Layer)) {
                    continue;
                }

                float layerDepth = depth;
                float layerFeetY = feetY;
                float layerVzRo = vzRo;
                if (IsCartLayer(layer.Layer)) {
                    // the cart is a separate object of the client (a CMsgEffect, draw-order §3): drawn like a monster at its
                    // own ground point, the follow position behind the owner, so it is keyed from that point
                    Vector3 cartFeet = layer.Visual.transform.position;
                    layerDepth = frame.ViewDepth(cartFeet);
                    if (layerDepth <= frame.NearClip) {
                        continue;
                    }

                    layerFeetY = frame.ScreenY(cartFeet);
                    layerVzRo = layerDepth * ClientRoUnitsPerUnity;
                }

                Bounds bounds = layerRenderer.bounds;
                float topY = frame.ScreenY(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
                float dyDown = -(topY - layerFeetY);
                float lean = layerVzRo + 0.16f * dyDown;
                int rank = ClientLayerBias(layer.Layer, facingAway, robePass, headFirst);
                animator.SetSpriteLayerRank(rank);
                float key = (lean > 0.001f ? 1f / lean : 1000f) + rank * 2e-4f;
                bool opaque = WritesSpriteDepth(layer.Layer) && animator.DrawAlpha >= 0.999f;
                into.Add(new RoClientDrawEntry {
                    Renderer = layerRenderer, Flags = opaque ? 0x101 : 1, Key = key, FeetDepth = layerDepth, FeetScreenY = layerFeetY,
                });
            }
        }

        /// <summary>Diagnostics (the !clip frame log): the cart's follow state, facing and action this frame, or null without a cart.</summary>
        internal string DescribeCartForDiagnostics() {
            if (!TryGetLayer(RoActorSpriteLayer.Cart, out RoActorLayerInstance cart) || cart.Visual == null) {
                return null;
            }

            System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;
            Vector3 delta = cart.Visual.transform.position - transform.position;
            RoActSpriteAnimator animator = cart.Visual.Animator;
            int bodyAction = TryGetLayer(RoActorSpriteLayer.Body, out RoActorLayerInstance body) && body.Visual != null ? body.Visual.ActionIndex : -1;
            Vector3 owner = transform.TransformPoint(cart.BaseLocalOffset);
            return name + " motion " + motion + " face " + facingDirection + " bodyAct " + bodyAction +
                " cartFace " + (cartFollowInitialized ? cartFollowFacingDirection.ToString() : "-") +
                " cartAct " + cart.Visual.ActionIndex + " cartFrame " + (animator != null ? animator.FrameIndex : -1) +
                " delta " + delta.x.ToString("F2", invariant) + "," + delta.z.ToString("F2", invariant) +
                " owner " + owner.x.ToString("F2", invariant) + "," + owner.z.ToString("F2", invariant) +
                " follow " + cartFollowWorldPosition.x.ToString("F2", invariant) + "," + cartFollowWorldPosition.z.ToString("F2", invariant) +
                " placed " + cart.Visual.transform.position.x.ToString("F2", invariant) + "," + cart.Visual.transform.position.z.ToString("F2", invariant) +
                " followFrame " + lastCartFollowFrame +
                (TryGetLayer(RoActorSpriteLayer.CartShadow, out RoActorLayerInstance shadow) && shadow.Visual != null
                    ? " shadow " + shadow.Visual.transform.position.x.ToString("F2", invariant) + "," + shadow.Visual.transform.position.z.ToString("F2", invariant) +
                      (shadow.Visual.gameObject.activeInHierarchy ? " on" : " off")
                    : " noShadow") +
                " layers " + layers.Count;
        }

        const float ClientRoUnitsPerUnity = 5f;

        /// <summary>
        /// The layer's AddSprite bias n: its rank in the client's key-ordered layer map (0x5e63b0, flushed by 0x5e621f;
        /// reference packages/draw-order §1): body, robe, head, weapon, weapon trail, shield by insertion, then the
        /// headgear by their Lua priority (mid 100, top 200, lower 300); facing away the shield is inserted first.
        /// </summary>
        // the robe's draw pass inputs (RoRobeDrawRules): robe id, the body job and sex the client passes to DrawOnTop
        private int robeId;
        private int robeBodyJob;
        private bool robeIsMale;

        /// <summary>Diagnostics: the robe pass inputs and result this frame.</summary>
        public string DescribeRobePass() {
            return "robe=" + robeId + " job=" + robeBodyJob + " male=" + robeIsMale + " rulesReady=" + RoRobeDrawRules.IsReady + " pass=" + ResolveRobePass();
        }

        /// <summary>The robe's pass this frame, from its ACT action and frame (the player's, the robe follows the body).</summary>
        private RoRobeDrawRules.Pass ResolveRobePass() {
            if (robeId <= 0 || !TryGetLayer(RoActorSpriteLayer.Garment, out RoActorLayerInstance garment) || garment.Visual == null) {
                return RoRobeDrawRules.Pass.AfterBody;
            }

            RoActSpriteAnimator animator = garment.Visual.Animator;
            return RoRobeDrawRules.Resolve(robeId, robeIsMale, robeBodyJob, garment.Visual.ActionIndex, animator != null ? animator.FrameIndex : 0);
        }

        // the job's IMF (head before the body per action and frame), null = always the body first
        private RoImfDrawOrder imfDrawOrder;

        /// <summary>Whether the body's current ACT action and frame draw the head before the body (<see cref="RoImfDrawOrder"/>).</summary>
        private bool ResolveHeadFirst() {
            if (imfDrawOrder == null || !TryGetLayer(RoActorSpriteLayer.Body, out RoActorLayerInstance body) || body.Visual == null) {
                return false;
            }

            return imfDrawOrder.IsHeadFirst(body.Visual.ActionIndex, body.Visual.FrameIndex);
        }

        /// <summary>Diagnostics: the IMF state this frame.</summary>
        public string DescribeHeadOrder() {
            string frame = TryGetLayer(RoActorSpriteLayer.Body, out RoActorLayerInstance body) && body.Visual != null
                ? "act=" + body.Visual.ActionIndex + " frame=" + body.Visual.FrameIndex
                : "no body";
            return "imf=" + (imfDrawOrder != null ? imfDrawOrder.HeadFirstFrameCount + " head-first frames" : "none") +
                " " + frame + " headFirst=" + ResolveHeadFirst();
        }

        internal static int ClientLayerBias(RoActorSpriteLayer layer, bool facingAway, RoRobeDrawRules.Pass robePass = RoRobeDrawRules.Pass.AfterBody, bool headFirst = false) {
            int shift = facingAway ? 1 : 0;
            // a robe drawn before the body (pass 0) is inserted first: it takes n 0 and the layers inserted before it in
            // pass 1 (the body, facing away also the shield) move up one; a RobeTopLayer robe (key 400) comes after the
            // headgear (draw-order §1.3 / §1.4)
            int robeFirst = robePass == RoRobeDrawRules.Pass.Behind ? 1 : 0;
            // an IMF head-first frame inserts the head where the body goes and the body (with the pass 1 robe right after
            // it) one step later (draw-order §1.3)
            int headStep = headFirst ? 1 : 0;
            return layer switch {
                RoActorSpriteLayer.Shield => facingAway ? robeFirst : 5,
                // the cart is no layer of the character's map but an object at its own ground point (draw-order §3): its depth
                // and key start there (PlaceLayer, CollectClientDrawEntries), so it takes no rank against the body
                RoActorSpriteLayer.Cart or RoActorSpriteLayer.CartShadow => 0,
                RoActorSpriteLayer.Body => shift + robeFirst + headStep,
                RoActorSpriteLayer.Garment => robePass switch {
                    RoRobeDrawRules.Pass.Behind => 0,
                    RoRobeDrawRules.Pass.TopLayer => 9,
                    _ => 1 + shift + headStep,
                },
                RoActorSpriteLayer.Head or RoActorSpriteLayer.Hair => headFirst ? shift + robeFirst : 2 + shift,
                RoActorSpriteLayer.Weapon => 3 + shift,
                RoActorSpriteLayer.WeaponTrail => 4 + shift,
                RoActorSpriteLayer.HeadgearMid => 6,
                RoActorSpriteLayer.HeadgearTop => 7,
                RoActorSpriteLayer.HeadgearLower => 8,
                _ => 0
            };
        }

        /// <summary>The layers' animators, ticked here instead of one tick service entry each.</summary>
        public void ActorUpdateTick(float now) {
            for (int i = 0; i < layers.Count; i++) {
                RoActSpriteAnimator animator = layers[i].Visual != null ? layers[i].Visual.Animator : null;
                // the managed check first: most layers' frames are set from outside (synced to the body or the walk)
                if (animator != null && animator.MayNeedUpdateTick && animator.isActiveAndEnabled && animator.ShouldRunActorTick(now, lateTick: false)) {
                    animator.ActorUpdateTick(now);
                }
            }
        }

        public void ActorLateTick(float now) {
            using (BillboardMarker.Auto()) ApplyLayerBillboards();
            bool needUpdate;
            using (AnchorCheckMarker.Auto()) needUpdate = AnchorsNeedUpdate();
            if (needUpdate) {
                ApplyLayerAnchorOffsets();
            }
        }

        // the parts of a layer placement (a walking crowd re-places its layers at every walk frame)
        private static readonly Unity.Profiling.ProfilerMarker BillboardMarker = new Unity.Profiling.ProfilerMarker("RO.Layers.Billboard");
        private static readonly Unity.Profiling.ProfilerMarker AnchorCheckMarker = new Unity.Profiling.ProfilerMarker("RO.Layers.AnchorCheck");
        private static readonly Unity.Profiling.ProfilerMarker RenderOrderMarker = new Unity.Profiling.ProfilerMarker("RO.Layers.RenderOrder");
        private static readonly Unity.Profiling.ProfilerMarker FrameSyncMarker = new Unity.Profiling.ProfilerMarker("RO.Layers.FrameSync");
        private static readonly Unity.Profiling.ProfilerMarker PlaceMarker = new Unity.Profiling.ProfilerMarker("RO.Layers.Place");
        private static readonly Unity.Profiling.ProfilerMarker ShadowCardMarker = new Unity.Profiling.ProfilerMarker("RO.Layers.ShadowCards");
        private static readonly Unity.Profiling.ProfilerMarker RecordMarker = new Unity.Profiling.ProfilerMarker("RO.Layers.Record");

        private int billboardCameraRevision;
        private Quaternion billboardOwnerRotation = Quaternion.identity;

        /// <summary>
        /// Every layer faces the camera (and maps its action to the camera direction) when the camera turned or a layer
        /// was added — once per actor, instead of each layer checking the camera every frame.
        /// </summary>
        private void ApplyLayerBillboards() {
            UnityEngine.Camera camera = RoActorTickService.FrameCamera;
            Quaternion ownerRotation = transform.rotation;
            if (camera == null || (billboardCameraRevision == RoActorTickService.CameraRevision && ownerRotation == billboardOwnerRotation)) {
                return;
            }

            Quaternion rotation = RoActorTickService.FrameCameraRotation;
            RoActorDirection cameraDirection = RoSpriteActionMap.GetCameraDirectionFromYaw(rotation.eulerAngles.y);
            Transform pivot = EnsureLayerPivot();
            bool facesCamera = UseLayerPivotForDiagnostics && AllLayersFaceCamera();
            if (facesCamera != pivotFacesCamera) {
                pivotFacesCamera = facesCamera;
                anchorsDirty = true;
            }

            if (facesCamera) {
                // the layers sit in the pivot's camera plane: one rotation turns them all, anchors included
                pivot.rotation = rotation;
                for (int i = 0; i < layers.Count; i++) {
                    layers[i].Visual?.ApplyOwnerCameraDirection(cameraDirection);
                }
            } else {
                pivot.localRotation = Quaternion.identity;
                for (int i = 0; i < layers.Count; i++) {
                    layers[i].Visual?.ApplyOwnerBillboard(rotation, cameraDirection);
                }
            }

            // the cart is placed at a world point but sits in the pivot: turning the pivot back after the own character turned
            // its root (placed in between, under the turned pivot) swung the cart to the mirrored side for a frame
            if (hasCameraOffsetLayers) {
                anchorsDirty = true;
            }

            billboardCameraRevision = RoActorTickService.CameraRevision;
            billboardOwnerRotation = ownerRotation;
        }

        /// <summary>Diagnostics (!toggle pivot): false = every layer faces the camera itself, world placement.</summary>
        public static bool UseLayerPivotForDiagnostics = true;

        private Transform EnsureLayerPivot() {
            if (layerPivot == null) {
                var pivotObject = new GameObject("RO Actor Layers");
                layerPivot = pivotObject.transform;
                layerPivot.SetParent(transform, false);
                pivotFacesCamera = false;
            }

            return layerPivot;
        }

        private bool AllLayersFaceCamera() {
            for (int i = 0; i < layers.Count; i++) {
                RoActorSpriteVisual visual = layers[i].Visual;
                if (visual != null && !visual.FacesCamera) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether the layers' placement depends on the camera: world-placed layers (carts, UI previews whose layers do not
        /// face the camera) do; layers in the camera-facing pivot keep fixed local positions.
        /// </summary>
        private bool AnchorsFollowCamera => hasCameraOffsetLayers || !pivotFacesCamera;

        /// <summary>
        /// Places a layer at the world position the anchor rules give: in the camera-facing pivot as a fixed local offset
        /// (pivot right/up = camera right/up, so the same world point), otherwise in world space.
        /// </summary>
        private void PlaceLayer(RoActorLayerInstance layer, Transform layerTransform, Vector3 anchorOffset, Vector3 worldPosition, float pivotScale) {
            if (!pivotFacesCamera || IsCartLayer(layer.Layer) || layerTransform.parent != layerPivot) {
                layerTransform.position = worldPosition;
            } else {
                layerTransform.localPosition = Quaternion.Inverse(layerPivot.localRotation) * layer.BaseLocalOffset +
                    new Vector3(anchorOffset.x, anchorOffset.y, 0f) / pivotScale;
            }

            // the ground point where the client's leaning sprite depth starts (RoSpriteEnhanced): the actor's, or for the cart
            // its own follow position (a separate object of the client, draw-order §3) - at the owner's point the cart shared
            // the body's depth plane, fought it and went under the body while the owner walked back into it
            Vector3 feet = IsCartLayer(layer.Layer) ? worldPosition : transform.position;
            layer.Visual?.SetSpriteFeet(layerTransform.InverseTransformPoint(feet));
        }

        private float ResolvePivotScale() {
            float scale = Mathf.Abs(transform.lossyScale.x);
            return scale > 0.0001f ? scale : 1f;
        }

        public bool ShouldRunActorTick(float now, bool lateTick) {
            if (!lateTick) {
                return true;
            }

            // the camera turned, or the actor itself rotated (the own character turns its root): face the camera again
            // (no frame camera, nothing to face: ApplyLayerBillboards would return at once)
            if (layers.Count > 0 && RoActorTickService.FrameCamera != null &&
                (billboardCameraRevision != RoActorTickService.CameraRevision || transform.rotation != billboardOwnerRotation ||
                pivotFacesCamera != (UseLayerPivotForDiagnostics && AllLayersFaceCamera()))) {
                return true;
            }

            return AnchorsNeedUpdate();
        }

        private bool AnchorsNeedUpdate() {
            if (!applyActAnchors || layers.Count == 0 || (!hasAnchorDrivenLayers && !hasCameraOffsetLayers)) {
                return false;
            }

            if (anchorsDirty) {
                return true;
            }

            if (hasCameraOffsetLayers && !cartFollowInitialized) {
                return true;
            }

            if (!hasAnchorDrivenLayers) {
                Quaternion noAnchorCameraRotation = ResolveCameraRotation();
                return !hasLastAnchorCameraRotation ||
                    (AnchorsFollowCamera && Quaternion.Angle(lastAnchorCameraRotation, noAnchorCameraRotation) > AnchorCameraRotationEpsilonDegrees) ||
                    HasAnchorTransformChanged();
            }

            if (!TryResolveAnchorBodyState(out int bodyActionIndex, out int bodyFrameIndex)) {
                return true;
            }

            // the cheap comparisons first: the camera is read only when the body's frame did not decide it
            return bodyActionIndex != lastAnchorBodyActionIndex ||
                bodyFrameIndex != lastAnchorBodyFrameIndex ||
                !hasLastAnchorCameraRotation ||
                (AnchorsFollowCamera && Quaternion.Angle(lastAnchorCameraRotation, ResolveCameraRotation()) > AnchorCameraRotationEpsilonDegrees) ||
                HasAnchorTransformChanged();
        }

        private bool TryResolveAnchorBodyState(out int bodyActionIndex, out int bodyFrameIndex) {
            if (TryGetLayer(RoActorSpriteLayer.Body, out RoActorLayerInstance bodyLayer) &&
                bodyLayer.Visual != null &&
                bodyLayer.Visual.Animator != null) {
                bodyActionIndex = bodyLayer.Visual.ActionIndex;
                bodyFrameIndex = bodyLayer.Visual.Animator.FrameIndex;
                return true;
            }

            bodyActionIndex = -1;
            bodyFrameIndex = -1;
            return false;
        }

        private void ClearLayers() {
            for (int i = layers.Count - 1; i >= 0; i--) {
                RoActorSpriteVisual visual = layers[i].Visual;
                if (visual != null) {
                    DestroyRuntimeObject(visual.gameObject);
                }
            }

            layers.Clear();
            hasAnchorDrivenLayers = false;
            hasCameraOffsetLayers = false;
            anchorsDirty = true;
            hasLastAnchorCameraRotation = false;
            ClearCartFollowState();
            ClearAnchorTransformState();
            lastAnchorBodyActionIndex = -1;
            lastAnchorBodyFrameIndex = -1;
        }

        private static float HorizontalDistanceSqr(Vector3 a, Vector3 b) {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private void ClearCartFollowState() {
            cartFollowInitialized = false;
            cartFollowWorldPosition = default;
            cartFollowFacingDirection = facingDirection == RoActorDirection.None
                ? RoActorDirection.South
                : facingDirection;
            cartFollowAccumulatedDistanceCells = 0f;
        }

        private void UpdateCartFollowState() {
            if (!hasCameraOffsetLayers) {
                return;
            }

            if (!TryResolveCartOwnerWorldPosition(out Vector3 ownerWorldPosition)) {
                ClearCartFollowState();
                return;
            }

            // a new cart, or an owner placed or moved further than any walk step (the visual is built before the actor stands
            // on its cell): the cart goes behind the owner as the client attaches it (CMsgEffect msg 4 0xd3e620: owner
            // position − 5 units along its rotation), instead of being dragged in from the old point
            if (!cartFollowInitialized || HorizontalDistanceSqr(cartFollowWorldPosition, ownerWorldPosition) > CartSnapDistanceCells * CartSnapDistanceCells) {
                cartFollowWorldPosition = CalculateInitialCartFollowWorldPosition(ownerWorldPosition, facingDirection);
                cartFollowFacingDirection = CalculateCartFacingDirection(
                    cartFollowWorldPosition,
                    ownerWorldPosition,
                    facingDirection == RoActorDirection.None ? RoActorDirection.South : facingDirection);
                cartFollowInitialized = true;
                ApplyCartLayerMotionAndFacing();
                return;
            }

            lastCartFollowFrame = Time.frameCount;
            Vector3 nextCartPosition = CalculateCartFollowWorldPosition(
                ownerWorldPosition,
                cartFollowWorldPosition,
                cartFollowFacingDirection,
                out float movedDistanceCells);
            cartFollowWorldPosition = nextCartPosition;
            cartFollowAccumulatedDistanceCells += movedDistanceCells;
            cartFollowFacingDirection = CalculateCartFacingDirection(
                cartFollowWorldPosition,
                ownerWorldPosition,
                cartFollowFacingDirection);
            ApplyCartLayerMotionAndFacing();
        }

        private bool TryResolveCartOwnerWorldPosition(out Vector3 ownerWorldPosition) {
            if (TryGetLayer(RoActorSpriteLayer.Cart, out RoActorLayerInstance cartLayer)) {
                ownerWorldPosition = transform.TransformPoint(cartLayer.BaseLocalOffset);
                return true;
            }

            if (TryGetLayer(RoActorSpriteLayer.CartShadow, out RoActorLayerInstance cartShadowLayer)) {
                ownerWorldPosition = transform.TransformPoint(cartShadowLayer.BaseLocalOffset);
                return true;
            }

            ownerWorldPosition = default;
            return false;
        }

        private void ApplyCartLayerMotionAndFacing() {
            if (!cartFollowInitialized) {
                return;
            }

            for (int i = 0; i < layers.Count; i++) {
                RoActorLayerInstance layer = layers[i];
                if (!IsCartLayer(layer.Layer) || layer.Visual == null) {
                    continue;
                }

                layer.Visual.SetMotionAndFacing(motion, cartFollowFacingDirection, restart: false);
                ApplyLayerFramePolicy(layer);
                ApplyCartWalkAnimationDistance(layer, null);
            }
        }

        private void ApplyCartWalkAnimationDistance(RoActorLayerInstance layer, RoMovementTuning tuning) {
            if (layer.Layer != RoActorSpriteLayer.Cart ||
                layer.Visual == null ||
                motion != RoSpriteMotion.Walk) {
                return;
            }

            layer.Visual.SetWalkFrameIndexFromDistance(cartFollowAccumulatedDistanceCells, tuning);
        }

        public static Vector3 CalculateInitialCartFollowWorldPosition(Vector3 ownerWorldPosition, RoActorDirection ownerFacingDirection) {
            RoActorDirection facing = ownerFacingDirection == RoActorDirection.None
                ? RoActorDirection.South
                : ownerFacingDirection;
            Vector3 behindOwner = -DirectionToWorldVector(facing);
            behindOwner.y = 0f;
            if (behindOwner.sqrMagnitude <= 0.0001f) {
                behindOwner = Vector3.forward;
            }

            behindOwner.Normalize();
            return ownerWorldPosition + behindOwner * CartFollowDistanceCells;
        }

        public static Vector3 CalculateCartFollowWorldPosition(
            Vector3 ownerWorldPosition,
            Vector3 currentCartWorldPosition,
            RoActorDirection fallbackFacingDirection,
            out float movedDistanceCells
        ) {
            Vector3 ownerToCart = currentCartWorldPosition - ownerWorldPosition;
            ownerToCart.y = 0f;
            float distance = ownerToCart.magnitude;
            Vector3 nextCartWorldPosition = currentCartWorldPosition;

            if (distance <= 0.0001f) {
                nextCartWorldPosition = CalculateInitialCartFollowWorldPosition(ownerWorldPosition, fallbackFacingDirection);
            } else if (distance > CartFollowDistanceCells) {
                nextCartWorldPosition = ownerWorldPosition + ownerToCart / distance * CartFollowDistanceCells;
            }

            nextCartWorldPosition.y = ownerWorldPosition.y;
            Vector3 movement = nextCartWorldPosition - currentCartWorldPosition;
            movement.y = 0f;
            movedDistanceCells = movement.magnitude;
            return nextCartWorldPosition;
        }

        public static RoActorDirection CalculateCartFacingDirection(
            Vector3 cartWorldPosition,
            Vector3 ownerWorldPosition,
            RoActorDirection fallbackFacingDirection
        ) {
            RoActorDirection fallback = fallbackFacingDirection == RoActorDirection.None
                ? RoActorDirection.South
                : fallbackFacingDirection;
            return RoSpriteActionMap.GetDirectionFromWorldDelta(
                ownerWorldPosition - cartWorldPosition,
                fallback,
                CartFacingHysteresisDegrees);
        }

        private void ApplyLayerFramePolicies() {
            for (int i = 0; i < layers.Count; i++) {
                ApplyLayerFramePolicy(layers[i]);
            }
        }

        private void ApplyLayerFramePolicy(RoActorLayerInstance layer) {
            if (layer.Visual == null) {
                return;
            }

            if (layer.Layer == RoActorSpriteLayer.CartShadow ||
                (layer.Layer == RoActorSpriteLayer.Cart && (motion == RoSpriteMotion.Idle || motion == RoSpriteMotion.Sit))) {
                layer.Visual.SetFramePlayback(RoActFramePlaybackMode.FixedFrame, 0);
            } else if (holdCurrentMotionCurrentFrame) {
                int currentFrame = layer.Visual.Animator != null ? layer.Visual.Animator.FrameIndex : 0;
                layer.Visual.SetFramePlayback(RoActFramePlaybackMode.FixedFrame, currentFrame);
            } else if (holdCurrentMotionFirstFrame) {
                layer.Visual.SetFramePlayback(RoActFramePlaybackMode.FixedFrame, 0);
            } else if (hasAnchorDrivenLayers && UsesHeadDirectionIdleFrame(layer.Layer, motion)) {
                layer.Visual.SetFramePlayback(RoActFramePlaybackMode.FixedFrame, headDirectionFrameIndex);
            } else if (hasAnchorDrivenLayers && UsesHeadDirectionAnimatedIdleFrame(layer.Layer, motion)) {
                layer.Visual.SetFramePlayback(RoActFramePlaybackMode.HeadDirectionSegmentAnimated, headDirectionFrameIndex);
            } else if (ShouldSyncFrameToBody(layer.Layer)) {
                layer.Visual.SetFramePlayback(RoActFramePlaybackMode.PrimarySynced);
            } else {
                layer.Visual.SetFramePlayback(RoActFramePlaybackMode.Animated);
            }
        }

        private void ApplyLayerAnchorOffsets() {
            if (!applyActAnchors || layers.Count == 0 || (!hasAnchorDrivenLayers && !hasCameraOffsetLayers)) {
                UpdateSharedActorShadowCards();
                anchorsDirty = false;
                return;
            }

            if (!hasAnchorDrivenLayers) {
                ApplyBaseLayerPositions();
                return;
            }

            if (!TryGetLayer(RoActorSpriteLayer.Body, out RoActorLayerInstance bodyLayer) ||
                bodyLayer.Visual == null ||
                bodyLayer.Visual.Animator == null ||
                !bodyLayer.Visual.TryGetCurrentAnchor(out Vector2Int bodyAnchor)) {
                ApplyBaseLayerPositions();
                return;
            }

            int bodyFrameIndex = bodyLayer.Visual.Animator.FrameIndex;
            int bodyActionIndex = bodyLayer.Visual.ActionIndex;
            UnityEngine.Camera camera = GetCamera();
            Quaternion anchorRotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            Vector3 anchorRight = anchorRotation * Vector3.right;
            Vector3 anchorUp = anchorRotation * Vector3.up;
            float pivotScale = ResolvePivotScale();
            UpdateCartFollowState();

            for (int i = 0; i < layers.Count; i++) {
                RoActorLayerInstance layer = layers[i];
                if (layer.Visual == null) {
                    continue;
                }

                using (RenderOrderMarker.Auto()) ApplyLayerRenderOrder(layer);
                Transform layerTransform = layer.Visual.transform;
                Vector3 baseWorldPosition;
                using (PlaceMarker.Auto()) baseWorldPosition = ResolveLayerWorldPosition(layer, camera, anchorRight, anchorUp);
                if (!UsesBodyAnchor(layer.Layer)) {
                    PlaceLayer(layer, layerTransform, Vector3.zero, baseWorldPosition, pivotScale);
                    layer.Visual.SetVerticalClipOffset(0f);
                    continue;
                }

                if (ShouldSyncFrameToBody(layer.Layer)) {
                    using var frameSyncScope = FrameSyncMarker.Auto();
                    layer.Visual.SetActionAndFrameFromPrimary(bodyActionIndex, bodyFrameIndex);
                    // Diagnostic only (gated, deduped, no behaviour change): catch the rare head/equip
                    // detach. When a body-anchored layer's ACT has fewer actions/frames than the body
                    // for the current motion, SetActionAndFrameFromPrimary wraps (modulo) to a
                    // non-matching action/frame, so its pos[0] anchor no longer tracks the body's bob
                    // and the layer visibly drifts. Logs the culprit layer + ACT indices so the fix
                    // can be made against real numbers instead of guessing.
                    LogAnchorFrameDesyncIfNeeded(layer, bodyActionIndex, bodyFrameIndex);
                }

                if (!layer.Visual.TryGetCurrentAnchor(out Vector2Int layerAnchor)) {
                    PlaceLayer(layer, layerTransform, Vector3.zero, baseWorldPosition, pivotScale);
                    layer.Visual.SetVerticalClipOffset(0f);
                    continue;
                }

                Vector3 anchorOffset = CalculateActAnchorOffset(bodyAnchor, layerAnchor);
                Vector3 anchoredWorldPosition = CalculateAnchoredLayerWorldPosition(baseWorldPosition, anchorRight, anchorUp, anchorOffset);
                using (PlaceMarker.Auto()) PlaceLayer(layer, layerTransform, anchorOffset, anchoredWorldPosition, pivotScale);
                layer.Visual.SetVerticalClipOffset(0f);
            }

            lastAnchorBodyActionIndex = bodyActionIndex;
            lastAnchorBodyFrameIndex = bodyFrameIndex;
            lastAnchorCameraRotation = anchorRotation;
            hasLastAnchorCameraRotation = true;
            using (RecordMarker.Auto()) RecordAnchorTransformState();
            anchorsDirty = false;
            using (ShadowCardMarker.Auto()) UpdateSharedActorShadowCards();
        }

        private void ApplyBaseLayerPositions() {
            UnityEngine.Camera camera = GetCamera();
            Quaternion currentCameraRotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            Vector3 cameraRight = currentCameraRotation * Vector3.right;
            Vector3 cameraUp = currentCameraRotation * Vector3.up;
            float pivotScale = ResolvePivotScale();
            UpdateCartFollowState();

            for (int i = 0; i < layers.Count; i++) {
                RoActorLayerInstance layer = layers[i];
                if (layer.Visual != null) {
                    ApplyLayerRenderOrder(layer);
                    PlaceLayer(layer, layer.Visual.transform, Vector3.zero, ResolveLayerWorldPosition(layer, camera, cameraRight, cameraUp), pivotScale);
                    layer.Visual.SetVerticalClipOffset(0f);
                }
            }

            anchorsDirty = false;
            if (TryResolveAnchorState(out int bodyActionIndex, out int bodyFrameIndex, out Quaternion anchorCameraRotation)) {
                lastAnchorBodyActionIndex = bodyActionIndex;
                lastAnchorBodyFrameIndex = bodyFrameIndex;
                lastAnchorCameraRotation = anchorCameraRotation;
                hasLastAnchorCameraRotation = true;
            } else {
                lastAnchorCameraRotation = currentCameraRotation;
                hasLastAnchorCameraRotation = true;
                lastAnchorBodyActionIndex = -1;
                lastAnchorBodyFrameIndex = -1;
            }

            RecordAnchorTransformState();
            UpdateSharedActorShadowCards();
        }

        private void UpdateSharedActorShadowCards() {
            Vector3 sharedCenterWS = default;
            float sharedBottomLift = 0f;
            bool useSharedCard = shadowRenderMode == RoShadowRenderMode.Enhanced &&
                layers.Count > 1 &&
                TryResolveSharedActorShadowCard(out sharedCenterWS, out sharedBottomLift);

            for (int i = 0; i < layers.Count; i++) {
                RoActorSpriteVisual visual = layers[i].Visual;
                if (visual == null) {
                    continue;
                }

                Vector3 centerOS = useSharedCard
                    ? visual.transform.InverseTransformPoint(sharedCenterWS)
                    : Vector3.zero;
                visual.SetSharedActorShadowCard(useSharedCard, centerOS, sharedBottomLift);
            }
        }

        private bool TryResolveSharedActorShadowCard(out Vector3 centerWS, out float bottomLift) {
            if (TryGetLayer(RoActorSpriteLayer.Body, out RoActorLayerInstance bodyLayer) &&
                bodyLayer.Visual != null) {
                centerWS = bodyLayer.Visual.transform.position;
                bottomLift = bodyLayer.Visual.Animator != null
                    ? bodyLayer.Visual.Animator.RealtimeShadowCasterBottomLift
                    : 0f;
                return true;
            }

            for (int i = 0; i < layers.Count; i++) {
                RoActorLayerInstance layer = layers[i];
                if (layer.Visual != null && ShouldLayerCastRealtimeShadow(layer.Layer)) {
                    centerWS = layer.Visual.transform.position;
                    bottomLift = layer.Visual.Animator != null
                        ? layer.Visual.Animator.RealtimeShadowCasterBottomLift
                        : 0f;
                    return true;
                }
            }

            centerWS = default;
            bottomLift = 0f;
            return false;
        }

        private bool TryResolveAnchorState(out int bodyActionIndex, out int bodyFrameIndex, out Quaternion cameraRotation) {
            if (TryGetLayer(RoActorSpriteLayer.Body, out RoActorLayerInstance bodyLayer) &&
                bodyLayer.Visual != null &&
                bodyLayer.Visual.Animator != null) {
                bodyActionIndex = bodyLayer.Visual.ActionIndex;
                bodyFrameIndex = bodyLayer.Visual.Animator.FrameIndex;
            } else {
                bodyActionIndex = -1;
                bodyFrameIndex = -1;
                cameraRotation = Quaternion.identity;
                return false;
            }

            UnityEngine.Camera camera = GetCamera();
            cameraRotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            return true;
        }

        /// <summary>
        /// The camera rotation for the per-frame "do the anchors need placing" check: the main camera's is read once a frame
        /// by the tick service (the billboards use it too), not by each actor; the placement itself reads the camera.
        /// </summary>
        private Quaternion ResolveCameraRotation() {
            UnityEngine.Camera camera = GetCamera();
            if (camera == null) {
                return Quaternion.identity;
            }

            return ReferenceEquals(camera, RoActorTickService.FrameCamera)
                ? RoActorTickService.FrameCameraRotation
                : camera.transform.rotation;
        }

        private Vector3 ResolveLayerWorldPosition(
            RoActorLayerInstance layer,
            UnityEngine.Camera camera,
            Vector3 cameraRight,
            Vector3 cameraUp
        ) {
            Vector3 baseWorldPosition = transform.TransformPoint(layer.BaseLocalOffset);
            if (IsCartLayer(layer.Layer)) {
                return cartFollowInitialized
                    ? cartFollowWorldPosition
                    : CalculateInitialCartFollowWorldPosition(baseWorldPosition, facingDirection);
            }

            return baseWorldPosition;
        }

        private bool HasAnchorTransformChanged() {
            if (!hasLastAnchorTransformState) {
                return true;
            }

            Transform visualTransform = transform;
            // a move alone leaves the layers where they are relative to the actor (they sit under its pivot); only the
            // cart, placed in the world at its own follow point, needs placing again (every walker re-placed every
            // layer every frame for its move)
            return (HasCartLayer() &&
                    Vector3.SqrMagnitude(visualTransform.position - lastAnchorTransformPosition) > AnchorTransformPositionEpsilonSqr) ||
                Quaternion.Angle(lastAnchorTransformRotation, visualTransform.rotation) > AnchorCameraRotationEpsilonDegrees ||
                Vector3.SqrMagnitude(visualTransform.lossyScale - lastAnchorTransformLossyScale) > AnchorTransformScaleEpsilonSqr;
        }

        private bool HasCartLayer() {
            for (int i = 0; i < layers.Count; i++) {
                if (IsCartLayer(layers[i].Layer)) {
                    return true;
                }
            }

            return false;
        }

        private void RecordAnchorTransformState() {
            Transform visualTransform = transform;
            lastAnchorTransformPosition = visualTransform.position;
            lastAnchorTransformRotation = visualTransform.rotation;
            lastAnchorTransformLossyScale = visualTransform.lossyScale;
            hasLastAnchorTransformState = true;
        }

        private void ClearAnchorTransformState() {
            hasLastAnchorTransformState = false;
            lastAnchorTransformPosition = default;
            lastAnchorTransformRotation = Quaternion.identity;
            lastAnchorTransformLossyScale = Vector3.one;
        }

        // Fires only while the runtime-perf log is enabled and at most once per (layer, body action,
        // body frame) combination, so it never spams or affects timing. A hit means a synced anchored
        // layer ended up on an action/frame that does not match the body it is supposed to ride.
        private void LogAnchorFrameDesyncIfNeeded(RoActorLayerInstance layer, int bodyActionIndex, int bodyFrameIndex) {
            if (!RoRuntimeDiagnostics.RuntimePerfLogEnabled ||
                layer.Visual == null ||
                layer.Visual.Animator == null) {
                return;
            }

            int layerActionIndex = layer.Visual.ActionIndex;
            int layerFrameIndex = layer.Visual.Animator.FrameIndex;
            if (layerActionIndex == bodyActionIndex && layerFrameIndex == bodyFrameIndex) {
                return;
            }

            loggedAnchorDesyncKeys ??= new HashSet<int>();
            int key = ((int) layer.Layer << 24) ^ (bodyActionIndex << 8) ^ (bodyFrameIndex & 0xFF);
            if (!loggedAnchorDesyncKeys.Add(key)) {
                return;
            }

            Debug.LogWarning(
                $"[RO head-anchor desync] layer={layer.Layer} motion={motion} facing={facingDirection} " +
                $"body(action={bodyActionIndex},frame={bodyFrameIndex}) " +
                $"layer(action={layerActionIndex},frame={layerFrameIndex}) — anchored layer wrapped to a " +
                $"non-matching ACT action/frame; pos[0] anchor stops tracking the body and the layer can " +
                $"visually detach.", this);
        }

        private bool TryGetLayer(RoActorSpriteLayer layer, out RoActorLayerInstance result) {
            for (int i = 0; i < layers.Count; i++) {
                if (layers[i].Layer == layer) {
                    result = layers[i];
                    return true;
                }
            }

            result = default;
            return false;
        }

        private static bool UsesBodyAnchor(RoActorSpriteLayer layer) {
            return IsHeadLayer(layer) ||
                layer == RoActorSpriteLayer.WeaponTrail ||
                layer == RoActorSpriteLayer.Shield ||
                layer == RoActorSpriteLayer.Garment;
        }

        private static bool IsCartLayer(RoActorSpriteLayer layer) {
            return layer == RoActorSpriteLayer.Cart ||
                layer == RoActorSpriteLayer.CartShadow;
        }

        private static bool ShouldLayerCastRealtimeShadow(RoActorSpriteLayer layer) {
            return layer != RoActorSpriteLayer.CartShadow;
        }

        private static bool ContainsAnchorDrivenLayer(IReadOnlyList<RoActorSpriteLayerSpec> specs) {
            if (specs == null || specs.Count <= 1) {
                return false;
            }

            bool hasBody = false;
            bool hasAnchoredLayer = false;
            for (int i = 0; i < specs.Count; i++) {
                RoActorSpriteLayerSpec spec = specs[i];
                if (spec == null) {
                    continue;
                }

                if (spec.layer == RoActorSpriteLayer.Body) {
                    hasBody = true;
                } else if (UsesBodyAnchor(spec.layer)) {
                    hasAnchoredLayer = true;
                }
            }

            return hasBody && hasAnchoredLayer;
        }

        private static bool ContainsCameraOffsetLayer(IReadOnlyList<RoActorSpriteLayerSpec> specs) {
            if (specs == null || specs.Count == 0) {
                return false;
            }

            for (int i = 0; i < specs.Count; i++) {
                RoActorSpriteLayerSpec spec = specs[i];
                if (spec != null && IsCartLayer(spec.layer)) {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldSyncFrameToBody(RoActorSpriteLayer layer) {
            return motion != RoSpriteMotion.Idle &&
                motion != RoSpriteMotion.Sit &&
                UsesBodyAnchor(layer);
        }

        public static bool UsesHeadDirectionIdleFrame(RoActorSpriteLayer layer, RoSpriteMotion currentMotion) {
            if (currentMotion != RoSpriteMotion.Idle && currentMotion != RoSpriteMotion.Sit) {
                return false;
            }

            return layer == RoActorSpriteLayer.Body ||
                layer == RoActorSpriteLayer.Head ||
                layer == RoActorSpriteLayer.Hair;
        }

        public static bool UsesHeadDirectionAnimatedIdleFrame(RoActorSpriteLayer layer, RoSpriteMotion currentMotion) {
            if (currentMotion != RoSpriteMotion.Idle && currentMotion != RoSpriteMotion.Sit) {
                return false;
            }

            // the garment too: the player's idle / sit frame is the head direction for every layer (actormotion state 0:
            // frame = +0x434, frozen; the robe draws act(action, frame) with the player's frame). Nearly every robe has 3 idle
            // frames, one per head direction like the body; animating through them swung the robe between the turned-head
            // variants (away from the back) and jumped it at every stop. A robe with 3 × n frames animates in its segment.
            return IsHeadgearLayer(layer) || layer == RoActorSpriteLayer.Garment;
        }

        private static bool IsHeadgearLayer(RoActorSpriteLayer layer) {
            return layer == RoActorSpriteLayer.HeadgearLower ||
                layer == RoActorSpriteLayer.HeadgearMid ||
                layer == RoActorSpriteLayer.HeadgearTop;
        }

        private static bool IsHeadLayer(RoActorSpriteLayer layer) {
            return layer == RoActorSpriteLayer.Head ||
                layer == RoActorSpriteLayer.Hair ||
                IsHeadgearLayer(layer);
        }

        public static Vector3 DirectionToWorldVector(RoActorDirection direction) {
            return direction switch {
                RoActorDirection.South => Vector3.back,
                RoActorDirection.SouthWest => (Vector3.back + Vector3.left).normalized,
                RoActorDirection.West => Vector3.left,
                RoActorDirection.NorthWest => (Vector3.forward + Vector3.left).normalized,
                RoActorDirection.North => Vector3.forward,
                RoActorDirection.NorthEast => (Vector3.forward + Vector3.right).normalized,
                RoActorDirection.East => Vector3.right,
                RoActorDirection.SouthEast => (Vector3.back + Vector3.right).normalized,
                _ => Vector3.back
            };
        }

        private static int DirectionToOctantIndex(RoActorDirection direction) {
            return direction switch {
                RoActorDirection.North => 0,
                RoActorDirection.NorthEast => 1,
                RoActorDirection.East => 2,
                RoActorDirection.SouthEast => 3,
                RoActorDirection.South => 4,
                RoActorDirection.SouthWest => 5,
                RoActorDirection.West => 6,
                RoActorDirection.NorthWest => 7,
                _ => 4
            };
        }

        private static RoActorDirection OctantIndexToDirection(int index) {
            return PositiveModulo(index, 8) switch {
                0 => RoActorDirection.North,
                1 => RoActorDirection.NorthEast,
                2 => RoActorDirection.East,
                3 => RoActorDirection.SouthEast,
                4 => RoActorDirection.South,
                5 => RoActorDirection.SouthWest,
                6 => RoActorDirection.West,
                7 => RoActorDirection.NorthWest,
                _ => RoActorDirection.South
            };
        }

        private static int PositiveModulo(int value, int divisor) {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        /// <summary>The body layer's ACT (the first layer's when there is no body layer), or null before the visual is
        /// built: the combat rules read the attack and hurt actions' delays and frame events from it.</summary>
        public ROIO.Models.FileTypes.ACT BodyAct {
            get {
                for (int i = 0; i < layers.Count; i++) {
                    if (layers[i].Layer == RoActorSpriteLayer.Body) {
                        return layers[i].Visual != null ? layers[i].Visual.Animator?.Act : null;
                    }
                }

                return layers.Count > 0 && layers[0].Visual != null ? layers[0].Visual.Animator?.Act : null;
            }
        }

        private readonly struct RoActorLayerInstance {
            public RoActorLayerInstance(RoActorSpriteLayer layer, RoActorSpriteVisual visual, Vector3 baseLocalOffset) {
                Layer = layer;
                Visual = visual;
                BaseLocalOffset = baseLocalOffset;
            }

            public RoActorSpriteLayer Layer { get; }
            public RoActorSpriteVisual Visual { get; }
            public Vector3 BaseLocalOffset { get; }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(target);
            } else {
                DestroyImmediate(target);
            }
        }
    }
}
