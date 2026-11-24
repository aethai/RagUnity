// Synced from reference/port/src/RoClassicFx/Shared/aura-vanish/CEffect.Level99.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The max-level aura of the new effect system (package aura-vanish; launch rules in packages/combat-timing §7):
    /// CLevel99Effect (EF 200, vtable 0xff4c1c, 0xfc bytes; allocator VCLevel99Effect 0xfe1708) — two "pikapika"
    /// sparkle discs turning on the ground and three blue rings of 22 segments spinning around the owner;
    /// CLevel99Orb1Effect (EF 976, vtable 0xff4cac, 0x198 bytes; VCLevel99Orb1Effect 0xfe1714) and CLevel99Orb2Effect
    /// (EF 977, vtable 0xff4d3c; VCLevel99Orb2Effect 0xfe1720) — 16 lights rising from the ground and drifting while
    /// they fade (whitelight.tga for the normal aura, freezing_circle.bmp for babies).
    /// All three follow their owner (by account id, FUN_00a76030) and live as long as the owner exists (vf06 =
    /// FUN_00a762f0); an ownerless one lives until the map changes. No start sounds (vf12 = CEffect::vf12, empty).
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCLevel99Effect = 0x00ff4c1c, VtCLevel99Orb1Effect = 0x00ff4cac, VtCLevel99Orb2Effect = 0x00ff4d3c;
        public const uint VtAllocLevel99 = 0x00fe1708, VtAllocLevel99Orb1 = 0x00fe1714, VtAllocLevel99Orb2 = 0x00fe1720;
        public const uint Level99Size = 0xfc, Level99OrbSize = 0x198;
        static readonly int[] Level99Ids = { 200 }, Level99Orb1Ids = { 976 }, Level99Orb2Ids = { 977 };

        const string Pikapika = "effect\\pikapika2.bmp", Level99Ring = "effect\\ring_blue.tga";
        const string WhiteLight = "effect\\whitelight.tga", FreezingCircle = "effect\\freezing_circle.bmp";

        // ---- CLevel99Effect ----------------------------------------------------------------------

        /// <summary>
        /// CLevel99Effect::CLevel99Effect (FUN_00a91800): CEffect(200); +0xcc..+0xe3 previous ring corners (inner x, y, z,
        /// outer x, y, z), particle vectors +0xe4 (sparkle discs) and +0xf0 (ring segments).
        /// </summary>
        static uint ConstructLevel99_a91800(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, 200);
            M.U32(a, VtCLevel99Effect);
            for (uint o = 0xcc; o < 0xfc; o += 4) M.U32(a + o, 0);
            return a;
        }

        /// <summary>
        /// CLevel99Effect::vf01_a92230 — two sparkle discs (pikapika2.bmp, RP flags 0xd, kind 1, alpha 200, scale 9,
        /// spin angle +0xc4 = 0 / 22, pulse phase +0x4c = 0 / 180), then the rings (FUN_00a91c80).
        /// </summary>
        void Level99Init_a92230(FxRuntime rt)
        {
            for (int i = 0, spin = 0, phase = 0; spin < 0x2c; i++, spin += 0x16, phase += 0xb4)
            {
                uint p = rt.AllocParticle_aaa6c0(A + 8, 0xd);
                if (p == 0) continue;
                new CParticle(M, p).SetTexture_a67fb0(rt, Pikapika);
                M.U32(p + 4, M.U32(A + 4));
                M.I32(p + 0xc, i);
                M.I32(p + 0x10, 0);
                M.I32(p + 8, 1);
                M.I32(p + 0x44, 200);
                M.F32(p + 0xc4, (float)spin);
                M.I32(p + 0x4c, phase);
                M.U32(p + 0x20, 0x41100000);   // 9.0
                CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                rt.PtrVectorPushBack(A + 0xe4, p);
            }
            Level99Rings_a91c80(rt);
        }

        /// <summary>
        /// FUN_00a91c80 — three rings (r = 0, 1, 2) of 22 segments each (ring_blue.tga, RP flags 0xd, kind 3, alpha 120,
        /// colour (100, 100, 255)): angle offset +0xc4 = 90 r, segment step +0x50 = 315 (/ 20 → 15°), band width +0xc0 =
        /// 15 − 2r, ring radius +0x20 = 0.2 r + 3.9, band tilt +0x4c = 55 / 50 / 45. Each segment's quad is built once
        /// here: inner corner = radius · (cos a, 0, sin a), outer = inner + width · sin(9i) · (cos t · cos a, −sin t / cos t,
        /// cos t · sin a) with a = (15 i + offset) % 360; the left edge reuses the previous segment's corners (+0xcc..).
        /// </summary>
        void Level99Rings_a91c80(FxRuntime rt)
        {
            int ring = 0, offset = 0, tilt = 0x37;
            do
            {
                for (int i = 0; i <= 0x15; i++)
                {
                    uint p = rt.AllocParticle_aaa6c0(A + 8, 0xd);
                    if (p == 0) continue;
                    var q = new CParticle(M, p);
                    q.SetTexture_a67fb0(rt, Level99Ring);
                    M.U32(p + 4, M.U32(A + 4));
                    M.I32(p + 0xc, i);
                    M.I32(p + 0x10, ring);
                    M.I32(p + 8, 3);
                    M.I32(p + 0x44, 0x78);
                    q.SetColor_a67f70(100, 100, 0xff);
                    float r = (float)ring;
                    M.F32(p + 0xc4, (float)offset);
                    float twice = r + r;
                    M.I32(p + 0x50, 0x13b);
                    M.F32(p + 0xc0, 15.0f - twice);
                    M.F32(p + 0x20, r * 0.2f + 3.9f);
                    M.I32(p + 0x4c, tilt);
                    M.U32(p + 0x54, 0);
                    CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                    rt.PtrVectorPushBack(A + 0xf0, p);
                    Level99Segment(p);
                }
                ring++;
                tilt -= 5;
                offset += 0x5a;
            } while (tilt > 0x28);
        }

        /// <summary>The segment quad of FUN_00a91c80 (inlined FUN_00a92020): SSE single precision in the client's order.</summary>
        void Level99Segment(uint p)
        {
            int seg = M.I32(p + 0xc);
            float step = (float)M.I32(p + 0x50) / 20.0f;
            float width = FxMath.SinTableAt(seg * 9) * M.F32(p + 0xc0);
            int a = FxOps.Cvtt((float)(FxOps.Cvtt(step) * seg) + M.F32(p + 0xc4)) % 0x168;
            int t = M.I32(p + 0x4c);
            float flat = width * FxMath.CosTableAt(t);
            float cosA = FxMath.CosTableAt(a);
            float sinA = FxMath.SinTableAt(a);
            float up = width * FxMath.SinTableAt(t);
            float dx = flat * cosA;
            float dz = flat * sinA;
            float scale = M.F32(p + 0x20);
            float iz = scale * sinA;
            M.U32(p + 0x25c, 0);
            float ix = scale * cosA;
            M.F32(p + 0x260, iz);
            M.F32(p + 0x258, ix);
            float ox = ix + dx;
            float oy = 0.0f - up;
            float oz = iz + dz;
            M.F32(p + 0x218, ox);
            M.F32(p + 0x21c, oy);
            M.F32(p + 0x220, oz);
            if (seg != 0)
            {
                M.U32(p + 0x1f8, M.U32(A + 0xd8));
                M.U32(p + 0x1fc, M.U32(A + 0xdc));
                M.U32(p + 0x200, M.U32(A + 0xe0));
                M.U32(p + 0x238, M.U32(A + 0xcc));
                M.U32(p + 0x23c, M.U32(A + 0xd0));
                M.U32(p + 0x240, M.U32(A + 0xd4));
            }
            M.F32(A + 0xcc, ix);
            M.U32(A + 0xd0, 0);
            M.F32(A + 0xd4, iz);
            M.F32(A + 0xd8, ox);
            M.F32(A + 0xdc, oy);
            M.F32(A + 0xe0, oz);
        }

        /// <summary>
        /// CLevel99Effect::vf03_a923f0 — every particle takes the effect's position (+0x24..) and ages (+0xe8). Discs:
        /// matrix = Scale(s, s, 1) × (RotZ(+0xc4°) × RotX(90°)) with the pulse phase +0x4c advanced by 3 (wrapping at
        /// 360) and s = (sin(phase) + 1) · (0.1 · scale) + scale. Ring segments: +0xc4 = (int)(ring + +0xc4 + 3) and
        /// matrix = RotY(+0xc4°).
        /// </summary>
        void Level99Process_a923f0(FxRuntime rt)
        {
            Span<float> rot = stackalloc float[12];
            Span<float> scale = stackalloc float[12];
            Span<float> m = stackalloc float[12];
            for (uint pp = M.U32(A + 0xe4); pp != M.U32(A + 0xe8); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    Mat43.RotationZ((M.F32(p + 0xc4) / 180.0f) * 3.141592f, rot);
                    Mat43.RotateX(rot, BitConverter.Int32BitsToSingle(0x3fc90fd8));
                    int phase = M.I32(p + 0x4c);
                    phase = (phase + 3 < 0x168 ? 3 : -0x165) + phase;
                    M.I32(p + 0x4c, phase);
                    float pulse = FxMath.SinTableAt(phase) + 1.0f;
                    float k = M.F32(p + 0x20) * 0.1f;
                    pulse = pulse * k;
                    pulse = pulse + M.F32(p + 0x20);
                    Mat43.Scale(pulse, pulse, 1.0f, scale);
                    Mat43.Mul(scale, rot, m);
                    Mat43.Store(M, p + 0x1c8, m);
                }
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
            for (uint pp = M.U32(A + 0xf0); pp != M.U32(A + 0xf4); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    float spin = (float)M.I32(p + 0x10) + M.F32(p + 0xc4);
                    spin = spin + 3.0f;
                    float a = (float)FxOps.Cvtt(spin);
                    M.F32(p + 0xc4, a);
                    Mat43.RotationY((a / 180.0f) * 3.141592f, m);
                    Mat43.Store(M, p + 0x1c8, m);
                }
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
        }

        /// <summary>
        /// CLevel99Effect::vf04_a92840 — the discs as camera-facing quads (FUN_00a67b60(0, 0, 1, 0, 1, 0, 1)), then every
        /// ring segment but the first of each ring as a strip quad with u = (i − 1) / 21 .. i / 21 (FUN_00a67b30).
        /// </summary>
        void Level99Render_a92840(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0xe4); pp != M.U32(A + 0xe8); pp += 4)
            {
                uint p = M.U32(pp);
                if (p != 0) new CParticle(M, p).Render_a67b60(rt, false, 0f, 1.0f, 0f, 1.0f, true);
            }
            for (uint pp = M.U32(A + 0xf0); pp != M.U32(A + 0xf4); pp += 4)
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

        // ---- CLevel99Orb1Effect / CLevel99Orb2Effect -----------------------------------------------

        /// <summary>
        /// CLevel99Orb1Effect::CLevel99Orb1Effect (FUN_00a92970) / CLevel99Orb2Effect (FUN_00a93560): CEffect(976 / 977),
        /// 16 light offsets (x, y, z) at +0xcc, particle vector +0x18c.
        /// </summary>
        static uint ConstructLevel99Orb(FxRuntime rt, uint a, int id, uint vtable)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, id);
            M.U32(a, vtable);
            for (uint o = 0xcc; o < 0x198; o += 4) M.U32(a + o, 0);
            return a;
        }

        /// <summary>
        /// vf01 of the orbs (CLevel99Orb1Effect::vf01_a92b30 / CLevel99Orb2Effect::vf01_a93710): 4 × 4 lights, row r and
        /// column c (+0x10 / +0xc), kind 2, rise speed +0xc0 = 0.15, alpha 0; Orb1: whitelight.tga, RP flags 5, scale 1.8,
        /// colour (80, 80, 255); Orb2: freezing_circle.bmp, RP flags 0xd, scale 0.55. Each light starts at height
        /// rand() % 100 (offset (0, y, 0) at +0xcc + 12 (4r + c)) with four random angles rand() % 360 at +0x54..+0x60
        /// (current / target drift directions).
        /// </summary>
        void Level99OrbInit(FxRuntime rt, bool orb2)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    uint p = rt.AllocParticle_aaa6c0(A + 8, orb2 ? 0xd : 5);
                    if (p == 0) continue;
                    var q = new CParticle(M, p);
                    q.SetTexture_a67fb0(rt, orb2 ? FreezingCircle : WhiteLight);
                    M.U32(p + 4, M.U32(A + 4));
                    M.I32(p + 0xc, col);
                    M.I32(p + 0x10, row);
                    M.I32(p + 8, 2);
                    M.U32(p + 0xc0, 0x3e19999a);                             // 0.15
                    M.U32(p + 0x20, orb2 ? 0x3f0ccccdu : 0x3fe66666u);         // 0.55 / 1.8
                    if (!orb2) q.SetColor_a67f70(0x50, 0x50, 0xff);
                    M.I32(p + 0x44, 0);
                    int h = RandModNonNegative(rt, 100);
                    uint slot = A + (uint)(row * 4 + 0x11 + col) * 0xc;
                    M.U32(slot, 0);
                    M.F32(slot + 4, (float)h);
                    M.U32(slot + 8, 0);
                    for (uint k = 0; k < 4; k++) M.F32(p + 0x54 + k * 4, (float)RandModNonNegative(rt, 0x168));
                    rt.PtrVectorPushBack(A + 0x18c, p);
                }
            }
        }

        /// <summary>rand() % n with the client's CMOVNS fix-up (a negative remainder becomes 0).</summary>
        static int RandModNonNegative(FxRuntime rt, int n)
        {
            int r = rt.Rand.Next() % n;
            return r >= 0 ? r : 0;
        }

        /// <summary>
        /// vf03 of the orbs (0xa92c90 / 0xa93860). Per light (offset o = +0xcc + 12 (4 row + col), speed s = +0xc0):
        /// below the ground (o.y &lt; 0) it either respawns (o.y &lt; −30: o = (0, rand() % 100, 0), four new random angles,
        /// alpha 0) or drifts: the two direction angles (+0x54/+0x58) step towards their targets (+0x5c/+0x60) by +3 / −2
        /// and a target that is reached (|diff| &lt; 3) is re-rolled (rand() % angle, or 359 − rand() % angle), then o.x / o.z
        /// += s · sin(angle) and the alpha is 255 while o.y ≥ −20, else it fades by 4. Then o.y −= s (so the light
        /// sinks: the client's y axis points down) and the matrix = Scale(scale, scale, 1) × R × Translation(o) with
        /// R = RotX(camera latitude) (Orb1) or RotX(180°) × RotX(latitude) (Orb2), i.e. the light faces the camera.
        /// </summary>
        void Level99OrbProcess(FxRuntime rt, bool orb2)
        {
            Span<float> rot = stackalloc float[12];
            Span<float> tr = stackalloc float[12];
            Span<float> sc = stackalloc float[12];
            Span<float> sr = stackalloc float[12];
            Span<float> m = stackalloc float[12];
            for (uint pp = M.U32(A + 0x18c); pp != M.U32(A + 0x190); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    float speed = M.F32(p + 0xc0);
                    int n = M.I32(p + 0xc) + M.I32(p + 0x10) * 4;
                    uint slot = A + 0xcc + (uint)n * 0xc;
                    float y = M.F32(slot + 4);
                    if (0.0f > y)
                    {
                        if (-30.0f > y)
                        {
                            int h = RandModNonNegative(rt, 100);
                            M.U32(slot, 0);
                            M.U32(slot + 8, 0);
                            M.F32(slot + 4, (float)h);
                            for (uint k = 0; k < 4; k++) M.F32(p + 0x54 + k * 4, (float)RandModNonNegative(rt, 0x168));
                            M.I32(p + 0x44, 0);
                        }
                        else
                        {
                            for (uint k = 0; k < 2; k++)
                            {
                                uint cur = p + 0x54 + k * 4;
                                int c = FxOps.Cvtt(M.F32(cur));
                                int diff = FxOps.Cvtt(M.F32(cur + 8)) - c;
                                M.F32(cur, (float)((diff > 0 ? 3 : -2) + c));
                                int abs = (diff ^ (diff >> 31)) - (diff >> 31);
                                if (abs >= 3) continue;
                                int target;
                                if (diff > 0) target = c == 0 ? 0 : RandModNonNegative(rt, c);
                                else target = 0x167 - (c == 0 ? 0 : RandModNonNegative(rt, c));
                                M.F32(cur + 8, (float)target);
                            }
                            M.F32(slot, speed * FxMath.SinTableAt(FxOps.Cvtt(M.F32(p + 0x54))) + M.F32(slot));
                            M.F32(slot + 8, speed * FxMath.SinTableAt(FxOps.Cvtt(M.F32(p + 0x58))) + M.F32(slot + 8));
                            if (-20.0f > M.F32(slot + 4))
                            {
                                int alpha = M.I32(p + 0x44) - 4;
                                M.I32(p + 0x44, alpha >= 0 ? alpha : 0);
                            }
                            else if (M.I32(p + 0x44) != 0xff) M.I32(p + 0x44, 0xff);
                        }
                    }
                    M.F32(slot + 4, M.F32(slot + 4) - speed);
                    uint view = M.U32(rt.Mode + 0xd0);
                    if (orb2)
                    {
                        Mat43.RotationX(BitConverter.Int32BitsToSingle(0x40490fd8), rot);
                        Mat43.RotateX(rot, (M.F32(view + 0x2c) / 180.0f) * 3.141592f);
                    }
                    else Mat43.RotationX((M.F32(view + 0x2c) / 180.0f) * 3.141592f, rot);
                    Mat43.Translation(M.F32(slot), M.F32(slot + 4), M.F32(slot + 8), tr);
                    float s = M.F32(p + 0x20);
                    Mat43.Scale(s, s, 1.0f, sc);
                    Mat43.Mul(sc, rot, sr);
                    Mat43.Mul(sr, tr, m);
                    Mat43.Store(M, p + 0x1c8, m);
                }
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
        }

        /// <summary>vf04 of the orbs (0xa93490 / 0xa94080): every light drawn twice as a camera-facing quad (additive glow).</summary>
        void Level99OrbRender(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0x18c); pp != M.U32(A + 0x190); pp += 4)
            {
                uint p = M.U32(pp);
                if (p == 0) continue;
                var q = new CParticle(M, p);
                for (int k = 0; k < 2; k++) q.Render_a67b60(rt, false, 0f, 1.0f, 0f, 1.0f, true);
            }
        }

        /// <summary>CLevel99Orb2Effect::vf05_a936e0 — returns the lights to the pool but (unlike Orb1) leaves the vector's end as it is.</summary>
        void Level99Orb2Release_a936e0(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0x18c); pp != M.U32(A + 0x190); pp += 4) rt.FreeParticle_aaa740(M.U32(pp));
        }

        /// <summary>
        /// FUN_00a762f0 (vf06 of the three classes) — alive unless the owner account id is set and no actor has it any more.
        /// </summary>
        bool OwnerAlive_a762f0(FxRuntime rt)
        {
            uint owner = M.U32(A + 0x20);
            return owner == 0 || rt.ActorByAid_a64480(owner) != 0;
        }
    }

    public static partial class Mat43
    {
        /// <summary>FUN_00530f80 — m = m × RotX(rad); element 0 untouched (same arithmetic as <see cref="FxMatrix.RotateX"/>).</summary>
        public static void RotateX(Span<float> m, float rad)
        {
            float s = (float)Math.Sin(rad), c = (float)Math.Cos(rad), ns = -s;
            float i0 = I[0], i1 = I[1], i2 = I[2], i3 = I[3], i6 = I[6], i9 = I[9], i10 = I[10], i11 = I[11];
            float m0 = m[0], m1 = m[1], m7 = m[7], m6 = m[6];
            float m10 = m[10], m9 = m[9], m2 = m[2], m4 = m[4];
            float m3 = m[3], m5 = m[5];
            m[1] = m0 * i1 + m1 * c + ns * m2;
            m[2] = m0 * i2 + m1 * s + m2 * c;
            m[3] = m3 * i0 + m4 * i3 + m5 * i6;
            float m8 = m[8];
            m[4] = m3 * i1 + m4 * c + ns * m5;
            m[5] = m3 * i2 + m4 * s + m5 * c;
            m[6] = m6 * i0 + m7 * i3 + m8 * i6;
            m[7] = m6 * i1 + m7 * c + ns * m8;
            m[8] = m6 * i2 + m7 * s + m8 * c;
            float m11 = m[11];
            m[9] = m9 * i0 + m10 * i3 + m11 * i6 + i9;
            m[10] = m9 * i1 + m10 * c + ns * m11 + i10;
            m[11] = m9 * i2 + m10 * s + m11 * c + i11;
        }
    }
}
