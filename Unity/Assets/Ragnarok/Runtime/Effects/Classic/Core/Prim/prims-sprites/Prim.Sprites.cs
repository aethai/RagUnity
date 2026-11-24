// Synced from reference/port/src/RoClassicFx/Prim/prims-sprites/Prim.Sprites.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Sprite (SPR/ACT) prims: types 5, 6, 7, 8, 0x2e, 0x33, 0x7c — OnProcess cases and the shared
    /// sprite renderer fn_af24e0 (a trail of up to f_1f4 copies of the current ACT motion).
    /// </summary>
    public readonly partial struct Prim
    {
        // ---- dispatch -------------------------------------------------------------------

        [FxPrimProcess(5)]
        public FxCaseResult Case05(FxRuntime rt) => Process_ade180(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        [FxPrimProcess(6)]
        public FxCaseResult Case06(FxRuntime rt) => Process_ade800(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        [FxPrimProcess(7)]
        public FxCaseResult Case07(FxRuntime rt) => Process_adf110(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        [FxPrimProcess(8)]
        public FxCaseResult Case08(FxRuntime rt) { Process_adf3d0(rt); return FxCaseResult.Continue; }

        [FxPrimProcess(0x7c)]
        public FxCaseResult Case7C(FxRuntime rt) { Process_ae6770(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x2e (inline case at 0xada02a): follow master, or the f_484 anchor when f_3e8 == 1.</summary>
        [FxPrimProcess(0x2e)]
        public FxCaseResult Case2E(FxRuntime rt)
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            if (f_3e8 == 1)
            {
                M.U32(A + 0x388, M.U32(A + 0x484));
                M.U32(A + 0x38c, M.U32(A + 0x488));
                M.U32(A + 0x390, M.U32(A + 0x48c));
            }
            M.U32(A + 0x10, M.U32(A + 0x388));
            M.U32(A + 0x14, M.U32(A + 0x38c));
            M.U32(A + 0x18, M.U32(A + 0x390));
            SpriteTail_adb354(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x33 (inline case at 0xadb2f8): follow master; f_3e8 == 1 counts f_3f4 and picks master or anchor.</summary>
        [FxPrimProcess(0x33)]
        public FxCaseResult Case33(FxRuntime rt)
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            if (f_3e8 == 1)
            {
                f_3f4 = f_3f4 + 1;
                if (f_454 == 1)
                {
                    M.U32(A + 0x10, M.U32(master + 0x10));
                    M.U32(A + 0x14, M.U32(master + 0x14));
                    M.U32(A + 0x18, M.U32(master + 0x18));
                }
                else
                {
                    M.U32(A + 0x10, M.U32(A + 0x484));
                    M.U32(A + 0x14, M.U32(A + 0x488));
                    M.U32(A + 0x18, M.U32(A + 0x48c));
                }
            }
            SpriteTail_adb354(rt);
            return FxCaseResult.Continue;
        }

        [FxPrimRender(5, 6, 7, 8, 0x2e, 0x33, 0x7c)]
        public void RenderSprite(FxRuntime rt, uint viewMatrix) => Render_af24e0(rt, viewMatrix);

        /// <summary>Shared tail at 0xadb354: camera-facing angle (flag 0x20), alpha, trail snapshot, frame advance (result ignored).</summary>
        void SpriteTail_adb354(FxRuntime rt)
        {
            FaceCamera(rt);
            UpdateAlpha_aed050();
            SnapshotTrail_b0d680();
            AdvanceFrame_ad5600(rt);
        }

        /// <summary>Flag 0x20: f_314 = f_4c − view angle (CView +0x30).</summary>
        void FaceCamera(FxRuntime rt)
        {
            if ((f_1dc & 0x20) != 0)
                f_314 = f_4c - M.F32(M.U32(rt.Mode + 0xd0) + 0x30);
        }

        // ---- process functions -------------------------------------------------------------

        /// <summary>
        /// CEffectPrim::fn_ade180 (0xade180) — type 5: matrix-driven drift with optional random wander
        /// (flags 2/4/8), master-distance kill (flag 0x100), then alpha/trail/frame.
        /// </summary>
        public bool Process_ade180(FxRuntime rt)
        {
            RagEffect master = m_master;
            float mx = master.m_pos_x, my = master.m_pos_y, mz = master.m_pos_z;
            f_37c = mx + f_394;
            f_380 = my + f_398;
            f_384 = mz + f_39c;
            int age = f_1e0;
            if (age == f_200)
            {
                M.U32(A + 0x370, M.U32(A + 0x2a4));
                M.U32(A + 0x374, M.U32(A + 0x2a8));
                M.U32(A + 0x378, M.U32(A + 0x2ac));
                M.U32(A + 0x3ac, M.U32(A + 0x2b0));
                M.U32(A + 0x3b0, M.U32(A + 0x2b4));
                M.U32(A + 0x3b4, M.U32(A + 0x2b8));
            }
            float v = f_2c0 + f_2bc;
            float ds = f_320 + f_31c;
            f_2bc = v;
            f_31c = ds;
            int flags = f_1dc;
            if ((flags & 0x400) != 0)
            {
                float z = f_3b4 + f_378;
                float x = f_3ac + f_370;
                float y = f_3b0 + f_374;
                f_378 = z;
                f_370 = x;
                f_374 = y;
                f_390 = z + f_390;
                f_388 = x + f_388;
                f_38c = y + f_38c;
                f_314 = f_338 + f_314;
                f_318 = ds + f_318;
            }
            else if ((flags & 1) != 0 || age > f_1ec)
            {
                float size = ds + f_318;
                float dx = f_16c * 0.0f + f_160 * 0.0f + v * f_178 + f_184;
                f_370 = dx;
                float dy = f_170 * 0.0f + f_164 * 0.0f + v * f_17c + f_188;
                f_374 = dy;
                float dz = f_174 * 0.0f + f_168 * 0.0f + v * f_180 + f_18c;
                f_388 = dx + f_388;
                f_38c = dy + f_38c;
                f_318 = size;
                f_378 = dz;
                f_390 = dz + f_390;
                f_314 = f_338 + f_314;
            }

            flags = f_1dc;
            if ((flags & 2) != 0)
            {
                if (f_1e0 % f_1d4 == 0)
                {
                    f_1d4 = NonNeg(rt.Rand.Next() % 8) + 0xc;
                    f_3a0 = (float)(NonNeg(rt.Rand.Next() % 5) + 5) / 100f;
                    f_3a8 = (float)(NonNeg(rt.Rand.Next() % 5) + 5) / 100f;
                    if (NonNeg(rt.Rand.Next() % 2) == 0) f_3a0 = f_3a0 * -1f;
                    if (NonNeg(rt.Rand.Next() % 2) == 0) f_3a8 = f_3a8 * -1f;
                }
                flags = f_1dc;
                f_388 = f_3a0 + f_388;
            }
            if ((flags & 4) != 0 && f_1e0 % f_1d4 == 0)
            {
                f_2bc = (float)(NonNeg(rt.Rand.Next() % 0x23) + 0xf) / 100f;
                f_30c = (float)NonNeg(rt.Rand.Next() % 0x50) + (f_30c - 40f);
                f_310 = (float)NonNeg(rt.Rand.Next() % 0x50) + (f_310 - 40f);
                FxMatrix.MakeRotationY(M, A + 0x160, FxMath.DegToRad(FxOps.Cvtt(f_30c)));
                FxMatrix.RotateX(M, A + 0x160, FxMath.DegToRad(FxOps.Cvtt(f_310)));
                f_1d4 = NonNeg(rt.Rand.Next() % 10) + 5;
                flags = f_1dc;
            }
            if ((flags & 8) != 0 && f_1e0 % f_1d4 == 0)
            {
                f_30c = (float)NonNeg(rt.Rand.Next() % 10) + (f_30c - 5f);
                f_310 = (float)NonNeg(rt.Rand.Next() % 10) + (f_310 - 5f);
                FxMatrix.MakeRotationX(M, A + 0x160, FxMath.DegToRad(FxOps.Cvtt(f_310)));
                FxMatrix.RotateY(M, A + 0x160, FxMath.DegToRad(FxOps.Cvtt(f_30c)));
                f_1d4 = NonNeg(rt.Rand.Next() % 0xf) + 5;
            }
            m_pos_x = f_37c + f_388;
            m_pos_y = f_380 + f_38c;
            m_pos_z = f_384 + f_390;
            if ((f_1dc & 0x100) != 0)
            {
                float dist = Distance_bce640(master.f_180, master.f_188);
                if (3.0f >= dist) return false;
            }
            FaceCamera(rt);
            UpdateAlpha_aed050();
            SnapshotTrail_b0d680();
            return AdvanceFrame_ad5600(rt);
        }

        static int NonNeg(int r) => r < 0 ? 0 : r;

        /// <summary>FUN_00bce640 (thiscall on the render object) — distance on the XZ plane to (x, z).</summary>
        float Distance_bce640(float x, float z)
        {
            float dz = z - m_pos_z;
            float dx = x - m_pos_x;
            return (float)Math.Sqrt(dz * dz + dx * dx);
        }

        /// <summary>
        /// CEffectPrim::fn_ade800 (0xade800) — type 6: a bouncing sprite (coins). Falls under gravity f_2f0,
        /// bounces off the ground (GAT height) with ×(−1/1.5) and plays EF_Coin1..3.wav when f_208 == 1.
        /// </summary>
        public bool Process_ade800(FxRuntime rt)
        {
            RagEffect master = m_master;
            float mx = master.m_pos_x, my = master.m_pos_y;
            f_384 = master.m_pos_z + f_39c;
            f_380 = my + f_398;
            f_37c = mx + f_394;
            f_2ec = f_2f0 + f_2ec;
            float v = f_2c0 + f_2bc;
            float speed = MaxSs(0f, v);
            f_2bc = speed;
            float dx = f_16c * 0.0f + f_160 * 0.0f + speed * f_178 + f_184;
            f_370 = dx;
            dx = dx + f_388;
            f_374 = f_170 * 0.0f + f_164 * 0.0f + speed * f_17c + f_188;
            f_388 = dx;
            f_378 = f_174 * 0.0f + f_168 * 0.0f + speed * f_180 + f_18c;
            float y = f_38c + f_2ec;
            float z = f_378 + f_390;
            f_38c = y;
            f_390 = z;
            dx = dx + f_37c;
            y = y + f_380;
            m_pos_z = z + f_384;
            m_pos_x = dx;
            m_pos_y = y;
            f_318 = f_31c + f_318;
            if (f_2ec > 0f)
            {
                uint attr = M.U32(M.U32(rt.Mode + 0xcc) + 0x30);
                float ground = rt.GatGetHeight_6c32a0(attr, dx, m_pos_z);
                if (y > ground)
                {
                    f_2ec = f_2ec / -1.5f;
                    if (f_208 == 1)
                    {
                        int r = NonNeg(rt.Rand.Next() % 3);
                        string wav = "effect\\EF_Coin" + (r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".wav";
                        rt.PlaySound3DRelative(wav, m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                    }
                }
            }
            UpdateAlpha_aed050();
            SnapshotTrail_b0d680();
            return AdvanceFrame_ad5600(rt);
        }

        /// <summary>MAXSS a, b = a &gt; b ? a : b (b when unordered).</summary>
        static float MaxSs(float a, float b) => a > b ? a : b;

        /// <summary>
        /// CEffectPrim::fn_adf110 (0xadf110) — type 7: orbiting sprite (Y rotation by f_30c, radius f_348),
        /// rising with f_2ec, keyframed radius speed (f_158), Z roll f_310.
        /// </summary>
        public bool Process_adf110(FxRuntime rt)
        {
            uint master = m_master_ptr;
            float ang = f_33c + f_330;
            float mx = M.F32(master + 0x10);
            f_37c = mx;
            M.U32(A + 0x380, M.U32(master + 0x14));
            M.U32(A + 0x384, M.U32(master + 0x18));
            float rise = f_2ec + f_2f0;
            float rspeed = f_34c + f_350;
            f_330 = ang;
            ang = ang + f_30c;
            f_2ec = rise;
            rise = rise + f_398;
            f_34c = rspeed;
            rspeed = rspeed + f_348;
            f_30c = ang;
            f_398 = rise;
            f_348 = rspeed;
            f_380 = rise + f_380;
            f_37c = mx + f_394;
            f_384 = f_39c + f_384;
            f_314 = f_338 + f_314;
            int age = f_1e0;
            if (age == f_200)
            {
                M.U32(A + 0x2ec, M.U32(A + 0x29c));
                M.U32(A + 0x2f0, M.U32(A + 0x2a0));
            }
            if (f_158 != 0)
            {
                uint k = (uint)f_1d8;
                uint keys = (uint)f_258;
                if ((int)k < 0 || k >= (uint)((f_25c - f_258) >> 2))
                {
                    f_1d8 = 0;
                    k = 0;
                    keys = (uint)f_258;
                }
                if ((float)age == M.F32(keys + k * 4))
                {
                    M.U32(A + 0x34c, M.U32((uint)f_264 + k * 4));
                    M.U32(A + 0x350, M.U32((uint)f_270 + k * 4));
                    f_1d8 = (int)(k + 1);
                }
            }
            uint mat = A + 0x160;
            FxMatrix.MakeRotationY(M, mat, FxMath.DegToRad(FxOps.Cvtt(f_30c)));
            float r = f_348;
            float x = f_16c * 0.0f + f_160 * 0.0f + r * f_178 + f_184;
            f_388 = x;
            float y = f_170 * 0.0f + f_164 * 0.0f + r * f_17c + f_188;
            f_38c = y;
            float z = f_174 * 0.0f + f_168 * 0.0f + r * f_180 + f_18c;
            f_390 = z;
            float size = f_31c + f_318;
            f_318 = size;
            float roll = f_334 + f_310;
            m_pos_x = x + f_37c;
            m_pos_y = y + f_380;
            m_pos_z = z + f_384;
            f_310 = roll;
            FxMatrix.RotateZ(M, mat, roll);
            UpdateAlpha_aed050();
            SnapshotTrail_b0d680();
            return AdvanceFrame_ad5600(rt);
        }

        /// <summary>
        /// CEffectPrim::fn_adf3d0 (0xadf3d0) — type 8: matrix-driven drift plus a spin about the axis at
        /// +0x290 (angle f_314 degrees) of the offset (0, f_310, 0).
        /// </summary>
        public bool Process_adf3d0(FxRuntime rt)
        {
            uint master = m_master_ptr;
            f_380 = M.F32(master + 0x14) + f_398;
            f_384 = M.F32(master + 0x18) + f_39c;
            f_37c = f_394 + M.F32(master + 0x10);
            int spinDeg = FxOps.Cvtt(f_314);
            float spinSpeed = f_334;
            f_334 = spinSpeed + f_340;
            f_310 = spinSpeed + f_310;
            float v = f_2c0 + f_2bc;
            float g = f_2f0 + f_2ec;
            f_2bc = v;
            f_2ec = g;
            v = v - g;
            f_370 = f_16c * 0.0f + f_160 * 0.0f + v * f_178 + f_184;
            f_374 = f_170 * 0.0f + f_164 * 0.0f + v * f_17c + f_188;
            f_378 = f_174 * 0.0f + f_168 * 0.0f + v * f_180 + f_18c;
            uint mat = A + 0x190;
            AxisRotation_532f80(mat, A + 0x290, FxMath.DegToRad(spinDeg));
            float d = f_310;
            float ox = d * f_19c + f_190 * 0.0f + f_1a8 * 0.0f + f_1b4;
            f_3a0 = ox;
            float oy = d * f_1a0 + f_194 * 0.0f + f_1ac * 0.0f + f_1b8;
            f_3a4 = oy;
            float oz = d * f_1a4 + f_198 * 0.0f + f_1b0 * 0.0f + f_1bc;
            f_318 = f_31c + f_318;
            f_3a8 = oz;
            float px = f_388 - f_370;
            f_388 = px;
            float py = f_38c - f_374;
            f_38c = py;
            float pz = f_390 - f_378;
            f_390 = pz;
            m_pos_x = px + f_37c + ox;
            m_pos_y = py + f_380 + oy;
            m_pos_z = pz + f_384 + oz;
            UpdateAlpha_aed050();
            SnapshotTrail_b0d680();
            return AdvanceFrame_ad5600(rt);
        }

        /// <summary>
        /// FUN_00532f80 (thiscall on a 4x3 matrix) — rotation of <paramref name="rad"/> about the axis at
        /// <paramref name="axis"/> (quaternion form, not normalised); translation row cleared.
        /// </summary>
        void AxisRotation_532f80(uint mat, uint axis, float rad)
        {
            float h = rad * 0.5f;
            float s = (float)Math.Sin(h);
            float X = s * M.F32(axis), Y = s * M.F32(axis + 4), Z = s * M.F32(axis + 8);
            float c = (float)Math.Cos(h);
            float Z2 = Z + Z, X2 = X + X, Y2 = Y + Y;
            M.U32(mat + 0x24, 0); M.U32(mat + 0x28, 0); M.U32(mat + 0x2c, 0);
            float cZ2 = c * Z2, cX2 = c * X2, XX2 = X2 * X, cY2 = c * Y2, YY2 = Y2 * Y;
            float XY2 = Y2 * X, ZZ2 = Z2 * Z, XZ2 = Z2 * X, YZ2 = Z2 * Y;
            M.F32(mat, 1f - (ZZ2 + YY2));
            M.F32(mat + 0x4, XY2 + cZ2);
            M.F32(mat + 0xc, XY2 - cZ2);
            M.F32(mat + 0x8, XZ2 - cY2);
            M.F32(mat + 0x18, XZ2 + cY2);
            M.F32(mat + 0x10, 1f - (ZZ2 + XX2));
            M.F32(mat + 0x14, YZ2 + cX2);
            M.F32(mat + 0x1c, YZ2 - cX2);
            M.F32(mat + 0x20, 1f - (YY2 + XX2));
        }

        /// <summary>
        /// CEffectPrim::fn_ae6770 (0xae6770) — type 0x7c: when f_3e8 == 1 two random-walk angles (f_3fc/f_400,
        /// f_404/f_408) steer an anchor (f_484..) that sinks by f_3f0 while alpha drops by 3 per tick.
        /// </summary>
        public bool Process_ae6770(FxRuntime rt)
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            if (f_3e8 == 1)
            {
                for (uint slot = A + 0x3fc, n = 0; n < 2; n++, slot += 8)
                {
                    float target = M.F32(slot + 4);
                    float cur = M.F32(slot);
                    int value;
                    if (target > cur)
                    {
                        cur = cur + 3.0f;
                        if (cur > 359.0f) cur = 359.0f;
                        M.F32(slot, cur);
                        if (!(cur > target)) continue;
                        int lim = FxOps.Cvtt(cur);
                        value = lim == 0 ? 0 : NonNeg(rt.Rand.Next() % lim);
                    }
                    else
                    {
                        cur = cur - 2.0f;
                        if (0f > cur) cur = 0f;
                        M.F32(slot, cur);
                        if (!(target >= cur)) continue;
                        int lim = FxOps.Cvtt(cur);
                        value = 0x167 - (lim == 0 ? 0 : NonNeg(rt.Rand.Next() % lim));
                    }
                    M.F32(slot + 4, (float)value);
                }
                float sink = f_488 - f_3f0;
                f_488 = sink;
                float a = f_324 - 3.0f;
                f_324 = a;
                if (3.0f > a) f_324 = 0f;
                float ax = FxMath.SinTableAt(FxOps.Cvtt(f_3fc)) * 0.15f + f_484;
                f_484 = ax;
                f_48c = FxMath.SinTableAt(FxOps.Cvtt(f_404)) * 0.15f + f_48c;
                f_388 = ax;
                f_38c = sink;
                M.U32(A + 0x390, M.U32(A + 0x48c));
            }
            M.U32(A + 0x10, M.U32(A + 0x388));
            M.U32(A + 0x14, M.U32(A + 0x38c));
            M.U32(A + 0x18, M.U32(A + 0x390));
            FaceCamera(rt);
            UpdateAlpha_aed050();
            SnapshotTrail_b0d680();
            return AdvanceFrame_ad5600(rt);
        }

        // ---- shared helpers ------------------------------------------------------------------

        /// <summary>
        /// CEffectPrim::fn_ad5600 (0xad5600) — ACT frame advance every f_1e8 ticks: f_3c++ and at the end of
        /// the action either die (default), hold the last frame (flag 0x800) or loop (f_254 != 0).
        /// </summary>
        public bool AdvanceFrame_ad5600(FxRuntime rt)
        {
            uint act = rt.GetActRes(A);
            if (f_1e0 % f_1e8 != 0) return true;
            f_3c = f_3c + 1;
            uint action = (uint)f_38;
            if (f_254 == 0)
            {
                if ((f_1dc & 0x800) != 0)
                {
                    int n = rt.ActMotionCount(act, action);
                    if (f_3c == n) { f_3c = f_3c - 1; return true; }
                }
                else
                {
                    int n = rt.ActMotionCount(act, action);
                    if (f_3c == n) return false;
                }
            }
            else
            {
                int n = rt.ActMotionCount(act, action);
                f_3c = f_3c % n;
            }
            return true;
        }

        /// <summary>
        /// CEffectPrim::fn_b0d680 (0xb0d680, "SnapshotTrail") — shifts the 0x8c-byte trail entries at +0x6b8 back
        /// by one (entry i: size and alpha fade with i) and records the current state in entry 0.
        /// Entry: +0 pos, +0xc 12 floats (f_3b8..), +0x3c f_348, +0x40 size, +0x44 f_30c, +0x48 matrix, +0x84 alpha, +0x88 ARGB.
        /// </summary>
        public void SnapshotTrail_b0d680()
        {
            int i = f_1f4 - 1;
            if (i > 0)
            {
                for (uint e = A + 0x6b8 + (uint)i * 0x8c; i > 0; i--, e -= 0x8c)
                {
                    uint prev = e - 0x8c;
                    for (uint o = 0; o <= 0x3c; o += 4) M.U32(e + o, M.U32(prev + o));
                    for (uint o = 0x44; o <= 0x74; o += 4) M.U32(e + o, M.U32(prev + o));
                    float fi = (float)i;
                    float alpha = f_324;
                    alpha = alpha - alpha / (float)f_1f4 * fi;
                    M.F32(e + 0x84, alpha);
                    M.U32(e + 0x88, Argb(FxOps.Cvtt(alpha)));
                    float size = f_318;
                    M.F32(e + 0x40, size - size / (float)f_1f4 * fi * 0.5f);
                }
            }
            uint t = A + 0x6b8;
            M.U32(t, M.U32(A + 0x10));
            M.U32(t + 4, M.U32(A + 0x14));
            M.U32(t + 8, M.U32(A + 0x18));
            for (uint o = 0; o < 0x30; o += 4) M.U32(t + 0xc + o, M.U32(A + 0x3b8 + o));
            M.U32(t + 0x3c, M.U32(A + 0x348));
            M.U32(t + 0x40, M.U32(A + 0x318));
            M.U32(t + 0x44, M.U32(A + 0x30c));
            for (uint o = 0; o < 0x30; o += 4) M.U32(t + 0x48 + o, M.U32(A + 0x160 + o));
            float a0 = f_324;
            M.F32(t + 0x84, a0);
            M.U32(t + 0x88, Argb(FxOps.Cvtt(a0)));
        }

        uint Argb(int alpha) => (((uint)alpha * 0x100 + M.U8(A + 0x23c)) * 0x100 + M.U8(A + 0x240)) * 0x100 + M.U8(A + 0x244);

        /// <summary>CEffectPrim::fn_b0dc70 (0xb0dc70) — type 5 sprites of an actor-owned effect get the actor's scale.</summary>
        void SpriteScale_b0dc70(FxRuntime rt, ref float scale)
        {
            uint master = m_master_ptr;
            if (master == 0) return;
            uint owner = M.U32(master + 0x138);
            if (owner == 0 || m_type != 5) return;
            rt.ActorScale_bd0b10(owner, ref scale);
        }

        /// <summary>
        /// CEffectPrim::fn_af24e0 (0xaf24e0) — sprite renderer: for each trail entry, project its position, then
        /// submit every clip of ACT motion (f_38, f_3c) as a screen-space sprite (AddSprite 0xbd7930),
        /// scaled by the entry size × clip zoom × screen scale, culled against the viewport.
        /// </summary>
        public void Render_af24e0(FxRuntime rt, uint viewMatrix)
        {
            uint spr = rt.GetSprRes(A);
            uint act = rt.GetActRes(A);
            FxRenderer r = rt.Renderer;
            uint m = viewMatrix;
            for (int i = 0; i < f_1f4; i++)
            {
                uint e = A + 0x6b8 + (uint)i * 0x8c;
                float ex = M.F32(e), ey = M.F32(e + 4), ez = M.F32(e + 8);
                float depth = ey * M.F32(m + 0x14) + M.F32(m + 0x8) * ex;
                depth += M.F32(m + 0x20) * ez;
                depth += M.F32(m + 0x2c);
                r.ProjectVertex2(M, m, ex, ey, ez, out float sx, out float sy, out float rhw);
                float scale = r.ScreenScale(rhw);
                SpriteScale_b0dc70(rt, ref scale);
                uint motion = rt.ActGetMotion(act, (uint)f_38, (uint)f_3c);
                for (int j = 0; j < M.I32(motion + 0x2c); j++)
                {
                    uint clip = rt.MotionGetClip(motion, (uint)j);
                    int sprIndex = M.I32(clip + 8);
                    if (sprIndex == -1) continue;
                    uint images = M.U32(spr + (uint)((M.I32(clip + 0x20) + 0x6c) * 0xc));
                    uint img = M.U32(images + (uint)sprIndex * 4);
                    double size = (double)M.F32(e + 0x40);
                    double zoomY = (double)M.F32(clip + 0x18);
                    double sc = (double)scale;
                    double dsy = (double)sy;
                    int cy = M.I32(clip + 4);
                    float top = (float)((double)cy * size * zoomY * sc + dsy);
                    int cy2 = (M.I16(img + 6) + 1) * M.I16(img + 2) + cy;
                    float bottom = (float)((double)cy2 * size * zoomY * sc + dsy);
                    if (top >= (float)r.ScreenHeight) continue;
                    if (0f >= bottom) continue;
                    int cx = M.I32(clip);
                    int cx2 = (M.I16(img + 4) + 1) * M.I16(img) + cx;
                    double zoomX = (double)M.F32(clip + 0x14);
                    double dsx = (double)sx;
                    float left, right;
                    if ((M.U8(clip + 0xc) & 1) != 0)
                    {
                        left = (float)((double)cx2 * size * zoomX * sc + dsx);
                        right = (float)((double)cx * size * zoomX * sc + dsx);
                        if (right >= (float)r.ScreenWidth) continue;
                        if (0f >= left) continue;
                    }
                    else
                    {
                        left = (float)((double)cx * size * zoomX * sc + dsx);
                        right = (float)((double)cx2 * size * zoomX * sc + dsx);
                        if (left >= (float)r.ScreenWidth) continue;
                        if (0f >= right) continue;
                    }
                    float db = (bottom - sy) * 0.16f + depth;
                    float dt = (top - sy) * 0.16f + depth;
                    uint argb = (f_1dc & 0x200) != 0
                        ? ((((((uint)M.U8(clip + 0x13) << 8) + M.U8(clip + 0x10)) << 8) + M.U8(clip + 0x11)) << 8) + M.U8(clip + 0x12)
                        : M.U32(e + 0x88);
                    var draw = new FxSpriteDraw
                    {
                        Object = A,
                        Top = top, Left = left, Bottom = bottom, Right = right,
                        RhwBottom = 1.0f / db + 0.0001f,
                        RhwTop = 1.0f / dt + 0.0001f,
                        Argb = argb,
                        Angle = (float)M.I32(clip + 0x1c) + f_314,
                        Image = img,
                        Palette = spr + 0x110,
                        Flags = f_1c4,
                        OffsetX = 0f, OffsetY = 0f, DepthBias = 0,
                    };
                    r.AddSprite(draw);
                }
            }
        }
    }
}
