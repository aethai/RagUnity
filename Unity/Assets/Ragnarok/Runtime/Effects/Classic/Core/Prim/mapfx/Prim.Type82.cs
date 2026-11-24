// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type82.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x82 — the white-light wisps of EF 713 / 714 / 946 (launched by CRagEffect::fn_b917f0). Four slots of
    /// 0xb4 bytes from +0x3e8, each four wisps (k = 0..3) with position (x, y, z) at slot +0x484 / +0x46c / +0x490 /
    /// +0x478 (k order), alpha byte +0x45b+k, blink counter +0x457+k, and two wandering (current, target) angle pairs
    /// from slot +0x3fc + 16k. Slot +0x450 = kind (1 = bob in place), +0x455 = colour selector, +0x468 = size.
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>CEffectPrim::OnProcess case 0x82: <c>fn_aec7d0(this); break;</c>.</summary>
        [FxPrimProcess(0x82)]
        public FxCaseResult Case82(FxRuntime rt)
        {
            Process_aec7d0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>CEffectPrim::Render case 0x82: fn_b0ce80(this, viewMatrix).</summary>
        [FxPrimRender(0x82)]
        public void Render82(FxRuntime rt, uint viewMatrix) => Render_b0ce80(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_aec7d0 (0xaec7d0) — camera angles (+0x3ec / +0x4a0), position = the master's; per slot: tick
        /// counter +0x3f4++ (every slot, active or not); for an active slot: alphas +1 up to 120 (kind 1: wisp 0 only),
        /// the 8 angles chase their targets (+3 / −2 per tick, clamped to 0..359; on reaching it a new random target:
        /// rising → rand % (int)cur, falling → 359 − rand % (int)cur), then each wisp moves by speed·sin(angle)
        /// (speed 0.05, kind 1: 0.03, and kind 1 moves only y). A wisp farther than 30 (|(short)x| then |(short)z|; wisps
        /// 2 / 3: more than 30) — or, kind 1, a slot counter above 3000 — fades (alpha −2) and at alpha ≤ 1 respawns at
        /// rand % 31 − 15 (kind 1: counter, x, y, z = 0). Otherwise a rare blink (rand % 125 == 0, kind 1 wisp 0: % 20,
        /// alpha &gt; 100): 90 ticks −6 alpha, then +4 up to 120 until the counter reaches 120. Kind 1 stops the slot
        /// loop after its first active slot. Returns true.
        /// </summary>
        public bool Process_aec7d0(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                float speed = 0.05f;                                                    // [0xf7d128]
                M.I32(s + 0x3f4, M.I32(s + 0x3f4) + 1);
                if (M.U8(s + 0x3e8) != 1) continue;
                IncAlpha_aec7d0(s + 0x45b);
                if (M.U8(s + 0x450) == 1) speed = 0.03f;                                // [0xff74ec]
                else
                {
                    IncAlpha_aec7d0(s + 0x45c);
                    IncAlpha_aec7d0(s + 0x45d);
                    IncAlpha_aec7d0(s + 0x45e);
                }

                for (uint e = s + 0x3fc; e < s + 0x43c; e += 8)
                {
                    float tgt = M.F32(e + 4);
                    float cur = M.F32(e);
                    if (tgt > cur)                                                      // COMISS tgt, cur; JBE
                    {
                        cur = cur + 3f;
                        if (cur > 359f) cur = 359f;
                        M.F32(e, cur);
                        if (cur > tgt) M.F32(e + 4, (float)RandBelow_aec7d0(rt, cur));
                    }
                    else
                    {
                        cur = cur - 2f;
                        if (0f > cur) cur = 0f;
                        M.F32(e, cur);
                        if (!(tgt < cur) && !float.IsNaN(cur) && !float.IsNaN(tgt))     // COMISS tgt, cur; JC
                            M.F32(e + 4, (float)(0x167 - RandBelow_aec7d0(rt, cur)));
                    }
                }

                byte kind = M.U8(s + 0x450);
                float t = SinAt(FxOps.Cvtt(M.F32(s + 0x3fc)));
                float x;
                if (kind == 1)
                {
                    M.F32(s + 0x488, t * speed + M.F32(s + 0x488));
                    x = M.F32(s + 0x484);
                }
                else
                {
                    x = t * speed + M.F32(s + 0x484);
                    M.F32(s + 0x484, x);
                    M.F32(s + 0x48c, speed * SinAt(FxOps.Cvtt(M.F32(s + 0x404))) + M.F32(s + 0x48c));
                }
                short d = Abs16_aec7d0(FxOps.Cvtt(x));
                if (d < 0x1e) d = Abs16_aec7d0(FxOps.Cvtt(M.F32(s + 0x48c)));
                if ((kind == 1 && M.I32(s + 0x3f4) > 3000) || d >= 0x1e)
                {
                    if (AddAlpha(s + 0x45b, -2) <= 1)
                    {
                        if (kind == 1)
                        {
                            M.I32(s + 0x3f4, 0);
                            M.U32(s + 0x488, 0);
                            M.U32(s + 0x484, 0);
                            M.U32(s + 0x48c, 0);
                        }
                        else
                        {
                            M.F32(s + 0x484, (float)(rt.Rand.Next() % 0x1f - 0xf));
                            M.F32(s + 0x48c, (float)(rt.Rand.Next() % 0x1f - 0xf));
                        }
                    }
                }
                else
                {
                    int r = rt.Rand.Next() % (kind == 1 ? 0x14 : 0x7d);
                    StartBlink_aec7d0(r, s + 0x457, s + 0x45b);
                    Blink_aec7d0(s + 0x457, s + 0x45b);
                }
                if (M.U8(s + 0x450) == 1) break;

                // wisp 1 (x +0x46c, z +0x474): angles +0x40c / +0x414, limit < 30
                x = M.F32(s + 0x46c) - speed * SinAt(FxOps.Cvtt(M.F32(s + 0x40c)));
                M.F32(s + 0x46c, x);
                float z = M.F32(s + 0x474) - speed * SinAt(FxOps.Cvtt(M.F32(s + 0x414)));
                M.F32(s + 0x474, z);
                Wander_aec7d0(rt, s, 0x46c, 0x474, 1, x, z, 0x1e);

                // wisp 2 (x +0x490, z +0x498): angles +0x41c / +0x424, limit ≤ 30
                x = speed * SinAt(FxOps.Cvtt(M.F32(s + 0x41c))) + M.F32(s + 0x490);
                M.F32(s + 0x490, x);
                z = M.F32(s + 0x498) - speed * SinAt(FxOps.Cvtt(M.F32(s + 0x424)));
                M.F32(s + 0x498, z);
                Wander_aec7d0(rt, s, 0x490, 0x498, 2, x, z, 0x1f);

                // wisp 3 (x +0x478, z +0x480): angles +0x42c / +0x434, limit ≤ 30
                x = M.F32(s + 0x478) - speed * SinAt(FxOps.Cvtt(M.F32(s + 0x42c)));
                M.F32(s + 0x478, x);
                z = speed * SinAt(FxOps.Cvtt(M.F32(s + 0x434))) + M.F32(s + 0x480);
                M.F32(s + 0x480, z);
                Wander_aec7d0(rt, s, 0x478, 0x480, 3, x, z, 0x1f);
            }
            return true;
        }

        /// <summary>Alpha byte +1 while (signed) below 120.</summary>
        void IncAlpha_aec7d0(uint a)
        {
            sbyte v = (sbyte)M.U8(a);
            if (v < 0x78) M.U8(a, (byte)(v + 1));
        }

        /// <summary>Byte add (ADD byte ptr), returns the new signed value.</summary>
        sbyte AddAlpha(uint a, int delta)
        {
            sbyte v = (sbyte)(M.U8(a) + delta);
            M.U8(a, (byte)v);
            return v;
        }

        /// <summary>0 when (int)cur is 0, else rand() % (int)cur with the CMOVNS clamp (IDIV semantics).</summary>
        static int RandBelow_aec7d0(FxRuntime rt, float cur)
        {
            int n = FxOps.Cvtt(cur);
            if (n == 0) return 0;
            int r = rt.Rand.Next() % n;
            return r >= 0 ? r : 0;
        }

        /// <summary>|(short)v| in 16-bit arithmetic (−32768 stays negative).</summary>
        static short Abs16_aec7d0(int v)
        {
            short s = (short)v;
            return s < 0 ? (short)-s : s;
        }

        /// <summary>A blink starts (counter := 1) on a zero roll when the alpha is above 100 and no blink runs.</summary>
        void StartBlink_aec7d0(int roll, uint counter, uint alpha)
        {
            if (roll == 0 && (sbyte)M.U8(alpha) > 0x64 && M.U8(counter) == 0) M.U8(counter, 1);
        }

        /// <summary>A running blink (counter ≥ 1): counter +1; ≤ 90: alpha −6 (negative → 0); &lt; 120: alpha +4 (max 120); else stop.</summary>
        void Blink_aec7d0(uint counter, uint alpha)
        {
            sbyte c = (sbyte)M.U8(counter);
            if (c < 1) return;
            c = (sbyte)(c + 1);
            M.U8(counter, (byte)c);
            if (c <= 0x5a)
            {
                if (AddAlpha(alpha, -6) < 0) M.U8(alpha, 0);
            }
            else if (c < 0x78)
            {
                if (AddAlpha(alpha, 4) > 0x78) M.U8(alpha, 0x78);
            }
            else M.U8(counter, 0);
        }

        /// <summary>
        /// Wisps 1..3 after their move: distance |(short)x| (then |(short)z| when below 30) against
        /// <paramref name="limit"/>: at or above it the wisp fades (alpha −2; at ≤ 1 respawn x/z = rand % 31 − 15),
        /// else rand() % 125 may start a blink and a running blink advances.
        /// </summary>
        void Wander_aec7d0(FxRuntime rt, uint s, uint xOff, uint zOff, int k, float x, float z, int limit)
        {
            short d = Abs16_aec7d0(FxOps.Cvtt(x));
            if (d < 0x1e) d = Abs16_aec7d0(FxOps.Cvtt(z));
            uint alpha = s + 0x45b + (uint)k, counter = s + 0x457 + (uint)k;
            if (d >= limit)
            {
                if (AddAlpha(alpha, -2) <= 1)
                {
                    M.F32(s + xOff, (float)(rt.Rand.Next() % 0x1f - 0xf));
                    M.F32(s + zOff, (float)(rt.Rand.Next() % 0x1f - 0xf));
                }
                return;
            }
            StartBlink_aec7d0(rt.Rand.Next() % 0x7d, counter, alpha);
            Blink_aec7d0(counter, alpha);
        }

        /// <summary>
        /// CEffectPrim::fn_b0ce80 (0xb0ce80) — Render for prim type 0x82: per active slot a diamond of radius +0x468
        /// (corners 45° / 135° / 225° / 315°) rotated to face the camera (FUN_00b0d9c0 with +0x3ec / +0x4a0), drawn at the
        /// four wisps (fn_b0a900: texture 0 = whitelight.tga, blend 0, relative to the prim position, uv 0..1, mode 0,
        /// alpha max(0, 2 · alpha byte)). Colour by slot +0x455: 7 (250, 70, 100), 8 (250, 250, 10), 10 (250, 250, 250),
        /// else (70, 120, 100). One face counter for the whole prim.
        /// </summary>
        public void Render_b0ce80(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            Span<float> p = stackalloc float[12];
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                Diamond45(p, M.F32(s + 0x468));
                Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                WispQuad_b0ce80(rt, ref count, p, s, 0x484, 0x488, 0x48c, 0x45b, viewMatrix);
                WispQuad_b0ce80(rt, ref count, p, s, 0x46c, 0x470, 0x474, 0x45c, viewMatrix);
                WispQuad_b0ce80(rt, ref count, p, s, 0x490, 0x494, 0x498, 0x45d, viewMatrix);
                WispQuad_b0ce80(rt, ref count, p, s, 0x478, 0x47c, 0x480, 0x45e, viewMatrix);
            }
        }

        void WispQuad_b0ce80(FxRuntime rt, ref int count, Span<float> p, uint s, uint xo, uint yo, uint zo, uint alphaOff, uint viewMatrix)
        {
            float x = M.F32(s + xo), y = M.F32(s + yo), z = M.F32(s + zo);
            int alpha = (sbyte)M.U8(s + alphaOff) * 2;
            if (alpha < 0) alpha = 0;
            byte r, g, b;
            switch (M.U8(s + 0x455))
            {
                case 7: r = 0xfa; g = 0x46; b = 0x64; break;
                case 8: r = 0xfa; g = 0xfa; b = 0x0a; break;
                case 10: r = 0xfa; g = 0xfa; b = 0xfa; break;
                default: r = 0x46; g = 0x78; b = 0x64; break;
            }
            Quad_b0a900(rt, ref count, (short)alpha,
                x + p[0], y + p[1], z + p[2], x + p[3], y + p[4], z + p[5],
                x + p[6], y + p[7], z + p[8], x + p[9], y + p[10], z + p[11],
                viewMatrix, 0, 0, 0, r, g, b, 0f, 1f, 0f, 1f, 0);
        }
    }
}
