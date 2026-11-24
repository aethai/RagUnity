using System;
using Ragnarok.Network;

namespace Ragnarok.Gameplay {
    public enum RoGameplayActionLockKind {
        None = 0,
        Attack = 1,
        Skill = 2,
        Pickup = 3
    }

    public readonly struct RoGameplayActionLockSpec {
        public static readonly RoGameplayActionLockSpec None = new RoGameplayActionLockSpec(
            RoGameplayActionLockKind.None,
            0,
            string.Empty);

        public RoGameplayActionLockSpec(RoGameplayActionLockKind kind, int durationMilliseconds, string reason) {
            Kind = durationMilliseconds > 0 ? kind : RoGameplayActionLockKind.None;
            DurationMilliseconds = Math.Max(0, durationMilliseconds);
            Reason = reason ?? string.Empty;
        }

        public RoGameplayActionLockKind Kind { get; }
        public int DurationMilliseconds { get; }
        public string Reason { get; }
        public bool IsActive => Kind != RoGameplayActionLockKind.None && DurationMilliseconds > 0;
    }

    public static class RoGameplayActionLockResolver {
        public const int MinimumAttackLockMilliseconds = 250;
        public const int MinimumSkillLockMilliseconds = 250;
        public const int MinimumPickupLockMilliseconds = 250;
        public const int DefaultAttackFallbackMilliseconds = 450;
        public const int DefaultSkillFallbackMilliseconds = 350;
        public const int DefaultPickupFallbackMilliseconds = 450;
        public const int MaximumActionLockMilliseconds = 5000;

        public static RoGameplayActionLockSpec ResolveFromDrain(
            RoMapEntryDrainResult drain,
            uint localActorId,
            RoGameplayActionLockKind fallbackKind,
            int fallbackMilliseconds = 0,
            string fallbackReason = null) {
            RoGameplayActionLockKind kind = RoGameplayActionLockKind.None;
            int duration = 0;
            string reason = string.Empty;

            if (drain?.OrderedEvents != null) {
                for (int i = 0; i < drain.OrderedEvents.Length; i++) {
                    ConsiderEvent(drain.OrderedEvents[i], localActorId, ref kind, ref duration, ref reason);
                }
            }

            if (duration <= 0 && fallbackKind != RoGameplayActionLockKind.None) {
                kind = fallbackKind;
                duration = fallbackMilliseconds > 0
                    ? fallbackMilliseconds
                    : DefaultDurationForKind(fallbackKind);
                reason = fallbackReason ?? string.Empty;
            }

            if (duration <= 0 || kind == RoGameplayActionLockKind.None) {
                return RoGameplayActionLockSpec.None;
            }

            return new RoGameplayActionLockSpec(kind, ClampDuration(kind, duration), reason);
        }

        private static void ConsiderEvent(
            RoMapEntryDrainEvent drainEvent,
            uint localActorId,
            ref RoGameplayActionLockKind kind,
            ref int duration,
            ref string reason) {
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.ActorAction:
                    if (IsLocalActor(localActorId, drainEvent.ActorAction.SourceId)) {
                        RoGameplayActionLockKind sourceKind = ResolveActorActionSourceLockKind(drainEvent.ActorAction.ActionRaw);
                        if (sourceKind != RoGameplayActionLockKind.None) {
                            int sourceDuration = sourceKind == RoGameplayActionLockKind.Attack
                                ? RoMapBasicAttackTimingSemantics.ResolveSourceAttackDurationMilliseconds(
                                    drainEvent.ActorAction,
                                    RoMapActorObjectType.Unknown,
                                    0,
                                    0,
                                    0)
                                : DefaultDurationForKind(sourceKind);
                            ConsiderCandidate(
                                sourceKind,
                                sourceDuration,
                                sourceKind == RoGameplayActionLockKind.Pickup ? "pickup motion" : "attack motion",
                                ref kind,
                                ref duration,
                                ref reason);
                        }
                    }

                    if (IsLocalActor(localActorId, drainEvent.ActorAction.TargetId) &&
                        ShouldApplyTargetHitLock(drainEvent.ActorAction)) {
                        ConsiderCandidate(
                            RoGameplayActionLockKind.Attack,
                            RoMapBasicAttackTimingSemantics.ResolveTargetHitDurationMilliseconds(drainEvent.ActorAction),
                            "hit motion",
                            ref kind,
                            ref duration,
                            ref reason);
                    }

                    break;
                case RoMapEntryDrainEventKind.SkillCast:
                    if (IsLocalActor(localActorId, drainEvent.SkillCast.SourceId)) {
                        ConsiderCandidate(
                            RoGameplayActionLockKind.Skill,
                            SafeMax(drainEvent.SkillCast.DelayTime, drainEvent.SkillCast.AttackMotionTime),
                            $"skill {drainEvent.SkillCast.SkillId} cast",
                            ref kind,
                            ref duration,
                            ref reason);
                    }

                    break;
                case RoMapEntryDrainEventKind.SkillDamage:
                    if (IsLocalActor(localActorId, drainEvent.SkillDamage.SourceId)) {
                        ConsiderCandidate(
                            RoGameplayActionLockKind.Skill,
                            Math.Max(drainEvent.SkillDamage.SourceDelay, 0),
                            $"skill {drainEvent.SkillDamage.SkillId} damage",
                            ref kind,
                            ref duration,
                            ref reason);
                    }

                    break;
                case RoMapEntryDrainEventKind.SkillPosition:
                    if (IsLocalActor(localActorId, drainEvent.SkillPosition.SourceId)) {
                        ConsiderCandidate(
                            RoGameplayActionLockKind.Skill,
                            Math.Max(drainEvent.SkillPosition.SourceDelay, 0),
                            $"skill {drainEvent.SkillPosition.SkillId} position",
                            ref kind,
                            ref duration,
                            ref reason);
                    }

                    break;
                case RoMapEntryDrainEventKind.SkillPostDelay:
                    ConsiderCandidate(
                        RoGameplayActionLockKind.Skill,
                        SafeInt(drainEvent.SkillPostDelay.DelayTime),
                        $"skill {drainEvent.SkillPostDelay.SkillId} post-delay",
                        ref kind,
                        ref duration,
                        ref reason);
                    break;
            }
        }

