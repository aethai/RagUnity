using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public enum RoStatusStat : ushort {
        Str = 0x0d,
        Agi = 0x0e,
        Vit = 0x0f,
        Int = 0x10,
        Dex = 0x11,
        Luk = 0x12
    }

    public enum RoStatusCommandStatus {
        Accepted = 0,
        InactiveMapSession = 1,
        InvalidStat = 2,
        InvalidAmount = 3,
        NotEnoughStatusPoints = 4,
        UnsupportedProfile = 5,
        SendFailed = 6,
        Disposed = 7
    }

    public readonly struct RoStatusAllocateRequest {
        public RoStatusAllocateRequest(RoStatusStat stat, int amount) {
            Stat = stat;
            Amount = amount;
        }

        public RoStatusStat Stat { get; }
        public int Amount { get; }
    }

    public readonly struct RoStatusCommandResult {
        public RoStatusCommandResult(RoStatusCommandStatus status, RoStatusStat stat, int amount, string message) {
            Status = status;
            Stat = stat;
            Amount = amount;
            Message = message ?? string.Empty;
        }

        public RoStatusCommandStatus Status { get; }
        public RoStatusStat Stat { get; }
        public int Amount { get; }
        public string Message { get; }
        public bool Accepted => Status == RoStatusCommandStatus.Accepted;
    }

    public readonly struct RoCharacterStatusSnapshot {
        public RoCharacterStatusSnapshot(
            long baseLevel,
            long jobLevel,
            long baseExperience,
            long jobExperience,
            long nextBaseExperience,
            long nextJobExperience,
            long hp,
            long maxHp,
            long sp,
            long maxSp,
            long zeny,
            long weight,
            long maxWeight,
            int weightPercentage,
            int statusPoints,
            bool hasKnownStatusPoints,
            int skillPoints,
            int str,
            int needStr,
            int agi,
            int needAgi,
            int vit,
            int needVit,
            int intelligence,
            int needInt,
            int dex,
            int needDex,
            int luk,
            int needLuk,
            int attack,
            int defense,
            int magicDefense,
            int hit,
            int flee,
            int critical) {
            BaseLevel = baseLevel;
            JobLevel = jobLevel;
            BaseExperience = baseExperience;
            JobExperience = jobExperience;
            NextBaseExperience = nextBaseExperience;
            NextJobExperience = nextJobExperience;
            Hp = hp;
            MaxHp = maxHp;
            Sp = sp;
            MaxSp = maxSp;
            Zeny = zeny;
            Weight = weight;
            MaxWeight = maxWeight;
            WeightPercentage = weightPercentage;
            StatusPoints = statusPoints;
            HasKnownStatusPoints = hasKnownStatusPoints;
            SkillPoints = skillPoints;
            Str = str;
            NeedStr = needStr;
            Agi = agi;
            NeedAgi = needAgi;
            Vit = vit;
            NeedVit = needVit;
            Int = intelligence;
            NeedInt = needInt;
            Dex = dex;
            NeedDex = needDex;
            Luk = luk;
            NeedLuk = needLuk;
            Attack = attack;
            Defense = defense;
            MagicDefense = magicDefense;
            Hit = hit;
            Flee = flee;
            Critical = critical;
        }

        public long BaseLevel { get; }
        public long JobLevel { get; }
        public long BaseExperience { get; }
        public long JobExperience { get; }
        public long NextBaseExperience { get; }
        public long NextJobExperience { get; }
        public long Hp { get; }
        public long MaxHp { get; }
        public long Sp { get; }
        public long MaxSp { get; }
        public long Zeny { get; }
        public long Weight { get; }
        public long MaxWeight { get; }
        public int WeightPercentage { get; }
        public int StatusPoints { get; }
        public bool HasKnownStatusPoints { get; }
        public int SkillPoints { get; }
        public int Str { get; }
        public int NeedStr { get; }
        public int Agi { get; }
        public int NeedAgi { get; }
        public int Vit { get; }
        public int NeedVit { get; }
        public int Int { get; }
        public int NeedInt { get; }
        public int Dex { get; }
        public int NeedDex { get; }
        public int Luk { get; }
        public int NeedLuk { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int MagicDefense { get; }
        public int Hit { get; }
        public int Flee { get; }
        public int Critical { get; }
    }
    public enum RoSocialCommandStatus {
        Accepted = 0,
        InactiveMapSession = 1,
        InvalidArgument = 2,
        UnsupportedProfile = 3,
        SendFailed = 4,
        Disposed = 5
    }

    public readonly struct RoSocialCommandResult {
        public RoSocialCommandResult(RoSocialCommandStatus status, string message) {
            Status = status;
            Message = message ?? string.Empty;
        }

        public RoSocialCommandStatus Status { get; }
        public string Message { get; }
        public bool Accepted => Status == RoSocialCommandStatus.Accepted;

        public static RoSocialCommandResult AcceptedResult() {
            return new RoSocialCommandResult(RoSocialCommandStatus.Accepted, string.Empty);
        }

        public static RoSocialCommandResult Inactive() {
            return new RoSocialCommandResult(RoSocialCommandStatus.InactiveMapSession, "Map runtime session is not active.");
        }
    }

    public readonly struct RoQuestCommandResult {
        public RoQuestCommandResult(RoSocialCommandStatus status, uint questId, string message) {
            Status = status;
            QuestId = questId;
            Message = message ?? string.Empty;
        }

        public RoSocialCommandStatus Status { get; }
        public uint QuestId { get; }
        public string Message { get; }
        public bool Accepted => Status == RoSocialCommandStatus.Accepted;
    }

    public readonly struct RoQuestSetActiveRequest {
        public RoQuestSetActiveRequest(uint questId, bool active) {
            QuestId = questId;
            Active = active;
        }

        public uint QuestId { get; }
        public bool Active { get; }
    }

    public readonly struct RoPartyInviteRequest {
        public RoPartyInviteRequest(string characterName) {
            CharacterName = characterName ?? string.Empty;
        }

        public string CharacterName { get; }
    }

    public readonly struct RoPartyInviteResponseRequest {
        public RoPartyInviteResponseRequest(uint invitationId, bool accept) {
            InvitationId = invitationId;
            Accept = accept;
        }

        public uint InvitationId { get; }
        public bool Accept { get; }
    }

    public readonly struct RoPartyCreateRequest {
        public RoPartyCreateRequest(string partyName, byte itemPickupRule, byte itemDivisionRule) {
            PartyName = partyName ?? string.Empty;
            ItemPickupRule = itemPickupRule;
            ItemDivisionRule = itemDivisionRule;
        }

        public string PartyName { get; }
        public byte ItemPickupRule { get; }
        public byte ItemDivisionRule { get; }
    }

    public readonly struct RoPartyExpelRequest {
        public RoPartyExpelRequest(uint accountId, string characterName) {
            AccountId = accountId;
            CharacterName = characterName ?? string.Empty;
        }

        public uint AccountId { get; }
        public string CharacterName { get; }
    }

    public readonly struct RoPartyLeaderChangeRequest {
        public RoPartyLeaderChangeRequest(uint accountId) {
            AccountId = accountId;
        }

        public uint AccountId { get; }
    }

    public readonly struct RoPartyOptionsRequest {
        public RoPartyOptionsRequest(uint expOption, byte itemPickupRule, byte itemDivisionRule) {
            ExpOption = expOption;
            ItemPickupRule = itemPickupRule;
            ItemDivisionRule = itemDivisionRule;
        }

        public uint ExpOption { get; }
        public byte ItemPickupRule { get; }
        public byte ItemDivisionRule { get; }
    }

    public readonly struct RoFriendNameRequest {
        public RoFriendNameRequest(string characterName) {
            CharacterName = characterName ?? string.Empty;
        }

        public string CharacterName { get; }
    }

    public readonly struct RoFriendIdRequest {
        public RoFriendIdRequest(uint accountId, uint characterId) {
            AccountId = accountId;
            CharacterId = characterId;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
    }

    public readonly struct RoFriendResponseRequest {
        public RoFriendResponseRequest(uint accountId, uint characterId, bool accept) {
            AccountId = accountId;
            CharacterId = characterId;
            Accept = accept;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
        public bool Accept { get; }
    }

    public readonly struct RoQuestObjectiveRuntimeSnapshot {
        public RoQuestObjectiveRuntimeSnapshot(uint huntIdent, uint huntIdent2, uint mobId, int current, int total, string mobName)
            : this(huntIdent, huntIdent2, mobId, current, total, mobName, 0, 0, 0) {
        }

        public RoQuestObjectiveRuntimeSnapshot(uint huntIdent, uint huntIdent2, uint mobId, int current, int total, string mobName,
            uint mobType, short levelMin, short levelMax) {
            HuntIdent = huntIdent;
            HuntIdent2 = huntIdent2;
            MobId = mobId;
            Current = Math.Max(0, current);
            Total = Math.Max(0, total);
            MobName = mobName ?? string.Empty;
            MobType = mobType;
            LevelMin = levelMin;
            LevelMax = levelMax;
        }

        /// <summary>The special-group mask of a mob 31999 objective (ZC_QUEST_LIST / ZC_ADD_QUEST_EX mobType).</summary>
        public uint MobType { get; }
        public short LevelMin { get; }
        public short LevelMax { get; }

        public uint HuntIdent { get; }
        public uint HuntIdent2 { get; }
        public uint MobId { get; }
        public int Current { get; }
        public int Total { get; }
        public string MobName { get; }
    }

    public readonly struct RoQuestRuntimeEntrySnapshot {
        public RoQuestRuntimeEntrySnapshot(uint questId, byte activeRaw, int serverTime, int endTime, RoQuestObjectiveRuntimeSnapshot[] objectives) {
            QuestId = questId;
            ActiveRaw = activeRaw;
            ServerTime = serverTime;
            EndTime = endTime;
            Objectives = Copy(objectives);
        }

        public uint QuestId { get; }
        public byte ActiveRaw { get; }
        public bool IsActive => ActiveRaw != 0;
        public int ServerTime { get; }
        public int EndTime { get; }
        public RoQuestObjectiveRuntimeSnapshot[] Objectives { get; }

        public RoQuestRuntimeEntrySnapshot WithActive(byte activeRaw) {
            return new RoQuestRuntimeEntrySnapshot(QuestId, activeRaw, ServerTime, EndTime, Objectives);
        }

        public RoQuestRuntimeEntrySnapshot WithObjectives(RoQuestObjectiveRuntimeSnapshot[] objectives) {
            return new RoQuestRuntimeEntrySnapshot(QuestId, ActiveRaw, ServerTime, EndTime, objectives);
        }

        private static RoQuestObjectiveRuntimeSnapshot[] Copy(RoQuestObjectiveRuntimeSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoQuestObjectiveRuntimeSnapshot>();
            }

            var copy = new RoQuestObjectiveRuntimeSnapshot[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
    public readonly struct RoPartyMemberRuntimeSnapshot {
        public RoPartyMemberRuntimeSnapshot(
            uint accountId,
            uint characterId,
            string name,
            string mapName,
            byte role,
            byte state,
            int classId,
            int baseLevel,
            int x,
            int y,
            int hp,
            int maxHp,
            bool isDead) {
            AccountId = accountId;
            CharacterId = characterId;
            Name = name ?? string.Empty;
            MapName = mapName ?? string.Empty;
            Role = role;
            State = state;
            ClassId = classId;
            BaseLevel = baseLevel;
            X = x;
            Y = y;
            Hp = hp;
            MaxHp = maxHp;
            IsDead = isDead;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
        public string Name { get; }
        public string MapName { get; }
        public byte Role { get; }
        public byte State { get; }
        public int ClassId { get; }
        public int BaseLevel { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public bool IsDead { get; }

        public RoPartyMemberRuntimeSnapshot WithPosition(int x, int y) {
            return new RoPartyMemberRuntimeSnapshot(AccountId, CharacterId, Name, MapName, Role, State, ClassId, BaseLevel, x, y, Hp, MaxHp, IsDead);
        }

        public RoPartyMemberRuntimeSnapshot WithHp(int hp, int maxHp) {
            return new RoPartyMemberRuntimeSnapshot(AccountId, CharacterId, Name, MapName, Role, State, ClassId, BaseLevel, X, Y, hp, maxHp, IsDead);
        }

        public RoPartyMemberRuntimeSnapshot WithAlive(bool isDead) {
            return new RoPartyMemberRuntimeSnapshot(AccountId, CharacterId, Name, MapName, Role, State, ClassId, BaseLevel, X, Y, Hp, MaxHp, isDead);
        }

        public RoPartyMemberRuntimeSnapshot WithJobAndLevel(int classId, int baseLevel) {
            return new RoPartyMemberRuntimeSnapshot(AccountId, CharacterId, Name, MapName, Role, State, classId, baseLevel, X, Y, Hp, MaxHp, IsDead);
        }

        public RoPartyMemberRuntimeSnapshot WithRole(byte role) {
            return new RoPartyMemberRuntimeSnapshot(AccountId, CharacterId, Name, MapName, role, State, ClassId, BaseLevel, X, Y, Hp, MaxHp, IsDead);
        }
    }

    public readonly struct RoPartyRuntimeSnapshot {
        public RoPartyRuntimeSnapshot(
            string partyName,
            uint pendingInvitationId,
            string pendingInvitationName,
            bool hasLastInvitationAnswer,
            string lastInvitationAnswerCharacterName,
            int lastInvitationAnswerCode,
            uint expOption,
            byte itemPickupRule,
            byte itemDivisionRule,
            RoPartyMemberRuntimeSnapshot[] members)
            : this(
                partyName,
                false,
                0,
                null,
                pendingInvitationId,
                pendingInvitationName,
                hasLastInvitationAnswer,
                lastInvitationAnswerCharacterName,
                lastInvitationAnswerCode,
                expOption,
                itemPickupRule,
                itemDivisionRule,
                members) {
        }

        public RoPartyRuntimeSnapshot(
            string partyName,
            bool isInParty,
            uint leaderAccountId,
            bool? refuseJoinMessages,
            uint pendingInvitationId,
            string pendingInvitationName,
            bool hasLastInvitationAnswer,
            string lastInvitationAnswerCharacterName,
            int lastInvitationAnswerCode,
            uint expOption,
            byte itemPickupRule,
            byte itemDivisionRule,
            RoPartyMemberRuntimeSnapshot[] members) {
            PartyName = partyName ?? string.Empty;
            IsInParty = isInParty;
            LeaderAccountId = leaderAccountId;
            RefuseJoinMessages = refuseJoinMessages;
            PendingInvitationId = pendingInvitationId;
            PendingInvitationName = pendingInvitationName ?? string.Empty;
            HasLastInvitationAnswer = hasLastInvitationAnswer;
            LastInvitationAnswerCharacterName = lastInvitationAnswerCharacterName ?? string.Empty;
            LastInvitationAnswerCode = lastInvitationAnswerCode;
            ExpOption = expOption;
            ItemPickupRule = itemPickupRule;
            ItemDivisionRule = itemDivisionRule;
            Members = Copy(members);
        }

        public string PartyName { get; }
        public bool IsInParty { get; }
        public uint LeaderAccountId { get; }
        public bool? RefuseJoinMessages { get; }
        public uint PendingInvitationId { get; }
        public string PendingInvitationName { get; }
        public bool HasLastInvitationAnswer { get; }
        public string LastInvitationAnswerCharacterName { get; }
        public int LastInvitationAnswerCode { get; }
        public bool LastInvitationAnswerAccepted => HasLastInvitationAnswer && LastInvitationAnswerCode == 2;
        public uint ExpOption { get; }
        public byte ItemPickupRule { get; }
        public byte ItemDivisionRule { get; }
        public RoPartyMemberRuntimeSnapshot[] Members { get; }

        private static RoPartyMemberRuntimeSnapshot[] Copy(RoPartyMemberRuntimeSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoPartyMemberRuntimeSnapshot>();
            }

            var copy = new RoPartyMemberRuntimeSnapshot[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    public sealed class RoPartyRuntime : IDisposable {
        private readonly object sync = new object();
        private readonly Dictionary<uint, RoPartyMemberRuntimeSnapshot> members = new Dictionary<uint, RoPartyMemberRuntimeSnapshot>();
        private readonly Dictionary<uint, RoZcPartyHp> pendingHp = new Dictionary<uint, RoZcPartyHp>();
        private readonly Func<RoPartyInviteRequest, CancellationToken, Task> sendInvite;
        private readonly Func<CancellationToken, Task> sendLeave;
        private readonly Func<RoPartyInviteResponseRequest, CancellationToken, Task> sendInviteResponse;
        private readonly Func<RoPartyCreateRequest, CancellationToken, Task> sendCreate;
        private readonly Func<RoPartyExpelRequest, CancellationToken, Task> sendExpel;
        private readonly Func<RoPartyLeaderChangeRequest, CancellationToken, Task> sendLeaderChange;
        private readonly Func<RoPartyOptionsRequest, CancellationToken, Task> sendOptions;
        private bool disposed;
        private bool isInParty;
        private string partyName = string.Empty;
        private uint leaderAccountId;
        private bool? refuseJoinMessages;
        private uint pendingInvitationId;
        private string pendingInvitationName = string.Empty;
        private bool hasLastInvitationAnswer;
        private string lastInvitationAnswerCharacterName = string.Empty;
        private int lastInvitationAnswerCode;
        private uint expOption;
        private byte itemPickupRule;
        private byte itemDivisionRule;

        public RoPartyRuntime(
            Func<RoPartyInviteRequest, CancellationToken, Task> sendInvite,
            Func<CancellationToken, Task> sendLeave,
            Func<RoPartyInviteResponseRequest, CancellationToken, Task> sendInviteResponse) {
            this.sendInvite = sendInvite;
            this.sendLeave = sendLeave;
            this.sendInviteResponse = sendInviteResponse;
        }

        public RoPartyRuntime(
            Func<RoPartyInviteRequest, CancellationToken, Task> sendInvite,
            Func<CancellationToken, Task> sendLeave,
            Func<RoPartyInviteResponseRequest, CancellationToken, Task> sendInviteResponse,
            Func<RoPartyCreateRequest, CancellationToken, Task> sendCreate,
            Func<RoPartyExpelRequest, CancellationToken, Task> sendExpel,
            Func<RoPartyLeaderChangeRequest, CancellationToken, Task> sendLeaderChange,
            Func<RoPartyOptionsRequest, CancellationToken, Task> sendOptions) {
            this.sendInvite = sendInvite;
            this.sendLeave = sendLeave;
            this.sendInviteResponse = sendInviteResponse;
            this.sendCreate = sendCreate;
            this.sendExpel = sendExpel;
            this.sendLeaderChange = sendLeaderChange;
            this.sendOptions = sendOptions;
        }

        /// <summary>
        /// FUN_00d1e3b0(id, 1): a party member with this account id whose state is online (0). No allocation (the battle
        /// log asks per landed hit).
        /// </summary>
        public bool IsOnlineMember(uint accountId) {
            lock (sync) {
                return accountId != 0 && members.TryGetValue(accountId, out RoPartyMemberRuntimeSnapshot member) && member.State == 0;
            }
        }

        /// <summary>FUN_00cec750: the party list's member with this account id (its name), false when not in the list.</summary>
        public bool TryGetMember(uint accountId, out RoPartyMemberRuntimeSnapshot member) {
            lock (sync) {
                return members.TryGetValue(accountId, out member);
            }
        }

        public RoPartyRuntimeSnapshot Snapshot() {
            lock (sync) {
                var values = new RoPartyMemberRuntimeSnapshot[members.Count];
                members.Values.CopyTo(values, 0);
                Array.Sort(values, (left, right) => left.AccountId.CompareTo(right.AccountId));
                return new RoPartyRuntimeSnapshot(
                    partyName,
                    isInParty || members.Count > 0,
                    leaderAccountId,
                    refuseJoinMessages,
                    pendingInvitationId,
                    pendingInvitationName,
                    hasLastInvitationAnswer,
                    lastInvitationAnswerCharacterName,
                    lastInvitationAnswerCode,
                    expOption,
                    itemPickupRule,
                    itemDivisionRule,
                    values);
            }
        }

        public void Apply(RoZcPartyInfo info) {
            lock (sync) {
                isInParty = true;
                partyName = info.PartyName;
                var previousMembers = new Dictionary<uint, RoPartyMemberRuntimeSnapshot>(members);
                members.Clear();
                for (int i = 0; i < info.Members.Length; i++) {
                    UpsertMemberLocked(ToRuntimeMember(info.Members[i]), previousMembers);
                }

                RefreshLeaderLocked();
            }
        }

        public void Apply(RoZcPartyMemberInfo memberInfo) {
            lock (sync) {
                isInParty = true;
                if (!string.IsNullOrWhiteSpace(memberInfo.PartyName)) {
                    partyName = memberInfo.PartyName;
                }

                if (memberInfo.HasItemRules) {
                    itemPickupRule = memberInfo.ItemPickupRule;
                    itemDivisionRule = memberInfo.ItemDivisionRule;
                }

                UpsertMemberLocked(ToRuntimeMember(memberInfo.Member));
                RefreshLeaderLocked();
            }
        }

        public void Apply(RoZcPartyMemberLeave leave, uint localAccountId = 0) {
            lock (sync) {
                if (leave.Result == 2 || leave.Result == 3) {
                    return;
                }

                if (localAccountId != 0 && leave.AccountId == localAccountId) {
                    ClearLocked();
                    return;
                }

                members.Remove(leave.AccountId);
                pendingHp.Remove(leave.AccountId);
                if (members.Count == 0) {
                    isInParty = false;
                    partyName = string.Empty;
                    leaderAccountId = 0;
                } else if (leaderAccountId == leave.AccountId) {
                    RefreshLeaderLocked();
                }
            }
        }

        public void Apply(RoZcPartyOption option) {
            lock (sync) {
                isInParty = true;
                expOption = option.ExpOption;
                if (option.HasItemRules) {
                    itemPickupRule = option.ItemPickupRule;
                    itemDivisionRule = option.ItemDivisionRule;
                }
            }
        }

        public void ApplyLocalOptions(uint newExpOption, byte newItemPickupRule, byte newItemDivisionRule) {
            lock (sync) {
                isInParty = true;
                expOption = newExpOption;
                itemPickupRule = newItemPickupRule == 0 ? (byte) 0 : (byte) 1;
                itemDivisionRule = newItemDivisionRule == 0 ? (byte) 0 : (byte) 1;
            }
        }

        public void Apply(RoZcPartyPosition position) {
            lock (sync) {
                if (members.TryGetValue(position.AccountId, out RoPartyMemberRuntimeSnapshot member)) {
                    members[position.AccountId] = member.WithPosition(position.X, position.Y);
                }
            }
        }

        public void Apply(RoZcPartyHp hp) {
            lock (sync) {
                if (members.TryGetValue(hp.AccountId, out RoPartyMemberRuntimeSnapshot member)) {
                    members[hp.AccountId] = member.WithHp(hp.Hp, hp.MaxHp);
                } else if (hp.AccountId != 0) {
                    pendingHp[hp.AccountId] = hp;
                }
            }
        }

        public void Apply(RoZcPartyAlive alive) {
            lock (sync) {
                if (members.TryGetValue(alive.AccountId, out RoPartyMemberRuntimeSnapshot member)) {
                    members[alive.AccountId] = member.WithAlive(alive.IsDead);
                }
            }
        }

        public void Apply(RoZcPartyJobAndLevel jobAndLevel) {
            lock (sync) {
                if (members.TryGetValue(jobAndLevel.AccountId, out RoPartyMemberRuntimeSnapshot member)) {
                    members[jobAndLevel.AccountId] = member.WithJobAndLevel(jobAndLevel.ClassId, jobAndLevel.BaseLevel);
                }
            }
        }

        public void Apply(RoZcPartyInvite invite) {
            lock (sync) {
                pendingInvitationId = invite.PartyId;
                pendingInvitationName = invite.PartyName;
            }
        }

        public void Apply(RoZcPartyInviteAck ack) {
            lock (sync) {
                hasLastInvitationAnswer = true;
                lastInvitationAnswerCharacterName = ack.CharacterName;
                lastInvitationAnswerCode = ack.Result;
            }
        }

        public async Task<RoSocialCommandResult> InviteAsync(string characterName, CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Party runtime is disposed.");
            }

            if (string.IsNullOrWhiteSpace(characterName)) {
                return new RoSocialCommandResult(RoSocialCommandStatus.InvalidArgument, "Character name is required.");
            }

            if (sendInvite == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Party invite is not supported by the active profile.");
            }

            return await SendSocialAsync(() => sendInvite(new RoPartyInviteRequest(characterName.Trim()), cancellationToken)).ConfigureAwait(false);
        }

        public async Task<RoSocialCommandResult> LeaveAsync(CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Party runtime is disposed.");
            }

            if (sendLeave == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Party leave is not supported by the active profile.");
            }

            return await SendSocialAsync(() => sendLeave(cancellationToken)).ConfigureAwait(false);
        }

        public async Task<RoSocialCommandResult> RespondInviteAsync(uint invitationId, bool accept, CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Party runtime is disposed.");
            }

            if (invitationId == 0) {
                return new RoSocialCommandResult(RoSocialCommandStatus.InvalidArgument, "Party invitation id is required.");
            }

            if (sendInviteResponse == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Party invite response is not supported by the active profile.");
            }

            return await SendSocialAsync(() => sendInviteResponse(new RoPartyInviteResponseRequest(invitationId, accept), cancellationToken)).ConfigureAwait(false);
        }

        public async Task<RoSocialCommandResult> CreateAsync(
            string newPartyName,
            byte newItemPickupRule = 0,
            byte newItemDivisionRule = 0,
            CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Party runtime is disposed.");
            }

            if (string.IsNullOrWhiteSpace(newPartyName)) {
                return new RoSocialCommandResult(RoSocialCommandStatus.InvalidArgument, "Party name is required.");
            }

            if (sendCreate == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Party create is not supported by the active profile.");
            }

            return await SendSocialAsync(() => sendCreate(
                new RoPartyCreateRequest(newPartyName.Trim(), newItemPickupRule, newItemDivisionRule),
                cancellationToken)).ConfigureAwait(false);
        }

        public async Task<RoSocialCommandResult> ExpelMemberAsync(
            uint accountId,
            string characterName,
            CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Party runtime is disposed.");
            }

            if (accountId == 0 || string.IsNullOrWhiteSpace(characterName)) {
                return new RoSocialCommandResult(RoSocialCommandStatus.InvalidArgument, "Party member account id and character name are required.");
            }

            if (sendExpel == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Party expel is not supported by the active profile.");
            }

            return await SendSocialAsync(() => sendExpel(new RoPartyExpelRequest(accountId, characterName.Trim()), cancellationToken)).ConfigureAwait(false);
        }

        public async Task<RoSocialCommandResult> ChangeLeaderAsync(uint accountId, CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Party runtime is disposed.");
            }

            if (accountId == 0) {
                return new RoSocialCommandResult(RoSocialCommandStatus.InvalidArgument, "Party member account id is required.");
            }

            if (sendLeaderChange == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Party leader change is not supported by the active profile.");
            }

            return await SendSocialAsync(() => sendLeaderChange(new RoPartyLeaderChangeRequest(accountId), cancellationToken)).ConfigureAwait(false);
        }

        public async Task<RoSocialCommandResult> UpdateOptionsAsync(
            uint newExpOption,
            byte newItemPickupRule,
            byte newItemDivisionRule,
            CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Party runtime is disposed.");
            }

            if (sendOptions == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Party option update is not supported by the active profile.");
            }

            RoSocialCommandResult result = await SendSocialAsync(() => sendOptions(
                new RoPartyOptionsRequest(newExpOption, newItemPickupRule, newItemDivisionRule),
                cancellationToken)).ConfigureAwait(false);
            if (result.Accepted) {
                ApplyLocalOptions(newExpOption, newItemPickupRule, newItemDivisionRule);
            }

            return result;
        }

        public void Dispose() {
            disposed = true;
            Clear();
        }

        public void Clear() {
            lock (sync) {
                ClearLocked();
            }
        }

        public void Apply(RoZcPartyCreateAck createAck) {
            lock (sync) {
                if (createAck.Accepted) {
                    isInParty = true;
                }
            }
        }

        public void Apply(RoZcPartyConfig config) {
            lock (sync) {
                isInParty = true;
                refuseJoinMessages = config.RefuseJoinMessages;
            }
        }

        public void Apply(RoZcPartyLeaderChanged leaderChanged) {
            lock (sync) {
                if (members.TryGetValue(leaderChanged.OldMasterAccountId, out RoPartyMemberRuntimeSnapshot oldLeader) &&
                    oldLeader.Role == 0) {
                    members[leaderChanged.OldMasterAccountId] = oldLeader.WithRole(1);
                }

                if (members.TryGetValue(leaderChanged.NewMasterAccountId, out RoPartyMemberRuntimeSnapshot newLeader)) {
                    members[leaderChanged.NewMasterAccountId] = newLeader.WithRole(0);
                }

                leaderAccountId = leaderChanged.NewMasterAccountId;
                isInParty = isInParty || members.Count > 0;
            }
        }

        private void UpsertMemberLocked(RoPartyMemberRuntimeSnapshot member) {
            UpsertMemberLocked(member, members);
        }

        private void UpsertMemberLocked(
            RoPartyMemberRuntimeSnapshot member,
            IReadOnlyDictionary<uint, RoPartyMemberRuntimeSnapshot> previousMembers) {
            if (member.AccountId == 0) {
                return;
            }

            if (previousMembers != null &&
                previousMembers.TryGetValue(member.AccountId, out RoPartyMemberRuntimeSnapshot previous)) {
                member = MergeMember(previous, member);
            }

            if (pendingHp.TryGetValue(member.AccountId, out RoZcPartyHp hp)) {
                member = member.WithHp(hp.Hp, hp.MaxHp);
                pendingHp.Remove(member.AccountId);
            }

            members[member.AccountId] = member;
        }

        private void ClearLocked() {
            members.Clear();
            pendingHp.Clear();
            isInParty = false;
            partyName = string.Empty;
            leaderAccountId = 0;
            refuseJoinMessages = null;
            pendingInvitationId = 0;
            pendingInvitationName = string.Empty;
            hasLastInvitationAnswer = false;
            lastInvitationAnswerCharacterName = string.Empty;
            lastInvitationAnswerCode = 0;
            expOption = 0;
            itemPickupRule = 0;
            itemDivisionRule = 0;
        }

        private void RefreshLeaderLocked() {
            leaderAccountId = 0;
            foreach (RoPartyMemberRuntimeSnapshot member in members.Values) {
                if (member.Role == 0) {
                    leaderAccountId = member.AccountId;
                    return;
                }
            }
        }

        private static RoPartyMemberRuntimeSnapshot ToRuntimeMember(RoZcPartyMember member) {
            return new RoPartyMemberRuntimeSnapshot(
                member.AccountId,
                member.CharacterId,
                member.Name,
                member.MapName,
                member.Role,
                member.State,
                member.ClassId,
                member.BaseLevel,
                // positions come only from ZC_NOTIFY_POSITION_TO_GROUPM 0x0107 (the client keeps them apart from the list)
                -1,
                -1,
                0,
                0,
                false);
        }

        private static RoPartyMemberRuntimeSnapshot MergeMember(
            RoPartyMemberRuntimeSnapshot previous,
            RoPartyMemberRuntimeSnapshot incoming) {
            return new RoPartyMemberRuntimeSnapshot(
                incoming.AccountId,
                incoming.CharacterId != 0 ? incoming.CharacterId : previous.CharacterId,
                string.IsNullOrWhiteSpace(incoming.Name) ? previous.Name : incoming.Name,
                string.IsNullOrWhiteSpace(incoming.MapName) ? previous.MapName : incoming.MapName,
                incoming.Role,
                incoming.State,
                incoming.ClassId != 0 ? incoming.ClassId : previous.ClassId,
                incoming.BaseLevel != 0 ? incoming.BaseLevel : previous.BaseLevel,
                incoming.X >= 0 ? incoming.X : previous.X,
                incoming.Y >= 0 ? incoming.Y : previous.Y,
                incoming.MaxHp > 0 ? incoming.Hp : previous.Hp,
                incoming.MaxHp > 0 ? incoming.MaxHp : previous.MaxHp,
                incoming.IsDead || previous.IsDead);
        }

        private static async Task<RoSocialCommandResult> SendSocialAsync(Func<Task> send) {
            try {
                await send().ConfigureAwait(false);
                return RoSocialCommandResult.AcceptedResult();
            } catch (RoUnsupportedPacketProfileException exception) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, exception.Message);
            } catch (Exception exception) {
                return new RoSocialCommandResult(RoSocialCommandStatus.SendFailed, exception.Message);
            }
        }
    }

    public readonly struct RoFriendRuntimeSnapshot {
        public RoFriendRuntimeSnapshot(uint accountId, uint characterId, string name, byte stateRaw, bool isOnline, bool hasPendingRequest) {
            AccountId = accountId;
            CharacterId = characterId;
            Name = name ?? string.Empty;
            StateRaw = stateRaw;
            IsOnline = isOnline;
            HasPendingRequest = hasPendingRequest;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
        public string Name { get; }
        public byte StateRaw { get; }
        public bool IsOnline { get; }
        public bool HasPendingRequest { get; }

        public RoFriendRuntimeSnapshot WithState(byte stateRaw, string name = null) {
            return new RoFriendRuntimeSnapshot(AccountId, CharacterId, name ?? Name, stateRaw, RoFriendsRuntime.IsOnlineState(stateRaw), HasPendingRequest);
        }
    }

    public sealed class RoFriendsRuntime : IDisposable {
        private static readonly Comparison<RoFriendRuntimeSnapshot> FriendNameComparison = CompareFriendSnapshotsByName;
        private readonly object sync = new object();
        private readonly Dictionary<ulong, RoFriendRuntimeSnapshot> friends = new Dictionary<ulong, RoFriendRuntimeSnapshot>();
        private readonly Func<RoFriendNameRequest, CancellationToken, Task> sendAdd;
        private readonly Func<RoFriendIdRequest, CancellationToken, Task> sendRemove;
        private readonly Func<RoFriendResponseRequest, CancellationToken, Task> sendResponse;
        private bool disposed;

        public RoFriendsRuntime(
            Func<RoFriendNameRequest, CancellationToken, Task> sendAdd,
            Func<RoFriendIdRequest, CancellationToken, Task> sendRemove,
            Func<RoFriendResponseRequest, CancellationToken, Task> sendResponse) {
            this.sendAdd = sendAdd;
            this.sendRemove = sendRemove;
            this.sendResponse = sendResponse;
        }

        public int FriendCount {
            get {
                lock (sync) {
                    return friends.Count;
                }
            }
        }

        public RoFriendRuntimeSnapshot[] SnapshotFriends() {
            lock (sync) {
                var values = new RoFriendRuntimeSnapshot[friends.Count];
                friends.Values.CopyTo(values, 0);
                Array.Sort(values, FriendNameComparison);
                return values;
            }
        }

        public void SnapshotFriends(List<RoFriendRuntimeSnapshot> output) {
            if (output == null) {
                throw new ArgumentNullException(nameof(output));
            }

            lock (sync) {
                output.Clear();
                foreach (RoFriendRuntimeSnapshot friend in friends.Values) {
                    output.Add(friend);
                }

                output.Sort(FriendNameComparison);
            }
        }

        private static int CompareFriendSnapshotsByName(RoFriendRuntimeSnapshot left, RoFriendRuntimeSnapshot right) {
            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        public void Apply(RoZcFriendsList list) {
            lock (sync) {
                friends.Clear();
                for (int i = 0; i < list.Friends.Length; i++) {
                    RoZcFriendEntry friend = list.Friends[i];
                    friends[Key(friend.AccountId, friend.CharacterId)] = ToRuntime(friend, hasPendingRequest: false);
                }
            }
        }

        public void Apply(RoZcFriendState state) {
            lock (sync) {
                ulong key = Key(state.AccountId, state.CharacterId);
                if (friends.TryGetValue(key, out RoFriendRuntimeSnapshot friend)) {
                    friends[key] = friend.WithState(state.StateRaw, string.IsNullOrWhiteSpace(state.Name) ? null : state.Name);
                } else {
                    friends[key] = new RoFriendRuntimeSnapshot(state.AccountId, state.CharacterId, state.Name, state.StateRaw, IsOnlineState(state.StateRaw), false);
                }
            }
        }

        /// <summary>A ZC_ACK_REQNAME_BYGID answer: names the friend with that GID (lists without names).</summary>
        public bool ApplyName(uint characterId, string name) {
            if (characterId == 0 || string.IsNullOrWhiteSpace(name)) {
                return false;
            }

            lock (sync) {
                foreach (KeyValuePair<ulong, RoFriendRuntimeSnapshot> pair in friends) {
                    if (pair.Value.CharacterId == characterId) {
                        friends[pair.Key] = pair.Value.WithState(pair.Value.StateRaw, name);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The GIDs of the friends the list gave no name.</summary>
        public void CollectNameless(List<uint> output) {
            lock (sync) {
                foreach (RoFriendRuntimeSnapshot friend in friends.Values) {
                    if (string.IsNullOrWhiteSpace(friend.Name) && friend.CharacterId != 0) {
                        output.Add(friend.CharacterId);
                    }
                }
            }
        }

        public void Apply(RoZcFriendRequest request) {
            lock (sync) {
                friends[Key(request.AccountId, request.CharacterId)] = new RoFriendRuntimeSnapshot(
                    request.AccountId,
                    request.CharacterId,
                    request.Name,
                    1,
                    false,
                    true);
            }
        }

        public void Apply(RoZcFriendAddResult result) {
            lock (sync) {
                if (result.IsSuccess) {
                    friends[Key(result.AccountId, result.CharacterId)] = new RoFriendRuntimeSnapshot(
                        result.AccountId,
                        result.CharacterId,
                        result.Name,
                        0,
                        true,
                        false);
                }
            }
        }

        public void Apply(RoZcFriendDelete delete) {
            lock (sync) {
                friends.Remove(Key(delete.AccountId, delete.CharacterId));
            }
        }

        public bool ClearPendingRequest(uint accountId, uint characterId) {
            lock (sync) {
                ulong key = Key(accountId, characterId);
                if (!friends.TryGetValue(key, out RoFriendRuntimeSnapshot friend) || !friend.HasPendingRequest) {
                    return false;
                }

                friends.Remove(key);
                return true;
            }
        }

        public async Task<RoSocialCommandResult> AddFriendAsync(string characterName, CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Friends runtime is disposed.");
            }

            if (string.IsNullOrWhiteSpace(characterName)) {
                return new RoSocialCommandResult(RoSocialCommandStatus.InvalidArgument, "Character name is required.");
            }

            if (sendAdd == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Friend add is not supported by the active profile.");
            }

            return await SendSocialAsync(() => sendAdd(new RoFriendNameRequest(characterName.Trim()), cancellationToken)).ConfigureAwait(false);
        }

        public async Task<RoSocialCommandResult> RemoveFriendAsync(uint accountId, uint characterId, CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Friends runtime is disposed.");
            }

            if (accountId == 0 || characterId == 0) {
                return new RoSocialCommandResult(RoSocialCommandStatus.InvalidArgument, "Friend account and character ids are required.");
            }

            if (sendRemove == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Friend remove is not supported by the active profile.");
            }

            return await SendSocialAsync(() => sendRemove(new RoFriendIdRequest(accountId, characterId), cancellationToken)).ConfigureAwait(false);
        }

        public async Task<RoSocialCommandResult> RespondFriendRequestAsync(uint accountId, uint characterId, bool accept, CancellationToken cancellationToken = default) {
            if (disposed) {
                return new RoSocialCommandResult(RoSocialCommandStatus.Disposed, "Friends runtime is disposed.");
            }

            if (accountId == 0 || characterId == 0) {
                return new RoSocialCommandResult(RoSocialCommandStatus.InvalidArgument, "Friend account and character ids are required.");
            }

            if (sendResponse == null) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, "Friend request response is not supported by the active profile.");
            }

            RoSocialCommandResult result = await SendSocialAsync(() => sendResponse(new RoFriendResponseRequest(accountId, characterId, accept), cancellationToken)).ConfigureAwait(false);
            if (result.Accepted) {
                ClearPendingRequest(accountId, characterId);
            }

            return result;
        }

        public void Dispose() {
            disposed = true;
            Clear();
        }

        public void Clear() {
            lock (sync) {
                friends.Clear();
            }
        }

        private static RoFriendRuntimeSnapshot ToRuntime(RoZcFriendEntry friend, bool hasPendingRequest) {
            return new RoFriendRuntimeSnapshot(
                friend.AccountId,
                friend.CharacterId,
                friend.Name,
                friend.StateRaw,
                IsOnlineState(friend.StateRaw),
                hasPendingRequest);
        }

        internal static bool IsOnlineState(byte stateRaw) => stateRaw == 0;

        private static ulong Key(uint accountId, uint characterId) {
            return ((ulong) accountId << 32) | characterId;
        }

        private static async Task<RoSocialCommandResult> SendSocialAsync(Func<Task> send) {
            try {
                await send().ConfigureAwait(false);
                return RoSocialCommandResult.AcceptedResult();
            } catch (RoUnsupportedPacketProfileException exception) {
                return new RoSocialCommandResult(RoSocialCommandStatus.UnsupportedProfile, exception.Message);
            } catch (Exception exception) {
                return new RoSocialCommandResult(RoSocialCommandStatus.SendFailed, exception.Message);
            }
        }
    }
}
