// Synced from reference/port/src/RoClassicFx/Rag/prims-4/RagEffect.Devil.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>prims-4: EF 475–484 (0x1db–0x1e4) — fn_b67ca0(kind 1..10), plus the owner-flag cases 446 / 458.</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b67ca0 (0xb67ca0) — follows the owner; at tick 0 launches one prim 0x47 (flag 0x200,
        /// duration = f_17c, two textures fullb.tga / white02.bmp), drops the effect 9 units, activates block 0
        /// with size 65 (kind 0) or 140 − 10·kind (+0x68 = 1).
        /// </summary>
        public void Fn_b67ca0(FxRuntime rt, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x47, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 |= 0x200;
            p.m_duration = f_17c;
            p.f_204 = 2;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture("effect\\fullb.tga"));
            M.U32((uint)p.f_144 + 4, rt.GetTexture("effect\\white02.bmp"));
            m_pos_y = m_pos_y - 9.0f;
            p.f_3e8 = 0;
            p.f_49c = 0;
            p.f_550 = 0;
            p.f_604 = 0;
            p.f_3e8 = 1;
            p.f_3f4 = 0;
            if (kind == 0)
            {
                p.f_468 = 65.0f;
                p.f_450 = 0;
            }
            else
            {
                p.f_468 = 140.0f - (float)(kind * 10);
                p.f_450 = 1;
            }
            p.f_3ea = 0;
        }

        /// <summary>EF 334 (0x14e, EF_DEVIL, vf14 case 0x22): fn_b67ca0(0) — the size-65 variant.</summary>
        [FxEffectProcess(0x14e)] public FxCaseResult Case_Devil0(FxRuntime rt) { Fn_b67ca0(rt, 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1db)] public FxCaseResult Case_Devil1(FxRuntime rt) { Fn_b67ca0(rt, 1); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1dc)] public FxCaseResult Case_Devil2(FxRuntime rt) { Fn_b67ca0(rt, 2); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1dd)] public FxCaseResult Case_Devil3(FxRuntime rt) { Fn_b67ca0(rt, 3); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1de)] public FxCaseResult Case_Devil4(FxRuntime rt) { Fn_b67ca0(rt, 4); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1df)] public FxCaseResult Case_Devil5(FxRuntime rt) { Fn_b67ca0(rt, 5); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1e0)] public FxCaseResult Case_Devil6(FxRuntime rt) { Fn_b67ca0(rt, 6); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1e1)] public FxCaseResult Case_Devil7(FxRuntime rt) { Fn_b67ca0(rt, 7); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1e2)] public FxCaseResult Case_Devil8(FxRuntime rt) { Fn_b67ca0(rt, 8); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1e3)] public FxCaseResult Case_Devil9(FxRuntime rt) { Fn_b67ca0(rt, 9); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1e4)] public FxCaseResult Case_Devil10(FxRuntime rt) { Fn_b67ca0(rt, 10); return FxCaseResult.Continue; }

        /// <summary>
        /// Case 446 (0x1be, Taekwon Jump landing, 0xb5765a): owner flag 0x20000 once (+0xf6 = tick, byte), then
        /// flag 0x40 once while tick &lt; 25 (+0xf4 = tick + 11).
        /// </summary>
        [FxEffectProcess(0x1be)]
        public FxCaseResult Case_JumpLanding(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x20000) == 0)
            {
                M.U32(master + 0xe8, flags + 0x20000);
                M.U8(M.U32(A + 0x138) + 0xf6, M.U8(A + 0x178));
                master = M.U32(A + 0x138);
            }
            flags = M.U32(master + 0xe8);
            if ((flags & 0x40) == 0 && m_stateCnt < 0x19)
            {
                M.U32(master + 0xe8, flags + 0x40);
                M.I16(M.U32(A + 0x138) + 0xf4, (short)(M.I16(A + 0x178) + 0xb));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Case 458 (0x1ca, Running, 0xb5753a): owner flag 0x2000 once, +0xf4 = tick.</summary>
        [FxEffectProcess(0x1ca)]
        public FxCaseResult Case_RunningStart(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x2000) == 0)
            {
                M.U32(master + 0xe8, flags + 0x2000);
                M.I16(M.U32(A + 0x138) + 0xf4, M.I16(A + 0x178));
            }
            return FxCaseResult.Continue;
        }
    }
}
