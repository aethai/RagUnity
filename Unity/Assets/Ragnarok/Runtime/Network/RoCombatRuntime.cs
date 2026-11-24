using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public enum RoCombatTargetStatus {
        Selected = 0,
        NoActor = 1,
        SelfTargetInvalid = 2,
        Invisible = 3,
        Dead = 4
    }

    public enum RoCombatCommandStatus {
        Sent = 0,
        NoTarget = 1,
        InvalidTarget = 2,
        SelfDead = 3,
        Throttled = 4,
        AutoAttackInactive = 5
    }

    public enum RoCombatDamageFeedbackKind {
        Damage = 0,
        Miss = 1,
        Critical = 2,
        Heal = 3,
        Sp = 4
    }

    public readonly struct RoCombatAttackRequest {
        public RoCombatAttackRequest(uint targetActorId, byte actionType) {
            TargetActorId = targetActorId;
            ActionType = actionType;
        }

        public uint TargetActorId { get; }
        public byte ActionType { get; }
    }

    public readonly struct RoCombatTargetResult {
        public RoCombatTargetResult(RoCombatTargetStatus status, uint actorId, string message) {
            Status = status;
            ActorId = actorId;
            Message = message ?? string.Empty;
        }

        public RoCombatTargetStatus Status { get; }
        public uint ActorId { get; }
        public string Message { get; }
        public bool Accepted => Status == RoCombatTargetStatus.Selected;
    }

    public readonly struct RoCombatCommandResult {
        public RoCombatCommandResult(RoCombatCommandStatus status, uint targetActorId, string message) {
            Status = status;
            TargetActorId = targetActorId;
            Message = message ?? string.Empty;
        }

        public RoCombatCommandStatus Status { get; }
        public uint TargetActorId { get; }
        public string Message { get; }
        public bool Sent => Status == RoCombatCommandStatus.Sent;
    }

    public readonly struct RoCombatVitalsSnapshot {
        internal RoCombatVitalsSnapshot(int hp, int maxHp, int sp, int maxSp) {
            Hp = hp;
            MaxHp = maxHp;
            Sp = sp;
            MaxSp = maxSp;
        }

        public int Hp { get; }
        public int MaxHp { get; }
        public int Sp { get; }
        public int MaxSp { get; }
        public bool IsDead => MaxHp > 0 && Hp <= 0;
    }

    public readonly struct RoCombatDamageEvent {
        internal RoCombatDamageEvent(
            uint sourceActorId,
            uint targetActorId,
            int amount,
            int secondaryAmount,
            ushort hitCount,
            byte actionType,
            uint serverTick,
            bool isSpDamage) {
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            Amount = amount;
            SecondaryAmount = secondaryAmount;
            HitCount = hitCount;
            ActionType = actionType;
            ServerTick = serverTick;
            IsSpDamage = isSpDamage;
        }

        public uint SourceActorId { get; }
        public uint TargetActorId { get; }
        public int Amount { get; }
        public int SecondaryAmount { get; }
        public ushort HitCount { get; }
        public byte ActionType { get; }
        public uint ServerTick { get; }
        public bool IsSpDamage { get; }
        public bool IsMiss => Amount == 0 && SecondaryAmount == 0;
        public bool IsCritical => ActionType == (byte) RoMapActorActionType.Critical ||
            ActionType == (byte) RoMapActorActionType.MultiHitCritical;
        public RoCombatDamageFeedbackKind FeedbackKind {
            get {
                if (IsSpDamage) {
                    return RoCombatDamageFeedbackKind.Sp;
                }

                if (Amount < 0 || SecondaryAmount < 0) {
                    return RoCombatDamageFeedbackKind.Heal;
                }

                if (IsCritical) {
                    return RoCombatDamageFeedbackKind.Critical;
                }

                return IsMiss ? RoCombatDamageFeedbackKind.Miss : RoCombatDamageFeedbackKind.Damage;
            }
        }
    }

    public readonly struct RoCombatStatusIconSnapshot {
        internal RoCombatStatusIconSnapshot(
            uint actorId,
            ushort statusId,
            bool active,
            uint totalMilliseconds,
            uint remainingMilliseconds,
            int value1,
            int value2,
            int value3) {
            ActorId = actorId;
            StatusId = statusId;
            Active = active;
            TotalMilliseconds = totalMilliseconds;
            RemainingMilliseconds = remainingMilliseconds;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
        }

        public uint ActorId { get; }
        public ushort StatusId { get; }
        public bool Active { get; }
        public uint TotalMilliseconds { get; }
        public uint RemainingMilliseconds { get; }
        public int Value1 { get; }
        public int Value2 { get; }
        public int Value3 { get; }
    }

    public sealed class RoCombatRuntimeConfig {
        private static readonly long StopwatchStartTimestamp = Stopwatch.GetTimestamp();

        public RoCombatRuntimeConfig(
            TimeSpan? attackInterval = null,
            Func<long> clockMilliseconds = null) {
            AttackInterval = attackInterval ?? TimeSpan.FromMilliseconds(500);
            ClockMilliseconds = clockMilliseconds ?? DefaultMonotonicMilliseconds;

            if (AttackInterval < TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(attackInterval));
            }
        }

        public static RoCombatRuntimeConfig Default { get; } = new RoCombatRuntimeConfig();

        public TimeSpan AttackInterval { get; }
        public Func<long> ClockMilliseconds { get; }

        private static long DefaultMonotonicMilliseconds() {
            long elapsedTicks = Stopwatch.GetTimestamp() - StopwatchStartTimestamp;
            return (long) (elapsedTicks * 1000.0 / Stopwatch.Frequency);
        }
    }
    internal readonly struct RoCombatStatusIconKey : IEquatable<RoCombatStatusIconKey> {
        public RoCombatStatusIconKey(uint actorId, ushort statusId) {
            ActorId = actorId;
            StatusId = statusId;
        }

        public uint ActorId { get; }
        public ushort StatusId { get; }

        public bool Equals(RoCombatStatusIconKey other) {
            return ActorId == other.ActorId && StatusId == other.StatusId;
        }

        public override bool Equals(object obj) {
            return obj is RoCombatStatusIconKey other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                return ((int) ActorId * 397) ^ StatusId;
            }
        }
    }
}
