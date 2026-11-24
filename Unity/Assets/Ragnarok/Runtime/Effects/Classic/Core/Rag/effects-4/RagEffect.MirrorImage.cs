// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.MirrorImage.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b81560 (0xb81560) — Mirror Image: tints the owner (155, 155, 255) every tick and every
        /// 20 ticks spawns a CBlurPC after-image of the owner (operator new 0x508 + FUN_00cbda20 + SendMsg vf02),
        /// linked into the world's render-object list (world +8, 0x783f90). Tick 9998 wraps to 0 (endless).
        /// The effect position fields are reused as the after-image colour.
        /// </summary>
        public void Fn_b81560(FxRuntime rt)
        {
            m_pos_x = 155.0f;
            m_pos_z = 255.0f;
            m_pos_y = 155.0f;
            MasterArgb(rt, -1, 0x9b, 0x9b, 0xff);
            int t = m_stateCnt;
            if (t == 0x270e) m_stateCnt = 0;
            else if (t % 0x14 != 0) return;
            uint blur = M.Alloc(0x508);
            if (blur != 0) rt.ConstructBlurPc_cbda20(blur, M.U32(A + 0x138));
            long z = FxOps.Cvtt(m_pos_z);
            long y = FxOps.Cvtt(m_pos_y);
            long x = FxOps.Cvtt(m_pos_x);
            rt.BlurPcSendMsg_cc13a0(blur, A, 0, 0, x, y, z);
            rt.ListPushBack(M.U32(rt.Mode + 0xcc) + 0x8, blur);
        }

        /// <summary>Case 617 (0x269, vf15) — Mirror Image.</summary>
        [FxEffectProcess(0x269)]
        public FxCaseResult Case_MirrorImage(FxRuntime rt)
        {
            Fn_b81560(rt);
            return FxCaseResult.Continue;
        }
    }
}
