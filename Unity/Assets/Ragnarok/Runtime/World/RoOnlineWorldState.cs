using System;
using System.Collections.Generic;
using Ragnarok.Combat;
using Ragnarok.Diagnostics;
using Ragnarok.Network;

namespace Ragnarok.World {
    public enum RoOnlineActorMotion {
        Idle = 0,
        Moving = 1,
        Sitting = 2,
        Pickup = 3,
        Attack = 4,
        Hit = 5,
        Dead = 6,
        Casting = 7,
        Attack2 = 8,
        Attack3 = 9,
        Skill = 10,
        Standby = 11,
        Throwing = 12
    }

    public readonly struct RoOnlineWorldReconciliationStats {
        public RoOnlineWorldReconciliationStats(
            int completedMoveSnaps,
            int ignoredStaleMoves,
            int ignoredDuplicateMoves
        ) {
            CompletedMoveSnaps = completedMoveSnaps;
            IgnoredStaleMoves = ignoredStaleMoves;
            IgnoredDuplicateMoves = ignoredDuplicateMoves;
        }

        public int CompletedMoveSnaps { get; }
        public int IgnoredStaleMoves { get; }
        public int IgnoredDuplicateMoves { get; }
    }

    public readonly struct RoMovementDiagnosticsSnapshot {
        internal RoMovementDiagnosticsSnapshot(
            int acceptedMoves,
            int ignoredStaleMoves,
            int ignoredDuplicateMoves,
            int ignoredInvalidMoves,
            int ignoredSuspiciousZeroPathMoves = 0
        ) {
            AcceptedMoves = acceptedMoves;
            IgnoredStaleMoves = ignoredStaleMoves;
            IgnoredDuplicateMoves = ignoredDuplicateMoves;
            IgnoredInvalidMoves = ignoredInvalidMoves;
            IgnoredSuspiciousZeroPathMoves = ignoredSuspiciousZeroPathMoves;
        }

        public int AcceptedMoves { get; }
        public int IgnoredStaleMoves { get; }
        public int IgnoredDuplicateMoves { get; }
        public int IgnoredInvalidMoves { get; }
        public int IgnoredSuspiciousZeroPathMoves { get; }
    }

    public readonly struct RoOnlineWorldBudget {
        public const int DefaultVisibleActorLimit = 300;
        public const int DefaultPooledActorStates = 512;
        public const int DefaultPooledPresentedActors = 512;
        public const int DefaultSnapshotActorCapacity = 512;
        public const int DefaultSpritePreloadActorLimit = 64;
        public const int DefaultEquipmentPreloadVariantLimit = 32;
        public const int DefaultPacketFrameBufferBytes = 64 * 1024;
        public const int DefaultPooledStrRenderersPerEffect = 8;
        public const int DefaultPooledActorOverlays = 32;
        public const int DefaultPooledWeatherLayers = 4;

        public RoOnlineWorldBudget(
            int visibleActorLimit,
            int pooledActorStates,
            int pooledPresentedActors,
            int snapshotActorCapacity,
            int spritePreloadActorLimit,
            int equipmentPreloadVariantLimit,
            int packetFrameBufferBytes,
            int pooledStrRenderersPerEffect,
            int pooledActorOverlays,
            int pooledWeatherLayers
        ) {
            VisibleActorLimit = visibleActorLimit;
            PooledActorStates = pooledActorStates;
            PooledPresentedActors = pooledPresentedActors;
            SnapshotActorCapacity = snapshotActorCapacity;
            SpritePreloadActorLimit = spritePreloadActorLimit;
            EquipmentPreloadVariantLimit = equipmentPreloadVariantLimit;
            PacketFrameBufferBytes = packetFrameBufferBytes;
            PooledStrRenderersPerEffect = pooledStrRenderersPerEffect;
            PooledActorOverlays = pooledActorOverlays;
            PooledWeatherLayers = pooledWeatherLayers;
        }

        public int VisibleActorLimit { get; }
        public int PooledActorStates { get; }
        public int PooledPresentedActors { get; }
        public int SnapshotActorCapacity { get; }
        public int SpritePreloadActorLimit { get; }
        public int EquipmentPreloadVariantLimit { get; }
        public int PacketFrameBufferBytes { get; }
        public int PooledStrRenderersPerEffect { get; }
        public int PooledActorOverlays { get; }
        public int PooledWeatherLayers { get; }
        public bool IsValid =>
            VisibleActorLimit > 0 &&
            PooledActorStates >= VisibleActorLimit &&
            PooledPresentedActors >= VisibleActorLimit &&
            SnapshotActorCapacity >= VisibleActorLimit &&
            PacketFrameBufferBytes > 0;

        public static RoOnlineWorldBudget Default => new RoOnlineWorldBudget(
            DefaultVisibleActorLimit,
            DefaultPooledActorStates,
            DefaultPooledPresentedActors,
            DefaultSnapshotActorCapacity,
            DefaultSpritePreloadActorLimit,
            DefaultEquipmentPreloadVariantLimit,
            DefaultPacketFrameBufferBytes,
            DefaultPooledStrRenderersPerEffect,
            DefaultPooledActorOverlays,
            DefaultPooledWeatherLayers);
    }

    /// <summary>An actor's status fields (see <see cref="RoOnlineWorldState.SnapshotActorStatus"/>).</summary>
    public readonly struct RoOnlineActorStatusSnapshot {
        public RoOnlineActorStatusSnapshot(uint serverId, RoMapActorObjectType objectType, short bodyState, short healthState,
                                           int effectState, int opt3) {
            ServerId = serverId;
            ObjectType = objectType;
            BodyState = bodyState;
            HealthState = healthState;
            EffectState = effectState;
            Opt3 = opt3;
        }

        public uint ServerId { get; }
        public RoMapActorObjectType ObjectType { get; }
        public short BodyState { get; }
        public short HealthState { get; }
        public int EffectState { get; }
        public int Opt3 { get; }
    }

