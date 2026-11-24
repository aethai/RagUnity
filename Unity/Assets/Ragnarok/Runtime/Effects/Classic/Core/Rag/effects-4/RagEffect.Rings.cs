// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.Rings.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b88410 (0xb88410) — at tick 0 launches a prim type 0x1e (four rotating rings of
        /// <paramref name="texture"/>) whose size / radii depend on <paramref name="variant"/>; stretches the
        /// effect to at least 56 ticks. Translated from the asm (word/byte field widths as stored there).
        /// </summary>
        public void Fn_b88410(FxRuntime rt, short angle, string texture, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            if (f_17c < 0x38) f_17c = 0x38;
            Prim p = LaunchLast(rt, 0x1e);
            if (p.IsNull) return;
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, texture);
            p = m_lastPrim;
            float size;
            switch (variant)
            {
                case 0x1: size = 8.0f; break;
                case 0x2: size = 5.0f; break;
                case 0x3: size = 10.0f; break;
                case 0x4: size = 11.0f; break;
                case 0x5: size = 4.0f; break;
                case 0x6: size = 12.0f; break;
                case 0xf: size = 7.0f; break;
                case 0x14: size = 24.0f; break;
                case 0x15: size = 25.0f; break;
                case 0x16: size = 26.0f; break;
                case 0x17: size = 27.0f; break;
                case 0x1f: size = 31.0f; break;
                case 0x2a: size = 42.0f; break;
                case 0x2b: size = 43.0f; break;
                case 0x2c: size = 44.0f; break;
                case 0x2d: size = 45.0f; break;
                case 0x2e: size = 46.0f; break;
                case 0x2f: size = 47.0f; break;
                case 0x31: size = 49.0f; break;
                case 0x32: size = 50.0f; break;
                case 0x30: size = 48.0f; break;   // the client stores 5.0 first, then 48.0
                default: size = 5.0f; break;
            }
            p.f_318 = size;

            short v = variant;
            uint a = m_lastPrim.A;
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.I16(a + 0x3ee, 0x168);
            M.F32(a + 0x3f0, variant == 2 ? 25.0f : variant == 0x16 ? 15.0f : 20.0f);
            M.I16(a + 0x3f8, 0xb4);
            M.I16(a + 0x3ea, (short)(angle + 0x87));
            M.F32(a + 0x468, 4.1f);
            M.I16(a + 0x466, 0x50);
            M.I16(a + 0x3ec, v);
            M.U32(a + 0x470, 0);

            M.U8(a + 0x49c, 1);
            M.I32(a + 0x4a8, 0);
            M.I16(a + 0x4a2, 0x168);
            M.F32(a + 0x4a4, variant == 2 ? 24.0f : variant == 0x16 ? 14.0f : 19.0f);
            M.I16(a + 0x4ac, 0x10e);
            M.I16(a + 0x49e, (short)(angle + 0x5a));
            M.F32(a + 0x51c, 4.1f);
            M.I16(a + 0x51a, 0x50);
            M.I16(a + 0x4a0, v);
            M.U32(a + 0x524, 0);

            M.U8(a + 0x550, 1);
            M.I32(a + 0x55c, 0);
            M.I16(a + 0x556, 0x168);
            M.F32(a + 0x558, variant == 2 ? 23.0f : variant == 0x16 ? 13.0f : 18.0f);
            M.I16(a + 0x560, 0);
            M.I16(a + 0x552, (short)(angle + 0x2d));
            M.F32(a + 0x5d0, 4.1f);
            M.I16(a + 0x5ce, 0x50);
            M.I16(a + 0x554, v);
            M.U32(a + 0x5d8, 0);

            M.U8(a + 0x604, 1);
            M.I32(a + 0x610, 0);
            M.I16(a + 0x60a, 0x168);
            M.F32(a + 0x60c, variant == 2 ? 22.0f : variant == 0x16 ? 12.0f : 17.0f);
            M.I16(a + 0x614, 0x5a);
            M.I16(a + 0x606, angle);
            M.F32(a + 0x684, 4.1f);
            M.I16(a + 0x682, 0x50);
            M.I16(a + 0x608, v);
            M.U32(a + 0x68c, 0);

            int start = 0;
            for (uint block = 0; block < 4 * 0xb4; block += 0xb4, start -= 5)
            {
                for (uint j = 0; j < 3; j++)
                {
                    for (uint k = 0; k < 7; k++)
                    {
                        M.U32(a + block + 0x3fc + j * 0x1c + k * 4, 0);
                        M.U8(a + block + 0x450 + j * 7 + k, 0);
                    }
                }
                if (f_17c <= 0x3c)
                {
                    M.I16(a + block + 0x3ea, 0);
                    M.I32(a + block + 0x3f4, start);
                }
            }
        }

        /// <summary>Case 12 (0xc) — Fury / Zen: sound + two yellow ring prims at tick 0.</summary>
        [FxEffectProcess(0xc)]
        public FxCaseResult Case_Fury(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt == 0)
            {
                rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Fn_b88410(rt, 0x2d, "effect\\ring_yellow.tga", 1);
                Fn_b88410(rt, 0x19, "effect\\ring_yellow.tga", 1);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>CRagEffect::fn_b66cf0 (0xb66cf0) — Aura Blade: white and yellow rings (variant 0x16) at tick 0.</summary>
        public void Fn_b66cf0(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt == 0)
            {
                Fn_b88410(rt, 0x2d, "effect\\ring_white.tga", 0x16);
                Fn_b88410(rt, 0x19, "effect\\ring_yellow.tga", 0x16);
            }
        }

        /// <summary>Case 367 (0x16f, vf14) — Aura Blade.</summary>
        [FxEffectProcess(0x16f)]
        public FxCaseResult Case_AuraBlade(FxRuntime rt)
        {
            Fn_b66cf0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>CRagEffect::fn_b6a440 (0xb6a440) — Comfort of the Sun/Moon/Stars: sound (y -100) and blue rings (variant 5) at tick 0.</summary>
        public void Fn_b6a440(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt == 0)
            {
                rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, -100.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Fn_b88410(rt, 0x2d, "effect\\ring_blue.tga", 5);
                Fn_b88410(rt, 0x19, "effect\\ring_blue.tga", 5);
            }
        }

        /// <summary>Case 441 (0x1b9).</summary>
        [FxEffectProcess(0x1b9)]
        public FxCaseResult Case_ComfortOfSun(FxRuntime rt)
        {
            Fn_b6a440(rt);
            return FxCaseResult.Continue;
        }
    }
}
