using System;

namespace Ragnarok.Network {
    public sealed class RoAttendanceSnapshot {
        public static readonly RoAttendanceSnapshot Empty = new RoAttendanceSnapshot(false, false, 0, 0, 0, string.Empty, 0, 0);

        public RoAttendanceSnapshot(
            bool isOpen,
            bool hasServerState,
            int claimedDays,
            int lastResponseUnknown,
            int changeSequence,
            string message,
            int recentClaimedDay,
            int blockedClaimDay = 0) {
            IsOpen = isOpen;
            HasServerState = hasServerState;
            ClaimedDays = Math.Max(0, Math.Min(RoAttendanceRuntime.MaxAttendanceDays, claimedDays));
            LastResponseUnknown = lastResponseUnknown;
            ChangeSequence = changeSequence;
            Message = message ?? string.Empty;
            RecentClaimedDay = Math.Max(0, Math.Min(RoAttendanceRuntime.MaxAttendanceDays, recentClaimedDay));
            BlockedClaimDay = Math.Max(0, Math.Min(RoAttendanceRuntime.MaxAttendanceDays, blockedClaimDay));
        }

        public bool IsOpen { get; }
        public bool HasServerState { get; }
        public int ClaimedDays { get; }
        public int LastResponseUnknown { get; }
        public int ChangeSequence { get; }
        public string Message { get; }
        public int RecentClaimedDay { get; }
        public int BlockedClaimDay { get; }
        public bool IsClaimBlockedToday => BlockedClaimDay > 0 && BlockedClaimDay == NextClaimDay;
        public bool IsRewardAvailable => HasServerState && ClaimedDays < RoAttendanceRuntime.MaxAttendanceDays && RecentClaimedDay <= 0 && !IsClaimBlockedToday;
        public bool CanClaim => IsOpen && IsRewardAvailable;
        public int NextClaimDay => Math.Max(1, Math.Min(RoAttendanceRuntime.MaxAttendanceDays, ClaimedDays + 1));
    }

    public sealed class RoAttendanceRuntime : IDisposable {
        public const byte ClientAttendanceUiType = 5;
        public const byte ServerAttendanceUiType = 7;
        public const int MaxAttendanceDays = 20;

        private readonly object sync = new object();
        private bool isOpen;
        private int claimedDays;
        private int recentClaimedDay;
        private DateTime recentClaimLocalDate;
        private int blockedClaimDay;
        private DateTime blockedClaimLocalDate;
        private int lastResponseUnknown;
        private int changeSequence;
        private string message = string.Empty;
        private bool hasServerState;

        public RoAttendanceSnapshot Snapshot() {
            lock (sync) {
                ClearExpiredDailyStateLocked();
                return new RoAttendanceSnapshot(
                    isOpen,
                    hasServerState,
                    claimedDays,
                    lastResponseUnknown,
                    changeSequence,
                    message,
                    recentClaimedDay,
                    blockedClaimDay);
            }
        }

        public void OpenLocal() {
            lock (sync) {
                if (isOpen) {
                    return;
                }

                isOpen = true;
                message = string.Empty;
                IncrementChangeSequenceLocked();
            }
        }

        public void CloseLocal() {
            lock (sync) {
                if (!isOpen) {
                    return;
                }

                isOpen = false;
                IncrementChangeSequenceLocked();
            }
        }

        public bool Apply(RoZcUiOpen uiOpen) {
            if (!uiOpen.IsAttendance) {
                return false;
            }

            lock (sync) {
                bool wasOpen = isOpen;
                bool hadServerState = hasServerState;
                isOpen = true;
                hasServerState = true;
                AttendanceOpenCounter counter = DecodeOpenCounter(uiOpen.Data);
                int nextClaimedDays = counter.ClaimedDays;
                int previousClaimedDays = claimedDays;
                int previousRecentClaimedDay = recentClaimedDay;
                int previousBlockedClaimDay = blockedClaimDay;
                bool changed = previousClaimedDays != nextClaimedDays;
                bool clearedMessage = !string.IsNullOrWhiteSpace(message);
                DateTime today = DateTime.Now.Date;
                claimedDays = nextClaimedDays;

                if (counter.ClaimedToday && claimedDays > 0) {
                    recentClaimedDay = claimedDays;
                    recentClaimLocalDate = today;
                    blockedClaimDay = 0;
                    blockedClaimLocalDate = default;
                } else {
                    recentClaimedDay = 0;
                    recentClaimLocalDate = default;
                }

                int nextClaimDay = NextClaimDayFor(claimedDays);
                if (blockedClaimDay != nextClaimDay) {
                    blockedClaimDay = 0;
                    blockedClaimLocalDate = default;
                }

                message = string.Empty;
                changed |= recentClaimedDay != previousRecentClaimedDay ||
                    blockedClaimDay != previousBlockedClaimDay;
                if (changed || !wasOpen || !hadServerState || clearedMessage) {
                    IncrementChangeSequenceLocked();
                }

                return true;
            }
        }

