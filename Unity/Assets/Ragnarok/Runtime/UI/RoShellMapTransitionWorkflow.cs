using System;
using System.Threading.Tasks;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellMapTransitionWorkflow {
        public bool TryPrepare(
            RoShellMapSessionState mapSessionState,
            RoLiveMapSession liveMapSession,
            RoMapEntryDrainResult drain,
            out RoPendingMapTransition transition,
            out string statusText) {
            if (drain == null ||
                liveMapSession == null ||
                liveMapSession.IsDisposed ||
                mapSessionState == null ||
                !mapSessionState.IsCurrentSession(liveMapSession)) {
                transition = default;
                statusText = string.Empty;
                return false;
            }

            if (!RoMapTransitionDetector.TryResolve(
                liveMapSession,
                mapSessionState.MapResult,
                drain,
                out transition,
                out statusText)) {
                return false;
            }

            mapSessionState.StopReceive();
            return true;
        }

        public RoLoginEnterMapResult Complete(
            RoPendingMapTransition transition,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null,
            Func<string, Task> sameMapTransitionBeforeAck = null) {
            if (transition.ReconnectToMapServer) {
                return transition.LiveMapSession.ReconnectMapServer(
                    transition.PreviousResult,
                    transition.ServerMove,
                    loadRuntimeMapBeforeAck);
            }

            RoRuntimeMapLoadBeforeAckHandler beforeAck;
            if (transition.RequiresRuntimeMapLoad) {
                beforeAck = loadRuntimeMapBeforeAck;
            } else if (sameMapTransitionBeforeAck == null) {
                beforeAck = null;
            } else {
                beforeAck = (mapName, _, _) => sameMapTransitionBeforeAck(mapName);
            }

            return transition.LiveMapSession.AcknowledgeSameServerMapMove(
                transition.PreviousResult,
                transition.MapMove,
                beforeAck,
                requireInitialState: !transition.IsSameMapMove);
        }

        public bool CanApply(RoShellMapSessionState mapSessionState, RoPendingMapTransition transition) {
            return mapSessionState != null &&
                mapSessionState.IsCurrentResult(transition.PreviousResult) &&
                mapSessionState.IsCurrentSession(transition.LiveMapSession);
        }

        public RoLiveMapSession ResolveSessionToDispose(
            RoPendingMapTransition transition,
            RoLiveMapSession replacedSession,
            RoLoginEnterMapResult nextResult) {
            RoLiveMapSession candidate = transition.ReconnectToMapServer
                ? transition.LiveMapSession
                : replacedSession;
            if (candidate != null &&
                nextResult != null &&
                !ReferenceEquals(candidate, nextResult.MapSession)) {
                return candidate;
            }

            return null;
        }

        public RoLiveMapSession StopFailedTransition(
            RoShellMapSessionState mapSessionState,
            RoPendingMapTransition transition) {
            if (!CanApply(mapSessionState, transition)) {
                return null;
            }

            mapSessionState.RestartReceiveIfCurrent(transition.LiveMapSession);
            return null;
        }
    }
}
