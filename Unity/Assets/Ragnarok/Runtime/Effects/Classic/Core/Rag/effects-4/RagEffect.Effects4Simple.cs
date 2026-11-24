// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.Effects4Simple.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>effects-4: cases of CRagEffect::OnProcess (0xb527f0) that are STR playback plus a sound.</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>CRagEffect::fn_b36820 (0xb36820) — plain STR playback at (0, -80, 0).</summary>
        public bool Fn_b36820(FxRuntime rt) => rt.StrPlayer(this, 0f, -80.0f, 0f);

        /// <summary>
        /// Cases 52, 70, 126, 139, 141, 143, 144, 146, 147, 148, 149, 150, 151, 152, 169, 235, 268:
        /// <c>pCVar6 = fn_b36820(this); goto code_r0x00b5293f;</c>
        /// </summary>
        [FxEffectProcess(0x34, 0x46, 0x7e, 0x8b, 0x8d, 0x8f, 0x90, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0xa9, 0xeb, 0x10c)]
        public FxCaseResult Case_StrOnly_b36820(FxRuntime rt) => Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>CRagEffect::fn_b351b0 (0xb351b0) — Improve Concentration: sound at tick 0, then STR.</summary>
        public bool Fn_b351b0(FxRuntime rt)
        {
            if (m_stateCnt == 0)
                rt.PlaySound3DRelative("effect\\ac_concentration.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            return rt.StrPlayer(this, 0f, -80.0f, 0f);
        }

        /// <summary>Case 153 (0x99).</summary>
        [FxEffectProcess(0x99)]
        public FxCaseResult Case_ImproveConcentration(FxRuntime rt) => Fn_b351b0(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>Case 49 (0x31) — fire hit: random fire-hit wav (FUN_00ce8810(3)) at tick 0, then STR.</summary>
        [FxEffectProcess(0x31)]
        public FxCaseResult Case_FireHit(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                uint player = M.U32(M.U32(rt.Mode + 0xcc) + 0x2c);
                float z = m_pos_z - M.F32(player + 0x18);
                float x = m_pos_x - M.F32(player + 0x10);
                string wav = rt.EnemyHitWav_ce8810(3);
                rt.Host?.PlaySound3D(wav, x, 0.0f, z, 0xfa, 0x28, 1.0f, 0);
            }
            return rt.StrPlayer(this, 0f, -80.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>Case 112 (0x70) — Kyrie Eleison: two sounds (ticks 0 and 15), then STR (code_r0x00b59b02).</summary>
        [FxEffectProcess(0x70)]
        public FxCaseResult Case_KyrieEleison(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                rt.PlaySound3DRelative("effect\\priest_kyrie_eleison_b.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t == 0xf)
                rt.PlaySound3DRelative("effect\\priest_kyrie_eleison_a.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            return rt.StrPlayer(this, 0f, -80.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>CRagEffect::fn_b81180 (0xb81180) — Magic Rod sound at tick 0 (position follows the owner).</summary>
        public void Fn_b81180(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\sage_magic rod.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
        }

        /// <summary>Case 244 (0xf4) — Magic Rod: STR (result ignored), sound; always continues.</summary>
        [FxEffectProcess(0xf4)]
        public FxCaseResult Case_MagicRod(FxRuntime rt)
        {
            Fn_b36820(rt);
            Fn_b81180(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 258 (0x102) — Endow Quake: sound at tick 10 (y -150), then STR (code_r0x00b59e13).</summary>
        [FxEffectProcess(0x102)]
        public FxCaseResult Case_EndowQuake(FxRuntime rt)
        {
            if (m_stateCnt == 10)
            {
                FollowMaster();
                rt.PlaySound3DRelative("_enemy_hit_wind1.wav", m_pos_x, -150.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>Case 273 (0x111) — Raging Quadruple Blow: sound at tick 0 (UNK_00ff95e0), then STR.</summary>
        [FxEffectProcess(0x111)]
        public FxCaseResult Case_RagingQuadrupleBlow(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\mon_\u00bf\u00ac\u00c8\u00af.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }
    }
}
