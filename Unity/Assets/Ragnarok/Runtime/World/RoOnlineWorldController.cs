using System;
using System.Collections.Generic;
using System.IO;
using Ragnarok.Assets;
using Ragnarok.Audio;
using Ragnarok.Combat;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Effects;
using Ragnarok.Gameplay;
using Ragnarok.Input;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.Sprites;
using UnityEngine;
using Unity.Profiling;

namespace Ragnarok.World {
    public readonly struct RoOnlineWorldBudgetSnapshot {
        public RoOnlineWorldBudgetSnapshot(
            RoOnlineWorldBudget budget,
            int activeActors,
            int pooledActorStates,
            int maxPooledActorStates,
            int presentedActors,
            int pooledPresentedActors,
            int maxPooledPresentedActors,
            int snapshotBufferCapacity,
            int activeActorOverlays,
            int pooledActorOverlays,
            int maxPooledActorOverlays,
            int activeWeatherLayers,
            int pooledWeatherLayers,
            int maxPooledWeatherLayers
        ) {
            Budget = budget;
            ActiveActors = activeActors;
            PooledActorStates = pooledActorStates;
            MaxPooledActorStates = maxPooledActorStates;
            PresentedActors = presentedActors;
            PooledPresentedActors = pooledPresentedActors;
            MaxPooledPresentedActors = maxPooledPresentedActors;
            SnapshotBufferCapacity = snapshotBufferCapacity;
            ActiveActorOverlays = activeActorOverlays;
            PooledActorOverlays = pooledActorOverlays;
            MaxPooledActorOverlays = maxPooledActorOverlays;
            ActiveWeatherLayers = activeWeatherLayers;
            PooledWeatherLayers = pooledWeatherLayers;
            MaxPooledWeatherLayers = maxPooledWeatherLayers;
        }

        public RoOnlineWorldBudget Budget { get; }
        public int ActiveActors { get; }
        public int PooledActorStates { get; }
        public int MaxPooledActorStates { get; }
        public int PresentedActors { get; }
        public int PooledPresentedActors { get; }
        public int MaxPooledPresentedActors { get; }
        public int SnapshotBufferCapacity { get; }
        public int ActiveActorOverlays { get; }
        public int PooledActorOverlays { get; }
        public int MaxPooledActorOverlays { get; }
        public int ActiveWeatherLayers { get; }
        public int PooledWeatherLayers { get; }
        public int MaxPooledWeatherLayers { get; }
        public bool IsWithinActorBudget => ActiveActors <= Budget.VisibleActorLimit;
        public bool PoolsAreWithinCeilings =>
            PooledActorStates <= MaxPooledActorStates &&
            PooledPresentedActors <= MaxPooledPresentedActors &&
            PooledActorOverlays <= MaxPooledActorOverlays &&
            PooledWeatherLayers <= MaxPooledWeatherLayers;
        public bool SnapshotBufferCoversVisibleActorBudget => SnapshotBufferCapacity >= Budget.VisibleActorLimit;
    }

    public readonly struct RoOwnActorVitalsSnapshot {
        public RoOwnActorVitalsSnapshot(bool hasVitals, uint actorId, int hp, int maxHp) {
            HasVitals = hasVitals;
            ActorId = actorId;
            Hp = hp;
            MaxHp = maxHp;
        }

        public bool HasVitals { get; }
        public uint ActorId { get; }
        public int Hp { get; }
        public int MaxHp { get; }
    }

    public sealed class RoOnlineWorldController : MonoBehaviour, IRoCombatPresentationContext, IRoCombatActorPresentationContext, IRoAmmunitionProjectileVisualContext, IRoHitContext {
        private const float PresentationDependencyLookupRetrySeconds = 0.5f;
        private const float SelectedTargetLockInitialLeaseSeconds = 0.85f;
        private const float SelectedTargetLockMinimumCombatLeaseSeconds = 1.5f;
        private const float SelectedTargetLockCombatLeasePaddingSeconds = 0.5f;
        private const ushort NvTrickDeadSkillId = 143;
        private const string HidingBashSoundPath = "effect/ef_bash";
        private const byte SkillUseFailureCauseCooldownDelay = 13;
        private const int ServerClockAnchorHardResetInnovationMilliseconds = 2000;
        private const int ServerClockAnchorSlewLimitMilliseconds = 40;
        private const float RecentFeedbackAnchorLifetimeSeconds = 3f;
        /// <summary>EF_HIT2: effect1 of a critical basic hit.</summary>
        private const int CriticalHitEffectId = 1;

        private readonly struct RoRecentFeedbackAnchor {
            public RoRecentFeedbackAnchor(Vector3 worldPosition, float expiresAtSeconds) {
                WorldPosition = worldPosition;
                ExpiresAtSeconds = expiresAtSeconds;
            }

            public Vector3 WorldPosition { get; }
            public float ExpiresAtSeconds { get; }
        }

        [SerializeField] private RoGridSelector gridSelector;
        [SerializeField] private bool createDebugMarkers;
        [SerializeField] private bool includeSelfDebugMarker;
        [SerializeField] private bool createActorPresenter = true;
        [SerializeField] private bool showStatusChangeDebugText;
        [SerializeField] private RoOnlineActorPresenter actorPresenter;
        [SerializeField] private RoActorHoverLabelOverlay actorHoverLabelOverlay;
        [SerializeField] private RoOnlineDebugMarkerRenderer debugMarkerRenderer;
        [SerializeField] private RoEffectManager effectManager;
        /// <summary>The own character's name (0xd0f6d0), for the PvP rank chat lines.</summary>
        private string ownCharacterName;
        [SerializeField] private RoRuntimeMapController runtimeMapController;
        [Header("Day / Night (server-driven)")]
        [SerializeField] private bool dayNightServerDriven = true;
        [SerializeField, Min(0.01f)] private float dayNightDayHours = 14f;
        [SerializeField, Min(0.01f)] private float dayNightNightHours = 8f;
        [SerializeField, Min(0.1f)] private float dayNightTransitionMinutes = 20f;
        [SerializeField] private RoCombatPresentationOptions combatPresentationOptions = new RoCombatPresentationOptions();
        [SerializeField] private bool showSelectedTargetLockIndicator = true;
        [SerializeField] private RoSelectedTargetLockIndicator selectedTargetLockIndicator;
        [SerializeField] private string selectedTargetDiagnostic = "target=none";

        private RoOnlineWorldState worldState = new RoOnlineWorldState();
        private readonly RoCombatTrace combatTrace = new RoCombatTrace();
        private readonly RoMovementTraceBuffer movementTrace = new RoMovementTraceBuffer();
        private RoSkillPresenter skillPresenter;
        private RoOnlineTargetSelection selectedTarget;
        private int selectedTargetRevision;
        private uint selectedTargetLockLeaseTargetId;
        private float selectedTargetLockLeaseExpiresAtSeconds;
        private bool hasServerClockAnchor;
        private uint serverClockAnchorMilliseconds;
        private float serverClockAnchorLocalSeconds;
        private bool hasLastEstimatedServerClock;
        private uint lastEstimatedServerClockMilliseconds;
        private long lastRecordedServerClockSyncSampleSequence;
        private RoServerClockSync serverClockSync;
        private const float DayNightPushIntervalSeconds = 1f;
        private const float DayNightMapEnterGraceSeconds = 3f;
        private readonly RoServerDayNightClock dayNightClock = new RoServerDayNightClock();
        private bool dayNightClockConfigured;
        private bool awaitingInitialDayNightSync;
        private float nextDayNightPushLocalSeconds;
        private Func<RoGameplayEquipmentSnapshot> ammunitionEquipmentSnapshotProvider;
        private Func<RoItemMetadataTable> ammunitionItemMetadataProvider;
        private int appliedServerTimeRevision = -1;
        private RoHitPresenter hitPresenter;
        /// <summary>Monster death vanishes held until the hits queued on the monster have played (due time, event).</summary>
        /// <summary>/swing: the further swings of multi-hit series (caster, swing length in ms) on the presentation clock.</summary>
        private readonly RoTimeline<KeyValuePair<uint, int>> multiHitSwings = new RoTimeline<KeyValuePair<uint, int>>();
        private readonly List<uint> multiHitSwingSyncIds = new List<uint>(2);
        private readonly List<KeyValuePair<float, RoMapEntryDrainEvent>> deferredDeathVanishes =
            new List<KeyValuePair<float, RoMapEntryDrainEvent>>();
        /// <summary>Warp NPC actor id → its classic EF 321 handle (launched once per warp, ended when it leaves).</summary>
        private readonly Dictionary<uint, uint> activeWarpPortalEffects = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, RoRecentFeedbackAnchor> recentFeedbackAnchors =
            new Dictionary<uint, RoRecentFeedbackAnchor>();
        private readonly List<uint> actorPresentationSyncIds = new List<uint>(64);
        private readonly List<RoOnlineActorSnapshot> warpPortalActorSnapshots = new List<RoOnlineActorSnapshot>(64);
        private readonly HashSet<uint> liveWarpPortalIds = new HashSet<uint>();
        private readonly List<uint> staleWarpPortalIds = new List<uint>(16);
        private float nextRuntimeMapControllerLookupTime;
        private int lastRuntimeMapControllerLookupFrame = -1;
        private float nextEffectManagerLookupTime;
        private int lastEffectManagerLookupFrame = -1;
        private int ownActorVitalsEventSuppressionDepth;
        private bool hasSuppressedOwnActorVitalsBefore;
        private RoOwnActorVitalsSnapshot suppressedOwnActorVitalsBefore;

        private static readonly ProfilerMarker ApplyMapEntryMarker = new ProfilerMarker("World.ApplyMapEntry");
        private static readonly ProfilerMarker ApplyOrderedDrainEventsMarker = new ProfilerMarker("World.ApplyOrderedDrain.Events");
        private static readonly ProfilerMarker ApplyOrderedDrainWorldStateMarker = new ProfilerMarker("World.ApplyOrderedDrain.WorldState");
        private static readonly ProfilerMarker ApplyMovementMarker = new ProfilerMarker("RO.WorldReplication.ApplyMovement");
        private static readonly ProfilerMarker ApplyActorStateMarker = new ProfilerMarker("RO.WorldReplication.ApplyActorState");
        private static readonly ProfilerMarker ApplyOrderedDrainPresentationMarker = new ProfilerMarker("World.ApplyOrderedDrain.Presentation");
        private static readonly ProfilerMarker SyncWarpPortalEffectsMarker = new ProfilerMarker("World.SyncWarpPortalEffects");
        private static readonly ProfilerMarker SyncDebugMarkersMarker = new ProfilerMarker("World.SyncDebugMarkers");
        private static readonly ProfilerMarker ApplyOrderedDrainFeedbackUnknownMarker = new ProfilerMarker("World.ApplyOrderedDrain.Feedback.Unknown");
        private static readonly ProfilerMarker[] ApplyOrderedDrainFeedbackKindMarkers = BuildFeedbackKindMarkers();

        public RoOnlineWorldState State => EnsureWorldState();
        public int ActorCount => EnsureWorldState().ActorCount;
        public int MovingActorCount => EnsureWorldState().MovingActorCount;
        public int PresentationRevision => EnsureWorldState().PresentationRevision;
        /// <summary>The own character's title (ZC_ACK_CHANGE_TITLE): its name card shows it.</summary>
        public void SetOwnTitle(int titleId, string titleName) {
            EnsureWorldState().SetOwnActorTitle(titleId, titleName);
        }

        public uint OwnActorId => EnsureWorldState().OwnActorId;
        public RoGridSelector GridSelector => gridSelector;
        public RoOnlineActorPresenter ActorPresenter => actorPresenter;
        public RoActorHoverLabelOverlay ActorHoverLabelOverlay => actorHoverLabelOverlay;
        public RoOnlineDebugMarkerRenderer DebugMarkerRenderer => debugMarkerRenderer;
        public RoEffectManager EffectManager => effectManager;
        public RoRuntimeMapController RuntimeMapController => runtimeMapController;
        public RoCombatPresentationOptions CombatPresentationOptions => EnsureCombatPresentationOptions();
        public RoCombatTrace CombatTrace => combatTrace;
        public RoMovementTraceBuffer MovementTrace => movementTrace;
        public bool IsMovementTraceActive => RoRuntimeDiagnostics.MovementTraceEnabled && movementTrace.IsEnabled;
        public Func<ushort, string> SkillNameResolver { get; set; }
        public bool ShowStatusChangeDebugText {
            get => showStatusChangeDebugText;
            set => showStatusChangeDebugText = value;
        }

        public RoOnlineWorldBudget Budget => RoOnlineWorldBudget.Default;
        public RoOnlineWorldBudgetSnapshot BudgetSnapshot => BuildBudgetSnapshot();
        public bool HasServerClockAnchor => hasServerClockAnchor;
        public uint ServerClockAnchorMilliseconds => serverClockAnchorMilliseconds;
        public float ServerClockAnchorLocalSeconds => serverClockAnchorLocalSeconds;
        public bool HasSelectedTarget => selectedTarget.HasTarget;
        public RoOnlineTargetSelection SelectedTarget => selectedTarget;
        public int SelectedTargetRevision => selectedTargetRevision;
        public bool ShowSelectedTargetLockIndicator {
            get => showSelectedTargetLockIndicator;
            set {
                showSelectedTargetLockIndicator = value;
                if (!showSelectedTargetLockIndicator) {
                    HideSelectedTargetLockIndicator();
                }
            }
        }
        public RoSelectedTargetLockIndicator SelectedTargetLockIndicator => selectedTargetLockIndicator;
        public bool HasSelectedTargetLockIndicator => selectedTargetLockIndicator != null &&
            selectedTargetLockIndicator.IsShowing;
        public bool HasSelectedTargetLockLease => HasActiveSelectedTargetLockLease(RoPresentationClock.NowSeconds);
        public uint SelectedTargetLockLeaseTargetId => HasSelectedTargetLockLease ? selectedTargetLockLeaseTargetId : 0u;
        public int MinimapMarkerRevision => EnsureWorldState().MinimapMarkerRevision;
        public string SelectedTargetDiagnostic => selectedTargetDiagnostic;
        /// <summary>Queued basic-attack hits, arrows and skill flinches (tests).</summary>
        public int PendingHitCountForTests => EnsureHitPresenter().PendingCount;

        public event Action<RoOnlineTargetSelection> TargetSelected;
        public event Action<RoOwnActorVitalsSnapshot> OwnActorVitalsChanged;

        private void OnValidate() {
            if (Application.isPlaying && isActiveAndEnabled) {
                SyncCombatTraceDiagnosticsFromPresentationOptions(logState: true);
            }
        }

        private void OnEnable() {
            SyncCombatTraceDiagnosticsFromPresentationOptions(logState: false);
        }

        public void SetServerClock(RoServerClockSync clock) {
            serverClockSync = clock;
            lastRecordedServerClockSyncSampleSequence = 0;
        }

        // the frame update's steps (cpuprobe)
        private static readonly Unity.Profiling.ProfilerMarker ServerTimeMarker = new Unity.Profiling.ProfilerMarker("RO.World.ServerTime");
        private static readonly Unity.Profiling.ProfilerMarker SkillCuesMarker = new Unity.Profiling.ProfilerMarker("RO.World.SkillCues");
        private static readonly Unity.Profiling.ProfilerMarker StatusesMarker = new Unity.Profiling.ProfilerMarker("RO.World.Statuses");
        private static readonly Unity.Profiling.ProfilerMarker StatusSyncMarker = new Unity.Profiling.ProfilerMarker("RO.World.Statuses.Sync");
        private static readonly Unity.Profiling.ProfilerMarker StatusEndMarker = new Unity.Profiling.ProfilerMarker("RO.World.Statuses.EndSync");
        private static readonly Unity.Profiling.ProfilerMarker StatusTickMarker = new Unity.Profiling.ProfilerMarker("RO.World.Statuses.Tick");
        private static readonly Unity.Profiling.ProfilerMarker HitsMarker = new Unity.Profiling.ProfilerMarker("RO.World.Hits");

