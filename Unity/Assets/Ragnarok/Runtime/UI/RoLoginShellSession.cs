using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Gameplay;
using Ragnarok.Localization;
using Ragnarok.Network;
using Unity.Profiling;
using UnityEngine;

namespace Ragnarok.UI {
    public enum RoLoginShellStage {
        Login = 0,
        LoadingCharacters = 1,
        CharacterSelect = 2,
        EnteringMap = 3,
        MapEntered = 4,
        Failed = 5,
        /// <summary>Logged in, the character server list waits for a choice (UISelectServerWnd 0x2; the classic UI only).</summary>
        ServerSelect = 6
    }

    [Flags]
    internal enum RoGameplayWindowChange {
        None = 0,
        BasicInfo = 1 << 0,
        Skill = 1 << 1,
        Inventory = 1 << 2,
        Equipment = 1 << 3,
        Hotkey = 1 << 4,
        Quest = 1 << 5,
        Functional = 1 << 6,
        StatusIcon = 1 << 7,
        Status = 1 << 8,
        All = BasicInfo | Skill | Inventory | Equipment | Hotkey | Quest | Functional | StatusIcon | Status
    }

    [Flags]
    internal enum RoFunctionalWindowChange {
        None = 0,
        Npc = 1 << 0,
        Storage = 1 << 1,
        Party = 1 << 2,
        Friends = 1 << 3,
        Guild = 1 << 4,
        Trade = 1 << 5,
        Cart = 1 << 6,
        Pet = 1 << 7,
        Homunculus = 1 << 8,
        Mercenary = 1 << 9,
        Vending = 1 << 10,
        BuyingStore = 1 << 11,
        Settings = 1 << 12,
        Bank = 1 << 13,
        Rodex = 1 << 14,
        Attendance = 1 << 15,
        ChatRoom = 1 << 16,
        All = Npc | Storage | Party | Friends | Guild | Trade | Cart | Pet | Homunculus | Mercenary | Vending | BuyingStore | Settings | Bank | Rodex | Attendance | ChatRoom
    }

    public readonly struct RoLoginShellCharacter {
        private const int UnixTimestampPlausibilityFloor = 946684800;

        public RoLoginShellCharacter(RoCharacterInfo source) {
            if (source == null) {
                CharacterId = 0;
                Name = string.Empty;
                Slot = 0;
                BaseLevel = 0;
                JobLevel = 0;
                JobId = 0;
                MapName = string.Empty;
                Experience = 0;
                JobExperience = 0;
                Hp = 0;
                MaxHp = 0;
                Sp = 0;
                MaxSp = 0;
                Head = 0;
                Body = 0;
                Weapon = 0;
                Accessory = 0;
                Shield = 0;
                Accessory2 = 0;
                Accessory3 = 0;
                HeadPalette = 0;
                BodyPalette = 0;
                HairColor = 0;
                Strength = 0;
                Agility = 0;
                Vitality = 0;
                Intelligence = 0;
                Dexterity = 0;
                Luck = 0;
                Sex = 1;
                DeleteRevisionDate = 0;
                SlotChangeCount = 0;
                NameChangeCount = 0;
                return;
            }

            CharacterId = source.CharacterId;
            Name = source.Name ?? string.Empty;
            Slot = source.Slot;
            BaseLevel = source.BaseLevel;
            JobLevel = source.JobLevel;
            JobId = source.JobId;
            MapName = source.MapName ?? string.Empty;
            Experience = source.Experience;
            JobExperience = source.JobExperience;
            Hp = source.Hp;
            MaxHp = source.MaxHp;
            Sp = source.Sp;
            MaxSp = source.MaxSp;
            Head = source.Head;
            Body = source.Body;
            Weapon = source.Weapon;
            Accessory = source.Accessory;
            Shield = source.Shield;
            Accessory2 = source.Accessory2;
            Accessory3 = source.Accessory3;
            HeadPalette = source.HeadPalette;
            BodyPalette = source.BodyPalette;
            HairColor = source.HairColor;
            Strength = source.Strength;
            Agility = source.Agility;
            Vitality = source.Vitality;
            Intelligence = source.Intelligence;
            Dexterity = source.Dexterity;
            Luck = source.Luck;
            Sex = source.Sex;
            DeleteRevisionDate = NormalizeDeleteRevisionDate(source.DeleteRevisionDate);
            SlotChangeCount = source.SlotChangeCount;
            NameChangeCount = source.NameChangeCount;
        }

        internal RoLoginShellCharacter(
            int characterId,
            string name,
            byte slot,
            short baseLevel,
            int jobLevel,
            int jobId,
            string mapName,
            long experience,
            long jobExperience,
            int hp,
            int maxHp,
            int sp,
            int maxSp,
            short head,
            short body,
            short weapon,
            short accessory,
            short shield,
            short accessory2,
            short accessory3,
            short headPalette,
            short bodyPalette,
            byte hairColor,
            byte strength,
            byte agility,
            byte vitality,
            byte intelligence,
            byte dexterity,
            byte luck,
            byte sex,
            int deleteRevisionDate = 0) {
            CharacterId = characterId;
            Name = name ?? string.Empty;
            Slot = slot;
            BaseLevel = baseLevel;
            JobLevel = jobLevel;
            JobId = jobId;
            MapName = mapName ?? string.Empty;
            Experience = experience;
            JobExperience = jobExperience;
            Hp = hp;
            MaxHp = maxHp;
            Sp = sp;
            MaxSp = maxSp;
            Head = head;
            Body = body;
            Weapon = weapon;
            Accessory = accessory;
            Shield = shield;
            Accessory2 = accessory2;
            Accessory3 = accessory3;
            HeadPalette = headPalette;
            BodyPalette = bodyPalette;
            HairColor = hairColor;
            Strength = strength;
            Agility = agility;
            Vitality = vitality;
            Intelligence = intelligence;
            Dexterity = dexterity;
            Luck = luck;
            Sex = sex;
            DeleteRevisionDate = NormalizeDeleteRevisionDate(deleteRevisionDate);
            SlotChangeCount = 0;
            NameChangeCount = 0;
        }

        private static int NormalizeDeleteRevisionDate(int value) {
            if (value <= 0) {
                return 0;
            }

            if (value >= UnixTimestampPlausibilityFloor) {
                return value;
            }

            long absolute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + value;
            return absolute >= int.MaxValue ? int.MaxValue : (int) absolute;
        }

        public int CharacterId { get; }
        public string Name { get; }
        public byte Slot { get; }
        public short BaseLevel { get; }
        public int JobLevel { get; }
        public int JobId { get; }
        public string MapName { get; }
        public long Experience { get; }
        public long JobExperience { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Sp { get; }
        public int MaxSp { get; }
        public short Head { get; }
        public short Body { get; }
        public short Weapon { get; }
        public short Accessory { get; }
        public short Shield { get; }
        public short Accessory2 { get; }
        public short Accessory3 { get; }
        public short HeadPalette { get; }
        public short BodyPalette { get; }
        public byte HairColor { get; }
        public byte Strength { get; }
        public byte Agility { get; }
        public byte Vitality { get; }
        public byte Intelligence { get; }
        public byte Dexterity { get; }
        public byte Luck { get; }
        public byte Sex { get; }
        public int DeleteRevisionDate { get; }
        /// <summary>The character block's slot-change and rename counts (the selection shows Add_Ons.bmp while either is left).</summary>
        public int SlotChangeCount { get; }
        public int NameChangeCount { get; }
    }

    public readonly struct RoLiveDrainSnapshot {
        public RoLiveDrainSnapshot(int sequence, RoMapEntryDrainResult drain) {
            Sequence = sequence;
            Drain = drain ?? RoMapEntryDrainResult.Empty;
        }

        public int Sequence { get; }
        public RoMapEntryDrainResult Drain { get; }
    }

    public readonly struct RoLiveDrainApplyDiagnosticsSnapshot {
        internal RoLiveDrainApplyDiagnosticsSnapshot(
            int overflowFallbackCount,
            long skippedSequenceCount,
            long sequenceJumpDistance,
            int pendingDepth,
            bool hasApplyAge,
            int applyAgeMilliseconds
        ) {
            OverflowFallbackCount = overflowFallbackCount;
            SkippedSequenceCount = skippedSequenceCount;
            SequenceJumpDistance = sequenceJumpDistance;
            PendingDepth = pendingDepth;
            HasApplyAge = hasApplyAge;
            ApplyAgeMilliseconds = Math.Max(0, applyAgeMilliseconds);
        }

        public int OverflowFallbackCount { get; }
        public long SkippedSequenceCount { get; }
        public long SequenceJumpDistance { get; }
        public int PendingDepth { get; }
        public bool HasApplyAge { get; }
        public int ApplyAgeMilliseconds { get; }
    }

    public sealed class RoLoginShellSnapshot {
        public RoLoginShellSnapshot(
            RoLoginShellStage stage,
            bool isBusy,
            string statusText,
            string errorText,
            int actionLockRemainingMilliseconds,
            string actionLockReason,
            string selectedCharacterName,
            RoLoginShellCharacter[] characters,
            RoPacketTraceEntry[] packetTrace,
            RoLoginEnterMapResult mapResult,
            RoGameplaySnapshot gameplay,
            bool isMapTransitionPending,
            bool isMapTransitionLoading,
            bool isSameMapTransitionPending,
            RoMapEntryDrainResult latestLiveDrain,
            int liveDrainSequence,
            RoLiveDrainSnapshot[] recentLiveDrains,
            RoHcAcceptEnter2 slotLimits = default,
            RoHcNotifyCharList pageInfo = default,
            int characterListTotalSlots = 0,
            bool hasLiveDrainPublishedTickCount = false,
            int liveDrainPublishedTickCount = 0,
            RoReconnectState reconnectState = RoReconnectState.Stable,
            string reconnectReason = "",
            bool canReconnect = false) {
            Stage = stage;
            IsBusy = isBusy;
            StatusText = statusText ?? string.Empty;
            ErrorText = errorText ?? string.Empty;
            ActionLockRemainingMilliseconds = Math.Max(0, actionLockRemainingMilliseconds);
            ActionLockReason = actionLockReason ?? string.Empty;
            SelectedCharacterName = selectedCharacterName ?? string.Empty;
            Characters = characters ?? Array.Empty<RoLoginShellCharacter>();
            PacketTrace = packetTrace ?? Array.Empty<RoPacketTraceEntry>();
            MapResult = mapResult;
            Gameplay = gameplay ?? RoGameplaySnapshot.Empty;
            IsMapTransitionPending = isMapTransitionPending;
            IsMapTransitionLoading = isMapTransitionLoading;
            IsSameMapTransitionPending = isSameMapTransitionPending;
            LatestLiveDrain = latestLiveDrain ?? RoMapEntryDrainResult.Empty;
            LiveDrainSequence = liveDrainSequence;
            RecentLiveDrains = recentLiveDrains ?? Array.Empty<RoLiveDrainSnapshot>();
            SlotLimits = slotLimits;
            PageInfo = pageInfo;
            CharacterListTotalSlots = Math.Max(0, characterListTotalSlots);
            HasLiveDrainPublishedTickCount = hasLiveDrainPublishedTickCount;
            LiveDrainPublishedTickCount = liveDrainPublishedTickCount;
            ReconnectState = reconnectState;
            ReconnectReason = reconnectReason ?? string.Empty;
            CanReconnect = canReconnect;
        }

        public RoLoginShellStage Stage { get; }
        public bool IsBusy { get; }
        public string StatusText { get; }
        public string ErrorText { get; }
        public int ActionLockRemainingMilliseconds { get; }
        public string ActionLockReason { get; }
        public bool IsActionLocked => ActionLockRemainingMilliseconds > 0;
        public string SelectedCharacterName { get; }
        public RoLoginShellCharacter[] Characters { get; }
        public RoPacketTraceEntry[] PacketTrace { get; }
        public RoLoginEnterMapResult MapResult { get; }
        public RoGameplaySnapshot Gameplay { get; }
        public bool IsMapTransitionPending { get; }
        public bool IsMapTransitionLoading { get; }
        public bool IsSameMapTransitionPending { get; }
        public RoMapEntryDrainResult LatestLiveDrain { get; }
        public int LiveDrainSequence { get; }
        public RoLiveDrainSnapshot[] RecentLiveDrains { get; }
        public RoHcAcceptEnter2 SlotLimits { get; }
        public RoHcNotifyCharList PageInfo { get; }
        public int CharacterListTotalSlots { get; }
        public bool HasLiveDrainPublishedTickCount { get; }
        public int LiveDrainPublishedTickCount { get; }
        public RoReconnectState ReconnectState { get; }
        public string ReconnectReason { get; }
        public bool ConnectionLost => ReconnectState == RoReconnectState.ConnectionLost;
        public string ConnectionLostReason => ConnectionLost ? ReconnectReason : string.Empty;
        public bool CanReconnect { get; }
    }

    public readonly struct RoShellLiveDrainSnapshot {
        public RoShellLiveDrainSnapshot(
            RoMapEntryDrainResult latestLiveDrain,
            int liveDrainSequence,
            RoLiveDrainSnapshot[] recentLiveDrains,
            bool hasLiveDrainPublishedTickCount = false,
            int liveDrainPublishedTickCount = 0) {
            LatestLiveDrain = latestLiveDrain ?? RoMapEntryDrainResult.Empty;
            LiveDrainSequence = liveDrainSequence;
            RecentLiveDrains = recentLiveDrains ?? Array.Empty<RoLiveDrainSnapshot>();
            HasLiveDrainPublishedTickCount = hasLiveDrainPublishedTickCount;
            LiveDrainPublishedTickCount = liveDrainPublishedTickCount;
        }

        public RoMapEntryDrainResult LatestLiveDrain { get; }
        public int LiveDrainSequence { get; }
        public RoLiveDrainSnapshot[] RecentLiveDrains { get; }
        public bool HasLiveDrainPublishedTickCount { get; }
        public int LiveDrainPublishedTickCount { get; }
    }

    internal readonly struct RoShellBufferedLiveDrainSnapshot {
        public RoShellBufferedLiveDrainSnapshot(
            RoMapEntryDrainResult latestLiveDrain,
            int liveDrainSequence,
            IReadOnlyList<RoLiveDrainSnapshot> recentLiveDrains,
            bool hasLiveDrainPublishedTickCount = false,
            int liveDrainPublishedTickCount = 0) {
            LatestLiveDrain = latestLiveDrain ?? RoMapEntryDrainResult.Empty;
            LiveDrainSequence = liveDrainSequence;
            RecentLiveDrains = recentLiveDrains ?? Array.Empty<RoLiveDrainSnapshot>();
            HasLiveDrainPublishedTickCount = hasLiveDrainPublishedTickCount;
            LiveDrainPublishedTickCount = liveDrainPublishedTickCount;
        }

        public RoMapEntryDrainResult LatestLiveDrain { get; }
        public int LiveDrainSequence { get; }
        public IReadOnlyList<RoLiveDrainSnapshot> RecentLiveDrains { get; }
        public bool HasLiveDrainPublishedTickCount { get; }
        public int LiveDrainPublishedTickCount { get; }
    }

    public readonly struct RoShellCommandStateSnapshot {
        public RoShellCommandStateSnapshot(
            RoLoginShellStage stage,
            bool isBusy,
            bool isMapTransitionPending,
            int actionLockRemainingMilliseconds,
            string actionLockReason) {
            Stage = stage;
            IsBusy = isBusy;
            IsMapTransitionPending = isMapTransitionPending;
            ActionLockRemainingMilliseconds = Math.Max(0, actionLockRemainingMilliseconds);
            ActionLockReason = actionLockReason ?? string.Empty;
        }

        public RoLoginShellStage Stage { get; }
        public bool IsBusy { get; }
        public bool IsMapTransitionPending { get; }
        public int ActionLockRemainingMilliseconds { get; }
        public string ActionLockReason { get; }
        public bool IsActionLocked => ActionLockRemainingMilliseconds > 0;
        public bool CanSendMapCommand =>
            Stage == RoLoginShellStage.MapEntered &&
            !IsBusy &&
            !IsMapTransitionPending &&
            !IsActionLocked;
    }

    public readonly struct RoShellMapHudSnapshot {
        public RoShellMapHudSnapshot(
            RoLoginShellStage stage,
            bool isBusy,
            string statusText,
            string errorText,
            int actionLockRemainingMilliseconds,
            string mapName,
            bool hasMapEntryPosition,
            int mapEntryX,
            int mapEntryY,
            int mapEntryDirection) {
            Stage = stage;
            IsBusy = isBusy;
            StatusText = statusText ?? string.Empty;
            ErrorText = errorText ?? string.Empty;
            ActionLockRemainingMilliseconds = Math.Max(0, actionLockRemainingMilliseconds);
            MapName = mapName ?? string.Empty;
            HasMapEntryPosition = hasMapEntryPosition;
            MapEntryX = mapEntryX;
            MapEntryY = mapEntryY;
            MapEntryDirection = mapEntryDirection;
        }

        public RoLoginShellStage Stage { get; }
        public bool IsBusy { get; }
        public string StatusText { get; }
        public string ErrorText { get; }
        public int ActionLockRemainingMilliseconds { get; }
        public bool IsActionLocked => ActionLockRemainingMilliseconds > 0;
        public string MapName { get; }
        public bool HasMapEntryPosition { get; }
        public int MapEntryX { get; }
        public int MapEntryY { get; }
        public int MapEntryDirection { get; }
    }

    public readonly struct RoShellBasicInfoSnapshot {
        public RoShellBasicInfoSnapshot(bool hasCharacter, RoLoginShellCharacter character, RoPlayerStatusSnapshot status) {
            HasCharacter = hasCharacter;
            Character = character;
            Status = status ?? RoPlayerStatusSnapshot.Empty;
        }

        public bool HasCharacter { get; }
        public RoLoginShellCharacter Character { get; }
        public RoPlayerStatusSnapshot Status { get; }
    }

    public sealed class RoShellFunctionalWindowSnapshot {
        public static readonly RoShellFunctionalWindowSnapshot Empty = new RoShellFunctionalWindowSnapshot(
            default,
            RoGameplayFunctionalSnapshot.Empty,
            RoMapEntryDrainResult.Empty,
            Array.Empty<RoLiveDrainSnapshot>());

        public RoShellFunctionalWindowSnapshot(
            RoShellCommandStateSnapshot commandState,
            RoGameplayFunctionalSnapshot functional,
            RoMapEntryDrainResult latestLiveDrain,
            RoLiveDrainSnapshot[] recentLiveDrains) {
            CommandState = commandState;
            Functional = functional ?? RoGameplayFunctionalSnapshot.Empty;
            LatestLiveDrain = latestLiveDrain ?? RoMapEntryDrainResult.Empty;
            RecentLiveDrains = recentLiveDrains ?? Array.Empty<RoLiveDrainSnapshot>();
        }

        public RoShellCommandStateSnapshot CommandState { get; }
        public RoGameplayFunctionalSnapshot Functional { get; }
        public RoMapEntryDrainResult LatestLiveDrain { get; }
        public RoLiveDrainSnapshot[] RecentLiveDrains { get; }
        public RoLoginShellStage Stage => CommandState.Stage;
        public bool IsBusy => CommandState.IsBusy;
        public bool IsMapTransitionPending => CommandState.IsMapTransitionPending;
        public int ActionLockRemainingMilliseconds => CommandState.ActionLockRemainingMilliseconds;
        public string ActionLockReason => CommandState.ActionLockReason;
        public bool IsActionLocked => CommandState.IsActionLocked;
    }

    public sealed class RoLoginShellSession : IDisposable, Ragnarok.World.IRoPickRelations {
        private const int MaxRecentLiveDrains = 64;
        private const int DefaultLiveKeepAliveIntervalMilliseconds = 10000;
        private static readonly ProfilerMarker NetworkApplyPacketMarker = new ProfilerMarker("RO.Network.ApplyPacket");
        private static readonly ProfilerMarker FullSnapshotMarker = new ProfilerMarker("RO.Session.Snapshot.Full");
        private static readonly ProfilerMarker FunctionalSnapshotMarker = new ProfilerMarker("RO.Session.Snapshot.Functional");
        private static readonly ProfilerMarker MapHudSnapshotMarker = new ProfilerMarker("RO.Session.Snapshot.MapHud");

        private sealed class RoInstrumentedPacketTraceSink : IRoPacketTraceSink {
            private static readonly ProfilerMarker WriteMarker = new ProfilerMarker("RO.Debug.PacketTrace.Write");

            private readonly RoPacketTraceBuffer inner;

            public RoInstrumentedPacketTraceSink(RoPacketTraceBuffer inner) {
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public bool IsEnabled => inner.IsEnabled;

            public void Write(RoPacketTraceEntry entry) {
                if (!inner.IsEnabled) {
                    RoRuntimePerfCounters.IncrementPacketTraceSkipped();
                    return;
                }

                using (WriteMarker.Auto()) {
                    inner.Write(entry);
                    RoRuntimePerfCounters.IncrementPacketTraceWrite();
                }
            }
        }

        private enum CharacterDeleteMutation {
            Reserve = 0,
            Confirm = 1,
            Cancel = 2,
            Delete = 3
        }

        private readonly object sync = new object();
        private readonly RoLocalizationService localization;
        private readonly RoPacketTraceBuffer traceBuffer;
        private readonly RoRuntimeDiagnosticsOptions diagnosticsOptions;
        private readonly RoShellConnectionWorkflow connectionWorkflow;
        private readonly RoShellTaskRunner taskRunner = new RoShellTaskRunner();
        private readonly RoShellProfileState profileState;
        private readonly RoShellLiveDrainHistory liveDrainHistory = new RoShellLiveDrainHistory(MaxRecentLiveDrains);
        private readonly RoServerClockSync serverClockSync = new RoServerClockSync();
        private readonly RoShellMapSessionState mapSessionState;
        private readonly RoShellMapTransitionWorkflow mapTransitionWorkflow = new RoShellMapTransitionWorkflow();
        private readonly RoShellCharacterState characterState = new RoShellCharacterState();
        private readonly RoShellUiState uiState;
        private readonly RoShellLiveSendExecutor liveSendExecutor;
        private readonly RoGameplayState gameplayState = new RoGameplayState();
        private readonly RoPartyRuntime partyState = new RoPartyRuntime(null, null, null, null, null, null, null);
        private readonly RoMinimapMarkerState minimapMarkers = new RoMinimapMarkerState();

        /// <summary>The minimap marks the map server sent on this map (guildmates, quest NPCs, the boss).</summary>
        public RoMinimapMarkerState MinimapMarkers => minimapMarkers;
        private readonly RoFriendsRuntime friendsState = new RoFriendsRuntime(null, null, null);
        private readonly RoChatRoomRuntime chatRoomState = new RoChatRoomRuntime();
        private readonly RoBuyingStoreRuntime buyingStoreState = new RoBuyingStoreRuntime();
        private readonly RoRodexRuntime rodexState = new RoRodexRuntime();
        private readonly RoMailRuntime mailState = new RoMailRuntime();
        private readonly RoAttendanceRuntime attendanceState = new RoAttendanceRuntime();
        private readonly RoVendingRuntime vendingState = new RoVendingRuntime();
        private readonly Queue<RoZcSkillInfo> pendingAutoRunSkills = new Queue<RoZcSkillInfo>(4);
        private readonly RoShellActionLockState actionLockState = new RoShellActionLockState();
        private readonly RoShellSnapshotBuilder snapshotBuilder;
        private readonly RoShellDirectGameplaySender directGameplaySender;
        private readonly RoShellGameplayPacketWorkflow gameplayPacketWorkflow;
        private readonly RoShellHotkeyInventoryChatWorkflow hotkeyInventoryChatWorkflow;
        private readonly RoShellNpcInteractionWorkflow npcInteractionWorkflow;
        private readonly RoShellStorageWorkflow storageWorkflow;
        private readonly RoShellBankWorkflow bankWorkflow;
        private readonly RoShellRodexWorkflow rodexWorkflow;
        private readonly RoShellSocialEconomyWorkflow socialEconomyWorkflow;
        private bool friendNamesPending;
        private readonly RoShellCombatSkillWorkflow combatSkillWorkflow;
        private readonly RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck;
        private readonly Func<string, Task> sameMapTransitionBeforeAck;

        private RoShellCharacterRuntimeSession characterRuntimeSession;
        // AC_ACCEPT_LOGIN kept while the server list (UISelectServerWnd 0x2) waits for a choice
        private RoShellPendingLogin pendingLogin;
        private RoShellCharacterRequest lastMapEntryRequest;
        private bool hasLastMapEntryRequest;
        private int phase11UiChangeSequence;
        private int gameplayChangeSequence;
        private int basicInfoChangeSequence;
        private int statusWindowChangeSequence;
        private int skillWindowChangeSequence;
        private int inventoryWindowChangeSequence;
        private int equipmentWindowChangeSequence;
        private int hotkeyWindowChangeSequence;
        private int questWindowChangeSequence;
        private int functionalWindowChangeSequence;
        private int statusIconChangeSequence;
        private int functionalNpcChangeSequence;
        private int functionalStorageChangeSequence;
        private int functionalPartyChangeSequence;
        private int functionalFriendsChangeSequence;
        private int functionalGuildChangeSequence;
        private int functionalTradeChangeSequence;
        private int functionalCartChangeSequence;
        private int functionalBankChangeSequence;
        private int functionalRodexChangeSequence;
        private int functionalAttendanceChangeSequence;
        private int functionalPetChangeSequence;
        private int functionalHomunculusChangeSequence;
        private int functionalMercenaryChangeSequence;
        private int functionalVendingChangeSequence;
        private int functionalBuyingStoreChangeSequence;
        private int functionalSettingsChangeSequence;
        private int playerViewEquipmentSequence;
        private RoZcEquipWinMicroscope latestPlayerViewEquipment;
        private int navigationActiveSequence;
        private readonly List<RoZcEntryQueue> pendingEntryQueue = new List<RoZcEntryQueue>();
        private RoZcNavigationActive latestNavigationActive;
        private RoFunctionalWindowChange lastFunctionalWindowChanges = RoFunctionalWindowChange.All;
        private bool disposed;

        public RoLoginShellSession(
            RoLocalizationService localization = null,
            RoServerProfile profile = default,
            int traceCapacity = 1024,
            int timeoutMilliseconds = 5000,
            int liveKeepAliveIntervalMilliseconds = DefaultLiveKeepAliveIntervalMilliseconds,
            Func<int, int, int, Task> runtimeUseSkillOnTarget = null,
            Func<int, int, int, int, Task> runtimeUseSkillOnGround = null,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null,
            Func<string, Task> sameMapTransitionBeforeAck = null,
            RoRuntimeDiagnosticsOptions diagnosticsOptions = null,
            Action<int, int, int> targetSkillSent = null) {
            this.localization = localization ?? RoLocalizationService.Empty;
            this.loadRuntimeMapBeforeAck = loadRuntimeMapBeforeAck;
            this.sameMapTransitionBeforeAck = sameMapTransitionBeforeAck;
            this.diagnosticsOptions = ResolveDiagnosticsOptions(diagnosticsOptions);
            RoRuntimeDiagnostics.Apply(this.diagnosticsOptions);
            profileState = new RoShellProfileState(profile);
            int resolvedLiveKeepAliveIntervalMilliseconds = liveKeepAliveIntervalMilliseconds <= 0
                ? DefaultLiveKeepAliveIntervalMilliseconds
                : liveKeepAliveIntervalMilliseconds;
            traceBuffer = new RoPacketTraceBuffer(traceCapacity <= 0 ? 1024 : traceCapacity);
            traceBuffer.IsEnabled = this.diagnosticsOptions.EffectivePacketTraceEnabled;
            TimeoutMilliseconds = timeoutMilliseconds <= 0 ? 5000 : timeoutMilliseconds;
            connectionWorkflow = new RoShellConnectionWorkflow(
                // always present: the buffer's IsEnabled gates the writes, so diagnostics can turn tracing on live (!trace)
                new RoInstrumentedPacketTraceSink(traceBuffer),
                TimeoutMilliseconds,
                loadRuntimeMapBeforeAck,
                this.diagnosticsOptions.EnableVerboseNetworkTraceMessages);
            mapSessionState = new RoShellMapSessionState(
                resolvedLiveKeepAliveIntervalMilliseconds,
                serverClockSync,
                HandleLiveReceiveDrain,
                HandleLiveReceiveError);
            RoNetworkRuntimeDiagnostics.DiagnosticRecorded += HandleNetworkDiagnosticRecorded;
            uiState = new RoShellUiState(RoLoginShellStage.Login, Text("ui.login.status"));
            liveSendExecutor = new RoShellLiveSendExecutor(
                sync,
                () => disposed,
                IsCurrentMapSessionLocked,
                (status, error) => uiState.SetError(status, error),
                () => Text("ui.error.connection"));
            directGameplaySender = new RoShellDirectGameplaySender(
                sync,
                () => disposed,
                IsCurrentMapSessionLocked,
                TryGetOpenMapSessionLocked,
                liveSendExecutor,
                actionLockState,
                LocalActorIdLocked,
                status => uiState.SetStatus(status));
            gameplayPacketWorkflow = new RoShellGameplayPacketWorkflow(
                sync,
                ThrowIfDisposed,
                ThrowIfBusyLocked,
                RequireOpenMapSessionLocked,
                () => gameplayState.Snapshot(),
                liveSendExecutor,
                actionLockState,
                LocalActorIdLocked,
                status => uiState.SetStatus(status));
            hotkeyInventoryChatWorkflow = new RoShellHotkeyInventoryChatWorkflow(
                sync,
                ThrowIfDisposed,
                ThrowIfBusyLocked,
                RequireOpenMapSessionLocked,
                () => gameplayState.Snapshot(),
                LocalActorAidLocked,
                LocalActorIdLocked,
                () => characterState.SelectedCharacterName,
                () => profileState.Profile.LegacyTextCodePage,
                gameplayState,
                liveSendExecutor,
                actionLockState,
                () => MarkGameplayChangedLocked(RoGameplayWindowChange.Hotkey),
                status => uiState.SetStatus(status),
                targetSkillSent);
            npcInteractionWorkflow = new RoShellNpcInteractionWorkflow(
                gameplayPacketWorkflow,
                () => profileState.Profile.LegacyTextCodePage,
                () => profileState.Profile,
                npcId => {
                    gameplayState.AdvanceNpcDialogPageLocally(npcId);
                    MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Npc);
                },
                () => {
                    gameplayState.ClearNpcDialogFullyLocally();
                    MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Npc);
                });
            storageWorkflow = new RoShellStorageWorkflow(
                gameplayPacketWorkflow,
                () => profileState.Profile);
            bankWorkflow = new RoShellBankWorkflow(
                gameplayPacketWorkflow,
                LocalActorAidLocked,
                () => profileState.Profile);
            rodexWorkflow = new RoShellRodexWorkflow(
                gameplayPacketWorkflow,
                () => profileState.Profile.LegacyTextCodePage,
                () => profileState.Profile);
            socialEconomyWorkflow = new RoShellSocialEconomyWorkflow(
                gameplayPacketWorkflow,
                () => profileState.Profile.LegacyTextCodePage,
                () => profileState.Profile);
            combatSkillWorkflow = new RoShellCombatSkillWorkflow(
                gameplayPacketWorkflow,
                runtimeUseSkillOnTarget,
                runtimeUseSkillOnGround,
                targetSkillSent);
            snapshotBuilder = new RoShellSnapshotBuilder(
                uiState,
                characterState,
                traceBuffer,
                mapSessionState,
                gameplayState,
                liveDrainHistory,
                actionLockState,
                IsBusyLocked,
                HasLastMapEntryRequestLocked);
        }

        public int TimeoutMilliseconds { get; }
        public RoPacketTraceBuffer TraceBuffer => traceBuffer;
        public RoRuntimeDiagnosticsOptions DiagnosticsOptions => diagnosticsOptions;
        public RoServerClockSync ServerClockSync => serverClockSync;

        private static RoRuntimeDiagnosticsOptions ResolveDiagnosticsOptions(RoRuntimeDiagnosticsOptions options) {
            if (options != null) {
                return RoRuntimeDiagnostics.Normalize(options);
            }

            return RoRuntimeDiagnostics.Normalize(null);
        }

        public event Action<int> BasicInfoSequenceChanged;

        public RoServerProfile Profile {
            get {
                lock (sync) {
                    return profileState.Profile;
                }
            }
        }

        public void SetProfile(RoServerProfile value) {
            lock (sync) {
                ThrowIfBusyLocked();
                if (profileState.Matches(value)) {
                    return;
                }

                ClearCharacterRuntimeLocked();
                ClearLastMapEntryRequestLocked();
                profileState.Set(value);
            }
        }

        public bool HasManualReconnectPrompt {
            get {
                lock (sync) {
                    return uiState.CanManualReconnect && hasLastMapEntryRequest;
                }
            }
        }

        public RoLoginShellSnapshot Snapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("session");
            using (RoProfilerMarkers.SessionBuildSnapshot.Auto())
            using (FullSnapshotMarker.Auto()) {
                lock (sync) {
                    return snapshotBuilder.Build(DateTime.UtcNow);
                }
            }
        }

