using System;
using System.Collections.Generic;
using Ragnarok.Network;

namespace Ragnarok.UI {
    /// <summary>The client's guild globals (DAT_014efa..): belong info, the tab mask, ZC_GUILD_INFO, the member and position
    /// caches. Copy on write: the session swaps in a new instance per change, the windows keep the one they drew.</summary>
    public sealed class RoClassicGuildState {
        public static readonly RoClassicGuildState None = new RoClassicGuildState();

        public uint GuildId;
        public string Name = string.Empty;
        public int EmblemVersion;
        /// <summary>ZC_UPDATE_GDID's position mode of the local character (1 invite, 0x10 expel, 0x100 storage).</summary>
        public int Rights;
        public bool BelongMaster;
        /// <summary>ZC_ACK_GUILD_MENUINTERFACE (DAT_014efb34): -1 until the server answered CZ_REQ_GUILD_MENUINTERFACE.</summary>
        public int Mask = -1;
        public RoGuildInfo Info;
        public RoGuildMember[] Members = Array.Empty<RoGuildMember>();
        /// <summary>Indexed by position id (20 from 0x0166 / 0x0160).</summary>
        public RoGuildPosition[] Positions = Array.Empty<RoGuildPosition>();
        public RoGuildRelation[] Relations = Array.Empty<RoGuildRelation>();
        public byte[] Castles = Array.Empty<byte>();
        public string NoticeSubject = string.Empty;
        public string NoticeBody = string.Empty;
        /// <summary>ZC_GUILD_SKILLINFO: the guild's skills and points (DAT_0154f310).</summary>
        public RoGuildSkill[] Skills = Array.Empty<RoGuildSkill>();
        public int SkillPoints;
        /// <summary>ZC_BAN_LIST (DAT_014efb7c); 0x0a87 names come through CZ_REQNAME_BYGID like the members'.</summary>
        public RoGuildBan[] Bans = Array.Empty<RoGuildBan>();
        /// <summary>The last leave / expulsion (0x0a83 / 0x0a82) for the chat line: a counter, its kind, who, their name, why.</summary>
        public int EventSerial;
        public RoGuildUpdateKind EventKind;
        public uint EventCharId;
        public string EventName = string.Empty;
        public string EventReason = string.Empty;
        public int EventValue;

        public bool HasGuild => GuildId != 0;

        public RoClassicGuildState Clone() => (RoClassicGuildState) MemberwiseClone();

        /// <summary>The guild master: ZC_GUILD_INFO's master char id (DAT_014efb60) when known, else ZC_UPDATE_GDID's flag.</summary>
        public bool IsMaster(uint localCharId) => Info != null && Info.MasterCharId != 0 ? Info.MasterCharId == localCharId : BelongMaster;

        public string PositionName(int positionId) =>
            positionId >= 0 && positionId < Positions.Length ? Positions[positionId].Name : string.Empty;

        /// <summary>An announcement for the windows (invitations, results) without other changes.</summary>
        public RoClassicGuildState WithEvent(RoGuildUpdateKind kind, uint id, string name, int value) {
            RoClassicGuildState next = Clone();
            next.SetEvent(kind, id, name, string.Empty, value);
            return next;
        }

        private void SetEvent(RoGuildUpdateKind kind, uint id, string name, string reason, int value) {
            EventSerial++;
            EventKind = kind;
            EventCharId = id;
            EventName = name ?? string.Empty;
            EventReason = reason ?? string.Empty;
            EventValue = value;
        }

        /// <summary>A fresh guild state that keeps the event counter (the windows compare it).</summary>
        private RoClassicGuildState Fresh() {
            var next = new RoClassicGuildState {
                EventSerial = EventSerial, EventKind = EventKind, EventCharId = EventCharId, EventName = EventName, EventReason = EventReason,
                EventValue = EventValue
            };
            return next;
        }

        public RoClassicGuildState WithBelong(RoZcGuildBelongInfo belong) {
            RoClassicGuildState next = belong.GuildId != GuildId ? Fresh() : Clone();
            next.GuildId = belong.GuildId;
            next.Name = belong.GuildName;
            next.EmblemVersion = belong.EmblemVersion;
            next.Rights = belong.Rights;
            next.BelongMaster = belong.IsMaster;
            return next;
        }

