// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type73.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        [FxPrimProcess(0x73)]
        public FxCaseResult Case73(FxRuntime rt)
        {
            Process_ae7540(rt);
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x73)]
        public void Render73(FxRuntime rt, uint viewMatrix) => Render_b088a0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_ae7540 (0xae7540) — OnProcess for prim type 0x73 (sparks burst, fn_b83440): camera
        /// angles, follow the master; per active slot three sparks fly along (heading, elevation) pairs
        /// (slot +0x14/+0x1c, +0x24/+0x2c, +0x34/+0x3c) at speed slot +8 (positions +0x9c.., +0x84.., +0xa8..);
        /// after 25 ticks speed ×0.9 and the three alphas (slot +0x44/+0x48/+0x4c) −6 down to 0.
        /// </summary>
        public void Process_ae7540(FxRuntime rt)
        {
            CameraAngles_prims2(rt);
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
                float speed = M.F32(s + 8);
                Spark_ae7540(s, speed, 0x14, 0x1c, 0x9c, 0xa0, 0xa4);
                Spark_ae7540(s, speed, 0x24, 0x2c, 0x84, 0x88, 0x8c);
                Spark_ae7540(s, speed, 0x34, 0x3c, 0xa8, 0xac, 0xb0);
                if (cnt > 0x19)
                {
                    M.F32(s + 8, speed * 0.9f);
                    for (uint o = 0x44; o <= 0x4c; o += 4)
                    {
                        float a = M.F32(s + o) - 6.0f;
                        M.F32(s + o, a);
                        if (0f > a) M.F32(s + o, 0f);
                    }
                }
            }
        }

        void Spark_ae7540(uint s, float speed, uint heading, uint elevation, uint x, uint y, uint z)
        {
            int e = FxOps.Cvtt(M.F32(s + elevation));
            M.F32(s + y, M.F32(s + y) - speed * FxMath.SinTableAt(e));
            float h = speed * FxMath.CosTableAt(e);
            int hd = FxOps.Cvtt(M.F32(s + heading));
            M.F32(s + x, h * FxMath.SinTableAt(hd) + M.F32(s + x));
            M.F32(s + z, h * FxMath.CosTableAt(hd) + M.F32(s + z));
        }

        static readonly uint[] SparkPos_b088a0 = { 0x9c, 0x84, 0xa8 };
        static readonly byte[] SparkRg4_b088a0 = { 0x28, 10, 0x46 };

        /// <summary>
        /// CEffectPrim::fn_b088a0 (0xb088a0) — Render for prim type 0x73: per active slot, three camera-facing
        /// diamonds (size slot +0x80) at the spark positions, each drawn while at or below y = 0 with alpha
        /// cvtt(slot +0x44/+0x48/+0x4c) (≥ 0) and colour by kind slot +0x50 (0..4, else not drawn).
        /// </summary>
        public void Render_b088a0(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            uint[] pos = SparkPos_b088a0;
            byte[] rg4 = SparkRg4_b088a0;
            System.Span<float> p = stackalloc float[12];
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                float size = M.F32(s + 0x80);
                Diamond45(p, size);
                Rotate4_b0d9c0(p, f_3ec, f_4a0);
                for (int k = 0; k < 3; k++)
                {
                    float x = M.F32(s + pos[k]), y = M.F32(s + pos[k] + 4), z = M.F32(s + pos[k] + 8);
                    int alpha = FxOps.Cvtt(M.F32(s + 0x44 + (uint)k * 4));
                    if (alpha < 0) alpha = 0;
                    float p0y = p[1] + y;
                    if (!(0f >= p0y)) continue;
                    float kind = M.F32(s + 0x50);
                    byte r, g, b;
                    if (kind == 0.0f) { r = 0x50; g = 0x50; b = 0xff; }
                    else if (kind == 1.0f) { r = 0xff; g = 200; b = 200; }
                    else if (kind == 2.0f) { r = 0xff; g = 0xa0; b = 0xa0; }
                    else if (kind == 3.0f) { r = 0xff; g = 0x78; b = 0x78; }
                    else if (kind == 4.0f) { r = rg4[k]; g = rg4[k]; b = 0xff; }
                    else continue;
                    Quad_b0a900(rt, ref count, (short)alpha,
                        p[0] + x, p0y, p[2] + z, p[3] + x, p[4] + y, p[5] + z,
                        p[6] + x, p[7] + y, p[8] + z, p[9] + x, p[10] + y, p[11] + z,
                        viewMatrix, 0, 0, 0, r, g, b, 0f, 1.0f, 0f, 1.0f, 0);
                }
            }
        }
    }
}
