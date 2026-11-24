using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public enum RoAdvancedCommandStatus {
        Accepted = 0,
        InactiveMapSession = 1,
        InvalidArgument = 2,
        InvalidState = 3,
        UnsupportedProfile = 4,
        SendFailed = 5
    }

    public readonly struct RoAdvancedCommandResult {
        public RoAdvancedCommandResult(RoAdvancedCommandStatus status, string diagnostic = "") {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RoAdvancedCommandStatus Status { get; }
        public string Diagnostic { get; }
        public bool IsAccepted => Status == RoAdvancedCommandStatus.Accepted;

        public static RoAdvancedCommandResult Accepted() {
            return new RoAdvancedCommandResult(RoAdvancedCommandStatus.Accepted);
        }

        public static RoAdvancedCommandResult Inactive() {
            return new RoAdvancedCommandResult(RoAdvancedCommandStatus.InactiveMapSession, "Map runtime session is not active.");
        }

        public static RoAdvancedCommandResult Unsupported(string diagnostic) {
            return new RoAdvancedCommandResult(RoAdvancedCommandStatus.UnsupportedProfile, diagnostic);
        }
    }

    public readonly struct RoGuildChatRequest {
        public RoGuildChatRequest(string message) {
            Message = message ?? string.Empty;
        }

        public string Message { get; }
    }

    public readonly struct RoGuildSnapshot {
        internal RoGuildSnapshot(
            uint guildId,
            string guildName,
            int emblemVersion,
            int rights,
            bool isMaster,
            RoGuildMemberSnapshot[] members,
            RoZcGuildInvite pendingInvite,
            RoZcGuildInviteResult lastInviteResult,
            RoZcGuildLeaveResult lastLeaveResult,
            RoZcGuildNotice lastNotice,
            string lastMessage) {
            GuildId = guildId;
            GuildName = guildName ?? string.Empty;
            EmblemVersion = emblemVersion;
            Rights = rights;
            IsMaster = isMaster;
            Members = members ?? Array.Empty<RoGuildMemberSnapshot>();
            PendingInvite = pendingInvite;
            LastInviteResult = lastInviteResult;
            LastLeaveResult = lastLeaveResult;
            LastNotice = lastNotice;
            LastMessage = lastMessage ?? string.Empty;
        }

        public uint GuildId { get; }
        public string GuildName { get; }
        public int EmblemVersion { get; }
        public int Rights { get; }
        public bool IsMaster { get; }
        public RoGuildMemberSnapshot[] Members { get; }
        public RoZcGuildInvite PendingInvite { get; }
        public RoZcGuildInviteResult LastInviteResult { get; }
        public RoZcGuildLeaveResult LastLeaveResult { get; }
        public RoZcGuildNotice LastNotice { get; }
        public string LastMessage { get; }
        public bool HasGuild => GuildId != 0;
        public bool HasPendingInvite => PendingInvite.GuildId != 0;
    }

    public readonly struct RoGuildMemberSnapshot {
        internal RoGuildMemberSnapshot(RoZcGuildMemberInfo info) {
            AccountId = info.AccountId;
            CharacterId = info.CharacterId;
            Name = info.CharacterName;
            Job = info.Job;
            Level = info.Level;
            PositionId = info.PositionId;
            CurrentState = info.CurrentState;
            Memo = info.Memo;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
        public string Name { get; }
        public short Job { get; }
        public short Level { get; }
        public int PositionId { get; }
        public int CurrentState { get; }
        public string Memo { get; }
    }
    public readonly struct RoStoreSnapshot {
        public RoStoreSnapshot(bool vendingFoundationReady, bool buyingStoreFoundationReady, string lastMessage) {
            VendingFoundationReady = vendingFoundationReady;
            BuyingStoreFoundationReady = buyingStoreFoundationReady;
            LastMessage = lastMessage ?? string.Empty;
        }

        public bool VendingFoundationReady { get; }
        public bool BuyingStoreFoundationReady { get; }
        public string LastMessage { get; }
    }
    public readonly struct RoPetSnapshot {
        internal RoPetSnapshot(RoZcPetProperty property, uint actorId, RoZcPetFeedResult lastFeedResult, RoZcPetAction lastAction, string lastMessage) {
            Property = property;
            ActorId = actorId;
            LastFeedResult = lastFeedResult;
            LastAction = lastAction;
            LastMessage = lastMessage ?? string.Empty;
        }

        public RoZcPetProperty Property { get; }
        public uint ActorId { get; }
        public RoZcPetFeedResult LastFeedResult { get; }
        public RoZcPetAction LastAction { get; }
        public string LastMessage { get; }
        public bool IsActive => ActorId != 0 || !string.IsNullOrEmpty(Property.Name);
    }
    public readonly struct RoCompanionSnapshot {
        internal RoCompanionSnapshot(bool active, uint actorId, string name, short level, int hp, int maxHp, int sp, int maxSp, short hunger, short intimacy, RoZcSkillInfo[] skills, string lastMessage) {
            Active = active;
            ActorId = actorId;
            Name = name ?? string.Empty;
            Level = level;
            Hp = hp;
            MaxHp = maxHp;
            Sp = sp;
            MaxSp = maxSp;
            Hunger = hunger;
            Intimacy = intimacy;
            Skills = skills ?? Array.Empty<RoZcSkillInfo>();
            LastMessage = lastMessage ?? string.Empty;
        }

        public bool Active { get; }
        public uint ActorId { get; }
        public string Name { get; }
        public short Level { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Sp { get; }
        public int MaxSp { get; }
        public short Hunger { get; }
        public short Intimacy { get; }
        public RoZcSkillInfo[] Skills { get; }
        public string LastMessage { get; }

        public static RoCompanionSnapshot Empty() {
            return new RoCompanionSnapshot(false, 0, string.Empty, 0, 0, 0, 0, 0, 0, 0, Array.Empty<RoZcSkillInfo>(), string.Empty);
        }
    }
    public readonly struct RoMailSnapshot {
        internal RoMailSnapshot(bool? hasUnreadRodexMail, int rodexIconUpdateCount, string lastMessage) {
            HasUnreadRodexMail = hasUnreadRodexMail;
            RodexIconUpdateCount = rodexIconUpdateCount;
            LastMessage = lastMessage ?? string.Empty;
        }

        public bool? HasUnreadRodexMail { get; }
        public int RodexIconUpdateCount { get; }
        public string LastMessage { get; }
    }

    public sealed class RoMailRuntime : IDisposable {
        private readonly object sync = new object();
        private bool disposed;
        private bool? hasUnreadRodexMail;
        private int rodexIconUpdateCount;
        private string lastMessage = string.Empty;

        public event EventHandler Changed;

        public void Apply(RoZcNotifyUnreadMail packet) {
            bool changed;
            lock (sync) {
                if (disposed) {
                    return;
                }

                hasUnreadRodexMail = packet.HasUnreadRodexMail;
                rodexIconUpdateCount++;
                lastMessage = packet.HasUnreadRodexMail
                    ? "RODEX unread mail notification received."
                    : "RODEX unread mail notification cleared.";
                changed = true;
            }

            if (changed) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public RoMailSnapshot Snapshot() {
            lock (sync) {
                return new RoMailSnapshot(hasUnreadRodexMail, rodexIconUpdateCount, lastMessage);
            }
        }

        public void Dispose() {
            lock (sync) {
                disposed = true;
                hasUnreadRodexMail = null;
                rodexIconUpdateCount = 0;
                lastMessage = string.Empty;
            }
        }
    }

    public readonly struct RoAchievementEntrySnapshot {
        internal RoAchievementEntrySnapshot(RoZcAchievementEntry entry) {
            Id = entry.Id;
            IsCompleted = entry.IsCompleted;
            Objectives = CopyObjectives(entry.Objectives);
            CompletedAt = entry.CompletedAt;
            RewardClaimed = entry.RewardClaimed;
        }

        public uint Id { get; }
        public bool IsCompleted { get; }
        public uint[] Objectives { get; }
        public uint? CompletedAt { get; }
        public bool RewardClaimed { get; }

        private static uint[] CopyObjectives(uint[] source) {
            var copy = new uint[RoMapPacketParser.AchievementObjectiveCount];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoAchievementSnapshot {
        internal RoAchievementSnapshot(
            uint totalAchievements,
            uint totalPoints,
            ushort rank,
            uint currentRankPoints,
            uint nextRankPoints,
            RoAchievementEntrySnapshot[] entries,
            string lastMessage) {
            TotalAchievements = totalAchievements;
            TotalPoints = totalPoints;
            Rank = rank;
            CurrentRankPoints = currentRankPoints;
            NextRankPoints = nextRankPoints;
            Entries = entries ?? Array.Empty<RoAchievementEntrySnapshot>();
            LastMessage = lastMessage ?? string.Empty;
        }

        public uint TotalAchievements { get; }
        public uint TotalPoints { get; }
        public ushort Rank { get; }
        public uint CurrentRankPoints { get; }
        public uint NextRankPoints { get; }
        public RoAchievementEntrySnapshot[] Entries { get; }
        public string LastMessage { get; }
    }
    public readonly struct RoWoeSnapshot {
        internal RoWoeSnapshot(short propertyType, uint flags, int updateCount) {
            PropertyType = propertyType;
            Flags = flags;
            UpdateCount = updateCount;
        }

        public short PropertyType { get; }
        public uint Flags { get; }
        public int UpdateCount { get; }
        public bool IsFreePvpZone => PropertyType == 1;
        public bool IsEventPvpZone => PropertyType == 2;
        public bool IsAgitZone => PropertyType == 3;
        public bool IsGuildZone => (Flags & (1u << 1)) != 0;
        public bool IsSiegeZone => (Flags & (1u << 2)) != 0;
        public bool IsNoLockOn => (Flags & (1u << 4)) != 0;
        public bool IsCountPk => (Flags & (1u << 5)) != 0;
        public bool IsBattlefield => (Flags & (1u << 7)) != 0;
    }
}
