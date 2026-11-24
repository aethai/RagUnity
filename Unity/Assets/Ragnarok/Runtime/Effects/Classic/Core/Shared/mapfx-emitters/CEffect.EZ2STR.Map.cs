// Synced from reference/port/src/RoClassicFx/Shared/mapfx-emitters/CEffect.EZ2STR.Map.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// EF 1412 — the map STR of CEZ2STREffect (class ported for EF 141 by package newsys; vtable 0xfe289c). Launched once
    /// per RSW record (ba_bath: 6 records) like every new-system map id; vf01 marks it looping (+0xc0 = 1, particle +0x3f5c
    /// = 1: case 0x584 of vf01_a7a500 → LAB_00a7a765); vf29 case 0x584 (0xa80560) loads the STR named by the effect's
    /// std::string +0xd0 — the ctor default "»¡°£Æ÷¼Ç.str" (빨간포션.str, a red potion) until the map's EffectTool calls
    /// <c>SetEZ2STRInfo</c> (FUN_00aa7520, <see cref="FxRuntime.SetEZ2STRInfo_aa7520"/>): MSG_SET_STR_FILENAME (vf18: the
    /// name, the particle back to its pool and vf01 again), MSG_SET_RENDER_FLAG (vf19: the particle's RP flags +0x1c),
    /// MSG_SET_SCALE_RATIO (vf20: +0x3fa8), MSG_SET_ALPHA_RATIO (vf21: +0x3fac). vf03 plays the STR at the anchor itself (no
    /// −80 lift for 0x584); vf06 keeps it while the particle's life (+0xcc) is 0 (a looping STR never sets it); no start
    /// sounds (vf12 jump table 0xa7c8d0 → 0xa7c7f5: nothing below 0x972). Package mapfx-emitters.
    /// </summary>
    public readonly partial struct CEffect
    {
        /// <summary>EF 1412 (0x584).</summary>
        public const int MapStrEffectId = 0x584;

        /// <summary>CEZ2STREffect::vf29_a7e880 case 0x584 (0xa80560): FUN_00a655c0 with a by-value copy (FUN_004ccbd0) of +0xd0.</summary>
        void EZ2STRSetupMapStr_a80560(FxRuntime rt, uint particle)
        {
            string name = rt.StrRead(A + 0xd0);
            uint size = M.U32(A + 0xd0 + 0x10);
            uint arg = rt.StrLocalAssign(rt.StrData(A + 0xd0), size);       // the copy's heap buffer (16+ bytes)
            new CEZ2STRParticle(M, particle).Load_a655c0(rt, name);
            rt.StrLocalDestroy(size, arg);                                  // destroyed at FUN_00a655c0's end
        }

        /// <summary>
        /// CEZ2STREffect::vf06_a7e2e0 for ids outside its owner-bound lists (0xa7e311): alive while the particle's life
        /// +0xcc is 0 (UCOMISS: equal and ordered), otherwise while life · 60 &gt; (float)age (COMISS; SETA).
        /// </summary>
        bool EZ2STRIsAliveByLife_a7e311()
        {
            uint p = M.U32(A + 0xcc);
            float life = M.F32(p + 0xcc);
            if (life == 0f) return true;
            return life * 60.0f > (float)M.I32(p + 0xe8);
        }

        /// <summary>CEZ2STREffect::vf18_a8de00 — STR name: +0xd0 = name (FUN_004d9270), the particle back to its pool, vf01.</summary>
        public void EZ2STRSetStrName_a8de00(FxRuntime rt, string name)
        {
            if (Vtable != VtCEZ2STREffect) throw Unported(18);
            rt.StrAssign(A + 0xd0, name);
            rt.FreeParticle_aaa740(M.U32(A + 0xcc));
            Vf01Init(rt);
        }

        /// <summary>CEZ2STREffect::vf19_a8dd10 — the particle's RP flags (+0x1c); no null check in the client.</summary>
        public void EZ2STRSetRenderFlag_a8dd10(uint flags)
        {
            if (Vtable != VtCEZ2STREffect) throw Unported(19);
            M.U32(M.U32(A + 0xcc) + 0x1c, flags);
        }

        /// <summary>CEZ2STREffect::vf20_a8dd90 — the particle's scale ratio (+0x3fa8) when there is one.</summary>
        public void EZ2STRSetScaleRatio_a8dd90(float scale)
        {
            if (Vtable != VtCEZ2STREffect) throw Unported(20);
            uint p = M.U32(A + 0xcc);
            if (p != 0) M.F32(p + 0x3fa8, scale);
        }

        /// <summary>CEZ2STREffect::vf21_a8dbe0 — the particle's alpha ratio (+0x3fac) when there is one.</summary>
        public void EZ2STRSetAlphaRatio_a8dbe0(float alpha)
        {
            if (Vtable != VtCEZ2STREffect) throw Unported(21);
            uint p = M.U32(A + 0xcc);
            if (p != 0) M.F32(p + 0x3fac, alpha);
        }
    }

    public sealed partial class FxRuntime
    {
        /// <summary>
        /// FUN_006c7a60 (thiscall on an STR layer; index, std::string directory by value; RET 0x1c) — the layer texture of an
        /// STR loaded from a sub-directory (CEZ2STRParticle +0x3f78 not empty): cached at layer +8 + 4i (the same slots as
        /// FUN_006c7940); otherwise sprintf("effect\%s", dir) + the frame name, ".bmp" appended when the whole name has no
        /// '.', GetTexture → cached. An index past the layer's count → FUN_0054f5b0 (the fallback texture). The caller's
        /// by-value directory string is destroyed here (its heap buffer when 16+ bytes).
        /// </summary>
        public uint GetLayerTextureInDir_6c7a60(uint layer, int index, string dir)
        {
            if (index >= M.I32(layer)) return FallbackTexture;
            uint cached = M.U32(layer + 8 + (uint)index * 4);
            if (cached != 0) return cached;
            string name = "effect\\" + dir + ReadCString(M.U32(layer + 0x1c0 + (uint)index * 4));
            if (name.IndexOf('.') < 0) name += ".bmp";
            uint tex = GetTexture(name);
            M.U32(layer + 8 + (uint)index * 4, tex);
            return tex;
        }
    }
}
