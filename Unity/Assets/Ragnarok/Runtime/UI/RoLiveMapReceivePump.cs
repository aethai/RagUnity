using System;
using System.Threading;
using System.Threading.Tasks;
using Ragnarok.Diagnostics;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoLiveMapReceivePump : IDisposable {
        private const int LiveDrainMaxFrames = 64;
        private const int LiveDrainIdleMilliseconds = 2;

        private readonly object sync = new object();
        private readonly RoLiveMapSession liveMapSession;
        private readonly int keepAliveIntervalMilliseconds;
        private readonly RoServerClockSync serverClock;
        private readonly Func<RoLiveMapSession, RoMapEntryDrainResult, bool> drainHandler;
        private readonly Action<RoLiveMapSession, string> errorHandler;
        private CancellationTokenSource cancellation;
        private bool disposed;

        public RoLiveMapReceivePump(
            RoLiveMapSession liveMapSession,
            int keepAliveIntervalMilliseconds,
            RoServerClockSync serverClock,
            Func<RoLiveMapSession, RoMapEntryDrainResult, bool> drainHandler,
            Action<RoLiveMapSession, string> errorHandler) {
            this.liveMapSession = liveMapSession ?? throw new ArgumentNullException(nameof(liveMapSession));
            this.keepAliveIntervalMilliseconds = keepAliveIntervalMilliseconds <= 0 ? 10000 : keepAliveIntervalMilliseconds;
            this.serverClock = serverClock;
            this.drainHandler = drainHandler ?? throw new ArgumentNullException(nameof(drainHandler));
            this.errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        }

        public RoLiveMapSession LiveMapSession => liveMapSession;

        public void Start() {
            lock (sync) {
                if (disposed) {
                    throw new ObjectDisposedException(nameof(RoLiveMapReceivePump));
                }

                if (cancellation != null) {
                    return;
                }

                cancellation = new CancellationTokenSource();
                CancellationToken token = cancellation.Token;
                _ = Task.Run(() => Run(token), token);
            }
        }

        public void Dispose() {
            CancellationTokenSource toCancel;
            lock (sync) {
                if (disposed) {
                    return;
                }

                disposed = true;
                toCancel = cancellation;
                cancellation = null;
            }

            if (toCancel != null) {
                toCancel.Cancel();
                toCancel.Dispose();
            }
        }

        private void Run(CancellationToken token) {
            int sentKeepAliveCount = 0;
            int nextKeepAliveTick = Environment.TickCount;
            while (!token.IsCancellationRequested) {
                if (ShouldSendKeepAlive(nextKeepAliveTick)) {
                    try {
                        serverClock?.OnTimeRequestSent(RoMonotonicTime.NowMilliseconds);
                        liveMapSession.Send(RoOutboundPackets.BuildCzRequestTime2(Environment.TickCount));
                        sentKeepAliveCount++;
                        int nextIntervalMilliseconds = sentKeepAliveCount < 2 ? 2000 : keepAliveIntervalMilliseconds;
                        nextKeepAliveTick = unchecked(Environment.TickCount + nextIntervalMilliseconds);
                    } catch (ObjectDisposedException) {
                        return;
                    } catch (Exception ex) {
                        CompleteWithError(token, ex.Message);
                        return;
                    }
                }

                RoMapEntryDrainResult drain;
                bool logRuntimePerf = RoRuntimeDiagnostics.RuntimePerfLogEnabled &&
                    RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics;
                int availableBeforeDrain = 0;
                long drainMilliseconds = 0;
                long handlerMilliseconds = 0;
                try {
                    long drainStartedAtMilliseconds = 0;
                    if (logRuntimePerf) {
                        availableBeforeDrain = liveMapSession.AvailableBytes;
                        drainStartedAtMilliseconds = RoMonotonicTime.NowMilliseconds;
                    }

                    drain = liveMapSession.Drain(
                        maxFrames: LiveDrainMaxFrames,
                        idleMilliseconds: LiveDrainIdleMilliseconds,
                        stopAfterMovementResponse: true);
                    if (logRuntimePerf) {
                        drainMilliseconds = RoMonotonicTime.NowMilliseconds - drainStartedAtMilliseconds;
                    }
                } catch (ObjectDisposedException) {
                    return;
                } catch (Exception ex) {
                    CompleteWithError(token, ex.Message);
                    return;
                }

                if (drain == null || (drain.Packets.Length == 0 && !drain.StoppedOnUnsupportedPacket)) {
                    if (logRuntimePerf) {
                        LogRuntimePerfIteration(availableBeforeDrain, drainMilliseconds, handlerMilliseconds, false, drain);
                    }

                    continue;
                }

                if (serverClock != null && drain.TimeNotifications.Length > 0) {
                    serverClock.OnTimeResponse(
                        drain.TimeNotifications[drain.TimeNotifications.Length - 1].ServerTime,
                        RoMonotonicTime.NowMilliseconds);
                }

                bool keepRunning;
                if (logRuntimePerf) {
                    long handlerStartedAtMilliseconds = RoMonotonicTime.NowMilliseconds;
                    keepRunning = drainHandler(liveMapSession, drain);
                    handlerMilliseconds = RoMonotonicTime.NowMilliseconds - handlerStartedAtMilliseconds;
                    LogRuntimePerfIteration(availableBeforeDrain, drainMilliseconds, handlerMilliseconds, true, drain);
                } else {
                    keepRunning = drainHandler(liveMapSession, drain);
                }

                if (!keepRunning) {
                    return;
                }
            }
        }

        private void CompleteWithError(CancellationToken token, string message) {
            if (!token.IsCancellationRequested) {
                errorHandler(liveMapSession, message);
            }
        }

        private static bool ShouldSendKeepAlive(int nextKeepAliveTick) {
            return unchecked(Environment.TickCount - nextKeepAliveTick) >= 0;
        }

        private static void LogRuntimePerfIteration(
            int availableBeforeDrain,
            long drainMilliseconds,
            long handlerMilliseconds,
            bool handlerRan,
            RoMapEntryDrainResult drain) {
            int packetCount = drain?.Packets?.Length ?? 0;
            int eventCount = drain?.OrderedEvents?.Length ?? 0;
            int parseFailureCount = drain?.ParseFailureCount ?? 0;
            bool stopped = drain?.StoppedOnUnsupportedPacket == true;
            UnityEngine.Debug.Log(
                $"RO live pump iteration socketAvailableBeforeDrain={availableBeforeDrain} drainMs={drainMilliseconds} handlerMs={handlerMilliseconds} handlerRan={handlerRan} packets={packetCount} events={eventCount} parseFailures={parseFailureCount} stopped={stopped}");
        }
    }
}
