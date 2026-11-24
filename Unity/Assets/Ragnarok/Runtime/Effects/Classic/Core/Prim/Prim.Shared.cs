// Synced from reference/port/src/RoClassicFx/Prim/Prim.Shared.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::SnapshotTrail (0xad58d0) — fills all f_1f4 trail entries (0x8c bytes each from +0x6b8)
        /// with the current state: position, +0x3b8..+0x3e4, f_348, f_318, f_30c, the 3x4 matrix at +0x160,
        /// the colour dwords +0x23c/+0x240/+0x244 and f_324 copied raw (plain MOVs in the asm — the decompiler's
        /// float conversions are wrong), and the packed ARGB cvtt(f_324)&lt;&lt;24 | r&lt;&lt;16 | g&lt;&lt;8 | b.
        /// </summary>
        public void SnapshotTrail_ad58d0()
        {
            for (int i = 0; i < f_1f4; i++)
            {
                uint e = A + 0x6b8 + (uint)i * 0x8c;
                M.U32(e + 0x0, M.U32(A + 0x10));
                M.U32(e + 0x4, M.U32(A + 0x14));
                M.U32(e + 0x8, M.U32(A + 0x18));
                for (uint k = 0; k < 12; k++) M.U32(e + 0xc + k * 4, M.U32(A + 0x3b8 + k * 4));
                M.U32(e + 0x3c, M.U32(A + 0x348));
                M.U32(e + 0x40, M.U32(A + 0x318));
                M.U32(e + 0x44, M.U32(A + 0x30c));
                for (uint k = 0; k < 12; k++) M.U32(e + 0x48 + k * 4, M.U32(A + 0x160 + k * 4));
                M.U32(e + 0x84, M.U32(A + 0x324));
                M.U32(e + 0x78, M.U32(A + 0x23c));
                M.U32(e + 0x7c, M.U32(A + 0x240));
                M.U32(e + 0x80, M.U32(A + 0x244));
                int argb = FxOps.Cvtt(M.F32(A + 0x324));
                argb = (argb << 8) + M.U8(A + 0x23c);
                argb = (argb << 8) + M.U8(A + 0x240);
                argb = (argb << 8) + M.U8(A + 0x244);
                M.I32(e + 0x88, argb);
            }
        }
    }
}
