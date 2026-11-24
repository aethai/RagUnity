// Synced from reference/port/src/RoClassicFx/Rag/effects-2/RagEffect.Effects2.Cases.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// OnProcess (0xb527f0) / vf14 (0xb46290) / vf15 (0xb47410) cases of package effects-2.
    /// Call-site arguments (lost by the decompiler) were read from the assembly of each case.
    /// </summary>
    public readonly partial struct RagEffect
    {
        FxCaseResult E2_Str(FxRuntime rt) => rt.StrPlayer(this, 0f, -80.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>Random master tint used by Stave Crasher / Mind Breaker: SetARGB(-1, r3, r2, r1) with rᵢ = rand() % 255 (≥ 0).</summary>
        void E2_RandomTint(FxRuntime rt, uint master)
        {
            int r1 = rt.Rand.Next() % 0xff;
            int r2 = rt.Rand.Next() % 0xff;
            int r3 = rt.Rand.Next() % 0xff;
            rt.SetArgb(master, -1, r3 >= 0 ? r3 : 0, r2 >= 0 ? r2 : 0, r1 >= 0 ? r1 : 0);
        }

        [FxEffectProcess(0xb)]
        public FxCaseResult Case_Endure(FxRuntime rt) { Fn_b37c50(rt); return FxCaseResult.Continue; }

        [FxEffectProcess(0x12)]
        public FxCaseResult Case_Steal(FxRuntime rt) { Fn_b620b0(rt); return FxCaseResult.Continue; }

        [FxEffectProcess(0x3e)]
        public FxCaseResult Case_Sightrasher(FxRuntime rt) { Fn_b5f000(rt); return FxCaseResult.Continue; }

        [FxEffectProcess(0x4a)]
        public FxCaseResult Case_IceWall(FxRuntime rt) { Fn_b41470(rt); return FxCaseResult.Continue; }

        [FxEffectProcess(0x57)]
        public FxCaseResult Case_LexDivina(FxRuntime rt) => Fn_b440a0(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>Case 92 (Meteor Storm): sound at tick 0, screen quake (CGameMode::SendMsg 0x7a) at tick 40, STR.</summary>
        [FxEffectProcess(0x5c)]
        public FxCaseResult Case_MeteorStorm(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                E2_Sound(rt, "effect\\wizard_meteor.wav");
                t = m_stateCnt;
            }
            if (t == 0x28) rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
            return E2_Str(rt);
        }

        [FxEffectProcess(0x5e)]
        public FxCaseResult Case_JupitelThunder(FxRuntime rt) { Fn_b65d20(rt, 0); return FxCaseResult.Continue; }

        /// <summary>Case 101 (Weapon Repair): sound at ticks 20 and 55, STR.</summary>
        [FxEffectProcess(0x65)]
        public FxCaseResult Case_WeaponRepair(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x14)
            {
                E2_Sound(rt, "effect\\black_weapon_repair_a.wav");
                t = m_stateCnt;
            }
            if (t == 0x37) E2_Sound(rt, "effect\\black_weapon_repair_a.wav");
            return E2_Str(rt);
        }

        [FxEffectProcess(0x74)]
        public FxCaseResult Case_WaterBall(FxRuntime rt) { Fn_b654a0(rt); return FxCaseResult.Continue; }

        [FxEffectProcess(0x77)]
        public FxCaseResult Case_Detect(FxRuntime rt) { Fn_b35d00(rt, 0); return FxCaseResult.Continue; }

        [FxEffectProcess(0xe0)]
        public FxCaseResult Case_Ganbantein_effects2(FxRuntime rt) { Fn_b8f3b0(rt); return FxCaseResult.Continue; }

        /// <summary>Case 234 (Spell Breaker): tick 0 follows the owner and plays the sound, then STR.</summary>
        [FxEffectProcess(0xea)]
        public FxCaseResult Case_SpellBreaker(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                E2_FollowMaster();
                E2_Sound(rt, "effect\\sage_spell breake.wav");
            }
            return E2_Str(rt);
        }

        /// <summary>Case 245 (Holy Cross): tick 0 plays the sound at the listener (absolute 0,0,0), then STR.</summary>
        [FxEffectProcess(0xf5)]
        public FxCaseResult Case_HolyCross(FxRuntime rt)
        {
            if (m_stateCnt == 0) rt.PlaySound3DAbsolute("effect\\cru_holy cross.wav", 0f, 0f, 0f);
            return E2_Str(rt);
        }

        /// <summary>Case 253 (Absorb Spirit Sphere): tick 0 sound at y = -80, then fn_b7ad40("ring_blue.tga", 2).</summary>
        [FxEffectProcess(0xfd)]
        public FxCaseResult Case_AbsorbSpiritSphere(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                E2_FollowMaster();
                E2_Sound(rt, Effects2Strings.Wav_ff3940, -80.0f);
            }
            Fn_b7ad40(rt, "effect\\ring_blue.tga", 2);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 262 (Raging Quadruple Blow): 20 × fn_b8cb60("alpha_center.tga", 0).</summary>
        [FxEffectProcess(0x106)]
        public FxCaseResult Case_RagingQuadrupleBlow_effects2(FxRuntime rt)
        {
            for (int i = 0x14; i != 0; i--) Fn_b8cb60(rt, "effect\\alpha_center.tga", 0);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(0x109)]
        public FxCaseResult Case_ThrowSpiritSphere(FxRuntime rt) { Fn_b8c810(rt); return FxCaseResult.Continue; }

        /// <summary>Case 270 (Divest Shield): tick 6 follows the owner and plays the sound, then STR.</summary>
        [FxEffectProcess(0x10e)]
        public FxCaseResult Case_DivestShield(FxRuntime rt)
        {
            if (m_stateCnt == 6)
            {
                E2_FollowMaster();
                E2_Sound(rt, Effects2Strings.Wav_ff7b48);
            }
            return E2_Str(rt);
        }

        [FxEffectProcess(0x116)] public FxCaseResult Case_Lullaby(FxRuntime rt) { Fn_b6b460(rt, "effect\\zz.bmp", 1); return FxCaseResult.Continue; }
        [FxEffectProcess(0x117)] public FxCaseResult Case_MentalSensing(FxRuntime rt) { Fn_b6b460(rt, "effect\\pocket.bmp", 3); return FxCaseResult.Continue; }
        [FxEffectProcess(0x118)] public FxCaseResult Case_DownTempo(FxRuntime rt) { Fn_b6ad90(rt, "effect\\twirl.bmp", 8); return FxCaseResult.Continue; }
        [FxEffectProcess(0x119)] public FxCaseResult Case_BattleTheme(FxRuntime rt) { Fn_b6b460(rt, "", 2); return FxCaseResult.Continue; }
        [FxEffectProcess(0x11a)] public FxCaseResult Case_HarmonicLick(FxRuntime rt) { Fn_b6b460(rt, "effect\\twirl.bmp", 7); return FxCaseResult.Continue; }
        [FxEffectProcess(0x11b)] public FxCaseResult Case_ClassicalPluck(FxRuntime rt) { Fn_b6b8b0(rt, "effect\\safeline.bmp", 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x11c)] public FxCaseResult Case_PowerChord(FxRuntime rt) { Fn_b6b460(rt, "", 5); return FxCaseResult.Continue; }
        [FxEffectProcess(0x11d)] public FxCaseResult Case_AcousticRhythm(FxRuntime rt) { Fn_b6ad90(rt, "effect\\twirl.bmp", 4); return FxCaseResult.Continue; }
        [FxEffectProcess(0x11e)] public FxCaseResult Case_PerfectTablature(FxRuntime rt) { Fn_b6b460(rt, "effect\\melody_b.bmp", 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x11f)] public FxCaseResult Case_ImpressiveRiff(FxRuntime rt) { Fn_b6bce0(rt, "effect\\ring_red.tga", 1); return FxCaseResult.Continue; }
        [FxEffectProcess(0x120)] public FxCaseResult Case_MagicStrings(FxRuntime rt) { Fn_b6b460(rt, "", 8); return FxCaseResult.Continue; }
        [FxEffectProcess(0x121)] public FxCaseResult Case_SongOfLutie(FxRuntime rt) { Fn_b6b460(rt, "effect\\idun_apple.bmp", 6); return FxCaseResult.Continue; }
        [FxEffectProcess(0x123)] public FxCaseResult Case_FocusBallet(FxRuntime rt) { Fn_b6b460(rt, "effect\\melody_a.bmp", 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x124)] public FxCaseResult Case_SlowGrace(FxRuntime rt) { Fn_b6bce0(rt, "effect\\magic_green.tga", 2); return FxCaseResult.Continue; }
        [FxEffectProcess(0x125)] public FxCaseResult Case_LadyLuck(FxRuntime rt) { Fn_b6b460(rt, "effect\\kiss.bmp", 4); return FxCaseResult.Continue; }
        [FxEffectProcess(0x126)] public FxCaseResult Case_GypsysKiss(FxRuntime rt) { Fn_b6bce0(rt, "effect\\safeline.bmp", 4); return FxCaseResult.Continue; }

        [FxEffectProcess(0x12a)] public FxCaseResult Case_AcidTerror(FxRuntime rt) { Fn_b8e3d0(rt, Effects2Strings.Tex_ff63e0, 0); return FxCaseResult.Continue; }
        [FxEffectProcess(0x12b)] public FxCaseResult Case_AidPotion(FxRuntime rt) { Fn_b8e3d0(rt, Effects2Strings.Tex_ff6290, 1); return FxCaseResult.Continue; }

        /// <summary>vf14 case 8 (id 308, Stone Fling): fn_b8e3d0("…item\돌.bmp", 2); vf14 returns true.</summary>
        [FxEffectProcess(0x134)] public FxCaseResult Case_StoneFling(FxRuntime rt) { Fn_b8e3d0(rt, Effects2Strings.Tex_ff6400, 2); return FxCaseResult.Continue; }

        /// <summary>vf14 case 70 (id 370, Battle Chant): fn_b6b460("cross_old.bmp", 9); vf14 returns true.</summary>
        [FxEffectProcess(0x172)] public FxCaseResult Case_BattleChant(FxRuntime rt) { Fn_b6b460(rt, "effect\\cross_old.bmp", 9); return FxCaseResult.Continue; }

        /// <summary>
        /// Case 380 (Stave Crasher): tick 25 follows the owner + sound, tick 30 screen quake, ticks 30..60 the owner
        /// flashes random colours (flag +0xe8 bit 0).
        /// </summary>
        [FxEffectProcess(0x17c)]
        public FxCaseResult Case_StaveCrasher(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x19)
            {
                E2_FollowMaster();
                E2_Sound(rt, Effects2Strings.Wav_ff3c04);
                t = m_stateCnt;
            }
            if (t == 0x1e)
            {
                rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
                t = m_stateCnt;
            }
            if ((uint)(t - 0x1e) <= 0x1e)
            {
                uint master = M.U32(A + 0x138);
                uint flags = M.U32(master + 0xe8);
                if ((flags & 1) == 0)
                {
                    M.U32(master + 0xe8, flags + 1);
                    master = M.U32(A + 0x138);
                }
                E2_RandomTint(rt, master);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Case 388 (Focused Arrow Strike): tick 30 follows the owner + sound; fn_b8d6e0 every tick.</summary>
        [FxEffectProcess(0x184)]
        public FxCaseResult Case_FocusedArrowStrike(FxRuntime rt)
        {
            if (m_stateCnt == 0x1e)
            {
                E2_FollowMaster();
                E2_Sound(rt, Effects2Strings.Wav_ff3ba4);
            }
            Fn_b8d6e0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 394 (Sheltering Bliss): fn_b8baa0("fire_ivy.bmp", a, 1) for a = 0, 9, …, 36.</summary>
        [FxEffectProcess(0x18a)]
        public FxCaseResult Case_ShelteringBliss(FxRuntime rt)
        {
            for (int a = 0; a < 0x2d; a += 9) Fn_b8baa0(rt, "effect\\fire_ivy.bmp", (short)a, 1);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 403 (Mind Breaker): tick 25 follows the owner + sound; ticks 0..60 random owner tint.</summary>
        [FxEffectProcess(0x193)]
        public FxCaseResult Case_MindBreaker(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x19)
            {
                E2_FollowMaster();
                E2_Sound(rt, "effect\\swordman_provoke.wav");
                t = m_stateCnt;
            }
            if ((uint)t <= 0x3c) E2_RandomTint(rt, M.U32(A + 0x138));
            return FxCaseResult.Continue;
        }

        /// <summary>Case 413 (Heel Drop): tick 5 sound; owner flag 0x20 + animation counter (+0xf4 = tick - 17) during ticks 17..50.</summary>
        [FxEffectProcess(0x19d)]
        public FxCaseResult Case_HeelDrop(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 5)
            {
                E2_FollowMaster();
                E2_Sound(rt, Effects2Strings.Wav_ff960c);
                t = m_stateCnt;
            }
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x20) == 0 && (uint)(t - 0x11) <= 0x21)
            {
                M.U32(master + 0xe8, flags + 0x20);
                M.I16(M.U32(A + 0x138) + 0xf4, (short)(m_stateCnt - 0x11));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Case 414 (Roundhouse Kick): owner flag 0x40 + counter (+0xf4 = 2·tick) for ticks &lt; 18.</summary>
        [FxEffectProcess(0x19e)]
        public FxCaseResult Case_RoundhouseKick(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x40) == 0 && m_stateCnt < 0x12)
            {
                M.U32(master + 0xe8, flags + 0x40);
                M.I16(M.U32(A + 0x138) + 0xf4, (short)(m_stateCnt * 2));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Case 419 (Kaite): owner flag 0x200.</summary>
        [FxEffectProcess(0x1a3)]
        public FxCaseResult Case_Kaite(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x200) == 0) M.U32(master + 0xe8, flags + 0x200);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 427 (Eske): owner flag 0x800 + counter (+0xf4 = tick + 50) for ticks &lt; 150.</summary>
        [FxEffectProcess(0x1ab)]
        public FxCaseResult Case_Eske(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x800) == 0)
            {
                M.U32(master + 0xe8, flags + 0x800);
                int t = m_stateCnt;
                if (t < 0x96) M.I16(M.U32(A + 0x138) + 0xf4, (short)(t + 0x32));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Case 440 (Assumptio): tick 0 follows the owner + sound; STR player (its result is ignored).</summary>
        [FxEffectProcess(0x1b8)]
        public FxCaseResult Case_Assumptio(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                E2_FollowMaster();
                E2_Sound(rt, Effects2Strings.Wav_ff9580);
            }
            rt.StrPlayer(this, 0f, -80.0f, 0f);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 443 (Running): empty case.</summary>
        [FxEffectProcess(0x1bb)]
        public FxCaseResult Case_Running_effects2(FxRuntime rt) => FxCaseResult.Continue;

        /// <summary>
        /// Case 488 (Warmth of the Sun/Moon/Stars): tick 5 follows the owner + sound; owner flag 0x800 with a fading
        /// byte at +0xf2; two blue ring sets (fn_b76990 variants 0 and 1).
        /// </summary>
        [FxEffectProcess(0x1e8)]
        public FxCaseResult Case_Warmth(FxRuntime rt)
        {
            if (m_stateCnt == 5)
            {
                E2_FollowMaster();
                E2_Sound(rt, Effects2Strings.Wav_ff4b2c);
            }
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x800) == 0)
            {
                M.U32(master + 0xe8, flags + 0x800);
                int t = m_stateCnt;
                if (t < 200) M.U8(M.U32(A + 0x138) + 0xf2, (byte)t);
                else if (t < 400) M.U8(M.U32(A + 0x138) + 0xf2, (byte)(0x90 - (byte)t));
            }
            Fn_b76990(rt, "effect\\ring_blue.tga", 4, 0);
            Fn_b76990(rt, "effect\\ring_blue.tga", 4, 1);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// vf15 case 496 (Preserve): fn_b7a290("a01.bmp"); ticks 10..30 flash the owner: even ticks set flag bit 0
        /// and grey (250,250,250), odd ticks white.
        /// </summary>
        [FxEffectProcess(0x1f0)]
        public FxCaseResult Case_Preserve(FxRuntime rt)
        {
            Fn_b7a290(rt, "effect\\a01.bmp");
            int t = m_stateCnt;
            if ((uint)(t - 10) > 0x14) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            if ((t & 1) != 0)
            {
                rt.SetArgb(master, -1, 0xff, 0xff, 0xff);
                return FxCaseResult.Continue;
            }
            uint flags = M.U32(master + 0xe8);
            if ((flags & 1) == 0)
            {
                M.U32(master + 0xe8, flags + 1);
                master = M.U32(A + 0x138);
            }
            rt.SetArgb(master, -1, 0xfa, 0xfa, 0xfa);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// vf15 case 521 (Double Casting): ticks 10..50 set owner flag 8; even ticks flag bit 0 + red, odd ticks white.
        /// </summary>
        [FxEffectProcess(0x209)]
        public FxCaseResult Case_DoubleCasting(FxRuntime rt)
        {
            int t = m_stateCnt;
            if ((uint)(t - 10) > 0x28) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 8) == 0)
            {
                M.U32(master + 0xe8, flags + 8);
                t = m_stateCnt;
            }
            master = M.U32(A + 0x138);
            if ((t & 1) != 0)
            {
                rt.SetArgb(master, -1, 0xff, 0xff, 0xff);
                return FxCaseResult.Continue;
            }
            flags = M.U32(master + 0xe8);
            if ((flags & 1) == 0)
            {
                M.U32(master + 0xe8, flags + 1);
                master = M.U32(A + 0x138);
            }
            rt.SetArgb(master, -1, 0xff, 0, 0);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(0x21d)] public FxCaseResult Case_AidBerserkPotion(FxRuntime rt) { Fn_b8e3d0(rt, Effects2Strings.Tex_ff62f4, 1); return FxCaseResult.Continue; }

        /// <summary>
        /// vf15 case 553 (Esma): at ticks 5/20/35/50/65 follow the owner and play the sound at the listener
        /// (0, 5 - tick or -50, 0); two wind strand sets (fn_b89610 variants 0 and 1).
        /// </summary>
        [FxEffectProcess(0x229)]
        public FxCaseResult Case_Esma_effects2(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 5 || t == 0x14 || t == 0x23 || t == 0x32 || t == 0x41)
            {
                E2_FollowMaster();
                t = m_stateCnt;
                float y = t <= 0x32 ? (float)(5 - t) : -50.0f;
                rt.PlaySound3DAbsolute(Effects2Strings.Wav_ff80d8, 0f, y, 0f);
            }
            Fn_b89610(rt, Effects2Strings.Tex_ff85b8, 0);
            Fn_b89610(rt, Effects2Strings.Tex_ff85b8, 1);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(0x258)] public FxCaseResult Case_ThrowVenomKnife(FxRuntime rt) { Fn_b8e3d0(rt, Effects2Strings.Tex_ff6318, 3); return FxCaseResult.Continue; }
        [FxEffectProcess(0x265)] public FxCaseResult Case_ThrowShuriken(FxRuntime rt) { Fn_b8e3d0(rt, Effects2Strings.Tex_ff633c, 3); return FxCaseResult.Continue; }
        [FxEffectProcess(0x266)] public FxCaseResult Case_ThrowKunai(FxRuntime rt) { Fn_b8e3d0(rt, Effects2Strings.Tex_ff5198, 3); return FxCaseResult.Continue; }
        [FxEffectProcess(0x267)] public FxCaseResult Case_ThrowHuumaShuriken(FxRuntime rt) { Fn_b8e3d0(rt, Effects2Strings.Tex_ff635c, 3); return FxCaseResult.Continue; }
        [FxEffectProcess(0x268)] public FxCaseResult Case_ThrowZeny(FxRuntime rt) { Fn_b8e3d0(rt, "effect\\coin_a.bmp", 3); return FxCaseResult.Continue; }

        /// <summary>
        /// vf15 case 622 (Lightning Strike of Destruction): follows the owner; tick 0: sound + the thunderstorm
        /// sprite prim (type 5).
        /// </summary>
        [FxEffectProcess(0x26e)]
        public FxCaseResult Case_LightningStrikeOfDestruction(FxRuntime rt)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return FxCaseResult.Continue;
            E2_Sound(rt, "effect\\EF_ThunderStorm.wav");
            Prim p = E2_Launch(rt, 5);
            p.m_duration = f_17c;
            p.f_1c4 |= 0x200;
            p.f_1dc |= 0x200;
            p.f_398 = -4.5f;
            p.f_1e8 = 2;
            p.f_318 = 2.5f;
            rt.SetSprName(p.A, Effects2Strings.Spr_ffa7d8);
            rt.SetActName(p.A, Effects2Strings.Act_ffa7ec);
            p.SnapshotTrail_ad58d0();
            return FxCaseResult.Continue;
        }
    }
}
