using System;
using System.Threading.Tasks;

namespace Ragnarok.UI {
    internal sealed class RoShellTaskRunner {
        private readonly object sync = new object();
        private Task activeTask;

        public bool IsBusy {
            get {
                lock (sync) {
                    return IsBusyLocked();
                }
            }
        }

        public Task Start(Action action, Action<Exception> onError) {
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            Task task = new Task(() => {
                try {
                    action();
                } catch (Exception ex) {
                    onError?.Invoke(ex);
                }
            });

            lock (sync) {
                if (IsBusyLocked()) {
                    throw new InvalidOperationException("Login shell operation is already running.");
                }

                activeTask = task;
            }

            task.ContinueWith(completed => {
                lock (sync) {
                    if (ReferenceEquals(activeTask, completed)) {
                        activeTask = null;
                    }
                }
            }, TaskScheduler.Default);

            task.Start(TaskScheduler.Default);
            return task;
        }

        public void ThrowIfBusy() {
            if (IsBusy) {
                throw new InvalidOperationException("Login shell operation is already running.");
            }
        }

        private bool IsBusyLocked() {
            return activeTask != null && !activeTask.IsCompleted;
        }
    }
}
