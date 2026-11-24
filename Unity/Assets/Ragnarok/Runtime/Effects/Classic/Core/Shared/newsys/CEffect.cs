// Synced from reference/port/src/RoClassicFx/Shared/newsys/CEffect.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// View of a new-system effect object (C++ class CEffect, vtable 0xfe25d8, 29 virtual methods) in FxMemory.
    /// Base layout (ctor FUN_00a75740): +4 effect id, +8/+0xc/+0x10 position (the owner's, refreshed every tick),
    /// +0x14/+0x18/+0x1c target position (copied at launch), +0x20 owner AID, +0x24 target AID, +0x28 tick,
    /// +0x2c duration in ticks (60), +0x30 skill level (1), +0x34 byte "out of range" (vf02), +0x38 range (130),
    /// +0xb0 launch parameter (-1), +0xb4..+0xbc std::vector of timed sounds (0x24-byte entries: type, tick,
    /// y, std::string wav), +0xc0 byte keyed, +0xc2 byte forced render (Lua IsForceRenderEffect), +0xc4 key,
    /// +0xc8 byte "attached" (skips the owner-visibility checks of vf28).
    /// Virtual methods are dispatched on the vtable of the ported classes.
    /// </summary>
    public readonly partial struct CEffect
    {
        public readonly FxMemory M;
        public readonly uint A;

        public CEffect(FxMemory m, uint a) { M = m; A = a; }

        public const uint VtCEffect = 0x00fe25d8;

        public uint Vtable => M.U32(A);
        public int Id => M.I32(A + 4);
        public float X => M.F32(A + 8);
        public float Y => M.F32(A + 0xc);
        public float Z => M.F32(A + 0x10);
        public uint OwnerAid => M.U32(A + 0x20);
        public uint TargetAid => M.U32(A + 0x24);
        public int Tick => M.I32(A + 0x28);
        public int Duration => M.I32(A + 0x2c);

        /// <summary>Skill level (+0x30; the throw effects' item count), set by callers after the launch.</summary>
        public int Level { get => M.I32(A + 0x30); set => M.I32(A + 0x30, value); }

        /// <summary>+0xc8: attached to its owner (FUN_00bcf990 sets it for effects launched on an actor's own behalf).</summary>
        public bool Attached { get => M.U8(A + 0xc8) != 0; set => M.U8(A + 0xc8, value ? (byte)1 : (byte)0); }

        // ---- registry ----------------------------------------------------------------------------

        static Dictionary<int, uint> registryCache;
        static Dictionary<int, uint> registry => registryCache ?? (registryCache = BuildRegistry());   // lazy: the id lists live in other partial files

        /// <summary>(effect id, allocator vtable) of the ported part of FUN_00a4ded0, in id order.</summary>
        public static IEnumerable<(int Id, uint AllocatorVtable)> Registry
        {
            get
            {
                var ids = new List<int>(registry.Keys);
                ids.Sort();
                foreach (int id in ids) yield return (id, registry[id]);
            }
        }

        static Dictionary<int, uint> BuildRegistry()
        {
            var r = new Dictionary<int, uint>();
            foreach (int id in WindIds) r[id] = VtAllocWind;                 // package mapfx-wind
            foreach (int id in MagicFloorIds) r[id] = VtAllocMagicFloor;     // package mapfx-wind
            foreach (int id in HealIds) r[id] = VtAllocHeal;
            foreach (int id in ThrowItemIds) r[id] = VtAllocThrowItem;
            foreach (int id in StormKickIds) r[id] = VtAllocStormKick;
            foreach (int id in EZ2STRIds) r[id] = VtAllocEZ2STR;
            foreach (int id in Level99Ids) r[id] = VtAllocLevel99;           // package aura-vanish
            foreach (int id in Level99Orb1Ids) r[id] = VtAllocLevel99Orb1;
            foreach (int id in Level99Orb2Ids) r[id] = VtAllocLevel99Orb2;
            foreach (int id in EmitterIds) r[id] = VtAllocEmitter;           // package mapfx
            foreach (int id in SmokeIds) r[id] = VtAllocSmoke;               // package mapfx
            foreach (int id in AnimatedEmitterIds) r[id] = VtAllocAnimatedEmitter;   // package mapfx-emitters
            foreach (int id in EvilsPawIds) r[id] = VtAllocEvilsPaw;                 // package mapfx-emitters
            return r;
        }

        /// <summary>The allocator vtable registered for <paramref name="efId"/> (0 = not a ported new-system id).</summary>
        public static uint AllocatorVtableFor(int efId) => registry.TryGetValue(efId, out uint v) ? v : 0u;

        /// <summary>
        /// The allocator's vf01 (operator new(size, nothrow) + the class constructor) for the allocator vtable
        /// found in the registry.
        /// </summary>
        public static uint Allocate(FxRuntime rt, uint allocatorVtable)
        {
            switch (allocatorVtable)
            {
                case VtAllocWind: return ConstructWind_6aacf0(rt, rt.M.Alloc(WindSize));                                         // vf01 0xa49b90 (mapfx-wind)
                case VtAllocMagicFloor: return ConstructMagicFloor_a6fe80(rt, rt.M.Alloc(MagicFloorSize));                       // vf01 0xa48620 (mapfx-wind)
                case VtAllocHeal: return ConstructHeal_a8ea70(rt, rt.M.Alloc(HealSize));
                case VtAllocThrowItem: return ConstructThrowItem_aa4120(rt, rt.M.Alloc(ThrowItemSize));
                case VtAllocStormKick: return ConstructStormKick_aa2950(rt, rt.M.Alloc(StormKickSize));
                case VtAllocEZ2STR: return ConstructEZ2STR_a793f0(rt, rt.M.Alloc(EZ2STRSize));
                case VtAllocLevel99: return ConstructLevel99_a91800(rt, rt.M.Alloc(Level99Size));
                case VtAllocLevel99Orb1: return ConstructLevel99Orb(rt, rt.M.Alloc(Level99OrbSize), 976, VtCLevel99Orb1Effect);   // FUN_00a92970
                case VtAllocLevel99Orb2: return ConstructLevel99Orb(rt, rt.M.Alloc(Level99OrbSize), 977, VtCLevel99Orb2Effect);   // FUN_00a93560
                case VtAllocEmitter: return ConstructEmitter_a77740(rt, rt.M.Alloc(EmitterSize));                                  // vf01 0xa47870
                case VtAllocSmoke: return ConstructSmoke_a9db40(rt, rt.M.Alloc(SmokeSize));                                        // vf01 0xa490f0
                case VtAllocAnimatedEmitter: return ConstructAnimatedEmitter_676eb0(rt, rt.M.Alloc(AnimatedEmitterSize));          // vf01 0xa46cf0
                case VtAllocEvilsPaw: return ConstructEvilsPaw_67ba10(rt, rt.M.Alloc(EvilsPawSize));                               // vf01 0xa47af0
                default: throw new NotSupportedException($"allocator vtable 0x{allocatorVtable:x8} is not ported");
            }
        }

        /// <summary>CEffect::CEffect (FUN_00a75740) with id -1.</summary>
        static void ConstructBase_a75740(FxMemory M, uint a, int id)
        {
            M.U32(a, VtCEffect);
            M.I32(a + 4, id);
            for (uint o = 8; o <= 0x28; o += 4) M.U32(a + o, 0);
            M.I32(a + 0x2c, 0x3c);
            M.I32(a + 0x30, 1);
            M.U8(a + 0x34, 0);
            M.I32(a + 0x38, 0x82);
            M.I32(a + 0xb0, -1);
            M.U32(a + 0xb4, 0);
            M.U32(a + 0xb8, 0);
            M.U32(a + 0xbc, 0);
            M.U8(a + 0xc0, 0);
            M.U8(a + 0xc2, 0);
            M.U8(a + 0xc8, 0);
            M.U32(a + 0xc4, 0);
        }

        /// <summary>CEffect::~CEffect body (FUN_00a75970 → FUN_00a758a0): vtable back to CEffect, the sound vector freed.</summary>
        void DestructBase_a75970(FxRuntime rt)
        {
            M.U32(A, VtCEffect);
            uint vec = A + 0xb4;
            uint begin = M.U32(vec);
            if (begin == 0) return;
            uint end = M.U32(vec + 4);
            for (uint p = begin; p != end; p += 0x24) rt.StrDestroy(p + 0xc);
            begin = M.U32(vec);
            uint bytes = (uint)((int)(M.U32(vec + 8) - begin) / 0x24) * 0x24;
            rt.StdDeallocate(begin, bytes);
            M.U32(vec, 0);
            M.U32(vec + 4, 0);
            M.U32(vec + 8, 0);
        }

        /// <summary>std::vector&lt;T*&gt; destructor as inlined in the effect destructors (FUN_00678360).</summary>
        void DestroyPtrVector_678360(FxRuntime rt, uint vec)
        {
            uint begin = M.U32(vec);
            if (begin == 0) return;
            rt.StdDeallocate(begin, (M.U32(vec + 8) - begin) & 0xfffffffcu);
            M.U32(vec, 0);
            M.U32(vec + 4, 0);
            M.U32(vec + 8, 0);
        }

        // ---- virtual methods ----------------------------------------------------------------------

        /// <summary>vf01 — Init (after SetId, before the start sounds).</summary>
        public void Vf01Init(FxRuntime rt)
        {
            switch (Vtable)
            {
                case VtCWindEffect: WindInit_6aaff0(rt); return;              // CWindEffect::vf01_6ab330 (mapfx-wind)
                case VtCMagicFloorEffect: MagicFloorInit_a70a20(rt); return;  // (mapfx-wind)
                case VtCHealEffect: M.I32(A + 0x2c, 100); return;            // CHealEffect::vf01_a8fed0
                case VtCThrowItemEffect: ThrowItemInit_aa4340(rt); return;
                case VtCStormKickEffect: StormKickInit_aa32a0(rt); return;
                case VtCEZ2STREffect: EZ2STRInit_a7a500(rt); return;
                case VtCLevel99Effect: Level99Init_a92230(rt); return;
                case VtCLevel99Orb1Effect: Level99OrbInit(rt, false); return;   // CLevel99Orb1Effect::vf01_a92b30
                case VtCLevel99Orb2Effect: Level99OrbInit(rt, true); return;    // CLevel99Orb2Effect::vf01_a93710
                case VtCEmitterEffect: EmitterInit_a77ca0(rt); return;
                case VtCSmokeEffect: SmokeInit_a9dce0(rt); return;
                case VtCAnimatedEmitterEffect: AnimatedEmitterInit_6770e0(rt); return;
                case VtCEvilsPawEffect: EvilsPawInit_67bd10(rt); return;
                default: throw Unported(1);
            }
        }

        /// <summary>vf02 (CEffect::vf02_a76160) — out-of-range flag +0x34 = !(range &gt; distance to the local player).</summary>
        public void Vf02RangeCheck(FxRuntime rt)
        {
            if (Vtable == VtCEmitterEffect || Vtable == VtCAnimatedEmitterEffect) { EmitterRangeCheck_a788c0(rt); return; }   // CEmitterEffect::vf02_a788c0
            rt.PlayerPosition_a644c0(out float px, out float py, out float pz);
            float dy = py - M.F32(A + 0xc);
            float dx = px - M.F32(A + 8);
            float dz = pz - M.F32(A + 0x10);
            float sum = dy * dy + dx * dx;
            sum += dz * dz;
            float dist = FxRuntime.SqrtF(sum);
            float range = (float)M.I32(A + 0x38);
            M.U8(A + 0x34, !(range > dist) ? (byte)1 : (byte)0);    // COMISS range, dist; SETBE
        }

        /// <summary>vf03 — per-tick process.</summary>
        public void Vf03Process(FxRuntime rt)
        {
            switch (Vtable)
            {
                case VtCWindEffect: WindProcess_6ab360(rt); return;           // (mapfx-wind)
                case VtCMagicFloorEffect: MagicFloorProcess_a70b90(rt); return;
                case VtCHealEffect: HealProcess_a908c0(rt); return;
                case VtCThrowItemEffect: ThrowItemProcess_aa5480(rt); return;
                case VtCStormKickEffect: StormKickProcess_aa3580(rt); return;
                case VtCEZ2STREffect: EZ2STRProcess_a8d640(rt); return;
                case VtCLevel99Effect: Level99Process_a923f0(rt); return;
                case VtCLevel99Orb1Effect: Level99OrbProcess(rt, false); return;   // CLevel99Orb1Effect::vf03_a92c90
                case VtCLevel99Orb2Effect: Level99OrbProcess(rt, true); return;    // CLevel99Orb2Effect::vf03_a93860
                case VtCEmitterEffect: EmitterProcess_a77f80(rt); return;
                case VtCSmokeEffect: SmokeProcess_a9de80(rt); return;
                case VtCAnimatedEmitterEffect: EmitterProcess_a77f80(rt); return;   // CEmitterEffect::vf03_a77f80
                case VtCEvilsPawEffect: EvilsPawProcess_67c060(rt); return;
                default: throw Unported(3);
            }
        }

        /// <summary>vf04 — render.</summary>
        public void Vf04Render(FxRuntime rt)
        {
            switch (Vtable)
            {
                case VtCWindEffect: WindRender_6abb10(rt); return;            // (mapfx-wind)
                case VtCMagicFloorEffect: MagicFloorRender_a713c0(rt); return;
                case VtCHealEffect: HealRender_a91320(rt); return;
                case VtCThrowItemEffect: ThrowItemRender_aa5c70(rt); return;
                case VtCStormKickEffect: StormKickRender_aa3de0(rt); return;
                case VtCEZ2STREffect: EZ2STRRender_a8d910(rt); return;
                case VtCLevel99Effect: Level99Render_a92840(rt); return;
                case VtCLevel99Orb1Effect:                                       // CLevel99Orb1Effect::vf04_a93490
                case VtCLevel99Orb2Effect: Level99OrbRender(rt); return;         // CLevel99Orb2Effect::vf04_a94080
                case VtCEmitterEffect: EmitterRender_a783c0(rt); return;
                case VtCSmokeEffect: SmokeRender_a9e4e0(rt); return;
                case VtCAnimatedEmitterEffect: EmitterRender_a783c0(rt); return;    // CEmitterEffect::vf04_a783c0
                case VtCEvilsPawEffect: EvilsPawRender_67c3d0(rt); return;
                default: throw Unported(4);
            }
        }

        /// <summary>vf05 — returns the effect's particles to their pools and empties its particle vectors.</summary>
        public void Vf05ReleaseParticles(FxRuntime rt)
        {
            switch (Vtable)
            {
                case VtCWindEffect: ReleaseParticleVector(rt, A + 0x12c); return;      // CWindEffect::vf05_6ab2f0 (mapfx-wind)
                case VtCMagicFloorEffect:                                     // CMagicFloorEffect::vf05_a709b0 (mapfx-wind)
                    ReleaseParticleVector(rt, A + 0xfc);
                    ReleaseParticleVector(rt, A + 0x108);
                    return;
                case VtCHealEffect:                                           // CHealEffect::vf05_a8fe30
                    ReleaseParticleVector(rt, A + 0x108);
                    ReleaseParticleVector(rt, A + 0x114);
                    ReleaseParticleVector(rt, A + 0x120);
                    return;
                case VtCThrowItemEffect: ReleaseParticleVector(rt, A + 0xf8); return;   // CThrowItemEffect::vf05_aa42d0
                case VtCStormKickEffect:                                      // CStormKickEffect::vf05_aa3230
                    ReleaseParticleVector(rt, A + 0xe4);
                    ReleaseParticleVector(rt, A + 0xf0);
                    return;
                case VtCEZ2STREffect: rt.FreeParticle_aaa740(M.U32(A + 0xcc)); return;   // CEZ2STREffect::vf05_a79870
                case VtCLevel99Effect:                                        // CLevel99Effect::vf05_a921c0
                    ReleaseParticleVector(rt, A + 0xe4);
                    ReleaseParticleVector(rt, A + 0xf0);
                    return;
                case VtCLevel99Orb1Effect: ReleaseParticleVector(rt, A + 0x18c); return;   // CLevel99Orb1Effect::vf05_a92af0
                case VtCLevel99Orb2Effect: Level99Orb2Release_a936e0(rt); return;
                case VtCEmitterEffect: EmitterRelease_a77c60(rt); return;
                case VtCSmokeEffect: ReleaseParticleVector(rt, A + 0xd0); return;   // CSmokeEffect::vf05_a9dca0
                case VtCAnimatedEmitterEffect: EmitterRelease_a77c60(rt); return;   // CEmitterEffect::vf05_a77c60
                case VtCEvilsPawEffect: ReleaseParticleVector(rt, A + 0xcc); return;   // CEvilsPawEffect::vf05_67bcd0
                default: throw Unported(5);
            }
        }

        /// <summary>FUN_00aaa740 on every element, then end = begin.</summary>
        void ReleaseParticleVector(FxRuntime rt, uint vec)
        {
            uint p = M.U32(vec);
            for (; p != M.U32(vec + 4); p += 4) rt.FreeParticle_aaa740(M.U32(p));
            M.U32(vec + 4, M.U32(vec));
        }

        /// <summary>vf06 — still alive? The three particle classes: tick &lt; duration (FUN_00a76310 negated).</summary>
        public bool Vf06IsAlive(FxRuntime rt)
        {
            switch (Vtable)
            {
                case VtCWindEffect:                                           // vf06 0x6ab350 / 0xa70b80 → FUN_00a762f0 (mapfx-wind)
                case VtCMagicFloorEffect:
                    return OwnerAlive_a762f0(rt);
                case VtCHealEffect:
                case VtCThrowItemEffect:
                case VtCStormKickEffect:
                    return !(M.I32(A + 0x28) >= M.I32(A + 0x2c));
                case VtCEZ2STREffect: return EZ2STRIsAlive_a7e2e0(rt);
                case VtCLevel99Effect:                                        // vf06 0xa92310 / 0xa92c80 / 0xa93850 → FUN_00a762f0
                case VtCSmokeEffect:                                          // CSmokeEffect::vf06_a9de70 → FUN_00a762f0
                case VtCLevel99Orb1Effect:
                case VtCLevel99Orb2Effect:
                    return OwnerAlive_a762f0(rt);
                case VtCEmitterEffect: return EmitterIsAlive_a77f60(rt);
                case VtCAnimatedEmitterEffect: return EmitterIsAlive_a77f60(rt);   // CEmitterEffect::vf06_a77f60
                case VtCEvilsPawEffect: return true;                                // CEvilsPawEffect::vf06_67c050
                default: throw Unported(6);
            }
        }

        /// <summary>vf12 — start sounds (scheduled with FUN_00a75aa0).</summary>
        public void Vf12Start(FxRuntime rt)
        {
            switch (Vtable)
            {
                case VtCWindEffect:                                           // CEffect::vf12_670bc0: no sounds (mapfx-wind)
                case VtCMagicFloorEffect:
                    return;
                case VtCHealEffect: HealStart_a8fee0(rt); return;
                case VtCThrowItemEffect: ThrowItemStart_aa4ec0(rt); return;
                case VtCStormKickEffect: StormKickStart_aa33e0(rt); return;
                case VtCEZ2STREffect: if (Id != 0x8d && Id != MapStrEffectId) throw new NotSupportedException($"CEZ2STREffect {Id} start sounds are not ported"); return;   // vf12_a7ad60: none for 141, 1412
                case VtCLevel99Effect:                                        // CEffect::vf12_670bc0 (RET 0): no sounds
                case VtCLevel99Orb1Effect:
                case VtCLevel99Orb2Effect:
                case VtCEmitterEffect:
                case VtCSmokeEffect:
                case VtCAnimatedEmitterEffect:
                case VtCEvilsPawEffect:
                    return;
                default: throw Unported(12);
            }
        }

        /// <summary>
        /// vf24 — CEffect::SetId (0xa75f20): +4 = id, then Lua <c>IsForceRenderEffect(id)</c> ("d&gt;b") into +0xc2
        /// through FUN_00a26ed0, which takes the function name as a by-value std::string (buffer 0x20 bytes).
        /// </summary>
        public void Vf24SetId(FxRuntime rt, int id)
        {
            M.I32(A + 4, id);
            rt.LuaCallTemporaryName("IsForceRenderEffect");
            M.U8(A + 0xc2, rt.IsForceRenderEffectImpl(id) ? (byte)1 : (byte)0);
            if (Vtable == VtCEZ2STREffect)                                  // CEZ2STREffect::vf24_a8dc00
            {
                if ((uint)(id - 0x3f4) <= 0x53) throw new NotSupportedException($"CEZ2STREffect {id} SetId flags are not ported");
                if (id >= 0x972) M.U8(A + 0xc8, 1);
            }
        }

        /// <summary>vf25 — scalar deleting destructor.</summary>
        public void Vf25Destroy(FxRuntime rt, bool free)
        {
            switch (Vtable)
            {
                case VtCWindEffect: WindDestruct_6aaf60(rt); break;           // CWindEffect::vf25_6aaf60 (mapfx-wind)
                case VtCMagicFloorEffect: MagicFloorDestruct_a6ffd0(rt); break;   // CMagicFloorEffect::vf25_a6ffd0
                case VtCHealEffect:                                           // CHealEffect::vf25_a8ebf0
                    M.U32(A, VtCHealEffect);
                    DestroyPtrVector_678360(rt, A + 0x120);
                    DestroyPtrVector_678360(rt, A + 0x114);
                    DestroyPtrVector_678360(rt, A + 0x108);
                    DestructBase_a75970(rt);
                    break;
                case VtCThrowItemEffect:                                      // CThrowItemEffect::vf25_aa4250
                    M.U32(A, VtCThrowItemEffect);
                    DestroyPtrVector_678360(rt, A + 0xf8);
                    DestructBase_a75970(rt);
                    break;
                case VtCStormKickEffect:                                      // CStormKickEffect::vf25_aa2a70
                    M.U32(A, VtCStormKickEffect);
                    DestroyPtrVector_678360(rt, A + 0xf0);
                    DestroyPtrVector_678360(rt, A + 0xe4);
                    DestructBase_a75970(rt);
                    break;
                case VtCEZ2STREffect:                                          // CEZ2STREffect::vf25_a79590
                    M.U32(A, VtCEZ2STREffect);
                    rt.StrDestroy(A + 0xd0);
                    DestructBase_a75970(rt);
                    break;
                case VtCLevel99Effect:                                         // CLevel99Effect::vf25_a91920
                    M.U32(A, VtCLevel99Effect);
                    DestroyPtrVector_678360(rt, A + 0xf0);
                    DestroyPtrVector_678360(rt, A + 0xe4);
                    DestructBase_a75970(rt);
                    break;
                case VtCLevel99Orb1Effect:                                     // CLevel99Orb1Effect::vf25_a92a70
                case VtCLevel99Orb2Effect:                                     // CLevel99Orb2Effect::vf25_a93660
                    M.U32(A, Vtable);
                    DestroyPtrVector_678360(rt, A + 0x18c);
                    DestructBase_a75970(rt);
                    break;
                case VtCEmitterEffect: EmitterDestruct_a77a60(rt); break;   // CEmitterEffect::vf25_a77a60
                case VtCSmokeEffect: SmokeDestruct_a9dc20(rt); break;       // CSmokeEffect::vf25_a9dc20
                case VtCAnimatedEmitterEffect: AnimatedEmitterDestruct_677050(rt); break;   // CAnimatedEmitterEffect::vf25_677050
                case VtCEvilsPawEffect: EvilsPawDestruct_67bb70(rt); break;                 // CEvilsPawEffect::vf25_67bb70
                default: throw Unported(25);
            }
            if (free) M.Free(A);
        }

        /// <summary>
        /// vf28 (CEffect::vf28_a75d40) — visible? Unless the owner exists and is flagged at +0xa8, always; otherwise
        /// (not attached) the local player's own effects, party members' with the world +0xfd option, and then the
        /// owner's +0xa0/+0xa1 flags (FUN_00bd1e40).
        /// </summary>
        public bool Vf28IsVisible(FxRuntime rt)
        {
            uint actor = rt.ActorByAid_a64480(M.U32(A + 0x20));
            if (actor == 0 || M.U32(actor + 0xa8) == 0) return true;
            if (M.U8(A + 0xc8) == 0)
            {
                if (M.U32(A + 0x20) == rt.PlayerAid) return true;
                if (rt.PartyOptionEnabled_d2b990() && rt.IsPartyMemberImpl(M.U32(actor + 0x110), true)) return true;
            }
            return M.U8(actor + 0xa0) != 0 && M.U8(actor + 0xa1) != 0;
        }

        Exception Unported(int slot) => new NotSupportedException($"CEffect vtable 0x{Vtable:x8} vf{slot:d2} is not ported");

        // ---- non-virtual helpers ------------------------------------------------------------------

        /// <summary>FUN_00a75ff0 — target: when the actor exists, +0x24 = its AID and +0x14..+0x1c its position.</summary>
        public void SetTarget_a75ff0(FxRuntime rt, uint targetAid)
        {
            uint actor = rt.ActorByAid_a64480(targetAid);
            if (actor == 0) return;
            M.U32(A + 0x24, targetAid);
            M.U32(A + 0x14, M.U32(actor + 0x10));
            M.U32(A + 0x18, M.U32(actor + 0x14));
            M.U32(A + 0x1c, M.U32(actor + 0x18));
        }

        /// <summary>
        /// FUN_00a76030 — one tick: follow the owner (by AID), vf02 range check; in range: the sounds scheduled for
        /// this tick (type 0 = PlaySound3D relative to the player, 1 = screen quake: mode SendMsg 0x7a), vf03, tick++.
        /// Out of range the effect neither processes nor advances.
        /// </summary>
        public void Tick_a76030(FxRuntime rt, uint ownerAid)
        {
            uint owner = rt.ActorByAid_a64480(ownerAid);
            if (owner != 0)
            {
                M.U32(A + 8, M.U32(owner + 0x10));
                M.U32(A + 0xc, M.U32(owner + 0x14));
                M.U32(A + 0x10, M.U32(owner + 0x18));
            }
            Vf02RangeCheck(rt);
            if (M.U8(A + 0x34) != 0) return;
            uint player = M.U32(M.U32(rt.Mode + 0xcc) + 0x2c);
            float px = M.F32(player + 0x10), pz = M.F32(player + 0x18);
            for (uint s = M.U32(A + 0xb4); s != M.U32(A + 0xb8); s += 0x24)
            {
                if (M.I32(A + 0x28) != M.I32(s + 4)) continue;
                int type = M.I32(s);
                if (type == 0)
                {
                    if (M.U32(s + 0x1c) == 0) continue;
                    float dz = M.F32(A + 0x10) - pz;
                    float dx = M.F32(A + 8) - px;
                    rt.Host?.PlaySound3D(rt.StrRead(s + 0xc), dx, M.F32(s + 8), dz, 0xfa, 0x28, 1.0f, 0);
                }
                else if (type == 1)
                {
                    rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
                }
            }
            Vf03Process(rt);
            M.I32(A + 0x28, M.I32(A + 0x28) + 1);
        }

        /// <summary>FUN_00a75ef0 — draw when effects are on (DAT_01552d74) or forced, vf28 visible and in range.</summary>
        public void Render_a75ef0(FxRuntime rt)
        {
            if (!rt.EffectsEnabled && M.U8(A + 0xc2) == 0) return;
            if (!Vf28IsVisible(rt)) return;
            if (M.U8(A + 0x34) != 0) return;
            Vf04Render(rt);
        }

        /// <summary>
        /// FUN_00a75aa0 — schedules a sound (type 0) or quake (type 1) for tick <paramref name="tick"/>: builds a
        /// std::string from <paramref name="wav"/> (null → ""), pushes {type, tick, y, string} onto the vector at
        /// +0xb4 (FUN_00a753d0 when full), then destroys the local string.
        /// </summary>
        public void AddSound_a75aa0(FxRuntime rt, int type, string wav, int tick, float y)
        {
            // the local std::string lives on the stack: only its heap buffer (names of 16+ bytes) is visible
            string name = wav ?? "";
            uint localBuf = rt.StrLocalAssign(name);
            uint vec = A + 0xb4;
            uint end = M.U32(vec + 4);
            if (end != M.U32(vec + 8))
            {
                M.I32(end, type);
                M.I32(end + 4, tick);
                M.F32(end + 8, y);
                rt.StrCopyConstructLocal(end + 0xc, name);
                M.U32(vec + 4, end + 0x24);
            }
            else SoundVectorEmplaceReallocate_a753d0(rt, vec, end, type, tick, y, name);
            rt.StrLocalDestroy(name, localBuf);
        }

        /// <summary>FUN_00a753d0 — std::vector&lt;SoundEntry&gt;::_Emplace_reallocate (entry 0x24 bytes).</summary>
        void SoundVectorEmplaceReallocate_a753d0(FxRuntime rt, uint vec, uint where, int type, int tick, float y, string name)
        {
            uint begin = M.U32(vec);
            int size = (int)(M.U32(vec + 4) - begin) / 0x24;
            if (size == 0x71c71c7) throw new InvalidOperationException("vector too long");
            uint newSize = (uint)size + 1;
            uint cap = (uint)((int)(M.U32(vec + 8) - begin) / 0x24);
            if (cap > 0x71c71c7u - (cap >> 1)) throw new InvalidOperationException("vector too long");
            uint newCap = (cap >> 1) + cap;
            if (newCap < newSize) newCap = newSize;
            if (newCap > 0x71c71c7u) throw new InvalidOperationException("vector too long");
            uint bytes = newCap * 0x24;
            uint buf = rt.StdAllocate(bytes);
            uint at = buf + (uint)((int)(where - begin) / 0x24) * 0x24;
            M.I32(at, type);                                                   // FUN_00a756e0
            M.I32(at + 4, tick);
            M.F32(at + 8, y);
            rt.StrCopyConstructLocal(at + 0xc, name);
            uint oldEnd = M.U32(vec + 4);
            uint dst = buf;
            if (where != oldEnd)
            {
                dst = MoveSoundEntries_a75610(rt, begin, where, buf);
                dst = at + 0x24;
                MoveSoundEntries_a75610(rt, where, oldEnd, dst);
            }
            else MoveSoundEntries_a75610(rt, begin, oldEnd, buf);
            begin = M.U32(vec);
            if (begin != 0)
            {
                uint e = M.U32(vec + 4);
                for (uint p = begin; p != e; p += 0x24) rt.StrDestroy(p + 0xc);
                begin = M.U32(vec);
                rt.StdDeallocate(begin, (uint)((int)(M.U32(vec + 8) - begin) / 0x24) * 0x24);
            }
            M.U32(vec, buf);
            M.U32(vec + 4, buf + newSize * 0x24);
            M.U32(vec + 8, buf + bytes);
        }

        /// <summary>FUN_00a75610 — uninitialized move of sound entries [first, last) to <paramref name="dst"/>.</summary>
        uint MoveSoundEntries_a75610(FxRuntime rt, uint first, uint last, uint dst)
        {
            for (uint s = first; s != last; s += 0x24, dst += 0x24)
            {
                M.U32(dst, M.U32(s));
                M.U32(dst + 4, M.U32(s + 4));
                M.U32(dst + 8, M.U32(s + 8));
                rt.StrMoveConstruct(dst + 0xc, s + 0xc);
            }
            return dst;
        }
    }

    public sealed partial class FxRuntime
    {
        /// <summary>
        /// The by-value std::string function-name argument of FUN_00a26ed0 (Lua call helper): the caller allocates
        /// a 0x20-byte buffer (FUN_004d89d0(0x20)) holding the name, the callee destroys the string on return.
        /// </summary>
        public void LuaCallTemporaryName(string name)
        {
            uint buf = StdAllocate(0x20);
            for (int i = 0; i < name.Length; i++) M.U8(buf + (uint)i, (byte)name[i]);
            M.U8(buf + (uint)name.Length, 0);
            StdDeallocate(buf, 0x20);
        }

        /// <summary>FUN_00d2b990 — the world's "party effects" option byte (world +0xfd), 0 without a world.</summary>
        public bool PartyOptionEnabled_d2b990()
        {
            if (Mode == 0) return false;
            uint world = M.U32(Mode + 0xcc);
            return world != 0 && M.U8(world + 0xfd) != 0;
        }
    }
}
