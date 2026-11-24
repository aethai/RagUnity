// Synced from reference/port/src/RoClassicFx/Prim/effects-9/Prim.Effects9.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-9 prim types: 0x52 (textured column/ring, Sma), 0x7b (falling item icons, Twilight Alchemy),
    /// 0x7e (wind blades, Kamaitachi), 0x95 (bubble ring, Bard/Dancer performance casts).
    /// </summary>
    public readonly partial struct Prim
    {
        // ================================================================ type 0x52

        [FxPrimProcess(0x52)]
        public FxCaseResult Case52_E9(FxRuntime rt) { Process_ae3e70(rt); return FxCaseResult.Continue; }

        [FxPrimRender(0x52)]
        public void Render52_E9(FxRuntime rt, uint viewMatrix) => Render_afcca0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_ae3e70 (0xae3e70) — camera angles, follow the master; slot 0 only: age +0x3f4, alpha
        /// +0x3ea by the style byte +0x454 (fade in/out, fixed levels), style 2 grows the size +0x468 after tick 40,
        /// styles 10..19 spin +0x3ee every other tick, alpha clamped to 0..255, +0x41c rotates by 2 (styles &lt; 30),
        /// position = master position with +0x470 = y - +0x438.
        /// </summary>
        public void Process_ae3e70(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            if (M.U8(A + 0x3e8) != 1) return;
            int cnt = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, cnt);
            if (cnt < 300)
            {
                sbyte c = M.I8(A + 0x454);
                if (c == 1 || c == 3)
                {
                    ushort a = M.U16(A + 0x3ea);
                    if (cnt < 200)
                    {
                        a = (ushort)(a + 10);
                        M.U16(A + 0x3ea, a);
                        if ((short)a >= 0xdc) M.I16(A + 0x3ea, 0xdc);
                    }
                    else
                    {
                        a = (ushort)(a - 5);
                        M.U16(A + 0x3ea, a);
                        if ((short)a < 0) M.I16(A + 0x3ea, 0);
                    }
                }
                else if (c == 0x16)
                {
                    M.I16(A + 0x3ea, (short)(M.I16(A + 0x3ea) + 10));
                    if (M.I16(A + 0x3ea) >= 0xdc) M.I16(A + 0x3ea, 0xdc);
                }
                else if (c == 0) M.I16(A + 0x3ea, (short)(M.I16(A + 0x3ea) + 1));
                else if (c == 0x14)
                {
                    M.I16(A + 0x3ea, (short)(M.I16(A + 0x3ea) + 5));
                    if (M.I16(A + 0x3ea) >= 200) M.I16(A + 0x3ea, 200);
                }
                else if ((byte)(c - 0x10) <= 2) M.I16(A + 0x3ea, 100);
                else if ((byte)(c - 0xa) <= 2) M.I16(A + 0x3ea, 0x96);
                else if (c == 0xe) M.I16(A + 0x3ea, 0x32);
                else if (c >= 0xa) M.I16(A + 0x3ea, 0xfa);
            }

            sbyte k = M.I8(A + 0x454);
            int path;   // 0: fade-out tail, 1: spin up, 2: spin down, 3: wrap only (k == 0x12), 4: nothing
            if (k == 2)
            {
                if (cnt > 0x28)
                {
                    short d = (short)(cnt >= 0x3e ? -8 : 8);
                    short a = (short)(d + M.I16(A + 0x3ea));
                    M.I16(A + 0x3ea, a);
                    if (a > 0) f_468 = f_468 + 0.7f;
                }
                path = 0;
            }
            else if (k == 0x10 || k == 0x11) path = 2;
            else if (k >= 0x14) path = 4;
            else if (k == 0x12) path = 3;
            else if (k == 0xa || k >= 0xd) path = 1;
            else if (k == 0xb || k == 0xc) path = 2;
            else path = 0;

            switch (path)
            {
                case 0:
                    if (cnt > 300 && M.I16(A + 0x3ea) > 0) M.I16(A + 0x3ea, (short)(M.I16(A + 0x3ea) - 1));
                    break;
                case 1:
                {
                    if ((cnt & 1) == 0) M.I16(A + 0x3ee, (short)(M.I16(A + 0x3ee) + 1));
                    ushort v = M.U16(A + 0x3ee);
                    if ((short)v >= 0x168) M.U16(A + 0x3ee, (ushort)(v - 0x168));
                    break;
                }
                case 2:
                case 3:
                {
                    if (path == 2 && (cnt & 1) == 0) M.I16(A + 0x3ee, (short)(M.I16(A + 0x3ee) - 1));
                    ushort v = M.U16(A + 0x3ee);
                    if ((short)v < 0) M.U16(A + 0x3ee, (ushort)(v + 0x168));
                    break;
                }
            }

            short alpha = M.I16(A + 0x3ea);
            if (alpha >= 0xff) M.I16(A + 0x3ea, 0xff);
            else if (alpha < 0) M.I16(A + 0x3ea, 0);

            float rot = f_41c;
            if (M.I8(A + 0x454) < 0x1e)
            {
                rot = rot + 2.0f;
                f_41c = rot;
            }
            if (!(rot < 360.0f || float.IsNaN(rot))) f_41c = rot - 360.0f;
            f_470 = m_pos_y - f_438;
            M.U32(A + 0x46c, M.U32(A + 0x10));
            M.U32(A + 0x474, M.U32(A + 0x18));
        }

        /// <summary>
        /// CEffectPrim::fn_afcca0 (0xafcca0; also types 0x5f, 0x80, 0x8c) — per active slot with alpha &gt; 0: a
        /// four-corner ring of radius size*(1 + 0.05 sin(+0x41c)) whose corner angles step by 90° (+0x424 == 0),
        /// 60°/120° (== 1) or 130°/50° (== 2) from +0x3ee, camera-rotated (FUN_00b0d9c0), placed at +0x46c. The slot
        /// style +0x452 picks the quads (colour / blend / screen mode); styles 2, 4, 5, 10, 12 fall through the next
        /// cases in the original switch and draw several coloured copies (2: six quads … 15: one).
        /// </summary>
        public void Render_afcca0(FxRuntime rt, uint matrix)
        {
            int count = 0;
            System.Span<float> p = stackalloc float[12];
            p.Clear();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                short alpha = M.I16(b + 2);
                if (alpha < 1) continue;
                int e = FxOps.Cvtt(M.F32(b + 0x34));
                int a1 = M.I16(b + 6);
                float size = M.F32(b + 0x80);
                float r = size * SinAt(e) * 0.05f + size;
                float step = M.F32(b + 0x3c);
                int a2, a3, a4;
                if (step == 0f) a2 = a1 + (a1 >= 0x5a ? -0x5a : 0x10e);
                else if (step == 1f) a2 = a1 + (a1 >= 0x3c ? -0x3c : 300);
                else if (step == 2f) { a2 = a1 - 0x82; if (a2 < 0) a2 += 0x168; }
                else a2 = a1;
                if (step == 0f) a3 = WrapNeg360(a2 - 0x5a);
                else if (step == 1f) a3 = WrapNeg360(a2 - 0x78);
                else if (step == 2f) { a3 = a2 - 0x32; if (a3 < 0) a3 += 0x168; }
                else a3 = a2;
                if (step == 0f) a4 = WrapNeg360(a3 - 0x5a);
                else if (step == 1f) a4 = WrapNeg360(a3 - 0x3c);
                else if (step == 2f) { a4 = a3 - 0x82; if (a4 < 0) a4 += 0x168; }
                else a4 = a3;

                // P1 (l4c l48 l44), P2 (l40 l3c l38), P3 (l28 l24 l20), P4 (l34 l30 l2c)
                p[0] = r * CosAt(a1); p[1] = 0f; p[2] = r * SinAt(a1);
                p[3] = r * CosAt(a2); p[4] = 0f; p[5] = r * SinAt(a2);
                p[6] = r * CosAt(a3); p[7] = 0f; p[8] = r * SinAt(a3);
                p[9] = r * CosAt(a4); p[10] = 0f; p[11] = r * SinAt(a4);
                sbyte style = M.I8(b + 0x6a);
                bool lift = false;
                if (style == 3)
                {
                    p[0] = p[0] + 3.1f; p[3] = p[3] + 3.1f; p[6] = p[6] + 3.1f; p[9] = p[9] + 3.1f;
                    lift = true;
                }
                else if ((byte)(style - 0x11) <= 0xe) lift = true;
                if (lift)
                {
                    p[11] = p[11] + 6.0f; p[8] = p[8] + 6.0f; p[5] = p[5] + 6.0f; p[2] = p[2] + 6.0f;
                }
                Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                float px = M.F32(b + 0x84), py = M.F32(b + 0x88), pz = M.F32(b + 0x8c);
                for (int k = 0; k < 4; k++)
                {
                    p[k * 3] = px + p[k * 3];
                    p[k * 3 + 1] = p[k * 3 + 1] + py;
                    p[k * 3 + 2] = pz + p[k * 3 + 2];
                }
                sbyte tex = (sbyte)M.U8(b + 0x68);

                switch ((int)style)
                {
                    case 0:
                    case 3:
                        QuadStraight_afcca0(rt, ref count, alpha, p, matrix, 1, tex, 0xff, 0xff, 0xff, 0);
                        break;
                    case 2:
                    case 4:
                    case 5:
                    case 10:
                    case 12:
                    case 15:
                        // the original cases 2, 4, 5, 10, 12 have no break: each falls into the next one down to 15
                        if (style == 2) QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0x82, 0x82, 0xfa, 0);
                        if (style <= 4) QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0xff, 0xff, 0xff, 0);
                        if (style <= 5) QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0x7d, 0xff, 0x7d, 0);
                        if (style <= 10) QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0x96, 0x96, 0x96, 0);
                        if (style <= 12) QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0x32, 0x32, 0x32, 0);
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0x33, 0x99, 0x99, 0);
                        break;
                    case 0x10:
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 1, tex, 0xff, 0xff, 0xff, 3);
                        break;
                    case 0x11:
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0xff, 0xff, 0xff, 0);
                        break;
                    case 0x12:
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 5, 5, 0xff, 0);
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0x9b, 0x9b, 0xff, 0);
                        break;
                    case 0x13:
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 5, 0xff, 5, 0);
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0x9b, 0xff, 0x9b, 0);
                        break;
                    case 0x14:
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0xff, 5, 5, 0);
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0xff, 0x9b, 0x9b, 0);
                        break;
                    case 0x20:
                        QuadSwapped_afcca0(rt, ref count, alpha, p, matrix, 0, tex, 0xff, 5, 5, 0);
                        break;
                }
            }
        }

        /// <summary>x + ((x &gt;&gt; 31) &amp; 360): the branch-free "wrap a negative angle" idiom.</summary>
        static int WrapNeg360(int x) => x + ((x >> 31) & 0x168);

        /// <summary>fn_b0a900 with (A, B, C, D) = (P1, P2, P3, P4), uv (0..1), relative.</summary>
        void QuadStraight_afcca0(FxRuntime rt, ref int count, short alpha, System.Span<float> p, uint matrix, short blend, sbyte tex, byte r, byte g, byte b, byte mode)
            => Quad_b0a900(rt, ref count, alpha, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                matrix, blend, tex, 1, r, g, b, 0f, 1f, 0f, 1f, mode);

        /// <summary>fn_b0a900 with (A, B, C, D) = (P2, P1, P4, P3), uv (0..1), relative.</summary>
        void QuadSwapped_afcca0(FxRuntime rt, ref int count, short alpha, System.Span<float> p, uint matrix, short blend, sbyte tex, byte r, byte g, byte b, byte mode)
            => Quad_b0a900(rt, ref count, alpha, p[3], p[4], p[5], p[0], p[1], p[2], p[9], p[10], p[11], p[6], p[7], p[8],
                matrix, blend, tex, 1, r, g, b, 0f, 1f, 0f, 1f, mode);

        // ================================================================ type 0x7b

        [FxPrimProcess(0x7b)]
        public FxCaseResult Case7B_E9(FxRuntime rt) { Process_aebe40(rt); return FxCaseResult.Continue; }

        [FxPrimRender(0x7b)]
        public void Render7B_E9(FxRuntime rt, uint viewMatrix) => Render_afaaf0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_aebe40 (0xaebe40) — camera angles, follow the master; per active slot: age +0x3f4, alpha
        /// +5 for 50 ticks and -5 after +0x3f8, two drifting angles (+0x3fc → +0x400, +0x404 → +0x408: step 1° towards
        /// the target, a new random target once reached), x/z drift by sin(angle) * 0.1, spin +0x41c and +0x3ee.
        /// </summary>
        public void Process_aebe40(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int cnt = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, cnt);
                if (cnt < 0x32) M.I16(b + 2, (short)(M.I16(b + 2) + 5));
                if (cnt > M.I16(b + 0x10)) M.I16(b + 2, (short)(M.I16(b + 2) - 5));
                for (uint pair = 0; pair < 2; pair++)
                {
                    uint cur = b + 0x14 + pair * 8, target = cur + 4;
                    float t = M.F32(target), v = M.F32(cur);
                    if (t > v)
                    {
                        v = v + 1.0f;
                        if (v > 359.0f) v = 359.0f;
                        M.F32(cur, v);
                        if (v > t)
                        {
                            int n = FxOps.Cvtt(v);
                            int pick = 0;
                            if (n != 0)
                            {
                                int m = rt.Rand.Next() % n;
                                pick = m >= 0 ? m : 0;
                            }
                            M.F32(target, (float)pick);
                        }
                    }
                    else
                    {
                        v = v - 1.0f;
                        if (0.0f > v) v = 0.0f;
                        M.F32(cur, v);
                        if (!(t < v || float.IsNaN(t) || float.IsNaN(v)))
                        {
                            int n = FxOps.Cvtt(v);
                            int pick = 0;
                            if (n != 0)
                            {
                                int m = rt.Rand.Next() % n;
                                pick = m >= 0 ? m : 0;
                            }
                            M.F32(target, (float)(0x167 - pick));
                        }
                    }
                }
                M.F32(b + 0x84, SinAt(FxOps.Cvtt(M.F32(b + 0x14))) * 0.1f + M.F32(b + 0x84));
                M.F32(b + 0x8c, SinAt(FxOps.Cvtt(M.F32(b + 0x1c))) * 0.1f + M.F32(b + 0x8c));
                float spin = M.F32(b + 0x34) + 1.0f;
                M.F32(b + 0x34, spin);
                if (!(spin < 360.0f || float.IsNaN(spin))) M.F32(b + 0x34, spin - 360.0f);
                ushort s = (ushort)(M.U16(b + 6) + 1);
                M.U16(b + 6, s);
                if ((short)s >= 0x168) M.U16(b + 6, (ushort)(s - 0x168));
            }
        }

        // ================================================================ type 0x7e

        /// <summary>
        /// CEffectPrim::OnProcess case 0x7e (inline, 0xad965c) — slot 0 only: age +0x3f4 (starts negative = delay),
        /// spin +0x466 by 30°; at age 1 aim +0x3f8 at the target +0x388/+0x390 (FUN_00ad5710); ages 2..10 alpha +6
        /// (≤ 60) and speed +0x400 * 1.02, later alpha -3 and speed * 1.04 (slot dies at alpha &lt; 0); moves along
        /// the heading; every 3rd tick slots 1..3 take the previous slot's position / alpha * 0.8 / spin (a trail).
        /// </summary>
        [FxPrimProcess(0x7e)]
        public FxCaseResult Case7E_E9(FxRuntime rt)
        {
            if (M.U8(A + 0x3e8) != 1) return FxCaseResult.Continue;
            int cnt = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, cnt);
            if (cnt <= 0) return FxCaseResult.Continue;
            ushort spin = (ushort)(M.U16(A + 0x466) + 0x1e);
            M.U16(A + 0x466, spin);
            if ((short)spin >= 0x168) M.U16(A + 0x466, (ushort)(spin - 0x168));
            float speed;
            if (cnt == 1)
            {
                M.I16(A + 0x3f8, FxMath.DirectionToAngle(f_388 - m_pos_x, f_390 - m_pos_z));
                speed = f_400;
                cnt = M.I32(A + 0x3f4);
            }
            else if (cnt <= 10)
            {
                ushort a = (ushort)(M.U16(A + 0x3ea) + 6);
                M.U16(A + 0x3ea, a);
                if ((short)a > 0x3c) M.I16(A + 0x3ea, 0x3c);
                speed = f_400 * 1.02f;
                f_400 = speed;
            }
            else
            {
                ushort a = (ushort)(M.U16(A + 0x3ea) - 3);
                M.U16(A + 0x3ea, a);
                speed = f_400 * 1.04f;
                f_400 = speed;
                if ((short)a < 0)
                {
                    M.I16(A + 0x3ea, 0);
                    M.U8(A + 0x3e8, 0);
                }
            }
            int h = M.I16(A + 0x3f8);
            f_46c = speed * CosAt(h) + f_46c;
            f_474 = speed * SinAt(h) + f_474;
            if (cnt % 3 != 0) return FxCaseResult.Continue;
            for (uint e = A + 0x68c; e > A + 0x524 - 1; e -= 0xb4)
            {
                uint n = e - 0xb4;
                M.U32(n + 0xb0, M.U32(e - 0xb8));
                M.U32(n + 0xb4, M.U32(n));
                M.U32(n + 0xb8, M.U32(n + 4));
                M.I16(n + 0x2e, (short)FxOps.Cvtt((float)M.I16(n - 0x86) * 0.8f));
                M.U16(n + 0xaa, M.U16(n - 0xa));
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x7e)]
        public void Render7E_E9(FxRuntime rt, uint viewMatrix) => Render_b07250(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b07250 (0xb07250) — per active slot with alpha &gt; 0: one green (0x50, 0xff, 0x50) quad,
        /// a square of radius +0x468 spun by +0x466 (corners 90° apart) in the vertical plane of the heading +0x3f8,
        /// at the slot position +0x46c.
        /// </summary>
        public void Render_b07250(FxRuntime rt, uint matrix)
        {
            int count = 0;
            System.Span<float> q = stackalloc float[12];
            System.Span<int> idx = stackalloc int[4];
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                short alpha = M.I16(b + 2);
                if (alpha <= 0) continue;
                int d = M.I16(b + 0x10);
                int a = M.I16(b + 0x7e);
                float radius = M.F32(b + 0x80);
                float px = M.F32(b + 0x84), py = M.F32(b + 0x88), pz = M.F32(b + 0x8c);
                float c = CosAt(d), s = SinAt(d);
                int i1 = a + (a < 0x10e ? 0x5a : -0x10e);
                int i2 = i1 + (i1 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int i3 = i2 + (i2 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int i4 = i3 + (i3 + 0x5a < 0x168 ? 0x5a : -0x10e);
                q.Clear();
                idx[0] = i1; idx[1] = i2; idx[2] = i3; idx[3] = i4;
                for (int k = 0; k < 4; k++)
                {
                    float rc = radius * CosAt(idx[k]);
                    q[k * 3] = c * rc + px;
                    q[k * 3 + 1] = radius * SinAt(idx[k]) + py;
                    q[k * 3 + 2] = rc * s + pz;
                }
                Quad_b0a900(rt, ref count, alpha, q[0], q[1], q[2], q[3], q[4], q[5], q[6], q[7], q[8], q[9], q[10], q[11],
                    matrix, 0, 0, 1, 0x50, 0xff, 0x50, 0f, 1f, 0f, 1f, 0);
            }
        }

        // ================================================================ type 0x95

        [FxPrimProcess(0x95)]
        public FxCaseResult Case95_E9(FxRuntime rt) { Process_b0dd80(); return FxCaseResult.Continue; }

        [FxPrimRender(0x95)]
        public void Render95_E9(FxRuntime rt, uint viewMatrix) => Render_b12310(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b0dd80 (0xb0dd80) — follow the master; per active slot: age +0x3f4 (≤ 0 = waiting), spin
        /// +0x3f8 by 3°, alpha +3 (≤ 100) for 30 ticks and -2 in the last 46; the 21 ring radii +0x3fc.. = base +0x3f0
        /// modulated by sin(9j°) * 0.3 * sin(phase), phase = age + 90·slot (slots 0/1) or (age + 45·slot) * 2.
        /// </summary>
        public void Process_b0dd80()
        {
            FollowMaster_P1();
            for (int k = 0; k < 4; k++)
            {
                uint b = Blk(k);
                if (M.U8(b) != 1) continue;
                int cnt = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, cnt);
                if (cnt <= 0) continue;
                ushort ang = (ushort)(M.U16(b + 0x10) + 3);
                M.U16(b + 0x10, ang);
                if ((short)ang >= 0x168) M.U16(b + 0x10, (ushort)(ang - 0x168));
                if (cnt < 0x1e)
                {
                    M.I16(b + 2, (short)(M.I16(b + 2) + 3));
                    if (M.I16(b + 2) > 100) M.I16(b + 2, 100);
                }
                if (cnt > m_duration - 0x2e)
                {
                    short a = (short)(M.I16(b + 2) - 2);
                    M.I16(b + 2, a);
                    if (a < 0) M.I16(b + 2, 0);
                }
                int ebx = k * 0x2d;
                for (int j = 0; j < 21; j++)
                {
                    int ph = M.I32(b + 0xc);
                    ph = ebx < 0x5a ? ph + k * 0x5a : (ph + ebx) * 2;
                    int rem = ph % 0x168;
                    float r0 = M.F32(b + 8);
                    M.F32(b + 0x14 + (uint)j * 4, r0);
                    M.F32(b + 0x14 + (uint)j * 4, SinAt(j * 9) * M.F32(b + 8) * 0.3f * SinAt(rem) + r0);
                }
            }
        }

        /// <summary>
        /// CEffectPrim::fn_b12310 (0xb12310) — per active slot (spread +0x3ee ≥ 0): a ring strip of up to 20 segments
        /// every spread/20 degrees from +0x3f8, inner radius sin(+0x3f8) + +0x468, outer radius + ring radius j
        /// (+0x3fc + 4j) tilted by +0x466; each segment one strip in palette 0 (alpha) and one in the slot mode's palette
        /// +0x3ec (≤2: 17, 3: 18, 4: 19, 5: 20, 6: 5, 7: 21, 8: 22, 9: 30, 11: 32, alpha ×3/×2, /mineffect keeps it
        /// for 9/11); mode 10 only one strip in palette 6 with alpha ×3 and frames (age / 3) % f_204. Modes above 11
        /// reuse an uninitialised local in the original (the port keeps the previous value, 0 at the start).
        /// </summary>
        public void Render_b12310(FxRuntime rt, uint matrix)
        {
            int count = 0;
            float prevAx = 0f, prevAz = 0f;              // l58, l50 (y is always 0)
            float prevBx = 0f, prevBy = 0f, prevBz = 0f; // l30, l2c, l10
            short staleAlpha = 0;                        // [ebp-0x20]
            for (int k = 0; k < 4; k++)
            {
                uint b = Blk(k);
                if (M.U8(b) != 1) continue;
                short spread = M.I16(b + 6);
                int step = spread / 20;
                if (spread < 0) continue;
                int j = 0;
                int t = 0;
                do
                {
                    int bas = M.I16(b + 0x10);
                    int ang = (bas + t) % 0x168;
                    if (M.U16(b + 6) == 0x168 && j == 0x14) ang = bas;
                    float sb = SinAt(bas);
                    float ci = CosAt(ang), si = SinAt(ang);
                    float rj = M.F32(A + 0x3fc + (uint)(k * 0x2d + j) * 4);
                    int w = M.I16(b + 0x7e);
                    sb = sb + M.F32(b + 0x80);
                    float rc = rj * CosAt(w);
                    float ry = -(rj * SinAt(w));
                    float ax = sb * ci, az = sb * si;
                    float bx = rc * ci + ax;
                    float bz = rc * si + az;
                    if (j > 0)
                    {
                        short mode = M.I16(b + 4);
                        int age = M.I32(b + 0xc);
                        int frames = f_204;
                        short alpha = M.I16(b + 2);
                        int tex;
                        short alpha2;
                        if (mode == 10)
                        {
                            tex = (age / 3) % frames;
                            f_318 = 6.0f;
                            alpha2 = (short)(alpha * 3);
                        }
                        else
                        {
                            tex = age % frames;
                            f_318 = 0f;
                            Strip_b0b320(rt, ref count, j - 1, alpha, prevAx, 0f, prevAz, ax, 0f, az, bx, ry, bz, prevBx, prevBy, prevBz,
                                matrix, (sbyte)tex);
                            if (mode <= 2) { f_318 = 17.0f; alpha2 = (short)(M.I16(b + 2) * 3); }
                            else if (mode == 3) { f_318 = 18.0f; alpha2 = (short)(M.I16(b + 2) * 3); }
                            else if (mode == 4) { f_318 = 19.0f; alpha2 = (short)(M.I16(b + 2) * 2); }
                            else if (mode == 5) { f_318 = 20.0f; alpha2 = (short)(M.I16(b + 2) * 2); }
                            else if (mode == 7) { f_318 = 21.0f; alpha2 = (short)(M.I16(b + 2) * 2); }
                            else if (mode == 8) { f_318 = 22.0f; alpha2 = (short)(M.I16(b + 2) * 2); }
                            else if (mode == 9) { f_318 = 30.0f; alpha2 = rt.MinEffect ? M.I16(b + 2) : (short)(M.I16(b + 2) * 2); }
                            else if (mode == 6) { f_318 = 5.0f; alpha2 = (short)(M.I16(b + 2) * 2); }
                            else if (mode == 11) { f_318 = 32.0f; alpha2 = rt.MinEffect ? M.I16(b + 2) : (short)(M.I16(b + 2) * 2); }
                            else alpha2 = staleAlpha;
                        }
                        if (alpha2 > 0xff) alpha2 = 0xff;
                        staleAlpha = alpha2;
                        Strip_b0b320(rt, ref count, j - 1, alpha2, prevAx, 0f, prevAz, ax, 0f, az, bx, ry, bz, prevBx, prevBy, prevBz,
                            matrix, (sbyte)tex);
                    }
                    j++;
                    if (j >= 0x15) j = 0x14;
                    prevAx = ax; prevAz = az;
                    prevBx = bx; prevBy = ry; prevBz = bz;
                    t += step;
                } while (t <= M.I16(b + 6));
            }
        }
    }
}
