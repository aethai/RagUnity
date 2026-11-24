// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type67.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x67 (0xad6c18, inline) — spinning pillar rings (fn_b8bd10): per active slot
        /// with started counter: spin (slot +0x10) += 3 + rand()%3; alpha +1 for 40 ticks, −1 after 120 (mode
        /// slot +0x6d == 1) / after 60 (other modes except 10); clamped at 0.
        /// </summary>
        [FxPrimProcess(0x67)]
        public FxCaseResult Case67(FxRuntime rt)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                M.I32(s + 0xc, M.I32(s + 0xc) + 1);
                if (M.I32(s + 0xc) <= 0) continue;
                int r = rt.Rand.Next() % 3;
                short ang = (short)((short)((r < 0 ? 0 : r) + 3) + M.I16(s + 0x10));
                M.I16(s + 0x10, ang);
                if (ang >= 0x168) M.I16(s + 0x10, (short)(ang - 0x168));
                byte mode = M.U8(s + 0x6d);
                bool dec;
                if (mode == 1) dec = M.I32(s + 0xc) > 0x78;
                else dec = mode != 10 && M.I32(s + 0xc) > 0x3c;
                if (dec) M.I16(s + 2, (short)(M.I16(s + 2) - 1));
                if (M.I32(s + 0xc) <= 0x28) M.I16(s + 2, (short)(M.I16(s + 2) + 1));
                if (M.I16(s + 2) < 0) M.I16(s + 2, 0);
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x67)]
        public void Render67(FxRuntime rt, uint viewMatrix) => Render_b094c0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b094c0 (0xb094c0) — Render for prim type 0x67: per active slot with alpha &gt; 0, a stack of
        /// slot +0x68 rings: ring i is a square (radius slot +8 shrinking by (2.2 − 0.2·i)·slot +0x80 per ring,
        /// height 1 from y = −slot +0x14 rising by slot +0x18) at angle spin + 45·i; colour / blend by slot +0x6c
        /// (9 and out-of-range: not drawn; 8: alpha/10; 11: extra white pass with alpha/3).
        /// </summary>
        public void Render_b094c0(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                if (M.I16(s + 2) <= 0) continue;
                float y1 = -M.F32(s + 0x14);
                float rad = M.F32(s + 8);
                for (int i = 0; i < (sbyte)M.U8(s + 0x68); i++)
                {
                    if (0 < i)
                    {
                        y1 = y1 + M.F32(s + 0x18);
                        rad = rad - (2.2f - (float)i * 0.2f) * M.F32(s + 0x80);
                    }
                    short a = (short)(i * 0x2d + M.I16(s + 0x10));
                    if (a >= 0x168) a = (short)((ushort)a % 0x168);
                    short b = (short)(a + 0x5a); if (b >= 0x168) b = (short)(a + 0x5a - 0x168);
                    short c = (short)(b + 0x5a); if (c >= 0x168) c = (short)(b + 0x5a - 0x168);
                    short d = (short)(c + 0x5a); if (d >= 0x168) d = (short)(c + 0x5a - 0x168);
                    float y0 = y1 - 1.0f;
                    float x0 = rad * FxMath.CosTableAt(a), z0 = rad * FxMath.SinTableAt(a);
                    float x1 = rad * FxMath.CosTableAt(b), z1 = rad * FxMath.SinTableAt(b);
                    float x2 = rad * FxMath.CosTableAt(c), z2 = rad * FxMath.SinTableAt(c);
                    float x3 = rad * FxMath.CosTableAt(d), z3 = rad * FxMath.SinTableAt(d);

                    sbyte kind = (sbyte)M.U8(s + 0x6c);
                    short alpha = M.I16(s + 2);
                    byte r, g, bl, blend = 1;
                    switch ((uint)kind > 0xb ? -1 : kind)
                    {
                        case 0: r = 0x32; g = 0x32; bl = 0xff; blend = 0; break;
                        case 1: r = 0xa4; g = 0x88; bl = 0x37; break;
                        case 2: r = 0x9b; g = 0xff; bl = 0x9b; break;
                        case 3: r = 0x82; g = 0x82; bl = 0xff; break;
                        case 4: r = 0xff; g = 0x32; bl = 0x32; break;
                        case 5: r = 5; g = 5; bl = 5; break;
                        case 6: r = 0xff; g = 0xff; bl = 0xff; break;
                        case 7: r = 0xff; g = 0x7d; bl = 0xff; break;
                        case 8: r = 0xe1; g = 0xe1; bl = 0xff; alpha = (short)(alpha / 10); break;
                        case 10: r = 0x7d; g = 0x7d; bl = 0xff; blend = 0; break;
                        case 11:
                            Quad_b0a900(rt, ref count, (short)(alpha / 3),
                                x0, y0, z0, x1, y0, z1, x2, y1, z2, x3, y1, z3,
                                viewMatrix, 1, 0, 0, 0xff, 0xff, 0xff, 0f, 1.0f, 0f, 1.0f, 0);
                            r = 0xff; g = 0x37; bl = 0x37; blend = 0;
                            break;
                        default: continue;
                    }
                    Quad_b0a900(rt, ref count, alpha,
                        x0, y0, z0, x1, y0, z1, x2, y1, z2, x3, y1, z3,
                        viewMatrix, blend, 0, 0, r, g, bl, 0f, 1.0f, 0f, 1.0f, 0);
                }
            }
        }
    }
}
