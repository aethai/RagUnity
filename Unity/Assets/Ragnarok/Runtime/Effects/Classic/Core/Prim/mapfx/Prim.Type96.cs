// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type96.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x96 — the scaled, turned and tinted spray of EF 846 (CRagEffect::fn_b9f770): as type 0x50 plus a yaw +0x3ee,
    /// a scale +0x466 (tenths), and an RGB from the digit bytes +0x45a / +0x45b / +0x45c (× 2.57).
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>CEffectPrim::OnProcess case 0x96 (0xad7d2a): <c>fn_b11b60(this); break;</c>.</summary>
        [FxPrimProcess(0x96)]
        public FxCaseResult Case96(FxRuntime rt)
        {
            Process_b11b60(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>CEffectPrim::Render case 0x96: fn_b1dfe0(this, viewMatrix).</summary>
        [FxPrimRender(0x96)]
        public void Render96(FxRuntime rt, uint viewMatrix) => Render_b1dfe0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b11b60 (0xb11b60) — camera angles; per live slot (scale s = (short)+0x466 / 10, yaw = (short)+0x3ee) the
        /// eight angle pairs wander (±1, <see cref="ChaseAngles_aec0c0"/>), then each drop: while y &lt; 0 drift x / z by
        /// ±0.01·sin(angle) (signs as type 0x50); below −8·s respawn: x = rand % 45 − 22 (drop 0 with +0x45c != 0: rand % 25 − 12),
        /// y = rand % 30, z = rand % 11 − 5, all × s, (x, z) turned by the yaw (x' = cos·x − sin·z, z' = sin·x + cos·z); always
        /// y −= speed (+0x3f0). Returns true.
        /// </summary>
        public bool Process_b11b60(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                short yaw = M.I16(s + 0x3ee);
                float scale = (float)M.I16(s + 0x466) / 10f;                                // [0xf4b624]
                ChaseAngles_aec0c0(rt, s + 0x3fc, 8);
                Drop_b11b60(rt, s, scale, yaw, 0x484, 0x488, 0x48c, 0x3fc, 0x404, true, true, true);
                Drop_b11b60(rt, s, scale, yaw, 0x46c, 0x470, 0x474, 0x40c, 0x414, false, false, false);
                Drop_b11b60(rt, s, scale, yaw, 0x490, 0x494, 0x498, 0x41c, 0x424, true, false, false);
                Drop_b11b60(rt, s, scale, yaw, 0x478, 0x47c, 0x480, 0x42c, 0x434, false, true, false);
            }
            return true;
        }

        void Drop_b11b60(FxRuntime rt, uint s, float scale, short yaw, uint xo, uint yo, uint zo, uint ax, uint az,
            bool xPlus, bool zPlus, bool spreadByte)
        {
            float y = M.F32(s + yo);
            if (0f > y)
            {
                Drift_adc150(s + xo, s + ax, 0.01f, xPlus);                                 // [0xf4a690]
                Drift_adc150(s + zo, s + az, 0.01f, zPlus);
                if (scale * -8f > y)                                                        // [0xff759c]
                {
                    int xr = spreadByte && M.U8(s + 0x45c) != 0 ? RandMod_P1D(rt, 0x19) - 0xc : RandMod_P1D(rt, 0x2d) - 0x16;
                    M.F32(s + xo, (float)xr);
                    M.F32(s + yo, (float)RandMod_P1D(rt, 0x1e));
                    int zr = RandMod_P1D(rt, 0xb) - 5;
                    float x = scale * M.F32(s + xo);
                    y = scale * M.F32(s + yo);
                    M.F32(s + xo, x);
                    M.F32(s + yo, y);
                    float z = (float)zr * scale;
                    M.F32(s + zo, z);
                    float c = CosAt(yaw), sn = SinAt(yaw);
                    M.F32(s + xo, c * x - z * sn);
                    M.F32(s + zo, sn * x + z * c);
                }
            }
            M.F32(s + yo, y - M.F32(s + 0x3f0));
        }

        /// <summary>
        /// CEffectPrim::fn_b1dfe0 (0xb1dfe0) — per live slot: colour (r, g, b) = (int)((char)+0x45a / +0x45b / +0x45c · 2.57) (low
        /// bytes), a diamond of radius +0x468 (Diamond45) turned to the camera (FUN_00b0d9c0, +0x3ec / +0x4a0), drawn at each drop
        /// (drop 0 as drop + corner, the others corner + drop) when q1 = y + the first corner's y is ≤ 0: alpha 50, below −4
        /// (q1 + 4)·15 + 50, above −0.5 −100·q1, clamped at 0; fn_b0a900 texture 0, blend 0, relative, uv 0..1, mode 0. One face
        /// counter for the prim.
        /// </summary>
        public void Render_b1dfe0(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            Span<float> p = stackalloc float[12];
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                byte r = (byte)FxOps.Cvtt((float)(sbyte)M.U8(s + 0x45a) * 2.57f);         // [0xff75c4]
                byte g = (byte)FxOps.Cvtt((float)(sbyte)M.U8(s + 0x45b) * 2.57f);
                byte b = (byte)FxOps.Cvtt((float)(sbyte)M.U8(s + 0x45c) * 2.57f);
                Diamond45(p, M.F32(s + 0x468));
                Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                Drop_b1dfe0(rt, ref count, p, s, 0x484, 0x488, 0x48c, true, r, g, b, viewMatrix);
                Drop_b1dfe0(rt, ref count, p, s, 0x46c, 0x470, 0x474, false, r, g, b, viewMatrix);
                Drop_b1dfe0(rt, ref count, p, s, 0x490, 0x494, 0x498, false, r, g, b, viewMatrix);
                Drop_b1dfe0(rt, ref count, p, s, 0x478, 0x47c, 0x480, false, r, g, b, viewMatrix);
            }
        }

        void Drop_b1dfe0(FxRuntime rt, ref int count, Span<float> p, uint s, uint xo, uint yo, uint zo, bool dropFirst,
            byte r, byte g, byte b, uint viewMatrix)
        {
            float x = M.F32(s + xo), y = M.F32(s + yo), z = M.F32(s + zo);
            float q0, q1, q2, q3, q4, q5, q6, q7, q8, q9, q10, q11;
            if (dropFirst)
            {
                q0 = x + p[0]; q1 = y + p[1]; q2 = z + p[2];
                q3 = x + p[3]; q4 = y + p[4]; q5 = z + p[5];
                q6 = x + p[6]; q7 = y + p[7]; q8 = z + p[8];
                q9 = x + p[9]; q10 = y + p[10]; q11 = z + p[11];
            }
            else
            {
                q0 = p[0] + x; q1 = p[1] + y; q2 = p[2] + z;
                q3 = p[3] + x; q4 = p[4] + y; q5 = p[5] + z;
                q6 = p[6] + x; q7 = p[7] + y; q8 = p[8] + z;
                q9 = p[9] + x; q10 = p[10] + y; q11 = p[11] + z;
            }
            int a = 0x32;
            if (-4f > q1) a = FxOps.Cvtt((q1 + 4f) * 15f + 50f);                            // [0xff7598] [0xf4a764] [0xf7e280] [0xff7530]
            if (q1 > -0.5f) a = FxOps.Cvtt(q1 * -100f);                                     // [0xf80830] [0xff75b0]
            if (!(q1 <= 0f)) return;                                                        // COMISS 0, q1; JB (also NaN)
            Quad_b0a900(rt, ref count, (short)(a < 0 ? 0 : a), q0, q1, q2, q3, q4, q5, q6, q7, q8, q9, q10, q11,
                viewMatrix, 0, 0, 0, r, g, b, 0f, 1f, 0f, 1f, 0);
        }
    }
}
