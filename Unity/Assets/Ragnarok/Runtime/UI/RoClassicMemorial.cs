using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UIMemorialDunWnd 0x89 (session-windows-spec §2), 200 × 150: ZC_MEMORIALDUNGEON_SUBSCRIPTION_INFO 0x02cb (standby,
    /// state 0), _SUBSCRIPTION_NOTIFY 0x02cc (priority; &lt; 0 cancels), _INFO 0x02cd (enter limit → state 1 "Available", else
    /// state 2 "in Progress") and _NOTIFY 0x02ce (0 like 0x02cd; 1/2/3 destroyed with MSI 0x542..0x544; 4 silent). Dates
    /// are shown as local MM/DD HH:MM, nothing counts down. The party leader's "Destroy instance" (state 1) sends
    /// CZ_MEMORIALDUNGEON_COMMAND 0x02cf {3} at once. Alt+B shows / hides it; Esc does not close it; the X hides it (in
    /// progress: until the next instance).
    /// </summary>
    public sealed class RoClassicMemorial {
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const int W = 200, H = 150, CloseId = 0xc5;
        private const string Prefs = "ro.ui.classic.memorial.";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Outline = new Color32(0x8e, 0x8e, 0x8e, 255);
        private static RoClassicMemorial instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private string name = string.Empty;
        private int state, priority, destroyDate, enterLimit;
        private bool visible;   // 0x1553274
        private bool destroyPressed;
        private bool wasInParty;

        private static RoClassicMemorial Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicMemorial { system = system };
            return instance;
        }

        public static bool Handles(ushort id) => id >= 0x02cb && id <= 0x02ce;

        public static void Handle(byte[] b) {
            RoClassicMemorial m = Ensure();
            if (m == null || b == null || b.Length < 4) return;
            switch (BitConverter.ToUInt16(b, 0)) {
                case 0x02cb when b.Length >= 65:
                    // 0xc53970
                    m.name = Text(b, 2, 61);
                    m.priority = BitConverter.ToInt16(b, 63);
                    m.state = 0;
                    m.Make();
                    break;
                case 0x02cc:
                    // 0xc539e0
                    m.priority = BitConverter.ToInt16(b, 2);
                    if (!m.Make()) break;
                    if (m.priority >= 0) {
                        m.state = 0;
                        m.window.Invalidate();
                    } else {
                        m.Delete();
                        Chat(Msg(0x541, "The Memorial Dungeon reservation has been canceled."));
                    }

                    break;
                case 0x02cd when b.Length >= 71:
                    // 0xc53800
                    m.name = Text(b, 2, 61);
                    m.destroyDate = BitConverter.ToInt32(b, 63);
                    m.enterLimit = BitConverter.ToInt32(b, 67);
                    m.Info();
                    break;
                case 0x02ce when b.Length >= 10: {
                    // 0xc53890
                    int type = BitConverter.ToInt32(b, 2);
                    m.enterLimit = BitConverter.ToInt32(b, 6);
                    if (type == 0) {
                        m.Info();
                        break;
                    }

                    m.Delete();
                    if (type >= 1 && type <= 3) {
                        string[] texts = {
                            "The Memorial Dungeon duration expired; it has been destroyed.",
                            "The Memorial Dungeon's entry time limit expired; it has been destroyed.", "The Memorial Dungeon has been removed."
                        };
                        Chat(Msg(0x541 + type, texts[type - 1]));
                    }

                    break;
                }
            }
        }

        /// <summary>The state from the enter limit; a repeated "in progress" for a hidden window keeps it hidden.</summary>
        private void Info() {
            int next = enterLimit != 0 ? 1 : 2;
            if (state == 2 && next == 2 && !visible) return;
            state = next;
            Make();
        }

        /// <summary>MakeWindow(0x89) (case 0x9cdce3): nothing without a name; else 200 × 150 at the saved place (first (0, 0)).</summary>
        private bool Make() {
            if (string.IsNullOrEmpty(name)) return false;
            if (window == null) {
                var layout = new RoClassicWindowLayout { Class = "UIMemorialDunWnd", WindowId = 0x89, Width = W, Height = H };
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = 186, Y = 3, W = 11, H = 11, Tooltip = "Alt+B" };
                close.States["normal"] = Basic + "sys_close_off.bmp";
                close.States["hover"] = close.States["pressed"] = Basic + "sys_close_on.bmp";
                layout.Children.Add(close);
                window = new RoClassicWindow(system, layout) { Visible = false };
                window.DrawDynamic += Draw;
                window.Clicked += child => {
                    if (child.Id != CloseId) return;
                    window.Visible = false;
                    if (state == 2) visible = false;
                };
                window.Moved += w => {
                    // vf04 0x87e3a0: off the right edge → x 0, off the bottom → y 310
                    Vector2Int c = system.ClientSize;
                    int x = w.Position.x > c.x - W ? 0 : w.Position.x, y = w.Position.y > c.y - H ? 310 : w.Position.y;
                    if (x != w.Position.x || y != w.Position.y) w.MoveTo(x, y);
                    PlayerPrefs.SetInt(Prefs + "x", x);
                    PlayerPrefs.SetInt(Prefs + "y", y);
                };
                window.Pressed += (w, p) => {
                    destroyPressed = DestroyShown && DestroyRect.Contains(p);
                    w.Invalidate();
                };
                window.Released += (w, p) => {
                    bool hit = destroyPressed && DestroyRect.Contains(p);
                    destroyPressed = false;
                    w.Invalidate();
                    // 0xcf: the leader's CZ_MEMORIALDUNGEON_COMMAND {command 3}, no confirmation
                    if (hit && IsLeader) _ = RoClassicEntryQueue.CurrentSession?.BeginClassicPacket(0x02cf, BitConverter.GetBytes(3), "a memorial dungeon destroy");
                };
                window.MoveTo(PlayerPrefs.GetInt(Prefs + "x", 0), PlayerPrefs.GetInt(Prefs + "y", 0));
            }

            window.Visible = true;
            visible = true;
            window.Invalidate();
            return true;
        }

        /// <summary>DeleteWindow(0x89) (0x87c830): the instance data reset, the window gone, the flag cleared.</summary>
        private void Delete() {
            name = string.Empty;
            priority = destroyDate = enterLimit = state = 0;
            visible = false;
            if (window != null) window.Visible = false;
        }

        /// <summary>Alt+B (hotkey action 22): an existing window shows / hides, else MakeWindow (nothing without an instance).</summary>
        public static bool Toggle() {
            RoClassicMemorial m = Ensure();
            if (m == null) return false;
            if (m.window != null && !string.IsNullOrEmpty(m.name)) {
                bool show = !m.window.Visible;
                if (show) m.window.CloseTabsSetShow(true);   // SetShow(1) also shows a window F11 hid
                m.window.Visible = show;
                return true;
            }

            m.Make();
            return true;
        }

        /// <summary>Per frame: leaving the party (ZC_DELETE_MEMBER_FROM_GROUP for me, 0xc488b0) deletes the window.</summary>
        public static void Tick(RoLoginShellSession session) {
            RoClassicMemorial m = instance;
            if (m == null || session == null) return;
            bool inParty = session.PartySnapshot().IsInParty;
            if (m.wasInParty && !inParty && !string.IsNullOrEmpty(m.name)) m.Delete();
            m.wasInParty = inParty;
        }

        private static bool IsLeader {
            get {
                RoLoginShellSession session = RoClassicEntryQueue.CurrentSession;
                if (session == null) return false;
                RoPartyRuntimeSnapshot party = session.PartySnapshot();
                return party.IsInParty && party.LeaderAccountId != 0 && party.LeaderAccountId == session.LocalActorId;
            }
        }

        private bool DestroyShown => IsLeader && state == 1;
        private static RectInt DestroyRect => new RectInt(41, 115, 125, 20);

        /// <summary>OnDraw 0x88d700.</summary>
        private void Draw(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            string title = Msg(0x538, "Memorial Dungeon");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            w.FillRect(0, 17, W, H - 19, White);
            w.FillRect(1, H - 2, W - 2, 1, White);
            w.FillRect(2, H - 1, W - 4, 1, White);
            string[] headers = { "%s in Standby", "%s Available", "%s in Progress" };
            if (state <= 2) Centred(w, Format(Msg(0x539 + state, headers[state]), "^4169E1[" + name + "]^000000"), 30);
            switch (state) {
                case 0:
                    Centred(w, Format(Msg(0x53e, "Your Standby Priority: ^ff0000%d^000000"), priority.ToString(CultureInfo.InvariantCulture)), (H - 17) / 2);
                    break;
                case 1:
                    if (enterLimit != 0) {
                        string s = Format(Msg(0x53f, "The requested dungeon will be removed if you do not enter within ^ff0000%s^000000."), Date(enterLimit));
                        List<string> lines = PixelWrap(w, s, W - 20);
                        if (lines.Count == 1) {
                            Centred(w, lines[0], 55);
                        } else {
                            Color32 color = Black;
                            int y0 = ((H - 17 * lines.Count - 72) >> 1) + 40;
                            for (int i = 0; i < lines.Count; i++) w.TextOutColored(10, y0 + 17 * i, lines[i], 0, lines[i].Length, 0, 12, ref color);
                        }
                    }

                    break;
                case 2:
                    if (destroyDate != 0) {
                        w.TextOut(10, 55, Msg(0x540, "Dungeon Mission Time Limit:"), 0, 12, Black);
                        Centred(w, "^ff0000" + Date(destroyDate) + "^ffffff", 75);
                    }

                    break;
            }

            // the 1-px rounded #8E8E8E outline
            foreach (RectInt r in new[] {
                         new RectInt(2, 0, W - 4, 1), new RectInt(1, 1, 1, 1), new RectInt(0, 2, 1, H - 4), new RectInt(1, H - 2, 1, 1),
                         new RectInt(W - 2, 1, 1, 1), new RectInt(W - 1, 2, 1, H - 4), new RectInt(2, H - 1, W - 4, 1), new RectInt(W - 2, H - 2, 1, 1)
                     }) {
                w.FillRect(r.x, r.y, r.width, r.height, Outline);
            }

            if (DestroyShown) RoClassicPreviewEquip.DrawBasicButton(w, 41, 115, 125, Msg(0xcc6, "Destroy instance"), destroyPressed);
        }

        private static void Centred(RoClassicWindow w, string s, int y) {
            Color32 color = Black;
            w.TextOutColored((W - Width(w, s)) / 2, y, s, 0, s.Length, 0, 12, ref color);
        }

        private static int Width(RoClassicWindow w, string s) => w.FontFor(0, 12).Measure(Plain(s));

        private static string Plain(string s) {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == '^' && i + 7 <= s.Length && IsHex(s, i + 1)) {
                    i += 6;
                    continue;
                }

                sb.Append(s[i]);
            }

            return sb.ToString();
        }

        private static bool IsHex(string s, int at) {
            for (int i = at; i < at + 6; i++) {
                if (!Uri.IsHexDigit(s[i])) return false;
            }

            return true;
        }

        /// <summary>Word wrap at <paramref name="width"/> pixels of the plain text, colour codes kept in the pieces.</summary>
        private static List<string> PixelWrap(RoClassicWindow w, string s, int width) {
            var lines = new List<string>();
            string[] words = s.Split(' ');
            var line = new StringBuilder();
            foreach (string word in words) {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Width(w, candidate) > width) {
                    lines.Add(line.ToString());
                    line.Clear().Append(word);
                } else {
                    line.Clear().Append(candidate);
                }
            }

            if (line.Length > 0) lines.Add(line.ToString());
            return lines;
        }

        /// <summary>strftime(MSI 0x4ec "%m/%d %H:%M", localtime(t)).</summary>
        private static string Date(int unix) => DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);

        private static string Format(string pattern, string arg) {
            int at = pattern.IndexOf("%s", StringComparison.Ordinal);
            if (at < 0) at = pattern.IndexOf("%d", StringComparison.Ordinal);
            return at < 0 ? pattern : pattern.Substring(0, at) + arg + pattern.Substring(at + 2);
        }

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        private static void Chat(string text) => RoClassicChat.Instance?.AddLine(text, "#FFFF64");

        private static string Text(byte[] b, int at, int length) {
            int end = at;
            while (end < at + length && end < b.Length && b[end] != 0) end++;
            Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
            return Encoding.GetEncoding(1252).GetString(b, at, end - at);
        }

        /// <summary>!md wait name prio | prio n | info name destroy enter | notify type enter | toggle | state.</summary>
        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => t.Length > i ? int.Parse(t[i], CultureInfo.InvariantCulture) : 0;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            switch (t[0]) {
                case "wait": {
                    var p = new byte[65];
                    BitConverter.GetBytes((ushort) 0x02cb).CopyTo(p, 0);
                    Encoding.ASCII.GetBytes(t.Length > 1 ? t[1] : "Endless Tower").CopyTo(p, 2);
                    BitConverter.GetBytes((short) Arg(2)).CopyTo(p, 63);
                    Handle(p);
                    break;
                }
                case "prio": {
                    var p = new byte[4];
                    BitConverter.GetBytes((ushort) 0x02cc).CopyTo(p, 0);
                    BitConverter.GetBytes((short) Arg(1)).CopyTo(p, 2);
                    Handle(p);
                    break;
                }
                case "info": {
                    var p = new byte[71];
                    BitConverter.GetBytes((ushort) 0x02cd).CopyTo(p, 0);
                    Encoding.ASCII.GetBytes(t.Length > 1 ? t[1] : "Endless Tower").CopyTo(p, 2);
                    BitConverter.GetBytes(Arg(2) > 0 ? (int) (now + Arg(2)) : 0).CopyTo(p, 63);
                    BitConverter.GetBytes(Arg(3) > 0 ? (int) (now + Arg(3)) : 0).CopyTo(p, 67);
                    Handle(p);
                    break;
                }
                case "notify": {
                    var p = new byte[10];
                    BitConverter.GetBytes((ushort) 0x02ce).CopyTo(p, 0);
                    BitConverter.GetBytes(Arg(1)).CopyTo(p, 2);
                    BitConverter.GetBytes(Arg(2) > 0 ? (int) (now + Arg(2)) : 0).CopyTo(p, 6);
                    Handle(p);
                    break;
                }
                case "toggle":
                    Toggle();
                    break;
            }

            RoClassicMemorial m = instance;
            return m == null ? "none" : "name='" + m.name + "' state=" + m.state + " prio=" + m.priority + " shown=" + (m.window != null && m.window.Visible) + " visible=" + m.visible;
        }
    }
}
