using System;

namespace Ragnarok.Effects {
    /// <summary>
    /// Evaluated position inside the server day/night cycle.
    /// </summary>
    public readonly struct RoServerDayNightSample {
        public RoServerDayNightSample(bool isNight, float cycleSeconds, float phaseProgress, float cycleNormalized) {
            IsNight = isNight;
            CycleSeconds = cycleSeconds;
            PhaseProgress = phaseProgress;
            CycleNormalized = cycleNormalized;
        }

        /// <summary>True while inside the night window.</summary>
        public bool IsNight { get; }

        /// <summary>Position inside the whole cycle in seconds, in [0, PeriodSeconds).</summary>
        public float CycleSeconds { get; }

        /// <summary>Progress through the current phase (day or night), in [0, 1].</summary>
        public float PhaseProgress { get; }

        /// <summary>Position through the whole cycle, in [0, 1].</summary>
        public float CycleNormalized { get; }
    }

    /// <summary>
    /// Reconstructs the server's continuous time-of-day from the binary night signal
    /// (rAthena <c>EFST_SKE</c>, id 160) plus the synchronized server tick.
    ///
    /// The server only tells the client "it is night" / "it is day" at phase boundaries
    /// (and re-states the current phase on map enter); it never sends the position within a
    /// phase. Because the server's day/night timer and the client's synchronized clock share
    /// the same tick base, a single witnessed boundary lets us anchor the cycle deterministically,
    /// so every client that saw the same boundary computes an identical sun position with no drift.
    ///
    /// Until a real boundary is witnessed (e.g. a fresh login mid-phase), the binary state is still
    /// correct and the position is provisionally placed in the middle of the phase; it snaps to the
    /// exact position at the first boundary the client observes.
    ///
    /// This type is deliberately free of UnityEngine dependencies so it can be unit tested headless.
    /// </summary>
    public sealed class RoServerDayNightClock {
        /// <summary>rAthena status id used for the night effect (<c>EFST_SKE</c>).</summary>
        public const ushort NightStatusId = 160;

        public const double DefaultDayDurationMilliseconds = 14.0 * 3600.0 * 1000.0; // 14h
        public const double DefaultNightDurationMilliseconds = 8.0 * 3600.0 * 1000.0; // 8h

        private const double MinPhaseDurationMilliseconds = 1000.0;

        // A naturally scheduled day/night flip arrives when the deterministic clock is already at the
        // boundary; a forced @day/@night flip arrives while the cycle is mid-phase. A flip within this
        // window of the expected boundary counts as natural (covers clock-sync jitter and processing lag).
        private const double NaturalBoundaryToleranceMilliseconds = 90_000.0;

        private double dayDurationMilliseconds = DefaultDayDurationMilliseconds;
        private double nightDurationMilliseconds = DefaultNightDurationMilliseconds;

        private bool hasAnchor;
        private bool anchorIsProvisional;
        private uint anchorServerTick;
        private double anchorCycleOffsetMilliseconds;

        private bool hasLastKnownNight;
        private bool lastKnownNight;

        public double DayDurationMilliseconds => dayDurationMilliseconds;
        public double NightDurationMilliseconds => nightDurationMilliseconds;
        public double PeriodMilliseconds => dayDurationMilliseconds + nightDurationMilliseconds;
        public float DayDurationSeconds => (float) (dayDurationMilliseconds / 1000.0);
        public float NightDurationSeconds => (float) (nightDurationMilliseconds / 1000.0);
        public float PeriodSeconds => (float) (PeriodMilliseconds / 1000.0);

        public bool HasAnchor => hasAnchor;
        public bool IsAnchorProvisional => anchorIsProvisional;
        public bool HasLastKnownNight => hasLastKnownNight;
        public bool LastKnownNight => lastKnownNight;

        /// <summary>Sets the day and night window lengths (must match the server's config).</summary>
        public void Configure(double dayDurationMs, double nightDurationMs) {
            dayDurationMilliseconds = Math.Max(MinPhaseDurationMilliseconds, dayDurationMs);
            nightDurationMilliseconds = Math.Max(MinPhaseDurationMilliseconds, nightDurationMs);
        }

        public void ConfigureHours(double dayHours, double nightHours) {
            Configure(dayHours * 3600.0 * 1000.0, nightHours * 3600.0 * 1000.0);
        }

        /// <summary>Forgets any anchor and learned state (e.g. on logout).</summary>
        public void Reset() {
            hasAnchor = false;
            anchorIsProvisional = false;
            hasLastKnownNight = false;
        }

