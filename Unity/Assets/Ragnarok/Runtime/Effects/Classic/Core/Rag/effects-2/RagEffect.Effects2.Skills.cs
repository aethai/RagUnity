// Synced from reference/port/src/RoClassicFx/Rag/effects-2/RagEffect.Effects2.Skills.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>Stand-alone skill handlers of package effects-2 (Endure, Steal, Sightrasher, Ice Wall, …).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b37c50 (0xb37c50) — EF_ENDURE: screen position of the owner into f_154/f_158; tick 0:
        /// sound + a big "endure.tga" prim 0; ticks 0..49: one random "alpha_down.tga" spark (prim 0) per tick.
        /// </summary>
        public void Fn_b37c50(FxRuntime rt)
        {
            uint view = M.U32(rt.Mode + 0xd0);
            rt.ProjectTl(view + 0x98, M.U32(A + 0x138) + 0x10, out float sx, out float sy, out _);
            int tick = m_stateCnt;
            f_154 = sx;
            f_158 = sy;
            if (tick == 0)
            {
                E2_Sound(rt, "effect\\EF_Endure.wav");
                Prim p = E2_Launch(rt, 0);
                p.m_duration = f_17c;
                p.f_1dc |= 2;
                p.f_398 = -20.0f;
                p.f_354 = 150.0f;
                p.f_360 = 150.0f;
                p.f_358 = -5.0f;
                p.f_364 = -5.0f;
                p.f_1d4 = p.m_duration - 0x23;
                p.f_35c = -p.f_358 / (float)p.f_1d4;
                p.f_368 = -p.f_364 / (float)p.f_1d4;
                p.f_324 = 0f;
                p.f_328 = p.f_2d4 / 20.0f;
                int d = p.m_duration;
                p.f_1f0 = d - d / 3;
                E2_AllocTextures(rt, p);
                E2_SetTexture(rt, p, 0, "effect\\endure.tga");
                p.f_3e8 = 0;
                tick = m_stateCnt;
            }
            if (tick < 0x32)
            {
                Prim p = E2_Launch(rt, 0);
                p.m_duration = 0x28;
                int ang = E2_Rand(rt, 0x168);
                p.f_314 = (float)ang;
                p.f_30c = (float)ang;
                p.f_2bc = -4.0f;
                p.f_2c0 = -(p.f_2bc / (float)p.m_duration);
                p.f_354 = (float)(E2_Rand(rt, 100) + 0x3c) / 100.0f;
                p.f_360 = (float)(E2_Rand(rt, 0x14) + 0x1e);
                p.f_324 = 0f;
                p.f_328 = p.f_2d4 / 20.0f;
                int d = p.m_duration;
                p.f_1f0 = d - d / 3;
                int r = E2_Rand(rt, 0x28);
                float s = FxMath.GetSin(FxOps.Cvtt(p.f_30c));
                p.f_394 = s * (float)(r + 100);
                float c = FxMath.GetCos(FxOps.Cvtt(p.f_30c));
                p.f_398 = c * -(float)(r + 100);
                p.f_398 = p.f_398 - 20.0f;
                E2_AllocTextures(rt, p);
                E2_SetTexture(rt, p, 0, "effect\\alpha_down.tga");
                p.f_3e8 = 0;
            }
        }

        /// <summary>
        /// CRagEffect::fn_b620b0 (0xb620b0) — EF_STEAL: tick 0: sound + 10 "particle7" sprite prims (type 6) thrown
        /// in random directions (Y rotation × X tilt applied to a (0, 0, 4..7) vector).
        /// </summary>
        public void Fn_b620b0(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            E2_Sound(rt, "effect\\EF_Steal.wav");
            for (int n = 10; n != 0; n--)
            {
                Prim p = E2_Launch(rt, 6);
                p.f_30c = (float)E2_Rand(rt, 0x168);
                p.f_310 = (float)(E2_Rand(rt, 100) - 0x32);
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                FxMatrix.RotateX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                float len = (float)(E2_Rand(rt, 4) + 4);
                p.f_3a0 = p.f_16c * 0.0f + p.f_160 * 0.0f + len * p.f_178 + p.f_184;
                p.f_3a4 = p.f_170 * 0.0f + p.f_164 * 0.0f + len * p.f_17c + p.f_188;
                p.f_3a8 = p.f_174 * 0.0f + p.f_168 * 0.0f + len * p.f_180 + p.f_18c;
                p.f_394 = p.f_3a0 + 0.0f;
                p.f_398 = p.f_3a4 - 10.0f;
                p.f_39c = p.f_3a8 + 0.0f;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                p.m_duration = f_17c;
                p.f_2bc = (float)(E2_Rand(rt, 0x32) + 0x32) / 100.0f;
                p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
                p.f_2ec = -(float)(E2_Rand(rt, 0x5a) + 0x3c) / 100.0f;
                float v = -(p.f_2ec / (float)p.m_duration);
                p.f_2f0 = v + v;
                p.f_318 = (float)(E2_Rand(rt, 100) + 100) / 100.0f;
                p.f_31c = -(p.f_318 / (float)p.m_duration);
                int d = p.m_duration;
                p.f_1f0 = d - d / 3;
                rt.SetSprName(p.A, Effects2Strings.Spr_fe25a0);
                rt.SetActName(p.A, Effects2Strings.Act_fe25b8);
                p.SnapshotTrail_ad58d0();
            }
        }

        /// <summary>
        /// CRagEffect::fn_b5f000 (0xb5f000) — EF_SIGHTRASHER: owner message (0x6d, 0x16) + sound at tick 0; every
        /// 5 ticks before 20 two rings of 8 sprite prims (type 5, "Sight" + "Shadow") flying outwards; tick 10: a
        /// yellow ground ring (type 0xc).
        /// </summary>
        public void Fn_b5f000(FxRuntime rt)
        {
            uint view = M.U32(rt.Mode + 0xd0);
            rt.ProjectTl(view + 0x98, M.U32(A + 0x138) + 0x10, out float sx, out float sy, out _);
            int tick = m_stateCnt;
            f_154 = sx;
            f_158 = sy;
            if (tick == 0)
            {
                rt.OwnerSendMsg(M.U32(A + 0x138), 0, 0x6d, 0, 0x16, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                E2_Sound(rt, "effect\\wizard_sightrasher.wav");
                tick = m_stateCnt;
            }
            if (tick % 5 == 0 && tick < 0x14)
            {
                for (int i = 0; i < 8; i++)
                {
                    int ang = FxOps.Cvtt((float)i * -45.0f);
                    float s = FxMath.GetSin(ang);
                    float c = FxMath.GetCos(ang);
                    Prim p = E2_Launch(rt, 5);
                    p.m_duration = 0x55 - m_stateCnt;
                    p.f_1dc |= 0x400;
                    int t = m_stateCnt;
                    float r = (float)(t / 5) + 5.0f;
                    p.f_394 = r * s;
                    p.f_39c = -r * c;
                    p.f_398 = (float)(t / 3 - 0x14);
                    double ds = s, dc = c;
                    p.f_374 = 0f;
                    p.f_370 = (float)ds;
                    p.f_378 = (float)(dc * -1.0);
                    int r2 = rt.Rand.Next() % 2;
                    p.f_1e8 = (r2 >= 0 ? r2 : 0) + 3;
                    p.f_318 = 2.5f - (float)m_stateCnt / 10.0f;
                    p.f_324 = (float)((0x4b - m_stateCnt) * 2);
                    p.f_3ac = 0f;
                    p.f_3b0 = 0f;
                    p.f_3b4 = 0f;
                    p.f_2a4 = (float)(ds * 0.5);
                    p.f_2a8 = 0f;
                    p.f_2ac = (float)(dc * -0.5);
                    p.f_200 = 0x23 - m_stateCnt;
                    p.f_2b0 = -0.004f;
                    p.f_2b4 = 0f;
                    p.f_2b8 = 0.004f;
                    p.f_1f0 = 0x41 - m_stateCnt;
                    rt.SetSprName(p.A, Effects2Strings.Spr_ffa6cc);
                    rt.SetActName(p.A, Effects2Strings.Act_ffa6e0);
                    p.SnapshotTrail_ad58d0();
                }
                for (int i = 0; i < 8; i++)
                {
                    int ang = FxOps.Cvtt((float)i * -45.0f);
                    float s = FxMath.GetSin(ang);
                    float c = FxMath.GetCos(ang);
                    Prim p = E2_Launch(rt, 5);
                    p.m_duration = 0x55 - m_stateCnt;
                    p.f_1dc |= 0x400;
                    int t = m_stateCnt;
                    float r = (float)(t / 5) + 5.0f;
                    p.f_398 = 0f;
                    p.f_394 = r * s;
                    p.f_39c = -r * c;
                    double ds = s, dc = c;
                    p.f_374 = 0f;
                    p.f_370 = (float)ds;
                    p.f_378 = (float)(dc * -1.0);
                    p.f_1e8 = 3;
                    p.f_318 = 1.0f - (float)m_stateCnt / 30.0f;
                    p.f_324 = (float)((0x4b - m_stateCnt) * 2);
                    p.f_2a4 = (float)(ds * 0.5);
                    p.f_2ac = (float)(dc * -0.5);
                    p.f_200 = 0x23 - m_stateCnt;
                    p.f_2b0 = -0.004f;
                    p.f_2b8 = 0.004f;
                    p.f_1f0 = 0x41 - m_stateCnt;
                    rt.SetSprName(p.A, "Shadow.spr");
                    rt.SetActName(p.A, "Shadow.act");
                    p.SnapshotTrail_ad58d0();
                }
                tick = m_stateCnt;
            }
            if (tick == 10)
            {
                Prim p = E2_Launch(rt, 0xc);
                p.m_duration = 0x1e;
                p.f_310 = 90.0f;
                p.f_34c = 2.5f;
                p.f_350 = -(p.f_34c / (float)p.m_duration) / 1.5f;
                p.f_2f4 = -10.0f;
                p.f_324 = 150.0f;
                p.f_1f0 = p.m_duration - tick;
                p.SnapshotTrail_ad58d0();
                E2_AllocTextures(rt, p);
                E2_SetTexture(rt, p, 0, "effect\\ring_yellow.tga");
            }
        }

        /// <summary>
        /// CRagEffect::fn_b41470 (0xb41470) — EF_ICEWALL: tick 0: sound, duration 9999 and three ice blocks
        /// (prim type 9, "ice.tga") along z = +2, 0, -2.
        /// </summary>
        public void Fn_b41470(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            E2_Sound(rt, "effect\\wizard_icewall.wav");
            f_17c = 9999;
            for (int k = -2; k < 4; k += 2)
            {
                Prim p = E2_Launch(rt, 9);
                p.m_duration = f_17c;
                p.f_1dc |= 1;
                int ang = E2_Rand(rt, 0x168);
                float s = FxMath.GetSin(ang);
                float c = FxMath.GetCos(ang);
                p.f_398 = 30.0f;
                p.f_394 = s * 0.0f;
                p.f_39c = -(float)k * c;
                p.f_318 = (float)(E2_Rand(rt, 0x28) / 100) + 2.5f;
                p.f_360 = (float)(E2_Rand(rt, 100) + 300) / 10.0f;
                p.f_30c = (float)E2_Rand(rt, 0x168);
                p.f_310 = (float)(E2_Rand(rt, 6) + 0x57);
                p.f_1d4 = 0x14;
                p.f_324 = 200.0f;
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                M.Copy(p.A + 0x190, p.A + 0x160, 0x30);
                p.f_2bc = 3.0f;
                float v = -(p.f_2bc / 40.0f);
                p.f_2c0 = v + v;
                p.f_1f0 = p.m_duration - 10;
                E2_AllocTextures(rt, p);
                E2_SetTexture(rt, p, 0, "effect\\ice.tga");
            }
        }

        /// <summary>CRagEffect::fn_b440a0 (0xb440a0) — EF_LEXDIVINA: sound at ticks 0, 20, 35, 60, 70, then the STR player.</summary>
        public bool Fn_b440a0(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0) { E2_Sound(rt, "effect\\priest_lexdivina.wav"); t = m_stateCnt; }
            if (t == 0x14) { E2_Sound(rt, "effect\\priest_lexdivina.wav"); t = m_stateCnt; }
            if (t == 0x23) { E2_Sound(rt, "effect\\priest_lexdivina.wav"); t = m_stateCnt; }
            if (t == 0x3c) { E2_Sound(rt, "effect\\priest_lexdivina.wav"); t = m_stateCnt; }
            if (t == 0x46) E2_Sound(rt, "effect\\priest_lexdivina.wav");
            return rt.StrPlayer(this, 0f, -80.0f, 0f);
        }

        /// <summary>
        /// CRagEffect::fn_b654a0 (0xb654a0) — EF_WATERBALL: tick 0: sound + a rising water column (prim 0xf) with
        /// three "water_out" textures.
        /// </summary>
        public void Fn_b654a0(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            E2_Sound(rt, "effect\\wizard_waterball_chulung.wav");
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0xf);
            p.m_duration = f_17c;
            p.f_1dc = 0x40;
            p.f_1dc |= 0x100;
            p.f_1c4 |= 2;
            p.f_254 = 1;
            p.f_354 = 3.5f;
            p.f_360 = 3.5f;
            p.f_374 = -(25.0f / (float)p.m_duration);
            p.f_398 = -5.0f;
            p.f_1e8 = 5;
            p.f_204 = 3;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, "effect\\water_out_a.bmp");
            E2_SetTexture(rt, p, 1, "effect\\water_out_b.bmp");
            E2_SetTexture(rt, p, 2, "effect\\water_out_c.bmp");
        }

        /// <summary>
        /// CRagEffect::fn_b65d20 (0xb65d20) — EF_JUPITEL (param 0) / variant 1: follows the owner; every 20 ticks a
        /// short flash (prim 0xf); tick 10: the long-lived thunder ball with 5 textures.
        /// </summary>
        public void Fn_b65d20(FxRuntime rt, int variant)
        {
            E2_FollowMaster();
            if (m_stateCnt % 0x14 == 0)
            {
                Prim p = E2_Launch(rt, 0xf);
                p.m_duration = 10;
                p.f_1dc |= 0x40;
                p.f_1c4 |= 2;
                p.f_1c4 |= 0x200;
                p.f_398 = -5.0f;
                p.f_358 = 2.5f;
                p.f_364 = 2.5f;
                p.f_200 = 10;
                p.f_2c8 = 0f;
                p.f_2cc = 0f;
                p.f_2c4 = -p.f_2c8 / 50.0f;
                p.f_2d0 = -p.f_2cc / 50.0f;
                p.f_1f0 = 1;
                E2_AllocTextures(rt, p);
                E2_SetTexture(rt, p, 0, variant == 0 ? "effect\\thunder_pang.bmp" : "effect\\pokjuk_d.bmp");
            }
            if (m_stateCnt == 10)
            {
                Prim p = E2_Launch(rt, 0xf);
                p.m_duration = 300;
                p.f_1dc |= 0x40;
                p.f_1c4 |= 0x200;
                p.f_1c4 |= 2;
                p.f_398 = -5.0f;
                p.f_254 = 1;
                p.f_354 = 7.5f;
                p.f_360 = 7.5f;
                p.f_1e8 = 1;
                p.f_204 = 5;
                E2_AllocTextures(rt, p);
                string last;
                if (variant == 0)
                {
                    E2_SetTexture(rt, p, 0, "effect\\thunder_plazma_blast_a.bmp");
                    E2_SetTexture(rt, p, 1, "effect\\thunder_plazma_blast_b.bmp");
                    E2_SetTexture(rt, p, 2, "effect\\thunder_ball_d.bmp");
                    E2_SetTexture(rt, p, 3, "effect\\thunder_ball_e.bmp");
                    last = "effect\\thunder_ball_f.bmp";
                }
                else
                {
                    E2_SetTexture(rt, p, 0, "effect\\twirl_soft.bmp");
                    E2_SetTexture(rt, p, 1, "effect\\thunder_ball_b.bmp");
                    E2_SetTexture(rt, p, 2, "effect\\twirl_soft.bmp");
                    E2_SetTexture(rt, p, 3, "effect\\thunder_ball_c.bmp");
                    last = "effect\\twirl_soft.bmp";
                }
                E2_SetTexture(rt, p, 4, last);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b35d00 (0xb35d00) — EF_DETECTING (variant 0) and relatives: variant sound at tick 0 and a
        /// "fashasha.tga" ring (prim 0xf) tinted per variant.
        /// </summary>
        public void Fn_b35d00(FxRuntime rt, int variant)
        {
            if (m_stateCnt != 0) return;
            string wav = variant == 0 ? "effect\\hunter_detecting.wav"
                : variant == 2 ? "effect\\priest_benedictio.wav"
                : variant == 4 ? "effect\\cru_grand cross.wav"
                : variant == 5 ? "effect\\ANALYZE.wav" : null;
            if (wav != null) E2_Sound(rt, wav);
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0xf);
            p.m_duration = 0x39;
            p.f_358 = 1.5f;
            p.f_364 = 1.5f;
            p.f_35c = p.f_358 / 14.0f;
            p.f_368 = p.f_364 / 14.0f;
            p.f_398 = -7.0f;
            p.f_374 = -0.5f;
            p.f_314 = 90.0f;
            if (variant == 3 || variant == 6) { p.f_200 = 10; p.f_1f0 = 0x1e; }
            else { p.f_200 = 0x11; p.f_1f0 = 0x28; }
            switch (variant)
            {
                case 4: M.U32(p.A + 0x23c, 0xff); p.f_244 = 0x8c; p.f_240 = 0x8c; break;
                case 5: M.U32(p.A + 0x23c, 0xd2); p.f_244 = 0xd2; p.f_240 = 0xfa; break;
                case 6: M.U32(p.A + 0x23c, 10); p.f_244 = 10; p.f_240 = 10; break;
                case 7: M.U32(p.A + 0x23c, 0xfa); p.f_244 = 0x8c; p.f_240 = 0xd2; break;
                case 8: M.U32(p.A + 0x23c, 0x8c); p.f_244 = 0x8c; p.f_240 = 0xff; break;
            }
            p.f_2c8 = 0.1f;
            p.f_2cc = 0.1f;
            p.f_2c4 = p.f_2c8 / 30.0f;
            p.f_2d0 = p.f_2cc / 30.0f;
            p.f_398 = -5.0f;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, "effect\\fashasha.tga");
        }

        /// <summary>
        /// CRagEffect::fn_b8f3b0 (0xb8f3b0) — EF_GANBANTEIN: tick 0: a 4-ring "cloud11.tga" swirl (prim 0x3e,
        /// four sub-emitters at +0x3e8/+0x49c/+0x550/+0x604) and "_heal_effect.wav".
        /// </summary>
        public void Fn_b8f3b0(FxRuntime rt)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0x3e);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, "effect\\cloud11.tga");
            uint a = p.A;
            p.f_318 = 4.0f;
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.I16(a + 0x3ee, 0x5a);
            M.F32(a + 0x3f0, 8.0f);
            M.I16(a + 0x3f8, 0);
            M.I16(a + 0x3ea, 0);
            M.F32(a + 0x468, 3.0f);
            M.I16(a + 0x466, 0x5a);
            E2_FillSubEmitter(a, 0, 0x40400000);
            M.I16(a + 0x3ec, 0);
            M.U8(a + 0x49c, 1);
            M.I32(a + 0x4a8, 0);
            M.I16(a + 0x4a2, 0x46);
            M.F32(a + 0x4a4, 8.0f);
            M.I16(a + 0x4ac, 10);
            M.I16(a + 0x49e, 0);
            M.F32(a + 0x51c, 3.2f);
            M.I16(a + 0x51a, 0x5a);
            E2_FillSubEmitter(a, 1, 0x40200000);
            M.I16(a + 0x4a0, 0);
            M.U8(a + 0x550, 1);
            M.I32(a + 0x55c, 0);
            M.I16(a + 0x556, 0x32);
            M.F32(a + 0x558, 8.0f);
            M.I16(a + 0x560, 0x14);
            M.I16(a + 0x552, 0);
            M.F32(a + 0x5d0, 3.4f);
            M.I16(a + 0x5ce, 0x5a);
            E2_FillSubEmitter(a, 2, 0x40000000);
            M.I16(a + 0x554, 0);
            M.U8(a + 0x604, 1);
            M.I32(a + 0x610, 0);
            M.I16(a + 0x60a, 0x1e);
            M.F32(a + 0x60c, 8.0f);
            M.I16(a + 0x614, 0x1e);
            M.I16(a + 0x606, 0);
            M.F32(a + 0x684, 3.6f);
            M.I16(a + 0x682, 0x5a);
            E2_FillSubEmitter(a, 3, 0x3fc00000);
            M.I16(a + 0x608, 0);
            E2_Sound(rt, "_heal_effect.wav");
        }

        /// <summary>
        /// The client's unrolled "clear sub-emitter" loop: in sub-emitter <paramref name="block"/> (0xb4 bytes from
        /// +0x3e8) the 21 dwords at +0x14..+0x64 are set to <paramref name="value"/> and the 21 bytes at +0x68..+0x7c
        /// to 0 (three iterations of 7).
        /// </summary>
        void E2_FillSubEmitter(uint prim, int block, uint value)
        {
            uint b = prim + 0x3e8 + (uint)block * 0xb4;
            for (uint i = 0; i < 21; i++)
            {
                M.U32(b + 0x14 + i * 4, value);
                M.U8(b + 0x68 + i, 0);
            }
        }
    }
}
