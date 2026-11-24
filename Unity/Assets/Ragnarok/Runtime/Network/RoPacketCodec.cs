using System;

namespace Ragnarok.Network {
    public sealed class RoPacketCodec {
        private readonly RoServerProfile profile;
        private readonly IRoPacketTransform transform;
        private readonly RoPacketStreamFramer inboundFramer;

        public RoPacketCodec(
            RoServerProfile profile,
            RoPacketRegistry registry = null,
            IRoPacketTransform transform = null,
            int inboundFrameBufferBytes = 4096) {
            this.profile = profile;
            this.transform = transform ?? RoNoPacketTransform.Instance;
            inboundFramer = new RoPacketStreamFramer(
                registry ?? RoPacketRegistry.CreateLocalRathena20200401(),
                RoPacketDirection.ServerToClient,
                inboundFrameBufferBytes);
        }

        public int BufferedInboundByteCount => inboundFramer.BufferedByteCount;

        public byte[] EncodeOutbound(byte[] packet) {
            if (packet == null) {
                throw new ArgumentNullException(nameof(packet));
            }

            if (packet.Length < 2) {
                throw new ArgumentException("Packet must include a 2-byte packet id.", nameof(packet));
            }

            var bytes = new byte[packet.Length];
            Array.Copy(packet, bytes, packet.Length);
            transform.TransformOutbound(bytes, 0, bytes.Length, profile);
            return bytes;
        }

        public void AppendInbound(byte[] data, int offset, int count) {
            inboundFramer.Append(data, offset, count);
        }

        public bool TryReadInboundFrame(out RoPacketFrame frame, out RoPacketFrameMeasurement measurement) {
            if (!inboundFramer.TryReadFrame(out frame, out measurement)) {
                return false;
            }

            byte[] bytes = frame.Bytes;
            transform.TransformInbound(bytes, 0, bytes.Length, profile);
            frame = new RoPacketFrame(frame.PacketId, bytes, frame.Definition);
            return true;
        }

        public byte[] CopyBufferedPrefix(int byteCount) {
            return inboundFramer.CopyBufferedPrefix(byteCount);
        }

        public void ResetInbound() {
            inboundFramer.Reset();
        }

        public void ResetConnectionState() {
            inboundFramer.Reset();
            if (transform is IRoPacketTransformLifecycle lifecycle) {
                lifecycle.Reset(profile);
            }
        }
    }
}