    public readonly struct RoOnlineActorSnapshot {
        internal RoOnlineActorSnapshot(RoOnlineActorState state) {
            ServerId = state.ServerId;
            AccountId = state.AccountId;
            IsSelf = state.IsSelf;
            ObjectType = state.ObjectType;
            SpawnState = state.SpawnState;
            Motion = state.Motion;
            IsPlayingDead = state.IsPlayingDead;
            CellX = state.CellX;
            CellY = state.CellY;
            Direction = state.Direction;
            HeadDirection = state.HeadDirection;
            HasMovePath = state.HasMovePath;
            MovePath = state.MovePath;
            MoveStartTime = state.MoveStartTime;
            ActionStartTime = state.ActionStartTime;
            ActionDurationMilliseconds = state.ActionDurationMilliseconds;
            ActionMotionTimeMilliseconds = state.ActionMotionTimeMilliseconds;
            ActionRevision = state.ActionRevision;
            CompletesActionToStandby = state.CompletesActionToStandby;
            Speed = state.Speed;
            BaseLevel = state.BaseLevel;
            Name = state.Name;
            PartyName = state.PartyName;
            GuildName = state.GuildName;
            GuildPositionName = state.GuildPositionName;
            TitleName = state.TitleName;
            TitleId = state.TitleId;
            HasTitleId = state.HasTitleId;
            HasResolvedIdentity = state.HasResolvedIdentity;
            Job = state.Job;
            BaseJob = state.BaseJob;
            CostumeJob = state.CostumeJob;
            VisualJob = state.VisualJob;
            AllRidingState = state.AllRidingState;
            HasCart = state.HasCart;
            CartNum = state.CartNum;
            HasFalcon = state.HasFalcon;
            HasWug = state.HasWug;
            Sex = state.Sex;
            Head = state.Head;
            Body = state.Body;
            Weapon = state.Weapon;
            Shield = state.Shield;
            Accessory = state.Accessory;
            Accessory2 = state.Accessory2;
            Accessory3 = state.Accessory3;
            HeadPalette = state.HeadPalette;
            BodyPalette = state.BodyPalette;
            Robe = state.Robe;
            Hp = state.Hp;
            MaxHp = state.MaxHp;
            BodyState = state.BodyState;
            HealthState = state.HealthState;
            EffectState = state.EffectState;
            GuildId = state.GuildId;
            BattlefieldTeam = state.BattlefieldTeam;
            IsPkModeOn = state.IsPkModeOn;
            ItemId = state.ItemId;
            ItemCount = state.ItemCount;
            ItemTypeRaw = state.ItemTypeRaw;
            ItemIdentified = state.ItemIdentified;
            ItemSubX = state.ItemSubX;
            ItemSubY = state.ItemSubY;
            ItemCellOffsetX = state.ItemCellOffsetX;
            ItemCellOffsetY = state.ItemCellOffsetY;
            ItemIsFalling = state.ItemIsFalling;
            ItemShowDropEffect = state.ItemShowDropEffect;
            ItemDropEffectMode = state.ItemDropEffectMode;
        }

        public uint ServerId { get; }
        public uint AccountId { get; }
        public bool IsSelf { get; }
        public RoMapActorObjectType ObjectType { get; }
        public RoMapActorSpawnState SpawnState { get; }
        public RoOnlineActorMotion Motion { get; }
        public bool IsPlayingDead { get; }
        public int CellX { get; }
        public int CellY { get; }
        public int Direction { get; }
        public ushort HeadDirection { get; }
        public bool HasMovePath { get; }
        public RoPacketPositionPair MovePath { get; }
        public uint MoveStartTime { get; }
        public uint ActionStartTime { get; }
        public int ActionDurationMilliseconds { get; }

        /// <summary>The action's attackMT (basic attack, ZC_NOTIFY_SKILL): its speed factor; 0 = plain ACT speed.</summary>
        public int ActionMotionTimeMilliseconds { get; }
        public int ActionRevision { get; }
        public bool CompletesActionToStandby { get; }
        public short Speed { get; }
        public short BaseLevel { get; }
        public string Name { get; }
        public string PartyName { get; }
        public string GuildName { get; }
        public string GuildPositionName { get; }
        public string TitleName { get; }
        public int TitleId { get; }
        public bool HasTitleId { get; }
        public bool HasResolvedIdentity { get; }
        public short Job { get; }
        public short BaseJob { get; }
        public short CostumeJob { get; }
        public short VisualJob { get; }
        public bool AllRidingState { get; }
        public bool HasCart { get; }
        public int CartNum { get; }
        public bool HasFalcon { get; }
        public bool HasWug { get; }
        public byte Sex { get; }
        public ushort Head { get; }
        public ushort Body { get; }
        public uint Weapon { get; }
        public uint Shield { get; }
        public ushort Accessory { get; }
        public ushort Accessory2 { get; }
        public ushort Accessory3 { get; }
        public short HeadPalette { get; }
        public short BodyPalette { get; }
        public ushort Robe { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public short BodyState { get; }
        public short HealthState { get; }
        public int EffectState { get; }
        /// <summary>Stone (opt1 1) or freeze (opt1 2): the unit cannot move.</summary>
        public bool IsImmobilizedByBodyState => RoOnlineActorState.IsImmobilizingBodyState(BodyState);
        public uint GuildId { get; }
        /// <summary>The battleground team from ZC_BATTLEFIELD_NOTIFY_CAMPINFO (actor +0xfb, a byte); 0 = none received.</summary>
        public byte BattlefieldTeam { get; }
        public bool IsPkModeOn { get; }
        public uint ItemId { get; }
        public short ItemCount { get; }
        public int ItemTypeRaw { get; }
        public bool ItemIdentified { get; }
        public byte ItemSubX { get; }
        public byte ItemSubY { get; }
        public float ItemCellOffsetX { get; }
        public float ItemCellOffsetY { get; }
        public bool ItemIsFalling { get; }
        public bool ItemShowDropEffect { get; }
        public short ItemDropEffectMode { get; }
    }

    public readonly struct RoOnlineMinimapMarkerSnapshot {
        public RoOnlineMinimapMarkerSnapshot(
            byte markerId,
            uint sourceId,
            int cellX,
            int cellY,
            uint colorRaw,
            bool isTimed,
            bool hasExpiration,
            uint expiresAtServerTimeMilliseconds
        ) {
            MarkerId = markerId;
            SourceId = sourceId;
            CellX = cellX;
            CellY = cellY;
            ColorRaw = colorRaw;
            IsTimed = isTimed;
            HasExpiration = hasExpiration;
            ExpiresAtServerTimeMilliseconds = expiresAtServerTimeMilliseconds;
        }

        public byte MarkerId { get; }
        public uint SourceId { get; }
        public int CellX { get; }
        public int CellY { get; }
        public uint ColorRaw { get; }
        public bool IsTimed { get; }
        public bool HasExpiration { get; }
        public uint ExpiresAtServerTimeMilliseconds { get; }
    }

    public sealed class RoOnlineWorldState {
        public const int DefaultMaxPooledActorStates = RoOnlineWorldBudget.DefaultPooledActorStates;
        private const int ClassicStraightStepCost = 10;
        private const int ClassicDiagonalStepCost = 14;
        private const int FallbackStraightStepMilliseconds = 150;
        private const uint TimedMinimapMarkerDurationMilliseconds = 15000;
        private const int ServerTimeMapEntrySanityWindowMilliseconds = 24 * 60 * 60 * 1000;
        private const ushort NvTrickDeadSkillId = 143;
        private const ushort MgSafetyWallSkillId = 12;
        private const ushort UntSafetyWallSkillId = 126;
        private const ushort AcDoubleSkillId = 46;
        private const ushort AcShowerSkillId = 47;
        private const ushort TfBackSlidingSkillId = 150;
        private const ushort TfPickStoneSkillId = 151;
        private const ushort MaShowerSkillId = 8208;

        private static readonly Comparison<RoOnlineActorSnapshot> CompareActorSnapshotsByServerId =
            (left, right) => left.ServerId.CompareTo(right.ServerId);
        private static readonly Comparison<RoOnlineMinimapMarkerSnapshot> CompareMinimapMarkersById =
            (left, right) => left.MarkerId.CompareTo(right.MarkerId);

        private readonly Dictionary<uint, RoOnlineActorState> actorsByServerId = new Dictionary<uint, RoOnlineActorState>();
        private readonly Dictionary<byte, RoOnlineMinimapMarkerSnapshot> minimapMarkersById =
            new Dictionary<byte, RoOnlineMinimapMarkerSnapshot>();
        private readonly List<byte> expiredMinimapMarkerIds = new List<byte>(8);
        private readonly Dictionary<uint, uint> serverIdByAccountId = new Dictionary<uint, uint>();
        private readonly HashSet<uint> movingActorServerIds = new HashSet<uint>();
        private readonly List<uint> movingActorScanBuffer = new List<uint>(64);
        private readonly Stack<RoOnlineActorState> actorStatePool;
        private readonly int maxPooledActorStates;
        private bool hasOwnPlayDeadVisualOverride;
        private bool ownPlayDeadVisualOverrideActive;

        public RoOnlineWorldState() : this(DefaultMaxPooledActorStates) {
        }

        public RoOnlineWorldState(int maxPooledActorStates) {
            this.maxPooledActorStates = Math.Max(0, maxPooledActorStates);
            actorStatePool = new Stack<RoOnlineActorState>(Math.Min(this.maxPooledActorStates, 64));
        }

        public uint OwnActorId { get; private set; }
        public int ActorCount => actorsByServerId.Count;
        public int MonsterLikeActorCount {
            get {
                int count = 0;
                foreach (RoOnlineActorState actor in actorsByServerId.Values) {
                    if (actor != null && IsMonsterLikeObject(actor.ObjectType)) {
                        count++;
                    }
                }

                return count;
            }
        }

        public int MovingActorCount => movingActorServerIds.Count;
        public int PooledActorStateCount => actorStatePool.Count;
        public int MaxPooledActorStateCount => maxPooledActorStates;
        public bool HasServerTime { get; private set; }
        public uint LatestServerTimeMilliseconds { get; private set; }
        public int ServerTimeRevision { get; private set; }
        public bool HasOwnMapEntryServerTime { get; private set; }
        public uint OwnMapEntryServerTimeMilliseconds { get; private set; }
        public int MinimapMarkerRevision { get; private set; }
        public int PresentationRevision { get; private set; }
        public RoOnlineWorldReconciliationStats ReconciliationStats =>
            new RoOnlineWorldReconciliationStats(completedMoveSnapCount, ignoredStaleMoveCount, ignoredDuplicateMoveCount);
        public RoMovementDiagnosticsSnapshot MovementDiagnostics =>
            new RoMovementDiagnosticsSnapshot(
                acceptedMoveCount,
                ignoredStaleMoveCount,
                ignoredDuplicateMoveCount,
                ignoredInvalidMoveCount,
                ignoredSuspiciousZeroPathCount);

        private int completedMoveSnapCount;
        private int ignoredStaleMoveCount;
        private int ignoredDuplicateMoveCount;
        private int ignoredInvalidMoveCount;
        private int ignoredSuspiciousZeroPathCount;
        private int acceptedMoveCount;

        public void Clear() {
            foreach (RoOnlineActorState actor in actorsByServerId.Values) {
                ReleaseActor(actor);
            }

            actorsByServerId.Clear();
            serverIdByAccountId.Clear();
            minimapMarkersById.Clear();
            expiredMinimapMarkerIds.Clear();
            movingActorServerIds.Clear();
            movingActorScanBuffer.Clear();
            OwnActorId = 0;
            HasServerTime = false;
            LatestServerTimeMilliseconds = 0;
            HasOwnMapEntryServerTime = false;
            OwnMapEntryServerTimeMilliseconds = 0;
            hasOwnPlayDeadVisualOverride = false;
            ownPlayDeadVisualOverrideActive = false;
            ServerTimeRevision++;
            MinimapMarkerRevision++;
            TouchPresentationRevision();
            completedMoveSnapCount = 0;
            ignoredStaleMoveCount = 0;
            ignoredDuplicateMoveCount = 0;
            ignoredInvalidMoveCount = 0;
            ignoredSuspiciousZeroPathCount = 0;
            acceptedMoveCount = 0;
        }

        public void ApplyMapEntry(RoLoginEnterMapResult result) {
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            RoCharacterInfo character = result.CharacterResult.SelectedCharacter;
            BeginMap(
                result.Aid,
                result.AcceptedEnter,
                character?.Name,
                character?.CharacterId ?? 0,
                character?.JobId ?? 0,
                character?.Sex ?? 0);
            ApplySelfCharacterAppearance(character);
            ApplyDrain(result.FollowUp);
            ApplyDrain(result.InitialState);
            ApplyDrain(result.MoveResponse);
        }

        public void BeginMap(
            RoZcAid aid,
            RoZcAcceptEnter2 acceptedEnter,
            string actorName = null,
            int characterId = 0,
            short jobId = 0,
            byte sex = 0) {
            Clear();
            uint serverId = ToServerId(aid.AccountId);
            OwnActorId = serverId;

            RoOnlineActorState actor = GetOrCreateActor(serverId);
            actor.AccountId = serverId;
            actor.IsSelf = true;
            actor.ObjectType = RoMapActorObjectType.Player;
            actor.SpawnState = RoMapActorSpawnState.Standing;
            actor.Motion = RoOnlineActorMotion.Idle;
            actor.CellX = acceptedEnter.Position.X;
            actor.CellY = acceptedEnter.Position.Y;
            actor.Direction = DirectionFromServerDirection(acceptedEnter.Position.Direction);
            actor.HasMovePath = false;
            actor.MoveStartTime = 0;
            actor.LastMoveStartTime = 0;
            actor.Name = actorName ?? string.Empty;
            actor.HasResolvedIdentity = !string.IsNullOrWhiteSpace(actor.Name);
            actor.Sex = sex;
            ApplyActorRawJob(actor, jobId);
            if (characterId > 0) {
                actor.CharacterId = characterId;
            }

            serverIdByAccountId[actor.AccountId] = actor.ServerId;
            if (acceptedEnter.StartTime >= 0) {
                uint entryServerTime = unchecked((uint) acceptedEnter.StartTime);
                SetOwnMapEntryServerTime(entryServerTime);
            }

            TouchPresentationRevision();
        }

        public void ApplySelfCharacterAppearance(RoCharacterInfo character) {
            if (character == null || OwnActorId == 0 || !TryGetActorState(OwnActorId, out RoOnlineActorState actor)) {
                return;
            }

            actor.Name = character.Name ?? actor.Name;
            if (!string.IsNullOrWhiteSpace(actor.Name)) {
                actor.HasResolvedIdentity = true;
            }
            actor.CharacterId = character.CharacterId;
            actor.Sex = character.Sex;
            actor.Speed = character.Speed;
            actor.Head = ToUnsignedAppearance(character.Head);
            actor.Body = ToUnsignedAppearance(character.Body);
            actor.Weapon = ToUnsignedAppearance(character.Weapon);
            actor.Shield = ToUnsignedAppearance(character.Shield);
            actor.Accessory = ToUnsignedAppearance(character.Accessory);
            actor.Accessory2 = ToUnsignedAppearance(character.Accessory2);
            actor.Accessory3 = ToUnsignedAppearance(character.Accessory3);
            actor.HeadPalette = character.HeadPalette;
            actor.BodyPalette = character.BodyPalette;
            actor.BodyState = unchecked((short) character.BodyState);
            actor.HealthState = unchecked((short) character.HealthState);
            actor.EffectState = character.EffectState;
            actor.BaseLevel = character.BaseLevel;
            ApplyActorRawJob(actor, character.JobId);
            NormalizeActorPresentationEquipment(actor);
            TouchPresentationRevision();
        }

        public bool ApplyOwnMapEntryPosition(RoZcAcceptEnter2 acceptedEnter) {
            if (OwnActorId == 0 || !TryGetActorState(OwnActorId, out RoOnlineActorState actor)) {
                return false;
            }

            ApplyStationaryPosition(actor, acceptedEnter.Position, RoOnlineActorMotion.Idle);
            ResetMovementEpoch(actor);
            if (acceptedEnter.StartTime >= 0) {
                uint entryServerTime = unchecked((uint) acceptedEnter.StartTime);
                SetOwnMapEntryServerTime(entryServerTime);
            }

            return true;
        }

        public bool ApplyOwnMapMovePosition(RoZcNpcMapMove mapMove) {
            if (OwnActorId == 0 || !TryGetActorState(OwnActorId, out RoOnlineActorState actor)) {
                return false;
            }

            ApplyStationaryPosition(
                actor,
                new RoPacketPosition(mapMove.X, mapMove.Y, actor.Direction),
                RoOnlineActorMotion.Idle,
                directionIsServerEncoded: false);
            actor.IsPlayingDead = false;
            // A same-map warp/respawn always lands the player alive and standing. Without this, a
            // save-point respawn on the map you died on keeps SpawnState == Dead (ApplyStationaryPosition
            // only resets Motion), so the body lies at the save point until the delayed ZC_RESURRECTION
            // (~2-3s). Cross-map respawn avoids it only because the full reload rebuilds a Standing actor.
            actor.SpawnState = RoMapActorSpawnState.Standing;
            ResetMovementEpoch(actor);
            if (HasServerTime) {
                SetOwnMapEntryServerTime(LatestServerTimeMilliseconds);
            }

            return true;
        }

        public void ApplyRuntimeActorVanish(uint actorId) {
            if (!actorsByServerId.TryGetValue(actorId, out RoOnlineActorState actor)) {
                return;
            }

            if (actor.AccountId != 0) {
                serverIdByAccountId.Remove(actor.AccountId);
            }

            actorsByServerId.Remove(actorId);
            ReleaseActor(actor);
            TouchPresentationRevision();
        }

        public void ApplyDrain(RoMapEntryDrainResult drain) {
            if (drain == null) {
                return;
            }

            if (drain.OrderedEvents.Length > 0) {
                for (int i = 0; i < drain.OrderedEvents.Length; i++) {
                    ApplyDrainEvent(drain.OrderedEvents[i]);
                }

                return;
            }

            for (int i = 0; i < drain.TimeNotifications.Length; i++) {
                ApplyServerTimeNotification(drain.TimeNotifications[i].ServerTime);
            }

            for (int i = 0; i < drain.ActorEntries.Length; i++) {
                ApplyActorEntry(drain.ActorEntries[i]);
            }

            for (int i = 0; i < drain.ActorMoves.Length; i++) {
                ApplyActorMove(drain.ActorMoves[i]);
            }

            for (int i = 0; i < drain.PlayerMoves.Length; i++) {
                ApplyPlayerMove(drain.PlayerMoves[i]);
            }

            for (int i = 0; i < drain.StopMoves.Length; i++) {
                ApplyStopMove(drain.StopMoves[i]);
            }

            for (int i = 0; i < drain.ActorVanishes.Length; i++) {
                ApplyVanish(drain.ActorVanishes[i]);
            }
        }

        public void ApplyDrainEvent(RoMapEntryDrainEvent drainEvent) {
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.TimeNotification:
                    ApplyServerTimeNotification(drainEvent.TimeNotification.ServerTime);
                    break;
                case RoMapEntryDrainEventKind.ActorEntry:
                    ApplyActorEntry(drainEvent.ActorEntry);
                    break;
                case RoMapEntryDrainEventKind.ActorMove:
                    ApplyActorMove(drainEvent.ActorMove);
                    break;
                case RoMapEntryDrainEventKind.PlayerMove:
                    ApplyPlayerMove(drainEvent.PlayerMove);
                    break;
                case RoMapEntryDrainEventKind.StopMove:
                    ApplyStopMove(drainEvent.StopMove);
                    break;
                case RoMapEntryDrainEventKind.ActorIdentity:
                    ApplyActorIdentity(drainEvent.ActorIdentity);
                    break;
                case RoMapEntryDrainEventKind.ActorHpInfo:
                    ApplyActorHpInfo(drainEvent.ActorHpInfo);
                    break;
                case RoMapEntryDrainEventKind.PartyHp:
                    ApplyPartyHp(drainEvent.PartyHp);
                    break;
                case RoMapEntryDrainEventKind.BattlefieldCampInfo:
                    ApplyBattlefieldCampInfo(drainEvent.BattlefieldCampInfo);
                    break;
                case RoMapEntryDrainEventKind.BattlefieldHp:
                    ApplyBattlefieldHp(drainEvent.BattlefieldHp);
                    break;
                case RoMapEntryDrainEventKind.ActorVanish:
                    ApplyVanish(drainEvent.ActorVanish);
                    break;
                case RoMapEntryDrainEventKind.ActorResurrection:
                    ApplyResurrection(drainEvent.ActorResurrection);
                    break;
                case RoMapEntryDrainEventKind.ActorDirectionChange:
                    ApplyActorDirectionChange(drainEvent.ActorDirectionChange);
                    break;
                case RoMapEntryDrainEventKind.ActorSpriteChange:
                    ApplyActorSpriteChange(drainEvent.ActorSpriteChange);
                    break;
                case RoMapEntryDrainEventKind.EntryQueue:
                    ApplyActorGuildChange(drainEvent.EntryQueue);
                    break;
                case RoMapEntryDrainEventKind.GroundItemEntry:
                    ApplyGroundItemEntry(drainEvent.GroundItemEntry);
                    break;
                case RoMapEntryDrainEventKind.GroundItemDisappear:
                    ApplyGroundItemDisappear(drainEvent.GroundItemDisappear);
                    break;
                case RoMapEntryDrainEventKind.ActorAction:
                    ApplyActorAction(drainEvent.ActorAction);
                    break;
                case RoMapEntryDrainEventKind.SkillUseAck:
                    ApplySkillUseAck(drainEvent.SkillUseAck);
                    break;
                case RoMapEntryDrainEventKind.OptionChange2:
                    ApplyOptionChange2(drainEvent.OptionChange2);
                    break;
                case RoMapEntryDrainEventKind.CastCancel:
                    ApplyCastCancel(drainEvent.CastCancel);
                    break;
                case RoMapEntryDrainEventKind.StatusChange:
                    ApplyStatusChange(drainEvent.StatusChange);
                    break;
                case RoMapEntryDrainEventKind.ActorStateChange:
                    ApplyActorStateChange(drainEvent.ActorStateChange);
                    break;
                case RoMapEntryDrainEventKind.ParameterChange:
                    ApplyParameterChange(drainEvent.ParameterChange);
                    break;
                case RoMapEntryDrainEventKind.StatusPropertyChange:
                    ApplyStatusPropertyChange(drainEvent.StatusPropertyChange);
                    break;
                case RoMapEntryDrainEventKind.SkillCast:
                    ApplySkillCast(drainEvent.SkillCast);
                    break;
                case RoMapEntryDrainEventKind.SkillDamage:
                    ApplySkillDamage(drainEvent.SkillDamage);
                    break;
                case RoMapEntryDrainEventKind.SkillResult:
                    ApplySkillResult(drainEvent.SkillResult);
                    break;
                case RoMapEntryDrainEventKind.SkillPosition:
                    ApplySkillPosition(drainEvent.SkillPosition);
                    break;
                case RoMapEntryDrainEventKind.GroundSkill:
                    ApplyGroundSkill(drainEvent.GroundSkill);
                    break;
                case RoMapEntryDrainEventKind.SkillEntry:
                    ApplySkillEntry(drainEvent.SkillEntry);
                    break;
                case RoMapEntryDrainEventKind.MinimapMarker:
                    ApplyMinimapMarker(drainEvent.MinimapMarker);
                    break;
            }
        }

        public void ApplyMinimapMarker(RoZcCompass marker) {
            if (marker.IsRemove) {
                if (minimapMarkersById.Remove(marker.MarkerId)) {
                    MinimapMarkerRevision++;
                }

                return;
            }

            if (marker.X < 0 || marker.Y < 0) {
                return;
            }

            bool isTimed = marker.TypeRaw == 0;
            bool hasExpiration = isTimed && HasServerTime;
            uint expiresAtServerTimeMilliseconds = hasExpiration
                ? unchecked(LatestServerTimeMilliseconds + TimedMinimapMarkerDurationMilliseconds)
                : 0u;

            minimapMarkersById[marker.MarkerId] = new RoOnlineMinimapMarkerSnapshot(
                marker.MarkerId,
                marker.NpcAid,
                marker.X,
                marker.Y,
                marker.ColorRaw,
                isTimed,
                hasExpiration,
                expiresAtServerTimeMilliseconds);
            MinimapMarkerRevision++;
        }

        public void ApplyActorEntry(RoZcNotifyActorEntry entry) {
            // entry.Aid is the unit's block-id (bl->id) — the server identifier we key by.
            // entry.Gid is the guild_id field, which is 0 for NPCs/mobs and only meaningful for players in a guild.
            if (entry.Aid == 0) {
                return;
            }

            RoOnlineActorState actor = GetOrCreateActor(entry.Aid);
            if (actor.AccountId != 0 && actor.AccountId != entry.Aid) {
                serverIdByAccountId.Remove(actor.AccountId);
            }

            actor.AccountId = entry.Aid;
            actor.IsSelf = IsOwnActor(entry.Aid, entry.Aid);
            actor.ObjectType = ResolveActorObjectType(entry.ObjectType, entry.Job);
            actor.SpawnState = entry.State;
            actor.IsPlayingDead = false;
            actor.Speed = entry.Speed;
            if (entry.ClientLevel > 0) {
                actor.BaseLevel = entry.ClientLevel;
            }

            actor.Name = entry.Name;
            actor.Sex = entry.Sex;
            actor.Head = entry.Head;
            actor.Body = entry.Body;
            actor.Weapon = entry.Weapon;
            actor.Shield = entry.Shield;
            actor.Accessory = entry.Accessory;
            actor.Accessory2 = entry.Accessory2;
            actor.Accessory3 = entry.Accessory3;
            actor.HeadPalette = entry.HeadPalette;
            actor.BodyPalette = entry.BodyPalette;
            actor.HeadDirection = entry.HeadDirection > 0 ? unchecked((ushort) entry.HeadDirection) : (ushort) 0;
            actor.Robe = entry.Robe;
            actor.Hp = entry.Hp;
            actor.MaxHp = entry.MaxHp;
            actor.BodyState = entry.BodyState;
            actor.HealthState = entry.HealthState;
            actor.EffectState = entry.EffectState;
            actor.Opt3 = entry.Virtue;   // the entry packets carry sc->opt3 in the virtue field
            actor.GuildId = entry.GuildId;
            // CGameActor::vf48 0xbe6000: the emblem version of a guild member (from 1) goes to CGuildMgr's map
            if (entry.GuildId != 0 && entry.GuildEmblemVersion >= 1) Ragnarok.UI.RoClassicGuildEmblems.SetVersion(entry.GuildId, entry.GuildEmblemVersion);
            actor.IsPkModeOn = entry.IsPkModeOn;
            ApplyActorRawJob(actor, entry.Job);
            NormalizeActorPresentationEquipment(actor);

            if (entry.State == RoMapActorSpawnState.Dead) {
                ApplyStationaryPosition(actor, entry.Position, RoOnlineActorMotion.Dead);
            } else if (entry.State == RoMapActorSpawnState.Sitting) {
                ApplyStationaryPosition(actor, entry.Position, RoOnlineActorMotion.Sitting);
            } else if (entry.HasMovePath && !IsSuspiciousZeroMovePath(entry.MovePath)) {
                ApplyMovePath(actor, entry.MovePath, entry.MoveStartTime);
            } else if (entry.HasMovePath) {
                ApplyStationaryPosition(
                    actor,
                    new RoPacketPosition(entry.MovePath.StartX, entry.MovePath.StartY, actor.Direction),
                    ResolveStationaryMotion(entry.State),
                    directionIsServerEncoded: false);
            } else {
                ApplyStationaryPosition(actor, entry.Position, RoOnlineActorMotion.Idle);
            }

            if (entry.Aid != 0) {
                serverIdByAccountId[entry.Aid] = actor.ServerId;
            }

            TouchPresentationRevision();
        }

        private static RoMapActorObjectType ResolveActorObjectType(RoMapActorObjectType objectType, short job) {
            return job == 45 ? RoMapActorObjectType.Warp : objectType;
        }

        private static bool IsMonsterLikeObject(RoMapActorObjectType objectType) {
            return objectType == RoMapActorObjectType.Monster ||
                objectType == RoMapActorObjectType.Pet ||
                objectType == RoMapActorObjectType.Homunculus ||
                objectType == RoMapActorObjectType.Mercenary ||
                objectType == RoMapActorObjectType.Elemental;
        }

        public void ApplyActorMove(RoZcNotifyMove move) {
            if (!TryGetActorState(move.Gid, out RoOnlineActorState actor)) {
                return;
            }

            ApplyMovePath(actor, move.MovePath, move.MoveStartTime);
            TouchPresentationRevision();
        }

        public void ApplyParameterChange(RoZcParameterChange change) {
            if (change.Property == RoPlayerStatusProperty.Speed) {
                ApplyOwnActorSpeed(change.Value);
            } else if (change.Property == RoPlayerStatusProperty.BaseLevel) {
                ApplyOwnActorBaseLevel(change.Value);
            } else if (change.Property == RoPlayerStatusProperty.Job) {
                ApplyOwnActorJob(change.Value);
            } else if (change.Property == RoPlayerStatusProperty.Hp || change.Property == RoPlayerStatusProperty.MaxHp) {
                ApplyOwnActorHpParameter(change.Property, change.Value);
            }
        }

        public void ApplyStatusPropertyChange(RoZcStatusPropertyChange change) {
            if (change.Property == RoPlayerStatusProperty.Speed) {
                ApplyOwnActorSpeed(change.Value);
            } else if (change.Property == RoPlayerStatusProperty.BaseLevel) {
                ApplyOwnActorBaseLevel(change.Value);
            } else if (change.Property == RoPlayerStatusProperty.Job) {
                ApplyOwnActorJob(change.Value);
            } else if (change.Property == RoPlayerStatusProperty.Hp || change.Property == RoPlayerStatusProperty.MaxHp) {
                ApplyOwnActorHpParameter(change.Property, change.Value);
            }
        }

        private void ApplyOwnActorHpParameter(RoPlayerStatusProperty property, long value) {
            if (OwnActorId == 0 || !TryGetActorState(OwnActorId, out RoOnlineActorState actor)) {
                return;
            }

            int clamped = value < 0 ? 0 : value > int.MaxValue ? int.MaxValue : (int) value;
            if (property == RoPlayerStatusProperty.MaxHp) {
                actor.MaxHp = clamped;
            } else {
                actor.Hp = clamped;
            }

            // Authoritative HP from ZC_PAR_CHANGE must lift a stale dead state. Self-revive (@revive)
            // arrives as a parameter change, not ZC_HP_INFO, so without this the actor stays Dead and
            // movement remains blocked until relog. Death is NOT set here — it keeps coming from the
            // authoritative HP-info / vanish paths so this cannot fight the combat damage model.
            if (actor.Hp > 0 &&
                (actor.SpawnState == RoMapActorSpawnState.Dead || actor.Motion == RoOnlineActorMotion.Dead)) {
                actor.SpawnState = RoMapActorSpawnState.Standing;
                actor.Motion = RoOnlineActorMotion.Idle;
                actor.IsPlayingDead = false;
            }

            TouchPresentationRevision();
        }

        public void ApplyActorIdentity(RoZcActorIdentity identity) {
            if (identity.ActorId == 0 || !TryGetActorStateByServerOrAccountId(identity.ActorId, out RoOnlineActorState actor)) {
                return;
            }

            ApplyActorIdentity(actor, identity);
            if (identity.HasTitleId && OwnActorId != 0 && identity.ActorId == OwnActorId) {
                Ragnarok.UI.RoClassicAchievements.SetCurrentTitle(identity.TitleId);   // 0xc80390: the own gid's title
            }

            TouchPresentationRevision();
        }

        /// <summary>ZC_ACK_CHANGE_TITLE on success: the own name card shows the new title.</summary>
        public void SetOwnActorTitle(int titleId, string titleName) {
            if (OwnActorId == 0 || !TryGetActorStateByServerOrAccountId(OwnActorId, out RoOnlineActorState actor)) {
                return;
            }

            actor.TitleId = titleId;
            actor.TitleName = titleName ?? string.Empty;
            actor.HasTitleId = true;
            TouchPresentationRevision();
        }

        public void ApplyActorHpInfo(RoZcHpInfo hpInfo) {
            if (hpInfo.ActorId == 0 || !TryGetActorStateByServerOrAccountId(hpInfo.ActorId, out RoOnlineActorState actor)) {
                return;
            }

            ApplyActorHp(actor, hpInfo.Hp, hpInfo.MaxHp);
            TouchPresentationRevision();
        }

        public void ApplyPartyHp(RoZcPartyHp partyHp) {
            if (partyHp.AccountId == 0 || !TryGetActorStateByServerOrAccountId(partyHp.AccountId, out RoOnlineActorState actor)) {
                return;
            }

            if (actor.ObjectType != RoMapActorObjectType.Player || actor.IsSelf) {
                return;
            }

            ApplyActorHp(actor, partyHp.Hp, partyHp.MaxHp);
            TouchPresentationRevision();
        }

        /// <summary>
        /// ZC_BATTLEFIELD_NOTIFY_CAMPINFO (inline 0xc2f48f): for the own AID the low byte becomes the own team (DAT_0154ebc4,
        /// RoPickHostility.OwnBattlefieldTeam), for anyone else the actor's team (+0xfb); an actor not in sight is dropped.
        /// The enemy mark over its head (RoClassicBattlefieldMarks) follows the team from here.
        /// </summary>
        public void ApplyBattlefieldCampInfo(RoZcBattlefieldCampInfo campInfo) {
            byte team = unchecked((byte) campInfo.Camp);
            if (campInfo.AccountId != 0 && campInfo.AccountId == OwnActorId) {
                if (RoPickHostility.OwnBattlefieldTeam != team) {
                    RoPickHostility.OwnBattlefieldTeam = team;
                    TouchPresentationRevision();
                }

                return;
            }

            if (campInfo.AccountId == 0 || !TryGetActorStateByServerOrAccountId(campInfo.AccountId, out RoOnlineActorState actor) ||
                actor.BattlefieldTeam == team) {
                return;
            }

            actor.BattlefieldTeam = team;
            TouchPresentationRevision();
        }

        /// <summary>
        /// ZC_BATTLEFIELD_NOTIFY_HP / HP2 (inline 0xc2f6e4 / 0xc2f749): a teammate's HP to its actor (SendMsg 0x22), unless it is
        /// a party member (0xd1e320) — the party's own HP packets keep that bar.
        /// </summary>
        public void ApplyBattlefieldHp(RoZcBattlefieldHp hp) {
            if (hp.AccountId == 0 || (RoPickHostility.Relations?.IsOnlinePartyMember(hp.AccountId) ?? false) ||
                !TryGetActorStateByServerOrAccountId(hp.AccountId, out RoOnlineActorState actor) || actor.IsSelf) {
                return;
            }

            ApplyActorHp(actor, hp.Hp, hp.MaxHp);
            TouchPresentationRevision();
        }

        public void ApplyStatusChange(RoZcMsgStateChange change) {
            uint actorId = change.Aid == 0 ? OwnActorId : change.Aid;
            if (actorId == 0 || !TryGetActorStateByServerOrAccountId(actorId, out RoOnlineActorState actor)) {
                return;
            }

            ApplyActorStatusVisual(actor, change);
            TouchPresentationRevision();
        }

        /// <summary>ZC_NPC_SHOWEFST_UPDATE (0x028a): the actor's option and opt3 (Quicken / Overthrust / Steel Body ... auras).</summary>
        public void ApplyOptionChange2(RoZcOptionChange2 change) {
            if (change.ActorId == 0 || !TryGetActorStateByServerOrAccountId(change.ActorId, out RoOnlineActorState actor)) {
                return;
            }

            if (actor.Opt3 == change.Opt3 && actor.EffectState == change.Option) {
                return;
            }

            actor.Opt3 = change.Opt3;
            actor.EffectState = change.Option;
            TouchPresentationRevision();
        }

        /// <summary>The status fields of every actor (opt1 / opt2 / option / opt3), for the status visual sync.</summary>
        public void SnapshotActorStatus(List<RoOnlineActorStatusSnapshot> snapshots) {
            if (snapshots == null) {
                throw new ArgumentNullException(nameof(snapshots));
            }

            snapshots.Clear();
            foreach (RoOnlineActorState actor in actorsByServerId.Values) {
                snapshots.Add(new RoOnlineActorStatusSnapshot(
                    actor.ServerId, actor.ObjectType, actor.BodyState, actor.HealthState, actor.EffectState, actor.Opt3));
            }
        }

        public void ApplyActorStateChange(RoZcStateChange change) {
            if (change.Aid == 0 || !TryGetActorStateByServerOrAccountId(change.Aid, out RoOnlineActorState actor)) {
                return;
            }

            bool wasImmobilized = RoOnlineActorState.IsImmobilizingBodyState(actor.BodyState);
            actor.BodyState = change.BodyState;
            actor.HealthState = change.HealthState;
            actor.EffectState = change.EffectState;
            actor.IsPkModeOn = change.IsPkModeOn;

            // Frost Diver freeze / full Stone Curse must immobilize the unit the instant the status
            // lands. Only act on the not-immobilized -> immobilized edge so repeated state packets
            // (or other opt1 churn while already frozen) never re-snap the unit. Petrifying/StoneWait
            // is intentionally excluded -- the official client lets the unit finish walking until it
            // turns to stone.
            bool isImmobilized = RoOnlineActorState.IsImmobilizingBodyState(actor.BodyState);
            if (!wasImmobilized && isImmobilized) {
                StopActorForImmobilizeStatus(actor);
            }

            RefreshActorRentalVisualState(actor, effectStateAuthoritative: true);
            NormalizeActorPresentationEquipment(actor);
            TouchPresentationRevision();
        }

        public void ApplyActorDirectionChange(RoZcChangeDirection change) {
            if (!TryGetActorStateByServerOrAccountId(change.Aid, out RoOnlineActorState actor)) {
                return;
            }

            actor.Direction = DirectionFromServerChangeDirection(change.DirectionRaw);
            actor.HeadDirection = change.HeadDirection;
            TouchPresentationRevision();
        }

        /// <summary>
        /// ZC_CHANGE_GUILD (0x01b4 {AID, gid, u16 emblem ver}, 0x0b47 {gid, u32 ver, AID}): the actor takes the new guild -
        /// a castle's flags, guardians and Emperium when the castle changes hands (Hercules flagemblem → clif guild_emblem_area),
        /// a player joining or leaving a guild in view. The emblem version itself goes to RoClassicGuildEmblems (the UI queue).
        /// </summary>
        public bool ApplyActorGuildChange(RoZcEntryQueue packet) {
            if (!TryParseActorGuildChange(packet, out uint aid, out uint guildId) ||
                !TryGetActorStateByServerOrAccountId(aid, out RoOnlineActorState actor) || actor.GuildId == guildId) {
                return false;
            }

            actor.GuildId = guildId;
            return true;
        }

        /// <summary>The actor and guild of a ZC_CHANGE_GUILD packet (0x01b4 or 0x0b47), false for any other packet.</summary>
        public static bool TryParseActorGuildChange(RoZcEntryQueue packet, out uint aid, out uint guildId) {
            aid = 0;
            guildId = 0;
            byte[] b = packet.Bytes;
            if (b == null) {
                return false;
            }

            if (packet.PacketId == 0x01b4 && b.Length >= 12) {
                aid = BitConverter.ToUInt32(b, 2);
                guildId = BitConverter.ToUInt32(b, 6);
                return aid != 0;
            }

            if (packet.PacketId == 0x0b47 && b.Length >= 14) {
                guildId = BitConverter.ToUInt32(b, 2);
                aid = BitConverter.ToUInt32(b, 10);
                return aid != 0;
            }

            return false;
        }

        public void ApplyActorSpriteChange(RoZcSpriteChange change) {
            if (!TryGetActorStateByServerOrAccountId(change.Gid, out RoOnlineActorState actor)) {
                return;
            }

            bool shouldNormalizeEquipment = false;
            switch (change.Type) {
                case RoMapActorSpriteChangeType.Job:
                    ApplyActorJob(actor, ClampJobId(change.Value));
                    shouldNormalizeEquipment = true;
                    break;
                case RoMapActorSpriteChangeType.Head:
                    actor.Head = unchecked((ushort) change.Value);
                    break;
                case RoMapActorSpriteChangeType.Weapon:
                    if (change.PacksWeaponAndShield) {
                        actor.Weapon = change.Value & 0x0000ffffu;
                        actor.Shield = (change.Value >> 16) & 0x0000ffffu;
                    } else {
                        actor.Weapon = change.Value;
                    }

                    shouldNormalizeEquipment = true;
                    break;
                case RoMapActorSpriteChangeType.HeadgearLower:
                    actor.Accessory = unchecked((ushort) change.Value);
                    break;
                case RoMapActorSpriteChangeType.HeadgearTop:
                    actor.Accessory2 = unchecked((ushort) change.Value);
                    break;
                case RoMapActorSpriteChangeType.HeadgearMid:
                    actor.Accessory3 = unchecked((ushort) change.Value);
                    break;
                case RoMapActorSpriteChangeType.HeadPalette:
                    actor.HeadPalette = unchecked((short) change.Value);
                    break;
                case RoMapActorSpriteChangeType.BodyPalette:
                    actor.BodyPalette = unchecked((short) change.Value);
                    break;
                case RoMapActorSpriteChangeType.Shield:
                    actor.Shield = change.Value;
                    shouldNormalizeEquipment = true;
                    break;
                case RoMapActorSpriteChangeType.Body:
                    actor.Body = unchecked((ushort) change.Value);
                    break;
                case RoMapActorSpriteChangeType.Robe:
                    actor.Robe = unchecked((ushort) change.Value);
                    break;
            }

            if (shouldNormalizeEquipment) {
                NormalizeActorPresentationEquipment(actor);
            }

            TouchPresentationRevision();
        }

        public void ApplyActorAction(RoZcNotifyAct action) {
            bool isPickupAction = RoMapActorActionSemantics.IsPickup(action.ActionRaw);
            uint actionServerTick = isPickupAction ? 0 : action.ServerTick;
            if (actionServerTick != 0) {
                AdvanceEstimatedServerTime(actionServerTick);
            }

            if (!TryGetActorStateByServerOrAccountId(action.SourceId, out RoOnlineActorState source)) {
                return;
            }

            ReconcileCompletedMoveAtServerTime(source, actionServerTick);

            RoOnlineActorState target = null;
            bool hasTarget = action.TargetId != 0 && TryGetActorStateByServerOrAccountId(action.TargetId, out target);
            if (hasTarget) {
                ReconcileCompletedMoveAtServerTime(target, actionServerTick);
            }

            if (hasTarget && ShouldFaceActorActionTarget(action.ActionRaw)) {
                source.Direction = DirectionFromCellDelta(target.CellX - source.CellX, target.CellY - source.CellY, source.Direction);
            }

            if (hasTarget && RoMapActorActionSemantics.IsDamage(action.ActionRaw)) {
                ApplyActionDamageToActor(target, action);
            }

            if (isPickupAction) {
                ApplyTransientAction(source, RoOnlineActorMotion.Pickup, 0, 500);
            } else if (RoMapActorActionSemantics.IsSit(action.ActionRaw)) {
                source.IsPlayingDead = false;
                ApplyStationaryPosition(source, new RoPacketPosition(source.CellX, source.CellY, source.Direction), RoOnlineActorMotion.Sitting, false);
                source.SpawnState = RoMapActorSpawnState.Sitting;
            } else if (RoMapActorActionSemantics.IsStand(action.ActionRaw)) {
                source.IsPlayingDead = false;
                ApplyStationaryPosition(source, new RoPacketPosition(source.CellX, source.CellY, source.Direction), RoOnlineActorMotion.Idle, false);
                source.SpawnState = RoMapActorSpawnState.Standing;
            } else if (ShouldApplyAttackMotion(source, action, hasTarget)) {
                ApplyTransientAction(
                    source,
                    ResolveBasicAttackActorMotion(source, action),
                    action.ServerTick,
                    ResolveBasicAttackSourceDurationMilliseconds(source, action),
                    action.SourceMotionTime);
            }

            TouchPresentationRevision();
        }

        public void ApplySkillCast(RoZcUseSkillAck skill) {
            if (!TryGetActorStateByServerOrAccountId(skill.SourceId, out RoOnlineActorState source)) {
                return;
            }

            uint actionStartTime = LatestServerTimeMilliseconds;
            ReconcileCompletedMoveAtServerTime(source, actionStartTime);
            FaceSkillTarget(source, skill.TargetId, skill.X, skill.Y, actionStartTime);

            if (IsPlayDeadSkill(skill.SkillId)) {
                ApplyPlayDeadVisualStateFromServer(source);
                TouchPresentationRevision();
                return;
            }

            CacheInstantSkillAttackMotion(source, skill);

            if (!ShouldSuppressSkillCastMotion(skill.SkillId) &&
                TryResolveSkillCastMotion(source, skill, out RoOnlineActorMotion motion)) {
                int durationMilliseconds = ResolveSkillCastSourceActionDurationMilliseconds(skill, motion);
                ApplyTransientAction(source, motion, actionStartTime, durationMilliseconds);
                MarkBackSlideCastingStopPreservation(source, skill.SkillId, motion);
                source.CompletesActionToStandby = ShouldCompleteSkillCastToStandbyOnMissingSuccess(source, skill);
                source.PendingSafetyWallCastX = skill.X;
                source.PendingSafetyWallCastY = skill.Y;
            }

            TouchPresentationRevision();
        }

        public bool ApplyOwnActorBaseLevel(long value) {
            if (OwnActorId == 0 || !TryGetActorState(OwnActorId, out RoOnlineActorState actor)) {
                return false;
            }

            short clamped = value < 0 ? (short) 0 : value > short.MaxValue ? short.MaxValue : (short) value;
            if (actor.BaseLevel == clamped) {
                return false;
            }

            actor.BaseLevel = clamped;
            TouchPresentationRevision();
            return true;
        }

        /// <summary>
        /// ZC_DISPEL: the client puts its own player back from the casting state to stand (state 8 → 0); other actors
        /// keep their state and finish the cast motion on their own.
        /// </summary>
        public void ApplyCastCancel(RoZcDispel dispel) {
            if (OwnActorId == 0 || dispel.ActorId != OwnActorId ||
                !TryGetActorState(OwnActorId, out RoOnlineActorState actor) ||
                actor.Motion != RoOnlineActorMotion.Casting) {
                return;
            }

            ApplyFailedSkillStandby(actor);
        }

        public void ApplySkillUseAck(RoZcAckToUseSkill ack) {
            if (OwnActorId == 0 || !TryGetActorState(OwnActorId, out RoOnlineActorState actor)) {
                return;
            }

            if (!ack.IsSuccess) {
                ApplyFailedSkillStandby(actor);
                return;
            }

            if (!IsPlayDeadSkill(ack.SkillId)) {
                return;
            }

            ApplyPlayDeadVisualStateFromServer(actor);
            TouchPresentationRevision();
        }

        public bool TryApplyPlayDeadVisualState(uint actorId) {
            if (actorId == 0 || !TryGetActorStateByServerOrAccountId(actorId, out RoOnlineActorState actor)) {
                return false;
            }

            ApplyPlayDeadVisualState(actor);
            TouchPresentationRevision();
            return true;
        }

        public bool TryTogglePlayDeadVisualState(uint actorId) {
            if (actorId == 0 || !TryGetActorStateByServerOrAccountId(actorId, out RoOnlineActorState actor)) {
                return false;
            }

            bool active = !actor.IsPlayingDead;
            SetPlayDeadVisualState(actor, active);
            if (IsOwnActor(actor.ServerId, actor.AccountId)) {
                hasOwnPlayDeadVisualOverride = true;
                ownPlayDeadVisualOverrideActive = active;
            }

            TouchPresentationRevision();
            return true;
        }

        public void ApplySkillDamage(RoZcNotifySkill skill) {
            if (!TryGetActorStateByServerOrAccountId(skill.SourceId, out RoOnlineActorState source)) {
                return;
            }

            ReconcileCompletedMoveAtServerTime(source, skill.ServerTick);

            RoOnlineActorState target = null;
            bool hasTarget = skill.TargetId != 0 && TryGetActorStateByServerOrAccountId(skill.TargetId, out target);
            if (hasTarget) {
                ReconcileCompletedMoveAtServerTime(target, skill.ServerTick);
                source.Direction = DirectionFromCellDelta(target.CellX - source.CellX, target.CellY - source.CellY, source.Direction);
                ApplyDamageToActor(target, skill.Damage);
            }

            // /swing: the older clients' attack motion, again on every further hit (ReplayMultiHitSwing)
            bool swing = Ragnarok.UI.RoClassicCommandFlags.MultiHitSwing &&
                Ragnarok.Combat.RoOriginalCombatRules.UsesClassicMultiHitSwing(skill.SkillId, skill.ActionRaw, skill.HitCount);
            RoOnlineActorMotion motion = swing ? ResolveOriginalAttackMotion(source, skill.ServerTick) : ResolveSkillActorMotion(source, skill.SkillId);
            ApplyTransientAction(source, motion, skill.ServerTick, ResolveSkillSourceActionDurationMilliseconds(source, skill),
                skill.SourceDelay);
            source.MultiHitSwingRevision = swing ? source.ActionRevision : -1;
            TouchPresentationRevision();
        }

        /// <summary>
        /// /swing: the caster's next swing of a multi-hit series (<paramref name="durationMilliseconds"/> long, from now),
        /// unless it did something else since the series' last swing. False when nothing was played.
        /// </summary>
        public bool ReplayMultiHitSwing(uint sourceId, int durationMilliseconds) {
            if (!TryGetActorStateByServerOrAccountId(sourceId, out RoOnlineActorState source) ||
                source.MultiHitSwingRevision < 0 ||
                source.ActionRevision != source.MultiHitSwingRevision ||
                source.SpawnState == RoMapActorSpawnState.Dead ||
                source.IsPlayingDead) {
                return false;
            }

            ApplyTransientAction(source, ResolveOriginalAttackMotion(source, 0), LatestServerTimeMilliseconds, durationMilliseconds,
                durationMilliseconds);   // the whole attack motion within the swing
            source.MultiHitSwingRevision = source.ActionRevision;
            return true;
        }

        public void ApplySkillResult(RoZcUseSkillResult skill) {
            if (!TryGetActorStateByServerOrAccountId(skill.SourceId, out RoOnlineActorState source)) {
                return;
            }

            if (!skill.IsSuccess) {
                ApplyFailedSkillStandby(source);
                return;
            }

            uint actionStartTime = LatestServerTimeMilliseconds;
            ReconcileCompletedMoveAtServerTime(source, actionStartTime);
            FaceSkillTarget(source, skill.TargetId, 0, 0, actionStartTime);

            if (IsPlayDeadSkill(skill.SkillId)) {
                ApplyPlayDeadVisualStateFromServer(source);
                TouchPresentationRevision();
                return;
            }

            if (!ShouldSuppressSkillResultMotion(source, skill.SkillId)) {
                RoOnlineActorMotion motion = ResolveSkillActorMotion(source, skill.SkillId);
                int durationMilliseconds = ResolveSkillResultSourceActionDurationMilliseconds(source, skill.SkillId);
                ApplyTransientAction(source, motion, actionStartTime, durationMilliseconds);
                MarkBackSlideCastingStopPreservation(source, skill.SkillId, motion);
            }

            if (skill.TargetId != 0 &&
                skill.TargetId != skill.SourceId &&
                TryGetActorStateByServerOrAccountId(skill.TargetId, out RoOnlineActorState target)) {
                ReconcileCompletedMoveAtServerTime(target, actionStartTime);
            }

            TouchPresentationRevision();
        }

        public void ApplyGroundSkill(RoZcNotifyGroundSkill skill) {
            if (!IsSafetyWallSkillOrUnit(skill.SkillId) ||
                !TryGetActorStateByServerOrAccountId(skill.SourceId, out RoOnlineActorState source)) {
                return;
            }

            if (ClearPendingSafetyWallCastSuccess(source, skill.X, skill.Y)) {
                TouchPresentationRevision();
            }
        }

        public void ApplySkillEntry(RoZcSkillEntry entry) {
            if (!IsSafetyWallSkillOrUnit(entry.SkillId) ||
                !TryGetActorStateByServerOrAccountId(entry.CreatorId, out RoOnlineActorState source)) {
                return;
            }

            if (ClearPendingSafetyWallCastSuccess(source, entry.X, entry.Y)) {
                TouchPresentationRevision();
            }
        }

        public void ApplySkillPosition(RoZcNotifySkillPosition skill) {
            if (!TryGetActorStateByServerOrAccountId(skill.SourceId, out RoOnlineActorState source)) {
                return;
            }

            ReconcileCompletedMoveAtServerTime(source, skill.ServerTick);
            FaceSkillTarget(source, skill.TargetId, skill.X, skill.Y, skill.ServerTick);
            RoOnlineActorMotion motion = ResolveSkillActorMotion(source, skill.SkillId);
            ApplyTransientAction(source, motion, skill.ServerTick, ResolveSkillPositionSourceActionDurationMilliseconds(source, skill),
                skill.SourceDelay);
            MarkBackSlideCastingStopPreservation(source, skill.SkillId, motion);

            if (skill.TargetId != 0 && TryGetActorStateByServerOrAccountId(skill.TargetId, out RoOnlineActorState target)) {
                ReconcileCompletedMoveAtServerTime(target, skill.ServerTick);
                ApplyDamageToActor(target, skill.Damage);
            }

            TouchPresentationRevision();
        }

        public void ApplyGroundItemEntry(RoZcItemEntry entry) {
            if (entry.ItemAid == 0) {
                return;
            }

            RoOnlineActorState actor = GetOrCreateActor(entry.ItemAid);
            if (actor.AccountId != 0) {
                serverIdByAccountId.Remove(actor.AccountId);
            }

            actor.AccountId = 0;
            actor.IsSelf = false;
            actor.ObjectType = RoMapActorObjectType.Item;
            actor.SpawnState = RoMapActorSpawnState.Standing;
            actor.Motion = RoOnlineActorMotion.Idle;
            actor.IsPlayingDead = false;
            movingActorServerIds.Remove(actor.ServerId);
            actor.CellX = entry.X;
            actor.CellY = entry.Y;
            actor.Direction = 0;
            actor.HeadDirection = 0;
            actor.HasMovePath = false;
            actor.MovePath = default;
            actor.MoveStartTime = 0;
            actor.LastMoveStartTime = 0;
            ClearActionState(actor);
            actor.Speed = 0;
            actor.Name = BuildGroundItemName(entry);
            ClearActorIdentityMetadata(actor);
            actor.HasResolvedIdentity = true;
            actor.Job = 0;
            actor.Sex = 0;
            actor.Head = 0;
            actor.Body = 0;
            actor.Weapon = 0;
            actor.Shield = 0;
            actor.Accessory = 0;
            actor.Accessory2 = 0;
            actor.Accessory3 = 0;
            actor.HeadPalette = 0;
            actor.BodyPalette = 0;
            actor.Robe = 0;
            actor.Hp = 0;
            actor.MaxHp = 0;
            actor.ItemId = entry.ItemId;
            actor.ItemCount = entry.Count;
            actor.ItemTypeRaw = entry.ItemTypeRaw;
            actor.ItemIdentified = entry.IsIdentified;
            actor.ItemSubX = entry.SubX;
            actor.ItemSubY = entry.SubY;
            actor.ItemCellOffsetX = CalculateGroundItemCellOffset(entry.SubX);
            actor.ItemCellOffsetY = CalculateGroundItemCellOffset(entry.SubY);
            actor.ItemIsFalling = entry.IsFalling;
            actor.ItemShowDropEffect = entry.ShowDropEffect;
            actor.ItemDropEffectMode = entry.DropEffectMode;
            TouchPresentationRevision();
        }

        public void ApplyGroundItemDisappear(RoZcItemDisappear disappear) {
            if (!actorsByServerId.TryGetValue(disappear.ItemAid, out RoOnlineActorState actor) ||
                actor.ObjectType != RoMapActorObjectType.Item) {
                return;
            }

            actorsByServerId.Remove(disappear.ItemAid);
            ReleaseActor(actor);
            TouchPresentationRevision();
        }

        public void ApplyPlayerMove(RoZcNotifyPlayerMove move) {
            if (OwnActorId == 0 || !TryGetActorState(OwnActorId, out RoOnlineActorState actor)) {
                return;
            }

            ApplyMovePath(actor, move.MovePath, move.MoveStartTime);
            TouchPresentationRevision();
        }

        public void ApplyStopMove(RoZcStopMove stop) {
            // ZC_STOPMOVE keys on the block/object id, which for monsters is their server id (they
            // carry no account id). Resolve server-id first so a mob stop never depends on the
            // account-id fallback and never collides with a player whose account id equals the GID.
            if (!TryGetActorStateByServerOrAccountId(stop.Aid, out RoOnlineActorState actor)) {
                return;
            }

            if (TryApplyStopMovePreservingBackSlideCasting(actor, stop)) {
                TouchPresentationRevision();
                return;
            }

            ApplyStationaryPosition(
                actor,
                new RoPacketPosition(stop.X, stop.Y, actor.Direction),
                actor.SpawnState == RoMapActorSpawnState.Dead ? RoOnlineActorMotion.Dead : RoOnlineActorMotion.Idle,
                false);
            TouchPresentationRevision();
        }

        public void ApplyVanish(RoZcNotifyVanish vanish) {
            if (!actorsByServerId.TryGetValue(vanish.Gid, out RoOnlineActorState actor)) {
                return;
            }

            if (vanish.Type == RoMapActorVanishType.TrickDead) {
                ApplyPlayDeadVisualStateFromServer(actor);
                TouchPresentationRevision();
                return;
            }

            // The own actor must never be removed by a vanish. On death the server vanishes the
            // player (Died), but the same character persists through @revive/respawn; removing it
            // would leave ApplyPlayerMove with no actor to move, locking movement until relog.
            // Keep self in the world and mark it dead so the authoritative HP/revive path restores it.
            // Another player who dies stays too, lying where they fell, like in the original client: the
            // server brings them back with ZC_RESURRECTION alone (no new entry while they stay in sight),
            // so a released remote player vanished for good on every revive. Monsters still go.
            if (vanish.Gid == OwnActorId || actor.IsSelf ||
                (vanish.Type == RoMapActorVanishType.Died && actor.ObjectType == RoMapActorObjectType.Player)) {
                actor.SpawnState = RoMapActorSpawnState.Dead;
                actor.Motion = RoOnlineActorMotion.Dead;
                actor.IsPlayingDead = false;
                actor.HasMovePath = false;
                actor.MoveStartTime = 0;
                movingActorServerIds.Remove(actor.ServerId);
                TouchPresentationRevision();
                return;
            }

            actorsByServerId.Remove(vanish.Gid);
            if (actor.AccountId != 0) {
                serverIdByAccountId.Remove(actor.AccountId);
            }

            ReleaseActor(actor);
            TouchPresentationRevision();
        }

        public void ApplyResurrection(RoZcResurrection resurrection) {
            // ZC_RESURRECTION carries the resurrected unit's object id (GID), looked up against the
            // server-id table exactly like ApplyVanish. The own actor and other players survive a death
            // vanish (see ApplyVanish) and stand up again here, on the cell where they fell.
            if (resurrection.Gid == 0 ||
                !actorsByServerId.TryGetValue(resurrection.Gid, out RoOnlineActorState actor)) {
                return;
            }

            actor.SpawnState = RoMapActorSpawnState.Standing;
            actor.Motion = RoOnlineActorMotion.Idle;
            actor.IsPlayingDead = false;
            actor.HasMovePath = false;
            actor.MoveStartTime = 0;
            ClearActionState(actor);
            movingActorServerIds.Remove(actor.ServerId);
            TouchPresentationRevision();
        }

        public bool TryGetActor(uint serverId, out RoOnlineActorSnapshot snapshot) {
            if (actorsByServerId.TryGetValue(serverId, out RoOnlineActorState actor)) {
                snapshot = new RoOnlineActorSnapshot(actor);
                return true;
            }

            snapshot = default;
            return false;
        }

        public bool TryGetActorByAccountId(uint accountId, out RoOnlineActorSnapshot snapshot) {
            if (serverIdByAccountId.TryGetValue(accountId, out uint serverId)) {
                return TryGetActor(serverId, out snapshot);
            }

            snapshot = default;
            return false;
        }

        public RoOnlineActorSnapshot[] SnapshotActors() {
            var snapshots = new List<RoOnlineActorSnapshot>(actorsByServerId.Count);
            SnapshotActors(snapshots);
            return snapshots.ToArray();
        }

        public void SnapshotActors(List<RoOnlineActorSnapshot> snapshots) {
            if (snapshots == null) {
                throw new ArgumentNullException(nameof(snapshots));
            }

            snapshots.Clear();
            foreach (RoOnlineActorState actor in actorsByServerId.Values) {
                snapshots.Add(new RoOnlineActorSnapshot(actor));
            }

            snapshots.Sort(CompareActorSnapshotsByServerId);
        }

        public void SnapshotMinimapMarkers(List<RoOnlineMinimapMarkerSnapshot> markers) {
            if (markers == null) {
                throw new ArgumentNullException(nameof(markers));
            }

            markers.Clear();
            foreach (RoOnlineMinimapMarkerSnapshot marker in minimapMarkersById.Values) {
                markers.Add(marker);
            }

            markers.Sort(CompareMinimapMarkersById);
        }

        public void PruneExpiredMinimapMarkers(uint serverTimeMilliseconds) {
            if (minimapMarkersById.Count == 0) {
                return;
            }

            expiredMinimapMarkerIds.Clear();
            foreach (KeyValuePair<byte, RoOnlineMinimapMarkerSnapshot> entry in minimapMarkersById) {
                RoOnlineMinimapMarkerSnapshot marker = entry.Value;
                if (marker.HasExpiration && HasServerTimeReached(serverTimeMilliseconds, marker.ExpiresAtServerTimeMilliseconds)) {
                    expiredMinimapMarkerIds.Add(entry.Key);
                }
            }

            for (int i = 0; i < expiredMinimapMarkerIds.Count; i++) {
                minimapMarkersById.Remove(expiredMinimapMarkerIds[i]);
            }

            if (expiredMinimapMarkerIds.Count > 0) {
                MinimapMarkerRevision++;
            }

            expiredMinimapMarkerIds.Clear();
        }

        public void AdvanceEstimatedServerTime(uint serverTimeMilliseconds) {
            if (!IsPlausibleServerTime(serverTimeMilliseconds)) {
                return;
            }

            bool currentServerTimeIsPlausible = IsCurrentServerTimePlausible();
            if (HasServerTime &&
                currentServerTimeIsPlausible &&
                !HasServerTimeReached(serverTimeMilliseconds, LatestServerTimeMilliseconds)) {
                return;
            }

            if (HasServerTime &&
                currentServerTimeIsPlausible &&
                serverTimeMilliseconds == LatestServerTimeMilliseconds) {
                PruneExpiredMinimapMarkers(serverTimeMilliseconds);
                return;
            }

            ApplyServerTime(serverTimeMilliseconds);
        }

        private RoOnlineActorState GetOrCreateActor(uint serverId) {
            if (actorsByServerId.TryGetValue(serverId, out RoOnlineActorState actor)) {
                return actor;
            }

            if (actorStatePool.Count > 0) {
                actor = actorStatePool.Pop();
                actor.Reset(serverId);
            } else {
                actor = new RoOnlineActorState(serverId);
            }

            actorsByServerId.Add(serverId, actor);
            RoRuntimePerfCounters.IncrementSpawnedObjects();
            TouchPresentationRevision();
            return actor;
        }

        private void ReleaseActor(RoOnlineActorState actor) {
            if (actor == null) {
                return;
            }

            movingActorServerIds.Remove(actor.ServerId);
            RoRuntimePerfCounters.IncrementDespawnedObjects();
            if (actorStatePool.Count >= maxPooledActorStates) {
                TouchPresentationRevision();
                return;
            }

            actor.Reset(0);
            actorStatePool.Push(actor);
            TouchPresentationRevision();
        }

        private void TouchPresentationRevision() {
            unchecked {
                PresentationRevision++;
                if (PresentationRevision == 0) {
                    PresentationRevision = 1;
                }
            }
        }

        private void ApplyServerTime(uint serverTimeMilliseconds) {
            HasServerTime = true;
            LatestServerTimeMilliseconds = serverTimeMilliseconds;
            ServerTimeRevision++;
            PruneExpiredMinimapMarkers(serverTimeMilliseconds);
            ReconcileCompletedMovesAtServerTime(serverTimeMilliseconds);
        }

        private void ApplyServerTimeNotification(uint serverTimeMilliseconds) {
            if (!IsPlausibleServerTime(serverTimeMilliseconds)) {
                return;
            }

            AdvanceEstimatedServerTime(serverTimeMilliseconds);
        }

        private bool IsPlausibleServerTime(uint serverTimeMilliseconds) {
            if (HasOwnMapEntryServerTime) {
                return IsServerTimeWithinMapEntrySanityWindow(serverTimeMilliseconds);
            }

            return !HasServerTime || IsServerTimeWithinMapEntrySanityWindow(serverTimeMilliseconds, LatestServerTimeMilliseconds);
        }

        private bool IsCurrentServerTimePlausible() {
            return !HasServerTime ||
                !HasOwnMapEntryServerTime ||
                IsServerTimeWithinMapEntrySanityWindow(LatestServerTimeMilliseconds);
        }

        private bool IsServerTimeWithinMapEntrySanityWindow(uint serverTimeMilliseconds) {
            return IsServerTimeWithinMapEntrySanityWindow(serverTimeMilliseconds, OwnMapEntryServerTimeMilliseconds);
        }

        private static bool IsServerTimeWithinMapEntrySanityWindow(uint serverTimeMilliseconds, uint anchorServerTimeMilliseconds) {
            int delta = unchecked((int) (serverTimeMilliseconds - anchorServerTimeMilliseconds));
            return delta >= -ServerTimeMapEntrySanityWindowMilliseconds &&
                   delta <= ServerTimeMapEntrySanityWindowMilliseconds;
        }

        private static bool HasServerTimeReached(uint serverTimeMilliseconds, uint targetServerTimeMilliseconds) {
            return unchecked((int) (serverTimeMilliseconds - targetServerTimeMilliseconds)) >= 0;
        }

        public static bool IsServerTimeBefore(uint serverTimeMilliseconds, uint targetServerTimeMilliseconds) {
            return unchecked((int) (serverTimeMilliseconds - targetServerTimeMilliseconds)) < 0;
        }

        private void SetOwnMapEntryServerTime(uint serverTimeMilliseconds) {
            HasOwnMapEntryServerTime = true;
            OwnMapEntryServerTimeMilliseconds = serverTimeMilliseconds;
        }

        private void ApplyMovePath(RoOnlineActorState actor, RoPacketPositionPair movePath, uint moveStartTime) {
            ApplyMovePath(actor, movePath, moveStartTime, validateMovePath: true);
        }

        private void ApplyMovePath(RoOnlineActorState actor, RoPacketPositionPair movePath, uint moveStartTime, bool validateMovePath) {
            if (validateMovePath) {
                RoOnlineMovePathDecision decision = EvaluateMovePath(actor, movePath, moveStartTime);
                if (decision != RoOnlineMovePathDecision.Accept) {
                    RecordIgnoredMove(decision);
                    if (RoRuntimeDiagnostics.RuntimePerfLogEnabled &&
                        RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                        UnityEngine.Debug.Log(
                            $"RO movement ignored decision={decision} actor={actor.ServerId} path={movePath.StartX},{movePath.StartY}->{movePath.EndX},{movePath.EndY} moveStartTime={moveStartTime}");
                    }

                    return;
                }
            }

            if (RoRuntimeDiagnostics.RuntimePerfLogEnabled) {
                acceptedMoveCount++;
            }

            if (moveStartTime != 0) {
                AdvanceEstimatedServerTime(moveStartTime);
            }

            actor.CellX = movePath.StartX;
            actor.CellY = movePath.StartY;
            actor.Direction = DirectionFromMoveRestingDelta(
                movePath.EndX - movePath.StartX,
                movePath.EndY - movePath.StartY,
                actor.Direction);
            actor.HeadDirection = 0;
            actor.IsPlayingDead = false;
            ClearActionState(actor);
            ClearPendingActionCompletionOverride(actor);
            actor.HasMovePath = true;
            actor.MovePath = movePath;
            actor.MoveStartTime = moveStartTime;
            actor.LastMoveStartTime = moveStartTime != 0 ? moveStartTime : actor.LastMoveStartTime;
            actor.Motion = RoOnlineActorMotion.Moving;
            movingActorServerIds.Add(actor.ServerId);
            ClearOwnPlayDeadVisualOverride(actor);
            TouchPresentationRevision();
        }

        private void ApplyStationaryPosition(
            RoOnlineActorState actor,
            RoPacketPosition position,
            RoOnlineActorMotion motion,
            bool directionIsServerEncoded = true) {
            movingActorServerIds.Remove(actor.ServerId);
            actor.CellX = position.X;
            actor.CellY = position.Y;
            actor.Direction = directionIsServerEncoded ? DirectionFromServerDirection(position.Direction) : NormalizeInternalDirection(position.Direction);
            actor.HasMovePath = false;
            actor.MoveStartTime = 0;
            if (motion != RoOnlineActorMotion.Idle) {
                actor.IsPlayingDead = false;
                ClearOwnPlayDeadVisualOverride(actor);
            }
            ClearActionState(actor);
            ClearPendingActionCompletionOverride(actor);
            actor.Motion = actor.IsPlayingDead && motion == RoOnlineActorMotion.Idle
                ? RoOnlineActorMotion.Dead
                : motion;
            TouchPresentationRevision();
        }

        private static void ResetMovementEpoch(RoOnlineActorState actor) {
            actor.HasMovePath = false;
            actor.MovePath = default;
            actor.MoveStartTime = 0;
            actor.LastMoveStartTime = 0;
            actor.ActionStartTime = 0;
            actor.ActionDurationMilliseconds = 0;
        }

        private void ApplyTransientAction(RoOnlineActorState actor, RoOnlineActorMotion motion, uint actionStartTime, int durationMilliseconds,
            int motionTimeMilliseconds = 0) {
            movingActorServerIds.Remove(actor.ServerId);
            ApplyMoveProgressCellBeforeTransientAction(actor);
            actor.HeadDirection = 0;
            actor.HasMovePath = false;
            actor.MoveStartTime = 0;
            actor.IsPlayingDead = false;
            ClearOwnPlayDeadVisualOverride(actor);
            ClearPendingActionCompletionOverride(actor);
            actor.PreserveBackSlideCastingOnNextStopMove = false;
            actor.Motion = motion;
            actor.ActionStartTime = actionStartTime;
            actor.ActionDurationMilliseconds = durationMilliseconds;
            actor.ActionMotionTimeMilliseconds = Math.Max(0, motionTimeMilliseconds);
            actor.ActionRevision++;
            TouchPresentationRevision();
        }

        private bool TryApplyStopMovePreservingBackSlideCasting(RoOnlineActorState actor, RoZcStopMove stop) {
            // rAthena sends Back Slide as a no-damage skill followed by clif_blown/fixpos.
            // Accept the authoritative cell without letting the fixpos erase the cast motion.
            if (actor == null ||
                !actor.PreserveBackSlideCastingOnNextStopMove ||
                actor.Motion != RoOnlineActorMotion.Casting ||
                actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.IsPlayingDead) {
                return false;
            }

            movingActorServerIds.Remove(actor.ServerId);
            actor.CellX = stop.X;
            actor.CellY = stop.Y;
            actor.Direction = NormalizeInternalDirection(actor.Direction);
            actor.HasMovePath = false;
            actor.MoveStartTime = 0;
            ClearPendingActionCompletionOverride(actor);
            actor.PreserveBackSlideCastingOnNextStopMove = false;
            return true;
        }

        private void ApplyFailedSkillStandby(RoOnlineActorState actor) {
            if (actor.ObjectType != RoMapActorObjectType.Player ||
                actor.Motion == RoOnlineActorMotion.Dead ||
                actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.Motion == RoOnlineActorMotion.Sitting ||
                actor.IsPlayingDead) {
                return;
            }

            movingActorServerIds.Remove(actor.ServerId);
            ApplyMoveProgressCellBeforeTransientAction(actor);
            actor.HeadDirection = 0;
            actor.HasMovePath = false;
            actor.MoveStartTime = 0;
            actor.ActionStartTime = 0;
            actor.ActionDurationMilliseconds = 0;
            ClearPendingActionCompletionOverride(actor);
            actor.Motion = RoOnlineActorMotion.Standby;
            actor.ActionRevision++;
            TouchPresentationRevision();
        }

        /// <summary>
        /// The damage motion the hit handler starts (reference combat-timing §2.2-§2.4): from the current server time for
        /// <paramref name="durationMilliseconds"/>, restarted by every hit. A walk stops where the actor is and is not
        /// resumed: the server sends a new move when the actor may walk again. False for unknown or dead actors.
        /// </summary>
        public bool ApplyHurt(uint actorId, int durationMilliseconds) {
            if (actorId == 0 ||
                !TryGetActorStateByServerOrAccountId(actorId, out RoOnlineActorState target) ||
                target.Motion == RoOnlineActorMotion.Dead ||
                target.SpawnState == RoMapActorSpawnState.Dead) {
                return false;
            }

            ApplyTransientAction(
                target,
                RoOnlineActorMotion.Hit,
                HasServerTime ? LatestServerTimeMilliseconds : 0,
                Math.Max(1, durationMilliseconds));
            return true;
        }

        // A unit entering a fully-immobilizing status mid-walk must stop on the cell it currently
        // occupies along its active server move path -- NOT the path's end cell, where
        // ReconcileCompletedMoveAtServerTime would otherwise slide it (the "frozen 2-3 cells too far"
        // bug). The authoritative position is the move-progress cell, never the skill/effect origin.
        // If the server also emits ZC_STOPMOVE it carries the same/finer cell and simply re-confirms
        // this stop; if it arrives first, the move path is already cleared and this is a no-op.
        private void StopActorForImmobilizeStatus(RoOnlineActorState actor) {
            if (!actor.HasMovePath) {
                return;
            }

            ApplyMoveProgressCellBeforeTransientAction(actor);
            ApplyStationaryPosition(
                actor,
                new RoPacketPosition(actor.CellX, actor.CellY, actor.Direction),
                ResolveStationaryMotion(actor.SpawnState),
                directionIsServerEncoded: false);
        }

        private void ApplyMoveProgressCellBeforeTransientAction(RoOnlineActorState actor) {
            if (!actor.HasMovePath || actor.MoveStartTime == 0 || !HasServerTime) {
                return;
            }

            int durationMilliseconds = CalculateMoveDurationMilliseconds(actor);
            int elapsedMilliseconds = unchecked((int) (LatestServerTimeMilliseconds - actor.MoveStartTime));
            double progress = durationMilliseconds > 0
                ? Math.Max(0d, Math.Min(1d, (double) elapsedMilliseconds / durationMilliseconds))
                : 1d;
            int cellX = (int) Math.Round(
                actor.MovePath.StartX + ((actor.MovePath.EndX - actor.MovePath.StartX) * progress),
                MidpointRounding.AwayFromZero);
            int cellY = (int) Math.Round(
                actor.MovePath.StartY + ((actor.MovePath.EndY - actor.MovePath.StartY) * progress),
                MidpointRounding.AwayFromZero);
            int correction = Math.Max(Math.Abs(cellX - actor.CellX), Math.Abs(cellY - actor.CellY));
            if (RoRuntimeDiagnostics.RuntimePerfLogEnabled &&
                RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics &&
                correction > 2) {
                UnityEngine.Debug.Log(
                    $"RO move progress snapped before transient actor={actor.ServerId} from={actor.CellX},{actor.CellY} to={cellX},{cellY} progress={progress:0.###}");
            }

            actor.CellX = cellX;
            actor.CellY = cellY;
        }

        private void FaceSkillTarget(RoOnlineActorState source, uint targetId, int x, int y, uint serverTimeMilliseconds) {
            if (source == null) {
                return;
            }

            if (targetId != 0 && TryGetActorStateByServerOrAccountId(targetId, out RoOnlineActorState target)) {
                ReconcileCompletedMoveAtServerTime(target, serverTimeMilliseconds);
                source.Direction = DirectionFromCellDelta(target.CellX - source.CellX, target.CellY - source.CellY, source.Direction);
                return;
            }

            if (x > 0 || y > 0) {
                source.Direction = DirectionFromCellDelta(x - source.CellX, y - source.CellY, source.Direction);
            }
        }

        private static void ClearActionState(RoOnlineActorState actor) {
            actor.ActionStartTime = 0;
            actor.ActionDurationMilliseconds = 0;
            actor.PreserveBackSlideCastingOnNextStopMove = false;
        }

        private static bool IsPlayDeadSkill(ushort skillId) {
            return skillId == NvTrickDeadSkillId;
        }

        private static bool IsBackSlideSkill(ushort skillId) {
            return skillId == TfBackSlidingSkillId;
        }

        private static bool ShouldUseBackSlideCastingMotion(RoOnlineActorState source, ushort skillId) {
            return IsBackSlideSkill(skillId) &&
                source != null &&
                source.ObjectType == RoMapActorObjectType.Player;
        }

        private static void MarkBackSlideCastingStopPreservation(
            RoOnlineActorState source,
            ushort skillId,
            RoOnlineActorMotion motion) {
            if (source == null) {
                return;
            }

            source.PreserveBackSlideCastingOnNextStopMove =
                motion == RoOnlineActorMotion.Casting &&
                ShouldUseBackSlideCastingMotion(source, skillId);
        }

        private static bool IsSafetyWallSkillOrUnit(ushort skillId) {
            return skillId == MgSafetyWallSkillId ||
                skillId == UntSafetyWallSkillId;
        }

        private static bool ShouldSuppressSkillCastMotion(ushort skillId) {
            return skillId == AcDoubleSkillId;
        }

        private static bool ShouldSuppressSkillResultMotion(RoOnlineActorState source, ushort skillId) {
            return skillId == TfPickStoneSkillId &&
                source != null &&
                source.Motion == RoOnlineActorMotion.Pickup;
        }

        private static bool ShouldCompleteSkillCastToStandbyOnMissingSuccess(RoOnlineActorState source, RoZcUseSkillAck skill) {
            return source != null &&
                source.ObjectType == RoMapActorObjectType.Player &&
                IsSafetyWallSkillOrUnit(skill.SkillId);
        }

        private static int ResolveSkillCastSourceActionDurationMilliseconds(RoZcUseSkillAck skill, RoOnlineActorMotion motion) {
            if (motion == RoOnlineActorMotion.Casting && skill.SkillId == TfBackSlidingSkillId) {
                return 0;
            }

            int attackMotionMilliseconds = ResolveActionDurationMilliseconds((int) skill.AttackMotionTime, 450);
            if (motion == RoOnlineActorMotion.Pickup) {
                return attackMotionMilliseconds;
            }

            return ResolveActionDurationMilliseconds((int) skill.DelayTime, attackMotionMilliseconds);
        }

        private static int ResolveSkillResultSourceActionDurationMilliseconds(RoOnlineActorState source, ushort skillId) {
            return ShouldUseBackSlideCastingMotion(source, skillId) ? 0 : 450;
        }

        private static bool ClearPendingSafetyWallCastSuccess(RoOnlineActorState source, int x, int y) {
            if (source == null || !source.CompletesActionToStandby) {
                return false;
            }

            if (source.PendingSafetyWallCastX != x ||
                source.PendingSafetyWallCastY != y) {
                return false;
            }

            ClearPendingActionCompletionOverride(source);
            return true;
        }

        private static void ClearPendingActionCompletionOverride(RoOnlineActorState actor) {
            if (actor == null) {
                return;
            }

            actor.CompletesActionToStandby = false;
            actor.PendingSafetyWallCastX = 0;
            actor.PendingSafetyWallCastY = 0;
        }

        private void ApplyPlayDeadVisualState(RoOnlineActorState actor) {
            SetPlayDeadVisualState(actor, active: true);
        }

        private void ApplyPlayDeadVisualStateFromServer(RoOnlineActorState actor) {
            if (IsOwnActor(actor.ServerId, actor.AccountId) && hasOwnPlayDeadVisualOverride) {
                SetPlayDeadVisualState(actor, ownPlayDeadVisualOverrideActive);
                return;
            }

            ApplyPlayDeadVisualState(actor);
        }

        private void SetPlayDeadVisualState(RoOnlineActorState actor, bool active) {
            movingActorServerIds.Remove(actor.ServerId);
            actor.HasMovePath = false;
            actor.MoveStartTime = 0;
            actor.IsPlayingDead = active;
            actor.Motion = active
                ? RoOnlineActorMotion.Dead
                : actor.SpawnState == RoMapActorSpawnState.Dead ? RoOnlineActorMotion.Dead : RoOnlineActorMotion.Idle;
            actor.ActionStartTime = 0;
            actor.ActionDurationMilliseconds = 0;
            actor.ActionRevision++;
        }

        private void ClearOwnPlayDeadVisualOverride(RoOnlineActorState actor) {
            if (actor == null || !IsOwnActor(actor.ServerId, actor.AccountId)) {
                return;
            }

            hasOwnPlayDeadVisualOverride = false;
            ownPlayDeadVisualOverrideActive = false;
        }

        private static void ClearActorIdentityMetadata(RoOnlineActorState actor) {
            actor.PartyName = string.Empty;
            actor.GuildName = string.Empty;
            actor.GuildPositionName = string.Empty;
            actor.TitleName = string.Empty;
            actor.TitleId = 0;
            actor.HasTitleId = false;
            actor.HasResolvedIdentity = false;
        }

        private static void ApplyActorIdentity(RoOnlineActorState actor, RoZcActorIdentity identity) {
            if (!string.IsNullOrWhiteSpace(identity.Name)) {
                actor.Name = identity.Name;
            }

            actor.PartyName = identity.PartyName ?? string.Empty;
            actor.GuildName = identity.GuildName ?? string.Empty;
            actor.GuildPositionName = identity.GuildPositionName ?? string.Empty;
            actor.TitleName = identity.TitleName ?? string.Empty;
            actor.TitleId = identity.TitleId;
            actor.HasTitleId = identity.HasTitleId;
            actor.HasResolvedIdentity = true;
            if (identity.TryParseHitPointsFromPartyInfo(out int hp, out int maxHp)) {
                ApplyActorHp(actor, hp, maxHp);
            }
        }

        private static void ApplyActorHp(RoOnlineActorState actor, int hp, int maxHp) {
            actor.MaxHp = Math.Max(0, maxHp);
            actor.Hp = actor.MaxHp > 0
                ? Math.Max(0, Math.Min(actor.MaxHp, hp))
                : Math.Max(0, hp);

            if (actor.MaxHp > 0 && actor.Hp <= 0) {
                actor.SpawnState = RoMapActorSpawnState.Dead;
                actor.Motion = RoOnlineActorMotion.Dead;
                actor.IsPlayingDead = false;
                actor.HasMovePath = false;
                actor.MoveStartTime = 0;
            } else if (actor.SpawnState == RoMapActorSpawnState.Dead && actor.Hp > 0) {
                actor.SpawnState = RoMapActorSpawnState.Standing;
                actor.Motion = RoOnlineActorMotion.Idle;
                actor.IsPlayingDead = false;
            }
        }

        private static void ApplyActionDamageToActor(RoOnlineActorState actor, RoZcNotifyAct action) {
            if (!RoMapActorActionSemantics.IsDamage(action.ActionRaw)) {
                return;
            }

            if (action.IsSpDamage) {
                return;
            }

            int damage = Math.Max(0, action.Damage) + Math.Max(0, action.Damage2);
            if (damage > 0) {
                ApplyDamageToActor(actor, damage);
            } else if (action.Damage < 0) {
                ApplyDamageToActor(actor, action.Damage);
            }
        }

        private static void ApplyDamageToActor(RoOnlineActorState actor, int damage) {
            if (actor.MaxHp <= 0 || damage == 0) {
                return;
            }

            if (damage > 0) {
                actor.Hp = Math.Max(1, actor.Hp - damage);
            } else {
                actor.Hp = Math.Min(actor.MaxHp, actor.Hp - damage);
            }
        }

        private void ReconcileCompletedMovesAtServerTime(uint serverTimeMilliseconds) {
            if (movingActorServerIds.Count == 0) {
                return;
            }

            movingActorScanBuffer.Clear();
            foreach (uint serverId in movingActorServerIds) {
                movingActorScanBuffer.Add(serverId);
            }

            for (int i = 0; i < movingActorScanBuffer.Count; i++) {
                if (actorsByServerId.TryGetValue(movingActorScanBuffer[i], out RoOnlineActorState actor)) {
                    ReconcileCompletedMoveAtServerTime(actor, serverTimeMilliseconds);
                }
            }
        }

        private void ReconcileCompletedMoveAtServerTime(RoOnlineActorState actor, uint serverTimeMilliseconds) {
            if (actor == null ||
                actor.Motion != RoOnlineActorMotion.Moving ||
                !actor.HasMovePath ||
                actor.MoveStartTime == 0 ||
                serverTimeMilliseconds == 0) {
                return;
            }

            if (IsSuspiciousZeroMovePath(actor.MovePath)) {
                ApplyStationaryPosition(
                    actor,
                    new RoPacketPosition(actor.CellX, actor.CellY, actor.Direction),
                    ResolveStationaryMotion(actor.SpawnState),
                    false);
                return;
            }

            int elapsedMilliseconds = unchecked((int) (serverTimeMilliseconds - actor.MoveStartTime));
            if (elapsedMilliseconds < CalculateMoveDurationMilliseconds(actor)) {
                return;
            }

            ApplyStationaryPosition(
                actor,
                new RoPacketPosition(actor.MovePath.EndX, actor.MovePath.EndY, actor.Direction),
                actor.SpawnState == RoMapActorSpawnState.Dead ? RoOnlineActorMotion.Dead : RoOnlineActorMotion.Idle,
                false);
            completedMoveSnapCount++;
        }

        private static RoOnlineMovePathDecision EvaluateMovePath(RoOnlineActorState actor, RoPacketPositionPair movePath, uint moveStartTime) {
            if (IsSuspiciousZeroMovePath(movePath)) {
                return RoOnlineMovePathDecision.IgnoreSuspiciousZeroPath;
            }

            if (actor == null || moveStartTime == 0 || actor.LastMoveStartTime == 0) {
                return RoOnlineMovePathDecision.Accept;
            }

            int tickDelta = unchecked((int) (moveStartTime - actor.LastMoveStartTime));
            if (tickDelta < 0) {
                return RoOnlineMovePathDecision.IgnoreStale;
            }

            if (tickDelta == 0) {
                if (actor.Motion != RoOnlineActorMotion.Moving) {
                    return RoOnlineMovePathDecision.IgnoreDuplicate;
                }

                return actor.HasMovePath && actor.MovePath.Equals(movePath)
                    ? RoOnlineMovePathDecision.IgnoreDuplicate
                    : RoOnlineMovePathDecision.Accept;
            }

            return RoOnlineMovePathDecision.Accept;
        }

        private void RecordIgnoredMove(RoOnlineMovePathDecision decision) {
            if (decision == RoOnlineMovePathDecision.IgnoreStale) {
                ignoredStaleMoveCount++;
            } else if (decision == RoOnlineMovePathDecision.IgnoreDuplicate) {
                ignoredDuplicateMoveCount++;
            } else if (decision == RoOnlineMovePathDecision.IgnoreInvalid) {
                ignoredInvalidMoveCount++;
            } else if (decision == RoOnlineMovePathDecision.IgnoreSuspiciousZeroPath) {
                ignoredSuspiciousZeroPathCount++;
            }
        }

        private static bool IsSuspiciousZeroMovePath(RoPacketPositionPair movePath) {
            return movePath.EndX == 0 &&
                movePath.EndY == 0 &&
                (movePath.StartX != 0 || movePath.StartY != 0);
        }

        private static RoOnlineActorMotion ResolveStationaryMotion(RoMapActorSpawnState state) {
            if (state == RoMapActorSpawnState.Dead) {
                return RoOnlineActorMotion.Dead;
            }

            return state == RoMapActorSpawnState.Sitting
                ? RoOnlineActorMotion.Sitting
                : RoOnlineActorMotion.Idle;
        }

        private static int CalculateMoveDurationMilliseconds(RoOnlineActorState actor) {
            return CalculateMoveDurationMilliseconds(actor.MovePath, actor.Speed);
        }

        private static int CalculateMoveDurationMilliseconds(RoPacketPositionPair movePath, short speed) {
            int straightStepMilliseconds = speed > 0 ? speed : FallbackStraightStepMilliseconds;
            return RoLiveMoveRoute.CalculateServerDeltaDurationMilliseconds(
                movePath.EndX - movePath.StartX,
                movePath.EndY - movePath.StartY,
                straightStepMilliseconds);
        }

        private void ApplyOwnActorSpeed(long speed) {
            if (OwnActorId == 0 || !TryGetActorState(OwnActorId, out RoOnlineActorState actor)) {
                return;
            }

            actor.Speed = ClampMoveSpeed(speed);
            TouchPresentationRevision();
        }

        private void ApplyOwnActorJob(long jobId) {
            if (OwnActorId == 0 || !TryGetActorState(OwnActorId, out RoOnlineActorState actor)) {
                return;
            }

            ApplyActorJob(actor, ClampJobId(jobId));
            TouchPresentationRevision();
        }

        private static void ApplyActorRawJob(RoOnlineActorState actor, short rawJob) {
            if (actor == null) {
                return;
            }

            RoMapRentalJobResolution resolution = RoMapRentalVisualSemantics.NormalizeServerJob(
                rawJob,
                actor.EffectState,
                actor.AllRidingState);
            actor.BaseJob = resolution.BaseJob;
            actor.CostumeJob = resolution.CostumeJob;
            actor.VisualJob = resolution.VisualJob;
            actor.Job = resolution.VisualJob;
            RoMapRentalAttachmentState attachments = RoMapRentalVisualSemantics.ResolveAttachments(actor.EffectState);
            actor.HasFalcon = actor.FalconStatus || attachments.HasFalcon;
            actor.HasWug = attachments.HasWug;
        }

        private static void RefreshActorRentalVisualState(RoOnlineActorState actor, bool effectStateAuthoritative = false) {
            if (actor == null) {
                return;
            }

            if (actor.BaseJob == 0 && actor.Job != 0) {
                RoMapRentalJobResolution resolution = RoMapRentalVisualSemantics.NormalizeServerJob(
                    actor.Job,
                    actor.EffectState,
                    actor.AllRidingState);
                actor.BaseJob = resolution.BaseJob;
                actor.CostumeJob = resolution.CostumeJob;
            }

            short baseJob = ResolveActorBaseJob(actor);
            actor.VisualJob = RoMapRentalVisualSemantics.ResolveVisualJob(
                baseJob,
                actor.EffectState,
                actor.AllRidingState,
                actor.CostumeJob);
            actor.Job = actor.VisualJob != 0 ? actor.VisualJob : baseJob;

            RoMapRentalAttachmentState attachments = RoMapRentalVisualSemantics.ResolveAttachments(actor.EffectState);
            if (effectStateAuthoritative) {
                actor.FalconStatus = false;
            }

            actor.HasFalcon = actor.FalconStatus || attachments.HasFalcon;
            actor.HasWug = attachments.HasWug;

            int cartVariant = RoMapRentalVisualSemantics.ResolveCartVariantFromEffectState(actor.EffectState);
            if (cartVariant >= 0 && !actor.HasCart) {
                actor.HasCart = true;
                actor.CartNum = cartVariant;
            } else if (effectStateAuthoritative && cartVariant < 0) {
                actor.HasCart = false;
                actor.CartNum = 0;
            }
        }

        private static void ApplyActorStatusVisual(RoOnlineActorState actor, RoZcMsgStateChange change) {
            if (actor == null) {
                return;
            }

            switch (change.StatusId) {
                case RoMapRentalVisualSemantics.StatusOnPushCart:
                    actor.HasCart = change.Active;
                    if (change.Active && change.Value1 >= 0) {
                        actor.CartNum = change.Value1;
                    } else if (!change.Active) {
                        actor.CartNum = 0;
                    }

                    int cartEffect = actor.HasCart
                        ? RoMapRentalVisualSemantics.ResolveCartEffectMask(actor.CartNum)
                        : 0;
                    actor.EffectState = (actor.EffectState & ~RoMapRentalVisualSemantics.CartEffectMask) | cartEffect;
                    CompleteStationaryCartChangeMotion(actor);
                    break;
                case RoMapRentalVisualSemantics.StatusAllRiding:
                    actor.AllRidingState = change.Active || change.Value1 > 0;
                    break;
                case RoMapRentalVisualSemantics.StatusFalcon:
                    actor.FalconStatus = change.Active || change.Value1 > 0;
                    break;
                default:
                    return;
            }

            RefreshActorRentalVisualState(actor);
            NormalizeActorPresentationEquipment(actor);
        }

        private static void CompleteStationaryCartChangeMotion(RoOnlineActorState actor) {
            if (actor == null ||
                actor.ObjectType != RoMapActorObjectType.Player ||
                actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.IsPlayingDead ||
                actor.Motion == RoOnlineActorMotion.Dead ||
                actor.Motion == RoOnlineActorMotion.Sitting ||
                actor.Motion == RoOnlineActorMotion.Moving ||
                actor.HasMovePath ||
                actor.Motion != RoOnlineActorMotion.Skill) {
                return;
            }

            ClearActionState(actor);
            ClearPendingActionCompletionOverride(actor);
            actor.HeadDirection = 0;
            actor.Motion = RoOnlineActorMotion.Idle;
            actor.ActionRevision++;
        }

        private static void ApplyActorJob(RoOnlineActorState actor, short jobId) {
            if (actor == null) {
                return;
            }

            short previousBaseJob = actor.BaseJob;
            short previousVisualJob = actor.VisualJob != 0 ? actor.VisualJob : actor.Job;
            ApplyActorRawJob(actor, jobId);
            if (actor.BaseJob == previousBaseJob && actor.Job == previousVisualJob) {
                return;
            }

            actor.Body = 0;
            NormalizeActorPresentationEquipment(actor);
        }

        private static short ResolveActorBaseJob(RoOnlineActorState actor) {
            if (actor == null) {
                return 0;
            }

            return actor.BaseJob != 0 || actor.CostumeJob != 0 || actor.VisualJob != 0
                ? actor.BaseJob
                : actor.Job;
        }

        private static short ClampMoveSpeed(long speed) {
            if (speed <= 0) {
                return 0;
            }

            return speed > short.MaxValue ? short.MaxValue : (short) speed;
        }

        private static short ClampJobId(long jobId) {
            if (jobId > short.MaxValue) {
                return short.MaxValue;
            }

            if (jobId < short.MinValue) {
                return short.MinValue;
            }

            return (short) jobId;
        }

        private bool TryGetActorState(uint serverId, out RoOnlineActorState actor) {
            return actorsByServerId.TryGetValue(serverId, out actor);
        }

        private bool TryGetActorStateByAccountId(uint accountId, out RoOnlineActorState actor) {
            if (serverIdByAccountId.TryGetValue(accountId, out uint serverId)) {
                return actorsByServerId.TryGetValue(serverId, out actor);
            }

            return actorsByServerId.TryGetValue(accountId, out actor);
        }

        private bool TryGetActorStateByServerOrAccountId(uint id, out RoOnlineActorState actor) {
            return actorsByServerId.TryGetValue(id, out actor) || TryGetActorStateByAccountId(id, out actor);
        }

        private bool IsOwnActor(uint serverId, uint accountId) {
            return OwnActorId != 0 && (serverId == OwnActorId || accountId == OwnActorId);
        }

        public static int DirectionFromServerChangeDirection(byte directionRaw) {
            return DirectionFromServerDirection(directionRaw);
        }

        public static int DirectionFromServerDirection(int directionRaw) {
            switch (directionRaw) {
                case 0:
                    return 4;
                case 1:
                    return 3;
                case 2:
                    return 2;
                case 3:
                    return 1;
                case 4:
                    return 0;
                case 5:
                    return 7;
                case 6:
                    return 6;
                case 7:
                    return 5;
                default:
                    return 0;
            }
        }

        private static int NormalizeInternalDirection(int direction) {
            return direction >= 0 && direction <= 7 ? direction : 0;
        }

        public static int DirectionFromCellDelta(int deltaX, int deltaY, int fallbackDirection) {
            if (deltaX == 0 && deltaY == 0) {
                return NormalizeInternalDirection(fallbackDirection);
            }

            int stepX = Math.Sign(deltaX);
            int stepY = Math.Sign(deltaY);
            if (stepX == 0 && stepY > 0) {
                return 4;
            }

            if (stepX < 0 && stepY > 0) {
                return 3;
            }

            if (stepX < 0 && stepY == 0) {
                return 2;
            }

            if (stepX < 0 && stepY < 0) {
                return 1;
            }

            if (stepX == 0 && stepY < 0) {
                return 0;
            }

            if (stepX > 0 && stepY < 0) {
                return 7;
            }

            if (stepX > 0 && stepY == 0) {
                return 6;
            }

            return 5;
        }

        // Resting facing after a straight walk. RO/roBrowser walk diagonals first, so the LAST step — which
        // sets the idle facing — is the dominant axis whenever the move is not a perfect diagonal. The old
        // code stored DirectionFromCellDelta(overall) here, whose per-axis Sign() turns any mixed move into a
        // diagonal: a (-2,-5) walk rested facing SouthWest ("skos") although the actor (and the original
        // client) ends facing South ("prosto"). Only used for the move resting direction; attack facing keeps
        // DirectionFromCellDelta. Internal convention: S=0,SW=1,W=2,NW=3,N=4,NE=5,E=6,SE=7.
        public static int DirectionFromMoveRestingDelta(int deltaX, int deltaY, int fallbackDirection) {
            if (deltaX == 0 && deltaY == 0) {
                return NormalizeInternalDirection(fallbackDirection);
            }

            int adx = Math.Abs(deltaX);
            int ady = Math.Abs(deltaY);
            if (adx > ady) {
                return deltaX > 0 ? 6 : 2; // East : West
            }

            if (ady > adx) {
                return deltaY > 0 ? 4 : 0; // North : South
            }

            if (deltaX > 0) {
                return deltaY > 0 ? 5 : 7; // NorthEast : SouthEast
            }

            return deltaY > 0 ? 3 : 1; // NorthWest : SouthWest
        }

        private static bool ShouldApplyAttackMotion(RoOnlineActorState source, RoZcNotifyAct action, bool hasTarget) {
            if (!RoMapActorActionSemantics.IsDamage(action.ActionRaw)) {
                return false;
            }

            return ShouldApplyMonsterAttackMotion(
                source != null ? source.ObjectType : RoMapActorObjectType.Unknown,
                hasTarget,
                action.Damage,
                action.Damage2);
        }

        private static RoOnlineActorMotion ResolveBasicAttackActorMotion(RoOnlineActorState source, RoZcNotifyAct action) =>
            ResolveOriginalAttackMotion(source, action.ServerTick);

        private static RoOnlineActorMotion ResolvePreferredBasicAttackActorMotion(RoOnlineActorState source) =>
            ResolveOriginalAttackMotion(source, 0);

        /// <summary>
        /// The attack motion the original plays (reference combat-timing §6): players motion 9 (ACT action 11 = Attack) or
        /// motion 2 (ACT action 10 = Attack2) from the job / sex / weapon class table, the Taekwon line at random (drawn
        /// from the packet's server tick, like the hit presenter's hit frame); others their attack action. Criticals and
        /// the left hand never change it.
        /// </summary>
        private static RoOnlineActorMotion ResolveOriginalAttackMotion(RoOnlineActorState source, uint serverTick) {
            if (source == null || source.ObjectType != RoMapActorObjectType.Player) {
                return RoOnlineActorMotion.Attack;
            }

            return RoHitPresenter.UsesAttackMotion9(ToHitActor(source), serverTick)
                ? RoOnlineActorMotion.Attack
                : RoOnlineActorMotion.Attack2;
        }

        /// <summary>An actor as the combat rules see it: display job, sex, right-hand weapon class.</summary>
        private static RoHitActor ToHitActor(RoOnlineActorState actor) => new RoHitActor {
            Job = actor.VisualJob != 0 ? actor.VisualJob : actor.Job,
            Sex = actor.Sex,
            WeaponClass = (int) RoMapWeaponActionSemantics.ResolveWeaponType(actor.Weapon, realType: true),
            IsPlayer = actor.ObjectType == RoMapActorObjectType.Player
        };

        private static RoOnlineActorMotion ResolveSkillActorMotion(RoOnlineActorState source, ushort skillId) {
            if (ShouldUseBackSlideCastingMotion(source, skillId)) {
                return RoOnlineActorMotion.Casting;
            }

            if (skillId == AcDoubleSkillId) {
                return ResolvePreferredBasicAttackActorMotion(source);
            }

            if (skillId == AcShowerSkillId || skillId == MaShowerSkillId) {
                return ResolvePreferredBasicAttackActorMotion(source);
            }

            // the original's caster action for the skill (FUN_00d15360, RoOriginalCombatRules.SkillActionState)
            switch (Ragnarok.Combat.RoOriginalCombatRules.SkillActionState(skillId)) {
                case 2:
                case 9:
                    return ResolveOriginalAttackMotion(source, 0);
                case 12:
                    return RoOnlineActorMotion.Throwing;
                case 5:
                    return RoOnlineActorMotion.Pickup;
                case 0:
                    return RoOnlineActorMotion.Idle;
                case -1:
                    // no SetAction: the caster keeps what it was doing
                    return source != null && IsSkillKeepableMotion(source.Motion) ? source.Motion : RoOnlineActorMotion.Idle;
                default:
                    return RoOnlineActorMotion.Skill;
            }
        }

        private static bool IsSkillKeepableMotion(RoOnlineActorMotion motion) =>
            motion != RoOnlineActorMotion.Moving && motion != RoOnlineActorMotion.Dead && motion != RoOnlineActorMotion.Casting;

        private static int ResolveBasicAttackSourceDurationMilliseconds(RoOnlineActorState source, RoZcNotifyAct action) {
            return RoMapBasicAttackTimingSemantics.ResolveSourceAttackDurationMilliseconds(
                action,
                source != null ? source.ObjectType : RoMapActorObjectType.Unknown,
                source != null ? ResolveActorBaseJob(source) : (short) 0,
                source != null ? source.Sex : (byte) 0,
                source != null ? source.Weapon : 0);
        }

        private static void NormalizeActorPresentationEquipment(RoOnlineActorState actor) {
            if (actor == null) {
                return;
            }

            RoMapPresentedEquipment equipment = RoMapWeaponActionSemantics.NormalizePresentationEquipment(
                actor.ObjectType,
                actor.VisualJob != 0 ? actor.VisualJob : actor.Job,
                actor.Weapon,
                actor.Shield);
            actor.Weapon = equipment.Weapon;
            actor.Shield = equipment.Shield;
        }

        public static bool ShouldApplyMonsterAttackMotion(
            RoMapActorObjectType objectType,
            bool hasTarget,
            int damage,
            int damage2
        ) {
            if (!IsMonsterLikeForAttackFiltering(objectType)) {
                return true;
            }

            return hasTarget || damage != 0 || damage2 != 0;
        }

        private static bool IsMonsterLikeForAttackFiltering(RoMapActorObjectType objectType) {
            return objectType == RoMapActorObjectType.Monster ||
                objectType == RoMapActorObjectType.Pet ||
                objectType == RoMapActorObjectType.Homunculus ||
                objectType == RoMapActorObjectType.Mercenary ||
                objectType == RoMapActorObjectType.Elemental ||
                objectType == RoMapActorObjectType.Unknown;
        }

        private static bool ShouldFaceActorActionTarget(byte actionRaw) {
            return RoMapActorActionSemantics.IsDamage(actionRaw) ||
                RoMapActorActionSemantics.IsPickup(actionRaw);
        }

        private static void CacheInstantSkillAttackMotion(RoOnlineActorState source, RoZcUseSkillAck skill) {
            if (source == null || !ShouldUseInstantSkillAttackMotion(skill.SkillId, skill.DelayTime) || skill.AttackMotionTime == 0) {
                ClearPendingSkillAttackMotion(source);
                return;
            }

            source.PendingSkillAttackMotionSkillId = skill.SkillId;
            source.PendingSkillAttackMotionTargetId = skill.TargetId;
            source.PendingSkillAttackMotionMilliseconds = (int) Math.Min(int.MaxValue, skill.AttackMotionTime);
        }

        private static int ResolveSkillSourceActionDurationMilliseconds(RoOnlineActorState source, RoZcNotifySkill skill) {
            int sourceDelayMilliseconds = ResolveActionDurationMilliseconds(skill.SourceDelay, 450);
            int pendingMilliseconds = ConsumePendingSkillAttackMotionMilliseconds(source, skill.SkillId, skill.TargetId);
            return Math.Max(sourceDelayMilliseconds, pendingMilliseconds);
        }

        private static int ResolveSkillPositionSourceActionDurationMilliseconds(RoOnlineActorState source, RoZcNotifySkillPosition skill) {
            if (ShouldUseBackSlideCastingMotion(source, skill.SkillId)) {
                return 0;
            }

            int sourceDelayMilliseconds = ResolveActionDurationMilliseconds(skill.SourceDelay, 450);
            int pendingMilliseconds = ConsumePendingSkillAttackMotionMilliseconds(source, skill.SkillId, skill.TargetId);
            return Math.Max(sourceDelayMilliseconds, pendingMilliseconds);
        }

        private static int ConsumePendingSkillAttackMotionMilliseconds(RoOnlineActorState source, ushort skillId, uint targetId) {
            if (source == null || source.PendingSkillAttackMotionMilliseconds <= 0) {
                return 0;
            }

            int result = 0;
            if (source.PendingSkillAttackMotionSkillId == skillId &&
                (source.PendingSkillAttackMotionTargetId == 0 || source.PendingSkillAttackMotionTargetId == targetId)) {
                result = ResolveActionDurationMilliseconds(source.PendingSkillAttackMotionMilliseconds, 0);
            }

            ClearPendingSkillAttackMotion(source);
            return result;
        }

        private static void ClearPendingSkillAttackMotion(RoOnlineActorState source) {
            if (source == null) {
                return;
            }

            source.PendingSkillAttackMotionSkillId = 0;
            source.PendingSkillAttackMotionTargetId = 0;
            source.PendingSkillAttackMotionMilliseconds = 0;
        }

        private static bool IsAttackActionState(int state) => state == 2 || state == 9 || state == 12;

        private static bool ShouldUseInstantSkillAttackMotion(ushort skillId, uint delayTime) {
            return delayTime == 0 &&
                IsAttackActionState(Ragnarok.Combat.RoOriginalCombatRules.SkillActionState(skillId));
        }

        /// <summary>
        /// The cast motion (FUN_00c7b930 → FUN_00d13d20, RoOriginalCombatRules.CastStartState): none without a cast time
        /// (FUN_00c7f710 returns early), else the skill's cast state — mostly the held cast pose.
        /// </summary>
        private static bool TryResolveSkillCastMotion(RoOnlineActorState actor, RoZcUseSkillAck skill, out RoOnlineActorMotion motion) {
            motion = default;
            // monsters and other non-players keep their motion (CNpc::SetAction has no cast state)
            if (actor == null || skill.DelayTime == 0 || actor.ObjectType != RoMapActorObjectType.Player) {
                return false;
            }

            switch (Ragnarok.Combat.RoOriginalCombatRules.CastStartState(skill.SkillId)) {
                case -1:
                    return false;
                case 2:
                case 9:
                    motion = ResolveOriginalAttackMotion(actor, 0);
                    return true;
                case 5:
                    motion = RoOnlineActorMotion.Pickup;
                    return true;
                case 12:
                    motion = RoOnlineActorMotion.Throwing;
                    return true;
                case 0:
                    motion = RoOnlineActorMotion.Idle;
                    return true;
                case 11:
                    motion = RoOnlineActorMotion.Standby;
                    return true;
                case 7:
                    motion = RoOnlineActorMotion.Skill;
                    return true;
                default:
                    motion = RoOnlineActorMotion.Casting;
                    return true;
            }
        }

        private static int ResolveActionDurationMilliseconds(int packetDurationMilliseconds, int fallbackMilliseconds) {
            return packetDurationMilliseconds > 0 ? packetDurationMilliseconds : Math.Max(1, fallbackMilliseconds);
        }

        public static float CalculateGroundItemCellOffset(byte subCell) {
            return -0.5f + subCell / 12f;
        }

        private static ushort ToUnsignedAppearance(short value) {
            return value > 0 ? unchecked((ushort) value) : (ushort) 0;
        }

        private static uint ToServerId(int value) {
            return unchecked((uint) value);
        }

        private static string BuildGroundItemName(RoZcItemEntry entry) {
            int count = entry.Count > 0 ? entry.Count : 0;
            return count > 1
                ? $"Item {entry.ItemId} x{count}"
                : $"Item {entry.ItemId}";
        }

        private enum RoOnlineMovePathDecision {
            Accept = 0,
            IgnoreStale = 1,
            IgnoreDuplicate = 2,
            IgnoreInvalid = 3,
            IgnoreSuspiciousZeroPath = 4
        }
    }

