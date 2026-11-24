using System.Collections.Generic;

namespace Ragnarok.UI {
    /// <summary>
    /// The status-change chat lines of the original (FUN_00c767a0, called by ZC_MSG_STATE_CHANGE2 / 3, 0x043f / 0x0983 /
    /// 0x0984 / 0x08ff): for the local player (type 8 "Abnormal status") or an online party member (type 10 "Party
    /// member's abnormal status", the text after MsgStr 0x4fb " %s's " with the member's name), a status turned on writes its
    /// MsgStr in COLORREF 0x00FFFF00 and turned off in 0x000000FF unless the table says otherwise, after mgr msg 3. The
    /// tables are the two EFST switches of 0xc767a0 (on: byte table 0xc7895c / jump table 0xc787c4; off: 0xc78f5c /
    /// 0xc78e98) read case by case (docs/audits/chat-types-2026-10-04.md); every EFST not listed writes nothing. EFST 64
    /// stands for EFST 0x381 + val1 (0xc76926). ZC_MSG_STATE_CHANGE 0x0196 (Hercules sends the status ends with it) goes
    /// through FUN_00c90ca0's own tables instead: the 4th-job "... has been canceled." lines exist only there.
    /// </summary>
    public static class RoStatusChatLines {
        private readonly struct Row {
            public Row(int msg, bool red) {
                Msg = msg;
                Red = red;
            }

            public readonly int Msg;
            public readonly bool Red;
        }

        private static readonly Dictionary<int, Row> On = Build(
            (2, 0x1cf, false), (6, 0x1d5, false), (7, 0x26c, false), (8, 0x26e, false), (9, 0xd6b, false),
            (10, 0xd6a, false), (12, 0xd1, false), (13, 0xd2, false), (14, 0x276, false), (15, 0xd87, false),
            (16, 0x1d3, false), (17, 0x1d6, false), (18, 0x1d8, false), (19, 0x1da, false), (20, 0x278, false),
            (21, 0x27a, false), (22, 0x27c, false), (23, 0x27e, false), (24, 0x1dc, false), (25, 0x1d1, false),
            (26, 0x1e0, false), (30, 0xd69, false), (31, 0x288, false), (32, 0x28a, false), (33, 0x28c, false),
            (37, 0x1cf, false), (38, 0x1cf, false), (39, 0x1cf, false), (40, 0x1cf, false), (41, 0xd1, false),
            (42, 0xd1, false), (64, 0x1d6, false), (68, 0x1cf, false), (147, 0x27e, false), (161, 0x1cf, false),
            (188, 0x1d1, false), (241, 0x4cf, false), (242, 0x4d1, false), (243, 0x4d3, false), (244, 0x4d7, false),
            (245, 0x4d5, false), (246, 0x4d9, false), (247, 0x4db, false), (248, 0x4dd, false), (249, 0x4df, false),
            (251, 0x4e2, false), (253, 0x4e4, false), (271, 0x4cf, false), (272, 0x4d1, false), (273, 0x4d3, false),
            (274, 0x4d7, false), (275, 0x4d5, false), (276, 0x4d9, false), (1013, 0xcaf, true), (1152, 0xeb0, false),
            (1160, 0xe8e, false), (1161, 0xe92, false), (1162, 0xe90, false), (1192, 0xeb2, false), (1194, 0xeb4, false),
            (1198, 0xe9e, false), (1199, 0xe9c, false), (1200, 0xe9a, false), (1201, 0xe98, false), (1202, 0xea6, false),
            (1203, 0xea4, false), (1204, 0xea8, false), (1217, 0xeaa, false), (1220, 0xeac, false), (1221, 0xeae, false),
            (1226, 0xeb6, false), (1227, 0xe96, false), (1228, 0xe94, false), (1236, 0xeca, false), (1237, 0xecc, false),
            (1245, 0xee7, false), (1247, 0xec6, false), (1248, 0xec2, false), (1249, 0xec8, false), (1250, 0xedf, true),
            (1252, 0xedd, false), (1256, 0xed1, false), (1257, 0xecf, false), (1259, 0xed3, true), (1260, 0xed5, true),
            (1261, 0xed7, false), (1262, 0xed9, false), (1263, 0xedb, false), (1271, 0xee4, false));

