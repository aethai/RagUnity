// Synced from reference/port/src/RoClassicFx/Shared/effects-10/FxRuntime.Effects10.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    public sealed partial class FxRuntime
    {
        /// <summary>
        /// Shim source for FUN_00bd09c0 (thiscall on an actor, no arguments, int in EAX): the actor's mount / job
        /// height step used by EF 1057 (Offertorium ring). The client derives it from the job (GetJob 0xbd0cd0 and the
        /// job-class tables FUN_00d1e440 / FUN_00d01810 / FUN_00d1dd80 / FUN_00d2b820) and the mount byte +0x2cc:
        /// 0 for jobs outside those classes (and for other players' non-matching jobs), 10 / −20 for one class
        /// (mounted / not), 30 / 0 for the riding classes, −40 / −30 for another. Default 0; hosts return the value for
        /// the actor (tests install the same function in the oracle).
        /// </summary>
        public Func<uint, int> ActorMountHeightImpl_bd09c0 = actor => 0;

        /// <summary>FUN_00bd09c0 — see <see cref="ActorMountHeightImpl_bd09c0"/>.</summary>
        public int ActorMountHeight_bd09c0(uint actor) => ActorMountHeightImpl_bd09c0(actor);

        // ---- actor job ---------------------------------------------------------------------------------------

        /// <summary>
        /// Shim source for GetJob (FUN_00bd0cd0, thiscall on an actor with −1, ret 4): the actor's job id (the client
        /// resolves transformations / disguises from the actor's state). Default 0 (novice).
        /// </summary>
        public Func<uint, int> ActorJobImpl_bd0cd0 = actor => 0;

        /// <summary>FUN_00bd0cd0(actor, −1) — see <see cref="ActorJobImpl_bd0cd0"/>.</summary>
        public int ActorJob_bd0cd0(uint actor) => ActorJobImpl_bd0cd0(actor);

        /// <summary>
        /// FUN_00d02e40 (thiscall on the job table 0x154dd00, ret 4): 1 for player jobs — job ≤ 30 or 4001..6000 —
        /// else 0. Pure logic; runs natively in the oracle.
        /// </summary>
        public static bool IsPlayerJob_d02e40(int job) => !((uint)(job - 0xfa1) > 0x7ce && (uint)job > 0x1e);

        // ---- hallucination clones (CHalluPC / CHalluPC2 / CHalluNPC) ------------------------------------------

        /// <summary>RTTI vtables of the after-image clone classes (game.exe).</summary>
        public const uint VtCHalluPC = 0x01002214, VtCHalluPC2 = 0x0100230c, VtCHalluNPC = 0x01002404;

        /// <summary>
        /// The clone constructors (thiscall (source actor), ret 4): FUN_00cbe340 CHalluPC (0x510 bytes, CPc base),
        /// FUN_00cbe020 CHalluPC2 (0x510, CPc base), FUN_00cbde20 CHalluNPC (0x450, CNpc base) — shim. The client
        /// builds a full actor copying the source's sprite, job, palette, position and motion state. The port lays
        /// down a CRenderObject base at the source's position (so SetARGB / the world render list work on it) with
        /// the class vtable at +0 and, for the PC classes, the source at +0x508 (the field the ctors store verbatim);
        /// hosts create the visible clone from <see cref="HalluSpawnedImpl"/>.
        /// </summary>
        public void ConstructHallu(uint obj, uint source, uint vtable)
        {
            ConstructRenderObject(obj);
            M.U32(obj + 0x10, M.U32(source + 0x10));
            M.U32(obj + 0x14, M.U32(source + 0x14));
            M.U32(obj + 0x18, M.U32(source + 0x18));
            M.U32(obj, vtable);
            if (vtable != VtCHalluNPC) M.U32(obj + 0x508, source);
            HalluSpawnedImpl?.Invoke(obj, source, vtable);
        }

        /// <summary>Host notification of a clone construction: (clone, source actor, class vtable).</summary>
        public Action<uint, uint, uint> HalluSpawnedImpl;

        /// <summary>Host notification of a clone's SendMsg (vf02): (clone, sender, msg, x, y, z).</summary>
        public Action<uint, uint, int, long, long, long> HalluSendMsgImpl;

        /// <summary>
        /// CHalluPC / CHalluNPC::SendMsg (vtable slot 2: 0xcc1730 / 0xcc1590; thiscall, 13 dwords, ret 0x34) — shim,
        /// forwarded to the host. Called by fn_b458c0 with msg 0 and the colour as three 64-bit parameters.
        /// </summary>
        public void HalluSendMsg(uint obj, uint sender, int msg, long x, long y, long z) => HalluSendMsgImpl?.Invoke(obj, sender, msg, x, y, z);
    }
}

namespace Ragnarok.Effects.Classic
{
    public sealed partial class FxRuntime
    {
        /// <summary>
        /// The default ACT every CRagEffect carries at +0x108. In the client the CRenderObject ctor (0xbcd750 →
        /// 0xbd3980, stores at 0xbd3b2c) loads NPC\1_ETC_01.spr/.act into every render object; that ACT has 8 actions
        /// × 1 frame with delay 4.0. The port's <see cref="ConstructRenderObject"/> skips resource loading, so effects
        /// get this shared stub instead (see <see cref="AllocDefaultEffectAct"/>): only CRenderObject::SetAction /
        /// CActRes::GetDelay (0x6c16b0) read it — EF 80 (fn_b61000 calls SetAction on the effect itself) and any other
        /// effect that calls SetAction without its own SetActName. Allocated once per runtime, after the fixed
        /// Mode / World / Player / View blocks.
        /// </summary>
        public uint DefaultEffectAct { get; private set; }

        /// <summary>
        /// Lays down the stub CActRes: vtable 0xf82e4c (CActRes), lock count +4 = 1, the per-action delay vector
        /// begin / end at +0x12c / +0x130 → 8 floats 4.0 (the 1_ETC_01 delays; GetDelay returns 4.0 outside the vector
        /// as well).
        /// </summary>
        uint AllocDefaultEffectAct()
        {
            uint act = M.Alloc(0x140);
            uint delays = M.Alloc(8 * 4);
            for (uint i = 0; i < 8; i++) M.F32(delays + i * 4, 4.0f);
            M.U32(act, FxSpriteResources.VtCActRes);
            M.I32(act + 4, 1);
            M.U32(act + 0x12c, delays);
            M.U32(act + 0x130, delays + 8 * 4);
            M.U32(act + 0x134, delays + 8 * 4);
            return act;
        }
    }
}
