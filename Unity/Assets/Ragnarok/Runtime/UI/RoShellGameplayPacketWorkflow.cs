using System;
using System.Threading.Tasks;
using Ragnarok.Gameplay;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal readonly struct RoShellPreparedGameplayPacket {
        public RoShellPreparedGameplayPacket(byte[] packet, string sendingStatus, string sentStatus) {
            Packet = packet ?? throw new ArgumentNullException(nameof(packet));
            SendingStatus = sendingStatus ?? "sending gameplay request";
            SentStatus = sentStatus ?? "sent gameplay request";
        }

        public byte[] Packet { get; }
        public string SendingStatus { get; }
        public string SentStatus { get; }
    }

    internal sealed class RoShellGameplayPacketWorkflow {
        private readonly object sync;
        private readonly Action throwIfDisposedLocked;
        private readonly Action throwIfBusyLocked;
        private readonly Func<RoLiveMapSession> requireOpenMapSessionLocked;
        private readonly Func<RoGameplaySnapshot> gameplaySnapshotLocked;
        private readonly RoShellLiveSendExecutor liveSendExecutor;
        private readonly RoShellActionLockState actionLockState;
        private readonly Func<uint> localActorIdLocked;
        private readonly Action<string> setStatusLocked;

        public RoShellGameplayPacketWorkflow(
            object sync,
            Action throwIfDisposedLocked,
            Action throwIfBusyLocked,
            Func<RoLiveMapSession> requireOpenMapSessionLocked,
            Func<RoGameplaySnapshot> gameplaySnapshotLocked,
            RoShellLiveSendExecutor liveSendExecutor,
            RoShellActionLockState actionLockState,
            Func<uint> localActorIdLocked,
            Action<string> setStatusLocked) {
            this.sync = sync ?? throw new ArgumentNullException(nameof(sync));
            this.throwIfDisposedLocked = throwIfDisposedLocked ?? throw new ArgumentNullException(nameof(throwIfDisposedLocked));
            this.throwIfBusyLocked = throwIfBusyLocked ?? throw new ArgumentNullException(nameof(throwIfBusyLocked));
            this.requireOpenMapSessionLocked = requireOpenMapSessionLocked ?? throw new ArgumentNullException(nameof(requireOpenMapSessionLocked));
            this.gameplaySnapshotLocked = gameplaySnapshotLocked ?? throw new ArgumentNullException(nameof(gameplaySnapshotLocked));
            this.liveSendExecutor = liveSendExecutor ?? throw new ArgumentNullException(nameof(liveSendExecutor));
            this.actionLockState = actionLockState ?? throw new ArgumentNullException(nameof(actionLockState));
            this.localActorIdLocked = localActorIdLocked ?? throw new ArgumentNullException(nameof(localActorIdLocked));
            this.setStatusLocked = setStatusLocked ?? throw new ArgumentNullException(nameof(setStatusLocked));
        }

        public Task Begin(
            Func<RoGameplaySnapshot, byte[]> packetFactory,
            string sendingStatus,
            string sentStatus,
            RoLocalActionLockRequest actionLockRequest = default) {
            if (packetFactory == null) {
                throw new ArgumentNullException(nameof(packetFactory));
            }

            return Begin(
                snapshot => new RoShellPreparedGameplayPacket(
                    packetFactory(snapshot),
                    sendingStatus,
                    sentStatus),
                actionLockRequest);
        }

        public Task Begin(
            Func<RoGameplaySnapshot, RoShellPreparedGameplayPacket> prepare,
            RoLocalActionLockRequest actionLockRequest = default) {
            return Begin(prepare, null, null, actionLockRequest);
        }

        public Task Begin(
            Func<RoGameplaySnapshot, RoShellPreparedGameplayPacket> prepare,
            Action<RoGameplaySnapshot, RoShellPreparedGameplayPacket> afterPrepareLocked,
            RoLocalActionLockRequest actionLockRequest = default) {
            return Begin(prepare, afterPrepareLocked, null, actionLockRequest);
        }

        public Task Begin(
            Func<RoGameplaySnapshot, RoShellPreparedGameplayPacket> prepare,
            Action<RoGameplaySnapshot, RoShellPreparedGameplayPacket> afterPrepareLocked,
            Action<RoGameplaySnapshot, RoShellPreparedGameplayPacket, DateTime> afterSentLocked,
            RoLocalActionLockRequest actionLockRequest = default) {
            if (prepare == null) {
                throw new ArgumentNullException(nameof(prepare));
            }

            RoLiveMapSession liveMapSession;
            RoShellPreparedGameplayPacket prepared;
            RoGameplaySnapshot snapshot;

            lock (sync) {
                throwIfDisposedLocked();
                throwIfBusyLocked();
                if (actionLockRequest.IsActive) {
                    actionLockState.ThrowIfLocked(DateTime.UtcNow);
                }

                liveMapSession = requireOpenMapSessionLocked();
                snapshot = gameplaySnapshotLocked();
                prepared = prepare(snapshot);
                afterPrepareLocked?.Invoke(snapshot, prepared);
                setStatusLocked(prepared.SendingStatus);
            }

            return liveSendExecutor.CompletePreparedSend(liveMapSession, prepared.Packet, requestStartedUtc => {
                setStatusLocked(prepared.SentStatus);
                actionLockState.Apply(
                    RoMapEntryDrainResult.Empty,
                    localActorIdLocked(),
                    actionLockRequest,
                    requestStartedUtc);
                afterSentLocked?.Invoke(snapshot, prepared, requestStartedUtc);
            });
        }
    }
}
