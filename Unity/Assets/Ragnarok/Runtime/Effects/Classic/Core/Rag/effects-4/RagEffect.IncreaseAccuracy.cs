// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.IncreaseAccuracy.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b6e0f0 (0xb6e0f0) — rising "agility" sparkles: screen anchor + owner position every
        /// tick, sound at tick 0 (Soul Change wav for variant 3), and on even ticks one ac_center2.tga prim
        /// (type 0x10) at a random angle; variants ≥ 2 rise faster and larger.
        /// </summary>
        public void Fn_b6e0f0(FxRuntime rt, sbyte variant)
        {
            ProjectMasterToF154(rt);
            FollowMaster();
            if (m_stateCnt == 0)
                rt.PlaySound3DRelative(variant == 3 ? "effect\\\u00bc\u00d2\u00bf\u00ef \u00c3\u00bc\u00c0\u00ce\u00c1\u00f6.wav" : "effect\\EF_IncAgility.wav",
                    m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            if ((m_stateCnt & 1) != 0) return;
            Prim p = LaunchLast(rt, 0x10);
            p.m_duration = 0x32;
            p = m_lastPrim;
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(RandMod(rt, 0x168)));
            p = m_lastPrim;
            float r = (float)(RandMod(rt, 7) + 2);
            p.f_394 = p.f_16c * 0.0f + p.f_160 * 0.0f + r * p.f_178 + p.f_184;
            p.f_398 = p.f_170 * 0.0f + p.f_164 * 0.0f + r * p.f_17c + p.f_188;
            p.f_39c = p.f_174 * 0.0f + p.f_168 * 0.0f + r * p.f_180 + p.f_18c;
            p = m_lastPrim;
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(0x5a));
            int speed = rt.Rand.Next() % 0x32;
            if (variant >= 2)
            {
                p = m_lastPrim; p.f_2bc = (float)((speed >= 0 ? speed : 0) + 0x28) / 100.0f;
                int w = RandMod(rt, 100);
                p = m_lastPrim; p.f_354 = (float)(w + 0x28) / 10.0f;
                int h = RandMod(rt, 6);
                p = m_lastPrim; p.f_360 = (float)h * 0.05f + 0.1f;
            }
            else
            {
                p = m_lastPrim; p.f_2bc = (float)((speed >= 0 ? speed : 0) + 0x14) / 100.0f;
                int w = RandMod(rt, 100);
                p = m_lastPrim; p.f_354 = (float)(w + 0x14) / 10.0f;
                int h = rt.Rand.Next() & 3;
                p = m_lastPrim; p.f_360 = (float)h * 0.1f + 0.1f;
            }
            p = m_lastPrim; p.f_324 = 0.0f;
            p = m_lastPrim; p.f_2d4 = 60.0f;
            p = m_lastPrim; p.f_328 = p.f_2d4 / 20.0f;
            p = m_lastPrim; p.f_1f0 = p.m_duration - 0x14;
            SetPrimTexture(rt, m_lastPrim, "effect\\ac_center2.tga");
        }

        /// <summary>Case 456 (0x1c8) — Increasing Accuracy.</summary>
        [FxEffectProcess(0x1c8)]
        public FxCaseResult Case_IncreasingAccuracy(FxRuntime rt)
        {
            Fn_b6e0f0(rt, 0);
            return FxCaseResult.Continue;
        }
    }
}