        private static readonly Dictionary<int, Row> Off = Build(
            (2, 0x1d0, true), (6, 0x1d7, true), (7, 0x26d, true), (8, 0x26f, true), (9, 0x271, true), (10, 0x273, true),
            (12, 0xd2, true), (13, 0xd1, true), (14, 0x277, true), (15, 0xd88, true), (16, 0x1d4, true),
            (17, 0x1d7, true), (18, 0x1d9, true), (19, 0x1db, true), (20, 0x279, true), (21, 0x27b, true),
            (22, 0x27d, true), (23, 0x27f, true), (24, 0x1dd, true), (25, 0x1d2, true), (26, 0x1e1, true),
            (30, 0x287, true), (31, 0x289, true), (37, 0x1d0, true), (38, 0x1d0, true), (39, 0x1d0, true),
            (40, 0xd2, true), (41, 0xd2, true), (42, 0xd2, true), (64, 0x1d7, true), (68, 0x1d0, true),
            (147, 0x27f, true), (161, 0x1d0, true), (188, 0x1d2, true), (241, 0x4d0, true), (242, 0x4d2, true),
            (243, 0x4d4, true), (244, 0x4d8, true), (245, 0x4d6, true), (246, 0x4da, true), (247, 0x4dc, true),
            (248, 0x4de, true), (249, 0x4e0, true), (271, 0x4d0, true), (272, 0x4d2, true), (273, 0x4d4, true),
            (274, 0x4d8, true), (275, 0x4d6, true), (276, 0x4da, true), (1013, 0xcaf, false));

        // FUN_00c90ca0 (ZC_MSG_STATE_CHANGE 0x0196): its own two switches (on: byte table 0xc921c4 / jump table 0xc92108,
        // EFST <= 0x461; off: 0xc927a8 / 0xc92628, EFST <= 0x53b), no EFST 64 remap and no formatted line; 253 on and the
        // conditional off cases (12, 40, 42, 64, 1152, 1172, 1192, 1237, 1252) only gate effects, never the line
        private static readonly Dictionary<int, Row> LegacyOn = Build(
            (2, 0x1cf, false), (6, 0x1d5, false), (7, 0x26c, false), (8, 0x26e, false), (9, 0xd6b, false),
            (10, 0xd6a, false), (12, 0xd1, false), (13, 0xd2, false), (14, 0x276, false), (15, 0xd87, false),
            (16, 0x1d3, false), (17, 0x1d6, false), (18, 0x1d8, false), (19, 0x1da, false), (20, 0x278, false),
            (21, 0x27a, false), (22, 0x27c, false), (23, 0x27e, false), (24, 0x1dc, false), (25, 0x1d1, false),
            (26, 0x1e0, false), (30, 0xd69, false), (31, 0x288, false), (32, 0x28a, false), (33, 0x28c, false),
            (37, 0x1cf, false), (38, 0x1cf, false), (39, 0x1cf, false), (40, 0x1cf, false), (41, 0xd1, false),
            (42, 0xd1, false), (64, 0x1d6, false), (68, 0x1cf, false), (147, 0x27e, false), (161, 0x1cf, false),
            (188, 0x1d1, false), (241, 0x4cf, false), (242, 0x4d1, false), (243, 0x4d3, false), (244, 0x4d7, false),
            (245, 0x4d5, false), (246, 0x4d9, false), (247, 0x4db, false), (248, 0x4dd, false), (249, 0x4df, false),
            (251, 0x4e2, false), (252, 0x4e3, false), (253, 0x4e4, false), (271, 0x4cf, false),
            (272, 0x4d1, false), (273, 0x4d3, false), (274, 0x4d7, false), (275, 0x4d5, false), (276, 0x4d9, false),
            (312, 0x567, false), (1013, 0xcaf, true));

