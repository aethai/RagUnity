using System.Threading.Tasks;
using Ragnarok.Diagnostics;
using UnityEngine;

namespace Ragnarok.Content {
    public sealed class RagnarokContentBootstrap : MonoBehaviour {
        [SerializeField] private RagnarokContentSettings settings;
        [SerializeField] private bool useSavedProfileIfSettingsMissing = true;
        [SerializeField] private bool initializeAsynchronouslyOnAwake = true;

        public RagnarokContentService Content { get; private set; }
        public Task StartupInitializeTask { get; private set; }

        private void Awake() {
            Content = new RagnarokContentService();

            if (settings != null) {
                StartupInitializeTask = initializeAsynchronouslyOnAwake
                    ? Content.BeginInitializeForStartup(settings)
                    : RunSynchronousInitialize(settings);
                return;
            }

            if (useSavedProfileIfSettingsMissing && BeginSavedProfileInitialization(out string report)) {
                if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                    RoRuntimeDiagnostics.IncrementDebugLogLine();
                    Debug.Log(report, this);
                }
                return;
            }

            if (BeginLocalSettingsInitialization(out report)) {
                if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                    RoRuntimeDiagnostics.IncrementDebugLogLine();
                    Debug.Log(report, this);
                }
                return;
            }

            if (useSavedProfileIfSettingsMissing) {
                Debug.LogWarning("RO content bootstrap has no scene settings and no saved content profile.", this);
            }
        }

        private bool BeginLocalSettingsInitialization(out string report) {
            if (!RagnarokContentProfileStore.TryLoadLocalSettings(out RagnarokContentSettings localSettings, out report)) {
                return false;
            }

            try {
                if (initializeAsynchronouslyOnAwake) {
                    StartupInitializeTask = Content.BeginInitializeForStartup(localSettings);
                    return true;
                }

                Content.Initialize(localSettings);
                StartupInitializeTask = Task.CompletedTask;
                return true;
            } catch (System.Exception ex) {
                report = $"Could not start local RO content settings initialization: {ex.Message}";
                StartupInitializeTask = null;
                return false;
            }
        }

        private bool BeginSavedProfileInitialization(out string report) {
            if (!RagnarokContentProfileStore.TryLoad(out RagnarokContentProfile profile, out report)) {
                return false;
            }

            try {
                if (initializeAsynchronouslyOnAwake) {
                    StartupInitializeTask = Content.BeginInitializeForStartup(profile);
                    return true;
                }

                Content.Initialize(profile);
                StartupInitializeTask = Task.CompletedTask;
                return true;
            } catch (System.Exception ex) {
                report = $"Could not start saved RO content profile initialization: {ex.Message}";
                StartupInitializeTask = null;
                return false;
            }
        }

        private Task RunSynchronousInitialize(RagnarokContentSettings contentSettings) {
            Content.Initialize(contentSettings);
            return Task.CompletedTask;
        }

    }
}
