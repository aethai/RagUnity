using System.Collections.Generic;
using Ragnarok.Network;
using ROIO.Models.FileTypes;
using R = Ragnarok.Combat.RoOriginalCombatRules;

namespace Ragnarok.Combat {
    /// <summary>What the hit presenter needs to know about an actor at a given moment.</summary>
    public struct RoHitActor {
        public int Job;
        public int Sex;
        /// <summary>The right-hand weapon class (players; −1 when unknown).</summary>
        public int WeaponClass;
        public bool IsPlayer;
        /// <summary><see cref="R.StateIdle"/>, walk, attack (motion 2), dead or hurt, as the client's state machine.</summary>
        public int State;
        /// <summary>Fake death: SetAction(4) refuses (players).</summary>
        public bool PlayingDead;
        /// <summary>The client's name of the actor (its name cache; the battle log's names), null when unknown.</summary>
        public string Name;
    }

    /// <summary>The actors, assets and sinks the hit presenter works through (the world controller).</summary>
    public interface IRoHitContext {
        uint OwnActorId { get; }
        int OwnJob { get; }
        /// <summary>The local player has Kyrie Eleison or Parrying (EFST 19 / 104).</summary>
        bool OwnHasGuardStatus { get; }
        bool TryGetHitActor(uint actorId, out RoHitActor actor);
        /// <summary>The actor's body ACT, null when its sprite is not loaded (the rules then use the defaults).</summary>
        ACT GetBodyAct(uint actorId);
        void PlayHitSound(uint targetId, string wav);
        /// <summary>The landed hit's effects on the target: EF_HIT2 (EF 1) first on a critical, then effect2 (−1 none).</summary>
        void LaunchHitEffects(uint targetId, uint attackerId, bool critical, int effect2);
        /// <summary>A number over an actor (CMsgEffect type, value, ARGB; a critical one with its burst behind it).</summary>
        void ShowNumber(uint actorId, in RoOriginalCombatRules.HitNumber number);
        /// <summary>MISS over an actor (the attacker).</summary>
        void ShowMiss(uint actorId, uint argb);
        /// <summary>"LUCKY!!" over an actor (the target).</summary>
        void ShowLucky(uint actorId);
        /// <summary>EF_GUARD (EF 336) on the local player: a monster's miss under Kyrie Eleison / Parrying.</summary>
        void ShowGuard(uint actorId);
        /// <summary>The damage motion from now on for <paramref name="lengthMs"/>: the walk stops where the actor is.</summary>
        void ApplyFlinch(uint targetId, float lengthMs);
        /// <summary>
        /// A basic-attack arrow (CArrowEffect) from the attacker toward the target, created now and released after
        /// <paramref name="releaseTicks"/> (24 ms ticks); <paramref name="spriteClass"/> 0 for players, the monster's job.
        /// </summary>
        void SpawnArrow(uint attackerId, uint targetId, float releaseTicks, int spriteClass);
    }

    /// <summary>
    /// The original client's queued hits (reference packages/combat-timing/NOTES.md): at ZC_NOTIFY_ACT receipt the
    /// attacker's hits are queued at receipt + hit delay (+200 ms per further hit, dual wield +100 / +350), at
    /// ZC_NOTIFY_SKILL receipt the skill's hits (<see cref="R.PlanSkillHits"/>); at each due time the hit handler runs on
    /// the target in the client's order — lucky / miss, hit sound, effects, numbers, flinch. One binary-heap timeline on
    /// the presentation clock (receipt time, like timeGetTime); the packet's server tick is not used for timing.
    /// </summary>
    public sealed class RoHitPresenter {
        struct QueuedHit {
            public uint Target;
            public uint Attacker;
            public int Damage;
            public int Subtype;
            public int RunningTotal;
            public int AttackedMT;
            public bool IsSp;
            public bool SelfHit;
            public int AttackerWeaponClass;
            /// <summary>A skill hit: its effect1 / effect2 come from the skill presenter, on the same plan.</summary>
            public bool SkillHit;
            /// <summary>The attacker's name when the hit was queued (the client's name cache outlives the actor).</summary>
            public string AttackerName;
        }

