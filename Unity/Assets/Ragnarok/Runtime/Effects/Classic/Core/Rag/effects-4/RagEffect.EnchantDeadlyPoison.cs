// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.EnchantDeadlyPoison.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b372c0 (0xb372c0) — Enchant Deadly Poison particles: sound at ticks 0/5/11/18/26/40,
        /// every 3rd tick a rising particle3 sprite (type 5) at a random angle and radius.
        /// </summary>
        public void Fn_b372c0(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0 || t == 5 || t == 0xb || t == 0x12 || t == 0x1a || t == 0x28)
            {
                rt.PlaySound3DRelative("effect\\assasin_enchantpoison.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t % 3 != 0) return;
            Prim p = LaunchLast(rt, 5);
            p.m_duration = 0x28;
            p = m_lastPrim;
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(RandMod(rt, 0x168)));
            p = m_lastPrim;
            float r = (float)(RandMod(rt, 6) + 2);
            p.f_394 = p.f_16c * 0.0f + p.f_160 * 0.0f + r * p.f_178 + p.f_184;
            p.f_398 = p.f_170 * 0.0f + p.f_164 * 0.0f + r * p.f_17c + p.f_188;
            p.f_39c = p.f_174 * 0.0f + p.f_168 * 0.0f + r * p.f_180 + p.f_18c;
            p = m_lastPrim;
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(0x5a));
            int speed = RandMod(rt, 0x32);
            p = m_lastPrim;
            p.f_2bc = (float)(speed + 0x1e) / 100.0f;
            p = m_lastPrim;
            p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
            int size = RandMod(rt, 0x14);
            p = m_lastPrim;
            p.f_318 = (float)(size + 0x14) / 100.0f;
            p = m_lastPrim; p.f_338 = 3.0f;
            p = m_lastPrim; p.f_324 = 0.0f;
            p = m_lastPrim; p.f_328 = p.f_2d4 / 10.0f;
            p = m_lastPrim; p.f_1f0 = p.m_duration - p.m_duration / 5;
            rt.SetSprName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle3.spr");
            rt.SetActName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle3.act");
            m_lastPrim.SnapshotTrail_ad58d0();
        }

        /// <summary>
        /// Case 493 (0x1ed, vf15) — Enchant Deadly Poison: particles; for t ≤ 80 the owner flashes magenta
        /// (flag 1, ARGB(-1, 255, 0, 255)) on even ticks and white on odd ticks (code_r0x00b4e168).
        /// </summary>
        [FxEffectProcess(0x1ed)]
        public FxCaseResult Case_EnchantDeadlyPoison(FxRuntime rt)
        {
            Fn_b372c0(rt);
            if (0x50 < m_stateCnt) return FxCaseResult.Continue;
            if ((m_stateCnt & 1) == 0)
            {
                AddMasterFlag(1);
                MasterArgb(rt, -1, 0xff, 0, 0xff);
            }
            else MasterArgb(rt, -1, 0xff, 0xff, 0xff);
            return FxCaseResult.Continue;
        }
    }
}
