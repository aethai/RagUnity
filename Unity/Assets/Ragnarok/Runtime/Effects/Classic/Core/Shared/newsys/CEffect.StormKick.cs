// Synced from reference/port/src/RoClassicFx/Shared/newsys/CEffect.StormKick.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CStormKickEffect (vtable 0xff6170, 0xfc bytes; allocator VCStormKickEffect) — Taekwon Storm Kick (EF 435):
    /// ten spinning "storm2" blades (+0xe4, EF 435 only) and four rings of cloud strips (+0xf0, previous corners
    /// in +0xcc..+0xe0) around the owner, who is turned 30° a tick for the first 48 ticks. EF 1223 is the
    /// cloud-only variant (60 ticks). The class also carries the ids 459..465, which are not in the registry.
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCStormKickEffect = 0x00ff6170;
        public const uint VtAllocStormKick = 0x00fe17ec;
        public const uint StormKickSize = 0xfc;
        static readonly int[] StormKickIds = { 435, 1223 };

        const string StormKickWav = "effect\\T_È¸¿À¸®Â÷±â.wav";   // 0xff61e4

        /// <summary>CStormKickEffect::CStormKickEffect (FUN_00aa2950).</summary>
        static uint ConstructStormKick_aa2950(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, -1);
            M.U32(a, VtCStormKickEffect);
            for (uint o = 0xcc; o < 0xfc; o += 4) M.U32(a + o, 0);
            return a;
        }

        /// <summary>CStormKickEffect::vf12_aa33e0 — 435: the kick sound at tick 5 (459 / 464 sounds are unreachable).</summary>
        void StormKickStart_aa33e0(FxRuntime rt)
        {
            switch (Id)
            {
                case 0x1b3: AddSound_a75aa0(rt, 0, StormKickWav, 5, 0f); break;
                case 0x1cc: AddSound_a75aa0(rt, 0, "ice_titan_attack.wav", 5, 0f); break;
                case 0x1d0: AddSound_a75aa0(rt, 0, "effect\\REVERBERATION.wav", 0, 0f); break;
            }
        }

        /// <summary>CStormKickEffect::vf01_aa32a0 — blades (FUN_00aa2b00) for 435, the two cloud layers (FUN_00aa2cd0), lifetime.</summary>
        void StormKickInit_aa32a0(FxRuntime rt)
        {
            int id = Id;
            switch (id)
            {
                case 0x1b3: case 0x1cb: case 0x1cc: case 0x1cd: case 0x1d1:
                    StormKickBlades_aa2b00(rt, 0);
                    StormKickClouds_aa2cd0(rt, 0);
                    StormKickClouds_aa2cd0(rt, 1);
                    break;
                case 0x1d0:
                    StormKickBlades_aa2b00(rt, 0);
                    StormKickBlades_aa2b00(rt, 1);
                    StormKickClouds_aa2cd0(rt, 0);
                    StormKickClouds_aa2cd0(rt, 1);
                    break;
                case 0x4c7:
                    StormKickClouds_aa2cd0(rt, 0);
                    StormKickClouds_aa2cd0(rt, 1);
                    break;
            }
            switch (Id)
            {
                case 0x1b3: case 0x1cb: case 0x1cc: case 0x1cd: case 0x1d0: M.I32(A + 0x2c, 100); break;
                case 0x1d1: M.I32(A + 0x2c, 1000); break;
                case 0x4c7: M.I32(A + 0x2c, 0x3c); break;
            }
        }

        /// <summary>FUN_00aa2b00 — ten "storm2" blades at random angles, tinted per id, shaped by FUN_00aa2ee0.</summary>
        void StormKickBlades_aa2b00(FxRuntime rt, int variant)
        {
            for (int i = 0; i < 10; i++)
            {
                uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                if (p == 0) continue;
                var q = new CParticle(M, p);
                q.SetTexture_a67fb0(rt, "effect\\storm2.tga");
                M.I32(p + 4, Id);
                M.I32(p + 0xc, i);
                M.I32(p + 0x10, 0);
                M.I32(p + 8, 1);
                M.F32(p + 0xc0, 40.0f);
                M.I32(p + 0x44, 0);
                M.F32(p + 0xc4, (float)RandMod(rt, 0x168));
                float v = (float)variant;
                M.F32(p + 0x20, 2.0f);
                M.F32(p + 0x54, 50.0f);
                M.F32(p + 0x58, 5.0f);
                M.F32(p + 0x5c, v);
                switch (Id)
                {
                    case 0x1b3: q.SetColor_a67f70(0x32, 0x32, 0xff); break;
                    case 0x1cb: q.SetColor_a67f70(0xa4, 0x88, 0x37); break;
                    case 0x4c7: q.SetColor_a67f70(0xff, 0xff, 0x32); break;
                    case 0x1cc: q.SetColor_a67f70(0xff, 0xff, 0xff); break;
                    case 0x1cd: q.SetColor_a67f70(0x82, 0x82, 0xff); break;
                    case 0x1d0:
                        if (!(v != 0f)) q.SetColor_a67f70(0xff, 0xff, 0xff);
                        else if (!(v != 1.0f)) q.SetColor_a67f70(0xff, 0x37, 0x37);
                        break;
                    case 0x1d1: q.SetColor_a67f70(0x9b, 0xff, 0x9b); break;
                }
                CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                rt.PtrVectorPushBack(A + 0xe4, p);
                StormKickBladeShape_aa2ee0(p);
            }
        }

        /// <summary>
        /// FUN_00aa2ee0 — a blade's corners: the radius +0xc0 shrinks by (2.2 − 0.2 k) · size for k = 1..index, the
        /// corners sit at 90° steps from (index · 45 + angle) % 360 + 90, at height index · +0x58 − +0x54 − 1.
        /// </summary>
        void StormKickBladeShape_aa2ee0(uint p)
        {
            if (p == 0) return;
            int i = M.I32(p + 0xc);
            float radius = M.F32(p + 0xc0);
            float h = (float)i * M.F32(p + 0x58) + -M.F32(p + 0x54);
            float af = (float)(i * 0x2d) + M.F32(p + 0xc4);
            int a = FxOps.Cvtt(af) % 0x168;
            if (i > 0)
            {
                float size = M.F32(p + 0x20);
                for (int k = 1; k <= i; k++)
                {
                    float d = 2.2f - (float)k * 0.2f;
                    radius -= d * size;
                }
            }
            h -= 1.0f;
            int k0 = (a + 0x5a) % 0x168;
            int k1 = (k0 + 0x5a) % 0x168;
            int k2 = (k1 + 0x5a) % 0x168;
            int k3 = (k2 + 0x5a) % 0x168;
            M.F32(p + 0x1f8, radius * FxMath.CosTableAt(k2));
            M.F32(p + 0x1fc, h);
            M.F32(p + 0x200, radius * FxMath.SinTableAt(k2));
            M.F32(p + 0x218, radius * FxMath.CosTableAt(k1));
            M.F32(p + 0x21c, h);
            M.F32(p + 0x220, radius * FxMath.SinTableAt(k1));
            M.F32(p + 0x238, radius * FxMath.CosTableAt(k3));
            M.F32(p + 0x23c, h);
            M.F32(p + 0x240, radius * FxMath.SinTableAt(k3));
            M.F32(p + 0x258, radius * FxMath.CosTableAt(k0));
            M.F32(p + 0x25c, h);
            M.F32(p + 0x260, radius * FxMath.SinTableAt(k0));
        }

        /// <summary>
        /// FUN_00aa2cd0 — four rings (start angles 0/90/180/270) of 21 "cloud11" strips; radius and size per ring
        /// and layer (<paramref name="layer"/> 0 outer, 1 inner; smaller for 1223), tilt 80..100°.
        /// </summary>
        void StormKickClouds_aa2cd0(FxRuntime rt, int layer)
        {
            int ring = 0;
            for (int deg = 0; deg < 0x168; deg += 0x5a, ring++)
            {
                for (int i = 0; i < 0x15; i++)
                {
                    uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                    if (p == 0) continue;
                    new CParticle(M, p).SetTexture_a67fb0(rt, "effect\\cloud11.tga");
                    M.I32(p + 4, Id);
                    M.I32(p + 0xc, i);
                    M.I32(p + 0x10, ring);
                    M.I32(p + 8, 1);
                    M.I32(p + 0x44, 0);
                    M.I32(p + 0x50, 0x1e);
                    M.F32(p + 0xc4, (float)deg);
                    M.I32(p + 0x4c, RandMod(rt, 0x15) + 0x50);
                    M.F32(p + 0x54, 1.0f);
                    float j = (float)ring;
                    bool small = Id == 0x4c7;
                    if (layer == 0)
                    {
                        M.F32(p + 0xc0, (small ? 28.0f : 48.0f) - j * 5.0f);
                        M.F32(p + 0x20, (small ? 10.0f : 14.0f) - (j + j));
                    }
                    else if (layer == 1)
                    {
                        M.F32(p + 0xc0, (small ? 18.0f : 28.0f) - j * 5.0f);
                        M.F32(p + 0x20, (small ? 5.0f : 6.0f) - j);
                    }
                    CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                    rt.PtrVectorPushBack(A + 0xf0, p);
                }
            }
        }

        /// <summary>
        /// CStormKickEffect::vf03_aa3580 — blades fade in (+1 a tick until 40, −1 after 60 unless 465) and turn
        /// 3..5° a tick (RotY matrix); cloud strips turn 5° a tick, widen (+3 up to 180 in +0x50), fade and grow
        /// (+0.1 size) by age, and are rebuilt as ring strips like the heal rings (tilt +0x4c, radius +0x54, height
        /// −+0xc0); finally the owner spins (435 / 1223: +30° while tick ≤ 47; 459: +14° while tick ≤ 50).
        /// </summary>
        void StormKickProcess_aa3580(FxRuntime rt)
        {
            System.Span<float> rot = stackalloc float[12];
            for (uint pp = M.U32(A + 0xe4); pp != M.U32(A + 0xe8); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    int alpha = M.I32(p + 0x44);
                    if (M.I32(p + 0xe8) > 0x3c && Id != 0x1d1)
                    {
                        alpha -= 1;
                        if (alpha < 0) alpha = 0;
                    }
                    M.I32(p + 0x44, M.I32(p + 0xe8) > 0x28 ? alpha : alpha + 1);
                    int spin = RandMod(rt, 3) + 3;
                    float af = M.F32(p + 0xc4) - (float)spin;
                    int a = FxOps.Cvtt(af) % 0x168;
                    float fa = (float)a;
                    M.F32(p + 0xc4, fa);
                    Mat43.RotationY(fa / 180.0f * 3.141592f, rot);
                    Mat43.Store(M, p + 0x1c8, rot);
                }
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
            for (uint pp = M.U32(A + 0xf0); pp != M.U32(A + 0xf4); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0) StormKickCloudStep(p);
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
            uint owner = rt.ActorByAid_a64480(M.U32(A + 0x20));
            if (owner == 0) return;
            float heading;
            switch (Id)
            {
                case 0x1b3:
                case 0x4c7:
                    if (M.I32(A + 0x28) > 0x2f) return;
                    heading = M.F32(owner + 0x4c) + 30.0f;
                    break;
                case 0x1cb:
                    if (M.I32(A + 0x28) > 0x32) return;
                    heading = M.F32(owner + 0x4c) + 14.0f;
                    break;
                default:
                    return;
            }
            M.F32(owner + 0x4c, heading);
            if (heading > 360.0f) M.F32(owner + 0x4c, heading - 360.0f);
        }

        void StormKickCloudStep(uint p)
        {
            float c4 = (float)(FxOps.Cvtt(M.F32(p + 0xc4) + 5.0f) % 0x168);
            int a50 = M.I32(p + 0x50) + 3;
            if (a50 > 0xb4) a50 = 0xb4;
            int age = M.I32(p + 0xe8);
            M.I32(p + 0x50, a50);
            int alpha = M.I32(p + 0x44);
            M.F32(p + 0xc4, c4);
            float size;
            bool grow;
            if (Id == 0x4c7)
            {
                if (age > 0x1e)
                {
                    size = M.F32(p + 0x20);
                    int a = alpha - 3;
                    M.I32(p + 0x44, a >= 0 ? a : 0);
                    grow = true;
                }
                else
                {
                    size = M.F32(p + 0x20);
                    M.I32(p + 0x44, alpha + 3);
                    grow = age > 0xc;
                }
            }
            else if (age > 0x32)
            {
                size = M.F32(p + 0x20);
                int a = alpha - 1;
                M.I32(p + 0x44, a >= 0 ? a : 0);
                grow = true;
            }
            else if (age >= 0xc)
            {
                size = M.F32(p + 0x20);
                M.I32(p + 0x44, alpha);
                grow = age > 0xc;
            }
            else
            {
                size = M.F32(p + 0x20);
                M.I32(p + 0x44, alpha + 1);
                grow = false;
            }
            if (grow) size += 0.1f;
            M.F32(p + 0x20, size);

            int i = M.I32(p + 0xc);
            float r54 = M.F32(p + 0x54);
            float step = (float)a50 / 20.0f;
            float angF = (float)(FxOps.Cvtt(step) * i);
            angF += c4;
            int ang = FxOps.Cvtt(angF) % 0x168;
            int tilt = M.I32(p + 0x4c);
            float rc = r54 * FxMath.CosTableAt(tilt);
            float c = FxMath.CosTableAt(ang), s = FxMath.SinTableAt(ang);
            float rs = r54 * FxMath.SinTableAt(tilt);
            float rcc = c * rc, rcs = s * rc;
            float innerX = c * M.F32(p + 0x20);
            float innerY = -M.F32(p + 0xc0);
            float outerX = innerX + rcc;
            float innerZ = s * M.F32(p + 0x20);
            float outerY = innerY - rs;
            M.F32(p + 0x258, innerX);
            M.F32(p + 0x25c, innerY);
            float outerZ = innerZ + rcs;
            M.F32(p + 0x260, innerZ);
            M.F32(p + 0x218, outerX);
            M.F32(p + 0x21c, outerY);
            M.F32(p + 0x220, outerZ);
            if (i != 0)
            {
                M.U32(p + 0x1f8, M.U32(A + 0xd8));
                M.U32(p + 0x1fc, M.U32(A + 0xdc));
                M.U32(p + 0x200, M.U32(A + 0xe0));
                M.U32(p + 0x238, M.U32(A + 0xcc));
                M.U32(p + 0x23c, M.U32(A + 0xd0));
                M.U32(p + 0x240, M.U32(A + 0xd4));
            }
            M.F32(A + 0xcc, innerX);
            M.F32(A + 0xd0, innerY);
            M.F32(A + 0xd4, innerZ);
            M.F32(A + 0xd8, outerX);
            M.F32(A + 0xdc, outerY);
            M.F32(A + 0xe0, outerZ);
        }

        /// <summary>
        /// CStormKickEffect::vf04_aa3de0 — blades billboarded (destination blend 2 for 435, 6 for 459..465; 464
        /// draws variant 0 at a third of its alpha and skips variant 1 ≠ 1), then the cloud strips with index &gt; 0.
        /// </summary>
        void StormKickRender_aa3de0(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0xe4); pp != M.U32(A + 0xe8); pp += 4)
            {
                uint p = M.U32(pp);
                var q = new CParticle(M, p);
                int alpha = M.I32(p + 0x44);
                switch (Id)
                {
                    case 0x1cb: case 0x1cc: case 0x1cd: case 0x1d1:
                        M.I32(p + 0x2ac, 6);
                        q.Render_a67b60(rt, false, 0f, 1.0f, 0f, 1.0f, true);
                        break;
                    case 0x1d0:
                    {
                        float v = M.F32(p + 0x5c);
                        if (!(v != 0f))
                        {
                            M.I32(p + 0x2ac, 6);
                            M.I32(p + 0x44, alpha / 3);
                            q.Render_a67b60(rt, false, 0f, 1.0f, 0f, 1.0f, true);
                            M.I32(p + 0x44, alpha);
                        }
                        else if (!(v != 1.0f))
                        {
                            M.I32(p + 0x2ac, 2);
                            q.Render_a67b60(rt, false, 0f, 1.0f, 0f, 1.0f, true);
                        }
                        break;
                    }
                    default:
                        M.I32(p + 0x2ac, 2);
                        q.Render_a67b60(rt, false, 0f, 1.0f, 0f, 1.0f, true);
                        break;
                }
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
    }
}
