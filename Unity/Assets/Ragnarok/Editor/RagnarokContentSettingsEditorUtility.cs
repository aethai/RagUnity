using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ragnarok.Content;
using UnityEditor;

namespace Ragnarok.Editor {
    public static class RagnarokContentSettingsEditorUtility {
        private static readonly string[] PreferredArchiveOrder = {
            "data_en.grf",
            "data_en.gpf",
            "rdata.grf",
            "sdata.grf",
            "adata.grf",
            "data.grf"
        };

        private static readonly string[] KnownOverlayArchives = {
            "data_en.grf",
            "data_en.gpf"
        };

        private static readonly string[] DataIniCandidates = {
            "DATA.INI",
            "data.ini"
        };

        public static bool SelectAndApplyContentFolder(RagnarokContentSettings settings, out string report) {
            if (settings == null) {
                report = "No RagnarokContentSettings asset selected.";
                return false;
            }

            string initialPath = Directory.Exists(settings.RootPath) ? settings.RootPath : string.Empty;
            string selectedPath = EditorUtility.OpenFolderPanel("Select Ragnarok folder containing GRF files", initialPath, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedPath)) {
                report = "Folder selection cancelled.";
                return false;
            }

            return ApplyContentFolder(settings, selectedPath, out report);
        }

        public static bool ApplyContentFolder(RagnarokContentSettings settings, string rootPath, out string report) {
            if (settings == null) {
                report = "No RagnarokContentSettings asset selected.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) {
                report = $"Folder does not exist: {rootPath}";
                return false;
            }

            List<string> archives = DetectArchiveFiles(rootPath);
            ApplySerializedSettings(settings, rootPath, archives);

            report = BuildReport(rootPath, archives);
            return archives.Count > 0;
        }

        public static bool ApplyContentProfile(RagnarokContentSettings settings, string rootPath, IReadOnlyList<string> archives, out string report) {
            if (settings == null) {
                report = "No RagnarokContentSettings asset selected.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) {
                report = $"Folder does not exist: {rootPath}";
                return false;
            }

            archives ??= new List<string>();
            ApplySerializedSettings(settings, rootPath, archives);
            report = BuildReport(rootPath, archives);
            return archives.Count > 0;
        }

        public static List<string> DetectArchiveFiles(string rootPath) {
            var files = new List<string>();
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) {
                return files;
            }

            List<string> dataIniArchives = DetectArchivesFromDataIni(rootPath);
            if (dataIniArchives.Count > 0) {
                return IncludeKnownOverlays(rootPath, dataIniArchives);
            }

            files.AddRange(Directory.GetFiles(rootPath, "*.grf", SearchOption.TopDirectoryOnly));
            files.AddRange(Directory.GetFiles(rootPath, "*.gpf", SearchOption.TopDirectoryOnly));

            return files
                .Select(path => Path.GetRelativePath(rootPath, path).Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetPreferredOrder)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> IncludeKnownOverlays(string rootPath, IReadOnlyList<string> dataIniArchives) {
            var archives = new List<string>();
            foreach (string overlayArchive in KnownOverlayArchives) {
                string fullPath = Path.Combine(rootPath, overlayArchive);
                if (File.Exists(fullPath) &&
                    !dataIniArchives.Contains(overlayArchive, StringComparer.OrdinalIgnoreCase) &&
                    !archives.Contains(overlayArchive, StringComparer.OrdinalIgnoreCase)) {
                    archives.Add(overlayArchive);
                }
            }

            archives.AddRange(dataIniArchives);
            return archives;
        }

        private static List<string> DetectArchivesFromDataIni(string rootPath) {
            string dataIniPath = null;
            for (int i = 0; i < DataIniCandidates.Length; i++) {
                string candidate = Path.Combine(rootPath, DataIniCandidates[i]);
                if (File.Exists(candidate)) {
                    dataIniPath = candidate;
                    break;
                }
            }

            var archives = new List<(int Index, string Path)>();
            if (string.IsNullOrEmpty(dataIniPath)) {
                return new List<string>();
            }

            foreach (string rawLine in File.ReadAllLines(dataIniPath)) {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal)) {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals <= 0 || equals == line.Length - 1) {
                    continue;
                }

                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim().Trim('"');
                if (!int.TryParse(key, out int index) || string.IsNullOrWhiteSpace(value)) {
                    continue;
                }

                string normalized = value.Replace('\\', '/');
                string fullPath = Path.Combine(rootPath, normalized);
                if (!File.Exists(fullPath)) {
                    continue;
                }

                string extension = Path.GetExtension(normalized);
                if (!string.Equals(extension, ".grf", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".gpf", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                archives.Add((index, normalized));
            }

            if (archives.Count == 0) {
                return new List<string>();
            }

            return archives
                .OrderBy(entry => entry.Index)
                .Select(entry => entry.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ApplySerializedSettings(RagnarokContentSettings settings, string rootPath, IReadOnlyList<string> archives) {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("rootPath").stringValue = Path.GetFullPath(rootPath);

            SerializedProperty grfFiles = serialized.FindProperty("grfFiles");
            grfFiles.arraySize = archives.Count;
            for (int i = 0; i < archives.Count; i++) {
                grfFiles.GetArrayElementAtIndex(i).stringValue = archives[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static string BuildReport(string rootPath, IReadOnlyList<string> archives) {
            var lines = new List<string> {
                $"RO content folder: {Path.GetFullPath(rootPath)}",
                $"Detected archive files: {archives.Count}"
            };

            if (archives.Count == 0) {
                lines.Add("No .grf/.gpf files found in the selected folder.");
                return string.Join(Environment.NewLine, lines);
            }

            lines.Add("Archive lookup order is top-to-bottom:");
            for (int i = 0; i < archives.Count; i++) {
                lines.Add($"- {i}: {archives[i]}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static int GetPreferredOrder(string archivePath) {
            string fileName = Path.GetFileName(archivePath);
            for (int i = 0; i < PreferredArchiveOrder.Length; i++) {
                if (string.Equals(fileName, PreferredArchiveOrder[i], StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }

            return PreferredArchiveOrder.Length;
        }
    }
}
