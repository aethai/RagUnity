// Synced from reference/port/src/RoClassicFx/Rag/RagEffect.Shared.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>Small inlined idioms shared by many CRagEffect::OnProcess cases.</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>Owner position copied into the effect (m_master +0x10/+0x14/+0x18).</summary>
        void FollowMaster()
        {
            uint master = M.U32(A + 0x138);
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
        }

        /// <summary>rand() % n with the MSVC signed-modulo fix-up (CDQ/AND/ADD) — never negative.</summary>
        static int RandMod(FxRuntime rt, int n)
        {
            int r = rt.Rand.Next() % n;
            return r >= 0 ? r : 0;
        }
    }
}
