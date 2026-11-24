// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.StrCases.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-1: OnProcess cases that are "sound at tick N (+ owner tint) + STR layer". Note the three
    /// STR tails of 0xb527f0: code_r0x00b59b02 / code_r0x00b59e13 (fn_b36820) use the player's result,
    /// code_r0x00b56bd3 ignores it (falls into the epilogue).
    /// </summary>
    public readonly partial struct RagEffect
    {

        /// <summary>code_r0x00b56bd3: STR player at (0,-80,0), result ignored.</summary>
        FxCaseResult StrIgnoreResult(FxRuntime rt)
        {
            rt.StrPlayer(this, 0f, -80.0f, 0f);
            return FxCaseResult.Continue;
        }

        FxCaseResult StrUseResult(FxRuntime rt)
            => rt.StrPlayer(this, 0f, -80.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>The "snap to owner, then PlaySound3D relative to the player" idiom.</summary>
        void FollowMasterAndPlay(FxRuntime rt, string wav, float y)
        {
            FollowMaster();
            rt.PlaySound3DRelative(wav, m_pos_x, y, m_pos_z, 0xfa, 0x28, 1.0f, 0);
        }

        /// <summary>master +0xe8 flag bit set by addition when clear (the client's `if (!(f &amp; b)) f += b`).</summary>
        void AddMasterFlag_effects1(uint bit)
        {
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & bit) == 0) M.U32(master + 0xe8, f + bit);
        }

        /// <summary>OnProcess case 0x43 — Provoke: sound at ticks 5 and 30, STR (result used).</summary>
        [FxEffectProcess(67)]
        public FxCaseResult Case_Provoke(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 5)
            {
                rt.PlaySound3DRelative("effect\\swordman_provoke.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t == 0x1e)
                rt.PlaySound3DRelative("effect\\swordman_provoke.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            return StrUseResult(rt);
        }

        /// <summary>OnProcess cases 0x88 (Slow Poison) and 0x187 (Cart Boost): STR only, result ignored.</summary>
        [FxEffectProcess(136, 391)]
        public FxCaseResult Case_StrOnlyIgnore(FxRuntime rt) => StrIgnoreResult(rt);

        /// <summary>OnProcess case 0xf6 — Smite: CRagEffect::fn_b8abb0 (0xb8abb0) = STR player at (0,0,0).</summary>
        [FxEffectProcess(246)]
        public FxCaseResult Case_Smite(FxRuntime rt)
            => rt.StrPlayer(this, 0f, 0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>OnProcess case 0x101 — Endow Tornado: at tick 10 snap to owner + wind hit sound (y -150); STR.</summary>
        [FxEffectProcess(257)]
        public FxCaseResult Case_EndowTornado(FxRuntime rt)
        {
            if (m_stateCnt == 10) FollowMasterAndPlay(rt, "_enemy_hit_wind1.wav", -150.0f);
            return StrUseResult(rt);
        }

        /// <summary>OnProcess case 0x110 — Divest Helm: at tick 6 snap to owner + sound; STR.</summary>
        [FxEffectProcess(272)]
        public FxCaseResult Case_DivestHelm(FxRuntime rt)
        {
            if (m_stateCnt == 6) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff7b48, 0f);
            return StrUseResult(rt);
        }

        /// <summary>OnProcess case 0x181 — Soul Exhale (target): ticks 40..70 owner flag 1 + tint (200,150,50).</summary>
        [FxEffectProcess(385)]
        public FxCaseResult Case_SoulExhaleTarget(FxRuntime rt)
        {
            if ((uint)(m_stateCnt - 0x28) <= 0x1eu)
            {
                AddMasterFlag_effects1(1);
                rt.SetArgb(M.U32(A + 0x138), -1, 200, 0x96, 0x32);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>OnProcess case 0x186 — Shattering Strike: tick 0 snap + sound; STR (result ignored).</summary>
        [FxEffectProcess(390)]
        public FxCaseResult Case_ShatteringStrike(FxRuntime rt)
        {
            if (m_stateCnt == 0) FollowMasterAndPlay(rt, "effect\\black_overthrust.wav", 0f);
            return StrIgnoreResult(rt);
        }

        /// <summary>
        /// OnProcess case 0x188 — Counter Instinct: tick 0 snap + kyrie sound; ticks 0..29 owner flag 8 and a
        /// grey/white blink (3 ticks each); STR (result ignored).
        /// </summary>
        [FxEffectProcess(392)]
        public FxCaseResult Case_CounterInstinct(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                FollowMasterAndPlay(rt, "effect\\kyrie_guard.wav", 0f);
                t = m_stateCnt;
            }
            if ((uint)t <= 0x1du)
            {
                uint master = M.U32(A + 0x138);
                uint f = M.U32(master + 0xe8);
                if ((f & 8) == 0)
                {
                    M.U32(master + 0xe8, f + 8);
                    t = m_stateCnt;
                    master = M.U32(A + 0x138);
                }
                if (t % 6 < 3) rt.SetArgb(master, -1, 100, 100, 100);
                else rt.SetArgb(master, -1, 0xfa, 0xfa, 0xfa);
            }
            return StrIgnoreResult(rt);
        }

        /// <summary>OnProcess case 0x18c — Marionette Control (target): owner flags 1 and 8, tint (255,89,182).</summary>
        [FxEffectProcess(396)]
        public FxCaseResult Case_MarionetteTarget(FxRuntime rt)
        {
            AddMasterFlag_effects1(1);
            AddMasterFlag_effects1(8);
            rt.SetArgb(M.U32(A + 0x138), -1, 0xff, 0x59, 0xb6);
            return FxCaseResult.Continue;
        }
    }
}
