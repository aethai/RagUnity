using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #4 ("skill"). Opens the skill window.</summary>
    public sealed class RoBasicInfoIcon_Skill : IRoBasicInfoIcon, IRoBasicInfoNotificationIcon {
        public string Id => "skill";
        public KeyCode? Hotkey => KeyCode.S;
        public EventModifiers HotkeyModifiers => EventModifiers.Alt;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;
        public bool HasNewNotification(in RoBasicInfoIconContext ctx) {
            var status = ctx.Session != null
                ? ctx.Session.BasicInfoSnapshot().Status
                : null;
            return status != null && status.SkillPoints > 0;
        }

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (IsAvailable(ctx)) RoClassicSkillTree.Toggle(ctx.Session); // UINewSkillListWnd 0x25
        }
    }
}
