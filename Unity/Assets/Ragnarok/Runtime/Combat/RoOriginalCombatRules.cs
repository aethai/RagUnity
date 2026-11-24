using System;
using System.Collections.Generic;

namespace Ragnarok.Combat {
    /// <summary>
    /// Combat rules of the original client (Custom game.exe), one place for each: hit timing, the queued hits of an
    /// attack, the flinch, damage number styles, hit sounds. Pure functions; addresses refer to game.exe. The research,
    /// with the oracle-checked reference implementation these are copied from, is in re-custom
    /// packages/combat-timing/NOTES.md (section numbers below) and packages/hit56/NOTES.md. Float math is single precision
    /// like the client's SSE code.
    /// </summary>
    public static partial class RoOriginalCombatRules {
        /// <summary>DAT_0118195c: the interval between the queued hits of one multi-hit attack or skill.</summary>
        public const float MultiHitSeconds = 0.2f;
        public const int MultiHitMs = 200;
        /// <summary>Dual wield: the right hand's further hits at hit delay + 100, the left-hand hit at + 350 (§1.2).</summary>
        public const int RightHandExtraHitMs = 100;
        public const int LeftHandHitMs = 350;
        /// <summary>Upper bound of attackMT for the attack motion speed (DWORD 0x1181e20 = 432).</summary>
        public const int AttackMotionCap = 432;
        /// <summary>attackMT / attackedMT 0 in the packet become 1440 (attackedMT stays 0 for actions 0 and 4, §1.1).</summary>
        public const int ZeroMotionTimeMs = 1440;
        /// <summary>dmotion = attackedMT / 288 (the player hurt motion: 3 frames × delay 4 × 24 ms).</summary>
        public const float HurtMotionUnitMs = 288f;
        /// <summary>One ACT delay unit (motion process 0xbd44e0).</summary>
        public const float ActDelayUnitMs = 24f;
        /// <summary>A ZC_NOTIFY_ACT with this damage is ignored (0xc54eb6).</summary>
        public const int IgnoredActDamage = -30000;
        /// <summary>Damage that shows no number (FUN_00beda30).</summary>
        public const int NoNumberDamage = -29999;
        /// <summary>A multi-hit count outside 1..36 counts as one hit (§1.2).</summary>
        public const int MaxHitCount = 36;

        // ---- attacker side: hit delay (§1) -------------------------------------------------------------------

        /// <summary>attackMT / attackedMT as the client uses them (§1.1): 0 becomes 1440, except attackedMT for actions
        /// 0 and 4, which stays 0.</summary>
        public static int EffectiveAttackMotion(int attackMT) => attackMT == 0 ? ZeroMotionTimeMs : attackMT;

        public static int EffectiveAttackedMotion(int attackedMT, int action) =>
            attackedMT == 0 && action != 0 && action != 4 ? ZeroMotionTimeMs : attackedMT;

        /// <summary>
        /// Attacker vf 0x9c: min(attackMT, 432) / 432; a player whose weapon class is a bow (11) is not clamped
        /// (CPc/CPlayer 0xccca30).
        /// </summary>
        public static float MotionSpeedFactor(int attackMT, bool playerWithBow) {
            int mt = !playerWithBow && attackMT > AttackMotionCap ? AttackMotionCap : attackMT;
            return (float) mt / 432.0f;
        }

        /// <summary>
        /// Render SetAction frame delay (CGameActor::vf07 0xbe3ab0): the ACT delay of the action (a value ≤ 0 keeps the
        /// previous one), at least 1, times the motion speed factor when that product is positive.
        /// </summary>
        public static float FrameDelay(float actDelay, float factor, float previous = 4f) {
            float d = actDelay > 0f ? actDelay : previous;
            if (1.0f > d) d = 1.0f;
            float x = d * factor;
            return x > 0f ? x : d;
        }

        /// <summary>
        /// Monster / NPC attack hit frame (vf 0x94 0xbd0a70): the first frame of the attack action (direction 0) whose
        /// ACT event is "atk"; otherwise frameCount − 2 (0 for a single frame).
        /// </summary>
        /// <param name="frameEventIds">Per frame the ACT event id (−1 none).</param>
        /// <param name="eventNames">The ACT's event names (an id past the end reads as "").</param>
        public static int ActHitFrame(IReadOnlyList<int> frameEventIds, IReadOnlyList<string> eventNames) {
            int n = frameEventIds?.Count ?? 0;
            for (int i = 0; i < n; i++) {
                int id = frameEventIds[i];
                if (id == -1) continue;
                string name = eventNames != null && (uint) id < (uint) eventNames.Count ? eventNames[id] : string.Empty;
                if (name == "atk") return i;
            }

            return n - (n != 1 ? 2 : 1);
        }

