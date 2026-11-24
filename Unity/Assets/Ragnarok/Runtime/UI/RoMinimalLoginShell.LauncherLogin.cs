using Ragnarok.Network;

namespace Ragnarok.UI {
    public sealed partial class RoMinimalLoginShell {
        private bool launcherLoginStarted;
        private bool launcherServerConfirmed;

        /// <summary>
        /// Started by the Custom launcher (<see cref="RoLauncherTicket"/>): once the login window is up, log in as
        /// the launcher's account by itself, and with a single character server go on to the characters. The shell keeps
        /// the placeholder as its password; each CA_LOGIN of that account swaps in a fresh one-time token. Only the first
        /// login screen does this - after a disconnect the player is back on the ordinary login window.
        /// </summary>
        private void TryBeginLauncherLogin() {
            if (!RoLauncherTicket.Active || session == null) {
                return;
            }

            if (launcherLoginStarted) {
                if (!launcherServerConfirmed && session.Snapshot()?.Stage == RoLoginShellStage.ServerSelect) {
                    launcherServerConfirmed = RoClassicPreGame.TryConfirmSingleServer();
                }

                return;
            }

            if (!IsBrowserContentReady || !RoClassicPreGame.LoginVisible) {
                return;
            }

            RoLoginShellSnapshot snapshot = session.Snapshot();
            if (snapshot == null || snapshot.Stage != RoLoginShellStage.Login) {
                return;
            }

            launcherLoginStarted = true;
            account = RoLauncherTicket.Account;
            password = RoLauncherTicket.Placeholder;
            LoginFromPhase11Ui();
        }
    }
}
