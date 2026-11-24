using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Content;
using ROIO.Models.FileTypes;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    public sealed class RagnarokContentDiagnosticsWindow : EditorWindow {
        private static readonly string[] BaselineAssets = {
            "data/prontera.rsw",
            "data/geffen.rsw",
            "data/payon.rsw",
            "data/izlude.rsw",
            "data/prt_in.rsw",
            "data/pay_dun00.rsw"
        };

        private static readonly ProbeSpec[] ProbeSpecs = {
            new ProbeSpec("Map world", "rsw", ProbeKind.TypedLoad),
            new ProbeSpec("Map ground", "gnd", ProbeKind.TypedLoad),
            new ProbeSpec("Map altitude", "gat", ProbeKind.TypedLoad),
            new ProbeSpec("Model", "rsm", ProbeKind.TypedLoad),
            new ProbeSpec("Model RSM2", "rsm2", ProbeKind.TypedLoad),
            new ProbeSpec("Texture BMP", "bmp", ProbeKind.Texture),
            new ProbeSpec("Texture PNG", "png", ProbeKind.Texture),
            new ProbeSpec("Texture JPG", "jpg", ProbeKind.Texture),
            new ProbeSpec("Sound WAV", "wav", ProbeKind.TypedLoad),
            new ProbeSpec("Music MP3", "mp3", ProbeKind.RawLoad),
            new ProbeSpec("Sprite", "spr", ProbeKind.TypedLoad),
            new ProbeSpec("Action", "act", ProbeKind.TypedLoad),
            new ProbeSpec("Palette", "pal", ProbeKind.RawBytes),
            new ProbeSpec("Effect STR", "str", ProbeKind.TypedLoad),
            new ProbeSpec("Lua/Text", "lua", ProbeKind.Text),
            new ProbeSpec("Lua Binary", "lub", ProbeKind.RawLoad),
            new ProbeSpec("Font TTF", "ttf", ProbeKind.RawLoad),
            new ProbeSpec("Font OTF", "otf", ProbeKind.RawLoad),
            new ProbeSpec("Font FNT", "fnt", ProbeKind.RawLoad)
        };

        private RagnarokContentSettings settings;
        private RoAssetDatabase assets;
        private Vector2 scroll;
        private string lookupPath = "data/prontera.rsw";
        private int textureProbeLimit = 10;
        private string report = "Initialize the database to inspect RO content.";

        [MenuItem("Ragnarok/Content Diagnostics")]
        public static void Open() {
            RagnarokContentDiagnosticsWindow window = GetWindow<RagnarokContentDiagnosticsWindow>("RO Content");
            window.minSize = new Vector2(620f, 520f);
            window.FindDefaultSettings();
            window.Show();
        }

        public static void Open(RagnarokContentSettings initialSettings) {
            RagnarokContentDiagnosticsWindow window = GetWindow<RagnarokContentDiagnosticsWindow>("RO Content");
            window.minSize = new Vector2(620f, 520f);
            window.settings = initialSettings;
            window.Show();
        }

        private void OnEnable() {
            FindDefaultSettings();
        }

        private void OnDisable() {
            DisposeDatabase();
        }

        private void OnGUI() {
            EditorGUILayout.LabelField("Ragnarok Content Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            settings = (RagnarokContentSettings) EditorGUILayout.ObjectField("Content Settings", settings, typeof(RagnarokContentSettings), false);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Find Settings", GUILayout.Width(110f))) {
                    FindDefaultSettings(true);
                }

                using (new EditorGUI.DisabledScope(settings == null)) {
                    if (GUILayout.Button("Select RO Folder", GUILayout.Width(130f))) {
                        SelectContentFolder();
                    }
                }

                if (GUILayout.Button("Initialize / Reload", GUILayout.Width(140f))) {
                    InitializeDatabase();
                }

                using (new EditorGUI.DisabledScope(assets == null || !assets.IsInitialized)) {
                    if (GUILayout.Button("Dispose", GUILayout.Width(80f))) {
                        DisposeDatabase();
                        report = "Database disposed.";
                    }
                }
            }

            using (new EditorGUI.DisabledScope(assets == null || !assets.IsInitialized)) {
                EditorGUILayout.Space();
                DrawActions();
                EditorGUILayout.Space();
                DrawLookup();
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void DrawActions() {
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Summary")) {
                    report = assets.BuildSummary();
                }

                if (GUILayout.Button("Validate Baseline")) {
                    ValidateBaseline();
                }

                if (GUILayout.Button("Probe Common Types")) {
                    ProbeCommonTypes();
                }

                if (GUILayout.Button("Map Source Audit")) {
                    AuditMapSources();
                }

                if (GUILayout.Button("Korean Path Audit")) {
                    AuditKoreanPaths();
                }
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Clear Texture Cache")) {
                    assets.ClearTextureCache();
                    report = $"Texture cache cleared. Cache count: {assets.TextureCacheCount}";
                }

                if (GUILayout.Button("Clear Missing Diagnostics")) {
                    assets.ClearDiagnostics();
                    report = "Missing asset diagnostics cleared.";
                }

                if (GUILayout.Button("Print Missing Summary")) {
                    report = assets.Diagnostics.BuildMissingSummary();
                }

                if (GUILayout.Button("Write Manifest Cache")) {
                    WriteManifestCache();
                }

                if (GUILayout.Button("Clear Disk Cache")) {
                    ClearDiskCache();
                }
            }

            using (new EditorGUILayout.HorizontalScope()) {
                textureProbeLimit = EditorGUILayout.IntField("Texture Probe Limit", textureProbeLimit);
                textureProbeLimit = Mathf.Clamp(textureProbeLimit, 1, 1000);

                if (GUILayout.Button("Build Texture Cache Probe", GUILayout.Width(190f))) {
                    BuildTextureCacheProbe();
                }
            }
        }

        private void DrawLookup() {
            EditorGUILayout.LabelField("Manual Lookup", EditorStyles.boldLabel);
            lookupPath = EditorGUILayout.TextField("Path", lookupPath);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Exists")) {
                    LookupExists();
                }

                if (GUILayout.Button("Read Bytes")) {
                    LookupBytes();
                }

                if (GUILayout.Button("Load Texture")) {
                    LookupTexture();
                }

                if (GUILayout.Button("Path Candidates")) {
                    LookupPathCandidates();
                }

                if (GUILayout.Button("Source Conflicts")) {
                    LookupSourceConflicts();
                }
            }
        }

        private void InitializeDatabase() {
            if (settings == null) {
                report = "No RagnarokContentSettings selected.";
                return;
            }

            DisposeDatabase();
            assets = new RoAssetDatabase();

            try {
                assets.Initialize(settings.RootPath, settings.GrfFiles);
                report = assets.BuildSummary();
            } catch (Exception ex) {
                DisposeDatabase();
                report = ex.ToString();
                Debug.LogException(ex, settings);
            }
        }

        private void SelectContentFolder() {
            DisposeDatabase();
            if (RagnarokContentSettingsEditorUtility.SelectAndApplyContentFolder(settings, out string utilityReport)) {
                report = utilityReport + "\n\nClick Initialize / Reload to inspect this content.";
            } else {
                report = utilityReport;
            }
        }

        private void ValidateBaseline() {
            List<string> missing = assets.ValidateRequiredFiles(BaselineAssets);
            var builder = new StringBuilder();
            builder.AppendLine(assets.BuildSummary());
            builder.AppendLine();

            if (missing.Count == 0) {
                builder.AppendLine("Baseline validation passed.");
            } else {
                builder.AppendLine($"Baseline validation failed: {missing.Count} missing file(s).");
                for (int i = 0; i < missing.Count; i++) {
                    builder.AppendLine("- " + missing[i]);
                }
            }

            report = builder.ToString().TrimEnd();
        }

        private void ProbeCommonTypes() {
            var builder = new StringBuilder();
            builder.AppendLine("RO asset probe:");

            foreach (ProbeSpec probe in ProbeSpecs) {
                builder.Append("- ");
                builder.Append(probe.Label);
                builder.Append(" (*.");
                builder.Append(probe.Extension);
                builder.Append("): ");

                if (!assets.TryFindFirstFileWithExtension(probe.Extension, out string path)) {
                    builder.AppendLine("not found in manifest");
                    continue;
                }

                builder.Append(path);
                builder.Append(" -> ");
                builder.AppendLine(RunProbe(probe, path));
            }

            builder.AppendLine();
            builder.Append(assets.Diagnostics.BuildMissingSummary());
            report = builder.ToString().TrimEnd();
        }

        private void AuditMapSources() {
            string mapBaseName = lookupPath;
            if (string.IsNullOrWhiteSpace(mapBaseName)) {
                report = "Enter a map path or map name first, for example data/izlude.rsw.";
                return;
            }

            mapBaseName = mapBaseName.Replace('\\', '/').Trim();
            if (mapBaseName.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) {
                mapBaseName = mapBaseName.Substring("data/".Length);
            }

            int extensionStart = mapBaseName.LastIndexOf('.');
            if (extensionStart >= 0) {
                mapBaseName = mapBaseName.Substring(0, extensionStart);
            }

            var builder = new StringBuilder();
            builder.AppendLine("Map source audit:");
            builder.AppendLine("Map: " + mapBaseName);
            builder.AppendLine();

            AppendSourceAudit(builder, "data/" + mapBaseName + ".rsw");
            AppendSourceAudit(builder, "data/" + mapBaseName + ".gnd");
            AppendSourceAudit(builder, "data/" + mapBaseName + ".gat");

            report = builder.ToString().TrimEnd();
        }

        private void AppendSourceAudit(StringBuilder builder, string path) {
            List<RoAssetManifestEntry> entries = assets.FindSourceEntries(path);
            builder.AppendLine(path);
            if (entries.Count == 0) {
                builder.AppendLine("- missing");
                builder.AppendLine();
                return;
            }

            for (int i = 0; i < entries.Count; i++) {
                RoAssetManifestEntry entry = entries[i];
                builder.Append("- ");
                builder.Append(entry.SourceIndex);
                builder.Append(": ");
                builder.Append(entry.SourceName);
                builder.Append(" -> ");
                builder.Append(entry.Path);
                if (i == 0) {
                    builder.Append(" [resolved]");
                }

                builder.AppendLine();
            }

            builder.AppendLine();
        }

        private string RunProbe(ProbeSpec probe, string path) {
            try {
                switch (probe.Kind) {
                    case ProbeKind.Texture:
                        Texture2D texture = assets.LoadTexture(path);
                        return texture != null ? $"OK texture {texture.width}x{texture.height}" : "FAILED texture load";
                    case ProbeKind.Text:
                        string text = assets.ReadText(path);
                        return text != null ? $"OK text {text.Length} char(s)" : "FAILED text load";
                    case ProbeKind.RawBytes:
                        byte[] bytes = assets.ReadBytes(path);
                        return bytes != null ? $"OK {bytes.Length} byte(s)" : "FAILED byte read";
                    case ProbeKind.RawLoad:
                        byte[] raw = assets.Load<byte[]>(path);
                        return raw != null ? $"OK raw load {raw.Length} byte(s)" : "FAILED raw load";
                    case ProbeKind.TypedLoad:
                        return RunTypedLoad(path);
                    default:
                        return "unknown probe";
                }
            } catch (Exception ex) {
                return "EXCEPTION " + ex.Message;
            }
        }

        private string RunTypedLoad(string path) {
            string extension = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            switch (extension) {
                case "rsw":
                    return assets.Load<RSW>(path) != null ? "OK RSW" : "FAILED RSW";
                case "gnd":
                    return assets.Load<GND>(path) != null ? "OK GND" : "FAILED GND";
                case "gat":
                    return assets.Load<GAT>(path) != null ? "OK GAT" : "FAILED GAT";
                case "rsm":
                case "rsm2":
                    return assets.Load<RSM>(path) != null ? "OK RSM" : "FAILED RSM";
                case "wav":
                    return assets.Load<AudioClip>(path) != null ? "OK AudioClip" : "FAILED WAV";
                case "spr":
                    return assets.Load<SPR>(path) != null ? "OK SPR" : "FAILED SPR";
                case "act":
                    return assets.Load<ACT>(path) != null ? "OK ACT" : "FAILED ACT";
                case "str":
                    STR effect = assets.Load<STR>(path);
                    return effect != null ? $"OK STR layers={effect.layers?.Length ?? 0}" : "FAILED STR";
                default:
                    byte[] bytes = assets.ReadBytes(path);
                    return bytes != null ? $"OK raw {bytes.Length} byte(s)" : "FAILED raw";
            }
        }

        private void AuditKoreanPaths() {
            var builder = new StringBuilder();
            builder.AppendLine("Korean/CP949 path audit:");

            int legacyBytePathCount = 0;
            int legacyByteRoundTripCount = 0;
            int legacyByteFailedRoundTripCount = 0;
            int unicodeKoreanPathCount = 0;
            int unicodeLegacyFallbackCount = 0;
            int unicodeLegacyFallbackFailureCount = 0;
            int shown = 0;

            foreach (string path in assets.ListFiles()) {
                string display = RoAssetPath.TryDecodeLegacyBytesAsKorean(path);
                if (!string.IsNullOrEmpty(display) && !string.Equals(display, path, StringComparison.Ordinal)) {
                    legacyBytePathCount++;
                    IReadOnlyList<string> candidates = RoAssetPath.GetLookupCandidates(display);
                    bool roundTrip = ContainsCandidate(candidates, path);
                    if (roundTrip) {
                        legacyByteRoundTripCount++;
                    } else {
                        legacyByteFailedRoundTripCount++;
                    }

                    if (shown < 20) {
                        builder.Append("- legacy ");
                        builder.Append(roundTrip ? "OK" : "FAILED");
                        builder.Append(": ");
                        builder.Append(display);
                        builder.Append(" -> ");
                        builder.AppendLine(path);
                        shown++;
                    }

                    continue;
                }

                if (!ContainsHangul(path)) {
                    continue;
                }

                unicodeKoreanPathCount++;
                bool hasLegacyFallback = HasLegacyRoundTripCandidate(path);
                if (hasLegacyFallback) {
                    unicodeLegacyFallbackCount++;
                } else {
                    unicodeLegacyFallbackFailureCount++;
                }

                if (shown < 20) {
                    builder.Append("- unicode ");
                    builder.Append(hasLegacyFallback ? "OK" : "FAILED");
                    builder.Append(": ");
                    builder.AppendLine(path);
                    shown++;
                }
            }

            builder.AppendLine();
            builder.AppendLine($"Legacy-byte manifest paths: {legacyBytePathCount}");
            builder.AppendLine($"Legacy-byte round-trip candidates resolved: {legacyByteRoundTripCount}");
            builder.AppendLine($"Legacy-byte round-trip failures: {legacyByteFailedRoundTripCount}");
            builder.AppendLine($"Unicode Korean manifest paths: {unicodeKoreanPathCount}");
            builder.AppendLine($"Unicode -> CP949 fallback candidates resolved: {unicodeLegacyFallbackCount}");
            builder.AppendLine($"Unicode -> CP949 fallback failures: {unicodeLegacyFallbackFailureCount}");

            if (legacyBytePathCount == 0 && unicodeKoreanPathCount == 0) {
                builder.AppendLine();
                builder.AppendLine(BuildSyntheticKoreanPathReport());
            }

            report = builder.ToString().TrimEnd();
        }

        private void LookupExists() {
            bool exists = assets.Exists(lookupPath);
            if (exists && assets.TryGetManifestEntry(lookupPath, out RoAssetManifestEntry entry)) {
                report = $"Exists: {lookupPath}\nResolved: {entry.Path}\nSource: {entry.SourceName} [{entry.SourceIndex}]";
                return;
            }

            report = $"Missing: {lookupPath}";
        }

        private void LookupBytes() {
            byte[] bytes = assets.ReadBytes(lookupPath);
            report = bytes != null
                ? $"Read {bytes.Length} byte(s) from {lookupPath}"
                : $"Could not read bytes from {lookupPath}\n{assets.Diagnostics.BuildMissingSummary()}";
        }

        private void LookupTexture() {
            Texture2D texture = assets.LoadTexture(lookupPath);
            report = texture != null
                ? $"Loaded texture {texture.width}x{texture.height}: {lookupPath}"
                : $"Could not load texture {lookupPath}\n{assets.Diagnostics.BuildMissingSummary()}";
        }

        private void LookupPathCandidates() {
            try {
                var builder = new StringBuilder();
                builder.AppendLine(RoAssetPath.BuildLookupReport(lookupPath));

                if (assets.TryGetManifestEntry(lookupPath, out RoAssetManifestEntry entry)) {
                    builder.AppendLine();
                    builder.AppendLine("Manifest hit:");
                    builder.AppendLine("Path: " + entry.Path);
                    builder.AppendLine("Source: " + entry.SourceName + " [" + entry.SourceIndex + "]");

                    string display = RoAssetPath.TryDecodeLegacyBytesAsKorean(entry.Path);
                    if (!string.IsNullOrEmpty(display)) {
                        builder.AppendLine("CP949 display: " + display);
                    }
                } else {
                    builder.AppendLine();
                    builder.AppendLine("Manifest hit: no");
                }

                report = builder.ToString().TrimEnd();
            } catch (Exception ex) {
                report = ex.ToString();
                Debug.LogException(ex);
            }
        }

        private void LookupSourceConflicts() {
            try {
                List<RoAssetManifestEntry> entries = assets.FindSourceEntries(lookupPath);
                var builder = new StringBuilder();
                builder.AppendLine("Source lookup:");
                builder.AppendLine("Path: " + lookupPath);
                builder.AppendLine("Hits: " + entries.Count);

                if (entries.Count == 0) {
                    builder.AppendLine("No source contains this asset.");
                } else {
                    for (int i = 0; i < entries.Count; i++) {
                        RoAssetManifestEntry entry = entries[i];
                        builder.AppendLine($"- {entry.SourceIndex}: {entry.SourceName} -> {entry.Path}");
                    }

                    builder.AppendLine();
                    builder.AppendLine("Resolved source:");
                    builder.AppendLine($"- {entries[0].SourceIndex}: {entries[0].SourceName}");
                }

                report = builder.ToString().TrimEnd();
            } catch (Exception ex) {
                report = ex.ToString();
                Debug.LogException(ex);
            }
        }

        private void WriteManifestCache() {
            try {
                string path = assets.CreateCacheBuilder().WriteManifest();
                report = "Manifest cache written:\n" + path;
            } catch (Exception ex) {
                report = ex.ToString();
                Debug.LogException(ex);
            }
        }

        private void ClearDiskCache() {
            try {
                bool cleared = assets.ClearDiskCache();
                report = cleared ? "Disk cache cleared for current content profile." : "Disk cache was not cleared.";
            } catch (Exception ex) {
                report = ex.ToString();
                Debug.LogException(ex);
            }
        }

        private void BuildTextureCacheProbe() {
            try {
                RoAssetCacheBuildReport buildReport = assets.CreateCacheBuilder().BuildTextureProbe(textureProbeLimit, false);
                report = buildReport.BuildSummary();
            } catch (Exception ex) {
                report = ex.ToString();
                Debug.LogException(ex);
            }
        }

        private void FindDefaultSettings(bool forceReport = false) {
            if (settings != null) {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:RagnarokContentSettings");
            if (guids.Length == 0) {
                if (forceReport) {
                    report = "No RagnarokContentSettings asset found.";
                }

                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            settings = AssetDatabase.LoadAssetAtPath<RagnarokContentSettings>(path);
            if (forceReport) {
                report = settings != null ? $"Selected settings: {path}" : "Could not load settings asset.";
            }
        }

        private void DisposeDatabase() {
            assets?.Dispose();
            assets = null;
        }

        private static bool ContainsCandidate(IReadOnlyList<string> candidates, string expectedPath) {
            for (int i = 0; i < candidates.Count; i++) {
                if (string.Equals(candidates[i], expectedPath, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLegacyRoundTripCandidate(string decodedPath) {
            IReadOnlyList<string> candidates = RoAssetPath.GetLookupCandidates(decodedPath);
            for (int i = 0; i < candidates.Count; i++) {
                string candidate = candidates[i];
                if (string.Equals(candidate, decodedPath, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(candidate);
                if (!string.IsNullOrEmpty(decoded) &&
                    string.Equals(RoAssetPath.Normalize(decoded), RoAssetPath.Normalize(decodedPath), StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsHangul(string value) {
            if (string.IsNullOrEmpty(value)) {
                return false;
            }

            for (int i = 0; i < value.Length; i++) {
                char c = value[i];
                if ((c >= '\uac00' && c <= '\ud7af') ||
                    (c >= '\u1100' && c <= '\u11ff') ||
                    (c >= '\u3130' && c <= '\u318f')) {
                    return true;
                }
            }

            return false;
        }

        private static string BuildSyntheticKoreanPathReport() {
            const string sample = "data/texture/\uc720\uc800\uc778\ud130\ud398\uc774\uc2a4/sample.bmp";
            IReadOnlyList<string> candidates = RoAssetPath.GetLookupCandidates(sample);
            if (candidates.Count < 2) {
                return "Synthetic CP949 fallback: FAILED, no legacy-byte candidate was generated.";
            }

            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(candidates[1]);
            if (string.IsNullOrEmpty(decoded)) {
                return "Synthetic CP949 fallback: FAILED, generated legacy-byte candidate could not be decoded.";
            }

            bool ok = string.Equals(RoAssetPath.Normalize(sample), RoAssetPath.Normalize(decoded), StringComparison.Ordinal);
            return ok
                ? "Synthetic CP949 fallback: OK, wide Korean path round-trips to a legacy-byte candidate."
                : "Synthetic CP949 fallback: FAILED, generated legacy-byte candidate did not round-trip.";
        }

        private readonly struct ProbeSpec {
            public ProbeSpec(string label, string extension, ProbeKind kind) {
                Label = label;
                Extension = extension;
                Kind = kind;
            }

            public string Label { get; }
            public string Extension { get; }
            public ProbeKind Kind { get; }
        }

        private enum ProbeKind {
            RawBytes,
            RawLoad,
            Text,
            Texture,
            TypedLoad
        }
    }
}
