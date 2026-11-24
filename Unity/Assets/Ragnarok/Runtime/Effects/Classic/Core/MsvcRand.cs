// Synced from reference/port/src/RoClassicFx/MsvcRand.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>MSVC CRT rand()/srand(): holdrand = holdrand * 214013 + 2531011; return (holdrand >> 16) &amp; 0x7fff.</summary>
    public sealed class MsvcRand
    {
        uint holdrand;

        public MsvcRand(uint seed = 1) { holdrand = seed; }

        public void Seed(uint seed) => holdrand = seed;

        public uint State => holdrand;

        public int Next()
        {
            holdrand = unchecked(holdrand * 214013u + 2531011u);
            return (int)((holdrand >> 16) & 0x7fff);
        }
    }
}
