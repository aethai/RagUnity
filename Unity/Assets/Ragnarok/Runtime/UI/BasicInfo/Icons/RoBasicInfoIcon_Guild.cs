using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #6 ("guild"). Owns the open / close lifecycle of <see cref="RoGuildWindow"/>.</summary>
    public sealed class RoBasicInfoIcon_Guild : IRoBasicInfoIcon {
        public string Id => "guild";
        public KeyCode? Hotkey => KeyCode.G;
        public EventModifiers HotkeyModifiers => EventModifiers.Alt;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (!IsAvailable(ctx)) {
                return;
            }

            // the classic UI: MakeWindow(0x3b), the guild manager's saved tab
            RoClassicGuild.Toggle();
        }
    }
}
