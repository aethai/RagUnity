using System;
using System.Collections.Generic;
using Ragnarok.Audio;
using Ragnarok.Effects;
using UnityEngine;

namespace Ragnarok.Combat {
    /// <summary>What the status handler needs to know about an actor it is told about.</summary>
    public struct RoStatusActorInfo {
        public uint ActorId;
        public bool IsLocalPlayer;
        public bool IsMonster;
        /// <summary>A player job (CPc): opt3 bits launch on set and are never removed on clear.</summary>
        public bool IsPlayerJob;
        public int BodyState;
        public int HealthState;
        public int Option;
        public int Opt3;
    }

    /// <summary>
    /// Presents statuses the way the original client does, from its own status handlers
    /// (<see cref="RoOriginalStatusEffectTable"/>, executed from game.exe, rules in re-custom
    /// packages/status-effects/UNITY_FORMAT.md): EFST on / off, opt1 (body state) value changes, opt2 (health) / option /
    /// opt3 bits, the per-tick Sight / Ruwach relaunch and ZC_NOTIFY_EFFECT*. Each step runs the table's action list in
    /// order on the actor the packet names: classic effects and their removal, sprite overlays, sounds, the actor's own
    /// tint and its render state (<see cref="RoActorStatusLook"/>: colour, visibility, shadow, frame hold), which the
    /// actor presentation applies to remote actors and the own character alike. Actions without a host path yet
    /// (new-system and hat effects, motions) are reported through <see cref="UnhandledAction"/>.
    /// </summary>
    public sealed class RoStatusPresenter {
        const float LogicTickSeconds = 0.016f;
        /// <summary>The client's per-actor tick counter while option == 0 (so the first Sight / Ruwach launches at once).</summary>
        const int IdleTickCounter = 36000;
        const int OptionHide = 0x2;
        const int OptionCloak = 0x4;
        /// <summary>opt3 under HIDE / CLOAK: these auras are removed and nothing else runs (FUN_00c794c0).</summary>
        static readonly int[] HiddenOpt3Removals = { 203, 254, 1132, 418 };
        const int HiddenOpt3PlayerRemoval = 586;

        readonly Func<RoOriginalStatusEffectTable> tableProvider;
        readonly Dictionary<uint, ActorStatus> actors = new Dictionary<uint, ActorStatus>();
        readonly List<uint> scratchIds = new List<uint>();
        float tickAccumulator;

        /// <summary>Actions this host does not perform yet (kind, effect id), for diagnostics and the sweep probe.</summary>
        public event Action<uint, RoStatusAction> UnhandledAction;

        sealed class ActorStatus {
            public bool Known;
            public int BodyState;
            public int HealthState;
            public int Option;
            public int Opt3;
            public bool IsLocalPlayer;
            public bool IsMonster;
            public bool IsPlayerJob;
            public int TickCounter = IdleTickCounter;
            public bool SeenThisSync;
            /// <summary>Classic effects this handler launched on the actor, by effect id (for actor msg 0x6d).</summary>
            public readonly Dictionary<int, List<uint>> Effects = new Dictionary<int, List<uint>>();
            /// <summary>EFSTs currently on, with their val1 (local player: re-applied after a map change).</summary>
            public readonly Dictionary<int, int> ActiveEfst = new Dictionary<int, int>();
            /// <summary>CMsgEffect overlays held by an actor slot (freeze, stun, curse, sleep, silence, angelus ...).</summary>
            public readonly Dictionary<string, RoSpriteEffectRenderer> Overlays = new Dictionary<string, RoSpriteEffectRenderer>();
            /// <summary>The render state the actor's status steps left behind.</summary>
            public RoActorStatusLook Look = RoActorStatusLook.Default;
        }

        public RoStatusPresenter(Func<RoOriginalStatusEffectTable> tableProvider = null) {
            this.tableProvider = tableProvider ?? (() => RoOriginalStatusEffectTable.Default);
        }

        RoOriginalStatusEffectTable Table => tableProvider() ?? RoOriginalStatusEffectTable.Empty;

        // the sync steps (cpuprobe)
        static readonly Unity.Profiling.ProfilerMarker RunMarker = new Unity.Profiling.ProfilerMarker("RO.Status.Run");
        static readonly Unity.Profiling.ProfilerMarker OptionMarker = new Unity.Profiling.ProfilerMarker("RO.Status.ActorOption");
        static readonly Unity.Profiling.ProfilerMarker TableMarker = new Unity.Profiling.ProfilerMarker("RO.Status.Table");

