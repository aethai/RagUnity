using Ragnarok.Diagnostics;
using UnityEditor;

namespace Ragnarok.Editor {
    /// <summary>
    /// Top-bar entry point for the RO map model picker overlay. Most useful in Play Mode;
    /// toggle visibility in play with Ctrl+Alt+Shift+F12.
    /// </summary>
    internal static class RoMapModelPickerDebugMenu {
        private const string ToggleMenu = "Ragnarok/Diagnostics/Map Model Picker";

        [MenuItem(ToggleMenu, false, 202)]
        private static void Toggle() {
            RoMapModelPickerOverlay.Toggle();
            Menu.SetChecked(ToggleMenu, RoMapModelPickerOverlay.IsActive);
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ToggleValidate() {
            Menu.SetChecked(ToggleMenu, RoMapModelPickerOverlay.IsActive);
            return true;
        }
    }
}
