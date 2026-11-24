// Synced from reference/port/src/RoClassicFx/Rag/effects-10/RagEffect.Effects10.Hallu.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>EF 792 (0x318; case 0xb496f6): fn_b458c0 — after-images of the owner every 20 ticks (EFST 334 / 869).</summary>
        [FxEffectProcess(0x318)]
        public FxCaseResult Case_E10_792(FxRuntime rt) { Fn_b458c0(rt); return FxCaseResult.Continue; }

        /// <summary>EF 925 (0x39d; case 0xb49700): fn_b456b0 — CHalluPC2 after-images of the owner every 5 ticks (EFST 445).</summary>
        [FxEffectProcess(0x39d)]
        public FxCaseResult Case_E10_925(FxRuntime rt) { Fn_b456b0(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b458c0 (0xb458c0) — like fn_b45180 (EF 166) with hallucination clones: owner byte +0x90 == 0:
        /// owner ARGB(−1, cvtt(pos x), cvtt(pos y), cvtt(pos z)) (the effect position carries the colour), counter
        /// 9998 wraps to 0, and every 20th tick (every tick when the duration is 41) a clone of the owner — CHalluPC
        /// (0x510) for player jobs, CHalluNPC (0x450) otherwise — gets SendMsg(0, colour) and joins the world render
        /// list. Owner byte +0x90 != 0: owner and clone are tinted halfway to the owner's colour bytes +0x95..+0x97
        /// instead (no SendMsg), every 20th tick. The return value is ignored by the case.
        /// </summary>
        public void Fn_b458c0(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint obj;
            if (M.U8(master + 0x90) != 0)
            {
                E3SnapHalfTint(rt, master, master);
                int t = m_stateCnt;
                if (t == 0x270e) m_stateCnt = 0;
                else if (t % 0x14 != 0) return;
                obj = NewHallu_b458c0(rt);
                E3SnapHalfTint(rt, obj, M.U32(A + 0x138));
                E3SnapAddToWorld(rt, obj);
                return;
            }

            rt.SetArgb(master, -1, FxOps.Cvtt(m_pos_x), FxOps.Cvtt(m_pos_y), FxOps.Cvtt(m_pos_z));
            int cnt = m_stateCnt;
            if (cnt == 0x270e)
            {
                m_stateCnt = 0;
                cnt = 0;
            }
            if (f_17c != 0x29 && cnt % 0x14 != 0) return;
            obj = NewHallu_b458c0(rt);
            long z = FxOps.Cvtt(m_pos_z);
            long y = FxOps.Cvtt(m_pos_y);
            long x = FxOps.Cvtt(m_pos_x);
            rt.HalluSendMsg(obj, A, 0, x, y, z);
            E3SnapAddToWorld(rt, obj);
        }

        /// <summary>fn_b458c0's clone: GetJob(owner) → FUN_00d02e40 → CHalluPC (0x510, ctor 0xcbe340) or CHalluNPC (0x450, 0xcbde20).</summary>
        uint NewHallu_b458c0(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            int job = rt.ActorJob_bd0cd0(master);
            uint obj;
            if (!FxRuntime.IsPlayerJob_d02e40(job))
            {
                obj = M.Alloc(0x450);
                rt.ConstructHallu(obj, M.U32(A + 0x138), FxRuntime.VtCHalluNPC);
            }
            else
            {
                obj = M.Alloc(0x510);
                rt.ConstructHallu(obj, M.U32(A + 0x138), FxRuntime.VtCHalluPC);
            }
            return obj;
        }

        /// <summary>
        /// CRagEffect::fn_b456b0 (0xb456b0) — owner byte +0x90 == 0: counter 9998 wraps to 0; every 5th tick (every tick
        /// when the duration is 30) a CHalluPC2 clone (0x510, ctor 0xcbe020) of the owner joins the world render list,
        /// untinted. Owner byte +0x90 != 0: owner and clone tinted halfway to the owner's colour bytes +0x95..+0x97,
        /// every 5th tick.
        /// </summary>
        public void Fn_b456b0(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint obj;
            if (M.U8(master + 0x90) != 0)
            {
                E3SnapHalfTint(rt, master, master);
                int t = m_stateCnt;
                if (t == 0x270e) m_stateCnt = 0;
                else if (t % 5 != 0) return;
                obj = M.Alloc(0x510);
                rt.ConstructHallu(obj, M.U32(A + 0x138), FxRuntime.VtCHalluPC2);
                E3SnapHalfTint(rt, obj, M.U32(A + 0x138));
                E3SnapAddToWorld(rt, obj);
                return;
            }
            int cnt = m_stateCnt;
            if (cnt == 0x270e)
            {
                m_stateCnt = 0;
                cnt = 0;
            }
            if (f_17c != 0x1e && cnt % 5 != 0) return;
            obj = M.Alloc(0x510);
            rt.ConstructHallu(obj, M.U32(A + 0x138), FxRuntime.VtCHalluPC2);
            E3SnapAddToWorld(rt, obj);
        }

        /// <summary>
        /// EF 1004 (0x3ec; case 0xb49714, EFST 656 / 1192): every tick a CHalluPC2 clone (0x510, ctor 0xcbe020) of the
        /// owner, placed 5 units behind it (x − sin(heading)·5, z + cos(heading)·5, heading = cvtt(owner +0x4c)),
        /// ARGB(100, 0xc0, 0xc0, 0xc0), joins the world render list.
        /// </summary>
        [FxEffectProcess(0x3ec)]
        public FxCaseResult Case_E10_1004(FxRuntime rt)
        {
            uint obj = M.Alloc(0x510);
            rt.ConstructHallu(obj, M.U32(A + 0x138), FxRuntime.VtCHalluPC2);
            uint master = M.U32(A + 0x138);
            M.U32(obj + 0x10, M.U32(master + 0x10));
            M.U32(obj + 0x14, M.U32(master + 0x14));
            M.U32(obj + 0x18, M.U32(master + 0x18));
            float s = FxMath.GetSin(FxOps.Cvtt(M.F32(master + 0x4c))) * 5.0f;
            M.F32(obj + 0x10, M.F32(obj + 0x10) - s);
            float c = FxMath.GetCos(FxOps.Cvtt(M.F32(M.U32(A + 0x138) + 0x4c))) * 5.0f;
            M.F32(obj + 0x18, c + M.F32(obj + 0x18));
            rt.SetArgb(obj, 100, 0xc0, 0xc0, 0xc0);
            E3SnapAddToWorld(rt, obj);
            return FxCaseResult.Continue;
        }
    }
}
