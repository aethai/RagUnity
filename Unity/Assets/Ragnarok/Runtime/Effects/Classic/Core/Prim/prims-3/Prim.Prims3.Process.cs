// Synced from reference/port/src/RoClassicFx/Prim/prims-3/Prim.Prims3.Process.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>prims-3: OnProcess cases that call a CEffectPrim process function.</summary>
    public readonly partial struct Prim
    {
        [FxPrimProcess(0x0, 0x2)]
        public FxCaseResult Case00(FxRuntime rt) => Process_adba70(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        [FxPrimProcess(0xd)]
        public FxCaseResult Case0D(FxRuntime rt)
        {
            Process_adda70();
            return FxCaseResult.Continue;
        }

        [FxPrimProcess(0x1f)]
        public FxCaseResult Case1F(FxRuntime rt)
        {
            Process_ae4e80();
            return FxCaseResult.Continue;
        }

        [FxPrimProcess(0x3c)]
        public FxCaseResult Case3C(FxRuntime rt)
        {
            Process_ae60e0(rt);
            return FxCaseResult.Continue;
        }

        [FxPrimProcess(0x44)]
        public FxCaseResult Case44(FxRuntime rt)
        {
            Process_ae1420(rt);
            return FxCaseResult.Continue;
        }

        [FxPrimProcess(0x5a)]
        public FxCaseResult Case5A(FxRuntime rt)
        {
            Process_ae8740();
            return FxCaseResult.Continue;
        }

        [FxPrimProcess(0x6c)]
        public FxCaseResult Case6C(FxRuntime rt)
        {
            Process_ae47b0();
            return FxCaseResult.Continue;
        }

        [FxPrimProcess(0x76)]
        public FxCaseResult Case76(FxRuntime rt)
        {
            Process_ae11f0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// Tail shared by fn_adba70 / fn_adda70: texture frame advance every f_1e8 ticks; returns false
        /// (AL = 0) when a non-looping animation (f_254 == 0) reaches f_204 frames.
        /// </summary>
        bool P3AdvanceFrame()
        {
            if (f_1e0 % f_1e8 != 0) return true;
            int frames = f_204;
            int f = M.I32(A + 0x3c) + 1;
            M.I32(A + 0x3c, f);
            if (M.U8(A + 0x254) == 0) return f != frames;
            M.I32(A + 0x3c, f % frames);
            return true;
        }

        /// <summary>
        /// CEffectPrim::fn_adba70 (0xadba70) — process for types 0 / 2 (2D screen sprite): velocity/size
        /// keyframes, heading-driven drift (f_30c degrees, speed f_2bc), RGB ramps, random heading wobble
        /// (flag 8), alpha (fn_aed050) and frame advance. Type 0 with f_3e8 == 'b' is the skill-cooldown
        /// clock (remaining seconds into f_3f4 via a client shim).
        /// </summary>
        public bool Process_adba70(FxRuntime rt)
        {
            if (M.U8(A + 0x3e8) == 0x62)
            {
                M.I32(A + 0x3f4, 999);
                int? secs = rt.SkillCooldownSecondsImpl_adba70(M.I16(A + 0x3f8));
                if (secs.HasValue) M.I32(A + 0x3f4, secs.Value);
            }
            if (f_200 == f_1e0)
            {
                const float unset = 0.0001f;
                if (f_2c8 != unset) f_358 = f_2c8;
                if (f_2cc != unset) f_364 = f_2cc;
                if (f_29c != unset) f_2bc = f_29c;
                if (f_2c4 != unset) f_35c = f_2c4;
                if (f_2d0 != unset) f_368 = f_2d0;
                if (f_2a0 != unset) f_2c0 = f_2a0;
            }
            uint flags = (uint)f_1dc;
            if ((flags & 4) != 0)
            {
                M.U32(A + 0x10, M.U32(A + 0x394));
                m_pos_y = f_398;
            }
            else
            {
                RagEffect master = m_master;
                m_pos_x = master.f_154 + f_394;
                m_pos_y = master.f_158 + f_398;
            }
            f_2bc = f_2c0 + f_2bc;
            float a338 = f_344 + f_338;
            float a358 = f_358 + f_35c;
            float a364 = f_368 + f_364;
            f_338 = a338;
            f_358 = a358;
            f_364 = a364;
            if ((flags & 2) != 0 && f_1e0 >= f_1d4)
            {
                f_35c = 0f; a358 = 0f; f_358 = 0f;
                f_368 = 0f; a364 = 0f; f_364 = 0f;
            }
            if ((sbyte)flags < 0)
            {
                float step = f_328, alpha = f_324;
                bool within = !(0f > step) ? P3Jb(alpha, f_2d4) : P3Jb(f_2d8, alpha);
                if (!within) f_328 = step * -1.0f;
            }
            f_314 = a338 + f_314;
            f_354 = a358 + f_354;
            f_360 = a364 + f_360;
            float s = (float)((double)FxMath.GetSin(FxOps.Cvtt(f_30c)) * f_2bc);   // x87 fmul, fstp float
            f_388 = s + f_388;
            float c = (float)((double)FxMath.GetCos(FxOps.Cvtt(f_30c)) * f_2bc);
            M.I32(A + 0x23c, M.I32(A + 0x23c) + f_248);
            int g = f_24c + f_240;
            int bl = f_244 + f_250;
            m_pos_x = f_388 + m_pos_x;
            f_240 = g;
            f_244 = bl;
            float y = f_38c - c;
            f_38c = y;
            m_pos_y = y + m_pos_y;
            int r = M.I32(A + 0x23c);
            if (r > 0xff) M.I32(A + 0x23c, 0);
            else if (r < 0) M.I32(A + 0x23c, 0xff);
            if (g > 0xff) f_240 = 0xff;
            else if (g < 0) f_240 = 0;
            if (bl > 0xff) f_244 = 0xff;
            else if (bl < 0) f_244 = 0;
            if ((f_1dc & 8) != 0 && f_1e0 % f_1d4 == 0)
            {
                int r1 = rt.Rand.Next() % 16;
                f_30c = (float)r1 + (f_30c - 8.0f);
                int r2 = rt.Rand.Next() % 10;
                f_1d4 = r2 + 10;
            }
            UpdateAlpha_aed050();
            return P3AdvanceFrame();
        }

        /// <summary>
        /// CEffectPrim::fn_adda70 (0xadda70) — process for type 0xd: orbit around master+offset with
        /// X/Y rotations (f_310, f_30c) of the prim matrix at +0x160, radius/height/width ramps, alpha, frames.
        /// </summary>
        public bool Process_adda70()
        {
            RagEffect master = m_master;
            f_384 = master.m_pos_z + f_39c;
            f_380 = master.m_pos_y + f_398;
            f_37c = master.m_pos_x + f_394;
            f_2bc = f_2c0 + f_2bc;
            float a334 = f_340 + f_334;
            f_334 = a334;
            f_364 = f_368 + f_364;
            float a310 = a334 + f_310;
            f_300 = f_308 + f_300;
            f_310 = a310;
            f_2fc = f_304 + f_2fc;
            float a330 = f_33c + f_330;
            f_330 = a330;
            f_30c = a330 + f_30c;
            FxMatrix.MakeRotationX(M, A + 0x160, FxMath.DegToRad(FxOps.Cvtt(a310)));
            FxMatrix.RotateY(M, A + 0x160, FxMath.DegToRad(FxOps.Cvtt(f_30c)));
            float v = -f_2bc;
            float x = v * f_16c + f_160 * 0f;
            x += f_178 * 0f;
            x += f_184;
            f_370 = x;
            float y = v * f_170 + f_164 * 0f;
            y += f_17c * 0f;
            y += f_188;
            f_374 = y;
            float z = v * f_174 + f_168 * 0f;
            z += f_180 * 0f;
            z += f_18c;
            f_378 = z;
            y += f_38c;
            x += f_388;
            z += f_390;
            f_38c = y;
            f_388 = x;
            f_390 = z;
            f_30c = f_330 + f_30c;
            m_pos_x = x + f_37c;
            m_pos_y = y + f_380;
            m_pos_z = z + f_384;
            f_2f4 = f_2f4 + f_2fc;
            float a2f8 = f_2f8 + f_300;
            float h = f_364 + f_360;
            f_2f8 = a2f8;
            f_360 = h;
            if (h > f_36c) f_360 = f_36c;
            if ((f_1dc & 1) != 0 && f_1e0 == f_1f0)
                f_300 = -(a2f8 / (float)((m_duration - f_1e0) + 1));
            UpdateAlpha_aed050();
            return P3AdvanceFrame();
        }

        /// <summary>
        /// CEffectPrim::fn_ae11f0 (0xae11f0) — process for type 0x76: four rotating rings with a random angle
        /// step, alpha in/out and a 21-point profile growing with sin(age) and clamping (flags at +0x450).
        /// </summary>
        public void Process_ae11f0(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                M.I32(b + 0xc, M.I32(b + 0xc) + 1);
                int r = rt.Rand.Next();
                short ang = k == 3
                    ? (short)(r % 2 + 2 + M.I16(A + 0x614))
                    : (short)(r % 3 + 4 + M.I16(b + 0x10));
                M.I16(b + 0x10, ang);
                if (ang >= 0x168) M.I16(b + 0x10, (short)(ang - 0x168));
                int cnt = M.I32(b + 0xc);
                if (cnt >= 0x28)
                {
                    short a = (short)(M.I16(b + 2) - 0xf);
                    M.I16(b + 2, a);
                    if (a < 0) M.I16(b + 2, 0);
                }
                else if (cnt < 0x14)
                {
                    short a = (short)(M.I16(b + 2) + 5);
                    M.I16(b + 2, a);
                    if (a > 0x50) M.I16(b + 2, 0x50);
                }
                M.F32(b + 8, M.F32(b + 8) * (k == 3 ? 1.025f : 1.005f));
                for (int j = 0; j < 21; j++)
                {
                    uint o = b + 0x14 + (uint)j * 4;
                    uint flag = b + 0x68 + (uint)j;
                    float tab = P3Sin(j * 9);
                    int cc = M.I32(b + 0xc);
                    float v;
                    if (cc <= 0x5a)
                    {
                        v = tab * M.F32(b + 8) * P3Sin(cc);
                        M.F32(o, v);
                    }
                    else
                    {
                        M.U8(flag, 1);
                        v = M.F32(o);
                    }
                    if (v > tab * M.F32(b + 8))
                    {
                        M.U8(flag, 1);
                        v = M.F32(o);
                    }
                    if (0f > v) M.F32(o, 0f);
                }
            }
        }

        /// <summary>
        /// CEffectPrim::fn_ae1420 (0xae1420) — process for type 0x44: camera-facing angles, then per particle a
        /// fan of 9/10 radii (+0x400..) growing to fixed caps and shrinking after 20 ticks, scale at +0x468.
        /// </summary>
        public void Process_ae1420(FxRuntime rt)
        {
            P3CameraAngles_3ec_4a0(rt);
            if (M.U8(A + 0x454) == 1) P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x404 + (uint)k * 0xb4;
                if (M.U8(c - 0x1c) != 1) continue;
                int cnt = M.I32(c - 0x10) + 1;
                M.I32(c - 0x10, cnt);
                if (cnt <= 0) continue;
                uint fan = c - 4;
                if (M.U8(c + 0x50) == 2)
                {
                    if (cnt < 0x14)
                    {
                        P3GrowFan(fan, FanCapsNear);
                    }
                    else
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            float v = M.F32(fan + (uint)i * 4) - 2.0f;
                            M.F32(fan + (uint)i * 4, v);
                            if (0f > v) M.F32(fan + (uint)i * 4, 0f);
                        }
                        if (M.I32(c - 0x10) > 100)
                        {
                            P3ShrinkTail(c + 0x1c, c + 0x64, 1.005f);
                            P3ShrinkTail(c + 0x20, c + 0x64, 1.005f);
                        }
                    }
                }
                else
                {
                    if (cnt < 0x14)
                    {
                        P3GrowFan(fan, FanCapsFar);
                        cnt = M.I32(c - 0x10);
                    }
                    else
                    {
                        for (int i = 0; i < 9; i++)
                        {
                            float v = M.F32(fan + (uint)i * 4) - 5.0f;
                            M.F32(fan + (uint)i * 4, v);
                            if (0f > v) M.F32(fan + (uint)i * 4, 0f);
                        }
                        int limit = M.U8(c + 0x50) == 4 ? 0x19 : 0x8c;
                        cnt = M.I32(c - 0x10);
                        if (cnt > limit) P3ShrinkTail(c + 0x20, c + 0x64, 1.01f);
                    }
                    if (cnt < 0x32 && M.U8(c + 0x50) != 4)
                        M.F32(c + 0x64, M.F32(c + 0x64) - 0.1f);
                }
            }
        }

        /// <summary>fn_ae1420: +13 per tick to ten consecutive floats, each clamped (COMISS/JB) to its cap.</summary>
        // the fan caps of fn_ae1420's two kinds (a params array was allocated on every call: ~24 a frame with 300 effects)
        static readonly float[] FanCapsNear = { 33f, 56f, 79f, 102f, 125f, 148f, 171f, 194f, 217f, 240f };
        static readonly float[] FanCapsFar = { 70f, 90f, 110f, 130f, 150f, 170f, 190f, 210f, 230f, 250f };

        void P3GrowFan(uint fan, float[] caps)
        {
            for (int i = 0; i < caps.Length; i++)
            {
                uint a = fan + (uint)i * 4;
                float v = M.F32(a) + 13.0f;
                M.F32(a, v);
                if (!P3Jb(v, caps[i])) M.F32(a, caps[i]);
            }
        }

        /// <summary>fn_ae1420: radius -= 5, scale *= factor while positive, clamp at 0.</summary>
        void P3ShrinkTail(uint radius, uint scale, float factor)
        {
            float v = M.F32(radius) - 5.0f;
            M.F32(radius, v);
            if (v > 0f) M.F32(scale, M.F32(scale) * factor);
            if (0f > v) M.F32(radius, 0f);
        }

        /// <summary>
        /// CEffectPrim::fn_ae47b0 (0xae47b0) — process for type 0x6c: breathing radius
        /// (2.5·sin((age % 90)·4) + base), alpha in/out, clamped 21-point profile.
        /// </summary>
        public bool Process_ae47b0()
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3f8 + (uint)k * 0xb4;
                if (M.U8(c - 0x10) != 1) continue;
                int cnt = M.I32(c - 4) + 1;
                M.I32(c - 4, cnt);
                short a = M.I16(c);
                if (a >= 0x168) M.I16(c, (short)(a - 0x168));
                M.F32(c - 8, P3Sin((cnt % 0x5a) * 4) * 2.5f + M.F32(c + 0x84));
                short w = M.I16(c + 0x6e);
                if (w >= 0x168)
                {
                    w = (short)(w - 0x168);
                    M.I16(c + 0x6e, w);
                }
                if (w < 0) M.I16(c + 0x6e, (short)(w + 0x168));
                if (cnt > 0x6e)
                {
                    short al = (short)(M.I16(c - 0xe) - 1);
                    M.I16(c - 0xe, al);
                    if (al < 0) M.I16(c - 0xe, 0);
                }
                else if (cnt < 0x14)
                {
                    short al = (short)(M.I16(c - 0xe) + 5);
                    M.I16(c - 0xe, al);
                    if (al > 0xb4) M.I16(c - 0xe, 0xb4);
                }
                for (int j = 0; j < 21; j++)
                {
                    uint flag = c + 0x58 + (uint)j;
                    if (M.U8(flag) != 0) continue;
                    uint o = A + 0x3fc + (uint)(k * 0x2d + j) * 4;
                    float tab = P3Sin(j * 9);
                    float v = M.F32(c - 8) * tab;
                    int cc = M.I32(c - 4);
                    if (cc <= 0x5a) v *= P3Sin(cc);
                    M.F32(o, v);
                    float lim = M.F32(c - 8) * tab;
                    if (v > lim)
                    {
                        M.F32(o, lim);
                        M.U8(flag, 1);
                    }
                    if (0f > M.F32(o)) M.F32(o, 0f);
                }
            }
            return true;
        }

        /// <summary>
        /// CEffectPrim::fn_ae4e80 (0xae4e80) — process for type 0x1f: rotating wave rings; f_2c0 == 99 selects
        /// the slow variant (step 3, growth 0.1, width floor 30 instead of 5 / 0.3 / 40).
        /// </summary>
        public void Process_ae4e80()
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                int cnt = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, cnt);
                short ang = (short)(M.I16(b + 0x10) + (f_2c0 == 99.0f ? 3 : 5));
                M.I16(b + 0x10, ang);
                if (ang >= 0x168) M.I16(b + 0x10, (short)(ang - 0x168));
                float grow = f_2c0 == 99.0f ? 0.1f : 0.3f;
                short w = (short)(M.I16(b + 0x7e) - 1);
                M.I16(b + 0x7e, w);
                M.F32(b + 0x80, M.F32(b + 0x80) + grow);
                if (f_2c0 == 99.0f)
                {
                    if (w < 0x1e) M.I16(b + 0x7e, 0x1e);
                }
                else if (w < 0x28)
                {
                    M.I16(b + 0x7e, 0x28);
                }
                short type = M.I16(b + 4);
                ushort alpha = M.U16(b + 2);
                if (cnt > 10)
                {
                    int d = type == 2 ? -8 : type == 3 ? -5 : type == 4 ? -5 : type == 5 ? -4 : -2;
                    short na = (short)(alpha + d);
                    M.I16(b + 2, na);
                    if (na <= 0) M.I16(b + 2, 0);
                }
                else
                {
                    M.U16(b + 2, (ushort)(alpha + (type == 1 ? 10 : 20)));
                }
                for (int j = 0; j < 21; j++)
                {
                    int idx = (M.I32(b + 0xc) + k * 0x5a) % 0x168;
                    float tab = P3Sin(j * 9);
                    uint o = b + 0x14 + (uint)j * 4;
                    float v1 = tab * M.F32(b + 8);
                    M.F32(o, v1);
                    M.F32(o, tab * M.F32(b + 8) * 0.15f * P3Sin(idx) + v1);
                }
            }
        }

        /// <summary>
        /// CEffectPrim::fn_ae60e0 (0xae60e0) — process for type 0x3c: per-particle kind byte (+0x450) selects
        /// the angle speed and the profile shape (0/'!': wave, 1/3: sin(age) swell, 2: fast swell then 0).
        /// </summary>
        public bool Process_ae60e0(FxRuntime rt)
        {
            uint mode = rt.Mode;
            if (M.U8(A + 0x450) == 0x21)
                M.I16(A + 0x554, (short)(0x168 - (short)FxOps.Cvtt(M.F32(M.U32(mode + 0xd0) + 0x30))));
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                M.I32(b + 0xc, M.I32(b + 0xc) + 1);
                uint kinds = b + 0x68;
                byte kind = M.U8(kinds);
                int add = kind == 1 ? k + 8 : kind == 2 ? (k < 2 ? 4 : 2) + k : kind == 3 ? k + 6 : 0;
                if (add != 0 || kind == 1 || kind == 2 || kind == 3)
                    M.U16(b + 0x10, (ushort)(M.U16(b + 0x10) + add));
                short ang = M.I16(b + 0x10);
                if (ang >= 0x168) M.I16(b + 0x10, (short)(ang - 0x168));
                short lim = M.I16(b + 4);
                if (M.I32(b + 0xc) >= lim && lim >= 0)
                {
                    short a = (short)(M.I16(b + 2) - 2);
                    M.I16(b + 2, a);
                    if (a < 0) M.I16(b + 2, 0);
                }
                else if (M.I32(b + 0xc) < 0x10)
                {
                    byte kk = M.U8(kinds);
                    short a;
                    if (kk == 2)
                    {
                        a = (short)(M.I16(b + 2) + 3);
                        M.I16(b + 2, a);
                    }
                    else
                    {
                        a = (short)(M.I16(b + 2) + (kk == 3 ? 2 : 5));
                        M.I16(b + 2, a);
                    }
                    if (a > 0xb4) M.I16(b + 2, 0xb4);
                }
                for (int j = 0; j < 21; j++)
                {
                    byte cj = M.U8(kinds + (uint)j);
                    uint o = A + 0x3fc + (uint)(k * 0x2d + j) * 4;
                    if (cj == 0 || cj == 0x21)
                    {
                        int idx = k == 0 ? j * 0x22 + M.I32(A + 0x3f4) * 4
                                : k == 1 ? j * 0x22 + M.I32(A + 0x4a8) * 3
                                : (M.I32(b + 0xc) + j * 0x11) * 2;
                        idx %= 0x168;
                        float r = M.F32(b + 8);
                        float v = r * 0.25f * P3Sin(idx) + r * 0.75f;
                        M.F32(o, v);
                        int cc = M.I32(b + 0xc);
                        if (cc <= 0x5a)
                        {
                            v *= P3Sin(cc);
                            M.F32(o, v);
                        }
                        if (0f > v) M.F32(o, 0f);
                    }
                    else if (cj == 1 || cj == 3)
                    {
                        float r = M.F32(b + 8);
                        M.F32(o, r);
                        int cc = M.I32(b + 0xc);
                        if (cc <= 0x5a) M.F32(o, r * P3Sin(cc));
                    }
                    else if (cj == 2)
                    {
                        float r = M.F32(b + 8);
                        M.F32(o, r);
                        int cc = M.I32(b + 0xc);
                        if (cc <= 0x2d) M.F32(o, r * P3Sin(cc * 4));
                        else M.F32(o, 0f);
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// CEffectPrim::fn_ae8740 (0xae8740) — process for type 0x5a: two pairs of particles whose radius
        /// (+0x404) eases in with sin(age) and eases out after a (possibly movement-triggered) end tick;
        /// the second pair also scrolls a texture offset (+0x5d0). Ends by following the master.
        /// </summary>
        public void Process_ae8740()
        {
            for (int k = 0; k < 2; k++) P3EaseParticle_ae8740(A + 0x3f4 + (uint)k * 0xb4, false);
            for (int k = 0; k < 2; k++) P3EaseParticle_ae8740(A + 0x55c + (uint)k * 0xb4, true);
            P3FollowMaster();
        }

        void P3EaseParticle_ae8740(uint c, bool scroll)
        {
            if (M.U8(c - 0xc) != 1) return;
            M.I32(c, M.I32(c) + 1);
            int cnt = M.I32(c);
            if (cnt > 0x7148)
            {
                cnt = 0x7148;
                M.I32(c, 0x7148);
            }
            else if (cnt <= 0)
            {
                return;
            }
            if (scroll)
            {
                float u = M.F32(c + 0x74) + 0.5f;
                M.F32(c + 0x74, u);
                float wrap = M.F32(c + 8) * 8.0f;
                if (u > wrap) M.F32(c + 0x74, u - wrap);
            }
            float kind = M.F32(c + 0x18);
            if (kind >= 20.0f && (uint)cnt > 200)
            {
                RagEffect master = m_master;
                bool moved = !(m_pos_x == master.m_pos_x) || !(m_pos_z == master.m_pos_z);
                if (moved && M.I16(c - 8) > 2000) M.I16(c - 8, (short)cnt);
            }
            int end = M.I16(c - 8);
            if (cnt > end)
            {
                int e;
                if (kind >= 20.0f) e = ((end - cnt) + 0x12) * 5;
                else if (kind >= 10.0f) e = (end - cnt) + 0x5a;
                else e = kind == 0f ? 0x9c9 - cnt : 0x9ab - cnt;
                if (e > 0x5a) e = 0x5a;
                if (e < 0) e = 0;
                M.F32(c + 0x10, P3Sin(e) * M.F32(c - 4));
            }
            else if ((uint)cnt < 0x5a)
            {
                M.F32(c + 0x10, P3Sin(cnt) * M.F32(c - 4));
            }
            else
            {
                M.U32(c + 0x10, M.U32(c - 4));
            }
        }
    }
}
