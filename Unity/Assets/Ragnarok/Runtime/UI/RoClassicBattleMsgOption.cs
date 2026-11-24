using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The classic battle-message filter of a chat tab, UIBattleMsgOptionWnd 0x84 (ui/RUNTIME-RULES-OPTIONS.md §7): the
    /// tab's 24 message flags as rows with an on/off icon (the party-search row is never shown), 8 rows visible, a press
    /// toggles a row, "all on" sets every flag, the wheel scrolls 3 rows; opened by the chat window's option button for its
    /// current tab (again on the same tab closes it, a tab switch shows the new tab's flags). The flags are the chat tab's
    /// own (RoChatModel: row index = chat type, the record's 24 bytes), changed at once and kept with the tabs.
    /// </summary>
    public sealed class RoClassicBattleMsgOption : IRoClassicUiProducer {
        private const string Layout = "Ragnarok/Generated/OriginalClient/ui/UIBattleMsgOptionWnd";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const int CloseId = 0xc5;
        private const int Rows = 8;           // (H − 38) / 20
        private const int HiddenRow = 15;     // "Search Message for Party Members": never drawn, not clickable
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Highlight = new Color32(0x70, 0x9f, 0xed, 255);

        // +0xa8: the 24 rows (row index = chat type = flag index)
        private static readonly (int MsgId, string Fallback)[] Flags = {
            (0x50b, "Public"),
            (0x4fd, "Public Chat"),
            (0x4fe, "Whisper"),
            (0x4ff, "Party Chat"),
            (0x500, "Guild Chat"),
            (0xfbf, "Show Alliance Chat messages"),
            (0x501, "Item get/drop"),
            (0x502, "Equipment on/off"),
            (0x503, "Abnormal status"),
            (0x504, "Party member's obtained item"),
            (0x505, "Party member's abnormal status"),
            (0x506, "Skill failure"),
            (0x507, "Party configuration"),
            (0x508, "Damaged equipment"),
            (0x533, "WOE Information"),
            (0x60d, "Search Message for Party Members"),
            (0x652, "Battle message"),
            (0x653, "Party member's battle message"),
            (0x654, "Experience message"),
            (0x656, "Quest information"),
            (0x657, "Battlefield message"),
            (0x93d, "Clan Chat"),
            (0xba4, "Display Call messages"),
            (0xfbe, "Displays changes in attributes when equipping/dismounting equipment"),
        };

        private static RoClassicBattleMsgOption instance;

        private readonly RoClassicWindow window;
        private readonly RoClassicChildLayout allOn;
        private readonly List<int> drawn = new List<int>(Flags.Length);   // the shown rows' flag indices
        private RoClassicChat chat;
        private int tab = -1;
        private bool mainTab = true;   // msg 0xbd's v2: the key is a main-window tab (map 0x154e3dc), else a separate window's (0x154e3e4)
        private string tabName = string.Empty;
        private readonly bool[] flags = new bool[Flags.Length];
        private int first;     // +0xa4, the scroll top in shown rows
        private int hover = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
        }

        private RoClassicBattleMsgOption(RoClassicUiSystem system, RoClassicWindowLayout layout) {
            // the oracle captured the window with eight rows drawn: keep its frame, the rows and the title come from here
            layout.Ops.RemoveAll(op =>
                op.Kind == RoClassicOpKind.Text ||
                (op.Kind == RoClassicOpKind.Bitmap && op.Path != null && op.Path.IndexOf("grp_o", StringComparison.OrdinalIgnoreCase) >= 0));
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UIScrollBar") child.Visible = false; // drawn with the live range
                if (child.Class == "UICheckBox") allOn = child;
            }

            for (int i = 0; i < Flags.Length; i++) {
                if (i != HiddenRow) drawn.Add(i);
            }

            window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = local => local.y < 17 };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == CloseId) Close();
            };
            window.Pressed += (w, local) => Press(local);
            window.Wheeled += (w, local, delta) => Scroll(delta > 0 ? -3 : 3);
            window.EscClose = () => {
                Close();
                return true;
            };
            system.Register(this);
        }

        public static bool IsOpen => instance != null && instance.window.Visible;

        /// <summary>
        /// SendMsg(6, 0xbd, tab, main, mode) from the chat window: mode 0 (its button) opens the tab's flags or, on the same tab,
        /// closes the window; mode 2 (a tab switch) only follows an open window to the new tab.
        /// </summary>
        public static void Toggle(RoClassicChat chat, int tab) => Toggle(chat, tab, true);

        /// <summary>
        /// SendMsg(6, 0xbd, key, isMain, 0): the chat window's button (<paramref name="isMain"/>) or a separate chat window's
        /// (UISubChatHisWnd msg 6 / 0xbd) — the same key of the same kind closes the window, anything else shows that record.
        /// </summary>
        public static void Toggle(RoClassicChat chat, int tab, bool isMain) {
            if (IsOpen && instance.chat == chat && instance.tab == tab && instance.mainTab == isMain) {
                Close();
                return;
            }

            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || chat == null) return;
            if (instance == null || instance.window == null) {
                var asset = Resources.Load<TextAsset>(Layout);
                if (asset == null) return;
                instance = new RoClassicBattleMsgOption(system, RoClassicWindowLayout.Parse(asset.text));
            }

            bool wasOpen = instance.window.Visible;
            instance.Load(chat, tab, isMain);
            if (!wasOpen) {
                // Move(scaledX(400), scaledY(200))
                Vector2Int client = system.ClientSize;
                instance.window.MoveTo(Mathf.Clamp((client.x - 640) / 2 + 400, 0, Mathf.Max(0, client.x - instance.window.Width)),
                    Mathf.Clamp(200 * client.y / 480, 0, Mathf.Max(0, client.y - instance.window.Height)));
            }

            instance.window.Visible = true;
            instance.window.Invalidate();
            system.Windows.BringToFront(instance.window);
            system.Windows.PushEsc(instance.window);
        }

        /// <summary>A tab switch (msg 0xbd mode 2) while the window is open: it shows that tab's record.</summary>
        public static void FollowTab(RoClassicChat chat, int tab) => FollowTab(chat, tab, true);

        /// <summary>Mode 2 for a main tab or a separate window's (its press, its rename): an open window shows that record.</summary>
        public static void FollowTab(RoClassicChat chat, int tab, bool isMain) {
            if (!IsOpen || instance.chat != chat) return;   // the same tab too: the record is read again (a rename, a removal)
            instance.Load(chat, tab, isMain);
            instance.window.Invalidate();
        }

        /// <summary>Mode 1 (a separate window's tab deleted, 0x89db04): the window closes when it showed that key of that kind, else nothing.</summary>
        public static void CloseForTab(RoClassicChat chat, int tab, bool isMain) {
            if (IsOpen && instance.chat == chat && instance.tab == tab && instance.mainTab == isMain) Close();
        }

        public static void Close() {
            if (instance == null) return;
            instance.window.Visible = false;
            RoClassicUiSystem.Instance?.Windows.RemoveEsc(instance.window);
        }

        private void Load(RoClassicChat owner, int tabIndex, bool isMain) {
            chat = owner;
            tab = tabIndex;
            mainTab = isMain;
            first = 0;
            hover = -1;
            RoChatModel.RoChatTab record = isMain ? chat.SnapshotTabForSettings(tabIndex) : chat.SnapshotSubTabForSettings(tabIndex);
            tabName = record != null ? record.Name : string.Empty;
            bool[] recordFlags = record != null ? record.CopyFlags() : new bool[Flags.Length];
            for (int i = 0; i < Flags.Length; i++) flags[i] = i < recordFlags.Length && recordFlags[i];
            SetAllOnBitmap();
        }

        /// <summary>The flags back into the tab record (record+0x18 + i), at once.</summary>
        private void Store() {
            if (mainTab) chat.SetTabFlagsForSettings(tab, flags);
            else chat.SetSubTabFlagsForSettings(tab, flags);
            SetAllOnBitmap();
        }

        private bool AllOn {
            get {
                foreach (bool f in flags) {
                    if (!f) return false;
                }

                return true;
            }
        }

        private void SetAllOnBitmap() {
            if (allOn == null) return;
            foreach (RoClassicOp op in allOn.Ops) {
                if (op.Kind == RoClassicOpKind.Bitmap && op.Path != null) {
                    op.Path = "유저인터페이스\\" + (AllOn ? "checkbox_1.bmp" : "checkbox_0.bmp");
                }
            }
        }

        // ---- drawing (OnDraw 0x88ca10 after the oracle's frame) ----

        private void Draw(RoClassicWindow w) {
            // the title: the tab's name + MsgStr(0x6f1), or MsgStr(0x509) for a tab without one
            string title = string.IsNullOrEmpty(tabName)
                ? RoClassicUiHost.MsgStr(0x509, "Battle Message Window")
                : tabName + RoClassicUiHost.MsgStr(0x6f1, " message log settings");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);

            for (int n = 0; n < Rows && first + n < drawn.Count; n++) {
                int index = drawn[first + n], y = 22 + 20 * n;
                if (first + n == hover) {
                    w.FillRect(25, y - 3, w.Width - 40, 20, Highlight);
                    w.FillRect(26, y - 2, w.Width - 42, 18, White);
                }

                w.TextOut(50, y, RoClassicUiHost.MsgStr(Flags[index].MsgId, Flags[index].Fallback), 0, 12, Black);
                w.DrawBitmap(27, y, Basic + (flags[index] ? "grp_online.bmp" : "grp_offline.bmp"));
            }

            int range = Math.Max(0, drawn.Count - Rows);
            w.DrawScrollBar(w.Width - 15, 14, w.Height - 38, range, drawn.Count, Rows, first);
        }

        // ---- input (0x891880, 0x892520, 0x894160) ----

        private int RowAt(Vector2Int local) {
            if (local.y < 17 || local.x < 18 || local.x >= window.Width - 15) return -1;
            int n = (local.y - 17) / 20;
            return n < Rows && first + n < drawn.Count ? first + n : -1;
        }

        private void Press(Vector2Int local) {
            if (local.y < 17) return;
            // "all on" (UICheckBox 0xd1, 46×12 at (20, H − 16)): every flag to its new state
            if (local.x >= 20 && local.x < 20 + 46 + 50 && local.y >= window.Height - 16 && local.y < window.Height - 4) {
                bool on = !AllOn;
                for (int i = 0; i < flags.Length; i++) flags[i] = on;
                Store();
                window.Invalidate();
                return;
            }

            if (local.x >= window.Width - 15 && local.y >= 14 && local.y < window.Height - 24) {
                PressScrollBar(local.y - 14, window.Height - 38);
                return;
            }

            int row = RowAt(local);
            if (row < 0) return;
            int index = drawn[row];
            flags[index] = !flags[index];
            hover = row;
            Store();
            window.Invalidate();
        }

        private void PressScrollBar(int y, int h) {
            int range = Math.Max(0, drawn.Count - Rows);
            if (range == 0) return;
            const int b = 13;
            int len = Math.Max(1, (h - 2 * b) * Rows / drawn.Count);
            int top = b + first * (h - 2 * b - len) / range;
            Scroll(y < b ? -1 : y >= h - b ? 1 : y < top ? -(Rows - 1) : y >= top + len ? Rows - 1 : 0);
        }

        private void Scroll(int rows) {
            int range = Math.Max(0, drawn.Count - Rows);
            int next = Mathf.Clamp(first + rows, 0, range);
            if (next == first) return;
            first = next;
            hover = -1;
            window.Invalidate();
        }

        public void ClassicUiTick(RoClassicUiSystem system) {
            if (window == null || !window.Visible || system.Windows.HoverWindow != window) return;
            int row = RowAt(system.Windows.Mouse - window.Position);
            if (row >= 0 && row != hover) {
                hover = row;
                window.Invalidate();
            }
        }

        // ---- diagnostics console (RoSkillSweepRunner "!bm ...") ----

        internal static string Sweep(string command, RoClassicChat chat) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length ? int.Parse(a[i], CultureInfo.InvariantCulture) : 0;
            switch (a[0]) {
                case "toggle":
                    if (chat != null) Toggle(chat, chat.ActiveTabIndex);
                    break;
                case "press":
                    instance?.Press(new Vector2Int(Arg(1), Arg(2)));
                    break;
                case "wheel":
                    instance?.Scroll(Arg(1));
                    break;
            }

            if (instance == null) return "no battle message window";
            var s = new StringBuilder();
            s.Append("open=").Append(IsOpen).Append(" tab=").Append(instance.tab).Append(instance.mainTab ? string.Empty : " (window)").Append(" '").Append(instance.tabName).Append("' first=").Append(instance.first)
                .Append(" pos=").Append(instance.window.Position).Append(" flags=");
            foreach (bool f in instance.flags) s.Append(f ? '1' : '0');
            RoChatModel.RoChatTab record = instance.chat == null ? null
                : instance.mainTab ? instance.chat.SnapshotTabForSettings(instance.tab) : instance.chat.SnapshotSubTabForSettings(instance.tab);
            if (record != null) {
                s.Append(" types=");
                foreach (int id in record.FilterIds) s.Append(id).Append(',');
            }

            return s.ToString();
        }
    }
}