        public bool Apply(RoZcAttendanceResponse response) {
            lock (sync) {
                int previousClaimedDays = claimedDays;
                int responseClaimedDays = ClampDays(response.Data);
                bool claimedReward = responseClaimedDays > previousClaimedDays;
                // Claim responses confirm the next reward; never let a malformed/status value jump multiple days.
                int nextClaimedDays = claimedReward
                    ? Math.Min(MaxAttendanceDays, previousClaimedDays + 1)
                    : previousClaimedDays;
                DateTime today = DateTime.Now.Date;
                claimedDays = nextClaimedDays;
                lastResponseUnknown = response.Unknown;
                isOpen = true;
                hasServerState = true;
                if (claimedReward) {
                    recentClaimedDay = nextClaimedDays;
                    recentClaimLocalDate = today;
                    blockedClaimDay = 0;
                    blockedClaimLocalDate = default;
                } else {
                    blockedClaimDay = NextClaimDayFor(nextClaimedDays);
                    blockedClaimLocalDate = today;
                    if (nextClaimedDays > 0) {
                        recentClaimedDay = nextClaimedDays;
                        recentClaimLocalDate = today;
                    }
                }

                message = claimedReward
                    ? "Attendance reward claimed. Check RodEx for delivery."
                    : "Attendance reward is not available yet.";
                IncrementChangeSequenceLocked();
                return claimedReward;
            }
        }

        public void Dispose() {
        }

        private static int ClampDays(int value) {
            return Math.Max(0, Math.Min(MaxAttendanceDays, value));
        }

        private static AttendanceOpenCounter DecodeOpenCounter(int value) {
            if (value >= 10) {
                // rAthena encodes ZC_UI_OPEN attendance data as 10 * claimedDays + claimedTodayFlag.
                return new AttendanceOpenCounter(ClampDays(value / 10), value % 10 != 0);
            }

            return new AttendanceOpenCounter(ClampDays(value), false);
        }

        private static int NextClaimDayFor(int claimedDayCount) {
            return Math.Max(1, Math.Min(MaxAttendanceDays, ClampDays(claimedDayCount) + 1));
        }

        private readonly struct AttendanceOpenCounter {
            public AttendanceOpenCounter(int claimedDays, bool claimedToday) {
                ClaimedDays = claimedDays;
                ClaimedToday = claimedToday;
            }

            public int ClaimedDays { get; }
            public bool ClaimedToday { get; }
        }

        private void ClearExpiredDailyStateLocked() {
            bool hasRecentClaim = recentClaimedDay > 0 && recentClaimLocalDate != default;
            bool hasBlockedClaim = blockedClaimDay > 0 && blockedClaimLocalDate != default;
            if (!hasRecentClaim && !hasBlockedClaim) {
                recentClaimedDay = 0;
                recentClaimLocalDate = default;
                blockedClaimDay = 0;
                blockedClaimLocalDate = default;
                return;
            }

            DateTime today = DateTime.Now.Date;
            bool changed = false;
            if (!hasRecentClaim || recentClaimLocalDate != today) {
                recentClaimedDay = 0;
                recentClaimLocalDate = default;
                changed |= hasRecentClaim;
            }

            if (!hasBlockedClaim || blockedClaimLocalDate != today) {
                blockedClaimDay = 0;
                blockedClaimLocalDate = default;
                changed |= hasBlockedClaim;
            }

            if (changed) {
                IncrementChangeSequenceLocked();
            }
        }

        private void IncrementChangeSequenceLocked() {
            unchecked {
                changeSequence++;
            }
        }
    }
}
