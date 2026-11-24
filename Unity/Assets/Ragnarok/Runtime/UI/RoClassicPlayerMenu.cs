using System;
using System.Collections.Generic;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The right-click menu on another player (CGameMode 0xbfc0a0 → UIMenuWnd 0x12 → mode Msg 0x18; player-menu-rest-spec
    /// §1), in the original's order: "Check %s's Equipment Info" and "Request a deal with (%s)" (not while I am hidden or
    /// cloaked), "Ask (%s) to join your party" (party leader, target not in it), "Send an adoption request to %s"
    /// (RoClassicAdoption.CanOffer), "Send (%s) a Guild Invitation" (a guild with the invite right, target guildless), the
    /// guild master's alliance / antagonist pair after a separator (a target not in my guild, guildless ones included),
    /// separator, "Open 1:1 Chat", "Register as a Friend" (not a friend yet), "Send a mail", separator, "(Un)Block Chat
    /// (Toggle)", separator, the red "Report a User" (0x2715), separator, and the target's "[C-Code] : …" line (copied to
    /// the clipboard). The GM rows need the AID in clientinfo.xml's &lt;aid&gt;&lt;admin&gt; list, which Custom's file
    /// does not have, so they never show there.
    /// </summary>
    public static class RoClassicPlayerMenu {
        private static readonly List<Action> Actions = new List<Action>();

        /// <summary>The menu for <paramref name="actor"/> at the mouse + (1, 1) (UIMenuWnd flips it at the screen edge).</summary>
        public static void Show(RoLoginShellSession session, RoClassicChat chat, RoOnlineActorSnapshot actor) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (session == null || system == null || actor.ServerId == 0) return;
            string name = string.IsNullOrWhiteSpace(actor.Name) ? "??????" : actor.Name.Trim();
            uint aid = actor.ServerId;
            Actions.Clear();
            RoClassicMenu.Begin();
            void Add(int msg, string fallback, Action action, bool withName = true) {
                string text = RoClassicUiHost.MsgStr(msg, fallback);
                if (withName) text = Format(text, name);
                RoClassicMenu.Add(text);
                Actions.Add(action);
            }

            // my option word (actor + 0x2c0): 0x02 Hide (0xd00920), 0x04 Cloak (0xd00a90)
            int option = SelfOption();
            if ((option & 2) == 0 && (option & 4) == 0) {
                Add(0x550, "Check %s's Equipment Info", () => _ = session.BeginLegacyCheckPlayerEquipment(aid));
                Add(0x187, "Request a deal with (%s)", () => {
                    RoClassicTrade.NoteOutgoingRequest(aid, name);
                    _ = session.BeginLegacyRequestTrade(aid, name);
                });
            }

            if (IsPartyLeader(session) && !InMyParty(session, name)) {
                Add(0x188, "Ask (%s) to join your party", () => _ = session.BeginLegacyPartyInvite(name));
            }

            if (RoClassicAdoption.CanOffer(session, actor, name)) {
                Add(0x377, "Send an adoption request to %s", () => RoClassicAdoption.Request(session, aid));
            }

            RoClassicGuildState guild = session.GuildSnapshot().Guild;
            if (guild != null && guild.GuildId != 0 && (guild.Rights & 1) != 0 && actor.GuildId == 0) {
                Add(0x17e, "Send (%s) a Guild Invitation", () => _ = session.BeginLegacyGuildInvite(aid));
            }

            // the master's pair (0x14efb44), for any target outside my guild: CZ_REQ_ALLY_GUILD 0x0170 {target, my AID, my GID},
            // CZ_REQ_HOSTILE_GUILD 0x0180 {target}
            if (guild != null && guild.GuildId != 0 && guild.BelongMaster && actor.GuildId != guild.GuildId) {
                RoClassicMenu.AddSeparator();
                Add(0x18f, "Send an Guild Alliance Request", () => {
                    var body = new byte[12];
                    BitConverter.GetBytes(aid).CopyTo(body, 0);
                    (uint charId, uint accountId) = session.LocalCharacterIds();
                    BitConverter.GetBytes(accountId).CopyTo(body, 4);
                    BitConverter.GetBytes(charId).CopyTo(body, 8);
                    _ = session.BeginClassicPacket(0x0170, body, "an alliance request");
                }, false);
                Add(0x193, "Set this guild as an Antagonist", () => _ = session.BeginClassicPacket(0x0180, BitConverter.GetBytes(aid), "an antagonist request"), false);
            }

            RoClassicMenu.AddSeparator();
            Add(0x168, "Open 1:1 Chat", () => {
                RoClassicWhisper.Bind(session, chat);
                RoClassicWhisper.OpenManual(name, aid);
            }, false);
            RoClassicWhisper.Bind(session, chat);
            if (!RoClassicWhisper.IsFriend(name)) Add(0x166, "Register as a Friend", () => _ = session.BeginLegacyAddFriend(name), false);
            Add(0xaf8, "Send a mail", () => _ = session.BeginLegacyOpenWriteRodex(name), false);

            // the client-local ignore set (/ex, /in)
            RoClassicMenu.AddSeparator();
            string realName = actor.Name?.Trim() ?? string.Empty;
            if (RoClassicChatBlock.IsBlocked(realName)) Add(0xeeb, "Unblock Chat (Toggle)", () => RoClassicChatBlock.Set(session, realName, false), false);
            else Add(0xeec, "Block Chat (Toggle)", () => RoClassicChatBlock.Set(session, realName, true), false);

            RoClassicMenu.AddSeparator();
            RoClassicMenu.Add(RoClassicUiHost.MsgStr(0xfe3, "Report a User"), new Color32(255, 0, 0, 255));
            Actions.Add(() => RoClassicReport.Open(session, aid, name));
            RoClassicMenu.AddSeparator();

            // "[C-Code] : " + the code of the actor (0xce6100), copied with a chat line in #FFFF64
            string code = RoClassicWhisper.CCode(aid);
            if (code.Length > 0) {
                RoClassicMenu.Add("[C-Code] : " + code);
                Actions.Add(() => {
                    GUIUtility.systemCopyBuffer = code;
                    chat?.AddLine("C-Code copied to clipboard", "#FFFF64");
                });
            }

            Vector2Int mouse = system.Windows.Mouse;
            RoClassicMenu.Open(mouse.x + 1, mouse.y + 1, pick => {
                if (pick >= 0 && pick < Actions.Count) Actions[pick]();
            });
        }

        private static int SelfOption() {
            RoOnlineWorldController world = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            return world != null && world.OwnActorId != 0 && world.State.TryGetActor(world.OwnActorId, out RoOnlineActorSnapshot self) ? self.EffectState : 0;
        }

        private static string Format(string pattern, string name) {
            int at = pattern.IndexOf("%s", StringComparison.Ordinal);
            return at < 0 ? pattern : pattern.Substring(0, at) + name + pattern.Substring(at + 2);
        }

        private static bool IsPartyLeader(RoLoginShellSession session) {
            RoPartyRuntimeSnapshot party = session.PartySnapshot();
            return party.IsInParty && party.LeaderAccountId != 0 && party.LeaderAccountId == session.LocalActorId;
        }

        private static bool InMyParty(RoLoginShellSession session, string name) {
            RoPartyRuntimeSnapshot party = session.PartySnapshot();
            foreach (RoPartyMemberRuntimeSnapshot member in party.Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>()) {
                if (member.Name == name) return true;
            }

            return false;
        }
    }
}
