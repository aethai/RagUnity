using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Player-side entry point for RoPerfCapture. Keeps the existing editor menu path intact while making
    /// standalone builds measurable with VSync disabled and a high target frame rate during capture.
    /// </summary>
    public sealed class RoPerfCaptureRuntimeControls : MonoBehaviour {
        private const string FolderName = "PerformanceCaptures";
        private const int DefaultTargetFrameRate = 300;
        private const float DefaultAutoCaptureDelaySeconds = 0f;
        private const string HotkeyChordPrefix = "Ctrl+Alt+Shift+";

        private static bool bootstrapped;

        private bool hotkeysEnabled;
        private bool oldInputUnavailable;
        private bool forceNoVsync = true;
        private bool stickyPerfMode;
        private bool perfModeApplied;
        private bool controllerStartedCapture;
        private bool wasRecording;
        private bool pendingAutoCapture;
        private int targetFrameRate = DefaultTargetFrameRate;
        private int previousVSyncCount;
        private int previousTargetFrameRate;
        private float pendingAutoCaptureSeconds;
        private float pendingAutoCaptureAtTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            bootstrapped = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() {
            if (bootstrapped) {
                return;
            }

            bootstrapped = true;
            var go = new GameObject("RoPerfCaptureRuntimeControls") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<RoPerfCaptureRuntimeControls>();
        }

        private void Awake() {
            DontDestroyOnLoad(gameObject);
            // development players only (a capture turns VSync off and writes next to the exe); -roPerfHotkeys still opts in
            hotkeysEnabled = !Application.isEditor && Debug.isDebugBuild;
            ParseConfiguration();

            if (hotkeysEnabled) {
                Debug.Log("[RoPerfCapturePlayer] Hotkeys enabled: " + HotkeyChordPrefix + "F9=30s, " + HotkeyChordPrefix + "F10=60s, " + HotkeyChordPrefix + "F11=120s, " + HotkeyChordPrefix + "F12=stop. Captures root: " + CaptureRootDirectory());
            }
        }

        private void Update() {
            bool recording = RoPerfCapture.IsRecording;
            if (wasRecording && !recording) {
                wasRecording = false;
                if (controllerStartedCapture && !stickyPerfMode) {
                    RestorePerfMode();
                }

                controllerStartedCapture = false;
            }

            if (pendingAutoCapture && Time.unscaledTime >= pendingAutoCaptureAtTime) {
                pendingAutoCapture = false;
                StartCapture(pendingAutoCaptureSeconds, "auto");
            }

            if (!hotkeysEnabled || oldInputUnavailable) {
                return;
            }

            if (WasKeyPressed(PerfKey.F9)) {
                StartCapture(30f, "hotkey");
            } else if (WasKeyPressed(PerfKey.F10)) {
                StartCapture(60f, "hotkey");
            } else if (WasKeyPressed(PerfKey.F11)) {
                StartCapture(120f, "hotkey");
            } else if (WasKeyPressed(PerfKey.F12)) {
                StopCapture();
            }
        }

        private void OnApplicationQuit() {
            RestorePerfMode();
        }

        private void ParseConfiguration() {
            float autoCaptureSeconds = 0f;
            float autoCaptureDelaySeconds = DefaultAutoCaptureDelaySeconds;
            bool applyPerfModeOnStart = false;

            string[] args = null;
            try {
                args = Environment.GetCommandLineArgs();
            } catch {
                args = null;
            }

            if (args != null) {
                for (int i = 0; i < args.Length; i++) {
                    string arg = args[i];
                    if (ArgEquals(arg, "-roPerfCapture") || ArgEquals(arg, "-roPerfCaptureSeconds") || ArgEquals(arg, "-ro-perf-capture")) {
                        TryReadNextFloat(args, ref i, out autoCaptureSeconds);
                    } else if (ArgEquals(arg, "-roPerfCapture30") || ArgEquals(arg, "-ro-perf-capture-30")) {
                        autoCaptureSeconds = 30f;
                    } else if (ArgEquals(arg, "-roPerfCapture60") || ArgEquals(arg, "-ro-perf-capture-60")) {
                        autoCaptureSeconds = 60f;
                    } else if (ArgEquals(arg, "-roPerfCapture120") || ArgEquals(arg, "-ro-perf-capture-120")) {
                        autoCaptureSeconds = 120f;
                    } else if (ArgEquals(arg, "-roPerfDelay") || ArgEquals(arg, "-roPerfDelaySeconds") || ArgEquals(arg, "-ro-perf-delay")) {
                        TryReadNextFloat(args, ref i, out autoCaptureDelaySeconds);
                    } else if (ArgEquals(arg, "-roPerfTargetFps") || ArgEquals(arg, "-ro-perf-target-fps")) {
                        TryReadNextInt(args, ref i, out targetFrameRate);
                    } else if (ArgEquals(arg, "-roPerfHotkeys") || ArgEquals(arg, "-ro-perf-hotkeys")) {
                        TryReadNextBool(args, ref i, out hotkeysEnabled);
                    } else if (ArgEquals(arg, "-roPerfNoVsync") || ArgEquals(arg, "-ro-perf-no-vsync")) {
                        forceNoVsync = true;
                    } else if (ArgEquals(arg, "-roPerfKeepVsync") || ArgEquals(arg, "-ro-perf-keep-vsync")) {
                        forceNoVsync = false;
                    } else if (ArgEquals(arg, "-roPerfMode") || ArgEquals(arg, "-ro-perf-mode")) {
                        applyPerfModeOnStart = true;
                    }
                }
            }

            TryReadEnvironmentFloat("RO_PERF_CAPTURE_SECONDS", ref autoCaptureSeconds);
            TryReadEnvironmentFloat("RO_PERF_DELAY_SECONDS", ref autoCaptureDelaySeconds);
            TryReadEnvironmentInt("RO_PERF_TARGET_FPS", ref targetFrameRate);
            TryReadEnvironmentBool("RO_PERF_HOTKEYS", ref hotkeysEnabled);
            TryReadEnvironmentBool("RO_PERF_NO_VSYNC", ref forceNoVsync);
            TryReadEnvironmentBool("RO_PERF_MODE", ref applyPerfModeOnStart);

            if (targetFrameRate <= 0) {
                targetFrameRate = DefaultTargetFrameRate;
            }

            if (applyPerfModeOnStart) {
                stickyPerfMode = true;
                ApplyPerfMode("startup");
            }

            if (autoCaptureSeconds > 0f) {
                pendingAutoCapture = true;
                pendingAutoCaptureSeconds = Mathf.Max(1f, autoCaptureSeconds);
                pendingAutoCaptureAtTime = Time.unscaledTime + Mathf.Max(0f, autoCaptureDelaySeconds);
                Debug.Log("[RoPerfCapturePlayer] Auto capture armed: " + pendingAutoCaptureSeconds.ToString("0", CultureInfo.InvariantCulture) + "s after " + Mathf.Max(0f, autoCaptureDelaySeconds).ToString("0.###", CultureInfo.InvariantCulture) + "s delay.");
            }
        }

        private void StartCapture(float seconds, string source) {
            if (RoPerfCapture.IsRecording) {
                Debug.LogWarning("[RoPerfCapturePlayer] Capture already recording; ignoring " + source + " start request.");
                return;
            }

            ApplyPerfMode(source);
            string directory = BuildCaptureDirectory(seconds);
            RoPerfCapture capture = RoPerfCapture.BeginCapture(seconds, directory);
            if (capture == null) {
                if (!stickyPerfMode) {
                    RestorePerfMode();
                }

                Debug.LogWarning("[RoPerfCapturePlayer] Could not start capture.");
                return;
            }

            controllerStartedCapture = true;
            wasRecording = true;
            Debug.Log("[RoPerfCapturePlayer] Recording " + seconds.ToString("0", CultureInfo.InvariantCulture) + "s -> " + directory);
        }

        private static void StopCapture() {
            if (!RoPerfCapture.StopActiveCapture()) {
                Debug.LogWarning("[RoPerfCapturePlayer] No performance capture is currently recording.");
            }
        }

        private void ApplyPerfMode(string source) {
            if (perfModeApplied) {
                return;
            }

            previousVSyncCount = QualitySettings.vSyncCount;
            previousTargetFrameRate = Application.targetFrameRate;
            Ragnarok.UI.RoDisplaySettings.PerfOverride = true; // the player's frame settings wait until the capture ends

            if (forceNoVsync) {
                QualitySettings.vSyncCount = 0;
            }

            Application.targetFrameRate = targetFrameRate;
            perfModeApplied = true;
            Debug.Log("[RoPerfCapturePlayer] Perf mode applied by " + source + ": vSyncCount=" + QualitySettings.vSyncCount + ", targetFrameRate=" + Application.targetFrameRate);
        }

        private void RestorePerfMode() {
            if (!perfModeApplied || stickyPerfMode) {
                return;
            }

            QualitySettings.vSyncCount = previousVSyncCount;
            Application.targetFrameRate = previousTargetFrameRate;
            perfModeApplied = false;
            Ragnarok.UI.RoDisplaySettings.PerfOverride = false;
            Ragnarok.UI.RoDisplaySettings.Apply();
            Debug.Log("[RoPerfCapturePlayer] Perf mode restored: vSyncCount=" + QualitySettings.vSyncCount + ", targetFrameRate=" + Application.targetFrameRate);
        }

        private static string BuildCaptureDirectory(float seconds) {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string scene = SanitizePathSegment(SceneManager.GetActiveScene().name);
            if (string.IsNullOrEmpty(scene)) {
                scene = "player";
            }

            return Path.Combine(CaptureRootDirectory(), stamp + "_" + scene + "_" + Mathf.CeilToInt(seconds).ToString(CultureInfo.InvariantCulture) + "s");
        }

        private static string CaptureRootDirectory() {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, FolderName);
        }

        private static string SanitizePathSegment(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++) {
                for (int j = 0; j < invalid.Length; j++) {
                    if (chars[i] == invalid[j]) {
                        chars[i] = '_';
                        break;
                    }
                }
            }

            return new string(chars).Replace(' ', '_').ToLowerInvariant();
        }

        private static bool ArgEquals(string value, string expected) {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        private enum PerfKey {
            F9,
            F10,
            F11,
            F12
        }

        private bool WasKeyPressed(PerfKey key) {
#if ENABLE_INPUT_SYSTEM
            global::UnityEngine.InputSystem.Keyboard keyboard = global::UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && ArePerfHotkeyModifiersPressed(keyboard)) {
                switch (key) {
                    case PerfKey.F9:
                        if (keyboard.f9Key.wasPressedThisFrame) {
                            return true;
                        }

                        break;
                    case PerfKey.F10:
                        if (keyboard.f10Key.wasPressedThisFrame) {
                            return true;
                        }

                        break;
                    case PerfKey.F11:
                        if (keyboard.f11Key.wasPressedThisFrame) {
                            return true;
                        }

                        break;
                    case PerfKey.F12:
                        if (keyboard.f12Key.wasPressedThisFrame) {
                            return true;
                        }

                        break;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try {
                if (!ArePerfHotkeyModifiersPressedLegacy()) {
                    return false;
                }

                switch (key) {
                    case PerfKey.F9:
                        return global::UnityEngine.Input.GetKeyDown(global::UnityEngine.KeyCode.F9);
                    case PerfKey.F10:
                        return global::UnityEngine.Input.GetKeyDown(global::UnityEngine.KeyCode.F10);
                    case PerfKey.F11:
                        return global::UnityEngine.Input.GetKeyDown(global::UnityEngine.KeyCode.F11);
                    case PerfKey.F12:
                        return global::UnityEngine.Input.GetKeyDown(global::UnityEngine.KeyCode.F12);
                }
            } catch (InvalidOperationException ex) {
                oldInputUnavailable = true;
                Debug.LogWarning("[RoPerfCapturePlayer] Legacy hotkeys disabled because UnityEngine.Input is unavailable: " + ex.Message);
            }
#endif

            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private static bool ArePerfHotkeyModifiersPressed(global::UnityEngine.InputSystem.Keyboard keyboard) {
            if (keyboard == null) {
                return false;
            }

            bool control = IsPressed(keyboard.leftCtrlKey) || IsPressed(keyboard.rightCtrlKey);
            bool alt = IsPressed(keyboard.leftAltKey) || IsPressed(keyboard.rightAltKey);
            bool shift = IsPressed(keyboard.leftShiftKey) || IsPressed(keyboard.rightShiftKey);
            return control && alt && shift;
        }

        private static bool IsPressed(global::UnityEngine.InputSystem.Controls.ButtonControl control) {
            return control != null && control.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private static bool ArePerfHotkeyModifiersPressedLegacy() {
            return (global::UnityEngine.Input.GetKey(global::UnityEngine.KeyCode.LeftControl) ||
                    global::UnityEngine.Input.GetKey(global::UnityEngine.KeyCode.RightControl)) &&
                (global::UnityEngine.Input.GetKey(global::UnityEngine.KeyCode.LeftAlt) ||
                    global::UnityEngine.Input.GetKey(global::UnityEngine.KeyCode.RightAlt)) &&
                (global::UnityEngine.Input.GetKey(global::UnityEngine.KeyCode.LeftShift) ||
                    global::UnityEngine.Input.GetKey(global::UnityEngine.KeyCode.RightShift));
        }
#endif

        private static bool TryReadNextFloat(string[] args, ref int index, out float value) {
            value = 0f;
            int nextIndex = index + 1;
            if (nextIndex >= args.Length || !TryParseFloat(args[nextIndex], out value)) {
                return false;
            }

            index = nextIndex;
            return true;
        }

        private static bool TryReadNextInt(string[] args, ref int index, out int value) {
            value = 0;
            int nextIndex = index + 1;
            if (nextIndex >= args.Length || !int.TryParse(args[nextIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
                return false;
            }

            index = nextIndex;
            return true;
        }

        private static bool TryReadNextBool(string[] args, ref int index, out bool value) {
            value = false;
            int nextIndex = index + 1;
            if (nextIndex >= args.Length || !TryParseBool(args[nextIndex], out value)) {
                return false;
            }

            index = nextIndex;
            return true;
        }

        private static void TryReadEnvironmentFloat(string key, ref float value) {
            string raw = Environment.GetEnvironmentVariable(key);
            if (TryParseFloat(raw, out float parsed)) {
                value = parsed;
            }
        }

        private static void TryReadEnvironmentInt(string key, ref int value) {
            string raw = Environment.GetEnvironmentVariable(key);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) {
                value = parsed;
            }
        }

        private static void TryReadEnvironmentBool(string key, ref bool value) {
            string raw = Environment.GetEnvironmentVariable(key);
            if (TryParseBool(raw, out bool parsed)) {
                value = parsed;
            }
        }

        private static bool TryParseFloat(string raw, out float value) {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseBool(string raw, out bool value) {
            value = false;
            if (string.IsNullOrWhiteSpace(raw)) {
                return false;
            }

            if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase) || raw.Equals("on", StringComparison.OrdinalIgnoreCase)) {
                value = true;
                return true;
            }

            if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw.Equals("no", StringComparison.OrdinalIgnoreCase) || raw.Equals("off", StringComparison.OrdinalIgnoreCase)) {
                value = false;
                return true;
            }

            return false;
        }
    }
}
