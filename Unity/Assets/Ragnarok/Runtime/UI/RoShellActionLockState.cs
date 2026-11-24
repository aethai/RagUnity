using System;
using Ragnarok.Gameplay;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal readonly struct RoLocalActionLockRequest {
        public RoLocalActionLockRequest(RoGameplayActionLockKind kind, int fallbackMilliseconds, string reason) {
            Kind = kind;
            FallbackMilliseconds = fallbackMilliseconds;
            Reason = reason ?? string.Empty;
        }

        public RoGameplayActionLockKind Kind { get; }
        public int FallbackMilliseconds { get; }
        public string Reason { get; }
        public bool IsActive => Kind != RoGameplayActionLockKind.None;

        public static RoLocalActionLockRequest None => default;
    }

    internal sealed class RoShellActionLockState {
        private DateTime actionLockUntilUtc = DateTime.MinValue;
        private string actionLockReason = string.Empty;

        public string Reason => actionLockReason ?? string.Empty;

        public int GetRemainingMilliseconds(DateTime nowUtc) {
            if (actionLockUntilUtc <= nowUtc) {
                if (actionLockUntilUtc != DateTime.MinValue) {
                    Clear();
                }

                return 0;
            }

            double remaining = (actionLockUntilUtc - nowUtc).TotalMilliseconds;
            if (remaining >= int.MaxValue) {
                return int.MaxValue;
            }

            return Math.Max(1, (int) Math.Ceiling(remaining));
        }

        public void Apply(
            RoMapEntryDrainResult drain,
            uint localActorId,
            RoLocalActionLockRequest request,
            DateTime requestStartedUtc) {
            if (!request.IsActive && (drain?.OrderedEvents == null || drain.OrderedEvents.Length == 0)) {
                return;
            }

            RoGameplayActionLockSpec spec = RoGameplayActionLockResolver.ResolveFromDrain(
                drain,
                localActorId,
                request.Kind,
                request.FallbackMilliseconds,
                request.Reason);
            if (!spec.IsActive) {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            int elapsedMilliseconds = Math.Max(0, (int) Math.Min(int.MaxValue, (nowUtc - requestStartedUtc).TotalMilliseconds));
            int remainingMilliseconds = Math.Max(0, spec.DurationMilliseconds - elapsedMilliseconds);
            if (remainingMilliseconds <= 0) {
                return;
            }

            DateTime nextUntilUtc = nowUtc.AddMilliseconds(remainingMilliseconds);
            if (nextUntilUtc > actionLockUntilUtc) {
                actionLockUntilUtc = nextUntilUtc;
                actionLockReason = spec.Reason;
            }
        }

        public void Clear() {
            actionLockUntilUtc = DateTime.MinValue;
            actionLockReason = string.Empty;
        }

        public void ThrowIfLocked(DateTime nowUtc) {
            int remainingMilliseconds = GetRemainingMilliseconds(nowUtc);
            if (remainingMilliseconds <= 0) {
                return;
            }

            string reason = string.IsNullOrWhiteSpace(actionLockReason)
                ? "combat action"
                : actionLockReason;
            throw new InvalidOperationException($"{reason} is locked for {remainingMilliseconds} ms.");
        }
    }
}
