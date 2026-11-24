using System;
using System.Collections.Generic;
using Ragnarok.Effects;
using Ragnarok.Network;
using UnityEngine;
using R = Ragnarok.Combat.RoOriginalCombatRules;

namespace Ragnarok.Combat {
    /// <summary>
    /// Presents skill packets the way the original client does, from its own skill → effect table
    /// (<see cref="RoOriginalSkillEffectTable"/>, extracted from game.exe, rules in re-custom
    /// packages/skill-effects/UNITY_FORMAT.md): for every packet it launches exactly the ids the original launches, on
    /// the same actor or cell, at the same moment, in the ported original effect engine, and shows the heal numbers. A
    /// skill the table lacks gets the client's default (<see cref="RoOriginalSkillEffects.Default"/>). Damage numbers,
    /// hit sounds and flinches are the hit presenter's, on the same hit plan; cast bars, skill-name bubbles, motions and
    /// status visuals are presented elsewhere.
    /// </summary>
    public sealed class RoSkillPresenter {
        const float TickSeconds = 0.016f;
        /// <summary>UNT_USED_TRAPS: a trap unit's look when it fires (ZC_SPRITE_CHANGE, LOOK_BASE).</summary>
        const int UsedTrapLook = 0x8c;

        readonly Func<RoOriginalSkillEffectTable> tableProvider;
        readonly RoTimeline<PendingLaunch> pendingLaunches = new RoTimeline<PendingLaunch>();
        readonly RoTimeline<uint> pendingEnds = new RoTimeline<uint>();
        readonly Dictionary<uint, UnitState> units = new Dictionary<uint, UnitState>();
        readonly HashSet<uint> knownUnitIds = new HashSet<uint>();
        readonly Dictionary<uint, uint> lockOnByCaster = new Dictionary<uint, uint>();
        /// <summary>The caster's begin-cast effect (the client's actor +0x14c), for ZC_DISPEL.</summary>
        readonly Dictionary<uint, uint> beginCastByCaster = new Dictionary<uint, uint>();

        struct PendingLaunch {
            public int EffectId;
            public ushort SkillId;
            public uint OnActor;       // 0 = at Cell
            public uint Caster;
            public uint AimActor;
            public Vector3 Cell;
            public int Count;
        }

        sealed class UnitState {
            public RoOriginalSkillUnit Unit;
            /// <summary>The unit's classic-engine owner (the client's CSkill): every effect of the unit runs on it.</summary>
            public uint Owner;
            public Vector3 Cell;
            public uint Creator;
            public int Level;
            public readonly List<uint> Kept = new List<uint>();
            public float[] NextRelaunch;
            /// <summary>Per periodic entry: the kept instance of a persistent one (CSkill +0x418).</summary>
            public uint[] PersistentHandles;
        }

        public RoSkillPresenter(Func<RoOriginalSkillEffectTable> tableProvider = null) {
            this.tableProvider = tableProvider ?? (() => RoOriginalSkillEffectTable.Default);
        }

        RoOriginalSkillEffectTable Table => tableProvider() ?? RoOriginalSkillEffectTable.Empty;

        readonly List<R.PlannedHit> plan = new List<R.PlannedHit>(4);

        public int PendingCueCount => pendingLaunches.Count;

        /// <summary>A skill unit this presenter has seen (its ZC_SKILL_DISAPPEAR and look changes must reach it).</summary>
        public bool IsTrackingUnit(uint unitId) => units.ContainsKey(unitId) || knownUnitIds.Contains(unitId);

        /// <summary>
        /// Whether a skill packet's source is a skill unit (the client finds a CSkill, not an actor, FUN_009f8c80): a unit
        /// this presenter has seen, or a skill whose damage comes from its units when the source is no actor.
        /// </summary>
        public bool IsUnitSource(uint sourceId, ushort skillId, IRoCombatPresentationContext context) =>
            knownUnitIds.Contains(sourceId) ||
            (Table.GetOrDefault(skillId).DamageFromUnit && !context.TryResolveActorFeedbackAnchor(sourceId, out _, out _));

        /// <summary>The original's effects for a skill: its table entry, or the client's default.</summary>
        public RoOriginalSkillEffects Resolve(ushort skillId) => Table.GetOrDefault(skillId);

