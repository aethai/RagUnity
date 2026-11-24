// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type5E.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x5e (0xad7bd8, inline) — floating sparkles (fn_b83b10): camera angles;
        /// per active slot with started counter: for 90 ticks drift along heading (slot +0x7e) by speed slot +8
        /// (x/z at slot +0x9c/+0xa4) with y = base (slot +0x88) − speed·sin(2·t)·50; alpha +2 until 25 ticks,
        /// −1 after 70, clamped at 0.
        /// </summary>
        [FxPrimProcess(0x5e)]
        public FxCaseResult Case5E(FxRuntime rt)
        {
            CameraAngles_prims2(rt);
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                int t = M.I32(s + 0xc) + 1;
                M.I32(s + 0xc, t);
                if (t <= 0) continue;
                if (t <= 0x5a)
                {
                    int h = M.I16(s + 0x7e);
                    float speed = M.F32(s + 8);
                    M.F32(s + 0x9c, speed * FxMath.CosTableAt(h) + M.F32(s + 0x9c));
                    M.F32(s + 0xa4, FxMath.SinTableAt(h) * speed + M.F32(s + 0xa4));
                    int k = t * 2;
                    if (k >= 0x168) k -= 0x168;
                    M.F32(s + 0xa0, M.F32(s + 0x88) - speed * FxMath.SinTableAt(k) * 50.0f);
                }
                if (t <= 0x19) M.I16(s + 2, (short)(M.I16(s + 2) + 2));
                short al = M.I16(s + 2);
                if (t > 0x46)
                {
                    al = (short)(al - 1);
                    M.I16(s + 2, al);
                }
                if (al < 0) M.I16(s + 2, 0);
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x5e)]
        public void Render5E(FxRuntime rt, uint viewMatrix) => Render_b04b70(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b04b70 (0xb04b70) — Render for prim type 0x5e: per active slot a camera-facing diamond
        /// (size slot +0x80, FUN_00b0d9c0 with f_3ec / f_4a0) at the slot position (+0x9c/+0xa0/+0xa4), white,
        /// blend 1, via fn_b0a900.
        /// </summary>
        public void Render_b04b70(FxRuntime rt, uint viewMatrix)
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
                Quad_b0a900(rt, ref count, M.I16(s + 2),
                    x + p[0], y + p[1], z + p[2], x + p[3], y + p[4], z + p[5],
                    x + p[6], y + p[7], z + p[8], x + p[9], y + p[10], z + p[11],
                    viewMatrix, 1, 0, 0, 0xff, 0xff, 0xff, 0f, 1.0f, 0f, 1.0f, 0);
            }
        }
    }
}
