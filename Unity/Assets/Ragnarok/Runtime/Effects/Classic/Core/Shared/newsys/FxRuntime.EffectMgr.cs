// Synced from reference/port/src/RoClassicFx/Shared/newsys/FxRuntime.EffectMgr.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The client's second, newer effect engine (package newsys): CEffectMgr (DAT_015127bc) with the C++
    /// CEffect classes it creates by effect id (registry FUN_00a4ded0 → FUN_00a62560), launched through
    /// FUN_00a4d090 before the old CRagEffect path, ticked by FUN_00a63060 once per logic tick (ahead of the
    /// world's CRagEffect pass, CGameMode::OnUpdate 0xc020a0) and drawn by FUN_00a63170 after the world's
    /// render objects (CView::OnRender 0xa098a0).
    ///
    /// Manager object (0x28 bytes, vtable 0xfe1578): +4 byte ready, +8/+0xc registry std::map&lt;int, allocator*&gt;
    /// (node 0x18), +0x10/+0x14 std::map&lt;owner AID, std::list&lt;CEffect*&gt;&gt; (node 0x1c), +0x18/+0x1c the
    /// "graveyard" std::list&lt;CEffect*&gt; (dead effects, destroyed only on a map change by FUN_00a4db00),
    /// +0x20/+0x24 std::map&lt;int, std::vector&lt;CEffect*&gt;&gt; (node 0x20, a handful of special ids).
    /// Effects are keyed by account ids, never by object pointers: the owner (+0x20) and target (+0x24) are
    /// resolved with FUN_00a64480 every time they are needed.
    /// </summary>
    public sealed partial class FxRuntime
    {
        public const uint VtCEffectMgr = 0x00fe1578;
        public const uint VtCAllocator = 0x00fe16e8;

        /// <summary>DAT_015127bc — the CEffectMgr (0 until <see cref="EnsureNewEffectSystem"/>).</summary>
        public uint EffectMgr;

        /// <summary>
        /// Creates CEffectMgr (FUN_00a4d5e0) and CEffectMemoryMgr (FUN_00aa9b60) in FxMemory and registers the
        /// ported effect ids with their allocators (the subset of FUN_00a4ded0 this port implements).
        /// </summary>
        public void EnsureNewEffectSystem()
        {
            if (EffectMgr != 0) return;
            EnsureEffectMemoryMgr();
            uint mgr = M.Alloc(0x28);
            M.U32(mgr, VtCEffectMgr);
            M.U32(mgr + 8, TreeNewHead(0x18));
            M.U32(mgr + 0xc, 0);
            M.U32(mgr + 0x10, TreeNewHead(0x1c));
            M.U32(mgr + 0x14, 0);
            uint list = M.Alloc(0xc);
            M.U32(list, list);
            M.U32(list + 4, list);
            M.U32(mgr + 0x18, list);
            M.U32(mgr + 0x1c, 0);
            M.U32(mgr + 0x20, TreeNewHead(0x20));
            M.U32(mgr + 0x24, 0);
            EffectMgr = mgr;
            foreach (var (id, allocatorVtable) in CEffect.Registry) RegisterEffect_a62560(id, allocatorVtable);
            M.U8(mgr + 4, 1);
        }

        /// <summary>
        /// FUN_00a62560 — registry insert (effect id → allocator object, a 4-byte object holding the allocator's
        /// vtable, operator new(4, nothrow)). Returns false when the id is already registered.
        /// </summary>
        bool RegisterEffect_a62560(int id, uint allocatorVtable)
        {
            uint allocator = M.Alloc(4);
            M.U32(allocator, allocatorVtable);
            uint tree = EffectMgr + 8;
            TreeFindLowerBound(tree, id, out uint parent, out bool left, out uint bound);
            if (M.U8(bound + 0xd) == 0 && M.I32(bound + 0x10) <= id) return false;
            uint head = M.U32(tree);
            uint node = M.Alloc(0x18);
            M.I32(node + 0x10, id);
            M.U32(node + 0x14, allocator);
            M.U32(node, head);
            M.U32(node + 4, head);
            M.U32(node + 8, head);
            M.U16(node + 0xc, 0);
            TreeInsertNode(tree, parent, left, node);
            return true;
        }

        /// <summary>True when <paramref name="efId"/> is in the (ported part of the) new-system registry.</summary>
        public static bool IsNewSystemEffect(int efId) => CEffect.AllocatorVtableFor(efId) != 0;

        /// <summary>
        /// FUN_00a4d090 (thiscall on CEffectMgr, 9 args, ret 0x24) — creates effect <paramref name="efId"/> for the
        /// actor with account id <paramref name="ownerAid"/> (0 = none: the effect stays at the given position).
        /// <paramref name="x"/>/<paramref name="y"/>/<paramref name="z"/> is the start position (callers pass 0 when
        /// an owner is given: the first tick moves the effect onto its owner), <paramref name="targetAid"/> the
        /// actor the effect aims at (-1 = none; its position is copied to +0x14..+0x1c), <paramref name="param7"/>
        /// (-1 = keep) goes to +0xb0, <paramref name="reuse"/> returns an existing effect with the same id and key
        /// instead of creating one, <paramref name="key"/> &gt; 0 makes the effect unique per (owner, id, key).
        /// Returns the effect (or the existing one) or 0.
        /// </summary>
        public uint LaunchNewEffect_a4d090(uint ownerAid, int efId, float x, float y, float z, uint targetAid,
            int param7 = -1, bool reuse = false, int key = 0)
        {
            uint mgr = EffectMgr;
            if (mgr == 0) return 0;
            if ((efId < 0 || efId > 60000) && !NewEffectIdOutOfRangeAllowed(efId)) return 0;
            if (efId == 0x3d2 || efId == 0x3d3)
            {
                if (FindEffectOfOwner_a4dd90(ownerAid, 200) != 0) return 0;
            }
            if (ownerAid != 0)
            {
                uint existing = FindEffect_a4de00(ownerAid, key, efId);
                if (existing != 0)
                {
                    if (key > 0) return 0;
                    if (M.U8(existing + 0xc0) != 0) return existing;
                    if (reuse) return existing;
                }
            }
            TreeFindLowerBound(mgr + 8, efId, out _, out _, out uint bound);
            if (M.U8(bound + 0xd) != 0 || efId < M.I32(bound + 0x10)) return 0;
            uint effect = CEffect.Allocate(this, M.U32(M.U32(bound + 0x14)));
            if (effect == 0) return 0;
            var e = new CEffect(M, effect);
            M.U32(effect + 0x20, ownerAid);                   // FUN_00a75fe0
            e.SetTarget_a75ff0(this, targetAid);
            M.F32(effect + 8, x);
            M.F32(effect + 0xc, y);
            M.F32(effect + 0x10, z);
            e.Vf24SetId(this, efId);
            if (param7 != -1) M.I32(effect + 0xb0, param7);
            if (key > 0)
            {
                M.I32(effect + 0xc4, key);
                M.U8(effect + 0xc0, 1);
            }
            e.Vf01Init(this);
            e.Vf12Start(this);
            RegisterEffect_a4c7d0(ownerAid, effect);
            return effect;
        }

        /// <summary>The out-of-range id switch of FUN_00a4d090 (ids &lt; 0 or &gt; 60000 that still get an effect).</summary>
        static bool NewEffectIdOutOfRangeAllowed(int efId) => false;

        /// <summary>FUN_00a4dd90 — the first effect with id <paramref name="efId"/> in the owner's list, or 0.</summary>
        public uint FindEffectOfOwner_a4dd90(uint ownerAid, int efId)
        {
            uint node = TreeFind(EffectMgr + 0x10, (int)ownerAid);
            if (node == 0) return 0;
            uint list = M.U32(node + 0x14);
            for (uint n = M.U32(list); n != list; n = M.U32(n))
                if (M.I32(M.U32(n + 8) + 4) == efId) return M.U32(n + 8);
            return 0;
        }

        /// <summary>FUN_00a4de00 — the owner's effect with id <paramref name="efId"/> and key <paramref name="key"/>, or 0.</summary>
        uint FindEffect_a4de00(uint ownerAid, int key, int efId)
        {
            uint tree = EffectMgr + 0x10;
            TreeFindLowerBound(tree, (int)ownerAid, out _, out _, out uint bound);
            if (M.U8(bound + 0xd) != 0 || (int)ownerAid < M.I32(bound + 0x10) || M.U32(tree) == bound) return 0;
            uint list = M.U32(bound + 0x14);
            for (uint n = M.U32(list); n != list; n = M.U32(n))
            {
                uint e = M.U32(n + 8);
                if (M.I32(e + 4) == efId && M.I32(e + 0xc4) == key) return e;
            }
            return 0;
        }

        /// <summary>
        /// FUN_00a46a80 — std::map&lt;int, std::list&gt;::operator[] on the owner map: the list (+0x14 of the node),
        /// inserting a node (0x1c) with an empty list (sentinel 0xc) when the key is new.
        /// </summary>
        uint OwnerList_a46a80(uint ownerAid)
        {
            uint tree = EffectMgr + 0x10;
            TreeFindLowerBound(tree, (int)ownerAid, out uint parent, out bool left, out uint bound);
            if (M.U8(bound + 0xd) == 0 && (int)ownerAid >= M.I32(bound + 0x10)) return bound + 0x14;
            uint head = M.U32(tree);
            uint node = M.Alloc(0x1c);
            M.I32(node + 0x10, (int)ownerAid);
            M.U32(node + 0x14, 0);
            M.U32(node + 0x18, 0);
            uint sentinel = M.Alloc(0xc);
            M.U32(sentinel, sentinel);
            M.U32(sentinel + 4, sentinel);
            M.U32(node + 0x14, sentinel);
            M.U32(node, head);
            M.U32(node + 4, head);
            M.U32(node + 8, head);
            M.U16(node + 0xc, 0);
            TreeInsertNode(tree, parent, left, node);
            return node + 0x14;
        }

        /// <summary>
        /// FUN_00a4c7d0 — appends the effect to its owner's list; ids 0x3ce, 0x401, 0x40f, 0x431, 0x584 and 0x927
        /// are also kept in the per-id vectors at +0x20 (package mapfx).
        /// </summary>
        void RegisterEffect_a4c7d0(uint ownerAid, uint effect)
        {
            ListPushBack(OwnerList_a46a80(ownerAid), effect);
            int id = M.I32(effect + 4);
            if (id == 0x431 || id == 0x3ce || id == 0x401 || id == 0x40f || id == 0x584 || id == 0x927)
                PerIdVectorPushBack_a4c7d0(id, effect);                        // Shared/mapfx/FxRuntime.Emitter.cs
        }

        /// <summary>
        /// FUN_00a4d9e0 — retires an effect: appends it to the graveyard list (+0x18); with
        /// <paramref name="unlink"/> it is also removed from its owner's list (the update loop unlinks it itself).
        /// </summary>
        public void KillEffect_a4d9e0(uint effect, bool unlink)
        {
            if (effect == 0) return;
            ListPushBack(EffectMgr + 0x18, effect);
            if (!unlink) return;
            uint owner = M.U32(effect + 0x20);
            uint tree = EffectMgr + 0x10;
            TreeFindLowerBound(tree, (int)owner, out _, out _, out uint bound);
            if (M.U8(bound + 0xd) != 0 || (int)owner < M.I32(bound + 0x10) || bound == M.U32(tree)) return;
            uint list = OwnerList_a46a80(owner);
            uint head = M.U32(list);
            uint chainFirst = 0, chainLast = 0;
            for (uint n = M.U32(head), next; n != head; n = next)
            {
                next = M.U32(n);
                if (M.U32(n + 8) != effect) continue;
                M.U32(list + 4, M.U32(list + 4) - 1);
                uint prev = M.U32(n + 4);
                M.U32(n, 0);
                M.U32(prev, next);
                M.U32(next + 4, prev);
                if (chainLast == 0) chainFirst = n; else M.U32(chainLast, n);
                chainLast = n;
            }
            for (uint n = chainFirst; n != 0;)
            {
                uint next = M.U32(n);
                M.Free(n);
                n = next;
            }
        }

        /// <summary>
        /// FUN_00a63060 — one logic tick of every live effect, owner by owner in account-id order: first the
        /// effects whose vf06 reports them finished move to the graveyard (their list nodes are released after
        /// the scan), then each remaining one ticks (FUN_00a76030) with the owner's account id.
        /// Called once per logic tick (not on frames without a tick).
        /// </summary>
        public void UpdateNewEffects_a63060()
        {
            uint mgr = EffectMgr;
            if (mgr == 0) return;
            uint head = M.U32(mgr + 0x10);
            for (uint node = M.U32(head); node != head; node = TreeNext(node))
            {
                uint aid = M.U32(node + 0x10);
                uint listObj = node + 0x14;
                uint lh = M.U32(listObj);
                uint chainFirst = 0, chainLast = 0;
                for (uint n = M.U32(lh), next; n != lh; n = next)
                {
                    next = M.U32(n);
                    var e = new CEffect(M, M.U32(n + 8));
                    if (e.Vf06IsAlive(this)) continue;
                    KillEffect_a4d9e0(e.A, false);
                    M.U32(listObj + 4, M.U32(listObj + 4) - 1);
                    uint prev = M.U32(n + 4);
                    M.U32(n, 0);
                    M.U32(prev, next);
                    M.U32(next + 4, prev);
                    if (chainLast == 0) chainFirst = n; else M.U32(chainLast, n);
                    chainLast = n;
                }
                for (uint n = chainFirst; n != 0;)
                {
                    uint next = M.U32(n);
                    M.Free(n);
                    n = next;
                }
                lh = M.U32(listObj);
                for (uint n = M.U32(lh); n != lh; n = M.U32(n)) new CEffect(M, M.U32(n + 8)).Tick_a76030(this, aid);
            }
        }

        /// <summary>
        /// FUN_00a63170 — draws every live effect (FUN_00a75ef0: effects enabled or forced, vf28 visible, in
        /// range → vf04), owner by owner. FUN_00d2b8c0 (the render gate) always returns 1 in this client.
        /// </summary>
        public void RenderNewEffects_a63170()
        {
            uint mgr = EffectMgr;
            if (mgr == 0) return;
            uint head = M.U32(mgr + 0x10);
            for (uint node = M.U32(head); node != head; node = TreeNext(node))
            {
                uint lh = M.U32(node + 0x14);
                for (uint n = M.U32(lh); n != lh; n = M.U32(n)) new CEffect(M, M.U32(n + 8)).Render_a75ef0(this);
            }
        }

        /// <summary>
        /// The graveyard half of FUN_00a4db00 (map change): every retired effect releases its particles (vf05) and
        /// is destroyed (vf25(1)), then the list is emptied. The client only does this on a map change, so retired
        /// effects (and their pooled particles) accumulate for the whole map; a host with a bounded heap calls this
        /// after each tick instead — retired effects are never read again, so nothing visible changes.
        /// </summary>
        public void DestroyDeadEffects_a4db00()
        {
            uint mgr = EffectMgr;
            if (mgr == 0) return;
            uint list = M.U32(mgr + 0x18);
            for (uint n = M.U32(list); n != list; n = M.U32(n))
            {
                var e = new CEffect(M, M.U32(n + 8));
                e.Vf05ReleaseParticles(this);
                if (e.A != 0) e.Vf25Destroy(this, true);
            }
            M.U32(M.U32(list + 4), 0);
            for (uint n = M.U32(list); n != 0;)
            {
                uint next = M.U32(n);
                M.Free(n);
                n = next;
            }
            M.U32(list, list);
            M.U32(list + 4, list);
            M.U32(mgr + 0x1c, 0);
        }

        /// <summary>
        /// FUN_00a4db00 — the map-change reset: every owner's effects are retired (FUN_00a4c960, owners in AID
        /// order), the owner map and the per-id map are erased (FUN_00a41f70 / FUN_00a42030), then the graveyard is
        /// destroyed (<see cref="DestroyDeadEffects_a4db00"/>).
        /// </summary>
        public void ClearNewEffects_a4db00()
        {
            uint mgr = EffectMgr;
            if (mgr == 0) return;
            uint head = M.U32(mgr + 0x10);
            for (uint node = M.U32(head); node != head; node = TreeNext(node)) KillEffectsOfOwner_a4c960(M.U32(node + 0x10));
            EraseOwnerTree_a41f70(M.U32(head + 4));
            M.U32(head + 4, head);
            M.U32(head, head);
            M.U32(head + 8, head);
            M.U32(mgr + 0x14, 0);
            KillEffectsOfOwner_a4c960(0);
            head = M.U32(mgr + 0x20);
            EraseIdTree_a42030(M.U32(head + 4));
            M.U32(head + 4, head);
            M.U32(head, head);
            M.U32(head + 8, head);
            M.U32(mgr + 0x24, 0);
            DestroyDeadEffects_a4db00();
        }

        /// <summary>FUN_00a41f70 — erase of an owner-map subtree: right subtree first, then the node (its list sentinel, then the node), then left.</summary>
        void EraseOwnerTree_a41f70(uint node)
        {
            while (M.U8(node + 0xd) == 0)
            {
                EraseOwnerTree_a41f70(M.U32(node + 8));
                uint left = M.U32(node);
                uint sentinel = M.U32(node + 0x14);
                M.U32(M.U32(sentinel + 4), 0);
                for (uint n = M.U32(sentinel); n != 0;)
                {
                    uint next = M.U32(n);
                    M.Free(n);
                    n = next;
                }
                M.Free(sentinel);
                M.Free(node);
                node = left;
            }
        }

        /// <summary>FUN_00a42030 — erase of a per-id-map subtree (each node's std::vector released by FUN_00a46780).</summary>
        void EraseIdTree_a42030(uint node)
        {
            while (M.U8(node + 0xd) == 0)
            {
                EraseIdTree_a42030(M.U32(node + 8));
                uint left = M.U32(node);
                uint vec = node + 0x14;
                uint begin = M.U32(vec);
                if (begin != 0)
                {
                    StdDeallocate(begin, (M.U32(vec + 8) - begin) & 0xfffffffcu);
                    M.U32(vec, 0);
                    M.U32(vec + 4, 0);
                    M.U32(vec + 8, 0);
                }
                M.Free(node);
                node = left;
            }
        }

        /// <summary>
        /// FUN_00a4c960 — retires every effect of one owner (appended to the graveyard in list order) and empties
        /// the owner's list (its node stays in the map). The client runs it for all owners on a map change.
        /// </summary>
        public void KillEffectsOfOwner_a4c960(uint ownerAid)
        {
            uint mgr = EffectMgr;
            if (mgr == 0) return;
            uint tree = mgr + 0x10;
            TreeFindLowerBound(tree, (int)ownerAid, out _, out _, out uint bound);
            if (M.U8(bound + 0xd) != 0 || (int)ownerAid < M.I32(bound + 0x10) || bound == M.U32(tree)) return;
            uint lh = M.U32(bound + 0x14);
            for (uint n = M.U32(lh); n != lh; n = M.U32(n)) ListPushBack(mgr + 0x18, M.U32(n + 8));
            M.U32(M.U32(lh + 4), 0);
            for (uint n = M.U32(lh); n != 0;)
            {
                uint next = M.U32(n);
                M.Free(n);
                n = next;
            }
            lh = M.U32(bound + 0x14);
            M.U32(lh, lh);
            M.U32(lh + 4, lh);
            M.U32(bound + 0x18, 0);
        }

        /// <summary>Number of live (registered, not retired) new-system effects.</summary>
        public int LiveNewEffectCount
        {
            get
            {
                if (EffectMgr == 0) return 0;
                int count = 0;
                uint head = M.U32(EffectMgr + 0x10);
                for (uint node = M.U32(head); node != head; node = TreeNext(node)) count += M.I32(node + 0x18);
                return count;
            }
        }

        /// <summary>True while <paramref name="effect"/> is in one of the owner lists (not yet retired).</summary>
        public bool IsNewEffectLive(uint effect)
        {
            if (EffectMgr == 0 || effect == 0) return false;
            // fast path: a live effect sits in the list of the owner it names (+0x20, set before FUN_00a4c7d0 registers
            // it), so look there first; only a miss (a retired effect, or a stale handle) needs the full scan below,
            // which gives the answer exactly as before. Hosts poll every tracked effect each frame.
            if (M.Contains(effect + 0x20, 4))
            {
                uint owner = TreeFind(EffectMgr + 0x10, M.I32(effect + 0x20));
                if (owner != 0)
                {
                    uint list = M.U32(owner + 0x14);
                    for (uint n = M.U32(list); n != list; n = M.U32(n))
                        if (M.U32(n + 8) == effect) return true;
                }
            }
            uint head = M.U32(EffectMgr + 0x10);
            for (uint node = M.U32(head); node != head; node = TreeNext(node))
            {
                uint lh = M.U32(node + 0x14);
                for (uint n = M.U32(lh); n != lh; n = M.U32(n))
                    if (M.U32(n + 8) == effect) return true;
            }
            return false;
        }

        // ---- actors as the new system sees them ------------------------------------------------

        /// <summary>
        /// FUN_00a64480 (cdecl) — account id → game object: the local player (DAT_0154f2b4) is the world's player
        /// object (world +0x2c), anyone else CWorld::GetGameActorByAID (0x9f8aa0), then the world's skill objects
        /// (FUN_009f8c80, matched on +0x410).
        /// </summary>
        public uint ActorByAid_a64480(uint aid)
        {
            uint world = M.U32(Mode + 0xcc);
            if (PlayerAid == aid) return M.U32(world + 0x2c);
            uint actor = FindActorByAid(aid);
            if (actor == 0) actor = FindSkillObjectById_9f8c80(aid);
            return actor;
        }

        /// <summary>Override for <see cref="FindSkillObjectById_9f8c80"/>.</summary>
        public Func<uint, uint> FindSkillObjectByIdImpl;

        /// <summary>
        /// FUN_009f8c80 (thiscall on the world, RET 4) shim: the skill object (CSkill, id at +0x410) in the world's
        /// list at +0x20, or 0. A world without that list has none.
        /// </summary>
        public uint FindSkillObjectById_9f8c80(uint id)
        {
            if (FindSkillObjectByIdImpl != null) return FindSkillObjectByIdImpl(id);
            uint head = M.U32(World + 0x20);
            if (head == 0) return 0;
            for (uint node = M.U32(head); node != head; node = M.U32(node))
            {
                uint obj = M.U32(node + 8);
                if (M.U32(obj + 0x410) == id) return obj;
            }
            return 0;
        }

        /// <summary>
        /// FUN_00a644c0 — the local player's position (world +0x2c +0x10..); without a player object the client
        /// converts its cell (DAT_0154ebb8/bc, FUN_00bf6710) and takes the ground height (not modelled: the port
        /// always has a player object).
        /// </summary>
        public void PlayerPosition_a644c0(out float x, out float y, out float z)
        {
            uint player = M.U32(M.U32(Mode + 0xcc) + 0x2c);
            if (player == 0) throw new InvalidOperationException("FUN_00a644c0 without a player object is not ported");
            x = M.F32(player + 0x10);
            y = M.F32(player + 0x14);
            z = M.F32(player + 0x18);
        }

        // ---- client services of the new system -----------------------------------------------

        /// <summary>
        /// Lua <c>IsForceRenderEffect(id)</c> (called by CEffect::SetId through FUN_00a26ed0 on DAT_01553314): effects
        /// that render even with /effect off. Client data (lua files); default: none.
        /// </summary>
        public Func<int, bool> IsForceRenderEffectImpl = id => false;

        /// <summary>DAT_01552d74 — effects enabled (/effect). Effects not forced by Lua draw only when set.</summary>
        public bool EffectsEnabled = true;

        /// <summary>
        /// FUN_00d1e3b0 (thiscall on CSession 0x154dd00) shim — whether <paramref name="aid"/> is in the party list
        /// (and, with <paramref name="online"/>, not flagged at +0x48). Client session state; default: no.
        /// </summary>
        public Func<uint, bool, bool> IsPartyMemberImpl = (aid, online) => false;

        /// <summary>
        /// The map name the client keeps in CGameMode +0x68 (e.g. "prontera.rsw"); the heal effect reads it (FUN_00a8f6f0).
        /// Written into the mode object as a C string.
        /// </summary>
        public void SetMapName(string rswName)
        {
            string s = rswName ?? "";
            if (s.Length > 0x3f) s = s.Substring(0, 0x3f);
            for (int i = 0; i < 0x40; i++) M.U8(Mode + 0x68 + (uint)i, i < s.Length ? (byte)s[i] : (byte)0);
        }
    }
}