        public void Present(in RoSkillEvent skillEvent, IRoCombatPresentationContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            RoEffectManager manager = context.ResolveEffectManager();
            if (manager == null) {
                return;
            }

            switch (skillEvent.Kind) {
                case RoSkillEventKind.Cast:
                    HandleCast(in skillEvent, context, manager);
                    break;
                case RoSkillEventKind.Damage:
                case RoSkillEventKind.Position:
                    EndLockOn(skillEvent.SourceActorId, manager);
                    HandleDamage(in skillEvent, context, manager);
                    break;
                case RoSkillEventKind.Result:
                    EndLockOn(skillEvent.SourceActorId, manager);
                    HandleResult(in skillEvent, context, manager);
                    break;
                case RoSkillEventKind.Ground:
                    EndLockOn(skillEvent.SourceActorId, manager);
                    HandleGround(in skillEvent, context, manager);
                    break;
                case RoSkillEventKind.Entry:
                    HandleEntry(in skillEvent, context, manager);
                    break;
                case RoSkillEventKind.Disappear:
                    HandleDisappear(skillEvent.UnitId, manager);
                    break;
            }
        }

        // ZC_USESKILL_ACK with a cast time: the begin-cast circle (by element) on the caster, the lock-on on the target,
        // both for delaytime / 16 ticks (actor msgs 0x55 / 0x56 → vf18); an instant cast launches nothing.
        void HandleCast(in RoSkillEvent ev, IRoCombatPresentationContext context, RoEffectManager manager) {
            uint caster = ev.SourceActorId;
            EndLockOn(caster, manager);
            if (ev.DelayMilliseconds == 0) {
                return;
            }

            RoOriginalSkillEffects skill = Table.GetOrDefault(ev.SkillId);

            int durationTicks = Mathf.Max(1, (int) ev.DelayMilliseconds / 16);
            uint target = ev.TargetActorId;
            if (!skill.HideCastAura) {
                int circle = skill.ResolveBeginCast((int) ev.Element);
                if (circle > 0 &&
                    LaunchOnActor(manager, context, circle, caster, caster, target, 1, durationTicks, out uint aura) && aura != 0) {
                    beginCastByCaster[caster] = aura;
                }
            }

            if (target != 0 && target != caster) {
                foreach (int id in skill.BeginCastOnTarget) {
                    if (LaunchOnActor(manager, context, id, target, caster, caster, 1, durationTicks, out uint handle) && handle != 0) {
                        lockOnByCaster[caster] = handle;
                    }
                }
            } else if (target == 0) {
                // a ground-target cast: the client's CGroundScope (FUN_00c7f900) — the rotating magic circle over the
                // target area, n x n cells by skill (2 x 2 by default), for the whole cast time
                manager.TryLaunchGroundScope(ev.X, ev.Y, ev.SkillId, (int) ev.DelayMilliseconds, caster);
            }
        }

        /// <summary>
        /// ZC_DISPEL: the cast was interrupted. The caster's begin-cast effect (actor msg 0x54) and its lock-on on the
        /// target (0x57) get effect msg 0x18; the ground-target circle is not touched (nothing ends it early).
        /// </summary>
        public void HandleCastCancel(uint caster, IRoCombatPresentationContext context) {
            if (caster == 0 || context == null) {
                return;
            }

            RoEffectManager manager = context.ResolveEffectManager();
            if (manager == null) {
                return;
            }

            if (beginCastByCaster.TryGetValue(caster, out uint aura)) {
                manager.EndClassicEffect(aura);
                beginCastByCaster.Remove(caster);
            }

            EndLockOn(caster, manager);
        }

        /// <summary>The lock-on goes when the caster's cast state ends (actor msg 0x57 → effect msg 0x18).</summary>
        void EndLockOn(uint caster, RoEffectManager manager) {
            if (caster != 0 && lockOnByCaster.TryGetValue(caster, out uint handle)) {
                manager.EndClassicEffect(handle);
                lockOnByCaster.Remove(caster);
            }
        }

