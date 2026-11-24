using System;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Network {
    /// <summary>ZC_GUILD_INFO 0x0a84 (20200401, 94) / 0x0b7b (20240619, 118, the master name appended).</summary>
    public sealed class RoGuildInfo {
        public uint GuildId;
        public int Level;
        public int ConnectedMembers;
        public int MaxMembers;
        public int AverageLevel;
        public int Exp;
        public int MaxExp;
        public int Points;
        public int Honor;
        public int Virtue;
        public int EmblemVersion;
        public string Name = string.Empty;
        public string ManageLand = string.Empty;
        public int Zeny;
        public uint MasterCharId;
        public string MasterName = string.Empty;
    }

    /// <summary>One ZC_MEMBERMGR_INFO record: 0x0aa5 (34 bytes, no name) / 0x0b7d (58 bytes, the name appended).</summary>
    public readonly struct RoGuildMember {
        public RoGuildMember(uint accountId, uint charId, short head, short headPalette, short sex, short job, short level, int contributionExp, int state, int positionId, uint lastLogin, string name) {
            AccountId = accountId;
            CharId = charId;
            Head = head;
            HeadPalette = headPalette;
            Sex = sex;
            Job = job;
            Level = level;
            ContributionExp = contributionExp;
            State = state;
            PositionId = positionId;
            LastLogin = lastLogin;
            Name = name ?? string.Empty;
        }

        public uint AccountId { get; }
        public uint CharId { get; }
        public short Head { get; }
        public short HeadPalette { get; }
        public short Sex { get; }
        public short Job { get; }
        public short Level { get; }
        public int ContributionExp { get; }
        /// <summary>1 online, 0 offline.</summary>
        public int State { get; }
        public int PositionId { get; }
        public uint LastLogin { get; }
        /// <summary>Empty on 0x0aa5: the client resolves it with CZ_REQNAME_BYGID.</summary>
        public string Name { get; }

        public RoGuildMember WithPosition(int positionId) =>
            new RoGuildMember(AccountId, CharId, Head, HeadPalette, Sex, Job, Level, ContributionExp, State, positionId, LastLogin, Name);

        public RoGuildMember WithName(string name) =>
            new RoGuildMember(AccountId, CharId, Head, HeadPalette, Sex, Job, Level, ContributionExp, State, PositionId, LastLogin, name);

        public RoGuildMember WithState(int state, short sex, short head, short headPalette) =>
            new RoGuildMember(AccountId, CharId, state == 1 && head != 0 ? head : Head, state == 1 && head != 0 ? headPalette : HeadPalette,
                state == 1 && head != 0 ? sex : Sex, Job, Level, ContributionExp, state, PositionId, LastLogin, Name);
    }

    /// <summary>A guild position: the name from ZC_POSITION_ID_NAME_INFO 0x0166 and the rest from ZC_POSITION_INFO 0x0160 or
    /// ZC_ACK_CHANGE_GUILD_POSITIONINFO 0x0174.</summary>
    public readonly struct RoGuildPosition {
        public RoGuildPosition(int id, int rights, int ranking, int payRate, string name) {
            Id = id;
            Rights = rights;
            Ranking = ranking;
            PayRate = payRate;
            Name = name ?? string.Empty;
        }

        public int Id { get; }
        /// <summary>Bits: 1 invite, 0x10 expel, 0x100 storage.</summary>
        public int Rights { get; }
        public int Ranking { get; }
        public int PayRate { get; }
        public string Name { get; }
    }

    /// <summary>ZC_MYGUILD_BASIC_INFO 0x014c record: relation 0 ally, 1 antagonist.</summary>
    public readonly struct RoGuildRelation {
        public RoGuildRelation(int relation, uint guildId, string name) {
            Relation = relation;
            GuildId = guildId;
            Name = name ?? string.Empty;
        }

        public int Relation { get; }
        public uint GuildId { get; }
        public string Name { get; }
    }

    /// <summary>One ZC_GUILD_SKILLINFO record (37 bytes): id.W, inf.L, level.W, sp.W, range.W, name.24B, upgradable.B.</summary>
    public readonly struct RoGuildSkill {
        public RoGuildSkill(int id, int inf, int level, int sp, int range, string name, bool upgradable) {
            Id = id;
            Inf = inf;
            Level = level;
            Sp = sp;
            Range = range;
            Name = name ?? string.Empty;
            Upgradable = upgradable;
        }

        public int Id { get; }
        /// <summary>The skill type (0 passive).</summary>
        public int Inf { get; }
        public int Level { get; }
        public int Sp { get; }
        public int Range { get; }
        public string Name { get; }
        public bool Upgradable { get; }
    }

    /// <summary>One ZC_BAN_LIST record: 0x0a87 char id.L, reason.40B (44) / 0x0b7c + name.24B (68).</summary>
    public readonly struct RoGuildBan {
        public RoGuildBan(uint charId, string reason, string name) {
            CharId = charId;
            Reason = reason ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public uint CharId { get; }
        public string Reason { get; }
        public string Name { get; }

        public RoGuildBan WithName(string name) => new RoGuildBan(CharId, Reason, name);
    }

    public enum RoGuildUpdateKind {
        MenuInterface,
        Info,
        MemberList,
        PositionNames,
        PositionInfo,
        PositionChanged,
        MemberPositionChanged,
        Relations,
        Castles,
        MemberLogin,
        /// <summary>ZC_ACK_LEAVE_GUILD2 0x0a83: char id.L, reason.40B.</summary>
        Left,
        /// <summary>ZC_ACK_BAN_GUILD3 0x0a82: reason.40B, char id.L.</summary>
        Expelled,
        SkillList,
        BanList,
        /// <summary>ZC_RESULT_MAKE_GUILD 0x0167: result.B (0 created, 1 already in a guild, 2 name taken, 3 no Emperium, 4 area).</summary>
        CreateResult,
        /// <summary>ZC_ACK_DISORGANIZE_GUILD_RESULT 0x015e: result.L (0 disbanded, 1 wrong key, 2 members remain).</summary>
        DisbandResult,
        /// <summary>ZC_REQ_ALLY_GUILD 0x0171: inviter account.L, guild name.24B.</summary>
        AllyOffer,
        /// <summary>ZC_ACK_REQ_ALLY_GUILD 0x0173: result.B (0..5).</summary>
        AllyResult,
        /// <summary>ZC_ACK_REQ_HOSTILE_GUILD 0x0181: result.B (0..3).</summary>
        HostileResult,
        /// <summary>ZC_ADD_RELATED_GUILD 0x0185: relation.L, guild id.L, name.24B.</summary>
        RelationAdded,
        /// <summary>ZC_DELETE_RELATED_GUILD 0x0184: guild id.L, relation.L.</summary>
        RelationRemoved,
        /// <summary>ZC_REQ_JOIN_GUILD 0x016a (the existing parser): guild id, guild name.</summary>
        Invite,
        /// <summary>ZC_ACK_REQ_JOIN_GUILD 0x0169 (the existing parser): result.B (0..4).</summary>
        InviteResult
    }

    /// <summary>One parsed guild-manager packet.</summary>
    public sealed class RoGuildUpdate {
        public RoGuildUpdateKind Kind;
        public int Mask;
        public RoGuildInfo Info;
        public RoGuildMember[] Members;
        public RoGuildPosition[] Positions;
        public RoGuildRelation[] Relations;
        public byte[] Castles;
        /// <summary>0x0156 records (account, char, position) and the 0x01f2 login record (account, char, state).</summary>
        public (uint AccountId, uint CharId, int Value)[] MemberValues;
        /// <summary>0x01f2: sex, hair style, hair colour (zero when the member logged out).</summary>
        public short Sex, Head, HeadPalette;
        /// <summary>0x0a83 / 0x0a82: the member who left or was expelled, and the reason.</summary>
        public uint CharId;
        public string Reason = string.Empty;
        public RoGuildSkill[] Skills;
        public int SkillPoints;
        public RoGuildBan[] Bans;
        /// <summary>The result of 0x0167 / 0x015e / 0x0173 / 0x0169.</summary>
        public int Value;
    }

    public static class RoGuildPacketParser {
        public const int NameLength = 24;
        public const int MapNameLength = 16;

        public static bool Handles(ushort id) {
            switch (id) {
                case RoPacketIds.ZcGuildMenuInterface:
                case RoPacketIds.ZcGuildInfo3:
                case RoPacketIds.ZcGuildInfo4:
                case RoPacketIds.ZcMemberMgrInfo2:
                case RoPacketIds.ZcMemberMgrInfo3:
                case RoPacketIds.ZcPositionIdNameInfo:
                case RoPacketIds.ZcPositionInfo:
                case RoPacketIds.ZcAckChangeGuildPositionInfo:
                case RoPacketIds.ZcAckReqChangeMembers:
                case RoPacketIds.ZcMyGuildBasicInfo:
                case RoPacketIds.ZcGuildAgitInfo:
                case RoPacketIds.ZcUpdateCharStat2:
                case RoPacketIds.ZcAckBanGuild3:
                case RoPacketIds.ZcAckLeaveGuild2:
                case RoPacketIds.ZcGuildSkillInfo:
                case RoPacketIds.ZcBanList2:
                case RoPacketIds.ZcBanList3:
                case RoPacketIds.ZcAckDisorganizeGuildResult:
                case RoPacketIds.ZcResultMakeGuild:
                case RoPacketIds.ZcReqAllyGuild:
                case RoPacketIds.ZcAckReqAllyGuild:
                case RoPacketIds.ZcAckReqHostileGuild:
                case RoPacketIds.ZcAddRelatedGuild:
                case RoPacketIds.ZcDeleteRelatedGuild:
                    return true;
                default:
                    return false;
            }
        }

        public static RoGuildUpdate Parse(byte[] frame, Encoding encoding = null) {
            if (frame == null || frame.Length < 2) throw new ArgumentException("Guild packet frame is empty.", nameof(frame));
            var reader = new RoPacketReader(frame);
            ushort id = reader.ReadUInt16();
            switch (id) {
                case RoPacketIds.ZcGuildMenuInterface:
                    RequireLength(frame, id, 6);
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.MenuInterface, Mask = reader.ReadInt32() };
                case RoPacketIds.ZcGuildInfo3:
                case RoPacketIds.ZcGuildInfo4: {
                    RequireLength(frame, id, id == RoPacketIds.ZcGuildInfo4 ? 118 : 94);
                    var info = new RoGuildInfo {
                        GuildId = reader.ReadUInt32(),
                        Level = reader.ReadInt32(),
                        ConnectedMembers = reader.ReadInt32(),
                        MaxMembers = reader.ReadInt32(),
                        AverageLevel = reader.ReadInt32(),
                        Exp = reader.ReadInt32(),
                        MaxExp = reader.ReadInt32(),
                        Points = reader.ReadInt32(),
                        Honor = reader.ReadInt32(),
                        Virtue = reader.ReadInt32(),
                        EmblemVersion = reader.ReadInt32(),
                        Name = reader.ReadFixedString(NameLength, encoding),
                        ManageLand = reader.ReadFixedString(MapNameLength, encoding),
                        Zeny = reader.ReadInt32(),
                        MasterCharId = reader.ReadUInt32()
                    };
                    if (id == RoPacketIds.ZcGuildInfo4) info.MasterName = reader.ReadFixedString(NameLength, encoding);
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.Info, Info = info };
                }
                case RoPacketIds.ZcMemberMgrInfo2:
                case RoPacketIds.ZcMemberMgrInfo3: {
                    bool named = id == RoPacketIds.ZcMemberMgrInfo3;
                    var records = Records(reader, frame, id, named ? 58 : 34);
                    var members = new RoGuildMember[records];
                    for (int i = 0; i < records; i++) {
                        uint aid = reader.ReadUInt32(), gid = reader.ReadUInt32();
                        short head = reader.ReadInt16(), palette = reader.ReadInt16(), sex = reader.ReadInt16(), job = reader.ReadInt16(), level = reader.ReadInt16();
                        int exp = reader.ReadInt32(), state = reader.ReadInt32(), position = reader.ReadInt32();
                        uint lastLogin = reader.ReadUInt32();
                        string name = named ? reader.ReadFixedString(NameLength, encoding) : string.Empty;
                        members[i] = new RoGuildMember(aid, gid, head, palette, sex, job, level, exp, state, position, lastLogin, name);
                    }

                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.MemberList, Members = members };
                }
                case RoPacketIds.ZcPositionIdNameInfo: {
                    int records = Records(reader, frame, id, 4 + NameLength);
                    var positions = new RoGuildPosition[records];
                    for (int i = 0; i < records; i++) {
                        int pid = reader.ReadInt32();
                        positions[i] = new RoGuildPosition(pid, 0, 0, 0, reader.ReadFixedString(NameLength, encoding));
                    }

                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.PositionNames, Positions = positions };
                }
                case RoPacketIds.ZcPositionInfo:
                case RoPacketIds.ZcAckChangeGuildPositionInfo: {
                    bool named = id == RoPacketIds.ZcAckChangeGuildPositionInfo;
                    int records = Records(reader, frame, id, named ? 16 + NameLength : 16);
                    var positions = new RoGuildPosition[records];
                    for (int i = 0; i < records; i++) {
                        int pid = reader.ReadInt32(), rights = reader.ReadInt32(), ranking = reader.ReadInt32(), payRate = reader.ReadInt32();
                        positions[i] = new RoGuildPosition(pid, rights, ranking, payRate, named ? reader.ReadFixedString(NameLength, encoding) : string.Empty);
                    }

                    return new RoGuildUpdate { Kind = named ? RoGuildUpdateKind.PositionChanged : RoGuildUpdateKind.PositionInfo, Positions = positions };
                }
                case RoPacketIds.ZcAckReqChangeMembers: {
                    int records = Records(reader, frame, id, 12);
                    var values = new (uint, uint, int)[records];
                    for (int i = 0; i < records; i++) values[i] = (reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadInt32());
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.MemberPositionChanged, MemberValues = values };
                }
                case RoPacketIds.ZcMyGuildBasicInfo: {
                    int records = Records(reader, frame, id, 8 + NameLength);
                    var relations = new RoGuildRelation[records];
                    for (int i = 0; i < records; i++) {
                        int relation = reader.ReadInt32();
                        uint guildId = reader.ReadUInt32();
                        relations[i] = new RoGuildRelation(relation, guildId, reader.ReadFixedString(NameLength, encoding));
                    }

                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.Relations, Relations = relations };
                }
                case RoPacketIds.ZcGuildAgitInfo: {
                    int records = Records(reader, frame, id, 1);
                    var castles = new byte[records];
                    for (int i = 0; i < records; i++) castles[i] = reader.ReadByte();
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.Castles, Castles = castles };
                }
                case RoPacketIds.ZcUpdateCharStat2: {
                    RequireLength(frame, id, 20);
                    uint aid = reader.ReadUInt32(), gid = reader.ReadUInt32();
                    int state = reader.ReadInt32();
                    return new RoGuildUpdate {
                        Kind = RoGuildUpdateKind.MemberLogin,
                        MemberValues = new[] { (aid, gid, state) },
                        Sex = reader.ReadInt16(),
                        Head = reader.ReadInt16(),
                        HeadPalette = reader.ReadInt16()
                    };
                }
                case RoPacketIds.ZcGuildSkillInfo: {
                    int length = reader.ReadUInt16();
                    if (length != frame.Length || length < 6 || (length - 6) % 37 != 0) throw new ArgumentException($"Packet 0x{id:x4} has a bad length {length}.", nameof(frame));
                    int points = reader.ReadInt16();
                    var skills = new RoGuildSkill[(length - 6) / 37];
                    for (int i = 0; i < skills.Length; i++) {
                        int skillId = reader.ReadUInt16(), inf = reader.ReadInt32(), level = reader.ReadUInt16(), sp = reader.ReadUInt16(), range = reader.ReadUInt16();
                        string name = reader.ReadFixedString(NameLength, encoding);
                        skills[i] = new RoGuildSkill(skillId, inf, level, sp, range, name, reader.ReadByte() != 0);
                    }

                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.SkillList, Skills = skills, SkillPoints = points };
                }
                case RoPacketIds.ZcBanList2:
                case RoPacketIds.ZcBanList3: {
                    bool named = id == RoPacketIds.ZcBanList3;
                    int records = Records(reader, frame, id, named ? 68 : 44);
                    var bans = new RoGuildBan[records];
                    for (int i = 0; i < records; i++) {
                        uint charId = reader.ReadUInt32();
                        string reason = reader.ReadFixedString(40, encoding);
                        bans[i] = new RoGuildBan(charId, reason, named ? reader.ReadFixedString(NameLength, encoding) : string.Empty);
                    }

                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.BanList, Bans = bans };
                }
                case RoPacketIds.ZcResultMakeGuild:
                    RequireLength(frame, id, 3);
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.CreateResult, Value = reader.ReadByte() };
                case RoPacketIds.ZcAckReqAllyGuild:
                    // clif_guild_allianceack writes a u32 but sends 3 bytes: only the low result byte is on the wire
                    RequireLength(frame, id, 3);
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.AllyResult, Value = reader.ReadByte() };
                case RoPacketIds.ZcAckReqHostileGuild:
                    RequireLength(frame, id, 3);
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.HostileResult, Value = reader.ReadByte() };
                case RoPacketIds.ZcAddRelatedGuild: {
                    RequireLength(frame, id, 34);
                    int relation = reader.ReadInt32();
                    uint guildId = reader.ReadUInt32();
                    return new RoGuildUpdate {
                        Kind = RoGuildUpdateKind.RelationAdded,
                        Relations = new[] { new RoGuildRelation(relation, guildId, reader.ReadFixedString(NameLength, encoding)) }
                    };
                }
                case RoPacketIds.ZcDeleteRelatedGuild: {
                    RequireLength(frame, id, 10);
                    uint guildId = reader.ReadUInt32();
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.RelationRemoved, Relations = new[] { new RoGuildRelation(reader.ReadInt32(), guildId, string.Empty) } };
                }
                case RoPacketIds.ZcAckDisorganizeGuildResult:
                    RequireLength(frame, id, 6);
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.DisbandResult, Value = reader.ReadInt32() };
                case RoPacketIds.ZcReqAllyGuild: {
                    RequireLength(frame, id, 30);
                    uint inviter = reader.ReadUInt32();
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.AllyOffer, CharId = inviter, Reason = reader.ReadFixedString(NameLength, encoding) };
                }
                case RoPacketIds.ZcAckLeaveGuild2: {
                    RequireLength(frame, id, 46);
                    uint charId = reader.ReadUInt32();
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.Left, CharId = charId, Reason = reader.ReadFixedString(40, encoding) };
                }
                case RoPacketIds.ZcAckBanGuild3: {
                    RequireLength(frame, id, 46);
                    string reason = reader.ReadFixedString(40, encoding);
                    return new RoGuildUpdate { Kind = RoGuildUpdateKind.Expelled, CharId = reader.ReadUInt32(), Reason = reason };
                }
                default:
                    throw new ArgumentException($"Packet 0x{id:x4} is not a guild manager packet.", nameof(frame));
            }
        }

        private static void RequireLength(byte[] frame, ushort id, int length) {
            if (frame.Length != length) throw new ArgumentException($"Packet 0x{id:x4} must be {length} bytes, got {frame.Length}.", nameof(frame));
        }

        /// <summary>Reads the variable header and returns the record count.</summary>
        private static int Records(RoPacketReader reader, byte[] frame, ushort id, int recordLength) {
            int length = reader.ReadUInt16();
            if (length != frame.Length || length < 4) throw new ArgumentException($"Packet 0x{id:x4} declares {length} bytes, got {frame.Length}.", nameof(frame));
            if ((length - 4) % recordLength != 0) throw new ArgumentException($"Packet 0x{id:x4} has {length - 4} payload byte(s), not a multiple of {recordLength}.", nameof(frame));
            return (length - 4) / recordLength;
        }
    }
}
