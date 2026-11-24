// Synced from reference/port/src/RoClassicFx/Rag/effects-6/RagEffect.Effects6.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-6 (post-merge additions): the tracker skills whose ids the first nine packages left out.
    /// Case arguments come from the main jump table (0xb5a19c) blocks, disassembled with port/tools/Disasm.
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 277 (0x115) Dissonance — bottom ring of ring_blue.tga (fn_b6bce0 variant 0).</summary>
        [FxEffectProcess(0x115)]
        public FxCaseResult Case_Dissonance(FxRuntime rt) { Fn_b6bce0(rt, "effect\\ring_blue.tga", 0); return FxCaseResult.Continue; }

        /// <summary>EF 290 (0x122) Ugly Dance — bottom ring of ring_red.tga (fn_b6bce0 variant 0).</summary>
        [FxEffectProcess(0x122)]
        public FxCaseResult Case_UglyDance(FxRuntime rt) { Fn_b6bce0(rt, "effect\\ring_red.tga", 0); return FxCaseResult.Continue; }

        /// <summary>EF 405 (0x195) Fog Wall — ground circle of ring_white.tga (fn_b6b080 variant 2).</summary>
        [FxEffectProcess(0x195)]
        public FxCaseResult Case_FogWall(FxRuntime rt) { Fn_b6b080(rt, "effect\\ring_white.tga", 2); return FxCaseResult.Continue; }

        /// <summary>EF 467–473 (0x1d3–0x1d9) Mild Wind (Warm Wind) elements: fn_b68410(hanmoonN.tga, 0, 0); 471/472/473 use hanmoon7/5/6 (jump-table order, not numeric).</summary>
        [FxEffectProcess(0x1d3)] public FxCaseResult Case_MildWind1(FxRuntime rt) { Fn_b68410(rt, "effect\\hanmoon1.tga", 0, 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1d4)] public FxCaseResult Case_MildWind2(FxRuntime rt) { Fn_b68410(rt, "effect\\hanmoon2.tga", 0, 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1d5)] public FxCaseResult Case_MildWind3(FxRuntime rt) { Fn_b68410(rt, "effect\\hanmoon3.tga", 0, 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1d6)] public FxCaseResult Case_MildWind4(FxRuntime rt) { Fn_b68410(rt, "effect\\hanmoon4.tga", 0, 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1d7)] public FxCaseResult Case_MildWind5(FxRuntime rt) { Fn_b68410(rt, "effect\\hanmoon7.tga", 0, 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1d8)] public FxCaseResult Case_MildWind6(FxRuntime rt) { Fn_b68410(rt, "effect\\hanmoon5.tga", 0, 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x1d9)] public FxCaseResult Case_MildWind7(FxRuntime rt) { Fn_b68410(rt, "effect\\hanmoon6.tga", 0, 0); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b68410 (0xb68410) — follows the master; at tick <paramref name="startTick"/> (0 when the
        /// argument is 0) launches one prim type 0x44 (flags 5, duration f_17c, one texture) as a rising aura plane:
        /// f_468 = 18 (7 for kind 3) with f_3fc = 0 / f_454 = 1 when startTick ≤ 0, else f_468 = 5, f_3fc = 1, f_454 = 4;
        /// the two 0x28-byte slots at +0x400 and the byte +0x455 are cleared. The texture-by-id branches (kind 2, or
        /// startTick &gt; 0: FUN_006eaaf0 + sprintf) are not used by any registered id and are not ported.
        /// </summary>
        public void Fn_b68410(FxRuntime rt, string texture, short startTick, short kind)
        {
            FollowMaster();
            int start = startTick != 0 ? startTick : 0;
            if (m_stateCnt != start) return;
            if (kind == 2 || startTick > 0)
                throw new System.NotSupportedException("fn_b68410: texture-by-id branch (FUN_006eaaf0) is not ported");
            Prim p = LaunchEffectPrim(rt, 0x44, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            p.f_318 = 0f;
            p.f_3e8 = 0;
            p.f_49c = 0;
            p.f_550 = 0;
            p.f_604 = 0;
            p.f_3e8 = 1;
            p.f_3f4 = 0;
            if (startTick < 1)
            {
                p.f_468 = kind == 3 ? 7.0f : 18.0f;
                p.f_3fc = 0f;
                p.f_454 = 1;
            }
            else
            {
                p.f_468 = 5.0f;
                p.f_3fc = 1.0f;
                p.f_454 = 4;
            }
            p.f_3ea = 0;
            p.f_450 = 0;
            for (uint o = 0x400; o < 0x450; o += 4) M.U32(p.A + o, 0);
            M.U8(p.A + 0x455, 0);
        }
    }
}
