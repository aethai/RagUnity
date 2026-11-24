// Synced from reference/port/src/RoClassicFx/Rag/effects-3/RagEffect.Effects3.Cases.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-3: the OnProcess (0xb527f0) / vf14 (0xb46290) / vf15 (0xb47410) case bodies.
    /// Hidden arguments that Ghidra dropped (texture names, flags) were recovered from the assembly.
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>Korean (CP949) literals as byte strings (one char per byte, like <see cref="FxRuntime.ReadCString"/>).</summary>
        internal static class E3Names
        {
            /// <summary>0xff7b48 "effect\\" + CP949 bytes BA B9 C8 A3 B0 DD + ".wav" (Divest Armor).</summary>
            public const string WavStripArmor = "effect\\º¹È£°Ý.wav";
            /// <summary>0xff8934 "effect\\T_" + C8 B8 C7 C7 + "2.wav" (Taekwon Jump).</summary>
            public const string WavTkJump2 = "effect\\T_È¸ÇÇ2.wav";
            /// <summary>0xff8800 "effect\\T_" + C4 B3 BD BA C6 C3 + ".wav" (Spirit, start).</summary>
            public const string WavSpiritStart = "effect\\T_Ä³½ºÆÃ.wav";
            /// <summary>0xff8814 "effect\\T_" + BF B5 C8 A5 + ".wav" (Spirit, tick 0x91).</summary>
            public const string WavSpiritSoul = "effect\\T_¿µÈ¥.wav";
            /// <summary>0xff85e0 "effect\\" + C7 B3 C0 CE + ".wav" (Wind Blade).</summary>
            public const string WavWindBlade = "effect\\Ç³ÀÎ.wav";
            /// <summary>0xff85b8 "effect\\270" + B9 D9 B6 F7 + ".tga" (Wind Blade texture).</summary>
            public const string TexWindBlade = "effect\\270¹Ù¶÷.tga";
        }

        /// <summary>The engine's "new[] texture array, slot 0 = GetTexture(name)" idiom on a prim.</summary>
        static void E3SetTexture(FxRuntime rt, Prim p, string texture)
        {
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            uint tex = rt.GetTexture(texture);
            p.M.U32((uint)p.f_144, tex);
        }

        /// <summary>Copies the master's position into the effect (the inlined idiom before many sounds).</summary>
        void E3FollowMaster()
        {
            uint master = m_master;
            m_pos_x = M.F32(master + 0x10);
            m_pos_y = M.F32(master + 0x14);
            m_pos_z = M.F32(master + 0x18);
        }

        void E3Sound(FxRuntime rt, string wav, float y) => rt.PlaySound3DRelative(wav, m_pos_x, y, m_pos_z, 0xfa, 0x28, 1.0f, 0);

        FxCaseResult E3Str(FxRuntime rt) => rt.StrPlayer(this, 0f, -80.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;

        // ---- OnProcess (0xb527f0) --------------------------------------------------------------------

        /// <summary>EF 16 (Bash, Double Strafe, Mystical Amplification): fn_b31940.</summary>
        [FxEffectProcess(0x10)]
        public FxCaseResult Case_E3_Bash(FxRuntime rt) { Fn_b31940(rt); return FxCaseResult.Continue; }

        /// <summary>EF 22 (Sight): fn_b5ed30.</summary>
        [FxEffectProcess(0x16)]
        public FxCaseResult Case_E3_Sight(FxRuntime rt) { Fn_b5ed30(rt); return FxCaseResult.Continue; }

        /// <summary>EF 27 (Frost Diver): fn_b3be30(this, 0).</summary>
        [FxEffectProcess(0x1b)]
        public FxCaseResult Case_E3_FrostDiver(FxRuntime rt) { Fn_b3be30(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 38 (Decrease AGI): fn_b357d0.</summary>
        [FxEffectProcess(0x26)]
        public FxCaseResult Case_E3_DecAgi(FxRuntime rt) { Fn_b357d0(rt); return FxCaseResult.Continue; }

        /// <summary>EF 51 (Cold Bolt / Frost Nova / Storm Gust hit): fn_b34bd0.</summary>
        [FxEffectProcess(0x33)]
        public FxCaseResult Case_E3_IceHit(FxRuntime rt) { Fn_b34bd0(rt); return FxCaseResult.Continue; }

        /// <summary>EF 90 (Lord of Vermilion): meteor/blast-mine sounds, quake every 50 ticks in [50, 300), STR.</summary>
        [FxEffectProcess(0x5a)]
        public FxCaseResult Case_E3_LordOfVermilion(FxRuntime rt)
        {
            switch (m_stateCnt)
            {
                case 0:
                    E3Sound(rt, "effect\\wizard_meteo.wav", 0.0f);
                    E3Sound(rt, "effect\\hunter_blastmine.wav", 0.0f);   // falls into case 10
                    break;
                case 10:
                    E3Sound(rt, "effect\\hunter_blastmine.wav", 0.0f);
                    break;
                case 0x14: case 0x32: case 0x50: case 100: case 0x82: case 0x8c: case 0xb4: case 200:
                    E3Sound(rt, "effect\\wizard_meteo.wav", 0.0f);
                    break;
            }
            int t = m_stateCnt;
            if (0x31 < t && t % 0x32 == 0 && t < 300)
                rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
            return E3Str(rt);
        }

        /// <summary>EF 93 (Jupitel Thunder): fn_b65700(this, 0).</summary>
        [FxEffectProcess(0x5d)]
        public FxCaseResult Case_E3_JupitelThunder(FxRuntime rt) { Fn_b65700(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 102 (Hammer Fall): fn_b35460 (its result decides).</summary>
        [FxEffectProcess(0x66)]
        public FxCaseResult Case_E3_HammerFall(FxRuntime rt) => Fn_b35460(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>EF 104 (Maximize Power): four sounds, STR.</summary>
        [FxEffectProcess(0x68)]
        public FxCaseResult Case_E3_MaximizePower(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0) { E3Sound(rt, "effect\\black_maximize_power_circle.wav", 0.0f); t = m_stateCnt; }
            if (t == 0x28) { E3Sound(rt, "effect\\black_maximize_power_sword.wav", 0.0f); t = m_stateCnt; }
            if (t == 0x2f) { E3Sound(rt, "effect\\black_maximize_power_sword.wav", 0.0f); t = m_stateCnt; }
            if (t == 0x41) E3Sound(rt, "effect\\black_maximize_power_sword_bic.wav", 0.0f);
            return E3Str(rt);
        }

        /// <summary>EF 128 (Power-Thrust): fn_b5aa30.</summary>
        [FxEffectProcess(0x80)]
        public FxCaseResult Case_E3_PowerThrust(FxRuntime rt) { Fn_b5aa30(rt); return FxCaseResult.Continue; }

        /// <summary>EF 129 (Venom Splasher): sound at tick 10, STR.</summary>
        [FxEffectProcess(0x81)]
        public FxCaseResult Case_E3_VenomSplasher(FxRuntime rt)
        {
            if (m_stateCnt == 10) E3Sound(rt, "effect\\assasin_venomsplasher.wav", 0.0f);
            return E3Str(rt);
        }

        /// <summary>EF 130 / 250 (Twohand Quicken, Spear Quicken): fn_b63f90 (result ignored: <c>break</c>).</summary>
        [FxEffectProcess(0x82, 0xfa)]
        public FxCaseResult Case_E3_Quicken(FxRuntime rt) { Fn_b63f90(rt); return FxCaseResult.Continue; }

        /// <summary>EF 140 (Resurrection): a ring (prim 0xd, ring_yellow.tga) every 25 ticks; the first lasts the whole effect.</summary>
        [FxEffectProcess(0x8c)]
        public FxCaseResult Case_E3_Resurrection(FxRuntime rt)
        {
            float phase = (float)(m_stateCnt % 0x19);
            if (phase == 0.0f)
            {
                Prim p = LaunchEffectPrim(rt, 0xd, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0x3c;
                { Prim lp_ = m_lastPrim; lp_.f_330 = 1.5f; }
                { Prim lp_ = m_lastPrim; lp_.f_33c = 0.03f; }
                { Prim lp_ = m_lastPrim; lp_.f_2f8 = 10.5f; }
                { Prim lp_ = m_lastPrim; lp_.f_2fc = -0.12f; }
                { Prim lp_ = m_lastPrim; lp_.f_304 = 0.003f; }
                { Prim lp_ = m_lastPrim; lp_.f_300 = -0.12f; }
                { Prim lp_ = m_lastPrim; lp_.f_308 = 0.003f; }
                { Prim lp_ = m_lastPrim; lp_.f_2f4 = 7.5f; }
                { Prim lp_ = m_lastPrim; lp_.f_360 = 7.5f; }
                { Prim lp_ = m_lastPrim; lp_.f_324 = 240.0f; }
                int d = m_lastPrim.m_duration;
                { Prim lp_ = m_lastPrim; lp_.f_1f0 = d - d / 2; }
                if (m_stateCnt == 0)
                {
                    { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
                    { Prim lp_ = m_lastPrim; lp_.f_2fc = 0.0f; }
                    { Prim lp_ = m_lastPrim; lp_.f_304 = 0.0f; }
                    { Prim lp_ = m_lastPrim; lp_.f_330 = 1.5f; }
                    { Prim lp_ = m_lastPrim; lp_.f_2f8 = 11.0f; }
                    { Prim lp_ = m_lastPrim; lp_.f_2f4 = 7.5f; }
                    { Prim lp_ = m_lastPrim; lp_.f_300 = 0.0f; }
                    { Prim lp_ = m_lastPrim; lp_.f_308 = 0.0f; }
                    d = m_lastPrim.m_duration;
                    { Prim lp_ = m_lastPrim; lp_.f_1f0 = d - d / 10; }
                }
                E3SetTexture(rt, m_lastPrim, "effect\\ring_yellow.tga");
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 166 (Snap): fn_b45180 (after-images of the caster).</summary>
        [FxEffectProcess(0xa6)]
        public FxCaseResult Case_E3_Snap(FxRuntime rt) { Fn_b45180(rt); return FxCaseResult.Continue; }

        /// <summary>EF 220 (Aid Berserk Potion): fn_b5c3d0 (its result decides).</summary>
        [FxEffectProcess(0xdc)]
        public FxCaseResult Case_E3_BerserkPotion(FxRuntime rt) => Fn_b5c3d0(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>EF 242 (Magnetic Earth): fn_b6aad0(this, "effect\\aaa copy.bmp", 1).</summary>
        [FxEffectProcess(0xf2)]
        public FxCaseResult Case_E3_MagneticEarth(FxRuntime rt) { Fn_b6aad0(rt, "effect\\aaa copy.bmp", 1); return FxCaseResult.Continue; }

        /// <summary>EF 248 (Resistant Souls): fn_b86ba0(this, "effect\\ring_yellow.tga", 0), then the STR (result decides).</summary>
        [FxEffectProcess(0xf8)]
        public FxCaseResult Case_E3_ResistantSouls(FxRuntime rt) { Fn_b86ba0(rt, "effect\\ring_yellow.tga", 0); return E3Str(rt); }

        /// <summary>EF 251 (Sacrifice): STR at offset (0, -20, 0).</summary>
        [FxEffectProcess(0xfb)]
        public FxCaseResult Case_E3_Sacrifice(FxRuntime rt) => rt.StrPlayer(this, 0f, -20.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>EF 256 (Elemental Change Water / Endow Tsunami): follow + sound at tick 10, STR.</summary>
        [FxEffectProcess(0x100)]
        public FxCaseResult Case_E3_EndowWater(FxRuntime rt)
        {
            if (m_stateCnt == 10)
            {
                E3FollowMaster();
                E3Sound(rt, "_enemy_hit_wind1.wav", -150.0f);
            }
            return E3Str(rt);
        }

        /// <summary>EF 261 (Fury, Eska): fn_b8ec00(this, "effect\\ring_yellow.tga", 0).</summary>
        [FxEffectProcess(0x105)]
        public FxCaseResult Case_E3_Fury(FxRuntime rt) { Fn_b8ec00(rt, "effect\\ring_yellow.tga", 0); return FxCaseResult.Continue; }

        /// <summary>
        /// EF 271 (Divest Armor): follow + Korean wav at tick 6, STR. Not registered while the oracle harness
        /// decodes C strings as ASCII (see packages/effects-3/NOTES.md); verified by Effects3Tests.
        /// </summary>
        [FxEffectProcess(0x10f)]
        public FxCaseResult Case_E3_StripArmor(FxRuntime rt)
        {
            if (m_stateCnt == 6)
            {
                E3FollowMaster();
                E3Sound(rt, E3Names.WavStripArmor, 0.0f);
            }
            return E3Str(rt);
        }

        /// <summary>EF 274 (Mug): follow + sound at tick 0, 30 coins (fn_b870e0) per tick.</summary>
        [FxEffectProcess(0x112)]
        public FxCaseResult Case_E3_Mug(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                E3FollowMaster();
                E3Sound(rt, "effect\\rog_steal coin.wav", 0.0f);
            }
            for (int i = 0x1e; i != 0; i--) Fn_b870e0(rt, "effect\\coin_a.bmp");
            return FxCaseResult.Continue;
        }

        /// <summary>EF 295 (Unbarring Octave): fn_b8e3a0(this, 1).</summary>
        [FxEffectProcess(0x127)]
        public FxCaseResult Case_E3_UnbarringOctave(FxRuntime rt) { Fn_b8e3a0(rt, 1); return FxCaseResult.Continue; }

        /// <summary>EF 296 (Dazzler): fn_b8e3a0(this, 0).</summary>
        [FxEffectProcess(0x128)]
        public FxCaseResult Case_E3_Dazzler(FxRuntime rt) { Fn_b8e3a0(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 374 (Basilica): fn_b67e70(this, "effect\\alpha_down.tga", 0) and (…, 1).</summary>
        [FxEffectProcess(0x176)]
        public FxCaseResult Case_E3_Basilica(FxRuntime rt)
        {
            Fn_b67e70(rt, "effect\\alpha_down.tga", 0);
            Fn_b67e70(rt, "effect\\alpha_down.tga", 1);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 404 (Fiber Lock): fn_b6aad0(this, "effect\\spiderweb.tga", 2).</summary>
        [FxEffectProcess(0x194)]
        public FxCaseResult Case_E3_FiberLock(FxRuntime rt) { Fn_b6aad0(rt, "effect\\spiderweb.tga", 2); return FxCaseResult.Continue; }

        /// <summary>
        /// EF 406 (Soul Siphon): label 0xb56bd3 — the STR player's result is ignored there (<c>break</c>),
        /// so the effect lives until its duration even when it has no STR.
        /// </summary>
        [FxEffectProcess(0x196)]
        public FxCaseResult Case_E3_SoulSiphon(FxRuntime rt) { rt.StrPlayer(this, 0f, -80.0f, 0f); return FxCaseResult.Continue; }

        /// <summary>
        /// EF 445 (Taekwon Jump): follow + Korean wav at tick 30, then from tick 36 flag 0x10000 on the master
        /// with the jump phase in master+0xf4. Not registered (Korean wav, see NOTES.md); verified by Effects3Tests.
        /// </summary>
        [FxEffectProcess(0x1bd)]
        public FxCaseResult Case_E3_TaekwonJump(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x1e)
            {
                E3FollowMaster();
                E3Sound(rt, E3Names.WavTkJump2, 0.0f);
                t = m_stateCnt;
            }
            uint master = m_master;
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x10000) == 0 && 0x23 < t)
            {
                M.U32(master + 0xe8, flags + 0x10000);
                M.I16(m_master + 0xf4, (short)(m_stateCnt - 0x23));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 487 (Hatred of the Sun Moon and Stars): fn_b66f60(this, "effect\\ring_black.tga").</summary>
        [FxEffectProcess(0x1e7)]
        public FxCaseResult Case_E3_Hatred(FxRuntime rt) { Fn_b66f60(rt, "effect\\ring_black.tga"); return FxCaseResult.Continue; }

        // ---- vf14 (0xb46290, ids 300..373: returns AL) ----------------------------------------------

        /// <summary>EF 302 (Bomb): fn_b70650; vf14 returns true.</summary>
        [FxEffectProcess(0x12e)]
        public FxCaseResult Case_E3_Bomb(FxRuntime rt) { Fn_b70650(rt); return FxCaseResult.Continue; }

        /// <summary>EF 336 (Guard, Parrying): fn_b7a4b0(this, "effect\\guardK.tga", 0); vf14 returns true.</summary>
        [FxEffectProcess(0x150)]
        public FxCaseResult Case_E3_Guard(FxRuntime rt) { Fn_b7a4b0(rt, "effect\\guardK.tga", 0); return FxCaseResult.Continue; }

        /// <summary>EF 368 (Frenzy): master flag 1 + SetARGB(master, -1, 0xff, 100, 100).</summary>
        [FxEffectProcess(0x170)]
        public FxCaseResult Case_E3_Frenzy(FxRuntime rt)
        {
            uint master = m_master;
            uint flags = M.U32(master + 0xe8);
            if ((flags & 1) == 0) M.U32(master + 0xe8, flags + 1);
            rt.SetArgb(m_master, -1, 0xff, 100, 100);
            return FxCaseResult.Continue;
        }

        // ---- vf15 (0xb47410, ids > 0x1ea: returns AL, always true for these) -----------------------

        /// <summary>
        /// EF 503 (Spirit of the Blacksmith / Soul Linker / Rebirth): follow, Korean wavs at ticks 0 and 145,
        /// fn_b8b0d0(whitelight.tga), fn_b68750(0). Not registered (Korean wav, see NOTES.md).
        /// </summary>
        [FxEffectProcess(0x1f7)]
        public FxCaseResult Case_E3_Spirit(FxRuntime rt)
        {
            E3FollowMaster();
            int t = m_stateCnt;
            if (t == 0) { E3Sound(rt, E3Names.WavSpiritStart, 0.0f); t = m_stateCnt; }
            if (t == 0x91) E3Sound(rt, E3Names.WavSpiritSoul, -50.0f);
            Fn_b8b0d0(rt, "effect\\whitelight.tga");
            Fn_b68750(rt, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 505 (Foresight): sound at 0; ticks 10..35 flash the master (even: flag 1 + ARGB fa/fa/64, odd: ff/ff/ff).</summary>
        [FxEffectProcess(0x1f9)]
        public FxCaseResult Case_E3_Foresight(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                E3Sound(rt, "effect\\priest_suffragium.wav", 0.0f);
                t = m_stateCnt;
            }
            if ((uint)(t - 10) > 0x19u) return FxCaseResult.Continue;
            uint master = m_master;
            if ((t & 1) == 0)
            {
                uint flags = M.U32(master + 0xe8);
                if ((flags & 1) == 0) M.U32(master + 0xe8, flags + 1);
                rt.SetArgb(m_master, -1, 0xfa, 0xfa, 100);
            }
            else rt.SetArgb(master, -1, 0xff, 0xff, 0xff);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 514 (Ki Explosion): fn_b77370(this, "effect\\ring_blue.tga").</summary>
        [FxEffectProcess(0x202)]
        public FxCaseResult Case_E3_KiExplosion(FxRuntime rt) { Fn_b77370(rt, "effect\\ring_blue.tga"); return FxCaseResult.Continue; }

        /// <summary>EF 537 (Acid Demonstration): quake at tick 5, fn_b69460(this, "effect\\ring_black.tga", 0x21).</summary>
        [FxEffectProcess(0x219)]
        public FxCaseResult Case_E3_AcidDemonstration(FxRuntime rt)
        {
            if (m_stateCnt == 5) rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
            Fn_b69460(rt, "effect\\ring_black.tga", 0x21);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 543 (Kaahi): master flag 0x4000 + fade byte master+0xf6 while t &lt; 200, fn_b83790(white02.bmp, 1).</summary>
        [FxEffectProcess(0x21f)]
        public FxCaseResult Case_E3_Kaahi(FxRuntime rt)
        {
            if (m_stateCnt < 200)
            {
                uint master = m_master;
                uint flags = M.U32(master + 0xe8);
                if ((flags & 0x4000) == 0)
                {
                    M.U32(master + 0xe8, flags + 0x4000);
                    int t = m_stateCnt;
                    if (t <= 0x19)
                    {
                        M.U8(m_master + 0xf6, (byte)t);
                        t = m_stateCnt;
                    }
                    if ((uint)(t - 0x97) <= 0x18u) M.U8(m_master + 0xf6, (byte)(0xaf - t));
                }
            }
            Fn_b83790(rt, "effect\\white02.bmp", 1);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 546 (Kaupe): fn_b83790(this, "effect\\white02.bmp", 2).</summary>
        [FxEffectProcess(0x222)]
        public FxCaseResult Case_E3_Kaupe(FxRuntime rt) { Fn_b83790(rt, "effect\\white02.bmp", 2); return FxCaseResult.Continue; }

        /// <summary>EF 604 (Close Confine): fn_b82770(this, 2).</summary>
        [FxEffectProcess(0x25c)]
        public FxCaseResult Case_E3_CloseConfine(FxRuntime rt) { Fn_b82770(rt, 2); return FxCaseResult.Continue; }

        /// <summary>
        /// EF 621 (Wind Blade): follow + Korean wav at tick 0, f_174 blades fn_b898f0(Korean tga, i % 2).
        /// Not registered (Korean names, see NOTES.md).
        /// </summary>
        [FxEffectProcess(0x26d)]
        public FxCaseResult Case_E3_WindBlade(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                E3FollowMaster();
                E3Sound(rt, E3Names.WavWindBlade, 0.0f);
            }
            for (int i = 0; i < f_174; i++)
            {
                int parity = i % 2;
                Fn_b898f0(rt, E3Names.TexWindBlade, parity);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 626 (Adjustment, Gatling Fever): every 4th tick, master flag 0x40000 once + master+0xf6 = 20.</summary>
        [FxEffectProcess(0x272)]
        public FxCaseResult Case_E3_Adjustment(FxRuntime rt)
        {
            if (m_stateCnt % 4 != 0) return FxCaseResult.Continue;
            uint master = m_master;
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x40000) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, flags + 0x40000);
            M.U8(m_master + 0xf6, 0x14);
            return FxCaseResult.Continue;
        }
    }
}