        private static readonly Dictionary<int, Row> LegacyOff = Build(
            (2, 0x1d0, true), (6, 0x1d7, true), (7, 0x26d, true), (8, 0x26f, true), (9, 0x271, true), (10, 0x273, true),
            (12, 0xd2, true), (13, 0xd1, true), (14, 0x277, true), (15, 0xd88, true), (16, 0x1d4, true),
            (17, 0x1d7, true), (18, 0x1d9, true), (19, 0x1db, true), (20, 0x279, true), (21, 0x27b, true),
            (22, 0x27d, true), (23, 0x27f, true), (24, 0x1dd, true), (25, 0x1d2, true), (26, 0x1e1, true),
            (30, 0x287, true), (31, 0x289, true), (37, 0x1d0, true), (38, 0x1d0, true), (39, 0x1d0, true),
            (40, 0xd2, true), (41, 0xd2, true), (42, 0xd2, true), (64, 0x1d7, true),
            (68, 0x1d0, true), (147, 0x27f, true), (161, 0x1d0, true), (188, 0x1d2, true), (241, 0x4d0, true),
            (242, 0x4d2, true), (243, 0x4d4, true), (244, 0x4d8, true), (245, 0x4d6, true), (246, 0x4da, true),
            (247, 0x4dc, true), (248, 0x4de, true), (249, 0x4e0, true), (271, 0x4d0, true), (272, 0x4d2, true),
            (273, 0x4d4, true), (274, 0x4d8, true), (275, 0x4d6, true), (276, 0x4da, true), (1013, 0xcb0, false),
            (1152, 0xeb1, true), (1160, 0xe8f, true), (1161, 0xe93, true), (1162, 0xe91, true),
            (1172, 0xea1, true), (1174, 0xea2, true), (1178, 0xea3, true), (1192, 0xeb3, true),
            (1194, 0xeb5, true), (1198, 0xe9f, true), (1199, 0xe9d, true), (1200, 0xe9b, true), (1201, 0xe99, true),
            (1202, 0xea7, true), (1203, 0xea5, true), (1204, 0xea9, true), (1217, 0xeab, true), (1220, 0xead, true),
            (1221, 0xeaf, true), (1226, 0xeb7, true), (1227, 0xe97, true), (1228, 0xe95, true), (1236, 0xecb, true),
            (1237, 0xecd, true), (1244, 0xee6, true), (1245, 0xee8, true), (1247, 0xec7, true),
            (1248, 0xec3, true), (1249, 0xec9, true), (1252, 0xede, true), (1256, 0xed2, true),
            (1257, 0xed0, true), (1259, 0xed4, false), (1260, 0xed6, false), (1261, 0xed8, true), (1262, 0xeda, true),
            (1263, 0xedc, true), (1271, 0xee5, true));

        /// <summary>ZC_MSG_STATE_CHANGE: read with FUN_00c90ca0's tables.</summary>
        public const ushort LegacyPacket = 0x0196;

        // the on cases formatted with the remaining minutes (0xc7748c / 0xc77644 / 0xc77520) and with a double (0xc776c5)
        private const int EfstExpBonus = 250, EfstJobExpBonus = 312, EfstDropBonus = 252, EfstJobHuntingDouble = 636;

        private static Dictionary<int, Row> Build(params (int Efst, int Msg, bool Red)[] rows) {
            var table = new Dictionary<int, Row>(rows.Length);
            foreach ((int efst, int msg, bool red) in rows) table[efst] = new Row(msg, red);
            return table;
        }

        /// <summary>The packet's state as the client reads it: 0x08ff and 0x0984 always "on" (0xc8ddbb / 0xc859ac), the others their flag.</summary>
        public static bool IsOn(ushort packetId, bool active) => packetId == 0x08ff || packetId == 0x0984 || active;

