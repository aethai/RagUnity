using System;

namespace Ragnarok.Network {
    public readonly struct RoPacketFrame {
        public RoPacketFrame(ushort packetId, byte[] bytes, RoPacketDefinition definition) {
            PacketId = packetId;
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            Definition = definition;
        }

        public ushort PacketId { get; }
        public byte[] Bytes { get; }
        public RoPacketDefinition Definition { get; }
        public int Length => Bytes.Length;
    }

    public sealed class RoPacketStreamFramer {
        private readonly RoPacketRegistry registry;
        private readonly RoPacketDirection direction;
        private byte[] buffer;
        private int count;

        public RoPacketStreamFramer(RoPacketRegistry registry, int initialCapacity)
            : this(registry, RoPacketDirection.Unknown, initialCapacity) {
        }

        public RoPacketStreamFramer(
            RoPacketRegistry registry,
            RoPacketDirection direction = RoPacketDirection.Unknown,
            int initialCapacity = 4096) {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (initialCapacity <= 0) {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            this.direction = direction;
            buffer = new byte[initialCapacity];
        }

        public int BufferedByteCount => count;

        public void Append(byte[] data, int offset, int length) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || length < 0 || offset + length > data.Length) {
                throw new ArgumentOutOfRangeException(nameof(length), "Append range must fit inside the supplied buffer.");
            }

            Ensure(count + length);
            Array.Copy(data, offset, buffer, count, length);
            count += length;
        }

        public RoPacketFrameMeasurement Peek() {
            return registry.MeasureFrame(buffer, 0, count, direction);
        }

        public bool TryReadFrame(out RoPacketFrame frame, out RoPacketFrameMeasurement measurement) {
            measurement = Peek();
            if (!measurement.IsComplete) {
                frame = default;
                return false;
            }

            var bytes = new byte[measurement.FrameLength];
            Array.Copy(buffer, 0, bytes, 0, measurement.FrameLength);
            Consume(measurement.FrameLength);
            frame = new RoPacketFrame(measurement.PacketId, bytes, measurement.Definition);
            return true;
        }

        public byte[] CopyBufferedPrefix(int byteCount) {
            if (byteCount < 0 || byteCount > count) {
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            }

            var bytes = new byte[byteCount];
            Array.Copy(buffer, 0, bytes, 0, byteCount);
            return bytes;
        }

        public void Reset() {
            count = 0;
        }

        private void Consume(int byteCount) {
            int remaining = count - byteCount;
            if (remaining > 0) {
                Array.Copy(buffer, byteCount, buffer, 0, remaining);
            }

            count = remaining;
        }

        private void Ensure(int required) {
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