        /// <summary>
        /// FUN_00bd58c0's battle-log step: every queued hit with damage &gt; 0, at its due time and before the hit handler,
        /// with (target, attacker, the attacker's name, damage). The receiver decides which line, if any (MsgStr 0x645..0x648).
        /// </summary>
        public System.Action<uint, uint, string, int> BattleLog { get; set; }

        /// <summary>The hurt motion of bodies whose ACT is not loaded: 3 frames at delay 4 (the player bodies).</summary>
        const int DefaultHurtFrames = 3;
        const float DefaultActDelay = 4f;
        /// <summary>A monster's attack hit frame when its ACT is not loaded.</summary>
        const int DefaultMonsterHitFrame = 6;

        readonly RoTimeline<QueuedHit> hits = new RoTimeline<QueuedHit>();
        readonly List<R.PlannedHit> plan = new List<R.PlannedHit>(4);
        readonly List<R.HitNumber> numbers = new List<R.HitNumber>(2);
        readonly List<int> frameEvents = new List<int>(32);
        readonly System.Random random = new System.Random();

        public int PendingCount => hits.Count;

        int Rand() => random.Next(0x8000);

        /// <summary>
        /// FUN_00c54e80 → FUN_00bebd90 for a ZC_NOTIFY_ACT attack: queues its hits. Ignored damage, an unknown attacker or an
        /// unknown target queue nothing. <paramref name="now"/> is the receipt time in seconds.
        /// </summary>
        public void OnAttack(in RoZcNotifyAct act, float now, IRoHitContext context) {
            int action = act.ActionRaw;
            if (!R.IsAttackAction(action) || act.Damage == R.IgnoredActDamage ||
                !context.TryGetHitActor(act.SourceId, out RoHitActor attacker) ||
                act.TargetId == 0 || !context.TryGetHitActor(act.TargetId, out _)) {
                return;
            }

            int attackMT = R.EffectiveAttackMotion(act.SourceMotionTime);
            bool selfHit = act.TargetId == act.SourceId;
            bool player = attacker.IsPlayer && R.IsPlayerJob(attacker.Job);
            bool motion9 = player && UsesAttackMotion9(attacker, act.ServerTick);
            bool arrow = player && R.UsesArrow(attacker.WeaponClass);
            float factor = R.MotionSpeedFactor(attackMT, arrow);

            ACT body = context.GetBodyAct(act.SourceId);
            int attackAction = player ? (motion9 ? 11 : 10) * 8 : 2 * 8;
            ACT.Action attackActAction = body != null && body.TryGetAction(attackAction, out ACT.Action a) ? a : null;
            float fd = R.FrameDelay(attackActAction != null ? attackActAction.delay / R.ActDelayUnitMs : DefaultActDelay, factor);
            float hitFrame = player
                ? R.PlayerHitFrame(attacker.Job, attacker.Sex, attacker.WeaponClass, motion9)
                : attackActAction != null ? ActHitFrame(body, attackActAction) : DefaultMonsterHitFrame;
            if (arrow) {
                // created with the attack motion, released at the hit frame; the hit lands 8 ticks later (CPc::SetAction
                // 0xcd0d69 / 0xcd0d6e)
                context.SpawnArrow(act.SourceId, act.TargetId, fd * hitFrame, 0);
                hitFrame = R.AddArrowFlight(hitFrame, fd);
            } else if (!player && R.MonsterShootsArrow(attacker.Job)) {
                context.SpawnArrow(act.SourceId, act.TargetId, fd * hitFrame, attacker.Job);   // the hit is not delayed
            }

            float hitDelay = (R.HitDelayMs(hitFrame, fd) + R.HitDelayExtraMs(attacker.Job)) / 1000f;
            R.PlanHits(action, act.Damage, act.HitCount, act.Damage2, attacker.Job, plan);
            int attackedMT = R.EffectiveAttackedMotion(act.TargetMotionTime, action);
            for (int i = 0; i < plan.Count; i++) {
                R.PlannedHit h = plan[i];
                hits.Add(now + hitDelay + h.OffsetMs / 1000f, new QueuedHit {
                    Target = act.TargetId,
                    Attacker = act.SourceId,
                    Damage = h.Damage,
                    Subtype = h.Subtype,
                    RunningTotal = h.RunningTotal,
                    AttackedMT = attackedMT,
                    IsSp = act.IsSpDamage,
                    SelfHit = selfHit,
                    AttackerWeaponClass = player ? attacker.WeaponClass : -1,
                    AttackerName = attacker.Name
                });
            }
        }

