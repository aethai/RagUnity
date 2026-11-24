// Synced from reference/port/src/RoClassicFx/Prim/prims-1/Prim.Prims1.Render.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>CEffectPrim::Render (0xaf4ce0) cases of the prims-1 types.</summary>
    public readonly partial struct Prim
    {
        // ---------------------------------------------------------------- registrations

        [FxPrimRender(0x22)]
        public void Render22(FxRuntime rt, uint viewMatrix) => Render_af0050(rt, viewMatrix);

        [FxPrimRender(0x2a, 0x2d, 0x2f, 0x31, 0x35, 0x3a, 0x3b, 0x57)]
        public void RenderBillboards(FxRuntime rt, uint viewMatrix) => Render_afaaf0(rt, viewMatrix);

        [FxPrimRender(0x2b)]
        public void Render2B(FxRuntime rt, uint viewMatrix) => Render_b02b70(rt, viewMatrix);

        [FxPrimRender(0x30)]
        public void Render30(FxRuntime rt, uint viewMatrix) => Render_af8640(rt, viewMatrix);

        [FxPrimRender(0x36)]
        public void Render36(FxRuntime rt, uint viewMatrix) => Render_af9f40(rt, viewMatrix);

        [FxPrimRender(0x3e)]
        public void Render3E(FxRuntime rt, uint viewMatrix) => Render_b0cc40(rt, viewMatrix);

        [FxPrimRender(0x45)]
        public void Render45(FxRuntime rt, uint viewMatrix) => Render_b0c250(rt, viewMatrix);

        [FxPrimRender(0x46)]
        public void Render46(FxRuntime rt, uint viewMatrix) => Render_b023e0(rt, viewMatrix);

        [FxPrimRender(0x49)]
        public void Render49(FxRuntime rt, uint viewMatrix) => Render_b04090(rt, viewMatrix);

        [FxPrimRender(0x66)]
        public void Render66(FxRuntime rt, uint viewMatrix) => Render_afd940(rt, viewMatrix);

        [FxPrimRender(0x6d)]
        public void Render6D(FxRuntime rt, uint viewMatrix) => Render_b08480(rt, viewMatrix);

        [FxPrimRender(0x77, 0x7a, 0x7d)]
        public void RenderBolt(FxRuntime rt, uint viewMatrix) => Render_b07540(rt, viewMatrix);

        // ---------------------------------------------------------------- fn_afaaf0

        /// <summary>True when the mode's map name (mode+0x68, copied into a std::string by the client) is "ecl_fild01.rsw".</summary>
        static bool MapIsEclFild01_afaaf0(FxRuntime rt)
        {
            const string name = "ecl_fild01.rsw";
            uint a = rt.Mode + 0x68;
            for (int i = 0; i < name.Length; i++)
                if (rt.M.U8(a + (uint)i) != (byte)name[i]) return false;
            return rt.M.U8(a + (uint)name.Length) == 0;
        }

        /// <summary>
        /// CEffectPrim::fn_afaaf0 — camera-facing sprite per active block (size B+0x80 pulsing with sin(B+0x34),
        /// spin B+6, position B+0x84..), coloured by the prim-level kind f_452 (f_453 for kind 9).
        /// </summary>
        public void Render_afaaf0(FxRuntime rt, uint matrix)
        {
            int count = 0;
            Span<float> p = stackalloc float[12];
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1 || M.I16(b + 2) < 1) continue;
                if (MapIsEclFild01_afaaf0(rt) && M.I16(b + 2) > 100) M.I16(b + 2, 100);
                float ang = M.F32(b + 0x34);
                int spin = M.I16(b + 6);
                if (ang >= 360f || ang < 0f)
                {
                    M.F32(b + 0x34, 0f);
                    ang = 0f;
                }
                int a = FxOps.Cvtt(ang);
                float size = M.F32(b + 0x80);
                float l = size * SinAt(a) * 0.05f + size;
                int i1 = spin + (spin < 0x10e ? 0x5a : -0x10e);
                int i2 = i1 + (i1 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int i3 = i2 + (i2 + 0x5a < 0x168 ? 0x5a : -0x10e);
                p[0] = l * CosAt(spin); p[1] = 0f; p[2] = l * SinAt(spin);
                p[3] = l * CosAt(i1); p[4] = 0f; p[5] = l * SinAt(i1);
                p[6] = l * CosAt(i2); p[7] = 0f; p[8] = l * SinAt(i2);
                p[9] = l * CosAt(i3); p[10] = 0f; p[11] = l * SinAt(i3);
                if (M.F32(b + 0x38) == 10f)
                {
                    p[0] += 4f; p[3] += 4f; p[6] += 4f; p[9] += 4f;
                }
                Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                float px = M.F32(b + 0x84), py = M.F32(b + 0x88), pz = M.F32(b + 0x8c);
                for (int k = 0; k < 4; k++)
                {
                    p[k * 3] = px + p[k * 3];
                    p[k * 3 + 1] = py + p[k * 3 + 1];
                    p[k * 3 + 2] = pz + p[k * 3 + 2];
                }

                // P1 = p[0..2], P2 = p[3..5], P3 = p[6..8], P4 = p[9..11]
                int kind = (sbyte)M.U8(A + 0x452);
                bool straight = false;          // quad (P1 P2 P3 P4) instead of (P2 P1 P4 P3)
                short blend;
                byte r, g, bl;
                short alpha = M.I16(b + 2);
                switch (kind)
                {
                    case 0: r = 0xff; g = 0xff; bl = 0xff; blend = 1; straight = true; break;
                    case 0xf: r = 0xff; g = 0xc8; bl = 0xff; blend = 1; straight = true; break;
                    case 1: r = 0x96; g = 0x96; bl = 0xfa; blend = 0; straight = true; break;
                    case 2: r = 0x82; g = 0x82; bl = 0xfa; blend = 0; break;
                    case 3: r = 200; g = 200; bl = 100; blend = 0; break;
                    case 4: r = 0xff; g = 0xff; bl = 0xff; blend = 1; break;
                    case 7: case 0x17: r = 0xff; g = 0xff; bl = 0xff; blend = 0; break;
                    case 8: r = 0x87; g = 0xff; bl = 0x87; blend = 1; break;
                    case 9:
                        switch (M.U8(A + 0x453))
                        {
                            case 1: r = 0xff; g = 0x87; bl = 0x87; blend = 1; break;
                            case 2: r = 0xff; g = 0xcd; bl = 0x87; blend = 1; break;
                            case 3: r = 0xff; g = 0xff; bl = 0x87; blend = 1; break;
                            case 4: r = 0xff; g = 0xff; bl = 0xff; blend = 1; break;
                            case 5: r = 0x87; g = 0x87; bl = 0xff; blend = 1; break;
                            case 6: r = 0xff; g = 0xff; bl = 0xff; blend = 0; break;
                            default: continue;
                        }
                        break;
                    case 10: r = 0xfa; g = 0xfa; bl = 0x96; blend = 0; break;
                    case 0xb: r = 0xdc; g = 0xfa; bl = 0xdc; blend = 0; break;
                    case 0xc: r = 0xfc; g = 0xab; bl = 0x8f; blend = 1; break;
                    case 0xd: r = 0xfa; g = 100; bl = 100; blend = 0; break;
                    case 0xe: r = 0x5e; g = 0; bl = 0; blend = 1; break;
                    case 0x10: r = 0; g = 0; bl = 0; blend = 1; break;
                    case 0x11: r = 0xff; g = 0xb4; bl = 0xb4; blend = 1; break;
                    case 0x12: r = 0xff; g = 0xff; bl = 0xff; blend = 0; alpha = (short)(alpha / 2); break;
                    case 0x18: r = 0x3f; g = 0x48; bl = 0xcc; blend = 0; break;
                    case 0x19:
                        p[1] -= 110f; p[4] -= 110f; p[7] -= 110f; p[10] -= 110f;
                        r = 0xff; g = 0xff; bl = 0xff; blend = 0;
                        break;
                    default: continue;
                }
                sbyte tex = (sbyte)M.U8(b + 0x68);
                if (straight)
                    Quad_b0a900(rt, ref count, alpha, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                        matrix, blend, tex, 1, r, g, bl, 0f, 1f, 0f, 1f, 0);
                else
                    Quad_b0a900(rt, ref count, alpha, p[3], p[4], p[5], p[0], p[1], p[2], p[9], p[10], p[11], p[6], p[7], p[8],
                        matrix, blend, tex, 1, r, g, bl, 0f, 1f, 0f, 1f, 0);
            }
        }

        // ---------------------------------------------------------------- fn_af8640 (0x30)

        /// <summary>CEffectPrim::fn_af8640 — per block a vertical quad from (B+0x84, B+0x8c) to (B+0x9c, B+0xa4), height -B+8, colour by B+0x14.</summary>
        public void Render_af8640(FxRuntime rt, uint matrix)
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                short alpha = M.I16(b + 2);
                if (alpha <= 0) continue;
                float x = M.F32(b + 0x84), z = M.F32(b + 0x8c), h = -M.F32(b + 8);
                float x2 = M.F32(b + 0x9c), z2 = M.F32(b + 0xa4);
                float kind = M.F32(b + 0x14);
                byte r, g, bl;
                short blend;
                if (kind == 1f) { r = 0xff; g = 0x9b; bl = 0x9b; blend = 1; }
                else if (kind == 2f) { r = 0x9b; g = 0xff; bl = 0x9b; blend = 1; }
                else if (kind == 3f) { r = 0xff; g = 0xff; bl = 100; blend = 0; }
                else if (kind == 4f) { r = 0xff; g = 0x96; bl = 0x96; blend = 0; }
                else { r = 0xff; g = 0xff; bl = 0xff; blend = 0; }
                Quad_b0a900(rt, ref count, alpha, x, 0f, z, x2, 0f, z2, x2, h, z2, x, h, z,
                    matrix, blend, 0, 0, r, g, bl, 0f, 1f, 0f, 1f, 0);
            }
        }

        // ---------------------------------------------------------------- fn_af9f40 (0x36)

        /// <summary>CEffectPrim::fn_af9f40 — per block two camera-facing fan slices (±3° half alpha white, ±5° blue) around spin B+0x7e.</summary>
        public void Render_af9f40(FxRuntime rt, uint matrix)
        {
            int count = 0;
            Span<float> p = stackalloc float[12];
            p.Clear();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                short alpha = M.I16(b + 2);
                if (alpha <= 0) continue;

                int d = M.I16(b + 0x7e);
                float r = M.F32(b + 0x80);
                int i1 = (d - 3 >= 0 ? -3 : 0x165) + d;
                int i2 = (d < 0x165 ? 3 : -0x165) + d;
                p.Clear();
                p[3] = r * CosAt(i1); p[5] = r * SinAt(i1);
                p[6] = r * CosAt(i2); p[8] = r * SinAt(i2);
                Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                Quad_b0a900(rt, ref count, (short)FxOps.Cvtt((float)alpha * 0.5f),
                    p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 0, 0, 0, 0xff, 0xff, 0xff, 0f, 1f, 0f, 1f, 0);

                d = M.I16(b + 0x7e);
                r = M.F32(b + 0x80);
                i1 = (d - 5 >= 0 ? -5 : 0x163) + d;
                i2 = (d + 5 < 0x168 ? 5 : -0x163) + d;
                p.Clear();
                p[3] = r * CosAt(i1); p[5] = r * SinAt(i1);
                p[6] = r * CosAt(i2); p[8] = r * SinAt(i2);
                Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                Quad_b0a900(rt, ref count, M.I16(b + 2),
                    p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 0, 0, 0, 0x82, 0x82, 0xff, 0f, 1f, 0f, 1f, 0);
            }
        }

        // ---------------------------------------------------------------- fn_b02b70 (0x2b)

        /// <summary>
        /// CEffectPrim::fn_b02b70 — per block a tilted ring quad: offset (B+0x80 rotated by B+6 / B+0x7e), square of
        /// radius B+0x18 turned by B+0x10 and tilted by (int)B+0x1c; kind B+0x68 == 2 makes it camera-facing instead.
        /// </summary>
        public void Render_b02b70(FxRuntime rt, uint matrix)
        {
            int count = 0;
            Span<float> q = stackalloc float[12];
            q.Clear();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                short alpha = M.I16(b + 2);
                if (alpha <= 0) continue;
                const float zero = 0f;
                int a6 = M.I16(b + 6), a10 = M.I16(b + 0x10), a7e = M.I16(b + 0x7e);
                float s80 = M.F32(b + 0x80);
                float c6 = CosAt(a6), s6 = SinAt(a6);
                float t0 = c6 * s80 - s6 * zero;
                float y0 = s6 * s80 + c6 * zero;
                float c7e = CosAt(a7e), s7e = SinAt(a7e);
                int a1c = FxOps.Cvtt(M.F32(b + 0x1c));
                float x0 = t0 * c7e - s7e * zero;
                float c1c = CosAt(a1c);
                float z0 = t0 * s7e + c7e * zero;
                float r = M.F32(b + 0x18);
                float rc = r * CosAt(a10);
                float rs = r * SinAt(a10);
                float s1c = SinAt(a1c);
                float sZero = s1c * zero;
                float cZero = c1c * zero;
                int i1 = a10 + 0x5a;
                if (i1 >= 0x168) i1 -= 0x168;
                int i2 = i1 + (i1 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int i3 = i2 + (i2 + 0x5a < 0x168 ? 0x5a : -0x10e);
                byte kind = M.U8(b + 0x68);
                if (kind == 2)
                {
                    int j1 = a10 + 0x5a < 0x168 ? a10 + 0x5a : a10 + 0x5a - 0x168;
                    int j2 = j1 + (j1 + 0x5a < 0x168 ? 0x5a : -0x10e);
                    int j3 = j2 + (j2 + 0x5a < 0x168 ? 0x5a : -0x10e);
                    q[0] = rc; q[1] = 0f; q[2] = rs;
                    q[3] = r * CosAt(j1); q[4] = 0f; q[5] = r * SinAt(j1);
                    q[6] = r * CosAt(j2); q[7] = 0f; q[8] = r * SinAt(j2);
                    q[9] = r * CosAt(j3); q[10] = 0f; q[11] = r * SinAt(j3);
                    Rotate4_b0d9c0(q, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                }
                else
                {
                    float c1 = r * CosAt(i1), c2 = r * CosAt(i2), c3 = r * CosAt(i3);
                    q[0] = c1c * rc - sZero; q[1] = rs; q[2] = s1c * rc + cZero;
                    q[3] = c1c * c1 - sZero; q[4] = r * SinAt(i1); q[5] = s1c * c1 + cZero;
                    q[6] = c1c * c2 - sZero; q[7] = r * SinAt(i2); q[8] = s1c * c2 + cZero;
                    q[9] = c1c * c3 - sZero; q[10] = r * SinAt(i3); q[11] = s1c * c3 + cZero;
                }
                float px = M.F32(b + 0x84), py = M.F32(b + 0x88), pz = M.F32(b + 0x8c);
                for (int k = 0; k < 4; k++)
                {
                    q[k * 3] = (x0 + q[k * 3]) + px;
                    q[k * 3 + 1] = (y0 + q[k * 3 + 1]) + py;
                    q[k * 3 + 2] = (z0 + q[k * 3 + 2]) + pz;
                }
                byte cr, cg, cb, mode = 0;
                switch (kind)
                {
                    case 0: cr = 100; cg = 0x96; cb = 0xff; break;
                    case 1: cr = 0xfa; cg = 0xfa; cb = 0x9b; break;
                    case 2: cr = 0xfa; cg = 100; cb = 100; break;
                    case 3: cr = 0x32; cg = 0x32; cb = 0xff; break;
                    case 4: cr = 0xff; cg = 0x32; cb = 0x32; break;
                    case 5: cr = 0x32; cg = 0xff; cb = 0x32; break;
                    case 6: cr = 0xfa; cg = 100; cb = 100; mode = 1; break;
                    case 7: cr = 5; cg = 5; cb = 0xff; mode = 1; break;
                    default: continue;
                }
                Quad_b0a900(rt, ref count, alpha, q[0], q[1], q[2], q[3], q[4], q[5], q[6], q[7], q[8], q[9], q[10], q[11],
                    matrix, 0, 0, 1, cr, cg, cb, 0f, 1f, 0f, 1f, mode);
            }
        }

        // ---------------------------------------------------------------- fn_af0050 (0x22)

        /// <summary>
        /// CEffectPrim::fn_af0050 — per block a ring of up to 20 ribbon segments: inner points on radius B+0x80 at
        /// B+0x10 + k * (count B+6 / 9), outer points lifted by B+0x14[k] along spin B+0x7e; offsets (char) B+0x68/B+0x69.
        /// Keeps the previous inner / outer points in B+0x84.. / B+0x90.. (written to the prim).
        /// </summary>
        public void Render_af0050(FxRuntime rt, uint matrix)
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int n = M.I16(b + 6);
                int step = n / 9;
                int j = 0, acc = 0;
                if (n <= 0) continue;
                ushort nWord = M.U16(b + 6);
                do
                {
                    int ang = (M.I16(b + 0x10) + acc) % 0x168;
                    if (nWord == 0x168 && j == 0x14) ang = M.I16(b + 0x10);
                    float big = M.F32(b + 0x80);
                    float ic = big * CosAt(ang);
                    float isn = big * SinAt(ang);
                    int sp = M.I16(b + 0x7e);
                    M.F32(b + 0x88, 0f);
                    M.F32(b + 0x84, ic);
                    M.F32(b + 0x8c, isn);
                    float rad = M.F32(b + 0x14 + (uint)j * 4);
                    float oc = rad * CosAt(sp);
                    float os = rad * SinAt(sp);
                    int e = i == 2 ? 0x5a - ang : i == 1 ? 0x10e - ang : i == 0 ? 0x1c2 - ang : 0x276 - ang;
                    if (e >= 0x168) e -= 0x168;
                    float ny = -os;
                    float oz = isn + oc * SinAt(e);
                    float ox = oc * CosAt(e) + ic;
                    M.F32(b + 0x94, ny);
                    M.F32(b + 0x98, oz);
                    M.F32(b + 0x90, ox);
                    float offX = (float)(sbyte)M.U8(b + 0x68);
                    float offZ = (float)(sbyte)M.U8(b + 0x69);
                    if (j > 0)
                    {
                        // A = previous inner, B = inner, C = outer, D = previous outer (all + (offX, 0, offZ))
                        Strip_b0b320(rt, ref count, j - 1, M.I16(b + 2),
                            offX + M.F32(b + 0x9c), M.F32(b + 0xa0), offZ + M.F32(b + 0xa4),
                            offX + ic, 0f, isn + offZ,
                            offX + ox, ny, offZ + oz,
                            offX + M.F32(b + 0xa8), M.F32(b + 0xac), offZ + M.F32(b + 0xb0),
                            matrix, 0);
                    }
                    j++;
                    if (j >= 0x15) j = 0x14;
                    acc += step;
                    M.U32(b + 0xa8, M.U32(b + 0x90));
                    M.U32(b + 0xac, M.U32(b + 0x94));
                    M.U32(b + 0xb0, M.U32(b + 0x98));
                    M.U32(b + 0x9c, M.U32(b + 0x84));
                    M.U32(b + 0xa0, M.U32(b + 0x88));
                    M.U32(b + 0xa4, M.U32(b + 0x8c));
                } while (acc < M.I16(b + 6));
            }
        }
    }
}
