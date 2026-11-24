using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ragnarok.UI {
    /// <summary>
    /// The client's chat types (the index into a tab's 24 flags: UIBattleMsgOptionWnd's rows, ChatWndInfo optionN = type + 1;
    /// 24 = every tab) and the lines whose type the original's producers name, built as the client builds them
    /// (docs/audits/chat-types-2026-10-04.md). Pure: the message-table texts come in through <see cref="MsgSource"/>.
    /// </summary>
    public static class RoChatLineTypes {
        public const int Public = 0;
        public const int PublicChat = 1;
        public const int Whisper = 2;
        public const int Party = 3;
        public const int Guild = 4;
        public const int Alliance = 5;
        public const int ItemGetDrop = 6;
        public const int EquipOnOff = 7;
        public const int AbnormalStatus = 8;
        public const int PartyItem = 9;
        public const int PartyStatus = 10;
        public const int SkillFailure = 11;
        public const int PartyConfig = 12;
        public const int EquipDamaged = 13;
        public const int Woe = 14;
        public const int PartySearch = 15;
        public const int Battle = 16;
        public const int PartyBattle = 17;
        public const int Exp = 18;
        public const int Quest = 19;
        public const int Battlefield = 20;
        public const int Clan = 21;
        public const int Call = 22;
        public const int Attributes = 23;
        public const int EveryTab = 24;

        // the producers' COLORREFs (0x00BBGGRR) as #RRGGBB
        public const string Yellow = "#FFFF00";      // 0x0000FFFF
        public const string Cyan = "#00FFFF";        // 0x00FFFF00
        public const string Red = "#FF0000";         // 0x000000FF
        public const string Pink = "#FFC8C8";        // 0x00C8C8FF (party battle lines)
        public const string EquipOn = "#00FFC8";     // 0x00C8FF00 (" is put on.")
        public const string QuestExp = "#BE58BE";    // 0x00BE58BE (exp type 1)
        public const string SkillMsg = "#CDCDFF";    // 0x00FFCDCD (ZC_MSG_SKILL)
        public const string Notice = "#64FFFF";      // 0x00FFFF64

        /// <summary>MsgStr(id): the message table's line id, <c>fallback</c> without one.</summary>
        public delegate string MsgSource(int id, string fallback);

        // ---- the battle log (FUN_00bd58c0) ----

        /// <summary>Which line a queued hit writes when it comes due (MsgStr 0x645..0x648), or none.</summary>
        public enum BattleLine {
            None = 0,
            /// <summary>0x645 " [%s] inflicts '%d' damage points on you." (attacker) — type 16, yellow.</summary>
            OwnHit = 0x645,
            /// <summary>0x646 " [%s] attacked by [%s] with '%d' damage points." (member, attacker) — type 17, pink.</summary>
            PartyMemberHit = 0x646,
            /// <summary>0x647 " [%s] receives '%d' damage points." (target) — type 16, yellow.</summary>
            OwnAttack = 0x647,
            /// <summary>0x648 " [%s] attacks [%s] with '%d' damage points." (member, target) — type 17, pink.</summary>
            PartyMemberAttack = 0x648
        }

        /// <summary>
        /// FUN_00bd58c0 at a queued hit's due time (damage &gt; 0 only), on the target actor that owns the queue: a player-job
        /// target (FUN_00d02e40 of its job) writes 0x645 when it is the local player, else 0x646 when it is an online party
        /// member (FUN_00d1e3b0(id, 1)); any other target writes 0x647 when the local player hit it, else 0x648 when an
        /// online party member did. Nothing else (another player's hit on a stranger, PvP on strangers, misses, heals).
        /// </summary>
        public static BattleLine ResolveBattleLine(int damage, bool targetIsPlayerJob, bool targetIsSelf, bool targetIsOnlinePartyMember,
            bool attackerIsSelf, bool attackerIsOnlinePartyMember) {
            if (damage <= 0) return BattleLine.None;
            if (targetIsPlayerJob) {
                if (targetIsSelf) return BattleLine.OwnHit;
                return targetIsOnlinePartyMember ? BattleLine.PartyMemberHit : BattleLine.None;
            }

            if (attackerIsSelf) return BattleLine.OwnAttack;
            return attackerIsOnlinePartyMember ? BattleLine.PartyMemberAttack : BattleLine.None;
        }

        /// <summary>The chat type of a battle line: 16 for the own lines, 17 for a party member's.</summary>
        public static int BattleLineType(BattleLine line) =>
            line == BattleLine.PartyMemberHit || line == BattleLine.PartyMemberAttack ? PartyBattle : Battle;

        /// <summary>The colour of a battle line: yellow (0x0000FFFF) for the own lines, pink (0x00C8C8FF) for a party member's.</summary>
        public static string BattleLineColor(BattleLine line) => BattleLineType(line) == PartyBattle ? Pink : Yellow;

        /// <summary>
        /// The name a battle line shows: the client's name of the id cut at its first '#' (an empty one is MsgStr 0x644
        /// "Someone").
        /// </summary>
        public static string BattleName(string name, MsgSource msg) {
            if (!string.IsNullOrEmpty(name)) {
                int hash = name.IndexOf('#');
                if (hash != 0) return hash > 0 ? name.Substring(0, hash) : name;
            }

            return Msg(msg, 0x644, "Someone");
        }

        /// <summary>The battle line's text: the attacker's and the target's names (already through <see cref="BattleName"/>) and the hit's damage.</summary>
        public static string BattleText(BattleLine line, string attackerName, string targetName, int damage, MsgSource msg) {
            switch (line) {
                case BattleLine.OwnHit:
                    return Printf(Msg(msg, 0x645, " [%s] inflicts '%d' damage points on you."), attackerName, damage);
                case BattleLine.PartyMemberHit:
                    return Printf(Msg(msg, 0x646, " [%s] attacked by [%s] with '%d' damage points."), targetName, attackerName, damage);
                case BattleLine.OwnAttack:
                    return Printf(Msg(msg, 0x647, " [%s] receives '%d' damage points."), targetName, damage);
                case BattleLine.PartyMemberAttack:
                    return Printf(Msg(msg, 0x648, " [%s] attacks [%s] with '%d' damage points."), attackerName, targetName, damage);
                default:
                    return null;
            }
        }

        // ---- experience (0xc93400 ZC_NOTIFY_EXP2 0x0acc, 0xc55980 ZC_NOTIFY_EXP 0x07f6) ----

        /// <summary>
        /// The own exp line: nothing for 0; var 1 → "'%lld' Base Experience" (0xc94; 0x64d " %d Base exp points" for the old
        /// 32-bit packet), var 2 → "'%lld' Job Experience" (0xc9b / 0x64e), then " has lost." (0x650) for a negative value
        /// else " acquired." (0x64f) — another var gets only that suffix. Colour: exp type 1 (quest) #BE58BE, else yellow.
        /// Type 18.
        /// </summary>
        public static bool TryExpLine(long exp, int varId, int expType, bool wide, MsgSource msg, out string text, out string color) {
            text = null;
            color = expType == 1 ? QuestExp : Yellow;
            if (exp == 0) return false;
            string head = string.Empty;
            if (varId == 1) {
                head = wide ? Printf(Msg(msg, 0xc94, "'%lld' Base Experience"), exp) : Printf(Msg(msg, 0x64d, " %d Base exp points"), (int) exp);
            } else if (varId == 2) {
                head = wide ? Printf(Msg(msg, 0xc9b, "'%lld' Job Experience"), exp) : Printf(Msg(msg, 0x64e, " %d Job exp points"), (int) exp);
            }

            text = head + (exp < 0 ? Msg(msg, 0x650, " has lost.") : Msg(msg, 0x64f, " acquired."));
            return true;
        }

        // ---- party configuration (type 12) ----

        /// <summary>
        /// 0xc5fec0 (ZC_PARTY_CONFIG 0x07d8): "Party Setup - How to share EXP : Each Take " / "Even Share" (exp 2: "… : Each
        /// Take (Can't be changed because of level gap)"), then "… - How to share Items : Each Take / Party Share", then "… -
        /// Item sharing type : Each Take / Even Share". The client formats into one buffer and adds it whenever it is not
        /// empty: an item value other than 0 / 1 repeats the line before it (an exp value other than 0..2 leaves the buffer
        /// empty, so no first line). Yellow, type 12, after mgr msg 3.
        /// </summary>
        public static void PartyOptionLines(uint expOption, int itemPickup, int itemDivision, MsgSource msg, List<string> into) {
            into.Clear();
            string setup = Msg(msg, 0x123, "Party Setup");
            string buffer = string.Empty;
            if (expOption == 0 || expOption == 1) {
                buffer = Printf("%s - %s : %s", setup, Msg(msg, 0x124, "How to share EXP"), Msg(msg, expOption == 0 ? 0x11f : 0x120, expOption == 0 ? "Each Take " : "Even Share"));
            } else if (expOption == 2) {
                buffer = Printf("%s - %s : %s(%s)", setup, Msg(msg, 0x124, "How to share EXP"), Msg(msg, 0x11f, "Each Take "), Msg(msg, 0x5ff, "Can't be changed because of level gap"));
            }

            if (buffer.Length > 0) into.Add(buffer);
            if (itemPickup == 0 || itemPickup == 1) {
                buffer = Printf("%s - %s : %s", setup, Msg(msg, 0x125, "How to share Items"), Msg(msg, itemPickup == 0 ? 0x11f : 0x122, itemPickup == 0 ? "Each Take " : "Party Share"));
            }

            if (buffer.Length > 0) into.Add(buffer);
            if (itemDivision == 0 || itemDivision == 1) {
                buffer = Printf("%s - %s : %s", setup, Msg(msg, 0x2e2, "Item sharing type"), Msg(msg, itemDivision == 0 ? 0x11f : 0x120, itemDivision == 0 ? "Each Take " : "Even Share"));
            }

            if (buffer.Length > 0) into.Add(buffer);
        }

        /// <summary>
        /// The old ZC_GROUPINFO_CHANGE 0x0101 (inline case 0xc315af): the exp line only — 0 / 1 as above, 2 MsgStr 0x175 "You
        /// can't modify Party Setup.", anything else nothing. Yellow, type 12.
        /// </summary>
        public static string LegacyPartyOptionLine(uint expOption, MsgSource msg) {
            if (expOption == 2) return Msg(msg, 0x175, "You can't modify Party Setup.");
            if (expOption > 1) return null;
            return Printf("%s - %s : %s", Msg(msg, 0x123, "Party Setup"), Msg(msg, 0x124, "How to share EXP"), Msg(msg, expOption == 0 ? 0x11f : 0x120, expOption == 0 ? "Each Take " : "Even Share"));
        }

        /// <summary>0xc47b30 (ZC_CHANGE_GROUP_MASTER 0x07fc): "Party leader is [%s]." (0x610) with the new leader's party-list name; yellow, type 12.</summary>
        public static string PartyLeaderLine(string leaderName, MsgSource msg) =>
            Printf(Msg(msg, 0x610, "Party leader is [%s]."), leaderName ?? string.Empty);

        // ---- equipment (types 7 and 13) ----

        /// <summary>FUN_00d03af0: "%s%s" of the item's display name and " is put on." (0xaa); #00FFC8, type 7.</summary>
        public static string EquipOnLine(string itemName, MsgSource msg) => (itemName ?? string.Empty) + Msg(msg, 0xaa, " is put on.");

        /// <summary>FUN_00d03760 / FUN_00d24f30: the item's display name and " is taken off." (0xab); red, type 7.</summary>
        public static string EquipOffLine(string itemName, MsgSource msg) => (itemName ?? string.Empty) + Msg(msg, 0xab, " is taken off.");

        /// <summary>
        /// The failed equip / unequip answers (0xc41890 ZC_ACK_WEAR_EQUIP_V5 0x0999, 0xc40c90 ZC_ACK_TAKEOFF_EQUIP_V5 0x099a):
        /// result 1 → MsgStr 0x7a1 (both), 2 → 0x174 (equip only); red, type 7. Null for the others (0 is the success line).
        /// </summary>
        public static string EquipFailLine(bool wear, int result, MsgSource msg) {
            if (result == 1) return Msg(msg, 0x7a1, "Can't equip this item because you don't meet the requirements.");
            if (result == 2 && wear) return Msg(msg, 0x174, "You can't put this item on.");
            return null;
        }

        /// <summary>
        /// 0xc49710 (ZC_EQUIPITEM_DAMAGED 0x02bb, location 0 ignored): the own item at that location → "%s's equipment has
        /// been damaged." (0x514) with its display name (none without an item there); a party member's weapon (location
        /// 0x02 or 0x20) / armour (0x10) → "%s's %s was damaged." (0x515) with "Weapon" (0x516) / "Armor" (0x517). Red, type
        /// 13, after mgr msg 3.
        /// </summary>
        public static string EquipDamagedLine(bool own, int location, string ownItemName, string memberName, MsgSource msg) {
            if (location == 0) return null;
            if (own) return string.IsNullOrEmpty(ownItemName) ? null : Printf(Msg(msg, 0x514, "%s's equipment has been damaged."), ownItemName);
            if (memberName == null) return null;
            if (location == 0x20 || location == 2) return Printf(Msg(msg, 0x515, "%s's %s was damaged."), memberName, Msg(msg, 0x516, "Weapon"));
            if (location == 0x10) return Printf(Msg(msg, 0x515, "%s's %s was damaged."), memberName, Msg(msg, 0x517, "Armor"));
            return null;
        }

        // ---- quests (type 19, yellow) ----

        /// <summary>0xc48570 (ZC_DEL_QUEST 0x02b4): "[%s] quest was deleted." (0x64b) with the quest's title.</summary>
        public static string QuestDeletedLine(string title, MsgSource msg) => Printf(Msg(msg, 0x64b, "[%s] quest was deleted."), title ?? string.Empty);

        /// <summary>0xc68c40 / 0xc69100 (ZC_UPDATE_MISSION_HUNT_EX): "[%s] quest - defeating [%s] progress (%d/%d)." (0x64a) per objective.</summary>
        public static string QuestHuntLine(string title, string huntName, int count, int maxCount, MsgSource msg) =>
            Printf(Msg(msg, 0x64a, "[%s] quest - defeating [%s] progress (%d/%d)."), title ?? string.Empty, huntName ?? string.Empty, count, maxCount);

        // ---- weight (ZC_PAR_CHANGE 0x18 / 0x19, 0xc5c6ca / 0xc5c8a7; type 8) ----

        /// <summary>
        /// The weight lines of ZC_PAR_CHANGE (values / 10, percent = 100·weight / max(maxWeight, 1)). Weight up: crossing the
        /// natural-recovery limit (ZC_OVERWEIGHT_PERCENT, 0x015556ac) → MsgStr 0xcef with the limit, red; crossing 90 % →
        /// 0x133, red. Weight down below them → 0x134 / 0x135 in cyan. Max weight up (not the first value): the percent with
        /// the new max falling below 50 (the literal, not the limit) → 0x134, below 90 → 0x135. Each after mgr msg 3, type 8.
        /// </summary>
        public static void WeightLines(int oldPercent, int newPercent, int limit, bool fromMaxWeight, MsgSource msg, List<(string Text, string Color)> into) {
            into.Clear();
            if (fromMaxWeight) {
                if (newPercent < 50 && oldPercent >= 50) into.Add((Msg(msg, 0x134, "Your HP/SP are now being restored naturally."), Cyan));
                if (newPercent < 90 && oldPercent >= 90) into.Add((Msg(msg, 0x135, "Attack and Skills are now available."), Cyan));
                return;
            }

            if (newPercent > oldPercent) {
                if (newPercent >= limit && oldPercent < limit) {
                    into.Add((Printf(Msg(msg, 0xcef, "Your weight limit is over %d, HP and SP will not recover naturally."), limit), Red));
                }

                if (newPercent >= 90 && oldPercent < 90) into.Add((Msg(msg, 0x133, "Attacks and skills cannot be used when your Weight is over 90%."), Red));
            } else if (newPercent < oldPercent) {
                if (newPercent < limit && oldPercent >= limit) into.Add((Msg(msg, 0x134, "Your HP/SP are now being restored naturally."), Cyan));
                if (newPercent < 90 && oldPercent >= 90) into.Add((Msg(msg, 0x135, "Attack and Skills are now available."), Cyan));
            }
        }

        // ---- the ZC_BROADCAST (0x009a) forms, broadcast-spec §1 ----

        /// <summary>
        /// The chat type of a 0x009a form (0xc44af1 / 0xc452ea / 0xc44f08): "ssss" (Hercules BC_WOE) 14, "micc" (megaphone)
        /// 0, every other form ("tool", "blue", "name#…", plain) 24 = every tab. ZC_BROADCAST2 0x01c3 is always 24.
        /// </summary>
        public static int BroadcastType(string form) {
            switch (form) {
                case "ssss": return Woe;
                case "micc": return Public;
                default: return EveryTab;
            }
        }

        // ---- ZC_ITEM_PICKUP_ACK (0xc52490 / 0xc505a0 / ...; FUN_00c50c10) ----

        /// <summary>A pickup result the handlers take as obtained (0, and 3 through the same "You got %s (%d)." path).</summary>
        public static bool PickupObtained(int result) => result == 0 || result == 3;

        /// <summary>
        /// FUN_00c50c10: a failed pickup's line — result 1 / 6 MsgStr 0x35 "You cannot get the item.", 2 0x34 (overweight), 4
        /// 0xdc (the weight limit), 5 0x117 (30,000 of one kind), 7 0x58a (the maximum of the same item), 9 0xf90 (the party's
        /// even share); any other none. Red, type 0 (so every tab), after mgr msg 3.
        /// </summary>
        public static string PickupFailureLine(int result, MsgSource msg) {
            switch (result) {
                case 1:
                case 6: return Msg(msg, 0x35, "You cannot get the item.");
                case 2: return Msg(msg, 0x34, "You cannot carry more items because you are overweight.");
                case 4: return Msg(msg, 0xdc, "You can't have this item because you will exceed the weight limit.");
                case 5: return Msg(msg, 0x117, "You cannot carry more than 30,000 of one kind of item.");
                case 7: return Msg(msg, 0x58a, "You have exceeded the maximum amount of the same item.");
                case 9: return Msg(msg, 0xf90, "It is evenly distributed.\\nItems cannot be obtained because the weight or type of party members is excessive.");
                default: return null;
            }
        }

        // ---- the other type-8 lines ----

        /// <summary>
        /// 0xc60d50 (ZC_GOSPEL_INFO 0x0215, {u32 type}): 0x15..0x19 → MsgStr 0x379..0x37d, 0x1c..0x20 → 0x37e..0x382, 0x28 →
        /// 0x393, any other none; COLORREF 0x009B9BFF, type 8, no mgr msg 3.
        /// </summary>
        public static string GospelLine(int type, MsgSource msg) {
            int id;
            switch (type) {
                case 0x15: id = 0x379; break;
                case 0x16: id = 0x37a; break;
                case 0x17: id = 0x37b; break;
                case 0x18: id = 0x37c; break;
                case 0x19: id = 0x37d; break;
                case 0x1c: id = 0x37e; break;
                case 0x1d: id = 0x37f; break;
                case 0x1e: id = 0x380; break;
                case 0x1f: id = 0x381; break;
                case 0x20: id = 0x382; break;
                case 0x28: id = 0x393; break;
                default: return null;
            }

            return Msg(msg, id, null);
        }

        /// <summary>The colour of the gospel lines (COLORREF 0x009B9BFF).</summary>
        public const string Gospel = "#FF9B9B";

        /// <summary>BM75 (item 12563 = 0x3113): its use writes a line (FUN_00bfef40).</summary>
        public const int ItemBattleManual75 = 0x3113;

        /// <summary>
        /// FUN_00bfef40 (the used item's effects, ZC_USE_ITEM_ACK2 0x01c8 with a result): the local player's BM75 writes MsgStr
        /// 0x704 "... up to 50%% for 30 minutes." when its job (FUN_00bd0cd0) is in 4054..4112, else 0x703 "... up to 75%%
        /// ..." — the text through sprintf, so "%%" prints '%'. Cyan, type 8.
        /// </summary>
        public static string BattleManualLine(int job, MsgSource msg) {
            bool third = unchecked((uint) (job - 0xfd6)) <= 0x3a;
            return third
                ? Printf(Msg(msg, 0x704, "Increases base exp and job exp gained by killing monsters up to 50%% for 30 minutes."), null)
                : Printf(Msg(msg, 0x703, "Increases base exp and job exp gained by killing monsters up to 75%% for 30 minutes."), null);
        }

        // ---- ZC_ACTION_FAILURE (0x013b, 0xc6c6f0; type 0) ----

        /// <summary>
        /// 0 → MsgStr 0xf2 in red, 1 → 0xf3, 2 → 0xf4 in #64FFFF, each after mgr msg 3; 3 (ammunition equipped, no msg 3) by
        /// the own job (FUN_00cea6a0, I): the assassin line (12, 4013, 4035, 4059, 4065, 4101, 4254) 0x410, Gunslinger (24)
        /// 0x497, Ninja (25) and 4222 none, any other 0xf5 in #64FFFF. Null for no line. Type 0.
        /// </summary>
        public static string ActionFailureLine(int type, int job, MsgSource msg, out string color, out bool popOpen) {
            color = Notice;
            popOpen = type >= 0 && type <= 2;
            switch (type) {
                case 0:
                    color = Red;
                    return Msg(msg, 0xf2, "Please equip the proper ammunition first.");
                case 1:
                    return Msg(msg, 0xf3, "You can't attack or use skills because you've exceeded the Weight Limit.");
                case 2:
                    return Msg(msg, 0xf4, "You can't use skills because you've exceeded the Weight Limit.");
                case 3:
                    switch (job) {
                        case 0x19:
                        case 0x107e:
                            return null;
                        case 0xc:
                        case 0xfad:
                        case 0xfc3:
                        case 0xfdb:
                        case 0xfe1:
                        case 0x1005:
                        case 0x109e:
                            return Msg(msg, 0x410, "You have equipped throwing daggers.");
                        case 0x18:
                            return Msg(msg, 0x497, "Bullets have been equipped.");
                        default:
                            return Msg(msg, 0xf5, "Ammunition has been equipped.");
                    }
                default:
                    return null;
            }
        }

        // ---- ZC_MSG_SKILL (0x07e6, inline 0xc30243) ----

        /// <summary>"[%s] %s" of the skill's name and the message (0xf99340); #CDCDFF, type 11.</summary>
        public static string SkillMessageLine(string skillName, string message) => "[" + (skillName ?? string.Empty) + "] " + (message ?? string.Empty);

        // ---- helpers ----

        private static string Msg(MsgSource msg, int id, string fallback) => msg != null ? msg(id, fallback) ?? fallback : fallback;

        [ThreadStatic] private static StringBuilder printfBuilder;

        public static string Printf(string format, string s0) => Format(format, s0, null, null, null, 0, 0, 0, 0d);
        public static string Printf(string format, string s0, string s1) => Format(format, s0, s1, null, null, 0, 0, 0, 0d);
        public static string Printf(string format, string s0, string s1, string s2) => Format(format, s0, s1, s2, null, 0, 0, 0, 0d);
        public static string Printf(string format, string s0, string s1, string s2, string s3) => Format(format, s0, s1, s2, s3, 0, 0, 0, 0d);
        public static string Printf(string format, long n0) => Format(format, null, null, null, null, n0, 0, 0, 0d);
        public static string Printf(string format, string s0, long n0) => Format(format, s0, null, null, null, n0, 0, 0, 0d);
        public static string Printf(string format, string s0, long n0, long n1) => Format(format, s0, null, null, null, n0, n1, 0, 0d);
        public static string Printf(string format, string s0, string s1, long n0) => Format(format, s0, s1, null, null, n0, 0, 0, 0d);
        public static string Printf(string format, string s0, string s1, long n0, long n1) => Format(format, s0, s1, null, null, n0, n1, 0, 0d);

        /// <summary>Three numbers and no string ("For %d minutes %.2d, ... %d%%.").</summary>
        /// <summary>A number and a double ("... of %d is %.2f minutes.").</summary>
        public static string PrintfDouble(string format, long n0, double d0) => Format(format, null, null, null, null, n0, 0, 0, d0);

        public static string Printf3(string format, long n0, long n1, long n2) => Format(format, null, null, null, null, n0, n1, n2, 0d);

        /// <summary>
        /// sprintf over %s (the strings in order) and %d / %u / %ld / %lld (the numbers in order), %% as '%', with the flags
        /// '-' / '0', a width and a precision ("%.2d", "%4d", "%-18s"). No boxing: battle lines are written per landed hit.
        /// </summary>
        private static string Format(string format, string s0, string s1, string s2, string s3, long n0, long n1, long n2, double d0) {
            if (string.IsNullOrEmpty(format)) return string.Empty;
            StringBuilder sb = printfBuilder ??= new StringBuilder(128);
            sb.Clear();
            int si = 0, ni = 0;
            for (int i = 0; i < format.Length; i++) {
                char c = format[i];
                if (c != '%' || i + 1 >= format.Length) {
                    sb.Append(c);
                    continue;
                }

                if (format[i + 1] == '%') {
                    sb.Append('%');
                    i++;
                    continue;
                }

                int j = i + 1;
                bool left = false, zero = false;
                for (; j < format.Length && (format[j] == '-' || format[j] == '0'); j++) {
                    if (format[j] == '-') left = true;
                    else zero = true;
                }

                int width = 0, precision = -1;
                for (; j < format.Length && char.IsDigit(format[j]); j++) width = width * 10 + (format[j] - '0');
                if (j < format.Length && format[j] == '.') {
                    precision = 0;
                    for (j++; j < format.Length && char.IsDigit(format[j]); j++) precision = precision * 10 + (format[j] - '0');
                }

                while (j < format.Length && format[j] == 'l') j++;
                if (j >= format.Length) {
                    sb.Append(c);
                    continue;
                }

                string value;
                char conversion = format[j];
                if (conversion == 's') {
                    value = (si == 0 ? s0 : si == 1 ? s1 : si == 2 ? s2 : si == 3 ? s3 : null) ?? string.Empty;
                    si++;
                    if (precision >= 0 && value.Length > precision) value = value.Substring(0, precision);
                } else if (conversion == 'd' || conversion == 'u' || conversion == 'i') {
                    long n = ni == 0 ? n0 : ni == 1 ? n1 : ni == 2 ? n2 : 0;
                    ni++;
                    value = Math.Abs(n).ToString(CultureInfo.InvariantCulture);
                    if (precision > value.Length) value = value.PadLeft(precision, '0');
                    if (n < 0) value = "-" + value;
                    if (zero && !left && precision < 0 && width > value.Length) value = n < 0 ? "-" + value.Substring(1).PadLeft(width - 1, '0') : value.PadLeft(width, '0');
                } else if (conversion == 'f') {
                    value = d0.ToString("F" + (precision >= 0 ? precision : 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                } else {
                    sb.Append(c);
                    continue;
                }

                if (value.Length < width) value = left ? value.PadRight(width) : value.PadLeft(width);
                sb.Append(value);
                i = j;
            }

            return sb.ToString();
        }
    }
}
