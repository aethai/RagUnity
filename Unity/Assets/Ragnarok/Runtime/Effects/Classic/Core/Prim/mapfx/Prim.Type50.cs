// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type50.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x50 — the waterfall spray drops of EF 349..356 (CRagEffect::fn_b91030); rendered by fn_aee7d0 (registered with 0x1d,
    /// kind +0x450 = 2 → the "other" alpha bands). Four slots of 0xb4 bytes, four drops each at +0x484 / +0x46c / +0x490 / +0x478
    /// (x, y, z), eight wander angles from +0x3fc, speed +0x3f0, swap flag +0x45b, spread +0x45c.
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>CEffectPrim::OnProcess case 0x50 (0xad7d1e): <c>fn_aec0c0(this); break;</c>.</summary>
        [FxPrimProcess(0x50)]
        public FxCaseResult Case50(FxRuntime rt)
        {
            Process_aec0c0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CEffectPrim::fn_aec0c0 (0xaec0c0) — camera angles (+0x3ec / +0x4a0), no position update; per live slot the eight angle
        /// pairs chase their targets (<see cref="ChaseAngles_aec0c0"/>), then each drop: while y &lt; 0 it drifts x / z by
        /// ±0.01·sin(angle) (signs (+, +), (−, −), (+, −), (−, +)); below −8 it respawns: x = rand % 45 − 22 (+0x45c == 0) or
        /// rand % 25 − 12, y = rand % 30, z = rand % 11 − 5 (swap flag 1: x = z, z = (float)(int)x); always y −= speed.
        /// Returns true.
        /// </summary>
        public bool Process_aec0c0(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                ChaseAngles_aec0c0(rt, s + 0x3fc, 8);
                Drop_aec0c0(rt, s, 0x484, 0x488, 0x48c, 0x3fc, 0x404, true, true);
                Drop_aec0c0(rt, s, 0x46c, 0x470, 0x474, 0x40c, 0x414, false, false);
                Drop_aec0c0(rt, s, 0x490, 0x494, 0x498, 0x41c, 0x424, true, false);
                Drop_aec0c0(rt, s, 0x478, 0x47c, 0x480, 0x42c, 0x434, false, true);
            }
            return true;
        }

        void Drop_aec0c0(FxRuntime rt, uint s, uint xo, uint yo, uint zo, uint ax, uint az, bool xPlus, bool zPlus)
        {
            float y = M.F32(s + yo);
            if (0f > y)
            {
                Drift_adc150(s + xo, s + ax, 0.01f, xPlus);                                 // [0xf4a690]
                Drift_adc150(s + zo, s + az, 0.01f, zPlus);
                if (-8f > y)                                                                // [0xff759c]
                {
                    int x = M.U8(s + 0x45c) == 0 ? RandMod_P1D(rt, 0x2d) - 0x16 : RandMod_P1D(rt, 0x19) - 0xc;
                    M.F32(s + xo, (float)x);
                    M.F32(s + yo, (float)RandMod_P1D(rt, 0x1e));
                    float z = (float)(RandMod_P1D(rt, 0xb) - 5);
                    M.F32(s + zo, z);
                    if (M.U8(s + 0x45b) == 1)
                    {
                        int xi = FxOps.Cvtt(M.F32(s + xo));
                        M.F32(s + xo, z);
                        M.F32(s + zo, (float)xi);
                    }
                }
            }
            M.F32(s + yo, M.F32(s + yo) - M.F32(s + 0x3f0));
        }

        /// <summary>
        /// The ±1 wander loop shared by fn_aec0c0 / fn_b11b60 (8 pairs) and fn_ae7020 (2 pairs): per (current, target) pair from
        /// <paramref name="first"/>: target &gt; current → current + 1 (max 359) and, once past the target, a new target rand() % (int)current;
        /// else (also NaN) current − 1 (min 0) and, unless still above the target (or NaN), a new target 359 − rand() % (int)current
        /// (no rand() when (int)current is 0).
        /// </summary>
        void ChaseAngles_aec0c0(FxRuntime rt, uint first, int pairs)
        {
            for (uint e = first; e < first + (uint)pairs * 8; e += 8)
            {
                float tgt = M.F32(e + 4);
                float cur = M.F32(e);
                if (tgt > cur)                                                              // COMISS tgt, cur; JBE
                {
                    cur = cur + 1f;                                                         // [0xf4a468]
                    if (cur > 359f) cur = 359f;                                             // [0xff758c]
                    M.F32(e, cur);
                    if (cur > tgt) M.F32(e + 4, (float)RandBelow_aec7d0(rt, cur));
                }
                else
                {
                    cur = cur - 1f;
                    if (0f > cur) cur = 0f;
                    M.F32(e, cur);
                    if (!(tgt < cur) && !float.IsNaN(cur) && !float.IsNaN(tgt))             // COMISS tgt, cur; JB
                        M.F32(e + 4, (float)(0x167 - RandBelow_aec7d0(rt, cur)));
                }
            }
        }
    }
}
