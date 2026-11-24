// Synced from reference/port/src/RoClassicFx/Prim/prims-1/Prim.Prims1.Helpers.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>Shared pieces of the prims-1 prim types (raw trig tables, direction search, quad emitters).</summary>
    public readonly partial struct Prim
    {
        // ---- raw trig tables ---------------------------------------------------------------------------
        // The client indexes DAT_011a4a28 (sin) / DAT_011a5570 (cos) directly with unchecked ints; the six
        // 361-entry tables (rad, -rad, sin, -sin, cos, -cos) are contiguous from 0x011a3ee0, so negative or
        // large indices read the neighbouring tables. Address arithmetic wraps like [base + idx*4].

        const uint TrigImageVa = 0x011a3ee0, SinTableVa = 0x011a4a28, CosTableVa = 0x011a5570;
        static readonly float[] trigImage = BuildTrigImage();

        static float[] BuildTrigImage()
        {
            var t = new float[361 * 6];
            for (int i = 0; i <= 360; i++)
            {
                float rad = FxMath.DegToRad(i), sin = FxMath.GetSin(i), cos = FxMath.GetCos(i);
                t[i] = rad; t[361 + i] = -rad;
                t[722 + i] = sin; t[1083 + i] = -sin;
                t[1444 + i] = cos; t[1805 + i] = -cos;
            }
            return t;
        }

        /// <summary>*(float*)(table + byteOffset) inside the trig image (throws outside it: the original would read other globals).</summary>
        static float TrigAtByte(uint table, int byteOffset)
        {
            uint off = table + (uint)byteOffset - TrigImageVa;
            if (off >= (uint)trigImage.Length * 4 || (off & 3) != 0)
                throw new InvalidOperationException($"trig table read at 0x{table + (uint)byteOffset:x8} is outside the sin/cos tables");
            return trigImage[off >> 2];
        }

        /// <summary>DAT_011a4a28[i] (raw, unchecked index).</summary>
        static float SinAt(int i) => TrigAtByte(SinTableVa, (int)((uint)i * 4u));

        /// <summary>DAT_011a5570[i] (raw, unchecked index).</summary>
        static float CosAt(int i) => TrigAtByte(CosTableVa, (int)((uint)i * 4u));

        // ---- small shared idioms -----------------------------------------------------------------------

        /// <summary>Address of sub-block <paramref name="i"/> (4 blocks of 0xb4 bytes from +0x3e8).</summary>
        uint Blk(int i) => A + 0x3e8 + (uint)i * 0xb4;

        /// <summary>
        /// Inlined camera snippet: f_3ec = 360 - (short)view.f_30, f_4a0 = 450 - (short)view.f_2c wrapped below
        /// 360 (16-bit arithmetic), view = GetWorld()->+0xd0.
        /// </summary>
        void CameraAngles_P1(FxRuntime rt)
        {
            uint view = M.U32(rt.Mode + 0xd0);
            M.I16(A + 0x3ec, (short)(0x168 - (short)FxOps.Cvtt(M.F32(view + 0x30))));
            short s = (short)(0x1c2 - (short)FxOps.Cvtt(M.F32(view + 0x2c)));
            M.I16(A + 0x4a0, s);
            if (s >= 0x168) M.I16(A + 0x4a0, (short)(s - 0x168));
        }

        /// <summary>m_pos = master position (copied as raw dwords).</summary>
        void FollowMaster_P1()
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
        }

        /// <summary>
        /// FUN_00ad5710 — direction (degrees, 0..359) of (dx, dz): first i in 0..180 with dx/len ≥ cos[i]
        /// (360 - i when dz/len &lt; 0 or unordered); 180 when none. len = FUN_00534e70 (x87 fsqrt, stored as float).
        /// </summary>
        static short Direction_ad5710(float dx, float dz)
        {
            float len = (float)Math.Sqrt((double)(dx * dx + dz * dz));
            float nz = dz / len;
            float nx = dx / len;
            for (short i = 0; i <= 0xb4; i++)
            {
                if (nx >= CosAt(i))   // COMISS nx, cos[i]; JNC
                    return (nz < 0f || float.IsNaN(nz)) ? (short)(0x168 - i) : i;
            }
            return 0xb4;
        }

        /// <summary>
        /// FUN_00b0d9c0 — rotates four points (x, y, z) about X by <paramref name="b"/> then about Y by
        /// <paramref name="a"/> (raw trig table indices).
        /// </summary>
        static void Rotate4_b0d9c0(Span<float> p, short a, short b)
        {
            float cb = CosAt(b), sb = SinAt(b);
            for (int k = 0; k < 4; k++)
            {
                float y = p[k * 3 + 1], z = p[k * 3 + 2];
                p[k * 3 + 2] = z * cb - y * sb;
                p[k * 3 + 1] = y * cb + z * sb;
            }
            float ca = CosAt(a), sa = SinAt(a);
            for (int k = 0; k < 4; k++)
            {
                float x = p[k * 3], z = p[k * 3 + 2];
                p[k * 3] = x * ca - z * sa;
                p[k * 3 + 2] = z * ca + x * sa;
            }
        }

        /// <summary>
        /// The camera-facing diamond several renderers build on the stack: corners at 45° / 135° / 225° / 315° of radius
        /// <paramref name="size"/> in the XZ plane (the client's initialised local array, element order kept).
        /// </summary>
        static void Diamond45(Span<float> p, float size)
        {
            p[0] = size * FxMath.CosTableAt(45); p[1] = 0f; p[2] = size * FxMath.SinTableAt(45);
            p[3] = size * FxMath.CosTableAt(135); p[4] = 0f; p[5] = size * FxMath.SinTableAt(135);
            p[6] = size * FxMath.CosTableAt(225); p[7] = 0f; p[8] = size * FxMath.SinTableAt(225);
            p[9] = size * FxMath.CosTableAt(315); p[10] = 0f; p[11] = size * FxMath.SinTableAt(315);
        }

        /// <summary>Slot field offsets of the four sub-sprites (types 0x23 / 0x78 renderers): cos, sin and height.</summary>
        static readonly uint[] SubSpriteCosOff = { 0x484, 0x46c, 0x490, 0x478 };
        static readonly uint[] SubSpriteSinOff = { 0x48c, 0x474, 0x498, 0x480 };
        static readonly uint[] SubSpriteHeightOff = { 0x488, 0x470, 0x494, 0x47c };

        /// <summary>FUN_0053b030 — ((1 / rhw) - 10) * 1.0067114 * rhw (SSE), returned through x87 as float.</summary>
        static float DepthFromRhw_53b030(float rhw)
        {
            float v = 1f / rhw;
            v -= 10f;
            v *= BitConverter.Int32BitsToSingle(0x3f80dbeb);
            v *= rhw;
            return v;
        }

        /// <summary>Projects (x, y, z) into the TL vertex at <paramref name="v"/> (+0 xyz rhw, +0x10 colour, +0x14 specular, +0x18 uv).</summary>
        void PutVertex_P1(FxRuntime rt, uint v, uint matrix, float x, float y, float z, int argb, float tu, float tv)
        {
            rt.Renderer.ProjectVertex(M, matrix, x, y, z, out float sx, out float sy, out float sz, out float rhw);
            M.TlVertex(v, sx, sy, sz, rhw, (uint)argb, tu, tv);
        }

        /// <summary>(renderer+0x2c * -30) / 480 — the screen-height dependent offset used by mode-3 billboards.</summary>
        static float ScreenYOffset_P1(FxRuntime rt) => (float)(rt.M.I32(rt.Renderer.Address + 0x2c) * -0x1e / 0x1e0);

        /// <summary>
        /// CEffectPrim::fn_b0a900 — a textured quad (A, B, C, D) as two RP faces (A B C) and (D C A) from the
        /// prim's face buffer (+0x2210, index <paramref name="count"/>). Colour = alpha:r:g:b with the master's
        /// colour / blend overrides (+0x11c88..+0x11c94); <paramref name="blend"/> 10 = SRCALPHA/ONE-ish
        /// (2, 1), 99 → dst 6, else dst = master override or (blend != 0 ? 6 : 2). <paramref name="relative"/> 0
        /// offsets the points by m_pos. <paramref name="mode"/>: 1 = z/rhw forced to 1e-9/1, 2 = rhw nudged and z
        /// recomputed (FUN_0053b030), 3 = screen-aligned width around the prim position.
        /// UVs: A (u0, v1), B (u1, v1), C (u1, v0) / D (u0, v0), C (u1, v0), A (u0, v1) with u0 = p25, u1 = p26,
        /// v0 = p23, v1 = p24.
        /// </summary>
        public void Quad_b0a900(FxRuntime rt, ref int count, short alpha,
            float ax, float ay, float az, float bx, float by, float bz,
            float cx, float cy, float cz, float dx, float dy, float dz,
            uint matrix, short blend, sbyte tex, byte relative, byte r, byte g, byte b,
            float v0, float v1, float u0, float u1, byte mode)
        {
            uint master = m_master_ptr;
            int rr = r, gg = g, bb = b;
            uint o = M.U32(master + 0x11c88); if (o != 0xffffffffu) rr = (byte)o;
            o = M.U32(master + 0x11c8c); if (o != 0xffffffffu) gg = (byte)o;
            o = M.U32(master + 0x11c90); if (o != 0xffffffffu) bb = (byte)o;
            int dstBlend = M.I32(master + 0x11c94);
            if (dstBlend == -1) dstBlend = (blend != 0 ? 1 : 0) * 4 + 2;
            if (count >= 0x1fe) return;
            int argb = (((alpha * 0x100 + (byte)rr) * 0x100 + (byte)gg) * 0x100) + (byte)bb;
            if (relative == 0)
            {
                float px = m_pos_x, py = m_pos_y, pz = m_pos_z;
                ax = px + ax; ay = py + ay; az = pz + az;
                bx = px + bx; by = py + by; bz = pz + bz;
                cx = px + cx; cy = py + cy; cz = pz + cz;
                dx = px + dx; dy = py + dy; dz = pz + dz;
            }
            uint texture = M.U32((uint)f_144 + (uint)(tex * 4));

            // face 1: A B C
            uint face = A + 0x2210 + (uint)count * FxRenderer.FaceSize;
            SetFaceBlend_b0a900(face, blend, dstBlend);
            PutVertex_P1(rt, face + 0x00, matrix, ax, ay, az, argb, u0, v1);
            PutVertex_P1(rt, face + 0x20, matrix, bx, by, bz, argb, u1, v1);
            PutVertex_P1(rt, face + 0x40, matrix, cx, cy, cz, argb, u1, v0);
            M.U32(face + 0x60, texture);
            FinishFace_b0a900(rt, face, matrix, mode, false);
            rt.Renderer.AddRP(face, (int)f_1c4);
            count++;

            // face 2: D C A
            face = A + 0x2210 + (uint)count * FxRenderer.FaceSize;
            SetFaceBlend_b0a900(face, blend, dstBlend);
            PutVertex_P1(rt, face + 0x00, matrix, dx, dy, dz, argb, u0, v0);
            PutVertex_P1(rt, face + 0x20, matrix, cx, cy, cz, argb, u1, v0);
            PutVertex_P1(rt, face + 0x40, matrix, ax, ay, az, argb, u0, v1);
            M.U32(face + 0x60, texture);
            FinishFace_b0a900(rt, face, matrix, mode, true);
            rt.Renderer.AddRP(face, (int)f_1c4);
            count++;
        }

        void SetFaceBlend_b0a900(uint face, short blend, int dstBlend)
        {
            if (blend == 10)
            {
                M.I32(face + 0x64, 2);
                M.I32(face + 0x68, 1);
            }
            else
            {
                M.I32(face + 0x64, 5);
                M.I32(face + 0x68, blend == 99 ? 6 : dstBlend);
            }
        }

        void FinishFace_b0a900(FxRuntime rt, uint face, uint matrix, byte mode, bool secondFace)
        {
            if (mode == 2)
            {
                for (uint k = 0; k < 0x60; k += 0x20)
                {
                    M.F32(face + k + 0xc, M.F32(face + k + 0xc) + BitConverter.Int32BitsToSingle(0x383cbe62));
                    M.F32(face + k + 8, DepthFromRhw_53b030(M.F32(face + k + 0xc)));
                }
                return;
            }
            if (mode == 3)
            {
                rt.ProjectTl(matrix, A + 0x10, out float sx, out _, out _);
                float half = Math.Abs(M.F32(face + 0x00) - M.F32(face + 0x20)) * 0.5f;
                M.F32(face + 0x00, sx + half);
                M.F32(face + 0x20, sx - half);
                M.F32(face + 0x40, secondFace ? sx + half : sx - half);
                M.F32(face + 0x04, ScreenYOffset_P1(rt) * f_5c + M.F32(face + 0x04));
                M.F32(face + 0x24, ScreenYOffset_P1(rt) * f_5c + M.F32(face + 0x24));
                M.F32(face + 0x44, ScreenYOffset_P1(rt) * f_5c + M.F32(face + 0x44));
            }
            else if (mode != 1) return;
            M.U32(face + 0x08, 0x3089705f); M.F32(face + 0x0c, 1f);
            M.U32(face + 0x28, 0x3089705f); M.F32(face + 0x2c, 1f);
            M.U32(face + 0x48, 0x3089705f); M.F32(face + 0x4c, 1f);
        }

        /// <summary>
        /// CEffectPrim::fn_b0b320 — one ribbon segment (A B C D, offset by m_pos) as two RP faces (A B C) and
        /// (D C A). Colour from the (int)f_318 palette (with the master's overrides), alpha <paramref name="alpha"/>
        /// (0x50 for palette 3), u from the segment index (/21, or fixed bands for 100/101/other ≥ 100).
        /// </summary>
        public void Strip_b0b320(FxRuntime rt, ref int count, int seg, short alpha,
            float ax, float ay, float az, float bx, float by, float bz,
            float cx, float cy, float cz, float dx, float dy, float dz, uint matrix, sbyte tex)
        {
            if (count >= 0x1fe) return;
            int dst = 2;
            int a = alpha;
            byte r = 0xff, g = 0xff, b = 0xff;
            float vTop = 1f;
            switch (FxOps.Cvtt(f_318))
            {
                case 0: dst = 6; break;
                case 1: r = 0xff; g = 0xaf; b = 0xaf; break;
                case 2: r = 0xc3; g = 0xc3; b = 0xff; dst = 6; break;
                case 3: a = 0x50; dst = 6; break;
                case 4: r = 100; g = 100; b = 0xff; break;
                case 6: r = 0xff; g = 100; b = 100; break;
                case 7: r = 100; g = 0xff; b = 100; break;
                case 8: r = 0xff; g = 0xff; b = 0xaa; break;
                case 9: r = 0x8c; g = 0xd2; b = 0x8c; break;
                case 10: r = 0xff; g = 0x59; b = 0xb6; break;
                case 0xb: r = 0xaa; g = 0xaa; b = 0xff; break;
                case 0xc: r = 0x32; g = 0x32; b = 0x32; dst = 6; break;
                case 0xd: r = 0xff; g = 0x32; b = 0xff; dst = 6; break;
                case 0xe: r = 0x78; g = 0x78; b = 0xff; dst = 6; break;
                case 0xf: r = 10; g = 0xff; b = 10; break;
                case 0x10: r = 0x14; g = 0xff; b = 0x14; break;
                case 0x11: r = 0; g = 0; b = 0xff; break;
                case 0x12: r = 0xff; g = 0; b = 0; break;
                case 0x13: r = 0; g = 0xff; b = 0; break;
                case 0x14: r = 0xff; g = 0xff; b = 0; break;
                case 0x15: r = 0xff; g = 0; b = 0xff; break;
                case 0x16: r = 0; g = 0xff; b = 0xff; break;
                case 0x17: r = 0x29; g = 0x5e; b = 0xbc; dst = 6; break;
                case 0x18: r = 10; g = 0x42; b = 0x72; break;
                case 0x19: r = 0x23; g = 0x92; b = 0xb9; break;
                case 0x1a: r = 0xa4; g = 0xcd; b = 0xfe; break;
                case 0x1b: r = 0x54; g = 0xa9; b = 0xcd; break;
                case 0x1c: r = 0x6c; g = 0x1a; b = 0xff; break;
                case 0x1d: r = 0xb1; g = 0xff; b = 0x3a; break;
                case 0x1e: r = 0x55; g = 0x1e; b = 0xb9; break;
                case 0x1f: r = 0xf4; g = 0xbb; b = 0x7c; break;
                case 0x20: r = 0; g = 0; b = 0; dst = 6; break;
                case 0x28: r = 0x46; g = 0x46; b = 0xff; break;
                case 0x29: r = 0x1e; g = 0x1e; b = 0xff; break;
                case 0x2a: r = 0x2b; g = 0x28; b = 0x2e; dst = 6; break;
                case 0x2b: r = 0x7d; g = 0xb6; b = 0xa0; dst = 6; break;
                case 0x2c: r = 0xb4; g = 0x37; b = 0x1e; dst = 6; break;
                case 0x2d: r = 0x41; g = 0x8d; b = 0xe7; break;
                case 0x2e: r = 0xa4; g = 0x2f; b = 0x1d; break;
                case 0x2f: r = 0xac; g = 0x9a; b = 0x38; break;
                case 0x30: r = 0x50; g = 0x14; b = 0x14; break;
                case 0x31: r = 200; g = 0x82; b = 0x54; break;
                case 0x32: r = 0xff; g = 0xc6; b = 0x5b; break;
                case 100: r = 0xfa; g = 0xfa; b = 0xff; vTop = BitConverter.Int32BitsToSingle(0x3f666666); dst = 6; break;
            }
            uint master = m_master_ptr;
            int dstBlend = M.I32(master + 0x11c94);
            if (dstBlend == -1) dstBlend = dst;
            uint o = M.U32(master + 0x11c88); int rr = o == 0xffffffffu ? r : (byte)o;
            o = M.U32(master + 0x11c8c); int gg = o == 0xffffffffu ? g : (byte)o;
            o = M.U32(master + 0x11c90); int bb = o == 0xffffffffu ? b : (byte)o;
            int argb = (((rr + a * 0x100) * 0x100 + gg) * 0x100) + bb;

            float u0, u1;
            if (seg < 100)
            {
                u0 = (float)seg / 21f;
                if (seg == 0) u0 = 0f;
                u1 = (float)(seg + 1) / 21f;
            }
            else if (seg == 100) { u0 = BitConverter.Int32BitsToSingle(0x3eb33333); u1 = BitConverter.Int32BitsToSingle(0x3ee66666); }
            else if (seg == 0x65) { u0 = BitConverter.Int32BitsToSingle(0x3ee66666); u1 = BitConverter.Int32BitsToSingle(0x3f0ccccd); }
            else { u0 = BitConverter.Int32BitsToSingle(0x3f0ccccd); u1 = BitConverter.Int32BitsToSingle(0x3f266666); }

            float px = m_pos_x, py = m_pos_y, pz = m_pos_z;
            ax = px + ax; ay = py + ay; az = pz + az;
            bx = px + bx; by = py + by; bz = pz + bz;
            cx = px + cx; cy = py + cy; cz = pz + cz;
            dx = px + dx; dy = py + dy; dz = pz + dz;
            uint texture = M.U32((uint)f_144 + (uint)(tex * 4));

            uint face = A + 0x2210 + (uint)count * FxRenderer.FaceSize;
            M.I32(face + 0x64, 5);
            M.I32(face + 0x68, dstBlend);
            PutVertex_P1(rt, face + 0x00, matrix, ax, ay, az, argb, u0, vTop);
            PutVertex_P1(rt, face + 0x20, matrix, bx, by, bz, argb, u1, vTop);
            PutVertex_P1(rt, face + 0x40, matrix, cx, cy, cz, argb, u1, 0f);
            M.U32(face + 0x60, texture);
            rt.Renderer.AddRP(face, (int)f_1c4);
            count++;

            face = A + 0x2210 + (uint)count * FxRenderer.FaceSize;
            M.I32(face + 0x64, 5);
            M.I32(face + 0x68, dstBlend);
            PutVertex_P1(rt, face + 0x00, matrix, dx, dy, dz, argb, u0, 0f);
            PutVertex_P1(rt, face + 0x20, matrix, cx, cy, cz, argb, u1, 0f);
            PutVertex_P1(rt, face + 0x40, matrix, ax, ay, az, argb, u0, vTop);
            M.U32(face + 0x60, texture);
            rt.Renderer.AddRP(face, (int)f_1c4);
            count++;
        }
    }
}
