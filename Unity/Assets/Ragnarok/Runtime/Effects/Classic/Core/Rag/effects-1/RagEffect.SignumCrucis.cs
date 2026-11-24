// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.SignumCrucis.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        [FxEffectProcess(40)]
        public FxCaseResult Case_SignumCrucis(FxRuntime rt) => Fn_b5f790(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// CRagEffect::fn_b5f790 (0xb5f790) — Signum Crucis: screen position of the owner into f_154/f_158
        /// every tick; at tick 30 two sounds, a falling light column (prim 4, alpha_down.tga) and 20 rising
        /// sparks (prim 3, alpha_center.tga); STR at (0,-80,0) decides liveness.
        /// </summary>
        public bool Fn_b5f790(FxRuntime rt)
        {
            uint view = M.U32(rt.Mode + 0xd0);
            rt.ProjectTl(view + 0x98, M.U32(A + 0x138) + 0x10, out float sx, out float sy, out _);
            f_154 = sx;
            f_158 = sy;
            if (m_stateCnt == 0x1e)
            {
                rt.PlaySound3DRelative("effect\\EF_Signum.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                rt.PlaySound3DRelative("effect\\EF_Bash.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Prim p = LaunchEffectPrim(rt, 4, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0x28;
                p.f_1dc |= 1;
                p.f_398 = -40.0f;
                p.f_348 = 100.0f;
                p.f_324 = 0f;
                p.f_2d4 = 170.0f;
                p.f_328 = p.f_2d4 / 6.0f;
                p.f_1f0 = p.m_duration - p.m_duration / 3;
                SetFirstTexture(rt, p, "effect\\alpha_down.tga");
                for (int n = 0x14; n != 0; n--)
                {
                    p = LaunchEffectPrim(rt, 3, 0f, 0f, 0f);
                    m_lastPrim = p;
                    p.m_duration = 0x23;
                    p.f_398 = -40.0f;
                    p.f_324 = 0f;
                    p.f_30c = (float)RandMod(rt, 0x168);
                    p.f_330 = (float)(RandMod(rt, 0x3c) + 10) / 10.0f;
                    p.f_33c = -(p.f_330 / (float)p.m_duration) / 1.5f;
                    p.f_288 = (float)(RandMod(rt, 0x19) + 0xf) / 10.0f;
                    p.f_360 = (float)(RandMod(rt, 0x3c) + 0x1e);
                    p.f_364 = (float)(RandMod(rt, 0x23) + 0x5f) / 10.0f;
                    p.f_2d4 = 200.0f;
                    p.f_328 = p.f_2d4 / 10.0f;
                    SetFirstTexture(rt, p, "EFFECT\\alpha_center.tga");
                }
            }
            return rt.StrPlayer(this, 0f, -80.0f, 0f);
        }
    }
}
