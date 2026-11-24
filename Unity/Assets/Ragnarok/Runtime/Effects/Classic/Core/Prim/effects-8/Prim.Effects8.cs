// Synced from reference/port/src/RoClassicFx/Prim/effects-8/Prim.Effects8.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-8 prim types: 0x3f (Warp Portal cylinders), 0x63 (Warm "super" sprites), 0x64 (Taekwon kick trails),
    /// 0x74 (Shield Chain shields) and the OnProcess of 0x75 (Gravitation Field debris; its render is type 9's).
    /// </summary>
    public readonly partial struct Prim
    {
        // ---- type 0x3f -----------------------------------------------------------------------------------------

        [FxPrimProcess(0x3f)]
        public FxCaseResult Case3F(FxRuntime rt) { Process_ae8040(); return FxCaseResult.Continue; }

        [FxPrimRender(0x3f)]
        public void Render3F(FxRuntime rt, uint viewMatrix) => Render_af0420(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_ae8040 (0xae8040) — Warp Portal cylinders: follow the master; for the three blocks: life++
        /// (block 0 also counts the total at +0x610); by the mode at +0x608 — 3: the height (+0x80) sinks 0.25 per
        /// tick and below 7 the alpha fades by 7, a faded block restarts at height 14 (disabled after 1400 ticks);
        /// else the height rises 0.5 per tick and above 7 the alpha fades by 5 (mode 4), 8 (mode 5) or 15, a faded
        /// block restarts at height 0 (disabled after 120 ticks for modes 1/4/5, after 1400 otherwise). Young blocks
        /// fade in (+12 up to 240 in mode 3 for 20 ticks; +4 up to 40 in mode 4, +24 up to 120 in mode 5, +24 up
        /// to 240 otherwise, for 10 ticks). The 21 segment radii (+0x14..) = radius (+8), scaled by sin(9·life)
        /// while life ≤ 10. Returns true.
        /// </summary>
        public bool Process_ae8040()
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            for (int k = 0; k < 3; k++)
            {
                uint s = A + (uint)k * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                M.I32(s + 0x3f4, M.I32(s + 0x3f4) + 1);
                int life = M.I32(s + 0x3f4);
                if (k == 0)
                {
                    M.I32(A + 0x610, M.I32(A + 0x610) + 1);
                    life = M.I32(s + 0x3f4);
                }
                if (life <= 0) continue;
                float h = M.F32(s + 0x468);
                if (M.I16(A + 0x608) == 3)
                {
                    h = h - 0.25f;
                    M.F32(s + 0x468, h);
                    if (7.0f > h)
                    {
                        if (0f > h) M.F32(s + 0x468, 0f);
                        short a = (short)(M.I16(s + 0x3ea) - 7);
                        M.I16(s + 0x3ea, a);
                        if (a < 0)
                        {
                            M.I16(s + 0x3ea, 0);
                            if (M.I32(A + 0x610) > 0x578) M.U8(s + 0x3e8, 0);
                            else
                            {
                                M.I32(s + 0x3f4, 0);
                                M.U32(s + 0x468, 0x41600000);   // 14.0
                            }
                        }
                    }
                }
                else
                {
                    h = h + 0.5f;
                    M.F32(s + 0x468, h);
                    if (h > 7.0f)
                    {
                        short mode = M.I16(A + 0x608);
                        short a;
                        if (mode == 4) a = (short)(M.I16(s + 0x3ea) - 5);
                        else if (mode == 5) a = (short)(M.I16(s + 0x3ea) - 8);
                        else a = (short)(M.I16(s + 0x3ea) - 0xf);
                        M.I16(s + 0x3ea, a);
                        if (a < 0)
                        {
                            M.I16(s + 0x3ea, 0);
                            int total = M.I32(A + 0x610);
                            bool off = total > 0x78 && (mode == 1 || mode == 4 || mode == 5) || total > 0x578;
                            if (off) M.U8(s + 0x3e8, 0);
                            else
                            {
                                M.I32(s + 0x3f4, 0);
                                M.U32(s + 0x468, 0);
                            }
                        }
                    }
                }
                short m = M.I16(A + 0x608);
                life = M.I32(s + 0x3f4);
                if (m == 3)
                {
                    if (life < 0x14)
                    {
                        M.I16(s + 0x3ea, (short)(M.I16(s + 0x3ea) + 0xc));
                        if (M.I16(s + 0x3ea) > 0xf0) M.I16(s + 0x3ea, 0xf0);
                    }
                }
                else if (life < 10)
                {
                    if (m == 4)
                    {
                        M.I16(s + 0x3ea, (short)(M.I16(s + 0x3ea) + 4));
                        if (M.I16(s + 0x3ea) > 0x28) M.I16(s + 0x3ea, 0x28);
                    }
                    else
                    {
                        M.I16(s + 0x3ea, (short)(M.I16(s + 0x3ea) + 0x18));
                        if (m != 5)
                        {
                            if (M.I16(s + 0x3ea) > 0xf0) M.I16(s + 0x3ea, 0xf0);
                        }
                        else if (M.I16(s + 0x3ea) > 0x78) M.I16(s + 0x3ea, 0x78);
                    }
                }
                for (uint j = 0; j < 0x15; j++)
                {
                    float r = M.F32(s + 0x3f0);
                    uint dst = s + 0x3fc + j * 4;
                    M.F32(dst, r);
                    int l = M.I32(s + 0x3f4);
                    if (l <= 10) M.F32(dst, r * FxMath.SinTableAt(l * 9));
                }
            }
            return true;
        }

        // ---- type 0x63 -----------------------------------------------------------------------------------------

        [FxPrimProcess(0x63)]
        public FxCaseResult Case63(FxRuntime rt) { Process_ae5d40(rt); return FxCaseResult.Continue; }

        [FxPrimRender(0x63)]
        public void Render63(FxRuntime rt, uint viewMatrix) => Render_afbec0(rt, viewMatrix);

        static readonly float[] LayerScales_afbec0 = { 1.0f, 1.2f, 2.0f };

        /// <summary>
        /// CEffectPrim::fn_afbec0 (0xafbec0) — Render for prim type 0x63 (Warm): like fn_b00750 (type 0x23: four
        /// camera-facing diamonds per slot at (cos, sin)·4 of their angle) but drawn three times per slot with the
        /// size scaled 1.0 / 1.2 / 2.0, a colour per layer from the slot byte +0x6c (0: blue, 1: grey, else red) and
        /// the alpha limited to the slot's life (+0xc).
        /// </summary>
        public void Render_afbec0(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            uint[] cosOff = SubSpriteCosOff, sinOff = SubSpriteSinOff, hOff = SubSpriteHeightOff;
            float[] scales = LayerScales_afbec0;
            System.Span<float> p = stackalloc float[12];
            System.Span<float> q = stackalloc float[12];
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                for (int layer = 0; layer < 3; layer++)
                {
                    byte r, g, b;
                    byte kind = M.U8(s + 0x454);
                    if (kind == 0)
                    {
                        if (layer == 0) { r = 100; g = 100; b = 0xff; }
                        else { r = 0; g = 0; b = 0x78; }
                    }
                    else if (kind == 1)
                    {
                        if (layer == 0) { r = 0x5a; g = 0x5a; b = 0x50; }
                        else if (layer == 1) { r = 0x3c; g = 0x3c; b = 0x32; }
                        else { r = 0x1e; g = 0x1e; b = 0x14; }
                    }
                    else
                    {
                        if (layer == 0) { r = 0xff; g = 100; b = 100; }
                        else { r = 0x78; g = 0; b = 0; }
                    }
                    float size = scales[layer] * M.F32(s + 0x468);
                    for (int j = 0; j < 4; j++)
                    {
                        int ia = FxOps.Cvtt(M.F32(s + 0x408 + (uint)j * 0x10));
                        M.F32(s + cosOff[j], FxMath.CosTableAt(ia) * 4.0f);
                        M.F32(s + sinOff[j], FxMath.SinTableAt(ia) * 4.0f);
                    }
                    Diamond45(p, size);
                    Rotate4_b0d9c0(p, f_3ec, f_4a0);
                    for (int j = 0; j < 4; j++)
                    {
                        uint e = s + (uint)j * 0x10;
                        float tex = M.F32(e + 0x404);
                        int rel = FxOps.Cvtt(M.F32(e + 0x408) - (float)(int)f_3ec);
                        bool flip = Flip_b00750(tex, rel);
                        float c = M.F32(s + cosOff[j]), sn = M.F32(s + sinOff[j]), h = M.F32(s + hOff[j]);
                        float off = M.F32(s + 0x44c);
                        q.Clear();
                        for (int k = 0; k < 4; k++)
                        {
                            int src = flip ? (k ^ 1) : k;
                            q[k * 3] = p[src * 3] + c;
                            q[k * 3 + 1] = (p[src * 3 + 1] + h) - off;
                            q[k * 3 + 2] = p[src * 3 + 2] + sn;
                        }
                        int alpha = FxOps.Cvtt(M.F32(e + 0x400));
                        int life = M.I32(s + 0x3f4);
                        if (!(alpha <= life)) alpha = life;   // CMOVLE: min(cvtt(alpha), life)
                        Quad_b0a900(rt, ref count, (short)alpha,
                            q[0], q[1], q[2], q[3], q[4], q[5], q[6], q[7], q[8], q[9], q[10], q[11],
                            viewMatrix, 0, (sbyte)(byte)FxOps.Cvtt(tex), 0, r, g, b,
                            0f, 1.0f, 0f, 1.0f, 2);
                    }
                }
            }
        }

        // ---- type 0x64 -----------------------------------------------------------------------------------------

        /// <summary>
        /// CEffectPrim::OnProcess case 0x64 (inline, 0xad6b0b) — Taekwon kick trails: per active slot life++; once
        /// alive: after 35 ticks the alpha fades by 5 (clamped at 0), the swing angle (+0x14) grows 6 per tick up to
        /// 180, the angle history (+0x14 dwords) and its byte twin (+0x68) shift by one for cvtt(+0x94) entries, and
        /// the slot centre (+0x84) follows the master effect.
        /// </summary>
        [FxPrimProcess(0x64)]
        public FxCaseResult Case64(FxRuntime rt)
        {
            for (int k = 0; k < 4; k++)
            {
                uint s = A + (uint)k * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                int life = M.I32(s + 0x3f4) + 1;
                M.I32(s + 0x3f4, life);
                if (life <= 0) continue;
                if (life > 0x23) M.I16(s + 0x3ea, (short)(M.I16(s + 0x3ea) - 5));
                if (M.I16(s + 0x3ea) < 0) M.I16(s + 0x3ea, 0);
                float a = M.F32(s + 0x3fc) + 6.0f;
                M.F32(s + 0x3fc, a);
                if (a > 180.0f) M.U32(s + 0x3fc, 0x43340000);
                int n = FxOps.Cvtt(M.F32(s + 0x47c));
                for (int i = n; i >= 1; i--)
                {
                    M.U32(s + 0x3fc + (uint)i * 4, M.U32(s + 0x3f8 + (uint)i * 4));
                    M.U8(s + 0x450 + (uint)i, M.U8(s + 0x44f + (uint)i));
                }
                uint master = m_master_ptr;
                M.U32(s + 0x46c, M.U32(master + 0x10));
                M.U32(s + 0x470, M.U32(master + 0x14));
                M.U32(s + 0x474, M.U32(master + 0x18));
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x64)]
        public void Render64(FxRuntime rt, uint viewMatrix) => Render_b01cc0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b01cc0 (0xb01cc0) — Render for prim type 0x64: for each live slot (enabled, alpha &gt; 0,
        /// life &gt; 0) and each trail step i &lt; +0x94 (angle history a_i, a_i+1 at +0x14 + 4i): the arc points at
        /// radius (a / 90)·R (R = +0x80) along the slot heading (+0x7e) and height −R·sin(a)·M (M = +0x90) around the
        /// slot centre, joined by a tube of three quads (circle of radius +8 at 0°/120°/240°/360°, 5 below, in the
        /// plane perpendicular to the step direction, FUN_00ad5710), alpha = alpha − 10i, colour by the kind (+4:
        /// 0 orange, 1 blue, 2 yellow, ≥ 3 grey); kind ≥ 3 adds a 4× wider halo (kind 3 only drawn: blue, half alpha).
        /// </summary>
        public void Render_b01cc0(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            // previous tube points (A = the a_i side, B = the a_i+1 side); zero at the start as in the original
            float pAx = 0f, pAy = 0f, pAz = 0f, pBx = 0f, pBy = 0f, pBz = 0f;
            for (int k = 0; k < 4; k++)
            {
                uint s = A + (uint)k * 0xb4;
                if (M.U8(s + 0x3e8) != 1 || M.I16(s + 0x3ea) <= 0 || M.I32(s + 0x3f4) <= 0) continue;
                if (!(M.F32(s + 0x47c) > 0f)) continue;
                int i = 0, fade = 0;
                do
                {
                    float R = M.F32(s + 0x468);
                    float a0 = M.F32(s + 0x3fc + (uint)i * 4);
                    float r0 = 0f;
                    if (a0 > 0f) r0 = (a0 / 90.0f) * R + 0f;
                    float Mh = M.F32(s + 0x478);
                    float negR = -R;
                    float yAoff = negR * FxMath.SinTableAt((short)FxOps.Cvtt(a0)) * Mh;
                    short dir = M.I16(s + 0x466);
                    float c = FxMath.CosTableAt(dir), sn = FxMath.SinTableAt(dir);
                    float zArel = sn * r0;
                    float xArel = c * r0;
                    float a1 = M.F32(s + 0x400 + (uint)i * 4);
                    float r1 = 0f;
                    if (a1 > 0f) r1 = (a1 / 90.0f) * R + 0f;
                    float xBrel = c * r1;
                    float zBrel = sn * r1;
                    float dx = xArel - xBrel;
                    float dz = zArel - zBrel;
                    float yBoff = negR * FxMath.SinTableAt((short)FxOps.Cvtt(a1)) * Mh;
                    short angle = FxMath.DirectionToAngle(dx, dz);
                    short perp = (short)(angle + 0x5a);
                    if (!(perp < 0x168)) perp = (short)(angle + 0x5a - 0x168);
                    float posX = M.F32(s + 0x46c), posY = M.F32(s + 0x470), posZ = M.F32(s + 0x474);
                    float xA = xArel + posX;
                    float yA = posY + yAoff;
                    float yB = posY + yBoff;
                    float zA = posZ + zArel;
                    float zB = posZ + zBrel;
                    float xB = xBrel + posX;
                    short kind = M.I16(s + 0x3ec);

                    for (int j = 0; j < 4; j++)
                    {
                        int th = j * 120;
                        float S = M.F32(s + 0x3f0);
                        float sy = S * FxMath.SinTableAt(th) - 5.0f;
                        float cxs = S * FxMath.CosTableAt(th);
                        float ox = cxs * FxMath.CosTableAt(perp);
                        float oz = cxs * FxMath.SinTableAt(perp);
                        float Ax = ox + xA, Bx = ox + xB;
                        float Ay = yA + sy;
                        float Bz = oz + zB, Az = oz + zA;
                        float By = yB + sy;
                        kind = M.I16(s + 0x3ec);
                        if (j > 0)
                        {
                            int alpha = M.I16(s + 0x3ea) - fade;
                            if (alpha > 0 && kind >= 0)   // negative kinds draw nothing (cmp ax, 3; jl)
                            {
                                byte cr, cg, cb;
                                if (kind == 0) { cr = 0xff; cg = 0xc8; cb = (byte)(i * 0x1e); }
                                else if (kind == 1) { cr = cg = (byte)(i * 0x16 + 10); cb = 0xff; }
                                else if (kind == 2) { cr = 0xff; cg = 0xc8; cb = (byte)(i * 0x32); }
                                else { cr = cg = cb = (byte)(i * 0x1e + 0x69); }
                                Quad_b0a900(rt, ref count, (short)alpha, Ax, Ay, Az, Bx, By, Bz, pBx, pBy, pBz, pAx, pAy, pAz,
                                    viewMatrix, 0, 0, 1, cr, cg, cb, 0f, 1.0f, 0f, 1.0f, 0);
                            }
                        }
                        pAx = Ax; pAy = Ay; pAz = Az;
                        pBx = Bx; pBy = By; pBz = Bz;
                    }
                    if (M.I16(s + 0x3ec) >= 3)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            int th = j * 120;
                            float S4 = M.F32(s + 0x3f0) * 4.0f;
                            float sy = S4 * FxMath.SinTableAt(th) - 5.0f;
                            float cxs = S4 * FxMath.CosTableAt(th);
                            float ox = cxs * FxMath.CosTableAt(perp);
                            float oz = cxs * FxMath.SinTableAt(perp);
                            float Ax = ox + xA, Bx = ox + xB;
                            float Ay = yA + sy;
                            float Bz = oz + zB, Az = oz + zA;
                            float By = yB + sy;
                            if (j > 0)
                            {
                                int alpha = FxOps.Cvtt((float)(M.I16(s + 0x3ea) - fade) * 0.5f);
                                if (alpha > 0 && M.I16(s + 0x3ec) == 3)
                                    Quad_b0a900(rt, ref count, (short)alpha, Ax, Ay, Az, Bx, By, Bz, pBx, pBy, pBz, pAx, pAy, pAz,
                                        viewMatrix, 0, 0, 1, 0, 0, 0xff, 0f, 1.0f, 0f, 1.0f, 0);
                            }
                            pAx = Ax; pAy = Ay; pAz = Az;
                            pBx = Bx; pBy = By; pBz = Bz;
                        }
                    }
                    i++;
                    fade += 10;
                } while (M.F32(s + 0x47c) > (float)i);
            }
        }

        // ---- type 0x74 -----------------------------------------------------------------------------------------

        [FxPrimProcess(0x74)]
        public FxCaseResult Case74(FxRuntime rt) { Process_ae9cb0(rt); return FxCaseResult.Continue; }

        [FxPrimRender(0x74)]
        public void Render74(FxRuntime rt, uint viewMatrix) => Render_afaaf0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_ae9cb0 (0xae9cb0) — Shield Chain shield: camera angles, follow the master; while block 0 is
        /// enabled: life++; every third tick the trail shifts one block (block 2 → 3 from tick 6, 1 → 2 from tick 3,
        /// 0 → 1 from tick 2: size, spin, position, texture byte, alpha − 25 / − 150), the spin (+6) turns −15° per
        /// tick, the alpha rises 45 per tick up to 255 for 5 ticks and falls 10 per tick from tick 15; the shield
        /// moves f_400 along (cos, sin) of its heading f_3f8.
        /// </summary>
        public void Process_ae9cb0(FxRuntime rt)
        {
            CameraAngles_prims2(rt);
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            if (M.U8(A + 0x3e8) != 1) return;
            int life = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, life);
            if (life <= 0) return;
            if (life % 3 == 0)
            {
                if (life > 3)
                {
                    M.U16(A + 0x60a, M.U16(A + 0x556));
                    M.U32(A + 0x684, M.U32(A + 0x5d0));
                    M.U32(A + 0x638, M.U32(A + 0x584));
                    short a = (short)(M.I16(A + 0x552) - 0x19);
                    M.I16(A + 0x606, a);
                    if (a < 0) M.I16(A + 0x606, 0);
                    M.U32(A + 0x688, M.U32(A + 0x5d4));
                    M.U32(A + 0x68c, M.U32(A + 0x5d8));
                    M.U32(A + 0x690, M.U32(A + 0x5dc));
                    M.U16(A + 0x60a, M.U16(A + 0x556));
                    M.U8(A + 0x66c, M.U8(A + 0x5b8));
                }
                if (life > 2)
                {
                    M.U16(A + 0x556, M.U16(A + 0x4a2));
                    M.U32(A + 0x5d0, M.U32(A + 0x51c));
                    M.U32(A + 0x584, M.U32(A + 0x4d0));
                    short a = (short)(M.I16(A + 0x49e) - 0x19);
                    M.I16(A + 0x552, a);
                    if (a < 0) M.I16(A + 0x552, 0);
                    M.U32(A + 0x5d4, M.U32(A + 0x520));
                    M.U32(A + 0x5d8, M.U32(A + 0x524));
                    M.U32(A + 0x5dc, M.U32(A + 0x528));
                    M.U16(A + 0x556, M.U16(A + 0x4a2));
                    M.U8(A + 0x5b8, M.U8(A + 0x504));
                    life = M.I32(A + 0x3f4);
                }
                if (life > 1)
                {
                    M.U16(A + 0x4a2, M.U16(A + 0x3ee));
                    M.U32(A + 0x51c, M.U32(A + 0x468));
                    M.U32(A + 0x4d0, M.U32(A + 0x41c));
                    short a = (short)(M.I16(A + 0x3ea) - 0x96);
                    M.I16(A + 0x49e, a);
                    if (a < 0) M.I16(A + 0x49e, 0);
                    M.U32(A + 0x520, M.U32(A + 0x46c));
                    M.U32(A + 0x524, M.U32(A + 0x470));
                    M.U32(A + 0x528, M.U32(A + 0x474));
                    M.U16(A + 0x4a2, M.U16(A + 0x3ee));
                    M.U8(A + 0x504, M.U8(A + 0x450));
                    life = M.I32(A + 0x3f4);
                }
            }
            short spin = M.I16(A + 0x3ee);
            short ns = (short)(spin - 0xf);
            M.I16(A + 0x3ee, ns);
            if (ns < 0) M.I16(A + 0x3ee, (short)(ns + 0x168));
            if (life <= 5)
            {
                M.I16(A + 0x3ea, (short)(M.I16(A + 0x3ea) + 0x2d));
                if (M.I16(A + 0x3ea) > 0xff) M.I16(A + 0x3ea, 0xff);
            }
            if (life >= 0xf)
            {
                short a = (short)(M.I16(A + 0x3ea) - 10);
                M.I16(A + 0x3ea, a);
                if (a < 0) M.I16(A + 0x3ea, 0);
            }
            float speed = M.F32(A + 0x400);
            short h = M.I16(A + 0x3f8);
            M.F32(A + 0x46c, speed * FxMath.CosTableAt(h) + M.F32(A + 0x46c));
            M.F32(A + 0x474, speed * FxMath.SinTableAt(h) + M.F32(A + 0x474));
        }

        // ---- type 0x75 (process only; the render is type 9's fn_af2870) ------------------------------------------

        /// <summary>
        /// CEffectPrim::OnProcess case 0x75 (inline, 0xad5e91) — Gravitation Field debris: like type 9 the position is
        /// master + offset + the matrix-driven accumulated velocity, but the speed/acceleration (+0x2bc/+0x2c0) are
        /// reset to (+0x29c, +0x2a0) when age % 6 == 2 (or at age 550) and to (1.18, 0.01) when age % 6 == 5
        /// (age &lt; 550); then fn_aed050 (alpha keyframes / fade / ARGB).
        /// </summary>
        [FxPrimProcess(0x75)]
        public FxCaseResult Case75(FxRuntime rt)
        {
            uint master = m_master_ptr;
            f_37c = M.F32(master + 0x10) + f_394;
            f_380 = M.F32(master + 0x14) + f_398;
            f_384 = M.F32(master + 0x18) + f_39c;
            int t = f_1e0;
            if (t < 0x226)
            {
                int r = t % 6;
                if (r == 2)
                {
                    M.U32(A + 0x2bc, M.U32(A + 0x29c));
                    M.U32(A + 0x2c0, M.U32(A + 0x2a0));
                }
                else if (r == 5)
                {
                    M.U32(A + 0x2bc, 0x3f970a3d);   // 1.18
                    M.U32(A + 0x2c0, 0x3c23d70a);   // 0.01
                }
            }
            else if (t == 0x226)
            {
                M.U32(A + 0x2bc, M.U32(A + 0x29c));
                M.U32(A + 0x2c0, M.U32(A + 0x2a0));
            }
            float v = f_2c0 + f_2bc;
            f_2bc = v;
            f_370 = f_16c * 0.0f + f_160 * 0.0f + v * f_178 + f_184;
            f_374 = f_170 * 0.0f + f_164 * 0.0f + v * f_17c + f_188;
            f_378 = f_174 * 0.0f + f_168 * 0.0f + v * f_180 + f_18c;
            f_388 = f_370 + f_388;
            float y = f_374 + f_38c;
            f_38c = y;
            float z = f_378 + f_390;
            f_390 = z;
            m_pos_x = f_37c + f_388;
            m_pos_y = y + f_380;
            m_pos_z = z + f_384;
            UpdateAlpha_aed050();
            return FxCaseResult.Continue;
        }
    }
}
