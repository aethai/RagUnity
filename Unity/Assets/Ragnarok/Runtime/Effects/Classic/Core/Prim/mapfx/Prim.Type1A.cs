// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type1A.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim types 0x1a / 0x1b / 0x1c — the map magic circles (CRagEffect::fn_b81d20 / fn_b81f80 / fn_b82320): 0x1a a flat circle turning
    /// one degree a tick, 0x1c two counter-phased glows (+0x466 / +0x51a, 3° a tick), both drawn by fn_aee0b0 (prims-3, the ground quad
    /// of 0x27 / 0x58); 0x1b rising glitter orbiting at radius 20 (fn_ae7020 / fn_b042d0).
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x1a (0xad78c1, inline): position = the master's; +0x40c == 14 → angle +0x3f8 − 1 (below 0: + 360)
        /// else + 1 (from 360: − 360), 16-bit; then the texture-frame tail 0xad787c (<see cref="FrameTail_ad787c"/>); break.
        /// </summary>
        [FxPrimProcess(0x1a)]
        public FxCaseResult Case1A(FxRuntime rt)
        {
            FollowMaster_P1();
            short a = M.I16(A + 0x3f8);
            if (M.F32(A + 0x40c) == 14f)                                                    // UCOMISS [0xf7cd50]; LAHF/TEST 0x44/JP
            {
                short n = (short)(a - 1);
                M.I16(A + 0x3f8, n);
                if (n < 0) M.I16(A + 0x3f8, (short)(n + 0x168));
            }
            else
            {
                short n = (short)(a + 1);
                M.I16(A + 0x3f8, n);
                if (n >= 0x168) M.I16(A + 0x3f8, (short)(n - 0x168));
            }
            FrameTail_ad787c();
            return FxCaseResult.Continue;
        }

        /// <summary>CEffectPrim::OnProcess case 0x1b (0xad7a0a): <c>fn_ae7020(this); break;</c>.</summary>
        [FxPrimProcess(0x1b)]
        public FxCaseResult Case1B(FxRuntime rt)
        {
            Process_ae7020(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CEffectPrim::OnProcess case 0x1c (0xad781f, inline): position = the master's; +0x466 and +0x51a each + 3, from 360 − 360
        /// (16-bit); then the texture-frame tail 0xad787c; break.
        /// </summary>
        [FxPrimProcess(0x1c)]
        public FxCaseResult Case1C(FxRuntime rt)
        {
            FollowMaster_P1();
            Step3_1c(A + 0x466);
            Step3_1c(A + 0x51a);
            FrameTail_ad787c();
            return FxCaseResult.Continue;
        }

        void Step3_1c(uint a)
        {
            short n = (short)(M.I16(a) + 3);
            M.I16(a, n);
            if (n >= 0x168) M.I16(a, (short)(n - 0x168));
        }

        /// <summary>OnProcess tail 0xad787c: every f_1e8 ticks f_3c + 1, wrapped at f_204 when the byte +0x254 is set (integer IDIV).</summary>
        void FrameTail_ad787c()
        {
            if (f_1e0 % f_1e8 != 0) return;
            int f = M.I32(A + 0x3c) + 1;
            M.I32(A + 0x3c, f);
            if (M.U8(A + 0x254) != 0) M.I32(A + 0x3c, f % f_204);
        }

        /// <summary>CEffectPrim::Render cases 0x1a / 0x1c (shared with 0x27 / 0x43 / 0x58 / 0xa1): fn_aee0b0(this, viewMatrix).</summary>
        [FxPrimRender(0x1a, 0x1c)]
        public void Render1A(FxRuntime rt, uint viewMatrix) => Render_aee0b0(rt, viewMatrix);

        /// <summary>CEffectPrim::Render case 0x1b: fn_b042d0(this, viewMatrix).</summary>
        [FxPrimRender(0x1b)]
        public void Render1B(FxRuntime rt, uint viewMatrix) => Render_b042d0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_ae7020 (0xae7020) — camera angles, position = the master's; per live slot: the two wander pairs
        /// (+0x3fc..+0x408, ±1, <see cref="ChaseAngles_aec0c0"/>), the four orbit angles +0x40c..+0x418 − 1 (below 0: + 360), glint k at
        /// (sin·20, y, cos·20) of its orbit angle (raw tables); glints below the plane (y &lt; 0) also wobble by 2.5·sin(wander angle
        /// +0x3fc / +0x404) with signs (+, +), (−, −), (+, −), (−, +). y never changes. Returns true (unused by the case).
        /// </summary>
        public bool Process_ae7020(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                ChaseAngles_aec0c0(rt, s + 0x3fc, 2);
                float a0 = Orbit_ae7020(s + 0x40c), a1 = Orbit_ae7020(s + 0x410), a2 = Orbit_ae7020(s + 0x414), a3 = Orbit_ae7020(s + 0x418);
                float x0 = SinAt(FxOps.Cvtt(a0)) * 20f; M.F32(s + 0x484, x0);              // [0xf7d134]
                float z0 = CosAt(FxOps.Cvtt(a0)) * 20f; M.F32(s + 0x48c, z0);
                float x1 = SinAt(FxOps.Cvtt(a1)) * 20f; M.F32(s + 0x46c, x1);
                float z1 = CosAt(FxOps.Cvtt(a1)) * 20f; M.F32(s + 0x474, z1);
                float x2 = SinAt(FxOps.Cvtt(a2)) * 20f; M.F32(s + 0x490, x2);
                float z2 = CosAt(FxOps.Cvtt(a2)) * 20f; M.F32(s + 0x498, z2);
                float x3 = SinAt(FxOps.Cvtt(a3)) * 20f; M.F32(s + 0x478, x3);
                float z3 = CosAt(FxOps.Cvtt(a3)) * 20f; M.F32(s + 0x480, z3);
                if (0f > M.F32(s + 0x488))
                {
                    M.F32(s + 0x484, Wobble_ae7020(s + 0x3fc) + x0);
                    M.F32(s + 0x48c, Wobble_ae7020(s + 0x404) + z0);
                }
                if (0f > M.F32(s + 0x470))
                {
                    M.F32(s + 0x46c, x1 - Wobble_ae7020(s + 0x3fc));
                    M.F32(s + 0x474, z1 - Wobble_ae7020(s + 0x404));
                }
                if (0f > M.F32(s + 0x494))
                {
                    M.F32(s + 0x490, Wobble_ae7020(s + 0x3fc) + x2);
                    M.F32(s + 0x498, z2 - Wobble_ae7020(s + 0x404));
                }
                if (0f > M.F32(s + 0x47c))
                {
                    M.F32(s + 0x478, x3 - Wobble_ae7020(s + 0x3fc));
                    M.F32(s + 0x480, Wobble_ae7020(s + 0x404) + z3);
                }
            }
            return true;
        }

        /// <summary>angle − 1, stored; below 0 + 360, stored again; returns the new angle.</summary>
        float Orbit_ae7020(uint a)
        {
            float v = M.F32(a) - 1f;                                                        // [0xf4a468]
            M.F32(a, v);
            if (0f > v)
            {
                v = v + 360f;                                                               // [0xf56528]
                M.F32(a, v);
            }
            return v;
        }

        /// <summary>sin[(int)angle] · 2.5 (raw table).</summary>
        float Wobble_ae7020(uint angle) => SinAt(FxOps.Cvtt(M.F32(angle))) * 2.5f;        // [0xf4a8c8]

        /// <summary>
        /// CEffectPrim::fn_b042d0 (0xb042d0) — per live slot a diamond of radius +0x468 (Diamond45) turned to the camera (FUN_00b0d9c0,
        /// +0x3ec / +0x4a0), drawn at each glint when q1 = y + the first corner's
        /// y is ≤ 0: alpha 150 (colour +0x451 == 2) or 130, below −1 (q1 + 1)·4 + 130, clamped at 0; colour (155, 255, 155) (+0x451 ==
        /// 2) else white; fn_b0a900 texture 0, blend 0, relative, uv 0..1, mode 0.
        /// </summary>
        public void Render_b042d0(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            Span<float> p = stackalloc float[12];
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                Diamond45(p, M.F32(s + 0x468));
                Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                Glint_b042d0(rt, ref count, p, s, 0x484, 0x488, 0x48c, viewMatrix);
                Glint_b042d0(rt, ref count, p, s, 0x46c, 0x470, 0x474, viewMatrix);
                Glint_b042d0(rt, ref count, p, s, 0x490, 0x494, 0x498, viewMatrix);
                Glint_b042d0(rt, ref count, p, s, 0x478, 0x47c, 0x480, viewMatrix);
            }
        }

        void Glint_b042d0(FxRuntime rt, ref int count, Span<float> p, uint s, uint xo, uint yo, uint zo, uint viewMatrix)
        {
            float x = M.F32(s + xo), y = M.F32(s + yo), z = M.F32(s + zo);
            // (the original adds some coordinates as corner + glint, some as glint + corner: equal bit for bit unless both are NaN)
            float q0 = x + p[0], q1 = y + p[1], q2 = z + p[2];
            float q3 = x + p[3], q4 = y + p[4], q5 = z + p[5];
            float q6 = x + p[6], q7 = y + p[7], q8 = z + p[8];
            float q9 = x + p[9], q10 = y + p[10], q11 = z + p[11];
            bool green = M.U8(s + 0x451) == 2;
            int a = green ? 0x96 : 0x82;
            if (-1f > q1) a = FxOps.Cvtt((q1 + 1f) * 4f + 130f);                            // [0xf4f14c] [0xf4a468] [0xf4a764] [0xff7560]
            if (!(q1 <= 0f)) return;                                                        // COMISS 0, q1; JB (also NaN)
            byte rb = green ? (byte)0x9b : (byte)0xff;
            Quad_b0a900(rt, ref count, (short)(a < 0 ? 0 : a), q0, q1, q2, q3, q4, q5, q6, q7, q8, q9, q10, q11,
                viewMatrix, 0, 0, 0, rb, 0xff, rb, 0f, 1f, 0f, 1f, 0);
        }
    }
}
