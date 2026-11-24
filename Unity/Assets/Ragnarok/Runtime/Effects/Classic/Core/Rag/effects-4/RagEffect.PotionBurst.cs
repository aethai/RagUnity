// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.PotionBurst.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b82e10 (0xb82e10) — at tick 0 a prim type 0x57 (four rotating billboards of
        /// <paramref name="texture"/>, e.g. a potion icon or cross) above the owner; <paramref name="variant"/>
        /// selects size, height and the per-block byte parameters.
        /// </summary>
        public void Fn_b82e10(FxRuntime rt, string texture, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x57);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, texture);
            uint a = m_lastPrim.A;
            M.F32(a + 0x318, 0.0f);
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.F32(a + 0x468, variant == 0 ? 12.0f : variant == 4 ? 14.0f : 8.0f);
            M.I16(a + 0x3ea, 0);
            M.U32(a + 0x484, 0);
            M.U32(a + 0x48c, 0);
            if (variant == 0) M.F32(a + 0x488, -115.0f);
            else if (variant == 4) M.F32(a + 0x488, -115.0f - (float)(rt.Rand.Next() % 0x38));
            else M.F32(a + 0x488, -80.0f);
            M.I16(a + 0x3f8, 0);
            M.F32(a + 0x400, 3.0f);
            M.I16(a + 0x3ee, (short)RandMod(rt, 0x168));
            M.U8(a + 0x450, 0);
            M.U8(a + 0x452, 7);
            if (variant == 1) { M.U8(a + 0x452, 9); M.U8(a + 0x453, 1); }
            else if (variant == 2) { M.U8(a + 0x452, 9); M.U8(a + 0x453, 3); }
            else if (variant == 3) { M.U8(a + 0x452, 9); M.U8(a + 0x453, 4); }
            M.I8(a + 0x454, variant);
            M.U8(a + 0x49c, 1);
            M.I16(a + 0x49e, 0);
            M.U8(a + 0x550, 1);
            M.I16(a + 0x552, 0);
            M.U8(a + 0x604, 1);
            M.I16(a + 0x606, 0);
        }

        /// <summary>
        /// CRagEffect::fn_b7a040 (0xb7a040) — at tick 0 a prim type 0x58 (burst of <paramref name="texture"/>)
        /// with start offset <paramref name="start"/>; <paramref name="variant"/> selects the size code in f_318.
        /// </summary>
        public void Fn_b7a040(FxRuntime rt, string texture, int start, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x58);
            p.f_1c4 = 0;
            p = m_lastPrim; p.f_1c4 |= 1;
            p = m_lastPrim; p.f_1c4 |= 5;
            p = m_lastPrim; p.m_duration = f_17c;
            p = m_lastPrim; p.f_204 = 1;
            SetPrimTexture(rt, m_lastPrim, texture);
            uint a = m_lastPrim.A;
            if (variant == 3)
            {
                M.F32(a + 0x318, 111.0f);
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, start);
                M.F32(a + 0x468, 13.0f);
                M.F32(m_lastPrim.A + 0x3fc, -5.0f - (float)(rt.Rand.Next() % 6));
            }
            else
            {
                if (variant == 1) M.F32(a + 0x318, 101.0f);
                else if (variant == 2) M.F32(a + 0x318, 102.0f);
                else if (variant == 4) M.F32(a + 0x318, 113.0f);
                else if (variant == 10) M.F32(a + 0x318, 114.0f);
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, start);
                M.F32(a + 0x468, 15.0f);
                M.F32(a + 0x3fc, -5.0f);
            }
            a = m_lastPrim.A;
            M.I16(a + 0x466, 0);
            M.I16(a + 0x3ea, 0);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
        }

        /// <summary>Case 365 (0x16d, vf14) — Gloria Domini (Pressure): sound at tick 3, cross + explosion bursts.</summary>
        [FxEffectProcess(0x16d)]
        public FxCaseResult Case_Pressure(FxRuntime rt)
        {
            if (m_stateCnt == 3)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\\u00c7\u00c1\u00b7\u00b9\u00bc\u00c5.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            Fn_b82e10(rt, "effect\\cross_old.bmp", 0);
            Fn_b7a040(rt, "effect\\explosive_1_128.bmp", -0x1c, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 497 (0x1f1, vf15) — Twilight Alchemy 1: red slim potion.</summary>
        [FxEffectProcess(0x1f1)]
        public FxCaseResult Case_TwilightAlchemy1(FxRuntime rt)
        {
            Fn_b82e10(rt, "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00b7\u00b9\u00b5\u00e5\u00bd\u00bd\u00b8\u00b2\u00c6\u00f7\u00bc\u00c7.bmp", 1);
            Fn_b7a040(rt, "effect\\bbbb.bmp", -0x10, 1);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 498 (0x1f2, vf15) — Twilight Alchemy 2: yellow slim potion.</summary>
        [FxEffectProcess(0x1f2)]
        public FxCaseResult Case_TwilightAlchemy2(FxRuntime rt)
        {
            Fn_b82e10(rt, "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00bf\u00bb\u00b7\u00ce\u00bf\u00ec\u00bd\u00bd\u00b8\u00b2\u00c6\u00f7\u00bc\u00c7.bmp", 2);
            Fn_b7a040(rt, "effect\\bbbb.bmp", -0x10, 2);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 499 (0x1f3, vf15) — Twilight Alchemy 3: white slim potion.</summary>
        [FxEffectProcess(0x1f3)]
        public FxCaseResult Case_TwilightAlchemy3(FxRuntime rt)
        {
            Fn_b82e10(rt, "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00c8\u00ad\u00c0\u00cc\u00c6\u00ae\u00bd\u00bd\u00b8\u00b2\u00c6\u00f7\u00bc\u00c7.bmp", 3);
            Fn_b7a040(rt, "effect\\bbbb.bmp", -0x10, 0);
            return FxCaseResult.Continue;
        }
    }
}
