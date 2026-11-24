using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #3 ("item"). Opens the inventory window.</summary>
    public sealed class RoBasicInfoIcon_Item : IRoBasicInfoIcon {
        public string Id => "item";
        public KeyCode? Hotkey => KeyCode.E;
        public EventModifiers HotkeyModifiers => EventModifiers.Alt;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (IsAvailable(ctx)) RoClassicInventory.Toggle(ctx.Session); // UIItemWnd 0x8
        }
    }
}
