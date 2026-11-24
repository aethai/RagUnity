using System;
using Ragnarok.Gameplay;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellSnapshotBuilder {
        private readonly RoShellUiState uiState;
        private readonly RoShellCharacterState characterState;
        private readonly RoPacketTraceBuffer traceBuffer;
        private readonly RoShellMapSessionState mapSessionState;
        private readonly RoGameplayState gameplayState;
        private readonly RoShellLiveDrainHistory liveDrainHistory;
        private readonly RoShellActionLockState actionLockState;
        private readonly Func<bool> isBusy;
        private readonly Func<bool> hasReconnectRequest;

        public RoShellSnapshotBuilder(
            RoShellUiState uiState,
            RoShellCharacterState characterState,
            RoPacketTraceBuffer traceBuffer,
            RoShellMapSessionState mapSessionState,
            RoGameplayState gameplayState,
            RoShellLiveDrainHistory liveDrainHistory,
            RoShellActionLockState actionLockState,
            Func<bool> isBusy,
            Func<bool> hasReconnectRequest = null) {
            this.uiState = uiState ?? throw new ArgumentNullException(nameof(uiState));
            this.characterState = characterState ?? throw new ArgumentNullException(nameof(characterState));
            this.traceBuffer = traceBuffer ?? throw new ArgumentNullException(nameof(traceBuffer));
            this.mapSessionState = mapSessionState ?? throw new ArgumentNullException(nameof(mapSessionState));
            this.gameplayState = gameplayState ?? throw new ArgumentNullException(nameof(gameplayState));
            this.liveDrainHistory = liveDrainHistory ?? throw new ArgumentNullException(nameof(liveDrainHistory));
            this.actionLockState = actionLockState ?? throw new ArgumentNullException(nameof(actionLockState));
            this.isBusy = isBusy ?? throw new ArgumentNullException(nameof(isBusy));
            this.hasReconnectRequest = hasReconnectRequest ?? (() => false);
        }

        public RoLoginShellSnapshot Build(DateTime nowUtc) {
            int actionLockRemaining = actionLockState.GetRemainingMilliseconds(nowUtc);
            return new RoLoginShellSnapshot(
                uiState.Stage,
                isBusy(),
                uiState.StatusText,
                uiState.ErrorText,
                actionLockRemaining,
                actionLockRemaining > 0 ? actionLockState.Reason : string.Empty,
                characterState.SelectedCharacterName,
                characterState.SnapshotCharacters(),
                traceBuffer.Snapshot(),
                mapSessionState.MapResult,
                gameplayState.Snapshot(),
                uiState.IsMapTransitionPending,
                uiState.IsMapTransitionLoading,
                uiState.IsSameMapTransitionPending,
                liveDrainHistory.LatestDrain,
                liveDrainHistory.Sequence,
                liveDrainHistory.SnapshotRecent(),
                characterState.SlotLimits,
                characterState.PageInfo,
                characterState.CharacterListTotalSlots,
                liveDrainHistory.HasLatestPublishTickCount,
                liveDrainHistory.LatestPublishTickCount,
                uiState.ReconnectState,
                uiState.ReconnectReason,
                uiState.CanManualReconnect && hasReconnectRequest());
        }

        public RoLoginShellSnapshot BuildPhase11Ui(DateTime nowUtc) {
            int actionLockRemaining = actionLockState.GetRemainingMilliseconds(nowUtc);
            return new RoLoginShellSnapshot(
                uiState.Stage,
                isBusy(),
                uiState.StatusText,
                uiState.ErrorText,
                actionLockRemaining,
                actionLockRemaining > 0 ? actionLockState.Reason : string.Empty,
                characterState.SelectedCharacterName,
                characterState.SnapshotCharacters(),
                Array.Empty<RoPacketTraceEntry>(),
                mapSessionState.MapResult,
                RoGameplaySnapshot.Empty,
                uiState.IsMapTransitionPending,
                uiState.IsMapTransitionLoading,
                uiState.IsSameMapTransitionPending,
                RoMapEntryDrainResult.Empty,
                liveDrainHistory.Sequence,
                Array.Empty<RoLiveDrainSnapshot>(),
                characterState.SlotLimits,
                characterState.PageInfo,
                characterState.CharacterListTotalSlots,
                false,
                0,
                uiState.ReconnectState,
                uiState.ReconnectReason,
                uiState.CanManualReconnect && hasReconnectRequest());
        }
    }
}
