// Synced from reference/port/src/RoClassicFx/Prim/effects-7/Prim.Effects7.CastRings.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-7: the magic-circle ring prims of fn_b69b60 (cast circles EF 54..59). Four ring particles of 0xb4 bytes
    /// at +0x3e8 (+0 active, +2 alpha, +4 variant, +6 span, +8 radius, +0xc counter, +0x10 start angle, +0x14 21-float
    /// radius profile, +0x68 21 done-flags, +0x7e tilt, +0x80 inner radius); both render with fn_af0420 (prims-3).
    /// </summary>
    public readonly partial struct Prim
    {
        [FxPrimProcess(0x17)]
        public FxCaseResult Case17(FxRuntime rt) { Process_add610(); return FxCaseResult.Continue; }

        [FxPrimProcess(0x19)]
        public FxCaseResult Case19(FxRuntime rt) { Process_add0d0(); return FxCaseResult.Continue; }

        [FxPrimRender(0x17, 0x19)]
        public void RenderCastRing(FxRuntime rt, uint viewMatrix) => Render_af0420(rt, viewMatrix);

        /// <summary>f_318 == 3.0 exactly (UCOMISS; LAHF; TEST AH,0x44; JP): unordered and unequal both skip.</summary>
        bool PaletteIsThreeE7() => f_318 == 3.0f;

        /// <summary>Angle +0x10 += k + 3 for the first three particles; wrapped once past 360 (signed word compare).</summary>
        void SpinRingE7(uint b, int k)
        {
            if (k < 3) M.I16(b + 0x10, (short)(M.I16(b + 0x10) + (short)(k + 3)));
            short ang = M.I16(b + 0x10);
            if (ang >= 0x168) M.I16(b + 0x10, (short)(ang - 0x168));
        }

        /// <summary>alpha +2 += delta, clamped to 0 (TEST AX,AX; JNS).</summary>
        void FadeRingOutE7(uint b, short delta)
        {
            short a = (short)(M.I16(b + 2) + delta);
            M.I16(b + 2, a);
            if (a < 0) M.I16(b + 2, 0);
        }

        /// <summary>
        /// Profile growth (0xadd743 / 0xadd28b): for every entry not yet done, while the counter ≤ 90 the radius is
        /// r·sin(9j)·sin(counter); an entry reaching r·sin(9j) is clamped there and marked done; negatives become 0.
        /// </summary>
        void GrowRingProfileE7(uint b)
        {
            for (int j = 0; j < 21; j++)
            {
                uint flag = b + 0x68 + (uint)j;
                if (M.U8(flag) != 0) continue;
                uint prof = b + 0x14 + (uint)j * 4;
                int c = M.I32(b + 0xc);
                if (c <= 0x5a) M.F32(prof, M.F32(b + 8) * FxMath.SinTableAt(j * 9) * FxMath.SinTableAt(c));
                float lim = M.F32(b + 8) * FxMath.SinTableAt(j * 9);
                if (M.F32(prof) > lim)
                {
                    M.F32(prof, lim);
                    M.U8(flag, 1);
                }
                if (0f > M.F32(prof)) M.F32(prof, 0f);
            }
        }

        /// <summary>
        /// CEffectPrim::fn_add610 (0xadd610) — type 0x17: follows the master; per active particle (only the 4th when
        /// f_318 == 3) counter++, spin, fade in by 10 (the 4th: +0x606 by 8, cap 60) for 20 ticks unless variant
        /// (+4) == 1, fade out by 5 (the 4th: 2) over the last 40 ticks, then the profile growth.
        /// </summary>
        public void Process_add610()
        {
            uint m = m_master_ptr;
            M.U32(A + 0x10, M.U32(m + 0x10));
            M.U32(A + 0x14, M.U32(m + 0x14));
            M.U32(A + 0x18, M.U32(m + 0x18));
            int k = 0;
            do
            {
                if (PaletteIsThreeE7() && k < 3) k = 3;
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) == 1)
                {
                    int cnt = M.I32(b + 0xc) + 1;
                    M.I32(b + 0xc, cnt);
                    SpinRingE7(b, k);
                    if (cnt >= m_duration - 0x28)
                        FadeRingOutE7(b, (short)(k < 3 ? -5 : -2));
                    else if (cnt < 0x14 && M.I16(b + 4) != 1)
                    {
                        if (k == 3)
                        {
                            short a = (short)(M.I16(A + 0x606) + 8);
                            M.I16(A + 0x606, a);
                            if (a > 0x3c) M.I16(A + 0x606, 0x3c);
                        }
                        else
                        {
                            short a = (short)(M.I16(b + 2) + 10);
                            M.I16(b + 2, a);
                            if (a > 0xb4) M.I16(b + 2, 0xb4);
                        }
                    }
                    GrowRingProfileE7(b);
                }
                k++;
            } while (k < 4);
        }

        /// <summary>
        /// CEffectPrim::fn_add0d0 (0xadd0d0) — type 0x19: like 0x17 without the variant check (4th particle: fade in
        /// by 8 to 100, out by 3), and the 4th particle's profile (0xadd1e5) is different: until done it grows like the
        /// others (counter &gt; 90 marks it done without writing), once done it pulses r·sin(9j)·(0.55 + 0.45·sin(counter % 360)).
        /// </summary>
        public void Process_add0d0()
        {
            uint m = m_master_ptr;
            M.U32(A + 0x10, M.U32(m + 0x10));
            M.U32(A + 0x14, M.U32(m + 0x14));
            M.U32(A + 0x18, M.U32(m + 0x18));
            int k = 0;
            do
            {
                if (PaletteIsThreeE7() && k < 3) k = 3;
                uint b = A + 0x3e8 + (uint)k * 0xb4;
                if (M.U8(b) == 1)
                {
                    int cnt = M.I32(b + 0xc) + 1;
                    M.I32(b + 0xc, cnt);
                    SpinRingE7(b, k);
                    if (cnt >= m_duration - 0x28)
                        FadeRingOutE7(b, (short)(k < 3 ? -5 : -3));
                    else if (cnt < 0x14)
                    {
                        if (k == 3)
                        {
                            short a = (short)(M.I16(A + 0x606) + 8);
                            M.I16(A + 0x606, a);
                            if (a > 100) M.I16(A + 0x606, 100);
                        }
                        else
                        {
                            short a = (short)(M.I16(b + 2) + 10);
                            M.I16(b + 2, a);
                            if (a > 0xb4) M.I16(b + 2, 0xb4);
                        }
                    }
                    if (k < 3) GrowRingProfileE7(b);
                    else PulseRingProfileE7(b);
                }
                k++;
            } while (k < 4);
        }

        /// <summary>The 4th particle's profile of type 0x19 (0xadd1e5..0xadd3a4).</summary>
        void PulseRingProfileE7(uint b)
        {
            for (int j = 0; j < 21; j++)
            {
                uint flag = b + 0x68 + (uint)j;
                uint prof = b + 0x14 + (uint)j * 4;
                int c = M.I32(b + 0xc);
                float tab = FxMath.SinTableAt(j * 9);
                if (M.U8(flag) == 0)
                {
                    float v;
                    if (c <= 0x5a)
                    {
                        v = M.F32(b + 8) * tab * FxMath.SinTableAt(c);
                        M.F32(prof, v);
                    }
                    else
                    {
                        M.U8(flag, 1);
                        v = M.F32(prof);
                    }
                    if (v > M.F32(b + 8) * tab) M.U8(flag, 1);
                    if (0f > M.F32(prof)) M.F32(prof, 0f);
                }
                else
                {
                    int idx = c % 0x168;
                    float v1 = M.F32(b + 8) * tab * 0.55f;
                    M.F32(prof, v1);
                    M.F32(prof, M.F32(b + 8) * tab * 0.45f * FxMath.SinTableAt(idx) + v1);
                }
            }
        }
    }
}
