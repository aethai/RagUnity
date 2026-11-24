using System;
using System.IO;
using System.Text;

namespace Ragnarok.Network {
    public sealed class RoPacketReader {
        private readonly byte[] buffer;
        private readonly int offset;
        private readonly int length;
        private int position;

        public RoPacketReader(byte[] data) : this(data, 0, data == null ? 0 : data.Length) {
        }

        public RoPacketReader(byte[] data, int offset, int length) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || length < 0 || offset + length > data.Length) {
                throw new ArgumentOutOfRangeException(nameof(length), "Reader range must fit inside the supplied buffer.");
            }

            buffer = data;
            this.offset = offset;
            this.length = length;
        }

        public int Position => position;
        public int Length => length;
        public int Remaining => length - position;
        public bool EndOfPacket => Remaining == 0;

        public byte ReadByte() {
            Require(1);
            return buffer[offset + position++];
        }

        public short ReadInt16() {
            return unchecked((short) ReadUInt16());
        }

        public ushort ReadUInt16() {
            Require(2);
            int absolute = offset + position;
            position += 2;
            return (ushort) (buffer[absolute] | (buffer[absolute + 1] << 8));
        }

        public int ReadInt32() {
            return unchecked((int) ReadUInt32());
        }

        public uint ReadUInt32() {
            Require(4);
            int absolute = offset + position;
            position += 4;
            return (uint) (buffer[absolute] |
                (buffer[absolute + 1] << 8) |
                (buffer[absolute + 2] << 16) |
                (buffer[absolute + 3] << 24));
        }

        public long ReadInt64() {
            return unchecked((long) ReadUInt64());
        }

        public ulong ReadUInt64() {
            Require(8);
            int absolute = offset + position;
            position += 8;
            return (ulong) buffer[absolute] |
                ((ulong) buffer[absolute + 1] << 8) |
                ((ulong) buffer[absolute + 2] << 16) |
                ((ulong) buffer[absolute + 3] << 24) |
                ((ulong) buffer[absolute + 4] << 32) |
                ((ulong) buffer[absolute + 5] << 40) |
                ((ulong) buffer[absolute + 6] << 48) |
                ((ulong) buffer[absolute + 7] << 56);
        }

        public byte[] ReadBytes(int count) {
            if (count < 0) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Require(count);
            var result = new byte[count];
            Array.Copy(buffer, offset + position, result, 0, count);
            position += count;
            return result;
        }

        public string ReadFixedString(int byteCount, Encoding encoding = null) {
            if (byteCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            }

            Require(byteCount);
            int absolute = offset + position;
            int used = 0;
            while (used < byteCount && buffer[absolute + used] != 0) {
                used++;
            }

            position += byteCount;
            if (used == 0) {
                return string.Empty;
            }

            var bytes = new byte[used];
            Array.Copy(buffer, absolute, bytes, 0, used);
            return (encoding ?? Encoding.ASCII).GetString(bytes);
        }

        public RoPacketPosition ReadPosition() {
            byte a = ReadByte();
            byte b = ReadByte();
            byte c = ReadByte();

            int x = (a << 2) | (b >> 6);
            int y = ((b & 0x3f) << 4) | (c >> 4);
            int direction = c & 0x0f;

            return new RoPacketPosition(x, y, direction);
        }

        public RoPacketPositionPair ReadPositionPair() {
            byte a = ReadByte();
            byte b = ReadByte();
            byte c = ReadByte();
            byte d = ReadByte();
            byte e = ReadByte();

            int startX = (a << 2) | ((b & 0xc0) >> 6);
            int startY = ((b & 0x3f) << 4) | ((c & 0xf0) >> 4);
            int endX = ((c & 0x0f) << 6) | ((d & 0xfc) >> 2);
            int endY = ((d & 0x03) << 8) | e;

            return new RoPacketPositionPair(startX, startY, endX, endY);
        }

        public void Skip(int byteCount) {
            if (byteCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            }

            Require(byteCount);
            position += byteCount;
        }

        private void Require(int byteCount) {
            if (Remaining < byteCount) {
                throw new EndOfStreamException($"Packet reader needs {byteCount} byte(s), but only {Remaining} remain.");
            }
        }
    }
}
