using System;
using System.Diagnostics;
using UnityEngine;

namespace Ragnarok.Network {
    public static class RoMonotonicTime {
        private static readonly long StartTimestamp = Stopwatch.GetTimestamp();

        public static long NowMilliseconds {
            get {
                long elapsedTicks = Stopwatch.GetTimestamp() - StartTimestamp;
                return (long) (elapsedTicks * 1000.0 / Stopwatch.Frequency);
            }
        }
    }

    public static class RoPresentationClock {
        public static float NowSeconds => Time.realtimeSinceStartup;
        public static float DeltaSeconds => Mathf.Max(0f, Time.unscaledDeltaTime);
        public static long NowMilliseconds => RoMonotonicTime.NowMilliseconds;

        public static long LocalMillisecondsAt(float presentationSeconds) {
            if (float.IsNaN(presentationSeconds) || float.IsInfinity(presentationSeconds)) {
                return NowMilliseconds;
            }

            double deltaSeconds = NowSeconds - presentationSeconds;
            return NowMilliseconds - (long) Math.Round(deltaSeconds * 1000d);
        }
    }

    public enum RoServerClockSyncSampleMode {
        None,
        InitialAnchor,
        Slew,
        HardReset
    }

    public readonly struct RoServerClockSyncSample {
        public RoServerClockSyncSample(
            long sequence,
            long localNowMilliseconds,
            int rttMilliseconds,
            uint rawSampleServerTick,
            uint predictedServerTick,
            int innovationMilliseconds,
            uint slewedServerTick,
            RoServerClockSyncSampleMode mode
        ) {
            Sequence = sequence;
            LocalNowMilliseconds = localNowMilliseconds;
            RttMilliseconds = rttMilliseconds;
            RawSampleServerTick = rawSampleServerTick;
            PredictedServerTick = predictedServerTick;
            InnovationMilliseconds = innovationMilliseconds;
            SlewedServerTick = slewedServerTick;
            Mode = mode;
        }

        public long Sequence { get; }
        public long LocalNowMilliseconds { get; }
        public int RttMilliseconds { get; }
        public uint RawSampleServerTick { get; }
        public uint PredictedServerTick { get; }
        public int InnovationMilliseconds { get; }
        public uint SlewedServerTick { get; }
        public RoServerClockSyncSampleMode Mode { get; }

        public string ToTraceDetail() {
            return $"clock raw={RawSampleServerTick} predicted={PredictedServerTick} innovationMs={InnovationMilliseconds} slewed={SlewedServerTick} rttMs={RttMilliseconds} mode={Mode}";
        }
    }

    public sealed class RoServerClockSync {
        private const int MaxAcceptedRttMilliseconds = 1000;
        private const int HardResetInnovationMilliseconds = 2000;
        private const int SlewLimitMilliseconds = 40;

        private readonly object sync = new object();
        private bool hasPendingRequest;
        private long requestSentLocalMs;
        private bool hasEstimate;
        private uint anchorServerTick;
        private long anchorLocalMs;
        private bool hasLastEstimate;
        private uint lastEstimateServerTick;
        private long sampleSequence;
        private RoServerClockSyncSample lastSample;
        private int lastRttMilliseconds;

        public bool HasEstimate {
            get {
                lock (sync) {
                    return hasEstimate;
                }
            }
        }

        public int LastRttMilliseconds {
            get {
                lock (sync) {
                    return lastRttMilliseconds;
                }
            }
        }

        public long LastSampleSequence {
            get {
                lock (sync) {
                    return lastSample.Sequence;
                }
            }
        }

        public bool TryGetLastSample(out RoServerClockSyncSample sample) {
            lock (sync) {
                sample = lastSample;
                return sample.Sequence > 0;
            }
        }

        public void Reset() {
            lock (sync) {
                hasPendingRequest = false;
                hasEstimate = false;
                hasLastEstimate = false;
                lastEstimateServerTick = 0;
                sampleSequence = 0;
                lastSample = default;
                lastRttMilliseconds = 0;
            }
        }

        public void OnTimeRequestSent(long localNowMs) {
            lock (sync) {
                hasPendingRequest = true;
                requestSentLocalMs = localNowMs;
            }
        }

        public void OnTimeResponse(uint serverTick, long localNowMs) {
            lock (sync) {
                if (!hasPendingRequest) {
                    return;
                }

                hasPendingRequest = false;
                long rtt = localNowMs - requestSentLocalMs;
                if (rtt < 0 || rtt > MaxAcceptedRttMilliseconds) {
                    return;
                }

                lastRttMilliseconds = (int) rtt;
                uint sampleServerNow = unchecked(serverTick + (uint) (rtt / 2));
                if (!hasEstimate) {
                    uint initialServerTick = ClampMonotonic(sampleServerNow);
                    SetAnchor(initialServerTick, localNowMs);
                    RecordSample(
                        localNowMs,
                        lastRttMilliseconds,
                        sampleServerNow,
                        sampleServerNow,
                        0,
                        initialServerTick,
                        RoServerClockSyncSampleMode.InitialAnchor);
                    return;
                }

                uint predicted = EstimateFromAnchor(localNowMs);
                int innovation = unchecked((int) (sampleServerNow - predicted));
                uint slewedServerTick;
                RoServerClockSyncSampleMode mode;
                if (Math.Abs(innovation) > HardResetInnovationMilliseconds) {
                    slewedServerTick = sampleServerNow;
                    mode = RoServerClockSyncSampleMode.HardReset;
                } else {
                    int correctionMilliseconds = Math.Max(
                        -SlewLimitMilliseconds,
                        Math.Min(SlewLimitMilliseconds, innovation));
                    slewedServerTick = unchecked(predicted + (uint) correctionMilliseconds);
                    mode = RoServerClockSyncSampleMode.Slew;
                }

                slewedServerTick = ClampMonotonic(slewedServerTick);
                SetAnchor(slewedServerTick, localNowMs);
                RecordSample(
                    localNowMs,
                    lastRttMilliseconds,
                    sampleServerNow,
                    predicted,
                    innovation,
                    slewedServerTick,
                    mode);
            }
        }

        public bool TryEstimateServerNow(long localNowMs, out uint serverTick) {
            lock (sync) {
                if (!hasEstimate) {
                    serverTick = 0;
                    return false;
                }

                serverTick = ClampMonotonic(EstimateFromAnchor(localNowMs));
                return true;
            }
        }

        private void SetAnchor(uint serverTick, long localNowMs) {
            anchorServerTick = serverTick;
            anchorLocalMs = localNowMs;
            hasEstimate = true;
        }

        private uint EstimateFromAnchor(long localNowMs) {
            long deltaMilliseconds = Math.Max(0L, localNowMs - anchorLocalMs);
            return unchecked(anchorServerTick + (uint) deltaMilliseconds);
        }

        private uint ClampMonotonic(uint serverTick) {
            if (hasLastEstimate && unchecked((int) (serverTick - lastEstimateServerTick)) < 0) {
                return lastEstimateServerTick;
            }

            lastEstimateServerTick = serverTick;
            hasLastEstimate = true;
            return serverTick;
        }

        private void RecordSample(
            long localNowMs,
            int rttMilliseconds,
            uint rawSampleServerTick,
            uint predictedServerTick,
            int innovationMilliseconds,
            uint slewedServerTick,
            RoServerClockSyncSampleMode mode
        ) {
            lastSample = new RoServerClockSyncSample(
                ++sampleSequence,
                localNowMs,
                rttMilliseconds,
                rawSampleServerTick,
                predictedServerTick,
                innovationMilliseconds,
                slewedServerTick,
                mode);
        }
    }
}