        /// <summary>
        /// Player hit frame FUN_00cec1d0(job, sex, weapon look, attack3): attack motion 2 (ACT action 10): merchant 5.85,
        /// thief 5.75, else 6; motion 9 (ACT action 11): novice / super novice males 5.85, assassin classes with a katar
        /// or a dual-wield weapon type 3, else 6.
        /// </summary>
        public static float PlayerHitFrame(int job, int sex, int weaponLook, bool attack3) {
            if (!attack3) {
                if (job == 5) return 5.85f;
                if (job == 6) return 5.75f;
                return 6.0f;
            }

            switch (job) {
                case 0: case 23: case 4045: case 4190: case 4191:
                    return sex == 1 ? 5.85f : 6.0f;
                case 12: case 4013: case 4035: case 4059: case 4065: case 4101: case 4254:
                    return weaponLook == 16 || (weaponLook >= 25 && weaponLook <= 30) ? 3.0f : 6.0f;
                default:
                    return 6.0f;
            }
        }

        /// <summary>
        /// A player's basic attack motion (§6, CPc::SetAction 0xcd0b92): motion 9 (ACT action 11) when the job / sex table
        /// lists the weapon class, motion 2 (ACT action 10) otherwise; the Taekwon line picks at random, motion 9 when
        /// rand() % 10 &lt; 7. A critical or the left hand never changes it.
        /// </summary>
        public static bool UsesAttackMotion9(int job, int sex, int weaponClass, Func<int> rand) {
            switch (job) {
                case 4046: case 4047: case 4048: case 4225: case 4226: case 4238: case 4239: case 4241: case 4243: case 4244:
                case 4302: case 4316:
                    return Rand(rand) % 10 < 7;
            }

            return (uint) weaponClass <= 30 && (AttackMotion9Classes(job, sex) & (1u << weaponClass)) != 0;
        }

        /// <summary>Lua IsItemUsingArrow: the weapon classes of BowTypeList (bow, crossbow, arbalest, kakkung, hunter bow,
        /// bow of Rudra). Instruments and whips shoot no arrow on a basic attack (§5.1).</summary>
        public static bool UsesArrow(int weaponClass) => weaponClass == 11 || (weaponClass >= 73 && weaponClass <= 77);

        /// <summary>
        /// Monsters whose attack launches a CArrowEffect (FUN_00bd2930 from CNpc::SetAction 0xcc287f; combat-timing §5.1):
        /// archer skeletons, orc / goblin / kobold / raydric archers, gargoyles, mercenary archers, the bullet shooters.
        /// Their hit delay is not extended by the flight (only the mob extras of <see cref="HitDelayExtraMs"/>).
        /// </summary>
        public static bool MonsterShootsArrow(int job) {
            switch (job) {
                case 1016: case 1189: case 1253: case 1258: case 1276: case 1282: case 1285: case 1420: case 1410: case 1412:
                case 1453: case 1455: case 1473: case 1495: case 1498: case 1500: case 1531: case 1550: case 1555: case 1664:
                case 1665: case 1666: case 1667: case 1688: case 1865: case 2021: case 2364: case 2475: case 3882: case 20274:
                case 20275: case 20279: case 20368: case 20575: case 20578: case 20585: case 20851: case 21520: case 21589:
                case 21970: case 21985: case 22001:
                    return true;
                default:
                    return job >= 6017 && job <= 6026;
            }
        }

        /// <summary>The arrow's flight added to a player's hit frame (CPc::SetAction 0xcd0d6e): 8 ticks = 192 ms.</summary>
        public static float AddArrowFlight(float hitFrame, float frameDelay) => 8.0f / frameDelay + hitFrame;

        /// <summary>FUN_00bebd90: hit delay in ms after the packet arrives, before the job extras.</summary>
        public static uint HitDelayMs(float hitFrame, float frameDelay) => FloatToUInt(hitFrame * frameDelay * ActDelayUnitMs);

