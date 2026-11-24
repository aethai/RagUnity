using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #2 ("equip"). Opens the equipment window.</summary>
    public sealed class RoBasicInfoIcon_Equip : IRoBasicInfoIcon {
        public string Id => "equip";
        public KeyCode? Hotkey => KeyCode.Q;
        public EventModifiers HotkeyModifiers => EventModifiers.Alt;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (IsAvailable(ctx)) RoClassicEquipment.Toggle(ctx.Session); // UIEquipWnd 0xa
        }
    }
}
