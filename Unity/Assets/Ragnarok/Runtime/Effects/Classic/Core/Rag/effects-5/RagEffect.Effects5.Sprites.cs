// Synced from reference/port/src/RoClassicFx/Rag/effects-5/RagEffect.Effects5.Sprites.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// The recurring "projected owner position" prologue: ProjectVertex (0x53b370) of owner +0x10 with the view
        /// matrix (mode+0xd0 → +0x98), screen x/y into f_154/f_158.
        /// </summary>
        void ProjectMasterToScreen(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint matrix = M.U32(rt.Mode + 0xd0) + 0x98;
            rt.Renderer.ProjectVertex(M, matrix, M.F32(master + 0x10), M.F32(master + 0x14), M.F32(master + 0x18),
                out float sx, out float sy, out _, out _);
            f_154 = sx;
            f_158 = sy;
        }

        /// <summary>prim.pos = rot(prim+0x160) · (0, 0, forward) + translation: the "(m0·0 + m3·0) + f·m6 + m9" rows.</summary>
        static void RotatedOffset(Prim p, float forward, uint dst)
        {
            p.M.F32(p.A + dst, p.f_16c * 0.0f + p.f_160 * 0.0f + forward * p.f_178 + p.f_184);
            p.M.F32(p.A + dst + 4, p.f_170 * 0.0f + p.f_164 * 0.0f + forward * p.f_17c + p.f_188);
            p.M.F32(p.A + dst + 8, p.f_174 * 0.0f + p.f_168 * 0.0f + forward * p.f_180 + p.f_18c);
        }

        /// <summary>
        /// CRagEffect::fn_b3ed70 (0xb3ed70): owner screen position; at tick 0 hit sound and a ring of 8 lens flares
        /// (prim 0, lens1/lens2 alternating) every 45°. <paramref name="tinted"/> sets colour 0x78/0x78.
        /// </summary>
        public void Fn_b3ed70(FxRuntime rt, int tinted)
        {
            ProjectMasterToScreen(rt);
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\EF_hit2.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            uint count = 0;
            float angle = 0.0f;
            do
            {
                Prim p = LaunchEffectPrim(rt, 0, 0f, 0f, 0f);
                m_lastPrim = p;
                p.f_1dc = 1;
                p.m_duration = RandMod(rt, 0x14) + 10;
                float a = (float)RandMod(rt, 0x1e) + (angle - 15.0f);
                p.f_314 = a;
                p.f_30c = a;
                p.f_2bc = (float)(RandMod(rt, 0x2d) + 5) / 10.0f;
                p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
                p.f_354 = (float)(RandMod(rt, 0xf) + 5);
                p.f_360 = (float)(RandMod(rt, 0x14) + 0x14);
                p.f_358 = -(p.f_354 / (float)p.m_duration);
                p.f_364 = 1.5f;
                p.f_368 = 0.25f;
                int r = RandMod(rt, 5);
                int deg = FxOps.Cvtt(angle);
                float fr = (float)r;
                p.f_394 = FxMath.GetSin(deg) * fr;
                p.f_398 = FxMath.GetCos(deg) * -(float)r;
                p.f_398 = p.f_398 - 20.0f;
                if (tinted != 0)
                {
                    M.I32(p.A + 0x23c, 0x78);
                    p.f_240 = 0x78;
                }
                p.f_324 = 0f;
                p.f_328 = p.f_2d4 * 0.125f;
                M.U8(p.A + 0x3e8, 0);
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                uint tex = rt.GetTexture((count & 1) == 0 ? "effect\\lens1.tga" : "effect\\lens2.tga");
                angle = angle + 45.0f;
                count++;
                M.U32((uint)p.f_144, tex);
            } while (360.0f > angle);
        }

        /// <summary>EF 0x1 (hit spark): fn_b3ed70(0).</summary>
        [FxEffectProcess(0x1)]
        public FxCaseResult Case_HitSpark(FxRuntime rt) { Fn_b3ed70(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 0x191 (Napalm Vulcan): Napalm Beat sound at ticks 20/30/40/50/60 at the owner, plus fn_b3ed70(0).</summary>
        [FxEffectProcess(0x191)]
        public FxCaseResult Case_NapalmVulcan(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x14 || t == 0x1e || t == 0x28 || t == 0x32 || t == 0x3c)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\EF_NapalmBeat.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Fn_b3ed70(rt, 0);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b41da0 (0xb41da0): Increase AGI — owner screen position and position; at tick 0 sound and the
        /// agi_up column (prim 0); every even tick a rising spark (prim 0x10) around the owner.
        /// </summary>
        public void Fn_b41da0(FxRuntime rt)
        {
            ProjectMasterToScreen(rt);
            FollowMaster();
            uint t = (uint)m_stateCnt;
            if (t == 0)
            {
                rt.PlaySound3DRelative("effect\\EF_IncAgility.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Prim p = LaunchEffectPrim(rt, 0, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = f_17c;
                p.f_30c = 0f;
                p.f_2bc = 1.5f;
                p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 1.2f;
                p.f_354 = 40.0f;
                p.f_360 = 20.0f;
                p.f_324 = 0f;
                p.f_2d4 = 200.0f;
                p.f_328 = p.f_2d4 / 15.0f;
                p.f_1f0 = p.m_duration - 0xf;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                M.U32((uint)p.f_144, rt.GetTexture("effect\\agi_up.bmp"));
                M.U8(p.A + 0x3e8, 0x63);
                t = (uint)m_stateCnt;
            }
            if ((t & 1) == 0)
            {
                Prim p = LaunchEffectPrim(rt, 0x10, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0x32;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(RandMod(rt, 0x168)));
                float fwd = (float)(RandMod(rt, 7) + 2);
                RotatedOffset(p, fwd, 0x394);
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(0x5a));
                p.f_2bc = (float)(RandMod(rt, 0x32) + 0x14) / 100.0f;
                p.f_354 = (float)(RandMod(rt, 0x3c) + 0x1e) / 10.0f;
                p.f_360 = 0.18f;
                p.f_324 = 0f;
                p.f_2d4 = 200.0f;
                p.f_328 = p.f_2d4 / 20.0f;
                p.f_1f0 = p.m_duration - 0x14;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                M.U32((uint)p.f_144, rt.GetTexture("effect\\ac_center2.tga"));
                M.U8(p.A + 0x3e8, 0x63);
            }
        }

        /// <summary>EF 0x25 (Increase AGI): fn_b41da0.</summary>
        [FxEffectProcess(0x25)]
        public FxCaseResult Case_IncreaseAgi(FxRuntime rt) { Fn_b41da0(rt); return FxCaseResult.Continue; }

        /// <summary>EF 0x79 (Sonic Blow): owner screen position; at tick 0 sound and a growing flash (prim 4, alpha_center).</summary>
        [FxEffectProcess(0x79)]
        public FxCaseResult Case_SonicBlow_effects5(FxRuntime rt)
        {
            ProjectMasterToScreen(rt);
            if (m_stateCnt != 0) return FxCaseResult.Continue;
            rt.PlaySound3DRelative("effect\\EF_StoneCurse.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchEffectPrim(rt, 4, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = f_17c;
            p.f_1f0 = 0;
            p.f_398 = -20.0f;
            p.f_288 = 10.0f;
            p.f_2f4 = 10.0f;
            p.f_34c = 7.0f;
            float v = -(p.f_34c / (float)(p.m_duration + 0x28));
            p.f_350 = v + v;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture("effect\\alpha_center.tga"));
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x27 (Aqua Benedicta): follows the owner; at tick 0 sound and the holy water sprite (prim 5).</summary>
        [FxEffectProcess(0x27)]
        public FxCaseResult Case_AquaBenedicta(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt != 0) return FxCaseResult.Continue;
            rt.PlaySound3DRelative("effect\\EF_Aqua.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = f_17c;
            p.f_398 = -20.0f;
            p.f_1e8 = 2;
            M.U8(p.A + 0x254, 0);
            rt.SetSprName(p.A, Fx5Names.HolyWaterSpr);
            rt.SetActName(p.A, Fx5Names.HolyWaterAct);
            p.SnapshotTrail_ad58d0();
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b31390 (0xb31390): Aspersio — sound at 70; every 5th tick in (60, 140) a particle sprite
        /// (prim 7) on a random circle; effect duration forced to 400; dies at the duration, else the STR player.
        /// </summary>
        public bool Fn_b31390(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x46)
            {
                rt.PlaySound3DRelative("effect\\priest_aspersio.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t % 5 == 0 && t > 0x3c && t < 0x8c)
            {
                Prim p = LaunchEffectPrim(rt, 7, 0f, 0f, 0f);
                m_lastPrim = p;
                f_17c = 400;
                p.m_duration = 0x28;
                p.f_348 = 1.5f;
                p.f_2ec = 0.19f;
                p.f_2f0 = 0.01f;
                p.f_30c = (float)RandMod(rt, 0x168);
                p.f_318 = 1.2f;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                float s = p.f_348;
                p.f_394 = (p.f_160 * 0.0f - p.f_16c * 30.0f) + s * p.f_178 + p.f_184;
                p.f_398 = (p.f_164 * 0.0f - p.f_170 * 30.0f) + s * p.f_17c + p.f_188;
                p.f_39c = (p.f_168 * 0.0f - p.f_174 * 30.0f) + s * p.f_180 + p.f_18c;
                p.f_1f0 = 0x1e;
                rt.SetSprName(p.A, Fx5Names.Particle1Spr);
                rt.SetActName(p.A, Fx5Names.Particle1Act);
                p.SnapshotTrail_ad58d0();
                t = m_stateCnt;
            }
            if (t == f_17c) return false;
            StrPlayerDefault(rt);
            return true;
        }

        /// <summary>EF 0x56 (Aspersio): fn_b31390.</summary>
        [FxEffectProcess(0x56)]
        public FxCaseResult Case_Aspersio(FxRuntime rt) => Fn_b31390(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// CRagEffect::fn_b37600 (0xb37600): Envenom — sounds at tick 0 (mode 0); every 5th tick (mode 0) or 9th tick
        /// (mode 1) a poison bubble sprite (prim 5, particle3) rising around the owner.
        /// </summary>
        public void Fn_b37600(FxRuntime rt, sbyte mode)
        {
            int t = m_stateCnt;
            if (t == 0 && mode == 0)
            {
                rt.PlaySound3DRelative("effect\\assasin_enchantpoison.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                rt.PlaySound3DRelative("effect\\EF_PoisonAttack.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            else if (mode == 1)
            {
                if (t % 9 != 0) return;
                LaunchPoisonBubble(rt, 170.0f);
                if (m_type == 0x304)
                {
                    Prim lp = m_lastPrim;
                    lp.f_1c4 = 0x201;
                    lp.SnapshotTrail_ad58d0();
                    return;
                }
                m_lastPrim.SnapshotTrail_ad58d0();
                return;
            }
            if (t % 5 != 0) return;
            LaunchPoisonBubble(rt, 100.0f);
            m_lastPrim.SnapshotTrail_ad58d0();
        }

        void LaunchPoisonBubble(FxRuntime rt, float sizeDiv)
        {
            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0x28;
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(RandMod(rt, 0x168)));
            float fwd = (float)(RandMod(rt, 6) + 2);
            RotatedOffset(p, fwd, 0x394);
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(0x5a));
            p.f_2bc = (float)(RandMod(rt, 0x1e) + 0x1e) / 100.0f;
            p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
            p.f_318 = (float)(RandMod(rt, 0x32) + 0x28) / sizeDiv;
            p.f_338 = 3.0f;
            p.f_324 = 0f;
            p.f_328 = p.f_2d4 / 10.0f;
            int d = p.m_duration;
            p.f_1f0 = d - d / 5;
            rt.SetSprName(p.A, Fx5Names.Particle3Spr);
            rt.SetActName(p.A, Fx5Names.Particle3Act);
        }

        /// <summary>EF 0x14 (Envenom): fn_b37600(0).</summary>
        [FxEffectProcess(0x14)]
        public FxCaseResult Case_Envenom(FxRuntime rt) { Fn_b37600(rt, 0); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b60bb0 (0xb60bb0): Soul Strike — f_174 (level) spirit bolts (prim 8, particle sprite) every 11
        /// ticks from tick 6, flying from the owner to the target f_180/f_188 in a fan.
        /// </summary>
        public void Fn_b60bb0(FxRuntime rt, sbyte variant)
        {
            int count = 0, step = 0, start = 0;
            switch (f_174)
            {
                case 1: count = 1; start = 0; step = 1; break;
                case 2: count = 2; step = 0xb4; start = -0x5a; break;
                case 3: count = 3; step = 0x5a; start = -0x5a; break;
                case 4: count = 4; step = 0x3c; start = -0x5a; break;
                case 5: count = 5; step = 0x2d; start = -0x5a; break;
                case 6: count = 6; step = 0x1e; start = -0x5a; break;
                case 7: count = 7; step = 0x19; start = -0x5a; break;
            }
            int t = m_stateCnt + 5;
            if (t % 0xb != 0) return;
            int n = t / 0xb;
            if (n > count) return;
            rt.PlaySound3DRelative("effect\\EF_SoulStrike.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchEffectPrim(rt, 8, 0f, 0f, 0f);
            m_lastPrim = p;
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
            p.f_314 = (float)(n * step + start);
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            p.f_3a0 = p.f_16c * 0.0f + p.f_160 * 0.0f + p.f_178 + p.f_184;
            p.f_3a4 = p.f_170 * 0.0f + p.f_164 * 0.0f + p.f_17c + p.f_188;
            p.f_3a8 = p.f_174 * 0.0f + p.f_168 * 0.0f + p.f_180 + p.f_18c;
            M.U32(p.A + 0x290, M.U32(p.A + 0x3a0));
            M.U32(p.A + 0x294, M.U32(p.A + 0x3a4));
            M.U32(p.A + 0x298, M.U32(p.A + 0x3a8));
            p.f_318 = 2.75f;
            rt.SetSprName(p.A, variant == 0 ? Fx5Names.Particle1Spr : Fx5Names.Particle5Spr);
            rt.SetActName(p.A, variant == 0 ? Fx5Names.Particle1Act : Fx5Names.Particle5Act);
            p.f_1f4 = 0xc;
            p.SnapshotTrail_ad58d0();
        }

        /// <summary>EF 0xf (Soul Strike): fn_b60bb0(0).</summary>
        [FxEffectProcess(0xf)]
        public FxCaseResult Case_SoulStrike(FxRuntime rt) { Fn_b60bb0(rt, 0); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b7eee0 (0xb7eee0): Crimson Fire Petal — follows the owner; fire arrow sound for ticks 12..15;
        /// for ticks &lt; 20 one sakura petal sprite (prim 0x7c) per tick around the owner with a random action.
        /// </summary>
        public void Fn_b7eee0(FxRuntime rt)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t >= 0xc && t <= 0xf)
            {
                int k = RandMod(rt, 3) + 1;
                rt.PlaySound3DRelative("effect\\EF_FireArrow" + k.ToString() + ".wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t >= 0x14) return;
            Prim p = LaunchEffectPrim(rt, 0x7c, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = f_17c;
            M.U8(p.A + 0x3e8, 1);
            p.f_3f0 = (float)(RandMod(rt, 3) + 2) * 0.15f;
            p.f_318 = (float)(RandMod(rt, 6) + 0x16) / 70.0f;
            M.U32(p.A + 0x488, M.U32(A + 0x14));
            short a = (short)RandMod(rt, 0x168);
            p.f_484 = FxMath.CosTableAt(a) * 8.0f + m_pos_x;
            p.f_48c = FxMath.SinTableAt(a) * 8.0f + m_pos_z;
            p.f_3fc = (float)RandMod(rt, 0x168);
            p.f_400 = (float)RandMod(rt, 0x168);
            p.f_404 = (float)RandMod(rt, 0x168);
            M.F32(p.A + 0x408, (float)RandMod(rt, 0x168));
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            p.f_324 = 250.0f;
            rt.SetSprName(p.A, Fx5Names.Sakura01Spr);
            rt.SetActName(p.A, Fx5Names.Sakura01Act);
            rt.SetAction(p.A, RandMod(rt, 3), 0x24, 0);
            p.SnapshotTrail_ad58d0();
        }

        /// <summary>EF 0x26a (Crimson Fire Petal, vf15): fn_b7eee0 plus 2 aura prims (style 2).</summary>
        [FxEffectProcess(0x26a)]
        public FxCaseResult Case_CrimsonFirePetal(FxRuntime rt)
        {
            Fn_b7eee0(rt);
            for (int i = 0; i < 2; i++) Fn_b66d40(rt, "effect\\alpha_center.tga", (sbyte)i, 2);
            return FxCaseResult.Continue;
        }
    }
}
