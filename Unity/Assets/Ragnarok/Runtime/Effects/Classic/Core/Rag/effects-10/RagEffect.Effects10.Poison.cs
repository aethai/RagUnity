// Synced from reference/port/src/RoClassicFx/Rag/effects-10/RagEffect.Effects10.Poison.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// EF 335 (0x14f, vf14 case 0xb467ec): fn_b82c60(this, 0) — the local player's poison mark (opt2 POISON 0x1 /
        /// DPOISON 0x80), duration 99999999.
        /// </summary>
        [FxEffectProcess(0x14f)]
        public FxCaseResult Case_E10_Poison(FxRuntime rt) { Fn_b82c60(rt, 0); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b82c60 (0xb82c60, thiscall (kind), ret 4) — follows the owner; at tick 0 (or when the
        /// counter reaches 99999998: restart with duration 99999999) launches one prim 0x48 (a 10 × 6 grid of 30-unit
        /// ground quads, lives as long as the effect) with effect\poison_f.bmp (kind 0) or effect\white02.bmp (kind ≠ 0),
        /// lowers the effect position by 9 and stores the kind as the prim's colour selector (+0x45a).
        /// </summary>
        public void Fn_b82c60(FxRuntime rt, sbyte kind)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t == 0x5f5e0fe)
            {
                m_stateCnt = 0;
                f_17c = 0x5f5e0ff;
            }
            else if (t != 0) return;

            Prim p = LaunchEffectPrim(rt, 0x48, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = p.f_1c4 | 0x200;
            { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
            { Prim lp = m_lastPrim; lp.f_204 = 1; }
            { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
            uint tex = rt.GetTexture(kind == 0 ? "effect\\poison_f.bmp" : "effect\\white02.bmp");
            M.U32((uint)m_lastPrim.f_144, tex);
            m_pos_y = m_pos_y - 9.0f;
            M.U8(m_lastPrim.A + 0x3e8, 0);
            M.U8(m_lastPrim.A + 0x49c, 0);
            M.U8(m_lastPrim.A + 0x550, 0);
            M.U8(m_lastPrim.A + 0x604, 0);
            M.U8(m_lastPrim.A + 0x3e8, 1);
            M.I32(m_lastPrim.A + 0x3f4, 0);
            M.F32(m_lastPrim.A + 0x468, 30.0f);
            M.F32(m_lastPrim.A + 0x3f0, 0.0f);
            M.U16(m_lastPrim.A + 0x3ea, 0);
            M.U8(m_lastPrim.A + 0x45a, (byte)kind);
        }
    }
}
