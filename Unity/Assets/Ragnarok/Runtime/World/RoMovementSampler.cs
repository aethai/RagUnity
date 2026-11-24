using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.World {
    public readonly struct RoMovementSample {
        public RoMovementSample(
            Vector3 position,
            int segmentIndex,
            float segmentStartSeconds,
            float segmentEndSeconds,
            float elapsedSeconds,
            float distanceCellsFromStart,
            float segmentDistanceCells,
            bool isComplete
        ) {
            Position = position;
            SegmentIndex = segmentIndex;
            SegmentStartSeconds = segmentStartSeconds;
            SegmentEndSeconds = segmentEndSeconds;
            ElapsedSeconds = elapsedSeconds;
            DistanceCellsFromStart = distanceCellsFromStart;
            SegmentDistanceCells = segmentDistanceCells;
            IsComplete = isComplete;
        }

        public Vector3 Position { get; }
        public int SegmentIndex { get; }
        public float SegmentStartSeconds { get; }
        public float SegmentEndSeconds { get; }
        public float ElapsedSeconds { get; }
        public float DistanceCellsFromStart { get; }
        public float SegmentDistanceCells { get; }
        public bool IsComplete { get; }
        public int FacingSegmentIndex => SegmentIndex;
        public Vector3 FacingCurrentPosition => Position;
        public float SegmentDurationSeconds => Mathf.Max(0.001f, SegmentEndSeconds - SegmentStartSeconds);
        public float SegmentElapsedSeconds => Mathf.Clamp(ElapsedSeconds - SegmentStartSeconds, 0f, SegmentDurationSeconds);
        public float SegmentProgress => Mathf.Clamp01(SegmentElapsedSeconds / SegmentDurationSeconds);
    }

    public static class RoMovementSampler {
        public static bool TrySample(
            IReadOnlyList<Vector3> waypoints,
            IReadOnlyList<float> segmentEndTimes,
            float elapsedSeconds,
            out RoMovementSample sample
        ) {
            sample = default;
            if (waypoints == null || segmentEndTimes == null || waypoints.Count < 2 || segmentEndTimes.Count == 0) {
                return false;
            }

            int segmentCount = Math.Min(segmentEndTimes.Count, waypoints.Count - 1);
            if (segmentCount <= 0) {
                return false;
            }

            float totalDuration = Mathf.Max(0f, segmentEndTimes[segmentCount - 1]);
            float elapsed = Mathf.Clamp(Mathf.Max(0f, elapsedSeconds), 0f, totalDuration);
            float distanceBeforeSegment = 0f;
            for (int i = 0; i < segmentCount; i++) {
                Vector3 segmentStartPosition = waypoints[i];
                Vector3 segmentEndPosition = waypoints[i + 1];
                float segmentDistance = FlatDistance(segmentStartPosition, segmentEndPosition);
                float segmentEnd = Mathf.Max(0f, segmentEndTimes[i]);
                float segmentStart = i == 0 ? 0f : Mathf.Max(0f, segmentEndTimes[i - 1]);
                if (elapsed > segmentEnd && i + 1 < segmentCount) {
                    distanceBeforeSegment += segmentDistance;
                    continue;
                }

                float segmentDuration = Mathf.Max(0.001f, segmentEnd - segmentStart);
                float progress = Mathf.Clamp01((elapsed - segmentStart) / segmentDuration);
                Vector3 position = Vector3.Lerp(segmentStartPosition, segmentEndPosition, progress);
                sample = new RoMovementSample(
                    position,
                    i,
                    segmentStart,
                    segmentEnd,
                    elapsed,
                    distanceBeforeSegment + segmentDistance * progress,
                    segmentDistance,
                    elapsed >= totalDuration - 0.0001f);
                return true;
            }

            int lastSegment = segmentCount - 1;
            float finalDistance = CalculatePathDistance(waypoints, segmentCount + 1);
            sample = new RoMovementSample(
                waypoints[segmentCount],
                lastSegment,
                lastSegment == 0 ? 0f : Mathf.Max(0f, segmentEndTimes[lastSegment - 1]),
                Mathf.Max(0f, segmentEndTimes[lastSegment]),
                totalDuration,
                finalDistance,
                FlatDistance(waypoints[lastSegment], waypoints[segmentCount]),
                true);
            return true;
        }

        public static bool TrySampleByDuration(
            IReadOnlyList<Vector3> waypoints,
            float elapsedSeconds,
            float durationSeconds,
            float totalDistanceCells,
            out RoMovementSample sample
        ) {
            sample = default;
            if (waypoints == null || waypoints.Count < 2) {
                return false;
            }

            float totalDistance = totalDistanceCells > 0.0001f
                ? totalDistanceCells
                : CalculatePathDistance(waypoints);
            if (totalDistance <= 0.0001f) {
                sample = SampleLinear(waypoints[0], waypoints[waypoints.Count - 1], elapsedSeconds, durationSeconds);
                return true;
            }

            float duration = Mathf.Max(0f, durationSeconds);
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, elapsedSeconds) / duration);
            float elapsed = duration * progress;
            float targetDistance = totalDistance * progress;
            float distanceBeforeSegment = 0f;
            int lastSegment = waypoints.Count - 2;
            for (int i = 0; i <= lastSegment; i++) {
                Vector3 segmentStartPosition = waypoints[i];
                Vector3 segmentEndPosition = waypoints[i + 1];
                float segmentDistance = FlatDistance(segmentStartPosition, segmentEndPosition);
                if (segmentDistance <= 0.0001f) {
                    continue;
                }

                float segmentEndDistance = distanceBeforeSegment + segmentDistance;
                if (targetDistance > segmentEndDistance && i < lastSegment) {
                    distanceBeforeSegment = segmentEndDistance;
                    continue;
                }

                float segmentProgress = Mathf.Clamp01((targetDistance - distanceBeforeSegment) / segmentDistance);
                float segmentStartSeconds = duration * (distanceBeforeSegment / totalDistance);
                float segmentEndSeconds = duration * (segmentEndDistance / totalDistance);
                sample = new RoMovementSample(
                    Vector3.Lerp(segmentStartPosition, segmentEndPosition, segmentProgress),
                    i,
                    segmentStartSeconds,
                    segmentEndSeconds,
                    elapsed,
                    distanceBeforeSegment + segmentDistance * segmentProgress,
                    segmentDistance,
                    progress >= 1f);
                return true;
            }

            sample = new RoMovementSample(
                waypoints[waypoints.Count - 1],
                lastSegment,
                duration,
                duration,
                elapsed,
                totalDistance,
                FlatDistance(waypoints[lastSegment], waypoints[waypoints.Count - 1]),
                true);
            return true;
        }

        public static RoMovementSample SampleLinear(
            Vector3 start,
            Vector3 end,
            float elapsedSeconds,
            float durationSeconds
        ) {
            float duration = Mathf.Max(0f, durationSeconds);
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, elapsedSeconds) / duration);
            float elapsed = duration * progress;
            float distance = FlatDistance(start, end);
            return new RoMovementSample(
                Vector3.Lerp(start, end, progress),
                0,
                0f,
                duration,
                elapsed,
                distance * progress,
                distance,
                progress >= 1f);
        }

        public static float CalculatePathDistance(IReadOnlyList<Vector3> waypoints) {
            return CalculatePathDistance(waypoints, waypoints?.Count ?? 0);
        }

        public static float FlatDistance(Vector3 a, Vector3 b) {
            return Vector3.Distance(Flatten(a), Flatten(b));
        }

        private static float CalculatePathDistance(IReadOnlyList<Vector3> waypoints, int count) {
            if (waypoints == null || count < 2) {
                return 0f;
            }

            int clampedCount = Math.Min(count, waypoints.Count);
            float distance = 0f;
            for (int i = 1; i < clampedCount; i++) {
                distance += FlatDistance(waypoints[i - 1], waypoints[i]);
            }

            return distance;
        }

        private static Vector3 Flatten(Vector3 value) {
            value.y = 0f;
            return value;
        }
    }
}