        /// <summary>Extra hit delay by attacker job / mob id (0xbebf8e..0xbec015).</summary>
        public static int HitDelayExtraMs(int attackerJob) {
            switch (attackerJob) {
                case 1016: case 1420: return 192;
                case 1285: case 1830: return 912;
                case 1286: case 1829: case 1287: return 408;
                default: return 0;
            }
        }

        /// <summary>FUN_00d48d60: float → unsigned int by truncation.</summary>
        public static uint FloatToUInt(float f) => f >= 2147483648.0f ? (uint) (long) f : (uint) (int) f;

        // ---- job kinds ------------------------------------------------------------------------------------------

        /// <summary>FUN_00d02e40: player jobs (0..30, 4001..5999).</summary>
        public static bool IsPlayerJob(int job) => (uint) (job - 4001) <= 0x7ce || (uint) job <= 0x1e;

        /// <summary>FUN_00d1e240: monsters (1001..3998, 20021..31999).</summary>
        public static bool IsMonsterJob(int job) => (job > 1000 && job < 3999) || (uint) (job - 20021) <= 0x2eca;

        /// <summary>FUN_00d01810: NPC jobs (45..999, 10001..19998).</summary>
        public static bool IsNpcJob(int job) => (job >= 45 && job <= 999) || (job >= 10001 && job <= 19998);

        /// <summary>Targets whose damage numbers are white with combo totals (§3.2): NPCs and monsters, not players,
        /// homunculi or mercenaries.</summary>
        public static bool IsMonsterLike(int job) => (job >= 45 && job < 1000) || (job > 1000 && job < 3999) || (job >= 20021 && job <= 31999);

        // ---- the queued hits of one ZC_NOTIFY_ACT (§1.2) ----------------------------------------------------------

        /// <summary>Hit subtypes of the queued hit message (0xbec3b8).</summary>
        public const int SubtypeNormal = 0x1a, SubtypeEndure = 0x27, SubtypeCritical = 0x5d, SubtypeLucky = 0x5e,
            SubtypeMultiHit = 0x6f, SubtypeMultiHitCritical = 0x94, SubtypeSkillSplash = 0x60, SubtypeSkillSingle = 0x61;

        /// <summary>Whether a ZC_NOTIFY_ACT action is an attack (0, 4, 8, 9, 10, 11, 13); the others are pick-up, sit, stand
        /// or nothing.</summary>
        public static bool IsAttackAction(int action) =>
            action == 0 || action == 4 || action == 8 || action == 9 || action == 10 || action == 11 || action == 13;

        /// <summary>The hit subtype of an attack action: 0/8 normal, 4/9 endure, 10 critical, 13 multi-hit critical,
        /// 11 lucky dodge.</summary>
        public static int HitSubtype(int action) {
            switch (action) {
                case 4: case 9: return SubtypeEndure;
                case 10: return SubtypeCritical;
                case 11: return SubtypeLucky;
                case 13: return SubtypeMultiHitCritical;
                default: return SubtypeNormal;
            }
        }

        /// <summary>The hit count: the packet's count for actions 8, 9 and 13 when it is 1..36, else one hit.</summary>
        public static int HitCount(int action, int count) =>
            (action == 8 || action == 9 || action == 13) && count >= 1 && count <= MaxHitCount ? count : 1;

        /// <summary>Whether the left-hand damage is shown: NPC jobs, jobs above 4000 and the assassin (12).</summary>
        public static bool ShowsLeftHand(int attackerJob, int leftDamage) =>
            leftDamage != 0 && (IsNpcJob(attackerJob) || attackerJob > 4000 || attackerJob == 12);

        /// <summary>One queued hit: due offset after the hit delay, its damage and subtype, the running total (count &gt; 1).</summary>
        public readonly struct PlannedHit {
            public readonly int OffsetMs;
            public readonly int Damage;
            public readonly int Subtype;
            /// <summary>Sum of the damage shown so far, this hit included; 0 when the attack is a single hit.</summary>
            public readonly int RunningTotal;
            public readonly bool LeftHand;

            public PlannedHit(int offsetMs, int damage, int subtype, int runningTotal, bool leftHand) {
                OffsetMs = offsetMs;
                Damage = damage;
                Subtype = subtype;
                RunningTotal = runningTotal;
                LeftHand = leftHand;
            }
        }