        /// <summary>
        /// ZC_NOTIFY_SKILL / _POSITION: the skill's hits on the target (FUN_00bede70, or CSkill::SendMsg case 1 when
        /// <paramref name="fromUnit"/>), queued from <paramref name="now"/> (the receipt time) with the packet's attackMT
        /// (<see cref="RoSkillEvent.DelayMilliseconds"/>) and attackedMT. An unknown target queues nothing.
        /// </summary>
        public void OnSkillDamage(in RoSkillEvent skill, int attackedMT, bool fromUnit, float now, IRoHitContext context) {
            uint targetId = skill.TargetActorId;
            if (targetId == 0 || !context.TryGetHitActor(targetId, out _)) {
                return;
            }

            if (fromUnit) {
                R.PlanUnitSkillHits(skill.Damage, skill.HitCount, plan);
            } else {
                R.PlanSkillHits(skill.SkillId, skill.ActionRaw, skill.Damage, skill.HitCount, (int) skill.DelayMilliseconds, plan);
            }

            bool attackerKnown = context.TryGetHitActor(skill.SourceActorId, out RoHitActor attacker);
            int weaponClass = !fromUnit && attackerKnown && attacker.IsPlayer && R.IsPlayerJob(attacker.Job) ? attacker.WeaponClass : -1;
            for (int i = 0; i < plan.Count; i++) {
                R.PlannedHit h = plan[i];
                hits.Add(now + h.OffsetMs / 1000f, new QueuedHit {
                    Target = targetId,
                    Attacker = skill.SourceActorId,
                    Damage = h.Damage,
                    Subtype = h.Subtype,
                    RunningTotal = h.RunningTotal,
                    AttackedMT = attackedMT,
                    AttackerWeaponClass = weaponClass,
                    SkillHit = true,
                    AttackerName = attackerKnown ? attacker.Name : null
                });
            }
        }

        /// <summary>
        /// The attack motion the attacker plays (§6): motion 9 or 2 from the job / weapon table. The Taekwon line's random
        /// pick draws from the packet's server tick, so the world state's visible motion and the hit frame agree.
        /// </summary>
        public static bool UsesAttackMotion9(in RoHitActor attacker, uint serverTick) =>
            R.UsesAttackMotion9(attacker.Job, attacker.Sex, attacker.WeaponClass, () => (int) ((serverTick * 1103515245u + 12345u) >> 16) & 0x7fff);

        int ActHitFrame(ACT body, ACT.Action action) {
            frameEvents.Clear();
            if (action.frames != null) {
                for (int i = 0; i < action.frames.Length; i++) {
                    frameEvents.Add(action.frames[i] != null ? action.frames[i].soundId : -1);
                }
            }

            return R.ActHitFrame(frameEvents, body.sounds);
        }

        /// <summary>Runs every hit due at <paramref name="now"/>, in due order. Returns how many ran.</summary>
        public int ProcessDue(float now, IRoHitContext context) {
            int processed = 0;
            while (hits.TryTakeDue(now, out QueuedHit hit)) {
                if (hit.Damage > 0) BattleLog?.Invoke(hit.Target, hit.Attacker, hit.AttackerName, hit.Damage);
                RunHit(hit, context);
                processed++;
            }

            return processed;
        }

