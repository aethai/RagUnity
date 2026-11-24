// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.Wisps.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>EF 713 / 714 / 946 — the drifting white-light wisps of the maps (prim type 0x82, packages/mapfx).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 713 (0x2c9; vf15 case 0xb4a9fb): fn_b917f0(this, 0) — four wandering wisp clusters.</summary>
        [FxEffectProcess(0x2c9)]
        public FxCaseResult Case_Map713(FxRuntime rt) { Fn_b917f0(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 714 (0x2ca; vf15 case 0xb4aa07): fn_b917f0(this, 1) — one wisp bobbing in place.</summary>
        [FxEffectProcess(0x2ca)]
        public FxCaseResult Case_Map714(FxRuntime rt) { Fn_b917f0(rt, 1); return FxCaseResult.Continue; }

        /// <summary>EF 946 (0x3b2; vf15 case 0xb4aa13): fn_b917f0(this, 2) — like 713 in white.</summary>
        [FxEffectProcess(0x3b2)]
        public FxCaseResult Case_Map946(FxRuntime rt) { Fn_b917f0(rt, 2); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b917f0 (0xb917f0) — follows the owner; on tick 0 (or when the counter reaches 99999998: then
        /// counter 0, life 99999999) one prim type 0x82 with RP flags +0x1c4 = 1 | 5, life = the effect's, texture
        /// effect\whitelight.tga. Four slots of 0xb4 bytes from +0x3e8, each four wisps; slot active +0x3e8 (kind 1: only
        /// slot 0), +0x3f0 = 0.15, size +0x468 = 2.4; per wisp k (offsets in slot order 0x484 / 0x46c / 0x490 / 0x478):
        /// position x/z = rand % 41 − 20 (kind 1: wisp 0 at (0, 0)), y drawn from rand % 100 then cleared, angle pairs
        /// (current, target) = rand % 360 × 4, alpha byte +0x45b+k = 0, blink +0x457+k = 0; +0x450 = kind, +0x451..+0x454 = 0,
        /// colour selector +0x455 = kind == 2 ? 10 : 8. Every rand() is drawn even when the value is overwritten.
        /// </summary>
        public void Fn_b917f0(FxRuntime rt, byte kind)
        {
            FollowMaster();
            if (m_stateCnt == 0x5f5e0fe)
            {
                m_stateCnt = 0;
                f_17c = 0x5f5e0ff;
            }
            else if (m_stateCnt != 0) return;

            Prim p = LaunchEffectPrim(rt, 0x82, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 0;
            p.f_1c4 |= 1;
            p.f_1c4 |= 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(MapFxNames.WhiteLight));
            p.f_318 = 0f;

            for (int slot = 0; slot < 4; slot++)
            {
                uint s = p.A + (uint)slot * 0xb4;
                if (kind == 1 && slot >= 1)
                {
                    M.U8(s + 0x3e8, 0);
                    M.U32(s + 0x3f0, 0x3e19999a);                                   // 0.15
                    M.U32(s + 0x468, 0x4019999a);                                   // 2.4
                    M.U32(s + 0x484, 0);
                    M.U32(s + 0x48c, 0);
                }
                else
                {
                    M.U8(s + 0x3e8, 1);
                    M.U32(s + 0x3f0, 0x3e19999a);
                    M.U32(s + 0x468, 0x4019999a);
                    if (kind == 1)
                    {
                        M.U32(s + 0x484, 0);
                        M.U32(s + 0x48c, 0);
                    }
                    else
                    {
                        M.F32(s + 0x484, (float)(RandMod(rt, 0x29) - 0x14));
                        M.F32(s + 0x48c, (float)(RandMod(rt, 0x29) - 0x14));
                    }
                }
                M.F32(s + 0x488, (float)RandMod(rt, 100));
                RandAngles_b917f0(rt, s + 0x3fc);
                M.U8(s + 0x45b, 0);
                M.U8(s + 0x457, 0);

                M.F32(s + 0x46c, (float)(RandMod(rt, 0x29) - 0x14));
                M.F32(s + 0x474, (float)(RandMod(rt, 0x29) - 0x14));
                M.F32(s + 0x470, (float)RandMod(rt, 100));
                RandAngles_b917f0(rt, s + 0x40c);
                M.U8(s + 0x45c, 0);
                M.U8(s + 0x458, 0);

                M.F32(s + 0x490, (float)(RandMod(rt, 0x29) - 0x14));
                M.F32(s + 0x498, (float)(RandMod(rt, 0x29) - 0x14));
                M.F32(s + 0x494, (float)RandMod(rt, 100));
                RandAngles_b917f0(rt, s + 0x41c);
                M.U8(s + 0x45d, 0);
                M.U8(s + 0x459, 0);

                M.F32(s + 0x478, (float)(RandMod(rt, 0x29) - 0x14));
                M.F32(s + 0x480, (float)(RandMod(rt, 0x29) - 0x14));
                M.F32(s + 0x47c, (float)RandMod(rt, 100));
                RandAngles_b917f0(rt, s + 0x42c);
                M.U8(s + 0x45e, 0);
                M.U8(s + 0x45a, 0);

                M.U32(s + 0x488, 0);
                M.U32(s + 0x470, 0);
                M.U32(s + 0x494, 0);
                M.U32(s + 0x47c, 0);
                M.U8(s + 0x450, kind);
                M.U8(s + 0x451, 0);
                M.U8(s + 0x452, 0);
                M.U8(s + 0x453, 0);
                M.U8(s + 0x454, 0);
                M.U8(s + 0x455, (byte)((kind == 2 ? 1 : 0) * 2 + 8));
            }
        }

        /// <summary>Four consecutive floats = rand() % 360 (one wisp's two (current, target) angle pairs).</summary>
        void RandAngles_b917f0(FxRuntime rt, uint a)
        {
            for (uint k = 0; k < 0x10; k += 4) M.F32(a + k, (float)RandMod(rt, 0x168));
        }
    }
}
