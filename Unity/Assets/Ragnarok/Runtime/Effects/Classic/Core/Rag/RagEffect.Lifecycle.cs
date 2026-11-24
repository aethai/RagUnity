// Synced from reference/port/src/RoClassicFx/Rag/RagEffect.Lifecycle.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        static readonly uint[] CtorZeroDwords = { 0x4e, 0x5d, 0x5e, 0x5a, 0x5c, 0x58, 0x59 };

        /// <summary>CRagEffect::CRagEffect (0xb1eb30).</summary>
        public void Construct(FxRuntime rt)
        {
            rt.ConstructRenderObject(A);
            M.U32(A + 0x108, rt.DefaultEffectAct);   // effects-10: the ctor's default NPC\1_ETC_01.act (shared stub)
            M.U32(A, FxRuntime.VtCRagEffect);
            for (uint i = 0x60; i <= 0x6a; i++) M.U32(A + i * 4, 0);
            uint head = M.Alloc(0xc);
            M.U32(head, head);
            M.U32(head + 4, head);
            M.U32(A + 0x69 * 4, head);
            for (uint block = 0; block < 0x14; block++)
            {
                M.U32(A + (0x6b + block * 0x14) * 4, 0);
                for (uint i = 0; i < 5; i++) M.U32(A + (0x6c + block * 0x14 + i) * 4, 0);
            }
            // (the client's per-vertex init of the 0x200 RP faces at 0x107d * 4 — zeros and specular 0xff000000 — is
            // skipped: the memset of the same 0xd800 bytes below overwrites all of it before anything reads it)
            M.U8(A + 0x4728 * 4, 0);
            foreach (uint i in CtorZeroDwords) M.U32(A + i * 4, 0);
            M.U32(A + 0x5b * 4, 1);
            for (uint i = 0x60; i <= 0x68; i++) M.U32(A + i * 4, 0);
            M.U32(A + 0x1fb * 4, 0);
            M.U32(A + 0x1fc * 4, 0);
            M.U32(A + 0x467d * 4, 0);
            M.Fill(A + 0x469f * 4, 0, 0x200);
            M.Fill(A + 0x107d * 4, 0, 0xd800);
            for (uint i = 0x4722; i <= 0x4725; i++) M.U32(A + i * 4, 0xffffffff);
            M.U32(A + 0x471f * 4, 0);
            M.U32(A + 0x4720 * 4, 0);
            M.U8(A + 0x4726 * 4, 0);
            M.U32(A + 0x4721 * 4, 0);
            M.U32(A + 0x4727 * 4, 0);
        }
    }
}
