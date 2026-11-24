// Synced from reference/port/src/RoClassicFx/Shared/newsys/CEffect.ThrowItem.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CThrowItemEffect (vtable 0xff621c, 0x104 bytes; allocator VCThrowItemEffect) — an item picture (a UI item
    /// bitmap or an effect texture) flying on an arc from the caster (+8) to the target (+0x14): Acid Terror
    /// (298), Potion Pitcher (299), Throw Stone (308), Acid Demonstration (539 + the companion 980), Berserk
    /// Pitcher (541), Venom Knife (600), Ninja Shuriken/Kunai/Huuma/Zeny (613..616) and the 914/981..989 kin.
    /// +0xcc flight ticks (FUN_00aa5d10), +0xd0 start delay, +0xd4 × +0xd8 particle grid (1 × 4 by default: the
    /// item plus three trailing ghosts), +0xdc height difference, +0xe0 arc height, +0xe4..+0xf4 the previous
    /// particle's alpha / angle / position (the ghosts follow it), +0xf8 particle vector.
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCThrowItemEffect = 0x00ff621c;
        public const uint VtAllocThrowItem = 0x00fe1780;
        public const uint ThrowItemSize = 0x104;
        static readonly int[] ThrowItemIds = { 298, 299, 308, 539, 541, 600, 613, 614, 615, 616, 914, 980, 981, 982, 983, 989 };

        const string NinjaThrowWav = "effect\\´ÑÀÚ_´øÁö±â.wav";   // 0xff641c
        const string GSporeWav = "effect\\G½ºÆ÷¾îÀÍ½ºÇÃ·ÎÁ¯.wav";   // 0xff6434
        const string ItemDir = "À¯ÀúÀÎÅÍÆäÀÌ½º\\item\\";   // 유저인터페이스\item\ .
        const string TexShuriken = ItemDir + "¼ö¸®°Ë.bmp";   // 0xff633c
        const string TexFireBottle = ItemDir + "È­¿°º´.bmp";   // 0xff62b4
        const string TexEmptyBottle = ItemDir + "ºóº´.bmp";   // 0xff63e0
        const string TexPotionBottle = ItemDir + "ºóÆ÷¼Çº´.bmp";   // 0xff6290
        const string TexStone = ItemDir + "µ¹.bmp";   // 0xff6400
        const string TexBerserkPotion = ItemDir + "¹ö¼­Å©Æ÷¼Ç.bmp";   // 0xff62f4
        const string TexVenomKnife = ItemDir + "º£³Ñ³ªÀÌÇÁ.bmp";   // 0xff6318
        const string TexAcidBottle = ItemDir + "¿°»êº´.bmp";   // 0xff62d4
        const string TexKunaiPoison = ItemDir + "Äí³ªÀÌ_µ¶.bmp";   // 0xff5198
        const string TexHuumaFire = ItemDir + "Ç³¸¶_¿­È­.bmp";   // 0xff635c
        const string TexSporeBomb = ItemDir + "ÆøÅº¹ö¼¸Æ÷ÀÚ.bmp";   // 0xff6394
        const string TexKunaiFire = ItemDir + "Äí³ªÀÌ_È­.bmp";   // 0xff63bc
        const string TexCoin = "effect\\coin_a.bmp";   // 0xff6380

        /// <summary>CThrowItemEffect::CThrowItemEffect (FUN_00aa4120): 1 × 4 particles.</summary>
        static uint ConstructThrowItem_aa4120(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, -1);
            M.U32(a, VtCThrowItemEffect);
            M.U32(a + 0xcc, 0);
            M.U32(a + 0xd0, 0);
            M.I32(a + 0xd4, 1);
            M.I32(a + 0xd8, 4);
            M.U32(a + 0xdc, 0);
            M.U32(a + 0xe0, 0);
            for (uint o = 0xec; o <= 0x100; o += 4) M.U32(a + o, 0);
            return a;
        }

        /// <summary>CThrowItemEffect::vf12_aa4ec0 — the throw sound (ninja items, 298's heal sound, 914's spore).</summary>
        void ThrowItemStart_aa4ec0(FxRuntime rt)
        {
            switch (Id)
            {
                case 0x265: case 0x266: case 0x267: case 0x268: AddSound_a75aa0(rt, 0, NinjaThrowWav, 0, 0f); break;
                case 0x12a: AddSound_a75aa0(rt, 0, "_heal_effect.wav", 0, 0f); break;
                case 0x392: AddSound_a75aa0(rt, 0, GSporeWav, 0, 0f); break;
            }
        }

        /// <summary>
        /// CThrowItemEffect::vf01_aa4340 — the particle grid (+0xd4 rows × +0xd8 columns; 981: 8 rows, 982: 10 rows,
        /// 989: the owner's sphere count (+0x3d0) × 1), each particle styled per id and aimed at the target
        /// (FUN_00aa5d10, which also yields the flight time +0xcc).
        /// </summary>
        void ThrowItemInit_aa4340(FxRuntime rt)
        {
            int id = Id;
            if (id == 0x3d5) M.I32(A + 0xd4, 8);
            else if (id == 0x3d6) M.I32(A + 0xd4, 10);
            else if (id == 0x3dd)
            {
                uint owner = rt.ActorByAid_a64480(M.U32(A + 0x20));
                if (owner != 0)
                {
                    M.U32(A + 0xd4, M.U32(owner + 0x3d0));
                    M.I32(A + 0xd8, 1);
                }
            }
            for (int row = 0; row < M.I32(A + 0xd4); row++)
            {
                for (int col = 0; col < M.I32(A + 0xd8); col++)
                {
                    uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                    if (p == 0) continue;
                    var q = new CParticle(M, p);
                    M.I32(p + 4, Id);
                    M.I32(p + 0xc, col);
                    M.I32(p + 0x10, row);
                    M.I32(p + 8, 1);
                    switch (Id)
                    {
                        case 0x134:                                                  // Throw Stone
                            M.I32(A + 0x2c, 300);
                            M.F32(A + 0xe0, 10.0f);
                            q.SetTexture_a67fb0(rt, TexStone);
                            M.F32(p + 0x20, 1.0f);
                            q.SetColor_a67f70(0xff, 0xff, 0xff);
                            M.U32(p + 0xc4, 0);
                            M.F32(p + 0xc0, 2.0f);
                            ThrowItemStartAtCaster(p);
                            break;
                        case 0x12a:                                                  // Acid Terror
                            M.I32(A + 0x2c, 300);
                            M.F32(A + 0xe0, 30.0f);
                            AimParticle_aa5d10(rt, p);
                            q.SetTexture_a67fb0(rt, TexEmptyBottle);
                            M.F32(p + 0x20, 1.5f);
                            q.SetColor_a67f70(0x87, 0xff, 0x87);
                            M.U32(p + 0xc4, 0);
                            M.F32(p + 0xc0, M.F32(p + 0x54) / 25.0f);
                            ThrowItemStartAtCaster(p);
                            break;
                        case 0x12b: q.SetTexture_a67fb0(rt, TexPotionBottle); ThrowPotion_aa4ae0(rt, p, LevelOrOne()); break;
                        case 0x21d: q.SetTexture_a67fb0(rt, TexBerserkPotion); ThrowPotion_aa4ae0(rt, p, LevelOrOne()); break;
                        case 0x392: q.SetTexture_a67fb0(rt, TexSporeBomb); ThrowPotion_aa4ae0(rt, p, LevelOrOne()); break;
                        case 0x21b:                                                  // Acid Demonstration: fire bottle
                            M.I32(A + 0xd0, 5);
                            q.SetTexture_a67fb0(rt, TexFireBottle);
                            ThrowBottle_aa49c0(rt, p);
                            break;
                        case 0x3d4:                                                  // Acid Demonstration: acid bottle
                            M.I32(A + 0xd0, 10);
                            q.SetTexture_a67fb0(rt, TexAcidBottle);
                            ThrowBottle_aa49c0(rt, p);
                            break;
                        case 0x258: q.SetTexture_a67fb0(rt, TexVenomKnife); ThrowSpinning_aa4e20(rt, p); break;
                        case 0x265: q.SetTexture_a67fb0(rt, TexShuriken); ThrowSpinning_aa4e20(rt, p); break;
                        case 0x266: q.SetTexture_a67fb0(rt, TexKunaiPoison); ThrowSpinning_aa4e20(rt, p); break;
                        case 0x267: q.SetTexture_a67fb0(rt, TexHuumaFire); ThrowSpinning_aa4e20(rt, p); break;
                        case 0x268: q.SetTexture_a67fb0(rt, TexCoin); ThrowSpinning_aa4e20(rt, p); break;
                        case 0x3d7:
                            q.SetTexture_a67fb0(rt, TexKunaiFire);
                            ThrowSpinning_aa4e20(rt, p);
                            M.F32(p + 0xc0, 1.0f);
                            ThrowKunaiRing(rt, q, p);
                            break;
                        case 0x3d5: ThrowKunaiRing(rt, q, p); break;
                        case 0x3d6:
                            M.I32(A + 0x2c, 200);
                            M.F32(A + 0xe0, 10.0f);
                            q.SetTexture_a67fb0(rt, TexCoin);
                            M.F32(p + 0x20, 2.5f);
                            q.SetColor_a67f70(0xff, 200, 0);
                            M.F32(p + 0xc4, 330.0f);
                            M.F32(p + 0xc0, 2.0f);
                            M.I32(p + 0x44, 0xff);
                            M.I32(p + 0x2ac, 6);
                            M.F32(p + 0xd8, M.F32(A + 0x10));
                            M.F32(p + 0xd4, M.F32(A + 0xc) - 8.0f);
                            M.F32(p + 0xd0, M.F32(A + 8));
                            break;
                        case 0x3dd:
                        {
                            uint owner = rt.ActorByAid_a64480(M.U32(A + 0x20));
                            if (owner == 0) break;
                            int kind = M.I32(owner + 0x3cc);
                            if (kind == 1)
                            {
                                q.SetTexture_a67fb0(rt, "effect\\WaterOrb.bmp");
                                M.F32(p + 0x20, 1.0f);
                            }
                            else if (kind == 2) q.SetTexture_a67fb0(rt, "effect\\StoneOrb.bmp");
                            else if (kind == 3) q.SetTexture_a67fb0(rt, "effect\\FireOrb.bmp");
                            else if (kind == 4) q.SetTexture_a67fb0(rt, "effect\\LightningOrb.bmp");
                            M.I32(A + 0x2c, 200);
                            M.F32(p + 0x20, 2.0f);
                            M.F32(p + 0xc4, 330.0f);
                            M.F32(p + 0xc0, 1.5f);
                            M.I32(p + 0x44, 0xff);
                            M.F32(p + 0xd8, M.F32(A + 0x10));
                            M.F32(p + 0xd4, M.F32(A + 0xc) - 15.0f);
                            M.F32(p + 0xd0, M.F32(A + 8));
                            break;
                        }
                    }
                    AimParticle_aa5d10(rt, p);
                    rt.PtrVectorPushBack(A + 0xf8, p);
                }
            }
        }

        /// <summary>Skill level +0x30 with -1 read as 1 (the potion colour argument of FUN_00aa4ae0).</summary>
        int LevelOrOne()
        {
            int level = M.I32(A + 0x30);
            return level == -1 ? 1 : level;
        }

        /// <summary>The block shared by 298 and 308: alpha 255, local position = caster − 8 in y, destination blend 6.</summary>
        void ThrowItemStartAtCaster(uint p)
        {
            M.I32(p + 0x44, 0xff);
            float y = M.F32(A + 0xc) - 8.0f;
            M.F32(p + 0xd0, M.F32(A + 8));
            M.F32(p + 0xd4, y);
            M.F32(p + 0xd8, M.F32(A + 0x10));
            M.I32(p + 0x2ac, 6);
        }

        /// <summary>
        /// The 981 block (also the tail of 983): kunai ring of 8 × 4 spinning around the caster; without a target the
        /// destination is the caster's own position 8 units left.
        /// </summary>
        void ThrowKunaiRing(FxRuntime rt, CParticle q, uint p)
        {
            q.SetTexture_a67fb0(rt, TexKunaiPoison);
            M.I32(A + 0x2c, 200);
            M.F32(A + 0xe0, 1.0f);
            M.F32(p + 0x20, 3.0f);
            q.SetColor_a67f70(0xff, 200, 0xff);
            M.F32(p + 0xc4, 330.0f);
            M.F32(p + 0xc0, 1.5f);
            M.I32(p + 0x44, 0xff);
            float y = M.F32(A + 0xc) - 8.0f;
            M.F32(p + 0xd0, M.F32(A + 8));
            M.F32(p + 0xd8, M.F32(A + 0x10));
            M.F32(p + 0xd4, y);
            M.I32(p + 0x2ac, 6);
            if (M.U32(A + 0x24) == 0)
            {
                float x = M.F32(A + 8) - 8.0f;
                M.U32(A + 0x18, M.U32(A + 0xc));
                M.U32(A + 0x1c, M.U32(A + 0x10));
                M.F32(A + 0x14, x);
            }
        }

        /// <summary>FUN_00aa4e20 — spinning throwables (knives, shuriken, coins): 200 ticks, arc 1, size 2, tint (255, 200, 255).</summary>
        void ThrowSpinning_aa4e20(FxRuntime rt, uint p)
        {
            M.I32(A + 0x2c, 200);
            M.F32(A + 0xe0, 1.0f);
            M.F32(p + 0x20, 2.0f);
            new CParticle(M, p).SetColor_a67f70(0xff, 200, 0xff);
            M.F32(p + 0xc4, 330.0f);
            M.F32(p + 0xc0, 3.0f);
            M.I32(p + 0x44, 0xff);
            float y = M.F32(A + 0xc);
            M.U32(p + 0xd0, M.U32(A + 8));
            M.U32(p + 0xd8, M.U32(A + 0x10));
            M.I32(p + 0x2ac, 6);
            M.F32(p + 0xd4, y - 8.0f);
        }

        /// <summary>FUN_00aa49c0 — Acid Demonstration bottles: 300 ticks, arc 1, aimed, size 1.5, speed = distance / 25.</summary>
        void ThrowBottle_aa49c0(FxRuntime rt, uint p)
        {
            M.I32(A + 0x2c, 300);
            M.F32(A + 0xe0, 1.0f);
            AimParticle_aa5d10(rt, p);
            M.F32(p + 0x20, 1.5f);
            new CParticle(M, p).SetColor_a67f70(0xff, 0xff, 0xff);
            M.U32(p + 0xc4, 0);
            M.I32(p + 0x44, 0xff);
            M.F32(p + 0xc0, M.F32(p + 0x54) / 25.0f);
            float y = M.F32(A + 0xc);
            M.U32(p + 0xd0, M.U32(A + 8));
            M.U32(p + 0xd8, M.U32(A + 0x10));
            M.I32(p + 0x2ac, 6);
            M.F32(p + 0xd4, y - 8.0f);
        }

        /// <summary>
        /// FUN_00aa4ae0 — potions: 300 ticks, arc 30, aimed, size 1.5, speed = distance / 25; the tint by
        /// <paramref name="level"/> (1 red, 2 orange, 3 yellow, 4 and 6 white, 5 blue); every level but 6 also
        /// switches the destination blend to 6.
        /// </summary>
        void ThrowPotion_aa4ae0(FxRuntime rt, uint p, int level)
        {
            M.I32(A + 0x2c, 300);
            M.F32(A + 0xe0, 30.0f);
            AimParticle_aa5d10(rt, p);
            float speed = M.F32(p + 0x54) / 25.0f;
            M.F32(p + 0x20, 1.5f);
            M.U32(p + 0xc4, 0);
            M.I32(p + 0x44, 0xff);
            M.F32(p + 0xc0, speed);
            M.U32(p + 0xd0, M.U32(A + 8));
            M.U32(p + 0xd8, M.U32(A + 0x10));
            M.F32(p + 0xd4, M.F32(A + 0xc) - 8.0f);
            var q = new CParticle(M, p);
            if (level != 6)
            {
                M.I32(p + 0x2ac, 6);
                if (level == 1) { q.SetColor_a67f70(0xff, 0x87, 0x87); return; }
                if (level == 2) { q.SetColor_a67f70(0xff, 0xcd, 0x87); return; }
                if (level == 3) { q.SetColor_a67f70(0xff, 0xff, 0x87); return; }
            }
            if (level != 4)
            {
                if (level == 5) { q.SetColor_a67f70(0x87, 0x87, 0xff); return; }
                if (level != 6) return;
            }
            q.SetColor_a67f70(0xff, 0xff, 0xff);
        }

        /// <summary>
        /// FUN_00aa5d10 — aims a particle at the target: heading +0x4c = direction of (dx, dz) (FUN_00a63f80, in 16-bit
        /// arithmetic; 981 spreads its rows by 360 / rows), distance +0x54, height difference +0xdc, and the flight
        /// time +0xcc = the first step count whose distance (step · speed +0xc0) passes it (unchanged if none &lt; 100).
        /// </summary>
        void AimParticle_aa5d10(FxRuntime rt, uint p)
        {
            float dz = M.F32(A + 0x1c) - M.F32(A + 0x10);
            float dx = M.F32(A + 0x14) - M.F32(A + 8);
            M.F32(A + 0xdc, M.F32(A + 0x18) - M.F32(A + 0xc));
            float ang = FxRuntime.Direction_a63f80(dx, dz);
            int a16 = FxOps.Cvtt(ang) & 0xffff;
            int add = 0;
            if (Id == 0x3d5)
            {
                int quot = 0x168 / M.I32(A + 0xd4);
                add = ((short)quot * (int)M.I16(p + 0x10)) & 0xffff;
            }
            int sum = (short)(add + a16);
            M.I32(p + 0x4c, sum % 0x168);
            float len = FxRuntime.SqrtF(dz * dz + dx * dx);
            float speed = M.F32(p + 0xc0);
            M.F32(p + 0x54, len);
            for (int i = 0; i < 100; i++)
            {
                if ((float)i * speed > len)
                {
                    M.I32(A + 0xcc, i);
                    return;
                }
            }
        }

        /// <summary>
        /// CThrowItemEffect::vf03_aa5480 — once the start delay +0xd0 has passed: the first particle of a row turns
        /// 45° a tick, the ghosts copy the previous particle (alpha −150 for the first ghost, −25 after); while the
        /// flight fraction t = tick / +0xcc is ≤ 1.05 the lead moves along its heading at +0xc0 per tick (height
        /// by +0xdc) with an arc of sin(180 t) · +0xe0, afterwards it fades by 20 a tick. Visible particles get
        /// the matrix Scale(size, size, 1) × RotX(camera latitude) · RotY(angle) × Translation(position − arc).
        /// 981 also spins the owner's heading.
        /// </summary>
        void ThrowItemProcess_aa5480(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0xf8); pp != M.U32(A + 0xfc); pp += 4)
            {
                uint p = M.U32(pp);
                M.U32(p + 0x24, M.U32(A + 8));
                M.U32(p + 0x28, M.U32(A + 0xc));
                M.U32(p + 0x2c, M.U32(A + 0x10));
                if (p != 0 && M.I32(A + 0xd0) <= M.I32(A + 0x28)) ThrowItemParticleStep(rt, p);
                M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
            }
        }

        void ThrowItemParticleStep(FxRuntime rt, uint p)
        {
            int col = M.I32(p + 0xc);
            if (col == 0)
            {
                float a = M.F32(p + 0xc4) + 45.0f;
                M.F32(p + 0xc4, (float)(FxOps.Cvtt(a) % 0x168));
            }
            else
            {
                M.I32(A + 0xe4, M.I32(A + 0xe4) - (col == 1 ? 0x96 : 0x19));
                M.I32(p + 0x44, M.I32(A + 0xe4));
                M.U32(p + 0xd0, M.U32(A + 0xec));
                M.U32(p + 0xd4, M.U32(A + 0xf0));
                M.U32(p + 0xd8, M.U32(A + 0xf4));
                M.U32(p + 0xc4, M.U32(A + 0xe8));
            }
            float t = (float)M.I32(A + 0x28) / (float)M.I32(A + 0xcc);
            float arc = 0f;
            if (t > 1.05f)
            {
                int a = M.I32(p + 0x44) - 0x14;
                M.I32(p + 0x44, a >= 0 ? a : 0);
            }
            else
            {
                float speed = M.F32(p + 0xc0);
                float dy = speed / M.F32(p + 0x54);
                float t180 = t * -180.0f;
                int head = M.I32(p + 0x4c);
                float nx = speed * FxMath.CosTableAt(head);
                dy *= M.F32(A + 0xdc);
                float nz = speed * FxMath.SinTableAt(head);
                dy += M.F32(p + 0xd4);
                nx += M.F32(p + 0xd0);
                nz += M.F32(p + 0xd8);
                int k = FxOps.Cvtt(t180);
                M.F32(p + 0xd0, nx);
                M.F32(p + 0xd4, dy);
                M.F32(p + 0xd8, nz);
                arc = FxMath.SinTableAt(-k) * M.F32(A + 0xe0);
            }
            M.U32(A + 0xec, M.U32(p + 0xd0));
            M.U32(A + 0xf0, M.U32(p + 0xd4));
            M.U32(A + 0xf4, M.U32(p + 0xd8));
            M.U32(A + 0xe8, M.U32(p + 0xc4));
            M.U32(A + 0xe4, M.U32(p + 0x44));
            if (M.I32(p + 0x44) <= 0) return;
            uint view = M.U32(rt.Mode + 0xd0);
            Span<float> r = stackalloc float[12];
            Span<float> s = stackalloc float[12];
            Span<float> tr = stackalloc float[12];
            Span<float> sr = stackalloc float[12];
            Mat43.RotationX(M.F32(view + 0x2c) / 180.0f * 3.141592f, r);
            Mat43.RotateY(r, M.F32(p + 0xc4) / 180.0f * 3.141592f);
            float size = M.F32(p + 0x20);
            Mat43.Scale(size, size, 1.0f, s);
            float y = M.F32(p + 0xd4) - arc;
            Mat43.Translation(M.F32(p + 0xd0), y, M.F32(p + 0xd8), tr);
            Mat43.Mul(s, r, sr);
            Mat43.Mul(sr, tr, s);                            // s is free again: the product
            Mat43.Store(M, p + 0x1c8, s);
            if (Id == 0x3d5 && M.I32(p + 0x10) == 0 && M.I32(p + 0xc) == 0)
            {
                uint owner = rt.ActorByAid_a64480(M.U32(A + 0x20));
                if (owner != 0)
                {
                    float h = M.F32(owner + 0x4c) + 45.0f;
                    M.F32(owner + 0x4c, (float)(FxOps.Cvtt(h) % 0x168));
                }
            }
        }

        /// <summary>CThrowItemEffect::vf04_aa5c70 — every particle with its own matrix (not billboarded), depth on.</summary>
        void ThrowItemRender_aa5c70(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0xf8); pp != M.U32(A + 0xfc); pp += 4)
            {
                uint p = M.U32(pp);
                if (p != 0) new CParticle(M, p).Render_a67b60(rt, true, 0f, 1.0f, 0f, 1.0f, true);
            }
        }
    }

    public sealed partial class FxRuntime
    {
        /// <summary>
        /// The CRT's acos (_libm_sse2_acos_precise, reached through the thunk 0xd94c3d) as the port computes it;
        /// the oracle services that import with this same function.
        /// </summary>
        public static double AcosPrecise(double v) => Math.Acos(v);

        /// <summary>
        /// FUN_00a63f80 — direction of (dx, dz) in degrees: acos(dx / |(dx, dz)|) (x87 fsqrt, SSE reciprocal, CRT acos
        /// in double) / 3.141592 · 180, mirrored to 360 − a when dz &lt; 0 (COMISS; JBE: unordered keeps a).
        /// </summary>
        public static float Direction_a63f80(float dx, float dz)
        {
            float len = SqrtF(dx * dx + dz * dz);
            float v = 1.0f / len;
            v *= dx;
            float a = (float)AcosPrecise(v);
            a /= 3.141592f;
            a *= 180.0f;
            if (0f > dz) return (float)(360.0 - (double)a);
            return a;
        }
    }
}
