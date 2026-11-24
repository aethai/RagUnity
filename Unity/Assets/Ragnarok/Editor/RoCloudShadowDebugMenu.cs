using Ragnarok.Diagnostics;
using UnityEditor;

namespace Ragnarok.Editor {
    internal static class RoCloudShadowDebugMenu {
        private const string ToggleMenu = "Ragnarok/Diagnostics/Cloud Shadow Debug";

        [MenuItem(ToggleMenu, false, 201)]
        private static void Toggle() {
            RoCloudShadowDebugOverlay.Toggle();
            Menu.SetChecked(ToggleMenu, RoCloudShadowDebugOverlay.IsActive);
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ToggleValidate() {
            Menu.SetChecked(ToggleMenu, RoCloudShadowDebugOverlay.IsActive);
            return true;
        }
    }
}
