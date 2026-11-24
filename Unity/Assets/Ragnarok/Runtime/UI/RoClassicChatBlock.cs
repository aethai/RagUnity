using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Chat blocking (player-menu-rest-spec §5): the client-local ignore set (object [0x11a62fc] + 0x18, a std::set of
    /// names) that the player menu's "Block / Unblock Chat (Toggle)" reads; /ex name and /in name (slash cases 0x12 / 0x13
    /// of 0xc07a00) change it at once, before any answer, and send CZ_SETTING_WHISPER_PC 0x00cf {name[24], u8 type}. The
    /// answers print the name + their MsgStr in yellow: ZC_SETTING_WHISPER_PC 0x00d1 (0xc61920, a successful /ex prints
    /// nothing and a failure does not undo the set), 0x0ae1 (0xc616f0), ZC_SETTING_WHISPER_STATE 0x00d2 (/exall, /inall)
    /// and ZC_WHISPER_LIST 0x00d4 (/ex alone).
    /// </summary>
    public static class RoClassicChatBlock {
        private const string Yellow = "#FFFF00", Cyan = "#00FFFF";
        private static readonly HashSet<string> Blocked = new HashSet<string>(StringComparer.Ordinal);
        private static string lastName = string.Empty;   // mode + 0x118

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            Blocked.Clear();
            lastName = string.Empty;
        }

        public static bool IsBlocked(string name) => name != null && Blocked.Contains(name);

        public static bool Handles(ushort id) => id == 0x00d1 || id == 0x00d2 || id == 0x00d4 || id == 0x0ae1;

        /// <summary>/ex (block = true) or /in for one name: nothing past 24 characters; the set first, then 0x00cf.</summary>
        public static void Set(RoLoginShellSession session, string name, bool block) {
            lastName = name ?? string.Empty;
            if (lastName.Length > 24) return;
            if (block) Blocked.Add(lastName);
            else Blocked.Remove(lastName);
            var body = new byte[25];
            byte[] bytes = RoLoginShellSession.TextBytes(lastName);
            Array.Copy(bytes, body, Math.Min(24, bytes.Length));
            body[24] = (byte) (block ? 0 : 1);
            _ = session?.BeginClassicPacket(0x00cf, body, block ? "a chat block" : "a chat unblock");
        }

        /// <summary>"/ex name", "/in name", "/ex" (the server's list, CZ_REQ_WHISPER_LIST 0x00d3), "/exall" / "/inall" (CZ_SETTING_WHISPER_STATE 0x00d0).</summary>
        public static bool TryCommand(string line, RoLoginShellSession session) {
            if (string.IsNullOrEmpty(line) || line[0] != '/') return false;
            string trimmed = line.TrimEnd();
            int space = trimmed.IndexOf(' ');
            string cmd = (space < 0 ? trimmed : trimmed.Substring(0, space)).ToLowerInvariant();
            string arg = space < 0 ? string.Empty : trimmed.Substring(space + 1).Trim();
            switch (cmd) {
                case "/ex":
                    if (arg.Length == 0) _ = session?.BeginClassicPacket(0x00d3, Array.Empty<byte>(), "a block list request");
                    else Set(session, arg, true);
                    return true;
                case "/in":
                    if (arg.Length > 0) Set(session, arg, false);
                    return true;
                case "/exall":
                    _ = session?.BeginClassicPacket(0x00d0, new byte[] { 0 }, "a block-all");
                    return true;
                case "/inall":
                    _ = session?.BeginClassicPacket(0x00d0, new byte[] { 1 }, "an allow-all");
                    return true;
            }

            return false;
        }

        public static void Handle(byte[] b) {
            if (b == null || b.Length < 2) return;
            switch (BitConverter.ToUInt16(b, 0)) {
                case 0x00d1: {
                    // {u8 type, u8 result}: /ex 1 / 2 → 0xc2 / 0xc3 (0 prints nothing), /in 0..2 → 0xc4..0xc6
                    if (b.Length < 4) return;
                    int type = b[2], result = b[3];
                    string text = null;
                    if (type == 0) {
                        if (result == 1) text = Msg(0xc2, ": Whispering Block has failed.");
                        else if (result == 2) text = Msg(0xc3, ": Whispering Block has failed. Block List is full.");
                    } else if (type == 1) {
                        if (result == 0) text = Msg(0xc4, ": Whispering accepted.");
                        else if (result == 1) text = Msg(0xc5, ": Command has failed.");
                        else if (result == 2) text = Msg(0xc6, ": Command has failed. Block List is full.");
                    }

                    if (text != null) Chat(lastName + text, Yellow);
                    break;
                }
                case 0x0ae1: {
                    // {name[24], u8 type, u8 result}: " is blocked." / " Failed to block." / "(Maximum number exceeded)", the unblock trio
                    if (b.Length < 28) return;
                    int type = b[26], result = b[27];
                    if (result > 2 || type > 1) return;
                    int[] ids = type == 0 ? new[] { 0xd3d, 0xd3e, 0xd3f } : new[] { 0xd40, 0xd41, 0xd42 };
                    string[] fallbacks = type == 0
                        ? new[] { " is blocked.", " Failed to block.", " Failed to block.(Maximum number exceeded)" }
                        : new[] { " is unblocked.", " Failed to unblock.", " Failed to unblock.(Maximum number exceeded)" };
                    Chat(Text(b, 2, 24) + Msg(ids[result], fallbacks[result]), Yellow);
                    break;
                }
                case 0x00d2: {
                    // {u8 type, u8 result}: /exall 0xd63 / 0xd64, /inall 0xd65 / 0xd66
                    if (b.Length < 4 || b[2] > 1 || b[3] > 1) return;
                    int id = 0xd63 + b[2] * 2 + b[3];
                    string[] fallbacks = {
                        "Blocked all chats and whispers from all characters", "Failed to block all chats and whispers from all characters",
                        "Allowed all chats and whispers from all characters", "Failed to allow all chats and whispers from all characters"
                    };
                    Chat(Msg(id, fallbacks[id - 0xd63]), Yellow);
                    break;
                }
                case 0x00d4: {
                    // {u16 len, name[24]…}: "There is no block list." or "-Block list-" and the names, cyan
                    int count = b.Length >= 4 ? Math.Max(0, (BitConverter.ToUInt16(b, 2) - 4) / 24) : 0;
                    if (count == 0) {
                        Chat(Msg(0xd43, "There is no block list."), Cyan);
                        break;
                    }

                    Chat(Msg(0xd44, "-Block list-"), Cyan);
                    for (int i = 0; i < count && 4 + 24 * (i + 1) <= b.Length; i++) Chat(Text(b, 4 + 24 * i, 24), Cyan);
                    break;
                }
            }
        }

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        private static string Text(byte[] b, int at, int length) {
            int end = at;
            while (end < at + length && end < b.Length && b[end] != 0) end++;
            Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
            return Encoding.GetEncoding(1252).GetString(b, at, end - at);
        }

        private static void Chat(string text, string colour) => RoClassicChat.Instance?.AddLine(text, colour);

        /// <summary>!block state | answer type result (a local 0x00d1) for the sweep.</summary>
        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            if (t.Length >= 3 && t[0] == "answer") Handle(new byte[] { 0xd1, 0x00, byte.Parse(t[1]), byte.Parse(t[2]) });
            return "blocked=[" + string.Join(",", Blocked) + "] last='" + lastName + "'";
        }
    }
}
