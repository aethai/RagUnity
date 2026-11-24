// Synced from reference/port/src/RoClassicFx/Rag/toprank/RagEffect.TopRank.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        // ---- EF 159 EF_TOPRANK -----------------------------------------------------------------------------

        /// <summary>
        /// EF 159 (0x9f, EF_TOPRANK) — the PvP rank mark under a player ranked 10th or better: OnProcess 0xb527f0 main
        /// switch case 0x9f (jump table 0xb5a19c → 0xb56002 .. 0xb561dc). Every tick the effect copies the owner's
        /// position (+0x138 → +0x10/+0x14/+0x18); on tick 0 and whenever stateCnt reaches 9998 (reset to 0) one flat
        /// effect\LockOn128.tga quad (prim 0xf: duration 9999, heading 0 turning 4.5° a tick, Z tilt 90° = lying on the
        /// ground, half size 4 × 4) coloured by the rank in f_174 (CRagEffect::SendMsg msg 0x2c, 0xb5e650 case 3):
        /// R 250, G = B = (10 − rank) · 25 — rank 11 alone gets (0, 250, 0); then the effect sits 2 above the ground
        /// under it (FUN_006c32a0 GetHeight of the world's GAT, x87 fsub of the float 2.0 at 0xf4b450).
        /// </summary>
        [FxEffectProcess(0x9f)]
        public FxCaseResult Case_TopRank(FxRuntime rt)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t == 0x270e)
            {
                m_stateCnt = 0;
                TopRankQuad_b56044(rt);
            }
            else if (t == 0) TopRankQuad_b56044(rt);
            float h = rt.GatGetHeight_6c32a0(M.U32(rt.World + 0x30), m_pos_x, m_pos_z);
            m_pos_y = (float)((double)h - 2.0);
            return FxCaseResult.Continue;
        }

        /// <summary>The quad launched at 0xb56044 (tick 0 and the 9998-tick restart).</summary>
        void TopRankQuad_b56044(FxRuntime rt)
        {
            Prim p = LaunchEffectPrim(rt, 0xf, 0.0f, 0.0f, 0.0f);
            m_lastPrim = p;
            p.m_duration = 0x270f;
            p.f_30c = 0.0f;
            p.f_330 = 4.5f;
            p.f_314 = 90.0f;
            p.f_354 = 4.0f;
            p.f_360 = 4.0f;
            if (f_174 == 0xb)
            {
                M.I32(p.A + 0x23c, 0);
                p.f_244 = 0;
                p.f_240 = 0xfa;
            }
            else
            {
                M.I32(p.A + 0x23c, 0xfa);
                p.f_244 = (10 - f_174) * 0x19;
                p.f_240 = (10 - f_174) * 0x19;
            }
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            uint tex = rt.GetTexture("effect\\LockOn128.tga");
            M.U32((uint)p.f_144, tex);
        }
    }
}
