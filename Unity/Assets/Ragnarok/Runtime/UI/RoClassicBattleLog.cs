using System;

namespace Ragnarok.UI {
    /// <summary>
    /// The battle log of the original (FUN_00bd58c0, the queued-hit pump of every actor's OnProcess): when a queued hit with
    /// damage &gt; 0 comes due on its target, a line in type 16 "Battle message" (yellow) or 17 "Party member's battle
    /// message" (pink), MsgStr 0x645..0x648 (<see cref="RoChatLineTypes.ResolveBattleLine"/>). No option gates it and the
    /// client throttles nothing: one line per landed hit that involves the player or an online party member; the tabs
    /// decide whether it shows (the first-run "Battle Log" tab has every flag off, as chatwndinfo.lub sets it).
    /// </summary>
    public static class RoClassicBattleLog {
        private const int FirstMsg = 0x644, LastMsg = 0x648;
        private static readonly string[] templates = new string[LastMsg - FirstMsg + 1];
        private static object templatesFor;
        private static readonly RoChatLineTypes.MsgSource Msg = CachedMsg;

        /// <summary>
        /// The hit that came due (the world controller's half of FUN_00bd58c0): the target's player-job test and who is the
        /// local player; the party test (online members, FUN_00d1e3b0) is asked only when the line depends on it.
        /// </summary>
        public static void Write(bool targetIsPlayerJob, bool targetIsSelf, string targetName, uint targetId,
            bool attackerIsSelf, string attackerName, uint attackerId, int damage) {
            if (damage <= 0) return;
            RoClassicChat chat = RoClassicChat.Instance;
            if (chat == null) return;
            RoLoginShellSession session = chat.Model.Session;
            bool targetInParty = targetIsPlayerJob && !targetIsSelf && session != null && session.IsOnlinePartyMember(targetId);
            bool attackerInParty = !targetIsPlayerJob && !attackerIsSelf && session != null && session.IsOnlinePartyMember(attackerId);
            RoChatLineTypes.BattleLine line = RoChatLineTypes.ResolveBattleLine(damage, targetIsPlayerJob, targetIsSelf, targetInParty, attackerIsSelf, attackerInParty);
            if (line == RoChatLineTypes.BattleLine.None) return;
            string text = RoChatLineTypes.BattleText(line, RoChatLineTypes.BattleName(attackerName, Msg), RoChatLineTypes.BattleName(targetName, Msg), damage, Msg);
            chat.AddTypedLine(text, RoChatLineTypes.BattleLineColor(line), RoChatLineTypes.BattleLineType(line));
        }

        /// <summary>The five battle-log texts, looked up once per message table (the lookup formats the id into a key).</summary>
        private static string CachedMsg(int id, string fallback) {
            if (id < FirstMsg || id > LastMsg) return RoClassicUiHost.MsgStr(id, fallback);
            object owner = RoClassicUiHost.GameAssets();
            if (!ReferenceEquals(owner, templatesFor)) {
                templatesFor = owner;
                Array.Clear(templates, 0, templates.Length);
            }

            return templates[id - FirstMsg] ??= RoClassicUiHost.MsgStr(id, fallback);
        }
    }
}
