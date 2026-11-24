using Ragnarok.Network;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Ragnarok.UI {
    /// <summary>
    /// The client's side of the held GC_ROLLINGCUTTER (<see cref="RoHeldSkillUse"/>, docs/audits/chat-options-2026-10-04.md
    /// §6): the key presses and releases WndProc hands on (0x9d60a0, WM_KEYUP 0xd45bcb), the focus loss (0xd44890), the
    /// mouse uses of a slot and the hotkey window opening, each sending CZ_USE_SKILL_END when a held start ends.
    /// </summary>
    internal static class RoClassicHeldSkill {
        private static RoLoginShellSession CurrentSession => RoClassicGameUi.Instance?.Shell?.ClassicSession;

        /// <summary>
        /// The keys pressed this frame, before their own uses (RoClassicGameUi): another key than the one holding the
        /// skill ends it, and the key is the one a start made now is held by. Keys are pressed once (the client skips the
        /// auto-repeats, bit 30 of lParam).
        /// </summary>
        public static void KeyDowns(Keyboard keyboard, RoLoginShellSession session) {
            if (keyboard == null || (!keyboard.anyKey.isPressed && !keyboard.anyKey.wasReleasedThisFrame)) return;
            var keys = keyboard.allKeys;
            for (int i = 0; i < keys.Count; i++) {
                KeyControl key = keys[i];
                if (key != null && key.wasPressedThisFrame && RoHeldSkillUse.OnKeyDown((int) key.keyCode)) session?.TrySendUseSkillEnd();
            }
        }

        /// <summary>The keys released this frame, after the uses (RoClassicGameUi): any WM_KEYUP ends a held start.</summary>
        public static void KeyUps(Keyboard keyboard, RoLoginShellSession session) {
            if (keyboard == null || !RoHeldSkillUse.IsHeld) return;
            var keys = keyboard.allKeys;
            for (int i = 0; i < keys.Count; i++) {
                KeyControl key = keys[i];
                if (key == null || !key.wasReleasedThisFrame) continue;
                if (RoHeldSkillUse.Release()) session?.TrySendUseSkillEnd();
                return;
            }
        }

        /// <summary>WM_ACTIVATE with the window losing the focus (0xd44890).</summary>
        public static void FocusLost() {
            if (RoHeldSkillUse.ReleaseOnFocusLost()) CurrentSession?.TrySendUseSkillEnd();
        }

        /// <summary>
        /// Before a mouse use of a slot (/q1, /q2, a double click on the shortcut bar or the quick slots): the skill in it
        /// is a one-shot use, any other slot ends a held start.
        /// </summary>
        public static void MouseUse(RoShortcutKeyEntry slot, RoLoginShellSession session) {
            int skillId = slot.IsSkill ? unchecked((int) slot.Id) : 0;
            if (RoHeldSkillUse.OnMouseUse(skillId)) (session ?? CurrentSession)?.TrySendUseSkillEnd();
        }

        /// <summary>0xd284e0 from a window (UIHotKeyWnd 0x881260 when it opens).</summary>
        public static void Release() {
            if (RoHeldSkillUse.Release()) CurrentSession?.TrySendUseSkillEnd();
        }
    }
}