        // ZC_NOTIFY_SKILL / _POSITION (skill damage handler 0x00bede70): on receipt the caster effect and the
        // projectile / target effect, each ONCE with the packet's count in +0x174 (msg 0x2c; the effect spreads its
        // own sub-bolts); then every queued hit that does damage launches its own effects on the target at its due time
        // (<see cref="HitEffects"/>, on the plan the hit presenter shows the numbers on: R.PlanSkillHits). A skill
        // unit's damage launches the hit effect on the target from receipt, every 200 ms, and nothing on the caster.
        void HandleDamage(in RoSkillEvent ev, IRoCombatPresentationContext context, RoEffectManager manager) {
            RoOriginalSkillEffects skill = Table.GetOrDefault(ev.SkillId);
            float now = context.NowSeconds;
            uint caster = ev.SourceActorId;
            uint target = ev.TargetActorId;
            int type = ev.ActionRaw;
            bool multiHitType = type == 8 || type == 9 || type == 13;
            // the packet count: only multi-hit types use it (clamped: <1 or >36 -> 1); everything else is one hit
            int count = multiHitType && ev.HitCount >= 1 && ev.HitCount <= 36 ? ev.HitCount : 1;
            bool fromUnit = IsUnitSource(caster, ev.SkillId, context);

            if (!fromUnit) {
                // FUN_00d15360 action effect: damage types 4/6/8/9/13 only (5 splash and 14 skip the caster part)
                if (type == 4 || type == 6 || multiHitType) {
                    foreach (int id in skill.OnCaster) {
                        LaunchOnActor(manager, context, id, caster, caster, target, count, 0, out _);
                    }
                }

                foreach (RoOriginalBeforeHitEffect before in skill.BeforeHit) {
                    // Count 0: this launch site sends no msg 0x2c (e.g. Jupitel's FUN_00d1eb10 ball next to the counted
                    // session-map one), so the effect keeps +0x174 = 0
                    int launchCount = before.Count == 0 ? 0 : count;
                    if (before.OnCaster) {
                        LaunchOnActor(manager, context, before.EffectId, caster, caster, before.ToTarget ? target : 0, launchCount, 0, out _);
                    } else if (target != 0) {
                        LaunchOnActor(manager, context, before.EffectId, target, caster, caster, launchCount, 0, out _);
                    }
                }
            }

            if (target == 0) {
                return;
            }

            if (fromUnit) {
                R.PlanUnitSkillHits(ev.Damage, ev.HitCount, plan);
            } else {
                R.PlanSkillHits(ev.SkillId, type, ev.Damage, ev.HitCount, (int) ev.DelayMilliseconds, plan);
            }

            for (int i = 0; i < plan.Count; i++) {
                if (plan[i].Damage <= 0) {
                    continue;   // the hit handler's miss path launches nothing
                }

                float due = now + plan[i].OffsetMs / 1000f;
                foreach (int id in HitEffects(skill, i, fromUnit)) {
                    Schedule(due, id, ev.SkillId, target, caster, caster, default);
                }
            }
        }

        /// <summary>
        /// The effects queued hit <paramref name="hitIndex"/> launches on the target: every queued hit carries its own
        /// effect pair (FUN_00bede70), the first one <see cref="RoOriginalSkillEffects.Hit"/>, the others
        /// <see cref="RoOriginalSkillEffects.HitEachHit"/> (Pierce: none); a skill unit's hits are all alike.
        /// </summary>
        public static int[] HitEffects(RoOriginalSkillEffects skill, int hitIndex, bool fromUnit) =>
            hitIndex == 0 || fromUnit ? skill.Hit : skill.HitEachHit;

