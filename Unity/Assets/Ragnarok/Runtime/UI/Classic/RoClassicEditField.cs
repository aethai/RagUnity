using System;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// A text input of a classic window, after UIEditCtrl (reference ui-static ui/RUNTIME-RULES-CHAT.md §3): the text
    /// at (3 − width of the scrolled-off part, 2) in fontType 0 height 12, the selection as a full-height #709FED block
    /// under unchanged text, the caret "|" at caret x − 2 blinking 500 ms on / 500 ms off. Focusing (or clicking the
    /// focused field again) selects all of the text; there is no caret placement by mouse. The background is the inner
    /// fill (+0x90..+0x98), or else the control's own recorded ops. Keys follow CLanguage (§4) through
    /// <see cref="RoClassicTextInput"/>.
    /// </summary>
    public sealed class RoClassicEditField {
        private static readonly Color32 HighlightColor = new Color32(112, 159, 237, 255); // pixel (6,2) of colorchip.bmp

        private readonly StringBuilder text = new StringBuilder();
        private int caret;
        private int anchor;
        private int scroll;
        private int oldCaret;
        private bool manualScroll;
        private int caretX = -1;
        private readonly System.Collections.Generic.List<int> lineStarts = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<int> lineEnds = new System.Collections.Generic.List<int>();
        private RoClassicGdiFont layoutFont;

        /// <summary>Window-local rectangle of the control.</summary>
        public RectInt Rect;

        /// <summary>Maximum length in bytes, one per character under langtype 1 (+0x88, default 255).</summary>
        public int MaxBytes = 255;

        /// <summary>Mask character for passwords (+0x84), '\0' = none.</summary>
        public char PasswordMask;

        /// <summary>Text colour (+0xa8).</summary>
        public Color32 TextColor = new Color32(0, 0, 0, 255);

        /// <summary>Hides the caret (the chat's edits while its input row is off).</summary>
        public bool CaretHidden;

        /// <summary>Centres a single line that fits the control (UIEditCtrl_Custom of the login: its caret sits mid-box).</summary>
        public bool Centered;

        /// <summary>Parked controls neither draw nor take the mouse or the keyboard.</summary>
        public bool Visible = true;

        /// <summary>Inner fill drawn as the background (+0x8c == 1: Clear(inner)); null = the layout draws it.</summary>
        public Color32? Background;

        /// <summary>
        /// UIChatEditCtrl LBtnDown (0x7c00f0): after taking the focus the press goes on to the parent window in its
        /// coordinates, so the window's frame drag starts there too (the main chat moves by its edit).
        /// </summary>
        public bool ForwardsPressToParent;

        /// <summary>Esc belongs to this field's owner (the chat hides its input row) instead of closing windows.</summary>
        public bool HandlesEscape;

        /// <summary>
        /// UIEditCtrl3 (+0xb8 = 1, ui/RUNTIME-RULES-COMPANIONS-MAIL.md "Compose 0x108"): lines wrapped in pixels at the
        /// control width − 6 (− the scroll bar width + 3 once it scrolls), CRLF breaks, <see cref="LineHeight"/> px lines drawn
        /// at (3, 1 + pitch · (line − first)), Enter inserts CRLF, Up / Down move between lines keeping the x.
        /// </summary>
        public bool Multiline;

        /// <summary>
        /// Display only (+0xd4 = 1: UIEditCtrl::vf05 refuses the click-to-edit path): the mouse never focuses it; the owner
        /// scrolls it with <see cref="ScrollLines"/>.
        /// </summary>
        public bool ReadOnly;

        /// <summary>The line pitch of a multi-line edit (+0x134, 14).</summary>
        public int LineHeight = 14;

        /// <summary>The scroll bar's width a multi-line edit leaves free once its lines overflow (13).</summary>
        public int ScrollBarWidth = 13;

        /// <summary>The selection fill (colorchip (6,2) = #709FED).</summary>
        public Color32 SelectionColor = HighlightColor;

        /// <summary>The first visible line of a multi-line edit (+0x154).</summary>
        public int FirstVisible { get; private set; }

        /// <summary>The wrapped line count after the last layout.</summary>
        public int LineCount => lineStarts.Count;

        public int VisibleLines => Mathf.Max(1, Rect.height / Mathf.Max(1, LineHeight));

        /// <summary>Msg 7 / wheel: moves the first visible line, clamped to the text.</summary>
        public void ScrollLines(int delta) {
            int next = Mathf.Clamp(FirstVisible + delta, 0, Mathf.Max(0, lineStarts.Count - VisibleLines));
            if (next == FirstVisible) return;
            FirstVisible = next;
            manualScroll = true;
            Touch();
        }

        /// <summary>The window drawing this field (set when it is attached to one).</summary>
        public RoClassicWindow Window { get; internal set; }

        /// <summary>Enter (SendMsg 0 → the parent).</summary>
        public event Action<RoClassicEditField> Submitted;

        /// <summary>Escape.</summary>
        public event Action<RoClassicEditField> Cancelled;

        public event Action<RoClassicEditField> Changed;

        /// <summary>Tab (the chat moves between its whisper and message edits).</summary>
        public event Action<RoClassicEditField> TabPressed;

        /// <summary>Up (+1) / Down (−1): the chat edits' history recall.</summary>
        public event Action<RoClassicEditField, int> HistoryRequested;

        public string Text => text.ToString();
        public int Caret => caret;
        public bool HasFocus => RoClassicTextInput.Focused == this;

        /// <summary>OnBeginEdit 0x7bc0d0: takes the keyboard and selects all (also when it already had it).</summary>
        public void Focus() {
            RoClassicTextInput.Focus(this);
            SelectAll();
        }

        public void Blur() {
            if (HasFocus) RoClassicTextInput.Focus(null);
        }

        /// <summary>SetText (vf53 0x7c96d0): the text, truncated to the limit, then all selected.</summary>
        public void SetText(string value) {
            text.Clear();
            text.Append(value ?? string.Empty);
            Truncate();
            SelectAll();
            Changed?.Invoke(this);
        }

        /// <summary>SelectAll (vf54): anchor 0, caret at the end.</summary>
        public void SelectAll() {
            anchor = 0;
            caret = text.Length;
            Touch();
        }

        // ---- CLanguage editing (OnChar 0x9fe8e0 / OnKeyDown 0x9feb00) ----

        public void Insert(string s) {
            if (string.IsNullOrEmpty(s)) return;
            DeleteSelection();
            caretX = -1;
            manualScroll = false;
            text.Insert(caret, s);
            caret += s.Length;
            anchor = caret;
            Truncate();
            Changed?.Invoke(this);
            Touch();
        }

        public void Backspace() {
            if (caret == anchor && caret > 0) caret--;
            DeleteSelection();
        }

        public void Delete() {
            if (caret == anchor && caret < text.Length) caret++;
            DeleteSelection();
        }

        /// <summary>Home/End/Left/Right: without Shift caret and anchor move together, with Shift the anchor stays.</summary>
        public void MoveCaret(int to, bool extend) {
            caretX = -1;
            manualScroll = false;
            caret = Mathf.Clamp(to, 0, text.Length);
            if (!extend) anchor = caret;
            Touch();
        }

        public string SelectedText() {
            int a = Mathf.Min(anchor, caret), b = Mathf.Max(anchor, caret);
            if (a == b) return null;
            return PasswordMask != '\0' ? new string('*', b - a) : text.ToString(a, b - a);
        }

        public bool DeleteSelection() {
            if (anchor == caret) return false;
            int a = Mathf.Min(anchor, caret), b = Mathf.Max(anchor, caret);
            text.Remove(a, b - a);
            caret = anchor = a;
            Changed?.Invoke(this);
            Touch();
            return true;
        }

        // what Enter, Esc, Tab and Up/Down do (hosts may drive the field with them too)
        public void Submit() => Submitted?.Invoke(this);
        public void Cancel() => Cancelled?.Invoke(this);
        public void Tab() => TabPressed?.Invoke(this);
        public void History(int direction) => HistoryRequested?.Invoke(this, direction);

        /// <summary>Truncate (vf57): the text cut to the byte limit, caret and anchor clamped.</summary>
        private void Truncate() {
            if (MaxBytes > 0 && text.Length > MaxBytes) text.Length = MaxBytes;
            caret = Mathf.Min(caret, text.Length);
            anchor = Mathf.Min(anchor, text.Length);
        }

        // ---- drawing (OnDraw 0x7be290 → vf55 0x7b8bf0) ----

        /// <summary>FUN_009b2460: the lines of a multi-line edit (start / end char offsets, CRLF excluded).</summary>
        private void LayoutLines(RoClassicGdiFont font) {
            layoutFont = font;
            string s = text.ToString();
            int width = Rect.width - 6;
            Wrap(font, s, width);
            if (lineStarts.Count > VisibleLines) Wrap(font, s, width + 3 - ScrollBarWidth);
        }

        private void Wrap(RoClassicGdiFont font, string s, int width) {
            lineStarts.Clear();
            lineEnds.Clear();
            int start = 0, x = 0, i = 0;
            while (i < s.Length) {
                char c = s[i];
                if (c == '\r' || c == '\n') {
                    lineStarts.Add(start);
                    lineEnds.Add(i);
                    i += c == '\r' && i + 1 < s.Length && s[i + 1] == '\n' ? 2 : 1;
                    start = i;
                    x = 0;
                    continue;
                }

                font.GetGlyph(c, out RoClassicGdiFont.Glyph g);
                if (x + g.Advance > width && i > start) {
                    // the glyph that would exceed the width starts the next line
                    lineStarts.Add(start);
                    lineEnds.Add(i);
                    start = i;
                    x = 0;
                    continue;
                }

                x += g.Advance;
                i++;
            }

            lineStarts.Add(start);
            lineEnds.Add(s.Length);
        }

        private int LineOf(int offset) {
            for (int l = lineStarts.Count - 1; l >= 0; l--) {
                if (offset >= lineStarts[l]) return l;
            }

            return 0;
        }

        /// <summary>Up / Down: the caret to the neighbour line at the remembered x.</summary>
        public void MoveVertical(int lines, bool extend) {
            if (layoutFont == null || lineStarts.Count == 0) return;
            string s = text.ToString();
            int line = LineOf(caret);
            if (caretX < 0) caretX = Width(layoutFont, s, lineStarts[line], caret);
            int target = Mathf.Clamp(line + lines, 0, lineStarts.Count - 1);
            if (target == line) return;
            int pos = lineStarts[target], x = 0;
            while (pos < lineEnds[target]) {
                layoutFont.GetGlyph(s[pos], out RoClassicGdiFont.Glyph g);
                if (x + g.Advance / 2 > caretX) break;
                x += g.Advance;
                pos++;
            }

            int keep = caretX;
            MoveCaret(pos, extend);
            caretX = keep;
        }

        /// <summary>Caret following (vf55 0x7b75d0): the caret's line stays in view unless the user scrolled by hand.</summary>
        private void FollowCaret() {
            if (manualScroll) return;
            int line = LineOf(caret), visible = VisibleLines;
            if (line < FirstVisible) FirstVisible = line;
            else if (line >= FirstVisible + visible) FirstVisible = line - visible + 1;
            FirstVisible = Mathf.Clamp(FirstVisible, 0, Mathf.Max(0, lineStarts.Count - visible));
        }

        private void DrawMultiline(RoClassicWindow w, RoClassicGdiFont font) {
            if (Background.HasValue) w.FillRect(Rect.x, Rect.y, Rect.width, Rect.height, Background.Value);
            LayoutLines(font);
            FollowCaret();
            string s = text.ToString();
            int a = Mathf.Min(anchor, caret), b = Mathf.Max(anchor, caret);
            bool focus = HasFocus;
            int visible = VisibleLines;
            for (int l = FirstVisible; l < lineStarts.Count && l < FirstVisible + visible; l++) {
                int y = Rect.y + 1 + LineHeight * (l - FirstVisible), start = lineStarts[l], end = lineEnds[l];
                if (focus && a != b && a < end + 1 && b > start) {
                    int from = Mathf.Max(a, start), to = Mathf.Min(b, end);
                    int xs = Width(font, s, start, from), xe = Width(font, s, start, to);
                    if (b > end && l + 1 < lineStarts.Count) xe += 3;
                    if (xe > xs) w.FillRect(Rect.x + 3 + xs, y, Mathf.Min(xe - xs, Rect.width - 3 - xs), LineHeight, SelectionColor);
                }

                w.TextOutPlain(Rect.x + 3, y, s.Substring(start, end - start), font, TextColor);
            }

            if (!focus || CaretHidden || a != b || ((long) (Time.realtimeSinceStartup * 1000) / 500 & 1) != 0) return;
            int line = LineOf(caret);
            if (line < FirstVisible || line >= FirstVisible + visible) return;
            int cx = Width(font, s, lineStarts[line], Mathf.Min(caret, lineEnds[line]));
            w.TextOutPlain(Rect.x + 3 + cx - 2, Rect.y + 1 + LineHeight * (line - FirstVisible), "|", font, TextColor);
        }

        internal void Draw(RoClassicWindow w, RoClassicGdiFont font) {
            if (Multiline) {
                DrawMultiline(w, font);
                return;
            }

            if (Background.HasValue) w.FillRect(Rect.x, Rect.y, Rect.width, Rect.height, Background.Value);
            string shown = PasswordMask != '\0' ? new string(PasswordMask, text.Length) : text.ToString();
            int ty = Rect.y + 2;
            int centred = -1;
            if (Centered) {
                int width = Width(font, shown, 0, shown.Length);
                if (width + 6 <= Rect.width) centred = Rect.x + (Rect.width - width) / 2;
            }

            if (!HasFocus) {
                w.TextOutPlain(centred >= 0 ? centred : Rect.x + 3, ty, shown, font, TextColor);
                return;
            }

            Sync(font, shown);
            int x0 = centred >= 0 ? centred : Rect.x + 3 - Width(font, shown, 0, scroll);
            int a = Mathf.Min(anchor, caret), b = Mathf.Max(anchor, caret);
            int xa = Width(font, shown, 0, a), xb = Width(font, shown, 0, b);
            if (a == b) {
                w.TextOutPlain(x0, ty, shown, font, TextColor);
                if (!CaretHidden && ((long) (Time.realtimeSinceStartup * 1000) / 500 & 1) == 0) {
                    w.TextOutPlain(xa - 2 + x0, ty, "|", font, TextColor);
                }

                return;
            }

            Highlight(w, xa + x0 - Rect.x, xb - xa);
            w.TextOutPlain(x0, ty, shown, font, TextColor);
        }

        /// <summary>The selection block: full control height from x ≥ 3, clipped to the control.</summary>
        private void Highlight(RoClassicWindow w, int x, int width) {
            int xs = Mathf.Max(3, x);
            if (x < 3) width += x - 3;
            if (xs < Rect.width && width > 0) w.FillRect(Rect.x + xs, Rect.y, Mathf.Min(width, Rect.width - xs), Rect.height, HighlightColor);
        }

        /// <summary>vf56 0x7c4570: the horizontal scroll follows the caret like the client's (byte offsets).</summary>
        private void Sync(RoClassicGdiFont font, string s) {
            int len = s.Length, limit = Rect.width - 7;
            if (caret == 0) {
                scroll = 0;
            } else {
                if (len <= Mathf.Abs(caret - anchor)) scroll = 0;
                if (caret > oldCaret) {
                    if (3 - Width(font, s, 0, scroll) + Width(font, s, 0, caret) > limit) {
                        int p = Mathf.Min(1, len);
                        while (p < len && Width(font, s, p, len) + 3 > limit) p++;
                        long fit = caret - (len - p);                 // (unsigned) in the client: negative = no limit
                        long step = scroll + (caret - oldCaret);
                        scroll = (int) (fit < 0 ? step : Math.Min(fit, step));
                    }
                } else if (caret < oldCaret) {
                    if (Width(font, s, 0, caret) + 3 < limit && caret < scroll) scroll -= oldCaret - caret;
                    scroll = Mathf.Max(0, scroll);
                }
            }

            scroll = Mathf.Clamp(scroll, 0, len);
            oldCaret = caret;
        }

        private static int Width(RoClassicGdiFont font, string s, int from, int to) {
            int width = 0;
            for (int i = from; i < to && i < s.Length; i++) {
                font.GetGlyph(s[i], out RoClassicGdiFont.Glyph g);
                width += g.Advance;
            }

            return width;
        }

        private void Touch() => Window?.Invalidate();
    }

    /// <summary>
    /// The keyboard side of classic text fields (the client's CLanguage, ui/RUNTIME-RULES-CHAT.md §4): one focused field
    /// gets the typed characters (InputSystem text input: OS layout and repeat) and the editing keys, polled once per
    /// frame with Windows' repeat timing for held keys. Gameplay hotkeys check <see cref="HasFocus"/>.
    /// </summary>
    public static class RoClassicTextInput {
        private const float RepeatDelay = 0.5f;
        private const float RepeatInterval = 1f / 30f;

        public static RoClassicEditField Focused { get; private set; }

        /// <summary>True while a classic field takes the keyboard.</summary>
        public static bool HasFocus => Focused != null;

        private static readonly System.Collections.Generic.List<Func<bool>> keyboardClaims = new System.Collections.Generic.List<Func<bool>>();

        /// <summary>
        /// A window ahead of the chat in the client's Enter chain that also takes Esc while <paramref name="claim"/> holds
        /// (0x9bcff0: the NPC input, menu and say box; the Shortcut Settings window binding a key): the game adds one per window.
        /// </summary>
        public static void AddKeyboardClaim(Func<bool> claim) {
            if (claim != null && !keyboardClaims.Contains(claim)) keyboardClaims.Add(claim);
        }

        public static void RemoveKeyboardClaim(Func<bool> claim) => keyboardClaims.Remove(claim);

        public static bool KeyboardClaimed {
            get {
                for (int i = 0; i < keyboardClaims.Count; i++) {
                    if (keyboardClaims[i]()) return true;
                }

                return false;
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static Keyboard hooked;
        private static Key heldKey = Key.None;
        private static float nextRepeat;
#endif
        private static int blinkPhase = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            Focused = null;
            keyboardClaims.Clear();
#if ENABLE_INPUT_SYSTEM
            hooked = null;
            heldKey = Key.None;
#endif
        }

        public static void Focus(RoClassicEditField field) {
            if (Focused == field) return;
            RoClassicEditField previous = Focused;
            Focused = field;
            previous?.Window?.Invalidate();
            field?.Window?.Invalidate();
        }

        /// <summary>Called once per frame by the classic UI system.</summary>
        public static void Process() {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != hooked) {
                if (hooked != null) hooked.onTextInput -= OnTextInput;
                hooked = keyboard;
                if (hooked != null) hooked.onTextInput += OnTextInput;
            }

            RoClassicEditField f = Focused;
            if (f == null || keyboard == null) return;
            ProcessFocused(keyboard, f);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>The focused field's keys (apart from Process: its key lambdas allocate only while a field has the focus).</summary>
        private static void ProcessFocused(Keyboard keyboard, RoClassicEditField f) {
            if (!f.Visible || (f.Window != null && !f.Window.Visible)) {
                Focus(null);
                return;
            }

            // the caret blinks from the global clock: redraw the window when its phase flips
            int phase = (int) (Time.realtimeSinceStartup * 2f) & 1;
            if (phase != blinkPhase) {
                blinkPhase = phase;
                f.Window?.Invalidate();
            }

            bool ctrl = keyboard.ctrlKey.isPressed, shift = keyboard.shiftKey.isPressed;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) {
                // UIEditCtrl3: Enter writes CRLF into the text (Msg 0 → 0x0d, 0x0a)
                if (f.Multiline) f.Insert("\r\n");
                else f.Submit();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame) f.Cancel();
            else if (keyboard.tabKey.wasPressedThisFrame) f.Tab();
            if (Focused != f) return; // the handlers above may move the focus

            if (ctrl && !shift) {
                // Ctrl: copy / paste / cut only (no Ctrl+A, no word jumps)
                if (keyboard.cKey.wasPressedThisFrame) Copy(f);
                else if (keyboard.vKey.wasPressedThisFrame) f.Insert(GUIUtility.systemCopyBuffer);
                else if (keyboard.xKey.wasPressedThisFrame) {
                    Copy(f);
                    f.DeleteSelection();
                }

                return;
            }

            if (shift && keyboard.insertKey.wasPressedThisFrame) f.Insert(GUIUtility.systemCopyBuffer);
            if (shift && keyboard.deleteKey.wasPressedThisFrame) {
                Copy(f);
                f.DeleteSelection();
            } else {
                Repeating(keyboard, Key.Delete, () => f.Delete());
            }

            Repeating(keyboard, Key.Backspace, () => f.Backspace());
            Repeating(keyboard, Key.LeftArrow, () => f.MoveCaret(f.Caret - 1, shift));
            Repeating(keyboard, Key.RightArrow, () => f.MoveCaret(f.Caret + 1, shift));
            if (keyboard.homeKey.wasPressedThisFrame) f.MoveCaret(0, shift);
            if (keyboard.endKey.wasPressedThisFrame) f.MoveCaret(int.MaxValue, shift);
            if (f.Multiline) {
                Repeating(keyboard, Key.UpArrow, () => f.MoveVertical(-1, shift));
                Repeating(keyboard, Key.DownArrow, () => f.MoveVertical(1, shift));
                return;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame) f.History(1);
            if (keyboard.downArrowKey.wasPressedThisFrame) f.History(-1);
        }
#endif

        /// <summary>
        /// True when a classic field has the keyboard and this frame's key press is text for it (FUN_009d60a0): only
        /// Tab, Enter, Shift, Ctrl, Alt, CapsLock, Insert, F1–F12 and Ctrl/Alt + key still reach the hotkeys; Esc too,
        /// unless the field's owner takes it. Gameplay hotkey handlers skip the frame when this is true.
        /// </summary>
        public static bool CapturesKeyPress() {
#if ENABLE_INPUT_SYSTEM
            RoClassicEditField f = Focused;
            Keyboard keyboard = Keyboard.current;
            if (RoClassicMessageBox.ModalOpen && keyboard != null) {
                // a modal message box: every key is its (Enter / Esc) or nobody's
                foreach (KeyControl key in keyboard.allKeys) {
                    if (key != null && key.wasPressedThisFrame) return true;
                }

                return false;
            }

            if (keyboard != null && !HasFocus && KeyboardClaimed &&
                (keyboard.escapeKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) {
                return true;   // an NPC window takes Enter / swallows Esc ahead of the chat and the Esc menu
            }

            if (f == null || keyboard == null || !HasFocus || !f.Visible) return false;
            if (keyboard.escapeKey.wasPressedThisFrame) return f.HandlesEscape;
            if (keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed) return false;
            foreach (KeyControl key in keyboard.allKeys) {
                if (key == null || !key.wasPressedThisFrame) continue;
                // a multi-line edit keeps Enter (it becomes CRLF)
                if (f.Multiline && (key.keyCode == Key.Enter || key.keyCode == Key.NumpadEnter)) return true;
                if (PassesToHotkeys(key.keyCode)) continue;
                return true;
            }
#endif
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private static bool PassesToHotkeys(Key key) {
            if (key >= Key.F1 && key <= Key.F12) return true;
            switch (key) {
                case Key.Tab:
                case Key.Enter:
                case Key.NumpadEnter:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.CapsLock:
                case Key.Insert:
                case Key.Escape:
                    return true;
                default:
                    return false;
            }
        }
#endif

        private static void Copy(RoClassicEditField f) {
            string selected = f.SelectedText();
            if (selected != null) GUIUtility.systemCopyBuffer = selected;
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>A key acts on press, then after 0.5 s every 1/30 s while held (Windows' default repeat).</summary>
        private static void Repeating(Keyboard keyboard, Key key, Action action) {
            var control = keyboard[key];
            if (control.wasPressedThisFrame) {
                heldKey = key;
                nextRepeat = Time.unscaledTime + RepeatDelay;
                action();
            } else if (heldKey == key && control.isPressed && Time.unscaledTime >= nextRepeat) {
                nextRepeat += RepeatInterval;
                action();
            } else if (heldKey == key && !control.isPressed) {
                heldKey = Key.None;
            }
        }

        /// <summary>OnChar: printable characters are inserted, control characters ignored (keys handle them).</summary>
        private static void OnTextInput(char c) {
            RoClassicEditField f = Focused;
            if (f == null || !f.Visible || c < 0x20 || c == 0x7f) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed) return;
            f.Insert(c.ToString());
        }
#endif
    }
}
