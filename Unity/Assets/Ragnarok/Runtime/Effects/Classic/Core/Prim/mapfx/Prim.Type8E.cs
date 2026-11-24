// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type8E.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x8e — the animated water of EF 821 (launched by FUN_00ba0190): per live slot four textured quads around the
    /// prim position (117 textures: W_C_water 0..29, W_B_water 30..59, W_bubble 60..86, W_A_water 87..116), size +0x468, yaw
    /// +0x3f8, alpha +0x3ea, frame counter +0x3f4.
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>CEffectPrim::OnProcess case 0x8e (0xad897d): <c>fn_b122d0(this); break;</c>.</summary>
        [FxPrimProcess(0x8e)]
        public FxCaseResult Case8E(FxRuntime rt)
        {
            Process_b122d0();
            return FxCaseResult.Continue;
        }

        /// <summary>CEffectPrim::fn_b122d0 (0xb122d0) — each slot's counter (+0x3f4 / +0x4a8 / +0x55c / +0x610) +1 while its (signed) flag byte is &gt; 0.</summary>
        public void Process_b122d0()
        {
            for (uint s = A; s < A + 0x2d0; s += 0xb4)
                if ((sbyte)M.U8(s + 0x3e8) > 0) M.I32(s + 0x3f4, M.I32(s + 0x3f4) + 1);
        }

        /// <summary>CEffectPrim::Render case 0x8e (0xaf5f8d): fn_b1d9f0(this, viewMatrix).</summary>
        [FxPrimRender(0x8e)]
        public void Render8E(FxRuntime rt, uint viewMatrix) => Render_b1d9f0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_b1d9f0 (0xb1d9f0) — per slot with +0x3e8 == 1 and alpha +0x3ea &gt; 0: with L = +0x468, c / s = cos / sin
        /// [+0x3f8], h = −L/2, hp = L/2, four quads (fn_b0a900: relative to the prim position, blend 1, colour white, alpha
        /// +0x3ea, uv 0..1, mode 0) in the plane rotated by the yaw:
        /// (1) a horizontal plate at y = −2L from (h, 0) to (hp, L) — texture 87 + counter % 30 (W_A_water);
        /// (2) a vertical wall y 0..−L along x h..hp at depth 0 — texture counter % 30 (W_C_water);
        /// (3) the same wall y −L..−2L — texture 30 + counter % 30 (W_B_water);
        /// (4) a wall y −0..−0.6L along x ±0.47L at depth −1 — texture 60 + counter % 27 (W_bubble).
        /// Points are (c·x − s·z, y, s·x + c·z) with every ×0 / ×−0 term kept; the counter remainders are C remainders.
        /// </summary>
        public void Render_b1d9f0(FxRuntime rt, uint viewMatrix)
        {
            int count = 0;
            for (int k = 0; k < 4; k++)
            {
                uint s = A + (uint)k * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                short alpha = M.I16(s + 0x3ea);
                if (alpha <= 0) continue;
                int cnt = M.I32(s + 0x3f4);
                sbyte rem = (sbyte)(cnt % 0x1e);

                // quad 1
                short ang = M.I16(s + 0x3f8);
                float L = M.F32(s + 0x468);
                float sn = SinAt(ang), c = CosAt(ang);
                float h = L * -0.5f;                                                    // [0xf80830]
                float hp = L * 0.5f;                                                    // [0xf4a460]
                float s0 = sn * 0f, c0 = c * 0f;                                        // [0xf79a04]
                float y = -L + -L;
                Quad_b0a900(rt, ref count, alpha,
                    c * hp - s0, y, sn * hp + c0,
                    c * h - s0, y, sn * h + c0,
                    c * h - sn * L, y, sn * h + c * L,
                    c * hp - sn * L, y, sn * hp + c * L,
                    viewMatrix, 1, (sbyte)(byte)(rem + 0x57), 0, 0xff, 0xff, 0xff, 0f, 1f, 0f, 1f, 0);

                // quad 2
                ang = M.I16(s + 0x3f8);
                L = M.F32(s + 0x468);
                c = CosAt(ang); sn = SinAt(ang);
                h = L * -0.5f; hp = L * 0.5f;
                s0 = sn * 0f; c0 = c * 0f;
                Quad_b0a900(rt, ref count, alpha,
                    c * hp - s0, 0f, sn * hp + c0,
                    c * h - s0, 0f, sn * h + c0,
                    c * h - s0, -L, sn * h + c0,
                    c * hp - s0, -L, sn * hp + c0,
                    viewMatrix, 1, rem, 0, 0xff, 0xff, 0xff, 0f, 1f, 0f, 1f, 0);

                // quad 3
                ang = M.I16(s + 0x3f8);
                L = M.F32(s + 0x468);
                c = CosAt(ang); sn = SinAt(ang);
                h = L * -0.5f; hp = L * 0.5f;
                s0 = sn * 0f; c0 = c * 0f;
                float nl = -L;
                float y2 = nl + nl;
                Quad_b0a900(rt, ref count, alpha,
                    c * hp - s0, nl, sn * hp + c0,
                    c * h - s0, nl, sn * h + c0,
                    c * h - s0, y2, sn * h + c0,
                    c * hp - s0, y2, sn * hp + c0,
                    viewMatrix, 1, (sbyte)(byte)(rem + 0x1e), 0, 0xff, 0xff, 0xff, 0f, 1f, 0f, 1f, 0);

                // quad 4
                ang = M.I16(s + 0x3f8);
                L = M.F32(s + 0x468);
                c = CosAt(ang); sn = SinAt(ang);
                sbyte rem27 = (sbyte)(byte)((sbyte)(cnt % 0x1b) + 0x3c);
                float cm = c * -1f, sm = sn * -1f;                                      // [0xf4f14c]
                float l1 = L * -0.47f, l2 = L * 0.47f;                                  // [0xff75d4], [0xff75b4]
                float yTop = L * -0.6f, yBot = L * -0f;                                 // [0xff75d8], [0xff7594]
                float bx = c * l1 - sm, bz = sn * l1 + cm;
                float ax = c * l2 - sm, az = sn * l2 + cm;
                Quad_b0a900(rt, ref count, alpha,
                    ax, yBot, az,
                    bx, yBot, bz,
                    bx, yTop, bz,
                    ax, yTop, az,
                    viewMatrix, 1, rem27, 0, 0xff, 0xff, 0xff, 0f, 1f, 0f, 1f, 0);
            }
        }
    }
}