        // ZC_USE_SKILL (0x00c9aa00): nothing at all when the result flag is 0; otherwise on the packet's target, the
        // source and the target's cell; Heal-type skills pick the id by the amount, Seven Wind adds base + level.
        void HandleResult(in RoSkillEvent ev, IRoCombatPresentationContext context, RoEffectManager manager) {
            if (!ev.Success) {
                return;
            }

            RoOriginalSkillEffects skill = Table.GetOrDefault(ev.SkillId);

            uint caster = ev.SourceActorId;
            uint target = ev.TargetActorId != 0 ? ev.TargetActorId : caster;
            int amount = ev.Damage;
            // the recast rule: the previous instance of a persistent look goes before the new one is launched
            foreach ((int effectId, bool onTarget) in skill.UseRemove) {
                manager.RemoveOldestClassicEffectById(onTarget ? target : caster, effectId);
            }

            foreach (int id in skill.Use) {
                LaunchOnActor(manager, context, id, target, caster, caster, 1, 0, out _);
            }

            if (skill.TryResolveUseByAmount(amount, out int byAmount)) {
                // 0xca82e3: the effect by amount, then the green heal number (msg 0x58, type 0x72) over the target
                LaunchOnActor(manager, context, byAmount, target, caster, caster, 1, 0, out _);
                context.ShowNumber(target, amount, R.NumberHealGreen, R.NumberHeal);
            }

            if (skill.UseLevelBase > 0 && amount > 0) {
                // level-dependent choices use the level clamped to 1..10
                LaunchOnActor(manager, context, skill.UseLevelBase + Mathf.Clamp(amount, 1, 10), target, caster, caster, 1, 0, out _);
            }

            foreach (int id in skill.UseOnCaster) {
                LaunchOnActor(manager, context, id, caster, caster, target, 1, 0, out _);
            }

            if (skill.UsePosition.Length > 0 && context.TryResolveActorFeedbackAnchor(target, out _, out Vector3 cell)) {
                foreach (int id in skill.UsePosition) {
                    LaunchAtCell(manager, context, id, cell, caster, 1, out _);
                }
            }

            if (skill.UseOnlyIfCasterIsLocalPlayer.Length > 0 &&
                context is IRoCombatActorPresentationContext actors && actors.IsOwnActor(caster)) {
                foreach (int id in skill.UseOnlyIfCasterIsLocalPlayer) {
                    LaunchOnActor(manager, context, id, target, caster, caster, 1, 0, out _);
                }
            }
        }

        // ZC_NOTIFY_GROUNDSKILL: at the target cell through a CDummyEffectLauncher, whose death (a constant lifetime or
        // defaultDuration + k, in 24 ms units) ends the effect - also one that lengthened its own duration (EF_ICEWALL).
        void HandleGround(in RoSkillEvent ev, IRoCombatPresentationContext context, RoEffectManager manager) {
            RoOriginalSkillEffects skill = Table.GetOrDefault(ev.SkillId);
            if (skill.Ground.Length == 0 ||
                !context.TryResolveCellFeedbackPosition(ev.X, ev.Y, out Vector3 cell)) {
                return;
            }

            foreach (int id in skill.Ground) {
                if (LaunchAtCell(manager, context, id, cell, ev.SourceActorId, Mathf.Max(1, (int) ev.Level), out uint handle) &&
                    handle != 0 && skill.GroundLifetimeUnits24.TryGetValue(id, out int units24)) {
                    pendingEnds.Add(context.NowSeconds + units24 * 0.024f, handle);
                }
            }
        }

        // ZC_SKILL_ENTRY: the unit's creation effects (kept until it disappears) and its periodic effects, relaunched
        // every DefaultDuration(ef) - k ticks while it lives (CSkill::OnProcess 0x00d3ac30).
        void HandleEntry(in RoSkillEvent ev, IRoCombatPresentationContext context, RoEffectManager manager) {
            uint unitId = ev.UnitId;
            knownUnitIds.Add(unitId);
            if (units.TryGetValue(unitId, out UnitState previous)) {
                EndUnit(previous, manager);
                units.Remove(unitId);
            }

            if (!Table.TryGetUnit(ev.SkillId, out RoOriginalSkillUnit unit) ||
                !context.TryResolveCellFeedbackPosition(ev.X, ev.Y, out Vector3 cell)) {
                return;
            }

            var state = new UnitState {
                Unit = unit,
                Owner = manager.CreateClassicUnitOwner(cell),
                Cell = cell,
                Creator = ev.SourceActorId,
                Level = Mathf.Max(1, (int) ev.Level),
                NextRelaunch = new float[unit.Periodic.Length],
            };

            foreach (int id in unit.OnCreate) {
                if (LaunchAtCell(manager, context, id, cell, state.Creator, state.Level, out uint handle, state.Owner) && handle != 0) {
                    state.Kept.Add(handle);
                }
            }

            float now = context.NowSeconds;
            for (int i = 0; i < unit.Periodic.Length; i++) {
                if (unit.Periodic[i].Persistent) {
                    LaunchPersistent(manager, context, state, i);
                } else {
                    LaunchAtCell(manager, context, unit.Periodic[i].EffectId, cell, state.Creator, state.Level, out _, state.Owner);
                }

                state.NextRelaunch[i] = now + RelaunchSeconds(unit.Periodic[i]);
            }

            units[unitId] = state;
        }

