// Synced from reference/port/src/RoClassicFx/Shared/mapfx-wind/CEffect.MagicFloor.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CMagicFloorEffect (EF 1025, vtable 0xfe2224, 0x114 bytes; allocator VCMagicFloorEffect 0xfe1768 → vf01 0xa48620
    /// → ctor FUN_00a6fe80) — a magic circle on the floor with rising cloud rings (package mapfx-wind; 38 RSW records on
    /// 9 maps). The world builder launches one per RSW record (ownerless, at the record's position); the map's EffectTool
    /// script configures the k-th one through <c>SetMagicFloorInfo</c> (FUN_00aa7db0,
    /// <see cref="FxRuntime.SetMagicFloorInfo_aa7db0"/>), which writes CEffect fields +0x44..+0xac and re-inits the circle
    /// (MSG_SET_INIT → vf07).
    ///
    /// Fields: +0x44 "area" (float, MSG 16 — never read), +0x48 size (float, MSG 42: the circle's quad size and the
    /// ring radius), +0x4c start angle (int, MSG 17), +0x50 rise angle (float of an int, MSG 18), +0x54 alpha (int, MSG
    /// 19), +0x58 full display angle (int, MSG 20 — never read), +0x5c..+0xac heights 0..20 (float of an int, MSG 21..41 —
    /// never read); +0xcc..+0xe0 the previous ring segment's two points (inner, outer; carried from particle to particle and
    /// across ticks), +0xe4..+0xf8 zeroed by the ctor (unused), +0xfc std::vector&lt;CParticle*&gt; circle (6 quads:
    /// effect\hero_011, 001, 021, 002, 031, 003.tga), +0x108 std::vector&lt;CParticle*&gt; rings (2 sets × 4 rings × 21
    /// segments of effect\cloud11.tga).
    /// Particles: +0xc index (circle: 0..5, ring: segment j), +0x10 ring 0..3, +0x20 size, +0x44 alpha, +0x4c angle
    /// (ring: tilt 80..100 degrees), +0x50 ring rise (30, +3 a tick up to 180), +0x54 ring width (1.0), +0xc0 ring
    /// height (48 / 28 − 5·ring), +0xc4 angle (degrees).
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCMagicFloorEffect = 0x00fe2224;
        public const uint VtAllocMagicFloor = 0x00fe1768;
        public const uint MagicFloorSize = 0x114;
        static readonly int[] MagicFloorIds = { 1025 };

        /// <summary>The six local std::strings of FUN_00a70060, in order (19 bytes each: a 0x20 heap buffer).</summary>
        static readonly string[] MagicFloorCircleTextures =
        {
            "effect\\hero_011.tga", "effect\\hero_001.tga", "effect\\hero_021.tga",
            "effect\\hero_002.tga", "effect\\hero_031.tga", "effect\\hero_003.tga",
        };

        // ---- construction / destruction -----------------------------------------------------------

        /// <summary>CMagicFloorEffect::CMagicFloorEffect (FUN_00a6fe80): CEffect(0x401), +0xcc..+0xfb zeroed, the two vectors empty.</summary>
        static uint ConstructMagicFloor_a6fe80(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, 0x401);
            M.U32(a, VtCMagicFloorEffect);
            for (uint o = 0xcc; o < 0xfc; o += 4) M.U32(a + o, 0);
            for (uint o = 0xfc; o < 0x114; o += 4) M.U32(a + o, 0);
            return a;
        }

        /// <summary>CMagicFloorEffect::vf25_a6ffd0 body: ring vector, circle vector (FUN_00678360), CEffect.</summary>
        void MagicFloorDestruct_a6ffd0(FxRuntime rt)
        {
            M.U32(A, VtCMagicFloorEffect);
            DestroyPtrVector_678360(rt, A + 0x108);
            DestroyPtrVector_678360(rt, A + 0xfc);
            DestructBase_a75970(rt);
        }

        // ---- virtual methods ----------------------------------------------------------------------

        /// <summary>CMagicFloorEffect::vf01_a70a20 — the circle (FUN_00a70060), then the two ring sets (FUN_00a704e0(0), (1)).</summary>
        void MagicFloorInit_a70a20(FxRuntime rt)
        {
            MagicFloorCircle_a70060(rt);
            MagicFloorRings_a704e0(rt, 0);
            MagicFloorRings_a704e0(rt, 1);
        }

        /// <summary>
        /// FUN_00a70060 — six local std::strings (five allocated directly with FUN_004d89d0(0x20), the sixth assigned:
        /// 0x20 bytes each), then six particles from pool 0 (RP flags 5) at the effect: texture FUN_00a67fe0 with a by-value
        /// copy of string i, id, index i, ring 0, flag 1, alpha 255, angle 0, +0x4c 0, size 30, unit quad, appended to
        /// +0xfc; finally the strings are destroyed in reverse order (`eh vector destructor iterator`).
        /// </summary>
        void MagicFloorCircle_a70060(FxRuntime rt)
        {
            var bufs = new uint[MagicFloorCircleTextures.Length];
            for (int k = 0; k < bufs.Length; k++) bufs[k] = rt.StrLocalAssign(MagicFloorCircleTextures[k]);
            for (int i = 0; i < 6; i++)
            {
                uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                if (p == 0) continue;
                string name = MagicFloorCircleTextures[i];
                uint copy = rt.StrLocalAssign(name);                        // the by-value argument
                M.U32(p + 0x18, rt.GetTexture(name));
                rt.StrLocalDestroy(name, copy);
                M.U32(p + 4, M.U32(A + 4));
                M.I32(p + 0xc, i);
                M.I32(p + 0x10, 0);
                M.I32(p + 8, 1);
                M.I32(p + 0x44, 0xff);
                M.U32(p + 0xc4, 0);
                M.I32(p + 0x4c, 0);
                M.U32(p + 0x20, 0x41f00000);                                // 30.0
                CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                rt.PtrVectorPushBack(A + 0xfc, p);
            }
            for (int k = bufs.Length - 1; k >= 0; k--) rt.StrLocalDestroy(MagicFloorCircleTextures[k], bufs[k]);
        }

        /// <summary>
        /// FUN_00a704e0 (thiscall, RET 4) — four rings of 21 segments (pool 0, RP flags 5, at the effect): per ring a random
        /// start angle rand() % 360; per segment texture effect\cloud11.tga (FUN_00a67fb0), id, segment j, ring, flag 1,
        /// alpha 200, rise 30, angle = start, tilt 80 + rand() % 21, width 1.0, colour (255, 100, 100); set 0: height
        /// 48 − 5·ring, size 11; set 1: height 28 − 5·ring, size 22; unit quad; appended to +0x108. A segment whose
        /// particle the pool can't give draws no rand().
        /// </summary>
        void MagicFloorRings_a704e0(FxRuntime rt, int set)
        {
            for (int ring = 0; ring < 4; ring++)
            {
                int r = rt.Rand.Next() % 360;
                int start = r < 0 ? 0 : r;
                for (int j = 0; j < 0x15; j++)
                {
                    uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                    if (p == 0) continue;
                    var cp = new CParticle(M, p);
                    cp.SetTexture_a67fb0(rt, "effect\\cloud11.tga");
                    M.U32(p + 4, M.U32(A + 4));
                    M.I32(p + 0xc, j);
                    M.I32(p + 0x10, ring);
                    M.I32(p + 8, 1);
                    M.I32(p + 0x44, 200);
                    M.I32(p + 0x50, 0x1e);
                    M.F32(p + 0xc4, (float)start);
                    int r2 = rt.Rand.Next() % 0x15;
                    M.I32(p + 0x4c, (r2 < 0 ? 0 : r2) + 0x50);
                    M.U32(p + 0x54, 0x3f800000);
                    cp.SetColor_a67f70(0xff, 100, 100);
                    if (set == 0)
                    {
                        M.F32(p + 0xc0, 48.0f - (float)ring * 5.0f);
                        M.U32(p + 0x20, 0x41300000);                        // 11.0
                    }
                    else if (set == 1)
                    {
                        M.F32(p + 0xc0, 28.0f - (float)ring * 5.0f);
                        M.U32(p + 0x20, 0x41b00000);                        // 22.0
                    }
                    CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                    rt.PtrVectorPushBack(A + 0x108, p);
                }
            }
        }

        /// <summary>
        /// CMagicFloorEffect::vf03_a70b90 — circle quads: position = effect; angle +1 (quads 0, 1), +2 (2, 3), −1 (4, 5)
        /// truncated and wrapped into 0..359 (one step), matrix +0x1c8 = Scale(size, size, 1) × (RotZ(angle) × RotX(90°))
        /// (flat on the ground, FUN_00533450 / FUN_00530f80 / FUN_00532b50 / FUN_005334c0); age +1. Ring segments: position =
        /// effect; rise +3 up to 180; angle = (int)(angle + 5) % 360; alpha: 50 at age % 300 == 0, −1 (≥ 0) once
        /// age % 300 &gt; 100, +1 while age % 300 &lt; 12; a = ((int)(rise / 20) · j + angle) % 360; inner point
        /// (size·cos a, −height, size·sin a) and outer point (inner.x + cos a·(width·cos tilt), −height − width·sin tilt,
        /// inner.z + sin a·(width·cos tilt)) (the client's sin / cos tables) into vertices 3 (+0x258) and 1 (+0x218);
        /// vertices 0 / 2 take the previous segment's outer / inner points (+0xd8 / +0xcc) unless j is 0; age +1.
        /// </summary>
        void MagicFloorProcess_a70b90(FxRuntime rt)
        {
            Span<float> rot = stackalloc float[12];
            Span<float> scale = stackalloc float[12];
            Span<float> res = stackalloc float[12];
            for (uint pp = M.U32(A + 0xfc); pp != M.U32(A + 0x100); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));                              // written before the null test (as the client)
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    int idx = M.I32(p + 0xc);
                    int deg = 0;
                    if ((uint)idx <= 5)                                     // CMP EDX, 5; JA (jump table 0xa70fdc)
                    {
                        float f = idx <= 1 ? M.F32(p + 0xc4) + 1.0f : idx <= 3 ? M.F32(p + 0xc4) + 2.0f : M.F32(p + 0xc4) - 1.0f;
                        deg = FxOps.Cvtt(f);
                        if (deg >= 0x168) deg = unchecked(deg - 0x168);
                        else if (deg < 0) deg += 0x168;
                    }
                    float fdeg = (float)deg;
                    M.F32(p + 0xc4, fdeg);
                    Mat43.RotationZ((fdeg / 180.0f) * 3.141592f, rot);       // identity copied first, then FUN_00533450
                    Mat43.RotateX(rot, BitConverter.Int32BitsToSingle(0x3fc90fd8));   // FUN_00530f80(π/2 as float)
                    float size = M.F32(p + 0x20);
                    Mat43.Scale(size, size, 1.0f, scale);                    // FUN_00532b50
                    Mat43.Mul(scale, rot, res);                              // FUN_005334c0(&scale, &rot)
                    Mat43.Store(M, p + 0x1c8, res);
                }
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
            for (uint pp = M.U32(A + 0x108); pp != M.U32(A + 0x10c); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    unchecked
                    {
                        int angle = FxOps.Cvtt(M.F32(p + 0xc4) + 5.0f) % 0x168;
                        int rise = M.I32(p + 0x50) + 3;
                        if (rise > 0xb4) rise = 0xb4;                       // CMOVG
                        M.I32(p + 0x50, rise);
                        int alpha = M.I32(p + 0x44);
                        int phase = M.I32(p + 0xe8) % 300;
                        float fangle = (float)angle;
                        M.F32(p + 0xc4, fangle);
                        int a;
                        if (phase == 0) a = 0x32;
                        else
                        {
                            a = alpha;
                            if (phase > 100)
                            {
                                a = alpha - 1;
                                if (a < 0) a = 0;                           // ADD EDX, −1; CMOVS
                            }
                        }
                        M.I32(p + 0x44, phase >= 12 ? a : a + 1);
                        int j = M.I32(p + 0xc);
                        float width = M.F32(p + 0x54);
                        int k = FxOps.Cvtt((float)rise / 20.0f) * j;
                        int at = FxOps.Cvtt((float)k + fangle) % 0x168;
                        int tilt = M.I32(p + 0x4c);
                        float wc = width * FxMath.CosTableAt(tilt);
                        float cosA = FxMath.CosTableAt(at);
                        float sinA = FxMath.SinTableAt(at);
                        float ws = width * FxMath.SinTableAt(tilt);
                        float dx = cosA * wc;
                        float dz = sinA * wc;
                        float size = M.F32(A + 0x48);
                        float iz = size * sinA;
                        float ix = size * cosA;
                        float iy = -M.F32(p + 0xc0);                         // XORPS sign
                        M.F32(p + 0x258, ix);
                        float ox = ix + dx;
                        float oy = iy - ws;
                        float oz = iz + dz;
                        M.F32(p + 0x25c, iy);
                        M.F32(p + 0x260, iz);
                        M.F32(p + 0x218, ox);
                        M.F32(p + 0x21c, oy);
                        M.F32(p + 0x220, oz);
                        if (j != 0)
                        {
                            M.U32(p + 0x1f8, M.U32(A + 0xd8));
                            M.U32(p + 0x1fc, M.U32(A + 0xdc));
                            M.U32(p + 0x200, M.U32(A + 0xe0));
                            M.U32(p + 0x238, M.U32(A + 0xcc));
                            M.U32(p + 0x23c, M.U32(A + 0xd0));
                            M.U32(p + 0x240, M.U32(A + 0xd4));
                        }
                        M.F32(A + 0xcc, ix);
                        M.F32(A + 0xd0, iy);
                        M.F32(A + 0xd4, iz);
                        M.F32(A + 0xd8, ox);
                        M.F32(A + 0xdc, oy);
                        M.F32(A + 0xe0, oz);
                    }
                }
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
        }

        /// <summary>
        /// CMagicFloorEffect::vf04_a713c0 — the circle quads through FUN_00a67b60 (camera-turned: +0x1c8 × RotY(camera
        /// longitude) × Translation(position); v 0..1, u 0..1, z on), then every ring segment with j &gt; 0 through
        /// FUN_00a67b30 {camera-turned, v 0..1, u (j − 1) / 21 .. j / 21, 0, z on}.
        /// </summary>
        void MagicFloorRender_a713c0(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0xfc); pp != M.U32(A + 0x100); pp += 4)
            {
                uint p = M.U32(pp);
                if (p == 0) continue;
                new CParticle(M, p).Render_a67b60(rt, false, 0f, 1.0f, 0f, 1.0f, true);
            }
            for (uint pp = M.U32(A + 0x108); pp != M.U32(A + 0x10c); pp += 4)
            {
                uint p = M.U32(pp);
                if (p == 0) continue;
                int j = M.I32(p + 0xc);
                if (j <= 0) continue;
                float u0 = (float)(j - 1) / 21.0f;
                float u1 = (float)j / 21.0f;
                new CParticle(M, p).RenderStrip_a67b30(rt, false, 0f, 1.0f, u0, u1, true);
            }
        }

        /// <summary>
        /// CMagicFloorEffect::vf07_a70ad0 (SetMagicFloorInfo MSG_SET_INIT) — every circle quad: RP flags 5, position =
        /// effect, size = +0x48, angle = (float)+0x4c, +0x4c = (int)((float)ring · +0x50) (ring is 0: 0), alpha = +0x54,
        /// blends 5 / 7, unit quad, age 0. The rings are not touched.
        /// </summary>
        void MagicFloorReinit_a70ad0(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0xfc); pp != M.U32(A + 0x100); pp += 4)
            {
                uint p = M.U32(pp);
                M.I32(p + 0x1c, 5);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                M.U32(p + 0x20, M.U32(A + 0x48));
                M.F32(p + 0xc4, (float)M.I32(A + 0x4c));
                M.I32(p + 0x4c, FxOps.Cvtt((float)M.I32(p + 0x10) * M.F32(A + 0x50)));
                M.I32(p + 0x44, M.I32(A + 0x54));
                M.I32(p + 0x2a8, 5);
                M.I32(p + 0x2ac, 7);
                CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                M.I32(p + 0xe8, 0);
            }
        }
    }
}
