// Synced from reference/port/src/RoClassicFx/Prim/prims-3/Prim.Prims3.QuadRender.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>prims-3: particle renderers built on fn_b0a900 quads.</summary>
    public readonly partial struct Prim
    {
        [FxPrimRender(0x27, 0x58)]
        public void Render27(FxRuntime rt, uint viewMatrix) => Render_aee0b0(rt, viewMatrix);

        [FxPrimRender(0x25)]
        public void Render25(FxRuntime rt, uint viewMatrix) => Render_aff310(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_aff310 (0xaff310) — type 0x25: per particle (alpha &gt; 0) a box: square of half-size
        /// (+0x14, +0x18) rotated by the angle +0x10 at height −1, four additive side walls down to −1 − (+0x1c)
        /// and the top face twice (both windings) at half alpha. Corners are kept at +0x84.. (4 × xyz).
        /// </summary>
        public void Render_aff310(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0) continue;
                float x = M.F32(b + 0x14), z = M.F32(b + 0x18);
                int ang = M.I16(b + 0x10);
                float c = P3Cos(ang), s = P3Sin(ang);
                float nz = -z, nx = -x;
                float xc = x * c, zs = z * s, zc = z * c, xs = x * s;
                uint p0 = b + 0x9c, p1 = b + 0x84, p2 = b + 0x90, p3 = b + 0xa8;
                M.F32(p0 + 4, -1.0f);
                float p0x = xc - zs;
                M.F32(p0, p0x);
                M.F32(p1 + 4, -1.0f);
                M.F32(p2 + 4, -1.0f);
                M.F32(p3 + 4, -1.0f);
                float p0z = zc + xs;
                M.F32(p0 + 8, p0z);
                float nzc = nz * c, nzs = nz * s;
                float p1x = xc - nzs;
                M.F32(p1, p1x);
                float p1z = nzc + xs;
                M.F32(p1 + 8, p1z);
                float nxs = nx * s, nxc = nx * c;
                float p2x = nxc - nzs;
                M.F32(p2, p2x);
                float p2z = nzc + nxs;
                M.F32(p2 + 8, p2z);
                float p3z = nxs + zc;
                M.F32(p3 + 8, p3z);
                float p3x = nxc - zs;
                M.F32(p3, p3x);
                float yb = -1.0f - M.F32(b + 0x1c);

                short alpha = M.I16(b + 2);
                P3QuadXyz(rt, ref cnt, alpha, M.F32(p0), M.F32(p0 + 4), M.F32(p0 + 8), M.F32(p1), M.F32(p1 + 4), M.F32(p1 + 8),
                    p1x, yb, p1z, p0x, yb, p0z, matrix, 1);
                P3QuadXyz(rt, ref cnt, M.I16(b + 2), M.F32(p1), M.F32(p1 + 4), M.F32(p1 + 8), M.F32(p2), M.F32(p2 + 4), M.F32(p2 + 8),
                    p2x, yb, p2z, p1x, yb, p1z, matrix, 1);
                P3QuadXyz(rt, ref cnt, M.I16(b + 2), M.F32(p2), M.F32(p2 + 4), M.F32(p2 + 8), M.F32(p3), M.F32(p3 + 4), M.F32(p3 + 8),
                    p3x, yb, p3z, p2x, yb, p2z, matrix, 1);
                P3QuadXyz(rt, ref cnt, M.I16(b + 2), M.F32(p3), M.F32(p3 + 4), M.F32(p3 + 8), M.F32(p0), M.F32(p0 + 4), M.F32(p0 + 8),
                    p0x, yb, p0z, p3x, yb, p3z, matrix, 1);
                short half = (short)(M.I16(b + 2) / 2);
                P3QuadXyz(rt, ref cnt, half, M.F32(p0), M.F32(p0 + 4), M.F32(p0 + 8), M.F32(p1), M.F32(p1 + 4), M.F32(p1 + 8),
                    M.F32(p2), M.F32(p2 + 4), M.F32(p2 + 8), M.F32(p3), M.F32(p3 + 4), M.F32(p3 + 8), matrix, 0);
                P3QuadXyz(rt, ref cnt, half, M.F32(p3), M.F32(p3 + 4), M.F32(p3 + 8), M.F32(p2), M.F32(p2 + 4), M.F32(p2 + 8),
                    M.F32(p1), M.F32(p1 + 4), M.F32(p1 + 8), M.F32(p0), M.F32(p0 + 4), M.F32(p0 + 8), matrix, 0);
            }
        }

        [FxPrimRender(0x26)]
        public void Render26(FxRuntime rt, uint viewMatrix) => Render_b03a80(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b03a80 (0xb03a80) — type 0x26: a beam from the source (+0x478) to the (eased) target
        /// (+0x490): two rings (radius f_3f0, 2·f_3f0) of 10 segments perpendicular to the beam direction,
        /// each segment a world-space quad from the source ring to the target ring. Colours by f_40c
        /// (2 / 3 / 4 / other). The direction angle is found by a linear search of the cos table (FUN_00534e70
        /// = x87 fsqrt). The "4.0" outer ring reuses the halved alpha left in EDI by the inner ring.
        /// </summary>
        public void Render_b03a80(FxRuntime rt, uint matrix)
        {
            if (M.U8(A + 0x3e8) != 1 || M.I16(A + 0x3ea) <= 0) return;
            float dx = M.F32(A + 0x478) - M.F32(A + 0x490);
            float dz = M.F32(A + 0x480) - M.F32(A + 0x498);
            float len = (float)Math.Sqrt((double)(dx * dx + dz * dz));
            float nz = dz / len;
            float nx = dx / len;
            int dir = 0xb4;
            for (short i = 0; i <= 0xb4; i++)
            {
                if (nx >= P3Cos(i))
                {
                    dir = P3Jb(nz, 0f) ? (ushort)(0x168 - i) : (ushort)i;
                    break;
                }
            }
            int a2 = (short)(dir + 0x5a) < 0x168 ? (short)(ushort)(dir + 0x5a) : (short)(ushort)(dir + 0x5a - 0x168);

            float tx, ty, tz;
            int cnt3f4 = M.I32(A + 0x3f4);
            if (cnt3f4 < 0xf)
            {
                float t = P3Sin(cnt3f4 * 6);
                float fx = M.F32(A + 0x478), fy = M.F32(A + 0x47c), fz = M.F32(A + 0x480);
                tx = (M.F32(A + 0x490) - fx) * t + fx;
                ty = (M.F32(A + 0x494) - fy) * t + fy;
                tz = (M.F32(A + 0x498) - fz) * t + fz;
            }
            else
            {
                tx = M.F32(A + 0x490);
                ty = M.F32(A + 0x494);
                tz = M.F32(A + 0x498);
            }

            int cnt = 0;
            float pax = 0f, pay = 0f, paz = 0f, pbx = 0f, pby = 0f, pbz = 0f;
            int edi = 0;
            for (int j = 0; j < 2; j++)
            {
                int count = 0;
                for (int ang = 0; ang <= 0x168; ang += 0x24)
                {
                    float r = j == 0 ? M.F32(A + 0x3f0) : M.F32(A + 0x3f0) + M.F32(A + 0x3f0);
                    float rc = r * P3Cos(ang);
                    float rs = r * P3Sin(ang);
                    float x = rc * P3Cos(a2);
                    float z = rc * P3Sin(a2);
                    float ax = x + M.F32(A + 0x478);
                    float ay = rs + M.F32(A + 0x47c);
                    float az = z + M.F32(A + 0x480);
                    float bz = z + tz;
                    float bx = tx + x;
                    float by = ty + rs;
                    if (count > 0)
                    {
                        float kind = M.F32(A + 0x40c);
                        short alphaS = M.I16(A + 0x3ea);
                        int alpha;
                        short blend = 0;
                        byte cr, cg, cb;
                        if (j == 0)
                        {
                            edi = alphaS;
                            alpha = (ushort)alphaS;
                            if (kind == 2.0f) { cr = 0; cg = 0x64; cb = 0x96; }
                            else if (kind == 3.0f) { cr = 0xff; cg = 0x59; cb = 0xb6; }
                            else if (kind == 4.0f) { cr = 0x32; cg = 0x32; cb = 0x32; blend = 1; edi = alphaS / 2; alpha = edi; }
                            else { cr = 0xff; cg = 0xff; cb = 0; }
                        }
                        else
                        {
                            if (kind == 2.0f) { edi = alphaS * 2; cr = 0xff; cg = 0x32; cb = 0; }
                            else if (kind == 3.0f) { edi = alphaS * 2; cr = 0xff; cg = 0x59; cb = 0xb6; }
                            else if (kind == 4.0f) { cr = 0x32; cg = 0x32; cb = 0x32; blend = 1; }
                            else { edi = alphaS * 2; cr = 0; cg = 0; cb = 0xff; }
                            alpha = edi;
                        }
                        Quad_b0a900(rt, ref cnt, (short)alpha, pax, pay, paz, ax, ay, az, bx, by, bz, pbx, pby, pbz,
                            matrix, blend, 0, 1, cr, cg, cb, 0f, 1.0f, 0f, 1.0f, 0);
                    }
                    count++;
                    pax = ax; pay = ay; paz = az;
                    pbx = bx; pby = by; pbz = bz;
                }
            }
        }

        [FxPrimRender(0x2c)]
        public void Render2C(FxRuntime rt, uint viewMatrix) => Render_b09eb0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b09eb0 (0xb09eb0) — type 0x2c: per particle (alpha &gt; 0) one quad: a strip of width
        /// 2·W (+0x18) at distance D ± n·W (D +0x80, n the signed byte +0x69) along the angle +6, tilted by +0x7e;
        /// colour/blend/mode by the byte +0x68.
        /// </summary>
        public void Render_b09eb0(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0) continue;
                float w = M.F32(b + 0x18);
                int a = M.I16(b + 6);
                float d = M.F32(b + 0x80);
                int t = M.I16(b + 0x7e);
                float sa = P3Sin(a), ca = P3Cos(a);
                float sW = sa * w;
                float nW = (float)M.I8(b + 0x69) * w;
                float cW = ca * w;
                float ct = P3Cos(t);
                float e1 = nW + d;
                float e2 = d - nW;
                float sE1 = sa * e1;
                float cE1 = ca * e1;
                float q1y = sE1 + cW;
                float cE2 = P3Cos(a) * e2;
                float f9 = cE1 - sW;
                float st0 = P3Sin(t) * 0f;
                float q1x = f9 * ct - st0;
                float ct0 = ct * 0f;
                float q1z = f9 * P3Sin(t) + ct0;
                float f11 = cE2 - sW;
                float sE2 = P3Sin(a) * e2;
                float st = P3Sin(t);
                float q2y = sE2 + cW;
                float q2x = f11 * ct - st0;
                float q2z = f11 * st + ct0;
                float negW = M.F32(b + 0x18) * -1.0f;
                float cNW = P3Cos(a) * negW;
                float sNW = P3Sin(a) * negW;
                float q3y = cNW + sE2;
                float g3 = cE2 - sNW;
                float g0 = cE1 - sNW;
                float q3z = g3 * st + ct0;
                float q3x = g3 * ct - st0;
                float q0y = cNW + sE1;
                float q0x = g0 * ct - st0;
                float q0z = g0 * st + ct0;

                byte kind = M.U8(b + 0x68);
                short blend = 0;
                byte cr, cg, cb, mode = 0;
                if (kind == 0) { cr = 0xe1; cg = 0xe1; cb = 0x50; }
                else if (kind == 1) { cr = 0xff; cg = 0xff; cb = 0xff; }
                else if (kind == 2) { cr = 0x41; cg = 0xb8; cb = 0xf0; mode = 1; }
                else { cr = 0xc8; cg = 0xc8; cb = 0xff; blend = 1; }
                Quad_b0a900(rt, ref cnt, M.I16(b + 2), q0x, q0y, q0z, q1x, q1y, q1z, q2x, q2y, q2z, q3x, q3y, q3z,
                    matrix, blend, 0, 0, cr, cg, cb, 0f, 1.0f, 0f, 1.0f, mode);
            }
        }

        [FxPrimRender(0x38)]
        public void Render38(FxRuntime rt, uint viewMatrix) => Render_af9950(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_af9950 (0xaf9950) — type 0x38: per particle (alpha &gt; 0) one ground quad (y = −6)
        /// along the direction +0x7e: a band [U, U+dU] (U +0x400, dU +0x404) of a track of length L (+0x468),
        /// wrapping around 100 (the far part mirrors by 180°), offset sideways by R0 (+0x3f0) / R1 (+0x3fc);
        /// colour set by the byte +0x450 (only 0..2, 10..12, 20..22, 30..32 draw).
        /// </summary>
        public void Render_af9950(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0) continue;
                int a = M.I16(b + 0x7e);
                float r1 = M.F32(b + 0x14);
                float r0 = M.F32(b + 8);
                int e1 = (a >= 0x5a ? -0x5a : 0x10e) + a;
                float t18 = r1 * P3Cos(e1);
                float t1c = r1 * P3Sin(e1);
                float p15 = r0 * P3Cos(e1) + t18;
                float p9 = r0 * P3Sin(e1) + t1c;
                float u = M.F32(b + 0x18);
                float l = M.F32(b + 0x80);
                float f13, c2, s2;
                if (100.0f > u)
                {
                    int i2 = a < 0xb4 ? a + 0xb4 : a - 0xb4;
                    c2 = P3Cos(i2);
                    s2 = P3Sin(i2);
                    f13 = l - u;
                }
                else
                {
                    f13 = u - l;
                    c2 = P3Cos(a);
                    s2 = P3Sin(a);
                }
                float u2 = u + M.F32(b + 0x1c);
                float f13c = f13 * c2;
                float f13s = f13 * s2;
                float v2x = p15 + f13c;
                float v2z = p9 + f13s;
                float f12, c3, s3;
                if (100.0f > u2)
                {
                    f12 = l - u2;
                    int i3 = (a >= 0xb4 ? -0xb4 : 0xb4) + a;
                    c3 = P3Cos(i3);
                    s3 = P3Sin(i3);
                }
                else
                {
                    c3 = P3Cos(a);
                    f12 = u2 - l;
                    s3 = P3Sin(a);
                }
                float f12c = f12 * c3;
                int e3 = (a < 0x10e ? 0x5a : -0x10e) + a;
                float f12s = f12 * s3;
                float v3x = p15 + f12c;
                float v3z = p9 + f12s;
                float pc = r0 * P3Cos(e3) + t18;
                float pz = r0 * P3Sin(e3) + t1c;
                float v1x = f13c + pc;
                float v0x = pc + f12c;
                float v1z = f13s + pz;
                float v0z = pz + f12s;

                byte kind = M.U8(b + 0x68);
                short blend = 1;
                byte cr, cg, cb, mode = 0;
                switch (kind)
                {
                    case 0: cr = 0xe1; cg = 0xe1; cb = 0xff; break;
                    case 1: cr = 0xcd; cg = 0xcd; cb = 0xff; break;
                    case 2: cr = 0xb9; cg = 0xb9; cb = 0xff; break;
                    case 10: cr = 0xff; cg = 0xff; cb = 0xe1; break;
                    case 11: cr = 0xff; cg = 0xff; cb = 0xcd; break;
                    case 12: cr = 0xff; cg = 0xff; cb = 0xb9; break;
                    case 20: cr = 0xe1; cg = 0xff; cb = 0xe1; break;
                    case 21: cr = 0xcd; cg = 0xff; cb = 0xcd; break;
                    case 22: cr = 0xb9; cg = 0xff; cb = 0xb9; break;
                    case 30: cr = 0x69; cg = 0x69; cb = 0xff; blend = 0; mode = 1; break;
                    case 31: cr = 0x41; cg = 0x41; cb = 0xff; blend = 0; mode = 1; break;
                    case 32: cr = 0x19; cg = 0x19; cb = 0xff; blend = 0; mode = 1; break;
                    default: continue;
                }
                const float y = -6.0f;
                Quad_b0a900(rt, ref cnt, M.I16(b + 2), v0x, y, v0z, v1x, y, v1z, v2x, y, v2z, v3x, y, v3z,
                    matrix, blend, 0, 0, cr, cg, cb, 0f, 1.0f, 0f, 1.0f, mode);
            }
        }

        [FxPrimRender(0x40)]
        public void Render40(FxRuntime rt, uint viewMatrix) => Render_b06720(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b06720 (0xb06720) — type 0x40: the fn_aff310 box walls (same corners at +0x84..)
        /// coloured by f_408 (0 white/normal, 1 green-ish, 2 grey + two extra diagonal walls at double alpha,
        /// 4 yellow with blue from +0x40c + +0x410). Other f_408 values reuse the colour/blend left in the
        /// frame by the previous particle (uninitialised locals for the first one — modelled as 0).
        /// </summary>
        public void Render_b06720(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            byte cr = 0, cg = 0, cb = 0, blend = 0;
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0) continue;
                float x = M.F32(b + 0x14), z = M.F32(b + 0x18);
                int ang = M.I16(b + 0x10);
                float c = P3Cos(ang), s = P3Sin(ang);
                uint p0 = b + 0x9c, p1 = b + 0x84, p2 = b + 0x90, p3 = b + 0xa8;
                float xc = x * c, zs = z * s, xs = x * s;
                M.F32(p0 + 4, -1.0f);
                M.F32(p1 + 4, -1.0f);
                float p0x = xc - zs;
                M.F32(p2 + 4, -1.0f);
                M.F32(p3 + 4, -1.0f);
                M.F32(p0, p0x);
                float nz = -z;
                float nzs = nz * s;
                float zc = z * c;
                float p1x = xc - nzs;
                float p0z = zc + xs;
                M.F32(p1, p1x);
                float nzc = nz * c;
                M.F32(p0 + 8, p0z);
                float p1z = nzc + xs;
                float nx = -x;
                float nxc = nx * c;
                M.F32(p1 + 8, p1z);
                float p3x = nxc - zs;
                float p2x = nxc - nzs;
                float nxs = nx * s;
                float p2z = nzc + nxs;
                M.F32(p3, p3x);
                float p3z = nxs + zc;
                M.F32(p2, p2x);
                M.F32(p2 + 8, p2z);
                M.F32(p3 + 8, p3z);
                float yb = -1.0f - M.F32(b + 0x1c);

                float kind = M.F32(b + 0x20);
                if (kind == 0f) { cr = 0xff; cg = 0xff; cb = 0xff; blend = 0; }
                else if (kind == 1.0f) { cr = 0xeb; cg = 0xff; cb = 0xeb; blend = 1; }
                else if (kind == 2.0f) { cr = 0x50; cg = 0x50; cb = 0x50; blend = 1; }
                else if (kind == 4.0f)
                {
                    cr = 0xff; cg = 0xff;
                    cb = (byte)FxOps.Cvtt(M.F32(b + 0x24) + 0f + M.F32(b + 0x28));
                    blend = 1;
                }
                short alpha = M.I16(b + 2);
                P3QuadColor(rt, ref cnt, alpha, p0, p1, p1x, yb, p1z, p0x, yb, p0z, matrix, blend, cr, cg, cb);
                P3QuadColor(rt, ref cnt, alpha, p1, p2, p2x, yb, p2z, p1x, yb, p1z, matrix, blend, cr, cg, cb);
                P3QuadColor(rt, ref cnt, alpha, p2, p3, p3x, yb, p3z, p2x, yb, p2z, matrix, blend, cr, cg, cb);
                P3QuadColor(rt, ref cnt, alpha, p3, p0, p0x, yb, p0z, p3x, yb, p3z, matrix, blend, cr, cg, cb);
                if (M.F32(b + 0x20) == 2.0f)
                {
                    int a2 = M.I16(b + 2) * 2;
                    if (a2 > 0xff) a2 = 0xff;
                    P3QuadColor(rt, ref cnt, (short)a2, p0, p2, p2x, yb, p2z, p0x, yb, p0z, matrix, blend, cr, cg, cb);
                    P3QuadColor(rt, ref cnt, (short)a2, p1, p3, p3x, yb, p3z, p1x, yb, p1z, matrix, blend, cr, cg, cb);
                }
            }
        }

        [FxPrimRender(0x4b)]
        public void Render4B(FxRuntime rt, uint viewMatrix) => Render_b07f30(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b07f30 (0xb07f30) — type 0x4b: per particle three vertical fins at angle−1 .. angle+1
        /// from radius R (+0x3fc) to R + (+0x468), rising to −0.1H/−0.4H (outer fins, alpha/3) or −0.2H/−H
        /// (middle fin), H = +0x3f0. Heights are computed as −0.0 − x.
        /// </summary>
        public void Render_b07f30(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            const float negZero = -0.0f;
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0) continue;
                for (int i = 0; i <= 2; i++)
                {
                    int a = M.I16(b + 0x10) - 1 + i;
                    if (a < 0) a += 0x168;
                    else if (a >= 0x168) a -= 0x168;
                    float r = M.F32(b + 0x14);
                    float c = P3Cos(a), s = P3Sin(a);
                    float h = M.F32(b + 8);
                    float rc = r * c, rs = r * s;
                    float r2 = r + M.F32(b + 0x80);
                    int alpha;
                    float yTip, yOut, oc, os;
                    if (i == 1)
                    {
                        yTip = negZero - h * 0.2f;
                        alpha = M.I16(b + 2);
                        oc = c * r2;
                        os = s * r2;
                        yOut = negZero - h;
                    }
                    else
                    {
                        yTip = negZero - h * 0.1f;
                        alpha = M.I16(b + 2) / 3;
                        oc = r2 * c;
                        os = r2 * s;
                        yOut = negZero - h * 0.4f;
                    }
                    Quad_b0a900(rt, ref cnt, (short)alpha, rc, negZero, rs, oc, negZero, os, oc, yOut, os, rc, yTip, rs,
                        matrix, 0, 0, 0, 0xff, 0xff, 0xff, 0f, 1.0f, 0f, 1.0f, 0);
                }
            }
        }

        [FxPrimRender(0x44)]
        public void Render44(FxRuntime rt, uint viewMatrix) => Render_af6810(rt, viewMatrix);

        /// <summary>
        /// FUN_00b0d9c0 (stdcall, RET 0x18) — rotates four points in place: about X by <paramref name="angleX"/>
        /// (z' = z·c − y·s, y' = y·c + z·s), then about Y by <paramref name="angleY"/> (x' = x·c − z'·s,
        /// z'' = z'·c + x·s). Raw degree-table reads.
        /// </summary>
        static void P3Rotate4_b0d9c0(Span<float> q, short angleY, short angleX)
        {
            for (int p = 0; p < 4; p++)
            {
                int o = p * 3;
                float y = q[o + 1], z = q[o + 2];
                q[o + 2] = z * P3Cos(angleX) - y * P3Sin(angleX);
                q[o + 1] = y * P3Cos(angleX) + z * P3Sin(angleX);
            }
            for (int p = 0; p < 4; p++)
            {
                int o = p * 3;
                float x = q[o], z = q[o + 2];
                q[o] = x * P3Cos(angleY) - z * P3Sin(angleY);
                q[o + 2] = z * P3Cos(angleY) + x * P3Sin(angleY);
            }
        }

        /// <summary>
        /// CEffectPrim::fn_af6810 (0xaf6810) — type 0x44: per particle ten concentric camera-facing rings
        /// (radius (10−j)·{1|3} + scale +0x468, corners at 45°/135°/225°/315° or 35°/145°/215°/325° for kinds
        /// 6/7) around +0x3fc, rotated by the camera angles (f_4a0, f_3ec) and lifted by 30/40; alpha = the
        /// fan value (int) +0x3fc[j]. Kind 3 adds a half-alpha grey quad for the outer ring; kinds 5/6/7 add a
        /// ring scaled by 1.1 (the scaled corners persist at +0x84..).
        /// </summary>
        public void Render_af6810(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            Span<float> q = stackalloc float[12];
            q.Clear();
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                for (int j = 10; j >= 1; j--)
                {
                    byte kind = M.U8(b + 0x6c);
                    bool k67 = kind == 6 || kind == 7;
                    int a1 = k67 ? 0x7d : 0x87;
                    int a1alt = k67 ? 0x91 : 0x9b;
                    int a0 = (k67 ? 0x8c : 0xb4) / 4;
                    int n = 10 - j;
                    float rr;
                    if (kind == 2) rr = (float)n + M.F32(b + 0x80);
                    else
                    {
                        float s = M.F32(b + 0x80);
                        if (kind != 4 && kind != 5) n *= 3;
                        rr = (float)n + s;
                    }
                    uint p0 = b + 0x9c, p1 = b + 0x84, p2 = b + 0x90, p3 = b + 0xa8;
                    float p0x = rr * P3Cos(a0);
                    float p0z = P3Sin(a0) * rr;
                    M.F32(p0 + 4, 0f);
                    M.F32(p0, p0x);
                    M.F32(p0 + 8, p0z);
                    if (k67) a1 = a1alt;
                    float p1x = rr * P3Cos(a1);
                    float p1z = P3Sin(a1) * rr;
                    M.F32(p1 + 4, 0f);
                    M.F32(p1, p1x);
                    M.F32(p1 + 8, p1z);
                    uint a2 = (uint)((k67 ? 0x46 : 0x5a) + a1);
                    if (a2 >= 0x168) a2 -= 0x168;
                    float p2x = rr * P3Cos((int)a2);
                    float p2z = P3Sin((int)a2) * rr;
                    M.F32(p2 + 4, 0f);
                    M.F32(p2, p2x);
                    M.F32(p2 + 8, p2z);
                    int a3 = (k67 ? 0x6e : 0x5a) + (int)a2;
                    if (a3 >= 0x168) a3 -= 0x168;
                    float p3x = rr * P3Cos(a3);
                    float p3z = P3Sin(a3) * rr;
                    M.F32(p3 + 4, 0f);
                    M.F32(p3, p3x);
                    M.F32(p3 + 8, p3z);
                    float bse = M.F32(b + 0x14);
                    // q: Q0 [ebp-48h], Q1 [ebp-54h], Q2 [ebp-30h], Q3 [ebp-3Ch]
                    q[0] = bse + p0x; q[1] = 0f; q[2] = p0z;
                    q[3] = bse + p1x; q[4] = 0f; q[5] = p1z;
                    q[6] = bse + p2x; q[7] = 0f; q[8] = p2z;
                    q[9] = bse + p3x; q[10] = 0f; q[11] = p3z;
                    P3Rotate4_b0d9c0(q, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                    kind = M.U8(b + 0x6c);
                    float lift = kind == 2 || kind == 4 || kind == 6 || kind == 7 ? 40.0f : 30.0f;
                    q[1] -= lift; q[4] -= lift; q[7] -= lift; q[10] -= lift;
                    int fi = k * 0x2d + j;
                    int alpha = FxOps.Cvtt(M.F32(A + 0x3fc + (uint)fi * 4));
                    if (alpha > 0)
                    {
                        sbyte tex = M.I8(b + 0x68);
                        short blend;
                        byte cr, cg, cb;
                        if (kind == 0) { blend = 1; cr = 0; cg = 0; cb = 0; }
                        else if (kind == 3)
                        {
                            if (j == 10)
                                P3Quad44(rt, ref cnt, (short)(alpha / 2), q, matrix, 1, tex, 10, 10, 10);
                            blend = 1; cr = 10; cg = 10; cb = 10;
                        }
                        else if (kind == 6 || kind == 7) { blend = 0; cr = 0xff; cg = 0xff; cb = 0xff; }
                        else { blend = 1; cr = 0xff; cg = 0xff; cb = 0xff; }
                        P3Quad44(rt, ref cnt, (short)alpha, q, matrix, blend, tex, cr, cg, cb);
                    }
                    byte kind2 = M.U8(b + 0x6c);
                    if (kind2 != 5 && kind2 != 6 && kind2 != 7) continue;
                    const float grow = 1.1f;
                    float s0x = M.F32(p0) * grow, s0z = M.F32(p0 + 8) * grow;
                    M.F32(p0, s0x);
                    M.F32(p0 + 8, s0z);
                    float s0y = M.F32(p0 + 4) * grow;
                    M.F32(p0 + 4, s0y);
                    float s1y = M.F32(p1 + 4), s1z = M.F32(p1 + 8);
                    float s1x = M.F32(p1) * grow;
                    s1y *= grow;
                    M.F32(p1, s1x);
                    s1z *= grow;
                    M.F32(p1 + 4, s1y);
                    M.F32(p1 + 8, s1z);
                    float s3x = M.F32(p3) * grow, s3y = M.F32(p3 + 4) * grow, s3z = M.F32(p3 + 8) * grow;
                    M.F32(p3, s3x);
                    M.F32(p3 + 4, s3y);
                    M.F32(p3 + 8, s3z);
                    float s2x = M.F32(p2) * grow;
                    float s2y = M.F32(p2 + 4) * grow;
                    float s2z = M.F32(p2 + 8) * grow;
                    M.F32(p2 + 4, s2y);
                    M.F32(p2, s2x);
                    M.F32(p2 + 8, s2z);
                    bse = M.F32(b + 0x14);
                    q[0] = s0x + bse; q[1] = s0y; q[2] = s0z;
                    q[3] = s1x + bse; q[4] = s1y; q[5] = s1z;
                    q[6] = s2x + bse; q[7] = s2y; q[8] = s2z;
                    q[9] = s3x + bse; q[10] = s3y; q[11] = s3z;
                    P3Rotate4_b0d9c0(q, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                    float lift2 = kind2 == 5 ? 30.0f : 40.0f;
                    q[1] -= lift2; q[4] -= lift2; q[7] -= lift2; q[10] -= lift2;
                    int alpha2 = FxOps.Cvtt(M.F32(A + 0x3fc + (uint)fi * 4));
                    if (alpha2 > 0)
                    {
                        sbyte tex = M.I8(b + 0x68);
                        if (kind2 == 7) P3Quad44(rt, ref cnt, (short)alpha2, q, matrix, 0, tex, 0xff, 5, 5);
                        else P3Quad44(rt, ref cnt, (short)alpha2, q, matrix, 0, tex, 5, 5, 0xff);
                    }
                }
            }
        }

        /// <summary>fn_af6810's quad: v0 = Q1, v1 = Q0, v2 = Q3, v3 = Q2 (prim-relative, uv (0,1)-(1,0), mode 0).</summary>
        void P3Quad44(FxRuntime rt, ref int cnt, short alpha, Span<float> q, uint matrix, short blend, sbyte tex, byte r, byte g, byte b)
            => Quad_b0a900(rt, ref cnt, alpha, q[3], q[4], q[5], q[0], q[1], q[2], q[9], q[10], q[11], q[6], q[7], q[8],
                matrix, blend, tex, 0, r, g, b, 0f, 1.0f, 0f, 1.0f, 0);

        [FxPrimRender(0x5a)]
        public void Render5A(FxRuntime rt, uint viewMatrix) => Render_b05b50(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b05b50 (0xb05b50) — type 0x5a: particles 0/1 draw the four walls of a W×D box
        /// (+0x14/+0x18, depth −1 − (+0x1c)); particles 2/3 draw a "fuse" running around a W×D rectangle:
        /// T (+0x80) is the distance travelled along the perimeter (sides of lengths D, 2W, …), dT (+0x20)
        /// the burning length; the side being burnt is drawn as one quad and, when the burn overflows the
        /// corner (e &gt; 0), the next side too. Colours: f(+0x24) ≥ 10 (or NaN) selects the second palette.
        /// </summary>
        public void Render_b05b50(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            for (int k = 0; k < 2; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0) continue;
                bool hi = !(10.0f > M.F32(b + 0x24));
                byte cb = hi ? (byte)0xfa : (byte)0xc8;
                byte cg = hi ? (byte)100 : (byte)0xfa;
                byte cr = hi ? (byte)100 : (byte)0xc8;
                float x = M.F32(b + 0x14), z = M.F32(b + 0x18);
                uint p0 = b + 0x9c, p1 = b + 0x84, p2 = b + 0x90, p3 = b + 0xa8;
                M.F32(p0, x); M.F32(p1, x); M.F32(p0 + 8, z);
                M.F32(p0 + 4, -1.0f); M.F32(p1 + 4, -1.0f); M.F32(p2 + 4, -1.0f);
                float nx = -x, nz = -z;
                M.F32(p3 + 8, z);
                M.F32(p2, nx); M.F32(p3, nx);
                M.F32(p1 + 8, nz); M.F32(p2 + 8, nz);
                M.F32(p3 + 4, -1.0f);
                float yb = -1.0f - M.F32(b + 0x1c);
                short alpha = M.I16(b + 2);
                P3QuadColor(rt, ref cnt, alpha, p0, p1, x, yb, nz, x, yb, z, matrix, 0, cr, cg, cb);
                P3QuadColor(rt, ref cnt, M.I16(b + 2), p1, p2, nx, yb, nz, x, yb, nz, matrix, 0, cr, cg, cb);
                P3QuadColor(rt, ref cnt, M.I16(b + 2), p2, p3, nx, yb, z, nx, yb, nz, matrix, 0, cr, cg, cb);
                P3QuadColor(rt, ref cnt, M.I16(b + 2), p3, p0, x, yb, z, nx, yb, z, matrix, 0, cr, cg, cb);
            }
            for (int k = 2; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                float e = 0f;
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0) continue;
                bool hi = !(10.0f > M.F32(b + 0x24));
                byte cb = hi ? (byte)0xfa : (byte)0x96;
                byte cg = hi ? (byte)0x32 : (byte)0xfa;
                byte cr = hi ? (byte)0x32 : (byte)0x96;
                float w = M.F32(b + 0x14);
                float t = M.F32(b + 0x80);
                float d = M.F32(b + 0x18);
                uint p0 = b + 0x9c, p1 = b + 0x84, p2 = b + 0x90, p3 = b + 0xa8;
                M.F32(p0, w);
                float w2 = w + w;
                M.F32(p0 + 8, d);
                M.F32(p0 + 4, -1.0f);
                M.F32(p1 + 4, -1.0f); M.F32(p2 + 4, -1.0f); M.F32(p3 + 4, -1.0f);
                M.F32(p1, w);
                float nd = -d, nw = -w;
                M.F32(p3 + 8, d);
                M.F32(p1 + 8, nd);
                M.F32(p2, nw);
                M.F32(p2 + 8, nd);
                M.F32(p3, nw);
                float v12 = d, v13 = nd, v14 = nw, v9 = w, f28 = w, f40 = d, f18 = nw, f1c = nd;
                float dT = M.F32(b + 0x20);
                int side;
                if (!(w2 > t))
                {
                    if (!(w * 4.0f > t))
                    {
                        if (!(w * 6.0f > t))
                        {
                            side = 3;
                            f18 = (t - w * 6.0f) - w;
                            M.F32(p3, f18);
                            v9 = f18 + dT;
                            M.F32(p0, v9);
                            if (v9 > w)
                            {
                                e = v9 - w;
                                M.F32(p0, w);
                                M.F32(p1 + 8, d - e);
                                v13 = d - e;
                                v9 = w;
                            }
                        }
                        else
                        {
                            side = 2;
                            f1c = (t - w * 4.0f) - d;
                            M.F32(p2 + 8, f1c);
                            v12 = f1c + dT;
                            M.F32(p3 + 8, v12);
                            if (v12 > d)
                            {
                                M.F32(p3 + 8, d);
                                e = v12 - d;
                                M.F32(p0, f18 + e);
                                v12 = d;
                                v9 = f18 + e;
                            }
                        }
                    }
                    else
                    {
                        side = 1;
                        f28 = w - (t - w2);
                        M.F32(p1, f28);
                        v14 = f28 - dT;
                        M.F32(p2, v14);
                        if (f18 > v14)
                        {
                            M.F32(p2, f18);
                            e = f18 - v14;
                            M.F32(p3 + 8, f1c + e);
                            v12 = f1c + e;
                            v14 = f18;
                        }
                    }
                }
                else
                {
                    side = 0;
                    f40 = d - t;
                    M.F32(p0 + 8, f40);
                    v13 = f40 - dT;
                    M.F32(p1 + 8, v13);
                    if (f1c > v13)
                    {
                        M.F32(p1 + 8, f1c);
                        e = f1c - v13;
                        v14 = w - e;
                        M.F32(p2, v14);
                        v13 = f1c;
                    }
                }
                float yb = -1.0f - M.F32(b + 0x1c);
                short alpha = M.I16(b + 2);
                switch (side)
                {
                    case 3:
                        P3QuadColor(rt, ref cnt, alpha, p3, p0, v9, yb, f40, f18, yb, v12, matrix, 0, cr, cg, cb);
                        if (e > 0f)
                            P3QuadColor(rt, ref cnt, M.I16(b + 2), p0, p1, f28, yb, v13, v9, yb, f40, matrix, 0, cr, cg, cb);
                        break;
                    case 2:
                        P3QuadColor(rt, ref cnt, alpha, p2, p3, f18, yb, v12, v14, yb, f1c, matrix, 0, cr, cg, cb);
                        if (e > 0f)
                            P3QuadColor(rt, ref cnt, M.I16(b + 2), p3, p0, v9, yb, f40, f18, yb, v12, matrix, 0, cr, cg, cb);
                        break;
                    case 1:
                        P3QuadColor(rt, ref cnt, alpha, p1, p2, v14, yb, f1c, f28, yb, v13, matrix, 0, cr, cg, cb);
                        if (e > 0f)
                            P3QuadColor(rt, ref cnt, M.I16(b + 2), p2, p3, f18, yb, v12, v14, yb, f1c, matrix, 0, cr, cg, cb);
                        break;
                    default:
                        P3QuadColor(rt, ref cnt, alpha, p0, p1, f28, yb, v13, v9, yb, f40, matrix, 0, cr, cg, cb);
                        if (e > 0f)
                            P3QuadColor(rt, ref cnt, M.I16(b + 2), p1, p2, v14, yb, f1c, f28, yb, v13, matrix, 0, cr, cg, cb);
                        break;
                }
            }
        }

        [FxPrimRender(0x65)]
        public void Render65(FxRuntime rt, uint viewMatrix) => Render_afe0a0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_afe0a0 (0xafe0a0) — type 0x65: per particle a jittered lightning-like tube: N (+0x94)
        /// profile angles at +0x14.. give points along the tilt +0x7e (height from −R·sin·S), each point
        /// jittered by three rand()%7 tables (drawn per particle even when inactive); consecutive points are
        /// joined by a 3-sided tube of radius +8 oriented by the segment direction (cos-table search, x87
        /// fsqrt), alpha fading by 5 per segment, world-space quads (kind +6 == 3: random r/g).
        /// </summary>
        public void Render_afe0a0(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            Span<float> ja = stackalloc float[21];
            Span<float> jb = stackalloc float[21];
            Span<float> jc = stackalloc float[21];
            ja.Clear(); jb.Clear(); jc.Clear();
            float p0px = 0f, p0py = 0f, p0pz = 0f, p1px = 0f, p1py = 0f, p1pz = 0f;
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                float n = M.F32(b + 0x94);
                for (int i = 0; n > (float)i; i++)
                {
                    ja[i] = (float)(rt.Rand.Next() % 7) * 0.1f - 0.3f;
                    jb[i] = (float)(rt.Rand.Next() % 7) * 0.1f - 0.3f;
                    jc[i] = (float)(rt.Rand.Next() % 7) * 0.1f - 0.3f;
                    n = M.F32(b + 0x94);
                }
                if (M.U8(b) != 1 || M.I16(b + 2) <= 0 || M.I32(b + 0xc) <= 0 || !(n > 0f)) continue;
                for (int j = 0; (float)j < M.F32(b + 0x94); j++)
                {
                    float f0 = M.F32(b + 0x14 + (uint)j * 4);
                    float r = M.F32(b + 0x80);
                    float h0 = 0f;
                    if (f0 > 0f) h0 = f0 / 90.0f * r + 0f;
                    int ta = M.I16(b + 0x7e);
                    float nr = -r;
                    float ct = P3Cos(ta), st = P3Sin(ta);
                    short s0 = (short)FxOps.Cvtt(f0);
                    float sS = M.F32(b + 0x90);
                    float x0 = ct * h0 + jb[j];
                    float y0 = nr * P3Sin(s0) * sS + (ja[j] - 2.0f);
                    float f1 = M.F32(b + 0x18 + (uint)j * 4);
                    float z0 = st * h0 + jc[j];
                    float h1 = 0f;
                    if (f1 > 0f) h1 = f1 / 90.0f * r + 0f;
                    short s1 = (short)FxOps.Cvtt(f1);
                    float x1 = ct * h1 + jb[j + 1];
                    float z1 = st * h1 + jc[j + 1];
                    float y1 = nr * P3Sin(s1) * sS + (ja[j + 1] - 2.0f);
                    float dz = z0 - z1;
                    float dx = x0 - x1;
                    float len = (float)Math.Sqrt((double)(dx * dx + dz * dz));
                    float nz = dz / len;
                    float nx = dx / len;
                    int dir = 0xb4;
                    for (short i = 0; i <= 0xb4; i++)
                    {
                        if (nx >= P3Cos(i))
                        {
                            dir = P3Jb(nz, 0f) ? (ushort)(0x168 - i) : (ushort)i;
                            break;
                        }
                    }
                    float bx = M.F32(b + 0x84), by = M.F32(b + 0x88), bz = M.F32(b + 0x8c);
                    x0 = bx + x0; y0 = y0 + by; z0 = z0 + bz;
                    x1 = bx + x1; y1 = y1 + by; z1 = z1 + bz;
                    int a2 = (short)(dir + 0x5a) < 0x168 ? (short)(ushort)(dir + 0x5a) : (short)(ushort)(dir + 0x5a - 0x168);
                    if ((uint)(j - 1) > 0x11) continue;
                    for (int m = 0; m <= 0x168; m += 0x78)
                    {
                        float r2 = M.F32(b + 8);
                        float rc = r2 * P3Cos(m);
                        float rs = r2 * P3Sin(m);
                        float rcz = rc * P3Sin(a2);
                        float rcx = rc * P3Cos(a2);
                        float q0x = rcx + x0, q1x = rcx + x1;
                        float q0y = rs + y0, q1y = rs + y1;
                        float q0z = rcz + z0, q1z = rcz + z1;
                        int alpha = M.I16(b + 2) - 5 * j;
                        if (m > 0 && alpha > 0)
                        {
                            byte cr, cg;
                            if (M.I16(b + 6) == 3)
                            {
                                int g1 = rt.Rand.Next() % 100;
                                int r1 = rt.Rand.Next() % 50;
                                cg = g1 >= 0 ? (byte)g1 : (byte)0;
                                cr = (byte)((r1 >= 0 ? (byte)r1 : (byte)0) - 0x38);
                            }
                            else
                            {
                                cr = cg = (byte)(j * 6);
                            }
                            Quad_b0a900(rt, ref cnt, (short)alpha, q0x, q0y, q0z, q1x, q1y, q1z, p1px, p1py, p1pz, p0px, p0py, p0pz,
                                matrix, 0, 0, 1, cr, cg, 0xff, 0f, 1.0f, 0f, 1.0f, 0);
                        }
                        p0px = q0x; p0py = q0y; p0pz = q0z;
                        p1px = q1x; p1py = q1y; p1pz = q1z;
                    }
                }
            }
        }

        /// <summary>fn_b0a900 wall quad: top corners read from memory (t0, t1), bottom corners given.</summary>
        void P3QuadColor(FxRuntime rt, ref int cnt, short alpha, uint t0, uint t1,
            float x2, float y2, float z2, float x3, float y3, float z3, uint matrix, short blend, byte r, byte g, byte b)
            => Quad_b0a900(rt, ref cnt, alpha, M.F32(t0), M.F32(t0 + 4), M.F32(t0 + 8), M.F32(t1), M.F32(t1 + 4), M.F32(t1 + 8),
                x2, y2, z2, x3, y3, z3, matrix, blend, 0, 0, r, g, b, 0f, 1.0f, 0f, 1.0f, 0);

        /// <summary>fn_b0a900 white, tex 0, prim-relative, uv (0,1)-(1,0), mode 0.</summary>
        void P3QuadXyz(FxRuntime rt, ref int cnt, short alpha,
            float x0, float y0, float z0, float x1, float y1, float z1,
            float x2, float y2, float z2, float x3, float y3, float z3, uint matrix, short blend)
            => Quad_b0a900(rt, ref cnt, alpha, x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3,
                matrix, blend, 0, 0, 0xff, 0xff, 0xff, 0f, 1.0f, 0f, 1.0f, 0);

        /// <summary>fn_b0a900 with the argument pattern shared by most callers: tex 0, prim-relative, uv (0,1)-(1,0).</summary>
        void P3Quad(FxRuntime rt, ref int cnt, short alpha, uint q0, uint q1, uint q2, uint q3, uint matrix,
            short blend, byte r, byte g, byte b, byte mode)
        {
            Quad_b0a900(rt, ref cnt, alpha,
                M.F32(q0), M.F32(q0 + 4), M.F32(q0 + 8), M.F32(q1), M.F32(q1 + 4), M.F32(q1 + 8),
                M.F32(q2), M.F32(q2 + 4), M.F32(q2 + 8), M.F32(q3), M.F32(q3 + 4), M.F32(q3 + 8),
                matrix, blend, 0, 0, r, g, b, 0f, 1.0f, 0f, 1.0f, mode);
        }

        /// <summary>
        /// CEffectPrim::fn_aee0b0 (0xaee0b0) — types 0x27 / 0x58: per particle a horizontal square (corners every
        /// 90° from the particle angle, or from 23·k for most f_318 values) of breathing radius
        /// (sin(w)+1)·0.1r + 0.8r (double precision), at height +0x3fc; colour/blend/mode chosen by f_318
        /// (100 / 101 draw the quad twice).
        /// </summary>
        public void Render_aee0b0(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3f8 + (uint)k * 0xb4;
                if (M.U8(c - 0x10) != 1) continue;
                float kind = f_318;
                int d0 = k * 0x17;
                if (kind == 100.0f || kind >= 110.0f || (kind == 4.0f && M.F32(c + 0x14) == 4.0f) || kind == 10.0f || kind == 0f)
                    d0 = M.I16(c);
                float r = M.F32(c + 0x70);
                float t = (P3Sin(M.I16(c + 0x6e)) + 1.0f) * (r * 0.1f);
                double kr = (double)t + (double)r * 0.8;
                int d1 = d0 + (d0 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int d2 = d1 + (d1 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int d3 = d2 + (d2 + 0x5a < 0x168 ? 0x5a : -0x10e);
                float y = M.F32(c + 4);
                M.F32(c + 0x8c, (float)((double)P3Cos(d0) * kr));
                M.F32(c + 0x94, (float)((double)P3Sin(d0) * kr));
                M.F32(c + 0x90, y);
                M.F32(c + 0x78, y);
                M.F32(c + 0x74, (float)((double)P3Cos(d1) * kr));
                M.F32(c + 0x7c, (float)((double)P3Sin(d1) * kr));
                M.F32(c + 0x84, y);
                M.F32(c + 0x80, (float)((double)P3Cos(d2) * kr));
                M.F32(c + 0x88, (float)((double)P3Sin(d2) * kr));
                M.F32(c + 0x9c, y);
                M.F32(c + 0x98, (float)((double)P3Cos(d3) * kr));
                M.F32(c + 0xa0, (float)((double)P3Sin(d3) * kr));

                short alpha = (short)M.U16(c - 0xe);
                uint q0 = c + 0x8c, q1 = c + 0x74, q2 = c + 0x80, q3 = c + 0x98;
                short blend = 0;
                byte cr = 0xff, cg = 0xff, cb = 0xff, mode = 0;
                kind = f_318;
                if (kind == 101.0f)
                {
                    P3Quad(rt, ref cnt, alpha, q0, q1, q2, q3, matrix, 0, 0xff, 0, 0, 0);
                    cg = 0; cb = 0;
                }
                else if (kind == 102.0f) { cb = 0; }
                else if (kind == 111.0f) { cr = 0x37; cg = 0x37; }
                else if (kind == 112.0f) { cr = 0x69; cg = 0xe1; cb = 0x69; }
                else if (kind == 100.0f)
                {
                    P3Quad(rt, ref cnt, alpha, q0, q1, q2, q3, matrix, 0, 0xff, 0xff, 0xff, 0);
                }
                else if (kind == 110.0f) { blend = 1; }
                else if (kind == 113.0f) { cr = 5; cg = 5; mode = 1; }
                else if (kind == 114.0f) { cr = 0x9b; cg = 5; cb = 5; mode = 1; }
                else if (kind == 15.0f) { cr = 0x23; cb = 0x23; }
                else if (kind == 17.0f) { cr = 0x69; cg = 0x69; }
                else if (kind == 18.0f) { cr = 0x23; cg = 0x23; }
                else if (kind == 0f) { blend = 1; }
                P3Quad(rt, ref cnt, alpha, q0, q1, q2, q3, matrix, blend, cr, cg, cb, mode);
            }
        }
    }
}
