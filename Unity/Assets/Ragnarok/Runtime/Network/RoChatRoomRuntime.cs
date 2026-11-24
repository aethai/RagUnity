using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    /// <summary>A chat room as the server announces it (ZC_ROOM_NEWENTRY / ZC_CHANGE_CHATROOM).</summary>
    public readonly struct RoChatRoomInfo {
        public RoChatRoomInfo(uint ownerAid, uint chatId, int limit, int users, byte type, string title) {
            OwnerAid = ownerAid;
            ChatId = chatId;
            Limit = limit;
            Users = users;
            Type = type;
            Title = title ?? string.Empty;
        }

        public uint OwnerAid { get; }
        public uint ChatId { get; }
        public int Limit { get; }
        public int Users { get; }
        /// <summary>0 private (password), 1 public, 2 NPC with a limit, 3 NPC without (clif_dispchat).</summary>
        public byte Type { get; }
        public string Title { get; }
        public bool IsPrivate => Type == 0;

        public RoChatRoomInfo WithUsers(int users) => new RoChatRoomInfo(OwnerAid, ChatId, Limit, users, Type, Title);
    }

    public readonly struct RoChatRoomMember {
        public RoChatRoomMember(string name, bool owner) {
            Name = name ?? string.Empty;
            IsOwner = owner;
        }

        public string Name { get; }
        public bool IsOwner { get; }
    }

    public enum RoChatRoomLineKind : byte {
        /// <summary>A chat line (the text as the server sent it, "name : message").</summary>
        Chat,
        /// <summary>ZC_MEMBER_NEWENTRY: the text is the name (MsgStr 0xb3 "(%s) has entered.").</summary>
        Entered,
        /// <summary>ZC_MEMBER_EXIT type 0: the name (0xb4 "(%s) has left.").</summary>
        Left,
        /// <summary>ZC_MEMBER_EXIT type 1: the name (0xb5 "(%s) was kicked out.").</summary>
        Kicked,
    }

    public readonly struct RoChatRoomLine {
        public RoChatRoomLine(RoChatRoomLineKind kind, string text) {
            Kind = kind;
            Text = text ?? string.Empty;
        }

        public RoChatRoomLineKind Kind { get; }
        public string Text { get; }
    }

    public sealed class RoChatRoomSnapshot {
        public static readonly RoChatRoomSnapshot Empty = new RoChatRoomSnapshot(
            Array.Empty<RoChatRoomInfo>(), false, default, Array.Empty<RoChatRoomMember>(), Array.Empty<RoChatRoomLine>(), -1, 0, -1, 0, 0, 0);

        public RoChatRoomSnapshot(RoChatRoomInfo[] rooms, bool inRoom, RoChatRoomInfo room, RoChatRoomMember[] members, RoChatRoomLine[] history,
            int lastCreateResult, int createSerial, int lastRefuseReason, int refuseSerial, int sequence, int historySerial) {
            Rooms = rooms;
            InRoom = inRoom;
            Room = room;
            Members = members;
            History = history;
            LastCreateResult = lastCreateResult;
            CreateSerial = createSerial;
            LastRefuseReason = lastRefuseReason;
            RefuseSerial = refuseSerial;
            Sequence = sequence;
            HistorySerial = historySerial;
        }

        /// <summary>The rooms (boards) in sight, the own one included.</summary>
        public RoChatRoomInfo[] Rooms { get; }
        public bool InRoom { get; }
        public RoChatRoomInfo Room { get; }
        public RoChatRoomMember[] Members { get; }
        /// <summary>The room's lines (ZC_NOTIFY_CHAT / own ZC_NOTIFY_PLAYERCHAT while inside, members coming and going), oldest first.</summary>
        public RoChatRoomLine[] History { get; }
        /// <summary>ZC_ACK_CREATE_CHATROOM: 0 done, 1 limit, 2 duplicate; −1 none yet.</summary>
        public int LastCreateResult { get; }
        /// <summary>Counts the create answers (each one prints its message once).</summary>
        public int CreateSerial { get; }
        /// <summary>ZC_REFUSE_ENTER_ROOM: 0 full, 1 password, 2 kicked, 4 zeny, 5/6 level, 7 job; −1 none yet.</summary>
        public int LastRefuseReason { get; }
        /// <summary>Counts the refusals.</summary>
        public int RefuseSerial { get; }
        public int Sequence { get; }
        /// <summary>Counts every line added to <see cref="History"/>.</summary>
        public int HistorySerial { get; }
    }

    /// <summary>
    /// The chat rooms (Hercules clif_createchat / dispchat / changechatstatus / clearchat / joinchatfail / joinchatok /
    /// addchat / chatRoleChange / leavechat): the boards in sight and the room the player is in, with its members and
    /// lines. Not thread-safe: the session applies it under its lock.
    /// </summary>
    public sealed class RoChatRoomRuntime {
        private const int MaxHistory = 200;
        private readonly Dictionary<uint, RoChatRoomInfo> rooms = new Dictionary<uint, RoChatRoomInfo>();
        private readonly List<RoChatRoomMember> members = new List<RoChatRoomMember>();
        private readonly List<RoChatRoomLine> history = new List<RoChatRoomLine>();
        private bool inRoom;
        private uint roomId;
        private RoChatRoomInfo pendingCreate;
        private bool hasPendingCreate;
        private int lastCreateResult = -1;
        private int lastRefuseReason = -1;
        private int createSerial;
        private int refuseSerial;
        private int sequence;
        private int historySerial;

        public bool InRoom => inRoom;

        public RoChatRoomSnapshot Snapshot() {
            var roomValues = new RoChatRoomInfo[rooms.Count];
            rooms.Values.CopyTo(roomValues, 0);
            rooms.TryGetValue(roomId, out RoChatRoomInfo room);
            return new RoChatRoomSnapshot(roomValues, inRoom, room, members.ToArray(), history.ToArray(), lastCreateResult, createSerial, lastRefuseReason, refuseSerial,
                sequence, historySerial);
        }

        /// <summary>The form of CZ_CREATE_CHATROOM: kept until the answer (the client opens its room from it).</summary>
        public void RememberCreate(int limit, bool isPublic, string title) {
            pendingCreate = new RoChatRoomInfo(0, 0, limit, 1, (byte) (isPublic ? 1 : 0), title);
            hasPendingCreate = true;
        }

        /// <summary>ZC_ACK_CREATE_CHATROOM: on success the player is in the new room as its owner.</summary>
        public void ApplyCreateResult(byte result, uint localAid, string localName) {
            lastCreateResult = result;
            createSerial++;
            if (result == 0 && hasPendingCreate) {
                // the room's id comes with its ZC_ROOM_NEWENTRY (which may come first): key by the owner until then
                uint id = FindRoomOf(localAid);
                RoChatRoomInfo info = id != 0 ? rooms[id] : new RoChatRoomInfo(localAid, 0, pendingCreate.Limit, 1, pendingCreate.Type, pendingCreate.Title);
                if (id == 0) rooms[0] = info;
                EnterRoom(id);
                members.Clear();
                members.Add(new RoChatRoomMember(localName, true));
            }

            hasPendingCreate = false;
            sequence++;
        }

        private uint FindRoomOf(uint ownerAid) {
            foreach (RoChatRoomInfo info in rooms.Values) {
                if (info.OwnerAid == ownerAid && info.ChatId != 0) return info.ChatId;
            }

            return 0;
        }

        /// <summary>ZC_ROOM_NEWENTRY / ZC_CHANGE_CHATROOM: a board appears or changes (the own room gets its id here).</summary>
        public void ApplyRoom(RoChatRoomInfo info) {
            if (rooms.TryGetValue(0, out RoChatRoomInfo provisional) && provisional.OwnerAid == info.OwnerAid) {
                rooms.Remove(0);
                if (inRoom && roomId == 0) roomId = info.ChatId;
            }

            rooms[info.ChatId] = info;
            sequence++;
        }

        /// <summary>ZC_DESTROY_ROOM: the board goes; inside, the room closes.</summary>
        public void ApplyDestroy(uint chatId) {
            rooms.Remove(chatId);
            if (inRoom && roomId == chatId) LeaveRoom();
            sequence++;
        }

        public void ApplyRefuse(byte reason) {
            lastRefuseReason = reason;
            refuseSerial++;
            sequence++;
        }

        /// <summary>ZC_ENTER_ROOM: inside, with the member list (role 0 = owner).</summary>
        public void ApplyEnter(uint chatId, RoChatRoomMember[] list) {
            EnterRoom(chatId);
            members.Clear();
            members.AddRange(list ?? Array.Empty<RoChatRoomMember>());
            // the board was announced before the player came in: the count is the list's
            if (rooms.TryGetValue(chatId, out RoChatRoomInfo info)) rooms[chatId] = info.WithUsers(members.Count);
            sequence++;
        }

        /// <summary>ZC_MEMBER_NEWENTRY: someone came in.</summary>
        public void ApplyMemberJoin(int users, string name) {
            if (!inRoom) return;
            members.Add(new RoChatRoomMember(name, false));
            if (rooms.TryGetValue(roomId, out RoChatRoomInfo info)) rooms[roomId] = info.WithUsers(users);
            AddLine(new RoChatRoomLine(RoChatRoomLineKind.Entered, name));
            sequence++;
        }

        /// <summary>ZC_MEMBER_EXIT: someone left (or was kicked); the player leaving closes the room (no message).</summary>
        public void ApplyMemberExit(int users, string name, bool kicked, string localName) {
            if (!inRoom) return;
            if (string.Equals(name, localName, StringComparison.Ordinal)) {
                LeaveRoom();
            } else {
                members.RemoveAll(m => string.Equals(m.Name, name, StringComparison.Ordinal));
                if (rooms.TryGetValue(roomId, out RoChatRoomInfo info)) rooms[roomId] = info.WithUsers(users);
                AddLine(new RoChatRoomLine(kicked ? RoChatRoomLineKind.Kicked : RoChatRoomLineKind.Left, name));
            }

            sequence++;
        }

        /// <summary>ZC_ROLE_CHANGE: role 0 = the new owner.</summary>
        public void ApplyRole(uint role, string name) {
            for (int i = 0; i < members.Count; i++) {
                if (string.Equals(members[i].Name, name, StringComparison.Ordinal)) members[i] = new RoChatRoomMember(name, role == 0);
                else if (role == 0) members[i] = new RoChatRoomMember(members[i].Name, false);
            }

            sequence++;
        }

        /// <summary>A chat line while inside (the server sends the room's messages as ZC_NOTIFY_CHAT / ZC_NOTIFY_PLAYERCHAT).</summary>
        public void ApplyLine(string text) {
            if (!inRoom || string.IsNullOrEmpty(text)) return;
            AddLine(new RoChatRoomLine(RoChatRoomLineKind.Chat, text));
            sequence++;
        }

        private void AddLine(RoChatRoomLine line) {
            history.Add(line);
            if (history.Count > MaxHistory) history.RemoveAt(0);
            historySerial++;
        }

        public bool ClearMapScopedState() {
            if (rooms.Count == 0 && !inRoom) return false;
            rooms.Clear();
            LeaveRoom();
            sequence++;
            return true;
        }

        private void EnterRoom(uint chatId) {
            if (!inRoom || roomId != chatId) history.Clear();
            inRoom = true;
            roomId = chatId;
        }

        private void LeaveRoom() {
            // the own room the server never named (its owner changed before any ZC_CHANGE_CHATROOM): outside, the
            // area's ZC_ROOM_NEWENTRY shows it again under its real id
            rooms.Remove(0);
            inRoom = false;
            roomId = 0;
            members.Clear();
            history.Clear();
        }
    }
}
