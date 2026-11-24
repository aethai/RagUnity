using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The ranking commands /blacksmith /alchemist /taekwon /pk (talk types 0x90 / 0x91 / 0x96 / 0x9f → mode msg 0x11f with
    /// 0 / 1 / 2 / 3, 0xc15ac8: CZ_REQ_RANKING 0x097c {u16 type}, 4 bytes by the client's table) and their answers in the
    /// chat, all type 0:
    /// <list type="bullet">
    /// <item>ZC_ACK_RANKING 0x0af6 (88: {type, u32 cid[10], u32 points[10], u32 myPoints}, what Hercules sends for this
    /// packet version) → CShowRankingScheduler (0x5e24a0, run at once and then every second for 3 s): while a
    /// character's name is not known it asks for it (the name cache's request queue, CZ_REQNAME_BYGID) and prints
    /// "Fetching rank..." (MsgStr 0xd99, yellow); when every name is known, or after 3 s, the list (0x5e3050).</item>
    /// <item>The old ZC_ACK_RANKING 0x097d (288: names inline) → the list at once (0xc3b9b0).</item>
    /// <item>ZC_UPDATE_RANKING_POINT 0x097e {type, points, total} → MsgStr 0x385 / 0x386 / 0x39e / 0x3b9 (0xc3c7b0).</item>
    /// </list>
    /// The list: the type's title in its frame colour, ten "[ %2d ] %s  :  %4d POINT" rows ("%-18s" for 0x097d; a missing
    /// name is "Nameless", MsgStr 0x245) in its row colour, a rule, "MY POINT    :  %4d POINT ", a rule.
    /// </summary>
    public static class RoClassicRanking {
        public const ushort CzReqRanking = 0x097c;
        public const ushort ZcAckRanking = 0x0af6;
        public const ushort ZcAckRankingOld = 0x097d;
        public const ushort ZcUpdateRankingPoint = 0x097e;

        private const string Rule = "===========================================";
        private const float IntervalSeconds = 1f;
        private const float TimeoutSeconds = 3f;

        /// <summary>The ranking titles and colours (0x5e2820 / 0xc28a00): title, frame colour, row colour; unknown types a rule in white.</summary>
        public static (string Title, string Frame, string Row) Style(int type) {
            switch (type) {
                case 0: return ("========== BLACKSMITH RANK ============", "#9B9B9B", "#CDCDCD");
                case 1: return ("=========== ALCHEMIST RANK =============", "#9BCD9B", "#CDFFCD");
                case 2: return ("============ TAEKWON RANK =============", "#9B9BCD", "#CDCDFF");
                case 3: return ("=========== KILLER RANK ==============", "#9B9B9B", "#CDCDCD");
                default: return (Rule, "#FFFFFF", "#FFFFFF");
            }
        }

        private sealed class RankList {
            public int Type;
            public readonly uint[] Cids = new uint[10];
            public readonly int[] Points = new int[10];
            public int MyPoints;
            public float Start;
            public float NextRun;
            public readonly HashSet<uint> Requested = new HashSet<uint>();
        }

        private static readonly Dictionary<uint, string> names = new Dictionary<uint, string>();
        private static RankList pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            names.Clear();
            pending = null;
        }

        /// <summary>A ranking list waits for names (the shell then hands the name answers in).</summary>
        public static bool Pending => pending != null;

        /// <summary>Talk types 0x90 /blacksmith, 0x91 /alchemist, 0x96 /taekwon, 0x9f /pk → their rank type, else −1.</summary>
        public static int RankTypeOf(int talkType) {
            switch (talkType) {
                case 0x90: return 0;
                case 0x91: return 1;
                case 0x96: return 2;
                case 0x9f: return 3;
                default: return -1;
            }
        }

        /// <summary>CZ_REQ_RANKING 0x097c {u16 type} (4 bytes, the client's table).</summary>
        public static byte[] RequestBody(int rankType) => new[] { (byte) rankType, (byte) (rankType >> 8) };

        /// <summary>Mode msg 0x11f: the request.</summary>
        public static void Request(RoLoginShellSession session, int rankType) {
            if (session == null || rankType < 0) return;
            _ = session.BeginClassicPacket(CzReqRanking, RequestBody(rankType), "a ranking request");
        }

        /// <summary>A character's name answered (ZC_ACK_REQNAME_BYGID): the name cache of the rankings.</summary>
        public static void OnCharacterName(uint characterId, string name) {
            if (characterId == 0 || string.IsNullOrEmpty(name)) return;
            names[characterId] = name;
        }

        /// <summary>The ranking answers of the entry-queue carrier.</summary>
        public static void HandleRaw(byte[] p) {
            if (p == null || p.Length < 4) return;
            RoClassicChat chat = RoClassicChat.Instance;
            switch ((ushort) (p[0] | p[1] << 8)) {
                case ZcAckRanking:
                    if (p.Length < 88) return;
                    var list = new RankList { Type = BitConverter.ToInt16(p, 2), MyPoints = BitConverter.ToInt32(p, 0x54), Start = Time.realtimeSinceStartup };
                    for (int i = 0; i < 10; i++) {
                        list.Cids[i] = BitConverter.ToUInt32(p, 4 + 4 * i);
                        list.Points[i] = BitConverter.ToInt32(p, 0x2c + 4 * i);
                    }

                    pending = list;
                    Run(list, chat, list.Start);
                    break;
                case ZcAckRankingOld:
                    if (chat == null || p.Length < 288) return;
                    var rows = new string[10];
                    var points = new int[10];
                    for (int i = 0; i < 10; i++) {
                        rows[i] = Text(p, 4 + 0x18 * i, 24);
                        points[i] = BitConverter.ToInt32(p, 0xf4 + 4 * i);
                    }

                    Print(chat, BitConverter.ToInt16(p, 2), rows, points, BitConverter.ToInt32(p, 0x11c), true);
                    break;
                case ZcUpdateRankingPoint:
                    if (chat == null || p.Length < 12) return;
                    PointLine(chat, BitConverter.ToUInt16(p, 2), BitConverter.ToInt32(p, 4), BitConverter.ToInt32(p, 8));
                    break;
            }
        }

        /// <summary>Per frame (the chat's tick): the scheduler's next run.</summary>
        public static void Tick() {
            RankList list = pending;
            if (list == null) return;
            float now = Time.realtimeSinceStartup;
            if (now >= list.NextRun) Run(list, RoClassicChat.Instance, now);
        }

        /// <summary>CShowRankingScheduler vf00 (0x5e2bb0): the list after the timeout or once every name is known, else "Fetching rank...".</summary>
        private static void Run(RankList list, RoClassicChat chat, float now) {
            list.NextRun = now + IntervalSeconds;
            if (now - list.Start < TimeoutSeconds) {
                bool all = true;
                RoLoginShellSession session = chat?.Model.Session;
                foreach (uint cid in list.Cids) {
                    if (cid == 0 || names.ContainsKey(cid)) continue;
                    all = false;
                    // FUN_005cd210: an unknown id goes to the name cache's request queue once
                    if (session != null && list.Requested.Add(cid)) {
                        try {
                            _ = session.BeginRequestCharacterName(cid);
                        } catch (Exception) {
                            // no CZ_REQNAME_BYGID in this profile: the names stay unknown ("Nameless" after the timeout)
                        }
                    }
                }

                if (!all) {
                    chat?.AddTypedLine(RoClassicUiHost.MsgStr(0xd99, "Fetching rank..."), RoChatLineTypes.Yellow, RoChatLineTypes.Public);
                    return;
                }
            }

            pending = null;
            if (chat == null) return;
            var rows = new string[10];
            for (int i = 0; i < 10; i++) {
                uint cid = list.Cids[i];
                rows[i] = cid != 0 && names.TryGetValue(cid, out string name) ? name : string.Empty;
            }

            Print(chat, list.Type, rows, list.Points, list.MyPoints, false);
        }

        /// <summary>0x5e3050 / 0xc3b9b0: the title, ten rows, the own points between rules.</summary>
        public static void Print(RoClassicChat chat, int type, IReadOnlyList<string> rowNames, IReadOnlyList<int> points, int myPoints, bool padNames) {
            (string title, string frame, string row) = Style(type);
            chat.AddTypedLine(title, frame, RoChatLineTypes.Public);
            for (int i = 0; i < 10; i++) {
                chat.AddTypedLine(RowLine(i + 1, i < rowNames.Count ? rowNames[i] : null, i < points.Count ? points[i] : 0, padNames), row, RoChatLineTypes.Public);
            }

            chat.AddTypedLine(Rule, frame, RoChatLineTypes.Public);
            chat.AddTypedLine(MyPointLine(myPoints), row, RoChatLineTypes.Public);
            chat.AddTypedLine(Rule, frame, RoChatLineTypes.Public);
        }

        /// <summary>"[ %2d ] %s  :  %4d POINT" (0x0af6; an empty name is MsgStr 0x245 "Nameless"), "[ %2d ] %-18s  :  %4d POINT" (0x097d, the name as sent).</summary>
        public static string RowLine(int rank, string name, int points, bool padName) {
            string shown = padName ? (name ?? string.Empty).PadRight(18) : string.IsNullOrEmpty(name) ? RoClassicUiHost.MsgStr(0x245, "Nameless") : name;
            return "[ " + rank.ToString(CultureInfo.InvariantCulture).PadLeft(2) + " ] " + shown + "  :  " +
                points.ToString(CultureInfo.InvariantCulture).PadLeft(4) + " POINT";
        }

        public static string MyPointLine(int points) => "MY POINT    :  " + points.ToString(CultureInfo.InvariantCulture).PadLeft(4) + " POINT ";

        /// <summary>0xc3c7b0: the rank-point reward line of the type (0x385 / 0x386 / 0x39e / 0x3b9) with the points and the total.</summary>
        private static void PointLine(RoClassicChat chat, int type, int points, int total) {
            int id;
            string fallback, color;
            switch (type) {
                case 0: id = 0x385; fallback = "[Point] You have been rewarded with %d Blacksmith rank points. Your point total is %d."; color = "#CDCDCD"; break;
                case 1: id = 0x386; fallback = "[Point] You have been rewarded with %d Alchemist rank points. Your point total is %d."; color = "#CDFFCD"; break;
                case 2: id = 0x39e; fallback = "[POINT] You have been rewarded with %d Tae-Kwon Mission rank points. Your point total is %d."; color = "#CDCDFF"; break;
                case 3: id = 0x3b9; fallback = "[POINT] You have been rewarded with %d Slayer rank points. Your point total is %d."; color = "#CDCDCD"; break;
                default: return;
            }

            chat.AddTypedLine(RoChatLineTypes.Printf(RoClassicUiHost.MsgStr(id, fallback), null, null, points, total), color, RoChatLineTypes.Public);
        }

        private static string Text(byte[] p, int at, int max) {
            int n = 0;
            while (n < max && at + n < p.Length && p[at + n] != 0) n++;
            if (n == 0) return string.Empty;
            var bytes = new byte[n];
            Array.Copy(p, at, bytes, 0, n);
            return RoTextEncoding.Decode(bytes) ?? string.Empty;
        }
    }
}
