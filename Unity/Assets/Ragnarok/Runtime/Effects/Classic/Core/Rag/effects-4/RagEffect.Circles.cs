// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.Circles.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b6a920 (0xb6a920) — at tick 0 a prim type 0x72 with one active ring (block 0 of the
        /// +0x3e8 ring table) of <paramref name="texture"/>, random start angles and a random byte phase.
        /// </summary>
        public void Fn_b6a920(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x72);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, texture);
            uint a = m_lastPrim.A;
            M.F32(a + 0x318, 5.0f);
            M.U8(a + 0x3e8, 1);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
            M.F32(a + 0x468, 1.6f);
            M.I16(m_lastPrim.A + 0x466, (short)RandMod(rt, 0x168));
            M.I16(m_lastPrim.A + 0x3f8, (short)RandMod(rt, 0x168));
            M.I16(m_lastPrim.A + 0x3ea, 0);
            M.F32(m_lastPrim.A + 0x3fc, -12.0f);
            M.U8(m_lastPrim.A + 0x450, (byte)RandMod(rt, 0x1f));
        }

        /// <summary>Case 517 (0x205, vf15) — Wand of Hermode.</summary>
        [FxEffectProcess(0x205)]
        public FxCaseResult Case_WandOfHermode(FxRuntime rt)
        {
            Fn_b6a920(rt, "effect\\white02.bmp");
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b6d6e0 (0xb6d6e0) — at tick 0 a prim type 0x36 (shockwave_c.bmp) with four ring blocks
        /// 90° apart starting at <paramref name="angle"/>; lowers the effect by 9.
        /// </summary>
        public void Fn_b6d6e0(FxRuntime rt, int angle)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x36);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, "effect\\shockwave_c.bmp");
            m_pos_y = m_pos_y - 9.0f;
            for (int i = 0; i < 4; i++)
            {
                uint a = m_lastPrim.A + (uint)i * 0xb4;
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, 0);
                M.F32(a + 0x468, 100.0f);
                M.I16(a + 0x466, (short)(i * 0x5a + angle));
                M.I16(a + 0x3ea, 0);
            }
        }

        /// <summary>Case 300 (0x12c, vf14) — Biochemical Helm etc.: sound at tick 0, two shockwave ring prims.</summary>
        [FxEffectProcess(0x12c)]
        public FxCaseResult Case_ChemicalProtection(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("apocalips_attack.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            Fn_b6d6e0(rt, 0x1e);
            Fn_b6d6e0(rt, 0x3c);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b6b080 (0xb6b080) — at tick 0 a prim type 0x40 (ground circle of
        /// <paramref name="texture"/>); <paramref name="variant"/> selects radius / timing (0 Magnus, 1 Sanctuary, …).
        /// </summary>
        public void Fn_b6b080(FxRuntime rt, string texture, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x40);
            p.f_1c4 = 0;
            p = m_lastPrim; p.f_1c4 |= 1;
            p = m_lastPrim; p.f_1c4 |= 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, texture);
            M.F32(m_lastPrim.A + 0x318, 1.0f);
            M.U8(m_lastPrim.A + 0x3e8, 1);
            M.I32(m_lastPrim.A + 0x3f4, 0);
            M.I16(m_lastPrim.A + 0x3ee, (short)RandMod(rt, 0x168));
            uint a;
            if (variant == 0)
            {
                a = m_lastPrim.A;
                M.F32(a + 0x3f0, rt.MinEffect ? 10.0f : 50.0f);
                M.I16(a + 0x3f8, 0);
                M.I16(a + 0x3ea, 0x1e);
                M.I16(a + 0x3ec, 0x360);
                M.F32(a + 0x3fc, 5.0f);
                M.F32(a + 0x400, 5.0f);
            }
            else
            {
                a = m_lastPrim.A;
                if (variant == 1)
                {
                    M.F32(a + 0x3f0, 16.0f);
                    M.I16(a + 0x3f8, 0);
                    M.I16(a + 0x3ea, 0x78);
                    M.I16(a + 0x3ec, 0x758);
                }
                else if (variant == 2)
                {
                    M.F32(a + 0x3f0, 32.0f);
                    M.I16(a + 0x3f8, 0);
                    M.I16(a + 0x3ea, 0x1e);
                    M.I16(a + 0x3ec, 0x752f);
                }
                else if (variant == 4)
                {
                    M.F32(a + 0x3f0, 16.0f);
                    M.I16(a + 0x3f8, 0);
                    M.I16(a + 0x3ea, 0x78);
                    M.I16(a + 0x3ec, 0x758);
                    M.F32(a + 0x3fc, 2.5f);
                    M.F32(a + 0x400, 2.5f);
                    M.F32(a + 0x404, 0.0f);
                    M.F32(a + 0x408, (float)variant);
                    M.F32(m_lastPrim.A + 0x40c, (float)RandMod(rt, 0x32));
                    goto Tail;
                }
                else
                {
                    if (variant == 3) M.F32(a + 0x3f0, 16.0f);
                    M.I16(a + 0x3f8, 0);
                }
                M.F32(a + 0x3fc, 2.5f);
                M.F32(a + 0x400, 2.5f);
            }
            M.F32(a + 0x404, 0.0f);
            M.F32(a + 0x408, (float)variant);
        Tail:
            a = m_lastPrim.A;
            M.U32(a + 0x410, 0);
            M.U32(a + 0x414, 0);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
        }

        /// <summary>Case 318 (0x13e, vf14) — Magnus Exorcismus circle (ring_red.tga, variant 0).</summary>
        [FxEffectProcess(0x13e)]
        public FxCaseResult Case_MagnusCircle(FxRuntime rt)
        {
            Fn_b6b080(rt, "effect\\ring_red.tga", 0);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 319 (0x13f, vf14) — Sanctuary circle (alpha_down.tga, variant 1).</summary>
        [FxEffectProcess(0x13f)]
        public FxCaseResult Case_SanctuaryCircle(FxRuntime rt)
        {
            Fn_b6b080(rt, "effect\\alpha_down.tga", 1);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 311 (0x137, vf14) — Crazy Uproar: sound (UNK_00ff7688) at tick 0, then STR (loud.str).</summary>
        [FxEffectProcess(0x137)]
        public FxCaseResult Case_CrazyUproar(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\\u00b0\u00ed\u00bc\u00ba\u00b9\u00e6\u00b0\u00a1.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }
    }
}
