namespace Ragnarok.UI {
    internal sealed class RoShellUiState {
        public RoShellUiState(RoLoginShellStage stage, string statusText) {
            if (IsLoginMode(stage)) {
                inLoginMode = true;
                Ragnarok.Audio.RoAudioSystem.SetLoginMode(true);
                Ragnarok.Audio.RoAudioSystem.PlayLoginBgm();
            }

            Stage = stage;
            StatusText = statusText ?? string.Empty;
            ErrorText = string.Empty;
        }

        private readonly RoReconnectPolicy reconnectPolicy = new RoReconnectPolicy();

        /// <summary>
        /// In the client's login mode (CLoginMode: login, the server list, character select). Entering it (the start, a return
        /// from the game) restarts bgm\01.mp3 from 0 (CLoginMode::vf02 0xcb5760); moving between its windows keeps the
        /// track playing. A failure keeps the mode it happened in.
        /// </summary>
        private bool inLoginMode;

        private static bool IsLoginMode(RoLoginShellStage stage) =>
            stage == RoLoginShellStage.Login || stage == RoLoginShellStage.ServerSelect || stage == RoLoginShellStage.LoadingCharacters ||
            stage == RoLoginShellStage.CharacterSelect;

        public RoLoginShellStage Stage { get; private set; }
        public string StatusText { get; private set; }
        public string ErrorText { get; private set; }
        public bool IsMapTransitionPending { get; private set; }
        public bool IsMapTransitionLoading { get; private set; }
        public bool IsSameMapTransitionPending { get; private set; }
        public RoReconnectState ReconnectState => reconnectPolicy.State;
        public string ReconnectReason => reconnectPolicy.Reason;
        public bool CanManualReconnect => reconnectPolicy.CanManualReconnect;

        public void SetStage(RoLoginShellStage stage, string statusText, string errorText = "") {
            if (IsLoginMode(stage)) {
                if (!inLoginMode) {
                    inLoginMode = true;
                    Ragnarok.Audio.RoAudioSystem.SetLoginMode(true);
                    Ragnarok.Audio.RoAudioSystem.PlayLoginBgm();
                }
            } else if (stage != RoLoginShellStage.Failed) {
                inLoginMode = false;
                Ragnarok.Audio.RoAudioSystem.SetLoginMode(false);
            }

            Stage = stage;
            StatusText = statusText ?? string.Empty;
            ErrorText = errorText ?? string.Empty;
            IsMapTransitionPending = false;
            IsMapTransitionLoading = false;
            IsSameMapTransitionPending = false;
            if (string.IsNullOrWhiteSpace(ErrorText) &&
                (stage != RoLoginShellStage.EnteringMap ||
                 reconnectPolicy.State != RoReconnectState.Reconnecting)) {
                reconnectPolicy.MarkStable(Ragnarok.Network.RoMonotonicTime.NowMilliseconds);
            }
        }

        public void SetStatus(string statusText, string errorText = "") {
            StatusText = statusText ?? string.Empty;
            ErrorText = errorText ?? string.Empty;
            IsMapTransitionPending = false;
            IsMapTransitionLoading = false;
            IsSameMapTransitionPending = false;
        }

        public void SetError(string statusText, string errorText) {
            StatusText = statusText ?? string.Empty;
            ErrorText = errorText ?? string.Empty;
            IsMapTransitionPending = false;
            IsMapTransitionLoading = false;
            IsSameMapTransitionPending = false;
        }

        public void SetConnectionLost(string statusText, string errorText, long nowMs) {
            SetError(statusText, errorText);
            reconnectPolicy.MarkConnectionLost(ErrorText, nowMs);
        }

        public bool TryBeginManualReconnect(long nowMs) {
            return reconnectPolicy.TryBeginManualReconnect(nowMs);
        }

        public bool TryBeginAutoReconnect(long nowMs) {
            return reconnectPolicy.TryBeginAutoReconnect(nowMs);
        }

        public bool IsAutoReconnectDue(long nowMs) {
            return reconnectPolicy.IsAutoReconnectDue(nowMs);
        }

        public void MarkReconnectStable(long nowMs) {
            reconnectPolicy.MarkStable(nowMs);
        }

        public void ResetReconnect() {
            reconnectPolicy.Reset();
        }

        public void SetReconnectFailed(string statusText, string errorText) {
            SetError(statusText, errorText);
            reconnectPolicy.MarkFailed(ErrorText);
        }

        public void BeginMapTransition(
            string statusText,
            bool showLoadingScreen = true,
            bool sameMapTransition = false) {
            StatusText = statusText ?? string.Empty;
            ErrorText = string.Empty;
            IsMapTransitionPending = true;
            IsMapTransitionLoading = showLoadingScreen;
            IsSameMapTransitionPending = sameMapTransition;
        }
    }
}
