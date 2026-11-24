// Synced from reference/port/src/RoClassicFx/Shared/newsys/CParticle.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// View of a new-system particle (C++ class CParticle, 0x2bc bytes, vtable 0xfe1d20), the textured quad the
    /// CEffect classes animate. Particles come from the pools of CEffectMemoryMgr (DAT_015127d8) and go back
    /// there; they are never deleted.
    /// Layout (ctor FUN_00a67480 / reset FUN_00a677e0): +4 effect id, +8 flag, +0xc/+0x10 indices, +0x18 texture,
    /// +0x1c RP flags, +0x20 scale, +0x24/+0x28/+0x2c position, +0x3c/+0x3e/+0x40 colour r/g/b (int16),
    /// +0x44 alpha, +0x4c/+0x50/+0x54 per-effect angles, +0xc0/+0xc4 radius/angle, +0xd0..+0xd8 local position,
    /// +0xe8 age, +0xf0 RP quad (vertex pointer → +0x118), +0x1c8 4x3 matrix, +0x1f8 four TL vertices
    /// {x, y, z, rhw, colour, specular, u, v}, +0x2a8/+0x2ac src/dst blend, +0x2b0 pool index, +0x2b4 rhw bias,
    /// +0x2b8 callback (0xa68060).
    /// </summary>
    public readonly partial struct CParticle
    {
        public readonly FxMemory M;
        public readonly uint A;

        public CParticle(FxMemory m, uint a) { M = m; A = a; }

        public const uint VtCParticle = 0x00fe1d20;
        public const uint Size = 0x2bc;
        const uint Callback_a68060 = 0x00a68060;

        /// <summary>FUN_00547010 — RP quad init: vertex pointer → inline 4 vertices at +0x28, count 4, cleared vertices, +0x24 = 5.</summary>
        static void InitRpQuad_547010(FxMemory M, uint q)
        {
            M.U32(q + 4, 4);
            M.U32(q, q + 0x28);
            M.U32(q + 8, 0); M.U32(q + 0xc, 0); M.U32(q + 0x10, 0);
            M.U16(q + 0x14, 1);
            M.U32(q + 0x18, 0); M.U32(q + 0x1c, 0); M.U32(q + 0x20, 0);
            ClearRpVertices(M, q + 0x28);
            M.U32(q + 0x24, 5);
        }

        /// <summary>Four default TL vertices (0, specular 0xff000000) — also FUN_005471a0 (copy of a fresh quad's vertices).</summary>
        static void ClearRpVertices(FxMemory M, uint v)
        {
            for (uint k = 0; k < 4; k++, v += 0x20)
            {
                for (uint i = 0; i < 0x20; i += 4) M.U32(v + i, 0);
                M.U32(v + 0x14, 0xff000000);
            }
        }

        // offsets zeroed by the constructor / reset / common tail, in the client's order (static: no array per particle)
        static readonly uint[] CtorZero = { 0xc0, 0x18, 0xc4, 0xcc, 0xc8, 0x14, 0xe8, 0xec, 0xd0, 0xd4, 0xd8 };
        static readonly uint[] ResetZero = { 0xc4, 0xcc, 0xc8, 0x14, 0xe8, 0xec, 0xd0, 0xd4, 0xd8 };
        static readonly uint[] CommonZero = { 0x278, 0x27c, 0x280, 0x218, 0x21c, 0x220, 0x284, 0x288, 0x28c, 0x238, 0x23c, 0x240,
                                              0x290, 0x294, 0x298, 0x258, 0x25c, 0x260, 0x29c, 0x2a0, 0x2a4 };

        /// <summary>CParticle::CParticle (FUN_00a67480; x, y, z, flags).</summary>
        public static void Construct_a67480(FxMemory M, uint p, float x, float y, float z, int flags)
        {
            M.I32(p + 0x1c, flags);
            M.F32(p + 0x24, x);
            M.F32(p + 0x28, y);
            M.U32(p, VtCParticle);
            M.F32(p + 0x2c, z);
            for (uint o = 0x30; o <= 0x38; o += 4) M.U32(p + o, 0);
            for (uint o = 0xd0; o <= 0xe4; o += 4) M.U32(p + o, 0);
            InitRpQuad_547010(M, p + 0xf0);
            ClearRpVertices(M, p + 0x1f8);
            for (uint o = 0x278; o < 0x2a8; o += 4) M.U32(p + o, 0);
            M.U32(p + 0x2b8, Callback_a68060);
            foreach (uint o in CtorZero) M.U32(p + o, 0);
            M.U32(p + 0x3c, 0x00ff00ff);
            M.U16(p + 0x40, 0xff);
            M.F32(p + 0x20, 1.0f);
            M.I32(p + 0x44, 0xff);
            M.I32(p + 0x2a8, 5);
            M.I32(p + 0x2ac, 2);
            ClearRpVertices(M, p + 0xf0 + 0x28);                             // FUN_00547010 on a local + FUN_005471a0
            ResetCommon(M, p);
        }

        /// <summary>FUN_00a677e0 — the reset a pooled particle gets when it is handed out again.</summary>
        public void Reset_a677e0()
        {
            M.U32(A + 0x3c, 0x00ff00ff);
            M.U16(A + 0x40, 0xff);
            M.F32(A + 0x20, 1.0f);
            M.U32(A + 0xc0, 0);
            M.U32(A + 0x18, 0);
            M.I32(A + 0x44, 0xff);
            foreach (uint o in ResetZero) M.U32(A + o, 0);
            M.I32(A + 0x2a8, 5);
            M.I32(A + 0x2ac, 2);
            ClearRpVertices(M, A + 0xf0 + 0x28);
            ResetCommon(M, A);
        }

        /// <summary>The tail shared by the constructor and the reset: identity matrix, bias 0, cleared vertex/normal slots, unit quad.</summary>
        static void ResetCommon(FxMemory M, uint p)
        {
            FxMatrix.Identity(M, p + 0x1c8);
            M.U32(p + 0x2b4, 0);
            foreach (uint o in CommonZero) M.U32(p + o, 0);
            SetUnitQuad_a67ad0(M, p + 0x1f8);
            M.U32(p + 0x2b8, Callback_a68060);
        }

        /// <summary>FUN_00a67ad0 — unit quad corners (-1,1,1) (1,1,1) (-1,-1,1) (1,-1,1) into four TL vertices.</summary>
        public static void SetUnitQuad_a67ad0(FxMemory M, uint v)
        {
            M.F32(v, -1f); M.F32(v + 4, 1f); M.F32(v + 8, 1f);
            M.F32(v + 0x20, 1f); M.F32(v + 0x24, 1f); M.F32(v + 0x28, 1f);
            M.F32(v + 0x40, -1f); M.F32(v + 0x44, -1f); M.F32(v + 0x48, 1f);
            M.F32(v + 0x60, 1f); M.F32(v + 0x64, -1f); M.F32(v + 0x68, 1f);
        }

        /// <summary>FUN_00a67fb0 — texture (+0x18) = GetTexture(name) (0x54f5c0 on CTexMgr 0x11a60e4).</summary>
        public void SetTexture_a67fb0(FxRuntime rt, string name) => M.U32(A + 0x18, rt.GetTexture(name));

        /// <summary>FUN_00a67f70 — colour (int16 r, g, b at +0x3c/+0x3e/+0x40); -1 keeps a channel.</summary>
        public void SetColor_a67f70(int r, int g, int b)
        {
            if ((short)r != -1) M.I16(A + 0x3c, (short)r);
            if ((short)g != -1) M.I16(A + 0x3e, (short)g);
            if ((short)b != -1) M.I16(A + 0x40, (short)b);
        }

        /// <summary>FUN_00a67b30 — FUN_00a67b60 with its arguments read from a small struct (billboard byte, v0, v1, u0, u1, byte, depth byte).</summary>
        public void RenderStrip_a67b30(FxRuntime rt, bool billboardOff, float v0, float v1, float u0, float u1, bool depth)
            => Render_a67b60(rt, billboardOff, v0, v1, u0, u1, depth);

        /// <summary>
        /// FUN_00a67b60 (thiscall, 7 args, ret 0x1c) — submits the particle's quad. Nothing is drawn while all
        /// four corners are exactly 0 (NaN counts as non-zero). Colour ((alpha &lt;&lt; 8 | r) &lt;&lt; 8 | g) &lt;&lt; 8 | b and
        /// the UVs (u0/u1 left/right, v0/v1 top/bottom) go into the corners, which are copied to the RP quad.
        /// World matrix: the particle's +0x1c8, or with <paramref name="fixedMatrix"/> false (camera billboard)
        /// +0x1c8 × (RotY(camera longitude) × Translation(position)). Every corner is projected in place
        /// (ProjectVertex 0x53b370); its rhw gets the bias +0x2b4 and a negative result abandons the particle
        /// (already projected corners stay written); z = FUN_0053b030(rhw) with <paramref name="depth"/>, else 0.
        /// Then texture / blend are stored and the quad goes to AddRP (0x537b00) with the flags at +0x1c.
        /// </summary>
        public void Render_a67b60(FxRuntime rt, bool fixedMatrix, float v0, float v1, float u0, float u1, bool depth)
        {
            if (FxMemory.CanSpanF32(A + 0x1f8))
            {
                // the particle's four TL vertices (+0x1f8, 0x80 bytes) through one span: same reads and stores
                Span<float> pv = M.F32Span(A + 0x1f8, 0x20);
                bool anyCorner = pv[0] != 0f || pv[1] != 0f || pv[2] != 0f || pv[8] != 0f || pv[9] != 0f || pv[10] != 0f
                              || pv[16] != 0f || pv[17] != 0f || pv[18] != 0f || pv[24] != 0f || pv[25] != 0f || pv[26] != 0f;
                if (!anyCorner) return;
                int cr = M.I16(A + 0x3c), cg = M.I16(A + 0x3e), cb = M.I16(A + 0x40);    // MOVSX: sign-extended into the OR
                uint color = (uint)((((M.I32(A + 0x44) << 8) | cr) << 8 | cg) << 8 | cb);
                Span<uint> pu = System.Runtime.InteropServices.MemoryMarshal.Cast<float, uint>(pv);
                pu[4] = color; pu[12] = color; pu[20] = color; pu[28] = color;           // +0x208 / +0x228 / +0x248 / +0x268
                pv[7] = v0; pv[15] = v0; pv[6] = u0; pv[14] = u1;                          // +0x214 +0x234 +0x210 +0x230
                pv[22] = u0; pv[23] = v1; pv[30] = u1; pv[31] = v1;                        // +0x250 +0x254 +0x270 +0x274
            }
            else
            {
                bool any = false;
                if (M.F32(A + 0x1f8) != 0f || M.F32(A + 0x1fc) != 0f || M.F32(A + 0x200) != 0f) any = true;
                if (M.F32(A + 0x218) != 0f || M.F32(A + 0x21c) != 0f || M.F32(A + 0x220) != 0f) any = true;
                if (M.F32(A + 0x238) != 0f || M.F32(A + 0x23c) != 0f || M.F32(A + 0x240) != 0f) any = true;
                if (!(M.F32(A + 0x258) != 0f || M.F32(A + 0x25c) != 0f || M.F32(A + 0x260) != 0f) && !any) return;

                int cr = M.I16(A + 0x3c), cg = M.I16(A + 0x3e), cb = M.I16(A + 0x40);        // MOVSX: sign-extended into the OR
                uint color = (uint)((((M.I32(A + 0x44) << 8) | cr) << 8 | cg) << 8 | cb);
                M.U32(A + 0x208, color);
                M.U32(A + 0x228, color);
                M.U32(A + 0x248, color);
                M.U32(A + 0x268, color);
                M.F32(A + 0x214, v0);
                M.F32(A + 0x234, v0);
                M.F32(A + 0x210, u0);
                M.F32(A + 0x230, u1);
                M.F32(A + 0x250, u0);
                M.F32(A + 0x254, v1);
                M.F32(A + 0x270, u1);
                M.F32(A + 0x274, v1);
            }
            uint vbuf = M.U32(A + 0xf0);
            M.Copy(vbuf, A + 0x1f8, 0x80);

            uint view = M.U32(rt.Mode + 0xd0) + 0x98;
            // stack matrices (the client's locals): no managed allocation per particle
            Span<float> world = stackalloc float[12];
            Span<float> tmp = stackalloc float[12];
            Span<float> final = stackalloc float[12];
            if (!fixedMatrix)
            {
                Span<float> t = stackalloc float[12];
                Span<float> r = stackalloc float[12];
                Mat43.Translation(M.F32(A + 0x24), M.F32(A + 0x28), M.F32(A + 0x2c), t);
                Mat43.SetIdentity(r);
                // every particle of a frame turns by the same camera longitude: sin/cos memoised on the exact angle
                rt.SinCosMemo(M.F32(M.U32(rt.Mode + 0xd0) + 0x30) / 180.0f * 3.141592f, out float sinY, out float cosY);
                Mat43.RotateY(r, sinY, cosY);
                Mat43.Mul(r, t, final);                      // rt1 (final is free until the last product)
                Mat43.Load(M, A + 0x1c8, tmp);
                Mat43.Mul(tmp, final, world);
            }
            else Mat43.Load(M, A + 0x1c8, world);
            Mat43.Load(M, view, tmp);
            Mat43.Mul(world, tmp, final);

            for (uint k = 0; k < 4; k++)
            {
                uint v = vbuf + k * 0x20;
                if (FxMemory.CanSpanF32(v))
                {
                    // x, y, z, rhw of the vertex through one span, stores in the original order
                    Span<float> vs = M.F32Span(v, 4);
                    rt.Renderer.ProjectVertex(final, vs[0], vs[1], vs[2], out float sx, out float sy, out float sz, out float rhw);
                    vs[3] = rhw;
                    vs[0] = sx;
                    vs[2] = sz;
                    vs[1] = sy;
                    rhw = M.F32(A + 0x2b4) + rhw;
                    vs[3] = rhw;
                    if (0f > rhw) return;
                    vs[2] = depth ? FxRenderer.DepthFromRhw_53b030(rhw) : 0f;
                }
                else
                {
                    float x = M.F32(v), y = M.F32(v + 4), z = M.F32(v + 8);
                    rt.Renderer.ProjectVertex(final, x, y, z, out float sx, out float sy, out float sz, out float rhw);
                    M.F32(v + 0xc, rhw);
                    M.F32(v, sx);
                    M.F32(v + 8, sz);
                    M.F32(v + 4, sy);
                    rhw = M.F32(A + 0x2b4) + rhw;
                    M.F32(v + 0xc, rhw);
                    if (0f > rhw) return;
                    M.F32(v + 8, depth ? FxRenderer.DepthFromRhw_53b030(rhw) : 0f);
                }
            }
            M.U32(A + 0xf8, M.U32(A + 0x18));
            M.U32(A + 0x108, M.U32(A + 0x2a8));
            M.U32(A + 0x10c, M.U32(A + 0x2ac));
            rt.Renderer.AddQuad(A + 0xf0, M.I32(A + 0x1c));
        }
    }

    public sealed partial class FxRenderer
    {
        /// <summary>ProjectVertex (0x53b370) with the 4x3 matrix held in an array (a stack matrix of the client); same operation order.</summary>
        public void ProjectVertex(float[] m, float vx, float vy, float vz, out float sx, out float sy, out float sz, out float rhw)
            => ProjectVertex(new ReadOnlySpan<float>(m), vx, vy, vz, out sx, out sy, out sz, out rhw);

        /// <summary>ProjectVertex (0x53b370) with the 4x3 matrix in a span (stack matrix); same operation order.</summary>
        public void ProjectVertex(ReadOnlySpan<float> m, float vx, float vy, float vz, out float sx, out float sy, out float sz, out float rhw)
        {
            if (m.Length < 12) throw new IndexOutOfRangeException();   // as indexing a short array did (lets the JIT drop the per-element checks)
            float den = m[5] * vy + m[2] * vx;
            den += m[8] * vz;
            den += m[11];
            rhw = 1.0f / den;

            float x = m[3] * vy + vx * m[0];
            float w = 1.0f / rhw;
            x += m[6] * vz;
            w -= 10.0f;
            x += m[9];
            w *= 1.0067114f;
            x *= rhw;
            w *= rhw;
            x *= XFactor;
            x += (float)CenterX;
            sx = x;

            float y = m[4] * vy + m[1] * vx;
            y += m[7] * vz;
            y += m[10];
            y *= rhw;
            y *= YFactor;
            sz = w;
            y += (float)CenterY;
            sy = y;
        }

        /// <summary>FUN_0053b030 — ((1 / rhw) - 10) * 1.0067114 * rhw in SSE single precision (returned through ST0).</summary>
        public static float DepthFromRhw_53b030(float rhw)
        {
            float v = 1.0f / rhw;
            v -= 10.0f;
            v *= 1.0067114f;
            v *= rhw;
            return v;
        }
    }

    /// <summary>
    /// 4x3 matrices held in C# arrays (the client builds them on the stack), with the exact operation order of
    /// the client's helpers: identity 0x1137438, FUN_005330d0 translation, FUN_00532b50 scale, FUN_005333e0 /
    /// FUN_00533370 rotation, FUN_00531280 M × RotY, FUN_005334c0 product.
    /// </summary>
    public static partial class Mat43
    {
        static readonly float[] I = { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };

        // The array forms allocate their result (kept for callers off the hot paths); the span forms write into a
        // caller-provided 12-float matrix (stackalloc) with the same arithmetic.

        public static float[] Identity() => new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };

        public static void SetIdentity(Span<float> r) => I.AsSpan().CopyTo(r);

        public static float[] Load(FxMemory m, uint a)
        {
            var r = new float[12];
            Load(m, a, r);
            return r;
        }

        public static void Load(FxMemory m, uint a, Span<float> r)
        {
            if (FxMemory.CanSpanF32(a)) m.F32Span(a, 12).CopyTo(r);   // one range check, same bits
            else for (int i = 0; i < 12; i++) r[i] = m.F32(a + (uint)i * 4);
        }

        public static void Store(FxMemory m, uint a, float[] v) => Store(m, a, new ReadOnlySpan<float>(v));

        public static void Store(FxMemory m, uint a, ReadOnlySpan<float> v)
        {
            for (int i = 0; i < 12; i++) m.F32(a + (uint)i * 4, v[i]);
        }

        /// <summary>FUN_005330d0 — identity with translation (x, y, z).</summary>
        public static float[] Translation(float x, float y, float z)
        {
            var r = new float[12];
            Translation(x, y, z, r);
            return r;
        }

        public static void Translation(float x, float y, float z, Span<float> r)
        {
            SetIdentity(r);
            r[9] = x; r[10] = y; r[11] = z;
        }

        /// <summary>FUN_00532b50 — identity with scale (x, y, z) on the diagonal.</summary>
        public static float[] Scale(float x, float y, float z)
        {
            var r = new float[12];
            Scale(x, y, z, r);
            return r;
        }

        public static void Scale(float x, float y, float z, Span<float> r)
        {
            SetIdentity(r);
            r[0] = x; r[4] = y; r[8] = z;
        }

        /// <summary>FUN_00533370 — rotation about X (x87 fsin/fcos of the float angle).</summary>
        public static float[] RotationX(float rad)
        {
            var r = new float[12];
            RotationX(rad, r);
            return r;
        }

        public static void RotationX(float rad, Span<float> r)
        {
            SetIdentity(r);
            float s = (float)Math.Sin(rad), c = (float)Math.Cos(rad);
            r[4] = c; r[8] = c; r[5] = s; r[7] = -s;
        }

        /// <summary>FUN_005333e0 — rotation about Y.</summary>
        public static float[] RotationY(float rad)
        {
            var r = new float[12];
            RotationY(rad, r);
            return r;
        }

        public static void RotationY(float rad, Span<float> r)
        {
            SetIdentity(r);
            float s = (float)Math.Sin(rad), c = (float)Math.Cos(rad);
            r[0] = c; r[8] = c; r[2] = -s; r[6] = s;
        }

        /// <summary>FUN_00531280 — m = m × RotY(rad) (same arithmetic as <see cref="FxMatrix.RotateY"/>).</summary>
        public static void RotateY(float[] m, float rad) => RotateY(new Span<float>(m), rad);

        public static void RotateY(Span<float> m, float rad) => RotateY(m, (float)Math.Sin(rad), (float)Math.Cos(rad));

        /// <summary><see cref="RotateY(Span{float}, float)"/> with sin / cos of the angle already computed.</summary>
        public static void RotateY(Span<float> m, float s, float c)
        {
            float ns = -s;
            float i1 = I[1], i3 = I[3], i4 = I[4], i5 = I[5], i7 = I[7], i9 = I[9], i10 = I[10], i11 = I[11];
            float m0 = m[0], m1 = m[1], m2 = m[2], m3 = m[3], m4 = m[4], m5 = m[5];
            float m6 = m[6], m7 = m[7], m8 = m[8], m9 = m[9], m10 = m[10], m11 = m[11];
            m[0] = m0 * c + m1 * i3 + m2 * s;
            m[1] = m0 * i1 + m1 * i4 + m2 * i7;
            m[2] = ns * m0 + m1 * i5 + m2 * c;
            m[3] = m3 * c + m4 * i3 + m5 * s;
            m[4] = m3 * i1 + m4 * i4 + m5 * i7;
            m[5] = ns * m3 + m4 * i5 + m5 * c;
            m[6] = m6 * c + m7 * i3 + m8 * s;
            m[7] = m6 * i1 + m7 * i4 + m8 * i7;
            m[8] = ns * m6 + m7 * i5 + m8 * c;
            m[9] = m9 * c + m10 * i3 + m11 * s + i9;
            m[10] = m9 * i1 + m10 * i4 + m11 * i7 + i10;
            m[11] = ns * m9 + m10 * i5 + m11 * c + i11;
        }

        /// <summary>FUN_005334c0 — a × b (rows of a times b's rotation, plus b's translation on row 3).</summary>
        public static float[] Mul(float[] a, float[] b)
        {
            var c = new float[12];
            Mul(a, b, c);
            return c;
        }

        /// <summary>
        /// <see cref="Mul(float[], float[])"/> into <paramref name="c"/>, which must not overlap a or b. Unrolled with the
        /// same per-element order: (a0·b_k + a1·b_3+k) + a2·b_6+k, plus b_9+k on row 3.
        /// </summary>
        public static void Mul(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c)
        {
            if (a.Length < 12 || b.Length < 12 || c.Length < 12) throw new IndexOutOfRangeException();   // as indexing a short array did
            float b0 = b[0], b1 = b[1], b2 = b[2], b3 = b[3], b4 = b[4], b5 = b[5];
            float b6 = b[6], b7 = b[7], b8 = b[8], b9 = b[9], b10 = b[10], b11 = b[11];
            for (int o = 0; o < 9; o += 3)
            {
                float a0 = a[o], a1 = a[o + 1], a2 = a[o + 2];
                float v0 = a0 * b0 + a1 * b3; v0 += a2 * b6;
                float v1 = a0 * b1 + a1 * b4; v1 += a2 * b7;
                float v2 = a0 * b2 + a1 * b5; v2 += a2 * b8;
                c[o] = v0; c[o + 1] = v1; c[o + 2] = v2;
            }
            {
                float a0 = a[9], a1 = a[10], a2 = a[11];
                float v0 = a0 * b0 + a1 * b3; v0 += a2 * b6; v0 += b9;
                float v1 = a0 * b1 + a1 * b4; v1 += a2 * b7; v1 += b10;
                float v2 = a0 * b2 + a1 * b5; v2 += a2 * b8; v2 += b11;
                c[9] = v0; c[10] = v1; c[11] = v2;
            }
        }
    }

    public sealed partial class FxRuntime
    {
        // ---- CEffectMemoryMgr (DAT_015127d8) -------------------------------------------------------

        public const uint VtCEffectMemoryMgr = 0x00ff651c;
        public const uint VtIEffectMemoryPool = 0x00ff6524;
        public const uint VtPoolCParticle = 0x00ff6534, VtPoolCSpriteParticle = 0x00ff6544,
            VtPoolCEZ2STRParticle = 0x00ff6554, VtPoolCMultiTexParticle = 0x00ff6564;

        /// <summary>DAT_015127d8 — CEffectMemoryMgr: +4 byte ready, +8/+0xc/+0x10 std::vector of four pools.</summary>
        public uint EffectMemoryMgr;

        /// <summary>FUN_00aa9b60 + FUN_00aa9850 — the memory manager with its four particle pools.</summary>
        void EnsureEffectMemoryMgr()
        {
            if (EffectMemoryMgr != 0) return;
            uint mgr = M.Alloc(0x14);
            M.U32(mgr, VtCEffectMemoryMgr);
            M.U32(mgr + 8, 0); M.U32(mgr + 0xc, 0); M.U32(mgr + 0x10, 0);
            M.U8(mgr + 4, 0);
            foreach (uint vt in new[] { VtPoolCParticle, VtPoolCSpriteParticle, VtPoolCEZ2STRParticle, VtPoolCMultiTexParticle })
            {
                uint pool = M.Alloc(0x18);
                M.U32(pool, vt);
                for (uint o = 4; o < 0x18; o += 4) M.U32(pool + o, 0);
                uint proxy = M.Alloc(8);
                M.U32(proxy + 4, 0);
                M.U32(pool + 4, proxy);
                M.U32(proxy, pool + 4);
                PtrVectorPushBack(mgr + 8, pool);
            }
            M.U8(mgr + 4, 1);
            EffectMemoryMgr = mgr;
        }

        uint Pool(int index) => M.U32(M.U32(EffectMemoryMgr + 8) + (uint)index * 4);

        /// <summary>
        /// FUN_00aaa6c0 — a CParticle from pool 0 (FUN_00aa9830(0) → VCParticle pool vf01 0xaa9650: pop the front
        /// of the free deque and reset it, or operator new(0x2bc) + ctor(0, 0, 0, -1)), then pool index 0 at
        /// +0x2b0, the position and the RP flags.
        /// </summary>
        public uint AllocParticle_aaa6c0(float x, float y, float z, int flags)
        {
            uint p = PoolAlloc(0);
            M.U32(p + 0x2b0, 0);
            M.F32(p + 0x24, x);
            M.F32(p + 0x28, y);
            M.I32(p + 0x1c, flags);
            M.F32(p + 0x2c, z);
            return p;
        }

        /// <summary>FUN_00aaa6c0 with the position read from a float[3] at <paramref name="pos"/> (the effect's +8).</summary>
        public uint AllocParticle_aaa6c0(uint pos, int flags) => AllocParticle_aaa6c0(M.F32(pos), M.F32(pos + 4), M.F32(pos + 8), flags);

        /// <summary>
        /// FUN_00aaa640 — a CEZ2STRParticle from pool 2 (VCEZ2STRParticle pool vf01 0xaa9470: reuse + FUN_00a65410, or
        /// operator new(0x3fe8) + ctor FUN_00a64cd0(0, 0, 0, -1)), pool index 2, position, RP flags.
        /// </summary>
        public uint AllocEz2StrParticle_aaa640(uint pos, int flags)
        {
            float x = M.F32(pos), y = M.F32(pos + 4), z = M.F32(pos + 8);
            uint p = PoolAlloc(2);
            M.U32(p + 0x2b0, 2);
            M.F32(p + 0x24, x);
            M.F32(p + 0x28, y);
            M.I32(p + 0x1c, flags);
            M.F32(p + 0x2c, z);
            return p;
        }

        uint PoolAlloc(int index)
        {
            uint pool = Pool(index);
            if (index > 2) throw new NotSupportedException($"particle pool {index} is not ported");
            if (M.U32(pool + 0x14) == 0)
            {
                if (index == 1)
                {
                    uint sp = M.Alloc(CSpriteParticle.Size);                  // VCSpriteParticle pool vf01 0xaa9740 (package mapfx)
                    CSpriteParticle.Construct_a689b0(M, sp, 0f, 0f, 0f, -1);
                    return sp;
                }
                if (index == 2)
                {
                    uint e = M.Alloc(CEZ2STRParticle.Size);
                    CEZ2STRParticle.Construct_a64cd0(this, e, 0f, 0f, 0f, -1);
                    return e;
                }
                uint p = M.Alloc(CParticle.Size);
                CParticle.Construct_a67480(M, p, 0f, 0f, 0f, -1);
                return p;
            }
            uint off = M.U32(pool + 0x10);
            uint block = (M.U32(pool + 0xc) - 1) & (off >> 2);
            uint particle = M.U32(M.U32(M.U32(pool + 8) + block * 4) + (off & 3) * 4);
            if (index == 2) new CEZ2STRParticle(M, particle).Reset_a65410();
            else if (index == 1) new CSpriteParticle(M, particle).Reset_a68ba0();
            else new CParticle(M, particle).Reset_a677e0();
            uint size = M.U32(pool + 0x14) - 1;
            M.U32(pool + 0x14, size);
            if (size == 0) M.U32(pool + 0x10, 0); else M.U32(pool + 0x10, off + 1);
            return particle;
        }

        /// <summary>FUN_00aaa740 — returns a particle to its pool (FUN_00aa9ed0 → pool vf02: deque push_back).</summary>
        public void FreeParticle_aaa740(uint particle)
        {
            int index = M.I32(particle + 0x2b0);
            if ((uint)index > 2) throw new NotSupportedException($"particle pool {index} is not ported");
            uint d = Pool(index) + 4;                          // the deque: +0 proxy, +4 map, +8 map size, +0xc offset, +0x10 size
            uint off = M.U32(d + 0xc), size = M.U32(d + 0x10);
            if (((off + size) & 3) == 0 && M.U32(d + 8) <= (size + 4) >> 2)
            {
                DequeGrowMap_aaa250(d, 1);
                off = M.U32(d + 0xc);
                size = M.U32(d + 0x10);
            }
            uint mapSize = M.U32(d + 8);
            off &= mapSize * 4 - 1;
            M.U32(d + 0xc, off);
            uint pos = size + off;
            uint slot = M.U32(d + 4) + ((mapSize - 1) & (pos >> 2)) * 4;
            if (M.U32(slot) == 0) M.U32(slot, M.Alloc(0x10));
            M.U32(M.U32(slot) + (pos & 3) * 4, particle);
            M.U32(d + 0x10, M.U32(d + 0x10) + 1);
        }

        /// <summary>FUN_00aaa250 — std::deque&lt;T*&gt;::_Growmap (4 elements per block).</summary>
        void DequeGrowMap_aaa250(uint d, uint count)
        {
            uint mapSize = M.U32(d + 8);
            uint newSize = mapSize != 0 ? mapSize : 1;
            uint grow;
            while ((grow = newSize - mapSize) < count || newSize < 8)
            {
                if (0xfffffffu - newSize < newSize) throw new InvalidOperationException("deque too long");
                newSize *= 2;
            }
            uint boff = M.U32(d + 0xc) >> 2;
            if (newSize > 0x3fffffff) throw new InvalidOperationException("deque too long");
            uint newMap = StdAllocate(newSize * 4);
            uint oldMap = M.U32(d + 4);
            uint tailBytes = (oldMap + mapSize * 4) - (oldMap + boff * 4);
            MemMove(newMap + boff * 4, oldMap + boff * 4, tailBytes);
            uint p = newMap + boff * 4 + tailBytes;
            if (boff <= grow)
            {
                MemMove(p, oldMap, boff * 4);
                M.Fill(p + boff * 4, 0, (int)((grow - boff) * 4));
                M.Fill(newMap, 0, (int)(boff * 4));
            }
            else
            {
                MemMove(p, oldMap, grow * 4);
                uint rest = oldMap + grow * 4;
                uint n = oldMap + boff * 4 - rest;
                MemMove(newMap, rest, n);
                M.Fill(newMap + n, 0, (int)(grow * 4));
            }
            if (oldMap != 0) StdDeallocate(oldMap, M.U32(d + 8) * 4);
            M.U32(d + 8, M.U32(d + 8) + grow);
            M.U32(d + 4, newMap);
        }

        void MemMove(uint dst, uint src, uint n)
        {
            if (n != 0) M.Copy(dst, src, (int)n);
        }
    }
}
