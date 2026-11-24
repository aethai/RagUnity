// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.Waterfalls.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The map waterfalls and their spray (packages/mapfx): EF 557..560 (fn_b6a4f0, prim 0x4f), EF 349..356 (fn_b90c90 +
    /// fn_b91030, prims 0x4f / 0x50), EF 846 (fn_b9f770, prim 0x96), and the magic rings EF 561 / 562 (fn_b85b10, prims 0x3c / 0x3e).
    /// </summary>
    public readonly partial struct RagEffect
    {
        static readonly string[] Waterfall1x =
            { "effect\\waterfall11.tga", "effect\\waterfall12.tga", "effect\\waterfall13.tga" };   // 0xffb704 / 0xffb71c / 0xffb734

        static readonly string[] Waterfall3x =
            { "effect\\waterfall31.tga", "effect\\waterfall32.tga", "effect\\waterfall33.tga" };   // 0xffb74c / 0xffb764 / 0xffb77c

        static readonly string[] AlphaDown3x =
            { "effect\\alpha_down.tga", "effect\\alpha_down.tga", "effect\\alpha_down.tga" };     // 0xf80c58

        // ---- EF 557..562 (vf15) ----------------------------------------------------------------------------------------

        /// <summary>EF 557 (0x22d; vf15 case 0xb4d0e9): fn_b6a4f0(this, 0, 0, 0).</summary>
        [FxEffectProcess(0x22d)]
        public FxCaseResult Case_Map557(FxRuntime rt) { Fn_b6a4f0(rt, 0, 0, 0); return FxCaseResult.Continue; }

        /// <summary>EF 558 (0x22e; vf15 case 0xb4d0f9): fn_b6a4f0(this, 1, 0, 0).</summary>
        [FxEffectProcess(0x22e)]
        public FxCaseResult Case_Map558(FxRuntime rt) { Fn_b6a4f0(rt, 1, 0, 0); return FxCaseResult.Continue; }

        /// <summary>EF 559 (0x22f; vf15 case 0xb4d109): fn_b6a4f0(this, 0, 1, 0).</summary>
        [FxEffectProcess(0x22f)]
        public FxCaseResult Case_Map559(FxRuntime rt) { Fn_b6a4f0(rt, 0, 1, 0); return FxCaseResult.Continue; }

        /// <summary>EF 560 (0x230; vf15 case 0xb4d119): fn_b6a4f0(this, 1, 1, 0).</summary>
        [FxEffectProcess(0x230)]
        public FxCaseResult Case_Map560(FxRuntime rt) { Fn_b6a4f0(rt, 1, 1, 0); return FxCaseResult.Continue; }

        /// <summary>EF 561 (0x231; vf15 case 0xb4d129): fn_b85b10(this, 0).</summary>
        [FxEffectProcess(0x231)]
        public FxCaseResult Case_Map561(FxRuntime rt) { Fn_b85b10(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 562 (0x232; vf15 case 0xb4d135): fn_b85b10(this, 1).</summary>
        [FxEffectProcess(0x232)]
        public FxCaseResult Case_Map562(FxRuntime rt) { Fn_b85b10(rt, 1); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b6a4f0 (0xb6a4f0) — tick 0 only: position = the owner's, one prim 0x4f (RP flags 5, life = the effect's,
        /// three textures: <paramref name="set"/> 0 waterfall11..13, 1 waterfall31..33, 10 alpha_down ×3, other none). Four slots k:
        /// live, counter 0, alpha 120, orientation +0x450 = <paramref name="orient"/>, width +0x468 = k + 36, +0x452 = 10, 20 band
        /// heights 0, 40 … 760 from +0x3fc, depth +0x474 = 0.25k − 1; <paramref name="speed"/> 0: +0x3f0 = 80 − 13k, 1: 30 − 6k,
        /// 10: 0 with alpha 70 and +0x451 = 10 (else +0x451 = 1). No RSW param is read.
        /// </summary>
        public void Fn_b6a4f0(FxRuntime rt, sbyte orient, sbyte speed, sbyte set)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x4f, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 3;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            string[] tex = set == 0 ? Waterfall1x : set == 1 ? Waterfall3x : set == 10 ? AlphaDown3x : null;
            if (tex != null)
                for (uint i = 0; i < 3; i++) M.U32((uint)p.f_144 + i * 4, rt.GetTexture(tex[i]));
            for (int k = 0; k < 4; k++)
            {
                uint s = p.A + (uint)k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, 0);
                M.I16(s + 0x3ea, 0x78);
                M.U8(s + 0x450, (byte)orient);
                M.F32(s + 0x468, (float)(k + 0x24));
                M.U8(s + 0x452, 10);
                WaterfallBands(s);
                M.F32(s + 0x474, (float)k * 0.25f - 1f);                                    // [0xf80824] [0xf4a468]
                byte mode = 1;
                if (speed == 0) M.F32(s + 0x3f0, (float)(0x50 - 0xd * k));
                else if (speed == 1) M.F32(s + 0x3f0, (float)(0x1e - 6 * k));
                else if (speed == 10)
                {
                    M.U32(s + 0x3f0, 0);
                    M.I16(s + 0x3ea, 0x46);
                    mode = 10;
                }
                M.U8(s + 0x451, mode);
            }
        }

        /// <summary>The 20 band heights of a waterfall slot: +0x3fc + 4j = 40j (j = 0..19).</summary>
        void WaterfallBands(uint s)
        {
            for (int j = 0; j < 20; j++) M.F32(s + 0x3fc + (uint)j * 4, (float)(j * 40));
        }

        /// <summary>
        /// CRagEffect::fn_b85b10 (0xb85b10) — no position update. Tick 0: prim 0x3c (RP flags 5, effect life) with
        /// effect\ring_red.tga (<paramref name="kind"/> 3, +0x318 = 10) or effect\magic_violet.tga (+0x318 = 4); slots 0..2 live:
        /// counter 0, +0x3ee 360, +0x3f0 80, angle +0x3f8 = rand() % 360, alpha 0, radius +0x468 7 / 5.5 / 4, +0x466 90, spin +0x3ec
        /// = −1 (kind 1) else 1400, +0x470 0, 21 floats from +0x3fc = 0, 21 bytes from +0x450 = 3; slot 3 off. Tick 2: prim 0x3e
        /// with effect\cloud11.tga (+0x318 10 / 4): four live slots, +0x3ee 30, +0x3f0 2.5 / 5 / 7.5 / 10, +0x3f8 0 / 90 / 180 /
        /// 270, alpha 0, +0x468 13 / 11 / 9 / 8, +0x466 90, +0x3ec 3, 21 floats 1.5 and 21 bytes 0. No RSW param is read.
        /// </summary>
        public void Fn_b85b10(FxRuntime rt, sbyte kind)
        {
            int st = m_stateCnt;
            if (st == 0)
            {
                // FUN_00a03ba0() is called first; its result is unused.
                Prim p = LaunchEffectPrim(rt, 0x3c, 0f, 0f, 0f);
                m_lastPrim = p;
                p.f_1c4 = 5;
                p.m_duration = f_17c;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                if (kind == 3)
                {
                    M.U32((uint)p.f_144, rt.GetTexture("effect\\ring_red.tga"));             // 0xff5080
                    M.U32(p.A + 0x318, 0x41200000);                                         // 10
                }
                else
                {
                    M.U32((uint)p.f_144, rt.GetTexture("effect\\magic_violet.tga"));         // 0xffabd8
                    M.U32(p.A + 0x318, 0x40800000);                                         // 4
                }
                short spin = (short)(kind != 1 ? 0x578 : -1);
                RingSlot_b85b10(rt, p.A, 0x40e00000, spin);                                 // 7
                RingSlot_b85b10(rt, p.A + 0xb4, 0x40b00000, spin);                          // 5.5
                RingSlot_b85b10(rt, p.A + 0x168, 0x40800000, spin);                         // 4
                M.U8(p.A + 0x604, 0);
                st = m_stateCnt;
            }
            if (st != 2) return;
            {
                Prim p = LaunchEffectPrim(rt, 0x3e, 0f, 0f, 0f);
                m_lastPrim = p;
                p.f_1c4 = 5;
                p.m_duration = f_17c;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                M.U32((uint)p.f_144, rt.GetTexture("effect\\cloud11.tga"));                 // 0xf82238
                M.F32(p.A + 0x318, kind == 3 ? 10f : 4f);                                    // [0xf4b624] / [0xf4a764]
                CloudSlot_b85b10(p.A, 0x40200000, 0, 0x41500000);                           // 2.5, 0, 13
                CloudSlot_b85b10(p.A + 0xb4, 0x40a00000, 0x5a, 0x41300000);                 // 5, 90, 11
                CloudSlot_b85b10(p.A + 0x168, 0x40f00000, 0xb4, 0x41100000);                // 7.5, 180, 9
                CloudSlot_b85b10(p.A + 0x21c, 0x41200000, 0x10e, 0x41000000);               // 10, 270, 8
            }
        }

        /// <summary>One prim-0x3c slot of fn_b85b10 (see there), with one rand() for the angle.</summary>
        void RingSlot_b85b10(FxRuntime rt, uint s, uint radiusBits, short spin)
        {
            M.U8(s + 0x3e8, 1);
            M.I32(s + 0x3f4, 0);
            M.I16(s + 0x3ee, 0x168);
            M.U32(s + 0x3f0, 0x42a00000);                                                   // 80
            M.I16(s + 0x3f8, (short)RandMod(rt, 0x168));
            M.I16(s + 0x3ea, 0);
            M.U32(s + 0x468, radiusBits);
            M.I16(s + 0x466, 0x5a);
            M.I16(s + 0x3ec, spin);
            M.U32(s + 0x470, 0);
            for (uint j = 0; j < 21; j++)
            {
                M.U32(s + 0x3fc + j * 4, 0);
                M.U8(s + 0x450 + j, 3);
            }
        }

        /// <summary>One prim-0x3e slot of fn_b85b10 (see there).</summary>
        void CloudSlot_b85b10(uint s, uint speedBits, short angle, uint radiusBits)
        {
            M.U8(s + 0x3e8, 1);
            M.I32(s + 0x3f4, 0);
            M.I16(s + 0x3ee, 0x1e);
            M.U32(s + 0x3f0, speedBits);
            M.I16(s + 0x3f8, angle);
            M.I16(s + 0x3ea, 0);
            M.U32(s + 0x468, radiusBits);
            M.I16(s + 0x466, 0x5a);
            M.I16(s + 0x3ec, 3);
            for (uint j = 0; j < 21; j++)
            {
                M.U32(s + 0x3fc + j * 4, 0x3fc00000);                                       // 1.5
                M.U8(s + 0x450 + j, 0);
            }
        }

        // ---- EF 349..356 (vf14) -----------------------------------------------------------------------------------------

        /// <summary>EF 349 (0x15d; vf14 case 0xb463e9): fn_b90c90(this, "", 0, 0, 0); 40 × fn_b91030(this, 0, 0).</summary>
        [FxEffectProcess(0x15d)]
        public FxCaseResult Case_Map349(FxRuntime rt) => Spray_vf14(rt, 0, 0, 0, 40);

        /// <summary>EF 350 (0x15e; vf14 case 0xb46419): fn_b90c90(this, "", 1, 0, 0); 40 × fn_b91030(this, 1, 0).</summary>
        [FxEffectProcess(0x15e)]
        public FxCaseResult Case_Map350(FxRuntime rt) => Spray_vf14(rt, 1, 0, 0, 40);

        /// <summary>EF 351 (0x15f; vf14 case 0xb46449): fn_b90c90(this, "", 0, 1, 0); 20 × fn_b91030(this, 0, 1).</summary>
        [FxEffectProcess(0x15f)]
        public FxCaseResult Case_Map351(FxRuntime rt) => Spray_vf14(rt, 0, 1, 0, 20);

        /// <summary>EF 352 (0x160; vf14 case 0xb46479): fn_b90c90(this, "", 1, 1, 0); 20 × fn_b91030(this, 1, 1).</summary>
        [FxEffectProcess(0x160)]
        public FxCaseResult Case_Map352(FxRuntime rt) => Spray_vf14(rt, 1, 1, 0, 20);

        /// <summary>EF 353 (0x161; vf14 case 0xb464a9): fn_b90c90(this, "", 0, 0, 1); 40 × fn_b91030(this, 0, 0).</summary>
        [FxEffectProcess(0x161)]
        public FxCaseResult Case_Map353(FxRuntime rt) => Spray_vf14(rt, 0, 0, 1, 40);

        /// <summary>EF 354 (0x162; vf14 case 0xb464d9): fn_b90c90(this, "", 1, 0, 1); 40 × fn_b91030(this, 1, 0).</summary>
        [FxEffectProcess(0x162)]
        public FxCaseResult Case_Map354(FxRuntime rt) => Spray_vf14(rt, 1, 0, 1, 40);

        /// <summary>EF 355 (0x163; vf14 case 0xb46509): fn_b90c90(this, "", 0, 1, 1); 20 × fn_b91030(this, 0, 1).</summary>
        [FxEffectProcess(0x163)]
        public FxCaseResult Case_Map355(FxRuntime rt) => Spray_vf14(rt, 0, 1, 1, 20);

        /// <summary>EF 356 (0x164; vf14 case 0xb46539): fn_b90c90(this, "", 1, 1, 1); 20 × fn_b91030(this, 1, 1).</summary>
        [FxEffectProcess(0x164)]
        public FxCaseResult Case_Map356(FxRuntime rt) => Spray_vf14(rt, 1, 1, 1, 20);

        /// <summary>The vf14 waterfall cases: fn_b90c90(this, "" (0xf432d8), a, b, c), then n × fn_b91030(this, a, b); mov al, 1.</summary>
        FxCaseResult Spray_vf14(FxRuntime rt, sbyte a, sbyte b, sbyte c, int n)
        {
            Fn_b90c90(rt, a, b, c);
            for (int i = 0; i < n; i++) Fn_b91030(rt, a, b);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b90c90 (0xb90c90; its string argument is never read) — tick 0 only: position = the owner's, one prim 0x4f
        /// (RP flags 5, effect life, three textures: <paramref name="set"/> 0 waterfall11..13, 1 waterfall31..33, other none). Four
        /// slots k: live, counter 0, alpha 80, orientation +0x450 = <paramref name="orient"/>, width +0x468 = k + 36
        /// (<paramref name="narrow"/> 0) / k + 18 (1) / untouched, <b>+0x452 = (char)(int)p0</b> (the RSW param: values above 10 stretch
        /// the bands by 10 / p0 in the render), bands 0, 40 … 760, depth +0x474 = k − 1, speed +0x3f0 = 80 − 13k, +0x451 = 0.
        /// </summary>
        public void Fn_b90c90(FxRuntime rt, sbyte orient, sbyte narrow, sbyte set)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x4f, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 3;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            string[] tex = set == 0 ? Waterfall1x : set == 1 ? Waterfall3x : null;
            if (tex != null)
                for (uint i = 0; i < 3; i++) M.U32((uint)p.f_144 + i * 4, rt.GetTexture(tex[i]));
            for (int k = 0; k < 4; k++)
            {
                uint s = p.A + (uint)k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, 0);
                M.I16(s + 0x3ea, 0x50);
                M.U8(s + 0x450, (byte)orient);
                if (narrow == 0) M.F32(s + 0x468, (float)(k + 0x24));
                else if (narrow == 1) M.F32(s + 0x468, (float)(k + 0x12));
                M.U8(s + 0x452, (byte)FxOps.Cvtt(M.F32(A + 0x13c)));
                WaterfallBands(s);
                M.F32(s + 0x474, (float)k - 1f);                                            // [0xf4a468]
                M.F32(s + 0x3f0, (float)(0x50 - 0xd * k));
                M.U8(s + 0x451, 0);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b91030 (0xb91030) — tick 0 only: position = the owner's, one prim 0x50 (RP flags 5, effect life, one
        /// texture: rand() % 10 &lt; 1 → effect\waterp1.tga with size 7, else effect\freeze_a_small.bmp with size 4.5), +0x318 = 4.
        /// Four slots: live, speed +0x3f0 = 0.05, swap flag +0x45b = <paramref name="swap"/>, spread +0x45c = <paramref name="narrow"/>,
        /// kind +0x450 = 2; four drops each (x / y / z at +0x484.. / +0x46c.. / +0x490.. / +0x478..): x = rand % 45 − 22 (spread 0) or
        /// rand % 23 − 11, y = rand % 30, z = rand % 11 − 5, then four wander angles rand % 360; swap 1 → x = z, z = (float)(int)x.
        /// </summary>
        public void Fn_b91030(FxRuntime rt, sbyte swap, sbyte narrow)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x50, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            bool water = RandMod(rt, 10) < 1;
            M.U32((uint)p.f_144, rt.GetTexture(water ? "effect\\waterp1.tga" : "effect\\freeze_a_small.bmp"));   // 0xffb7b0 / 0xffb794
            p.f_318 = 4f;
            for (int k = 0; k < 4; k++)
            {
                uint s = p.A + (uint)k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.U32(s + 0x3f0, 0x3d4ccccd);                                               // 0.05
                M.U8(s + 0x45b, (byte)swap);
                M.U8(s + 0x45c, (byte)narrow);
                M.U32(s + 0x468, water ? 0x40e00000u : 0x40900000u);                        // [0xf4b620] 7 / [0xff4c0c] 4.5
                Drop_b91030(rt, s, 0x484, 0x488, 0x48c, 0x3fc);
                Drop_b91030(rt, s, 0x46c, 0x470, 0x474, 0x40c);
                Drop_b91030(rt, s, 0x490, 0x494, 0x498, 0x41c);
                Drop_b91030(rt, s, 0x478, 0x47c, 0x480, 0x42c);
                M.U8(s + 0x450, 2);
            }
        }

        /// <summary>One drop of fn_b91030 in the original's rand() order (x, y, z, four angles), then the x / z swap.</summary>
        void Drop_b91030(FxRuntime rt, uint s, uint xo, uint yo, uint zo, uint angles)
        {
            int x = M.U8(s + 0x45c) == 0 ? RandMod(rt, 0x2d) - 0x16 : RandMod(rt, 0x17) - 0xb;
            M.F32(s + xo, (float)x);
            M.F32(s + yo, (float)RandMod(rt, 0x1e));
            M.F32(s + zo, (float)(RandMod(rt, 0xb) - 5));
            for (uint j = 0; j < 0x10; j += 4) M.F32(s + angles + j, (float)RandMod(rt, 0x168));
            if (M.U8(s + 0x45b) == 1)
            {
                int xi = FxOps.Cvtt(M.F32(s + xo));
                M.U32(s + xo, M.U32(s + zo));
                M.F32(s + zo, (float)xi);
            }
        }

        // ---- EF 846 (vf15) ----------------------------------------------------------------------------------------------

        /// <summary>EF 846 (0x34e; vf15 case 0xb48ba9): 40 × fn_b9f770(this, 0, 0) (both arguments unused, RET 8).</summary>
        [FxEffectProcess(0x34e)]
        public FxCaseResult Case_Map846(FxRuntime rt)
        {
            for (int i = 0; i < 40; i++) Fn_b9f770(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b9f770 (0xb9f770) — tick 0 only: position = the owner's, one prim 0x96 (RP flags 5, effect life, one
        /// texture: rand() % 10 &lt; 1 → effect\waterp1.tga (factor 7) else effect\freeze_a_small.bmp (4.5)), +0x318 = 4. Per slot
        /// (all four live, kind +0x450 = 2, speed +0x3f0 = 0.05) the RSW params: <b>yaw +0x3ee = (short)(int)p1 % 360</b>,
        /// <b>scale +0x466 = (short)(int)p0</b> with p0 below 10 (not NaN) raised to 10 in the effect's own +0x13c, s = +0x466 / 10,
        /// size +0x468 = s · factor, <b>colour digits of (uint)p2 % 1000000</b>: +0x45a = v / 10000, +0x45b = (v − (char)+0x45c · 10000)
        /// / 100 as a byte (reads +0x45c before it is written — client bug; maps: p2 1337755 with +0x45c 0 → 33, 49 (not 77), 55), +0x45c = v % 100.
        /// Four drops: drop 0 y = rand % 30, angles, x = rand % 45 − 22, z = rand % 11 − 5; drops 1..3 x, y, z, angles; each scaled
        /// by s and turned by the yaw (x' = cos·x − sin·z, z' = sin·x + cos·z, raw tables at the short yaw).
        /// </summary>
        public void Fn_b9f770(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x96, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            bool water = RandMod(rt, 10) <= 0;
            M.U32((uint)p.f_144, rt.GetTexture(water ? "effect\\waterp1.tga" : "effect\\freeze_a_small.bmp"));   // 0xffb7b0 / 0xffb794
            M.U32(p.A + 0x318, 0x40800000);                                                 // 4
            for (int k = 0; k < 4; k++)
            {
                uint s = p.A + (uint)k * 0xb4;
                M.I16(s + 0x3ee, (short)FxOps.Cvtt(M.F32(A + 0x140)));
                M.I16(s + 0x3ee, (short)(M.I16(s + 0x3ee) % 0x168));
                short yaw = M.I16(s + 0x3ee);
                float p0 = M.F32(A + 0x13c);
                if (10f > p0)                                                               // COMISS 10, p0; JBE (NaN kept)
                {
                    M.U32(A + 0x13c, 0x41200000);
                    p0 = 10f;
                }
                M.I16(s + 0x466, (short)FxOps.Cvtt(p0));
                float scale = (float)M.I16(s + 0x466) / 10f;                                // [0xf4b624]
                uint v = FxOps.FloatToUInt_d48d60(M.F32(A + 0x144)) % 1000000u;
                M.U8(s + 0x45a, (byte)(v / 10000u));
                M.U8(s + 0x45b, (byte)((v - (uint)((sbyte)M.U8(s + 0x45c) * 10000)) / 100u));
                M.U8(s + 0x45c, (byte)((byte)v - (byte)((sbyte)(byte)(v / 100u) * 100)));
                M.U8(s + 0x3e8, 1);
                M.U32(s + 0x3f0, 0x3d4ccccd);                                               // 0.05
                M.F32(s + 0x468, scale * (water ? 7f : 4.5f));                              // [0xf4b620] / [0xff4c0c]

                M.F32(s + 0x488, (float)RandMod(rt, 0x1e));
                for (uint j = 0; j < 0x10; j += 4) M.F32(s + 0x3fc + j, (float)RandMod(rt, 0x168));
                M.F32(s + 0x484, (float)(RandMod(rt, 0x2d) - 0x16));
                M.F32(s + 0x48c, (float)(RandMod(rt, 0xb) - 5));
                ScaleTurn_b9f770(s, 0x484, 0x488, 0x48c, scale, yaw);
                Drop_b9f770(rt, s, 0x46c, 0x470, 0x474, 0x40c, scale, yaw);
                Drop_b9f770(rt, s, 0x490, 0x494, 0x498, 0x41c, scale, yaw);
                Drop_b9f770(rt, s, 0x478, 0x47c, 0x480, 0x42c, scale, yaw);
                M.U8(s + 0x450, 2);
            }
        }

        /// <summary>Drops 1..3 of fn_b9f770: x = rand % 45 − 22, y = rand % 30, z = rand % 11 − 5, four angles, then scale and turn.</summary>
        void Drop_b9f770(FxRuntime rt, uint s, uint xo, uint yo, uint zo, uint angles, float scale, short yaw)
        {
            M.F32(s + xo, (float)(RandMod(rt, 0x2d) - 0x16));
            M.F32(s + yo, (float)RandMod(rt, 0x1e));
            M.F32(s + zo, (float)(RandMod(rt, 0xb) - 5));
            for (uint j = 0; j < 0x10; j += 4) M.F32(s + angles + j, (float)RandMod(rt, 0x168));
            ScaleTurn_b9f770(s, xo, yo, zo, scale, yaw);
        }

        /// <summary>x, z, y ×= scale; (x, z) turned by the yaw: x' = cos·x − sin·z, z' = sin·x + cos·z (tables 0x11a5570 / 0x11a4a28).</summary>
        void ScaleTurn_b9f770(uint s, uint xo, uint yo, uint zo, float scale, short yaw)
        {
            M.F32(s + xo, scale * M.F32(s + xo));
            M.F32(s + zo, scale * M.F32(s + zo));
            M.F32(s + yo, scale * M.F32(s + yo));
            float c = FxMath.CosTableAt(yaw), sn = FxMath.SinTableAt(yaw);
            float x = M.F32(s + xo), z = M.F32(s + zo);
            M.F32(s + xo, c * x - sn * z);
            M.F32(s + zo, sn * x + c * z);
        }
    }
}
