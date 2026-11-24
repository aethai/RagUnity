using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Ragnarok.UI {
    public interface IRoHotkeyCommandExecutor {
        Task UseHotkeyAsync(int hotkeyIndex);
        Task SetHotkeyAsync(int hotkeyIndex, RoShortcutKeyEntry shortcut);
    }

    public interface IRoKeyboardInputState {
        bool IsKeyHeld(KeyCode key);
        bool IsKeyPressedThisFrame(KeyCode key);
    }

    public sealed class RoUnityKeyboardInputState : IRoKeyboardInputState {
        public bool IsKeyHeld(KeyCode key) {
            KeyControl control = GetKeyControl(key);
            return control != null && control.isPressed;
        }

        public bool IsKeyPressedThisFrame(KeyCode key) {
            KeyControl control = GetKeyControl(key);
            return control != null && control.wasPressedThisFrame;
        }

        public static KeyControl GetKeyControl(KeyCode key) {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) {
                return null;
            }

            switch (key) {
                case KeyCode.A: return keyboard.aKey;
                case KeyCode.B: return keyboard.bKey;
                case KeyCode.C: return keyboard.cKey;
                case KeyCode.D: return keyboard.dKey;
                case KeyCode.E: return keyboard.eKey;
                case KeyCode.F: return keyboard.fKey;
                case KeyCode.G: return keyboard.gKey;
                case KeyCode.H: return keyboard.hKey;
                case KeyCode.I: return keyboard.iKey;
                case KeyCode.J: return keyboard.jKey;
                case KeyCode.K: return keyboard.kKey;
                case KeyCode.L: return keyboard.lKey;
                case KeyCode.M: return keyboard.mKey;
                case KeyCode.N: return keyboard.nKey;
                case KeyCode.O: return keyboard.oKey;
                case KeyCode.P: return keyboard.pKey;
                case KeyCode.Q: return keyboard.qKey;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.S: return keyboard.sKey;
                case KeyCode.T: return keyboard.tKey;
                case KeyCode.U: return keyboard.uKey;
                case KeyCode.V: return keyboard.vKey;
                case KeyCode.W: return keyboard.wKey;
                case KeyCode.X: return keyboard.xKey;
                case KeyCode.Y: return keyboard.yKey;
                case KeyCode.Z: return keyboard.zKey;
                case KeyCode.Escape:
                    return keyboard.escapeKey;
                case KeyCode.Quote:
                    return keyboard.quoteKey;
                case KeyCode.BackQuote:
                    return keyboard.backquoteKey;
                case KeyCode.Tab:
                    return keyboard.tabKey;
                case KeyCode.Return:
                    return keyboard.enterKey;
                case KeyCode.Insert:
                    return keyboard.insertKey;
                case KeyCode.Home:
                    return keyboard.homeKey;
                case KeyCode.End:
                    return keyboard.endKey;
                case KeyCode.Delete:
                    return keyboard.deleteKey;
                case KeyCode.Backspace:
                    return keyboard.backspaceKey;
                case KeyCode.Alpha0:
                    return keyboard.digit0Key;
                case KeyCode.Alpha1:
                    return keyboard.digit1Key;
                case KeyCode.Alpha2:
                    return keyboard.digit2Key;
                case KeyCode.Alpha3:
                    return keyboard.digit3Key;
                case KeyCode.Alpha4:
                    return keyboard.digit4Key;
                case KeyCode.Alpha5:
                    return keyboard.digit5Key;
                case KeyCode.Alpha6:
                    return keyboard.digit6Key;
                case KeyCode.Alpha7:
                    return keyboard.digit7Key;
                case KeyCode.Alpha8:
                    return keyboard.digit8Key;
                case KeyCode.Alpha9:
                    return keyboard.digit9Key;
                case KeyCode.F1:
                    return keyboard.f1Key;
                case KeyCode.F2:
                    return keyboard.f2Key;
                case KeyCode.F3:
                    return keyboard.f3Key;
                case KeyCode.F4:
                    return keyboard.f4Key;
                case KeyCode.F5:
                    return keyboard.f5Key;
                case KeyCode.F6:
                    return keyboard.f6Key;
                case KeyCode.F7:
                    return keyboard.f7Key;
                case KeyCode.F8:
                    return keyboard.f8Key;
                case KeyCode.F9:
                    return keyboard.f9Key;
                case KeyCode.F10:
                    return keyboard.f10Key;
                case KeyCode.F11:
                    return keyboard.f11Key;
                case KeyCode.F12:
                    return keyboard.f12Key;
                case KeyCode.LeftAlt:
                    return keyboard.leftAltKey;
                case KeyCode.RightAlt:
                    return keyboard.rightAltKey;
                case KeyCode.LeftControl:
                    return keyboard.leftCtrlKey;
                case KeyCode.RightControl:
                    return keyboard.rightCtrlKey;
                case KeyCode.LeftShift:
                    return keyboard.leftShiftKey;
                case KeyCode.RightShift:
                    return keyboard.rightShiftKey;
                default:
                    return null;
            }
        }
    }

    public sealed class RoBasicInfoIconHotkeyExecutor {
        private readonly Func<RoBasicInfoIconContext> contextProvider;
        private readonly Func<bool> suppressHotkeys;
        private readonly IRoKeyboardInputState input;
        private readonly RoClientSettingsModel settings;

        public RoBasicInfoIconHotkeyExecutor(
            Func<RoBasicInfoIconContext> contextProvider,
            Func<bool> suppressHotkeys,
            IRoKeyboardInputState input,
            RoClientSettingsModel settings = null
        ) {
            this.contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            this.suppressHotkeys = suppressHotkeys;
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.settings = settings;
        }

        public bool TryExecute() {
            if (suppressHotkeys != null && suppressHotkeys()) {
                return false;
            }

            EventModifiers currentModifiers = GetCurrentKeyboardModifiers(input);
            IReadOnlyList<IRoBasicInfoIcon> icons = RoBasicInfoIconRegistry.All;
            for (int i = 0; i < icons.Count; i++) {
                IRoBasicInfoIcon icon = icons[i];
                if (!TryResolveIconChord(icon, out RoKeyChord chord) ||
                    chord.Modifiers != currentModifiers ||
                    !input.IsKeyPressedThisFrame(chord.Key)) {
                    continue;
                }

                RoBasicInfoIconContext context = contextProvider();
                if (context.Session == null || context.Host == null || !icon.IsAvailable(context)) {
                    return false;
                }

                icon.Toggle(context);
                return true;
            }

            return false;
        }

        private bool TryResolveIconChord(IRoBasicInfoIcon icon, out RoKeyChord chord) {
            chord = RoKeyChord.None;
            if (icon == null) {
                return false;
            }

            if (settings != null &&
                settings.TryGetShortcutBinding(RoClientSettingsModel.InterfaceActionId(icon.Id), out RoShortcutBindingSnapshot binding) &&
                binding.RuntimeSupported) {
                if (!binding.Binding.IsAssigned) {
                    return false;
                }

                chord = binding.Binding;
                return true;
            }

            if (!icon.Hotkey.HasValue) {
                return false;
            }

            chord = new RoKeyChord(icon.Hotkey.Value, icon.HotkeyModifiers);
            return chord.IsAssigned;
        }

        public static EventModifiers GetCurrentKeyboardModifiers(IRoKeyboardInputState input) {
            if (input == null) {
                return EventModifiers.None;
            }

            EventModifiers modifiers = EventModifiers.None;
            if (input.IsKeyHeld(KeyCode.LeftAlt) || input.IsKeyHeld(KeyCode.RightAlt)) {
                modifiers |= EventModifiers.Alt;
            }

            if (input.IsKeyHeld(KeyCode.LeftControl) || input.IsKeyHeld(KeyCode.RightControl)) {
                modifiers |= EventModifiers.Control;
            }

            if (input.IsKeyHeld(KeyCode.LeftShift) || input.IsKeyHeld(KeyCode.RightShift)) {
                modifiers |= EventModifiers.Shift;
            }

            return modifiers;
        }
    }

    public sealed class RoLegacyHotkeyCommandExecutor : IRoHotkeyCommandExecutor {
        private readonly RoLoginShellSession session;
        private readonly Func<int> targetAidProvider;

        public RoLegacyHotkeyCommandExecutor(RoLoginShellSession session, Func<int> targetAidProvider = null) {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.targetAidProvider = targetAidProvider;
        }

        public Task UseHotkeyAsync(int hotkeyIndex) {
            int targetAid = targetAidProvider != null ? Math.Max(0, targetAidProvider()) : 0;
            return session.BeginUseHotkey(hotkeyIndex, targetAid);
        }

        public Task SetHotkeyAsync(int hotkeyIndex, RoShortcutKeyEntry shortcut) {
            return session.BeginSetHotkey(hotkeyIndex, shortcut);
        }
    }

}
