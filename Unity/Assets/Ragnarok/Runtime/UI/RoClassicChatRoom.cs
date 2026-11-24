using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// The classic chat rooms (codex-ui-research RUNTIME-RULES-TRADE-CART-VENDING-CHATROOM.md "Chat rooms and actor title
    /// boards"): UIChatRoomMakeWnd 0x1b (the create form, 280 × 120 at (W/2 − 135, H·5/8)), UIChatRoomWnd 0x1c (the room:
    /// history, member list, edit; resizable 280..512 × 120..384 in 32 px steps), UIPasswordWnd 0x1d (a private room's
    /// password before joining), UIChatRoomChangeWnd 0x1e (the owner's Room Setup) and the form's UIComboBoxWnd 0x1a (the
    /// member limit / style list). The room follows the session's chat room state (ZC_ACK_CREATE_CHATROOM / ZC_ENTER_ROOM
    /// open it, the own ZC_MEMBER_EXIT / ZC_DESTROY_ROOM close it); the boards over the owners are drawn by
    /// <see cref="RoClassicActorBoards"/>. Ticked by the chat window while the classic UI is on.
    /// </summary>
    public sealed class RoClassicChatRoom : IRoClassicUiProducer {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string Ui = "유저인터페이스\\";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const string Prefs = "ro.ui.classic.chatroom.";
        private const int OkId = 0xb4;
        private const int CancelId = 0xb5;
        private const int CloseId = 0xc5;
        private const int CountButtonId = 0xe0;
        private const int StyleButtonId = 0xe1;
        private const int LineH = 14;
        private const int ComboRowH = 16;
        private const int ComboMaxH = 48;
        private static readonly int[] Limits = { 2, 3, 5, 10, 15, 20 };
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 EditFill = new Color32(0xf2, 0xf2, 0xf2, 255);
        private static readonly Color32 Separator = new Color32(0xd4, 0xd4, 0xd4, 255);
        private static readonly Color32 Selected = new Color32(0x70, 0x9f, 0xed, 255);
        private static readonly Color32 Entered = new Color32(0x00, 0x80, 0x80, 255);   // 0x00808000 (0xc2e4ee)
        private static readonly Color32 LeftColor = new Color32(0x80, 0x00, 0x00, 255); // 0x00000080 (0xc2e5f4)

        private static RoClassicChatRoom instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        /// <summary>The room window exists (mgr+0x27c): chat 0x8d / 0x8e then makes no talk balloons.</summary>
        public static bool RoomOpen => instance != null && instance.room != null && instance.room.Visible;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private int seenSequence = -1;
        private int seenCreateSerial;
        private int seenRefuseSerial;
        private RoChatRoomSnapshot state = RoChatRoomSnapshot.Empty;
        private readonly HashSet<string> knownMembers = new HashSet<string>();   // the room's member list as last seen (/tingonly)

        // 0x1c
        private RoClassicWindow room;
        private RoClassicEditField roomEdit;
        private readonly List<(string Text, Color32 Color)> wrapped = new List<(string, Color32)>();
        private int wrappedSerial = -1;
        private int wrappedWidth = -1;
        private int historyFirst;
        private int memberFirst;
        private string selectedMember;

        // 0x1b / 0x1e
        private RoClassicWindow makeForm;
        private RoClassicWindow changeForm;
        private RoClassicWindow form;
        private RoClassicEditField titleEdit;
        private RoClassicEditField passEdit;
        private readonly Dictionary<RoClassicWindow, (RoClassicEditField Title, RoClassicEditField Pass)> formEdits =
            new Dictionary<RoClassicWindow, (RoClassicEditField, RoClassicEditField)>();
        private bool formPrivate;
        private int formLimit = 20;

        // 0x1a
        private RoClassicWindow combo;
        private bool comboStyle;
        private int comboFirst;
        private int comboHover = -1;
        private int comboOpenedFrame;

        // 0x1d
        private RoClassicWindow passwordWin;
        private RoClassicEditField passwordEdit;
        private uint passwordChatId;

        private static RoClassicChatRoom Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicChatRoom { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Per frame from the chat window while the classic UI is on.</summary>
        public static void Tick(RoLoginShellSession session, RoClassicChat chat) {
            if (session == null) {
                instance?.HideAll();
                return;
            }

            RoClassicChatRoom r = Ensure();
            if (r == null) return;
            if (!ReferenceEquals(r.session, session)) {
                r.session = session;
                r.seenSequence = -1;
                r.seenCreateSerial = 0;
                r.seenRefuseSerial = 0;
            }

            r.chat = chat;
            int sequence = session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.ChatRoom);
            if (sequence == r.seenSequence) return;
            r.seenSequence = sequence;
            r.Apply(session.ChatRoomSnapshot());
        }

        private void HideAll() {
            CloseRoom();
            CloseForm();
            ClosePassword();
            RoClassicActorBoards.SyncChats(Array.Empty<RoChatRoomInfo>(), 0, null);
        }

        private void Apply(RoChatRoomSnapshot snapshot) {
            state = snapshot ?? RoChatRoomSnapshot.Empty;
            if (state.CreateSerial != seenCreateSerial) {
                seenCreateSerial = state.CreateSerial;
                // 0xc2e301: 0 MsgStr 0x40 in 0x00FFFF00 (the room opens), 1 0x41 / 2 0x42 in red
                switch (state.LastCreateResult) {
                    case 0: Chat(RoClassicUiHost.MsgStr(0x40, "Room has been successfully created."), "#00FFFF"); break;
                    case 1: Chat(RoClassicUiHost.MsgStr(0x41, "Room Limit Exceeded."), "#FF0000"); break;
                    case 2: Chat(RoClassicUiHost.MsgStr(0x42, "Same Room exists."), "#FF0000"); break;
                }
            }

            if (state.RefuseSerial != seenRefuseSerial) {
                seenRefuseSerial = state.RefuseSerial;
                // 0xc5f570 (ZC_REFUSE_ENTER_ROOM): reason → MsgStr, red; 3 says nothing
                int id = state.LastRefuseReason switch {
                    0 => 0x43,
                    1 => 0x07,
                    2 => 0x44,
                    4 => 0x37,
                    5 => 0x1b0,
                    6 => 0x1b1,
                    7 => 0x1b2,
                    _ => -1,
                };
                if (id >= 0) Chat(RoClassicUiHost.MsgStr(id, "You cannot enter the room."), "#FF0000");
            }

            if (state.InRoom) {
                OpenRoom();
                if (selectedMember != null && Array.FindIndex(state.Members, m => m.Name == selectedMember) < 0) selectedMember = null;
                memberFirst = Mathf.Clamp(memberFirst, 0, Math.Max(0, state.Members.Length - VisibleRows));
                PlayEntrySound();
                room.Invalidate();
            } else {
                // the own exit / the room's end deletes 0x1c and 0x1e (0xc2e4c0)
                knownMembers.Clear();
                CloseRoom();
                if (form == changeForm) CloseForm();
            }

            RoClassicActorBoards.SyncChats(BoardRooms(), session != null ? session.LocalActorId : 0, OnBoardDoubleClicked);
        }

        /// <summary>
        /// UIChatRoomWnd msg 0x2c (0x81bd7f), one per member put into the list (the entry list and each newcomer): a member
        /// that is not the owner sounds 버튼소리.wav (PlaySound3D 250 / 40) while /sound and /tingonly are both on.
        /// </summary>
        private void PlayEntrySound() {
            bool newcomer = false;
            foreach (RoChatRoomMember m in state.Members) {
                if (!m.IsOwner && !knownMembers.Contains(m.Name ?? string.Empty)) newcomer = true;
            }

            knownMembers.Clear();
            foreach (RoChatRoomMember m in state.Members) knownMembers.Add(m.Name ?? string.Empty);
            if (newcomer && RoClassicCommandFlags.TingOnly && RoChatSlashCommands.GetOption("/sound", RoClassicUiHost.ClientSettings)) {
                Ragnarok.Audio.RoAudioEvents.PlayButtonSound();
            }
        }

        /// <summary>
        /// The boards to draw: the server moves a room's board to its new owner only for those outside it (AREA_WOSC), so
        /// inside, the room's board follows its owner by name (ZC_ROLE_CHANGE names the new one).
        /// </summary>
        private RoChatRoomInfo[] BoardRooms() {
            RoChatRoomInfo[] rooms = state.Rooms;
            if (!state.InRoom || session == null) return rooms;
            string owner = null;
            foreach (RoChatRoomMember m in state.Members) {
                if (m.IsOwner) owner = m.Name;
            }

            if (owner == null) return rooms;
            uint aid = owner == session.SelectedCharacterName ? session.LocalActorId : FindActorByName(owner);
            if (aid == 0 || aid == state.Room.OwnerAid) return rooms;
            var moved = (RoChatRoomInfo[]) rooms.Clone();
            for (int i = 0; i < moved.Length; i++) {
                RoChatRoomInfo r = moved[i];
                if (r.ChatId == state.Room.ChatId && r.OwnerAid == state.Room.OwnerAid) moved[i] = new RoChatRoomInfo(aid, r.ChatId, r.Limit, r.Users, r.Type, r.Title);
            }

            return moved;
        }

        private readonly List<Ragnarok.World.RoOnlineActorSnapshot> actorScratch = new List<Ragnarok.World.RoOnlineActorSnapshot>();

        private uint FindActorByName(string name) {
            var world = UnityEngine.Object.FindFirstObjectByType<Ragnarok.World.RoOnlineWorldController>();
            if (world == null) return 0;
            actorScratch.Clear();
            world.SnapshotActors(actorScratch);
            foreach (Ragnarok.World.RoOnlineActorSnapshot actor in actorScratch) {
                if (actor.Name == name) return actor.ServerId;
            }

            return 0;
        }

        private void Chat(string text, string color) {
            if (string.IsNullOrEmpty(text)) return;
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, color);
        }

        private bool IsOwner {
            get {
                string me = session != null ? session.SelectedCharacterName : null;
                foreach (RoChatRoomMember m in state.Members) {
                    if (m.Name == me) return m.IsOwner;
                }

                return false;
            }
        }

        // ================================================================ 0x1c: the room

        private void EnsureRoom() {
            if (room != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIChatRoomWnd", WindowId = 0x1c, Width = 280, Height = 120 };
            var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = 266, Y = 3, W = 11, H = 11 };
            close.States["normal"] = Basic + "sys_close_off.bmp";
            close.States["hover"] = Basic + "sys_close_on.bmp";
            close.States["pressed"] = Basic + "sys_close_on.bmp";
            layout.Children.Add(close);
            room = new RoClassicWindow(system, layout) { Visible = false };
            room.DrawDynamic += DrawRoom;
            room.Clicked += child => {
                // X: mode Msg 0x30 → CZ_EXIT_ROOM; the window goes with the server's answer
                if (child.Id == CloseId && session != null) _ = session.BeginLegacyExitChatRoom();
            };
            room.Pressed += (w, local) => {
                int k = MemberAt(local);
                if (k >= 0 || InMemberList(local)) {
                    selectedMember = k >= 0 ? state.Members[k].Name : null;
                    room.Invalidate();
                }
            };
            room.RightClicked += (w, local) => {
                if (InMemberList(local)) {
                    int k = MemberAt(local);
                    selectedMember = k >= 0 ? state.Members[k].Name : null;
                    room.Invalidate();
                }

                OpenMemberMenu(system.Windows.Mouse);
            };
            room.Wheeled += (w, local, delta) => {
                if (InMemberList(local)) ScrollMembers(delta > 0 ? -1 : 1);
                else ScrollHistory(delta > 0 ? -1 : 1);
            };
            room.ResizerModeAt = local =>
                local.x >= room.Width - 15 && local.x < room.Width + 1 && local.y >= room.Height - 15 && local.y < room.Height + 1 ? 7 : -1;
            room.ResizeRequested += (w, mode, width, height) => {
                if (mode == 7) ApplyRoomSize(width, height, true);
            };
            room.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "y", w.Position.y);
            };
            roomEdit = new RoClassicEditField { Rect = new RectInt(3, 102, 250, 16), Background = EditFill, MaxBytes = 70 };
            roomEdit.Submitted += field => {
                // msg 6 / 0xb4 → mode Msg 0x2a: the line goes out as ordinary chat (the server routes it to the room)
                string text = field.Text;
                field.SetText(string.Empty);
                field.Focus();
                if (text.Trim().Length > 0) chat?.SendClassicRoomLine(text);
            };
            room.AddEditField(roomEdit);
            // msg 0x35: CHATROOMWNDINFO, else (500, 180, 280, 120)
            ApplyRoomSize(PlayerPrefs.GetInt(Prefs + "w", 280), PlayerPrefs.GetInt(Prefs + "h", 120), false);
        }

        /// <summary>msg 0xe (mode 7): the width clamped to 280..512 and the height to 120..384, each in 32 px steps.</summary>
        public static Vector2Int SnapRoomSize(int w, int h) {
            int cw = Math.Max(280, Math.Min(512, w)), ch = Math.Max(120, Math.Min(384, h));
            return new Vector2Int(280 + (cw - 280) / 32 * 32, 120 + (ch - 120) / 32 * 32);
        }

        /// <summary>OnDraw 0x817130: the title bar, the body, the line left of the member list, the button bar.</summary>
        public static void BuildRoomFrame(List<RoClassicOp> ops, int w, int h) {
            ops.Clear();
            RoClassicFrames.TitleBar(ops, w, false);
            RoClassicFrames.Body(ops, w, h);
            RoClassicFrames.Fill(ops, ListX(w) - 1, 17, 1, h - 38, Separator);
            RoClassicFrames.ButtonBar(ops, w, h, 0);
        }

        // msg 0xe: the history (1, 17, w − w·56/280 − 3, h − 38), the members (w − w·56/280 − 1, 17, w·56/280, h − 38)
        private static int ListW(int w) => w * 56 / 280;
        private static int ListX(int w) => w - ListW(w) - 1;
        private static int HistoryW(int w) => w - ListW(w) - 3;

        private void ApplyRoomSize(int w, int h, bool save) {
            Vector2Int size = SnapRoomSize(w, h);
            room.Layout.Width = size.x;
            room.Layout.Height = size.y;
            room.SetHeight(size.y);
            BuildRoomFrame(room.Layout.Ops, size.x, size.y);
            room.SetChildPosition("close", size.x - 14, 3);
            roomEdit.Rect = new RectInt(3, size.y - 18, size.x - 30, 16);
            if (save) {
                PlayerPrefs.SetInt(Prefs + "w", size.x);
                PlayerPrefs.SetInt(Prefs + "h", size.y);
            }

            wrappedWidth = -1;
            room.Invalidate();
        }

        private int VisibleRows => room == null ? 5 : Math.Max(1, (room.Height - 38) / LineH);

        private void OpenRoom() {
            EnsureRoom();
            if (room.Visible) return;
            historyFirst = 0;
            memberFirst = 0;
            selectedMember = null;
            wrappedSerial = -1;
            roomEdit.SetText(string.Empty);
            Vector2Int client = system.ClientSize;
            int x = PlayerPrefs.GetInt(Prefs + "x", 500), y = PlayerPrefs.GetInt(Prefs + "y", 180);
            room.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - room.Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - room.Height)));
            room.Visible = true;
            system.Windows.BringToFront(room);
            roomEdit.Focus();
        }

        private void CloseRoom() {
            if (room == null || !room.Visible) return;
            if (RoClassicTextInput.Focused == roomEdit) RoClassicTextInput.Focus(null);
            room.Visible = false;
            if (RoClassicMenu.IsOpen) RoClassicMenu.Close();
        }

        /// <summary>The history's lines wrapped to the pane (newest at the bottom while scrolled down).</summary>
        private void Rewrap(RoClassicGdiFont font) {
            int width = HistoryW(room.Width) - 17;
            if (wrappedSerial == state.HistorySerial && wrappedWidth == width) return;
            int rows = VisibleRows;
            bool atBottom = historyFirst >= Math.Max(0, wrapped.Count - rows);
            wrapped.Clear();
            foreach (RoChatRoomLine line in state.History) {
                string text;
                Color32 color;
                switch (line.Kind) {
                    case RoChatRoomLineKind.Entered:
                        text = Format(RoClassicUiHost.MsgStr(0xb3, "(%s) has entered."), line.Text);
                        color = Entered;
                        break;
                    case RoChatRoomLineKind.Left:
                        text = Format(RoClassicUiHost.MsgStr(0xb4, "(%s) has left."), line.Text);
                        color = LeftColor;
                        break;
                    case RoChatRoomLineKind.Kicked:
                        text = Format(RoClassicUiHost.MsgStr(0xb5, "(%s) was kicked out."), line.Text);
                        color = LeftColor;
                        break;
                    default:
                        text = line.Text;
                        color = Black;
                        break;
                }

                Wrap(font, text, width, color);
            }

            wrappedSerial = state.HistorySerial;
            wrappedWidth = width;
            int max = Math.Max(0, wrapped.Count - rows);
            historyFirst = atBottom ? max : Mathf.Clamp(historyFirst, 0, max);
        }

        private static string Format(string pattern, string name) {
            int at = pattern.IndexOf("%s", StringComparison.Ordinal);
            return at < 0 ? pattern + " " + name : pattern.Substring(0, at) + name + pattern.Substring(at + 2);
        }

        private void Wrap(RoClassicGdiFont font, string text, int width, Color32 color) {
            while (text.Length > 0) {
                if (font.Measure(text) <= width) {
                    wrapped.Add((text, color));
                    return;
                }

                int n = text.Length - 1;
                while (n > 1 && font.Measure(text.Substring(0, n)) > width) n--;
                int space = text.LastIndexOf(' ', n - 1, n);
                int cut = space > 0 ? space : n;
                wrapped.Add((text.Substring(0, cut), color));
                text = text.Substring(cut).TrimStart(' ');
            }
        }

        private void DrawRoom(RoClassicWindow w) {
            int W = w.Width, H = w.Height, rows = VisibleRows;
            RoClassicGdiFont font = w.FontFor(0, 12);
            // msg 0x22: "Pri:" / "Pub:" (MsgStr 0x59 / 0x5a by the room type), then "%s%s (%d/%d)"
            RoChatRoomInfo info = state.Room;
            string kind = RoClassicUiHost.MsgStr(info.IsPrivate ? 0x59 : 0x5a, info.IsPrivate ? "Pri:" : "Pub:");
            string title = string.Format(CultureInfo.InvariantCulture, "{0}{1} ({2}/{3})", kind, info.Title, info.Users, info.Limit);
            w.TextOut(9, 2, Fit(font, title, W - 30), 0, 12, Black);

            Rewrap(font);
            int historyW = HistoryW(W);
            for (int r = 0; r < rows && historyFirst + r < wrapped.Count; r++) {
                (string text, Color32 color) = wrapped[historyFirst + r];
                w.TextOut(4, 19 + LineH * r, text, 0, 12, color);
            }

            int range = Math.Max(0, wrapped.Count - rows);
            w.DrawScrollBar(1 + historyW - 13, 17, H - 38, range, wrapped.Count, rows, historyFirst);

            int listX = ListX(W), listW = ListW(W);
            for (int r = 0; r < rows && memberFirst + r < state.Members.Length; r++) {
                RoChatRoomMember m = state.Members[memberFirst + r];
                int y = 19 + LineH * r;
                if (m.Name == selectedMember) w.FillRect(listX + 1, y - 1, listW - 2, LineH, Selected);
                w.TextOut(listX + 3, y, Fit(font, m.Name, listW - 5), 0, 12, Black);
            }

            w.DrawBitmapRaw(W - 15, H - 15, Ui + "btn_resize.bmp");
        }

        private static string Fit(RoClassicGdiFont font, string text, int width) {
            if (font.Measure(text) <= width) return text;
            int n = text.Length;
            while (n > 0 && font.Measure(text.Substring(0, n)) > width) n--;
            return text.Substring(0, n);
        }

        private bool InMemberList(Vector2Int local) =>
            room != null && local.x >= ListX(room.Width) && local.x < room.Width - 1 && local.y >= 17 && local.y < room.Height - 21;

        private int MemberAt(Vector2Int local) {
            if (!InMemberList(local)) return -1;
            int k = memberFirst + (local.y - 18) / LineH;
            return k >= 0 && k < state.Members.Length ? k : -1;
        }

        private void ScrollHistory(int rows) {
            int first = Mathf.Clamp(historyFirst + rows, 0, Math.Max(0, wrapped.Count - VisibleRows));
            if (first == historyFirst) return;
            historyFirst = first;
            room.Invalidate();
        }

        private void ScrollMembers(int rows) {
            int first = Mathf.Clamp(memberFirst + rows, 0, Math.Max(0, state.Members.Length - VisibleRows));
            if (first == memberFirst) return;
            memberFirst = first;
            room.Invalidate();
        }

        /// <summary>
        /// msg 0x31 (asm 0x81b098–0x81b748): with a member picked, the owner gets Kick / Give Master (not for himself),
        /// everyone View Information and Register as a Friend (another, not yet a friend), the owner Room Setup; without
        /// one, the owner Room Setup and Save Chat, the others Save Chat.
        /// </summary>
        private void OpenMemberMenu(Vector2Int mouse) {
            var actions = new List<int>();
            RoClassicMenu.Begin();
            void Add(int msg, string fallback, int action) {
                RoClassicMenu.Add(RoClassicUiHost.MsgStr(msg, fallback));
                actions.Add(action);
            }

            bool owner = IsOwner;
            string me = session != null ? session.SelectedCharacterName : string.Empty;
            if (selectedMember != null) {
                bool other = selectedMember != me;
                if (owner && other) {
                    Add(0x7f, "Kick Character Out", 0);
                    Add(0x80, "Give Master Authority", 1);
                }

                Add(0x81, "View Information", 3);
                if (other && !IsFriend(selectedMember)) Add(0x166, "Register as a Friend", 0x15);
                if (owner) Add(0x7e, "Room Setup", 2);
            } else {
                if (owner) Add(0x7e, "Room Setup", 2);
                Add(0x138, "Save Chat as Text File", 0xb);
            }

            string target = selectedMember;
            RoClassicMenu.Open(mouse.x + 1, mouse.y + 1, pick => {
                if (pick >= 0 && pick < actions.Count) MenuAction(actions[pick], target);
            });
        }

        private bool IsFriend(string name) {
            if (session == null) return false;
            foreach (RoFriendRuntimeSnapshot f in session.FriendsSnapshot()) {
                if (f.Name == name) return true;
            }

            return false;
        }

        /// <summary>msg 0x30: 0 kick (0x00e2), 1 owner (0x00e0), 2 Room Setup, 3 MsgStr 0xbf, 0xb save, 0x15 friend (0x0202).</summary>
        private void MenuAction(int action, string member) {
            if (session == null) return;
            switch (action) {
                case 0:
                    if (member != null) _ = session.BeginLegacyChatRoomExpel(member);
                    break;
                case 1:
                    if (member != null) _ = session.BeginLegacyChatRoomOwner(member);
                    break;
                case 2:
                    OpenForm(true);
                    break;
                case 3:
                    Chat(RoClassicUiHost.MsgStr(0xbf, "This is not implemented yet."), "#FF0000");
                    break;
                case 0xb:
                    SaveChat();
                    break;
                case 0x15:
                    if (member != null) _ = session.BeginLegacyAddFriend(member);
                    break;
            }
        }

        /// <summary>FUN_00822280: the room's lines into a text file named after the title.</summary>
        private void SaveChat() {
            try {
                var name = new StringBuilder();
                foreach (char ch in state.Room.Title) name.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
                string dir = Path.Combine(Application.persistentDataPath, "Chat");
                Directory.CreateDirectory(dir);
                var text = new StringBuilder();
                foreach ((string line, Color32 _) in wrapped) text.AppendLine(line);
                File.WriteAllText(Path.Combine(dir, (name.Length > 0 ? name.ToString() : "chat") + ".txt"), text.ToString());
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }

        // ================================================================ 0x1b / 0x1e: create / Room Setup

        /// <summary>
        /// The chat room key: closes the create form when it is open, else opens it (not while in a room; the vending
        /// windows refuse with MsgStr 0x24b).
        /// </summary>
        public static void ToggleCreate(RoLoginShellSession session) {
            if (session == null) return;
            RoClassicChatRoom r = Ensure();
            if (r == null) return;
            r.session = session;
            if (r.form != null && r.form.Visible && r.form == r.makeForm) {
                r.CloseForm();
                return;
            }

            if (r.room != null && r.room.Visible) return;
            if (session.VendingSnapshot().IsOwnStoreOpen) {
                r.Chat(RoClassicUiHost.MsgStr(0x24b, "Please avoid opening a chatroom while vending."), "#FF0000");
                return;
            }

            r.OpenForm(false);
        }

        private RoClassicWindow BuildForm(string layoutName) {
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + layoutName).text);
            foreach (RoClassicChildLayout child in layout.Children) {
                // the edits are drawn by the edit fields; the two labels take their texts per state (SyncLabels)
                if (child.Class == "UIEditCtrl") child.Visible = false;
            }

            var window = new RoClassicWindow(system, layout) { Visible = false };
            window.Clicked += child => {
                if (child.Id == OkId) FormOk();
                else if (child.Id == CancelId || child.Id == CloseId) CloseForm();
                else if (child.Id == CountButtonId) OpenCombo(false);
                else if (child.Id == StyleButtonId) OpenCombo(true);
            };
            window.Pressed += (w, local) => {
                foreach (RoClassicChildLayout child in w.Layout.Children) {
                    if (child.Class != "UIRadioBtn") continue;
                    if (local.x >= child.X && local.x < child.X + child.W && local.y >= child.Y && local.y < child.Y + child.H) {
                        // radio event 0xd3: the left one is Public (0), the right one Private (1)
                        formPrivate = child.X > 60;
                        SyncRadios(w);
                    }
                }
            };
            window.EscClose = () => {
                CloseForm();
                return true;
            };
            var title = new RoClassicEditField { Rect = new RectInt(45, 24, 228, 16), Background = EditFill, MaxBytes = 36 };
            var pass = new RoClassicEditField { Rect = new RectInt(182, 70, 90, 16), Background = EditFill, MaxBytes = 8, PasswordMask = '*' };
            title.Submitted += _ => FormOk();
            pass.Submitted += _ => FormOk();
            window.AddEditField(title);
            window.AddEditField(pass);
            formEdits[window] = (title, pass);
            return window;
        }

        private void SyncRadios(RoClassicWindow window) {
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                if (child.Class != "UIRadioBtn") continue;
                bool on = (child.X > 60) == formPrivate;
                foreach (RoClassicOp op in child.Ops) {
                    if (op.Kind == RoClassicOpKind.Bitmap && op.Path != null) op.Path = Ui + (on ? "radiobtn_on.bmp" : "radiobtn_off.bmp");
                }
            }

            window.Invalidate();
        }

        /// <summary>vf15 0x837950 (create: limit 20, style 0, Public) / 0x1e msg 0x22 (Room Setup: the room's title, limit, type).</summary>
        private void OpenForm(bool change) {
            if (change && !state.InRoom) return;
            CloseForm();
            if (change) {
                if (changeForm == null) changeForm = BuildForm("UIChatRoomChangeWnd");
                form = changeForm;
            } else {
                if (makeForm == null) makeForm = BuildForm("UIChatRoomMakeWnd");
                form = makeForm;
            }

            (titleEdit, passEdit) = formEdits[form];
            if (change) {
                titleEdit.SetText(state.Room.Title);
                formLimit = state.Room.Limit > 0 ? state.Room.Limit : 20;
                formPrivate = state.Room.IsPrivate;
            } else {
                titleEdit.SetText(string.Empty);
                formLimit = 20;
                formPrivate = false;
            }

            passEdit.SetText(string.Empty);
            SyncRadios(form);
            SyncLabels(form);
            Vector2Int client = system.ClientSize;
            form.MoveTo(Mathf.Clamp(client.x / 2 - 135, 0, Math.Max(0, client.x - form.Width)), Mathf.Clamp(client.y * 5 / 8, 0, Math.Max(0, client.y - form.Height)));
            form.Visible = true;
            system.Windows.BringToFront(form);
            system.Windows.PushEsc(form);
            titleEdit.Focus();
        }

        private void CloseForm() {
            CloseCombo();
            if (form == null) return;
            if (RoClassicTextInput.Focused == titleEdit || RoClassicTextInput.Focused == passEdit) RoClassicTextInput.Focus(null);
            form.Visible = false;
            system.Windows.RemoveEsc(form);
            form = null;
            if (room != null && room.Visible) roomEdit.Focus();
        }

        private static string LimitText(int limit) =>
            limit.ToString(CultureInfo.InvariantCulture) + " " + RoClassicUiHost.MsgStr(0x83, "Ppl");

        /// <summary>The labels (48, 49) and (187, 49): the limit "%d Ppl" and the style (one: MsgStr 0x82 "Chat Room").</summary>
        private void SyncLabels(RoClassicWindow window) {
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                if (child.Class != "UIStaticText") continue;
                foreach (RoClassicOp op in child.Ops) {
                    if (op.Kind == RoClassicOpKind.Text) op.Text = child.X < 100 ? LimitText(formLimit) : RoClassicUiHost.MsgStr(0x82, "Chat Room");
                }
            }

            window.Invalidate();
        }

        /// <summary>
        /// OK (vf37 0x858980 / 0x8583e0 case 6): a title is required (0x0d), a private room a password of 4 or more (0x0f
        /// empty, 0x10 shorter); then mode Msg 0x2b (CZ_CREATE_CHATROOM) / 0x2c (CZ_CHANGE_CHATROOM) and the form closes.
        /// </summary>
        private void FormOk() {
            if (form == null || session == null) return;
            bool change = form == changeForm;
            string title = titleEdit.Text.Trim();
            if (title.Length == 0) {
                Chat(RoClassicUiHost.MsgStr(0x0d, "Please Enter Room Title."), "#FF0000");
                return;
            }

            string password = passEdit.Text;
            if (formPrivate) {
                if (password.Length == 0) {
                    Chat(RoClassicUiHost.MsgStr(0x0f, "Please enter Password."), "#FF0000");
                    return;
                }

                if (password.Length < 4) {
                    Chat(RoClassicUiHost.MsgStr(0x10, "Please enter Password. Passwords must be at least 4 characters long."), "#FF0000");
                    return;
                }
            }

            int limit = formLimit;
            bool isPublic = !formPrivate;
            CloseForm();
            _ = session.BeginLegacyChatRoomForm(change, limit, isPublic, password, title);
        }

        // ================================================================ 0x1a: the form's list

        private void OpenCombo(bool style) {
            if (form == null) return;
            if (combo != null && combo.Visible && comboStyle == style) {
                CloseCombo();
                return;
            }

            if (combo == null) {
                combo = new RoClassicWindow(system, new RoClassicWindowLayout { Class = "UIComboBoxWnd", WindowId = 0x1a, Width = 89, Height = ComboMaxH }) {
                    Visible = false,
                    Draggable = false,
                    ClickableAt = _ => true,
                };
                combo.DrawDynamic += DrawCombo;
                combo.Pressed += (w, local) => {
                    int k = comboFirst + local.y / ComboRowH;
                    if (k < ComboCount) PickCombo(k);
                };
                combo.Wheeled += (w, local, delta) => {
                    comboFirst = Mathf.Clamp(comboFirst + (delta > 0 ? -1 : 1), 0, Math.Max(0, ComboCount - ComboRows));
                    combo.Invalidate();
                };
                combo.EscClose = () => {
                    CloseCombo();
                    return true;
                };
            }

            comboStyle = style;
            comboFirst = 0;
            comboHover = -1;
            // anchored under its label: the count (48, 49, 68, 14) up to its button (119 + 18), the style (187..274)
            int x = style ? 187 : 48, w = style ? 274 - 187 : 137 - 48;
            int h = Math.Min(ComboMaxH, ComboCount * ComboRowH);
            combo.Layout.Width = w;
            combo.Layout.Height = h;
            combo.SetHeight(h);
            combo.MoveTo(form.Position.x + x, form.Position.y + 63);
            comboOpenedFrame = Time.frameCount;
            combo.Visible = true;
            system.Windows.BringToFront(combo);
            system.Windows.PushEsc(combo);
            combo.Invalidate();
        }

        private int ComboCount => comboStyle ? 1 : Limits.Length;
        private int ComboRows => Math.Max(1, ComboMaxH / ComboRowH);

        private string ComboText(int k) => comboStyle ? RoClassicUiHost.MsgStr(0x82, "Chat Room") : LimitText(Limits[k]);

        private void DrawCombo(RoClassicWindow w) {
            int width = w.Width, height = w.Height;
            w.FillRect(0, 0, width, height, White);
            w.DrawBitmapRaw(0, 0, Basic + "sysboxs_lu.bmp");
            w.DrawBitmapRaw(0, height - 7, Basic + "sysboxs_ld.bmp");
            w.DrawBitmapRaw(width - 7, 0, Basic + "sysboxs_ru.bmp");
            w.DrawBitmapRaw(width - 7, height - 7, Basic + "sysboxs_rd.bmp");
            int rows = Math.Min(ComboRows, ComboCount);
            for (int r = 0; r < rows && comboFirst + r < ComboCount; r++) {
                int k = comboFirst + r;
                bool on = k == comboHover;
                if (on) w.FillRect(2, ComboRowH * r + 1, width - 4, ComboRowH - 2, Selected);
                w.TextOut(4, ComboRowH * r + 2, ComboText(k), 0, 12, on ? White : Black);
            }

            w.DrawScrollBar(width - 13, 0, height, Math.Max(0, ComboCount - ComboRows), ComboCount, ComboRows, comboFirst);
        }

        /// <summary>Selector callback 0x27: the limit (+0xd0) or the style (+0xd4) from the picked value.</summary>
        private void PickCombo(int k) {
            if (!comboStyle) formLimit = Limits[k];
            CloseCombo();
            if (form != null) SyncLabels(form);
        }

        private void CloseCombo() {
            if (combo == null || !combo.Visible) return;
            combo.Visible = false;
            system.Windows.RemoveEsc(combo);
        }

        // ================================================================ 0x1d: a private room's password

        /// <summary>Title board double-click (vf26 0x7bfe60): private → 0x1d; public / NPC with a limit → join; type 3 nothing.</summary>
        private void OnBoardDoubleClicked(RoChatRoomInfo info) {
            if (session == null || info.Type == 3) return;
            if (state.InRoom && state.Room.ChatId == info.ChatId) return;
            if (info.IsPrivate) {
                OpenPassword(info.ChatId);
                return;
            }

            _ = session.BeginLegacyEnterChatRoom(info.ChatId, string.Empty);
        }

        private void OpenPassword(uint chatId) {
            if (passwordWin == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIPasswordWnd").text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIEditCtrl") child.Visible = false;
                }

                passwordWin = new RoClassicWindow(system, layout) { Visible = false };
                passwordWin.Clicked += child => {
                    if (child.Id == OkId) PasswordOk();
                };
                passwordEdit = new RoClassicEditField { Rect = new RectInt(15, 22, 90, 16), Background = new Color32(0xe8, 0xe8, 0xe8, 255), MaxBytes = 8, PasswordMask = '*' };
                passwordEdit.Submitted += _ => PasswordOk();
                passwordWin.AddEditField(passwordEdit);
            }

            passwordChatId = chatId; // msg 0x2f
            passwordEdit.SetText(string.Empty);
            Vector2Int client = system.ClientSize;
            passwordWin.MoveTo(Mathf.Clamp(client.x / 2 - 120, 0, Math.Max(0, client.x - passwordWin.Width)), Mathf.Clamp(client.y * 200 / 480, 0, Math.Max(0, client.y - passwordWin.Height)));
            passwordWin.Visible = true;
            system.Windows.BringToFront(passwordWin);
            passwordEdit.Focus();
        }

        /// <summary>vf37 0x861450: mode Msg 0x2d (CZ_REQ_ENTER_ROOM with the password) and the window goes (no local check).</summary>
        private void PasswordOk() {
            if (passwordWin == null || !passwordWin.Visible) return;
            string password = passwordEdit.Text;
            ClosePassword();
            if (session != null) _ = session.BeginLegacyEnterChatRoom(passwordChatId, password);
        }

        private void ClosePassword() {
            if (passwordWin == null || !passwordWin.Visible) return;
            if (RoClassicTextInput.Focused == passwordEdit) RoClassicTextInput.Focus(null);
            passwordWin.Visible = false;
        }

        // ================================================================ per frame

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (combo == null || !combo.Visible) return;
            if (form == null || !form.Visible) {
                CloseCombo();
                return;
            }
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null && Time.frameCount != comboOpenedFrame &&
                (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) && ui.Windows.HoverWindow != combo) {
                CloseCombo();
                return;
            }
