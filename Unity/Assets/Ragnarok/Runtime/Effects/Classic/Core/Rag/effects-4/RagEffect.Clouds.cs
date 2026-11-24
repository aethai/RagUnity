// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.Clouds.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b867b0 (0xb867b0) — at tick 0 a prim type 0x3e (cloud11.tga) with four cloud rings;
        /// <paramref name="variant"/> picks radius / height laws (0 Wind Walker, 1 Zen, 2/3 Tornado Kick inner/outer).
        /// </summary>
        public void Fn_b867b0(FxRuntime rt, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x3e);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, "effect\\cloud11.tga");
            p = m_lastPrim; p.f_318 = variant == 1 ? 8.0f : 5.0f;
            for (int i = 0; i < 4; i++)
            {
                uint a = m_lastPrim.A + (uint)i * 0xb4;
                float fi = (float)i;
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, 0);
                M.I16(a + 0x3ee, 0x1e);
                float radius;
                if (variant == 1) radius = fi + fi + 3.0f;
                else if (variant == 2) radius = 48.0f - fi * 5.0f;
                else if (variant == 3) radius = 28.0f - fi * 5.0f;
                else radius = fi + fi + 5.0f;
                M.F32(a + 0x3f0, radius);
                M.I16(a + 0x3f8, (short)(i * 0x5a));
                M.I16(a + 0x3ea, 0);
                float h;
                if (variant >= 2)
                {
                    M.I16(a + 0x3ec, 2);
                    if (variant == 2) h = 14.0f - (fi + fi);
                    else if (variant == 3) h = 6.0f - fi;
                    else h = (float)RandMod(rt, 0x65) * 0.03f + 4.5f;
                }
                else
                {
                    M.I16(a + 0x3ec, 1);
                    if (variant == 1) h = (float)RandMod(rt, 0x65) * 0.01f + 2.5f;
                    else h = (float)RandMod(rt, 0x65) * 0.03f + 4.5f;
                }
                M.F32(m_lastPrim.A + (uint)i * 0xb4 + 0x468, h);
                a = m_lastPrim.A + (uint)i * 0xb4;
                M.I16(a + 0x466, (short)(RandMod(rt, 0x15) + 0x50));
                for (uint j = 0; j < 3; j++)
                    for (uint k = 0; k < 7; k++)
                    {
                        M.F32(a + 0x3fc + j * 0x1c + k * 4, 1.0f);
                        M.U8(a + 0x450 + j * 7 + k, 0);
                    }
            }
        }

        /// <summary>
        /// CRagEffect::fn_b8bd10 (0xb8bd10) — at tick 0 a prim type 0x67 (one ring of <paramref name="texture"/>);
        /// <paramref name="variant"/> 10 = wide flat ring, 2 = alternative byte parameter.
        /// </summary>
        public void Fn_b8bd10(FxRuntime rt, string texture, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x67);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            p = m_lastPrim; p.f_204 = 1;
            SetPrimTexture(rt, m_lastPrim, texture);
            uint a = m_lastPrim.A;
            M.F32(a + 0x318, 0.0f);
            M.U8(a + 0x3e8, 0);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.F32(a + 0x3f0, 40.0f);
            M.I16(a + 0x3ea, 0);
            M.I16(m_lastPrim.A + 0x3f8, (short)RandMod(rt, 0x168));
            a = m_lastPrim.A;
            M.F32(a + 0x468, 2.0f);
            M.F32(a + 0x3fc, 50.0f);
            M.F32(a + 0x400, 5.0f);
            M.U8(a + 0x450, 10);
            M.I8(a + 0x454, variant);
            M.U8(a + 0x455, 0);
            if (variant == 10)
            {
                M.F32(a + 0x3f0, 14.0f);
                M.F32(a + 0x3fc, 70.0f);
                M.U8(a + 0x450, 0xe);
                M.F32(a + 0x468, 0.0f);
                M.U8(a + 0x455, 1);
                return;
            }
            if (variant == 2) M.U8(a + 0x455, 10);
        }

        /// <summary>
        /// Case 389 (0x185) — Wind Walker: clouds, sound at tick 0; for t in [5, 26) flag 1 and a green tint.
        /// </summary>
        [FxEffectProcess(0x185)]
        public FxCaseResult Case_WindWalker(FxRuntime rt)
        {
            Fn_b867b0(rt, 0);
            int t = m_stateCnt;
            if (t == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\\u00c0\u00a9\u00b5\u00e5\u00bf\u00f6\u00c5\u00a9.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if ((uint)(t - 5) < 0x15)
            {
                AddMasterFlag(1);
                MasterArgb(rt, -1, 0xdc, 0xfa, 0xdc);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Case 402 (0x192) — Zen: clouds (variant 1); for t in [5, 66) flag 1 and a warm tint.</summary>
        [FxEffectProcess(0x192)]
        public FxCaseResult Case_Zen(FxRuntime rt)
        {
            Fn_b867b0(rt, 1);
            if ((uint)(m_stateCnt - 5) < 0x3d)
            {
                AddMasterFlag(1);
                MasterArgb(rt, -1, 0xfa, 0xfa, 200);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// Case 435 (0x1b3) — Tornado Kick: sound at tick 5, storm ring + two cloud rings; the owner spins
        /// 30°/tick while t &lt; 48.
        /// </summary>
        [FxEffectProcess(0x1b3)]
        public FxCaseResult Case_TornadoKick(FxRuntime rt)
        {
            if (m_stateCnt == 5)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\T_\u00c8\u00b8\u00bf\u00c0\u00b8\u00ae\u00c2\u00f7\u00b1\u00e2.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            Fn_b8bd10(rt, "effect\\storm2.tga", 0);
            Fn_b867b0(rt, 2);
            Fn_b867b0(rt, 3);
            if (m_stateCnt < 0x30)
            {
                uint master = M.U32(A + 0x138);
                M.F32(master + 0x4c, M.F32(master + 0x4c) + 30.0f);
                float r = M.F32(M.U32(A + 0x138) + 0x4c);
                if (360.0f < r) M.F32(M.U32(A + 0x138) + 0x4c, r - 360.0f);
            }
            return FxCaseResult.Continue;
        }
    }
}
