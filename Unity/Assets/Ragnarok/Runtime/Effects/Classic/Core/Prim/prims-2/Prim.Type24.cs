// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type24.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x24 (0xad807b, inline) — waving ring (fn_b6c9a0): follows the master;
        /// per active slot (0xb4 bytes @0x3e8): counter +1, width +0.04 up to 2.5 while alpha +20, then alpha −2
        /// and reset to 2.14 / tilt 0x4a when faded; tilt −1 per tick (min 10, alpha reset); 21 radii
        /// (slot +0x14) = base · (1 + sin(9j°) · 0.3 · sin(phase)).
        /// </summary>
        [FxPrimProcess(0x24)]
        public FxCaseResult Case24(FxRuntime rt)
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            int phase0 = 0;
            for (int k = 0; k < 0xb4; k += 0x2d, phase0 += 0x5a)
            {
                uint s = A + 0x3e8 + (uint)k * 4;
                if (M.U8(s) != 1) continue;
                M.I32(s + 0xc, M.I32(s + 0xc) + 1);
                float w = M.F32(s + 0x80) + 0.04f;
                M.F32(s + 0x80, w);
                short tilt = (short)(M.I16(s + 0x7e) - 1);
                M.I16(s + 0x7e, tilt);
                if (tilt < 10)
                {
                    M.I16(s + 0x7e, 10);
                    M.I16(s + 2, 0);
                }
                if (!(w >= 2.5f))
                {
                    M.I16(s + 2, (short)(M.I16(s + 2) + 0x14));
                }
                else
                {
                    short al = (short)(M.I16(s + 2) - 2);
                    M.I16(s + 2, al);
                    if (!(al > 0))
                    {
                        M.I16(s + 2, 0);
                        M.U32(s + 0x80, 0x4008f5c2);
                        M.I16(s + 0x7e, 0x4a);
                    }
                }
                for (int j = 0; j < 21; j++)
                {
                    int ph = M.I32(s + 0xc);
                    ph = k < 0x5a ? ph + phase0 : (ph + k) * 2;
                    int idx = ph % 0x168;
                    float b = M.F32(s + 8);
                    uint dst = s + 0x14 + (uint)j * 4;
                    M.F32(dst, b);
                    M.F32(dst, M.F32(s + 8) * FxMath.SinTableAt(j * 9) * 0.3f * FxMath.SinTableAt(idx) + b);
                }
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x24)]
        public void Render24(FxRuntime rt, uint viewMatrix) => Render_af0710(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_af0710 (0xaf0710) — Render for prim type 0x24: per active slot, an arc band from
        /// start angle (slot +0x10) over span (slot +6) in span/10 steps; inner radius slot +0x80, outer point
        /// tilted by (slot +0x7e) with radius slot +0x14[i]; optional offset rotation when slot +0x88 == 1234.5;
        /// segments emitted with fn_b0b320.
        /// </summary>
        public void Render_af0710(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            float pix = 0f, piz = 0f;          // previous inner point (y = 0)
            float pox = 0f, poy = 0f, poz = 0f; // previous outer point
            for (int k = 0; k < 0xb4; k += 0x2d)
            {
                uint s = A + 0x3e8 + (uint)k * 4;
                if (M.U8(s) != 1) continue;
                int span = M.I16(s + 6);
                int step = span / 10;
                int acc = 0;
                if (span < 0) continue;
                int i = 0;
                do
                {
                    int ang = (M.I16(s + 0x10) + acc) % 0x168;
                    if (M.U16(s + 6) == 0x168 && i == 10) ang = M.I16(s + 0x10);
                    float c = FxMath.CosTableAt(ang), sn = FxMath.SinTableAt(ang);
                    int tilt = M.I16(s + 0x7e);
                    float bas = M.F32(s + 0x80);
                    float innerZ = bas * sn;
                    float r = M.F32(A + 0x3fc + (uint)(k + i) * 4);
                    float innerX = bas * c;
                    float outerY = -(r * FxMath.SinTableAt(tilt));
                    float rc = r * FxMath.CosTableAt(tilt);
                    float outerX = rc * c + innerX;
                    float outerZ = rc * sn + innerZ;
                    if (M.F32(s + 0x88) == 1234.5f)
                    {
                        int e = M.I16(A + 0x554);
                        float se = FxMath.SinTableAt(e), ce = FxMath.CosTableAt(e);
                        float ox = ce * M.F32(s + 0x84) - se * M.F32(s + 0x8c);
                        float oz = se * M.F32(s + 0x84) + ce * M.F32(s + 0x8c);
                        outerX = ox + outerX;
                        innerX = innerX + ox;
                        outerZ = oz + outerZ;
                        innerZ = innerZ + oz;
                    }
                    if (0 < i)
                    {
                        short al = M.I16(s + 2);
                        Strip_b0b320(rt, ref count, i - 1, al,
                            pix, 0f, piz, innerX, 0f, innerZ, outerX, outerY, outerZ, pox, poy, poz,
                            viewMatrix, 0);
                    }
                    i++;
                    if (i > 10) i = 10;
                    pix = innerX; piz = innerZ;
                    pox = outerX; poy = outerY; poz = outerZ;
                    acc += step;
                } while (acc <= M.I16(s + 6));
            }
        }
    }
}
