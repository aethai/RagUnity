// Synced from reference/port/src/RoClassicFx/Prim/prims-3/Prim.Prims3.SpriteRender.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>prims-3: screen-space quads (types 0 / 2) and the textured cylinder band (type 0xd).</summary>
    public readonly partial struct Prim
    {
        [FxPrimRender(0x0)]
        public void Render00(FxRuntime rt, uint viewMatrix) => Render_aedbf0(rt);

        [FxPrimRender(0x2)]
        public void Render02(FxRuntime rt, uint viewMatrix) => Render_aed820(rt);

        [FxPrimRender(0xd)]
        public void Render0D(FxRuntime rt, uint viewMatrix) => Render_af1a50(rt, viewMatrix);

        const uint P3Z_1e6 = 0x358637bd;      // 1e-06f
        const uint P3Rhw_0999999 = 0x3f7fffef; // 0.999999f

        /// <summary>Screen-space vertex: x, y, z = 1e-6, rhw = 0.999999, colour, (specular untouched), tu, tv.</summary>
        void P3ScreenVertex(uint v, float x, float y, int color, float tu, float tv)
        {
            M.F32(v + 0x00, x);
            M.F32(v + 0x04, y);
            M.U32(v + 0x08, P3Z_1e6);
            M.U32(v + 0x0c, P3Rhw_0999999);
            M.I32(v + 0x10, color);
            M.F32(v + 0x18, tu);
            M.F32(v + 0x1c, tv);
        }

        /// <summary>
        /// CEffectPrim::fn_aedbf0 (0xaedbf0) — type 0: screen-space quad of half-size (f_354, f_360) rotated by
        /// f_314 degrees around the prim position (pixel-centre −0.5), flag 1 anchors it at the bottom edge.
        /// f_3e8 == 'b' (skill cooldown clock) resets the frame and draws the remaining-time text (UI shim).
        /// </summary>
        public void Render_aedbf0(FxRuntime rt)
        {
            bool clock = M.U8(A + 0x3e8) == 0x62;
            if (clock) M.I32(A + 0x3c, 0);
            float s = FxMath.GetSin(FxOps.Cvtt(f_314));
            float c = FxMath.GetCos(FxOps.Cvtt(f_314));
            float w = f_354;
            float nw = -w;
            float h = f_360;
            float nh = -h;
            float top = 0f;
            if ((f_1dc & 1) != 0) nh = nh + nh;
            else top = h;
            float wc = nw * c;
            float nhs = nh * s;
            float t0c = wc - nhs;
            float ws = nw * s;
            float nhc = nh * c;
            float t10 = ws + nhc;
            float pwc = w * c;
            float pws = w * s;
            float t18 = pwc - nhs;
            float t1c = pws + nhc;
            float topC = top * c;
            float topS = top * s;
            float t14 = pws + topC;
            float x0 = wc - topS;
            float t8 = pwc - topS;
            float px = m_pos_x, py = m_pos_y;
            int argb = m_argb;
            uint f = A + 0x2210;
            P3ScreenVertex(f + 0x00, (x0 + px) - 0.5f, (ws + topC + py) - 0.5f, argb, 0f, 1.0f);
            P3ScreenVertex(f + 0x20, (t0c + px) - 0.5f, (py + t10) - 0.5f, argb, 0f, 0f);
            P3ScreenVertex(f + 0x40, (t8 + px) - 0.5f, (py + t14) - 0.5f, argb, 1.0f, 1.0f);
            M.U32(f + 0x60, M.U32((uint)f_144 + (uint)M.I32(A + 0x3c) * 4));
            rt.Renderer.AddRP(f, f_1c8 == 0 ? 1 : (int)f_1c4);

            f = A + 0x227c;
            px = m_pos_x;
            py = m_pos_y;
            argb = m_argb;
            P3ScreenVertex(f + 0x00, (t8 + px) - 0.5f, (t14 + py) - 0.5f, argb, 1.0f, 1.0f);
            P3ScreenVertex(f + 0x20, (px + t0c) - 0.5f, (py + t10) - 0.5f, argb, 0f, 0f);
            P3ScreenVertex(f + 0x40, (px + t18) - 0.5f, (py + t1c) - 0.5f, argb, 1.0f, 0f);
            M.U32(f + 0x60, M.U32((uint)f_144 + (uint)M.I32(A + 0x3c) * 4));
            rt.Renderer.AddRP(f, f_1c8 == 0 ? 1 : (int)f_1c4);

            if (clock) rt.SkillCooldownTextImpl_aedbf0(A);
        }

        /// <summary>
        /// CEffectPrim::fn_aed820 (0xaed820) — type 2: screen-space arrow/diamond (half-size f_354 × f_360,
        /// waist at h/1.2) rotated by f_314, centre vertices white, tips m_argb.
        /// </summary>
        public void Render_aed820(FxRuntime rt)
        {
            float s = FxMath.GetSin(FxOps.Cvtt(f_314));
            float c = FxMath.GetCos(FxOps.Cvtt(f_314));
            float h = f_360 * 0.5f;
            float w = f_354 * 0.5f;
            float h12 = h / 1.2f;
            float nw = -w;
            float waistA = h12 - h;
            float nh = -h;
            int argb = m_argb;
            float x0 = nw * c - waistA * s;
            float y0 = waistA * c + nw * s;
            float c0 = c * 0f;
            float x1 = c0 - h * s;
            float s0 = s * 0f;
            float y1 = h * c + s0;
            float x2 = c0 - nh * s;
            float y2 = nh * c + s0;

            uint f = A + 0x2210;
            float px = m_pos_x, py = m_pos_y;
            P3ScreenVertex(f + 0x00, x0 + px, y0 + py, argb, 0f, 0.5f);
            P3ScreenVertex(f + 0x20, px + x1, py + y1, -1, 0.5f, 1.0f);
            P3ScreenVertex(f + 0x40, px + x2, py + y2, argb, 0.5f, 0f);
            M.U32(f + 0x60, M.U32((uint)f_144 + (uint)M.I32(A + 0x3c) * 4));
            rt.Renderer.AddRP(f, 1);

            float waistB = h - h12;
            float x3 = w * c - waistB * s;
            float y3 = waistB * c + w * s;
            f = A + 0x227c;
            px = m_pos_x;
            py = m_pos_y;
            P3ScreenVertex(f + 0x00, x3 + px, y3 + py, argb, 1.0f, 0.5f);
            P3ScreenVertex(f + 0x20, px + x1, py + y1, -1, 0.5f, 1.0f);
            P3ScreenVertex(f + 0x40, px + x2, py + y2, argb, 0.5f, 0f);
            M.U32(f + 0x60, M.U32((uint)f_144 + (uint)M.I32(A + 0x3c) * 4));
            rt.Renderer.AddRP(f, 1);
        }

        /// <summary>
        /// CEffectPrim::fn_af1a50 (0xaf1a50) — type 0xd: a band between radius f_2f4 (at height 0) and f_2f8
        /// (at height −f_360) around the prim matrix, one quad every f_288 degrees; u advances 0.25 per quad.
        /// </summary>
        public void Render_af1a50(FxRuntime rt, uint matrix)
        {
            FxRenderer rr = rt.Renderer;
            float u = 0f;
            float L = 0f;
            float end = 360.0f - f_288;
            if (P3Jb(end, 0f)) return;
            uint face = A + 0x2210;
            do
            {
                float step = f_288;
                int ia = FxOps.Cvtt(L);
                int ib = FxOps.Cvtt(step + L);
                float m0 = f_160, m1 = f_164, m2 = f_168, m3 = f_16c, m4 = f_170, m5 = f_174;
                float m6 = f_178, m7 = f_17c, m8 = f_180, m9 = f_184, m10 = f_188, m11 = f_18c;
                float r1 = f_2f4, r2 = f_2f8;
                float cA = P3Cos(ia), sB = P3Sin(ib), cB = P3Cos(ib);
                float t2c = r1 * cA;
                float t30 = r1 * P3Sin(ia);
                float t34 = m3 * 0f;
                float t48 = m4 * 0f;
                float t4c = m5 * 0f;
                float t38 = r1 * sB;
                float r1cB = r1 * cB;
                float t3c = r2 * cA;
                float nh = -f_360;
                float t58 = r2 * P3Sin(ia);
                float t54 = nh * m3;
                float t40 = nh * m4;
                float nhm5 = nh * m5;
                float t60 = r2 * sB;
                float t5c = r2 * cB;
                float px = m_pos_x, py = m_pos_y, pz = m_pos_z;

                float ax = t2c * m0 + t34 + t30 * m6 + m9 + px;
                float ay = t2c * m1 + t48 + t30 * m7 + m10 + py;
                float az = t2c * m2 + t4c + t30 * m8 + m11 + pz;
                float bx = r1cB * m0 + t34 + t38 * m6 + m9 + px;
                float by = r1cB * m1 + t48 + t38 * m7 + m10 + py;
                float bz = r1cB * m2 + t4c + t38 * m8 + m11 + pz;
                float cx = t3c * m0 + t54 + t58 * m6 + m9 + px;
                float cy = t3c * m1 + t40 + t58 * m7 + m10 + py;
                float cz = t3c * m2 + nhm5 + t58 * m8 + m11 + pz;
                float dx = m0 * t5c + t54 + m6 * t60 + m9 + px;
                float dy = m1 * t5c + t40 + m7 * t60 + m10 + py;
                float dz = m2 * t5c + nhm5 + m8 * t60 + m11 + pz;

                int argb = m_argb;
                float u1 = u + 0.25f;
                P3Vertex(rr, matrix, face + 0x00, ax, ay, az, argb, u, 1.0f);
                P3Vertex(rr, matrix, face + 0x40, bx, by, bz, argb, u1, 1.0f);
                P3Vertex(rr, matrix, face + 0x20, cx, cy, cz, argb, u, 0f);
                M.U32(face + 0x60, M.U32((uint)f_144 + (uint)M.I32(A + 0x3c) * 4));
                rr.AddRP(face, 1);

                P3Vertex(rr, matrix, face + 0xac, dx, dy, dz, m_argb, u1, 0f);
                P3Vertex(rr, matrix, face + 0x8c, cx, cy, cz, m_argb, u, 0f);
                P3Vertex(rr, matrix, face + 0x6c, bx, by, bz, m_argb, u1, 1.0f);
                M.U32(face + 0xcc, M.U32((uint)f_144 + (uint)M.I32(A + 0x3c) * 4));
                rr.AddRP(face + 0x6c, 1);

                u = u1;
                if (!P3Jb(u1, 1.0f)) u = 0f;
                face += 0xd8;
                L = f_288 + L;
            } while (end >= L);
        }
    }
}
