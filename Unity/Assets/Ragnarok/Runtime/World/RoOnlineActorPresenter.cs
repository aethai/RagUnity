using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Audio;
using Ragnarok.Content;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Effects;
using Ragnarok.Input;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.Sprites;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

using Ragnarok.Combat;

namespace Ragnarok.World {
    public readonly struct RoActorPresentationSyncResult {
        public static readonly RoActorPresentationSyncResult Empty = new RoActorPresentationSyncResult(0, 0, 0, 0);

        public RoActorPresentationSyncResult(int created, int rebuilt, int updated, int removed) {
            Created = Math.Max(0, created);
            Rebuilt = Math.Max(0, rebuilt);
            Updated = Math.Max(0, updated);
            Removed = Math.Max(0, removed);
        }

        public int Created { get; }
        public int Rebuilt { get; }
        public int Updated { get; }
        public int Removed { get; }
        public int Processed => Created + Rebuilt + Updated;
        public int Changed => Created + Rebuilt + Removed;

        public static RoActorPresentationSyncResult operator +(
            RoActorPresentationSyncResult left,
            RoActorPresentationSyncResult right
        ) {
            return new RoActorPresentationSyncResult(
                left.Created + right.Created,
                left.Rebuilt + right.Rebuilt,
                left.Updated + right.Updated,
                left.Removed + right.Removed);
        }
    }

    public sealed class RoRentalCompanionVisual : MonoBehaviour {
        private const float TeleportSnapDistanceCells = 16f;

        private RoActorLayeredVisual visual;
        private Transform visualRoot;
        private Transform ownerTransform;
        private string basePath;
        private uint ownerId;
        private Vector3 ownerWorldPosition;
        private RoActorDirection ownerFacing = RoActorDirection.South;
        private float followSpeedMilliseconds = RoMapRentalVisualSemantics.ResolveFalconFollowSpeedMilliseconds(0f);
        private float nextFollowCheckTime;
        private float dashActionUntilTime;
        private bool hasOwnerPosition;
        private bool hasMove;
        private bool isDashMove;
        private Vector3 moveStartWorld;
        private Vector3 moveEndWorld;
        private float moveStartTime;
        private float moveDurationSeconds;

        public RoActorLayeredVisual Visual => visual;
        public bool HasMove => hasMove;
        public uint OwnerId => ownerId;

        public static string BuildFalconObjectName(uint ownerId) {
            return $"RO Online Rental Companion - Falcon - {ownerId}";
        }

        public static Vector3 ResolveFalconWorldPosition(Vector3 ownerWorldPosition) {
            return ownerWorldPosition + Vector3.up * RoMapRentalVisualSemantics.FalconAltitudeOffset;
        }

        public static RoSpriteMotion ResolveFalconPassiveMotion(bool companionIsMoving) {
            return RoSpriteMotion.Idle;
        }

        public static RoSpriteMotion ResolveFalconMotion(bool dashActionActive) {
            return dashActionActive
                ? RoSpriteMotion.Walk
                : RoSpriteMotion.Idle;
        }

        public static int ResolveFalconRenderQueueBase(int ownerRenderQueueBase) {
            return ownerRenderQueueBase + RoMapRentalVisualSemantics.FalconRenderOrderOffset;
        }

        public static int ResolveFalconSortingOrder(int ownerSortingOrder) {
            return ownerSortingOrder + RoMapRentalVisualSemantics.FalconRenderOrderOffset;
        }

        public bool SyncFalcon(
            RoAssetDatabase assets,
            uint ownerId,
            Transform ownerTransform,
            Vector3 ownerWorldPosition,
            string basePath,
            RoActorDirection ownerFacing,
            float ownerSpeedMilliseconds,
            int ownerRenderQueueBase,
            int ownerSortingOrder,
            bool snapToOwner = false
        ) {
            if (assets == null || !assets.IsInitialized || string.IsNullOrWhiteSpace(basePath)) {
                return false;
            }

            this.ownerId = ownerId;
            this.ownerTransform = ownerTransform;
            this.ownerWorldPosition = ownerWorldPosition;
            this.ownerFacing = ownerFacing == RoActorDirection.None ? RoActorDirection.South : ownerFacing;
            followSpeedMilliseconds = RoMapRentalVisualSemantics.ResolveFalconFollowSpeedMilliseconds(ownerSpeedMilliseconds);

            if (!EnsureVisual(assets, basePath)) {
                return false;
            }

            Vector3 currentBaseWorld = ResolveCurrentBaseWorldPosition();
            bool shouldSnap = snapToOwner ||
                !hasOwnerPosition ||
                Vector3.SqrMagnitude(Flatten(currentBaseWorld - ownerWorldPosition)) >= TeleportSnapDistanceCells * TeleportSnapDistanceCells;
            hasOwnerPosition = true;
            if (shouldSnap) {
                SnapToOwner(ownerWorldPosition);
            }

            ApplyRenderState(ownerRenderQueueBase, ownerSortingOrder);
            ApplyMotionState();
            return true;
        }

        public bool DashTo(Vector3 targetWorldPosition) {
            if (visual == null) {
                return false;
            }

            Vector3 start = ResolveCurrentBaseWorldPosition();
            dashActionUntilTime = RoPresentationClock.NowSeconds + RoMapRentalVisualSemantics.FalconDashActionSeconds;
            BeginMove(
                start,
                targetWorldPosition,
                RoMapRentalVisualSemantics.FalconDashSpeedMilliseconds,
                RoPresentationClock.NowSeconds,
                isDash: true);
            return true;
        }

        private void Update() {
            if (ownerTransform != null) {
                ownerWorldPosition = ownerTransform.position;
                hasOwnerPosition = true;
            }

            float now = RoPresentationClock.NowSeconds;
            if (hasMove) {
                AdvanceMove(now);
                return;
            }

            if (hasOwnerPosition && now >= nextFollowCheckTime) {
                TryBeginPassiveFollow(now);
            }

            ApplyMotionState();
        }

        private bool EnsureVisual(RoAssetDatabase assets, string resolvedBasePath) {
            if (visual != null && string.Equals(basePath, resolvedBasePath, StringComparison.Ordinal)) {
                return true;
            }

            DestroyVisual();
            basePath = resolvedBasePath;
            GameObject visualObject = new GameObject("RO Rental Companion Visual");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = Vector3.zero;
            visualRoot = visualObject.transform;
            visual = visualObject.AddComponent<RoActorLayeredVisual>();
            visual.SetShadowRenderMode(RoShadowRenderMode.Original);

            var specs = new[] {
                new RoActorSpriteLayerSpec(RoActorSpriteLayer.Body, resolvedBasePath) {
                    motionProfile = RoSpriteMotionProfile.Monster
                }
            };

            if (!visual.Initialize(assets, specs, RoSpriteMotion.Idle, ownerFacing)) {
                DestroyVisual();
                return false;
            }

            visual.SetLoop(true);
            return true;
        }

        private void ApplyRenderState(int ownerRenderQueueBase, int ownerSortingOrder) {
            visual?.SetRenderQueueBase(ResolveFalconRenderQueueBase(ownerRenderQueueBase));
            visual?.SetSortingOrderBase(ResolveFalconSortingOrder(ownerSortingOrder));
        }

        private void ApplyMotionState() {
            if (visual == null) {
                return;
            }

            RoActorDirection facing = hasMove
                ? RoSpriteActionMap.GetDirectionFromWorldDelta(moveEndWorld - ResolveCurrentBaseWorldPosition(), ownerFacing)
                : ownerFacing;
            RoSpriteMotion motion = ResolveFalconMotion(RoPresentationClock.NowSeconds < dashActionUntilTime);
            visual.SetMotionAndFacing(motion, facing, restart: false);
        }

        private void TryBeginPassiveFollow(float now) {
            Vector3 current = ResolveCurrentBaseWorldPosition();
            Vector3 delta = Flatten(ownerWorldPosition - current);
            if (delta.sqrMagnitude <
                RoMapRentalVisualSemantics.FalconFollowStartDistanceCells *
                RoMapRentalVisualSemantics.FalconFollowStartDistanceCells) {
                return;
            }

            Vector3 target = ResolveFollowTarget(current, ownerWorldPosition);
            if (BeginMove(current, target, followSpeedMilliseconds, now, isDash: false)) {
                nextFollowCheckTime = now + RoMapRentalVisualSemantics.FalconFollowDelaySeconds;
            }
        }

        private bool BeginMove(Vector3 startWorld, Vector3 endWorld, float speedMilliseconds, float now, bool isDash) {
            moveStartWorld = startWorld;
            moveEndWorld = endWorld;
            moveStartTime = now;
            float distance = Vector3.Distance(Flatten(endWorld - startWorld), Vector3.zero);
            float secondsPerCell = Mathf.Max(1f, speedMilliseconds) / 1000f;
            moveDurationSeconds = Mathf.Max(0.01f, distance * secondsPerCell);
            hasMove = distance > 0.001f;
            isDashMove = isDash;
            if (!hasMove) {
                isDashMove = false;
                transform.position = ResolveFalconWorldPosition(endWorld);
                nextFollowCheckTime = isDash
                    ? Math.Max(now, dashActionUntilTime)
                    : now;
            }

            ApplyMotionState();
            return hasMove;
        }

        private void AdvanceMove(float now) {
            float t = moveDurationSeconds > 0f
                ? Mathf.Clamp01((now - moveStartTime) / moveDurationSeconds)
                : 1f;
            Vector3 baseWorld = Vector3.Lerp(moveStartWorld, moveEndWorld, t);
            transform.position = ResolveFalconWorldPosition(baseWorld);
            if (t >= 1f) {
                bool completedDash = isDashMove;
                hasMove = false;
                isDashMove = false;
                nextFollowCheckTime = completedDash
                    ? Math.Max(now, dashActionUntilTime)
                    : now + RoMapRentalVisualSemantics.FalconFollowDelaySeconds;
            }

            ApplyMotionState();
        }

        private void SnapToOwner(Vector3 ownerPosition) {
            hasMove = false;
            isDashMove = false;
            dashActionUntilTime = 0f;
            transform.position = ResolveFalconWorldPosition(ownerPosition);
            nextFollowCheckTime = RoPresentationClock.NowSeconds;
        }

        private Vector3 ResolveCurrentBaseWorldPosition() {
            return transform.position - Vector3.up * RoMapRentalVisualSemantics.FalconAltitudeOffset;
        }

        private static Vector3 ResolveFollowTarget(Vector3 currentWorld, Vector3 ownerWorld) {
            Vector3 delta = Flatten(currentWorld - ownerWorld);
            if (delta.sqrMagnitude <=
                RoMapRentalVisualSemantics.FalconFollowRangeCells *
                RoMapRentalVisualSemantics.FalconFollowRangeCells) {
                return currentWorld;
            }

            return ownerWorld + delta.normalized * RoMapRentalVisualSemantics.FalconFollowRangeCells;
        }

        private static Vector3 Flatten(Vector3 value) {
            value.y = 0f;
            return value;
        }

        private void DestroyVisual() {
            if (visualRoot != null) {
                DestroyRuntimeObject(visualRoot.gameObject);
            }

            visualRoot = null;
            visual = null;
            basePath = null;
        }

        private void OnDestroy() {
            visual = null;
            visualRoot = null;
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
    }

    [DefaultExecutionOrder(10020)]
    public sealed class RoOnlineActorPresenter : MonoBehaviour {
        private const float FallbackCapsuleHeight = 1.4f;
        private const float FallbackStraightStepMilliseconds = RoClickToMoveController.ClassicStraightStepMilliseconds;
        private const float MinimumMovePresentationSeconds = 0.05f;
        private const float CompletedMoveCatchUpSeconds = 0.18f;
        private const float DefaultDeathPresentationSeconds = 1.0f;
        // monsters and NPCs are deleted the moment their death motion ends (vanish type 1, no fade: combat-timing §8)
        private const float DeathFadeSeconds = 0f;
        private const float IdleFullSyncIntervalSeconds = 0.25f;
        private const float IdleFullSyncSafetyIntervalSeconds = 2f;
        private const float RuntimeDependencyRetryIntervalSeconds = 1f;
        private const float WorldControllerLookupRetrySeconds = 0.5f;
        private const float SortingCameraPositionEpsilonSqr = 0.0001f;
        private const float SortingCameraRotationEpsilonDegrees = 0.01f;
        private const float PointerTargetSpriteMarginPixels = 4f;
        public const int DefaultSortingBucketCount = 240;
        private const int ActorSortingTieBreakerRange = 20;

        [SerializeField] private RoOnlineWorldController worldController;
        [SerializeField] private RoRuntimeMapController mapController;
        [SerializeField] private RoGridSelector gridSelector;
        [SerializeField] private RoMovementTuning movementTuning;
        [SerializeField] private bool presentSelf;
        [SerializeField] private bool interpolateMovement = true;
#pragma warning disable CS0414 // Kept for serialized scene compatibility; active network moves no longer idle locally.
        [SerializeField] private bool idleWhenInterpolatedMoveCompletes = true;
#pragma warning restore CS0414
        [SerializeField] private float fallbackStraightStepMilliseconds = FallbackStraightStepMilliseconds;
        [SerializeField] private float diagonalStepMultiplier = RoClickToMoveController.ClassicDiagonalStepMultiplier;
        [SerializeField] private bool createShadows = true;
        [SerializeField] private bool createFallbackCapsules = true;
        [SerializeField] private bool createPointerTargets = true;
        [SerializeField] private bool reusePresentedActorObjects = true;
        [SerializeField] private int maxPooledPresentedActors = RoOnlineWorldBudget.DefaultPooledPresentedActors;
        [SerializeField] private int renderQueueBase = (int) RenderQueue.Transparent;
        [SerializeField] private bool useDynamicSortingOrder = true;
        [SerializeField] private int sortingOrderBase;
        [SerializeField] private int sortingBucketCount = DefaultSortingBucketCount;
        [SerializeField] private UnityEngine.Camera sortingCamera;
        [SerializeField] private bool syncPresentationRevisionImmediately = true;

        private readonly Dictionary<uint, PresentedActor> actors = new Dictionary<uint, PresentedActor>();

        /// <summary>The status render state of an actor (the world controller's status presenter keeps it).</summary>
        public Func<uint, Ragnarok.Combat.RoActorStatusLook> StatusLookProvider { get; set; }
        private readonly Stack<PresentedActor> pooledActors = new Stack<PresentedActor>();
        private readonly List<RoOnlineActorSnapshot> snapshotBuffer = new List<RoOnlineActorSnapshot>(RoOnlineWorldBudget.DefaultSnapshotActorCapacity);
        private readonly HashSet<uint> seenServerIds = new HashSet<uint>();
        private readonly HashSet<uint> incrementalSeenServerIds = new HashSet<uint>();
        private readonly List<uint> removeServerIds = new List<uint>(64);
        private readonly List<uint> activePresentationSyncIds = new List<uint>(64);
        private readonly List<Vector2Int> movementCellPathBuffer = new List<Vector2Int>(64);
        private readonly Dictionary<string, string> actorResourceNameCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly StringBuilder actorResourceNameBuilder = new StringBuilder(64);
        private RoAssetDatabase assets;
        private Transform actorRoot;
        private Transform rentalCompanionRoot;
        private Transform pooledActorRoot;
        private Material fallbackMaterial;
        private Material itemFallbackMaterial;
        private UnityEngine.Camera cachedSortingCamera;
        private int cachedSortingCameraFrame = -1;
        private float nextIdleFullSyncTime;
        private bool hasSortingCameraTransformState;
        private Vector3 lastSortingCameraPosition;
        private Quaternion lastSortingCameraRotation;
        private int sortingCameraRevision;
        private int lastSyncFrame = -1;
        private RoActorPresentationSyncResult lastSyncResult = RoActorPresentationSyncResult.Empty;
        private float nextRuntimeDependencyRetryTime;
        private float nextWorldControllerLookupTime;
        private float nextIdleSafetyFullSyncTime;
        private int lastIdleFullSyncPresentationRevision = int.MinValue;
        private int lastIdleFullSyncActorCount = int.MinValue;

        // Spread expensive brand-new actor visual builds across frames so a burst of spawns (map entry,
        // walking into a crowded area) cannot build many actors in a single frame and cause a multi-ms
        // hitch. Only brand-new creates are budgeted, and only during the live per-frame loop on a
        // non-forced sync: the local player is never deferred, existing actors always update their
        // position/animation the same frame, and forced/edit-mode syncs (tests, map setup) still build
        // everything synchronously. Deferred actors are built over the next frames and simply appear a
        // frame or two later (they fade in anyway).
        private const int MaxNewActorBuildsPerFrame = 2;
        private int newActorBuildBudget;
        private bool hasDeferredActorBuilds;

        /// <summary>New actors over the per-frame build budget are still waiting to be built (load tests settle on it).</summary>
        public bool HasDeferredActorBuilds => hasDeferredActorBuilds;

        private static readonly ProfilerMarker SyncNowMarker = new ProfilerMarker("ActorPresenter.SyncNow");
        private static readonly ProfilerMarker ApplyDirtyMarker = new ProfilerMarker("RO.Actor.Presentation.ApplyDirty");
        // SyncActor's steps (the cpuprobe breakdown of ApplyDirty)
        private static readonly ProfilerMarker SyncPositionMarker = new ProfilerMarker("RO.Actor.Sync.Position");
        private static readonly ProfilerMarker SyncVisualMarker = new ProfilerMarker("RO.Actor.Sync.Visual");
        private static readonly ProfilerMarker SyncVisualWalkMarker = new ProfilerMarker("RO.Actor.Sync.Visual.Walk");
        private static readonly ProfilerMarker SyncVisualHeadMarker = new ProfilerMarker("RO.Actor.Sync.Visual.Head");
        private static readonly ProfilerMarker SyncRenderMarker = new ProfilerMarker("RO.Actor.Sync.Render");
        private static readonly ProfilerMarker SyncAttachMarker = new ProfilerMarker("RO.Actor.Sync.Attach");
        private static readonly ProfilerMarker SyncShadowPointerMarker = new ProfilerMarker("RO.Actor.Sync.ShadowPointer");
        private static readonly ProfilerMarker SyncStatusMarker = new ProfilerMarker("RO.Actor.Sync.Status");
        private static readonly ProfilerMarker SyncBarsMarker = new ProfilerMarker("RO.Actor.Sync.Bars");
        private static readonly ProfilerMarker SortingUpdateMarker = new ProfilerMarker("RO.Actor.Sorting.Update");
        private static readonly ProfilerMarker ResourceBarUpdateMarker = new ProfilerMarker("RO.Actor.ResourceBar.Update");

        private enum ActorVisualBuildStatus {
            Ready,
            Failed
        }

        public int PresentedActorCount => actors.Count;
        public int PooledPresentedActorCount => pooledActors.Count;
        public int MaxPooledPresentedActorCount => Mathf.Max(0, maxPooledPresentedActors);
        public int SnapshotBufferCapacity => snapshotBuffer.Capacity;
        public static float DeathFadeDurationSeconds => DeathFadeSeconds;
        private RoMovementTuning MovementTuning => RoMovementTuning.Resolve(movementTuning);

        public bool PresentSelf {
            get => presentSelf;
            set {
                if (presentSelf == value) {
                    return;
                }

                presentSelf = value;
                SyncNow(force: true);
            }
        }

        public void SetRuntimeMapController(RoRuntimeMapController controller) {
            if (ReferenceEquals(mapController, controller)) {
                return;
            }

            mapController = controller;
            SyncNow(force: true);
        }

        public bool ReusePresentedActorObjects {
            get => reusePresentedActorObjects;
            set {
                reusePresentedActorObjects = value;
                if (!reusePresentedActorObjects) {
                    ClearPooledPresentedActors();
                }
            }
        }

        public void Initialize(
            RoOnlineWorldController controller,
            RoAssetDatabase assetDatabase,
            RoGridSelector selector = null
        ) {
            worldController = controller;
            nextWorldControllerLookupTime = 0f;
            assets = assetDatabase;
            gridSelector = selector;
            SyncNow(force: true);
        }

        public void SetWorldController(RoOnlineWorldController controller) {
            if (worldController == controller) {
                return;
            }

            worldController = controller;
            nextWorldControllerLookupTime = 0f;
            SyncNow(force: true);
        }

        public void SetAssets(RoAssetDatabase assetDatabase) {
            if (ReferenceEquals(assets, assetDatabase)) {
                return;
            }

            ClearPooledPresentedActors();
            MarkActiveActorVisualsDirty();
            assets = assetDatabase;
            SyncNow(force: true);
        }

        public void SetGridSelector(RoGridSelector selector) {
            if (gridSelector == selector) {
                return;
            }

            gridSelector = selector;
            SyncNow(force: true);
        }

        /// <summary>The actor's status render state changed: applied to its visual right away.</summary>
        public void RefreshStatusLook(uint serverId) {
            RoOnlineWorldState state = ResolveWorldController()?.State;
            if (actors.TryGetValue(serverId, out PresentedActor actor) && actor?.Root != null &&
                state != null && state.TryGetActor(serverId, out RoOnlineActorSnapshot snapshot)) {
                ApplyActorStatusVisualState(actor, snapshot);
                ApplyActorShadowRenderMode(actor, snapshot, ResolveStatusLook(serverId).Shadow);
                RefreshPointerTarget(actor, snapshot);
            }
        }

        private Ragnarok.Combat.RoActorStatusLook ResolveStatusLook(uint serverId) =>
            StatusLookProvider != null ? StatusLookProvider(serverId) : Ragnarok.Combat.RoActorStatusLook.Default;

        public void ClearPresentedActors() {
            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            foreach (KeyValuePair<uint, PresentedActor> pair in actors) {
                bridge?.ForgetActor(pair.Key, pair.Value.Root != null ? pair.Value.Root.transform : null);
                ReleasePresentedActor(pair.Value);
            }

            actors.Clear();
            lastSyncFrame = -1;
            lastSyncResult = RoActorPresentationSyncResult.Empty;
        }

        public bool TryGetPresentedActorTransform(uint serverId, out Transform actorTransform) {
            if (actors.TryGetValue(serverId, out PresentedActor actor) && actor?.Root != null && actor.Root.activeInHierarchy) {
                actorTransform = actor.Root.transform;
                return true;
            }

            actorTransform = null;
            return false;
        }

        /// <summary>The body ACT of a presented actor (null when it is not presented or its sprite is not loaded).</summary>
        public ROIO.Models.FileTypes.ACT GetActorBodyAct(uint serverId) =>
            actors.TryGetValue(serverId, out PresentedActor actor) && actor?.Visual != null ? actor.Visual.BodyAct : null;

        public void TryHideCastBar(uint serverId) {
            if (!actors.TryGetValue(serverId, out PresentedActor actor) || actor?.Root == null) {
                return;
            }

            if (actor.CastBar != null) {
                actor.CastBar.HideCast();
            }
        }

        public void TryClearCastOverlay(uint serverId) {
            if (!actors.TryGetValue(serverId, out PresentedActor actor) || actor?.Root == null) {
                return;
            }

            ClearActorCastOverlay(actor);
        }

