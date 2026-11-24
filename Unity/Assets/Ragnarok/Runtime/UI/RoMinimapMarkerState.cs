using System.Collections.Generic;
using Ragnarok.Network;

namespace Ragnarok.UI {
    /// <summary>
    /// The minimap marks the map server sends, kept like the original's CGameMode trees (codex-ui-research
    /// RUNTIME-RULES-BIG-MINIMAP-WORLD-MAP.md "Part 3"): guildmates by AID (0x01eb → mode+0x1bc, the own AID ignored, −1 / −1
    /// kept but not drawn), quest NPC marks by actor (0x0446 → mode+0x1f0, state 9999 parks them at −1 / −1, colour 0 is
    /// not drawn) and the boss position (0x0293 → mode+0x5d8 / +0x5dc, reset to −1 by every packet, set for types 1 / 2).
    /// The boss mark itself shows only while the own status icon 253 (SI_CASH_BOSS_ALARM) is on. A new map starts empty.
    /// Thread-safe: the session applies packets, the minimaps copy.
    /// </summary>
    public sealed class RoMinimapMarkerState {
        public readonly struct QuestMark {
            public QuestMark(uint actorId, int x, int y, int state, int color) {
                ActorId = actorId;
                X = x;
                Y = y;
                State = state;
                Color = color;
            }

            public uint ActorId { get; }
            public int X { get; }
            public int Y { get; }
            public int State { get; }
            public int Color { get; }
        }

        private readonly object sync = new object();
        private readonly Dictionary<uint, (int X, int Y)> guild = new Dictionary<uint, (int, int)>();
        private readonly Dictionary<uint, QuestMark> quests = new Dictionary<uint, QuestMark>();
        private readonly Dictionary<uint, (int X, int Y)> battlefield = new Dictionary<uint, (int, int)>();
        private int bossX = -1, bossY = -1;
        private int sequence;

        /// <summary>Changes with every applied packet or clear (the minimaps redraw on a new value).</summary>
        public int Sequence {
            get {
                lock (sync) return sequence;
            }
        }

        public void Apply(RoMapEntryDrainEvent drainEvent, uint localAccountId) {
            lock (sync) {
                switch (drainEvent.MarkerKind) {
                    case 1:
                        RoZcGuildPosition g = drainEvent.GuildPosition;
                        if (g.AccountId == 0 || g.AccountId == localAccountId) return;
                        guild[g.AccountId] = (g.X, g.Y);
                        break;
                    case 2:
                        RoZcQuestNotifyEffect q = drainEvent.QuestNotifyEffect;
                        bool removed = q.State == 9999;
                        quests[q.ActorId] = new QuestMark(q.ActorId, removed ? -1 : q.X, removed ? -1 : q.Y, q.State, q.Color);
                        break;
                    case 3:
                        RoZcBossInfo b = drainEvent.BossInfo;
                        bool alive = b.Type == 1 || b.Type == 2;
                        bossX = alive ? b.X : -1;
                        bossY = alive ? b.Y : -1;
                        break;
                    default:
                        return;
                }

                sequence++;
            }
        }

        /// <summary>
        /// ZC_BATTLEFIELD_NOTIFY_POSITION (inline 0xc2f6af): a teammate other than the own AID goes to the party tree
        /// (mode+0x1b4, SendMsg 0x3f like 0x0107), −1 / −1 kept but not drawn. Kept apart from the party list here, which the
        /// minimap reads from the party window's members.
        /// </summary>
        public void ApplyBattlefieldPosition(RoZcBattlefieldPosition position, uint localAccountId) {
            if (position.AccountId == 0 || position.AccountId == localAccountId) return;
            lock (sync) {
                battlefield[position.AccountId] = (position.X, position.Y);
                sequence++;
            }
        }

        /// <summary>The battleground teammates' positions (AID, x, y) into <paramref name="into"/>.</summary>
        public void CopyBattlefield(List<(uint AccountId, int X, int Y)> into) {
            into.Clear();
            lock (sync) {
                foreach (KeyValuePair<uint, (int X, int Y)> pair in battlefield) into.Add((pair.Key, pair.Value.X, pair.Value.Y));
            }
        }

        /// <summary>A new map (or a return to the login): the trees start empty.</summary>
        public void Clear() {
            lock (sync) {
                guild.Clear();
                quests.Clear();
                battlefield.Clear();
                bossX = bossY = -1;
                sequence++;
            }
        }

        /// <summary>The guildmates' positions (AID, x, y) into <paramref name="into"/>.</summary>
        public void CopyGuild(List<(uint AccountId, int X, int Y)> into) {
            into.Clear();
            lock (sync) {
                foreach (KeyValuePair<uint, (int X, int Y)> pair in guild) into.Add((pair.Key, pair.Value.X, pair.Value.Y));
            }
        }

        public void CopyQuests(List<QuestMark> into) {
            into.Clear();
            lock (sync) {
                foreach (QuestMark mark in quests.Values) into.Add(mark);
            }
        }

        public bool TryGetBoss(out int x, out int y) {
            lock (sync) {
                x = bossX;
                y = bossY;
                return bossX >= 0 && bossY >= 0;
            }
        }
    }
}
