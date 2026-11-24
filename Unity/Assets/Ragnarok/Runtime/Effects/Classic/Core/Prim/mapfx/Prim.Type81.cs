// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type81.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x81 — the upward light cone of EF 703 (launched by CRagEffect::fn_b6e560). Four slots of 0xb4 from +0x3e8, each a
    /// ten-sided frustum between a wide bottom ring (centre +0x478 / +0x47c / +0x480, radius 4 · +0x3f0) and a top ring (centre
    /// +0x490 / +0x494 / +0x498, radius +0x3f0; base +0x3fc), angle +0x3f8, alpha +0x3ea, counter +0x3f4, kind +0x450.
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x81 (inline, 0xad883c) — no follow, no camera. Per slot with (signed) +0x3e8 &gt; 0: counter
        /// +0x3f4 +1, angle +0x3f8 −360 once ≥ 360 (16-bit; it never moves here); kind 11: alpha +1 while the counter ≤ 40, −1 after
        /// 140, clamped at 0; radius +0x3f0 = base +0x3fc + 0.1 · sin[counter % 360] (C remainder: a negative counter reads before
        /// the table). <c>break;</c>
        /// </summary>
        [FxPrimProcess(0x81)]
        public FxCaseResult Case81(FxRuntime rt)
        {
            for (int k = 0; k < 4; k++)
            {
                uint s = A + (uint)k * 0xb4;
                if ((sbyte)M.U8(s + 0x3e8) <= 0) continue;
                int cnt = M.I32(s + 0x3f4) + 1;
                M.I32(s + 0x3f4, cnt);
                short ang = M.I16(s + 0x3f8);
                if (ang >= 0x168) M.I16(s + 0x3f8, (short)(ang - 0x168));
                if (M.U8(s + 0x450) == 0xb)
                {
                    if (cnt <= 0x28) M.I16(s + 0x3ea, (short)(M.I16(s + 0x3ea) + 1));
                    else if (cnt > 0x8c) M.I16(s + 0x3ea, (short)(M.I16(s + 0x3ea) - 1));
                    if (M.I16(s + 0x3ea) <= 0) M.I16(s + 0x3ea, 0);
                }
                float b = M.F32(s + 0x3fc);
                M.F32(s + 0x3f0, b);
                M.F32(s + 0x3f0, SinAt(cnt % 0x168) * 0.1f + b);                         // [0xf7cff8]
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CEffectPrim::Render case 0x81 (inline, 0xaf5d27) — per slot with +0x3e8 == 1 and alpha +0x3ea &gt; 0: eleven ring points at
        /// +0x3f8 + 36·i (−360 once ≥ 360, 16-bit), bottom radius 4·r around (+0x478, +0x47c, +0x480), top radius r = +0x3f0 around
        /// (+0x490, +0x494, +0x498); the ten side quads between consecutive points (fn_b0a900: A = previous bottom, B = bottom,
        /// C = top, D = previous top; absolute coordinates, texture 0, blend 0, colour (230, 255, 230), alpha +0x3ea, uv 0..1,
        /// mode 0). One face counter for the whole prim; the "previous" points carry over between slots (only read from i = 1).
        /// </summary>
        [FxPrimRender(0x81)]
        public void Render81(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            float ax = 0f, ay = 0f, az = 0f, dx = 0f, dy = 0f, dz = 0f;
            for (int k = 0; k < 4; k++)
            {
                uint s = A + (uint)k * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                short alpha = M.I16(s + 0x3ea);
                if (alpha <= 0) continue;
                float r = M.F32(s + 0x3f0);
                float r4 = r * 4f;                                                      // [0xf4a764]
                for (int i = 0; i <= 0xa; i++)
                {
                    float by = M.F32(s + 0x47c);
                    short a = (short)((ushort)(i * 0x24) + M.U16(s + 0x3f8));
                    if (a >= 0x168) a = (short)(a - 0x168);
                    float c = CosAt(a), sn = SinAt(a);
                    float bx = c * r4 + M.F32(s + 0x478);
                    float cx = c * r + M.F32(s + 0x490);
                    float bz = r4 * sn + M.F32(s + 0x480);
                    float cy = M.F32(s + 0x494);
                    float cz = r * sn + M.F32(s + 0x498);
                    if (i != 0)
                        Quad_b0a900(rt, ref count, alpha, ax, ay, az, bx, by, bz, cx, cy, cz, dx, dy, dz,
                            viewMatrix, 0, 0, 1, 0xe6, 0xff, 0xe6, 0f, 1f, 0f, 1f, 0);
                    ax = bx; ay = by; az = bz;
                    dx = cx; dy = cy; dz = cz;
                }
            }
        }
    }
}
