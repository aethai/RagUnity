using System;
using System.Threading.Tasks;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>roBrowserLegacy V4 button #11 ("bank"). Opens banking through the server-gated flow.</summary>
    public sealed class RoBasicInfoIcon_Bank : IRoBasicInfoIcon {
        public string Id => "bank";
        public KeyCode? Hotkey => KeyCode.B;
        public EventModifiers HotkeyModifiers => EventModifiers.Control;
        public bool IsAvailable(in RoBasicInfoIconContext ctx) => ctx.Session != null && ctx.Host != null;

        public void Toggle(in RoBasicInfoIconContext ctx) {
            if (!IsAvailable(ctx)) {
                return;
            }

            // UIBank_NewWnd 0x113: CZ_REQ_BANKING_CHECK, the answer opens it (RoClassicBank)
            RoClassicBank.Toggle(ctx.Session);
        }
    }
}
