// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.Rings.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>EF 231 / 247 / 259 / 260 — the slow ground rings of the Yuno / dancer maps (prim type 0x29, packages/mapfx).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 231 (0xe7; OnProcess case 0xb53e67): fn_b80e50(this, "effect\ring_blue.tga", 0); <c>break;</c>.</summary>
        [FxEffectProcess(0xe7)]
        public FxCaseResult Case_Map231(FxRuntime rt) { Fn_b80e50(rt, MapFxNames.RingBlue, 0); return FxCaseResult.Continue; }

        /// <summary>EF 247 (0xf7; OnProcess case 0xb53eac): fn_b80e50(this, "effect\ring_blue.tga", 1); <c>break;</c>.</summary>
        [FxEffectProcess(0xf7)]
        public FxCaseResult Case_Map247(FxRuntime rt) { Fn_b80e50(rt, MapFxNames.RingBlue, 1); return FxCaseResult.Continue; }

        /// <summary>EF 259 (0x103; OnProcess case 0xb53e7e): fn_b80e50(this, "effect\magic_green.tga", 2); <c>break;</c>.</summary>
        [FxEffectProcess(0x103)]
        public FxCaseResult Case_Map259(FxRuntime rt) { Fn_b80e50(rt, MapFxNames.MagicGreen, 2); return FxCaseResult.Continue; }

        /// <summary>EF 260 (0x104; OnProcess case 0xb53e95): fn_b80e50(this, "effect\ring_red.tga", 3); <c>break;</c>.</summary>
        [FxEffectProcess(0x104)]
        public FxCaseResult Case_Map260(FxRuntime rt) { Fn_b80e50(rt, MapFxNames.RingRed, 3); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b80e50 (0xb80e50) — on tick 0 only: position = the owner's, one prim type 0x29 (RP flags +0x1c4 = 5,
        /// life = the effect's) with texture 0 = <paramref name="texture"/> (array of +0x204 entries); +0x318 = 4 (kind ≤ 1),
        /// 7 (kind 2), 6 (kind 3), else untouched. Four slots of 0xb4 (k = 0..3): live +0x3e8 = 1, counter +0x3f4 = 30k,
        /// +0x3ee = 360, +0x3f0 = 120, angle +0x3f8 = 90k, +0x470 = 0, alpha +0x3ea = 70 and radius +0x468 = 0.5k + 11 for
        /// kind 1, else alpha 50 and radius 0.5k + 2, +0x3ec = 0, +0x466 = 89, dwords +0x3fc..+0x44c and bytes
        /// +0x450..+0x464 zeroed. No RSW param is read.
        /// </summary>
        public void Fn_b80e50(FxRuntime rt, string texture, sbyte kind)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x29, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            if (kind <= 1) M.U32(p.A + 0x318, 0x40800000);                              // 4.0
            else if (kind == 2) M.U32(p.A + 0x318, 0x40e00000);                         // 7.0
            else if (kind == 3) M.U32(p.A + 0x318, 0x40c00000);                         // 6.0

            for (int k = 0; k < 4; k++)
            {
                uint s = p.A + (uint)k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, k * 0x1e);
                M.I16(s + 0x3ee, 0x168);
                M.U32(s + 0x3f0, 0x42f00000);                                           // 120.0
                M.I16(s + 0x3f8, (short)(k * 0x5a));
                M.I16(s + 0x3ea, 0x32);
                M.U32(s + 0x470, 0);
                float r;
                if (kind != 0 && kind <= 1)
                {
                    M.I16(s + 0x3ea, 0x46);
                    r = (float)k * 0.5f + 11f;                                          // [0xf4a460], [0xfe2494]
                }
                else
                {
                    M.I16(s + 0x3ea, 0x32);
                    r = (float)k * 0.5f + 2f;                                           // [0xf4b450]
                }
                M.F32(s + 0x468, r);
                M.I16(s + 0x3ec, 0);
                M.I16(s + 0x466, 0x59);
                for (uint o = 0x3fc; o <= 0x44c; o += 4) M.U32(s + o, 0);
                for (uint o = 0x450; o <= 0x464; o++) M.U8(s + o, 0);
            }
        }
    }
}
