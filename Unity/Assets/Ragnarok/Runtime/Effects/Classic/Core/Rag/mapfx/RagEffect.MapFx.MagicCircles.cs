// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.MagicCircles.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The map magic circles (packages/mapfx): EF 650 / 651 / 687 / 688 / 695 — flat rotating circles (fn_b81d20, prim 0x1a),
    /// rising glitter (fn_b81f80, prim 0x1b), a twin rotating glow (fn_b82320, prim 0x1c) and a three-ring halo (fn_b817a0, prim 0x17).
    /// </summary>
    public readonly partial struct RagEffect
    {
        const string Mjin2 = "effect\\mjin2.tga";                                                  // 0xff4ec0
        const string Mjin = "effect\\mjin.tga";                                                    // 0xff4ed4
        const string Mpa3 = "effect\\mpa3.tga";                                                    // 0xff829c

        /// <summary>
        /// EF 650 (0x28a; vf15 case 0xb4ac48): fn_b81d20(this, "effect\mjin2.tga", 0), (mjin.tga, 1); fn_b82320(this,
        /// "effect\pikapika2.bmp"); fn_b817a0(this, "effect\ring_blue.tga").
        /// </summary>
        [FxEffectProcess(0x28a)]
        public FxCaseResult Case_Map650(FxRuntime rt)
        {
            Fn_b81d20(rt, Mjin2, 0);
            Fn_b81d20(rt, Mjin, 1);
            Fn_b82320(rt, "effect\\pikapika2.bmp");                                               // 0xff4c90
            Fn_b817a0(rt, "effect\\ring_blue.tga");                                               // 0xf7ddac
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 651 (0x28b; vf15 case 0xb4ac81): fn_b81d20(this, "effect\mjin2.tga", 2), (mjin.tga, 3); for i = 2, 4 … 20:
        /// fn_b81f80(this, "effect\mpa3.tga", i, 0).
        /// </summary>
        [FxEffectProcess(0x28b)]
        public FxCaseResult Case_Map651(FxRuntime rt)
        {
            Fn_b81d20(rt, Mjin2, 2);
            Fn_b81d20(rt, Mjin, 3);
            for (int i = 2; i < 0x15; i += 2) Fn_b81f80(rt, Mpa3, (sbyte)i, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 687 (0x2af; vf15 case 0xb4acbe): fn_b81d20(this, "effect\마법진1-1.bmp", 4), (마법진1-2.bmp, 14), (마법진1-3.bmp, 4).</summary>
        [FxEffectProcess(0x2af)]
        public FxCaseResult Case_Map687(FxRuntime rt)
        {
            Fn_b81d20(rt, "effect\\¸¶¹ýÁø1-1.bmp", 4);                // 0xff82ac
            Fn_b81d20(rt, "effect\\¸¶¹ýÁø1-2.bmp", 0xe);              // 0xff82c4
            Fn_b81d20(rt, "effect\\¸¶¹ýÁø1-3.bmp", 4);                // 0xff80f0
            return FxCaseResult.Continue;
        }

        /// <summary>EF 688 (0x2b0; vf15 case 0xb4aced): fn_b81d20(this, "effect\마법진2-1.bmp", 4), (마법진2-2.bmp, 14), (마법진2-3.bmp, 4).</summary>
        [FxEffectProcess(0x2b0)]
        public FxCaseResult Case_Map688(FxRuntime rt)
        {
            Fn_b81d20(rt, "effect\\¸¶¹ýÁø2-1.bmp", 4);                // 0xff82dc
            Fn_b81d20(rt, "effect\\¸¶¹ýÁø2-2.bmp", 0xe);              // 0xff82f4
            Fn_b81d20(rt, "effect\\¸¶¹ýÁø2-3.bmp", 4);                // 0xff830c
            return FxCaseResult.Continue;
        }

        /// <summary>EF 695 (0x2b7; vf15 case 0xb4abd7): fn_b81d20(this, "effect\glow04.bmp", 5); for i = 2, 4 … 20: fn_b81f80(this, "effect\mpa3.tga", i, 2).</summary>
        [FxEffectProcess(0x2b7)]
        public FxCaseResult Case_Map695(FxRuntime rt)
        {
            Fn_b81d20(rt, "effect\\glow04.bmp", 5);                                                // 0xff8288
            for (int i = 2; i < 0x15; i += 2) Fn_b81f80(rt, Mpa3, (sbyte)i, 2);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b81d20 (0xb81d20) — position = the owner's (every pass); tick 0: one prim 0x1a (RP flags 5, effect life,
        /// texture 0 = <paramref name="texture"/>), slot 0 live only. Render style +0x318 = 111 (<paramref name="kind"/> 4 / 14), 112
        /// (5), 110 (else); radius +0x468 = 40 (kinds 2 / 3), <b>p0 when 0 &lt; p0 or NaN, else 40</b> (kinds 4 / 14; maps 30 / 35), 30
        /// (else); kinds 4 / 5 / 14: angle +0x3f8 0, alpha 250, height +0x3fc −0.5; kinds 0 / 2: 0, 200, −0.5; others: 358, 50, −0.4;
        /// spin selector +0x40c = (float)kind (14 turns backwards).
        /// </summary>
        public void Fn_b81d20(FxRuntime rt, string texture, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x1a, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            if (kind == 4 || kind == 0xe) M.U32(p.A + 0x318, 0x42de0000);                  // 111
            else if (kind == 5) M.U32(p.A + 0x318, 0x42e00000);                            // 112
            else M.U32(p.A + 0x318, 0x42dc0000);                                           // 110
            M.U8(p.A + 0x3e8, 1);
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            if ((byte)(kind - 2) <= 1) M.U32(p.A + 0x468, 0x42200000);                     // 40
            else if (kind == 4 || kind == 0xe)
            {
                float p0 = M.F32(A + 0x13c);
                if (0f < p0 || float.IsNaN(p0)) M.U32(p.A + 0x468, M.U32(A + 0x13c));       // COMISS 0, p0; JB
                else M.U32(p.A + 0x468, 0x42200000);
            }
            else M.U32(p.A + 0x468, 0x41f00000);                                           // 30
            if (kind == 4 || kind == 5 || kind == 0xe)
            {
                M.I16(p.A + 0x3f8, 0);
                M.I16(p.A + 0x3ea, 0xfa);
                M.U32(p.A + 0x3fc, 0xbf000000);                                             // -0.5
            }
            else if (kind == 0 || kind == 2)
            {
                M.I16(p.A + 0x3f8, 0);
                M.I16(p.A + 0x3ea, 200);
                M.U32(p.A + 0x3fc, 0xbf000000);
            }
            else
            {
                M.I16(p.A + 0x3f8, 0x166);
                M.I16(p.A + 0x3ea, 0x32);
                M.U32(p.A + 0x3fc, 0xbecccccd);                                             // -0.4
            }
            M.F32(p.A + 0x40c, (float)kind);
        }

        /// <summary>
        /// CRagEffect::fn_b81f80 (0xb81f80) — position = the owner's (every pass); tick 0: one prim 0x1b (RP flags 5, effect life,
        /// texture 0 = <paramref name="texture"/>, +0x318 = 0). Four live slots: speed +0x3f0 0.15, size +0x468 1, kind +0x450 1,
        /// colour +0x451 = 2 (<paramref name="mode"/> 2) else 0; rand() % 360 for the two wander pairs (+0x3fc..+0x408), then per
        /// glint (0 / 1 / 2 / 3) x = z = 0, y = −2·<paramref name="height"/>, one orbit angle (+0x40c / +0x410 / +0x414 / +0x418).
        /// </summary>
        public void Fn_b81f80(FxRuntime rt, string texture, sbyte height, sbyte mode)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x1b, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            p.f_318 = 0f;
            float y = -(float)(height * 2);
            for (int k = 0; k < 4; k++)
            {
                uint s = p.A + (uint)k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.U32(s + 0x3f0, 0x3e19999a);                                               // 0.15
                M.U32(s + 0x468, 0x3f800000);                                               // 1
                for (uint j = 0; j < 0x10; j += 4) M.F32(s + 0x3fc + j, (float)RandMod(rt, 0x168));
                Glint_b81f80(rt, s, 0x484, 0x488, 0x48c, 0x40c, y);
                Glint_b81f80(rt, s, 0x46c, 0x470, 0x474, 0x410, y);
                Glint_b81f80(rt, s, 0x490, 0x494, 0x498, 0x414, y);
                Glint_b81f80(rt, s, 0x478, 0x47c, 0x480, 0x418, y);
                M.U8(s + 0x450, 1);
                M.U8(s + 0x451, (byte)(mode == 2 ? 2 : 0));
            }
        }

        void Glint_b81f80(FxRuntime rt, uint s, uint xo, uint yo, uint zo, uint angle, float y)
        {
            M.U32(s + xo, 0);
            M.F32(s + yo, y);
            M.U32(s + zo, 0);
            M.F32(s + angle, (float)RandMod(rt, 0x168));
        }

        /// <summary>
        /// CRagEffect::fn_b82320 (0xb82320) — position = the owner's (every pass); tick 0: one prim 0x1c (RP flags 1 | 5, effect life,
        /// texture 0 = <paramref name="texture"/>, +0x318 = 4): slots 0 and 1 live, radius 46, phase +0x466 = 0 / 180, alpha 25, height
        /// −1; slots 2 / 3 off.
        /// </summary>
        public void Fn_b82320(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x1c, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 0;
            p.f_1c4 |= 1;
            p.f_1c4 |= 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            M.U32(p.A + 0x318, 0x40800000);                                                 // 4
            M.U8(p.A + 0x3e8, 1);
            M.U8(p.A + 0x49c, 1);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            M.U32(p.A + 0x468, 0x42380000);                                                 // 46
            M.U32(p.A + 0x51c, 0x42380000);
            M.I16(p.A + 0x466, 0);
            M.I16(p.A + 0x51a, 0xb4);
            M.I16(p.A + 0x3ea, 0x19);
            M.I16(p.A + 0x49e, 0x19);
            M.U32(p.A + 0x3fc, 0xbf800000);                                                 // -1
            M.U32(p.A + 0x4b0, 0xbf800000);
        }

        /// <summary>
        /// CRagEffect::fn_b817a0 (0xb817a0) — position = the owner's (every pass); tick 0: one prim 0x17 (RP flags 1 | 5, effect life,
        /// texture 0 = <paramref name="texture"/>, +0x318 = 0): slots 0..2 live, counter 0, +0x3ee 315, +0x3f0 15 / 13 / 11, angle
        /// +0x3f8 0 / 90 / 180, alpha 50, +0x3ec 1, radius +0x468 12.9 / 13.1 / 13.3, +0x466 55 / 50 / 45, +0x470 0, 21 floats from
        /// +0x3fc and 21 bytes from +0x450 = 0; slot 3 off.
        /// </summary>
        public void Fn_b817a0(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x17, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 0;
            p.f_1c4 |= 1;
            p.f_1c4 |= 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            p.f_318 = 0f;
            HaloSlot_b817a0(p.A, 0x41700000, 0, 0x414e6666, 0x37);                          // 15, 0, 12.9, 55
            HaloSlot_b817a0(p.A + 0xb4, 0x41500000, 0x5a, 0x4151999a, 0x32);              // 13, 90, 13.1, 50
            HaloSlot_b817a0(p.A + 0x168, 0x41300000, 0xb4, 0x4154cccd, 0x2d);              // 11, 180, 13.3, 45
            M.U8(p.A + 0x604, 0);
        }

        void HaloSlot_b817a0(uint s, uint speedBits, short angle, uint radiusBits, short phase)
        {
            M.U8(s + 0x3e8, 1);
            M.I32(s + 0x3f4, 0);
            M.I16(s + 0x3ee, 0x13b);
            M.U32(s + 0x3f0, speedBits);
            M.I16(s + 0x3f8, angle);
            M.I16(s + 0x3ea, 0x32);
            M.I16(s + 0x3ec, 1);
            M.U32(s + 0x468, radiusBits);
            M.I16(s + 0x466, phase);
            M.U32(s + 0x470, 0);
            for (uint j = 0; j < 21; j++)
            {
                M.U32(s + 0x3fc + j * 4, 0);
                M.U8(s + 0x450 + j, 0);
            }
        }
    }
}
