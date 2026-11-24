using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.World {
    public static class RoLiveMoveRoute {
        public const int StraightStepCost = 10;
        public const int DiagonalStepCost = 14;
        public const int DefaultStraightStepMilliseconds = 150;
        public const int MaxWalkPathSteps = 32;
        public const float HardSnapDistanceCells = 2f;

        public static int CalculateServerStepMilliseconds(float straightStepMilliseconds, bool diagonal) {
            int speed = Math.Max(1, Mathf.RoundToInt(straightStepMilliseconds));
            return diagonal
                ? Math.Max(1, speed * DiagonalStepCost / StraightStepCost)
                : speed;
        }

        public static int CalculateServerPathDurationMilliseconds(
            IReadOnlyList<Vector2Int> path,
            float straightStepMilliseconds
        ) {
            if (path == null || path.Count < 2) {
                return 0;
            }

            int total = 0;
            for (int i = 1; i < path.Count; i++) {
                total += CalculateServerStepMilliseconds(
                    straightStepMilliseconds,
                    IsDiagonalStep(path[i - 1], path[i]));
            }

            return total;
        }

        public static float CalculateServerStepSeconds(
            Vector2Int fromCell,
            Vector2Int toCell,
            float straightStepMilliseconds
        ) {
            return CalculateServerStepMilliseconds(straightStepMilliseconds, IsDiagonalStep(fromCell, toCell)) / 1000f;
        }

        public static int CalculateServerDeltaDurationMilliseconds(
            int deltaX,
            int deltaY,
            float straightStepMilliseconds
        ) {
            int dx = Math.Abs(deltaX);
            int dy = Math.Abs(deltaY);
            int diagonalSteps = Math.Min(dx, dy);
            int straightSteps = Math.Max(dx, dy) - diagonalSteps;
            int straightStep = CalculateServerStepMilliseconds(straightStepMilliseconds, diagonal: false);
            int diagonalStep = CalculateServerStepMilliseconds(straightStepMilliseconds, diagonal: true);
            return Math.Max(1, straightSteps * straightStep + diagonalSteps * diagonalStep);
        }

        public static float CalculatePartialFirstStepSeconds(
            Vector3 currentWorld,
            Vector3 targetWorld,
            Vector2Int fromCell,
            Vector2Int toCell,
            float straightStepMilliseconds
        ) {
            float fullStepSeconds = CalculateServerStepSeconds(fromCell, toCell, straightStepMilliseconds);
            float fullDistance = IsDiagonalStep(fromCell, toCell) ? Mathf.Sqrt(2f) : 1f;
            float remainingDistance = Vector3.Distance(Flatten(currentWorld), Flatten(targetWorld));
            float ratio = fullDistance > 0.0001f
                ? Mathf.Clamp01(remainingDistance / fullDistance)
                : 1f;
            return Mathf.Max(0.001f, fullStepSeconds * ratio);
        }

        public static bool IsDiagonalStep(Vector2Int fromCell, Vector2Int toCell) {
            return fromCell.x != toCell.x && fromCell.y != toCell.y;
        }

        private static Vector3 Flatten(Vector3 value) {
            value.y = 0f;
            return value;
        }
    }
}
