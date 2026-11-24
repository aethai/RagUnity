// Synced from reference/port/src/RoClassicFx/Rag/effects-7/RagEffect.Effects7.Hits.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-7: the melee hit sparks (EF 0 / 2 / 3), Bowling Bash (EF 73) and Pierce (EF 81): a flat flash
    /// (prim 0xd) turned against the owner's heading plus sprite sparks (prims 5 / 6, particle1) sprayed around it.
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>(int)(180 - owner heading) (CVTTSS2SI), + 360 when negative (TEST; CMOVNS).</summary>
        int FacingAwayFromOwnerE7()
        {
            int d = FxOps.Cvtt(180.0f - M.F32(M.U32(A + 0x138) + 0x4c));
            return d < 0 ? d + 0x168 : d;
        }

        /// <summary>
        /// The flash prologue: prim 0xd of <paramref name="duration"/> ticks, heading <paramref name="dir"/>,
        /// laid flat (x rotation from f_310 = -90, then y rotation from f_30c), 10 above the owner, width f_2bc 0.7
        /// shrinking by half over the duration.
        /// </summary>
        Prim LaunchFlatFlashE7(FxRuntime rt, int duration, int dir)
        {
            Prim p = LaunchLast(rt, 0xd);
            p.m_duration = duration;
            p.f_30c = (float)dir;
            p.f_310 = -90.0f;
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
            FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            p.f_394 = 0.0f;
            p.f_398 = -10.0f;
            p.f_39c = 0.0f;
            p.f_2bc = 0.7f;
            p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
            return p;
        }

        /// <summary>
        /// The spark prologue shared by fn_b3e470 / fn_b3f1b0 / fn_b3f960 / fn_b5aee0: sprite prim
        /// <paramref name="type"/>, heading dir + <paramref name="headingOffset"/> + rand%80, pitch rand%100 - 50
        /// (y then x rotation), pushed (rand%4 + 4) forward into f_3a0.., drawn from there 10 higher (f_394..).
        /// </summary>
        Prim LaunchSparkE7(FxRuntime rt, int type, int dir, int headingOffset)
        {
            Prim p = LaunchLast(rt, type);
            int r = RandMod(rt, 0x50);
            p.f_30c = (float)(r + (dir + headingOffset));
            r = RandMod(rt, 100);
            p.f_310 = (float)(r - 0x32);
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            FxMatrix.RotateX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
            float fwd = (float)(RandMod(rt, 4) + 4);
            RotatedOffset(p, fwd, 0x3a0);
            p.f_394 = p.f_3a0 + 0.0f;
            p.f_398 = p.f_3a4 - 10.0f;
            p.f_39c = p.f_3a8 + 0.0f;
            return p;
        }

        /// <summary>The spark epilogue: f_1f0 = 0, particle1 sprite, 3 trail entries, trail snapshot.</summary>
        void FinishSparkE7(FxRuntime rt, Prim p)
        {
            p.f_1f0 = 0;
            rt.SetSprName(p.A, Effects1Strings.SprParticle1);
            rt.SetActName(p.A, Effects1Strings.ActParticle1);
            p.f_1f4 = 3;
            p.SnapshotTrail_ad58d0();
        }

        /// <summary>
        /// Prim-5 spark (upward): size f_2bc = (rand%<paramref name="sizeRange"/> + <paramref name="sizeBase"/>)/100
        /// shrinking by half over rand%24 + 6 ticks, scale f_318 = (rand%100 + 60)/100 (shrinking to 0 when
        /// <paramref name="shrinkScale"/>).
        /// </summary>
        void SparkUpE7(FxRuntime rt, int dir, int sizeRange, int sizeBase, bool shrinkScale)
        {
            Prim p = LaunchSparkE7(rt, 5, dir, -0x28);
            p.f_2bc = (float)(RandMod(rt, sizeRange) + sizeBase) / 100.0f;
            p.m_duration = RandMod(rt, 0x18) + 6;
            p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
            p.f_318 = (float)(RandMod(rt, 100) + 0x3c) / 100.0f;
            if (shrinkScale) p.f_31c = -(p.f_318 / (float)p.m_duration);
            FinishSparkE7(rt, p);
        }

        /// <summary>
        /// Prim-6 spark (falling, behind the owner: heading + 140): y rotation again, duration rand%24 + 6, size
        /// (rand%90 + 60)/100, fall speed f_2ec = -(rand%90 + 30)/100 and f_2f0 = -2·f_2ec/duration.
        /// </summary>
        void SparkDownE7(FxRuntime rt, int dir, bool shrinkScale)
        {
            Prim p = LaunchSparkE7(rt, 6, dir, 0x8c);
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            p.m_duration = RandMod(rt, 0x18) + 6;
            p.f_2bc = (float)(RandMod(rt, 0x5a) + 0x3c) / 100.0f;
            p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
            p.f_2ec = -(float)(RandMod(rt, 0x5a) + 0x1e) / 100.0f;
            float v = p.f_2ec / (float)p.m_duration;
            p.f_2f0 = -(v + v);
            p.f_318 = (float)(RandMod(rt, 100) + 0x3c) / 100.0f;
            if (shrinkScale) p.f_31c = -(p.f_318 / (float)p.m_duration);
            FinishSparkE7(rt, p);
        }

        // ---- EF 0 EF_HIT1 ------------------------------------------------------------------------------

        /// <summary>
        /// CRagEffect::fn_b3e470 (0xb3e470) — EF_HIT1, the generic hit: at tick 0 a ring_blue flash (10 ticks) against
        /// the owner's heading and, unless /mineffect, two rising and two falling particle1 sparks. No sound.
        /// </summary>
        public void Fn_b3e470(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            int dir = FacingAwayFromOwnerE7();
            Prim p = LaunchFlatFlashE7(rt, 10, dir);
            p.f_2f8 = 10.0f;
            p.f_2f4 = 5.0f;
            p.f_360 = 3.5f;
            int d = p.m_duration;
            p.f_1f0 = d - d / 2;
            SetPrimTexture(rt, p, "effect\\ring_blue.tga");
            if (rt.MinEffect) return;
            for (int i = 0; i < 2; i++) SparkUpE7(rt, dir, 0x5a, 0x3c, false);
            for (int i = 0; i < 2; i++) SparkDownE7(rt, dir, false);
        }

        /// <summary>EF 0 (EF_HIT1): fn_b3e470.</summary>
        [FxEffectProcess(0x0)]
        public FxCaseResult Case_Hit1(FxRuntime rt) { Fn_b3e470(rt); return FxCaseResult.Continue; }

        // ---- EF 2 EF_HIT3 ------------------------------------------------------------------------------

        /// <summary>The two-layer lens2 flash of EF 2 / 3: 15 ticks, alpha out over the last third.</summary>
        void LensFlashE7(FxRuntime rt, int dir, float f2f8, float f2f4, float f364, float f368)
        {
            Prim p = LaunchFlatFlashE7(rt, 0xf, dir);
            p.f_2f8 = f2f8;
            p.f_2f4 = f2f4;
            p.f_364 = f364;
            p.f_368 = f368;
            int d = p.m_duration;
            p.f_1f0 = d - d / 3;
            SetPrimTexture(rt, p, "effect\\lens2.tga");
        }

        /// <summary>
        /// CRagEffect::fn_b3f1b0 (0xb3f1b0) — EF_HIT3: at tick 0 sound, two lens2 flashes against the owner's heading
        /// and eight rising particle1 sparks (also under /mineffect).
        /// </summary>
        public void Fn_b3f1b0(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\EF_hit3.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            int dir = FacingAwayFromOwnerE7();
            LensFlashE7(rt, dir, 1.5f, 1.5f, 0.5f, 0.2f);
            LensFlashE7(rt, dir, 4.0f, 1.5f, 0.25f, 0.2f);
            for (int i = 0; i < 8; i++) SparkUpE7(rt, dir, 0x78, 0x50, true);
        }

        /// <summary>EF 2 (EF_HIT3): fn_b3f1b0.</summary>
        [FxEffectProcess(0x2)]
        public FxCaseResult Case_Hit3(FxRuntime rt) { Fn_b3f1b0(rt); return FxCaseResult.Continue; }

        // ---- EF 3 EF_HIT4 ------------------------------------------------------------------------------

        /// <summary>
        /// CRagEffect::fn_b3f960 (0xb3f960) — EF_HIT4: every tick the owner's screen position (f_154/f_158); at tick 0
        /// sound, one lens2 flash and five rising particle1 sparks.
        /// </summary>
        public void Fn_b3f960(FxRuntime rt)
        {
            ProjectMasterToScreen(rt);
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\EF_hit4.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            int dir = FacingAwayFromOwnerE7();
            LensFlashE7(rt, dir, 4.0f, 0.5f, 0.25f, 0.15f);
            for (int i = 0; i < 5; i++) SparkUpE7(rt, dir, 0x5a, 0x3c, true);
        }

        /// <summary>EF 3 (EF_HIT4): fn_b3f960.</summary>
        [FxEffectProcess(0x3)]
        public FxCaseResult Case_Hit4(FxRuntime rt) { Fn_b3f960(rt); return FxCaseResult.Continue; }

        // ---- EF 73 Bowling Bash --------------------------------------------------------------------------

        /// <summary>
        /// CRagEffect::fn_b33f00 (0xb33f00) — Bowling Bash: at ticks 0 and 5 a ring_blue slash (prim 0xd, 20 - t ticks,
        /// heading turned by 20·t, x rotation from the prim's own f_310), plus at tick 0 a rising ring_yellow
        /// cylinder (prim 0xc).
        /// </summary>
        public void Fn_b33f00(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t != 0 && t != 5) return;
            int d = FxOps.Cvtt(180.0f - M.F32(M.U32(A + 0x138) + 0x4c));
            Prim p = LaunchLast(rt, 0xd);
            p.m_duration = 0x14 - m_stateCnt;
            int dir = d < 0 ? d + 0x168 : d;
            p.f_30c = (float)(dir + m_stateCnt * 0x14);
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
            FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            p.f_2f8 = 8.0f;
            p.f_300 = 0.5f;
            p.f_308 = -0.03f;
            p.f_2f4 = 3.0f;
            p.f_2fc = 0.5f;
            p.f_304 = -0.03f;
            p.f_360 = 3.5f;
            p.f_324 = (float)(0xf0 - m_stateCnt * 7);
            int dur = p.m_duration;
            p.f_1f0 = dur - dur / 2;
            SetPrimTexture(rt, p, "effect\\ring_blue.tga");
            if (m_stateCnt != 0) return;
            Prim q = LaunchLast(rt, 0xc);
            q.m_duration = 0x32;
            q.f_1c4 |= 0x200;
            q.f_310 = 90.0f;
            q.f_348 = 8.0f;
            q.f_34c = 0.7f;
            q.f_350 = -(q.f_34c / 30.0f);
            q.f_2f4 = 5.0f;
            q.f_200 = 0x14;
            q.f_2a0 = 0.0f;
            q.f_29c = 0.08f;
            q.f_324 = 45.0f;
            q.f_1f0 = 0x23;
            q.SnapshotTrail_ad58d0();
            SetPrimTexture(rt, q, "effect\\ring_yellow.tga");
        }

        /// <summary>EF 73 (0x49, Bowling Bash): fn_b33f00.</summary>
        [FxEffectProcess(0x49)]
        public FxCaseResult Case_BowlingBash(FxRuntime rt) { Fn_b33f00(rt); return FxCaseResult.Continue; }

        // ---- EF 81 Pierce ---------------------------------------------------------------------------------

        /// <summary>
        /// CRagEffect::fn_b5aee0 (0xb5aee0) — Pierce / Spear Stab / Spiral Pierce: every 20 ticks while t &lt; 20·f_16c
        /// (hit count) a ring_yellow flash growing with t (f_2f8/f_2f4 = t·0.1 + 11 / 6 in double), and from the second
        /// hit on four rising and four falling particle1 sparks.
        /// </summary>
        public void Fn_b5aee0(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t % 0x14 != 0 || t >= f_16c * 0x14) return;
            int dir = FacingAwayFromOwnerE7();
            Prim p = LaunchFlatFlashE7(rt, 0xf, dir);
            p.f_2f8 = (float)((double)m_stateCnt * 0.1 + 11.0);
            p.f_2f4 = (float)((double)m_stateCnt * 0.1 + 6.0);
            p.f_360 = 2.5f;
            int d = p.m_duration;
            p.f_1f0 = d - d / 2;
            SetPrimTexture(rt, p, "effect\\ring_yellow.tga");
            if (m_stateCnt <= 0) return;
            for (int i = 0; i < 4; i++) SparkUpE7(rt, dir, 0x5a, 0x3c, true);
            for (int i = 0; i < 4; i++) SparkDownE7(rt, dir, true);
        }

        /// <summary>EF 81 (0x51, Pierce): fn_b5aee0.</summary>
        [FxEffectProcess(0x51)]
        public FxCaseResult Case_Pierce(FxRuntime rt) { Fn_b5aee0(rt); return FxCaseResult.Continue; }
    }
}
