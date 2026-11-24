// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type0C.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        [FxPrimProcess(0xc)]
        public FxCaseResult Case0C(FxRuntime rt)
        {
            Process_add810();
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0xc)]
        public void Render0C(FxRuntime rt, uint viewMatrix) => Render_af0a30(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_add810 (0xadd810) — OnProcess for prim type 0xc (cylinder ring, e.g. Blessing /
        /// Magnum Break): position like type 9, rotation X(f_310)·Y(f_30c) into the matrix at +0x160,
        /// radius f_318 = min(f_348, f_2f4), alpha, then shifts the trail (0x8c-byte entries @0x6b8)
        /// and snapshots the current state into entry 0.
        /// </summary>
        public void Process_add810()
        {
            RagEffect master = m_master;
            float my = master.m_pos_y, mz = master.m_pos_z;
            f_37c = master.m_pos_x + f_394;
            f_380 = my + f_398;
            f_384 = mz + f_39c;

            float speed, accel;
            if (f_200 == f_1e0)
            {
                speed = f_2a0;
                accel = f_29c;
                f_350 = speed;
            }
            else
            {
                speed = f_350;
                accel = f_34c;
            }
            float rotX = f_33c + f_330;
            f_2bc = f_2c0 + f_2bc;
            float grow = f_304 + f_2fc;
            f_330 = rotX;
            f_34c = accel + speed;
            f_2fc = grow;
            f_30c = rotX + f_30c;
            f_348 = accel + speed + f_348;
            f_2f4 = grow + f_2f4;
            FxMatrix.MakeRotationX(M, A + 0x160, FxMath.DegToRad(FxOps.Cvtt(f_310)));
            FxMatrix.RotateY(M, A + 0x160, FxMath.DegToRad(FxOps.Cvtt(f_30c)));

            float v = f_2bc;
            float dx = f_16c * 0.0f + f_160 * 0.0f + v * f_178 + f_184;
            f_370 = dx;
            dx += f_388;
            float dy = f_170 * 0.0f + f_164 * 0.0f + v * f_17c + f_188;
            f_374 = dy;
            dy += f_38c;
            float dz = f_174 * 0.0f + f_168 * 0.0f + v * f_180 + f_18c;
            f_388 = dx;
            f_38c = dy;
            f_378 = dz;
            dz += f_390;
            f_390 = dz;
            m_pos_x = dx + f_37c;
            m_pos_y = dy + f_380;
            m_pos_z = dz + f_384;

            float r;
            if ((f_1dc & 1) != 0) r = f_348;
            else
            {
                r = f_2f4;
                if (r > f_348) r = f_348;
            }
            f_318 = r;
            UpdateAlpha_aed050();

            int n = f_1f4;
            int i = n - 1;
            if (0 < i)
            {
                uint dst = A + 0x6b8 + (uint)i * 0x8c;
                do
                {
                    uint src = dst - 0x8c;
                    for (uint w = 0; w < 0x1e; w++)
                        if (w != 0x10) M.U32(dst + w * 4, M.U32(src + w * 4));
                    float alpha = f_324 - (f_324 / (float)f_1f4) * (float)i;
                    M.F32(dst + 0x84, alpha);
                    M.U32(dst + 0x88, P2Argb(alpha));
                    M.F32(dst + 0x40, f_318 - (f_318 / (float)f_1f4) * (float)i * 0.5f);
                    dst -= 0x8c;
                    i--;
                } while (0 < i);
            }

            M.U32(A + 0x6b8, M.U32(A + 0x10));
            M.U32(A + 0x6bc, M.U32(A + 0x14));
            M.U32(A + 0x6c0, M.U32(A + 0x18));
            for (uint w = 0; w < 12; w++) M.U32(A + 0x6c4 + w * 4, M.U32(A + 0x3b8 + w * 4));
            M.U32(A + 0x6f4, M.U32(A + 0x348));
            M.U32(A + 0x6f8, M.U32(A + 0x318));
            M.U32(A + 0x6fc, M.U32(A + 0x30c));
            for (uint w = 0; w < 12; w++) M.U32(A + 0x700 + w * 4, M.U32(A + 0x160 + w * 4));
            M.U32(A + 0x73c, M.U32(A + 0x324));
            M.U32(A + 0x740, P2Argb(f_324));
        }

        /// <summary>
        /// CEffectPrim::fn_af0a30 (0xaf0a30) — Render for prim type 0xc: for each trail entry, a ring band
        /// between radius (entry f_348 − f_318) and entry f_348, in f_288-degree steps, rotated by the current
        /// matrix and placed at the entry position; two RP triangles per step, u advancing by 0.25.
        /// </summary>
        public void Render_af0a30(FxRuntime rt, uint viewMatrix)
        {
            int face = 0;
            float u = 0f;
            float end = 360.0f - f_288;
            for (int k = 0; k < f_1f4; k++)
            {
                uint e = A + 0x6b8 + (uint)k * 0x8c;
                float inner = M.F32(e + 0x3c) - f_318;
                float ang = 0f;
                if (!(end >= 0f)) continue;
                uint dst = A + 0x2210 + (uint)face * FxRenderer.FaceSize;
                do
                {
                    float step = f_288;
                    int ia = FxOps.Cvtt(ang);
                    int ib = FxOps.Cvtt(step + ang);
                    float cosA = FxMath.CosTableAt(ia), sinA = FxMath.SinTableAt(ia);
                    float sinB = FxMath.SinTableAt(ib), cosB = FxMath.CosTableAt(ib);
                    float outer = M.F32(e + 0x3c);
                    float ciA = cosA * inner, siA = sinA * inner;
                    float z0 = f_178 * 0.0f, z1 = f_17c * 0.0f, z2 = f_180 * 0.0f;
                    float coA = outer * cosA, soA = outer * sinA;
                    float soB = outer * sinB, coB = outer * cosB;
                    float ciB = cosB * inner, siB = sinB * inner;
                    float px = M.F32(e + 0x0), py = M.F32(e + 0x4), pz = M.F32(e + 0x8);

                    float ax = f_160 * ciA + f_16c * siA + z0 + f_184 + px;
                    float ay = f_164 * ciA + f_170 * siA + z1 + f_188 + py;
                    float az = f_168 * ciA + f_174 * siA + z2 + f_18c + pz;
                    float bx = ciB * f_160 + siB * f_16c + z0 + f_184 + px;
                    float by = ciB * f_164 + siB * f_170 + z1 + f_188 + py;
                    float bz = ciB * f_168 + siB * f_174 + z2 + f_18c + pz;
                    float cx = f_16c * soA + f_160 * coA + z0 + f_184 + px;
                    float cy = f_164 * coA + f_170 * soA + z1 + f_188 + py;
                    float cz = f_168 * coA + f_174 * soA + z2 + f_18c + pz;
                    float dx = f_16c * soB + f_160 * coB + z0 + f_184 + px;
                    float dy = f_164 * coB + f_170 * soB + z1 + f_188 + py;
                    float dz = f_168 * coB + f_174 * soB + z2 + f_18c + pz;

                    uint color = M.U32(e + 0x88);
                    float u2 = u + 0.25f;
                    P2Vertex(rt, viewMatrix, dst + 0x00, ax, ay, az, color, u, 1.0f);
                    P2Vertex(rt, viewMatrix, dst + 0x20, bx, by, bz, color, u2, 1.0f);
                    P2Vertex(rt, viewMatrix, dst + 0x40, cx, cy, cz, color, u, 0.0f);
                    M.U32(dst + 0x60, M.U32((uint)f_144));
                    rt.Renderer.AddRP(dst, (int)f_1c4);
                    uint dst2 = dst + FxRenderer.FaceSize;
                    P2Vertex(rt, viewMatrix, dst2 + 0x00, dx, dy, dz, color, u2, 0.0f);
                    P2Vertex(rt, viewMatrix, dst2 + 0x20, cx, cy, cz, color, u, 0.0f);
                    P2Vertex(rt, viewMatrix, dst2 + 0x40, bx, by, bz, color, u2, 1.0f);
                    M.U32(dst2 + 0x60, M.U32((uint)f_144));
                    face += 2;
                    rt.Renderer.AddRP(dst2, (int)f_1c4);
                    u = u2;
                    if (1.0f <= u2) u = 0f;
                    dst += 2 * FxRenderer.FaceSize;
                    ang = f_288 + ang;
                } while (ang <= end);
            }
        }
    }
}
