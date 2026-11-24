// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.Streaks.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b7e480 (0xb7e480) — for t &lt; 4 one prim type 0x46 (white02.bmp) with four random streak
        /// blocks (start delay, spin, radius 6..13) anchored at the effect position; <paramref name="variant"/>
        /// goes to each block's +0x3ec.
        /// </summary>
        public void Fn_b7e480(FxRuntime rt, sbyte variant)
        {
            FollowMaster();
            if ((uint)m_stateCnt >= 4) return;
            Prim p = LaunchLast(rt, 0x46);
            p.f_1c4 = 0;
            p = m_lastPrim; p.f_1c4 |= 1;
            p = m_lastPrim; p.f_1c4 |= 5;
            p = m_lastPrim; p.m_duration = f_17c;
            p = m_lastPrim; p.f_204 = 1;
            SetPrimTexture(rt, m_lastPrim, "effect\\white02.bmp");
            uint a = m_lastPrim.A;
            M.F32(a + 0x318, 0.0f);
            M.U8(a + 0x3e8, 0);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
            for (uint b = 0; b < 0x2d0; b += 0xb4)
            {
                a = m_lastPrim.A + b;
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, -RandMod(rt, 0x1a));
                M.F32(a + 0x3f0, 0.1f);
                M.I16(a + 0x3ea, (short)(RandMod(rt, 0x33) + 0x7d));
                M.I16(a + 0x466, (short)RandMod(rt, 0x168));
                M.F32(a + 0x468, (float)(rt.Rand.Next() & 7) + 6.0f);
                M.U8(a + 0x450, 0);
                M.U32(a + 0x3fc, 0);
                M.U32(a + 0x46c, M.U32(A + 0x10));
                M.U32(a + 0x470, M.U32(A + 0x14));
                M.U32(a + 0x474, M.U32(A + 0x18));
                for (uint k = 0; k < 20; k++)
                {
                    M.U32(a + 0x400 + k * 4, 0);
                    M.U8(a + 0x451 + k, 0);
                }
                M.I16(a + 0x3ec, variant);
            }
        }

        /// <summary>Case 330 (0x14a, vf14) — Raging Thrust.</summary>
        [FxEffectProcess(0x14a)]
        public FxCaseResult Case_RagingThrust(FxRuntime rt)
        {
            Fn_b7e480(rt, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 376 (0x178) — Raging Palm Strike: streaks (variant 2); t in [10, 21) flag 8 + orange tint.</summary>
        [FxEffectProcess(0x178)]
        public FxCaseResult Case_RagingPalmStrike(FxRuntime rt)
        {
            Fn_b7e480(rt, 2);
            if ((uint)(m_stateCnt - 10) < 0xb)
            {
                AddMasterFlag(8);
                MasterArgb(rt, -1, 0xff, 0x78, 0x32);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b7e850 (0xb7e850) — at tick 0 one prim type 0x2b (white01/white03.bmp) with four random
        /// blocks aimed at the target position f_180/f_184/f_188; lowers the effect by 9.
        /// </summary>
        public void Fn_b7e850(FxRuntime rt, sbyte variant)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchLast(rt, 0x2b);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, variant == 4 ? "effect\\white03.bmp" : "effect\\white01.bmp");
            m_pos_y = m_pos_y - 9.0f;
            for (uint b = 0; b < 0x2d0; b += 0xb4)
            {
                uint a = m_lastPrim.A + b;
                M.U8(a + 0x3e8, 1);
                M.U32(a + 0x46c, M.U32(A + 0x180));
                M.U32(a + 0x470, M.U32(A + 0x184));
                M.U32(a + 0x474, M.U32(A + 0x188));
                if (variant == 2)
                {
                    M.I32(a + 0x3f4, -RandMod(rt, 0x1a) * 2);
                    M.U8(a + 0x450, 2);
                    M.F32(a + 0x400, 2.5f);
                }
                else if (variant == 3 || variant == 4)
                {
                    M.I32(a + 0x3f4, variant == 3 ? -0x46 - RandMod(rt, 0x1e) : -0x1e - RandMod(rt, 0x14));
                    M.U8(a + 0x450, (byte)(rt.Rand.Next() % 3 + 3));
                    M.F32(a + 0x400, variant == 3 ? 4.0f : 3.0f);
                }
                else
                {
                    M.I32(a + 0x3f4, -0x46 - RandMod(rt, 0x1e));
                    M.U8(a + 0x450, 0);
                    M.F32(a + 0x400, 3.5f);
                }
                M.F32(a + 0x468, 12.0f);
                M.F32(a + 0x3fc, 1.5f);
                M.I16(a + 0x466, (short)RandMod(rt, 0x168));
                M.I16(a + 0x3ee, (short)RandMod(rt, 0x168));
                M.I16(a + 0x3f8, (short)RandMod(rt, 0x168));
                M.F32(a + 0x404, (float)RandMod(rt, 0x168));
                M.I16(a + 0x3ea, 0);
            }
        }

        /// <summary>Case 227 (0xe3) — Snatch: sounds at ticks 0 and 35, 20 streak prims at tick 0.</summary>
        [FxEffectProcess(0xe3)]
        public FxCaseResult Case_Snatch(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\EF_Bash.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t == 0x23)
                rt.PlaySound3DRelative("effect\\rog_intimidate.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            for (int i = 0x14; i != 0; i--) Fn_b7e850(rt, 0);
            return FxCaseResult.Continue;
        }
    }
}
