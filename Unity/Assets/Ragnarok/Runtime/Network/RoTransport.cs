using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public enum RoTransportConnectionState {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Closing = 3,
        Faulted = 4
    }

    public sealed class RoTransportStateChangedEventArgs : EventArgs {
        public RoTransportStateChangedEventArgs(
            RoTransportConnectionState previousState,
            RoTransportConnectionState currentState,
            Exception exception = null) {
            PreviousState = previousState;
            CurrentState = currentState;
            Exception = exception;
        }

        public RoTransportConnectionState PreviousState { get; }
        public RoTransportConnectionState CurrentState { get; }
        public Exception Exception { get; }
    }

    public sealed class RoTransportDataReceivedEventArgs : EventArgs {
        public RoTransportDataReceivedEventArgs(byte[] data) {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public byte[] Data { get; }
    }

    public sealed class RoTransportErrorEventArgs : EventArgs {
        public RoTransportErrorEventArgs(Exception exception) {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public Exception Exception { get; }
    }

    public interface IRoTransport : IDisposable {
        event EventHandler<RoTransportStateChangedEventArgs> ConnectionStateChanged;
        event EventHandler<RoTransportDataReceivedEventArgs> DataReceived;
        event EventHandler<RoTransportErrorEventArgs> Error;

        RoTransportConnectionState State { get; }

        Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
        Task SendAsync(byte[] data, int offset, int count, CancellationToken cancellationToken = default);
        Task CloseAsync();
    }

    public sealed class RoTcpTransport : IRoTransport {
        private const int ReceiveLoopShutdownTimeoutMilliseconds = 5000;

        private readonly int receiveBufferSize;
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

        private TcpClient client;
        private NetworkStream stream;
        private CancellationTokenSource receiveCancellation;
        private Task receiveLoopTask;
        private bool disposed;

        public RoTcpTransport(int receiveBufferSize = 8192) {
            if (receiveBufferSize <= 0) {
                throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));
            }

            this.receiveBufferSize = receiveBufferSize;
            State = RoTransportConnectionState.Disconnected;
        }

        public event EventHandler<RoTransportStateChangedEventArgs> ConnectionStateChanged;
        public event EventHandler<RoTransportDataReceivedEventArgs> DataReceived;
        public event EventHandler<RoTransportErrorEventArgs> Error;

        public RoTransportConnectionState State { get; private set; }

        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(host)) {
                throw new ArgumentException("Host is required.", nameof(host));
            }

            if (port <= 0 || port > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            if (State != RoTransportConnectionState.Disconnected) {
                throw new InvalidOperationException($"Cannot connect while transport state is {State}.");
            }

            TransitionState(RoTransportConnectionState.Connecting);
            var nextClient = new TcpClient {
                NoDelay = true,
            };

            try {
                cancellationToken.ThrowIfCancellationRequested();
                if (IPAddress.TryParse(host, out IPAddress address)) {
                    await nextClient.ConnectAsync(address, port).ConfigureAwait(false);
                } else {
                    await nextClient.ConnectAsync(host, port).ConfigureAwait(false);
                }

                client = nextClient;
                stream = nextClient.GetStream();
                receiveCancellation = new CancellationTokenSource();
                receiveLoopTask = Task.Run(() => ReceiveLoopAsync(receiveCancellation.Token));

                TransitionState(RoTransportConnectionState.Connected);
            } catch (Exception exception) {
                nextClient.Dispose();
                TransitionState(RoTransportConnectionState.Faulted, exception);
                TransitionState(RoTransportConnectionState.Disconnected);
                throw;
            }
        }

        public async Task SendAsync(byte[] data, int offset, int count, CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || count < 0 || offset + count > data.Length) {
                throw new ArgumentOutOfRangeException(nameof(count), "Send range must fit inside the supplied buffer.");
            }

            if (State != RoTransportConnectionState.Connected || stream == null) {
                throw new InvalidOperationException("Transport is not connected.");
            }

            await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try {
                await stream.WriteAsync(data, offset, count, cancellationToken).ConfigureAwait(false);
            } catch (Exception exception) {
                OnError(exception);
                throw;
            } finally {
                sendLock.Release();
            }
        }

        public async Task CloseAsync() {
            if (State == RoTransportConnectionState.Disconnected &&
                client == null &&
                stream == null &&
                receiveCancellation == null &&
                receiveLoopTask == null) {
                return;
            }

            if (State != RoTransportConnectionState.Closing &&
                State != RoTransportConnectionState.Disconnected) {
                TransitionState(RoTransportConnectionState.Closing);
            }

            CancellationTokenSource cancellation = receiveCancellation;
            Task loopTask = receiveLoopTask;
            receiveCancellation = null;
            receiveLoopTask = null;

            try {
                cancellation?.Cancel();
            } catch (ObjectDisposedException) {
            }

            ShutdownSocket();

            try {
                stream?.Close();
                client?.Close();
            } finally {
                stream?.Dispose();
                client?.Dispose();
                cancellation?.Dispose();
                stream = null;
                client = null;
            }

            if (loopTask != null) {
                await AwaitReceiveLoopShutdownAsync(loopTask).ConfigureAwait(false);
            }

            if (State != RoTransportConnectionState.Disconnected) {
                TransitionState(RoTransportConnectionState.Disconnected);
            }
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            CloseAsync().GetAwaiter().GetResult();
            disposed = true;
            sendLock.Dispose();
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken) {
            byte[] buffer = new byte[receiveBufferSize];

            try {
                while (!cancellationToken.IsCancellationRequested) {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0) {
                        break;
                    }

                    var chunk = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
                    DataReceived?.Invoke(this, new RoTransportDataReceivedEventArgs(chunk));
                }
            } catch (OperationCanceledException) {
                return;
            } catch (ObjectDisposedException) {
                return;
            } catch (Exception exception) {
                if (State == RoTransportConnectionState.Closing || IsExpectedTermination(exception, cancellationToken)) {
                    return;
                }

                OnError(exception);
                TransitionState(RoTransportConnectionState.Faulted, exception);
            } finally {
                if (!disposed &&
                    State != RoTransportConnectionState.Closing &&
                    State != RoTransportConnectionState.Disconnected) {
                    TransitionState(RoTransportConnectionState.Disconnected);
                }
            }
        }

        private void TransitionState(RoTransportConnectionState nextState, Exception exception = null) {
            RoTransportConnectionState previousState = State;
            State = nextState;
            ConnectionStateChanged?.Invoke(this, new RoTransportStateChangedEventArgs(previousState, nextState, exception));
        }

        private void OnError(Exception exception) {
            Error?.Invoke(this, new RoTransportErrorEventArgs(exception));
        }

        private async Task AwaitReceiveLoopShutdownAsync(Task loopTask) {
            Task completed = await Task
                .WhenAny(loopTask, Task.Delay(ReceiveLoopShutdownTimeoutMilliseconds))
                .ConfigureAwait(false);
            if (completed != loopTask) {
                OnError(new TimeoutException("Timed out while waiting for the TCP receive loop to stop."));
                return;
            }

            try {
                await loopTask.ConfigureAwait(false);
            } catch (OperationCanceledException) {
            } catch (ObjectDisposedException) {
            }
        }

        private void ShutdownSocket() {
            try {
                client?.Client?.Shutdown(SocketShutdown.Both);
            } catch (ObjectDisposedException) {
            } catch (SocketException) {
            } catch (InvalidOperationException) {
            }
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoTcpTransport));
            }
        }

        private static bool IsExpectedTermination(Exception exception, CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return true;
            }

            if (exception is IOException ioException && ioException.InnerException is SocketException innerSocketException) {
                return IsExpectedSocketTermination(innerSocketException.SocketErrorCode);
            }

            return exception is SocketException socketException &&
                IsExpectedSocketTermination(socketException.SocketErrorCode);
        }

        private static bool IsExpectedSocketTermination(SocketError errorCode) {
            return errorCode == SocketError.OperationAborted ||
                errorCode == SocketError.Interrupted ||
                errorCode == SocketError.Shutdown ||
                errorCode == SocketError.NotSocket;
        }
    }
}
