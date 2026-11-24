// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.ActorTint.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-4: cases that drive the owner actor's render state — the per-frame flag word at owner +0xe8
    /// (bits are added once per tick; OnProcess's prologue clears the word when owner +0xec == 1), the
    /// counters at +0xf0.. and the colour via CRenderObject::SetARGB (0xbe3d40).
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary><c>if ((master[0xe8] &amp; bit) == 0) master[0xe8] += bit;</c></summary>
        void AddMasterFlag(uint bit)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & bit) == 0) M.U32(master + 0xe8, f + bit);
        }

        /// <summary>SetARGB on the owner (thiscall, ECX = m_master).</summary>
        void MasterArgb(FxRuntime rt, int a, int r, int g, int b) => rt.SetArgb(M.U32(A + 0x138), a, r, g, b);

        /// <summary>Case 379 (0x17b) — Indulge: owner tint (205-t, 205-t, 255) for t &lt; 201.</summary>
        [FxEffectProcess(0x17b)]
        public FxCaseResult Case_Indulge(FxRuntime rt)
        {
            if ((uint)m_stateCnt < 0xc9)
            {
                int v = 0xcd - m_stateCnt;
                MasterArgb(rt, -1, v, v, 0xff);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// Case 387 (0x183) — Falcon Assault: CGameMode::SendMsg(0x7a) at tick 30, owner spins 30°/tick for
        /// t in [30, 55), flag 1 on the first half of every 10 ticks in [30, 51).
        /// </summary>
        [FxEffectProcess(0x183)]
        public FxCaseResult Case_FalconAssault(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x1e)
            {
                rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
                t = m_stateCnt;
            }
            if ((uint)(t - 0x1e) < 0x19)
            {
                uint master = M.U32(A + 0x138);
                M.F32(master + 0x4c, M.F32(master + 0x4c) + 30.0f);
                float r = M.F32(M.U32(A + 0x138) + 0x4c);
                if (360.0f < r) M.F32(M.U32(A + 0x138) + 0x4c, r - 360.0f);
            }
            if ((uint)(m_stateCnt - 0x1e) < 0x15)
            {
                if (4 < m_stateCnt % 10) return FxCaseResult.Continue;
                AddMasterFlag(1);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Case 420 (0x1a4) — Eswoo: flag 0x10 and the counter byte owner +0xf0 = t (t &lt; 46).</summary>
        [FxEffectProcess(0x1a4)]
        public FxCaseResult Case_Eswoo(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x10) == 0)
            {
                M.U32(master + 0xe8, f + 0x10);
                if (m_stateCnt < 0x2e) M.U8(M.U32(A + 0x138) + 0xf0, (byte)m_stateCnt);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Case 424 (0x1a8) — Spirit link: flag 0x800 and owner +0xf2 = min(t, 200).</summary>
        [FxEffectProcess(0x1a8)]
        public FxCaseResult Case_Spirit(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x800) == 0)
            {
                M.U32(master + 0xe8, f + 0x800);
                byte v = 200;
                if (m_stateCnt < 200) v = (byte)m_stateCnt;
                M.U8(M.U32(A + 0x138) + 0xf2, v);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Case 442 (0x1ba) — Running: flag 0x8000 and owner +0xf7 = t.</summary>
        [FxEffectProcess(0x1ba)]
        public FxCaseResult Case_Running(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x8000) == 0)
            {
                M.U32(master + 0xe8, f + 0x8000);
                M.U8(M.U32(A + 0x138) + 0xf7, (byte)m_stateCnt);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// Case 500 (0x1f4, vf15) — Full Protection / Tarot: for t ≤ 30 flag 8, and blue (0,0,255) with flag 1
        /// on even ticks, white on odd ticks (code_r0x00b47d67 / code_r0x00b4e168).
        /// </summary>
        [FxEffectProcess(0x1f4)]
        public FxCaseResult Case_FullProtection(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (0x1e < t) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 8) == 0)
            {
                M.U32(master + 0xe8, f + 8);
                t = m_stateCnt;
            }
            if ((t & 1) == 0)
            {
                AddMasterFlag(1);
                MasterArgb(rt, -1, 0, 0, 0xff);
            }
            else MasterArgb(rt, -1, 0xff, 0xff, 0xff);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// Case 602 (0x25a, vf15) — Close Confine: sound at tick 20; after it flag 0x2000, owner +0xf4 = (short)t,
        /// red tint (250,50,50) on even ticks, white on odd ticks.
        /// </summary>
        [FxEffectProcess(0x25a)]
        public FxCaseResult Case_CloseConfine(FxRuntime rt)
        {
            int t = m_stateCnt;
            bool at20 = false;
            if (t == 0x14)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\ef_hit6.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
                at20 = t == 0x14;
            }
            if (at20 || t < 0x14) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            M.U32(master + 0xe8, M.U32(master + 0xe8) | 0x2000);
            M.I16(M.U32(A + 0x138) + 0xf4, (short)m_stateCnt);
            if ((m_stateCnt & 1) == 0) MasterArgb(rt, -1, 0xfa, 0x32, 0x32);
            else MasterArgb(rt, -1, 0xff, 0xff, 0xff);
            return FxCaseResult.Continue;
        }
    }
}
