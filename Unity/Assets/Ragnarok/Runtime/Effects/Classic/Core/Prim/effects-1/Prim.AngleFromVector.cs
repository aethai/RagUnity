// Synced from reference/port/src/RoClassicFx/Prim/effects-1/Prim.AngleFromVector.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// FUN_00ad5710 — integer heading (degrees, [0, 360)) of the ground vector (dx, dz) found by scanning the
        /// client's cos table: the first i in 0..180 with dx/len ≥ cos(i); 360 - i when dz/len &lt; 0 (or NaN);
        /// 180 when nothing matches (NaN input). len goes through FUN_00534e70 (x87 sqrt, stored to float).
        /// </summary>
        public static ushort Fn_ad5710(float dx, float dz)
        {
            float sq = dx * dx;
            sq += dz * dz;
            float len = (float)System.Math.Sqrt((double)sq);
            float nz = dz / len;
            float nx = dx / len;
            for (short i = 0; i <= 0xb4; i++)
            {
                if (nx >= FxMath.GetCos(i))
                    return (nz < 0f || float.IsNaN(nz)) ? (ushort)(0x168 - i) : (ushort)i;
            }
            return 0xb4;
        }
    }
}
