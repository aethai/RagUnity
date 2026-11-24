using System;

namespace Ragnarok.Network {
    public interface IRoPacketTransform {
        void TransformOutbound(byte[] buffer, int offset, int count, RoServerProfile profile);
        void TransformInbound(byte[] buffer, int offset, int count, RoServerProfile profile);
    }

    public interface IRoPacketTransformLifecycle {
        void Reset(RoServerProfile profile);
    }

    public sealed class RoNoPacketTransform : IRoPacketTransform {
        public static readonly RoNoPacketTransform Instance = new RoNoPacketTransform();

        private RoNoPacketTransform() {
        }

        public void TransformOutbound(byte[] buffer, int offset, int count, RoServerProfile profile) {
        }

        public void TransformInbound(byte[] buffer, int offset, int count, RoServerProfile profile) {
        }
    }

    public readonly struct RoPacketCryptKeys : IEquatable<RoPacketCryptKeys> {
        public RoPacketCryptKeys(uint key0, uint key1, uint key2) {
            Key0 = key0;
            Key1 = key1;
            Key2 = key2;
        }

        public uint Key0 { get; }
        public uint Key1 { get; }
        public uint Key2 { get; }

        public bool Equals(RoPacketCryptKeys other) {
            return Key0 == other.Key0 &&
                Key1 == other.Key1 &&
                Key2 == other.Key2;
        }

        public override bool Equals(object obj) {
            return obj is RoPacketCryptKeys other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = Key0.GetHashCode();
                hash = (hash * 397) ^ Key1.GetHashCode();
                hash = (hash * 397) ^ Key2.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct RoPacketCryptConfig : IEquatable<RoPacketCryptConfig> {
        private RoPacketCryptConfig(
            bool enabled,
            bool required,
            bool transformInbound,
            RoPacketCryptKeys keys) {
            Enabled = enabled;
            Required = required;
            TransformInbound = transformInbound;
            Keys = keys;
        }

        public bool Enabled { get; }
        public bool Required { get; }
        public bool TransformInbound { get; }
        public RoPacketCryptKeys Keys { get; }

        public static RoPacketCryptConfig Disabled => default;

        public static RoPacketCryptConfig EnableDefault(bool required = true, bool transformInbound = false) {
            return new RoPacketCryptConfig(
                enabled: true,
                required: required,
                transformInbound: transformInbound,
                keys: new RoPacketCryptKeys(0u, 0u, 0u));
        }

        public static RoPacketCryptConfig Enable(RoPacketCryptKeys keys, bool required = true, bool transformInbound = false) {
            return new RoPacketCryptConfig(
                enabled: true,
                required: required,
                transformInbound: transformInbound,
                keys: keys);
        }

        public bool Equals(RoPacketCryptConfig other) {
            return Enabled == other.Enabled &&
                Required == other.Required &&
                TransformInbound == other.TransformInbound &&
                Keys.Equals(other.Keys);
        }

        public override bool Equals(object obj) {
            return obj is RoPacketCryptConfig other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = Enabled.GetHashCode();
                hash = (hash * 397) ^ Required.GetHashCode();
                hash = (hash * 397) ^ TransformInbound.GetHashCode();
                hash = (hash * 397) ^ Keys.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class RoPacketCryptTransform : IRoPacketTransform, IRoPacketTransformLifecycle {
        private readonly RoPacketCryptConfig config;
        private uint key0;
        private uint key1;
        private uint key2;

        public RoPacketCryptTransform(RoPacketCryptConfig config) {
            if (!config.Enabled) {
                throw new InvalidOperationException("Packet crypt transform requires an enabled crypt config.");
            }

            if (config.TransformInbound) {
                throw new NotSupportedException(
                    "Inbound packet crypt requires encrypted stream framing support and is not enabled in S3.");
            }

            this.config = config;
            Reset(default);
        }

        public RoPacketCryptConfig Config => config;

        public void Reset(RoServerProfile profile) {
            key0 = config.Keys.Key0;
            key1 = config.Keys.Key1;
            key2 = config.Keys.Key2;
        }

        public void TransformOutbound(byte[] buffer, int offset, int count, RoServerProfile profile) {
            ProcessPacketHeader(buffer, offset, count);
        }

        public void TransformInbound(byte[] buffer, int offset, int count, RoServerProfile profile) {
            if (config.TransformInbound) {
                ProcessPacketHeader(buffer, offset, count);
            }
        }

        private void ProcessPacketHeader(byte[] buffer, int offset, int count) {
            if (buffer == null) {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length) {
                throw new ArgumentOutOfRangeException(nameof(count), "Packet crypt range must fit inside the supplied buffer.");
            }

            if (count < 2) {
                throw new ArgumentException("Packet crypt requires at least a 2-byte packet header.", nameof(count));
            }

            ushort opcode = (ushort) (buffer[offset] | (buffer[offset + 1] << 8));
            key0 = unchecked((key0 * key1) + key2);
            opcode ^= (ushort) ((key0 >> 16) & 0x7fff);
            buffer[offset] = (byte) (opcode & 0xff);
            buffer[offset + 1] = (byte) (opcode >> 8);
        }
    }
}
