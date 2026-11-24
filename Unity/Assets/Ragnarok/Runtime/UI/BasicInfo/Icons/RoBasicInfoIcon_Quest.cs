using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #7 ("quest"). Owns the open / close lifecycle of <see cref="RoQuestWindow"/>.</summary>
    public sealed class RoBasicInfoIcon_Quest : IRoBasicInfoIcon {
        public string Id => "quest";
        public KeyCode? Hotkey => KeyCode.U;
        public EventModifiers HotkeyModifiers => EventModifiers.Alt;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (!IsAvailable(ctx)) {
                return;
            }

            // the classic UI: MakeWindow(0x2718) / DeleteWindow(0x2718)
            RoClassicQuest.Toggle();
        }
    }
}
