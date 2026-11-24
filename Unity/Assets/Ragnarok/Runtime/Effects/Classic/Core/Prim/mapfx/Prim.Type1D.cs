// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type1D.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x1d — the light motes of EF 703 / 710 / 712 (launched by CRagEffect::fn_b80030). Four slots of 0xb4 bytes from
    /// +0x3e8, each four specks (k = 0..3) with position (x, y, z) at slot +0x484 / +0x46c / +0x490 / +0x478 (k order), two
    /// wandering (current, target) angle pairs from slot +0x3fc + 16k, kind +0x450 (14 rise and drift, 16 rise and respawn,
    /// 15 wander and fade, 11 / other sink), fade bytes +0x451..+0x454, colour selector +0x455, speed +0x3f0, size +0x468.
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>CEffectPrim::OnProcess case 0x1d (0xad79f2): <c>fn_adc150(this); break;</c>.</summary>
        [FxPrimProcess(0x1d)]
        public FxCaseResult Case1D(FxRuntime rt)
        {
            Process_adc150(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CEffectPrim::fn_adc150 (0xadc150) — camera angles (+0x3ec / +0x4a0), position = the master's; per slot with +0x3e8 == 1:
        /// the 8 angles chase their targets exactly like fn_aec7d0 (+3 / −2, 0..359, new random target on arrival), then each speck
        /// by the slot kind (sin = the raw table at (int)angle; speed = +0x3f0):
        /// kind 14 — while y &gt; −40 drift x / z by ±0.1·sin; y &gt; 0 → x = z = 0, y = −40; then y += speed (rises: y is down);
        /// kind 16 — y &gt; 0 → z = 0, x = rand % 35 − 17, y = −40; y += speed;
        /// kind 15 — x / z drift by ±0.1·sin; when |(short)x| (or, below 10, |(short)z|) exceeds 10 the fade byte +5, at ≥ 120
        /// (signed) fade 0 and x = z = 0; y untouched;
        /// other — while y &lt; 0 drift by ±0.15·sin; below −30 (speck 0 of kind 11: −50) x / z = rand % 15 − 8 (kind 11) or 0 and
        /// y = rand % 100; then y −= speed (sinks back down).
        /// Signs (x, z): speck 0 (+, +) (kind 14: +, −), speck 1 (−, −) (kind 14: +, −), speck 2 (+, −), speck 3 (−, +). Returns true.
        /// </summary>
        public bool Process_adc150(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                for (uint e = s + 0x3fc; e < s + 0x43c; e += 8)
                {
                    float tgt = M.F32(e + 4);
                    float cur = M.F32(e);
                    if (tgt > cur)                                                      // COMISS tgt, cur; JBE
                    {
                        cur = cur + 3f;                                                 // [0xf4ec9c]
                        if (cur > 359f) cur = 359f;                                     // [0xff758c]
                        M.F32(e, cur);
                        if (cur > tgt) M.F32(e + 4, (float)RandBelow_aec7d0(rt, cur));
                    }
                    else
                    {
                        cur = cur - 2f;                                                 // [0xf4b450]
                        if (0f > cur) cur = 0f;
                        M.F32(e, cur);
                        if (!(tgt < cur) && !float.IsNaN(cur) && !float.IsNaN(tgt))     // COMISS tgt, cur; JB
                            M.F32(e + 4, (float)(0x167 - RandBelow_aec7d0(rt, cur)));
                    }
                }
                //                     x      y      z      ax     az     x+ (14) z+ (14) x+     z+     fade   low threshold (kind 11)
                Speck_adc150(rt, s, 0x484, 0x488, 0x48c, 0x3fc, 0x404, true, false, true, true, 0x451, -50f);
                Speck_adc150(rt, s, 0x46c, 0x470, 0x474, 0x40c, 0x414, true, false, false, false, 0x452, -30f);
                Speck_adc150(rt, s, 0x490, 0x494, 0x498, 0x41c, 0x424, true, false, true, false, 0x453, -30f);
                Speck_adc150(rt, s, 0x478, 0x47c, 0x480, 0x42c, 0x434, false, true, false, true, 0x454, -30f);
            }
            return true;
        }

        /// <summary>v = sin·k + v (<paramref name="plus"/>) or v − sin·k, sin = the raw table at (int)angle.</summary>
        void Drift_adc150(uint v, uint angle, float k, bool plus)
        {
            float t = SinAt(FxOps.Cvtt(M.F32(angle))) * k;
            M.F32(v, plus ? t + M.F32(v) : M.F32(v) - t);
        }

        /// <summary>One speck of fn_adc150 (see <see cref="Process_adc150"/>), in the original's store and rand() order.</summary>
        void Speck_adc150(FxRuntime rt, uint s, uint xo, uint yo, uint zo, uint ax, uint az,
            bool xPlus14, bool zPlus14, bool xPlus, bool zPlus, uint fade, float kind11Low)
        {
            byte kind = M.U8(s + 0x450);
            if (kind == 0xe)
            {
                float y = M.F32(s + yo);
                if (y > -40f)                                                           // [0xf91ab0]
                {
                    Drift_adc150(s + xo, s + ax, 0.1f, xPlus14);                        // [0xf7cff8]
                    Drift_adc150(s + zo, s + az, 0.1f, zPlus14);
                }
                if (y > 0f)
                {
                    M.U32(s + xo, 0);
                    y = -40f;
                    M.U32(s + zo, 0);
                }
                M.F32(s + yo, y + M.F32(s + 0x3f0));
            }
            else if (kind == 0x10)
            {
                float y = M.F32(s + yo);
                if (y > 0f)
                {
                    int r = rt.Rand.Next() % 0x23;
                    M.U32(s + zo, 0);
                    M.F32(s + xo, (float)((r >= 0 ? r : 0) - 0x11));
                    y = -40f;
                }
                M.F32(s + yo, y + M.F32(s + 0x3f0));
            }
            else if (kind == 0xf)
            {
                Drift_adc150(s + xo, s + ax, 0.1f, xPlus);
                float x = M.F32(s + xo);
                Drift_adc150(s + zo, s + az, 0.1f, zPlus);
                short d = Abs16_aec7d0(FxOps.Cvtt(x));
                if (d < 0xa) d = Abs16_aec7d0(FxOps.Cvtt(M.F32(s + zo)));
                if (d > 0xa && AddAlpha(s + fade, 5) >= 0x78)
                {
                    M.U8(s + fade, 0);
                    M.U32(s + xo, 0);
                    M.U32(s + zo, 0);
                }
            }
            else
            {
                float y = M.F32(s + yo);
                if (0f > y)
                {
                    Drift_adc150(s + xo, s + ax, 0.15f, xPlus);                         // [0xfe2104]
                    Drift_adc150(s + zo, s + az, 0.15f, zPlus);
                    float low = kind == 0xb ? kind11Low : -30f;                         // [0xff75a4] / [0xf818dc]
                    if (low > y)
                    {
                        if (kind == 0xb)
                        {
                            M.F32(s + xo, (float)(RandMod_P1D(rt, 0xf) - 8));
                            M.F32(s + zo, (float)(RandMod_P1D(rt, 0xf) - 8));
                        }
                        else
                        {
                            M.U32(s + xo, 0);
                            M.U32(s + zo, 0);
                        }
                        y = (float)RandMod_P1D(rt, 100);
                    }
                }
                M.F32(s + yo, y - M.F32(s + 0x3f0));
            }
        }

        /// <summary>CEffectPrim::Render cases 0x1d / 0x50 (0xaf558b): fn_aee7d0(this, viewMatrix).</summary>
        [FxPrimRender(0x1d, 0x50)]
        public void Render1D(FxRuntime rt, uint viewMatrix) => Render_aee7d0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_aee7d0 (0xaee7d0) — per slot with +0x3e8 == 1: a diamond of radius +0x468 (Diamond45) turned to the camera
        /// (FUN_00b0d9c0 with +0x3ec / +0x4a0), drawn at each speck k (fn_b0a900: texture 0, blend 0, relative to the prim position,
        /// uv 0..1, mode 0). With q1 = speck y + the first corner's y, by the slot kind +0x450:
        /// alpha — kind ≤ 1 (and 11 for specks 1..3): 250, below −20 (q1 + 20)·25 + 250; kind 11 speck 0: 250, below −40
        /// (q1 + 40)·25 + 250; kinds 14 / 16: q1 &gt; −10 → 150 − (q1 + 10)·15, else 100 (0 below −40); kind 15: 255 − 2·fade byte
        /// (+0x451 + k); others: 50, below −4 (q1 + 4)·15 + 50, above −0.5 −100·q1; clamped at 0 (cvttss2si).
        /// Draw — kind 15: always, colour by +0x455 (7 (250, 70, 100), 8 (250, 250, 10), else (70, 120, 100)); every other kind only
        /// when q1 ≤ 0: kind 0 white twice, kind 1 (80, 80, 255) twice, kind 3 (255, 255, 55), kinds 10 / 14 white, kind 11
        /// (155, 155, 155) for speck 0 only (specks 1..3 not drawn), else (70, 120, 100).
        /// One face counter for the whole prim.
        /// </summary>
        public void Render_aee7d0(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            Span<float> p = stackalloc float[12];
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                Diamond45(p, M.F32(s + 0x468));
                Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                SpeckQuad_aee7d0(rt, ref count, p, s, 0x484, 0x488, 0x48c, 0, viewMatrix);
                SpeckQuad_aee7d0(rt, ref count, p, s, 0x46c, 0x470, 0x474, 1, viewMatrix);
                SpeckQuad_aee7d0(rt, ref count, p, s, 0x490, 0x494, 0x498, 2, viewMatrix);
                SpeckQuad_aee7d0(rt, ref count, p, s, 0x478, 0x47c, 0x480, 3, viewMatrix);
            }
        }

        void SpeckQuad_aee7d0(FxRuntime rt, ref int count, Span<float> p, uint s, uint xo, uint yo, uint zo, int k, uint viewMatrix)
        {
            float x = M.F32(s + xo), y = M.F32(s + yo), z = M.F32(s + zo);
            float q0 = x + p[0], q1 = y + p[1], q2 = z + p[2];
            float q3 = x + p[3], q4 = y + p[4], q5 = z + p[5];
            float q6 = x + p[6], q7 = y + p[7], q8 = z + p[8];
            float q9 = x + p[9], q10 = y + p[10], q11 = z + p[11];
            sbyte kind = (sbyte)M.U8(s + 0x450);

            int a;
            if (kind == 0xb && k == 0)
            {
                a = 0xfa;
                if (-40f > q1) a = FxOps.Cvtt((q1 + 40f) * 25f + 250f);                 // [0xf91ab0] [0xf7d138] [0xf9e440] [0xf87448]
            }
            else if (kind <= 1 || kind == 0xb)
            {
                a = 0xfa;
                if (-20f > q1) a = FxOps.Cvtt((q1 + 20f) * 25f + 250f);                 // [0xf817c8] [0xf7d134]
            }
            else if (kind == 0xe || kind == 0x10)
            {
                a = 100;
                if (q1 > -10f) a = FxOps.Cvtt(150f - (q1 + 10f) * 15f);                // [0xf7e078] [0xff756c] [0xf4b624] [0xf7e280]
                else if (-40f > q1) a = 0;
            }
            else if (kind == 0xf)
                a = 0xff - (sbyte)M.U8(s + 0x451 + (uint)k) * 2;
            else
            {
                a = 0x32;
                if (-4f > q1) a = FxOps.Cvtt((q1 + 4f) * 15f + 50f);                    // [0xff7598] [0xf4a764] [0xff7530]
                if (q1 > -0.5f) a = FxOps.Cvtt(q1 * -100f);                             // [0xf80830] [0xff75b0]
            }
            short alpha = (short)(a < 0 ? 0 : a);

            byte r, g, b;
            int times = 1;
            if (kind == 0xf)
            {
                switch (M.U8(s + 0x455))
                {
                    case 7: r = 0xfa; g = 0x46; b = 0x64; break;
                    case 8: r = 0xfa; g = 0xfa; b = 0x0a; break;
                    default: r = 0x46; g = 0x78; b = 0x64; break;
                }
            }
            else
            {
                if (!(q1 <= 0f)) return;                                                // COMISS 0, q1; JB (also NaN)
                switch (kind)
                {
                    case 0: r = 0xff; g = 0xff; b = 0xff; times = 2; break;
                    case 1: r = 0x50; g = 0x50; b = 0xff; times = 2; break;
                    case 3: r = 0xff; g = 0xff; b = 0x37; break;
                    case 0xa: case 0xe: r = 0xff; g = 0xff; b = 0xff; break;
                    case 0xb:
                        if (k != 0) return;
                        r = 0x9b; g = 0x9b; b = 0x9b; break;
                    default: r = 0x46; g = 0x78; b = 0x64; break;
                }
            }
            for (int i = 0; i < times; i++)
                Quad_b0a900(rt, ref count, alpha, q0, q1, q2, q3, q4, q5, q6, q7, q8, q9, q10, q11,
                    viewMatrix, 0, 0, 0, r, g, b, 0f, 1f, 0f, 1f, 0);
        }

        /// <summary>rand() % n with the CMOVNS clamp at 0.</summary>
        static int RandMod_P1D(FxRuntime rt, int n)
        {
            int r = rt.Rand.Next() % n;
            return r >= 0 ? r : 0;
        }
    }
}
