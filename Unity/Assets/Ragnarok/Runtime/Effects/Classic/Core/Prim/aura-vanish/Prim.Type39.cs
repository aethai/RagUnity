// Synced from reference/port/src/RoClassicFx/Prim/aura-vanish/Prim.Type39.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x39 (EF 304, launched by fn_b8d220): four rings that swell and spin. Processed inline in
    /// CEffectPrim::OnProcess (case 0x39 @0xad8a06, ends with break), drawn by the shared ring renderer fn_af0420
    /// (CEffectPrim::Render 0xaf4ce0 groups 0x39 with 0x17..0x8b).
    /// </summary>
    public readonly partial struct Prim
    {
        [FxPrimProcess(0x39)]
        public FxCaseResult Case39(FxRuntime rt)
        {
            Process39_ad8a06();
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x39)]
        public void Render39(FxRuntime rt, uint viewMatrix) => Render_af0420(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::OnProcess case 0x39 (0xad8a06). For each enabled ring block k (stride 0xb4 from +0x3e8): age
        /// (+0xc) + 1; start angle (+0x10) += 5 − k, wrapped by −360 once it reaches 360 (16-bit). From age 1 on: while
        /// the "grown" byte (+0x68) is clear the ring radius (+8) = (char)+0x69 · sin(2 · age) — the byte is set once
        /// 2 · age ≥ 180, and from 2 · age ≥ 135 the alpha (+2) drops by 1 a tick; once grown the alpha drops by 10 a
        /// tick (both clamped at 0). Then its 21 segment heights (+0x14 + 4j) = r + sin(9j) · r · 0.3 · sin(w), with
        /// w = (age + 90k) % 360 for the first two rings and ((age + 30k) · 3) % 360 for the other two.
        /// </summary>
        void Process39_ad8a06()
        {
            const float Swell = 0.3f;   // [0xf81604]
            int k = 0, angle = 0, third = 0;
            for (uint c = A + 0x3f8; angle < 0x168; c += 0xb4, k++, angle += 0x5a, third += 0x1e)
            {
                if (M.U8(c - 0x10) != 1) continue;
                int age = M.I32(c - 4) + 1;
                M.I32(c - 4, age);
                ushort start = (ushort)(M.U16(c) - (ushort)k + 5);
                M.U16(c, start);
                if ((short)start >= 0x168) M.U16(c, (ushort)(start + 0xfe98));
                if (age <= 0) continue;
                if (M.U8(c + 0x58) == 0)
                {
                    int twice = age + age;
                    if (twice >= 0xb4) M.U8(c + 0x58, 1);
                    else if (twice >= 0x87)
                    {
                        short alpha = (short)(M.I16(c - 0xe) - 1);
                        M.I16(c - 0xe, alpha);
                        if (alpha < 0) M.I16(c - 0xe, 0);
                    }
                    M.F32(c - 8, (float)(sbyte)M.U8(c + 0x59) * FxMath.SinTableAt(age + age));
                }
                else
                {
                    short alpha = (short)(M.I16(c - 0xe) - 10);
                    M.I16(c - 0xe, alpha);
                    if (alpha < 0) M.I16(c - 0xe, 0);
                }
                for (int j = 0; j < 21; j++)
                {
                    int a = M.I32(c - 4);
                    int w = (angle < 0xb4 ? a + angle : (a + third) * 3) % 0x168;
                    float r = M.F32(c - 8);
                    uint dst = c + 4 + (uint)j * 4;
                    M.F32(dst, r);
                    float v = FxMath.SinTableAt(j * 9) * M.F32(c - 8);
                    v = v * Swell;
                    v = v * FxMath.SinTableAt(w);
                    M.F32(dst, v + r);
                }
            }
        }
    }
}
