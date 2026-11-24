// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>Texture names of the map-effect cases (byte strings as in game.exe; one char per CP949 byte).</summary>
    static class MapFxNames
    {
        /// <summary>0xffa5e8 이팩트\torch_01.spr</summary>
        public const string TorchSpr = "ÀÌÆÑÆ®\\torch_01.spr";
        /// <summary>0xffa5fc 이팩트\torch_01.act</summary>
        public const string TorchAct = "ÀÌÆÑÆ®\\torch_01.act";
        /// <summary>0xffa628 이팩트\크리스마스.spr</summary>
        public const string ChristmasSpr = "ÀÌÆÑÆ®\\Å©¸®½º¸¶½º.spr";
        /// <summary>0xffa640 이팩트\크리스마스.act</summary>
        public const string ChristmasAct = "ÀÌÆÑÆ®\\Å©¸®½º¸¶½º.act";
        /// <summary>0xffa408 이팩트\particle1.spr</summary>
        public const string Particle1Spr = "ÀÌÆÑÆ®\\particle1.spr";
        /// <summary>0xffa420 이팩트\particle1.act</summary>
        public const string Particle1Act = "ÀÌÆÑÆ®\\particle1.act";
        /// <summary>0xffb464 이팩트\유령.spr</summary>
        public const string GhostSpr = "ÀÌÆÑÆ®\\À¯·É.spr";
        /// <summary>0xffb474 이팩트\유령.act</summary>
        public const string GhostAct = "ÀÌÆÑÆ®\\À¯·É.act";
        /// <summary>0xffb484 이팩트\박쥐.spr</summary>
        public const string BatSpr = "ÀÌÆÑÆ®\\¹ÚÁã.spr";
        /// <summary>0xffb494 이팩트\박쥐.act</summary>
        public const string BatAct = "ÀÌÆÑÆ®\\¹ÚÁã.act";
        /// <summary>0xf82238</summary>
        public const string Cloud11 = "effect\\cloud11.tga";
        /// <summary>0xff4d20</summary>
        public const string WhiteLight = "effect\\whitelight.tga";
        /// <summary>0xff824c</summary>
        public const string Glow01 = "effect\\glow01.bmp";
        /// <summary>0xff81f0</summary>
        public const string Glow02 = "effect\\glow02.bmp";
        /// <summary>0xff8204</summary>
        public const string Glow03 = "effect\\glow03.bmp";
        /// <summary>0xff8260</summary>
        public const string Glow11 = "effect\\glow11.bmp";
        /// <summary>0xff8274 — the space before the dot is in the client's string.</summary>
        public const string Glow12b = "effect\\glow12b .bmp";
        /// <summary>0xff81e0</summary>
        public const string B = "effect\\b.bmp";
        /// <summary>0xff8324 effect\구체2.bmp</summary>
        public const string Sphere2 = "effect\\±¸Ã¼2.bmp";
        /// <summary>0xff8338 effect\구체3.bmp</summary>
        public const string Sphere3 = "effect\\±¸Ã¼3.bmp";
        /// <summary>0xff834c effect\구체4.bmp</summary>
        public const string Sphere4 = "effect\\±¸Ã¼4.bmp";
        /// <summary>0xff8360 effect\구체5.bmp</summary>
        public const string Sphere5 = "effect\\±¸Ã¼5.bmp";
        /// <summary>0xff8374 effect\구체12.bmp</summary>
        public const string Sphere12 = "effect\\±¸Ã¼12.bmp";
        /// <summary>0xff8388 effect\구체14.bmp</summary>
        public const string Sphere14 = "effect\\±¸Ã¼14.bmp";
        /// <summary>0xff7994 effect\구체15.bmp</summary>
        public const string Sphere15 = "effect\\±¸Ã¼15.bmp";
        /// <summary>0xf7ddac</summary>
        public const string RingBlue = "effect\\ring_blue.tga";
        /// <summary>0xff64d0</summary>
        public const string MagicGreen = "effect\\magic_green.tga";
        /// <summary>0xff5080</summary>
        public const string RingRed = "effect\\ring_red.tga";
        /// <summary>0xff8234</summary>
        public const string AlphaUpper = "effect\\alpha_upper.tga";
        /// <summary>0xff4db0</summary>
        public const string FreezingCircle = "effect\\freezing_circle.bmp";
        /// <summary>0xffb848</summary>
        public const string GhostBmp = "effect\\ghost.bmp";
    }

    /// <summary>
    /// Effect cases used by RSW map effect records (packages/mapfx) whose bodies only call helpers other packages
    /// already ported: the STR player fn_b36820 (effects-4) and the glow-light helper fn_b70c60 (effects-9).
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// EF 109 (EF_BUBBLE, Bubble%d.str), 110 (EF_GASPUSH, GasPush.str), 199 (EF_PONG, Pong%d.str) — OnProcess
        /// case group with 0x7e …: <c>pCVar6 = fn_b36820(this); goto code_r0x00b5293f</c> (STR only). The random STR
        /// pick is Init's (ClassicEffectTables.RandomStrIds).
        /// </summary>
        [FxEffectProcess(0x6d, 0x6e, 0xc7)]
        public FxCaseResult Case_MapStr_b36820(FxRuntime rt) => Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// EF 47 (0x2f, EF_TORCH; OnProcess case 0xb53b3b) — follows the owner; on tick 0 one sprite prim (type 5, flags
        /// +0x1dc |= 0x200, life = the effect's, y offset +0x398 = −10) showing 이팩트\torch_01.spr / .act. The RSW
        /// record's params (effect +0x13c / +0x140, from CEffectMaker) set it up: scale +0x318 = p0 (≤ 0 → 1; NaN kept),
        /// frame delay +0x1e8 = (int)p1 (below 1 → 1).
        /// </summary>
        [FxEffectProcess(0x2f)]
        public FxCaseResult Case_Torch(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt != 0) return FxCaseResult.Continue;
            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1dc |= 0x200;
            p.m_duration = f_17c;
            p.f_398 = -10f;
            float scale = f_13c;
            if (!(0f < scale) && !float.IsNaN(scale)) { f_13c = 1f; scale = 1f; }   // COMISS 0, f_13c; JB (CF: < or unordered)
            if (1f > f_140) f_140 = 1f;                                              // COMISS 1, f_140; JBE
            p.f_318 = scale;
            p.f_1e8 = FxOps.Cvtt(f_140);
            rt.SetSprName(p.A, MapFxNames.TorchSpr);
            rt.SetActName(p.A, MapFxNames.TorchAct);                                 // tail 0xb55779
            p.SnapshotTrail_ad58d0();
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 165 (0xa5, EF_BANJJAKII; OnProcess case 0xb53c5c) — like the torch with 이팩트\크리스마스.spr / .act (the
        /// Christmas sparkle), prim life 125, frame delay +0x1e8 = (int)max(p1, 1) (p0 ≤ 0 → 1 is stored but unused);
        /// r = rand() % 10: scale +0x318 = 0.01 when r % 3 == 1 (an invisible one), else 1; vf07 SetAction(r / 2, 1, 1).
        /// </summary>
        [FxEffectProcess(0xa5)]
        public FxCaseResult Case_Banjjakii(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt != 0) return FxCaseResult.Continue;
            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1dc |= 0x200;
            p.m_duration = 0x7d;
            p.f_398 = -10f;
            if (!(0f < f_13c) && !float.IsNaN(f_13c)) f_13c = 1f;
            float delay = f_140;
            if (1f > delay) { f_140 = 1f; delay = 1f; }
            p.f_1e8 = FxOps.Cvtt(delay);
            rt.SetSprName(p.A, MapFxNames.ChristmasSpr);
            rt.SetActName(p.A, MapFxNames.ChristmasAct);
            int r = rt.Rand.Next() % 10;
            p.f_318 = r % 3 == 1 ? 0.01f : 1f;                                      // [0xf4a690] = 0.01
            rt.SetAction(p.A, r / 2, 1, 1);                                          // vf07 (prim +0x1c)
            p.SnapshotTrail_ad58d0();
            return FxCaseResult.Continue;
        }

        /// <summary>EF 693 (0x2b5; vf15 case 0xb4ab93): fn_b70c60(this, "effect\glow01.bmp", 0xe).</summary>
        [FxEffectProcess(0x2b5)]
        public FxCaseResult Case_Map693(FxRuntime rt) { Fn_b70c60(rt, MapFxNames.Glow01, 0xe); return FxCaseResult.Continue; }

        /// <summary>EF 694 (0x2b6; vf15 case 0xb4aba4): fn_b70c60(this, "effect\glow02.bmp", 0xe).</summary>
        [FxEffectProcess(0x2b6)]
        public FxCaseResult Case_Map694(FxRuntime rt) { Fn_b70c60(rt, MapFxNames.Glow02, 0xe); return FxCaseResult.Continue; }

        /// <summary>EF 701 (0x2bd; vf15 case 0xb4abb5): fn_b70c60(this, "effect\glow11.bmp", 0xf).</summary>
        [FxEffectProcess(0x2bd)]
        public FxCaseResult Case_Map701(FxRuntime rt) { Fn_b70c60(rt, MapFxNames.Glow11, 0xf); return FxCaseResult.Continue; }

        /// <summary>EF 702 (0x2be; vf15 case 0xb4abc6): fn_b70c60(this, "effect\glow12b .bmp", 0xe).</summary>
        [FxEffectProcess(0x2be)]
        public FxCaseResult Case_Map702(FxRuntime rt) { Fn_b70c60(rt, MapFxNames.Glow12b, 0xe); return FxCaseResult.Continue; }

        /// <summary>EF 681 (0x2a9; vf15 case 0xb4ad58): fn_b70c60 × 4 — 구체2 (0xc), 구체3 (0xd), 구체4 (0xb), 구체5 (0xa).</summary>
        [FxEffectProcess(0x2a9)]
        public FxCaseResult Case_Map681(FxRuntime rt)
        {
            Fn_b70c60(rt, MapFxNames.Sphere2, 0xc);
            Fn_b70c60(rt, MapFxNames.Sphere3, 0xd);
            Fn_b70c60(rt, MapFxNames.Sphere4, 0xb);
            Fn_b70c60(rt, MapFxNames.Sphere5, 0xa);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 700 (0x2bc; vf15 case 0xb4ad95): fn_b70c60 × 4 — 구체12 (0xc), 구체3 (0xd), 구체14 (0xb), 구체15 (0xa).</summary>
        [FxEffectProcess(0x2bc)]
        public FxCaseResult Case_Map700(FxRuntime rt)
        {
            Fn_b70c60(rt, MapFxNames.Sphere12, 0xc);
            Fn_b70c60(rt, MapFxNames.Sphere3, 0xd);
            Fn_b70c60(rt, MapFxNames.Sphere14, 0xb);
            Fn_b70c60(rt, MapFxNames.Sphere15, 0xa);
            return FxCaseResult.Continue;
        }
    }
}
