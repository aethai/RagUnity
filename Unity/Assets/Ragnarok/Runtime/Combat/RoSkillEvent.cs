using System;
using Ragnarok.Network;

namespace Ragnarok.Combat {
    public enum RoSkillEventKind {
        /// <summary>ZC_USESKILL_ACK: a cast starts.</summary>
        Cast,
        /// <summary>ZC_NOTIFY_SKILL: a skill hits an actor.</summary>
        Damage,
        /// <summary>ZC_NOTIFY_SKILL_POSITION: a skill hits an actor and moves it.</summary>
        Position,
        /// <summary>ZC_USE_SKILL: a skill without damage takes effect.</summary>
        Result,
        /// <summary>ZC_NOTIFY_GROUNDSKILL: a skill on a cell.</summary>
        Ground,
        /// <summary>ZC_SKILL_ENTRY: a skill unit appears.</summary>
        Entry,
        /// <summary>ZC_SKILL_DISAPPEAR: a skill unit goes.</summary>
        Disappear,
    }

    /// <summary>One skill packet as <see cref="RoSkillPresenter"/> reads it.</summary>
    public readonly struct RoSkillEvent {
        public RoSkillEvent(RoSkillEventKind kind, ushort skillId, uint sourceActorId = 0, uint targetActorId = 0,
                            short x = 0, short y = 0, short level = 0, int damage = 0, short hitCount = 0,
                            uint delayMilliseconds = 0, uint element = 0, bool success = true, uint unitId = 0,
                            sbyte actionRaw = 0) {
            Kind = kind;
            SkillId = skillId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            X = x;
            Y = y;
            Level = level;
            Damage = damage;
            HitCount = hitCount;
            DelayMilliseconds = delayMilliseconds;
            Element = element;
            Success = success;
            UnitId = unitId;
            ActionRaw = actionRaw;
        }

        public RoSkillEventKind Kind { get; }
        /// <summary>The skill id; for <see cref="RoSkillEventKind.Entry"/> the unit's look (UNT_*).</summary>
        public ushort SkillId { get; }
        public uint SourceActorId { get; }
        public uint TargetActorId { get; }
        public short X { get; }
        public short Y { get; }
        public short Level { get; }
        /// <summary>Damage (ZC_NOTIFY_SKILL) or the amount of a ZC_USE_SKILL (heal points, chosen element ...).</summary>
        public int Damage { get; }
        public short HitCount { get; }
        /// <summary>ZC_USESKILL_ACK delaytime (the cast time), ZC_NOTIFY_SKILL attackMT.</summary>
        public uint DelayMilliseconds { get; }
        /// <summary>ZC_USESKILL_ACK property: the skill's element, which picks the begin-cast effect.</summary>
        public uint Element { get; }
        public bool Success { get; }
        public uint UnitId { get; }
        /// <summary>ZC_NOTIFY_SKILL damage type (6 single, 8 multi-hit ...).</summary>
        public sbyte ActionRaw { get; }

        public static RoSkillEvent FromCast(RoZcUseSkillAck packet) => new RoSkillEvent(
            RoSkillEventKind.Cast, packet.SkillId, packet.SourceId, packet.TargetId,
            unchecked((short) packet.X), unchecked((short) packet.Y),
            delayMilliseconds: packet.DelayTime, element: packet.Element);

        public static RoSkillEvent FromDamage(RoZcNotifySkill packet) => new RoSkillEvent(
            RoSkillEventKind.Damage, packet.SkillId, packet.SourceId, packet.TargetId,
            level: packet.Level, damage: packet.Damage, hitCount: packet.HitCount,
            delayMilliseconds: unchecked((uint) Math.Max(0, packet.SourceDelay)), actionRaw: packet.ActionRaw);

        public static RoSkillEvent FromPosition(RoZcNotifySkillPosition packet) => new RoSkillEvent(
            RoSkillEventKind.Position, packet.SkillId, packet.SourceId, packet.TargetId, packet.X, packet.Y,
            packet.Level, packet.Damage, packet.HitCount,
            delayMilliseconds: unchecked((uint) Math.Max(0, packet.SourceDelay)), actionRaw: packet.ActionRaw);

        public static RoSkillEvent FromResult(RoZcUseSkillResult packet) => new RoSkillEvent(
            RoSkillEventKind.Result, packet.SkillId, packet.SourceId, packet.TargetId,
            damage: packet.Value, success: packet.IsSuccess);

        public static RoSkillEvent FromGround(RoZcNotifyGroundSkill packet) => new RoSkillEvent(
            RoSkillEventKind.Ground, packet.SkillId, packet.SourceId, x: packet.X, y: packet.Y, level: packet.Level);

        public static RoSkillEvent FromEntry(RoZcSkillEntry packet) => new RoSkillEvent(
            RoSkillEventKind.Entry, packet.SkillId, packet.CreatorId, x: packet.X, y: packet.Y,
            level: unchecked((short) packet.SkillLevel), success: packet.Visible != 0, unitId: packet.UnitId);

        public static RoSkillEvent FromDisappear(RoZcSkillDisappear packet) => new RoSkillEvent(
            RoSkillEventKind.Disappear, 0, unitId: packet.UnitId);
    }
}
