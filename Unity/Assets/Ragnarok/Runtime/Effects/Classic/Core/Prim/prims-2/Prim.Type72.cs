// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type72.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x72 (0xad9f24, inline) — floating cube (fn_b6a920): follows the master;
        /// alpha f_3ea +2 up to 0x8c; pulse angle f_466 and spin f_3f8 +1 (wrapped at 360).
        /// </summary>
        [FxPrimProcess(0x72)]
        public FxCaseResult Case72(FxRuntime rt)
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            if (f_3ea < 0x8c) f_3ea = (short)(f_3ea + 2);
            short p = (short)(f_466 + 1);
            f_466 = p;
            if (p >= 0x168) f_466 = (short)(p - 0x168);
            short a = (short)(f_3f8 + 1);
            f_3f8 = a;
            if (a >= 0x168) f_3f8 = (short)(a - 0x168);
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x72)]
        public void Render72(FxRuntime rt, uint viewMatrix) => Render_b01680(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b01680 (0xb01680) — Render for prim type 0x72: per active slot a cube of height
        /// slot +0x80 whose top square (corners at spin slot +0x10 + k·90, pulsing radius
        /// (sin(p)+1)·size·0.1 + size·0.8 in double precision) bobs at 2·sin(spin) + slot +0x14; the top corners
        /// are stored at slot +0x84..+0xb0; six faces via fn_b0a900, grey level slot +0x68, blue 0xfa..0x82.
        /// </summary>
        public void Render_b01680(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                int pa = M.I16(s + 0x7e);
                float size = M.F32(s + 0x80);
                int a = M.I16(s + 0x10);
                int b = a + (a < 0x10e ? 0x5a : -0x10e);
                float pulse = (FxMath.SinTableAt(pa) + 1.0f) * (size * 0.1f);
                double rad = (double)pulse + (double)size * 0.8;
                int c = b + (b + 0x5a < 0x168 ? 0x5a : -0x10e);
                int d = c + (c + 0x5a < 0x168 ? 0x5a : -0x10e);
                float sa = FxMath.SinTableAt(a);
                float xa = (float)((double)FxMath.CosTableAt(a) * rad);
                float za = (float)((double)sa * rad);
                float y = (sa + sa) + M.F32(s + 0x14);
                float xb = (float)((double)FxMath.CosTableAt(b) * rad);
                float zb = (float)((double)FxMath.SinTableAt(b) * rad);
                float xc = (float)((double)FxMath.CosTableAt(c) * rad);
                float zc = (float)((double)FxMath.SinTableAt(c) * rad);
                float xd = (float)((double)FxMath.CosTableAt(d) * rad);
                float zd = (float)((double)FxMath.SinTableAt(d) * rad);
                M.F32(s + 0x84, xb); M.F32(s + 0x88, y); M.F32(s + 0x8c, zb);
                M.F32(s + 0x90, xc); M.F32(s + 0x94, y); M.F32(s + 0x98, zc);
                M.F32(s + 0x9c, xa); M.F32(s + 0xa0, y); M.F32(s + 0xa4, za);
                M.F32(s + 0xa8, xd); M.F32(s + 0xac, y); M.F32(s + 0xb0, zd);
                float yb = y - size;
                byte grey = M.U8(s + 0x68);
                short alpha = M.I16(s + 2);
                Quad_b0a900(rt, ref count, alpha, xa, y, za, xb, y, zb, xc, y, zc, xd, y, zd,
                    viewMatrix, 0, 0, 0, grey, grey, 0xfa, 0f, 1.0f, 0f, 1.0f, 0);
                Quad_b0a900(rt, ref count, alpha, xa, y, za, xb, y, zb, xb, yb, zb, xa, yb, za,
                    viewMatrix, 0, 0, 0, grey, grey, 0xdc, 0f, 1.0f, 0f, 1.0f, 0);
                Quad_b0a900(rt, ref count, alpha, xb, y, zb, xc, y, zc, xc, yb, zc, xb, yb, zb,
                    viewMatrix, 0, 0, 0, grey, grey, 0xbe, 0f, 1.0f, 0f, 1.0f, 0);
                Quad_b0a900(rt, ref count, alpha, xc, y, zc, xd, y, zd, xd, yb, zd, xc, yb, zc,
                    viewMatrix, 0, 0, 0, grey, grey, 0x82, 0f, 1.0f, 0f, 1.0f, 0);
                Quad_b0a900(rt, ref count, alpha, xd, y, zd, xa, y, za, xa, yb, za, xd, yb, zd,
                    viewMatrix, 0, 0, 0, grey, grey, 0xa0, 0f, 1.0f, 0f, 1.0f, 0);
                Quad_b0a900(rt, ref count, alpha, xa, yb, za, xb, yb, zb, xc, yb, zc, xd, yb, zd,
                    viewMatrix, 0, 0, 0, grey, grey, 0xfa, 0f, 1.0f, 0f, 1.0f, 0);
            }
        }
    }
}
