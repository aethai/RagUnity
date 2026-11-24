using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    public readonly struct RoPacketTraceEntry {
        public RoPacketTraceEntry(
            DateTimeOffset timestamp,
            RoPacketDirection direction,
            ushort packetId,
            string packetName,
            int length,
            RoPacketFrameStatus status,
            string message = null) {
            Timestamp = timestamp;
            Direction = direction;
            PacketId = packetId;
            PacketName = packetName;
            Length = length;
            Status = status;
            Message = message;
        }

        public DateTimeOffset Timestamp { get; }
        public RoPacketDirection Direction { get; }
        public ushort PacketId { get; }
        public string PacketName { get; }
        public int Length { get; }
        public RoPacketFrameStatus Status { get; }
        public string Message { get; }
    }

    public interface IRoPacketTraceSink {
        bool IsEnabled { get; }
        void Write(RoPacketTraceEntry entry);
    }

    public sealed class RoPacketTraceBuffer : IRoPacketTraceSink {
        private readonly object sync = new object();
        private readonly Queue<RoPacketTraceEntry> entries;
        private long revision;

        public RoPacketTraceBuffer(int capacity = 256) {
            if (capacity <= 0) {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
            entries = new Queue<RoPacketTraceEntry>(capacity);
        }

        public int Capacity { get; }
        public bool IsEnabled { get; set; } = true;
        public long Revision {
            get {
                lock (sync) {
                    return revision;
                }
            }
        }
        public int Count {
            get {
                lock (sync) {
                    return entries.Count;
                }
            }
        }

        public RoPacketTraceEntry[] Snapshot() {
            lock (sync) {
                if (entries.Count == 0) {
                    return Array.Empty<RoPacketTraceEntry>();
                }

                return entries.ToArray();
            }
        }

        public void Write(RoPacketTraceEntry entry) {
            if (!IsEnabled) {
                return;
            }

            lock (sync) {
                while (entries.Count >= Capacity) {
                    entries.Dequeue();
                }

                entries.Enqueue(entry);
                revision++;
            }
        }

        public void Clear() {
            lock (sync) {
                if (entries.Count == 0) {
                    return;
                }

                entries.Clear();
                revision++;
            }
        }
    }
}
