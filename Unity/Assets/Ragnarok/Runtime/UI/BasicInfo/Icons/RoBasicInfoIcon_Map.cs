using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #8 ("map"). Opens the fullscreen World Map.</summary>
    public sealed class RoBasicInfoIcon_Map : IRoBasicInfoIcon {
        public string Id => "map";
        public KeyCode? Hotkey => KeyCode.Quote;
        public EventModifiers HotkeyModifiers => EventModifiers.Control;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => true;
        public void Toggle(in RoBasicInfoIconContext ctx) {
            RoClassicWorldMap.Toggle(ctx.Session);
        }
    }
}
