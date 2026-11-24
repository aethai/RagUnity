// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type8F.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x8f — the waterfall curtain of EF 838 (launched by CRagEffect::fn_b93c20): per live slot one flat quad (a square of
    /// half-diagonal R turned by +0x3f8 in the XZ plane at height +0x3fc) with a texture animation (frame +0x400, step every 4 ticks),
    /// a breathing size from +0x468 and the pulse angle +0x466. +0x404 == 0 spins it (yaw +6, pulse +3 a tick); +0x404 == 1 is
    /// static and additive.
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>CEffectPrim::OnProcess case 0x8f (0xad9f0c): <c>fn_b0ed30(this); break;</c>.</summary>
        [FxPrimProcess(0x8f)]
        public FxCaseResult Case8F(FxRuntime rt)
        {
            Process_b0ed30();
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CEffectPrim::fn_b0ed30 (0xb0ed30) — position = the master's; per slot with +0x3e8 == 1: counter +0x3f4 +1; when +0x404 ==
        /// 0.0 (UCOMISS / LAHF / TEST AH,0x44 / JP: NaN does not spin) the yaw +0x3f8 += 6 and the pulse +0x466 += 3 (16-bit, −360 once
        /// ≥ 360); every 4th counter value (C remainder == 0) frame +0x400 += 1.0; frame ≥ (float)+0x204 → frame −= count (COMISS /
        /// JB). Returns true.
        /// </summary>
        public bool Process_b0ed30()
        {
            FollowMaster_P1();
            for (uint s = A; s < A + 0x2d0; s += 0xb4)
            {
                if (M.U8(s + 0x3e8) != 1) continue;
                int cnt = M.I32(s + 0x3f4) + 1;
                M.I32(s + 0x3f4, cnt);
                if (M.F32(s + 0x404) == 0f)
                {
                    short a = (short)(M.I16(s + 0x3f8) + 6);
                    M.I16(s + 0x3f8, a);
                    if (a >= 0x168) M.I16(s + 0x3f8, (short)(a - 0x168));
                    short b = (short)(M.I16(s + 0x466) + 3);
                    M.I16(s + 0x466, b);
                    if (b >= 0x168) M.I16(s + 0x466, (short)(b - 0x168));
                }
                float frame = M.F32(s + 0x400);
                if (cnt % 4 == 0)
                {
                    frame = frame + 1f;
                    M.F32(s + 0x400, frame);
                }
                float n = (float)M.I32(A + 0x204);
                if (!(frame < n) && !float.IsNaN(frame) && !float.IsNaN(n)) M.F32(s + 0x400, frame - n);
            }
            return true;
        }

        /// <summary>CEffectPrim::Render case 0x8f (0xaf5447): fn_b13530(this, viewMatrix).</summary>
        [FxPrimRender(0x8f)]
        public void Render8F(FxRuntime rt, uint viewMatrix) => Render_b13530(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b13530 (0xb13530) — per slot with +0x3e8 == 1: R = (double)((sin[+0x466] + 1) · (L · 0.1)) + (double)L ·
        /// 0.8 (L = +0x468, SSE2 double), corner angles a0 = +0x3f8 and a(i+1) = a(i) + 90 (−270 instead once a(i) + 90 ≥ 360); the
        /// corners (float)(cos·R), y = +0x3fc, (float)(sin·R) are stored in the slot (A +0x484.., B +0x46c.., C +0x478.., D
        /// +0x490..) and drawn as one quad (fn_b0a900: relative to the prim position, texture (int)+0x400, blend 1 when +0x404 ==
        /// 1.0 else 0, colour white, alpha +0x3ea, uv 0..1, mode 0). No alpha test.
        /// </summary>
        public void Render_b13530(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            for (uint s = A; s < A + 0x2d0; s += 0xb4)
            {
                if (M.U8(s + 0x3e8) != 1) continue;
                int pulse = M.I16(s + 0x466);
                float L = M.F32(s + 0x468);
                int a0 = M.I16(s + 0x3f8);
                float f1 = (SinAt(pulse) + 1f) * (L * 0.1f);                             // [0xf4a468], [0xf7cff8]
                double R = (double)f1 + (double)L * 0.8;                                 // [0xf82ba0] (double)
                int a1 = (a0 < 0x10e ? 0x5a : -0x10e) + a0;
                int a2 = (a1 + 0x5a < 0x168 ? 0x5a : -0x10e) + a1;
                int a3 = (a2 + 0x5a < 0x168 ? 0x5a : -0x10e) + a2;
                float y = M.F32(s + 0x3fc);
                M.F32(s + 0x484, (float)((double)CosAt(a0) * R));
                M.F32(s + 0x48c, (float)((double)SinAt(a0) * R));
                M.F32(s + 0x488, y);
                M.F32(s + 0x470, y);
                M.F32(s + 0x46c, (float)((double)CosAt(a1) * R));
                M.F32(s + 0x474, (float)((double)SinAt(a1) * R));
                M.F32(s + 0x47c, y);
                M.F32(s + 0x478, (float)((double)CosAt(a2) * R));
                M.F32(s + 0x480, (float)((double)SinAt(a2) * R));
                M.F32(s + 0x494, y);
                M.F32(s + 0x490, (float)((double)CosAt(a3) * R));
                M.F32(s + 0x498, (float)((double)SinAt(a3) * R));
                sbyte tex = (sbyte)FxOps.Cvtt(M.F32(s + 0x400));
                byte blend = M.F32(s + 0x404) == 1f ? (byte)1 : (byte)0;
                Quad_b0a900(rt, ref count, M.I16(s + 0x3ea),
                    M.F32(s + 0x484), M.F32(s + 0x488), M.F32(s + 0x48c),
                    M.F32(s + 0x46c), M.F32(s + 0x470), M.F32(s + 0x474),
                    M.F32(s + 0x478), M.F32(s + 0x47c), M.F32(s + 0x480),
                    M.F32(s + 0x490), M.F32(s + 0x494), M.F32(s + 0x498),
                    viewMatrix, blend, tex, 0, 0xff, 0xff, 0xff, 0f, 1f, 0f, 1f, 0);
            }
        }
    }
}
