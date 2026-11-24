// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type0F10.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        [FxPrimProcess(0xf, 0x10)]
        public FxCaseResult Case0F10(FxRuntime rt)
        {
            Process_adfea0(rt);
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0xf)]
        public void Render0F(FxRuntime rt, uint viewMatrix) => Render_af42e0(rt, viewMatrix);

        [FxPrimRender(0x10)]
        public void Render10(FxRuntime rt, uint viewMatrix) => Render_af1150(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_adfea0 (0xadfea0) — OnProcess for prim types 0xf / 0x10 (textured quad / cross):
        /// one-shot keyframe of speeds at f_200, velocity integration (plain, 0x100 = raw velocity, 0x400 =
        /// homing to master f_180/f_188 with arrival test), size/rotation growth, orientation matrix at +0x190
        /// (0x200 camera-facing Z, 0x40 billboard, else Z·X) · Y, texture-v scroll (2), master follow (0x10),
        /// ground hit (4), size ping-pong (0x20), colour cycling (0x800 wrap / clamp), alpha, texture frames.
        /// Returns the AL result (false when homing arrived or a non-looping animation ended).
        /// </summary>
        public bool Process_adfea0(FxRuntime rt)
        {
            RagEffect master = m_master;
            float my = master.m_pos_y, mz = master.m_pos_z;
            f_37c = master.m_pos_x + f_394;
            f_380 = my + f_398;
            f_384 = mz + f_39c;
            if (f_200 == f_1e0)
            {
                const float K = 0.0001f;
                f_138 = 1;
                if (f_2c8 != K) f_358 = f_2c8;
                if (f_2cc != K) f_364 = f_2cc;
                if (f_29c != K) f_2bc = f_29c;
                if (f_2c4 != K) f_35c = f_2c4;
                if (f_2d0 != K) f_368 = f_2d0;
                if (f_2a0 != K) f_2c0 = f_2a0;
                if (f_2a4 != K) f_370 = f_2a4;
                if (f_2a8 != K) f_374 = f_2a8;
                if (f_2ac != K) f_378 = f_2ac;
                f_2c8 = K; f_2cc = K; f_29c = K; f_2c4 = K; f_2d0 = K; f_2a0 = K;
                f_2a4 = K; f_2a8 = K; f_2ac = K; f_2b0 = K; f_2b4 = K; f_2b8 = K;
            }
            float vx = f_370 + f_3ac;
            f_370 = vx;
            f_374 = f_3b0 + f_374;
            f_378 = f_3b4 + f_378;
            float v = f_2c0 + f_2bc;
            f_358 = f_35c + f_358;
            f_2bc = v;
            f_364 = f_368 + f_364;
            f_330 = f_33c + f_330;
            f_334 = f_340 + f_334;

            float dy, dz;
            int flags = f_1dc;
            if ((flags & 0x400) != 0 || (flags & 0x100) == 0)
            {
                if ((flags & 0x400) != 0)
                {
                    f_370 = 0f;
                    f_374 = 0f;
                    f_378 = 0f;
                }
                float dx = f_16c * 0.0f + f_160 * 0.0f + v * f_178 + f_184;
                f_370 = dx;
                dy = f_170 * 0.0f + f_164 * 0.0f + v * f_17c + f_188;
                f_374 = dy;
                dz = f_174 * 0.0f + f_168 * 0.0f + v * f_180 + f_18c;
                f_388 = dx + f_388;
                f_378 = dz;
                dz = dz + f_390;
                dy = dy + f_38c;
                f_390 = dz;
                f_38c = dy;
            }
            else
            {
                f_388 = vx + f_388;
                dy = f_374 + f_38c;
                f_38c = dy;
                dz = f_378 + f_390;
                f_390 = dz;
            }

            if ((f_1dc & 0x400) != 0)
            {
                // FUN_00bce640 (thiscall on this): distance in the XZ plane from the prim to (x, z).
                float ddz = master.f_188 - m_pos_z;
                float ddx = master.f_180 - m_pos_x;
                float d = (float)Math.Sqrt((double)(ddz * ddz + ddx * ddx));
                if (3.0f >= d)
                {
                    f_14c = 1;
                    return false;
                }
                M.U32(A + 0x30c, M.U32(A + 0x4c));
                dy = f_38c;
                dz = f_390;
            }
            m_pos_x = f_37c + f_388;
            m_pos_y = dy + f_380;
            m_pos_z = dz + f_384;
            float size = f_364 + f_360;
            f_354 = f_358 + f_354;
            f_30c = f_30c + f_330;
            f_360 = size;
            f_310 = f_310 + f_334;
            if (size > f_36c)
            {
                f_368 = 0f;
                f_364 = 0f;
            }

            uint mat = A + 0x190;
            float yaw;
            flags = f_1dc;
            if ((flags & 0x200) != 0)
            {
                uint view = M.U32(rt.Mode + 0xd0);
                FxMatrix.MakeRotationZ(M, mat, M.F32(view + 0x2c));
                yaw = f_4c;
            }
            else
            {
                if ((flags & 0x40) != 0)
                {
                    uint view = M.U32(rt.Mode + 0xd0);
                    f_30c = M.F32(view + 0x30) + 90.0f;
                    M.U32(A + 0x314, M.U32(view + 0x2c));
                    FxMatrix.MakeRotationX(M, mat, FxMath.DegToRad(FxOps.Cvtt(f_310)));
                    FxMatrix.RotateZ(M, mat, FxMath.DegToRad(FxOps.Cvtt(f_314)));
                }
                else
                {
                    FxMatrix.MakeRotationZ(M, mat, FxMath.DegToRad(FxOps.Cvtt(f_314)));
                    FxMatrix.RotateX(M, mat, FxMath.DegToRad(FxOps.Cvtt(f_310)));
                }
                yaw = f_30c;
            }
            FxMatrix.RotateY(M, mat, FxMath.DegToRad(FxOps.Cvtt(yaw)));

            int fl = f_1dc;
            if ((fl & 2) != 0)
            {
                int frame = f_1d4;
                int next = frame + 1;
                if (next == 10) next = 0;
                f_1d4 = next;
                float t = (float)frame / 10.0f;
                f_2e4 = t;
                f_2e8 = t + 1.0f;
            }
            if ((fl & 0x10) != 0)
            {
                float oy = f_398, py = f_38c, oz = f_39c, pz = f_390;
                master.f_180 = f_394 + f_388;
                master.f_184 = py + oy;
                master.f_188 = pz + oz;
                fl = f_1dc;
            }
            if ((fl & 4) != 0)
            {
                float h = rt.GatGetHeight_6c32a0(M.U32(rt.World + 0x30), m_pos_x, m_pos_z);
                if (m_pos_y > h)
                {
                    f_1dc = f_1dc & unchecked((int)0xfffffffb);
                    f_1e0 = m_duration - 0x19;
                    f_2bc = 0f;
                    M.I32(master.A + 0x170, 1);
                }
            }
            int fb = f_1dc;
            if ((fb & 0x20) != 0 && f_1e0 % f_1cc == 0)
                f_364 = f_364 * -1.0f;

            uint rA = A + 0x23c, gA = A + 0x240, bA = A + 0x244;
            int wrap = fb & 0x800;
            int r;
            if (wrap != 0 && (M.I32(rA) == 0xfe || M.I32(bA) == 0xfe || M.I32(gA) == 0xfe))
            {
                r = M.I32(rA);
                int step = f_234;
                int cnt = f_1fc;
                if (step != 0)
                {
                    cnt++;
                    f_1fc = cnt;
                }
                if (step == cnt)
                {
                    r += f_248;
                    M.I32(gA, M.I32(gA) + f_24c);
                    M.I32(bA, M.I32(bA) + f_250);
                    M.I32(rA, r);
                    f_1fc = 0;
                }
                ColorWrap_adfea0(r);
            }
            else
            {
                M.I32(rA, M.I32(rA) + f_248);
                M.I32(gA, M.I32(gA) + f_24c);
                M.I32(bA, M.I32(bA) + f_250);
                r = M.I32(rA);
                if (wrap != 0) ColorWrap_adfea0(r);
                else
                {
                    if (r > 0xff) M.I32(rA, 0);
                    else if (r < 0) M.I32(rA, 0xff);
                    int g = M.I32(gA);
                    if (g > 0xff) M.I32(gA, 0xff);
                    else if (g < 0) M.I32(gA, 0);
                    int b = M.I32(bA);
                    if (b > 0xff) M.I32(bA, 0xff);
                    else if (b < 0) M.I32(bA, 0);
                }
            }

            UpdateAlpha_aed050();
            if (f_1e0 % f_1e8 == 0)
            {
                int n = f_204;
                int frame = f_3c + 1;
                f_3c = frame;
                if (M.U8(A + 0x254) == 0)
                {
                    if (frame == n) return false;
                }
                else f_3c = frame % n;
            }
            return true;
        }

        /// <summary>Colour cycling with wrap (flag 0x800) in fn_adfea0: components wrap within 0..0xfe.</summary>
        void ColorWrap_adfea0(int r)
        {
            uint rA = A + 0x23c, gA = A + 0x240, bA = A + 0x244;
            if (r > 0xfe) M.I32(rA, 0);
            else if (r < 0) M.I32(rA, 0xfe);
            int g = M.I32(gA);
            if (g > 0xfe) M.I32(gA, 0);
            else if (g < 0) M.I32(gA, 0xfe);
            int b = M.I32(bA);
            if (b > 0xfe) M.I32(bA, 0);
            else if (b < 0) M.I32(bA, 0xfe);
        }

        /// <summary>
        /// CEffectPrim::fn_af42e0 (0xaf42e0) — Render for prim type 0xf: one quad in the local YZ plane
        /// (width ±f_354, height f_360 down to −f_360; flag 0x80 = hanging from the origin at double height,
        /// flag 8 = bottom at origin), rotated by the matrix at +0x190, two RP triangles, texture frame f_3c.
        /// </summary>
        public void Render_af42e0(FxRuntime rt, uint viewMatrix)
        {
            float w = f_354;
            float top = f_360;
            float nw = -w;
            float bottom = -top;
            int flags = f_1dc;
            if ((sbyte)flags < 0)
            {
                bottom = bottom + bottom;
                top = 0f;
            }
            if ((flags & 8) != 0) top = 0f;

            float z0 = f_190 * 0.0f;
            float xb = f_19c * bottom + z0;
            float xw = f_1a8 * w;
            float p0x = xw + xb + f_1b4;
            float z1 = f_194 * 0.0f;
            float yb = f_1a0 * bottom + z1;
            float yw = f_1ac * w;
            float p0y = yw + yb + f_1b8;
            float z2 = f_198 * 0.0f;
            float zb = f_1a4 * bottom + z2;
            float zw = f_1b0 * f_354;
            float p0z = zw + zb + f_1bc;
            float xn = nw * f_1a8;
            float yn = nw * f_1ac;
            float tz = f_1bc;
            float p1x = xn + xb + f_1b4;
            float ty = f_1b8;
            float p1y = yn + yb + ty;
            float zn = nw * f_1b0;
            float yt = top * f_1a0 + z1;
            float p1z = zn + zb + tz;
            float xt = top * f_19c + z0;
            float p2x = xt + xw + f_1b4;
            float p2y = yt + yw + ty;
            float zt = top * f_1a4 + z2;
            float p2z = zt + zw + tz;
            float p3x = xt + xn + f_1b4;
            float p3y = yt + yn + ty;
            float p3z = zt + zn + tz;

            uint argb = (uint)m_argb;
            uint f1 = A + 0x2210, f2 = A + 0x227c;
            P2Vertex(rt, viewMatrix, f1 + 0x00, p0x + m_pos_x, p0y + m_pos_y, p0z + m_pos_z, argb, 0f, f_2e4);
            P2Vertex(rt, viewMatrix, f1 + 0x20, p1x + m_pos_x, p1y + m_pos_y, p1z + m_pos_z, argb, 1.0f, f_2e4);
            P2Vertex(rt, viewMatrix, f1 + 0x40, p2x + m_pos_x, p2y + m_pos_y, p2z + m_pos_z, argb, 0f, f_2e8);
            M.U32(f1 + 0x60, M.U32((uint)(f_144 + f_3c * 4)));
            P2Vertex(rt, viewMatrix, f2 + 0x00, p3x + m_pos_x, p3y + m_pos_y, p3z + m_pos_z, argb, 1.0f, f_2e8);
            P2Vertex(rt, viewMatrix, f2 + 0x20, p2x + m_pos_x, p2y + m_pos_y, p2z + m_pos_z, argb, 0f, f_2e8);
            P2Vertex(rt, viewMatrix, f2 + 0x40, p1x + m_pos_x, p1y + m_pos_y, p1z + m_pos_z, argb, 1.0f, f_2e4);
            M.U32(f2 + 0x60, M.U32((uint)(f_144 + f_3c * 4)));
            rt.Renderer.AddRP(f1, (int)f_1c4);
            rt.Renderer.AddRP(f2, (int)f_1c4);
        }

        /// <summary>
        /// CEffectPrim::fn_af1150 (0xaf1150) — Render for prim type 0x10: two crossed quads (XZ and YZ planes,
        /// ±f_360 × ±f_354) rotated by the matrix at +0x160, four RP triangles (flags 1), texture frame f_3c.
        /// </summary>
        public void Render_af1150(FxRuntime rt, uint viewMatrix)
        {
            float h = f_360, w = f_354, nh = -h, nw = -w;
            Span<float> a = stackalloc float[]
            {
                nh, 0f, w,   nh, 0f, nw,   h, 0f, w,   h, 0f, nw,
                0f, nh, w,   0f, nh, nw,   0f, h, w,   0f, h, nw,
            };
            uint face = A + 0x2210;
            Span<float> px = stackalloc float[4], py = stackalloc float[4], pz = stackalloc float[4];
            for (int q = 0; q < 2; q++)
            {
                int b = q * 12;
                for (int k = 0; k < 4; k++)
                {
                    float x = a[b + k * 3], y = a[b + k * 3 + 1], z = a[b + k * 3 + 2];
                    px[k] = f_160 * x + f_16c * y + z * f_178 + f_184;
                    py[k] = f_164 * x + f_170 * y + z * f_17c + f_188;
                    pz[k] = f_168 * x + f_174 * y + z * f_180 + f_18c;
                }
                uint argb = (uint)m_argb;
                P2Vertex(rt, viewMatrix, face + 0x00, px[0] + m_pos_x, py[0] + m_pos_y, pz[0] + m_pos_z, argb, 0f, 1.0f);
                P2Vertex(rt, viewMatrix, face + 0x20, px[1] + m_pos_x, py[1] + m_pos_y, pz[1] + m_pos_z, argb, 1.0f, 1.0f);
                P2Vertex(rt, viewMatrix, face + 0x40, px[2] + m_pos_x, py[2] + m_pos_y, pz[2] + m_pos_z, argb, 0f, 0f);
                M.U32(face + 0x60, M.U32((uint)(f_144 + f_3c * 4)));
                rt.Renderer.AddRP(face, 1);
                uint f2 = face + FxRenderer.FaceSize;
                P2Vertex(rt, viewMatrix, f2 + 0x00, px[3] + m_pos_x, py[3] + m_pos_y, pz[3] + m_pos_z, argb, 1.0f, 0f);
                P2Vertex(rt, viewMatrix, f2 + 0x20, px[2] + m_pos_x, py[2] + m_pos_y, pz[2] + m_pos_z, argb, 0f, 0f);
                P2Vertex(rt, viewMatrix, f2 + 0x40, px[1] + m_pos_x, py[1] + m_pos_y, pz[1] + m_pos_z, argb, 1.0f, 1.0f);
                M.U32(f2 + 0x60, M.U32((uint)(f_144 + f_3c * 4)));
                rt.Renderer.AddRP(f2, 1);
                face += 2 * FxRenderer.FaceSize;
            }
        }
    }
}
