using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public sealed class RoSimulatedLink {
        private readonly object sync = new object();
        private readonly Queue<PendingChunk> pending = new Queue<PendingChunk>();
        private readonly Random random;
        private readonly int latencyMilliseconds;
        private readonly int jitterMilliseconds;
        private long lastDueMilliseconds = long.MinValue;

        public RoSimulatedLink(int latencyMilliseconds, int jitterMilliseconds = 0, int seed = 0) {
            if (latencyMilliseconds < 0) {
                throw new ArgumentOutOfRangeException(nameof(latencyMilliseconds));
            }

            if (jitterMilliseconds < 0) {
                throw new ArgumentOutOfRangeException(nameof(jitterMilliseconds));
            }

            this.latencyMilliseconds = latencyMilliseconds;
            this.jitterMilliseconds = jitterMilliseconds;
            random = new Random(seed);
        }

        public int PendingCount {
            get {
                lock (sync) {
                    return pending.Count;
                }
            }
        }

        public void Enqueue(byte[] data, long nowMilliseconds) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] copy = CopyBytes(data, 0, data.Length);
            lock (sync) {
                long delay = Math.Max(0, latencyMilliseconds + NextJitterMilliseconds());
                long dueMilliseconds = SafeAdd(nowMilliseconds, delay);
                if (pending.Count > 0 && dueMilliseconds < lastDueMilliseconds) {
                    dueMilliseconds = lastDueMilliseconds;
                }

                lastDueMilliseconds = dueMilliseconds;
                pending.Enqueue(new PendingChunk(copy, dueMilliseconds));
            }
        }

        public bool TryPeekNextDueMilliseconds(out long dueMilliseconds) {
            lock (sync) {
                if (pending.Count == 0) {
                    dueMilliseconds = 0;
                    return false;
                }

                dueMilliseconds = pending.Peek().DueMilliseconds;
                return true;
            }
        }

        public bool DequeueDue(long nowMilliseconds, out byte[] data) {
            lock (sync) {
                if (pending.Count == 0 || pending.Peek().DueMilliseconds > nowMilliseconds) {
                    data = null;
                    return false;
                }

                PendingChunk chunk = pending.Dequeue();
                if (pending.Count == 0) {
                    lastDueMilliseconds = long.MinValue;
                }

                data = CopyBytes(chunk.Data, 0, chunk.Data.Length);
                return true;
            }
        }

        private int NextJitterMilliseconds() {
            if (jitterMilliseconds == 0) {
                return 0;
            }

            return random.Next(-jitterMilliseconds, jitterMilliseconds + 1);
        }

        private static long SafeAdd(long value, long delta) {
            if (delta > 0 && value > long.MaxValue - delta) {
                return long.MaxValue;
            }

            return value + delta;
        }

        private static byte[] CopyBytes(byte[] data, int offset, int count) {
            var copy = new byte[count];
            Buffer.BlockCopy(data, offset, copy, 0, count);
            return copy;
        }

        private readonly struct PendingChunk {
            public PendingChunk(byte[] data, long dueMilliseconds) {
                Data = data;
                DueMilliseconds = dueMilliseconds;
            }

            public byte[] Data { get; }
            public long DueMilliseconds { get; }
        }
    }

    public sealed class RoSimulatedTransport : IRoTransport {
        private readonly object sync = new object();
        private readonly IRoTransport inner;
        private readonly RoSimulatedLink inboundLink;
        private readonly Func<long> nowMilliseconds;
        private CancellationTokenSource drainCancellation = new CancellationTokenSource();
        private bool drainScheduled;
        private bool disposed;

        public RoSimulatedTransport(
            IRoTransport inner,
            int latencyMilliseconds,
            int jitterMilliseconds = 0,
            int seed = 0,
            Func<long> nowMilliseconds = null) {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            inboundLink = new RoSimulatedLink(latencyMilliseconds, jitterMilliseconds, seed);
            this.nowMilliseconds = nowMilliseconds ?? (() => RoMonotonicTime.NowMilliseconds);
            inner.ConnectionStateChanged += HandleInnerConnectionStateChanged;
            inner.DataReceived += HandleInnerDataReceived;
            inner.Error += HandleInnerError;
        }

        public event EventHandler<RoTransportStateChangedEventArgs> ConnectionStateChanged;
        public event EventHandler<RoTransportDataReceivedEventArgs> DataReceived;
        public event EventHandler<RoTransportErrorEventArgs> Error;

        public RoTransportConnectionState State => inner.State;

        public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return inner.ConnectAsync(host, port, cancellationToken);
        }

        public Task SendAsync(byte[] data, int offset, int count, CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return inner.SendAsync(data, offset, count, cancellationToken);
        }

        public Task CloseAsync() {
            if (disposed) {
                return Task.CompletedTask;
            }

            return inner.CloseAsync();
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            inner.ConnectionStateChanged -= HandleInnerConnectionStateChanged;
            inner.DataReceived -= HandleInnerDataReceived;
            inner.Error -= HandleInnerError;
            drainCancellation.Cancel();
            drainCancellation.Dispose();
            inner.Dispose();
        }

        private void HandleInnerConnectionStateChanged(object sender, RoTransportStateChangedEventArgs e) {
            ConnectionStateChanged?.Invoke(this, e);
        }

        private void HandleInnerDataReceived(object sender, RoTransportDataReceivedEventArgs e) {
            lock (sync) {
                if (disposed) {
                    return;
                }

                inboundLink.Enqueue(e.Data, nowMilliseconds());
                if (drainScheduled) {
                    return;
                }

                drainScheduled = true;
            }

            _ = DrainInboundAsync();
        }

        private void HandleInnerError(object sender, RoTransportErrorEventArgs e) {
            Error?.Invoke(this, e);
        }

        private async Task DrainInboundAsync() {
            while (true) {
                byte[] data;
                long delayMilliseconds = 0;
                CancellationToken cancellationToken;

                lock (sync) {
                    if (disposed) {
                        return;
                    }

                    cancellationToken = drainCancellation.Token;
                    if (!inboundLink.DequeueDue(nowMilliseconds(), out data)) {
                        if (!inboundLink.TryPeekNextDueMilliseconds(out long dueMilliseconds)) {
                            drainScheduled = false;
                            return;
                        }

                        delayMilliseconds = Math.Max(1, dueMilliseconds - nowMilliseconds());
                    }
                }

                if (data != null) {
                    DataReceived?.Invoke(this, new RoTransportDataReceivedEventArgs(data));
                    continue;
                }

                try {
                    await Task.Delay((int) Math.Min(delayMilliseconds, int.MaxValue), cancellationToken).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    return;
                }
            }
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoSimulatedTransport));
            }
        }
    }
}
