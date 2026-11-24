// Synced from reference/port/src/RoClassicFx/Prim/prims-3/Prim.Prims3.InlineProcess.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// prims-3: CEffectPrim::OnProcess (0xad5a30) main-switch cases whose bodies are inlined in the
    /// dispatcher. Most of these types keep 4 "particles" of 0xb4 bytes starting at +0x3e8
    /// (+0 active flag, +2 alpha, +0xc counter, +8 radius, +0x14.. a 21-float profile, +0x68.. 21 flags).
    /// Offsets below are written relative to the particle base b = A + 0x3e8 + k*0xb4.
    /// </summary>
    public readonly partial struct Prim
    {
        static float P3Sin(int index) => FxMath.SinTableAt(index);
        static float P3Cos(int index) => FxMath.CosTableAt(index);

        /// <summary>COMISS a,b + JB: a &lt; b or unordered.</summary>
        static bool P3Jb(float a, float b) => !(a >= b);

        /// <summary>Master position → prim position (the idiom opening many cases).</summary>
        void P3FollowMaster()
        {
            uint m = m_master_ptr;
            M.U32(A + 0x10, M.U32(m + 0x10));
            M.U32(A + 0x14, M.U32(m + 0x14));
            M.U32(A + 0x18, M.U32(m + 0x18));
        }

        /// <summary>
        /// Shared inner loop of types 0x18 / 0x32 (0xad65e3, 0xad8300): profile[j] = r·sin(9j) ·
        /// (1 + 0.3·sin(phase)), phase = (cnt + k·90) for the first two particles, (cnt + k·45)·2 otherwise.
        /// </summary>
        void P3WaveProfile(int k, uint cntAddr, uint radiusAddr, uint outAddr)
        {
            for (int j = 0; j < 21; j++)
            {
                int cnt = M.I32(cntAddr);
                int idx = k * 45 < 0x5a ? cnt + k * 90 : (cnt + k * 45) * 2;
                idx %= 0x168;
                float tab = P3Sin(j * 9);
                float v1 = tab * M.F32(radiusAddr);
                M.F32(outAddr + (uint)j * 4, v1);
                float v = tab * M.F32(radiusAddr) * 0.3f * P3Sin(idx) + v1;
                M.F32(outAddr + (uint)j * 4, v);
            }
        }

        // ------------------------------------------------------------------ 0x18 (0xad64f9)
        [FxPrimProcess(0x18)]
        public FxCaseResult Case18(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                int cnt = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, cnt);
                short ang = (short)(M.I16(b + 0x10) + (k == 0 || k == 2 ? 1 : 2));
                M.I16(b + 0x10, ang);
                if (ang >= 0x168) M.I16(b + 0x10, (short)(ang - 0x168));
                if (cnt < m_duration - 0x14)
                {
                    if (cnt < 100)
                    {
                        short a = (short)(M.I16(b + 2) + 1);
                        M.I16(b + 2, a);
                        if (a > 0x50) M.I16(b + 2, 0x50);
                    }
                }
                else
                {
                    short a = (short)(M.I16(b + 2) - 5);
                    M.I16(b + 2, a);
                    if (a < 0) M.I16(b + 2, 0);
                }
                P3WaveProfile(k, b + 0xc, b + 8, b + 0x14);
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x1e (0xad7d36)
        [FxPrimProcess(0x1e)]
        public FxCaseResult Case1E(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                int cnt = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, cnt);
                if (cnt <= 0) continue;
                float s = M.F32(b + 0x80) + 0.07f;
                M.F32(b + 0x80, s);
                short w = (short)(M.I16(b + 0x7e) - 1);
                M.I16(b + 0x7e, w);
                if (w < 10)
                {
                    M.I16(b + 0x7e, 10);
                    M.I16(b + 2, 0);
                }
                if (P3Jb(s, 4.0f))
                {
                    M.I16(b + 2, (short)(M.I16(b + 2) + (M.I16(b + 4) == 0x16 ? 5 : 10)));
                }
                else
                {
                    short a = (short)(M.I16(b + 2) - 3);
                    M.I16(b + 2, a);
                    if (a <= 0)
                    {
                        M.I16(b + 2, 0);
                        if (cnt < m_duration - 0x1e)
                        {
                            M.U32(b + 0x80, 0x4057ae14); // 3.37f
                            M.I16(b + 0x7e, (short)(M.I16(b + 4) == 0x16 ? 0x40 : 0x4a));
                        }
                    }
                }
                for (int j = 0; j < 21; j++)
                {
                    int c = M.I32(b + 0xc);
                    int idx = k * 45 < 0x5a ? c + k * 90 : (c + k * 45) * 2;
                    idx %= 0x168;
                    float r = M.F32(b + 8);
                    uint o = b + 0x14 + (uint)j * 4;
                    M.F32(o, r);
                    float v = P3Sin(j * 9) * M.F32(b + 8) * 0.3f * P3Sin(idx) + r;
                    M.F32(o, v);
                }
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x25 (0xad8bb3)
        [FxPrimProcess(0x25)]
        public FxCaseResult Case25(FxRuntime rt)
        {
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                M.I32(b + 0xc, M.I32(b + 0xc) + 1);
                short a = M.I16(b + 2);
                if (M.I32(b + 0xc) < 10)
                {
                    M.I16(b + 2, (short)(a + 9));
                }
                else
                {
                    a = (short)(a - 1);
                    M.I16(b + 2, a);
                    if (a <= 0)
                    {
                        M.I16(b + 2, 0);
                        if (M.I32(b + 0xc) > 0x1e) M.U8(b, 0);
                    }
                }
                M.F32(b + 0x18, M.F32(b + 0x18) + 0.005f);
                M.F32(b + 0x14, M.F32(b + 0x14) + 0.005f);
                M.F32(b + 0x1c, P3Sin(M.I32(b + 0xc)) * M.F32(b + 8));
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x26 (0xad9ac3)
        [FxPrimProcess(0x26)]
        public FxCaseResult Case26(FxRuntime rt)
        {
            uint mode = rt.Mode; // FUN_00a03ba0
            P3FollowMaster();
            M.I32(A + 0x3f4, M.I32(A + 0x3f4) + 1);
            M.U32(A + 0x478, M.U32(A + 0x10));
            M.U32(A + 0x47c, M.U32(A + 0x14));
            M.U32(A + 0x480, M.U32(A + 0x18));
            M.F32(A + 0x47c, M.F32(A + 0x47c) - 8.0f);
            float kind = M.F32(A + 0x40c);
            int cnt = M.I32(A + 0x3f4);
            if (kind == 2.0f)
            {
                if (cnt <= 0x28)
                {
                    if (cnt < 0x14) M.I16(A + 0x3ea, (short)(M.I16(A + 0x3ea) + 6));
                }
                else
                {
                    short a = (short)(M.I16(A + 0x3ea) - 3);
                    M.I16(A + 0x3ea, a);
                    if (a < 0) M.I16(A + 0x3ea, 0);
                }
            }
            else if (kind == 3.0f)
            {
                int rem = cnt % 0x168;
                if (cnt <= 0x50) M.I16(A + 0x3ea, (short)(M.I16(A + 0x3ea) + 1));
                M.F32(A + 0x3f0, P3Sin(rem) * 0.005f + M.F32(A + 0x3f0));
            }
            else
            {
                short w = M.I16(A + 0x3ea);
                M.I16(A + 0x3ea, (short)(cnt % 10 < 5 ? w + 2 : w - 2));
            }

            float target = M.F32(A + 0x3fc);
            if (target > 0f)
            {
                uint world = M.U32(mode + 0xcc);
                float self = (float)(double)rt.PlayerAid;
                uint actor = target == self
                    ? M.U32(world + 0x2c)
                    : rt.FindActorByAid(FxOps.FloatToUInt_d48d60(target));
                if (actor != 0)
                {
                    M.U32(A + 0x490, M.U32(actor + 0x10));
                    M.U32(A + 0x494, M.U32(actor + 0x14));
                    M.U32(A + 0x498, M.U32(actor + 0x18));
                    M.F32(A + 0x494, M.F32(A + 0x494) - 8.0f);
                    return FxCaseResult.Continue;
                }
            }
            M.U8(A + 0x3e8, 0);
            M.I16(A + 0x3ea, 0);
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x27 (0xad9dca)
        [FxPrimProcess(0x27)]
        public FxCaseResult Case27(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 2; k++)
            {
                uint c = A + 0x3f8 + (uint)k * 0xb4;
                if (M.U8(c - 0x10) != 1) continue;
                float f = M.F32(c + 0x14);
                int step = 0;
                if (f == 4.0f) step = 6;
                else if (f == 10.0f || f == 11.0f || f == 12.0f) step = 3;
                if (step != 0)
                {
                    short v = (short)(M.I16(c) + step);
                    M.I16(c, v);
                    if (v >= 0x168) M.I16(c, (short)(v - 0x168));
                }
                if (f == 2.0f)
                {
                    M.I16(c + 0x6e, (short)(M.I16(c + 0x6e) + 1));
                }
                else if (f == 10.0f)
                {
                    float g = M.F32(c + 0x70);
                    g = 0xb4 < M.I16(c) ? g + 0.3f : g - 0.3f;
                    M.F32(c + 0x70, g);
                }
                else if (f != 11.0f && f != 12.0f)
                {
                    M.I16(c + 0x6e, (short)(M.I16(c + 0x6e) + 3));
                }
                short w = M.I16(c + 0x6e);
                if (w >= 0x168) M.I16(c + 0x6e, (short)(w - 0x168));
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x29 (0xad859c)
        [FxPrimProcess(0x29)]
        public FxCaseResult Case29(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3f8 + (uint)k * 0xb4;
                if (M.U8(c - 0x10) != 1) continue;
                int cnt = M.I32(c - 4) + 1;
                M.I32(c - 4, cnt);
                short a = (short)(M.I16(c) + 1);
                M.I16(c, a);
                if (a >= 0x168) M.I16(c, (short)(a - 0x168));
                if (M.I16(c - 0xc) == 1 && cnt > 0xfa)
                {
                    short al = (short)(M.I16(c - 0xe) - 1);
                    M.I16(c - 0xe, al);
                    if (al < 0) M.I16(c - 0xe, 0);
                }
                if (cnt >= 800)
                {
                    float f = M.F32(c - 8) - 1.0f;
                    M.F32(c - 8, f);
                    if (0f > f) M.F32(c - 8, 0f);
                }
                for (int j = 0; j < 21; j++)
                {
                    int cc = M.I32(c - 4);
                    float v;
                    if (cc < 200) v = 0f;
                    else if (cc <= 0x122) v = P3Sin(cc - 200) * M.F32(c - 8); // DAT_011a4708[cc]
                    else v = M.F32(c - 8);
                    uint o = c + 4 + (uint)j * 4;
                    M.F32(o, v);
                    if (0f > v) M.F32(o, 0f);
                }
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x2c (0xad9171)
        [FxPrimProcess(0x2c)]
        public FxCaseResult Case2C(FxRuntime rt)
        {
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                int cnt = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, cnt);
                if (cnt > 0) M.F32(b + 0x80, M.F32(b + 0x14) + M.F32(b + 0x80));
                if ((uint)(cnt - 11) <= 9)
                {
                    byte t = M.U8(b + 0x69);
                    ushort a = M.U16(b + 2);
                    if (t == 0x10 || t != 0x18) M.U16(b + 2, (ushort)(a + 0xf));
                    else M.U16(b + 2, (ushort)(a + (rt.MinEffect ? 8 : 0xf)));
                }
                else
                {
                    short a = (short)(M.I16(b + 2) - 5);
                    M.I16(b + 2, a);
                    if (a <= 0) M.I16(b + 2, 0);
                }
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x32 (0xad822b)
        [FxPrimProcess(0x32)]
        public FxCaseResult Case32(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3ec + (uint)k * 0xb4;
                if (M.U8(c - 4) != 1) continue;
                M.I32(c + 8, M.I32(c + 8) + 1);
                short a = (short)(M.I16(c) + 1);
                M.I16(c, a);
                if (a >= 0x168)
                {
                    a = (short)(a - 0x168);
                    M.I16(c, a);
                }
                M.F32(c + 0x7c, P3Sin(a) + 3.0f);
                short a2 = (short)(M.I16(c + 0xc) + 3);
                M.I16(c + 0xc, a2);
                if (a2 >= 0x168) M.I16(c + 0xc, (short)(a2 - 0x168));
                P3WaveProfile(k, c + 8, c + 4, c + 0x10);
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x38 (0xada5b1)
        [FxPrimProcess(0x38)]
        public FxCaseResult Case38(FxRuntime rt)
        {
            P3CameraAngles_3ec_4a0(rt);
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x408 + (uint)k * 0xb4;
                if (M.U8(c - 0x20) != 1) continue;
                M.I32(c - 0x14, M.I32(c - 0x14) + 1);
                if (M.I32(c - 0x14) <= 10)
                    M.I16(c - 0x1e, (short)(M.I16(c - 0x1e) + (M.I8(c + 0x48) < 10 ? 0xc : 0x14)));
                sbyte t = M.I8(c + 0x48);
                float f = M.F32(c);
                if (t >= 10)
                {
                    f += 0.05f;
                    M.F32(c, f);
                }
                f += M.F32(c - 8);
                M.F32(c - 8, f);
                if (!P3Jb(f, 200.0f)) M.F32(c - 8, f - 200.0f);
                if (M.I32(c - 0x14) > 0x14)
                {
                    short a = (short)(M.I16(c - 0x1e) - (t < 10 ? 4 : 2));
                    M.I16(c - 0x1e, a);
                    if (a < 0) M.I16(c - 0x1e, 0);
                }
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Camera-facing angles from the view (mode+0xd0 +0x2c/+0x30), inlined in types 0x38 / 0x44.</summary>
        void P3CameraAngles_3ec_4a0(FxRuntime rt)
        {
            uint view = M.U32(rt.Mode + 0xd0);
            M.I16(A + 0x3ec, (short)(0x168 - (short)FxOps.Cvtt(M.F32(view + 0x30))));
            short a = (short)(0x1c2 - (short)FxOps.Cvtt(M.F32(view + 0x2c)));
            M.I16(A + 0x4a0, a);
            if (a >= 0x168) M.I16(A + 0x4a0, (short)(a - 0x168));
        }

        // ------------------------------------------------------------------ 0x40 (0xad8d2e)
        [FxPrimProcess(0x40)]
        public FxCaseResult Case40(FxRuntime rt)
        {
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3ee + (uint)k * 0xb4;
                if (M.U8(c - 6) != 1) continue;
                M.I32(c + 6, M.I32(c + 6) + 1);
                if (M.I32(c + 6) <= 0) continue;
                short a = (short)(M.I16(c) + 1);
                M.I16(c, a);
                if (a >= 0x168) M.I16(c, (short)(a - 0x168));
                int ang = M.I16(c);
                int cnt = M.I32(c + 6);
                if (cnt > M.I16(c - 2))
                {
                    short al = (short)(M.I16(c - 4) - 2);
                    M.I16(c - 4, al);
                    if (al < 0) M.I16(c - 4, 0);
                }
                float r = M.F32(c + 2);
                float kind = M.F32(c + 0x1a);
                float v = r * P3Sin(ang) * 0.35f + r * 0.65f;
                M.F32(c + 0x16, v);
                if (cnt < 0x5a) M.F32(c + 0x16, v * P3Sin(cnt));
                if (kind == 4.0f)
                {
                    float st = M.F32(c + 0x26);
                    if (st == 0f)
                    {
                        float x = M.F32(c + 0x22) + 1.0f;
                        M.F32(c + 0x22, x);
                        if (x > 200.0f)
                        {
                            M.F32(c + 0x26, 1.0f);
                            M.F32(c + 0x22, 200.0f);
                        }
                    }
                    else if (st == 1.0f)
                    {
                        float x = M.F32(c + 0x22) - 1.0f;
                        M.F32(c + 0x22, x);
                        if (0f > x)
                        {
                            M.F32(c + 0x26, 0f);
                            M.F32(c + 0x22, 0f);
                        }
                    }
                }
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x4b (0xad8c3b)
        [FxPrimProcess(0x4b)]
        public FxCaseResult Case4B(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                M.I32(b + 0xc, M.I32(b + 0xc) + 1);
                float x = M.F32(b + 0x14) + 1.0f;
                M.F32(b + 0x14, x);
                if (x > 10.0f)
                {
                    short a = (short)(M.I16(b + 2) - 2);
                    M.I16(b + 2, a);
                    if (a < 0)
                    {
                        M.I16(b + 2, 0);
                        M.U8(b, 0);
                    }
                }
                bool grow;
                if (M.U8(b + 0x68) == 1)
                {
                    M.F32(b + 0x80, M.F32(b + 0x80) + 1.0f);
                    float s = M.F32(b + 8);
                    if (2.0f > s) M.F32(b + 8, s + 0.2f);
                    grow = M.I32(b + 0xc) <= 5;
                }
                else
                {
                    M.F32(b + 0x80, M.F32(b + 0x80) + 2.0f);
                    float s = M.F32(b + 8);
                    if (4.0f > s) M.F32(b + 8, s + 0.4f);
                    grow = M.I32(b + 0xc) <= 7;
                }
                if (grow) M.I16(b + 2, (short)(M.I16(b + 2) + 5));
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x4d (0xad750f)
        [FxPrimProcess(0x4d)]
        public FxCaseResult Case4D(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3f4 + (uint)k * 0xb4;
                if (M.U8(c - 0xc) != 1) continue;
                M.I32(c, M.I32(c) + 1);
                short a = (short)FxOps.Cvtt((float)M.I16(c + 4) + M.F32(c + 0xc));
                M.I16(c + 4, a);
                if (a >= 0x168) M.I16(c + 4, (short)(a - 0x168));
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x58 (0xad794a)
        [FxPrimProcess(0x58)]
        public FxCaseResult Case58(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3f4 + (uint)k * 0xb4;
                if (M.I8(c - 0xc) <= 0) continue;
                int cnt = M.I32(c) + 1;
                M.I32(c, cnt);
                if (cnt <= 0) continue;
                if (cnt <= 10)
                {
                    ushort a = M.U16(c - 0xa);
                    M.U16(c - 0xa, (ushort)(f_318 == 113.0f ? a + 6 : a + 2));
                }
                M.F32(c + 0x74, M.F32(c + 0x74) * 1.07f);
                if (cnt > 10)
                {
                    short a = (short)(M.I16(c - 0xa) - 1);
                    M.I16(c - 0xa, a);
                    if (a < 0) M.I16(c - 0xa, 0);
                }
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x65 (0xad6ce5)
        [FxPrimProcess(0x65)]
        public FxCaseResult Case65(FxRuntime rt)
        {
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                int cnt = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, cnt);
                if (cnt <= 0) continue;
                float x = M.F32(b + 0x80) + 0.4f;
                M.F32(b + 0x80, x);
                if (x > 15.0f) M.I16(b + 2, (short)(M.I16(b + 2) - 2));
                short a = M.I16(b + 2);
                if (cnt <= 0x19)
                {
                    int r = rt.Rand.Next() % 4;
                    M.I16(b + 2, (short)(M.I16(b + 2) + (short)(r + 5)));
                    a = M.I16(b + 2);
                    cnt = M.I32(b + 0xc);
                }
                if (a < 0) M.I16(b + 2, 0);
                if (cnt != 1) continue;
                for (int it = 0; it < 21; it++)
                {
                    float ang = M.F32(b + 0x14) + 9.0f;
                    M.F32(b + 0x14, ang);
                    if (ang > 180.0f)
                    {
                        M.F32(b + 0x14, 180.0f);
                        continue;
                    }
                    int n = FxOps.Cvtt(M.F32(b + 0x94));
                    for (int i = n; i >= 1; i--)
                    {
                        M.U32(b + 0x14 + (uint)i * 4, M.U32(b + 0x14 + (uint)(i - 1) * 4));
                        M.U8(b + 0x68 + (uint)i, M.U8(b + 0x68 + (uint)(i - 1)));
                    }
                }
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x6b (0xad67d2)
        [FxPrimProcess(0x6b)]
        public FxCaseResult Case6B(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3f4 + (uint)k * 0xb4;
                if (M.U8(c - 0xc) != 1) continue;
                int cnt = M.I32(c) + 1;
                M.I32(c, cnt);
                float x = M.F32(c + 0x74) + 0.1f;
                M.F32(c + 0x74, x);
                if (!P3Jb(x, 13.0f))
                {
                    M.F32(c + 0x74, 3.0f);
                    M.I16(c - 0xa, 0);
                    x = 3.0f;
                    if (cnt > 0x32) M.U8(c - 0xc, 0);
                }
                x -= 3.0f;
                M.F32(c - 4, x);
                if (x > 5.0f)
                {
                    M.F32(c - 4, 10.0f - x);
                    short a = M.I16(c - 0xa);
                    if (a > 0) M.I16(c - 0xa, (short)(a - 2));
                }
                else
                {
                    M.I16(c - 0xa, (short)(M.I16(c - 0xa) + 2));
                }
                float y = M.F32(c - 4);
                M.F32(c - 4, y + y);
                M.I16(c + 0x72, (short)FxOps.Cvtt(90.0f - x * 5.0f));
                for (int j = 0; j < 21; j++)
                {
                    if (M.U8(c + 0x5c + (uint)j) != 0) continue;
                    float v = M.F32(c - 4);
                    uint o = A + 0x3fc + (uint)(k * 0x2d + j) * 4;
                    M.F32(o, v);
                    if (0f > v) M.F32(o, 0f);
                }
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x71 (0xada7fb)
        [FxPrimProcess(0x71)]
        public FxCaseResult Case71(FxRuntime rt)
        {
            P3FollowMaster();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3f8 + (uint)k * 0xb4;
                if (M.U8(c - 0x10) != 1) continue;
                int cnt = M.I32(c - 4) + 1;
                M.I32(c - 4, cnt);
                short a = (short)(M.I16(c) + 4);
                M.I16(c, a);
                if (a >= 0x168) M.I16(c, (short)(a - 0x168));
                if (cnt <= 0x1e)
                {
                    short al = (short)(M.I16(c - 0xe) + 8);
                    M.I16(c - 0xe, al);
                    if (al > 0xa0) M.I16(c - 0xe, 0xa0);
                }
                M.F32(c + 0x70, M.F32(c + 0x70) - 0.2f);
                for (int j = 0; j < 21; j++)
                {
                    uint flag = c + 0x58 + (uint)j;
                    if (M.U8(flag) != 0) continue;
                    float tab = P3Sin(j * 9);
                    uint o = A + 0x3fc + (uint)(k * 0x2d + j) * 4;
                    int cc = M.I32(c - 4);
                    if (cc <= 0x5a) M.F32(o, tab * M.F32(c - 8) * P3Sin(cc));
                    float lim = tab * M.F32(c - 8);
                    if (M.F32(o) > lim)
                    {
                        M.F32(o, lim);
                        M.U8(flag, 1);
                    }
                    if (0f > M.F32(o)) M.F32(o, 0f);
                }
            }
            return FxCaseResult.Continue;
        }
    }
}