        /// <summary>
        /// A persistent unit effect (Ice Wall, Fog Wall, songs, ...): CSkill::OnProcess launches it only while +0x418 is
        /// empty and keeps it there; the unit's destructor kills it (msg 0x18). Relaunching it on the periodic counter
        /// stacked copies nobody ended: the walls and the fog stayed after their units were gone.
        /// </summary>
        static void LaunchPersistent(RoEffectManager manager, IRoCombatPresentationContext context, UnitState state, int index) {
            state.PersistentHandles ??= new uint[state.Unit.Periodic.Length];
            uint kept = state.PersistentHandles[index];
            if (kept != 0 && manager.IsClassicEffectAlive(kept)) {
                return;
            }

            if (kept != 0) state.Kept.Remove(kept);
            state.PersistentHandles[index] = 0;
            if (LaunchAtCell(manager, context, state.Unit.Periodic[index].EffectId, state.Cell, state.Creator, state.Level, out uint handle, state.Owner) &&
                handle != 0) {
                state.PersistentHandles[index] = handle;
                state.Kept.Add(handle);
            }
        }

        static float RelaunchSeconds(RoOriginalPeriodicEffect periodic) {
            int ticks = Ragnarok.Effects.Classic.ClassicEffectTables.DefaultDuration(periodic.EffectId) - periodic.K;
            return Mathf.Max(1, ticks) * TickSeconds;
        }

        /// <summary>ZC_SPRITE_CHANGE on a skill unit: a trap that fires turns into UNT_USED_TRAPS (0x8c) and launches its
        /// trigger effects on the unit's cell.</summary>
        public void HandleUnitLookChange(uint unitId, uint look, IRoCombatPresentationContext context) {
            if (look != UsedTrapLook || context == null || !units.TryGetValue(unitId, out UnitState state)) {
                return;
            }

            RoEffectManager manager = context.ResolveEffectManager();
            if (manager == null) {
                return;
            }

            foreach (int id in state.Unit.TrapTriggered) {
                LaunchAtCell(manager, context, id, state.Cell, state.Creator, state.Level, out _);
            }
        }

        void HandleDisappear(uint unitId, RoEffectManager manager) {
            if (units.TryGetValue(unitId, out UnitState state)) {
                EndUnit(state, manager);
                units.Remove(unitId);
            }

            knownUnitIds.Remove(unitId);
        }

        /// <summary>CSkill's destructor (~CAbleToMakeEffect 0xbcdc60): every effect launched on the unit gets msg 0x18
        /// and dies on its next pass; nothing is relaunched any more.</summary>
        static void EndUnit(UnitState state, RoEffectManager manager) {
            manager.ReleaseClassicUnitOwner(state.Owner);
            foreach (uint handle in state.Kept) {
                manager.EndClassicEffect(handle);
            }

            state.Kept.Clear();
        }

        void Schedule(float due, int effectId, ushort skillId, uint onActor, uint caster, uint aimActor, Vector3 cell) {
            pendingLaunches.Add(due, new PendingLaunch {
                EffectId = effectId, SkillId = skillId, OnActor = onActor, Caster = caster, AimActor = aimActor, Cell = cell, Count = 1,
            });
        }

        public void ProcessDueCues(float nowSeconds, IRoCombatPresentationContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            if (pendingLaunches.Count == 0 && pendingEnds.Count == 0 && units.Count == 0) {
                return;
            }

            RoEffectManager manager = context.ResolveEffectManager();
            if (manager == null) {
                return;
            }

            while (pendingLaunches.TryTakeDue(nowSeconds, out PendingLaunch p)) {
                Launch(manager, context, in p);
            }

            while (pendingEnds.TryTakeDue(nowSeconds, out uint handle)) {
                manager.EndClassicEffect(handle);
            }

            foreach (UnitState state in units.Values) {
                for (int i = 0; i < state.Unit.Periodic.Length; i++) {
                    if (state.NextRelaunch[i] > nowSeconds) continue;
                    RoOriginalPeriodicEffect periodic = state.Unit.Periodic[i];
                    if (periodic.Persistent) {
                        LaunchPersistent(manager, context, state, i);
                    } else {
                        LaunchAtCell(manager, context, periodic.EffectId, state.Cell, state.Creator, state.Level, out _, state.Owner);
                    }

                    state.NextRelaunch[i] = nowSeconds + RelaunchSeconds(periodic);
                }
            }
        }

