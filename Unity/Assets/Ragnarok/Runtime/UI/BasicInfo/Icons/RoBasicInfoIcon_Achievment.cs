using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #13 ("achievment"; typo preserved for parity). Opens an achievement placeholder.</summary>
    public sealed class RoBasicInfoIcon_Achievment : IRoBasicInfoIcon, IRoBasicInfoNotificationIcon {
        public string Id => "achievment";
        public KeyCode? Hotkey => null;
        public EventModifiers HotkeyModifiers => EventModifiers.None;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => true;

        /// <summary>The "new" badge: on after a completion (ZC_ACH_UPDATE), off when the window opens.</summary>
        public bool HasNewNotification(in RoBasicInfoIconContext ctx) => RoClassicAchievements.HasNewBadge;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            RoClassicAchievements.Toggle(ctx.Session);
        }
    }
}
