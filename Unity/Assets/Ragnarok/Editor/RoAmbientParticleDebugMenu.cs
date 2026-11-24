using Ragnarok.Diagnostics;
using UnityEditor;

namespace Ragnarok.Editor {
    /// <summary>
    /// Top-bar entry point for the ambient particle tuning overlay. Most useful in Play Mode;
    /// toggle visibility in play with Ctrl+Alt+Shift+F12.
    /// </summary>
    internal static class RoAmbientParticleDebugMenu {
        private const string ToggleMenu = "Ragnarok/Diagnostics/Ambient Particles Debug";

        [MenuItem(ToggleMenu, false, 203)]
        private static void Toggle() {
            RoAmbientParticleDebugOverlay.Toggle();
            Menu.SetChecked(ToggleMenu, RoAmbientParticleDebugOverlay.IsActive);
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ToggleValidate() {
            Menu.SetChecked(ToggleMenu, RoAmbientParticleDebugOverlay.IsActive);
            return true;
        }
    }
}