        public RoClassicGuildState WithNotice(RoZcGuildNotice notice) {
            RoClassicGuildState next = Clone();
            next.NoticeSubject = notice.Subject;
            next.NoticeBody = notice.Notice;
            return next;
        }

        public RoClassicGuildState WithName(uint charId, string name) {
            if (string.IsNullOrEmpty(name)) return this;
            int index = Array.FindIndex(Members, m => m.CharId == charId);
            int ban = Array.FindIndex(Bans, b => b.CharId == charId);
            bool member = index >= 0 && Members[index].Name != name, banned = ban >= 0 && Bans[ban].Name != name;
            if (!member && !banned) return this;
            RoClassicGuildState next = Clone();
            if (member) {
                next.Members = (RoGuildMember[]) Members.Clone();
                next.Members[index] = Members[index].WithName(name);
            }

            if (banned) {
                next.Bans = (RoGuildBan[]) Bans.Clone();
                next.Bans[ban] = Bans[ban].WithName(name);
            }

            return next;
        }

        /// <summary>Character ids still without a name (0x0aa5 lists carry none; the client asks CZ_REQNAME_BYGID).</summary>
        public void CollectNameless(List<uint> into) {
            foreach (RoGuildMember member in Members) {
                if (string.IsNullOrEmpty(member.Name)) into.Add(member.CharId);
            }

            foreach (RoGuildBan ban in Bans) {
                if (string.IsNullOrEmpty(ban.Name) && !into.Contains(ban.CharId)) into.Add(ban.CharId);
            }
        }

        public RoClassicGuildState Apply(RoGuildUpdate update) => Apply(update, 0);