    internal sealed class RoOnlineActorState {
        public RoOnlineActorState(uint serverId) {
            Reset(serverId);
        }

        public void Reset(uint serverId) {
            ServerId = serverId;
            AccountId = 0;
            CharacterId = 0;
            IsSelf = false;
            ObjectType = RoMapActorObjectType.Unknown;
            SpawnState = RoMapActorSpawnState.Standing;
            Motion = RoOnlineActorMotion.Idle;
            IsPlayingDead = false;
            CellX = 0;
            CellY = 0;
            Direction = 0;
            HeadDirection = 0;
            HasMovePath = false;
            MovePath = default;
            MoveStartTime = 0;
            LastMoveStartTime = 0;
            ActionStartTime = 0;
            ActionDurationMilliseconds = 0;
            ActionRevision = 0;
            CompletesActionToStandby = false;
            PendingSafetyWallCastX = 0;
            PendingSafetyWallCastY = 0;
            PendingSkillAttackMotionSkillId = 0;
            PendingSkillAttackMotionTargetId = 0;
            PendingSkillAttackMotionMilliseconds = 0;
            MultiHitSwingRevision = -1;
            PreserveBackSlideCastingOnNextStopMove = false;
            Speed = 0;
            BaseLevel = 0;
            Name = string.Empty;
            PartyName = string.Empty;
            GuildName = string.Empty;
            GuildPositionName = string.Empty;
            TitleName = string.Empty;
            TitleId = 0;
            HasTitleId = false;
            HasResolvedIdentity = false;
            Job = 0;
            BaseJob = 0;
            CostumeJob = 0;
            VisualJob = 0;
            AllRidingState = false;
            HasCart = false;
            CartNum = 0;
            FalconStatus = false;
            HasFalcon = false;
            HasWug = false;
            Sex = 0;
            Head = 0;
            Body = 0;
            Weapon = 0;
            Shield = 0;
            Accessory = 0;
            Accessory2 = 0;
            Accessory3 = 0;
            HeadPalette = 0;
            BodyPalette = 0;
            Robe = 0;
            Hp = 0;
            MaxHp = 0;
            BodyState = 0;
            HealthState = 0;
            EffectState = 0;
            GuildId = 0;
            BattlefieldTeam = 0;
            IsPkModeOn = false;
            ItemId = 0;
            ItemCount = 0;
            ItemTypeRaw = -1;
            ItemIdentified = false;
            ItemSubX = 0;
            ItemSubY = 0;
            ItemCellOffsetX = 0f;
            ItemCellOffsetY = 0f;
            ItemIsFalling = false;
            ItemShowDropEffect = false;
            ItemDropEffectMode = 0;
        }

