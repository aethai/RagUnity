using System;
using System.Threading.Tasks;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellLiveSendExecutor {
        private readonly object sync;
        private readonly Func<bool> isDisposedLocked;
        private readonly Func<RoLiveMapSession, bool> isCurrentSessionLocked;
        private readonly Action<string, string> setErrorLocked;
        private readonly Func<string> connectionErrorText;

        public RoShellLiveSendExecutor(
            object sync,
            Func<bool> isDisposedLocked,
            Func<RoLiveMapSession, bool> isCurrentSessionLocked,
            Action<string, string> setErrorLocked,
            Func<string> connectionErrorText) {
            this.sync = sync ?? throw new ArgumentNullException(nameof(sync));
            this.isDisposedLocked = isDisposedLocked ?? throw new ArgumentNullException(nameof(isDisposedLocked));
            this.isCurrentSessionLocked = isCurrentSessionLocked ?? throw new ArgumentNullException(nameof(isCurrentSessionLocked));
            this.setErrorLocked = setErrorLocked ?? throw new ArgumentNullException(nameof(setErrorLocked));
            this.connectionErrorText = connectionErrorText ?? throw new ArgumentNullException(nameof(connectionErrorText));
        }

        public Task CompletePreparedSend(RoLiveMapSession liveMapSession, byte[] packet, Action<DateTime> onSentLocked) {
            if (liveMapSession == null) {
                throw new InvalidOperationException("Map session is not open.");
            }

            DateTime requestStartedUtc = DateTime.UtcNow;
            try {
                liveMapSession.Send(packet);
            } catch (ObjectDisposedException ex) {
                SetErrorIfCurrent(liveMapSession, "Map session is not open.");
                throw new InvalidOperationException("Map session is not open.", ex);
            } catch (Exception ex) {
                SetErrorIfCurrent(liveMapSession, ex.Message);
                throw;
            }

            lock (sync) {
                if (!isDisposedLocked() && isCurrentSessionLocked(liveMapSession)) {
                    onSentLocked?.Invoke(requestStartedUtc);
                }
            }

            return Task.FromResult(0);
        }

        public bool TrySend(RoLiveMapSession liveMapSession, byte[] packet) {
            try {
                liveMapSession.Send(packet);
                return true;
            } catch (ObjectDisposedException) {
                return false;
            } catch (Exception ex) {
                SetErrorIfCurrent(liveMapSession, ex.Message);
                return false;
            }
        }

        private void SetErrorIfCurrent(RoLiveMapSession liveMapSession, string message) {
            lock (sync) {
                if (!isDisposedLocked() && isCurrentSessionLocked(liveMapSession)) {
                    setErrorLocked(connectionErrorText(), message ?? string.Empty);
                }
            }
        }
    }
}