        /// <summary>
        /// The hits FUN_00bebd90 queues for one attack (§1.2): damage / count per hit (integer), hit k at 200·k ms, or with
        /// a shown left hand the further right-hand hits at +100 and the left-hand hit at +350. With more than one hit
        /// every hit carries the running total and its subtype becomes multi-hit (0x6f, msg 0x1b handler 0xbda7f0) unless
        /// it is 0x94. Offsets are relative to the hit delay. Clears <paramref name="hits"/> first.
        /// </summary>
        public static void PlanHits(int action, int damage, int count, int leftDamage, int attackerJob, List<PlannedHit> hits) {
            hits.Clear();
            int n = HitCount(action, count);
            int subtype = HitSubtype(action);
            bool left = ShowsLeftHand(attackerJob, leftDamage);
            bool running = n > 1;
            if (running && subtype != SubtypeMultiHitCritical) {
                subtype = SubtypeMultiHit;
            }

            int perHit = damage / n;
            int total = 0;
            for (int k = 0; k < n; k++) {
                total += perHit;
                int offset = k == 0 ? 0 : left ? RightHandExtraHitMs : MultiHitMs * k;
                hits.Add(new PlannedHit(offset, perHit, subtype, running ? total : 0, false));
            }

            if (left) {
                total += leftDamage;
                hits.Add(new PlannedHit(LeftHandHitMs, leftDamage, subtype, running ? total : 0, true));
            }
        }

        /// <summary>
        /// effect2 of a landed basic hit (hit56, FUN_00bebd90 step 7): EF 0 (EF_HIT1), EF 434 (EF_HITLINE7) for the Taekwon
        /// line, nothing (−1) on a hit on oneself. effect1 is EF 1 (EF_HIT2) on criticals (subtypes 0x5d / 0x94).
        /// </summary>
        public static int BasicHitEffect(int attackerJob, bool selfHit) => selfHit ? -1 : IsTaekwonLine(attackerJob) ? 434 : 0;

        /// <summary>Taekwon-line jobs (Taekwon, Star Gladiator, Soul Linker and their baby / expanded forms).</summary>
        public static bool IsTaekwonLine(int job) {
            switch (job) {
                case 4046: case 4047: case 4048: case 4225: case 4226: case 4238: case 4239: case 4240: case 4241: case 4242:
                case 4243: case 4302: case 4303: case 4316:
                    return true;
                default:
                    return false;
            }
        }

        // ---- target side: flinch (§2) -----------------------------------------------------------------------------

        /// <summary>Actor states the flinch rule reads (CGameActor state machine).</summary>
        public const int StateIdle = 0, StateWalk = 1, StateAttack = 2, StateDead = 3, StateHurt = 4;

        /// <summary>
        /// Does the hit handler call SetAction(4) (the damage motion) on the target? Not for a heal, a dead target, endure
        /// (0x27) or subtype 0x61, a player-job target in attack state 2, or a monster with a negative dmotion.
        /// SetAction(4) itself also refuses while a scripted motion runs, for fake death and for monsters showing an ACT
        /// action ≥ 40 (callers check those).
        /// </summary>
        public static bool Flinches(int subtype, bool heal, int targetState, int targetJob, float dmotion) {
            if (heal || targetState == StateDead) return false;
            if (subtype == SubtypeEndure || subtype == SubtypeSkillSingle) return false;
            if (IsPlayerJob(targetJob)) return targetState != StateAttack;
            return !(IsMonsterJob(targetJob) && -0.00001f > dmotion);
        }

        /// <summary>dmotion of a queued hit: attackedMT / 288.</summary>
        public static float DamageMotion(int attackedMT) => attackedMT / HurtMotionUnitMs;

        /// <summary>
        /// The damage motion after SetAction(4) (§2.3): dmotion &gt; 1 holds the last frame until dmotion × the motion
        /// length; otherwise the frame delay becomes dmotion · frameDelay for player jobs or dmotion itself for everyone
        /// else (an original inconsistency) when that is positive. Returns (frame delay, hold factor).
        /// </summary>
        public static (float FrameDelay, float Hold) HurtMotion(float hurtFrameDelay, float dmotion, int targetJob) {
            if (dmotion > 1.0f) return (hurtFrameDelay, dmotion);
            float x = IsPlayerJob(targetJob) ? dmotion * hurtFrameDelay : dmotion;
            return (x > 0f ? x : hurtFrameDelay, 1.0f);
        }

