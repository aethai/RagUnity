using System;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellMapSessionState : IDisposable {
        private readonly int liveKeepAliveIntervalMilliseconds;
        private readonly RoServerClockSync serverClock;
        private readonly Func<RoLiveMapSession, RoMapEntryDrainResult, bool> drainHandler;
        private readonly Action<RoLiveMapSession, string> errorHandler;

        private RoLoginEnterMapResult mapResult;
        private RoLiveMapReceivePump liveReceivePump;

        public RoShellMapSessionState(
            int liveKeepAliveIntervalMilliseconds,
            RoServerClockSync serverClock,
            Func<RoLiveMapSession, RoMapEntryDrainResult, bool> drainHandler,
            Action<RoLiveMapSession, string> errorHandler) {
            this.liveKeepAliveIntervalMilliseconds = liveKeepAliveIntervalMilliseconds <= 0
                ? 10000
                : liveKeepAliveIntervalMilliseconds;
            this.serverClock = serverClock;
            this.drainHandler = drainHandler ?? throw new ArgumentNullException(nameof(drainHandler));
            this.errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        }

        public RoLoginEnterMapResult MapResult => mapResult;

        public uint LocalActorId {
            get {
                return mapResult != null && mapResult.Aid.AccountId > 0
                    ? unchecked((uint) mapResult.Aid.AccountId)
                    : 0;
            }
        }

        public bool IsCurrentResult(RoLoginEnterMapResult result) {
            return mapResult != null && ReferenceEquals(mapResult, result);
        }

        public bool IsCurrentSession(RoLiveMapSession liveMapSession) {
            return mapResult != null &&
                liveMapSession != null &&
                ReferenceEquals(mapResult.MapSession, liveMapSession);
        }

        public bool TryGetOpenSession(out RoLiveMapSession liveMapSession) {
            liveMapSession = mapResult?.MapSession;
            return liveMapSession != null && !liveMapSession.IsDisposed;
        }

        public RoLiveMapSession ReplaceMapResultAndStart(RoLoginEnterMapResult nextResult) {
            StopReceive();
            serverClock?.Reset();
            RoLiveMapSession previousSession = mapResult?.MapSession;
            mapResult = nextResult;
            StartReceive(nextResult?.MapSession);
            return previousSession;
        }

        public void ClearAndDispose() {
            StopReceive();
            serverClock?.Reset();
            RoLiveMapSession session = mapResult?.MapSession;
            mapResult = null;
            session?.Dispose();
        }

        public void StopReceive() {
            RoLiveMapReceivePump pump = liveReceivePump;
            liveReceivePump = null;
            pump?.Dispose();
        }

        public void RestartReceiveIfCurrent(RoLiveMapSession liveMapSession) {
            if (IsCurrentSession(liveMapSession) &&
                liveMapSession != null &&
                !liveMapSession.IsDisposed) {
                StartReceive(liveMapSession);
            }
        }

        public void Dispose() {
            ClearAndDispose();
        }

        private void StartReceive(RoLiveMapSession liveMapSession) {
            StopReceive();
            if (liveMapSession == null || liveMapSession.IsDisposed) {
                return;
            }

            liveReceivePump = new RoLiveMapReceivePump(
                liveMapSession,
                liveKeepAliveIntervalMilliseconds,
                serverClock,
                drainHandler,
                errorHandler);
            liveReceivePump.Start();
        }
    }
}
