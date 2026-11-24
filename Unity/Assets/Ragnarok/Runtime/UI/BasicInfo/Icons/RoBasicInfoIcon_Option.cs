using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #10 ("option"). Opens the classic in-game Game Options menu.</summary>
    public sealed class RoBasicInfoIcon_Option : IRoBasicInfoIcon {
        public string Id => "option";
        public KeyCode? Hotkey => null;
        public EventModifiers HotkeyModifiers => EventModifiers.None;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (!IsAvailable(ctx)) {
                return;
            }

            ctx.Host.ToggleGameOptions();
        }
    }
}
