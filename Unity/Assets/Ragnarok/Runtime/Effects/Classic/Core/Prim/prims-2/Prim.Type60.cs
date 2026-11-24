// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type60.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        [FxPrimProcess(0x60)]
        public FxCaseResult Case60(FxRuntime rt)
        {
            Process_ae7a40(rt);
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x60)]
        public void Render60(FxRuntime rt, uint viewMatrix) => Render_b04e30(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_ae7a40 (0xae7a40) — OnProcess for prim type 0x60 (falling leaves / petals, fn_b84010):
        /// camera angles; per active slot with started counter: alpha +5 (−2 from tick 30, min 0); two wobble
        /// angles (slot +0x14/+0x18 and +0x1c/+0x20: value chases a random target, +3 up to 359 / −2 down to 0);
        /// while above ground (y slot +0xa0 &lt; 0) x/z drift by sin(wobble)·0.03; y −= fall speed slot +8.
        /// </summary>
        public bool Process_ae7a40(FxRuntime rt)
        {
            CameraAngles_prims2(rt);
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                int cnt = M.I32(s + 0xc) + 1;
                M.I32(s + 0xc, cnt);
                if (cnt <= 0) continue;
                short al = (short)((cnt >= 0x1e ? -2 : 5) + M.I16(s + 2));
                M.I16(s + 2, al);
                if (al < 0) M.I16(s + 2, 0);
                for (uint w = s + 0x14; w < s + 0x24; w += 8)
                {
                    float target = M.F32(w + 4);
                    float cur = M.F32(w);
                    if (target > cur)
                    {
                        cur = cur + 3.0f;
                        if (cur > 359.0f) cur = 359.0f;
                        M.F32(w, cur);
                        if (cur > target)
                        {
                            int n = FxOps.Cvtt(cur);
                            int v = 0;
                            if (n != 0)
                            {
                                int r = rt.Rand.Next() % n;
                                v = r < 0 ? 0 : r;
                            }
                            M.F32(w + 4, (float)v);
                        }
                    }
                    else
                    {
                        cur = cur - 2.0f;
                        if (0f > cur) cur = 0f;
                        M.F32(w, cur);
                        if (target >= cur)
                        {
                            int n = FxOps.Cvtt(cur);
                            int v = 0;
                            if (n != 0)
                            {
                                int r = rt.Rand.Next() % n;
                                v = r < 0 ? 0 : r;
                            }
                            M.F32(w + 4, (float)(0x167 - v));
                        }
                    }
                }
                float y = M.F32(s + 0xa0);
                if (0f > y)
                {
                    M.F32(s + 0x9c, FxMath.SinTableAt(FxOps.Cvtt(M.F32(s + 0x14))) * 0.03f + M.F32(s + 0x9c));
                    M.F32(s + 0xa4, FxMath.SinTableAt(FxOps.Cvtt(M.F32(s + 0x1c))) * 0.03f + M.F32(s + 0xa4));
                }
                M.F32(s + 0xa0, y - M.F32(s + 8));
            }
            return true;
        }

        /// <summary>
        /// CEffectPrim::fn_b04e30 (0xb04e30) — Render for prim type 0x60: per active slot a camera-facing diamond
        /// (size slot +0x80) at the slot position, drawn only while its first corner is at or below y = 0;
        /// colour by slot +0x68 (2 = blue, 3 = dark, else white), via fn_b0a900.
        /// </summary>
        public void Render_b04e30(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            System.Span<float> p = stackalloc float[12];
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                float size = M.F32(s + 0x80);
                Diamond45(p, size);
                Rotate4_b0d9c0(p, f_3ec, f_4a0);
                float x = M.F32(s + 0x9c), y = M.F32(s + 0xa0), z = M.F32(s + 0xa4);
                float p0y = y + p[1];
                if (!(0f >= p0y)) continue;
                byte kind = M.U8(s + 0x68);
                byte r, g, b;
                if (kind == 2) { r = 0x69; g = 0x69; b = 0xe1; }
                else if (kind == 3) { r = 0x37; g = 0x37; b = 0x37; }
                else { r = 0xff; g = 0xff; b = 0xff; }
                Quad_b0a900(rt, ref count, M.I16(s + 2),
                    x + p[0], p0y, z + p[2], x + p[3], y + p[4], z + p[5],
                    x + p[6], y + p[7], z + p[8], x + p[9], y + p[10], z + p[11],
                    viewMatrix, 0, 0, 0, r, g, b, 0f, 1.0f, 0f, 1.0f, 0);
            }
        }
    }
}
