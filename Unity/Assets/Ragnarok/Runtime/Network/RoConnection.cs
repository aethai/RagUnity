using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public sealed class RoPacketFrameEventArgs : EventArgs {
        public RoPacketFrameEventArgs(RoPacketFrame frame) {
            Frame = frame;
        }

        public RoPacketFrame Frame { get; }
    }

    public sealed class RoPacketSentEventArgs : EventArgs {
        public RoPacketSentEventArgs(ushort packetId, byte[] bytes) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }

            PacketId = packetId;
            Bytes = new byte[bytes.Length];
            Array.Copy(bytes, Bytes, bytes.Length);
        }

        public ushort PacketId { get; }
        public byte[] Bytes { get; }
        public int Length => Bytes.Length;
    }

    public sealed class RoNetworkDiagnosticEventArgs : EventArgs {
        public RoNetworkDiagnosticEventArgs(
            string message,
            ushort packetId = 0,
            string packetName = "",
            RoPacketFrameStatus status = RoPacketFrameStatus.Complete,
            int length = 0,
            string prefix = "") {
            Message = message ?? string.Empty;
            PacketId = packetId;
            PacketName = packetName ?? string.Empty;
            Status = status;
            Length = length;
            Prefix = prefix ?? string.Empty;
        }

        public string Message { get; }
        public ushort PacketId { get; }
        public string PacketName { get; }
        public RoPacketFrameStatus Status { get; }
        public int Length { get; }
        public string Prefix { get; }
    }

    public static class RoNetworkRuntimeDiagnostics {
        private static readonly object Sync = new object();
        private static string lastDiagnostic = string.Empty;
        private static string lastInbound = string.Empty;
        private static string lastOutbound = string.Empty;

        public static event EventHandler<RoNetworkDiagnosticEventArgs> DiagnosticRecorded;

        public static string LastDiagnostic {
            get {
                lock (Sync) {
                    return lastDiagnostic;
                }
            }
        }

        public static string LastInbound {
            get {
                lock (Sync) {
                    return lastInbound;
                }
            }
        }

        public static string LastOutbound {
            get {
                lock (Sync) {
                    return lastOutbound;
                }
            }
        }

        public static string FormatRecentContext() {
            lock (Sync) {
                var builder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(lastOutbound)) {
                    builder.Append(" lastOutbound=");
                    builder.Append(lastOutbound);
                }

                if (!string.IsNullOrWhiteSpace(lastInbound)) {
                    builder.Append(" lastInbound=");
                    builder.Append(lastInbound);
                }

                if (!string.IsNullOrWhiteSpace(lastDiagnostic)) {
                    builder.Append(" lastDiagnostic=\"");
                    builder.Append(lastDiagnostic);
                    builder.Append('"');
                }

                return builder.ToString();
            }
        }

        internal static void RecordOutbound(ushort packetId, int length, string packetName = "") {
            string summary = $"0x{packetId:x4}/{(string.IsNullOrWhiteSpace(packetName) ? "?" : packetName)} len={length}";
            lock (Sync) {
                lastOutbound = summary;
            }
        }

        internal static void RecordInbound(ushort packetId, int length, string packetName = "") {
            string summary = $"0x{packetId:x4}/{(string.IsNullOrWhiteSpace(packetName) ? "?" : packetName)} len={length}";
            lock (Sync) {
                lastInbound = summary;
            }
        }

        internal static void RecordDiagnostic(
            string message,
            ushort packetId = 0,
            string packetName = "",
            RoPacketFrameStatus status = RoPacketFrameStatus.Complete,
            int length = 0,
            string prefix = "") {
            var eventArgs = new RoNetworkDiagnosticEventArgs(message, packetId, packetName, status, length, prefix);
            lock (Sync) {
                lastDiagnostic = eventArgs.Message;
            }

            DiagnosticRecorded?.Invoke(null, eventArgs);
        }

        internal static string FormatPacketPrefix(byte[] bytes, int maxBytes) {
            if (bytes == null || bytes.Length == 0 || maxBytes <= 0) {
                return string.Empty;
            }

            int count = Math.Min(bytes.Length, maxBytes);
            var builder = new StringBuilder(count * 3);
            for (int i = 0; i < count; i++) {
                if (i > 0) {
                    builder.Append(' ');
                }

                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    public sealed class RoConnection : IDisposable {
        private readonly IRoTransport transport;
        private readonly RoServerProfile profile;
        private readonly RoPacketRegistry registry;
        private readonly RoPacketCodec codec;
        private readonly IRoPacketTraceSink traceSink;
        private readonly IRoPacketCaptureSink captureSink;
        private readonly bool enableVerboseTraceMessages;
        private readonly object rawReadSync = new object();
        private PendingRawRead pendingRawRead;
        private int rodexOpenDiagnosticsRemaining;
        private bool disposed;

        public RoConnection(
            IRoTransport transport,
            RoServerProfile profile,
            RoPacketRegistry registry = null,
            IRoPacketTransform transform = null,
            IRoPacketTraceSink traceSink = null,
            IRoPacketCaptureSink captureSink = null,
            int receiveFrameBufferBytes = 4096,
            bool enableVerboseTraceMessages = false) {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.profile = profile;
            this.registry = registry ?? RoPacketRegistry.CreateLocalRathena20200401();
            this.traceSink = traceSink;
            this.captureSink = captureSink;
            this.enableVerboseTraceMessages = enableVerboseTraceMessages;
            codec = new RoPacketCodec(
                profile,
                this.registry,
                transform,
                receiveFrameBufferBytes);

            this.transport.ConnectionStateChanged += HandleConnectionStateChanged;
            this.transport.DataReceived += HandleDataReceived;
            this.transport.Error += HandleError;
        }

        public RoConnection(
            IRoTransport transport,
            IRoPacketProfile packetProfile,
            IRoPacketTraceSink traceSink = null,
            IRoPacketCaptureSink captureSink = null,
            int receiveFrameBufferBytes = 4096,
            bool enableVerboseTraceMessages = false)
            : this(
                transport,
                ToServerProfile(packetProfile),
                packetProfile?.CreateRegistry(),
                packetProfile?.CreatePacketTransform(),
                traceSink,
                captureSink,
                receiveFrameBufferBytes,
                enableVerboseTraceMessages) {
        }

        public event EventHandler<RoTransportStateChangedEventArgs> ConnectionStateChanged;
        public event EventHandler<RoTransportErrorEventArgs> Error;
        public event EventHandler<RoNetworkDiagnosticEventArgs> Diagnostic;
        public event EventHandler<RoPacketFrameEventArgs> PacketReceived;
        public event EventHandler<RoPacketSentEventArgs> PacketSent;

        public RoTransportConnectionState State => transport.State;
        public int BufferedInboundByteCount => codec.BufferedInboundByteCount;

        public Task ConnectAsync(RoServerEndpoint endpoint, CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            codec.ResetConnectionState();
            return transport.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken);
        }

        public async Task SendAsync(byte[] packet, CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            if (packet == null) {
                throw new ArgumentNullException(nameof(packet));
            }

            if (packet.Length < 2) {
                throw new ArgumentException("Packet must include a 2-byte packet id.", nameof(packet));
            }

            if (State != RoTransportConnectionState.Connected) {
                throw new InvalidOperationException("Connection is not connected.");
            }

            ushort packetId = ReadPacketId(packet);
            registry.TryGet(packetId, out RoPacketDefinition definition);
            byte[] bytes = codec.EncodeOutbound(packet);
            await transport.SendAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);

            Trace(
                RoPacketDirection.ClientToServer,
                packetId,
                definition.Name,
                bytes.Length,
                RoPacketFrameStatus.Complete,
                ShouldBuildVerboseTraceMessage() ? FormatOutboundTraceMessage(packetId, packet) : null);
            Capture(RoPacketDirection.ClientToServer, packetId, definition.Name, bytes, RoPacketFrameStatus.Complete);
            RoNetworkRuntimeDiagnostics.RecordOutbound(packetId, bytes.Length, definition.Name);
            ObserveRodexOutbound(packetId, bytes.Length);
            PacketSent?.Invoke(this, new RoPacketSentEventArgs(packetId, bytes));
        }

        public Task<byte[]> ReadRawAsync(int byteCount, string traceName, CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            if (byteCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var read = new PendingRawRead(byteCount, traceName);

            lock (rawReadSync) {
                if (pendingRawRead != null) {
                    read.Dispose();
                    throw new InvalidOperationException("Only one pending raw read is supported per connection.");
                }

                pendingRawRead = read;
            }

            if (cancellationToken.CanBeCanceled) {
                read.Cancellation = cancellationToken.Register(
                    () => CancelRawRead(read, new OperationCanceledException(cancellationToken)));
                if (cancellationToken.IsCancellationRequested) {
                    CancelRawRead(read, new OperationCanceledException(cancellationToken));
                }
            }

            return read.Task;
        }

        public Task CloseAsync() {
            if (disposed) {
                return Task.CompletedTask;
            }

            CancelPendingRawRead(new EndOfStreamException("Connection was closed while waiting for raw bytes."));
            codec.ResetConnectionState();
            return transport.CloseAsync();
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            CancelPendingRawRead(new ObjectDisposedException(nameof(RoConnection)));
            transport.ConnectionStateChanged -= HandleConnectionStateChanged;
            transport.DataReceived -= HandleDataReceived;
            transport.Error -= HandleError;
            transport.Dispose();
        }

        private void HandleConnectionStateChanged(object sender, RoTransportStateChangedEventArgs eventArgs) {
            if (eventArgs.CurrentState == RoTransportConnectionState.Disconnected) {
                CancelPendingRawRead(new EndOfStreamException("Transport disconnected while waiting for raw bytes."));
                codec.ResetConnectionState();
            }

            ConnectionStateChanged?.Invoke(this, eventArgs);
        }

        private void HandleDataReceived(object sender, RoTransportDataReceivedEventArgs eventArgs) {
            if (disposed) {
                return;
            }

            int offset = ConsumePendingRawRead(eventArgs.Data, 0, eventArgs.Data.Length);
            if (offset < eventArgs.Data.Length) {
                codec.AppendInbound(eventArgs.Data, offset, eventArgs.Data.Length - offset);
                DrainInboundFrames();
            }
        }

        private void DrainInboundFrames() {
            while (true) {
                if (codec.TryReadInboundFrame(out RoPacketFrame frame, out RoPacketFrameMeasurement measurement)) {
                    Trace(
                        RoPacketDirection.ServerToClient,
                        frame.PacketId,
                        frame.Definition.Name,
                        frame.Length,
                        RoPacketFrameStatus.Complete);
                    Capture(
                        RoPacketDirection.ServerToClient,
                        frame.PacketId,
                        frame.Definition.Name,
                        frame.Bytes,
                        RoPacketFrameStatus.Complete);
                    RoNetworkRuntimeDiagnostics.RecordInbound(frame.PacketId, frame.Length, frame.Definition.Name);
                    ObserveRodexInbound(frame);
                    PacketReceived?.Invoke(this, new RoPacketFrameEventArgs(frame));
                    continue;
                }

                if (measurement.Status == RoPacketFrameStatus.NeedMoreData) {
                    return;
                }

                ReportFrameError(measurement);
                return;
            }
        }

        private void ReportFrameError(RoPacketFrameMeasurement measurement) {
            int prefixLength = measurement.Status == RoPacketFrameStatus.InvalidLength ? 4 : 16;
            byte[] bytes = codec.CopyBufferedPrefix(Math.Min(prefixLength, codec.BufferedInboundByteCount));
            string packetName = measurement.Definition.Name ?? string.Empty;
            int traceLength = measurement.FrameLength > 0 ? measurement.FrameLength : bytes.Length;

            Trace(RoPacketDirection.ServerToClient, measurement.PacketId, packetName, traceLength, measurement.Status);
            Capture(RoPacketDirection.ServerToClient, measurement.PacketId, packetName, bytes, measurement.Status);
            RecordFrameDiagnostic(measurement, bytes, "Map inbound frame error");
            ObserveRodexFrameError(measurement, bytes);
            codec.ResetInbound();

            Error?.Invoke(
                this,
                new RoTransportErrorEventArgs(new RoNetworkProtocolException(
                    $"Packet 0x{measurement.PacketId:x4} could not be framed: {measurement.Status}.")));
        }

        private void HandleError(object sender, RoTransportErrorEventArgs eventArgs) {
            RecordDiagnostic($"Map transport error: {eventArgs.Exception?.Message ?? "unknown error"}");
            ObserveRodexTransportError(eventArgs.Exception);
            CancelPendingRawRead(eventArgs.Exception);
            Error?.Invoke(this, eventArgs);
        }

        private void ObserveRodexOutbound(ushort packetId, int length) {
            if (packetId != RoPacketIds.CzOpenAllRodex) {
                return;
            }

            rodexOpenDiagnosticsRemaining = 8;
        }

        private void ObserveRodexInbound(RoPacketFrame frame) {
            if (rodexOpenDiagnosticsRemaining <= 0) {
                return;
            }

            rodexOpenDiagnosticsRemaining--;
            if (frame.PacketId == RoPacketIds.ZcAckRodexList3 ||
                frame.PacketId == RoPacketIds.ZcAckRodexList4 ||
                frame.PacketId == RoPacketIds.ZcAckFailedAllRodexList ||
                frame.PacketId == RoPacketIds.ZcNpcChat ||
                rodexOpenDiagnosticsRemaining <= 0) {
                rodexOpenDiagnosticsRemaining = 0;
            }
        }

        private void ObserveRodexFrameError(RoPacketFrameMeasurement measurement, byte[] prefix) {
            if (rodexOpenDiagnosticsRemaining <= 0) {
                return;
            }

            string prefixText = RoNetworkRuntimeDiagnostics.FormatPacketPrefix(prefix, 24);
            RecordDiagnostic(
                $"RODEX inbound after open failed opcode=0x{measurement.PacketId:x4} len={measurement.FrameLength} status={measurement.Status} prefix={prefixText}",
                measurement.PacketId,
                measurement.Definition.Name,
                measurement.Status,
                measurement.FrameLength,
                prefixText);
            rodexOpenDiagnosticsRemaining = 0;
        }

        private void ObserveRodexTransportError(Exception exception) {
            if (rodexOpenDiagnosticsRemaining <= 0) {
                return;
            }

            RecordDiagnostic($"RODEX transport error after open: {exception?.Message ?? "unknown error"}");
            rodexOpenDiagnosticsRemaining = 0;
        }

        private void RecordFrameDiagnostic(RoPacketFrameMeasurement measurement, byte[] prefix, string reason) {
            string prefixText = RoNetworkRuntimeDiagnostics.FormatPacketPrefix(prefix, 24);
            string packetName = measurement.Definition.Name ?? string.Empty;
            string expected = measurement.Definition.Length > 0
                ? measurement.Definition.Length.ToString()
                : (measurement.Definition.IsVariableLength ? "variable" : "?");
            RecordDiagnostic(
                $"{reason}: opcode=0x{measurement.PacketId:x4} name={(string.IsNullOrWhiteSpace(packetName) ? "?" : packetName)} status={measurement.Status} expected={expected} actual={measurement.FrameLength} buffered={codec.BufferedInboundByteCount} prefix={prefixText}",
                measurement.PacketId,
                packetName,
                measurement.Status,
                measurement.FrameLength,
                prefixText);
        }

        private void RecordDiagnostic(
            string message,
            ushort packetId = 0,
            string packetName = "",
            RoPacketFrameStatus status = RoPacketFrameStatus.Complete,
            int length = 0,
            string prefix = "") {
            RoNetworkRuntimeDiagnostics.RecordDiagnostic(message, packetId, packetName, status, length, prefix);
            Diagnostic?.Invoke(this, new RoNetworkDiagnosticEventArgs(message, packetId, packetName, status, length, prefix));
        }

        private int ConsumePendingRawRead(byte[] data, int offset, int count) {
            PendingRawRead completed = null;
            byte[] bytes = null;
            int consumed = 0;

            lock (rawReadSync) {
                if (pendingRawRead == null) {
                    return 0;
                }

                consumed = pendingRawRead.Append(data, offset, count);
                if (pendingRawRead.IsComplete) {
                    completed = pendingRawRead;
                    pendingRawRead = null;
                    bytes = completed.ToArray();
                }
            }

            if (completed != null) {
                completed.Dispose();
                Trace(
                    RoPacketDirection.ServerToClient,
                    0,
                    completed.TraceName,
                    bytes.Length,
                    RoPacketFrameStatus.Complete);
                Capture(
                    RoPacketDirection.ServerToClient,
                    0,
                    completed.TraceName,
                    bytes,
                    RoPacketFrameStatus.Complete);
                completed.TrySetResult(bytes);
            }

            return consumed;
        }

        private void CancelPendingRawRead(Exception exception) {
            PendingRawRead read = null;

            lock (rawReadSync) {
                if (pendingRawRead == null) {
                    return;
                }

                read = pendingRawRead;
                pendingRawRead = null;
            }

            read.Dispose();
            read.TrySetException(exception);
        }

        private void CancelRawRead(PendingRawRead read, Exception exception) {
            bool shouldCancel = false;

            lock (rawReadSync) {
                if (pendingRawRead == read) {
                    pendingRawRead = null;
                    shouldCancel = true;
                }
            }

            if (!shouldCancel) {
                return;
            }

            read.Dispose();
            read.TrySetException(exception);
        }

        private void Trace(
            RoPacketDirection direction,
            ushort packetId,
            string packetName,
            int length,
            RoPacketFrameStatus status,
            string message = null) {
            if (traceSink == null) {
                return;
            }

            if (!traceSink.IsEnabled) {
                return;
            }

            traceSink?.Write(new RoPacketTraceEntry(
                DateTimeOffset.UtcNow,
                direction,
                packetId,
                packetName ?? string.Empty,
                length,
                status,
                message));
        }

        private bool ShouldBuildVerboseTraceMessage() {
            return enableVerboseTraceMessages && traceSink != null && traceSink.IsEnabled;
        }

        private static string FormatOutboundTraceMessage(ushort packetId, byte[] packet) {
            switch (packetId) {
                case RoPacketIds.CzMoveItemFromBodyToStore2:
                case RoPacketIds.CzMoveItemFromStoreToBody2:
                case RoPacketIds.CzMoveItemFromBodyToCart:
                case RoPacketIds.CzMoveItemFromCartToBody:
                case RoPacketIds.CzMoveItemFromStoreToCart:
                case RoPacketIds.CzMoveItemFromCartToStore:
                    if (packet == null || packet.Length < 8) {
                        return null;
                    }

                    int sourceIndex = packet[2] | (packet[3] << 8);
                    int amount = packet[4] | (packet[5] << 8) | (packet[6] << 16) | (packet[7] << 24);
                    return $"sourceIndex={sourceIndex} amount={amount} plainBytes={FormatBytes(packet)}";
                default:
                    return null;
            }
        }

        private static string FormatBytes(byte[] packet) {
            if (packet == null || packet.Length == 0) {
                return string.Empty;
            }

            var builder = new StringBuilder(packet.Length * 3);
            for (int i = 0; i < packet.Length; i++) {
                if (i > 0) {
                    builder.Append(' ');
                }

                builder.Append(packet[i].ToString("X2"));
            }

            return builder.ToString();
        }

        private void Capture(
            RoPacketDirection direction,
            ushort packetId,
            string packetName,
            byte[] bytes,
            RoPacketFrameStatus status) {
            captureSink?.Write(
                DateTimeOffset.UtcNow,
                direction,
                packetId,
                packetName ?? string.Empty,
                bytes,
                status);
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoConnection));
            }
        }

        private static ushort ReadPacketId(byte[] bytes) {
            return bytes.Length < 2 ? (ushort) 0 : (ushort) (bytes[0] | (bytes[1] << 8));
        }

        private static RoServerProfile ToServerProfile(IRoPacketProfile packetProfile) {
            if (packetProfile == null) {
                throw new ArgumentNullException(nameof(packetProfile));
            }

            var empty = new RoServerEndpoint(string.Empty, 0);
            return new RoServerProfile(
                packetProfile.Name,
                packetProfile.PacketVersion,
                packetProfile.ClientVersion,
                packetProfile.LanguageType,
                packetProfile.Renewal,
                packetProfile.PacketObfuscation,
                empty,
                empty,
                empty);
        }

        private sealed class PendingRawRead : IDisposable {
            private readonly byte[] buffer;
            private readonly TaskCompletionSource<byte[]> completion =
                new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int offset;

            public PendingRawRead(int length, string traceName) {
                buffer = new byte[length];
                TraceName = string.IsNullOrWhiteSpace(traceName) ? "RAW_BYTES" : traceName;
            }

            public CancellationTokenRegistration Cancellation { get; set; }
            public string TraceName { get; }
            public bool IsComplete => offset == buffer.Length;
            public Task<byte[]> Task => completion.Task;

            public int Append(byte[] data, int dataOffset, int count) {
                int copy = Math.Min(buffer.Length - offset, count);
                Array.Copy(data, dataOffset, buffer, offset, copy);
                offset += copy;
                return copy;
            }

            public byte[] ToArray() {
                var result = new byte[buffer.Length];
                Array.Copy(buffer, result, buffer.Length);
                return result;
            }

            public void TrySetResult(byte[] bytes) {
                completion.TrySetResult(bytes);
            }

            public void TrySetException(Exception exception) {
                completion.TrySetException(exception ?? new IOException("Raw read failed."));
            }

            public void Dispose() {
                Cancellation.Dispose();
            }
        }
    }
}
