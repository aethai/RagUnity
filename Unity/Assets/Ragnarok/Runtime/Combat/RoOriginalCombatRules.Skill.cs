using System.Collections.Generic;

namespace Ragnarok.Combat {
    public static partial class RoOriginalCombatRules {
        // ---- the queued hits of one ZC_NOTIFY_SKILL (skill damage handler FUN_00bede70, CSkill::SendMsg 0xd3ef40) ----

        /// <summary>Taekwon kick hits (msg 0x82 / 0x85 handlers 0xbda824 / 0xbda855): a blue burst and a white combo total.</summary>
        public const int SubtypeKick = 0x81, SubtypeKickFollow = 0x84;

        /// <summary>Pierce: its further hits come 280 ms apart instead of 200 (0xbef6c8).</summary>
        public const int PierceSkillId = 0x38;

        /// <summary>
        /// The hits FUN_00bede70 queues on the target for a skill used by an actor (skill-effects §2, combat-timing §3.2).
        /// Only damage types 4, 5, 6, 8, 9, 13 and 14 queue anything. Damage per hit = damage / count (a count outside
        /// 1..36 is 1), taken before the single-hit types force one hit. Splash (5) and type 14 skills without the direct
        /// flag (FUN_00bf0330) queue one hit at attackMT; every other skill queues its first hit at attackMT plus the skill's
        /// extra delay and, for types 8 / 9 / 13, one more hit every 200 ms (Pierce 280). A hit that carries a running total
        /// turns multi-hit (0x6f, unless 0x94): every further hit, and the first one when count &gt; 1 or for the skills
        /// that always show a total. Taekwon kicks go out as msg 0x82 / 0x85 (subtypes 0x81 / 0x84). Offsets are from the
        /// packet's receipt. Clears <paramref name="hits"/> first.
        /// </summary>
        public static void PlanSkillHits(int skillId, int damageType, int damage, int count, int attackMT, List<PlannedHit> hits) {
            hits.Clear();
            if (!QueuesSkillHits(damageType)) {
                return;
            }

            int n = count >= 1 && count <= MaxHitCount ? count : 1;
            int perHit = damage / n;
            if (damageType != 8 && damageType != 9 && damageType != 13) {
                n = 1;
            }

            if ((damageType == 5 || damageType == 14) && !IsSplashDirectSkill(skillId)) {
                // 0xbee038 / 0xbee0ec: one hit, no total, no extra delay
                hits.Add(new PlannedHit(attackMT, perHit, damageType == 5 ? SubtypeSkillSplash : SubtypeSkillSingle, 0, false));
                return;
            }

            int subtype = skillId == 0xaa ? SubtypeCritical : SkillHitSubtype(damageType);   // NPC_CRITICALSLASH
            bool kick = IsKickSkill(skillId);
            int due = attackMT + SkillFirstHitExtraMs(skillId);
            int step = skillId == PierceSkillId ? 280 : MultiHitMs;
            int total = perHit;
            int firstTotal = kick || n > 1 || AlwaysShowsSkillTotal(skillId) ? total : 0;
            hits.Add(new PlannedHit(due, perHit, WithTotal(subtype, kick ? SubtypeKick : SubtypeMultiHit, firstTotal), firstTotal, false));
            for (int k = 1; k < n; k++) {
                due += step;
                total += perHit;
                hits.Add(new PlannedHit(due, perHit, WithTotal(subtype, kick ? SubtypeKickFollow : SubtypeMultiHit, total), total, false));
            }
        }