        public RoLoginShellSnapshot Phase11UiSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("phase11-ui");
            using (RoProfilerMarkers.SessionBuildSnapshot.Auto())
            lock (sync) {
                return snapshotBuilder.BuildPhase11Ui(DateTime.UtcNow);
            }
        }

        public int Phase11UiChangeSequence {
            get {
                lock (sync) {
                    return phase11UiChangeSequence;
                }
            }
        }

        public int LiveDrainSequence {
            get {
                lock (sync) {
                    return liveDrainHistory.Sequence;
                }
            }
        }

        public bool IsStableAppliedMapResult(RoLoginEnterMapResult appliedMapResult) {
            lock (sync) {
                return appliedMapResult != null &&
                    uiState.Stage == RoLoginShellStage.MapEntered &&
                    !IsBusyLocked() &&
                    !uiState.IsMapTransitionPending &&
                    !uiState.IsMapTransitionLoading &&
                    !uiState.IsSameMapTransitionPending &&
                    ReferenceEquals(mapSessionState.MapResult, appliedMapResult);
            }
        }

        public RoShellLiveDrainSnapshot LiveDrainSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("live-drain");
            lock (sync) {
                return new RoShellLiveDrainSnapshot(
                    liveDrainHistory.LatestDrain,
                    liveDrainHistory.Sequence,
                    liveDrainHistory.SnapshotRecent(),
                    liveDrainHistory.HasLatestPublishTickCount,
                    liveDrainHistory.LatestPublishTickCount);
            }
        }

        internal RoShellBufferedLiveDrainSnapshot LiveDrainSnapshot(List<RoLiveDrainSnapshot> recentLiveDrains) {
            if (recentLiveDrains == null) {
                throw new ArgumentNullException(nameof(recentLiveDrains));
            }

            RoRuntimePerfCounters.IncrementSnapshot("live-drain-buffered");
            lock (sync) {
                liveDrainHistory.CopyRecentInto(recentLiveDrains);
                return new RoShellBufferedLiveDrainSnapshot(
                    liveDrainHistory.LatestDrain,
                    liveDrainHistory.Sequence,
                    recentLiveDrains,
                    liveDrainHistory.HasLatestPublishTickCount,
                    liveDrainHistory.LatestPublishTickCount);
            }
        }

        public RoShellCommandStateSnapshot CommandStateSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("command-state");
            lock (sync) {
                DateTime nowUtc = DateTime.UtcNow;
                int actionLockRemaining = actionLockState.GetRemainingMilliseconds(nowUtc);
                return new RoShellCommandStateSnapshot(
                    uiState.Stage,
                    IsBusyLocked(),
                    uiState.IsMapTransitionPending,
                    actionLockRemaining,
                    actionLockRemaining > 0 ? actionLockState.Reason : string.Empty);
            }
        }

        public RoShellMapHudSnapshot MapHudSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("map-hud");
            using (MapHudSnapshotMarker.Auto()) {
                lock (sync) {
                    DateTime nowUtc = DateTime.UtcNow;
                    int actionLockRemaining = actionLockState.GetRemainingMilliseconds(nowUtc);
                    RoLoginEnterMapResult mapResult = mapSessionState.MapResult;
                    bool hasMapEntryPosition = mapResult != null;
                    return new RoShellMapHudSnapshot(
                        uiState.Stage,
                        IsBusyLocked(),
                        uiState.StatusText,
                        uiState.ErrorText,
                        actionLockRemaining,
                        mapResult?.CharacterResult?.SelectedCharacter?.MapName,
                        hasMapEntryPosition,
                        hasMapEntryPosition ? mapResult.AcceptedEnter.Position.X : 0,
                        hasMapEntryPosition ? mapResult.AcceptedEnter.Position.Y : 0,
                        hasMapEntryPosition ? mapResult.AcceptedEnter.Position.Direction : 0);
                }
            }
        }

        public RoShellBasicInfoSnapshot BasicInfoSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("basic-info");
            lock (sync) {
                bool hasCharacter = TryResolveBasicInfoCharacterLocked(out RoLoginShellCharacter character);
                return new RoShellBasicInfoSnapshot(
                    hasCharacter,
                    character,
                    gameplayState.StatusSnapshot());
            }
        }

        public RoGameplaySkillSnapshot SkillSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("skill");
            lock (sync) {
                return gameplayState.SkillSnapshot();
            }
        }

        /// <summary>A ZC_AUTORUN_SKILL is waiting (an unlocked read for the per-frame check; the dequeue takes the lock).</summary>
        public bool HasPendingAutoRunSkills => pendingAutoRunSkills.Count > 0;

        public bool TryDequeueAutoRunSkill(out RoZcSkillInfo skillInfo) {
            lock (sync) {
                if (pendingAutoRunSkills.Count > 0) {
                    skillInfo = pendingAutoRunSkills.Dequeue();
                    return true;
                }
            }

            skillInfo = default;
            return false;
        }

        public RoGameplayInventorySnapshot InventorySnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("inventory");
            lock (sync) {
                return gameplayState.InventorySnapshot();
            }
        }

        public RoGameplayEquipmentSnapshot EquipmentSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("equipment");
            lock (sync) {
                return gameplayState.EquipmentSnapshot();
            }
        }

        /// <summary>The entry-queue packets received since the last call, in order (the classic battleground windows take them).</summary>
        public void DrainEntryQueue(List<RoZcEntryQueue> into) {
            lock (sync) {
                if (pendingEntryQueue.Count == 0) return;
                into.AddRange(pendingEntryQueue);
                pendingEntryQueue.Clear();
            }
        }

        /// <summary>CZ_BATTLEFIELD_CHAT 0x02db {u16 len, "Name : text\0"} (the /battlechat mode's lines).</summary>
        public Task BeginBattlefieldChat(string line) =>
            socialEconomyWorkflow.BeginPetPacket(_ => BuildRaw(0x02db, TextBytes(line ?? string.Empty), true), "sending battlefield chat", "sent battlefield chat");

        /// <summary>An entry-queue request: 0x08da / 0x090a {bgName[24]}, 0x08e0 {u8 result, bgName[24], lobbyName[24]}.</summary>
        public Task BeginEntryQueuePacket(ushort packetId, byte[] body) =>
            socialEconomyWorkflow.BeginPetPacket(_ => BuildRaw(packetId, body, false), "sending a battleground queue request", "sent battleground queue request");

        /// <summary>A fixed-length classic request built from its body (the menu's guild, adoption, chat-block and report packets).</summary>
        public Task BeginClassicPacket(ushort packetId, byte[] body, string what) =>
            socialEconomyWorkflow.BeginPetPacket(_ => BuildRaw(packetId, body, false), "sending " + what, "sent " + what);

        /// <summary>Text for a packet in the client's code page (Windows-1252 for Custom's langtype 1).</summary>
        public static byte[] TextBytes(string text) {
            Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
            return System.Text.Encoding.GetEncoding(1252).GetBytes(text ?? string.Empty);
        }

        private static byte[] BuildRaw(ushort packetId, byte[] body, bool variable) {
            body = body ?? Array.Empty<byte>();
            int header = variable ? 4 : 2;
            var packet = new byte[header + body.Length + (variable ? 1 : 0)];
            packet[0] = (byte) packetId;
            packet[1] = (byte) (packetId >> 8);
            if (variable) {
                packet[2] = (byte) packet.Length;
                packet[3] = (byte) (packet.Length >> 8);
            }

            Buffer.BlockCopy(body, 0, packet, header, body.Length);
            return packet;
        }

        /// <summary>The last ZC_NAVIGATION_ACTIVE and its count (the classic navigation follows the count).</summary>
        public (RoZcNavigationActive Packet, int Sequence) NavigationActiveSnapshot() {
            lock (sync) {
                return (latestNavigationActive, navigationActiveSequence);
            }
        }

        public RoZcEquipWinMicroscope PlayerViewEquipmentSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("player-view-equipment");
            lock (sync) {
                return latestPlayerViewEquipment;
            }
        }

        public RoGameplayHotkeySnapshot HotkeySnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("hotkey");
            lock (sync) {
                return gameplayState.HotkeySnapshot();
            }
        }

        public RoGameplayQuestSnapshot QuestSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("quest");
            lock (sync) {
                return gameplayState.QuestSnapshot();
            }
        }

        public RoGameplayFunctionalSnapshot FunctionalSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("functional");
            using (FunctionalSnapshotMarker.Auto()) {
                lock (sync) {
                    return gameplayState.FunctionalSnapshot();
                }
            }
        }

        public RoNpcDialogSnapshot NpcDialogSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("npc-dialog");
            lock (sync) {
                return gameplayState.NpcDialogSnapshot();
            }
        }

        public RoNpcShopSnapshot NpcShopSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("npc-shop");
            lock (sync) {
                return gameplayState.NpcShopSnapshot();
            }
        }

        public RoStorageSnapshot StorageSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("storage");
            lock (sync) {
                return gameplayState.StorageSnapshot();
            }
        }

        public RoCartInventorySnapshot CartSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("cart");
            lock (sync) {
                return gameplayState.CartSnapshot();
            }
        }

        public RoVendingSnapshot VendingSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("vending");
            lock (sync) {
                return vendingState.Snapshot();
            }
        }

        public RoBankSnapshot BankSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("bank");
            lock (sync) {
                return gameplayState.BankSnapshot();
            }
        }

        public RoMapMoveSnapshot MapMoveSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("map-move");
            lock (sync) {
                return gameplayState.MapMoveSnapshot();
            }
        }

        public RoRodexSnapshot RodexSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("rodex");
            lock (sync) {
                return rodexState.Snapshot();
            }
        }

        /// <summary><see cref="RodexSnapshot"/>'s Sequence without building the snapshot (per-frame change checks).</summary>
        public int RodexSequence {
            get {
                lock (sync) {
                    return rodexState.CurrentSequence;
                }
            }
        }

        public RoMailSnapshot MailSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("mail");
            lock (sync) {
                return mailState.Snapshot();
            }
        }

        public RoAttendanceSnapshot AttendanceSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("attendance");
            lock (sync) {
                return attendanceState.Snapshot();
            }
        }

        public RoStatusIconSnapshot[] StatusIconSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("status-icon");
            lock (sync) {
                return gameplayState.StatusIconSnapshot();
            }
        }

        public void StatusIconSnapshot(List<RoStatusIconSnapshot> snapshots) {
            if (snapshots == null) {
                throw new ArgumentNullException(nameof(snapshots));
            }

            RoRuntimePerfCounters.IncrementSnapshot("status-icon");
            lock (sync) {
                gameplayState.StatusIconSnapshot(snapshots);
            }
        }

        public RoShellFunctionalWindowSnapshot FunctionalWindowSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("functional-window");
            using (FunctionalSnapshotMarker.Auto()) {
                lock (sync) {
                    return BuildFunctionalWindowSnapshotLocked(gameplayState.FunctionalSnapshot());
                }
            }
        }

        public RoShellFunctionalWindowSnapshot FunctionalWindowEventSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("functional-window-events");
            lock (sync) {
                return BuildFunctionalWindowSnapshotLocked(RoGameplayFunctionalSnapshot.Empty);
            }
        }

        private RoShellFunctionalWindowSnapshot BuildFunctionalWindowSnapshotLocked(RoGameplayFunctionalSnapshot functional) {
            DateTime nowUtc = DateTime.UtcNow;
            int actionLockRemaining = actionLockState.GetRemainingMilliseconds(nowUtc);
            var commandState = new RoShellCommandStateSnapshot(
                uiState.Stage,
                IsBusyLocked(),
                uiState.IsMapTransitionPending,
                actionLockRemaining,
                actionLockRemaining > 0 ? actionLockState.Reason : string.Empty);
            return new RoShellFunctionalWindowSnapshot(
                commandState,
                functional,
                liveDrainHistory.LatestDrain,
                liveDrainHistory.SnapshotRecent());
        }

        public RoPartyRuntimeSnapshot PartySnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("party");
            lock (sync) {
                return partyState.Snapshot();
            }
        }

        /// <summary>FUN_00d1e3b0(id, 1): an online member of the own party (no snapshot: the battle log asks per hit).</summary>
        public bool IsOnlinePartyMember(uint accountId) {
            lock (sync) {
                return partyState.IsOnlineMember(accountId);
            }
        }

        /// <summary>The item a ZC_ITEM_THROW_ACK of this bag index dropped, as it was before the ack.</summary>
        public bool TryGetThrownItem(int bagIndex, out RoItemInstance item) {
            lock (sync) {
                return gameplayState.TryGetThrownItem(bagIndex, out item);
            }
        }

        /// <summary>CZ_REQNAME_BYGID for a character id (the name cache's request; the answer is a CharacterNameByGid event).</summary>
        public Task BeginRequestCharacterName(uint characterId) => socialEconomyWorkflow.BeginRequestNameByGid(characterId);

        /// <summary>FUN_00cec750: the own party list's member with this account id.</summary>
        public bool TryGetPartyMember(uint accountId, out RoPartyMemberRuntimeSnapshot member) {
            lock (sync) {
                return partyState.TryGetMember(accountId, out member);
            }
        }

        public RoChatRoomSnapshot ChatRoomSnapshot() {
            lock (sync) {
                return chatRoomState.Snapshot();
            }
        }

        /// <summary>CZ_CREATE_CHATROOM (change = CZ_CHANGE_CHATROOM); the form is kept for the room the answer opens.</summary>
        public Task BeginLegacyChatRoomForm(bool change, int limit, bool isPublic, string password, string title) {
            lock (sync) {
                if (!change) chatRoomState.RememberCreate(limit, isPublic, title);
            }

            return socialEconomyWorkflow.BeginChatRoomForm(change, limit, isPublic, password, title);
        }

        public Task BeginLegacyEnterChatRoom(uint chatId, string password) => socialEconomyWorkflow.BeginEnterChatRoom(chatId, password);
        public Task BeginLegacyChatRoomOwner(string name) => socialEconomyWorkflow.BeginChatRoomOwner(name);
        public Task BeginLegacyChatRoomExpel(string name) => socialEconomyWorkflow.BeginChatRoomExpel(name);
        public Task BeginLegacyExitChatRoom() => socialEconomyWorkflow.BeginExitChatRoom();

        /// <summary>CZ_REQ_EMOTION (the server answers with ZC_EMOTION to the area, the player included).</summary>
        public Task BeginLegacyEmotion(byte emotion) => socialEconomyWorkflow.BeginEmotion(emotion);

        /// <summary>An emotion of a pack through the client's packet for this profile (CZ_REQ_EMOTION_EXPANSION 0x0be9 on 20240619).</summary>
        public Task BeginEmotion(ushort pack, ushort emotion) => socialEconomyWorkflow.BeginEmotion(pack, emotion);

        private RoClassicGuildState guild = RoClassicGuildState.None;
        private int guildSerial;
        private bool guildNamesPending;

        /// <summary>The guild caches (ZC_UPDATE_GDID, the tab mask, ZC_GUILD_INFO, members, positions) with a change counter.</summary>
        public (RoClassicGuildState Guild, int Serial) GuildSnapshot() {
            lock (sync) {
                return (guild, guildSerial);
            }
        }

        /// <summary>The pick rule's own guild (FUN_00d1e2a0).</summary>
        public uint PickOwnGuildId {
            get {
                lock (sync) {
                    return guild.GuildId;
                }
            }
        }

        /// <summary>FUN_00a120b0: an ally (relation 0) of the own guild.</summary>
        public bool IsAlliedGuild(uint guildId) {
            lock (sync) {
                foreach (RoGuildRelation r in guild.Relations) {
                    if (r.Relation == 0 && r.GuildId == guildId) return true;
                }

                return false;
            }
        }

        /// <summary>The own homunculus or mercenary (pick type 4's owner check).</summary>
        public bool IsOwnCompanion(uint actorId) {
            lock (sync) {
                return actorId != 0 && (homun.Gid == actorId || merc.Gid == actorId);
            }
        }

        /// <summary>The selected character's id (GID) and account id for the guild windows' self checks.</summary>
        public (uint CharId, uint AccountId) LocalCharacterIds() {
            lock (sync) {
                return ((uint) (mapSessionState.MapResult?.CharacterResult?.SelectedCharacter?.CharacterId ?? 0), mapSessionState.LocalActorId);
            }
        }

        /// <summary>CZ_REQ_GUILD_MENUINTERFACE (mode 0x69): the server answers with the tab mask.</summary>
        public Task BeginLegacyGuildMenuInterface() =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildMenuInterface(p), "asking for the guild tabs", "sent guild tab request");

        /// <summary>CZ_REQ_GUILD_MENU (mode 0x6b): 0 info, 1 members, 2 positions, 3 skills, 4 expelled.</summary>
        public Task BeginLegacyGuildMenu(int type) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildMenu(type, p), "asking for guild information", "sent guild information request");

        /// <summary>CZ_REQ_CHANGE_MEMBERPOS (mode 0x67); the server broadcasts ZC_ACK_REQ_CHANGE_MEMBERS per change.</summary>
        public Task BeginLegacyGuildMemberPositions(IReadOnlyList<(uint AccountId, uint CharId, int PositionId)> members) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildMemberPositions(members, p), "changing member positions", "sent member positions");

        /// <summary>CZ_REG_CHANGE_GUILD_POSITIONINFO (mode 0x68); the server broadcasts ZC_ACK_CHANGE_GUILD_POSITIONINFO per position.</summary>
        public Task BeginLegacyGuildPositions(IReadOnlyList<RoGuildPosition> positions) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildPositions(positions, p), "changing guild positions", "sent guild positions");

        /// <summary>CZ_REQ_JOIN_GUILD: invites a player (by account id); the answer comes as ZC_ACK_REQ_JOIN_GUILD.</summary>
        public Task BeginLegacyGuildInvite(uint targetAccountId) {
            uint accountId, charId;
            lock (sync) {
                accountId = mapSessionState.LocalActorId;
                charId = (uint) (mapSessionState.MapResult?.CharacterResult?.SelectedCharacter?.CharacterId ?? 0);
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildInvite(targetAccountId, accountId, charId, p), "inviting to the guild", "sent guild invitation");
        }

        /// <summary>CZ_GUILD_NOTICE (mode 0x76); the server broadcasts ZC_GUILD_NOTICE.</summary>
        public Task BeginLegacyGuildNotice(string subject, string notice) {
            uint guildId;
            lock (sync) {
                guildId = guild.GuildId;
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildNotice(guildId, subject, notice, p), "changing the guild notice", "sent guild notice");
        }

        /// <summary>CZ_REQ_MAKE_GUILD (mode 0x70) with the selected character's id.</summary>
        public Task BeginLegacyGuildCreate(string name) {
            uint charId;
            lock (sync) {
                charId = (uint) (mapSessionState.MapResult?.CharacterResult?.SelectedCharacter?.CharacterId ?? 0);
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildCreate(charId, name, p), "creating a guild", "sent guild creation");
        }

        /// <summary>CZ_REQ_DISORGANIZE_GUILD (mode 0x6a): the guild name as the key.</summary>
        public Task BeginLegacyGuildDisband(string key) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildDisband(key, p), "disbanding the guild", "sent guild disband");

        /// <summary>CZ_JOIN_GUILD (mode 0x72): the answer to ZC_REQ_JOIN_GUILD.</summary>
        public Task BeginLegacyGuildInviteAnswer(uint guildId, bool accept) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildInviteAnswer(guildId, accept, p), "answering a guild invitation", "sent guild invitation answer");

        /// <summary>CZ_ALLY_GUILD (mode 0x78): the answer to ZC_REQ_ALLY_GUILD.</summary>
        public Task BeginLegacyGuildAllyAnswer(uint inviterAccountId, bool accept) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildAllyAnswer(inviterAccountId, accept, p), "answering an alliance offer", "sent alliance answer");

        /// <summary>CZ_REQ_LEAVE_GUILD (mode 0x6e): the guild and the local account / character, the reason (0x4b).</summary>
        public Task BeginLegacyGuildLeave(string reason) {
            uint guildId, accountId, charId;
            lock (sync) {
                guildId = guild.GuildId;
                accountId = mapSessionState.LocalActorId;
                charId = (uint) (mapSessionState.MapResult?.CharacterResult?.SelectedCharacter?.CharacterId ?? 0);
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildLeave(guildId, accountId, charId, reason, p), "leaving the guild", "sent guild leave");
        }

        /// <summary>CZ_REQ_BAN_GUILD (mode 0x6f): the member picked in 0x3d and the reason (0x4c).</summary>
        public Task BeginLegacyGuildExpel(uint accountId, uint charId, string reason) {
            uint guildId;
            lock (sync) {
                guildId = guild.GuildId;
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildGuildExpel(guildId, accountId, charId, reason, p), "expelling a member", "sent guild expulsion");
        }

        private void ApplyGuildEventLocked(RoMapEntryDrainEvent drainEvent) {
            RoClassicGuildState before = guild;
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.GuildBelongInfo:
                    guild = guild.WithBelong(drainEvent.GuildBelongInfo);
                    break;
                case RoMapEntryDrainEventKind.GuildNotice:
                    guild = guild.WithNotice(drainEvent.GuildNotice);
                    break;
                case RoMapEntryDrainEventKind.GuildLeaveResult:
                    if (string.Equals(drainEvent.GuildLeaveResult.CharacterName, characterState.SelectedCharacterName, StringComparison.Ordinal)) guild = RoClassicGuildState.None;
                    break;
                case RoMapEntryDrainEventKind.GuildInvite:
                    guild = guild.WithEvent(RoGuildUpdateKind.Invite, drainEvent.GuildInvite.GuildId, drainEvent.GuildInvite.GuildName, 0);
                    break;
                case RoMapEntryDrainEventKind.GuildInviteResult:
                    guild = guild.WithEvent(RoGuildUpdateKind.InviteResult, 0, string.Empty, drainEvent.GuildInviteResult.Result);
                    break;
                case RoMapEntryDrainEventKind.GuildManager:
                    guild = guild.Apply(drainEvent.GuildUpdate, (uint) (mapSessionState.MapResult?.CharacterResult?.SelectedCharacter?.CharacterId ?? 0));
                    if (drainEvent.GuildUpdate.Kind == RoGuildUpdateKind.MemberList || drainEvent.GuildUpdate.Kind == RoGuildUpdateKind.BanList) guildNamesPending = true;
                    break;
                case RoMapEntryDrainEventKind.CharacterNameByGid:
                    guild = guild.WithName(drainEvent.CharacterNameByGid.CharacterId, drainEvent.CharacterNameByGid.Name);
                    break;
            }

            if (!ReferenceEquals(before, guild)) guildSerial++;
        }

        private async Task RequestNamelessGuildNamesAsync() {
            var characterIds = new List<uint>();
            lock (sync) {
                guild.CollectNameless(characterIds);
            }

            foreach (uint characterId in characterIds) {
                try {
                    await socialEconomyWorkflow.BeginRequestNameByGid(characterId).ConfigureAwait(false);
                } catch {
                    return;
                }
            }
        }

        private RoZcSkillInfo[] homunSkills = Array.Empty<RoZcSkillInfo>();
        private RoZcSkillInfo[] mercSkills = Array.Empty<RoZcSkillInfo>();
        private int companionSkillSerial;

        /// <summary>The homunculus (mode 0) or mercenary (mode 1) skills (ZC_HOSKILLINFO_LIST / ZC_MER_SKILLINFO_LIST) and a counter.</summary>
        public (RoZcSkillInfo[] Skills, int Serial) CompanionSkillsSnapshot(int mode) {
            lock (sync) {
                return (mode == 1 ? mercSkills : homunSkills, companionSkillSerial);
            }
        }

        /// <summary>CZ_UPGRADE_SKILLLEVEL for a companion skill (pc_skillup hands homunculus ids to homun->skillup).</summary>
        public Task BeginLegacyCompanionSkillUp(int skillId) =>
            socialEconomyWorkflow.BeginPetPacket(_ => RoShellGameplayCommands.BuildPlannedUpgradeSkill(skillId), "raising a companion skill", "sent companion skill up");

        private RoClassicHomunState homun = RoClassicHomunState.None;
        private int homunSerial;
        private RoClassicMercState merc = RoClassicMercState.None;
        private int mercSerial;

        /// <summary>The homunculus as ZC_PROPERTY_HOMUN / ZC_CHANGESTATE_MER left it, with a change counter.</summary>
        public (RoClassicHomunState Homun, int Serial) HomunSnapshot() {
            lock (sync) {
                return (homun, homunSerial);
            }
        }

        /// <summary>The mercenary as ZC_MER_INIT / ZC_MER_PAR_CHANGE left it, with a change counter.</summary>
        public (RoClassicMercState Merc, int Serial) MercSnapshot() {
            lock (sync) {
                return (merc, mercSerial);
            }
        }

        /// <summary>CZ_COMMAND_MER: 1 feed, 2 delete (the client drops its homunculus at once on 2).</summary>
        public Task BeginLegacyHomunculusCommand(byte command) {
            if (command == 2) {
                lock (sync) {
                    homun = RoClassicHomunState.None;
                    homunSerial++;
                }
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildHomunculusCommand(command, p), "commanding the homunculus", "sent homunculus command");
        }

        /// <summary>CZ_RENAME_MER; the client shows the name at once (vf37 0x81cd10 copies it to DAT_01552eb0).</summary>
        public Task BeginLegacyHomunculusRename(string name) {
            lock (sync) {
                if (homun.Active) {
                    homun = homun.WithName(name);
                    homunSerial++;
                }
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildHomunculusRename(name, p), "renaming the homunculus", "sent homunculus rename");
        }

        /// <summary>CZ_MER_COMMAND: 2 fires the mercenary (the client resets its data at once).</summary>
        public Task BeginLegacyMercenaryCommand(byte command) {
            if (command == 2) {
                lock (sync) {
                    merc = RoClassicMercState.None;
                    mercSerial++;
                }
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildMercenaryCommand(command, p), "commanding the mercenary", "sent mercenary command");
        }

        /// <summary>CZ_CONFIG (FUN_00d36750 / 00d36e30): 2 pet, 3 homunculus auto feeding.</summary>
        public Task BeginLegacyCompanionConfig(int config, int value) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildCompanionConfig(config, value, p), "changing auto feeding", "sent auto feeding");

        private RoClassicPetState pet = RoClassicPetState.None;
        private int petSerial;
        private RoZcPetEggList petEggList;
        private int petEggListSerial;
        private int[] petEggIndexes = Array.Empty<int>();
        private int petEggIndexesSerial;
        private int petEggCloseSerial;
        private int petCaptureStartSerial;
        private int petCaptureResult = -1;
        private int petCaptureResultSerial;
        private uint petEvolutionResult;
        private int petEvolutionSerial;
        private RoZcPetFeedResult petFeed;
        private int petFeedSerial;

        /// <summary>The own pet as ZC_PROPERTY_PET / ZC_CHANGESTATE_PET left it, with a change counter.</summary>
        public (RoClassicPetState Pet, int Serial) PetSnapshot() {
            lock (sync) {
                return (pet, petSerial);
            }
        }

        /// <summary>The incubator list (ZC_PETEGG_LIST indexes), its counter, and the counter of the server closing it (state 6, data 1).</summary>
        public (int[] Eggs, int ListSerial, int CloseSerial) PetEggSnapshot() {
            lock (sync) {
                if (petEggIndexesSerial != petEggListSerial) {
                    petEggIndexesSerial = petEggListSerial;
                    short[] bag = petEggList.BagIndexes;
                    petEggIndexes = bag == null || bag.Length == 0 ? Array.Empty<int>() : Array.ConvertAll(bag, i => (int) i);
                }

                return (petEggIndexes, petEggListSerial, petEggCloseSerial);
            }
        }

        /// <summary>The taming packets: ZC_START_CAPTURE's counter, ZC_TRYCAPTURE_MONSTER's result and counter.</summary>
        public (int StartSerial, int Result, int ResultSerial) PetCaptureSnapshot() {
            lock (sync) {
                return (petCaptureStartSerial, petCaptureResult, petCaptureResultSerial);
            }
        }

        /// <summary>ZC_FEED_PET's last result (fed or not, the food) and its counter.</summary>
        public (bool Fed, uint Food, int Serial) PetFeedSnapshot() {
            lock (sync) {
                return (petFeed.IsSuccess, petFeed.ItemId, petFeedSerial);
            }
        }

        /// <summary>ZC_PET_EVOLUTION_RESULT's last result and its counter.</summary>
        public (uint Result, int Serial) PetEvolutionSnapshot() {
            lock (sync) {
                return (petEvolutionResult, petEvolutionSerial);
            }
        }

        /// <summary>CZ_PET_EVOLUTION (0x105's OK): the target egg and the recipe's (inventory index, amount) pairs.</summary>
        public Task BeginLegacyPetEvolution(uint targetEggId, IReadOnlyList<(int Index, int Amount)> materials) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildPetEvolution(targetEggId, materials, p), "evolving the pet", "sent pet evolution");

        /// <summary>CZ_COMMAND_PET: 1 feed, 2 performance, 3 return to egg, 4 unequip the accessory.</summary>
        public Task BeginLegacyPetCommand(byte subCommand) {
            if (subCommand == 3) {
                lock (sync) {
                    // the pet goes back into its egg: the information window has nothing to show
                    pet = RoClassicPetState.None;
                    petSerial++;
                }
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildPetCommand(subCommand, p), "commanding the pet", "sent pet command");
        }

        /// <summary>
        /// CZ_RENAME_PET. Like the client (vf37 0x81ea20) the new name shows at once: an accepted rename only sends the
        /// actor name (pet_change_name_ack), a refused one sends ZC_PROPERTY_PET back with the old name.
        /// </summary>
        public Task BeginLegacyRenamePet(string name) {
            lock (sync) {
                if (pet.Active) {
                    pet = pet.WithName(name);
                    petSerial++;
                }
            }

            return socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildRenamePet(name, p), "renaming the pet", "sent pet rename");
        }

        public Task BeginLegacySelectPetEgg(int bagIndex) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildSelectPetEgg(unchecked((short) bagIndex), p), "hatching a pet", "sent pet egg choice");

        public Task BeginLegacyTryCapture(uint actorId) =>
            socialEconomyWorkflow.BeginPetPacket(p => RoShellGameplayCommands.BuildTryCaptureMonster(actorId, p), "taming a monster", "sent taming target");

        private void ApplyPetChange(RoZcPetChangeState change) {
            switch (change.Type) {
                case 0:
                    pet = pet.With(gid: change.ActorId);
                    break;
                case 1:
                    if (change.ActorId == pet.Gid) pet = pet.With(intimacy: change.Data);
                    break;
                case 2:
                    if (change.ActorId == pet.Gid) pet = pet.With(hunger: change.Data);
                    break;
                case 3:
                    if (change.ActorId == pet.Gid) pet = pet.With(accessory: change.Data);
                    break;
                case 6:
                    // 0x00c480a8: data 1 closes the incubator list
                    if (change.Data == 1) petEggCloseSerial++;
                    return;
                default:
                    return;
            }

            petSerial++;
        }

        private RoZcMakableItemList makableList;
        private int makableListSerial;
        private RoZcAckReqMakingItem makingResult;
        private int makingResultSerial;

        /// <summary>The last ZC_MAKABLEITEMLIST and ZC_ACK_REQMAKINGITEM, each with a counter.</summary>
        public (RoZcMakableItemList List, int ListSerial, RoZcAckReqMakingItem Result, int ResultSerial) MakingSnapshot() {
            lock (sync) {
                return (makableList, makableListSerial, makingResult, makingResultSerial);
            }
        }

        /// <summary>CZ_REQMAKINGITEM with the item and up to three added materials, or item 0 to close the list.</summary>
        public Task BeginLegacyMakingItem(uint itemId, uint[] materials) => socialEconomyWorkflow.BeginMakingItem(itemId, materials);

        private RoZcMergeItemOpen mergeOpen;
        private int mergeOpenSerial;
        private RoZcAckMergeItem mergeResult;
        private int mergeResultSerial;

        /// <summary>The last ZC_MERGE_ITEM_OPEN and ZC_ACK_MERGE_ITEM, each with a counter.</summary>
        public (RoZcMergeItemOpen Open, int OpenSerial, RoZcAckMergeItem Result, int ResultSerial) MergeSnapshot() {
            lock (sync) {
                return (mergeOpen, mergeOpenSerial, mergeResult, mergeResultSerial);
            }
        }

        /// <summary>CZ_REQ_MERGE_ITEM with the chosen inventory indexes, or CZ_CANCEL_MERGE_ITEM for null.</summary>
        public Task BeginLegacyMergeItem(short[] bagIndexes) => socialEconomyWorkflow.BeginMergeItem(bagIndexes);

        private RoZcRepairItemList weaponRefineList;
        private int weaponRefineListSerial;
        private RoZcAckWeaponRefine weaponRefineResult;
        private int weaponRefineResultSerial;
        private int[] autoSpellList = Array.Empty<int>();
        private int autoSpellListSerial;

        /// <summary>The last ZC_NOTIFY_WEAPONITEMLIST and ZC_ACK_WEAPONREFINE, each with a counter.</summary>
        public (RoZcRepairItemList List, int ListSerial, RoZcAckWeaponRefine Result, int ResultSerial) WeaponRefineSnapshot() {
            lock (sync) {
                return (weaponRefineList, weaponRefineListSerial, weaponRefineResult, weaponRefineResultSerial);
            }
        }

        /// <summary>The last ZC_AUTOSPELLLIST's skill ids with a counter.</summary>
        public (int[] SkillIds, int Serial) AutoSpellSnapshot() {
            lock (sync) {
                return (autoSpellList, autoSpellListSerial);
            }
        }

        /// <summary>CZ_REQ_WEAPONREFINE with the listed index, or −1 to close the list.</summary>
        public Task BeginLegacyWeaponRefine(int index) => socialEconomyWorkflow.BeginWeaponRefine(index);

        /// <summary>CZ_SELECTAUTOSPELL with the chosen skill, or 0 to close the list.</summary>
        public Task BeginLegacySelectAutoSpell(int skillId) => socialEconomyWorkflow.BeginSelectAutoSpell(skillId);

        private RoZcRepairItemList repairList;
        private int repairListSerial;
        private int repairResult = -1;
        private int repairResultSerial;

        /// <summary>The last ZC_REPAIRITEMLIST and ZC_ACK_ITEMREPAIR, each with a counter (a new one opens / prints once).</summary>
        public (RoZcRepairItemList List, int ListSerial, int Result, int ResultSerial) RepairSnapshot() {
            lock (sync) {
                return (repairList, repairListSerial, repairResult, repairResultSerial);
            }
        }

        /// <summary>CZ_REQ_ITEMREPAIR2 with the chosen listed record, or index −1 to close the list.</summary>
        public Task BeginLegacyItemRepair(RoRepairItem? item) =>
            item.HasValue
                ? socialEconomyWorkflow.BeginItemRepair(item.Value.Index, item.Value.ItemId, item.Value.Cards, item.Value.Refine)
                : socialEconomyWorkflow.BeginItemRepair(-1, 0, null, 0);

        public RoBuyingStoreSnapshot BuyingStoreSnapshot() {
            lock (sync) {
                return buyingStoreState.Snapshot();
            }
        }

        /// <summary>CZ_REQ_OPEN_BUYING_STORE (open = result 1; cancel = result 0, which ends the setup).</summary>
        public Task BeginLegacyOpenBuyingStore(uint zenyLimit, bool open, string title, RoBuyingStoreItem[] items) {
            lock (sync) {
                if (open) buyingStoreState.RememberOpen(title);
                else buyingStoreState.CancelSetup();
                MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.BuyingStore);
            }

            return socialEconomyWorkflow.BeginOpenBuyingStore(zenyLimit, open, title, items);
        }

        /// <summary>CZ_REQ_CLOSE_BUYING_STORE: the own store closes here at once (the server tells only the others).</summary>
        public Task BeginLegacyCloseBuyingStore() {
            lock (sync) {
                buyingStoreState.CloseOwnLocally();
                MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.BuyingStore);
            }

            return socialEconomyWorkflow.BeginCloseBuyingStore();
        }

        public Task BeginLegacyClickBuyingStore(uint buyerAid) => socialEconomyWorkflow.BeginClickBuyingStore(buyerAid);

        public Task BeginLegacyTradeBuyingStore(uint buyerAid, uint storeId, (ushort BagIndex, uint ItemId, int Amount)[] items) =>
            socialEconomyWorkflow.BeginTradeBuyingStore(buyerAid, storeId, items);

        public RoFriendRuntimeSnapshot[] FriendsSnapshot() {
            RoRuntimePerfCounters.IncrementSnapshot("friends");
            lock (sync) {
                return friendsState.SnapshotFriends();
            }
        }

        public void FriendsSnapshot(List<RoFriendRuntimeSnapshot> friends) {
            if (friends == null) {
                throw new ArgumentNullException(nameof(friends));
            }

            RoRuntimePerfCounters.IncrementSnapshot("friends");
            lock (sync) {
                friendsState.SnapshotFriends(friends);
            }
        }

        public uint LocalActorId {
            get {
                lock (sync) {
                    return mapSessionState.LocalActorId;
                }
            }
        }

        public string SelectedCharacterName {
            get {
                lock (sync) {
                    return characterState.SelectedCharacterName;
                }
            }
        }

        public int GameplayChangeSequence {
            get {
                lock (sync) {
                    return gameplayChangeSequence;
                }
            }
        }

        public int BasicInfoChangeSequence {
            get {
                lock (sync) {
                    return basicInfoChangeSequence;
                }
            }
        }

        public int StatusWindowChangeSequence {
            get {
                lock (sync) {
                    return statusWindowChangeSequence;
                }
            }
        }

        public int SkillWindowChangeSequence {
            get {
                lock (sync) {
                    return skillWindowChangeSequence;
                }
            }
        }

        public int InventoryWindowChangeSequence {
            get {
                lock (sync) {
                    return inventoryWindowChangeSequence;
                }
            }
        }

        // Bumps only when the whole normal inventory is (re)loaded as a list (login / character change /
        // map re-entry), letting the item-obtain overlay re-baseline instead of treating the bulk list
        // as freshly obtained items.
        public int InventoryReloadSequence {
            get {
                lock (sync) {
                    return gameplayState?.InventoryReloadVersion ?? 0;
                }
            }
        }

        public int EquipmentWindowChangeSequence {
            get {
                lock (sync) {
                    return equipmentWindowChangeSequence;
                }
            }
        }

        public int PlayerViewEquipmentSequence {
            get {
                lock (sync) {
                    return playerViewEquipmentSequence;
                }
            }
        }

        public int HotkeyWindowChangeSequence {
            get {
                lock (sync) {
                    return hotkeyWindowChangeSequence;
                }
            }
        }

        public int QuestWindowChangeSequence {
            get {
                lock (sync) {
                    return questWindowChangeSequence;
                }
            }
        }

        public int FunctionalWindowChangeSequence {
            get {
                lock (sync) {
                    return functionalWindowChangeSequence;
                }
            }
        }

        public int StatusIconChangeSequence {
            get {
                lock (sync) {
                    return statusIconChangeSequence;
                }
            }
        }

        internal int GetFunctionalWindowChangeSequence(RoFunctionalWindowChange windowChange) {
            lock (sync) {
                return GetFunctionalWindowChangeSequenceLocked(windowChange);
            }
        }

        internal RoFunctionalWindowChange LastFunctionalWindowChanges {
            get {
                lock (sync) {
                    return lastFunctionalWindowChanges;
                }
            }
        }

        /// <summary>
        /// The login alone (the classic pre-game): AC_ACCEPT_LOGIN's character servers stay pending in the ServerSelect stage
        /// until <see cref="BeginSelectServer"/> connects to one (or <see cref="CancelServerSelect"/> drops them).
        /// </summary>
        public Task BeginLogin(string account, string password) {
            var request = new RoShellLoginRequest(account, password);
            RoServerProfile currentProfile;
            int? basicInfoSequence = null;

            lock (sync) {
                ThrowIfDisposed();
                ThrowIfBusyLocked();
                currentProfile = profileState.Profile;
                traceBuffer.Clear();
                uiState.SetStage(RoLoginShellStage.LoadingCharacters, Text("ui.network.connecting"));
                characterState.Clear();
                ClearCharacterRuntimeLocked();
                ClearLastMapEntryRequestLocked();
                ClearMapResultLocked();
                gameplayState.Clear();
                vendingState.ClearMapScopedState();
                chatRoomState.ClearMapScopedState();
                buyingStoreState.ClearMapScopedState();
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
            return StartTask(() => {
                RoShellPendingLogin pending = connectionWorkflow.Login(currentProfile, request);
                lock (sync) {
                    if (disposed) {
                        pending.Dispose();
                        return;
                    }

                    pendingLogin?.Dispose();
                    pendingLogin = pending;
                    uiState.SetStage(RoLoginShellStage.ServerSelect, Text("ui.network.connecting"));
                    MarkPhase11UiChangedLocked();
                }
            });
        }

        /// <summary>The name of the character server chosen in the server list (empty when none was chosen there).</summary>
        public string SelectedServerName { get; private set; } = string.Empty;

        /// <summary>The login's web auth token (0x154e400) and the login server host the web API address falls back to.</summary>
        public string WebAuthToken { get; private set; } = string.Empty;

        public string LoginHost { get; private set; } = string.Empty;

        /// <summary>The pending login's character servers (empty outside the ServerSelect stage).</summary>
        public RoLoginCharServerInfo[] ServerListSnapshot() {
            lock (sync) {
                return pendingLogin != null ? (RoLoginCharServerInfo[]) pendingLogin.Servers.Clone() : Array.Empty<RoLoginCharServerInfo>();
            }
        }

        /// <summary>Connects to the pending login's character server <paramref name="index"/> and loads its characters.</summary>
        public Task BeginSelectServer(int index) {
            RoShellPendingLogin pending;
            lock (sync) {
                ThrowIfDisposed();
                ThrowIfBusyLocked();
                pending = pendingLogin;
                pendingLogin = null;
                if (pending == null) {
                    throw new InvalidOperationException("There is no character server list to choose from.");
                }

                SelectedServerName = index >= 0 && index < pending.Servers.Length ? pending.Servers[index].Name ?? string.Empty : string.Empty;
                WebAuthToken = pending.WebAuthToken;
                LoginHost = pending.Profile.Login.Host ?? string.Empty;

                uiState.SetStage(RoLoginShellStage.LoadingCharacters, Text("ui.network.connecting"));
                MarkPhase11UiChangedLocked();
            }

            return StartTask(() => {
                RoShellCharacterRuntimeSession runtimeSession;
                try {
                    runtimeSession = connectionWorkflow.OpenCharacterRuntime(pending, index);
                } finally {
                    pending.Dispose();   // nothing left once the runtime moved to the character session
                }

                bool stored = false;
                try {
                    RoLoginSessionResult result = runtimeSession.ToLoginSessionResult();
                    lock (sync) {
                        if (disposed) {
                            return;
                        }

                        ReplaceCharacterRuntimeLocked(runtimeSession);
                        stored = true;
                        characterState.ApplyLoadedCharacters(result.Characters);
                        uiState.SetStage(RoLoginShellStage.CharacterSelect, Text("ui.network.charactersLoaded"));
                        MarkPhase11UiChangedLocked();
                    }
                } finally {
                    if (!stored) {
                        runtimeSession.Dispose();
                    }
                }
            });
        }

        /// <summary>Cancel on the server list: the pending login is dropped and the login screen comes back.</summary>
        public void CancelServerSelect() {
            lock (sync) {
                if (uiState.Stage != RoLoginShellStage.ServerSelect) {
                    return;
                }

                pendingLogin?.Dispose();
                pendingLogin = null;
                uiState.SetStage(RoLoginShellStage.Login, Text("ui.login.status"));
                MarkPhase11UiChangedLocked();
            }
        }

        public Task BeginLoadCharacters(string account, string password) {
            var request = new RoShellLoginRequest(account, password);
            RoServerProfile currentProfile;
            int? basicInfoSequence = null;

            lock (sync) {
                ThrowIfDisposed();
                ThrowIfBusyLocked();
                currentProfile = profileState.Profile;
                traceBuffer.Clear();
                uiState.SetStage(RoLoginShellStage.LoadingCharacters, Text("ui.network.connecting"));
                characterState.Clear();
                ClearCharacterRuntimeLocked();
                ClearLastMapEntryRequestLocked();
                ClearMapResultLocked();
                gameplayState.Clear();
                vendingState.ClearMapScopedState();
                chatRoomState.ClearMapScopedState();
                buyingStoreState.ClearMapScopedState();
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
            return StartTask(() => {
                RoShellCharacterRuntimeSession runtimeSession =
                    connectionWorkflow.OpenCharacterRuntime(currentProfile, request);
                bool stored = false;

                try {
                    RoLoginSessionResult result = runtimeSession.ToLoginSessionResult();
                    lock (sync) {
                        if (disposed) {
                            return;
                        }

                        ReplaceCharacterRuntimeLocked(runtimeSession);
                        stored = true;
                        characterState.ApplyLoadedCharacters(result.Characters);
                        uiState.SetStage(RoLoginShellStage.CharacterSelect, Text("ui.network.charactersLoaded"));
                        MarkPhase11UiChangedLocked();
                    }
                } finally {
                    if (!stored) {
                        runtimeSession.Dispose();
                    }
                }
            });
        }

        public Task BeginEnterMap(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null) {
            var request = new RoShellCharacterRequest(account, password, characterName, characterSlot, hairColor, hairStyle, startJob, sex);
            return BeginEnterMap(request, reconnect: false);
        }

        public bool TryBeginAutoReconnect(long nowMs) {
            lock (sync) {
                ThrowIfDisposed();
                if (!hasLastMapEntryRequest) {
                    return false;
                }

                if (taskRunner.IsBusy) {
                    return false;
                }

                RoReconnectState before = uiState.ReconnectState;
                bool started = uiState.TryBeginAutoReconnect(nowMs);
                if (started || uiState.ReconnectState != before) {
                    MarkPhase11UiChangedLocked();
                }

                return started;
            }
        }

        public bool IsAutoReconnectDue(long nowMs) {
            lock (sync) {
                return !disposed &&
                    hasLastMapEntryRequest &&
                    uiState.IsAutoReconnectDue(nowMs);
            }
        }

        public void MarkReconnectStable(long nowMs) {
            lock (sync) {
                if (!disposed &&
                    uiState.Stage == RoLoginShellStage.MapEntered &&
                    mapSessionState.MapResult != null) {
                    uiState.MarkReconnectStable(nowMs);
                }
            }
        }

        public Task BeginReconnectMapEntry(long nowMs, bool reconnectAlreadyStarted = false) {
            RoShellCharacterRequest request;
            lock (sync) {
                ThrowIfDisposed();
                if (!hasLastMapEntryRequest) {
                    throw new InvalidOperationException("No previous map-entry request is available for reconnect.");
                }

                request = lastMapEntryRequest;
            }

            return BeginEnterMap(request, reconnect: true, reconnectAlreadyStarted, nowMs);
        }

        private Task BeginEnterMap(
            RoShellCharacterRequest request,
            bool reconnect,
            bool reconnectAlreadyStarted = false,
            long reconnectNowMs = 0) {
            RoServerProfile currentProfile;
            RoShellCharacterRuntimeSession cachedRuntimeSession = null;
            string fallbackReason = null;
            int? basicInfoSequence = null;

            lock (sync) {
                ThrowIfDisposed();
                ThrowIfBusyLocked();
                if (reconnect &&
                    !reconnectAlreadyStarted &&
                    !uiState.TryBeginManualReconnect(reconnectNowMs)) {
                    throw new InvalidOperationException("Reconnect is not available in the current shell state.");
                }

                currentProfile = profileState.Profile;
                if (characterRuntimeSession != null && characterRuntimeSession.Matches(currentProfile, request)) {
                    cachedRuntimeSession = characterRuntimeSession;
                } else if (characterRuntimeSession != null) {
                    fallbackReason = "cached character runtime session does not match the requested profile/account";
                    ClearCharacterRuntimeLocked();
                } else {
                    fallbackReason = "no cached character runtime session";
                }

                traceBuffer.Clear();
                uiState.SetStage(RoLoginShellStage.EnteringMap, Text("ui.network.enteringMap"));
                characterState.SetPendingCharacterName(request.CharacterName);
                ClearMapResultLocked();
                gameplayState.Clear();
                vendingState.ClearMapScopedState();
                chatRoomState.ClearMapScopedState();
                buyingStoreState.ClearMapScopedState();
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
            return StartTask(() => {
                bool usedCachedRuntime = false;
                RoLoginEnterMapResult result;
                if (cachedRuntimeSession != null) {
                    try {
                        result = connectionWorkflow.EnterMap(currentProfile, request, cachedRuntimeSession);
                        usedCachedRuntime = true;
                    } catch (Exception ex) {
                        Debug.Log(
                            $"RO login shell W2 map handoff fallback: handoff failed ({ex.GetType().Name}: {ex.Message}).");
                        lock (sync) {
                            if (ReferenceEquals(characterRuntimeSession, cachedRuntimeSession)) {
                                ClearCharacterRuntimeLocked();
                            }
                        }

                        result = connectionWorkflow.EnterMap(currentProfile, request);
                    }
                } else {
                    Debug.Log($"RO login shell W2 map handoff fallback: {fallbackReason}.");
                    result = connectionWorkflow.EnterMap(currentProfile, request);
                }

                int? enteredBasicInfoSequence = null;

                lock (sync) {
                    gameplayState.ApplyMapEntry(result);
                    result = ApplyCurrentMovementSpeedToMapResult(result);
                    ApplySocialMapEntryLocked(result);
                    enteredBasicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
                    mapSessionState.ReplaceMapResultAndStart(result);
                    if (usedCachedRuntime && ReferenceEquals(characterRuntimeSession, cachedRuntimeSession)) {
                        ClearCharacterRuntimeLocked();
                    }

                    RememberLastMapEntryRequestLocked(request);
                    uiState.SetStage(RoLoginShellStage.MapEntered, Text("ui.map.entered"));
                    MarkPhase11UiChangedLocked();
                }

                PublishBasicInfoSequenceChanged(enteredBasicInfoSequence);
            }, reconnect ? ex => uiState.SetReconnectFailed(Text("ui.error.connection"), ex.Message) : null);
        }

        public Task BeginCreateCharacter(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null) {
            var request = new RoShellCharacterRequest(account, password, characterName, characterSlot, hairColor, hairStyle, startJob, sex);
            RoServerProfile currentProfile;
            RoShellCharacterRuntimeSession existingRuntime;
            int? basicInfoSequence = null;

            lock (sync) {
                ThrowIfDisposed();
                ThrowIfBusyLocked();
                currentProfile = profileState.Profile;
                existingRuntime = characterRuntimeSession != null && characterRuntimeSession.Matches(currentProfile, request)
                    ? characterRuntimeSession
                    : null;
                if (characterRuntimeSession != null && existingRuntime == null) {
                    ClearCharacterRuntimeLocked();
                }

                traceBuffer.Clear();
                uiState.SetStage(RoLoginShellStage.LoadingCharacters, Text("ui.network.connecting"));
                characterState.SetPendingCharacterName(request.CharacterName);
                ClearLastMapEntryRequestLocked();
                ClearMapResultLocked();
                gameplayState.Clear();
                vendingState.ClearMapScopedState();
                chatRoomState.ClearMapScopedState();
                buyingStoreState.ClearMapScopedState();
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
            return StartTask(() => {
                RoShellCharacterRuntimeSession runtimeSession = existingRuntime;
                RoShellCharacterRuntimeSession createdRuntimeSession = null;

                try {
                    if (runtimeSession == null) {
                        createdRuntimeSession = connectionWorkflow.OpenCharacterRuntime(currentProfile, request.ToLoginRequest());
                        runtimeSession = createdRuntimeSession;
                    }

                    RoLoginCreateCharacterResult result;
                    if (!runtimeSession.TryCreateResultFromCurrentCharacters(request, out result)) {
                        try {
                            result = runtimeSession.CreateCharacter(request);
                        } catch (RoNetworkProtocolException ex) when (IsCharacterNameAlreadyExistsRefusal(ex)) {
                            if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                                RoRuntimeDiagnostics.IncrementDebugLogLine();
                                Debug.Log(
                                    $"RO character create returned name-exists for '{request.CharacterName}'. Refreshing the char session before deciding.");
                            }

                            runtimeSession = ReopenCharacterRuntimeSession(
                                currentProfile,
                                request,
                                runtimeSession,
                                ref createdRuntimeSession);
                            if (!runtimeSession.TryCreateResultFromCurrentCharacters(request, out result)) {
                                try {
                                    result = runtimeSession.CreateCharacter(request);
                                } catch (RoNetworkProtocolException retryEx) when (IsCharacterNameAlreadyExistsRefusal(retryEx)) {
                                    runtimeSession = ReopenCharacterRuntimeSession(
                                        currentProfile,
                                        request,
                                        runtimeSession,
                                        ref createdRuntimeSession);
                                    if (!runtimeSession.TryCreateResultFromCurrentCharacters(request, out result)) {
                                        throw;
                                    }
                                }
                            }
                        }
                    }

                    lock (sync) {
                        if (disposed) {
                            return;
                        }

                        if (createdRuntimeSession != null) {
                            ReplaceCharacterRuntimeLocked(createdRuntimeSession);
                            createdRuntimeSession = null;
                        }

                        characterState.ApplyCreatedCharacterResult(result, request);
                        uiState.SetStage(
                            RoLoginShellStage.CharacterSelect,
                            result.CharacterWasCreated ? Text("ui.network.characterCreated") : Text("ui.network.charactersLoaded"));
                        MarkPhase11UiChangedLocked();
                    }
                } catch {
                    lock (sync) {
                        if (ReferenceEquals(characterRuntimeSession, runtimeSession)) {
                            ClearCharacterRuntimeLocked();
                        }
                    }

                    throw;
                } finally {
                    createdRuntimeSession?.Dispose();
                }
            });
        }

        public Task BeginReserveDeleteCharacter(string account, string password, int characterId) {
            return BeginDeleteCharacterMutation(
                account,
                password,
                characterId,
                string.Empty,
                CharacterDeleteMutation.Reserve);
        }

        public Task BeginConfirmDeleteCharacter(string account, string password, int characterId, string birthdateYyMmDd) {
            return BeginDeleteCharacterMutation(
                account,
                password,
                characterId,
                birthdateYyMmDd,
                CharacterDeleteMutation.Confirm);
        }

        /// <summary>The Delete of a character past its deletion date: CH_DELETE_CHAR2 with the deletion password.</summary>
        public Task BeginDeleteCharacter(string account, string password, int characterId, string deletionKey) {
            return BeginDeleteCharacterMutation(
                account,
                password,
                characterId,
                deletionKey ?? string.Empty,
                CharacterDeleteMutation.Delete);
        }

        public Task BeginCancelDeleteCharacter(string account, string password, int characterId) {
            return BeginDeleteCharacterMutation(
                account,
                password,
                characterId,
                string.Empty,
                CharacterDeleteMutation.Cancel);
        }

        private Task BeginDeleteCharacterMutation(
            string account,
            string password,
            int characterId,
            string birthdateYyMmDd,
            CharacterDeleteMutation mutation) {
            var request = new RoShellLoginRequest(account, password);
            RoServerProfile currentProfile;
            RoShellCharacterRuntimeSession existingRuntime;
            string selectedCharacterName;
            int? basicInfoSequence = null;

            lock (sync) {
                ThrowIfDisposed();
                ThrowIfBusyLocked();
                currentProfile = profileState.Profile;
                existingRuntime = characterRuntimeSession != null && characterRuntimeSession.Matches(currentProfile, request)
                    ? characterRuntimeSession
                    : null;
                if (characterRuntimeSession != null && existingRuntime == null) {
                    ClearCharacterRuntimeLocked();
                }

                selectedCharacterName = ResolveCharacterNameByIdLocked(characterId);
                traceBuffer.Clear();
                uiState.SetStage(RoLoginShellStage.LoadingCharacters, CharacterDeleteStatusText(mutation));
                characterState.SetSelectedCharacterName(selectedCharacterName);
                ClearMapResultLocked();
                gameplayState.Clear();
                vendingState.ClearMapScopedState();
                chatRoomState.ClearMapScopedState();
                buyingStoreState.ClearMapScopedState();
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
            return StartTask(() => {
                RoShellCharacterRuntimeSession runtimeSession = existingRuntime;
                RoShellCharacterRuntimeSession createdRuntimeSession = null;

                try {
                    if (runtimeSession == null) {
                        createdRuntimeSession = connectionWorkflow.OpenCharacterRuntime(currentProfile, request);
                        runtimeSession = createdRuntimeSession;
                    }

                    bool success;
                    int resultCode;
                    RoCharFlowResult characterFlow;
                    switch (mutation) {
                        case CharacterDeleteMutation.Reserve: {
                            RoCharDeleteReserveFlowResult result = runtimeSession.ReserveDeleteCharacter(characterId);
                            success = result.IsSuccess;
                            resultCode = result.Reservation.Result;
                            characterFlow = result.CharacterFlow;
                            break;
                        }
                        case CharacterDeleteMutation.Confirm: {
                            RoCharDeleteConfirmFlowResult result = runtimeSession.ConfirmDeleteCharacter(characterId, birthdateYyMmDd);
                            success = result.IsSuccess;
                            resultCode = result.Confirmation.Result;
                            characterFlow = result.CharacterFlow;
                            break;
                        }
                        case CharacterDeleteMutation.Delete: {
                            RoCharDeleteFlowResult result = runtimeSession.DeleteCharacter(characterId, birthdateYyMmDd);
                            success = result.IsSuccess;
                            resultCode = result.Answer.ErrorCode;
                            characterFlow = result.CharacterFlow;
                            break;
                        }
                        case CharacterDeleteMutation.Cancel: {
                            RoCharDeleteCancelFlowResult result = runtimeSession.CancelDeleteCharacter(characterId);
                            success = result.IsSuccess;
                            resultCode = result.Cancellation.Result;
                            characterFlow = result.CharacterFlow;
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown delete mutation.");
                    }

                    lock (sync) {
                        if (disposed) {
                            return;
                        }

                        if (createdRuntimeSession != null) {
                            ReplaceCharacterRuntimeLocked(createdRuntimeSession);
                            createdRuntimeSession = null;
                        }

                        characterState.ApplyLoadedCharacters(characterFlow.Characters);
                        if (success &&
                            (mutation == CharacterDeleteMutation.Confirm || mutation == CharacterDeleteMutation.Delete) &&
                            !string.IsNullOrWhiteSpace(selectedCharacterName)) {
                            characterState.SelectFirstAvailable();
                        } else if (!string.IsNullOrWhiteSpace(selectedCharacterName)) {
                            characterState.SetSelectedCharacterName(selectedCharacterName);
                        }

                        uiState.SetStage(
                            RoLoginShellStage.CharacterSelect,
                            success ? CharacterDeleteSuccessText(mutation) : "Character delete failed.",
                            success ? string.Empty : CharacterDeleteErrorText(mutation, resultCode));
                        MarkPhase11UiChangedLocked();
                    }
                } catch {
                    lock (sync) {
                        if (ReferenceEquals(characterRuntimeSession, runtimeSession)) {
                            ClearCharacterRuntimeLocked();
                        }
                    }

                    throw;
                } finally {
                    createdRuntimeSession?.Dispose();
                }
            });
        }

        private RoShellCharacterRuntimeSession ReopenCharacterRuntimeSession(
            RoServerProfile currentProfile,
            RoShellCharacterRequest request,
            RoShellCharacterRuntimeSession runtimeSession,
            ref RoShellCharacterRuntimeSession createdRuntimeSession) {
            if (createdRuntimeSession != null) {
                createdRuntimeSession.Dispose();
                createdRuntimeSession = null;
            } else {
                lock (sync) {
                    if (ReferenceEquals(characterRuntimeSession, runtimeSession)) {
                        ClearCharacterRuntimeLocked();
                    }
                }
            }

            createdRuntimeSession = connectionWorkflow.OpenCharacterRuntime(currentProfile, request.ToLoginRequest());
            return createdRuntimeSession;
        }

        private static bool IsCharacterNameAlreadyExistsRefusal(RoNetworkProtocolException exception) {
            return exception != null &&
                !string.IsNullOrWhiteSpace(exception.Message) &&
                exception.Message.IndexOf(
                    "Character creation refused with error code 0.",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string ResolveCharacterNameByIdLocked(int characterId) {
            RoLoginShellCharacter[] characters = characterState.SnapshotCharacters();
            for (int i = 0; i < characters.Length; i++) {
                if (characters[i].CharacterId == characterId) {
                    return characters[i].Name;
                }
            }

            return characterState.SelectedCharacterName;
        }

        private static string CharacterDeleteStatusText(CharacterDeleteMutation mutation) {
            switch (mutation) {
                case CharacterDeleteMutation.Reserve:
                    return "Reserving character deletion...";
                case CharacterDeleteMutation.Confirm:
                case CharacterDeleteMutation.Delete:
                    return "Deleting character...";
                case CharacterDeleteMutation.Cancel:
                    return "Cancelling character deletion...";
                default:
                    return "Updating character deletion...";
            }
        }

        private static string CharacterDeleteSuccessText(CharacterDeleteMutation mutation) {
            switch (mutation) {
                case CharacterDeleteMutation.Reserve:
                    return "Character deletion reserved.";
                case CharacterDeleteMutation.Confirm:
                case CharacterDeleteMutation.Delete:
                    return "Character deleted.";
                case CharacterDeleteMutation.Cancel:
                    return "Character deletion cancelled.";
                default:
                    return "Character deletion updated.";
            }
        }

        private static string CharacterDeleteErrorText(CharacterDeleteMutation mutation, int resultCode) {
            switch (mutation) {
                case CharacterDeleteMutation.Reserve:
                    return ReserveDeleteErrorText(resultCode);
                case CharacterDeleteMutation.Confirm:
                    return ConfirmDeleteErrorText(resultCode);
                case CharacterDeleteMutation.Cancel:
                    return CancelDeleteErrorText(resultCode);
                case CharacterDeleteMutation.Delete:
                    // HC_REFUSE_DELETECHAR: the client shows MsgStr 0x12d whatever the code (checked live, 10-05)
                    return $"Character delete refused with error code {resultCode}.";
                default:
                    return $"Character delete failed with result code {resultCode}.";
            }
        }

        private static string ReserveDeleteErrorText(int resultCode) {
            switch (resultCode) {
                case 0:
                    return "Character deletion could not be reserved.";
                case 3:
                    return "Character deletion could not be reserved because the server database rejected it.";
                case 4:
                    return "Character deletion is blocked while the character is a guild master.";
                case 5:
                    return "Character deletion is blocked while the character is in a party.";
                default:
                    return $"Character deletion reserve failed with result code {resultCode}.";
            }
        }

        private static string ConfirmDeleteErrorText(int resultCode) {
            switch (resultCode) {
                case 2:
                    return "Character deletion is blocked by the server.";
                case 3:
                    return "Character deletion could not be completed because the server database rejected it.";
                case 4:
                    return "Character deletion is not ready yet.";
                case 5:
                    return "Birthdate did not match the server account record.";
                case 6:
                    return "Character deletion failed because the character name did not match.";
                case 7:
                    return "Character deletion failed because the account email did not match.";
                default:
                    return $"Character deletion failed with result code {resultCode}.";
            }
        }

        private static string CancelDeleteErrorText(int resultCode) {
            switch (resultCode) {
                case 2:
                    return "Character deletion cancellation failed on the server.";
                default:
                    return $"Character deletion cancellation failed with result code {resultCode}.";
            }
        }

        public void SelectCharacter(string characterName) {
            lock (sync) {
                characterState.SetSelectedCharacterName(characterName);
                MarkPhase11UiChangedLocked();
            }
        }

        public void ReturnToCharacterSelect() {
            int? basicInfoSequence = null;
            lock (sync) {
                ThrowIfDisposed();
                if (taskRunner.IsBusy) {
                    return;
                }

                ClearMapResultLocked();
                gameplayState.Clear();
                vendingState.ClearMapScopedState();
                chatRoomState.ClearMapScopedState();
                buyingStoreState.ClearMapScopedState();
                ClearLastMapEntryRequestLocked();
                // a lost map connection that brought us here (ZC_RESTART_ACK from the server) is not lost any more,
                // else the next map entry would see it and bounce back
                uiState.ResetReconnect();
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
                uiState.SetStage(RoLoginShellStage.CharacterSelect, Text("ui.network.charactersLoaded"));
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public Task BeginUseHotkey(int hotkeyIndex, int targetAid = 0) {
            return hotkeyInventoryChatWorkflow.BeginUseHotkey(hotkeyIndex, targetAid);
        }

        public Task BeginSetHotkey(int hotkeyIndex, RoShortcutKeyEntry shortcut) {
            return hotkeyInventoryChatWorkflow.BeginSetHotkey(hotkeyIndex, shortcut);
        }

        public Task BeginUpgradeSkill(int skillId) {
            return gameplayPacketWorkflow.Begin(snapshot => {
                byte[] packet = RoShellGameplayCommands.BuildUpgradeSkill(snapshot, skillId, out RoGameplaySkillInfo skill);
                string name = !string.IsNullOrWhiteSpace(skill.Name)
                    ? skill.Name
                    : "skill " + skillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return new RoShellPreparedGameplayPacket(
                    packet,
                    "upgrading " + name,
                    "sent skill-up request for " + name);
            });
        }

        public Task BeginPlannedSkillUpgrade(int skillId) {
            return gameplayPacketWorkflow.Begin(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildPlannedUpgradeSkill(skillId),
                "upgrading skill " + skillId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "sent skill-up request for skill " + skillId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        public Task BeginLegacyAllocateStat(RoStatusStat stat, int amount = 1) {
            return gameplayPacketWorkflow.Begin(snapshot => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildAllocateStat(snapshot, stat, amount),
                "upgrading " + FormatStatusStatName(stat),
                "sent status-up request for " + FormatStatusStatName(stat)));
        }

        public Task BeginUseInventoryItem(int bagIndex) {
            return hotkeyInventoryChatWorkflow.BeginUseInventoryItem(bagIndex);
        }

        public Task BeginDropInventoryItem(int bagIndex, int count = 1) {
            return hotkeyInventoryChatWorkflow.BeginDropInventoryItem(bagIndex, count);
        }

        public Task BeginWearInventoryItem(int bagIndex) {
            return hotkeyInventoryChatWorkflow.BeginWearInventoryItem(bagIndex);
        }

        public Task BeginTakeoffInventoryItem(int bagIndex) {
            return hotkeyInventoryChatWorkflow.BeginTakeoffInventoryItem(bagIndex);
        }

        public Task BeginLegacyUseInventoryItem(int bagIndex) {
            return hotkeyInventoryChatWorkflow.BeginUseInventoryItem(bagIndex);
        }

        public Task BeginLegacyRequestCardCompositionList(int cardBagIndex) {
            return gameplayPacketWorkflow.Begin(
                snapshot => {
                    byte[] packet = RoShellGameplayCommands.BuildRequestCardCompositionList(
                        snapshot,
                        cardBagIndex,
                        out RoItemInstance card);
                    return new RoShellPreparedGameplayPacket(
                        packet,
                        RoShellStatusTextFormatter.FormatInventoryItemStatus(card, "requesting card targets for"),
                        RoShellStatusTextFormatter.FormatInventoryItemStatus(card, "sent card target request for"));
                },
                (_, __) => {
                    gameplayState.BeginCardComposition(cardBagIndex);
                    MarkGameplayChangedLocked(RoGameplayWindowChange.Inventory);
                });
        }

        public Task BeginLegacyInsertCard(int cardBagIndex, int equipBagIndex) {
            return gameplayPacketWorkflow.Begin(snapshot => {
                byte[] packet = RoShellGameplayCommands.BuildInsertCard(
                    snapshot,
                    cardBagIndex,
                    equipBagIndex,
                    out RoItemInstance card,
                    out RoItemInstance equipment);
                return new RoShellPreparedGameplayPacket(
                    packet,
                    RoShellStatusTextFormatter.FormatInventoryItemStatus(card, "inserting"),
                    RoShellStatusTextFormatter.FormatInventoryItemStatus(equipment, "sent card insertion request for"));
            });
        }

        public Task BeginLegacyIdentifyItem(int bagIndex) {
            return gameplayPacketWorkflow.Begin(snapshot => {
                byte[] packet = RoShellGameplayCommands.BuildIdentifyItem(
                    snapshot,
                    bagIndex,
                    out RoItemInstance item);
                return new RoShellPreparedGameplayPacket(
                    packet,
                    RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "identifying"),
                    RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "sent identify request for"));
            });
        }

        public Task BeginLegacyCancelIdentifyItem() {
            return gameplayPacketWorkflow.Begin(_ => {
                byte[] packet = RoShellGameplayCommands.BuildCancelIdentifyItem();
                return new RoShellPreparedGameplayPacket(
                    packet,
                    "Cancelling item appraisal",
                    "Cancelled item appraisal");
            });
        }

        public Task BeginLegacyMakeArrow(uint itemId) {
            return gameplayPacketWorkflow.Begin(
                _ => {
                    bool cancel = itemId == uint.MaxValue;
                    byte[] packet = RoShellGameplayCommands.BuildMakingArrow(profileState.Profile, itemId);
                    return new RoShellPreparedGameplayPacket(
                        packet,
                        cancel ? "closing arrow crafting" : "crafting arrows",
                        cancel ? "sent arrow crafting close request" : "sent arrow crafting request");
                },
                (_, __) => {
                    gameplayState.ClearMakingArrow();
                    MarkGameplayChangedLocked(RoGameplayWindowChange.Inventory);
                });
        }

        public Task BeginLegacyChangeCart(int cartNumber) {
            return gameplayPacketWorkflow.Begin(_ => {
                byte[] packet = RoShellGameplayCommands.BuildChangeCart(profileState.Profile, cartNumber);
                return new RoShellPreparedGameplayPacket(
                    packet,
                    "changing cart",
                    "sent cart appearance request");
            });
        }

        public Task BeginLegacySelectCartDecoration(uint identity, int cartType) {
            return gameplayPacketWorkflow.Begin(_ => {
                byte[] packet = RoShellGameplayCommands.BuildSelectCartDecoration(profileState.Profile, identity, cartType);
                return new RoShellPreparedGameplayPacket(
                    packet,
                    "decorating cart",
                    "sent cart decoration request");
            });
        }

        public Task BeginLegacySelectWarpPoint(ushort skillId, string mapName) {
            return gameplayPacketWorkflow.Begin(
                _ => {
                    bool cancel = string.Equals(mapName, "cancel", StringComparison.OrdinalIgnoreCase);
                    byte[] packet = RoShellGameplayCommands.BuildSelectWarpPoint(profileState.Profile, skillId, mapName);
                    return new RoShellPreparedGameplayPacket(
                        packet,
                        cancel ? "closing teleport menu" : "selecting teleport destination",
                        cancel ? "sent teleport cancel request" : "sent teleport request");
                },
                (_, __) => {
                    gameplayState.ClearTeleportMenu();
                    MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Npc);
                });
        }

        public void SetLocalCardCompositionSelection(int cardBagIndex) {
            lock (sync) {
                ThrowIfDisposed();
                gameplayState.BeginCardComposition(cardBagIndex);
                MarkGameplayChangedLocked(RoGameplayWindowChange.Inventory);
            }
        }

        public void ClearLegacyCardCompositionSelection() {
            lock (sync) {
                ThrowIfDisposed();
                gameplayState.ClearCardComposition();
                MarkGameplayChangedLocked(RoGameplayWindowChange.Inventory);
            }
        }

        public void ClearLegacyItemIdentifySelection() {
            lock (sync) {
                ThrowIfDisposed();
                gameplayState.ClearItemIdentify();
                MarkGameplayChangedLocked(RoGameplayWindowChange.Inventory);
            }
        }

        public void ClearLegacyMakingArrowSelection() {
            lock (sync) {
                ThrowIfDisposed();
                gameplayState.ClearMakingArrow();
                MarkGameplayChangedLocked(RoGameplayWindowChange.Inventory);
            }
        }

        public void ClearLegacyTeleportMenuSelection() {
            lock (sync) {
                ThrowIfDisposed();
                gameplayState.ClearTeleportMenu();
                MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Npc);
            }
        }

        public Task BeginLegacyUseCartItem(int cartIndex) {
            return hotkeyInventoryChatWorkflow.BeginUseCartItem(cartIndex);
        }

        public Task BeginLegacyDropInventoryItem(int bagIndex, int count = 1) {
            return hotkeyInventoryChatWorkflow.BeginDropInventoryItem(bagIndex, count);
        }

        public Task BeginLegacyWearInventoryItem(int bagIndex) {
            return hotkeyInventoryChatWorkflow.BeginWearInventoryItem(bagIndex);
        }

        public Task BeginLegacyTakeoffInventoryItem(int bagIndex) {
            return hotkeyInventoryChatWorkflow.BeginTakeoffInventoryItem(bagIndex);
        }

        public Task BeginLegacySetEquipmentConfig(int config, int value) {
            return gameplayPacketWorkflow.Begin(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildEquipmentConfig(config, value),
                "updating equipment display config",
                "sent equipment display config update"));
        }

        public Task BeginQuestActive(uint questId, bool active) {
            return gameplayPacketWorkflow.Begin(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildQuestActive(questId, active),
                active ? "activating quest" : "deactivating quest",
                active ? "sent quest activate request" : "sent quest deactivate request"));
        }

        public Task BeginSendChatMessage(string message) {
            return hotkeyInventoryChatWorkflow.BeginSendChatMessage(message);
        }

        public Task BeginSendPrivateMessage(string receiverName, string message) {
            // the CZ_WHISPER builder keeps the target and the text (mode +0x22c / +0x214) for the ack's echo
            LastWhisperTarget = receiverName ?? string.Empty;
            LastWhisperText = message ?? string.Empty;
            return hotkeyInventoryChatWorkflow.BeginSendPrivateMessage(receiverName, message);
        }

        /// <summary>The last whisper's target, as ZC_ACK_WHISPER answers it.</summary>
        public string LastWhisperTarget { get; private set; } = string.Empty;

        /// <summary>The last whisper's text, as ZC_ACK_WHISPER answers it.</summary>
        public string LastWhisperText { get; private set; } = string.Empty;

        public Task BeginSendPartyChatMessage(string message) {
            return hotkeyInventoryChatWorkflow.BeginSendPartyChatMessage(message);
        }

        public Task BeginSendGuildChatMessage(string message) {
            return hotkeyInventoryChatWorkflow.BeginSendGuildChatMessage(message);
        }

        public Task BeginSendClanChatMessage(string message) {
            return hotkeyInventoryChatWorkflow.BeginSendClanChatMessage(message);
        }

        public bool TrySendMoveRequest(int x, int y, int direction = 0) {
            return directGameplaySender.TrySendMoveRequest(x, y, direction);
        }

        public bool TrySendAttackRequest(int targetAid, bool repeatAttack = true) {
            return directGameplaySender.TrySendAttackRequest(targetAid, repeatAttack);
        }

        public bool TrySendPickupGroundItemRequest(int itemAid) {
            return directGameplaySender.TrySendPickupGroundItemRequest(itemAid);
        }

        public bool TrySendTargetSkillRequest(int skillId, int skillLevel, int targetAid) {
            return directGameplaySender.TrySendTargetSkillRequest(skillId, skillLevel, targetAid);
        }

        /// <summary>CZ_USE_SKILL_END 0x0b11 for the held GC_ROLLINGCUTTER (0xd284e0 / 0xd44890); false without a map session.</summary>
        public bool TrySendUseSkillEnd() {
            return directGameplaySender.TrySendRawPacket(Ragnarok.Network.RoHeldSkillUse.BuildEnd());
        }

        public bool TrySendActorNameRequest(uint actorId) {
            return directGameplaySender.TrySendActorNameRequest(actorId);
        }

        public bool TrySendSitStandRequest(bool sit) {
            return directGameplaySender.TrySendSitStandRequest(sit);
        }

        public bool TrySendRestartRequest(byte type) {
            return directGameplaySender.TrySendRestartRequest(type);
        }

        public bool TrySendStandingResurrectionRequest() {
            return directGameplaySender.TrySendStandingResurrectionRequest();
        }

        public Task BeginContactNpc(int npcId, byte type = 1) {
            return npcInteractionWorkflow.BeginContactNpc(npcId, type);
        }

        public Task BeginNpcDialogNext() {
            return npcInteractionWorkflow.BeginNpcDialogNext();
        }

        public Task BeginNpcDialogClose() {
            return npcInteractionWorkflow.BeginNpcDialogClose();
        }

        public Task BeginNpcDialogMenuChoice(int choice) {
            return npcInteractionWorkflow.BeginNpcDialogMenuChoice(choice);
        }

        public Task BeginNpcDialogNumberInput(int value) {
            return npcInteractionWorkflow.BeginNpcDialogNumberInput(value);
        }

        public Task BeginNpcDialogTextInput(string value) {
            return npcInteractionWorkflow.BeginNpcDialogTextInput(value);
        }

        public Task BeginPickupGroundItem(int itemAid) {
            return combatSkillWorkflow.BeginPickupGroundItem(itemAid);
        }

        public Task BeginAttackTarget(int targetAid, bool repeatAttack = true) {
            return combatSkillWorkflow.BeginAttackTarget(targetAid, repeatAttack);
        }

        public Task BeginUseSkillOnTarget(int skillId, int skillLevel, int targetAid) {
            return combatSkillWorkflow.BeginUseSkillOnTarget(skillId, skillLevel, targetAid);
        }

        public Task BeginUseSkillOnGround(int skillId, int skillLevel, int x, int y) {
            return combatSkillWorkflow.BeginUseSkillOnGround(skillId, skillLevel, x, y);
        }

        public Task BeginLegacyUseSkillOnTarget(int skillId, int skillLevel, int targetAid) {
            return combatSkillWorkflow.BeginLegacyUseSkillOnTarget(skillId, skillLevel, targetAid);
        }

        public Task BeginLegacyUseSkillOnGround(int skillId, int skillLevel, int x, int y) {
            return combatSkillWorkflow.BeginLegacyUseSkillOnGround(skillId, skillLevel, x, y);
        }

        public Task BeginNpcShopOpenBuy() {
            return npcInteractionWorkflow.BeginNpcShopOpenBuy();
        }

        public Task BeginNpcShopOpenSell() {
            return npcInteractionWorkflow.BeginNpcShopOpenSell();
        }

        public Task BeginNpcShopSelectDealType(byte type) {
            return npcInteractionWorkflow.BeginNpcShopSelectDealType(type);
        }

        public Task BeginNpcShopBuyFirst(int amount = 1) {
            return npcInteractionWorkflow.BeginNpcShopBuyFirst(amount);
        }

        public Task BeginNpcShopBuy(uint itemId, int amount = 1) {
            return npcInteractionWorkflow.BeginNpcShopBuy(itemId, amount);
        }

        public Task BeginNpcShopBuy(params RoNpcShopPurchaseRequest[] items) {
            return npcInteractionWorkflow.BeginNpcShopBuy(items);
        }

        public Task BeginNpcShopSellFirst(int amount = 1) {
            return npcInteractionWorkflow.BeginNpcShopSellFirst(amount);
        }

        public Task BeginNpcShopSell(int bagIndex, int amount = 1) {
            return npcInteractionWorkflow.BeginNpcShopSell(bagIndex, amount);
        }

        public Task BeginNpcShopSell(params RoNpcShopSellRequest[] items) {
            return npcInteractionWorkflow.BeginNpcShopSell(items);
        }

        public Task BeginNpcShopClose() {
            return npcInteractionWorkflow.BeginNpcShopClose();
        }

        public Task BeginNpcMarketPurchase(params RoNpcMarketPurchaseRequest[] items) {
            return npcInteractionWorkflow.BeginNpcMarketPurchase(items);
        }

        public Task BeginNpcMarketClose() {
            return npcInteractionWorkflow.BeginNpcMarketClose();
        }

        public Task BeginNpcBarterPurchase(params RoNpcBarterPurchaseRequest[] items) {
            return npcInteractionWorkflow.BeginNpcBarterPurchase(items);
        }

        public Task BeginNpcBarterClose() {
            return npcInteractionWorkflow.BeginNpcBarterClose();
        }

        public Task BeginNpcExpandedBarterPurchase(params RoNpcExpandedBarterPurchaseRequest[] items) {
            return npcInteractionWorkflow.BeginNpcExpandedBarterPurchase(items);
        }

        public Task BeginNpcExpandedBarterClose() {
            return npcInteractionWorkflow.BeginNpcExpandedBarterClose();
        }

        /// <summary>
        /// Drops the item list of a barter / expanded barter / market / zeny shop (or the pending deal choice) whose windows
        /// closed (the client clears its item manager / shop lists on Cancel and Buy): the shop does not count as open any
        /// more and nothing shows it again.
        /// </summary>
        public void ClearNpcCommerceLocally(RoNpcShopMode mode) {
            int? basicInfoSequence;
            lock (sync) {
                gameplayState.ClearNpcCommerceLocally(mode);
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Npc);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public Task BeginLegacyMoveInventoryItemToStorage(int bagIndex, int amount = 1) {
            return storageWorkflow.BeginLegacyMoveInventoryItemToStorage(bagIndex, amount);
        }

        public Task BeginLegacyMoveStorageItemToInventory(int storageIndex, int amount = 1) {
            return storageWorkflow.BeginLegacyMoveStorageItemToInventory(storageIndex, amount);
        }

        public Task BeginLegacyMoveStorageItemToCart(int storageIndex, int amount = 1) {
            return storageWorkflow.BeginLegacyMoveStorageItemToCart(storageIndex, amount);
        }

        public Task BeginLegacyCloseStorage() {
            return storageWorkflow.BeginLegacyCloseStorage();
        }

        public Task BeginLegacyOpenBank() {
            return bankWorkflow.BeginLegacyOpenBank();
        }

        public Task BeginLegacyCloseBank() {
            return bankWorkflow.BeginLegacyCloseBank();
        }

        public void CloseBankLocally() {
            int? basicInfoSequence;
            lock (sync) {
                gameplayState.CloseBankLocally();
                basicInfoSequence = MarkGameplayChangedLocked(
                    RoGameplayWindowChange.Functional,
                    RoFunctionalWindowChange.Bank);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public Task BeginLegacyCheckBank() {
            return bankWorkflow.BeginLegacyCheckBank();
        }

        public Task BeginLegacyDepositBankZeny(int amount) {
            return bankWorkflow.BeginLegacyDepositBankZeny(amount);
        }

        public Task BeginLegacyWithdrawBankZeny(int amount) {
            return bankWorkflow.BeginLegacyWithdrawBankZeny(amount);
        }

        public void OpenRodexLocally() {
            int? basicInfoSequence;
            lock (sync) {
                rodexState.OpenListLocal();
                basicInfoSequence = MarkGameplayChangedLocked(
                    RoGameplayWindowChange.Functional,
                    RoFunctionalWindowChange.Rodex);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public Task BeginLegacyOpenRodex() {
            return rodexWorkflow.BeginLegacyOpenRodex();
        }

        public Task BeginLegacyRefreshRodex(ulong mailId = 0, ulong returnMailId = 0, ulong accountMailId = 0) {
            return rodexWorkflow.BeginLegacyRefreshRodex(mailId, returnMailId, accountMailId);
        }

        public Task BeginLegacyCloseRodex() {
            return rodexWorkflow.BeginLegacyCloseRodex();
        }

        public void CloseRodexLocally() {
            int? basicInfoSequence;
            lock (sync) {
                rodexState.CloseLocal();
                basicInfoSequence = MarkGameplayChangedLocked(
                    RoGameplayWindowChange.Functional,
                    RoFunctionalWindowChange.Rodex);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public Task BeginLegacyReadRodex(byte openType, ulong mailId) {
            return rodexWorkflow.BeginLegacyReadRodex(openType, mailId);
        }

        public void CloseRodexReadLocally() {
            int? basicInfoSequence;
            lock (sync) {
                bool changed = rodexState.CloseReadLocal();
                basicInfoSequence = changed
                    ? MarkGameplayChangedLocked(
                        RoGameplayWindowChange.Functional,
                        RoFunctionalWindowChange.Rodex)
                    : null;
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public Task BeginLegacyNextRodex(byte openType, ulong mailId) {
            return rodexWorkflow.BeginLegacyNextRodex(openType, mailId);
        }

        public Task BeginLegacyDeleteRodex(byte openType, ulong mailId) {
            return rodexWorkflow.BeginLegacyDeleteRodex(openType, mailId);
        }

        public Task BeginLegacyRetrieveRodexZeny(byte openType, ulong mailId) {
            return rodexWorkflow.BeginLegacyRetrieveRodexZeny(openType, mailId);
        }

        public Task BeginLegacyRetrieveRodexItems(byte openType, ulong mailId) {
            return rodexWorkflow.BeginLegacyRetrieveRodexItems(openType, mailId);
        }

        public Task BeginLegacyOpenWriteRodex(string receiverName) {
            lock (sync) {
                rodexState.OpenComposeLocal(receiverName);
            }

            return rodexWorkflow.BeginLegacyOpenWriteRodex(receiverName);
        }

        public Task BeginLegacyCancelWriteRodex() {
            CloseRodexComposeLocally();
            return rodexWorkflow.BeginLegacyCancelWriteRodex();
        }

        public void CloseRodexComposeLocally() {
            int? basicInfoSequence;
            lock (sync) {
                bool changed = rodexState.CloseComposeLocal();
                basicInfoSequence = changed
                    ? MarkGameplayChangedLocked(
                        RoGameplayWindowChange.Functional,
                        RoFunctionalWindowChange.Rodex)
                    : null;
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public Task BeginLegacyCheckRodexReceiver(string receiverName) {
            string sanitized = RoRodexRuntime.SanitizeReceiverName(receiverName);
            int? basicInfoSequence;
            lock (sync) {
                rodexState.CheckReceiverLocal(sanitized);
                basicInfoSequence = MarkGameplayChangedLocked(
                    RoGameplayWindowChange.Functional,
                    RoFunctionalWindowChange.Rodex);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
            return rodexWorkflow.BeginLegacyCheckRodexReceiver(sanitized);
        }

        public Task BeginLegacyAddRodexItem(int bagIndex, int amount) {
            return rodexWorkflow.BeginLegacyAddRodexItem(bagIndex, amount);
        }

        public Task BeginLegacyRemoveRodexItem(int bagIndex, int amount) {
            return rodexWorkflow.BeginLegacyRemoveRodexItem(bagIndex, amount);
        }

        public Task BeginLegacySendRodexMail(string receiverName, string senderName, long zeny, string title, string body, uint receiverCharacterId) {
            return rodexWorkflow.BeginLegacySendRodexMail(receiverName, senderName, zeny, title, body, receiverCharacterId);
        }

        public void OpenAttendanceLocally() {
            int? basicInfoSequence;
            lock (sync) {
                attendanceState.OpenLocal();
                basicInfoSequence = MarkGameplayChangedLocked(
                    RoGameplayWindowChange.Functional,
                    RoFunctionalWindowChange.Attendance);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public void CloseAttendanceLocally() {
            int? basicInfoSequence;
            lock (sync) {
                attendanceState.CloseLocal();
                basicInfoSequence = MarkGameplayChangedLocked(
                    RoGameplayWindowChange.Functional,
                    RoFunctionalWindowChange.Attendance);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public Task BeginLegacyOpenAttendance() {
            OpenAttendanceLocally();
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildAttendanceOpen(profileState.Profile),
                "opening attendance check",
                "sent attendance open request");
        }

        public Task BeginLegacyClaimAttendance() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildAttendanceClaim(profileState.Profile),
                "claiming attendance reward",
                "sent attendance claim request");
        }

        public Task BeginLegacyPartyInvite(string characterName) {
            return socialEconomyWorkflow.BeginPartyInvite(characterName);
        }

        public Task BeginLegacyPartyLeave() {
            return socialEconomyWorkflow.BeginPartyLeave();
        }

        public Task BeginLegacyPartyInviteResponse(uint invitationId, bool accept) {
            return socialEconomyWorkflow.BeginPartyInviteResponse(invitationId, accept);
        }

        public Task BeginLegacyCreateParty(string partyName, byte itemPickupRule, byte itemDivisionRule) {
            return socialEconomyWorkflow.BeginCreateParty(partyName, itemPickupRule, itemDivisionRule);
        }

        public Task BeginLegacyExpelPartyMember(uint accountId, string characterName) {
            return socialEconomyWorkflow.BeginExpelPartyMember(accountId, characterName);
        }

        public Task BeginLegacyChangePartyLeader(uint accountId) {
            return socialEconomyWorkflow.BeginChangePartyLeader(accountId);
        }

        public async Task BeginLegacyPartyOptions(uint expOption, byte itemPickupRule, byte itemDivisionRule) {
            await socialEconomyWorkflow.BeginPartyOptions(expOption, itemPickupRule, itemDivisionRule).ConfigureAwait(false);
            lock (sync) {
                partyState.ApplyLocalOptions(expOption, itemPickupRule, itemDivisionRule);
                MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Party);
            }
        }

        public Task BeginLegacyAddFriend(string characterName) {
            return socialEconomyWorkflow.BeginAddFriend(characterName);
        }

        public Task BeginLegacyRemoveFriend(uint accountId, uint characterId) {
            return socialEconomyWorkflow.BeginRemoveFriend(accountId, characterId);
        }

        public async Task BeginLegacyFriendRequestResponse(uint accountId, uint characterId, bool accept) {
            await socialEconomyWorkflow.BeginFriendRequestResponse(accountId, characterId, accept).ConfigureAwait(false);
            lock (sync) {
                if (friendsState.ClearPendingRequest(accountId, characterId)) {
                    MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Friends);
                }
            }
        }

        public Task BeginLegacyGuildChat(string message) {
            return socialEconomyWorkflow.BeginGuildChat(message);
        }

        public Task BeginLegacyRequestTrade(uint targetActorId, string targetName = "") {
            return socialEconomyWorkflow.BeginRequestTrade(targetActorId, targetName);
        }

        public Task BeginLegacyCheckPlayerEquipment(uint targetActorId) {
            return socialEconomyWorkflow.BeginCheckPlayerEquipment(targetActorId);
        }

        public Task BeginLegacyAnswerTradeRequest(bool accept) {
            return socialEconomyWorkflow.BeginAnswerTradeRequest(accept);
        }

        public Task BeginLegacyAddTradeItem(int bagIndex, int amount = 1) {
            return socialEconomyWorkflow.BeginAddTradeItem(bagIndex, amount);
        }

        public Task BeginLegacyAddTradeZeny(int amount) {
            return socialEconomyWorkflow.BeginAddTradeZeny(amount);
        }

        public Task BeginLegacyLockTrade() {
            return socialEconomyWorkflow.BeginLockTrade();
        }

        public Task BeginLegacyCancelTrade() {
            return socialEconomyWorkflow.BeginCancelTrade();
        }

        public Task BeginLegacyConfirmTrade() {
            return socialEconomyWorkflow.BeginConfirmTrade();
        }

        public Task BeginLegacyMoveInventoryItemToCart(int bagIndex, int amount = 1) {
            return socialEconomyWorkflow.BeginMoveInventoryItemToCart(bagIndex, amount);
        }

        public Task BeginLegacyMoveCartItemToInventory(int cartIndex, int amount = 1) {
            return socialEconomyWorkflow.BeginMoveCartItemToInventory(cartIndex, amount);
        }

        public Task BeginLegacyMoveCartItemToStorage(int cartIndex, int amount = 1) {
            return socialEconomyWorkflow.BeginMoveCartItemToStorage(cartIndex, amount);
        }

        public Task BeginLegacyCartOff() {
            return socialEconomyWorkflow.BeginCartOff();
        }

        public Task BeginLegacyFeedPet() {
            return socialEconomyWorkflow.BeginFeedPet();
        }

        public Task BeginLegacyOpenVendingStore(string shopName, RoVendingOpenStoreItem[] items) {
            return gameplayPacketWorkflow.Begin(
                _ => new RoShellPreparedGameplayPacket(
                    RoShellGameplayCommands.BuildVendingOpenStore(profileState.Profile, shopName, items ?? Array.Empty<RoVendingOpenStoreItem>()),
                    "opening vending store",
                    "sent vending open request"),
                (_, __) => {
                    vendingState.RememberRequestedTitle(shopName);
                    MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Vending);
                });
        }

        public Task BeginLegacyCancelVendingSetup(string shopName = "") {
            return gameplayPacketWorkflow.Begin(
                _ => new RoShellPreparedGameplayPacket(
                    RoShellGameplayCommands.BuildVendingCancelSetup(profileState.Profile, shopName),
                    "canceling vending setup",
                    "sent vending cancel request"),
                null,
                (_, __, ___) => {
                    vendingState.CancelSetupLocally();
                    MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Vending);
                });
        }

        public Task BeginLegacyCloseVendingStore() {
            return gameplayPacketWorkflow.Begin(
                _ => new RoShellPreparedGameplayPacket(
                    RoShellGameplayCommands.BuildVendingCloseStore(profileState.Profile),
                    "closing vending store",
                    "sent vending close request"),
                null,
                (_, __, ___) => {
                    if (vendingState.CloseOwnStoreLocally(mapSessionState.LocalActorId)) {
                        MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Vending);
                    }
                });
        }

        public Task BeginLegacyRequestPlayerShop(uint ownerAid) {
            return gameplayPacketWorkflow.Begin(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildVendingRequestPlayerShop(ownerAid, profileState.Profile),
                "requesting player shop",
                "sent player shop request"));
        }

        public Task BeginLegacyPurchaseFromPlayerShop(uint ownerAid, uint uniqueId, RoPlayerShopPurchaseRequest[] items) {
            return gameplayPacketWorkflow.Begin(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildVendingPurchaseFromPlayerShop(ownerAid, uniqueId, profileState.Profile, items ?? Array.Empty<RoPlayerShopPurchaseRequest>()),
                "buying from player shop",
                "sent player shop purchase request"));
        }

        public void Reset() {
            int? basicInfoSequence = null;
            lock (sync) {
                ThrowIfBusyLocked();
                traceBuffer.Clear();
                uiState.SetStage(RoLoginShellStage.Login, Text("ui.login.status"));
                characterState.Clear();
                ClearCharacterRuntimeLocked();
                ClearLastMapEntryRequestLocked();
                ClearMapResultLocked();
                gameplayState.Clear();
                vendingState.ClearMapScopedState();
                chatRoomState.ClearMapScopedState();
                buyingStoreState.ClearMapScopedState();
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
        }

        public void Dispose() {
            lock (sync) {
                disposed = true;
                RoNetworkRuntimeDiagnostics.DiagnosticRecorded -= HandleNetworkDiagnosticRecorded;
                ClearCharacterRuntimeLocked();
                mapSessionState.ClearAndDispose();
                ClearSocialStateLocked();
                vendingState.ClearMapScopedState();
                chatRoomState.ClearMapScopedState();
                buyingStoreState.ClearMapScopedState();
                rodexState.Dispose();
                mailState.Dispose();
                attendanceState.Dispose();
            }

            RoRuntimeDiagnostics.Apply(RoRuntimeDiagnosticsOptions.DisabledForRuntime());
        }

        private Task StartTask(Action action, Action<Exception> onFailureLocked = null) {
            return taskRunner.Start(action, ex => {
                Debug.LogWarning($"RO login shell operation failed: {ex}");
                lock (sync) {
                    uiState.SetStage(RoLoginShellStage.Failed, Text("ui.error.connection"), ex.Message);
                    onFailureLocked?.Invoke(ex);
                    MarkPhase11UiChangedLocked();
                }
            });
        }

        private bool IsCurrentMapSessionLocked(RoLiveMapSession liveMapSession) {
            return mapSessionState.IsCurrentSession(liveMapSession);
        }

        private RoLiveMapSession RequireOpenMapSessionLocked() {
            if (!TryGetOpenMapSessionLocked(out RoLiveMapSession liveMapSession)) {
                throw new InvalidOperationException("Map session is not open.");
            }

            return liveMapSession;
        }

        private bool TryGetOpenMapSessionLocked(out RoLiveMapSession liveMapSession) {
            liveMapSession = null;
            return !disposed &&
                uiState.Stage == RoLoginShellStage.MapEntered &&
                mapSessionState.TryGetOpenSession(out liveMapSession);
        }

        private void ClearMapResultLocked() {
            mapSessionState.ClearAndDispose();
            liveDrainHistory.ClearAll();
            pendingAutoRunSkills.Clear();
            ClearSocialStateLocked();
            rodexState.CloseLocal();
            ClearActionLockLocked();
        }

        private void ClearSocialStateLocked() {
            partyState.Clear();
            friendsState.Clear();
        }

        private void ReplaceCharacterRuntimeLocked(RoShellCharacterRuntimeSession runtimeSession) {
            if (ReferenceEquals(characterRuntimeSession, runtimeSession)) {
                return;
            }

            ClearCharacterRuntimeLocked();
            characterRuntimeSession = runtimeSession;
        }

        private void ClearCharacterRuntimeLocked() {
            characterRuntimeSession?.Dispose();
            characterRuntimeSession = null;
            pendingLogin?.Dispose();
            pendingLogin = null;
        }

        private bool HasLastMapEntryRequestLocked() {
            return hasLastMapEntryRequest;
        }

        private void RememberLastMapEntryRequestLocked(RoShellCharacterRequest request) {
            lastMapEntryRequest = request;
            hasLastMapEntryRequest = true;
        }

        private void ClearLastMapEntryRequestLocked() {
            lastMapEntryRequest = default;
            hasLastMapEntryRequest = false;
        }

        private void RestartLiveReceiveLoopIfCurrentLocked(RoLiveMapSession liveMapSession) {
            if (!disposed &&
                mapSessionState.IsCurrentSession(liveMapSession) &&
                liveMapSession != null &&
                !liveMapSession.IsDisposed) {
                if (TryStartMapTransitionTaskLocked(liveMapSession, liveDrainHistory.LatestDrain)) {
                    return;
                }

                mapSessionState.RestartReceiveIfCurrent(liveMapSession);
            }
        }

        private bool HandleLiveReceiveDrain(RoLiveMapSession liveMapSession, RoMapEntryDrainResult drain) {
            using (RoProfilerMarkers.NetworkPacketBatch.Auto())
            using (NetworkApplyPacketMarker.Auto()) {
                RoRuntimePerfCounters.AddPacketsProcessed(drain?.Packets?.Length ?? 0);
                bool stop = false;
                bool transitionStarted = false;
                bool shouldCheckBank = false;
                bool requestFriendNames = false;
                bool requestGuildNames = false;
                int? basicInfoSequence = null;
                bool logRuntimePerf = RoRuntimeDiagnostics.RuntimePerfLogEnabled &&
                    RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics;
                long lockWaitStartedAtMilliseconds = logRuntimePerf ? RoMonotonicTime.NowMilliseconds : 0;
                long lockEnteredAtMilliseconds = 0;
                long lockExitedAtMilliseconds = 0;
                bool lockTaken = false;
                try {
                    Monitor.Enter(sync, ref lockTaken);
                    if (logRuntimePerf) {
                        lockEnteredAtMilliseconds = RoMonotonicTime.NowMilliseconds;
                    }

                    if (disposed ||
                        !mapSessionState.IsCurrentSession(liveMapSession)) {
                        return false;
                    }

                    gameplayState.ApplyDrain(drain, mapSessionState.LocalActorId);
                    ApplySocialDrainLocked(drain, mapSessionState.LocalActorId);
                    requestFriendNames = friendNamesPending;
                    friendNamesPending = false;
                    requestGuildNames = guildNamesPending;
                    guildNamesPending = false;
                    ApplyVendingDrainLocked(drain, mapSessionState.LocalActorId);
                    ApplyRodexDrainLocked(drain);
                    ApplyAttendanceDrainLocked(drain);
                    ApplyPlayerViewEquipmentDrainLocked(drain);
                    if (HasGameplayEvents(drain)) {
                        RoGameplayWindowChange gameplayChanges = ClassifyGameplayWindowChanges(drain);
                        RoFunctionalWindowChange functionalChanges = ClassifyFunctionalWindowChanges(drain);
                        if (gameplayChanges != RoGameplayWindowChange.None ||
                            functionalChanges != RoFunctionalWindowChange.None ||
                            !IsPresentationOnlyDrain(drain)) {
                            basicInfoSequence = MarkGameplayChangedLocked(gameplayChanges, functionalChanges);
                        }

                        shouldCheckBank = ContainsSuccessfulBankOpenAck(drain);
                    }

                    PublishLiveDrainLocked(drain);
                    ApplyActionLockLocked(drain, RoLocalActionLockRequest.None, DateTime.UtcNow);
                    UpdateLiveDrainStatusLocked(drain);
                    if (drain.ParseFailureCount > 0) {
                        ReportDrainParseFailuresLocked(drain);
                    }

                    stop = TryHandleStoppedDrainLocked(drain);
                    if (!stop) {
                        transitionStarted = TryStartMapTransitionTaskLocked(liveMapSession, drain);
                    }
                } finally {
                    if (lockTaken) {
                        if (logRuntimePerf) {
                            lockExitedAtMilliseconds = RoMonotonicTime.NowMilliseconds;
                        }

                        Monitor.Exit(sync);
                        if (logRuntimePerf) {
                            LogLiveReceiveDrainLockPerf(
                                drain,
                                lockEnteredAtMilliseconds - lockWaitStartedAtMilliseconds,
                                lockExitedAtMilliseconds - lockEnteredAtMilliseconds);
                        }
                    }
                }

                PublishBasicInfoSequenceChanged(basicInfoSequence);
                if (shouldCheckBank) {
                    _ = BeginLegacyCheckBankAfterOpenAck();
                }

                if (requestFriendNames) {
                    _ = RequestNamelessFriendNamesAsync();
                }

                if (requestGuildNames) {
                    _ = RequestNamelessGuildNamesAsync();
                }

                return !stop && !transitionStarted;
            }
        }

        private static void LogLiveReceiveDrainLockPerf(
            RoMapEntryDrainResult drain,
            long lockWaitMilliseconds,
            long lockHeldMilliseconds) {
            int packetCount = drain?.Packets?.Length ?? 0;
            int eventCount = drain?.OrderedEvents?.Length ?? 0;
            int parseFailureCount = drain?.ParseFailureCount ?? 0;
            bool stopped = drain?.StoppedOnUnsupportedPacket == true;
            Debug.Log(
                $"RO live drain handler lock waitMs={lockWaitMilliseconds} heldMs={lockHeldMilliseconds} packets={packetCount} events={eventCount} parseFailures={parseFailureCount} stopped={stopped}");
        }

        /// <summary>Diagnostics: a party invitation or a friend request as if the server had sent it.</summary>
        internal void InjectSocialEventForDiagnostics(RoMapEntryDrainEvent drainEvent) {
            lock (sync) {
                switch (drainEvent.Kind) {
                    case RoMapEntryDrainEventKind.PartyInvite:
                        partyState.Apply(drainEvent.PartyInvite);
                        break;
                    case RoMapEntryDrainEventKind.FriendRequest:
                        friendsState.Apply(drainEvent.FriendRequest);
                        break;
                    default:
                        return;
                }

                MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.Party | RoFunctionalWindowChange.Friends);
            }
        }

        /// <summary>
        /// A friend list without names (packetver 20200401, often received during the map entry): asks each nameless
        /// friend's name with CZ_REQNAME_BYGID on the first live drain after it.
        /// </summary>
        private async Task RequestNamelessFriendNamesAsync() {
            var characterIds = new List<uint>();
            friendsState.CollectNameless(characterIds);
            foreach (uint characterId in characterIds) {
                try {
                    await socialEconomyWorkflow.BeginRequestNameByGid(characterId).ConfigureAwait(false);
                } catch {
                    return; // no CZ_REQNAME_BYGID in this profile (its lists carry the names) or the session closed
                }
            }
        }

        private async Task BeginLegacyCheckBankAfterOpenAck() {
            try {
                await BeginLegacyCheckBank().ConfigureAwait(false);
            } catch {
            }
        }

        private bool TryHandleStoppedDrainLocked(RoMapEntryDrainResult drain) {
            if (drain == null || !drain.StoppedOnUnsupportedPacket) {
                return false;
            }

            uiState.SetError(
                Text("ui.error.connection"),
                string.IsNullOrWhiteSpace(drain.StopReason)
                    ? "Map receive loop stopped on an unsupported packet."
                    : drain.StopReason);
            return true;
        }

        private void ApplySocialMapEntryLocked(RoLoginEnterMapResult result) {
            if (result == null) {
                return;
            }

            uint localActorId = result.Aid.AccountId == 0 ? 0u : unchecked((uint) result.Aid.AccountId);
            ApplySocialDrainLocked(result.FollowUp, localActorId);
            ApplySocialDrainLocked(result.InitialState, localActorId);
            ApplySocialDrainLocked(result.MoveResponse, localActorId);
            ApplyVendingDrainLocked(result.FollowUp, localActorId);
            ApplyVendingDrainLocked(result.InitialState, localActorId);
            ApplyVendingDrainLocked(result.MoveResponse, localActorId);
            // and RODEX: the login check (pc_reg_received → ZC_NOTIFY_UNREADMAIL 0x09e7) arrives with the entry
            ApplyRodexDrainLocked(result.FollowUp);
            ApplyRodexDrainLocked(result.InitialState);
            ApplyRodexDrainLocked(result.MoveResponse);
            // the raw carrier too: the map entry brings e.g. ZC_ALL_ACH_LIST
            ApplyPlayerViewEquipmentDrainLocked(result.FollowUp);
            ApplyPlayerViewEquipmentDrainLocked(result.InitialState);
            ApplyPlayerViewEquipmentDrainLocked(result.MoveResponse);
        }

        private void ApplySocialDrainLocked(RoMapEntryDrainResult drain, uint localActorId) {
            if (drain == null || drain.OrderedEvents == null) {
                return;
            }

            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                RoMapEntryDrainEvent drainEvent = events[i];
                switch (drainEvent.Kind) {
                    case RoMapEntryDrainEventKind.PartyOption:
                        partyState.Apply(drainEvent.PartyOption);
                        break;
                    case RoMapEntryDrainEventKind.PartyCreateAck:
                        partyState.Apply(drainEvent.PartyCreateAck);
                        break;
                    case RoMapEntryDrainEventKind.PartyConfig:
                        partyState.Apply(drainEvent.PartyConfig);
                        break;
                    case RoMapEntryDrainEventKind.PartyLeaderChanged:
                        partyState.Apply(drainEvent.PartyLeaderChanged);
                        break;
                    case RoMapEntryDrainEventKind.PartyInfo:
                        partyState.Apply(drainEvent.PartyInfo);
                        break;
                    case RoMapEntryDrainEventKind.PartyMemberInfo:
                        partyState.Apply(drainEvent.PartyMemberInfo);
                        break;
                    case RoMapEntryDrainEventKind.PartyMemberLeave:
                        partyState.Apply(drainEvent.PartyMemberLeave, localActorId);
                        break;
                    case RoMapEntryDrainEventKind.PartyPosition:
                        partyState.Apply(drainEvent.PartyPosition);
                        break;
                    case RoMapEntryDrainEventKind.PartyHp:
                        partyState.Apply(drainEvent.PartyHp);
                        break;
                    case RoMapEntryDrainEventKind.PartyAlive:
                        partyState.Apply(drainEvent.PartyAlive);
                        break;
                    case RoMapEntryDrainEventKind.PartyJobAndLevel:
                        partyState.Apply(drainEvent.PartyJobAndLevel);
                        break;
                    case RoMapEntryDrainEventKind.PartyInvite:
                        partyState.Apply(drainEvent.PartyInvite);
                        break;
                    case RoMapEntryDrainEventKind.PartyInviteAck:
                        partyState.Apply(drainEvent.PartyInviteAck);
                        break;
                    case RoMapEntryDrainEventKind.FriendsList:
                        friendsState.Apply(drainEvent.FriendsList);
                        friendNamesPending = true;
                        break;
                    case RoMapEntryDrainEventKind.CharacterNameByGid:
                        friendsState.ApplyName(drainEvent.CharacterNameByGid.CharacterId, drainEvent.CharacterNameByGid.Name);
                        ApplyGuildEventLocked(drainEvent);
                        break;
                    case RoMapEntryDrainEventKind.GuildBelongInfo:
                    case RoMapEntryDrainEventKind.GuildNotice:
                    case RoMapEntryDrainEventKind.GuildLeaveResult:
                    case RoMapEntryDrainEventKind.GuildManager:
                    case RoMapEntryDrainEventKind.GuildInvite:
                    case RoMapEntryDrainEventKind.GuildInviteResult:
                        ApplyGuildEventLocked(drainEvent);
                        break;
                    case RoMapEntryDrainEventKind.ChatRoomCreateAck:
                        chatRoomState.ApplyCreateResult(drainEvent.ChatRoomResult.Result, localActorId, characterState.SelectedCharacterName);
                        break;
                    case RoMapEntryDrainEventKind.ChatRoomRefuse:
                        chatRoomState.ApplyRefuse(drainEvent.ChatRoomResult.Result);
                        break;
                    case RoMapEntryDrainEventKind.ChatRoomInfo:
                        chatRoomState.ApplyRoom(drainEvent.ChatRoomInfo);
                        break;
                    case RoMapEntryDrainEventKind.ChatRoomDestroy:
                        chatRoomState.ApplyDestroy(drainEvent.DestroyedChatId);
                        break;
                    case RoMapEntryDrainEventKind.ChatRoomEnter:
                        chatRoomState.ApplyEnter(drainEvent.ChatRoomEnter.ChatId, drainEvent.ChatRoomEnter.Members);
                        break;
                    case RoMapEntryDrainEventKind.ChatRoomMemberJoin:
                        chatRoomState.ApplyMemberJoin(drainEvent.ChatRoomMember.Users, drainEvent.ChatRoomMember.Name);
                        break;
                    case RoMapEntryDrainEventKind.ChatRoomMemberExit:
                        chatRoomState.ApplyMemberExit(drainEvent.ChatRoomMember.Users, drainEvent.ChatRoomMember.Name, drainEvent.ChatRoomMember.Kicked,
                            characterState.SelectedCharacterName);
                        break;
                    case RoMapEntryDrainEventKind.ChatRoomRole:
                        chatRoomState.ApplyRole(drainEvent.ChatRoomRole.Role, drainEvent.ChatRoomRole.Name);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreSetup:
                        buyingStoreState.ApplySetup(drainEvent.BuyingStoreResult.Result);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreOpenFailed:
                        buyingStoreState.ApplyOpenFailed(drainEvent.BuyingStoreResult.Result, drainEvent.BuyingStoreResult.Value);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreOwnList:
                        buyingStoreState.ApplyOwnList(drainEvent.BuyingStoreList, localActorId);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreItemList:
                        buyingStoreState.ApplyShop(drainEvent.BuyingStoreList);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreEntry:
                        buyingStoreState.ApplyEntry(drainEvent.BuyingStoreEntry);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreDisappear:
                        buyingStoreState.ApplyDisappear(drainEvent.BuyingStoreEntry.OwnerAid);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreBuyerFailed:
                        buyingStoreState.ApplyBuyerFailed(drainEvent.BuyingStoreResult.Result);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreSold:
                        buyingStoreState.ApplySold(drainEvent.BuyingStoreSold);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreSellerFailed:
                        buyingStoreState.ApplySellerFailed(drainEvent.BuyingStoreResult);
                        break;
                    case RoMapEntryDrainEventKind.BuyingStoreUpdate:
                        buyingStoreState.ApplyUpdate(drainEvent.BuyingStoreUpdate);
                        break;
                    case RoMapEntryDrainEventKind.RepairItemList:
                        repairList = drainEvent.RepairItemList;
                        repairListSerial++;
                        break;
                    case RoMapEntryDrainEventKind.RepairItemResult:
                        repairResult = drainEvent.RepairItemResult.Result;
                        repairResultSerial++;
                        break;
                    case RoMapEntryDrainEventKind.WeaponRefineList:
                        weaponRefineList = drainEvent.WeaponRefineList;
                        weaponRefineListSerial++;
                        break;
                    case RoMapEntryDrainEventKind.WeaponRefineResult:
                        weaponRefineResult = drainEvent.WeaponRefineResult;
                        weaponRefineResultSerial++;
                        break;
                    case RoMapEntryDrainEventKind.AutoSpellList:
                        autoSpellList = drainEvent.AutoSpellList.SkillIds;
                        autoSpellListSerial++;
                        break;
                    case RoMapEntryDrainEventKind.MergeItemOpen:
                        mergeOpen = drainEvent.MergeItemOpen;
                        mergeOpenSerial++;
                        break;
                    case RoMapEntryDrainEventKind.MakableItemList:
                        makableList = drainEvent.MakableItemList;
                        makableListSerial++;
                        break;
                    case RoMapEntryDrainEventKind.PetProperty:
                        pet = RoClassicPetState.From(drainEvent.PetProperty, pet.Gid);
                        petSerial++;
                        break;
                    case RoMapEntryDrainEventKind.HomunculusProperty:
                        // the 0x0b2f parser keeps the flags byte in the legacy struct's item id slot (no item in that layout)
                        homun = RoClassicHomunState.From(drainEvent.HomunculusProperty, homun.Gid, unchecked((int) drainEvent.HomunculusProperty.ItemId));
                        homunSerial++;
                        break;
                    case RoMapEntryDrainEventKind.HomunculusChangeState:
                        // 0xc47c70: state 0 = the actor (active), 1 intimacy, 2 hunger
                        homun = homun.Apply(drainEvent.CompanionChangeState);
                        homunSerial++;
                        break;
                    case RoMapEntryDrainEventKind.HomunculusParameterChange:
                        homun = homun.Apply(drainEvent.CompanionParameterChange);
                        homunSerial++;
                        break;
                    case RoMapEntryDrainEventKind.MercenaryInfo:
                        merc = RoClassicMercState.From(drainEvent.MercenaryInfo);
                        mercSerial++;
                        break;
                    case RoMapEntryDrainEventKind.HomunculusSkillInfoList:
                        homunSkills = drainEvent.CompanionSkillInfoList.Skills ?? Array.Empty<RoZcSkillInfo>();
                        companionSkillSerial++;
                        break;
                    case RoMapEntryDrainEventKind.MercenarySkillInfoList:
                        mercSkills = drainEvent.CompanionSkillInfoList.Skills ?? Array.Empty<RoZcSkillInfo>();
                        companionSkillSerial++;
                        break;
                    case RoMapEntryDrainEventKind.HomunculusSkillUpdate: {
                        // 0x0239 after a skill up: level, sp, range and the upgradable flag of one skill
                        RoZcSkillInfo update = drainEvent.HomunculusSkillUpdate;
                        var next = (RoZcSkillInfo[]) homunSkills.Clone();
                        for (int si = 0; si < next.Length; si++) {
                            RoZcSkillInfo s = next[si];
                            if (s.SkillId != update.SkillId) continue;
                            next[si] = new RoZcSkillInfo(s.SkillId, s.Type, update.Level, update.SpCost, update.AttackRange, s.Name, update.Upgradable, update.Level);
                        }

                        homunSkills = next;
                        companionSkillSerial++;
                        break;
                    }
                    case RoMapEntryDrainEventKind.MercenaryParameterChange:
                        merc = merc.Apply(drainEvent.MercenaryParameter);
                        mercSerial++;
                        break;
                    case RoMapEntryDrainEventKind.PetChangeState:
                        ApplyPetChange(drainEvent.PetChangeState);
                        break;
                    case RoMapEntryDrainEventKind.PetEggList:
                        petEggList = drainEvent.PetEggList;
                        petEggListSerial++;
                        break;
                    case RoMapEntryDrainEventKind.PetCaptureStart:
                        petCaptureStartSerial++;
                        break;
                    case RoMapEntryDrainEventKind.PetCaptureResult:
                        petCaptureResult = drainEvent.PetCaptureResult;
                        petCaptureResultSerial++;
                        break;
                    case RoMapEntryDrainEventKind.PetEvolutionResult:
                        petEvolutionResult = drainEvent.PetEvolutionResult;
                        petEvolutionSerial++;
                        break;
                    case RoMapEntryDrainEventKind.ServerMapMark:
                        minimapMarkers.Apply(drainEvent, localActorId);
                        break;
                    case RoMapEntryDrainEventKind.BattlefieldPosition:
                        minimapMarkers.ApplyBattlefieldPosition(drainEvent.BattlefieldPosition, localActorId);
                        break;
                    case RoMapEntryDrainEventKind.PetFeedResult:
                        petFeed = drainEvent.PetFeedResult;
                        petFeedSerial++;
                        break;
                    case RoMapEntryDrainEventKind.MakingItemResult:
                        makingResult = drainEvent.MakingItemResult;
                        makingResultSerial++;
                        break;
                    case RoMapEntryDrainEventKind.MergeItemResult:
                        mergeResult = drainEvent.MergeItemResult;
                        mergeResultSerial++;
                        break;
                    case RoMapEntryDrainEventKind.PlayerChat:
                        if (chatRoomState.InRoom) {
                            chatRoomState.ApplyLine(drainEvent.PlayerChat.Message);
                            MarkGameplayChangedLocked(RoGameplayWindowChange.Functional, RoFunctionalWindowChange.ChatRoom);
                        }

                        break;
                    case RoMapEntryDrainEventKind.FriendState:
                        friendsState.Apply(drainEvent.FriendState);
                        break;
                    case RoMapEntryDrainEventKind.FriendRequest:
                        friendsState.Apply(drainEvent.FriendRequest);
                        break;
                    case RoMapEntryDrainEventKind.FriendAddResult:
                        friendsState.Apply(drainEvent.FriendAddResult);
                        break;
                    case RoMapEntryDrainEventKind.FriendDelete:
                        friendsState.Apply(drainEvent.FriendDelete);
                        break;
                }
            }
        }

        private void ApplyVendingDrainLocked(RoMapEntryDrainResult drain, uint localActorId) {
            if (vendingState.ApplyDrain(drain, localActorId)) {
                MarkGameplayChangedLocked(
                    RoGameplayWindowChange.Functional,
                    RoFunctionalWindowChange.Vending);
            }
        }

        private void ApplyRodexDrainLocked(RoMapEntryDrainResult drain) {
            if (drain == null || drain.OrderedEvents == null) {
                return;
            }

            bool changed = false;
            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                RoMapEntryDrainEvent drainEvent = events[i];
                switch (drainEvent.Kind) {
                    case RoMapEntryDrainEventKind.RodexList:
                        rodexState.Apply(drainEvent.RodexList);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexListFailed:
                        rodexState.ApplyListFailed(drainEvent.RodexListFailureOpensWindow);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexRead:
                        rodexState.Apply(drainEvent.RodexRead);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexSendAck:
                        rodexState.Apply(drainEvent.RodexSendAck);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexZenyAck:
                        rodexState.Apply(drainEvent.RodexAttachmentAck, zeny: true);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexItemsAck:
                        rodexState.Apply(drainEvent.RodexAttachmentAck, zeny: false);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexDeleteAck:
                        rodexState.Apply(drainEvent.RodexDeleteAck);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexAddItemAck:
                        rodexState.Apply(drainEvent.RodexAddItemAck);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexRemoveItemAck:
                        rodexState.Apply(drainEvent.RodexRemoveItemAck);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexOpenWriteAck:
                        if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                            RoRuntimeDiagnostics.IncrementDebugLogLine();
                            Debug.Log("RODEX write open ack result=" + drainEvent.RodexOpenWriteAck.Result +
                                " success=" + drainEvent.RodexOpenWriteAck.Success +
                                " receiver='" + drainEvent.RodexOpenWriteAck.ReceiverName + "'");
                        }

                        rodexState.Apply(drainEvent.RodexOpenWriteAck);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.RodexCheckNameAck:
                        if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                            RoRuntimeDiagnostics.IncrementDebugLogLine();
                            Debug.Log("RODEX validate ack charId=" + drainEvent.RodexCheckNameAck.CharacterId +
                                " job=" + drainEvent.RodexCheckNameAck.Job +
                                " level=" + drainEvent.RodexCheckNameAck.Level +
                                " name='" + drainEvent.RodexCheckNameAck.CharacterName + "'");
                        }

                        rodexState.Apply(drainEvent.RodexCheckNameAck);
                        changed = true;
                        break;
                    case RoMapEntryDrainEventKind.NpcChat:
                        changed |= rodexState.ApplyOpenRejectedIfPending(drainEvent.NpcChat.Message);
                        break;
                    case RoMapEntryDrainEventKind.UnreadMail:
                        mailState.Apply(drainEvent.UnreadMail);
                        break;
                }
            }

            if (changed) {
                MarkGameplayChangedLocked(
                    RoGameplayWindowChange.Functional,
                    RoFunctionalWindowChange.Rodex);
            }
        }

        private void ApplyAttendanceDrainLocked(RoMapEntryDrainResult drain) {
            if (drain == null || drain.OrderedEvents == null) {
                return;
            }

            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                RoMapEntryDrainEvent drainEvent = events[i];
                switch (drainEvent.Kind) {
                    case RoMapEntryDrainEventKind.UiOpen:
                        attendanceState.Apply(drainEvent.UiOpen);
                        // the classic attendance reads the frame as the client does (ZC_UI_OPEN 0x0ae2 {u8 type, i32 data})
                        var open = new byte[7];
                        BitConverter.GetBytes((ushort) 0x0ae2).CopyTo(open, 0);
                        open[2] = drainEvent.UiOpen.UiType;
                        BitConverter.GetBytes(drainEvent.UiOpen.Data).CopyTo(open, 3);
                        pendingEntryQueue.Add(new RoZcEntryQueue(0x0ae2, open));

                        break;
                    case RoMapEntryDrainEventKind.AttendanceResponse:
                        // ZC_UI_ACTION 0x0af0 {i32 type, i32 data}
                        var action = new byte[10];
                        BitConverter.GetBytes((ushort) 0x0af0).CopyTo(action, 0);
                        BitConverter.GetBytes(drainEvent.AttendanceResponse.Unknown).CopyTo(action, 2);
                        BitConverter.GetBytes(drainEvent.AttendanceResponse.Data).CopyTo(action, 6);
                        pendingEntryQueue.Add(new RoZcEntryQueue(0x0af0, action));

                        if (attendanceState.Apply(drainEvent.AttendanceResponse)) {
                            mailState.Apply(new RoZcNotifyUnreadMail(1));
                        }
                        break;
                }
            }
        }

        private void ApplyPlayerViewEquipmentDrainLocked(RoMapEntryDrainResult drain) {
            if (drain?.OrderedEvents == null) {
                return;
            }

            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                if (events[i].Kind == RoMapEntryDrainEventKind.EntryQueue) {
                    ushort raw = events[i].EntryQueue.PacketId;
                    pendingEntryQueue.Add(events[i].EntryQueue);
                    continue;
                }

                if (events[i].Kind == RoMapEntryDrainEventKind.NavigationActive) {
                    latestNavigationActive = events[i].NavigationActive;
                    unchecked {
                        navigationActiveSequence++;
                    }

                    continue;
                }

                if (events[i].Kind != RoMapEntryDrainEventKind.PlayerViewEquipment) {
                    continue;
                }

                latestPlayerViewEquipment = events[i].PlayerViewEquipment;
                unchecked {
                    playerViewEquipmentSequence++;
                }
            }
        }

        private bool TryStartMapTransitionTaskLocked(RoLiveMapSession liveMapSession, RoMapEntryDrainResult drain) {
            if (!mapTransitionWorkflow.TryPrepare(
                mapSessionState,
                liveMapSession,
                drain,
                out RoPendingMapTransition transition,
                out string transitionStatus)) {
                return false;
            }

            uiState.BeginMapTransition(
                transitionStatus,
                transition.ShowsLoadingScreen,
                transition.IsSameMapMove);
            StartTask(() => CompleteMapTransition(transition));
            return true;
        }

        private void CompleteMapTransition(RoPendingMapTransition transition) {
            RoLoginEnterMapResult nextResult;
            try {
                nextResult = mapTransitionWorkflow.Complete(
                    transition,
                    loadRuntimeMapBeforeAck,
                    sameMapTransitionBeforeAck);
            } catch (Exception ex) {
                CompleteMapTransitionWithError(transition, ex.Message);
                return;
            }

            RoLiveMapSession oldSessionToDispose = null;
            int? basicInfoSequence = null;
            lock (sync) {
                if (disposed || !mapTransitionWorkflow.CanApply(mapSessionState, transition)) {
                    nextResult?.MapSession?.Dispose();
                    return;
                }

                liveDrainHistory.ClearCurrentMap();
                // the original's CGameMode starts a map with empty marker trees
                minimapMarkers.Clear();
                nextResult = ApplyCurrentMovementSpeedToMapResult(nextResult);
                if (transition.IsSameMapMove) {
                    gameplayState.ApplySameMapEntry(nextResult);
                } else {
                    gameplayState.ApplyMapTransitionEntry(nextResult, preserveKnownVitals: !transition.ReconnectToMapServer);
                    vendingState.ClearMapScopedState();
                    chatRoomState.ClearMapScopedState();
                    buyingStoreState.ClearMapScopedState();
                }

                pendingAutoRunSkills.Clear();
                nextResult = ApplyCurrentMovementSpeedToMapResult(nextResult);
                ApplySocialMapEntryLocked(nextResult);
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
                RoLiveMapSession replacedSession = mapSessionState.ReplaceMapResultAndStart(nextResult);
                oldSessionToDispose = mapTransitionWorkflow.ResolveSessionToDispose(transition, replacedSession, nextResult);
                uiState.SetStage(RoLoginShellStage.MapEntered, Text("ui.map.entered"));
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
            if (oldSessionToDispose != null && !ReferenceEquals(oldSessionToDispose, nextResult.MapSession)) {
                oldSessionToDispose.Dispose();
            }
        }

        private void CompleteMapTransitionWithError(RoPendingMapTransition transition, string message) {
            RoLiveMapSession sessionToDispose = null;
            int? basicInfoSequence = null;
            lock (sync) {
                if (disposed || !mapTransitionWorkflow.CanApply(mapSessionState, transition)) {
                    return;
                }

                sessionToDispose = mapTransitionWorkflow.StopFailedTransition(mapSessionState, transition);
                liveDrainHistory.ClearCurrentMap();
                gameplayState.ClearPendingMapMove();
                pendingAutoRunSkills.Clear();
                basicInfoSequence = MarkGameplayChangedLocked(RoGameplayWindowChange.All);
                uiState.SetError(
                    Text("ui.error.connection"),
                    FormatMapTransitionFailure(transition, message));
            }

            PublishBasicInfoSequenceChanged(basicInfoSequence);
            sessionToDispose?.Dispose();
        }

        private string FormatMapTransitionFailure(RoPendingMapTransition transition, string message) {
            string reason = string.IsNullOrWhiteSpace(message)
                ? "Map transition failed."
                : message.Trim();
            RoServerProfile profile = profileState.Profile;
            int packetVersion = profile.PacketVersion;

            if (transition.ReconnectToMapServer) {
                RoZcNpcServerMove serverMove = transition.ServerMove;
                return $"Map travel failed: redirect packet=ZC_NPCACK_SERVERMOVE packetver={packetVersion} target={serverMove.MapName} {serverMove.X},{serverMove.Y} endpoint={serverMove.Address}:{serverMove.Port}; reason={reason}";
            }

            RoZcNpcMapMove mapMove = transition.MapMove;
            return $"Map travel failed: same-server packet=ZC_NPCACK_MAPMOVE packetver={packetVersion} target={mapMove.MapName} {mapMove.X},{mapMove.Y}; reason={reason}";
        }

        private RoLoginEnterMapResult ApplyCurrentMovementSpeedToMapResult(RoLoginEnterMapResult result) {
            if (result == null) {
                return null;
            }

            int movementSpeed = gameplayState.StatusSnapshot().MovementSpeed;
            if (movementSpeed <= 0) {
                return result;
            }

            short selectedCharacterSpeed = movementSpeed > short.MaxValue
                ? short.MaxValue
                : (short) movementSpeed;
            return result.WithSelectedCharacterSpeed(selectedCharacterSpeed);
        }

        private void HandleLiveReceiveError(RoLiveMapSession liveMapSession, string message) {
            string context = RoNetworkRuntimeDiagnostics.FormatRecentContext();
            string resolvedMessage = string.IsNullOrWhiteSpace(message)
                ? "Map receive loop stopped."
                : message;
            if (!string.IsNullOrWhiteSpace(context)) {
                resolvedMessage += context;
            }

            Debug.LogWarning(resolvedMessage);
            lock (sync) {
                if (disposed ||
                    !mapSessionState.IsCurrentSession(liveMapSession)) {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(uiState.ErrorText)) {
                    return;
                }

                uiState.SetConnectionLost(
                    Text("ui.error.connection"),
                    string.IsNullOrWhiteSpace(message)
                        ? "Map receive loop stopped."
                        : message,
                    RoMonotonicTime.NowMilliseconds);
                MarkPhase11UiChangedLocked();
            }
        }

        private void HandleNetworkDiagnosticRecorded(object sender, RoNetworkDiagnosticEventArgs eventArgs) {
            if (eventArgs == null || string.IsNullOrWhiteSpace(eventArgs.Message)) {
                return;
            }

            Debug.LogWarning(eventArgs.Message);
        }

        private void PublishLiveDrainLocked(RoMapEntryDrainResult drain) {
            CaptureAutoRunSkillsLocked(drain);
            liveDrainHistory.Publish(drain);
        }

        private void CaptureAutoRunSkillsLocked(RoMapEntryDrainResult drain) {
            RoMapEntryDrainEvent[] events = drain != null ? drain.OrderedEvents : null;
            if (events == null || events.Length == 0) {
                return;
            }

            for (int i = 0; i < events.Length; i++) {
                RoMapEntryDrainEvent drainEvent = events[i];
                if (drainEvent.Kind == RoMapEntryDrainEventKind.AutoRunSkill &&
                    drainEvent.SkillInfo.SkillId != 0) {
                    pendingAutoRunSkills.Enqueue(drainEvent.SkillInfo);
                }
            }
        }

        private int? MarkGameplayChangedLocked() {
            return MarkGameplayChangedLocked(RoGameplayWindowChange.None);
        }

        private void MarkPhase11UiChangedLocked() {
            unchecked {
                phase11UiChangeSequence++;
            }
        }

        private int? MarkGameplayChangedLocked(RoGameplayWindowChange changes) {
            return MarkGameplayChangedLocked(changes, (changes & RoGameplayWindowChange.Functional) != 0
                ? RoFunctionalWindowChange.All
                : RoFunctionalWindowChange.None);
        }

        private int? MarkGameplayChangedLocked(RoGameplayWindowChange changes, RoFunctionalWindowChange functionalChanges) {
            int? basicInfoSequenceToPublish = null;
            unchecked {
                phase11UiChangeSequence++;
                gameplayChangeSequence++;
                if ((changes & RoGameplayWindowChange.BasicInfo) != 0) {
                    basicInfoChangeSequence++;
                    basicInfoSequenceToPublish = basicInfoChangeSequence;
                }

                if ((changes & RoGameplayWindowChange.Status) != 0) {
                    statusWindowChangeSequence++;
                }

                if ((changes & RoGameplayWindowChange.Skill) != 0) {
                    skillWindowChangeSequence++;
                }

                if ((changes & RoGameplayWindowChange.Inventory) != 0) {
                    inventoryWindowChangeSequence++;
                }

                if ((changes & RoGameplayWindowChange.Equipment) != 0) {
                    equipmentWindowChangeSequence++;
                }

                if ((changes & RoGameplayWindowChange.Hotkey) != 0) {
                    hotkeyWindowChangeSequence++;
                }

                if ((changes & RoGameplayWindowChange.Quest) != 0) {
                    questWindowChangeSequence++;
                }

                if ((changes & RoGameplayWindowChange.Functional) != 0) {
                    functionalWindowChangeSequence++;
                    RoFunctionalWindowChange effectiveFunctionalChanges = functionalChanges != RoFunctionalWindowChange.None
                        ? functionalChanges
                        : RoFunctionalWindowChange.All;
                    lastFunctionalWindowChanges = effectiveFunctionalChanges;
                    IncrementFunctionalWindowSequencesLocked(effectiveFunctionalChanges);
                }

                if ((changes & RoGameplayWindowChange.StatusIcon) != 0) {
                    statusIconChangeSequence++;
                }
            }

            return basicInfoSequenceToPublish;
        }

        private void PublishBasicInfoSequenceChanged(int? sequence) {
            if (sequence.HasValue) {
                BasicInfoSequenceChanged?.Invoke(sequence.Value);
            }
        }

        private int GetFunctionalWindowChangeSequenceLocked(RoFunctionalWindowChange windowChange) {
            int sequence = 0;
            if ((windowChange & RoFunctionalWindowChange.Npc) != 0) {
                sequence = Math.Max(sequence, functionalNpcChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Storage) != 0) {
                sequence = Math.Max(sequence, functionalStorageChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Party) != 0) {
                sequence = Math.Max(sequence, functionalPartyChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Friends) != 0) {
                sequence = Math.Max(sequence, functionalFriendsChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Guild) != 0) {
                sequence = Math.Max(sequence, functionalGuildChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Trade) != 0) {
                sequence = Math.Max(sequence, functionalTradeChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Cart) != 0) {
                sequence = Math.Max(sequence, functionalCartChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Bank) != 0) {
                sequence = Math.Max(sequence, functionalBankChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Rodex) != 0) {
                sequence = Math.Max(sequence, functionalRodexChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Attendance) != 0) {
                sequence = Math.Max(sequence, functionalAttendanceChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Pet) != 0) {
                sequence = Math.Max(sequence, functionalPetChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Homunculus) != 0) {
                sequence = Math.Max(sequence, functionalHomunculusChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Mercenary) != 0) {
                sequence = Math.Max(sequence, functionalMercenaryChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Vending) != 0) {
                sequence = Math.Max(sequence, functionalVendingChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.BuyingStore) != 0) {
                sequence = Math.Max(sequence, functionalBuyingStoreChangeSequence);
            }

            if ((windowChange & RoFunctionalWindowChange.Settings) != 0) {
                sequence = Math.Max(sequence, functionalSettingsChangeSequence);
            }

            return sequence != 0 ? sequence : functionalWindowChangeSequence;
        }

        private void IncrementFunctionalWindowSequencesLocked(RoFunctionalWindowChange changes) {
            if ((changes & RoFunctionalWindowChange.Npc) != 0) {
                functionalNpcChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Storage) != 0) {
                functionalStorageChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Party) != 0) {
                functionalPartyChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Friends) != 0) {
                functionalFriendsChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Guild) != 0) {
                functionalGuildChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Trade) != 0) {
                functionalTradeChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Cart) != 0) {
                functionalCartChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Bank) != 0) {
                functionalBankChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Rodex) != 0) {
                functionalRodexChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Attendance) != 0) {
                functionalAttendanceChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Pet) != 0) {
                functionalPetChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Homunculus) != 0) {
                functionalHomunculusChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Mercenary) != 0) {
                functionalMercenaryChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Vending) != 0) {
                functionalVendingChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.BuyingStore) != 0) {
                functionalBuyingStoreChangeSequence++;
            }

            if ((changes & RoFunctionalWindowChange.Settings) != 0) {
                functionalSettingsChangeSequence++;
            }
        }

        private static bool HasGameplayEvents(RoMapEntryDrainResult drain) {
            return drain?.OrderedEvents != null && drain.OrderedEvents.Length > 0;
        }

        private static bool IsPresentationOnlyDrain(RoMapEntryDrainResult drain) {
            RoMapEntryDrainEvent[] events = drain?.OrderedEvents;
            if (events == null || events.Length == 0) {
                return true;
            }

            for (int i = 0; i < events.Length; i++) {
                if (!IsPresentationOnlyEvent(events[i].Kind)) {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPresentationOnlyEvent(RoMapEntryDrainEventKind kind) {
            switch (kind) {
                case RoMapEntryDrainEventKind.TimeNotification:
                case RoMapEntryDrainEventKind.ActorEntry:
                case RoMapEntryDrainEventKind.ActorMove:
                case RoMapEntryDrainEventKind.PlayerMove:
                case RoMapEntryDrainEventKind.StopMove:
                case RoMapEntryDrainEventKind.ActorVanish:
                case RoMapEntryDrainEventKind.ActorDirectionChange:
                case RoMapEntryDrainEventKind.GroundItemEntry:
                case RoMapEntryDrainEventKind.GroundItemDisappear:
                case RoMapEntryDrainEventKind.ActorAction:
                case RoMapEntryDrainEventKind.Emotion:
                case RoMapEntryDrainEventKind.SkillUseAck:
                case RoMapEntryDrainEventKind.SkillCast:
                case RoMapEntryDrainEventKind.SkillDamage:
                case RoMapEntryDrainEventKind.SkillResult:
                case RoMapEntryDrainEventKind.GroundSkill:
                case RoMapEntryDrainEventKind.SkillPostDelay:
                case RoMapEntryDrainEventKind.SkillPosition:
                case RoMapEntryDrainEventKind.SkillEntry:
                case RoMapEntryDrainEventKind.SkillDisappear:
                case RoMapEntryDrainEventKind.NotifyEffect:
                case RoMapEntryDrainEventKind.AttackFailureForDistance:
                case RoMapEntryDrainEventKind.ActionFailure:
                case RoMapEntryDrainEventKind.ActorIdentity:
                case RoMapEntryDrainEventKind.ActorHpInfo:
                case RoMapEntryDrainEventKind.NpcShowScript:
                case RoMapEntryDrainEventKind.ActorStateChange:
                    return true;
                case RoMapEntryDrainEventKind.ActorSpriteChange:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ContainsSuccessfulBankOpenAck(RoMapEntryDrainResult drain) {
            RoMapEntryDrainEvent[] events = drain?.OrderedEvents;
            if (events == null) {
                return false;
            }

            for (int i = 0; i < events.Length; i++) {
                if (events[i].Kind == RoMapEntryDrainEventKind.BankOpenAck && events[i].BankSimpleAck.Success) {
                    return true;
                }
            }

            return false;
        }

        private static RoGameplayWindowChange ClassifyGameplayWindowChanges(RoMapEntryDrainResult drain) {
            RoMapEntryDrainEvent[] events = drain?.OrderedEvents;
            if (events == null || events.Length == 0) {
                return RoGameplayWindowChange.None;
            }

            RoGameplayWindowChange changes = RoGameplayWindowChange.None;
            for (int i = 0; i < events.Length; i++) {
                changes |= ClassifyGameplayWindowChange(events[i]);
            }

            return changes;
        }

        private static RoFunctionalWindowChange ClassifyFunctionalWindowChanges(RoMapEntryDrainResult drain) {
            RoMapEntryDrainEvent[] events = drain?.OrderedEvents;
            if (events == null || events.Length == 0) {
                return RoFunctionalWindowChange.None;
            }

            RoFunctionalWindowChange changes = RoFunctionalWindowChange.None;
            for (int i = 0; i < events.Length; i++) {
                changes |= ClassifyFunctionalWindowChange(events[i]);
            }

            return changes;
        }

        private static RoFunctionalWindowChange ClassifyFunctionalWindowChange(RoMapEntryDrainEvent drainEvent) {
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.ParameterChange:
                    return ClassifyFunctionalStatusChange(drainEvent.ParameterChange.Property);
                case RoMapEntryDrainEventKind.StatusPropertyChange:
                    return ClassifyFunctionalStatusChange(drainEvent.StatusPropertyChange.Property);
                case RoMapEntryDrainEventKind.StatusBaseBonus:
                    return ClassifyFunctionalStatusChange(drainEvent.StatusBaseBonus.Property);
                case RoMapEntryDrainEventKind.StatusSnapshot:
                    return RoFunctionalWindowChange.Party;
                case RoMapEntryDrainEventKind.InventoryStart:
                    return ClassifyFunctionalInventoryTypeChange(drainEvent.InventoryStart.InventoryType);
                case RoMapEntryDrainEventKind.InventoryList:
                    return ClassifyFunctionalInventoryTypeChange(drainEvent.InventoryList.InventoryType);
                case RoMapEntryDrainEventKind.InventoryEnd:
                    return ClassifyFunctionalInventoryTypeChange(drainEvent.InventoryEnd.InventoryType);
                case RoMapEntryDrainEventKind.NpcDialogText:
                case RoMapEntryDrainEventKind.NpcDialogNext:
                case RoMapEntryDrainEventKind.NpcDialogClose:
                case RoMapEntryDrainEventKind.NpcDialogMenu:
                case RoMapEntryDrainEventKind.NpcDialogNumberInput:
                case RoMapEntryDrainEventKind.NpcDialogTextInput:
                case RoMapEntryDrainEventKind.NpcDialogClear:
                case RoMapEntryDrainEventKind.TeleportWarpList:
                case RoMapEntryDrainEventKind.NpcShopDealType:
                case RoMapEntryDrainEventKind.NpcShopBuyList:
                case RoMapEntryDrainEventKind.NpcShopSellList:
                case RoMapEntryDrainEventKind.NpcShopBuyResult:
                case RoMapEntryDrainEventKind.NpcShopSellResult:
                case RoMapEntryDrainEventKind.NpcMarketList:
                case RoMapEntryDrainEventKind.NpcMarketResult:
                case RoMapEntryDrainEventKind.NpcBarterList:
                case RoMapEntryDrainEventKind.NpcExpandedBarterList:
                case RoMapEntryDrainEventKind.NpcCutin:
                    return RoFunctionalWindowChange.Npc;
                case RoMapEntryDrainEventKind.StorageAmount:
                case RoMapEntryDrainEventKind.StorageItemAdd:
                case RoMapEntryDrainEventKind.StorageItemDelete:
                case RoMapEntryDrainEventKind.StorageClose:
                    return RoFunctionalWindowChange.Storage;
                case RoMapEntryDrainEventKind.PartyOption:
                case RoMapEntryDrainEventKind.PartyCreateAck:
                case RoMapEntryDrainEventKind.PartyConfig:
                case RoMapEntryDrainEventKind.PartyLeaderChanged:
                case RoMapEntryDrainEventKind.PartyInfo:
                case RoMapEntryDrainEventKind.PartyMemberInfo:
                case RoMapEntryDrainEventKind.PartyMemberLeave:
                case RoMapEntryDrainEventKind.PartyPosition:
                case RoMapEntryDrainEventKind.PartyHp:
                case RoMapEntryDrainEventKind.PartyAlive:
                case RoMapEntryDrainEventKind.PartyJobAndLevel:
                case RoMapEntryDrainEventKind.PartyInvite:
                case RoMapEntryDrainEventKind.PartyInviteAck:
                    return RoFunctionalWindowChange.Party;
                case RoMapEntryDrainEventKind.FriendsList:
                case RoMapEntryDrainEventKind.FriendState:
                case RoMapEntryDrainEventKind.FriendRequest:
                case RoMapEntryDrainEventKind.FriendAddResult:
                case RoMapEntryDrainEventKind.FriendDelete:
                case RoMapEntryDrainEventKind.CharacterNameByGid:
                    return RoFunctionalWindowChange.Friends;
                case RoMapEntryDrainEventKind.ChatRoomCreateAck:
                case RoMapEntryDrainEventKind.ChatRoomRefuse:
                case RoMapEntryDrainEventKind.ChatRoomInfo:
                case RoMapEntryDrainEventKind.ChatRoomDestroy:
                case RoMapEntryDrainEventKind.ChatRoomEnter:
                case RoMapEntryDrainEventKind.ChatRoomMemberJoin:
                case RoMapEntryDrainEventKind.ChatRoomMemberExit:
                case RoMapEntryDrainEventKind.ChatRoomRole:
                    return RoFunctionalWindowChange.ChatRoom;
                case RoMapEntryDrainEventKind.BuyingStoreSetup:
                case RoMapEntryDrainEventKind.BuyingStoreOpenFailed:
                case RoMapEntryDrainEventKind.BuyingStoreOwnList:
                case RoMapEntryDrainEventKind.BuyingStoreItemList:
                case RoMapEntryDrainEventKind.BuyingStoreEntry:
                case RoMapEntryDrainEventKind.BuyingStoreDisappear:
                case RoMapEntryDrainEventKind.BuyingStoreBuyerFailed:
                case RoMapEntryDrainEventKind.BuyingStoreSold:
                case RoMapEntryDrainEventKind.BuyingStoreSellerFailed:
                case RoMapEntryDrainEventKind.BuyingStoreUpdate:
                    return RoFunctionalWindowChange.BuyingStore;
                case RoMapEntryDrainEventKind.RepairItemList:
                case RoMapEntryDrainEventKind.RepairItemResult:
                case RoMapEntryDrainEventKind.WeaponRefineList:
                case RoMapEntryDrainEventKind.WeaponRefineResult:
                case RoMapEntryDrainEventKind.AutoSpellList:
                case RoMapEntryDrainEventKind.MergeItemOpen:
                case RoMapEntryDrainEventKind.MergeItemResult:
                case RoMapEntryDrainEventKind.MakableItemList:
                case RoMapEntryDrainEventKind.MakingItemResult:
                    return RoFunctionalWindowChange.Npc;
                case RoMapEntryDrainEventKind.PetEggList:
                case RoMapEntryDrainEventKind.PetCaptureStart:
                case RoMapEntryDrainEventKind.PetCaptureResult:
                case RoMapEntryDrainEventKind.PetEvolutionResult:
                    return RoFunctionalWindowChange.Pet;
                case RoMapEntryDrainEventKind.MercenaryParameterChange:
                    return RoFunctionalWindowChange.Mercenary;
                case RoMapEntryDrainEventKind.HomunculusSkillUpdate:
                    return RoFunctionalWindowChange.Homunculus;
                case RoMapEntryDrainEventKind.GuildBelongInfo:
                case RoMapEntryDrainEventKind.GuildNotice:
                case RoMapEntryDrainEventKind.GuildMemberInfo:
                case RoMapEntryDrainEventKind.GuildInvite:
                case RoMapEntryDrainEventKind.GuildInviteResult:
                case RoMapEntryDrainEventKind.GuildLeaveResult:
                case RoMapEntryDrainEventKind.GuildChat:
                case RoMapEntryDrainEventKind.GuildManager:
                    return RoFunctionalWindowChange.Guild;
                case RoMapEntryDrainEventKind.TradeRequest:
                case RoMapEntryDrainEventKind.TradeAck:
                case RoMapEntryDrainEventKind.TradeItemAdd:
                case RoMapEntryDrainEventKind.TradeAddItemAck:
                case RoMapEntryDrainEventKind.TradeConclude:
                case RoMapEntryDrainEventKind.TradeCancel:
                case RoMapEntryDrainEventKind.TradeSubmit:
                    return RoFunctionalWindowChange.Trade;
                case RoMapEntryDrainEventKind.CartAmount:
                case RoMapEntryDrainEventKind.CartItemAdd:
                case RoMapEntryDrainEventKind.CartItemDelete:
                case RoMapEntryDrainEventKind.CartOff:
                    return RoFunctionalWindowChange.Cart;
                case RoMapEntryDrainEventKind.VendingSetupAllowed:
                case RoMapEntryDrainEventKind.VendingBoardAdded:
                case RoMapEntryDrainEventKind.VendingBoardRemoved:
                case RoMapEntryDrainEventKind.PlayerShopListReceived:
                case RoMapEntryDrainEventKind.VendingOpenAccepted:
                case RoMapEntryDrainEventKind.VendingSaleReported:
                case RoMapEntryDrainEventKind.VendingOpenAck:
                case RoMapEntryDrainEventKind.PlayerShopPurchaseResult:
                case RoMapEntryDrainEventKind.VendingClosed:
                    return RoFunctionalWindowChange.Vending;
                case RoMapEntryDrainEventKind.BankOpenAck:
                case RoMapEntryDrainEventKind.BankCheck:
                case RoMapEntryDrainEventKind.BankDepositAck:
                case RoMapEntryDrainEventKind.BankWithdrawAck:
                case RoMapEntryDrainEventKind.BankCloseAck:
                    return RoFunctionalWindowChange.Bank;
                case RoMapEntryDrainEventKind.RodexList:
                case RoMapEntryDrainEventKind.RodexListFailed:
                case RoMapEntryDrainEventKind.RodexRead:
                case RoMapEntryDrainEventKind.RodexSendAck:
                case RoMapEntryDrainEventKind.RodexZenyAck:
                case RoMapEntryDrainEventKind.RodexItemsAck:
                case RoMapEntryDrainEventKind.RodexDeleteAck:
                case RoMapEntryDrainEventKind.RodexAddItemAck:
                case RoMapEntryDrainEventKind.RodexRemoveItemAck:
                case RoMapEntryDrainEventKind.RodexOpenWriteAck:
                case RoMapEntryDrainEventKind.RodexCheckNameAck:
                case RoMapEntryDrainEventKind.UnreadMail:
                    return RoFunctionalWindowChange.Rodex;
                case RoMapEntryDrainEventKind.NpcChat:
                    return ContainsRodexMessage(drainEvent.NpcChat.Message)
                        ? RoFunctionalWindowChange.Rodex
                        : RoFunctionalWindowChange.None;
                case RoMapEntryDrainEventKind.UiOpen:
                    return drainEvent.UiOpen.IsAttendance
                        ? RoFunctionalWindowChange.Attendance
                        : RoFunctionalWindowChange.None;
                case RoMapEntryDrainEventKind.AttendanceResponse:
                    return RoFunctionalWindowChange.Attendance;
                case RoMapEntryDrainEventKind.PetProperty:
                case RoMapEntryDrainEventKind.PetFeedResult:
                case RoMapEntryDrainEventKind.PetChangeState:
                case RoMapEntryDrainEventKind.PetAction:
                    return RoFunctionalWindowChange.Pet;
                case RoMapEntryDrainEventKind.HomunculusProperty:
                case RoMapEntryDrainEventKind.HomunculusChangeState:
                case RoMapEntryDrainEventKind.HomunculusParameterChange:
                case RoMapEntryDrainEventKind.HomunculusSkillInfoList:
                    return RoFunctionalWindowChange.Homunculus;
                case RoMapEntryDrainEventKind.MercenaryInfo:
                case RoMapEntryDrainEventKind.MercenarySkillInfoList:
                    return RoFunctionalWindowChange.Mercenary;
                default:
                    return RoFunctionalWindowChange.None;
            }
        }

        private static RoFunctionalWindowChange ClassifyFunctionalStatusChange(RoPlayerStatusProperty property) {
            switch (property) {
                case RoPlayerStatusProperty.Hp:
                case RoPlayerStatusProperty.MaxHp:
                case RoPlayerStatusProperty.BaseLevel:
                case RoPlayerStatusProperty.Job:
                    return RoFunctionalWindowChange.Party;
                default:
                    return RoFunctionalWindowChange.None;
            }
        }

        private static bool ContainsRodexMessage(string message) {
            return !string.IsNullOrWhiteSpace(message) &&
                message.IndexOf("rodex", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static RoGameplayWindowChange ClassifyGameplayWindowChange(RoMapEntryDrainEvent drainEvent) {
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.ParameterChange:
                    return ClassifyStatusParameterChange(drainEvent.ParameterChange.Property);
                case RoMapEntryDrainEventKind.StatusPropertyChange:
                    return ClassifyStatusParameterChange(drainEvent.StatusPropertyChange.Property);
                case RoMapEntryDrainEventKind.StatusChangeAck:
                    return drainEvent.StatusChangeAck.Result == 0
                        ? RoGameplayWindowChange.None
                        : ClassifyStatusParameterChange((RoPlayerStatusProperty) drainEvent.StatusChangeAck.StatusId);
                case RoMapEntryDrainEventKind.StatusBaseBonus:
                    return ClassifyStatusParameterChange(drainEvent.StatusBaseBonus.Property);
                case RoMapEntryDrainEventKind.StatusSnapshot:
                    return RoGameplayWindowChange.BasicInfo | RoGameplayWindowChange.Status | RoGameplayWindowChange.StatusIcon;
                case RoMapEntryDrainEventKind.StatusChange:
                    return RoGameplayWindowChange.StatusIcon;
                case RoMapEntryDrainEventKind.ActorSpriteChange:
                    return drainEvent.ActorSpriteChange.Type == RoMapActorSpriteChangeType.Job
                        ? RoGameplayWindowChange.BasicInfo | RoGameplayWindowChange.Skill | RoGameplayWindowChange.Hotkey
                        : RoGameplayWindowChange.None;
                case RoMapEntryDrainEventKind.WeightPercentage:
                    return RoGameplayWindowChange.BasicInfo | RoGameplayWindowChange.Inventory;
                case RoMapEntryDrainEventKind.AttackRange:
                    return RoGameplayWindowChange.BasicInfo | RoGameplayWindowChange.Status;
                case RoMapEntryDrainEventKind.SkillInfoList:
                case RoMapEntryDrainEventKind.SkillInfoUpdate:
                case RoMapEntryDrainEventKind.SkillInfoAdd:
                case RoMapEntryDrainEventKind.SkillInfoUpdate2:
                    return RoGameplayWindowChange.Skill | RoGameplayWindowChange.Hotkey;
                case RoMapEntryDrainEventKind.ShortcutKeyList:
                    return RoGameplayWindowChange.Hotkey;
                case RoMapEntryDrainEventKind.InventoryStart:
                    return ClassifyInventoryTypeChange(drainEvent.InventoryStart.InventoryType);
                case RoMapEntryDrainEventKind.InventoryList:
                    return ClassifyInventoryTypeChange(drainEvent.InventoryList.InventoryType);
                case RoMapEntryDrainEventKind.InventoryEnd:
                    return ClassifyInventoryTypeChange(drainEvent.InventoryEnd.InventoryType);
                case RoMapEntryDrainEventKind.EquipSwitchList:
                case RoMapEntryDrainEventKind.WearEquipmentAck:
                case RoMapEntryDrainEventKind.TakeoffEquipmentAck:
                case RoMapEntryDrainEventKind.EquipArrow:
                case RoMapEntryDrainEventKind.ItemThrowAck:
                case RoMapEntryDrainEventKind.ItemPickupAck:
                case RoMapEntryDrainEventKind.InventoryItemDelete:
                case RoMapEntryDrainEventKind.UseItemAck:
                case RoMapEntryDrainEventKind.ItemCompositionList:
                case RoMapEntryDrainEventKind.ItemCompositionAck:
                case RoMapEntryDrainEventKind.ItemIdentifyList:
                case RoMapEntryDrainEventKind.ItemIdentifyAck:
                case RoMapEntryDrainEventKind.MakingArrowList:
                    return RoGameplayWindowChange.Inventory | RoGameplayWindowChange.Equipment | RoGameplayWindowChange.Hotkey;
                case RoMapEntryDrainEventKind.QuestList:
                case RoMapEntryDrainEventKind.QuestAdd:
                case RoMapEntryDrainEventKind.QuestDelete:
                case RoMapEntryDrainEventKind.QuestActive:
                case RoMapEntryDrainEventKind.QuestObjectiveUpdate:
                    return RoGameplayWindowChange.Quest;
                case RoMapEntryDrainEventKind.NpcDialogText:
                case RoMapEntryDrainEventKind.NpcDialogNext:
                case RoMapEntryDrainEventKind.NpcDialogClose:
                case RoMapEntryDrainEventKind.NpcDialogMenu:
                case RoMapEntryDrainEventKind.NpcDialogNumberInput:
                case RoMapEntryDrainEventKind.NpcDialogTextInput:
                case RoMapEntryDrainEventKind.NpcDialogClear:
                case RoMapEntryDrainEventKind.TeleportWarpList:
                case RoMapEntryDrainEventKind.NpcShopDealType:
                case RoMapEntryDrainEventKind.NpcShopBuyList:
                case RoMapEntryDrainEventKind.NpcShopSellList:
                case RoMapEntryDrainEventKind.NpcShopBuyResult:
                case RoMapEntryDrainEventKind.NpcShopSellResult:
                case RoMapEntryDrainEventKind.NpcMarketList:
                case RoMapEntryDrainEventKind.NpcMarketResult:
                case RoMapEntryDrainEventKind.NpcBarterList:
                case RoMapEntryDrainEventKind.NpcExpandedBarterList:
                case RoMapEntryDrainEventKind.StorageAmount:
                case RoMapEntryDrainEventKind.StorageItemAdd:
                case RoMapEntryDrainEventKind.StorageItemDelete:
                case RoMapEntryDrainEventKind.StorageClose:
                case RoMapEntryDrainEventKind.NpcCutin:
                case RoMapEntryDrainEventKind.PartyOption:
                case RoMapEntryDrainEventKind.PartyCreateAck:
                case RoMapEntryDrainEventKind.PartyConfig:
                case RoMapEntryDrainEventKind.PartyLeaderChanged:
                case RoMapEntryDrainEventKind.PartyInfo:
                case RoMapEntryDrainEventKind.PartyMemberInfo:
                case RoMapEntryDrainEventKind.PartyMemberLeave:
                case RoMapEntryDrainEventKind.PartyPosition:
                case RoMapEntryDrainEventKind.PartyHp:
                case RoMapEntryDrainEventKind.PartyAlive:
                case RoMapEntryDrainEventKind.PartyJobAndLevel:
                case RoMapEntryDrainEventKind.PartyInvite:
                case RoMapEntryDrainEventKind.PartyInviteAck:
                case RoMapEntryDrainEventKind.FriendsList:
                case RoMapEntryDrainEventKind.FriendState:
                case RoMapEntryDrainEventKind.FriendRequest:
                case RoMapEntryDrainEventKind.FriendAddResult:
                case RoMapEntryDrainEventKind.FriendDelete:
                case RoMapEntryDrainEventKind.CharacterNameByGid:
                case RoMapEntryDrainEventKind.ChatRoomCreateAck:
                case RoMapEntryDrainEventKind.ChatRoomRefuse:
                case RoMapEntryDrainEventKind.ChatRoomInfo:
                case RoMapEntryDrainEventKind.ChatRoomDestroy:
                case RoMapEntryDrainEventKind.ChatRoomEnter:
                case RoMapEntryDrainEventKind.ChatRoomMemberJoin:
                case RoMapEntryDrainEventKind.ChatRoomMemberExit:
                case RoMapEntryDrainEventKind.ChatRoomRole:
                case RoMapEntryDrainEventKind.BuyingStoreSetup:
                case RoMapEntryDrainEventKind.BuyingStoreOpenFailed:
                case RoMapEntryDrainEventKind.BuyingStoreOwnList:
                case RoMapEntryDrainEventKind.BuyingStoreItemList:
                case RoMapEntryDrainEventKind.BuyingStoreEntry:
                case RoMapEntryDrainEventKind.BuyingStoreDisappear:
                case RoMapEntryDrainEventKind.BuyingStoreBuyerFailed:
                case RoMapEntryDrainEventKind.BuyingStoreSold:
                case RoMapEntryDrainEventKind.BuyingStoreSellerFailed:
                case RoMapEntryDrainEventKind.BuyingStoreUpdate:
                case RoMapEntryDrainEventKind.RepairItemList:
                case RoMapEntryDrainEventKind.RepairItemResult:
                case RoMapEntryDrainEventKind.WeaponRefineList:
                case RoMapEntryDrainEventKind.WeaponRefineResult:
                case RoMapEntryDrainEventKind.AutoSpellList:
                case RoMapEntryDrainEventKind.MergeItemOpen:
                case RoMapEntryDrainEventKind.MergeItemResult:
                case RoMapEntryDrainEventKind.MakableItemList:
                case RoMapEntryDrainEventKind.MakingItemResult:
                case RoMapEntryDrainEventKind.PetEggList:
                case RoMapEntryDrainEventKind.PetCaptureStart:
                case RoMapEntryDrainEventKind.PetCaptureResult:
                case RoMapEntryDrainEventKind.PetEvolutionResult:
                case RoMapEntryDrainEventKind.MercenaryParameterChange:
                case RoMapEntryDrainEventKind.HomunculusSkillUpdate:
                case RoMapEntryDrainEventKind.CartAmount:
                case RoMapEntryDrainEventKind.CartItemAdd:
                case RoMapEntryDrainEventKind.CartItemDelete:
                case RoMapEntryDrainEventKind.CartOff:
                case RoMapEntryDrainEventKind.VendingSetupAllowed:
                case RoMapEntryDrainEventKind.VendingBoardAdded:
                case RoMapEntryDrainEventKind.VendingBoardRemoved:
                case RoMapEntryDrainEventKind.PlayerShopListReceived:
                case RoMapEntryDrainEventKind.VendingOpenAccepted:
                case RoMapEntryDrainEventKind.VendingSaleReported:
                case RoMapEntryDrainEventKind.VendingOpenAck:
                case RoMapEntryDrainEventKind.PlayerShopPurchaseResult:
                case RoMapEntryDrainEventKind.VendingClosed:
                case RoMapEntryDrainEventKind.BankOpenAck:
                case RoMapEntryDrainEventKind.BankCheck:
                case RoMapEntryDrainEventKind.BankCloseAck:
                case RoMapEntryDrainEventKind.RodexList:
                case RoMapEntryDrainEventKind.RodexListFailed:
                case RoMapEntryDrainEventKind.RodexRead:
                case RoMapEntryDrainEventKind.RodexSendAck:
                case RoMapEntryDrainEventKind.RodexZenyAck:
                case RoMapEntryDrainEventKind.RodexItemsAck:
                case RoMapEntryDrainEventKind.RodexDeleteAck:
                case RoMapEntryDrainEventKind.RodexAddItemAck:
                case RoMapEntryDrainEventKind.RodexRemoveItemAck:
                case RoMapEntryDrainEventKind.RodexOpenWriteAck:
                case RoMapEntryDrainEventKind.RodexCheckNameAck:
                case RoMapEntryDrainEventKind.NpcChat:
                case RoMapEntryDrainEventKind.TradeRequest:
                case RoMapEntryDrainEventKind.TradeAck:
                case RoMapEntryDrainEventKind.TradeItemAdd:
                case RoMapEntryDrainEventKind.TradeAddItemAck:
                case RoMapEntryDrainEventKind.TradeConclude:
                case RoMapEntryDrainEventKind.TradeCancel:
                case RoMapEntryDrainEventKind.TradeSubmit:
                case RoMapEntryDrainEventKind.GuildBelongInfo:
                case RoMapEntryDrainEventKind.GuildNotice:
                case RoMapEntryDrainEventKind.GuildMemberInfo:
                case RoMapEntryDrainEventKind.GuildInvite:
                case RoMapEntryDrainEventKind.GuildInviteResult:
                case RoMapEntryDrainEventKind.GuildLeaveResult:
                case RoMapEntryDrainEventKind.GuildChat:
                case RoMapEntryDrainEventKind.GuildManager:
                case RoMapEntryDrainEventKind.PetProperty:
                case RoMapEntryDrainEventKind.PetFeedResult:
                case RoMapEntryDrainEventKind.PetChangeState:
                case RoMapEntryDrainEventKind.PetAction:
                case RoMapEntryDrainEventKind.HomunculusProperty:
                case RoMapEntryDrainEventKind.HomunculusChangeState:
                case RoMapEntryDrainEventKind.HomunculusParameterChange:
                case RoMapEntryDrainEventKind.HomunculusSkillInfoList:
                case RoMapEntryDrainEventKind.MercenaryInfo:
                case RoMapEntryDrainEventKind.MercenarySkillInfoList:
                    return RoGameplayWindowChange.Functional;
                case RoMapEntryDrainEventKind.UiOpen:
                    return drainEvent.UiOpen.IsAttendance
                        ? RoGameplayWindowChange.BasicInfo | RoGameplayWindowChange.Functional
                        : RoGameplayWindowChange.None;
                case RoMapEntryDrainEventKind.UnreadMail:
                    return RoGameplayWindowChange.BasicInfo | RoGameplayWindowChange.Functional;
                case RoMapEntryDrainEventKind.AttendanceResponse:
                    return RoGameplayWindowChange.BasicInfo | RoGameplayWindowChange.Functional;
                case RoMapEntryDrainEventKind.BankDepositAck:
                case RoMapEntryDrainEventKind.BankWithdrawAck:
                    return RoGameplayWindowChange.BasicInfo | RoGameplayWindowChange.Inventory | RoGameplayWindowChange.Functional;
                default:
                    return RoGameplayWindowChange.None;
            }
        }

        private static RoGameplayWindowChange ClassifyInventoryTypeChange(RoInventoryType inventoryType) {
            return inventoryType == RoInventoryType.Inventory
                ? RoGameplayWindowChange.Inventory | RoGameplayWindowChange.Equipment | RoGameplayWindowChange.Hotkey
                : RoGameplayWindowChange.Functional;
        }

        private static RoFunctionalWindowChange ClassifyFunctionalInventoryTypeChange(RoInventoryType inventoryType) {
            if (inventoryType == RoInventoryType.Inventory) {
                return RoFunctionalWindowChange.None;
            }

            return inventoryType == RoInventoryType.Cart
                ? RoFunctionalWindowChange.Cart
                : RoFunctionalWindowChange.Storage;
        }

        private static RoGameplayWindowChange ClassifyStatusParameterChange(RoPlayerStatusProperty property) {
            RoGameplayWindowChange changes = RoGameplayWindowChange.BasicInfo | RoGameplayWindowChange.Status;
            switch (property) {
                case RoPlayerStatusProperty.SkillPoints:
                    changes |= RoGameplayWindowChange.Skill;
                    break;
                case RoPlayerStatusProperty.Job:
                    changes |= RoGameplayWindowChange.Skill | RoGameplayWindowChange.Hotkey;
                    break;
                case RoPlayerStatusProperty.Zeny:
                case RoPlayerStatusProperty.Weight:
                case RoPlayerStatusProperty.MaxWeight:
                    changes |= RoGameplayWindowChange.Inventory;
                    break;
            }

            return changes;
        }

        private void UpdateLiveDrainStatusLocked(RoMapEntryDrainResult drain) {
            if (!RoLiveDrainStatusResolver.ContainsStatusUpdateEvent(drain)) {
                return;
            }

            if (RoLiveDrainStatusResolver.TryResolve(drain, gameplayState.InventorySnapshot(), out RoShellStatusUpdate update)) {
                uiState.SetStatus(update.StatusText, update.ErrorText);
            }
        }

        private void ReportDrainParseFailuresLocked(RoMapEntryDrainResult drain) {
            uiState.SetStatus($"dropped {drain.ParseFailureCount} unparsable packet(s); see log");
            if (!RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                return;
            }

            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log($"RO map drain dropped {drain.ParseFailureCount} unparsable packet(s).");
            foreach (string message in drain.ParseFailureMessages) {
                RoRuntimeDiagnostics.IncrementDebugLogLine();
                Debug.Log("RO map drain parse failure: " + message);
            }
        }

        private string Text(string key) {
            return localization.GetStringOrKey(key);
        }

        private static string FormatStatusStatName(RoStatusStat stat) {
            switch (stat) {
                case RoStatusStat.Str:
                    return "STR";
                case RoStatusStat.Agi:
                    return "AGI";
                case RoStatusStat.Vit:
                    return "VIT";
                case RoStatusStat.Int:
                    return "INT";
                case RoStatusStat.Dex:
                    return "DEX";
                case RoStatusStat.Luk:
                    return "LUK";
                default:
                    return "status";
            }
        }

        private bool IsBusyLocked() {
            return taskRunner.IsBusy;
        }

        private bool TryResolveBasicInfoCharacterLocked(out RoLoginShellCharacter character) {
            RoCharacterInfo selectedCharacter = mapSessionState.MapResult?.CharacterResult?.SelectedCharacter;
            if (selectedCharacter != null) {
                character = new RoLoginShellCharacter(selectedCharacter);
                return true;
            }

            return characterState.TryResolveSelectedCharacter(out character);
        }

        private void ApplyActionLockLocked(
            RoMapEntryDrainResult drain,
            RoLocalActionLockRequest request,
            DateTime requestStartedUtc) {
            actionLockState.Apply(
                drain,
                LocalActorIdLocked(),
                request,
                requestStartedUtc);
        }

        private void ClearActionLockLocked() {
            actionLockState.Clear();
        }

        private uint LocalActorIdLocked() {
            return mapSessionState.LocalActorId;
        }

        private int LocalActorAidLocked() {
            return unchecked((int) mapSessionState.LocalActorId);
        }

        private void ThrowIfBusyLocked() {
            taskRunner.ThrowIfBusy();
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoLoginShellSession));
            }
        }
    }
}
