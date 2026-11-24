// Synced from reference/port/src/RoClassicFx/Rag/actorfx/RagEffect.ActorLook.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// actorfx: the owner-look effect cases no earlier package ported — each only sets bits / counters on its owner
    /// for the actor renderer (FxRuntime.ActorRender.cs). OnProcess 0xb527f0 cases 0x1a1..0x1a7 (asm 0xb57288..0xb5742b)
    /// and vf15 0xb47410 cases 0x28f / 0x290 (0xb4b082 / 0xb4b0ca) and 0x337 (0xb48a70).
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 417 (0x1a1, EF_HITBODY): flag 0x100 (white flash) once per tick, +0xf4 = tick while tick &lt; 15.</summary>
        [FxEffectProcess(0x1a1)]
        public FxCaseResult Case_Afx_HitBody(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x100) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, f + 0x100);
            int t = M.I32(A + 0x178);
            if (t < 0xf) M.I16(M.U32(A + 0x138) + 0xf4, (short)t);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 421 (0x1a5, EF_BABYBODY2): loops (stateCnt back to 0 at DefaultDuration(421) − 1); flag 0x10 with +0xf0 = 45
        /// — the actor stays at half size while the effect lives.
        /// </summary>
        [FxEffectProcess(0x1a5)]
        public FxCaseResult Case_Afx_BabyBody2(FxRuntime rt)
        {
            int dur = ClassicEffectTables.DefaultDuration(0x1a5);
            if (dur - 1 <= M.I32(A + 0x178)) M.I32(A + 0x178, 0);
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x10) == 0)
            {
                M.U32(master + 0xe8, f + 0x10);
                M.U8(M.U32(A + 0x138) + 0xf0, 0x2d);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 422 (0x1a6, EF_GIANTBODY): flag 0x400 (grow to 1.5×), +0xf1 = tick while tick ≤ 45.</summary>
        [FxEffectProcess(0x1a6)]
        public FxCaseResult Case_Afx_GiantBody(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x400) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, f + 0x400);
            int t = M.I32(A + 0x178);
            if (t <= 0x2d) M.U8(M.U32(A + 0x138) + 0xf1, (byte)t);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 423 (0x1a7, EF_GIANTBODY2): flag 0x400 with +0xf1 = 45 — the actor stays 1.5× while the effect lives.</summary>
        [FxEffectProcess(0x1a7)]
        public FxCaseResult Case_Afx_GiantBody2(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x400) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, f + 0x400);
            M.U8(M.U32(A + 0x138) + 0xf1, 0x2d);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 655 (0x28f, vf15): flag 0x80000 (green aura copies) with +0xf2 = min(tick, 200).</summary>
        [FxEffectProcess(0x28f)]
        public FxCaseResult Case_Afx_GreenAura(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x80000) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, f + 0x80000);
            int t = M.I32(A + 0x178);
            M.U8(M.U32(A + 0x138) + 0xf2, (byte)(t < 200 ? (byte)t : 200));
            return FxCaseResult.Continue;
        }

        /// <summary>EF 656 (0x290, vf15): ticks 0..39: flag 0x80000 with +0xf2 = 200 − 5·tick (fading green aura).</summary>
        [FxEffectProcess(0x290)]
        public FxCaseResult Case_Afx_GreenAuraFade(FxRuntime rt)
        {
            if (M.I32(A + 0x178) >= 0x28) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x80000) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, f + 0x80000);
            byte t = M.U8(A + 0x178);
            M.U8(M.U32(A + 0x138) + 0xf2, (byte)(0xc8 - (byte)(t + (byte)(t << 2))));
            return FxCaseResult.Continue;
        }

        /// <summary>EF 823 (0x337, vf15): flag 0x100000 every tick (players sink by 24000 / zoom px, generic actors turn by 60°).</summary>
        [FxEffectProcess(0x337)]
        public FxCaseResult Case_Afx_Sink(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x100000) == 0) M.U32(master + 0xe8, f + 0x100000);
            return FxCaseResult.Continue;
        }
    }
}
