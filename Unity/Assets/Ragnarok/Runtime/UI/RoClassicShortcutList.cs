using System;
using System.Globalization;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UIEmotionWnd 0x56, the Shortcut List (codex-ui-research RUNTIME-RULES-QUESTS-EMOTIONS-NOTIFICATIONS.md "Static geometry
    /// and rendering"): 375 × 260 at ((W − 640) / 2 + 185, H·300/480 − 60), title MsgStr 0x23e, ten 270 × 16 edits at
    /// (83, 20 + 21i) holding up to 50 bytes with "Alt + 1" … "Alt + 0" beside them at (20, 23 + 21i), close 0xc5 and the
    /// view button 0x117 (btn_view: the cash emotion list 0x57 made or deleted, RoClassicEmojiList). Alt + digit sends the
    /// stored line through the chat (FUN_009d6290: a leading '/' is a command). The lines are kept per player.
    /// </summary>
    public sealed class RoClassicShortcutList : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const string Prefs = "ro.ui.classic.shortcuts.";
        private const int Width = 375;
        private const int Height = 260;
        private const int CloseId = 0xc5;
        private const int ViewId = 0x117;
        private const int Lines = 10;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 EditFill = new Color32(232, 232, 232, 255);

        private static RoClassicShortcutList instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private readonly RoClassicEditField[] edits = new RoClassicEditField[Lines];

        private static RoClassicShortcutList Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicShortcutList { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>The stored line of a shortcut (0 = Alt + 1 … 9 = Alt + 0).</summary>
        public static string Line(int index) => index < 0 || index >= Lines ? string.Empty : PlayerPrefs.GetString(Prefs + index.ToString(CultureInfo.InvariantCulture), string.Empty);

        /// <summary>Whether the window exists (DAT_01273260, which CCashEmotionAction_Register checks).</summary>
        public static bool IsOpen => instance?.window != null && instance.window.Visible;

        /// <summary>
        /// 0x804350(text, −1): the line whose edit has the focus gets the text (SetText); nothing when none of the ten has it.
        /// </summary>
        public static void SetFocusedLine(string text) {
            RoClassicShortcutList s = instance;
            if (s?.window == null || !s.window.Visible) return;
            int line = Array.IndexOf(s.edits, RoClassicTextInput.Focused);
            if (line < 0) return;
            s.edits[line].SetText(text ?? string.Empty);
            Save(line, s.edits[line].Text);
            s.window.Invalidate();
        }

        /// <summary>Interface action 14 (Alt + M; FUN_009d4010 case 0x72): opens the list, or closes it when open.</summary>
        public static bool Toggle() {
            RoClassicShortcutList s = Ensure();
            if (s == null) return false;
            if (s.window != null && s.window.Visible) s.Close();
            else s.Open();
            return true;
        }

        /// <summary>Actions 0xc8..0xd1 (FUN_009d6290): the line goes out like typed chat; an empty line does nothing.</summary>
        public static bool Send(int index, RoClassicChat chat) {
            string line = Line(index);
            if (line.Trim().Length == 0 || chat == null) return false;
            chat.SendClassicRoomLine(line);
            return true;
        }

        private void Open() {
            if (window == null) Build();
            for (int i = 0; i < Lines; i++) edits[i].SetText(Line(i));
            Vector2Int client = system.ClientSize;
            int x = PlayerPrefs.GetInt(Prefs + "x", (client.x - 640) / 2 + 185), y = PlayerPrefs.GetInt(Prefs + "y", client.y * 300 / 480 - 60);
            window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - Height)));
            window.Visible = true;
            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
            edits[0].Focus();
        }

        private void Build() {
            var layout = new RoClassicWindowLayout { Class = "UIEmotionWnd", WindowId = 0x56, Width = Width, Height = Height };
            RoClassicFrames.TitleBar(layout.Ops, Width, false);
            RoClassicFrames.Body(layout.Ops, Width, Height);
            var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = Width - 14, Y = 3, W = 11, H = 11 };
            close.States["normal"] = Basic + "sys_close_off.bmp";
            close.States["hover"] = Basic + "sys_close_on.bmp";
            close.States["pressed"] = Basic + "sys_close_on.bmp";
            layout.Children.Add(close);
            var view = new RoClassicChildLayout { Class = "UIBitmapButton", Id = ViewId, Path = "view", X = Width - 50, Y = Height - 25, W = 42, H = 20 };
            view.States["normal"] = Ui + "btn_view.bmp";
            view.States["hover"] = Ui + "btn_view_a.bmp";
            view.States["pressed"] = Ui + "btn_view_b.bmp";
            layout.Children.Add(view);
            window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == CloseId) Close();
                else if (child.Id == ViewId) RoClassicEmojiList.Toggle(null);   // 0x117: DeleteWindow(0x57) when it exists, else MakeWindow(0x57)
            };
            window.EscClose = () => {
                Close();
                return true;
            };
            window.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "y", w.Position.y);
            };
            for (int i = 0; i < Lines; i++) {
                int line = i;
                var edit = new RoClassicEditField { Rect = new RectInt(83, 20 + 21 * i, 270, 16), Background = EditFill, MaxBytes = 50 };
                edit.Changed += field => Save(line, field.Text);
                // UIEditCtrl2: Enter moves to the next line
                edit.Submitted += _ => edits[(line + 1) % Lines].Focus();
                edits[i] = edit;
                window.AddEditField(edit);
            }
        }

        private static void Save(int index, string text) {
            PlayerPrefs.SetString(Prefs + index.ToString(CultureInfo.InvariantCulture), text ?? string.Empty);
        }

        /// <summary>vf20 0x804230: the title (MsgStr 0x23e) and the "Alt + n" labels.</summary>
        private void Draw(RoClassicWindow w) {
            string title = RoClassicUiHost.MsgStr(0x23e, "Shortcut List");
            w.TextOut(17, 3, title, 0, 12, White);
            w.TextOut(16, 2, title, 0, 12, Black);
            for (int i = 0; i < Lines; i++) {
                w.TextOut(20, 23 + 21 * i, "Alt + " + ((i + 1) % 10).ToString(CultureInfo.InvariantCulture), 0, 12, Black);
            }
        }

        private void Close() {
            if (window == null || !window.Visible) return;
            PlayerPrefs.Save();
            if (Array.IndexOf(edits, RoClassicTextInput.Focused) >= 0) RoClassicTextInput.Focus(null);
            window.Visible = false;
            system.Windows.RemoveEsc(window);
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
        }

        /// <summary>The sweep console: toggle | set i text | send i | state.</summary>
        public static string Sweep(string command, RoClassicChat chat) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            switch (a[0]) {
                case "toggle":
                    Toggle();
                    break;
                case "set":
                    Save(Arg(1), a.Length > 2 ? string.Join(" ", a, 2, a.Length - 2) : string.Empty);
                    if (instance?.window != null && instance.window.Visible) instance.edits[Arg(1)].SetText(Line(Arg(1)));
                    break;
                case "send":
                    Send(Arg(1), chat);
                    break;
            }

            var s = new System.Text.StringBuilder();
            s.Append("open=").Append(instance?.window != null && instance.window.Visible).Append(" lines=");
            for (int i = 0; i < Lines; i++) s.Append(i).Append(":'").Append(Line(i)).Append("' ");
            return s.ToString().TrimEnd();
        }
    }
}
