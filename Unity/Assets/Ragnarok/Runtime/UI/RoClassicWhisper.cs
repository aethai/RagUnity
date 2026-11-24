using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The 1:1 whisper windows (UIWhisperWnd, window id 0x43; scratchpad whisper-spec): one per partner name (exact,
    /// case-sensitive), any number of them, made only by the window manager's msg 0xe (0x9d9c3e) - an incoming whisper or
    /// the own whisper's success ack with the auto-open options, or a menu "Open 1:1 Chat". 280×120 by default, resized
    /// in 32 px steps within 280..512 × 120..376; a translucent body (0x66000000) under a #9EB6E4 title band (alpha 0x44),
    /// "With  name [code]" in white at (10, 3), a #C0C0C0 rounded outline and the line over the edit; the history
    /// (1, 17) (W−2)×(H−38) - lines 14 px apart at (2, 3) without a shadow, wrapped by characters at the box width − 16,
    /// the own lines #FFFF00, the partner's #B6DDE8, 240 kept; the edit (3, H−18) (W−30)×16, 255 bytes, white on the
    /// window. Enter whispers the partner (no local echo: the line comes with the ack), a "/" line is a command or chat.
    /// The rect (WHISPERWNDINFO) is one record: restored for each new window, cascaded 17 px per open window, written
    /// back by the last one closed. History goes with the window.
    /// </summary>
    public sealed class RoClassicWhisper {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string Prefs = "ro.ui.classic.whisper.";
        private const int CloseId = 0xc5;
        private const int LineH = 14, MaxLines = 240, MinW = 280, MinH = 120;
        private static readonly Color32 Outline = new Color32(0xc0, 0xc0, 0xc0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Body = new Color32(0, 0, 0, 0x66);
        private static readonly Color32 Band = new Color32(0x9e, 0xb6, 0xe4, 0x44);
        public static readonly Color32 OwnColor = new Color32(0xff, 0xff, 0x00, 255);       // 0x7800 → COLORREF 0x00FFFF
        public static readonly Color32 PartnerColor = new Color32(0xb6, 0xdd, 0xe8, 255);   // 0x222222 → COLORREF 0xE8DDB6

        private static readonly Dictionary<string, RoClassicWhisper> open = new Dictionary<string, RoClassicWhisper>(StringComparer.Ordinal);
        private static RoLoginShellSession session;
        private static RoClassicChat chat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            open.Clear();
            session = null;
            chat = null;
        }

        private readonly RoClassicWindow window;
        private readonly RoClassicEditField edit;
        private readonly string partner;
        private readonly string title;
        private readonly string code;
        private readonly List<(string Text, Color32 Color)> lines = new List<(string, Color32)>();
        private readonly List<(string Text, Color32 Color)> wrapped = new List<(string, Color32)>();
        private int wrappedWidth = -1, wrappedCount = -1, first;

        /// <summary>The session that sends the whispers and the main chat the command lines go to.</summary>
        public static void Bind(RoLoginShellSession loginSession, RoClassicChat chatWindow) {
            session = loginSession;
            if (chatWindow != null) chat = chatWindow;
        }

        public static bool IsOpen(string name) => name != null && open.ContainsKey(name);

        public static int Count => open.Count;

        /// <summary>
        /// Window manager msg 0xe: a window for <paramref name="name"/> unless one exists (then nothing at all happens);
        /// <paramref name="tag"/> ("Friend" / "GuildMember" or null) goes into the title, <paramref name="ccode"/> after it.
        /// True when a window was made.
        /// </summary>
        public static bool Open(string name, string tag, string ccode) {
            if (string.IsNullOrEmpty(name) || open.ContainsKey(name)) return false;
            RoClassicUiSystem system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
            if (system == null) return false;
            var w = new RoClassicWhisper(system, name, tag, ccode);
            open.Add(name, w);
            // 0x9d9f7e: the partner joins the main chat's whisper-name list
            (chat != null ? chat : RoClassicChat.Instance)?.AddClassicWhisperName(name);
            return true;
        }

        /// <summary>
        /// A menu's "Open 1:1 Chat" (player menu 0xc161e7, messenger, party, guild member list): the window with the friend /
        /// guild member tag and the C-Code of <paramref name="id"/>.
        /// </summary>
        public static bool OpenManual(string name, uint id) {
            name = name?.Trim();
            return !string.IsNullOrEmpty(name) && Open(name, Tag(name, id), CCode(id));
        }

        /// <summary>The title tag: MsgStr 0x66 "Friend" for a friend, "GuildMember" for a guild member, else none.</summary>
        public static string Tag(string name, uint id) {
            if (IsFriend(name)) return RoClassicUiHost.MsgStr(0x66, "Friend");
            return IsGuildMember(name, id) ? "GuildMember" : null;
        }

        /// <summary>A guild member by char id (0xd01240), or by name when there is no id (0xd00fa0).</summary>
        public static bool IsGuildMember(string name, uint id) {
            if (session == null) return false;
            RoClassicGuildState guild = session.GuildSnapshot().Guild;
            if (guild == null) return false;
            foreach (RoGuildMember member in guild.Members) {
                if (id != 0 ? member.CharId == id : member.Name == name) return true;
            }

            return false;
        }

        /// <summary>
        /// Msg 0x25 to the partner's window (when there is one): the line in the own or the partner's colour, then the
        /// window raised. False when no window is open for the name (the caller writes the main chat line).
        /// </summary>
        public static bool AddLine(string name, string text, bool own) {
            if (name == null || !open.TryGetValue(name, out RoClassicWhisper w)) return false;
            w.Add(text, own ? OwnColor : PartnerColor);
            RoClassicUiSystem.Instance?.Windows.BringToFront(w.window);
            return true;
        }

        /// <summary>
        /// The C-Code (0xce6100) of an id: its decimal digits through 0→3 1→8 2→6 3→7 4→0 5→1 6→2 7→4 8→9 9→5, a '-'
        /// before the hundreds digit; "" for 0.
        /// </summary>
        public static string CCode(uint id) {
            if (id == 0) return string.Empty;
            const string map = "3867012495";
            string digits = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var s = new StringBuilder(digits.Length + 1);
            for (int i = 0; i < digits.Length; i++) {
                if (i == digits.Length - 3 && i > 0) s.Append('-');
                s.Append(map[digits[i] - '0']);
            }

            return s.ToString();
        }

        /// <summary>Every window goes (a map change back to the login).</summary>
        public static void CloseAll() {
            var all = new List<RoClassicWhisper>(open.Values);
            foreach (RoClassicWhisper w in all) w.Close();
        }

        private RoClassicWhisper(RoClassicUiSystem system, string name, string tag, string ccode) {
            partner = name;
            code = ccode ?? string.Empty;
            title = string.IsNullOrEmpty(tag) ? name : name + "  (" + tag + ")  *^_^*";
            var layout = new RoClassicWindowLayout { Class = "UIWhisperWnd", WindowId = 0x43, Width = MinW, Height = MinH };
            var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = MinW - 14, Y = 3, W = 11, H = 11, Tooltip = "/q" };
            close.States["normal"] = Basic + "sys_close_off.bmp";
            close.States["hover"] = Basic + "sys_close_on.bmp";
            close.States["pressed"] = Basic + "sys_close_on.bmp";
            layout.Children.Add(close);
            window = new RoClassicWindow(system, layout);
            window.DrawUnderlay += DrawBoxes;
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == CloseId) Close();
            };
            window.Wheeled += (w, local, delta) => Scroll(delta > 0 ? -3 : 3);
            window.DoubleClicked += (w, local) => {
                // vf26 0x8535c0: the title band copies the C-Code
                if (local.y >= 17 || code.Length == 0) return;
                GUIUtility.systemCopyBuffer = code;
                chat?.AddLine("C-Code copied to clipboard", "#FFFF64");
            };
            window.RightClicked += (w, local) => OpenMenu(system.Windows.Mouse);
            window.ResizerModeAt = local =>
                local.x >= window.Width - 15 && local.x < window.Width + 1 && local.y >= window.Height - 15 && local.y < window.Height + 1 ? 7 : -1;
            window.ResizeRequested += (w, mode, width, height) => {
                if (mode == 7) Resize(width, height);
            };
            // no EscClose: 0x43 is not on the Esc list (0x9c71c0) and neither the edit nor CLanguage takes 0x1b, so Esc goes on
            // to the next closable window (or the game menu) whether or not this edit has the focus
            edit = new RoClassicEditField { Rect = new RectInt(3, MinH - 18, MinW - 30, 16), MaxBytes = 255, TextColor = White };
            edit.Submitted += _ => Send();
            window.AddEditField(edit);

            // msg 0x22: WHISPERWNDINFO, else (0, 347, 280, 120); then the cascade by the windows already open
            Vector2Int screen = system.ClientSize;
            int x = PlayerPrefs.GetInt(Prefs + "x", 0), y = PlayerPrefs.GetInt(Prefs + "y", 347);
            int ww = PlayerPrefs.GetInt(Prefs + "w", MinW), wh = PlayerPrefs.GetInt(Prefs + "h", MinH);
            if (ww < MinW || wh < MinH || x < 0 || y < 0 || x > screen.x - ww || y > screen.y - wh) {
                x = 0;
                y = 347;
                ww = MinW;
                wh = MinH;
            }

            Resize(ww, wh);
            int off = 17 * open.Count;
            if (x + off > 0 && window.Width + x + off < screen.x - 17 && y + off > 0 && window.Height + y + off < screen.y - 17) {
                x += off;
                y += off;
            } else if (off > 0 && window.Width + off < screen.x - 17 && window.Height + off < screen.y - 17) {
                x = off;
                y = off;
            }

            window.MoveTo(x, y);
            window.Visible = true;
            system.Windows.BringToFront(window);
            edit.Focus();
        }

        /// <summary>Msg 0xe mode 7: 280..512 × 120..384 in 32 px steps; the edit, the X and the resizer follow.</summary>
        private void Resize(int width, int height) {
            int w = Math.Max(MinW, Math.Min(512, width)), h = Math.Max(MinH, Math.Min(384, height));
            w = MinW + (w - MinW) / 32 * 32;
            h = MinH + (h - MinH) / 32 * 32;
            window.Layout.Width = w;
            window.Layout.Height = h;
            window.SetHeight(h);
            window.HitArea = new RectInt(0, 0, w, h);
            window.SetChildPosition("close", w - 14, 3);
            edit.Rect = new RectInt(3, h - 18, w - 30, 16);
            wrappedWidth = -1;
            window.Invalidate();
        }

        private int Rows => Math.Max(1, (window.Height - 38) / LineH);

        private void Add(string text, Color32 color) {
            lines.Add((text ?? string.Empty, color));
            if (lines.Count > MaxLines) lines.RemoveAt(0);
            window.Invalidate();
        }

        /// <summary>PixelWrapAdd 0x9b27a0: by characters at the box width − 16; the view follows the newest line when at the bottom.</summary>
        private void Rewrap(RoClassicGdiFont font) {
            int width = window.Width - 2 - 16;
            if (wrappedWidth == width && wrappedCount == lines.Count && (lines.Count < MaxLines)) return;
            bool atBottom = first >= Math.Max(0, wrapped.Count - Rows);
            wrapped.Clear();
            foreach ((string text, Color32 color) in lines) {
                string rest = text;
                while (rest.Length > 0) {
                    int n = rest.Length;
                    while (n > 1 && font.Measure(rest.Substring(0, n)) > width) n--;
                    wrapped.Add((rest.Substring(0, n), color));
                    rest = rest.Substring(n);
                }
            }

            wrappedWidth = width;
            wrappedCount = lines.Count;
            int max = Math.Max(0, wrapped.Count - Rows);
            first = atBottom ? max : Mathf.Clamp(first, 0, max);
        }

        private void Scroll(int delta) {
            int max = Math.Max(0, wrapped.Count - Rows);
            int next = Mathf.Clamp(first + delta, 0, max);
            if (next == first) return;
            first = next;
            window.Invalidate();
        }

        /// <summary>vf43 0x82d5e0: the body and the title band, translucent under the window's pixels.</summary>
        private void DrawBoxes(RoClassicWindow w) {
            w.FillRect(1, 1, w.Width - 2, w.Height, Body);
            w.FillRect(1, 1, w.Width - 2, 17, Band);
        }

        /// <summary>OnDraw 0x852d40 plus the history box 0x7ea6d0, its scroll bar and the resizer.</summary>
        private void Draw(RoClassicWindow w) {
            int W = w.Width, H = w.Height;
            string text = code.Length == 0 ? "With  " + title : "With  " + title + " [" + code + "]";
            w.RoundOutline(0, 0, W, H, Outline);
            w.FillRect(1, H - 20, W - 2, 1, Outline);
            w.TextOut(10, 3, text, 0, 12, White);

            RoClassicGdiFont font = w.FontFor(0, 12);
            Rewrap(font);
            int rows = Rows;
            for (int r = 0; r < rows && first + r < wrapped.Count; r++) {
                (string line, Color32 color) = wrapped[first + r];
                w.TextOut(3, 20 + LineH * r, line, 0, 12, color);
            }

            if (wrapped.Count > rows) w.DrawScrollBar(W - 14, 17, H - 38, wrapped.Count - rows, wrapped.Count, rows, first);
            w.DrawBitmapRaw(W - 15, H - 15, Ui + "btn_resize.bmp");
        }

        /// <summary>
        /// Msg 6 / 0xb4: a line not starting with '/' whispers the partner (the echo comes with the ack); "/q" closes the
        /// window (ParseChat 0x18, the X's tooltip); any other '/' line goes out as the main chat would send it.
        /// </summary>
        private void Send() {
            string text = edit.Text;
            edit.SetText(string.Empty);
            edit.Focus();
            if (text.Length == 0) return;
            if (text[0] != '/') {
                if (session != null) _ = session.BeginSendPrivateMessage(partner, text);
                return;
            }

            if (text.Trim() == "/q") {
                Close();
                return;
            }

            chat?.SendClassicRoomLine(text);
        }

        /// <summary>RBtnUp 0x856110: "Save Chat as Text File", and "Register as a Friend" for a stranger.</summary>
        private void OpenMenu(Vector2Int mouse) {
            bool stranger = !IsFriend(partner);
            RoClassicMenu.Begin();
            RoClassicMenu.Add(RoClassicUiHost.MsgStr(0x138, "Save Chat as Text File"));
            if (stranger) RoClassicMenu.Add(RoClassicUiHost.MsgStr(0x166, "Register as a Friend"));
            RoClassicMenu.Open(mouse.x + 1, mouse.y + 1, pick => {
                if (pick == 0) SaveChat();
                else if (pick == 1 && stranger && session != null) _ = session.BeginLegacyAddFriend(partner);
            });
        }

        public static bool IsFriend(string name) {
            if (session == null) return false;
            foreach (RoFriendRuntimeSnapshot friend in session.FriendsSnapshot()) {
                if (friend.Name == name) return true;
            }

            return false;
        }

        /// <summary>0x86ad20: Whisper\&lt;partner&gt;.txt (bad characters → '_', a %03d suffix when it exists), then "&lt;path&gt; is Saved." in green.</summary>
        private void SaveChat() {
            try {
                var name = new StringBuilder();
                foreach (char ch in partner) name.Append("\\/:*?\"<>|".IndexOf(ch) >= 0 || Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
                string dir = Path.Combine(Application.persistentDataPath, "Whisper");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, name + ".txt");
                for (int i = 1; File.Exists(path) && i < 1000; i++) path = Path.Combine(dir, name + i.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".txt");
                var text = new StringBuilder();
                foreach ((string line, Color32 _) in lines) text.AppendLine(line);
                File.WriteAllText(path, text.ToString());
                chat?.AddLine(path + " is Saved.", "#00FF00");
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }

        /// <summary>StoreInfo 0x86a680 + the deferred delete: the rect becomes the next window's default; the history goes.</summary>
        private void Close() {
            PlayerPrefs.SetInt(Prefs + "x", window.Position.x);
            PlayerPrefs.SetInt(Prefs + "y", window.Position.y);
            PlayerPrefs.SetInt(Prefs + "w", window.Width);
            PlayerPrefs.SetInt(Prefs + "h", window.Height);
            if (RoClassicTextInput.Focused == edit) RoClassicTextInput.Focus(null);
            open.Remove(partner);
            window.Destroy();
        }

        // ---- diagnostics (RoSkillSweepRunner "!wh ...") ----

        /// <summary>open name | say name text | close name | state.</summary>
        internal static string Sweep(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 3);
            RoClassicWhisper w = a.Length > 1 && open.TryGetValue(a[1], out RoClassicWhisper found) ? found : null;
            switch (a[0]) {
                case "open":
                    if (a.Length > 1) Open(a[1], null, string.Empty);
                    break;
                case "say":
                    if (w != null && a.Length > 2) {
                        w.edit.SetText(a[2]);
                        w.Send();
                    }

                    break;
                case "close":
                    w?.Close();
                    break;
            }

            var s = new StringBuilder("open=" + open.Count);
            foreach (RoClassicWhisper x in open.Values) {
                s.Append(" [").Append(x.partner).Append(" at ").Append(x.window.Position).Append(' ').Append(x.window.Width).Append('x').Append(x.window.Height)
                    .Append(" lines=").Append(x.lines.Count);
                if (x.lines.Count > 0) s.Append(" last='").Append(x.lines[x.lines.Count - 1].Text).Append('\'');
                s.Append(']');
            }

            return s.ToString();
        }
    }
}
