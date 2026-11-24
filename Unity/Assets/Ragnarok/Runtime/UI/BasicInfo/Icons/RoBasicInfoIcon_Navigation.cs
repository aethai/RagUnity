using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #9 ("navigation"). Opens the classic navigation window.</summary>
    public sealed class RoBasicInfoIcon_Navigation : IRoBasicInfoIcon {
        public string Id => "navigation";
        public KeyCode? Hotkey => null;
        public EventModifiers HotkeyModifiers => EventModifiers.None;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => true;
        public void Toggle(in RoBasicInfoIconContext ctx) {
            RoClassicNavigation.Toggle();
        }
    }
}
