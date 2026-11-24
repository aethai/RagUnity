// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.SandAttack.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b82980 (0xb82980) — at tick 0 one prim type 0x3b (thunder_center.bmp) at the effect
        /// position with four random sand-grain blocks scattered ±4 around it.
        /// </summary>
        public void Fn_b82980(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x3b);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, "effect\\thunder_center.bmp");
            p = m_lastPrim; p.f_318 = 0.0f;
            p = m_lastPrim;
            M.U32(p.A + 0x10, M.U32(A + 0x10));
            M.U32(p.A + 0x14, M.U32(A + 0x14));
            M.U32(p.A + 0x18, M.U32(A + 0x18));
            for (uint b = 0; b < 0x2d0; b += 0xb4)
            {
                uint a = m_lastPrim.A + b;
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, RandMod(rt, 10) - 10);
                M.F32(a + 0x468, (float)(rt.Rand.Next() & 0xf) * 0.03f + 0.1f);
                M.U32(a + 0x41c, 0);
                M.I16(a + 0x3ea, 0);
                M.F32(a + 0x46c, (float)RandMod(rt, 9) + m_pos_x - 4.0f);
                M.F32(a + 0x474, (float)RandMod(rt, 9) + m_pos_z - 4.0f);
                M.U32(a + 0x470, M.U32(A + 0x14));
                M.I16(a + 0x3f8, 0);
                M.F32(a + 0x400, (float)RandMod(rt, 10) * 0.01f);
                M.F32(a + 0x404, (float)RandMod(rt, 0x28) * 0.01f + 0.2f);
                M.I16(a + 0x3ee, 0);
                M.U8(a + 0x450, 0);
                M.U8(a + 0x451, 0);
                M.U8(a + 0x452, 10);
                M.U32(a + 0x418, 0);
            }
        }

        /// <summary>Case 310 (0x136, vf14) — Sand Attack: 20 sand prims at tick 0.</summary>
        [FxEffectProcess(0x136)]
        public FxCaseResult Case_SandAttack(FxRuntime rt)
        {
            for (int i = 0x14; i != 0; i--) Fn_b82980(rt);
            return FxCaseResult.Continue;
        }
    }
}