        public int TrackedActorCount => actors.Count;

        public bool IsEfstActive(uint actorId, int efst) =>
            actors.TryGetValue(actorId, out ActorStatus s) && s.ActiveEfst.ContainsKey(efst);

        /// <summary>The actor's status render state (<see cref="RoActorStatusLook.Default"/> for an actor without status).</summary>
        public RoActorStatusLook ResolveLook(uint actorId) =>
            actors.TryGetValue(actorId, out ActorStatus s) ? s.Look : RoActorStatusLook.Default;

        // ---- EFST ------------------------------------------------------------------------------------------------

        /// <summary>
        /// ZC_MSG_STATE_CHANGE* / ZC_EFST_SET_ENTER*: state 1 runs the EFST's on list, 0 its off list (the variant for
        /// the local player, a monster or the packet's val1 when the table has one).
        /// </summary>
        public void HandleEfst(in RoStatusActorInfo actor, int efst, bool active, int val1, IRoCombatPresentationContext context) {
            if (actor.ActorId == 0 || context == null) return;
            ActorStatus status = Ensure(actor);
            if (active) status.ActiveEfst[efst] = val1;
            else status.ActiveEfst.Remove(efst);

            if (!Table.Efst.TryGetValue(efst, out RoStatusEntry entry)) return;
            RoStatusActionSet set = entry.Resolve(actor.IsLocalPlayer, actor.IsMonster, val1);
            Run(actor.ActorId, status, active ? set.On : set.Off, context, playSounds: true);
        }

        /// <summary>The local player after a map change: every EFST still on runs its reapply list (FUN_00cd2c90).</summary>
        public void ReapplyLocalPlayer(uint actorId, IRoCombatPresentationContext context) {
            if (!actors.TryGetValue(actorId, out ActorStatus status) || context == null) return;
            foreach (KeyValuePair<int, int> active in new List<KeyValuePair<int, int>>(status.ActiveEfst)) {
                if (!Table.Efst.TryGetValue(active.Key, out RoStatusEntry entry)) continue;
                RoStatusActionSet set = entry.Resolve(localPlayer: true, monster: false, active.Value);
                RoStatusActionSet remote = entry.Remote;
                Run(actorId, status, set.Reapply ?? remote.Reapply ?? Array.Empty<RoStatusAction>(), context, playSounds: false);
            }
        }

        // ---- opt1 / opt2 / option / opt3 ----------------------------------------------------------------------------

        /// <summary>
        /// Brings the actor's body / health / option / opt3 visuals to <paramref name="actor"/>'s values. The first call
        /// for an actor is its entry into view (entry lists, no sounds); later calls run off / on per changed value or bit.
        /// </summary>
        public void SyncActorState(in RoStatusActorInfo actor, IRoCombatPresentationContext context) {
            if (actor.ActorId == 0 || context == null) return;
            ActorStatus status = Ensure(actor);
            status.SeenThisSync = true;
            RoOriginalStatusEffectTable table;
            using (TableMarker.Auto()) {
                table = Table;
            }

            bool entering = !status.Known;
            int oldBody = entering ? 0 : status.BodyState;
            int oldHealth = entering ? 0 : status.HealthState;
            int oldOption = entering ? 0 : status.Option;
            int oldOpt3 = entering ? 0 : status.Opt3;
            status.Known = true;
            status.BodyState = actor.BodyState;
            status.HealthState = actor.HealthState;
            status.Option = actor.Option;
            status.Opt3 = actor.Opt3;

            // opt1: only when it changes — off of the old value, then on of the new one
            if (oldBody != actor.BodyState) {
                if (oldBody != 0 && table.BodyState.TryGetValue(oldBody, out RoStatusEntry oldEntry)) {
                    Run(actor.ActorId, status, Pick(oldEntry, actor).Off, context, playSounds: true);
                }

                if (actor.BodyState != 0 && table.BodyState.TryGetValue(actor.BodyState, out RoStatusEntry newEntry)) {
                    RoStatusActionSet set = Pick(newEntry, actor);
                    Run(actor.ActorId, status, entering ? set.Entry ?? set.On : set.On, context, playSounds: !entering);
                }
            }

            SyncBits(actor, status, table.HealthState, oldHealth, actor.HealthState, entering, context);
            SyncBits(actor, status, table.Option, oldOption, actor.Option, entering, context);
            if (oldOption != actor.Option || entering) {
                // the actor renderer reads the option too (Chase Walk footprints, flying hover)
                using var optionMarker = OptionMarker.Auto();
                RoEffectManager manager = context.ResolveEffectManager();
                if (manager != null) {
                    context.TryResolveActorFeedbackAnchor(actor.ActorId, out Transform anchor, out _);
                    manager.SetClassicActorOption(actor.ActorId, actor.Option, anchor);
                }
            }

            if (oldOption == 0 && actor.Option != 0) status.TickCounter = IdleTickCounter;
            SyncOpt3(actor, status, table, oldOpt3, entering, context);
        }

