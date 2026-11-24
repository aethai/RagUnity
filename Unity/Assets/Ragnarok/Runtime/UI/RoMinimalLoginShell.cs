using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ragnarok.Assets;
using Ragnarok.Audio;
using Ragnarok.Content;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Gameplay;
using Ragnarok.Localization;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.Sprites;
using Ragnarok.World;
using UnityEngine;
using Unity.Profiling;

namespace Ragnarok.UI {
    public sealed partial class RoMinimalLoginShell : MonoBehaviour, IRoBasicInfoIconHost {
        private enum CharacterDeleteRequest {
            Reserve = 0,
            Confirm = 1,
            Cancel = 2,
            Delete = 3
        }

        private struct RuntimePerfCaptureSample {
            public float ElapsedSeconds;
            public int Frame;
            public long ManagedMemoryBytes;
            public long ManagedMemoryDeltaBytes;
            public int SessionSequence;
            public int AppliedSequence;
            public int PendingDepth;
            public bool HasApplyAge;
            public int ApplyAgeMilliseconds;
            public int OverflowFallbackCount;
            public long SkippedSequenceCount;
            public long SequenceJumpDistance;
            public int ActorCount;
            public int MovingActorCount;
            public int PooledActorStateCount;
            public int MaxPooledActorStateCount;
            public bool HasServerTime;
            public uint LatestServerTimeMilliseconds;
            public int AcceptedMoves;
            public int IgnoredStaleMoves;
            public int IgnoredDuplicateMoves;
            public int IgnoredInvalidMoves;
            public bool HasCommandState;
            public string Stage;
            public bool IsActionLocked;
            public int ActionLockRemainingMilliseconds;
            public string ActionLockReason;
            public long DrainToResultCalls;
            public long DrainToResultArrayCount;
            public long DrainToResultElementCount;
            public long DrainToResultEstimatedMinBytes;
            public long SnapshotRecentCalls;
            public long SnapshotRecentArrayCount;
            public long SnapshotRecentElementCount;
            public long SnapshotRecentEstimatedMinBytes;
            public long SessionSnapshots;
            public long SchedulerRefreshes;
            public long SchedulerSkipped;
            public long ActorPresenterSyncs;
            public long ActorPresenterActorsProcessed;
            public long ActorPresenterActorsChanged;
            public long ForcedPresenterSyncs;
            public long ForcedActorMotions;
            public long PacketTraceWrites;
            public long PacketTraceSkipped;
            public long PacketLogRefreshes;
            public long CombatTraceRecords;
            public long DebugLogLines;
            public int StaticChunksVisible;
            public int StaticChunksTotal;
        }

        private const int MovementCriticalLiveDrainBurstBudget = 64;
        private const int MerchantChangeCartSkillId = 154;
        private const float RuntimePerfCaptureDefaultDurationSeconds = 60f;
        private const float RuntimePerfCaptureSampleIntervalSeconds = 0.5f;

        [SerializeField] private string account = string.Empty;
        [SerializeField] private string password = string.Empty;
        [SerializeField] private string characterName = string.Empty;
        [SerializeField] private int characterSlot;
        [SerializeField] private ushort hairColor;
        [SerializeField] private ushort hairStyle = 1;
        [SerializeField] private int startJob;
        [SerializeField] private byte createSex = 1;
        [SerializeField] private string loginHost = "localhost";
        [SerializeField] private int loginPort = 6900;
        [SerializeField] private int characterPort = 6121;
        [SerializeField] private int mapPort = 5121;
        [SerializeField] private int timeoutMilliseconds = 5000;
        [SerializeField] private RoRuntimeDiagnosticsOptions diagnosticsOptions = new RoRuntimeDiagnosticsOptions {
            EnableRuntimePerfLog = true
        };
        [SerializeField] private bool pushMapEntryToOnlineWorld = true;
        [SerializeField, Min(1)] private int liveDrainApplyBudgetPerFrame = 1;
        [SerializeField] private bool enableSameMapTransitionFade;
        [SerializeField] private RoOnlineWorldController onlineWorldController;
        [SerializeField] private RagnarokContentSettings browserContentSettings;

        private RoLoginShellSession session;
        private RoLoginEnterMapResult appliedWorldMapResult;
        private int appliedLiveDrainSequence;
        private int appendedLiveChatSequence;
        private int liveDrainOverflowFallbackCount;
        private long liveDrainSkippedSequenceCount;
        private long liveDrainSequenceJumpDistance;
        private int liveDrainPendingDepth;
        private int liveDrainApplyAgeMilliseconds;
        private bool hasLiveDrainApplyAge;
        private bool mapDiagnosticsReportWritten;
        private int mapDiagnosticsReportSequence;
        private DateTime mapDiagnosticsStartedUtc;
        private RoMovementDiagnosticsSnapshot cachedMapDiagnosticsMovement;
        private bool cachedMapDiagnosticsHasWorldState;
        private int cachedMapDiagnosticsActorCount;
        private int cachedMapDiagnosticsMovingActorCount;
        private int cachedMapDiagnosticsPooledActorStateCount;
        private int cachedMapDiagnosticsMaxPooledActorStateCount;
        private bool cachedMapDiagnosticsHasServerTime;
        private uint cachedMapDiagnosticsLatestServerTimeMilliseconds;
        private readonly List<string> cachedClickToMoveTrace = new List<string>(200);
        private bool cachedClickToMoveTraceAvailable;
        private int cachedClickToMoveTraceCount;
        private RoClickToMoveController mapDiagnosticsClickToMoveController;
        private readonly List<RoLiveDrainSnapshot> liveDrainApplyBuffer = new List<RoLiveDrainSnapshot>(64);
        public string LastMapDiagnosticsReportPath { get; private set; } = string.Empty;
        private readonly List<RuntimePerfCaptureSample> runtimePerfCaptureSamples = new List<RuntimePerfCaptureSample>(128);
        private bool runtimePerfCaptureActive;
        private float runtimePerfCaptureStartedAt;
        private float runtimePerfCaptureDurationSeconds;
        private float runtimePerfCaptureNextSampleAt;
        private DateTime runtimePerfCaptureStartedUtc;
        private long runtimePerfCaptureStartManagedMemoryBytes;
        private int runtimePerfCaptureReportSequence;
        public string LastRuntimePerfCapturePath { get; private set; } = string.Empty;
        private RoMapLoadingScreen mapLoadingScreen;
        private bool runtimeMapLoadAwaitingPresentation;
        private string runtimeMapLoadAwaitingMapName;
        private bool isFinishingRuntimeMapLoadingScreen;
        private RoRuntimeDiagnosticsOptions activeDiagnosticsOptions;
        private bool browserLoginFlowLoadingCharacters;
        private bool browserLoginFlowEnteringMap;
        private bool browserLoginFlowCreatingCharacter;
        private RagnarokContentService browserContent;
        private Task browserContentTask;
        private bool browserContentOwned;
        private float browserContentLoadStartedAt = -1f;
        private string browserContentLoadReport = string.Empty;
        private bool browserContentPreLoginLoadingVisible;
        private Ragnarok.UI.Classic.RoClassicMessageBox reconnectPrompt;
        private int makingArrowDismissedSequence = int.MinValue;
        private RoItemMetadataTable cachedGameplayItemMetadata;
        private object cachedGameplayItemMetadataMap;
        private float nextGameplayItemMetadataLookup;
        private readonly RoItemObtainDetector itemObtainDetector = new RoItemObtainDetector();
        private readonly List<uint> shownCustomerShops = new List<uint>();
        private int itemIdentifyDismissedSequence = int.MinValue;
        private int lastSelectionWindowsSequence = int.MinValue;
        private int lastSelectionIdentifyDismissed = int.MinValue;
        private int lastSelectionArrowDismissed = int.MinValue;
        private RoClassicChat chatWindow;
        private RoBasicInfoIconHotkeyExecutor basicInfoHotkeyExecutor;
        private readonly IRoKeyboardInputState interfaceShortcutInput = new RoUnityKeyboardInputState();
        private readonly RoClientSettingsModel clientSettings = new RoClientSettingsModel();
        private RoOnlineWorldController npcTargetRouterWorldController;
        private uint lastNpcContactRequestNpcId;
        private DateTime lastNpcContactRequestUtc = DateTime.MinValue;
        private bool playableLegacyOverlaysSuppressed;
        private int lastScheduledInventorySequence = int.MinValue;
        private int lastScheduledInventoryReloadSequence = int.MinValue;
        private int lastScheduledFunctionalSequence = int.MinValue;
        private RoLoginShellSession phase11UiSnapshotCacheSession;
        private RoLoginShellSnapshot phase11UiSnapshotCache;
        private int phase11UiSnapshotCacheSequence = int.MinValue;
        private long lastProcessedShellSnapshotRevision = long.MinValue;
        private int lastHandledGameOptionsEscapeFrame = -1;
        private bool lastOwnActorDeadForDeathMenu;
        private float nextOnlineWorldControllerLookupTime;
        private readonly object runtimeMapLoadSync = new object();
        private readonly Queue<RuntimeMapLoadRequest> runtimeMapLoadRequests = new Queue<RuntimeMapLoadRequest>();
        private bool isProcessingRuntimeMapLoad;
        private readonly object sameMapTransitionFadeSync = new object();
        private readonly Queue<TaskCompletionSource<bool>> sameMapTransitionFadeRequests = new Queue<TaskCompletionSource<bool>>();
        private bool isProcessingSameMapTransitionFade;
        private TaskCompletionSource<bool> activeSameMapTransitionFadeCompletion;
        private bool sameMapTransitionFadeHeld;
        private readonly RoClassicScreenFade mapTransitionFade = new RoClassicScreenFade();
        private Coroutine mapTransitionFadeCoroutine;
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("RO Shell.Update");
        private static readonly ProfilerMarker ShellSnapshotMarker = new ProfilerMarker("RO Shell.Snapshot");
        private static readonly ProfilerMarker ProcessSameMapTransitionFadeRequestsMarker = new ProfilerMarker("RoLoginShell.Update.ProcessSameMapTransitionFadeRequests");
        private static readonly ProfilerMarker ProcessRuntimeMapLoadRequestsMarker = new ProfilerMarker("RoLoginShell.Update.ProcessRuntimeMapLoadRequests");
        private static readonly ProfilerMarker SyncBrowserVisibilityMarker = new ProfilerMarker("RoLoginShell.Update.SyncBrowserVisibility");
        private static readonly ProfilerMarker SuppressLegacyOverlaysMarker = new ProfilerMarker("RoLoginShell.Update.SuppressLegacyOverlays");
        private static readonly ProfilerMarker HandleMissingMapResultMarker = new ProfilerMarker("RoLoginShell.Update.HandleMissingMapResult");
        private static readonly ProfilerMarker SyncMapLoadingStateMarker = new ProfilerMarker("RoLoginShell.Update.SyncMapLoadingState");
        private static readonly ProfilerMarker ApplySameMapEntryMarker = new ProfilerMarker("RoLoginShell.Update.ApplySameMapEntry");
        private static readonly ProfilerMarker ApplyMapEntryMarker = new ProfilerMarker("RoLoginShell.Update.ApplyMapEntry");
        private static readonly ProfilerMarker ApplyMapEntryWorldMarker = new ProfilerMarker("RoLoginShell.Update.ApplyMapEntry.World");
        private static readonly ProfilerMarker ApplyMapEntryLocalPlayerMarker = new ProfilerMarker("RoLoginShell.Update.ApplyMapEntry.LocalPlayer");
        private static readonly ProfilerMarker ApplyMapEntryGameplayWindowsMarker = new ProfilerMarker("RoLoginShell.Update.ApplyMapEntry.GameplayWindows");
        private static readonly ProfilerMarker ApplyMapEntryPresentationFlushMarker = new ProfilerMarker("RoLoginShell.Update.ApplyMapEntry.PresentationFlush");
        private static readonly ProfilerMarker EnsureGameplayContextWindowsMarker = new ProfilerMarker("RoLoginShell.Update.EnsureGameplayContextWindows");
        private static readonly ProfilerMarker RefreshGameplayWindowsMarker = new ProfilerMarker("RoLoginShell.Update.RefreshGameplayWindows");
        private static readonly ProfilerMarker ApplyLiveDrainMarker = new ProfilerMarker("RO Shell.ApplyLiveDrain");
        private static readonly ProfilerMarker ApplyWorldDrainMarker = new ProfilerMarker("RO Shell.ApplyWorldDrain");
        private static readonly ProfilerMarker ApplyLiveDrainPostSyncMarker = new ProfilerMarker("RO Shell.ApplyLiveDrain.PostSync");
        private const float SameMapTransitionFadeOutSeconds = 0.1f;
        private const float SameMapTransitionFadeInSeconds = 0.14f;
        private const float FastMapLoadingScreenThresholdSeconds = 0.85f;
        private const float FastMapLoadingScreenFinishSeconds = 0.08f;
        private const float WorldControllerLookupRetrySeconds = 0.5f;
        private const double NpcContactRequestDebounceSeconds = 0.45;
        private const byte RestartTypeReturnToSavePoint = 0;
        private const byte RestartTypeCharacterSelect = 1;
        // HasResurrectionItem 0xbf8ab0: Token of Siegfried (7621) and the four other items the client checks
        private static readonly int[] ResurrectionTokenItemIds = { 7621, 6833, 6316, 6293, 25310 };

        private readonly struct RuntimeMapLoadRequest {
            public RuntimeMapLoadRequest(string mapName, int spawnX, int spawnY, TaskCompletionSource<bool> completion) {
                MapName = mapName ?? string.Empty;
                SpawnX = spawnX;
                SpawnY = spawnY;
                Completion = completion;
            }

            public string MapName { get; }
            public int SpawnX { get; }
            public int SpawnY { get; }
            public TaskCompletionSource<bool> Completion { get; }
        }

        public string Account {
            get => account;
            set => account = value ?? string.Empty;
        }

        public RoServerProfile ActiveServerProfile => session != null ? session.Profile : BuildProfile();

        public string Password {
            get => password;
            set => password = value ?? string.Empty;
        }

        public string CharacterName {
            get => characterName;
            set => characterName = value ?? string.Empty;
        }

        public int CharacterSlot {
            get => characterSlot;
            set => characterSlot = value;
        }

        public ushort HairColor {
            get => hairColor;
            set => hairColor = value;
        }

        public ushort HairStyle {
            get => hairStyle;
            set => hairStyle = value;
        }

        public int StartJob {
            get => startJob;
            set => startJob = value;
        }

        public byte CreateSex {
            get => createSex;
            set => createSex = value == 0 ? (byte) 0 : (byte) 1;
        }

        public string LoginHost {
            get => loginHost;
            set => loginHost = value ?? string.Empty;
        }

        public int LoginPort {
            get => loginPort;
            set => loginPort = value;
        }

        public int CharacterPort {
            get => characterPort;
            set => characterPort = value;
        }

        public int MapPort {
            get => mapPort;
            set => mapPort = value;
        }

        public bool IsBrowserCharacterCreateFlowActive => browserLoginFlowCreatingCharacter;
        public int Phase11UiChangeSequence => session != null ? session.Phase11UiChangeSequence : 0;

        public bool TryGetCommandStateSnapshot(out RoShellCommandStateSnapshot snapshot) {
            if (session == null) {
                snapshot = default;
                return false;
            }

            snapshot = session.CommandStateSnapshot();
            return true;
        }

        public bool TrySendMoveRequest(int x, int y, int direction = 0) {
            return session != null &&
                CanOwnActorMoveOrInteract("Move request") &&
                session.TrySendMoveRequest(x, y, direction);
        }

        public bool TrySendAttackRequest(int targetAid, bool repeatAttack = true) {
            return session != null &&
                CanOwnActorMoveOrInteract("Attack request") &&
                session.TrySendAttackRequest(targetAid, repeatAttack);
        }

        public bool TrySendPickupGroundItemRequest(int itemAid) {
            return session != null &&
                CanOwnActorMoveOrInteract("Pickup request") &&
                session.TrySendPickupGroundItemRequest(itemAid);
        }

        public bool TrySendTargetSkillRequest(int skillId, int skillLevel, int targetAid) {
            return session != null &&
                CanOwnActorMoveOrInteract("Skill request") &&
                session.TrySendTargetSkillRequest(skillId, skillLevel, targetAid);
        }

        public bool TrySendActorNameRequest(uint actorId) {
            return session != null && session.TrySendActorNameRequest(actorId);
        }

        public bool TryBeginAttackTarget(int targetAid, bool repeatAttack = true) {
            return TrySendAttackRequest(targetAid, repeatAttack);
        }

        public bool TryBeginPickupGroundItem(int itemAid) {
            return TrySendPickupGroundItemRequest(itemAid);
        }

        public bool TrySendSitRequest() {
            return session != null &&
                CanOwnActorSitOrStand("Sit request") &&
                session.TrySendSitStandRequest(true);
        }

        public bool TrySendStandRequest() {
            return session != null &&
                CanOwnActorSitOrStand("Stand request") &&
                session.TrySendSitStandRequest(false);
        }

        public bool TrySendReturnToSavePointRequest() {
            return session != null && session.TrySendRestartRequest(RestartTypeReturnToSavePoint);
        }

        public bool TrySendCharacterSelectRestartRequest() {
            bool sent = session != null && session.TrySendRestartRequest(RestartTypeCharacterSelect);
            ReturnToCharacterSelectFromPhase11Ui();
            return sent;
        }

        public bool TrySendStandingResurrectionRequest() {
            return session != null && HasTokenOfSiegfried() && session.TrySendStandingResurrectionRequest();
        }

        public bool HasTokenOfSiegfried() {
            if (session == null) {
                return false;
            }

            RoGameplayInventorySnapshot inventory = session.InventorySnapshot();
            if (inventory == null || inventory.InventoryItems == null) {
                return false;
            }

            for (int i = 0; i < inventory.InventoryItems.Length; i++) {
                var item = inventory.InventoryItems[i];
                if (item != null && Array.IndexOf(ResurrectionTokenItemIds, (int) item.ItemId) >= 0 && item.Count > 0) {
                    return true;
                }
            }

            return false;
        }

        public bool TryToggleSitRequest() {
            if (TryGetOwnOnlineActor(out RoOnlineActorSnapshot actor) &&
                RoOnlineActorInteractionRules.IsSitting(actor)) {
                return TrySendStandRequest();
            }

            return TrySendSitRequest();
        }

        private bool CanOwnActorMoveOrInteract(string requestName) {
            if (!TryGetOwnOnlineActor(out RoOnlineActorSnapshot actor) ||
                RoOnlineActorInteractionRules.CanMoveOrInteract(actor)) {
                return true;
            }

            Debug.LogWarning($"{requestName} blocked: local actor is {(RoOnlineActorInteractionRules.IsDead(actor) ? "dead" : "sitting")}.");
            return false;
        }

        private bool CanOwnActorSitOrStand(string requestName) {
            if (!TryGetOwnOnlineActor(out RoOnlineActorSnapshot actor) ||
                !RoOnlineActorInteractionRules.IsDead(actor)) {
                return true;
            }

            Debug.LogWarning($"{requestName} blocked: local actor is dead.");
            return false;
        }

        private bool TryGetOwnOnlineActor(out RoOnlineActorSnapshot actor) {
            RoOnlineWorldController controller = ResolveOnlineWorldController(forceLookup: true);
            if (controller != null &&
                controller.OwnActorId != 0 &&
                controller.State.TryGetActor(controller.OwnActorId, out actor)) {
                return true;
            }

            actor = default;
            return false;
        }

        private RoRuntimeDiagnosticsOptions ActiveDiagnosticsOptions =>
            activeDiagnosticsOptions ?? (activeDiagnosticsOptions = RoRuntimeDiagnostics.Normalize(diagnosticsOptions));

        public RoLiveDrainApplyDiagnosticsSnapshot LiveDrainApplyDiagnostics =>
            new RoLiveDrainApplyDiagnosticsSnapshot(
                liveDrainOverflowFallbackCount,
                liveDrainSkippedSequenceCount,
                liveDrainSequenceJumpDistance,
                liveDrainPendingDepth,
                hasLiveDrainApplyAge,
                liveDrainApplyAgeMilliseconds);

        public bool IsRuntimePerfCaptureActive => runtimePerfCaptureActive;
        public float RuntimePerfCaptureRemainingSeconds =>
            runtimePerfCaptureActive
                ? Mathf.Max(0f, runtimePerfCaptureDurationSeconds - (Time.realtimeSinceStartup - runtimePerfCaptureStartedAt))
                : 0f;

        [ContextMenu("Start 60s Runtime Perf Capture")]
        public void StartRuntimePerfCapture60Seconds() {
            StartRuntimePerfCapture(RuntimePerfCaptureDefaultDurationSeconds);
        }

        [ContextMenu("Stop Runtime Perf Capture")]
        public void StopRuntimePerfCaptureNow() {
            FinishRuntimePerfCapture("manual-stop");
        }

        private void Awake() {
            clientSettings.EnsurePersistedLoaded();   // the saved options (BGM off, volumes...) before anything plays
            RoClassicUiHost.ClientSettings = clientSettings;   // the classic close buttons' and menu icons' hotkey texts
        }

        private void OnValidate() {
            if (Application.isPlaying && isActiveAndEnabled) {
                ApplyRuntimeDiagnosticsOptions(logCombatTraceState: true);
            }
        }

        private void OnEnable() {
            ApplyRuntimeDiagnosticsOptions(logCombatTraceState: false);
            mapDiagnosticsReportWritten = false;
            mapDiagnosticsReportSequence = 0;
            mapDiagnosticsStartedUtc = DateTime.MinValue;
            LastMapDiagnosticsReportPath = string.Empty;
            LastRuntimePerfCapturePath = string.Empty;
            ResetMapRuntimeBoundaryState();
            ClearOnlineWorldControllerForPlayModeBoundary();
            CloseOptionalGameplayWindowsForFreshEntry();
            session = new RoLoginShellSession(
                RoLocalizationService.Empty,
                BuildProfile(),
                timeoutMilliseconds: timeoutMilliseconds,
                targetSkillSent: TryApplyLocalTargetSkillVisual,
                loadRuntimeMapBeforeAck: QueueRuntimeMapLoadForTransition,
                sameMapTransitionBeforeAck: enableSameMapTransitionFade
                    ? (Func<string, Task>) QueueSameMapTransitionFadeBeforeAck
                    : null,
                diagnosticsOptions: activeDiagnosticsOptions);
            Ragnarok.World.RoPickHostility.Relations = session;
            ApplyCombatTraceOptionsToOnlineWorldController();

            lastProcessedShellSnapshotRevision = long.MinValue;

            BeginBrowserContentPreloadBeforeLogin();
            TryEnsureBrowserFrontendScreens();
            RoClassicGameUi.Ensure(this);
        }

        private void OnDisable() {
            FinishRuntimePerfCapture("shell-disabled");
            SaveMapDiagnosticsReport("shell-disabled", appliedWorldMapResult);
            UnbindNpcTargetRouter();
            ClearOnlineWorldControllerForPlayModeBoundary();
            ResetMapRuntimeBoundaryState();

            if (mapLoadingScreen != null) {
                mapLoadingScreen.Hide();
                Destroy(mapLoadingScreen.gameObject);
                mapLoadingScreen = null;
            }

            runtimeMapLoadAwaitingPresentation = false;
            runtimeMapLoadAwaitingMapName = null;
            isFinishingRuntimeMapLoadingScreen = false;
            browserContentPreLoginLoadingVisible = false;
            DisposeBrowserContent();

            if (chatWindow != null) {
                RoClassicChat.Bind(null);
                chatWindow = null;
            }

            CloseOptionalGameplayWindowsForFreshEntry();

            itemObtainDetector.Bind(null, null);
            RoClassicHotkeyBar.Close();
            RoClassicMinimap.Close();

            RoClassicBasicInfo.Close();

            if (ReferenceEquals(Ragnarok.World.RoPickHostility.Relations, session)) Ragnarok.World.RoPickHostility.Relations = null;
            session?.Dispose();
            session = null;
            phase11UiSnapshotCacheSession = null;
            phase11UiSnapshotCache = null;
            phase11UiSnapshotCacheSequence = int.MinValue;
            lastProcessedShellSnapshotRevision = long.MinValue;
            FailPendingRuntimeMapLoads(new ObjectDisposedException(nameof(RoMinimalLoginShell)));
            FailPendingSameMapTransitionFades(new ObjectDisposedException(nameof(RoMinimalLoginShell)));
            sameMapTransitionFadeHeld = false;
            if (mapTransitionFadeCoroutine != null) {
                StopCoroutine(mapTransitionFadeCoroutine);
                mapTransitionFadeCoroutine = null;
            }

            mapTransitionFade.Hide();
            RoUiPointerCapture.SetGlobalBlocker(this, false);
        }