        public bool PlayDeathThenRelease(RoOnlineActorSnapshot snapshot) {
            if (!ShouldPresentActor(snapshot, presentSelf)) {
                return false;
            }

            TryResolveRuntimeDependencies();
            float nowSeconds = RoPresentationClock.NowSeconds;
            RoOnlineActorPresentationSignature signature = RoOnlineActorPresentationSignature.From(snapshot);
            if (!actors.TryGetValue(snapshot.ServerId, out PresentedActor actor) || actor.Root == null) {
                actor = CreateActor(snapshot, signature);
                actors[snapshot.ServerId] = actor;
            } else if (!actor.HasBuiltPresentation || !actor.Signature.Equals(signature) || ShouldRetryAssetVisual(actor)) {
                RebuildActorVisual(actor, snapshot, signature);
            }

            if (!actor.HasBuiltPresentation) {
                ApplyActorObjectName(actor, snapshot);
                Vector3 deferredWorldPosition = actor.HasPosition
                    ? actor.Root.transform.position
                    : ResolveActorWorldPosition(snapshot, actor, nowSeconds);
                SetActorRootWorldPositionIfChanged(actor, deferredWorldPosition);
                return false;
            }

            ApplyActorObjectName(actor, snapshot);
            Vector3 worldPosition = actor.HasPosition ? actor.Root.transform.position : ResolveActorWorldPosition(snapshot, actor, nowSeconds);
            SetActorRootWorldPositionIfChanged(actor, worldPosition);
            ClearActorResourceBars(actor);
            ClearActorCastOverlay(actor);
            SetPointerTargetActive(actor, false);
            SetActorShadowActive(actor, false);

            RoSpriteMotion deathMotion = RoSpriteMotion.Dead;
            RoActorDirection facing = ResolveFacingDirection(snapshot);
            ApplyActorVisualState(actor, snapshot, deathMotion, facing, actionRevisionChanged: true, loop: false, headDirectionFrameIndex: 0);
            ApplyActorRenderState(actor, snapshot, worldPosition);

            actor.PendingDeathRelease = true;
            float deathAnimationSeconds = ResolveDeathAnimationSeconds(actor);
            actor.DeathFadeStartLocalTime = nowSeconds + deathAnimationSeconds;
            actor.DeathReleaseLocalTime = actor.DeathFadeStartLocalTime + DeathFadeSeconds;
            return true;
        }

        public RoActorPresentationSyncResult SyncNow(bool force = false) {
            if (force) {
                RoRuntimePerfCounters.IncrementForcedPresenterSync();
            }

            int frame = Time.frameCount;
            if (!force && lastSyncFrame == frame && !HasPendingPresentationRevisionSync()) {
                return lastSyncResult;
            }

            using (SyncNowMarker.Auto()) {
                RoActorPresentationSyncResult result = SyncNowInternal(Application.isPlaying && !force);
                RoRuntimePerfCounters.AddActorPresenterSync(result.Processed, result.Changed);
                lastSyncFrame = frame;
                lastSyncResult = result;
                return result;
            }
        }

        public RoActorPresentationSyncResult SyncActorIds(IReadOnlyList<uint> actorIds, bool force = false) {
            if (actorIds == null || actorIds.Count == 0) {
                return RoActorPresentationSyncResult.Empty;
            }

            if (force) {
                RoRuntimePerfCounters.IncrementForcedPresenterSync();
            }

            ResolveWorldController();

            if (worldController == null) {
                return SyncNow(force);
            }

            TryResolveRuntimeDependencies();
            RoOnlineWorldState state = worldController.State;
            incrementalSeenServerIds.Clear();
            int created = 0;
            int rebuilt = 0;
            int updated = 0;
            int removed = 0;
            float nowSeconds = RoPresentationClock.NowSeconds;
            BeginActorBuildBudget(Application.isPlaying && !force, fullSync: false);

            for (int i = 0; i < actorIds.Count; i++) {
                uint actorId = actorIds[i];
                if (actorId == 0) {
                    continue;
                }

                bool hasSnapshot = state.TryGetActor(actorId, out RoOnlineActorSnapshot snapshot) ||
                    state.TryGetActorByAccountId(actorId, out snapshot);
                uint serverId = hasSnapshot ? snapshot.ServerId : actorId;
                if (serverId == 0 || !incrementalSeenServerIds.Add(serverId)) {
                    continue;
                }

                if (hasSnapshot && ShouldPresentActor(snapshot, presentSelf)) {
                    RoOnlineActorPresentationSignature signature = RoOnlineActorPresentationSignature.From(snapshot);
                    if (!force &&
                        actors.TryGetValue(serverId, out PresentedActor existingActor) &&
                        CanSkipIncrementalSync(existingActor, snapshot, signature)) {
                        continue;
                    }

                    if (ShouldWaitForSpritePrefetch(snapshot)) {
                        hasDeferredActorBuilds = true;
                        continue;
                    }

                    if (ShouldDeferNewActorBuild(snapshot)) {
                        hasDeferredActorBuilds = true;
                        RequestSpritePrefetch(snapshot);
                        continue;
                    }

                    RoActorPresentationSyncResult actorResult;
                    using (ApplyDirtyMarker.Auto()) {
                        actorResult = SyncActor(snapshot);
                    }

                    if (actorResult.Created > 0) {
                        newActorBuildBudget--;
                        spritePrefetchRequested.Remove(serverId);
                    }

                    created += actorResult.Created;
                    rebuilt += actorResult.Rebuilt;
                    updated += actorResult.Updated;
                } else {
                    removed += FadeOrReleasePresentedActor(serverId, nowSeconds);
                }
            }

            RecordSortingCameraState();
            RecordIdleFullSyncState();

            RoActorPresentationSyncResult result = new RoActorPresentationSyncResult(created, rebuilt, updated, removed);
            RoRuntimePerfCounters.AddActorPresenterSync(result.Processed, result.Changed);
            return result;
        }

        private RoActorPresentationSyncResult SyncNowInternal(bool allowBuildBudget = false) {
            ResolveWorldController();

            if (worldController == null) {
                int removed = actors.Count;
                ClearPresentedActors();
                return new RoActorPresentationSyncResult(0, 0, 0, removed);
            }

            TryResolveRuntimeDependencies();
            worldController.SnapshotActors(snapshotBuffer);
            seenServerIds.Clear();
            BeginActorBuildBudget(allowBuildBudget, fullSync: true);
            int created = 0;
            int rebuilt = 0;
            int updated = 0;
            for (int i = 0; i < snapshotBuffer.Count; i++) {
                RoOnlineActorSnapshot snapshot = snapshotBuffer[i];
                if (!ShouldPresentActor(snapshot, presentSelf)) {
                    continue;
                }

                seenServerIds.Add(snapshot.ServerId);
                if (ShouldWaitForSpritePrefetch(snapshot)) {
                    hasDeferredActorBuilds = true;
                    continue;
                }

                if (ShouldDeferNewActorBuild(snapshot)) {
                    hasDeferredActorBuilds = true;
                    RequestSpritePrefetch(snapshot);
                    continue;
                }

                RoActorPresentationSyncResult actorResult;
                using (ApplyDirtyMarker.Auto()) {
                    actorResult = SyncActor(snapshot);
                }

                if (actorResult.Created > 0) {
                    newActorBuildBudget--;
                    spritePrefetchRequested.Remove(snapshot.ServerId);
                }

                created += actorResult.Created;
                rebuilt += actorResult.Rebuilt;
                updated += actorResult.Updated;
            }

            int removedActors = RemoveUnseenActors(seenServerIds);
            RecordSortingCameraState();
            RecordIdleFullSyncState();
            return new RoActorPresentationSyncResult(created, rebuilt, updated, removedActors);
        }

        private RoOnlineWorldController ResolveWorldController() {
            if (worldController != null) {
                return worldController;
            }

            worldController = GetComponentInParent<RoOnlineWorldController>();
            if (worldController != null) {
                nextWorldControllerLookupTime = 0f;
                return worldController;
            }

            float now = Time.unscaledTime;
            if (now < nextWorldControllerLookupTime) {
                return null;
            }

            worldController = FindFirstObjectByType<RoOnlineWorldController>();
            nextWorldControllerLookupTime = worldController != null
                ? 0f
                : now + WorldControllerLookupRetrySeconds;
            return worldController;
        }

        private void LateUpdate() {
            UpdateGrannyEmblems();
            if (RequiresFullFrameSync()) {
                SyncNow();
                return;
            }

            // with the client's single draw order every layer's order is assigned each frame (RoClientDrawOrder): the
            // screen-y order would be overwritten, so a camera move does not re-sort the crowd (153 actors a frame in !woe)
            if (!RoClientDrawOrder.Enabled && HasSortingCameraChanged()) {
                UpdateSortingForCamera();
            }

            if (TryCollectActivePresentationActorIds(activePresentationSyncIds)) {
                SyncActorIds(activePresentationSyncIds);
                return;
            }

            if (RoPresentationClock.NowSeconds >= nextIdleFullSyncTime && ShouldRunIdleFullSync()) {
                SyncNow();
            } else if (RoPresentationClock.NowSeconds >= nextIdleFullSyncTime) {
                ScheduleNextIdleFullSyncCheck();
            }
        }

        private bool ShouldRunIdleFullSync() {
            if (worldController == null || RoPresentationClock.NowSeconds >= nextIdleSafetyFullSyncTime) {
                return true;
            }

            return worldController.PresentationRevision != lastIdleFullSyncPresentationRevision ||
                worldController.ActorCount != lastIdleFullSyncActorCount;
        }

        private void RecordIdleFullSyncState() {
            ScheduleNextIdleFullSyncCheck();
            nextIdleSafetyFullSyncTime = RoPresentationClock.NowSeconds + IdleFullSyncSafetyIntervalSeconds;
            if (worldController == null) {
                lastIdleFullSyncPresentationRevision = int.MinValue;
                lastIdleFullSyncActorCount = int.MinValue;
                return;
            }

            lastIdleFullSyncPresentationRevision = worldController.PresentationRevision;
            lastIdleFullSyncActorCount = worldController.ActorCount;
        }

        private void ScheduleNextIdleFullSyncCheck() {
            nextIdleFullSyncTime = RoPresentationClock.NowSeconds + IdleFullSyncIntervalSeconds;
        }

        private void OnDestroy() {
            DestroyActivePresentedActors();
            ClearPooledPresentedActors();
            if (rentalCompanionRoot != null) {
                DestroyRuntimeObject(rentalCompanionRoot.gameObject);
                rentalCompanionRoot = null;
            }

            DestroyRuntimeObject(fallbackMaterial);
            DestroyRuntimeObject(itemFallbackMaterial);
            fallbackMaterial = null;
            itemFallbackMaterial = null;
        }

        private void TryResolveRuntimeDependencies() {
            float now = Time.unscaledTime;
            bool canRetrySearch = now >= nextRuntimeDependencyRetryTime;
            bool searched = false;

            if (mapController == null && canRetrySearch) {
                searched = true;
                mapController = FindFirstObjectByType<RoRuntimeMapController>();
            }

            if ((assets == null || !assets.IsInitialized) &&
                mapController != null &&
                mapController.Content != null &&
                mapController.Content.IsInitialized) {
                assets = mapController.Content.Assets;
            }

#if UNITY_EDITOR
            RoMapSmokeTest smokeTest = null;
            if ((assets == null || !assets.IsInitialized) && canRetrySearch) {
                searched = true;
                smokeTest = FindFirstObjectByType<RoMapSmokeTest>();
                if (smokeTest != null &&
                    smokeTest.Content != null &&
                    smokeTest.Content.IsInitialized) {
                    assets = smokeTest.Content.Assets;
                }
            }
#endif

            if (gridSelector == null) {
                gridSelector = worldController != null ? worldController.GridSelector : null;
            }

            if (gridSelector == null && mapController != null) {
                gridSelector = mapController.GridSelector;
            }

#if UNITY_EDITOR
            if (gridSelector == null && canRetrySearch) {
                searched = true;
                smokeTest ??= FindFirstObjectByType<RoMapSmokeTest>();
                if (smokeTest != null) {
                    gridSelector = smokeTest.GridSelector;
                }
            }
#endif

            if (gridSelector == null && canRetrySearch) {
                searched = true;
                gridSelector = FindFirstObjectByType<RoGridSelector>();
            }

            if (searched && (mapController == null || gridSelector == null || assets == null || !assets.IsInitialized)) {
                nextRuntimeDependencyRetryTime = now + RuntimeDependencyRetryIntervalSeconds;
            }
        }

        private RoActorPresentationSyncResult SyncActor(RoOnlineActorSnapshot snapshot) {
            float nowSeconds = RoPresentationClock.NowSeconds;
            RoOnlineActorPresentationSignature signature = RoOnlineActorPresentationSignature.From(snapshot);
            bool created = false;
            bool rebuilt = false;
            if (!actors.TryGetValue(snapshot.ServerId, out PresentedActor actor) || actor.Root == null) {
                actor = CreateActor(snapshot, signature);
                actors[snapshot.ServerId] = actor;
                created = true;
            } else if (!actor.HasBuiltPresentation || !actor.Signature.Equals(signature) || ShouldRetryAssetVisual(actor)) {
                RebuildActorVisual(actor, snapshot, signature);
                rebuilt = true;
            }

            if (!actor.HasBuiltPresentation) {
                ApplyActorObjectName(actor, snapshot);
                actor.PendingDeathRelease = false;
                actor.DeathReleaseLocalTime = 0f;
                Vector3 deferredWorldPosition = ResolveActorWorldPosition(snapshot, actor, nowSeconds);
                SetActorRootWorldPositionIfChanged(actor, deferredWorldPosition);
                return new RoActorPresentationSyncResult(
                    created ? 1 : 0,
                    rebuilt ? 1 : 0,
                    0,
                    0);
            }

            bool actionRevisionChanged = actor.ActionRevision != snapshot.ActionRevision;
            if (!created && !rebuilt && !actionRevisionChanged && CanUseMoveFastPath(actor, snapshot)) {
                Vector3 fastWorldPosition;
                RoSpriteMotion fastMotion;
                RoActorDirection fastFacing;
                using (SyncPositionMarker.Auto()) {
                    fastWorldPosition = ResolveActorWorldPosition(snapshot, actor, nowSeconds);
                    fastMotion = ResolveMotion(snapshot, actor, nowSeconds);
                    fastFacing = ResolveFacingDirection(snapshot, actor, nowSeconds);
                    UpdatePresentedWalkDistance(actor, fastMotion, fastWorldPosition);
                    SetActorRootWorldPositionIfChanged(actor, fastWorldPosition);
                }

                using (SyncVisualMarker.Auto()) {
                    ApplyActorVisualState(actor, snapshot, fastMotion, fastFacing, actionRevisionChanged: false);
                }

                using (SyncRenderMarker.Auto()) {
                    ApplyActorRenderState(actor, snapshot, fastWorldPosition);
                }

                using (SyncAttachMarker.Auto()) {
                    SyncRentalAttachments(actor, snapshot, fastMotion, fastFacing);
                }

                using (SyncShadowPointerMarker.Auto()) {
                    ApplyActorShadowRenderMode(actor, snapshot, ResolveStatusLook(snapshot.ServerId).Shadow);
                    RefreshPointerTarget(actor, snapshot);
                }

                using (SyncStatusMarker.Auto()) {
                    ApplyActorStatusVisualState(actor, snapshot);
                }

                using (SyncBarsMarker.Auto()) {
                    RefreshActorResourceBarsIfDirty(actor, snapshot);
                    RecordIncrementalSnapshotState(actor, snapshot);
                }

                return new RoActorPresentationSyncResult(0, 0, 1, 0);
            }

            ApplyActorObjectName(actor, snapshot);
            actor.PendingDeathRelease = false;
            actor.DeathReleaseLocalTime = 0f;
            ApplyActorShadowRenderMode(actor, snapshot, ResolveStatusLook(snapshot.ServerId).Shadow);
            RefreshPointerTarget(actor, snapshot);
            // When two attack packets arrive very close together (network burst or ASPD edge),
            // the second restart can overwrite the first before any frames render.  Require at
            // least 80 ms between animation restarts so the swing always has time to play.
            bool restartVisual = actionRevisionChanged;
            if (actionRevisionChanged) {
                actor.ActionRevision = snapshot.ActionRevision;
                actor.ActionStartLocalTime = nowSeconds;
            }

            Vector3 worldPosition = ResolveActorWorldPosition(snapshot, actor, nowSeconds);
            RoSpriteMotion motion = ResolveMotion(snapshot, actor, nowSeconds);
            RoActorDirection facing = ResolveFacingDirection(snapshot, actor, nowSeconds);
            UpdatePresentedWalkDistance(actor, motion, worldPosition);
            SetActorRootWorldPositionIfChanged(actor, worldPosition);
            ApplyActorVisualState(actor, snapshot, motion, facing, restartVisual);
            ApplyActorRenderState(actor, snapshot, worldPosition);
            SyncRentalAttachments(actor, snapshot, motion, facing);
            if (created || rebuilt || actionRevisionChanged || !actor.PointerTargetShapeConfigured) {
                RefreshPointerTargetShape(actor, snapshot);
            }

            ApplyActorStatusVisualState(actor, snapshot);
            RefreshActorResourceBarsIfDirty(actor, snapshot, force: created || rebuilt);
            RecordIncrementalSnapshotState(actor, snapshot);
            return new RoActorPresentationSyncResult(
                created ? 1 : 0,
                rebuilt ? 1 : 0,
                created || rebuilt ? 0 : 1,
                0);
        }

        private static bool CanSkipIncrementalSync(
            PresentedActor actor,
            RoOnlineActorSnapshot snapshot,
            RoOnlineActorPresentationSignature signature
        ) {
            if (actor == null ||
                !actor.HasBuiltPresentation ||
                !actor.HasIncrementalSnapshotState ||
                actor.PendingDeathRelease ||
                actor.HasMove ||
                IsFinitePresentationMotion(actor.LastMotion) ||
                snapshot.HasMovePath ||
                snapshot.Motion == RoOnlineActorMotion.Moving ||
                !actor.Signature.Equals(signature)) {
                return false;
            }

            return actor.LastSnapshotCellX == snapshot.CellX &&
                actor.LastSnapshotCellY == snapshot.CellY &&
                actor.LastSnapshotDirection == snapshot.Direction &&
                actor.LastSnapshotHeadDirection == snapshot.HeadDirection &&
                actor.LastSnapshotMotion == snapshot.Motion &&
                actor.LastSnapshotSpawnState == snapshot.SpawnState &&
                actor.LastSnapshotActionRevision == snapshot.ActionRevision &&
                actor.LastSnapshotCompletesActionToStandby == snapshot.CompletesActionToStandby &&
                actor.LastSnapshotEffectState == snapshot.EffectState &&
                actor.LastSnapshotHp == snapshot.Hp &&
                actor.LastSnapshotMaxHp == snapshot.MaxHp &&
                actor.LastSnapshotBaseLevel == snapshot.BaseLevel;
        }

        private static void RecordIncrementalSnapshotState(PresentedActor actor, RoOnlineActorSnapshot snapshot) {
            if (actor == null) {
                return;
            }

            actor.HasIncrementalSnapshotState = true;
            actor.LastSnapshotCellX = snapshot.CellX;
            actor.LastSnapshotCellY = snapshot.CellY;
            actor.LastSnapshotDirection = snapshot.Direction;
            actor.LastSnapshotHeadDirection = snapshot.HeadDirection;
            actor.LastSnapshotMotion = snapshot.Motion;
            actor.LastSnapshotSpawnState = snapshot.SpawnState;
            actor.LastSnapshotActionRevision = snapshot.ActionRevision;
            actor.LastSnapshotCompletesActionToStandby = snapshot.CompletesActionToStandby;
            actor.LastSnapshotEffectState = snapshot.EffectState;
            actor.LastSnapshotHp = snapshot.Hp;
            actor.LastSnapshotMaxHp = snapshot.MaxHp;
            actor.LastSnapshotBaseLevel = snapshot.BaseLevel;
        }

        private static void SetActorRootWorldPositionIfChanged(PresentedActor actor, Vector3 worldPosition) {
            if (actor?.Root == null) {
                return;
            }

            Transform rootTransform = actor.Root.transform;
            if (rootTransform.position != worldPosition) {
                rootTransform.position = worldPosition;
            }
        }

        private static void UpdatePresentedWalkDistance(PresentedActor actor, RoSpriteMotion motion, Vector3 worldPosition) {
            if (actor == null) {
                return;
            }

            if (motion != RoSpriteMotion.Walk) {
                actor.ClearWalkDistance();
                return;
            }

            if (!actor.HasWalkDistancePosition) {
                actor.LastWalkDistanceWorldPosition = worldPosition;
                actor.HasWalkDistancePosition = true;
                return;
            }

            actor.WalkDistanceCells = AccumulatePresentedWalkDistance(
                actor.WalkDistanceCells,
                actor.LastWalkDistanceWorldPosition,
                worldPosition);
            actor.LastWalkDistanceWorldPosition = worldPosition;
        }

        private static bool CanUseMoveFastPath(PresentedActor actor, RoOnlineActorSnapshot snapshot) {
            return actor != null &&
                actor.HasBuiltPresentation &&
                actor.HasMove &&
                actor.HasVisualState &&
                actor.HasRenderState &&
                actor.PointerTargetShapeConfigured &&
                !actor.PendingDeathRelease &&
                snapshot.Motion == RoOnlineActorMotion.Moving &&
                snapshot.HasMovePath;
        }

        private static void ApplyActorObjectName(PresentedActor actor, RoOnlineActorSnapshot snapshot) {
            if (actor?.Root == null) {
                return;
            }

            string actorName = snapshot.Name ?? string.Empty;
            if (actor.HasObjectNameState &&
                actor.ObjectNameServerId == snapshot.ServerId &&
                actor.ObjectNameObjectType == snapshot.ObjectType &&
                actor.ObjectNameItemId == snapshot.ItemId &&
                string.Equals(actor.ObjectNameActorName, actorName, StringComparison.Ordinal)) {
                return;
            }

            string objectName = BuildActorObjectName(snapshot);
            actor.Root.name = objectName;
            actor.HasObjectNameState = true;
            actor.ObjectNameServerId = snapshot.ServerId;
            actor.ObjectNameObjectType = snapshot.ObjectType;
            actor.ObjectNameItemId = snapshot.ItemId;
            actor.ObjectNameActorName = actorName;
        }

        private void ApplyActorVisualState(
            PresentedActor actor,
            RoOnlineActorSnapshot snapshot,
            RoSpriteMotion motion,
            RoActorDirection facing,
            bool actionRevisionChanged,
            bool? loop = null,
            int? headDirectionFrameIndex = null
        ) {
            if (actor?.Granny != null) {
                // the model maps the motion to its own actions (stand / move / attack / damage / dead)
                RoMotionStep modelStep = actor.MotionStep.Motion == motion ? actor.MotionStep : new RoMotionStep(motion, ShouldLoopSpriteMotion(motion) ? RoMotionMode.Loop : RoMotionMode.Once);
                actor.Granny.SetMotion(motion, facing, actionRevisionChanged, modelStep.FrameDelayScale > 0f ? modelStep.FrameDelayScale : 1f, loop ?? modelStep.Loops);
                return;
            }

            if (actor?.Visual == null) {
                return;
            }

            RoMotionStep step = actor.MotionStep.Motion == motion ? actor.MotionStep : new RoMotionStep(motion, ShouldLoopSpriteMotion(motion) ? RoMotionMode.Loop : RoMotionMode.Once);
            bool resolvedLoop = loop ?? step.Loops;
            if (!actor.HasVisualState || actor.LastLoop != resolvedLoop) {
                actor.Visual.SetLoop(resolvedLoop);
                actor.LastLoop = resolvedLoop;
            }

            const float opacity = 1f;
            if (!actor.HasVisualState || !Mathf.Approximately(actor.LastOpacity, opacity)) {
                actor.Visual.SetOpacity(opacity);
                actor.LastOpacity = opacity;
            }

            bool visualMotionStateChanged = !actor.HasVisualState ||
                actionRevisionChanged ||
                actor.LastMotion != motion ||
                actor.LastFacing != facing;
            if (visualMotionStateChanged) {
                actor.Visual.SetMotionAndFacing(motion, facing, restart: actionRevisionChanged, force: actionRevisionChanged);
                // Attack3 is used for double-attack / combo hits. Some job ACT files lack a dedicated
                // third attack action (missing or empty frames), which makes the renderer show no mesh
                // for the whole attack motion. Fall back to Attack1, matching roBrowser's
                // "if (option.action === -2) option.action = ATTACK1" guard in setAction.
                if (RoActorActionPresenter.ShouldFallbackBasicAttackMotion(motion, actor.Visual)) {
                    actor.Visual.SetMotionAndFacing(RoSpriteMotion.Attack1, facing, restart: actionRevisionChanged, force: true);
                    motion = RoSpriteMotion.Attack1;
                }
                actor.LastMotion = motion;
                actor.LastFacing = facing;
            }

            // the cast pose holds its first frame until another action replaces it (state 8, actormotion §2.3)
            actor.Visual.SetMotionFirstFrameHeld(step.Motion == RoSpriteMotion.Casting && step.Mode == RoMotionMode.Frozen);

            // the action's speed factor scales the ACT delay of every frame (+0x64); walking and resting run at 1
            float frameDelayScale = step.FrameDelayScale > 0f ? step.FrameDelayScale : 1f;
            if (visualMotionStateChanged || !actor.HasVisualState || !Mathf.Approximately(actor.LastFrameDelayScale, frameDelayScale)) {
                actor.Visual.SetFrameDelayScale(frameDelayScale);
                actor.LastFrameDelayScale = frameDelayScale;
            }

            if (motion == RoSpriteMotion.Walk && actor.HasWalkDistancePosition) {
                using var walkMarker = SyncVisualWalkMarker.Auto();
                actor.Visual.SetWalkAnimationDistance(actor.WalkDistanceCells, MovementTuning);
            }

            using var headMarker = SyncVisualHeadMarker.Auto();
            int resolvedHeadDirectionFrameIndex = headDirectionFrameIndex ?? ResolveHeadDirectionFrameIndex(snapshot);
            if (!actor.HasVisualState || actor.LastHeadDirectionFrameIndex != resolvedHeadDirectionFrameIndex) {
                actor.Visual.SetHeadDirectionFrameIndex(resolvedHeadDirectionFrameIndex);
                actor.LastHeadDirectionFrameIndex = resolvedHeadDirectionFrameIndex;
            }

            actor.HasVisualState = true;
        }


