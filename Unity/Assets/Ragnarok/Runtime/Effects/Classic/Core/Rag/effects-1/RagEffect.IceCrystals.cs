// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.IceCrystals.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>effects-1: handlers built on prim type 9 crystals (ice / stone), like Storm Gust.</summary>
    public readonly partial struct RagEffect
    {
        // ---- EF 28 Frost Diver (hit) ---------------------------------------------------------

        [FxEffectProcess(28)]
        public FxCaseResult Case_FrostDiverHit(FxRuntime rt) { Fn_b3baa0(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b3baa0 (0xb3baa0) — Frost Diver freeze: at tick 0 a sound and 8 ice crystals
        /// (prim 9, ice.tga) around the target at radius 0.1–0.4, 30 up, random tilt/heading.
        /// </summary>
        public void Fn_b3baa0(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\EF_FrostDiver2.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            for (int n = 8; n != 0; n--)
            {
                Prim p = LaunchEffectPrim(rt, 9, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0x28;
                p.f_1dc |= 1;
                int ang = RandMod(rt, 0x168);
                int k = rt.Rand.Next() & 3;
                float r = (float)(k + 1) / 10.0f;
                float s = FxMath.GetSin(ang);
                float c = FxMath.GetCos(ang);
                p.f_398 = 30.0f;
                p.f_394 = s * r;
                p.f_39c = -r * c;
                p.f_318 = (float)(RandMod(rt, 0xfa) + 100) / 100.0f;
                p.f_360 = (float)(RandMod(rt, 100) + 200) / 10.0f;
                p.f_30c = (float)RandMod(rt, 0x168);
                p.f_310 = (float)(RandMod(rt, 0x14) + 0x50);
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                M.Copy(p.A + 0x190, p.A + 0x160, 0x30);
                p.f_2bc = 3.0f;
                float v = -(p.f_2bc / (float)p.m_duration);
                p.f_2c0 = v + v;
                p.f_1d4 = 0x14;
                p.f_324 = 200.0f;
                p.f_1f0 = p.m_duration - 10;
                SetFirstTexture(rt, p, "effect\\ice.tga");
            }
        }
    }
}
