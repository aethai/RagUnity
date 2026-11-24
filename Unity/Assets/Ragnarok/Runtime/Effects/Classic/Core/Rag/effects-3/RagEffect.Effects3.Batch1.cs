// Synced from reference/port/src/RoClassicFx/Rag/effects-3/RagEffect.Effects3.Batch1.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>ProjectVertex (0x53b370) of the master's position with the view matrix; stores screen x/y in f_154/f_158.</summary>
        void E3ProjectMaster(FxRuntime rt)
        {
            uint view = M.U32(rt.Mode + 0xd0);
            rt.ProjectTl(view + 0x98, m_master + 0x10, out float sx, out float sy, out _);
            f_154 = sx;
            f_158 = sy;
        }

        static int E3RandMod(FxRuntime rt, int n)
        {
            int r = rt.Rand.Next() % n;
            return r >= 0 ? r : 0;
        }

        /// <summary>
        /// CRagEffect::fn_b31940 (0xb31940) — EF 16 (Bash): at tick 0 (not /mineffect) a flash (prim 4, alpha_down.tga)
        /// and 20 sparks (prim 3, alpha_center.tga) with random angle/speed/size.
        /// </summary>
        public void Fn_b31940(FxRuntime rt)
        {
            if (rt.MinEffect) return;
            E3ProjectMaster(rt);
            if (m_stateCnt != 0) return;
            E3Sound(rt, "effect\\EF_Bash.wav", 0.0f);
            Prim p = LaunchEffectPrim(rt, 4, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0x28;
            { Prim lp_ = m_lastPrim; lp_.f_1dc |= 1; }
            { Prim lp_ = m_lastPrim; lp_.f_398 = -40.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_348 = 100.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_324 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_2d4 = 170.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_328 = m_lastPrim.f_2d4 / 6.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_1f0 = m_lastPrim.m_duration - 10; }
            E3SetTexture(rt, m_lastPrim, "effect\\alpha_down.tga");
            int count = rt.MinEffect ? 10 : 0x14;
            do
            {
                p = LaunchEffectPrim(rt, 3, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0x28;
                { Prim lp_ = m_lastPrim; lp_.f_398 = -40.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_30c = (float)E3RandMod(rt, 0x168); }
                { Prim lp_ = m_lastPrim; lp_.f_330 = (float)(E3RandMod(rt, 0x3c) + 10) / 10.0f; }
                p = m_lastPrim;
                p.f_33c = -(p.f_330 / (float)p.m_duration) / 1.5f;
                { Prim lp_ = m_lastPrim; lp_.f_288 = (float)(E3RandMod(rt, 0x19) + 5) / 10.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_360 = (float)(E3RandMod(rt, 0x28) + 0x14); }
                { Prim lp_ = m_lastPrim; lp_.f_364 = (float)(E3RandMod(rt, 0x1e) + 0x14) / 10.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_324 = 0.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_2d4 = 200.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_328 = m_lastPrim.f_2d4 / 10.0f; }
                int d = m_lastPrim.m_duration;
                { Prim lp_ = m_lastPrim; lp_.f_1f0 = d - d / 3; }
                E3SetTexture(rt, m_lastPrim, "EFFECT\\alpha_center.tga");
                count--;
            } while (count != 0);
        }

        /// <summary>
        /// CRagEffect::fn_b34bd0 (0xb34bd0) — EF 51 (cold bolt / frost nova hit): at tick 0 nine ice shards (prim 2,
        /// ice.tga) around the target every 40°; at ticks 0 and 7 a smoke puff (prim 0, smoke.tga).
        /// Several constants are double precision in the original (12.8, 4.8, /1.5).
        /// </summary>
        public void Fn_b34bd0(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                int i = 0;
                float angle = 0.0f;
                do
                {
                    Prim p = LaunchEffectPrim(rt, 2, 0f, 0f, 0f);
                    m_lastPrim = p;
                    p.m_duration = 0xf;
                    int r = E3RandMod(rt, 0x3c);
                    float a = (float)r + (angle - 15.0f);
                    { Prim lp_ = m_lastPrim; lp_.f_314 = a; }
                    { Prim lp_ = m_lastPrim; lp_.f_30c = a; }
                    float size;
                    if (i % 3 == 0)
                    {
                        { Prim lp_ = m_lastPrim; lp_.f_360 = (float)(E3RandMod(rt, 10) + 100); }
                        { Prim lp_ = m_lastPrim; lp_.f_354 = 10.0f; }
                        size = (float)((double)(float)(E3RandMod(rt, 0x2d) / 10) + 12.8);
                    }
                    else
                    {
                        { Prim lp_ = m_lastPrim; lp_.f_360 = (float)(E3RandMod(rt, 5) + 0x2d); }
                        { Prim lp_ = m_lastPrim; lp_.f_354 = 4.0f; }
                        size = (float)((double)(float)(E3RandMod(rt, 0x2d) / 10) + 4.8);
                    }
                    { Prim lp_ = m_lastPrim; lp_.f_2bc = size; }
                    p = m_lastPrim;
                    float speed = -(p.f_2bc / (float)p.m_duration);
                    p.f_2c0 = (float)((double)speed / 1.5);
                    int dist = E3RandMod(rt, 5);
                    int deg = FxOps.Cvtt(angle);
                    float fdist = (float)dist;
                    { Prim lp_ = m_lastPrim; lp_.f_394 = FxMath.GetSin(deg) * fdist; }
                    float c = FxMath.GetCos(deg);
                    { Prim lp_ = m_lastPrim; lp_.f_398 = c * -(float)dist; }
                    { Prim lp_ = m_lastPrim; lp_.f_398 = m_lastPrim.f_398 - 20.0f; }
                    p = m_lastPrim;
                    FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                    p = m_lastPrim;
                    FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                    p = m_lastPrim;
                    M.Copy(p.A + 0x190, p.A + 0x160, 0x30);
                    { Prim lp_ = m_lastPrim; lp_.f_1d4 = 0x14; }
                    { Prim lp_ = m_lastPrim; lp_.f_324 = 200.0f; }
                    { Prim lp_ = m_lastPrim; lp_.f_1f0 = m_lastPrim.m_duration - 10; }
                    E3SetTexture(rt, m_lastPrim, "effect\\ice.tga");
                    angle = angle + 40.0f;
                    i++;
                } while (angle < 360.0f);
            }

            E3ProjectMaster(rt);
            if (m_stateCnt == 0 || m_stateCnt == 7)
            {
                Prim p = LaunchEffectPrim(rt, 0, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0x19;
                { Prim lp_ = m_lastPrim; lp_.f_2bc = 0.85f; }
                { Prim lp_ = m_lastPrim; lp_.f_30c = (float)(E3RandMod(rt, 0x28) - 0x14); }
                { Prim lp_ = m_lastPrim; lp_.f_354 = 1.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_360 = 1.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_2c8 = 0.5f; }
                { Prim lp_ = m_lastPrim; lp_.f_2cc = 0.5f; }
                { Prim lp_ = m_lastPrim; lp_.f_364 = 6.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_358 = 6.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_200 = 10; }
                int d = m_lastPrim.m_duration;
                { Prim lp_ = m_lastPrim; lp_.f_1f0 = d - d / 3; }
                E3SetTexture(rt, m_lastPrim, "effect\\smoke.tga");
                { Prim lp_ = m_lastPrim; lp_.f_3e8 = 0; }
            }
        }

        /// <summary>CRagEffect::fn_b35460 (0xb35460) — EF 102 (Hammer Fall): sound at 30, quake at 35, STR.</summary>
        public bool Fn_b35460(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x1e)
            {
                E3Sound(rt, "effect\\wizard_fire_pillar_b.wav", 0.0f);
                t = m_stateCnt;
            }
            if (t == 0x23) rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
            return rt.StrPlayer(this, 0f, -80.0f, 0f);
        }

        /// <summary>
        /// CRagEffect::fn_b357d0 (0xb357d0) — EF 38 (Decrease AGI): follows the master; at tick 0 a slow.bmp disc
        /// (prim 0) for the whole effect, every even tick a rising particle (prim 0x10, ac_center2.tga).
        /// </summary>
        public void Fn_b357d0(FxRuntime rt)
        {
            E3ProjectMaster(rt);
            E3FollowMaster();
            int t = m_stateCnt;
            if (t == 0)
            {
                E3Sound(rt, "effect\\EF_DecAgility.wav", 0.0f);
                Prim p = LaunchEffectPrim(rt, 0, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = f_17c;
                { Prim lp_ = m_lastPrim; lp_.f_398 = m_lastPrim.f_398 - 80.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_30c = 180.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_2bc = 1.0f; }
                p = m_lastPrim;
                p.f_2c0 = -(p.f_2bc / (float)p.m_duration);
                { Prim lp_ = m_lastPrim; lp_.f_354 = 40.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_360 = 10.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_324 = 0.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_2d4 = 200.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_328 = m_lastPrim.f_2d4 / 15.0f; }
                { Prim lp_ = m_lastPrim; lp_.f_1f0 = m_lastPrim.m_duration - 0xf; }
                E3SetTexture(rt, m_lastPrim, "effect\\slow.bmp");
                { Prim lp_ = m_lastPrim; lp_.f_3e8 = 0; }
                t = m_stateCnt;
            }
            if ((t & 1) == 0) E3RisingParticle(rt, 0x32, -0x5a, 0.015f, 0x1e, 0.18f, 200.0f, "effect\\ac_center2.tga", false);
        }

        /// <summary>
        /// The prim 0x10 "rising particle" block shared by fn_b357d0 and fn_b5c3d0: random yaw (MakeRotationY), offset
        /// 2..8 along the rotated Z axis, X rotation <paramref name="pitch"/>, speed/size/alpha as given.
        /// </summary>
        void E3RisingParticle(FxRuntime rt, int duration, int pitch, float speedOrZero, int sizeBase, float growth, float alpha,
            string texture, bool berserk)
        {
            Prim p = LaunchEffectPrim(rt, 0x10, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = duration;
            int yaw = E3RandMod(rt, 0x168);
            p = m_lastPrim;
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(yaw));
            p = m_lastPrim;
            float r = (float)(E3RandMod(rt, 7) + 2);
            p.f_394 = p.f_16c * 0.0f + p.f_160 * 0.0f + r * p.f_178 + p.f_184;
            p.f_398 = p.f_170 * 0.0f + p.f_164 * 0.0f + r * p.f_17c + p.f_188;
            p.f_39c = p.f_174 * 0.0f + p.f_168 * 0.0f + r * p.f_180 + p.f_18c;
            if (!berserk) { Prim lp_ = m_lastPrim; lp_.f_398 = m_lastPrim.f_398 - 20.0f; }
            p = m_lastPrim;
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(pitch));
            if (berserk) { Prim lp_ = m_lastPrim; lp_.f_2bc = (float)(E3RandMod(rt, 0x32) + 0x14) / 60.0f; }
            else { Prim lp_ = m_lastPrim; lp_.f_2c0 = speedOrZero; }
            { Prim lp_ = m_lastPrim; lp_.f_354 = (float)(E3RandMod(rt, 0x3c) + sizeBase) / 10.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_360 = growth; }
            if (berserk)
            {
                M.U32(m_lastPrim.A + 0x23c, 0xf0);
                { Prim lp_ = m_lastPrim; lp_.f_240 = 0; }
                { Prim lp_ = m_lastPrim; lp_.f_244 = 0; }
            }
            { Prim lp_ = m_lastPrim; lp_.f_324 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_2d4 = alpha; }
            { Prim lp_ = m_lastPrim; lp_.f_328 = m_lastPrim.f_2d4 / 20.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_1f0 = m_lastPrim.m_duration - 0x14; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_3e8 = 0; }
        }

        /// <summary>
        /// CRagEffect::fn_b5aa30 (0xb5aa30) — EF 128 (Power-Thrust): two sounds and a ring (prim 4, alpha_center.tga) at tick 0;
        /// duration forced to 9999.
        /// </summary>
        public void Fn_b5aa30(FxRuntime rt)
        {
            if (m_stateCnt == 0) E3Sound(rt, "effect\\black_overthrust.wav", 0.0f);
            E3ProjectMaster(rt);
            if (m_stateCnt != 0) return;
            f_17c = 9999;
            E3Sound(rt, "effect\\EF_StoneCurse.wav", 0.0f);
            Prim p = LaunchEffectPrim(rt, 4, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0x28;
            { Prim lp_ = m_lastPrim; lp_.f_398 = -20.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_288 = 10.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_2f4 = 10.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_34c = 7.0f; }
            p = m_lastPrim;
            float v = -(p.f_34c / (float)(p.m_duration + 0x28));
            p.f_350 = v + v;
            { Prim lp_ = m_lastPrim; lp_.f_1f0 = 0; }
            E3SetTexture(rt, m_lastPrim, "effect\\alpha_center.tga");
        }

        /// <summary>CRagEffect::fn_b5be00 (0xb5be00) — ring (prim 0xd, alpha_down.tga) rising at <paramref name="rise"/>, lasting <paramref name="duration"/>.</summary>
        public void Fn_b5be00(FxRuntime rt, float rise, int duration)
        {
            Prim p = LaunchEffectPrim(rt, 0xd, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = duration;
            { Prim lp_ = m_lastPrim; lp_.f_2f8 = 4.5f; }
            { Prim lp_ = m_lastPrim; lp_.f_2f4 = 4.5f; }
            { Prim lp_ = m_lastPrim; lp_.f_360 = 10.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_364 = rise; }
            { Prim lp_ = m_lastPrim; lp_.f_368 = 0.01f; }
            { Prim lp_ = m_lastPrim; lp_.f_324 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_2d4 = 90.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_328 = m_lastPrim.f_2d4 / 20.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_1f0 = m_lastPrim.m_duration - 10; }
            E3SetTexture(rt, m_lastPrim, "effect\\alpha_down.tga");
        }

        /// <summary>
        /// CRagEffect::fn_b5c3d0 (0xb5c3d0) — EF 220 (Aid Berserk Potion): tints the master (msg 0x7f), ring, quake + sound at 18,
        /// red particles every 4th tick in [13, 109], STR (result ignored); after tick 150 untints (msg 0x80) and ends.
        /// </summary>
        public bool Fn_b5c3d0(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                rt.GameObjectSendMsg(m_master, 0, 0x7f, 0xfa, 0x78, 0x78, 0, 0);
                Fn_b5be00(rt, 1.0f, 0x32);
                t = m_stateCnt;
            }
            if (t == 0x12)
            {
                rt.GameModeSendMsg(0x7a, 1, 0, 0, 0);
                rt.Host?.PlaySound3D("effect\\ac_concentration.wav", 0.0f, 0.0f, 0.0f, 0xfa, 0x28, 1.0f, 0);
            }
            E3FollowMaster();
            t = m_stateCnt;
            if ((uint)(t - 0xd) <= 0x60u && t % 4 == 0)
                E3RisingParticle(rt, 0x28, 0x5a, 0f, 0x1e, 0.2f, 180.0f, "effect\\ac_center2.tga", true);
            rt.StrPlayer(this, 0f, -80.0f, 0f);
            if (0x96 < m_stateCnt)
            {
                rt.GameObjectSendMsg(m_master, 0, 0x80, 0, 0, 0, 0, 0);
                return false;
            }
            return true;
        }

        /// <summary>CRagEffect::fn_b63f90 (0xb63f90) — EF 130/250: duration 9999, sound at tick 0, STR.</summary>
        public bool Fn_b63f90(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                f_17c = 9999;
                E3Sound(rt, "effect\\knight_twohandquicken.wav", 0.0f);
            }
            return rt.StrPlayer(this, 0f, -80.0f, 0f);
        }
    }
}
