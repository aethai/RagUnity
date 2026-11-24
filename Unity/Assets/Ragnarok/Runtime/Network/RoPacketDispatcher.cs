using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    public sealed class RoPacketUnhandledEventArgs : EventArgs {
        public RoPacketUnhandledEventArgs(RoPacketFrame frame) {
            Frame = frame;
        }

        public RoPacketFrame Frame { get; }
    }

    public sealed class RoPacketHandlerErrorEventArgs : EventArgs {
        public RoPacketHandlerErrorEventArgs(ushort packetId, string handlerName, Exception exception) {
            PacketId = packetId;
            HandlerName = handlerName ?? string.Empty;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public ushort PacketId { get; }
        public string HandlerName { get; }
        public Exception Exception { get; }
    }

    public sealed class RoPacketDispatcher : IDisposable {
        private readonly RoConnection connection;
        private readonly Dictionary<ushort, List<Action<RoPacketFrame>>> handlersByPacketId =
            new Dictionary<ushort, List<Action<RoPacketFrame>>>();
        private readonly List<Action<RoPacketFrame>> dispatchSnapshotScratch = new List<Action<RoPacketFrame>>(4);
        private int dispatchDepth;
        private bool disposed;

        public RoPacketDispatcher() {
        }

        public RoPacketDispatcher(RoConnection connection) {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this.connection.PacketReceived += HandlePacketReceived;
        }

        public event EventHandler<RoPacketUnhandledEventArgs> UnhandledPacket;
        public event EventHandler<RoPacketHandlerErrorEventArgs> HandlerError;

        public IDisposable Subscribe(ushort packetId, Action<RoPacketFrame> handler) {
            ThrowIfDisposed();

            if (handler == null) {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!handlersByPacketId.TryGetValue(packetId, out List<Action<RoPacketFrame>> handlers)) {
                handlers = new List<Action<RoPacketFrame>>();
                handlersByPacketId.Add(packetId, handlers);
            }

            handlers.Add(handler);
            return new Subscription(this, packetId, handler);
        }

        public bool HasHandlers(ushort packetId) {
            return handlersByPacketId.TryGetValue(packetId, out List<Action<RoPacketFrame>> handlers) &&
                handlers.Count > 0;
        }

        public void Dispatch(RoPacketFrame frame) {
            ThrowIfDisposed();

            if (!handlersByPacketId.TryGetValue(frame.PacketId, out List<Action<RoPacketFrame>> handlers) ||
                handlers.Count == 0) {
                UnhandledPacket?.Invoke(this, new RoPacketUnhandledEventArgs(frame));
                return;
            }

            if (dispatchDepth > 0) {
                Action<RoPacketFrame>[] snapshot = handlers.ToArray();
                for (int i = 0; i < snapshot.Length; i++) {
                    InvokeHandler(snapshot[i], frame);
                }

                return;
            }

            dispatchDepth++;
            try {
                dispatchSnapshotScratch.Clear();
                dispatchSnapshotScratch.AddRange(handlers);
                for (int i = 0; i < dispatchSnapshotScratch.Count; i++) {
                    InvokeHandler(dispatchSnapshotScratch[i], frame);
                }
            } finally {
                dispatchSnapshotScratch.Clear();
                dispatchDepth--;
            }
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            if (connection != null) {
                connection.PacketReceived -= HandlePacketReceived;
            }

            handlersByPacketId.Clear();
            disposed = true;
        }

        private void HandlePacketReceived(object sender, RoPacketFrameEventArgs eventArgs) {
            Dispatch(eventArgs.Frame);
        }

        private void Unsubscribe(ushort packetId, Action<RoPacketFrame> handler) {
            if (disposed || handler == null) {
                return;
            }

            if (!handlersByPacketId.TryGetValue(packetId, out List<Action<RoPacketFrame>> handlers)) {
                return;
            }

            handlers.Remove(handler);
            if (handlers.Count == 0) {
                handlersByPacketId.Remove(packetId);
            }
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoPacketDispatcher));
            }
        }

        private void InvokeHandler(Action<RoPacketFrame> handler, RoPacketFrame frame) {
            try {
                handler(frame);
            } catch (Exception exception) {
                string handlerName = $"{handler.Method.DeclaringType?.FullName ?? "<unknown>"}.{handler.Method.Name}";
                var handlerException = new InvalidOperationException(
                    $"Failed to handle packet 0x{frame.PacketId:x4} ({frame.Length} bytes) in {handlerName}: {exception.Message}",
                    exception);
                if (HandlerError == null) {
                    throw handlerException;
                }

                HandlerError.Invoke(this, new RoPacketHandlerErrorEventArgs(frame.PacketId, handlerName, handlerException));
            }
        }

        private sealed class Subscription : IDisposable {
            private readonly RoPacketDispatcher dispatcher;
            private readonly ushort packetId;
            private readonly Action<RoPacketFrame> handler;
            private bool disposed;

            public Subscription(RoPacketDispatcher dispatcher, ushort packetId, Action<RoPacketFrame> handler) {
                this.dispatcher = dispatcher;
                this.packetId = packetId;
                this.handler = handler;
            }

            public void Dispose() {
                if (disposed) {
                    return;
                }

                dispatcher.Unsubscribe(packetId, handler);
                disposed = true;
            }
        }
    }
}