        void SyncBits(in RoStatusActorInfo actor, ActorStatus status, Dictionary<int, RoStatusEntry> entries, int oldBits,
                      int newBits, bool entering, IRoCombatPresentationContext context) {
            int changed = oldBits ^ newBits;
            if (changed == 0) return;
            for (int bit = 1; bit != 0; bit <<= 1) {
                if ((changed & bit) == 0 || !entries.TryGetValue(bit, out RoStatusEntry entry)) continue;
                RoStatusActionSet set = Pick(entry, actor);
                if ((newBits & bit) != 0) {
                    Run(actor.ActorId, status, entering ? set.Entry ?? set.On : set.On, context, playSounds: !entering);
                } else {
                    Run(actor.ActorId, status, set.Off, context, playSounds: true);
                }
            }
        }

        void SyncOpt3(in RoStatusActorInfo actor, ActorStatus status, RoOriginalStatusEffectTable table, int oldOpt3,
                      bool entering, IRoCombatPresentationContext context) {
            if (oldOpt3 == actor.Opt3 && !entering) return;
            // FUN_00c794c0: under HIDE / CLOAK the auras go and nothing else runs
            if ((actor.Option & (OptionHide | OptionCloak)) != 0) {
                foreach (int ef in HiddenOpt3Removals) RemoveEffects(actor.ActorId, status, ef, context);
                if (actor.IsPlayerJob) RemoveEffects(actor.ActorId, status, HiddenOpt3PlayerRemoval, context);
                return;
            }

            for (int bit = 1; bit != 0; bit <<= 1) {
                bool set = (actor.Opt3 & bit) != 0, was = (oldOpt3 & bit) != 0;
                if (!table.Opt3.TryGetValue(bit, out RoStatusOpt3Entry entry)) continue;
                if (actor.IsPlayerJob) {
                    // player jobs launch for every set bit (if not active); cleared bits remove nothing here
                    if (set && (!was || entering)) {
                        foreach (RoStatusAction action in entry.PlayerJob.On) {
                            if (action.Kind == RoStatusActionKind.Effect && HasEffect(actor.ActorId, status, action.EffectId, context)) continue;
                            RunOne(actor.ActorId, status, action, context, playSounds: !entering);
                        }
                    }
                } else if (set != was) {
                    Run(actor.ActorId, status, set ? entry.NonPlayerJob.On : entry.NonPlayerJob.Off, context, playSounds: !entering);
                }
            }
        }

        static RoStatusActionSet Pick(RoStatusEntry entry, in RoStatusActorInfo actor) =>
            entry.Resolve(actor.IsLocalPlayer, actor.IsMonster, 0);

        /// <summary>Start of a snapshot sync pass: actors not synced before <see cref="EndSync"/> have left the view.</summary>
        public void BeginSync() {
            foreach (ActorStatus status in actors.Values) status.SeenThisSync = false;
        }

        /// <summary>Actors that were not synced since <see cref="BeginSync"/> left: their status effects end with them.</summary>
        public void EndSync(IRoCombatPresentationContext context, uint keepActorId = 0) {
            scratchIds.Clear();
            foreach (KeyValuePair<uint, ActorStatus> pair in actors) {
                if (!pair.Value.SeenThisSync && pair.Key != keepActorId) scratchIds.Add(pair.Key);
            }

            foreach (uint id in scratchIds) RemoveActor(id, context);
        }

        public void RemoveActor(uint actorId, IRoCombatPresentationContext context) {
            if (!actors.TryGetValue(actorId, out ActorStatus status)) return;
            EndAllEffects(status, context?.ResolveEffectManager());
            actors.Remove(actorId);
        }

