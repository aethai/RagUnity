// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type56_5C.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// FUN_00ad5710 (stdcall, RET 0xc) — integer heading (0..360) of the XZ vector (x, z): the first
        /// i in 0..180 with x/len ≥ cos(i), mirrored to 360 − i when z/len &lt; 0 (or unordered); 180 when none.
        /// </summary>
        public static short Heading_ad5710(float x, float z)
        {
            float len = (float)Math.Sqrt((double)(x * x + z * z));
            float nz = z / len;
            float nx = x / len;
            for (short i = 0; i <= 0xb4; i++)
            {
                if (nx >= FxMath.CosTableAt(i))
                    return !(nz >= 0.0f) ? (short)(0x168 - i) : i;
            }
            return 0xb4;
        }

        /// <summary>
        /// CEffectPrim::OnProcess case 0x56 (0xad91fb, inline) — Sonic Wave / purple slashes (fn_b8adf0):
        /// per active slot, delayed start (counter slot +0xc); on the first tick the heading towards the target
        /// (f_388/f_390 − position) plus a random spread (±5 for kind slot +0x69 == 1, else ±15); then alpha +10 /
        /// speed ×1.02 for 10 ticks, then alpha −5 / speed ×1.04 until faded (slot off); moves x/z (slot +0x84/+0x8c).
        /// </summary>
        [FxPrimProcess(0x56)]
        public FxCaseResult Case56(FxRuntime rt)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                int old = M.I32(s + 0xc);
                M.I32(s + 0xc, old + 1);
                if (old + 1 <= 0) continue;
                if (old == 0)
                {
                    M.I16(s + 0x10, Heading_ad5710(f_388 - m_pos_x, f_390 - m_pos_z));
                    short d;
                    if (M.U8(s + 0x69) == 1)
                    {
                        int r = rt.Rand.Next() % 0xb;
                        d = (short)((r < 0 ? 0 : r) - 5);
                    }
                    else
                    {
                        int r = rt.Rand.Next() % 0x1f;
                        d = (short)((r < 0 ? 0 : r) - 0xf);
                    }
                    short ang = (short)(d + M.I16(s + 0x10));
                    M.I16(s + 0x10, ang);
                    if (ang >= 0x168) M.I16(s + 0x10, (short)(ang - 0x168));
                    if (M.I16(s + 0x10) < 0) M.I16(s + 0x10, (short)(M.I16(s + 0x10) + 0x168));
                }
                else if (old < 10)
                {
                    M.I16(s + 2, (short)(M.I16(s + 2) + 10));
                    M.F32(s + 0x18, M.F32(s + 0x18) * 1.02f);
                }
                else
                {
                    M.I16(s + 2, (short)(M.I16(s + 2) - 5));
                    M.F32(s + 0x18, M.F32(s + 0x18) * 1.04f);
                    if (M.I16(s + 2) < 0)
                    {
                        M.I16(s + 2, 0);
                        M.U8(s, 0);
                    }
                }
                MoveSlot_56(s);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>x (slot +0x84) += speed · cos(heading), z (slot +0x8c) += speed · sin(heading).</summary>
        void MoveSlot_56(uint s)
        {
            int ang = M.I16(s + 0x10);
            float speed = M.F32(s + 0x18);
            M.F32(s + 0x84, speed * FxMath.CosTableAt(ang) + M.F32(s + 0x84));
            M.F32(s + 0x8c, speed * FxMath.SinTableAt(ang) + M.F32(s + 0x8c));
        }

        /// <summary>
        /// CEffectPrim::OnProcess case 0x5c (0xad93af, inline) — like 0x56 with a preset heading (fn_b8abe0):
        /// no heading pass on the first tick, same fade / acceleration / movement.
        /// </summary>
        [FxPrimProcess(0x5c)]
        public FxCaseResult Case5C(FxRuntime rt)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                int old = M.I32(s + 0xc);
                M.I32(s + 0xc, old + 1);
                if (old + 1 <= 0) continue;
                if (old != 0)
                {
                    if (old < 10)
                    {
                        M.I16(s + 2, (short)(M.I16(s + 2) + 10));
                        M.F32(s + 0x18, M.F32(s + 0x18) * 1.02f);
                    }
                    else
                    {
                        M.I16(s + 2, (short)(M.I16(s + 2) - 5));
                        M.F32(s + 0x18, M.F32(s + 0x18) * 1.04f);
                        if (M.I16(s + 2) < 0)
                        {
                            M.I16(s + 2, 0);
                            M.U8(s, 0);
                        }
                    }
                }
                MoveSlot_56(s);
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x56, 0x5c)]
        public void Render56(FxRuntime rt, uint viewMatrix) => Render_b08190(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b08190 (0xb08190) — Render for prim types 0x56 / 0x5c: per active slot with alpha &gt; 0,
        /// a flat square (half-diagonal slot +0x80) at the slot position, corners at heading + 45 + k·90,
        /// absolute coordinates, blend 1; kind (slot +0x69) 1 = animated texture (counter / 6 mod 4) blue-white,
        /// 2 = green, else light blue.
        /// </summary>
        public void Render_b08190(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                short alpha = M.I16(s + 2);
                if (alpha <= 0) continue;
                float size = M.F32(s + 0x80);
                float x = M.F32(s + 0x84);
                float y = M.F32(s + 0x88) + 0.0f;
                float z = M.F32(s + 0x8c);
                int i0 = M.I16(s + 0x10) + 0x2d;
                if (i0 >= 0x168) i0 -= 0x168;
                int i1 = i0 + (i0 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int i2 = i1 + (i1 + 0x5a < 0x168 ? 0x5a : -0x10e);
                int i3 = i2 + (i2 + 0x5a < 0x168 ? 0x5a : -0x10e);
                byte kind = M.U8(s + 0x69);
                byte r, g, b;
                sbyte tex = 0;
                if (kind == 1)
                {
                    int cnt = M.I32(s + 0xc);
                    tex = cnt < 1 ? (sbyte)0 : (sbyte)((cnt / 6) % 4);
                    r = 0xcd; g = 0xcd; b = 0xff;
                }
                else if (kind == 2) { r = 0; g = 0xff; b = 0; }
                else { r = 0xc8; g = 0xc8; b = 0xff; }
                Quad_b0a900(rt, ref count, alpha,
                    size * FxMath.CosTableAt(i0) + x, y, size * FxMath.SinTableAt(i0) + z,
                    size * FxMath.CosTableAt(i1) + x, y, size * FxMath.SinTableAt(i1) + z,
                    size * FxMath.CosTableAt(i2) + x, y, size * FxMath.SinTableAt(i2) + z,
                    size * FxMath.CosTableAt(i3) + x, y, size * FxMath.SinTableAt(i3) + z,
                    viewMatrix, 1, tex, 1, r, g, b, 0f, 1.0f, 0f, 1.0f, 0);
            }
        }
    }
}