        /// <summary>0xc76926: EFST 64 is read as EFST 0x381 + val1 before the switches (FUN_00c767a0 only).</summary>
        public static int SwitchEfst(int efst, int value1) => efst == 0x40 ? value1 + 0x381 : efst;

        /// <summary>Whether the status can write a line at all (the shell's chat gate; a dictionary test, no allocation).</summary>
        public static bool HasLine(ushort packetId, int efst, bool on, int value1) {
            if (packetId == LegacyPacket) return (on ? LegacyOn : LegacyOff).ContainsKey(efst);
            efst = SwitchEfst(efst, value1);
            return on
                ? On.ContainsKey(efst) || efst == EfstExpBonus || efst == EfstJobExpBonus || efst == EfstDropBonus || efst == EfstJobHuntingDouble
                : Off.ContainsKey(efst);
        }

        /// <summary>
        /// The status line's text (without the party member's prefix) and colour, or null. 0x0196 reads FUN_00c90ca0's
        /// tables, every other packet FUN_00c767a0's. The formatted ones of the latter: 250 "For %d minutes your Exp will
        /// increase by %d%%." (0x7ef), 312 the job exp twin (0x99e), 252 the drop rate one (0x9a7, or 0x9a8 with hours and
        /// minutes past an hour), each with the remaining minutes (ms / 60000, none when 0) and val1; 636 "... doubling of %d
        /// is %.2f minutes." (0x784) with val2 and val1 / 100 + 1.
        /// </summary>
        public static string Text(ushort packetId, int efst, bool on, uint remainingMs, int value1, int value2, RoChatLineTypes.MsgSource msg, out string color) {
            color = on ? RoChatLineTypes.Cyan : RoChatLineTypes.Red;
            bool legacy = packetId == LegacyPacket;
            if (!legacy) efst = SwitchEfst(efst, value1);
            Dictionary<int, Row> table = legacy ? (on ? LegacyOn : LegacyOff) : (on ? On : Off);
            if (table.TryGetValue(efst, out Row row)) {
                color = row.Red ? RoChatLineTypes.Red : RoChatLineTypes.Cyan;
                return Msg(msg, row.Msg, null);
            }

            if (!on || legacy) return null;
            if (efst == EfstJobHuntingDouble) {
                return RoChatLineTypes.PrintfDouble(Msg(msg, 0x784, "Monster Job hunting experience that you can get through the doubling of %d is %.2f minutes."),
                    value2, value1 / 100.0 + 1.0);
            }

            int minutes = (int) (remainingMs / 60000u);
            if (minutes <= 0) return null;
            switch (efst) {
                case EfstExpBonus:
                    return RoChatLineTypes.Printf(Msg(msg, 0x7ef, "For %d minutes your Exp will increase by %d%%."), null, null, minutes, value1);
                case EfstJobExpBonus:
                    return RoChatLineTypes.Printf(Msg(msg, 0x99e, "For %d minutes, Job experience obtained from monster is increased by %d%%."), null, null, minutes, value1);
                case EfstDropBonus: {
                    int hours = minutes / 60;
                    if (hours <= 0) return RoChatLineTypes.Printf(Msg(msg, 0x9a7, "For %d minutes, item drop rate from defeating monster is increased by %d%%."), null, null, minutes, value1);
                    return RoChatLineTypes.Printf3(Msg(msg, 0x9a8, "For %d minutes %.2d, item drop rate from defeating monster is increased by %d%%."), hours, minutes - hours * 60, value1);
                }
                default:
                    return null;
            }
        }

        /// <summary>The party member's prefix (MsgStr 0x4fb " %s's ").</summary>
        public static string MemberPrefix(string name, RoChatLineTypes.MsgSource msg) => RoChatLineTypes.Printf(Msg(msg, 0x4fb, " %s's "), name ?? string.Empty);

        private static string Msg(RoChatLineTypes.MsgSource msg, int id, string fallback) {
            string text = msg != null ? msg(id, fallback) : fallback;
            return text ?? string.Empty;
        }
    }
}
