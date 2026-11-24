// Synced from reference/port/src/RoClassicFx/Rag/hit434/RagEffect.Hit434.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// hit434: EF 434 (0x1b2) EF_HITLINE7, the basic-attack hit effect of the Taekwon-line attackers (FUN_00bebd90 puts
    /// 0x1b2 into the queued hit's effect2 instead of EF_HIT1 for jobs 4046–4048, 4225, 4226, 4238–4243, 4302, 4303,
    /// 4316; packages/hit56/NOTES.md). The case body is the effects-8 kick-trail launcher with kind 1.
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// EF 434 (0x1b2) EF_HITLINE7: <c>fn_b7d570(this, 1)</c> (0xb527f0 case 0x1b2; effects-8 <see cref="Fn_b7d570"/>):
        /// follows the owner and at ticks 0 and 1 launches a prim 0x64 (white02.bmp, duration f_17c = 50 by default) with
        /// four kick-trail slots — delay 0..10, tube 0.1, alpha 125..175, random heading, arc radius 5..9, trail 3, swing
        /// pre-advanced 10 ticks.
        /// </summary>
        [FxEffectProcess(0x1b2)]
        public FxCaseResult Case_HitLine7(FxRuntime rt) { Fn_b7d570(rt, 1); return FxCaseResult.Continue; }
    }
}