        /// <summary>Length of a play-once motion: frameCount · fd · 24 · max(1, hold) ms.</summary>
        public static float MotionLengthMs(int frameCount, float frameDelay, float hold) =>
            frameCount * frameDelay * ActDelayUnitMs * Math.Max(1.0f, hold);

        // ---- damage numbers (§3) ----------------------------------------------------------------------------------

        /// <summary>CMsgEffect number types.</summary>
        public const int NumberNormal = 0xe, NumberCritical = 0x10, NumberMultiHitPart = 0x15, NumberComboTotal = 0x16,
            NumberHeal = 0x72;

        public const uint NumberWhite = 0xFFFFFFFF, NumberRed = 0xFFFF0000, NumberYellow = 0xFFFFFF00, NumberSpBlue = 0xFF1E90FF,
            NumberHealGreen = 0xFF00FF00;

        /// <summary>One number the hit handler shows over the target.</summary>
        public readonly struct HitNumber {
            public readonly int Type;
            public readonly int Value;
            public readonly uint Argb;
            /// <summary>The red "Critical" burst (msg 0x5c) behind it.</summary>
            public readonly bool CriticalBurst;
            /// <summary>The Taekwon kick's blue burst (msg 0x83) behind it.</summary>
            public readonly bool ComboBurst;

            public HitNumber(int type, int value, uint argb, bool criticalBurst, bool comboBurst = false) {
                Type = type;
                Value = value;
                Argb = argb;
                CriticalBurst = criticalBurst;
                ComboBurst = comboBurst;
            }
        }

        /// <summary>
        /// The numbers of a landed hit (damage &gt; 0) over the target (§3.2): the colour depends only on the target's
        /// kind (white monster-like, red player-like), critical yellow with a burst, combo totals (yellow, the latest one
        /// replaces the previous; white after a Taekwon kick, the first one with its blue burst) only on monster-like
        /// targets; SP damage blue. Clears <paramref name="numbers"/> first.
        /// </summary>
        public static void HitNumbers(int subtype, int damage, int runningTotal, bool isSp, int targetJob, List<HitNumber> numbers) {
            numbers.Clear();
            uint spWhite = isSp ? NumberSpBlue : NumberWhite;
            uint spYellow = isSp ? NumberSpBlue : NumberYellow;
            if (subtype == SubtypeCritical) {
                numbers.Add(new HitNumber(NumberCritical, damage, spYellow, true));
                return;
            }

            if (!IsMonsterLike(targetJob)) {
                numbers.Add(new HitNumber(NumberNormal, damage, isSp ? NumberSpBlue : NumberRed, false));
                return;
            }

            switch (subtype) {
                case SubtypeMultiHit:
                    numbers.Add(new HitNumber(NumberMultiHitPart, damage, spWhite, false));
                    numbers.Add(new HitNumber(NumberComboTotal, runningTotal, spYellow, false));
                    return;
                case SubtypeMultiHitCritical:
                    numbers.Add(new HitNumber(NumberCritical, damage, NumberYellow, true));
                    numbers.Add(new HitNumber(NumberComboTotal, runningTotal, spYellow, false));
                    return;
                case SubtypeKick:
                    numbers.Add(new HitNumber(NumberComboTotal, runningTotal, spWhite, false, comboBurst: true));
                    return;
                case SubtypeKickFollow:
                    numbers.Add(new HitNumber(NumberComboTotal, runningTotal, spWhite, false));
                    return;
                default:
                    numbers.Add(new HitNumber(NumberNormal, damage, spWhite, false));
                    return;
            }
        }

        /// <summary>The local player's statuses that turn a monster's miss into the guard effect: Kyrie Eleison, Parrying.</summary>
        public const int EfstKyrieEleison = 19, EfstParrying = 104;
        /// <summary>EF_GUARD, launched on the local player instead of MISS (actor msg 0x7e).</summary>
        public const int GuardEffectId = 336;

        /// <summary>
        /// A miss (damage 0, 0xbda65e) shows EF_GUARD on the target instead of MISS over the attacker when the attacker is
        /// monster-like and the target is the local player under Kyrie Eleison or Parrying.
        /// </summary>
        public static bool MissShowsGuard(int attackerJob, bool targetIsLocalPlayer, bool localPlayerHasGuardStatus) =>
            targetIsLocalPlayer && localPlayerHasGuardStatus && IsMonsterLike(attackerJob);