#endif
            int hover = ui.Windows.HoverWindow == combo ? comboFirst + (ui.Windows.Mouse.y - combo.Position.y) / ComboRowH : -1;
            if (hover != comboHover) {
                comboHover = hover;
                combo.Invalidate();
            }
        }

        // ================================================================ diagnostics

        /// <summary>
        /// The sweep console: form | create title limit pub|pri [password] | setup | join chatId [password] | password text |
        /// say text | leave | select name | menu | pick n | size w h | wheel n | state.
        /// </summary>
        public static string Sweep(string command) {
            RoClassicChatRoom r = Ensure();
            if (r == null) return "no classic ui";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            string Rest(int i) => a.Length > i ? string.Join(" ", a, i, a.Length - i) : string.Empty;
            switch (a[0]) {
                case "form":
                    if (r.session != null) ToggleCreate(r.session);
                    break;
                case "create":
                case "change":
                    // create <title> <limit> pub|pri [password]: as if typed into the form and OK pressed
                    r.OpenForm(a[0] == "change");
                    if (r.form != null) {
                        r.titleEdit.SetText(a.Length > 1 ? a[1].Replace('_', ' ') : string.Empty);
                        if (Arg(2) > 0) r.formLimit = Arg(2);
                        r.formPrivate = a.Length > 3 && a[3] == "pri";
                        r.passEdit.SetText(a.Length > 4 ? a[4] : string.Empty);
                        r.SyncRadios(r.form);
                        r.FormOk();
                    }

                    break;
                case "setup":
                    r.OpenForm(true);
                    break;
                case "combo":
                    r.OpenCombo(Arg(1) == 1);
                    break;
                case "combopick":
                    if (r.combo != null && r.combo.Visible && Arg(1) < r.ComboCount) r.PickCombo(Arg(1));
                    break;
                case "join":
                    if (uint.TryParse(a.Length > 1 ? a[1] : string.Empty, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint chatId)) {
                        foreach (RoChatRoomInfo info in r.state.Rooms) {
                            if (info.ChatId == chatId) r.OnBoardDoubleClicked(info);
                        }
                    }

                    break;
                case "password":
                    if (r.passwordWin != null && r.passwordWin.Visible) {
                        r.passwordEdit.SetText(Rest(1));
                        r.PasswordOk();
                    }

                    break;
                case "say":
                    r.chat?.SendClassicRoomLine(Rest(1));
                    break;
                case "leave":
                    if (r.session != null) _ = r.session.BeginLegacyExitChatRoom();
                    break;
                case "select":
                    r.selectedMember = a.Length > 1 ? Rest(1) : null;
                    r.room?.Invalidate();
                    break;
                case "menu":
                    if (r.room != null && r.room.Visible) r.OpenMemberMenu(r.room.Position + new Vector2Int(r.room.Width / 2, 40));
                    break;
                case "pick":
                    RoClassicMenu.PickForTests(Arg(1));
                    break;
                case "size":
                    if (r.room != null) r.ApplyRoomSize(Arg(1), Arg(2), true);
                    break;
                case "wheel":
                    if (r.room != null) r.ScrollHistory(Arg(1));
                    break;
            }

            RoChatRoomSnapshot s = r.state;
            var text = new StringBuilder();
            text.Append("inRoom=").Append(s.InRoom);
            if (s.InRoom) {
                text.Append(" room=").Append(s.Room.ChatId).Append(" '").Append(s.Room.Title).Append("' ").Append(s.Room.Users).Append('/').Append(s.Room.Limit)
                    .Append(" type=").Append(s.Room.Type).Append(" members=");
                foreach (RoChatRoomMember m in s.Members) text.Append(m.Name).Append(m.IsOwner ? "*" : string.Empty).Append(',');
                text.Append(" lines=").Append(s.History.Length).Append(" wrapped=").Append(r.wrapped.Count).Append(" first=").Append(r.historyFirst);
                if (r.wrapped.Count > 0) text.Append(" last='").Append(r.wrapped[r.wrapped.Count - 1].Text).Append('\'');
                text.Append(" size=").Append(r.room.Width).Append('x').Append(r.room.Height).Append(" sel=").Append(r.selectedMember ?? "-");
            }

            text.Append(" form=").Append(r.form == null ? "-" : r.form == r.changeForm ? "change" : "create");
            if (r.form != null) text.Append(" limit=").Append(r.formLimit).Append(r.formPrivate ? " pri" : " pub");
            text.Append(" combo=").Append(r.combo != null && r.combo.Visible ? (r.comboStyle ? "style" : "count") : "-");
            text.Append(" password=").Append(r.passwordWin != null && r.passwordWin.Visible ? r.passwordChatId.ToString(CultureInfo.InvariantCulture) : "-");
            text.Append(" create=").Append(s.LastCreateResult).Append('#').Append(s.CreateSerial).Append(" refuse=").Append(s.LastRefuseReason).Append('#').Append(s.RefuseSerial);
            text.Append(" boards=");
            foreach (RoChatRoomInfo info in s.Rooms) text.Append(info.ChatId).Append('@').Append(info.OwnerAid).Append(" '").Append(info.Title).Append("' ");
            text.Append(" menu=").Append(RoClassicMenu.Describe());
            return text.ToString().TrimEnd();
        }
    }
}
