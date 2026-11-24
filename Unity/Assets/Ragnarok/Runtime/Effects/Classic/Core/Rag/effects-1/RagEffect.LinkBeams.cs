// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.LinkBeams.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>effects-1: prim 0x26 "link" beams between the caster and a target actor (id in effect +0x184).</summary>
    public readonly partial struct RagEffect
    {
        // ---- EF 384 Soul Exhale (caster) / EF 395 Marionette Control (caster) ----------------------

        [FxEffectProcess(384)]
        public FxCaseResult Case_SoulExhaleCaster(FxRuntime rt)
        {
            if (m_stateCnt == 0) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff95f4, 0f);
            Fn_b7f230(rt);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(395)]
        public FxCaseResult Case_MarionetteCaster(FxRuntime rt)
        {
            if (m_stateCnt == 0) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff95f4, 0f);
            Fn_b7f450(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>CRagEffect::fn_b7f230 (0xb7f230) — link beam, width 0.2, height 2.</summary>
        public void Fn_b7f230(FxRuntime rt) => LinkBeam(rt, 0x3e4ccccd, 0x40000000);

        /// <summary>CRagEffect::fn_b7f450 (0xb7f450) — link beam, width 0.7, height 3.</summary>
        public void Fn_b7f450(FxRuntime rt) => LinkBeam(rt, 0x3f333333, 0x40400000);

        /// <summary>
        /// Body of fn_b7f230 / fn_b7f450: follows the owner; at tick 0 a prim 0x26 (alpha_center.tga) whose
        /// block 0 runs from the effect position to the target actor: the local player when the id equals
        /// DAT_0154f2b4, else CWorld::GetActorByAid(id). No target → block disabled.
        /// </summary>
        void LinkBeam(FxRuntime rt, uint widthBits, uint heightBits)
        {
            uint mode = rt.Mode;   // GetWorld() is called before anything else
            uint actor = 0;
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x26, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            SetFirstTexture(rt, p, "effect\\alpha_center.tga");
            uint a = p.A;
            M.U8(a + 0x3e8, 1);
            M.U32(a + 0x3f4, 0);
            M.U16(a + 0x3ea, 0);
            M.U32(a + 0x468, 0);
            M.U32(a + 0x3f0, widthBits);
            M.U32(a + 0x40c, heightBits);
            M.U32(a + 0x478, M.U32(A + 0x10));
            M.U32(a + 0x47c, M.U32(A + 0x14));
            M.U32(a + 0x480, M.U32(A + 0x18));
            M.U32(a + 0x3fc, M.U32(A + 0x184));
            float id = M.F32(a + 0x3fc);
            if (id > 0f)
            {
                uint world = M.U32(mode + 0xcc);
                float player = (float)((double)(int)rt.PlayerAid + (rt.PlayerAid >> 31 != 0 ? 4294967296.0 : 0.0));
                if (id == player) actor = M.U32(world + 0x2c);
                else actor = rt.FindActorByAid(FxOps.FloatToUInt_d48d60(id));
            }
            if (actor == 0) M.U8(a + 0x3e8, 0);
            else
            {
                M.U32(a + 0x490, M.U32(actor + 0x10));
                M.U32(a + 0x494, M.U32(actor + 0x14));
                M.U32(a + 0x498, M.U32(actor + 0x18));
            }
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
        }
    }
}
