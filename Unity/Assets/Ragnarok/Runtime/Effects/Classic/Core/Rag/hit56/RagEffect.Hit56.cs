// Synced from reference/port/src/RoClassicFx/Rag/hit56/RagEffect.Hit56.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>Strings of the package in game.exe (checked by Hit56Tests.NamesMatchGameExe).</summary>
    public static class Hit56Strings
    {
        public const string Hit5Wav = "effect\\EF_hit5.wav";
        public const string Hit6Wav = "effect\\EF_hit6.wav";
        public const string Lens2Tga = "effect\\lens2.tga";

        public static readonly (uint Address, string Value)[] Table =
        {
            (0x00ffa44c, Hit5Wav), (0x00ffa460, Hit6Wav), (0x00ff95b4, Lens2Tga),
        };
    }

    /// <summary>
    /// hit56: the two remaining melee hit flashes of the OnProcess switch head, EF 4 (EF_HIT5) and EF 5 (EF_HIT6):
    /// a crossed pair of lens2 flares (prim 0) at a random angle and that angle + 90, 20 above the owner.
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// The shared body of fn_b3ffa0 / fn_b402a0 (the two functions differ only in the constants): every tick the
        /// owner's screen position (f_154/f_158); at tick 0 the sound, then rand()%360 = a0 (CMOVNS, negative → 0) and two
        /// prim-0 lens2 flares of 17 ticks with rotation f_314 = a0 and a0 + 90: f_398 = -20, f_338 = -5 with
        /// f_344 = -(f_338 / duration) / 1.5 (DIVSS by the float at 0xf7e110), size f_354 = rand%<paramref name="sizeRange"/>
        /// + <paramref name="sizeBase"/> shrinking to 0 (f_358), f_360 = rand%<paramref name="f360Range"/> +
        /// <paramref name="f360Base"/>, f_364 = <paramref name="f364"/>, f_368 = 0.5, fade-out from half the duration.
        /// </summary>
        void CrossedLensFlaresH56(FxRuntime rt, string wav, int sizeRange, int sizeBase, int f360Range, int f360Base, float f364)
        {
            ProjectMasterToScreen(rt);
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative(wav, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            int a0 = RandMod(rt, 0x168);
            int a1 = a0 + 0x5a;
            for (int i = 0; i < 2; i++)
            {
                Prim p = LaunchLast(rt, 0);
                p.m_duration = 0x11;
                p.f_398 = -20.0f;
                p.f_314 = (float)(i == 0 ? a0 : a1);
                p.f_338 = -5.0f;
                p.f_344 = -(p.f_338 / (float)p.m_duration) / 1.5f;
                p.f_354 = (float)(RandMod(rt, sizeRange) + sizeBase);
                p.f_360 = (float)(RandMod(rt, f360Range) + f360Base);
                p.f_358 = -(p.f_354 / (float)p.m_duration);
                p.f_364 = f364;
                p.f_368 = 0.5f;
                int d = p.m_duration;
                p.f_1f0 = d - d / 2;
                SetPrimTexture(rt, p, Hit56Strings.Lens2Tga);
                M.U8(p.A + 0x3e8, 0);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b3ffa0 (0xb3ffa0) — EF_HIT5: <c>EF_hit5.wav</c>, two crossed lens2 flares, size rand%5 + 20,
        /// f_360 rand%10 + 30, f_364 2.5.
        /// </summary>
        public void Fn_b3ffa0(FxRuntime rt) => CrossedLensFlaresH56(rt, Hit56Strings.Hit5Wav, 5, 0x14, 10, 0x1e, 2.5f);

        /// <summary>
        /// CRagEffect::fn_b402a0 (0xb402a0) — EF_HIT6: <c>EF_hit6.wav</c>, two crossed lens2 flares, size rand%5 + 10,
        /// f_360 rand%5 + 15, f_364 1.7 (0x3fd9999a).
        /// </summary>
        public void Fn_b402a0(FxRuntime rt) => CrossedLensFlaresH56(rt, Hit56Strings.Hit6Wav, 5, 10, 5, 0xf, 1.7f);

        /// <summary>EF 4 (EF_HIT5): fn_b3ffa0.</summary>
        [FxEffectProcess(0x4)]
        public FxCaseResult Case_Hit5(FxRuntime rt) { Fn_b3ffa0(rt); return FxCaseResult.Continue; }

        /// <summary>EF 5 (EF_HIT6): fn_b402a0.</summary>
        [FxEffectProcess(0x5)]
        public FxCaseResult Case_Hit6(FxRuntime rt) { Fn_b402a0(rt); return FxCaseResult.Continue; }
    }
}
