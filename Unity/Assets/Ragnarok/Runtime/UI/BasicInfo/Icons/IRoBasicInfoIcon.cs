using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// One action of the Basic Info menu (RoClassicBasicInfo's UIMenuIconWnd icons) and of its Alt / Ctrl hotkeys: the
    /// window it toggles, its default key and its availability. The classic menu draws the icons itself.
    /// </summary>
    /// <remarks>
    /// Implementations should be stateless singletons registered in
    /// <see cref="RoBasicInfoIconRegistry"/>.
    /// </remarks>
    public interface IRoBasicInfoIcon {
        /// <summary>
        /// Stable identifier used by the registry and tests. Lowercase, no
        /// spaces. Mirrors the roBrowserLegacy V4 button id (typo
        /// <c>"achievment"</c> is preserved on purpose for parity).
        /// </summary>
        string Id { get; }

        /// <summary>Keyboard shortcut key for this icon, or null when no shortcut is exposed.</summary>
        KeyCode? Hotkey { get; }

        /// <summary>Modifier keys required by <see cref="Hotkey"/>.</summary>
        EventModifiers HotkeyModifiers { get; }

        /// <summary>
        /// Returns true when this icon should respond to clicks under the
        /// current session/world state. Gated icons can use this for feature,
        /// packetver, or server-state availability checks.
        /// </summary>
        bool IsAvailable(in RoBasicInfoIconContext ctx);

        /// <summary>
        /// Open, close, or focus the icon's associated window. Implementations
        /// own their window lifecycle and use <see cref="RoBasicInfoIconContext.Host"/>
        /// only for shell-owned bookkeeping such as refresh sequencing and layout.
        /// </summary>
        void Toggle(in RoBasicInfoIconContext ctx);
    }

    public interface IRoBasicInfoNotificationIcon {
        /// <summary>True while the menu shows the icon's "new" variant (the client's alarm list).</summary>
        bool HasNewNotification(in RoBasicInfoIconContext ctx);
    }
}