        /// <summary>The MISS shown over the attacker: white for a monster-like attacker, red for a player.</summary>
        public static uint MissArgb(int attackerJob) => IsMonsterLike(attackerJob) ? NumberWhite : NumberRed;

        // ---- hit sound (§4) ---------------------------------------------------------------------------------------

        /// <summary>
        /// The wav (under data\wav\) a landed hit plays first: none for lucky, miss and self-hits; a player target plays its
        /// job's armour sound (the attacker is ignored); otherwise the attacker's weapon class (−1 for monsters and unknown
        /// attackers). <paramref name="rand"/> (the CRT rand()) is drawn only where the client calls it.
        /// </summary>
        public static string HitSound(int subtype, int damage, bool selfHit, int targetDisplayJob, int attackerWeaponClass,
            int localJob, Func<int> rand) {
            if (subtype == SubtypeLucky || damage == 0 || selfHit) return null;
            if (subtype == SubtypeSkillSplash || subtype == SubtypeSkillSingle) return WeaponHitWav(-1, localJob, rand);
            if (IsPlayerJob(targetDisplayJob)) return PlayerJobHitWav(targetDisplayJob);
            return WeaponHitWav(attackerWeaponClass, localJob, rand);
        }

        /// <summary>FUN_00cefd20: the hit wav of a weapon class (−1 = monster / unknown attacker).</summary>
        public static string WeaponHitWav(int weaponClass, int localJob, Func<int> rand) {
            if (weaponClass == -1) return "_enemy_hit_normal" + (Rand(rand) % 4 + 1) + ".wav";
            if ((uint) weaponClass >= 105) return null;
            if (weaponClass == 0 && localJob != 4046 && localJob != 4225) return "_hit_fist" + (Rand(rand) % 4 + 1) + ".wav";
            return WeaponClassWav(weaponClass);
        }

        static int Rand(Func<int> rand) => rand != null ? rand() & 0x7fff : 0;

        /// <summary>Lua ReqWeaponHitWaveName by weapon class (Custom weapontable), classes 0..104.</summary>
        static string WeaponClassWav(int c) {
            if (c == 1 || c == 2 || c == 3 || (c >= 31 && c <= 51)) return "_hit_sword.wav";
            if (c == 4 || c == 5 || (c >= 52 && c <= 57)) return "_hit_spear.wav";
            if (c == 6 || c == 7 || (c >= 58 && c <= 61)) return "_hit_axe.wav";
            if (c == 10 || c == 23 || (c >= 69 && c <= 72) || c == 96 || c == 97 || (c >= 99 && c <= 102)) return "_hit_rod.wav";
            if (UsesArrow(c)) return "_hit_arrow.wav";
            switch (c) {
                case 17: return "_hit_권총.wav";
                case 18: return "_hit_라이플.wav";
                case 19: return "_hit_개틀링한발.wav";
                case 20: return "_hit_샷건.wav";
                case 21: return "_hit_그레네이드런쳐.wav";
                default: return "_hit_mace.wav";   // maces, the Taekwon line's fists and the Lua default
            }
        }

        /// <summary>FUN_00ceaa50 / FUN_00cf1fe0: a player target's armour sound by job.</summary>
        public static string PlayerJobHitWav(int job) {
            switch (job) {
                case 1: case 7: case 14: case 15:
                case 4002: case 4008: case 4015: case 4016: case 4024: case 4030: case 4037: case 4038: case 4047: case 4048:
                case 4051: case 4054: case 4060: case 4066: case 4070: case 4073: case 4077: case 4226: case 4238: case 4239:
                case 4241: case 4252: case 4255: case 4258: case 4262: case 4280: case 4281: case 4302: case 4316:
                    return "player_metal.wav";
                case 3: case 6: case 11: case 12: case 17: case 19: case 20: case 24: case 25:
                case 4004: case 4007: case 4012: case 4013: case 4018: case 4020: case 4021: case 4026: case 4029: case 4034:
                case 4035: case 4040: case 4042: case 4043: case 4046: case 4050: case 4056: case 4059: case 4062: case 4065:
                case 4068: case 4069: case 4072: case 4075: case 4076: case 4079: case 4211: case 4212: case 4222: case 4223:
                case 4224: case 4225: case 4228: case 4229: case 4254: case 4257: case 4260: case 4263: case 4264: case 4304:
                case 4305: case 4306:
                    return "player_wooden_male.wav";
                default:
                    return "player_clothes.wav";
            }
        }
    }
}
