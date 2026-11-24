// Synced from reference/port/src/RoClassicFx/Rag/effects-2/RagEffect.Effects2.Common.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>Small idioms shared by the effects-2 handlers (each one is inlined identically in the client).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>m_pos = master position (the common "follow the owner" prologue).</summary>
        void E2_FollowMaster()
        {
            uint master = M.U32(A + 0x138);
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
        }

        /// <summary>PlaySound3D(wav, pos.x - player.x, y, pos.z - player.z, 0xfa, 0x28, 1.0, 0).</summary>
        void E2_Sound(FxRuntime rt, string wav, float y = 0f)
            => rt.PlaySound3DRelative(wav, m_pos_x, y, m_pos_z, 0xfa, 0x28, 1.0f, 0);

        /// <summary>m_lastPrim = LaunchEffectPrim(type, 0, 0, 0).</summary>
        Prim E2_Launch(FxRuntime rt, int type)
        {
            Prim p = LaunchEffectPrim(rt, type, 0f, 0f, 0f);
            m_lastPrim = p;
            return p;
        }

        /// <summary>prim->f_144 = operator new[](prim->f_204 * 4) (with the client's overflow clamp).</summary>
        static void E2_AllocTextures(FxRuntime rt, Prim p) => p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);

        /// <summary>((uint*)prim->f_144)[index] = GetTexture(name).</summary>
        static void E2_SetTexture(FxRuntime rt, Prim p, int index, string name)
        {
            uint tex = rt.GetTexture(name);
            p.M.U32((uint)p.f_144 + (uint)index * 4, tex);
        }

        /// <summary>rand() % n clamped at 0 (the client's "x = 0; if (-1 &lt; r % n) x = r % n" idiom).</summary>
        static int E2_Rand(FxRuntime rt, int n)
        {
            int r = rt.Rand.Next() % n;
            return r >= 0 ? r : 0;
        }
    }
}
