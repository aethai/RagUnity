// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.SpriteParticles.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>effects-1: handlers whose particles are sprite prims (SetSprName/SetActName + SnapshotTrail_effects1).</summary>
    public readonly partial struct RagEffect
    {

        // ---- EF 10 Mammonite ------------------------------------------------------------------

        [FxEffectProcess(10)]
        public FxCaseResult Case_Mammonite(FxRuntime rt) => Fn_b34830(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// CRagEffect::fn_b34830 (0xb34830) — Mammonite: at tick 20 (not /mineffect) five coin sprites
        /// (prim 6) thrown outwards; STR at (0,-80,0); alive while m_stateCnt &lt; 100.
        /// </summary>
        public bool Fn_b34830(FxRuntime rt)
        {
            if (!rt.MinEffect && m_stateCnt == 0x14)
            {
                for (int n = 5; n != 0; n--)
                {
                    Prim p = LaunchEffectPrim(rt, 6, 0f, 0f, 0f);
                    m_lastPrim = p;
                    p.m_duration = RandMod(rt, 0x1e) + 0x28;
                    p.f_30c = (float)(RandMod(rt, 0xc) * 0x1e);
                    FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));

                    int bit = rt.Rand.Next() & 1;   // (rand() & 0x80000001, sign-fixed) → 0/1
                    float s = (float)(bit + 2);
                    uint m = p.A + 0x160;
                    float x = M.F32(m + 0xc) * 0f + M.F32(m) * 0f;
                    x += s * M.F32(m + 0x18);
                    x += M.F32(m + 0x24);
                    float y = M.F32(m + 0x10) * 0f + M.F32(m + 4) * 0f;
                    y += s * M.F32(m + 0x1c);
                    x += 0f;
                    y += M.F32(m + 0x28);
                    float z = M.F32(m + 0x14) * 0f + M.F32(m + 8) * 0f;
                    y -= 10.0f;
                    z += s * M.F32(m + 0x20);
                    z += M.F32(m + 0x2c);
                    p.f_394 = x;
                    p.f_398 = y;
                    z += 0f;
                    p.f_39c = z;
                    FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));

                    p.f_2bc = (float)(RandMod(rt, 0x32) + 0x32) / 100.0f;
                    p.f_2c0 = -(p.f_2bc / (float)p.m_duration) / 3.5f;
                    p.f_2ec = -(float)(RandMod(rt, 100) + 0x32) / 25.0f;
                    p.f_2f0 = -((p.f_2ec / (float)p.m_duration) * 4.0f);
                    p.f_318 = 2.0f;
                    p.f_1f0 = p.m_duration - p.m_duration / 3;
                    p.f_208 = 1;
                    p.f_1e8 = 1;
                    rt.SetSprName(p.A, Effects1Strings.SprCoin);
                    rt.SetActName(p.A, Effects1Strings.ActCoin);
                    p.f_1f4 = 2;
                    p.SnapshotTrail_ad58d0();
                }
            }
            rt.StrPlayer(this, 0f, -80.0f, 0f);
            return m_stateCnt < 100;
        }

        // ---- EF 21 Detoxify -------------------------------------------------------------------

        [FxEffectProcess(21)]
        public FxCaseResult Case_Detoxify(FxRuntime rt) { Fn_b36170(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b36170 (0xb36170) — Detoxify: every 5th tick (15th with /mineffect) one rising
        /// particle2 sprite (prim 5); sound at tick 0.
        /// </summary>
        public void Fn_b36170(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (!rt.MinEffect ? t % 5 != 0 : t % 0xf != 0) return;
            if (t == 0)
                rt.PlaySound3DRelative("effect\\EF_Detoxication.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_324 = 0f;
            p.m_duration = 0x28;
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(RandMod(rt, 0x168)));
            float s = (float)(RandMod(rt, 6) + 2);
            uint m = p.A + 0x160;
            float x = M.F32(m + 0xc) * 0f + M.F32(m) * 0f;
            x += s * M.F32(m + 0x18);
            x += M.F32(m + 0x24);
            p.f_394 = x;
            float y = M.F32(m + 0x10) * 0f + M.F32(m + 4) * 0f;
            y += s * M.F32(m + 0x1c);
            y += M.F32(m + 0x28);
            p.f_398 = y;
            float z = M.F32(m + 0x14) * 0f + M.F32(m + 8) * 0f;
            z += s * M.F32(m + 0x20);
            z += M.F32(m + 0x2c);
            p.f_39c = z;
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(0x5a));
            p.f_2bc = (float)(RandMod(rt, 0x1e) + 0x1e) / 100.0f;
            p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
            p.f_318 = (float)(RandMod(rt, 0x32) + 0x28) / 100.0f;
            p.f_338 = 3.0f;
            p.f_328 = p.f_2d4 / 10.0f;
            p.f_1f0 = p.m_duration - p.m_duration / 5;
            rt.SetSprName(p.A, Effects1Strings.SprParticle2);
            rt.SetActName(p.A, Effects1Strings.ActParticle2);
            p.SnapshotTrail_ad58d0();
        }

        // ---- EF 24 Fire Ball ------------------------------------------------------------------

        [FxEffectProcess(24)]
        public FxCaseResult Case_FireBall(FxRuntime rt) { Fn_b3a500(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b3a500 (0xb3a500) — Fire Ball flight: at ticks 20/24/28/32 a fireball sprite
        /// (prim 5) aimed at the effect target (+0x180/+0x188), trailing copies fainter and tinted.
        /// </summary>
        public void Fn_b3a500(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t != 0x14 && t != 0x18 && t != 0x1c && t != 0x20) return;
            rt.PlaySound3DRelative("effect\\EF_FireBall.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchProjectileSprite(rt, 0x14);
            p.f_318 = 1.3f;
            p.f_1e8 = 1;
            if (m_stateCnt != 0x14)
            {
                M.U32(p.A + 0x23c, 0xf6);
                p.f_240 = 199;
                p.f_244 = 0x4c;
                int s = m_stateCnt;
                p.f_324 = s == 0x18 ? 180.0f : s == 0x1c ? 130.0f : 80.0f;
            }
            rt.SetSprName(p.A, Effects1Strings.SprFireball);
            rt.SetActName(p.A, Effects1Strings.ActFireball);
            p.SnapshotTrail_ad58d0();
        }

        /// <summary>
        /// The projectile preamble shared by fn_b3a500 / fn_b61000: prim 5 of <paramref name="duration"/> ticks,
        /// flags |= 0x120, offset (0,-15,0), heading towards the target, speed = -distance / duration.
        /// </summary>
        Prim LaunchProjectileSprite(FxRuntime rt, int duration)
        {
            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = duration;
            p.f_1dc |= 0x120;
            p.f_394 = 0f;
            p.f_398 = -15.0f;
            p.f_39c = 0f;
            rt.FaceTowards(p.A, f_180, f_188);
            p.f_30c = -p.f_4c;
            p.f_4c = 180.0f - p.f_4c;
            float h = p.f_4c;
            if (0f > h) p.f_4c = h + 360.0f;
            float dist = rt.DistanceXZ(p.A, f_180, f_188);
            p.f_2bc = -(float)((double)dist / (double)(float)p.m_duration);
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            return p;
        }

        // ---- EF 171 Venom Dust --------------------------------------------------------------------

        /// <summary>OnProcess case 0xab (inline) — Venom Dust: every 5th tick a rising particle3 sprite (prim 5).</summary>
        [FxEffectProcess(171)]
        public FxCaseResult Case_VenomDust(FxRuntime rt)
        {
            if ((float)(m_stateCnt % 5) != 0f) return FxCaseResult.Continue;
            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 10;
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(RandMod(rt, 0x168)));
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(0x5a));
            p.f_2bc = (float)(RandMod(rt, 0x14) + 0x14) / 80.0f;
            p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
            p.f_318 = 0.8f;
            p.f_338 = 3.0f;
            p.f_324 = 70.0f;
            p.f_328 = p.f_2d4 / 10.0f;
            p.f_1f0 = p.m_duration - p.m_duration / 5;
            rt.SetSprName(p.A, Effects1Strings.SprParticle3);
            rt.SetActName(p.A, Effects1Strings.ActParticle3);
            p.SnapshotTrail_ad58d0();
            return FxCaseResult.Continue;
        }
        // ---- EF 80 Spear Boomerang (NOT registered, see NOTES) ---------------------------------------

        /// <summary>
        /// CRagEffect::fn_b61000 (0xb61000) — Spear Boomerang: at ticks 0/4/8/12 a spear sprite (prim 5) thrown at
        /// the target like fn_b3a500, then SetAction (vf07) on the EFFECT itself with the owner's screen
        /// direction. The effect has no ACT (+0x108 == 0) in the client, so that call faults in the original
        /// (0x6c16b0 reads [0 + 0x130]); only reachable when something gave the effect an ACT.
        /// </summary>
        public void Fn_b61000(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t != 0 && t != 4 && t != 8 && t != 0xc) return;
            rt.PlaySound3DRelative("effect\\EF_FireBall.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchProjectileSprite(rt, 0xf);
            p.f_318 = 1.0f;
            p.f_1e8 = 1;
            int s = m_stateCnt;
            if (s != 0) p.f_324 = s == 4 ? 180.0f : s == 8 ? 130.0f : 80.0f;
            int dir = rt.DirectionIndex(M.F32(M.U32(A + 0x138) + 0x4c), 1);
            rt.SetAction(A, dir, 1, 1);
            rt.SetSprName(p.A, Effects1Strings.SprSpear);
            rt.SetActName(p.A, Effects1Strings.ActSpear);
            p.SnapshotTrail_ad58d0();
        }
    }
}
