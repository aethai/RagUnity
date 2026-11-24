// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.Effects1.Common.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>Small idioms shared by the effects-1 handlers.</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// The texture-array idiom: <c>prim.f_144 = new[](f_204 * 4); *prim.f_144 = GetTexture(name, 0, 0, 1, 0)</c>
        /// (operator new[] 0xd496f5 with the 64-bit size clamp, then GetTexture 0x54f5c0).
        /// </summary>
        static void SetFirstTexture(FxRuntime rt, Prim p, string name)
        {
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            rt.M.U32((uint)p.f_144, rt.GetTexture(name));
        }
    }
}