        /// <summary>
        /// The hits a skill unit's damage queues (CSkill::SendMsg 0xd3ef40 case 1): damage / count per hit (count 0 is 1;
        /// no 36 limit, no damage type check), subtype 0x1a, the first hit at receipt and one more every 200 ms. Every hit
        /// carries the running total, so any hit that does damage is multi-hit (0x6f). Clears <paramref name="hits"/> first.
        /// </summary>
        public static void PlanUnitSkillHits(int damage, int count, List<PlannedHit> hits) {
            hits.Clear();
            int n = count == 0 ? 1 : count;
            int perHit = damage / n;
            int total = 0;
            for (int k = 0; k == 0 || k < n; k++) {
                total += perHit;
                hits.Add(new PlannedHit(MultiHitMs * k, perHit, WithTotal(SubtypeNormal, SubtypeMultiHit, total), total, false));
            }
        }

        /// <summary>Damage types FUN_00bede70 acts on (0xbee0f0): 4 endure, 5 splash, 6 skill, 8 / 9 multi-hit, 13 multi-hit
        /// critical, 14; anything else shows nothing.</summary>
        public static bool QueuesSkillHits(int damageType) =>
            damageType == 4 || damageType == 5 || damageType == 6 || damageType == 8 || damageType == 9 || damageType == 13 ||
            damageType == 14;

        /// <summary>The queued subtype of a skill damage type (0xbee938): 4 / 9 endure, 6 single (no flinch), 10 critical,
        /// 13 multi-hit critical, anything else splash (0x60).</summary>
        public static int SkillHitSubtype(int damageType) {
            switch (damageType) {
                case 4: case 9: return SubtypeEndure;
                case 6: return SubtypeSkillSingle;
                case 10: return SubtypeCritical;
                case 13: return SubtypeMultiHitCritical;
                default: return SubtypeSkillSplash;
            }
        }

        /// <summary>The first hit's delay after attackMT (0xbee990..): Turn Undead and 0x8ab +500, 0x8ac +1000, 0x8ad +1500,
        /// Down Kick, Turn Kick and 0xa20 / 0xa21 +300.</summary>
        public static int SkillFirstHitExtraMs(int skillId) {
            switch (skillId) {
                case 0x4d: case 0x8ab: return 500;
                case 0x8ac: return 1000;
                case 0x8ad: return 1500;
                case 0x19f: case 0x1a1: case 0xa20: case 0xa21: return 300;
                default: return 0;
            }
        }

        /// <summary>Skills whose hits go out as msg 0x82 / 0x85 (the Taekwon kicks and their later kin).</summary>
        public static bool IsKickSkill(int skillId) {
            switch (skillId) {
                case 0x19d: case 0x19f: case 0x1a1: case 0x1a3: case 0x1a5:
                case 0x91c: case 0x920: case 0x926: case 0xa10: case 0xa14: case 0xa20: case 0xa21:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Skills whose first hit carries a total even when it is the only hit.</summary>
        public static bool AlwaysShowsSkillTotal(int skillId) {
            switch (skillId) {
                case 0x111: case 0x173: case 0x8aa: case 0x8ab: case 0x8ac: case 0x8ad: case 0x918: case 0x919: case 0x91a:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>FUN_00bf0330: splash skills that take the normal skill path (tbl_actionfx.tsv splashDirect).</summary>
        public static bool IsSplashDirectSkill(int skillId) {
            switch (skillId) {
                case 11: case 47: case 273: case 413: case 732: case 2258: case 2284: case 2307: case 2317: case 2328: case 2329:
                case 2332: case 2337: case 2476: case 2519: case 2576: case 3006: case 3007: case 5026: case 5046: case 5211:
                case 5230: case 5235: case 5243: case 5244: case 5245: case 5248: case 5252: case 5283: case 5284: case 5316:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The queued-hit message handlers (0xbda7f0, 0xbda824, 0xbda855): a non-zero running total replaces the
        /// subtype (msg 0x1b: 0x6f unless it is 0x94; 0x82: 0x81; 0x85: 0x84).</summary>
        static int WithTotal(int subtype, int totalSubtype, int total) {
            if (total == 0) return subtype;
            return totalSubtype == SubtypeMultiHit && subtype == SubtypeMultiHitCritical ? subtype : totalSubtype;
        }
    }
}
