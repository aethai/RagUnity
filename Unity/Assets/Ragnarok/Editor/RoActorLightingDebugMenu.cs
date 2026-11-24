using Ragnarok.Diagnostics;
using UnityEditor;

namespace Ragnarok.Editor {
    /// <summary>
    /// Top-bar entry point for the actor lighting debug overlay. Spawns/removes a read-only on-screen
    /// breakdown of how each actor sprite is being lit (map-light multiply, shader policy flags, and the
    /// legacy baked-tint alarm). Most useful in Play Mode; toggle visibility in play with Ctrl+Alt+Shift+F9.
    /// </summary>
    internal static class RoActorLightingDebugMenu {
        private const string ToggleMenu = "Ragnarok/Diagnostics/Actor Lighting Debug";

        [MenuItem(ToggleMenu, false, 200)]
        private static void Toggle() {
            RoActorLightingDebugOverlay.Toggle();
            Menu.SetChecked(ToggleMenu, RoActorLightingDebugOverlay.IsActive);
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ToggleValidate() {
            Menu.SetChecked(ToggleMenu, RoActorLightingDebugOverlay.IsActive);
            return true;
        }
    }
}
