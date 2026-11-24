using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The general UI scale of the game settings (the scale the classic UI draws at): a settings model shared with the
    /// Game Settings window, the saved scale (RoUiScaleStore) loaded on first use, and in Auto mode the scale computed
    /// from the screen (RoUiScaleCalculator), recomputed when the screen size changes.
    /// </summary>
    public static class RoClassicUiScale {
        private static RoClientSettingsModel settings;
        private static int autoWidth = -1, autoHeight = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            settings = null;
            autoWidth = autoHeight = -1;
        }

        public static RoClientSettingsModel Settings {
            get {
                if (settings == null) Bind(new RoClientSettingsModel(), loadPersistentSettings: true);
                return settings;
            }
        }

        public static RoUiScaleMode Mode => Settings.UiScaleMode;

        /// <summary>The effective scale, the Auto one following the screen size.</summary>
        public static float EffectiveScale {
            get {
                RoClientSettingsModel model = Settings;
                if (model.UiScaleMode == RoUiScaleMode.Auto && (Screen.width != autoWidth || Screen.height != autoHeight)) RefreshAutoScale();
                return model.EffectiveUiScale;
            }
        }

        /// <summary>Makes <paramref name="model"/> the one the scale follows (the Game Settings window's); optionally loads the saved scale into it.</summary>
        public static void Bind(RoClientSettingsModel model, bool loadPersistentSettings) {
            if (model == null) return;
            settings = model;
            if (loadPersistentSettings) RoUiScaleStore.LoadInto(model);
            autoWidth = autoHeight = -1;
            if (model.UiScaleMode == RoUiScaleMode.Auto) RefreshAutoScale();
        }

        public static RoClientSettingsResult SetManualScale(float scale) {
            RoClientSettingsResult result = Settings.SetManualUiScale(scale);
            if (result.Applied) RoUiScaleStore.Save(settings);
            return result;
        }

        public static void ResetToAuto() {
            Settings.ResetUiScaleToAuto();
            RoUiScaleStore.ResetToAuto();
            RefreshAutoScale();
        }

        private static void RefreshAutoScale() {
            autoWidth = Screen.width;
            autoHeight = Screen.height;
            float autoScale = RoUiScaleCalculator.CalculateAutoScale(
                Screen.width, Screen.height, Screen.dpi, RoUiScaleCalculator.ReferenceScale, RoClientSettingsModel.MaxUiScale);
            settings.SetAutoUiScale(autoScale);
        }
    }
}
