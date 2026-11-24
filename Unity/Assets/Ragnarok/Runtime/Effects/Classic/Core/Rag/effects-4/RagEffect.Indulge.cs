// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.Indulge.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b74f20 (0xb74f20) — for t ≤ 4, three particle1 sprites (type 8; tilt -90/0/90) facing a
        /// random point near the effect (FUN_00bce590), tinted by <paramref name="variant"/> (0 greenish white,
        /// 1 grey-white, 2 dark grey-blue).
        /// </summary>
        public void Fn_b74f20(FxRuntime rt, sbyte variant)
        {
            if (m_stateCnt > 4) return;
            for (int tilt = -0x5a; tilt < 0xb4; tilt += 0x5a)
            {
                Prim p = LaunchLast(rt, 8);
                p.m_duration = 0x3c;
                p = m_lastPrim;
                p.f_394 = 0.0f;
                p.f_398 = -10.0f;
                p.f_39c = 0.0f;
                float dist = rt.DistanceXZ(m_lastPrim.A, m_pos_x, m_pos_z);
                p = m_lastPrim; p.f_348 = dist;
                uint prim = m_lastPrim.A;
                float z = (float)RandMod(rt, 7) + m_pos_z - 3.0f;
                float x = (float)RandMod(rt, 7) + m_pos_x - 3.0f;
                rt.FaceTowards(prim, x, z);
                p = m_lastPrim; p.f_30c = -M.F32(p.A + 0x4c);
                p = m_lastPrim;
                float v = p.f_348 / (float)p.m_duration;
                p.f_2bc = v + v;
                p = m_lastPrim; p.f_2ec = 2.0f;
                p = m_lastPrim;
                v = -(p.f_2ec / (float)p.m_duration);
                p.f_2f0 = v + v;
                p = m_lastPrim; p.f_334 = -2.0f;
                p = m_lastPrim;
                v = -(p.f_334 / (float)p.m_duration);
                p.f_340 = v + v;
                p = m_lastPrim; p.f_314 = (float)tilt;
                p = m_lastPrim;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                p = m_lastPrim;
                p.f_3a0 = p.f_16c * 0.0f + p.f_160 * 0.0f + p.f_178 + p.f_184;
                p.f_3a4 = p.f_170 * 0.0f + p.f_164 * 0.0f + p.f_17c + p.f_188;
                p.f_3a8 = p.f_174 * 0.0f + p.f_168 * 0.0f + p.f_180 + p.f_18c;
                p = m_lastPrim; M.U32(p.A + 0x290, M.U32(p.A + 0x3a0));
                p = m_lastPrim; M.U32(p.A + 0x294, M.U32(p.A + 0x3a4));
                p = m_lastPrim; M.U32(p.A + 0x298, M.U32(p.A + 0x3a8));
                if (variant == 0)
                {
                    int r = RandMod(rt, 0x51) + 0xa0;
                    M.I32(m_lastPrim.A + 0x23c, r);
                    M.I32(m_lastPrim.A + 0x240, 0xff);
                    int b = RandMod(rt, 0x51) + 0xa0;
                    M.I32(m_lastPrim.A + 0x244, b);
                }
                else if (variant == 1 || variant == 2)
                {
                    int c = variant == 1 ? RandMod(rt, 0x37) + 200 : RandMod(rt, 100) + 0x14;
                    M.I32(m_lastPrim.A + 0x23c, c);
                    M.I32(m_lastPrim.A + 0x240, c);
                    M.I32(m_lastPrim.A + 0x244, c);
                }
                int s = RandMod(rt, 6);
                p = m_lastPrim; p.f_318 = (float)s * 0.1f + 1.0f;
                rt.SetSprName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle1.spr");
                rt.SetActName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle1.act");
                p = m_lastPrim; p.f_1f4 = 7;
                m_lastPrim.SnapshotTrail_ad58d0();
            }
        }

        /// <summary>
        /// Case 383 (0x17f) — Indulge (second part): sparkles; for t in [50, 81) flag 8 and a fading blue tint
        /// ARGB(-1, (125-t)*2, (125-t)*2, 250).
        /// </summary>
        [FxEffectProcess(0x17f)]
        public FxCaseResult Case_IndulgeSparkles(FxRuntime rt)
        {
            Fn_b74f20(rt, 0);
            int t = m_stateCnt;
            if ((uint)(t - 0x32) < 0x1f)
            {
                uint master = M.U32(A + 0x138);
                uint f = M.U32(master + 0xe8);
                if ((f & 8) == 0)
                {
                    M.U32(master + 0xe8, f + 8);
                    t = m_stateCnt;
                }
                int v = (0x7d - t) * 2;
                MasterArgb(rt, -1, v, v, 0xfa);
            }
            return FxCaseResult.Continue;
        }
    }
}
