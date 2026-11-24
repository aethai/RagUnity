using System;
using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using Unity.Profiling;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// The chat: the original client's UINewChatWnd (window id 1) with its tab control, chat history box (UIChatHisBox),
    /// whisper and message edits (UIWhisperEditCtrl / UIChatEditCtrl) and input row, from the reverse-engineered SetSize
    /// 0x89e5f0, OnDraw 0x88e470, DrawTransBox2, mouse handlers and send flow (reference ui-static
    /// ui/RUNTIME-RULES-CHAT.md §1–2). Bound by the shell on the map entry (and by the first line it prints), ticked by
    /// RoClassicGameUi, closed on leaving the map. The data (tabs, kept messages, typed-line history, whisper names, the
    /// routing and the send queue) is RoChatModel's; this class wraps, draws and takes the mouse and the keyboard. Every
    /// window prints into it through <see cref="Instance"/> (null before the map entry and after leaving the map).
    /// </summary>
    public sealed partial class RoClassicChat {
        private static readonly ProfilerMarker ClassicChatMarker = new ProfilerMarker("RO.UI.Chat.ClassicChat");
        private static readonly ProfilerMarker RefreshMarker = new ProfilerMarker("RO.UI.Chat.Refresh");
        private const string ChatUiRoot = "유저인터페이스\\basic_interface\\";
        private const int ClassicChatWidth = 600;
        private const int ClassicBoxWidth = 592;          // UIChatHisBox w (= window w − 8)
        private const int ClassicLineHeight = 14;         // UIChatHisBox +0xb8
        private const int ClassicBoxLineLimit = 240;      // UIChatHisBox +0xf0
        private const int ClassicByteWrap = 94;           // FUN_00915a50 limit for a 592 px box
        private const int ClassicStickOffId = 0x162;
        private const int ClassicBattleOption2Id = 0xbd;
        private const int ClassicBattleOptionId = 0x172;
        private const int ClassicWndMiniId = 0xc6;
        private const int ClassicReceiverId = 0xdd;
        private const int ClassicSizeId = 0xd0;
        private const int ClassicGroupId = 0xea;
        private const string ClassicPrefsKey = "ro.ui.classic.chat.";

        private static readonly Color32 ClassicFrameGrey = new Color32(0x8e, 0x8e, 0x8e, 255);
        private static readonly Color32 ClassicChatWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ClassicChatBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 ClassicThumb = new Color32(165, 170, 175, 255);
        private static readonly Color32 ClassicBoxActive = new Color32(0, 0, 0, 0x66);   // 0x66000000
        private static readonly Color32 ClassicBoxInactive = new Color32(0, 0, 0, 0x99); // 0x99000000
        private static readonly Color32 ClassicEditFill = new Color32(240, 240, 240, 255);

        private static RoClassicChat instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        /// <summary>The chat the windows print to; null before the map entry and after leaving the map.</summary>
        public static RoClassicChat Instance => instance;

        private readonly RoChatModel model = new RoChatModel();

        private RoClassicChat() {
            // a failed send's reason as a system line
            model.SystemLine = AddSystemLine;
        }

        /// <summary>The chat's data (tabs, kept messages, histories, whisper names, routing).</summary>
        public RoChatModel Model => model;

        /// <summary>One wrapped line of a history box: text, colour and the sender it came from (whisper target).</summary>
        private readonly struct ClassicChatLine {
            public ClassicChatLine(string text, Color32 color, string name, string naviLabel = null, string naviPayload = null,
                (RoClassicItemLink.Link Link, int X)[] itemLinks = null) {
                ItemLinks = itemLinks;
                Text = text;
                Color = color;
                Name = name;
                NaviLabel = naviLabel;
                NaviPayload = naviPayload;
            }

            public string Text { get; }
            public Color32 Color { get; }
            public string Name { get; }

            /// <summary>A received navigation link on this line: its "&lt;map x,y&gt;" label and its &lt;NAVIL&gt; payload.</summary>
            public string NaviLabel { get; }
            public string NaviPayload { get; }

            /// <summary>The UIItemTagButtons of this line (0x7e3b00): each label and its x in the line.</summary>
            public (RoClassicItemLink.Link Link, int X)[] ItemLinks { get; }
        }

        private static readonly Color32 ClassicNaviLink = new Color32(0xe1, 0xf2, 0x82, 255);   // 0x0082F2E1

        private int pendingExtraFlag = -1;
        private string pendingName;

        /// <summary>A line of a chat type the tabs gate with the battle-message option's own flag (type 23: 0x628ab0's lines).</summary>
        public void AddFlaggedLine(string text, string color, int extraFlag) => AddTypedLine(text, color, extraFlag);

        /// <summary>
        /// UIWindowMgr::SendMsg(1, text, colour, type, name) with the caller's chat type (RoChatLineTypes; 24 = every tab).
        /// <paramref name="popOpen"/>: the caller's mgr msg 3 before the line (a hidden chat shows, size 0 opens at 1);
        /// <paramref name="name"/>: the sender kept with the line (double click → whisper target).
        /// </summary>
        public void AddTypedLine(string text, string color, int type, bool popOpen = false, string name = null) {
            if (closed) {
                instance?.AddTypedLine(text, color, type, popOpen, name);
                return;
            }

            if (popOpen) ClassicPopOpen();
            pendingExtraFlag = type;
            pendingName = name;
            try {
                AddLine(text, color);
            } finally {
                pendingExtraFlag = -1;
                pendingName = null;
            }
        }

        /// <summary>UIChatHisBox: one per tab, its own wrapped lines and first visible line.</summary>
        private sealed class ClassicChatBox {
            public readonly List<ClassicChatLine> Lines = new List<ClassicChatLine>(64);
            public int First;
            public int VisibleLines;   // a separate window's box (0 = the chat's own height)
        }

        private readonly List<ClassicChatBox> classicBoxes = new List<ClassicChatBox>(6);
        private readonly List<(int Start, int End)> classicWrap = new List<(int, int)>(4);
        private readonly List<(string Display, string Raw)> classicItemLinks = new List<(string, string)>(4);

        private RoClassicWindow classicChat;
        private RoClassicEditField classicMessageEdit;
        private RoClassicEditField classicWhisperEdit;
        private RoClassicChildLayout classicStickOff;
        private RoClassicChildLayout classicBattleOption2;
        private RoClassicChildLayout classicBattleOption;
        private RoClassicChildLayout classicWndMini;
        private RoClassicChildLayout classicReceiver;
        private RoClassicChildLayout classicSize;
        private RoClassicChildLayout classicGroup;
        private bool classicChatShown;
        private bool classicChatFailed;

        // closed on leaving the map: a window still holding this chat prints into the current one (as a destroyed legacy
        // chat fell back to the scene's chat)
        private bool closed;
        private bool classicBoxesDirty = true;
        private bool classicUnlocked = true;   // ChatWndInfo CanDragWnd (the player's file has 1)
        private bool classicAppliedInputOn;
        private bool classicAppliedChatMode = true;
        private int classicState = 1;           // M_CHATWNDSTATUS (size state n)
        private int classicBoxHeight = 70;      // UIChatHisBox h: CreateEx(.., 70), then H − 44 on every sized SetSize
        private int classicModeIconX;
        private int classicEnterFrame = -1;

        /// <summary>
        /// m_bCanSetChatMode, mgr+0x26 (OptionInfo.lua ChangeChatMode, the player's file has 1): Enter shows / hides the input
        /// row. It is the Shortcut Settings' "Enable Battle Mode" box and /battlemode (case 0x85); on by default.
        /// </summary>
        private static bool ClassicChatMode => RoClassicUiHost.ClientSettings == null || RoClassicUiHost.ClientSettings.IsBattleModeEnabled;

        /// <summary>SetSize: H = 24 for state 0, else 24 + (n + 3)·14 + 16 = 82 + 14n.</summary>
        private static int ClassicChatHeight(int state) => state == 0 ? 24 : 24 + (state + 3) * ClassicLineHeight + 16;

        private int ClassicVisibleLines => classicBoxHeight / ClassicLineHeight;

        private IReadOnlyList<RoChatModel.RoChatTab> tabs => model.Tabs;
        private int activeTabIndex => model.ActiveTabIndex;
        private RoLoginShellSession session => model.Session;

        private bool chatInputVisible {
            get => model.InputVisible;
            set => model.InputVisible = value;
        }

        // ---- the shell's side: bind, tick, close ----

        /// <summary>
        /// The shell on the map entry (and whenever it prints a line without a chat): the chat for the session, made on the
        /// first call. Another session than before clears the kept messages; queued lines are dropped.
        /// </summary>
        public static RoClassicChat Bind(RoLoginShellSession loginSession) => Bind(loginSession, RoChatModel.CreateExecutor(loginSession));

        /// <summary>The chat for the session with its own sender of the typed lines.</summary>
        public static RoClassicChat Bind(RoLoginShellSession loginSession, IRoChatCommandExecutor commands) {
            if (instance == null) instance = new RoClassicChat();
            if (instance.model.Bind(loginSession, commands)) instance.classicBoxesDirty = true;
            return instance;
        }

        /// <summary>Per frame (RoClassicGameUi): the window (made on the first tick), its boxes, the input row, Enter / F10, the popup.</summary>
        public static void Tick() {
            RoClassicChat chat = instance;
            if (chat == null) return;
            using (ClassicChatMarker.Auto()) chat.UpdateClassicChat();
        }

        /// <summary>Leaving the map (character select, shell off): the window goes, the next entry starts a fresh chat.</summary>
        public static void Close() {
            RoClassicChat chat = instance;
            if (chat == null) return;
            instance = null;
            chat.closed = true;
            chat.DisposeClassicPopup();
            chat.DisposeClassicChat();
        }

        public int ActiveTabIndex => model.ActiveTabIndex;
        public int TabCount => model.TabCount;

        public void AddSystemLine(string text) {
            AddLine(text, "#b8e6a1", RoChatChannel.System);
        }

        /// <summary>
        /// A line for the chat (msg 0x25); the colour is the caller's (#RRGGBB). Without a channel the line is a system line
        /// (chat type 0, "Public": a red one goes to every tab), as the client's callers that pass no type; chat lines name
        /// their channel. The text is kept as it is — mgr msg 1 (0x9da007) and msg 0x25 trim nothing: the byte wrap
        /// (0x915a50) keeps the leading spaces of " [%s] receives ..." and makes a blank line of a text of spaces; only an
        /// empty text (no wrapped line) and the literal "No Msg" / "NO MSG" (0x9da048 / 0x9da07d) add nothing.
        /// </summary>
        public void AddLine(string text, string color = "#f0eadb", RoChatChannel channel = RoChatChannel.System) {
            if (closed) {
                instance?.AddLine(text, color, channel);
                return;
            }

            if (!RoChatModel.IsShownText(text)) {
                return;
            }

            AutoSaveChatLine(text, channel);   // /autosavechat (RoClassicChat.Options.cs)
            ClassicOnLineAdded(channel, text, color);
        }

        public void AddMessage(RoChatMessage message) {
            string prefix = string.IsNullOrWhiteSpace(message.Sender) ? string.Empty : message.Sender.Trim() + ": ";
            AddLine(prefix + message.Text, message.IsSystem ? "#b8e6a1" : RoChatModel.ResolveColorHex(message.Channel), message.Channel);
        }

        /// <summary>Every kept message goes (the boxes empty).</summary>
        public void Clear() {
            ClassicClearHistory();
        }

        /// <summary>A caller's channel: the tab that takes it becomes the current one.</summary>
        public void SetActiveChannel(RoChatChannel channel) {
            model.SetActiveChannel(channel);
            classicChat?.Invalidate();
        }

        /// <summary>A name into the whisper edit (and the whisper names).</summary>
        public void SetWhisperTarget(string target) {
            if (closed) {
                instance?.SetWhisperTarget(target);
                return;
            }

            string resolved = target != null ? target.Trim() : string.Empty;
            SetClassicWhisperTarget(resolved);
        }

        /// <summary>
        /// 0x5928b0 → msg 0x99: a navigation link into the chat input, shown as <paramref name="display"/> and sent as
        /// <paramref name="payload"/> (one link per input: an earlier one is taken out first).
        /// </summary>
        public void InsertNaviLink(string display, string payload) {
            if (closed) {
                instance?.InsertNaviLink(display, payload);
                return;
            }

            if (string.IsNullOrEmpty(display) || string.IsNullOrEmpty(payload)) return;
            RemoveClassicNaviLink();
            TryInsertClassicItemLink(display, payload);
        }

        /// <summary>An item dropped on the chat or Alt-clicked: "&lt;its full name&gt;" in the chat edit, sent as the client's own link (0x7fe020: zero cards kept, grade always, variable-length fields).</summary>
        public void InsertItemLink(RoItemInstance item) {
            if (closed) {
                instance?.InsertItemLink(item);
                return;
            }

            if (item == null) {
                return;
            }

            RoItemMetadataTable metadata = ResolveItemMetadata();
            string rawLink = RoClassicItemLink.Encode(item, metadata);
            if (string.IsNullOrWhiteSpace(rawLink)) {
                return;
            }

            string displayName = item.GetFullDisplayName(metadata, includeRandomOptionCount: true);
            TryInsertClassicItemLink("<" + displayName + ">", rawLink);
        }

        public void SetTabFiltersForSettings(int tabIndex, IList<RoChatChannel> filters) {
            if (model.SetTabFilters(tabIndex, filters)) ClassicTabsChanged();
        }

        /// <summary>The battle-message option window's 24 flags for a tab (they take effect at once and are kept).</summary>
        internal void SetTabFlagsForSettings(int tabIndex, IList<bool> flags) {
            if (!model.SetTabFlags(tabIndex, flags)) return;
            ClassicTabsChanged();
            SaveClassicTabs();
        }

        internal RoChatModel.RoChatTab SnapshotTabForSettings(int tabIndex) => model.SnapshotTab(tabIndex);

        /// <summary>The item table of the map's content (empty until its assets are up).</summary>
        private static RoItemMetadataTable ResolveItemMetadata() {
            RoOnlineWorldController onlineWorld = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            RoAssetDatabase assets = ResolveInitializedAssets(onlineWorld != null ? onlineWorld.RuntimeMapController : null) ??
                ResolveInitializedAssets(UnityEngine.Object.FindFirstObjectByType<RoRuntimeMapController>());
            return assets != null && assets.IsInitialized
                ? assets.GameData?.Items ?? RoItemMetadataTable.Empty
                : RoItemMetadataTable.Empty;
        }

        private static RoAssetDatabase ResolveInitializedAssets(RoRuntimeMapController runtimeMap) {
            return runtimeMap != null &&
                runtimeMap.Content != null &&
                runtimeMap.Content.Assets != null &&
                runtimeMap.Content.Assets.IsInitialized
                ? runtimeMap.Content.Assets
                : null;
        }

        /// <summary>Per frame: the window, its boxes, the input row, the keys, the popup and the link tooltip.</summary>
        private void UpdateClassicChat() {
            if (classicChatFailed) return;
            try {
                EnsureClassicChat();
                if (!classicChatShown) {
                    classicChatShown = true;
                    classicChat.Visible = true;
                    classicBoxesDirty = true;
                }

                if (classicBoxesDirty) {
                    using (RefreshMarker.Auto()) RebuildClassicBoxes();
                }

                // battle mode off (Shortcut Settings 0xd1, /bm): the input row is always there (mgr+0x24 = 1)
                bool chatMode = ClassicChatMode;
                if (!chatMode && !chatInputVisible) chatInputVisible = true;
                if (classicAppliedInputOn != chatInputVisible || classicAppliedChatMode != chatMode) ClassicLayoutInputRow();
                ClassicSyncInputColor();
                ProcessClassicChatKeys();
                TickClassicPopup();
                if (classicResizing) TickClassicResize();
                UpdateClassicItemLinkHover();
                TickClassicSubs();
            } catch (Exception e) {
                classicChatFailed = true;
                DisposeClassicChat();
                Debug.LogException(e);
            }
        }

        private void EnsureClassicChat() {
            if (classicChat != null) return;
            RoClassicUiSystem system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
            var layout = new RoClassicWindowLayout {
                Class = "UINewChatWnd", WindowId = 1, Width = ClassicChatWidth, Height = ClassicChatHeight(15),
            };

            // AddChild order of OnCreate (the tab control and the history boxes are drawn by DrawClassicChat)
            classicStickOff = ChatButton(ClassicStickOffId, "stickoff", 14, 9, "stickoff");
            classicBattleOption2 = ChatButton(ClassicBattleOption2Id, "battle_option2", 9, 9, "battle_option2");
            classicBattleOption = ChatButton(ClassicBattleOptionId, "battle_option", 8, 8, "battle_option");
            classicWndMini = ChatButton(ClassicWndMiniId, "wnd_mini", 8, 8, "wnd_mini");
            classicReceiver = ChatButton(ClassicReceiverId, "receiver", 8, 18, null);
            classicReceiver.States["normal"] = ChatUiRoot + "dialog_btn0.bmp";
            classicReceiver.States["hover"] = ChatUiRoot + "dialog_btn1.bmp";
            classicReceiver.States["pressed"] = ChatUiRoot + "dialog_btn2.bmp";
            classicSize = ChatButton(ClassicSizeId, "size", 11, 11, null);
            classicGroup = ChatButton(ClassicGroupId, "group", 11, 11, null);
            foreach (RoClassicChildLayout b in new[] { classicSize, classicGroup }) {
                b.States["normal"] = ChatUiRoot + "sys_base_off.bmp";
                b.States["hover"] = ChatUiRoot + "sys_base_on.bmp";
                b.States["pressed"] = ChatUiRoot + "sys_base_on.bmp";
            }

            layout.Children.Add(classicStickOff);
            layout.Children.Add(classicBattleOption2);
            layout.Children.Add(classicBattleOption);
            layout.Children.Add(classicWndMini);
            layout.Children.Add(classicReceiver);
            layout.Children.Add(classicSize);
            layout.Children.Add(classicGroup);

            classicChat = new RoClassicWindow(system, layout) { FrameDragArea = ClassicChatDragArea };
            classicChat.DrawUnderlay += DrawClassicChatBoxes;
            classicChat.DrawDynamic += DrawClassicChat;
            classicChat.Clicked += OnClassicChatClicked;
            classicChat.Pressed += OnClassicChatPressed;
            classicChat.DoubleClicked += OnClassicChatDoubleClicked;
            classicChat.Wheeled += OnClassicChatWheel;
            classicChat.Moved += window => {
                PlayerPrefs.SetInt(ClassicPrefsKey + "x", window.Position.x);
                PlayerPrefs.SetInt(ClassicPrefsKey + "y", window.Position.y);
            };

            // chat edit Create(460, 16) max 255, whisper edit Create(90, 16) max 24, both inner RGB(240, 240, 240)
            classicMessageEdit = new RoClassicEditField { MaxBytes = 255, Background = ClassicEditFill, ForwardsPressToParent = true };
            classicWhisperEdit = new RoClassicEditField { MaxBytes = 24, Background = ClassicEditFill };
            classicChat.AddEditField(classicMessageEdit);
            classicChat.AddEditField(classicWhisperEdit);
            classicMessageEdit.Submitted += _ => ClassicChatEnter();
            classicWhisperEdit.Submitted += _ => ClassicChatEnter();
            classicMessageEdit.Cancelled += OnClassicEditCancelled;
            classicWhisperEdit.Cancelled += OnClassicEditCancelled;
            classicMessageEdit.TabPressed += _ => classicWhisperEdit.Focus();
            classicWhisperEdit.TabPressed += _ => {
                model.RememberWhisperName(classicWhisperEdit.Text);
                classicMessageEdit.Focus();
            };
            classicMessageEdit.HistoryRequested += (_, direction) => RecallClassicHistory(direction);
            classicWhisperEdit.HistoryRequested += (_, direction) => RecallClassicWhisperName(direction);
            model.ResetCursors();

            // UITabControl's rename edit (UIEditCtrl2, +0xd8): parked until a double click on the current tab
            classicTabRename = new RoClassicEditField { Background = ClassicChatWhite, Visible = false, HandlesEscape = true };
            classicChat.AddEditField(classicTabRename);
            classicTabRename.Submitted += _ => CommitClassicTabRename();
            classicTabRename.Cancelled += _ => EndClassicTabRename();

            // the tabs the player left (SaveData\ChatWndInfo_U.lua; here the prefs), else the first-run ones; the tab control is
            // made from them with the window's font (AddTab cuts the names to 600 / tabs − 10)
            model.TabMeasure = classicChat.FontFor(0, 12).Measure;
            model.StripParentWidth = ClassicChatWidth;
            model.LoadTabs(LoadClassicTabs());
            model.RebuildStrip();
            classicTabDragLast = Vector2Int.zero;
            classicBoxesDirty = true;

            // msg 0x22: the stored state and position; off screen (or none stored) → bottom left
            classicUnlocked = PlayerPrefs.GetInt(ClassicPrefsKey + "unlocked", 1) != 0;
            classicState = Mathf.Clamp(PlayerPrefs.GetInt(ClassicPrefsKey + "state", 1), 0, 15);
            int h = ClassicChatHeight(classicState);
            Vector2Int screen = system.ClientSize;
            int x = PlayerPrefs.GetInt(ClassicPrefsKey + "x", -1), y = PlayerPrefs.GetInt(ClassicPrefsKey + "y", -1);
            if (x < 0 || y < 0 || x > screen.x - ClassicChatWidth || y > screen.y - h) {
                x = 0;
                y = screen.y - h;
            }

            classicChat.SetHeight(h);
            classicChat.MoveTo(x, y);
            ClassicSetSize(classicState);
            CreateClassicSubWindows();   // OnCreate: one UISubChatHisWnd per separate record (0x9c7cd0)
        }

        private static RoClassicChildLayout ChatButton(int id, string path, int w, int h, string bitmap) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, W = w, H = h, Visible = false };
            if (bitmap != null) {
                // basic_interface\<name>_a.bmp normal, _b.bmp hover and pressed
                button.States["normal"] = ChatUiRoot + bitmap + "_a.bmp";
                button.States["hover"] = ChatUiRoot + bitmap + "_b.bmp";
                button.States["pressed"] = ChatUiRoot + bitmap + "_b.bmp";
            }

            return button;
        }

        // ---- layout (SetSize 0x89e5f0, LayoutHeader 0x894370, LayoutInputRow 0x894500) ----

        private void ClassicSetSize(int n) {
            n = n < 16 ? n : 0;
            if (n < 0) n = 15;
            int h0 = classicChat.Height, h = ClassicChatHeight(n);
            classicState = n;
            classicChat.SetHeight(h);
            classicChat.MoveTo(classicChat.Position.x, classicChat.Position.y + h0 - h); // the bottom edge stays
            if (n > 0) {
                // every box Resize(592, H − 44): UpdateScroll, then the last lines in view
                classicBoxHeight = h - 44;
                foreach (ClassicChatBox box in classicBoxes) box.First = Math.Max(0, box.Lines.Count - ClassicVisibleLines);
            }

            ClassicLayoutHeader();
            ClassicLayoutInputRow();
            PlayerPrefs.SetInt(ClassicPrefsKey + "state", n);
        }

        /// <summary>msg 2 (size button, F10): forward 0 → 1 → 4 → 7 → 10 → 13 → 0; backward from 0 to 15.</summary>
        private void ClassicStepSize(bool forward) {
            CloseClassicPopup();   // SetSize 0x89e5f0 deletes an open 0x1a first
            int n = classicState, d = forward ? 1 : -1;
            int m = n != 0 ? n + 3 * d : d;
            ClassicSetSize(m > 0 ? 3 * (m / 3) + 1 : n != 0 ? 0 : m);
        }

        /// <summary>The header buttons right-aligned from the lock icon when dragging is unlocked, else only battle_option2.</summary>
        private void ClassicLayoutHeader() {
            bool header = classicState != 0;
            classicChat.SetChildVisible(classicWndMini, header && classicUnlocked);
            classicChat.SetChildVisible(classicBattleOption, header && classicUnlocked);
            classicChat.SetChildVisible(classicStickOff, header && classicUnlocked);
            classicChat.SetChildVisible(classicBattleOption2, header);
            int option2X;
            if (classicUnlocked) {
                int x = 580;
                foreach (RoClassicChildLayout b in new[] { classicWndMini, classicBattleOption, classicStickOff, classicBattleOption2 }) {
                    x -= b.W + 2;
                    classicChat.SetChildPosition(b.Path, x, 4);
                }

                option2X = x;
            } else {
                option2X = 580 - 9 - 2;
                classicChat.SetChildPosition(classicBattleOption2.Path, option2X, 4);
            }

            classicModeIconX = option2X - 14 - 2;
            classicChat.Invalidate();
        }

        private void ClassicLayoutInputRow() {
            bool on = chatInputVisible;
            classicAppliedInputOn = on;
            classicAppliedChatMode = ClassicChatMode;
            int h = ClassicChatHeight(classicState);
            classicWhisperEdit.Rect = new RectInt(6, h - 20, 90, 16);
            classicMessageEdit.Rect = new RectInt(110, h - 20, 460, 16);
            classicWhisperEdit.Visible = on;
            classicMessageEdit.Visible = on;
            // Esc in chat mode hides the row instead of closing windows
            classicWhisperEdit.HandlesEscape = classicMessageEdit.HandlesEscape = ClassicChatMode && on;
            classicChat.SetChildPosition(classicReceiver.Path, 97, h - 21);
            classicChat.SetChildPosition(classicSize.Path, 586, h - 17);
            classicChat.SetChildPosition(classicGroup.Path, 572, h - 17);
            classicChat.SetChildVisible(classicReceiver, on);
            classicChat.SetChildVisible(classicSize, on);
            classicChat.SetChildVisible(classicGroup, on);
            if (!on && (classicMessageEdit.HasFocus || classicWhisperEdit.HasFocus)) RoClassicTextInput.Focus(null);
            classicChat.Invalidate();
        }

        private void ClassicSetInputOn(bool on) {
            chatInputVisible = on;
            ClassicLayoutInputRow();
        }

        /// <summary>The receiving group's text colour on the chat edit (set by the group popup's choice).</summary>
        private void ClassicSyncInputColor() {
            Color32 color = classicGroupColor;
            if (classicMessageEdit.TextColor.Equals(color)) return;
            classicMessageEdit.TextColor = color;
            classicChat.Invalidate();
        }

        // ---- drawing ----

        /// <summary>
        /// DrawTransBox2 (vf43), under the window: per tab a black box, alpha 0x66 for the current one and 0x99 for the
        /// others, then the body box (4, 17, 592, H − 17) at 0x66 — only while the history is open.
        /// </summary>
        private void DrawClassicChatBoxes(RoClassicWindow w) {
            if (classicState == 0) return;
            int tabW = ClassicTabMemberWidth(w);
            for (int i = 0; i < tabs.Count; i++) {
                bool current = i == activeTabIndex;
                w.FillRect(4 + tabW * i, current ? 1 : 2, tabW - 1, current ? 16 : 15, current ? ClassicBoxActive : ClassicBoxInactive);
            }

            w.FillRect(4, 17, ClassicBoxWidth, classicChat.Height - 17, ClassicBoxActive);
        }

        /// <summary>OnDraw 0x88e470, then the tab control and the current history box (children drawn in their order).</summary>
        private void DrawClassicChat(RoClassicWindow w) {
            int h = w.Height;
            w.FillRect(4, h - 24, ClassicBoxWidth, 1, ClassicFrameGrey); // separator above the input row
            if (classicState != 0) {
                int tabsW = tabs.Count * ClassicTabMemberWidth(w);
                w.FillRect(3, 16, 1, h - 16, ClassicChatWhite);
                w.FillRect(tabsW + 3, 16, ClassicChatWidth - tabsW - 10, 1, ClassicChatWhite);
                w.FillRect(594, 16, 1, 1, ClassicFrameGrey);
                w.FillRect(595, 17, 1, 1, ClassicFrameGrey);
                w.FillRect(596, 18, 1, h - 18, ClassicFrameGrey);
            }

            if (chatInputVisible) w.DrawBitmap(0, h - 24, ChatUiRoot + "dialog_bg.bmp");
            if (classicState != 0) {
                if (ClassicChatMode) w.DrawBitmap(classicModeIconX, 4, ChatUiRoot + (chatInputVisible ? "chatmode_on.bmp" : "chatmode_off.bmp"));
                w.DrawBitmap(580, 4, ChatUiRoot + (classicUnlocked ? "unlock_dragwnd.bmp" : "lock_dragwnd.bmp"));
                DrawClassicTabs(w);
                DrawClassicHistory(w);
            }

            // the input row's grip (UIResizer +0x98, dialog_mid.bmp)
            if (chatInputVisible) w.DrawBitmap(3, h - 24, ChatUiRoot + "dialog_mid.bmp");
        }

        /// <summary>Tab width: the widest shown name (fontType 0, height 12) + 10, the same for every tab (the strip's last layout).</summary>
        private int ClassicTabCellWidth(RoClassicWindow w) => model.Strip.TabWidth(0);

        /// <summary>+0x100: the tab control's width (n·(W − 1) + 1) per tab.</summary>
        private int ClassicTabMemberWidth(RoClassicWindow w) {
            if (tabs.Count == 0) return 0;
            return model.Strip.ControlWidth / tabs.Count;
        }

        /// <summary>
        /// UITabControl style 1 (FUN_007e0fe0) at (3, 0): no body fill; each tab's border, white at y 0 when selected,
        /// grey at y 1 with a white line under it otherwise; the name centred in white, at y 2 / 3.
        /// </summary>
        private void DrawClassicTabs(RoClassicWindow w) {
            if (tabs.Count == 0) return;
            RoClassicGdiFont font = w.FontFor(0, 12);
            int cell = ClassicTabCellWidth(w);
            w.ClipTo(3, 0, tabs.Count * (cell - 1) + 1, 17);
            for (int pass = 0; pass < 2; pass++) {
                for (int i = 0; i < tabs.Count; i++) {
                    bool selected = i == activeTabIndex;
                    if (selected != (pass == 1)) continue;
                    int x = 3 + i * (cell - 1), y = selected ? 0 : 1;
                    Color32 c = selected ? ClassicChatWhite : ClassicFrameGrey;
                    w.FillRect(x + 2, y, cell - 4, 1, c);
                    w.FillRect(x + 1, y + 1, 1, 1, c);
                    w.FillRect(x, y + 2, 1, 15, c);
                    w.FillRect(x + cell - 2, y + 1, 1, 1, c);
                    w.FillRect(x + cell - 1, y + 2, 1, 15, c);
                    if (!selected) w.FillRect(x, 16, cell, 1, ClassicChatWhite);
                    string name = model.Strip.Shown(i);
                    int tw = font.Measure(name);
                    w.TextOut(x + (cell - tw) / 2 + (cell - tw) % 2, selected ? 2 : 3, name, 0, 12, ClassicChatWhite);
                }
            }

            w.ResetClip();
        }

        /// <summary>
        /// UIChatHisBox OnDraw at (5, 18): each visible line with a black drop shadow at (3, 14k + 4) and in its colour at
        /// (2, 14k + 3); then its UIScrollBar2 at (w − 13, 0) when there is more than fits.
        /// </summary>
        private void DrawClassicHistory(RoClassicWindow w) {
            ClassicChatBox box = ClassicCurrentBox();
            if (box == null) return;
            int bh = classicBoxHeight, visible = ClassicVisibleLines;
            int last = Math.Min(box.Lines.Count, visible + box.First);
            w.ClipTo(5, 18, ClassicBoxWidth, bh);
            for (int i = box.First, k = 0; i < last; i++, k++) {
                ClassicChatLine line = box.Lines[i];
                w.TextOut(5 + 3, 18 + ClassicLineHeight * k + 4, line.Text, 0, 12, ClassicChatBlack);
                w.TextOut(5 + 2, 18 + ClassicLineHeight * k + 3, line.Text, 0, 12, line.Color);
                if (line.ItemLinks != null) {
                    // UIItemTagButton 0x7ec700: each label again over its glyphs in #E1F282, no underline
                    foreach ((RoClassicItemLink.Link l, int lx) in line.ItemLinks) w.TextOut(5 + 2 + lx, 18 + ClassicLineHeight * k + 3, l.Label, 0, 12, ClassicNaviLink);
                }

                if (line.NaviLabel != null) {
                    // the link button over its glyphs: the label in #E1F282, no underline
                    int at = line.Text.IndexOf(line.NaviLabel, StringComparison.Ordinal);
                    int lx = w.FontFor(0, 12).Measure(line.Text.Substring(0, at));
                    w.TextOut(5 + 2 + lx, 18 + ClassicLineHeight * k + 3, line.NaviLabel, 0, 12, ClassicNaviLink);
                }
            }

            int total = box.Lines.Count, range = total - visible;
            if (range > 0) {
                // UIScrollBar2 0x7ee970: black track, arrows, thumb max(8, (h − 20)·visible/total) at 11 + pos·(h − 20 − T)/range
                int bx = 5 + ClassicBoxWidth - 13, by = 18;
                w.ClipTo(bx, by, 12, bh);
                w.FillRect(bx, by + 11, 10, bh - 21, ClassicChatBlack);
                w.DrawBitmap(bx, by, ChatUiRoot + "dialscr_up.bmp");
                int thumb = Math.Max(8, (bh - 20) * visible / total);
                w.FillRect(bx, by + 10 + 1 + box.First * (bh - 20 - thumb) / range, 10, thumb - 1, ClassicThumb);
                w.DrawBitmap(bx, by + bh - 11, ChatUiRoot + "dialscr_down.bmp");
            }

            w.ResetClip();
        }

        // ---- history (msg 0x25, UIChatHisBox AddLine vf57) ----

        /// <summary>Every chat line is kept by the model and goes to the boxes (wrapped only while the window exists).</summary>
        private void ClassicOnLineAdded(RoChatChannel channel, string text, string colorHex) {
            if (string.IsNullOrEmpty(text) || (channel == RoChatChannel.System && text == RoChatModel.LegacyWindowTitle)) return;
            Color32 color = model.ResolveMessageColor(channel, colorHex, text, out string name, out bool allTabs);
            if (pendingName != null) name = pendingName;
            string shown = RoChatModel.StripColourCodes(text);
            if (shown.Length == 0) return;   // a text of colour codes only wraps into no line
            string naviLabel = null, naviPayload = null;
            List<RoClassicItemLink.Link> itemLinks = null;
            bool special = false;
            if (shown.IndexOf("<ITEML>", StringComparison.Ordinal) >= 0 || shown.IndexOf("</ITEML>", StringComparison.Ordinal) >= 0) {
                // vf57 → 0x7fec70: with both tags every decodable link becomes "<name>" (cut to half the box), a NAVIL stays raw;
                // with one tag only the text goes in raw as one line
                special = true;
                if (RoClassicItemLink.HasLinks(shown)) {
                    itemLinks = new List<RoClassicItemLink.Link>();
                    shown = RoClassicItemLink.Convert(shown, ResolveItemMetadata(), classicChat != null ? classicChat.FontFor(0, 12) : null, ClassicBoxWidth / 2, itemLinks);
                }
            } else if (shown.IndexOf("<NAVIL>", StringComparison.Ordinal) >= 0) {
                // 0x7ff130 → 0x796220: the first link as "<map x,y>" cut to half the box with "..."
                RoClassicGdiFont linkFont = classicChat != null ? classicChat.FontFor(0, 12) : null;
                if (RoClassicNavigation.ConvertNaviLine(shown, linkFont, ClassicBoxWidth / 2, out string converted, out naviLabel, out naviPayload)) shown = converted;
                else shown = converted;
                if (shown.Length == 0) return;
            }

            var raw = new RoChatModel.Entry(channel, shown, color, name, allTabs, naviLabel, naviPayload, itemLinks, special, pendingExtraFlag);
            // party and whisper lines (mgr msg 3 before their text): a hidden chat shows, a chat at size 0 opens at size 1
            if (channel == RoChatChannel.Party || channel == RoChatChannel.Whisper) ClassicPopOpen();
            model.Add(raw);
            if (classicChat != null) AppendClassicSubRaw(raw);   // the separate windows' tabs (msg 0x25's second loop)
            if (classicChat == null || classicBoxesDirty || classicBoxes.Count != tabs.Count) {
                classicBoxesDirty = true;
                return;
            }

            AppendClassicRaw(raw);
            classicChat.Invalidate();
        }

        private void ClassicClearHistory() {
            model.ClearEntries();
            classicBoxesDirty = true;
        }

        /// <summary>The tabs or their filters changed: the boxes are refilled from the kept messages.</summary>
        private void ClassicTabsChanged() => classicBoxesDirty = true;

        private void RebuildClassicBoxes() {
            classicBoxesDirty = false;
            foreach (ClassicSubChat sub in classicSubs) {
                if (sub != null) sub.Dirty = true;   // the kept messages changed for the separate windows too
            }

            classicBoxes.Clear();
            for (int i = 0; i < tabs.Count; i++) classicBoxes.Add(new ClassicChatBox());
            foreach (RoChatModel.Entry raw in model.Entries) AppendClassicRaw(raw);
            foreach (ClassicChatBox box in classicBoxes) box.First = Math.Max(0, box.Lines.Count - ClassicVisibleLines);
            classicChat.Invalidate();
        }

        private ClassicChatBox ClassicCurrentBox() =>
            activeTabIndex >= 0 && activeTabIndex < classicBoxes.Count ? classicBoxes[activeTabIndex] : null;

        /// <summary>
        /// msg 0x25: a tab takes the message when its filter has the type (or the message goes to every tab: type 0x18,
        /// or a red system message); the text is cut at 94 bytes (FUN_00915a50), and a piece wider than the box − 20
        /// is cut again by pixels at box − 16 (FUN_009b27a0).
        /// </summary>
        private void AppendClassicRaw(RoChatModel.Entry raw) {
            RoClassicGdiFont font = classicChat.FontFor(0, 12);
            for (int t = 0; t < tabs.Count && t < classicBoxes.Count; t++) {
                if (!model.Accepts(t, raw)) continue;
                ClassicChatBox box = classicBoxes[t];
                classicLinkCursor = 0;
                if (raw.Special) {
                    // 0x7fec70: pixel-wrapped at box − 16 with a "<label>" kept whole; one raw line without both tags
                    if (raw.ItemLinks != null && font.Measure(raw.Text) > ClassicBoxWidth - 20) PixelWrapAdd(box, font, raw.Text, ClassicBoxWidth - 16, raw);
                    else AddClassicBoxLine(box, raw.Text, raw);
                    continue;
                }

                if (raw.NaviLabel != null) {
                    // a line with a link is not byte-wrapped (0x7951d0), only cut by pixels
                    if (font.Measure(raw.Text) > ClassicBoxWidth - 20) PixelWrapAdd(box, font, raw.Text, ClassicBoxWidth - 16, raw);
                    else AddClassicBoxLine(box, raw.Text, raw);
                    continue;
                }

                RoClassicText.ByteWrap(raw.Text, ClassicByteWrap, classicWrap);
                foreach ((int start, int end) in classicWrap) {
                    string piece = raw.Text.Substring(start, end - start);
                    if (font.Measure(piece) > ClassicBoxWidth - 20) PixelWrapAdd(box, font, piece, ClassicBoxWidth - 16, raw);
                    else AddClassicBoxLine(box, piece, raw);
                }
            }
        }

        /// <summary>AddLine vf57 0x7d6610: drops the oldest line past 240 and follows the bottom when it was there.</summary>
        private void AddClassicBoxLine(ClassicChatBox box, string text, RoChatModel.Entry raw) {
            int visible = box.VisibleLines > 0 ? box.VisibleLines : ClassicVisibleLines;
            bool atBottom = box.Lines.Count <= visible || box.First == box.Lines.Count - visible;
            if (box.Lines.Count >= ClassicBoxLineLimit) box.Lines.RemoveAt(0);
            bool link = raw.NaviLabel != null && text.IndexOf(raw.NaviLabel, StringComparison.Ordinal) >= 0;
            (RoClassicItemLink.Link, int)[] items = null;
            if (raw.ItemLinks != null && raw.ItemLinks.Count > 0) {
                // 0x7e3b00: the next links whose labels lie on this line, x = the width before each
                var found = new List<(RoClassicItemLink.Link, int)>();
                int from = 0;
                RoClassicGdiFont lineFont = classicChat.FontFor(0, 12);
                while (classicLinkCursor < raw.ItemLinks.Count) {
                    RoClassicItemLink.Link l = raw.ItemLinks[classicLinkCursor];
                    int at = text.IndexOf(l.Label, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    found.Add((l, lineFont.Measure(text.Substring(0, at))));
                    from = at + l.Label.Length;
                    classicLinkCursor++;
                }

                if (found.Count > 0) items = found.ToArray();
            }

            box.Lines.Add(new ClassicChatLine(text, raw.Color, raw.Name, link ? raw.NaviLabel : null, link ? raw.NaviPayload : null, items));
            int max = Math.Max(0, box.Lines.Count - visible);
            box.First = atBottom ? max : Math.Min(box.First, max);
        }

        /// <summary>
        /// FUN_009b27a0: per-character widths summed, a break before the character that makes the line wider than
        /// <paramref name="maxWidth"/>; a "[...]" group that would cross the limit starts the next line whole.
        /// </summary>
        private void PixelWrapAdd(ClassicChatBox box, RoClassicGdiFont font, string s, int maxWidth, RoChatModel.Entry raw) {
            int start = 0, width = 0;
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                if (c == '\r' || c == '\n') {
                    AddClassicBoxLine(box, i > start ? s.Substring(start, i - start) : " ", raw);
                    start = i + 1;
                    width = 0;
                    continue;
                }

                if (c == '<' && i > start && raw.ItemLinks != null) {
                    foreach (RoClassicItemLink.Link l in raw.ItemLinks) {
                        if (string.CompareOrdinal(s, i, l.Label, 0, l.Label.Length) != 0) continue;
                        int group = ClassicTextWidth(font, s, i, i + l.Label.Length);
                        if (width + group > maxWidth && group <= maxWidth) {
                            AddClassicBoxLine(box, s.Substring(start, i - start), raw);
                            start = i;
                            width = 0;
                        }

                        break;
                    }
                }

                if (c == '[' && i > start) {
                    int close = s.IndexOf(']', i);
                    if (close > 0) {
                        int group = ClassicTextWidth(font, s, i, close + 1);
                        if (width + group > maxWidth && group <= maxWidth) {
                            AddClassicBoxLine(box, s.Substring(start, i - start), raw);
                            start = i;
                            width = 0;
                        }
                    }
                }

                font.GetGlyph(c, out RoClassicGdiFont.Glyph g);
                if (width + g.Advance > maxWidth && i > start) {
                    AddClassicBoxLine(box, s.Substring(start, i - start), raw);
                    start = i;
                    width = 0;
                }

                width += g.Advance;
            }

            if (start < s.Length) AddClassicBoxLine(box, s.Substring(start), raw);
        }

        private static int ClassicTextWidth(RoClassicGdiFont font, string s, int from, int to) {
            int width = 0;
            for (int i = from; i < to; i++) {
                font.GetGlyph(s[i], out RoClassicGdiFont.Glyph g);
                width += g.Advance;
            }

            return width;
        }

        // ---- tabs: add, rename, keep (chat msg 6 / 0x172, UITabControl 0x7faa00, ChatWndInfo_U.lua) ----

        private RoClassicEditField classicTabRename;

        /// <summary>
        /// The "+" button (0x899581): with 10 tabs in the main window and the separate ones MsgStr 0x62a "Cannot add more."
        /// in a box; else "NewTab_&lt;tabs + 1&gt;" with every flag off — into a new separate chat window (UISubChatHisWnd at
        /// the cascade place) when one more tab of the current width would make the strip wider than W − 90, else into the
        /// strip as the current tab.
        /// </summary>
        private void AddClassicTab() {
            EndClassicTabRename();
            if (model.TotalTabCount >= RoChatModel.MaxTabs) {
                RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x62a, "Cannot add more."), RoClassicMessageBox.TypeOk, true, _ => { });
                return;
            }

            int member = classicChat != null ? ClassicTabMemberWidth(classicChat) : 0;
            bool separate = (tabs.Count + 1) * member > ClassicChatWidth - 90;
            if (!model.AddTab(separate)) return;
            if (separate) {
                CreateClassicSub(model.SubTabCount - 1, -1, -1, 0, 0);
                SaveClassicTabs();
                return;
            }

            ClassicTabsChanged();
            SaveClassicTabs();
            classicChat?.Invalidate();
            RoClassicBattleMsgOption.FollowTab(this, activeTabIndex);
        }

        /// <summary>vf26 0x7f14b0: the edit over the current tab (x + 2, 2, width − 4, 15) with its name, focused, all selected.</summary>
        private void BeginClassicTabRename() {
            if (classicTabRename == null || activeTabIndex < 0 || activeTabIndex >= tabs.Count) return;
            int cell = ClassicTabCellWidth(classicChat);
            classicTabRename.Rect = new RectInt(3 + activeTabIndex * (cell - 1) + 2, 2, Math.Max(20, cell - 4), 15);
            classicTabRename.Visible = true;
            classicTabRename.SetText(model.Strip.Shown(activeTabIndex));   // the shown name (+0xb4), cut or not
            classicTabRename.Focus();
            classicChat.Invalidate();
        }

        /// <summary>
        /// Enter in the rename edit (0x7faa00): an empty name keeps the edit; a name that makes tabs · (10 + width) reach
        /// W − 90 is refused with MsgStr 0x629 in a box; then the edit goes.
        /// </summary>
        private void CommitClassicTabRename() {
            string name = classicTabRename.Text.Trim();
            if (name.Length == 0) return;
            RoClassicGdiFont font = classicChat.FontFor(0, 12);
            if (model.RenameTab(activeTabIndex, name, font.Measure, out bool tooWide)) {
                SaveClassicTabs();
                RoClassicBattleMsgOption.FollowTab(this, activeTabIndex);
            } else if (tooWide) {
                RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x629, "Not very long to change the name of the specified tab."), RoClassicMessageBox.TypeOk, true, _ => { });
            }

            EndClassicTabRename();
        }

        private void EndClassicTabRename() {
            if (classicTabRename == null || !classicTabRename.Visible) return;
            classicTabRename.Visible = false;
            if (classicTabRename.HasFocus) {
                if (chatInputVisible) classicMessageEdit.Focus();
                else RoClassicTextInput.Focus(null);
            }

            classicChat?.Invalidate();
        }

        private const string ClassicTabsKey = ClassicPrefsKey + "tabs";

        /// <summary>
        /// The kept tabs: "ro.ui.classic.chat.tabs" = count, ".tab&lt;i&gt;.name" and ".tab&lt;i&gt;.flags" (option1..24 as '1' /
        /// '0'), ".tab&lt;i&gt;.state" (TabState: 1 "On" = the main window, 0 = a separate window; missing = 1) and its XPos /
        /// YPos / Width / Height as ".x" ".y" ".w" ".h".
        /// </summary>
        private static List<RoChatModel.SavedTab> LoadClassicTabs() {
            var saved = new List<RoChatModel.SavedTab>();
            int count = Mathf.Clamp(PlayerPrefs.GetInt(ClassicTabsKey, 0), 0, RoChatModel.MaxTabs);
            for (int i = 0; i < count; i++) {
                string key = ClassicPrefsKey + "tab" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                saved.Add(new RoChatModel.SavedTab(PlayerPrefs.GetString(key + ".name", string.Empty), PlayerPrefs.GetString(key + ".flags", string.Empty),
                    PlayerPrefs.GetInt(key + ".state", 1) != 0, PlayerPrefs.GetInt(key + ".x", 0), PlayerPrefs.GetInt(key + ".y", 0),
                    PlayerPrefs.GetInt(key + ".w", 0), PlayerPrefs.GetInt(key + ".h", 0)));
            }

            return saved;
        }

        /// <summary>ChatWndInfo_U.lua's writer (0x894ef0) as prefs: the main tabs, then the separate ones with their rects, on each change.</summary>
        private void SaveClassicTabs() {
            int old = PlayerPrefs.GetInt(ClassicTabsKey, 0);
            List<RoChatModel.SavedTab> records = model.SaveTabs();
            for (int i = 0; i < records.Count; i++) {
                string key = ClassicPrefsKey + "tab" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                RoChatModel.SavedTab record = records[i];
                PlayerPrefs.SetString(key + ".name", record.Name);
                PlayerPrefs.SetString(key + ".flags", record.Flags);
                PlayerPrefs.SetInt(key + ".state", record.Main ? 1 : 0);
                PlayerPrefs.SetInt(key + ".x", record.X);
                PlayerPrefs.SetInt(key + ".y", record.Y);
                PlayerPrefs.SetInt(key + ".w", record.Width);
                PlayerPrefs.SetInt(key + ".h", record.Height);
            }

            for (int i = records.Count; i < old; i++) {
                string key = ClassicPrefsKey + "tab" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                foreach (string field in new[] { ".name", ".flags", ".state", ".x", ".y", ".w", ".h" }) PlayerPrefs.DeleteKey(key + field);
            }

            PlayerPrefs.SetInt(ClassicTabsKey, records.Count);
        }

        // ---- mouse (LBtnDown 0x891b40, tab control, history box) ----

        private bool InClassicHistory(Vector2Int local) =>
            classicState != 0 && local.x >= 5 && local.x < 5 + ClassicBoxWidth && local.y >= 18 && local.y < 18 + classicBoxHeight;

        /// <summary>UIFrameWnd drag while unlocked: outside y 1..20 and the controls, and the header beside the tabs.</summary>
        /// <summary>
        /// LBtnDown 0x891b40: only presses outside y 1..20 reach UIFrameWnd 0x819510 (the header band never drags), and the
        /// controls take theirs first: the history box, the top resizer strip (x 3..576, y 16..21); MouseMove 0x8934f0
        /// moves the window only while unlocked.
        /// </summary>
        private bool ClassicChatDragArea(Vector2Int local) =>
            classicUnlocked && (local.y < 1 || local.y > 20) && !InClassicHistory(local) && !(local.y <= 21 && local.x >= 3 && local.x <= 576);

        private void OnClassicChatPressed(RoClassicWindow window, Vector2Int local) {
            if (classicState != 0 && tabs.Count > 0 && local.y < 17 && local.x >= 3 && local.x < 3 + model.Strip.ControlWidth) {
                // UITabControl vf25 0x7f1a90: the tab under the press (0x7e2510) becomes current, the control takes the mouse
                int i = model.Strip.HitTest(local.x - 3);
                if (i >= 0 && i < tabs.Count) {
                    SelectClassicTab(i);
                    classicTabCaptured = true;
                    classicTabMouse = RoClassicUiSystem.Instance != null ? RoClassicUiSystem.Instance.Windows.Mouse : window.Position + local;
                }

                return;
            }

            if (classicState != 0 && local.y >= 16 && local.y < 22 && local.x >= 3 && local.x < 577) {
                // the top UIResizer (mode 2, 574 × 6 at (3, 16)): a drag of the top edge resizes the history
                classicResizing = true;
                classicResizeStartY = RoClassicUiSystem.Instance != null ? RoClassicUiSystem.Instance.Windows.Mouse.y : 0;
                classicResizeStartH = classicChat.Height;
                return;
            }

            if (InClassicHistory(local)) {
                if (TryPressClassicNaviLink(local)) return;
                if (TryPressClassicItemLink(local)) return;
                ClassicHistoryPressed(local);
                return;
            }

            if (local.y < 1 || local.y > 20) {
                if (chatInputVisible) classicMessageEdit.Focus(); // SetFocus(+0x94)
                return;
            }

            if (classicState == 0 || local.y >= 14) return;
            if (local.x > 580 && local.x < 589) {
                classicUnlocked = !classicUnlocked;
                PlayerPrefs.SetInt(ClassicPrefsKey + "unlocked", classicUnlocked ? 1 : 0);
                ClassicLayoutHeader();
                foreach (ClassicSubChat sub in classicSubs) {
                    if (sub?.Window != null) LayoutClassicSubButtons(sub);   // the separate windows' 0x894470 too
                }
            } else if (ClassicChatMode && local.x > classicModeIconX && local.x < classicModeIconX + 14) {
                ClassicSetInputOn(!chatInputVisible);
            }
        }

        private bool classicResizing;
        private int classicResizeStartY, classicResizeStartH;

        /// <summary>
        /// Chat msg 0xe while the top resizer is dragged (asm 0x8999f9): v3 = the history's requested height (H − 24 + the
        /// drag up); v3 ≥ 72 → size min(15, (v3 − 72) / 14 + 1), else size 0 — the 14-px steps of SetSize. The bottom edge stays.
        /// </summary>
        private void TickClassicResize() {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            RoClassicUiSystem ui = RoClassicUiSystem.Instance;
            if (mouse == null || ui == null || !mouse.leftButton.isPressed) {
                classicResizing = false;
                return;
            }

            int v3 = classicResizeStartH - 24 + (classicResizeStartY - ui.Windows.Mouse.y);
            if (v3 >= 72) {
                int m = Math.Min(15, (v3 - 72) / ClassicLineHeight + 1);
                if (m != classicState) ClassicSetSize(m);
            } else if (classicState != 0) {
                ClassicSetSize(0);
                classicResizing = false;
            }
#else
            classicResizing = false;
#endif
        }

        /// <summary>The tab control's selection → msg 0x16: that tab's box shows.</summary>
        private void SelectClassicTab(int index) {
            if (index != activeTabIndex) EndClassicTabRename();
            if (!model.SelectTab(index)) return;
            classicChat.Invalidate();
            RoClassicBattleMsgOption.FollowTab(this, index); // SendMsg(6, 0xbd, tab, 1, 2)
        }

        /// <summary>UIScrollBar2: the arrows scroll a line, the track a page; elsewhere the box only selects a line.</summary>
        private void ClassicHistoryPressed(Vector2Int local) {
            ClassicChatBox box = ClassicCurrentBox();
            if (box == null) return;
            int visible = ClassicVisibleLines, range = box.Lines.Count - visible;
            int bx = 5 + ClassicBoxWidth - 13, by = local.y - 18;
            if (range <= 0 || local.x < bx || local.x >= bx + 10) return;
            if (by < 10) {
                ScrollClassicHistory(-1);
            } else if (by >= classicBoxHeight - 11) {
                ScrollClassicHistory(1);
            } else {
                int thumb = Math.Max(8, (classicBoxHeight - 20) * visible / box.Lines.Count);
                int top = 11 + box.First * (classicBoxHeight - 20 - thumb) / range;
                if (by < top) ScrollClassicHistory(-(visible - 1));
                else if (by >= top + thumb) ScrollClassicHistory(visible - 1);
            }
        }

        private void ScrollClassicHistory(int lines) {
            ClassicChatBox box = ClassicCurrentBox();
            if (box == null) return;
            int first = Mathf.Clamp(box.First + lines, 0, Math.Max(0, box.Lines.Count - ClassicVisibleLines));
            if (first == box.First) return;
            box.First = first;
            classicChat.Invalidate();
        }

        private int classicLinkCursor;

        /// <summary>The item link button (label width + 1 by 14) under a history point, or false.</summary>
        private bool ClassicItemLinkAt(Vector2Int local, out RoClassicItemLink.Link link, out int x0, out int y0) {
            link = default;
            x0 = y0 = 0;
            ClassicChatBox box = ClassicCurrentBox();
            if (box == null || local.y < 18) return false;
            int row = (local.y - 18) / ClassicLineHeight, index = box.First + row;
            if (index < 0 || index >= box.Lines.Count || row >= ClassicVisibleLines) return false;
            ClassicChatLine line = box.Lines[index];
            if (line.ItemLinks == null) return false;
            RoClassicGdiFont font = classicChat.FontFor(0, 12);
            foreach ((RoClassicItemLink.Link l, int lx) in line.ItemLinks) {
                int bx = 5 + 2 + lx - 1, by = 18 + ClassicLineHeight * row + 2;
                if (local.x >= bx && local.x < bx + font.Measure(l.Label) + 1 && local.y >= by && local.y < by + 14) {
                    link = l;
                    x0 = bx;
                    y0 = by;
                    return true;
                }
            }

            return false;
        }

        /// <summary>UIItemTagButton vf25 0x7f1640 → msg 0x62 0x1cc: on press, the item description (window 12) of the link's item.</summary>
        private bool TryPressClassicItemLink(Vector2Int local) {
            if (!ClassicItemLinkAt(local, out RoClassicItemLink.Link link, out _, out _)) return false;
            RoClassicItemDescription.Toggle(link.Item, ResolveItemMetadata());
            return true;
        }

        /// <summary>vf28 0x7f2bd0: a truncated label shows its full name in the tooltip at the button + (0, −20).</summary>
        private void UpdateClassicItemLinkHover() {
            if (classicChat == null || !classicChat.Visible || RoClassicUiSystem.Instance == null) return;
            Vector2Int mouse = RoClassicUiSystem.Instance.Windows.Mouse;
            Vector2Int local = mouse - classicChat.Position;
            if (!InClassicHistory(local) || !ClassicItemLinkAt(local, out RoClassicItemLink.Link link, out int bx, out int by) || !link.Truncated) return;
            RoClassicTooltip.Shared.Show(link.Name, new Color32(255, 255, 255, 255), classicChat.Position.x + bx, classicChat.Position.y + by - 20);
        }

        /// <summary>UINaviLinkButton vf25 0x7f1810 → 0x7f4190: a press on a link selects its location in the navigation.</summary>
        private bool TryPressClassicNaviLink(Vector2Int local) {
            ClassicChatBox box = ClassicCurrentBox();
            if (box == null) return false;
            int index = box.First + (local.y - 18) / ClassicLineHeight;
            if (index < 0 || index >= box.Lines.Count) return false;
            ClassicChatLine line = box.Lines[index];
            if (line.NaviLabel == null) return false;
            RoClassicGdiFont font = classicChat.FontFor(0, 12);
            int at = line.Text.IndexOf(line.NaviLabel, StringComparison.Ordinal);
            int x0 = 5 + 2 + font.Measure(line.Text.Substring(0, at)), x1 = x0 + font.Measure(line.NaviLabel) + 1;
            if (local.x < x0 || local.x >= x1) return false;
            RoClassicNavigation.SelectLocation(line.NaviPayload);
            return true;
        }

        /// <summary>Msg 0x99 keeps one navigation link per input: an earlier one's label goes from the text and the list.</summary>
        private void RemoveClassicNaviLink() {
            for (int i = classicItemLinks.Count - 1; i >= 0; i--) {
                (string display, string raw) = classicItemLinks[i];
                if (raw.IndexOf("<NAVIL>", StringComparison.Ordinal) < 0) continue;
                classicItemLinks.RemoveAt(i);
                if (classicMessageEdit != null) {
                    string text = classicMessageEdit.Text;
                    int at = text.IndexOf(display, StringComparison.Ordinal);
                    if (at >= 0) classicMessageEdit.SetText((text.Substring(0, at) + text.Substring(at + display.Length)).TrimEnd() + (at > 0 ? " " : string.Empty));
                }
            }
        }

        /// <summary>Double click on a line with a sender: the name goes to the whisper box.</summary>
        private void OnClassicChatDoubleClicked(RoClassicWindow window, Vector2Int local) {
            if (classicState != 0 && tabs.Count > 0 && local.y < 17 && local.x >= 3) {
                // UITabControl vf26 (0x7f14b0): a double click renames the current tab
                if (model.Strip.HitTest(local.x - 3) == activeTabIndex) BeginClassicTabRename();
                return;
            }

            ClassicChatBox box = ClassicCurrentBox();
            if (box == null || !InClassicHistory(local)) return;
            int line = box.First + (local.y - 18) / ClassicLineHeight;
            if (line < 0 || line >= box.Lines.Count || box.Lines[line].Name.Length == 0) return;
            ClassicWhisperFromLine(box.Lines[line].Name);   // msg 6 / 0x157 → 0x9bb6e0
        }

        /// <summary>Wheel over the box: three lines per notch.</summary>
        private void OnClassicChatWheel(RoClassicWindow window, Vector2Int local, int delta) {
            if (InClassicHistory(local)) ScrollClassicHistory(delta < 0 ? 3 : -3);
        }

        private void OnClassicChatClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case ClassicSizeId:
                    ClassicStepSize(true);
                    break;
                case ClassicBattleOption2Id:
                    RoClassicBattleMsgOption.Toggle(this, activeTabIndex); // UIBattleMsgOptionWnd 0x84 for this tab
                    break;
                case ClassicBattleOptionId:
                    AddClassicTab();
                    break;
                case ClassicWndMiniId:
                    // id 0xc6 (0x89980d): the current tab goes, no question asked
                    EndClassicTabRename();
                    if (model.RemoveActiveTab()) {
                        ClassicTabsChanged();
                        SaveClassicTabs();
                        RoClassicBattleMsgOption.FollowTab(this, activeTabIndex);
                    }

                    break;
                case ClassicReceiverId:
                    OpenClassicReceiverPopup();
                    break;
                case ClassicGroupId:
                    OpenClassicGroupPopup();
                    break;
            }
        }

        // ---- keyboard and send (RUNTIME-RULES-CHAT §2) ----

        private void ProcessClassicChatKeys() {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || RoClassicMessageBox.ModalOpen) return; // Enter is the box's
            RoClassicEditField focused = RoClassicTextInput.Focused;
            if (focused != null && focused != classicMessageEdit && focused != classicWhisperEdit) return; // another window types
            bool modifiers = keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed || keyboard.shiftKey.isPressed;
            if (keyboard.f10Key.wasPressedThisFrame && !modifiers && !RoClassicShortcutSettings.ShouldSuppressGameplayHotkeys) ClassicSizeHotkey();
            bool npcFirst = focused == null && RoClassicTextInput.KeyboardClaimed;   // an NPC window is first in the Enter chain
            if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && !npcFirst) ClassicChatEnter();
