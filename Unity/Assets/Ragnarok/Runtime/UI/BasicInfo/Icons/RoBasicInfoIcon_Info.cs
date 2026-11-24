using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #1 ("info"). Opens the character status window.</summary>
    public sealed class RoBasicInfoIcon_Info : IRoBasicInfoIcon, IRoBasicInfoNotificationIcon {
        public string Id => "info";
        public KeyCode? Hotkey => KeyCode.A;
        public EventModifiers HotkeyModifiers => EventModifiers.Alt;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;
        public bool HasNewNotification(in RoBasicInfoIconContext ctx) {
            var status = ctx.Session != null
                ? ctx.Session.BasicInfoSnapshot().Status
                : null;
            return RoClassicStatus.HasAffordableBaseStatIncrease(status);
        }

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (IsAvailable(ctx)) RoClassicStatus.Toggle(ctx.Session); // UIStatusWnd 0xb
        }
    }
}