        private void ResetMapRuntimeBoundaryState() {
            appliedWorldMapResult = null;
            appliedLiveDrainSequence = 0;
            appendedLiveChatSequence = 0;
            runtimeMapLoadAwaitingPresentation = false;
            runtimeMapLoadAwaitingMapName = null;
            browserLoginFlowLoadingCharacters = false;
            browserLoginFlowEnteringMap = false;
            browserLoginFlowCreatingCharacter = false;
            phase11UiSnapshotCacheSession = null;
            phase11UiSnapshotCache = null;
            phase11UiSnapshotCacheSequence = int.MinValue;
            lastProcessedShellSnapshotRevision = long.MinValue;
        }

        private void ClearOnlineWorldControllerForPlayModeBoundary() {
            RoOnlineWorldController controller = ResolveOnlineWorldController(forceLookup: true);
            if (controller == null) {
                return;
            }

            controller.SetServerClock(null);
            controller.ClearWorld();
        }

        private void TryApplyLocalTargetSkillVisual(int skillId, int skillLevel, int targetAid) {
            RoOnlineWorldController controller = ResolveOnlineWorldController(forceLookup: true);
            RoRuntimeMapController mapController = ResolveActiveRuntimeMapController(controller);
            int rangeCells = ResolveTargetSkillRangeCells(mapController, skillId, skillLevel);
            if (rangeCells >= 0) {
                mapController?.TryRecordLocalTargetSkillUse(skillId, skillLevel, targetAid, rangeCells);
            }

            controller?.TryApplyLocalPlayDeadSkillUse(skillId, targetAid);
            TryOpenChangeCartWindowForSkill(skillId);
        }

        private void TryOpenChangeCartWindowForSkill(int skillId) {
            if (skillId != MerchantChangeCartSkillId ||
                session == null ||
                !TryGetOwnOnlineActor(out RoOnlineActorSnapshot actor) ||
                !actor.HasCart) {
                return;
            }

            AppendLocalChangeCartChat();
            RoClassicSelectCart.Show(session, ResolveGameplayUiAssetDatabase(), ResolveLocalBaseLevel(), 0, null);
        }

        private int ResolveLocalBaseLevel() {
            if (session == null) {
                return 0;
            }

            RoShellBasicInfoSnapshot basic = session.BasicInfoSnapshot();
            int statusLevel = basic.Status != null ? basic.Status.BaseLevel : 0;
            int characterLevel = basic.Character.BaseLevel;
            return Math.Max(0, Math.Max(statusLevel, characterLevel));
        }

        private void AppendLocalChangeCartChat() {
            RoClassicChat chat = ResolveChatWindow();
            if (chat == null) {
                return;
            }

            chat.AddLine("Change Cart!!", "#f0eadb", Ragnarok.Network.RoChatChannel.Main);
        }

        private static int ResolveTargetSkillRangeCells(RoRuntimeMapController mapController, int skillId, int skillLevel) {
            RoSkillMetadataTable skills = mapController?.Content?.Assets?.GameData?.Skills;
            if (skills != null && skills.TryGetSkill(skillId, out RoSkillMetadata skill) && skill != null) {
                return RoClickToMoveController.ResolveServerTargetSkillRangeCells(skill.GetAttackRange(skillLevel));
            }

            return -1;
        }

        public RoLoginShellSnapshot GetSnapshotForPhase11Ui() {
            if (session == null) {
                phase11UiSnapshotCacheSession = null;
                phase11UiSnapshotCache = null;
                phase11UiSnapshotCacheSequence = int.MinValue;
                return null;
            }

            int sequence = session.Phase11UiChangeSequence;
            if (ReferenceEquals(phase11UiSnapshotCacheSession, session) &&
                phase11UiSnapshotCacheSequence == sequence &&
                phase11UiSnapshotCache != null) {
                return phase11UiSnapshotCache;
            }

            phase11UiSnapshotCacheSession = session;
            phase11UiSnapshotCacheSequence = sequence;
            phase11UiSnapshotCache = session.Phase11UiSnapshot();
            return phase11UiSnapshotCache;
        }

        public bool IsBrowserContentReady =>
            browserContent != null && browserContent.ContentReady;

        public bool IsBrowserContentLoading {
            get {
                if (IsBrowserContentReady) {
                    return false;
                }

                if (browserContentTask != null && !browserContentTask.IsCompleted) {
                    return true;
                }

                return browserContent != null &&
                    browserContent.InitializationState != RagnarokContentInitializationState.NotStarted &&
                    browserContent.InitializationState != RagnarokContentInitializationState.Failed;
            }
        }

        public float BrowserContentLoadPercent => ResolveBrowserContentLoadPercent();

        public bool TryGetBrowserContentAssets(out RoAssetDatabase assets) {
            if (IsBrowserContentReady) {
                assets = browserContent.Assets;
                return true;
            }

            assets = null;
            return false;
        }

        public bool BeginBrowserContentPreload(out string report) {
            if (IsBrowserContentReady) {
                report = "RO content is ready.";
                return true;
            }

            if (TryGetBrowserContentLoadFailure(out report)) {
                return false;
            }

            EnsureBrowserContentService();
            if (browserContent == null) {
                report = "Could not create RO content service.";
                return false;
            }

            if (IsBrowserContentReady) {
                report = "RO content is ready.";
                return true;
            }

            if (TryGetBrowserContentLoadFailure(out report)) {
                return false;
            }

            if (browserContentTask != null && !browserContentTask.IsCompleted) {
                report = browserContentLoadReport;
                return false;
            }

            if (TryResolveBrowserContentSettings(
                out RagnarokContentSettings settings,
                out string settingsReport)) {
                try {
                    browserContentTask = browserContent.BeginInitializeForStartup(settings);
                    browserContentLoadStartedAt = Time.unscaledTime;
                    browserContentLoadReport = settingsReport;
                } catch (Exception ex) {
                    browserContentLoadReport = $"Could not start RO content initialization: {ex.Message}";
                    report = browserContentLoadReport;
                    return false;
                }

                report = browserContentLoadReport;
                return IsBrowserContentReady;
            }

            if (!RagnarokContentProfileStore.TryLoad(out RagnarokContentProfile profile, out report)) {
                browserContentLoadReport = report;
                return false;
            }

            try {
                browserContentTask = browserContent.BeginInitializeForStartup(profile);
                browserContentLoadStartedAt = Time.unscaledTime;
                browserContentLoadReport = report;
            } catch (Exception ex) {
                browserContentLoadReport = $"Could not start RO content initialization: {ex.Message}";
                report = browserContentLoadReport;
                return false;
            }

            report = browserContentLoadReport;
            return IsBrowserContentReady;
        }

        public bool TryGetBrowserContentLoadFailure(out string report) {
            if (browserContent != null &&
                browserContent.InitializationState == RagnarokContentInitializationState.Failed) {
                report = browserContent.InitializationException != null
                    ? $"Could not initialize RO content: {browserContent.InitializationException.Message}"
                    : "Could not initialize RO content.";
                return true;
            }

            if (browserContentTask != null && browserContentTask.IsFaulted) {
                Exception exception = browserContentTask.Exception?.GetBaseException();
                report = exception != null
                    ? $"Could not initialize RO content: {exception.Message}"
                    : "Could not initialize RO content.";
                return true;
            }

            report = string.Empty;
            return false;
        }

        private void BeginBrowserContentPreloadBeforeLogin() {
            if (!Application.isPlaying) {
                return;
            }

            BeginBrowserContentPreload(out _);
        }

        private bool TryEnsureBrowserFrontendScreens() {
            if (!Application.isPlaying) {
                return false;
            }

            if (ShouldDelayBrowserLoginForContent()) {
                ShowBrowserContentPreLoginLoadingScreen();
                return false;
            }

            HideBrowserContentPreLoginLoadingScreen();
            // the classic pre-game screens (RoClassicPreGame: login, server list, wait, character select and creation)
            return RoClassicPreGame.Ensure(this) != null;
        }

        private bool ShouldDelayBrowserLoginForContent() {
            if (!Application.isPlaying ||
                IsBrowserContentReady ||
                TryGetBrowserContentLoadFailure(out _)) {
                return false;
            }

            return IsBrowserContentLoading ||
                (browserContentTask != null && !browserContentTask.IsCompleted);
        }

        private void ShowBrowserContentPreLoginLoadingScreen() {
            RoMapLoadingScreen loadingScreen = EnsureMapLoadingScreen();
            loadingScreen.Show(null);
            loadingScreen.SetProgress(BrowserContentLoadPercent);
            browserContentPreLoginLoadingVisible = true;
        }

        private void HideBrowserContentPreLoginLoadingScreen() {
            if (!browserContentPreLoginLoadingVisible) {
                return;
            }

            if (mapLoadingScreen != null) {
                mapLoadingScreen.Hide();
            }

            browserContentPreLoginLoadingVisible = false;
        }

        private void EnsureBrowserContentService() {
            if (browserContent != null) {
                return;
            }

            RagnarokContentBootstrap bootstrap = FindFirstObjectByType<RagnarokContentBootstrap>();
            if (bootstrap != null && bootstrap.Content != null) {
                browserContent = bootstrap.Content;
                browserContentTask = bootstrap.StartupInitializeTask;
                browserContentOwned = false;
                browserContentLoadStartedAt = Time.unscaledTime;
                browserContentLoadReport = "Using scene RO content bootstrap.";
                return;
            }

            RoRuntimeMapController mapController = FindFirstObjectByType<RoRuntimeMapController>();
            if (mapController != null &&
                mapController.TryBeginContentPreloadForStartup(out string mapContentReport) &&
                mapController.Content != null) {
                browserContent = mapController.Content;
                browserContentTask = mapController.ContentPreloadTask;
                browserContentOwned = false;
                browserContentLoadStartedAt = Time.unscaledTime;
                browserContentLoadReport = mapContentReport;
                return;
            }

            browserContent = new RagnarokContentService();
            browserContentTask = null;
            browserContentOwned = true;
            browserContentLoadStartedAt = -1f;
            browserContentLoadReport = string.Empty;
        }

        private bool TryResolveBrowserContentSettings(out RagnarokContentSettings settings, out string report) {
            if (browserContentSettings != null) {
                settings = browserContentSettings;
                report = $"Preloading RO content from login settings '{browserContentSettings.name}'.";
                return true;
            }

            if (RagnarokContentProfileStore.TryLoadLocalSettings(out settings, out report)) {
                return true;
            }

            return false;
        }

        private float ResolveBrowserContentLoadPercent() {
            if (IsBrowserContentReady) {
                return 100f;
            }

            if (browserContent == null) {
                return 0f;
            }

            float elapsed = browserContentLoadStartedAt >= 0f
                ? Mathf.Max(0f, Time.unscaledTime - browserContentLoadStartedAt)
                : 0f;

            return browserContent.InitializationState switch {
                RagnarokContentInitializationState.LoadingCachedIndex => Mathf.Min(92f, 12f + elapsed * 55f),
                RagnarokContentInitializationState.RebuildingIndex => Mathf.Min(95f, 8f + elapsed * 2.2f),
                RagnarokContentInitializationState.Ready => 100f,
                RagnarokContentInitializationState.Failed => 0f,
                _ => browserContentTask != null && !browserContentTask.IsCompleted
                    ? Mathf.Min(80f, 5f + elapsed * 4f)
                    : 0f
            };
        }

        private void DisposeBrowserContent() {
            if (browserContentOwned) {
                browserContent?.Dispose();
            }

            browserContent = null;
            browserContentTask = null;
            browserContentOwned = false;
            browserContentLoadStartedAt = -1f;
            browserContentLoadReport = string.Empty;
        }

        public void LoadCharactersFromPhase11Ui() {
            TryStart(() => {
                if (session == null) {
                    return;
                }

                browserLoginFlowLoadingCharacters = RoClassicPreGame.LoginVisible;
                browserLoginFlowEnteringMap = false;
                browserLoginFlowCreatingCharacter = false;
                ClearOnlineWorld();
                session.SetProfile(BuildProfile());
                session.BeginLoadCharacters(account, password);
            });
        }

        /// <summary>
        /// The classic login's OK: the login alone; the character server list (UISelectServerWnd 0x2, the ServerSelect stage)
        /// comes next, then <see cref="SelectServerFromPhase11Ui"/> loads the characters.
        /// </summary>
        public void LoginFromPhase11Ui() {
            TryStart(() => {
                if (session == null) {
                    return;
                }

                browserLoginFlowLoadingCharacters = RoClassicPreGame.LoginVisible;
                browserLoginFlowEnteringMap = false;
                browserLoginFlowCreatingCharacter = false;
                ClearOnlineWorld();
                session.SetProfile(BuildProfile());
                session.BeginLogin(account, password);
            });
        }

        /// <summary>The pending login's character servers, for the classic server list.</summary>
        public RoLoginCharServerInfo[] ServerListForPhase11Ui() {
            return session != null ? session.ServerListSnapshot() : Array.Empty<RoLoginCharServerInfo>();
        }

        /// <summary>The server list's OK: connects to that character server and loads its characters.</summary>
        public void SelectServerFromPhase11Ui(int index) {
            TryStart(() => {
                if (session == null) {
                    return;
                }

                session.BeginSelectServer(index);
            });
        }

        /// <summary>The server list's Cancel: back to the login.</summary>
        public void CancelServerSelectFromPhase11Ui() {
            session?.CancelServerSelect();
        }

        public void ResetFromPhase11Ui() {
            TryStart(() => {
                if (session == null) {
                    return;
                }

                browserLoginFlowLoadingCharacters = false;
                browserLoginFlowEnteringMap = false;
                browserLoginFlowCreatingCharacter = false;
                session.Reset();
                ClearOnlineWorld();
            });
        }

        public void SelectCharacterFromPhase11Ui(string selectedCharacterName) {
            if (session == null || string.IsNullOrWhiteSpace(selectedCharacterName)) {
                return;
            }

            characterName = selectedCharacterName.Trim();
            session.SelectCharacter(characterName);
        }

        public void EnterMapFromPhase11Ui() {
            TryStart(() => {
                if (session == null) {
                    return;
                }

                browserLoginFlowLoadingCharacters = false;
                browserLoginFlowEnteringMap = RoClassicPreGame.SelectVisible;
                browserLoginFlowCreatingCharacter = false;
                ClearOnlineWorld();
                session.SetProfile(BuildProfile());
                session.BeginEnterMap(account, password, characterName, characterSlot, hairColor, hairStyle, startJob);
                if (browserLoginFlowEnteringMap) {
                    // Cover the world immediately, in the same synchronous step that hides char-select,
                    // so the char-select backdrop (map + character preview) never flashes in the gap
                    // before the async map-load loading screen appears a few frames later.
                    ShowRuntimeMapLoadingScreen(null);
                }

            });
        }

        public void ReconnectFromPhase11Ui() {
            ReconnectFromPhase11Ui(automaticReconnectAlreadyStarted: false, RoMonotonicTime.NowMilliseconds);
        }

        private void ReconnectFromPhase11Ui(bool automaticReconnectAlreadyStarted, long nowMs) {
            TryStart(() => {
                if (session == null) {
                    return;
                }

                browserLoginFlowLoadingCharacters = false;
                browserLoginFlowEnteringMap = true;
                browserLoginFlowCreatingCharacter = false;
                ClearOnlineWorld();
                session.ServerClockSync.Reset();
                session.BeginReconnectMapEntry(nowMs, automaticReconnectAlreadyStarted);
            });
        }

        public void CreateCharacterFromPhase11Ui() {
            TryStart(() => {
                if (session == null) {
                    return;
                }

                browserLoginFlowLoadingCharacters = false;
                browserLoginFlowEnteringMap = false;
                browserLoginFlowCreatingCharacter = RoClassicPreGame.CreateVisible;
                ClearOnlineWorld();
                session.SetProfile(BuildProfile());
                session.BeginCreateCharacter(account, password, characterName, characterSlot, hairColor, hairStyle, startJob, createSex);
            });
        }

        public void ReserveDeleteCharacterFromPhase11Ui(int characterId) {
            BeginDeleteCharacterFromPhase11Ui(characterId, string.Empty, CharacterDeleteRequest.Reserve);
        }

        public void ConfirmDeleteCharacterFromPhase11Ui(int characterId, string birthdateYyMmDd) {
            BeginDeleteCharacterFromPhase11Ui(characterId, birthdateYyMmDd, CharacterDeleteRequest.Confirm);
        }

        /// <summary>The Delete button past the deletion date: the deletion password (account e-mail) goes as CH_DELETE_CHAR2.</summary>
        public void DeleteCharacterFromPhase11Ui(int characterId, string deletionKey) {
            BeginDeleteCharacterFromPhase11Ui(characterId, deletionKey ?? string.Empty, CharacterDeleteRequest.Delete);
        }

        public void CancelDeleteCharacterFromPhase11Ui(int characterId) {
            BeginDeleteCharacterFromPhase11Ui(characterId, string.Empty, CharacterDeleteRequest.Cancel);
        }

        private void BeginDeleteCharacterFromPhase11Ui(int characterId, string birthdateYyMmDd, CharacterDeleteRequest request) {
            TryStart(() => {
                if (session == null || characterId <= 0) {
                    return;
                }

                browserLoginFlowLoadingCharacters = false;
                browserLoginFlowEnteringMap = false;
                browserLoginFlowCreatingCharacter = false;
                ClearOnlineWorld();
                session.SetProfile(BuildProfile());
                switch (request) {
                    case CharacterDeleteRequest.Reserve:
                        session.BeginReserveDeleteCharacter(account, password, characterId);
                        break;
                    case CharacterDeleteRequest.Confirm:
                        session.BeginConfirmDeleteCharacter(account, password, characterId, birthdateYyMmDd);
                        break;
                    case CharacterDeleteRequest.Cancel:
                        session.BeginCancelDeleteCharacter(account, password, characterId);
                        break;
                    case CharacterDeleteRequest.Delete:
                        session.BeginDeleteCharacter(account, password, characterId, birthdateYyMmDd);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(request), request, "Unknown character delete request.");
                }

            });
        }

        public void ReturnToCharacterSelectFromPhase11Ui() {
            TryStart(() => {
                if (session == null) {
                    return;
                }

                browserLoginFlowLoadingCharacters = false;
                browserLoginFlowEnteringMap = false;
                browserLoginFlowCreatingCharacter = false;
                session.ReturnToCharacterSelect();
                ClearOnlineWorld();
            });
        }

        private void SyncBrowserLoginFlowWindowVisibility(RoLoginShellSnapshot snapshot) {
            if (snapshot == null) {
                return;
            }

            if (browserLoginFlowEnteringMap) {
                if (snapshot.Stage == RoLoginShellStage.EnteringMap ||
                    snapshot.Stage == RoLoginShellStage.MapEntered) {
                    return;
                }

                if (snapshot.Stage == RoLoginShellStage.Login ||
                    snapshot.Stage == RoLoginShellStage.Failed ||
                    snapshot.Stage == RoLoginShellStage.CharacterSelect) {
                    browserLoginFlowEnteringMap = false;
                    // The map entry bounced back to the front-end (cancel/failure), so drop the cover
                    // loading screen shown on enter — otherwise it stays stuck over char-select.
                    if (!browserContentPreLoginLoadingVisible && mapLoadingScreen != null) {
                        mapLoadingScreen.Hide();
                    }

                    return;
                }
            }

            if (browserLoginFlowCreatingCharacter) {
                if (snapshot.Stage == RoLoginShellStage.LoadingCharacters ||
                    snapshot.Stage == RoLoginShellStage.Failed) {
                    return;
                }

                if (snapshot.Stage == RoLoginShellStage.CharacterSelect ||
                    snapshot.Stage == RoLoginShellStage.Login) {
                    browserLoginFlowCreatingCharacter = false;
                    return;
                }
            }

            if (!browserLoginFlowLoadingCharacters) {
                return;
            }

            if (snapshot.Stage == RoLoginShellStage.LoadingCharacters) {
                return;
            }

            if (snapshot.Stage == RoLoginShellStage.CharacterSelect) {
                browserLoginFlowLoadingCharacters = false;
                return;
            }

            if (snapshot.Stage == RoLoginShellStage.Login ||
                snapshot.Stage == RoLoginShellStage.Failed) {
                browserLoginFlowLoadingCharacters = false;
            }
        }

