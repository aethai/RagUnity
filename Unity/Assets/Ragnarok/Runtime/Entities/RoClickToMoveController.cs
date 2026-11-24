using System.Collections.Generic;
using Ragnarok.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using Ragnarok.Diagnostics;
using Ragnarok.Input;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.Sprites;
using Ragnarok.UI;
using Ragnarok.World;
using ROIO.Models.FileTypes;
using Unity.Profiling;

using Ragnarok.Combat;

namespace Ragnarok.Entities {
    public sealed class RoClickToMoveController : MonoBehaviour {
        public const float ClassicStraightStepMilliseconds = 150f;
        public const int ClassicStraightStepCost = 10;
        public const int ClassicDiagonalStepCost = 14;
        public const float ClassicDiagonalStepMultiplier = ClassicDiagonalStepCost / (float) ClassicStraightStepCost;
        public const int DefaultAttackRangeCells = 1;
        public const int DefaultPickupRangeCells = 2;
        private const int MaxGridStepsPerFrame = 8;
        private const float DefaultLocalActionMotionSeconds = 0.45f;
        private const float MinimumLocalActionMotionSeconds = 0.08f;
        // Upper bound (as a multiple of walk speed) on how fast the server-gated visual may chase the
        // authoritative position when correcting a re-route gap. Keeps catch-up looking like a walk, not a
        // dash, while staying just above walk speed so the move still completes on time.
        private const float ServerGatedCorrectionMaxWalkSpeedFactor = 1.15f;
        // Distance (in cells / world units) within which the server-gated visual is treated as having
        // reached its destination cell. The capped catch-up step (above) can leave the sprite trailing the
        // authoritative position on the final frame; if we ended the walk there the sprite would rest on a
        // different cell than actor.CellX/CellY and the next click-to-move would overshoot by a cell. Small
        // enough to be imperceptible, large enough that the capped step closes it within a couple of frames.
        private const float ServerGatedArrivalToleranceCells = 0.05f;
        private const float PortalScreenVerticalWorldUnits = 4.25f;
        private const float PortalScreenGroundRadiusPixels = 76f;
        private const float PortalScreenBaseHalfWidthPixels = 58f;
        private const float PortalScreenTopHalfWidthPixels = 46f;
        private const float SpritePointerCachePixelEpsilonSqr = 0.25f;
        private const int SpritePointerCacheMaxFrames = 8;
        private const int MaxSpritePointerExactCandidates = 4;
        private const int PointerTargetHitBufferSize = 64;
        private const float InputCameraResolveIntervalSeconds = 0.5f;
        private const float ContextMenuRightClickMaxMovePixels = 4f;
        private const float OnlineMoveAckTimeoutSeconds = 0.6f;
        private const float DependencyLookupRetrySeconds = 0.5f;
        private const float LocalMoveLeadToleranceCells = RoMovePresentation.DefaultLeadToleranceCells;
        private const float LocalMoveCorrectionOffsetEpsilonSqr = 0.0001f;
        // Minimum interval between held-LMB walk re-issues. Without it, a cursor straddling a cell
        // boundary (e.g. close-range diagonal) re-issues the destination every frame, churning the path
        // and flicking the first-step continuous heading between two 8-way directions. roBrowser throttles
        // to ~0.2s and Rebuild to ~0.5s for the same reason; the first click of a hold is never throttled.
        private const float DefaultHeldGridMoveThrottleSeconds = 0.15f;
        private const float PendingTargetSkillTimeoutSeconds = 2f;
        private const float PendingTargetSkillQueuedSendTimeoutSeconds = 15f;
        private const float PendingPickupRetryTimeoutSeconds = 3f;
        private const float DefaultVisualStopSettleSeconds = 0.07f;
        private const string VisualStopSettleReasonStopMove = "StopMoveSettle";
        private const string VisualStopSettleReasonActionInterrupt = "ActionInterruptSettle";
        private const string VisualStopSettleReasonHardSnap = "HardSnap";
        private const int DefaultSpritePointerColliderCrowdThreshold = 48;
        private const int RuntimePerfTraceCapacity = 200;
        private static readonly ProfilerMarker PointerRaycastMarker = new ProfilerMarker("RO ClickToMove.PointerRaycast");

        [SerializeField] private UnityEngine.Camera inputCamera;
        [SerializeField] private Collider groundCollider;
        [SerializeField] private RoGridSelector gridSelector;
        [SerializeField] private RoMovementTuning movementTuning;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float fallbackMoveSpeed = 8f;
        [SerializeField] private float straightStepMilliseconds = ClassicStraightStepMilliseconds;
        [SerializeField, Min(0f)] private float heldGridMoveThrottleSeconds = DefaultHeldGridMoveThrottleSeconds;
        [SerializeField] private float stopDistance = 0.08f;
        [SerializeField] private float groundOffset;
        [SerializeField] private bool lockYToGround = true;
        [SerializeField] private bool showTargetMarker;
        [SerializeField] private LayerMask pointerTargetMask = ~0;
        [SerializeField] private RoCursorStateController cursorStateController;
        [SerializeField] private RoOnlineWorldController onlineWorldController;
        [SerializeField] private RoMinimalLoginShell loginShell;
        [SerializeField] private RoSkillTargetSelection skillTargetSelection;
        [SerializeField, Min(0f)] private float visualStopSettleDurationSeconds = DefaultVisualStopSettleSeconds;
        [SerializeField, Min(0f)] private float visualStopSettleHardSnapCells = RoLiveMoveRoute.HardSnapDistanceCells;
        [SerializeField] private bool useOptimizedPointerColliderRefresh = true;
        [SerializeField] private bool useAdaptiveSpritePointerTargeting = true;
        [SerializeField, Min(0)] private int spritePointerColliderCrowdThreshold = DefaultSpritePointerColliderCrowdThreshold;
        [Header("Input Actions")]
        [SerializeField] private InputActionReference pointerPositionAction;
        [SerializeField] private InputActionReference primaryClickAction;
        [SerializeField] private InputActionReference secondaryClickAction;
        [SerializeField] private InputActionReference headLookModifierAction;

        private Vector3 targetPosition;
        private bool hasTarget;
        private bool wasLeftPressed;
        private bool wasRightPressed;
        private bool wasHeadLookModifierPressed;
        private RoClickIntentTracker clickIntentTracker;
        private bool hasSubmittedHeldGridCell;
        private bool hasSubmittedHeldHeadLookDirection;
        private Vector2Int submittedHeldGridCell;
        private float nextHeldGridMoveTime;
        private RoActorDirection submittedHeldHeadLookDirection;
        private GameObject targetMarker;
        private RoActorSpriteVisual actorVisual;
        private RoActorLayeredVisual layeredActorVisual;
        private RoActorDirection facingDirection = RoActorDirection.South;
        private readonly List<Vector2Int> gridPath = new();
        private readonly List<Vector2Int> pendingGridPath = new();
        private readonly List<Vector2Int> rebuiltGridPath = new();
        private readonly List<Vector2Int> attackApproachPath = new();
        private readonly List<Vector2Int> attackLineScratchPath = new();
        private readonly List<Vector2Int> portalApproachPath = new();
        private readonly List<Vector2Int> pendingServerGatedContinuationPath = new();
        private readonly List<Vector3> waypoints = new();
        private readonly List<Vector3> serverGatedMoveWaypoints = new();
        private readonly List<float> serverGatedMoveSegmentEndTimes = new();
        private readonly List<RoPointerTarget> pointerTargetCandidates = new();
        private readonly List<SpritePointerTargetHit> spritePointerTargetHits = new();
        private readonly List<SpritePointerTargetCandidate> spritePointerTargetCandidates = new();
        private readonly List<SpritePointerTargetHit> cachedSpritePointerTargetHits = new();
        private readonly List<RoOnlineActorSnapshot> onlineActorSnapshots = new();
        private readonly List<RoOnlineActorSnapshot> portalActorSnapshots = new();
        private readonly HashSet<Vector2Int> onlineBlockedCells = new();
        private int currentAttackRangeCells;
        private int waypointIndex;
        private bool isGridPathMovement;
        private bool stepActive;
        private Vector3 stepStartPosition;
        private Vector3 stepEndPosition;
        private float stepElapsedSeconds;
        private float stepDurationSeconds;
        /// <summary>
        /// Distance walked so far (cells), the walk frame's phase: like the client's CGameActor +0x240 it is never reset,
        /// so a new path, a stop or a re-click continues the step instead of starting the cycle over.
        /// </summary>
        private float accumulatedWalkDistance;
        private RoSpriteMotion lastAppliedMotion = RoSpriteMotion.Idle;
        private RoActorDirection lastAppliedDirection = RoActorDirection.South;
        private bool hasAppliedActorMotion;
        private bool ownsPointerPositionActionEnable;
        private bool ownsPrimaryClickActionEnable;
        private bool ownsSecondaryClickActionEnable;
        private bool ownsHeadLookModifierActionEnable;
        private bool hasRightClickDownTarget;
        private uint rightClickDownServerId;
        private Vector2 rightClickDownPosition;
        private bool hasCachedSpritePointerTargets;
        private Vector2 cachedSpritePointerPosition;
        private RoPointerModifierFlags cachedSpritePointerModifiers;
        private bool cachedSpritePointerAllowLocalSelfTarget;
        private int cachedSpritePointerShapeCount;
        private int cachedSpritePointerFrame = -SpritePointerCacheMaxFrames - 1;
        private UnityEngine.Camera cachedInputCamera;
        private float nextInputCameraResolveTime;
        private float nextOnlineWorldControllerLookupTime;
        private float nextRuntimeMapControllerLookupTime;
        private float nextLoginShellLookupTime;
        private readonly RaycastHit[] pointerTargetHitBuffer = new RaycastHit[PointerTargetHitBufferSize];
        private readonly RaycastHit[] groundHitBuffer = new RaycastHit[PointerTargetHitBufferSize];
        private uint pendingAttackTargetId;
        private Vector2Int pendingAttackTargetCell;
        private bool hasPendingAttackTargetCell;
        private uint pendingPickupItemAid;
        private Vector2Int pendingPickupItemCell;
        private bool hasPendingPickupItemCell;
        private Vector3 pendingPickupWorldPosition;
        private bool hasPendingPickupWorldPosition;
        private float pendingPickupRetryStartTime;
        private bool hasPendingPickupRetryStartTime;
        private bool hasPendingTargetSkillUse;
        private int pendingTargetSkillId;
        private int pendingTargetSkillLevel;
        private uint pendingTargetSkillTargetId;
        private Vector2Int pendingTargetSkillTargetCell;
        private int pendingTargetSkillRangeCells;
        private float pendingTargetSkillSentTime;
        private bool pendingTargetSkillRequiresSend;
        private bool hasPendingOnlineMoveAck;
        private Vector2Int pendingOnlineMoveAckTargetCell;
        private float pendingOnlineMoveAckSentTime;
        private bool loggedPendingOnlineMoveAckTimeout;
        private bool hasObservedOwnServerMove;
        private uint observedOwnServerMoveStartTime;
        private RoPacketPositionPair observedOwnServerMovePath;
        private bool hasServerGatedMove;
        private uint serverGatedMoveStartTime;
        private RoPacketPositionPair serverGatedMovePath;
        private short serverGatedMoveSpeed;
        private float serverGatedMoveDurationSeconds;
        private float serverGatedPresentedElapsedSeconds;
        private Vector3 serverGatedCorrectionOffset;
        private float serverGatedCorrectionElapsedSeconds;
        private float serverGatedCorrectionDurationSeconds;
        private bool hasPendingServerGatedContinuation;
        private uint pendingServerGatedContinuationStartTime;
        private RoPacketPositionPair pendingServerGatedContinuationMovePath;
        private short pendingServerGatedContinuationSpeed;
        private bool hasQueuedFinalMoveTargetCell;
        private Vector2Int queuedFinalMoveTargetCell;
        private bool hasLocalActionMotion;
        private float localActionMotionStartTime;
        private float localActionMotionUntilTime;
        /// <summary>The running action hands over to the ready pose (players after attack motion 2 or a flinch), else to idle.</summary>
        private bool localActionSettlesToStandby;
        private RoSpriteMotion localActionMotion = RoSpriteMotion.Idle;
        private bool localActionHeldFirstFrame;
        private bool hasCombatStandby;
        private bool hasAppliedOnlineActionRevision;
        private int lastAppliedOnlineActionRevision;
        private bool hasVisualStopSettle;
        private Vector3 visualStopSettleStartWorld;
        private Vector3 visualStopSettleTargetWorld;
        private float visualStopSettleDuration;
        private float visualStopSettleStartedAt;
        private string visualStopSettleReason;
        private bool hasVisualStopSettleFacing;
        private RoActorDirection visualStopSettleFacing;
        private RoRuntimeMapController runtimeMapController;
        private readonly string[] runtimePerfTrace = new string[RuntimePerfTraceCapacity];
        private int runtimePerfTraceNextIndex;
        private int runtimePerfTraceCount;
        private long runtimePerfTraceSequence;
        private int lastMovementTraceSegmentIndex = -1;

        public RoPointerTarget CurrentPointerTarget { get; private set; } = RoPointerTarget.None;
        public RoPointerModifierFlags CurrentPointerModifiers { get; private set; }
        private RoMovementTuning MovementTuning => RoMovementTuning.Resolve(movementTuning);

        public int SnapshotRuntimePerfTrace(List<string> output) {
            if (output == null) {
                return 0;
            }

            int count = runtimePerfTraceCount;
            int start = runtimePerfTraceNextIndex - count;
            if (start < 0) {
                start += RuntimePerfTraceCapacity;
            }

            for (int i = 0; i < count; i++) {
                int index = (start + i) % RuntimePerfTraceCapacity;
                string entry = runtimePerfTrace[index];
                if (!string.IsNullOrEmpty(entry)) {
                    output.Add(entry);
                }
            }

            return count;
        }

        private void TraceRuntimePerf(string message) {
            if (!RoRuntimeDiagnostics.RuntimePerfLogEnabled) {
                return;
            }

            string entry =
                $"{++runtimePerfTraceSequence} t={GetPresentationTimeSeconds():F3} f={Time.frameCount} {message} | {FormatRuntimePerfState()}";
            runtimePerfTrace[runtimePerfTraceNextIndex] = entry;
            runtimePerfTraceNextIndex = (runtimePerfTraceNextIndex + 1) % RuntimePerfTraceCapacity;
            if (runtimePerfTraceCount < RuntimePerfTraceCapacity) {
                runtimePerfTraceCount++;
            }
        }

        private string FormatRuntimePerfState() {
            string pendingAck = hasPendingOnlineMoveAck
                ? $"pendingAck={FormatCell(pendingOnlineMoveAckTargetCell)} ageMs={(GetPresentationTimeSeconds() - pendingOnlineMoveAckSentTime) * 1000f:F0}"
                : "pendingAck=none";
            string pendingPickup = pendingPickupItemAid != 0
                ? $"pendingPickup={pendingPickupItemAid} cell={(hasPendingPickupItemCell ? FormatCell(pendingPickupItemCell) : "unknown")}"
                : "pendingPickup=none";
            string localMotion = hasLocalActionMotion
                ? $"localMotion={localActionMotion} remainingMs={Mathf.Max(0f, localActionMotionUntilTime - GetPresentationTimeSeconds()) * 1000f:F0}"
                : "localMotion=none";
            string queuedMove = hasQueuedFinalMoveTargetCell
                ? $"queuedMove={FormatCell(queuedFinalMoveTargetCell)}"
                : "queuedMove=none";
            string serverClock = FormatServerClockState();
            string actionLock = "actionLock=unknown";
            RoMinimalLoginShell shell = loginShell;
            if (shell != null && shell.TryGetCommandStateSnapshot(out RoShellCommandStateSnapshot commandState)) {
                actionLock = commandState.IsActionLocked
                    ? $"actionLock={commandState.ActionLockRemainingMilliseconds}ms reason={commandState.ActionLockReason}"
                    : "actionLock=none";
            }

            return
                $"grid={isGridPathMovement} step={stepActive} target={hasTarget} serverGated={hasServerGatedMove} {pendingAck} {pendingPickup} {queuedMove} {localMotion} {serverClock} {actionLock} facing={facingDirection} motion={lastAppliedMotion}";
        }

        private static string FormatCell(Vector2Int cell) {
            return $"{cell.x},{cell.y}";
        }

        private string FormatServerClockState() {
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller == null) {
                return "serverClock=none";
            }

            RoOnlineWorldState state = controller.State;
            bool hasEstimatedServerTime = controller.TryEstimateServerTimeMilliseconds(
                GetServerClockLocalTimeSeconds(),
                out uint estimatedServerTimeMilliseconds);
            string latestServerTime = state.HasServerTime
                ? state.LatestServerTimeMilliseconds.ToString()
                : "none";
            string estimatedServerTime = hasEstimatedServerTime
                ? estimatedServerTimeMilliseconds.ToString()
                : "none";
            return
                $"serverClock=hasState:{state.HasServerTime} latest:{latestServerTime} est:{estimatedServerTime} rev:{state.ServerTimeRevision}";
        }

        private string FormatMovementTuningTrace() {
            return MovementTuning.ToTraceSummary();
        }

