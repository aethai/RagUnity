// Synced from reference/port/src/RoClassicFx/Prim/prims-1/Prim.Prims1.Render2.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>CEffectPrim::Render cases of the prims-1 types (ribbons, rings, bolts, bubbles).</summary>
    public readonly partial struct Prim
    {
        /// <summary>
        /// 0.0f the JIT cannot constant-fold: the client adds +0.0 in places (x + 0.0 turns -0 into +0), which a
        /// compiler folding "x + 0f" to "x" would lose.
        /// </summary>
        static float ZeroP1 = 0f;

        // ---------------------------------------------------------------- fn_b04090 (0x49)

        /// <summary>
        /// CEffectPrim::fn_b04090 — per block a ring of ribbon segments (count B+6 / 20 degrees apart from B+0x10)
        /// at height -3, outer edge lifted by B+8 along spin B+0x7e; previous points kept in B+0x9c.. / B+0xa8..
        /// Stops starting new blocks once 256 faces were emitted.
        /// </summary>
        public void Render_b04090(FxRuntime rt, uint matrix)
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if (count >= 0x100) continue;
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int n = M.I16(b + 6);
                int step = n / 0x14;
                int j = 0, acc = 0;
                if (n < 0) continue;
                ushort nWord = M.U16(b + 6);
                do
                {
                    int bas = M.I16(b + 0x10);
                    int ang = bas + acc;
                    if (ang >= 0x168) ang -= 0x168;
                    if (nWord == 0x168 && j == 0x14) ang = bas;
                    float c = CosAt(ang), s = SinAt(ang);
                    float big = M.F32(b + 0x80);
                    int sp = M.I16(b + 0x7e);
                    float lift = M.F32(b + 8);
                    float ix = big * c;
                    M.F32(b + 0x88, -3f);
                    float iz = big * s;
                    M.F32(b + 0x84, ix);
                    M.F32(b + 0x8c, iz);
                    float t = lift * CosAt(sp);
                    float u = lift * SinAt(sp);
                    float ox = t * c;
                    float oz = t * s + iz;
                    u = u + 3f;
                    ox = ox + ix;
                    M.F32(b + 0x98, oz);
                    float oy = -u;
                    M.F32(b + 0x90, ox);
                    M.F32(b + 0x94, oy);
                    float iy = -3f;
                    if (j > 0)
                    {
                        short alpha = M.I16(b + 2);
                        if (alpha > 0)
                            Strip_b0b320(rt, ref count, j - 1, alpha,
                                M.F32(b + 0x9c), M.F32(b + 0xa0), M.F32(b + 0xa4),
                                M.F32(b + 0x84), M.F32(b + 0x88), M.F32(b + 0x8c),
                                M.F32(b + 0x90), M.F32(b + 0x94), M.F32(b + 0x98),
                                M.F32(b + 0xa8), M.F32(b + 0xac), M.F32(b + 0xb0), matrix, 0);
                    }
                    j++;
                    if (j >= 0x15) j = 0x14;
                    M.F32(b + 0x9c, ix);
                    M.F32(b + 0xa0, iy);
                    M.F32(b + 0xa4, iz);
                    M.F32(b + 0xa8, ox);
                    M.F32(b + 0xac, oy);
                    M.F32(b + 0xb0, oz);
                    acc += step;
                    nWord = M.U16(b + 6);
                } while (acc <= M.I16(b + 6));
            }
        }

        // ---------------------------------------------------------------- fn_b0cc40 (0x3e)

        /// <summary>
        /// CEffectPrim::fn_b0cc40 — per block a ring of ribbon segments (count B+6 / 21 degrees apart from B+0x10)
        /// hanging at -B+8, outer edge B+0x14[k] along spin B+0x7e; previous points kept in B+0x9c.. / B+0xa8..
        /// </summary>
        public void Render_b0cc40(FxRuntime rt, uint matrix)
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int n = M.I16(b + 6);
                int step = n / 0x15;
                int j = 0, acc = 0;
                if (n <= 0) continue;
                ushort nWord = M.U16(b + 6);
                do
                {
                    int ang = (M.I16(b + 0x10) + acc) % 0x168;
                    if (nWord == 0x168 && j == 0x14) ang = M.I16(b + 0x10);
                    float c = CosAt(ang), s = SinAt(ang);
                    float big = M.F32(b + 0x80);
                    float h = M.F32(b + 8);
                    float ix = big * c;
                    float iz = big * s;
                    float iy = -h;
                    int sp = M.I16(b + 0x7e);
                    M.F32(b + 0x88, iy);
                    M.F32(b + 0x84, ix);
                    M.F32(b + 0x8c, iz);
                    float rad = M.F32(b + 0x14 + (uint)j * 4);
                    float u = rad * SinAt(sp);
                    float t = rad * CosAt(sp);
                    float oy = h + u;
                    float ox = t * c;
                    float oz = t * s;
                    oy = -oy;
                    oz = oz + iz;
                    M.F32(b + 0x94, oy);
                    ox = ox + ix;
                    M.F32(b + 0x98, oz);
                    M.F32(b + 0x90, ox);
                    if (j > 0)
                        Strip_b0b320(rt, ref count, j - 1, M.I16(b + 2),
                            M.F32(b + 0x9c), M.F32(b + 0xa0), M.F32(b + 0xa4),
                            M.F32(b + 0x84), M.F32(b + 0x88), M.F32(b + 0x8c),
                            M.F32(b + 0x90), M.F32(b + 0x94), M.F32(b + 0x98),
                            M.F32(b + 0xa8), M.F32(b + 0xac), M.F32(b + 0xb0), matrix, 0);
                    j++;
                    if (j >= 0x15) j = 0x14;
                    M.F32(b + 0x9c, ix);
                    M.F32(b + 0xa0, iy);
                    M.F32(b + 0xa4, iz);
                    M.F32(b + 0xa8, ox);
                    M.F32(b + 0xac, oy);
                    M.F32(b + 0xb0, oz);
                    acc += step;
                    nWord = M.U16(b + 6);
                } while (acc < M.I16(b + 6));
            }
        }

        // ---------------------------------------------------------------- fn_b0c250 (0x45)

        /// <summary>
        /// CEffectPrim::fn_b0c250 — per block two pentagon rings (radius B+8, then B+8 doubled / +0.25) sweeping
        /// between the centres B+0x84.. and B+0x90.., turned by B+0x10 + 90. Colours by kind B+0x24. The previous
        /// ring points carry over between rings and blocks (function locals, 0 at the start).
        /// </summary>
        public void Render_b0c250(FxRuntime rt, uint matrix)
        {
            int count = 0;
            float pix = 0f, piy = 0f, piz = 0f, pox = 0f, poy = 0f, poz = 0f;
            float r = 0f;
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0) continue;
                int s2 = (short)((M.I16(b + 0x10) + 0x5a) % 0x168);
                for (int ring = 0; ring < 2; ring++)
                {
                    for (int k = 0, off = 0; off <= 0x5a0; k++, off += 0x120)
                    {
                        if (ring == 0)
                        {
                            r = M.F32(b + 8);
                        }
                        else
                        {
                            float r0 = M.F32(b + 8);
                            r = M.F32(b + 0x24) == 3f ? r0 + r0 : r0 + 0.25f;
                        }
                        float t = r * TrigAtByte(CosTableVa, off);
                        float u = r * TrigAtByte(SinTableVa, off);
                        float tz = t * SinAt(s2);
                        float tx = t * CosAt(s2);
                        u = u - M.F32(b + 0x14);
                        float ix = tx + M.F32(b + 0x84), ox = tx + M.F32(b + 0x90);
                        float iy = u + M.F32(b + 0x88), oy = u + M.F32(b + 0x94);
                        float iz = tz + M.F32(b + 0x8c), oz = tz + M.F32(b + 0x98);
                        if (k > 0)
                        {
                            float kind = M.F32(b + 0x24);
                            short alpha = M.I16(b + 2);
                            byte cr, cg, cb, mode;
                            if (kind == 2f) { cr = 0xff; cg = 0xff; cb = 0xff; mode = 0; }
                            else if (kind == 3f) { cr = 0xff; cg = 0x59; cb = 0xb6; mode = 0; }
                            else if (kind == 4f) { cr = 0x37; cg = 0x37; cb = 0xff; mode = 1; }
                            else if (kind == 5f) { cr = 0x37; cg = 0x37; cb = 0x37; mode = 1; }
                            else if (ring == 0) { cr = 0xff; cg = 0xff; cb = 0x37; mode = 0; }
                            else { cr = 0xff; cg = 0xff; cb = 0xc8; mode = 0; }
                            Quad_b0a900(rt, ref count, alpha, pix, piy, piz, ix, iy, iz, ox, oy, oz, pox, poy, poz,
                                matrix, (short)(ring == 0 ? 1 : 0), 0, 1, cr, cg, cb, 0f, 1f, 0f, 1f, mode);
                        }
                        pix = ix; piy = iy; piz = iz;
                        pox = ox; poy = oy; poz = oz;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- fn_b07540 (0x77 / 0x7a / 0x7d)

        /// <summary>CEffectPrim::fn_b07540 — per block a flat spinning square (radius B+0x80, spin B+0x7e) at B+0x84.., colour by B+0x6b.</summary>
        public void Render_b07540(FxRuntime rt, uint matrix)
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                short alpha = M.I16(b + 2);
                if (alpha <= 0) continue;
                int sp = M.I16(b + 0x7e);
                float big = M.F32(b + 0x80);
                float px = M.F32(b + 0x84), pz = M.F32(b + 0x8c);
                float y = M.F32(b + 0x88) + ZeroP1;
                int i0 = sp + (sp < 0x10e ? 0x5a : -0x10e);
                int i1 = i0 + (i0 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int i2 = i1 + (i1 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int i3 = i2 + (i2 + 0x5a < 0x168 ? 0x5a : -0x10e);
                float x0 = big * CosAt(i0) + px, z0 = big * SinAt(i0) + pz;
                float x1 = big * CosAt(i1) + px, z1 = big * SinAt(i1) + pz;
                float x2 = big * CosAt(i2) + px, z2 = big * SinAt(i2) + pz;
                float x3 = big * CosAt(i3) + px, z3 = big * SinAt(i3) + pz;
                byte cr, cg, cb;
                short blend;
                switch (M.U8(b + 0x6b))
                {
                    case 0: cr = 0x50; cg = 0x50; cb = 0xff; blend = 0; break;
                    case 1: cr = 0x50; cg = 0xff; cb = 0x50; blend = 0; break;
                    case 2: cr = 0xff; cg = 0xff; cb = 0xff; blend = 1; break;
                    case 3: cr = 0x46; cg = 0x46; cb = 0x46; blend = 1; break;
                    default: continue;
                }
                Quad_b0a900(rt, ref count, alpha, x0, y, z0, x1, y, z1, x2, y, z2, x3, y, z3,
                    matrix, blend, 0, 1, cr, cg, cb, 0f, 1f, 0f, 1f, 0);
            }
        }

        // ---------------------------------------------------------------- fn_b08480 (0x6d)

        /// <summary>
        /// CEffectPrim::fn_b08480 — per block a camera-facing square (radius B+0x80) offset by B+0x9c toward the
        /// camera and B+0xa0 up; drawn once it is at or below the ground, and while B+0xc &lt; 180 it spawns effect
        /// 0x1fa on the target actor (f_3c as AID; the local player when it matches) with the offset as msg 0xe.
        /// </summary>
        public void Render_b08480(FxRuntime rt, uint matrix)
        {
            int count = 0;
            Span<float> p = stackalloc float[12];
            p.Clear();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                float big = M.F32(b + 0x80);
                p[0] = big * CosAt(45); p[1] = 0f; p[2] = big * SinAt(45);
                p[3] = big * CosAt(135); p[4] = 0f; p[5] = big * SinAt(135);
                p[6] = big * CosAt(225); p[7] = 0f; p[8] = big * SinAt(225);
                p[9] = big * CosAt(315); p[10] = 0f; p[11] = big * SinAt(315);
                short a = M.I16(A + 0x3ec);
                Rotate4_b0d9c0(p, a, M.I16(A + 0x4a0));
                float d = M.F32(b + 0x9c), up = M.F32(b + 0xa0);
                float dc = d * CosAt(a), ds = d * SinAt(a);
                for (int k = 0; k < 4; k++)
                {
                    p[k * 3] = dc + p[k * 3];
                    p[k * 3 + 1] = up + p[k * 3 + 1];
                    p[k * 3 + 2] = ds + p[k * 3 + 2];
                }
                int alpha = FxOps.Cvtt(M.F32(b + 0x48));
                if (!(p[1] <= 0f)) continue;   // COMISS 0, y; JC skip (also skips NaN)
                if (M.I32(b + 0xc) < 0xb4)
                {
                    uint effect = 0;
                    if ((float)(double)rt.PlayerAid == M.F32(b + 0x3c))
                    {
                        effect = rt.LaunchEffectOn(M.U32(M.U32(rt.Mode + 0xcc) + 0x2c), 0x1fa);
                    }
                    else
                    {
                        uint actor = rt.FindActorByAid(FxOps.FloatToUInt_d48d60(M.F32(b + 0x3c)));
                        if (actor != 0) effect = rt.LaunchEffectOn(actor, 0x1fa);
                    }
                    // CRagEffect vf16 (0xb5e650) msg 0xe: the effect's offset vector +0x180..+0x188 = (dc, up, ds)
                    if (effect != 0)
                    {
                        M.F32(effect + 0x180, dc);
                        M.F32(effect + 0x184, up);
                        M.F32(effect + 0x188, ds);
                    }
                }
                Quad_b0a900(rt, ref count, (short)alpha, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 0, 0, 0, 0x7d, 0x7d, 0xff, 0f, 1f, 0f, 1f, 0);
            }
        }

        // ---------------------------------------------------------------- fn_afd940 (0x66)

        /// <summary>
        /// CEffectPrim::fn_afd940 — per block a jittered lightning-like chain of up to 20 segments along heading
        /// B+0x7e (segment length B+0x80 / 5, wobble sin(B+0x14[k]) * 15 * B+0x90, jitter ±0.5 from rand()), each
        /// segment 2..19 wrapped in a 4-sided tube (radius B+8) with random red/green. rand() is drawn for every
        /// block with a positive segment count, active or not.
        /// </summary>
        public void Render_afd940(FxRuntime rt, uint matrix)
        {
            int count = 0;
            // the client's stack arrays (21 entries; one extra slot so the never-rendered read past the end is harmless)
            Span<float> ra = stackalloc float[22];
            Span<float> rb = stackalloc float[22];
            Span<float> rc = stackalloc float[22];
            ra.Clear(); rb.Clear(); rc.Clear();
            float pq0x = 0f, pq0y = 0f, pq0z = 0f, pq1x = 0f, pq1y = 0f, pq1z = 0f;
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                float n = M.F32(b + 0x94);
                if (n > 0f)
                {
                    int s = 0;
                    do
                    {
                        int v = rt.Rand.Next() % 0xb;
                        ra[s] = (float)(v >= 0 ? v : 0) * 0.1f - 0.5f;
                        v = rt.Rand.Next() % 0xb;
                        rb[s] = (float)(v >= 0 ? v : 0) * 0.1f - 0.5f;
                        v = rt.Rand.Next() % 0xb;
                        n = M.F32(b + 0x94);
                        rc[s] = (float)(v >= 0 ? v : 0) * 0.1f - 0.5f;
                        s++;
                    } while (n > (float)s);
                }
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0 || M.I32(b + 0xc) <= 0 || !(n > 0f)) continue;

                int seg = 0;
                int fade = 0x96, rise = 0;
                uint ptr = b + 0x18;
                float fseg = 0f;
                int seg1;
                do
                {
                    float f11 = 0f;
                    if (seg > 0) f11 = (fseg / 5f) * M.F32(b + 0x80) + ZeroP1;
                    int head = M.I16(b + 0x7e);
                    float wave = M.F32(b + 0x90);
                    seg1 = seg + 1;
                    float ch = CosAt(head), sh = SinAt(head);
                    float z0 = sh * f11 + rc[seg];
                    float y0 = (ra[seg] - 8f) - SinAt((short)FxOps.Cvtt(M.F32(ptr - 4))) * 15f * wave;
                    float x0 = ch * f11 + rb[seg];
                    float f12 = 0f;
                    if (seg1 > 0) f12 = ((float)seg1 / 5f) * M.F32(b + 0x80) + ZeroP1;
                    float y1 = (ra[seg + 1] - 8f) - SinAt((short)FxOps.Cvtt(M.F32(ptr))) * 15f * wave;
                    float x1 = ch * f12 + rb[seg + 1];
                    float z1 = sh * f12 + rc[seg + 1];
                    short dir = Direction_ad5710(x0 - x1, z0 - z1);
                    float px = M.F32(b + 0x84), py = M.F32(b + 0x88), pz = M.F32(b + 0x8c);
                    y0 = py + y0;
                    x0 = px + x0;
                    x1 = px + x1;
                    y1 = py + y1;
                    z0 = pz + z0;
                    z1 = pz + z1;
                    short d2 = (short)(dir + 0x5a);
                    if (d2 >= 0x168) d2 = (short)(d2 - 0x168);
                    if ((uint)(seg1 - 2) <= 0x11)
                    {
                        int d2off = d2 * 4;
                        for (int k = 0, off = 0; off <= 0x5a0; k++, off += 0x1e0)
                        {
                            float rad = M.F32(b + 8);
                            float wc = rad * TrigAtByte(CosTableVa, off);
                            float ws = rad * TrigAtByte(SinTableVa, off);
                            float wcs = wc * TrigAtByte(SinTableVa, d2off);
                            float wcc = wc * TrigAtByte(CosTableVa, d2off);
                            float q1x = wcc + x1, q0x = wcc + x0;
                            float q0y = ws + y0;
                            float q1z = wcs + z1, q0z = wcs + z0;
                            float q1y = y1 + ws;
                            if (k > 0)
                            {
                                int add = fade > 0 ? rise - 0x87 : fade;
                                int a2 = M.I16(b + 2) + add;
                                if (a2 > 0)
                                {
                                    int r1 = rt.Rand.Next() % 0x79;
                                    int r2 = rt.Rand.Next() % 0x79;
                                    byte g = r1 >= 0 ? (byte)r1 : (byte)0;
                                    byte r = r2 >= 0 ? (byte)r2 : (byte)0;
                                    Quad_b0a900(rt, ref count, (short)a2, q0x, q0y, q0z, q1x, q1y, q1z, pq1x, pq1y, pq1z, pq0x, pq0y, pq0z,
                                        matrix, 0, 0, 1, r, g, 0xff, 0f, 1f, 0f, 1f, 0);
                                }
                            }
                            pq0x = q0x; pq0y = q0y; pq0z = q0z;
                            pq1x = q1x; pq1y = q1y; pq1z = q1z;
                        }
                    }
                    fade -= 0xf;
                    ptr += 4;
                    rise += 0xf;
                    seg = seg1;
                    fseg = (float)seg1;
                } while (M.F32(b + 0x94) > (float)seg1);
            }
        }

        // ---------------------------------------------------------------- fn_b023e0 (0x46)

        /// <summary>Sum of 2r * 0.7^t for t = 0..n-1 (sequential, as the client's unrolled loop) and the final power.</summary>
        static float DecaySum_b023e0(float r, int n, ref float p)
        {
            float acc = 0f;
            if (n <= 0) return acc;
            float twoR = r + r;
            for (int t = 0; t < n; t++)
            {
                acc = twoR * p + acc;
                p = p * 0.7f;
            }
            return acc;
        }

        static float DecayPower_b023e0(int n)
        {
            float q = 1f;
            for (int t = 0; t < n; t++) q = q * 0.7f;
            return q;
        }

        /// <summary>
        /// CEffectPrim::fn_b023e0 — per block 20 wave segments: reach = decaying sums of 2 * B+0x80 over the halving
        /// counts (char) B+0x68[k] / B+0x69[k] plus the phase B+0x14[k] / 90, bob -1.5 * B+0x80 * sin(phase) * 0.7^n;
        /// each segment wrapped in a 4-sided tube (radius B+8), alpha B+2 - 5k, colour by kind B+4.
        /// </summary>
        public void Render_b023e0(FxRuntime rt, uint matrix)
        {
            int count = 0;
            float pq1x = 0f, pq1y = 0f, pq1z = 0f, pq2x = 0f, pq2y = 0f, pq2z = 0f;
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0 || M.I32(b + 0xc) <= 0) continue;
                for (int k = 0; k * 5 < 100; k++)
                {
                    int n1 = (sbyte)M.U8(b + 0x68 + (uint)k);
                    float p = 1f;
                    float acc = DecaySum_b023e0(M.F32(b + 0x80), n1, ref p);
                    float ang1 = M.F32(b + 0x14 + (uint)k * 4);
                    float big = M.F32(b + 0x80);
                    if (ang1 > 0f) acc = acc + big * p * (ang1 / 90f);
                    float negBig = -big;
                    float h1 = negBig * SinAt((short)FxOps.Cvtt(ang1)) * 1.5f;
                    h1 = h1 * DecayPower_b023e0(n1);
                    int head = M.I16(b + 0x7e);
                    float c = CosAt(head), s = SinAt(head);
                    float x1 = c * acc;
                    float z1 = s * acc;

                    int n2 = (sbyte)M.U8(b + 0x69 + (uint)k);
                    float p2 = 1f;
                    float acc2 = DecaySum_b023e0(M.F32(b + 0x80), n2, ref p2);
                    float ang2 = M.F32(b + 0x18 + (uint)k * 4);
                    if (ang2 > 0f) acc2 = acc2 + big * p2 * (ang2 / 90f);
                    float h2 = negBig * SinAt((short)FxOps.Cvtt(ang2)) * 1.5f;
                    h2 = h2 * DecayPower_b023e0(n2);
                    float x2 = c * acc2;
                    float z2 = s * acc2;
                    short dir = Direction_ad5710(x1 - x2, z1 - z2);

                    float px = M.F32(b + 0x84), py = M.F32(b + 0x88), pz = M.F32(b + 0x8c);
                    float y1p = h1 + py, z1p = z1 + pz, y2p = h2 + py, z2p = z2 + pz;
                    float x1p = px + x1, x2p = px + x2;
                    int d2off = (short)((dir + 0x5a) % 0x168) * 4;
                    for (int j = 0, off = 0; off <= 0x5a0; j++, off += 0x1e0)
                    {
                        float w = M.F32(b + 8);
                        float wc = w * TrigAtByte(CosTableVa, off);
                        float ws = w * TrigAtByte(SinTableVa, off);
                        float wcs = wc * TrigAtByte(SinTableVa, d2off);
                        float wcc = wc * TrigAtByte(CosTableVa, d2off);
                        float q1x = x1p + wcc, q1y = y1p + ws, q1z = z1p + wcs;
                        float q2x = x2p + wcc, q2y = y2p + ws, q2z = z2p + wcs;
                        if (j > 0)
                        {
                            int alpha = M.I16(b + 2) - k * 5;
                            if (alpha > 0)
                            {
                                ushort kind = M.U16(b + 4);
                                byte v = (byte)(k * 7);
                                byte cr, cg, cb;
                                if (kind == 2)
                                {
                                    v += 0x3c;
                                    cr = 0xff; cg = v; cb = v;
                                }
                                else
                                {
                                    v += 0xa;
                                    if (kind == 8) { cr = v; cg = v; cb = 0xff; }
                                    else { cr = 0xff; cg = 0xc8; cb = v; }
                                }
                                Quad_b0a900(rt, ref count, (short)alpha, pq1x, pq1y, pq1z, q1x, q1y, q1z, q2x, q2y, q2z, pq2x, pq2y, pq2z,
                                    matrix, 0, 0, 1, cr, cg, cb, 0f, 1f, 0f, 1f, 0);
                            }
                        }
                        pq1x = q1x; pq1y = q1y; pq1z = q1z;
                        pq2x = q2x; pq2y = q2y; pq2z = q2z;
                    }
                }
            }
        }
    }
}
