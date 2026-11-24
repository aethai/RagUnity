// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.Heal.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>Zeroes the 3 × (7 dwords + 7 bytes) sub-records of ring block <paramref name="block"/> (+0x3fc / +0x450 + block * 0xb4).</summary>
        void ClearRingBlockTrail(uint prim, uint block)
        {
            uint b = prim + block * 0xb4;
            for (uint j = 0; j < 3; j++)
                for (uint k = 0; k < 7; k++)
                {
                    M.U32(b + 0x3fc + j * 0x1c + k * 4, 0);
                    M.U8(b + 0x450 + j * 7 + k, 0);
                }
        }

        /// <summary>
        /// The "pok" sparkle prim of fn_b7c210 (type 0x3b): 4 blocks scattered ±6 around the effect; variant 1
        /// adds pok3.tga as second texture and uses a larger rise.
        /// </summary>
        void LaunchHealSparkles(FxRuntime rt, bool two)
        {
            Prim p = LaunchLast(rt, 0x3b);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            if (two)
            {
                p = m_lastPrim; p.f_204 = 2;
                p = m_lastPrim;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                uint t0 = rt.GetTexture("effect\\pok1.tga");
                M.U32((uint)m_lastPrim.f_144, t0);
                uint t1 = rt.GetTexture("effect\\pok3.tga");
                M.U32((uint)m_lastPrim.f_144 + 4, t1);
            }
            else SetPrimTexture(rt, m_lastPrim, "effect\\pok1.tga");
            p = m_lastPrim; p.f_318 = 0.0f;
            p = m_lastPrim;
            M.U32(p.A + 0x10, M.U32(A + 0x10));
            M.U32(p.A + 0x14, M.U32(A + 0x14));
            M.U32(p.A + 0x18, M.U32(A + 0x18));
            for (uint b = 0; b < 0x2d0; b += 0xb4)
            {
                uint a = m_lastPrim.A + b;
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, 0);
                M.F32(a + 0x468, (float)RandMod(rt, 10) * 0.1f + 2.0f);
                M.U32(a + 0x41c, 0);
                M.I16(a + 0x3ea, 0);
                M.F32(a + 0x46c, (float)RandMod(rt, 0xd) + m_pos_x - 6.0f);
                M.F32(a + 0x474, (float)RandMod(rt, 0xd) + m_pos_z - 6.0f);
                M.U32(a + 0x470, M.U32(A + 0x14));
                M.I16(a + 0x3f8, 0);
                M.U32(a + 0x400, 0);
                M.F32(a + 0x404, (float)RandMod(rt, 0x28) * 0.01f + (two ? 0.4f : 0.3f));
                M.I16(a + 0x3ee, 0);
                M.U8(a + 0x450, 0);
                M.U8(a + 0x451, (byte)(two ? 1 : 0));
                M.U8(a + 0x452, 0xb);
                M.U32(a + 0x418, 0);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b7c210 (0xb7c210) — Heal: at tick 0 a prim type 0x3c with two rising rings of
        /// <paramref name="texture"/> and the heal sound; then every 6th tick up to 50 (variant 0) / 25 (variant 1)
        /// a pok-sparkle prim.
        /// </summary>
        public void Fn_b7c210(FxRuntime rt, string texture, sbyte variant)
        {
            FollowMaster();
            int limit = variant != 0 ? 0x19 : 0x32;
            int t = m_stateCnt;
            if (t != 0)
            {
                if (t > limit || t % 6 != 0) return;
                if (variant == 0) LaunchHealSparkles(rt, false);
                else if (variant == 1) LaunchHealSparkles(rt, true);
                return;
            }

            Prim p = LaunchLast(rt, 0x3c);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, texture);
            if (variant == 0) { p = m_lastPrim; p.f_318 = 9.0f; }
            else if (variant == 1) { p = m_lastPrim; p.f_318 = 5.0f; }
            uint a = m_lastPrim.A;
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.I16(a + 0x3ee, 0x168);
            M.I16(m_lastPrim.A + 0x3f8, (short)RandMod(rt, 0x168));
            a = m_lastPrim.A;
            M.I16(a + 0x3ea, 0);
            M.I16(a + 0x466, 0x5a);
            M.U32(a + 0x470, 0);
            if (variant == 0)
            {
                M.I16(a + 0x3ec, 0x3c);
                M.F32(a + 0x3f0, 40.0f);
                M.F32(a + 0x468, 4.6f);
            }
            else if (variant == 1)
            {
                M.I16(a + 0x3ec, 0x1e);
                M.F32(m_lastPrim.A + 0x3f0, (float)RandMod(rt, 0x14) + 40.0f);
                M.F32(m_lastPrim.A + 0x468, 4.5f);
                int r = RandMod(rt, 100);
                a = m_lastPrim.A;
                M.F32(a + 0x468, (float)r * 0.005f + M.F32(a + 0x468));
            }
            ClearRingBlockTrail(m_lastPrim.A, 0);
            a = m_lastPrim.A;
            M.U8(a + 0x49c, 1);
            M.I32(a + 0x4a8, 0);
            M.I16(a + 0x4a2, 0x168);
            M.I16(m_lastPrim.A + 0x4ac, (short)RandMod(rt, 0x168));
            a = m_lastPrim.A;
            M.I16(a + 0x49e, 0);
            M.I16(a + 0x51a, 0x5a);
            M.U32(a + 0x524, 0);
            if (variant == 0)
            {
                M.I16(a + 0x4a0, 0x3c);
                M.F32(a + 0x4a4, 40.0f);
                M.F32(a + 0x51c, 4.8f);
            }
            else if (variant == 1)
            {
                M.I16(a + 0x4a0, 0x1e);
                M.U32(a + 0x4a4, M.U32(a + 0x3f0));
                M.F32(a + 0x51c, M.F32(a + 0x468) + 0.2f);
            }
            ClearRingBlockTrail(m_lastPrim.A, 1);
            M.U8(m_lastPrim.A + 0x550, 0);
            M.U8(m_lastPrim.A + 0x604, 0);
            rt.PlaySound3DRelative("_heal_effect.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
        }

        /// <summary>Case 312 (0x138, vf14) — Heal (alpha_down.tga, variant 0).</summary>
        [FxEffectProcess(0x138)]
        public FxCaseResult Case_Heal(FxRuntime rt)
        {
            Fn_b7c210(rt, "effect\\alpha_down.tga", 0);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 320 (0x140, vf14) — Heal, small variant (alpha_down.tga, variant 1).</summary>
        [FxEffectProcess(0x140)]
        public FxCaseResult Case_HealSmall(FxRuntime rt)
        {
            Fn_b7c210(rt, "effect\\alpha_down.tga", 1);
            return FxCaseResult.Continue;
        }
    }
}