        public uint ServerId { get; private set; }
        public uint AccountId { get; set; }
        public int CharacterId { get; set; }
        public bool IsSelf { get; set; }
        public RoMapActorObjectType ObjectType { get; set; }
        /// <summary>sc->opt3 (entry packets' virtue field, ZC_NPC_SHOWEFST_UPDATE).</summary>
        public int Opt3 { get; set; }
        public RoMapActorSpawnState SpawnState { get; set; }
        public RoOnlineActorMotion Motion { get; set; }
        public bool IsPlayingDead { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }
        public int Direction { get; set; }
        public ushort HeadDirection { get; set; }
        public bool HasMovePath { get; set; }
        public RoPacketPositionPair MovePath { get; set; }
        public uint MoveStartTime { get; set; }
        public uint LastMoveStartTime { get; set; }
        public uint ActionStartTime { get; set; }
        public int ActionDurationMilliseconds { get; set; }
        public int ActionMotionTimeMilliseconds { get; set; }
        public int ActionRevision { get; set; }
        public bool CompletesActionToStandby { get; set; }
        public int PendingSafetyWallCastX { get; set; }
        public int PendingSafetyWallCastY { get; set; }
        public ushort PendingSkillAttackMotionSkillId { get; set; }
        public uint PendingSkillAttackMotionTargetId { get; set; }
        public int PendingSkillAttackMotionMilliseconds { get; set; }
        /// <summary>/swing: the action revision of the series' last swing, −1 when no series runs.</summary>
        public int MultiHitSwingRevision { get; set; } = -1;
        public bool PreserveBackSlideCastingOnNextStopMove { get; set; }
        public short Speed { get; set; }
        public short BaseLevel { get; set; }
        public string Name { get; set; }
        public string PartyName { get; set; }
        public string GuildName { get; set; }
        public string GuildPositionName { get; set; }
        public string TitleName { get; set; }
        public int TitleId { get; set; }
        public bool HasTitleId { get; set; }
        public bool HasResolvedIdentity { get; set; }
        public short Job { get; set; }
        public short BaseJob { get; set; }
        public short CostumeJob { get; set; }
        public short VisualJob { get; set; }
        public bool AllRidingState { get; set; }
        public bool HasCart { get; set; }
        public int CartNum { get; set; }
        public bool FalconStatus { get; set; }
        public bool HasFalcon { get; set; }
        public bool HasWug { get; set; }
        public byte Sex { get; set; }
        public ushort Head { get; set; }
        public ushort Body { get; set; }
        public uint Weapon { get; set; }
        public uint Shield { get; set; }
        public ushort Accessory { get; set; }
        public ushort Accessory2 { get; set; }
        public ushort Accessory3 { get; set; }
        public short HeadPalette { get; set; }
        public short BodyPalette { get; set; }
        public ushort Robe { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public short BodyState { get; set; }
        public short HealthState { get; set; }
        public int EffectState { get; set; }

        /// <summary>opt1 values that stop the unit: 1 stone, 2 freeze (opt1 is a value, not a bit set).</summary>
        public static bool IsImmobilizingBodyState(short bodyState) => bodyState == 1 || bodyState == 2;
        public uint GuildId { get; set; }
        public byte BattlefieldTeam { get; set; }
        public bool IsPkModeOn { get; set; }
        public uint ItemId { get; set; }
        public short ItemCount { get; set; }
        public int ItemTypeRaw { get; set; }
        public bool ItemIdentified { get; set; }
        public byte ItemSubX { get; set; }
        public byte ItemSubY { get; set; }
        public float ItemCellOffsetX { get; set; }
        public float ItemCellOffsetY { get; set; }
        public bool ItemIsFalling { get; set; }
        public bool ItemShowDropEffect { get; set; }
        public short ItemDropEffectMode { get; set; }
    }
}
