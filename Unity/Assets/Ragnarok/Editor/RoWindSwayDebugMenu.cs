using Ragnarok.Diagnostics;
using UnityEditor;

namespace Ragnarok.Editor {
    /// <summary>
    /// Top-bar entry point for the RSM wind-sway tuning overlay. Most useful in Play Mode;
    /// toggle visibility in play with Ctrl+Alt+Shift+F11.
    /// </summary>
    internal static class RoWindSwayDebugMenu {
        private const string ToggleMenu = "Ragnarok/Diagnostics/Wind Sway Debug";

        [MenuItem(ToggleMenu, false, 201)]
        private static void Toggle() {
            RoWindSwayDebugOverlay.Toggle();
            Menu.SetChecked(ToggleMenu, RoWindSwayDebugOverlay.IsActive);
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ToggleValidate() {
            Menu.SetChecked(ToggleMenu, RoWindSwayDebugOverlay.IsActive);
            return true;
        }
    }
}