        /// <summary>
        /// One guild packet. A leave / expulsion of <paramref name="localCharId"/> drops the guild (FUN_00c6b4b0 /
        /// FUN_00c6a0f0 clear it and close 0x3b); another member's is removed from the list. Both keep the event.
        /// </summary>
        public RoClassicGuildState Apply(RoGuildUpdate update, uint localCharId) {
            if (update == null) return this;
            if (update.Kind == RoGuildUpdateKind.Left || update.Kind == RoGuildUpdateKind.Expelled) {
                int index = Array.FindIndex(Members, m => m.CharId == update.CharId);
                bool self = localCharId != 0 && update.CharId == localCharId;
                RoClassicGuildState after = self ? Fresh() : Clone();
                if (!self && index >= 0) {
                    var members = new List<RoGuildMember>(Members);
                    members.RemoveAt(index);
                    after.Members = members.ToArray();
                }

                after.EventSerial = EventSerial + 1;
                after.EventKind = update.Kind;
                after.EventCharId = update.CharId;
                after.EventName = index >= 0 ? Members[index].Name : string.Empty;
                after.EventReason = update.Reason ?? string.Empty;
                return after;
            }

            switch (update.Kind) {
                case RoGuildUpdateKind.CreateResult:
                case RoGuildUpdateKind.AllyResult:
                case RoGuildUpdateKind.HostileResult:
                    return WithEvent(update.Kind, 0, string.Empty, update.Value);
                case RoGuildUpdateKind.AllyOffer:
                    return WithEvent(update.Kind, update.CharId, update.Reason, 0);
                case RoGuildUpdateKind.DisbandResult: {
                    // 0 (FUN at 0xc31e45): the guild is gone; 1 / 2 only a message
                    RoClassicGuildState after = update.Value == 0 ? Fresh() : Clone();
                    after.SetEvent(update.Kind, 0, string.Empty, string.Empty, update.Value);
                    return after;
                }
            }

            RoClassicGuildState next = Clone();
            switch (update.Kind) {
                case RoGuildUpdateKind.MenuInterface:
                    next.Mask = update.Mask;
                    break;
                case RoGuildUpdateKind.Info:
                    next.Info = update.Info;
                    if (next.GuildId == 0) next.GuildId = update.Info.GuildId;
                    if (!string.IsNullOrEmpty(update.Info.Name)) next.Name = update.Info.Name;
                    break;
                case RoGuildUpdateKind.MemberList: {
                    // keep the names already resolved for a nameless (0x0aa5) list
                    var members = update.Members ?? Array.Empty<RoGuildMember>();
                    for (int i = 0; i < members.Length; i++) {
                        if (!string.IsNullOrEmpty(members[i].Name)) continue;
                        uint charId = members[i].CharId;
                        int old = Array.FindIndex(Members, m => m.CharId == charId);
                        if (old >= 0 && !string.IsNullOrEmpty(Members[old].Name)) members[i] = members[i].WithName(Members[old].Name);
                    }

                    next.Members = members;
                    break;
                }
                case RoGuildUpdateKind.PositionNames:
                    next.Positions = MergePositions(Positions, update.Positions, true, false);
                    break;
                case RoGuildUpdateKind.PositionInfo:
                    next.Positions = MergePositions(Positions, update.Positions, false, true);
                    break;
                case RoGuildUpdateKind.PositionChanged:
                    next.Positions = MergePositions(Positions, update.Positions, true, true);
                    break;
                case RoGuildUpdateKind.MemberPositionChanged: {
                    var members = (RoGuildMember[]) Members.Clone();
                    foreach (var (accountId, charId, positionId) in update.MemberValues) {
                        int index = Array.FindIndex(members, m => m.AccountId == accountId && m.CharId == charId);
                        if (index >= 0) members[index] = members[index].WithPosition(positionId);
                    }

                    next.Members = members;
                    break;
                }
                case RoGuildUpdateKind.Relations:
                    next.Relations = update.Relations ?? Array.Empty<RoGuildRelation>();
                    break;
                case RoGuildUpdateKind.RelationAdded:
                case RoGuildUpdateKind.RelationRemoved: {
                    // 0x0185 (0xc6c8f0) / 0x0184 (0xa11980): one ally (0) or antagonist (1) in or out of the lists
                    RoGuildRelation changed = update.Relations[0];
                    var relations = new List<RoGuildRelation>(Relations);
                    relations.RemoveAll(r => r.GuildId == changed.GuildId && r.Relation == changed.Relation);
                    if (update.Kind == RoGuildUpdateKind.RelationAdded) relations.Add(changed);
                    next.Relations = relations.ToArray();
                    break;
                }
                case RoGuildUpdateKind.Castles:
                    next.Castles = update.Castles ?? Array.Empty<byte>();
                    break;
                case RoGuildUpdateKind.SkillList:
                    next.Skills = update.Skills ?? Array.Empty<RoGuildSkill>();
                    next.SkillPoints = update.SkillPoints;
                    break;
                case RoGuildUpdateKind.BanList: {
                    // keep the names already resolved for a nameless (0x0a87) list
                    var bans = update.Bans ?? Array.Empty<RoGuildBan>();
                    for (int i = 0; i < bans.Length; i++) {
                        if (!string.IsNullOrEmpty(bans[i].Name)) continue;
                        uint charId = bans[i].CharId;
                        int old = Array.FindIndex(Bans, b => b.CharId == charId);
                        if (old >= 0 && !string.IsNullOrEmpty(Bans[old].Name)) bans[i] = bans[i].WithName(Bans[old].Name);
                    }

                    next.Bans = bans;
                    break;
                }
                case RoGuildUpdateKind.MemberLogin: {
                    var (accountId, charId, state) = update.MemberValues[0];
                    int index = Array.FindIndex(Members, m => m.AccountId == accountId && m.CharId == charId);
                    if (index < 0) return this;
                    next.Members = (RoGuildMember[]) Members.Clone();
                    next.Members[index] = Members[index].WithState(state, update.Sex, update.Head, update.HeadPalette);
                    break;
                }
            }

            return next;
        }

        private static RoGuildPosition[] MergePositions(RoGuildPosition[] current, RoGuildPosition[] incoming, bool names, bool values) {
            int count = current.Length;
            foreach (RoGuildPosition p in incoming ?? Array.Empty<RoGuildPosition>()) count = Math.Max(count, p.Id + 1);
            var merged = new RoGuildPosition[count];
            Array.Copy(current, merged, current.Length);
            for (int i = current.Length; i < count; i++) merged[i] = new RoGuildPosition(i, 0, i, 0, string.Empty);
            foreach (RoGuildPosition p in incoming ?? Array.Empty<RoGuildPosition>()) {
                if (p.Id < 0) continue;
                RoGuildPosition old = merged[p.Id];
                merged[p.Id] = new RoGuildPosition(p.Id, values ? p.Rights : old.Rights, values ? p.Ranking : old.Ranking,
                    values ? p.PayRate : old.PayRate, names ? p.Name : old.Name);
            }

            return merged;
        }
    }
}
