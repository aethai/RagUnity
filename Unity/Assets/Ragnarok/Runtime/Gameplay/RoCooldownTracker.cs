using System;
using System.Collections.Generic;

namespace Ragnarok.Gameplay {
    public readonly struct RoCooldownSnapshot {
        public RoCooldownSnapshot(string key, long startMilliseconds, long endMilliseconds, long nowMilliseconds) {
            Key = key ?? string.Empty;
            StartMilliseconds = startMilliseconds;
            EndMilliseconds = endMilliseconds;
            RemainingMilliseconds = Math.Max(0, endMilliseconds - nowMilliseconds);
            DurationMilliseconds = Math.Max(0, endMilliseconds - startMilliseconds);
        }

        public string Key { get; }
        public long StartMilliseconds { get; }
        public long EndMilliseconds { get; }
        public long RemainingMilliseconds { get; }
        public long DurationMilliseconds { get; }
        public bool IsActive => RemainingMilliseconds > 0;
    }

    public sealed class RoCooldownTracker {
        private readonly Dictionary<string, RoCooldownEntry> cooldownsByKey =
            new Dictionary<string, RoCooldownEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, RoCooldownEntry> skillCooldownsById =
            new Dictionary<int, RoCooldownEntry>();

        public bool HasEntries => cooldownsByKey.Count > 0 || skillCooldownsById.Count > 0;

        public void Start(string key, long nowMilliseconds, long durationMilliseconds) {
            if (string.IsNullOrWhiteSpace(key) || durationMilliseconds <= 0) {
                return;
            }

            cooldownsByKey[key.Trim()] = new RoCooldownEntry(nowMilliseconds, nowMilliseconds + durationMilliseconds);
        }

        public void StartSkill(int skillId, long nowMilliseconds, long durationMilliseconds) {
            if (skillId <= 0 || durationMilliseconds <= 0) {
                return;
            }

            skillCooldownsById[skillId] = new RoCooldownEntry(nowMilliseconds, nowMilliseconds + durationMilliseconds);
        }

        public bool TryGet(string key, long nowMilliseconds, out RoCooldownSnapshot snapshot) {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(key) ||
                !cooldownsByKey.TryGetValue(key.Trim(), out RoCooldownEntry entry)) {
                return false;
            }

            snapshot = new RoCooldownSnapshot(key.Trim(), entry.StartMilliseconds, entry.EndMilliseconds, nowMilliseconds);
            if (snapshot.IsActive) {
                return true;
            }

            cooldownsByKey.Remove(key.Trim());
            snapshot = default;
            return false;
        }

        public bool TryGetSkill(int skillId, long nowMilliseconds, out RoCooldownSnapshot snapshot) {
            snapshot = default;
            if (skillId <= 0 || !skillCooldownsById.TryGetValue(skillId, out RoCooldownEntry entry)) {
                return false;
            }

            snapshot = new RoCooldownSnapshot(string.Empty, entry.StartMilliseconds, entry.EndMilliseconds, nowMilliseconds);
            if (snapshot.IsActive) {
                return true;
            }

            skillCooldownsById.Remove(skillId);
            snapshot = default;
            return false;
        }

        public RoCooldownSnapshot[] Snapshot(long nowMilliseconds) {
            var expired = new List<string>();
            var expiredSkills = new List<int>();
            var snapshots = new List<RoCooldownSnapshot>();
            foreach (KeyValuePair<string, RoCooldownEntry> pair in cooldownsByKey) {
                var snapshot = new RoCooldownSnapshot(pair.Key, pair.Value.StartMilliseconds, pair.Value.EndMilliseconds, nowMilliseconds);
                if (snapshot.IsActive) {
                    snapshots.Add(snapshot);
                } else {
                    expired.Add(pair.Key);
                }
            }

            foreach (KeyValuePair<int, RoCooldownEntry> pair in skillCooldownsById) {
                var snapshot = new RoCooldownSnapshot(SkillCooldownKey(pair.Key), pair.Value.StartMilliseconds, pair.Value.EndMilliseconds, nowMilliseconds);
                if (snapshot.IsActive) {
                    snapshots.Add(snapshot);
                } else {
                    expiredSkills.Add(pair.Key);
                }
            }

            for (int i = 0; i < expired.Count; i++) {
                cooldownsByKey.Remove(expired[i]);
            }

            for (int i = 0; i < expiredSkills.Count; i++) {
                skillCooldownsById.Remove(expiredSkills[i]);
            }

            snapshots.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            return snapshots.ToArray();
        }

        public void Clear(string key) {
            if (!string.IsNullOrWhiteSpace(key)) {
                cooldownsByKey.Remove(key.Trim());
            }
        }

        public void ClearSkill(int skillId) {
            if (skillId > 0) {
                skillCooldownsById.Remove(skillId);
            }
        }

        public void ClearAll() {
            cooldownsByKey.Clear();
            skillCooldownsById.Clear();
        }

        private static string SkillCooldownKey(int skillId) {
            return "skill:" + skillId.ToString();
        }

        private readonly struct RoCooldownEntry {
            public RoCooldownEntry(long startMilliseconds, long endMilliseconds) {
                StartMilliseconds = startMilliseconds;
                EndMilliseconds = endMilliseconds;
            }

            public long StartMilliseconds { get; }
            public long EndMilliseconds { get; }
        }
    }
}
