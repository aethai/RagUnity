// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type80.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x80 — the animated flat torch / dust flame of EF 689 / 690 / 691 / 696 (launched by CRagEffect::fn_b713d0).
    /// Slot 0 (+0x3e8) only: counter +0x3f4, frame +0x450 into the texture array +0x144 (count +0x204), alpha +0x3ea,
    /// kind +0x454 (0 = dust: a frame every 8 ticks, else every 6), position +0x46c / +0x470 / +0x474.
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x80 (inline, 0xadae18): camera angles (+0x3ec / +0x4a0), position = the master's;
        /// slot 0 live (+0x3e8 == 1): counter +0x3f4 +1, every 8th (kind +0x454 == 0) / 6th tick the frame +0x450 +1, back
        /// to 0 once (signed) ≥ the texture count +0x204; alpha +0x3ea clamped to 0..255; draw position +0x46c / +0x474 =
        /// the prim's x / z, +0x470 = +0x488 (the launch y). <c>break;</c>
        /// </summary>
        [FxPrimProcess(0x80)]
        public FxCaseResult Case80(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            if (M.U8(A + 0x3e8) != 1) return FxCaseResult.Continue;
            int cnt = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, cnt);
            int period = (M.U8(A + 0x454) == 0 ? 1 : 0) * 2 + 6;
            if (cnt % period == 0)
            {
                byte f = (byte)(M.U8(A + 0x450) + 1);
                M.U8(A + 0x450, f);
                if ((sbyte)f >= M.I32(A + 0x204)) f = 0;                                  // CMOVGE with the zero remainder
                M.U8(A + 0x450, f);
            }
            short alpha = M.I16(A + 0x3ea);
            if (alpha >= 0xff) M.I16(A + 0x3ea, 0xff);
            else if (alpha < 0) M.I16(A + 0x3ea, 0);
            M.U32(A + 0x470, M.U32(A + 0x488));
            M.U32(A + 0x46c, M.U32(A + 0x10));
            M.U32(A + 0x474, M.U32(A + 0x18));
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CEffectPrim::Render case 0x80 (with 0x8c): fn_afcca0 (effects-9) — a camera-facing four-corner quad of size
        /// +0x468 at +0x46c, texture = frame +0x450, style +0x452 (fn_b713d0: 4 — five tinted copies, see Render_afcca0).
        /// </summary>
        [FxPrimRender(0x80)]
        public void Render80(FxRuntime rt, uint viewMatrix) => Render_afcca0(rt, viewMatrix);
    }
}
