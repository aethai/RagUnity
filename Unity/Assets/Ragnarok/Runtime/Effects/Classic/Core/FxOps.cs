// Synced from reference/port/src/RoClassicFx/FxOps.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>x86 semantics that C# does not reproduce by default.</summary>
    public static partial class FxOps
    {
        /// <summary>cvttss2si: truncation; NaN or out-of-range yields 0x80000000 ("integer indefinite").</summary>
        public static int Cvtt(float v)
        {
            if (float.IsNaN(v) || v >= 2147483648f || v < -2147483648f) return int.MinValue;
            return (int)v;
        }

        /// <summary>cvttsd2si for double sources.</summary>
        public static int Cvtt(double v)
        {
            if (double.IsNaN(v) || v >= 2147483648.0 || v < -2147483648.0) return int.MinValue;
            return (int)v;
        }
    }
}
