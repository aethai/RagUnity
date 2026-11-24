using System;
using ROIO.Utils;

namespace ROIO.Loaders {
    internal static class RoLoaderValidation {
        public const int MaxMapDimension = 50000;
        public const int MaxMapCells = 16000000;
        public const int MaxCollectionCount = 100000;
        public const int MaxTexturePathLength = 4096;
        public const int MaxSpriteDimension = 8192;
        public const int MaxSpritePixels = 16777216;
        public const int MaxModelNodeCount = 10000;
        public const int MaxModelElementCount = 100000;

        public static int RequireCount(long count, int max, string name, string context) {
            if (count < 0 || count > max) {
                throw new Exception($"{context}: Invalid {name} count {count}; expected 0..{max}.");
            }

            return checked((int) count);
        }

        public static int RequirePositiveCount(long count, int max, string name, string context) {
            if (count <= 0 || count > max) {
                throw new Exception($"{context}: Invalid {name} count {count}; expected 1..{max}.");
            }

            return checked((int) count);
        }

        public static int RequireMapElementCount(uint width, uint height, string context) {
            if (width == 0 || height == 0 || width > MaxMapDimension || height > MaxMapDimension) {
                throw new Exception($"{context}: Invalid dimensions {width}x{height}; expected non-zero dimensions up to {MaxMapDimension}.");
            }

            ulong total = (ulong) width * height;
            if (total > MaxMapCells) {
                throw new Exception($"{context}: Invalid cell count {total}; maximum is {MaxMapCells}.");
            }

            return checked((int) total);
        }

        public static int RequireSpritePixelCount(int width, int height, string context) {
            if (width <= 0 || height <= 0 || width > MaxSpriteDimension || height > MaxSpriteDimension) {
                throw new Exception($"{context}: Invalid frame dimensions {width}x{height}; expected non-zero dimensions up to {MaxSpriteDimension}.");
            }

            long pixels = (long) width * height;
            if (pixels > MaxSpritePixels) {
                throw new Exception($"{context}: Invalid frame pixel count {pixels}; maximum is {MaxSpritePixels}.");
            }

            return checked((int) pixels);
        }

        public static void RequireRemaining(MemoryStreamReader data, long byteCount, string context) {
            if (byteCount < 0 || data.Position + byteCount > data.Length) {
                throw new Exception($"{context}: Truncated data at offset {data.Position}; need {byteCount} byte(s), have {Math.Max(0, data.Length - data.Position)}.");
            }
        }

        public static int RequireElementBytes(long count, long bytesPerElement, int max, string name, string context) {
            int validatedCount = RequireCount(count, max, name, context);
            checked {
                long byteCount = validatedCount * bytesPerElement;
                if (byteCount > int.MaxValue) {
                    throw new Exception($"{context}: Invalid {name} byte count {byteCount}; maximum is {int.MaxValue}.");
                }

                return (int) byteCount;
            }
        }

        public static byte ReadByte(MemoryStreamReader data, string context) {
            RequireRemaining(data, 1, context);
            int value = data.ReadByte();
            if (value < 0) {
                throw new Exception($"{context}: Truncated data at offset {data.Position}; need 1 byte(s), have 0.");
            }

            return (byte) value;
        }

        public static int ReadInt(MemoryStreamReader data, string context) {
            RequireRemaining(data, 4, context);
            return data.ReadInt();
        }

        public static uint ReadUInt(MemoryStreamReader data, string context) {
            RequireRemaining(data, 4, context);
            return data.ReadUInt();
        }

        public static ushort ReadUShort(MemoryStreamReader data, string context) {
            RequireRemaining(data, 2, context);
            return data.ReadUShort();
        }

        public static float ReadFloat(MemoryStreamReader data, string context) {
            RequireRemaining(data, 4, context);
            return data.ReadFloat();
        }

        public static string ReadBinaryString(MemoryStreamReader data, int length, string context) {
            RequireRemaining(data, length, context);
            return data.ReadBinaryString(length);
        }
    }
}
