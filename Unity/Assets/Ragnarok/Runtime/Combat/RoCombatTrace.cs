using System.Collections.Generic;
using Ragnarok.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace Ragnarok.Combat {
    public enum RoCombatTracePhase {
        Dispatch,
        SkillCast,
        Motion,
        CastAura,
        TargetLock,
        SkillRelease,
        BeforeHit,
        HitEffect,
        Damage,
        Cleanup,
        SuppressedLegacy,
        SuppressedVisual,
        Warning
    }

    public readonly struct RoCombatTraceEvent {
        public RoCombatTraceEvent(
            float localTimeSeconds,
            RoCombatTracePhase phase,
            ushort skillId,
            uint sourceId,
            uint targetId,
            string packetName,
            string detail
        ) {
            LocalTimeSeconds = localTimeSeconds;
            Phase = phase;
            SkillId = skillId;
            SourceId = sourceId;
            TargetId = targetId;
            PacketName = packetName ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public float LocalTimeSeconds { get; }
        public RoCombatTracePhase Phase { get; }
        public ushort SkillId { get; }
        public uint SourceId { get; }
        public uint TargetId { get; }
        public string PacketName { get; }
        public string Detail { get; }

        public override string ToString() {
            return $"t={LocalTimeSeconds:0.000} phase={Phase} skill={SkillId} src={SourceId} dst={TargetId} packet={PacketName} {Detail}";
        }
    }

    public sealed class RoCombatTrace {
        private static readonly ProfilerMarker RecordMarker = new ProfilerMarker("RO.Combat.Trace.Record");

        private readonly List<RoCombatTraceEvent> entries = new List<RoCombatTraceEvent>(128);
        private int capacity = 256;

        public bool Enabled { get; set; } = true;

        public int Capacity {
            get => capacity;
            set {
                capacity = Mathf.Max(1, value);
                TrimToCapacity();
            }
        }

        public IReadOnlyList<RoCombatTraceEvent> Entries => entries;
        public int Count => entries.Count;

        public void Clear() {
            entries.Clear();
        }

        public void Record(
            float localTimeSeconds,
            RoCombatTracePhase phase,
            ushort skillId,
            uint sourceId,
            uint targetId,
            string packetName,
            string detail = null
        ) {
            if (!Enabled) {
                return;
            }

            using (RecordMarker.Auto()) {
                entries.Add(new RoCombatTraceEvent(localTimeSeconds, phase, skillId, sourceId, targetId, packetName, detail));
                RoRuntimePerfCounters.IncrementCombatTraceRecord();
                TrimToCapacity();
            }
        }

        private void TrimToCapacity() {
            int overflow = entries.Count - capacity;
            if (overflow > 0) {
                entries.RemoveRange(0, overflow);
            }
        }
    }
}
