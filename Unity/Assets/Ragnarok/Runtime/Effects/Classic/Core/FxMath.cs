// Synced from reference/port/src/RoClassicFx/FxMath.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Degree-indexed trigonometry of the original client (InitSinCosTables 0x532290,
    /// GetSin 0x532480, GetCos 0x5323e0, DegToRad 0x532430).
    ///
    /// The six 361-entry tables are laid out contiguously exactly like the client's globals
    /// (rad @0x11a3ee0, -rad, sin, -sin, cos, -cos). Negative angles index the "-table"
    /// with a negative subscript, i.e. they read backwards into the preceding table.
    /// This is an original-client quirk (GetSin(-p) == sin(361 - p) degrees, not -sin(p))
    /// that must be preserved for 1:1 output.
    /// </summary>
    public static partial class FxMath
    {
        const int Entries = 361;
        const int RadBase = 0;
        const int NegRadBase = RadBase + Entries;
        const int SinBase = NegRadBase + Entries;
        const int NegSinBase = SinBase + Entries;
        const int CosBase = NegSinBase + Entries;
        const int NegCosBase = CosBase + Entries;

        static readonly float[] Tables = BuildTables();

        static float[] BuildTables()
        {
            var t = new float[Entries * 6];
            for (int i = 0; i < Entries; i++)
            {
                // SSE single precision: cvtdq2ps, divss 180.0f, mulss 3.141592f (verified against the oracle).
                float rad = (float)i / 180f * 3.141592f;
                // x87 fsin/fcos on the float value, stored back as float.
                float sin = (float)Math.Sin(rad);
                float cos = (float)Math.Cos(rad);
                t[RadBase + i] = rad;
                t[NegRadBase + i] = -rad;       // stored as bits ^ 0x80000000
                t[SinBase + i] = sin;
                t[NegSinBase + i] = -sin;       // bits ^ 0x80000000
                t[CosBase + i] = cos;
                t[NegCosBase + i] = -cos;
            }
            return t;
        }

        /// <summary>Signed modulo applied by the client only when |deg| exceeds 360.</summary>
        static int Wrap(int deg)
        {
            if (deg > 360 || deg < -360) deg -= deg / 360 * 360;
            return deg;
        }

        public static float GetSin(int deg)
        {
            deg = Wrap(deg);
            return deg < 0 ? Tables[NegSinBase + deg] : Tables[SinBase + deg];
        }

        public static float GetCos(int deg)
        {
            deg = Wrap(deg);
            return deg < 0 ? Tables[NegCosBase + deg] : Tables[CosBase + deg];
        }

        public static float DegToRad(int deg)
        {
            deg = Wrap(deg);
            return deg < 0 ? Tables[NegRadBase + deg] : Tables[RadBase + deg];
        }
    }
}