        /// <summary>Runs every queued hit now (map change, tests).</summary>
        public int Flush(IRoHitContext context) => ProcessDue(float.PositiveInfinity, context);

        public void Clear() => hits.Clear();

        /// <summary>The hit handler (0xbd9af6) on the target at the hit's due time.</summary>
        void RunHit(in QueuedHit hit, IRoHitContext context) {
            if (!context.TryGetHitActor(hit.Target, out RoHitActor target)) {
                return;
            }

            if (hit.Subtype == R.SubtypeLucky) {
                context.ShowLucky(hit.Target);
                return;
            }

            if (hit.Damage == 0) {
                // MISS over the attacker (msg 0x59), or the guard on the local player (msg 0x7e); an attacker that is gone
                // shows nothing
                if (!context.TryGetHitActor(hit.Attacker, out RoHitActor attacker)) {
                    return;
                }

                if (R.MissShowsGuard(attacker.Job, hit.Target == context.OwnActorId, context.OwnHasGuardStatus)) {
                    context.ShowGuard(hit.Target);
                } else {
                    context.ShowMiss(hit.Attacker, R.MissArgb(attacker.Job));
                }

                return;
            }

            if (hit.Damage < 0) {
                return;   // the client's heal display bug: nothing visible, no flinch
            }

            string wav = R.HitSound(hit.Subtype, hit.Damage, hit.SelfHit, target.Job, hit.AttackerWeaponClass, context.OwnJob, Rand);
            if (wav != null) {
                context.PlayHitSound(hit.Target, wav);
            }

            if (!hit.SkillHit) {
                bool critical = hit.Subtype == R.SubtypeCritical || hit.Subtype == R.SubtypeMultiHitCritical;
                int effect2 = R.BasicHitEffect(AttackerJob(hit, context), hit.SelfHit);
                context.LaunchHitEffects(hit.Target, hit.Attacker, critical, effect2);
            }

            if (hit.Damage != R.NoNumberDamage) {
                R.HitNumbers(hit.Subtype, hit.Damage, hit.RunningTotal, hit.IsSp, target.Job, numbers);
                for (int i = 0; i < numbers.Count; i++) {
                    context.ShowNumber(hit.Target, numbers[i]);
                }
            }

            Flinch(hit, target, context);
        }

        /// <summary>The end of the hit handler: SetAction(4) unless the rules or a refusal say no.</summary>
        void Flinch(in QueuedHit hit, in RoHitActor target, IRoHitContext context) {
            float dmotion = R.DamageMotion(hit.AttackedMT);
            if (!target.PlayingDead && R.Flinches(hit.Subtype, false, target.State, target.Job, dmotion)) {
                context.ApplyFlinch(hit.Target, HurtLengthMs(hit.Target, target, dmotion, context));
            }
        }

        static int AttackerJob(in QueuedHit hit, IRoHitContext context) =>
            context.TryGetHitActor(hit.Attacker, out RoHitActor attacker) ? attacker.Job : -1;

        /// <summary>The damage motion's length (§2.3): players exactly attackedMT ms on their 3-frame hurt action, others
        /// from their hurt action's frames and delay (ACT action 6 / 3).</summary>
        float HurtLengthMs(uint targetId, in RoHitActor target, float dmotion, IRoHitContext context) {
            bool player = target.IsPlayer && R.IsPlayerJob(target.Job);
            ACT body = context.GetBodyAct(targetId);
            int hurtAction = (player ? 6 : 3) * 8;
            int frames = DefaultHurtFrames;
            float delay = DefaultActDelay;
            if (body != null && body.TryGetAction(hurtAction, out ACT.Action a) && a.frames != null && a.frames.Length > 0) {
                frames = a.frames.Length;
                delay = a.delay / R.ActDelayUnitMs;
            }

            (float fd, float hold) = R.HurtMotion(R.FrameDelay(delay, 1f), dmotion, target.Job);
            return R.MotionLengthMs(frames, fd, hold);
        }
    }
}
