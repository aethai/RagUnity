using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The chat's data behind the classic chat window (RoClassicChat, UINewChatWnd 1; reference ui-static
    /// ui/RUNTIME-RULES-CHAT.md §1–2): the tabs and their filters, the received messages kept to re-wrap the tab boxes
    /// (msg 0x25, the newest 400), the typed-line history (FUN_007cb390, 30 lines) and the whisper names (FUN_007cb5c0 /
    /// FUN_009bb880) of the input row, the receiving group (DAT_01552dc8), the message colour and sender rules (§1g), the
    /// routing of a typed line (whisper, /w, /cl, '%' / '$', the group rule 0xc07a00), the emotion commands, and the
    /// outgoing queue to the session. No UI here: the window draws, wraps and takes the keyboard.
    /// </summary>
    public sealed class RoChatModel {
        /// <summary>The messages kept per chat (the boxes keep 240 wrapped lines each).</summary>
        public const int MaxMessages = 400;

        /// <summary>FUN_007cb390 / FUN_007cb5c0: 30 typed lines, 30 whisper names.</summary>
        public const int HistoryLimit = 30;

        /// <summary>The legacy chat's first system line: never shown by the classic chat.</summary>
        public const string LegacyWindowTitle = "Command List";

        /// <summary>The tab limit (+0xf0 = 10; "Cannot add more." past it).</summary>
        public const int MaxTabs = 10;

        /// <summary>The chat types a tab filters (UIBattleMsgOptionWnd's 24 rows, ChatWndInfo option1..option24).</summary>
        public const int ChatTypeCount = 24;

        /// <summary>Chat type 24: the line goes to every tab (ZC_BROADCAST2, an admin whisper).</summary>
        public const int ChatTypeEveryTab = 24;

        /// <summary>The first-run tabs (Lua Files\ChatWndInfo\ChatWndInfo, MSI_ST_CHAT / MSI_BT_CHAT in msgstring_kr.lub).</summary>
        public const string RegularChatName = "Regular Chat";
        public const string BattleLogName = "Battle Log";
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);

        /// <summary>A message as the chat window got it (msg 0x25), kept to re-wrap the boxes when the tabs change.</summary>
        public readonly struct Entry {
            public Entry(RoChatChannel channel, string text, Color32 color, string name, bool allTabs, string naviLabel = null, string naviPayload = null,
                List<RoClassicItemLink.Link> itemLinks = null, bool special = false, int extraFlag = -1) {
                ExtraFlag = extraFlag;
                ItemLinks = itemLinks;
                Special = special;
                Channel = channel;
                Text = text;
                Color = color;
                Name = name;
                AllTabs = allTabs;
                NaviLabel = naviLabel;
                NaviPayload = naviPayload;
            }

            /// <summary>A received navigation link: its "&lt;map x,y&gt;" label and its &lt;NAVIL&gt; payload.</summary>
            public string NaviLabel { get; }
            public string NaviPayload { get; }

            /// <summary>The message's converted item links (null without); Special = a line with link tags (no byte wrap).</summary>
            public List<RoClassicItemLink.Link> ItemLinks { get; }
            public bool Special { get; }

            public RoChatChannel Channel { get; }
            public string Text { get; }
            public Color32 Color { get; }

            /// <summary>The sender (whisper target on a double click), empty for the player's own lines and system lines.</summary>
            public string Name { get; }

            /// <summary>Type 0x18 or a red system message: every tab takes it.</summary>
            public bool AllTabs { get; }

            /// <summary>The client's chat type when the caller names one (RoChatLineTypes: 7 equip, 16 battle, 23 equip attributes, ...), else −1 (from the channel).</summary>
            public int ExtraFlag { get; }
        }

        /// <summary>A typed line on its way out: the channel, the text and the whisper target.</summary>
        public readonly struct RoQueuedChatMessage {
            public RoQueuedChatMessage(RoChatChannel channel, string message, string target) {
                Channel = channel;
                Message = message ?? string.Empty;
                Target = target ?? string.Empty;
            }

            public RoChatChannel Channel { get; }
            public string Message { get; }
            public string Target { get; }
        }

        /// <summary>
        /// A chat tab (a record of map 0x154e3dc): its name and the 24 chat-type flags its box takes (record+0x18 + type,
        /// ChatWndInfo optionN = type N − 1).
        /// </summary>
        internal sealed class RoChatTab {
            private readonly bool[] flags = new bool[ChatTypeCount];

            public RoChatTab(string name, IEnumerable<int> types = null) {
                Name = name ?? string.Empty;
                if (types == null) return;
                foreach (int type in types) {
                    if (type >= 0 && type < ChatTypeCount) flags[type] = true;
                }
            }

            public string Name { get; internal set; }

            /// <summary>A typed line from this tab goes out as public chat (the client's tabs only filter what they show).</summary>
            public RoChatChannel SubmitChannel => RoChatChannel.Main;

            public bool Takes(int type) => type >= 0 && type < ChatTypeCount && flags[type];

            public bool Accepts(RoChatChannel channel) => Takes(ChatTypeForChannel(channel));

            /// <summary>The types that are on (the diagnostics' view).</summary>
            public IReadOnlyCollection<int> FilterIds {
                get {
                    var on = new List<int>(ChatTypeCount);
                    for (int i = 0; i < ChatTypeCount; i++) {
                        if (flags[i]) on.Add(i);
                    }

                    return on;
                }
            }

            public bool[] CopyFlags() => (bool[]) flags.Clone();

            public void SetFlags(IList<bool> values) {
                for (int i = 0; i < ChatTypeCount; i++) flags[i] = values != null && i < values.Count && values[i];
            }

            public void SetChannelFilters(IEnumerable<RoChatChannel> channels) {
                Array.Clear(flags, 0, flags.Length);
                if (channels == null) return;
                foreach (RoChatChannel channel in channels) {
                    int type = ChatTypeForChannel(channel);
                    if (type < ChatTypeCount) flags[type] = true;
                }
            }

            /// <summary>The flags as ChatWndInfo writes them: '1' / '0' for option1..option24.</summary>
            public string FlagText() {
                var c = new char[ChatTypeCount];
                for (int i = 0; i < ChatTypeCount; i++) c[i] = flags[i] ? '1' : '0';
                return new string(c);
            }

            /// <summary>XPos / YPos / Width / Height of the record (+0x30..+0x3c): a separate chat window's rect, 0 in the main window.</summary>
            public int X { get; internal set; }
            public int Y { get; internal set; }
            public int Width { get; internal set; }
            public int Height { get; internal set; }

            public RoChatTab Clone() {
                var copy = new RoChatTab(Name) { X = X, Y = Y, Width = Width, Height = Height };
                copy.SetFlags(flags);
                return copy;
            }
        }

        /// <summary>
        /// One ChatSubWnd_n of SaveData\ChatWndInfo_U.lua (writer 0x894ef0, reader SetSubChatWndList 0xa291b0): TabState "On"
        /// (<see cref="Main"/>) is a tab of the main window, anything else a separate chat window at XPos / YPos of
        /// Width × Height; option1..24 as '1' / '0'.
        /// </summary>
        public readonly struct SavedTab {
            public SavedTab(string name, string flags, bool main, int x = 0, int y = 0, int width = 0, int height = 0) {
                Name = name ?? string.Empty;
                Flags = flags ?? string.Empty;
                Main = main;
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public string Name { get; }
            public string Flags { get; }
            public bool Main { get; }
            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }
        }

        private readonly List<Entry> entries = new List<Entry>(128);
        private readonly List<RoChatTab> tabs = new List<RoChatTab>(6);
        private readonly List<RoChatTab> subTabs = new List<RoChatTab>(4);   // map 0x154e3e4: the separate chat windows' tabs
        private readonly Queue<RoQueuedChatMessage> pendingMessages = new Queue<RoQueuedChatMessage>(4);
        private readonly List<string> history = new List<string>(HistoryLimit);
        private readonly List<string> whisperNames = new List<string>(HistoryLimit);
        private bool isSendingPendingMessages;
        private int historyIndex;
        private string historyDraft = string.Empty;
        private int whisperIndex;

        public RoChatModel() {
            ResetTabs();
            ActiveChannel = RoChatChannel.Main;
        }

        public RoLoginShellSession Session { get; private set; }
        public IRoChatCommandExecutor Executor { get; private set; }

        /// <summary>Called with a line for the chat when a send fails (may run off the main thread, as the send's continuation).</summary>
        public Action<string> SystemLine { get; set; }

        /// <summary>The input row is shown (chat mode, m_bCanSetChatMode: Enter shows / hides it).</summary>
        public bool InputVisible { get; set; } = true;

        /// <summary>DAT_01552dc8: 0 all, 1 party, 2 guild, 3 clan, 4 allies; 0 at start, not saved.</summary>
        public int SendGroup { get; set; }

        public IReadOnlyList<Entry> Entries => entries;
        internal IReadOnlyList<RoChatTab> Tabs => tabs;
        internal IReadOnlyList<RoChatTab> SubTabs => subTabs;
        public int TabCount => tabs.Count;

        /// <summary>The tabs that live in separate chat windows (UISubChatHisWnd), in key order.</summary>
        public int SubTabCount => subTabs.Count;

        /// <summary>Every tab, main window and separate windows (≤ 10, +0xf0).</summary>
        public int TotalTabCount => tabs.Count + subTabs.Count;

        /// <summary>A separate window tab's name (empty past them).</summary>
        public string SubTabName(int sub) => sub >= 0 && sub < subTabs.Count ? subTabs[sub].Name : string.Empty;

        /// <summary>A separate window tab's 24 flags as '1' / '0' (empty past them).</summary>
        public string SubTabFlagText(int sub) => sub >= 0 && sub < subTabs.Count ? subTabs[sub].FlagText() : string.Empty;

        /// <summary>A separate window tab's kept rect (XPos, YPos, Width, Height).</summary>
        public RectInt SubTabRect(int sub) =>
            sub >= 0 && sub < subTabs.Count ? new RectInt(subTabs[sub].X, subTabs[sub].Y, subTabs[sub].Width, subTabs[sub].Height) : default;

        /// <summary>The main window's tab control (shown names, tooltips, widths), kept in step with the main tabs.</summary>
        public RoChatTabStrip Strip { get; } = new RoChatTabStrip();

        /// <summary>The tab control's text width (font 0, height 12); a fixed 6 px per character until the window sets it.</summary>
        public Func<string, int> TabMeasure { get; set; }

        /// <summary>The width of the window the tab control is in (the chat, 600): the cut limit is that / tabs − 10.</summary>
        public int StripParentWidth { get; set; } = 600;

        /// <summary>A tab's name (empty past the tabs).</summary>
        public string TabName(int tab) => tab >= 0 && tab < tabs.Count ? tabs[tab].Name : string.Empty;

        /// <summary>A tab's 24 flags as '1' / '0' (option1..option24; empty past the tabs).</summary>
        public string TabFlagText(int tab) => tab >= 0 && tab < tabs.Count ? tabs[tab].FlagText() : string.Empty;
        public int ActiveTabIndex { get; private set; }
        public RoChatChannel ActiveChannel { get; private set; }

        public IReadOnlyList<string> History => history;
        public int HistoryIndex => historyIndex;
        public IReadOnlyList<string> WhisperNames => whisperNames;

        /// <summary>The whisper name list's cursor (the receiver popup's choice, the end after a double click).</summary>
        public int WhisperIndex {
            get => whisperIndex;
            set => whisperIndex = value;
        }

        /// <summary>
        /// A session (and its sender) for the chat: queued lines are dropped; another session than before also clears the
        /// kept messages. True when the session changed.
        /// </summary>
        public bool Bind(RoLoginShellSession loginSession, IRoChatCommandExecutor commands) {
            bool changed = !ReferenceEquals(Session, loginSession);
            Session = loginSession;
            Executor = commands;
            pendingMessages.Clear();
            if (changed) {
                entries.Clear();
            }

            return changed;
        }

        /// <summary>The sender of a session's chat lines (null without a session).</summary>
        public static IRoChatCommandExecutor CreateExecutor(RoLoginShellSession loginSession) {
            return loginSession != null ? new RoLegacyChatCommandExecutor(loginSession) : null;
        }

        // ---- messages ----

        /// <summary>A received message: kept, the oldest past 400 dropped.</summary>
        public void Add(Entry entry) {
            entries.Add(entry);
            if (entries.Count > MaxMessages) entries.RemoveRange(0, entries.Count - MaxMessages);
        }

        public void ClearEntries() => entries.Clear();

        /// <summary>
        /// msg 0x25: a tab takes the message when its flag for the message's type is on, or the message goes to every tab
        /// (type 24, or a red type-0 line: 0x8988b9 tests the colour only for type 0, a red line of another type stays in its
        /// type's tabs).
        /// </summary>
        public bool Accepts(int tab, Entry entry) {
            if (tab < 0 || tab >= tabs.Count) return false;
            int type = TypeOf(entry);
            return (entry.AllTabs && type == 0) || type == ChatTypeEveryTab || tabs[tab].Takes(type);
        }

        /// <summary>The client's chat type of a kept message: the caller's type, else its channel's.</summary>
        public static int TypeOf(Entry entry) => entry.ExtraFlag >= 0 ? entry.ExtraFlag : ChatTypeForChannel(entry.Channel);

        /// <summary>msg 0x25's second loop (0x898a84): a separate window's tab takes the line by the same rule.</summary>
        public bool AcceptsSub(int sub, Entry entry) {
            if (sub < 0 || sub >= subTabs.Count) return false;
            int type = TypeOf(entry);
            return (entry.AllTabs && type == 0) || type == ChatTypeEveryTab || subTabs[sub].Takes(type);
        }

        /// <summary>
        /// The send path's reserved-tag test (0x899053: FUN_007bae00 with 0x115d150 / 0x115d168, FUN_007bae90 with
        /// 0x115d228 / 0x115d240): a typed line holding "&lt;ITEML&gt;", "&lt;/ITEML&gt;", "&lt;NAVIL&gt;" or "&lt;/NAVIL&gt;"
        /// (exact case) is refused with MsgStr 0xbe2.
        /// </summary>
        public static bool ContainsReservedLinkTag(string text) {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf("<ITEML>", StringComparison.Ordinal) >= 0 || text.IndexOf("</ITEML>", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("<NAVIL>", StringComparison.Ordinal) >= 0 || text.IndexOf("</NAVIL>", StringComparison.Ordinal) >= 0;
        }

        /// <summary>The kept messages a tab takes, one per line (the diagnostics' and the tests' view of a box).</summary>
        public string TabText(int tab) {
            var sb = new System.Text.StringBuilder(256);
            foreach (Entry entry in entries) {
                if (!Accepts(tab, entry)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(entry.Text);
            }

            return sb.ToString();
        }

        /// <summary>
        /// The client's colour per message source (RUNTIME-RULES-CHAT §1g): public chat white, own green; whisper yellow;
        /// party own (255,200,0) / others (255,200,200); guild (180,255,180); clan (255,180,180); everything else as the
        /// handler resolved it. The sender is the text before " :" for public / party / guild / clan chat (types 1, 3, 4,
        /// 21), cleared when it is the player's own name.
        /// </summary>
        public Color32 ResolveMessageColor(RoChatChannel channel, string colorHex, string text, out string name, out bool allTabs) {
            name = string.Empty;
            allTabs = false;
            switch (channel) {
                case RoChatChannel.Main:
                case RoChatChannel.Party:
                case RoChatChannel.Guild:
                case RoChatChannel.Clan: {
                    int p = text.IndexOf(" :", StringComparison.Ordinal);
                    if (p > 0 && p < 24) name = text.Substring(0, p);
                    break;
                }
                case RoChatChannel.Whisper:
                    if (text.StartsWith("[ From ", StringComparison.Ordinal)) {
                        int e = text.IndexOf(" ] :", StringComparison.Ordinal);
                        if (e > 7) name = text.Substring(7, e - 7);
                    }

                    break;
            }

            string me = Session != null ? Session.SelectedCharacterName ?? string.Empty : string.Empty;
            bool own = name.Length > 0 && name == me;
            if (own) name = string.Empty;
            Color32 legacy = ColorUtility.TryParseHtmlString(string.IsNullOrWhiteSpace(colorHex) ? ResolveColorHex(channel) : colorHex, out Color c)
                ? (Color32) c
                : White;
            switch (channel) {
                case RoChatChannel.Main:
                    return string.Equals(colorHex, "#f0eadb", StringComparison.OrdinalIgnoreCase)
                        ? own ? new Color32(0, 255, 0, 255) : White
                        : legacy;
                case RoChatChannel.Party:
                    return own ? new Color32(255, 200, 0, 255) : new Color32(255, 200, 200, 255);
                case RoChatChannel.Guild:
                    return new Color32(180, 255, 180, 255);
                case RoChatChannel.Clan:
                    return new Color32(255, 180, 180, 255);
                case RoChatChannel.Whisper:
                    return new Color32(255, 255, 0, 255);
                default:
                    // red system messages (type 0, 0x0000ff) go to every tab
                    allTabs = channel == RoChatChannel.System && legacy.Equals(Red);
                    return legacy;
            }
        }

        /// <summary>The colour a caller's line gets when it names none.</summary>
        public static string ResolveColorHex(RoChatChannel channel) {
            switch (channel) {
                case RoChatChannel.Party: return "#e6d7c8";
                case RoChatChannel.Guild: return "#b4ffb4";
                case RoChatChannel.Whisper: return "#ffff00";
                case RoChatChannel.System: return "#b8e6a1";
                case RoChatChannel.Item: return "#00ffff";
                case RoChatChannel.Clan: return "#b5deef";
                default: return "#f0eadb";
            }
        }

        /// <summary>
        /// mgr msg 1 / msg 0x25: a text adds lines unless it is empty (the byte wrap 0x915a50 makes none) or the literal "No Msg" /
        /// "NO MSG" (0x9da048 / 0x9da07d); spaces are kept (a text of spaces is a blank line).
        /// </summary>
        public static bool IsShownText(string text) => !string.IsNullOrEmpty(text) && text != "No Msg" && text != "NO MSG";

        /// <summary>FUN_009af5e0: removes every ^RRGGBB code.</summary>
        public static string StripColourCodes(string s) {
            if (s.IndexOf('^') < 0) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == '^' && i + 7 <= s.Length && IsHex6(s, i + 1)) {
                    i += 6;
                    continue;
                }

                sb.Append(s[i]);
            }

            return sb.ToString();
        }

        private static bool IsHex6(string s, int at) {
            for (int k = 0; k < 6; k++) {
                if (!Uri.IsHexDigit(s[at + k])) return false;
            }

            return true;
        }

        // ---- tabs ----

        /// <summary>The tab control's selection (msg 0x16): that tab's box shows. False when it already was the current one.</summary>
        public bool SelectTab(int index) {
            if (index == ActiveTabIndex || index < 0 || index >= tabs.Count) return false;
            ActiveTabIndex = index;
            ActiveChannel = tabs[index].SubmitChannel;
            return true;
        }

        /// <summary>A caller's channel: the tab that takes it becomes the current one.</summary>
        public void SetActiveChannel(RoChatChannel channel) {
            ActiveChannel = ResolveSubmitChannel(channel);
            int tab = FindTabForChannel(channel);
            if (tab >= 0) {
                ActiveTabIndex = tab;
            }
        }

        /// <summary>
        /// The first-run tabs (ChatWndInfo.lub): "Regular Chat" with option1..option14 (types 0..13), "Battle Log" with none
        /// (it only gets the every-tab lines until set up); the first one current.
        /// </summary>
        public void ResetTabs() {
            tabs.Clear();
            tabs.Add(new RoChatTab(RegularChatName, new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }));
            tabs.Add(new RoChatTab(BattleLogName));
            ActiveTabIndex = 0;
            RebuildStrip();
        }

        /// <summary>
        /// The tab control made again from the main tabs (OnCreate 0x884330: AddTab for each record in key order), with the
        /// current text measure — every add cuts the names to the limit of that moment.
        /// </summary>
        public void RebuildStrip() {
            Strip.Clear();
            foreach (RoChatTab tab in tabs) Strip.Add(tab.Name, TabMeasure, StripParentWidth);
        }

        /// <summary>
        /// The "+" button (chat msg 6, id 0x172, 0x899581): refused at 10 tabs (the caller shows "Cannot add more."); else
        /// "NewTab_&lt;n&gt;" (n = tabs + 1) with every flag off becomes the current tab.
        /// </summary>
        public bool AddTab() => AddTab(false);

        /// <summary>
        /// 0x899581 in full: refused when main and separate tabs make 10 (+0xf0); else "NewTab_&lt;main + separate + 1&gt;"
        /// with every flag off goes to a separate chat window when <paramref name="separate"/> (the caller's test: one more
        /// tab of the current width would make the strip wider than W − 90), else into the main window as the current tab.
        /// </summary>
        public bool AddTab(bool separate) {
            if (TotalTabCount >= MaxTabs) {
                return false;
            }

            var tab = new RoChatTab("NewTab_" + (TotalTabCount + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (separate) {
                subTabs.Add(tab);
                return true;
            }

            tabs.Add(tab);
            Strip.Add(tab.Name, TabMeasure, StripParentWidth);
            ActiveTabIndex = tabs.Count - 1;
            ActiveChannel = tabs[ActiveTabIndex].SubmitChannel;
            return true;
        }

        /// <summary>The "−" button (id 0xc6, 0x89980d): the current tab goes (never the last one), the one before it becomes current.</summary>
        public bool RemoveActiveTab() {
            if (tabs.Count <= 1) {
                return false;
            }

            int removeIndex = Mathf.Clamp(ActiveTabIndex, 0, tabs.Count - 1);
            tabs.RemoveAt(removeIndex);
            Strip.Remove(removeIndex);
            ActiveTabIndex = Mathf.Clamp(removeIndex - 1, 0, tabs.Count - 1);
            ActiveChannel = tabs[ActiveTabIndex].SubmitChannel;
            return true;
        }

        /// <summary>
        /// Chat msg 0x75 (0x897ac4, a tab dragged out of the strip): with more than one main tab the current one moves to the
        /// end of the separate tabs (its record whole), leaves the tab control, and the tab before it becomes current.
        /// Returns the separate tab's index (its window key), or −1.
        /// </summary>
        public int DetachActiveTab() {
            if (tabs.Count <= 1 || ActiveTabIndex < 0 || ActiveTabIndex >= tabs.Count) return -1;
            int index = ActiveTabIndex;
            subTabs.Add(tabs[index]);
            tabs.RemoveAt(index);
            Strip.Remove(index);
            if (index > 0) index--;
            ActiveTabIndex = Mathf.Clamp(index, 0, tabs.Count - 1);
            ActiveChannel = tabs[ActiveTabIndex].SubmitChannel;
            return subTabs.Count - 1;
        }

        /// <summary>
        /// Chat msg 0x76 (0x897d83, accepted): separate tab <paramref name="sub"/> becomes the last main tab and the current
        /// one (AddTab cuts the shown names again); the separate tabs after it move up a key. Returns its main index, or −1.
        /// </summary>
        public int MergeSubTab(int sub) {
            if (sub < 0 || sub >= subTabs.Count) return -1;
            RoChatTab tab = subTabs[sub];
            subTabs.RemoveAt(sub);
            tabs.Add(tab);
            Strip.Add(tab.Name, TabMeasure, StripParentWidth);
            ActiveTabIndex = tabs.Count - 1;
            ActiveChannel = tab.SubmitChannel;
            return ActiveTabIndex;
        }

        /// <summary>A separate window's close button (id 0xc6, 0x89da37): the tab and its record go; the later keys move up.</summary>
        public bool RemoveSubTab(int sub) {
            if (sub < 0 || sub >= subTabs.Count) return false;
            subTabs.RemoveAt(sub);
            return true;
        }

        /// <summary>A separate window's Move / Resize (0x87e4d0, 0x894e10) write the window's rect into its record.</summary>
        public void SetSubTabRect(int sub, int x, int y, int width, int height) {
            if (sub < 0 || sub >= subTabs.Count) return;
            RoChatTab tab = subTabs[sub];
            tab.X = x;
            tab.Y = y;
            tab.Width = width;
            tab.Height = height;
        }

        /// <summary>A separate window's rename (msg 0 of its edit, 0x89d569): the record takes the name as it is.</summary>
        public bool RenameSubTab(int sub, string name) {
            if (sub < 0 || sub >= subTabs.Count || string.IsNullOrEmpty(name)) return false;
            subTabs[sub].Name = name;
            return true;
        }

        /// <summary>The battle-message option window's 24 flags for a separate window's tab.</summary>
        public bool SetSubTabFlags(int sub, IList<bool> flags) {
            if (sub < 0 || sub >= subTabs.Count || flags == null) return false;
            subTabs[sub].SetFlags(flags);
            return true;
        }

        internal RoChatTab SnapshotSubTab(int sub) => sub >= 0 && sub < subTabs.Count ? subTabs[sub].Clone() : default;

        // ---- a separate chat window's geometry (UISubChatHisWnd) ----

        /// <summary>
        /// 0x9c7cd0 (key, x, y, w, h): x = y = −1 → the cascade ((key + 10)·10, (key + 5)·20); w 0 or wider than the screen →
        /// 300; h 0 or taller → 95; x past the screen → screen − w; y past it → 100.
        /// </summary>
        public static RectInt SubWindowRect(int key, int x, int y, int width, int height, int screenWidth, int screenHeight) {
            if (x == -1 && y == -1) {
                x = (key + 10) * 10;
                y = (key + 5) * 20;
            }

            if (width == 0 || width > screenWidth) width = 300;
            if (height == 0 || height > screenHeight) height = 95;
            if (x > screenWidth) x = screenWidth - width;
            if (y > screenHeight) y = 100;
            return new RectInt(x, y, width, height);
        }

        /// <summary>Msg 0xe from its resizer: 0x118..0x200 × 0x4a..0x180, each snapped down to the minimum + 32k.</summary>
        public static Vector2Int SnapSubWindowSize(int width, int height) {
            int w = Math.Max(0x118, Math.Min(0x200, width)), h = Math.Max(0x4a, Math.Min(0x180, height));
            return new Vector2Int(0x118 + (w - 0x118) / 32 * 32, 0x4a + (h - 0x4a) / 32 * 32);
        }

        /// <summary>vf31 0x892490: the window's lower left corner (x, y + h) inside the chat window, edges included.</summary>
        public static bool DropsOnChat(RectInt window, RectInt chat) {
            int bottom = window.y + window.height;
            return chat.x <= window.x && window.x <= chat.x + chat.width && chat.y <= bottom && bottom <= chat.y + chat.height;
        }

        /// <summary>
        /// UITabControl's rename (msg 0 of its edit, 0x7faa00): an empty name keeps the edit open (false); the name is taken
        /// when tabs · (10 + its width) &lt; 600 − 90, else <paramref name="tooWide"/> (box MsgStr 0x629).
        /// </summary>
        public bool RenameTab(int index, string name, Func<string, int> measure, out bool tooWide) {
            tooWide = false;
            if (index < 0 || index >= tabs.Count || string.IsNullOrEmpty(name)) return false;
            int width = measure != null ? measure(name) : name.Length * 6;
            if (tabs.Count * (10 + width) >= 600 - 90) {
                tooWide = true;
                return false;
            }

            tabs[index].Name = name;
            Strip.Rename(index, name, TabMeasure);
            return true;
        }

        internal RoChatTab SnapshotTab(int tabIndex) {
            return tabIndex >= 0 && tabIndex < tabs.Count ? tabs[tabIndex].Clone() : default;
        }

        public bool SetTabFilters(int tabIndex, IList<RoChatChannel> filters) {
            if (tabIndex < 0 || tabIndex >= tabs.Count || filters == null) {
                return false;
            }

            tabs[tabIndex].SetChannelFilters(filters);
            return true;
        }

        /// <summary>A tab's 24 flags (the battle-message option window's rows).</summary>
        public bool SetTabFlags(int tabIndex, IList<bool> flags) {
            if (tabIndex < 0 || tabIndex >= tabs.Count || flags == null) {
                return false;
            }

            tabs[tabIndex].SetFlags(flags);
            return true;
        }

        /// <summary>
        /// The kept tabs back (SaveData\ChatWndInfo_U.lua: TabName and option1..option24 per ChatSubWnd_n): names and
        /// '1'/'0' flag texts; nothing usable → the first-run tabs.
        /// </summary>
        public void LoadTabs(IList<(string Name, string Flags)> saved) {
            if (saved == null || saved.Count == 0) return;
            var records = new List<SavedTab>(saved.Count);
            foreach ((string name, string flagText) in saved) records.Add(new SavedTab(name, flagText, true));
            LoadTabs(records);
        }

        /// <summary>
        /// The kept tabs back with their windows (SetSubChatWndList 0xa291b0 per ChatSubWnd_n while main + separate &lt; 10:
        /// TabState "On" → a main tab, else a separate window's tab with its rect; then SetSubChatWndOption per optionN).
        /// Nothing usable → the tabs stay; no main tab among them → the first-run main tabs.
        /// </summary>
        public void LoadTabs(IList<SavedTab> saved) {
            if (saved == null || saved.Count == 0) return;
            tabs.Clear();
            subTabs.Clear();
            foreach (SavedTab record in saved) {
                if (TotalTabCount >= MaxTabs) break;
                var tab = new RoChatTab(string.IsNullOrEmpty(record.Name) ? "NewTab_" + (TotalTabCount + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : record.Name);
                var flags = new bool[ChatTypeCount];
                string flagText = record.Flags;
                for (int i = 0; i < ChatTypeCount && flagText != null && i < flagText.Length; i++) flags[i] = flagText[i] == '1';
                tab.SetFlags(flags);
                if (record.Main) {
                    tabs.Add(tab);
                } else {
                    tab.X = record.X;
                    tab.Y = record.Y;
                    tab.Width = record.Width;
                    tab.Height = record.Height;
                    subTabs.Add(tab);
                }
            }

            if (tabs.Count == 0) {
                ResetTabs();
                while (TotalTabCount > MaxTabs && subTabs.Count > 0) subTabs.RemoveAt(subTabs.Count - 1);
            }

            ActiveTabIndex = 0;
            ActiveChannel = RoChatChannel.Main;
            RebuildStrip();
        }

        /// <summary>The records as ChatWndInfo_U.lua's writer (0x894ef0) puts them: the main tabs ("On", rect 0), then the separate ones.</summary>
        public List<SavedTab> SaveTabs() {
            var records = new List<SavedTab>(TotalTabCount);
            foreach (RoChatTab tab in tabs) records.Add(new SavedTab(tab.Name, tab.FlagText(), true));
            foreach (RoChatTab tab in subTabs) records.Add(new SavedTab(tab.Name, tab.FlagText(), false, tab.X, tab.Y, tab.Width, tab.Height));
            return records;
        }

        private int FindTabForChannel(RoChatChannel channel) {
            for (int i = 0; i < tabs.Count; i++) {
                if (tabs[i].Accepts(channel)) {
                    return i;
                }
            }

            return -1;
        }

        internal static RoChatChannel ResolveSubmitChannel(RoChatChannel channel) {
            return channel == RoChatChannel.All || channel == RoChatChannel.System || channel == RoChatChannel.Item
                ? RoChatChannel.Main
                : channel;
        }

        /// <summary>
        /// The client's chat type of a line a caller adds by channel (RUNTIME-RULES-OPTIONS §7e): public chat 1, whisper 2,
        /// party 3, guild 4, item get / drop 6, clan 21, everything else 0 ("Public").
        /// </summary>
        public static int ChatTypeForChannel(RoChatChannel channel) {
            switch (channel) {
                case RoChatChannel.Main:
                    return 1;
                case RoChatChannel.Whisper:
                    return 2;
                case RoChatChannel.Party:
                    return 3;
                case RoChatChannel.Guild:
                    return 4;
                case RoChatChannel.Item:
                    return 6;
                case RoChatChannel.Clan:
                    return 21;
                default:
                    return 0;
            }
        }

        // ---- typed-line history (FUN_007cb390) and whisper names (FUN_007cb5c0, FUN_009bb880) ----

        /// <summary>A fresh input row: both cursors past the newest entry.</summary>
        public void ResetCursors() {
            historyIndex = history.Count;
            whisperIndex = whisperNames.Count;
        }

        /// <summary>FUN_007cb390: up to 30 lines; a recalled line is replaced in place.</summary>
        public void PushHistory(string text) {
            if (text.Length == 0) return;
            if (historyIndex >= history.Count) {
                if (history.Count >= HistoryLimit) history.RemoveAt(0);
                history.Add(text);
                historyIndex = history.Count;
            } else {
                history[historyIndex] = text;
                historyIndex++;
            }
        }

        /// <summary>
        /// UIChatEditCtrl msg 0x12 (Up, +1) / 0x13 (Down, −1): the line for the chat edit, the draft (<paramref name="current"/>)
        /// kept while browsing. False when nothing changes.
        /// </summary>
        public bool RecallHistory(int direction, string current, out string text) {
            text = null;
            int size = history.Count;
            if (size == 0) return false;
            if (direction > 0) {
                if (historyIndex >= size) historyDraft = current;
                if (historyIndex > 0) historyIndex--;
                text = history[Math.Min(historyIndex, size - 1)];
                return true;
            }

            if (historyIndex < size) {
                historyIndex++;
                text = historyIndex == size ? historyDraft : history[historyIndex];
                return true;
            }

            return false;
        }

        /// <summary>UIWhisperEditCtrl msg 0x12 / 0x13 over the names, an empty field past the newest. False when nothing changes.</summary>
        public bool RecallWhisperName(int direction, out string text) {
            text = null;
            int size = whisperNames.Count;
            if (size == 0) return false;
            if (direction > 0) {
                if (whisperIndex > 0) whisperIndex--;
                text = whisperNames[Math.Min(whisperIndex, size - 1)];
                return true;
            }

            if (whisperIndex < size) {
                whisperIndex++;
                text = whisperIndex == size ? string.Empty : whisperNames[whisperIndex];
                return true;
            }

            return false;
        }

        /// <summary>
        /// FUN_007cb5c0 (the whisper edit's finish-edit): empty → the cursor at the end; a known name changes nothing; with the
        /// cursor at the end the oldest of 30 goes, the name is pushed and the cursor points at it; elsewhere it is pushed
        /// without the cap and the cursor points at it.
        /// </summary>
        public void RememberWhisperName(string name) {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0) {
                whisperIndex = whisperNames.Count;
                return;
            }

            if (whisperNames.Contains(name)) return;
            if (whisperIndex >= whisperNames.Count && whisperNames.Count >= HistoryLimit) whisperNames.RemoveAt(0);
            whisperNames.Add(name);
            whisperIndex = whisperNames.Count - 1;
        }

        /// <summary>
        /// FUN_009bb880 (a 1:1 window made, an incoming whisper written to the main chat): an exact, case-sensitive check, a
        /// new name pushed without a cap; a cursor at the end stays at the end.
        /// </summary>
        public void AddWhisperName(string name) {
            if (string.IsNullOrEmpty(name) || whisperNames.Contains(name)) return;
            whisperNames.Add(name);
            int n = whisperNames.Count;
            if (whisperIndex >= n - 1) whisperIndex = n;
        }

        // ---- the typed line's routing and send ----

        /// <summary>The typed text as one line: CR / LF to spaces, trimmed.</summary>
        public static string NormalizeSubmittedText(string rawText) {
            return string.IsNullOrWhiteSpace(rawText) ? string.Empty : rawText.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        /// <summary>
        /// The client's emotion commands: a line whose first word is an emotion talk type of RoChatSlashCommands ("/!",
        /// "/?", "/ho" … "/ok", "/e1" … "/e33", "/heart", "/sorry", ...; the rest of the line is ignored) → its emotion id.
        /// </summary>
        public static bool TryParseEmotionCommand(string message, out byte emotion) {
            emotion = 0;
            string text = (message ?? string.Empty).Trim();
            if (text.Length < 2 || text[0] != '/') return false;
            if (!RoChatSlashCommands.TryGetEmotion(RoChatSlashCommands.Parse(text, out _), out int id)) return false;
            emotion = (byte) id;
            return true;
        }

        /// <summary>"/w name text": the whisper target and the text, both non-empty.</summary>
        public static bool TryParseWhisperCommand(string value, out string target, out string message) {
            target = string.Empty;
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("/w ", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string remainder = value.Substring(3).TrimStart();
            int separator = remainder.IndexOf(' ');
            if (separator <= 0 || separator >= remainder.Length - 1) {
                return false;
            }

            target = remainder.Substring(0, separator).Trim();
            message = remainder.Substring(separator + 1).Trim();
            return target.Length > 0 && message.Length > 0;
        }

        /// <summary>
        /// A command line (or a chat room line): "/w name text" whispers; "/cl" and "/clanchat" with a text (0xcef1c8, chat
        /// type 0xd4) go to the clan (mode msg 0x121 → CZ_CLAN_CHAT); a '%' / '$' prefix to the party / guild; else the
        /// current tab's channel (public chat, which sends a server command as it is).
        /// </summary>
        public RoQueuedChatMessage ResolveQueuedMessage(string message) {
            if (TryParseWhisperCommand(message, out string commandTarget, out string commandMessage)) {
                return new RoQueuedChatMessage(RoChatChannel.Whisper, commandMessage, commandTarget);
            }

            foreach (string clan in new[] { "/cl ", "/clanchat " }) {
                if (message.StartsWith(clan, StringComparison.OrdinalIgnoreCase) && message.Length > clan.Length && message.Substring(clan.Length).Trim().Length > 0) {
                    return new RoQueuedChatMessage(RoChatChannel.Clan, message.Substring(clan.Length).Trim(), string.Empty);
                }
            }

            if (message.StartsWith("%", StringComparison.Ordinal)) {
                return new RoQueuedChatMessage(RoChatChannel.Party, message.Substring(1).TrimStart(), string.Empty);
            }

            if (message.StartsWith("$", StringComparison.Ordinal)) {
                return new RoQueuedChatMessage(RoChatChannel.Guild, message.Substring(1).TrimStart(), string.Empty);
            }

            RoChatChannel channel = ResolveSubmitChannel(ActiveChannel);
            return new RoQueuedChatMessage(channel, message, string.Empty);
        }

        /// <summary>
        /// 0xc07a00 case 0 for a plain line: the '%' / '$' / '#' prefixes (stripped in every mode) and Ctrl / Alt / CapsLock
        /// held, then the group: guild (in a guild; '$' → public), party ('%' or no party → public), clan (in a clan),
        /// allies (in a guild; '#' → public), else the prefix decides. False when the line sends nothing ("%" alone).
        /// The alliance packet 0x0bdd is unknown to Custom's Hercules (it would drop the session): its lines go to the guild.
        /// </summary>
        public static bool TryResolveGroupMessage(string message, int sendGroup, bool ctrl, bool alt, bool capsLock, bool inParty, bool inGuild, bool inClan,
            out RoQueuedChatMessage queued) {
            queued = default;
            string text = message;
            bool party = false, guild = false, ally = false;
            if (text[0] == '%' || text[0] == '$') {
                if (text.Length < 2) return false;
                party = text[0] == '%';
                guild = text[0] == '$';
                text = text.Substring(1);
            }

            if (ctrl) party = true;
            if (alt) guild = true;
            if (message[0] == '#') {
                if (message.Length < 2) return false;
                text = message.Substring(1);
                ally = true;
            }

            if (capsLock) ally = true;
            RoChatChannel channel;
            switch (sendGroup) {
                case 2 when inGuild:
                    channel = guild ? RoChatChannel.Main : RoChatChannel.Guild;
                    break;
                case 1:
                    channel = party || !inParty ? RoChatChannel.Main : RoChatChannel.Party;
                    break;
                case 3 when inClan:
                    channel = RoChatChannel.Clan;
                    break;
                case 4 when inGuild:
                    channel = ally ? RoChatChannel.Main : RoChatChannel.Guild;
                    break;
                default:
                    channel = party && inParty ? RoChatChannel.Party : guild || ally ? RoChatChannel.Guild : RoChatChannel.Main;
                    break;
            }

            queued = new RoQueuedChatMessage(channel, text, string.Empty);
            return true;
        }

        /// <summary>The line to the session's sender, in order (a failed send prints its reason as a system line).</summary>
        public void Send(RoQueuedChatMessage queued) {
            pendingMessages.Enqueue(queued);
            FlushPendingMessages();
        }

        private void FlushPendingMessages() {
            if (Executor == null || isSendingPendingMessages) {
                return;
            }

            _ = FlushPendingMessagesAsync();
        }

        private async Task FlushPendingMessagesAsync() {
            if (isSendingPendingMessages || Executor == null) {
                return;
            }

            isSendingPendingMessages = true;
            try {
                while (pendingMessages.Count > 0 && Executor != null) {
                    RoQueuedChatMessage queued = pendingMessages.Peek();
                    try {
                        RoChatCommandResult result = await Executor.SendChatAsync(queued.Channel, queued.Message, queued.Target).ConfigureAwait(false);
                        if (!result.IsAccepted) {
                            SystemLine?.Invoke(result.Diagnostic.Length > 0 ? result.Diagnostic : $"Chat send failed: {result.Status}");
                        }
                    } catch (Exception ex) {
                        SystemLine?.Invoke($"Chat message was not sent: {ex.Message}");
                    }

                    pendingMessages.Dequeue();
                }
            } finally {
                isSendingPendingMessages = false;
            }
        }
    }
}
