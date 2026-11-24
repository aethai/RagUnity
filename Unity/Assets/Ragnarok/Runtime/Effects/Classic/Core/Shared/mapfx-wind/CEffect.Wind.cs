// Synced from reference/port/src/RoClassicFx/Shared/mapfx-wind/CEffect.Wind.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CWindEffect (EF 2343, vtable 0xf821c4, 0x138 bytes; allocator VCWindEffect 0xfe1af8 → vf01 0xa49b90 → ctor
    /// FUN_006aacf0) — the map's wind streaks (package mapfx-wind; 191 RSW records on 38 maps). The world builder
    /// launches one per RSW record (ownerless, at the record's position); the map's EffectTool script then configures
    /// the k-th one through <c>SetWindEffectInfo</c> (FUN_00aa8100, <see cref="FxRuntime.SetWindEffectInfo_aa8100"/>),
    /// which also moves it (MSG_SET_POS) and rebuilds it (MSG_SET_INIT → vf07).
    ///
    /// Fields: u16 +0xcc streak count (4); byte +0xce alpha maximum (200; a streak starts at rand() % max and blinks
    /// between 0 and max), bytes +0xcf / +0xd0 / +0xd1 colour r, g, b (255, 100, 100); +0xd4 radius = the particle size
    /// (12); +0xd8 thickness (0.5); +0xdc height (50: streak i sits at height · i / (count − 1)); +0xe0 speed (5, degrees
    /// a tick); u16 +0xe4 full angle (200: segment j starts at j · full / 20 degrees ahead of the streak's random start);
    /// +0xe8 / +0xec / +0xf0 rotation (x, 0, z) in degrees; +0xf4 / +0xf8 src / dest blend (5, 7; −1 = keep the
    /// particle's); +0xfc std::string texture ("effect\cloud11.tga"); +0x114..+0x128 the previous segment's two points
    /// (end point, top point; carried from particle to particle, also across ticks); +0x12c std::vector&lt;CParticle*&gt;
    /// (count × 21 segments, streak-major).
    /// Particles (CParticle): +0xc segment index j (0..20), +0x10 streak index, +0x20 radius, +0x44 alpha, +0x50 full
    /// angle, +0x54 height, +0xa8 blink phase (0 rising, 1 falling), +0xc0 speed, +0xc4 angle (degrees).
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCWindEffect = 0x00f821c4;
        public const uint VtAllocWind = 0x00fe1af8;
        public const uint WindSize = 0x138;
        static readonly int[] WindIds = { 2343 };

        /// <summary>Segments per streak (the inner loop bound 0x15 of FUN_006aaff0 and the 21.0 of the render's u range).</summary>
        const int WindSegments = 0x15;

        // ---- construction / destruction -----------------------------------------------------------

        /// <summary>CWindEffect::CWindEffect (FUN_006aacf0): CEffect(0x927), the defaults above, the texture string, the vector.</summary>
        static uint ConstructWind_6aacf0(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, 0x927);
            M.U32(a, VtCWindEffect);
            M.U32(a + 0xcc, 0xffc80004);                                    // count 4, alpha max 200, red 255
            M.U16(a + 0xd0, 0x6464);                                        // green 100, blue 100
            M.U32(a + 0xd4, 0x41400000);                                    // radius 12
            M.U32(a + 0xd8, 0x3f000000);                                    // thickness 0.5
            M.U32(a + 0xdc, 0x42480000);                                    // height 50
            M.U32(a + 0xe0, 0x40a00000);                                    // speed 5
            M.U16(a + 0xe4, 200);                                           // full angle 200
            M.U32(a + 0xe8, 0);
            M.U32(a + 0xec, 0);
            M.U32(a + 0xf0, 0);
            M.I32(a + 0xf4, 5);
            M.I32(a + 0xf8, 7);
            rt.StrInitEmpty(a + 0xfc);                                      // then assign: 18 bytes → a 0x20 heap buffer
            rt.StrAssign(a + 0xfc, "effect\\cloud11.tga");
            for (uint o = 0x114; o < 0x12c; o += 4) M.U32(a + o, 0);
            M.U32(a + 0x12c, 0);
            M.U32(a + 0x130, 0);
            M.U32(a + 0x134, 0);
            return a;
        }

        /// <summary>CWindEffect::vf25_6aaf60 body: the particle vector (FUN_00678360), the texture string, CEffect.</summary>
        void WindDestruct_6aaf60(FxRuntime rt)
        {
            M.U32(A, VtCWindEffect);
            DestroyPtrVector_678360(rt, A + 0x12c);
            rt.StrDestroy(A + 0xfc);
            DestructBase_a75970(rt);
        }

        // ---- virtual methods ----------------------------------------------------------------------

        /// <summary>
        /// CWindEffect::vf01_6ab330 (a jump to FUN_006aaff0) — per streak i (count +0xcc): a random start angle
        /// rand() % 360 and start alpha rand() % alpha max (no rand() when the maximum is 0), then 21 segments from pool 0
        /// (RP flags 5) at the effect's position: alpha, colour, radius, height · i / (count − 1) (0 for a count below 2),
        /// speed, full angle, blends (unless −1), texture (FUN_00a67fe0 with a by-value copy of +0xfc), angle
        /// j · full / 20 + start, phase 0, unit quad, id / j / i / flag 1, appended to the vector.
        /// </summary>
        void WindInit_6aaff0(FxRuntime rt)
        {
            int i = 0;
            float fullStep = (float)M.U16(A + 0xe4) / 20.0f;                // cvtdq2ps, divss [20.0f]
            if (0 >= M.U16(A + 0xcc)) return;                               // CMP AX(0), [+0xcc]; JAE (unsigned)
            do
            {
                int r = rt.Rand.Next() % 360;
                int start = r < 0 ? 0 : r;                                  // TEST EDX; CMOVNS
                int alphaMax = M.U8(A + 0xce);
                int alpha0;
                if (alphaMax == 0) alpha0 = 0;
                else
                {
                    int r2 = rt.Rand.Next() % alphaMax;
                    alpha0 = r2 < 0 ? 0 : r2;
                }
                for (int j = 0; j < WindSegments; j++)
                {
                    uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                    if (p == 0) continue;
                    M.I32(p + 0x44, alpha0);
                    new CParticle(M, p).SetColor_a67f70(M.U8(A + 0xcf), M.U8(A + 0xd0), M.U8(A + 0xd1));
                    M.U32(p + 0x20, M.U32(A + 0xd4));
                    int count = M.U16(A + 0xcc);
                    float height;
                    if (1 >= count) height = 0f;                            // CMP CX(1), AX; JAE (unsigned)
                    else height = M.F32(A + 0xdc) / (float)(count - 1) * (float)i;
                    M.F32(p + 0x54, height);
                    M.U32(p + 0xc0, M.U32(A + 0xe0));
                    M.I32(p + 0x50, M.U16(A + 0xe4));
                    int src = M.I32(A + 0xf4);
                    if (src != -1) M.I32(p + 0x2a8, src);
                    int dst = M.I32(A + 0xf8);
                    if (dst != -1) M.I32(p + 0x2ac, dst);
                    SetTextureCopy_a67fe0(rt, p, A + 0xfc);
                    M.F32(p + 0xc4, (float)j * fullStep + (float)start);
                    M.U8(p + 0xa8, 0);
                    CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                    M.U32(p + 4, M.U32(A + 4));
                    M.I32(p + 0xc, j);
                    M.I32(p + 0x10, i);
                    M.I32(p + 8, 1);
                    rt.PtrVectorPushBack(A + 0x12c, p);
                }
                i++;
            } while (i < M.U16(A + 0xcc));
        }

        /// <summary>
        /// FUN_00a67fe0 (thiscall on a particle, the texture as a by-value std::string) called with a copy of the
        /// std::string at <paramref name="str"/>: the caller's copy construction (a heap buffer of (size | 15) + 1 bytes
        /// for 16 bytes and more), texture +0x18 = GetTexture(name), then the callee destroys the copy.
        /// </summary>
        static void SetTextureCopy_a67fe0(FxRuntime rt, uint p, uint str)
        {
            uint size = (uint)rt.StrSize(str);
            uint data = rt.StrData(str);
            uint buf = rt.StrLocalAssign(data, size);
            rt.M.U32(p + 0x18, rt.GetTexture(rt.ReadCString(data)));
            rt.StrLocalDestroy(size, buf);
        }

        /// <summary>
        /// CWindEffect::vf03_6ab360 — per segment: the blink (phase 0: alpha +1 until the maximum, then phase 1; phase 1:
        /// alpha −1 down to 0, then phase 0), the angle += speed (−360 once above 360), then two points in the streak's
        /// frame — end (radius·cos + 0, radius·0 − height, sin·radius + 0) and top (end + ((cos + 0)·thickness,
        /// thickness, (sin + 0)·thickness)) with cos / sin of the angle (x87 fcos / fsin of the float, FUN_00534e50 /
        /// FUN_00534e60) — through RotX(+0xe8) × RotZ(+0xf0) × Translation(effect position) into vertices 3 (+0x258) and 1
        /// (+0x218); vertices 0 (+0x1f8) and 2 (+0x238) take the previous segment's top and end points (+0x120 / +0x114)
        /// unless j is 0; the points are then kept in +0x114..+0x128 for the next particle of the vector.
        /// </summary>
        void WindProcess_6ab360(FxRuntime rt)
        {
            Span<float> m = stackalloc float[12];
            for (uint pp = M.U32(A + 0x12c); pp != M.U32(A + 0x130); pp += 4)
            {
                uint p = M.U32(pp);
                if (p == 0) continue;
                unchecked
                {
                    sbyte phase = M.I8(p + 0xa8);                           // MOVSX; SUB 0 / SUB 1
                    if (phase == 0)
                    {
                        int a = M.I32(p + 0x44) + 1;
                        int max = M.U8(A + 0xce);
                        if (!(max > a))                                     // CMP ECX(max), EAX; JG store
                        {
                            a = max;
                            M.U8(p + 0xa8, 1);
                        }
                        M.I32(p + 0x44, a);
                    }
                    else if (phase == 1)
                    {
                        int a = M.I32(p + 0x44) - 1;
                        if (!(a > 0))                                       // TEST EAX, EAX; JG store
                        {
                            a = 0;
                            M.U8(p + 0xa8, 0);
                        }
                        M.I32(p + 0x44, a);
                    }
                }
                float angle = M.F32(p + 0xc0) + M.F32(p + 0xc4);
                if (angle > 360.0f) angle -= 360.0f;                       // COMISS 360; JBE (NaN keeps)
                M.F32(p + 0xc4, angle);
                float cos = (float)Math.Cos((angle / 180.0f) * 3.141592f);  // FUN_00534e50: fld, fcos, fstp dword
                float sin = (float)Math.Sin((M.F32(p + 0xc4) / 180.0f) * 3.141592f);   // FUN_00534e60: fsin
                float size = M.F32(p + 0x20);
                float t = M.F32(A + 0xd8);
                float v0y = size * 0f - M.F32(p + 0x54);
                float v0z = sin * size + 0f;
                float v0x = size * cos + 0f;
                float v1y = t + v0y;
                float v1z = (sin + 0f) * t + v0z;
                float v1x = (cos + 0f) * t + v0x;
                Mat43.RotationX((M.F32(A + 0xe8) / 180.0f) * 3.141592f, m);  // FUN_00533370 on the stack matrix
                Mat43.RotateZ(m, (M.F32(A + 0xf0) / 180.0f) * 3.141592f);    // FUN_00531580
                Mat43.Translate_530cb0(m, M.F32(A + 8), M.F32(A + 0xc), M.F32(A + 0x10));
                float p0x = ((v0y * m[3] + v0x * m[0]) + v0z * m[6]) + m[9];
                float p0y = ((v0y * m[4] + v0x * m[1]) + v0z * m[7]) + m[10];
                float p0z = ((v0y * m[5] + v0x * m[2]) + v0z * m[8]) + m[11];
                float p1x = ((v1y * m[3] + v1x * m[0]) + v1z * m[6]) + m[9];
                float p1y = ((v1y * m[4] + v1x * m[1]) + v1z * m[7]) + m[10];
                float p1z = ((v1y * m[5] + v1x * m[2]) + v1z * m[8]) + m[11];
                M.F32(p + 0x218, p1x);
                M.F32(p + 0x25c, p0y);
                M.F32(p + 0x21c, p1y);
                M.F32(p + 0x258, p0x);
                M.F32(p + 0x260, p0z);
                M.F32(p + 0x220, p1z);
                if (M.I32(p + 0xc) != 0)
                {
                    M.U32(p + 0x1f8, M.U32(A + 0x120));
                    M.U32(p + 0x1fc, M.U32(A + 0x124));
                    M.U32(p + 0x200, M.U32(A + 0x128));
                    M.U32(p + 0x238, M.U32(A + 0x114));
                    M.U32(p + 0x23c, M.U32(A + 0x118));
                    M.U32(p + 0x240, M.U32(A + 0x11c));
                }
                M.F32(A + 0x11c, p0z);
                M.F32(A + 0x114, p0x);
                M.F32(A + 0x118, p0y);
                M.F32(A + 0x120, p1x);
                M.F32(A + 0x124, p1y);
                M.F32(A + 0x128, p1z);
            }
        }

        /// <summary>
        /// CWindEffect::vf04_6abb10 — every segment with j &gt; 0 through FUN_00a67b30 {fixed matrix 1, v 0..1,
        /// u (j − 1) / 21 .. j / 21, 0, z on}: the vertices are world positions already (the particle's +0x1c8 stays
        /// the identity of its reset), the texture stretched along the streak.
        /// </summary>
        void WindRender_6abb10(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0x12c); pp != M.U32(A + 0x130); pp += 4)
            {
                uint p = M.U32(pp);
                if (p == 0) continue;
                int j = M.I32(p + 0xc);
                if (j <= 0) continue;
                float u0 = (float)(j - 1) / 21.0f;
                float u1 = (float)j / 21.0f;
                new CParticle(M, p).RenderStrip_a67b30(rt, true, 0f, 1.0f, u0, u1, true);
            }
        }

        /// <summary>CWindEffect::vf07_6ab340 — vf05 (particles back to the pool), then vf01 (FUN_006aaff0) again.</summary>
        void WindReinit_6ab340(FxRuntime rt)
        {
            Vf05ReleaseParticles(rt);
            WindInit_6aaff0(rt);
        }
    }

    public static partial class Mat43
    {
        /// <summary>FUN_00531580 — m = m × RotZ(rad) (same arithmetic as <see cref="FxMatrix.RotateZ"/>).</summary>
        public static void RotateZ(Span<float> m, float rad)
        {
            float s = (float)Math.Sin(rad), c = (float)Math.Cos(rad), ns = -s;
            float i2 = I[2], i5 = I[5], i6 = I[6], i7 = I[7], i8 = I[8], i9 = I[9], i10 = I[10], i11 = I[11];
            float m7 = m[7], m0 = m[0], m1 = m[1], m6 = m[6], m5 = m[5];
            float m10 = m[10], m2 = m[2], m9 = m[9], m3 = m[3], m4 = m[4];
            m[0] = ns * m1 + m0 * c + m2 * i6;
            m[1] = m0 * s + m1 * c + m2 * i7;
            m[2] = m0 * i2 + m1 * i5 + m2 * i8;
            m[3] = ns * m4 + m3 * c + m5 * i6;
            float m8 = m[8];
            m[5] = m3 * i2 + m4 * i5 + m5 * i8;
            m[4] = m3 * s + m4 * c + m5 * i7;
            m[6] = ns * m7 + m6 * c + m8 * i6;
            float m11 = m[11];
            m[7] = m6 * s + m7 * c + m8 * i7;
            m[8] = m6 * i2 + m7 * i5 + m8 * i8;
            m[9] = ns * m10 + m9 * c + m11 * i6 + i9;
            m[10] = m9 * s + m10 * c + m11 * i7 + i10;
            m[11] = m9 * i2 + m10 * i5 + m11 * i8 + i11;
        }

        /// <summary>
        /// FUN_00530cb0 (thiscall on a 4x3 matrix, the position as a float[3] pointer) — m = m × Translation(x, y, z)
        /// with the identity constant (0x1137438) as the rotation part: element (r, c) = (m[r,0]·I[c] + m[r,1]·I[3 + c])
        /// + m[r,2]·I[6 + c], plus the position on row 3 (the ×0 / ×1 terms kept, as <c>CEffect.Translate_530cb0</c>).
        /// </summary>
        public static void Translate_530cb0(Span<float> m, float x, float y, float z)
        {
            for (int r = 0; r < 4; r++)
            {
                float a0 = m[r * 3], a1 = m[r * 3 + 1], a2 = m[r * 3 + 2];
                for (int c = 0; c < 3; c++)
                {
                    float e = a0 * I[c] + a1 * I[3 + c];
                    e += a2 * I[6 + c];
                    if (r == 3) e += c == 0 ? x : c == 1 ? y : z;
                    m[r * 3 + c] = e;
                }
            }
        }
    }
}
