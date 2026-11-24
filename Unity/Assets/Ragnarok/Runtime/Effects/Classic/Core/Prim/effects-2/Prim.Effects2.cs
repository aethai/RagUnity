// Synced from reference/port/src/RoClassicFx/Prim/effects-2/Prim.Effects2.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// FUN_00ad5710 (stdcall, 0xad5710) — direction (x, z) → integer degrees written as a short:
        /// r = sqrt(x²+z²) (FUN_00534e70); the first d in 0..180 with x/r ≥ cos(d) (cos table 0x11a5570,
        /// COMISS/JAE: ordered ≥) gives d, mirrored to 360-d when z/r &lt; 0 (or unordered); 180 if none.
        /// </summary>
        public static void DirToAngle_ad5710(FxMemory m, float x, float z, uint outShort)
        {
            float r = FxRuntime.SqrtF(x * x + z * z);
            float nz = z / r;
            float nx = x / r;
            for (short d = 0; d <= 0xb4; d++)
            {
                float c = FxMath.GetCos(d);
                if (nx >= c)   // JAE: CF=0 → ordered and not below
                {
                    ushort res = (ushort)d;
                    if (nz < 0f || float.IsNaN(nz)) res = (ushort)(0x168 - d);   // CMOVB: below or unordered
                    m.U16(outShort, res);
                    return;
                }
            }
            m.U16(outShort, 0xb4);
        }
    }
}
