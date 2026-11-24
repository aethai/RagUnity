// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type4F.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x4f — the map waterfalls (CRagEffect::fn_b6a4f0 / fn_b90c90). Four slots of 0xb4 bytes from +0x3e8: live +0x3e8,
    /// alpha +0x3ea, counter +0x3f4, speed +0x3f0 (ticks per band), 20 band heights +0x3fc.., orientation +0x450 (1 = in the
    /// x = depth plane), mode +0x451 (1 / 10 = flow reversed), band stretch +0x452 (above 10: × 10 / value), width +0x468, depth +0x474.
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>CEffectPrim::OnProcess case 0x4f (0xad8938, inline): each slot with (char)+0x3e8 &gt; 0 counts +0x3f4 up; break.</summary>
        [FxPrimProcess(0x4f)]
        public FxCaseResult Case4F(FxRuntime rt)
        {
            for (uint o = 0; o < 0x2d0; o += 0xb4)
                if ((sbyte)M.U8(A + o + 0x3e8) > 0) M.I32(A + o + 0x3f4, M.I32(A + o + 0x3f4) + 1);
            return FxCaseResult.Continue;
        }

        /// <summary>CEffectPrim::Render case 0x4f: fn_b0c760(this, viewMatrix).</summary>
        [FxPrimRender(0x4f)]
        public void Render4F(FxRuntime rt, uint viewMatrix) => Render_b0c760(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b0c760 (0xb0c760) — per slot with +0x3e8 == 1 and alpha (short)+0x3ea &gt; 0: n = (int)+0x3f0 (0 → one quad,
        /// t = 40), else r = counter % 3n, t = (counter % n)·40 / n; mode 1 / 10 → t = 40 − t. Five stacked quads i = 0..4 (only i = 0
        /// when n is 0 / 1): corners (∓w/2, t − band[i+1] / t − band[i], depth) — or (depth, …, ∓w/2) with orientation 1 — y stretched by
        /// 10 / +0x452 when that byte is above 10; the top quad (i = 0) is cut at t·0.025 of its height (v from t·0.025), the bottom one
        /// (i = 4) ends there (v to t·0.025). Texture (i % 3 ∓ r / n) wrapped into 0..2 (scrolls the three frames); colour (55, 55, 255)
        /// blend 0 (mode 1), (255, 55, 255) blend 0 (mode 10), else white blend 1; fn_b0a900 relative to the prim, mode 0.
        /// </summary>
        public void Render_b0c760(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1 || M.I16(s + 0x3ea) <= 0) continue;
                int n = FxOps.Cvtt(M.F32(s + 0x3f0));
                int rem;
                float t;
                if (n == 0)
                {
                    rem = 0;
                    t = 40f;                                                                // [0xf7d138]
                    n = 1;
                }
                else
                {
                    int cnt = M.I32(s + 0x3f4);
                    rem = cnt % (n * 3);
                    t = (float)(cnt % n) * 40f / (float)n;
                }
                byte mode = M.U8(s + 0x451);
                if (mode == 1 || mode == 10) t = 40f - t;
                for (int i = 0; i < 5; i++)
                {
                    if (n == 1 && i > 0) break;
                    float w = M.F32(s + 0x468), h = M.F32(s + 0x474);
                    float nw = -w * 0.5f, pw = w * 0.5f;                                    // [0xf4a4a0] [0xf4a460]
                    float y1 = t - M.F32(s + 0x3fc + (uint)i * 4);
                    float y2 = t - M.F32(s + 0x400 + (uint)i * 4);
                    sbyte stretch = (sbyte)M.U8(s + 0x452);
                    if (stretch > 10)
                    {
                        float d = (float)stretch;
                        y1 = y1 * 10f / d;                                                  // [0xf4b624]
                        y2 = y2 * 10f / d;
                    }
                    float ax = nw, az = h, bx = pw, bz = h, cx = pw, cz = h, dx = nw, dz = h;
                    if (M.U8(s + 0x450) == 1)
                    {
                        ax = h; az = nw; bx = h; bz = pw; cx = h; cz = pw; dx = h; dz = nw;
                    }
                    mode = M.U8(s + 0x451);
                    int q = rem / n;
                    int tex;
                    if (mode == 1 || mode == 10)
                    {
                        tex = i % 3 - q;
                        if (tex < 0) tex = (int)((uint)tex + ((uint)~tex / 3u) * 3u + 3u);
                    }
                    else
                    {
                        tex = i % 3 + q;
                        if (tex > 2) tex -= 3;
                    }
                    float ay = y2, by = y2, cy = y1, dy = y1;
                    float v0 = 0f, v1 = 1f;
                    if (i == 0)
                    {
                        float k = t * 0.025f;                                               // [0xff74e8]
                        v0 = k;
                        float c = y1 - (y1 - y2) * k;
                        cy = c; dy = c;
                    }
                    else if (i == 4)
                    {
                        float k = t * 0.025f;
                        v1 = k;
                        float c = (y1 - y2) * (1f - k) + y2;
                        ay = c; by = c;
                    }
                    byte r, g;
                    short blend;
                    if (mode == 1) { r = 0x37; g = 0x37; blend = 0; }
                    else if (mode == 10) { r = 0xff; g = 0x37; blend = 0; }
                    else { r = 0xff; g = 0xff; blend = 1; }
                    Quad_b0a900(rt, ref count, M.I16(s + 0x3ea), ax, ay, az, bx, by, bz, cx, cy, cz, dx, dy, dz,
                        viewMatrix, blend, (sbyte)tex, 0, r, g, 0xff, v0, v1, 0f, 1f, 0);
                    if (i == 4) break;
                }
            }
        }
    }
}
