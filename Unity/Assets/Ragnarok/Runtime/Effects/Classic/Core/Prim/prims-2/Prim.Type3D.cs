// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type3D.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x3d (0xad6ecc, inline) — sword aura (fn_b7a290 / fn_b7a4b0): follows the
        /// master; per active slot: counter +1, yaw (slot +0x10) = owner direction + 270 on the first tick (or
        /// without the spin flag slot +0x68), else +10; alpha +20 up to 100 for 10 ticks, after 26 ticks
        /// slot +0x14 += 0.1 and alpha −6 down to 0.
        /// </summary>
        [FxPrimProcess(0x3d)]
        public FxCaseResult Case3D(FxRuntime rt)
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
                short yaw;
                if (cnt > 1 && M.U8(s + 0x68) == 1) yaw = (short)(M.I16(s + 0x10) + 10);
                else
                {
                    uint owner = M.U32(m_master_ptr + 0x138);
                    yaw = (short)FxOps.Cvtt(M.F32(owner + 0x4c) + 270.0f);
                }
                M.I16(s + 0x10, yaw);
                if (yaw >= 0x168) M.I16(s + 0x10, (short)(yaw - 0x168));
                cnt = M.I32(s + 0xc);
                if (cnt < 10)
                {
                    short al = M.I16(s + 2);
                    if (al < 0x64) M.I16(s + 2, (short)(al + 0x14));
                }
                else if (cnt > 0x1a)
                {
                    M.F32(s + 0x14, M.F32(s + 0x14) + 0.1f);
                    short al = M.I16(s + 2);
                    if (al > 0)
                    {
                        al = (short)(al - 6);
                        M.I16(s + 2, al);
                    }
                    if (al < 0) M.I16(s + 2, 0);
                }
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x3d)]
        public void Render3D(FxRuntime rt, uint viewMatrix) => Render_affd30(rt, viewMatrix);

        static int Wrap360_affd30(int v)
        {
            if (v >= 0x168) v -= 0x168;
            if (v < 0) v += 0x168;
            return v;
        }

        /// <summary>
        /// CEffectPrim::fn_affd30 (0xaffd30) — Render for prim type 0x3d: per active slot a 3 × 5 lattice of
        /// blade quads around the owner (yaw slot +0x10), each drawn in three layers (radius R, R+0.2, R+0.4+slot
        /// +0x14) with fn_b0a900; width (1.5 − |row−1|) + 1.5 stored at slot +0x80; colour by slot +0x68.
        /// </summary>
        public void Render_affd30(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                int row = 0;
                int l14 = -0x195;
                do
                {
                    float rm1 = (float)(row - 1);
                    int u = l14 + 0x1c2;
                    int i1 = u - 0x5a;
                    int col = 0;
                    do
                    {
                        float abs = rm1;
                        if (0f > rm1) abs = rm1 * -1.0f;
                        float w = (1.5f - abs) + 1.5f;
                        M.F32(s + 0x80, w);
                        for (int layer = 0; layer < 3; layer++)
                        {
                            int a = i1 < 0x168 ? i1 : l14;
                            if (a < 0) a += 0x168;
                            short yaw = M.I16(s + 0x10);
                            int b = Wrap360_affd30(col - 0x5a + yaw);
                            int c = Wrap360_affd30(col - 0xb4 + yaw);
                            int ui = layer == 0 ? u : (u < 0 ? u + 0x168 : u);
                            float R = M.F32(s + 8);
                            float yTerm, xTerm, zTerm;
                            if (layer == 0)
                            {
                                float rc = R * FxMath.CosTableAt(a);
                                yTerm = R * FxMath.SinTableAt(a) - (R + 2.0f);
                                zTerm = rc * FxMath.SinTableAt(b);
                                xTerm = rc * FxMath.CosTableAt(b);
                            }
                            else
                            {
                                float rk = layer == 1 ? R + 0.2f : (R + 0.4f) + M.F32(s + 0x14);
                                float rc = rk * FxMath.CosTableAt(a);
                                yTerm = rk * FxMath.SinTableAt(a) - M.F32(s + 8);
                                xTerm = rc * FxMath.CosTableAt(b);
                                zTerm = rc * FxMath.SinTableAt(b);
                            }
                            float wv = M.F32(s + 0x80);
                            float w1 = (float)FxOps.Cvtt(wv);
                            float w2 = (float)FxOps.Cvtt(w1);
                            float n1 = (float)FxOps.Cvtt(-wv);
                            float n2 = (float)FxOps.Cvtt(n1);
                            float cc = FxMath.CosTableAt(c), sc = FxMath.SinTableAt(c);
                            float cu = FxMath.CosTableAt(ui), su = FxMath.SinTableAt(ui);
                            float y0 = w1 * su + yTerm;
                            float ac = w1 * cu;
                            float aCos = ac * cc, aSin = ac * sc;
                            float w2c = w2 * cc, w2s = w2 * sc;
                            float n2c = n2 * cc, n2s = n2 * sc;
                            float bc = n1 * cu;
                            float bCos = bc * cc, bSin = bc * sc;
                            float y2 = n1 * su + yTerm;

                            byte kind = M.U8(s + 0x68);
                            byte r, g, bl;
                            byte blend = 0, tex = 0;
                            if (kind == 3) { r = 0xff; g = 100; bl = 0; }
                            else if (kind == 2) { r = 0xff; g = 0x9b; bl = 0; }
                            else if (kind == 4) { r = 0xff; g = 0x96; bl = 0xff; }
                            else if (kind == 5) { r = 100; g = 100; bl = 0xff; }
                            else
                            {
                                byte k = layer == 0 ? (byte)0xcd : layer == 1 ? (byte)0x9b : (byte)0xe1;
                                r = k; g = 0xff; bl = k;
                            }
                            if (layer == 2 && kind != 5) { blend = 1; tex = 1; }

                            Quad_b0a900(rt, ref count, M.I16(s + 2),
                                (w2c - aSin) + xTerm, y0, (aCos + w2s) + zTerm,
                                (n2c - aSin) + xTerm, y0, (n2s + aCos) + zTerm,
                                (n2c - bSin) + xTerm, y2, (bCos + n2s) + zTerm,
                                (w2c - bSin) + xTerm, y2, (bCos + w2s) + zTerm,
                                viewMatrix, blend, (sbyte)tex, 0, r, g, bl, 0f, 1.0f, 0f, 1.0f, 0);
                        }
                        col += 0x2d;
                    } while (col < 0xe1);
                    l14 += 0x2d;
                    row++;
                } while (l14 < -0x10e);
            }
        }
    }
}
