// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type59.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x59 (0xada1ba, inline) — expanding ring fans (fn_b66d40): follows the
        /// master; per active slot with started counter: kind (slot +0x24) 2/6: alpha +10 (≤ 10 ticks) / −3
        /// (after 11), size +3; kind 3: +6 / −1, size +3; other: +20 / −15 (after 12), size ×1.15.
        /// </summary>
        [FxPrimProcess(0x59)]
        public FxCaseResult Case59(FxRuntime rt)
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                int cnt = M.I32(s + 0xc) + 1;
                M.I32(s + 0xc, cnt);
                if (cnt <= 0) continue;
                float kind = M.F32(s + 0x24);
                float size;
                if (kind == 2.0f || kind == 6.0f || kind == 3.0f)
                {
                    bool k3 = !(kind == 2.0f || kind == 6.0f);
                    if (cnt <= 10) M.I16(s + 2, (short)(M.I16(s + 2) + (k3 ? 6 : 10)));
                    else if (cnt > 11)
                    {
                        short al = (short)(M.I16(s + 2) + (k3 ? -1 : -3));
                        M.I16(s + 2, al);
                        if (al < 0) M.I16(s + 2, 0);
                    }
                    size = M.F32(s + 0x80) + 3.0f;
                }
                else
                {
                    if (cnt <= 10) M.I16(s + 2, (short)(M.I16(s + 2) + 0x14));
                    else if (cnt > 12)
                    {
                        short al = (short)(M.I16(s + 2) - 0xf);
                        M.I16(s + 2, al);
                        if (al < 0) M.I16(s + 2, 0);
                    }
                    size = M.F32(s + 0x80) * 1.15f;
                }
                M.F32(s + 0x80, size);
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x59)]
        public void Render59(FxRuntime rt, uint viewMatrix) => Render_af7650(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_af7650 (0xaf7650) — Render for prim type 0x59: per active slot with alpha &gt; 0, two
        /// thin fans from the slot origin (0, y, 0): edge points at base angle (slot +0x7e) ∓ 2 / ± 2 (∓1 / ±1 for
        /// kind 2) and ∓5 / ±5 (∓2 / ±2), radius slot +0x80, tilted by slot +0x10; colours / blend / mode by kind.
        /// </summary>
        public void Render_af7650(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                if (M.I16(s + 2) <= 0) continue;
                for (int q = 0; q < 2; q++)
                {
                    float kind = M.F32(s + 0x24);
                    int bas = M.I16(s + 0x7e);
                    float size = M.F32(s + 0x80);
                    float y0 = M.F32(s + 0x14);
                    bool k2 = kind == 2.0f;
                    int a1 = bas + (q == 0 ? (k2 ? -1 : -2) : (k2 ? -2 : -5));
                    if (a1 < 0) a1 += 0x168;
                    int e = M.I16(s + 0x10);
                    float ce = FxMath.CosTableAt(e), se = FxMath.SinTableAt(e);
                    float p0z = size * FxMath.SinTableAt(a1);
                    float r1 = size * FxMath.CosTableAt(a1);
                    float p0x = ce * r1;
                    float p0y = se * r1 + y0;
                    int a2 = (q == 0 ? (k2 ? 1 : 2) : (k2 ? 2 : 5)) + bas;
                    if (a2 >= 0x168) a2 -= 0x168;
                    float r2 = size * FxMath.CosTableAt(a2);
                    float p1z = size * FxMath.SinTableAt(a2);
                    float p1x = r2 * ce;
                    float p1y = r2 * se + y0;

                    byte r, g, b, mode = 0, blend = 0;
                    if (q == 0)
                    {
                        if (kind == 2.0f || kind == 4.0f) { r = 0; g = 0; b = 0xfa; }
                        else if (kind == 3.0f) { r = 0xfa; g = 0xfa; b = 0xfa; }
                        else if (kind == 5.0f) { r = 0x32; g = 0x32; b = 0x32; }
                        else if (kind == 6.0f) { r = 0; g = 0; b = 0xff; mode = 1; }
                        else { r = 0; g = 0xfa; b = 0xfa; }
                    }
                    else
                    {
                        if (kind == 2.0f) { r = 0xfa; g = 0xfa; b = 0; }
                        else if (kind == 3.0f || kind == 5.0f) { r = 0xfa; g = 0xfa; b = 0xfa; blend = 1; }
                        else if (kind == 4.0f) { r = 0xfa; g = 0xfa; b = 0; blend = 1; }
                        else if (kind == 6.0f) { r = 0x32; g = 0x32; b = 0xff; mode = 1; }
                        else { r = 0xfa; g = 0; b = 0; }
                    }
                    Quad_b0a900(rt, ref count, M.I16(s + 2),
                        p0x, p0y, p0z, p1x, p1y, p1z, 0f, y0, 0f, 0f, y0, 0f,
                        viewMatrix, blend, 0, 0, r, g, b, 0f, 1.0f, 0f, 1.0f, mode);
                }
            }
        }
    }
}