        public void FlushPendingCues(IRoCombatPresentationContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            RoEffectManager manager = context.ResolveEffectManager();
            while (pendingLaunches.TryTakeDue(float.PositiveInfinity, out PendingLaunch p)) {
                if (manager != null) Launch(manager, context, in p);
            }
        }

        static void Launch(RoEffectManager manager, IRoCombatPresentationContext context, in PendingLaunch p) {
            if (p.OnActor != 0) {
                LaunchOnActor(manager, context, p.EffectId, p.OnActor, p.Caster, p.AimActor, p.Count, 0, out _);
            } else {
                LaunchAtCell(manager, context, p.EffectId, p.Cell, p.Caster, p.Count, out _);
            }
        }

        public void Clear() {
            pendingLaunches.Clear();
            pendingEnds.Clear();
            units.Clear();
            knownUnitIds.Clear();
            lockOnByCaster.Clear();
            beginCastByCaster.Clear();
        }

        /// <summary>
        /// Launches <paramref name="effectId"/> owned by actor <paramref name="onActor"/> (followed while it lives),
        /// aimed at <paramref name="aimActor"/> (a projectile's destination / the other end of the cast), with
        /// <paramref name="count"/> in +0x174 (the client's effect msg 0x2c).
        /// </summary>
        static bool LaunchOnActor(RoEffectManager manager, IRoCombatPresentationContext context, int effectId,
                                  uint onActor, uint caster, uint aimActor, int count, int durationTicks, out uint handle) {
            handle = 0;
            if (effectId < 0 || onActor == 0 ||
                !context.TryResolveActorFeedbackAnchor(onActor, out Transform follow, out Vector3 position)) {
                return false;
            }

            var options = new RoEffectSpawnOptions {
                FollowTarget = follow,
                SkillLevel = count <= 0 ? 0 : Mathf.Clamp(count, 1, 50),   // 0: no msg 0x2c, +0x174 stays 0
                ClassicDurationTicks = durationTicks,
                OnActorId = onActor,
            };

            if (onActor == caster) {
                options.SourceTarget = follow;
                options.SourceWorldPosition = position;
                options.HasSourceWorldPosition = true;
                options.SourceActorId = caster;
                options.TargetActorId = aimActor;
            } else {
                RoCombatEffectOptions.ApplySource(ref options, context, caster);
                options.SourceActorId = caster;
                options.TargetActorId = onActor;
            }

            if (aimActor != 0 && aimActor != onActor &&
                context.TryResolveActorFeedbackAnchor(aimActor, out Transform aimFollow, out Vector3 aimPosition)) {
                options.TargetWorldPosition = aimPosition;
                options.HasTargetWorldPosition = true;
                if (aimFollow != null) {
                    manager.RegisterClassicActorAnchor(aimActor, aimFollow);
                }
            }

            return Launch(manager, effectId, position, options, out handle);
        }

        static bool LaunchAtCell(RoEffectManager manager, IRoCombatPresentationContext context, int effectId,
                                 Vector3 cell, uint caster, int level, out uint handle, uint owner = 0) {
            handle = 0;
            if (effectId < 0) {
                return false;
            }

            var options = new RoEffectSpawnOptions { SkillLevel = Mathf.Clamp(level, 1, 50), ClassicOwner = owner };
            RoCombatEffectOptions.ApplySource(ref options, context, caster);
            options.SourceActorId = caster;
            options.TargetWorldPosition = cell;
            options.HasTargetWorldPosition = true;
            return Launch(manager, effectId, cell, options, out handle);
        }

        /// <summary>The ported original engine; ids it does not run yet fall back to the legacy renderers.</summary>
        static bool Launch(RoEffectManager manager, int effectId, Vector3 position, RoEffectSpawnOptions options, out uint handle) {
            if (manager.TrySpawnClassicEffect(effectId, position, options, out handle)) {
                return true;
            }

            handle = 0;
            return manager.TrySpawnEffectId(effectId, position, options: options);
        }
    }
}