        /// <summary>
        /// Feeds the night status (<c>EFST_SKE</c>) for the local player.
        /// <paramref name="isNight"/> is the status active flag (true = night).
        /// <paramref name="isLiveBoundary"/> must be true only for a transition observed while already
        /// standing on a map (a real global day&lt;-&gt;night flip), and false for the status the server
        /// re-states on map enter. Only a live boundary re-anchors the exact position; a map-enter
        /// re-statement keeps the existing anchor so the sun never snaps when zoning. Before any anchor
        /// exists the binary state is bootstrapped to the middle of the phase.
        /// </summary>
        public void OnNightStatus(bool isNight, bool isLiveBoundary, bool hasServerTick, uint serverTick) {
            bool stateChanged = !hasLastKnownNight || lastKnownNight != isNight;
            lastKnownNight = isNight;
            hasLastKnownNight = true;

            if (!hasServerTick) {
                return;
            }

            if (isLiveBoundary && stateChanged) {
                if (hasAnchor && !anchorIsProvisional && !IsNearExpectedBoundary(serverTick, isNight)) {
                    // The flip arrived "out of schedule" while the cycle was mid-phase: this is a forced
                    // transition (@day / @night). Jump straight to the middle of the new phase (full midday
                    // or deep midnight) instead of starting the phase at sunrise / sunset.
                    AnchorAtPhaseMidpoint(serverTick, isNight);
                } else {
                    // A naturally scheduled transition (the cycle already reached the boundary), or the
                    // first reliable boundary after login: snap to the exact boundary so the cycle keeps
                    // flowing smoothly through dusk / dawn and stays synced to the server schedule.
                    AnchorFromBoundary(serverTick, isNight);
                }

                return;
            }

            if (!hasAnchor) {
                AnchorProvisionalMidphase(serverTick, isNight);
            }

            // Otherwise keep the existing anchor; only the binary state was refreshed.
        }

        /// <summary>Anchors the cycle assuming <paramref name="serverTick"/> is the exact phase boundary.</summary>
        public void AnchorFromBoundary(uint serverTick, bool nightStarting) {
            anchorServerTick = serverTick;
            anchorCycleOffsetMilliseconds = nightStarting ? dayDurationMilliseconds : 0.0;
            hasAnchor = true;
            anchorIsProvisional = false;
        }

        /// <summary>
        /// Anchors with the position guessed in the middle of the current phase, used before a real
        /// boundary is observed. Keeps the binary state correct and bounds the worst-case error.
        /// </summary>
        public void AnchorProvisionalMidphase(uint serverTick, bool isNight) {
            anchorServerTick = serverTick;
            anchorCycleOffsetMilliseconds = isNight
                ? dayDurationMilliseconds + nightDurationMilliseconds * 0.5
                : dayDurationMilliseconds * 0.5;
            hasAnchor = true;
            anchorIsProvisional = true;
        }

        /// <summary>
        /// Anchors so the position lands in the middle of the phase at <paramref name="serverTick"/> (full
        /// midday / deep midnight). Used for forced @day/@night so they snap to the phase's final state.
        /// </summary>
        public void AnchorAtPhaseMidpoint(uint serverTick, bool isNight) {
            anchorServerTick = serverTick;
            anchorCycleOffsetMilliseconds = isNight
                ? dayDurationMilliseconds + nightDurationMilliseconds * 0.5
                : dayDurationMilliseconds * 0.5;
            hasAnchor = true;
            anchorIsProvisional = false;
        }

        private bool IsNearExpectedBoundary(uint serverTick, bool nightStarting) {
            if (!hasAnchor) {
                return false;
            }

            int deltaMilliseconds = unchecked((int) (serverTick - anchorServerTick));
            double offset = Modulo(anchorCycleOffsetMilliseconds + deltaMilliseconds, PeriodMilliseconds);
            double boundary = nightStarting ? dayDurationMilliseconds : 0.0;
            return CircularDistance(offset, boundary, PeriodMilliseconds) <= NaturalBoundaryToleranceMilliseconds;
        }

        private static double CircularDistance(double a, double b, double period) {
            double distance = Math.Abs(a - b) % period;
            return Math.Min(distance, period - distance);
        }

        /// <summary>Evaluates the cycle position at the given synchronized server tick.</summary>
        public bool TryEvaluate(uint serverTick, out RoServerDayNightSample sample) {
            if (!hasAnchor) {
                sample = default;
                return false;
            }

            double period = PeriodMilliseconds;
            // Tick is a 32-bit millisecond counter; the signed difference handles wrap as long as the
            // gap stays under ~24 days, which it always does (we re-anchor every phase boundary).
            int deltaMilliseconds = unchecked((int) (serverTick - anchorServerTick));
            double offset = Modulo(anchorCycleOffsetMilliseconds + deltaMilliseconds, period);
            bool isNight = offset >= dayDurationMilliseconds;
            double phaseDuration = isNight ? nightDurationMilliseconds : dayDurationMilliseconds;
            double phasePosition = isNight ? offset - dayDurationMilliseconds : offset;
            float phaseProgress = phaseDuration > 0.0
                ? (float) Clamp01(phasePosition / phaseDuration)
                : 0f;

            sample = new RoServerDayNightSample(
                isNight,
                (float) (offset / 1000.0),
                phaseProgress,
                (float) (offset / period));
            return true;
        }

        private static double Modulo(double value, double modulus) {
            double result = value % modulus;
            return result < 0.0 ? result + modulus : result;
        }

        private static double Clamp01(double value) {
            if (value < 0.0) {
                return 0.0;
            }

            return value > 1.0 ? 1.0 : value;
        }
    }
}
