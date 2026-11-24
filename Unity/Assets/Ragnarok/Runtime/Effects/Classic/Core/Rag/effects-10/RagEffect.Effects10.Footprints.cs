// Synced from reference/port/src/RoClassicFx/Rag/effects-10/RagEffect.Effects10.Footprints.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// EF 326 (0x146, vf14 case 0xb4674c): Chase Walk left footprint — fn_b76e70("effect\foot_l_b.tga", 0).
        /// Launched as a ground effect by CPc::Render every 12 frames while option 0x4000 (lifetime 333).
        /// </summary>
        [FxEffectProcess(0x146)]
        public FxCaseResult Case_E10_FootLeft(FxRuntime rt) { Fn_b76e70(rt, E10Names.TexFootLeft, 0); return FxCaseResult.Continue; }

        /// <summary>EF 327 (0x147, vf14 case 0xb46761): Chase Walk right footprint — fn_b76e70("effect\foot_r_b.tga", 0).</summary>
        [FxEffectProcess(0x147)]
        public FxCaseResult Case_E10_FootRight(FxRuntime rt) { Fn_b76e70(rt, E10Names.TexFootRight, 0); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b76e70 (0xb76e70, thiscall (texture, kind), ret 8) — follows the owner; at tick 0 launches one
        /// flat ground quad (prim 0x43, lives as long as the effect) with the texture, size 5 (kind 1: 6, f_318 = 100),
        /// rotation +0x3f8 = cvtt(f_180) (the walk direction the launcher stores), alpha +0x3ea = 150 fading by one
        /// every other tick.
        /// </summary>
        public void Fn_b76e70(FxRuntime rt, string texture, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x43, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 1;
            { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
            { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
            M.U32((uint)m_lastPrim.f_144, rt.GetTexture(texture));
            { Prim lp = m_lastPrim; lp.f_318 = kind == 1 ? 100.0f : 0.0f; }
            M.U8(m_lastPrim.A + 0x3e8, 1);
            M.U8(m_lastPrim.A + 0x49c, 0);
            M.U8(m_lastPrim.A + 0x550, 0);
            M.U8(m_lastPrim.A + 0x604, 0);
            { Prim lp = m_lastPrim; lp.f_3f4 = 0; }
            { Prim lp = m_lastPrim; lp.f_468 = kind == 1 ? 6.0f : 5.0f; }
            { Prim lp = m_lastPrim; lp.f_466 = 0; }
            { Prim lp = m_lastPrim; lp.f_3f8 = (short)FxOps.Cvtt(f_180); }
            { Prim lp = m_lastPrim; lp.f_3ea = 0x96; }
            { Prim lp = m_lastPrim; lp.f_3fc = -0.2f; }
        }
    }
}
