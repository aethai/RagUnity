using System.IO;
using Ragnarok.UI;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    [CustomEditor(typeof(RoMinimalLoginShell))]
    public sealed class RoMinimalLoginShellEditor : UnityEditor.Editor {
        private const string MenuRoot = "Ragnarok/Runtime Perf Capture/";

        [MenuItem(MenuRoot + "Start 60s Capture", false, 2300)]
        private static void StartRuntimePerfCaptureFromMenu() {
            if (!TryFindShell(out RoMinimalLoginShell shell)) {
                Debug.LogWarning("RO runtime perf capture: no RoMinimalLoginShell found in the active scene.");
                return;
            }

            Selection.activeObject = shell.gameObject;
            shell.StartRuntimePerfCapture60Seconds();
        }

        [MenuItem(MenuRoot + "Start 60s Capture", true)]
        private static bool ValidateStartRuntimePerfCaptureFromMenu() {
            return Application.isPlaying &&
                TryFindShell(out RoMinimalLoginShell shell) &&
                !shell.IsRuntimePerfCaptureActive;
        }

        [MenuItem(MenuRoot + "Stop Capture", false, 2301)]
        private static void StopRuntimePerfCaptureFromMenu() {
            if (!TryFindShell(out RoMinimalLoginShell shell)) {
                Debug.LogWarning("RO runtime perf capture: no RoMinimalLoginShell found in the active scene.");
                return;
            }

            Selection.activeObject = shell.gameObject;
            shell.StopRuntimePerfCaptureNow();
        }

        [MenuItem(MenuRoot + "Stop Capture", true)]
        private static bool ValidateStopRuntimePerfCaptureFromMenu() {
            return Application.isPlaying &&
                TryFindShell(out RoMinimalLoginShell shell) &&
                shell.IsRuntimePerfCaptureActive;
        }

        [MenuItem(MenuRoot + "Reveal Last Capture", false, 2302)]
        private static void RevealLastRuntimePerfCaptureFromMenu() {
            if (TryFindShell(out RoMinimalLoginShell shell) &&
                !string.IsNullOrEmpty(shell.LastRuntimePerfCapturePath)) {
                EditorUtility.RevealInFinder(shell.LastRuntimePerfCapturePath);
                return;
            }

            string latestCapture = FindLatestCapturePath();
            if (!string.IsNullOrEmpty(latestCapture)) {
                EditorUtility.RevealInFinder(latestCapture);
                return;
            }

            OpenRuntimePerfCaptureFolderFromMenu();
        }

        [MenuItem(MenuRoot + "Open Capture Folder", false, 2303)]
        private static void OpenRuntimePerfCaptureFolderFromMenu() {
            string folder = GetRuntimePerfCaptureFolder();
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Perf Capture", EditorStyles.boldLabel);

            var shell = (RoMinimalLoginShell) target;
            using (new EditorGUI.DisabledScope(!Application.isPlaying)) {
                if (shell.IsRuntimePerfCaptureActive) {
                    EditorGUILayout.LabelField("Recording", $"{shell.RuntimePerfCaptureRemainingSeconds:F1}s left");
                    if (GUILayout.Button("Stop Runtime Perf Capture")) {
                        shell.StopRuntimePerfCaptureNow();
                    }
                } else if (GUILayout.Button("Start 60s Runtime Perf Capture")) {
                    shell.StartRuntimePerfCapture60Seconds();
                }
            }

            if (!Application.isPlaying) {
                EditorGUILayout.HelpBox("Enter Play Mode to record a runtime capture.", MessageType.Info);
            }

            if (!string.IsNullOrEmpty(shell.LastRuntimePerfCapturePath)) {
                EditorGUILayout.LabelField("Last capture");
                EditorGUILayout.SelectableLabel(
                    shell.LastRuntimePerfCapturePath,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Reveal Last Capture")) {
                    EditorUtility.RevealInFinder(shell.LastRuntimePerfCapturePath);
                }
            }

            if (shell.IsRuntimePerfCaptureActive) {
                Repaint();
            }
        }

        private static bool TryFindShell(out RoMinimalLoginShell shell) {
            shell = null;
            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected.TryGetComponent(out shell)) {
                return true;
            }

            shell = Object.FindFirstObjectByType<RoMinimalLoginShell>();
            return shell != null;
        }

        private static string FindLatestCapturePath() {
            string folder = GetRuntimePerfCaptureFolder();
            if (!Directory.Exists(folder)) {
                return string.Empty;
            }

            string latestPath = string.Empty;
            long latestWriteTicks = long.MinValue;
            string[] files = Directory.GetFiles(folder, "*.csv");
            for (int i = 0; i < files.Length; i++) {
                string path = files[i];
                long writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
                if (writeTicks <= latestWriteTicks) {
                    continue;
                }

                latestWriteTicks = writeTicks;
                latestPath = path;
            }

            return latestPath;
        }

        private static string GetRuntimePerfCaptureFolder() {
            return Path.Combine(Application.persistentDataPath, "RagnarokDiagnostics", "RuntimePerfCaptures");
        }
    }
}
