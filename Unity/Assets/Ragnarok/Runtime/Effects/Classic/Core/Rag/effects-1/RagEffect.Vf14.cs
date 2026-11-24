// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.Vf14.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>effects-1: ids 300..373 (CRagEffect::vf14 0xb46290; its bool result decides liveness).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>vf14 case 39 — Spiral Pierce: fn_b81210(ring_yellow), always alive.</summary>
        [FxEffectProcess(339)]
        public FxCaseResult Case_SpiralPierce(FxRuntime rt)
        {
            Fn_b81210(rt, "effect\\ring_yellow.tga");
            return FxCaseResult.Continue;
        }

        /// <summary>vf14 case 69 — Concentration (LK): sound at tick 0, owner tint (255,255,160), STR (result ignored).</summary>
        [FxEffectProcess(369)]
        public FxCaseResult Case_LkConcentration(FxRuntime rt)
        {
            if (m_stateCnt == 0)
                rt.PlaySound3DRelative("effect\\knight_twohandquicken.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            rt.SetArgb(M.U32(A + 0x138), -1, 0xff, 0xff, 0xa0);
            rt.StrPlayer(this, 0f, -80.0f, 0f);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b81210 (0xb81210) — follows the owner; sounds at ticks 20 and 30; for ticks 25..55,
        /// every 3rd tick (at most 10 times, counter +0x170) a spiralling ring prim 0x49 with two blocks
        /// whose radius shrinks (20 - 0.7k) and height grows (0.2k + 5), k = tick - 25.
        /// </summary>
        public void Fn_b81210(FxRuntime rt, string texture)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t == 0) M.I32(A + 0x170, 0);
            else if (t == 0x14) rt.PlaySound3DRelative("permeter_attack.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            else if (t == 0x1e) rt.PlaySound3DRelative("effect\\EF_MagnumBreak.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            t = m_stateCnt;
            int k = t - 0x19;
            if ((uint)k > 0x1eu) return;
            if (t % 3 != 0) return;
            M.I32(A + 0x170, M.I32(A + 0x170) + 1);
            if (M.I32(A + 0x170) >= 0xb) return;
            Prim p = LaunchEffectPrim(rt, 0x49, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            SetFirstTexture(rt, p, texture);
            M.U32(p.A + 0x318, 0x40a00000);
            for (uint i = 0; i < 4; i++) M.U8(p.A + 0x3e8 + i * 0xb4, 0);
            float radius = 20.0f - (float)k * 0.7f;
            float height = (float)k * 0.2f + 5.0f;
            int k6 = (k & 0xffff) * 6;
            for (int i = 0; i < 2; i++)
            {
                uint b = p.A + 0x3e8 + (uint)i * 0xb4;
                M.U8(b + 0x0, 1);
                M.U32(b + 0xc, 0);
                M.U16(b + 0x6, 0x168);
                M.F32(b + 0x8, radius);
                M.U16(b + 0x10, (ushort)(k6 + i * 9));
                M.U16(b + 0x2, 0);
                M.F32(b + 0x80, height);
                M.U16(b + 0x7e, (ushort)((0x23 - i * 4 - (k & 0xffff)) * 2));
                M.U16(b + 0x4, (ushort)i);
            }
        }
    }
}
