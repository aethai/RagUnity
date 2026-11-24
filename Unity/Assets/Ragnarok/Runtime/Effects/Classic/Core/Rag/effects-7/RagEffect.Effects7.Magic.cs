// Synced from reference/port/src/RoClassicFx/Rag/effects-7/RagEffect.Effects7.Magic.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>effects-7: Heal &lt; 200 (EF 7), Cold Bolt / Fire Bolt (EF 26 / 31), Turn Undead (EF 82), Enchant Poison (EF 125).</summary>
    public readonly partial struct RagEffect
    {
        // ---- EF 7 (Heal, small amount) -----------------------------------------------------------------

        /// <summary>
        /// CRagEffect::fn_b390c0 (0xb390c0) — small Heal: follows the owner; at tick 0 an alpha_down column (prim 0xd,
        /// 100 ticks — the effect is stretched to 100 too) and the heal sound; unless /mineffect every 9th tick a
        /// particle1 sparkle (prim 7) rising on a radius-3 circle (one rand() discarded first).
        /// </summary>
        public void Fn_b390c0(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt == 0)
            {
                Prim p = LaunchLast(rt, 0xd);
                f_17c = 100;
                p.m_duration = 100;
                p.f_2f8 = 4.5f;
                p.f_2f4 = 4.5f;
                p.f_360 = 35.0f;
                p.f_324 = 0.0f;
                p.f_2d4 = 90.0f;
                p.f_328 = p.f_2d4 / 30.0f;
                p.f_1f0 = p.m_duration - 0x14;
                SetPrimTexture(rt, p, "effect\\alpha_down.tga");
                rt.PlaySound3DRelative("_heal_effect.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            if (rt.MinEffect || m_stateCnt % 9 != 0) return;
            Prim s = LaunchLast(rt, 7);
            rt.Rand.Next();
            s.f_348 = 3.0f;
            s.m_duration = 0x32;
            s.f_30c = (float)RandMod(rt, 0x168);
            s.f_2ec = -1.2f;
            s.f_2f0 = -(s.f_2ec / (float)s.m_duration) / 1.5f;
            FxMatrix.MakeRotationY(M, s.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(s.f_30c)));
            RotatedOffset(s, s.f_348, 0x394);
            s.f_318 = 0.55f;
            int d = s.m_duration;
            s.f_1f0 = d - d / 3;
            rt.SetSprName(s.A, Effects1Strings.SprParticle1);
            rt.SetActName(s.A, Effects1Strings.ActParticle1);
            s.SnapshotTrail_ad58d0();
        }

        /// <summary>EF 7 (Heal &lt; 200): fn_b390c0.</summary>
        [FxEffectProcess(0x7)]
        public FxCaseResult Case_HealBelow200(FxRuntime rt) { Fn_b390c0(rt); return FxCaseResult.Continue; }

        // ---- EF 26 / 31 Cold Bolt / Fire Bolt ------------------------------------------------------------

        /// <summary>
        /// The bolt shard shared by fn_b40ef0 / fn_b394a0: prim 0x10 (70 ticks, flags 0x14) placed at
        /// (rand%10 + 25, -60, rand%10 + 15) and aimed at a random point of the (-2..1)² grid around the owner:
        /// heading = -FUN_00bce500(offset → point), pitch = FUN_00bce500(-|y|, horizontal distance); both are also
        /// kept in the effect (+0x14c / +0x150) for the trailing sprites.
        /// </summary>
        Prim LaunchBoltShardE7(FxRuntime rt, float f354, float f360)
        {
            Prim p = LaunchLast(rt, 0x10);
            p.m_duration = 0x46;
            p.f_1dc = 0x14;
            int r1 = rt.Rand.Next() % 10;
            int r2 = rt.Rand.Next() % 10;
            p.f_398 = -60.0f;
            p.f_394 = (float)((r2 >= 0 ? r2 : 0) + 0x19);
            p.f_39c = (float)((r1 >= 0 ? r1 : 0) + 0xf);
            int s1 = RandMod(rt, 4);
            int s2 = RandMod(rt, 4);
            float heading = FxMath.HeadingTo_bce500(p.f_394, p.f_39c, (float)(s2 - 2), (float)(s1 - 2));
            p.f_30c = -heading;
            M.F32(A + 0x14c, -heading);
            float ax = System.Math.Abs(p.f_394);
            float az = System.Math.Abs(p.f_39c);
            float len = FxRuntime.SqrtF(ax * ax + az * az);
            float pitch = FxMath.HeadingTo_bce500(-System.Math.Abs(p.f_398), len, 0f, 0f);
            p.f_310 = pitch;
            M.F32(A + 0x150, pitch);
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
            FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            p.f_2bc = -2.75f;
            p.f_354 = f354;
            p.f_360 = f360;
            p.f_1f0 = p.m_duration - 0x14;
            return p;
        }

        /// <summary>
        /// The impact ring of both bolts, launched when +0x170 is set (then cleared): a rising cylinder (prim 0xc,
        /// 30 ticks) of <paramref name="texture"/>.
        /// </summary>
        void BoltImpactRingE7(FxRuntime rt, string texture)
        {
            M.I32(A + 0x170, 0);
            Prim p = LaunchLast(rt, 0xc);
            p.m_duration = 0x1e;
            p.f_310 = 90.0f;
            p.f_34c = 1.2f;
            float v = -(p.f_34c / (float)(p.m_duration + 0x28));
            p.f_350 = v + v;
            p.f_2f4 = 10.0f;
            p.f_324 = 0.0f;
            p.f_328 = p.f_2d4 / 10.0f;
            p.f_1f0 = p.m_duration - 10;
            p.SnapshotTrail_ad58d0();
            SetPrimTexture(rt, p, texture);
        }

        /// <summary>
        /// CRagEffect::fn_b40ef0 (0xb40ef0) — Cold Bolt (EF_ICEARROW): follows the owner; at tick 12 one of three
        /// EF_IceArrow sounds and f_174 (the bolt count) × 10; then every 10 ticks while (t - 12) &lt; f_174 an icearrow
        /// shard (prim 0x10); an impact ring (ring_blue) whenever +0x170 is set.
        /// </summary>
        public void Fn_b40ef0(FxRuntime rt)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t == 0xc)
            {
                string wav = "effect\\EF_IceArrow" + (RandMod(rt, 3) + 1) + ".wav";
                rt.PlaySound3DRelative(wav, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                f_174 = f_174 * 10;
                t = m_stateCnt;
            }
            if (t >= 0xc)
            {
                int k = t - 0xc;
                if (k % 10 == 0 && k < f_174)
                {
                    Prim p = LaunchBoltShardE7(rt, 11.5f, 3.8f);
                    SetPrimTexture(rt, p, "effect\\icearrow.tga");
                    M.U8(m_lastPrim.A + 0x3e8, 0);
                }
            }
            if (M.I32(A + 0x170) != 0) BoltImpactRingE7(rt, "effect\\ring_blue.tga");
        }

        /// <summary>EF 26 (0x1a, Cold Bolt): fn_b40ef0.</summary>
        [FxEffectProcess(0x1a)]
        public FxCaseResult Case_ColdBolt(FxRuntime rt) { Fn_b40ef0(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b394a0 (0xb394a0) — Fire Bolt (EF_FIREARROW): like Cold Bolt (EF_FireArrow sounds, a 6-frame
        /// 불화살 shard animation, f_354 14 / f_360 3.5), plus every 4th tick (t &gt; 0) a particle4 ember (prim 5) near
        /// the last shard's heading/pitch (+0x14c / +0x150) at the effect's target offset (f_180..); impact ring_yellow.
        /// </summary>
        public void Fn_b394a0(FxRuntime rt)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t == 0xc)
            {
                string wav = "effect\\EF_FireArrow" + (RandMod(rt, 3) + 1) + ".wav";
                rt.PlaySound3DRelative(wav, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
                f_174 = f_174 * 10;
            }
            bool ember;
            if (t < 0xc) ember = t > 0;
            else
            {
                int k = t - 0xc;
                if (k % 10 == 0 && k < f_174)
                {
                    Prim p = LaunchBoltShardE7(rt, 14.0f, 3.5f);
                    p = m_lastPrim; p.f_204 = 6;
                    p = m_lastPrim; p.f_1e8 = 1;
                    p = m_lastPrim;
                    p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                    for (uint i = 0; i < 6; i++)
                    {
                        uint tex = rt.GetTexture(Effects7Strings.FireArrowTga[i]);
                        M.U32((uint)m_lastPrim.f_144 + i * 4, tex);
                    }
                    M.U8(m_lastPrim.A + 0x3e8, 0);
                    t = m_stateCnt;
                    ember = t > 0;
                }
                else ember = true;
            }
            if (ember && t % 4 == 0)
            {
                Prim p = LaunchLast(rt, 5);
                p.m_duration = RandMod(rt, 0x18) + 6;
                int r = RandMod(rt, 0x28);
                p.f_30c = (float)r + (M.F32(A + 0x14c) - 20.0f);
                r = RandMod(rt, 0x14);
                p.f_310 = (float)r + (M.F32(A + 0x150) - 10.0f);
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                M.U32(p.A + 0x394, M.U32(A + 0x180));
                M.U32(p.A + 0x398, M.U32(A + 0x184));
                M.U32(p.A + 0x39c, M.U32(A + 0x188));
                p.f_2bc = (float)(RandMod(rt, 0x5a) + 0x3c) / 100.0f;
                p.f_2c0 = -(p.f_2bc / (float)p.m_duration) / 1.5f;
                p.f_318 = (float)(RandMod(rt, 0x1e) + 0x14) / 100.0f;
                p.f_31c = -(p.f_318 / (float)p.m_duration);
                p.f_1f0 = 0;
                rt.SetSprName(p.A, Effects7Strings.SprParticle4);
                rt.SetActName(p.A, Effects7Strings.ActParticle4);
                p.SnapshotTrail_ad58d0();
            }
            if (M.I32(A + 0x170) != 0) BoltImpactRingE7(rt, "effect\\ring_yellow.tga");
        }

        /// <summary>EF 31 (0x1f, Fire Bolt): fn_b394a0.</summary>
        [FxEffectProcess(0x1f)]
        public FxCaseResult Case_FireBolt(FxRuntime rt) { Fn_b394a0(rt); return FxCaseResult.Continue; }

        // ---- EF 82 Turn Undead ---------------------------------------------------------------------------

        /// <summary>
        /// CRagEffect::fn_b63860 (0xb63860) — Turn Undead: Bash sound at tick 37; every tick the owner's screen position;
        /// at tick 0 a shrinking alpha_down flash (prim 4) and five alpha_center light shafts (prim 3) spinning up
        /// around the owner; follows the owner; at tick 50 a ring_liner halo (prim 0xf).
        /// </summary>
        public void Fn_b63860(FxRuntime rt)
        {
            if (m_stateCnt == 0x25)
                rt.PlaySound3DRelative("effect\\EF_Bash.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            ProjectMasterToScreen(rt);
            int t = m_stateCnt;
            if (t == 0)
            {
                Prim p = LaunchLast(rt, 4);
                p.f_348 = 370.0f;
                p.f_34c = -(p.f_348 / 20.0f);
                p.m_duration = 0x14;
                p.f_1dc |= 1;
                p.f_398 = -40.0f;
                p.f_324 = 0.0f;
                p.f_2d4 = 130.0f;
                p.f_328 = p.f_2d4 / 6.0f;
                p.f_1f0 = p.m_duration - 10;
                SetPrimTexture(rt, p, "effect\\alpha_down.tga");
                int n = m_stateCnt != 0 ? 0x14 : 5;
                do
                {
                    Prim q = LaunchLast(rt, 3);
                    q.m_duration = 0x14;
                    q.f_1d4 = -1;
                    uint master = M.U32(A + 0x138);
                    M.U32(q.A + 0x370, M.U32(master + 0x10));
                    M.U32(q.A + 0x374, M.U32(master + 0x18));
                    q.f_398 = -40.0f;
                    q.f_30c = (float)RandMod(rt, 0x168);
                    q.f_330 = (float)(RandMod(rt, 0x3c) + 10) / 10.0f;
                    q.f_33c = -(q.f_330 / (float)q.m_duration) / 1.5f;
                    q.f_288 = (float)(RandMod(rt, 0x19) + 5) / 10.0f;
                    if (m_stateCnt == 0)
                    {
                        q.f_360 = (float)(RandMod(rt, 0x1e) + 0x15e);
                        q.f_364 = -(float)(RandMod(rt, 0x1e) + 0x96) / 10.0f;
                    }
                    else
                    {
                        q.f_360 = 0.0f;
                        q.f_364 = (float)(RandMod(rt, 0x14) + 0x32) / 10.0f;
                        q.f_200 = 0x28;
                        q.f_29c = (float)(RandMod(rt, 10) + 0xf) / 10.0f;
                        q.f_2a0 = 0.0f;
                        q.f_2cc = 0.0f;
                    }
                    q.f_324 = 0.0f;
                    q.f_2d4 = 150.0f;
                    q.f_328 = q.f_2d4 / 10.0f;
                    int d = q.m_duration;
                    q.f_1f0 = d - d / 3;
                    SetPrimTexture(rt, q, "EFFECT\\alpha_center.tga");
                } while (--n != 0);
                t = m_stateCnt;
            }
            FollowMaster();
            if (t != 0x32) return;
            Prim h = LaunchLast(rt, 0xf);
            h.m_duration = 0x1e;
            h.f_314 = 90.0f;
            h.f_358 = 1.5f;
            h.f_364 = 1.5f;
            h.f_398 = -7.0f;
            h.f_35c = h.f_358 / 10.0f;
            h.f_368 = h.f_364 / 10.0f;
            h.f_324 = 150.0f;
            h.f_1f0 = h.m_duration - 10;
            h.f_200 = 0xf;
            h.f_2c8 = 0.5f;
            h.f_2c4 = -0.033333335f;
            h.f_2cc = 0.5f;
            h.f_2d0 = -0.033333335f;
            h.SnapshotTrail_ad58d0();
            SetPrimTexture(rt, h, "effect\\ring_liner.tga");
        }

        /// <summary>EF 82 (0x52, Turn Undead): fn_b63860.</summary>
        [FxEffectProcess(0x52)]
        public FxCaseResult Case_TurnUndead(FxRuntime rt) { Fn_b63860(rt); return FxCaseResult.Continue; }

        // ---- EF 125 Enchant Poison -----------------------------------------------------------------------

        /// <summary>EF 125 (0x7d, Enchant Poison / Venom Splasher): the OnProcess case shares Envenom's label — fn_b37600(0).</summary>
        [FxEffectProcess(0x7d)]
        public FxCaseResult Case_EnchantPoison(FxRuntime rt) { Fn_b37600(rt, 0); return FxCaseResult.Continue; }
    }
}
