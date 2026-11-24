using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    public readonly struct RoWalkCell : IEquatable<RoWalkCell> {
        public RoWalkCell(int x, int y) {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(RoWalkCell other) {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj) {
            return obj is RoWalkCell other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString() {
            return $"{X},{Y}";
        }
    }

    public readonly struct RoMovementRequest {
        public RoMovementRequest(int x, int y, int direction = 0) {
            X = ValidateCoordinate(x, nameof(x));
            Y = ValidateCoordinate(y, nameof(y));
            Direction = ValidateDirection(direction);
        }

        public int X { get; }
        public int Y { get; }
        public int Direction { get; }

        private static int ValidateCoordinate(int value, string name) {
            if (value < 0 || value > RoPacketPosition.MaxCoordinate) {
                throw new ArgumentOutOfRangeException(name, value, "RO movement coordinates are 10-bit values.");
            }

            return value;
        }

        private static int ValidateDirection(int direction) {
            if (direction < 0 || direction > 7) {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "RO movement direction must be in the range 0..7.");
            }

            return direction;
        }
    }

    public enum RoClickMoveTargetStatus {
        Accepted = 0,
        OutOfBounds = 1,
        NotWalkable = 2
    }

    public readonly struct RoClickMoveTargetResult {
        public RoClickMoveTargetResult(
            RoClickMoveTargetStatus status,
            RoMovementRequest request,
            string message) {
            Status = status;
            Request = request;
            Message = message ?? string.Empty;
        }

        public RoClickMoveTargetStatus Status { get; }
        public RoMovementRequest Request { get; }
        public string Message { get; }
        public bool Accepted => Status == RoClickMoveTargetStatus.Accepted;
    }

    public sealed class RoWalkSurfaceGrid {
        private readonly bool[] walkable;
        private readonly float[] heights;

        public RoWalkSurfaceGrid(int width, int height, bool[] walkable, float[] heights = null) {
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            int count = checked(width * height);
            if (walkable == null) {
                throw new ArgumentNullException(nameof(walkable));
            }

            if (walkable.Length != count) {
                throw new ArgumentException("Walkability array length must match width * height.", nameof(walkable));
            }

            if (heights != null && heights.Length != count) {
                throw new ArgumentException("Height array length must match width * height.", nameof(heights));
            }

            Width = width;
            Height = height;
            this.walkable = new bool[count];
            Array.Copy(walkable, this.walkable, count);

            if (heights != null) {
                this.heights = new float[count];
                Array.Copy(heights, this.heights, count);
            }
        }

        public int Width { get; }
        public int Height { get; }

        public bool Contains(int x, int y) {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public bool IsInsideMap(int x, int y) {
            return Contains(x, y);
        }

        public bool CanWalk(int x, int y) {
            return Contains(x, y) && walkable[ToIndex(x, y)];
        }

        public bool TryGetHeight(int x, int y, out float height) {
            if (heights == null || !Contains(x, y)) {
                height = 0f;
                return false;
            }

            height = heights[ToIndex(x, y)];
            return true;
        }

        public bool TryClampToMap(int x, int y, out RoWalkCell cell) {
            if (Width <= 0 || Height <= 0) {
                cell = default;
                return false;
            }

            int clampedX = Clamp(x, 0, Width - 1);
            int clampedY = Clamp(y, 0, Height - 1);
            cell = new RoWalkCell(clampedX, clampedY);
            return true;
        }

        public bool TryBuildSimplePath(RoWalkCell start, RoWalkCell target, List<RoWalkCell> path) {
            if (path == null) {
                throw new ArgumentNullException(nameof(path));
            }

            path.Clear();
            if (!CanWalk(start.X, start.Y) || !CanWalk(target.X, target.Y)) {
                return false;
            }

            path.Add(start);
            int x = start.X;
            int y = start.Y;
            int guard = Width * Height;
            while ((x != target.X || y != target.Y) && guard-- > 0) {
                int nextX = x + Math.Sign(target.X - x);
                int nextY = y + Math.Sign(target.Y - y);
                if (!CanStep(x, y, nextX, nextY)) {
                    path.Clear();
                    return false;
                }

                x = nextX;
                y = nextY;
                path.Add(new RoWalkCell(x, y));
            }

            return x == target.X && y == target.Y;
        }

        private bool CanStep(int fromX, int fromY, int toX, int toY) {
            if (!CanWalk(toX, toY)) {
                return false;
            }

            int deltaX = toX - fromX;
            int deltaY = toY - fromY;
            if (deltaX == 0 || deltaY == 0) {
                return true;
            }

            return CanWalk(fromX + deltaX, fromY) &&
                CanWalk(fromX, fromY + deltaY);
        }

        private int ToIndex(int x, int y) {
            return x + y * Width;
        }

        private static int Clamp(int value, int min, int max) {
            if (value < min) {
                return min;
            }

            return value > max ? max : value;
        }
    }

    public static class RoClickMovePlanner {
        public static RoClickMoveTargetResult ValidateTileTarget(
            RoWalkSurfaceGrid surface,
            int targetX,
            int targetY,
            int direction = 0) {
            if (surface == null) {
                throw new ArgumentNullException(nameof(surface));
            }

            if (!surface.Contains(targetX, targetY)) {
                return new RoClickMoveTargetResult(
                    RoClickMoveTargetStatus.OutOfBounds,
                    default,
                    $"Target {targetX},{targetY} is outside the walk surface.");
            }

            if (!surface.CanWalk(targetX, targetY)) {
                return new RoClickMoveTargetResult(
                    RoClickMoveTargetStatus.NotWalkable,
                    default,
                    $"Target {targetX},{targetY} is not walkable.");
            }

            return new RoClickMoveTargetResult(
                RoClickMoveTargetStatus.Accepted,
                new RoMovementRequest(targetX, targetY, direction),
                string.Empty);
        }
    }
}
