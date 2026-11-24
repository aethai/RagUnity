// Synced from reference/port/src/RoClassicFx/Shared/mapfx/CEffect.Smoke.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CSmokeEffect (EF 44, vtable 0xff5370, 0xdc bytes; allocator VCSmokeEffect 0xfe172c, vf01 0xa490f0 → ctor
    /// FUN_00a9db40) — chimney smoke (이팩트\굴뚝연기.spr / .act) of the new effect system (package mapfx; 1074 RSW records
    /// on 46 maps, launched once at map load, ownerless, at the record's position). Four sprite particles (pool 1) rise
    /// and fade in a 50-tick cycle; only the first +0xcc of them (re-rolled 1..4 at each restart) are drawn.
    /// Fields: +0xcc number of particles drawn (1 at construction), +0xd0 std::vector&lt;CSpriteParticle*&gt;.
    /// Particle fields used: +0xc index 0..3, +0x2bc/+0x2c0/+0x2c4 offset from the effect, +0x2c8 spin step (0.75°),
    /// +0x2cc rise speed (0.3..0.5; also the sprite frame delay, which truncates to 0 → a new frame every draw),
    /// +0x198 4x3 direction matrix (RotX(75..104°) then × RotY(0..359°)), +0xc4 spin angle, +0x20 size 1.5.
    /// Slots not listed are CEffect's: vf02 range check (130), vf07..vf24, vf26..vf28.
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCSmokeEffect = 0x00ff5370;
        public const uint VtAllocSmoke = 0x00fe172c;
        public const uint SmokeSize = 0xdc;
        static readonly int[] SmokeIds = { 44 };

        /// <summary>FUN_00a9db40 — CEffect(0x2c), vtable, +0xcc = 1, empty particle vector.</summary>
        static uint ConstructSmoke_a9db40(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, 0x2c);
            M.U32(a, VtCSmokeEffect);
            M.I32(a + 0xcc, 1);
            M.U32(a + 0xd0, 0);
            M.U32(a + 0xd4, 0);
            M.U32(a + 0xd8, 0);
            return a;
        }

        /// <summary>
        /// CSmokeEffect::vf01_a9dce0 — four sprite particles (FUN_00aaa700 at the effect, RP flags 1): +4 = effect id,
        /// +0xc = index, +0x10 = 0, +8 = 1, appended to the vector (FUN_00683bc0 when full).
        /// </summary>
        void SmokeInit_a9dce0(FxRuntime rt)
        {
            for (int i = 0; i < 4; i++)
            {
                uint p = rt.AllocSpriteParticle_aaa700(A + 8, 1);
                if (p == 0) continue;
                M.U32(p + 4, M.U32(A + 4));
                M.I32(p + 0xc, i);
                M.U32(p + 0x10, 0);
                M.U32(p + 8, 1);
                rt.PtrVectorPushBack(A + 0xd0, p);
            }
        }

        /// <summary>
        /// CSmokeEffect::vf03_a9de80 — every particle: position = the effect's. Age &lt; 50: offset += M·(0, 0, speed)
        /// (direction matrix +0x198, the ×0 terms kept), spin +0xc4 += +0x2c8, and from age 35 alpha −15 a tick (not below 0).
        /// Age ≥ 50 (restart): SPR / ACT 이팩트\굴뚝연기, size 1.5, spin step 0.75, age and offset 0, RP flags 1, alpha 255,
        /// speed = (rand() % 3 + 3) / 10, matrix +0x198 = RotX((rand() % 30 + 75)°) × RotY((rand() % 360)°), and the
        /// effect's drawn count +0xcc = (int)RandRange(1, particle count) (FUN_00a64570: one more rand()). Then matrix
        /// +0x1c8 = Translation(offset.x + 0, offset.y − 9, offset.z + 0); age +1.
        /// </summary>
        void SmokeProcess_a9de80(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0xd0); pp != M.U32(A + 0xd4); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0)
                {
                    float x;
                    int age = M.I32(p + 0xe8);
                    uint m = p + 0x198;
                    if (age < 0x32)
                    {
                        float s = M.F32(p + 0x2cc);
                        x = M.F32(m + 0xc) * 0f + M.F32(m) * 0f;
                        x += s * M.F32(m + 0x18);
                        float y = M.F32(m + 0x10) * 0f + M.F32(m + 4) * 0f;
                        y += s * M.F32(m + 0x1c);
                        float z = M.F32(m + 0x14) * 0f + M.F32(m + 8) * 0f;
                        z += s * M.F32(m + 0x20);
                        x += M.F32(m + 0x24);
                        y += M.F32(m + 0x28);
                        x += M.F32(p + 0x2bc);
                        y += M.F32(p + 0x2c0);
                        z += M.F32(m + 0x2c);
                        z += M.F32(p + 0x2c4);
                        M.F32(p + 0x2bc, x);
                        M.F32(p + 0x2c0, y);
                        M.F32(p + 0x2c4, z);
                        M.F32(p + 0xc4, M.F32(p + 0x2c8) + M.F32(p + 0xc4));
                        if (age > 0x22)
                        {
                            int a = unchecked(M.I32(p + 0x44) - 0xf);
                            M.I32(p + 0x44, a >= 0 ? a : 0);                         // ADD; CMOVNS
                        }
                    }
                    else
                    {
                        var sp = new CSpriteParticle(M, p);
                        sp.SetSpr_a69a10(rt, SmokeSpr);
                        sp.SetAct_a699d0(rt, SmokeAct);
                        M.U32(p + 0x20, 0x3fc00000);                                   // 1.5
                        M.U32(p + 0x2c8, 0x3f400000);                                  // 0.75
                        int r = rt.Rand.Next();
                        M.I32(p + 0xe8, 0);
                        M.U32(p + 0x2bc, 0);
                        M.U32(p + 0x2c0, 0);
                        M.U32(p + 0x2c4, 0);
                        M.I32(p + 0x1c, 1);
                        M.I32(p + 0x44, 0xff);
                        int k = r % 3;
                        M.F32(p + 0x2cc, (float)((k < 0 ? 0 : k) + 3) / 10.0f);
                        // FUN_00532630 (is +0x198 the identity?) — result unused
                        r = rt.Rand.Next() % 0x1e;
                        FxMatrix.MakeRotationX(M, m, ((float)((r < 0 ? 0 : r) + 0x4b) / 180.0f) * 3.141592f);
                        r = rt.Rand.Next() % 0x168;
                        FxMatrix.RotateY(M, m, ((float)(r < 0 ? 0 : r) / 180.0f) * 3.141592f);
                        int n = (int)(M.U32(A + 0xd4) - M.U32(A + 0xd0)) >> 2;
                        float count = (float)((double)n + (n < 0 ? 4294967296.0 : 0.0));    // unsigned → double → float
                        M.I32(A + 0xcc, FxOps.Cvtt(RandRange_a64570(rt, 1.0f, count)));    // FUN_00d49720 on the float result
                        x = M.F32(p + 0x2bc);
                    }
                    Span4x3Translation(p + 0x1c8, x + 0f, M.F32(p + 0x2c0) - 9.0f, M.F32(p + 0x2c4) + 0f);
                }
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
        }

        /// <summary>The identity (0x1137438) with row 3 = (x, y, z) — FUN_005330d0 on a stack copy, then copied to <paramref name="at"/>.</summary>
        void Span4x3Translation(uint at, float x, float y, float z)
        {
            M.F32(at, 1f); M.F32(at + 4, 0f); M.F32(at + 8, 0f);
            M.F32(at + 0xc, 0f); M.F32(at + 0x10, 1f); M.F32(at + 0x14, 0f);
            M.F32(at + 0x18, 0f); M.F32(at + 0x1c, 0f); M.F32(at + 0x20, 1f);
            M.F32(at + 0x24, x); M.F32(at + 0x28, y); M.F32(at + 0x2c, z);
        }

        /// <summary>CSmokeEffect::vf04_a9e4e0 — the particles whose index +0xc is below +0xcc (signed) through FUN_00a68d20(1).</summary>
        void SmokeRender_a9e4e0(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0xd0); pp != M.U32(A + 0xd4); pp += 4)
            {
                uint p = M.U32(pp);
                if (p != 0 && M.I32(p + 0xc) < M.I32(A + 0xcc)) new CSpriteParticle(M, p).Render_a68d20(rt, true);
            }
        }

        /// <summary>CSmokeEffect::vf25_a9dc20 body: vtable, the particle vector freed (FUN_006857c0), CEffect.</summary>
        void SmokeDestruct_a9dc20(FxRuntime rt)
        {
            M.U32(A, VtCSmokeEffect);
            DestroyPtrVector_678360(rt, A + 0xd0);
            DestructBase_a75970(rt);
        }

        /// <summary>이팩트\굴뚝연기.spr (0xff53e4) / .act (0xff53f8).</summary>
        const string SmokeSpr = "ÀÌÆÑÆ®\\±¼¶Ò¿¬±â.spr";
        const string SmokeAct = "ÀÌÆÑÆ®\\±¼¶Ò¿¬±â.act";
    }
}
