using System;
using System.Collections.Generic;
using Ragnarok.Diagnostics;
using Ragnarok.Network;
using Unity.Profiling;

namespace Ragnarok.UI {
    internal sealed class RoShellLiveDrainHistory {
        private static readonly ProfilerMarker SnapshotRecentMarker = new ProfilerMarker("RO.Shell.SnapshotRecent");

        private readonly int maxRecentDrains;
        private readonly Queue<RoLiveDrainSnapshot> recentDrains;

        private RoMapEntryDrainResult latestDrain;
        private int sequence;
        private int latestPublishTickCount;
        private bool hasLatestPublishTickCount;

        public RoShellLiveDrainHistory(int maxRecentDrains) {
            this.maxRecentDrains = maxRecentDrains <= 0 ? 64 : maxRecentDrains;
            recentDrains = new Queue<RoLiveDrainSnapshot>(this.maxRecentDrains);
            latestDrain = RoMapEntryDrainResult.Empty;
        }

        public RoMapEntryDrainResult LatestDrain => latestDrain ?? RoMapEntryDrainResult.Empty;
        public int Sequence => sequence;
        public bool HasLatestPublishTickCount => hasLatestPublishTickCount;
        public int LatestPublishTickCount => latestPublishTickCount;

        public RoLiveDrainSnapshot[] SnapshotRecent() {
            if (RoRuntimeDiagnostics.RuntimePerfLogEnabled) {
                using (SnapshotRecentMarker.Auto()) {
                    RoRuntimePerfCounters.AddSnapshotRecent(recentDrains.Count);
                    return recentDrains.ToArray();
                }
            }

            return recentDrains.ToArray();
        }

        public void CopyRecentInto(List<RoLiveDrainSnapshot> buffer) {
            if (buffer == null) {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (RoRuntimeDiagnostics.RuntimePerfLogEnabled) {
                using (SnapshotRecentMarker.Auto()) {
                    buffer.Clear();
                    foreach (RoLiveDrainSnapshot drain in recentDrains) {
                        buffer.Add(drain);
                    }
                }

                return;
            }

            buffer.Clear();
            foreach (RoLiveDrainSnapshot drain in recentDrains) {
                buffer.Add(drain);
            }
        }

        public void Publish(RoMapEntryDrainResult drain) {
            latestDrain = drain ?? RoMapEntryDrainResult.Empty;
            sequence++;
            if (RoRuntimeDiagnostics.RuntimePerfLogEnabled) {
                latestPublishTickCount = Environment.TickCount;
                hasLatestPublishTickCount = true;
            }

            recentDrains.Enqueue(new RoLiveDrainSnapshot(sequence, latestDrain));
            while (recentDrains.Count > maxRecentDrains) {
                recentDrains.Dequeue();
            }
        }

        public void ClearAll() {
            latestDrain = RoMapEntryDrainResult.Empty;
            sequence = 0;
            latestPublishTickCount = 0;
            hasLatestPublishTickCount = false;
            recentDrains.Clear();
        }

        public void ClearCurrentMap() {
            latestDrain = RoMapEntryDrainResult.Empty;
            latestPublishTickCount = 0;
            hasLatestPublishTickCount = false;
            recentDrains.Clear();
        }
    }
}
