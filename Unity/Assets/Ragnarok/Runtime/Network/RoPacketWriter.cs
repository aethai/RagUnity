using System;
using System.Text;

namespace Ragnarok.Network {
    public sealed class RoPacketWriter {
        private byte[] buffer;
        private int count;

        public RoPacketWriter(int initialCapacity = 64) {
            if (initialCapacity <= 0) {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            buffer = new byte[initialCapacity];
        }

        public int Count => count;

        public void Clear() {
            count = 0;
        }

        public void WriteByte(byte value) {
            Ensure(1);
            buffer[count++] = value;
        }

        public void WriteInt16(short value) {
            WriteUInt16(unchecked((ushort) value));
        }

        public void WriteUInt16(ushort value) {
            Ensure(2);
            buffer[count++] = (byte) value;
            buffer[count++] = (byte) (value >> 8);
        }

        public void WriteInt32(int value) {
            WriteUInt32(unchecked((uint) value));
        }

        public void WriteUInt32(uint value) {
            Ensure(4);
            buffer[count++] = (byte) value;
            buffer[count++] = (byte) (value >> 8);
            buffer[count++] = (byte) (value >> 16);
            buffer[count++] = (byte) (value >> 24);
        }

        public void WriteInt64(long value) {
            WriteUInt64(unchecked((ulong) value));
        }

        public void WriteUInt64(ulong value) {
            Ensure(8);
            buffer[count++] = (byte) value;
            buffer[count++] = (byte) (value >> 8);
            buffer[count++] = (byte) (value >> 16);
            buffer[count++] = (byte) (value >> 24);
            buffer[count++] = (byte) (value >> 32);
            buffer[count++] = (byte) (value >> 40);
            buffer[count++] = (byte) (value >> 48);
            buffer[count++] = (byte) (value >> 56);
        }

        public void WriteBytes(byte[] value) {
            if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }

            WriteBytes(value, 0, value.Length);
        }

        public void WriteBytes(byte[] value, int offset, int length) {
            if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }

            if (offset < 0 || length < 0 || offset + length > value.Length) {
                throw new ArgumentOutOfRangeException(nameof(length), "Write range must fit inside the supplied buffer.");
            }

            Ensure(length);
            Array.Copy(value, offset, buffer, count, length);
            count += length;
        }

        public void WriteFixedString(string value, int byteCount, Encoding encoding = null) {
            if (byteCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            }

            Ensure(byteCount);
            Array.Clear(buffer, count, byteCount);

            if (!string.IsNullOrEmpty(value) && byteCount > 0) {
                byte[] encoded = (encoding ?? Encoding.ASCII).GetBytes(value);
                Array.Copy(encoded, 0, buffer, count, Math.Min(encoded.Length, byteCount));
            }

            count += byteCount;
        }

        public void WritePosition(RoPacketPosition position) {
            WriteByte((byte) (position.X >> 2));
            WriteByte((byte) (((position.X & 0x03) << 6) | ((position.Y >> 4) & 0x3f)));
            WriteByte((byte) (((position.Y & 0x0f) << 4) | (position.Direction & 0x0f)));
        }

        public void WritePositionPair(RoPacketPositionPair position) {
            WriteByte((byte) (position.StartX >> 2));
            WriteByte((byte) (((position.StartX & 0x03) << 6) | ((position.StartY >> 4) & 0x3f)));
            WriteByte((byte) (((position.StartY & 0x0f) << 4) | ((position.EndX >> 6) & 0x0f)));
            WriteByte((byte) (((position.EndX & 0x3f) << 2) | ((position.EndY >> 8) & 0x03)));
            WriteByte((byte) position.EndY);
        }

        public byte[] ToArray() {
            var result = new byte[count];
            Array.Copy(buffer, result, count);
            return result;
        }

        private void Ensure(int additionalBytes) {
            int required = count + additionalBytes;
            if (required <= buffer.Length) {
                return;
            }

            int capacity = buffer.Length;
            while (capacity < required) {
                capacity *= 2;
            }

            Array.Resize(ref buffer, capacity);
        }
    }
}
