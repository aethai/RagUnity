using UnityEngine;

namespace Ragnarok.UI {
    public static class RoUiScaleStore {
        public const int CurrentVersion = 1;
        public const string VersionPrefsKey = "Ragnarok.ClientSettings.UiScale.Version";
        public const string ModePrefsKey = "Ragnarok.ClientSettings.UiScale.Mode";
        public const string ManualPrefsKey = "Ragnarok.ClientSettings.UiScale.Manual";

        public static void LoadInto(RoClientSettingsModel settings) {
            if (settings == null) {
                return;
            }

            int mode = PlayerPrefs.GetInt(ModePrefsKey, (int) RoUiScaleMode.Auto);
            if (mode != (int) RoUiScaleMode.Manual) {
                settings.ResetUiScaleToAuto();
                return;
            }

            float manualScale = PlayerPrefs.GetFloat(ManualPrefsKey, 1f);
            RoClientSettingsResult result = settings.SetManualUiScale(manualScale);
            if (!result.Applied) {
                settings.ResetUiScaleToAuto();
            }
        }

        public static void Save(RoClientSettingsModel settings) {
            if (settings == null) {
                return;
            }

            Save(settings.Snapshot());
        }

        public static void Save(RoClientSettingsSnapshot snapshot) {
            PlayerPrefs.SetInt(VersionPrefsKey, CurrentVersion);

            if (snapshot.UiScaleMode == RoUiScaleMode.Manual) {
                PlayerPrefs.SetInt(ModePrefsKey, (int) RoUiScaleMode.Manual);
                PlayerPrefs.SetFloat(ManualPrefsKey, snapshot.ManualUiScale);
            } else {
                PlayerPrefs.SetInt(ModePrefsKey, (int) RoUiScaleMode.Auto);
                PlayerPrefs.DeleteKey(ManualPrefsKey);
            }

            PlayerPrefs.Save();
        }

        public static void SaveManual(float scale) {
            var settings = new RoClientSettingsModel();
            settings.SetManualUiScale(scale);
            Save(settings);
        }

        public static void ResetToAuto() {
            PlayerPrefs.SetInt(VersionPrefsKey, CurrentVersion);
            PlayerPrefs.SetInt(ModePrefsKey, (int) RoUiScaleMode.Auto);
            PlayerPrefs.DeleteKey(ManualPrefsKey);
            PlayerPrefs.Save();
        }

        public static void DeleteAll() {
            PlayerPrefs.DeleteKey(VersionPrefsKey);
            PlayerPrefs.DeleteKey(ModePrefsKey);
            PlayerPrefs.DeleteKey(ManualPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
