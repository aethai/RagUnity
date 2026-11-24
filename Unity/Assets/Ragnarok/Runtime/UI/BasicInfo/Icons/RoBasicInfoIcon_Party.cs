using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #5 ("party"). Opens the Party / Friend window on its Party tab.</summary>
    public sealed class RoBasicInfoIcon_Party : IRoBasicInfoIcon {
        public string Id => "party";
        public KeyCode? Hotkey => KeyCode.Z;
        public EventModifiers HotkeyModifiers => EventModifiers.Alt;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (IsAvailable(ctx)) RoClassicPartyFriends.Toggle(ctx.Session, RoPartyFriendsTab.Party); // UIMessengerGroupWnd 0x22
        }
    }
}