        private void RecordLocalMovementTrace(
            RoMovementTraceEventType eventType,
            string detail,
            RoMovePresentationCorrectionMode correctionMode = RoMovePresentationCorrectionMode.None,
            float serverElapsedSeconds = RoMovementTraceRecord.UnsetFloat,
            float presentedElapsedSeconds = RoMovementTraceRecord.UnsetFloat,
            float distanceToAuthoritative = RoMovementTraceRecord.UnsetFloat,
            int segmentIndex = RoMovementTraceRecord.UnsetInt
        ) {
            if (!RoRuntimeDiagnostics.MovementTraceEnabled) {
                return;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller == null || !controller.IsMovementTraceActive) {
                return;
            }

            bool hasActor = TryGetOwnActor(out RoOnlineActorSnapshot actor);
            uint actorId = hasActor ? actor.ServerId : controller.OwnActorId;
            RoMapActorObjectType objectType = hasActor ? actor.ObjectType : RoMapActorObjectType.Player;
            bool isSelf = !hasActor || actor.IsSelf;
            int logicalCellX = hasActor ? actor.CellX : RoMovementTraceRecord.UnsetInt;
            int logicalCellY = hasActor ? actor.CellY : RoMovementTraceRecord.UnsetInt;
            bool hasDisplayCell = hasActor;
            int displayCellX = RoMovementTraceRecord.UnsetInt;
            int displayCellY = RoMovementTraceRecord.UnsetInt;
            uint moveStartTime = hasServerGatedMove
                ? serverGatedMoveStartTime
                : hasActor
                ? actor.MoveStartTime
                : 0;
            RoOnlineActorMotion onlineMotion = hasActor ? actor.Motion : RoOnlineActorMotion.Idle;
            if (hasActor) {
                Vector2Int displayCell = RoOnlineActorPresenter.GetDisplayCell(actor);
                displayCellX = displayCell.x;
                displayCellY = displayCell.y;
            } else if (gridSelector != null && gridSelector.TryGetCellAtWorld(transform.position, out Vector2Int visualCell)) {
                hasDisplayCell = true;
                displayCellX = visualCell.x;
                displayCellY = visualCell.y;
                logicalCellX = visualCell.x;
                logicalCellY = visualCell.y;
            }

            var record = new RoMovementTraceRecord(
                eventType,
                Time.frameCount,
                GetPresentationTimeSeconds(),
                GetPresentationDeltaSeconds(),
                actorId,
                objectType,
                isSelf,
                logicalCellX,
                logicalCellY,
                hasDisplayCell,
                displayCellX,
                displayCellY,
                transform.position,
                moveStartTime,
                serverElapsedSeconds,
                presentedElapsedSeconds,
                correctionMode,
                distanceToAuthoritative,
                segmentIndex,
                facingDirection,
                onlineMotion,
                lastAppliedMotion,
                accumulatedWalkDistance,
                detail);
            controller.TryRecordMovementTrace(record);
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

        private void RecordServerGatedMoveTrace(
            RoMovementSample sample,
            float serverElapsedSeconds,
            RoMovePresentationCorrectionMode correctionMode,
            float distanceToAuthoritative,
            string detail
        ) {
            if (!RoRuntimeDiagnostics.MovementTraceEnabled) {
                return;
            }

            int segmentIndex = sample.SegmentIndex;
            if (segmentIndex >= 0 && segmentIndex != lastMovementTraceSegmentIndex) {
                lastMovementTraceSegmentIndex = segmentIndex;
                RecordLocalMovementTrace(
                    RoMovementTraceEventType.SegmentTransition,
                    "local server-gated segment transition",
                    correctionMode,
                    serverElapsedSeconds,
                    sample.ElapsedSeconds,
                    distanceToAuthoritative,
                    segmentIndex);
            }

            if (correctionMode != RoMovePresentationCorrectionMode.None) {
                RecordLocalMovementTrace(
                    ToMovementTraceCorrectionEvent(correctionMode),
                    detail,
                    correctionMode,
                    serverElapsedSeconds,
                    sample.ElapsedSeconds,
                    distanceToAuthoritative,
                    segmentIndex);
            }

            RecordLocalMovementTrace(
                RoMovementTraceEventType.MovementSample,
                detail,
                correctionMode,
                serverElapsedSeconds,
                sample.ElapsedSeconds,
                distanceToAuthoritative,
                segmentIndex);
        }

        private static string FormatPointerTarget(RoPointerTarget target) {
            uint serverId = 0;
            if (target.GameObject != null) {
                RoOnlinePointerTargetMarker marker = target.GameObject.GetComponentInParent<RoOnlinePointerTargetMarker>();
                if (marker != null) {
                    serverId = marker.ServerId;
                }
            }

            return
                $"targetKind={target.Kind} serverId={serverId} cell={FormatCell(target.Cell)} label={target.Label ?? string.Empty}";
        }

        public void Initialize(UnityEngine.Camera camera, LayerMask mask) {
            inputCamera = camera;
            groundMask = mask;
            targetPosition = transform.position;
        }

        public void Initialize(UnityEngine.Camera camera, Collider ground) {
            inputCamera = camera;
            groundCollider = ground;
            targetPosition = transform.position;
        }

        public void SetGridSelector(RoGridSelector selector) {
            gridSelector = selector;
        }

        public void SetActorVisual(RoActorSpriteVisual visual) {
            actorVisual = visual;
            layeredActorVisual = null;
            hasAppliedActorMotion = false;
            UpdateActorMotion(RoSpriteMotion.Idle, facingDirection, force: true);
        }

        public void SetActorVisual(RoActorLayeredVisual visual) {
            layeredActorVisual = visual;
            actorVisual = null;
            hasAppliedActorMotion = false;
            UpdateActorMotion(RoSpriteMotion.Idle, facingDirection, force: true);
        }

        public void SetCursorStateController(RoCursorStateController controller) {
            cursorStateController = controller;
        }

        public void SetOnlineWorldController(RoOnlineWorldController controller) {
            onlineWorldController = controller;
        }

        public void SetLoginShell(RoMinimalLoginShell shell) {
            loginShell = shell;
        }

        public void SetAttackRangeCells(int rangeCells) {
            currentAttackRangeCells = Mathf.Max(0, rangeCells);
        }

        public void RecordPendingTargetSkillUse(int skillId, int skillLevel, uint targetId, int rangeCells) {
            if (targetId == 0 || rangeCells < 0) {
                ClearPendingTargetSkillUse();
                return;
            }

            if (!TryResolveOnlineActorCell(targetId, out Vector2Int targetCell)) {
                ClearPendingTargetSkillUse();
                return;
            }

            hasPendingTargetSkillUse = true;
            pendingTargetSkillId = skillId;
            pendingTargetSkillLevel = skillLevel;
            pendingTargetSkillTargetId = targetId;
            pendingTargetSkillTargetCell = targetCell;
            pendingTargetSkillRangeCells = rangeCells;
            pendingTargetSkillSentTime = GetPresentationTimeSeconds();
            pendingTargetSkillRequiresSend = false;
            TraceRuntimePerf($"target skill pending skill={skillId} lv={skillLevel} target={targetId} cell={FormatCell(targetCell)} range={rangeCells}");
        }

        public bool TrySubmitTargetSkillUse(int skillId, int skillLevel, uint targetId, int rangeCells) {
            if (skillId <= 0 || skillLevel <= 0 || targetId == 0 || targetId > int.MaxValue || rangeCells < 0) {
                return false;
            }

            if (ResolveLoginShell(forceLookup: true) == null) {
                return false;
            }

            if (!TryResolveOnlineActorCell(targetId, out Vector2Int targetCell) ||
                !TryResolveOwnGameplayCell(out Vector2Int casterCell)) {
                ClearPendingTargetSkillUse();
                return false;
            }

            if (IsOwnActorMovementOrInteractionBlocked()) {
                ClearPendingTargetAction();
                ClearLocalMovement();
                return true;
            }

            SetPendingTargetSkillSend(skillId, skillLevel, targetId, targetCell, rangeCells);
            if (IsWithinServerPlayerSkillRange(casterCell, targetCell, rangeCells)) {
                TrySendPendingTargetSkill(casterCell, targetCell);
                return true;
            }

            if (!TryFindTargetSkillApproachCell(casterCell, targetCell, rangeCells, out Vector2Int approachCell) ||
                !TrySetGridPath(approachCell)) {
                ClearPendingTargetAction();
                return false;
            }

            return true;
        }

        public void SnapToWorldPosition(Vector3 worldPosition) {
            SnapToAuthoritativeWorldPosition(worldPosition, null);
        }

        public void ApplyAuthoritativeStopCell(Vector2Int cell, RoActorDirection? facing, uint placementServerTime = 0) {
            RecordLocalMovementTrace(
                RoMovementTraceEventType.StopMove,
                $"STOPMOVE cell={FormatCell(cell)} placementServerTime={placementServerTime}");
            BeginVisualStopSettleOrHardSnap(
                ResolveCellWorldPosition(cell, groundOffset),
                facing,
                placementServerTime,
                VisualStopSettleReasonStopMove);
        }

        public void SnapToAuthoritativeWorldPosition(
            Vector3 worldPosition,
            RoActorDirection? facing,
            uint placementServerTime = 0,
            string reason = "HardSnap"
        ) {
            RecordLocalMovementTrace(
                RoMovementTraceEventType.HardSnap,
                $"reason={reason} hard snap to={worldPosition.x:F3},{worldPosition.y:F3},{worldPosition.z:F3} placementServerTime={placementServerTime}",
                RoMovePresentationCorrectionMode.HardSnap,
                distanceToAuthoritative: RoMovePresentation.FlatDistance(transform.position, worldPosition));
            ClearPendingTargetAction();
            ClearLocalMovement();
            ClearObservedOwnServerMove();
            lastMovementTraceSegmentIndex = -1;
            hasSubmittedHeldGridCell = false;
            hasSubmittedHeldHeadLookDirection = false;
            targetPosition = worldPosition;
            stepStartPosition = worldPosition;
            stepEndPosition = worldPosition;
            transform.position = worldPosition;
            if (facing.HasValue && facing.Value != RoActorDirection.None) {
                ApplyFacingDirection(facing.Value);
            }

            hasLocalActionMotion = false;
            localActionMotionStartTime = 0f;
            localActionMotionUntilTime = 0f;
            localActionMotion = RoSpriteMotion.Idle;
            hasCombatStandby = false;
            UpdateActorMotion(RoSpriteMotion.Idle, facingDirection, force: true);
        }

        private void BeginVisualStopSettleOrHardSnap(
            Vector3 worldPosition,
            RoActorDirection? facing,
            uint placementServerTime,
            string reason
        ) {
            float distanceToAuthoritative = IsFinite(transform.position) && IsFinite(worldPosition)
                ? RoMovePresentation.FlatDistance(transform.position, worldPosition)
                : float.PositiveInfinity;
            float hardSnapCells = ResolveVisualStopSettleHardSnapCells();
            float durationSeconds = Mathf.Clamp(ResolveVisualStopSettleDurationSeconds(), 0.05f, 0.10f);
            bool canSettle = IsFinite(transform.position) &&
                IsFinite(worldPosition) &&
                !ShouldHardSnapVisualStop(distanceToAuthoritative, hardSnapCells) &&
                durationSeconds > 0f;

            if (!canSettle) {
                SnapToAuthoritativeWorldPosition(
                    worldPosition,
                    facing,
                    placementServerTime,
                    VisualStopSettleReasonHardSnap);
                return;
            }

            Vector3 startWorld = transform.position;
            float preservedWalkDistance = accumulatedWalkDistance;
            ClearPendingTargetAction();
            ClearLocalMovement();
            accumulatedWalkDistance = preservedWalkDistance;
            ClearObservedOwnServerMove();
            lastMovementTraceSegmentIndex = -1;
            hasSubmittedHeldGridCell = false;
            hasSubmittedHeldHeadLookDirection = false;
            targetPosition = worldPosition;
            stepStartPosition = startWorld;
            stepEndPosition = worldPosition;
            hasLocalActionMotion = false;
            localActionMotionStartTime = 0f;
            localActionMotionUntilTime = 0f;
            localActionMotion = RoSpriteMotion.Idle;
            hasCombatStandby = false;

            hasVisualStopSettle = true;
            visualStopSettleStartWorld = startWorld;
            visualStopSettleTargetWorld = worldPosition;
            visualStopSettleDuration = durationSeconds;
            visualStopSettleStartedAt = GetPresentationTimeSeconds();
            visualStopSettleReason = reason ?? string.Empty;
            hasVisualStopSettleFacing = facing.HasValue && facing.Value != RoActorDirection.None;
            visualStopSettleFacing = hasVisualStopSettleFacing ? facing.Value : facingDirection;

            if (hasVisualStopSettleFacing) {
                ApplyFacingDirection(visualStopSettleFacing);
            }

            UpdateActorMotion(RoSpriteMotion.Idle, facingDirection, force: true);
            RecordLocalMovementTrace(
                RoMovementTraceEventType.SettleCorrection,
                $"reason={visualStopSettleReason} start={startWorld.x:F3},{startWorld.y:F3},{startWorld.z:F3} target={worldPosition.x:F3},{worldPosition.y:F3},{worldPosition.z:F3} duration={durationSeconds:F3} hardSnapCells={hardSnapCells:F3} placementServerTime={placementServerTime} {FormatMovementTuningTrace()}",
                RoMovePresentationCorrectionMode.SoftCorrection,
                distanceToAuthoritative: distanceToAuthoritative);
        }

        private bool UpdateVisualStopSettle() {
            if (!hasVisualStopSettle) {
                return false;
            }

            float elapsedSeconds = Mathf.Max(0f, GetPresentationTimeSeconds() - visualStopSettleStartedAt);
            float progress = visualStopSettleDuration > 0f
                ? Mathf.Clamp01(elapsedSeconds / visualStopSettleDuration)
                : 1f;
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 previousPosition = transform.position;
            transform.position = Vector3.Lerp(visualStopSettleStartWorld, visualStopSettleTargetWorld, easedProgress);
            ApplyWalkAnimationDistance(previousPosition, transform.position);

            if (progress >= 1f) {
                transform.position = visualStopSettleTargetWorld;
                RecordLocalMovementTrace(
                    RoMovementTraceEventType.SettleCorrection,
                    $"reason={visualStopSettleReason} complete",
                    RoMovePresentationCorrectionMode.SoftCorrection,
                    distanceToAuthoritative: 0f);
                ClearVisualStopSettle();
            }

            return true;
        }

        private void ClearVisualStopSettle() {
            hasVisualStopSettle = false;
            visualStopSettleStartWorld = default;
            visualStopSettleTargetWorld = default;
            visualStopSettleDuration = 0f;
            visualStopSettleStartedAt = 0f;
            visualStopSettleReason = null;
            hasVisualStopSettleFacing = false;
            visualStopSettleFacing = RoActorDirection.None;
        }

        private static bool IsFinite(Vector3 value) {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void ApplyFacingDirection(RoActorDirection direction) {
            if (direction == RoActorDirection.None) {
                return;
            }

            facingDirection = direction;
            Vector3 look = ResolveFacingWorldDelta(facingDirection);
            if (look.sqrMagnitude > 0.001f) {
                transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }
        }

        public void SetInputActions(
            InputActionReference pointerPosition,
            InputActionReference primaryClick,
            InputActionReference headLookModifier,
            InputActionReference secondaryClick = null
        ) {
            DisableConfiguredActions();
            pointerPositionAction = pointerPosition;
            primaryClickAction = primaryClick;
            secondaryClickAction = secondaryClick;
            headLookModifierAction = headLookModifier;
            EnableConfiguredActions();
        }

        private void OnEnable() {
            useGUILayout = false;
            EnableConfiguredActions();
        }

        private void OnDisable() {
            DisableConfiguredActions();
            wasLeftPressed = false;
            wasRightPressed = false;
            wasHeadLookModifierPressed = false;
            ClearRightClickContextState();
            clickIntentTracker.Reset();
            hasSubmittedHeldGridCell = false;
            hasSubmittedHeldHeadLookDirection = false;
            ClearVisualStopSettle();
            CurrentPointerTarget = RoPointerTarget.None;
            CurrentPointerModifiers = RoPointerModifierFlags.None;
        }

        private void Update() {
            long updateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                ResolveLoginShell(forceLookup: true)?.FlushGameplayLiveDrains();
                TryStartOwnMoveFromWalkOk();
                TryContinueQueuedOnlineMove();
                TryObserveOnlineMoveAckTimeout();
                ExpirePendingTargetSkillUse();
                long handleClickStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
                try {
                    HandleClick();
                } finally {
                    RoRuntimePerfCounters.AddClickHandleTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(handleClickStartTicks));
                }

                UpdateVisualStopSettle();
                long moveToTargetStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
                try {
                    MoveToTarget();
                } finally {
                    RoRuntimePerfCounters.AddClickMoveToTargetTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(moveToTargetStartTicks));
                }

                TrySubmitPendingTargetAction();
                ExpireLocalActionMotion();
            } finally {
                RoRuntimePerfCounters.AddClickToMoveUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(updateStartTicks));
            }
        }

        private void OnGUI() {
            long onGuiStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            Event current = Event.current;
            RoRuntimePerfCounters.IncrementOnGuiEvent(current != null ? (int)current.type : -1);
            try {
                if (IsPointerPositionActionActive()) {
                    return;
                }

                RoPointerScreenPosition.ReportGuiMousePosition(current);
            } finally {
                long elapsedTicks = RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(onGuiStartTicks);
                RoRuntimePerfCounters.AddOnGuiTicks(elapsedTicks);
                RoRuntimePerfCounters.AddClickToMoveOnGuiTicks(elapsedTicks);
            }
        }

        private void HandleClick() {
            if (!TryReadPointerPosition(out Vector2 pointerPosition)) {
                CurrentPointerTarget = RoPointerTarget.None;
                CurrentPointerModifiers = RoPointerModifierFlags.None;
                if (wasRightPressed) {
                    wasRightPressed = false;
                    ClearRightClickContextState();
                }

                if (wasHeadLookModifierPressed) {
                    wasHeadLookModifierPressed = false;
                    hasSubmittedHeldHeadLookDirection = false;
                }

                clickIntentTracker.Reset();
                return;
            }

            bool isPointerCaptured = RoUiPointerCapture.IsPointerCaptured(pointerPosition);
            bool isLeftPressed = IsPrimaryClickPressed();
            bool isRightPressed = IsSecondaryClickPressed();
            bool wasPressed = wasLeftPressed;
            bool wasSecondaryPressed = wasRightPressed;
            bool releasedThisFrame = !isLeftPressed && wasPressed;
            bool rightPressedThisFrame = isRightPressed && !wasSecondaryPressed;
            bool rightReleasedThisFrame = !isRightPressed && wasSecondaryPressed;
            bool isHeadLookModifierPressed = IsHeadLookModifierPressed();
            bool releasedHeadLookModifier = !isHeadLookModifierPressed && wasHeadLookModifierPressed;

            UnityEngine.Camera camera = ResolveInputCamera();
            RoPointerModifierFlags modifiers = BuildPointerModifiers(isLeftPressed, isRightPressed, isHeadLookModifierPressed);
            Ray ray = default;
            RoPointerTarget pointerTarget = RoPointerTarget.None;
            RoPointerTarget resolvedPointerTarget = RoPointerTarget.None;
            bool hasPointerTargetPoint = false;
            Vector3 pointerTargetPoint = default;
            RoActorDirection pointerTargetDirection = RoActorDirection.None;
            bool isSitting = false;
            RoSkillTargetSelection activeSkillTargetSelection = ResolveSkillTargetSelectionOverlay();
            bool hasSkillTargetSelection = activeSkillTargetSelection != null && activeSkillTargetSelection.HasActiveSelection;
            bool allowLocalSelfTarget = AllowsLocalSelfPointerTarget(activeSkillTargetSelection);
            RoPointerModifierFlags pointerResolutionModifiers = modifiers;
            if (releasedThisFrame && hasSkillTargetSelection) {
                pointerResolutionModifiers |= RoPointerModifierFlags.PrimaryHeld;
            }

            if (rightReleasedThisFrame) {
                pointerResolutionModifiers |= RoPointerModifierFlags.SecondaryHeld;
            }

            if (isPointerCaptured) {
                resolvedPointerTarget = RoPointerTarget.Ui("captured ui");
                long cursorSetTargetStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
                try {
                    cursorStateController?.SetPointerTarget(resolvedPointerTarget, modifiers, "ui-pointer-capture");
                } finally {
                    RoRuntimePerfCounters.AddCursorSetPointerTargetTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(cursorSetTargetStartTicks));
                }
            } else if (camera != null) {
                ray = camera.ScreenPointToRay(pointerPosition);
                pointerTarget = ResolvePointerTarget(
                    pointerPosition,
                    camera,
                    ray,
                    pointerResolutionModifiers,
                    allowLocalSelfTarget);
                resolvedPointerTarget = pointerTarget;
                long cursorSetTargetStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
                try {
                    cursorStateController?.SetPointerTarget(pointerTarget, pointerResolutionModifiers, "click-to-move");
                } finally {
                    RoRuntimePerfCounters.AddCursorSetPointerTargetTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(cursorSetTargetStartTicks));
                }

                if (hasSkillTargetSelection) {
                    cursorStateController?.SetState(RoCursorState.Target, "skill-target-selection");
                }

                hasPointerTargetPoint = TryGetPointerTargetPoint(pointerTarget, ray, out pointerTargetPoint);
                pointerTargetDirection = hasPointerTargetPoint
                    ? GetHeadLookTargetDirection(pointerTargetPoint)
                    : RoActorDirection.None;
                isSitting = isLeftPressed &&
                    TryGetOwnActor(out RoOnlineActorSnapshot actor) &&
                    RoOnlineActorInteractionRules.IsSitting(actor);
            }

            CurrentPointerTarget = resolvedPointerTarget;
            CurrentPointerModifiers = modifiers;
            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled && isLeftPressed && !wasPressed) {
                Ragnarok.Diagnostics.RoSkillSweepProbe.Record(
                    $"click press captured={isPointerCaptured} target={resolvedPointerTarget.Kind} go={(resolvedPointerTarget.GameObject != null ? resolvedPointerTarget.GameObject.name : "-")} cell={resolvedPointerTarget.Cell}");
            }

            if (HandleRightClickContextMenu(
                    pointerTarget,
                    pointerPosition,
                    modifiers,
                    isPointerCaptured,
                    hasSkillTargetSelection,
                    rightPressedThisFrame,
                    rightReleasedThisFrame)) {
                wasLeftPressed = isLeftPressed;
                wasRightPressed = isRightPressed;
                wasHeadLookModifierPressed = isHeadLookModifierPressed;
                return;
            }

            RoClickIntentContext clickIntentContext = new RoClickIntentContext(
                hasPointerPosition: true,
                isPointerCaptured: isPointerCaptured,
                hasCamera: camera != null,
                isPrimaryHeld: isLeftPressed,
                wasPrimaryHeld: wasPressed,
                isSecondaryHeld: isRightPressed,
                wasSecondaryHeld: wasSecondaryPressed,
                isHeadLookModifierHeld: isHeadLookModifierPressed,
                hasGridSelector: gridSelector != null,
                isSitting: isSitting,
                hasSelectedSkill: hasSkillTargetSelection,
                selectedSkillTargetsTile: hasSkillTargetSelection && activeSkillTargetSelection.TargetsGround,
                selectedSkillTargetsEntity: hasSkillTargetSelection && activeSkillTargetSelection.TargetsEntity,
                canSubmitPointerTargetClick: CanSubmitOnlinePointerTargetClick(pointerTarget),
                hasPointerTargetPoint: hasPointerTargetPoint,
                pointerTargetPoint: pointerTargetPoint,
                pointerTargetDirection: pointerTargetDirection,
                pointerTarget: pointerTarget,
                hasSubmittedHeldGridCell: hasSubmittedHeldGridCell,
                submittedHeldGridCell: submittedHeldGridCell,
                hasSubmittedHeldHeadLookDirection: hasSubmittedHeldHeadLookDirection,
                submittedHeldHeadLookDirection: submittedHeldHeadLookDirection,
                pointerModifiers: modifiers
            );
            RoClickIntent intent;
            long clickIntentResolveStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                intent = RoClickIntentResolver.Resolve(clickIntentContext, ref clickIntentTracker);
            } finally {
                RoRuntimePerfCounters.AddClickIntentResolveTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(clickIntentResolveStartTicks));
            }

            if (intent.Kind == RoClickIntentKind.BlockedByUi) {
                wasLeftPressed = isLeftPressed;
                wasRightPressed = isRightPressed;
                wasHeadLookModifierPressed = isHeadLookModifierPressed;
                hasSubmittedHeldGridCell = false;
                hasSubmittedHeldHeadLookDirection = false;
                return;
            }

            wasLeftPressed = isLeftPressed;
            wasRightPressed = isRightPressed;
            if (releasedThisFrame) {
                hasSubmittedHeldGridCell = false;
                hasSubmittedHeldHeadLookDirection = false;
            }

            wasHeadLookModifierPressed = isHeadLookModifierPressed;
            if (releasedHeadLookModifier) {
                hasSubmittedHeldHeadLookDirection = false;
            }

            if (hasSkillTargetSelection) {
                switch (intent.Kind) {
                    case RoClickIntentKind.CastOnTile:
                        activeSkillTargetSelection.TrySubmitGroundTarget(intent.Target.Cell);
                        return;
                    case RoClickIntentKind.CastOnEntity:
                        activeSkillTargetSelection.TrySubmitEntityTarget(intent.Target, intent.Modifiers);
                        return;
                    default:
                        if (releasedThisFrame) {
                            if (activeSkillTargetSelection.TargetsGround &&
                                TryResolveGroundSkillCell(ray, out Vector2Int groundSkillCell)) {
                                activeSkillTargetSelection.TrySubmitGroundTarget(groundSkillCell);
                            } else {
                                activeSkillTargetSelection.Cancel();
                            }
                        }

                        return;
                }
            }

            switch (intent.Kind) {
                case RoClickIntentKind.SittingHeadLook:
                    ClearPendingTargetAction();
                    ClearLocalMovement();
                    if (intent.ShouldApplyLook) {
                        submittedHeldHeadLookDirection = intent.LookDirection;
                        hasSubmittedHeldHeadLookDirection = true;
                        ApplyHeadLook(intent.LookPoint);
                    }

                    return;

                case RoClickIntentKind.InteractEntity:
                case RoClickIntentKind.PickUpItem:
                    TraceRuntimePerf($"pointer target click intent={intent.Kind} {FormatPointerTarget(intent.Target)}");
                    TrySubmitOnlinePointerTargetClick(intent.Target, intent.Modifiers);
                    return;

                case RoClickIntentKind.HeadLook:
                    if (intent.ShouldApplyLook) {
                        submittedHeldHeadLookDirection = intent.LookDirection;
                        hasSubmittedHeldHeadLookDirection = true;
                        ApplyHeadLook(intent.LookPoint);
                    }

                    return;

                case RoClickIntentKind.WalkToGround:
                    // Throttle held re-issues (cursor straddling a cell boundary churns the destination and
                    // flicks the first-step heading). The first click of a hold (hasSubmittedHeldGridCell ==
                    // false) is never throttled; while throttled we keep submittedHeldGridCell untouched so
                    // the latest cursor cell is picked up once the window elapses.
                    if (ShouldThrottleHeldGridMove(hasSubmittedHeldGridCell, GetPresentationTimeSeconds(), nextHeldGridMoveTime)) {
                        return;
                    }

                    TraceRuntimePerf($"walk-to-ground click cell={FormatCell(intent.Cell)}");
                    RecordLocalMovementTrace(
                        RoMovementTraceEventType.Click,
                        $"walk-to-ground cell={FormatCell(intent.Cell)}");
                    ClearPendingTargetAction();
                    submittedHeldGridCell = intent.Cell;
                    hasSubmittedHeldGridCell = true;
                    nextHeldGridMoveTime = GetPresentationTimeSeconds() + Mathf.Max(0f, heldGridMoveThrottleSeconds);
                    if (!TrySetGridPath(intent.Cell)) {
                        TraceRuntimePerf($"walk-to-ground path rejected cell={FormatCell(intent.Cell)}");
                    }
                    return;

                case RoClickIntentKind.WalkToWorldPoint:
                    if (TryRaycastGround(ray, out RaycastHit hit)) {
                        if (IsOwnActorMovementOrInteractionBlocked()) {
                            ClearPendingTargetAction();
                            ClearLocalMovement();
                            return;
                        }

                        TraceRuntimePerf($"walk-to-world click point={hit.point.x:F2},{hit.point.z:F2}");
                        RecordLocalMovementTrace(
                            RoMovementTraceEventType.Click,
                            $"walk-to-world point={hit.point.x:F3},{hit.point.y:F3},{hit.point.z:F3}");
                        ClearPendingTargetAction();
                        ClearVisualStopSettle();
                        isGridPathMovement = false;
                        stepActive = false;
                        ClearServerGatedMove();
                        waypoints.Clear();
                        waypointIndex = 0;
                        targetPosition = hit.point;
                        if (lockYToGround) {
                            targetPosition.y += groundOffset;
                        } else {
                            targetPosition.y = transform.position.y;
                        }

                        hasTarget = true;
                        UpdateTargetMarker(targetPosition);
                    }

                    return;

                case RoClickIntentKind.CastOnTile:
                case RoClickIntentKind.CastOnEntity:
                case RoClickIntentKind.RotateCamera:
                case RoClickIntentKind.Idle:
                default:
                    return;
            }
        }

        private RoPointerModifierFlags BuildPointerModifiers(bool primaryHeld, bool secondaryHeld, bool shiftHeld) {
            RoPointerModifierFlags modifiers = RoPointerModifierFlags.None;
            if (primaryHeld) {
                modifiers |= RoPointerModifierFlags.PrimaryHeld;
            }

            if (secondaryHeld) {
                modifiers |= RoPointerModifierFlags.SecondaryHeld;
            }

            if (shiftHeld) {
                modifiers |= RoPointerModifierFlags.Shift;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) {
                if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) {
                    modifiers |= RoPointerModifierFlags.Ctrl;
                }

                if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed) {
                    modifiers |= RoPointerModifierFlags.Alt;
                }
            }

            return modifiers;
        }

        private bool HandleRightClickContextMenu(
            RoPointerTarget pointerTarget,
            Vector2 pointerPosition,
            RoPointerModifierFlags modifiers,
            bool isPointerCaptured,
            bool hasSkillTargetSelection,
            bool rightPressedThisFrame,
            bool rightReleasedThisFrame
        ) {
            if (rightPressedThisFrame) {
                if (!isPointerCaptured && TryResolveContextPlayerTarget(pointerTarget, out RoOnlinePointerTargetMarker pressedMarker, out _)) {
                    hasRightClickDownTarget = true;
                    rightClickDownServerId = pressedMarker.ServerId;
                    rightClickDownPosition = pointerPosition;
                } else {
                    ClearRightClickContextState();
                }
            }

            if (!rightReleasedThisFrame) {
                return false;
            }

            bool hadDownTarget = hasRightClickDownTarget;
            uint downServerId = rightClickDownServerId;
            Vector2 downPosition = rightClickDownPosition;
            ClearRightClickContextState();

            if (!hadDownTarget ||
                isPointerCaptured ||
                hasSkillTargetSelection ||
                (modifiers & RoPointerModifierFlags.Shift) != 0 ||
                Vector2.SqrMagnitude(pointerPosition - downPosition) > ContextMenuRightClickMaxMovePixels * ContextMenuRightClickMaxMovePixels) {
                return false;
            }

            bool hasReleasedTarget = TryResolveContextPlayerTarget(
                pointerTarget,
                out RoOnlinePointerTargetMarker releasedMarker,
                out RoOnlineActorSnapshot actor);
            if (hasReleasedTarget && releasedMarker.ServerId != downServerId) {
                return false;
            }

            RoPointerModifierFlags clickModifiers = modifiers & ~RoPointerModifierFlags.SecondaryHeld;
            if (!hasReleasedTarget && !TryResolveContextPlayerActor(downServerId, out actor)) {
                return false;
            }

            if (hasReleasedTarget) {
                releasedMarker.SubmitPointerClick(pointerTarget, clickModifiers);
            } else {
                ResolveOnlineWorldController(forceLookup: true)?.SelectOnlineTarget(downServerId, RoPointerTarget.None, clickModifiers);
            }

            ResolveLoginShell(forceLookup: true)?.ShowPlayerContextMenu(pointerPosition, actor);
            return true;
        }

        private bool TryResolveContextPlayerTarget(
            RoPointerTarget pointerTarget,
            out RoOnlinePointerTargetMarker marker,
            out RoOnlineActorSnapshot actor
        ) {
            marker = null;
            actor = default;
            if (!pointerTarget.IsValid ||
                pointerTarget.Kind != RoPointerTargetKind.Player ||
                pointerTarget.GameObject == null) {
                return false;
            }

            marker = pointerTarget.GameObject.GetComponentInParent<RoOnlinePointerTargetMarker>();
            if (marker == null || marker.ServerId == 0 || marker.ObjectType != RoMapActorObjectType.Player) {
                return false;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller == null ||
                !controller.State.TryGetActor(marker.ServerId, out actor) ||
                actor.ObjectType != RoMapActorObjectType.Player ||
                actor.IsSelf ||
                (controller.OwnActorId != 0 && marker.ServerId == controller.OwnActorId)) {
                return false;
            }

            return true;
        }

        private bool TryResolveContextPlayerActor(uint serverId, out RoOnlineActorSnapshot actor) {
            actor = default;
            if (serverId == 0) {
                return false;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            return controller != null &&
                controller.State.TryGetActor(serverId, out actor) &&
                actor.ObjectType == RoMapActorObjectType.Player &&
                !actor.IsSelf &&
                (controller.OwnActorId == 0 || serverId != controller.OwnActorId);
        }

        private void ClearRightClickContextState() {
            hasRightClickDownTarget = false;
            rightClickDownServerId = 0;
            rightClickDownPosition = default;
        }

        private RoPointerTarget ResolvePointerTarget(
            Vector2 pointerPosition,
            UnityEngine.Camera camera,
            Ray ray,
            RoPointerModifierFlags modifiers,
            bool allowLocalSelfTarget
        ) {
            RoRuntimePerfCounters.IncrementPointerResolveTarget();
            long pointerResolveStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                using (RoProfilerMarkers.PointerResolveTarget.Auto()) {
                    pointerTargetCandidates.Clear();
                    int worldTargetStart = pointerTargetCandidates.Count;
                    int activeSpriteShapeCount = RoSpritePointerTargetShape.ActiveShapes.Count;
                    bool useSpriteColliderPath = ShouldUseSpriteColliderPath(activeSpriteShapeCount);
                    CollectWorldPointerTargets(
                        pointerPosition,
                        camera,
                        ray,
                        modifiers,
                        allowLocalSelfTarget,
                        useSpriteColliderPath,
                        pointerTargetCandidates);
                    bool hasColliderCharacterTarget = useSpriteColliderPath &&
                        HasColliderCharacterTarget(pointerTargetCandidates, worldTargetStart);
                    RoRuntimePerfCounters.SetActiveSpritePointerShapeCount(activeSpriteShapeCount);
                    if (ShouldCollectSpritePointerTargets(
                            hasColliderCharacterTarget,
                            modifiers,
                            forceSpriteShapeTargets: !useSpriteColliderPath)) {
                        CollectSpritePointerTargets(camera, pointerPosition, modifiers, allowLocalSelfTarget, pointerTargetCandidates);
                    }

                    if (TryResolvePortalPointerTarget(pointerPosition, camera, modifiers, out RoPointerTarget portalTarget)) {
                        pointerTargetCandidates.Add(portalTarget);
                    }

                    if (gridSelector != null && TryGetPointerGroundPoint(ray, out Vector3 groundPoint)) {
                        if (gridSelector.TryGetCellAtWorld(groundPoint, out Vector2Int cell)) {
                            bool isWalkable = gridSelector.IsCellWalkable(cell);
                            Vector3 center = gridSelector.GetCellWorldCenter(cell, groundOffset);
                            pointerTargetCandidates.Add(RoPointerTarget.GroundCell(cell, center, isWalkable));
                        }
                    }

                    long pointerResolverStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
                    try {
                        return RoPointerTargetResolver.Resolve(pointerTargetCandidates, modifiers);
                    } finally {
                        RoRuntimePerfCounters.AddPointerResolverTicks(
                            RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(pointerResolverStartTicks));
                    }
                }
            } finally {
                RoRuntimePerfCounters.AddPointerResolveTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(pointerResolveStartTicks));
            }
        }

        private void CollectWorldPointerTargets(
            Vector2 pointerPosition,
            UnityEngine.Camera camera,
            Ray ray,
            RoPointerModifierFlags modifiers,
            bool allowLocalSelfTarget,
            bool useSpriteColliderPath,
            List<RoPointerTarget> targets
        ) {
            long pointerWorldTargetsStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                bool colliderRuntimeChanged = RoSpritePointerBoundsCollider.SetRuntimeCollidersEnabled(useSpriteColliderPath);
                bool colliderTransformsChanged = false;
                if (useSpriteColliderPath) {
                    colliderTransformsChanged = RoSpritePointerBoundsCollider.RefreshActiveColliders();
                }

                if (colliderRuntimeChanged ||
                    (useSpriteColliderPath && (!useOptimizedPointerColliderRefresh || colliderTransformsChanged))) {
                    RoRuntimePerfCounters.IncrementPointerSyncTransforms();
                    using (RoProfilerMarkers.PointerSyncTransforms.Auto()) {
                        Physics.SyncTransforms();
                    }
                }

                int hitCount = 0;
                long pointerRaycastStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
                try {
                    using (RoProfilerMarkers.PointerRaycast.Auto())
                    using (PointerRaycastMarker.Auto()) {
                        hitCount = Physics.RaycastNonAlloc(
                            ray,
                            pointerTargetHitBuffer,
                            2000f,
                            pointerTargetMask,
                            QueryTriggerInteraction.Collide);
                    }
                } finally {
                    RoRuntimePerfCounters.AddPointerRaycastTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(pointerRaycastStartTicks));
                }

                RoRuntimePerfCounters.IncrementSpritePointerColliderRaycast();
                int acceptedHits = 0;
                for (int i = 0; i < hitCount; i++) {
                    RaycastHit hit = pointerTargetHitBuffer[i];
                    Collider collider = hit.collider;
                    if (collider == null) {
                        continue;
                    }

                    RoPointerTargetMarker marker = collider.GetComponentInParent<RoPointerTargetMarker>();
                    if (marker == null) {
                        continue;
                    }

                    RoPointerTarget target = marker.BuildTarget(hit, modifiers);
                    if (!ShouldKeepPointerTargetForClick(target, allowLocalSelfTarget)) {
                        continue;
                    }

                    if (!TryConfirmSpritePointerColliderTarget(
                            marker,
                            camera,
                            pointerPosition,
                            modifiers,
                            allowLocalSelfTarget,
                            ref target)) {
                        continue;
                    }

                    targets.Add(target);
                    acceptedHits++;
                }

                RoRuntimePerfCounters.AddSpritePointerColliderHits(acceptedHits);
            } finally {
                RoRuntimePerfCounters.AddPointerWorldTargetsTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(pointerWorldTargetsStartTicks));
            }
        }

        private static bool ShouldCollectSpritePointerTargets(
            bool hasColliderCharacterTarget,
            RoPointerModifierFlags modifiers,
            bool forceSpriteShapeTargets
        ) {
            if (forceSpriteShapeTargets) {
                return true;
            }

            RoPointerTargetingMode mode = RoSpritePointerTargetShape.GlobalTargetingMode;
            if (mode == RoPointerTargetingMode.AlphaExact) {
                return true;
            }

            if (mode == RoPointerTargetingMode.ColliderOnlyFast) {
                return false;
            }

            if (mode == RoPointerTargetingMode.ColliderFirst && hasColliderCharacterTarget) {
                return false;
            }

            return (modifiers & (RoPointerModifierFlags.PrimaryHeld | RoPointerModifierFlags.SecondaryHeld)) != 0;
        }

        private bool ShouldUseSpriteColliderPath(int activeSpriteShapeCount) {
            if (!useOptimizedPointerColliderRefresh) {
                return true;
            }

            RoPointerTargetingMode mode = RoSpritePointerTargetShape.GlobalTargetingMode;
            if (mode == RoPointerTargetingMode.AlphaExact) {
                return false;
            }

            if (mode == RoPointerTargetingMode.ColliderOnlyFast) {
                return true;
            }

            if (!useAdaptiveSpritePointerTargeting || spritePointerColliderCrowdThreshold <= 0) {
                return true;
            }

            return activeSpriteShapeCount < spritePointerColliderCrowdThreshold;
        }

        private static bool HasColliderCharacterTarget(List<RoPointerTarget> targets, int startIndex) {
            if (targets == null) {
                return false;
            }

            for (int i = Mathf.Max(0, startIndex); i < targets.Count; i++) {
                switch (targets[i].Kind) {
                    case RoPointerTargetKind.Item:
                    case RoPointerTargetKind.Player:
                    case RoPointerTargetKind.Monster:
                    case RoPointerTargetKind.Npc:
                        return true;
                }
            }

            return false;
        }

        private bool TryConfirmSpritePointerColliderTarget(
            RoPointerTargetMarker marker,
            UnityEngine.Camera camera,
            Vector2 pointerPosition,
            RoPointerModifierFlags modifiers,
            bool allowLocalSelfTarget,
            ref RoPointerTarget target
        ) {
            if (marker == null ||
                camera == null ||
                RoSpritePointerTargetShape.GlobalTargetingMode == RoPointerTargetingMode.ColliderOnlyFast ||
                !IsSpritePointerCharacterTarget(target.Kind)) {
                return true;
            }

            RoSpritePointerTargetShape shape = marker.FindComponent<RoSpritePointerTargetShape>();
            if (shape == null || !shape.isActiveAndEnabled) {
                return true;
            }

            RoPointerTarget spriteTarget;
            using (RoProfilerMarkers.PointerSpriteExact.Auto()) {
                if (!shape.TryBuildTarget(camera, pointerPosition, modifiers, out spriteTarget, out _)) {
                    return false;
                }
            }

            if (!ShouldKeepPointerTargetForClick(spriteTarget, allowLocalSelfTarget)) {
                return false;
            }

            target = spriteTarget;
            return true;
        }

        private static bool IsSpritePointerCharacterTarget(RoPointerTargetKind kind) {
            switch (kind) {
                case RoPointerTargetKind.Player:
                case RoPointerTargetKind.Monster:
                case RoPointerTargetKind.Npc:
                    return true;
                default:
                    return false;
            }
        }

        private UnityEngine.Camera ResolveInputCamera() {
            if (inputCamera != null) {
                return inputCamera;
            }

            float now = Time.unscaledTime;
            if (cachedInputCamera != null && now < nextInputCameraResolveTime) {
                return cachedInputCamera;
            }

            cachedInputCamera = UnityEngine.Camera.main;
            nextInputCameraResolveTime = now + InputCameraResolveIntervalSeconds;
            return cachedInputCamera;
        }

        private void CollectSpritePointerTargets(
            UnityEngine.Camera camera,
            Vector2 pointerPosition,
            RoPointerModifierFlags modifiers,
            bool allowLocalSelfTarget,
            List<RoPointerTarget> targets
        ) {
            spritePointerTargetHits.Clear();
            IReadOnlyList<RoSpritePointerTargetShape> shapes = RoSpritePointerTargetShape.ActiveShapes;
            RoRuntimePerfCounters.SetActiveSpritePointerShapeCount(shapes.Count);
            if (TryAppendCachedSpritePointerTargets(pointerPosition, modifiers, allowLocalSelfTarget, shapes.Count, targets)) {
                RoRuntimePerfCounters.IncrementSpritePointerCacheHit();
                return;
            }

            RoRuntimePerfCounters.IncrementSpritePointerCacheMiss();
            spritePointerTargetCandidates.Clear();
            using (RoProfilerMarkers.PointerSpriteBroadphase.Auto()) {
                // the camera's terms read once for the pass (not ForCamera: this runs in Update, before the camera's
                // LateUpdate move, and must not leave a stale frame cached for the draw order collect)
                var frame = new RoClientDrawFrame(camera);
                for (int i = 0; i < shapes.Count; i++) {
                    RoSpritePointerTargetShape shape = shapes[i];
                    if (shape == null || !shape.isActiveAndEnabled) {
                        continue;
                    }

                    if (shape.TryCalculateBroadphaseScore(frame, pointerPosition, out int priority, out float broadphaseDepth)) {
                        spritePointerTargetCandidates.Add(new SpritePointerTargetCandidate(shape, priority, broadphaseDepth));
                    }
                }
            }

            if (spritePointerTargetCandidates.Count > 1) {
                spritePointerTargetCandidates.Sort(CompareSpritePointerTargetCandidates);
            }

            int exactCount = Mathf.Min(spritePointerTargetCandidates.Count, MaxSpritePointerExactCandidates);
            using (RoProfilerMarkers.PointerSpriteExact.Auto()) {
                for (int i = 0; i < exactCount; i++) {
                    RoSpritePointerTargetShape shape = spritePointerTargetCandidates[i].Shape;
                    if (shape != null && shape.TryBuildTarget(camera, pointerPosition, modifiers, out RoPointerTarget target, out float depth)) {
                        if (!ShouldKeepPointerTargetForClick(target, allowLocalSelfTarget)) {
                            continue;
                        }

                        spritePointerTargetHits.Add(new SpritePointerTargetHit(target, depth));
                        break;
                    }
                }
            }

            if (spritePointerTargetHits.Count == 0) {
                UpdateCachedSpritePointerTargets(pointerPosition, modifiers, allowLocalSelfTarget, shapes.Count);
                return;
            }

            spritePointerTargetHits.Sort(CompareSpritePointerTargetHits);
            UpdateCachedSpritePointerTargets(pointerPosition, modifiers, allowLocalSelfTarget, shapes.Count);
            for (int i = 0; i < spritePointerTargetHits.Count; i++) {
                targets.Add(spritePointerTargetHits[i].Target);
            }
        }

        private bool TryAppendCachedSpritePointerTargets(
            Vector2 pointerPosition,
            RoPointerModifierFlags modifiers,
            bool allowLocalSelfTarget,
            int shapeCount,
            List<RoPointerTarget> targets
        ) {
            if (!hasCachedSpritePointerTargets ||
                cachedSpritePointerShapeCount != shapeCount ||
                cachedSpritePointerModifiers != modifiers ||
                cachedSpritePointerAllowLocalSelfTarget != allowLocalSelfTarget ||
                Time.frameCount - cachedSpritePointerFrame > SpritePointerCacheMaxFrames ||
                Vector2.SqrMagnitude(pointerPosition - cachedSpritePointerPosition) > SpritePointerCachePixelEpsilonSqr) {
                return false;
            }

            for (int i = 0; i < cachedSpritePointerTargetHits.Count; i++) {
                targets.Add(cachedSpritePointerTargetHits[i].Target);
            }

            return true;
        }

        private void UpdateCachedSpritePointerTargets(
            Vector2 pointerPosition,
            RoPointerModifierFlags modifiers,
            bool allowLocalSelfTarget,
            int shapeCount
        ) {
            cachedSpritePointerTargetHits.Clear();
            for (int i = 0; i < spritePointerTargetHits.Count; i++) {
                cachedSpritePointerTargetHits.Add(spritePointerTargetHits[i]);
            }

            cachedSpritePointerPosition = pointerPosition;
            cachedSpritePointerModifiers = modifiers;
            cachedSpritePointerAllowLocalSelfTarget = allowLocalSelfTarget;
            cachedSpritePointerShapeCount = shapeCount;
            cachedSpritePointerFrame = Time.frameCount;
            hasCachedSpritePointerTargets = true;
        }

        private static int CompareSpritePointerTargetHits(SpritePointerTargetHit left, SpritePointerTargetHit right) {
            int priority = right.Target.Priority.CompareTo(left.Target.Priority);
            return priority != 0 ? priority : left.Depth.CompareTo(right.Depth);
        }

        private static int CompareSpritePointerTargetCandidates(SpritePointerTargetCandidate left, SpritePointerTargetCandidate right) {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : left.Depth.CompareTo(right.Depth);
        }

        private bool TryResolvePortalPointerTarget(
            Vector2 pointerPosition,
            UnityEngine.Camera camera,
            RoPointerModifierFlags modifiers,
            out RoPointerTarget target
        ) {
            target = RoPointerTarget.None;
            if (camera == null || gridSelector == null) {
                return false;
            }

            float bestScore = float.PositiveInfinity;
            ConsiderOnlinePortalPointerTargets(pointerPosition, camera, modifiers, ref target, ref bestScore);
            ConsiderMapPortalPointerTargets(pointerPosition, camera, modifiers, ref target, ref bestScore);
            return target.Kind == RoPointerTargetKind.Portal;
        }

        private void ConsiderOnlinePortalPointerTargets(
            Vector2 pointerPosition,
            UnityEngine.Camera camera,
            RoPointerModifierFlags modifiers,
            ref RoPointerTarget bestTarget,
            ref float bestScore
        ) {
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller == null) {
                return;
            }

            controller.SnapshotActors(portalActorSnapshots);
            for (int i = 0; i < portalActorSnapshots.Count; i++) {
                RoOnlineActorSnapshot actor = portalActorSnapshots[i];
                if (!IsPortalActor(actor)) {
                    continue;
                }

                Vector2Int cell = RoOnlineActorPresenter.GetDisplayCell(actor);
                Vector3 worldPosition = ResolveCellWorldPosition(cell, groundOffset);
                ConsiderPortalPointerTarget(
                    pointerPosition,
                    camera,
                    cell,
                    worldPosition,
                    null,
                    modifiers,
                    string.IsNullOrWhiteSpace(actor.Name) ? $"portal {actor.ServerId}" : actor.Name,
                    ref bestTarget,
                    ref bestScore);
            }
        }

        private void ConsiderMapPortalPointerTargets(
            Vector2 pointerPosition,
            UnityEngine.Camera camera,
            RoPointerModifierFlags modifiers,
            ref RoPointerTarget bestTarget,
            ref float bestScore
        ) {
            RoRuntimeMapController mapController = ResolveRuntimeMapController();
            if (mapController == null ||
                mapController.CurrentMapPortalIndicators == null ||
                mapController.CurrentMapPortalIndicators.Count == 0) {
                return;
            }

            Transform portalRoot = mapController.Effects != null
                ? mapController.Effects.transform
                : mapController.CurrentMap != null ? mapController.CurrentMap.transform : null;
            for (int i = 0; i < mapController.CurrentMapPortalIndicators.Count; i++) {
                RoMapPortalIndicator indicator = mapController.CurrentMapPortalIndicators[i];
                Vector3 worldPosition = portalRoot != null
                    ? portalRoot.TransformPoint(indicator.LocalPosition)
                    : indicator.LocalPosition;
                if (!gridSelector.TryGetCellAtWorld(worldPosition, out Vector2Int cell)) {
                    continue;
                }

                ConsiderPortalPointerTarget(
                    pointerPosition,
                    camera,
                    cell,
                    worldPosition,
                    null,
                    modifiers,
                    string.IsNullOrWhiteSpace(indicator.Name) ? "map portal" : indicator.Name,
                    ref bestTarget,
                    ref bestScore);
            }
        }

        private void ConsiderPortalPointerTarget(
            Vector2 pointerPosition,
            UnityEngine.Camera camera,
            Vector2Int cell,
            Vector3 worldPosition,
            GameObject gameObject,
            RoPointerModifierFlags modifiers,
            string label,
            ref RoPointerTarget bestTarget,
            ref float bestScore
        ) {
            if (!TryCalculatePortalScreenHitScore(camera, pointerPosition, worldPosition, out float score) ||
                score >= bestScore) {
                return;
            }

            bestScore = score;
            bestTarget = RoPointerTarget.Portal(cell, worldPosition, gameObject, modifiers, label);
        }

        private bool TryGetPointerTargetPoint(RoPointerTarget target, Ray ray, out Vector3 point) {
            if (target.Kind != RoPointerTargetKind.None && target.IsValid) {
                point = target.WorldPosition;
                return true;
            }

            return TryGetPointerGroundPoint(ray, out point);
        }

        private bool ShouldKeepPointerTargetForClick(RoPointerTarget target, bool allowLocalSelfTarget) {
            if (!allowLocalSelfTarget && IsLocalSelfPointerTarget(target)) {
                return false;
            }

            return !IsInactiveOnlinePointerTarget(target);
        }

        private bool IsInactiveOnlinePointerTarget(RoPointerTarget target) {
            if (!target.IsValid || target.GameObject == null) {
                return false;
            }

            RoOnlinePointerTargetMarker onlineTarget =
                target.GameObject.GetComponentInParent<RoOnlinePointerTargetMarker>();
            if (onlineTarget == null || onlineTarget.ServerId == 0) {
                return false;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller == null) {
                return false;
            }

            if (!controller.State.TryGetActor(onlineTarget.ServerId, out RoOnlineActorSnapshot actor)) {
                return true;
            }

            return actor.ObjectType != RoMapActorObjectType.Player &&
                RoOnlineActorInteractionRules.IsDead(actor);
        }

        private bool IsLocalSelfPointerTarget(RoPointerTarget target) {
            if (!target.IsValid ||
                target.Kind != RoPointerTargetKind.Player ||
                target.GameObject == null) {
                return false;
            }

            RoOnlinePointerTargetMarker onlineTarget =
                target.GameObject.GetComponentInParent<RoOnlinePointerTargetMarker>();
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (onlineTarget != null &&
                controller != null &&
                controller.OwnActorId != 0 &&
                onlineTarget.ServerId == controller.OwnActorId) {
                return true;
            }

            Transform targetTransform = target.GameObject.transform;
            return targetTransform == transform || targetTransform.IsChildOf(transform);
        }

        private static bool AllowsLocalSelfPointerTarget(RoSkillTargetSelection selection) {
            if (selection == null ||
                !selection.HasActiveSelection ||
                !selection.TargetsEntity) {
                return false;
            }

            return selection.TargetType == RoSkillTargetType.Support ||
                selection.TargetType == RoSkillTargetType.Friend;
        }

        private bool TrySubmitOnlinePointerTargetClick(
            RoPointerTarget target,
            RoPointerModifierFlags modifiers
        ) {
            if (!target.IsValid ||
                target.Kind == RoPointerTargetKind.None ||
                target.Kind == RoPointerTargetKind.GroundCell) {
                return false;
            }

            if (target.Kind == RoPointerTargetKind.Portal && target.GameObject == null) {
                return SubmitPortalInteraction(target.Cell);
            }

            if (target.GameObject == null) {
                return false;
            }

            if (IsLocalSelfPointerTarget(target)) {
                return false;
            }

            RoOnlinePointerTargetMarker onlineTarget = target.GameObject.GetComponentInParent<RoOnlinePointerTargetMarker>();
            if (onlineTarget == null || !onlineTarget.SubmitPointerClick(target, modifiers)) {
                return false;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            RoOnlineTargetSelection selection = controller != null ? controller.SelectedTarget : default;
            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) {
                Ragnarok.Diagnostics.RoSkillSweepProbe.Record(
                    $"click target aid={selection.ServerId} has={selection.HasTarget} interaction={selection.Interaction} cell={selection.Cell} blocked={IsOwnActorMovementOrInteractionBlocked()}");
            }

            if (ShouldBlockOnlineTargetInteraction(selection.Interaction) && IsOwnActorMovementOrInteractionBlocked()) {
                ClearPendingTargetAction();
                ClearLocalMovement();
                return true;
            }

            if (selection.Interaction == RoOnlineTargetInteraction.Attack) {
                SubmitAttackInteraction(selection);
            } else if (selection.Interaction == RoOnlineTargetInteraction.Pickup) {
                SubmitPickupInteraction(selection);
            } else if (selection.Interaction == RoOnlineTargetInteraction.EnterPortal) {
                SubmitPortalInteraction(selection.Cell);
            }

            return true;
        }

        private bool CanSubmitOnlinePointerTargetClick(RoPointerTarget target) {
            if (!target.IsValid ||
                target.Kind == RoPointerTargetKind.None ||
                target.Kind == RoPointerTargetKind.GroundCell) {
                return false;
            }

            if (target.Kind == RoPointerTargetKind.Portal && target.GameObject == null) {
                return true;
            }

            if (IsLocalSelfPointerTarget(target)) {
                return false;
            }

            return target.GameObject != null &&
                target.GameObject.GetComponentInParent<RoOnlinePointerTargetMarker>() != null;
        }

        private readonly struct SpritePointerTargetHit {
            public SpritePointerTargetHit(RoPointerTarget target, float depth) {
                Target = target;
                Depth = depth;
            }

            public RoPointerTarget Target { get; }
            public float Depth { get; }
        }

        private readonly struct SpritePointerTargetCandidate {
            public SpritePointerTargetCandidate(RoSpritePointerTargetShape shape, int priority, float depth) {
                Shape = shape;
                Priority = priority;
                Depth = depth;
            }

            public RoSpritePointerTargetShape Shape { get; }
            public int Priority { get; }
            public float Depth { get; }
        }

        private void EnableConfiguredActions() {
            EnableAction(pointerPositionAction, ref ownsPointerPositionActionEnable);
            EnableAction(primaryClickAction, ref ownsPrimaryClickActionEnable);
            EnableAction(secondaryClickAction, ref ownsSecondaryClickActionEnable);
            EnableAction(headLookModifierAction, ref ownsHeadLookModifierActionEnable);
        }

        private void DisableConfiguredActions() {
            DisableAction(pointerPositionAction, ref ownsPointerPositionActionEnable);
            DisableAction(primaryClickAction, ref ownsPrimaryClickActionEnable);
            DisableAction(secondaryClickAction, ref ownsSecondaryClickActionEnable);
            DisableAction(headLookModifierAction, ref ownsHeadLookModifierActionEnable);
        }

        private static void EnableAction(InputActionReference actionReference, ref bool ownsEnable) {
            InputAction action = actionReference != null ? actionReference.action : null;
            if (action != null && !action.enabled) {
                action.Enable();
                ownsEnable = true;
            }
        }

        private static void DisableAction(InputActionReference actionReference, ref bool ownsEnable) {
            InputAction action = actionReference != null ? actionReference.action : null;
            if (ownsEnable && action != null && action.enabled) {
                action.Disable();
            }

            ownsEnable = false;
        }

        private bool TryReadPointerPosition(out Vector2 pointerPosition) {
            return RoPointerScreenPosition.TryRead(pointerPositionAction, out pointerPosition);
        }

        private bool IsPointerPositionActionActive() {
            InputAction action = pointerPositionAction != null ? pointerPositionAction.action : null;
            return action != null && action.enabled;
        }

        private bool IsPrimaryClickPressed() {
            InputAction action = primaryClickAction != null ? primaryClickAction.action : null;
            if (action != null) {
                return IsPressedValue(action.ReadValue<float>());
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        private bool IsSecondaryClickPressed() {
            InputAction action = secondaryClickAction != null ? secondaryClickAction.action : null;
            if (action != null) {
                return IsPressedValue(action.ReadValue<float>());
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }

        private bool TryGetPointerGroundPoint(Ray ray, out Vector3 point) {
            long pointerGroundStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                if (gridSelector != null && gridSelector.TryGetPoint(ray, out point)) {
                    return true;
                }

                if (gridSelector != null && gridSelector.HasCell) {
                    point = gridSelector.GetCellWorldCenter(gridSelector.CurrentCell, groundOffset);
                    return true;
                }

                if (TryRaycastGround(ray, out RaycastHit hit)) {
                    point = hit.point;
                    return true;
                }

                point = default;
                return false;
            } finally {
                RoRuntimePerfCounters.AddPointerGroundPointTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(pointerGroundStartTicks));
            }
        }

        private RoActorDirection GetHeadLookTargetDirection(Vector3 lookPoint) {
            Vector3 lookDelta = lookPoint - transform.position;
            lookDelta.y = 0f;
            return RoSpriteActionMap.GetDirectionFromWorldDelta(lookDelta, facingDirection);
        }

        private void ApplyHeadLook(Vector3 lookPoint) {
            if (layeredActorVisual == null) {
                layeredActorVisual = GetComponentInChildren<RoActorLayeredVisual>();
            }

            RoActorLayeredVisual visual = layeredActorVisual;
            RoSpriteMotion lookMotion = ResolveHeadLookMotion();

            hasTarget = false;
            isGridPathMovement = false;
            stepActive = false;
            ClearServerGatedMove();
            waypoints.Clear();
            waypointIndex = 0;

            Vector3 lookDelta = lookPoint - transform.position;
            lookDelta.y = 0f;
            if (lookDelta.sqrMagnitude <= 0.0001f) {
                visual?.SetHeadDirectionFrameIndex(0);
                UpdateActorMotion(lookMotion, facingDirection);
                return;
            }

            RoActorDirection targetDirection = RoSpriteActionMap.GetDirectionFromWorldDelta(lookDelta, facingDirection);
            int directionStep = RoActorLayeredVisual.CalculateShortestDirectionStep(facingDirection, targetDirection);
            int targetHeadFrame = RoActorLayeredVisual.GetHeadDirectionFrameIndexForStep(directionStep);

            if (visual == null || (visual.HeadDirectionFrameIndex != 0 && visual.HeadDirectionFrameIndex == targetHeadFrame)) {
                facingDirection = RoActorLayeredVisual.StepFacingDirection(facingDirection, directionStep);
                directionStep = RoActorLayeredVisual.CalculateShortestDirectionStep(facingDirection, targetDirection);
                targetHeadFrame = RoActorLayeredVisual.GetHeadDirectionFrameIndexForStep(directionStep);

                Vector3 bodyForward = RoActorLayeredVisual.DirectionToWorldVector(facingDirection);
                if (bodyForward.sqrMagnitude > 0.0001f) {
                    transform.rotation = Quaternion.LookRotation(bodyForward, Vector3.up);
                }
            }

            UpdateActorMotion(lookMotion, facingDirection);
            visual?.SetHeadDirectionFrameIndex(targetHeadFrame);
        }

        private RoSpriteMotion ResolveHeadLookMotion() {
            return TryGetOwnActor(out RoOnlineActorSnapshot actor) && RoOnlineActorInteractionRules.IsSitting(actor)
                ? RoSpriteMotion.Sit
                : RoSpriteMotion.Idle;
        }

        private bool IsHeadLookModifierPressed() {
            InputAction action = headLookModifierAction != null ? headLookModifierAction.action : null;
            if (action != null) {
                return IsPressedValue(action.ReadValue<float>());
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        public static bool IsPressedValue(float value, float pressPoint = 0.5f) {
            return value >= Mathf.Max(0.0001f, pressPoint);
        }

        public static float GetPresentationDeltaSeconds() {
            return RoPresentationClock.DeltaSeconds;
        }

        public static float GetPresentationTimeSeconds() {
            return RoPresentationClock.NowSeconds;
        }

        public static float GetServerClockLocalTimeSeconds() {
            return RoPresentationClock.NowSeconds;
        }

        public static bool ShouldUseDynamicBlockersForGridPath(bool requiresOnlineSend, bool ignoreDynamicBlockers) {
            return !requiresOnlineSend && !ignoreDynamicBlockers;
        }

        public static int ResolveServerTargetSkillRangeCells(int skillRange) {
            return skillRange < 0 ? -skillRange : skillRange;
        }

        public static bool IsWithinServerPlayerSkillRange(Vector2Int fromCell, Vector2Int targetCell, int rangeCells) {
            int dx = Mathf.Abs(fromCell.x - targetCell.x);
            int dy = Mathf.Abs(fromCell.y - targetCell.y);
            float distance = Mathf.Sqrt(dx * dx + dy * dy) - 0.0625f;
            distance = Mathf.Max(0f, distance);
            return Mathf.FloorToInt(distance) <= Mathf.Max(0, rangeCells);
        }

        public static bool TryTrimPathToServerTargetSkillRange(
            List<Vector2Int> path,
            Vector2Int targetCell,
            int rangeCells
        ) {
            if (path == null || path.Count < 2) {
                return false;
            }

            for (int i = 1; i < path.Count; i++) {
                if (!IsWithinServerPlayerSkillRange(path[i], targetCell, rangeCells)) {
                    continue;
                }

                if (i + 1 < path.Count) {
                    path.RemoveRange(i + 1, path.Count - i - 1);
                }

                return true;
            }

            return false;
        }

        private bool TrySetGridPath(Vector2Int targetCell, bool ignoreDynamicBlockers = false) {
            if (gridSelector == null || gridSelector.Altitude == null) {
                TraceRuntimePerf($"grid path rejected no-grid target={FormatCell(targetCell)}");
                return false;
            }

            if (IsOwnActorMovementOrInteractionBlocked()) {
                TraceRuntimePerf($"grid path rejected actor-blocked target={FormatCell(targetCell)}");
                ClearLocalMovement();
                return false;
            }

            bool requiresOnlineSend = ResolveLoginShell(forceLookup: true) != null;
            Vector2Int activeStepEndCell = default;
            bool preserveActiveStep = !requiresOnlineSend &&
                isGridPathMovement &&
                stepActive &&
                TryGetActiveGridStepCells(gridPath, waypointIndex, out _, out activeStepEndCell);

            Vector2Int startCell;
            if (preserveActiveStep) {
                startCell = activeStepEndCell;
            } else if (requiresOnlineSend && TryResolveOwnServerCommandCell(out Vector2Int serverCommandCell)) {
                startCell = serverCommandCell;
            } else if (!gridSelector.TryGetCellAtWorld(transform.position, out startCell)) {
                TraceRuntimePerf($"grid path rejected no-start target={FormatCell(targetCell)} online={requiresOnlineSend}");
                return false;
            }

            ISet<Vector2Int> blockedCells = ShouldUseDynamicBlockersForGridPath(requiresOnlineSend, ignoreDynamicBlockers)
                ? BuildOnlineBlockedCells(startCell)
                : null;
            if (!RoGridPathfinder.TryFindPath(gridSelector.Altitude, startCell, targetCell, blockedCells, pendingGridPath)) {
                TraceRuntimePerf($"grid path rejected no-path start={FormatCell(startCell)} target={FormatCell(targetCell)} online={requiresOnlineSend}");
                return false;
            }

            Vector2Int commandTargetCell = targetCell;
            bool requiresContinuation = false;
            if (requiresOnlineSend &&
                !TryResolveCommandTargetForMaxWalkPath(
                    pendingGridPath,
                    MovementTuning.MaxWalkPathSteps,
                    out commandTargetCell,
                    out requiresContinuation)) {
                TraceRuntimePerf($"grid path rejected no-command-target start={FormatCell(startCell)} target={FormatCell(targetCell)} steps={pendingGridPath.Count}");
                return false;
            }

            if (requiresOnlineSend) {
                if (!TrySendOnlineMoveRequest(startCell, commandTargetCell)) {
                    TraceRuntimePerf($"grid path online send failed start={FormatCell(startCell)} command={FormatCell(commandTargetCell)} final={FormatCell(targetCell)}");
                    return false;
                }

                BeginPendingOnlineMoveAck(commandTargetCell);
                SetOnlineMoveCommandTargets(commandTargetCell, targetCell, requiresContinuation);
                UpdateTargetMarker(gridSelector.GetCellWorldCenter(commandTargetCell, groundOffset));
                TraceRuntimePerf($"grid path online sent start={FormatCell(startCell)} command={FormatCell(commandTargetCell)} final={FormatCell(targetCell)} requested={FormatCell(targetCell)} steps={pendingGridPath.Count} continuation={requiresContinuation}");
                return true;
            }

            IReadOnlyList<Vector2Int> sourcePath = pendingGridPath;
            bool resetActiveStep = true;
            bool resetWalkDistance = true;
            if (preserveActiveStep) {
                if (!TryBuildGridPathPreservingActiveStep(
                    gridPath,
                    waypointIndex,
                    pendingGridPath,
                    rebuiltGridPath
                )) {
                    TraceRuntimePerf($"grid path rejected preserve-active-step target={FormatCell(targetCell)}");
                    return false;
                }

                sourcePath = rebuiltGridPath;
                resetActiveStep = false;
                resetWalkDistance = false;
            }

            ApplyGridPath(sourcePath, resetActiveStep, resetWalkDistance);
            TraceRuntimePerf($"grid path local applied target={FormatCell(targetCell)} steps={sourcePath.Count} preserveActiveStep={preserveActiveStep}");
            if (preserveActiveStep && waypoints.Count > 0) {
                waypoints[0] = stepEndPosition;
                if (waypoints.Count == 1) {
                    targetPosition = stepEndPosition;
                    UpdateTargetMarker(targetPosition);
                }
            }

            return true;
        }

        /// <summary>
        /// Diagnostics (sweep console !click): a click on an online actor without a pointer - it is selected, then
        /// attacked or picked up the way a pointer click on it is (local approach, then the request in range).
        /// </summary>
        internal bool SubmitOnlineTargetForDiagnostics(uint serverId) {
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller == null || !controller.SelectOnlineTarget(serverId, default, RoPointerModifierFlags.None)) {
                return false;
            }

            RoOnlineTargetSelection selection = controller.SelectedTarget;
            if (ShouldBlockOnlineTargetInteraction(selection.Interaction) && IsOwnActorMovementOrInteractionBlocked()) {
                return false;
            }

            if (selection.Interaction == RoOnlineTargetInteraction.Attack) {
                SubmitAttackInteraction(selection);
            } else if (selection.Interaction == RoOnlineTargetInteraction.Pickup) {
                SubmitPickupInteraction(selection);
            } else {
                return false;
            }

            return true;
        }

        private void SubmitAttackInteraction(RoOnlineTargetSelection selection) {
            if (!selection.HasTarget || selection.ServerId > int.MaxValue) {
                ClearPendingTargetAction();
                return;
            }

            if (!TryResolveOwnGameplayCell(out Vector2Int startCell)) {
                ClearPendingTargetAction();
                return;
            }

            Vector2Int targetCell = selection.Cell;
            int attackRangeCells = ResolveCurrentAttackPathRangeCells();
            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) {
                Ragnarok.Diagnostics.RoSkillSweepProbe.Record(
                    $"click attack aid={selection.ServerId} from={startCell} to={targetCell} range={attackRangeCells} inRange={IsWithinAttackRange(startCell, targetCell, attackRangeCells)}");
            }

            if (IsWithinAttackRange(startCell, targetCell, attackRangeCells)) {
                ClearPendingTargetAction();
                bool sent = ResolveLoginShell(forceLookup: true)?.TrySendAttackRequest(unchecked((int) selection.ServerId)) == true;
                if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"click attack request sent={sent}");
                if (sent) {
                    RoActorDirection direction = CalculateAttackFacingDirection(startCell, targetCell, facingDirection);
                    ApplyLocalAttackRequestFacing(direction);
                }

                return;
            }

            if (!TryFindAttackApproachCell(startCell, targetCell, attackRangeCells, out Vector2Int approachCell)) {
                ClearPendingTargetAction();
                return;
            }

            pendingAttackTargetId = selection.ServerId;
            pendingAttackTargetCell = targetCell;
            hasPendingAttackTargetCell = true;
            pendingPickupItemAid = 0;
            pendingPickupItemCell = default;
            hasPendingPickupItemCell = false;
            pendingPickupWorldPosition = default;
            hasPendingPickupWorldPosition = false;
            if (!TrySetGridPath(approachCell)) {
                ClearPendingTargetAction();
            }
        }

        private void SubmitPickupInteraction(RoOnlineTargetSelection selection) {
            if (!selection.HasTarget || selection.ServerId > int.MaxValue) {
                TraceRuntimePerf($"pickup rejected invalid-target serverId={selection.ServerId}");
                ClearPendingTargetAction();
                return;
            }

            if (!TryResolveOwnGameplayCell(out Vector2Int startCell)) {
                TraceRuntimePerf($"pickup rejected no-own-cell item={selection.ServerId} targetCell={FormatCell(selection.Cell)}");
                ClearPendingTargetAction();
                return;
            }

            Vector2Int targetCell = selection.Cell;
            TraceRuntimePerf($"pickup submit item={selection.ServerId} start={FormatCell(startCell)} target={FormatCell(targetCell)}");
            if (startCell == targetCell) {
                if (TrySendPickupGroundItemRequestWithLocalMotion(
                    selection.ServerId,
                    true,
                    targetCell,
                    true,
                    selection.WorldPosition)) {
                    ClearPendingTargetAction();
                    return;
                }

                SetPendingPickupInteraction(selection, targetCell);
                return;
            }

            SetPendingPickupInteraction(selection, targetCell);
            if (!TrySetGridPath(targetCell)) {
                TraceRuntimePerf($"pickup path failed item={selection.ServerId} cell={FormatCell(targetCell)}");
                ClearPendingTargetAction();
            }
        }

        private void SetPendingPickupInteraction(RoOnlineTargetSelection selection, Vector2Int targetCell) {
            pendingAttackTargetId = 0;
            pendingAttackTargetCell = default;
            hasPendingAttackTargetCell = false;
            pendingPickupItemAid = selection.ServerId;
            pendingPickupItemCell = targetCell;
            hasPendingPickupItemCell = true;
            pendingPickupWorldPosition = selection.WorldPosition;
            hasPendingPickupWorldPosition = true;
            pendingPickupRetryStartTime = 0f;
            hasPendingPickupRetryStartTime = false;
            TraceRuntimePerf($"pickup pending item={pendingPickupItemAid} cell={FormatCell(pendingPickupItemCell)}");
        }

        private bool SubmitPortalInteraction(Vector2Int portalCell) {
            ClearPendingTargetAction();
            if (gridSelector == null || gridSelector.Altitude == null) {
                return false;
            }

            if (IsOwnActorMovementOrInteractionBlocked()) {
                ClearLocalMovement();
                return true;
            }

            if (!TryResolveOwnGameplayCell(out Vector2Int startCell)) {
                return false;
            }

            ISet<Vector2Int> blockedCells = BuildOnlineBlockedCells(startCell);
            if (!TryResolvePortalMoveCell(
                gridSelector.Altitude,
                startCell,
                portalCell,
                blockedCells,
                portalApproachPath,
                out Vector2Int moveCell)) {
                return false;
            }

            return TrySetGridPath(moveCell);
        }

        private bool TryResolveCurrentCell(out Vector2Int cell) {
            if (gridSelector != null && gridSelector.TryGetCellAtWorld(transform.position, out cell)) {
                return true;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller != null &&
                controller.OwnActorId != 0 &&
                controller.State.TryGetActor(controller.OwnActorId, out RoOnlineActorSnapshot self)) {
                cell = RoOnlineActorPresenter.GetDisplayCell(self);
                return true;
            }

            cell = default;
            return false;
        }

        private bool TryResolveOwnGameplayCell(out Vector2Int cell) {
            if (ResolveLoginShell(forceLookup: true) != null) {
                return TryResolveOwnServerCommandCell(out cell);
            }

            return TryResolveCurrentCell(out cell);
        }

        private bool TryResolveOwnServerCommandCell(out Vector2Int cell) {
            if (TryGetOwnActor(out RoOnlineActorSnapshot actor)) {
                cell = new Vector2Int(actor.CellX, actor.CellY);
                return true;
            }

            return TryResolveCurrentCell(out cell);
        }

        private void BeginPendingOnlineMoveAck(Vector2Int commandTargetCell) {
            hasPendingOnlineMoveAck = true;
            pendingOnlineMoveAckTargetCell = commandTargetCell;
            pendingOnlineMoveAckSentTime = GetPresentationTimeSeconds();
            loggedPendingOnlineMoveAckTimeout = false;
            TraceRuntimePerf($"move ack begin target={FormatCell(commandTargetCell)}");
        }

        private void ClearPendingOnlineMoveAck() {
            if (hasPendingOnlineMoveAck) {
                TraceRuntimePerf($"move ack clear target={FormatCell(pendingOnlineMoveAckTargetCell)} ageMs={(GetPresentationTimeSeconds() - pendingOnlineMoveAckSentTime) * 1000f:F0}");
            }

            hasPendingOnlineMoveAck = false;
            pendingOnlineMoveAckTargetCell = default;
            pendingOnlineMoveAckSentTime = 0f;
            loggedPendingOnlineMoveAckTimeout = false;
        }

        private void SetOnlineMoveCommandTargets(Vector2Int commandTargetCell, Vector2Int finalTargetCell, bool requiresContinuation) {
            hasQueuedFinalMoveTargetCell = requiresContinuation;
            queuedFinalMoveTargetCell = requiresContinuation ? finalTargetCell : default;
        }

        private void ClearOnlineMoveCommandTargets() {
            hasQueuedFinalMoveTargetCell = false;
            queuedFinalMoveTargetCell = default;
        }

        public static bool ShouldQueueServerGatedContinuation(
            IReadOnlyList<Vector2Int> activePath,
            IReadOnlyList<Vector2Int> nextPath,
            float remainingSeconds,
            float continuationLeadSeconds
        ) {
            if (activePath == null ||
                nextPath == null ||
                activePath.Count < 2 ||
                nextPath.Count < 2) {
                return false;
            }

            return activePath[^1] == nextPath[0] &&
                Mathf.Max(0f, remainingSeconds) > Mathf.Max(0f, continuationLeadSeconds);
        }

        public static bool ShouldActivateServerGatedContinuation(float remainingSeconds, float deltaSeconds) {
            return Mathf.Max(0f, remainingSeconds) <= Mathf.Max(0.001f, deltaSeconds);
        }

        public static bool TryResolveCommandTargetForMaxWalkPath(
            IReadOnlyList<Vector2Int> path,
            int maxWalkPathSteps,
            out Vector2Int commandTarget,
            out bool requiresContinuation
        ) {
            commandTarget = default;
            requiresContinuation = false;
            if (path == null || path.Count == 0) {
                return false;
            }

            int maxSteps = Mathf.Max(1, maxWalkPathSteps);
            int commandIndex = Mathf.Min(path.Count - 1, maxSteps);
            commandTarget = path[commandIndex];
            requiresContinuation = commandIndex < path.Count - 1;
            return true;
        }

        private bool TryFindAttackApproachCell(Vector2Int startCell, Vector2Int targetCell, int rangeCells, out Vector2Int approachCell) {
            approachCell = default;
            if (gridSelector == null || gridSelector.Altitude == null) {
                return false;
            }

            ISet<Vector2Int> blockedCells = BuildOnlineBlockedCells(startCell, targetCell);
            if (!TryResolveLegacyAttackApproachPath(
                    gridSelector.Altitude,
                    startCell,
                    targetCell,
                    rangeCells,
                    attackApproachPath,
                    attackLineScratchPath,
                    blockedCells) ||
                attackApproachPath.Count == 0) {
                return false;
            }

            approachCell = attackApproachPath[^1];
            return true;
        }

        private bool TryFindTargetSkillApproachCell(
            Vector2Int startCell,
            Vector2Int targetCell,
            int rangeCells,
            out Vector2Int approachCell
        ) {
            approachCell = default;
            if (gridSelector == null || gridSelector.Altitude == null) {
                return false;
            }

            if (IsWithinServerPlayerSkillRange(startCell, targetCell, rangeCells)) {
                approachCell = startCell;
                return true;
            }

            ISet<Vector2Int> blockedCells = BuildOnlineBlockedCells(startCell, targetCell);
            if (!RoGridPathfinder.TryFindPath(gridSelector.Altitude, startCell, targetCell, blockedCells, attackApproachPath) ||
                attackApproachPath.Count == 0) {
                return false;
            }

            if (!TryTrimPathToServerTargetSkillRange(attackApproachPath, targetCell, rangeCells) &&
                !IsWithinServerPlayerSkillRange(attackApproachPath[^1], targetCell, rangeCells)) {
                return false;
            }

            approachCell = attackApproachPath[^1];
            return true;
        }

        public static bool TryResolvePortalMoveCell(
            ROIO.Models.FileTypes.GAT altitude,
            Vector2Int startCell,
            Vector2Int portalCell,
            ISet<Vector2Int> blockedCells,
            List<Vector2Int> pathScratch,
            out Vector2Int moveCell
        ) {
            moveCell = default;
            if (altitude == null || pathScratch == null) {
                return false;
            }

            if (RoGridPathfinder.TryFindPath(altitude, startCell, portalCell, blockedCells, pathScratch)) {
                for (int i = pathScratch.Count - 1; i >= 0; i--) {
                    Vector2Int candidate = pathScratch[i];
                    if (RoGridPathfinder.IsWalkable(altitude, candidate.x, candidate.y)) {
                        moveCell = candidate;
                        return true;
                    }
                }
            }

            int bestCost = int.MaxValue;
            int bestDistanceToPortal = int.MaxValue;
            for (int y = portalCell.y - 1; y <= portalCell.y + 1; y++) {
                for (int x = portalCell.x - 1; x <= portalCell.x + 1; x++) {
                    var candidate = new Vector2Int(x, y);
                    if (!RoGridPathfinder.TryFindPath(altitude, startCell, candidate, blockedCells, pathScratch)) {
                        continue;
                    }

                    int distanceToPortal = Mathf.Max(Mathf.Abs(candidate.x - portalCell.x), Mathf.Abs(candidate.y - portalCell.y));
                    int cost = CalculatePathCost(pathScratch);
                    if (distanceToPortal > bestDistanceToPortal ||
                        (distanceToPortal == bestDistanceToPortal && cost >= bestCost)) {
                        continue;
                    }

                    bestDistanceToPortal = distanceToPortal;
                    bestCost = cost;
                    moveCell = candidate;
                }
            }

            return bestCost < int.MaxValue;
        }

        public static bool IsWithinAttackRange(Vector2Int fromCell, Vector2Int targetCell, int rangeCells = DefaultAttackRangeCells) {
            int range = Mathf.Max(0, rangeCells);
            long deltaX = targetCell.x - fromCell.x;
            long deltaY = targetCell.y - fromCell.y;
            long distanceSquared = deltaX * deltaX + deltaY * deltaY;
            long rangeSquared = (long) range * range;
            return distanceSquared <= rangeSquared;
        }

        public static int CalculateAttackPathRangeCells(int currentAttackRangeCells) {
            return Mathf.Max(DefaultAttackRangeCells, currentAttackRangeCells + 1);
        }

        public static bool TryResolveLegacyAttackApproachPath(
            GAT altitude,
            Vector2Int startCell,
            Vector2Int targetCell,
            int rangeCells,
            List<Vector2Int> path,
            List<Vector2Int> lineScratch = null,
            ISet<Vector2Int> blockedCells = null
        ) {
            if (path == null) {
                return false;
            }

            if (lineScratch == path) {
                lineScratch = null;
            }

            path.Clear();
            if (altitude == null ||
                altitude.cells == null ||
                !ContainsGatCell(altitude, startCell) ||
                !ContainsGatCell(altitude, targetCell) ||
                !RoGridPathfinder.IsWalkable(altitude, startCell.x, startCell.y)) {
                return false;
            }

            int range = Mathf.Max(0, rangeCells);
            if (TryResolveDirectLegacyAttackPath(altitude, startCell, targetCell, range, path, blockedCells)) {
                return true;
            }

            if (!RoGridPathfinder.TryFindPath(altitude, startCell, targetCell, blockedCells, path) || path.Count == 0) {
                path.Clear();
                return false;
            }

            int earliestInRangeIndex = -1;
            lineScratch ??= new List<Vector2Int>();
            for (int i = path.Count - 1; i >= 0; i--) {
                if (!HasDirectLegacyAttackLine(
                        altitude,
                        path[i],
                        targetCell,
                        range,
                        requireStartingPointInRange: true,
                        lineScratch,
                        blockedCells)) {
                    break;
                }

                earliestInRangeIndex = i;
            }

            if (earliestInRangeIndex < 0) {
                return true;
            }

            if (earliestInRangeIndex + 1 < path.Count) {
                path.RemoveRange(earliestInRangeIndex + 1, path.Count - earliestInRangeIndex - 1);
            }

            return path.Count > 0;
        }

        private static bool TryResolveDirectLegacyAttackPath(
            GAT altitude,
            Vector2Int startCell,
            Vector2Int targetCell,
            int rangeCells,
            List<Vector2Int> path,
            ISet<Vector2Int> blockedCells = null
        ) {
            path.Clear();
            if (!HasDirectLegacyAttackLine(
                    altitude,
                    startCell,
                    targetCell,
                    rangeCells,
                    requireStartingPointInRange: false,
                    path,
                    blockedCells)) {
                path.Clear();
                return false;
            }

            return true;
        }

        private static bool HasDirectLegacyAttackLine(
            GAT altitude,
            Vector2Int startCell,
            Vector2Int targetCell,
            int rangeCells,
            bool requireStartingPointInRange,
            List<Vector2Int> path,
            ISet<Vector2Int> blockedCells = null
        ) {
            if (path == null) {
                return false;
            }

            path.Clear();
            path.Add(startCell);

            bool inRange = IsWithinAttackRange(startCell, targetCell, rangeCells);
            if (requireStartingPointInRange && !inRange) {
                return false;
            }

            if (startCell == targetCell) {
                return true;
            }

            int currentX = startCell.x;
            int currentY = startCell.y;
            int stepX = Sign(targetCell.x - startCell.x);
            int stepY = Sign(targetCell.y - startCell.y);
            int guard = Mathf.Max(1, (int) altitude.width + (int) altitude.height + 1);

            while ((stepX != 0 || stepY != 0) && guard-- > 0) {
                currentX += stepX;
                currentY += stepY;

                if (inRange && currentX == targetCell.x && currentY == targetCell.y) {
                    return true;
                }

                if (!RoGridPathfinder.IsWalkable(altitude, currentX, currentY) ||
                    IsDynamicBlocked(blockedCells, currentX, currentY)) {
                    return false;
                }

                if (!inRange) {
                    path.Add(new Vector2Int(currentX, currentY));
                }

                if (!inRange && IsWithinAttackRange(new Vector2Int(currentX, currentY), targetCell, rangeCells)) {
                    inRange = true;
                }

                if (currentX == targetCell.x) {
                    stepX = 0;
                }

                if (currentY == targetCell.y) {
                    stepY = 0;
                }
            }

            return false;
        }

        private static bool ContainsGatCell(GAT altitude, Vector2Int cell) {
            if (altitude == null) {
                return false;
            }

            return cell.x >= 0 &&
                cell.y >= 0 &&
                cell.x < (int) altitude.width &&
                cell.y < (int) altitude.height;
        }

        private static bool IsDynamicBlocked(ISet<Vector2Int> blockedCells, int x, int y) {
            return blockedCells != null && blockedCells.Contains(new Vector2Int(x, y));
        }

        private static int Sign(int value) {
            return value == 0 ? 0 : value < 0 ? -1 : 1;
        }

        private int ResolveCurrentAttackPathRangeCells() {
            return CalculateAttackPathRangeCells(currentAttackRangeCells);
        }

        public static bool IsWithinPickupRange(Vector2Int fromCell, Vector2Int targetCell, int rangeCells = DefaultPickupRangeCells) {
            int range = Mathf.Max(0, rangeCells);
            return Mathf.Max(Mathf.Abs(fromCell.x - targetCell.x), Mathf.Abs(fromCell.y - targetCell.y)) <= range;
        }

        public static RoActorDirection CalculateAttackFacingDirection(
            Vector2Int attackerCell,
            Vector2Int targetCell,
            RoActorDirection fallbackDirection
        ) {
            return (RoActorDirection) RoOnlineWorldState.DirectionFromCellDelta(
                targetCell.x - attackerCell.x,
                targetCell.y - attackerCell.y,
                (int) fallbackDirection);
        }

        public static RoActorDirection CalculatePickupFacingDirection(
            Vector2Int actorCell,
            Vector2Int itemCell,
            RoActorDirection fallbackDirection
        ) {
            return (RoActorDirection) RoOnlineWorldState.DirectionFromCellDelta(
                itemCell.x - actorCell.x,
                itemCell.y - actorCell.y,
                (int) fallbackDirection);
        }

        public static int CalculatePathCost(IReadOnlyList<Vector2Int> path) {
            if (path == null || path.Count < 2) {
                return 0;
            }

            int cost = 0;
            for (int i = 1; i < path.Count; i++) {
                int dx = Mathf.Abs(path[i].x - path[i - 1].x);
                int dy = Mathf.Abs(path[i].y - path[i - 1].y);
                cost += dx != 0 && dy != 0 ? ClassicDiagonalStepCost : ClassicStraightStepCost;
            }

            return cost;
        }

        private bool TrySendOnlineMoveRequest(Vector2Int startCell, Vector2Int targetCell) {
            if (startCell == targetCell) {
                TraceRuntimePerf($"move request skipped same-cell cell={FormatCell(startCell)}");
                return false;
            }

            RoMinimalLoginShell shell = ResolveLoginShell(forceLookup: true);
            if (shell == null) {
                TraceRuntimePerf($"move request failed no-shell start={FormatCell(startCell)} target={FormatCell(targetCell)}");
                return false;
            }

            int direction = CalculateMoveRequestDirection(startCell, targetCell, facingDirection);
            bool sent = shell.TrySendMoveRequest(targetCell.x, targetCell.y, direction);
            TraceRuntimePerf($"move request {(sent ? "sent" : "failed")} start={FormatCell(startCell)} target={FormatCell(targetCell)} direction={direction}");
            if (sent) {
                RecordLocalMovementTrace(
                    RoMovementTraceEventType.MoveRequestSent,
                    $"request start={FormatCell(startCell)} target={FormatCell(targetCell)} direction={direction}");
            }

            return sent;
        }

        public static int CalculateMoveRequestDirection(
            Vector2Int startCell,
            Vector2Int targetCell,
            RoActorDirection fallbackDirection
        ) {
            return RoOnlineWorldState.DirectionFromCellDelta(
                targetCell.x - startCell.x,
                targetCell.y - startCell.y,
                (int) fallbackDirection);
        }

        private ISet<Vector2Int> BuildOnlineBlockedCells(Vector2Int startCell) {
            return BuildOnlineBlockedCells(startCell, false, default);
        }

        private ISet<Vector2Int> BuildOnlineBlockedCells(Vector2Int startCell, Vector2Int allowedCell) {
            return BuildOnlineBlockedCells(startCell, true, allowedCell);
        }

        private ISet<Vector2Int> BuildOnlineBlockedCells(Vector2Int startCell, bool hasAllowedCell, Vector2Int allowedCell) {
            onlineBlockedCells.Clear();
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller == null) {
                return onlineBlockedCells;
            }

            controller.SnapshotActors(onlineActorSnapshots);
            for (int i = 0; i < onlineActorSnapshots.Count; i++) {
                RoOnlineActorSnapshot actor = onlineActorSnapshots[i];
                if (!ShouldBlockLocalMovement(actor)) {
                    continue;
                }

                AddBlockedActorCell(actor.CellX, actor.CellY, startCell, hasAllowedCell, allowedCell);
                if (actor.HasMovePath) {
                    AddBlockedActorCell(actor.MovePath.StartX, actor.MovePath.StartY, startCell, hasAllowedCell, allowedCell);
                    AddBlockedActorCell(actor.MovePath.EndX, actor.MovePath.EndY, startCell, hasAllowedCell, allowedCell);
                } else {
                    Vector2Int displayCell = RoOnlineActorPresenter.GetDisplayCell(actor);
                    AddBlockedActorCell(displayCell.x, displayCell.y, startCell, hasAllowedCell, allowedCell);
                }
            }

            return onlineBlockedCells;
        }

        private RoOnlineWorldController ResolveOnlineWorldController(bool forceLookup = false) {
            if (onlineWorldController != null) {
                return onlineWorldController;
            }

            float now = Time.unscaledTime;
            if (!forceLookup && now < nextOnlineWorldControllerLookupTime) {
                return null;
            }

            onlineWorldController = FindFirstObjectByType<RoOnlineWorldController>();
            nextOnlineWorldControllerLookupTime = onlineWorldController != null
                ? 0f
                : now + DependencyLookupRetrySeconds;
            return onlineWorldController;
        }

        private RoRuntimeMapController ResolveRuntimeMapController(bool forceLookup = false) {
            if (runtimeMapController != null) {
                return runtimeMapController;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController(forceLookup);
            runtimeMapController = controller != null ? controller.RuntimeMapController : null;
            if (runtimeMapController != null) {
                nextRuntimeMapControllerLookupTime = 0f;
                return runtimeMapController;
            }

            float now = Time.unscaledTime;
            if (!forceLookup && now < nextRuntimeMapControllerLookupTime) {
                return null;
            }

            runtimeMapController = FindFirstObjectByType<RoRuntimeMapController>();
            nextRuntimeMapControllerLookupTime = runtimeMapController != null
                ? 0f
                : now + DependencyLookupRetrySeconds;
            return runtimeMapController;
        }

        private RoMinimalLoginShell ResolveLoginShell(bool forceLookup = false) {
            if (loginShell != null) {
                return loginShell;
            }

            float now = Time.unscaledTime;
            if (!forceLookup && now < nextLoginShellLookupTime) {
                return null;
            }

            loginShell = FindFirstObjectByType<RoMinimalLoginShell>();
            nextLoginShellLookupTime = loginShell != null
                ? 0f
                : now + DependencyLookupRetrySeconds;
            return loginShell;
        }

        private RoSkillTargetSelection ResolveSkillTargetSelectionOverlay() {
            if (skillTargetSelection != null) {
                return skillTargetSelection;
            }

            skillTargetSelection = RoSkillTargetSelection.Current;
            return skillTargetSelection;
        }

        private Vector3 ResolveCellWorldPosition(Vector2Int cell, float yOffset) {
            if (gridSelector != null && gridSelector.IsCellAvailable(cell)) {
                return gridSelector.GetCellWorldCenter(cell, yOffset);
            }

            return new Vector3(cell.x + 0.5f, yOffset, cell.y + 0.5f);
        }

        private static bool IsPortalActor(RoOnlineActorSnapshot actor) {
            return actor.ServerId != 0 &&
                (actor.ObjectType == RoMapActorObjectType.Warp || actor.Job == 45);
        }

        public static bool TryCalculatePortalScreenHitScore(
            UnityEngine.Camera camera,
            Vector2 pointerPosition,
            Vector3 portalWorldPosition,
            out float score
        ) {
            score = float.PositiveInfinity;
            if (camera == null) {
                return false;
            }

            Vector3 baseScreen3 = camera.WorldToScreenPoint(portalWorldPosition);
            Vector3 topScreen3 = camera.WorldToScreenPoint(portalWorldPosition + Vector3.up * PortalScreenVerticalWorldUnits);
            if (baseScreen3.z <= 0f || topScreen3.z <= 0f) {
                return false;
            }

            var baseScreen = new Vector2(baseScreen3.x, baseScreen3.y);
            var topScreen = new Vector2(topScreen3.x, topScreen3.y);
            float projectedHeight = Vector2.Distance(baseScreen, topScreen);
            float bodyDistance = DistancePointToSegment(pointerPosition, baseScreen, topScreen, out float segmentT);
            float bodyHalfWidth = Mathf.Lerp(
                Mathf.Max(PortalScreenBaseHalfWidthPixels, projectedHeight * 0.32f),
                Mathf.Max(PortalScreenTopHalfWidthPixels, projectedHeight * 0.22f),
                segmentT);
            float groundRadius = Mathf.Max(PortalScreenGroundRadiusPixels, projectedHeight * 0.35f);
            float bodyScore = bodyHalfWidth > 0f ? bodyDistance / bodyHalfWidth : float.PositiveInfinity;
            float groundScore = groundRadius > 0f ? Vector2.Distance(pointerPosition, baseScreen) / groundRadius : float.PositiveInfinity;
            score = Mathf.Min(bodyScore, groundScore);
            return score <= 1f;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd, out float t) {
            Vector2 segment = segmentEnd - segmentStart;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f) {
                t = 0f;
                return Vector2.Distance(point, segmentStart);
            }

            t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSquared);
            Vector2 projected = segmentStart + segment * t;
            return Vector2.Distance(point, projected);
        }

        private bool IsOwnActorMovementOrInteractionBlocked() {
            return TryGetOwnActor(out RoOnlineActorSnapshot actor) &&
                RoOnlineActorInteractionRules.BlocksMovementOrAction(actor);
        }

        private bool TryGetOwnActor(out RoOnlineActorSnapshot actor) {
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller != null &&
                controller.OwnActorId != 0 &&
                controller.State.TryGetActor(controller.OwnActorId, out actor)) {
                return true;
            }

            actor = default;
            return false;
        }

        private bool IsOwnActorServerMoving() {
            return TryGetOwnActor(out RoOnlineActorSnapshot actor) &&
                actor.Motion == RoOnlineActorMotion.Moving &&
                actor.HasMovePath;
        }

        private bool TryHoldOwnActorBlockingMotion() {
            if (!TryGetOwnActor(out RoOnlineActorSnapshot actor)) {
                return false;
            }

            return TryHoldBlockingActorMotion(actor, preserveLocalFacing: ShouldPreserveOwnBlockingFacing(actor));
        }

        private static bool ShouldPreserveOwnBlockingFacing(RoOnlineActorSnapshot actor) {
            return actor.Motion != RoOnlineActorMotion.Casting &&
                actor.Motion != RoOnlineActorMotion.Skill &&
                actor.Motion != RoOnlineActorMotion.Attack &&
                actor.Motion != RoOnlineActorMotion.Attack2 &&
                actor.Motion != RoOnlineActorMotion.Attack3 &&
                actor.Motion != RoOnlineActorMotion.Throwing;
        }

        private bool TryHoldBlockingActorMotion(RoOnlineActorSnapshot actor, bool preserveLocalFacing) {
            if (!RoOnlineActorInteractionRules.BlocksMovementOrAction(actor)) {
                return false;
            }

            ClearPendingTargetAction();
            ClearLocalMovement();
            RoSpriteMotion motion = RoOnlineActorPresenter.ResolveMotion(actor);
            RoActorDirection direction = preserveLocalFacing
                ? facingDirection
                : RoOnlineActorPresenter.ResolveFacingDirection(actor);
            facingDirection = direction == RoActorDirection.None ? facingDirection : direction;
            hasLocalActionMotion = false;
            hasCombatStandby = false;
            localActionMotion = RoSpriteMotion.Idle;
            localActionMotionStartTime = 0f;
            localActionMotionUntilTime = 0f;
            UpdateActorMotion(motion, facingDirection, force: true);
            return true;
        }

        private bool TryHoldPlayDeadVisualMotion(RoOnlineActorSnapshot actor) {
            if (!actor.IsPlayingDead) {
                return false;
            }

            ClearPendingTargetAction();
            ClearLocalMovement();
            hasLocalActionMotion = false;
            hasCombatStandby = false;
            localActionMotion = RoSpriteMotion.Idle;
            localActionMotionStartTime = 0f;
            localActionMotionUntilTime = 0f;
            RoActorDirection direction = RoOnlineActorPresenter.ResolveFacingDirection(actor);
            facingDirection = direction == RoActorDirection.None ? facingDirection : direction;
            UpdateActorMotion(RoSpriteMotion.Dead, facingDirection, force: true);
            return true;
        }

        private static bool ShouldBlockOnlineTargetInteraction(RoOnlineTargetInteraction interaction) {
            return interaction == RoOnlineTargetInteraction.Attack ||
                interaction == RoOnlineTargetInteraction.Pickup ||
                interaction == RoOnlineTargetInteraction.EnterPortal;
        }

        private void ClearLocalMovement() {
            bool hadLocalMovement = gridPath.Count > 0 ||
                waypoints.Count > 0 ||
                hasTarget ||
                isGridPathMovement ||
                stepActive ||
                hasServerGatedMove ||
                hasPendingOnlineMoveAck ||
                hasQueuedFinalMoveTargetCell;
            if (hadLocalMovement) {
                TraceRuntimePerf("local movement cleared");
            }

            gridPath.Clear();
            waypoints.Clear();
            waypointIndex = 0;
            hasTarget = false;
            isGridPathMovement = false;
            stepActive = false;
            ClearVisualStopSettle();
            ClearServerGatedMove();
            ClearPendingOnlineMoveAck();
            ClearOnlineMoveCommandTargets();
            if (targetMarker != null) {
                Destroy(targetMarker);
                targetMarker = null;
            }
        }

        private void ClearServerGatedMove() {
            hasServerGatedMove = false;
            serverGatedMoveStartTime = 0;
            serverGatedMovePath = default;
            serverGatedMoveSpeed = 0;
            serverGatedMoveDurationSeconds = 0f;
            serverGatedPresentedElapsedSeconds = 0f;
            lastMovementTraceSegmentIndex = -1;
            ClearServerGatedCorrectionOffset();
            ClearPendingServerGatedContinuation();
            serverGatedMoveWaypoints.Clear();
            serverGatedMoveSegmentEndTimes.Clear();
        }

        private void ClearPendingServerGatedContinuation() {
            hasPendingServerGatedContinuation = false;
            pendingServerGatedContinuationStartTime = 0;
            pendingServerGatedContinuationMovePath = default;
            pendingServerGatedContinuationSpeed = 0;
            pendingServerGatedContinuationPath.Clear();
        }

        private void ClearServerGatedCorrectionOffset() {
            serverGatedCorrectionOffset = Vector3.zero;
            serverGatedCorrectionElapsedSeconds = 0f;
            serverGatedCorrectionDurationSeconds = 0f;
        }

        private void AddBlockedActorCell(
            int x,
            int y,
            Vector2Int startCell,
            bool hasAllowedCell = false,
            Vector2Int allowedCell = default) {
            var cell = new Vector2Int(x, y);
            if (cell != startCell && (!hasAllowedCell || cell != allowedCell)) {
                onlineBlockedCells.Add(cell);
            }
        }

        private static bool ShouldBlockLocalMovement(RoOnlineActorSnapshot actor) {
            if (actor.ServerId == 0 ||
                actor.IsSelf ||
                actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.Motion == RoOnlineActorMotion.Dead) {
                return false;
            }

            return actor.ObjectType != RoMapActorObjectType.Item &&
                actor.ObjectType != RoMapActorObjectType.Warp &&
                actor.ObjectType != RoMapActorObjectType.Effect;
        }

        public static bool ShouldSubmitHeldGridTarget(
            bool isLeftPressed,
            bool hasCell,
            Vector2Int currentCell,
            bool hasSubmittedCell,
            Vector2Int submittedCell
        ) {
            return isLeftPressed &&
                hasCell &&
                (!hasSubmittedCell || currentCell != submittedCell);
        }

        /// <summary>
        /// True when a held click-to-move re-issue should be skipped this frame. Only re-issues during an
        /// active hold (<paramref name="isHeldReissue"/>) are throttled; the first click of a hold always
        /// passes. Prevents a boundary-straddling cursor from churning the path and flicking the
        /// first-step facing (roBrowser/Rebuild throttle the held re-issue the same way).
        /// </summary>
        public static bool ShouldThrottleHeldGridMove(bool isHeldReissue, float nowSeconds, float nextAllowedSeconds) {
            return isHeldReissue && nowSeconds < nextAllowedSeconds;
        }

        public static bool ShouldHardSnapVisualStop(float distanceCells, float hardSnapCells) {
            return Mathf.Max(0f, distanceCells) > Mathf.Max(0f, hardSnapCells);
        }

        public static bool ShouldHardSnapVisualStop(float distanceCells, RoMovementTuning tuning) {
            return ShouldHardSnapVisualStop(distanceCells, RoMovementTuning.Resolve(tuning).HardSnapCells);
        }

        public static bool ShouldApplyHeldHeadLook(
            bool isLeftPressed,
            bool isHeadLookModifierPressed,
            bool hasPointerTarget,
            RoActorDirection targetDirection,
            bool hasSubmittedDirection,
            RoActorDirection submittedDirection
        ) {
            return isLeftPressed &&
                isHeadLookModifierPressed &&
                hasPointerTarget &&
                targetDirection != RoActorDirection.None &&
                (!hasSubmittedDirection || targetDirection != submittedDirection);
        }

        public static bool ShouldApplyHeldSittingLook(
            bool isLeftPressed,
            bool isSitting,
            bool hasPointerTarget,
            RoActorDirection targetDirection,
            bool hasSubmittedDirection,
            RoActorDirection submittedDirection
        ) {
            return isLeftPressed &&
                isSitting &&
                hasPointerTarget &&
                targetDirection != RoActorDirection.None &&
                (!hasSubmittedDirection || targetDirection != submittedDirection);
        }

        private void ApplyGridPath(
            IReadOnlyList<Vector2Int> sourcePath,
            bool resetActiveStep,
            bool resetWalkDistance,
            bool recordTrace = true
        ) {
            ClearVisualStopSettle();
            gridPath.Clear();
            for (int i = 0; i < sourcePath.Count; i++) {
                gridPath.Add(sourcePath[i]);
            }

            waypoints.Clear();
            waypointIndex = 0;
            // resetWalkDistance is kept for callers: the client never resets the walk distance (the step continues)

            for (int i = 1; i < gridPath.Count; i++) {
                waypoints.Add(gridSelector.GetCellWorldCenter(gridPath[i], groundOffset));
            }

            if (waypoints.Count == 0) {
                hasTarget = false;
                isGridPathMovement = false;
                if (resetActiveStep) {
                    stepActive = false;
                }

                return;
            }

            targetPosition = waypoints[^1];
            hasTarget = true;
            isGridPathMovement = true;
            if (resetActiveStep) {
                stepActive = false;
            }

            lastMovementTraceSegmentIndex = -1;
            if (recordTrace) {
                RecordLocalMovementTrace(
                    RoMovementTraceEventType.VisualMoveStarted,
                    $"local grid visual started start={FormatCell(sourcePath[0])} end={FormatCell(sourcePath[^1])} steps={sourcePath.Count} {FormatMovementTuningTrace()}",
                    presentedElapsedSeconds: 0f,
                    segmentIndex: 0);
            }

            UpdateTargetMarker(targetPosition);
        }

        private bool TryRaycastGround(Ray ray, out RaycastHit hit) {
            if (!RoMapCoordinateSystem.TryNormalizeRay(ray, out Ray normalizedRay)) {
                hit = default;
                return false;
            }

            if (gridSelector != null && gridSelector.TryGetPoint(normalizedRay, out Vector3 walkSurfacePoint)) {
                hit = new RaycastHit {
                    point = walkSurfacePoint,
                    normal = Vector3.up,
                    distance = Vector3.Distance(normalizedRay.origin, walkSurfacePoint)
                };
                return true;
            }

            if (groundCollider != null) {
                if (groundCollider.Raycast(normalizedRay, out hit, 2000f)) {
                    return true;
                }
            }

            if (TryRaycastPhysicsGround(normalizedRay, out hit)) {
                return true;
            }

            Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y - groundOffset, 0f));
            if (fallbackPlane.Raycast(normalizedRay, out float distance)) {
                hit = new RaycastHit {
                    point = normalizedRay.GetPoint(distance),
                    normal = Vector3.up,
                    distance = distance
                };
                return true;
            }

            hit = default;
            return false;
        }

        private bool TryRaycastPhysicsGround(Ray ray, out RaycastHit hit) {
            hit = default;
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                groundHitBuffer,
                2000f,
                groundMask,
                QueryTriggerInteraction.Ignore);
            float bestDistance = float.PositiveInfinity;
            bool hasHit = false;
            for (int i = 0; i < hitCount; i++) {
                RaycastHit candidate = groundHitBuffer[i];
                if (candidate.collider == null || IsPointerTargetCollider(candidate.collider)) {
                    continue;
                }

                if (candidate.distance >= bestDistance) {
                    continue;
                }

                hit = candidate;
                bestDistance = candidate.distance;
                hasHit = true;
            }

            return hasHit;
        }

        private static bool IsPointerTargetCollider(Collider collider) {
            return collider != null && collider.GetComponentInParent<RoPointerTargetMarker>() != null;
        }

        private bool TryResolveGroundSkillCell(Ray ray, out Vector2Int cell) {
            cell = default;
            if (gridSelector == null || !TryRaycastGround(ray, out RaycastHit hit)) {
                return false;
            }

            return gridSelector.TryGetCellAtWorld(hit.point, out cell);
        }

        private void MoveToTarget() {
            if (TryHoldOwnActorBlockingMotion()) {
                return;
            }

            if (hasLocalActionMotion && localActionMotion == RoSpriteMotion.Hit) {
                return;
            }

            if (!hasTarget) {
                stepActive = false;
                if (!hasLocalActionMotion && !hasCombatStandby) {
                    UpdateActorMotion(RoSpriteMotion.Idle, facingDirection);
                }

                return;
            }

            if (isGridPathMovement && waypoints.Count > 0) {
                if (hasServerGatedMove) {
                    MoveAlongServerGatedGridPath();
                    return;
                }

                MoveAlongGridPath();
                return;
            }

            MoveToWorldTarget();
        }

        private void TryStartOwnMoveFromWalkOk() {
            if (!TryGetOwnActor(out RoOnlineActorSnapshot actor) ||
                actor.Motion != RoOnlineActorMotion.Moving ||
                !actor.HasMovePath ||
                actor.MoveStartTime == 0 ||
                HasObservedOwnServerMove(actor)) {
                return;
            }

            if (!TryBuildOwnServerMovePath(actor, pendingGridPath)) {
                return;
            }

            TryTrimOwnServerMovePathForPendingTargetSkill(pendingGridPath);
            MarkObservedOwnServerMove(actor);
            TraceRuntimePerf($"move ack observed serverMove start={actor.MoveStartTime} path={actor.MovePath.StartX},{actor.MovePath.StartY}->{actor.MovePath.EndX},{actor.MovePath.EndY}");
            RecordLocalMovementTrace(
                RoMovementTraceEventType.OwnMovePacketObserved,
                $"own move packet path={actor.MovePath} speed={actor.Speed}",
                serverElapsedSeconds: 0f,
                presentedElapsedSeconds: 0f,
                segmentIndex: 0);
            ClearPendingOnlineMoveAck();
            if (hasServerGatedMove &&
                TryResolveLocalGridPathRemainingSeconds(out float remainingSeconds) &&
                ShouldQueueServerGatedContinuation(
                    gridPath,
                    pendingGridPath,
                    remainingSeconds,
                    ResolveCurrentContinuationLeadSeconds())) {
                QueueServerGatedContinuation(actor, pendingGridPath);
                return;
            }

            if (!TryApplyServerGatedGridPath(actor, pendingGridPath)) {
                ApplyGridPath(pendingGridPath, resetActiveStep: true, resetWalkDistance: !isGridPathMovement);
            }
        }

        private bool HasObservedOwnServerMove(RoOnlineActorSnapshot actor) {
            return hasObservedOwnServerMove &&
                observedOwnServerMoveStartTime == actor.MoveStartTime &&
                observedOwnServerMovePath.Equals(actor.MovePath);
        }

        private void MarkObservedOwnServerMove(RoOnlineActorSnapshot actor) {
            observedOwnServerMoveStartTime = actor.MoveStartTime;
            observedOwnServerMovePath = actor.MovePath;
            hasObservedOwnServerMove = true;
        }

        private void ClearObservedOwnServerMove() {
            hasObservedOwnServerMove = false;
            observedOwnServerMoveStartTime = 0;
            observedOwnServerMovePath = default;
        }

        private bool TryBuildOwnServerMovePath(RoOnlineActorSnapshot actor, List<Vector2Int> path) {
            path.Clear();
            var startCell = new Vector2Int(actor.MovePath.StartX, actor.MovePath.StartY);
            var endCell = new Vector2Int(actor.MovePath.EndX, actor.MovePath.EndY);
            if (gridSelector == null ||
                gridSelector.Altitude == null) {
                path.Add(startCell);
                if (endCell != startCell) {
                    path.Add(endCell);
                }

                return path.Count > 1;
            }

            if (!RoGridPathfinder.TryFindPath(gridSelector.Altitude, startCell, endCell, path)) {
                path.Clear();
            }

            return path.Count > 1;
        }

        private bool TryTrimOwnServerMovePathForPendingTargetSkill(List<Vector2Int> path) {
            if (!hasPendingTargetSkillUse) {
                return false;
            }

            float ageSeconds = GetPresentationTimeSeconds() - pendingTargetSkillSentTime;
            if (ageSeconds > PendingTargetSkillTimeoutSeconds) {
                TraceRuntimePerf($"target skill pending expired skill={pendingTargetSkillId} lv={pendingTargetSkillLevel} target={pendingTargetSkillTargetId}");
                ClearPendingTargetSkillUse();
                return false;
            }

            Vector2Int targetCell = TryResolveOnlineActorCell(pendingTargetSkillTargetId, out Vector2Int latestTargetCell)
                ? latestTargetCell
                : pendingTargetSkillTargetCell;
            bool trimmed = TryTrimPathToServerTargetSkillRange(path, targetCell, pendingTargetSkillRangeCells);
            TraceRuntimePerf($"target skill pending move handled skill={pendingTargetSkillId} lv={pendingTargetSkillLevel} target={pendingTargetSkillTargetId} targetCell={FormatCell(targetCell)} range={pendingTargetSkillRangeCells} trimmed={trimmed} end={(path != null && path.Count > 0 ? FormatCell(path[^1]) : "none")}");
            if (pendingTargetSkillRequiresSend) {
                pendingTargetSkillTargetCell = targetCell;
            } else {
                ClearPendingTargetSkillUse();
            }

            return trimmed;
        }

        private void ExpirePendingTargetSkillUse() {
            if (!hasPendingTargetSkillUse) {
                return;
            }

            float timeoutSeconds = pendingTargetSkillRequiresSend
                ? PendingTargetSkillQueuedSendTimeoutSeconds
                : PendingTargetSkillTimeoutSeconds;
            if (GetPresentationTimeSeconds() - pendingTargetSkillSentTime <= timeoutSeconds) {
                return;
            }

            TraceRuntimePerf($"target skill pending expired skill={pendingTargetSkillId} lv={pendingTargetSkillLevel} target={pendingTargetSkillTargetId}");
            ClearPendingTargetSkillUse();
        }

        private bool TryApplyServerGatedGridPath(RoOnlineActorSnapshot actor, IReadOnlyList<Vector2Int> sourcePath) {
            return TryApplyServerGatedGridPath(
                actor.MoveStartTime,
                actor.MovePath,
                actor.Speed,
                sourcePath,
                resetWalkDistance: !isGridPathMovement);
        }

        private bool TryApplyServerGatedGridPath(
            uint moveStartTime,
            RoPacketPositionPair movePath,
            short moveSpeed,
            IReadOnlyList<Vector2Int> sourcePath,
            bool resetWalkDistance
        ) {
            ClearServerGatedMove();
            if (sourcePath == null || sourcePath.Count < 2) {
                return false;
            }

            Vector3 previousPosition = transform.position;
            float currentStraightStepMilliseconds = ResolveStraightStepMilliseconds(moveSpeed, ResolveConfiguredStraightStepMilliseconds());
            serverGatedMoveWaypoints.Clear();
            for (int i = 0; i < sourcePath.Count; i++) {
                serverGatedMoveWaypoints.Add(ResolveCellWorldPosition(sourcePath[i], groundOffset));
            }

            if (!RoMovePresentation.TryBuildSegmentEndTimes(
                    sourcePath,
                    currentStraightStepMilliseconds,
                    serverGatedMoveSegmentEndTimes,
                    out serverGatedMoveDurationSeconds) ||
                serverGatedMoveDurationSeconds <= 0f) {
                ClearServerGatedMove();
                return false;
            }

            ApplyGridPath(sourcePath, resetActiveStep: true, resetWalkDistance: resetWalkDistance, recordTrace: false);
            hasServerGatedMove = true;
            serverGatedMoveStartTime = moveStartTime;
            serverGatedMovePath = movePath;
            serverGatedMoveSpeed = moveSpeed;
            lastMovementTraceSegmentIndex = -1;
            TraceRuntimePerf($"server gated path applied start={FormatCell(sourcePath[0])} end={FormatCell(sourcePath[^1])} durationMs={serverGatedMoveDurationSeconds * 1000f:F0}");
            RecordLocalMovementTrace(
                RoMovementTraceEventType.VisualMoveStarted,
                $"local server-gated visual started start={FormatCell(sourcePath[0])} end={FormatCell(sourcePath[^1])} speed={moveSpeed} {FormatMovementTuningTrace()}",
                serverElapsedSeconds: 0f,
                presentedElapsedSeconds: 0f,
                segmentIndex: 0);

            float serverElapsedSeconds = TryResolveServerGatedMoveElapsed(out float resolvedServerElapsed)
                ? resolvedServerElapsed
                : 0f;
            serverElapsedSeconds = Mathf.Clamp(serverElapsedSeconds, 0f, serverGatedMoveDurationSeconds);
            if (!RoMovementSampler.TrySample(
                    serverGatedMoveWaypoints,
                    serverGatedMoveSegmentEndTimes,
                    serverElapsedSeconds,
                    out RoMovementSample authoritativeSample)) {
                ClearServerGatedMove();
                return false;
            }

            RoMovementSample sample = authoritativeSample;
            serverGatedPresentedElapsedSeconds = serverElapsedSeconds;

            float visualElapsedSeconds = serverElapsedSeconds;
            float visualDistance = RoMovePresentation.FlatDistance(transform.position, authoritativeSample.Position);
            bool projectedOnPath = RoMovePresentation.TryProjectElapsed(
                    serverGatedMoveWaypoints,
                    serverGatedMoveSegmentEndTimes,
                    transform.position,
                    out float projectedElapsed,
                    out float distanceToPath);
            if (projectedOnPath) {
                visualElapsedSeconds = projectedElapsed;
                visualDistance = Mathf.Max(visualDistance, distanceToPath);
            }

            float hardSnapDistanceCells = projectedOnPath
                ? float.PositiveInfinity
                : MovementTuning.HardSnapCells;
            RoMovePresentationCorrection correction = RoMovePresentation.CalculateInitialLocalCorrection(
                visualElapsedSeconds,
                serverElapsedSeconds,
                serverGatedMoveDurationSeconds,
                visualDistance,
                RoMovePresentation.CalculateSpeedScaledLeadToleranceSeconds(currentStraightStepMilliseconds, MovementTuning),
                LocalMoveLeadToleranceCells,
                hardSnapDistanceCells);
            serverGatedPresentedElapsedSeconds = correction.TargetElapsedSeconds;
            sample = authoritativeSample;
            if (correction.Mode != RoMovePresentationCorrectionMode.HardSnap) {
                RoMovementSampler.TrySample(
                    serverGatedMoveWaypoints,
                    serverGatedMoveSegmentEndTimes,
                    serverGatedPresentedElapsedSeconds,
                    out sample);
            }

            if (correction.Mode == RoMovePresentationCorrectionMode.HardSnap) {
                ClearServerGatedCorrectionOffset();
                transform.position = sample.Position;
            } else {
                BeginServerGatedCorrectionOffset(previousPosition, sample.Position);
                transform.position = ResolveServerGatedVisualPosition(sample.Position, 0f);
                ApplyWalkAnimationDistance(previousPosition, transform.position);
            }

            ApplyServerGatedMoveSample(sample, serverElapsedSeconds);
            UpdateServerGatedWalkMotion(previousPosition, transform.position, sample);
            RecordServerGatedMoveTrace(sample, serverElapsedSeconds, correction.Mode, visualDistance, "local server-gated initial sample");
            return true;
        }

        private void QueueServerGatedContinuation(RoOnlineActorSnapshot actor, IReadOnlyList<Vector2Int> sourcePath) {
            pendingServerGatedContinuationPath.Clear();
            if (sourcePath != null) {
                for (int i = 0; i < sourcePath.Count; i++) {
                    pendingServerGatedContinuationPath.Add(sourcePath[i]);
                }
            }

            hasPendingServerGatedContinuation = pendingServerGatedContinuationPath.Count > 1;
            pendingServerGatedContinuationStartTime = actor.MoveStartTime;
            pendingServerGatedContinuationMovePath = actor.MovePath;
            pendingServerGatedContinuationSpeed = actor.Speed;
        }

        private bool TryActivatePendingServerGatedContinuation() {
            if (!hasPendingServerGatedContinuation || pendingServerGatedContinuationPath.Count < 2) {
                return false;
            }

            pendingGridPath.Clear();
            for (int i = 0; i < pendingServerGatedContinuationPath.Count; i++) {
                pendingGridPath.Add(pendingServerGatedContinuationPath[i]);
            }

            uint moveStartTime = pendingServerGatedContinuationStartTime;
            RoPacketPositionPair movePath = pendingServerGatedContinuationMovePath;
            short moveSpeed = pendingServerGatedContinuationSpeed;
            ClearPendingServerGatedContinuation();
            return TryApplyServerGatedGridPath(
                moveStartTime,
                movePath,
                moveSpeed,
                pendingGridPath,
                resetWalkDistance: false);
        }

        private void TryContinueQueuedOnlineMove() {
            if (!hasQueuedFinalMoveTargetCell ||
                hasPendingOnlineMoveAck ||
                hasPendingServerGatedContinuation) {
                return;
            }

            if (isGridPathMovement || stepActive) {
                if (!TryResolveLocalGridPathRemainingSeconds(out float remainingSeconds) ||
                    remainingSeconds > ResolveCurrentContinuationLeadSeconds() ||
                    gridPath.Count == 0) {
                    return;
                }

                Vector2Int commandStartCell = gridPath[^1];
                TrySendQueuedOnlineMove(commandStartCell);
                return;
            }

            if (hasTarget) {
                return;
            }

            if (TryGetOwnActor(out RoOnlineActorSnapshot actor) &&
                actor.Motion == RoOnlineActorMotion.Moving &&
                actor.HasMovePath) {
                return;
            }

            if (!TryResolveOwnServerCommandCell(out Vector2Int startCell)) {
                return;
            }

            TrySendQueuedOnlineMove(startCell);
        }

        private bool TrySendQueuedOnlineMove(Vector2Int commandStartCell) {
            Vector2Int requestedTargetCell = queuedFinalMoveTargetCell;
            if (!TryBuildOnlineCommandPath(commandStartCell, requestedTargetCell, pendingGridPath)) {
                ClearOnlineMoveCommandTargets();
                return false;
            }

            Vector2Int finalTargetCell = pendingGridPath[^1];
            if (!TryResolveCommandTargetForMaxWalkPath(
                    pendingGridPath,
                    MovementTuning.MaxWalkPathSteps,
                    out Vector2Int commandTargetCell,
                    out bool requiresContinuation)) {
                ClearOnlineMoveCommandTargets();
                return false;
            }

            if (!TrySendOnlineMoveRequest(commandStartCell, commandTargetCell)) {
                return false;
            }

            BeginPendingOnlineMoveAck(commandTargetCell);
            SetOnlineMoveCommandTargets(commandTargetCell, finalTargetCell, requiresContinuation);
            if (gridSelector != null) {
                UpdateTargetMarker(gridSelector.GetCellWorldCenter(commandTargetCell, groundOffset));
            }

            return true;
        }

        private bool TryBuildOnlineCommandPath(Vector2Int startCell, Vector2Int targetCell, List<Vector2Int> path) {
            path.Clear();
            if (gridSelector == null || gridSelector.Altitude == null) {
                if (startCell == targetCell) {
                    return false;
                }

                path.Add(startCell);
                path.Add(targetCell);
                return true;
            }

            return RoGridPathfinder.TryFindPath(
                gridSelector.Altitude,
                startCell,
                targetCell,
                path) &&
                path.Count > 1;
        }

        private bool TryResolveLocalGridPathRemainingSeconds(out float remainingSeconds) {
            remainingSeconds = 0f;
            if (!isGridPathMovement || gridPath.Count < 2) {
                return false;
            }

            if (hasServerGatedMove) {
                float elapsedSeconds = TryResolveServerGatedMoveElapsed(out float serverElapsedSeconds)
                    ? serverElapsedSeconds
                    : serverGatedPresentedElapsedSeconds;
                remainingSeconds = Mathf.Max(0f, serverGatedMoveDurationSeconds - elapsedSeconds);
                return true;
            }

            int stepIndex = Mathf.Clamp(waypointIndex, 0, gridPath.Count - 2);
            float currentStraightStepMilliseconds = ResolveCurrentStraightStepMilliseconds();
            if (stepActive) {
                remainingSeconds += Mathf.Max(0f, stepDurationSeconds - stepElapsedSeconds);
                stepIndex++;
            }

            for (int i = stepIndex; i < gridPath.Count - 1; i++) {
                remainingSeconds += CalculateClassicStepDurationSeconds(
                    gridPath[i],
                    gridPath[i + 1],
                    currentStraightStepMilliseconds);
            }

            return true;
        }

        private void TryObserveOnlineMoveAckTimeout() {
            if (!hasPendingOnlineMoveAck || loggedPendingOnlineMoveAckTimeout) {
                return;
            }

            float ageSeconds = GetPresentationTimeSeconds() - pendingOnlineMoveAckSentTime;
            if (ageSeconds < OnlineMoveAckTimeoutSeconds) {
                return;
            }

            loggedPendingOnlineMoveAckTimeout = true;
            TraceRuntimePerf($"move ack timeout target={FormatCell(pendingOnlineMoveAckTargetCell)} ageMs={ageSeconds * 1000f:F0}");
            hasPendingOnlineMoveAck = false;
            Debug.LogWarning(
                $"Move ACK timeout target={pendingOnlineMoveAckTargetCell.x},{pendingOnlineMoveAckTargetCell.y} ageMs={ageSeconds * 1000f:F0}.",
                this);
        }

        private void BeginServerGatedCorrectionOffset(Vector3 visualPosition, Vector3 authoritativePosition) {
            serverGatedCorrectionOffset = RoMovePresentation.CalculateCorrectionOffset(visualPosition, authoritativePosition);
            if (serverGatedCorrectionOffset.sqrMagnitude <= LocalMoveCorrectionOffsetEpsilonSqr) {
                ClearServerGatedCorrectionOffset();
                return;
            }

            serverGatedCorrectionElapsedSeconds = 0f;
            serverGatedCorrectionDurationSeconds = RoMovePresentation.CalculateCorrectionOffsetDurationSeconds(
                RoMovePresentation.FlatDistance(visualPosition, authoritativePosition),
                MovementTuning);
        }

        private bool HasActiveServerGatedCorrectionOffset() {
            return serverGatedCorrectionDurationSeconds > 0f &&
                serverGatedCorrectionElapsedSeconds < serverGatedCorrectionDurationSeconds &&
                serverGatedCorrectionOffset.sqrMagnitude > LocalMoveCorrectionOffsetEpsilonSqr;
        }

        private Vector3 ResolveServerGatedVisualPosition(Vector3 authoritativePosition, float deltaSeconds) {
            if (!HasActiveServerGatedCorrectionOffset()) {
                ClearServerGatedCorrectionOffset();
                return authoritativePosition;
            }

            serverGatedCorrectionElapsedSeconds = Mathf.Min(
                serverGatedCorrectionDurationSeconds,
                serverGatedCorrectionElapsedSeconds + Mathf.Max(0f, deltaSeconds));
            Vector3 position = RoMovePresentation.ApplyCorrectionOffset(
                authoritativePosition,
                serverGatedCorrectionOffset,
                serverGatedCorrectionElapsedSeconds,
                serverGatedCorrectionDurationSeconds);
            if (serverGatedCorrectionElapsedSeconds >= serverGatedCorrectionDurationSeconds) {
                ClearServerGatedCorrectionOffset();
            }

            return position;
        }

        private void MoveAlongServerGatedGridPath() {
            if (!hasServerGatedMove ||
                serverGatedMoveWaypoints.Count < 2 ||
                serverGatedMoveSegmentEndTimes.Count == 0) {
                ClearServerGatedMove();
                MoveAlongGridPath();
                return;
            }

            float deltaSeconds = GetPresentationDeltaSeconds();
            float serverElapsedSeconds = TryResolveServerGatedMoveElapsed(out float resolvedServerElapsed)
                ? resolvedServerElapsed
                : Mathf.Min(serverGatedMoveDurationSeconds, serverGatedPresentedElapsedSeconds + deltaSeconds);
            serverElapsedSeconds = Mathf.Clamp(serverElapsedSeconds, 0f, serverGatedMoveDurationSeconds);
            if (!RoMovementSampler.TrySample(
                    serverGatedMoveWaypoints,
                    serverGatedMoveSegmentEndTimes,
                    serverElapsedSeconds,
                    out RoMovementSample authoritativeSample)) {
                ClearServerGatedMove();
                MoveAlongGridPath();
                return;
            }

            float previousPresentedElapsedSeconds = serverGatedPresentedElapsedSeconds;
            float distanceToAuthoritative = RoMovePresentation.FlatDistance(transform.position, authoritativeSample.Position);
            RoMovePresentationCorrection correction = RoMovePresentation.AdvanceLocalElapsed(
                serverGatedPresentedElapsedSeconds,
                serverElapsedSeconds,
                serverGatedMoveDurationSeconds,
                deltaSeconds,
                distanceToAuthoritative,
                ResolveServerGatedLeadToleranceSeconds(),
                LocalMoveLeadToleranceCells,
                MovementTuning.CatchupSeconds,
                MovementTuning.SoftCorrectionSeconds,
                float.PositiveInfinity);
            RoMovementSample sample = authoritativeSample;
            serverGatedPresentedElapsedSeconds = correction.Mode == RoMovePresentationCorrectionMode.HardSnap
                ? correction.TargetElapsedSeconds
                : Mathf.Max(previousPresentedElapsedSeconds, correction.TargetElapsedSeconds);
            if (correction.Mode != RoMovePresentationCorrectionMode.HardSnap) {
                RoMovementSampler.TrySample(
                    serverGatedMoveWaypoints,
                    serverGatedMoveSegmentEndTimes,
                    serverGatedPresentedElapsedSeconds,
                    out sample);
            }

            Vector3 previousPosition = transform.position;
            if (correction.Mode == RoMovePresentationCorrectionMode.HardSnap) {
                ClearServerGatedCorrectionOffset();
                transform.position = sample.Position;
            } else {
                // The correction model is (authoritative + decaying offset). When the sprite trails the path
                // after a mid-move re-route, the offset decay ADDS to the walk velocity, so the sprite
                // visibly speeds up for a few cells to chase the authoritative position. Cap the per-frame
                // step to ~walk speed so that catch-up reads as a walk, not a dash. The corrected position is
                // always forward along the path toward the destination, so MoveTowards never reverses it.
                Vector3 correctedPosition = ResolveServerGatedVisualPosition(sample.Position, deltaSeconds);
                float maxCorrectionStep = ServerGatedWalkUnitsPerSecond() *
                    ResolveServerGatedCorrectionMaxWalkSpeedFactor() * deltaSeconds;
                transform.position = maxCorrectionStep > 0f
                    ? Vector3.MoveTowards(previousPosition, correctedPosition, maxCorrectionStep)
                    : correctedPosition;
                ApplyWalkAnimationDistance(previousPosition, transform.position);
            }

            ApplyServerGatedMoveSample(sample, serverElapsedSeconds);
            UpdateServerGatedWalkMotion(previousPosition, transform.position, sample);
            RecordServerGatedMoveTrace(sample, serverElapsedSeconds, correction.Mode, distanceToAuthoritative, "local server-gated sample");

            float serverRemainingSeconds = Mathf.Max(0f, serverGatedMoveDurationSeconds - serverElapsedSeconds);
            if (hasPendingServerGatedContinuation &&
                ShouldActivateServerGatedContinuation(serverRemainingSeconds, deltaSeconds) &&
                TryActivatePendingServerGatedContinuation()) {
                return;
            }

            if (sample.IsComplete &&
                RoMovePresentation.IsServerAuthoritativeMoveComplete(serverElapsedSeconds, serverGatedMoveDurationSeconds)) {
                if (TryActivatePendingServerGatedContinuation()) {
                    return;
                }

                // The authoritative move has completed. The per-frame catch-up cap can leave the sprite
                // trailing the destination on this final frame; ending the walk now would rest the sprite on
                // a different cell than actor.CellX/CellY, so the next click-to-move would start from the
                // wrong cell and travel one cell too far ("click 4, walk 5"). Keep gliding at walk speed
                // (UpdateServerGatedWalkMotion above keeps the walk pose) until the sprite actually reaches
                // the destination cell, then land exactly on it so visual and authoritative agree.
                Vector3 endPosition = serverGatedMoveWaypoints[serverGatedMoveWaypoints.Count - 1];
                if (!HasReachedServerGatedDestination(transform.position, endPosition)) {
                    return;
                }

                transform.position = endPosition;
                FinishMovement();
            }
        }

        public static bool HasReachedServerGatedDestination(Vector3 currentPosition, Vector3 endPosition) {
            return RoMovePresentation.FlatDistance(currentPosition, endPosition) <= ServerGatedArrivalToleranceCells;
        }

        private bool TryResolveServerGatedMoveElapsed(out float elapsedSeconds) {
            elapsedSeconds = 0f;
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller == null || serverGatedMoveStartTime == 0) {
                return false;
            }

            if (!controller.TryCalculateMoveElapsedSeconds(
                    serverGatedMoveStartTime,
                    GetServerClockLocalTimeSeconds(),
                    out elapsedSeconds)) {
                return false;
            }

            elapsedSeconds = Mathf.Clamp(elapsedSeconds, 0f, Mathf.Max(0f, serverGatedMoveDurationSeconds));
            return true;
        }

        private void ApplyServerGatedMoveSample(RoMovementSample sample, float serverElapsedSeconds) {
            int segmentIndex = Mathf.Clamp(sample.SegmentIndex, 0, serverGatedMoveWaypoints.Count - 2);
            waypointIndex = Mathf.Clamp(segmentIndex, 0, Mathf.Max(0, waypoints.Count - 1));
            stepStartPosition = serverGatedMoveWaypoints[segmentIndex];
            stepEndPosition = serverGatedMoveWaypoints[segmentIndex + 1];
            stepDurationSeconds = sample.SegmentDurationSeconds;
            stepElapsedSeconds = sample.SegmentElapsedSeconds;
            stepActive = !sample.IsComplete ||
                !RoMovePresentation.IsServerAuthoritativeMoveComplete(serverElapsedSeconds, serverGatedMoveDurationSeconds);
        }

        private void UpdateServerGatedWalkMotion(
            Vector3 previousPosition,
            Vector3 currentPosition,
            RoMovementSample sample
        ) {
            Vector3 visualDelta = currentPosition - previousPosition;
            var fallbackPathDelta = new Vector3(
                serverGatedMovePath.EndX - serverGatedMovePath.StartX,
                0f,
                serverGatedMovePath.EndY - serverGatedMovePath.StartY);
            Vector3 look = ResolveServerGatedWalkLookDelta(
                serverGatedMoveWaypoints,
                sample.SegmentIndex,
                currentPosition,
                visualDelta,
                fallbackPathDelta);

            if (look.sqrMagnitude > 0.001f) {
                facingDirection = RoSpriteActionMap.GetDirectionFromWorldDelta(look, facingDirection, MovementTuning.WalkFacingHysteresisDegrees);
                transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            UpdateActorMotion(RoSpriteMotion.Walk, facingDirection);
        }

        public static Vector3 ResolveServerGatedWalkLookDelta(
            IReadOnlyList<Vector3> routeWaypoints,
            int segmentIndex,
            Vector3 visualDelta,
            Vector3 fallbackPathDelta
        ) {
            Vector3 currentPosition = routeWaypoints != null && routeWaypoints.Count > 0
                ? routeWaypoints[Mathf.Clamp(segmentIndex, 0, routeWaypoints.Count - 1)]
                : Vector3.zero;
            return ResolveServerGatedWalkLookDelta(
                routeWaypoints,
                segmentIndex,
                currentPosition,
                visualDelta,
                fallbackPathDelta);
        }

        public static Vector3 ResolveServerGatedWalkLookDelta(
            IReadOnlyList<Vector3> routeWaypoints,
            int segmentIndex,
            Vector3 currentPosition,
            Vector3 visualDelta,
            Vector3 fallbackPathDelta
        ) {
            if (routeWaypoints != null && segmentIndex >= 0 && segmentIndex + 1 < routeWaypoints.Count) {
                return RoFacingResolver.ResolveWalkLookDelta(
                    routeWaypoints,
                    segmentIndex,
                    currentPosition,
                    fallbackPathDelta,
                    visualDelta);
            }

            fallbackPathDelta.y = 0f;
            if (fallbackPathDelta.sqrMagnitude > 0.001f) {
                return fallbackPathDelta;
            }

            visualDelta.y = 0f;
            return visualDelta.sqrMagnitude > 0.001f ? visualDelta : Vector3.zero;
        }

        private static Vector3 ResolveFacingWorldDelta(RoActorDirection direction) {
            return direction switch {
                RoActorDirection.North => Vector3.forward,
                RoActorDirection.NorthEast => (Vector3.forward + Vector3.right).normalized,
                RoActorDirection.East => Vector3.right,
                RoActorDirection.SouthEast => (Vector3.back + Vector3.right).normalized,
                RoActorDirection.South => Vector3.back,
                RoActorDirection.SouthWest => (Vector3.back + Vector3.left).normalized,
                RoActorDirection.West => Vector3.left,
                RoActorDirection.NorthWest => (Vector3.forward + Vector3.left).normalized,
                _ => Vector3.zero
            };
        }

        private void MoveAlongGridPath() {
            if (waypointIndex < 0 || waypointIndex >= waypoints.Count) {
                FinishMovement();
                return;
            }

            float remainingDeltaSeconds = GetPresentationDeltaSeconds();
            int completedSteps = 0;
            while (hasTarget && isGridPathMovement && waypointIndex >= 0 && waypointIndex < waypoints.Count) {
                if (!stepActive) {
                    BeginCurrentGridStep();
                }

                if (stepDurationSeconds <= 0f) {
                    CompleteCurrentGridStep();
                    completedSteps++;
                    if (completedSteps >= MaxGridStepsPerFrame) {
                        break;
                    }

                    continue;
                }

                Vector3 previousPosition = transform.position;
                remainingDeltaSeconds = ConsumeGridStepTime(
                    ref stepElapsedSeconds,
                    stepDurationSeconds,
                    remainingDeltaSeconds,
                    out bool completed
                );
                float progress = Mathf.Clamp01(stepElapsedSeconds / stepDurationSeconds);
                transform.position = Vector3.Lerp(stepStartPosition, stepEndPosition, progress);
                ApplyWalkAnimationDistance(previousPosition, transform.position);
                RecordLocalMovementTrace(
                    RoMovementTraceEventType.MovementSample,
                    "local grid sample",
                    presentedElapsedSeconds: stepElapsedSeconds,
                    segmentIndex: waypointIndex);

                if (!completed) {
                    break;
                }

                CompleteCurrentGridStep();
                completedSteps++;
                if (remainingDeltaSeconds <= 0f || completedSteps >= MaxGridStepsPerFrame) {
                    break;
                }
            }
        }

        private void CompleteCurrentGridStep() {
            Vector3 previousPosition = transform.position;
            transform.position = stepEndPosition;
            ApplyWalkAnimationDistance(previousPosition, transform.position);
            stepActive = false;
            AdvanceWaypoint();
        }

        private void BeginCurrentGridStep() {
            Vector3 current = transform.position;
            Vector3 destination = GetCurrentDestination();
            if (!lockYToGround) {
                destination.y = current.y;
            }

            Vector3 look = ResolveLocalGridStepLookDelta(current, destination);
            if (look.sqrMagnitude > 0.001f) {
                facingDirection = RoSpriteActionMap.GetDirectionFromWorldDelta(look, facingDirection, MovementTuning.WalkFacingHysteresisDegrees);
                UpdateActorMotion(RoSpriteMotion.Walk, facingDirection);
                transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            stepStartPosition = current;
            stepEndPosition = destination;
            stepElapsedSeconds = 0f;
            stepDurationSeconds = GetCurrentGridStepDurationSeconds();
            stepActive = true;
            if (waypointIndex != lastMovementTraceSegmentIndex) {
                lastMovementTraceSegmentIndex = waypointIndex;
                RecordLocalMovementTrace(
                    RoMovementTraceEventType.SegmentTransition,
                    "local grid segment transition",
                    presentedElapsedSeconds: stepElapsedSeconds,
                    segmentIndex: waypointIndex);
            }
        }

        private Vector3 ResolveLocalGridStepLookDelta(Vector3 currentPosition, Vector3 destination) {
            Vector3 fallbackDelta = destination - currentPosition;
            fallbackDelta.y = 0f;
            if (!isGridPathMovement || waypointIndex < 0) {
                return fallbackDelta;
            }

            Vector3 segmentStart = currentPosition;
            if (waypointIndex > 0 && waypointIndex - 1 < waypoints.Count) {
                segmentStart = waypoints[waypointIndex - 1];
            }

            return RoFacingResolver.ResolveWalkLookDelta(
                currentPosition,
                segmentStart,
                destination,
                waypointIndex,
                fallbackDelta);
        }

        private float GetCurrentGridStepDurationSeconds() {
            float currentStraightStepMilliseconds = ResolveCurrentStraightStepMilliseconds();
            if (waypointIndex >= 0 && waypointIndex + 1 < gridPath.Count) {
                if (waypointIndex == 0) {
                    return RoLiveMoveRoute.CalculatePartialFirstStepSeconds(
                        stepStartPosition,
                        stepEndPosition,
                        gridPath[waypointIndex],
                        gridPath[waypointIndex + 1],
                        currentStraightStepMilliseconds);
                }

                return CalculateClassicStepDurationSeconds(gridPath[waypointIndex], gridPath[waypointIndex + 1], currentStraightStepMilliseconds);
            }

            return Mathf.Max(0.001f, currentStraightStepMilliseconds / 1000f);
        }

        private float ResolveCurrentStraightStepMilliseconds() {
            return TryGetOwnActor(out RoOnlineActorSnapshot actor)
                ? ResolveStraightStepMilliseconds(actor.Speed, ResolveConfiguredStraightStepMilliseconds())
                : ResolveStraightStepMilliseconds(0, ResolveConfiguredStraightStepMilliseconds());
        }

        private float ResolveConfiguredStraightStepMilliseconds() {
            return movementTuning != null
                ? MovementTuning.StraightStepMsBaseline
                : Mathf.Max(1f, straightStepMilliseconds);
        }

        private float ResolveVisualStopSettleDurationSeconds() {
            return movementTuning != null
                ? MovementTuning.StopSettleSeconds
                : Mathf.Max(0f, visualStopSettleDurationSeconds);
        }

        private float ResolveVisualStopSettleHardSnapCells() {
            return movementTuning != null
                ? MovementTuning.HardSnapCells
                : Mathf.Max(0f, visualStopSettleHardSnapCells);
        }

        private float ResolveServerGatedCorrectionMaxWalkSpeedFactor() {
            return movementTuning != null
                ? MovementTuning.ServerGatedCorrectionMaxWalkSpeedFactor
                : ServerGatedCorrectionMaxWalkSpeedFactor;
        }

        private float ResolveServerGatedStraightStepMilliseconds() {
            return serverGatedMoveSpeed > 0
                ? ResolveStraightStepMilliseconds(serverGatedMoveSpeed, ResolveConfiguredStraightStepMilliseconds())
                : ResolveCurrentStraightStepMilliseconds();
        }

        // Walk speed in world units per second for the active server-gated move (one cell == one unit).
        private float ServerGatedWalkUnitsPerSecond() {
            float stepMilliseconds = ResolveServerGatedStraightStepMilliseconds();
            return stepMilliseconds > 0f ? 1000f / stepMilliseconds : 0f;
        }

        private float ResolveServerGatedLeadToleranceSeconds() {
            return RoMovePresentation.CalculateSpeedScaledLeadToleranceSeconds(ResolveServerGatedStraightStepMilliseconds(), MovementTuning);
        }

        private float ResolveCurrentContinuationLeadSeconds() {
            return RoMovePresentation.CalculateSpeedScaledContinuationLeadSeconds(ResolveCurrentStraightStepMilliseconds());
        }

        public static float ResolveStraightStepMilliseconds(short actorSpeed, float fallbackStraightStepMilliseconds = ClassicStraightStepMilliseconds) {
            return actorSpeed > 0
                ? actorSpeed
                : Mathf.Max(1f, fallbackStraightStepMilliseconds);
        }

        public static float CalculateWorldMoveSpeed(
            float baseWorldSpeed,
            float currentStraightStepMilliseconds,
            float baselineStraightStepMilliseconds = ClassicStraightStepMilliseconds) {
            return Mathf.Max(0.001f, baseWorldSpeed) *
                Mathf.Max(1f, baselineStraightStepMilliseconds) /
                Mathf.Max(1f, currentStraightStepMilliseconds);
        }

        public static float CalculateClassicStepDurationSeconds(Vector2Int fromCell, Vector2Int toCell, float straightStepMilliseconds) {
            return RoLiveMoveRoute.CalculateServerStepSeconds(fromCell, toCell, straightStepMilliseconds);
        }


        public static bool TryGetActiveGridStepCells(
            IReadOnlyList<Vector2Int> path,
            int waypointIndex,
            out Vector2Int fromCell,
            out Vector2Int toCell
        ) {
            if (path != null && waypointIndex >= 0 && waypointIndex + 1 < path.Count) {
                fromCell = path[waypointIndex];
                toCell = path[waypointIndex + 1];
                return true;
            }

            fromCell = default;
            toCell = default;
            return false;
        }

        public static bool TryBuildGridPathPreservingActiveStep(
            IReadOnlyList<Vector2Int> activePath,
            int activeWaypointIndex,
            IReadOnlyList<Vector2Int> pathFromActiveStepEnd,
            List<Vector2Int> result
        ) {
            if (result == null) {
                return false;
            }

            result.Clear();
            if (!TryGetActiveGridStepCells(activePath, activeWaypointIndex, out Vector2Int activeStepStart, out Vector2Int activeStepEnd) ||
                pathFromActiveStepEnd == null ||
                pathFromActiveStepEnd.Count == 0 ||
                pathFromActiveStepEnd[0] != activeStepEnd) {
                return false;
            }

            result.Add(activeStepStart);
            result.Add(activeStepEnd);
            for (int i = 1; i < pathFromActiveStepEnd.Count; i++) {
                if (result[^1] != pathFromActiveStepEnd[i]) {
                    result.Add(pathFromActiveStepEnd[i]);
                }
            }

            return true;
        }

        public static float ConsumeGridStepTime(ref float elapsedSeconds, float durationSeconds, float deltaSeconds, out bool completed) {
            if (durationSeconds <= 0f) {
                elapsedSeconds = 0f;
                completed = true;
                return Mathf.Max(0f, deltaSeconds);
            }

            float remainingStepSeconds = Mathf.Max(0f, durationSeconds - elapsedSeconds);
            float usedSeconds = Mathf.Min(Mathf.Max(0f, deltaSeconds), remainingStepSeconds);
            elapsedSeconds += usedSeconds;
            completed = elapsedSeconds >= durationSeconds;
            return Mathf.Max(0f, deltaSeconds - usedSeconds);
        }

        private void MoveToWorldTarget() {
            Vector3 current = transform.position;
            Vector3 destination = GetCurrentDestination();
            if (!lockYToGround) {
                destination.y = current.y;
            }

            Vector3 delta = destination - current;
            if (delta.magnitude <= stopDistance) {
                AdvanceWaypoint();
                return;
            }

            Vector3 look = destination - current;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) {
                facingDirection = RoSpriteActionMap.GetDirectionFromWorldDelta(look, facingDirection, MovementTuning.WalkFacingHysteresisDegrees);
                UpdateActorMotion(RoSpriteMotion.Walk, facingDirection);
            }

            float currentMoveSpeed = CalculateWorldMoveSpeed(fallbackMoveSpeed, ResolveCurrentStraightStepMilliseconds());
            Vector3 next = Vector3.MoveTowards(current, destination, currentMoveSpeed * GetPresentationDeltaSeconds());
            transform.position = next;
            ApplyWalkAnimationDistance(current, next);
            RecordLocalMovementTrace(
                RoMovementTraceEventType.MovementSample,
                "local world-target sample",
                presentedElapsedSeconds: RoMovementTraceRecord.UnsetFloat,
                segmentIndex: waypointIndex);

            if (look.sqrMagnitude > 0.001f) {
                transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }
        }

        private Vector3 GetCurrentDestination() {
            if (waypointIndex >= 0 && waypointIndex < waypoints.Count) {
                return waypoints[waypointIndex];
            }

            return targetPosition;
        }

        private void AdvanceWaypoint() {
            if (waypointIndex + 1 < waypoints.Count) {
                waypointIndex++;
                return;
            }

            FinishMovement();
        }

        private void FinishMovement() {
            bool hadMovement = gridPath.Count > 0 ||
                waypoints.Count > 0 ||
                hasTarget ||
                isGridPathMovement ||
                stepActive ||
                hasServerGatedMove;
            if (hadMovement) {
                RecordLocalMovementTrace(
                    RoMovementTraceEventType.StopMove,
                    "local movement finished",
                    presentedElapsedSeconds: stepElapsedSeconds,
                    segmentIndex: waypointIndex);
            }

            waypoints.Clear();
            waypointIndex = 0;
            hasTarget = false;
            isGridPathMovement = false;
            stepActive = false;
            ClearServerGatedMove();
            if (!TryHoldOwnActorBlockingMotion()) {
                UpdateActorMotion(RoSpriteMotion.Idle, facingDirection);
            }

            TrySubmitPendingTargetAction();
        }

        private void TrySubmitPendingTargetAction() {
            if (pendingAttackTargetId == 0 &&
                pendingPickupItemAid == 0 &&
                (!hasPendingTargetSkillUse || !pendingTargetSkillRequiresSend)) {
                return;
            }

            if (hasTarget ||
                isGridPathMovement ||
                stepActive ||
                hasPendingOnlineMoveAck ||
                IsOwnActorServerMoving()) {
                return;
            }

            uint attackTargetId = pendingAttackTargetId;
            uint pickupItemAid = pendingPickupItemAid;
            Vector2Int attackTargetCell = pendingAttackTargetCell;
            bool hasAttackTargetCell = hasPendingAttackTargetCell;
            Vector2Int pickupItemCell = pendingPickupItemCell;
            bool hasPickupItemCell = hasPendingPickupItemCell;
            Vector3 pickupWorldPosition = pendingPickupWorldPosition;
            bool hasPickupWorldPosition = hasPendingPickupWorldPosition;
            RoMinimalLoginShell shell = ResolveLoginShell(forceLookup: true);
            if (hasPendingTargetSkillUse && pendingTargetSkillRequiresSend) {
                if (pendingTargetSkillTargetId == 0 || pendingTargetSkillTargetId > int.MaxValue) {
                    ClearPendingTargetAction();
                    return;
                }

                float ageSeconds = GetPresentationTimeSeconds() - pendingTargetSkillSentTime;
                if (ageSeconds > PendingTargetSkillQueuedSendTimeoutSeconds) {
                    TraceRuntimePerf($"target skill queued expired skill={pendingTargetSkillId} lv={pendingTargetSkillLevel} target={pendingTargetSkillTargetId}");
                    ClearPendingTargetAction();
                    return;
                }

                Vector2Int targetCell = TryResolveOnlineActorCell(pendingTargetSkillTargetId, out Vector2Int latestTargetSkillCell)
                    ? latestTargetSkillCell
                    : pendingTargetSkillTargetCell;
                pendingTargetSkillTargetCell = targetCell;

                if (!TryResolveOwnServerCommandCell(out Vector2Int casterCell)) {
                    ClearPendingTargetAction();
                    return;
                }

                if (IsWithinServerPlayerSkillRange(casterCell, targetCell, pendingTargetSkillRangeCells)) {
                    TrySendPendingTargetSkill(casterCell, targetCell);
                    return;
                }

                if (TryFindTargetSkillApproachCell(casterCell, targetCell, pendingTargetSkillRangeCells, out Vector2Int approachCell) &&
                    TrySetGridPath(approachCell)) {
                    return;
                }

                ClearPendingTargetAction();
                return;
            }

            if (attackTargetId != 0 && attackTargetId <= int.MaxValue) {
                int attackRangeCells = ResolveCurrentAttackPathRangeCells();
                if (TryResolveOnlineActorCell(attackTargetId, out Vector2Int latestTargetCell)) {
                    attackTargetCell = latestTargetCell;
                    hasAttackTargetCell = true;
                    pendingAttackTargetCell = latestTargetCell;
                    hasPendingAttackTargetCell = true;
                }

                if (hasAttackTargetCell &&
                    TryResolveOwnServerCommandCell(out Vector2Int attackerCell)) {
                    if (IsWithinAttackRange(attackerCell, attackTargetCell, attackRangeCells)) {
                        ClearPendingTargetAction();
                        if (shell?.TrySendAttackRequest(unchecked((int) attackTargetId)) == true) {
                            RoActorDirection direction = ResolveAttackFacingDirection(attackTargetId, hasAttackTargetCell, attackTargetCell);
                            ApplyLocalAttackRequestFacing(direction);
                        }

                        return;
                    }

                    if (TryFindAttackApproachCell(attackerCell, attackTargetCell, attackRangeCells, out Vector2Int approachCell) &&
                        TrySetGridPath(approachCell)) {
                        return;
                    }

                    ClearPendingTargetAction();
                    return;
                }

                ClearPendingTargetAction();
                if (shell?.TrySendAttackRequest(unchecked((int) attackTargetId)) == true) {
                    RoActorDirection direction = ResolveAttackFacingDirection(attackTargetId, hasAttackTargetCell, attackTargetCell);
                    ApplyLocalAttackRequestFacing(direction);
                }

                return;
            }

            if (pickupItemAid != 0 && pickupItemAid <= int.MaxValue) {
                if (hasPickupItemCell &&
                    TryResolveOwnServerCommandCell(out Vector2Int actorCell) &&
                    !IsWithinPickupRange(actorCell, pickupItemCell, DefaultPickupRangeCells)) {
                    return;
                }

                TraceRuntimePerf($"pickup pending ready item={pickupItemAid} cell={(hasPickupItemCell ? FormatCell(pickupItemCell) : "unknown")}");
                if (TrySendPickupGroundItemRequestWithLocalMotion(
                    pickupItemAid,
                    hasPickupItemCell,
                    pickupItemCell,
                    hasPickupWorldPosition,
                    pickupWorldPosition)) {
                    ClearPendingTargetAction();
                    return;
                }

                float nowSeconds = GetPresentationTimeSeconds();
                if (!hasPendingPickupRetryStartTime) {
                    pendingPickupRetryStartTime = nowSeconds;
                    hasPendingPickupRetryStartTime = true;
                    return;
                }

                if (nowSeconds - pendingPickupRetryStartTime >= PendingPickupRetryTimeoutSeconds) {
                    TraceRuntimePerf($"pickup pending expired item={pickupItemAid}");
                    ClearPendingTargetAction();
                }
            }
        }

        private void SetPendingTargetSkillSend(
            int skillId,
            int skillLevel,
            uint targetId,
            Vector2Int targetCell,
            int rangeCells
        ) {
            pendingAttackTargetId = 0;
            pendingAttackTargetCell = default;
            hasPendingAttackTargetCell = false;
            pendingPickupItemAid = 0;
            pendingPickupItemCell = default;
            hasPendingPickupItemCell = false;
            pendingPickupWorldPosition = default;
            hasPendingPickupWorldPosition = false;
            pendingPickupRetryStartTime = 0f;
            hasPendingPickupRetryStartTime = false;
            hasPendingTargetSkillUse = true;
            pendingTargetSkillRequiresSend = true;
            pendingTargetSkillId = skillId;
            pendingTargetSkillLevel = Mathf.Max(1, skillLevel);
            pendingTargetSkillTargetId = targetId;
            pendingTargetSkillTargetCell = targetCell;
            pendingTargetSkillRangeCells = Mathf.Max(0, rangeCells);
            pendingTargetSkillSentTime = GetPresentationTimeSeconds();
            TraceRuntimePerf($"target skill queued skill={skillId} lv={pendingTargetSkillLevel} target={targetId} cell={FormatCell(targetCell)} range={pendingTargetSkillRangeCells}");
        }

        private bool TrySendPendingTargetSkill(Vector2Int casterCell, Vector2Int targetCell) {
            if (!hasPendingTargetSkillUse ||
                !pendingTargetSkillRequiresSend ||
                pendingTargetSkillTargetId == 0 ||
                pendingTargetSkillTargetId > int.MaxValue) {
                return false;
            }

            RoMinimalLoginShell shell = ResolveLoginShell(forceLookup: true);
            if (shell?.TrySendTargetSkillRequest(
                    pendingTargetSkillId,
                    pendingTargetSkillLevel,
                    unchecked((int) pendingTargetSkillTargetId)) != true) {
                return false;
            }

            RoActorDirection direction = CalculateAttackFacingDirection(casterCell, targetCell, facingDirection);
            ApplyLocalAttackRequestFacing(direction);
            TraceRuntimePerf($"target skill sent skill={pendingTargetSkillId} lv={pendingTargetSkillLevel} target={pendingTargetSkillTargetId} targetCell={FormatCell(targetCell)} direction={direction}");
            ClearPendingTargetSkillUse();
            return true;
        }

        private void ClearPendingTargetAction() {
            ClearPendingTargetSkillUse();
            pendingAttackTargetId = 0;
            pendingAttackTargetCell = default;
            hasPendingAttackTargetCell = false;
            pendingPickupItemAid = 0;
            pendingPickupItemCell = default;
            hasPendingPickupItemCell = false;
            pendingPickupWorldPosition = default;
            hasPendingPickupWorldPosition = false;
            pendingPickupRetryStartTime = 0f;
            hasPendingPickupRetryStartTime = false;
        }

        private void ClearPendingTargetSkillUse() {
            hasPendingTargetSkillUse = false;
            pendingTargetSkillId = 0;
            pendingTargetSkillLevel = 0;
            pendingTargetSkillTargetId = 0;
            pendingTargetSkillTargetCell = default;
            pendingTargetSkillRangeCells = 0;
            pendingTargetSkillSentTime = 0f;
            pendingTargetSkillRequiresSend = false;
        }

        private bool TrySendPickupGroundItemRequestWithLocalMotion(
            uint itemAid,
            bool hasItemCell,
            Vector2Int itemCell,
            bool hasWorldPosition,
            Vector3 worldPosition
        ) {
            if (itemAid == 0 || itemAid > int.MaxValue) {
                TraceRuntimePerf($"pickup send rejected invalid item={itemAid}");
                return false;
            }

            if (ResolveLoginShell(forceLookup: true)?.TrySendPickupGroundItemRequest(unchecked((int) itemAid)) != true) {
                TraceRuntimePerf($"pickup send failed item={itemAid}");
                return false;
            }

            RoActorDirection direction = ResolvePickupFacingDirection(
                hasItemCell,
                itemCell,
                hasWorldPosition,
                worldPosition);
            TraceRuntimePerf($"pickup send success item={itemAid} itemCell={(hasItemCell ? FormatCell(itemCell) : "unknown")} direction={direction}");
            BeginLocalActionMotion(RoSpriteMotion.PickUp, direction, DefaultLocalActionMotionSeconds);
            return true;
        }

        private RoActorDirection ResolvePickupFacingDirection(
            bool hasItemCell,
            Vector2Int itemCell,
            bool hasWorldPosition,
            Vector3 worldPosition
        ) {
            if (hasWorldPosition) {
                Vector3 worldDelta = worldPosition - transform.position;
                worldDelta.y = 0f;
                if (worldDelta.sqrMagnitude > 0.0001f) {
                    return RoSpriteActionMap.GetDirectionFromWorldDelta(worldDelta, facingDirection);
                }
            }

            if (hasItemCell && TryResolveCurrentCell(out Vector2Int actorCell)) {
                return CalculatePickupFacingDirection(actorCell, itemCell, facingDirection);
            }

            return facingDirection;
        }

        private RoActorDirection ResolveAttackFacingDirection(uint targetId, bool hasFallbackTargetCell, Vector2Int fallbackTargetCell) {
            if (!TryResolveCurrentCell(out Vector2Int attackerCell)) {
                return facingDirection;
            }

            if (TryResolveOnlineActorCell(targetId, out Vector2Int targetCell)) {
                return CalculateAttackFacingDirection(attackerCell, targetCell, facingDirection);
            }

            if (hasFallbackTargetCell) {
                return CalculateAttackFacingDirection(attackerCell, fallbackTargetCell, facingDirection);
            }

            return facingDirection;
        }

        private bool TryResolveOnlineActorCell(uint targetId, out Vector2Int cell) {
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller != null && controller.State.TryGetActor(targetId, out RoOnlineActorSnapshot actor)) {
                cell = RoOnlineActorPresenter.GetDisplayCell(actor);
                return true;
            }

            cell = default;
            return false;
        }

        public bool TryApplyOnlineActorMotion(RoOnlineActorSnapshot actor) {
            if (actor.ServerId == 0) {
                return false;
            }

            RoSpriteMotion motion = RoOnlineActorPresenter.ResolveMotion(actor);
            RoActorDirection direction = RoOnlineActorPresenter.ResolveFacingDirection(actor);
            bool holdFirstFrame = motion == RoSpriteMotion.Casting && actor.Motion == RoOnlineActorMotion.Casting;
            // a held cast pose lasts until the world action changes (SetAction from outside, actormotion §2.3)
            if (hasLocalActionMotion && float.IsPositiveInfinity(localActionMotionUntilTime) &&
                actor.Motion != RoOnlineActorMotion.Casting) {
                hasLocalActionMotion = false;
                localActionHeldFirstFrame = false;
            }

            if (motion == RoSpriteMotion.Walk) {
                return false;
            }

            if (TryHoldPlayDeadVisualMotion(actor)) {
                return true;
            }

            if (TryHoldBlockingActorMotion(actor, preserveLocalFacing: actor.IsSelf && ShouldPreserveOwnBlockingFacing(actor))) {
                return true;
            }

            if (IsFiniteLocalActionMotion(motion)) {
                if (hasAppliedOnlineActionRevision && lastAppliedOnlineActionRevision == actor.ActionRevision) {
                    return true;
                }

                hasAppliedOnlineActionRevision = true;
                lastAppliedOnlineActionRevision = actor.ActionRevision;
                if (motion == RoSpriteMotion.PickUp &&
                    hasLocalActionMotion &&
                    localActionMotion == RoSpriteMotion.PickUp) {
                    facingDirection = direction == RoActorDirection.None ? facingDirection : direction;
                    UpdateActorMotion(motion, facingDirection);
                    ExtendLocalActionMotionToSpriteDuration(motion);
                    return true;
                }

                StopOwnMoveForAuthoritativeAction(actor, motion);
                BeginLocalActionMotion(
                    motion,
                    direction,
                    ResolveLocalActionMotionSeconds(actor, motion),
                    holdFirstFrame,
                    actor);
                return true;
            }

            if (ShouldDeferNonBlockingOnlineMotionDuringLocalMove(hasTarget, stepActive, motion)) {
                return true;
            }

            if (motion == RoSpriteMotion.Standby) {
                facingDirection = direction == RoActorDirection.None ? facingDirection : direction;
                BeginCombatStandby();
                return true;
            }

            // Don't let the between-attacks Idle snapshot overwrite the combat Standby pose. The server
            // sends Idle in the gap between rapid attacks; without this guard the player snapped to the
            // standing Idle sprite after every swing instead of holding the READYFIGHT/Standby stance like
            // the original client.
            if (!hasLocalActionMotion && !hasCombatStandby) {
                UpdateActorMotion(motion, direction);
            }

            return true;
        }

        private void BeginLocalActionMotion(
            RoSpriteMotion motion,
            RoActorDirection direction,
            float durationSeconds,
            bool holdFirstFrame = false,
            RoOnlineActorSnapshot? actor = null) {
            RoActorDirection resolvedDirection = direction == RoActorDirection.None ? facingDirection : direction;
            facingDirection = resolvedDirection;
            float now = GetPresentationTimeSeconds();

            hasLocalActionMotion = IsFiniteLocalActionMotion(motion);
            localActionMotion = motion;
            localActionHeldFirstFrame = holdFirstFrame;
            hasCombatStandby = false;
            localActionMotionStartTime = now;
            UpdateActorMotion(motion, resolvedDirection, force: true, holdFirstFrame: holdFirstFrame);
            if (TryFallbackLocalBasicAttackMotion(ref motion, resolvedDirection)) {
                localActionMotion = motion;
            }

            // online: the original's motion rules, the same as for every other player (speed, length, next pose)
            if (actor.HasValue && TryResolveOwnMotionStep(actor.Value, motion, out RoMotionStep step, out bool settlesToStandby)) {
                ResolveCurrentActionVisual()?.SetFrameDelayScale(step.FrameDelayScale > 0f ? step.FrameDelayScale : 1f);
                localActionSettlesToStandby = settlesToStandby;
                localActionMotionUntilTime = step.Mode == RoMotionMode.Frozen
                    ? float.PositiveInfinity
                    : localActionMotionStartTime + Mathf.Max(MinimumLocalActionMotionSeconds, step.LengthMs / 1000f);
                TraceRuntimePerf($"local action begin motion={motion} direction={resolvedDirection} mode={step.Mode} scale={step.FrameDelayScale:F3} lengthMs={step.LengthMs:F0} standby={settlesToStandby}");
                return;
            }

            if (IsAttackMotion(motion)) {
                ApplyLocalPlayerAttackFrameTiming(actor, durationSeconds);
            } else if (ShouldScaleLocalActionMotionToDuration(motion)) {
                TryScaleCurrentActionToDuration(durationSeconds);
            }

            localActionSettlesToStandby = ShouldSettleLocalActionToCombatStandby(motion);
            float resolvedDurationSeconds = ResolveLocalActionPresentationSeconds(motion, durationSeconds, holdFirstFrame);
            localActionMotionUntilTime = localActionMotionStartTime + resolvedDurationSeconds;
            TraceRuntimePerf($"local action begin motion={motion} direction={resolvedDirection} hold={holdFirstFrame} packetMs={durationSeconds * 1000f:F0} windowMs={resolvedDurationSeconds * 1000f:F0}");
        }

        /// <summary>
        /// The own character's action per the original (RoOriginalCombatRules.ActorMotion — the local player animates like
        /// every other player): its play mode, speed factor and length, and whether it hands over to the ready pose.
        /// False for looping motions (nothing to time).
        /// </summary>
        /// <summary>Diagnostics (!motion self): the own character's motion and local action window.</summary>
        public string DescribeMotionForDiagnostics() {
            float now = GetPresentationTimeSeconds();
            string window = hasLocalActionMotion
                ? localActionMotion + " t=" + ((now - localActionMotionStartTime) * 1000f).ToString("F0") +
                  " until=" + (float.IsPositiveInfinity(localActionMotionUntilTime) ? "held" : ((localActionMotionUntilTime - localActionMotionStartTime) * 1000f).ToString("F0")) +
                  " standby=" + localActionSettlesToStandby
                : "no action";
            if (layeredActorVisual == null) {
                layeredActorVisual = GetComponentInChildren<RoActorLayeredVisual>();
            }

            return lastAppliedMotion + " [" + window + "] standbyPose=" + hasCombatStandby + " " +
                (layeredActorVisual != null ? layeredActorVisual.DescribeBodyFrame() : "no visual");
        }

        private bool TryResolveOwnMotionStep(RoOnlineActorSnapshot actor, RoSpriteMotion motion, out RoMotionStep step, out bool settlesToStandby) {
            IRoActorActionVisual visual = ResolveCurrentActionVisual();
            float authoredMs = visual != null && visual.TryGetMotionBaseDurationSeconds(motion, out float authoredSeconds)
                ? authoredSeconds * 1000f
                : 0f;
            var input = new RoMotionInput {
                WorldMotion = actor.Motion,
                IsPlayer = true,
                AttackMotionMs = actor.ActionMotionTimeMilliseconds,
                DurationMs = actor.ActionDurationMilliseconds,
                PlayerWithBow = Ragnarok.Combat.RoOriginalCombatRules.UsesArrow(
                    (int) RoMapWeaponActionSemantics.ResolveWeaponType(actor.Weapon, realType: true)),
                ElapsedMs = 0f,
                AuthoredMs = authoredMs
            };
            step = Ragnarok.Combat.RoOriginalCombatRules.ActorMotion(input);
            settlesToStandby = false;
            if (step.Mode == RoMotionMode.Loop) {
                return false;
            }

            if (step.Mode == RoMotionMode.Once && step.LengthMs > 0f) {
                input.ElapsedMs = step.LengthMs + 1f;
                settlesToStandby = Ragnarok.Combat.RoOriginalCombatRules.ActorMotion(input).Motion == RoSpriteMotion.Standby;
            }

            return true;
        }

        private bool TryFallbackLocalBasicAttackMotion(ref RoSpriteMotion motion, RoActorDirection direction) {
            if (motion != RoSpriteMotion.Attack2 && motion != RoSpriteMotion.Attack3) {
                return false;
            }

            IRoActorActionVisual visual = ResolveCurrentActionVisual();
            if (!RoActorActionPresenter.ShouldFallbackBasicAttackMotion(motion, visual)) {
                return false;
            }

            motion = RoSpriteMotion.Attack1;
            UpdateActorMotion(motion, direction, force: true);
            return true;
        }

        private IRoActorActionVisual ResolveCurrentActionVisual() {
            if (layeredActorVisual == null) {
                layeredActorVisual = GetComponentInChildren<RoActorLayeredVisual>();
            }

            if (layeredActorVisual != null) {
                return layeredActorVisual;
            }

            if (actorVisual == null) {
                actorVisual = GetComponentInChildren<RoActorSpriteVisual>();
            }

            return actorVisual;
        }

        private void ApplyLocalAttackRequestFacing(RoActorDirection direction) {
            RoActorDirection resolvedDirection = direction == RoActorDirection.None ? facingDirection : direction;
            facingDirection = resolvedDirection;
            if (hasLocalActionMotion ||
                hasTarget ||
                isGridPathMovement ||
                stepActive ||
                hasServerGatedMove ||
                hasPendingOnlineMoveAck) {
                return;
            }

            UpdateActorMotion(hasCombatStandby ? RoSpriteMotion.Standby : RoSpriteMotion.Idle, resolvedDirection);
        }

        private void ExpireLocalActionMotion() {
            if (!hasLocalActionMotion || GetPresentationTimeSeconds() < localActionMotionUntilTime) {
                return;
            }

            RoSpriteMotion expiredMotion = localActionMotion;
            TraceRuntimePerf($"local action expired motion={expiredMotion}");
            hasLocalActionMotion = false;
            localActionMotion = RoSpriteMotion.Idle;
            localActionMotionStartTime = 0f;
            localActionHeldFirstFrame = false;

            if (localActionSettlesToStandby) {
                if (TryHoldOwnActorBlockingMotion()) {
                    return;
                }

                BeginCombatStandby();
                return;
            }

            if (!hasTarget && !stepActive) {
                if (TryHoldOwnActorBlockingMotion()) {
                    return;
                }

                if (hasCombatStandby) {
                    UpdateActorMotion(RoSpriteMotion.Standby, facingDirection);
                } else {
                    UpdateActorMotion(RoSpriteMotion.Idle, facingDirection, force: true);
                }
            }
        }

        private void BeginCombatStandby() {
            hasCombatStandby = true;
            UpdateActorMotion(RoSpriteMotion.Standby, facingDirection);
        }

        private static bool ShouldSettleLocalActionToCombatStandby(RoSpriteMotion motion) {
            return IsAttackMotion(motion) ||
                motion == RoSpriteMotion.Hit;
        }

        public static bool ShouldDeferNonBlockingOnlineMotionDuringLocalMove(
            bool hasLocalTarget,
            bool stepActive,
            RoSpriteMotion motion
        ) {
            return (hasLocalTarget || stepActive) &&
                motion != RoSpriteMotion.Walk &&
                !IsFiniteLocalActionMotion(motion);
        }

        private static bool IsFiniteLocalActionMotion(RoSpriteMotion motion) {
            return IsAttackMotion(motion) ||
                motion == RoSpriteMotion.PickUp ||
                motion == RoSpriteMotion.Hit ||
                motion == RoSpriteMotion.Casting ||
                motion == RoSpriteMotion.Special ||
                motion == RoSpriteMotion.Throwing;
        }

        private static bool IsAttackMotion(RoSpriteMotion motion) {
            return motion == RoSpriteMotion.Attack1 ||
                motion == RoSpriteMotion.Attack2 ||
                motion == RoSpriteMotion.Attack3;
        }

        private void StopOwnMoveForAuthoritativeAction(RoOnlineActorSnapshot actor, RoSpriteMotion motion) {
            if (!actor.IsSelf ||
                !IsMovementStoppingAction(motion) ||
                !HasActiveLocalMovement()) {
                return;
            }

            Vector3 stopPosition = ResolveCellWorldPosition(new Vector2Int(actor.CellX, actor.CellY), groundOffset);
            RecordLocalMovementTrace(
                RoMovementTraceEventType.ActionInterrupt,
                $"local movement interrupted by {motion} cell={actor.CellX},{actor.CellY}",
                distanceToAuthoritative: RoMovePresentation.FlatDistance(transform.position, stopPosition),
                segmentIndex: waypointIndex);
            BeginVisualStopSettleOrHardSnap(
                stopPosition,
                null,
                actor.ActionStartTime,
                VisualStopSettleReasonActionInterrupt);
        }

        private bool HasActiveLocalMovement() {
            return gridPath.Count > 0 ||
                waypoints.Count > 0 ||
                hasTarget ||
                isGridPathMovement ||
                stepActive ||
                hasServerGatedMove ||
                hasPendingOnlineMoveAck ||
                hasQueuedFinalMoveTargetCell;
        }

        /// <summary>
        /// Actions that end the own walk. The damage motion is one of them, like in the original client (re-custom
        /// combat-timing §2.4): the walk is not resumed by the client; the server sends a new move when the character may
        /// walk again.
        /// </summary>
        private static bool IsMovementStoppingAction(RoSpriteMotion motion) {
            return IsAttackMotion(motion) ||
                motion == RoSpriteMotion.Hit ||
                motion == RoSpriteMotion.Casting ||
                motion == RoSpriteMotion.Special ||
                motion == RoSpriteMotion.Throwing;
        }

        private static float ResolveLocalActionMotionSeconds(RoOnlineActorSnapshot actor, RoSpriteMotion motion) {
            return RoActorActionPresenter.ResolveDurationSecondsFromMilliseconds(motion, actor.ActionDurationMilliseconds);
        }

        public static bool ShouldScaleLocalActionMotionToDuration(RoSpriteMotion motion) {
            return motion != RoSpriteMotion.PickUp &&
                motion != RoSpriteMotion.Casting &&
                !ShouldUseAuthoredLocalActionDuration(motion) &&
                !IsAttackMotion(motion);
        }

        public static bool ShouldUseAuthoredLocalActionDuration(RoSpriteMotion motion) {
            // roBrowser's default skill action plays the cast/skill pose for its FULL authored ACT
            // duration, then returns to idle. Players map the post-cast skill action to the Casting
            // sprite motion (RoOnlineActorMotion.Skill -> RoSpriteMotion.Casting), so it must use the
            // authored sprite duration too. Without it the local action window is only the packet's
            // source-delay (~0.45s) and ExpireLocalActionMotion drops the magician back to idle before
            // the cast pose (hand-raise) finishes, so the animation looks cut off. Monsters still use
            // Special. The cast-channel hold is excluded at the call site (holdFirstFrame) so the frozen
            // pre-cast pose is not stretched to the authored duration.
            return motion == RoSpriteMotion.Special || motion == RoSpriteMotion.Casting;
        }

        private float ResolveLocalActionPresentationSeconds(RoSpriteMotion motion, float durationSeconds, bool holdFirstFrame) {
            float resolvedDurationSeconds = Mathf.Max(MinimumLocalActionMotionSeconds, durationSeconds);
            if (IsAttackMotion(motion) &&
                TryGetCurrentVisualActionDurationSeconds(out float attackDurationSeconds) &&
                attackDurationSeconds > 0f) {
                // Player basic attacks are paced per frame from roBrowser/Rebuild weapon motion units.
                // If the ACT has more frames than those motion units, the visible swing naturally lasts
                // longer than packet amotion. Keep that full swing plus Rebuild's short follow-through
                // hold before handing back to READYFIGHT/Standby.
                resolvedDurationSeconds = Mathf.Max(resolvedDurationSeconds, attackDurationSeconds) +
                    RoActorActionPresenter.AttackFollowThroughHoldSeconds;
            }

            if (motion == RoSpriteMotion.PickUp &&
                TryGetCurrentVisualActionBaseDurationSeconds(out float spriteDurationSeconds) &&
                spriteDurationSeconds > 0f) {
                resolvedDurationSeconds = Mathf.Max(MinimumLocalActionMotionSeconds, spriteDurationSeconds);
            }

            // Only the animating post-cast should run for the authored sprite duration. The cast-channel
            // pose is frozen on the first frame (holdFirstFrame), so stretching it to the authored sprite
            // duration would just hold the frozen pose longer than the cast bar; keep its packet-timed window.
            if (!holdFirstFrame &&
                ShouldUseAuthoredLocalActionDuration(motion) &&
                TryGetCurrentVisualActionBaseDurationSeconds(out spriteDurationSeconds) &&
                spriteDurationSeconds > 0f) {
                resolvedDurationSeconds = Mathf.Max(resolvedDurationSeconds, spriteDurationSeconds);
            }

            return resolvedDurationSeconds;
        }

        private void ExtendLocalActionMotionToSpriteDuration(RoSpriteMotion motion) {
            if (motion != RoSpriteMotion.PickUp ||
                !TryGetCurrentVisualActionBaseDurationSeconds(out float spriteDurationSeconds) ||
                spriteDurationSeconds <= 0f) {
                return;
            }

            float startTime = localActionMotionStartTime > 0f
                ? localActionMotionStartTime
                : GetPresentationTimeSeconds();
            float untilTime = startTime + Mathf.Max(MinimumLocalActionMotionSeconds, spriteDurationSeconds);
            if (untilTime > localActionMotionUntilTime) {
                localActionMotionUntilTime = untilTime;
            }
        }

        private bool TryGetCurrentVisualActionBaseDurationSeconds(out float durationSeconds) {
            durationSeconds = 0f;
            if (layeredActorVisual == null) {
                layeredActorVisual = GetComponentInChildren<RoActorLayeredVisual>();
            }

            if (layeredActorVisual != null &&
                layeredActorVisual.TryGetCurrentActionBaseDurationSeconds(out durationSeconds)) {
                return true;
            }

            if (actorVisual == null) {
                actorVisual = GetComponentInChildren<RoActorSpriteVisual>();
            }

            return actorVisual != null &&
                actorVisual.TryGetCurrentActionBaseDurationSeconds(out durationSeconds);
        }

        private bool TryGetCurrentVisualActionDurationSeconds(out float durationSeconds) {
            durationSeconds = 0f;
            if (layeredActorVisual == null) {
                layeredActorVisual = GetComponentInChildren<RoActorLayeredVisual>();
            }

            if (layeredActorVisual != null &&
                layeredActorVisual.TryGetCurrentActionDurationSeconds(out durationSeconds)) {
                return true;
            }

            if (actorVisual == null) {
                actorVisual = GetComponentInChildren<RoActorSpriteVisual>();
            }

            return actorVisual != null &&
                actorVisual.TryGetCurrentActionDurationSeconds(out durationSeconds);
        }

        private void ApplyLocalPlayerAttackFrameTiming(RoOnlineActorSnapshot? actor, float attackMotionSeconds) {
            IRoActorActionVisual visual = ResolveCurrentActionVisual();
            if (actor.HasValue &&
                RoActorActionPresenter.ApplyPlayerAttackFrameTiming(visual, actor.Value, attackMotionSeconds)) {
                return;
            }

            // Fallback for non-authoritative/offline actions without actor metadata.
            TryScaleCurrentActionToDuration(attackMotionSeconds);
        }

        private void TryScaleCurrentActionToDuration(float durationSeconds) {
            if (durationSeconds <= 0f) {
                return;
            }

            if (layeredActorVisual == null) {
                layeredActorVisual = GetComponentInChildren<RoActorLayeredVisual>();
            }

            // Fit-to-duration policy lives in RoActorActionPresenter so local and remote non-attack
            // finite actions scale consistently.
            if (RoActorActionPresenter.ApplyActionScaling(layeredActorVisual, durationSeconds)) {
                return;
            }

            if (actorVisual == null) {
                actorVisual = GetComponentInChildren<RoActorSpriteVisual>();
            }

            RoActorActionPresenter.ApplyActionScaling(actorVisual, durationSeconds);
        }

        private void ApplyWalkAnimationDistance(Vector3 from, Vector3 to) {
            Vector3 delta = to - from;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.0000001f) {
                return;
            }

            accumulatedWalkDistance += delta.magnitude;
            ApplyDistanceDrivenWalkFrame();
        }

        private void ApplyDistanceDrivenWalkFrame() {
            if (!hasAppliedActorMotion || lastAppliedMotion != RoSpriteMotion.Walk) {
                return;
            }

            if (layeredActorVisual == null) {
                layeredActorVisual = GetComponentInChildren<RoActorLayeredVisual>();
            }

            if (layeredActorVisual != null) {
                layeredActorVisual.SetWalkAnimationDistance(accumulatedWalkDistance, MovementTuning);
                return;
            }

            if (actorVisual == null) {
                actorVisual = GetComponentInChildren<RoActorSpriteVisual>();
            }

            actorVisual?.SetWalkFrameIndexFromDistance(accumulatedWalkDistance, MovementTuning);
        }

        private void UpdateTargetMarker(Vector3 position) {
            if (!showTargetMarker) {
                if (targetMarker != null) {
                    Destroy(targetMarker);
                    targetMarker = null;
                }

                return;
            }

            if (targetMarker == null) {
                targetMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                targetMarker.name = "Click Target Marker";
                if (transform.parent != null) {
                    targetMarker.transform.SetParent(transform.parent, true);
                }

                targetMarker.transform.localScale = Vector3.one * 0.8f;
                Collider markerCollider = targetMarker.FindComponent<Collider>();
                if (markerCollider != null) {
                    Destroy(markerCollider);
                }

                Renderer renderer = targetMarker.FindComponent<Renderer>();
                if (renderer != null) {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
                    var material = new Material(shader);
                    material.name = "Click Target Marker Material";
                    material.color = new Color(1f, 0.95f, 0.1f, 1f);
                    renderer.sharedMaterial = material;
                }
            }

            targetMarker.transform.position = position + Vector3.up * 0.2f;
        }

        private void UpdateActorMotion(
            RoSpriteMotion motion,
            RoActorDirection direction,
            bool force = false,
            bool holdFirstFrame = false) {
            bool motionChanged = !hasAppliedActorMotion || lastAppliedMotion != motion;
            bool directionChanged = !hasAppliedActorMotion || lastAppliedDirection != direction;
            if (!force && !motionChanged && !directionChanged) {
                return;
            }

            if (force) {
                RoRuntimePerfCounters.IncrementForcedActorMotion();
            }

            lastAppliedMotion = motion;
            lastAppliedDirection = direction;
            hasAppliedActorMotion = true;

            // Finite actions (attack/hit/cast/pickup/...) must play once and hold their last frame, not
            // loop. The controller previously never set loop state, so the local player's animator stayed
            // loop=true: each attack swing wrapped back to frame 0 at the end ("animacja leci od nowa") and
            // the finite-action full-rate tick path never engaged (it keys off loop==false). Drive loop
            // from the motion type, matching how the presenter handles remote actors.
            bool shouldLoop = RoOnlineActorPresenter.ShouldLoopSpriteMotion(motion);

            if (layeredActorVisual == null) {
                layeredActorVisual = GetComponentInChildren<RoActorLayeredVisual>();
            }

            if (layeredActorVisual != null) {
                layeredActorVisual.SetLoop(shouldLoop);
                layeredActorVisual.SetFrameDelayScale(1f);
                layeredActorVisual.SetMotionAndFacing(motion, direction, restart: motionChanged || force, force: force);
                layeredActorVisual.SetMotionFirstFrameHeld(holdFirstFrame);
                if (motion == RoSpriteMotion.Walk) {
                    ApplyDistanceDrivenWalkFrame();
                }

                return;
            }

            if (actorVisual == null) {
                actorVisual = GetComponentInChildren<RoActorSpriteVisual>();
            }

            if (actorVisual != null) {
                actorVisual.SetLoop(shouldLoop);
                actorVisual.SetFrameDelayScale(1f);
                actorVisual.SetMotionAndFacing(motion, direction, restart: motionChanged || force, force: force);
                actorVisual.SetMotionFirstFrameHeld(holdFirstFrame);
                if (motion == RoSpriteMotion.Walk) {
                    ApplyDistanceDrivenWalkFrame();
                }
            }
        }
    }
}
