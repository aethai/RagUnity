using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ragnarok.Content {
    public static class RagnarokContentProfileStore {
        public const string ProfilePathEnvironmentVariable = "RO_CONTENT_PROFILE_PATH";
        public const string TestProfilePathEnvironmentVariable = "RO_TEST_PROFILE_PATH";
        public const string ContentRootEnvironmentVariable = "RO_CONTENT_ROOT";
        public const string GrfFilesEnvironmentVariable = "RO_GRF_FILES";
        public const string LocalSettingsAssetPath = "Assets/LocalRagnarokContent.asset";

        private const string ProfileFileName = "RagnarokContentProfile.json";

        public static string DefaultProfilePath => Path.Combine(Application.persistentDataPath, ProfileFileName);

        public static string ResolveProfilePath() {
            string profilePath = Environment.GetEnvironmentVariable(ProfilePathEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(profilePath)) {
                return Path.GetFullPath(profilePath);
            }

            profilePath = Environment.GetEnvironmentVariable(TestProfilePathEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(profilePath)) {
                return Path.GetFullPath(profilePath);
            }

            return DefaultProfilePath;
        }

        public static bool TryLoad(out RagnarokContentProfile profile, out string report) {
            if (TryLoadFromEnvironment(out profile, out report)) {
                return true;
            }

            // A standalone client is portable: its default content travels with the executable,
            // rather than using an editor profile left in the current user's AppData.
            bool hasExplicitProfilePath =
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProfilePathEnvironmentVariable)) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TestProfilePathEnvironmentVariable));
            if (!Application.isEditor && !hasExplicitProfilePath) {
                profile = CreatePlayerProfile(Application.dataPath);
                report = $"Using RO content from '{profile.RootPath}'.";
                return true;
            }

            string profilePath = ResolveProfilePath();
            if (!File.Exists(profilePath)) {
                profile = null;
                report = $"No saved RO content profile found at '{profilePath}'.";
                return false;
            }

            try {
                profile = JsonUtility.FromJson<RagnarokContentProfile>(File.ReadAllText(profilePath));
                if (profile == null) {
                    report = $"Could not parse RO content profile '{profilePath}'.";
                    return false;
                }

                report = $"Loaded RO content profile '{profile.Name}' from '{profilePath}'.";
                return true;
            } catch (Exception ex) {
                profile = null;
                report = $"Could not load RO content profile '{profilePath}': {ex.Message}";
                return false;
            }
        }

        private static RagnarokContentProfile CreatePlayerProfile(string applicationDataPath) {
            // Windows/Linux dataPath is <installation>/<PlayerName>_Data, regardless of the
            // working directory chosen by a launcher or shortcut.
            string installation = Path.GetFullPath(Path.Combine(applicationDataPath, ".."));
            string rootPath = Path.Combine(installation, "ROData");
            // without ROData, a build unpacked into a folder of the original client reads that client's GRFs (kept
            // up to date by the server's own patcher)
            string parent = Path.GetDirectoryName(installation);
            if (!Directory.Exists(rootPath) && parent != null && File.Exists(Path.Combine(parent, "data.grf"))) {
                rootPath = parent;
            }

            return new RagnarokContentProfile("ROData", rootPath, new[] { "data.grf" });
        }

        public static bool TryLoadLocalSettings(out RagnarokContentSettings settings, out string report) {
#if UNITY_EDITOR
            settings = AssetDatabase.LoadAssetAtPath<RagnarokContentSettings>(LocalSettingsAssetPath);
            if (IsUsable(settings)) {
                report = $"Loaded RO content settings from '{LocalSettingsAssetPath}'.";
                return true;
            }
#else
            settings = null;
#endif

            report = $"No usable RO content settings found at '{LocalSettingsAssetPath}'.";
            return false;
        }

        public static void Save(RagnarokContentProfile profile, string path = null) {
            if (profile == null) {
                throw new ArgumentNullException(nameof(profile));
            }

            string profilePath = string.IsNullOrWhiteSpace(path) ? ResolveSaveProfilePath() : Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(profilePath);
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(profilePath, JsonUtility.ToJson(profile, true));
        }

        public static string ResolveSaveProfilePath() {
            string profilePath = Environment.GetEnvironmentVariable(ProfilePathEnvironmentVariable);
            return string.IsNullOrWhiteSpace(profilePath) ? DefaultProfilePath : Path.GetFullPath(profilePath);
        }

        private static bool TryLoadFromEnvironment(out RagnarokContentProfile profile, out string report) {
            string rootPath = Environment.GetEnvironmentVariable(ContentRootEnvironmentVariable);
            string grfFiles = Environment.GetEnvironmentVariable(GrfFilesEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(rootPath) && string.IsNullOrWhiteSpace(grfFiles)) {
                profile = null;
                report = "";
                return false;
            }

            if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(grfFiles)) {
                profile = null;
                report = $"Set both {ContentRootEnvironmentVariable} and {GrfFilesEnvironmentVariable}.";
                return false;
            }

            profile = new RagnarokContentProfile("environment", rootPath, SplitGrfFiles(grfFiles));
            report = $"Loaded RO content profile from {ContentRootEnvironmentVariable}/{GrfFilesEnvironmentVariable}.";
            return true;
        }

        private static string[] SplitGrfFiles(string grfFiles) {
            return grfFiles.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool IsUsable(RagnarokContentSettings settings) {
            return settings != null &&
                !string.IsNullOrWhiteSpace(settings.RootPath) &&
                settings.GrfFiles != null &&
                settings.GrfFiles.Count > 0;
        }
    }
}
