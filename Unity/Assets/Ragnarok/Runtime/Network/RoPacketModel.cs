using System;

namespace Ragnarok.Network {
    public enum RoPacketDirection {
        Unknown = 0,
        ClientToServer = 1,
        ServerToClient = 2
    }

    public enum RoPacketFamily {
        Unknown = 0,
        Ca = 1,
        Ac = 2,
        Ch = 3,
        Hc = 4,
        Cz = 5,
        Zc = 6,
        Sc = 7
    }

    public readonly struct RoPacketPosition : IEquatable<RoPacketPosition> {
        public const int MaxCoordinate = 0x3ff;
        public const int MaxDirection = 0x0f;

        public RoPacketPosition(int x, int y, int direction) {
            if (x < 0 || x > MaxCoordinate) {
                throw new ArgumentOutOfRangeException(nameof(x), x, "RO packet coordinates are 10-bit values.");
            }

            if (y < 0 || y > MaxCoordinate) {
                throw new ArgumentOutOfRangeException(nameof(y), y, "RO packet coordinates are 10-bit values.");
            }

            if (direction < 0 || direction > MaxDirection) {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "RO packet directions are 4-bit values.");
            }

            X = x;
            Y = y;
            Direction = direction;
        }

        public int X { get; }
        public int Y { get; }
        public int Direction { get; }

        public bool Equals(RoPacketPosition other) {
            return X == other.X && Y == other.Y && Direction == other.Direction;
        }

        public override bool Equals(object obj) {
            return obj is RoPacketPosition other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Direction;
                return hash;
            }
        }

        public override string ToString() {
            return $"{X},{Y}:{Direction}";
        }
    }

    public readonly struct RoPacketPositionPair : IEquatable<RoPacketPositionPair> {
        public RoPacketPositionPair(int startX, int startY, int endX, int endY) {
            StartX = ValidateCoordinate(startX, nameof(startX));
            StartY = ValidateCoordinate(startY, nameof(startY));
            EndX = ValidateCoordinate(endX, nameof(endX));
            EndY = ValidateCoordinate(endY, nameof(endY));
        }

        public int StartX { get; }
        public int StartY { get; }
        public int EndX { get; }
        public int EndY { get; }

        public bool Equals(RoPacketPositionPair other) {
            return StartX == other.StartX &&
                StartY == other.StartY &&
                EndX == other.EndX &&
                EndY == other.EndY;
        }

        public override bool Equals(object obj) {
            return obj is RoPacketPositionPair other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = StartX;
                hash = (hash * 397) ^ StartY;
                hash = (hash * 397) ^ EndX;
                hash = (hash * 397) ^ EndY;
                return hash;
            }
        }

        public override string ToString() {
            return $"{StartX},{StartY}->{EndX},{EndY}";
        }

        private static int ValidateCoordinate(int value, string name) {
            if (value < 0 || value > RoPacketPosition.MaxCoordinate) {
                throw new ArgumentOutOfRangeException(name, value, "RO packet coordinates are 10-bit values.");
            }

            return value;
        }
    }
}
