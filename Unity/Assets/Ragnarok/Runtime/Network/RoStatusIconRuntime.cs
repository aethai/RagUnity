using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ragnarok.Network {
    public readonly struct RoStatusIconSnapshot {
        public RoStatusIconSnapshot(
            uint actorId,
            ushort statusId,
            bool active,
            uint totalMilliseconds,
            uint remainingMilliseconds,
            int value1,
            int value2,
            int value3,
            long startedAtMilliseconds,
            long expiresAtMilliseconds,
            bool hasFiniteDuration,
            bool isInfiniteDuration,
            long activationOrder) {
            ActorId = actorId;
            StatusId = statusId;
            Active = active;
            TotalMilliseconds = totalMilliseconds;
            RemainingMilliseconds = remainingMilliseconds;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
            StartedAtMilliseconds = startedAtMilliseconds;
            ExpiresAtMilliseconds = expiresAtMilliseconds;
            HasFiniteDuration = hasFiniteDuration;
            IsInfiniteDuration = isInfiniteDuration;
            ActivationOrder = activationOrder;
        }

        public uint ActorId { get; }
        public ushort StatusId { get; }
        public bool Active { get; }
        public uint TotalMilliseconds { get; }
        public uint RemainingMilliseconds { get; }
        public int Value1 { get; }
        public int Value2 { get; }
        public int Value3 { get; }
        public long StartedAtMilliseconds { get; }
        public long ExpiresAtMilliseconds { get; }
        public bool HasFiniteDuration { get; }
        public bool IsInfiniteDuration { get; }
        public long ActivationOrder { get; }
        public bool HasExpiration => HasFiniteDuration && ExpiresAtMilliseconds > 0L;

        public uint ResolveRemainingMilliseconds(long nowMilliseconds) {
            if (!HasFiniteDuration || ExpiresAtMilliseconds <= 0L) {
                return RemainingMilliseconds;
            }

            long remaining = ExpiresAtMilliseconds - nowMilliseconds;
            if (remaining <= 0L) {
                return 0u;
            }

            return remaining >= uint.MaxValue ? uint.MaxValue : (uint) remaining;
        }

        public float ResolveRemainingRatio(long nowMilliseconds) {
            if (!HasFiniteDuration || TotalMilliseconds == 0u) {
                return 0f;
            }

            float ratio = ResolveRemainingMilliseconds(nowMilliseconds) / (float) TotalMilliseconds;
            if (ratio < 0f) {
                return 0f;
            }

            return ratio > 1f ? 1f : ratio;
        }
    }

    public sealed class RoStatusIconTracker {
        public const uint InfiniteDurationSentinelMilliseconds = 9999u;

        private static readonly long StopwatchStartTimestamp = Stopwatch.GetTimestamp();
        private static readonly Comparison<RoStatusIconSnapshot> CompareByActivationOrder =
            (left, right) => left.ActivationOrder.CompareTo(right.ActivationOrder);

        private readonly Dictionary<Key, RoStatusIconSnapshot> activeByKey =
            new Dictionary<Key, RoStatusIconSnapshot>();
        private readonly List<Key> expiredKeys = new List<Key>(8);
        private long nextActivationOrder;

        public int Count => activeByKey.Count;

        public static long DefaultMonotonicMilliseconds() {
            long elapsedTicks = Stopwatch.GetTimestamp() - StopwatchStartTimestamp;
            return (long) (elapsedTicks * 1000.0 / Stopwatch.Frequency);
        }

        public bool Apply(RoZcMsgStateChange change, uint defaultActorId, long nowMilliseconds) {
            uint actorId = change.Aid == 0 ? defaultActorId : change.Aid;
            if (actorId == 0) {
                return false;
            }

            var key = new Key(actorId, change.StatusId);
            bool isInfinite = IsInfiniteDuration(change.TotalMilliseconds, change.RemainingMilliseconds);
            bool hasFiniteDuration = !isInfinite && (change.RemainingMilliseconds > 0u || change.TotalMilliseconds > 0u);
            bool shouldKeepActive = change.Active || isInfinite || hasFiniteDuration;
            if (!shouldKeepActive) {
                return activeByKey.Remove(key);
            }

            uint resolvedRemainingMilliseconds = change.RemainingMilliseconds;
            if (hasFiniteDuration && resolvedRemainingMilliseconds == 0u) {
                resolvedRemainingMilliseconds = change.TotalMilliseconds;
            }

            uint resolvedTotalMilliseconds = change.TotalMilliseconds;
            if (hasFiniteDuration && resolvedTotalMilliseconds == 0u) {
                resolvedTotalMilliseconds = resolvedRemainingMilliseconds;
            }

            long activationOrder = activeByKey.TryGetValue(key, out RoStatusIconSnapshot previous)
                ? previous.ActivationOrder
                : ++nextActivationOrder;
            long expiresAtMilliseconds = hasFiniteDuration
                ? AddClamped(nowMilliseconds, resolvedRemainingMilliseconds)
                : 0L;

            activeByKey[key] = new RoStatusIconSnapshot(
                actorId,
                change.StatusId,
                active: true,
                resolvedTotalMilliseconds,
                resolvedRemainingMilliseconds,
                change.Value1,
                change.Value2,
                change.Value3,
                nowMilliseconds,
                expiresAtMilliseconds,
                hasFiniteDuration,
                isInfinite,
                activationOrder);
            return true;
        }

        public RoStatusIconSnapshot[] Snapshot(long nowMilliseconds, uint actorId = 0) {
            PruneExpired(nowMilliseconds);
            var snapshots = new List<RoStatusIconSnapshot>(activeByKey.Count);
            Snapshot(snapshots, nowMilliseconds, actorId);
            return snapshots.ToArray();
        }

        public void Snapshot(List<RoStatusIconSnapshot> snapshots, long nowMilliseconds, uint actorId = 0) {
            if (snapshots == null) {
                throw new ArgumentNullException(nameof(snapshots));
            }

            snapshots.Clear();
            PruneExpired(nowMilliseconds);
            foreach (RoStatusIconSnapshot snapshot in activeByKey.Values) {
                if (actorId != 0 && snapshot.ActorId != actorId) {
                    continue;
                }

                uint remaining = snapshot.ResolveRemainingMilliseconds(nowMilliseconds);
                snapshots.Add(new RoStatusIconSnapshot(
                    snapshot.ActorId,
                    snapshot.StatusId,
                    snapshot.Active,
                    snapshot.TotalMilliseconds,
                    remaining,
                    snapshot.Value1,
                    snapshot.Value2,
                    snapshot.Value3,
                    snapshot.StartedAtMilliseconds,
                    snapshot.ExpiresAtMilliseconds,
                    snapshot.HasFiniteDuration,
                    snapshot.IsInfiniteDuration,
                    snapshot.ActivationOrder));
            }

            snapshots.Sort(CompareByActivationOrder);
        }

        public bool ClearActor(uint actorId) {
            if (actorId == 0 || activeByKey.Count == 0) {
                return false;
            }

            expiredKeys.Clear();
            foreach (Key key in activeByKey.Keys) {
                if (key.ActorId == actorId) {
                    expiredKeys.Add(key);
                }
            }

            for (int i = 0; i < expiredKeys.Count; i++) {
                activeByKey.Remove(expiredKeys[i]);
            }

            bool changed = expiredKeys.Count > 0;
            expiredKeys.Clear();
            return changed;
        }

        public bool ClearAll() {
            if (activeByKey.Count == 0) {
                return false;
            }

            activeByKey.Clear();
            return true;
        }

        public bool PruneExpired(long nowMilliseconds) {
            if (activeByKey.Count == 0) {
                return false;
            }

            expiredKeys.Clear();
            foreach (KeyValuePair<Key, RoStatusIconSnapshot> entry in activeByKey) {
                RoStatusIconSnapshot snapshot = entry.Value;
                if (snapshot.HasExpiration && snapshot.ResolveRemainingMilliseconds(nowMilliseconds) == 0u) {
                    expiredKeys.Add(entry.Key);
                }
            }

            for (int i = 0; i < expiredKeys.Count; i++) {
                activeByKey.Remove(expiredKeys[i]);
            }

            bool changed = expiredKeys.Count > 0;
            expiredKeys.Clear();
            return changed;
        }

        private static bool IsInfiniteDuration(uint totalMilliseconds, uint remainingMilliseconds) {
            return totalMilliseconds == InfiniteDurationSentinelMilliseconds ||
                remainingMilliseconds == InfiniteDurationSentinelMilliseconds;
        }

        private static long AddClamped(long nowMilliseconds, uint durationMilliseconds) {
            if (durationMilliseconds == 0u) {
                return nowMilliseconds;
            }

            long maxAdd = long.MaxValue - nowMilliseconds;
            return durationMilliseconds > maxAdd ? long.MaxValue : nowMilliseconds + durationMilliseconds;
        }

        private readonly struct Key : IEquatable<Key> {
            public Key(uint actorId, ushort statusId) {
                ActorId = actorId;
                StatusId = statusId;
            }

            public uint ActorId { get; }
            public ushort StatusId { get; }

            public bool Equals(Key other) {
                return ActorId == other.ActorId && StatusId == other.StatusId;
            }

            public override bool Equals(object obj) {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode() {
                unchecked {
                    return ((int) ActorId * 397) ^ StatusId;
                }
            }
        }
    }
}
