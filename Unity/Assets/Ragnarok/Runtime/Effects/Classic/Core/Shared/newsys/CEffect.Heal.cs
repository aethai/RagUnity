// Synced from reference/port/src/RoClassicFx/Shared/newsys/CEffect.Heal.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CHealEffect (vtable 0xff4b48, 0x12c bytes; allocator VCHealEffect) — the rising rings and sparkles of
    /// Heal (EF 312/313/325 by amount, 320 against undead), Absorb Spirits (253) and the 1113/1143/1144
    /// variants. Two ring layers (particle vectors +0x108 and +0x114, each ring a strip of 21 quads whose
    /// previous corners are carried in +0xcc..+0xe0 / +0xe4..+0xf8, fading from tick +0xfc / +0x100) and falling
    /// sparkles (+0x120). The spawner counts its own ticks in +0x104. Lifetime 100 ticks (vf01).
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCHealEffect = 0x00ff4b48;
        public const uint VtAllocHeal = 0x00fe1894;
        public const uint HealSize = 0x12c;
        static readonly int[] HealIds = { 253, 312, 313, 320, 325, 1113, 1143, 1144 };

        const string RingBlue = "effect\\ring_blue.tga", RingWhite = "effect\\ring_white.tga", AlphaDown = "effect\\alpha_down.tga";
        const string Pok1 = "effect\\pok1.tga", Pok3 = "effect\\pok3.tga";

        /// <summary>CHealEffect::CHealEffect (FUN_00a8ea70).</summary>
        static uint ConstructHeal_a8ea70(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, -1);
            M.U32(a, VtCHealEffect);
            for (uint o = 0xcc; o < 0xfc; o += 4) M.U32(a + o, 0);
            for (uint o = 0x108; o < 0x12c; o += 4) M.U32(a + o, 0);
            M.U32(a + 0x104, 0);
            return a;
        }

        /// <summary>CHealEffect::vf12_a8fee0 — start sound: 흡기.wav for 253, _heal_effect.wav for 312/313/320/325.</summary>
        void HealStart_a8fee0(FxRuntime rt)
        {
            switch (Id)
            {
                case 0xfd: AddSound_a75aa0(rt, 0, "effect\\Èí±â.wav", 0, 0f); break;
                case 0x138: case 0x139: case 0x140: case 0x145: AddSound_a75aa0(rt, 0, "_heal_effect.wav", 0, 0f); break;
            }
        }

        /// <summary>
        /// CHealEffect::vf03_a908c0 — spawner (FUN_00a8ff90), then the three particle layers: the rings are rebuilt
        /// around the owner every tick (FUN_00a908c0's two loops), the sparkles fall and turn (FUN_00a8f6f0).
        /// </summary>
        void HealProcess_a908c0(FxRuntime rt)
        {
            HealSpawn_a8ff90(rt);
            HealRingLayer(rt, A + 0x108, A + 0xfc, A + 0xcc, false);
            HealRingLayer(rt, A + 0x114, A + 0x100, A + 0xe4, true);
            uint vec = A + 0x120;
            for (uint pp = M.U32(vec); pp != M.U32(vec + 4); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    int a50 = M.I32(p + 0x50);
                    M.I32(p + 0x50, a50 + (a50 - 5 >= 0 ? -5 : 0x163));
                    int alpha = M.I32(p + 0x44);
                    if (M.I32(p + 0xe8) <= 0xa) alpha += 0xf;
                    else if (Id != 0x138)
                    {
                        int i = M.I32(p + 0xc), age = M.I32(p + 0xe8);
                        bool pok3 = (5 - i) * 7 == age || 0x24 - i * 7 == age;
                        new CParticle(M, p).SetTexture_a67fb0(rt, pok3 ? Pok3 : Pok1);
                    }
                    int na = alpha - 3;
                    M.I32(p + 0x44, na >= 0 ? na : 0);
                    M.F32(p + 0xd0, M.F32(p + 0xd0) - 0f);
                    M.F32(p + 0xd4, M.F32(p + 0xd4) - M.F32(p + 0x54));
                    M.F32(p + 0xd8, M.F32(p + 0xd8) - 0f);
                }
                HealSparkleQuad_a8f6f0(rt, p);
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
        }

        /// <summary>
        /// One ring layer of FUN_00a908c0: alpha fades in (+5 up to 180 in the first 16 ticks) and out (−2 from the
        /// layer's fade tick on), the radius +0xc0 wobbles with the sine table (0.25 · r · sin + 0.75 · r, times
        /// sin(age) for the first 90 ticks, clamped at 0 with MAXSS), the segment angle turns with +0x50 / 20; the
        /// quad spans the previous segment's corners (kept in <paramref name="prev"/>: inner at +0, outer at +0xc)
        /// and this one's (inner = scale · (cos, 0, sin), outer = inner + radius · (cos(+0x4c) · (cos, sin), −sin(+0x4c))).
        /// </summary>
        void HealRingLayer(FxRuntime rt, uint vec, uint fadeTick, uint prev, bool second)
        {
            for (uint pp = M.U32(vec); pp != M.U32(vec + 4); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    int fade = M.I32(fadeTick);
                    int age = M.I32(p + 0xe8);
                    int alpha = M.I32(p + 0x44);
                    if (fade >= 0 && fade <= age)
                    {
                        int a = alpha - 2;
                        alpha = a >= 0 ? a : 0;
                    }
                    else if (age < 0x10)
                    {
                        alpha += 5;
                        if (alpha > 0xb4) alpha = 0xb4;
                    }
                    M.I32(p + 0x44, alpha);
                    int i = M.I32(p + 0xc);
                    int wobble = second ? ((i * 0x11 + age) * 2) % 0x168 : ((4 - M.I32(p + 0x10)) * age + i * 0x22) % 0x168;
                    float r = M.F32(p + 0xc0);
                    float radius = r * 0.25f * FxMath.SinTableAt(wobble) + r * 0.75f;
                    if (age <= 0x5a) radius *= FxMath.SinTableAt(age);
                    float rr = 0f > radius ? 0f : radius;                            // MAXSS 0, radius
                    float step = (float)M.I32(p + 0x50) / 20.0f;
                    float angF = (float)(FxOps.Cvtt(step) * i);
                    angF += M.F32(p + 0xc4);
                    int ang = FxOps.Cvtt(angF) % 0x168;
                    int tilt = M.I32(p + 0x4c);
                    float rc = rr * FxMath.CosTableAt(tilt);
                    float c = FxMath.CosTableAt(ang), s = FxMath.SinTableAt(ang);
                    float rs = rr * FxMath.SinTableAt(tilt);
                    float rcc = rc * c, rcs = rc * s;
                    float scale = M.F32(p + 0x20);
                    float innerZ = scale * s;
                    M.U32(p + 0x25c, 0);
                    float innerX = scale * c;
                    M.F32(p + 0x260, innerZ);
                    M.F32(p + 0x258, innerX);
                    float outerX = innerX + rcc;
                    float outerY = 0f - rs;
                    float outerZ = innerZ + rcs;
                    M.F32(p + 0x218, outerX);
                    M.F32(p + 0x21c, outerY);
                    M.F32(p + 0x220, outerZ);
                    if (i != 0)
                    {
                        M.U32(p + 0x1f8, M.U32(prev + 0xc));
                        M.U32(p + 0x1fc, M.U32(prev + 0x10));
                        M.U32(p + 0x200, M.U32(prev + 0x14));
                        M.U32(p + 0x238, M.U32(prev));
                        M.U32(p + 0x23c, M.U32(prev + 4));
                        M.U32(p + 0x240, M.U32(prev + 8));
                    }
                    M.F32(prev + 0xc, outerX);
                    M.F32(prev + 0x10, outerY);
                    M.F32(prev, innerX);
                    M.U32(prev + 4, 0);
                    M.F32(prev + 8, innerZ);
                    M.F32(prev + 0x14, outerZ);
                }
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
        }

        /// <summary>
        /// FUN_00a8f6f0 — a sparkle's corners: the map name (CGameMode +0x68, as a std::string) equal to
        /// "ecl_fild01.rsw" caps its alpha at 100; the square of half-size +0x20 turned by +0x50 is faced to the
        /// camera (FUN_00a91590) and moved to the local position +0xd0..+0xd8.
        /// </summary>
        void HealSparkleQuad_a8f6f0(FxRuntime rt, uint p)
        {
            if (p == 0) return;
            // the std::string of the map name, compared in place (no managed string per sparkle)
            uint mapName = rt.Mode + 0x68;
            uint mapLength = rt.CStringLength(mapName);
            uint buf = rt.StrLocalAssign(mapName, mapLength);
            bool ecl = rt.CStringEquals(mapName, "ecl_fild01.rsw");
            rt.StrLocalDestroy(mapLength, buf);
            if (ecl && M.I32(p + 0x44) > 100) M.I32(p + 0x44, 100);
            float s = M.F32(p + 0x20);
            int a = M.I32(p + 0x50);
            Span<float> L = stackalloc float[12];
            int k0 = a % 0x168, k1 = (a + 0x5a) % 0x168, k2 = (a + 0xb4) % 0x168, k3 = (a + 0x10e) % 0x168;
            L[0] = s * FxMath.CosTableAt(k0); L[2] = s * FxMath.SinTableAt(k0);
            L[3] = s * FxMath.CosTableAt(k1); L[5] = s * FxMath.SinTableAt(k1);
            L[6] = s * FxMath.CosTableAt(k2); L[8] = s * FxMath.SinTableAt(k2);
            L[9] = s * FxMath.CosTableAt(k3); L[11] = s * FxMath.SinTableAt(k3);
            FaceCamera_a91590(rt, L);
            float dx = M.F32(p + 0xd0), dy = M.F32(p + 0xd4), dz = M.F32(p + 0xd8);
            M.F32(p + 0x21c, dy + L[1]);
            M.F32(p + 0x1f8, dx + L[9]);
            M.F32(p + 0x220, dz + L[2]);
            M.F32(p + 0x238, dx + L[6]);
            M.F32(p + 0x1fc, dy + L[10]);
            M.F32(p + 0x200, dz + L[11]);
            M.F32(p + 0x218, dx + L[0]);
            M.F32(p + 0x240, dz + L[8]);
            M.F32(p + 0x258, dx + L[3]);
            M.F32(p + 0x25c, dy + L[4]);
            M.F32(p + 0x23c, dy + L[7]);
            M.F32(p + 0x260, dz + L[5]);
        }

        /// <summary>
        /// FUN_00a91590 — turns four points (x, y, z) to face the camera: (y, z) by the latitude index
        /// (450 − (int)view+0x2c) % 360, then (x, z) by the longitude index 360 − (int)view+0x30 (raw table reads).
        /// </summary>
        static void FaceCamera_a91590(FxRuntime rt, Span<float> L)
        {
            var M = rt.M;
            uint view = M.U32(rt.Mode + 0xd0);
            int lon = 0x168 - FxOps.Cvtt(M.F32(view + 0x30));
            int lat = (0x1c2 - FxOps.Cvtt(M.F32(view + 0x2c))) % 0x168;
            float cl = FxMath.CosTableAt(lat), sl = FxMath.SinTableAt(lat);
            Span<float> zr = stackalloc float[4];
            for (int k = 0; k < 4; k++)
            {
                float y = L[k * 3 + 1], z = L[k * 3 + 2];
                float nz = z * cl - y * sl;
                L[k * 3 + 2] = nz;
                L[k * 3 + 1] = y * cl + z * sl;
                zr[k] = nz;
            }
            float co = FxMath.CosTableAt(lon), so = FxMath.SinTableAt(lon);
            for (int k = 0; k < 4; k++)
            {
                float x = L[k * 3];
                L[k * 3] = x * co - zr[k] * so;
                L[k * 3 + 2] = zr[k] * co + x * so;
            }
        }

        /// <summary>FUN_00a8ff90 — per-id particle spawning on the effect's own tick counter +0x104.</summary>
        void HealSpawn_a8ff90(FxRuntime rt)
        {
            int id = Id;
            int t = M.I32(A + 0x104);
            switch (id)
            {
                case 0x145:
                    if (t == 0) { HealRings_a8f050(rt, 1); HealRings2_a8f470(rt, 1); t = M.I32(A + 0x104); }
                    if (t == 1) { HealRings_a8f050(rt, 2); HealRings2_a8f470(rt, 2); t = M.I32(A + 0x104); }
                    if (t <= 0x32)
                    {
                        if (t % 6 == 0)
                        {
                            HealSparkles_a8ec90(rt, 1);
                            t = M.I32(A + 0x104);
                            if (t > 0x32) break;
                        }
                        if (t % 9 == 2) HealSparkles_a8ec90(rt, 2);
                    }
                    break;
                case 0x138:
                    if (t == 0) { HealRings_a8f050(rt, 4); t = M.I32(A + 0x104); }
                    if (t <= 0x32 && t % 6 == 0) HealSparkles_a8ec90(rt, 4);
                    break;
                case 0x139:
                    if (t == 0) { HealRings_a8f050(rt, 0); HealRings2_a8f470(rt, 0); t = M.I32(A + 0x104); }
                    if (t <= 0x32 && t % 6 == 0) HealSparkles_a8ec90(rt, 0);
                    break;
                case 0x140:
                    if (t == 0) { HealRings_a8f050(rt, 3); t = M.I32(A + 0x104); }
                    if (t <= 0x19 && t % 6 == 0) HealSparkles_a8ec90(rt, 3);
                    break;
                case 0xfd:
                    if (t == 0)
                    {
                        rt.Rand.Next();
                        SpawnRingPair(rt, RingBlue, 100, 100, 0xff, 0x28);
                        HealRings2_a8f470(rt, 5);
                    }
                    break;
                case 0x459:
                    if (t == 0)
                    {
                        rt.Rand.Next();
                        SpawnRingPair(rt, RingWhite, 0x73, 0x19, 0x73, 0x3c);
                        t = M.I32(A + 0x104);
                    }
                    if (t <= 0x32 && t % 6 == 0) HealSparkles_a8ec90(rt, 6);
                    break;
                case 0x477:
                case 0x478:
                    if (t == 0)
                    {
                        rt.Rand.Next();
                        for (int j = 0; j < 2; j++)
                        {
                            int ang = rt.Rand.Next() % 0x168;
                            if (ang < 0) ang = 0;
                            for (int i = 0; i < 0x15; i++)
                            {
                                uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                                if (p == 0) continue;
                                var q = new CParticle(M, p);
                                M.I32(p + 0xc, i);
                                M.I32(p + 4, Id);
                                M.I32(p + 0x10, j);
                                M.I32(p + 8, 1);
                                M.I32(p + 0x50, 0x168);
                                M.I32(p + 0x44, 0);
                                M.I32(p + 0x4c, 0x5a);
                                M.I32(p + 0x54, 0);
                                q.SetTexture_a67fb0(rt, AlphaDown);
                                int rnd = rt.Rand.Next() % 0x14;
                                if (rnd < 0) rnd = 0;
                                M.F32(p + 0xc0, (float)(rnd * j) + 40.0f);
                                M.F32(p + 0xc4, (float)ang);
                                M.F32(p + 0x20, (float)j * 0.2f + 4.6f);
                                if (id == 0x477) q.SetColor_a67f70(0, 0xff, 0xff); else q.SetColor_a67f70(0xff, 0xff, 0);
                                M.I32(A + 0xfc, 0x3c);
                                CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                                rt.PtrVectorPushBack(A + 0x108, p);
                            }
                        }
                        t = M.I32(A + 0x104);
                    }
                    if (t <= 0x32 && t % 6 == 0) HealSparkles_a8ec90(rt, id == 0x477 ? 7 : 8);
                    break;
            }
            M.I32(A + 0x104, M.I32(A + 0x104) + 1);
        }

        /// <summary>The ring pair of ids 253 / 1113 in FUN_00a8ff90: two rings of 21 quads (one rand() each).</summary>
        void SpawnRingPair(FxRuntime rt, string texture, int r, int g, int b, int fadeTick)
        {
            int ring = 0;
            for (int deg = 0; deg < 0x168; deg += 0xb4, ring++)
            {
                rt.Rand.Next();
                for (int i = 0; i < 0x15; i++)
                {
                    uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                    if (p == 0) continue;
                    var q = new CParticle(M, p);
                    M.I32(p + 4, Id);
                    M.I32(p + 0xc, i);
                    M.I32(p + 0x10, ring);
                    M.I32(p + 8, 1);
                    M.I32(p + 0x50, 0x168);
                    M.I32(p + 0x44, 0);
                    M.I32(p + 0x4c, 0x5a);
                    M.I32(p + 0x54, 0);
                    q.SetTexture_a67fb0(rt, texture);
                    M.F32(p + 0xc0, 60.0f);
                    M.F32(p + 0xc4, (float)deg);
                    M.F32(p + 0x20, (float)ring * 0.4f + 4.6f);
                    q.SetColor_a67f70(r, g, b);
                    M.I32(A + 0xfc, fadeTick);
                    CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                    rt.PtrVectorPushBack(A + 0x108, p);
                }
            }
        }

        /// <summary>FUN_00a8f050 — the outer ring layer (+0x108): two rings of 21 quads, style by <paramref name="kind"/>.</summary>
        void HealRings_a8f050(FxRuntime rt, int kind)
        {
            int rnd = rt.Rand.Next() % 0x14;
            if (rnd < 0) rnd = 0;
            float radius40 = (float)rnd + 40.0f;
            int ring = 0;
            for (int deg = 0; ; )
            {
                int ang = rt.Rand.Next() % 0x168;
                if (ang < 0) ang = 0;
                for (int i = 0; i < 0x15; i++)
                {
                    uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                    if (p == 0) continue;
                    var q = new CParticle(M, p);
                    M.I32(p + 0xc, i);
                    M.I32(p + 0x10, ring);
                    M.I32(p + 4, Id);
                    M.I32(p + 8, 1);
                    M.I32(p + 0x50, 0x168);
                    M.I32(p + 0x44, 0);
                    M.I32(p + 0x4c, 0x5a);
                    M.I32(p + 0x54, 0);
                    switch (kind)
                    {
                        case 0:
                            q.SetTexture_a67fb0(rt, RingWhite);
                            M.F32(p + 0xc0, 60.0f);
                            M.F32(p + 0xc4, (float)deg);
                            M.F32(p + 0x20, (float)ring * 0.4f + 4.6f);
                            q.SetColor_a67f70(0x8c, 0xd2, 0x8c);
                            M.I32(A + 0xfc, 0x3c);
                            break;
                        case 1:
                        case 5:
                            if (kind == 1) q.SetTexture_a67fb0(rt, RingWhite); else q.SetTexture_a67fb0(rt, RingBlue);
                            M.F32(p + 0xc0, 60.0f);
                            M.F32(p + 0xc4, (float)deg);
                            M.F32(p + 0x20, (float)ring * 0.4f + 4.6f);
                            if (kind == 1) q.SetColor_a67f70(0x8c, 0xd2, 0x8c); else q.SetColor_a67f70(100, 100, 0xff);
                            M.I32(A + 0xfc, 0x28);
                            break;
                        case 2:
                            q.SetTexture_a67fb0(rt, AlphaDown);
                            M.F32(p + 0xc0, 90.0f);
                            M.F32(p + 0xc4, (float)deg);
                            M.F32(p + 0x20, (float)ring * 0.1f + 4.2f);
                            q.SetColor_a67f70(0x8c, 0xd2, 0x8c);
                            M.I32(A + 0xfc, 0x28);
                            break;
                        case 3:
                        {
                            q.SetTexture_a67fb0(rt, AlphaDown);
                            M.F32(p + 0xc0, radius40);
                            M.F32(p + 0xc4, (float)ang);
                            int r = rt.Rand.Next() % 100;
                            if (r < 0) r = 0;
                            float sc = (float)r * 0.005f + 4.5f;
                            sc += (float)ring * 0.2f;
                            M.F32(p + 0x20, sc);
                            q.SetColor_a67f70(0xff, 0xff, 0xff);
                            M.I32(A + 0xfc, 0x1e);
                            break;
                        }
                        case 4:
                        case 7:
                        case 8:
                        {
                            q.SetTexture_a67fb0(rt, AlphaDown);
                            int r = rt.Rand.Next() % 0x14;
                            if (r < 0) r = 0;
                            M.F32(p + 0xc0, (float)(r * ring) + 40.0f);
                            M.F32(p + 0xc4, (float)ang);
                            M.F32(p + 0x20, (float)ring * 0.2f + 4.6f);
                            if (kind == 4) q.SetColor_a67f70(0x8c, 0xd2, 0x8c);
                            else if (kind == 7) q.SetColor_a67f70(0, 0xff, 0xff);
                            else q.SetColor_a67f70(0xff, 0xff, 0);
                            M.I32(A + 0xfc, 0x3c);
                            break;
                        }
                        case 6:
                            q.SetTexture_a67fb0(rt, RingWhite);
                            M.F32(p + 0xc0, 60.0f);
                            M.F32(p + 0xc4, (float)deg);
                            M.F32(p + 0x20, (float)ring * 0.4f + 4.6f);
                            q.SetColor_a67f70(0x73, 0x19, 0x73);
                            M.I32(A + 0xfc, 0x3c);
                            break;
                    }
                    CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                    rt.PtrVectorPushBack(A + 0x108, p);
                }
                ring++;
                deg += 0xb4;
                if (deg > 0x167) return;
            }
        }

        /// <summary>FUN_00a8f470 — the inner ring layer (+0x114): two rings of 21 quads tilted 50° and 48°.</summary>
        void HealRings2_a8f470(FxRuntime rt, int kind)
        {
            int ring = 0;
            for (int tilt = 0x32; ; )
            {
                int ang = rt.Rand.Next() % 0x168;
                if (ang < 0) ang = 0;
                for (int i = 0; i < 0x15; i++)
                {
                    uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                    if (p == 0) continue;
                    var q = new CParticle(M, p);
                    M.I32(p + 0x10, ring);
                    M.I32(p + 4, Id);
                    M.I32(p + 0xc, i);
                    M.I32(p + 8, 1);
                    M.I32(p + 0x50, 0x168);
                    M.I32(p + 0x44, 0);
                    M.I32(p + 0x54, 0);
                    switch (kind)
                    {
                        case 0:
                            q.SetTexture_a67fb0(rt, RingWhite);
                            M.F32(p + 0xc4, (float)ang);
                            M.F32(p + 0xc0, 12.0f);
                            M.F32(p + 0x20, (float)ring * 0.2f + 5.0f);
                            M.I32(p + 0x4c, tilt);
                            q.SetColor_a67f70(0x8c, 0xd2, 0x8c);
                            M.I32(A + 0x100, 0x3c);
                            break;
                        case 1:
                        case 5:
                            if (kind == 1) q.SetTexture_a67fb0(rt, RingWhite); else q.SetTexture_a67fb0(rt, RingBlue);
                            M.F32(p + 0xc4, (float)ang);
                            M.F32(p + 0xc0, 18.0f);
                            M.F32(p + 0x20, (float)ring * 0.2f + 5.0f);
                            M.I32(p + 0x4c, tilt);
                            if (kind == 1) q.SetColor_a67f70(0x8c, 0xd2, 0x8c); else q.SetColor_a67f70(100, 100, 0xff);
                            M.I32(A + 0x100, 0x28);
                            break;
                        case 2:
                            q.SetTexture_a67fb0(rt, AlphaDown);
                            M.F32(p + 0xc4, (float)ang);
                            M.F32(p + 0xc0, 10.0f);
                            M.F32(p + 0x20, (float)ring * 0.1f + 5.3f);
                            M.I32(p + 0x4c, tilt - 4);
                            q.SetColor_a67f70(0x8c, 0xd2, 0x8c);
                            M.I32(A + 0x100, 0x1e);
                            break;
                    }
                    CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                    rt.PtrVectorPushBack(A + 0x114, p);
                }
                ring++;
                tilt -= 2;
                if (tilt < 0x2f) return;
            }
        }

        /// <summary>FUN_00a8ec90 — four falling sparkles (+0x120) scattered ±6 around the centre, size/speed by <paramref name="kind"/>.</summary>
        void HealSparkles_a8ec90(FxRuntime rt, int kind)
        {
            for (int i = 0; i < 4; i++)
            {
                uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                if (p == 0) continue;
                var q = new CParticle(M, p);
                M.I32(p + 4, Id);
                M.I32(p + 0xc, i);
                M.I32(p + 0x10, 0);
                M.I32(p + 8, 1);
                q.SetTexture_a67fb0(rt, Pok1);
                M.I32(p + 0x44, 0);
                M.U32(p + 0xc4, 0);
                M.I32(p + 0x50, 0);
                int r1 = rt.Rand.Next() % 0xd;
                int r2 = rt.Rand.Next() % 0xd;
                M.U32(p + 0xd4, 0);
                M.F32(p + 0xd0, (float)((r2 >= 0 ? r2 : 0) - 6));
                M.F32(p + 0xd8, (float)((r1 >= 0 ? r1 : 0) - 6));
                q.SetColor_a67f70(0xdc, 0xfa, 0xdc);
                CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                rt.PtrVectorPushBack(A + 0x120, p);
                switch (kind)
                {
                    case 0: case 3: case 6:
                        M.F32(p + 0x20, (float)RandMod(rt, 10) * 0.1f + 2.0f);
                        M.F32(p + 0x54, (float)RandMod(rt, 0x28) * 0.01f + 0.4f);
                        break;
                    case 1:
                        M.F32(p + 0x20, (float)RandMod(rt, 10) * 0.1f + 2.0f);
                        M.F32(p + 0x54, (float)RandMod(rt, 0x28) * 0.01f + 0.6f);
                        break;
                    case 2:
                        M.F32(p + 0x20, (float)RandMod(rt, 0x28) * 0.1f + 0.4f);
                        M.F32(p + 0x54, (float)RandMod(rt, 0x28) * 0.01f + 0.4f);
                        break;
                    case 4:
                        M.F32(p + 0x20, (float)RandMod(rt, 10) * 0.1f + 2.0f);
                        M.F32(p + 0x54, (float)RandMod(rt, 0x28) * 0.01f + 0.3f);
                        break;
                    case 7:
                    case 8:
                        M.F32(p + 0x20, (float)RandMod(rt, 10) * 0.1f + 2.0f);
                        M.F32(p + 0x54, (float)RandMod(rt, 0x28) * 0.01f + 0.4f);
                        if (kind == 7) q.SetColor_a67f70(0, 0xff, 0xff); else q.SetColor_a67f70(0xff, 0xff, 0);
                        break;
                }
            }
        }

        /// <summary>The client's <c>max(rand() % n, 0)</c> (CDQ; IDIV; CMOVNS).</summary>
        static int RandMod(FxRuntime rt, int n)
        {
            int r = rt.Rand.Next() % n;
            return r >= 0 ? r : 0;
        }

        /// <summary>
        /// CHealEffect::vf04_a91320 — ring quads with index &gt; 0 as strips (u from (i − 1) / 21 to i / 21),
        /// then every sparkle with full UVs; all camera-billboarded with depth.
        /// </summary>
        void HealRender_a91320(FxRuntime rt)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                uint vec = A + (layer == 0 ? 0x108u : 0x114u);
                for (uint pp = M.U32(vec); pp != M.U32(vec + 4); pp += 4)
                {
                    uint p = M.U32(pp);
                    if (p == 0) continue;
                    int i = M.I32(p + 0xc);
                    if (i <= 0) continue;
                    float u0 = (float)(i - 1) / 21.0f;
                    float u1 = (float)i / 21.0f;
                    new CParticle(M, p).RenderStrip_a67b30(rt, false, 0f, 1.0f, u0, u1, true);
                }
            }
            for (uint pp = M.U32(A + 0x120); pp != M.U32(A + 0x124); pp += 4)
            {
                uint p = M.U32(pp);
                if (p != 0) new CParticle(M, p).Render_a67b60(rt, false, 0f, 1.0f, 0f, 1.0f, true);
            }
        }
    }
}
