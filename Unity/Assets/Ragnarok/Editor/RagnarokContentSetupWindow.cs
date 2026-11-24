using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Content;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    public sealed class RagnarokContentSetupWindow : EditorWindow {
        private static readonly string[] BaselineAssets = {
            "data/prontera.rsw",
            "data/geffen.rsw",
            "data/payon.rsw",
            "data/izlude.rsw",
            "data/prt_in.rsw",
            "data/pay_dun00.rsw"
        };

        private const string LocalSettingsPath = "Assets/LocalRagnarokContent.asset";

        private RagnarokContentSettings settings;
        private string rootPath = "";
        private List<string> archives = new List<string>();
        private string report = "Select a legal Ragnarok/GRF folder, then save a runtime content profile.";
        private Vector2 scroll;

        [MenuItem("Ragnarok/Content Setup")]
        public static void Open() {
            RagnarokContentSetupWindow window = GetWindow<RagnarokContentSetupWindow>("RO Content Setup");
            window.minSize = new Vector2(620f, 520f);
            window.Show();
        }

        private void OnEnable() {
            settings = LoadOrCreateLocalSettings(false);
            LoadInitialProfile();
        }

        private void OnGUI() {
            EditorGUILayout.LabelField("Ragnarok Content Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This stores the active RO content folder outside scene inspector fields. Use only legal local GRF/data files.",
                MessageType.Info
            );

            EditorGUILayout.Space();
            settings = (RagnarokContentSettings) EditorGUILayout.ObjectField("Optional Settings Asset", settings, typeof(RagnarokContentSettings), false);
            rootPath = EditorGUILayout.TextField("Content Root", rootPath);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Select Folder")) {
                    SelectFolder();
                }

                if (GUILayout.Button("Detect GRFs")) {
                    DetectArchives();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Detected Archives ({archives.Count})", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true)) {
                for (int i = 0; i < archives.Count; i++) {
                    EditorGUILayout.TextField($"{i}", archives[i]);
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Validate")) {
                    ValidateProfile();
                }

                if (GUILayout.Button("Save Runtime Profile")) {
                    SaveRuntimeProfile();
                }

                if (GUILayout.Button("Apply To Local Settings Asset")) {
                    ApplyToLocalSettingsAsset();
                }
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Load Saved Profile")) {
                    LoadSavedProfile(true);
                }

                if (GUILayout.Button("Open Diagnostics")) {
                    RagnarokContentDiagnosticsWindow.Open(settings);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void LoadInitialProfile() {
            if (RagnarokContentProfileStore.TryLoad(out RagnarokContentProfile profile, out string profileReport)) {
                ApplyProfile(profile);
                report = profileReport;
                return;
            }

            if (settings != null && !string.IsNullOrWhiteSpace(settings.RootPath)) {
                rootPath = settings.RootPath;
                archives = new List<string>(settings.GrfFiles);
                report = "Loaded initial content values from LocalRagnarokContent.asset.";
                return;
            }

            report = profileReport;
        }

        private void SelectFolder() {
            string initialPath = Directory.Exists(rootPath) ? rootPath : "";
            string selected = EditorUtility.OpenFolderPanel("Select Ragnarok folder containing GRF files", initialPath, "");
            if (string.IsNullOrWhiteSpace(selected)) {
                report = "Folder selection cancelled.";
                return;
            }

            rootPath = selected;
            DetectArchives();
        }

        private void DetectArchives() {
            archives = RagnarokContentSettingsEditorUtility.DetectArchiveFiles(rootPath);
            report = BuildArchiveReport();
        }

        private void ValidateProfile() {
            if (!TryCreateProfile(out RagnarokContentProfile profile)) {
                return;
            }

            using var assets = new RoAssetDatabase();
            try {
                assets.Initialize(profile.RootPath, profile.GrfFiles);
                List<string> missing = assets.ValidateRequiredFiles(BaselineAssets);

                var builder = new StringBuilder();
                builder.AppendLine(assets.BuildSummary());
                builder.AppendLine();
                if (missing.Count == 0) {
                    builder.AppendLine("Baseline map validation passed.");
                } else {
                    builder.AppendLine($"Baseline map validation failed: {missing.Count} missing file(s).");
                    for (int i = 0; i < missing.Count; i++) {
                        builder.AppendLine("- " + missing[i]);
                    }
                }

                report = builder.ToString().TrimEnd();
            } catch (Exception ex) {
                report = ex.ToString();
                Debug.LogException(ex);
            }
        }

        private void SaveRuntimeProfile() {
            if (!TryCreateProfile(out RagnarokContentProfile profile)) {
                return;
            }

            try {
                RagnarokContentProfileStore.Save(profile);
                report = $"Saved runtime RO content profile to:\n{RagnarokContentProfileStore.ResolveSaveProfilePath()}";
            } catch (Exception ex) {
                report = ex.ToString();
                Debug.LogException(ex);
            }
        }

        private void ApplyToLocalSettingsAsset() {
            if (!TryCreateProfile(out RagnarokContentProfile profile)) {
                return;
            }

            settings = LoadOrCreateLocalSettings(true);
            if (RagnarokContentSettingsEditorUtility.ApplyContentProfile(settings, profile.RootPath, profile.GrfFiles, out string utilityReport)) {
                report = utilityReport;
            } else {
                report = utilityReport;
            }
        }

        private void LoadSavedProfile(bool explicitRequest) {
            if (RagnarokContentProfileStore.TryLoad(out RagnarokContentProfile profile, out string profileReport)) {
                ApplyProfile(profile);
            }

            report = profileReport;
            if (explicitRequest) {
                Repaint();
            }
        }

        private bool TryCreateProfile(out RagnarokContentProfile profile) {
            profile = null;
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) {
                report = $"Content root does not exist: {rootPath}";
                return false;
            }

            if (archives == null || archives.Count == 0) {
                report = "No GRF/GPF archives detected. Select a folder or click Detect GRFs.";
                return false;
            }

            profile = new RagnarokContentProfile("local", Path.GetFullPath(rootPath), archives);
            return true;
        }

        private void ApplyProfile(RagnarokContentProfile profile) {
            rootPath = profile.RootPath;
            archives = new List<string>(profile.GrfFiles);
        }

        private string BuildArchiveReport() {
            var builder = new StringBuilder();
            builder.AppendLine($"Content root: {rootPath}");
            builder.AppendLine($"Detected archive files: {archives.Count}");
            for (int i = 0; i < archives.Count; i++) {
                builder.AppendLine($"- {i}: {archives[i]}");
            }

            return builder.ToString().TrimEnd();
        }

        private static RagnarokContentSettings LoadOrCreateLocalSettings(bool createIfMissing) {
            RagnarokContentSettings asset = AssetDatabase.LoadAssetAtPath<RagnarokContentSettings>(LocalSettingsPath);
            if (asset != null || !createIfMissing) {
                return asset;
            }

            asset = CreateInstance<RagnarokContentSettings>();
            AssetDatabase.CreateAsset(asset, LocalSettingsPath);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
