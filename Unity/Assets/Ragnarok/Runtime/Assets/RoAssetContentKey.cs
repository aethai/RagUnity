using System;

namespace Ragnarok.Assets {
    /// <summary>
    /// Identifies a file's stored bytes rather than its name. GRF entries that point at the same block get the same
    /// key. data.grf stores one copy per content: for example, every job's sprite of a robe is one block, which is
    /// ~19k .spr names. Caches keyed by this key decode and upload such content once.
    /// </summary>
    public readonly struct RoAssetContentKey : IEquatable<RoAssetContentKey> {
        public RoAssetContentKey(int sourceIndex, ulong offset, uint length) {
            SourceIndex = sourceIndex;
            Offset = offset;
            Length = length;
        }

        public int SourceIndex { get; }
        public ulong Offset { get; }
        public uint Length { get; }

        public bool Equals(RoAssetContentKey other) {
            return SourceIndex == other.SourceIndex && Offset == other.Offset && Length == other.Length;
        }

        public override bool Equals(object obj) => obj is RoAssetContentKey other && Equals(other);

        public override int GetHashCode() {
            unchecked {
                int hash = SourceIndex;
                hash = (hash * 397) ^ Offset.GetHashCode();
                hash = (hash * 397) ^ (int) Length;
                return hash;
            }
        }

        public override string ToString() => $"{SourceIndex}:{Offset:x}:{Length:x}";
    }
}
