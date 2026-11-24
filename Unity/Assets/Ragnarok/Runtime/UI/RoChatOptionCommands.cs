using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Ragnarok.UI {
    /// <summary>
    /// The non-UI part of the slash commands that were silent until now (docs/audits/chat-options-2026-10-04.md): the head
    /// and body turns (/doridori /bingbing /bangbang, CGameMode::SendMsg 0xc07a00 cases 0x6c–0x6e), /hi (0x95), the away
    /// message (/automsg 0x97), /savechat (0x33) and their packets, lines and file names. RoClassicChat runs them.
    /// </summary>
    public static class RoChatOptionCommands {
        /// <summary>CZ_CHANGE_DIRECTION2 0x0361 (5) {u16 head direction, u8 body direction}: mode msg 0x8b (0xc1cf9c).</summary>
        public const ushort CzChangeDirection2 = 0x0361;

        /// <summary>CZ_DORIDORI 0x01e7 (2): mode msg 0xa9 (0xc1aade).</summary>
        public const ushort CzDoriDori = 0x01e7;

        /// <summary>EFST_SUHIDE (933): while the own status list (0x012c1fb8) holds it, /doridori does nothing.</summary>
        public const int StatusSuHide = 933;

        /// <summary>The colour of the /hi line (COLORREF 0x1effff).</summary>
        public const string HiColor = "#FFFF1E";

        /// <summary>The colour of "… is Saved." (COLORREF 0xff00).</summary>
        public const string SavedColor = "#00FF00";

        /// <summary>[0x01555ead]: at most this many /hi whispers wait for their quiet ZC_ACK_WHISPER.</summary>
        public const int MaxPendingHiAcks = 40;

        // ---- /doridori /bingbing /bangbang ----

        /// <summary>
        /// /doridori (0xc085e5): the head goes to 2 unless it is already there, then to 1 (CPc+0x3c: 0 straight, 1 / 2 the
        /// two sides).
        /// </summary>
        public static ushort DoriDoriHead(ushort head) => (ushort) (head != 2 ? 2 : 1);

        /// <summary>
        /// /bingbing (0xc08eda) and /bangbang (0xc08f6b): the body's screen direction d = CGameActor::vf04(rotation)
        /// becomes the rotation (8 − d)·45 ± 45 − camera, and 0xbf3600 ((angle + 180) / 45 mod 8) turns that back into the
        /// packet's direction: one step on, +1 for /bingbing, −1 for /bangbang (the camera cancels out).
        /// </summary>
        public static byte TurnBody(byte direction, bool bingBing) => (byte) ((direction + (bingBing ? 1 : 7)) & 7);

        /// <summary>The packet direction (0 south, 1 south-west, …) of the world state's internal one (0 north, 1 north-west, …).</summary>
        public static byte PacketDirection(int internalDirection) => (byte) (((4 - internalDirection) % 8 + 8) % 8);

        /// <summary>CZ_CHANGE_DIRECTION2's body: the head direction (u16) and the body direction (u8).</summary>
        public static byte[] ChangeDirectionBody(ushort head, byte direction) =>
            new[] { (byte) head, (byte) (head >> 8), direction };

        /// <summary>
        /// The sitting /doridori clock (CGameMode+0x5bc, a list kept at 6 times by OnInit 0xc012bb): each /doridori while
        /// sitting (CPc+0x70 == 6) drops the oldest time and adds timeGetTime(); when the newest is 1501..2999 ms after the
        /// oldest, CZ_DORIDORI goes out and every time is cleared to 0.
        /// </summary>
        public sealed class DoriDoriClock {
            private readonly long[] times = new long[6];
            private int oldest;

            /// <summary>Records a sitting /doridori at <paramref name="nowMs"/>; true when CZ_DORIDORI is due.</summary>
            public bool Record(long nowMs) {
                times[oldest] = nowMs;
                oldest = (oldest + 1) % times.Length;
                long span = nowMs - times[oldest];
                if (span - 1501 > 1498 || span < 1501) return false;   // the unsigned test (span − 0x5dd) ≤ 0x5da
                Array.Clear(times, 0, times.Length);
                return true;
            }
        }

        // ---- /hi ----

        /// <summary>The whisper each friend gets (0xcf0430): "(Hi) " and the text, or "*^_^*" without one.</summary>
        public static string HiWhisper(string argument) => "(Hi) " + (string.IsNullOrEmpty(argument) ? "*^_^*" : argument);

        /// <summary>The line it prints (0xc081da / 0xc082d4): "[ To Friends ] : (Hi) …" in COLORREF 0x1effff.</summary>
        public static string HiLine(string argument) => "[ To Friends ] : " + HiWhisper(argument);

        // ---- /automsg ----

        /// <summary>
        /// The away reply (0xc3ccb9): with /automsg on, a whisper that does not start with MsgStr 0x3ad "[Automated
        /// Message]" gets that text followed by the away message back.
        /// </summary>
        public static bool TryBuildAwayReply(bool enabled, string awayMessage, string received, string prefix, out string reply) {
            reply = null;
            if (!enabled || string.IsNullOrEmpty(prefix)) return false;
            if ((received ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)) return false;
            reply = prefix + (awayMessage ?? string.Empty);
            return true;
        }

        // ---- /autosavechat (GM only) ----

        /// <summary>
        /// The client's GM list (0xa01160 → [0x014ef1e4..e8], tested by 0xa010f0): the &lt;admin&gt; numbers inside
        /// &lt;aid&gt; of the (first) &lt;connection&gt; of clientinfo.xml. Custom's file has none.
        /// </summary>
        public static List<uint> ParseClientInfoAdmins(string xml) {
            var aids = new List<uint>();
            if (string.IsNullOrEmpty(xml)) return aids;
            Match connection = Regex.Match(xml, @"<connection>(.*?)</connection>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!connection.Success) return aids;
            Match aid = Regex.Match(connection.Groups[1].Value, @"<aid>(.*?)</aid>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!aid.Success) return aids;
            foreach (Match admin in Regex.Matches(aid.Groups[1].Value, @"<admin>\s*(\d+)\s*</admin>", RegexOptions.IgnoreCase)) {
                if (uint.TryParse(admin.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint id)) aids.Add(id);
            }

            return aids;
        }

        /// <summary>
        /// The /autosavechat log (case 0x66 → 0x153a8a0): "&lt;dir&gt;\Chat\Chat.txt", and while that exists
        /// "Chat001.txt", "Chat002.txt" … (the first free name).
        /// </summary>
        public static string AutoSaveChatPath(string chatDirectory, Func<string, bool> exists) {
            string path = Path.Combine(chatDirectory, "Chat.txt");
            for (int n = 1; exists(path); n++) path = Path.Combine(chatDirectory, "Chat" + n.ToString("000", CultureInfo.InvariantCulture) + ".txt");
            return path;
        }

        /// <summary>
        /// The chat lines 0xc10e60 appends while the log runs: public chat (ZC_NOTIFY_CHAT / PLAYERCHAT 0x8d / 0x8e,
        /// battlefield 0x2dc), whispers (received 0xc3cb00, sent 0x9df), party (0x109), guild (0x17f / 0xbde) and NPC /
        /// channel chat (0x2c1 / 0xb20).
        /// </summary>
        public static bool IsAutoSavedChannel(Ragnarok.Network.RoChatChannel channel) =>
            channel == Ragnarok.Network.RoChatChannel.Main || channel == Ragnarok.Network.RoChatChannel.Whisper ||
            channel == Ragnarok.Network.RoChatChannel.Party || channel == Ragnarok.Network.RoChatChannel.Guild;

        // ---- /savechat ----

        /// <summary>
        /// The file of one tab (0x8a24c0): "&lt;dir&gt;\Chat\Chat_&lt;tab&gt;.txt", and while that exists
        /// "Chat_&lt;tab&gt;_001.txt", "_002" … (the first free name).
        /// </summary>
        public static string SaveChatPath(string chatDirectory, string tabName, Func<string, bool> exists) {
            string path = Path.Combine(chatDirectory, "Chat_" + tabName + ".txt");
            for (int n = 1; exists(path); n++) {
                path = Path.Combine(chatDirectory, "Chat_" + tabName + "_" + n.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".txt");
            }

            return path;
        }
    }
}
