using System;
using System.Collections.Generic;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The chat's slash commands (docs/audits/chat-parity-2026-10-03.md §2): a typed line, a chat room line or a macro that
    /// starts with '/' is parsed by RoChatSlashCommands (0xced690) and run here like CGameMode::SendMsg(0x2a, type)
    /// (0xc07a00) runs it; it never goes to the server as chat. A word the client does not know prints "Invalid Command".
    /// </summary>
    public sealed partial class RoClassicChat {
        private const string HelpMsgStrPath = "data\\luafiles514\\lua files\\datainfo\\helpmsgstr.lub";
        private static List<string> helpLines;

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        /// <summary>A line run as typed (the Command List's cells, mode Msg(0x2a, id)); false without a chat.</summary>
        public static bool RunCommand(string line) {
            RoClassicChat chat = instance;
            if (chat == null || string.IsNullOrEmpty(line) || line[0] != '/') return false;
            chat.RunSlashLine(line);
            return true;
        }

        /// <summary>
        /// A '/' line: the chat block's /ex, /in, /exall, /inall first (they take a name), then the parse and the talk type.
        /// </summary>
        private void RunSlashLine(string line) {
            if (RoClassicChatBlock.TryCommand(line, session)) return;
            int type = RoChatSlashCommands.Parse(line, out string argument);
            RunTalkType(type, argument, line);
        }

        private void RunTalkType(int type, string argument, string line) {
            if (RoChatSlashCommands.TryGetEmotion(type, out int emotion)) {
                // the "ET_..." cases: mode msg 0x149 → 0x56e6a0 → the basic pack's emotion through mode msg 0x1f
                RoEmotionSender.PlayBasic(session, emotion);
                return;
            }

            if (RoChatSlashCommands.TryGetOption(type, out RoChatSlashCommands.Option option)) {
                RunOption(option);
                return;
            }

            switch (type) {
                case RoChatSlashCommands.TypeChat:
                    model.Send(model.ResolveQueuedMessage(line));
                    return;
                case RoChatSlashCommands.TypeInvalid:
                    // case 3: MsgStr 0x5f in yellow
                    AddLine(Msg(0x5f, "Invalid Command"), RoChatSlashCommands.YellowColor, RoChatChannel.System);
                    return;
                case RoChatSlashCommands.TypeSit:
                case RoChatSlashCommands.TypeStand:
                    // cases 0x16 / 0x15: the player's actor msg 0x1f / 0x1e (sit, or stand when sitting; Insert is /sit)
                    RoClassicGameUi.Instance?.Shell?.TryToggleSitRequest();
                    return;
                case RoChatSlashCommands.TypeWho:
                    // case 6: mode msg 0x20 → CZ_REQ_USER_COUNT; ZC_USER_COUNT prints the count
                    if (session != null) _ = session.BeginClassicPacket(RoChatSlashCommands.CzReqUserCount, Array.Empty<byte>(), "a user count request");
                    return;
                case RoChatSlashCommands.TypeMemo:
                    // case 0x2b: mode msg 0x4b → CZ_REMEMBER_WARPPOINT; ZC_ACK_REMEMBER_WARPPOINT prints the result
                    if (session != null) _ = session.BeginClassicPacket(RoChatSlashCommands.CzRememberWarpPoint, Array.Empty<byte>(), "a memo request");
                    return;
                case RoChatSlashCommands.TypeWhere:
                    PrintWhere();
                    return;
                case RoChatSlashCommands.TypeHelp:
                    PrintHelp();
                    return;
                case RoChatSlashCommands.TypeEmotionList:
                    ClassicPopOpen();
                    foreach (int id in RoChatSlashCommands.EmotionListMsgIds()) AddLine(Msg(id, string.Empty), RoChatSlashCommands.ListColor, RoChatChannel.System);
                    return;
                case RoChatSlashCommands.TypeQuickSpell3:
                    RunTalkType(RoChatSlashCommands.TypeQuickSpell, string.Empty, line);
                    RunTalkType(RoChatSlashCommands.TypeQuickSpell2, string.Empty, line);
                    return;
                case RoChatSlashCommands.TypeSet1:
                case RoChatSlashCommands.TypeSet2:
                case RoChatSlashCommands.TypeSet3:
                    RunSet(type, line);
                    return;
                case RoChatSlashCommands.TypeChatRoom:
                    // case 0x17: the chat room form (refused while vending); nothing while in a room
                    if (session != null && !RoClassicChatRoom.RoomOpen) RoClassicChatRoom.ToggleCreate(session);
                    return;
                case RoChatSlashCommands.TypeLeaveRoom:
                    // case 0x18: mode msg 0x30 → CZ_EXIT_ROOM
                    if (session != null && RoClassicChatRoom.RoomOpen) _ = session.BeginLegacyExitChatRoom();
                    return;
                case RoChatSlashCommands.TypeOrganize:
                    // case 0x1a: mode msg 0x3a with the party name
                    if (session != null && argument.Length > 0) _ = session.BeginLegacyCreateParty(argument, 0, 0);
                    return;
                case RoChatSlashCommands.TypeLeaveParty:
                    // case 0x1b: mode msg 0x3d → CZ_REQ_LEAVE_GROUP
                    if (session != null) _ = session.BeginLegacyPartyLeave();
                    return;
                case RoChatSlashCommands.TypeExpel:
                    // case 0x1c: mode msg 0x3e with the member's name
                    ExpelPartyMember(argument);
                    return;
                case RoChatSlashCommands.TypeInvite:
                    // type 0xb6: no name → MsgStr 0x9e4; else mode msg 0x3b → CZ_PARTY_JOIN_REQ by name
                    if (argument.Length == 0) AddLine(Msg(0x9e4, "Character with that name is not possible to invite."), RoChatSlashCommands.RedColor, RoChatChannel.System);
                    else if (session != null) _ = session.BeginLegacyPartyInvite(argument);
                    return;
                case RoChatSlashCommands.TypeGuildInvite:
                    // type 0xd1: no name → MsgStr 0x9e5; else mode msg 0x11e → CZ_REQ_JOIN_GUILD2 by name
                    if (argument.Length == 0) AddLine(Msg(0x9e5, "Character with that name is not possbile to invite to the guild."), RoChatSlashCommands.RedColor, RoChatChannel.System);
                    else if (session != null) _ = session.BeginClassicPacket(RoChatSlashCommands.CzReqJoinGuild2, RoChatSlashCommands.Name24(argument), "a guild invitation");
                    return;
                case RoChatSlashCommands.TypeBreakGuild:
                    // type 0x38: mode msg 0x6a with the key (the guild's name) → CZ_REQ_DISORGANIZE_GUILD
                    if (session != null && argument.Length > 0) _ = session.BeginLegacyGuildDisband(argument);
                    return;
                case RoChatSlashCommands.TypeAccept:
                case RoChatSlashCommands.TypeRefuse:
                    // cases 0xb4 / 0xb5: CZ_PARTY_CONFIG {refuse invitations}
                    if (session != null) {
                        _ = session.BeginClassicPacket(RoChatSlashCommands.CzPartyConfig, new[] { (byte) (type == RoChatSlashCommands.TypeRefuse ? 1 : 0) },
                            "a party invitation setting");
                    }

                    return;
                case RoChatSlashCommands.TypeClanChat:
                    // type 0xd4: mode msg 0x121 → CZ_CLAN_CHAT with the text after the word
                    if (argument.Length > 0) model.Send(new RoChatModel.RoQueuedChatMessage(RoChatChannel.Clan, argument, string.Empty));
                    return;
                case RoChatSlashCommands.TypeNavi:
                case RoChatSlashCommands.TypeNavi2:
                case RoChatSlashCommands.TypeNaviOpen:
                    RoClassicNavigation.TryCommand(line);
                    return;
                case RoChatSlashCommands.TypeTip:
                    RoClassicTipBox.Toggle();
                    return;
                case RoChatSlashCommands.TypeStateInfo:
                    RoClassicStatusIcons.TryCommand("/stateinfo");
                    return;
                case RoChatSlashCommands.TypeBattleChat:
                    RoClassicEntryQueue.TryCommand("/battlechat", this);
                    return;
                case RoChatSlashCommands.TypeEmblem:
                    RoClassicGuildEmblems.TryCommand("/emblem", this);
                    return;
                case 0x90:   // /blacksmith
                case 0x91:   // /alchemist
                case 0x96:   // /taekwon
                case 0x9f:   // /pk
                    // mode msg 0x11f (0xc15ac8) → CZ_REQ_RANKING with the rank type; the answer prints the list
                    RoClassicRanking.Request(session, RoClassicRanking.RankTypeOf(type));
                    return;
            }

            // the head turns, /hi, /automsg, /savechat, /autosavechat, /eqopen, /font (RoClassicChat.Options.cs)
            if (RunOptionCommand(type, argument)) return;

            // the rest (GM tools, ...) are not part of this client yet: silent, as the client is for a type its
            // build does not run
        }

        /// <summary>An option's case: msg 3 (the chat pops open at size 1), the flag flips, its line in RGB(255, 255, 100).</summary>
        private void RunOption(RoChatSlashCommands.Option option) {
            RoClientSettingsModel settings = RoClassicUiHost.ClientSettings;
            string text = RoChatSlashCommands.ToggleOption(option, settings, Msg, out bool on);
            if (option.Type == RoChatSlashCommands.TypeBattleMode && classicChat != null) {
                // /bm off (0xc08bee): a hidden input row comes back (FUN_00875a20); either way Esc's owner follows the mode
                if (!on) chatInputVisible = true;
                ClassicLayoutInputRow();
            }

            RoClassicGameSettings.RefreshValues();
            if (text == null) return;
            ClassicPopOpen();
            string color = option.Type == RoChatSlashCommands.TypeRange ? RoChatSlashCommands.GreenColor : RoChatSlashCommands.OptionColor;
            AddLine(text, color, RoChatChannel.System);
        }

        /// <summary>/set1 /set2 /set3 (0xc088fa..): each listed type again through mode msg 0x2a (each toggles and prints).</summary>
        private void RunSet(int type, string line) {
            int[] types;
            switch (type) {
                case RoChatSlashCommands.TypeSet1:
                    types = new[] { 0x6f, 0x6a, 0x6b, 0x8b };
                    break;
                case RoChatSlashCommands.TypeSet2:
                    types = new[] { 0x6f, 0x6a, 0x6b, 0x76, 0x77, 0x8b };
                    break;
                default:
                    types = new[] { 0x6f, 0x6a, 0x6b, 0x8b, 0xa3, 0x85, 0xa5 };
                    break;
            }

            foreach (int t in types) RunTalkType(t, string.Empty, line);
        }

        /// <summary>Mgr msg 3 before a command's line: a hidden chat shows, a chat at size 0 opens at size 1 (chat msg 0x10).</summary>
        private void ClassicPopOpen() {
            if (classicChat == null) return;
            if (!classicChat.Visible) {
                classicChat.CloseTabsSetShow(true);   // also a chat F11 hid
                classicChat.Visible = true;
            }

            if (classicState == 0) ClassicSetSize(1);
        }

        /// <summary>Case 9: "%s(%s) : %d, %d" (the map's display name, its name, the own cell) in RGB(255, 255, 100).</summary>
        private void PrintWhere() {
            RoOnlineWorldController world = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            if (world == null || world.OwnActorId == 0 || !world.State.TryGetActor(world.OwnActorId, out RoOnlineActorSnapshot own)) return;
            Maps.RoRuntimeMapController map = world.RuntimeMapController != null ? world.RuntimeMapController : UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            string mapName = map != null ? map.MapName ?? string.Empty : string.Empty;
            ClassicPopOpen();
            AddLine(RoChatSlashCommands.WhereLine(RoClassicUiHost.MapDisplayName(mapName), mapName, own.CellX, own.CellY), RoChatSlashCommands.OptionColor, RoChatChannel.System);
        }

        /// <summary>
        /// Case 0xe: every line of the Lua table HelpMsgStr (datainfo\helpmsgstr.lub, through Common's getHelpMsg), then
        /// MsgStr 0x399, all in RGB(100, 255, 255).
        /// </summary>
        private void PrintHelp() {
            ClassicPopOpen();
            foreach (string help in HelpLines()) AddLine(help, RoChatSlashCommands.ListColor, RoChatChannel.System);
            AddLine(Msg(0x399, "Whispers from friends are displayed as [ Friend ], and ones from guildsmen are displayed as [ Member ]."),
                RoChatSlashCommands.ListColor, RoChatChannel.System);
        }

        private static List<string> HelpLines() {
            if (helpLines != null && helpLines.Count > 0) return helpLines;
            var lines = new List<string>(96);
            try {
                byte[] bytes = RoClassicUiHost.GameAssets()?.ReadBytes(HelpMsgStrPath);
                if (bytes != null && bytes.Length > 0 && bytes[0] != 0x1b) {
                    Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
                    lines.AddRange(ParseLuaStringTable(System.Text.Encoding.GetEncoding(1252).GetString(bytes), "HelpMsgStr"));
                }
            } catch (Exception e) {
                Debug.LogWarning("helpmsgstr.lub: " + e.Message);
            }

            helpLines = lines;
            return lines;
        }

        /// <summary>The double-quoted strings of "name = { ... }" in a Lua source, escapes resolved.</summary>
        public static List<string> ParseLuaStringTable(string source, string name) {
            var result = new List<string>();
            int at = source.IndexOf(name, StringComparison.Ordinal);
            if (at < 0) return result;
            int open = source.IndexOf('{', at);
            if (open < 0) return result;
            var sb = new System.Text.StringBuilder(128);
            for (int i = open + 1; i < source.Length; i++) {
                char c = source[i];
                if (c == '}') break;
                if (c == '-' && i + 1 < source.Length && source[i + 1] == '-') {
                    while (i < source.Length && source[i] != '\n') i++;
                    continue;
                }

                if (c != '"') continue;
                sb.Length = 0;
                for (i++; i < source.Length && source[i] != '"'; i++) {
                    char d = source[i];
                    if (d == '\\' && i + 1 < source.Length) {
                        char e = source[++i];
                        sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                    } else {
                        sb.Append(d);
                    }
                }

                result.Add(sb.ToString());
            }

            return result;
        }

        /// <summary>/expel name: the party member with that name (CZ_REQ_EXPEL_GROUP_MEMBER needs the account id).</summary>
        private void ExpelPartyMember(string name) {
            if (session == null || name.Length == 0) return;
            RoPartyRuntimeSnapshot party = session.PartySnapshot();
            foreach (RoPartyMemberRuntimeSnapshot member in party.Members) {
                if (string.Equals(member.Name, name, StringComparison.Ordinal)) {
                    _ = session.BeginLegacyExpelPartyMember(member.AccountId, member.Name);
                    return;
                }
            }
        }

        /// <summary>
        /// The answers of the commands' requests (the raw frames RoClassicEntryQueue forwards): ZC_USER_COUNT 0x00c2 (case
        /// 0xc2da61: MsgStr 0xb2 with the count, yellow) and ZC_ACK_REMEMBER_WARPPOINT 0x011e (0xc308a1).
        /// </summary>
        public static void HandleCommandReply(byte[] b) {
            RoClassicChat chat = instance;
            if (chat == null || b == null || b.Length < 2) return;
            ushort id = (ushort) (b[0] | (b[1] << 8));
            if (id == RoChatSlashCommands.ZcUserCount && b.Length >= 6) {
                int count = b[2] | (b[3] << 8) | (b[4] << 16) | (b[5] << 24);
                chat.ClassicPopOpen();
                chat.AddLine(RoChatSlashCommands.UserCountLine(count, Msg), RoChatSlashCommands.YellowColor, RoChatChannel.System);
            } else if (id == RoChatSlashCommands.ZcAckRememberWarpPoint && b.Length >= 3) {
                string text = RoChatSlashCommands.MemoResultLine(b[2], Msg, out string color);
                if (text != null) chat.AddLine(text, color, RoChatChannel.System);
            }
        }
    }
}