        private static bool IsLocalActor(uint localActorId, uint actorId) {
            return actorId != 0 && (localActorId == 0 || localActorId == actorId);
        }

        private static RoGameplayActionLockKind ResolveActorActionSourceLockKind(byte actionRaw) {
            if (RoMapActorActionSemantics.IsPickup(actionRaw)) {
                return RoGameplayActionLockKind.Pickup;
            }

            return RoMapActorActionSemantics.IsDamage(actionRaw)
                ? RoGameplayActionLockKind.Attack
                : RoGameplayActionLockKind.None;
        }

        private static bool ShouldApplyTargetHitLock(RoZcNotifyAct action) {
            return RoMapActorActionSemantics.IsDamage(action.ActionRaw) &&
                (action.Damage != 0 || action.Damage2 != 0) &&
                action.ActionRaw != (byte) RoMapActorActionType.Absorbed &&
                action.ActionRaw != (byte) RoMapActorActionType.Endure &&
                action.ActionRaw != (byte) RoMapActorActionType.LuckyDodge;
        }

        private static void ConsiderCandidate(
            RoGameplayActionLockKind candidateKind,
            int candidateDuration,
            string candidateReason,
            ref RoGameplayActionLockKind kind,
            ref int duration,
            ref string reason) {
            if (candidateKind == RoGameplayActionLockKind.None || candidateDuration <= 0) {
                return;
            }

            int clamped = ClampDuration(candidateKind, candidateDuration);
            if (clamped <= duration) {
                return;
            }

            kind = candidateKind;
            duration = clamped;
            reason = candidateReason ?? string.Empty;
        }

        private static int ClampDuration(RoGameplayActionLockKind kind, int duration) {
            int minimum = kind switch {
                RoGameplayActionLockKind.Attack => MinimumAttackLockMilliseconds,
                RoGameplayActionLockKind.Pickup => MinimumPickupLockMilliseconds,
                RoGameplayActionLockKind.Skill => MinimumSkillLockMilliseconds,
                _ => 0
            };
            return Math.Min(Math.Max(duration, minimum), MaximumActionLockMilliseconds);
        }

        private static int DefaultDurationForKind(RoGameplayActionLockKind kind) {
            return kind switch {
                RoGameplayActionLockKind.Attack => DefaultAttackFallbackMilliseconds,
                RoGameplayActionLockKind.Pickup => DefaultPickupFallbackMilliseconds,
                RoGameplayActionLockKind.Skill => DefaultSkillFallbackMilliseconds,
                _ => 0
            };
        }

        private static int SafeMax(uint left, uint right) {
            return Math.Max(SafeInt(left), SafeInt(right));
        }

        private static int SafeInt(uint value) {
            return value > int.MaxValue ? int.MaxValue : (int) value;
        }
    }
}
