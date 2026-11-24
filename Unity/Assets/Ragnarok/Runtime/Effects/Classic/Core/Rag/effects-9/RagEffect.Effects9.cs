// Synced from reference/port/src/RoClassicFx/Rag/effects-9/RagEffect.Effects9.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-9: CRagEffect::vf15 (0xb47410) cases the original client launches for tracker skills — Soul Linker
    /// body states, Star Gladiator warm aura, Twilight Alchemy, Gunslinger / Ninja projectiles, Bard/Dancer
    /// performance casts, Adaptation, Chase Walk. vf15 returns 1 after every case below (→ Continue).
    /// </summary>
    public readonly partial struct RagEffect
    {
        // ---- owner body-state cases (flags at owner +0xe8, counters at +0xf4 / +0xf6) --------------------

        /// <summary>
        /// EF 542 (0x21e, Kaupe / Kaite): while owner flag 0x4000 is clear and stateCnt &lt;= 0x96, sets it and
        /// stores stateCnt / 3 in the owner byte +0xf6.
        /// </summary>
        [FxEffectProcess(0x21e)]
        public FxCaseResult Case_E9_KaupeKaite(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x4000) != 0 || m_stateCnt > 0x96) return FxCaseResult.Continue;
            M.U32(master + 0xe8, flags + 0x4000);
            M.U8(M.U32(A + 0x138) + 0xf6, (byte)(m_stateCnt / 3));
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 544 (0x220, also 0x576; Sun/Moon/Star Warm aura, duration 99999999): while owner flag 0x40000 is
        /// clear, sets it and stores cvtt(stateCnt * 0.125) (stateCnt &lt;= 200) or 25 in the owner byte +0xf6.
        /// </summary>
        [FxEffectProcess(0x220, 0x576)]
        public FxCaseResult Case_E9_WarmAura(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x40000) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, flags + 0x40000);
            int t = m_stateCnt;
            if (t <= 0xc8) M.U8(M.U32(A + 0x138) + 0xf6, (byte)FxOps.Cvtt((float)t * 0.125f));
            else M.U8(M.U32(A + 0x138) + 0xf6, 0x19);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 548 (0x224, Fling): ticks 0..16 owner flag |= 0x40000, owner byte +0xf6 = stateCnt * 3.</summary>
        [FxEffectProcess(0x224)]
        public FxCaseResult Case_E9_Fling(FxRuntime rt)
        {
            if (m_stateCnt > 0x10) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            M.U32(master + 0xe8, M.U32(master + 0xe8) | 0x40000);
            M.U8(M.U32(A + 0x138) + 0xf6, (byte)(m_stateCnt * 3));
            return FxCaseResult.Continue;
        }

        /// <summary>EF 549 (0x225, Stin / Stun hit): ticks 0..16 owner flag |= 0x4000, owner byte +0xf6 = stateCnt * 3.</summary>
        [FxEffectProcess(0x225)]
        public FxCaseResult Case_E9_StinHit(FxRuntime rt)
        {
            if (m_stateCnt > 0x10) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            M.U32(master + 0xe8, M.U32(master + 0xe8) | 0x4000);
            M.U8(M.U32(A + 0x138) + 0xf6, (byte)(m_stateCnt * 3));
            return FxCaseResult.Continue;
        }

        /// <summary>EF 550 (0x226, Stin): ticks 31..50 owner flag |= 0x2000, owner word +0xf4 = stateCnt.</summary>
        [FxEffectProcess(0x226)]
        public FxCaseResult Case_E9_Stin(FxRuntime rt)
        {
            if ((uint)(m_stateCnt - 0x1f) > 0x13) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            M.U32(master + 0xe8, M.U32(master + 0xe8) | 0x2000);
            M.I16(M.U32(A + 0x138) + 0xf4, (short)m_stateCnt);
            return FxCaseResult.Continue;
        }

        // ---- sound + helper cases ---------------------------------------------------------------------------

        /// <summary>EF 554 (0x22a, Sma): fn_b70c60(this, "effect\freeze_a.bmp", 2).</summary>
        [FxEffectProcess(0x22a)]
        public FxCaseResult Case_E9_Sma(FxRuntime rt)
        {
            Fn_b70c60(rt, "effect\\freeze_a.bmp", 2);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 572 (0x23c, Warm cast): tick 10 sound at the owner, then fn_b83790(this, "effect\white02.bmp", 3).</summary>
        [FxEffectProcess(0x23c)]
        public FxCaseResult Case_E9_WarmCast(FxRuntime rt)
        {
            if (m_stateCnt == 10)
            {
                FollowMaster();
                rt.PlaySound3DRelative(E9Names.WavWarm, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            Fn_b83790(rt, "effect\\white02.bmp", 3);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 573 (0x23d, Twilight Alchemy 1): 80 × fn_b8e7d0(0).</summary>
        [FxEffectProcess(0x23d)]
        public FxCaseResult Case_E9_Twilight1(FxRuntime rt)
        {
            for (int i = 0; i < 0x50; i++) Fn_b8e7d0(rt, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 574 (0x23e, Twilight Alchemy 2): 80 × fn_b8e7d0(1).</summary>
        [FxEffectProcess(0x23e)]
        public FxCaseResult Case_E9_Twilight2(FxRuntime rt)
        {
            for (int i = 0; i < 0x50; i++) Fn_b8e7d0(rt, 1);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 575 (0x23f, Twilight Alchemy 3): 80 × fn_b8e7d0(2).</summary>
        [FxEffectProcess(0x23f)]
        public FxCaseResult Case_E9_Twilight3(FxRuntime rt)
        {
            for (int i = 0; i < 0x50; i++) Fn_b8e7d0(rt, 2);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 583 (0x247, Warm): fn_b72000(7) — m_ef01 sprite with the fire-hit sound.</summary>
        [FxEffectProcess(0x247)]
        public FxCaseResult Case_E9_WarmSprite(FxRuntime rt) { Fn_b72000(rt, 7); return FxCaseResult.Continue; }

        /// <summary>EF 623 (0x26f, Piercing Shot): tick 28 sound; 4 × (fn_b6d260(20), fn_b6d260(21)) speed lines.</summary>
        [FxEffectProcess(0x26f)]
        public FxCaseResult Case_E9_PiercingShot(FxRuntime rt)
        {
            if (m_stateCnt == 0x1c)
            {
                FollowMaster();
                rt.PlaySound3DRelative(E9Names.WavPiercingShot, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            for (int i = 0; i < 4; i++)
            {
                Fn_b6d260(rt, 0x14);
                Fn_b6d260(rt, 0x15);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 624 (0x270, Kamaitachi): tick 1 sound; fn_b89b90(this, "effect\270바람.tga").</summary>
        [FxEffectProcess(0x270)]
        public FxCaseResult Case_E9_Kamaitachi(FxRuntime rt)
        {
            if (m_stateCnt == 1)
            {
                FollowMaster();
                rt.PlaySound3DRelative(E9Names.WavKamaitachi, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            Fn_b89b90(rt, E9Names.TexWind270);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 638 (0x27e, Ground Drift): fn_b72000(0x15).</summary>
        [FxEffectProcess(0x27e)]
        public FxCaseResult Case_E9_GroundDrift1(FxRuntime rt) { Fn_b72000(rt, 0x15); return FxCaseResult.Continue; }

        /// <summary>EF 641 (0x281, Ground Drift): fn_b72000(0x18).</summary>
        [FxEffectProcess(0x281)]
        public FxCaseResult Case_E9_GroundDrift4(FxRuntime rt) { Fn_b72000(rt, 0x18); return FxCaseResult.Continue; }

        // effects-11: the other Ground Drift grenades (the server picks the unit by the bullet's element: UNT_GROUNDDRIFT
        // 0xbf dark / 0xc0 poison / 0xc2 fire relaunch these), vf15 cases 0x27f / 0x280 / 0x282
        /// <summary>EF 639 (0x27f, vf15): fn_b72000(this, 0x16).</summary>
        [FxEffectProcess(0x27f)]
        public FxCaseResult Case_E11_GroundDrift2(FxRuntime rt) { Fn_b72000(rt, 0x16); return FxCaseResult.Continue; }

        /// <summary>EF 640 (0x280, vf15): fn_b72000(this, 0x17).</summary>
        [FxEffectProcess(0x280)]
        public FxCaseResult Case_E11_GroundDrift3(FxRuntime rt) { Fn_b72000(rt, 0x17); return FxCaseResult.Continue; }

        /// <summary>EF 642 (0x282, vf15): fn_b72000(this, 0x19).</summary>
        [FxEffectProcess(0x282)]
        public FxCaseResult Case_E11_GroundDrift5(FxRuntime rt) { Fn_b72000(rt, 0x19); return FxCaseResult.Continue; }

        /// <summary>
        /// EF 840..845 (0x348..0x34d, Bard/Dancer performance casts): fn_b93140(this, 0x2d, "", 3..8) — sound and one
        /// bubble ring prim 0x95 at tick 0, lifetime at least 120 ticks.
        /// </summary>
        [FxEffectProcess(0x348, 0x349, 0x34a, 0x34b, 0x34c, 0x34d)]
        public FxCaseResult Case_E9_Performance(FxRuntime rt)
        {
            Fn_b93140(rt, 0x2d, "", (sbyte)(m_type - 0x348 + 3));
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 848 (0x350, Adaptation): ticks 4..90 blink the owner (even ticks: flag 1 + ARGB(-1, 0xb6, 0x59, 0xff),
        /// odd ticks white); every even tick one rising particle (fn_b99990(2)).
        /// </summary>
        [FxEffectProcess(0x350)]
        public FxCaseResult Case_E9_Adaptation(FxRuntime rt)
        {
            int t = m_stateCnt;
            if ((uint)(t - 4) <= 0x56)
            {
                uint master = M.U32(A + 0x138);
                if ((t & 1) == 0)
                {
                    uint flags = M.U32(master + 0xe8);
                    if ((flags & 1) == 0)
                    {
                        M.U32(master + 0xe8, flags + 1);
                        master = M.U32(A + 0x138);
                    }
                    rt.SetArgb(master, -1, 0xb6, 0x59, 0xff);
                }
                else rt.SetArgb(master, -1, 0xff, 0xff, 0xff);
            }
            Fn_b99990(rt, 2);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 1132 (0x46c, SKA; duration 99999999): the sound at tick 0, nothing else.</summary>
        [FxEffectProcess(0x46c)]
        public FxCaseResult Case_E9_Ska(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative(E9Names.WavSka, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 1195 (0x4ab, Chase Walk cast): tick 0 sound; while the owner byte +0xa1 is set and owner +0x70 (action)
        /// is 0, spins the owner heading (+0x4c) by 12/18/24/48 degrees per tick (ticks ≤15/≤30/≤40/later),
        /// wrapped below 360.
        /// </summary>
        [FxEffectProcess(0x4ab)]
        public FxCaseResult Case_E9_ChaseWalk(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            uint master = M.U32(A + 0x138);
            if (M.U8(master + 0xa1) == 0) return FxCaseResult.Continue;
            if (M.I32(master + 0x70) != 0) return FxCaseResult.Continue;
            int t = m_stateCnt;
            float h = M.F32(master + 0x4c);
            if (t <= 0xf) h = h + 12.0f;
            else if (t <= 0x1e) h = h + 18.0f;
            else if (t <= 0x28) h = h + 24.0f;
            else h = h + 48.0f;
            M.F32(master + 0x4c, h);
            master = M.U32(A + 0x138);
            float v = M.F32(master + 0x4c);
            if (v > 360.0f) M.F32(master + 0x4c, v - 360.0f);
            return FxCaseResult.Continue;
        }
    }
}