        // ---- per tick ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The client's per-actor logic tick (16 ms, FUN_00bd4210): while option has Sight (0x1) or Ruwach (0x2000) the
        /// shared counter relaunches EF 22 every 270 / EF 33 every 200 ticks, with count (time / 16) % 10 + 1.
        /// </summary>
        public void Tick(float deltaSeconds, IRoCombatPresentationContext context) {
            if (context == null || actors.Count == 0) return;
            tickAccumulator += Mathf.Max(0f, deltaSeconds);
            int ticks = (int) (tickAccumulator / LogicTickSeconds);
            if (ticks <= 0) return;
            tickAccumulator -= ticks * LogicTickSeconds;
            ticks = Mathf.Min(ticks, 16);   // a hitch must not burst launches

            RoOriginalStatusEffectTable table = Table;
            scratchIds.Clear();
            scratchIds.AddRange(actors.Keys);
            foreach (uint id in scratchIds) {
                if (!actors.TryGetValue(id, out ActorStatus status)) continue;
                for (int t = 0; t < ticks; t++) {
                    if (status.Option == 0) {
                        status.TickCounter = IdleTickCounter;
                        break;
                    }

                    status.TickCounter++;
                    foreach (KeyValuePair<int, RoStatusPerTickEntry> perTick in table.OptionPerTick) {
                        if ((status.Option & perTick.Key) == 0 || status.TickCounter < perTick.Value.EveryTicks) continue;
                        int count = (int) ((uint) Environment.TickCount / 16u % 10u) + 1;
                        Launch(id, status, perTick.Value.EffectId, count, null, context);
                        status.TickCounter = 0;
                    }
                }
            }
        }

        // ---- ZC_NOTIFY_EFFECT* ---------------------------------------------------------------------------------------

        /// <summary>ZC_NOTIFY_EFFECT 0x19b (type) — returns false when the table has nothing for the type.</summary>
        public bool HandleNotifyEffectType(uint actorId, int type, IRoCombatPresentationContext context) {
            if (!Table.NotifyEffectType.TryGetValue(type, out RoStatusAction[] actions)) return false;
            Run(actorId, EnsureId(actorId), actions, context, playSounds: true);
            return true;
        }

        /// <summary>ZC_NOTIFY_EFFECT2 0x1f3 / 3 0x284 (effect id): the listed actions, else the plain classic launch.</summary>
        public void HandleNotifyEffectId(uint actorId, int effectId, IRoCombatPresentationContext context) {
            ActorStatus status = EnsureId(actorId);
            if (Table.NotifyEffectId.TryGetValue(effectId, out RoStatusAction[] actions)) {
                Run(actorId, status, actions, context, playSounds: true);
            } else {
                Launch(actorId, status, effectId, -1, null, context, track: false);
            }
        }

        public void Clear(IRoCombatPresentationContext context = null) {
            RoEffectManager manager = context?.ResolveEffectManager();
            foreach (ActorStatus status in actors.Values) EndAllEffects(status, manager);
            actors.Clear();
            tickAccumulator = 0f;
        }

        /// <summary>
        /// Map change: effects and view state go, but the local player's active EFSTs are kept for
        /// <see cref="ReapplyLocalPlayer"/> (the client re-applies them after the new map loaded).
        /// </summary>
        public void ClearForMapChange(uint localPlayerId, IRoCombatPresentationContext context = null) {
            RoEffectManager manager = context?.ResolveEffectManager();
            ActorStatus keep = null;
            foreach (KeyValuePair<uint, ActorStatus> pair in actors) {
                EndAllEffects(pair.Value, manager);
                if (pair.Key == localPlayerId) keep = pair.Value;
            }

            actors.Clear();
            if (keep != null) {
                var fresh = new ActorStatus { IsLocalPlayer = true };
                foreach (KeyValuePair<int, int> efst in keep.ActiveEfst) fresh.ActiveEfst[efst.Key] = efst.Value;
                actors[localPlayerId] = fresh;
            }
        }

        // ---- actions -------------------------------------------------------------------------------------------------

