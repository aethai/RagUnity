namespace Ragnarok.UI {
    public enum RoReconnectState {
        Stable = 0,
        ConnectionLost = 1,
        Reconnecting = 2,
        Failed = 3
    }

    public sealed class RoReconnectPolicy {
        public const long AutoReconnectDelayMilliseconds = 2000;
        public const long AutoReconnectAttemptWindowMilliseconds = 30000;
        public const long StableAttemptResetMilliseconds = 60000;

        public RoReconnectState State { get; private set; } = RoReconnectState.Stable;
        public string Reason { get; private set; } = string.Empty;

        private bool hasConnectionLostAt;
        private long connectionLostAtMs;
        private bool hasAutoAttemptWindow;
        private long autoAttemptWindowStartedAtMs;
        private int autoAttemptsInWindow;
        private bool hasStableSince;
        private long stableSinceMs;

        public bool CanManualReconnect =>
            State == RoReconnectState.ConnectionLost ||
            State == RoReconnectState.Failed;

        public bool IsAutoReconnectDue(long nowMs) {
            return State == RoReconnectState.ConnectionLost &&
                hasConnectionLostAt &&
                nowMs - connectionLostAtMs >= AutoReconnectDelayMilliseconds;
        }

        public void MarkStable(long nowMs) {
            // the shell marks every MapEntered frame stable: a lost or failed connection must survive that until a
            // reconnect (or the return to the login) starts, else its prompt never shows
            if (State == RoReconnectState.ConnectionLost || State == RoReconnectState.Failed) {
                return;
            }

            if (State != RoReconnectState.Stable || !hasStableSince) {
                hasStableSince = true;
                stableSinceMs = nowMs;
            }

            State = RoReconnectState.Stable;
            Reason = string.Empty;
            hasConnectionLostAt = false;
            if (hasStableSince && nowMs - stableSinceMs >= StableAttemptResetMilliseconds) {
                ResetAutoAttemptWindow();
                stableSinceMs = nowMs;
            }
        }

        public void MarkConnectionLost(string reason, long nowMs) {
            if (State == RoReconnectState.ConnectionLost) {
                return;
            }

            ResetExpiredAutoAttemptWindow(nowMs);
            State = RoReconnectState.ConnectionLost;
            Reason = NormalizeReason(reason, "Map receive loop stopped.");
            hasConnectionLostAt = true;
            connectionLostAtMs = nowMs;
            hasStableSince = false;
        }

        public bool TryBeginManualReconnect(long nowMs) {
            if (!CanManualReconnect) {
                return false;
            }

            if (State == RoReconnectState.Failed) {
                ResetAutoAttemptWindow();
            }

            State = RoReconnectState.Reconnecting;
            hasConnectionLostAt = false;
            hasStableSince = false;
            return true;
        }

        public bool TryBeginAutoReconnect(long nowMs) {
            if (!IsAutoReconnectDue(nowMs)) {
                return false;
            }

            ResetExpiredAutoAttemptWindow(nowMs);
            if (autoAttemptsInWindow >= 1) {
                MarkFailed("Automatic reconnect limit reached.");
                return false;
            }

            if (!hasAutoAttemptWindow) {
                hasAutoAttemptWindow = true;
                autoAttemptWindowStartedAtMs = nowMs;
            }

            autoAttemptsInWindow++;
            State = RoReconnectState.Reconnecting;
            hasConnectionLostAt = false;
            hasStableSince = false;
            return true;
        }

        /// <summary>The session left the map on purpose (back to the character select): nothing is lost any more.</summary>
        public void Reset() {
            State = RoReconnectState.Stable;
            Reason = string.Empty;
            hasConnectionLostAt = false;
            hasStableSince = false;
        }

        public void MarkFailed(string reason) {
            State = RoReconnectState.Failed;
            Reason = NormalizeReason(reason, "Reconnect failed.");
            hasConnectionLostAt = false;
            hasStableSince = false;
        }

        private static string NormalizeReason(string reason, string fallback) {
            return string.IsNullOrWhiteSpace(reason) ? fallback : reason;
        }

        private void ResetExpiredAutoAttemptWindow(long nowMs) {
            if (hasAutoAttemptWindow &&
                nowMs - autoAttemptWindowStartedAtMs >= AutoReconnectAttemptWindowMilliseconds) {
                ResetAutoAttemptWindow();
            }
        }

        private void ResetAutoAttemptWindow() {
            hasAutoAttemptWindow = false;
            autoAttemptWindowStartedAtMs = 0;
            autoAttemptsInWindow = 0;
        }
    }
}
