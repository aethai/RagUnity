// Synced from reference/port/src/RoClassicFx/Prim/effects-10/Prim.Effects10.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>effects-10 prim types (process and render cases of CEffectPrim::OnProcess 0xad5a30 / Render 0xaf4ce0).</summary>
    public readonly partial struct Prim
    {
        // ================================================================ type 0x43 (Chase Walk footprint)

        /// <summary>
        /// Type 0x43 — inline OnProcess case 0xad9faf: position = the master effect's; age +0x3f4 +1; while the age is
        /// positive, odd ages fade the alpha +0x3ea by one (clamped at 0).
        /// </summary>
        [FxPrimProcess(0x43)]
        public FxCaseResult Case43_E10(FxRuntime rt)
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            int age = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, age);
            if (age <= 0) return FxCaseResult.Continue;
            if ((age & 1) != 0) M.U16(A + 0x3ea, (ushort)(M.U16(A + 0x3ea) - 1));
            if (M.I16(A + 0x3ea) < 0) M.U16(A + 0x3ea, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x43 render: fn_aee0b0 (prims-3, the flat ground quad of types 0x27 / 0x58).</summary>
        [FxPrimRender(0x43)]
        public void Render43_E10(FxRuntime rt, uint viewMatrix) => Render_aee0b0(rt, viewMatrix);

        // ================================================================ type 0x84 (song aura rings, EF 861..877)

        /// <summary>
        /// Type 0x84 — inline OnProcess case 0xad8eaa (the prologue already followed the master): position = the master
        /// effect's; per live slot (4 × 0xb4 from +0x3e8) with a positive age after +1: phase +0x3ee +1 (wraps at 360),
        /// radius +0x3f0 ·= 1.02; once it passes 20 the alpha +0x3ea drops by 3 and, at 0 or below, the ring restarts
        /// (radius 10, new random angle +0x3f8, alpha 200 — or 100 for schemes ≥ 2, which retire the slot after age
        /// 300); +0x404 = radius · sin[phase] · 0.1 + radius · 0.9, clamped at 0.
        /// </summary>
        [FxPrimProcess(0x84)]
        public FxCaseResult Case84_E10(FxRuntime rt)
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            for (int k = 0; k < 4; k++)
            {
                uint s = A + 0x3ee + (uint)k * 0xb4;
                if (M.U8(s - 6) != 1) continue;
                int age = M.I32(s + 6) + 1;
                M.I32(s + 6, age);
                if (age <= 0) continue;
                ushort ph = (ushort)(M.U16(s) + 1);
                M.U16(s, ph);
                if ((short)ph >= 0x168)
                {
                    ph = (ushort)(ph - 0x168);
                    M.U16(s, ph);
                }
                int idx = (short)ph;
                float r = M.F32(s + 2) * 1.02f;
                M.F32(s + 2, r);
                if (r > 20.0f)   // COMISS; JBE skips when ≤ or unordered
                {
                    short a = (short)(M.U16(s - 4) + 0xfffd);
                    M.I16(s - 4, a);
                    if (a <= 0)
                    {
                        float scheme = M.F32(s + 0x22);
                        if (scheme < 2.0f || float.IsNaN(scheme))   // COMISS; JB
                        {
                            M.F32(s + 2, 10.0f);
                            int v = rt.Rand.Next() % 0x168;
                            M.U16(s + 0xa, (ushort)(v < 0 ? 0 : v));
                            M.U16(s - 4, 200);
                        }
                        else if (M.I32(s + 6) > 300)
                        {
                            M.U16(s - 4, 0);
                            M.U8(s - 6, 0);
                        }
                        else
                        {
                            M.F32(s + 2, 10.0f);
                            int v = rt.Rand.Next() % 0x168;
                            M.U16(s + 0xa, (ushort)(v < 0 ? 0 : v));
                            M.U16(s - 4, 100);
                        }
                    }
                }
                float rr = M.F32(s + 2);
                float y = ((rr * SinAt(idx)) * 0.1f) + (rr * 0.9f);
                M.F32(s + 0x16, y);
                if (0f > y) M.F32(s + 0x16, 0f);
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x84)]
        public void Render84_E10(FxRuntime rt, uint viewMatrix) => Render_afa520(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_afa520 (0xafa520) — per live slot (alpha &gt; 0, age &gt; 0): a vertical band of height r
        /// (r = +0x404) from y = r/2 − 10 down to r/2 − 10 − r between (r/2·cos a, r/2·sin a) and the opposite point
        /// (a ± 180, a = +0x3f8), the corners also stored at +0x46c.. / +0x484..; colour from the scheme +0x410 and the
        /// slot index +0x408 (bytes carried over between slots like the original stack locals), mode 1 for schemes
        /// 2..8, else 0; skipped when r ≤ 0.
        /// </summary>
        public void Render_afa520(FxRuntime rt, uint matrix)
        {
            int count = 0;
            // the original's colour locals ([ebp-8], [ebp-4] uninitialised; blue reuses the matrix argument's slot)
            byte cr = 0, cg = 0, cb = (byte)matrix;
            for (int k = 0; k < 4; k++)
            {
                uint s = A + 0x3f4 + (uint)k * 0xb4;
                short alpha = M.I16(s - 0xa);
                if (M.U8(s - 0xc) != 1 || alpha <= 0 || M.I32(s) <= 0) continue;
                float r = M.F32(s + 0x10);
                int a = M.I16(s + 4);
                float h = r * 0.5f;
                int b = (a < 0xb4 ? 0xb4 : -0xb4) + a;
                float sn = SinAt(a), cs = CosAt(a);
                float zero = 0f;
                float x1 = (h * cs) - (sn * zero);
                float z1 = (h * sn) + (cs * zero);
                M.F32(s + 0x90, x1);
                M.F32(s + 0x98, z1);
                float y = h - M.F32(s + 0x18);
                M.F32(s + 0x94, y);
                float sn2 = SinAt(b), cs2 = CosAt(b);
                float x2 = (h * cs2) - (sn2 * 0.0f);
                float z2 = (h * sn2) + (cs2 * 0.0f);
                M.F32(s + 0x7c, y);
                float yb = y - r;
                M.F32(s + 0x78, x2);
                M.F32(s + 0x80, z2);

                float type = M.F32(s + 0x1c), idx = M.F32(s + 0x14);
                int sel = idx == 0f ? 0 : idx == 1.0f ? 1 : idx == 2.0f ? 2 : idx == 3.0f ? 3 : -1;
                byte v = sel switch { 0 => 0x69, 1 => 5, 2 => 0x37, 3 => 0x9b, _ => 0 };
                byte mode = 1;
                if (type == 2.0f) { if (sel >= 0) { cr = v; cb = v; cg = 0xff; } }
                else if (type == 3.0f) { if (sel >= 0) { cr = v; cg = v; cb = 0xff; } }
                else if (type == 4.0f) { if (sel >= 0) { cb = v; cr = 0xff; cg = 0xff; } }
                else if (type == 5.0f) { if (sel >= 0) { cg = v; cb = v; cr = 0xff; } }
                else if (type == 6.0f) { if (sel >= 0) { cg = v; cb = 0xff; cr = 0xff; } }
                else if (type == 7.0f) { if (sel >= 0) { cr = v; cb = 0xff; cg = 0xff; } }
                else if (type == 8.0f)
                {
                    if (sel == 3) { cr = 0xff; cb = 0xff; cg = 0xff; }
                    else if (sel >= 0) { cr = v; cg = v; cb = v; }
                }
                else
                {
                    mode = 0;
                    if (sel >= 0) { cr = v; cg = v; cb = 0xff; }
                }
                if (!(r > 0f)) continue;   // COMISS; JBE
                Quad_b0a900(rt, ref count, alpha, M.F32(s + 0x90), M.F32(s + 0x94), M.F32(s + 0x98),
                    M.F32(s + 0x78), M.F32(s + 0x7c), M.F32(s + 0x80), x2, yb, z2, x1, yb, z1,
                    matrix, 0, 0, 0, cr, cg, cb, 0f, 1.0f, 0f, 1.0f, mode);
            }
        }

        // ================================================================ type 0x91 (spirit sphere, EF 833)

        /// <summary>Type 0x91: fn_b0f390 — always returns 1 (AL) → alive while the age allows.</summary>
        [FxPrimProcess(0x91)]
        public FxCaseResult Case91_E10(FxRuntime rt) => Process_b0f390(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        [FxPrimRender(0x91)]
        public void Render91_E10(FxRuntime rt, uint viewMatrix) => Render_b15600(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b0f390 (0xb0f390) — camera angles, follow the master; slot 0 live (+0x3e8 == 1): age +0x3f4 +1
        /// (alpha +0x3ea +1 while the age ≤ 200); three wandering angles (+0x3fc/+0x404/+0x40c) step by 1 towards their
        /// targets (+0x400/+0x408/+0x410), clamped to 0..359, picking a new random target on arrival; every even age the
        /// orbit angle +0x414 advances by 1 (wrap at 360); centre +0x46c/+0x470/+0x474 = sin of the three angles (halved
        /// when +0x450 != 0) plus a radius-7 circle of +0x414 (x, z) and +0x418 (y); spin +0x41c += 5 + rand() % 5 (wrap
        /// at 360); then the four trail rings of type 0x84's scheme (slot 0 without the age step, radius ·1.02 past 6 →
        /// alpha +0x466 −3, restart at 3 / alpha 200), +0x42c = radius · sin[phase] · 0.1 + radius · 0.9 (clamped at 0).
        /// Returns 1.
        /// </summary>
        public bool Process_b0f390(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            if (M.U8(A + 0x3e8) != 1) return true;
            int age = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, age);
            if (age <= 0xc8) M.U16(A + 0x3ea, (ushort)(M.U16(A + 0x3ea) + 1));
            for (int k = 0; k < 3; k++)
            {
                uint s = A + 0x3fc + (uint)k * 8;
                float target = M.F32(s + 4), cur = M.F32(s);
                if (target > cur)
                {
                    cur = cur + 1.0f;
                    if (cur > 359.0f) cur = 359.0f;
                    M.F32(s, cur);
                    if (cur > target)
                    {
                        int n = FxOps.Cvtt(cur);
                        int v = 0;
                        if (n != 0)
                        {
                            int r = rt.Rand.Next() % n;
                            v = r < 0 ? 0 : r;
                        }
                        M.F32(s + 4, (float)v);
                    }
                }
                else
                {
                    cur = cur - 1.0f;
                    if (0f > cur) cur = 0f;
                    M.F32(s, cur);
                    if (!(target < cur || float.IsNaN(target) || float.IsNaN(cur)))   // COMISS target, cur; JB
                    {
                        int n = FxOps.Cvtt(cur);
                        int v = 0;
                        if (n != 0)
                        {
                            int r = rt.Rand.Next() % n;
                            v = r < 0 ? 0 : r;
                        }
                        M.F32(s + 4, (float)(0x167 - v));
                    }
                }
            }
            if ((M.U8(A + 0x3f4) & 1) == 0)
            {
                float o = M.F32(A + 0x414) + 1.0f;
                M.F32(A + 0x414, o);
                if (!(o < 360.0f || float.IsNaN(o))) M.F32(A + 0x414, o - 360.0f);
            }
            float sx = SinAt(FxOps.Cvtt(M.F32(A + 0x3fc)));
            float sz, sy;
            if (M.U8(A + 0x450) == 0)
            {
                M.F32(A + 0x46c, sx);
                sz = SinAt(FxOps.Cvtt(M.F32(A + 0x404)));
                M.F32(A + 0x474, sz);
                sy = SinAt(FxOps.Cvtt(M.F32(A + 0x40c)));
            }
            else
            {
                sx = sx * 0.5f;
                M.F32(A + 0x46c, sx);
                sz = SinAt(FxOps.Cvtt(M.F32(A + 0x404))) * 0.5f;
                M.F32(A + 0x474, sz);
                sy = SinAt(FxOps.Cvtt(M.F32(A + 0x40c))) * 0.5f;
            }
            M.F32(A + 0x470, sy);
            int orbit = FxOps.Cvtt(M.F32(A + 0x414));
            M.F32(A + 0x46c, (SinAt(orbit) * 7.0f) + sx);
            M.F32(A + 0x474, (CosAt(orbit) * 7.0f) + sz);
            M.F32(A + 0x470, M.F32(A + 0x418) + M.F32(A + 0x470));
            int rs = rt.Rand.Next() % 5;
            float spin = (float)((rs < 0 ? 0 : rs) + 5) + M.F32(A + 0x41c);
            M.F32(A + 0x41c, spin);
            if (!(spin < 360.0f || float.IsNaN(spin))) M.F32(A + 0x41c, spin - 360.0f);

            for (int k = 0; k < 4; k++)
            {
                uint s = A + 0x3ee + (uint)k * 0xb4;
                if (k != 0) M.I32(s + 6, M.I32(s + 6) + 1);
                if (M.I32(s + 6) <= 0) continue;
                ushort ph = (ushort)(M.U16(s) + 1);
                M.U16(s, ph);
                if ((short)ph >= 0x168)
                {
                    ph = (ushort)(ph - 0x168);
                    M.U16(s, ph);
                }
                float r = M.F32(s + 2) * 1.02f;
                M.F32(s + 2, r);
                if (r > 6.0f)
                {
                    short a = (short)(M.U16(s + 0x78) + 0xfffd);
                    M.I16(s + 0x78, a);
                    if (a <= 0)
                    {
                        M.F32(s + 2, 3.0f);
                        int v = rt.Rand.Next() % 0x168;
                        r = M.F32(s + 2);
                        M.U16(s + 0xa, (ushort)(v < 0 ? 0 : v));
                        M.U16(s + 0x78, 200);
                    }
                }
                float y = ((r * SinAt((short)ph)) * 0.1f) + (r * 0.9f);
                M.F32(s + 0x3e, y);
                if (0f > y) M.U32(s + 0x3e, 0);
            }
            return true;
        }

        /// <summary>
        /// CEffectPrim::fn_b15600 (0xb15600) — slot 0 live with alpha &gt; 0: two camera-facing squares at the centre
        /// (+0x46c..) — radius R = f_468·sin[+0x41c]·0.05 + f_468 in (0x44, 0x2a, 0xff), then f_468·sin·0.05 + f_468·0.5
        /// in (0x9b, 0x9b, 0xff), corners at 0/90/180/270° rotated by FUN_00b0d9c0 — then per slot a vertical band of
        /// height r (+0x42c) between the angle +0x3f8 and its opposite (texture 1, colour 0x69 / 0x23 / 0x4b / 5 per
        /// slot, alpha +0x466), corners also stored at +0x478.. / +0x490.., drawn when r &gt; 0.
        /// </summary>
        public void Render_b15600(FxRuntime rt, uint matrix)
        {
            int count = 0;
            if (M.U8(A + 0x3e8) != 1 || M.I16(A + 0x3ea) <= 0) return;
            System.Span<float> p = stackalloc float[12];
            p.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                int t = FxOps.Cvtt(M.F32(A + 0x41c));
                float size = M.F32(A + 0x468);
                float R = pass == 0
                    ? ((size * SinAt(t)) * 0.05f) + size
                    : ((size * SinAt(t)) * 0.05f) + (size * 0.5f);
                p[0] = R * CosAt(0); p[1] = 0f; p[2] = R * SinAt(0);
                p[3] = R * CosAt(90); p[4] = 0f; p[5] = R * SinAt(90);
                p[6] = R * CosAt(180); p[7] = 0f; p[8] = R * SinAt(180);
                p[9] = R * CosAt(270); p[10] = 0f; p[11] = R * SinAt(270);
                Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                float cx = M.F32(A + 0x46c), cy = M.F32(A + 0x470), cz = M.F32(A + 0x474);
                for (int k = 0; k < 4; k++)
                {
                    p[k * 3] = cx + p[k * 3];
                    p[k * 3 + 1] = cy + p[k * 3 + 1];
                    p[k * 3 + 2] = cz + p[k * 3 + 2];
                }
                byte r = pass == 0 ? (byte)0x44 : (byte)0x9b, g = pass == 0 ? (byte)0x2a : (byte)0x9b;
                Quad_b0a900(rt, ref count, M.I16(A + 0x3ea), p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 0, 0, 0, r, g, 0xff, 0f, 1.0f, 0f, 1.0f, 0);
            }

            for (int k = 0; k < 4; k++)
            {
                uint d = A + (uint)k * 0xb4;
                float h = M.F32(d + 0x42c) * 0.5f;
                int a = M.I16(d + 0x3f8);
                int b = (a < 0xb4 ? 0xb4 : -0xb4) + a;
                float cs = CosAt(a), sn = SinAt(a);
                M.F32(d + 0x494, h);
                M.F32(d + 0x498, (sn * h) + (cs * 0f));
                float x1 = (cs * h) - (sn * 0f);
                M.F32(d + 0x490, x1);
                float cs2 = CosAt(b), sn2 = SinAt(b);
                M.F32(d + 0x47c, h);
                M.F32(d + 0x478, (cs2 * h) - (sn2 * 0f));
                M.F32(d + 0x480, (sn2 * h) + (cs2 * 0f));
                M.F32(d + 0x490, x1 + M.F32(A + 0x46c));
                M.F32(d + 0x494, M.F32(A + 0x470) + M.F32(d + 0x494));
                M.F32(d + 0x498, M.F32(A + 0x474) + M.F32(d + 0x498));
                float x2 = M.F32(A + 0x46c) + M.F32(d + 0x478);
                M.F32(d + 0x478, x2);
                float y2 = M.F32(A + 0x470) + M.F32(d + 0x47c);
                M.F32(d + 0x47c, y2);
                float z2 = M.F32(A + 0x474) + M.F32(d + 0x480);
                M.F32(d + 0x480, z2);
                float r = M.F32(d + 0x42c);
                float y2b = y2 - r;
                float y1b = M.F32(d + 0x494) - r;
                byte col = k == 0 ? (byte)0x69 : k == 1 ? (byte)0x23 : k == 2 ? (byte)0x4b : (byte)5;
                if (!(r > 0f)) continue;   // COMISS; JBE
                Quad_b0a900(rt, ref count, M.I16(d + 0x466), M.F32(d + 0x490), M.F32(d + 0x494), M.F32(d + 0x498),
                    x2, y2, z2, x2, y2b, z2, M.F32(d + 0x490), y1b, M.F32(d + 0x498),
                    matrix, 0, 1, 0, col, col, 0xff, 0f, 1.0f, 0f, 1.0f, 0);
            }
        }

        // ================================================================ type 0x93 (kiss rings, EF 879)

        [FxPrimProcess(0x93)]
        public FxCaseResult Case93_E10(FxRuntime rt) { Process_b0eaa0(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x93 render: fn_af6810 (prims-3, the ten concentric rings of type 0x44).</summary>
        [FxPrimRender(0x93)]
        public void Render93_E10(FxRuntime rt, uint viewMatrix) => Render_af6810(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b0eaa0 (0xb0eaa0) — camera angles; follows the master when the style +0x454 is 1; per live
        /// slot with a positive age after +1: ages &lt; 20 grow the ten ring radii +0x400..+0x424 by 10 up to
        /// 70, 90 .. 250; later they shrink by 4 (floor 0), and styles 6 / 7 past age 90 also shrink the last radius
        /// by 5 while the size +0x468 grows ·1.01; ages &lt; 50 shrink the size by 0.2. Returns 1.
        /// </summary>
        public void Process_b0eaa0(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            if (M.U8(A + 0x454) == 1) FollowMaster_P1();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x404 + (uint)k * 0xb4;
                if (M.U8(c - 0x1c) != 1) continue;
                int age = M.I32(c - 0x10) + 1;
                M.I32(c - 0x10, age);
                if (age <= 0) continue;
                if (age < 0x14)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        uint f = c - 4 + (uint)i * 4;
                        float v = M.F32(f) + 10.0f;
                        M.F32(f, v);
                        float lim = 70.0f + 20.0f * i;
                        if (!(v < lim || float.IsNaN(v))) M.F32(f, lim);   // COMISS; JB
                    }
                }
                else
                {
                    for (int i = 0; i < 9; i++)
                    {
                        uint f = c - 4 + (uint)i * 4;
                        float v = M.F32(f) - 4.0f;
                        M.F32(f, v);
                        if (0f > v) M.U32(f, 0);
                    }
                    byte style = M.U8(c + 0x50);
                    if ((style == 6 || style == 7) && M.I32(c - 0x10) > 0x5a)
                    {
                        float v = M.F32(c + 0x20) - 5.0f;
                        M.F32(c + 0x20, v);
                        if (v > 0f) M.F32(c + 0x64, M.F32(c + 0x64) * 1.01f);
                        if (0f > v) M.U32(c + 0x20, 0);
                    }
                }
                if (M.I32(c - 0x10) < 0x32) M.F32(c + 0x64, M.F32(c + 0x64) - 0.2f);
            }
        }

        // ================================================================ type 0x9f (banding sparks, EF 909)

        [FxPrimProcess(0x9f)]
        public FxCaseResult Case9F_E10(FxRuntime rt) { Process_b10530(rt); return FxCaseResult.Continue; }

        [FxPrimRender(0x9f)]
        public void Render9F_E10(FxRuntime rt, uint viewMatrix) => Render_b19b50(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b10530 (0xb10530) — follows the master; per live slot with a positive age after +1: alpha +5
        /// (max 255) while the age &lt; 20; state +0x450: 0 flies in until the distance of (+0x46c, +0x470, +0x474) drops
        /// below 12 (FUN_00534e70), 1 spins the phase +0x466 by 9 and counts +0x451 to 10 (then state 2) fading the
        /// alpha by 2 and the size +0x468 ·0.99, 2 turns +0x466 by −3 and +0x3f8 by +1 and after age 300 fades by 1;
        /// then the point moves by −speed·(cos p·cos t, cos p·sin t, sin p) (speed +0x400, p = +0x466, t = +0x3f8).
        /// </summary>
        public void Process_b10530(FxRuntime rt)
        {
            FollowMaster_P1();
            for (int k = 0; k < 4; k++)
            {
                uint s = A + 0x474 + (uint)k * 0xb4;
                if (M.U8(s - 0x8c) != 1) continue;
                int age = M.I32(s - 0x80) + 1;
                M.I32(s - 0x80, age);
                if (age <= 0) continue;
                if (age < 0x14)
                {
                    short al = (short)(M.U16(s - 0x8a) + 5);
                    M.I16(s - 0x8a, al);
                    if (al > 0xff) M.I16(s - 0x8a, 0xff);
                }
                float px = M.F32(s - 8), py = M.F32(s - 4), pz = M.F32(s);
                float dist = (float)System.Math.Sqrt((double)(((px * px) + (py * py)) + (pz * pz)));
                byte st = M.U8(s - 0x24);
                bool move = true;
                if (st == 0)
                {
                    if (12.0f > dist)   // COMISS 12, dist; JBE skips
                    {
                        M.U16(s - 0x24, 1);
                        st = 1;
                    }
                }
                if (st == 1)
                {
                    ushort ph = (ushort)(M.U16(s - 0xe) + 9);
                    M.U16(s - 0xe, ph);
                    if ((short)ph >= 0x168) M.U16(s - 0xe, (ushort)(ph - 0x168));
                    if (M.I16(s - 0x7c) >= 0x168) M.U16(s - 0x7c, (ushort)(M.U16(s - 0x7c) - 0x168));
                    sbyte n = (sbyte)(M.U8(s - 0x23) + 1);
                    M.I8(s - 0x23, n);
                    if (n >= 10)
                    {
                        M.U8(s - 0x24, 2);
                        st = 2;
                    }
                    short al = (short)(M.U16(s - 0x8a) + 0xfffe);
                    M.I16(s - 0x8a, al);
                    M.F32(s - 0xc, M.F32(s - 0xc) * 0.99f);
                    if (al < 0) M.U16(s - 0x8a, 0);
                }
                if (st == 2)
                {
                    short ph = (short)(M.U16(s - 0xe) - 3);
                    M.I16(s - 0xe, ph);
                    if (ph < 0)
                    {
                        ph = (short)(ph + 0x168);
                        M.I16(s - 0xe, ph);
                    }
                    if (ph >= 0x168) M.I16(s - 0xe, (short)(ph - 0x168));
                    short t = (short)(M.U16(s - 0x7c) + 1);
                    M.I16(s - 0x7c, t);
                    if (t < 0)
                    {
                        t = (short)(t + 0x168);
                        M.I16(s - 0x7c, t);
                    }
                    if (t >= 0x168) M.I16(s - 0x7c, (short)(t - 0x168));
                    if (M.I32(s - 0x80) > 300)
                    {
                        short al = (short)(M.U16(s - 0x8a) + 0xffff);
                        M.I16(s - 0x8a, al);
                        M.F32(s - 0xc, M.F32(s - 0xc) * 0.99f);
                        if (al < 0) M.U16(s - 0x8a, 0);
                    }
                }
                if (!move) continue;
                int p = M.I16(s - 0xe);
                float sc = M.F32(s - 0x74) * CosAt(p);
                float sp = SinAt(p);
                int tt = M.I16(s - 0x7c);
                float dx = sc * CosAt(tt);
                float dy = SinAt(tt) * sc;
                M.F32(s - 8, M.F32(s - 8) - dx);
                dy = dy + 0f;
                M.F32(s - 4, M.F32(s - 4) - dy);
                sp = sp * M.F32(s - 0x74);
                M.F32(s, M.F32(s) - sp);
            }
        }

        /// <summary>
        /// CEffectPrim::fn_b19b50 (0xb19b50) — per live slot with alpha &gt; 0: two small camera-independent quads on a
        /// sphere of radius r (+0x468) around the point (+0x46c, +0x470 + y offset +0x3fc, +0x474), tilted by +0x3f8,
        /// corners at the phase +0x466 ± 2 / ± 178 (first) and ± 8 / ± 172 (second); colours by +0x452 (0: (0, fa, fa) /
        /// (fa, 0, 0), 1: white-ish / yellow, 2: (fa, 64, 64) / (ff, a6, d2), 3: (c8, c8, ff) / (5, 5, ff); other
        /// selectors draw nothing).
        /// </summary>
        public void Render_b19b50(FxRuntime rt, uint matrix)
        {
            int count = 0;
            for (int k = 0; k < 4; k++)
            {
                uint s = A + 0x474 + (uint)k * 0xb4;
                short alpha = M.I16(s - 0x8a);
                if (M.U8(s - 0x8c) != 1 || alpha <= 0) continue;
                int ph = M.I16(s - 0xe);
                byte sel = M.U8(s - 0x22);
                byte r, g, b;
                if (sel <= 3)
                {
                    int i1 = ph >= 2 ? ph - 2 : ph + 0x166;
                    int i2 = ph < 0x166 ? ph + 2 : ph - 0x166;
                    int i3 = ph >= 0xb6 ? ph - 0xb6 : ph + 0xb2;
                    int i4 = ph >= 0xb2 ? ph - 0xb2 : ph + 0xb6;
                    if (sel == 0) { r = 0; g = 0xfa; b = 0xfa; }
                    else if (sel == 1) { r = 0xfa; g = 0xfa; b = 0xfa; }
                    else if (sel == 2) { r = 0xfa; g = 0x64; b = 0x64; }
                    else { r = 0xc8; g = 0xc8; b = 0xff; }
                    SphereQuad_b19b50(rt, ref count, s, alpha, matrix, i1, i2, i3, i4, r, g, b);
                }
                ph = M.I16(s - 0xe);
                if (sel > 3) continue;
                int j1 = ph >= 8 ? ph - 8 : ph + 0x160;
                int j2 = ph < 0x160 ? ph + 8 : ph - 0x160;
                int j3 = ph >= 0xbc ? ph - 0xbc : ph + 0xac;
                int j4 = ph < 0xac ? ph + 0xbc : ph - 0xac;
                if (sel == 0) { r = 0xfa; g = 0; b = 0; }
                else if (sel == 1) { r = 0xfa; g = 0xfa; b = 0; }
                else if (sel == 2) { r = 0xff; g = 0xa6; b = 0xd2; }
                else { r = 5; g = 5; b = 0xff; }
                SphereQuad_b19b50(rt, ref count, s, M.I16(s - 0x8a), matrix, j1, j2, j3, j4, r, g, b);
            }
        }

        /// <summary>One quad of fn_b19b50: corner q = (r·cos[i]·cos t + x, (r·cos[i]·sin t + yoff) + y, r·sin[i] + z).</summary>
        void SphereQuad_b19b50(FxRuntime rt, ref int count, uint s, short alpha, uint matrix, int i1, int i2, int i3, int i4,
            byte r, byte g, byte b)
        {
            float size = M.F32(s - 0xc), yoff = M.F32(s - 0x78);
            int t = M.I16(s - 0x7c);
            float ct = CosAt(t), st = SinAt(t);
            float x = M.F32(s - 8), y = M.F32(s - 4), z = M.F32(s);
            System.Span<float> p = stackalloc float[12];
            p.Clear();
            System.Span<int> idx = stackalloc int[] { i1, i2, i3, i4 };
            for (int q = 0; q < 4; q++)
            {
                float rc = size * CosAt(idx[q]);
                p[q * 3] = (rc * ct) + x;
                p[q * 3 + 1] = ((rc * st) + yoff) + y;
                p[q * 3 + 2] = (size * SinAt(idx[q])) + z;
            }
            Quad_b0a900(rt, ref count, alpha, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                matrix, 0, 0, 0, r, g, b, 0f, 1.0f, 0f, 1.0f, 0);
        }

        // ================================================================ type 0x55 (angel sprites, EF 338 / 582)

        [FxPrimProcess(0x55)]
        public FxCaseResult Case55_E10(FxRuntime rt) { Process_aeaec0(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x55 render: fn_af24e0 (prims-sprites, the sprite trail renderer).</summary>
        [FxPrimRender(0x55)]
        public void Render55_E10(FxRuntime rt, uint viewMatrix) => Render_af24e0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_aeaec0 (0xaeaec0) — slot 0 live: age +1, rise (+0x470 −= 0.1), the draw position +0x388..
        /// (and +0x484..) = +0x46c/+0x470/+0x474; before age 50 the alpha f_324 fades in by kind +0x450 (counter
        /// +0x451 != 0: +1 up to 40 − counter; 1: +5 to 200; 10: +10 to 255; 11: 0; 0 / negative: +5 to 245; 2..9 /
        /// 12..: +5 to 150 while the age &lt; 30); after age 30 the ACT frame +0x3c steps every 3rd tick up to 29
        /// (kinds 10 / 11: every 4th up to 15); the fade-out starts at age 100 / 30 / 60 / 70 / 100 / 75 (kinds 0 / 2
        /// / 3 / 4 / 10 / others) by 4 / 3 / 3 / 3 / 6 / (counter ? 3 : 6); finally m_pos = +0x388.. and the trail
        /// snapshot fn_b0d680. Returns 1.
        /// </summary>
        public void Process_aeaec0(FxRuntime rt)
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            if (M.U8(A + 0x3e8) == 1)
            {
                M.I32(A + 0x3f4, M.I32(A + 0x3f4) + 1);
                M.F32(A + 0x470, M.F32(A + 0x470) - 0.1f);
                float x = M.F32(A + 0x46c);
                M.F32(A + 0x484, x);
                M.U32(A + 0x488, M.U32(A + 0x470));
                M.U32(A + 0x48c, M.U32(A + 0x474));
                M.F32(A + 0x388, x);
                M.U32(A + 0x38c, M.U32(A + 0x488));
                M.U32(A + 0x390, M.U32(A + 0x48c));
                int age = M.I32(A + 0x3f4);
                bool skipFrames = false;
                if (age < 0x32)
                {
                    sbyte counter = M.I8(A + 0x451);
                    if (counter != 0)
                    {
                        float a = M.F32(A + 0x324);
                        if ((float)(0x28 - counter) > a) M.F32(A + 0x324, a + 1.0f);
                    }
                    else
                    {
                        sbyte kind = M.I8(A + 0x450);
                        float lim = 0f;
                        bool towards = false;
                        if (kind == 1) { lim = 200.0f; towards = true; }
                        else if (kind == 0xa)
                        {
                            float a = M.F32(A + 0x324);
                            if (255.0f > a)
                            {
                                a = a + 10.0f;
                                M.F32(A + 0x324, a);
                            }
                            if (a > 255.0f) M.F32(A + 0x324, 255.0f);
                        }
                        else if (kind == 0xb) M.F32(A + 0x324, 0f);
                        else if (kind >= 2)
                        {
                            if (age < 0x1e)
                            {
                                float a = M.F32(A + 0x324);
                                if (150.0f > a) M.F32(A + 0x324, a + 5.0f);
                                skipFrames = true;
                            }
                        }
                        else { lim = 245.0f; towards = true; }
                        if (towards)
                        {
                            float a = M.F32(A + 0x324);
                            if (lim > a) M.F32(A + 0x324, a + 5.0f);
                        }
                    }
                }
                if (!skipFrames && age > 0x1e)
                {
                    sbyte kind = M.I8(A + 0x450);
                    if (kind == 0xa || kind == 0xb)
                    {
                        int f = M.I32(A + 0x3c);
                        if ((age & 3) == 0)
                        {
                            f++;
                            M.I32(A + 0x3c, f);
                        }
                        if (f > 0xf) M.I32(A + 0x3c, 0xf);
                    }
                    else if (kind >= 0)
                    {
                        int f = M.I32(A + 0x3c);
                        if (age % 3 == 0)
                        {
                            f++;
                            M.I32(A + 0x3c, f);
                        }
                        if (f > 0x1d) M.I32(A + 0x3c, 0x1d);
                    }
                }
                sbyte k = M.I8(A + 0x450);
                int ag = M.I32(A + 0x3f4);
                float? next = null;
                float cur = M.F32(A + 0x324);
                if (k == 0) { if (ag > 100) next = cur - 4.0f; }
                else if (k == 2) { if (ag > 0x1e) next = cur - 3.0f; }
                else if (k == 3) { if (ag > 0x3c) next = cur - 3.0f; }
                else if (k == 4) { if (ag > 0x46) next = cur - 3.0f; }
                else if (k == 0xa) { if (ag > 100) next = cur - 6.0f; }
                else if (ag > 0x4b) next = M.I8(A + 0x451) != 0 ? cur - 3.0f : cur - 6.0f;
                if (next.HasValue)
                {
                    float v = next.Value;
                    M.F32(A + 0x324, v);
                    if (0f > v) M.F32(A + 0x324, 0f);
                }
            }
            M.U32(A + 0x10, M.U32(A + 0x388));
            M.U32(A + 0x14, M.U32(A + 0x38c));
            M.U32(A + 0x18, M.U32(A + 0x390));
            SnapshotTrail_b0d680();
        }

        // ================================================================ type 0x48 (poison ground grid, EF 335)

        [FxPrimProcess(0x48)]
        public FxCaseResult Case48_E10(FxRuntime rt) { Process_ae7c40(rt); return FxCaseResult.Continue; }

        [FxPrimRender(0x48)]
        public void Render48_E10(FxRuntime rt, uint viewMatrix) => Render_b05110(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_ae7c40 (0xae7c40) — camera angles, follow the master; per live slot (4 × 0xb4 from +0x3e8):
        /// age +0xc +1, scroll +0x8 wraps by −30 once ≥ 30, alpha +0x2: selector +0x72 == 1 → +10 while age &lt; 10 (up to
        /// 55), −10 after age 65 (down to 0); otherwise +5 (up to 50).
        /// </summary>
        public void Process_ae7c40(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            for (int k = 0; k < 4; k++)
            {
                uint c = A + 0x3ea + (uint)k * 0xb4;
                if (M.U8(c - 2) != 1) continue;
                int age = M.I32(c + 0xa) + 1;
                float scroll = M.F32(c + 6);
                M.I32(c + 0xa, age);
                if (!(scroll < 30.0f || float.IsNaN(scroll))) M.F32(c + 6, scroll - 30.0f);   // COMISS; JB
                short a = M.I16(c);
                if (M.U8(c + 0x70) == 1)
                {
                    if (age < 10)
                    {
                        if (a <= 0x2d) M.I16(c, (short)(a + 10));
                    }
                    else if (age > 0x41 && a >= 10) M.I16(c, (short)(a - 10));
                }
                else if (a <= 0x2d) M.I16(c, (short)(a + 5));
            }
        }

        /// <summary>
        /// CEffectPrim::fn_b05110 (0xb05110) — per live slot with alpha &gt; 0: a 10 × 6 grid of 30-unit quads at y −10
        /// ((v + x, v + z) .. (v + size + x, v + size + z), v = the scroll +0x3f0, size +0x468, x in −150..120, z in −90..60),
        /// camera-rotated (FUN_00b0d9c0), fn_b0a900 blend 1; colour by +0x45a: 0 / 3 white, 1 (255, 0, 0), else (0, 0, 255).
        /// </summary>
        public void Render_b05110(FxRuntime rt, uint matrix)
        {
            int count = 0;
            System.Span<float> p = stackalloc float[12];
            p.Clear();
            for (int k = 0; k < 4; k++)
            {
                uint s = A + 0x3f0 + (uint)k * 0xb4;
                if (M.U8(s - 8) != 1 || M.I16(s - 6) <= 0) continue;
                for (int x = -0x96; x < 0x96; x += 0x1e)
                {
                    float fx = (float)x;
                    for (int z = -0x5a; z < 0x5a; z += 0x1e)
                    {
                        float size = M.F32(s + 0x78), v = M.F32(s);
                        float fz = (float)z;
                        float far = (size + fx) + v;
                        float nearX = v + fx;
                        float farZ = (size + fz) + v;
                        float nearZ = v + fz;
                        p[0] = nearX; p[1] = -10.0f; p[2] = nearZ;
                        p[3] = far; p[4] = -10.0f; p[5] = nearZ;
                        p[6] = far; p[7] = -10.0f; p[8] = farZ;
                        p[9] = nearX; p[10] = -10.0f; p[11] = farZ;
                        Rotate4_b0d9c0(p, M.I16(A + 0x3ec), M.I16(A + 0x4a0));
                        byte sel = M.U8(s + 0x6a);
                        byte r, g, b;
                        if (sel == 0) { r = 0xff; g = 0xff; b = 0xff; }
                        else if (sel == 1) { r = 0xff; g = 0; b = 0; }
                        else if (sel == 3) { r = 0xff; g = 0xff; b = 0xff; }
                        else { r = 0; g = 0; b = 0xff; }
                        Quad_b0a900(rt, ref count, M.I16(s - 6), p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                            matrix, 1, 0, 0, r, g, b, 0f, 1.0f, 0f, 1.0f, 0);
                    }
                }
            }
        }
    }
}