        private void Update() {
            long onlineWorldUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                EnsureRuntimeState();
                float presentationNowSeconds = RoPresentationClock.NowSeconds;
                RecordServerClockSyncTraceIfAvailable();
                RoOnlineActorSnapshot ownActorBeforeServerTime = default;
                bool hadOwnActorBeforeServerTime = OwnActorId != 0 &&
                    worldState.TryGetActor(OwnActorId, out ownActorBeforeServerTime);
                bool hasServerTime = TryEstimateServerTimeMilliseconds(presentationNowSeconds, out uint serverTimeMilliseconds);
                if (hasServerTime) {
                    using (ServerTimeMarker.Auto()) {
                        worldState.AdvanceEstimatedServerTime(serverTimeMilliseconds);
                        SyncLocalPlayerPresentationAfterTimedWorldStateChange(
                            hadOwnActorBeforeServerTime,
                            ownActorBeforeServerTime);
                    }
                }

                if (dayNightServerDriven && hasServerTime) {
                    PushServerDayNight(presentationNowSeconds, serverTimeMilliseconds);
                }

                using (SkillCuesMarker.Auto()) {
                    EnsureSkillPresenter().ProcessDueCues(presentationNowSeconds, this);
                }

                using (StatusesMarker.Auto()) {
                    SyncActorStatuses(Time.deltaTime);
                }

                using (HitsMarker.Auto()) {
                    EnsureHitPresenter().ProcessDue(presentationNowSeconds, this);
                    ApplyDueDeferredDeathVanishes(presentationNowSeconds);
                    ApplyDueMultiHitSwings(presentationNowSeconds);
                }
                SyncSelectedTargetLockIndicator();
            } finally {
                RoRuntimePerfCounters.AddOnlineWorldUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(onlineWorldUpdateStartTicks));
            }
        }

        private void SyncLocalPlayerPresentationAfterTimedWorldStateChange(
            bool hadOwnActorBeforeServerTime,
            RoOnlineActorSnapshot ownActorBeforeServerTime
        ) {
            if (!hadOwnActorBeforeServerTime ||
                OwnActorId == 0 ||
                !worldState.TryGetActor(OwnActorId, out RoOnlineActorSnapshot ownActorAfterServerTime)) {
                return;
            }

            if (ownActorAfterServerTime.Motion == ownActorBeforeServerTime.Motion &&
                ownActorAfterServerTime.ActionRevision == ownActorBeforeServerTime.ActionRevision &&
                ownActorAfterServerTime.ActionStartTime == ownActorBeforeServerTime.ActionStartTime &&
                ownActorAfterServerTime.ActionDurationMilliseconds == ownActorBeforeServerTime.ActionDurationMilliseconds &&
                ownActorAfterServerTime.SpawnState == ownActorBeforeServerTime.SpawnState &&
                ownActorAfterServerTime.HasMovePath == ownActorBeforeServerTime.HasMovePath &&
                ownActorAfterServerTime.MoveStartTime == ownActorBeforeServerTime.MoveStartTime) {
                return;
            }

            SyncLocalPlayerAppearanceFromWorldState();
        }

        public int FlushScheduledSkillFeedbackForTests() {
            EnsureRuntimeState();
            int pendingSkillVisualCueCount = EnsureSkillPresenter().PendingCueCount;
            EnsureSkillPresenter().FlushPendingCues(this);
            return pendingSkillVisualCueCount + EnsureHitPresenter().Flush(this);
        }

        public bool CreateDebugMarkers {
            get => createDebugMarkers;
            set {
                createDebugMarkers = value;
                if (!createDebugMarkers) {
                    ClearDebugMarkers();
                } else {
                    SyncDebugMarkers();
                }
            }
        }

        public bool IncludeSelfDebugMarker {
            get => includeSelfDebugMarker;
            set {
                includeSelfDebugMarker = value;
                if (createDebugMarkers) {
                    SyncDebugMarkers();
                }
            }
        }

        public void SetGridSelector(RoGridSelector selector) {
            gridSelector = selector;
            if (debugMarkerRenderer != null) {
                debugMarkerRenderer.SetGridSelector(selector);
            }

            if (actorPresenter != null) {
                actorPresenter.SetGridSelector(selector);
            }

        }

        public void SetEffectManager(RoEffectManager manager) {
            if (!ReferenceEquals(effectManager, manager)) {
                ClearWarpPortalEffects();
            }

            effectManager = manager;
            nextEffectManagerLookupTime = 0f;
            lastEffectManagerLookupFrame = -1;
        }

        public void BindAmmunitionProjectileVisuals(
            Func<RoGameplayEquipmentSnapshot> equipmentSnapshotProvider,
            Func<RoItemMetadataTable> itemMetadataProvider
        ) {
            ammunitionEquipmentSnapshotProvider = equipmentSnapshotProvider;
            ammunitionItemMetadataProvider = itemMetadataProvider;
        }

        public void SetRuntimeMapController(RoRuntimeMapController controller) {
            runtimeMapController = controller;
            nextRuntimeMapControllerLookupTime = 0f;
            lastRuntimeMapControllerLookupFrame = -1;
            actorPresenter?.SetRuntimeMapController(controller);
            if (controller == null) {
                return;
            }

            if (controller.GridSelector != null) {
                SetGridSelector(controller.GridSelector);
            }

            if (controller.Effects != null) {
                SetEffectManager(controller.Effects);
            }
        }

        public void ClearRuntimeMapController(
            RoRuntimeMapController controller,
            RoGridSelector expectedGridSelector,
            RoEffectManager expectedEffectManager
        ) {
            bool clearedRuntimeMapController = false;
            if (ReferenceEquals(runtimeMapController, controller)) {
                runtimeMapController = null;
                nextRuntimeMapControllerLookupTime = 0f;
                lastRuntimeMapControllerLookupFrame = -1;
                clearedRuntimeMapController = true;
            }


            if (expectedGridSelector != null && ReferenceEquals(gridSelector, expectedGridSelector)) {
                SetGridSelector(null);
            }

            if (expectedEffectManager != null && ReferenceEquals(effectManager, expectedEffectManager)) {
                SetEffectManager(null);
            }
        }

        private RoOwnActorVitalsSnapshot SnapshotOwnActorVitals() {
            RoOnlineWorldState state = EnsureWorldState();
            uint actorId = state.OwnActorId;
            if (actorId != 0 &&
                state.TryGetActor(actorId, out RoOnlineActorSnapshot actor) &&
                actor.MaxHp > 0) {
                return new RoOwnActorVitalsSnapshot(true, actorId, actor.Hp, actor.MaxHp);
            }

            return new RoOwnActorVitalsSnapshot(false, 0, 0, 0);
        }

        private void PublishOwnActorVitalsIfChanged(RoOwnActorVitalsSnapshot before) {
            if (ownActorVitalsEventSuppressionDepth > 0) {
                return;
            }

            RoOwnActorVitalsSnapshot after = SnapshotOwnActorVitals();
            if (!OwnActorVitalsEqual(before, after)) {
                OwnActorVitalsChanged?.Invoke(after);
            }
        }

        private void BeginOwnActorVitalsEventScope(RoOwnActorVitalsSnapshot before) {
            if (ownActorVitalsEventSuppressionDepth == 0) {
                suppressedOwnActorVitalsBefore = before;
                hasSuppressedOwnActorVitalsBefore = true;
            }

            ownActorVitalsEventSuppressionDepth++;
        }

        private void EndOwnActorVitalsEventScope() {
            if (ownActorVitalsEventSuppressionDepth <= 0) {
                ownActorVitalsEventSuppressionDepth = 0;
                hasSuppressedOwnActorVitalsBefore = false;
                return;
            }

            ownActorVitalsEventSuppressionDepth--;
            if (ownActorVitalsEventSuppressionDepth != 0 || !hasSuppressedOwnActorVitalsBefore) {
                return;
            }

            RoOwnActorVitalsSnapshot before = suppressedOwnActorVitalsBefore;
            suppressedOwnActorVitalsBefore = default;
            hasSuppressedOwnActorVitalsBefore = false;
            PublishOwnActorVitalsIfChanged(before);
        }

        private static bool OwnActorVitalsEqual(RoOwnActorVitalsSnapshot a, RoOwnActorVitalsSnapshot b) {
            return a.HasVitals == b.HasVitals &&
                a.ActorId == b.ActorId &&
                a.Hp == b.Hp &&
                a.MaxHp == b.MaxHp;
        }

        public void ApplyMapEntry(RoLoginEnterMapResult result) {
            EnsureRuntimeState();
            awaitingInitialDayNightSync = true;
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            RoOwnActorVitalsSnapshot ownVitalsBefore = SnapshotOwnActorVitals();
            BeginOwnActorVitalsEventScope(ownVitalsBefore);
            try {
                using (ApplyMapEntryMarker.Auto()) {
                    RoCharacterInfo character = result.CharacterResult.SelectedCharacter;
                    ownCharacterName = character?.Name ?? ownCharacterName;
                    RoPvpState.BeginMap();
                    BeginMapInternal(
                        result.Aid,
                        result.AcceptedEnter,
                        character?.Name,
                        character?.CharacterId ?? 0,
                        character?.JobId ?? 0,
                        character?.Sex ?? 0,
                        syncPresentation: false);
                    worldState.ApplySelfCharacterAppearance(character);
                    ApplyDrainInternal(result.FollowUp, syncPresentation: false);
                    ApplyDrainInternal(result.InitialState, syncPresentation: false);
                    ApplyDrainInternal(result.MoveResponse, syncPresentation: false);
                    ValidateSelectedTarget();
                    bool logWorldSync = RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics;
                    long syncStartTicks = logWorldSync ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
                    RoActorPresentationSyncResult syncResult = SyncActorPresenter(force: true);
                    SyncLocalPlayerAppearanceFromWorldState();
                    ProcessFeedbackEvents(result.FollowUp);
                    ProcessFeedbackEvents(result.InitialState);
                    ProcessFeedbackEvents(result.MoveResponse);
                    SyncWarpPortalEffects();
                    SyncDebugMarkers();

                    if (logWorldSync) {
                        double syncMilliseconds =
                            (System.Diagnostics.Stopwatch.GetTimestamp() - syncStartTicks) * 1000d /
                            System.Diagnostics.Stopwatch.Frequency;
                        RoSpriteAtlasCacheStats atlasStats = RoSpriteAtlasCache.SnapshotStats();
                        RoRuntimeDiagnostics.IncrementDebugLogLine();
                        Debug.Log(
                            $"RO map entry world sync: actors={ActorCount}, presented={(actorPresenter != null ? actorPresenter.PresentedActorCount : 0)}, syncMs={syncMilliseconds:F2}, created={syncResult.Created}, rebuilt={syncResult.Rebuilt}, updated={syncResult.Updated}, removed={syncResult.Removed}, atlasCache hits={atlasStats.Hits}, misses={atlasStats.Misses}, builds={atlasStats.Builds}, shared={atlasStats.SharedBuilds}, entries={atlasStats.Entries}, atlases={atlasStats.Atlases}.",
                            this);
                    }
                }
            } finally {
                EndOwnActorVitalsEventScope();
            }
        }

        public void ApplySameMapEntry(RoLoginEnterMapResult result) {
            EnsureRuntimeState();
            awaitingInitialDayNightSync = true;
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            RoOwnActorVitalsSnapshot ownVitalsBefore = SnapshotOwnActorVitals();
            BeginOwnActorVitalsEventScope(ownVitalsBefore);
            try {
                if (!worldState.ApplyOwnMapEntryPosition(result.AcceptedEnter)) {
                    ApplyMapEntry(result);
                    return;
                }

                SyncServerClockFromState(RoPresentationClock.NowSeconds);
                ClearSelectedTarget();
                ApplyDrainIfPresent(result.FollowUp);
                ApplyDrainIfPresent(result.InitialState);
                ApplyDrainIfPresent(result.MoveResponse);
                SyncDebugMarkers();
            } finally {
                EndOwnActorVitalsEventScope();
            }
        }

        public bool ApplySameMapMovePreview(RoZcNpcMapMove mapMove) {
            EnsureRuntimeState();
            RoOwnActorVitalsSnapshot ownVitalsBefore = SnapshotOwnActorVitals();
            if (!worldState.ApplyOwnMapMovePosition(mapMove)) {
                return false;
            }

            ClearSelectedTarget();
            SyncDebugMarkers();
            PublishOwnActorVitalsIfChanged(ownVitalsBefore);
            return true;
        }

        public void BeginMap(
            RoZcAid aid,
            RoZcAcceptEnter2 acceptedEnter,
            string actorName = null,
            int characterId = 0,
            short jobId = 0,
            byte sex = 0) {
            BeginMapInternal(aid, acceptedEnter, actorName, characterId, jobId, sex, syncPresentation: true);
        }

        private void BeginMapInternal(
            RoZcAid aid,
            RoZcAcceptEnter2 acceptedEnter,
            string actorName,
            int characterId,
            short jobId,
            byte sex,
            bool syncPresentation
        ) {
            EnsureRuntimeState();
            ClearSkillVisualPresentation();
            combatTrace.Clear();
            movementTrace.Clear();
            hitPresenter?.Clear();
            deferredDeathVanishes.Clear();
            multiHitSwings.Clear();
            recentFeedbackAnchors.Clear();
            Ragnarok.UI.RoClassicChatBalloons.ClearAll();
            ClearWarpPortalEffects();
            if (statusPresenter != null) {
                statusPresenter.ClearForMapChange(OwnActorId != 0 ? OwnActorId : unchecked((uint) aid.AccountId), this);
                statusReapplyPending = true;
            }

            statusSyncRevision = -1;
            classicLocalPlayerAnchorId = 0;
            classicLocalPlayerAnchor = null;
            worldState.BeginMap(aid, acceptedEnter, actorName, characterId, jobId, sex);
            SyncServerClockFromState(RoPresentationClock.NowSeconds);
            ClearSelectedTarget();
            if (syncPresentation) {
                SyncActorPresenter(force: true);
                SyncDebugMarkers();
            }
        }

        public void ApplyDrain(RoMapEntryDrainResult drain) {
            ApplyDrainInternal(drain, syncPresentation: true);
        }

        public bool ApplyOwnActorBaseLevel(long value, bool syncPresentation = true) {
            EnsureRuntimeState();
            if (!worldState.ApplyOwnActorBaseLevel(value)) {
                return false;
            }

            if (syncPresentation && createActorPresenter && OwnActorId != 0) {
                actorPresentationSyncIds.Clear();
                actorPresentationSyncIds.Add(OwnActorId);
                EnsureActorPresenter().SyncActorIds(actorPresentationSyncIds);
            }

            return true;
        }

        private void ApplyDrainInternal(RoMapEntryDrainResult drain, bool syncPresentation) {
            EnsureRuntimeState();
            RoOwnActorVitalsSnapshot ownVitalsBefore = SnapshotOwnActorVitals();
            if (drain != null && drain.OrderedEvents.Length > 0) {
                ApplyOrderedDrain(drain, syncPresentation);
                PublishOwnActorVitalsIfChanged(ownVitalsBefore);
                return;
            }

            PresentActorDeathsBeforeVanish(drain);
            PresentActorTeleportsBeforeVanish(drain);
            float localSeconds = RoPresentationClock.NowSeconds;
            RememberFeedbackPositionsBeforeWorldState(drain, localSeconds);
            worldState.ApplyDrain(drain);
            SyncServerClockFromState(localSeconds);
            ProcessFeedbackEvents(drain);
            if (syncPresentation) {
                bool hasActorPresentationChanges = HasActorPresentationChanges(drain);
                ValidateSelectedTarget();
                if (hasActorPresentationChanges) {
                    SyncActorPresenterForDrain(drain);
                    PresentArrivals(drain);
                }

                SyncLocalPlayerAppearanceFromWorldState();
                SyncWarpPortalEffects();
                SyncDebugMarkers();
            }

            PublishOwnActorVitalsIfChanged(ownVitalsBefore);
        }

        public RoActorPresentationSyncResult FlushActorPresentation(bool force = true) {
            EnsureRuntimeState();
            ValidateSelectedTarget();
            RoActorPresentationSyncResult result = SyncActorPresenter(force);
            SyncLocalPlayerAppearanceFromWorldState();
            SyncWarpPortalEffects();
            SyncDebugMarkers();
            return result;
        }

        public bool TryApplyLocalPlayDeadSkillUse(int skillId, int targetAid) {
            if (skillId != NvTrickDeadSkillId) {
                return false;
            }

            EnsureRuntimeState();
            if (OwnActorId == 0 || OwnActorId > int.MaxValue) {
                return false;
            }

            int ownAid = unchecked((int) OwnActorId);
            if (targetAid > 0 && targetAid != ownAid) {
                return false;
            }

            if (!worldState.TryTogglePlayDeadVisualState(OwnActorId)) {
                return false;
            }

            if (createActorPresenter) {
                actorPresentationSyncIds.Clear();
                actorPresentationSyncIds.Add(OwnActorId);
                EnsureActorPresenter().SyncActorIds(actorPresentationSyncIds);
            }

            SyncLocalPlayerAppearanceFromWorldState();
            SyncDebugMarkers();
            return true;
        }

        public void ClearWorld() {
            EnsureRuntimeState();
            RoOwnActorVitalsSnapshot ownVitalsBefore = SnapshotOwnActorVitals();
            ClearSkillVisualPresentation();
            combatTrace.Clear();
            movementTrace.Clear();
            Ragnarok.UI.RoClassicChatBalloons.ClearAll();
            hitPresenter?.Clear();
            deferredDeathVanishes.Clear();
            multiHitSwings.Clear();
            recentFeedbackAnchors.Clear();
            worldState.Clear();
            ClearWarpPortalEffects();
            ClearServerClockAnchor();
            ClearSelectedTarget();
            appliedServerTimeRevision = worldState.ServerTimeRevision;
            if (actorPresenter != null) {
                actorPresenter.ClearPresentedActors();
            }

            ClearDebugMarkers();
            PublishOwnActorVitalsIfChanged(ownVitalsBefore);
        }

        public bool TryRecordMovementTrace(in RoMovementTraceRecord record) {
            if (!IsMovementTraceActive) {
                return false;
            }

            bool recorded = movementTrace.Record(record);
            if (recorded && RoRuntimeDiagnostics.MovementTraceConsoleLogEnabled) {
                Debug.Log(record.ToString(), this);
            }

            return recorded;
        }

        private void RecordServerClockSyncTraceIfAvailable() {
            if (!IsMovementTraceActive ||
                serverClockSync == null ||
                !serverClockSync.TryGetLastSample(out RoServerClockSyncSample sample) ||
                sample.Sequence == lastRecordedServerClockSyncSampleSequence) {
                return;
            }

            lastRecordedServerClockSyncSampleSequence = sample.Sequence;
            RecordServerClockTraceIfAvailable(sample.ToTraceDetail());
        }

        private void RecordServerClockTraceIfAvailable(string detail) {
            if (!IsMovementTraceActive) {
                return;
            }

            var record = new RoMovementTraceRecord(
                RoMovementTraceEventType.ServerClockSample,
                Time.frameCount,
                RoPresentationClock.NowSeconds,
                RoPresentationClock.DeltaSeconds,
                0,
                RoMapActorObjectType.Unknown,
                false,
                RoMovementTraceRecord.UnsetInt,
                RoMovementTraceRecord.UnsetInt,
                false,
                RoMovementTraceRecord.UnsetInt,
                RoMovementTraceRecord.UnsetInt,
                Vector3.zero,
                0,
                RoMovementTraceRecord.UnsetFloat,
                RoMovementTraceRecord.UnsetFloat,
                RoMovePresentationCorrectionMode.None,
                RoMovementTraceRecord.UnsetFloat,
                RoMovementTraceRecord.UnsetInt,
                RoActorDirection.None,
                RoOnlineActorMotion.Idle,
                RoSpriteMotion.Idle,
                RoMovementTraceRecord.UnsetFloat,
                detail);
            TryRecordMovementTrace(record);
        }

        public string ExportMovementTraceCsv() {
            return movementTrace.ToCsv();
        }

        public void ExportMovementTraceCsv(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Movement trace CSV path is required.", nameof(path));
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, movementTrace.ToCsv());
        }

        [ContextMenu("RO/Export Movement Trace CSV")]
        private void ExportMovementTraceCsvToPersistentDataPath() {
            string fileName = $"movement-trace-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            ExportMovementTraceCsv(path);
            Debug.Log($"Movement trace exported to {path}", this);
        }

        public RoOnlineActorSnapshot[] SnapshotActors() {
            EnsureRuntimeState();
            return worldState.SnapshotActors();
        }

        public void SnapshotActors(List<RoOnlineActorSnapshot> snapshots) {
            EnsureRuntimeState();
            worldState.SnapshotActors(snapshots);
        }

        public void SnapshotMinimapMarkers(List<RoOnlineMinimapMarkerSnapshot> markers) {
            EnsureRuntimeState();
            if (TryEstimateServerTimeMilliseconds(RoPresentationClock.NowSeconds, out uint serverTimeMilliseconds)) {
                worldState.PruneExpiredMinimapMarkers(serverTimeMilliseconds);
            }

            worldState.SnapshotMinimapMarkers(markers);
        }

        public RoOnlineWorldBudgetSnapshot BuildBudgetSnapshot() {
            EnsureRuntimeState();
            RoOnlineActorPresenter presenter = actorPresenter;
            RoEffectManager effects = effectManager;
            RoOnlineWorldBudget budget = Budget;
            RoOnlineWorldState state = worldState;
            return new RoOnlineWorldBudgetSnapshot(
                budget,
                state.ActorCount,
                state.PooledActorStateCount,
                state.MaxPooledActorStateCount,
                presenter != null ? presenter.PresentedActorCount : 0,
                presenter != null ? presenter.PooledPresentedActorCount : 0,
                presenter != null ? presenter.MaxPooledPresentedActorCount : budget.PooledPresentedActors,
                presenter != null ? presenter.SnapshotBufferCapacity : budget.SnapshotActorCapacity,
                effects != null ? effects.ActiveActorOverlayCount : 0,
                effects != null ? effects.PooledActorOverlayCount : 0,
                effects != null ? effects.MaxPooledActorOverlayCount : budget.PooledActorOverlays,
                effects != null ? effects.ActiveWeatherLayerCount : 0,
                effects != null ? effects.PooledWeatherLayerCount : 0,
                effects != null ? effects.MaxPooledWeatherLayerCount : budget.PooledWeatherLayers);
        }

        public bool TryEstimateServerTimeMilliseconds(float localSeconds, out uint serverTimeMilliseconds) {
            RoServerClockSync clock = serverClockSync;
            if (clock != null) {
                long nowMs = RoPresentationClock.LocalMillisecondsAt(localSeconds);
                if (clock.TryEstimateServerNow(nowMs, out serverTimeMilliseconds)) {
                    return true;
                }
            }

            if (!hasServerClockAnchor) {
                serverTimeMilliseconds = 0;
                return false;
            }

            double deltaSeconds = Math.Max(0d, localSeconds - serverClockAnchorLocalSeconds);
            uint deltaMilliseconds = deltaSeconds >= uint.MaxValue / 1000d
                ? uint.MaxValue
                : (uint) Math.Round(deltaSeconds * 1000d);
            serverTimeMilliseconds = ClampMonotonicServerEstimate(unchecked(serverClockAnchorMilliseconds + deltaMilliseconds));
            return true;
        }

        public bool TryCalculateMoveElapsedSeconds(uint moveStartTimeMilliseconds, float localSeconds, out float elapsedSeconds) {
            elapsedSeconds = 0f;
            if (moveStartTimeMilliseconds == 0 ||
                !TryEstimateServerTimeMilliseconds(localSeconds, out uint serverNowMilliseconds)) {
                return false;
            }

            elapsedSeconds = CalculateServerElapsedSeconds(serverNowMilliseconds, moveStartTimeMilliseconds);
            return true;
        }

        public static float CalculateServerElapsedSeconds(uint serverNowMilliseconds, uint startMilliseconds) {
            int deltaMilliseconds = unchecked((int) (serverNowMilliseconds - startMilliseconds));
            if (deltaMilliseconds <= 0) {
                return 0f;
            }

            return deltaMilliseconds / 1000f;
        }

        private uint ClampMonotonicServerEstimate(uint serverTimeMilliseconds) {
            if (hasLastEstimatedServerClock &&
                unchecked((int) (serverTimeMilliseconds - lastEstimatedServerClockMilliseconds)) < 0) {
                return lastEstimatedServerClockMilliseconds;
            }

            lastEstimatedServerClockMilliseconds = serverTimeMilliseconds;
            hasLastEstimatedServerClock = true;
            return serverTimeMilliseconds;
        }

        /// <summary>The current snapshot of an actor by server id.</summary>
        public bool TryGetActorSnapshot(uint serverId, out RoOnlineActorSnapshot actor) {
            actor = default;
            return serverId != 0 && worldState != null && worldState.TryGetActor(serverId, out actor);
        }

        public bool SelectOnlineTarget(
            uint serverId,
            RoPointerTarget pointerTarget,
            RoPointerModifierFlags modifiers
        ) {
            EnsureRuntimeState();
            if (serverId == 0 || !worldState.TryGetActor(serverId, out RoOnlineActorSnapshot actor)) {
                return false;
            }

            selectedTarget = RoOnlineTargetSelection.FromSnapshot(actor, pointerTarget, modifiers);
            selectedTargetDiagnostic = selectedTarget.ToString();
            if (selectedTarget.HasTarget) {
                ArmInitialSelectedTargetLockLease(selectedTarget);
                unchecked {
                    selectedTargetRevision++;
                }

                TargetSelected?.Invoke(selectedTarget);
                SyncSelectedTargetLockIndicator();
            }

            return selectedTarget.HasTarget;
        }

        public void ClearSelectedTarget() {
            bool hadTarget = selectedTarget.HasTarget;
            selectedTarget = default;
            selectedTargetDiagnostic = "target=none";
            ClearSelectedTargetLockLease();
            if (hadTarget) {
                unchecked {
                    selectedTargetRevision++;
                }
            }

            HideSelectedTargetLockIndicator();
        }

        private void ProcessFeedbackEvents(RoMapEntryDrainResult drain) {
            RoMapEntryDrainEvent[] orderedEvents = drain?.OrderedEvents;
            if (orderedEvents == null || orderedEvents.Length == 0) {
                return;
            }

            for (int i = 0; i < orderedEvents.Length; i++) {
                RoMapEntryDrainEvent drainEvent = orderedEvents[i];
                if (ShouldProcessFeedbackEvent(drainEvent)) {
                    using (GetFeedbackKindMarker(drainEvent.Kind).Auto()) {
                        ProcessFeedbackEvent(drainEvent);
                    }
                }
            }
        }

        private void ApplyOrderedDrain(RoMapEntryDrainResult drain, bool syncPresentation) {
            float localSeconds = RoPresentationClock.NowSeconds;
            RoMapEntryDrainEvent[] orderedEvents = drain.OrderedEvents;
            bool[] preAppliedOwnStopMovePresentations = null;
            using (ApplyOrderedDrainEventsMarker.Auto()) {
                for (int i = 0; i < orderedEvents.Length; i++) {
                    RoMapEntryDrainEvent drainEvent = orderedEvents[i];
                    if (syncPresentation && TryDeferDeathVanish(drainEvent, localSeconds)) {
                        continue;
                    }

                    RememberFeedbackPositionsBeforeWorldState(drainEvent, localSeconds);
                    PresentActorDeathBeforeVanish(drainEvent);
                    PresentActorTeleportBeforeVanish(drainEvent);
                    AdvanceWorldClockForTimedMinimapMarker(drainEvent, localSeconds);
                    ApplyWorldStateEvent(drainEvent);
                    if (TryPreApplyFollowingOwnStopMovePresentation(
                            orderedEvents,
                            i,
                            drainEvent,
                            syncPresentation,
                            out int preAppliedStopMoveIndex)) {
                        if (preAppliedOwnStopMovePresentations == null) {
                            preAppliedOwnStopMovePresentations = new bool[orderedEvents.Length];
                        }

                        preAppliedOwnStopMovePresentations[preAppliedStopMoveIndex] = true;
                    }

                    SyncOwnActorActionPresentationBeforeLaterOrderedEvents(drainEvent, syncPresentation);
                    DetectServerNightStatus(drainEvent, localSeconds);
                    if (syncPresentation &&
                        (preAppliedOwnStopMovePresentations == null ||
                         !preAppliedOwnStopMovePresentations[i])) {
                        ApplyOwnStopMovePresentation(drainEvent);
                    }

                    if (syncPresentation && ShouldProcessFeedbackEvent(drainEvent)) {
                        using (GetFeedbackKindMarker(drainEvent.Kind).Auto()) {
                            ProcessFeedbackEvent(drainEvent);
                        }
                    }
                }

                SyncServerClockFromState(localSeconds);
            }

            if (syncPresentation) {
                using (ApplyOrderedDrainPresentationMarker.Auto()) {
                    bool hasActorPresentationChanges = HasActorPresentationChanges(drain);
                    ValidateSelectedTarget();
                    if (hasActorPresentationChanges) {
                        SyncActorPresenterForDrain(drain);
                        PresentArrivals(drain);
                    }

                    SyncLocalPlayerAppearanceFromWorldState();
                    SyncWarpPortalEffects();
                    SyncDebugMarkers();
                }
            }
        }

        private bool TryPreApplyFollowingOwnStopMovePresentation(
            RoMapEntryDrainEvent[] orderedEvents,
            int currentIndex,
            RoMapEntryDrainEvent drainEvent,
            bool syncPresentation,
            out int stopMoveIndex
        ) {
            stopMoveIndex = -1;
            if (!syncPresentation ||
                drainEvent.Kind != RoMapEntryDrainEventKind.ActorAction ||
                !IsOwnActorId(drainEvent.ActorAction.SourceId) ||
                !TryFindFollowingOwnStopMoveIndex(orderedEvents, currentIndex, out stopMoveIndex)) {
                return false;
            }

            ApplyOwnStopMovePresentation(orderedEvents[stopMoveIndex]);
            return true;
        }

        private bool TryFindFollowingOwnStopMoveIndex(
            RoMapEntryDrainEvent[] orderedEvents,
            int currentIndex,
            out int stopMoveIndex
        ) {
            stopMoveIndex = -1;
            if (orderedEvents == null || currentIndex < 0 || currentIndex >= orderedEvents.Length - 1) {
                return false;
            }

            for (int i = currentIndex + 1; i < orderedEvents.Length; i++) {
                RoMapEntryDrainEvent nextEvent = orderedEvents[i];
                switch (nextEvent.Kind) {
                    case RoMapEntryDrainEventKind.StopMove:
                        if (IsOwnActorId(nextEvent.StopMove.Aid)) {
                            stopMoveIndex = i;
                            return true;
                        }

                        break;
                    case RoMapEntryDrainEventKind.ActorAction:
                        if (IsOwnActorId(nextEvent.ActorAction.SourceId)) {
                            return false;
                        }

                        break;
                    case RoMapEntryDrainEventKind.ActorMove:
                        if (IsOwnActorId(nextEvent.ActorMove.Gid)) {
                            return false;
                        }

                        break;
                    case RoMapEntryDrainEventKind.PlayerMove:
                        return false;
                }
            }

            return false;
        }

        private void SyncOwnActorActionPresentationBeforeLaterOrderedEvents(
            RoMapEntryDrainEvent drainEvent,
            bool syncPresentation
        ) {
            if (!syncPresentation ||
                drainEvent.Kind != RoMapEntryDrainEventKind.ActorAction ||
                !IsOwnActorId(drainEvent.ActorAction.SourceId)) {
                return;
            }

            SyncLocalPlayerAppearanceFromWorldState();
        }

        private void ApplyOwnStopMovePresentation(RoMapEntryDrainEvent drainEvent) {
            if (drainEvent.Kind != RoMapEntryDrainEventKind.StopMove || !IsOwnActorId(drainEvent.StopMove.Aid)) {
                return;
            }

            ResolveRuntimeMapController(forceLookup: true)?.TryApplyLocalPlayerStopCell(
                drainEvent.StopMove.X,
                drainEvent.StopMove.Y,
                null);
        }

        // The server signals night with the EFST_SKE status (id 160) on the local player: active on a
        // day->night flip, inactive on night->day, and re-stated whenever we enter a map. We feed it into
        // the day/night clock, which reconstructs the continuous time-of-day from the synchronized server
        // tick so every client renders the same sun position. A status that arrives right after a map
        // change is treated as a re-statement (not a live boundary) so zoning never snaps the sun.
        private void DetectServerNightStatus(RoMapEntryDrainEvent drainEvent, float localSeconds) {
            if (!dayNightServerDriven || drainEvent.Kind != RoMapEntryDrainEventKind.StatusChange) {
                return;
            }

            RoZcMsgStateChange change = drainEvent.StatusChange;
            if (change.StatusId != RoServerDayNightClock.NightStatusId) {
                return;
            }

            if (change.Aid != 0 && !IsOwnActorId(change.Aid)) {
                return;
            }

            EnsureDayNightClockConfigured();
            // The first night status after entering a map is the server re-stating the current state -- it
            // arrives only after the map finishes loading (which can take many seconds), so a time-based
            // grace window is unreliable. Treat the first one as a state sync (not a real flip); only later
            // flips are live boundaries. This stops a fresh login from anchoring at dusk ("rewound" time).
            bool isLiveBoundary = !awaitingInitialDayNightSync;
            awaitingInitialDayNightSync = false;
            bool hasServerTick = TryEstimateServerTimeMilliseconds(localSeconds, out uint serverTick);
            dayNightClock.OnNightStatus(change.Active, isLiveBoundary, hasServerTick, serverTick);
            nextDayNightPushLocalSeconds = 0f; // push the corrected position on the next frame
        }

        /// <summary>
        /// Diagnostics (!daytime): a fixed position in the day/night cycle (0..1, day first) shown instead of the
        /// server clock, client-side only; negative = off.
        /// </summary>
        public static float DiagnosticCyclePosition = -1f;

        private string dayNightClearedFor;

        private void PushServerDayNight(float localSeconds, uint serverTick) {
            // a map with no sky (dungeon, interior) has no night in RO: its own RSW light, day all the time
            var mapController = ResolveRuntimeMapController();
            bool skyless = mapController != null && mapController.CloudShadows != null && mapController.CloudShadows.IsSkylessMap;
            if (skyless) {
                // a map with no sky keeps its own RSW light (Payon Cave's violet), not the cycle's: restored once per
                // map load (the controller object is the same for every map, the map name is not)
                string skylessMap = mapController.MapName;
                if (skylessMap != dayNightClearedFor) {
                    mapController.ClearServerDayNightCycle();
                    dayNightClearedFor = skylessMap;
                }

                return;
            }

            dayNightClearedFor = null;
            if (DiagnosticCyclePosition >= 0f) {
                float position = DiagnosticCyclePosition;
                float overrideDayMinutes = Mathf.Max(0.01f, dayNightDayHours) * 60f;
                float overrideNightMinutes = Mathf.Max(0.01f, dayNightNightHours) * 60f;
                mapController?.ApplyServerDayNightCycle(
                    Mathf.Repeat(position, 1f) * (overrideDayMinutes + overrideNightMinutes) * 60f,
                    overrideDayMinutes,
                    overrideNightMinutes,
                    Mathf.Clamp(dayNightTransitionMinutes / overrideDayMinutes, 0.001f, 0.49f));
                return;
            }

            if (!dayNightClock.HasAnchor || localSeconds < nextDayNightPushLocalSeconds) {
                return;
            }

            nextDayNightPushLocalSeconds = localSeconds + DayNightPushIntervalSeconds;
            if (!dayNightClock.TryEvaluate(serverTick, out RoServerDayNightSample sample)) {
                return;
            }

            float dayMinutes = dayNightClock.DayDurationSeconds / 60f;
            float nightMinutes = dayNightClock.NightDurationSeconds / 60f;
            // Dawn/dusk is a fixed-length window (minutes), converted to a fraction of the day phase so a
            // long phase does not stretch the sunset to hours. Both phases share the fraction, so night's
            // sunset is proportionally shorter than the day's, which reads naturally.
            float transitionFraction = dayMinutes > 0f
                ? Mathf.Clamp(dayNightTransitionMinutes / dayMinutes, 0.001f, 0.49f)
                : 0.05f;

            ResolveRuntimeMapController()?.ApplyServerDayNightCycle(
                sample.CycleSeconds,
                dayMinutes,
                nightMinutes,
                transitionFraction);
        }

        private void EnsureDayNightClockConfigured() {
            if (dayNightClockConfigured) {
                return;
            }

            dayNightClock.ConfigureHours(
                Mathf.Max(0.01f, dayNightDayHours),
                Mathf.Max(0.01f, dayNightNightHours));
            dayNightClockConfigured = true;
        }

        private void ApplyDrainIfPresent(RoMapEntryDrainResult drain) {
            if (HasDrainContent(drain)) {
                ApplyDrain(drain);
            }
        }

        private void RememberFeedbackPositionsBeforeWorldState(RoMapEntryDrainResult drain, float nowSeconds) {
            if (drain == null) {
                return;
            }

            RoZcNotifyVanish[] actorVanishes = drain.ActorVanishes;
            for (int i = 0; i < actorVanishes.Length; i++) {
                RememberFeedbackAnchor(actorVanishes[i].Gid, nowSeconds);
            }

            RoMapEntryDrainEvent[] orderedEvents = drain.OrderedEvents;
            for (int i = 0; i < orderedEvents.Length; i++) {
                RememberFeedbackPositionsBeforeWorldState(orderedEvents[i], nowSeconds);
            }
        }

        private void RememberFeedbackPositionsBeforeWorldState(RoMapEntryDrainEvent drainEvent, float nowSeconds) {
            if (drainEvent == null) {
                return;
            }

            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.ActorVanish:
                    RememberFeedbackAnchor(drainEvent.ActorVanish.Gid, nowSeconds);
                    break;
                case RoMapEntryDrainEventKind.ActorAction:
                    RememberFeedbackAnchor(drainEvent.ActorAction.SourceId, nowSeconds);
                    RememberFeedbackAnchor(drainEvent.ActorAction.TargetId, nowSeconds);
                    break;
                case RoMapEntryDrainEventKind.SkillCast:
                    RememberFeedbackAnchor(drainEvent.SkillCast.SourceId, nowSeconds);
                    RememberFeedbackAnchor(drainEvent.SkillCast.TargetId, nowSeconds);
                    break;
                case RoMapEntryDrainEventKind.SkillDamage:
                    RememberFeedbackAnchor(drainEvent.SkillDamage.SourceId, nowSeconds);
                    RememberFeedbackAnchor(drainEvent.SkillDamage.TargetId, nowSeconds);
                    break;
                case RoMapEntryDrainEventKind.SkillPosition:
                    RememberFeedbackAnchor(drainEvent.SkillPosition.SourceId, nowSeconds);
                    RememberFeedbackAnchor(drainEvent.SkillPosition.TargetId, nowSeconds);
                    break;
                case RoMapEntryDrainEventKind.SkillResult:
                    RememberFeedbackAnchor(drainEvent.SkillResult.SourceId, nowSeconds);
                    RememberFeedbackAnchor(drainEvent.SkillResult.TargetId, nowSeconds);
                    break;
            }
        }

        private void RememberFeedbackAnchor(uint actorId, float nowSeconds) {
            if (actorId == 0) {
                return;
            }

            if (actorPresenter != null && actorPresenter.TryGetPresentedActorTransform(actorId, out Transform followTarget) && followTarget != null) {
                StoreRecentFeedbackAnchor(actorId, followTarget.position, nowSeconds);
                return;
            }

            if (worldState.TryGetActor(actorId, out RoOnlineActorSnapshot actor) ||
                worldState.TryGetActorByAccountId(actorId, out actor)) {
                Vector3 worldPosition = ResolveSnapshotWorldPosition(actor);
                StoreRecentFeedbackAnchor(actor.ServerId, worldPosition, nowSeconds);
                if (actor.AccountId != 0) {
                    StoreRecentFeedbackAnchor(actor.AccountId, worldPosition, nowSeconds);
                }
            }
        }

        private void StoreRecentFeedbackAnchor(uint actorId, Vector3 worldPosition, float nowSeconds) {
            if (actorId == 0) {
                return;
            }

            recentFeedbackAnchors[actorId] = new RoRecentFeedbackAnchor(
                worldPosition,
                nowSeconds + RecentFeedbackAnchorLifetimeSeconds);
        }

        private void ApplyWorldStateEvent(RoMapEntryDrainEvent drainEvent) {
            using (ApplyOrderedDrainWorldStateMarker.Auto()) {
                if (IsMovementReplicationEvent(drainEvent.Kind)) {
                    using (ApplyMovementMarker.Auto()) {
                        worldState.ApplyDrainEvent(drainEvent);
                    }
                } else if (IsActorStateReplicationEvent(drainEvent.Kind)) {
                    using (ApplyActorStateMarker.Auto()) {
                        worldState.ApplyDrainEvent(drainEvent);
                    }
                } else {
                    worldState.ApplyDrainEvent(drainEvent);
                }
            }
        }

        private static bool IsMovementReplicationEvent(RoMapEntryDrainEventKind kind) {
            switch (kind) {
                case RoMapEntryDrainEventKind.ActorMove:
                case RoMapEntryDrainEventKind.PlayerMove:
                case RoMapEntryDrainEventKind.StopMove:
                case RoMapEntryDrainEventKind.ActorDirectionChange:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsActorStateReplicationEvent(RoMapEntryDrainEventKind kind) {
            switch (kind) {
                case RoMapEntryDrainEventKind.ActorEntry:
                case RoMapEntryDrainEventKind.ActorVanish:
                case RoMapEntryDrainEventKind.ActorResurrection:
                case RoMapEntryDrainEventKind.ActorSpriteChange:
                case RoMapEntryDrainEventKind.ActorStateChange:
                case RoMapEntryDrainEventKind.ActorIdentity:
                case RoMapEntryDrainEventKind.ActorHpInfo:
                case RoMapEntryDrainEventKind.StatusChange:
                case RoMapEntryDrainEventKind.ParameterChange:
                case RoMapEntryDrainEventKind.StatusPropertyChange:
                case RoMapEntryDrainEventKind.GroundItemEntry:
                case RoMapEntryDrainEventKind.GroundItemDisappear:
                case RoMapEntryDrainEventKind.ActorAction:
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasDrainContent(RoMapEntryDrainResult drain) {
            return drain != null &&
                (drain.PlayerChats.Length > 0 ||
                 drain.TimeNotifications.Length > 0 ||
                 drain.ActorEntries.Length > 0 ||
                 drain.ActorMoves.Length > 0 ||
                 drain.PlayerMoves.Length > 0 ||
                 drain.StopMoves.Length > 0 ||
                 drain.ActorVanishes.Length > 0 ||
                 drain.OrderedEvents.Length > 0);
        }

        private static bool HasActorPresentationChanges(RoMapEntryDrainResult drain) {
            if (drain == null) {
                return false;
            }

            if (drain.ActorEntries.Length > 0 ||
                drain.ActorMoves.Length > 0 ||
                drain.PlayerMoves.Length > 0 ||
                drain.StopMoves.Length > 0 ||
                drain.ActorVanishes.Length > 0) {
                return true;
            }

            RoMapEntryDrainEvent[] orderedEvents = drain.OrderedEvents;
            if (orderedEvents == null || orderedEvents.Length == 0) {
                return false;
            }

            for (int i = 0; i < orderedEvents.Length; i++) {
                switch (orderedEvents[i].Kind) {
                    case RoMapEntryDrainEventKind.ActorEntry:
                    case RoMapEntryDrainEventKind.ActorMove:
                    case RoMapEntryDrainEventKind.PlayerMove:
                    case RoMapEntryDrainEventKind.StopMove:
                    case RoMapEntryDrainEventKind.ActorVanish:
                    case RoMapEntryDrainEventKind.ActorResurrection:
                    case RoMapEntryDrainEventKind.ActorDirectionChange:
                    case RoMapEntryDrainEventKind.ActorSpriteChange:
                    case RoMapEntryDrainEventKind.ActorStateChange:
                    case RoMapEntryDrainEventKind.StatusChange:
                    case RoMapEntryDrainEventKind.GroundItemEntry:
                    case RoMapEntryDrainEventKind.GroundItemDisappear:
                    case RoMapEntryDrainEventKind.ActorAction:
                    case RoMapEntryDrainEventKind.SkillCast:
                    case RoMapEntryDrainEventKind.SkillDamage:
                    case RoMapEntryDrainEventKind.SkillResult:
                    case RoMapEntryDrainEventKind.GroundSkill:
                    case RoMapEntryDrainEventKind.SkillEntry:
                    case RoMapEntryDrainEventKind.SkillPosition:
                        return true;
                    case RoMapEntryDrainEventKind.SkillUseAck:
                        if (!orderedEvents[i].SkillUseAck.IsSuccess ||
                            orderedEvents[i].SkillUseAck.SkillId == NvTrickDeadSkillId) {
                            return true;
                        }

                        break;
                    case RoMapEntryDrainEventKind.CastCancel:
                        return true;
                    case RoMapEntryDrainEventKind.ParameterChange:
                        if (orderedEvents[i].ParameterChange.Property == RoPlayerStatusProperty.Speed ||
                            orderedEvents[i].ParameterChange.Property == RoPlayerStatusProperty.BaseLevel ||
                            orderedEvents[i].ParameterChange.Property == RoPlayerStatusProperty.Job) {
                            return true;
                        }

                        break;
                    case RoMapEntryDrainEventKind.StatusPropertyChange:
                        if (orderedEvents[i].StatusPropertyChange.Property == RoPlayerStatusProperty.Speed ||
                            orderedEvents[i].StatusPropertyChange.Property == RoPlayerStatusProperty.BaseLevel ||
                            orderedEvents[i].StatusPropertyChange.Property == RoPlayerStatusProperty.Job) {
                            return true;
                        }

                        break;
                }
            }

            return false;
        }

        private RoActorPresentationSyncResult SyncActorPresenterForDrain(RoMapEntryDrainResult drain) {
            if (!createActorPresenter) {
                return RoActorPresentationSyncResult.Empty;
            }

            actorPresentationSyncIds.Clear();
            CollectActorPresentationSyncIds(drain, actorPresentationSyncIds);
            if (actorPresentationSyncIds.Count == 0) {
                return SyncActorPresenter(force: false);
            }

            return EnsureActorPresenter().SyncActorIds(actorPresentationSyncIds);
        }

        private void CollectActorPresentationSyncIds(RoMapEntryDrainResult drain, List<uint> actorIds) {
            if (drain == null) {
                return;
            }

            for (int i = 0; i < drain.ActorEntries.Length; i++) {
                AddActorPresentationSyncId(actorIds, drain.ActorEntries[i].Aid);
            }

            for (int i = 0; i < drain.ActorMoves.Length; i++) {
                AddActorPresentationSyncId(actorIds, drain.ActorMoves[i].Gid);
            }

            for (int i = 0; i < drain.PlayerMoves.Length; i++) {
                AddActorPresentationSyncId(actorIds, OwnActorId);
            }

            for (int i = 0; i < drain.StopMoves.Length; i++) {
                AddActorPresentationSyncId(actorIds, drain.StopMoves[i].Aid);
            }

            for (int i = 0; i < drain.ActorVanishes.Length; i++) {
                AddActorPresentationSyncId(actorIds, drain.ActorVanishes[i].Gid);
            }

            RoMapEntryDrainEvent[] orderedEvents = drain.OrderedEvents;
            if (orderedEvents == null) {
                return;
            }

            for (int i = 0; i < orderedEvents.Length; i++) {
                CollectActorPresentationSyncIds(orderedEvents[i], actorIds);
            }
        }

        private void CollectActorPresentationSyncIds(RoMapEntryDrainEvent drainEvent, List<uint> actorIds) {
            if (drainEvent == null) {
                return;
            }

            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.ActorEntry:
                    AddActorPresentationSyncId(actorIds, drainEvent.ActorEntry.Aid);
                    break;
                case RoMapEntryDrainEventKind.ActorMove:
                    AddActorPresentationSyncId(actorIds, drainEvent.ActorMove.Gid);
                    break;
                case RoMapEntryDrainEventKind.PlayerMove:
                    AddActorPresentationSyncId(actorIds, OwnActorId);
                    break;
                case RoMapEntryDrainEventKind.StopMove:
                    AddActorPresentationSyncId(actorIds, drainEvent.StopMove.Aid);
                    break;
                case RoMapEntryDrainEventKind.ActorVanish:
                    AddActorPresentationSyncId(actorIds, drainEvent.ActorVanish.Gid);
                    break;
                case RoMapEntryDrainEventKind.ActorResurrection:
                    AddActorPresentationSyncId(actorIds, drainEvent.ActorResurrection.Gid);
                    break;
                case RoMapEntryDrainEventKind.EntryQueue:
                    // ZC_CHANGE_GUILD: the actor's guild changed (a castle flag's emblem)
                    if (RoOnlineWorldState.TryParseActorGuildChange(drainEvent.EntryQueue, out uint guildChangeAid, out _)) {
                        AddActorPresentationSyncId(actorIds, guildChangeAid);
                    }

                    break;
                case RoMapEntryDrainEventKind.ActorDirectionChange:
                    AddActorPresentationSyncId(actorIds, drainEvent.ActorDirectionChange.Aid);
                    break;
                case RoMapEntryDrainEventKind.ActorSpriteChange:
                    AddActorPresentationSyncId(actorIds, drainEvent.ActorSpriteChange.Gid);
                    break;
                case RoMapEntryDrainEventKind.ActorStateChange:
                    AddActorPresentationSyncId(actorIds, drainEvent.ActorStateChange.Aid);
                    break;
                case RoMapEntryDrainEventKind.StatusChange:
                    AddActorPresentationSyncId(actorIds, drainEvent.StatusChange.Aid == 0 ? OwnActorId : drainEvent.StatusChange.Aid);
                    break;
                case RoMapEntryDrainEventKind.GroundItemEntry:
                    AddActorPresentationSyncId(actorIds, drainEvent.GroundItemEntry.ItemAid);
                    break;
                case RoMapEntryDrainEventKind.GroundItemDisappear:
                    AddActorPresentationSyncId(actorIds, drainEvent.GroundItemDisappear.ItemAid);
                    break;
                case RoMapEntryDrainEventKind.ActorAction:
                    AddActorPresentationSyncId(actorIds, drainEvent.ActorAction.SourceId);
                    AddActorPresentationSyncId(actorIds, drainEvent.ActorAction.TargetId);
                    break;
                case RoMapEntryDrainEventKind.SkillCast:
                    AddActorPresentationSyncId(actorIds, drainEvent.SkillCast.SourceId);
                    AddActorPresentationSyncId(actorIds, drainEvent.SkillCast.TargetId);
                    break;
                case RoMapEntryDrainEventKind.SkillDamage:
                    AddActorPresentationSyncId(actorIds, drainEvent.SkillDamage.SourceId);
                    AddActorPresentationSyncId(actorIds, drainEvent.SkillDamage.TargetId);
                    break;
                case RoMapEntryDrainEventKind.SkillResult:
                    AddActorPresentationSyncId(actorIds, drainEvent.SkillResult.SourceId);
                    AddActorPresentationSyncId(actorIds, drainEvent.SkillResult.TargetId);
                    break;
                case RoMapEntryDrainEventKind.GroundSkill:
                    AddActorPresentationSyncId(actorIds, drainEvent.GroundSkill.SourceId);
                    break;
                case RoMapEntryDrainEventKind.SkillEntry:
                    AddActorPresentationSyncId(actorIds, drainEvent.SkillEntry.CreatorId);
                    break;
                case RoMapEntryDrainEventKind.SkillUseAck:
                    if (!drainEvent.SkillUseAck.IsSuccess ||
                        drainEvent.SkillUseAck.SkillId == NvTrickDeadSkillId) {
                        AddActorPresentationSyncId(actorIds, OwnActorId);
                    }

                    break;
                case RoMapEntryDrainEventKind.CastCancel:
                    if (drainEvent.CastCancel.ActorId == OwnActorId) {
                        AddActorPresentationSyncId(actorIds, OwnActorId);
                    }

                    break;
                case RoMapEntryDrainEventKind.SkillPosition:
                    AddActorPresentationSyncId(actorIds, drainEvent.SkillPosition.SourceId);
                    AddActorPresentationSyncId(actorIds, drainEvent.SkillPosition.TargetId);
                    break;
                case RoMapEntryDrainEventKind.ParameterChange:
                    if (drainEvent.ParameterChange.Property == RoPlayerStatusProperty.Speed ||
                        drainEvent.ParameterChange.Property == RoPlayerStatusProperty.BaseLevel ||
                        drainEvent.ParameterChange.Property == RoPlayerStatusProperty.Job) {
                        AddActorPresentationSyncId(actorIds, OwnActorId);
                    }

                    break;
                case RoMapEntryDrainEventKind.StatusPropertyChange:
                    if (drainEvent.StatusPropertyChange.Property == RoPlayerStatusProperty.Speed ||
                        drainEvent.StatusPropertyChange.Property == RoPlayerStatusProperty.BaseLevel ||
                        drainEvent.StatusPropertyChange.Property == RoPlayerStatusProperty.Job) {
                        AddActorPresentationSyncId(actorIds, OwnActorId);
                    }

                    break;
            }
        }

        private static void AddActorPresentationSyncId(List<uint> actorIds, uint actorId) {
            if (actorId != 0) {
                actorIds.Add(actorId);
            }
        }

        private void AdvanceWorldClockForTimedMinimapMarker(RoMapEntryDrainEvent drainEvent, float localSeconds) {
            if (drainEvent.Kind != RoMapEntryDrainEventKind.MinimapMarker || drainEvent.MinimapMarker.TypeRaw != 0) {
                return;
            }

            if (TryEstimateServerTimeMilliseconds(localSeconds, out uint serverTimeMilliseconds)) {
                worldState.AdvanceEstimatedServerTime(serverTimeMilliseconds);
            }
        }

        private void PresentActorDeathsBeforeVanish(RoMapEntryDrainResult drain) {
            if (drain == null || drain.ActorVanishes == null || drain.ActorVanishes.Length == 0) {
                return;
            }

            for (int i = 0; i < drain.ActorVanishes.Length; i++) {
                PresentActorDeathBeforeVanish(drain.ActorVanishes[i]);
            }
        }

        /// <summary>
        /// The original lets the hits queued on a monster play before it dies: a Sonic Blow's eight hits run out over
        /// 1.4 s on a standing target that falls after the last hurt motion, although the server sends the death right
        /// after the damage. A monster's death vanish waits here until its queued hits and their hurt motion are over;
        /// before, the target left at once and the rest of the series showed nothing.
        /// </summary>
        private bool TryDeferDeathVanish(RoMapEntryDrainEvent drainEvent, float now) {
            if (drainEvent.Kind == RoMapEntryDrainEventKind.ActorEntry && deferredDeathVanishes.Count > 0) {
                // the same unit enters again (respawn) while its death is held: the death goes first
                uint entryId = drainEvent.ActorEntry.Aid;
                for (int i = 0; i < deferredDeathVanishes.Count; i++) {
                    if (deferredDeathVanishes[i].Value.ActorVanish.Gid == entryId) {
                        ApplyDeferredDeathVanishes(float.PositiveInfinity);
                        break;
                    }
                }

                return false;
            }

            if (drainEvent.Kind != RoMapEntryDrainEventKind.ActorVanish || hitPresenter == null) {
                return false;
            }

            RoZcNotifyVanish vanish = drainEvent.ActorVanish;
            if (vanish.Type != RoMapActorVanishType.Died || IsOwnActorId(vanish.Gid) ||
                !worldState.TryGetActor(vanish.Gid, out RoOnlineActorSnapshot actor) ||
                actor.ObjectType == RoMapActorObjectType.Player ||
                !hitPresenter.TryGetPendingHitsEnd(vanish.Gid, out float end) || end <= now) {
                return false;
            }

            deferredDeathVanishes.Add(new KeyValuePair<float, RoMapEntryDrainEvent>(end, drainEvent));
            return true;
        }

        private void ApplyDueDeferredDeathVanishes(float now) {
            if (deferredDeathVanishes.Count > 0) {
                ApplyDeferredDeathVanishes(now);
            }
        }

        private void ApplyDeferredDeathVanishes(float now) {
            List<RoMapEntryDrainEvent> due = null;
            for (int i = 0; i < deferredDeathVanishes.Count; ) {
                if (deferredDeathVanishes[i].Key <= now) {
                    (due ??= new List<RoMapEntryDrainEvent>()).Add(deferredDeathVanishes[i].Value);
                    deferredDeathVanishes.RemoveAt(i);
                } else {
                    i++;
                }
            }

            if (due != null) {
                ApplyDrainInternal(new RoMapEntryDrainResult(
                    null, null, null, null, null, null, null, null, due.ToArray(), false, null), syncPresentation: true);
            }
        }

        private void PresentActorDeathBeforeVanish(RoMapEntryDrainEvent drainEvent) {
            if (drainEvent.Kind == RoMapEntryDrainEventKind.ActorVanish) {
                PresentActorDeathBeforeVanish(drainEvent.ActorVanish);
            }
        }

        private void PresentActorDeathBeforeVanish(RoZcNotifyVanish vanish) {
            Ragnarok.UI.RoClassicChatBalloons.Clear(vanish.Gid);

            if (vanish.Type != RoMapActorVanishType.Died ||
                vanish.Gid == 0 ||
                !createActorPresenter ||
                !worldState.TryGetActor(vanish.Gid, out RoOnlineActorSnapshot actor)) {
                return;
            }

            if (actor.ObjectType == RoMapActorObjectType.Player) {
                return;   // players stay in the world, lying dead until ZC_RESURRECTION (RoOnlineWorldState.ApplyVanish)
            }

            EnsureActorPresenter().PlayDeathThenRelease(actor);
        }

        private void PresentActorTeleportsBeforeVanish(RoMapEntryDrainResult drain) {
            if (drain == null || drain.ActorVanishes == null || drain.ActorVanishes.Length == 0) {
                return;
            }

            for (int i = 0; i < drain.ActorVanishes.Length; i++) {
                PresentActorTeleportBeforeVanish(drain.ActorVanishes[i]);
            }
        }

        private void PresentActorTeleportBeforeVanish(RoMapEntryDrainEvent drainEvent) {
            if (drainEvent.Kind == RoMapEntryDrainEventKind.ActorVanish) {
                PresentActorTeleportBeforeVanish(drainEvent.ActorVanish);
            }
        }

        /// <summary>A logged-out or teleported actor leaves EF 314 / 304 where it stood (vanish handler FUN_00c5adc0).</summary>
        private void PresentActorTeleportBeforeVanish(RoZcNotifyVanish vanish) {
            if (vanish.Gid == 0 || !worldState.TryGetActor(vanish.Gid, out RoOnlineActorSnapshot actor)) {
                return;
            }

            int effectId = Ragnarok.Combat.RoOriginalCombatRules.VanishGroundEffect(vanish.Type, actor.EffectState, out _);
            if (effectId != 0) {
                SpawnActorVanishGroundEffect(actor, effectId);
            }
        }

        private void SyncLocalPlayerAppearanceFromWorldState() {
            if (OwnActorId == 0 || !worldState.TryGetActor(OwnActorId, out RoOnlineActorSnapshot actor)) {
                return;
            }

            RoRuntimeMapController mapController = ResolveRuntimeMapController(forceLookup: true);
            if (mapController == null) {
                return;
            }

            mapController.TryApplyLocalPlayerAppearance(actor);
            mapController.TryApplyLocalPlayerMotion(actor);
        }

        /// <summary>
        /// Diagnostics (skill sweep "inject" mode): presents a synthetic map-server event exactly like a drained one, for
        /// skills the lab server cannot be made to use (combos, menus, homunculus, class-gated links ...).
        /// </summary>
        public void InjectFeedbackEventForDiagnostics(RoMapEntryDrainEvent drainEvent) {
            if (drainEvent != null && ShouldProcessFeedbackEvent(drainEvent)) {
                ProcessFeedbackEvent(drainEvent);
            }
        }

        private void ProcessFeedbackEvent(RoMapEntryDrainEvent drainEvent) {
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.MapProperty:
                    RoPvpState.ApplyMapProperty(drainEvent.MapProperty.PacketId, drainEvent.MapProperty.Type, drainEvent.MapProperty.Flags);
                    return;
                case RoMapEntryDrainEventKind.PvpRanking:
                    PresentPvpRanking(drainEvent.Ranking);
                    return;
                case RoMapEntryDrainEventKind.BattlefieldScore:
                    RoPvpState.ApplyBattlefieldScore(drainEvent.BattlefieldScore.Lion, drainEvent.BattlefieldScore.Eagle);
                    return;
                case RoMapEntryDrainEventKind.ParameterChange:
                    // ZC_PAR_CHANGE base level (handler 0xc5bfd0, 0xc5c3dc): levelup.wav at the player on every update,
                    // whether or not the level went up (reference packages/sound §4.1)
                    RoAudioEvents.PlayEffectRelative("levelup.wav", 0f, 0f, 0f, RoAudioSystem.DefaultMaxDistance, RoAudioSystem.DefaultMinDistance);
                    return;
                case RoMapEntryDrainEventKind.Emotion:
                    SpawnEmotionFeedback(drainEvent.Emotion);
                    break;
                case RoMapEntryDrainEventKind.ActorAction:
                    RefreshSelectedTargetLockLease(drainEvent.ActorAction);
                    SpawnActorActionFeedback(drainEvent.ActorAction);
                    break;
                case RoMapEntryDrainEventKind.AttackFailureForDistance:
                    break;
                case RoMapEntryDrainEventKind.ActionFailure:
                    break;
                case RoMapEntryDrainEventKind.PlayerChat:
                    ShowPlayerChatBubbleFeedback(drainEvent.PlayerChat);
                    break;
                case RoMapEntryDrainEventKind.NpcChat:
                    ShowNpcChatBubbleFeedback(drainEvent.NpcChat);
                    break;
                case RoMapEntryDrainEventKind.PartyChat:
                    ShowPartyChatBubbleFeedback(drainEvent.PartyChat);
                    break;
                case RoMapEntryDrainEventKind.BattlefieldChat:
                    // 0xc43e30: the speaker's balloon (msg 7), as chat 0x8d
                    TryShowClassicBalloon(drainEvent.BattlefieldChat.AccountId, drainEvent.BattlefieldChat.Message, null);
                    break;
                case RoMapEntryDrainEventKind.StatusChange:
                    HandleEfstChange(drainEvent.StatusChange);
                    SpawnStatusFeedback(drainEvent.StatusChange);
                    break;
                case RoMapEntryDrainEventKind.NotifyEffectById:
                    if (drainEvent.NotifyEffectById.ActorId != 0) {
                        EnsureStatusPresenter().HandleNotifyEffectId(
                            drainEvent.NotifyEffectById.ActorId, drainEvent.NotifyEffectById.EffectId, this);
                    }

                    break;
                case RoMapEntryDrainEventKind.SkillUseAck:
                    break;
                case RoMapEntryDrainEventKind.SkillCast:
                    TryShowCastNameForSkill(drainEvent.SkillCast);
                    TryShowCastBarForSkill(drainEvent.SkillCast);
                    PresentSkill(RoSkillEvent.FromCast(drainEvent.SkillCast));
                    break;
                case RoMapEntryDrainEventKind.SkillDamage:
                    QueueSkillHits(RoSkillEvent.FromDamage(drainEvent.SkillDamage), drainEvent.SkillDamage.TargetDelay);
                    TriggerFalconSkillDash(
                        drainEvent.SkillDamage.SkillId,
                        drainEvent.SkillDamage.SourceId,
                        drainEvent.SkillDamage.TargetId);
                    PresentSkill(RoSkillEvent.FromDamage(drainEvent.SkillDamage));
                    EnsureActorPresenter().TryHideCastBar(drainEvent.SkillDamage.SourceId);
                    break;
                case RoMapEntryDrainEventKind.SkillResult:
                    TryShowUseSkillName(drainEvent.SkillResult);
                    TriggerFalconSkillDash(
                        drainEvent.SkillResult.SkillId,
                        drainEvent.SkillResult.SourceId,
                        drainEvent.SkillResult.TargetId);
                    PresentSkill(RoSkillEvent.FromResult(drainEvent.SkillResult));
                    break;
                case RoMapEntryDrainEventKind.NotifyEffect:
                    // ZC_NOTIFY_EFFECT carries a type (0 base level up → EF 371, 1 job level up → 158, 3 refine ok → 154 ...)
                    EnsureStatusPresenter().HandleNotifyEffectType(
                        drainEvent.NotifyEffect.ActorId, drainEvent.NotifyEffect.EffectId, this);
                    break;
                case RoMapEntryDrainEventKind.NpcShowScript:
                    ShowScriptFeedback(drainEvent.ShowScript);
                    break;
                case RoMapEntryDrainEventKind.ActorVanish:
                    Ragnarok.UI.RoClassicChatBalloons.Clear(drainEvent.ActorVanish.Gid);
                    EnsureActorPresenter().TryClearCastOverlay(drainEvent.ActorVanish.Gid);
                    if (IsOwnActorDeathVanish(drainEvent.ActorVanish)) {
                        SpawnActorDeathEffect(drainEvent.ActorVanish.Gid);
                    }

                    break;
                case RoMapEntryDrainEventKind.SkillPosition:
                    QueueSkillHits(RoSkillEvent.FromPosition(drainEvent.SkillPosition), drainEvent.SkillPosition.TargetDelay);
                    TriggerFalconSkillDash(
                        drainEvent.SkillPosition.SkillId,
                        drainEvent.SkillPosition.SourceId,
                        drainEvent.SkillPosition.TargetId,
                        hasCellTarget: true,
                        drainEvent.SkillPosition.X,
                        drainEvent.SkillPosition.Y);
                    PresentSkill(RoSkillEvent.FromPosition(drainEvent.SkillPosition));
                    EnsureActorPresenter().TryHideCastBar(drainEvent.SkillPosition.SourceId);
                    break;
                case RoMapEntryDrainEventKind.GroundSkill:
                    PresentSkill(RoSkillEvent.FromGround(drainEvent.GroundSkill));
                    break;
                case RoMapEntryDrainEventKind.SkillEntry:
                    PresentSkill(RoSkillEvent.FromEntry(drainEvent.SkillEntry));
                    break;
                case RoMapEntryDrainEventKind.SkillDisappear:
                    PresentSkill(RoSkillEvent.FromDisappear(drainEvent.SkillDisappear));
                    break;
                case RoMapEntryDrainEventKind.CastCancel:
                    // ZC_DISPEL (inline case @0x00c33677): cast bar, begin-cast effect and lock-on go (actor msgs
                    // 0x53 / 0x54 / 0x57); the ground-target circle stays until its own end
                    HideCastBar(drainEvent.CastCancel.ActorId);
                    skillPresenter?.HandleCastCancel(drainEvent.CastCancel.ActorId, this);
                    break;
                case RoMapEntryDrainEventKind.ActorSpriteChange:
                    // a skill unit's look change: a trap that fires (Hercules clif->changetraplook, LOOK_BASE)
                    if (drainEvent.ActorSpriteChange.TypeRaw == 0) {
                        skillPresenter?.HandleUnitLookChange(drainEvent.ActorSpriteChange.Gid, drainEvent.ActorSpriteChange.Value, this);
                    }

                    break;
            }
        }

        private void ShowScriptFeedback(RoZcShowScript showScript) {
            if (showScript.EntityId == 0 || string.IsNullOrWhiteSpace(showScript.Message)) {
                return;
            }

            TryShowClassicBalloon(showScript.EntityId, showScript.Message, null);
        }

        private void ShowPlayerChatBubbleFeedback(RoZcNotifyPlayerChat chat) {
            uint sourceActorId = chat.SourceActorId != 0 ? chat.SourceActorId : OwnActorId;
            // 0x8d / 0x8e: no balloon while the chat-room window exists (the text goes to the room only)
            if (Ragnarok.UI.RoClassicChatRoom.RoomOpen) {
                return;
            }

            TryShowClassicBalloon(sourceActorId, chat.Message, null);
        }

        /// <summary>0x0109: the speaker's balloon (the own AID is the own player), shown even in a chat room.</summary>
        private void ShowPartyChatBubbleFeedback(RoZcPartyChat chat) {
            TryShowClassicBalloon(chat.SourceActorId != 0 ? chat.SourceActorId : OwnActorId, chat.Message, null);
        }

        private void ShowNpcChatBubbleFeedback(RoZcNpcChat chat) {
            // 0x02c1 colours the balloon (COLORREF 0x00BBGGRR); the colour stays on it for later plain messages
            var color = new Color32((byte) chat.ColorBgr, (byte) (chat.ColorBgr >> 8), (byte) (chat.ColorBgr >> 16), 255);
            TryShowClassicBalloon(chat.SourceId, chat.Message, color);
        }

        /// <summary>
        /// The classic UI's talk balloon (RoClassicChatBalloons): the server's text verbatim over the actor's feet.
        /// </summary>
        private void TryShowClassicBalloon(uint actorId, string message, Color32? color) {
            if (actorId != 0 && !string.IsNullOrEmpty(message) && TryResolveActorFeedbackAnchor(actorId, out Transform followTarget, out Vector3 worldPosition)) {
                Ragnarok.UI.RoClassicChatBalloons.Show(actorId, followTarget, worldPosition, message, color);
            }
        }

        private bool ShouldProcessFeedbackEvent(RoMapEntryDrainEvent drainEvent) {
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.MapProperty:
                case RoMapEntryDrainEventKind.PvpRanking:
                case RoMapEntryDrainEventKind.BattlefieldScore:
                    return true;
                case RoMapEntryDrainEventKind.ParameterChange:
                    return drainEvent.ParameterChange.Property == RoPlayerStatusProperty.BaseLevel;
                case RoMapEntryDrainEventKind.Emotion:
                case RoMapEntryDrainEventKind.ActorAction:
                    return true;
                case RoMapEntryDrainEventKind.ActionFailure:
                    // ZC_ACTION_FAILURE carries client system/item messages in the original client.
                    return false;
                case RoMapEntryDrainEventKind.AttackFailureForDistance:
                case RoMapEntryDrainEventKind.SkillUseAck:
                    return true;
                case RoMapEntryDrainEventKind.PlayerChat:
                    return !string.IsNullOrWhiteSpace(drainEvent.PlayerChat.Message);
                case RoMapEntryDrainEventKind.NpcChat:
                    return drainEvent.NpcChat.SourceId != 0 && !string.IsNullOrWhiteSpace(drainEvent.NpcChat.Message);
                case RoMapEntryDrainEventKind.PartyChat:
                    return !string.IsNullOrWhiteSpace(drainEvent.PartyChat.Message);
                case RoMapEntryDrainEventKind.BattlefieldChat:
                    return !string.IsNullOrWhiteSpace(drainEvent.BattlefieldChat.Message);
                case RoMapEntryDrainEventKind.StatusChange:
                case RoMapEntryDrainEventKind.SkillCast:
                case RoMapEntryDrainEventKind.SkillDamage:
                case RoMapEntryDrainEventKind.SkillResult:
                case RoMapEntryDrainEventKind.NotifyEffect:
                case RoMapEntryDrainEventKind.NotifyEffectById:
                    return true;
                case RoMapEntryDrainEventKind.NpcShowScript:
                    return !string.IsNullOrWhiteSpace(drainEvent.ShowScript.Message);
                case RoMapEntryDrainEventKind.ActorVanish:
                    return true; // the classic balloons and the cast bar of the actor clear
                case RoMapEntryDrainEventKind.SkillPosition:
                case RoMapEntryDrainEventKind.GroundSkill:
                case RoMapEntryDrainEventKind.SkillEntry:
                    return true;
                case RoMapEntryDrainEventKind.SkillDisappear:
                    return skillPresenter != null && skillPresenter.IsTrackingUnit(drainEvent.SkillDisappear.UnitId);
                case RoMapEntryDrainEventKind.CastCancel:
                    return drainEvent.CastCancel.ActorId != 0;
                case RoMapEntryDrainEventKind.ActorSpriteChange:
                    return skillPresenter != null && skillPresenter.IsTrackingUnit(drainEvent.ActorSpriteChange.Gid);
                default:
                    return false;
            }
        }

        private static ProfilerMarker[] BuildFeedbackKindMarkers() {
            Array values = Enum.GetValues(typeof(RoMapEntryDrainEventKind));
            int maxKind = 0;
            for (int i = 0; i < values.Length; i++) {
                maxKind = Math.Max(maxKind, (int) values.GetValue(i));
            }

            var markers = new ProfilerMarker[maxKind + 1];
            for (int i = 0; i < values.Length; i++) {
                var kind = (RoMapEntryDrainEventKind) values.GetValue(i);
                markers[(int) kind] = new ProfilerMarker("World.ApplyOrderedDrain.Feedback." + kind);
            }

            return markers;
        }

        private static ProfilerMarker GetFeedbackKindMarker(RoMapEntryDrainEventKind kind) {
            int index = (int) kind;
            if (index < 0 || index >= ApplyOrderedDrainFeedbackKindMarkers.Length) {
                return ApplyOrderedDrainFeedbackUnknownMarker;
            }

            return ApplyOrderedDrainFeedbackKindMarkers[index];
        }

        private bool IsRemoteEntityOrUnknownActor(uint actorId) {
            if (actorId == 0) {
                return false;
            }

            if (OwnActorId != 0 && actorId == OwnActorId) {
                return false;
            }

            if (!worldState.TryGetActor(actorId, out RoOnlineActorSnapshot actor) &&
                !worldState.TryGetActorByAccountId(actorId, out actor)) {
                return true;
            }

            if (actor.IsSelf) {
                return false;
            }

            return actor.ObjectType != RoMapActorObjectType.Player;
        }

        /// <summary>
        /// ZC_NOTIFY_RANKING (0x19a, handler 0xc58fb0), only on a PvP map. The own rank: the rank 1 clock, then on a change
        /// MsgStr 0x246 (into the top 10) or 0x247 (out of it) in yellow and number_change.wav at the player (also on
        /// the first packet). Every ranked player: EF 159 (EF_TOPRANK) is relaunched while the rank is 10 or better, with
        /// the rank as its msg 0x2c.
        /// </summary>
        private void PresentPvpRanking(in RoZcNotifyRanking ranking) {
            if (!RoPvpState.IsPvpMap) {
                return;
            }

            if (IsOwnActorId(ranking.Aid)) {
                int old = RoPvpState.ApplyOwnRank(ranking.Rank, ranking.Total, RoMonotonicTime.NowMilliseconds);
                if (old != ranking.Rank) {
                    string line = null;
                    if (old > 10 && ranking.Rank <= 10) {
                        line = Ragnarok.UI.RoClassicUiHost.MsgStr(0x246, "Congratulations! %s's rank has gone up to %d.");
                    } else if (old <= 10 && ranking.Rank > 10) {
                        line = Ragnarok.UI.RoClassicUiHost.MsgStr(0x247, "What a pity! %s's rank has gone down to %d.");
                    }

                    if (line != null) {
                        Ragnarok.UI.RoClassicChat.Instance?.AddLine(
                            FormatPrintf(line, ownCharacterName ?? string.Empty, ranking.Rank), "#FFFF00");
                    }

                    RoAudioEvents.PlayEffectRelative("effect\\number_change.wav", 0f, 0f, 0f, RoAudioSystem.DefaultMaxDistance, RoAudioSystem.DefaultMinDistance);
                }
            } else if (!worldState.TryGetActor(ranking.Aid, out _) && !worldState.TryGetActorByAccountId(ranking.Aid, out _)) {
                return;
            }

            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            if (bridge == null) {
                return;
            }

            bridge.RemoveEffectById(ranking.Aid, Ragnarok.Combat.RoOriginalCombatRules.EfTopRank);
            if (ranking.Rank <= 10 && TryResolveActorFeedbackAnchor(ranking.Aid, out Transform anchor, out Vector3 position)) {
                bridge.TrySpawn(Ragnarok.Combat.RoOriginalCombatRules.EfTopRank, position, new RoEffectSpawnOptions {
                    FollowTarget = anchor,
                    TargetActorId = ranking.Aid,
                    SkillLevel = ranking.Rank > 0 ? ranking.Rank : 0,
                });
            }
        }

        /// <summary>The client's sprintf for one %s and one %d (msgstringtable lines).</summary>
        private static string FormatPrintf(string format, string text, int number) {
            int s = format.IndexOf("%s", StringComparison.Ordinal);
            if (s >= 0) format = format.Substring(0, s) + text + format.Substring(s + 2);
            int d = format.IndexOf("%d", StringComparison.Ordinal);
            if (d >= 0) format = format.Substring(0, d) + number + format.Substring(d + 2);
            return format;
        }

        private bool IsOwnActorId(uint actorId) {
            if (actorId == 0) {
                return false;
            }

            if (OwnActorId != 0 && actorId == OwnActorId) {
                return true;
            }

            if ((worldState.TryGetActor(actorId, out RoOnlineActorSnapshot actor) ||
                 worldState.TryGetActorByAccountId(actorId, out actor)) &&
                actor.IsSelf) {
                return true;
            }

            return false;
        }

        bool IRoCombatActorPresentationContext.IsOwnActor(uint actorId) {
            EnsureRuntimeState();
            return IsOwnActorId(actorId);
        }

        void IRoCombatActorPresentationContext.ApplyActorStatusLook(uint actorId, in RoActorStatusLook look) {
            if (IsOwnActorId(actorId)) {
                ResolveRuntimeMapController(forceLookup: true)?.ApplyLocalPlayerStatusLook(look);
            } else if (createActorPresenter) {
                EnsureActorPresenter().RefreshStatusLook(actorId);
            }
        }

        /// <summary>The actor's status render state (tint, visibility, shadow, frame hold), kept by the status presenter.</summary>
        private RoActorStatusLook ResolveStatusLook(uint actorId) =>
            statusPresenter != null ? statusPresenter.ResolveLook(actorId) : RoActorStatusLook.Default;

        private void TriggerFalconSkillDash(
            ushort skillId,
            uint sourceId,
            uint targetId,
            bool hasCellTarget = false,
            int cellX = 0,
            int cellY = 0
        ) {
            if (!RoMapRentalVisualSemantics.IsFalconSkill(skillId) || sourceId == 0) {
                return;
            }

            Vector3 targetWorldPosition = default;
            bool hasTarget = targetId != 0 &&
                TryResolveActorFeedbackAnchor(targetId, out _, out targetWorldPosition);
            if (!hasTarget && hasCellTarget) {
                hasTarget = TryResolveCellFeedbackPosition(cellX, cellY, out targetWorldPosition);
            }

            if (!hasTarget) {
                return;
            }

            if (createActorPresenter) {
                EnsureActorPresenter().TryPlayFalconSkillDash(skillId, sourceId, targetWorldPosition);
            }

            if (IsOwnActorId(sourceId)) {
                ResolveRuntimeMapController(forceLookup: true)?.TryPlayLocalFalconSkillDash(skillId, targetWorldPosition);
            }
        }

        /// <summary>
        /// ZC_EMOTION (handler 0xc2d9c2): the actor (the player object for the own id) gets msg 0xa0 — the classic engine's
        /// emotion balloon. An actor that is not on screen is ignored, as in the client.
        /// </summary>
        private void SpawnEmotionFeedback(RoZcEmotion emotion) {
            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            if (bridge == null || emotion.Gid == 0 ||
                !TryResolveActorFeedbackAnchor(emotion.Gid, out Transform followTarget, out _) || followTarget == null) {
                if (emotion.Gid != 0 && IsOwnActorId(emotion.Gid)) Ragnarok.UI.RoEmotionSender.EmotionDone();   // no balloon for it here
                return;
            }

            bool shown = bridge.ShowEmotion(emotion.Gid, emotion.EmotionType, followTarget);
            // the client's own emotion with no balloon sends mode msg 0x21 at once (actor msg 0xa0, no record)
            if (!shown && IsOwnActorId(emotion.Gid)) Ragnarok.UI.RoEmotionSender.EmotionDone();
        }

        private void SpawnActorActionFeedback(RoZcNotifyAct action) {
            if (!ShouldSpawnDamageFeedback(action)) {
                return;
            }

            ScheduleActorActionFeedback(action);
        }

        private void ScheduleActorActionFeedback(RoZcNotifyAct action) {
            if (RoSkillSweepProbe.Enabled) {
                RoSkillSweepProbe.Record($"packet Act src={action.SourceId} dst={action.TargetId} dmg={action.Damage} dmg2={action.Damage2} " +
                    $"action={action.ActionRaw} hits={action.HitCount}");
            }

            EnsureHitPresenter().OnAttack(action, RoPresentationClock.NowSeconds, this);
        }

        /// <summary>The basic-attack arrow: the original CArrowEffect, flown and drawn by the classic engine.</summary>
        void IRoHitContext.SpawnArrow(uint attackerId, uint targetId, float releaseTicks, int spriteClass) {
            bool launched = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance != null &&
                TryResolveActorFeedbackAnchor(attackerId, out Transform source, out _) &&
                TryResolveActorFeedbackAnchor(targetId, out Transform target, out _) &&
                Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance.LaunchArrow(attackerId, source, targetId, target, releaseTicks, spriteClass);
            if (RoSkillSweepProbe.Enabled) {
                RoSkillSweepProbe.Record($"hit arrow {attackerId} -> {targetId} release {releaseTicks:0.#} class {spriteClass} launched={launched}");
            }
        }

        private void SyncActorHitReactionPresentation(uint actorId) {
            if (actorId == 0) {
                return;
            }

            if (actorId == OwnActorId) {
                SyncLocalPlayerAppearanceFromWorldState();
            }

            if (!createActorPresenter) {
                return;
            }

            actorPresentationSyncIds.Clear();
            actorPresentationSyncIds.Add(actorId);
            EnsureActorPresenter().SyncActorIds(actorPresentationSyncIds);
        }

        private RoHitPresenter EnsureHitPresenter() => hitPresenter ??= new RoHitPresenter { BattleLog = WriteBattleLog };

        /// <summary>
        /// FUN_00bd58c0's battle line for a hit that came due on <paramref name="targetId"/> (still in view: the queue is the
        /// target actor's): the target's job (FUN_00bd0cd0 → FUN_00d02e40) and who is the local player decide here, the
        /// party check and the line are the chat's (RoClassicBattleLog).
        /// </summary>
        private void WriteBattleLog(uint targetId, uint attackerId, string attackerName, int damage) {
            if (!TryGetHitSnapshot(targetId, out RoOnlineActorSnapshot target)) {
                return;
            }

            Ragnarok.UI.RoClassicBattleLog.Write(RoOriginalCombatRules.IsPlayerJob(DisplayJob(target)), IsOwnActorId(targetId), target.Name,
                targetId, IsOwnActorId(attackerId), attackerName, attackerId, damage);
        }

        /// <summary>The local player's active statuses (EFST ids), as the client's session keeps them for its own checks.</summary>
        private readonly HashSet<int> ownStatuses = new HashSet<int>();
        private uint ownStatusesActorId;

        private void TrackOwnStatus(int statusId, bool active) {
            if (ownStatusesActorId != OwnActorId) {
                ownStatuses.Clear();
                ownStatusesActorId = OwnActorId;
            }

            if (active) {
                ownStatuses.Add(statusId);
            } else {
                ownStatuses.Remove(statusId);
            }
        }

        bool IRoHitContext.OwnHasGuardStatus =>
            ownStatusesActorId == OwnActorId &&
            (ownStatuses.Contains(RoOriginalCombatRules.EfstKyrieEleison) || ownStatuses.Contains(RoOriginalCombatRules.EfstParrying));

        void IRoHitContext.ShowGuard(uint actorId) {
            if (RoSkillSweepProbe.Enabled) RoSkillSweepProbe.Record($"hit guard {actorId}");
            RoEffectManager manager = ResolveEffectManager();
            if (manager != null && TryResolveActorFeedbackAnchor(actorId, out Transform followTarget, out Vector3 worldPosition)) {
                manager.TrySpawnEffectId(RoOriginalCombatRules.GuardEffectId, worldPosition, options: new RoEffectSpawnOptions {
                    FollowTarget = followTarget, SkillLevel = 1, TargetActorId = actorId
                });
            }
        }

        /// <summary>ZC_NOTIFY_SKILL / _POSITION: the skill's hits (numbers, hit sounds, flinches) on the hit timeline, from
        /// the receipt time; the skill presenter launches their effects on the same plan.</summary>
        private void QueueSkillHits(in RoSkillEvent skill, int attackedMT) {
            bool fromUnit = EnsureSkillPresenter().IsUnitSource(skill.SourceActorId, skill.SkillId, this);
            EnsureHitPresenter().OnSkillDamage(in skill, attackedMT, fromUnit, RoPresentationClock.NowSeconds, this);
            QueueMultiHitSwings(in skill);
        }

        /// <summary>
        /// /swing: a listed multi-hit skill's caster starts one more swing before each further hit (the hits come at attackMT
        /// plus the skill's extra delay, then every 200 ms, Pierce 280); the first swing is the attack motion the world state
        /// gave it for the packet.
        /// </summary>
        private void QueueMultiHitSwings(in RoSkillEvent skill) {
            if (!Ragnarok.UI.RoClassicCommandFlags.MultiHitSwing ||
                !RoOriginalCombatRules.UsesClassicMultiHitSwing(skill.SkillId, skill.ActionRaw, skill.HitCount)) {
                return;
            }

            int step = skill.SkillId == RoOriginalCombatRules.PierceSkillId ? 280 : RoOriginalCombatRules.MultiHitMs;
            int firstHit = (int) Math.Min(int.MaxValue, skill.DelayMilliseconds) + RoOriginalCombatRules.SkillFirstHitExtraMs(skill.SkillId);
            float now = RoPresentationClock.NowSeconds;
            for (int k = 1; k < skill.HitCount; k++) {
                float due = now + (firstHit + (k - 1) * step) / 1000f;
                multiHitSwings.Add(due, new KeyValuePair<uint, int>(skill.SourceActorId, step));
            }
        }

        private void ApplyDueMultiHitSwings(float now) {
            if (multiHitSwings.Count == 0) {
                return;
            }

            multiHitSwingSyncIds.Clear();
            bool own = false;
            while (multiHitSwings.TryTakeDue(now, out KeyValuePair<uint, int> swing)) {
                if (!worldState.ReplayMultiHitSwing(swing.Key, swing.Value)) {
                    continue;
                }

                if (IsOwnActorId(swing.Key)) {
                    own = true;
                } else if (!multiHitSwingSyncIds.Contains(swing.Key)) {
                    multiHitSwingSyncIds.Add(swing.Key);
                }
            }

            if (own) {
                SyncLocalPlayerAppearanceFromWorldState();
            }

            if (multiHitSwingSyncIds.Count > 0 && createActorPresenter) {
                EnsureActorPresenter().SyncActorIds(multiHitSwingSyncIds);
            }
        }

        int IRoHitContext.OwnJob => TryGetHitSnapshot(OwnActorId, out RoOnlineActorSnapshot own) ? DisplayJob(own) : -1;

        private bool TryGetHitSnapshot(uint actorId, out RoOnlineActorSnapshot actor) {
            actor = default;
            return actorId != 0 && (worldState.TryGetActor(actorId, out actor) || worldState.TryGetActorByAccountId(actorId, out actor));
        }

        private static int DisplayJob(in RoOnlineActorSnapshot actor) => actor.VisualJob != 0 ? actor.VisualJob : actor.Job;

        bool IRoHitContext.TryGetHitActor(uint actorId, out RoHitActor actor) {
            if (!TryGetHitSnapshot(actorId, out RoOnlineActorSnapshot snapshot)) {
                actor = default;
                return false;
            }

            actor = new RoHitActor {
                Job = DisplayJob(snapshot),
                Sex = snapshot.Sex,
                WeaponClass = (int) RoMapWeaponActionSemantics.ResolveWeaponType(snapshot.Weapon, realType: true),
                IsPlayer = snapshot.ObjectType == RoMapActorObjectType.Player,
                State = ResolveHitState(snapshot),
                PlayingDead = snapshot.IsPlayingDead,
                Name = snapshot.Name
            };
            return true;
        }

        /// <summary>The client state the flinch rule reads: dead, hurt, attack state 2 (motion 2, ACT action 10, while it
        /// plays), walking or idle.</summary>
        private int ResolveHitState(in RoOnlineActorSnapshot actor) {
            if (actor.Motion == RoOnlineActorMotion.Dead || actor.SpawnState == RoMapActorSpawnState.Dead) {
                return RoOriginalCombatRules.StateDead;
            }

            bool playing = !worldState.HasServerTime ||
                unchecked((int) (worldState.LatestServerTimeMilliseconds - actor.ActionStartTime)) < actor.ActionDurationMilliseconds;
            switch (actor.Motion) {
                case RoOnlineActorMotion.Hit:
                    return playing ? RoOriginalCombatRules.StateHurt : RoOriginalCombatRules.StateIdle;
                case RoOnlineActorMotion.Attack2:
                    return playing ? RoOriginalCombatRules.StateAttack : RoOriginalCombatRules.StateIdle;
                case RoOnlineActorMotion.Moving:
                    return RoOriginalCombatRules.StateWalk;
                default:
                    return RoOriginalCombatRules.StateIdle;
            }
        }

        ROIO.Models.FileTypes.ACT IRoHitContext.GetBodyAct(uint actorId) {
            if (actorId != 0 && actorId == OwnActorId) {
                return ResolveRuntimeMapController()?.LocalPlayerBodyAct;
            }

            return actorPresenter != null && TryGetHitSnapshot(actorId, out RoOnlineActorSnapshot actor)
                ? actorPresenter.GetActorBodyAct(actor.ServerId)
                : null;
        }

        void IRoHitContext.PlayHitSound(uint targetId, string wav) {
            if (RoSkillSweepProbe.Enabled) RoSkillSweepProbe.Record($"hit sound {targetId} {wav}");
            if (TryResolveActorFeedbackAnchor(targetId, out _, out Vector3 position)) {
                RoAudioEvents.PlayEffect(wav, position);
            }
        }

        void IRoHitContext.LaunchHitEffects(uint targetId, uint attackerId, bool critical, int effect2) {
            if (RoSkillSweepProbe.Enabled) RoSkillSweepProbe.Record($"hit effects {targetId}{(critical ? " EF 1" : string.Empty)} EF {effect2}");
            if (!TryResolveActorFeedbackAnchor(targetId, out Transform followTarget, out Vector3 worldPosition)) {
                return;
            }

            RoEffectManager manager = ResolveEffectManager();
            if (manager == null) {
                return;
            }

            var options = new RoEffectSpawnOptions {
                FollowTarget = followTarget,
                SkillLevel = 1,
                SourceActorId = attackerId,
                TargetActorId = targetId
            };
            RoCombatEffectOptions.ApplySource(ref options, this, attackerId);
            if (critical) {
                manager.TrySpawnEffectId(CriticalHitEffectId, worldPosition, options: options);
            }

            if (effect2 >= 0) {
                manager.TrySpawnEffectId(effect2, worldPosition, options: options);
            }
        }

        /// <summary>Numbers and messages are the original CMsgEffect objects, drawn by the classic engine.</summary>
        void IRoHitContext.ShowNumber(uint actorId, in RoOriginalCombatRules.HitNumber number) {
            if (RoSkillSweepProbe.Enabled) RoSkillSweepProbe.Record($"hit number {actorId} 0x{number.Type:x} {number.Value} {number.Argb:X8}{(number.CriticalBurst ? " burst" : number.ComboBurst ? " blueburst" : string.Empty)}");
            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            if (bridge == null || !TryResolveActorFeedbackAnchor(actorId, out Transform anchor, out _)) {
                return;
            }

            if (number.CriticalBurst) {
                bridge.ShowCriticalBurst(actorId, anchor);   // msg 0x5c before the 0x10 number (0xbda3c4)
            } else if (number.ComboBurst) {
                bridge.ShowComboBurst(actorId, anchor);      // msg 0x83 before a kick's combo total
            }

            bridge.ShowNumber(actorId, anchor, number.Value, number.Argb, number.Type);
        }

        void IRoHitContext.ShowMiss(uint actorId, uint argb) {
            if (RoSkillSweepProbe.Enabled) RoSkillSweepProbe.Record($"hit miss {actorId} {argb:X8}");
            if (Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance != null && TryResolveActorFeedbackAnchor(actorId, out Transform anchor, out _)) {
                Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance.ShowMiss(actorId, anchor, argb);
            }
        }

        void IRoHitContext.ShowLucky(uint actorId) {
            if (RoSkillSweepProbe.Enabled) RoSkillSweepProbe.Record($"hit lucky {actorId}");
            if (Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance != null && TryResolveActorFeedbackAnchor(actorId, out Transform anchor, out _)) {
                Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance.ShowLucky(actorId, anchor);
            }
        }

        void IRoHitContext.ApplyFlinch(uint targetId, float lengthMs) {
            if (RoSkillSweepProbe.Enabled) RoSkillSweepProbe.Record($"hit flinch {targetId} {lengthMs:0} ms");
            if (worldState.ApplyHurt(targetId, Mathf.Max(1, Mathf.RoundToInt(lengthMs)))) {
                SyncActorHitReactionPresentation(targetId);
            }
        }

        private void SpawnStatusFeedback(RoZcMsgStateChange status) {
            if (!showStatusChangeDebugText ||
                status.Aid == 0 ||
                status.StatusId == 0 ||
                !TryResolveActorFeedbackAnchor(status.Aid, out Transform followTarget, out Vector3 worldPosition)) {
                return;
            }

            RoEffectManager manager = ResolveEffectManager();
            if (manager == null) {
                return;
            }

            string label = status.Active ? $"SC {status.StatusId}" : $"SC {status.StatusId} off";
            manager.SpawnStatusTextOverlay(label, worldPosition, options: new RoEffectSpawnOptions {
                FollowTarget = followTarget
            });
        }

        private const ushort TfHidingSkillId = 51;
        private const ushort AsCloakingSkillId = 135;

        private void TryShowCastNameForSkill(RoZcUseSkillAck skill) {
            if (!ShouldShowSkillCastName(skill.SkillId, skill.SourceId) ||
                !TryResolveActorFeedbackAnchor(skill.SourceId, out Transform sourceFollow, out _) ||
                sourceFollow == null) {
                return;
            }

            // CGameMode msg 0x56 (@0x00c1438d): the caster's talk balloon with the skill name
            TryShowClassicBalloon(skill.SourceId, ResolveSkillDisplayName(skill.SkillId) + " !!", null);
        }

        /// <summary>
        /// ZC_USE_SKILL (FUN_00c9aa00): the caster shouts the skill name (CGameMode msg 0x56) before the result check —
        /// a monster too (FUN_00d1e240, after its skill act), anyone else except for skill 9. A monster skill the client's
        /// table does not name (NPC_EMOTION) shouts a bare "!!".
        /// </summary>
        private void TryShowUseSkillName(RoZcUseSkillResult skill) {
            if (skill.SkillId == 0 || skill.SourceId == 0) {
                return;
            }

            if (skill.SkillId == 9 && !IsClientMonster(skill.SourceId)) {
                return;
            }

            TryShowClassicBalloon(skill.SourceId, ResolveSkillShoutName(skill.SkillId) + " !!", null);
        }

        /// <summary>FUN_00d1e240: the actor's job is a monster's (1001..3998 or 20021..31999).</summary>
        private bool IsClientMonster(uint actorId) {
            if (!worldState.TryGetActor(actorId, out RoOnlineActorSnapshot actor)) {
                return false;
            }

            int job = actor.Job;
            return (job > 1000 && job < 3999) || (uint) (job - 20021) < 11979u;
        }

        /// <summary>FUN_006eaba0: the client's skill name, empty for a skill its table does not have.</summary>
        private string ResolveSkillShoutName(ushort skillId) {
            string name = SkillNameResolver?.Invoke(skillId);
            if (!string.IsNullOrEmpty(name)) {
                return name;
            }

            return ResolveSkillMetadata().Skills.TryGetValue(skillId, out RoSkillMetadata meta) ? meta.DisplayName ?? string.Empty : string.Empty;
        }

        private void TryShowCastBarForSkill(RoZcUseSkillAck skill) {
            if (skill.DelayTime == 0 || skill.SourceId == 0) {
                return;
            }

            // the client's UIRechargeGage (actor msg 0x52): every caster with a cast time, except skills 57 and 62
            if (EnsureSkillPresenter().Resolve(skill.SkillId).HideCastBar) {
                return;
            }

            if (!TryResolveActorFeedbackAnchor(skill.SourceId, out Transform sourceFollow, out _) || sourceFollow == null) {
                return;
            }

            EnsureActorCastOverlay(sourceFollow).ShowCast(skill.DelayTime);
        }

        private bool ShouldShowSkillCastName(ushort skillId, uint sourceActorId) {
            if (skillId == 0 ||
                skillId == TfHidingSkillId ||
                skillId == AsCloakingSkillId ||
                sourceActorId == 0) {
                return false;
            }

            return IsPlayerLikeSkillNameSource(sourceActorId);
        }

        private bool IsPlayerLikeSkillNameSource(uint actorId) {
            if (!worldState.TryGetActor(actorId, out RoOnlineActorSnapshot actor) &&
                !worldState.TryGetActorByAccountId(actorId, out actor)) {
                return false;
            }

            if (actor.IsSelf) {
                return true;
            }

            return actor.ObjectType == RoMapActorObjectType.Player ||
                actor.ObjectType == RoMapActorObjectType.Disguised ||
                actor.ObjectType == RoMapActorObjectType.Pet ||
                actor.ObjectType == RoMapActorObjectType.Homunculus ||
                actor.ObjectType == RoMapActorObjectType.Mercenary ||
                actor.ObjectType == RoMapActorObjectType.Elemental;
        }

        private void HideCastBar(uint actorId) {
            EnsureActorPresenter().TryHideCastBar(actorId);
            if (TryResolveActorFeedbackAnchor(actorId, out Transform follow, out _) && follow != null &&
                follow.TryGetComponent(out RoActorCastBarOverlay overlay)) {
                overlay.HideCast();
            }
        }

        private static RoActorCastBarOverlay EnsureActorCastOverlay(Transform sourceFollow) {
            RoActorCastBarOverlay overlay = sourceFollow.FindComponent<RoActorCastBarOverlay>();
            if (overlay == null) {
                overlay = sourceFollow.gameObject.AddComponent<RoActorCastBarOverlay>();
            }

            return overlay;
        }

        private string ResolveSkillDisplayName(ushort skillId) {
            Func<ushort, string> resolver = SkillNameResolver;
            if (resolver != null) {
                string name = resolver(skillId);
                if (!string.IsNullOrEmpty(name)) {
                    return name;
                }
            }

            RoSkillMetadataTable skillTable = ResolveSkillMetadata();
            if (skillTable.Skills.TryGetValue(skillId, out RoSkillMetadata meta)) {
                return meta.DisplayName;
            }

            return "Unknown Skill";
        }

        private RoSkillMetadataTable ResolveSkillMetadata() {
            RoRuntimeMapController map = ResolveRuntimeMapController();
            RoAssetDatabase assets = map != null && map.Content != null ? map.Content.Assets : null;
            return assets != null && assets.IsInitialized
                ? assets.GameData?.Skills ?? RoSkillMetadataTable.Empty
                : RoSkillMetadataTable.Empty;
        }

        /// <summary>
        /// EF_DEVIL: only the local player's death, from CPlayer::SetAction (0xcd71e7) when the state turns dead — the
        /// own vanish type 1 — as a ground effect at the player's position. Other players and teleports get none.
        /// </summary>
        public static bool ShouldSpawnOwnDeathEffect(RoZcNotifyVanish vanish, uint ownActorId) {
            return vanish.Type == RoMapActorVanishType.Died &&
                vanish.Gid != 0 &&
                ownActorId != 0 &&
                vanish.Gid == ownActorId;
        }

        private bool IsOwnActorDeathVanish(RoZcNotifyVanish vanish) {
            return ShouldSpawnOwnDeathEffect(vanish, OwnActorId);
        }

        private void SpawnActorDeathEffect(uint gid) {
            if (gid == 0 || !TryResolveActorFeedbackAnchor(gid, out _, out Vector3 worldPosition)) {
                return;
            }

            Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance?.LaunchGroundEffect(
                Ragnarok.Combat.RoOriginalCombatRules.EfDevil, worldPosition);
        }

        /// <summary>EF 314 / 304 on a ground holder at the vanishing actor's last drawn position (the actor goes at once).</summary>
        private void SpawnActorVanishGroundEffect(RoOnlineActorSnapshot actor, int effectId) {
            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            if (bridge == null) {
                return;
            }

            bool hasWorldPosition = TryResolveLocalPlayerFeedbackAnchor(actor, out _, out Vector3 worldPosition);
            if (!hasWorldPosition && actorPresenter != null && actorPresenter.TryGetPresentedActorTransform(actor.ServerId, out Transform followTarget)) {
                worldPosition = followTarget.position;
                hasWorldPosition = true;
            }

            if (!hasWorldPosition) {
                worldPosition = ResolveSnapshotWorldPosition(actor);
            }

            bridge.LaunchGroundEffect(effectId, worldPosition);
        }

        /// <summary>
        /// A player's NEWENTRY (teleport or warp arrival, login) launches EF_ENTRY2 (344) on it unless it is invisible
        /// (0xccb518); standing and moving entries show nothing. Launched once the presenter has the actor, which the
        /// effect then follows.
        /// </summary>
        private void PresentArrivals(RoMapEntryDrainResult drain) {
            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            if (drain == null || bridge == null || !createActorPresenter || actorPresenter == null) {
                return;
            }

            RoMapEntryDrainEvent[] ordered = drain.OrderedEvents;
            if (ordered != null && ordered.Length > 0) {
                for (int i = 0; i < ordered.Length; i++) {
                    if (ordered[i].Kind == RoMapEntryDrainEventKind.ActorEntry) {
                        PresentArrival(bridge, ordered[i].ActorEntry);
                    }
                }

                return;
            }

            RoZcNotifyActorEntry[] entries = drain.ActorEntries;
            for (int i = 0; entries != null && i < entries.Length; i++) {
                PresentArrival(bridge, entries[i]);
            }
        }

        private void PresentArrival(Ragnarok.Effects.Classic.RoClassicEffectBridge bridge, in RoZcNotifyActorEntry entry) {
            if (!Ragnarok.Combat.RoOriginalCombatRules.ShowsEntryEffect(entry.Kind, entry.ObjectType, entry.EffectState) ||
                IsOwnActorId(entry.Aid) ||
                !actorPresenter.TryGetPresentedActorTransform(entry.Aid, out Transform anchor)) {
                return;
            }

            bridge.TrySpawn(Ragnarok.Combat.RoOriginalCombatRules.EfEntry2, anchor.position, new RoEffectSpawnOptions {
                FollowTarget = anchor,
                TargetActorId = entry.Aid,
            });
        }

        private int ResolveActorJob(uint actorId) {
            return actorId != 0 &&
                (worldState.TryGetActor(actorId, out RoOnlineActorSnapshot actor) ||
                 worldState.TryGetActorByAccountId(actorId, out actor))
                ? actor.Job
                : -1;
        }

        private void OnDestroy() {
            HideSelectedTargetLockIndicator();
            ClearSkillVisualPresentation();
            hitPresenter?.Clear();
            deferredDeathVanishes.Clear();
            multiHitSwings.Clear();
            recentFeedbackAnchors?.Clear();

            if (actorPresenter != null) {
                actorPresenter.ClearPresentedActors();
            }

            ClearDebugMarkers();
        }

        private RoCombatPresentationOptions EnsureCombatPresentationOptions() {
            combatPresentationOptions ??= new RoCombatPresentationOptions();
            SyncCombatTraceDiagnosticsFromPresentationOptions(logState: false);
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            combatPresentationOptions.EnableCombatTrace = false;
            combatPresentationOptions.EnableCombatTraceConsoleLog = false;
            combatTrace.Enabled = false;
#else
            combatTrace.Enabled = combatPresentationOptions.EnableCombatTrace && RoRuntimeDiagnostics.CombatTraceEnabled;
#endif
            return combatPresentationOptions;
        }

        private void SyncCombatTraceDiagnosticsFromPresentationOptions(bool logState) {
            combatPresentationOptions ??= new RoCombatPresentationOptions();
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            combatPresentationOptions.EnableCombatTrace = false;
            combatPresentationOptions.EnableCombatTraceConsoleLog = false;
            combatTrace.Enabled = false;
#else
            if (combatPresentationOptions.EnableCombatTrace) {
                RoRuntimeDiagnostics.Apply(new RoRuntimeDiagnosticsOptions {
                    EnablePacketTrace = RoRuntimeDiagnostics.PacketTraceEnabled,
                    EnablePacketLogWindow = RoRuntimeDiagnostics.PacketLogWindowEnabled,
                    EnableRuntimePerfLog = RoRuntimeDiagnostics.RuntimePerfLogEnabled,
                    EnableCombatTrace = true,
                    EnableCombatTraceConsoleLog = combatPresentationOptions.EnableCombatTraceConsoleLog,
                    EnableMovementTrace = RoRuntimeDiagnostics.MovementTraceEnabled,
                    EnableMovementTraceConsoleLog = RoRuntimeDiagnostics.MovementTraceConsoleLogEnabled,
                    EnableVerboseNetworkTraceMessages = RoRuntimeDiagnostics.VerboseNetworkTraceMessagesEnabled,
                });
            }

            combatTrace.Enabled = combatPresentationOptions.EnableCombatTrace && RoRuntimeDiagnostics.CombatTraceEnabled;
            if (logState &&
                combatPresentationOptions.EnableCombatTraceConsoleLog &&
                RoRuntimeDiagnostics.CombatTraceConsoleLogEnabled) {
                RoRuntimeDiagnostics.IncrementDebugLogLine();
                Debug.Log($"[RO Combat] world trace options applied trace={combatPresentationOptions.EnableCombatTrace} console={combatPresentationOptions.EnableCombatTraceConsoleLog} globalTrace={RoRuntimeDiagnostics.CombatTraceEnabled} globalConsole={RoRuntimeDiagnostics.CombatTraceConsoleLogEnabled}");
            }
#endif
        }

        private RoSkillPresenter EnsureSkillPresenter() {
            skillPresenter ??= new RoSkillPresenter();
            return skillPresenter;
        }

        private void PresentSkill(in RoSkillEvent skillEvent) {
            if (RoSkillSweepProbe.Enabled) {
                RoSkillSweepProbe.Record($"packet {skillEvent.Kind} skill={skillEvent.SkillId} src={skillEvent.SourceActorId} dst={skillEvent.TargetActorId} xy={skillEvent.X},{skillEvent.Y} lv={skillEvent.Level} dmg={skillEvent.Damage} hits={skillEvent.HitCount} delay={skillEvent.DelayMilliseconds} ok={skillEvent.Success} handler=RoSkillPresenter");
            }

            EnsureSkillPresenter().Present(in skillEvent, this);
        }

        private RoStatusPresenter statusPresenter;
        private uint classicLocalPlayerAnchorId;
        private Transform classicLocalPlayerAnchor;
        private int classicLocalPlayerAnchorFrame = -1;
        private readonly List<RoOnlineActorStatusSnapshot> statusSyncSnapshots = new List<RoOnlineActorStatusSnapshot>();
        private int statusSyncRevision = -1;
        private bool statusReapplyPending;

        private RoStatusPresenter EnsureStatusPresenter() {
            if (statusPresenter == null) {
                statusPresenter = new RoStatusPresenter();
                statusPresenter.UnhandledAction += (actorId, action) => {
                    if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"status unhandled {action.Kind} {action.EffectId} aid={actorId}");
                };
            }

            return statusPresenter;
        }

        private RoStatusActorInfo BuildStatusActorInfo(uint actorId) {
            var info = new RoStatusActorInfo { ActorId = actorId, IsLocalPlayer = actorId != 0 && actorId == OwnActorId };
            if (worldState.TryGetActor(actorId, out RoOnlineActorSnapshot actor)) {
                info.IsMonster = actor.ObjectType == RoMapActorObjectType.Monster;
                info.IsPlayerJob = actor.ObjectType == RoMapActorObjectType.Player;
            } else {
                info.IsPlayerJob = info.IsLocalPlayer;
            }

            return info;
        }

        private void HandleEfstChange(RoZcMsgStateChange status) {
            uint actorId = status.Aid == 0 ? OwnActorId : status.Aid;
            if (actorId == 0 || status.StatusId == 0) {
                return;
            }

            if (actorId == OwnActorId) {
                TrackOwnStatus(status.StatusId, status.Active);
            }

            RoStatusActorInfo info = BuildStatusActorInfo(actorId);
            EnsureStatusPresenter().HandleEfst(info, status.StatusId, status.Active, status.Value1, this);
        }

        /// <summary>
        /// Brings every actor's opt1 / opt2 / option / opt3 visuals up to date after a world-state change (entries,
        /// ZC_STATE_CHANGE, ZC_NPC_SHOWEFST_UPDATE, vanishes) and runs the status per-tick relaunches.
        /// </summary>
        private void SyncActorStatuses(float deltaSeconds) {
            RoStatusPresenter handler = EnsureStatusPresenter();
            int revision = worldState.PresentationRevision;
            if (revision != statusSyncRevision) {
                statusSyncRevision = revision;
                worldState.SnapshotActorStatus(statusSyncSnapshots);
                handler.BeginSync();
                StatusSyncMarker.Begin();
                bool sawOwnActor = false;
                foreach (RoOnlineActorStatusSnapshot snapshot in statusSyncSnapshots) {
                    bool local = snapshot.ServerId == OwnActorId && OwnActorId != 0;
                    sawOwnActor |= local;
                    handler.SyncActorState(new RoStatusActorInfo {
                        ActorId = snapshot.ServerId,
                        IsLocalPlayer = local,
                        IsMonster = snapshot.ObjectType == RoMapActorObjectType.Monster,
                        IsPlayerJob = snapshot.ObjectType == RoMapActorObjectType.Player || local,
                        BodyState = snapshot.BodyState,
                        HealthState = snapshot.HealthState,
                        Option = snapshot.EffectState,
                        Opt3 = snapshot.Opt3,
                    }, this);
                }

                StatusSyncMarker.End();
                using (StatusEndMarker.Auto()) {
                    handler.EndSync(this);
                }
                if (statusReapplyPending && sawOwnActor) {
                    statusReapplyPending = false;
                    handler.ReapplyLocalPlayer(OwnActorId, this);
                }
            }

            using (StatusTickMarker.Auto()) {
                handler.Tick(deltaSeconds, this);
            }
        }

        private void ClearSkillVisualPresentation() {
            skillPresenter?.Clear();
        }

        private RoActorPresentationSyncResult SyncActorPresenter(bool force = true) {
            if (!createActorPresenter) {
                return RoActorPresentationSyncResult.Empty;
            }

            RoOnlineActorPresenter presenter = EnsureActorPresenter();
            return presenter.SyncNow(force);
        }

        private void SyncServerClockFromState(float localSeconds) {
            EnsureRuntimeState();
            if (appliedServerTimeRevision == worldState.ServerTimeRevision) {
                return;
            }

            appliedServerTimeRevision = worldState.ServerTimeRevision;
            if (worldState.HasServerTime) {
                uint rawSampleMilliseconds = worldState.LatestServerTimeMilliseconds;
                uint predictedMilliseconds = hasServerClockAnchor
                    ? EstimateServerTimeFromAnchor(localSeconds)
                    : rawSampleMilliseconds;
                int innovationMilliseconds = hasServerClockAnchor
                    ? unchecked((int) (rawSampleMilliseconds - predictedMilliseconds))
                    : 0;
                uint slewedMilliseconds = rawSampleMilliseconds;
                string mode = "InitialAnchor";
                if (hasServerClockAnchor) {
                    if (Math.Abs(innovationMilliseconds) > ServerClockAnchorHardResetInnovationMilliseconds) {
                        mode = "HardReset";
                    } else {
                        int correctionMilliseconds = Math.Max(
                            -ServerClockAnchorSlewLimitMilliseconds,
                            Math.Min(ServerClockAnchorSlewLimitMilliseconds, innovationMilliseconds));
                        slewedMilliseconds = unchecked(predictedMilliseconds + (uint) correctionMilliseconds);
                        mode = "Slew";
                    }
                }

                slewedMilliseconds = ClampMonotonicServerEstimate(slewedMilliseconds);
                hasServerClockAnchor = true;
                serverClockAnchorMilliseconds = slewedMilliseconds;
                serverClockAnchorLocalSeconds = localSeconds;
                RecordServerClockTraceIfAvailable(
                    $"clock state raw={rawSampleMilliseconds} predicted={predictedMilliseconds} innovationMs={innovationMilliseconds} slewed={slewedMilliseconds} mode={mode}");
            } else {
                ClearServerClockAnchor();
            }
        }

        private uint EstimateServerTimeFromAnchor(float localSeconds) {
            if (!hasServerClockAnchor) {
                return 0;
            }

            double deltaSeconds = Math.Max(0d, localSeconds - serverClockAnchorLocalSeconds);
            uint deltaMilliseconds = deltaSeconds >= uint.MaxValue / 1000d
                ? uint.MaxValue
                : (uint) Math.Round(deltaSeconds * 1000d);
            return unchecked(serverClockAnchorMilliseconds + deltaMilliseconds);
        }

        private void ClearServerClockAnchor() {
            hasServerClockAnchor = false;
            serverClockAnchorMilliseconds = 0;
            serverClockAnchorLocalSeconds = 0f;
            hasLastEstimatedServerClock = false;
            lastEstimatedServerClockMilliseconds = 0;
        }

        public static bool ShouldShowSelectedTargetLockIndicator(RoOnlineTargetSelection selection) {
            return selection.HasTarget &&
                selection.Interaction == RoOnlineTargetInteraction.Attack &&
                IsTargetLockObjectType(selection.ObjectType);
        }

        public static bool IsSelectedTargetLockLeaseActive(
            RoOnlineTargetSelection selection,
            uint leaseTargetId,
            float nowSeconds,
            float expiresAtSeconds
        ) {
            return ShouldShowSelectedTargetLockIndicator(selection) &&
                leaseTargetId != 0 &&
                leaseTargetId == selection.ServerId &&
                nowSeconds <= expiresAtSeconds;
        }

        public static bool ShouldRefreshSelectedTargetLockLease(
            RoOnlineTargetSelection selection,
            RoZcNotifyAct action,
            uint ownActorId
        ) {
            return ownActorId != 0 &&
                action.SourceId == ownActorId &&
                ShouldRefreshSelectedTargetLockLeaseForTarget(selection, action);
        }

        private static bool ShouldRefreshSelectedTargetLockLeaseForTarget(
            RoOnlineTargetSelection selection,
            RoZcNotifyAct action
        ) {
            return selection.HasTarget &&
                action.TargetId != 0 &&
                action.TargetId == selection.ServerId &&
                RoMapActorActionSemantics.IsDamage(action.ActionRaw) &&
                ShouldShowSelectedTargetLockIndicator(selection);
        }

        public static float ResolveSelectedTargetLockCombatLeaseSeconds(RoZcNotifyAct action) {
            float sourceMotionSeconds = Mathf.Max(0f, action.SourceMotionTime) / 1000f;
            return Mathf.Max(
                SelectedTargetLockMinimumCombatLeaseSeconds,
                sourceMotionSeconds + SelectedTargetLockCombatLeasePaddingSeconds);
        }

        private static bool IsTargetLockObjectType(RoMapActorObjectType objectType) {
            return objectType == RoMapActorObjectType.Player ||
                objectType == RoMapActorObjectType.Disguised ||
                objectType == RoMapActorObjectType.Monster ||
                objectType == RoMapActorObjectType.Pet ||
                objectType == RoMapActorObjectType.Homunculus ||
                objectType == RoMapActorObjectType.Mercenary ||
                objectType == RoMapActorObjectType.Elemental;
        }

        private void ArmInitialSelectedTargetLockLease(RoOnlineTargetSelection selection) {
            if (!ShouldShowSelectedTargetLockIndicator(selection)) {
                ClearSelectedTargetLockLease();
                return;
            }

            SetSelectedTargetLockLease(selection.ServerId, SelectedTargetLockInitialLeaseSeconds);
        }

        private void RefreshSelectedTargetLockLease(RoZcNotifyAct action) {
            if (!IsOwnActorId(action.SourceId) ||
                !ShouldRefreshSelectedTargetLockLeaseForTarget(selectedTarget, action)) {
                return;
            }

            SetSelectedTargetLockLease(action.TargetId, ResolveSelectedTargetLockCombatLeaseSeconds(action));
        }

        private void SetSelectedTargetLockLease(uint targetId, float leaseSeconds) {
            if (targetId == 0 || leaseSeconds <= 0f) {
                ClearSelectedTargetLockLease();
                return;
            }

            float expiresAtSeconds = RoPresentationClock.NowSeconds + leaseSeconds;
            if (selectedTargetLockLeaseTargetId != targetId) {
                selectedTargetLockLeaseTargetId = targetId;
                selectedTargetLockLeaseExpiresAtSeconds = expiresAtSeconds;
                return;
            }

            selectedTargetLockLeaseExpiresAtSeconds = Mathf.Max(selectedTargetLockLeaseExpiresAtSeconds, expiresAtSeconds);
        }

        private void ClearSelectedTargetLockLease() {
            selectedTargetLockLeaseTargetId = 0;
            selectedTargetLockLeaseExpiresAtSeconds = 0f;
        }

        private bool HasActiveSelectedTargetLockLease(float nowSeconds) {
            return IsSelectedTargetLockLeaseActive(
                selectedTarget,
                selectedTargetLockLeaseTargetId,
                nowSeconds,
                selectedTargetLockLeaseExpiresAtSeconds);
        }

        private void SyncSelectedTargetLockIndicator() {
            if (!showSelectedTargetLockIndicator ||
                !ShouldShowSelectedTargetLockIndicator(selectedTarget) ||
                !HasActiveSelectedTargetLockLease(RoPresentationClock.NowSeconds) ||
                !TryResolveActorFeedbackAnchor(selectedTarget.ServerId, out Transform followTarget, out Vector3 worldPosition)) {
                HideSelectedTargetLockIndicator();
                return;
            }

            RoAssetDatabase assets = ResolveSelectedTargetLockAssets();
            if (assets == null) {
                HideSelectedTargetLockIndicator();
                return;
            }

            EnsureSelectedTargetLockIndicator().Configure(
                assets,
                followTarget,
                worldPosition,
                ResolveSelectedTargetLockCamera());
        }

        private void HideSelectedTargetLockIndicator() {
            if (selectedTargetLockIndicator != null) {
                selectedTargetLockIndicator.Hide();
            }
        }

        private RoSelectedTargetLockIndicator EnsureSelectedTargetLockIndicator() {
            if (selectedTargetLockIndicator != null) {
                return selectedTargetLockIndicator;
            }

            var indicatorObject = new GameObject("RO Selected Target Lock Indicator");
            indicatorObject.transform.SetParent(transform, false);
            selectedTargetLockIndicator = indicatorObject.AddComponent<RoSelectedTargetLockIndicator>();
            selectedTargetLockIndicator.Hide();
            return selectedTargetLockIndicator;
        }

        private RoAssetDatabase ResolveSelectedTargetLockAssets() {
            RoRuntimeMapController mapController = ResolveRuntimeMapController(forceLookup: true);
            RoAssetDatabase assets = mapController?.Content?.Assets;
            return assets != null && assets.IsInitialized ? assets : null;
        }

        private static UnityEngine.Camera ResolveSelectedTargetLockCamera() {
            return UnityEngine.Camera.main;
        }

        /// <summary>The actor's follow transform (when presented) and world position, as the talk balloons place themselves.</summary>
        public bool TryResolveActorAnchor(uint id, out Transform followTarget, out Vector3 worldPosition) =>
            TryResolveActorFeedbackAnchor(id, out followTarget, out worldPosition);

        private bool TryResolveActorFeedbackAnchor(uint id, out Transform followTarget, out Vector3 worldPosition) {
            RoOnlineActorSnapshot actor;
            bool hasActor = worldState.TryGetActor(id, out actor) || worldState.TryGetActorByAccountId(id, out actor);

            if (hasActor && TryResolveLocalPlayerFeedbackAnchor(actor, out followTarget, out worldPosition)) {
                return true;
            }

            if (hasActor && actorPresenter != null && actorPresenter.TryGetPresentedActorTransform(actor.ServerId, out followTarget)) {
                worldPosition = followTarget.position;
                return true;
            }

            if (hasActor) {
                followTarget = null;
                worldPosition = ResolveSnapshotWorldPosition(actor);
                return true;
            }

            if (TryResolveRecentFeedbackAnchor(id, out worldPosition)) {
                followTarget = null;
                return true;
            }

            followTarget = null;
            worldPosition = default;
            return false;
        }

        private bool TryResolveRecentFeedbackAnchor(uint id, out Vector3 worldPosition) {
            if (id != 0 && recentFeedbackAnchors.TryGetValue(id, out RoRecentFeedbackAnchor anchor)) {
                if (anchor.ExpiresAtSeconds >= RoPresentationClock.NowSeconds) {
                    worldPosition = anchor.WorldPosition;
                    return true;
                }

                recentFeedbackAnchors.Remove(id);
            }

            worldPosition = default;
            return false;
        }

        private bool TryResolveLocalPlayerFeedbackAnchor(RoOnlineActorSnapshot actor, out Transform followTarget, out Vector3 worldPosition) {
            bool isOwnActor = actor.IsSelf ||
                (OwnActorId != 0 && (actor.ServerId == OwnActorId || actor.AccountId == OwnActorId));
            if (!isOwnActor) {
                followTarget = null;
                worldPosition = default;
                return false;
            }

            RoRuntimeMapController mapController = ResolveRuntimeMapController(forceLookup: true);
            Transform localPlayer = mapController != null ? mapController.LocalPlayer : null;
            if (localPlayer == null || !localPlayer.gameObject.activeInHierarchy) {
                followTarget = null;
                worldPosition = default;
                return false;
            }

            followTarget = localPlayer;
            worldPosition = localPlayer.position;
            return true;
        }

        private RoRuntimeMapController ResolveRuntimeMapController(bool forceLookup = false) {
            if (runtimeMapController != null) {
                return runtimeMapController;
            }

            float now = Time.unscaledTime;
            if (!forceLookup && now < nextRuntimeMapControllerLookupTime) {
                return null;
            }

            int frame = Time.frameCount;
            if (forceLookup && lastRuntimeMapControllerLookupFrame == frame) {
                return null;
            }

            lastRuntimeMapControllerLookupFrame = frame;
            runtimeMapController = FindFirstObjectByType<RoRuntimeMapController>();
            nextRuntimeMapControllerLookupTime = runtimeMapController != null
                ? 0f
                : now + PresentationDependencyLookupRetrySeconds;
            return runtimeMapController;
        }

        private Vector3 ResolveSnapshotWorldPosition(RoOnlineActorSnapshot actor) {
            Vector2Int cell = RoOnlineActorPresenter.GetDisplayCell(actor);
            Vector3 position = gridSelector != null && IsGridCellAvailable(cell)
                ? gridSelector.GetCellWorldCenter(cell, 0f)
                : new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);

            if (actor.ObjectType == RoMapActorObjectType.Item) {
                position += new Vector3(actor.ItemCellOffsetX, 0f, actor.ItemCellOffsetY);
            }

            return position;
        }

        private bool TryResolveCellFeedbackPosition(int x, int y, out Vector3 worldPosition) {
            if (x < 0 || y < 0) {
                worldPosition = default;
                return false;
            }

            var cell = new Vector2Int(x, y);
            worldPosition = gridSelector != null && IsGridCellAvailable(cell)
                ? gridSelector.GetCellWorldCenter(cell, 0f)
                : new Vector3(x + 0.5f, 0f, y + 0.5f);
            return true;
        }

        private bool IsGridCellAvailable(Vector2Int cell) {
            return gridSelector != null &&
                gridSelector.Altitude != null &&
                cell.x >= 0 &&
                cell.y >= 0 &&
                cell.x < gridSelector.Altitude.width &&
                cell.y < gridSelector.Altitude.height;
        }

        /// <summary>The receipt clock the hit timeline runs on (real time, like the client's timeGetTime).</summary>
        float IRoCombatPresentationContext.NowSeconds => RoPresentationClock.NowSeconds;

        void IRoCombatPresentationContext.ShowNumber(uint actorId, int value, uint argb, int type) =>
            ((IRoHitContext) this).ShowNumber(actorId, new RoOriginalCombatRules.HitNumber(type, value, argb, false));

        RoCombatPresentationOptions IRoCombatPresentationContext.CombatOptions => EnsureCombatPresentationOptions();

        RoCombatTrace IRoCombatPresentationContext.CombatTrace => combatTrace;

        RoEffectManager IRoCombatPresentationContext.ResolveEffectManager() {
            return ResolveEffectManager();
        }

        bool IRoCombatPresentationContext.TryResolveActorFeedbackAnchor(uint actorId, out Transform followTarget, out Vector3 worldPosition) {
            return TryResolveActorFeedbackAnchor(actorId, out followTarget, out worldPosition);
        }

        bool IRoCombatPresentationContext.TryResolveCellFeedbackPosition(int x, int y, out Vector3 worldPosition) {
            return TryResolveCellFeedbackPosition(x, y, out worldPosition);
        }

        bool IRoAmmunitionProjectileVisualContext.TryResolveEquippedAmmoProjectileSprite(uint actorId, out string spritePath) {
            return TryResolveEquippedAmmoProjectileSprite(actorId, out spritePath);
        }

        private bool TryResolveEquippedAmmoProjectileSprite(uint actorId, out string spritePath) {
            spritePath = null;
            if (!IsOwnActorId(actorId) || ammunitionEquipmentSnapshotProvider == null) {
                return false;
            }

            RoGameplayEquipmentSnapshot equipment = ammunitionEquipmentSnapshotProvider();
            RoItemMetadataTable metadata = ammunitionItemMetadataProvider != null
                ? ammunitionItemMetadataProvider()
                : RoItemMetadataTable.Empty;
            return RoAmmunitionProjectileVisuals.TryResolveEquippedAmmoProjectileSpritePath(
                equipment,
                metadata,
                out spritePath);
        }

        private RoEffectManager ResolveEffectManager(bool forceLookup = true) {
            RoRuntimeMapController mapController = ResolveRuntimeMapController(forceLookup);
            if (mapController != null) {
                if (mapController.Effects != null &&
                    (effectManager == null || !effectManager.HasAssetDatabase)) {
                    effectManager = mapController.Effects;
                    nextEffectManagerLookupTime = 0f;
                }
            }

            if (effectManager != null) {
                effectManager.SetClassicLocalPlayer(OwnActorId);
                if (OwnActorId != 0 && classicLocalPlayerAnchorFrame != Time.frameCount &&
                    (classicLocalPlayerAnchor == null || classicLocalPlayerAnchorId != OwnActorId)) {
                    // the classic engine's player object follows the own actor (new-system effects run within 130
                    // units of it); re-registered when the actor object is replaced (respawn, map change)
                    classicLocalPlayerAnchorFrame = Time.frameCount;
                    if (TryResolveActorFeedbackAnchor(OwnActorId, out Transform ownAnchor, out _) && ownAnchor != null) {
                        effectManager.RegisterClassicActorAnchor(OwnActorId, ownAnchor);
                        classicLocalPlayerAnchor = ownAnchor;
                        classicLocalPlayerAnchorId = OwnActorId;
                    }
                }

                return effectManager;
            }

            float now = Time.unscaledTime;
            if (!forceLookup && now < nextEffectManagerLookupTime) {
                return null;
            }

            int frame = Time.frameCount;
            if (forceLookup && lastEffectManagerLookupFrame == frame) {
                return null;
            }

            lastEffectManagerLookupFrame = frame;
            effectManager = FindFirstObjectByType<RoEffectManager>();
            nextEffectManagerLookupTime = effectManager != null
                ? 0f
                : now + PresentationDependencyLookupRetrySeconds;
            return effectManager;
        }

        private void SyncWarpPortalEffects() {
            using (SyncWarpPortalEffectsMarker.Auto()) {
                Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
                if (bridge == null) {
                    activeWarpPortalEffects.Clear();
                    return;
                }

                warpPortalActorSnapshots.Clear();
                liveWarpPortalIds.Clear();
                staleWarpPortalIds.Clear();
                worldState.SnapshotActors(warpPortalActorSnapshots);
                for (int i = 0; i < warpPortalActorSnapshots.Count; i++) {
                    RoOnlineActorSnapshot actor = warpPortalActorSnapshots[i];
                    if (!IsWarpPortalActor(actor)) {
                        continue;
                    }

                    liveWarpPortalIds.Add(actor.ServerId);
                    // once per warp object, like the NPC's +0x420 flag; the effect stays at the warp's cell
                    if (activeWarpPortalEffects.ContainsKey(actor.ServerId) ||
                        !bridge.TrySpawn(Ragnarok.Combat.RoOriginalCombatRules.EfWarpNpc, ResolveSnapshotWorldPosition(actor),
                            default(RoEffectSpawnOptions), out uint handle)) {
                        continue;
                    }

                    activeWarpPortalEffects[actor.ServerId] = handle;
                }

                foreach (uint actorId in activeWarpPortalEffects.Keys) {
                    if (!liveWarpPortalIds.Contains(actorId)) {
                        staleWarpPortalIds.Add(actorId);
                    }
                }

                for (int i = 0; i < staleWarpPortalIds.Count; i++) {
                    bridge.EndEffect(activeWarpPortalEffects[staleWarpPortalIds[i]]);
                    activeWarpPortalEffects.Remove(staleWarpPortalIds[i]);
                }

                warpPortalActorSnapshots.Clear();
                liveWarpPortalIds.Clear();
                staleWarpPortalIds.Clear();
            }
        }

        private void ClearWarpPortalEffects() {
            if (activeWarpPortalEffects.Count == 0) {
                return;
            }

            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            if (bridge != null) {
                foreach (uint handle in activeWarpPortalEffects.Values) {
                    bridge.EndEffect(handle);
                }
            }

            activeWarpPortalEffects.Clear();
        }

        private static bool IsWarpPortalActor(RoOnlineActorSnapshot actor) {
            return actor.ServerId != 0 &&
                (actor.ObjectType == RoMapActorObjectType.Warp || actor.Job == 45);
        }

        private static bool ShouldSpawnDamageFeedback(RoZcNotifyAct action) {
            return RoMapActorActionSemantics.IsDamage(action.ActionRaw);
        }

        public static string BuildActionFailureFeedbackLabel(RoZcActionFailure failure) {
            return BuildActionFailureChatMessage(failure);
        }

        public static string BuildActionFailureChatMessage(RoZcActionFailure failure) {
            switch (failure.TypeRaw) {
                case 0:
                    return "Please equip proper ammunition";
                case 1:
                    return "Weight limit exceeded";
                case 2:
                    return "Skill blocked by weight";
                case 3:
                    return "Ammunition has been equipped.";
                default:
                    return $"Action failed ({failure.TypeRaw})";
            }
        }

        public static string BuildSkillFailureChatMessage(RoZcAckToUseSkill ack) {
            return BuildSkillFailureChatMessage(ack, skillNameResolver: null);
        }

        public static string BuildSkillFailureChatMessage(
            RoZcAckToUseSkill ack,
            Func<ushort, string> skillNameResolver) {
            if (ack.IsSuccess) {
                return string.Empty;
            }

            switch (ack.CauseRaw) {
                case SkillUseFailureCauseCooldownDelay:
                    return $"Cannot use [{ResolveSkillFailureDisplayName(ack.SkillId, skillNameResolver)}] due to cooldown delay.";
                default:
                    return "Skill has failed.";
            }
        }

        private static string ResolveSkillFailureDisplayName(ushort skillId, Func<ushort, string> skillNameResolver) {
            if (skillNameResolver != null) {
                string name = skillNameResolver(skillId);
                if (!string.IsNullOrWhiteSpace(name)) {
                    return name.Trim();
                }
            }

            return $"Skill {skillId}";
        }

        private void ValidateSelectedTarget() {
            EnsureRuntimeState();
            if (selectedTarget.HasTarget && !worldState.TryGetActor(selectedTarget.ServerId, out _)) {
                ClearSelectedTarget();
            }
        }

        private RoOnlineActorPresenter EnsureActorPresenter() {
            if (actorPresenter == null) {
                actorPresenter = GetComponentInChildren<RoOnlineActorPresenter>();
                if (actorPresenter == null) {
                    var presenterObject = new GameObject("RO Online Actor Presenter");
                    presenterObject.transform.SetParent(transform, false);
                    actorPresenter = presenterObject.AddComponent<RoOnlineActorPresenter>();
                }
            }

            actorPresenter.SetWorldController(this);
            actorPresenter.StatusLookProvider ??= ResolveStatusLook;
            actorPresenter.SetRuntimeMapController(runtimeMapController);
            if (gridSelector != null) {
                actorPresenter.SetGridSelector(gridSelector);
            }

            EnsureActorHoverLabelOverlay();
            return actorPresenter;
        }

        private RoActorHoverLabelOverlay EnsureActorHoverLabelOverlay() {
            if (actorHoverLabelOverlay == null) {
                actorHoverLabelOverlay = GetComponentInChildren<RoActorHoverLabelOverlay>();
                if (actorHoverLabelOverlay == null) {
                    var overlayObject = new GameObject("RO Online Actor Hover Labels");
                    overlayObject.transform.SetParent(transform, false);
                    actorHoverLabelOverlay = overlayObject.AddComponent<RoActorHoverLabelOverlay>();
                }
            }

            actorHoverLabelOverlay.SetWorldController(this);
            return actorHoverLabelOverlay;
        }

        private void SyncDebugMarkers() {
            using (SyncDebugMarkersMarker.Auto()) {
                if (!createDebugMarkers) {
                    return;
                }

                RoOnlineDebugMarkerRenderer renderer = EnsureDebugMarkerRenderer();
                renderer.Configure(this, gridSelector, includeSelfDebugMarker);
                renderer.SyncNow();
            }
        }

        private RoOnlineDebugMarkerRenderer EnsureDebugMarkerRenderer() {
            if (debugMarkerRenderer == null) {
                debugMarkerRenderer = GetComponentInChildren<RoOnlineDebugMarkerRenderer>();
                if (debugMarkerRenderer == null) {
                    var rendererObject = new GameObject("RO Online Debug Marker Renderer");
                    rendererObject.transform.SetParent(transform, false);
                    debugMarkerRenderer = rendererObject.AddComponent<RoOnlineDebugMarkerRenderer>();
                }
            }

            debugMarkerRenderer.Configure(this, gridSelector, includeSelfDebugMarker);

            return debugMarkerRenderer;
        }

        private void ClearDebugMarkers() {
            if (debugMarkerRenderer != null) {
                debugMarkerRenderer.ClearMarkers();
            }
        }

        private void Awake() {
            // the original client's tables parse off the main thread while the map and actors come in
            Ragnarok.Effects.RoOriginalSkillEffectTable.Preload();
            Ragnarok.Effects.RoOriginalStatusEffectTable.Preload();
            // the classic effects' check for actors that left before they were presented (their effects end with them)
            Ragnarok.Effects.Classic.RoClassicEffectBridge.ActorExists = id =>
                worldState != null && (worldState.TryGetActor(id, out _) || worldState.TryGetActorByAccountId(id, out _));
        }

        private void EnsureRuntimeState() {
            EnsureWorldState();
            EnsureCombatPresentationOptions();
        }

        private RoOnlineWorldState EnsureWorldState() {
            worldState ??= new RoOnlineWorldState();
            return worldState;
        }
    }
}
