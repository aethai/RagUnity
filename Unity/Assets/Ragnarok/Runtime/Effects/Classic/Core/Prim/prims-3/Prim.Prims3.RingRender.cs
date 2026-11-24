// Synced from reference/port/src/RoClassicFx/Prim/prims-3/Prim.Prims3.RingRender.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>prims-3: ring renderers built from fn_b0b320 segments.</summary>
    public readonly partial struct Prim
    {
        [FxPrimRender(0x18, 0x1e, 0x1f, 0x29, 0x32, 0x3c, 0x6b, 0x6c, 0x71, 0x76)]
        public void RenderRing(FxRuntime rt, uint viewMatrix) => Render_af0420(rt, viewMatrix);

        [FxPrimRender(0x4d)]
        public void Render4D(FxRuntime rt, uint viewMatrix) => Render_b09220(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_af0420 (0xaf0420) — per active particle a ring (inner radius +0x80, outer radius =
        /// profile[i] tilted by angle +0x7e) sampled every (span/20) degrees from the start angle +0x10;
        /// consecutive samples are joined by fn_b0b320 quads. +8 == 6.001 / 24.001 lifts the ring by 2 / 1.
        /// span (+6) in [0, 20) never advances: the original loops forever there.
        /// </summary>
        public void Render_af0420(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            float ppx = 0f, ppy = 0f, ppz = 0f;   // previous inner point
            float pqx = 0f, pqy = 0f, pqz = 0f;   // previous outer point
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                ushort span16 = M.U16(b + 6);
                int span = (short)span16;
                int step = span / 20;
                if (span < 0) continue;
                int i = 0, acc = 0;
                ushort cur = span16;
                do
                {
                    int start = M.I16(b + 0x10);
                    int a = (acc + start) % 0x168;
                    if (cur == 0x168 && i == 0x14) a = start;
                    float cs = P3Cos(a);
                    float kind = M.F32(b + 8);
                    float rad = M.F32(b + 0x80);
                    float pz = rad * P3Sin(a);
                    float px = rad * cs;
                    float py = kind == 6.001f ? -2.0f : kind == 24.001f ? -1.0f : 0f;
                    int tilt = M.I16(b + 0x7e);
                    float v = M.F32(A + 0x3fc + (uint)(k * 0x2d + i) * 4);
                    float qy = v * P3Sin(tilt);
                    float h = v * P3Cos(tilt);
                    float qz = h * P3Sin(a) + pz;
                    float qx = h * cs + px;
                    if (kind == 6.001f) qy += 2.0f;
                    else if (kind == 24.001f) qy += 1.0f;
                    qy = -qy;
                    if (i > 0)
                    {
                        short alpha = (short)M.U16(b + 2);
                        Strip_b0b320(rt, ref cnt, i - 1, alpha,
                            ppx, ppy, ppz, px, py, pz, qx, qy, qz, pqx, pqy, pqz, matrix, 0);
                    }
                    ppx = px; ppy = py; ppz = pz;
                    pqx = qx; pqy = qy; pqz = qz;
                    i++;
                    if (i >= 0x15) i = 0x14;
                    acc += step;
                    cur = M.U16(b + 6);
                } while (acc <= (short)cur);
            }
        }

        /// <summary>
        /// CEffectPrim::fn_b09220 (0xb09220) — per active particle a vertical band: points at angle a on a
        /// circle of radius +0x80, offset ±(height +8) along the tilt +0x7e, base height +0x14; consecutive
        /// samples joined by fn_b0b320 quads (same stepping, same infinite loop for span in [0, 20)).
        /// </summary>
        public void Render_b09220(FxRuntime rt, uint matrix)
        {
            int cnt = 0;
            float p1x = 0f, p1y = 0f, p1z = 0f;
            float p2x = 0f, p2y = 0f, p2z = 0f;
            for (int k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) != 1) continue;
                int span = M.I16(b + 6);
                int step = span / 20;
                if (span < 0) continue;
                int i = 0, acc = 0;
                ushort cur = M.U16(b + 6);
                do
                {
                    int start = M.I16(b + 0x10);
                    int a = start + acc;
                    if (a >= 0x168) a -= 0x168;
                    else if (a < 0) a += 0x168;
                    if (cur == 0x168 && i == 0x14) a = start;
                    int tilt = M.I16(b + 0x7e);
                    float rad = M.F32(b + 0x80);
                    float hgt = M.F32(b + 8);
                    float ca = P3Cos(a);
                    float ct = P3Cos(tilt);
                    float st = P3Sin(tilt);
                    float sa = rad * P3Sin(a);
                    float hs = -(hgt * st);
                    float rc = rad * ct * ca;
                    float rs = rad * st * ca;
                    float hc = hgt * ct;
                    float x1 = hs + rc;
                    float x2 = rc - hs;
                    float z1 = sa + 0f;
                    float z2 = sa - 0f;
                    float y1 = hc + rs + M.F32(b + 0x14);
                    float y2 = rs - hc + M.F32(b + 0x14);
                    if (i > 0)
                    {
                        short alpha = (short)M.U16(b + 2);
                        Strip_b0b320(rt, ref cnt, i - 1, alpha,
                            p1x, p1y, p1z, x1, y1, z1, x2, y2, z2, p2x, p2y, p2z, matrix, 0);
                    }
                    p1x = x1; p1y = y1; p1z = z1;
                    p2x = x2; p2y = y2; p2z = z2;
                    i++;
                    if (i >= 0x15) i = 0x14;
                    acc += step;
                    cur = M.U16(b + 6);
                } while (acc <= (short)cur);
            }
        }
    }
}
