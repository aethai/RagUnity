// Synced from reference/port/src/RoClassicFx/Prim/prims-4/Prim.Type47.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// prims-4: prim type 0x47 — the "devil" cross / halo (EF 475–484, fn_b67ca0). Four particle blocks of
    /// 0xb4 bytes at +0x3e8; only the first is used by the handler. Process is the inline case of
    /// CEffectPrim::OnProcess (0xadaa2f), render is CEffectPrim::fn_af7c60.
    /// </summary>
    public readonly partial struct Prim
    {
        // ------------------------------------------------------------------ 0x47 process (0xadaa2f)

        /// <summary>
        /// Case 0x47 of CEffectPrim::OnProcess (0xadaa2f): camera-facing angles (f_3ec / f_4a0 from the view),
        /// position = master; each active block (flag +0) bumps its counter (+0xc, int) and its alpha (+2, short)
        /// up to 255.
        /// </summary>
        [FxPrimProcess(0x47)]
        public FxCaseResult Case47(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            for (int k = 0; k < 4; k++)
            {
                uint b = Blk(k);
                if (M.I8(b) != 1) continue;
                M.I32(b + 0xc, M.I32(b + 0xc) + 1);
                short a = M.I16(b + 2);
                if (a < 0xff) M.I16(b + 2, (short)(a + 1));
            }
            return FxCaseResult.Continue;
        }

        // ------------------------------------------------------------------ 0x47 render (fn_af7c60)

        [FxPrimRender(0x47)]
        public void Render47(FxRuntime rt, uint viewMatrix) => Render_af7c60(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_af7c60 — per active block with alpha &gt; 0: four r×r quads (texture 0) tiling the
        /// square [-r, r]² around the origin and four (r+100)-wide strips (texture 1) around it, all rotated by
        /// FUN_00b0d9c0 (f_3ec / f_4a0), colour 10/10/10 or 30/30/30 once a block with +0x68 == 1 was seen
        /// (the colour locals persist across blocks), blend 1, relative, u/v 0..1.
        /// </summary>
        public void Render_af7c60(FxRuntime rt, uint matrix)
        {
            int count = 0;
            byte cr = 10, cg = 10, cb = 10;
            Span<float> p = stackalloc float[12];
            p.Clear();
            for (int k = 0; k < 4; k++)
            {
                uint b = Blk(k);
                if (M.I8(b) != 1) continue;
                short alpha = M.I16(b + 2);
                if (alpha <= 0) continue;
                if (M.I8(b + 0x68) == 1) { cr = 0x1e; cg = 0x1e; cb = 0x1e; }
                short ay = M.I16(A + 0x3ec), ax = M.I16(A + 0x4a0);
                float r;

                // quad 1: (0,0,0) (0,0,r) (r,0,r) (r,0,0)
                r = M.F32(b + 0x80);
                p.Clear();
                p[5] = r; p[6] = r; p[8] = r; p[9] = r;
                Rotate4_b0d9c0(p, ay, ax);
                Quad_b0a900(rt, ref count, alpha, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 1, 0, 0, cr, cg, cb, 0f, 1f, 0f, 1f, 0);

                // quad 2: (0,0,0) (0,0,-r) (r,0,-r) (r,0,0)
                r = M.F32(b + 0x80);
                p.Clear();
                p[5] = -r; p[6] = r; p[8] = -r; p[9] = r;
                Rotate4_b0d9c0(p, ay, ax);
                Quad_b0a900(rt, ref count, M.I16(b + 2), p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 1, 0, 0, cr, cg, cb, 0f, 1f, 0f, 1f, 0);

                // quad 3: (0,0,0) (0,0,r) (-r,0,r) (-r,0,0)
                r = M.F32(b + 0x80);
                p.Clear();
                p[5] = r; p[6] = -r; p[8] = r; p[9] = -r;
                Rotate4_b0d9c0(p, ay, ax);
                Quad_b0a900(rt, ref count, M.I16(b + 2), p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 1, 0, 0, cr, cg, cb, 0f, 1f, 0f, 1f, 0);

                // quad 4: (0,0,0) (0,0,-r) (-r,0,-r) (-r,0,0)
                r = -M.F32(b + 0x80);
                p.Clear();
                p[5] = r; p[6] = r; p[8] = r; p[9] = r;
                Rotate4_b0d9c0(p, ay, ax);
                Quad_b0a900(rt, ref count, M.I16(b + 2), p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 1, 0, 0, cr, cg, cb, 0f, 1f, 0f, 1f, 0);

                // strip 1 (+x): (r,0,-r) (r+100,0,-r) (r+100,0,r+100) (r,0,r+100)
                r = M.F32(b + 0x80);
                float r1 = r + 100.0f, nr = -r;
                p.Clear();
                p[0] = r; p[2] = nr; p[3] = r1; p[5] = nr; p[6] = r1; p[8] = r1; p[9] = r; p[11] = r1;
                Rotate4_b0d9c0(p, ay, ax);
                Quad_b0a900(rt, ref count, M.I16(b + 2), p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 1, 1, 0, cr, cg, cb, 0f, 1f, 0f, 1f, 0);

                // strip 2 (+z): (r,0,r) (r,0,r+100) (-(r+100),0,r+100) (-(r+100),0,r)
                r = M.F32(b + 0x80);
                r1 = r + 100.0f; float nr1 = -r1;
                p.Clear();
                p[0] = r; p[2] = r; p[3] = r; p[5] = r1; p[6] = nr1; p[8] = r1; p[9] = nr1; p[11] = r;
                Rotate4_b0d9c0(p, ay, ax);
                Quad_b0a900(rt, ref count, M.I16(b + 2), p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 1, 1, 0, cr, cg, cb, 0f, 1f, 0f, 1f, 0);

                // strip 3 (-x): (-r,0,r) (-(r+100),0,r) (-(r+100),0,-(r+100)) (-r,0,-(r+100))
                r = M.F32(b + 0x80);
                r1 = r + 100.0f; nr = -r; nr1 = -r1;
                p.Clear();
                p[0] = nr; p[2] = r; p[3] = nr1; p[5] = r; p[6] = nr1; p[8] = nr1; p[9] = nr; p[11] = nr1;
                Rotate4_b0d9c0(p, ay, ax);
                Quad_b0a900(rt, ref count, M.I16(b + 2), p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 1, 1, 0, cr, cg, cb, 0f, 1f, 0f, 1f, 0);

                // strip 4 (-z): (-r,0,-r) (-r,0,-(r+100)) (r+100,0,-(r+100)) (r+100,0,-r)
                r = M.F32(b + 0x80);
                nr = -r; r1 = r + 100.0f; nr1 = -r1;
                p.Clear();
                p[0] = nr; p[2] = nr; p[3] = nr; p[5] = nr1; p[6] = r1; p[8] = nr1; p[9] = r1; p[11] = nr;
                Rotate4_b0d9c0(p, ay, ax);
                Quad_b0a900(rt, ref count, M.I16(b + 2), p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11],
                    matrix, 1, 1, 0, cr, cg, cb, 0f, 1f, 0f, 1f, 0);
            }
        }
    }
}