        void Run(uint actorId, ActorStatus status, RoStatusAction[] actions, IRoCombatPresentationContext context, bool playSounds) {
            if (actions == null) return;
            using var runMarker = RunMarker.Auto();
            RoActorStatusLook lookBefore = status.Look;
            // a CMsgEffect overlay stays attached when the same step sets an actor slot for it (freeze, stun ...) and
            // goes when a later step clears that slot; without a slot it plays once (the Hiding smoke, the ice breaking)
            string heldSlot = null;
            foreach (RoStatusAction action in actions) {
                if (action.Kind != RoStatusActionKind.Actor || action.Actor?.MsgEffectSlots == null) continue;
                foreach (KeyValuePair<string, bool> slot in action.Actor.MsgEffectSlots) {
                    if (slot.Value) heldSlot = slot.Key;
                }
            }

            foreach (RoStatusAction action in actions) {
                if (action.Kind == RoStatusActionKind.MsgEffect) {
                    // SI_ON_PUSH_CART's cart (the CMsgEffect type 3 in actor +0x380) is the actor's Cart layer, which follows
                    // the owner behind it (RoActorLayeredVisual); an overlay here drew a second cart standing at the feet
                    if (heldSlot != null && heldSlot.Contains("cartOrWug")) {
                        heldSlot = null;
                        continue;
                    }

                    SpawnOverlay(actorId, status, action, heldSlot, context);
                    heldSlot = null;
                    continue;
                }

                if (action.Kind == RoStatusActionKind.Actor && action.Actor?.MsgEffectSlots != null) {
                    foreach (KeyValuePair<string, bool> slot in action.Actor.MsgEffectSlots) {
                        if (!slot.Value) StopOverlay(status, slot.Key);
                    }
                }

                RunOne(actorId, status, action, context, playSounds);
            }

            if (!status.Look.Equals(lookBefore) && context is IRoCombatActorPresentationContext actors) {
                actors.ApplyActorStatusLook(actorId, status.Look);
            }
        }

        /// <summary>The CMsgEffect sprite overlay (data\sprite\ + the table's CP949-decoded path) on the actor.</summary>
        void SpawnOverlay(uint actorId, ActorStatus status, RoStatusAction action, string heldSlot, IRoCombatPresentationContext context) {
            RoEffectManager manager = context.ResolveEffectManager();
            if (manager == null || string.IsNullOrEmpty(action.Sprite) ||
                !context.TryResolveActorFeedbackAnchor(actorId, out Transform follow, out Vector3 position)) {
                UnhandledAction?.Invoke(actorId, action);
                return;
            }

            if (heldSlot != null) StopOverlay(status, heldSlot);
            string path = "data/sprite/" + action.Sprite.Replace('\\', '/');
            var options = new RoEffectSpawnOptions {
                FollowTarget = follow,
                SpriteActionIndex = action.Action,
            };
            // the client's resource lookup ignores case (the table says Status-stun.spr, the GRF holds status-stun.spr)
            RoSpriteEffectRenderer overlay = manager.SpawnSpriteEffect(path, position, options, loop: heldSlot != null) ??
                                             manager.SpawnSpriteEffect(path.ToLowerInvariant(), position, options, loop: heldSlot != null);
            if (overlay == null) {
                UnhandledAction?.Invoke(actorId, action);
                return;
            }

            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"status overlay {action.Sprite} action={action.Action} slot={heldSlot ?? "-"} aid={actorId}");
            if (heldSlot != null) status.Overlays[heldSlot] = overlay;
        }

        static void StopOverlay(ActorStatus status, string slot) {
            if (status.Overlays.TryGetValue(slot, out RoSpriteEffectRenderer overlay)) {
                if (overlay != null && !overlay.IsFinished) overlay.Stop();
                status.Overlays.Remove(slot);
            }
        }

        static void StopOverlays(ActorStatus status) {
            foreach (RoSpriteEffectRenderer overlay in status.Overlays.Values) {
                if (overlay != null && !overlay.IsFinished) overlay.Stop();
            }

            status.Overlays.Clear();
        }

        void RunOne(uint actorId, ActorStatus status, RoStatusAction action, IRoCombatPresentationContext context, bool playSounds) {
            switch (action.Kind) {
                case RoStatusActionKind.Effect:
                    if (!Launch(actorId, status, action.EffectId, action.Count, action.Color, context)) {
                        UnhandledAction?.Invoke(actorId, action);
                    }

                    break;
                case RoStatusActionKind.RemoveEffect:
                    RemoveEffects(actorId, status, action.EffectId, context);
                    break;
                case RoStatusActionKind.Sound:
                    if (playSounds && !string.IsNullOrEmpty(action.Text) &&
                        context.TryResolveActorFeedbackAnchor(actorId, out _, out Vector3 position)) {
                        RoAudioEvents.PlayEffect(action.Text, position);
                    }

                    break;
                case RoStatusActionKind.Actor:
                    status.Look.Apply(action.Actor);   // its CMsgEffect slots were handled by Run
                    break;
                case RoStatusActionKind.SetArgb:
                    if (action.Argb != null && action.Argb.Length == 4 &&
                        context.TryResolveActorFeedbackAnchor(actorId, out Transform anchor, out _)) {
                        context.ResolveEffectManager()?.SetClassicActorArgb(
                            actorId, action.Argb[0], action.Argb[1], action.Argb[2], action.Argb[3], anchor);
                    }

                    break;
                case RoStatusActionKind.Call:
                case RoStatusActionKind.MsgEffect:   // drawn by Run (it needs the step's actor slot)
                    break;   // documented client call without a host action
                default:
                    UnhandledAction?.Invoke(actorId, action);
                    break;
            }
        }

