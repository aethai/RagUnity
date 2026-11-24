using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// The Shortcut Settings window, UIHotKeyWnd 0x9c (ui/RUNTIME-RULES-HOTKEYS.md §1): 436×432, the tabs Skill Bar /
    /// Hotkey Bar 2 / Interface / Macros over 18 × 2 rows a page, a click selects a row and the next key binds it (Esc
    /// clears it), conflicts ask first, nothing is kept until OK. While the window is open the game's hotkeys rest
    /// (<see cref="ShouldSuppressGameplayHotkeys"/>). Toggled by the Esc menu's button; the bindings are the shell's
    /// settings model.
    /// </summary>
    public sealed class RoClassicShortcutSettings {
        private const int ScW = 436;
        private const int ScH = 432;
        private const int ScRows = 18;
        private const int ScPage = 36;
        private const int ScBaseId = 1;
        private const int ScCloseId = 0xc5;
        private const int ScResetId = 0x167;
        private const int ScOkId = 0xb4;
        private const int ScCancelId = 0xb5;
        private const int ScPrevId = 0x138;
        private const int ScNextId = 0x139;
        private const string ScUi = "유저인터페이스\\";
        private const string ScBasic = ScUi + "basic_interface\\";
        private const string ScPrefs = "ro.ui.classic.shortcuts.";

        private static readonly Color32 ScBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 ScWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ScYellow = new Color32(255, 255, 0, 255);
        private static readonly Color32 ScLine = new Color32(0xc0, 0xc0, 0xc0, 255);
        private static readonly Color32 ScTabLine = new Color32(0xc3, 0xc3, 0xc3, 255);
        private static readonly Color32 ScSelected = new Color32(0xff, 0xe1, 0xe1, 255);
        private static readonly Color32 ScPageNumber = new Color32(0xff, 0x3c, 0x7f, 255);
        private static readonly Color32 ScPageTotal = new Color32(0x66, 0x66, 0x66, 255);

        // the tabs in the client's order and the table each shows (Skill Bar → HOTKEY_1, Hotkey Bar 2 → HOTKEY_4,
        // Interface → HOTKEY_2, Macros → HOTKEY_3) with its key box colour
        private static readonly (int MsgId, string Fallback, string Category, Color32 KeyBox)[] ScTabs = {
            (0x5d3, "Skill Bar", RoClientSettingsModel.CategorySkillBar, new Color32(0xd6, 0xef, 0xff, 255)),
            (0xe0b, "Hotkey Bar 2", RoClientSettingsModel.CategoryHotkeyBar2, new Color32(0xff, 0xd6, 0xef, 255)),
            (0x5d4, "Interface", RoClientSettingsModel.CategoryInterface, new Color32(0xfe, 0xff, 0xdf, 255)),
            (0x5d5, "Macros", RoClientSettingsModel.CategoryMacros, new Color32(0xfe, 0xff, 0xdf, 255)),
        };

        private static RoClassicShortcutSettings instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClientSettingsModel settings;
        private RoClassicWindow classicSc;
        private bool classicScShown;
        private int scTab;
        private int scPageNo = 1;
        private int scSelected = -1;          // +0x94, the selected row on the page
        private string scNotice;
        private Color32 scNoticeColor;
        private Func<bool> scKeyboardClaim;
        private readonly Dictionary<string, RoKeyChord> scPending = new Dictionary<string, RoKeyChord>(StringComparer.OrdinalIgnoreCase);

        public static bool IsOpen => instance != null && instance.classicScShown;

        /// <summary>While the window is open every key is its: the game's hotkey table rests (0x9d60a0).</summary>
        public static bool ShouldSuppressGameplayHotkeys => IsOpen;

        // ---- open / close ----

        /// <summary>The Esc menu's button (window 0x9c): an open window closes, else it opens.</summary>
        public static void Toggle(RoClientSettingsModel clientSettings) {
            if (IsOpen) {
                Close();
                return;
            }

            Open(clientSettings);
        }

        /// <summary>MakeWindow2(0x9c) on the Skill Bar tab with nothing pending; already open, it stays as it is.</summary>
        public static void Open(RoClientSettingsModel clientSettings) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicShortcutSettings { system = system };
            RoClassicShortcutSettings sc = instance;
            sc.settings = clientSettings ?? sc.settings ?? new RoClientSettingsModel();
            if (!sc.classicScShown) sc.Show();
        }

        public static void Close() => instance?.Hide();

        /// <summary>Per frame (RoClassicGameUi): the key pressed this frame binds the selected row, the balloons follow.</summary>
        public static void Tick() {
            RoClassicShortcutSettings sc = instance;
            if (sc == null || !sc.classicScShown) return;
            sc.Update();
        }

        private void Show() {
            RoClassicHeldSkill.Release();   // UIHotKeyWnd 0x881260: a held skill ends when the window opens
            EnsureWindow();
            scPending.Clear();
            SelectScTab(0);
            Vector2Int client = system.ClientSize;
            // GetWindowInfo(156): (W·300/1024, H·100/768), or the saved position when the whole window stays on screen
            int x = client.x * 300 / 1024, y = client.y * 100 / 768;
            int sx = PlayerPrefs.GetInt(ScPrefs + "x", int.MinValue), sy = PlayerPrefs.GetInt(ScPrefs + "y", int.MinValue);
            if (sx >= 0 && sy >= 0 && sx + ScW <= client.x && sy + ScH <= client.y) {
                x = sx;
                y = sy;
            }

            classicSc.MoveTo(x, y);
            classicSc.Visible = true;
            classicSc.Invalidate();
            system.Windows.BringToFront(classicSc);
            classicScShown = true;
            scKeyboardClaim ??= () => classicScShown && scSelected >= 0 && !RoClassicTextInput.HasFocus;
            RoClassicTextInput.AddKeyboardClaim(scKeyboardClaim);
        }

        private void EnsureWindow() {
            if (classicSc != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIHotKeyWnd", WindowId = 0x9c, Width = ScW, Height = ScH };
            // OnDraw 0x88cf10: the title bar, the button bar (painted over), then the body
            RoClassicFrames.TitleBar(layout.Ops, ScW, false);
            RoClassicFrames.Body(layout.Ops, ScW, ScH);
            layout.Children.Add(ScButton(ScBaseId, "base", 3, 3, 11, 11, ScBasic + "sys_base_off.bmp", ScBasic + "sys_base_on.bmp", ScBasic + "sys_base_on.bmp"));
            layout.Children.Add(ScButton(ScCloseId, "closex", ScW - 14, 3, 11, 11, ScBasic + "sys_close_off.bmp", ScBasic + "sys_close_on.bmp", ScBasic + "sys_close_on.bmp"));
            layout.Children.Add(ScButton(ScResetId, "reset", 5, ScH - 25, 42, 20, ScUi + "btn_reset.bmp", ScUi + "btn_reset_a.bmp", ScUi + "btn_reset_b.bmp"));
            layout.Children.Add(ScButton(ScOkId, "ok", ScW - 139, ScH - 25, 42, 20, ScUi + "btn_ok.bmp", ScUi + "btn_ok_a.bmp", ScUi + "btn_ok_b.bmp"));
            layout.Children.Add(ScButton(ScCancelId, "cancel", ScW - 92, ScH - 25, 42, 20, ScUi + "btn_cancel.bmp", ScUi + "btn_cancel_a.bmp", ScUi + "btn_cancel_b.bmp"));
            layout.Children.Add(ScButton(ScCloseId, "close", ScW - 46, ScH - 25, 42, 20, ScUi + "btn_close.bmp", ScUi + "btn_close_a.bmp", ScUi + "btn_close_b.bmp"));
            layout.Children.Add(ScButton(ScPrevId, "prev", ScW - 80, 25, 11, 11, ScBasic + "arw_left.bmp", ScBasic + "arw_left.bmp", ScBasic + "arw_left.bmp"));
            layout.Children.Add(ScButton(ScNextId, "next", ScW - 20, 25, 11, 11, ScBasic + "arw_right.bmp", ScBasic + "arw_right.bmp", ScBasic + "arw_right.bmp"));

            // not in the Esc list (IsEscExcluded 0x9c71c0): Esc only clears the selected row's key
            classicSc = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = local => local.y < 17 };
            classicSc.DrawDynamic += DrawClassicShortcuts;
            classicSc.Clicked += OnScButtonClicked;
            classicSc.Pressed += (w, local) => OnScPressed(local);
            classicSc.Moved += w => {
                PlayerPrefs.SetInt(ScPrefs + "x", w.Position.x);
                PlayerPrefs.SetInt(ScPrefs + "y", w.Position.y);
            };
        }

        private static RoClassicChildLayout ScButton(int id, string path, int x, int y, int w, int h, string normal, string hover, string pressed) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            button.States["normal"] = normal;
            button.States["hover"] = hover;
            button.States["pressed"] = pressed;
            return button;
        }

        private void Hide() {
            if (!classicScShown) return;
            classicScShown = false;
            scPending.Clear();
            scSelected = -1;
            if (classicSc != null) classicSc.Visible = false;
            RoClassicTextInput.RemoveKeyboardClaim(scKeyboardClaim);
            RoClassicTooltip.Named("hotkeynotice").Hide();
        }

        // ---- tables ----

        private string ScCategory => ScTabs[scTab].Category;

        private IReadOnlyList<string> ScTable => settings != null ? settings.ShortcutOrder(ScCategory) : Array.Empty<string>();

        private int ScPages => Math.Max(1, (ScTable.Count + ScPage - 1) / ScPage);

        /// <summary>msg 0x16 from the tab control: deselect, the tab's table, page 1, the arrows for a table above 36 rows.</summary>
        private void SelectScTab(int tab) {
            scTab = tab;
            scPageNo = 1;
            scSelected = -1;
            scNotice = null;
            UpdateScArrows();
        }

        /// <summary>0x8946b0: Prev while page &gt; 1, Next while page &lt; pages (a disabled arrow is hidden).</summary>
        private void UpdateScArrows() {
            if (classicSc == null) return;
            foreach (RoClassicChildLayout child in classicSc.Layout.Children) {
                if (child.Id == ScPrevId) classicSc.SetChildVisible(child, scPageNo > 1);
                else if (child.Id == ScNextId) classicSc.SetChildVisible(child, scPageNo < ScPages);
            }

            classicSc.Invalidate();
        }

        /// <summary>HotKey 0x87c040: the pending change, else the binding, else the default.</summary>
        private RoKeyChord ScBinding(string actionId) {
            if (scPending.TryGetValue(actionId, out RoKeyChord pending)) return pending;
            if (settings != null && settings.TryGetShortcutBinding(actionId, out RoShortcutBindingSnapshot binding)) return binding.Binding;
            return RoKeyChord.None;
        }

        private string ScActionName(string actionId) =>
            settings != null && settings.TryGetShortcutBinding(actionId, out RoShortcutBindingSnapshot binding) ? binding.DisplayName : actionId;

        // ---- drawing (OnDraw 0x88cf10) ----

        private void DrawClassicShortcuts(RoClassicWindow w) {
            RoClassicGdiFont font = w.FontFor(0, 12, false);
            string title = RoClassicUiHost.MsgStr(0x5d6, "Shortcut Settings");
            w.TextOut(19, 4, title, 0, 12, ScWhite);
            w.TextOut(18, 3, title, 0, 12, ScBlack);

            int tabsW = DrawScTabs(w, font);

            // the rows of this page: 18 a column, the label 135 wide, the key box 80
            IReadOnlyList<string> table = ScTable;
            Color32 keyBox = ScTabs[scTab].KeyBox;
            int v = 0;
            for (int i = ScPage * (scPageNo - 1); i < table.Count && i < ScPage * scPageNo; i++) {
                string actionId = table[i];
                int col = v / ScRows, r = v % ScRows, y = 40 + 20 * r;
                int rowX = col != 0 ? 218 : 3, keyX = col != 0 ? 353 : 138;
                w.FillRect(keyX, y, 80, 19, keyBox);
                if (v == scSelected) w.FillRect(rowX + 1, y, 214, 19, ScSelected);
                w.FillRect(rowX, y + 19, 215, 1, ScLine);
                w.FillRect(keyX, y, 1, 20, ScLine);
                string action = ScFit(ScActionName(actionId), font, 130);
                w.TextOut(rowX + (135 - font.Measure(action)) / 2, y + 5, action, 0, 12, ScBlack);
                string key = ScKeyText(ScBinding(actionId));
                w.TextOut(keyX + (80 - font.Measure(key)) / 2, y + 5, key, 0, 12, ScBlack);
                v++;
            }

            if (ScPages > 1) {
                // the page between the arrows: the number in #FF3C7F, " / pages" in #666666, both outlined white, h11 bold
                string page = scPageNo.ToString(CultureInfo.InvariantCulture), total = " / " + ScPages.ToString(CultureInfo.InvariantCulture);
                int x = 356 + (11 + 60 - font.Measure(page + total)) / 2;
                w.TextOutOutline(x, 25, page, 0, 11, ScPageNumber, ScWhite, true);
                x += font.Measure(page); // measured with h12 regular, drawn h11 bold (as the client does)
                w.TextOutOutline(x, 25, total, 0, 11, ScPageTotal, ScWhite, true);
            }

            const int listH = ScH - 6 - 20 - 17 - 29;
            w.FillRect(3, 40, 430, 1, ScLine);
            w.FillRect(3, 40 + listH - 1, 430, 1, ScLine);
            w.FillRect(3, 40, 1, listH, ScLine);
            w.FillRect(432, 40, 1, listH, ScLine);
            w.FillRect(218, 40, 1, listH, ScLine);
            w.FillRect(3, 37, 1, 3, ScLine);
            w.FillRect(432, 37, 1, 3, ScLine);
            w.FillRect(3 + tabsW, 37, 430 - tabsW, 1, ScLine);

            // Enable Battle Mode (UICheckBox 0xd1): the box at (0, 1), its label at (13, 0)
            bool battle = settings != null && settings.Snapshot().BattleModeEnabled;
            w.DrawBitmap(80, ScH - 21, ScUi + (battle ? "checkbox_1.bmp" : "checkbox_0.bmp"));
            w.TextOut(93, ScH - 22, RoClassicUiHost.MsgStr(0x6ef, "Enable Battle Mode"), 0, 12, ScBlack);
        }

        /// <summary>
        /// UITabControl style 1 at (3, 20): every tab as wide as the widest label + 20, overlapping by one column, #C3C3C3
        /// edges (the selected one a row higher and open below); returns the control's width.
        /// </summary>
        private int DrawScTabs(RoClassicWindow w, RoClassicGdiFont font) {
            int tw = 0;
            foreach ((int id, string fallback, _, _) in ScTabs) tw = Math.Max(tw, font.Measure(RoClassicUiHost.MsgStr(id, fallback)) + 20);
            int controlW = ScTabs.Length * (tw - 1) + 1;
            w.ClipTo(3, 20, controlW, 17);
            for (int i = 0; i < ScTabs.Length; i++) {
                int x = 3 + i * (tw - 1), y = 20 + (i == scTab ? 0 : 1);
                w.FillRect(x + 2, y, tw - 4, 1, ScTabLine);
                w.FillRect(x + 1, y + 1, 1, 1, ScTabLine);
                w.FillRect(x, y + 2, 1, 15, ScTabLine);
                w.FillRect(x + tw - 2, y + 1, 1, 1, ScTabLine);
                w.FillRect(x + tw - 1, y + 2, 1, 15, ScTabLine);
                if (i != scTab) w.FillRect(x, 20 + 16, tw, 1, ScTabLine);
                string label = RoClassicUiHost.MsgStr(ScTabs[i].MsgId, ScTabs[i].Fallback);
                int lw = font.Measure(label);
                w.TextOut(x + (tw - lw) / 2 + (tw - lw) % 2, 20 + (i == scTab ? 2 : 3), label, 0, 12, ScBlack);
            }

            w.ResetClip();
            return controlW;
        }

        private static string ScFit(string text, RoClassicGdiFont font, int width) {
            if (string.IsNullOrEmpty(text) || font.Measure(text) <= width) return text;
            for (int n = text.Length - 1; n > 0; n--) {
                string cut = text.Substring(0, n) + "...";
                if (font.Measure(cut) <= width) return cut;
            }

            return "...";
        }

        /// <summary>GetKeyDes: "&lt;modifier&gt; + &lt;key&gt;" with the client's KEYNAME, "Not Assigned" or "Unspecified value".</summary>
        private static string ScKeyText(RoKeyChord chord) {
            if (!chord.IsAssigned) return RoClassicUiHost.MsgStr(0x5ee, "Not Assigned");
            string key = ScKeyName(chord.Key);
            if (key == null) return RoClassicUiHost.MsgStr(0x6a4, "Unspecified value");
            EventModifiers m = chord.Modifiers;
            string modifier = (m & EventModifiers.Alt) != 0 ? "Alt" : (m & EventModifiers.Shift) != 0 ? "Shift" : (m & EventModifiers.Control) != 0 ? "Ctrl" : null;
            return modifier != null ? modifier + " + " + key : key;
        }

        /// <summary>hotkey.lub KEYNAME by key; null for keys without a name (Esc, Win).</summary>
        private static string ScKeyName(KeyCode key) {
            if (key >= KeyCode.A && key <= KeyCode.Z) return ((char) ('A' + (key - KeyCode.A))).ToString();
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((char) ('0' + (key - KeyCode.Alpha0))).ToString();
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "Num_" + (key - KeyCode.Keypad0).ToString(CultureInfo.InvariantCulture);
            if (key >= KeyCode.F1 && key <= KeyCode.F15) return "F" + (1 + (key - KeyCode.F1)).ToString(CultureInfo.InvariantCulture);
            switch (key) {
                case KeyCode.Backspace: return "Back";
                case KeyCode.Tab: return "Tab";
                case KeyCode.Clear: return "Clear";
                case KeyCode.Return:
                case KeyCode.KeypadEnter: return "Return";
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt: return "Alt";
                case KeyCode.Pause: return "Pause";
                case KeyCode.CapsLock: return "Caps";
                case KeyCode.Space: return "Space";
                case KeyCode.PageUp: return "Prior";
                case KeyCode.PageDown: return "Next";
                case KeyCode.End: return "End";
                case KeyCode.Home: return "Home";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.Insert: return "Ins";
                case KeyCode.Delete: return "Del";
                case KeyCode.Help: return "Help";
                case KeyCode.Print: return "Snapshot";
                case KeyCode.Menu: return "Apps";
                case KeyCode.KeypadMultiply: return "Multiply";
                case KeyCode.KeypadPlus: return "Add";
                case KeyCode.KeypadMinus: return "Sub";
                case KeyCode.KeypadPeriod: return "Decimal";
                case KeyCode.KeypadDivide: return "Devide";
                case KeyCode.Numlock: return "Num";
                case KeyCode.ScrollLock: return "Scroll";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Equals: return "=";
                case KeyCode.Comma: return ",";
                case KeyCode.Minus: return "-";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.BackQuote: return "`";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.Backslash: return "\\";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Quote: return "'";
                default: return null;
            }
        }

        // ---- mouse (0x891980, 0x892560) ----

        /// <summary>Hit test 0x87b6a0: x 3..433, y 40..400, 20 px rows, the right column from x 218; −1 outside.</summary>
        private int ScRowAt(Vector2Int local) {
            if (local.x < 3 || local.x > 433 || local.y < 40 || local.y > 400) return -1;
            int row = (local.y - 40) / 20;
            if (row >= ScRows) return -1;
            if (local.x > 218) row += ScRows;
            return row + ScPage * (scPageNo - 1) < ScTable.Count ? row : -1;
        }

        private void OnScPressed(Vector2Int local) {
            if (local.y < 17) return;
            // the tab strip (UITabControl at (3, 20), 17 high): a press switches
            if (local.y >= 20 && local.y < 37 && local.x >= 3) {
                RoClassicGdiFont font = classicSc.FontFor(0, 12, false);
                int tw = 0;
                foreach ((int id, string fallback, _, _) in ScTabs) tw = Math.Max(tw, font.Measure(RoClassicUiHost.MsgStr(id, fallback)) + 20);
                int t = (local.x - 3) / (tw - 1);
                if (t < ScTabs.Length) {
                    if (t != scTab) SelectScTab(t);
                    return;
                }
            }

            // Enable Battle Mode: the box and its label (UICheckBox 150×12 at (80, H − 22))
            if (local.x >= 80 && local.x < 230 && local.y >= ScH - 22 && local.y < ScH - 10) {
                if (settings != null) settings.SetBattleModeEnabled(!settings.Snapshot().BattleModeEnabled);
                classicSc.Invalidate();
                return;
            }

            int row = ScRowAt(local);
            if (row != scSelected) {
                scSelected = row;
                scNotice = null;
                classicSc.Invalidate();
            }
        }

        private void OnScButtonClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case ScOkId:
                    ApplyClassicShortcuts();
                    break;
                case ScCancelId:
                    // drop the pending changes, deselect; the window stays
                    scPending.Clear();
                    scSelected = -1;
                    classicSc.Invalidate();
                    break;
                case ScResetId:
                    ResetClassicShortcuts();
                    break;
                case ScCloseId:
                    Hide();
                    break;
                case ScPrevId:
                    if (scPageNo > 1) {
                        scPageNo--;
                        scSelected = -1;
                        UpdateScArrows();
                    }

                    break;
                case ScNextId:
                    if (scPageNo < ScPages) {
                        scPageNo++;
                        scSelected = -1;
                        UpdateScArrows();
                    }

                    break;
            }
        }

        /// <summary>
        /// OK: every pending change into the settings (ChangeUserHotKey), the chat line MsgStr 0xdf4 in yellow, the window
        /// closes. The changed keys are cleared first, so a key moved from one row to another lands.
        /// </summary>
        private void ApplyClassicShortcuts() {
            if (settings != null) {
                foreach (KeyValuePair<string, RoKeyChord> change in scPending) settings.ClearShortcut(change.Key);
                foreach (KeyValuePair<string, RoKeyChord> change in scPending) {
                    if (change.Value.IsAssigned) settings.RebindShortcut(change.Key, change.Value);
                }
            }

            scPending.Clear();
            // type 1 "Public Chat" (RUNTIME-RULES-OPTIONS §7e), COLORREF 0x00FFFF00
            RoClassicChat.Instance?.AddTypedLine(
                RoClassicUiHost.MsgStr(0xdf4, "The hotkey settings are stored on the server on normal exit."), "#FFFF00", RoChatLineTypes.PublicChat);
            Hide();
        }

        /// <summary>Reset 0x87c340: deselect, drop the pending changes, every changed binding of the four tables back to its default (until OK).</summary>
        private void ResetClassicShortcuts() {
            scSelected = -1;
            scPending.Clear();
            if (settings != null) {
                foreach ((_, _, string category, _) in ScTabs) {
                    foreach (string actionId in settings.ShortcutOrder(category)) {
                        if (!settings.TryGetDefaultShortcutBinding(actionId, out RoShortcutBindingSnapshot def)) continue;
                        if (ScBinding(actionId) != def.Binding) scPending[actionId] = def.Binding;
                    }
                }
            }

            classicSc.Invalidate();
        }

        // ---- keys (0x9d60a0 → 0x9bc200 → 0x875ad0) ----

        private static readonly (Key Input, KeyCode Code)[] ScKeys = BuildScKeys();

        private static (Key, KeyCode)[] BuildScKeys() {
            var keys = new List<(Key, KeyCode)>();
            for (int i = 0; i < 26; i++) keys.Add((Key.A + i, KeyCode.A + i));
            for (int i = 0; i < 10; i++) keys.Add((i == 0 ? Key.Digit0 : Key.Digit1 + (i - 1), KeyCode.Alpha0 + i));
            for (int i = 0; i < 10; i++) keys.Add((Key.Numpad0 + i, KeyCode.Keypad0 + i));
            for (int i = 0; i < 12; i++) keys.Add((Key.F1 + i, KeyCode.F1 + i));
            keys.AddRange(new[] {
                (Key.Tab, KeyCode.Tab), (Key.Enter, KeyCode.Return), (Key.NumpadEnter, KeyCode.Return), (Key.Insert, KeyCode.Insert),
                (Key.Delete, KeyCode.Delete), (Key.Home, KeyCode.Home), (Key.End, KeyCode.End), (Key.PageUp, KeyCode.PageUp),
                (Key.PageDown, KeyCode.PageDown), (Key.LeftArrow, KeyCode.LeftArrow), (Key.RightArrow, KeyCode.RightArrow),
                (Key.UpArrow, KeyCode.UpArrow), (Key.DownArrow, KeyCode.DownArrow), (Key.Space, KeyCode.Space),
                (Key.Backspace, KeyCode.Backspace), (Key.Backquote, KeyCode.BackQuote), (Key.Quote, KeyCode.Quote),
                (Key.Semicolon, KeyCode.Semicolon), (Key.Equals, KeyCode.Equals), (Key.Comma, KeyCode.Comma), (Key.Minus, KeyCode.Minus),
                (Key.Period, KeyCode.Period), (Key.Slash, KeyCode.Slash), (Key.LeftBracket, KeyCode.LeftBracket),
                (Key.Backslash, KeyCode.Backslash), (Key.RightBracket, KeyCode.RightBracket), (Key.NumpadMultiply, KeyCode.KeypadMultiply),
                (Key.NumpadPlus, KeyCode.KeypadPlus), (Key.NumpadMinus, KeyCode.KeypadMinus), (Key.NumpadPeriod, KeyCode.KeypadPeriod),
                (Key.NumpadDivide, KeyCode.KeypadDivide), (Key.CapsLock, KeyCode.CapsLock), (Key.NumLock, KeyCode.Numlock),
                (Key.ScrollLock, KeyCode.ScrollLock), (Key.Pause, KeyCode.Pause), (Key.PrintScreen, KeyCode.Print),
                (Key.ContextMenu, KeyCode.Menu), (Key.LeftAlt, KeyCode.LeftAlt), (Key.RightAlt, KeyCode.LeftAlt),
                (Key.LeftCtrl, KeyCode.LeftControl), (Key.RightCtrl, KeyCode.LeftControl), (Key.LeftShift, KeyCode.LeftShift),
                (Key.RightShift, KeyCode.LeftShift),
            });
            return keys.ToArray();
        }

        private static bool IsScModifier(KeyCode key) => key == KeyCode.LeftAlt || key == KeyCode.LeftControl || key == KeyCode.LeftShift || key == KeyCode.Tab;

        private static EventModifiers ScModifierOf(KeyCode key) =>
            key == KeyCode.LeftAlt ? EventModifiers.Alt : key == KeyCode.LeftShift ? EventModifiers.Shift : key == KeyCode.LeftControl ? EventModifiers.Control : EventModifiers.None;

        /// <summary>Per frame while open: the key pressed this frame binds the selected row (Esc clears it).</summary>
        private void Update() {
            ShowScBalloons();
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || scSelected < 0 || RoClassicTextInput.HasFocus || RoClassicMessageBox.ModalOpen) return; // code 1: a field types
            if (keyboard.escapeKey.wasPressedThisFrame) {
                StageScBinding(RoKeyChord.None); // code 0: clear
                return;
            }

            KeyCode pressed = KeyCode.None;
            foreach ((Key input, KeyCode keyCode) in ScKeys) {
                if (keyboard[input].wasPressedThisFrame) {
                    pressed = keyCode;
                    break;
                }
            }

            if (pressed == KeyCode.None) return;
            // the modifier held: Alt, else Shift, else Ctrl, else Tab (a held key equal to the pressed one does not count)
            KeyCode mod = KeyCode.None;
            if (pressed != KeyCode.LeftAlt && keyboard.altKey.isPressed) mod = KeyCode.LeftAlt;
            else if (pressed != KeyCode.LeftShift && keyboard.shiftKey.isPressed) mod = KeyCode.LeftShift;
            else if (pressed != KeyCode.LeftControl && keyboard.ctrlKey.isPressed) mod = KeyCode.LeftControl;
            else if (pressed != KeyCode.Tab && keyboard.tabKey.isPressed) mod = KeyCode.Tab;
            int code = ClassifyScKey(ref pressed, ref mod);
            string actionId = ScSelectedActionId();
            if (actionId == "interface.chatMode" && code != 1) code = ChatModeScCode(pressed, mod, code);
            switch (code) {
                case 2:
                    ScNotice(0x5cf, "Unable to specify a single key.");
                    return;
                case 3:
                    ScNotice(0x5d0, "Unable to specify the key assigned.");
                    return;
                case 4:
                    var chord = new RoKeyChord(pressed, ScModifierOf(mod));
                    string other = ScConflict(actionId, chord);
                    if (other == null) {
                        StageScBinding(chord);
                        return;
                    }

                    // MsgStr 0x5d1 "Duplicated with [[%s]]. Do you still want to change?" — Yes clears that row
                    string ask = RoClassicUiHost.MsgStr(0x5d1, "Duplicated with [[%s]]. Do you still want to change?").Replace("%s", ScActionName(other));
                    RoClassicMessageBox.Show(ask, RoClassicMessageBox.TypeOkCancel, true, result => {
                        if (result != RoClassicMessageBox.IdYes || !classicScShown) return;
                        scPending[other] = RoKeyChord.None;
                        scPending[actionId] = chord;
                        classicSc.Invalidate();
                    });
                    return;
            }
        }

        /// <summary>0x9bc200: 0 clear (Esc), 2 a modifier alone, 3 a forbidden Tab pairing, 4 a key to bind (two modifiers normalised).</summary>
        private static int ClassifyScKey(ref KeyCode key, ref KeyCode mod) {
            if (mod == KeyCode.None) return IsScModifier(key) ? 2 : 4;
            if (key == KeyCode.Tab) return mod == KeyCode.LeftControl ? 4 : 3;
            if (key == KeyCode.LeftAlt && mod == KeyCode.Tab) return 3;
            if (key == KeyCode.LeftShift) {
                if (mod == KeyCode.Tab) return 3;
                (key, mod) = (mod, key);
                return 4;
            }

            if (key == KeyCode.LeftAlt) {
                if (mod == KeyCode.LeftControl || mod == KeyCode.LeftShift) (key, mod) = (mod, key);
                return 4;
            }

            if (key == KeyCode.LeftControl) {
                if (mod == KeyCode.Tab || mod == KeyCode.LeftShift) (key, mod) = (mod, key);
                return 4;
            }

            return 4;
        }

        /// <summary>Chatmode On/Off only: no modifiers, no Space / NumLock alone, only Tab, Enter, Ins or F1–F12.</summary>
        private static int ChatModeScCode(KeyCode key, KeyCode mod, int code) {
            if (mod != KeyCode.None || key == KeyCode.LeftAlt || key == KeyCode.LeftControl || key == KeyCode.LeftShift) return 3;
            if (key == KeyCode.Space || key == KeyCode.Numlock) return 2;
            if (key == KeyCode.Tab || key == KeyCode.Return || key == KeyCode.Insert || (key >= KeyCode.F1 && key <= KeyCode.F12)) return code;
            return 3;
        }

        private string ScSelectedActionId() {
            int index = ScPage * (scPageNo - 1) + scSelected;
            IReadOnlyList<string> table = ScTable;
            return index >= 0 && index < table.Count ? table[index] : null;
        }

        /// <summary>0x8761e0: the row already holding (key, mod) in any table — the Skill Bar and Hotkey Bar 2 are not compared.</summary>
        private string ScConflict(string actionId, RoKeyChord chord) {
            if (settings == null || actionId == null) return null;
            string own = ScCategory;
            foreach ((_, _, string category, _) in ScTabs) {
                bool bars = (own == RoClientSettingsModel.CategorySkillBar && category == RoClientSettingsModel.CategoryHotkeyBar2) ||
                    (own == RoClientSettingsModel.CategoryHotkeyBar2 && category == RoClientSettingsModel.CategorySkillBar);
                if (bars) continue;
                foreach (string other in settings.ShortcutOrder(category)) {
                    if (!string.Equals(other, actionId, StringComparison.OrdinalIgnoreCase) && ScBinding(other) == chord) return other;
                }
            }

            return null;
        }

        private void StageScBinding(RoKeyChord chord) {
            string actionId = ScSelectedActionId();
            if (actionId == null) return;
            scPending[actionId] = chord;
            scNotice = null;
            classicSc.Invalidate();
        }

        private void ScNotice(int msgId, string fallback) {
            scNotice = RoClassicUiHost.MsgStr(msgId, fallback);
            scNoticeColor = ScYellow;
        }

        /// <summary>
        /// The notice balloon (MsgStr 0x5ce, wrapped at 60 bytes) centred on the window one row above the selected row,
        /// and the hovered row's full action name as the tooltip.
        /// </summary>
        private void ShowScBalloons() {
            if (classicSc == null) return;
            Vector2Int at = classicSc.Position;
            if (scSelected >= 0) {
                string text = scNotice ?? RoClassicUiHost.MsgStr(0x5ce, "Press a key to assign. Pressing 'ESC' will remove the assigned key.");
                RoClassicTooltip.Named("hotkeynotice").Show(text, scNotice != null ? scNoticeColor : ScWhite, at.x, at.y + 40 + 20 * (scSelected % ScRows) - 20, 60, ScW);
            }

            if (system.Windows.HoverWindow != classicSc) return;
            int row = ScRowAt(system.Windows.Mouse - at);
            if (row < 0) return;
            string actionId = ScTable[ScPage * (scPageNo - 1) + row];
            RoClassicTooltip.Shared.Show(ScActionName(actionId), ScWhite, at.x + 218 * (row / ScRows) + 20, at.y + 40 + 20 * (row % ScRows) - 20);
        }

        // ---- diagnostics console (RoSkillSweepRunner "!sc ...") ----

        /// <summary>open | tab t | press x y | key name [mod] | button id | state.</summary>
        internal static string Sweep(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length ? int.Parse(a[i].StartsWith("0x") ? a[i].Substring(2) : a[i],
                a[i].StartsWith("0x") ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture) : 0;
            if (a[0] == "open") {
                if (!IsOpen) Open(RoClassicUiHost.ClientSettings);
            } else if (instance != null && instance.classicSc != null) {
                RoClassicShortcutSettings sc = instance;
                switch (a[0]) {
                    case "tab":
                        sc.SelectScTab(Arg(1));
                        break;
                    case "press":
                        sc.OnScPressed(new Vector2Int(Arg(1), Arg(2)));
                        break;
                    case "button":
                        sc.OnScButtonClicked(new RoClassicChildLayout { Id = Arg(1) });
                        break;
                    case "key":
                        // key <KeyCode> [Alt|Ctrl|Shift|Tab]: as if pressed now
                        if (sc.scSelected >= 0 && Enum.TryParse(a[1], true, out KeyCode key)) {
                            KeyCode mod = a.Length > 2 ? (a[2] == "Alt" ? KeyCode.LeftAlt : a[2] == "Ctrl" ? KeyCode.LeftControl : a[2] == "Shift" ? KeyCode.LeftShift : KeyCode.Tab) : KeyCode.None;
                            if (key == KeyCode.Escape) {
                                sc.StageScBinding(RoKeyChord.None);
                                break;
                            }

                            int code = ClassifyScKey(ref key, ref mod);
                            if (sc.ScSelectedActionId() == "interface.chatMode") code = ChatModeScCode(key, mod, code);
                            if (code == 2) sc.ScNotice(0x5cf, "Unable to specify a single key.");
                            else if (code == 3) sc.ScNotice(0x5d0, "Unable to specify the key assigned.");
                            else if (code == 4) {
                                var chord = new RoKeyChord(key, ScModifierOf(mod));
                                string other = sc.ScConflict(sc.ScSelectedActionId(), chord);
                                if (other == null) sc.StageScBinding(chord);
                                else sc.scNotice = "conflict with " + other;
                            }
                        }

                        break;
                }
            }

            RoClassicShortcutSettings s0 = instance;
            if (s0 == null) return "shown=False (open it: !sc open, or from the Esc menu: !esc click 0x16e)";
            var s = new StringBuilder();
            s.Append("shown=").Append(s0.classicScShown).Append(" tab=").Append(s0.scTab).Append(" page=").Append(s0.scPageNo).Append('/').Append(s0.ScPages)
                .Append(" rows=").Append(s0.ScTable.Count).Append(" sel=").Append(s0.scSelected);
            if (s0.scSelected >= 0) s.Append(' ').Append(s0.ScSelectedActionId()).Append('=').Append(ScKeyText(s0.ScBinding(s0.ScSelectedActionId())));
            s.Append(" notice='").Append(s0.scNotice ?? string.Empty).Append("' pending=").Append(s0.scPending.Count);
            if (s0.classicSc != null) s.Append(" pos=").Append(s0.classicSc.Position);
            return s.ToString();
        }
    }
}
