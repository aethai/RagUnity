using System;
using System.IO;
using Ragnarok.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    internal static class RoPerfCaptureMenu {
        private const string FolderName = "PerformanceCaptures";

        [MenuItem("Tools/Ragnarok/Performance/Record 30s", false, 100)]
        private static void Capture30() => StartCapture(30);

        [MenuItem("Tools/Ragnarok/Performance/Record 60s", false, 101)]
        private static void Capture60() => StartCapture(60);

        [MenuItem("Tools/Ragnarok/Performance/Record 120s", false, 102)]
        private static void Capture120() => StartCapture(120);

        [MenuItem("Tools/Ragnarok/Performance/Stop Recording", false, 110)]
        private static void StopRecording() {
            if (!RoPerfCapture.StopActiveCapture()) {
                EditorUtility.DisplayDialog(
                    "Ragnarok Performance Capture",
                    "No performance capture is currently recording.",
                    "OK");
            }
        }

        [MenuItem("Tools/Ragnarok/Performance/Open Captures Folder", false, 120)]
        private static void OpenFolder() {
            string dir = CaptureRootDirectory();
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private static void StartCapture(int seconds) {
            if (!EditorApplication.isPlaying) {
                EditorUtility.DisplayDialog(
                    "Ragnarok Performance Capture",
                    "Enter Play Mode first, then use Tools/Ragnarok/Performance/Record to capture runtime performance.",
                    "OK");
                return;
            }

            if (RoPerfCapture.IsRecording) {
                EditorUtility.DisplayDialog(
                    "Ragnarok Performance Capture",
                    "A performance capture is already recording. Stop it before starting another capture.",
                    "OK");
                return;
            }

            string dir = BuildCaptureDirectory(seconds);
            Directory.CreateDirectory(dir);
            RoPerfCapture.BeginCapture(seconds, dir);
            Debug.Log("[RoPerfCapture] Recording " + seconds + "s. Reports will be written to " + dir);
        }

        private static string BuildCaptureDirectory(int seconds) {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string scene = SanitizePathSegment(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            if (string.IsNullOrEmpty(scene)) {
                scene = "playmode";
            }

            return Path.Combine(CaptureRootDirectory(), stamp + "_" + scene + "_" + seconds + "s");
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
            var chars = value.Trim().ToCharArray();
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
    }
}
