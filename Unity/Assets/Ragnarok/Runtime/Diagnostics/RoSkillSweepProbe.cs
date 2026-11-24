using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Passive tap for the automated skill sweep (<see cref="RoSkillSweepRunner"/>): records the skill packets the
    /// visual director dispatched, the effect ids that were launched (and by which engine), skill failures and how
    /// much the classic effect engine drew. Off unless a sweep is running; every call site is a single bool test.
    /// </summary>
    public static class RoSkillSweepProbe {
        public static bool Enabled;

        /// <summary>Diagnostics (!fxspikes): every classic spike and slow launch line is also handed here.</summary>
        public static System.Action<string> Echo;

        static readonly List<string> events = new List<string>();

        public static int ClassicMaxEffects;
        public static int ClassicMaxPrims;
        public static int ClassicMaxBatches;
        public static int ClassicMaxVertices;
        public static int ClassicDrawnFrames;
        /// <summary>Most depth-tested ground-cell quads (CGroundScope, cursor cell, /range) submitted in one frame.</summary>
        public static int GroundQuadMax;
        static int groundQuadsThisFrame;

        public static void CountGroundQuad() {
            if (Enabled) groundQuadsThisFrame++;
        }

        public static void Record(string line) {
            if (!Enabled) {
                return;
            }

            events.Add(Time.realtimeSinceStartup.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + line);
        }

        public static void RecordClassicFrame(int effects, int prims, int batches, int vertices) {
            if (!Enabled || (effects == 0 && prims == 0)) {
                return;
            }

            if (effects > ClassicMaxEffects) ClassicMaxEffects = effects;
            if (prims > ClassicMaxPrims) ClassicMaxPrims = prims;
            if (batches > ClassicMaxBatches) ClassicMaxBatches = batches;
            if (vertices > ClassicMaxVertices) ClassicMaxVertices = vertices;
            if (batches > 0) ClassicDrawnFrames++;
        }

        static uint heapBump, heapSize;

        /// <summary>Classic engine frame cost while a sweep records: phases of RoClassicEffectBridge.RunFrame.</summary>
        public enum ClassicPhase { Follow, Advance, Render, Draw, Looks, Total, Resources, Audio }

        static readonly long[] phaseSum = new long[8];
        static readonly long[] phaseMax = new long[8];
        static long resourceTicksThisFrame;
        static long audioTicksThisFrame;

        /// <summary>Time a classic frame spent in sound requests (clip lookup and decode on first use).</summary>
        public static void AddAudioTicks(long stopwatchTicks) {
            if (Enabled) audioTicksThisFrame += stopwatchTicks;
        }

        /// <summary>Time spent loading engine resources (GRF reads, texture creation) inside the frame's phases.</summary>
        public static void AddResourceTicks(long stopwatchTicks) {
            if (Enabled) resourceTicksThisFrame += stopwatchTicks;
        }
        static int timedFrames;

        static int gcAtFrameStart, gcFrames, lookFrames;

        /// <summary>A frame in which the classic look renderer showed an actor look (flags or tint): such effects draw
        /// no batches of their own (Mind Breaker's tint, Frenzy's glow ...).</summary>
        public static void CountLookFrame() {
            if (Enabled) lookFrames++;
        }
        static long maxWithGc, maxWithoutGc;

        /// <summary>Called when a timed classic frame starts (GC attribution of its cost).</summary>
        public static void MarkClassicFrameStart() {
            if (Enabled) gcAtFrameStart = System.GC.CollectionCount(0);
        }

        public static void RecordClassicPhase(ClassicPhase phase, long stopwatchTicks) {
            if (!Enabled) return;
            int i = (int) phase;
            phaseSum[i] += stopwatchTicks;
            if (stopwatchTicks > phaseMax[i]) phaseMax[i] = stopwatchTicks;
            phaseThisFrame[i] = stopwatchTicks;
            if (phase == ClassicPhase.Total) {
                timedFrames++;
                // a garbage collection inside this classic frame: keep the worst frame with and without one apart
                bool collected = System.GC.CollectionCount(0) != gcAtFrameStart;
                if (collected) {
                    gcFrames++;
                    if (stopwatchTicks > maxWithGc) maxWithGc = stopwatchTicks;
                } else if (stopwatchTicks > maxWithoutGc) {
                    maxWithoutGc = stopwatchTicks;
                }

                if (stopwatchTicks * TicksToMs >= SpikeMilliseconds) {
                    RecordSpike(collected);
                }

                stepTicks.Clear();
                submitTicksThisFrame = 0;
                phaseSum[(int) ClassicPhase.Resources] += resourceTicksThisFrame;
                if (resourceTicksThisFrame > phaseMax[(int) ClassicPhase.Resources]) phaseMax[(int) ClassicPhase.Resources] = resourceTicksThisFrame;
                resourceTicksThisFrame = 0;
                phaseSum[(int) ClassicPhase.Audio] += audioTicksThisFrame;
                if (audioTicksThisFrame > phaseMax[(int) ClassicPhase.Audio]) phaseMax[(int) ClassicPhase.Audio] = audioTicksThisFrame;
                audioTicksThisFrame = 0;
            }
        }

        /// <summary>A classic frame at least this long is written out step by step ("spike classic ...").</summary>
        const double SpikeMilliseconds = 8.0;
        /// <summary>A launch at least this long is written out ("slow launch ...").</summary>
        const double SlowLaunchMilliseconds = 2.0;
        static readonly double TicksToMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        static readonly long[] phaseThisFrame = new long[8];
        static readonly Dictionary<long, long> stepTicks = new Dictionary<long, long>();
        /// <summary>Names of the FxWorld profiler's step kinds (FxProfileKind order).</summary>
        static readonly string[] StepKinds = { "ef", "prim", "newsys", "ef-render", "prim-render", "newsys-render" };

        /// <summary>One step of the classic world (FxWorld.Profiler): its kind, effect id / prim type and cost.</summary>
        public static void AddClassicStep(int kind, int id, long stopwatchTicks) {
            if (!Enabled) return;
            long key = ((long) kind << 32) | (uint) id;
            stepTicks.TryGetValue(key, out long sum);
            stepTicks[key] = sum + stopwatchTicks;
        }

        static long submitTicksThisFrame;

        /// <summary>Time the bridge spent turning engine geometry into Unity vertices (inside the render steps).</summary>
        public static void AddSubmitTicks(long stopwatchTicks) {
            if (Enabled) submitTicksThisFrame += stopwatchTicks;
        }

        /// <summary>How long one effect launch took (the presenter calls it outside the classic frame).</summary>
        public static void RecordLaunch(int effectId, long stopwatchTicks) {
            double ms = stopwatchTicks * TicksToMs;
            if (Enabled && ms >= SlowLaunchMilliseconds) {
                Record($"slow launch ef {effectId} {Ms(ms)} ms");
                Echo?.Invoke($"slow launch ef {effectId} {Ms(ms)} ms");
            }
        }

        static void RecordSpike(bool collected) {
            var steps = new List<KeyValuePair<long, long>>(stepTicks);
            steps.Sort((a, b) => b.Value.CompareTo(a.Value));
            var line = new System.Text.StringBuilder("spike classic total=").Append(Ms(phaseThisFrame[(int) ClassicPhase.Total] * TicksToMs))
                .Append(" advance=").Append(Ms(phaseThisFrame[(int) ClassicPhase.Advance] * TicksToMs))
                .Append(" render=").Append(Ms(phaseThisFrame[(int) ClassicPhase.Render] * TicksToMs))
                .Append(" draw=").Append(Ms(phaseThisFrame[(int) ClassicPhase.Draw] * TicksToMs))
                .Append(" resources=").Append(Ms(resourceTicksThisFrame * TicksToMs))
                .Append(" audio=").Append(Ms(audioTicksThisFrame * TicksToMs))
                .Append(" submit=").Append(Ms(submitTicksThisFrame * TicksToMs))
                .Append(collected ? " gc" : string.Empty).Append(" |");
            for (int i = 0; i < steps.Count && i < 6; i++) {
                int kind = (int) (steps[i].Key >> 32), id = (int) (uint) steps[i].Key;
                bool prim = kind == 1 || kind == 4;
                line.Append(' ').Append(kind < StepKinds.Length ? StepKinds[kind] : kind.ToString())
                    .Append(id < 0 ? string.Empty : prim ? " 0x" + id.ToString("x2") : " " + id)
                    .Append('=').Append(Ms(steps[i].Value * TicksToMs));
            }

            Record(line.ToString());
            Echo?.Invoke(line.ToString());
        }

        static string Ms(double ms) => ms.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The classic engine heap: bytes handed out so far and its current size.</summary>
        public static void RecordHeap(uint bump, uint size) {
            if (!Enabled) return;
            heapBump = bump;
            heapSize = size;
        }

        public static void EndClassicFrame() {
            if (groundQuadsThisFrame > GroundQuadMax) GroundQuadMax = groundQuadsThisFrame;
            groundQuadsThisFrame = 0;
        }

        /// <summary>Returns everything recorded since the last call and clears the counters.</summary>
        public static List<string> Drain(out int maxEffects, out int maxPrims, out int maxBatches, out int maxVertices, out int drawnFrames) {
            var copy = new List<string>(events);
            events.Clear();
            maxEffects = ClassicMaxEffects;
            maxPrims = ClassicMaxPrims;
            maxBatches = ClassicMaxBatches;
            maxVertices = ClassicMaxVertices;
            drawnFrames = ClassicDrawnFrames;
            if (GroundQuadMax > 0) copy.Add("groundquads max=" + GroundQuadMax);
            if (heapSize > 0) copy.Add($"heap bump={heapBump / (1024 * 1024)}MB size={heapSize / (1024 * 1024)}MB");
            if (lookFrames > 0) copy.Add($"looks frames={lookFrames}");
            lookFrames = 0;
            if (timedFrames > 0) {
                double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                var line = new System.Text.StringBuilder("perf classic frames=").Append(timedFrames);
                string[] names = { "follow", "advance", "render", "draw", "looks", "total", "resources", "audio" };
                for (int i = 0; i < names.Length; i++) {
                    line.Append(' ').Append(names[i]).Append('=')
                        .Append((phaseSum[i] * ms / timedFrames).ToString("F3", System.Globalization.CultureInfo.InvariantCulture))
                        .Append('/').Append((phaseMax[i] * ms).ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }

                line.Append(" ms(avg/max) gcFrames=").Append(gcFrames)
                    .Append(" maxWithGc=").Append((maxWithGc * ms).ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" maxWithoutGc=").Append((maxWithoutGc * ms).ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                copy.Add(line.ToString());
                gcFrames = 0;
                maxWithGc = maxWithoutGc = 0;
                System.Array.Clear(phaseSum, 0, phaseSum.Length);
                System.Array.Clear(phaseMax, 0, phaseMax.Length);
                timedFrames = 0;
            }
            ClassicMaxEffects = ClassicMaxPrims = ClassicMaxBatches = ClassicMaxVertices = ClassicDrawnFrames = GroundQuadMax = 0;
            return copy;
        }

        public static IReadOnlyList<string> Peek() => events;
    }
}