        /// <summary>The status render state (stone grey, poison, freeze frame, Hiding / Cloaking ...) on the visual.</summary>
        private void ApplyActorStatusVisualState(PresentedActor actor, RoOnlineActorSnapshot snapshot) {
            if (actor?.Visual == null) {
                return;
            }

            Ragnarok.Combat.RoActorStatusLook look = ResolveStatusLook(snapshot.ServerId);
            if (!actor.HasAppliedStatusLook || !actor.AppliedStatusLook.Equals(look)) {
                actor.Visual.SetTintColor(look.Tint);
                actor.Visual.SetOpacity(look.IsDrawn ? 1f : 0f);
                actor.Visual.SetMotionCurrentFrameHeld(look.FreezeFrame);
                actor.AppliedStatusLook = look;
                actor.HasAppliedStatusLook = true;
            }

            ApplyMaxLevelAuraVisualState(actor, snapshot);
        }

        /// <summary>
        /// The max-level aura (FUN_00bcf420): players, homunculi and mercenaries at exactly their category's max level
        /// get the original's new-system aura on their shim, off while hidden, cloaked or invisible. The bridge is told
        /// only when the actor's aura state changes.
        /// </summary>
        private void ApplyMaxLevelAuraVisualState(PresentedActor actor, RoOnlineActorSnapshot snapshot) {
            int main = 0, sub = 0;
            if (snapshot.ObjectType == RoMapActorObjectType.Player ||
                snapshot.ObjectType == RoMapActorObjectType.Homunculus ||
                snapshot.ObjectType == RoMapActorObjectType.Mercenary) {
                Ragnarok.Combat.RoOriginalCombatRules.LevelAuraIds(snapshot.Job, snapshot.BaseLevel, out main, out sub);
            }

            bool blocked = Ragnarok.Combat.RoOriginalCombatRules.LevelAuraBlocked(snapshot.EffectState) ||
                !ResolveStatusLook(snapshot.ServerId).IsDrawn;
            int key = main == 0 && sub == 0 ? 0 : (main << 17) ^ (sub << 1) ^ (blocked ? 1 : 0);
            if (actor.LevelAuraStateId == snapshot.ServerId && actor.LevelAuraKey == key) {
                return;
            }

            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            if (bridge == null) {
                return;
            }

            if (actor.LevelAuraActorId != 0 && actor.LevelAuraActorId != snapshot.ServerId) {
                bridge.SetLevelAura(actor.LevelAuraActorId, null, 0, 0, false);
            }

            bridge.SetLevelAura(snapshot.ServerId, actor.Root.transform, main, sub, blocked);
            actor.LevelAuraActorId = key != 0 ? snapshot.ServerId : 0;
            actor.LevelAuraKey = key;
            actor.LevelAuraStateId = snapshot.ServerId;
        }

        /// <summary>The actor leaves the view (or its object goes back to the pool): its aura ends.</summary>
        private static void ClearMaxLevelAura(PresentedActor actor) {
            if (actor == null) {
                return;
            }

            actor.LevelAuraStateId = 0;
            if (actor.LevelAuraActorId == 0) {
                return;
            }

            Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance?.SetLevelAura(actor.LevelAuraActorId, null, 0, 0, false);
            actor.LevelAuraActorId = 0;
            actor.LevelAuraKey = 0;
        }

        private void ApplyActorRenderState(PresentedActor actor, RoOnlineActorSnapshot snapshot, Vector3 worldPosition) {
            if (actor?.Visual == null) {
                return;
            }

            if (!actor.HasRenderState || actor.LastRenderQueueBase != renderQueueBase) {
                actor.Visual.SetRenderQueueBase(renderQueueBase);
                actor.LastRenderQueueBase = renderQueueBase;
            }

            UnityEngine.Camera resolvedSortingCamera = useDynamicSortingOrder ? ResolveSortingCamera() : null;
            if (!CanSkipActorSortingUpdate(actor, worldPosition, resolvedSortingCamera)) {
                long sortingStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
                using (RoProfilerMarkers.SortingDepthUpdate.Auto())
                using (SortingUpdateMarker.Auto()) {
                    try {
                        int sortingOrder = ResolveActorSortingOrder(snapshot, worldPosition, resolvedSortingCamera);
                        if (!actor.HasRenderState || actor.LastSortingOrder != sortingOrder) {
                            actor.Visual.SetSortingOrderBase(sortingOrder);
                            actor.LastSortingOrder = sortingOrder;
                        }
                    } finally {
                        RoRuntimePerfCounters.AddActorSortingUpdateTicks(
                            RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(sortingStartTicks));
                    }
                }

                RecordActorSortingInput(actor, worldPosition, resolvedSortingCamera);
            }

            actor.HasRenderState = true;
        }

        private bool CanSkipActorSortingUpdate(PresentedActor actor, Vector3 worldPosition, UnityEngine.Camera resolvedSortingCamera) {
            // the client's single draw order overwrites the screen-y order every frame: computed once (a falcon takes its
            // owner's), again when that order goes off (RequestFullSync)
            if (RoClientDrawOrder.Enabled && actor.HasRenderState && actor.LastSortingOrder != int.MinValue) {
                return true;
            }

            return actor.HasRenderState &&
                actor.LastSortingDynamic == useDynamicSortingOrder &&
                actor.LastSortingOrderBase == sortingOrderBase &&
                actor.LastSortingBucketCount == sortingBucketCount &&
                actor.LastSortingCameraRevision == sortingCameraRevision &&
                actor.LastSortingCamera == resolvedSortingCamera &&
                Vector3.SqrMagnitude(actor.LastSortingWorldPosition - worldPosition) <= 0.0001f;
        }

        private void RecordActorSortingInput(PresentedActor actor, Vector3 worldPosition, UnityEngine.Camera resolvedSortingCamera) {
            if (actor == null) {
                return;
            }

            actor.LastSortingDynamic = useDynamicSortingOrder;
            actor.LastSortingOrderBase = sortingOrderBase;
            actor.LastSortingBucketCount = sortingBucketCount;
            actor.LastSortingCameraRevision = sortingCameraRevision;
            actor.LastSortingCamera = resolvedSortingCamera;
            actor.LastSortingWorldPosition = worldPosition;
        }

        private PresentedActor CreateActor(RoOnlineActorSnapshot snapshot, RoOnlineActorPresentationSignature signature) {
            PresentedActor actor = AcquirePresentedActor();
            if (!actor.HasBuiltPresentation || !actor.Signature.Equals(signature) || ShouldRetryAssetVisual(actor)) {
                RebuildActorVisual(actor, snapshot, signature);
            }

            return actor;
        }

        private void RebuildActorVisual(PresentedActor actor, RoOnlineActorSnapshot snapshot, RoOnlineActorPresentationSignature signature) {
            actor.DestroyVisualChildren();
            RoActorEffectAnchors anchors = RoActorEffectAnchors.EnsureOn(actor.Root);
            actor.InvalidateVisualState();
            actor.Signature = signature;
            actor.Visual = null;
            actor.TriedAssetVisual = assets != null && assets.IsInitialized;
            actor.HasBuiltPresentation = false;

            RoActorLayeredVisual visual = null;
            if (assets != null && assets.IsInitialized && TryCreateGrannyModel(actor, snapshot)) {
                // a 3D model instead of sprite layers: an opaque mesh in the geometry queue, so the sprites and effects
                // depth-test against it without the client draw order; its renderers appear once the file has loaded
                actor.HasBuiltPresentation = true;
                if (createPointerTargets) {
                    CreatePointerTarget(actor, snapshot);
                }

                anchors?.RefreshOverheadAnchor(actor.Root.transform);
                return;
            }

            ActorVisualBuildStatus buildStatus = assets != null && assets.IsInitialized
                ? TryCreateLayeredVisual(actor.Root.transform, snapshot, out visual)
                : ActorVisualBuildStatus.Failed;
            if (buildStatus == ActorVisualBuildStatus.Ready) {
                actor.Visual = visual;
                actor.HasBuiltPresentation = true;
                EnsureActorMapLightProbe(actor.Root.transform, snapshot);
                if (ShouldUseFakeActorShadow(snapshot)) {
                    CreateActorShadow(actor.Root.transform);
                }
            } else {
                if (createFallbackCapsules && ShouldCreateFallbackCapsule(snapshot)) {
                    CreateFallbackCapsule(actor.Root.transform, snapshot);
                }

                actor.HasBuiltPresentation = true;
            }

            if (createPointerTargets) {
                CreatePointerTarget(actor, snapshot);
            }

            anchors?.RefreshOverheadAnchor(visual != null ? visual.transform : actor.Root.transform);
        }

        /// <summary>
        /// The jobname resource of a monster / NPC is a Granny model (Empelium90_0.gr2, the WoE guardians, guild flags,
        /// the treasure box): the model is created under the actor's root. False keeps the sprite path (and its fallback).
        /// </summary>
        private bool TryCreateGrannyModel(PresentedActor actor, RoOnlineActorSnapshot snapshot) {
            actor.Granny = null;
            if (snapshot.ObjectType == RoMapActorObjectType.Player || snapshot.ObjectType == RoMapActorObjectType.Item) {
                return false;
            }

            string resourceName = null;
            RoEntityDisplayMetadataTable entityDisplay = assets.GameData?.EntityDisplay ?? RoEntityDisplayMetadataTable.Empty;
            if (entityDisplay.TryGetEntity(snapshot.Job, out RoEntityDisplayMetadata metadata) &&
                Ragnarok.Granny.RoGrannyActorModel.IsGrannyResource(metadata.ResourceName)) {
                resourceName = metadata.ResourceName;
            } else if (RoEntityDisplayMetadataTable.TryGetKnownNpcResourceName(snapshot.Job, out string npcResource) &&
                Ragnarok.Granny.RoGrannyActorModel.IsGrannyResource(npcResource)) {
                resourceName = npcResource;
            }

            if (resourceName == null ||
                !Ragnarok.Granny.RoGrannyActorModel.TryCreate(assets, resourceName, actor.Root.transform, out Ragnarok.Granny.RoGrannyActorModel model)) {
                return false;
            }

            actor.Granny = model;
            actor.GrannyServerId = snapshot.ServerId;
            actor.GrannyGuildId = snapshot.GuildId;
            actor.GrannyEmblemSerial = -1;
            if (!grannyActors.Contains(actor)) {
                grannyActors.Add(actor);
            }

            return true;
        }

        private readonly List<PresentedActor> grannyActors = new List<PresentedActor>();

        /// <summary>
        /// A guild flag's emblem (C3dGrannyGameActor: the guild id of the actor's entry packet): set once the emblem file is
        /// there, again when an emblem lands (the download is asynchronous); no guild = the emblem mesh stays hidden.
        /// </summary>
        private void UpdateGrannyEmblems() {
            if (grannyActors.Count == 0) {
                return;
            }

            int serial = Ragnarok.UI.RoClassicGuildEmblems.Serial;
            RoOnlineWorldState state = ResolveWorldController()?.State;
            for (int i = grannyActors.Count - 1; i >= 0; i--) {
                PresentedActor actor = grannyActors[i];
                if (actor?.Granny == null) {
                    grannyActors.RemoveAt(i);
                    continue;
                }

                // ZC_CHANGE_GUILD changes only the guild, which an incremental sync need not present: read here (a few models)
                if (state != null && state.TryGetActor(actor.GrannyServerId, out RoOnlineActorSnapshot snapshot) &&
                    snapshot.GuildId != actor.GrannyGuildId) {
                    actor.GrannyGuildId = snapshot.GuildId;
                    actor.GrannyEmblemSerial = -1;
                    if (snapshot.GuildId == 0) {
                        actor.Granny.SetEmblem(null);
                    }
                }

                if (actor.GrannyGuildId == 0 || actor.GrannyEmblemSerial == serial) {
                    continue;
                }

                Texture2D emblem = Ragnarok.UI.RoClassicGuildEmblems.ResolveTexture(actor.GrannyGuildId);
                if (emblem != null) {
                    actor.Granny.SetEmblem(emblem);
                    actor.GrannyEmblemSerial = serial;
                }
            }
        }

        private void SyncRentalAttachments(
            PresentedActor actor,
            RoOnlineActorSnapshot snapshot,
            RoSpriteMotion ownerMotion,
            RoActorDirection ownerFacing
        ) {
            if (actor == null) {
                return;
            }

            if (actor.Root == null || assets == null || !assets.IsInitialized || snapshot.ObjectType != RoMapActorObjectType.Player) {
                DestroyRentalCompanion(ref actor.FalconCompanion);
                DestroyRentalAttachment(ref actor.WugVisual);
                return;
            }

            short baseJob = ResolveRentalAttachmentBaseJob(snapshot);
            SyncFalconAttachment(actor, snapshot, baseJob, ownerFacing);
            SyncRentalAttachment(
                actor.Root.transform,
                snapshot.HasWug,
                RoMapRentalVisualSemantics.ResolveWugBasePath(baseJob),
                "Wug",
                ref actor.WugVisual,
                ownerMotion,
                ownerFacing,
                Vector3.up * RoActorSpriteVisual.GroundedVisualYOffset,
                renderQueueBase - 5,
                null);
        }

        private void SyncFalconAttachment(
            PresentedActor actor,
            RoOnlineActorSnapshot snapshot,
            short baseJob,
            RoActorDirection ownerFacing
        ) {
            bool active = snapshot.HasFalcon && RoMapRentalVisualSemantics.CanUseFalconAttachmentVisual(baseJob);
            if (!active) {
                DestroyRentalCompanion(ref actor.FalconCompanion);
                return;
            }

            EnsureRentalCompanionRoot();
            if (actor.FalconCompanion == null) {
                var companionObject = new GameObject(RoRentalCompanionVisual.BuildFalconObjectName(snapshot.ServerId));
                companionObject.transform.SetParent(rentalCompanionRoot != null ? rentalCompanionRoot : transform, false);
                actor.FalconCompanion = companionObject.AddComponent<RoRentalCompanionVisual>();
            } else {
                actor.FalconCompanion.name = RoRentalCompanionVisual.BuildFalconObjectName(snapshot.ServerId);
            }

            int ownerSortingOrder = actor.LastSortingOrder != int.MinValue
                ? actor.LastSortingOrder
                : ResolveActorSortingOrder(snapshot, actor.Root.transform.position);
            if (!actor.FalconCompanion.SyncFalcon(
                    assets,
                    snapshot.ServerId,
                    actor.Root.transform,
                    actor.Root.transform.position,
                    RoMapRentalVisualSemantics.ResolveFalconBasePath(baseJob),
                    ownerFacing,
                    snapshot.Speed,
                    renderQueueBase,
                    ownerSortingOrder,
                    snapToOwner: false)) {
                DestroyRentalCompanion(ref actor.FalconCompanion);
            }
        }

        private void SyncRentalAttachment(
            Transform parent,
            bool active,
            string basePath,
            string label,
            ref RoActorLayeredVisual visual,
            RoSpriteMotion ownerMotion,
            RoActorDirection ownerFacing,
            Vector3 localOffset,
            int resolvedRenderQueueBase,
            int? resolvedSortingOrderBase
        ) {
            if (!active || string.IsNullOrWhiteSpace(basePath)) {
                DestroyRentalAttachment(ref visual);
                return;
            }

            if (visual == null) {
                var attachmentObject = new GameObject($"RO Online Rental Attachment - {label}");
                attachmentObject.transform.SetParent(parent, false);
                attachmentObject.transform.localPosition = localOffset;
                visual = attachmentObject.AddComponent<RoActorLayeredVisual>();
                var specs = new[] {
                    new RoActorSpriteLayerSpec(RoActorSpriteLayer.Body, basePath) {
                        motionProfile = RoSpriteMotionProfile.Monster
                    }
                };

                if (!visual.Initialize(assets, specs, ownerMotion, ownerFacing)) {
                    DestroyRuntimeObject(attachmentObject);
                    visual = null;
                    return;
                }
            }

            visual.transform.localPosition = localOffset;
            visual.SetMotionAndFacing(ownerMotion, ownerFacing, restart: false);
            visual.SetRenderQueueBase(resolvedRenderQueueBase);
            if (resolvedSortingOrderBase.HasValue) {
                visual.SetSortingOrderBase(resolvedSortingOrderBase.Value);
            }
        }

        public static short ResolveRentalAttachmentBaseJob(RoOnlineActorSnapshot snapshot) {
            return snapshot.BaseJob != 0 ? snapshot.BaseJob : snapshot.Job;
        }

        public static RoSpriteMotion ResolveFalconAttachmentMotion() {
            return RoRentalCompanionVisual.ResolveFalconPassiveMotion(companionIsMoving: false);
        }

        public static Vector3 ResolveFalconAttachmentLocalOffset() {
            return Vector3.up * RoMapRentalVisualSemantics.FalconAltitudeOffset;
        }

        public static int ResolveFalconAttachmentRenderQueueBase(int ownerRenderQueueBase) {
            return RoRentalCompanionVisual.ResolveFalconRenderQueueBase(ownerRenderQueueBase);
        }

        public static int ResolveFalconAttachmentSortingOrder(int ownerSortingOrder) {
            return RoRentalCompanionVisual.ResolveFalconSortingOrder(ownerSortingOrder);
        }

        public bool TryPlayFalconSkillDash(ushort skillId, uint sourceId, Vector3 targetWorldPosition) {
            if (!RoMapRentalVisualSemantics.IsFalconSkill(skillId) ||
                sourceId == 0 ||
                !actors.TryGetValue(sourceId, out PresentedActor actor) ||
                actor?.FalconCompanion == null) {
                return false;
            }

            return actor.FalconCompanion.DashTo(targetWorldPosition);
        }

        private static void DestroyRentalAttachment(ref RoActorLayeredVisual visual) {
            if (visual == null) {
                return;
            }

            GameObject target = visual.gameObject;
            visual = null;
            DestroyRuntimeObject(target);
        }

        private static void DestroyRentalCompanion(ref RoRentalCompanionVisual companion) {
            if (companion == null) {
                return;
            }

            GameObject target = companion.gameObject;
            companion = null;
            DestroyRuntimeObject(target);
        }

