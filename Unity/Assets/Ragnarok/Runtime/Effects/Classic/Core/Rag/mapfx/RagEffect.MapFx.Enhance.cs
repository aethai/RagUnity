// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.Enhance.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>EF 9 (EF_ENHANCE) — the map record variant of the weapon-refine glow (packages/mapfx).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 9 (EF_ENHANCE; OnProcess case 0xb52bb6): fn_b384f0(this); break.</summary>
        [FxEffectProcess(0x9)]
        public FxCaseResult Case_Enhance(FxRuntime rt)
        {
            Fn_b384f0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b384f0 (0xb384f0) — no position update. Tick 0: prim 0xc (effect life, +0x288 22.5, +0x318 7, +0x310 90,
        /// matrix +0x160 = RotX(90°), alpha 128, fade from life − life/3, trail snapshot, effect\ring_yellow.tga) and prim 0xd
        /// (effect life, +0x2f4 / +0x2f8 6, +0x364 2, +0x368 = −(2 / life) / 1.5, +0x36c 50, alpha 128, fade from life − life/3,
        /// effect\alpha_down.tga). Then every 5th pass (/mineffect: every 15th) one prim 0x10 (life 50): matrix = RotY(rand % 360),
        /// start = (0, 0, r) through it with r = rand % 5 + 2, then matrix = RotX(90°), +0x2bc 2, +0x354 7, +0x360 0.25,
        /// effect\alpha_down.tga, +0x3e8 = 0.
        /// </summary>
        public void Fn_b384f0(FxRuntime rt)
        {
            int st = m_stateCnt;
            if (st == 0)
            {
                Prim p = LaunchEffectPrim(rt, 0xc, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = f_17c;
                M.U32(p.A + 0x288, 0x41b40000);                                             // 22.5
                M.U32(p.A + 0x318, 0x40e00000);                                             // 7
                M.U32(p.A + 0x310, 0x42b40000);                                             // 90
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                M.U32(p.A + 0x324, 0x43000000);                                             // 128
                p.f_1f0 = p.m_duration - p.m_duration / 3;
                p.SnapshotTrail_ad58d0();
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                M.U32((uint)p.f_144, rt.GetTexture("effect\\ring_yellow.tga"));             // 0xf7e7ac

                p = LaunchEffectPrim(rt, 0xd, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = f_17c;
                M.U32(p.A + 0x2f8, 0x40c00000);                                             // 6
                M.U32(p.A + 0x2f4, 0x40c00000);
                M.U32(p.A + 0x364, 0x40000000);                                             // 2
                p.f_368 = -(p.f_364 / (float)p.m_duration) / 1.5f;                          // [0xf7e110]
                M.U32(p.A + 0x36c, 0x42480000);                                             // 50
                M.U32(p.A + 0x324, 0x43000000);
                p.f_1f0 = p.m_duration - p.m_duration / 3;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                M.U32((uint)p.f_144, rt.GetTexture("effect\\alpha_down.tga"));              // 0xf80c58
                st = m_stateCnt;
            }
            if (st % (rt.MinEffect ? 0xf : 5) != 0) return;
            {
                Prim p = LaunchEffectPrim(rt, 0x10, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0x32;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(RandMod(rt, 0x168)));
                float r = (float)(RandMod(rt, 5) + 2);
                p.f_394 = p.f_16c * 0.0f + p.f_160 * 0.0f + r * p.f_178 + p.f_184;
                p.f_398 = p.f_170 * 0.0f + p.f_164 * 0.0f + r * p.f_17c + p.f_188;
                p.f_39c = p.f_174 * 0.0f + p.f_168 * 0.0f + r * p.f_180 + p.f_18c;
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(0x5a));
                M.U32(p.A + 0x2bc, 0x40000000);                                             // 2
                M.U32(p.A + 0x354, 0x40e00000);                                             // 7
                M.U32(p.A + 0x360, 0x3e800000);                                             // 0.25
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                M.U32((uint)p.f_144, rt.GetTexture("effect\\alpha_down.tga"));
                M.U8(p.A + 0x3e8, 0);
            }
        }
    }
}
