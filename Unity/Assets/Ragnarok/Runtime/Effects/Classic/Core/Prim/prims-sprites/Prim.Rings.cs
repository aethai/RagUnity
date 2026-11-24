// Synced from reference/port/src/RoClassicFx/Prim/prims-sprites/Prim.Rings.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Textured-geometry prims of package prims-sprites: type 3 (screen-space fan triangle), type 4
    /// (screen-space ring of quads, fn_aed330) and type 0xe (projected sphere, fn_af3870).
    /// Faces are written to the prim's face buffer (+0x2210) and submitted with AddRP (0x537dd0).
    /// </summary>
    public readonly partial struct Prim
    {
        const uint ZNear = 0x358637bd;      // 1e-6
        const uint RhwOne = 0x3f7fffef;     // 0.999999

        // ---- type 3 ------------------------------------------------------------------------

        /// <summary>Type 3 OnProcess case (0xad5cb3): screen-space radius f_360 grows by f_364 (+f_368 accel), spin f_30c.</summary>
        [FxPrimProcess(3)]
        public FxCaseResult Case03(FxRuntime rt)
        {
            uint master = m_master_ptr;
            m_pos_x = f_394 + M.F32(master + 0x154);
            m_pos_y = f_398 + M.F32(master + 0x158);
            if ((f_1dc & 1) != 0 && !(0f < f_360 || float.IsNaN(f_360)))
            {
                f_364 = 0f;
                f_368 = 0f;
            }
            float speed;
            if (f_1e0 == f_200)
            {
                M.U32(A + 0x330, M.U32(A + 0x29c));
                speed = f_2cc;
                M.U32(A + 0x33c, M.U32(A + 0x2a0));
            }
            else speed = f_364;
            float radius = f_360;
            if (0f > radius)
            {
                speed = speed * -1.0f;
                radius = 0f;
            }
            float spin = f_33c + f_330;
            f_330 = spin;
            speed = speed + f_368;
            f_364 = speed;
            speed = speed + radius;
            f_30c = spin + f_30c;
            f_360 = speed;
            f_288 = f_358 + f_288;
            UpdateAlpha_aed050();
            return FxCaseResult.Continue;
        }

        /// <summary>Type 3 Render case (inline at 0xaf4d72): one screen-space triangle, apex at the position, radius f_360, half-angle f_288.</summary>
        [FxPrimRender(3)]
        public void Render03(FxRuntime rt, uint viewMatrix)
        {
            float c0 = FxMath.GetCos(FxOps.Cvtt(f_30c - f_288));
            float s0 = FxMath.GetSin(FxOps.Cvtt(f_30c - f_288));
            float c1 = FxMath.GetCos(FxOps.Cvtt(f_30c + f_288));
            float s1 = FxMath.GetSin(FxOps.Cvtt(f_30c + f_288));
            uint face = A + 0x2210;
            float x = m_pos_x, y = m_pos_y;
            M.F32(face, x);
            M.F32(face + 4, y);
            M.U32(face + 8, ZNear);
            M.U32(face + 0xc, RhwOne);
            int argb = m_argb;
            M.I32(face + 0x10, argb);
            M.F32(face + 0x18, 0.5f);
            M.U32(face + 0x1c, 0);
            float r = f_360;
            M.F32(face + 0x20, r * c0 + x);
            M.F32(face + 0x24, y - r * s0);
            M.F32(face + 0x40, r * c1 + x);
            M.F32(face + 0x3c, 1.0f);
            M.F32(face + 0x38, 1.0f);
            M.I32(face + 0x30, argb);
            M.U32(face + 0x2c, RhwOne);
            M.U32(face + 0x28, ZNear);
            M.F32(face + 0x44, y - r * s1);
            M.U32(face + 0x48, ZNear);
            M.U32(face + 0x4c, RhwOne);
            M.I32(face + 0x50, argb);
            M.U32(face + 0x58, 0);
            M.F32(face + 0x5c, 1.0f);
            M.U32(face + 0x60, M.U32((uint)f_144));
            rt.Renderer.AddRP(face, 1);
        }

        // ---- type 4 ------------------------------------------------------------------------

        /// <summary>Type 4 OnProcess case (0xad5dc4): ring radius f_348 grows (f_34c speed, f_350 accel); inner radius f_318 capped at f_2f4.</summary>
        [FxPrimProcess(4)]
        public FxCaseResult Case04(FxRuntime rt)
        {
            uint master = m_master_ptr;
            m_pos_x = f_394 + M.F32(master + 0x154);
            m_pos_y = f_398 + M.F32(master + 0x158);
            float v = f_350 + f_34c;
            f_34c = v;
            v = v + f_348;
            f_348 = v;
            float inner = f_2f4;
            if ((f_1dc & 1) != 0 || inner > v) inner = v;
            f_318 = inner;
            UpdateAlpha_aed050();
            return FxCaseResult.Continue;
        }

        [FxPrimRender(4)]
        public void Render04(FxRuntime rt, uint viewMatrix) => Render_aed330(rt);

        /// <summary>
        /// CEffectPrim::fn_aed330 (0xaed330) — screen-space ring between radii f_348 − f_318 and f_348, one quad
        /// (two AddRP triangles) per f_288 degrees; u advances 0.25 per segment and wraps at 1.
        /// </summary>
        public void Render_aed330(FxRuntime rt)
        {
            float step = f_288;
            float limit = 360.0f - step;
            float inner = f_348 - f_318;
            if (limit < 0f || float.IsNaN(limit)) return;
            float u0 = 0f, angle = 0f;
            uint face = A + 0x2210;
            float angleEnd = step;
            while (true)
            {
                int a = FxOps.Cvtt(angle);
                angleEnd = step + angle;
                float c1 = FxMath.CosTableAt(a), s1 = FxMath.SinTableAt(a);
                float ic = c1 * inner;
                float isn = s1 * inner;
                int b = FxOps.Cvtt(angleEnd);
                float c2 = FxMath.CosTableAt(b), s2 = FxMath.SinTableAt(b);
                int argb = m_argb;
                M.F32(face, ic + m_pos_x);
                M.F32(face + 4, m_pos_y - isn);
                M.U32(face + 8, ZNear); M.U32(face + 0xc, RhwOne);
                M.I32(face + 0x10, argb);
                M.F32(face + 0x18, u0);
                M.F32(face + 0x1c, 1.0f);
                M.F32(face + 0x20, c2 * f_348 + m_pos_x);
                M.U32(face + 0x28, ZNear);
                M.U32(face + 0x2c, RhwOne);
                M.F32(face + 0x24, m_pos_y - s2 * f_348);
                float u1 = u0 + 0.25f;
                M.I32(face + 0x30, m_argb);
                M.U32(face + 0x3c, 0);
                M.F32(face + 0x38, u1);
                M.F32(face + 0x40, c1 * f_348 + m_pos_x);
                M.U32(face + 0x48, ZNear);
                M.U32(face + 0x4c, RhwOne);
                M.F32(face + 0x44, m_pos_y - s1 * f_348);
                M.I32(face + 0x50, m_argb);
                M.F32(face + 0x58, u0);
                M.U32(face + 0x5c, 0);
                M.U32(face + 0x60, M.U32((uint)f_144));
                rt.Renderer.AddRP(face, 1);

                uint f2 = face + 0x6c;
                M.F32(f2, ic + m_pos_x);
                M.F32(f2 + 4, m_pos_y - isn);
                M.U32(f2 + 8, ZNear); M.U32(f2 + 0xc, RhwOne);
                M.I32(f2 + 0x10, m_argb);
                M.F32(f2 + 0x18, u0);
                M.F32(f2 + 0x1c, 1.0f);
                M.F32(f2 + 0x20, c2 * inner + m_pos_x);
                M.F32(f2 + 0x24, m_pos_y - s2 * inner);
                M.U32(f2 + 0x28, ZNear); M.U32(f2 + 0x2c, RhwOne);
                M.I32(f2 + 0x30, m_argb);
                M.F32(f2 + 0x38, u1);
                M.F32(f2 + 0x3c, 1.0f);
                M.F32(f2 + 0x40, c2 * f_348 + m_pos_x);
                M.F32(f2 + 0x44, m_pos_y - s2 * f_348);
                M.U32(f2 + 0x48, ZNear); M.U32(f2 + 0x4c, RhwOne);
                M.I32(f2 + 0x50, m_argb);
                M.F32(f2 + 0x58, u1);
                M.U32(f2 + 0x5c, 0);
                M.U32(f2 + 0x60, M.U32((uint)f_144));
                rt.Renderer.AddRP(f2, 1);

                u0 = u1;
                if (u1 >= 1.0f) u0 = 0f;
                step = f_288;
                face += 0xd8;
                angle = step + angle;
                if (!(limit >= angle)) break;
            }
        }

        // ---- type 0xe ----------------------------------------------------------------------

        /// <summary>Type 0xe OnProcess case (0xad608b): follows master + offset; radius f_348 grows; spin f_30c.</summary>
        [FxPrimProcess(0xe)]
        public FxCaseResult Case0E(FxRuntime rt)
        {
            RagEffect master = m_master;
            float my = master.m_pos_y, mz = master.m_pos_z;
            f_37c = master.m_pos_x + f_394;
            f_380 = my + f_398;
            f_384 = mz + f_39c;
            M.U32(A + 0x10, M.U32(A + 0x37c));
            M.U32(A + 0x14, M.U32(A + 0x380));
            M.U32(A + 0x18, M.U32(A + 0x384));
            float v = f_350 + f_34c;
            f_34c = v;
            f_348 = v + f_348;
            f_30c = f_330 + f_30c;
            UpdateAlpha_aed050();
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0xe)]
        public void Render0E(FxRuntime rt, uint viewMatrix) => Render_af3870(rt, viewMatrix);

        [ThreadStatic] static FxMemory sphereScratch;

        /// <summary>
        /// CEffectPrim::fn_af3870 (0xaf3870) — a textured sphere of radius f_348 around the position: latitude
        /// −90..90 and longitude 0..360 in f_288-degree steps (longitude spun by f_30c), each cell projected
        /// (ProjectVertex 0x53b370) into two AddRP triangles. The four corner matrices live on the client's
        /// stack; here in a private scratch memory so the heap stays untouched.
        /// </summary>
        public void Render_af3870(FxRuntime rt, uint viewMatrix)
        {
            var S = sphereScratch ??= new FxMemory(0x100);
            uint mA = S.Base + 0x10, mB = mA + 0x30, mC = mB + 0x30, mD = mC + 0x30;
            FxRenderer r = rt.Renderer;
            float step = f_288;
            float du = 1.0f / (360.0f / step);
            float latLimit = 90.0f - step;
            float lonLimit = 360.0f - step;
            float dv = 1.0f / (180.0f / step);
            float v0 = 0f;
            float lat = -90.0f;
            if (latLimit < lat || float.IsNaN(latLimit)) return;
            int faceIndex = 0;
            while (true)
            {
                float v1 = dv + v0;
                float u = 0f, lon = 0f;
                if (!(lonLimit < 0f || float.IsNaN(lonLimit)))
                {
                    int latInt = FxOps.Cvtt(lat);
                    uint face = A + 0x2210 + (uint)faceIndex * 0x6c;
                    while (true)
                    {
                        float rLon = FxMath.DegToRad(FxOps.Cvtt(lon + f_30c));
                        float rLat = FxMath.DegToRad(latInt);
                        float rLon2 = FxMath.DegToRad(FxOps.Cvtt(lon + f_288 + 0.25f + f_30c));
                        float rLat2 = FxMath.DegToRad(FxOps.Cvtt(lat + f_288));
                        FxMatrix.MakeRotationX(S, mA, rLat);
                        FxMatrix.RotateY(S, mA, rLon);
                        Corner(S, mA, f_348, out float p1x, out float p1y, out float p1z);
                        FxMatrix.MakeRotationX(S, mB, rLat2);
                        FxMatrix.RotateY(S, mB, rLon);
                        Corner(S, mB, f_348, out float p2x, out float p2y, out float p2z);
                        FxMatrix.MakeRotationX(S, mC, rLat);
                        FxMatrix.RotateY(S, mC, rLon2);
                        Corner(S, mC, f_348, out float p3x, out float p3y, out float p3z);
                        FxMatrix.MakeRotationX(S, mD, rLat2);
                        FxMatrix.RotateY(S, mD, rLon2);
                        float rad = f_348, px = m_pos_x, py = m_pos_y, pz = m_pos_z;
                        p1x += px; p1y += py; p1z += pz;
                        p2x += px; p2y += py; p2z += pz;
                        p3x += px; p3y += py; p3z += pz;
                        Corner(S, mD, rad, out float p4x, out float p4y, out float p4z);
                        p4x += px; p4y += py; p4z += pz;

                        float u2 = u + du;
                        SphereVertex(r, viewMatrix, face, p1x, p1y, p1z, u, v0);
                        SphereVertex(r, viewMatrix, face + 0x20, p2x, p2y, p2z, u, v1);
                        SphereVertex(r, viewMatrix, face + 0x40, p3x, p3y, p3z, u2, v0);
                        M.U32(face + 0x60, M.U32((uint)f_144));
                        r.AddRP(face, 1);
                        uint f2 = face + 0x6c;
                        SphereVertex(r, viewMatrix, f2, p4x, p4y, p4z, u2, v1);
                        SphereVertex(r, viewMatrix, f2 + 0x20, p3x, p3y, p3z, u2, v0);
                        SphereVertex(r, viewMatrix, f2 + 0x40, p2x, p2y, p2z, u, v1);
                        M.U32(f2 + 0x60, M.U32((uint)f_144));
                        r.AddRP(f2, 1);

                        faceIndex += 2;
                        face += 0xd8;
                        u = u2;
                        lon = f_288 + lon;
                        if (!(lonLimit >= lon)) break;
                    }
                }
                lat = f_288 + lat;
                v0 = v1;
                if (!(latLimit >= lat)) break;
            }
            for (uint k = 0; k < 0x100; k += 4) S.U32(S.Base + k, 0);
        }

        /// <summary>Matrix × (0, 0, radius): ((m3·0 + m0·0) + m6·r) + m9 per axis.</summary>
        static void Corner(FxMemory S, uint m, float rad, out float x, out float y, out float z)
        {
            x = S.F32(m + 0xc) * 0f + S.F32(m) * 0f + S.F32(m + 0x18) * rad + S.F32(m + 0x24);
            y = S.F32(m + 0x10) * 0f + S.F32(m + 0x4) * 0f + S.F32(m + 0x1c) * rad + S.F32(m + 0x28);
            z = S.F32(m + 0x14) * 0f + S.F32(m + 0x8) * 0f + S.F32(m + 0x20) * rad + S.F32(m + 0x2c);
        }

        void SphereVertex(FxRenderer r, uint viewMatrix, uint dst, float x, float y, float z, float tu, float tv)
        {
            r.ProjectVertex(M, viewMatrix, x, y, z, out float sx, out float sy, out float sz, out float rhw);
            M.TlVertex(dst, sx, sy, sz, rhw, (uint)m_argb, tu, tv);
        }
    }
}
