// Synced from reference/port/src/RoClassicFx/Shared/newsys/CEffect.EZ2STR.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CEZ2STREffect (vtable 0xfe289c, 0x108 bytes; allocator VCEZ2STREffect) — the new system's STR effect: one
    /// CEZ2STRParticle (+0xcc) playing a per-id STR (vf29, a 1000-id switch) 80 units above its anchor.
    /// Ported for EF 141 (Pneuma: "Pneuma%d.str", rand() % 3 + 1). It lives as long as its owner exists
    /// (FUN_00a762f0; ownerless: forever) — Pneuma's owner is the skill unit (a CSkill found by FUN_009f8c80), so
    /// the effect ends when the unit disappears; the STR plays once and then holds its last frame.
    /// Fields: +0xcc particle, +0xd0 std::string (the ctor default name at 0xfe2914), +0xe8 byte, +0xec fade, +0xf0 byte
    /// camera-relative, +0xf8 double camera longitude, +0x100 byte size-by-owner.
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCEZ2STREffect = 0x00fe289c;
        public const uint VtAllocEZ2STR = 0x00fe1774;
        public const uint EZ2STRSize = 0x108;
        static readonly int[] EZ2STRIds = { 141, 1412 };   // 1412: package mapfx-emitters (CEffect.EZ2STR.Map.cs)

        const string EZ2STRDefaultName = "»¡°£Æ÷¼Ç.str";   // 0xfe2914

        /// <summary>CEZ2STREffect::CEZ2STREffect (FUN_00a793f0).</summary>
        static uint ConstructEZ2STR_a793f0(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, -1);
            M.U32(a, VtCEZ2STREffect);
            M.U32(a + 0xcc, 0);
            M.U32(a + 0xd0, 0);
            M.U32(a + 0xe0, 0);
            M.U32(a + 0xe4, 0xf);
            M.U8(a + 0xd0, 0);
            rt.StrAssign(a + 0xd0, EZ2STRDefaultName);
            M.U8(a + 0xe8, 0);
            M.U32(a + 0xec, 0);
            M.U8(a + 0xf0, 0);
            M.U32(a + 0xf8, 0);
            M.U32(a + 0xfc, 0);
            M.U8(a + 0x100, 0);
            return a;
        }

        /// <summary>
        /// CEZ2STREffect::vf01_a7a500 — particle from pool 2 at the effect's position (FUN_00aaa640, RP flags 5), owner
        /// AID into it, looping when keyed, unit quad, the per-id STR (vf29), and the camera longitude (+0xf8).
        /// </summary>
        void EZ2STRInit_a7a500(FxRuntime rt)
        {
            uint p = rt.AllocEz2StrParticle_aaa640(A + 8, 5);
            if (p == 0) return;
            M.U32(p + 0x3fc8, M.U32(A + 0x20));
            if (M.U8(A + 0xc8) == 1) throw new NotSupportedException("attached CEZ2STREffects (Lua shrink state) are not ported");
            if (Id == MapStrEffectId)                                         // case 0x584 → LAB_00a7a765
            {
                M.U8(A + 0xc0, 1);
                M.U8(p + 0x3f5c, 1);
            }
            if (M.I32(A + 0xc4) > 0) M.U8(p + 0x3f5c, 1);
            CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
            M.U32(A + 0xcc, p);
            EZ2STRSetup_a7e880(rt, p, Id);
            uint view = M.U32(rt.Mode + 0xd0);
            WriteF64(A + 0xf8, (double)M.F32(view + 0x30));
        }

        void WriteF64(uint a, double v)
        {
            long bits = BitConverter.DoubleToInt64Bits(v);
            M.U32(a, (uint)bits);
            M.U32(a + 4, (uint)(bits >> 32));
        }

        /// <summary>CEZ2STREffect::vf29_a7e880 (particle, id) — the STR of each id; ported: 141.</summary>
        void EZ2STRSetup_a7e880(FxRuntime rt, uint particle, int id)
        {
            switch (id)
            {
                case 0x8d:
                {
                    int r = rt.Rand.Next() % 3;
                    if (r < 0) r = 0;
                    new CEZ2STRParticle(M, particle).Load_a655c0(rt, "Pneuma" + (r + 1) + ".str");
                    return;
                }
                case MapStrEffectId: EZ2STRSetupMapStr_a80560(rt, particle); return;
                default: throw new NotSupportedException($"CEZ2STREffect STR setup for effect {id} is not ported");
            }
        }

        /// <summary>
        /// CEZ2STREffect::vf03_a8d640 — the particle follows the effect, plays one STR frame 80 units up
        /// (FUN_00a65840(0, -80, 0) unless FUN_00a79610 anchors it to the owner), ages, and takes the owner's
        /// int64 height (+0x128) for the draw.
        /// </summary>
        void EZ2STRProcess_a8d640(FxRuntime rt)
        {
            uint p = M.U32(A + 0xcc);
            if (M.U8(A + 0xf0) == 1 && p != 0) throw new NotSupportedException("camera-relative CEZ2STREffects are not ported");
            M.U32(p + 0x24, M.U32(A + 8));
            M.U32(p + 0x28, M.U32(A + 0xc));
            M.U32(p + 0x2c, M.U32(A + 0x10));
            p = M.U32(A + 0xcc);
            if (p != 0)
            {
                bool anchored = EZ2STRAnchorToOwner_a79610(rt, p);
                new CEZ2STRParticle(M, p).Process_a65840(rt, 0f, !anchored && Id != 0x584 ? -80.0f : 0f, 0f);
            }
            p = M.U32(A + 0xcc);
            M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            if (Id == 0x851) throw new NotSupportedException("effect 2129's periodic sound is not ported");
            uint owner = rt.ActorByAid_a64480(M.U32(A + 0x20));
            if (owner == 0) return;
            if (M.U8(A + 0x100) == 1) throw new NotSupportedException("owner-sized CEZ2STREffects are not ported");
            p = M.U32(A + 0xcc);
            M.U32(p + 0x3fd8, M.U32(owner + 0x128));
            M.U32(p + 0x3fdc, M.U32(owner + 0x12c));
        }

        /// <summary>
        /// FUN_00a79610 — anchors the particle to the owner's head for attached effects and the ids of FUN_00a7dd60
        /// (0x425..0x96a); every other effect keeps its own anchor (false).
        /// </summary>
        bool EZ2STRAnchorToOwner_a79610(FxRuntime rt, uint particle)
        {
            // FUN_00a7dd60: byte table 0xa7dd8c[id - 0x425] (0 → anchored); it holds 1 for 1412 (0x584)
            if (M.U8(A + 0xc8) == 1 || ((uint)(Id - 0x425) < 0x546 && Id != MapStrEffectId))
                throw new NotSupportedException($"head-anchored CEZ2STREffect {Id} (FUN_00a79610) is not ported");
            return false;
        }

        /// <summary>CEZ2STREffect::vf04_a8d910 — the particle's STR layers (the screen fades of 1607/2311.. are not reached).</summary>
        void EZ2STRRender_a8d910(FxRuntime rt)
        {
            uint p = M.U32(A + 0xcc);
            if (p == 0) return;
            int id = Id;
            if (id == 0x647 || (id >= 0x907 && id <= 0x95f)) throw new NotSupportedException($"CEZ2STREffect {id} screen fade is not ported");
            new CEZ2STRParticle(M, p).Render_a660b0(rt, M.U32(rt.Mode + 0xd0) + 0x98);
        }

        /// <summary>CEZ2STREffect::vf06_a7e2e0 — 141 (and attached effects): alive while the owner exists (FUN_00a762f0).</summary>
        bool EZ2STRIsAlive_a7e2e0(FxRuntime rt)
        {
            if (M.U8(A + 0xc8) != 1 && Id == MapStrEffectId) return EZ2STRIsAliveByLife_a7e311();
            if (M.U8(A + 0xc8) != 1 && Id != 0x8d) throw new NotSupportedException($"CEZ2STREffect {Id} lifetime is not ported");
            uint owner = M.U32(A + 0x20);
            if (owner != 0 && rt.ActorByAid_a64480(owner) == 0) return false;
            return true;
        }
    }
}
