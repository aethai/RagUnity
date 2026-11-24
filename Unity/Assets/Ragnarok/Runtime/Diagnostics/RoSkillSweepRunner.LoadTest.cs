using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Ragnarok.Effects.Classic;
using Ragnarok.Network;
using Ragnarok.World;
using Unity.Profiling;
using UnityEngine;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Load test commands of the sweep console (the scale target: stable frames with 300-500 actors on screen): a crowd
    /// of client-side players, walking or standing, skill uses of the whole sweep pool played through the injection path
    /// of <see cref="InjectSkill"/>, a storm of classic effects, the WoE scenario that runs them together and the
    /// reproducible <c>!bench</c>, whose report goes to PerformanceCaptures/bench. Everything here is client-side: no
    /// packet goes to the server.
    /// </summary>
    public sealed partial class RoSkillSweepRunner {
        const uint CrowdFirstId = 3900000;
        /// <summary>An injected spam use casts at most this long (the cast circle and bar play, the use does not wait the
        /// skill's real cast time).</summary>
        const int SpamCastCapMs = 600;
        const float SpamUnitSeconds = 3.5f;
        const int MaxSpamUsesPerFrame = 32;
        const int MaxPendingPerFrame = 64;
        const int MaxWalkLegsPerFrame = 64;
        const float StageSettleSeconds = 3f;
        const int BenchCrowd = 300, BenchWalkers = 150, BenchFxAlive = 300, BenchCrowdRadius = 13;
        const float BenchSpamPerSecond = 40f;
        const int SpamSeed = 4321, FxStormSeed = 2468, CrowdWalkSeed = 8642, CrowdChatSeed = 9753;

        /// <summary>The load test commands (also documented where the console parses them), for the bench report.</summary>
        static readonly string[] LoadTestCommands = {
            "!crowd <n> [radius] | !crowd clear - n client-side standing players (fixed seed) on walkable cells around the own character; clear removes them and stops the crowd walk",
            "!crowddie <n> - the first n crowd players die and lie in the dead pose",
            "!crowdwalk <n|all> [cells] | !crowdwalk stop - the first n crowd players walk back and forth over [cells] (5) cells (ZC_NOTIFY_MOVE records applied like the drain)",
            "!skillspam <total> [per second] | !skillspam stop - random uses (50/s) of the whole sweep skill pool (RoLoadTestSkills, 365 skills) from crowd players (or the own character) on random crowd players / self / cells within 8 cells, casts capped at 600 ms",
            "!fxspam <total> [per second] [radius] | !fxspam stop - the pool's ground skills alone from the own character on cells within [radius] (8)",
            "!fxstorm <alive> [seconds] [radius] | !fxstorm stop - keeps <alive> classic effects running for [seconds] (20), random ids of every ported effect within [radius] (10) cells",
            "!woe <players> [seconds] | !woe stop - WoE scenario: a crowd of <players>, half of it walking, fxstorm min(players, 300) alive and skillspam 40/s together for [seconds] (30), then cleaned up",
            "!bench [seconds per stage] | !bench stop - fixed-seed stages (empty; crowd 300; 150 walking; + fxstorm 300; + skillspam 40/s), [seconds] (15) each after a 3 s settle, report in PerformanceCaptures/bench",
            "!walkloop <cells> <seconds> - the own character walks back and forth (server moves)",
            "!crowdui <balloons per second> [shops] [rooms] | !crowdui stop - the crowd's overhead UI: chat balloons from random crowd players at that rate (the 0x008d path, RoClassicChatBalloons.Show), [shops] buying store boards and [rooms] chat room boards on the first crowd players (RoClassicActorBoards)",
        };

        int crowdCount;
        int crowdWalkers;
        Coroutine crowdChatRoutine;
        static readonly string[] CrowdChatLines = {
            "hi", "anyone selling oridecon?", "lf party 90+ glast heim, priest pls", "S> +7 Chain Mail [1] 2.5m, pm me",
            "wts elu 45k ea", "B> Yggdrasil Berry x10 @ 18k", "lol", "brb", "guild recruiting! all jobs welcome, WoE every sat",
            "thx", "ty for the buff!", "where is the kafra?", "need 1 more for the run", "afk 5 min", "gg",
        };

        /// <summary>
        /// !crowdui: the crowd's overhead UI as a town shows it — chat balloons from random crowd players at
        /// <paramref name="perSecond"/>, <paramref name="shops"/> buying store boards and <paramref name="rooms"/> chat room
        /// boards on the first crowd players (fed through the same entry points as the packets).
        /// </summary>
        string CrowdUi(float perSecond, int shops, int rooms) {
            if (crowdCount == 0) return "no crowd (!crowd first)";
            var boards = new RoBuyingStoreBoard[Math.Min(shops, crowdCount)];
            for (int i = 0; i < boards.Length; i++) boards[i] = new RoBuyingStoreBoard(CrowdFirstId + (uint) i, "B> cheap stuff #" + i);
            Ragnarok.UI.RoClassicActorBoards.SyncBuying(boards, 0, null);
            int roomCount = Math.Min(rooms, Math.Max(0, crowdCount - boards.Length));
            var chats = new RoChatRoomInfo[roomCount];
            for (int i = 0; i < chats.Length; i++) {
                chats[i] = new RoChatRoomInfo(CrowdFirstId + (uint) (boards.Length + i), (uint) (900000 + i), 20, 1 + i % 20, (byte) (i % 2), "chat room " + i);
            }

            Ragnarok.UI.RoClassicActorBoards.SyncChats(chats, 0, null);
            if (crowdChatRoutine != null) StopCoroutine(crowdChatRoutine);
            crowdChatRoutine = perSecond > 0f ? StartCoroutine(CrowdChat(perSecond)) : null;
            return "crowdui: " + perSecond + " balloons/s, " + boards.Length + " shop boards, " + chats.Length + " room boards";
        }

        string StopCrowdUi() {
            if (crowdChatRoutine != null) StopCoroutine(crowdChatRoutine);
            crowdChatRoutine = null;
            Ragnarok.UI.RoClassicActorBoards.SyncBuying(Array.Empty<RoBuyingStoreBoard>(), 0, null);
            Ragnarok.UI.RoClassicActorBoards.SyncChats(Array.Empty<RoChatRoomInfo>(), 0, null);
            Ragnarok.UI.RoClassicChatBalloons.ClearAll();
            return "crowdui stopped";
        }

        IEnumerator CrowdChat(float perSecond) {
            var rng = new System.Random(CrowdChatSeed);
            var context = world as Ragnarok.Combat.IRoCombatPresentationContext;
            float due = 0f;
            while (true) {
                yield return null;
                due += perSecond * Time.unscaledDeltaTime;
                while (due >= 1f && crowdCount > 0 && context != null) {
                    due -= 1f;
                    uint id = CrowdFirstId + (uint) rng.Next(crowdCount);
                    if (context.TryResolveActorFeedbackAnchor(id, out Transform follow, out Vector3 position)) {
                        Ragnarok.UI.RoClassicChatBalloons.Show(id, follow, position, CrowdChatLines[rng.Next(CrowdChatLines.Length)]);
                    }
                }
            }
        }
        Coroutine skillSpamRoutine, fxStormRoutine, crowdWalkRoutine, scenarioRoutine;
        FrameStats skillSpamStats, fxStormStats, scenarioStats;
        System.Random spamRng = new System.Random(SpamSeed);
        static int[] classicEffectIds;
        static int[] groundSkillIndices;
        // the fxstorm's own effects, ended when it stops: loopers (owner-bound auras and the like) never end by themselves
        readonly System.Collections.Generic.List<uint> fxStormHandles = new System.Collections.Generic.List<uint>(512);

        // ---- injected uses still to come (landings after the cast, trap triggers, unit removals), pumped by Update
        struct PendingSpam {
            public float Due;
            public byte Kind;
            public short Skill;
            public uint Source, Target, Unit;
            public short X, Y;
        }

        const byte PendingLand = 0, PendingTrap = 1, PendingRemoveUnit = 2;
        readonly PendingSpam[] pendingSpam = new PendingSpam[4096];
        int pendingSpamCount;
        int spamDropped;

        void PumpPendingSpam() {
            if (pendingSpamCount == 0 || world == null) return;
            float now = Now;
            int budget = MaxPendingPerFrame;
            for (int i = 0; i < pendingSpamCount && budget > 0;) {
                if (pendingSpam[i].Due > now) {
                    i++;
                    continue;
                }

                PendingSpam p = pendingSpam[i];
                pendingSpam[i] = pendingSpam[--pendingSpamCount];   // swap-remove: slot i is looked at again
                budget--;
                switch (p.Kind) {
                    case PendingLand:
                        LandSpamUse(p.Skill, p.Source, p.Target, p.X, p.Y);
                        break;
                    case PendingTrap:
                        world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcSpriteChange(p.Unit, 0, 0x8c, false)));
                        break;
                    default:
                        world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcSkillDisappear(p.Unit)));
                        break;
                }
            }
        }

        bool EnqueueSpam(float due, byte kind, int skill, uint source, uint target, uint unit, short x, short y) {
            if (pendingSpamCount >= pendingSpam.Length) {
                spamDropped++;
                return false;
            }

            pendingSpam[pendingSpamCount++] = new PendingSpam {
                Due = due, Kind = kind, Skill = (short) skill, Source = source, Target = target, Unit = unit, X = x, Y = y,
            };
            return true;
        }

        /// <summary>Ends what the spam left running: its units go now, landings and trap triggers still due are dropped.</summary>
        void FlushPendingSpam() {
            for (int i = 0; i < pendingSpamCount && world != null; i++) {
                if (pendingSpam[i].Kind == PendingRemoveUnit) {
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcSkillDisappear(pendingSpam[i].Unit)));
                }
            }

            pendingSpamCount = 0;
        }

        void StopLoad() {
            if (skillSpamRoutine != null) StopCoroutine(skillSpamRoutine);
            if (fxStormRoutine != null) StopCoroutine(fxStormRoutine);
            if (crowdWalkRoutine != null) StopCoroutine(crowdWalkRoutine);
            skillSpamRoutine = fxStormRoutine = crowdWalkRoutine = null;
            crowdWalkers = 0;
            EndFxStormEffects();
        }

        void EndFxStormEffects() {
            RoClassicEffectBridge bridge = RoClassicEffectBridge.Instance;
            if (bridge != null) {
                foreach (uint handle in fxStormHandles) bridge.EndEffect(handle);
            }

            fxStormHandles.Clear();
        }

        void DisposeLoadTest() {
            skillSpamStats?.Dispose();
            fxStormStats?.Dispose();
            scenarioStats?.Dispose();
        }

        // ---- console entry points

        void StartSkillSpam(int total, float perSecond, bool areaOnly, int radius) {
            if (skillSpamRoutine != null) StopCoroutine(skillSpamRoutine);
            skillSpamRoutine = null;
            if (total == 0) return;   // stop
            if (total > 0 && skillSpamStats == null) skillSpamStats = new FrameStats();
            skillSpamRoutine = StartCoroutine(SkillSpam(total, perSecond, areaOnly, radius, total > 0 ? skillSpamStats : null));
        }

        void StartFxStorm(int alive, float seconds, float radius, bool report) {
            if (fxStormRoutine != null) StopCoroutine(fxStormRoutine);
            fxStormRoutine = null;
            if (alive <= 0) {
                EndFxStormEffects();
                return;   // stop
            }
            if (report && fxStormStats == null) fxStormStats = new FrameStats();
            fxStormRoutine = StartCoroutine(FxStorm(alive, seconds, radius, report ? fxStormStats : null));
        }

        string StartCrowdWalk(string countArg, int cells) {
            if (crowdWalkRoutine != null) StopCoroutine(crowdWalkRoutine);
            crowdWalkRoutine = null;
            crowdWalkers = 0;
            if (countArg == "stop") return "stopped";
            int count = countArg == "all" ? crowdCount : int.Parse(countArg, CultureInfo.InvariantCulture);
            if (world == null || crowdCount == 0) return "no crowd (!crowd <n> first)";
            crowdWalkRoutine = StartCoroutine(CrowdWalk(count, Math.Max(1, cells), CrowdWalkSeed));
            return "starting " + Math.Min(count, crowdCount) + " of " + crowdCount + ", " + cells + " cells";
        }

        void StartScenario(IEnumerator scenario) {
            if (scenarioRoutine != null) StopCoroutine(scenarioRoutine);
            scenarioRoutine = scenario != null ? StartCoroutine(scenario) : null;
        }

        void StopScenario(string what) {
            StartScenario(null);
            StopLoad();
            FlushPendingSpam();
            Log(what + " stopped: " + ClearCrowd());
        }

        // ---- skill spam

        static int[] GroundSkillIndices() {
            if (groundSkillIndices != null) return groundSkillIndices;
            int n = 0;
            RoLoadTestSkills.Entry[] all = RoLoadTestSkills.All;
            for (int i = 0; i < all.Length; i++) {
                if (all[i].Mode == RoLoadTestSkills.Mode.Ground) n++;
            }

            var indices = new int[n];
            n = 0;
            for (int i = 0; i < all.Length; i++) {
                if (all[i].Mode == RoLoadTestSkills.Mode.Ground) indices[n++] = i;
            }

            return groundSkillIndices = indices;
        }

        /// <summary>
        /// Random uses of the skill pool, <paramref name="perSecond"/> at most <see cref="MaxSpamUsesPerFrame"/> per
        /// frame; <paramref name="total"/> &lt; 0 runs until stopped (scenarios, no summary). Each use plays what
        /// InjectSkill plays: ZC_USESKILL_ACK (cast capped at <see cref="SpamCastCapMs"/>), then after it the skill's
        /// packets (<see cref="LandSpamUse"/>), queued instead of a coroutine per use.
        /// </summary>
        IEnumerator SkillSpam(int total, float perSecond, bool areaOnly, int radius, FrameStats stats) {
            if (world == null || !TryGetSelf(out RoOnlineActorSnapshot self)) {
                Log((areaOnly ? "fxspam" : "skillspam") + " ABORTED: no world controller or no own actor in the world state (not on a map?), nothing played");
                skillSpamRoutine = null;
                yield break;
            }

            spamRng = new System.Random(SpamSeed);
            RoLoadTestSkills.Entry[] pool = RoLoadTestSkills.All;
            int[] groundPool = GroundSkillIndices();
            uint selfId = world.OwnActorId;
            int centreX = self.CellX, centreY = self.CellY;
            float interval = 1f / Mathf.Max(0.1f, perSecond);
            float start = Now, next = start;
            int sent = 0, droppedBefore = spamDropped;
            bool unlimited = total < 0;
            RoClassicEffectBridge bridge = RoClassicEffectBridge.Instance;
            RoOnlineActorPresenter presenter = FindFirstObjectByType<RoOnlineActorPresenter>();
            Log($"{(areaOnly ? "fxspam" : "skillspam")} start: {(unlimited ? "until stopped" : total + " uses")} at {perSecond}/s, " +
                $"{(areaOnly ? groundPool.Length : pool.Length)} skills in the pool, sources {(crowdCount > 0 && !areaOnly ? crowdCount + " crowd players" : "the own character")}, " +
                $"cells within {radius} of {centreX},{centreY}");
            stats?.Begin();
            while (unlimited || sent < total) {
                stats?.Sample(bridge, world.State, presenter);
                float now = Now;
                int budget = MaxSpamUsesPerFrame;
                while ((unlimited || sent < total) && next <= now && budget-- > 0) {
                    int skill = areaOnly ? groundPool[spamRng.Next(groundPool.Length)] : spamRng.Next(pool.Length);
                    uint source = !areaOnly && crowdCount > 0 ? CrowdFirstId + (uint) spamRng.Next(crowdCount) : selfId;
                    uint target = crowdCount > 1 ? CrowdFirstId + (uint) spamRng.Next(crowdCount) : selfId;
                    if (target == source) target = selfId;
                    int x = 0, y = 0;
                    if (pool[skill].Mode == RoLoadTestSkills.Mode.Ground) {
                        x = centreX + spamRng.Next(-radius, radius + 1);
                        y = centreY + spamRng.Next(-radius, radius + 1);
                        SnapGroundToWalkable(ref x, ref y);
                    }

                    PlaySpamUse(skill, source, target, (short) x, (short) y);
                    sent++;
                    next += interval;
                }

                // a frame too slow for the rate does not pile up a burst for the next ones
                if (next < now - 1f) next = now;
                yield return null;
            }

            // let the last effects play out before the summary
            float settleEnd = Now + 3f;
            while (Now < settleEnd) {
                stats?.Sample(bridge, world.State, presenter);
                yield return null;
            }

            StageResult result = stats != null ? stats.End("skillspam") : default;
            Log($"{(areaOnly ? "fxspam" : "skillspam")} done: {sent} uses in {Now - start:F1} s ({pool.Length} skills in the pool, " +
                $"{spamDropped - droppedBefore} queue drops), " + result.Describe());
            skillSpamRoutine = null;
        }

        void PlaySpamUse(int skill, uint source, uint target, short x, short y) {
            RoLoadTestSkills.Entry e = RoLoadTestSkills.All[skill];
            bool ground = e.Mode == RoLoadTestSkills.Mode.Ground;
            uint castTarget = e.Mode == RoLoadTestSkills.Mode.Enemy || e.Mode == RoLoadTestSkills.Mode.Friend ? target : ground ? 0u : source;
            int castMs = Math.Min(e.CastMs, SpamCastCapMs);
            if (castMs <= 0) {
                LandSpamUse(skill, source, castTarget, x, y);
                return;
            }

            world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                new RoZcUseSkillAck(source, castTarget, (ushort) x, (ushort) y, e.Id, (uint) Math.Max(0, e.Element), (uint) castMs, 1, 0)));
            EnqueueSpam(Now + castMs / 1000f + 0.05f, PendingLand, skill, source, castTarget, 0, x, y);
        }

        /// <summary>The packets after the cast, as <see cref="InjectSkill"/> plays them for the sweep.</summary>
        void LandSpamUse(int skill, uint source, uint target, short x, short y) {
            if (world == null) return;
            RoLoadTestSkills.Entry e = RoLoadTestSkills.All[skill];
            bool ground = e.Mode == RoLoadTestSkills.Mode.Ground;
            const RoLoadTestSkills.Flags packetFlags = RoLoadTestSkills.Flags.Damage | RoLoadTestSkills.Flags.Result | RoLoadTestSkills.Flags.GroundSkill;
            RoLoadTestSkills.Flags packets = e.Flags & packetFlags;
            if (packets != RoLoadTestSkills.Flags.None) {
                if ((packets & RoLoadTestSkills.Flags.Damage) != 0 && target != 0) {   // a trap's damage comes from its unit
                    InjectSpamDamage(e, source, target);
                }

                if ((packets & RoLoadTestSkills.Flags.Result) != 0) {
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                        new RoZcUseSkillResult(e.Id, e.Level, target != 0 ? target : source, source, 1)));
                }

                if ((packets & RoLoadTestSkills.Flags.GroundSkill) != 0) {
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcNotifyGroundSkill(e.Id, source, e.Level, x, y, 0)));
                }

                if (e.UnitType > 0) {
                    PlaceSpamUnit(e, source, ground, x, y, (e.Flags & RoLoadTestSkills.Flags.Trap) != 0);
                }
            } else if (e.Mode == RoLoadTestSkills.Mode.Enemy) {
                InjectSpamDamage(e, source, target);
            } else if (ground) {
                world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcNotifyGroundSkill(e.Id, source, e.Level, x, y, 0)));
                if (e.UnitType > 0) {
                    PlaceSpamUnit(e, source, true, x, y, false);
                }
            } else {
                world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcUseSkillResult(e.Id, e.Level, target, source, 1)));
            }
        }

        void InjectSpamDamage(in RoLoadTestSkills.Entry e, uint source, uint target) {
            world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                new RoZcNotifySkill(e.Id, source, target, 0, 300, 230, spamRng.Next(50, 30000), e.Level, e.Hits, 6)));
        }

        /// <summary>Units of a ground skill sit on its cell, those of a self skill (Desperado) around the caster.</summary>
        void PlaceSpamUnit(in RoLoadTestSkills.Entry e, uint source, bool ground, short x, short y, bool trap) {
            if (pendingSpamCount + 2 > pendingSpam.Length) {   // no room to take it off again: no unit
                spamDropped++;
                return;
            }

            if (!ground) {
                if (!world.State.TryGetActor(source, out RoOnlineActorSnapshot caster)) return;
                x = (short) caster.CellX;
                y = (short) caster.CellY;
            }

            uint unitId = 0x7F000000u + (uint) (++injectedUnits);
            world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                new RoZcSkillEntry(e.UnitType, source, x, y, unitId, 0, 1, (byte) e.Level)));
            float now = Now;
            if (trap) EnqueueSpam(now + 1.2f, PendingTrap, 0, source, 0, unitId, x, y);
            EnqueueSpam(now + SpamUnitSeconds, PendingRemoveUnit, 0, source, 0, unitId, x, y);
        }

        // ---- effect storm

        /// <summary>
        /// The own character's transform: the local player is driven by RoClickToMoveController, not presented by
        /// RoOnlineActorPresenter (which only has it in some setups), so the controller first, the presenter as fallback.
        /// </summary>
        bool TryGetOwnTransform(RoOnlineActorPresenter presenter, out Transform own, out string source) {
            var controller = FindFirstObjectByType<Ragnarok.Entities.RoClickToMoveController>();
            if (controller != null) {
                own = controller.transform;
                source = "RoClickToMoveController";
                return true;
            }

            if (presenter != null && world != null && presenter.TryGetPresentedActorTransform(world.OwnActorId, out own)) {
                source = "RoOnlineActorPresenter";
                return true;
            }

            own = null;
            source = "-";
            return false;
        }

        static int[] ClassicEffectIds() {
            if (classicEffectIds != null && classicEffectIds.Length > 0) return classicEffectIds;
            int n = 0;
            for (int id = 1; id < 2000; id++) {
                if (IsStormEffect(id)) n++;
            }

            var ids = new int[n];
            n = 0;
            for (int id = 1; id < 2000; id++) {
                if (IsStormEffect(id)) ids[n++] = id;
            }

            return classicEffectIds = ids;
        }

        /// <summary>
        /// Every classic effect but the screen darkening of EF_DEVIL (334) and its blind sizes 475..484 (fn_b67ca0): the
        /// client launches them for the local player alone, and a random one left at a random point greys the whole screen;
        /// the local player's poison mark (335, a screen-wide ground grid without depth test); and the map decorations
        /// (<see cref="MapEffectIds"/>), built for their map's cliffs and water: waterfalls, glow spheres and curtains
        /// standing in a town square are huge hard-edged walls, nothing a fight shows.
        /// </summary>
        static bool IsStormEffect(int id) => RoClassicEffectBridge.CanHandle(id) && id != 334 && id != 335 && (id < 475 || id > 484) &&
            Array.BinarySearch(MapEffectIds, id) < 0;

        /// <summary>The effect ids of the RSW effect records (object type 4) of the client's 1024 maps (client archives).</summary>
        static readonly int[] MapEffectIds = {
            0, 4, 9, 21, 44, 45, 47, 94, 109, 110, 165, 199, 231, 247, 259, 260, 307, 322, 323, 324,
            349, 350, 351, 352, 353, 354, 355, 356, 358, 359, 360, 557, 558, 559, 560, 561, 562, 650, 651, 681,
            687, 688, 689, 690, 691, 693, 694, 695, 696, 700, 701, 702, 703, 710, 712, 713, 714, 821, 838, 846,
            946, 974, 1025, 1039, 1073, 1412, 2343,
        };

        /// <summary>Keeps <paramref name="alive"/> classic effects running; <paramref name="seconds"/> &lt;= 0 until stopped
        /// (scenarios, no summary).</summary>
        IEnumerator FxStorm(int alive, float seconds, float radius, FrameStats stats) {
            RoClassicEffectBridge bridge = RoClassicEffectBridge.Instance;
            RoOnlineActorPresenter presenter = FindFirstObjectByType<RoOnlineActorPresenter>();
            if (bridge == null) {
                Log("fxstorm ABORTED: no RoClassicEffectBridge.Instance (classic effects off?), nothing launched");
                fxStormRoutine = null;
                yield break;
            }

            if (world == null) {
                Log("fxstorm ABORTED: no RoOnlineWorldController (not on a map?), nothing launched");
                fxStormRoutine = null;
                yield break;
            }

            if (!TryGetOwnTransform(presenter, out Transform own, out string ownSource)) {
                Log("fxstorm ABORTED: no own character transform (neither RoClickToMoveController nor the presenter has it), nothing launched");
                fxStormRoutine = null;
                yield break;
            }

            int[] ids = ClassicEffectIds();
            if (ids.Length == 0) {
                Log("fxstorm ABORTED: RoClassicEffectBridge.CanHandle is false for every id 1..1999 (classic engine disabled or nothing ported), nothing launched");
                fxStormRoutine = null;
                yield break;
            }

            Log($"fxstorm start: {alive} alive, {(seconds > 0f ? seconds.ToString("F0", CultureInfo.InvariantCulture) + " s" : "until stopped")}, " +
                $"radius {radius} cells, {ids.Length} effect ids (CanHandle 1..1999), around the own character from {ownSource}");
            var rng = new System.Random(FxStormSeed);
            float start = Now, end = start + seconds;
            int launched = 0;
            stats?.Begin();
            while (seconds <= 0f || Now < end) {
                stats?.Sample(bridge, world.State, presenter);
                // a few per frame, so a burst does not land on one frame
                for (int k = 0; k < 32 && bridge.ActiveEffects < alive; k++) {
                    Vector2 offset = new Vector2((float) (rng.NextDouble() * 2 - 1), (float) (rng.NextDouble() * 2 - 1)) * radius;
                    if (bridge.LaunchGroundEffect(ids[rng.Next(ids.Length)], own.position + new Vector3(offset.x, 0f, offset.y), out uint handle)) {
                        launched++;
                        fxStormHandles.Add(handle);
                    }
                }

                yield return null;
            }

            StageResult result = stats != null ? stats.End("fxstorm") : default;
            Log($"fxstorm done: {launched} launched ({ids.Length} effect ids), " + result.Describe());
            fxStormRoutine = null;
        }

        // ---- crowd

        string SpawnCrowd(int count, int radius) {
            if (world == null || !TryGetSelf(out RoOnlineActorSnapshot self)) return "no world";
            short[] jobs = { 7, 8, 9, 10, 11, 12, 14, 15, 16, 17, 18, 19, 20 };
            uint[] weapons = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 15, 16 };
            var rng = new System.Random(1234 + crowdCount);
            Ragnarok.Maps.RoGridSelector crowdGrid = world.RuntimeMapController != null ? world.RuntimeMapController.GridSelector : null;
            int spawned = 0;
            var spawnedIds = new System.Collections.Generic.List<uint>(count);
            for (int i = 0; i < count; i++) {
                uint id = CrowdFirstId + (uint) crowdCount + (uint) i;
                // only walkable cells: a player cannot stand inside a wall or a building (and would be drawn over it)
                int x = 0, y = 0;
                for (int attempt = 0; attempt < 32; attempt++) {
                    x = self.CellX + rng.Next(-radius, radius + 1);
                    y = self.CellY + rng.Next(-radius, radius + 1);
                    if (crowdGrid == null || crowdGrid.IsCellWalkable(new Vector2Int(x, y))) {
                        break;
                    }
                }

                short job = jobs[rng.Next(jobs.Length)];
                byte sex = job == 19 ? (byte) 1 : job == 20 ? (byte) 0 : (byte) rng.Next(2);   // bard male, dancer female
                var entry = new RoZcNotifyActorEntry(
                    RoMapActorEntryKind.Standing, 0, RoMapActorObjectType.Player, id, id,
                    150, 0, 0, 0, job, (ushort) rng.Next(1, 24), weapons[rng.Next(weapons.Length)], 0,
                    (ushort) (rng.Next(3) == 0 ? 0 : rng.Next(1, 220)), (ushort) (rng.Next(2) == 0 ? 0 : rng.Next(1, 220)),
                    (ushort) (rng.Next(3) == 0 ? 0 : rng.Next(1, 220)), (short) rng.Next(0, 8), 0, 0, (ushort) (rng.Next(2) == 0 ? 0 : rng.Next(1, 40)), 0, 0, 0, 0, false,
                    sex, false, new RoPacketPosition(x, y, rng.Next(8)), default, 0, 5, 5, 0,
                    RoMapActorSpawnState.Standing, 90, 0, 0, 0, false, 0, "Crowd " + i);   // no HP: the server sends none for other players
                world.State.ApplyActorEntry(entry);
                spawnedIds.Add(id);
                spawned++;
            }

            SyncCrowdPresentation(spawnedIds);
            crowdCount += spawned;
            return "spawned " + spawned + " (total " + crowdCount + ")";
        }

        string ClearCrowd() {
            if (crowdWalkRoutine != null) StopCoroutine(crowdWalkRoutine);
            crowdWalkRoutine = null;
            crowdWalkers = 0;
            if (world == null) return "no world";
            var vanished = new System.Collections.Generic.List<uint>(crowdCount);
            for (int i = 0; i < crowdCount; i++) {
                world.State.ApplyVanish(new RoZcNotifyVanish(CrowdFirstId + (uint) i, 0, RoMapActorVanishType.OutOfSight));
                vanished.Add(CrowdFirstId + (uint) i);
            }

            SyncCrowdPresentation(vanished);

            int cleared = crowdCount;
            crowdCount = 0;
            return "cleared " + cleared;
        }

        /// <summary>
        /// The crowd's entries / vanishes / moves are applied to the world state as the drain applies those packets, then the
        /// changed actors presented as the drain presents them (SyncActorPresenterForDrain -> SyncActorIds). Left to the
        /// presenter's revision check, a sync of other actors in between (the crowd walk's) recorded the new revision and the
        /// rest of a fresh crowd was never presented.
        /// </summary>
        internal void SyncCrowdPresentation(System.Collections.Generic.List<uint> actorIds) {
            if (actorIds == null || actorIds.Count == 0) return;
            var presenter = FindFirstObjectByType<RoOnlineActorPresenter>();
            presenter?.SyncActorIds(actorIds);
        }

        static bool StraightLineWalkable(Ragnarok.Maps.RoGridSelector grid, Vector2Int from, Vector2Int step, int cells) {
            for (int i = 1; i <= cells; i++) {
                if (!grid.IsCellWalkable(from + step * i)) return false;
            }

            return true;
        }

        /// <summary>
        /// The first <paramref name="count"/> crowd players walk back and forth between their cell and one
        /// <paramref name="cells"/> away in a free straight direction: ZC_NOTIFY_MOVE records applied to the world state
        /// like the drain applies another player's move (stamped with the estimated server time), a short random pause
        /// at each end. Runs until stopped.
        /// </summary>
        IEnumerator CrowdWalk(int count, int cells, int seed) {
            int n = Math.Min(count, crowdCount);
            var ids = new uint[n];
            var home = new Vector2Int[n];
            var far = new Vector2Int[n];
            var atFar = new bool[n];
            var next = new float[n];
            var rng = new System.Random(seed);
            Ragnarok.Maps.RoGridSelector grid = world.RuntimeMapController != null ? world.RuntimeMapController.GridSelector : null;
            int walkers = 0;
            for (int i = 0; i < n; i++) {
                uint id = CrowdFirstId + (uint) i;
                if (!world.State.TryGetActor(id, out RoOnlineActorSnapshot actor) || actor.Motion == RoOnlineActorMotion.Dead) continue;
                var cell = new Vector2Int(actor.CellX, actor.CellY);
                int firstDirection = rng.Next(AdjacentOffsets.Length);
                for (int k = 0; k < AdjacentOffsets.Length; k++) {
                    Vector2Int step = AdjacentOffsets[(firstDirection + k) % AdjacentOffsets.Length];
                    Vector2Int end = cell + step * cells;
                    if (end.x <= 0 || end.y <= 0 || (grid != null && !StraightLineWalkable(grid, cell, step, cells))) continue;
                    ids[walkers] = id;
                    home[walkers] = cell;
                    far[walkers] = end;
                    next[walkers] = Now + (float) rng.NextDouble() * 1.5f;   // staggered, not all on one frame
                    walkers++;
                    break;
                }
            }

            crowdWalkers = walkers;
            Log("crowdwalk: " + walkers + " of " + n + " walking " + cells + " cells");
            int cursor = 0;
            var moved = new System.Collections.Generic.List<uint>(MaxWalkLegsPerFrame);
            var presenter = FindFirstObjectByType<RoOnlineActorPresenter>();
            while (true) {
                float now = Now;
                if (!world.TryEstimateServerTimeMilliseconds(RoPresentationClock.NowSeconds, out uint serverTime)) serverTime = 0;
                int budget = MaxWalkLegsPerFrame;
                moved.Clear();
                for (int k = 0; k < walkers && budget > 0; k++) {
                    int i = (cursor + k) % walkers;
                    if (next[i] > now) continue;
                    Vector2Int from = atFar[i] ? far[i] : home[i], to = atFar[i] ? home[i] : far[i];
                    world.State.ApplyDrainEvent(new RoMapEntryDrainEvent(
                        new RoZcNotifyMove(ids[i], new RoPacketPositionPair(from.x, from.y, to.x, to.y), serverTime)));
                    moved.Add(ids[i]);
                    atFar[i] = !atFar[i];
                    bool diagonal = from.x != to.x && from.y != to.y;
                    // the crowd walks at speed 150 (a diagonal cell costs 14/10)
                    next[i] = now + cells * (diagonal ? 0.21f : 0.15f) + 0.2f + (float) rng.NextDouble() * 0.8f;
                    budget--;
                }

                // as the drain does for these packets (RoOnlineWorldController.SyncActorPresenterForDrain): only the moved actors
                // are presented again; applied to the state alone, the new presentation revision made the presenter re-sync
                // the whole crowd every frame, a cost the game never has
                if (moved.Count > 0) {
                    if (presenter == null) presenter = FindFirstObjectByType<RoOnlineActorPresenter>();
                    presenter?.SyncActorIds(moved);
                }

                if (walkers > 0) cursor = (cursor + 1) % walkers;
                yield return null;
            }
        }

        // ---- scenarios

        /// <summary>A crowd of <paramref name="players"/>, half of it walking, an effect storm and the skill spam at once.</summary>
        IEnumerator Woe(int players, float seconds) {
            StopLoad();
            FlushPendingSpam();
            ClearCrowd();
            int radius = Math.Max(10, (int) Math.Ceiling(Math.Sqrt(players) * 0.75));
            string spawned = SpawnCrowd(players, radius);
            if (crowdCount == 0) {
                Log("woe ABORTED: no crowd spawned (" + spawned + "), nothing measured");
                scenarioRoutine = null;
                yield break;
            }

            int fxAlive = Math.Min(players, 300);
            Log($"woe: {players} players within {radius} cells ({spawned}), {players / 2} walking, fxstorm {fxAlive}, skillspam {BenchSpamPerSecond}/s, {seconds} s");
            crowdWalkRoutine = StartCoroutine(CrowdWalk(players / 2, 5, CrowdWalkSeed));
            StartFxStorm(fxAlive, 0f, 10f, false);
            StartSkillSpam(-1, BenchSpamPerSecond, false, Math.Min(radius, 10));
            if (scenarioStats == null) scenarioStats = new FrameStats();
            yield return Settle(StageSettleSeconds);
            RoClassicEffectBridge bridge = RoClassicEffectBridge.Instance;
            RoOnlineActorPresenter presenter = FindFirstObjectByType<RoOnlineActorPresenter>();
            scenarioStats.Begin();
            float end = Now + seconds;
            while (Now < end) {
                yield return null;
                scenarioStats.Sample(bridge, world.State, presenter);
            }

            StageResult result = scenarioStats.End("woe " + players);
            StopLoad();
            FlushPendingSpam();
            string cleared = ClearCrowd();
            Log("woe done: " + result.Describe() + " (crowd " + cleared + ")");
            scenarioRoutine = null;
        }

        IEnumerator Settle(float seconds) {
            float end = Now + seconds;
            while (Now < end) yield return null;
            // a fresh crowd is built at the presenter's budget (2 new actors a frame): 500 players take ~250 frames, so a
            // fixed settle measured the crowd arriving (actor builds, GC, the worst frames) instead of the fight
            RoOnlineActorPresenter presenter = FindFirstObjectByType<RoOnlineActorPresenter>();
            float start = Now, limit = Now + CrowdBuildSettleLimitSeconds;
            while (presenter != null && presenter.HasDeferredActorBuilds && Now < limit) yield return null;
            if (Now - start > 0.05f) {
                Log($"settle: the crowd finished building {Now - start:F1} s after the fixed settle" +
                    (presenter != null && presenter.HasDeferredActorBuilds ? " (limit reached, still building)" : string.Empty));
            }
        }

        const float CrowdBuildSettleLimitSeconds = 30f;

        static readonly string[] BenchStageNames = {
            "empty",
            "crowd 300 standing",
            "crowd 300, 150 walking",
            "+ fxstorm 300 alive",
            "+ skillspam 40/s",
        };

        /// <summary>
        /// Fixed-seed stages measured one after the other, each adding load to the previous one; the report goes to
        /// PerformanceCaptures/bench/bench-&lt;time&gt;.tsv / .md and its table lines to the sweep log.
        /// </summary>
        IEnumerator Bench(float stageSeconds) {
            if (world == null || !TryGetSelf(out RoOnlineActorSnapshot self)) {
                Log("bench ABORTED: no world controller or no own actor in the world state (not on a map?), nothing measured");
                scenarioRoutine = null;
                yield break;
            }

            StopLoad();
            FlushPendingSpam();
            ClearCrowd();
            if (scenarioStats == null) scenarioStats = new FrameStats();
            var results = new StageResult[BenchStageNames.Length];
            DateTime started = DateTime.Now;
            string place = $"own cell {self.CellX},{self.CellY}";
            Log($"bench: {BenchStageNames.Length} stages x {stageSeconds} s (+{StageSettleSeconds} s settle), {place}");
            for (int stage = 0; stage < BenchStageNames.Length; stage++) {
                switch (stage) {
                    case 1:
                        Log("bench crowd: " + SpawnCrowd(BenchCrowd, BenchCrowdRadius));
                        break;
                    case 2:
                        crowdWalkRoutine = StartCoroutine(CrowdWalk(BenchWalkers, 5, CrowdWalkSeed));
                        break;
                    case 3:
                        StartFxStorm(BenchFxAlive, 0f, 10f, false);
                        break;
                    case 4:
                        StartSkillSpam(-1, BenchSpamPerSecond, false, 8);
                        break;
                }

                yield return Settle(StageSettleSeconds);
                RoClassicEffectBridge bridge = RoClassicEffectBridge.Instance;
                RoOnlineActorPresenter presenter = FindFirstObjectByType<RoOnlineActorPresenter>();
                scenarioStats.Begin();
                float end = Now + stageSeconds;
                while (Now < end) {
                    yield return null;
                    scenarioStats.Sample(bridge, world.State, presenter);
                }

                results[stage] = scenarioStats.End(BenchStageNames[stage]);
                Log($"bench {stage + 1}/{BenchStageNames.Length} {BenchStageNames[stage]}: " + results[stage].Describe());
            }

            StopLoad();
            FlushPendingSpam();
            string cleared = ClearCrowd();
            RoClassicEffectBridge leftBridge = RoClassicEffectBridge.Instance;
            string left = leftBridge != null ? leftBridge.ActiveEffects + " classic effects still running" : "no bridge";
            WriteBenchReport(started, stageSeconds, place, results, cleared + ", " + left);
            scenarioRoutine = null;
        }

        void WriteBenchReport(DateTime started, float stageSeconds, string place, StageResult[] results, string cleanup) {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PerformanceCaptures", "bench"));
            string stamp = started.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string tsvPath = Path.Combine(dir, "bench-" + stamp + ".tsv");
            string mdPath = Path.Combine(dir, "bench-" + stamp + ".md");
            var tsv = new StringBuilder();
            tsv.Append("stage\tseconds\tframes\tavg_fps\tp50_ms\tp99_ms\tworst_ms\tgc_bytes_per_frame_avg\tgc_bytes_per_frame_max\t" +
                       "main_thread_ms\trender_thread_ms\tpeak_effects\tpeak_prims\tpeak_actors\tpeak_presented\tpeak_moving\n");
            var md = new StringBuilder();
            md.Append("# Client load bench ").Append(started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append("\n\n");
            md.Append("- build: ").Append(Application.isEditor ? "editor" : Debug.isDebugBuild ? "development player" : "release player")
                .Append(", Unity ").Append(Application.unityVersion).Append(", app ").Append(Application.version).Append('\n');
            md.Append("- machine: ").Append(SystemInfo.processorType).Append(" (").Append(SystemInfo.processorCount).Append(" threads), ")
                .Append(SystemInfo.graphicsDeviceName).Append(" (").Append(SystemInfo.graphicsDeviceType).Append("), ")
                .Append(SystemInfo.systemMemorySize).Append(" MB\n");
            md.Append("- screen: ").Append(Screen.width).Append('x').Append(Screen.height).Append(", quality ")
                .Append(QualitySettings.names.Length > QualitySettings.GetQualityLevel() ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "?")
                .Append(", vSync ").Append(QualitySettings.vSyncCount).Append(", target fps ").Append(Application.targetFrameRate).Append('\n');
            md.Append("- ").Append(place).Append("; stages ").Append(stageSeconds.ToString("F0", CultureInfo.InvariantCulture))
                .Append(" s each after a ").Append(StageSettleSeconds.ToString("F0", CultureInfo.InvariantCulture)).Append(" s settle; seeds crowd 1234, walk ")
                .Append(CrowdWalkSeed).Append(", fxstorm ").Append(FxStormSeed).Append(", skillspam ").Append(SpamSeed).Append('\n');
            md.Append("- sources: GC ").Append(scenarioStats.GcSource).Append(", main thread ").Append(scenarioStats.MainSource)
                .Append(", render thread ").Append(scenarioStats.RenderSource).Append('\n');
            md.Append("- cleanup: ").Append(cleanup).Append("\n\n");
            md.Append("Stages (each adds to the previous one): crowd ").Append(BenchCrowd).Append(" players within ").Append(BenchCrowdRadius)
                .Append(" cells, ").Append(BenchWalkers).Append(" of them walking 5 cells back and forth, fxstorm ").Append(BenchFxAlive)
                .Append(" classic effects alive within 10 cells, skillspam ").Append(BenchSpamPerSecond.ToString("F0", CultureInfo.InvariantCulture))
                .Append(" uses/s of the ").Append(RoLoadTestSkills.All.Length).Append("-skill pool.\n\n");
            md.Append("Load test commands (sweep console, Temp/ro-skill-sweep.cmd):\n\n");
            foreach (string command in LoadTestCommands) md.Append("- `").Append(command.Replace(" - ", "` - ")).Append('\n');
            md.Append('\n');
            string header = "| stage | fps | p50 ms | p99 ms | worst ms | GC B/frame (max) | main ms | render ms | effects / prims | actors (presented, moving) |";
            md.Append(header).Append('\n').Append("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|\n");
            Log("bench report:");
            Log(header);
            for (int i = 0; i < results.Length; i++) {
                StageResult r = results[i];
                tsv.Append(r.Name).Append('\t').Append(F(r.Seconds, "F1")).Append('\t').Append(r.Frames).Append('\t').Append(F(r.AvgFps, "F1")).Append('\t')
                    .Append(F(r.P50Ms, "F2")).Append('\t').Append(F(r.P99Ms, "F2")).Append('\t').Append(F(r.WorstMs, "F2")).Append('\t')
                    .Append(F(r.GcAvgBytes, "F0")).Append('\t').Append(r.GcMaxBytes).Append('\t').Append(F(r.MainMs, "F2")).Append('\t')
                    .Append(F(r.RenderMs, "F2")).Append('\t').Append(r.PeakEffects).Append('\t').Append(r.PeakPrims).Append('\t')
                    .Append(r.PeakActors).Append('\t').Append(r.PeakPresented).Append('\t').Append(r.PeakMoving).Append('\n');
                string row = "| " + r.Name + " | " + F(r.AvgFps, "F0") + " | " + F(r.P50Ms, "F1") + " | " + F(r.P99Ms, "F1") + " | " + F(r.WorstMs, "F1") +
                             " | " + F(r.GcAvgBytes, "F0") + " (" + r.GcMaxBytes + ") | " + F(r.MainMs, "F1") + " | " + F(r.RenderMs, "F1") + " | " +
                             r.PeakEffects + " / " + r.PeakPrims + " | " + r.PeakActors + " (" + r.PeakPresented + ", " + r.PeakMoving + ") |";
                md.Append(row).Append('\n');
                Log(row);
            }

            try {
                Directory.CreateDirectory(dir);
                File.WriteAllText(tsvPath, tsv.ToString());
                File.WriteAllText(mdPath, md.ToString());
                Log("bench done: " + mdPath + " (+ .tsv); " + cleanup);
            } catch (Exception e) {
                Log("bench: could not write the report to " + dir + ": " + e.Message);
            }
        }

        static string F(double value, string format) =>
            double.IsNaN(value) ? "-" : value.ToString(format, CultureInfo.InvariantCulture);

        // ---- measurement

        struct StageResult {
            public string Name;
            public float Seconds;
            public int Frames;
            public float AvgFps, P50Ms, P99Ms, WorstMs;
            public double GcAvgBytes;
            public long GcMaxBytes;
            /// <summary>NaN when no recorder gives it.</summary>
            public double MainMs, RenderMs;
            public int PeakEffects, PeakPrims, PeakActors, PeakPresented, PeakMoving;

            public string Describe() =>
                Name == null
                    ? string.Empty
                    : $"{AvgFps:F0} fps average over {Frames} frames, p50 {P50Ms:F1} ms, p99 {P99Ms:F1} ms, worst {WorstMs:F0} ms, " +
                      $"GC {GcAvgBytes:F0} B/frame (max {GcMaxBytes}), main {F(MainMs, "F1")} ms, render {F(RenderMs, "F1")} ms, " +
                      $"peak {PeakEffects} effects / {PeakPrims} prims, {PeakActors} actors ({PeakPresented} presented, {PeakMoving} moving)";
        }

        /// <summary>
        /// Per-frame measurement without allocations: frame times into a preallocated array (percentiles sorted at the
        /// end), GC bytes of the frame ("GC Allocated In Frame", else the managed heap's growth), main / render thread
        /// times when a recorder gives them, and the peaks of the classic engine and the actor counts.
        /// </summary>
        sealed class FrameStats : IDisposable {
            const int Capacity = 1 << 15;
            readonly float[] frameMs = new float[Capacity];
            ProfilerRecorder gcRecorder, mainRecorder, renderRecorder;
            bool recordersStarted;
            int frames;
            double sumMs, gcSum, mainSum, renderSum;
            int mainSamples, renderSamples;
            long gcMax, lastHeap;
            float worstMs, startTime;
            int peakEffects, peakPrims, peakActors, peakPresented, peakMoving;

            public string GcSource { get; private set; } = "-";
            public string MainSource { get; private set; } = "-";
            public string RenderSource { get; private set; } = "-";

            public void Begin() {
                if (!recordersStarted) StartRecorders();
                frames = 0;
                sumMs = gcSum = mainSum = renderSum = 0d;
                mainSamples = renderSamples = 0;
                gcMax = 0;
                worstMs = 0f;
                peakEffects = peakPrims = peakActors = peakPresented = peakMoving = 0;
                lastHeap = GC.GetTotalMemory(false);
                startTime = Time.realtimeSinceStartup;
            }

            void StartRecorders() {
                recordersStarted = true;
                gcRecorder = TryStart(ProfilerCategory.Memory, "GC Allocated In Frame");
                GcSource = gcRecorder.Valid ? "GC Allocated In Frame" : "GC.GetTotalMemory growth (approximate)";
                mainRecorder = TryStart(ProfilerCategory.Internal, "Main Thread");
                MainSource = "Main Thread";
                if (!mainRecorder.Valid) {
                    mainRecorder = TryStart(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
                    MainSource = mainRecorder.Valid ? "CPU Main Thread Frame Time" : "-";
                }

                renderRecorder = TryStart(ProfilerCategory.Render, "Render Thread");
                RenderSource = "Render Thread";
                if (!renderRecorder.Valid) renderRecorder = TryStart(ProfilerCategory.Internal, "Render Thread");
                if (!renderRecorder.Valid) {
                    renderRecorder = TryStart(ProfilerCategory.Internal, "CPU Render Thread Frame Time");
                    RenderSource = renderRecorder.Valid ? "CPU Render Thread Frame Time" : "-";
                }
            }

            static ProfilerRecorder TryStart(ProfilerCategory category, string name) {
                try {
                    ProfilerRecorder recorder = ProfilerRecorder.StartNew(category, name);
                    if (recorder.Valid) return recorder;
                    recorder.Dispose();
                } catch (Exception) {
                    // not in this build
                }

                return default;
            }

            public void Sample(RoClassicEffectBridge bridge, RoOnlineWorldState state, RoOnlineActorPresenter presenter) {
                float ms = Time.unscaledDeltaTime * 1000f;
                if (frames < Capacity) frameMs[frames] = ms;
                frames++;
                sumMs += ms;
                if (ms > worstMs) worstMs = ms;

                long gcBytes;
                if (gcRecorder.Valid) {
                    gcBytes = gcRecorder.LastValue;
                } else {
                    long heap = GC.GetTotalMemory(false);
                    gcBytes = Math.Max(0L, heap - lastHeap);
                    lastHeap = heap;
                }

                gcSum += gcBytes;
                if (gcBytes > gcMax) gcMax = gcBytes;
                if (mainRecorder.Valid) {
                    long ns = mainRecorder.LastValue;
                    if (ns > 0) {
                        mainSum += ns * 1e-6;
                        mainSamples++;
                    }
                }

                if (renderRecorder.Valid) {
                    long ns = renderRecorder.LastValue;
                    if (ns > 0) {
                        renderSum += ns * 1e-6;
                        renderSamples++;
                    }
                }

                if (bridge != null) {
                    peakEffects = Math.Max(peakEffects, bridge.ActiveEffects);
                    peakPrims = Math.Max(peakPrims, bridge.ActivePrims);
                }

                if (state != null) {
                    peakActors = Math.Max(peakActors, state.ActorCount);
                    peakMoving = Math.Max(peakMoving, state.MovingActorCount);
                }

                if (presenter != null) peakPresented = Math.Max(peakPresented, presenter.PresentedActorCount);
            }

            public StageResult End(string name) {
                float seconds = Time.realtimeSinceStartup - startTime;
                int stored = Math.Min(frames, Capacity);
                Array.Sort(frameMs, 0, stored);
                return new StageResult {
                    Name = name,
                    Seconds = seconds,
                    Frames = frames,
                    AvgFps = seconds > 0f ? frames / seconds : 0f,
                    P50Ms = stored > 0 ? frameMs[stored / 2] : 0f,
                    P99Ms = stored > 0 ? frameMs[Math.Min(stored - 1, (int) (stored * 0.99f))] : 0f,
                    WorstMs = worstMs,
                    GcAvgBytes = frames > 0 ? gcSum / frames : 0d,
                    GcMaxBytes = gcMax,
                    MainMs = mainSamples > 0 ? mainSum / mainSamples : double.NaN,
                    RenderMs = renderSamples > 0 ? renderSum / renderSamples : double.NaN,
                    PeakEffects = peakEffects,
                    PeakPrims = peakPrims,
                    PeakActors = peakActors,
                    PeakPresented = peakPresented,
                    PeakMoving = peakMoving,
                };
            }

            public void Dispose() {
                if (gcRecorder.Valid) gcRecorder.Dispose();
                if (mainRecorder.Valid) mainRecorder.Dispose();
                if (renderRecorder.Valid) renderRecorder.Dispose();
                recordersStarted = false;
            }
        }
    }
}
