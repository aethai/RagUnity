// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.Firefly.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>EF 45 (EF_FIREFLY) — the map fireflies (packages/mapfx).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 45 (0x2d, EF_FIREFLY; OnProcess case 0xb53b1b): <c>fn_b3a7c0(this); break;</c>.</summary>
        [FxEffectProcess(0x2d)]
        public FxCaseResult Case_Firefly(FxRuntime rt)
        {
            Fn_b3a7c0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b3a7c0 (0xb3a7c0) — one firefly: on tick 0 a sprite prim (type 5, flags +0x1dc |= 4 | 0x80)
        /// showing 이팩트\particle1.spr / .act, prim and effect life 140 (0x8c). Random: +0x2bc = (15 + rand % 35) / 100,
        /// heading angles +0x30c / +0x310 = rand % 360 (matrix +0x160 = RotY(+0x30c) then RotX(+0x310)), start offset
        /// (+0x394.. = the matrix applied to (0, 2 + rand % 13, 0), y − 10), frame delay +0x1d4 = 5 + rand % 25, scale
        /// +0x318 = (30 + rand % 70) / 100. Alpha 0 → max 80 (+0x324 / +0x2d4, step +0x328 = 80 / 1.5), +0x154 = 1,
        /// +0x1d8 = 0, and the keyframe vectors +0x258 / +0x264 / +0x270 get (40, 120, 200) and (100, 0, 80).
        /// The RSW record's params (+0x13c..+0x148) are not read.
        /// </summary>
        public void Fn_b3a7c0(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1dc |= 4;
            p.f_1dc |= 0x80;
            p.m_duration = 0x8c;
            f_17c = 0x8c;
            int r = RandMod(rt, 0x23);
            p.f_2bc = (float)(r + 0xf) / 100f;                                   // [0xf7d9e4] = 100
            p.f_30c = (float)RandMod(rt, 0x168);
            p.f_310 = (float)RandMod(rt, 0x168);
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            FxMatrix.RotateX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));

            uint m = p.A + 0x160;
            float n = (float)(RandMod(rt, 0xd) + 2);
            // (0, n, 0) through the 4x3 matrix; the client keeps the multiplications by 0 ([0xf79a04]) and the + 0.
            M.F32(p.A + 0x394, (((M.F32(m + 0xc) * 0f + M.F32(m) * 0f) + n * M.F32(m + 0x18)) + M.F32(m + 0x24)) + 0f);
            M.F32(p.A + 0x398, (((M.F32(m + 4) * 0f + M.F32(m + 0x10) * 0f) + n * M.F32(m + 0x1c)) + M.F32(m + 0x28)) - 10f);
            M.F32(p.A + 0x39c, (((M.F32(m + 0x14) * 0f + M.F32(m + 8) * 0f) + n * M.F32(m + 0x20)) + M.F32(m + 0x2c)) + 0f);

            p.f_1d4 = RandMod(rt, 0x19) + 5;
            p.f_318 = (float)(RandMod(rt, 0x46) + 0x1e) / 100f;
            p.f_324 = 0f;
            p.f_2d4 = 80f;
            p.f_328 = p.f_2d4 / 1.5f;                                            // [0xf7e110] = 1.5
            p.f_154 = 1;
            p.f_1d8 = 0;
            rt.FloatVectorPushBack(p.A + 0x258, 40f);
            rt.FloatVectorPushBack(p.A + 0x264, 120f);
            rt.FloatVectorPushBack(p.A + 0x270, 200f);
            rt.FloatVectorPushBack(p.A + 0x258, 100f);
            rt.FloatVectorPushBack(p.A + 0x264, 0f);
            rt.FloatVectorPushBack(p.A + 0x270, 80f);
            rt.SetSprName(p.A, MapFxNames.Particle1Spr);
            rt.SetActName(p.A, MapFxNames.Particle1Act);
            p.SnapshotTrail_ad58d0();
        }
    }
}