#endif
        }

        /// <summary>Hotkey 0x76 (Interface 18 "Chatting Tab", Alt+F10; FUN_009dacd0(1, !shown)): the chat window hides or shows.</summary>
        public static void ToggleShown() {
            RoClassicChat chat = instance;
            if (chat?.classicChat == null) return;
            bool show = !chat.classicChat.Visible;
            if (show) chat.classicChat.CloseTabsSetShow(true);   // SetShow(1) also shows a chat F11 hid
            chat.classicChat.Visible = show;
            if (show) chat.ClassicLayoutInputRow();
        }

        /// <summary>Hotkey 0x77 (F10, FUN_009d4010): a hidden chat shows; else one size step, hiding the window at state 0.</summary>
        private void ClassicSizeHotkey() {
            if (!classicChat.Visible) {
                classicChat.CloseTabsSetShow(true);   // also a chat F11 hid
                classicChat.Visible = true;
                ClassicLayoutInputRow();
                return;
            }

            ClassicStepSize(true);
            if (classicState == 0) classicChat.Visible = false;
        }

        /// <summary>
        /// Enter, once per frame (the hotkey and the focused edit's commit both land here). Chat mode (FUN_00875a20):
        /// a hidden input row shows with an empty, focused chat edit; a visible row with an empty chat edit hides; else
        /// the line is sent (msg 6 / 0xb4, which with nothing typed only focuses the chat edit).
        /// </summary>
        private void ClassicChatEnter() {
            if (classicEnterFrame == Time.frameCount || classicChat == null || !classicChat.Visible) return;
            classicEnterFrame = Time.frameCount;
            if (RoClassicEmojiList.MiniKey(0x0d)) return;   // msg 0xb0 / Enter: the mini window takes its emotion
            if (ClassicPopupOpen) {
                // Enter (FUN_009bcff0): an open 0x1a picks its highlighted row
                PickClassicPopup();
                return;
            }
            if (ClassicChatMode) {
                if (!chatInputVisible) {
                    ClassicSetInputOn(true);
                    classicMessageEdit.SetText(string.Empty);
                    classicMessageEdit.Focus();
                    return;
                }

                if (classicMessageEdit.Text.Length == 0) {
                    ClassicSetInputOn(false);
                    return;
                }
            }

            ClassicSendFromEdit();
        }

        /// <summary>Esc in chat mode with the row visible hides it (the manager's Esc rule).</summary>
        private void OnClassicEditCancelled(RoClassicEditField field) {
            if (HideClassicEmotionMini()) return;   // Esc hides the cash emotion mini window first (0x9d3830)
            if (ClassicChatMode && chatInputVisible) ClassicSetInputOn(false);
        }

        /// <summary>msg 6 / 0xb4 (asm 0x899053): history push, the field emptied and focused, then whisper / command / chat.</summary>
        private void ClassicSendFromEdit() {
            if (!chatInputVisible && ClassicChatMode) return;
            string s = classicMessageEdit.Text;
            if (RefuseReservedClassicLine(s)) return;   // <ITEML> / <NAVIL> typed in: MsgStr 0xbe2
            int newline = s.IndexOf('\n');
            if (newline >= 0) s = s.Substring(0, newline);
            model.PushHistory(s);
            classicMessageEdit.SetText(string.Empty);
            classicMessageEdit.Focus();
            string message = RoChatModel.NormalizeSubmittedText(s);
            foreach ((string display, string raw) in classicItemLinks) {
                // the shown <name> back to its <ITEML> token
                int at = message.IndexOf(display, StringComparison.Ordinal);
                if (at >= 0) message = message.Substring(0, at) + raw + message.Substring(at + display.Length);
            }

            classicItemLinks.Clear();
            if (message.Length == 0 || model.Executor == null) return;

            // a whisper target (a name, or a server channel such as "#main" that the server routes) takes the plain lines;
            // a '/' line is a command even then (0x899053: the whisper test is "len && buf[0] != '/'")
            string whisper = classicWhisperEdit.Text.Trim();
            if (whisper.Length > 0 && message[0] != '/') {
                model.RememberWhisperName(whisper);
                model.Send(new RoChatModel.RoQueuedChatMessage(RoChatChannel.Whisper, message, whisper));
                return;
            }

            if (message[0] == '/') {
                RunSlashLine(message);
                return;
            }

            // /battlechat on: an ordinary line goes out as CZ_BATTLEFIELD_CHAT before the party / guild routing (0xc07f5d)
            if (RoClassicEntryQueue.TryBattleChat(message, session)) return;
            if (!ClassicGroupMessage(message, out RoChatModel.RoQueuedChatMessage queued)) return;
            model.Send(queued);
        }

        /// <summary>
        /// The chat room's edit (UIChatRoomWnd msg 6 / 0xb4 → mode Msg 0x2a): the line goes out like a chat line (a command
        /// stays a command), the server sending it to the room's members.
        /// </summary>
        public void SendClassicRoomLine(string text) {
            if (closed) {
                instance?.SendClassicRoomLine(text);
                return;
            }

            string message = RoChatModel.NormalizeSubmittedText(text ?? string.Empty);
            if (message.Length == 0 || model.Executor == null) return;
            if (message[0] == '/') {
                RunSlashLine(message);
                return;
            }

            model.Send(model.ResolveQueuedMessage(message));
        }

        /// <summary>UIChatEditCtrl msg 0x12 (Up, +1) / 0x13 (Down, −1): the draft is kept while browsing.</summary>
        private void RecallClassicHistory(int direction) {
            if (ClassicPopupKey(direction)) return;   // Up / Down go to an open 0x1a first
            if (model.RecallHistory(direction, classicMessageEdit.Text, out string text)) classicMessageEdit.SetText(text);
        }

        /// <summary>UIWhisperEditCtrl msg 0x12 / 0x13 over the names, an empty field past the newest.</summary>
        private void RecallClassicWhisperName(int direction) {
            if (ClassicPopupKey(direction)) return;
            if (!model.RecallWhisperName(direction, out string name)) return;
            classicWhisperEdit.SetText(name);
            classicWhisperEdit.SelectAll();
        }

        /// <summary>
        /// FUN_009bb880 (a 1:1 window made, an incoming whisper written to the main chat): an exact, case-sensitive check, a
        /// new name pushed without a cap; a cursor at the end stays at the end. The edit's text is left alone.
        /// </summary>
        public void AddClassicWhisperName(string name) {
            if (closed) {
                instance?.AddClassicWhisperName(name);
                return;
            }

            model.AddWhisperName(name);
        }

        /// <summary>An item dropped on the chat: its name at the end of the chat edit, sent as the item link token.</summary>
        private bool TryInsertClassicItemLink(string display, string raw) {
            if (!classicChatShown || classicMessageEdit == null) return false;
            if (!chatInputVisible) ClassicSetInputOn(true);
            string text = classicMessageEdit.Text;
            string add = (text.Length > 0 && !char.IsWhiteSpace(text[text.Length - 1]) ? " " : string.Empty) + display + " ";
            classicMessageEdit.SetText(text + add);
            classicMessageEdit.Focus();
            classicMessageEdit.MoveCaret(int.MaxValue, false);
            classicItemLinks.Add((display, raw));
            return true;
        }

        private void SetClassicWhisperTarget(string target) {
            if (classicWhisperEdit == null) return;
            classicWhisperEdit.SetText(target);
            model.RememberWhisperName(target);
        }

        // ---- diagnostics console (RoSkillSweepRunner "!chat ...") ----

        /// <summary>
        /// Drives the classic chat through the same handlers as the keyboard and the mouse (the sweep cannot type into
        /// the Game view): enter | esc | tab | up | down | f10 | type &lt;text&gt; | say &lt;line&gt; | press x y | dbl x y | wheel ±1 |
        /// button &lt;path&gt; | state. Returns the chat's state after the command.
        /// </summary>
        internal static string Sweep(string command) => instance != null ? instance.SweepClassicChat(command) : "no chat window";

        private string SweepClassicChat(string command) {
            if (classicChat == null) return "no classic chat";
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            string rest = a.Length > 1 ? a[1] : string.Empty;
            int Arg(int i) => int.Parse(rest.Split(' ')[i], System.Globalization.CultureInfo.InvariantCulture);
            RoClassicEditField focused = RoClassicTextInput.Focused;
            switch (a[0]) {
                case "enter":
                    if (focused != null) focused.Submit();
                    else ClassicChatEnter();
                    break;
                case "esc":
                    focused?.Cancel();
                    break;
                case "tab":
                    focused?.Tab();
                    break;
                case "up":
                    focused?.History(1);
                    break;
                case "down":
                    focused?.History(-1);
                    break;
                case "f10":
                    ClassicSizeHotkey();
                    break;
                case "type":
                    focused?.Insert(rest);
                    break;
                case "say":
                    // a whole line into the chat edit and sent as Enter would (commands run, chat goes out)
                    if (!chatInputVisible) ClassicSetInputOn(true);
                    classicMessageEdit.SetText(rest);
                    ClassicSendFromEdit();
                    break;
                case "popup":
                    return SweepClassicPopup(rest);
                case "sub":
                    return SweepClassicSubs(rest);
                case "move": {
                    // a test move is not the player's: the stored place stays (the Editor's prefs are shared)
                    int keptX = PlayerPrefs.GetInt(ClassicPrefsKey + "x", -1), keptY = PlayerPrefs.GetInt(ClassicPrefsKey + "y", -1);
                    classicChat.MoveTo(Arg(0), Arg(1));
                    PlayerPrefs.SetInt(ClassicPrefsKey + "x", keptX);
                    PlayerPrefs.SetInt(ClassicPrefsKey + "y", keptY);
                    break;
                }
                case "home":
                    // back to msg 0x22's default place, bottom left, and nothing stored
                    PlayerPrefs.DeleteKey(ClassicPrefsKey + "x");
                    PlayerPrefs.DeleteKey(ClassicPrefsKey + "y");
                    classicChat.MoveTo(0, RoClassicUiSystem.Instance.ClientSize.y - classicChat.Height);
                    PlayerPrefs.DeleteKey(ClassicPrefsKey + "x");
                    PlayerPrefs.DeleteKey(ClassicPrefsKey + "y");
                    break;
                case "dragarea":
                    return "dragarea=" + ClassicChatDragArea(new Vector2Int(Arg(0), Arg(1)));
                case "press":
                    var local = new Vector2Int(Arg(0), Arg(1));
                    RoClassicEditField edit = classicChat.EditFieldAt(classicChat.Position.x + local.x, classicChat.Position.y + local.y);
                    if (edit != null) edit.Focus();
                    else OnClassicChatPressed(classicChat, local);
                    break;
                case "dbl":
                    OnClassicChatDoubleClicked(classicChat, new Vector2Int(Arg(0), Arg(1)));
                    break;
                case "wheel":
                    OnClassicChatWheel(classicChat, new Vector2Int(100, 30), Arg(0));
                    break;
                case "button":
                    foreach (RoClassicChildLayout child in classicChat.Layout.Children) {
                        if (child.Path == rest) OnClassicChatClicked(child);
                    }

                    break;
            }

            ClassicChatBox box = ClassicCurrentBox();
            focused = RoClassicTextInput.Focused;
            string focus = focused == classicMessageEdit ? "message" : focused == classicWhisperEdit ? "whisper" : focused != null ? "other" : "none";
            return $"state={classicState} h={classicChat.Height} pos={classicChat.Position} visible={classicChat.Visible} input={chatInputVisible} " +
                $"focus={focus} msg='{classicMessageEdit.Text}' caret={classicMessageEdit.Caret} whisper='{classicWhisperEdit.Text}' tab={activeTabIndex}/{tabs.Count} " +
                $"lines={box?.Lines.Count ?? -1} first={box?.First ?? -1} last='{(box != null && box.Lines.Count > 0 ? box.Lines[box.Lines.Count - 1].Text : string.Empty)}' " +
                $"unlocked={classicUnlocked} hist={model.History.Count}@{model.HistoryIndex}";
        }

        private void DisposeClassicChat() {
            DisposeClassicSubs();
            if (classicChat == null) return;
            if (RoClassicTextInput.Focused == classicMessageEdit || RoClassicTextInput.Focused == classicWhisperEdit) RoClassicTextInput.Focus(null);
            classicChat.Destroy();
            classicChat = null;
            classicChatShown = false;
        }
    }
}
