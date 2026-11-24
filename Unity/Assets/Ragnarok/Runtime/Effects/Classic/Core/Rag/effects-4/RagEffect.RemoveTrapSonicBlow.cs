// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.RemoveTrapSonicBlow.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b5d330 (0xb5d330) — Remove Trap (EF 100): sound at tick 0; at ticks 0 and 7 twelve
        /// particle1 sprites (type 7) at random angles.
        /// </summary>
        public void Fn_b5d330(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                rt.PlaySound3DRelative("effect\\hunter_removetrap.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t != 0 && t != 7) return;
            for (int i = 0xc; i != 0; i--)
            {
                Prim p = LaunchLast(rt, 7);
                p.m_duration = 0x32;
                p = m_lastPrim; p.f_34c = 0.2f;
                rt.Rand.Next();
                p = m_lastPrim; p.f_2ec = -2.0f;
                int d = RandMod(rt, 10);
                p = m_lastPrim; p.f_2f0 = (float)d / 1000.0f + 0.08f;
                int ang = RandMod(rt, 0x168);
                p = m_lastPrim; p.f_30c = (float)ang;
                p = m_lastPrim; p.f_318 = 0.8f;
                p = m_lastPrim;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                p = m_lastPrim;
                float r = p.f_348;
                p.f_394 = p.f_16c * 0.0f + p.f_160 * 0.0f + r * p.f_178 + p.f_184;
                p.f_398 = p.f_170 * 0.0f + p.f_164 * 0.0f + r * p.f_17c + p.f_188;
                p.f_39c = p.f_174 * 0.0f + p.f_168 * 0.0f + r * p.f_180 + p.f_18c;
                p = m_lastPrim; p.f_1f0 = 0x23;
                rt.SetSprName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle1.spr");
                rt.SetActName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle1.act");
                m_lastPrim.SnapshotTrail_ad58d0();
            }
        }

        /// <summary>Case 100 (0x64) — Remove Trap.</summary>
        [FxEffectProcess(0x64)]
        public FxCaseResult Case_RemoveTrap(FxRuntime rt)
        {
            Fn_b5d330(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b607d0 (0xb607d0) — Sonic Blow (EF 122): sound and, at tick 0, one slash (prim 0xd,
        /// magic_red.tga) oriented against the owner's direction (owner +0x4c) with a random offset.
        /// </summary>
        public void Fn_b607d0(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\assasin_sonicblow.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            if (m_stateCnt != 0) return;
            int dir = FxOps.Cvtt(180.0f - M.F32(M.U32(A + 0x138) + 0x4c));
            int r1 = rt.Rand.Next() % 10;
            int r2 = rt.Rand.Next() % 0x14;
            rt.Rand.Next();
            Prim p = LaunchLast(rt, 0xd);
            p.m_duration = 0xc;
            int ang = dir < 0 ? dir + 0x168 : dir;
            p = m_lastPrim; p.f_30c = (float)ang;
            p = m_lastPrim; p.f_310 = -90.0f;
            p = m_lastPrim;
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
            p = m_lastPrim;
            FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            p = m_lastPrim;
            p.f_370 = p.f_160 * 0.0f - p.f_16c * 2.0f + p.f_178 * 0.0f + p.f_184;
            p.f_374 = p.f_164 * 0.0f - p.f_170 * 2.0f + p.f_17c * 0.0f + p.f_188;
            p.f_378 = p.f_168 * 0.0f - p.f_174 * 2.0f + p.f_180 * 0.0f + p.f_18c;
            p = m_lastPrim;
            p.f_388 = p.f_370 + p.f_388;
            p.f_38c = p.f_374 + p.f_38c;
            p.f_390 = p.f_378 + p.f_390;
            p = m_lastPrim;
            int a = (r1 >= 0 ? r1 : 0) + 1;
            p.f_39c = 0.0f;
            p.f_394 = (float)(a / 4);
            int b = (r2 >= 0 ? r2 : 0) + 1;
            p.f_398 = (float)(b / 2 - 0xb);
            p = m_lastPrim; p.f_2bc = 0.4f;
            p = m_lastPrim; p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
            p = m_lastPrim; p.f_2f8 = 7.0f;
            p = m_lastPrim; p.f_2f4 = 4.0f;
            p = m_lastPrim; p.f_360 = 3.5f;
            p = m_lastPrim; p.f_1f0 = p.m_duration - p.m_duration / 2;
            SetPrimTexture(rt, m_lastPrim, "effect\\magic_red.tga");
        }

        /// <summary>Case 122 (0x7a) — Sonic Blow.</summary>
        [FxEffectProcess(0x7a)]
        public FxCaseResult Case_SonicBlow(FxRuntime rt)
        {
            Fn_b607d0(rt);
            return FxCaseResult.Continue;
        }
    }
}