        private void Update() {
            long shellUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                using (UpdateMarker.Auto()) {
                    CleanupDestroyedReferences();
                    RefreshMapDiagnosticsSnapshot();
                    TickRuntimePerfCapture();

                using (ProcessSameMapTransitionFadeRequestsMarker.Auto()) {
                    ProcessSameMapTransitionFadeRequests();
                }

                using (ProcessRuntimeMapLoadRequestsMarker.Auto()) {
                    ProcessRuntimeMapLoadRequests();
                }

                BeginBrowserContentPreloadBeforeLogin();
                TryEnsureBrowserFrontendScreens();
                TryBeginLauncherLogin();
                ProcessBasicInfoIconHotkeys();
                SyncReconnectPrompt();

                if (!pushMapEntryToOnlineWorld || session == null) {
                    return;
                }

                if (TryProcessStablePlayableGameplayLightPath()) {
                    return;
                }

                long snapshotRevision = BuildShellUpdateRevision();
                if (CanSkipPlayableFrontendShellUpdate(snapshotRevision)) {
                    return;
                }

                if (CanSkipStableGameplayShellUpdate(snapshotRevision)) {
                    return;
                }

                RoLoginShellSnapshot snapshot;
                using (ShellSnapshotMarker.Auto()) {
                    snapshot = session.Snapshot();
                }

                if (snapshot.Stage == RoLoginShellStage.MapEntered &&
                    snapshot.MapResult != null) {
                    session.MarkReconnectStable(RoMonotonicTime.NowMilliseconds);
                }

                lastProcessedShellSnapshotRevision = snapshotRevision;
                using (SyncBrowserVisibilityMarker.Auto()) {
                    SyncBrowserLoginFlowWindowVisibility(snapshot);
                }

                using (SuppressLegacyOverlaysMarker.Auto()) {
                    playableLegacyOverlaysSuppressed = ShouldKeepPlayableGameplayWindows(snapshot);
                }

                if (snapshot.MapResult == null) {
                    using (HandleMissingMapResultMarker.Auto()) {
                        if (snapshot.Stage == RoLoginShellStage.Login ||
                            snapshot.Stage == RoLoginShellStage.ServerSelect ||
                            snapshot.Stage == RoLoginShellStage.LoadingCharacters ||
                            snapshot.Stage == RoLoginShellStage.EnteringMap ||
                            snapshot.Stage == RoLoginShellStage.Failed) {
                            if (snapshot.Stage != RoLoginShellStage.EnteringMap) {
                                runtimeMapLoadAwaitingPresentation = false;
                                runtimeMapLoadAwaitingMapName = null;
                            }

                            bool hadGameplayWindows = appliedWorldMapResult != null ||
                                RoClassicBasicInfo.IsBound ||
                                HasGameplayWindowReferences();
                            appliedWorldMapResult = null;
                            appliedLiveDrainSequence = 0;
                            appendedLiveChatSequence = 0;
                            if (hadGameplayWindows) {
                                ClearOnlineWorld();
                            }
                        }
                    }

                    return;
                }

                using (SyncMapLoadingStateMarker.Auto()) {
                    SyncSameMapTransitionFadeVisibility(snapshot);
                    SyncMapLoadingScreenCompletion();
                }

                if (!ReferenceEquals(appliedWorldMapResult, snapshot.MapResult)) {
                    bool isSameMapEntryUpdate = IsSameRuntimeMapEntryUpdate(appliedWorldMapResult, snapshot.MapResult);
                    if (appliedWorldMapResult != null && !isSameMapEntryUpdate) {
                        SaveMapDiagnosticsReport("map-transition", appliedWorldMapResult);
                    }

                    RoOnlineWorldController controller = EnsureOnlineWorldController();
                    RoRuntimeMapController activeMapController = ResolveActiveRuntimeMapController(controller);
                    BindRuntimeMapController(controller, activeMapController);
                    BindNpcTargetRouter(controller);
                    if (isSameMapEntryUpdate) {
                        using (ApplySameMapEntryMarker.Auto()) {
                            controller.SetServerClock(session?.ServerClockSync);
                            controller.ApplySameMapEntry(snapshot.MapResult);
                            SyncLocalPlayerToMapEntry(snapshot.MapResult, activeMapController, applyAppearance: false);
                            SyncLocalPlayerStatus(snapshot, activeMapController, controller);
                            RefreshDeathOptionsWindowForOwnActor();
                            CompleteRuntimeMapLoadingAfterPresentation(
                                snapshot.MapResult,
                                controller.FlushActorPresentation(force: false));
                            appliedWorldMapResult = snapshot.MapResult;
                            if (IsClearedLiveDrainHistory(snapshot)) {
                                appliedLiveDrainSequence = snapshot.LiveDrainSequence;
                                appendedLiveChatSequence = snapshot.LiveDrainSequence;
                            }
                        }
                    } else {
                        using (ApplyMapEntryMarker.Auto()) {
                            using (ApplyMapEntryWorldMarker.Auto()) {
                                controller.SetServerClock(session?.ServerClockSync);
                                controller.ApplyMapEntry(snapshot.MapResult);
                            }

                            using (ApplyMapEntryLocalPlayerMarker.Auto()) {
                                SyncLocalPlayerToMapEntry(snapshot.MapResult, activeMapController);
                                SyncLocalPlayerAppearanceFromOnlineWorld(controller, activeMapController);
                                SyncLocalPlayerStatus(snapshot, activeMapController, controller);
                                RefreshDeathOptionsWindowForOwnActor();
                            }

                            using (ApplyMapEntryGameplayWindowsMarker.Auto()) {
                                browserLoginFlowEnteringMap = false;
                                RoEmotionSender.ResetForMap();   // a new game mode: no emotion in flight
                                RoClassicCloseTabs.ResetForMap();   // 0x9d2220: F11 back to its first step

                                if (appliedWorldMapResult == null) {
                                    CloseOptionalGameplayWindowsForFreshEntry();
                                }
                                if (chatWindow == null) {
                                    chatWindow = RoClassicChat.Bind(session);
                                }

                                itemObtainDetector.Bind(session, ResolveGameplayItemMetadata);

                                AppendMapResultChat(snapshot.MapResult);
                                try {
                                    // the shell implements IRoBasicInfoIconHost so the menu icons drive their
                                    // window lifecycles directly
                                    RoClassicBasicInfo.Bind(session, controller, this);
                                } catch (Exception ex) {
                                    Debug.LogWarning($"Basic Info failed to initialize: {ex.Message}");
                                }

                                TimedEnsurePhase12GameplayWindows();

                                TimedEnsureGameplayContextWindows(snapshot);
                                playableLegacyOverlaysSuppressed = ShouldKeepPlayableGameplayWindows(snapshot);
                                TimedRefreshGameplayWindowsFromSequences();
                            }

                            using (ApplyMapEntryPresentationFlushMarker.Auto()) {
                                bool wasAwaitingRuntimeMapPresentation = runtimeMapLoadAwaitingPresentation;
                                CompleteRuntimeMapLoadingAfterPresentation(
                                    snapshot.MapResult,
                                    controller.FlushActorPresentation(force: false));
                                if (!wasAwaitingRuntimeMapPresentation) {
                                    ShowPresentedMapNameOverlay(
                                        activeMapController,
                                        snapshot.MapResult?.CharacterResult?.MapHandoff.MapName);
                                }
                            }

                            appliedWorldMapResult = snapshot.MapResult;
                            appliedLiveDrainSequence = 0;
                            appendedLiveChatSequence = 0;
                            BeginMapDiagnosticsSession(snapshot.MapResult);
                        }
                    }

                    return;
                }

                ApplyLiveDrain(snapshot);
                using (EnsureGameplayContextWindowsMarker.Auto()) {
                    TimedEnsureGameplayContextWindows(snapshot);
                }

                    using (RefreshGameplayWindowsMarker.Auto()) {
                        TimedRefreshGameplayWindowsFromSequences();
                    }
                }
            } finally {
                RoRuntimePerfCounters.AddMinimalLoginShellUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(shellUpdateStartTicks));
            }
        }

        private void CleanupDestroyedReferences() {
            onlineWorldController = ClearIfDestroyed(onlineWorldController);
            mapLoadingScreen = ClearIfDestroyed(mapLoadingScreen);
            if (chatWindow != null && !ReferenceEquals(chatWindow, RoClassicChat.Instance)) chatWindow = null;
            mapDiagnosticsClickToMoveController = ClearIfDestroyed(mapDiagnosticsClickToMoveController);
        }

        private static T ClearIfDestroyed<T>(T value) where T : UnityEngine.Object {
            return ReferenceEquals(value, null) || value != null ? value : null;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private void ProcessBasicInfoIconHotkeys() {
            if (basicInfoHotkeyExecutor == null) {
                basicInfoHotkeyExecutor = new RoBasicInfoIconHotkeyExecutor(
                    BuildBasicInfoIconHotkeyContext,
                    IsTextInputFocusedForBasicInfoHotkeys,
                    new RoUnityKeyboardInputState(),
                    clientSettings);
            }

            if (basicInfoHotkeyExecutor.TryExecute()) {
                return;
            }

            TryExecutePartyFriendsShortcut();
        }

        private bool TryExecutePartyFriendsShortcut() {
            if (IsTextInputFocusedForBasicInfoHotkeys()) {
                return false;
            }

            if (TryHandlePartyFriendsShortcut("interface.friends", RoPartyFriendsTab.Friends, openPartySetup: false, openFriendSetup: false)) {
                return true;
            }

            if (TryHandlePartyFriendsShortcut("interface.friendSetup", RoPartyFriendsTab.Friends, openPartySetup: false, openFriendSetup: true)) {
                return true;
            }

            if (TryHandleChatRoomShortcut()) {
                return true;
            }

            if (TryHandleShortcutListKeys()) {
                return true;
            }

            return TryHandlePartyFriendsShortcut("interface.partySetup", RoPartyFriendsTab.Party, openPartySetup: true, openFriendSetup: false);
        }

        /// <summary>
        /// The Shortcut List keys of the classic UI: Interface action 14 (Alt+M) opens UIEmotionWnd 0x56, the macro keys
        /// (Alt+1 … Alt+0) send its lines.
        /// </summary>
        // the macro keys' action ids, built once (the loop below ran every frame)
        private static readonly string[] MacroShortcutIds = {
            "macro.1", "macro.2", "macro.3", "macro.4", "macro.5", "macro.6", "macro.7", "macro.8", "macro.9", "macro.10"
        };

        private bool TryHandleShortcutListKeys() {
            if (clientSettings == null || session == null) {
                return false;
            }

            if (IsShortcutPressed("interface.shortcuts")) {
                return RoClassicShortcutList.Toggle();
            }

            // Interface 11 "Emoticon Icon List" (Alt+L): hotkey 0x6f deletes or makes the Emoji List 0x57 while Basic Info exists
            RoClassicEmojiList.Bind(session);
            if (IsShortcutPressed("interface.emotions")) {
                if (RoClassicBasicInfo.IsBound) RoClassicEmojiList.Toggle(session);
                return true;
            }

            // Interface 12 "Sit/Stand" (Insert): hotkey 0x70 = mode Msg(0x2a, 0x16), the /sit talk type
            if (IsShortcutPressed("interface.sitstand")) {
                TryToggleSitRequest();
                return true;
            }

            // Interface 15 "Commands List" (Alt+Y): hotkey 0x73 makes window 0x70 when it is not open
            if (IsShortcutPressed("interface.commands")) {
                if (!RoClassicCommandList.IsOpen) RoClassicCommandList.Toggle(clientSettings);
                return true;
            }

            // Interface 16 "Close Tabs" (F11): hotkey 0x74 → 0x9d6530, the three-step window cycle
            if (IsShortcutPressed("interface.closeTabs")) {
                return RoClassicCloseTabs.Press();
            }

            // Interface 18 "Chatting Tab" (Alt+F10): hotkey 0x76 shows / hides the chat window
            if (IsShortcutPressed("interface.chatTab")) {
                RoClassicChat.ToggleShown();
                return true;
            }

            if (IsShortcutPressed("interface.pet")) {
                return RoClassicPet.ToggleInfo();
            }

            if (IsShortcutPressed("interface.homunculus")) {
                return RoClassicCompanion.ToggleHomun();
            }

            if (IsShortcutPressed("interface.mercenary")) {
                return RoClassicCompanion.ToggleMerc();
            }

            if (IsShortcutPressed("interface.adventurerAgency")) {
                return RoClassicAgency.Toggle();
            }

            if (IsShortcutPressed("interface.memorialDungeon")) {
                return RoClassicMemorial.Toggle();
            }

            if (IsShortcutPressed("interface.clan")) {
                return RoClassicClan.Toggle();
            }

            if (IsShortcutPressed("interface.tipbox")) {
                return RoClassicTipBox.Toggle();
            }

            if (IsShortcutPressed("interface.equipmentProperties")) {
                return RoClassicEquipProperties.Toggle();
            }

            if (IsShortcutPressed("interface.cart")) {
                return ToggleClassicCart();
            }

            for (int i = 1; i <= 10; i++) {
                if (IsShortcutPressed(MacroShortcutIds[i - 1])) {
                    RoClassicShortcutList.Send(i - 1, RoClassicChat.Instance);
                    return true;
                }
            }

            return false;
        }

        /// <summary>View Cart (Alt+W): the cart window UIMerchantItemWnd 0x28 opens or closes while a cart is on.</summary>
        private bool ToggleClassicCart() {
            RoCartInventorySnapshot cart = session.CartSnapshot() ?? RoCartInventorySnapshot.Empty;
            if (!RoClassicCart.IsOpen && !cart.HasCart && !cart.IsOpen) return false;
            RoClassicCart.Toggle(session);
            return true;
        }

        private bool IsShortcutPressed(string actionId) {
            if (!clientSettings.TryGetShortcutBinding(actionId, out RoShortcutBindingSnapshot binding) || !binding.Binding.IsAssigned) {
                return false;
            }

            RoKeyChord chord = binding.Binding;
            return RoBasicInfoIconHotkeyExecutor.GetCurrentKeyboardModifiers(interfaceShortcutInput) == chord.Modifiers &&
                   interfaceShortcutInput.IsKeyPressedThisFrame(chord.Key);
        }

        /// <summary>The chat room key (Alt+C by default): the classic create form (UIChatRoomMakeWnd 0x1b) opens or closes.</summary>
        private bool TryHandleChatRoomShortcut() {
            if (clientSettings == null || session == null || !clientSettings.TryGetShortcutBinding("interface.chatroom", out RoShortcutBindingSnapshot binding) ||
                !binding.Binding.IsAssigned) {
                return false;
            }

            RoKeyChord chord = binding.Binding;
            if (RoBasicInfoIconHotkeyExecutor.GetCurrentKeyboardModifiers(interfaceShortcutInput) != chord.Modifiers ||
                !interfaceShortcutInput.IsKeyPressedThisFrame(chord.Key)) {
                return false;
            }

            RoClassicChatRoom.ToggleCreate(session);
            return true;
        }

        private bool TryHandlePartyFriendsShortcut(string actionId, RoPartyFriendsTab tab, bool openPartySetup, bool openFriendSetup) {
            if (clientSettings == null ||
                !clientSettings.TryGetShortcutBinding(actionId, out RoShortcutBindingSnapshot binding) ||
                !binding.Binding.IsAssigned) {
                return false;
            }

            RoKeyChord chord = binding.Binding;
            if (RoBasicInfoIconHotkeyExecutor.GetCurrentKeyboardModifiers(interfaceShortcutInput) != chord.Modifiers ||
                !interfaceShortcutInput.IsKeyPressedThisFrame(chord.Key)) {
                return false;
            }

            RoBasicInfoIconContext context = BuildBasicInfoIconHotkeyContext();
            if (context.Session == null) {
                return false;
            }

            // the classic Party / Friend window (UIMessengerGroupWnd 0x22) on the tab, with its setup dialog
            if (openPartySetup) {
                RoClassicPartyFriends.OpenPartySetup(context.Session);
            } else if (openFriendSetup) {
                RoClassicPartyFriends.OpenFriendSetup(context.Session);
            } else {
                RoClassicPartyFriends.Open(context.Session, tab);
            }

            return true;
        }

        private RoBasicInfoIconContext BuildBasicInfoIconHotkeyContext() {
            return RoClassicBasicInfo.BuildMenuIconContext();
        }

        private static bool IsTextInputFocusedForBasicInfoHotkeys() {
            if (Ragnarok.UI.Classic.RoClassicTextInput.CapturesKeyPress()) return true;
            return RoClassicShortcutSettings.ShouldSuppressGameplayHotkeys;
        }

        public bool TryHandleGameplayEscapeFromUiRoot() {
            if (lastHandledGameOptionsEscapeFrame == Time.frameCount) {
                return true;
            }

            if (!CanToggleGameOptionsFromEscape()) {
                return false;
            }

            lastHandledGameOptionsEscapeFrame = Time.frameCount;
            ToggleGameOptionsWindow();
            return true;
        }

        private bool CanToggleGameOptionsFromEscape() {
            if (session == null || IsTextInputFocusedForBasicInfoHotkeys()) {
                return false;
            }

            RoSkillTargetSelection skillTargetSelection = RoSkillTargetSelection.Current;
            if (skillTargetSelection != null && skillTargetSelection.HasActiveSelection) {
                return false;
            }

            RoShellCommandStateSnapshot commandState = session.CommandStateSnapshot();
            return commandState.Stage == RoLoginShellStage.MapEntered;
        }

        private void ToggleGameOptionsWindow() {
            if (IsOwnActorDeadForDeathMenu()) {
                ShowDeathOptionsWindow();
                return;
            }

            RoClassicEscMenu.Toggle(this, false);
        }

        private void ShowGameOptionsWindow() {
            if (IsOwnActorDeadForDeathMenu()) {
                ShowDeathOptionsWindow();
                return;
            }

            RoClassicEscMenu.Toggle(this, false);
        }

        private void ShowDeathOptionsWindow() {
            RoClassicEscMenu.Toggle(this, true, HasTokenOfSiegfried());
        }

        /// <summary>The settings the classic option windows edit.</summary>
        internal RoClientSettingsModel ClassicClientSettings => clientSettings;

        private void RefreshDeathOptionsWindowForOwnActor() {
            bool isDead = IsOwnActorDeadForDeathMenu();
            if (isDead) {
                if (!lastOwnActorDeadForDeathMenu) {
                    // Open the death menu once, on the alive->dead transition (matches roBrowser, which
                    // only opens it from the death packet). We deliberately do NOT re-open it on every
                    // live drain: the player may dismiss it with "Return to game" and reopen it with
                    // Escape, exactly like roBrowser's .cancel button.
                    ShowDeathOptionsWindow();
                } else if (RoClassicEscMenu.IsDeathMenuMode && RoClassicEscMenu.IsOpen) {
                    // the open layout stays; the Token of Siegfried state counts for the next open
                    RoClassicEscMenu.RefreshDeathMenuSiegfried(HasTokenOfSiegfried());
                }
            } else if (lastOwnActorDeadForDeathMenu && RoClassicEscMenu.IsDeathMenuMode) {
                RoClassicEscMenu.ResetDeathMenu();
                RoClassicEscMenu.Close();
            }

            lastOwnActorDeadForDeathMenu = isDead;
        }

        private bool IsOwnActorDeadForDeathMenu() {
            return TryGetOwnOnlineActor(out RoOnlineActorSnapshot actor) &&
                RoOnlineActorInteractionRules.IsDead(actor);
        }

        private bool TryProcessStablePlayableGameplayLightPath() {
            long startTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                if (!IsStablePlayableGameplayUi() ||
                    session == null ||
                    session.HasManualReconnectPrompt ||
                    !session.IsStableAppliedMapResult(appliedWorldMapResult)) {
                    return false;
                }

                session.MarkReconnectStable(RoMonotonicTime.NowMilliseconds);
                EnsureNpcTargetRouterBound();
                TimedRefreshGameplayWindowsFromSequences();
                int liveDrainSequence = session.LiveDrainSequence;
                if (liveDrainSequence != appliedLiveDrainSequence) {
                    ApplyLiveDrain(session.LiveDrainSnapshot(liveDrainApplyBuffer));
                }

                return true;
            } finally {
                RoRuntimePerfCounters.AddMinimalLoginShellStableLightPathTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(startTicks));
            }
        }

        private void TimedEnsurePhase12GameplayWindows(bool bindExisting = true) {
            long startTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                EnsurePhase12GameplayWindows(bindExisting);
            } finally {
                RoRuntimePerfCounters.AddMinimalLoginShellEnsurePhase12Ticks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(startTicks));
            }
        }

        private void TimedRefreshGameplayWindowsFromSequences() {
            long startTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                RefreshGameplayWindowsFromSequences();
            } finally {
                RoRuntimePerfCounters.AddMinimalLoginShellRefreshGameplayWindowsTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(startTicks));
            }
        }

        private void TimedEnsureGameplayContextWindows(RoLoginShellSnapshot snapshot) {
            long startTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                EnsureGameplayContextWindows(snapshot);
            } finally {
                RoRuntimePerfCounters.AddMinimalLoginShellEnsureGameplayContextTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(startTicks));
            }
        }

        private void TimedEnsureGameplayContextWindowsFromCurrentState(
            RoMapEntryDrainResult latestLiveDrain,
            IReadOnlyList<RoLiveDrainSnapshot> recentLiveDrains
        ) {
            long startTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                EnsureGameplayContextWindowsFromCurrentState(latestLiveDrain, recentLiveDrains);
            } finally {
                RoRuntimePerfCounters.AddMinimalLoginShellEnsureGameplayContextTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(startTicks));
            }
        }

        private bool CanSkipStableGameplayShellUpdate(long snapshotRevision) {
            return snapshotRevision == lastProcessedShellSnapshotRevision &&
                appliedWorldMapResult != null &&
                playableLegacyOverlaysSuppressed &&
                !browserLoginFlowLoadingCharacters &&
                !browserLoginFlowEnteringMap &&
                !browserLoginFlowCreatingCharacter &&
                !isProcessingRuntimeMapLoad &&
                !isProcessingSameMapTransitionFade &&
                !sameMapTransitionFadeHeld &&
                (mapLoadingScreen == null || !mapLoadingScreen.IsVisible);
        }

        private bool IsStablePlayableGameplayUi() {
            return pushMapEntryToOnlineWorld &&
                (session == null || !session.HasManualReconnectPrompt) &&
                appliedWorldMapResult != null &&
                playableLegacyOverlaysSuppressed &&
                !browserLoginFlowLoadingCharacters &&
                !browserLoginFlowEnteringMap &&
                !browserLoginFlowCreatingCharacter &&
                !isProcessingRuntimeMapLoad &&
                !isProcessingSameMapTransitionFade &&
                !sameMapTransitionFadeHeld &&
                (mapLoadingScreen == null || !mapLoadingScreen.IsVisible);
        }

        private long BuildShellUpdateRevision() {
            if (session == null) {
                return long.MinValue;
            }

            unchecked {
                long revision = 17;
                revision = revision * 31 + session.Phase11UiChangeSequence;
                revision = revision * 31 + session.LiveDrainSequence;
                revision = revision * 31 + session.GameplayChangeSequence;
                revision = revision * 31 + session.BasicInfoChangeSequence;
                revision = revision * 31 + session.StatusWindowChangeSequence;
                revision = revision * 31 + session.SkillWindowChangeSequence;
                revision = revision * 31 + session.InventoryWindowChangeSequence;
                revision = revision * 31 + session.EquipmentWindowChangeSequence;
                revision = revision * 31 + session.HotkeyWindowChangeSequence;
                revision = revision * 31 + session.QuestWindowChangeSequence;
                revision = revision * 31 + session.FunctionalWindowChangeSequence;
                revision = revision * 31 + session.StatusIconChangeSequence;
                return revision;
            }
        }

        private bool CanSkipPlayableFrontendShellUpdate(long snapshotRevision) {
            return snapshotRevision == lastProcessedShellSnapshotRevision &&
                appliedWorldMapResult == null &&
                !browserLoginFlowLoadingCharacters &&
                !browserLoginFlowEnteringMap &&
                !browserLoginFlowCreatingCharacter &&
                !browserContentPreLoginLoadingVisible &&
                !isProcessingRuntimeMapLoad &&
                !isProcessingSameMapTransitionFade &&
                !sameMapTransitionFadeHeld &&
                (mapLoadingScreen == null || !mapLoadingScreen.IsVisible);
        }

        private void SyncSameMapTransitionFadeVisibility(RoLoginShellSnapshot snapshot) {
            if (!sameMapTransitionFadeHeld ||
                snapshot == null ||
                snapshot.IsSameMapTransitionPending ||
                snapshot.IsMapTransitionPending) {
                return;
            }

            StartSameMapTransitionFadeIn();
        }

        private void SyncMapLoadingScreenCompletion() {
            if (mapLoadingScreen == null ||
                !mapLoadingScreen.IsVisible ||
                isProcessingRuntimeMapLoad ||
                runtimeMapLoadAwaitingPresentation ||
                isFinishingRuntimeMapLoadingScreen) {
                return;
            }

            mapLoadingScreen.Hide();
        }

        private void CompleteRuntimeMapLoadingAfterPresentation(
            RoLoginEnterMapResult mapResult,
            RoActorPresentationSyncResult syncResult
        ) {
            if (!runtimeMapLoadAwaitingPresentation) {
                return;
            }

            string appliedMapName = mapResult?.CharacterResult?.MapHandoff.MapName;
            if (!string.IsNullOrWhiteSpace(runtimeMapLoadAwaitingMapName) &&
                !AreComparableMapNamesEqual(runtimeMapLoadAwaitingMapName, appliedMapName)) {
                return;
            }

            runtimeMapLoadAwaitingPresentation = false;
            runtimeMapLoadAwaitingMapName = null;
            if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                RoRuntimeDiagnostics.IncrementDebugLogLine();
                Debug.Log(
                    $"RO runtime map load presentation ready: map={NormalizeComparableMapName(appliedMapName)}, actors={mapResult?.InitialState?.ActorEntries.Length ?? 0}, created={syncResult.Created}, rebuilt={syncResult.Rebuilt}, updated={syncResult.Updated}, removed={syncResult.Removed}.",
                    this);
            }

            RoRuntimeMapController mapController = FindFirstObjectByType<RoRuntimeMapController>();
            if (mapLoadingScreen != null && mapLoadingScreen.IsVisible && !isFinishingRuntimeMapLoadingScreen) {
                _ = FinishRuntimeMapLoadingScreenAfterPresentation(mapLoadingScreen, mapController, appliedMapName);
            } else {
                ShowPresentedMapNameOverlay(mapController, appliedMapName);
            }
        }

        private async Task FinishRuntimeMapLoadingScreenAfterPresentation(
            RoMapLoadingScreen loadingScreen,
            RoRuntimeMapController mapController,
            string mapName
        ) {
            if (loadingScreen == null || isFinishingRuntimeMapLoadingScreen) {
                return;
            }

            isFinishingRuntimeMapLoadingScreen = true;
            try {
                await FinishRuntimeMapLoadingScreen(loadingScreen);
                ShowPresentedMapNameOverlay(mapController, mapName);
            } finally {
                isFinishingRuntimeMapLoadingScreen = false;
            }
        }

        private static void ShowPresentedMapNameOverlay(RoRuntimeMapController mapController, string mapName) {
            if (mapController == null || mapController.Content == null || !mapController.Content.IsInitialized) {
                return;
            }

            string resolvedMapName = string.IsNullOrWhiteSpace(mapName)
                ? mapController.MapName
                : RoRuntimeMapController.NormalizeMapResourceName(mapName);
            RoClassicMapBanner.ShowForMap(mapController.Content.Assets, resolvedMapName, forceRestart: true);
        }

        private static bool IsSameRuntimeMapEntryUpdate(RoLoginEnterMapResult previous, RoLoginEnterMapResult next) {
            if (previous == null ||
                next == null ||
                previous.MapSession == null ||
                !ReferenceEquals(previous.MapSession, next.MapSession)) {
                return false;
            }

            return AreComparableMapNamesEqual(
                previous.CharacterResult?.MapHandoff.MapName,
                next.CharacterResult?.MapHandoff.MapName);
        }

        private static bool AreComparableMapNamesEqual(string left, string right) {
            return !string.IsNullOrWhiteSpace(left) &&
                !string.IsNullOrWhiteSpace(right) &&
                string.Equals(
                    NormalizeComparableMapName(left),
                    NormalizeComparableMapName(right),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeComparableMapName(string mapName) {
            string normalized = (mapName ?? string.Empty).Trim().Replace('\\', '/');
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0) {
                normalized = normalized.Substring(slashIndex + 1);
            }

            if (normalized.EndsWith(".gat", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase)) {
                normalized = normalized.Substring(0, normalized.Length - 4);
            }

            return normalized;
        }

        private static bool IsClearedLiveDrainHistory(RoLoginShellSnapshot snapshot) {
            return snapshot != null &&
                snapshot.LiveDrainSequence > 0 &&
                (snapshot.RecentLiveDrains == null || snapshot.RecentLiveDrains.Length == 0) &&
                IsEmptyDrain(snapshot.LatestLiveDrain);
        }

        private static bool IsEmptyDrain(RoMapEntryDrainResult drain) {
            return drain == null ||
                (drain.PlayerChats.Length == 0 &&
                 drain.TimeNotifications.Length == 0 &&
                 drain.ActorEntries.Length == 0 &&
                 drain.ActorMoves.Length == 0 &&
                 drain.PlayerMoves.Length == 0 &&
                 drain.StopMoves.Length == 0 &&
                 drain.ActorVanishes.Length == 0 &&
                 drain.OrderedEvents.Length == 0);
        }

        private void SyncLocalPlayerToMapEntry(
            RoLoginEnterMapResult mapResult,
            RoRuntimeMapController mapController,
            bool applyAppearance = true
        ) {
            if (mapResult == null) {
                return;
            }

            RoPacketPosition position = mapResult.AcceptedEnter.Position;
            mapController ??= FindFirstObjectByType<RoRuntimeMapController>();
            if (mapController != null) {
                if (applyAppearance) {
                    mapController.TryApplyLocalPlayerAppearance(mapResult.CharacterResult?.SelectedCharacter);
                }

                RoOnlineWorldController controller = onlineWorldController != null ? onlineWorldController : EnsureOnlineWorldController();
                RoActorDirection? facing = ResolveLocalPlayerMapEntryFacing(controller, mapResult);
                uint placementServerTime = mapResult.AcceptedEnter.StartTime >= 0
                    ? unchecked((uint) mapResult.AcceptedEnter.StartTime)
                    : 0;
                if (mapController.TryMoveLocalPlayerToCell(position.X, position.Y, facing, placementServerTime)) {
                    return;
                }
            }

#if UNITY_EDITOR
            RoMapSmokeTest smokeTest = FindFirstObjectByType<RoMapSmokeTest>();
            if (smokeTest != null && smokeTest.TryMovePlayerToCell(position.X, position.Y)) {
                return;
            }
#endif

            Debug.LogWarning($"Local player visual could not be moved to online map-entry cell {position.X},{position.Y}.");
        }

        private static RoActorDirection? ResolveLocalPlayerMapEntryFacing(
            RoOnlineWorldController controller,
            RoLoginEnterMapResult mapResult
        ) {
            RoActorDirection? worldFacing = ResolveLocalPlayerFacingFromWorld(controller);
            if (worldFacing.HasValue) {
                return worldFacing;
            }

            if (mapResult == null) {
                return null;
            }

            int direction = RoOnlineWorldState.DirectionFromServerDirection(mapResult.AcceptedEnter.Position.Direction);
            return RoOnlineActorPresenter.DirectionFromPacket(direction);
        }

        private static RoActorDirection? ResolveLocalPlayerFacingFromWorld(RoOnlineWorldController controller) {
            if (controller != null &&
                controller.OwnActorId != 0 &&
                controller.State.TryGetActor(controller.OwnActorId, out RoOnlineActorSnapshot actor)) {
                return RoOnlineActorPresenter.ResolveFacingDirection(actor);
            }

            return null;
        }

        private static uint ResolveLocalPlayerPlacementServerTime(RoOnlineWorldController controller) {
            if (controller == null) {
                return 0;
            }

            if (controller.State.HasOwnMapEntryServerTime) {
                return controller.State.OwnMapEntryServerTimeMilliseconds;
            }

            return controller.State.HasServerTime ? controller.State.LatestServerTimeMilliseconds : 0;
        }

        private static void SyncLocalPlayerAppearanceFromOnlineWorld(
            RoOnlineWorldController controller,
            RoRuntimeMapController mapController = null
        ) {
            if (controller == null ||
                controller.OwnActorId == 0 ||
                !controller.State.TryGetActor(controller.OwnActorId, out RoOnlineActorSnapshot actor)) {
                return;
            }

            mapController ??= controller.RuntimeMapController;
            mapController ??= FindFirstObjectByType<RoRuntimeMapController>();
            if (mapController == null) {
                return;
            }

            mapController.TryApplyLocalPlayerAppearance(actor);
            mapController.TryApplyLocalPlayerMotion(actor);
        }

        private static void SyncLocalPlayerStatus(
            RoLoginShellSnapshot snapshot,
            RoRuntimeMapController mapController = null,
            RoOnlineWorldController controller = null
        ) {
            if (snapshot == null) {
                return;
            }

            mapController ??= FindFirstObjectByType<RoRuntimeMapController>();
            if (mapController == null) {
                return;
            }

            RoPlayerStatusSnapshot status = snapshot.Gameplay?.Status ?? RoPlayerStatusSnapshot.Empty;
            SyncOwnActorBaseLevel(controller, status, snapshot.MapResult?.CharacterResult?.SelectedCharacter);
            if (status.MaxHp > 0) {
                mapController.TryApplyLocalPlayerStatus(status);
                return;
            }

            RoCharacterInfo character = snapshot.MapResult?.CharacterResult?.SelectedCharacter;
            if (character != null) {
                mapController.TryApplyLocalPlayerStatus(character.Hp, character.MaxHp, character.Sp, character.MaxSp);
            }
        }

        private void SyncLocalPlayerStatusFromCurrentState(
            RoLoginShellSnapshot fullSnapshot,
            RoOnlineWorldController controller,
            RoRuntimeMapController mapController = null
        ) {
            if (fullSnapshot != null) {
                SyncLocalPlayerStatus(fullSnapshot, mapController, controller);
                return;
            }

            if (session == null) {
                return;
            }

            mapController ??= FindFirstObjectByType<RoRuntimeMapController>();
            if (mapController == null) {
                return;
            }

            RoShellBasicInfoSnapshot basicInfo = session.BasicInfoSnapshot();
            RoPlayerStatusSnapshot status = basicInfo.Status ?? RoPlayerStatusSnapshot.Empty;
            SyncOwnActorBaseLevel(controller, status, basicInfo.HasCharacter ? basicInfo.Character : default);
            if (status.MaxHp > 0) {
                mapController.TryApplyLocalPlayerStatus(status);
                return;
            }

            if (basicInfo.HasCharacter) {
                RoLoginShellCharacter character = basicInfo.Character;
                mapController.TryApplyLocalPlayerStatus(character.Hp, character.MaxHp, character.Sp, character.MaxSp);
            }
        }

        private static void SyncOwnActorBaseLevel(
            RoOnlineWorldController controller,
            RoPlayerStatusSnapshot status,
            RoCharacterInfo character
        ) {
            int baseLevel = status != null && status.BaseLevel > 0 ? status.BaseLevel : character?.BaseLevel ?? 0;
            if (baseLevel > 0) {
                controller?.ApplyOwnActorBaseLevel(baseLevel);
            }
        }

        private static void SyncOwnActorBaseLevel(
            RoOnlineWorldController controller,
            RoPlayerStatusSnapshot status,
            RoLoginShellCharacter character
        ) {
            int baseLevel = status != null && status.BaseLevel > 0 ? status.BaseLevel : character.BaseLevel;
            if (baseLevel > 0) {
                controller?.ApplyOwnActorBaseLevel(baseLevel);
            }
        }


        private void EnsurePhase12GameplayWindows(bool bindExisting = true) {
            try {
                RoOnlineWorldController controller = ResolveOnlineWorldController();
                RoRuntimeMapController activeMapController = ResolveActiveRuntimeMapController(controller);
                BindRuntimeMapController(controller, activeMapController);
                IRoRodexCommandExecutor legacyRodexCommands = CreateLegacyRodexCommandExecutor();
                // the shortcut bar 0x24 follows the session of every entry; the first pass binds it in any case
                if (bindExisting || !RoClassicHotkeyBar.IsBound) {
                    RoClassicHotkeyBar.Bind(session, clientSettings);
                }

                // the HUD minimap 0xe follows the runtime map of every entry; the first pass binds it in any case
                if (bindExisting || !RoClassicMinimap.IsBound) {
                    RoClassicMinimap.Bind(session, controller, activeMapController);
                }

            } catch (Exception ex) {
                Debug.LogWarning($"Phase 12 gameplay UI failed to initialize: {ex.Message}");
            }
        }

        private IRoInventoryCommandExecutor CreateLegacyInventoryCommandExecutor() {
            return session != null ? new RoLegacyInventoryCommandExecutor(session) : null;
        }

        private IRoSocialEconomyCommandExecutor CreateLegacySocialEconomyCommandExecutor() {
            return session != null ? new RoLegacySocialEconomyCommandExecutor(session) : null;
        }

        private IRoRodexCommandExecutor CreateLegacyRodexCommandExecutor() {
            return session != null ? new RoLegacyRodexCommandExecutor(session) : null;
        }


        // Phase 2D: ToggleGameplayWindow / TryCloseGameplayWindow / RefreshGameplayWindow
        // were removed; they only existed to serve the BasicInfoV4 menu dispatcher, which
        // is now owned by each IRoBasicInfoIcon's Toggle body via IRoBasicInfoIconHost.

        private void RefreshGameplayWindowsFromSequences() {
            if (session == null) {
                return;
            }

            int inventoryReloadSequence = session.InventoryReloadSequence;
            if (inventoryReloadSequence != lastScheduledInventoryReloadSequence) {
                // A wholesale inventory (re)load (login / character change / map re-entry) must not be
                // mistaken for obtained items — re-baseline the detector instead of diffing it as gains.
                itemObtainDetector.ObserveInventoryReload();
                lastScheduledInventoryReloadSequence = inventoryReloadSequence;
            }

            int inventorySequence = session.InventoryWindowChangeSequence;
            if (inventorySequence != lastScheduledInventorySequence) {
                itemObtainDetector.ObserveInventorySequence(inventorySequence);
                lastScheduledInventorySequence = inventorySequence;
            }

            // both lists live in the session's gameplay state (every change bumps its sequence) or close by a local dismiss:
            // re-read only then, not every frame (each read took an inventory snapshot)
            int selectionSequence = session.GameplayChangeSequence;
            if (selectionSequence != lastSelectionWindowsSequence ||
                itemIdentifyDismissedSequence != lastSelectionIdentifyDismissed ||
                makingArrowDismissedSequence != lastSelectionArrowDismissed) {
                lastSelectionWindowsSequence = selectionSequence;
                lastSelectionIdentifyDismissed = itemIdentifyDismissedSequence;
                lastSelectionArrowDismissed = makingArrowDismissedSequence;
                SyncItemIdentifySelectionWindow();
                SyncMakingArrowSelectionWindow();
            }
            SyncTeleportMenuWindow();

            int functionalSequence = session.FunctionalWindowChangeSequence;
            if (functionalSequence == lastScheduledFunctionalSequence) {
                return;
            }

            RoFunctionalWindowChange functionalChanges = session.LastFunctionalWindowChanges;
            if (functionalChanges == RoFunctionalWindowChange.None) {
                functionalChanges = RoFunctionalWindowChange.All;
            }

            if (HasFunctionalChange(functionalChanges, RoFunctionalWindowChange.Vending)) {
                SyncVendingWindows();
            }

            lastScheduledFunctionalSequence = functionalSequence;
        }

        /// <summary>
        /// The vending windows follow the server's vending state (RoClassicVending: the shop setup, the own shop, the
        /// shops being browsed) and so do the shop title boards over the merchants (RoClassicActorBoards).
        /// </summary>
        private void SyncVendingWindows() {
            RoLoginShellSession current = session;
            if (!IsMapEnteredSession(current)) {
                RoClassicVending.HideSetup();
                RoClassicVending.HideOwnShop();
                HideClassicCustomerShops(null);
                RoClassicActorBoards.Clear();
                return;
            }

            RoVendingSnapshot vending = current.VendingSnapshot();
            RoItemMetadataTable metadata = ResolveGameplayItemMetadata();
            IRoSocialEconomyCommandExecutor commands = CreateLegacySocialEconomyCommandExecutor();
            if (vending.SetupAllowed) {
                RoClassicVending.ShowSetup(current, commands, metadata, vending);
            } else {
                RoClassicVending.HideSetup();
            }

            if (vending.IsOwnStoreOpen) {
                RoClassicVending.ShowOwnShop(current, commands, metadata, vending);
            } else {
                RoClassicVending.HideOwnShop();
            }

            RoPlayerShopSnapshot[] shops = vending.PlayerShops ?? Array.Empty<RoPlayerShopSnapshot>();
            for (int i = 0; i < shops.Length; i++) {
                if (shops[i].OwnerAid == 0 || shops[i].OwnerAid == current.LocalActorId) {
                    continue;
                }

                RoClassicVending.ShowCustomerShop(current, commands, metadata, shops[i]);
                if (!shownCustomerShops.Contains(shops[i].OwnerAid)) {
                    shownCustomerShops.Add(shops[i].OwnerAid);
                }
            }

            HideClassicCustomerShops(shops);
            RoClassicActorBoards.Sync(current, commands, ResolveOnlineWorldController());
        }

        /// <summary>Closes the browsed shops no longer in the vending state (all of them for null).</summary>
        private void HideClassicCustomerShops(RoPlayerShopSnapshot[] open) {
            for (int i = shownCustomerShops.Count - 1; i >= 0; i--) {
                uint ownerAid = shownCustomerShops[i];
                bool stillOpen = false;
                for (int j = 0; open != null && j < open.Length; j++) {
                    stillOpen |= open[j].OwnerAid == ownerAid;
                }

                if (!stillOpen) {
                    RoClassicVending.HideCustomerShop(ownerAid);
                    shownCustomerShops.RemoveAt(i);
                }
            }
        }

        /// <summary>UIMakingArrowListWnd 0x5e (RoClassicItemSelect) follows the arrow-crafting list (0x01ad: item ids only).</summary>
        private void SyncMakingArrowSelectionWindow() {
            RoLoginShellSession current = session;
            RoMakingArrowSnapshot makingArrow = IsMapEnteredSession(current) ? current.InventorySnapshot().MakingArrow : null;
            if (makingArrow == null ||
                !makingArrow.IsActive ||
                makingArrow.ItemIds == null ||
                makingArrow.ItemIds.Length == 0 ||
                makingArrow.Sequence == makingArrowDismissedSequence) {
                RoClassicItemSelect.Hide(RoClassicItemSelect.MakingArrow);
                return;
            }

            var items = new List<RoItemInstance>(makingArrow.ItemIds.Length);
            foreach (uint id in makingArrow.ItemIds) {
                items.Add(RoItemInstance.CreateRegular(0, unchecked((int) id), 1));
            }

            int sequence = makingArrow.Sequence;
            RoClassicItemSelect.Show(RoClassicItemSelect.MakingArrow, sequence, items, ResolveGameplayItemMetadata(),
                picked => _ = MakeArrowAsync(current, unchecked((uint) picked.ItemId)),
                () => {
                    makingArrowDismissedSequence = sequence;
                    _ = MakeArrowAsync(current, uint.MaxValue);
                });
        }

        /// <summary>CZ_REQ_MAKINGARROW with the picked material; uint.MaxValue closes the list.</summary>
        private static async Task MakeArrowAsync(RoLoginShellSession current, uint itemId) {
            try {
                await current.BeginLegacyMakeArrow(itemId);
            } catch (Exception ex) {
                Debug.LogWarning("Arrow crafting request was not accepted: " + ex.Message);
                if (itemId == uint.MaxValue) {
                    current.ClearLegacyMakingArrowSelection();
                }
            }
        }

        /// <summary>UIItemIdentifyWnd 0x49 (RoClassicItemSelect) follows the appraisal list (bag indexes into the inventory).</summary>
        private void SyncItemIdentifySelectionWindow() {
            RoLoginShellSession current = session;
            RoGameplayInventorySnapshot inventory = IsMapEnteredSession(current) ? current.InventorySnapshot() : null;
            RoItemIdentifySnapshot identify = inventory?.ItemIdentify;
            if (identify == null ||
                !identify.IsActive ||
                identify.BagIndexes == null ||
                identify.BagIndexes.Length == 0 ||
                identify.Sequence == itemIdentifyDismissedSequence) {
                RoClassicItemSelect.Hide(RoClassicItemSelect.Identify);
                return;
            }

            RoItemInstance[] inventoryItems = inventory.InventoryItems ?? Array.Empty<RoItemInstance>();
            var items = new List<RoItemInstance>(identify.BagIndexes.Length);
            for (int i = 0; i < identify.BagIndexes.Length; i++) {
                for (int j = 0; j < inventoryItems.Length; j++) {
                    if (inventoryItems[j] != null && inventoryItems[j].BagIndex == identify.BagIndexes[i]) {
                        items.Add(inventoryItems[j]);
                        break;
                    }
                }
            }

            int sequence = identify.Sequence;
            RoClassicItemSelect.Show(RoClassicItemSelect.Identify, sequence, items, ResolveGameplayItemMetadata(),
                picked => _ = IdentifyItemAsync(current, picked.BagIndex),
                () => {
                    itemIdentifyDismissedSequence = sequence;
                    _ = CancelIdentifyItemAsync(current);
                });
        }

        private static async Task IdentifyItemAsync(RoLoginShellSession current, int bagIndex) {
            try {
                await current.BeginLegacyIdentifyItem(bagIndex);
            } catch (Exception ex) {
                Debug.LogWarning("Item appraisal request was not accepted: " + ex.Message);
            }
        }

        private static async Task CancelIdentifyItemAsync(RoLoginShellSession current) {
            try {
                await current.BeginLegacyCancelIdentifyItem();
            } catch (Exception ex) {
                Debug.LogWarning("Item appraisal close request was not accepted: " + ex.Message);
                current.ClearLegacyItemIdentifySelection();
            }
        }

        /// <summary>UIChooseWarpWnd 0x27 (RoClassicWarpList) follows the teleport / warp portal menu.</summary>
        private void SyncTeleportMenuWindow() {
            RoLoginShellSession current = session;
            RoTeleportMenuSnapshot teleport = IsMapEnteredSession(current) ? current.FunctionalSnapshot().TeleportMenu : null;
            RoClassicWarpList.Sync(current, ResolveGameplayUiAssetDatabase(), teleport);
        }

        private static bool IsMapEnteredSession(RoLoginShellSession current) {
            return current != null && current.CommandStateSnapshot().Stage == RoLoginShellStage.MapEntered;
        }

        /// <summary>The session while the gameplay UI is up (RoClassicGameUi).</summary>
        internal RoLoginShellSession ClassicSession => session;

        /// <summary>The chat window the classic windows print to; null before the map entry and after leaving the map.</summary>
        internal RoClassicChat ClassicChat => chatWindow;

        /// <summary>The item table of the map's content, looked up again per map (and twice a second while missing).</summary>
        internal RoItemMetadataTable GameplayItemMetadata {
            get {
                if (cachedGameplayItemMetadata == null ||
                    !ReferenceEquals(cachedGameplayItemMetadataMap, appliedWorldMapResult) ||
                    (ReferenceEquals(cachedGameplayItemMetadata, RoItemMetadataTable.Empty) && Time.unscaledTime >= nextGameplayItemMetadataLookup)) {
                    cachedGameplayItemMetadata = ResolveGameplayItemMetadata();
                    cachedGameplayItemMetadataMap = appliedWorldMapResult;
                    nextGameplayItemMetadataLookup = Time.unscaledTime + 0.5f;
                }

                return cachedGameplayItemMetadata;
            }
        }

        private RoItemMetadataTable ResolveGameplayItemMetadata() {
            RoAssetDatabase assets = ResolveGameplayUiAssetDatabase();
            return assets != null ? assets.GameData?.Items ?? RoItemMetadataTable.Empty : RoItemMetadataTable.Empty;
        }

        private RoAssetDatabase ResolveGameplayUiAssetDatabase() {
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            RoRuntimeMapController activeMapController = ResolveActiveRuntimeMapController(controller);
            RoAssetDatabase assets = activeMapController != null ? activeMapController.Content?.Assets : null;
            if (assets != null && assets.IsInitialized) {
                return assets;
            }

            assets = browserContent != null ? browserContent.Assets : null;
            return assets != null && assets.IsInitialized ? assets : null;
        }


        private static bool HasFunctionalChange(RoFunctionalWindowChange changed, RoFunctionalWindowChange windowChange) {
            return (changed & windowChange) != 0;
        }

        private void EnsureGameplayContextWindows(RoLoginShellSnapshot snapshot) {
            if (session == null) {
                return;
            }

            if (snapshot == null ||
                snapshot.Stage != RoLoginShellStage.MapEntered ||
                snapshot.MapResult == null) {
                return;
            }

            // the NPC dialog and shop are classic windows ticked by RoClassicGameUi (RoClassicNpcDialog, RoClassicNpcShop)
            EnsureNpcTargetRouterBound();
        }

        private void EnsureGameplayContextWindowsFromCurrentState(
            RoMapEntryDrainResult latestLiveDrain,
            IReadOnlyList<RoLiveDrainSnapshot> recentLiveDrains
        ) {
            if (session == null ||
                !session.IsStableAppliedMapResult(appliedWorldMapResult)) {
                return;
            }

            EnsureNpcTargetRouterBound();
        }

        private bool ShouldKeepPlayableGameplayWindows() {
            if (session == null) {
                return false;
            }

            RoShellMapHudSnapshot snapshot = session.MapHudSnapshot();
            return snapshot.Stage == RoLoginShellStage.MapEntered &&
                snapshot.HasMapEntryPosition;
        }

        private bool ShouldKeepPlayableGameplayWindows(RoLoginShellSnapshot snapshot) {
            return snapshot != null &&
                snapshot.Stage == RoLoginShellStage.MapEntered &&
                snapshot.MapResult != null;
        }

        private bool HasGameplayWindowReferences() {
            return chatWindow != null ||
                RoClassicHotkeyBar.IsBound;
        }

        private void ClearGameplayWindows() {
            RoClassicStatus.Close();
            RoClassicSkillTree.Close();
            RoClassicPartyFriends.Close();
            RoClassicInventory.Close();
            RoClassicEquipment.Close();
            RoClassicMinimap.Close();
            RoClassicHotkeyBar.Close();
            RoClassicGameSettings.Close();
            RoClassicShortcutSettings.Close();
            RoClassicCart.Close();
            RoClassicStorage.Close();
            RoClassicTrade.Close();
            RoClassicNpcMarket.Close();
            RoClassicNpcDialog.Close();
            RoClassicNpcShop.Close();
            RoClassicVending.HideSetup();
            RoClassicVending.HideOwnShop();
            HideClassicCustomerShops(null);
            RoClassicActorBoards.Clear();
            RoClassicChat.Close();
            UnbindNpcTargetRouter();
            chatWindow = null;
            playableLegacyOverlaysSuppressed = false;
            lastScheduledInventorySequence = int.MinValue;
            lastScheduledInventoryReloadSequence = int.MinValue;
            lastScheduledFunctionalSequence = int.MinValue;
            HideGameModeWindows();
        }

        /// <summary>
        /// Leaving the game mode the client deletes every window of it. The closes above cover the windows with a state of their
        /// own; once the chat is unbound the gameplay ticks stop, so anything else still up (the quest overlay, the mail icon,
        /// the achievement windows...) would stay frozen on the character select. The producers show theirs again in the map.
        /// </summary>
        private static void HideGameModeWindows() {
            RoClassicMapBanner.Hide();
            Ragnarok.UI.Classic.RoClassicUiSystem ui = Ragnarok.UI.Classic.RoClassicUiSystem.Instance;
            if (ui == null) return;
            IReadOnlyList<Ragnarok.UI.Classic.RoClassicWindow> windows = ui.Windows.Windows;
            for (int i = 0; i < windows.Count; i++) {
                Ragnarok.UI.Classic.RoClassicWindow w = windows[i];
                if (!w.Visible || RoClassicPreGame.OwnsWindow(w) || w.Layout?.Class == "UIMessageBox") continue;
                w.Visible = false;
            }
        }

        private void CloseOptionalGameplayWindowsForFreshEntry() {
            RoClassicSkillTree.Close();
            RoClassicPartyFriends.Close();
            RoClassicInventory.Close();
            RoClassicEquipment.Close();
            RoClassicCart.Close();
            RoClassicStorage.Close();
            RoClassicTrade.Close();
            RoClassicNpcMarket.Close();
            RoClassicNpcDialog.Close();
            RoClassicNpcShop.Close();
            RoClassicGameSettings.Close();
            RoClassicShortcutSettings.Close();
        }

        private void AppendMapResultChat(RoLoginEnterMapResult mapResult) {
            if (mapResult == null) {
                return;
            }

            TryAppendDrainChat(mapResult.FollowUp);
            TryAppendDrainChat(mapResult.InitialState);
            TryAppendDrainChat(mapResult.MoveResponse);
        }

        private bool TryAppendDrainChat(RoMapEntryDrainResult drain) {
            if (drain == null) {
                return true;
            }

            bool hasChat = DrainHasChat(drain);
            if (!hasChat) {
                return true;
            }

            RoClassicChat targetChatWindow = ResolveChatWindow();
            if (targetChatWindow == null) {
                Debug.LogWarning("Live chat packet was received, but no chat window was available to render it.");
                return false;
            }

            bool appendedFromOrderedEvents = false;
            // in a chat room the client writes the room's lines only into the room window (UIChatRoomWnd), not the main chat
            bool roomTakesChat = RoClassicChatRoom.RoomOpen;
            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                if (events[i].Kind == RoMapEntryDrainEventKind.PlayerChat) {
                    appendedFromOrderedEvents = true;
                    if (roomTakesChat) continue;
                    // /notalkmsg, /notalkmsg2 (0xc55360): other players' public chat is dropped; the own line (0x8e) never
                    if (events[i].PlayerChat.SourceActorId != 0 && RoChatSlashCommands.HidesPublicChat()) continue;
                    targetChatWindow.AddLine(events[i].PlayerChat.Message, "#f0eadb", Ragnarok.Network.RoChatChannel.Main);
                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.NpcChat) {
                    RoClassicChat.AutoSaveNpcChatLine(events[i].NpcChat.Message);   // /autosavechat logs 0x2c1 too (0xc5b340)
                    targetChatWindow.AddLine(events[i].NpcChat.Message, ResolveNpcChatColor(events[i].NpcChat), Ragnarok.Network.RoChatChannel.System);
                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.SystemText) {
                    // ZC_BROADCAST / ZC_BROADCAST2 write their chat line with the box (RoClassicBroadcast: the form's type)
                    ushort source = events[i].SystemText.PacketId;
                    if (source != RoPacketIds.ZcBroadcast && source != RoPacketIds.ZcBroadcast2) {
                        targetChatWindow.AddLine(events[i].SystemText.Message, ResolveSystemTextColor(events[i].SystemText), Ragnarok.Network.RoChatChannel.System);
                    }

                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.Message) {
                    if (events[i].Message.HasSkillId) {
                        // ZC_MSG_SKILL (0xc30243): "[skill] message" in #CDCDFF, type 11 "Skill failure"
                        RoLocalizationService localization = ResolveChatLocalization();
                        targetChatWindow.AddTypedLine(
                            RoChatLineTypes.SkillMessageLine(ResolveServerMessageSkillName(localization, events[i].Message.SkillId), ResolveServerMessageText(events[i].Message, false)),
                            RoChatLineTypes.SkillMsg, RoChatLineTypes.SkillFailure);
                    } else {
                        targetChatWindow.AddLine(ResolveServerMessageText(events[i].Message), ResolveMessageColor(events[i].Message), Ragnarok.Network.RoChatChannel.System);
                    }

                    appendedFromOrderedEvents = true;
                } else if (RoClassicSystemLines.IsChatEvent(events[i])) {
                    // the typed system lines: ammunition / weight failures, equipment on / off (7), weight (8), party setup
                    // (12), quests (19), the ranking names
                    RoClassicSystemLines.OnDrainEvent(targetChatWindow, session, events[i]);
                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.ItemPickupAck) {
                    RoZcItemPickupAck pickup = events[i].ItemPickupAck;
                    if (RoChatLineTypes.PickupObtained(pickup.Result)) {
                        // "You got %s (%d)." in cyan, type 6 (0xc52846)
                        string message = ResolveItemPickupMessage(pickup);
                        if (!string.IsNullOrEmpty(message)) {
                            targetChatWindow.AddLine(message, "#00ffff", Ragnarok.Network.RoChatChannel.Item);
                            appendedFromOrderedEvents = true;
                        }
                    } else {
                        // FUN_00c50c10: mgr msg 3, the result's MsgStr in red, type 0 (no line for an unknown result)
                        string failure = RoChatLineTypes.PickupFailureLine(pickup.Result, (id, fallback) => RoClassicUiHost.MsgStr(id, fallback));
                        if (failure != null) {
                            targetChatWindow.AddTypedLine(failure, RoChatLineTypes.Red, RoChatLineTypes.Public, popOpen: true);
                            appendedFromOrderedEvents = true;
                        }
                    }
                } else if (events[i].Kind == RoMapEntryDrainEventKind.ItemIdentifyAck) {
                    string message = ResolveItemIdentifyMessage(events[i].ItemIdentifyAck);
                    if (!string.IsNullOrEmpty(message)) {
                        // ZC_ACK_ITEMIDENTIFY (0xc32596): type 0, cyan on success, red on failure (not an item-get line)
                        targetChatWindow.AddLine(
                            message,
                            events[i].ItemIdentifyAck.IsSuccess ? "#00ffff" : "#ff0000",
                            Ragnarok.Network.RoChatChannel.System);
                        appendedFromOrderedEvents = true;
                    }
                } else if (events[i].Kind == RoMapEntryDrainEventKind.BattlefieldChat) {
                    // 0xc43e30: the server's "Name : text" verbatim in RGB(255, 0, 155), the public tab (not with /nm, /nm2)
                    if (RoChatSlashCommands.HidesPublicChat()) continue;
                    targetChatWindow.AddLine(events[i].BattlefieldChat.Message, "#FF009B", Ragnarok.Network.RoChatChannel.Main);
                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.PartyChat) {
                    targetChatWindow.AddLine(events[i].PartyChat.Message, "#e6d7c8", Ragnarok.Network.RoChatChannel.Party);
                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.GuildChat) {
                    // /notalkmsg2 (0xc6e4f0) drops guild chat
                    if (RoChatSlashCommands.HidesGuildChat()) continue;
                    targetChatWindow.AddLine(events[i].GuildChat.Message, "#b4ffb4", Ragnarok.Network.RoChatChannel.Guild);
                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.ClanChat) {
                    // /notalkmsg2 (0xc861a0) drops clan chat
                    if (RoChatSlashCommands.HidesGuildChat()) continue;
                    targetChatWindow.AddLine(events[i].ClanChat.Message, "#b5deef", Ragnarok.Network.RoChatChannel.Clan);
                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.Whisper) {
                    HandleIncomingWhisper(events[i].Whisper, targetChatWindow);
                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.WhisperAck) {
                    HandleWhisperAck(events[i].WhisperAck, targetChatWindow);
                    appendedFromOrderedEvents = true;
                } else if (events[i].Kind == RoMapEntryDrainEventKind.SkillUseAck) {
                    if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"fail skill={events[i].SkillUseAck.SkillId} cause={events[i].SkillUseAck.CauseRaw} result={events[i].SkillUseAck.ResultRaw} success={events[i].SkillUseAck.IsSuccess}");
                    RoLocalizationService localization = ResolveChatLocalization();
                    string message = RoOnlineWorldController.BuildSkillFailureChatMessage(
                        events[i].SkillUseAck,
                        skillId => ResolveServerMessageSkillName(localization, skillId));
                    // /skillfail off (0x01552d4c, read by the skill failure handlers 0xc40e7c..) shows no failure line
                    if (!string.IsNullOrEmpty(message) && RoChatSlashCommands.GetOption("/skillfail", null)) {
                        // chat type 11 "Skill failure": only the tabs with that flag (not every tab like a red type-0 line)
                        targetChatWindow.AddFlaggedLine(message, "#ff0000", 11);
                        appendedFromOrderedEvents = true;
                    }
                } else if (events[i].Kind == RoMapEntryDrainEventKind.TeleportResult) {
                    string message = ResolveTeleportResultMessage(events[i].TeleportResult);
                    if (!string.IsNullOrEmpty(message)) {
                        targetChatWindow.AddLine(message, "#ff0000", Ragnarok.Network.RoChatChannel.System);
                        appendedFromOrderedEvents = true;
                    }
                } else if (TryResolveVendingChatMessage(events[i], out string message, out string color)) {
                    targetChatWindow.AddLine(message, color, Ragnarok.Network.RoChatChannel.System);
                    appendedFromOrderedEvents = true;
                } else if (IsLoginOutEvent(events[i])) {
                    TryAppendLoginOutLine(events[i], targetChatWindow);   // /loginout (RoMinimalLoginShell.ChatOptions.cs)
                }
            }

            if (appendedFromOrderedEvents) {
                return true;
            }

            RoZcNotifyPlayerChat[] playerChats = drain.PlayerChats;
            for (int i = 0; i < playerChats.Length && !roomTakesChat; i++) {
                if (playerChats[i].SourceActorId != 0 && RoChatSlashCommands.HidesPublicChat()) continue;
                targetChatWindow.AddLine(playerChats[i].Message, "#f0eadb", Ragnarok.Network.RoChatChannel.Main);
            }

            return true;
        }

        private static bool TryResolveVendingChatMessage(RoMapEntryDrainEvent drainEvent, out string message, out string color) {
            message = string.Empty;
            color = "#00ffff";
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.VendingSetupAllowed:
                    message = "Vending setup allowed for " + drainEvent.VendingOpenStore.SlotCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + " slot(s).";
                    return true;
                case RoMapEntryDrainEventKind.VendingOpenAccepted:
                    message = "Your vending store is open.";
                    return true;
                case RoMapEntryDrainEventKind.VendingOpenAck:
                    if (drainEvent.VendingOpenAck.IsSuccess) {
                        message = "Vending store accepted.";
                    } else {
                        message = "Vending store failed to open (" + drainEvent.VendingOpenAck.ResultRaw.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").";
                        color = "#ff0000";
                    }

                    return true;
                case RoMapEntryDrainEventKind.PlayerShopPurchaseResult:
                    message = RoVendingRuntime.FormatPurchaseResult(drainEvent.VendingPurchaseResult.ResultRaw);
                    color = drainEvent.VendingPurchaseResult.IsSuccess ? "#00ffff" : "#ff0000";
                    return true;
                case RoMapEntryDrainEventKind.VendingSaleReported:
                    if (drainEvent.VendingSaleReport.HasBuyerDetails) {
                        message =
                            "Sold " +
                            drainEvent.VendingSaleReport.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                            " item(s) for " +
                            drainEvent.VendingSaleReport.Zeny.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                            " zeny.";
                    } else {
                        message = "Sold " + drainEvent.VendingSaleReport.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture) + " item(s).";
                    }

                    return true;
                default:
                    return false;
            }
        }

        private static string ResolveItemIdentifyMessage(RoZcItemIdentifyAck ack) {
            if (ack.BagIndex < 0) {
                return string.Empty;
            }

            return ack.IsSuccess
                ? "Item appraisal has completed successfully."
                : "Item appraisal has failed.";
        }

        private string ResolveItemPickupMessage(RoZcItemPickupAck ack) {
            RoLocalizationService localization = ResolveChatLocalization();
            if (!RoChatLineTypes.PickupObtained(ack.Result)) {
                return localization.TryGetSystemMessage("53", out RoLocalizedString failure) &&
                    !string.IsNullOrWhiteSpace(failure.Text)
                    ? failure.Text
                    : "Failed to pick up item.";
            }

            string text = localization.TryGetSystemMessage("153", out RoLocalizedString success) &&
                !string.IsNullOrWhiteSpace(success.Text)
                ? success.Text
                : "%s - %d obtained.";
            string itemName = ResolveItemPickupDisplayName(localization, ack);
            string count = ack.Count.ToString(CultureInfo.InvariantCulture);
            if (!TryReplaceFirstAny(ref text, itemName, "%s", "%S")) {
                text = itemName + " " + text;
            }

            if (!TryReplaceFirstAny(ref text, count, "%d", "%u", "%ld", "%lld")) {
                text = text + " " + count;
            }

            return text;
        }

        private string ResolveItemPickupDisplayName(RoLocalizationService localization, RoZcItemPickupAck ack) {
            int itemId = unchecked((int) ack.ItemId);
            if (localization != null &&
                localization.TryGetItemName(itemId, ack.IsIdentified, out RoLocalizedString localized) &&
                !string.IsNullOrWhiteSpace(localized.Text)) {
                return localized.Text;
            }

            RoAssetDatabase assets = ResolveGameplayUiAssetDatabase();
            RoItemMetadataTable metadata = assets != null
                ? assets.GameData?.Items ?? RoItemMetadataTable.Empty
                : RoItemMetadataTable.Empty;
            if (metadata.TryGetItem(itemId, out RoItemMetadata item) && item != null) {
                return item.GetDisplayName(ack.IsIdentified);
            }

            return RoItemMetadataTable.Empty.GetItemOrFallback(itemId).GetDisplayName(ack.IsIdentified);
        }

        private string ResolveTeleportResultMessage(RoZcNotifyMapInfo result) {
            string key;
            string fallback;
            switch (result.Type) {
                case 0:
                    key = "500";
                    fallback = "Unable to Teleport in this area";
                    break;
                case 1:
                    key = "501";
                    fallback = "Saved point cannot be memorized.";
                    break;
                default:
                    return string.Empty;
            }

            RoLocalizationService localization = ResolveChatLocalization();
            return localization.TryGetSystemMessage(key, out RoLocalizedString localized) &&
                !string.IsNullOrWhiteSpace(localized.Text)
                ? localized.Text
                : fallback;
        }

        private RoClassicChat ResolveChatWindow() {
            if (chatWindow != null) {
                return chatWindow;
            }

            chatWindow = RoClassicChat.Bind(session);
            return chatWindow;
        }

        private string ResolveServerMessageText(RoZcMessage message, bool withSkillName = true) {
            RoLocalizationService localization = ResolveChatLocalization();
            string key = message.MessageId.ToString(CultureInfo.InvariantCulture);
            string text = localization.TryGetSystemMessage(key, out RoLocalizedString localized)
                ? localized.Text
                : "[system." + key + "]";

            if (message.HasSkillId && withSkillName) {
                string skillName = ResolveServerMessageSkillName(localization, message.SkillId);
                if (!TryReplaceFirstAny(ref text, skillName, "%s", "%S")) {
                    text = "[" + skillName + "] " + text;
                }
            }

            if (message.HasNumberValue) {
                string value = message.NumberValue.ToString(CultureInfo.InvariantCulture);
                if (!TryReplaceFirstAny(ref text, value, "%d", "%u", "%ld", "%lld")) {
                    text = text + " " + value;
                }
            }

            if (message.HasStringValue) {
                string value = message.StringValue ?? string.Empty;
                if (!TryReplaceFirstAny(ref text, value, "%s", "%S")) {
                    text = text + " " + value;
                }
            }

            return text;
        }

        private RoLocalizationService ResolveChatLocalization() {
            return TryGetBrowserContentAssets(out RoAssetDatabase assets) && assets != null
                ? assets.Localization ?? RoLocalizationService.Empty
                : RoLocalizationService.Empty;
        }

        private static string ResolveServerMessageSkillName(RoLocalizationService localization, ushort skillId) {
            return localization != null && localization.TryGetSkillName(skillId, out RoLocalizedString localized)
                ? localized.Text
                : skillId.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryReplaceFirstAny(ref string text, string value, params string[] tokens) {
            if (string.IsNullOrEmpty(text) || tokens == null || tokens.Length == 0) {
                return false;
            }

            int bestIndex = -1;
            string bestToken = null;
            for (int i = 0; i < tokens.Length; i++) {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token)) {
                    continue;
                }

                int index = text.IndexOf(token, StringComparison.Ordinal);
                if (index >= 0 && (bestIndex < 0 || index < bestIndex)) {
                    bestIndex = index;
                    bestToken = token;
                }
            }

            if (bestIndex < 0) {
                return false;
            }

            text = text.Substring(0, bestIndex) + (value ?? string.Empty) + text.Substring(bestIndex + bestToken.Length);
            return true;
        }

        private static string ResolveSystemTextColor(RoZcSystemText text) {
            return text.HasColor ? ColorRgbToHtml(text.ColorRgb) : "#b8e6a1";
        }

        private static string ResolveNpcChatColor(RoZcNpcChat chat) {
            // 0xc5b340: the packet's colour (server channels "[ #main ] Name : text" come this way); 0 = white (0x9d9b90)
            return chat.ColorBgr != 0u ? ColorRgbToHtml(BgrToRgb(chat.ColorBgr)) : "#ffffff";
        }

        private static string ResolveMessageColor(RoZcMessage message) {
            return message.HasColor ? ColorRgbToHtml(message.ColorRgb) : "#b8e6a1";
        }

        private static string ColorRgbToHtml(uint colorRgb) {
            return "#" +
                ((colorRgb >> 16) & 0xff).ToString("X2", CultureInfo.InvariantCulture) +
                ((colorRgb >> 8) & 0xff).ToString("X2", CultureInfo.InvariantCulture) +
                (colorRgb & 0xff).ToString("X2", CultureInfo.InvariantCulture);
        }

        private static uint BgrToRgb(uint colorBgr) {
            return ((colorBgr & 0x0000ffu) << 16) |
                (colorBgr & 0x00ff00u) |
                ((colorBgr & 0xff0000u) >> 16);
        }

        public void ShowPlayerContextMenu(Vector2 screenPosition, RoOnlineActorSnapshot actor) {
            if (session == null ||
                actor.ServerId == 0 ||
                actor.IsSelf ||
                actor.ObjectType != RoMapActorObjectType.Player) {
                return;
            }

            RoClassicPlayerMenu.Show(session, ResolveChatWindow(), actor);
        }

        private void OpenWhisperFromContext(string characterName) {
            if (string.IsNullOrWhiteSpace(characterName)) {
                return;
            }

            OpenWhisperWindow(characterName);
        }

        private void OpenWhisperWindow(string characterName) {
            if (string.IsNullOrWhiteSpace(characterName)) {
                return;
            }

            RoClassicWhisper.Bind(session, ResolveChatWindow());
            RoClassicWhisper.OpenManual(characterName, 0);
        }

        private void HandleIncomingWhisper(RoZcWhisper whisper, RoClassicChat fallbackChatWindow) {
            string sender = whisper.Sender != null ? whisper.Sender.Trim() : string.Empty;
            string message = CleanWhisperMessage(whisper.Message);
            if (sender.Length == 0 || message.Length == 0) {
                return;
            }

            HandleClassicIncomingWhisper(whisper, sender, message, fallbackChatWindow ?? ResolveChatWindow());
        }

        private void HandleWhisperAck(RoZcWhisperAck ack, RoClassicChat fallbackChatWindow) {
            if (session != null) {
                HandleClassicWhisperAck(ack, fallbackChatWindow ?? ResolveChatWindow());
            }
        }

        /// <summary>
        /// The incoming whisper (0xc3cb00) with the classic UI: "name : msg" to the partner's 1:1 window (auto-opened per
        /// the options, FUN_009bba10); without a window the main chat gets "[ Friend name (cc) ] : msg", "[ name (cc) ] : msg"
        /// (a GM), "[ Member name (cc) ] : msg" or "( From name [cc] ) : msg" in #FFFF00, the (cc) part only with an id.
        /// </summary>
        private void HandleClassicIncomingWhisper(RoZcWhisper whisper, string sender, string message, RoClassicChat chat) {
            RoClassicWhisper.Bind(session, chat);
            if (DeliverClassicWhisper(sender, sender + " : " + message, false, whisper.SenderActorId)) {
                TrySendAwayReply(sender, message);   // /automsg on both paths (0xc3ccb9, 0xc3da54)
                return;
            }

            TrySendAwayReply(sender, message);

            string cc = RoClassicWhisper.CCode(whisper.SenderActorId);
            string line;
            if (RoClassicWhisper.IsFriend(sender)) {
                line = "[ Friend " + sender + (cc.Length > 0 ? " (" + cc + ")" : string.Empty) + " ] : " + message;
            } else if (whisper.IsAdmin != 0) {
                line = "[ " + sender + (cc.Length > 0 ? " (" + cc + ")" : string.Empty) + " ] : " + message;
            } else if (RoClassicWhisper.IsGuildMember(sender, 0)) {
                line = "[ Member " + sender + (cc.Length > 0 ? " (" + cc + ")" : string.Empty) + " ] : " + message;
            } else {
                line = "( From " + sender + (cc.Length > 0 ? " [" + cc + "]" : string.Empty) + " ) : " + message;
            }

            if (whisper.IsAdmin != 0) {
                // 0xc3d864: a GM's whisper is type 24 (every tab), the sender kept with the line
                chat?.AddTypedLine(line, "#FFFF00", RoChatLineTypes.EveryTab, popOpen: true, name: sender);
            } else {
                chat?.AddLine(line, "#FFFF00", RoChatChannel.Whisper);
            }

            chat?.AddClassicWhisperName(sender);   // 0xc3d8af
        }

        /// <summary>
        /// ZC_ACK_WHISPER (0xc29c80) with the classic UI, for the last whisper sent: 0 echoes "me : text" to the partner's
        /// window (opening one per the options), else to the main chat as "[ To partner (cc) ] : text" (a friend or a guild
        /// member) or "( To partner (cc) : text"; 1 / 2 / 3 put "(partner)" + MsgStr 0x94 / 0x95 / 0x96 in the main chat only.
        /// </summary>
        private void HandleClassicWhisperAck(RoZcWhisperAck ack, RoClassicChat chat) {
            RoClassicWhisper.Bind(session, chat);
            // the answers to /hi's whispers (0 sent, 1 offline) print nothing while [0x01555ead] counts them
            if (RoClassicChat.TryConsumeHiAck(ack.Result)) return;
            string partner = session.LastWhisperTarget, text = session.LastWhisperText;
            if (ack.IsSuccess) {
                string me = session.SelectedCharacterName ?? string.Empty;
                if (DeliverClassicWhisper(partner, me + " : " + text, true, ack.Id)) {
                    return;
                }

                string cc = RoClassicWhisper.CCode(ack.Id);
                bool known = RoClassicWhisper.IsFriend(partner) || RoClassicWhisper.IsGuildMember(partner, 0);
                chat?.AddLine(known
                    ? "[ To " + partner + " (" + cc + ") ] : " + text
                    : "( To " + partner + " (" + cc + ") : " + text, "#FFFF00", RoChatChannel.Whisper);
                return;
            }

            string reason = ack.Result == 2
                ? RoClassicUiHost.MsgStr(0x95, " doesn't want to receive your messages.")
                : ack.Result == 3
                    ? RoClassicUiHost.MsgStr(0x96, " is not in the mood to talk with anyone.")
                    : RoClassicUiHost.MsgStr(0x94, "There is no such character name or the user is offline.");
            chat?.AddLine("(" + partner + ")" + reason, "#FFFF00", RoChatChannel.Whisper);
        }

        /// <summary>
        /// FUN_009bba10: a 1:1 window is opened for the name when the options say so (the friend / stranger switch; the
        /// alarm sound only when it is new), then the line goes to the name's window if there is one (true) - never to
        /// one of the own name.
        /// </summary>
        private bool DeliverClassicWhisper(string name, string line, bool own, uint id) {
            if (string.IsNullOrEmpty(name)) return false;
            bool isFriend = RoClassicWhisper.IsFriend(name);
            if (LoadFriendSetupPreference(isFriend ? "OpenFriend" : "OpenStranger", true)) {
                bool opened = RoClassicWhisper.Open(name, RoClassicWhisper.Tag(name, id), RoClassicWhisper.CCode(id));
                if (opened && ShouldPlayWhisperAlarm()) {
                    RoAudioEvents.PlayButtonSound();
                }
            }

            if (name == session?.SelectedCharacterName) return false;
            return RoClassicWhisper.AddLine(name, line, own);
        }

        private bool ShouldOpenWhisperWindow(string sender) {
            bool isFriend = HasFriendNamed(sender);
            return LoadFriendSetupPreference(isFriend ? "OpenFriend" : "OpenStranger", true);
        }

        private bool ShouldPlayWhisperAlarm() {
            return LoadFriendSetupPreference("Alarm", true);
        }

        private bool LoadFriendSetupPreference(string preferenceName, bool defaultValue) {
            // the Friend Setup options (UIFriendOptionWnd 0x44) of the character
            return RoClassicPartyFriends.LoadFriendSetupPreference(session, preferenceName, defaultValue);
        }

        private static string CleanWhisperMessage(string message) {
            if (string.IsNullOrWhiteSpace(message)) {
                return string.Empty;
            }

            string cleaned = message.Trim();
            int colorMarker = cleaned.IndexOf('|');
            if (colorMarker >= 0 && colorMarker + 2 < cleaned.Length && char.IsDigit(cleaned[colorMarker + 1]) && char.IsDigit(cleaned[colorMarker + 2])) {
                cleaned = cleaned.Remove(colorMarker, 3);
            }

            return cleaned.Trim();
        }

        private bool HasFriendNamed(string characterName) {
            if (session == null || string.IsNullOrWhiteSpace(characterName)) {
                return false;
            }

            RoFriendRuntimeSnapshot[] friends = session.FriendsSnapshot();
            for (int i = 0; i < friends.Length; i++) {
                if (string.Equals(friends[i].Name, characterName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        private bool IsLocalPartyLeader() {
            if (session == null) {
                return false;
            }

            RoPartyRuntimeSnapshot party = session.PartySnapshot();
            uint localActorId = session.LocalActorId;
            if (localActorId == 0 || (!party.IsInParty && (party.Members == null || party.Members.Length == 0))) {
                return false;
            }

            if (party.LeaderAccountId != 0) {
                return party.LeaderAccountId == localActorId;
            }

            RoPartyMemberRuntimeSnapshot[] members = party.Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>();
            for (int i = 0; i < members.Length; i++) {
                if (members[i].AccountId == localActorId) {
                    return members[i].Role == 0;
                }
            }

            return false;
        }

        private async Task RunContextSocialCommandAsync(Task<RoSocialCommandResult> command, string acceptedStatus) {
            if (command == null) {
                return;
            }

            try {
                RoSocialCommandResult result = await command;
                AppendContextCommandStatus(result.Accepted
                    ? acceptedStatus
                    : string.IsNullOrWhiteSpace(result.Message) ? result.Status.ToString() : result.Message);
            } catch (Exception ex) {
                AppendContextCommandStatus(ex.Message);
                Debug.LogWarning($"Player context command failed: {ex.Message}");
            }
        }

        private async Task RunContextEconomyCommandAsync(Task<RoEconomyCommandResult> command, string acceptedStatus) {
            if (command == null) {
                return;
            }

            try {
                RoEconomyCommandResult result = await command;
                AppendContextCommandStatus(result.Accepted
                    ? acceptedStatus
                    : string.IsNullOrWhiteSpace(result.Message) ? result.Status.ToString() : result.Message);
            } catch (Exception ex) {
                AppendContextCommandStatus(ex.Message);
                Debug.LogWarning($"Player context command failed: {ex.Message}");
            }
        }

        private void AppendContextCommandStatus(string message) {
            if (string.IsNullOrWhiteSpace(message)) {
                return;
            }

            RoClassicChat chat = ResolveChatWindow();
            chat?.AddSystemLine(message);
        }

        private void ApplyLiveDrain(RoLoginShellSnapshot snapshot) {
            if (snapshot == null) {
                return;
            }

            ApplyLiveDrain(
                snapshot.LiveDrainSequence,
                snapshot.LatestLiveDrain,
                snapshot.RecentLiveDrains,
                snapshot,
                snapshot.HasLiveDrainPublishedTickCount,
                snapshot.LiveDrainPublishedTickCount);
        }

        private void ApplyLiveDrain(RoShellLiveDrainSnapshot snapshot) {
            ApplyLiveDrain(
                snapshot.LiveDrainSequence,
                snapshot.LatestLiveDrain,
                snapshot.RecentLiveDrains,
                null,
                snapshot.HasLiveDrainPublishedTickCount,
                snapshot.LiveDrainPublishedTickCount);
        }

        private void ApplyLiveDrain(RoShellBufferedLiveDrainSnapshot snapshot) {
            ApplyLiveDrain(
                snapshot.LiveDrainSequence,
                snapshot.LatestLiveDrain,
                snapshot.RecentLiveDrains,
                null,
                snapshot.HasLiveDrainPublishedTickCount,
                snapshot.LiveDrainPublishedTickCount);
        }

        public void FlushGameplayLiveDrains() {
            if (session == null) {
                return;
            }

            RoShellBufferedLiveDrainSnapshot snapshot = session.LiveDrainSnapshot(liveDrainApplyBuffer);
            if (snapshot.LiveDrainSequence != appliedLiveDrainSequence) {
                ApplyLiveDrain(snapshot);
            }
        }

        private void ApplyLiveDrain(
            int liveDrainSequence,
            RoMapEntryDrainResult latestLiveDrain,
            IReadOnlyList<RoLiveDrainSnapshot> recentLiveDrains,
            RoLoginShellSnapshot fullSnapshot,
            bool hasLiveDrainPublishedTickCount,
            int liveDrainPublishedTickCount
        ) {
            long applyLiveDrainStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                using (ApplyLiveDrainMarker.Auto()) {
            if (liveDrainSequence < appliedLiveDrainSequence) {
                appliedLiveDrainSequence = 0;
                appendedLiveChatSequence = 0;
            }

            CaptureLiveDrainApplyDiagnostics(
                liveDrainSequence,
                hasLiveDrainPublishedTickCount,
                liveDrainPublishedTickCount);

            if (liveDrainSequence <= appliedLiveDrainSequence) {
                return;
            }

            bool appliedAny = false;
            bool appliedWorldDrain = false;
            bool appliedBasicInfoWorldDrain = false;
            bool hasSelectCartPacket = false;
            RoZcSelectCart selectCartPacket = default;
            RoOnlineWorldController controller = null;
            int appliedThisFrame = 0;
            int maxDrainsThisFrame = ResolveLiveDrainApplyBudget(
                liveDrainSequence,
                latestLiveDrain,
                recentLiveDrains,
                appliedLiveDrainSequence,
                liveDrainApplyBudgetPerFrame);
            IReadOnlyList<RoLiveDrainSnapshot> drains = recentLiveDrains ?? Array.Empty<RoLiveDrainSnapshot>();
            for (int i = 0; i < drains.Count; i++) {
                if (appliedThisFrame >= maxDrainsThisFrame) {
                    break;
                }

                RoLiveDrainSnapshot entry = drains[i];
                if (entry.Sequence <= appliedLiveDrainSequence) {
                    continue;
                }

                if (controller == null) {
                    controller = EnsureOnlineWorldController();
                }

                AppendLiveDrainChat(entry.Sequence, entry.Drain);
                if (TryGetSelectCart(entry.Drain, out RoZcSelectCart selectCart)) {
                    selectCartPacket = selectCart;
                    hasSelectCartPacket = true;
                }

                TryApplySameMapTransitionPreview(controller, entry.Drain);
                bool containsMapTransition = DrainContainsMapTransition(entry.Drain);
                if (!containsMapTransition) {
                    using (ApplyWorldDrainMarker.Auto()) {
                        TryApplyWorldDrain(controller, entry.Drain);
                    }
                    appliedWorldDrain = true;
                    appliedBasicInfoWorldDrain |= DrainAffectsBasicInfoWorldState(entry.Drain);
                }

                appliedLiveDrainSequence = entry.Sequence;
                appliedAny = true;
                appliedThisFrame++;
            }

            if (!appliedAny && appliedThisFrame < maxDrainsThisFrame && liveDrainSequence > appliedLiveDrainSequence) {
                RoMapEntryDrainResult drain = latestLiveDrain;
                CaptureLiveDrainOverflowFallbackDiagnostics(liveDrainSequence);
                if (drain != null) {
                    controller = EnsureOnlineWorldController();
                    AppendLiveDrainChat(liveDrainSequence, drain);
                    if (TryGetSelectCart(drain, out RoZcSelectCart selectCart)) {
                        selectCartPacket = selectCart;
                        hasSelectCartPacket = true;
                    }

                    TryApplySameMapTransitionPreview(controller, drain);
                    bool containsMapTransition = DrainContainsMapTransition(drain);
                    if (!containsMapTransition) {
                        using (ApplyWorldDrainMarker.Auto()) {
                            TryApplyWorldDrain(controller, drain);
                        }
                        appliedWorldDrain = true;
                        appliedBasicInfoWorldDrain |= DrainAffectsBasicInfoWorldState(drain);
                    }

                    appliedAny = true;
                    appliedThisFrame++;
                }

                appliedLiveDrainSequence = liveDrainSequence;
            }

            if (hasSelectCartPacket) {
                ShowCartDecorationSelection(selectCartPacket);
            }

            if (appliedWorldDrain) {
                long postSyncStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
                try {
                    using (ApplyLiveDrainPostSyncMarker.Auto()) {
                RoRuntimeMapController activeMapController = ResolveActiveRuntimeMapController(controller);
                SyncLocalPlayerAppearanceFromOnlineWorld(controller, activeMapController);
                RefreshDeathOptionsWindowForOwnActor();
                if (fullSnapshot != null || LiveDrainsAffectLocalPlayerStatus(latestLiveDrain, recentLiveDrains)) {
                    SyncLocalPlayerStatusFromCurrentState(fullSnapshot, controller, activeMapController);
                }

                if (appliedBasicInfoWorldDrain) {
                    RoClassicBasicInfo.Invalidate();
                }

                TimedEnsurePhase12GameplayWindows(bindExisting: false);
                if (fullSnapshot != null) {
                    TimedEnsureGameplayContextWindows(fullSnapshot);
                } else {
                    TimedEnsureGameplayContextWindowsFromCurrentState(latestLiveDrain, recentLiveDrains);
                }
                    }
                } finally {
                    RoRuntimePerfCounters.AddMinimalLoginShellApplyLiveDrainPostSyncTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(postSyncStartTicks));
                }
            }

            CaptureLiveDrainPendingDepthDiagnostics(liveDrainSequence);
                }
            } finally {
                RoRuntimePerfCounters.AddMinimalLoginShellApplyLiveDrainTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(applyLiveDrainStartTicks));
            }
        }

        private void CaptureLiveDrainApplyDiagnostics(
            int liveDrainSequence,
            bool hasLiveDrainPublishedTickCount,
            int liveDrainPublishedTickCount
        ) {
            if (!RoRuntimeDiagnostics.RuntimePerfLogEnabled) {
                return;
            }

            liveDrainPendingDepth = Math.Max(0, liveDrainSequence - appliedLiveDrainSequence);
            if (hasLiveDrainPublishedTickCount) {
                int elapsedMilliseconds = unchecked(Environment.TickCount - liveDrainPublishedTickCount);
                liveDrainApplyAgeMilliseconds = Math.Max(0, elapsedMilliseconds);
                hasLiveDrainApplyAge = true;
            } else {
                liveDrainApplyAgeMilliseconds = 0;
                hasLiveDrainApplyAge = false;
            }
        }

        private void CaptureLiveDrainPendingDepthDiagnostics(int liveDrainSequence) {
            if (!RoRuntimeDiagnostics.RuntimePerfLogEnabled) {
                return;
            }

            liveDrainPendingDepth = Math.Max(0, liveDrainSequence - appliedLiveDrainSequence);
        }

        private void CaptureLiveDrainOverflowFallbackDiagnostics(int liveDrainSequence) {
            if (!RoRuntimeDiagnostics.RuntimePerfLogEnabled) {
                return;
            }

            int sequenceJumpDistance = Math.Max(0, liveDrainSequence - appliedLiveDrainSequence);
            liveDrainOverflowFallbackCount++;
            liveDrainSkippedSequenceCount += Math.Max(0, sequenceJumpDistance - 1);
            liveDrainSequenceJumpDistance += sequenceJumpDistance;
        }

        public static int ResolveLiveDrainApplyBudget(
            int liveDrainSequence,
            RoMapEntryDrainResult latestLiveDrain,
            IReadOnlyList<RoLiveDrainSnapshot> recentLiveDrains,
            int appliedLiveDrainSequence,
            int configuredBudget
        ) {
            int normalBudget = Mathf.Max(1, configuredBudget);
            if (HasPendingMovementCriticalDrain(liveDrainSequence, latestLiveDrain, recentLiveDrains, appliedLiveDrainSequence)) {
                return Mathf.Max(normalBudget, MovementCriticalLiveDrainBurstBudget);
            }

            return normalBudget;
        }

        private static bool HasPendingMovementCriticalDrain(
            int liveDrainSequence,
            RoMapEntryDrainResult latestLiveDrain,
            IReadOnlyList<RoLiveDrainSnapshot> recentLiveDrains,
            int appliedLiveDrainSequence
        ) {
            IReadOnlyList<RoLiveDrainSnapshot> drains = recentLiveDrains ?? Array.Empty<RoLiveDrainSnapshot>();
            for (int i = 0; i < drains.Count; i++) {
                RoLiveDrainSnapshot entry = drains[i];
                if (entry.Sequence > appliedLiveDrainSequence && IsMovementCriticalDrain(entry.Drain)) {
                    return true;
                }
            }

            return liveDrainSequence > appliedLiveDrainSequence && IsMovementCriticalDrain(latestLiveDrain);
        }

        public static bool IsMovementCriticalDrain(RoMapEntryDrainResult drain) {
            if (drain == null) {
                return false;
            }

            if (drain.ActorMoves.Length > 0 ||
                drain.PlayerMoves.Length > 0 ||
                drain.StopMoves.Length > 0) {
                return true;
            }

            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                switch (events[i].Kind) {
                    case RoMapEntryDrainEventKind.ActorMove:
                    case RoMapEntryDrainEventKind.PlayerMove:
                    case RoMapEntryDrainEventKind.StopMove:
                    case RoMapEntryDrainEventKind.ActorAction:
                    case RoMapEntryDrainEventKind.SkillCast:
                    case RoMapEntryDrainEventKind.SkillDamage:
                    case RoMapEntryDrainEventKind.SkillResult:
                    case RoMapEntryDrainEventKind.SkillPosition:
                        return true;
                }
            }

            return false;
        }

        private static bool LiveDrainsAffectLocalPlayerStatus(
            RoMapEntryDrainResult latestLiveDrain,
            IReadOnlyList<RoLiveDrainSnapshot> recentLiveDrains
        ) {
            if (recentLiveDrains != null) {
                for (int i = 0; i < recentLiveDrains.Count; i++) {
                    if (DrainAffectsLocalPlayerStatus(recentLiveDrains[i].Drain)) {
                        return true;
                    }
                }
            }

            return DrainAffectsLocalPlayerStatus(latestLiveDrain);
        }

        private void ShowCartDecorationSelection(RoZcSelectCart selectCart) {
            if (session == null) {
                return;
            }

            RoClassicSelectCart.Show(session, ResolveGameplayUiAssetDatabase(), 0, selectCart.Identity, selectCart.CartTypes);
        }

        private static bool TryGetSelectCart(RoMapEntryDrainResult drain, out RoZcSelectCart selectCart) {
            selectCart = default;
            if (drain?.OrderedEvents == null) {
                return false;
            }

            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                if (events[i].Kind == RoMapEntryDrainEventKind.SelectCart) {
                    selectCart = events[i].SelectCart;
                    return true;
                }
            }

            return false;
        }

        private static bool DrainAffectsLocalPlayerStatus(RoMapEntryDrainResult drain) {
            if (drain?.OrderedEvents == null) {
                return false;
            }

            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                switch (events[i].Kind) {
                    case RoMapEntryDrainEventKind.ParameterChange:
                    case RoMapEntryDrainEventKind.StatusPropertyChange:
                    case RoMapEntryDrainEventKind.StatusChangeAck:
                    case RoMapEntryDrainEventKind.StatusBaseBonus:
                    case RoMapEntryDrainEventKind.StatusSnapshot:
                    case RoMapEntryDrainEventKind.AttackRange:
                    case RoMapEntryDrainEventKind.StatusChange:
                        return true;
                }
            }

            return false;
        }

        private static bool DrainAffectsBasicInfoWorldState(RoMapEntryDrainResult drain) {
            if (drain?.OrderedEvents == null) {
                return false;
            }

            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                switch (events[i].Kind) {
                    case RoMapEntryDrainEventKind.ActorAction:
                    case RoMapEntryDrainEventKind.ActorHpInfo:
                    case RoMapEntryDrainEventKind.PartyHp:
                        return true;
                }
            }

            return false;
        }

        private static RoRuntimeMapController ResolveActiveRuntimeMapController(RoOnlineWorldController controller) {
            if (controller != null && controller.RuntimeMapController != null) {
                return controller.RuntimeMapController;
            }

            return FindFirstObjectByType<RoRuntimeMapController>();
        }

        private static void BindRuntimeMapController(
            RoOnlineWorldController controller,
            RoRuntimeMapController mapController
        ) {
            if (controller == null || mapController == null) {
                return;
            }

            mapController.BindOnlineWorldController(controller);
        }

        private bool TryApplySameMapTransitionPreview(RoOnlineWorldController controller, RoMapEntryDrainResult drain) {
            if (!TryGetSameMapMove(appliedWorldMapResult, drain, out RoZcNpcMapMove mapMove)) {
                return false;
            }

            RoRuntimeMapController activeMapController = ResolveActiveRuntimeMapController(controller);
            BindRuntimeMapController(controller, activeMapController);
            controller?.ApplySameMapMovePreview(mapMove);
            return activeMapController != null &&
                activeMapController.TryMoveLocalPlayerToCell(
                    mapMove.X,
                    mapMove.Y,
                    ResolveLocalPlayerFacingFromWorld(controller),
                    ResolveLocalPlayerPlacementServerTime(controller));
        }

        private static bool TryGetSameMapMove(
            RoLoginEnterMapResult currentMapResult,
            RoMapEntryDrainResult drain,
            out RoZcNpcMapMove mapMove
        ) {
            if (currentMapResult != null && drain?.OrderedEvents != null) {
                string currentMapName = currentMapResult.CharacterResult?.MapHandoff.MapName;
                for (int i = 0; i < drain.OrderedEvents.Length; i++) {
                    RoMapEntryDrainEvent entry = drain.OrderedEvents[i];
                    if (entry.Kind != RoMapEntryDrainEventKind.NpcMapMove) {
                        continue;
                    }

                    RoZcNpcMapMove candidate = entry.NpcMapMove;
                    if (AreComparableMapNamesEqual(currentMapName, candidate.MapName)) {
                        mapMove = candidate;
                        return true;
                    }
                }
            }

            mapMove = default;
            return false;
        }

        private static bool DrainContainsMapTransition(RoMapEntryDrainResult drain) {
            if (drain?.OrderedEvents == null) {
                return false;
            }

            for (int i = 0; i < drain.OrderedEvents.Length; i++) {
                RoMapEntryDrainEventKind kind = drain.OrderedEvents[i].Kind;
                if (kind == RoMapEntryDrainEventKind.NpcMapMove ||
                    kind == RoMapEntryDrainEventKind.NpcServerMove) {
                    return true;
                }
            }

            return false;
        }

        private static void TryApplyWorldDrain(RoOnlineWorldController controller, RoMapEntryDrainResult drain) {
            long startTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            if (controller == null || drain == null) {
                RoRuntimePerfCounters.AddMinimalLoginShellApplyWorldDrainTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(startTicks));
                return;
            }

            try {
                controller.ApplyDrain(drain);
            } catch (Exception ex) {
                Debug.LogWarning($"Live world drain failed after packet receive; UI chat will continue. {ex.Message}");
            } finally {
                RoRuntimePerfCounters.AddMinimalLoginShellApplyWorldDrainTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(startTicks));
            }
        }

        private void AppendLiveDrainChat(int sequence, RoMapEntryDrainResult drain) {
            if (sequence <= appendedLiveChatSequence) {
                return;
            }

            if (TryAppendDrainChat(drain)) {
                appendedLiveChatSequence = sequence;
            }
        }

        private static bool DrainHasChat(RoMapEntryDrainResult drain) {
            if (drain == null) {
                return false;
            }

            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                if (events[i].Kind == RoMapEntryDrainEventKind.PlayerChat ||
                    events[i].Kind == RoMapEntryDrainEventKind.NpcChat ||
                    events[i].Kind == RoMapEntryDrainEventKind.SystemText ||
                    events[i].Kind == RoMapEntryDrainEventKind.Message ||
                    events[i].Kind == RoMapEntryDrainEventKind.ActionFailure ||
                    events[i].Kind == RoMapEntryDrainEventKind.ItemPickupAck ||
                    events[i].Kind == RoMapEntryDrainEventKind.PartyChat ||
                    events[i].Kind == RoMapEntryDrainEventKind.BattlefieldChat ||
                    events[i].Kind == RoMapEntryDrainEventKind.GuildChat ||
                    events[i].Kind == RoMapEntryDrainEventKind.ClanChat ||
                    events[i].Kind == RoMapEntryDrainEventKind.Whisper ||
                    events[i].Kind == RoMapEntryDrainEventKind.WhisperAck ||
                    (events[i].Kind == RoMapEntryDrainEventKind.ItemIdentifyAck &&
                        events[i].ItemIdentifyAck.BagIndex >= 0) ||
                    (events[i].Kind == RoMapEntryDrainEventKind.TeleportResult &&
                        events[i].TeleportResult.HasTeleportMessage) ||
                    (events[i].Kind == RoMapEntryDrainEventKind.SkillUseAck &&
                        !events[i].SkillUseAck.IsSuccess) ||
                    events[i].Kind == RoMapEntryDrainEventKind.VendingSetupAllowed ||
                    events[i].Kind == RoMapEntryDrainEventKind.VendingOpenAccepted ||
                    events[i].Kind == RoMapEntryDrainEventKind.VendingOpenAck ||
                    events[i].Kind == RoMapEntryDrainEventKind.PlayerShopPurchaseResult ||
                    events[i].Kind == RoMapEntryDrainEventKind.VendingSaleReported ||
                    IsLoginOutEvent(events[i]) ||
                    RoClassicSystemLines.IsChatEvent(events[i])) {
                    return true;
                }
            }

            return drain.PlayerChats.Length > 0;
        }

        private RoServerProfile BuildProfile() {
            RoServerProfile fallback = RoServerProfile.LocalRathena20200401();
            // RO_LOGIN_HOST (e.g. a Steam Deck on the LAN): the login server host instead of the serialized one
            string hostOverride = Environment.GetEnvironmentVariable("RO_LOGIN_HOST");
            string host = !string.IsNullOrWhiteSpace(hostOverride) ? hostOverride.Trim() : string.IsNullOrWhiteSpace(loginHost) ? fallback.Login.Host : loginHost.Trim();
            var login = new RoServerEndpoint(host, loginPort);
            var character = new RoServerEndpoint(login.Host, characterPort);
            var map = new RoServerEndpoint(login.Host, mapPort);
            var inspectorProfile = new RoServerProfile(
                fallback.Name,
                fallback.PacketVersion,
                fallback.ClientVersion,
                fallback.LanguageType,
                fallback.Renewal,
                fallback.PacketObfuscation,
                login,
                character,
                map,
                fallback.WebSocketProxy,
                fallback.LegacyTextCodePage);
            RoServerProfile profile = RoServerProfileConfig.LoadFromStreamingAssets(inspectorProfile, out string diagnostic);
            if (!string.IsNullOrWhiteSpace(diagnostic)) {
                if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                    RoRuntimeDiagnostics.IncrementDebugLogLine();
                    Debug.Log(diagnostic);
                }
            }

            return profile;
        }

        public Encoding ResolveBrowserTextEncoding() {
            return RoShellTextEncoding.Resolve(BuildProfile().LegacyTextCodePage);
        }

        private void TryStart(Action action) {
            try {
                action();
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        private Task QueueRuntimeMapLoadForTransition(string mapName, int spawnX, int spawnY) {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (runtimeMapLoadSync) {
                runtimeMapLoadRequests.Enqueue(new RuntimeMapLoadRequest(mapName, spawnX, spawnY, completion));
            }

            return completion.Task;
        }

        private Task QueueSameMapTransitionFadeBeforeAck(string mapName) {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (sameMapTransitionFadeSync) {
                sameMapTransitionFadeRequests.Enqueue(completion);
            }

            return completion.Task;
        }

        private void ProcessSameMapTransitionFadeRequests() {
            if (isProcessingSameMapTransitionFade) {
                return;
            }

            TaskCompletionSource<bool> completion;
            lock (sameMapTransitionFadeSync) {
                if (sameMapTransitionFadeRequests.Count == 0) {
                    return;
                }

                completion = sameMapTransitionFadeRequests.Dequeue();
                isProcessingSameMapTransitionFade = true;
                activeSameMapTransitionFadeCompletion = completion;
            }

            StartSameMapTransitionFadeOut(completion);
        }

        private void StartSameMapTransitionFadeOut(TaskCompletionSource<bool> completion) {
            mapTransitionFade.Show();
            RoUiPointerCapture.SetGlobalBlocker(this, true);
            StartMapTransitionFade(1f, SameMapTransitionFadeOutSeconds, () => {
                sameMapTransitionFadeHeld = true;
                isProcessingSameMapTransitionFade = false;
                activeSameMapTransitionFadeCompletion = null;
                completion.TrySetResult(true);
                ProcessSameMapTransitionFadeRequests();
            });
        }

        private void StartSameMapTransitionFadeIn() {
            sameMapTransitionFadeHeld = false;
            RoUiPointerCapture.SetGlobalBlocker(this, false);
            StartMapTransitionFade(0f, SameMapTransitionFadeInSeconds, () => {
                if (mapTransitionFade.Alpha <= 0.001f) {
                    mapTransitionFade.Hide();
                }
            });
        }

        private void StartMapTransitionFade(float targetAlpha, float seconds, Action completed) {
            if (mapTransitionFadeCoroutine != null) {
                StopCoroutine(mapTransitionFadeCoroutine);
            }

            mapTransitionFadeCoroutine = StartCoroutine(FadeMapTransitionOverlay(targetAlpha, seconds, completed));
        }

        private IEnumerator FadeMapTransitionOverlay(float targetAlpha, float seconds, Action completed) {
            float startAlpha = mapTransitionFade.Alpha;
            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                mapTransitionFade.Alpha = alpha;
                yield return null;
            }

            mapTransitionFade.Alpha = targetAlpha;
            mapTransitionFadeCoroutine = null;
            completed?.Invoke();
        }

        private void ProcessRuntimeMapLoadRequests() {
            if (isProcessingRuntimeMapLoad) {
                return;
            }

            RuntimeMapLoadRequest request;
            lock (runtimeMapLoadSync) {
                if (runtimeMapLoadRequests.Count == 0) {
                    return;
                }

                request = runtimeMapLoadRequests.Dequeue();
                isProcessingRuntimeMapLoad = true;
            }

            _ = CompleteRuntimeMapLoadRequest(request);
        }

        private RoMapLoadingScreen EnsureMapLoadingScreen() {
            if (mapLoadingScreen == null) {
                mapLoadingScreen = RoMapLoadingScreen.EnsureInScene();
            }

            return mapLoadingScreen;
        }

        private RoAssetDatabase ResolveMapLoadingAssets(RoRuntimeMapController preferredController = null) {
            if (preferredController != null &&
                preferredController.Content != null &&
                preferredController.Content.IsInitialized) {
                return preferredController.Content.Assets;
            }

            RoRuntimeMapController activeController = FindFirstObjectByType<RoRuntimeMapController>();
            if (activeController != null &&
                activeController.Content != null &&
                activeController.Content.IsInitialized) {
                return activeController.Content.Assets;
            }

            return null;
        }

        private RoMapLoadingScreen ShowRuntimeMapLoadingScreen(RoRuntimeMapController mapController) {
            RoMapLoadingScreen loadingScreen = EnsureMapLoadingScreen();
            loadingScreen.Show(ResolveMapLoadingAssets(mapController));
            loadingScreen.SetProgress(0f);

            return loadingScreen;
        }

        private async Task TrackRuntimeMapLoadingProgress(
            RoMapLoadingScreen loadingScreen,
            Task loadTask,
            RoRuntimeMapController mapController) {
            if (loadingScreen == null || loadTask == null) {
                return;
            }

            float elapsed = 0f;
            loadingScreen.SetProgress(5f);
            while (!loadTask.IsCompleted) {
                if (!loadingScreen.HasLoadingTexture) {
                    loadingScreen.Show(ResolveMapLoadingAssets(mapController));
                }

                elapsed += Mathf.Max(Time.unscaledDeltaTime, 0.016f);
                float eased = 1f - Mathf.Exp(-elapsed * 0.55f);
                loadingScreen.SetProgress(Mathf.Lerp(5f, 92f, eased));
                await Task.Yield();
            }
        }

        private static async Task FinishRuntimeMapLoadingScreen(RoMapLoadingScreen loadingScreen) {
            if (loadingScreen == null) {
                return;
            }

            float visibleSeconds = loadingScreen.VisibleElapsedSeconds;
            float startPercent = Mathf.Clamp(
                Mathf.Max(loadingScreen.DisplayedPercent, loadingScreen.TargetPercent),
                0f,
                99f);
            bool fastFinish = visibleSeconds <= FastMapLoadingScreenThresholdSeconds;
            if (fastFinish) {
                loadingScreen.SetDisplayedProgress(startPercent);
                await AnimateRuntimeMapLoadingScreenFinish(
                    loadingScreen,
                    startPercent,
                    FastMapLoadingScreenFinishSeconds);
                LogRuntimeMapLoadingScreenFinish(visibleSeconds, startPercent, FastMapLoadingScreenFinishSeconds, fastFinish);
                loadingScreen.Hide();
                return;
            }

            // the map is ready: the bar only closes (the client shows its real progress and no eased finish); the old
            // 0.22-0.55 s ease to 100 % kept every slower map change that much longer behind the loading screen
            float finishDuration = FastMapLoadingScreenFinishSeconds;
            await AnimateRuntimeMapLoadingScreenFinish(loadingScreen, startPercent, finishDuration);
            LogRuntimeMapLoadingScreenFinish(visibleSeconds, startPercent, finishDuration, fastFinish);
            loadingScreen.Hide();
        }

        private static async Task AnimateRuntimeMapLoadingScreenFinish(
            RoMapLoadingScreen loadingScreen,
            float startPercent,
            float finishDuration
        ) {
            float elapsed = 0f;

            loadingScreen.SetDisplayedProgress(startPercent);
            while (elapsed < finishDuration) {
                await Task.Yield();
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 0.016f);
                float normalized = Mathf.Clamp01(elapsed / finishDuration);
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                loadingScreen.SetDisplayedProgress(Mathf.Lerp(startPercent, 100f, eased));
            }

            loadingScreen.SetDisplayedProgress(100f);
            await Task.Yield();
        }

        private static void LogRuntimeMapLoadingScreenFinish(
            float visibleSeconds,
            float startPercent,
            float finishDuration,
            bool fastFinish
        ) {
            // one line per map change in the Editor and development builds: the map change's length behind the screen
            if (!RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics && !Application.isEditor && !Debug.isDebugBuild) {
                return;
            }

            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log(
                $"RO map loading screen finish: visible={visibleSeconds * 1000f:0} ms, " +
                $"start={startPercent:0.#}%, finish={finishDuration * 1000f:0} ms, fast={fastFinish}.");
        }

        private async Task CompleteRuntimeMapLoadRequest(RuntimeMapLoadRequest request) {
            RoMapLoadingScreen loadingScreen = null;
            try {
                RoRuntimeMapController mapController = FindFirstObjectByType<RoRuntimeMapController>();
                if (mapController == null) {
                    throw new InvalidOperationException("No RoRuntimeMapController exists in the scene, so the portal target map cannot be loaded before CZ_NOTIFY_ACTORINIT.");
                }

                RoOnlineWorldController controller = EnsureOnlineWorldController();
                mapController.BindOnlineWorldController(controller);
                string requestedMap = RoRuntimeMapController.NormalizeMapResourceName(request.MapName);
                mapController.SetNextLocalPlayerSpawnCell(request.SpawnX, request.SpawnY);
                loadingScreen = ShowRuntimeMapLoadingScreen(mapController);
                await Task.Yield();
                if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                    RoRuntimeDiagnostics.IncrementDebugLogLine();
                    Debug.Log($"Loading RO portal target map '{requestedMap}' before CZ_NOTIFY_ACTORINIT at cell {request.SpawnX},{request.SpawnY}.", this);
                }

                Task loadTask = mapController.LoadMapAsync(requestedMap);
                await TrackRuntimeMapLoadingProgress(loadingScreen, loadTask, mapController);
                await loadTask;

                mapController.BindOnlineWorldController(controller);
                if (mapController.GridSelector != null) {
                    controller.SetGridSelector(mapController.GridSelector);
                }

                loadingScreen.SetProgress(96f);
                runtimeMapLoadAwaitingPresentation = true;
                runtimeMapLoadAwaitingMapName = requestedMap;
                request.Completion.SetResult(true);
                loadingScreen = null;
            } catch (Exception ex) {
                runtimeMapLoadAwaitingPresentation = false;
                runtimeMapLoadAwaitingMapName = null;
                request.Completion.SetException(ex);
            } finally {
                if (loadingScreen != null) {
                    loadingScreen.Hide();
                }

                isProcessingRuntimeMapLoad = false;
                ProcessRuntimeMapLoadRequests();
            }
        }

        private void FailPendingRuntimeMapLoads(Exception exception) {
            runtimeMapLoadAwaitingPresentation = false;
            runtimeMapLoadAwaitingMapName = null;
            lock (runtimeMapLoadSync) {
                while (runtimeMapLoadRequests.Count > 0) {
                    runtimeMapLoadRequests.Dequeue().Completion.TrySetException(exception);
                }
            }
        }

        private void FailPendingSameMapTransitionFades(Exception exception) {
            activeSameMapTransitionFadeCompletion?.TrySetException(exception);
            activeSameMapTransitionFadeCompletion = null;
            lock (sameMapTransitionFadeSync) {
                while (sameMapTransitionFadeRequests.Count > 0) {
                    sameMapTransitionFadeRequests.Dequeue().TrySetException(exception);
                }
            }

            isProcessingSameMapTransitionFade = false;
        }

        private RoOnlineWorldController ResolveOnlineWorldController(bool forceLookup = false) {
            if (onlineWorldController != null) {
                ApplyCombatTraceOptionsToOnlineWorldController();
                return onlineWorldController;
            }

            float now = Time.unscaledTime;
            if (!forceLookup && now < nextOnlineWorldControllerLookupTime) {
                return null;
            }

            onlineWorldController = FindFirstObjectByType<RoOnlineWorldController>();
            nextOnlineWorldControllerLookupTime = onlineWorldController != null
                ? 0f
                : now + WorldControllerLookupRetrySeconds;
            ApplyCombatTraceOptionsToOnlineWorldController();
            return onlineWorldController;
        }

        private RoOnlineWorldController EnsureOnlineWorldController() {
            ResolveOnlineWorldController(forceLookup: true);

            if (onlineWorldController == null) {
                var worldObject = new GameObject("RO Online World");
                onlineWorldController = worldObject.AddComponent<RoOnlineWorldController>();
                nextOnlineWorldControllerLookupTime = 0f;
            }

            ApplyCombatTraceOptionsToOnlineWorldController();

            onlineWorldController.CreateDebugMarkers = false;

            // Resolving the grid selector needs a full-scene FindFirstObjectByType, and this method runs on
            // every live drain (i.e. whenever movement/action packets arrive). The grid selector is bound
            // explicitly by the map-load path and stays valid for the life of the map, so only fall back to
            // a scene scan when the controller has no live selector — a fresh controller, or the previous
            // selector was destroyed on map unload (Unity-null). This removes a per-drain full-scene scan
            // during normal play, which was the dominant cost of each live-drain frame.
            if (onlineWorldController.GridSelector == null) {
                RoGridSelector selector = FindFirstObjectByType<RoGridSelector>();
                if (selector != null) {
                    onlineWorldController.SetGridSelector(selector);
                }
            }

            BindAmmunitionProjectileVisuals(onlineWorldController);
            return onlineWorldController;
        }

        private void BindAmmunitionProjectileVisuals(RoOnlineWorldController controller) {
            controller?.BindAmmunitionProjectileVisuals(
                () => session != null ? session.EquipmentSnapshot() : RoGameplayEquipmentSnapshot.Empty,
                () => {
                    RoRuntimeMapController activeMapController = ResolveActiveRuntimeMapController(onlineWorldController);
                    return activeMapController != null
                        ? activeMapController.Content?.Assets?.GameData?.Items ?? RoItemMetadataTable.Empty
                        : RoItemMetadataTable.Empty;
                });
        }

        private void EnsureNpcTargetRouterBound() {
            if (session == null) {
                UnbindNpcTargetRouter();
                return;
            }

            RoShellCommandStateSnapshot commandState = session.CommandStateSnapshot();
            if (commandState.Stage != RoLoginShellStage.MapEntered || commandState.IsMapTransitionPending) {
                UnbindNpcTargetRouter();
                return;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller == null) {
                return;
            }

            BindNpcTargetRouter(controller);
        }

        private void BindNpcTargetRouter(RoOnlineWorldController controller) {
            if (controller == null || ReferenceEquals(npcTargetRouterWorldController, controller)) {
                return;
            }

            UnbindNpcTargetRouter();
            npcTargetRouterWorldController = controller;
            npcTargetRouterWorldController.TargetSelected += OnNpcTargetRouterSelected;
        }

        private void UnbindNpcTargetRouter() {
            if (npcTargetRouterWorldController != null) {
                npcTargetRouterWorldController.TargetSelected -= OnNpcTargetRouterSelected;
            }

            npcTargetRouterWorldController = null;
            ResetNpcContactRequestDebounce();
        }

        private void OnNpcTargetRouterSelected(RoOnlineTargetSelection selected) {
            if (selected.Interaction != RoOnlineTargetInteraction.Talk) {
                return;
            }

            TryBeginNpcContactFromTarget(selected);
        }

        private void TryBeginNpcContactFromTarget(RoOnlineTargetSelection selected) {
            if (!TryGetSelectedNpcContactId(selected, out uint npcId) ||
                session == null ||
                npcId > int.MaxValue) {
                return;
            }

            RoShellCommandStateSnapshot commandState = session.CommandStateSnapshot();
            RoNpcDialogSnapshot dialog = session.NpcDialogSnapshot();
            RoNpcShopSnapshot shop = session.NpcShopSnapshot();
            bool closedLocally = RoNpcInteractionModel.ClosedLocally(session);
            if (!CanSendNpcContact(commandState, dialog, shop, out string reason)) {
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (!closedLocally && IsDuplicateNpcContactRequest(npcId, now)) {
                return;
            }

            try {
                _ = session.BeginContactNpc(unchecked((int) npcId), type: 1);
                RememberNpcContactRequest(npcId, now);
            } catch (Exception ex) {
                Debug.LogWarning($"NPC contact was not sent for {npcId}: {ex.Message}");
            }
        }

        private bool CanSendNpcContact(
            RoShellCommandStateSnapshot commandState,
            RoNpcDialogSnapshot dialog,
            RoNpcShopSnapshot shop,
            out string reason
        ) {
            if (!RoNpcInteractionModel.CanContactNpc(commandState, dialog, shop, out reason)) {
                return false;
            }

            if (TryGetOwnOnlineActor(out RoOnlineActorSnapshot actor) &&
                RoOnlineActorInteractionRules.BlocksMovementOrAction(actor)) {
                reason = RoOnlineActorInteractionRules.IsDead(actor)
                    ? "local actor is dead"
                    : "local actor is sitting";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool TryGetSelectedNpcContactId(RoOnlineTargetSelection selected, out uint npcId) {
            npcId = 0;
            if (!selected.HasTarget ||
                selected.Interaction != RoOnlineTargetInteraction.Talk ||
                selected.ServerId == 0) {
                return false;
            }

            npcId = selected.ServerId;
            return true;
        }

        private bool IsDuplicateNpcContactRequest(uint npcId, DateTime nowUtc) {
            return npcId != 0 &&
                lastNpcContactRequestNpcId == npcId &&
                (nowUtc - lastNpcContactRequestUtc).TotalSeconds < NpcContactRequestDebounceSeconds;
        }

        private void RememberNpcContactRequest(uint npcId, DateTime nowUtc) {
            lastNpcContactRequestNpcId = npcId;
            lastNpcContactRequestUtc = nowUtc;
        }

        private void ResetNpcContactRequestDebounce() {
            lastNpcContactRequestNpcId = 0;
            lastNpcContactRequestUtc = DateTime.MinValue;
        }

        /// <summary>
        /// The manual reconnect prompt (the connection dropped and the automatic retries gave up): a classic OK box with
        /// the reason; its answer reconnects. Closed again when the session no longer asks for it.
        /// </summary>
        private void SyncReconnectPrompt() {
            bool wanted = session != null && session.HasManualReconnectPrompt;
            if (!wanted) {
                if (reconnectPrompt != null) {
                    reconnectPrompt.Close();
                    reconnectPrompt = null;
                }

                return;
            }

            if (reconnectPrompt != null) {
                return;
            }

            RoLoginShellSnapshot snapshot = session.Snapshot();
            if (snapshot == null ||
                !snapshot.CanReconnect ||
                !(snapshot.ConnectionLost || snapshot.ReconnectState == RoReconnectState.Failed)) {
                return;
            }

            // the server sent the character to the character select itself (ZC_RESTART_ACK 1 unasked, e.g. after
            // @autotrade): the original goes there without a box
            if (RoServerRestart.Is(snapshot.ReconnectReason)) {
                ReturnToCharacterSelectFromPhase11Ui();
                return;
            }

            // the client's disconnect box: SC_NOTIFY_BAN 2 "Someone has Logged in with this ID." (MsgStr 5), 0 / 1
            // "Server Closed." (4), anything else and a socket the server closed "Disconnected from Server!" (3); OK goes
            // back to the login, the client never reconnects by itself
            int msg = 3;
            if (RoServerKick.TryParse(snapshot.ReconnectReason, out int kickCode)) {
                msg = kickCode == 2 ? 5 : kickCode == 0 || kickCode == 1 ? 4 : 3;
            }

            string text = RoClassicUiHost.MsgStr(msg, msg == 5 ? "Someone has Logged in with this ID." : msg == 4 ? "Server Closed." : "Disconnected from Server!");
            reconnectPrompt = Ragnarok.UI.Classic.RoClassicMessageBox.Show(text, Ragnarok.UI.Classic.RoClassicMessageBox.TypeOk, true, _ => {
                reconnectPrompt = null;
                ResetFromPhase11Ui();
            }, isModal: true);
        }

        private void ClearOnlineWorld() {
            SaveMapDiagnosticsReport("clear-online-world", appliedWorldMapResult);
            appliedWorldMapResult = null;
            appliedLiveDrainSequence = 0;
            appendedLiveChatSequence = 0;
            lastOwnActorDeadForDeathMenu = false;
            if (RoClassicEscMenu.IsDeathMenuMode) {
                RoClassicEscMenu.ResetDeathMenu();
                RoClassicEscMenu.Close();
            }

            ClearGameplayWindows();


            RoClassicBasicInfo.Close();

            if (onlineWorldController != null) {
                onlineWorldController.SetServerClock(null);
                onlineWorldController.ClearWorld();
            }
        }

        private void BeginMapDiagnosticsSession(RoLoginEnterMapResult mapResult) {
            liveDrainOverflowFallbackCount = 0;
            liveDrainSkippedSequenceCount = 0;
            liveDrainSequenceJumpDistance = 0;
            liveDrainPendingDepth = 0;
            liveDrainApplyAgeMilliseconds = 0;
            hasLiveDrainApplyAge = false;
            cachedMapDiagnosticsMovement = default;
            cachedMapDiagnosticsHasWorldState = false;
            cachedMapDiagnosticsActorCount = 0;
            cachedMapDiagnosticsMovingActorCount = 0;
            cachedMapDiagnosticsPooledActorStateCount = 0;
            cachedMapDiagnosticsMaxPooledActorStateCount = 0;
            cachedMapDiagnosticsHasServerTime = false;
            cachedMapDiagnosticsLatestServerTimeMilliseconds = 0;
            cachedClickToMoveTrace.Clear();
            cachedClickToMoveTraceAvailable = false;
            cachedClickToMoveTraceCount = 0;
            mapDiagnosticsClickToMoveController = null;

            if (mapResult == null) {
                mapDiagnosticsStartedUtc = DateTime.MinValue;
                mapDiagnosticsReportWritten = false;
                return;
            }

            mapDiagnosticsStartedUtc = DateTime.UtcNow;
            mapDiagnosticsReportWritten = false;
        }

        private void RefreshMapDiagnosticsSnapshot() {
            if (!RoRuntimeDiagnostics.RuntimePerfLogEnabled || appliedWorldMapResult == null) {
                return;
            }

            RoOnlineWorldController controller = ResolveOnlineWorldController();
            if (controller != null) {
                RoOnlineWorldState state = controller.State;
                cachedMapDiagnosticsMovement = state.MovementDiagnostics;
                cachedMapDiagnosticsHasWorldState = true;
                cachedMapDiagnosticsActorCount = state.ActorCount;
                cachedMapDiagnosticsMovingActorCount = state.MovingActorCount;
                cachedMapDiagnosticsPooledActorStateCount = state.PooledActorStateCount;
                cachedMapDiagnosticsMaxPooledActorStateCount = state.MaxPooledActorStateCount;
                cachedMapDiagnosticsHasServerTime = state.HasServerTime;
                cachedMapDiagnosticsLatestServerTimeMilliseconds = state.LatestServerTimeMilliseconds;
            }

            if (mapDiagnosticsClickToMoveController == null) {
                mapDiagnosticsClickToMoveController = FindFirstObjectByType<RoClickToMoveController>();
            }

            if (mapDiagnosticsClickToMoveController != null) {
                cachedClickToMoveTrace.Clear();
                cachedClickToMoveTraceCount = mapDiagnosticsClickToMoveController.SnapshotRuntimePerfTrace(cachedClickToMoveTrace);
                cachedClickToMoveTraceAvailable = true;
            }
        }

        public void StartRuntimePerfCapture(float durationSeconds) {
            if (!Application.isPlaying) {
                Debug.LogWarning("RO runtime perf capture can only run in Play Mode.");
                return;
            }

            EnsureRuntimePerfLogEnabledForCapture();
            runtimePerfCaptureSamples.Clear();
            runtimePerfCaptureActive = true;
            runtimePerfCaptureStartedAt = Time.realtimeSinceStartup;
            runtimePerfCaptureDurationSeconds = Mathf.Max(1f, durationSeconds);
            runtimePerfCaptureNextSampleAt = runtimePerfCaptureStartedAt;
            runtimePerfCaptureStartedUtc = DateTime.UtcNow;
            runtimePerfCaptureStartManagedMemoryBytes = GC.GetTotalMemory(false);
            LastRuntimePerfCapturePath = string.Empty;
            CaptureRuntimePerfSample(0f);
            runtimePerfCaptureNextSampleAt = runtimePerfCaptureStartedAt + RuntimePerfCaptureSampleIntervalSeconds;
            Debug.Log($"RO runtime perf capture started for {runtimePerfCaptureDurationSeconds:F1}s.");
        }

        private void EnsureRuntimePerfLogEnabledForCapture() {
            if (diagnosticsOptions == null) {
                diagnosticsOptions = new RoRuntimeDiagnosticsOptions();
            }

            if (!diagnosticsOptions.EnableRuntimePerfLog) {
                diagnosticsOptions.EnableRuntimePerfLog = true;
            }

            ApplyRuntimeDiagnosticsOptions(logCombatTraceState: false);
        }

        private void ApplyRuntimeDiagnosticsOptions(bool logCombatTraceState) {
            activeDiagnosticsOptions = RoRuntimeDiagnostics.Normalize(diagnosticsOptions);
            RoRuntimeDiagnostics.Apply(activeDiagnosticsOptions);
            ApplyCombatTraceOptionsToOnlineWorldController();
            if (logCombatTraceState && activeDiagnosticsOptions.EnableCombatTraceConsoleLog) {
                Debug.Log($"[RO Combat] trace options applied trace={activeDiagnosticsOptions.EnableCombatTrace} console={activeDiagnosticsOptions.EnableCombatTraceConsoleLog} controller={(onlineWorldController != null)}");
            }
        }

        private void ApplyCombatTraceOptionsToOnlineWorldController() {
            if (onlineWorldController == null || activeDiagnosticsOptions == null) {
                return;
            }

            var options = onlineWorldController.CombatPresentationOptions;
            options.EnableCombatTrace = activeDiagnosticsOptions.EnableCombatTrace;
            options.EnableCombatTraceConsoleLog = activeDiagnosticsOptions.EnableCombatTraceConsoleLog;
        }

        private void TickRuntimePerfCapture() {
            if (!runtimePerfCaptureActive) {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now >= runtimePerfCaptureNextSampleAt) {
                CaptureRuntimePerfSample(now - runtimePerfCaptureStartedAt);
                do {
                    runtimePerfCaptureNextSampleAt += RuntimePerfCaptureSampleIntervalSeconds;
                } while (runtimePerfCaptureNextSampleAt <= now);
            }

            if (now - runtimePerfCaptureStartedAt >= runtimePerfCaptureDurationSeconds) {
                FinishRuntimePerfCapture("duration-complete");
            }
        }

        private void FinishRuntimePerfCapture(string reason) {
            if (!runtimePerfCaptureActive) {
                return;
            }

            CaptureRuntimePerfSample(Mathf.Max(0f, Time.realtimeSinceStartup - runtimePerfCaptureStartedAt));
            runtimePerfCaptureActive = false;
            SaveRuntimePerfCapture(reason);
        }

        private void CaptureRuntimePerfSample(float elapsedSeconds) {
            RoRuntimePerfCounterSnapshot counters = RoRuntimePerfCounters.Snapshot();
            RoLiveDrainApplyDiagnosticsSnapshot drain = LiveDrainApplyDiagnostics;
            RoOnlineWorldController controller = ResolveOnlineWorldController();
            RoMovementDiagnosticsSnapshot movement = default;
            bool hasWorldState = controller != null;
            int actorCount = 0;
            int movingActorCount = 0;
            int pooledActorStateCount = 0;
            int maxPooledActorStateCount = 0;
            bool hasServerTime = false;
            uint latestServerTimeMilliseconds = 0;

            if (hasWorldState) {
                RoOnlineWorldState state = controller.State;
                movement = state.MovementDiagnostics;
                actorCount = state.ActorCount;
                movingActorCount = state.MovingActorCount;
                pooledActorStateCount = state.PooledActorStateCount;
                maxPooledActorStateCount = state.MaxPooledActorStateCount;
                hasServerTime = state.HasServerTime;
                latestServerTimeMilliseconds = state.LatestServerTimeMilliseconds;
            } else if (cachedMapDiagnosticsHasWorldState) {
                movement = cachedMapDiagnosticsMovement;
                actorCount = cachedMapDiagnosticsActorCount;
                movingActorCount = cachedMapDiagnosticsMovingActorCount;
                pooledActorStateCount = cachedMapDiagnosticsPooledActorStateCount;
                maxPooledActorStateCount = cachedMapDiagnosticsMaxPooledActorStateCount;
                hasServerTime = cachedMapDiagnosticsHasServerTime;
                latestServerTimeMilliseconds = cachedMapDiagnosticsLatestServerTimeMilliseconds;
            }

            RoShellCommandStateSnapshot? commandState = session != null
                ? (RoShellCommandStateSnapshot?) session.CommandStateSnapshot()
                : null;
            long managedMemoryBytes = GC.GetTotalMemory(false);

            runtimePerfCaptureSamples.Add(new RuntimePerfCaptureSample {
                ElapsedSeconds = Mathf.Max(0f, elapsedSeconds),
                Frame = Time.frameCount,
                ManagedMemoryBytes = managedMemoryBytes,
                ManagedMemoryDeltaBytes = managedMemoryBytes - runtimePerfCaptureStartManagedMemoryBytes,
                SessionSequence = session != null ? session.LiveDrainSequence : 0,
                AppliedSequence = appliedLiveDrainSequence,
                PendingDepth = drain.PendingDepth,
                HasApplyAge = drain.HasApplyAge,
                ApplyAgeMilliseconds = drain.ApplyAgeMilliseconds,
                OverflowFallbackCount = drain.OverflowFallbackCount,
                SkippedSequenceCount = drain.SkippedSequenceCount,
                SequenceJumpDistance = drain.SequenceJumpDistance,
                ActorCount = actorCount,
                MovingActorCount = movingActorCount,
                PooledActorStateCount = pooledActorStateCount,
                MaxPooledActorStateCount = maxPooledActorStateCount,
                HasServerTime = hasServerTime,
                LatestServerTimeMilliseconds = latestServerTimeMilliseconds,
                AcceptedMoves = movement.AcceptedMoves,
                IgnoredStaleMoves = movement.IgnoredStaleMoves,
                IgnoredDuplicateMoves = movement.IgnoredDuplicateMoves,
                IgnoredInvalidMoves = movement.IgnoredInvalidMoves,
                HasCommandState = commandState.HasValue,
                Stage = commandState.HasValue ? commandState.Value.Stage.ToString() : string.Empty,
                IsActionLocked = commandState.HasValue && commandState.Value.IsActionLocked,
                ActionLockRemainingMilliseconds = commandState.HasValue
                    ? commandState.Value.ActionLockRemainingMilliseconds
                    : 0,
                ActionLockReason = commandState.HasValue ? commandState.Value.ActionLockReason ?? string.Empty : string.Empty,
                DrainToResultCalls = counters.DrainToResultCalls,
                DrainToResultArrayCount = counters.DrainToResultArrayCount,
                DrainToResultElementCount = counters.DrainToResultElementCount,
                DrainToResultEstimatedMinBytes = counters.DrainToResultEstimatedMinBytes,
                SnapshotRecentCalls = counters.SnapshotRecentCalls,
                SnapshotRecentArrayCount = counters.SnapshotRecentArrayCount,
                SnapshotRecentElementCount = counters.SnapshotRecentElementCount,
                SnapshotRecentEstimatedMinBytes = counters.SnapshotRecentEstimatedMinBytes,
                SessionSnapshots = counters.SessionSnapshots,
                SchedulerRefreshes = counters.SchedulerRefreshes,
                SchedulerSkipped = counters.SchedulerSkipped,
                ActorPresenterSyncs = counters.ActorPresenterSyncs,
                ActorPresenterActorsProcessed = counters.ActorPresenterActorsProcessed,
                ActorPresenterActorsChanged = counters.ActorPresenterActorsChanged,
                ForcedPresenterSyncs = counters.ForcedPresenterSyncs,
                ForcedActorMotions = counters.ForcedActorMotions,
                PacketTraceWrites = counters.PacketTraceWrites,
                PacketTraceSkipped = counters.PacketTraceSkipped,
                PacketLogRefreshes = counters.PacketLogRefreshes,
                CombatTraceRecords = counters.CombatTraceRecords,
                DebugLogLines = counters.DebugLogLines,
                StaticChunksVisible = counters.StaticChunksVisible,
                StaticChunksTotal = counters.StaticChunksTotal,
            });
        }

        private void SaveRuntimePerfCapture(string reason) {
            if (runtimePerfCaptureSamples.Count == 0) {
                return;
            }

            try {
                DateTime nowUtc = DateTime.UtcNow;
                string root = Path.Combine(Application.persistentDataPath, "RagnarokDiagnostics", "RuntimePerfCaptures");
                Directory.CreateDirectory(root);
                string mapName = ResolveMapDiagnosticsMapName(appliedWorldMapResult);
                string timestamp = nowUtc.ToString("yyyyMMdd-HHmmss");
                string fileName = $"{timestamp}-{SanitizeDiagnosticsFileName(mapName)}-60s-{++runtimePerfCaptureReportSequence}.csv";
                string path = Path.Combine(root, fileName);
                File.WriteAllText(path, BuildRuntimePerfCaptureCsv(reason, nowUtc), Encoding.UTF8);
                LastRuntimePerfCapturePath = path;
                Debug.Log($"RO runtime perf capture saved: {path}");
            } catch (Exception ex) {
                Debug.LogWarning($"RO runtime perf capture save failed: {ex.Message}");
            }
        }

        private string BuildRuntimePerfCaptureCsv(string reason, DateTime nowUtc) {
            var csv = new StringBuilder(Math.Max(4096, runtimePerfCaptureSamples.Count * 360));
            RoCharacterInfo character = appliedWorldMapResult != null && appliedWorldMapResult.CharacterResult != null
                ? appliedWorldMapResult.CharacterResult.SelectedCharacter
                : null;
            TimeSpan duration = runtimePerfCaptureStartedUtc != DateTime.MinValue
                ? nowUtc - runtimePerfCaptureStartedUtc
                : TimeSpan.Zero;

            csv.AppendLine("# Ragnarok runtime perf capture");
            csv.Append("# reason: ");
            csv.AppendLine(string.IsNullOrWhiteSpace(reason) ? "unknown" : reason);
            csv.Append("# savedUtc: ");
            csv.AppendLine(nowUtc.ToString("O", CultureInfo.InvariantCulture));
            csv.Append("# startedUtc: ");
            csv.AppendLine(runtimePerfCaptureStartedUtc != DateTime.MinValue
                ? runtimePerfCaptureStartedUtc.ToString("O", CultureInfo.InvariantCulture)
                : "unknown");
            csv.Append("# durationSeconds: ");
            csv.AppendLine(Math.Max(0d, duration.TotalSeconds).ToString("F3", CultureInfo.InvariantCulture));
            csv.Append("# sampleIntervalSeconds: ");
            csv.AppendLine(RuntimePerfCaptureSampleIntervalSeconds.ToString("F3", CultureInfo.InvariantCulture));
            csv.Append("# map: ");
            csv.AppendLine(ResolveMapDiagnosticsMapName(appliedWorldMapResult));
            csv.Append("# character: ");
            csv.AppendLine(character != null ? character.Name ?? string.Empty : string.Empty);
            csv.Append("# characterId: ");
            csv.AppendLine(character != null ? character.CharacterId.ToString(CultureInfo.InvariantCulture) : "0");
            csv.Append("# lastMapDiagnosticsReportPath: ");
            csv.AppendLine(LastMapDiagnosticsReportPath ?? string.Empty);
            csv.AppendLine("# estimatedMinBytes is a lower-bound estimate: array headers plus pointer-sized element slots.");
            csv.AppendLine("elapsedSeconds,frame,managedMemoryBytes,managedMemoryDeltaBytes,sessionSequence,appliedSequence,pendingDepth,hasApplyAge,applyAgeMs,overflowFallbackCount,skippedSequenceCount,sequenceJumpDistance,actorCount,movingActors,pooledActorStates,maxPooledActorStates,hasServerTime,latestServerTimeMs,acceptedMoves,ignoredStaleMoves,ignoredDuplicateMoves,ignoredInvalidMoves,hasCommandState,stage,isActionLocked,actionLockRemainingMs,actionLockReason,drainToResultCalls,drainToResultArrayCount,drainToResultElementCount,drainToResultEstimatedMinBytes,snapshotRecentCalls,snapshotRecentArrayCount,snapshotRecentElementCount,snapshotRecentEstimatedMinBytes,sessionSnapshots,schedulerRefreshes,schedulerSkipped,actorPresenterSyncs,actorPresenterActorsProcessed,actorPresenterActorsChanged,forcedPresenterSyncs,forcedActorMotions,packetTraceWrites,packetTraceSkipped,packetLogRefreshes,combatTraceRecords,debugLogLines,staticChunksVisible,staticChunksTotal");

            for (int i = 0; i < runtimePerfCaptureSamples.Count; i++) {
                AppendRuntimePerfCaptureRow(csv, runtimePerfCaptureSamples[i]);
            }

            AppendRuntimePerfClickTrace(csv);
            return csv.ToString();
        }

        private static void AppendRuntimePerfCaptureRow(StringBuilder csv, RuntimePerfCaptureSample sample) {
            csv.Append(sample.ElapsedSeconds.ToString("F3", CultureInfo.InvariantCulture));
            AppendCsv(csv, sample.Frame);
            AppendCsv(csv, sample.ManagedMemoryBytes);
            AppendCsv(csv, sample.ManagedMemoryDeltaBytes);
            AppendCsv(csv, sample.SessionSequence);
            AppendCsv(csv, sample.AppliedSequence);
            AppendCsv(csv, sample.PendingDepth);
            AppendCsv(csv, sample.HasApplyAge);
            AppendCsv(csv, sample.ApplyAgeMilliseconds);
            AppendCsv(csv, sample.OverflowFallbackCount);
            AppendCsv(csv, sample.SkippedSequenceCount);
            AppendCsv(csv, sample.SequenceJumpDistance);
            AppendCsv(csv, sample.ActorCount);
            AppendCsv(csv, sample.MovingActorCount);
            AppendCsv(csv, sample.PooledActorStateCount);
            AppendCsv(csv, sample.MaxPooledActorStateCount);
            AppendCsv(csv, sample.HasServerTime);
            AppendCsv(csv, sample.LatestServerTimeMilliseconds);
            AppendCsv(csv, sample.AcceptedMoves);
            AppendCsv(csv, sample.IgnoredStaleMoves);
            AppendCsv(csv, sample.IgnoredDuplicateMoves);
            AppendCsv(csv, sample.IgnoredInvalidMoves);
            AppendCsv(csv, sample.HasCommandState);
            AppendCsv(csv, sample.Stage);
            AppendCsv(csv, sample.IsActionLocked);
            AppendCsv(csv, sample.ActionLockRemainingMilliseconds);
            AppendCsv(csv, sample.ActionLockReason);
            AppendCsv(csv, sample.DrainToResultCalls);
            AppendCsv(csv, sample.DrainToResultArrayCount);
            AppendCsv(csv, sample.DrainToResultElementCount);
            AppendCsv(csv, sample.DrainToResultEstimatedMinBytes);
            AppendCsv(csv, sample.SnapshotRecentCalls);
            AppendCsv(csv, sample.SnapshotRecentArrayCount);
            AppendCsv(csv, sample.SnapshotRecentElementCount);
            AppendCsv(csv, sample.SnapshotRecentEstimatedMinBytes);
            AppendCsv(csv, sample.SessionSnapshots);
            AppendCsv(csv, sample.SchedulerRefreshes);
            AppendCsv(csv, sample.SchedulerSkipped);
            AppendCsv(csv, sample.ActorPresenterSyncs);
            AppendCsv(csv, sample.ActorPresenterActorsProcessed);
            AppendCsv(csv, sample.ActorPresenterActorsChanged);
            AppendCsv(csv, sample.ForcedPresenterSyncs);
            AppendCsv(csv, sample.ForcedActorMotions);
            AppendCsv(csv, sample.PacketTraceWrites);
            AppendCsv(csv, sample.PacketTraceSkipped);
            AppendCsv(csv, sample.PacketLogRefreshes);
            AppendCsv(csv, sample.CombatTraceRecords);
            AppendCsv(csv, sample.DebugLogLines);
            AppendCsv(csv, sample.StaticChunksVisible);
            AppendCsv(csv, sample.StaticChunksTotal);
            csv.AppendLine();
        }

        private static void AppendCsv(StringBuilder csv, int value) {
            csv.Append(',');
            csv.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendCsv(StringBuilder csv, uint value) {
            csv.Append(',');
            csv.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendCsv(StringBuilder csv, long value) {
            csv.Append(',');
            csv.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendCsv(StringBuilder csv, bool value) {
            csv.Append(',');
            csv.Append(value ? "true" : "false");
        }

        private static void AppendCsv(StringBuilder csv, string value) {
            csv.Append(',');
            csv.Append('"');
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c == '"') {
                    csv.Append("\"\"");
                } else {
                    csv.Append(c);
                }
            }

            csv.Append('"');
        }

        private void AppendRuntimePerfClickTrace(StringBuilder csv) {
            csv.AppendLine("# clickToMoveTrace");
            RoClickToMoveController clickToMove = mapDiagnosticsClickToMoveController != null
                ? mapDiagnosticsClickToMoveController
                : FindFirstObjectByType<RoClickToMoveController>();
            bool hasLiveClickToMoveTrace = clickToMove != null;
            var traceEntries = new List<string>(200);
            int traceCount = 0;
            if (hasLiveClickToMoveTrace) {
                traceCount = clickToMove.SnapshotRuntimePerfTrace(traceEntries);
            } else if (cachedClickToMoveTraceAvailable) {
                traceCount = cachedClickToMoveTraceCount;
                traceEntries.AddRange(cachedClickToMoveTrace);
            }

            csv.Append("# clickToMoveTraceAvailable: ");
            csv.AppendLine(hasLiveClickToMoveTrace.ToString());
            csv.Append("# clickToMoveTraceCachedAvailable: ");
            csv.AppendLine(cachedClickToMoveTraceAvailable.ToString());
            csv.Append("# clickToMoveTraceCount: ");
            csv.AppendLine(traceCount.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < traceEntries.Count; i++) {
                csv.Append("# clickToMoveTraceEntry: ");
                csv.AppendLine(traceEntries[i]);
            }
        }

        private void SaveMapDiagnosticsReport(string reason, RoLoginEnterMapResult mapResult) {
            if (!RoRuntimeDiagnostics.RuntimePerfLogEnabled ||
                mapDiagnosticsReportWritten ||
                mapResult == null) {
                return;
            }

            try {
                RefreshMapDiagnosticsSnapshot();
                DateTime nowUtc = DateTime.UtcNow;
                string root = Path.Combine(Application.persistentDataPath, "RagnarokDiagnostics", "MapDrainDiagnostics");
                Directory.CreateDirectory(root);
                string mapName = ResolveMapDiagnosticsMapName(mapResult);
                string timestamp = nowUtc.ToString("yyyyMMdd-HHmmss");
                string fileName = $"{timestamp}-{SanitizeDiagnosticsFileName(mapName)}-{++mapDiagnosticsReportSequence}.txt";
                string path = Path.Combine(root, fileName);
                File.WriteAllText(path, BuildMapDiagnosticsReport(reason, mapResult, nowUtc), Encoding.UTF8);
                LastMapDiagnosticsReportPath = path;
                mapDiagnosticsReportWritten = true;
                Debug.Log($"RO map diagnostics saved: {path}");
            } catch (Exception ex) {
                Debug.LogWarning($"RO map diagnostics save failed: {ex.Message}");
            }
        }

        private string BuildMapDiagnosticsReport(string reason, RoLoginEnterMapResult mapResult, DateTime nowUtc) {
            var report = new StringBuilder(1024);
            RoCharacterInfo character = mapResult.CharacterResult != null
                ? mapResult.CharacterResult.SelectedCharacter
                : null;
            RoOnlineWorldController controller = onlineWorldController;
            bool hasLiveWorldState = controller != null;
            bool hasWorldState = hasLiveWorldState || cachedMapDiagnosticsHasWorldState;
            RoMovementDiagnosticsSnapshot movement = hasLiveWorldState
                ? controller.State.MovementDiagnostics
                : cachedMapDiagnosticsMovement;
            int worldActorCount = cachedMapDiagnosticsActorCount;
            int worldMovingActorCount = cachedMapDiagnosticsMovingActorCount;
            int worldPooledActorStateCount = cachedMapDiagnosticsPooledActorStateCount;
            int worldMaxPooledActorStateCount = cachedMapDiagnosticsMaxPooledActorStateCount;
            bool worldHasServerTime = cachedMapDiagnosticsHasServerTime;
            uint worldLatestServerTimeMilliseconds = cachedMapDiagnosticsLatestServerTimeMilliseconds;
            if (hasLiveWorldState) {
                RoOnlineWorldState state = controller.State;
                worldActorCount = state.ActorCount;
                worldMovingActorCount = state.MovingActorCount;
                worldPooledActorStateCount = state.PooledActorStateCount;
                worldMaxPooledActorStateCount = state.MaxPooledActorStateCount;
                worldHasServerTime = state.HasServerTime;
                worldLatestServerTimeMilliseconds = state.LatestServerTimeMilliseconds;
            }
            RoShellCommandStateSnapshot? commandState = session != null
                ? (RoShellCommandStateSnapshot?) session.CommandStateSnapshot()
                : null;

            TimeSpan duration = mapDiagnosticsStartedUtc != DateTime.MinValue
                ? nowUtc - mapDiagnosticsStartedUtc
                : TimeSpan.Zero;

            report.AppendLine("Ragnarok map diagnostics");
            report.Append("reason: ");
            report.AppendLine(string.IsNullOrWhiteSpace(reason) ? "unknown" : reason);
            report.Append("savedUtc: ");
            report.AppendLine(nowUtc.ToString("O"));
            report.Append("startedUtc: ");
            report.AppendLine(mapDiagnosticsStartedUtc != DateTime.MinValue ? mapDiagnosticsStartedUtc.ToString("O") : "unknown");
            report.Append("durationSeconds: ");
            report.AppendLine(Math.Max(0d, duration.TotalSeconds).ToString("F3"));
            report.Append("map: ");
            report.AppendLine(ResolveMapDiagnosticsMapName(mapResult));
            report.Append("character: ");
            report.AppendLine(character != null ? character.Name ?? string.Empty : string.Empty);
            report.Append("characterId: ");
            report.AppendLine(character != null ? character.CharacterId.ToString() : "0");
            report.Append("acceptedEnterStartTime: ");
            report.AppendLine(mapResult.AcceptedEnter.StartTime.ToString());
            report.Append("acceptedEnterPosition: ");
            report.Append(mapResult.AcceptedEnter.Position.X);
            report.Append(',');
            report.Append(mapResult.AcceptedEnter.Position.Y);
            report.Append(" dir=");
            report.AppendLine(mapResult.AcceptedEnter.Position.Direction.ToString());

            report.AppendLine();
            report.AppendLine("[movement]");
            report.Append("accepted: ");
            report.AppendLine(movement.AcceptedMoves.ToString());
            report.Append("ignoredStale: ");
            report.AppendLine(movement.IgnoredStaleMoves.ToString());
            report.Append("ignoredDuplicate: ");
            report.AppendLine(movement.IgnoredDuplicateMoves.ToString());
            report.Append("ignoredInvalid: ");
            report.AppendLine(movement.IgnoredInvalidMoves.ToString());

            RoLiveDrainApplyDiagnosticsSnapshot drain = LiveDrainApplyDiagnostics;
            report.AppendLine();
            report.AppendLine("[drain]");
            report.Append("sessionSequence: ");
            report.AppendLine(session != null ? session.LiveDrainSequence.ToString() : "0");
            report.Append("appliedSequence: ");
            report.AppendLine(appliedLiveDrainSequence.ToString());
            report.Append("pendingDepth: ");
            report.AppendLine(drain.PendingDepth.ToString());
            report.Append("hasApplyAge: ");
            report.AppendLine(drain.HasApplyAge.ToString());
            report.Append("applyAgeMilliseconds: ");
            report.AppendLine(drain.ApplyAgeMilliseconds.ToString());
            report.Append("overflowFallbackCount: ");
            report.AppendLine(drain.OverflowFallbackCount.ToString());
            report.Append("skippedSequenceCount: ");
            report.AppendLine(drain.SkippedSequenceCount.ToString());
            report.Append("sequenceJumpDistance: ");
            report.AppendLine(drain.SequenceJumpDistance.ToString());

            report.AppendLine();
            report.AppendLine("[actionLock]");
            report.Append("available: ");
            report.AppendLine(commandState.HasValue.ToString());
            report.Append("isActionLocked: ");
            report.AppendLine(commandState.HasValue ? commandState.Value.IsActionLocked.ToString() : "False");
            report.Append("remainingMilliseconds: ");
            report.AppendLine(commandState.HasValue ? commandState.Value.ActionLockRemainingMilliseconds.ToString() : "0");
            report.Append("reason: ");
            report.AppendLine(commandState.HasValue ? commandState.Value.ActionLockReason ?? string.Empty : string.Empty);

            report.AppendLine();
            report.AppendLine("[world]");
            report.Append("hasWorldState: ");
            report.AppendLine(hasWorldState.ToString());
            report.Append("source: ");
            report.AppendLine(hasLiveWorldState ? "live" : cachedMapDiagnosticsHasWorldState ? "cached" : "none");
            if (hasWorldState) {
                report.Append("actors: ");
                report.AppendLine(worldActorCount.ToString());
                report.Append("movingActors: ");
                report.AppendLine(worldMovingActorCount.ToString());
                report.Append("pooledActorStates: ");
                report.Append(worldPooledActorStateCount);
                report.Append('/');
                report.AppendLine(worldMaxPooledActorStateCount.ToString());
                report.Append("hasServerTime: ");
                report.AppendLine(worldHasServerTime.ToString());
                report.Append("latestServerTimeMilliseconds: ");
                report.AppendLine(worldLatestServerTimeMilliseconds.ToString());
            }

            report.AppendLine();
            report.AppendLine("[clickToMoveTrace]");
            RoClickToMoveController clickToMove = mapDiagnosticsClickToMoveController != null
                ? mapDiagnosticsClickToMoveController
                : FindFirstObjectByType<RoClickToMoveController>();
            bool hasLiveClickToMoveTrace = clickToMove != null;
            var traceEntries = new List<string>(200);
            int traceCount = 0;
            if (hasLiveClickToMoveTrace) {
                traceCount = clickToMove.SnapshotRuntimePerfTrace(traceEntries);
            } else if (cachedClickToMoveTraceAvailable) {
                traceCount = cachedClickToMoveTraceCount;
                traceEntries.AddRange(cachedClickToMoveTrace);
            }

            report.Append("available: ");
            report.AppendLine(hasLiveClickToMoveTrace.ToString());
            report.Append("cachedAvailable: ");
            report.AppendLine(cachedClickToMoveTraceAvailable.ToString());
            report.Append("source: ");
            report.AppendLine(hasLiveClickToMoveTrace ? "live" : cachedClickToMoveTraceAvailable ? "cached" : "none");
            report.Append("count: ");
            report.AppendLine(traceCount.ToString());
            for (int i = 0; i < traceEntries.Count; i++) {
                report.AppendLine(traceEntries[i]);
            }

            return report.ToString();
        }

        private static string ResolveMapDiagnosticsMapName(RoLoginEnterMapResult mapResult) {
            RoCharacterInfo character = mapResult != null && mapResult.CharacterResult != null
                ? mapResult.CharacterResult.SelectedCharacter
                : null;
            return character != null && !string.IsNullOrWhiteSpace(character.MapName)
                ? character.MapName
                : "unknown-map";
        }

        private static string SanitizeDiagnosticsFileName(string value) {
            string candidate = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            var sanitized = new StringBuilder(candidate.Length);
            for (int i = 0; i < candidate.Length; i++) {
                char c = candidate[i];
                sanitized.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            return sanitized.Length > 0 ? sanitized.ToString() : "unknown";
        }
    }
}