        bool Launch(uint actorId, ActorStatus status, int effectId, int count, int[] color, IRoCombatPresentationContext context,
                    bool track = true) {
            RoEffectManager manager = context.ResolveEffectManager();
            if (manager == null || effectId < 0 ||
                !context.TryResolveActorFeedbackAnchor(actorId, out Transform follow, out Vector3 position)) {
                return false;
            }

            var options = new RoEffectSpawnOptions {
                FollowTarget = follow,
                SourceTarget = follow,
                SourceWorldPosition = position,
                HasSourceWorldPosition = true,
                SourceActorId = actorId,
                OnActorId = actorId,
                // effect msg 0x2c(n) → +0x174 = min(n, 50); without it the effect keeps 0
                SkillLevel = count > 0 ? Mathf.Min(count, 50) : 0,
            };

            if (!manager.TrySpawnClassicEffect(effectId, position, options, out uint handle) || handle == 0) {
                return false;
            }

            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"status ef {effectId} aid={actorId}");
            if (color != null && color.Length == 3) {   // [-1,-1,-1] / [-2,-2,-2]: msg 0x2c(0xfffe, 0x80 / 0x82) writes those floats
                // effect msg 0x2c(0xffff, sel): the effect's colour floats
                manager.SetClassicEffectColor(handle, color[0], color[1], color[2]);
            }

            if (track) {
                if (!status.Effects.TryGetValue(effectId, out List<uint> handles)) {
                    handles = new List<uint>(1);
                    status.Effects[effectId] = handles;
                }

                handles.Add(handle);
            }

            return true;
        }

        /// <summary>
        /// Actor msg 0x6d until none is left: every running instance of <paramref name="effectId"/> on the actor ends
        /// (effect msg 0x18), whoever launched it — e.g. Aura Blade's EF 474 from the skill packet goes with EFST 103's off.
        /// </summary>
        static void RemoveEffects(uint actorId, ActorStatus status, int effectId, IRoCombatPresentationContext context) {
            RoEffectManager manager = context?.ResolveEffectManager();
            if (manager != null) {
                manager.RemoveClassicEffectsById(actorId, effectId);
                if (status.Effects.TryGetValue(effectId, out List<uint> handles)) {
                    foreach (uint handle in handles) manager.EndClassicEffect(handle);
                }
            }

            status.Effects.Remove(effectId);
        }

        /// <summary>Actor msg 0x78: whether an instance of <paramref name="effectId"/> runs on the actor.</summary>
        static bool HasEffect(uint actorId, ActorStatus status, int effectId, IRoCombatPresentationContext context) {
            RoEffectManager manager = context?.ResolveEffectManager();
            if (manager != null && manager.HasClassicEffect(actorId, effectId)) return true;
            if (!status.Effects.TryGetValue(effectId, out List<uint> handles)) return false;
            if (manager != null) handles.RemoveAll(h => !manager.IsClassicEffectAlive(h));
            return handles.Count > 0;
        }

        static void EndAllEffects(ActorStatus status, RoEffectManager manager) {
            StopOverlays(status);
            if (manager != null) {
                foreach (List<uint> handles in status.Effects.Values) {
                    foreach (uint handle in handles) manager.EndClassicEffect(handle);
                }
            }

            status.Effects.Clear();
        }

        ActorStatus Ensure(in RoStatusActorInfo actor) {
            ActorStatus status = EnsureId(actor.ActorId);
            status.IsLocalPlayer = actor.IsLocalPlayer;
            status.IsMonster = actor.IsMonster;
            status.IsPlayerJob = actor.IsPlayerJob;
            return status;
        }

        ActorStatus EnsureId(uint actorId) {
            if (!actors.TryGetValue(actorId, out ActorStatus status)) {
                status = new ActorStatus();
                actors[actorId] = status;
            }

            return status;
        }
    }
}