        private ActorVisualBuildStatus TryCreateLayeredVisual(
            Transform parent,
            RoOnlineActorSnapshot snapshot,
            out RoActorLayeredVisual visual
        ) {
            visual = null;
            RoActorSpriteLayerSpec[] entitySpecs = null;
            bool hasEntitySpecs = false;
            if (snapshot.ObjectType != RoMapActorObjectType.Player &&
                snapshot.ObjectType != RoMapActorObjectType.Item &&
                ShouldAttemptEntitySpriteVisual(snapshot)) {
                if (!TryBuildEntityLayerSpecs(snapshot, out entitySpecs)) {
                    return ActorVisualBuildStatus.Failed;
                }

                if (!BuildEntityLayerAtlasesNow(entitySpecs)) {
                    return ActorVisualBuildStatus.Failed;
                }

                hasEntitySpecs = true;
            }

            var visualObject = new GameObject("RO Online Actor Layered Visual");
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = Vector3.up * RoActorSpriteVisual.GroundedVisualYOffset;

            visual = visualObject.AddComponent<RoActorLayeredVisual>();
            try {
                bool budgetRuntimeMeshBuilds = Application.isPlaying;
                visual.SetRuntimeSpriteBudgets(
                    meshBuilds: budgetRuntimeMeshBuilds,
                    visibleAnimationTicks: !MovementTuning.DisableWalkAnimatorThrottleInView);
                if (hasEntitySpecs) {
                    visual.SetUseMutableRuntimeMeshes(true);
                }

                visual.SetShadowRenderMode(CurrentShadowRenderMode, CurrentEnhancedShadowSettings);
                if (snapshot.ObjectType == RoMapActorObjectType.Player) {
                    RoActorAppearanceRequest appearance = BuildPlayerAppearanceRequest(snapshot);
                    if (visual.Initialize(assets, appearance)) {
                        return ActorVisualBuildStatus.Ready;
                    }
                } else if (snapshot.ObjectType == RoMapActorObjectType.Item) {
                    if (TryBuildGroundItemLayerSpecs(snapshot, out RoActorSpriteLayerSpec[] itemSpecs) &&
                        visual.Initialize(assets, itemSpecs, RoSpriteMotion.Idle, RoActorDirection.South)) {
                        return ActorVisualBuildStatus.Ready;
                    }
                } else if (hasEntitySpecs) {
                    if (visual.Initialize(assets, entitySpecs, ResolveMotion(snapshot), ResolveFacingDirection(snapshot))) {
                        visual.SetFrameDelayScale(1f);
                        if (!budgetRuntimeMeshBuilds) {
                            visual.PrebuildAllFrameMeshes();
                        }

                        return ActorVisualBuildStatus.Ready;
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning($"Could not create online actor visual for {snapshot.ServerId}: {ex.Message}", this);
            }

            DestroyRuntimeObject(visualObject);
            visual = null;
            return ActorVisualBuildStatus.Failed;
        }

        private bool BuildEntityLayerAtlasesNow(IReadOnlyList<RoActorSpriteLayerSpec> specs) {
            if (assets == null || specs == null || specs.Count == 0) {
                return false;
            }

            for (int i = 0; i < specs.Count; i++) {
                RoActorSpriteLayerSpec spec = specs[i];
                if (spec == null || string.IsNullOrWhiteSpace(spec.basePath)) {
                    return false;
                }

                byte[] paletteOverride = TryLoadLayerPalette(spec);
                if (!RoSpriteAtlasCache.TryGetOrBuild(assets, spec.basePath, out _, paletteOverride)) {
                    return false;
                }
            }

            return true;
        }

        private byte[] TryLoadLayerPalette(RoActorSpriteLayerSpec spec) {
            if (assets == null || spec == null || string.IsNullOrWhiteSpace(spec.palettePath)) {
                return null;
            }

            return RoSpriteAtlasCache.LoadPalette(assets, spec.palettePath);
        }

        private readonly HashSet<uint> spritePrefetchRequested = new HashSet<uint>();

        /// <summary>A new actor's layer preparations in the background and when they were asked for.</summary>
        private sealed class PendingSpritePrefetch {
            public readonly List<RoSpriteAtlasCache.PrefetchTicket> Tickets = new List<RoSpriteAtlasCache.PrefetchTicket>(4);
            public float RequestedAt;
        }

        private readonly Dictionary<uint, PendingSpritePrefetch> pendingSpritePrefetches = new Dictionary<uint, PendingSpritePrefetch>();
        private readonly Stack<PendingSpritePrefetch> pendingSpritePrefetchPool = new Stack<PendingSpritePrefetch>();

        /// <summary>
        /// The longest a new actor waits for its sprites to be read, parsed and compiled in the background before it is
        /// built anyway (synchronously, as before): a new monster type read on the main thread cost up to ~50 ms there.
        /// </summary>
        private const float MaxSpritePrefetchWaitSeconds = 0.5f;

        /// <summary>
        /// A brand-new actor (not the local player) waits until the background preparation of its layers is done, so
        /// its build only packs and uploads the atlases; the first sight asks for the preparation.
        /// </summary>
        private bool ShouldWaitForSpritePrefetch(RoOnlineActorSnapshot snapshot) {
            if (snapshot.IsSelf || !Application.isPlaying || (actors.TryGetValue(snapshot.ServerId, out PresentedActor actor) && actor.Root != null)) {
                return false;
            }

            if (!pendingSpritePrefetches.TryGetValue(snapshot.ServerId, out PendingSpritePrefetch pending)) {
                if (spritePrefetchRequested.Contains(snapshot.ServerId)) {
                    return false;   // asked for already by the budget path, without tickets: build as before
                }

                if (!RequestSpritePrefetch(snapshot, out pending)) {
                    return true;    // this frame's prefetch budget is spent: ask next frame
                }

                if (pending == null) {
                    return false;   // nothing to prepare (no sprite layers)
                }
            }

            bool done = true;
            for (int i = 0; i < pending.Tickets.Count && done; i++) {
                done = pending.Tickets[i].Done;
            }

            if (!done && RoPresentationClock.NowSeconds - pending.RequestedAt < MaxSpritePrefetchWaitSeconds) {
                return true;
            }

            ReleasePendingSpritePrefetch(snapshot.ServerId);
            return false;
        }

        private void ReleasePendingSpritePrefetch(uint serverId) {
            if (pendingSpritePrefetches.TryGetValue(serverId, out PendingSpritePrefetch pending)) {
                pendingSpritePrefetches.Remove(serverId);
                pending.Tickets.Clear();
                pendingSpritePrefetchPool.Push(pending);
            }
        }
        private const double SpritePrefetchBudgetMilliseconds = 2.0;
        private int spritePrefetchFrame = -1;
        private long spritePrefetchFrameTicks;
        private static readonly ProfilerMarker SpritePrefetchMarker = new ProfilerMarker("RO.Actor.SpritePrefetch");

        /// <summary>
        /// A new actor whose build waits for the per-frame budget: its layers' sprites are prepared in the background
        /// meanwhile (RoSpriteAtlasCache.Prefetch), so the build itself only packs and uploads the atlases.
        /// </summary>
        private void RequestSpritePrefetch(RoOnlineActorSnapshot snapshot) => RequestSpritePrefetch(snapshot, out _);

        /// <summary>
        /// False when this frame's budget is spent (nothing asked). True otherwise, with the tickets of the asked layers in
        /// <paramref name="pending"/> (registered for <see cref="ShouldWaitForSpritePrefetch"/>), or null when there is none.
        /// </summary>
        private bool RequestSpritePrefetch(RoOnlineActorSnapshot snapshot, out PendingSpritePrefetch pending) {
            pending = null;
            if (assets == null || !assets.IsInitialized || spritePrefetchRequested.Contains(snapshot.ServerId)) {
                return true;
            }

            // the layer paths are resolved here on the main thread: a few ms of it a frame, the rest waits for the next
            int frame = Time.frameCount;
            if (frame != spritePrefetchFrame) {
                spritePrefetchFrame = frame;
                spritePrefetchFrameTicks = 0;
            } else if (spritePrefetchFrameTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency >= SpritePrefetchBudgetMilliseconds) {
                return false;
            }

            spritePrefetchRequested.Add(snapshot.ServerId);
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            using var marker = SpritePrefetchMarker.Auto();
            try {
                IReadOnlyList<RoActorSpriteLayerSpec> specs = null;
                if (snapshot.ObjectType == RoMapActorObjectType.Player) {
                    specs = RoActorSpritePathResolver.BuildPlayerLayerSpecs(assets, BuildPlayerAppearanceRequest(snapshot));
                } else if (snapshot.ObjectType != RoMapActorObjectType.Item &&
                    ShouldAttemptEntitySpriteVisual(snapshot) &&
                    TryBuildEntityLayerSpecs(snapshot, out RoActorSpriteLayerSpec[] entitySpecs)) {
                    specs = entitySpecs;
                }

                for (int i = 0; specs != null && i < specs.Count; i++) {
                    if (specs[i] == null) {
                        continue;
                    }

                    RoSpriteAtlasCache.PrefetchTicket ticket = RoSpriteAtlasCache.Prefetch(assets, specs[i].basePath, specs[i].palettePath);
                    if (ticket.Done) {
                        continue;
                    }

                    if (pending == null) {
                        pending = pendingSpritePrefetchPool.Count > 0 ? pendingSpritePrefetchPool.Pop() : new PendingSpritePrefetch();
                        pending.RequestedAt = RoPresentationClock.NowSeconds;
                        pendingSpritePrefetches[snapshot.ServerId] = pending;
                    }

                    pending.Tickets.Add(ticket);
                }
            } catch (Exception ex) {
                Debug.LogWarning($"Sprite prefetch for actor {snapshot.ServerId} failed: {ex.Message}", this);
            } finally {
                spritePrefetchFrameTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            }

            return true;
        }

        private bool TryBuildEntityLayerSpecs(RoOnlineActorSnapshot snapshot, out RoActorSpriteLayerSpec[] specs) {
            specs = Array.Empty<RoActorSpriteLayerSpec>();
            if (assets == null || !assets.IsInitialized) {
                return false;
            }

            RoEntityDisplayMetadataTable entityDisplay = assets.GameData?.EntityDisplay ?? RoEntityDisplayMetadataTable.Empty;
            if (!TryResolveEntitySpriteBasePath(snapshot, entityDisplay, out string basePath)) {
                return false;
            }

            specs = new[] {
                new RoActorSpriteLayerSpec(RoActorSpriteLayer.Body, basePath) {
                    initialActionIndex = 0,
                    motionProfile = ResolveMotionProfile(snapshot)
                }
            };
            return true;
        }

        private bool TryResolveEntitySpriteBasePath(
            RoOnlineActorSnapshot snapshot,
            RoEntityDisplayMetadataTable entityDisplay,
            out string basePath
        ) {
            basePath = null;
            if (entityDisplay.TryGetEntity(snapshot.Job, out RoEntityDisplayMetadata metadata)) {
                if (entityDisplay.TryResolveSpriteBasePath(assets, snapshot.Job, out basePath)) {
                    return true;
                }

                if (IsUnsupportedEntityModelResource(metadata.ResourceName) &&
                    TryResolveUnsupportedEntityModelSpriteFallback(metadata.ResourceName, out basePath)) {
                    return true;
                }
            }

            if (TryResolveFallbackEntitySpriteBasePath(snapshot, out basePath)) {
                return true;
            }

            return false;
        }

        private bool TryResolveFallbackEntitySpriteBasePath(RoOnlineActorSnapshot snapshot, out string basePath) {
            basePath = null;
            RoEntityDisplayKind kind = ResolveEntityDisplayKind(snapshot);
            if (kind == RoEntityDisplayKind.Unknown || assets == null || !assets.IsInitialized) {
                return false;
            }

            if (TryResolveNamedEntitySpriteBasePath(kind, snapshot.Name, out basePath)) {
                return true;
            }

            if (TryResolveKnownEntitySpriteBasePath(kind, snapshot.Job, out basePath)) {
                return true;
            }

            return false;
        }

        private bool TryResolveNamedEntitySpriteBasePath(RoEntityDisplayKind kind, string actorName, out string basePath) {
            basePath = null;
            string resourceName = NormalizeActorResourceName(actorName);
            if (string.IsNullOrEmpty(resourceName)) {
                return false;
            }

            string candidate = new RoEntityDisplayMetadata(0, kind, resourceName).GetSpriteBasePath();
            if (SpritePairExists(candidate)) {
                basePath = candidate;
                return true;
            }

            return false;
        }

        private bool TryResolveKnownEntitySpriteBasePath(RoEntityDisplayKind kind, short job, out string basePath) {
            basePath = null;
            if (kind == RoEntityDisplayKind.Monster) {
                string resourceName = job switch {
                    1001 => "scorpion",
                    1002 => "poring",
                    _ => null
                };

                return TryResolveKnownEntityResourceBasePath(kind, job, resourceName, out basePath);
            } else if (kind == RoEntityDisplayKind.Npc) {
                if (RoEntityDisplayMetadataTable.TryGetKnownNpcResourceName(job, out string resourceName)) {
                    if (IsUnsupportedEntityModelResource(resourceName)) {
                        return TryResolveUnsupportedEntityModelSpriteFallback(resourceName, out basePath);
                    }

                    if (TryResolveKnownEntityResourceBasePath(kind, job, resourceName, out basePath)) {
                        return true;
                    }
                }

                return TryResolveKnownEntityResourceBasePath(kind, job, "1_etc_01", out basePath);
            }

            return false;
        }

        private bool TryResolveKnownEntityResourceBasePath(RoEntityDisplayKind kind, short job, string resourceName, out string basePath) {
            basePath = null;
            if (string.IsNullOrEmpty(resourceName)) {
                return false;
            }

            string candidate = new RoEntityDisplayMetadata(job, kind, resourceName).GetSpriteBasePath();
            if (SpritePairExists(candidate)) {
                basePath = candidate;
                return true;
            }

            return false;
        }

        private bool TryResolveUnsupportedEntityModelSpriteFallback(string resourceName, out string basePath) {
            basePath = null;
            ResolveUnsupportedEntityModelSpriteFallback(
                resourceName,
                out int fallbackClassId,
                out string fallbackResourceName);

            if (TryResolveKnownEntityResourceBasePath(
                RoEntityDisplayKind.Monster,
                (short) fallbackClassId,
                fallbackResourceName,
                out basePath)) {
                return true;
            }

            if (fallbackClassId != 1002 &&
                TryResolveKnownEntityResourceBasePath(RoEntityDisplayKind.Monster, 1002, "poring", out basePath)) {
                return true;
            }

            return false;
        }

        private bool SpritePairExists(string basePath) {
            return !string.IsNullOrWhiteSpace(basePath) &&
                assets != null &&
                assets.Exists(basePath + ".spr") &&
                assets.Exists(basePath + ".act");
        }

        public static bool IsUnsupportedEntityModelResource(string resourceName) {
            return !string.IsNullOrWhiteSpace(resourceName) &&
                resourceName.Trim().EndsWith(".gr2", StringComparison.OrdinalIgnoreCase);
        }

        public static void ResolveUnsupportedEntityModelSpriteFallback(
            string resourceName,
            out int fallbackClassId,
            out string fallbackResourceName
        ) {
            string normalized = string.IsNullOrWhiteSpace(resourceName)
                ? string.Empty
                : resourceName.Trim().Replace('\\', '/');
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0) {
                normalized = normalized.Substring(slashIndex + 1);
            }

            if (normalized.Equals("aguardian90_8.gr2", StringComparison.OrdinalIgnoreCase)) {
                fallbackClassId = 1276;
                fallbackResourceName = "Raydric_Archer";
            } else if (normalized.Equals("empelium90_0.gr2", StringComparison.OrdinalIgnoreCase)) {
                fallbackClassId = 2080;
                fallbackResourceName = "CRYSTAL_L";
            } else if (normalized.Equals("guildflag90_1.gr2", StringComparison.OrdinalIgnoreCase)) {
                fallbackClassId = 1911;
                fallbackResourceName = "OBJ_NEUTRAL";
            } else if (normalized.Equals("kguardian90_7.gr2", StringComparison.OrdinalIgnoreCase)) {
                fallbackClassId = 2691;
                fallbackResourceName = "RAYDRIC";
            } else if (normalized.Equals("sguardian90_9.gr2", StringComparison.OrdinalIgnoreCase)) {
                fallbackClassId = 1163;
                fallbackResourceName = "RAYDRIC";
            } else if (normalized.Equals("treasurebox_2.gr2", StringComparison.OrdinalIgnoreCase)) {
                fallbackClassId = 1191;
                fallbackResourceName = "MIMIC";
            } else {
                fallbackClassId = 1002;
                fallbackResourceName = "poring";
            }
        }

        private static RoEntityDisplayKind ResolveEntityDisplayKind(RoOnlineActorSnapshot snapshot) {
            return snapshot.ObjectType switch {
                RoMapActorObjectType.Npc or RoMapActorObjectType.Npc2 => RoEntityDisplayKind.Npc,
                RoMapActorObjectType.Monster => RoEntityDisplayKind.Monster,
                RoMapActorObjectType.Pet or RoMapActorObjectType.Homunculus or RoMapActorObjectType.Mercenary or RoMapActorObjectType.Elemental => RoEntityDisplayKind.Homunculus,
                _ => RoEntityDisplayMetadataTable.Classify(snapshot.Job)
            };
        }





        private static bool IsAttackMotion(RoSpriteMotion motion)
        {
            return motion == RoSpriteMotion.Attack1 ||
                motion == RoSpriteMotion.Attack2 ||
                motion == RoSpriteMotion.Attack3;
        }

        private string NormalizeActorResourceName(string actorName) {
            if (string.IsNullOrWhiteSpace(actorName)) {
                return string.Empty;
            }

            if (actorResourceNameCache.TryGetValue(actorName, out string cached)) {
                return cached;
            }

            string originalName = actorName;
            int suffixIndex = actorName.IndexOf('#');
            if (suffixIndex > 0) {
                actorName = actorName.Substring(0, suffixIndex);
            }

            actorName = actorName.Trim().ToLowerInvariant();
            actorResourceNameBuilder.Length = 0;
            bool lastWasSeparator = false;
            for (int i = 0; i < actorName.Length; i++) {
                char c = actorName[i];
                if (char.IsLetterOrDigit(c)) {
                    actorResourceNameBuilder.Append(c);
                    lastWasSeparator = false;
                } else if ((char.IsWhiteSpace(c) || c == '-' || c == '_') && !lastWasSeparator && actorResourceNameBuilder.Length > 0) {
                    actorResourceNameBuilder.Append('_');
                    lastWasSeparator = true;
                }
            }

            if (actorResourceNameBuilder.Length > 0 && actorResourceNameBuilder[actorResourceNameBuilder.Length - 1] == '_') {
                actorResourceNameBuilder.Length--;
            }

            string normalized = actorResourceNameBuilder.ToString();
            actorResourceNameCache[originalName] = normalized;
            return normalized;
        }

        private bool TryBuildGroundItemLayerSpecs(RoOnlineActorSnapshot snapshot, out RoActorSpriteLayerSpec[] specs) {
            specs = Array.Empty<RoActorSpriteLayerSpec>();
            if (assets == null || !assets.IsInitialized || snapshot.ItemId == 0) {
                return false;
            }

            RoItemMetadataTable items = assets.GameData?.Items ?? RoItemMetadataTable.Empty;
            RoItemMetadata item = items.GetItemOrFallback(unchecked((int) snapshot.ItemId));
            string basePath = item.GetWorldSpriteBasePath(snapshot.ItemIdentified);
            if (!assets.Exists(basePath + ".spr") || !assets.Exists(basePath + ".act")) {
                return false;
            }

            specs = new[] {
                new RoActorSpriteLayerSpec(RoActorSpriteLayer.Body, basePath) {
                    initialActionIndex = 0,
                    motionProfile = RoSpriteMotionProfile.Npc
                }
            };
            return true;
        }

        private void CreateActorShadow(Transform parent) {
            var shadowObject = new GameObject("RO Online Actor Shadow");
            shadowObject.transform.SetParent(parent, false);
            var shadow = shadowObject.AddComponent<RoActorShadowVisual>();
            shadow.Initialize(assets, actorGroundOffset: 0f);
        }

        private RoShadowRenderMode CurrentShadowRenderMode =>
            mapController != null ? mapController.ShadowRenderMode : RoShadowRenderMode.Original;

        private RoEnhancedShadowSettings CurrentEnhancedShadowSettings =>
            mapController != null ? mapController.EnhancedShadowSettings : new RoEnhancedShadowSettings();

        private bool ShouldUseFakeActorShadow(RoOnlineActorSnapshot snapshot) =>
            RoEnhancedShadowRuntime.ShouldUseFakeActorShadow(CurrentShadowRenderMode, createShadows) &&
            ShouldCreateShadow(snapshot);

        private void ApplyActorShadowRenderMode(PresentedActor actor, RoOnlineActorSnapshot snapshot, bool active) {
            if (actor?.Root == null) {
                return;
            }

            // the same state again changes nothing (every layer's materials, the shared shadow card, child lookups)
            RoShadowRenderMode mode = CurrentShadowRenderMode;
            RoEnhancedShadowSettings settings = CurrentEnhancedShadowSettings;
            bool fake = ShouldUseFakeActorShadow(snapshot);
            if (actor.HasShadowState &&
                actor.ShadowStateVisual == actor.Visual &&
                actor.LastShadowMode == mode &&
                actor.LastShadowSettings == settings &&
                actor.LastShadowActive == active &&
                actor.LastShadowFake == fake) {
                return;
            }

            actor.HasShadowState = true;
            actor.ShadowStateVisual = actor.Visual;
            actor.LastShadowMode = mode;
            actor.LastShadowSettings = settings;
            actor.LastShadowActive = active;
            actor.LastShadowFake = fake;
            actor.Visual?.SetShadowRenderMode(CurrentShadowRenderMode, CurrentEnhancedShadowSettings);
            if (CurrentShadowRenderMode == RoShadowRenderMode.Enhanced) {
                DestroyActorShadow(actor);
                return;
            }

            if (ShouldUseFakeActorShadow(snapshot) && actor.Root.transform.Find("RO Online Actor Shadow") == null) {
                CreateActorShadow(actor.Root.transform);
            }

            SetActorShadowActive(actor, active);
        }

        private void EnsureActorMapLightProbe(Transform parent, RoOnlineActorSnapshot snapshot) {
            if (parent == null) {
                return;
            }

            var probe = parent.FindComponent<RoActorMapLightProbe>();
            if (probe == null) {
                probe = parent.gameObject.AddComponent<RoActorMapLightProbe>();
            }

            Collider collider = mapController != null ? mapController.GroundCollider : null;
            probe.SetSamplingMode(ResolveActorMapLightProbeSamplingMode(snapshot));
            probe.Initialize(
                collider,
                mapController != null ? mapController.CompiledGround : null,
                enableBakedShadowSampling:
                    RoActorLightingPolicy.Resolve(CurrentShadowRenderMode, CurrentEnhancedShadowSettings).SampleBakedShadows,
                controller: mapController);
        }

        private static RoActorMapLightProbeSamplingMode ResolveActorMapLightProbeSamplingMode(RoOnlineActorSnapshot snapshot) {
            return snapshot.IsSelf
                ? RoActorMapLightProbeSamplingMode.Continuous
                : RoActorMapLightProbeSamplingMode.OnMoveOrInitial;
        }

        private void CreateFallbackCapsule(Transform parent, RoOnlineActorSnapshot snapshot) {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "RO Online Actor Fallback";
            capsule.transform.SetParent(parent, false);
            float height = snapshot.ObjectType == RoMapActorObjectType.Item ? 0.25f : FallbackCapsuleHeight;
            float width = snapshot.ObjectType == RoMapActorObjectType.Item ? 0.25f : 0.55f;
            capsule.transform.localPosition = Vector3.up * (height * 0.5f);
            capsule.transform.localScale = new Vector3(width, height, width);

            Collider collider = capsule.FindComponent<Collider>();
            if (collider != null) {
                DestroyRuntimeObject(collider);
            }

            Renderer renderer = capsule.FindComponent<Renderer>();
            if (renderer != null) {
                renderer.sharedMaterial = snapshot.ObjectType == RoMapActorObjectType.Item
                    ? itemFallbackMaterial ??= CreateItemFallbackMaterial()
                    : fallbackMaterial ??= CreateFallbackMaterial();
                // a sprite the GRFs lack (e.g. the pet nightmare_terror_mj): the client draws nothing for it, so players
                // see no placeholder; the editor and development builds keep it to spot missing assets
                renderer.enabled = Application.isEditor || Debug.isDebugBuild;
            }
        }

        private void CreatePointerTarget(PresentedActor actor, RoOnlineActorSnapshot snapshot) {
            Transform parent = actor.Root.transform;
            var targetObject = new GameObject("RO Online Pointer Target");
            targetObject.transform.SetParent(parent, false);

            RoPointerTargetKind pointerKind = RoOnlinePointerTargetMarker.GetPointerTargetKind(snapshot.ObjectType);
            var marker = targetObject.AddComponent<RoPointerTargetMarker>();
            marker.Kind = pointerKind;
            marker.BlocksGroundClick = true;
            marker.Label = BuildPointerTargetLabel(snapshot);

            var onlineMarker = targetObject.AddComponent<RoOnlinePointerTargetMarker>();
            onlineMarker.Configure(worldController, snapshot.ServerId, snapshot.ObjectType);
            actor.PointerMarker = marker;
            actor.OnlinePointerMarker = onlineMarker;

            if (snapshot.ObjectType == RoMapActorObjectType.Item) {
                var sphere = targetObject.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 0.35f;
                sphere.center = Vector3.up * 0.18f;
                return;
            }

            ConfigureActorPointerSpriteShape(actor, targetObject.transform);
        }

        private void RefreshPointerTarget(PresentedActor actor, RoOnlineActorSnapshot snapshot) {
            if (!createPointerTargets || actor?.Root == null) {
                return;
            }

            if (!ShouldEnablePointerTarget(snapshot)) {
                SetPointerTargetActive(actor, false);
                return;
            }

            RoOnlinePointerTargetMarker onlineMarker = actor.OnlinePointerMarker;
            if (onlineMarker == null) {
                onlineMarker = actor.Root.GetComponentInChildren<RoOnlinePointerTargetMarker>(true);
                actor.OnlinePointerMarker = onlineMarker;
            }

            if (onlineMarker != null) {
                onlineMarker.gameObject.SetActive(true);
                onlineMarker.Configure(worldController, snapshot.ServerId, snapshot.ObjectType);
            }

            RoPointerTargetMarker pointerMarker = actor.PointerMarker;
            if (pointerMarker == null) {
                pointerMarker = actor.Root.GetComponentInChildren<RoPointerTargetMarker>(true);
                actor.PointerMarker = pointerMarker;
            }

            if (pointerMarker != null) {
                pointerMarker.gameObject.SetActive(true);
                pointerMarker.Kind = RoOnlinePointerTargetMarker.GetPointerTargetKind(snapshot.ObjectType);
                pointerMarker.BlocksGroundClick = true;
                pointerMarker.Label = BuildPointerTargetLabel(snapshot);
            }
        }

        private void RefreshPointerTargetShape(PresentedActor actor, RoOnlineActorSnapshot snapshot) {
            if (!createPointerTargets || actor?.Root == null) {
                return;
            }

            if (!ShouldEnablePointerTarget(snapshot)) {
                return;
            }

            Transform targetTransform = actor.PointerMarker != null
                ? actor.PointerMarker.transform
                : actor.OnlinePointerMarker != null
                    ? actor.OnlinePointerMarker.transform
                    : actor.Root.GetComponentInChildren<RoPointerTargetMarker>(true)?.transform;
            if (targetTransform == null) {
                return;
            }

            if (snapshot.ObjectType == RoMapActorObjectType.Item) {
                ConfigureItemPointerSphereCollider(targetTransform);
                actor.PointerTargetShapeConfigured = true;
                return;
            }

            ConfigureActorPointerSpriteShape(actor, targetTransform);
        }

        private void ConfigureItemPointerSphereCollider(Transform targetTransform) {
            DestroyPointerCollider<BoxCollider>(targetTransform);
            DestroyPointerCollider<CapsuleCollider>(targetTransform);
            DestroyPointerCollider<MeshCollider>(targetTransform);
            DestroyPointerTargetShape(targetTransform);
            DestroyPointerBoundsCollider(targetTransform);

            SphereCollider sphere = targetTransform.FindComponent<SphereCollider>();
            if (sphere == null) {
                sphere = targetTransform.gameObject.AddComponent<SphereCollider>();
            }

            sphere.isTrigger = true;
            sphere.radius = 0.35f;
            sphere.center = Vector3.up * 0.18f;
        }

        private void ConfigureActorPointerSpriteShape(PresentedActor actor, Transform targetTransform) {
            DestroyPointerCollider<SphereCollider>(targetTransform);
            DestroyPointerCollider<CapsuleCollider>(targetTransform);
            DestroyPointerCollider<MeshCollider>(targetTransform);

            RoSpritePointerTargetShape shape = targetTransform.FindComponent<RoSpritePointerTargetShape>();
            if (shape == null) {
                shape = targetTransform.gameObject.AddComponent<RoSpritePointerTargetShape>();
            }

            Transform boundsRoot = actor.Visual != null ? actor.Visual.transform : actor.Root.transform;
            shape.Configure(boundsRoot, PointerTargetSpriteMarginPixels);

            RoSpritePointerBoundsCollider boundsCollider = targetTransform.FindComponent<RoSpritePointerBoundsCollider>();
            if (boundsCollider == null) {
                boundsCollider = targetTransform.gameObject.AddComponent<RoSpritePointerBoundsCollider>();
            }

            boundsCollider.Configure(boundsRoot);
            actor.PointerTargetShapeConfigured = true;
        }

        private void RefreshActorResourceBarsIfDirty(PresentedActor actor, RoOnlineActorSnapshot snapshot, bool force = false) {
            if (actor?.Root == null) {
                return;
            }

            // 0xbd43d0: the monster gauge is hidden while the actor is hidden (0x02), cloaked (0x04)
            // or invisible (0x40) (0xd00920 / 0xd00a90 / 0xd00be0 on its effect state +0x2c0); /monsterhp and F11 are read at
            // every draw (RoClassicActorGauges.MonsterGaugesHidden)
            bool shouldShow = ShouldShowPresentedActorResourceBar(snapshot) &&
                (!IsMonsterLikeForResourceBar(snapshot.ObjectType) ||
                 !HidesMonsterGauge(snapshot.EffectState));
            int hp = shouldShow ? Mathf.Clamp(snapshot.Hp, 0, snapshot.MaxHp) : -1;
            int maxHp = shouldShow ? snapshot.MaxHp : 0;
            UnityEngine.Camera camera = shouldShow ? ResolveSortingCamera() : null;
            if (!force &&
                actor.HasResourceBarState &&
                actor.LastResourceBarShouldShow == shouldShow &&
                actor.LastResourceBarHp == hp &&
                actor.LastResourceBarMaxHp == maxHp &&
                actor.LastResourceBarCamera == camera) {
                return;
            }

            RefreshActorResourceBars(actor, shouldShow, hp, maxHp, camera, IsMonsterLikeForResourceBar(snapshot.ObjectType));
        }

        private void RefreshActorResourceBars(PresentedActor actor, bool shouldShow, int hp, int maxHp, UnityEngine.Camera camera, bool monster) {
            using (ResourceBarUpdateMarker.Auto()) {
                if (actor?.Root == null) {
                    return;
                }

                if (!shouldShow) {
                    ClearActorResourceBars(actor);
                    return;
                }

                RoActorResourceBarOverlay overlay = actor.ResourceBars;
                if (overlay == null) {
                    overlay = actor.Root.FindComponent<RoActorResourceBarOverlay>();
                    if (overlay == null) {
                        overlay = actor.Root.AddComponent<RoActorResourceBarOverlay>();
                    }

                    actor.ResourceBars = overlay;
                }

                overlay.Initialize(camera);
                overlay.ClassicMonsterGauge = monster;
                overlay.SetValues(hp, maxHp, -1, 0);
                actor.HasResourceBarState = true;
                actor.LastResourceBarShouldShow = true;
                actor.LastResourceBarHp = hp;
                actor.LastResourceBarMaxHp = maxHp;
                actor.LastResourceBarCamera = camera;
            }
        }

        private static void ClearActorResourceBars(PresentedActor actor) {
            if (actor?.ResourceBars != null) {
                actor.ResourceBars.ClearValues();
            }

            if (actor != null) {
                actor.HasResourceBarState = true;
                actor.LastResourceBarShouldShow = false;
                actor.LastResourceBarHp = -1;
                actor.LastResourceBarMaxHp = 0;
                actor.LastResourceBarCamera = null;
            }
        }

        private static void DestroyPointerCollider<TCollider>(Transform targetTransform) where TCollider : Collider {
            TCollider collider = targetTransform != null ? targetTransform.FindComponent<TCollider>() : null;
            if (collider != null) {
                DestroyRuntimeObject(collider);
            }
        }

        private static void DestroyPointerBoundsCollider(Transform targetTransform) {
            RoSpritePointerBoundsCollider boundsCollider = targetTransform != null
                ? targetTransform.FindComponent<RoSpritePointerBoundsCollider>()
                : null;
            if (boundsCollider != null) {
                boundsCollider.ClearGeneratedColliders();
                DestroyRuntimeObject(boundsCollider);
            }
        }

        private static void DestroyPointerTargetShape(Transform targetTransform) {
            RoSpritePointerTargetShape shape = targetTransform != null
                ? targetTransform.FindComponent<RoSpritePointerTargetShape>()
                : null;
            if (shape != null) {
                DestroyRuntimeObject(shape);
            }
        }

        private static void SetPointerTargetActive(PresentedActor actor, bool active) {
            if (actor?.PointerMarker != null) {
                actor.PointerMarker.gameObject.SetActive(active);
            } else if (actor?.OnlinePointerMarker != null) {
                actor.OnlinePointerMarker.gameObject.SetActive(active);
            }
        }

        private bool ShouldEnablePointerTarget(RoOnlineActorSnapshot snapshot) {
            if (!ResolveStatusLook(snapshot.ServerId).IsDrawn) {
                return false;
            }

            return !RoOnlineActorInteractionRules.IsDead(snapshot) ||
                snapshot.ObjectType == RoMapActorObjectType.Player;
        }

        private static void SetActorShadowActive(PresentedActor actor, bool active) {
            if (actor?.Root == null) {
                return;
            }

            Transform shadow = actor.Root.transform.Find("RO Online Actor Shadow");
            if (shadow != null) {
                shadow.gameObject.SetActive(active);
            }
        }

        private static void DestroyActorShadow(PresentedActor actor) {
            if (actor?.Root == null) {
                return;
            }

            Transform shadow = actor.Root.transform.Find("RO Online Actor Shadow");
            if (shadow == null) {
                return;
            }

            shadow.gameObject.SetActive(false);
            DestroyRuntimeObject(shadow.gameObject);
        }

        private Vector3 ResolveActorWorldPosition(RoOnlineActorSnapshot actor, PresentedActor presentedActor, float nowSeconds) {
            if (interpolateMovement &&
                TryResolveInterpolatedMovePosition(actor, presentedActor, nowSeconds, out Vector3 interpolatedPosition)) {
                presentedActor.ClearInterruptedMoveActionVisualState();
                presentedActor.HasPosition = true;
                return interpolatedPosition;
            }

            if (presentedActor.HasPosition && ShouldHoldCurrentVisualPosition(actor)) {
                TrackInterruptedMoveActionVisualHold(actor, presentedActor);
                presentedActor.ClearMove();
                return presentedActor.Root.transform.position;
            }

            // Frost Diver / Stone Curse cut a walk short. The presented sprite is latency-compensated
            // and sits slightly ahead of the authoritative stop cell, so a hard snap would yank it back
            // a cell and then jump forward again when ZC_STOPMOVE confirms the exact cell a moment
            // later. Reuse the interrupted-action settle so it glides to the cell instead; it also
            // re-targets when the authoritative cell is corrected, smoothing the ZC_STOPMOVE step too.
            if (presentedActor.HasPosition &&
                actor.IsImmobilizedByBodyState &&
                HasVisualGapToDisplayCell(actor, presentedActor)) {
                presentedActor.HasInterruptedMoveActionVisualHold = true;
                presentedActor.ClearMove();
            }

            if (presentedActor.HasPosition &&
                TryResolveInterruptedMoveActionSettlePosition(actor, presentedActor, nowSeconds, out Vector3 settlePosition)) {
                return settlePosition;
            }

            presentedActor.ClearMove();
            presentedActor.ClearInterruptedMoveActionVisualState();
            Vector2Int cell = GetDisplayCell(actor);
            Vector3 position = ResolveCellWorldPosition(cell, ShouldUseWalkableGridPosition(actor));
            if (actor.ObjectType == RoMapActorObjectType.Item) {
                position += new Vector3(actor.ItemCellOffsetX, 0f, actor.ItemCellOffsetY);
            }

            presentedActor.HasPosition = true;
            return position;
        }

        // True when a frozen/stone actor's sprite is still visibly away from its authoritative cell,
        // i.e. there is a gap worth gliding across rather than hard-snapping. Once settled onto the
        // cell the gap collapses to ~0, so the settle is not re-armed every frame.
        private bool HasVisualGapToDisplayCell(RoOnlineActorSnapshot actor, PresentedActor presentedActor) {
            if (presentedActor?.Root == null) {
                return false;
            }

            Vector2Int cell = GetDisplayCell(actor);
            Vector3 cellWorld = ResolveCellWorldPosition(cell, ShouldUseWalkableGridPosition(actor));
            return RoMovePresentation.FlatDistance(presentedActor.Root.transform.position, cellWorld) > 0.01f;
        }

        private static bool ShouldHoldCurrentVisualPosition(RoOnlineActorSnapshot actor) {
            return actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.IsPlayingDead ||
                actor.Motion == RoOnlineActorMotion.Dead ||
                actor.Motion == RoOnlineActorMotion.Pickup ||
                actor.Motion == RoOnlineActorMotion.Attack ||
                actor.Motion == RoOnlineActorMotion.Attack2 ||
                actor.Motion == RoOnlineActorMotion.Attack3 ||
                actor.Motion == RoOnlineActorMotion.Throwing ||
                actor.Motion == RoOnlineActorMotion.Skill ||
                actor.Motion == RoOnlineActorMotion.Casting ||
                actor.Motion == RoOnlineActorMotion.Hit;
        }

        private void TrackInterruptedMoveActionVisualHold(RoOnlineActorSnapshot actor, PresentedActor presentedActor) {
            if (presentedActor == null ||
                !presentedActor.HasMove ||
                !IsFiniteActionMotion(actor.Motion)) {
                return;
            }

            if (presentedActor.HasInterruptedMoveActionVisualHold &&
                presentedActor.InterruptedMoveActionRevision == actor.ActionRevision) {
                return;
            }

            Vector3 visualWorldPosition = presentedActor.Root != null
                ? presentedActor.Root.transform.position
                : presentedActor.MoveStartWorld;
            presentedActor.HasInterruptedMoveActionVisualHold = true;
            presentedActor.HasInterruptedMoveActionSettle = false;
            presentedActor.InterruptedMoveActionRevision = actor.ActionRevision;
            RecordMovementTrace(
                RoMovementTraceEventType.ActionInterrupt,
                actor,
                presentedActor,
                visualWorldPosition,
                presentedActor.LastMoveServerElapsedSeconds,
                presentedActor.MovePresentedElapsedSeconds,
                presentedActor.LastMoveCorrectionMode,
                presentedActor.LastMoveDistanceToAuthoritative,
                presentedActor.LastMoveTraceSegmentIndex,
                ResolveFacingDirection(actor),
                ResolveMotion(actor),
                $"remote action interrupt hold motion={actor.Motion}");
        }

        private bool TryResolveInterruptedMoveActionSettlePosition(
            RoOnlineActorSnapshot actor,
            PresentedActor presentedActor,
            float nowSeconds,
            out Vector3 position
        ) {
            position = default;
            if (presentedActor == null || !presentedActor.HasInterruptedMoveActionVisualHold) {
                return false;
            }

            if (actor.Motion == RoOnlineActorMotion.Moving && actor.HasMovePath) {
                presentedActor.ClearInterruptedMoveActionVisualState();
                return false;
            }

            if (IsFiniteActionMotion(actor.Motion) ||
                actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.IsPlayingDead ||
                actor.Motion == RoOnlineActorMotion.Dead) {
                return false;
            }

            Vector2Int cell = GetDisplayCell(actor);
            Vector3 targetWorld = ResolveCellWorldPosition(cell, ShouldUseWalkableGridPosition(actor));
            if (actor.ObjectType == RoMapActorObjectType.Item) {
                targetWorld += new Vector3(actor.ItemCellOffsetX, 0f, actor.ItemCellOffsetY);
            }

            Vector3 currentWorld = presentedActor.Root != null
                ? presentedActor.Root.transform.position
                : targetWorld;
            if (!presentedActor.HasInterruptedMoveActionSettle ||
                RoMovePresentation.FlatDistance(presentedActor.InterruptedMoveActionSettleTargetWorld, targetWorld) > 0.001f) {
                presentedActor.HasInterruptedMoveActionSettle = true;
                presentedActor.InterruptedMoveActionSettleStartWorld = currentWorld;
                presentedActor.InterruptedMoveActionSettleTargetWorld = targetWorld;
                presentedActor.InterruptedMoveActionSettleStartLocalTime = nowSeconds;
                presentedActor.InterruptedMoveActionSettleDurationSeconds = Mathf.Max(0f, MovementTuning.StopSettleSeconds);
                RecordMovementTrace(
                    RoMovementTraceEventType.SettleCorrection,
                    actor,
                    presentedActor,
                    currentWorld,
                    RoMovementTraceRecord.UnsetFloat,
                    RoMovementTraceRecord.UnsetFloat,
                    RoMovePresentationCorrectionMode.SoftCorrection,
                    RoMovePresentation.FlatDistance(currentWorld, targetWorld),
                    RoMovementTraceRecord.UnsetInt,
                    ResolveFacingDirection(actor),
                    ResolveMotion(actor),
                    $"remote action interrupt settle target={cell.x},{cell.y}");
            }

            float duration = presentedActor.InterruptedMoveActionSettleDurationSeconds;
            if (duration <= 0f) {
                presentedActor.ClearInterruptedMoveActionVisualState();
                position = targetWorld;
                return true;
            }

            float t = Mathf.Clamp01((nowSeconds - presentedActor.InterruptedMoveActionSettleStartLocalTime) / duration);
            position = Vector3.Lerp(
                presentedActor.InterruptedMoveActionSettleStartWorld,
                presentedActor.InterruptedMoveActionSettleTargetWorld,
                t);
            if (t >= 1f) {
                presentedActor.ClearInterruptedMoveActionVisualState();
            }

            presentedActor.HasPosition = true;
            return true;
        }

        private Vector3 ResolveCellWorldPosition(Vector2Int cell, bool preferWalkableCell) {
            if (preferWalkableCell && TryResolveWalkableCell(cell, out Vector2Int walkableCell)) {
                return gridSelector.GetCellWorldCenter(walkableCell, 0f);
            }

            if (gridSelector != null && IsGridCellInBounds(cell)) {
                return gridSelector.GetCellWorldCenter(cell, 0f);
            }

            return new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
        }

        private bool TryResolveInterpolatedMovePosition(
            RoOnlineActorSnapshot actor,
            PresentedActor presentedActor,
            float nowSeconds,
            out Vector3 position
        ) {
            if (actor.Motion != RoOnlineActorMotion.Moving || !actor.HasMovePath) {
                position = default;
                return false;
            }

            RoOnlineMovePresentationKey moveKey = RoOnlineMovePresentationKey.From(actor);
            if (!presentedActor.HasMove || !presentedActor.MoveKey.Equals(moveKey)) {
                BeginMovePresentation(actor, presentedActor, moveKey, nowSeconds);
            }

            float elapsedSeconds = ResolveMoveElapsedSeconds(actor, presentedActor, nowSeconds);
            RoMovementSample sample = SamplePresentedMove(presentedActor, elapsedSeconds);
            position = sample.Position;
            presentedActor.HasPosition = true;
            RecordMovePresentationTrace(actor, presentedActor, position, elapsedSeconds, nowSeconds);
            return true;
        }

        private static RoMovementSample SamplePresentedMove(PresentedActor presentedActor, float elapsedSeconds) {
            if (presentedActor != null &&
                presentedActor.MoveWaypoints.Count > 1 &&
                presentedActor.MoveSegmentEndTimes.Count > 0 &&
                RoMovementSampler.TrySample(
                    presentedActor.MoveWaypoints,
                    presentedActor.MoveSegmentEndTimes,
                    elapsedSeconds,
                    out RoMovementSample timedSample)) {
                return timedSample;
            }

            if (presentedActor != null &&
                presentedActor.MoveWaypoints.Count > 1 &&
                RoMovementSampler.TrySampleByDuration(
                    presentedActor.MoveWaypoints,
                    elapsedSeconds,
                    presentedActor.MoveDurationSeconds,
                    presentedActor.MovePathDistance,
                    out RoMovementSample routeSample)) {
                return routeSample;
            }

            return presentedActor != null
                ? RoMovementSampler.SampleLinear(
                    presentedActor.MoveStartWorld,
                    presentedActor.MoveEndWorld,
                    elapsedSeconds,
                    presentedActor.MoveDurationSeconds)
                : default;
        }

        private void BeginMovePresentation(
            RoOnlineActorSnapshot actor,
            PresentedActor presentedActor,
            RoOnlineMovePresentationKey moveKey,
            float nowSeconds
        ) {
            var startCell = new Vector2Int(actor.MovePath.StartX, actor.MovePath.StartY);
            var endCell = new Vector2Int(actor.MovePath.EndX, actor.MovePath.EndY);
            bool useWalkableGridPosition = ShouldUseWalkableGridPosition(actor);
            Vector3 startWorld = ResolveCellWorldPosition(startCell, useWalkableGridPosition);
            Vector3 endWorld = ResolveCellWorldPosition(endCell, useWalkableGridPosition);
            bool startFromCurrentVisual = presentedActor.HasPosition;
            presentedActor.MoveKey = moveKey;
            presentedActor.MoveStartWorld = startFromCurrentVisual ? presentedActor.Root.transform.position : startWorld;
            presentedActor.MoveEndWorld = endWorld;
            BuildMoveWaypoints(startCell, endCell, actor, presentedActor);
            float serverElapsedSeconds = 0f;
            if (worldController != null) {
                worldController.TryCalculateMoveElapsedSeconds(actor.MoveStartTime, nowSeconds, out serverElapsedSeconds);
            }

            float moveDurationSeconds = presentedActor.MoveSegmentEndTimes.Count > 0
                ? presentedActor.MoveSegmentEndTimes[^1]
                : CalculateMoveDurationSeconds(
                    actor,
                    ResolveConfiguredStraightStepMilliseconds(),
                    ResolveConfiguredDiagonalStepMultiplier());
            serverElapsedSeconds = ClampRemoteMoveFastForwardSeconds(actor, serverElapsedSeconds, moveDurationSeconds, MovementTuning);
            presentedActor.MoveTotalDurationSeconds = moveDurationSeconds;
            presentedActor.MovePresentedElapsedSeconds = serverElapsedSeconds;
            presentedActor.MoveLastPresentationLocalTime = nowSeconds;
            presentedActor.MoveDurationSeconds = CalculateMovePresentationDuration(
                moveDurationSeconds,
                serverElapsedSeconds,
                startFromCurrentVisual);
            presentedActor.MoveStartLocalTime = CalculateMovePresentationStartLocalTime(
                nowSeconds,
                moveDurationSeconds,
                serverElapsedSeconds,
                startFromCurrentVisual);
            presentedActor.HasMove = true;
            presentedActor.LastMoveServerElapsedSeconds = serverElapsedSeconds;
            presentedActor.LastMoveCorrectionMode = RoMovePresentationCorrectionMode.None;
            presentedActor.LastMoveDistanceToAuthoritative = 0f;
            presentedActor.LastMoveTraceSegmentIndex = -1;
            BeginPresentedWalkDistance(presentedActor);
            // the detail text only while a trace records (it was built for every move start of every walker)
            if (worldController != null && worldController.IsMovementTraceActive) {
                RecordMovementTrace(
                    RoMovementTraceEventType.VisualMoveStarted,
                    actor,
                    presentedActor,
                    presentedActor.MoveStartWorld,
                    serverElapsedSeconds,
                    serverElapsedSeconds,
                    RoMovePresentationCorrectionMode.None,
                    0f,
                    0,
                    ResolveFacingDirection(actor),
                    RoSpriteMotion.Walk,
                    $"remote visual move started {MovementTuning.ToTraceSummary()}");
            }
        }

        private static void BeginPresentedWalkDistance(PresentedActor actor) {
            if (actor == null) {
                return;
            }

            // the walk distance carries over from earlier walks (the client never resets it): the step continues
            actor.LastWalkDistanceWorldPosition = actor.MoveStartWorld;
            actor.HasWalkDistancePosition = true;
        }

        private bool TryResolveWalkableCell(Vector2Int requestedCell, out Vector2Int resolvedCell) {
            if (gridSelector == null || gridSelector.Altitude == null) {
                resolvedCell = default;
                return false;
            }

            if (gridSelector.IsCellWalkable(requestedCell)) {
                resolvedCell = requestedCell;
                return true;
            }

            return RoGridPathfinder.TryFindNearestWalkable(gridSelector.Altitude, requestedCell, 8, out resolvedCell);
        }

        private bool IsGridCellInBounds(Vector2Int cell) {
            if (gridSelector == null || gridSelector.Altitude == null) {
                return false;
            }

            return cell.x >= 0 &&
                cell.y >= 0 &&
                cell.x < gridSelector.Altitude.width &&
                cell.y < gridSelector.Altitude.height;
        }

        private void BuildMoveWaypoints(
            Vector2Int requestedStartCell,
            Vector2Int requestedEndCell,
            RoOnlineActorSnapshot actor,
            PresentedActor presentedActor
        ) {
            presentedActor.MoveWaypoints.Clear();
            presentedActor.MoveSegmentEndTimes.Clear();
            presentedActor.MovePathDistance = 0f;

            if (!ShouldUseWalkableGridPosition(actor) ||
                gridSelector == null ||
                gridSelector.Altitude == null ||
                !TryResolveWalkableCell(requestedStartCell, out Vector2Int startCell) ||
                !TryResolveWalkableCell(requestedEndCell, out Vector2Int endCell)) {
                return;
            }

            movementCellPathBuffer.Clear();
            if (!RoGridPathfinder.TryFindPath(
                    gridSelector.Altitude,
                    startCell,
                    endCell,
                    movementCellPathBuffer,
                    maxSteps: MovementTuning.MaxWalkPathSteps) ||
                movementCellPathBuffer.Count == 0) {
                return;
            }

            AppendMoveWaypoint(presentedActor.MoveWaypoints, presentedActor.MoveStartWorld);
            float elapsedSeconds = 0f;
            float straightStepMilliseconds = actor.Speed > 0
                ? actor.Speed
                : ResolveConfiguredStraightStepMilliseconds();

            for (int i = 1; i < movementCellPathBuffer.Count; i++) {
                Vector3 previousWorld = presentedActor.MoveWaypoints[^1];
                Vector3 nextWorld = gridSelector.GetCellWorldCenter(movementCellPathBuffer[i], 0f);
                AppendMoveWaypoint(presentedActor.MoveWaypoints, gridSelector.GetCellWorldCenter(movementCellPathBuffer[i], 0f));
                float segmentSeconds = i == 1
                    ? RoLiveMoveRoute.CalculatePartialFirstStepSeconds(
                        previousWorld,
                        nextWorld,
                        movementCellPathBuffer[i - 1],
                        movementCellPathBuffer[i],
                        straightStepMilliseconds)
                    : RoLiveMoveRoute.CalculateServerStepSeconds(
                        movementCellPathBuffer[i - 1],
                        movementCellPathBuffer[i],
                        straightStepMilliseconds);
                elapsedSeconds += segmentSeconds;
                presentedActor.MoveSegmentEndTimes.Add(elapsedSeconds);
            }

            presentedActor.MovePathDistance = CalculateMovePathDistance(presentedActor.MoveWaypoints);
        }

        private int RemoveUnseenActors(HashSet<uint> seen) {
            // actors that left before their sprites were ready: their waits go too
            if (pendingSpritePrefetches.Count > 0) {
                removeServerIds.Clear();
                foreach (uint serverId in pendingSpritePrefetches.Keys) {
                    if (!seen.Contains(serverId)) {
                        removeServerIds.Add(serverId);
                    }
                }

                for (int i = 0; i < removeServerIds.Count; i++) {
                    ReleasePendingSpritePrefetch(removeServerIds[i]);
                    spritePrefetchRequested.Remove(removeServerIds[i]);
                }
            }

            if (actors.Count == 0) {
                return 0;
            }

            float nowSeconds = RoPresentationClock.NowSeconds;
            removeServerIds.Clear();
            foreach (uint serverId in actors.Keys) {
                if (!seen.Contains(serverId)) {
                    if (actors.TryGetValue(serverId, out PresentedActor actor) &&
                        actor.PendingDeathRelease &&
                        nowSeconds < actor.DeathReleaseLocalTime) {
                        ApplyDeathFade(actor, nowSeconds);
                        continue;
                    }

                    removeServerIds.Add(serverId);
                }
            }

            int removed = 0;
            for (int i = 0; i < removeServerIds.Count; i++) {
                removed += FadeOrReleasePresentedActor(removeServerIds[i], nowSeconds);
            }

            return removed;
        }

        private int FadeOrReleasePresentedActor(uint serverId, float nowSeconds) {
            if (!actors.TryGetValue(serverId, out PresentedActor actor)) {
                return 0;
            }

            if (actor.PendingDeathRelease && nowSeconds < actor.DeathReleaseLocalTime) {
                ApplyDeathFade(actor, nowSeconds);
                return 0;
            }

            // ~CGameActor: the effects launched on the actor or following it end with it (a pooled root kept them alive,
            // and a reused root carried them over to the next actor); before the release, while the root is still its
            Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance?.ForgetActor(serverId, actor.Root != null ? actor.Root.transform : null);
            ReleasePresentedActor(actor);
            actors.Remove(serverId);
            return 1;
        }

        private static float ResolveDeathAnimationSeconds(PresentedActor actor) {
            if (actor?.Visual != null &&
                actor.Visual.TryGetCurrentActionDurationSeconds(out float actionSeconds) &&
                actionSeconds > 0f) {
                return actionSeconds;
            }

            return DefaultDeathPresentationSeconds;
        }

        private static void ApplyDeathFade(PresentedActor actor, float nowSeconds) {
            if (actor == null || !actor.PendingDeathRelease) {
                return;
            }

            float opacity = CalculateDeathFadeOpacity(nowSeconds, actor.DeathFadeStartLocalTime);
            actor.Visual?.SetOpacity(opacity);
            actor.LastOpacity = opacity;
            actor.HasVisualState = true;
        }

        private void BeginActorBuildBudget(bool allowBuildBudget, bool fullSync) {
            newActorBuildBudget = allowBuildBudget ? MaxNewActorBuildsPerFrame : int.MaxValue;
            // only a full sync sees every actor, so only it can say no deferred build is left: an incremental sync of a few
            // actors (the drain's, every frame in a crowded town) cleared the flag and the actors left over from a crowd
            // arrival were built only when they next changed or at the idle full sync, seconds later
            if (fullSync) {
                hasDeferredActorBuilds = false;
            }
        }

        // True when this snapshot would trigger a brand-new actor visual build and the per-frame build
        // budget is spent. Only brand-new actors (not yet presented) are ever deferred, and never the
        // local player; existing actors fall through to their normal same-frame position/animation update.
        private bool ShouldDeferNewActorBuild(RoOnlineActorSnapshot snapshot) {
            if (newActorBuildBudget > 0 || snapshot.IsSelf) {
                return false;
            }

            return !actors.TryGetValue(snapshot.ServerId, out PresentedActor actor) || actor.Root == null;
        }

        private static int fullSyncRequests;
        private int seenFullSyncRequests;

        /// <summary>Every presenter re-syncs every actor next frame (RoClientDrawOrder going off: the screen-y order again).</summary>
        public static void RequestFullSync() => fullSyncRequests++;

        private bool RequiresFullFrameSync() {
            if (worldController == null) {
                return true;
            }

            if (seenFullSyncRequests != fullSyncRequests) {
                seenFullSyncRequests = fullSyncRequests;
                return true;
            }

            // Keep draining budgeted new-actor builds over subsequent frames until none remain.
            if (hasDeferredActorBuilds) {
                return true;
            }

            if (HasPendingPresentationRevisionSync()) {
                return true;
            }

            if (actors.Count == 0) {
                return worldController.ActorCount > 0;
            }

            return !SortOnlyOnCameraMoveForDiagnostics && HasSortingCameraChanged();
        }

        /// <summary>Diagnostics (!toggle sortonly): false = a camera move re-syncs every actor, as before.</summary>
        public static bool SortOnlyOnCameraMoveForDiagnostics = true;

        /// <summary>
        /// The camera moved or turned and nothing else changed: only the actors' depth order follows it — no full sync of
        /// every actor (the camera follows the own character, so this ran for the whole crowd every frame of a walk).
        /// </summary>
        private void UpdateSortingForCamera() {
            UnityEngine.Camera camera = ResolveSortingCamera();
            using (SortingUpdateMarker.Auto()) {
                foreach (KeyValuePair<uint, PresentedActor> pair in actors) {
                    PresentedActor actor = pair.Value;
                    if (actor?.Visual == null || !actor.HasRenderState) {
                        continue;
                    }

                    int sortingOrder = CalculateActorSortingOrder(actor.LastSortingWorldPosition, camera, sortingOrderBase, sortingBucketCount, pair.Key);
                    if (actor.LastSortingOrder != sortingOrder) {
                        actor.Visual.SetSortingOrderBase(sortingOrder);
                        actor.LastSortingOrder = sortingOrder;
                    }

                    actor.LastSortingCameraRevision = sortingCameraRevision;
                    actor.LastSortingCamera = camera;
                }
            }

            RecordSortingCameraState();
        }

        private bool HasPendingPresentationRevisionSync() {
            return syncPresentationRevisionImmediately &&
                worldController != null &&
                worldController.PresentationRevision != lastIdleFullSyncPresentationRevision;
        }

        private bool TryCollectActivePresentationActorIds(List<uint> actorIds) {
            actorIds.Clear();
            if (actors.Count == 0) {
                return false;
            }

            foreach (KeyValuePair<uint, PresentedActor> pair in actors) {
                PresentedActor actor = pair.Value;
                if (!RequiresActivePresentationSync(actor)) {
                    continue;
                }

                actorIds.Add(pair.Key);
            }

            return actorIds.Count > 0;
        }

        private static bool RequiresActivePresentationSync(PresentedActor actor) {
            if (actor == null) {
                return false;
            }

            if (actor.PendingDeathRelease || actor.HasMove || IsFinitePresentationMotion(actor.LastMotion)) {
                return true;
            }

            return false;
        }

        private bool HasSortingCameraChanged() {
            if (!useDynamicSortingOrder) {
                return false;
            }

            UnityEngine.Camera camera = ResolveSortingCamera();
            if (camera == null) {
                hasSortingCameraTransformState = false;
                return false;
            }

            Transform cameraTransform = camera.transform;
            Vector3 position = cameraTransform.position;
            Quaternion rotation = cameraTransform.rotation;
            if (!hasSortingCameraTransformState) {
                lastSortingCameraPosition = position;
                lastSortingCameraRotation = rotation;
                hasSortingCameraTransformState = true;
                return false;
            }

            bool changed = Vector3.SqrMagnitude(position - lastSortingCameraPosition) > SortingCameraPositionEpsilonSqr ||
                Quaternion.Angle(rotation, lastSortingCameraRotation) > SortingCameraRotationEpsilonDegrees;
            if (changed) {
                sortingCameraRevision++;
            }

            return changed;
        }

        private void RecordSortingCameraState() {
            if (!useDynamicSortingOrder) {
                hasSortingCameraTransformState = false;
                return;
            }

            UnityEngine.Camera camera = ResolveSortingCamera();
            if (camera == null) {
                hasSortingCameraTransformState = false;
                return;
            }

            Transform cameraTransform = camera.transform;
            lastSortingCameraPosition = cameraTransform.position;
            lastSortingCameraRotation = cameraTransform.rotation;
            hasSortingCameraTransformState = true;
        }

        private static bool IsFinitePresentationMotion(RoSpriteMotion motion) {
            switch (motion) {
                case RoSpriteMotion.PickUp:
                case RoSpriteMotion.Attack1:
                case RoSpriteMotion.Attack2:
                case RoSpriteMotion.Attack3:
                case RoSpriteMotion.Hit:
                case RoSpriteMotion.Casting:
                case RoSpriteMotion.Special:
                case RoSpriteMotion.Throwing:
                    return true;
                default:
                    return false;
            }
        }

        private bool ShouldRetryAssetVisual(PresentedActor actor) {
            return actor.Visual == null &&
                !actor.TriedAssetVisual &&
                assets != null &&
                assets.IsInitialized;
        }

        private PresentedActor AcquirePresentedActor() {
            EnsureActorRoot();
            PresentedActor actor = reusePresentedActorObjects && pooledActors.Count > 0
                ? pooledActors.Pop()
                : CreatePresentedActor();

            actor.ResetTransientState();
            actor.Root.transform.SetParent(actorRoot, false);
            actor.Root.SetActive(true);
            RoActorEffectAnchors.EnsureOn(actor.Root);
            return actor;
        }

        private PresentedActor CreatePresentedActor() {
            var root = new GameObject("RO Online Actor");
            return new PresentedActor(root);
        }

        private void ReleasePresentedActor(PresentedActor actor) {
            if (actor?.Root == null) {
                return;
            }

            ClearActorCastOverlay(actor);
            ClearMaxLevelAura(actor);
            actor.ResetTransientState();
            int poolLimit = Mathf.Max(0, maxPooledPresentedActors);
            if (reusePresentedActorObjects && pooledActors.Count < poolLimit) {
                EnsurePooledActorRoot();
                actor.Root.SetActive(false);
                actor.Root.transform.SetParent(pooledActorRoot, false);
                pooledActors.Push(actor);
                return;
            }

            DestroyRuntimeObject(actor.Root);
        }

        private static void ClearActorCastOverlay(PresentedActor actor) {
            if (actor?.Root == null) {
                return;
            }

            RoActorCastBarOverlay overlay = actor.CastBar;
            if (overlay == null) {
                overlay = actor.Root.FindComponent<RoActorCastBarOverlay>();
                actor.CastBar = overlay;
            }

            overlay?.ClearAll();
        }

        private void DestroyActivePresentedActors() {
            foreach (PresentedActor actor in actors.Values) {
                ClearMaxLevelAura(actor);
                if (actor?.Root != null) {
                    DestroyRentalCompanion(ref actor.FalconCompanion);
                    DestroyRuntimeObject(actor.Root);
                }
            }

            actors.Clear();
        }

        private void ClearPooledPresentedActors() {
            while (pooledActors.Count > 0) {
                PresentedActor actor = pooledActors.Pop();
                if (actor?.Root != null) {
                    DestroyRentalCompanion(ref actor.FalconCompanion);
                    DestroyRuntimeObject(actor.Root);
                }
            }

            if (pooledActorRoot != null) {
                DestroyRuntimeObject(pooledActorRoot.gameObject);
                pooledActorRoot = null;
            }
        }

        private void MarkActiveActorVisualsDirty() {
            foreach (PresentedActor actor in actors.Values) {
                actor.HasBuiltPresentation = false;
            }
        }

        private void EnsureActorRoot() {
            if (actorRoot != null) {
                return;
            }

            var rootObject = new GameObject("RO Online Presented Actors");
            rootObject.transform.SetParent(transform, false);
            actorRoot = rootObject.transform;
        }

        private void EnsureRentalCompanionRoot() {
            if (rentalCompanionRoot != null) {
                return;
            }

            var rootObject = new GameObject("RO Online Rental Companions");
            rootObject.transform.SetParent(transform, false);
            rentalCompanionRoot = rootObject.transform;
        }

        private void EnsurePooledActorRoot() {
            if (pooledActorRoot != null) {
                return;
            }

            var rootObject = new GameObject("RO Online Presented Actor Pool");
            rootObject.transform.SetParent(transform, false);
            rootObject.SetActive(false);
            pooledActorRoot = rootObject.transform;
        }

        public static bool ShouldPresentActor(RoOnlineActorSnapshot actor, bool includeSelf) {
            if (actor.ServerId == 0) {
                return false;
            }

            if (actor.ObjectType == RoMapActorObjectType.Warp ||
                actor.Job == 45 ||
                actor.ObjectType == RoMapActorObjectType.Effect) {
                return false;
            }

            return includeSelf || !actor.IsSelf;
        }

        public static bool ShouldShowPresentedActorResourceBar(RoOnlineActorSnapshot actor) {
            if (actor.ServerId == 0 ||
                actor.IsSelf ||
                actor.MaxHp <= 0 ||
                actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.Motion == RoOnlineActorMotion.Dead) {
                return false;
            }

            if (actor.ObjectType == RoMapActorObjectType.Player) {
                return true;
            }

            if (IsMonsterLikeForResourceBar(actor.ObjectType)) {
                return Mathf.Clamp01(actor.Hp / (float) actor.MaxHp) < 0.999f;
            }

            return false;
        }

        /// <summary>0xbd43d0's effect state test: hidden (0x02), cloaked (0x04) or invisible (0x40) hide the monster gauge.</summary>
        public static bool HidesMonsterGauge(int effectState) => (effectState & 0x46) != 0;

        public static bool IsMonsterLikeForResourceBar(RoMapActorObjectType objectType) {
            return objectType == RoMapActorObjectType.Monster ||
                objectType == RoMapActorObjectType.Pet ||
                objectType == RoMapActorObjectType.Homunculus ||
                objectType == RoMapActorObjectType.Mercenary ||
                objectType == RoMapActorObjectType.Elemental;
        }

        public static RoActorAppearanceRequest BuildPlayerAppearanceRequest(RoCharacterInfo character) {
            if (character == null) {
                return null;
            }

            return BuildPlayerAppearanceRequest(
                serverId: character.CharacterId,
                baseClassId: character.JobId,
                classId: character.JobId,
                bodyStyleId: character.Body,
                isMainCharacter: true,
                isMale: character.Sex != 0,
                head: character.Head,
                headPalette: character.HeadPalette,
                bodyPalette: character.BodyPalette,
                weapon: ToUnsignedAppearance(character.Weapon),
                shield: ToUnsignedAppearance(character.Shield),
                accessory: character.Accessory,
                accessory2: character.Accessory2,
                accessory3: character.Accessory3,
                robe: 0,
                motion: RoSpriteMotion.Idle,
                facingDirection: RoActorDirection.South,
                headFacing: RoActorHeadFacing.Center);
        }

        public static RoActorAppearanceRequest BuildPlayerAppearanceRequest(RoOnlineActorSnapshot actor) {
            return new RoActorAppearanceRequest {
                kind = RoActorAppearanceKind.Player,
                serverId = unchecked((int) actor.ServerId),
                baseClassId = actor.BaseJob != 0 || actor.CostumeJob != 0 || actor.VisualJob != 0 ? actor.BaseJob : actor.Job,
                classId = actor.VisualJob != 0 ? actor.VisualJob : actor.Job,
                bodyStyleId = actor.Body,
                isMainCharacter = actor.IsSelf,
                isMale = actor.Sex != 0,
                headId = actor.Head,
                hairId = actor.Head,
                hairColorId = actor.HeadPalette,
                bodyPaletteId = actor.BodyPalette,
                weaponId = unchecked((int) actor.Weapon),
                shieldId = unchecked((int) actor.Shield),
                robeId = actor.Robe,
                headgearTopId = actor.Accessory2,
                headgearMidId = actor.Accessory3,
                headgearLowerId = actor.Accessory,
                motionProfile = RoSpriteMotionProfile.Player,
                motion = ResolveMotion(actor),
                facingDirection = ResolveFacingDirection(actor),
                headFacing = HeadFacingFromPacket(actor.HeadDirection),
                hasCart = actor.HasCart,
                cartNum = actor.CartNum
            };
        }

        private static RoActorAppearanceRequest BuildPlayerAppearanceRequest(
            int serverId,
            short baseClassId,
            short classId,
            int bodyStyleId,
            bool isMainCharacter,
            bool isMale,
            int head,
            int headPalette,
            int bodyPalette,
            uint weapon,
            uint shield,
            int accessory,
            int accessory2,
            int accessory3,
            int robe,
            RoSpriteMotion motion,
            RoActorDirection facingDirection,
            RoActorHeadFacing headFacing
        ) {
            int hairStyle = Mathf.Max(0, head);
            return NormalizePlayerAppearanceEquipment(new RoActorAppearanceRequest {
                kind = RoActorAppearanceKind.Player,
                serverId = serverId,
                baseClassId = baseClassId,
                classId = classId,
                bodyStyleId = bodyStyleId,
                isMainCharacter = isMainCharacter,
                isMale = isMale,
                headId = hairStyle,
                hairId = hairStyle,
                hairColorId = headPalette,
                bodyPaletteId = bodyPalette,
                weaponId = unchecked((int) weapon),
                shieldId = unchecked((int) shield),
                robeId = robe,
                headgearTopId = accessory2,
                headgearMidId = accessory3,
                headgearLowerId = accessory,
                motionProfile = RoSpriteMotionProfile.Player,
                motion = motion,
                facingDirection = facingDirection,
                headFacing = headFacing
            }, classId);
        }

        private static RoActorAppearanceRequest NormalizePlayerAppearanceEquipment(RoActorAppearanceRequest appearance, short jobId) {
            if (appearance == null) {
                return null;
            }

            RoMapPresentedEquipment equipment = RoMapWeaponActionSemantics.NormalizePresentationEquipment(
                RoMapActorObjectType.Player,
                jobId,
                ToUnsignedAppearance(appearance.weaponId),
                ToUnsignedAppearance(appearance.shieldId));
            appearance.weaponId = unchecked((int) equipment.Weapon);
            appearance.shieldId = unchecked((int) equipment.Shield);
            return appearance;
        }

        private static uint ToUnsignedAppearance(int value) {
            return value > 0 ? unchecked((uint) value) : 0u;
        }

        public static RoSpriteMotion ResolveMotion(RoOnlineActorSnapshot actor) {
            if (actor.Motion == RoOnlineActorMotion.Dead || actor.SpawnState == RoMapActorSpawnState.Dead) {
                return RoSpriteMotion.Dead;
            }

            if (actor.IsPlayingDead) {
                return RoSpriteMotion.Dead;
            }

            if (actor.Motion == RoOnlineActorMotion.Sitting) {
                return RoSpriteMotion.Sit;
            }

            switch (actor.Motion) {
                case RoOnlineActorMotion.Moving:
                    return RoSpriteMotion.Walk;
                case RoOnlineActorMotion.Pickup:
                    return RoSpriteMotion.PickUp;
                case RoOnlineActorMotion.Attack:
                    return RoSpriteMotion.Attack1;
                case RoOnlineActorMotion.Attack2:
                    return RoSpriteMotion.Attack2;
                case RoOnlineActorMotion.Attack3:
                    return RoSpriteMotion.Attack3;
                case RoOnlineActorMotion.Throwing:
                    return RoSpriteMotion.Throwing;
                case RoOnlineActorMotion.Skill:
                    return ResolveMotionProfile(actor) == RoSpriteMotionProfile.Player
                        ? RoSpriteMotion.Casting
                        : RoSpriteMotion.Special;
                case RoOnlineActorMotion.Hit:
                    return RoSpriteMotion.Hit;
                case RoOnlineActorMotion.Casting:
                    return RoSpriteMotion.Casting;
                case RoOnlineActorMotion.Standby:
                    return RoSpriteMotion.Standby;
                default:
                    return actor.SpawnState == RoMapActorSpawnState.Sitting
                        ? RoSpriteMotion.Sit
                        : RoSpriteMotion.Idle;
            }
        }

        private RoSpriteMotion ResolveMotion(RoOnlineActorSnapshot actor, PresentedActor presentedActor, float nowSeconds) {
            RoMotionStep step = ResolveMotionStep(actor, presentedActor, nowSeconds);
            if (presentedActor != null) {
                presentedActor.MotionStep = step;
            }

            return step.Motion;
        }

        /// <summary>
        /// What the actor shows now (RoOriginalCombatRules.ActorMotion, reference actormotion): the world action's motion
        /// played the original's way — speed from its attackMT, once or looping, then the ready pose or idle — the same for
        /// the own character and everyone else.
        /// </summary>
        /// <summary>Diagnostics (!layers): presented players whose body layer is missing, not drawn or not facing the camera.</summary>
        public string DescribeBrokenLayersForDiagnostics(int max) {
            var text = new System.Text.StringBuilder();
            int listed = 0, players = 0;
            foreach (KeyValuePair<uint, PresentedActor> pair in actors) {
                RoActorLayeredVisual visual = pair.Value?.Visual;
                if (visual == null || pair.Key < 3900000) {
                    continue;
                }

                players++;
                string layers = visual.DescribeLayers();
                bool bodyOk = layers.Contains("[Body on") && !layers.Contains("[Body on angle=9") && !layers.Contains("verts=0 ") && !layers.Contains("verts=-1");
                if (bodyOk || listed >= max) {
                    continue;
                }

                listed++;
                text.Append("\n  ").Append(pair.Key).Append(' ').Append(layers);
            }

            return players + " players, " + listed + " suspicious:" + text;
        }

        /// <summary>Diagnostics (!granny): the presented Granny models, their guild, renderers and emblem state.</summary>
        public string DescribeGrannyForDiagnostics() {
            var text = new System.Text.StringBuilder();
            int count = 0;
            foreach (KeyValuePair<uint, PresentedActor> pair in actors) {
                PresentedActor actor = pair.Value;
                if (actor?.Granny == null) {
                    if (actor?.Root != null && count < 1000) {
                        text.Append("\n  (sprite) ").Append(pair.Key).Append(' ').Append(actor.Root.name);
                    }

                    continue;
                }

                count++;
                Vector3 p = actor.Root.transform.position;
                text.Append("\n  ").Append(pair.Key).Append(' ').Append(actor.Granny.name)
                    .Append(" guild=").Append(actor.GrannyGuildId).Append(" emblemSerial=").Append(actor.GrannyEmblemSerial)
                    .Append(" renderers=").Append(actor.Granny.Renderers.Length)
                    .Append(" pos=").Append(p.x.ToString("F1")).Append(',').Append(p.z.ToString("F1"));
            }

            return count + " Granny models, " + actors.Count + " actors" + text;
        }

        /// <summary>Diagnostics (!garments): the layers of presented players that wear a garment (robe), walking ones first.</summary>
        public string DescribeGarmentLayersForDiagnostics(int max) {
            var text = new System.Text.StringBuilder();
            int listed = 0;
            for (int pass = 0; pass < 2; pass++) {
                foreach (KeyValuePair<uint, PresentedActor> pair in actors) {
                    RoActorLayeredVisual visual = pair.Value?.Visual;
                    if (visual == null || listed >= max || !visual.HasLayer(RoActorSpriteLayer.Garment)) {
                        continue;
                    }

                    bool walking = visual.Motion == RoSpriteMotion.Walk;
                    if (walking != (pass == 0)) {
                        continue;
                    }

                    listed++;
                    UnityEngine.Camera camera = UnityEngine.Camera.main;
                    Vector3 screen = camera != null ? camera.WorldToScreenPoint(visual.transform.position) : default;
                    text.Append("\n  ").Append(pair.Key).Append(walking ? " WALK " : " stand ")
                        .Append("face=").Append(visual.FacingDirection)
                        .Append(" screen=").Append(Mathf.RoundToInt(screen.x)).Append(',').Append(Mathf.RoundToInt(Screen.height - screen.y))
                        .Append(' ').Append(visual.DescribeLayers());
                }
            }

            return listed + " with a garment:" + text;
        }

        /// <summary>
        /// Diagnostics (!heads): presented players whose body does not show under the head — body renderer off, not
        /// visible or empty, or the head's screen rect not over the body's.
        /// </summary>
        public string DescribeHeadsWithoutBodyForDiagnostics(int max) {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            var text = new System.Text.StringBuilder();
            int listed = 0, players = 0;
            foreach (KeyValuePair<uint, PresentedActor> pair in actors) {
                RoActorLayeredVisual visual = pair.Value?.Visual;
                if (visual == null || camera == null) {
                    continue;
                }

                players++;
                MeshRenderer body = null, head = null;
                string bodyName = "-";
                for (int i = 0; visual.TryGetLayer(i, out RoActorSpriteLayer kind, out RoActorSpriteVisual layer); i++) {
                    if (layer == null) {
                        continue;
                    }

                    if (kind == RoActorSpriteLayer.Body) {
                        body = layer.GetComponent<MeshRenderer>();
                        bodyName = layer.gameObject.name;
                    } else if (kind == RoActorSpriteLayer.Head) {
                        head = layer.GetComponent<MeshRenderer>();
                    }
                }

                if (head == null || !head.enabled || !head.isVisible) {
                    continue;
                }

                Rect headRect = ScreenRect(camera, head.bounds);
                string problem = null;
                if (body == null) {
                    problem = "no body layer";
                } else if (!body.enabled || !body.gameObject.activeInHierarchy) {
                    problem = "body renderer off";
                } else if (!body.isVisible) {
                    problem = "body not visible";
                } else {
                    MeshFilter filter = body.GetComponent<MeshFilter>();
                    Rect bodyRect = ScreenRect(camera, body.bounds);
                    if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) {
                        problem = "body mesh empty";
                    } else if (bodyRect.height < 4f || headRect.yMin > bodyRect.yMax + 8f || headRect.yMax < bodyRect.yMin) {
                        problem = "head off body body=" + bodyRect + " head=" + headRect;
                    } else {
                        Material material = body.sharedMaterial;
                        Color color = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                        if (color.a < 0.05f) {
                            problem = "body transparent a=" + color.a;
                        }
                    }
                }

                if (problem == null || listed >= max) {
                    continue;
                }

                listed++;
                text.Append("\n  ").Append(pair.Key).Append(' ').Append(problem).Append(" | ").Append(bodyName).Append(" |").Append(visual.DescribeLayers());
            }

            return players + " players, " + listed + " heads without a body:" + text;
        }

