// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.SpriteProjectiles.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>effects-1: sprite prims of type 8 flying from the caster to the target (+0x180/+0x188).</summary>
    public readonly partial struct RagEffect
    {
        // ---- EF 117 Water Ball ---------------------------------------------------------------------

        [FxEffectProcess(117)]
        public FxCaseResult Case_WaterBall_effects1(FxRuntime rt) { Fn_b64d10(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b64d10 (0xb64d10) — Water Ball: at tick 0 sound + one Waterball sprite (prim 8)
        /// arcing from the caster to the target over 40 ticks.
        /// </summary>
        public void Fn_b64d10(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\wizard_waterball_chulung.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchEffectPrim(rt, 8, 0f, 0f, 0f);
            m_lastPrim = p;
            rt.SetSprName(p.A, Effects1Strings.SprWaterball);
            rt.SetActName(p.A, Effects1Strings.ActWaterball);
            p.m_duration = 0x28;
            p.f_394 = 0f;
            p.f_398 = -10.0f;
            p.f_39c = 0f;
            p.f_348 = rt.DistanceXZ(p.A, f_180, f_188);
            rt.FaceTowards(p.A, f_180, f_188);
            p.f_30c = -p.f_4c;
            p.f_2bc = p.f_348 / (float)p.m_duration;
            p.f_2ec = 3.5f;
            float v = -(p.f_2ec / (float)p.m_duration);
            p.f_2f0 = v + v;
            p.f_334 = -2.0f;
            v = -(p.f_334 / (float)p.m_duration);
            p.f_340 = v + v;
            p.f_314 = 0f;
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            SetArcStart(p);
            p.f_318 = 0.75f;
            p.f_1f4 = 0xc;
            p.SnapshotTrail_ad58d0();
        }

        /// <summary>+0x3a0.. = row 0·0 + row 1·0 + row 2 + row 3 of the prim matrix; copied to +0x290..+0x298.</summary>
        void SetArcStart(Prim p)
        {
            uint m = p.A + 0x160;
            float x = M.F32(m + 0xc) * 0f + M.F32(m) * 0f;
            x += M.F32(m + 0x18);
            x += M.F32(m + 0x24);
            M.F32(p.A + 0x3a0, x);
            float y = M.F32(m + 0x10) * 0f + M.F32(m + 4) * 0f;
            y += M.F32(m + 0x1c);
            y += M.F32(m + 0x28);
            M.F32(p.A + 0x3a4, y);
            float z = M.F32(m + 0x14) * 0f + M.F32(m + 8) * 0f;
            z += M.F32(m + 0x20);
            z += M.F32(m + 0x2c);
            M.F32(p.A + 0x3a8, z);
            M.U32(p.A + 0x290, M.U32(p.A + 0x3a0));
            M.U32(p.A + 0x294, M.U32(p.A + 0x3a4));
            M.U32(p.A + 0x298, M.U32(p.A + 0x3a8));
        }

        // ---- EF 378 Indulge -----------------------------------------------------------------------

        /// <summary>OnProcess case 0x17a — Indulge: fn_b74b20, then ticks 55..65 owner flag 1 + tint (100,100,255).</summary>
        [FxEffectProcess(378)]
        public FxCaseResult Case_Indulge_effects1(FxRuntime rt)
        {
            Fn_b74b20(rt);
            if ((uint)(m_stateCnt - 0x37) <= 0xau)
            {
                AddMasterFlag_effects1(1);
                rt.SetArgb(M.U32(A + 0x138), -1, 100, 100, 0xff);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b74b20 (0xb74b20) — at tick 0 three bluish particle1 sprites (prim 8) arcing from
        /// the target back to the caster, rolled -90/0/90 degrees.
        /// </summary>
        public void Fn_b74b20(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            for (int roll = -0x5a; roll < 0xb4; roll += 0x5a)
            {
                Prim p = LaunchEffectPrim(rt, 8, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0x3c;
                p.f_394 = f_180 - m_pos_x;
                p.f_39c = f_188 - m_pos_z;
                p.f_398 = -10.0f;
                float tz = f_188, tx = f_180;
                float midZ = tz - (tz - m_pos_z) * 0.5f;
                float midX = tx - (tx - m_pos_x) * 0.5f;
                p.f_348 = rt.DistanceXZ(p.A, midX, midZ);
                float backZ = (m_pos_z + m_pos_z) - f_188;
                float backX = (m_pos_x + m_pos_x) - f_180;
                rt.FaceTowards(p.A, backX, backZ);
                p.f_30c = -p.f_4c;
                float v = p.f_348 / (float)p.m_duration;
                p.f_2bc = v + v;
                p.f_2ec = 2.0f;
                v = -(p.f_2ec / (float)p.m_duration);
                p.f_2f0 = v + v;
                p.f_334 = -2.0f;
                v = -(p.f_334 / (float)p.m_duration);
                p.f_340 = v + v;
                p.f_314 = (float)roll;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                SetArcStart(p);
                M.I32(p.A + 0x23c, RandMod(rt, 0x51) + 0xa0);
                p.f_240 = RandMod(rt, 0x51) + 0xa0;
                p.f_244 = 0xff;
                p.f_318 = (float)RandMod(rt, 6) * 0.1f + 1.5f;
                rt.SetSprName(p.A, Effects1Strings.SprParticle1);
                rt.SetActName(p.A, Effects1Strings.ActParticle1);
                p.f_1f4 = 7;
                p.SnapshotTrail_ad58d0();
            }
        }
    }
}
