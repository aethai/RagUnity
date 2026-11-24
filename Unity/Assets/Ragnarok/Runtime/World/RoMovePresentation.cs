using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.World {
    public enum RoMovePresentationCorrectionMode {
        None,
        CatchUp,
        Hold,
        SoftCorrection,
        HardSnap
    }

    public readonly struct RoMovePresentationSample {
        public RoMovePresentationSample(
            Vector3 position,
            int segmentIndex,
            float segmentStartSeconds,
            float segmentEndSeconds,
            float elapsedSeconds,
            bool isComplete
        ) {
            Position = position;
            SegmentIndex = segmentIndex;
            SegmentStartSeconds = segmentStartSeconds;
            SegmentEndSeconds = segmentEndSeconds;
            ElapsedSeconds = elapsedSeconds;
            IsComplete = isComplete;
        }

        public Vector3 Position { get; }
        public int SegmentIndex { get; }
        public float SegmentStartSeconds { get; }
        public float SegmentEndSeconds { get; }
        public float ElapsedSeconds { get; }
        public bool IsComplete { get; }
        public float SegmentDurationSeconds => Mathf.Max(0.001f, SegmentEndSeconds - SegmentStartSeconds);
        public float SegmentElapsedSeconds => Mathf.Clamp(ElapsedSeconds - SegmentStartSeconds, 0f, SegmentDurationSeconds);
        public float SegmentProgress => Mathf.Clamp01(SegmentElapsedSeconds / SegmentDurationSeconds);
    }

    public readonly struct RoMovePresentationCorrection {
        public RoMovePresentationCorrection(
            RoMovePresentationCorrectionMode mode,
            float targetElapsedSeconds,
            float leadSeconds,
            float distanceToAuthoritative
        ) {
            Mode = mode;
            TargetElapsedSeconds = targetElapsedSeconds;
            LeadSeconds = leadSeconds;
            DistanceToAuthoritative = distanceToAuthoritative;
        }

        public RoMovePresentationCorrectionMode Mode { get; }
        public float TargetElapsedSeconds { get; }
        public float LeadSeconds { get; }
        public float DistanceToAuthoritative { get; }
    }

    public static class RoMovePresentation {
        public const float DefaultLeadToleranceSeconds = 0.12f;
        public const float DefaultLeadToleranceCells = 0.35f;
        public const float DefaultCatchUpSeconds = 0.18f;
        public const float DefaultSoftCorrectionSeconds = 0.10f;
        public const float DefaultCorrectionOffsetMinSeconds = 0.10f;
        public const float DefaultCorrectionOffsetMaxSeconds = 0.18f;

        public static float CalculateSpeedScaledLeadToleranceSeconds(float straightStepMilliseconds) {
            float stepSeconds = Mathf.Max(0.001f, straightStepMilliseconds / 1000f);
            return Mathf.Clamp(stepSeconds * 0.5f, 0.025f, 0.08f);
        }

        public static float CalculateSpeedScaledLeadToleranceSeconds(float straightStepMilliseconds, RoMovementTuning tuning) {
            float scaledToleranceSeconds = CalculateSpeedScaledLeadToleranceSeconds(straightStepMilliseconds);
            float tuningToleranceSeconds = RoMovementTuning.Resolve(tuning).LeadToleranceSeconds;
            return Mathf.Min(scaledToleranceSeconds, Mathf.Max(0f, tuningToleranceSeconds));
        }

        public static float CalculateSpeedScaledContinuationLeadSeconds(float straightStepMilliseconds) {
            float stepSeconds = Mathf.Max(0.001f, straightStepMilliseconds / 1000f);
            return Mathf.Clamp(stepSeconds * 0.6f, 0.025f, 0.06f);
        }

        public static bool TryBuildSegmentEndTimes(
            IReadOnlyList<Vector2Int> cellPath,
            float straightStepMilliseconds,
            List<float> segmentEndTimes,
            out float durationSeconds
        ) {
            return TryBuildSegmentEndTimes(
                cellPath,
                null,
                false,
                straightStepMilliseconds,
                segmentEndTimes,
                out durationSeconds);
        }

        public static bool TryBuildSegmentEndTimes(
            IReadOnlyList<Vector2Int> cellPath,
            IReadOnlyList<Vector3> worldWaypoints,
            bool usePartialFirstStep,
            float straightStepMilliseconds,
            List<float> segmentEndTimes,
            out float durationSeconds
        ) {
            durationSeconds = 0f;
            if (segmentEndTimes == null) {
                return false;
            }

            segmentEndTimes.Clear();
            if (cellPath == null || cellPath.Count < 2) {
                return false;
            }

            for (int i = 1; i < cellPath.Count; i++) {
                float segmentSeconds;
                if (i == 1 &&
                    usePartialFirstStep &&
                    worldWaypoints != null &&
                    worldWaypoints.Count > 1) {
                    segmentSeconds = RoLiveMoveRoute.CalculatePartialFirstStepSeconds(
                        worldWaypoints[0],
                        worldWaypoints[1],
                        cellPath[0],
                        cellPath[1],
                        straightStepMilliseconds);
                } else {
                    segmentSeconds = RoLiveMoveRoute.CalculateServerStepSeconds(
                        cellPath[i - 1],
                        cellPath[i],
                        straightStepMilliseconds);
                }

                durationSeconds += Mathf.Max(0.001f, segmentSeconds);
                segmentEndTimes.Add(durationSeconds);
            }

            return segmentEndTimes.Count > 0;
        }

        public static bool TrySample(
            IReadOnlyList<Vector3> waypoints,
            IReadOnlyList<float> segmentEndTimes,
            float elapsedSeconds,
            out RoMovePresentationSample sample
        ) {
            sample = default;
            if (!RoMovementSampler.TrySample(
                    waypoints,
                    segmentEndTimes,
                    elapsedSeconds,
                    out RoMovementSample movementSample)) {
                return false;
            }

            sample = new RoMovePresentationSample(
                movementSample.Position,
                movementSample.SegmentIndex,
                movementSample.SegmentStartSeconds,
                movementSample.SegmentEndSeconds,
                movementSample.ElapsedSeconds,
                movementSample.IsComplete);
            return true;
        }

        public static bool TryProjectElapsed(
            IReadOnlyList<Vector3> waypoints,
            IReadOnlyList<float> segmentEndTimes,
            Vector3 position,
            out float elapsedSeconds,
            out float distanceToPath
        ) {
            elapsedSeconds = 0f;
            distanceToPath = 0f;
            if (waypoints == null || segmentEndTimes == null || waypoints.Count < 2 || segmentEndTimes.Count == 0) {
                return false;
            }

            int segmentCount = Mathf.Min(segmentEndTimes.Count, waypoints.Count - 1);
            float bestDistanceSqr = float.PositiveInfinity;
            float bestElapsed = 0f;
            Vector3 flatPosition = Flatten(position);
            for (int i = 0; i < segmentCount; i++) {
                Vector3 start = Flatten(waypoints[i]);
                Vector3 end = Flatten(waypoints[i + 1]);
                Vector3 segment = end - start;
                float segmentLengthSqr = segment.sqrMagnitude;
                float t = segmentLengthSqr > 0.0001f
                    ? Mathf.Clamp01(Vector3.Dot(flatPosition - start, segment) / segmentLengthSqr)
                    : 0f;
                Vector3 projected = Vector3.Lerp(start, end, t);
                float distanceSqr = Vector3.SqrMagnitude(flatPosition - projected);
                if (distanceSqr >= bestDistanceSqr) {
                    continue;
                }

                float segmentStartSeconds = i == 0 ? 0f : segmentEndTimes[i - 1];
                float segmentEndSeconds = segmentEndTimes[i];
                bestDistanceSqr = distanceSqr;
                bestElapsed = Mathf.Lerp(segmentStartSeconds, segmentEndSeconds, t);
            }

            elapsedSeconds = bestElapsed;
            distanceToPath = Mathf.Sqrt(bestDistanceSqr);
            return true;
        }

        public static RoMovePresentationCorrection CalculateInitialLocalCorrection(
            float visualElapsedSeconds,
            float serverElapsedSeconds,
            float totalDurationSeconds,
            float distanceToAuthoritative,
            float leadToleranceSeconds = DefaultLeadToleranceSeconds,
            float leadToleranceCells = DefaultLeadToleranceCells,
            float hardSnapDistanceCells = RoLiveMoveRoute.HardSnapDistanceCells
        ) {
            float duration = Mathf.Max(0f, totalDurationSeconds);
            float visualElapsed = Mathf.Clamp(visualElapsedSeconds, 0f, duration);
            float serverElapsed = Mathf.Clamp(serverElapsedSeconds, 0f, duration);
            float distance = Mathf.Max(0f, distanceToAuthoritative);
            float lead = visualElapsed - serverElapsed;
            if (distance > hardSnapDistanceCells) {
                return new RoMovePresentationCorrection(
                    RoMovePresentationCorrectionMode.HardSnap,
                    serverElapsed,
                    lead,
                    distance);
            }

            if (serverElapsed > visualElapsed) {
                return new RoMovePresentationCorrection(
                    RoMovePresentationCorrectionMode.CatchUp,
                    serverElapsed,
                    lead,
                    distance);
            }

            if (lead > Mathf.Max(0f, leadToleranceSeconds) || distance > Mathf.Max(0f, leadToleranceCells)) {
                float targetElapsed = lead > Mathf.Max(0f, leadToleranceSeconds)
                    ? Mathf.Min(visualElapsed, serverElapsed + Mathf.Max(0f, leadToleranceSeconds))
                    : visualElapsed;
                RoMovePresentationCorrectionMode mode = distance > Mathf.Max(0f, leadToleranceCells)
                    ? RoMovePresentationCorrectionMode.SoftCorrection
                    : RoMovePresentationCorrectionMode.Hold;
                return new RoMovePresentationCorrection(mode, targetElapsed, lead, distance);
            }

            return new RoMovePresentationCorrection(
                RoMovePresentationCorrectionMode.None,
                visualElapsed,
                lead,
                distance);
        }

        public static RoMovePresentationCorrection CalculateInitialLocalCorrection(
            float visualElapsedSeconds,
            float serverElapsedSeconds,
            float totalDurationSeconds,
            float distanceToAuthoritative,
            RoMovementTuning tuning,
            float leadToleranceCells = DefaultLeadToleranceCells
        ) {
            RoMovementTuning resolved = RoMovementTuning.Resolve(tuning);
            return CalculateInitialLocalCorrection(
                visualElapsedSeconds,
                serverElapsedSeconds,
                totalDurationSeconds,
                distanceToAuthoritative,
                resolved.LeadToleranceSeconds,
                leadToleranceCells,
                resolved.HardSnapCells);
        }

        public static RoMovePresentationCorrection AdvanceLocalElapsed(
            float presentedElapsedSeconds,
            float serverElapsedSeconds,
            float totalDurationSeconds,
            float deltaSeconds,
            float distanceToAuthoritative,
            float leadToleranceSeconds = DefaultLeadToleranceSeconds,
            float leadToleranceCells = DefaultLeadToleranceCells,
            float catchUpSeconds = DefaultCatchUpSeconds,
            float softCorrectionSeconds = DefaultSoftCorrectionSeconds,
            float hardSnapDistanceCells = RoLiveMoveRoute.HardSnapDistanceCells
        ) {
            float duration = Mathf.Max(0f, totalDurationSeconds);
            float presentedElapsed = Mathf.Clamp(presentedElapsedSeconds, 0f, duration);
            float serverElapsed = Mathf.Clamp(serverElapsedSeconds, 0f, duration);
            float delta = Mathf.Max(0f, deltaSeconds);
            float distance = Mathf.Max(0f, distanceToAuthoritative);
            float lead = presentedElapsed - serverElapsed;
            if (distance > hardSnapDistanceCells) {
                return new RoMovePresentationCorrection(
                    RoMovePresentationCorrectionMode.HardSnap,
                    serverElapsed,
                    lead,
                    distance);
            }

            float toleranceSeconds = Mathf.Max(0f, leadToleranceSeconds);
            float toleranceCells = Mathf.Max(0f, leadToleranceCells);
            float maxAllowedElapsed = Mathf.Min(duration, serverElapsed + toleranceSeconds);
            if (serverElapsed > presentedElapsed) {
                float gap = serverElapsed - presentedElapsed;
                float catchUpWindow = Mathf.Max(0.001f, catchUpSeconds);
                float catchUpStep = delta + gap * delta / catchUpWindow;
                return new RoMovePresentationCorrection(
                    RoMovePresentationCorrectionMode.CatchUp,
                    Mathf.Min(serverElapsed, presentedElapsed + catchUpStep),
                    lead,
                    distance);
            }

            if (lead > toleranceSeconds || distance > toleranceCells) {
                if (lead > toleranceSeconds) {
                    float leadLimitedTarget = CalculateLeadLimitedTargetElapsed(
                        presentedElapsed,
                        duration,
                        delta,
                        lead,
                        toleranceSeconds,
                        softCorrectionSeconds);
                    if (distance > toleranceCells && leadLimitedTarget > presentedElapsed) {
                        return new RoMovePresentationCorrection(
                            RoMovePresentationCorrectionMode.SoftCorrection,
                            leadLimitedTarget,
                            lead,
                            distance);
                    }

                    return new RoMovePresentationCorrection(
                        RoMovePresentationCorrectionMode.Hold,
                        leadLimitedTarget,
                        lead,
                        distance);
                }

                if (distance > toleranceCells) {
                    float correctionSeconds = Mathf.Max(0.001f, softCorrectionSeconds);
                    float target = Mathf.MoveTowards(
                        presentedElapsed,
                        maxAllowedElapsed,
                        Mathf.Max(delta, Mathf.Abs(presentedElapsed - maxAllowedElapsed) * delta / correctionSeconds));
                    return new RoMovePresentationCorrection(
                        RoMovePresentationCorrectionMode.SoftCorrection,
                        Mathf.Max(presentedElapsed, target),
                        lead,
                        distance);
                }

                return new RoMovePresentationCorrection(
                    RoMovePresentationCorrectionMode.Hold,
                    presentedElapsed,
                    lead,
                    distance);
            }

            float normalElapsed = Mathf.Min(duration, presentedElapsed + delta);
            return new RoMovePresentationCorrection(
                RoMovePresentationCorrectionMode.None,
                Mathf.Min(normalElapsed, maxAllowedElapsed),
                lead,
                distance);
        }

        private static float CalculateLeadLimitedTargetElapsed(
            float presentedElapsed,
            float durationSeconds,
            float deltaSeconds,
            float leadSeconds,
            float leadToleranceSeconds,
            float softCorrectionSeconds
        ) {
            if (deltaSeconds <= 0f || presentedElapsed >= durationSeconds) {
                return presentedElapsed;
            }

            float leadExcess = Mathf.Max(0f, leadSeconds - leadToleranceSeconds);
            if (leadExcess <= 0f) {
                return Mathf.Min(durationSeconds, presentedElapsed + deltaSeconds);
            }

            float correctionWindow = Mathf.Max(0.001f, softCorrectionSeconds);
            float speedScale = Mathf.Clamp01(1f - leadExcess / correctionWindow);
            if (speedScale <= 0f) {
                return presentedElapsed;
            }

            return Mathf.Min(durationSeconds, presentedElapsed + deltaSeconds * speedScale);
        }

        public static RoMovePresentationCorrection AdvanceLocalElapsed(
            float presentedElapsedSeconds,
            float serverElapsedSeconds,
            float totalDurationSeconds,
            float deltaSeconds,
            float distanceToAuthoritative,
            RoMovementTuning tuning,
            float leadToleranceCells = DefaultLeadToleranceCells,
            float hardSnapDistanceCells = float.NaN
        ) {
            RoMovementTuning resolved = RoMovementTuning.Resolve(tuning);
            float hardSnapCells = float.IsNaN(hardSnapDistanceCells)
                ? resolved.HardSnapCells
                : hardSnapDistanceCells;
            return AdvanceLocalElapsed(
                presentedElapsedSeconds,
                serverElapsedSeconds,
                totalDurationSeconds,
                deltaSeconds,
                distanceToAuthoritative,
                resolved.LeadToleranceSeconds,
                leadToleranceCells,
                resolved.CatchupSeconds,
                resolved.SoftCorrectionSeconds,
                hardSnapCells);
        }

        public static bool IsServerAuthoritativeMoveComplete(float serverElapsedSeconds, float totalDurationSeconds) {
            return Mathf.Max(0f, serverElapsedSeconds) >= Mathf.Max(0f, totalDurationSeconds) - 0.0001f;
        }

        public static float CalculateCorrectionOffsetDurationSeconds(
            float offsetDistanceCells,
            float hardSnapDistanceCells = RoLiveMoveRoute.HardSnapDistanceCells,
            float minimumSeconds = DefaultCorrectionOffsetMinSeconds,
            float maximumSeconds = DefaultCorrectionOffsetMaxSeconds
        ) {
            float minimum = Mathf.Max(0.001f, minimumSeconds);
            float maximum = Mathf.Max(minimum, maximumSeconds);
            float range = Mathf.Max(0.001f, hardSnapDistanceCells);
            float t = Mathf.Clamp01(Mathf.Max(0f, offsetDistanceCells) / range);
            return Mathf.Lerp(minimum, maximum, t);
        }

        public static float CalculateCorrectionOffsetDurationSeconds(float offsetDistanceCells, RoMovementTuning tuning) {
            RoMovementTuning resolved = RoMovementTuning.Resolve(tuning);
            return CalculateCorrectionOffsetDurationSeconds(
                offsetDistanceCells,
                resolved.HardSnapCells,
                resolved.SoftCorrectionSeconds,
                Mathf.Max(DefaultCorrectionOffsetMaxSeconds, resolved.SoftCorrectionSeconds));
        }

        public static Vector3 CalculateCorrectionOffset(Vector3 visualPosition, Vector3 authoritativePosition) {
            return visualPosition - authoritativePosition;
        }

        public static Vector3 DecayCorrectionOffset(Vector3 offset, float elapsedSeconds, float durationSeconds) {
            if (durationSeconds <= 0f) {
                return Vector3.zero;
            }

            float progress = Mathf.Clamp01(Mathf.Max(0f, elapsedSeconds) / durationSeconds);
            return Vector3.Lerp(offset, Vector3.zero, progress);
        }

        public static Vector3 ApplyCorrectionOffset(
            Vector3 authoritativePosition,
            Vector3 offset,
            float elapsedSeconds,
            float durationSeconds
        ) {
            return authoritativePosition + DecayCorrectionOffset(offset, elapsedSeconds, durationSeconds);
        }

        public static float FlatDistance(Vector3 a, Vector3 b) {
            return RoMovementSampler.FlatDistance(a, b);
        }

        private static Vector3 Flatten(Vector3 value) {
            value.y = 0f;
            return value;
        }
    }

    public static class RoFacingResolver {
        private const float DirectionEpsilonSqr = 0.001f;

        public static Vector3 ResolveWalkLookDelta(
            IReadOnlyList<Vector3> routeWaypoints,
            int segmentIndex,
            Vector3 currentPosition,
            Vector3 fallbackPathDelta,
            Vector3 fallbackVisualDelta
        ) {
            if (routeWaypoints != null && routeWaypoints.Count > 1) {
                int clampedSegment = Mathf.Clamp(segmentIndex, 0, routeWaypoints.Count - 2);
                Vector3 look = ResolveWalkLookDelta(
                    currentPosition,
                    routeWaypoints[clampedSegment],
                    routeWaypoints[clampedSegment + 1],
                    clampedSegment,
                    fallbackPathDelta);
                if (look.sqrMagnitude > DirectionEpsilonSqr) {
                    return look;
                }
            }

            fallbackPathDelta.y = 0f;
            if (fallbackPathDelta.sqrMagnitude > DirectionEpsilonSqr) {
                return fallbackPathDelta;
            }

            fallbackVisualDelta.y = 0f;
            return fallbackVisualDelta.sqrMagnitude > DirectionEpsilonSqr ? fallbackVisualDelta : Vector3.zero;
        }

        public static Vector3 ResolveWalkLookDelta(
            Vector3 currentPosition,
            Vector3 segmentStartWorld,
            Vector3 segmentEndWorld,
            int segmentIndex,
            Vector3 fallbackDelta
        ) {
            Vector3 segmentDelta = Flatten(segmentEndWorld - segmentStartWorld);
            if (segmentIndex <= 0) {
                Vector3 continuousDelta = Flatten(segmentEndWorld - currentPosition);
                if (continuousDelta.sqrMagnitude > DirectionEpsilonSqr &&
                    (segmentDelta.sqrMagnitude <= DirectionEpsilonSqr ||
                     Vector3.Dot(continuousDelta, segmentDelta) > 0f)) {
                    return continuousDelta;
                }
            }

            if (segmentDelta.sqrMagnitude > DirectionEpsilonSqr) {
                return segmentDelta;
            }

            fallbackDelta.y = 0f;
            return fallbackDelta.sqrMagnitude > DirectionEpsilonSqr ? fallbackDelta : Vector3.zero;
        }

        private static Vector3 Flatten(Vector3 value) {
            value.y = 0f;
            return value;
        }
    }
}
