using Ragnarok.Network;

namespace Ragnarok.UI {
    /// <summary>
    /// The parts of the slash-command options the shell's drain carries (docs/audits/chat-options-2026-10-04.md): the
    /// friend and guild member log in / out lines and the guild notice of /loginout, the away reply of /automsg and the
    /// quiet answers of /hi.
    /// </summary>
    public sealed partial class RoMinimalLoginShell {
        // the COLORREF 0xffff00 of both log in / out lines, 0x80ffff of the guild notice
        private const string LoginOutColor = "#00FFFF";
        private const string GuildNoticeColor = "#FFFF80";

        /// <summary>A friend's ZC_FRIENDS_STATE, a guild member's ZC_UPDATE_CHARSTAT2 or ZC_GUILD_NOTICE: lines while /loginout is on.</summary>
        private static bool IsLoginOutEvent(RoMapEntryDrainEvent drainEvent) =>
            drainEvent.Kind == RoMapEntryDrainEventKind.FriendState ||
            drainEvent.Kind == RoMapEntryDrainEventKind.GuildNotice ||
            (drainEvent.Kind == RoMapEntryDrainEventKind.GuildManager && drainEvent.GuildUpdate != null &&
             drainEvent.GuildUpdate.Kind == RoGuildUpdateKind.MemberLogin);

        /// <summary>
        /// /loginout ([0x01552d50]): ZC_FRIENDS_STATE 0x0206 (0xd06570) prints "%s has logged in." (MsgStr 0x411, state 0)
        /// or "%s has logged out." (0x412) for a friend of the list; ZC_UPDATE_CHARSTAT2 0x01f2 (0xc3213f → mode msg 0x8c,
        /// 0xc1cfe6) prints "Guild Member %s has connected." (0x1e5, state 1) or "… has disconnected." (0x1e6, state 0) for
        /// a member with a name, chat type 4. Both in COLORREF 0xffff00. False when nothing was added.
        /// </summary>
        private bool TryAppendLoginOutLine(RoMapEntryDrainEvent drainEvent, RoClassicChat chat) {
            if (chat == null || session == null || !IsLoginOutEvent(drainEvent) || !RoClassicCommandFlags.LoginOut) return false;
            if (drainEvent.Kind == RoMapEntryDrainEventKind.GuildNotice) {
                // ZC_GUILD_NOTICE 0x016f (0xc6f100): "[ subject ]" and "[ notice ]", each when not empty, COLORREF 0x80ffff
                bool subject = TryAppendGuildNoticeLine(drainEvent.GuildNotice.Subject, chat);
                bool notice = TryAppendGuildNoticeLine(drainEvent.GuildNotice.Notice, chat);
                return subject || notice;
            }

            if (drainEvent.Kind == RoMapEntryDrainEventKind.FriendState) {
                RoZcFriendState state = drainEvent.FriendState;
                string name = null;
                foreach (RoFriendRuntimeSnapshot friend in session.FriendsSnapshot()) {
                    if (friend.AccountId == state.AccountId && friend.CharacterId == state.CharacterId) {
                        name = string.IsNullOrEmpty(friend.Name) ? state.Name : friend.Name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(name)) return false;   // 0xd06570: not in the list, nothing
                string format = state.StateRaw == 0
                    ? RoClassicUiHost.MsgStr(0x411, "%s has logged in.")
                    : RoClassicUiHost.MsgStr(0x412, "%s has logged out.");
                chat.AddLine(ReplaceFirst(format, "%s", name), LoginOutColor, RoChatChannel.System);
                return true;
            }

            (uint accountId, uint charId, int value) = drainEvent.GuildUpdate.MemberValues[0];
            if (value != 0 && value != 1) return false;
            string member = null;
            foreach (RoGuildMember m in session.GuildSnapshot().Guild.Members) {
                if (m.CharId == charId && (accountId == 0 || m.AccountId == accountId)) {
                    member = m.Name;
                    break;
                }
            }

            if (string.IsNullOrEmpty(member)) return false;   // 0xc32098: no name, no message
            string line = value == 1
                ? RoClassicUiHost.MsgStr(0x1e5, "Guild Member %s has connected.")
                : RoClassicUiHost.MsgStr(0x1e6, "Guild Member %s has disconnected.");
            chat.AddLine(ReplaceFirst(line, "%s", member), LoginOutColor, RoChatChannel.Guild);
            return true;
        }

        private static bool TryAppendGuildNoticeLine(string part, RoClassicChat chat) {
            string text = (part ?? string.Empty).Trim();
            if (text.Length == 0) return false;
            chat.AddLine("[ " + text + " ]", GuildNoticeColor, RoChatChannel.System);
            return true;
        }

        /// <summary>/automsg (0xc3ccb9): the away reply to a whisper just received, whispered back to its sender.</summary>
        private void TrySendAwayReply(string sender, string message) {
            if (session == null || string.IsNullOrEmpty(sender)) return;
            string reply = RoClassicAutoMessage.ReplyTo(message);
            if (reply != null) _ = session.BeginSendPrivateMessage(sender, reply);
        }

        private static string ReplaceFirst(string text, string token, string value) {
            int at = text.IndexOf(token, System.StringComparison.Ordinal);
            return at < 0 ? text + " " + value : text.Substring(0, at) + value + text.Substring(at + token.Length);
        }
    }
}
