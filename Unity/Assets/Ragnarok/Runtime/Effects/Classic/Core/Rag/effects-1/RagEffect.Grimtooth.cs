// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.Grimtooth.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        [FxEffectProcess(123)]
        public FxCaseResult Case_Grimtooth_effects1(FxRuntime rt) { Fn_b3cc50(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b3cc50 (0xb3cc50) — Grimtooth: a line of stone crystals (prim 9, stone.bmp) walking
        /// from the caster towards the target (+0x180/+0x188) in steps of 2 every 3rd tick, until the walker
        /// is within 2.5 of the target (flag +0x170). Walker state: step +0x18c/+0x190/+0x194 and offset
        /// +0x198/+0x19c/+0x1a0 (floats; the field list types the step as int).
        /// </summary>
        public void Fn_b3cc50(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                rt.PlaySound3DRelative("effect\\EF_FrostDiver.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                rt.FaceTowards(A, f_180, f_188);
                float rad = FxMath.DegToRad(FxOps.Cvtt(-M.F32(A + 0x4c)));
                // FUN_005333e0 on a stack matrix: identity, then [0]=[8]=cos, [2]=-sin, [6]=sin
                float s = (float)System.Math.Sin(rad), c = (float)System.Math.Cos(rad);
                float l0 = c, l1 = 0f, l2 = -s, l3 = 0f, l4 = 1f, l5 = 0f, l6 = s, l7 = 0f, l8 = c, l9 = 0f, l10 = 0f, l11 = 0f;
                float x = l3 * 0f + l0 * 0f;
                x += l6 * 2.0f;
                x += l9;
                M.F32(A + 0x18c, x);
                float y = l4 * 0f + l1 * 0f;
                y += l7 * 2.0f;
                y += l10;
                M.F32(A + 0x190, y);
                float z = l5 * 0f + l2 * 0f;
                z += l8 * 2.0f;
                z += l11;
                f_198 = f_198 - x;
                M.F32(A + 0x194, z);
                f_19c = f_19c - y;
                f_1a0 = f_1a0 - z;
            }
            if (M.I32(A + 0x170) != 0) return;
            if (m_stateCnt % 3 == 0)
            {
                uint px = M.U32(A + 0x10), py = M.U32(A + 0x14), pz = M.U32(A + 0x18);
                float dist = rt.DistanceXZ(A, f_180, f_188);
                if (2.5f >= dist) M.I32(A + 0x170, 1);
                M.U32(A + 0x10, px); M.U32(A + 0x14, py); M.U32(A + 0x18, pz);
                Prim p = LaunchEffectPrim(rt, 9, 0f, 0f, 0f);
                m_lastPrim = p;
                p.f_1dc |= 1;
                p.m_duration = 0x28;
                RandMod(rt, 0x168);     // angle for GetSin/GetCos, results discarded
                rt.Rand.Next();
                p.f_394 = 0f;
                p.f_398 = 20.0f;
                p.f_39c = 0f;
                p.f_394 = f_198 + p.f_394;
                p.f_398 = f_19c + p.f_398;
                p.f_39c = f_1a0 + p.f_39c;
                p.f_318 = (float)(RandMod(rt, 0x28) + 0x3c) / 100.0f;
                p.f_360 = 10.0f;
                p.f_30c = (float)RandMod(rt, 0x168);
                p.f_310 = (float)(RandMod(rt, 0x1e) + 0x4b);
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                M.Copy(p.A + 0x190, p.A + 0x160, 0x30);
                p.f_2bc = 3.0f;
                float v = -(p.f_2bc / (float)p.m_duration);
                p.f_2c0 = v + v;
                p.f_1d4 = 6;
                p.f_1f0 = p.m_duration - 10;
                SetFirstTexture(rt, p, "effect\\stone.bmp");
            }
            f_198 = f_198 - M.F32(A + 0x18c);
            f_19c = f_19c - M.F32(A + 0x190);
            f_1a0 = f_1a0 - M.F32(A + 0x194);
        }
    }
}
