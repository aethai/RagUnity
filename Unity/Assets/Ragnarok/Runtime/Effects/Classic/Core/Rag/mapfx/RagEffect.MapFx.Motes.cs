// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.Motes.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// EF 703 / 710 / 712 — the Splendide light motes: rising / circling light specks (prim type 0x1d, CRagEffect::fn_b80030), with
    /// glow-light columns (fn_b70c60, prim 0x52) or an upward light cone (fn_b6e560, prim 0x81). packages/mapfx.
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 703 (0x2bf; vf15 case 0xb4ab28): fn_b6e560(this, "effect\alpha_upper.tga", 0); fn_b80030(this, 4).</summary>
        [FxEffectProcess(0x2bf)]
        public FxCaseResult Case_Map703(FxRuntime rt)
        {
            Fn_b6e560(rt, MapFxNames.AlphaUpper, 0);
            Fn_b80030(rt, 4);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 710 (0x2c6; vf15 case 0xb4aa44): fn_b70c60(this, "effect\b.bmp", 0x12), (glow02.bmp, 0x10), (glow02.bmp, 0x11);
        /// fn_b80030(this, 5).
        /// </summary>
        [FxEffectProcess(0x2c6)]
        public FxCaseResult Case_Map710(FxRuntime rt)
        {
            Fn_b70c60(rt, MapFxNames.B, 0x12);
            Fn_b70c60(rt, MapFxNames.Glow02, 0x10);
            Fn_b70c60(rt, MapFxNames.Glow02, 0x11);
            Fn_b80030(rt, 5);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 712 (0x2c8; vf15 case 0xb4aa7c — the decompiler's body for 0x2c8 is another case's): fn_b70c60(this, "effect\b.bmp",
        /// 0x12), (glow03.bmp, 0x10), (glow03.bmp, 0x11); fn_b80030(this, 7).
        /// </summary>
        [FxEffectProcess(0x2c8)]
        public FxCaseResult Case_Map712(FxRuntime rt)
        {
            Fn_b70c60(rt, MapFxNames.B, 0x12);
            Fn_b70c60(rt, MapFxNames.Glow03, 0x10);
            Fn_b70c60(rt, MapFxNames.Glow03, 0x11);
            Fn_b80030(rt, 7);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b6e560 (0xb6e560) — on tick 0 only: position = the owner's, one prim type 0x81 (RP flags +0x1c4 = 5, life =
        /// the effect's) with texture 0 = <paramref name="texture"/>. All four slots cleared, then slots k = 0..2: live +0x3e8 = 1,
        /// counter +0x3f4 = 0, alpha +0x3ea = 30, +0x468 = 0, radius +0x3f0 = base +0x3fc = 0.5k + 3, angle +0x3f8 = 25k,
        /// bottom centre +0x478.. = the effect position, top centre +0x490.. = the effect position − (0, 40, 0) (also left in
        /// the effect's +0x180..+0x188), kind +0x450. No RSW param is read.
        /// </summary>
        public void Fn_b6e560(FxRuntime rt, string texture, sbyte kind)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x81, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            for (uint o = 0; o < 0x2d0; o += 0xb4) M.U8(p.A + o + 0x3e8, 0);
            for (int k = 0; k < 3; k++)
            {
                uint s = p.A + (uint)k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, 0);
                M.I16(s + 0x3ea, 0x1e);
                M.U32(s + 0x468, 0);
                M.F32(s + 0x3f0, (float)k * 0.5f + 3f);                                 // [0xf4a460], [0xf4ec9c]
                M.U32(s + 0x3fc, M.U32(s + 0x3f0));
                M.I16(s + 0x3f8, (short)(k * 0x19));
                M.U32(s + 0x478, M.U32(A + 0x10));
                M.U32(s + 0x47c, M.U32(A + 0x14));
                M.U32(s + 0x480, M.U32(A + 0x18));
                uint x = M.U32(A + 0x10);
                M.U32(A + 0x180, x);
                M.U32(A + 0x184, M.U32(A + 0x14));
                M.U32(A + 0x188, M.U32(A + 0x18));
                M.F32(A + 0x184, M.F32(A + 0x184) - 40f);                               // [0xf7d138]
                M.U32(s + 0x490, x);
                M.U32(s + 0x494, M.U32(A + 0x184));
                M.U32(s + 0x498, M.U32(A + 0x188));
                M.U8(s + 0x450, (byte)kind);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b80030 (0xb80030) — follows the owner; on tick 0 (or when the counter reaches 99999998: counter 0, life
        /// 99999999, another prim) one prim type 0x1d (RP flags +0x1c4 = 1 | 5, life = the effect's) with texture 0 =
        /// effect\ghost.bmp (kind 3), effect\whitelight.tga (kinds ≥ 1; +0x318 = 7 for kind 2, else 0) or
        /// effect\freezing_circle.bmp (kinds ≤ 0). Four slots of 0xb4, each four specks (positions x/y/z at +0x484 / +0x46c /
        /// +0x490 / +0x478 …, two (current, target) angle pairs each from +0x3fc + 16·i). Per slot n: live +0x3e8 = 1 (0 for
        /// n ≥ 1 with kind 3, n ≥ 2 with kinds 7 / 8), +0x3f0 = 0.15 (kind 3 live: 0.6), size +0x468 = 2.4 (kinds 1 / 4 / 5 / 6
        /// / ≥ 8), 3.2 (kind 3), 0.8 (else, and dead kind-7 slots); speck x/z = 0, kind 3: rand % 15 − 8, kind 6 (x only):
        /// rand % 35 − 17; y = rand % 100; the angles rand % 360. Kind 4: y = −40 / −50 / −60 / −70, +0x450 = 14; kinds 5 / 7: y =
        /// 0; kind 6: y = −40 − rand % 40, +0x450 = 16; +0x450 = 1 (kind 1), 11 (kind 3), 15 with +0x451..+0x454 = 0 and
        /// +0x455 = kind (kinds 5 / 7 / 8), else 0. No RSW param is read.
        /// </summary>
        public void Fn_b80030(FxRuntime rt, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt == 0x5f5e0fe)
            {
                m_stateCnt = 0;
                f_17c = 0x5f5e0ff;
            }
            else if (m_stateCnt != 0) return;

            Prim p = LaunchEffectPrim(rt, 0x1d, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 0;
            p.f_1c4 |= 1;
            p.f_1c4 |= 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            if (kind == 3)
            {
                M.U32((uint)p.f_144, rt.GetTexture(MapFxNames.GhostBmp));
                p.f_318 = 0f;
            }
            else if (kind >= 1)
            {
                M.U32((uint)p.f_144, rt.GetTexture(MapFxNames.WhiteLight));
                p.f_318 = 0f;
                if (kind == 2) M.U32(p.A + 0x318, 0x40e00000);                          // 7.0
            }
            else
            {
                M.U32((uint)p.f_144, rt.GetTexture(MapFxNames.FreezingCircle));
                p.f_318 = 0f;
            }

            for (int n = 0; n < 4; n++)
            {
                uint s = p.A + (uint)n * 0xb4;
                Slot_b80030(rt, s, kind, n);
            }
        }

        /// <summary>One slot of fn_b80030 (0xb801b0..0xb80b8b), in the original's store and rand() order.</summary>
        void Slot_b80030(FxRuntime rt, uint s, sbyte kind, int n)
        {
            // live flag, +0x3f0, size +0x468 and speck 0's x/z (+0x484 / +0x48c) — the branch web at 0xb801b0
            bool live = !((kind == 3 && n >= 1) || ((kind == 7 || kind == 8) && n >= 2));
            M.U8(s + 0x3e8, (byte)(live ? 1 : 0));
            M.U32(s + 0x3f0, 0x3e19999a);                                               // 0.15
            if (kind == 3)
            {
                M.U32(s + 0x468, 0x404ccccd);                                           // 3.2
                M.U32(s + 0x3f0, 0x3f19999a);                                           // 0.6
                M.F32(s + 0x484, (float)(RandMod(rt, 0xf) - 8));
                M.F32(s + 0x48c, (float)(RandMod(rt, 0xf) - 8));
            }
            else
            {
                if (kind == 7 && !live) M.U32(s + 0x468, 0x3f4ccccd);                   // 0.8
                else if (kind == 1 || kind == 4 || kind == 5 || kind == 6 || kind >= 8) M.U32(s + 0x468, 0x4019999a);   // 2.4
                else M.U32(s + 0x468, 0x3f4ccccd);                                      // 0.8 (kinds ≤ 0, 2, 7)
                if (kind == 6) M.F32(s + 0x484, (float)(RandMod(rt, 0x23) - 0x11));
                else M.U32(s + 0x484, 0);
                M.U32(s + 0x48c, 0);
            }

            // speck 0: y, angles
            M.F32(s + 0x488, (float)RandMod(rt, 100));
            M.F32(s + 0x3fc, (float)RandMod(rt, 0x168));
            M.F32(s + 0x400, (float)RandMod(rt, 0x168));
            M.F32(s + 0x404, (float)RandMod(rt, 0x168));
            M.F32(s + 0x408, (float)RandMod(rt, 0x168));
            // specks 1..3: x, z, y, angles
            SpeckInit_b80030(rt, s, kind, 0x46c, 0x474, 0x470, 0x40c);
            SpeckInit_b80030(rt, s, kind, 0x490, 0x498, 0x494, 0x41c);
            SpeckInit_b80030(rt, s, kind, 0x478, 0x480, 0x47c, 0x42c);

            if (kind == 4)
            {
                M.U32(s + 0x488, 0xc2200000);                                           // -40
                M.U32(s + 0x470, 0xc2480000);                                           // -50
                M.U32(s + 0x494, 0xc2700000);                                           // -60
                M.U32(s + 0x47c, 0xc28c0000);                                           // -70
                M.U8(s + 0x450, 0xe);
                return;
            }
            if (kind == 5 || kind == 7)
            {
                M.U32(s + 0x488, 0);
                M.U32(s + 0x470, 0);
                M.U32(s + 0x494, 0);
                M.U32(s + 0x47c, 0);
            }
            else if (kind == 6)
            {
                M.F32(s + 0x488, (float)(-0x28 - RandMod(rt, 0x28)));
                M.F32(s + 0x470, (float)(-0x28 - RandMod(rt, 0x28)));
                M.F32(s + 0x494, (float)(-0x28 - RandMod(rt, 0x28)));
                M.F32(s + 0x47c, (float)(-0x28 - RandMod(rt, 0x28)));
                M.U8(s + 0x450, 0x10);
                return;
            }
            if (kind == 1) M.U8(s + 0x450, 1);
            else if (kind == 3) M.U8(s + 0x450, 0xb);
            else if (kind == 5 || kind == 7 || kind == 8)
            {
                M.U8(s + 0x450, 0xf);
                M.U8(s + 0x451, 0);
                M.U8(s + 0x452, 0);
                M.U8(s + 0x453, 0);
                M.U8(s + 0x454, 0);
                M.U8(s + 0x455, (byte)kind);
            }
            else M.U8(s + 0x450, 0);
        }

        /// <summary>Specks 1..3 of a fn_b80030 slot: x / z (kind 3: rand % 15 − 8 both; kind 6: x = rand % 35 − 17, z = 0; else 0), y = rand % 100, four angles rand % 360.</summary>
        void SpeckInit_b80030(FxRuntime rt, uint s, sbyte kind, uint xo, uint zo, uint yo, uint angles)
        {
            if (kind == 3)
            {
                M.F32(s + xo, (float)(RandMod(rt, 0xf) - 8));
                M.F32(s + zo, (float)(RandMod(rt, 0xf) - 8));
            }
            else
            {
                if (kind == 6) M.F32(s + xo, (float)(RandMod(rt, 0x23) - 0x11));
                else M.U32(s + xo, 0);
                M.U32(s + zo, 0);
            }
            M.F32(s + yo, (float)RandMod(rt, 100));
            for (uint k = 0; k < 0x10; k += 4) M.F32(s + angles + k, (float)RandMod(rt, 0x168));
        }
    }
}
