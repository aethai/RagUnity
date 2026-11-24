// Synced from reference/port/src/RoClassicFx/Shared/mapfx-emitters/CEffect.EvilsPaw.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CEvilsPawEffect (EF 1039, vtable 0xf7db54, 0x100 bytes; allocator VCEvilsPawEffect 0xfe17f8 → ctor FUN_0067ba10) —
    /// a map effect of the new system: one camera-facing particle (pool 0, RP flags 5, blend 5 / 6) at the effect's
    /// position + an offset, flipped by RotZ(π), cycling through twenty textures effect\1-1.tga .. 1-10.tga,
    /// effect\2-1.tga .. 2-10.tga, one frame every speed + 1 ticks. Package mapfx-emitters; configured by the map's
    /// EffectTool (<c>SetEvilsPawInfo</c>, FUN_00aa7c50, <see cref="FxRuntime.SetEvilsPawInfo_aa7c50"/>). 76 RSW records,
    /// all on 1@eom.
    ///
    /// Fields: +0xcc std::vector&lt;CParticle*&gt; (one particle), +0xd8 frame delay in ticks ("Speed", 5), +0xdc size
    /// ("Size", int, 50), +0xe0 ticks left on the current frame (5), +0xe4/+0xe8/+0xec offset ("offsetPos", (0, −40, 0)),
    /// +0xf0 std::vector&lt;std::string&gt; the texture names, +0xfc current frame. vf02 = CEffect's 130-unit range check,
    /// vf06 always alive (the effect ends only with the map), no sounds.
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCEvilsPawEffect = 0x00f7db54;
        public const uint VtAllocEvilsPaw = 0x00fe17f8;
        public const uint EvilsPawSize = 0x100;
        static readonly int[] EvilsPawIds = { 1039 };

        /// <summary>FUN_0067ba10: CEffect(0x40f) and the defaults above.</summary>
        static uint ConstructEvilsPaw_67ba10(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, 0x40f);
            M.U32(a, VtCEvilsPawEffect);
            M.U32(a + 0xcc, 0); M.U32(a + 0xd0, 0); M.U32(a + 0xd4, 0);
            M.I32(a + 0xd8, 5);
            M.I32(a + 0xdc, 0x32);
            M.I32(a + 0xe0, 5);
            M.U32(a + 0xe4, 0); M.U32(a + 0xe8, 0xc2200000); M.U32(a + 0xec, 0);
            M.U32(a + 0xf0, 0); M.U32(a + 0xf4, 0); M.U32(a + 0xf8, 0);
            M.U32(a + 0xfc, 0);
            return a;
        }

        /// <summary>CEvilsPawEffect::vf25_67bb70 body: vtable, the texture names (FUN_00568f30), the particle vector (FUN_00678360), CEffect.</summary>
        void EvilsPawDestruct_67bb70(FxRuntime rt)
        {
            M.U32(A, VtCEvilsPawEffect);
            rt.StrVectorDestroy_568f30(A + 0xf0);
            DestroyPtrVector_678360(rt, A + 0xcc);
            DestructBase_a75970(rt);
        }

        /// <summary>
        /// CEvilsPawEffect::vf01_67bd10 — one particle (FUN_00aaa6c0 at the effect, RP flags 5): white, alpha 255, size
        /// (float)+0xdc, offset +0xd0..+0xd8 = +0xe4..+0xec, blend 5 / 6, unit quad, appended to +0xcc; then the twenty
        /// names "effect\%d-%d.tga" (sprintf into a 64-byte buffer, a local std::string moved into +0xf0; FUN_0058b0c0 when
        /// full).
        /// </summary>
        void EvilsPawInit_67bd10(FxRuntime rt)
        {
            uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
            if (p != 0)
            {
                EvilsPawSetupParticle(p);
                M.I32(p + 0x2a8, 5);
                M.I32(p + 0x2ac, 6);
                CParticle.SetUnitQuad_a67ad0(M, p + 0x1f8);
                rt.PtrVectorPushBack(A + 0xcc, p);
            }
            for (int set = 1; set <= 2; set++)
                for (int frame = 1; frame <= 10; frame++)
                    rt.StrVectorPushBackLocal(A + 0xf0, "effect\\" + set.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                                        + "-" + frame.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".tga");
        }

        /// <summary>The particle set-up vf01 and vf07 share: colour 255, alpha 255, size (CVTDQ2PS of +0xdc), offset (raw dwords).</summary>
        void EvilsPawSetupParticle(uint p)
        {
            new CParticle(M, p).SetColor_a67f70(0xff, 0xff, 0xff);
            M.I32(p + 0x44, 0xff);
            M.F32(p + 0x20, (float)M.I32(A + 0xdc));
            M.U32(p + 0xd0, M.U32(A + 0xe4));
            M.U32(p + 0xd4, M.U32(A + 0xe8));
            M.U32(p + 0xd8, M.U32(A + 0xec));
        }

        /// <summary>
        /// CEvilsPawEffect::vf03_67c060 — per particle: position = effect; texture = name +0xfc (FUN_00a67fb0, the C string);
        /// matrix +0x1c8 = Scale(size, size, 1) × (RotZ(π as float) × RotX(camera latitude)) (FUN_00533450, FUN_00530f80,
        /// FUN_00532b50, FUN_005334c0); position = effect + offset; age +1. Then the frame clock: +0xe0 − 1, below 0 → next
        /// frame (+0xfc + 1, back to 0 past the last name; unsigned compare) and +0xe0 = +0xd8.
        /// </summary>
        void EvilsPawProcess_67c060(FxRuntime rt)
        {
            Span<float> rot = stackalloc float[12];
            Span<float> sc = stackalloc float[12];
            Span<float> res = stackalloc float[12];
            for (uint pp = M.U32(A + 0xcc); pp != M.U32(A + 0xd0); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    uint name = M.U32(A + 0xf0) + (uint)M.I32(A + 0xfc) * 0x18;
                    new CParticle(M, p).SetTexture_a67fb0(rt, rt.ReadCString(rt.StrData(name)));
                    Mat43.RotationZ(BitConverter.Int32BitsToSingle(0x40490fd8), rot);
                    uint view = M.U32(rt.Mode + 0xd0);
                    Mat43.RotateX(rot, (M.F32(view + 0x2c) / 180.0f) * 3.141592f);
                    float size = M.F32(p + 0x20);
                    Mat43.Scale(size, size, 1.0f, sc);
                    Mat43.Mul(sc, rot, res);
                    Mat43.Store(M, p + 0x1c8, res);
                    float x = M.F32(A + 8) + M.F32(A + 0xe4);
                    float y = M.F32(A + 0xc) + M.F32(A + 0xe8);
                    float z = M.F32(A + 0x10) + M.F32(A + 0xec);
                    M.F32(p + 0x24, x);
                    M.F32(p + 0x28, y);
                    M.F32(p + 0x2c, z);
                }
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
            int left = M.I32(A + 0xe0) - 1;
            M.I32(A + 0xe0, left);
            if (left >= 0) return;
            uint frame = M.U32(A + 0xfc) + 1;
            uint last = (uint)((int)(M.U32(A + 0xf4) - M.U32(A + 0xf0)) / 0x18 - 1);
            M.U32(A + 0xe0, M.U32(A + 0xd8));
            if (last < frame) frame = 0;                                    // CMOVB
            M.U32(A + 0xfc, frame);
        }

        /// <summary>CEvilsPawEffect::vf04_67c3d0 — every particle camera-facing through FUN_00a67b60(0, 0, 1, 0, 1, 0, 1).</summary>
        void EvilsPawRender_67c3d0(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0xcc); pp != M.U32(A + 0xd0); pp += 4)
            {
                uint p = M.U32(pp);
                if (p != 0) new CParticle(M, p).Render_a67b60(rt, false, 0f, 1.0f, 0f, 1.0f, true);
            }
        }

        /// <summary>CEvilsPawEffect::vf07_67bfb0 (SetEvilsPawInfo MSG_SET_INIT) — frame 0, +0xe0 = +0xd8, every particle set up again.</summary>
        void EvilsPawReinit_67bfb0()
        {
            M.U32(A + 0xfc, 0);
            M.U32(A + 0xe0, M.U32(A + 0xd8));
            for (uint pp = M.U32(A + 0xcc); pp != M.U32(A + 0xd0); pp += 4) EvilsPawSetupParticle(M.U32(pp));
        }
    }
}
