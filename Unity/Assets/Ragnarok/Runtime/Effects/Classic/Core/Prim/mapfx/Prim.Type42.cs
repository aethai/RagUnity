// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type42.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x42 — the slanted light shafts of EF 307 / 322 / 323 / 324 (launched by CRagEffect::fn_b77010). Four slots
    /// of 0xb4 from +0x3e8, each a five-sided tube between a bottom ring (centre +0x478 / +0x47c / +0x480) and a top ring
    /// (centre +0x490 / +0x494 / +0x498) of radius +0x3f0 (base +0x3fc), angle +0x3f8, alpha +0x3ea, counter +0x3f4, kind +0x450.
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x42 (inline, 0xad86f8) — no follow, no camera. Per slot with (signed) +0x3e8 &gt; 0:
        /// counter +0x3f4 +1, angle +0x3f8 −360 once ≥ 360 (it never moves here); kind 13: alpha +1 while the counter ≤ 40;
        /// kinds ≥ 11: alpha +1 while ≤ 40, −1 after 140; both clamp the alpha at 0. Slots 1 and 3 breathe: radius +0x3f0 =
        /// base +0x3fc + 0.5 · sin[(counter % 720) / 2]. <c>break;</c>
        /// </summary>
        [FxPrimProcess(0x42)]
        public FxCaseResult Case42(FxRuntime rt)
        {
            for (int k = 0; k < 4; k++)
            {
                uint s = A + (uint)k * 0xb4;
                if ((sbyte)M.U8(s + 0x3e8) <= 0) continue;
                int cnt = M.I32(s + 0x3f4) + 1;
                M.I32(s + 0x3f4, cnt);
                short ang = M.I16(s + 0x3f8);
                if (ang >= 0x168) M.I16(s + 0x3f8, (short)(ang - 0x168));
                sbyte kind = (sbyte)M.U8(s + 0x450);
                if (kind == 0xd)
                {
                    if (M.I32(s + 0x3f4) <= 0x28) M.I16(s + 0x3ea, (short)(M.I16(s + 0x3ea) + 1));
                    if (M.I16(s + 0x3ea) <= 0) M.I16(s + 0x3ea, 0);
                }
                else if (kind >= 0xb)
                {
                    if (cnt <= 0x28) M.I16(s + 0x3ea, (short)(M.I16(s + 0x3ea) + 1));
                    else if (cnt > 0x8c) M.I16(s + 0x3ea, (short)(M.I16(s + 0x3ea) - 1));
                    if (M.I16(s + 0x3ea) <= 0) M.I16(s + 0x3ea, 0);
                }
                if (k == 1 || k == 3)
                {
                    float b = M.F32(s + 0x3fc);
                    M.F32(s + 0x3f0, b);
                    M.F32(s + 0x3f0, SinAt((cnt % 0x2d0) / 2) * 0.5f + b);                // [0xf4a460]
                }
            }
            return FxCaseResult.Continue;
        }

        /// <summary>CEffectPrim::Render case 0x42: fn_afed80(this, viewMatrix).</summary>
        [FxPrimRender(0x42)]
        public void Render42(FxRuntime rt, uint viewMatrix) => Render_afed80(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_afed80 (0xafed80) — per slot with +0x3e8 == 1 and alpha +0x3ea &gt; 0: six ring points at angles
        /// +0x3f8 + 72·i (−360 once ≥ 360), radius +0x3f0, around the bottom centre (+0x478.., y fixed) and the top centre
        /// (+0x490..); the five side quads between consecutive points (fn_b0a900: A = previous bottom, B = bottom,
        /// C = top, D = previous top; absolute coordinates, texture 0, blend 0, uv 0..1, mode 0), colour (80, 80, 255)
        /// for kind 13, else (230, 255, 230), alpha = +0x3ea. One face counter for the whole prim.
        /// </summary>
        public void Render_afed80(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            float ax = 0f, ay = 0f, az = 0f, dx = 0f, dy = 0f, dz = 0f;
            for (int k = 0; k < 4; k++)
            {
                uint s = A + (uint)k * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                short alpha = M.I16(s + 0x3ea);
                if (alpha <= 0) continue;
                float topY = M.F32(s + 0x494), topX = M.F32(s + 0x490), topZ = M.F32(s + 0x498);
                float r = M.F32(s + 0x3f0);
                for (int i = 0; i < 6; i++)
                {
                    float by = M.F32(s + 0x47c);
                    short a = (short)((ushort)(i * 0x48) + M.U16(s + 0x3f8));
                    if (a >= 0x168) a = (short)(a - 0x168);
                    float c = r * CosAt(a);
                    float sn = r * SinAt(a);
                    float bx = c + M.F32(s + 0x478);
                    float bz = sn + M.F32(s + 0x480);
                    float cx = topX + c;
                    float cz = topZ + sn;
                    if (i != 0)
                    {
                        bool blue = M.U8(s + 0x450) == 0xd;
                        Quad_b0a900(rt, ref count, alpha, ax, ay, az, bx, by, bz, cx, topY, cz, dx, dy, dz,
                            viewMatrix, 0, 0, 1, blue ? (byte)0x50 : (byte)0xe6, blue ? (byte)0x50 : (byte)0xff,
                            blue ? (byte)0xff : (byte)0xe6, 0f, 1f, 0f, 1f, 0);
                    }
                    ax = bx; ay = by; az = bz;
                    dx = cx; dy = topY; dz = cz;
                }
            }
        }
    }
}