        private static Rect ScreenRect(UnityEngine.Camera camera, Bounds bounds) {
            Vector3 min = camera.WorldToScreenPoint(bounds.min), max = camera.WorldToScreenPoint(bounds.max);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        /// <summary>Diagnostics (!motion): the presented actor's motion step and body frame.</summary>
        public string DescribeMotionForDiagnostics(uint serverId) {
            if (!actors.TryGetValue(serverId, out PresentedActor actor) || actor?.Visual == null) {
                return "not presented";
            }

            RoMotionStep step = actor.MotionStep;
            return step.Motion + "/" + step.Mode + " x" + step.FrameDelayScale.ToString("F2") + " len=" + step.LengthMs.ToString("F0") +
                " t=" + ((RoPresentationClock.NowSeconds - actor.ActionStartLocalTime) * 1000f).ToString("F0") + " " +
                actor.Visual.DescribeBodyFrame();
        }

        private RoMotionStep ResolveMotionStep(RoOnlineActorSnapshot actor, PresentedActor presentedActor, float nowSeconds) {
            RoSpriteMotion motion = ResolveMotion(actor);
            if (motion == RoSpriteMotion.Dead) {
                return new RoMotionStep(RoSpriteMotion.Dead, RoMotionMode.Once);
            }

            if (actor.Motion == RoOnlineActorMotion.Moving) {
                return new RoMotionStep(RoSpriteMotion.Walk, RoMotionMode.Loop);
            }

            bool player = ResolveMotionProfile(actor) == RoSpriteMotionProfile.Player;
            float authoredMs = presentedActor?.Visual != null &&
                presentedActor.Visual.TryGetMotionBaseDurationSeconds(motion, out float authoredSeconds)
                ? authoredSeconds * 1000f
                : 0f;
            float elapsedSeconds = presentedActor != null ? Mathf.Max(0f, nowSeconds - presentedActor.ActionStartLocalTime) : 0f;
            return Ragnarok.Combat.RoOriginalCombatRules.ActorMotion(new RoMotionInput {
                WorldMotion = actor.Motion,
                IsPlayer = player,
                AttackMotionMs = actor.ActionMotionTimeMilliseconds,
                DurationMs = actor.ActionDurationMilliseconds,
                PlayerWithBow = player && Ragnarok.Combat.RoOriginalCombatRules.UsesArrow(
                    (int) RoMapWeaponActionSemantics.ResolveWeaponType(actor.Weapon, realType: true)),
                ElapsedMs = elapsedSeconds * 1000f,
                AuthoredMs = authoredMs
            });
        }

        private float ResolveMoveElapsedSeconds(RoOnlineActorSnapshot actor, PresentedActor presentedActor, float nowSeconds) {
            if (worldController != null &&
                worldController.TryCalculateMoveElapsedSeconds(actor.MoveStartTime, nowSeconds, out float serverElapsedSeconds)) {
                float duration = Mathf.Max(0f, presentedActor.MoveTotalDurationSeconds);
                if (duration <= 0f) {
                    duration = presentedActor.MoveSegmentEndTimes.Count > 0
                        ? presentedActor.MoveSegmentEndTimes[^1]
                        : presentedActor.MoveDurationSeconds;
                }

                serverElapsedSeconds = Mathf.Clamp(serverElapsedSeconds, 0f, Mathf.Max(0f, duration));
                presentedActor.LastMoveServerElapsedSeconds = serverElapsedSeconds;
                if (actor.IsSelf) {
                    presentedActor.MovePresentedElapsedSeconds = serverElapsedSeconds;
                    presentedActor.MoveLastPresentationLocalTime = nowSeconds;
                    presentedActor.LastMoveCorrectionMode = RoMovePresentationCorrectionMode.None;
                    presentedActor.LastMoveDistanceToAuthoritative = 0f;
                    return serverElapsedSeconds;
                }

                float deltaSeconds = Mathf.Max(0f, nowSeconds - presentedActor.MoveLastPresentationLocalTime);
                RoMovePresentationCorrection correction = RoMovePresentation.AdvanceLocalElapsed(
                    presentedActor.MovePresentedElapsedSeconds,
                    serverElapsedSeconds,
                    duration,
                    deltaSeconds,
                    0f,
                    RoMovePresentation.CalculateSpeedScaledLeadToleranceSeconds(
                        ResolveStraightStepMilliseconds(actor.Speed, ResolveConfiguredStraightStepMilliseconds()),
                        MovementTuning),
                    RoMovePresentation.DefaultLeadToleranceCells,
                    MovementTuning.CatchupSeconds,
                    MovementTuning.SoftCorrectionSeconds,
                    float.PositiveInfinity);
                presentedActor.MovePresentedElapsedSeconds = correction.TargetElapsedSeconds;
                presentedActor.MoveLastPresentationLocalTime = nowSeconds;
                presentedActor.LastMoveCorrectionMode = correction.Mode;
                presentedActor.LastMoveDistanceToAuthoritative = correction.DistanceToAuthoritative;
                if (correction.Mode != RoMovePresentationCorrectionMode.None) {
                    RecordMovementTrace(
                        ToMovementTraceCorrectionEvent(correction.Mode),
                        actor,
                        presentedActor,
                        presentedActor.Root != null ? presentedActor.Root.transform.position : presentedActor.MoveStartWorld,
                        serverElapsedSeconds,
                        correction.TargetElapsedSeconds,
                        correction.Mode,
                        correction.DistanceToAuthoritative,
                        presentedActor.LastMoveTraceSegmentIndex,
                        ResolveFacingDirection(actor, presentedActor, nowSeconds),
                        RoSpriteMotion.Walk,
                        "remote correction");
                }

                return correction.TargetElapsedSeconds;
            }

            float localElapsedSeconds = Mathf.Max(0f, nowSeconds - presentedActor.MoveStartLocalTime);
            presentedActor.MovePresentedElapsedSeconds = localElapsedSeconds;
            presentedActor.MoveLastPresentationLocalTime = nowSeconds;
            presentedActor.LastMoveServerElapsedSeconds = RoMovementTraceRecord.UnsetFloat;
            presentedActor.LastMoveCorrectionMode = RoMovePresentationCorrectionMode.None;
            presentedActor.LastMoveDistanceToAuthoritative = 0f;
            return localElapsedSeconds;
        }

        private void RecordMovePresentationTrace(
            RoOnlineActorSnapshot actor,
            PresentedActor presentedActor,
            Vector3 visualWorldPosition,
            float presentedElapsedSeconds,
            float nowSeconds
        ) {
            if (worldController == null || !worldController.IsMovementTraceActive) {
                return;
            }

            int segmentIndex = RoMovementTraceRecord.UnsetInt;
            float distanceToAuthoritative = presentedActor.LastMoveDistanceToAuthoritative;
            if (presentedActor.HasMove) {
                RoMovementSample presentedSample = SamplePresentedMove(presentedActor, presentedElapsedSeconds);
                segmentIndex = presentedSample.SegmentIndex;
                if (presentedActor.LastMoveServerElapsedSeconds >= 0f) {
                    RoMovementSample authoritativeSample = SamplePresentedMove(
                        presentedActor,
                        presentedActor.LastMoveServerElapsedSeconds);
                    distanceToAuthoritative = RoMovePresentation.FlatDistance(visualWorldPosition, authoritativeSample.Position);
                }
            }

            RoActorDirection facing = ResolveFacingDirection(actor, presentedActor, nowSeconds);
            RoSpriteMotion spriteMotion = ResolveMotion(actor);
            if (segmentIndex >= 0 && segmentIndex != presentedActor.LastMoveTraceSegmentIndex) {
                presentedActor.LastMoveTraceSegmentIndex = segmentIndex;
                RecordMovementTrace(
                    RoMovementTraceEventType.SegmentTransition,
                    actor,
                    presentedActor,
                    visualWorldPosition,
                    presentedActor.LastMoveServerElapsedSeconds,
                    presentedElapsedSeconds,
                    presentedActor.LastMoveCorrectionMode,
                    distanceToAuthoritative,
                    segmentIndex,
                    facing,
                    spriteMotion,
                    "remote segment transition");
            }

            RecordMovementTrace(
                RoMovementTraceEventType.MovementSample,
                actor,
                presentedActor,
                visualWorldPosition,
                presentedActor.LastMoveServerElapsedSeconds,
                presentedElapsedSeconds,
                presentedActor.LastMoveCorrectionMode,
                distanceToAuthoritative,
                segmentIndex,
                facing,
                spriteMotion,
                "remote sample");
        }

        private void RecordMovementTrace(
            RoMovementTraceEventType eventType,
            RoOnlineActorSnapshot actor,
            PresentedActor presentedActor,
            Vector3 visualWorldPosition,
            float serverElapsedSeconds,
            float presentedElapsedSeconds,
            RoMovePresentationCorrectionMode correctionMode,
            float distanceToAuthoritative,
            int segmentIndex,
            RoActorDirection facing,
            RoSpriteMotion spriteMotion,
            string detail
        ) {
            if (worldController == null || !worldController.IsMovementTraceActive) {
                return;
            }

            Vector2Int displayCell = GetDisplayCell(actor);
            var record = new RoMovementTraceRecord(
                eventType,
                Time.frameCount,
                RoPresentationClock.NowSeconds,
                RoPresentationClock.DeltaSeconds,
                actor.ServerId,
                actor.ObjectType,
                actor.IsSelf,
                actor.CellX,
                actor.CellY,
                true,
                displayCell.x,
                displayCell.y,
                visualWorldPosition,
                actor.MoveStartTime,
                serverElapsedSeconds,
                presentedElapsedSeconds,
                correctionMode,
                distanceToAuthoritative,
                segmentIndex,
                facing,
                actor.Motion,
                spriteMotion,
                presentedActor?.WalkDistanceCells ?? RoMovementTraceRecord.UnsetFloat,
                detail);
            worldController.TryRecordMovementTrace(record);
        }

        private static RoMovementTraceEventType ToMovementTraceCorrectionEvent(RoMovePresentationCorrectionMode correctionMode) {
            return correctionMode switch {
                RoMovePresentationCorrectionMode.HardSnap => RoMovementTraceEventType.HardSnap,
                RoMovePresentationCorrectionMode.SoftCorrection => RoMovementTraceEventType.SoftCorrection,
                RoMovePresentationCorrectionMode.CatchUp => RoMovementTraceEventType.CatchUp,
                RoMovePresentationCorrectionMode.Hold => RoMovementTraceEventType.Hold,
                _ => RoMovementTraceEventType.CorrectionNone
            };
        }



        public static bool ShouldLoopSpriteMotion(RoSpriteMotion motion) {
            switch (motion) {
                case RoSpriteMotion.PickUp:
                case RoSpriteMotion.Attack1:
                case RoSpriteMotion.Attack2:
                case RoSpriteMotion.Attack3:
                case RoSpriteMotion.Hit:
                case RoSpriteMotion.Dead:
                case RoSpriteMotion.Casting:
                case RoSpriteMotion.Special:
                case RoSpriteMotion.Throwing:
                    return false;
                default:
                    return true;
            }
        }

        public static float CalculateDeathFadeOpacity(float nowSeconds, float fadeStartSeconds) {
            if (fadeStartSeconds <= 0f || nowSeconds < fadeStartSeconds) {
                return 1f;
            }

            float fadeProgress = DeathFadeSeconds > 0f ? Mathf.Clamp01((nowSeconds - fadeStartSeconds) / DeathFadeSeconds) : 1f;
            return 1f - fadeProgress;
        }


        private static bool IsFiniteActionMotion(RoOnlineActorMotion motion) {
            return motion == RoOnlineActorMotion.Pickup ||
                motion == RoOnlineActorMotion.Attack ||
                motion == RoOnlineActorMotion.Attack2 ||
                motion == RoOnlineActorMotion.Attack3 ||
                motion == RoOnlineActorMotion.Throwing ||
                motion == RoOnlineActorMotion.Skill ||
                motion == RoOnlineActorMotion.Hit ||
                motion == RoOnlineActorMotion.Casting;
        }







        public static RoSpriteMotionProfile ResolveMotionProfile(RoOnlineActorSnapshot actor) {
            switch (actor.ObjectType) {
                case RoMapActorObjectType.Monster:
                    return RoSpriteMotionProfile.Monster;
                case RoMapActorObjectType.Pet:
                case RoMapActorObjectType.Homunculus:
                case RoMapActorObjectType.Mercenary:
                case RoMapActorObjectType.Elemental:
                    return RoSpriteMotionProfile.Pet;
                case RoMapActorObjectType.Npc:
                case RoMapActorObjectType.Npc2:
                    return RoSpriteMotionProfile.Npc;
                case RoMapActorObjectType.Unknown:
                    return RoEntityDisplayMetadataTable.Classify(actor.Job) switch {
                        RoEntityDisplayKind.Monster => RoSpriteMotionProfile.Monster,
                        RoEntityDisplayKind.Homunculus or RoEntityDisplayKind.Mercenary => RoSpriteMotionProfile.Pet,
                        RoEntityDisplayKind.Npc => RoSpriteMotionProfile.Npc,
                        _ => RoSpriteMotionProfile.Player
                    };
                default:
                    return RoSpriteMotionProfile.Player;
            }
        }

        public static RoActorDirection ResolveFacingDirection(RoOnlineActorSnapshot actor) {
            if (actor.Motion == RoOnlineActorMotion.Moving && actor.HasMovePath) {
                var delta = new Vector3(actor.MovePath.EndX - actor.MovePath.StartX, 0f, actor.MovePath.EndY - actor.MovePath.StartY);
                return RoSpriteActionMap.GetDirectionFromWorldDelta(delta, DirectionFromPacket(actor.Direction));
            }

            return DirectionFromPacket(actor.Direction);
        }

        private static RoActorDirection ResolveFacingDirection(
            RoOnlineActorSnapshot snapshot,
            PresentedActor actor,
            float nowSeconds
        ) {
            if (snapshot.Motion == RoOnlineActorMotion.Moving &&
                actor != null &&
                actor.HasMove &&
                actor.MoveWaypoints.Count > 1) {
                // Use the same smoothed/presented elapsed that sampled the visual position. Using
                // now-MoveStartLocalTime here puts facing on a different segment during Hold/CatchUp.
                float elapsedSeconds = Mathf.Max(0f, actor.MovePresentedElapsedSeconds);
                RoMovementSample sample = SamplePresentedMove(actor, elapsedSeconds);
                // First partial step uses the presented position for natural heading; later steps remain
                // segment-stable so correction overshoot cannot flip the actor 180 degrees.
                var fallbackPathDelta = new Vector3(
                    snapshot.MovePath.EndX - snapshot.MovePath.StartX,
                    0f,
                    snapshot.MovePath.EndY - snapshot.MovePath.StartY);
                Vector3 delta = RoFacingResolver.ResolveWalkLookDelta(
                    actor.MoveWaypoints,
                    sample.FacingSegmentIndex,
                    sample.FacingCurrentPosition,
                    fallbackPathDelta,
                    Vector3.zero);
                if (delta.sqrMagnitude > 0.0001f) {
                    return RoSpriteActionMap.GetDirectionFromWorldDelta(delta, DirectionFromPacket(snapshot.Direction));
                }
            }

            return ResolveFacingDirection(snapshot);
        }

        public static Vector2Int GetDisplayCell(RoOnlineActorSnapshot actor) {
            if (actor.Motion == RoOnlineActorMotion.Moving && actor.HasMovePath) {
                return new Vector2Int(actor.MovePath.EndX, actor.MovePath.EndY);
            }

            return new Vector2Int(actor.CellX, actor.CellY);
        }

        private float ResolveConfiguredStraightStepMilliseconds() {
            return movementTuning != null
                ? MovementTuning.StraightStepMsBaseline
                : Mathf.Max(1f, fallbackStraightStepMilliseconds);
        }

        private float ResolveConfiguredDiagonalStepMultiplier() {
            return movementTuning != null
                ? MovementTuning.DiagonalStepMultiplier
                : Mathf.Max(0.001f, diagonalStepMultiplier);
        }

        public static bool ShouldUseWalkableGridPosition(RoOnlineActorSnapshot actor) {
            if (actor.ObjectType == RoMapActorObjectType.Npc ||
                actor.ObjectType == RoMapActorObjectType.Npc2) {
                return actor.Motion == RoOnlineActorMotion.Moving && actor.HasMovePath;
            }

            if (actor.ObjectType == RoMapActorObjectType.Player ||
                actor.ObjectType == RoMapActorObjectType.Disguised ||
                actor.ObjectType == RoMapActorObjectType.Monster ||
                actor.ObjectType == RoMapActorObjectType.Pet ||
                actor.ObjectType == RoMapActorObjectType.Homunculus ||
                actor.ObjectType == RoMapActorObjectType.Mercenary ||
                actor.ObjectType == RoMapActorObjectType.Elemental) {
                return true;
            }

            if (actor.ObjectType != RoMapActorObjectType.Unknown) {
                return false;
            }

            RoEntityDisplayKind kind = RoEntityDisplayMetadataTable.Classify(actor.Job);
            return kind == RoEntityDisplayKind.Monster || kind == RoEntityDisplayKind.Homunculus || kind == RoEntityDisplayKind.Mercenary;
        }

        public static bool ShouldAttemptEntitySpriteVisual(RoOnlineActorSnapshot actor) {
            return actor.ObjectType != RoMapActorObjectType.Player &&
                actor.ObjectType != RoMapActorObjectType.Item &&
                actor.ObjectType != RoMapActorObjectType.Warp &&
                actor.ObjectType != RoMapActorObjectType.Effect;
        }

        public static float CalculateMoveDurationSeconds(
            RoOnlineActorSnapshot actor,
            float fallbackStraightStepMilliseconds = FallbackStraightStepMilliseconds,
            float diagonalMultiplier = RoClickToMoveController.ClassicDiagonalStepMultiplier
        ) {
            if (actor.Motion != RoOnlineActorMotion.Moving || !actor.HasMovePath) {
                return 0f;
            }

            int deltaX = Mathf.Abs(actor.MovePath.EndX - actor.MovePath.StartX);
            int deltaY = Mathf.Abs(actor.MovePath.EndY - actor.MovePath.StartY);
            int diagonalSteps = Mathf.Min(deltaX, deltaY);
            int straightSteps = Mathf.Max(deltaX, deltaY) - diagonalSteps;
            float straightStepMilliseconds = actor.Speed > 0
                ? actor.Speed
                : Mathf.Max(1f, fallbackStraightStepMilliseconds);
            int straightStep = Mathf.Max(1, Mathf.RoundToInt(straightStepMilliseconds));
            int diagonalStep = Mathf.Max(1, Mathf.FloorToInt(straightStep * Mathf.Max(0.001f, diagonalMultiplier)));
            int milliseconds = Math.Max(1, straightSteps * straightStep + diagonalSteps * diagonalStep);
            return Mathf.Max(0.001f, milliseconds / 1000f);
        }

        public static float CalculateMoveProgress(float elapsedSeconds, float durationSeconds) {
            if (durationSeconds <= 0f) {
                return 1f;
            }

            return Mathf.Clamp01(Mathf.Max(0f, elapsedSeconds) / durationSeconds);
        }

        public static float ClampRemoteMoveFastForwardSeconds(
            RoOnlineActorSnapshot actor,
            float serverElapsedSeconds,
            float moveDurationSeconds
        ) {
            return ClampRemoteMoveFastForwardSeconds(
                actor,
                serverElapsedSeconds,
                moveDurationSeconds,
                RoMovementTuning.Default);
        }

        public static float ClampRemoteMoveFastForwardSeconds(
            RoOnlineActorSnapshot actor,
            float serverElapsedSeconds,
            float moveDurationSeconds,
            RoMovementTuning tuning
        ) {
            float elapsed = Mathf.Clamp(serverElapsedSeconds, 0f, Mathf.Max(0f, moveDurationSeconds));
            if (actor.IsSelf) {
                return elapsed;
            }

            RoMovementTuning resolved = RoMovementTuning.Resolve(tuning);
            float straightStepSeconds = ResolveStraightStepMilliseconds(
                actor.Speed,
                fallbackMilliseconds: resolved.StraightStepMsBaseline) / 1000f;
            return Mathf.Min(elapsed, straightStepSeconds * resolved.RemoteInitialFastForwardMaxSteps);
        }

        private static float ResolveStraightStepMilliseconds(short actorSpeed, float fallbackMilliseconds) {
            return actorSpeed > 0
                ? actorSpeed
                : Mathf.Max(1f, fallbackMilliseconds);
        }

        public static float CalculateMovePresentationDuration(
            float moveDurationSeconds,
            float serverElapsedSeconds,
            bool startFromCurrentVisual
        ) {
            float duration = Mathf.Max(MinimumMovePresentationSeconds, moveDurationSeconds);
            if (!startFromCurrentVisual) {
                return duration;
            }

            float remaining = duration - Mathf.Max(0f, serverElapsedSeconds);
            if (remaining <= 0f) {
                return Mathf.Min(duration, CompletedMoveCatchUpSeconds);
            }

            return Mathf.Max(MinimumMovePresentationSeconds, remaining);
        }

        public static float CalculateMovePresentationStartLocalTime(
            float nowSeconds,
            float moveDurationSeconds,
            float serverElapsedSeconds,
            bool startFromCurrentVisual
        ) {
            if (startFromCurrentVisual) {
                return nowSeconds;
            }

            float duration = Mathf.Max(MinimumMovePresentationSeconds, moveDurationSeconds);
            return nowSeconds - Mathf.Clamp(serverElapsedSeconds, 0f, duration);
        }

        public static bool IsMoveComplete(float elapsedSeconds, float durationSeconds) {
            return CalculateMoveProgress(elapsedSeconds, durationSeconds) >= 1f;
        }

        public static float CalculatePresentedWalkDistanceDelta(Vector3 fromWorld, Vector3 toWorld) {
            Vector3 delta = toWorld - fromWorld;
            delta.y = 0f;
            return delta.magnitude;
        }

        public static float AccumulatePresentedWalkDistance(float accumulatedDistanceCells, Vector3 fromWorld, Vector3 toWorld) {
            return Mathf.Max(0f, accumulatedDistanceCells) + CalculatePresentedWalkDistanceDelta(fromWorld, toWorld);
        }

        public static Vector3 InterpolateMovePosition(Vector3 start, Vector3 end, float progress) {
            return Vector3.Lerp(start, end, Mathf.Clamp01(progress));
        }

        public static Vector3 InterpolateMovePosition(
            IReadOnlyList<Vector3> waypoints,
            IReadOnlyList<float> segmentEndTimes,
            float elapsedSeconds
        ) {
            if (RoMovementSampler.TrySample(waypoints, segmentEndTimes, elapsedSeconds, out RoMovementSample sample)) {
                return sample.Position;
            }

            return waypoints != null && waypoints.Count > 0 ? waypoints[0] : default;
        }

        public static Vector3 InterpolateMovePosition(IReadOnlyList<Vector3> waypoints, float progress, float totalDistance) {
            if (waypoints == null || waypoints.Count == 0) {
                return default;
            }

            if (waypoints.Count == 1) {
                return waypoints[0];
            }

            if (RoMovementSampler.TrySampleByDuration(
                    waypoints,
                    Mathf.Clamp01(progress),
                    1f,
                    totalDistance,
                    out RoMovementSample sample)) {
                return sample.Position;
            }

            return progress <= 0f ? waypoints[0] : waypoints[waypoints.Count - 1];
        }

        public static float CalculateMovePathDistance(IReadOnlyList<Vector3> waypoints) {
            return RoMovementSampler.CalculatePathDistance(waypoints);
        }

        private static void AppendMoveWaypoint(List<Vector3> waypoints, Vector3 waypoint) {
            if (waypoints == null) {
                return;
            }

            if (waypoints.Count > 0 && Vector3.SqrMagnitude(waypoints[waypoints.Count - 1] - waypoint) <= 0.0001f) {
                return;
            }

            waypoints.Add(waypoint);
        }

        public static int CalculateActorSortingOrder(
            Vector3 worldPosition,
            UnityEngine.Camera camera,
            int baseOrder,
            int bucketCount,
            uint serverId
        ) {
            int safeBucketCount = Mathf.Max(1, bucketCount);
            int bucket;
            if (camera != null) {
                Vector3 viewportPosition = camera.WorldToViewportPoint(worldPosition);
                float screenY = SanitizeFinite(viewportPosition.y, 0.5f);
                bucket = Mathf.RoundToInt((1f - screenY) * safeBucketCount);
            } else {
                bucket = Mathf.RoundToInt(SanitizeFinite(worldPosition.z, 0f));
            }

            return baseOrder +
                bucket * RoActorLayeredVisual.SortingOrderStride +
                CalculateActorSortingTieBreaker(serverId);
        }

        public static RoActorDirection DirectionFromPacket(int direction) {
            return direction >= 0 && direction <= 7 ? (RoActorDirection) direction : RoActorDirection.South;
        }

        public static RoActorHeadFacing HeadFacingFromPacket(int headDirection) {
            return headDirection switch {
                1 => RoActorHeadFacing.Right,
                2 => RoActorHeadFacing.Left,
                _ => RoActorHeadFacing.Center
            };
        }

        private static int ResolveHeadDirectionFrameIndex(RoOnlineActorSnapshot actor) {
            return (int) HeadFacingFromPacket(actor.HeadDirection);
        }

        private int ResolveActorSortingOrder(RoOnlineActorSnapshot snapshot, Vector3 worldPosition) {
            return ResolveActorSortingOrder(snapshot, worldPosition, useDynamicSortingOrder ? ResolveSortingCamera() : null);
        }

        private int ResolveActorSortingOrder(RoOnlineActorSnapshot snapshot, Vector3 worldPosition, UnityEngine.Camera resolvedSortingCamera) {
            if (!useDynamicSortingOrder) {
                return sortingOrderBase;
            }

            return CalculateActorSortingOrder(
                worldPosition,
                resolvedSortingCamera,
                sortingOrderBase,
                sortingBucketCount,
                snapshot.ServerId);
        }

        private UnityEngine.Camera ResolveSortingCamera() {
            if (sortingCamera != null) {
                return sortingCamera;
            }

            int frame = Time.frameCount;
            if (cachedSortingCameraFrame != frame) {
                cachedSortingCamera = UnityEngine.Camera.main;
                cachedSortingCameraFrame = frame;
            }

            return cachedSortingCamera;
        }

        private static int CalculateActorSortingTieBreaker(uint serverId) {
            return (int) (serverId % ActorSortingTieBreakerRange);
        }

        private static float SanitizeFinite(float value, float fallback) {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private static bool ShouldCreateShadow(RoOnlineActorSnapshot actor) {
            return actor.ObjectType == RoMapActorObjectType.Player ||
                actor.ObjectType == RoMapActorObjectType.Monster ||
                actor.ObjectType == RoMapActorObjectType.Npc ||
                actor.ObjectType == RoMapActorObjectType.Npc2 ||
                actor.ObjectType == RoMapActorObjectType.Pet ||
                actor.ObjectType == RoMapActorObjectType.Homunculus ||
                actor.ObjectType == RoMapActorObjectType.Mercenary ||
                actor.ObjectType == RoMapActorObjectType.Elemental;
        }

        private static bool ShouldCreateFallbackCapsule(RoOnlineActorSnapshot actor) {
            return actor.ObjectType == RoMapActorObjectType.Monster ||
                actor.ObjectType == RoMapActorObjectType.Pet ||
                actor.ObjectType == RoMapActorObjectType.Homunculus ||
                actor.ObjectType == RoMapActorObjectType.Mercenary ||
                actor.ObjectType == RoMapActorObjectType.Elemental ||
                actor.ObjectType == RoMapActorObjectType.Item ||
                actor.ObjectType == RoMapActorObjectType.Npc ||
                actor.ObjectType == RoMapActorObjectType.Npc2;
        }

        private static string BuildActorObjectName(RoOnlineActorSnapshot actor) {
            string label = string.IsNullOrWhiteSpace(actor.Name) ? actor.ObjectType.ToString() : actor.Name;
            return $"RO Online Actor {actor.ServerId} - {label}";
        }

        private static string BuildPointerTargetLabel(RoOnlineActorSnapshot actor) {
            if (!string.IsNullOrWhiteSpace(actor.Name)) {
                return actor.Name;
            }

            return actor.ObjectType == RoMapActorObjectType.Item && actor.ItemId != 0
                ? $"Item {actor.ItemId}"
                : actor.ObjectType.ToString();
        }

        private static Material CreateFallbackMaterial() {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) {
                shader = Shader.Find("Sprites/Default");
            }

            return new Material(shader) {
                name = "RO Online Actor Fallback Material",
                color = new Color(0.35f, 0.85f, 1f, 0.75f)
            };
        }

        private static Material CreateItemFallbackMaterial() {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) {
                shader = Shader.Find("Sprites/Default");
            }

            return new Material(shader) {
                name = "RO Online Item Fallback Material",
                color = new Color(1f, 0.78f, 0.2f, 0.85f)
            };
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

        private sealed class PresentedActor {
            public RoMotionStep MotionStep;
            public PresentedActor(GameObject root) {
                Root = root;
            }

            public GameObject Root { get; }
            public RoActorLayeredVisual Visual { get; set; }
            /// <summary>The Granny (.gr2) model drawn instead of sprite layers (Emperium, guardians, guild flags, treasure box).</summary>
            public Ragnarok.Granny.RoGrannyActorModel Granny { get; set; }
            /// <summary>The guild whose emblem the model shows (a flag), and the emblem serial it was last set at.</summary>
            public uint GrannyGuildId { get; set; }
            public uint GrannyServerId { get; set; }
            public int GrannyEmblemSerial { get; set; } = -1;
            public RoRentalCompanionVisual FalconCompanion;
            public RoActorLayeredVisual WugVisual;
            public RoPointerTargetMarker PointerMarker { get; set; }
            public RoOnlinePointerTargetMarker OnlinePointerMarker { get; set; }
            public bool PointerTargetShapeConfigured { get; set; }
            public RoActorResourceBarOverlay ResourceBars { get; set; }
            public RoActorCastBarOverlay CastBar { get; set; }
            public RoOnlineActorPresentationSignature Signature { get; set; }
            public bool HasIncrementalSnapshotState { get; set; }
            public int LastSnapshotCellX { get; set; }
            public int LastSnapshotCellY { get; set; }
            public int LastSnapshotDirection { get; set; }
            public ushort LastSnapshotHeadDirection { get; set; }
            public RoOnlineActorMotion LastSnapshotMotion { get; set; }
            public RoMapActorSpawnState LastSnapshotSpawnState { get; set; }
            public int LastSnapshotActionRevision { get; set; }
            public bool LastSnapshotCompletesActionToStandby { get; set; }
            public int LastSnapshotEffectState { get; set; }
            public int LastSnapshotHp { get; set; }
            public int LastSnapshotMaxHp { get; set; }
            public short LastSnapshotBaseLevel { get; set; }
            public bool HasResourceBarState { get; set; }
            public bool LastResourceBarShouldShow { get; set; }
            public int LastResourceBarHp { get; set; }
            public int LastResourceBarMaxHp { get; set; }
            public UnityEngine.Camera LastResourceBarCamera { get; set; }
            public bool TriedAssetVisual { get; set; }
            public bool HasBuiltPresentation { get; set; }
            public bool HasPosition { get; set; }
            public bool HasMove { get; set; }
            public RoOnlineMovePresentationKey MoveKey { get; set; }
            public Vector3 MoveStartWorld { get; set; }
            public Vector3 MoveEndWorld { get; set; }
            public List<Vector3> MoveWaypoints { get; } = new List<Vector3>(32);
            public List<float> MoveSegmentEndTimes { get; } = new List<float>(32);
            public float MovePathDistance { get; set; }
            public float MoveStartLocalTime { get; set; }
            public float MoveDurationSeconds { get; set; }
            public float MoveTotalDurationSeconds { get; set; }
            public float MovePresentedElapsedSeconds { get; set; }
            public float MoveLastPresentationLocalTime { get; set; }
            public float LastMoveServerElapsedSeconds { get; set; }
            public RoMovePresentationCorrectionMode LastMoveCorrectionMode { get; set; }
            public float LastMoveDistanceToAuthoritative { get; set; }
            public int LastMoveTraceSegmentIndex { get; set; }
            public bool HasInterruptedMoveActionVisualHold { get; set; }
            public int InterruptedMoveActionRevision { get; set; }
            public bool HasInterruptedMoveActionSettle { get; set; }
            public Vector3 InterruptedMoveActionSettleStartWorld { get; set; }
            public Vector3 InterruptedMoveActionSettleTargetWorld { get; set; }
            public float InterruptedMoveActionSettleStartLocalTime { get; set; }
            public float InterruptedMoveActionSettleDurationSeconds { get; set; }
            public bool HasWalkDistancePosition { get; set; }
            public Vector3 LastWalkDistanceWorldPosition { get; set; }
            public float WalkDistanceCells { get; set; }
            public int ActionRevision { get; set; }
            public float ActionStartLocalTime { get; set; }
            public bool PendingDeathRelease { get; set; }
            public float DeathFadeStartLocalTime { get; set; }
            public float DeathReleaseLocalTime { get; set; }
            public bool HasObjectNameState { get; set; }
            public uint ObjectNameServerId { get; set; }
            public RoMapActorObjectType ObjectNameObjectType { get; set; }
            public uint ObjectNameItemId { get; set; }
            public string ObjectNameActorName { get; set; }
            public bool HasVisualState { get; set; }
            public bool LastLoop { get; set; }
            public float LastOpacity { get; set; }
            public float LastFrameDelayScale { get; set; }
            public RoSpriteMotion LastMotion { get; set; }
            public RoActorDirection LastFacing { get; set; }
            public int LastHeadDirectionFrameIndex { get; set; }
            public bool HasAppliedStatusLook { get; set; }
            public Ragnarok.Combat.RoActorStatusLook AppliedStatusLook { get; set; }
            /// <summary>The shadow render state last applied (ApplyActorShadowRenderMode).</summary>
            public bool HasShadowState { get; set; }
            public RoActorLayeredVisual ShadowStateVisual { get; set; }
            public RoShadowRenderMode LastShadowMode { get; set; }
            public RoEnhancedShadowSettings LastShadowSettings { get; set; }
            public bool LastShadowActive { get; set; }
            public bool LastShadowFake { get; set; }
            /// <summary>The actor the bridge holds a level aura for, and that aura's state (0 = none).</summary>
            public uint LevelAuraActorId { get; set; }
            public int LevelAuraKey { get; set; }
            /// <summary>The actor whose aura state LevelAuraKey is (also when it is "none", so an actor without an aura
            /// is not re-sent to the bridge at every sync).</summary>
            public uint LevelAuraStateId { get; set; }
            public bool HasRenderState { get; set; }
            public int LastRenderQueueBase { get; set; }
            public int LastSortingOrder { get; set; }
            public bool LastSortingDynamic { get; set; }
            public int LastSortingOrderBase { get; set; }
            public int LastSortingBucketCount { get; set; }
            public int LastSortingCameraRevision { get; set; }
            public UnityEngine.Camera LastSortingCamera { get; set; }
            public Vector3 LastSortingWorldPosition { get; set; }

            public void ClearMove() {
                HasMove = false;
                MoveKey = default;
                MoveStartWorld = default;
                MoveEndWorld = default;
                MoveWaypoints.Clear();
                MoveSegmentEndTimes.Clear();
                MovePathDistance = 0f;
                MoveStartLocalTime = 0f;
                MoveDurationSeconds = 0f;
                MoveTotalDurationSeconds = 0f;
                MovePresentedElapsedSeconds = 0f;
                MoveLastPresentationLocalTime = 0f;
                LastMoveServerElapsedSeconds = 0f;
                LastMoveCorrectionMode = RoMovePresentationCorrectionMode.None;
                LastMoveDistanceToAuthoritative = 0f;
                LastMoveTraceSegmentIndex = -1;
            }

            public void ClearInterruptedMoveActionVisualState() {
                HasInterruptedMoveActionVisualHold = false;
                InterruptedMoveActionRevision = 0;
                HasInterruptedMoveActionSettle = false;
                InterruptedMoveActionSettleStartWorld = default;
                InterruptedMoveActionSettleTargetWorld = default;
                InterruptedMoveActionSettleStartLocalTime = 0f;
                InterruptedMoveActionSettleDurationSeconds = 0f;
            }

            public void ClearWalkDistance() {
                HasWalkDistancePosition = false;
                LastWalkDistanceWorldPosition = default;
                WalkDistanceCells = 0f;
            }

            public void ResetTransientState() {
                HasPosition = false;
                ActionRevision = 0;
                ActionStartLocalTime = 0f;
                PendingDeathRelease = false;
                DeathFadeStartLocalTime = 0f;
                DeathReleaseLocalTime = 0f;
                HasObjectNameState = false;
                ObjectNameServerId = 0;
                ObjectNameObjectType = default;
                ObjectNameItemId = 0;
                ObjectNameActorName = null;
                PointerTargetShapeConfigured = false;
                HasIncrementalSnapshotState = false;
                LastSnapshotCellX = 0;
                LastSnapshotCellY = 0;
                LastSnapshotDirection = 0;
                LastSnapshotHeadDirection = 0;
                LastSnapshotMotion = default;
                LastSnapshotSpawnState = default;
                LastSnapshotActionRevision = 0;
                LastSnapshotCompletesActionToStandby = false;
                LastSnapshotEffectState = 0;
                LastSnapshotHp = 0;
                LastSnapshotMaxHp = 0;
                LastSnapshotBaseLevel = 0;
                ResourceBars?.ClearValues();
                CastBar?.HideCast();
                HasResourceBarState = false;
                LastResourceBarShouldShow = false;
                LastResourceBarHp = -1;
                LastResourceBarMaxHp = 0;
                LastResourceBarCamera = null;
                ClearWalkDistance();
                DestroyRentalCompanion(ref FalconCompanion);

                InvalidateVisualState();
                ClearMove();
            }

            public void InvalidateVisualState() {
                HasVisualState = false;
                LastLoop = false;
                LastOpacity = 1f;
                LastFrameDelayScale = 1f;
                LastMotion = default;
                LastFacing = default;
                LastHeadDirectionFrameIndex = 0;
                HasAppliedStatusLook = false;
                HasShadowState = false;
                HasRenderState = false;
                LastRenderQueueBase = int.MinValue;
                LastSortingOrder = int.MinValue;
                LastSortingDynamic = false;
                LastSortingOrderBase = int.MinValue;
                LastSortingBucketCount = 0;
                LastSortingCameraRevision = int.MinValue;
                LastSortingCamera = null;
                LastSortingWorldPosition = default;
            }

            public void DestroyVisualChildren() {
                for (int i = Root.transform.childCount - 1; i >= 0; i--) {
                    GameObject child = Root.transform.GetChild(i).gameObject;
                    child.SetActive(false);
                    DestroyRuntimeObject(child);
                }

                PointerMarker = null;
                OnlinePointerMarker = null;
                WugVisual = null;
                Granny = null;
                PointerTargetShapeConfigured = false;
                HasIncrementalSnapshotState = false;
            }
        }

        public readonly struct RoOnlineMovePresentationKey : IEquatable<RoOnlineMovePresentationKey> {
            private RoOnlineMovePresentationKey(
                uint moveStartTime,
                int startX,
                int startY,
                int endX,
                int endY
            ) {
                MoveStartTime = moveStartTime;
                StartX = startX;
                StartY = startY;
                EndX = endX;
                EndY = endY;
            }

            private uint MoveStartTime { get; }
            private int StartX { get; }
            private int StartY { get; }
            private int EndX { get; }
            private int EndY { get; }

            public static RoOnlineMovePresentationKey From(RoOnlineActorSnapshot actor) {
                return new RoOnlineMovePresentationKey(
                    actor.MoveStartTime,
                    actor.MovePath.StartX,
                    actor.MovePath.StartY,
                    actor.MovePath.EndX,
                    actor.MovePath.EndY
                );
            }

            public bool Equals(RoOnlineMovePresentationKey other) {
                return MoveStartTime == other.MoveStartTime &&
                    StartX == other.StartX &&
                    StartY == other.StartY &&
                    EndX == other.EndX &&
                    EndY == other.EndY;
            }

            public override bool Equals(object obj) {
                return obj is RoOnlineMovePresentationKey other && Equals(other);
            }

            public override int GetHashCode() {
                unchecked {
                    int hash = MoveStartTime.GetHashCode();
                    hash = (hash * 397) ^ StartX;
                    hash = (hash * 397) ^ StartY;
                    hash = (hash * 397) ^ EndX;
                    hash = (hash * 397) ^ EndY;
                    return hash;
                }
            }
        }

        private readonly struct RoOnlineActorPresentationSignature : IEquatable<RoOnlineActorPresentationSignature> {
            private RoOnlineActorPresentationSignature(
                RoMapActorObjectType objectType,
                short job,
                ushort body,
                byte sex,
                ushort head,
                uint weapon,
                uint shield,
                ushort accessory,
                ushort accessory2,
                ushort accessory3,
                short headPalette,
                short bodyPalette,
                ushort robe,
                bool hasCart,
                int cartNum,
                bool hasFalcon,
                bool hasWug,
                uint itemId,
                bool itemIdentified
            ) {
                ObjectType = objectType;
                Job = job;
                Body = body;
                Sex = sex;
                Head = head;
                Weapon = weapon;
                Shield = shield;
                Accessory = accessory;
                Accessory2 = accessory2;
                Accessory3 = accessory3;
                HeadPalette = headPalette;
                BodyPalette = bodyPalette;
                Robe = robe;
                HasCart = hasCart;
                CartNum = cartNum;
                HasFalcon = hasFalcon;
                HasWug = hasWug;
                ItemId = itemId;
                ItemIdentified = itemIdentified;
            }

            private RoMapActorObjectType ObjectType { get; }
            private short Job { get; }
            private ushort Body { get; }
            private byte Sex { get; }
            private ushort Head { get; }
            private uint Weapon { get; }
            private uint Shield { get; }
            private ushort Accessory { get; }
            private ushort Accessory2 { get; }
            private ushort Accessory3 { get; }
            private short HeadPalette { get; }
            private short BodyPalette { get; }
            private ushort Robe { get; }
            private bool HasCart { get; }
            private int CartNum { get; }
            private bool HasFalcon { get; }
            private bool HasWug { get; }
            private uint ItemId { get; }
            private bool ItemIdentified { get; }

            public static RoOnlineActorPresentationSignature From(RoOnlineActorSnapshot actor) {
                return new RoOnlineActorPresentationSignature(
                    actor.ObjectType,
                    actor.Job,
                    actor.Body,
                    actor.Sex,
                    actor.Head,
                    actor.Weapon,
                    actor.Shield,
                    actor.Accessory,
                    actor.Accessory2,
                    actor.Accessory3,
                    actor.HeadPalette,
                    actor.BodyPalette,
                    actor.Robe,
                    actor.HasCart,
                    actor.CartNum,
                    actor.HasFalcon,
                    actor.HasWug,
                    actor.ItemId,
                    actor.ItemIdentified
                );
            }

            public bool Equals(RoOnlineActorPresentationSignature other) {
                return ObjectType == other.ObjectType &&
                    Job == other.Job &&
                    Body == other.Body &&
                    Sex == other.Sex &&
                    Head == other.Head &&
                    Weapon == other.Weapon &&
                    Shield == other.Shield &&
                    Accessory == other.Accessory &&
                    Accessory2 == other.Accessory2 &&
                    Accessory3 == other.Accessory3 &&
                    HeadPalette == other.HeadPalette &&
                    BodyPalette == other.BodyPalette &&
                    Robe == other.Robe &&
                    HasCart == other.HasCart &&
                    CartNum == other.CartNum &&
                    HasFalcon == other.HasFalcon &&
                    HasWug == other.HasWug &&
                    ItemId == other.ItemId &&
                    ItemIdentified == other.ItemIdentified;
            }

            public override bool Equals(object obj) {
                return obj is RoOnlineActorPresentationSignature other && Equals(other);
            }

            public override int GetHashCode() {
                unchecked {
                    int hash = (int) ObjectType;
                    hash = (hash * 397) ^ Job.GetHashCode();
                    hash = (hash * 397) ^ Body.GetHashCode();
                    hash = (hash * 397) ^ Sex.GetHashCode();
                    hash = (hash * 397) ^ Head.GetHashCode();
                    hash = (hash * 397) ^ Weapon.GetHashCode();
                    hash = (hash * 397) ^ Shield.GetHashCode();
                    hash = (hash * 397) ^ Accessory.GetHashCode();
                    hash = (hash * 397) ^ Accessory2.GetHashCode();
                    hash = (hash * 397) ^ Accessory3.GetHashCode();
                    hash = (hash * 397) ^ HeadPalette.GetHashCode();
                    hash = (hash * 397) ^ BodyPalette.GetHashCode();
                    hash = (hash * 397) ^ Robe.GetHashCode();
                    hash = (hash * 397) ^ HasCart.GetHashCode();
                    hash = (hash * 397) ^ CartNum;
                    hash = (hash * 397) ^ HasFalcon.GetHashCode();
                    hash = (hash * 397) ^ HasWug.GetHashCode();
                    hash = (hash * 397) ^ ItemId.GetHashCode();
                    hash = (hash * 397) ^ ItemIdentified.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
