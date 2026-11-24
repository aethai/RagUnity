using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #12 ("mail"). Opens Rodex.</summary>
    public sealed class RoBasicInfoIcon_Mail : IRoBasicInfoIcon, IRoBasicInfoNotificationIcon {
        public string Id => "mail";
        public KeyCode? Hotkey => null;
        public EventModifiers HotkeyModifiers => EventModifiers.None;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;
        public bool HasNewNotification(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Session.MailSnapshot().HasUnreadRodexMail == true;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (!IsAvailable(ctx)) {
                return;
            }

            // the classic UI: MakeWindow(0x107) / DeleteWindow(0x107)
            RoClassicRodex.ToggleInbox();
        }
    }
}
