using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #14 ("attendance"). Opens the attendance reward window.</summary>
    public sealed class RoBasicInfoIcon_Attendance : IRoBasicInfoIcon, IRoBasicInfoNotificationIcon {
        public string Id => "attendance";
        public KeyCode? Hotkey => null;
        public EventModifiers HotkeyModifiers => EventModifiers.None;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;
        public bool HasNewNotification(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Session.AttendanceSnapshot().IsRewardAvailable;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (!IsAvailable(ctx)) {
                return;
            }

            // UICheckAttendanceWnd 0x148: CZ_OPEN_UI, the server's ZC_UI_OPEN makes it (RoClassicAttendance)
            RoClassicAttendance.Toggle(ctx.Session);
        }
    }
}
