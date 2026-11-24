// Synced from reference/port/src/RoClassicFx/Rag/npcfx/RagEffect.NpcFx.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// npcfx: the CRagEffect cases the Custom server's monster skills (NPC_*ATTACK, NPC_DARKBREATH, NPC_BLOODDRAIN,
    /// NPC_ENERGYDRAIN, NPC_DARKCROSS / DARKSTRIKE / DARKTHUNDER, NPC_UNDEADATTACK, NPC_POWERUP, NPC_CRITICALWOUND)
    /// and the client's own death effect (EF 372 EF_DEVIL) still needed. Most of them are other cases' bodies with
    /// other arguments (CRagEffect::OnProcess 0xb527f0, vf14 0xb46290, vf15 0xb47410).
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>CP949 sprite names (one char per byte): 이팩트\DarkBreath.spr / .act (0xffadc0 / 0xffadd8).</summary>
        public const string NpcFxSprDarkBreath = "ÀÌÆÑÆ®\\DarkBreath.spr";
        public const string NpcFxActDarkBreath = "ÀÌÆÑÆ®\\DarkBreath.act";

        // ---- STR-only cases ------------------------------------------------------------------------------

        /// <summary>
        /// EF 181 EF_MENTALBREAK (MentalBreak.str), 182 EF_MAGICALATTHIT (magical.str), 190 EF_COMBOATTACK5 (yunta_5.str),
        /// 191 EF_GUIDEDATTACK (homing.str), 192 EF_POISONATTACK (poison.str), 193 EF_SILENCEATTACK (silence.str),
        /// 194 EF_STUNATTACK (stun.str), 197 EF_SLEEPATTACK (sleep.str): case 0xb59c33 of 0xb527f0,
        /// <c>pCVar6 = fn_b36820(this); goto code_r0x00b5293f</c> — the STR player at (0, -80, 0), dead when it ends.
        /// </summary>
        [FxEffectProcess(0xb5, 0xb6, 0xbe, 0xbf, 0xc0, 0xc1, 0xc2, 0xc5)]
        public FxCaseResult Case_Npc_StrOnly(FxRuntime rt) => Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// EF 195 EF_PETRIFYATTACK: case 0xb598ff (shared with EF 23 Stone Curse), <c>fn_b62510(this)</c> = the STR player;
        /// Init's <c>case 0xc3: goto case 0x17</c> loads StoneCurse.str.
        /// </summary>
        [FxEffectProcess(0xc3)]
        public FxCaseResult Case_Npc_PetrifyAttack(FxRuntime rt) => StrPlayerDefault(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// EF 372 EF_DEVIL (the death effect of the own character): vf14 (0xb46290) case 0x48 → 0xb4635a, shared with
        /// EF 371 — <c>fn_b36820</c>, vf14 returns its AL. Init's <c>case 0x174: goto case 0x409</c> loads devil.str.
        /// </summary>
        [FxEffectProcess(0x174)]
        public FxCaseResult Case_Npc_Devil(FxRuntime rt) => Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// EF 677 EF_CRITICALWOUND: vf15 (0xb47410) case 0x2a5 → 0xb4ae00. At tick 1 the owner position and
        /// <c>effect\wideb.wav</c>; every tick the STR player (cwound.str) with its result ignored
        /// (code_r0x00b48e3a → vf15 returns 1).
        /// </summary>
        [FxEffectProcess(0x2a5)]
        public FxCaseResult Case_Npc_CriticalWound(FxRuntime rt)
        {
            if (m_stateCnt == 1) FollowMasterAndPlay(rt, "effect\\wideb.wav", 0f);
            rt.StrPlayer(this, 0f, -80.0f, 0f);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 183 EF_SUI_EXPLOSION (NPC_SELFDESTRUCTION, HVAN_EXPLOSION): case 0xb59815. Tick 0: <c>fn_b3ed70(this, 1)</c>
        /// (EF_HIT2's lens-flare ring, tinted); ticks 3, 7, 9: a screen quake (CGameMode msg 0x7a, 1). Then the STR
        /// player (sui_explosion.str) — code_r0x00b59b02, dead when it ends.
        /// </summary>
        [FxEffectProcess(0xb7)]
        public FxCaseResult Case_Npc_SuiExplosion(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0) Fn_b3ed70(rt, 1);
            else if (t == 3 || t == 7 || t == 9) rt.GameModeSendMsg(0x7a, 1, 0, 0, 0);
            return rt.StrPlayer(this, 0f, -80.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        // ---- EF 180 / 184 dark hit -------------------------------------------------------------------------

        /// <summary>EF 180 EF_HITDARK, 184 EF_DARKATTACK: case 0xb57e66, <c>fn_b405a0(this); break</c>.</summary>
        [FxEffectProcess(0xb4, 0xb8)]
        public FxCaseResult Case_Npc_DarkHit(FxRuntime rt) { Fn_b405a0(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b405a0 (0xb405a0) — the dark hit: EF_HIT1's body (fn_b3e470, effects-7) without the /mineffect
        /// check and with the four particle1 sparks tinted RGB (20, 20, 20): at tick 0 a ring_blue flash (prim 0xd,
        /// 10 ticks) against the owner's heading, two rising (prim 5) and two falling (prim 6) sparks. No sound.
        /// </summary>
        public void Fn_b405a0(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            int dir = FacingAwayFromOwnerE7();
            Prim p = LaunchFlatFlashE7(rt, 10, dir);
            p.f_2f8 = 10.0f;
            p.f_2f4 = 5.0f;
            p.f_360 = 3.5f;
            int d = p.m_duration;
            p.f_1f0 = d - d / 2;
            SetPrimTexture(rt, p, "effect\\ring_blue.tga");
            for (int i = 0; i < 2; i++)
            {
                Prim s = LaunchSparkE7(rt, 5, dir, -0x28);
                s.f_2bc = (float)(RandMod(rt, 0x5a) + 0x3c) / 100.0f;
                s.m_duration = RandMod(rt, 0x18) + 6;
                s.f_2c0 = -(s.f_2bc / (float)s.m_duration) * 0.5f;
                s.f_318 = (float)(RandMod(rt, 100) + 0x3c) / 100.0f;
                FinishDarkSparkNpc(rt, s);
            }
            for (int i = 0; i < 2; i++)
            {
                Prim s = LaunchSparkE7(rt, 6, dir, 0x8c);
                FxMatrix.MakeRotationY(M, s.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(s.f_30c)));
                s.m_duration = RandMod(rt, 0x18) + 6;
                s.f_2bc = (float)(RandMod(rt, 0x5a) + 0x3c) / 100.0f;
                s.f_2c0 = -(s.f_2bc / (float)s.m_duration) * 0.5f;
                s.f_2ec = -(float)(RandMod(rt, 0x5a) + 0x1e) / 100.0f;
                float v = s.f_2ec / (float)s.m_duration;
                s.f_2f0 = -(v + v);
                s.f_318 = (float)(RandMod(rt, 100) + 0x3c) / 100.0f;
                FinishDarkSparkNpc(rt, s);
            }
        }

        /// <summary>
        /// The spark epilogue of fn_b405a0: colour (20, 20, 20) — +0x23c is written as a dword (MOV dword [eax+0x23c], 0x14)
        /// although the field table types it as a byte — before the trail snapshot (which copies the colour), then the
        /// effects-7 epilogue (f_1f0 = 0, particle1, 3 trail entries, snapshot).
        /// </summary>
        void FinishDarkSparkNpc(FxRuntime rt, Prim s)
        {
            M.I32(s.A + 0x23c, 0x14);
            s.f_240 = 0x14;
            s.f_244 = 0x14;
            FinishSparkE7(rt, s);
        }

        // ---- EF 196 curse / EF 198 telekinesis hit / EF 212 dark breath ----------------------------------------

        /// <summary>
        /// EF 196 EF_CURSEATTACK: case 0xb57e76. At tick 0 a prim 0xf (9999 ticks; it dies with the effect, default 150):
        /// f_30c = 0, f_330 = 4.5, 30 higher (f_398 −= 30.0 at 0xf818d8), f_354 = f_360 = 4.0, texture effect\curse.bmp
        /// (shared tail code_r0x00b53137: new[] f_204·4, GetTexture). → epilogue.
        /// </summary>
        [FxEffectProcess(0xc4)]
        public FxCaseResult Case_Npc_CurseAttack(FxRuntime rt)
        {
            if (m_stateCnt != 0) return FxCaseResult.Continue;
            Prim p = LaunchLast(rt, 0xf);
            p.m_duration = 9999;
            p = m_lastPrim; p.f_30c = 0.0f;
            p = m_lastPrim; p.f_330 = 4.5f;
            p = m_lastPrim; p.f_398 = p.f_398 - 30.0f;
            p = m_lastPrim; p.f_354 = 4.0f;
            p = m_lastPrim; p.f_360 = 4.0f;
            SetPrimTexture(rt, m_lastPrim, "effect\\curse.bmp");
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 198 EF_TELEKHIT: case 0xb52ca5 (shared with 0x69, 0xa1, 0x1bb, 0x1e9, 0x1ea) — an empty <c>break</c>; the
        /// default duration is 0, so the effect dies after its first tick. Draws nothing.
        /// </summary>
        [FxEffectProcess(0xc6)]
        public FxCaseResult Case_Npc_TelekinesisHit(FxRuntime rt) => FxCaseResult.Continue;

        /// <summary>
        /// EF 212 EF_DARKBREATH: case 0xb57f59. At tick 0 a sprite prim 5 (DarkBreath.spr/.act, 65 ticks, f_1c4 |= 0x200,
        /// 20 higher (f_398 −= 20.0 at 0xf7d134), scale 0.8, f_1e8 = 1, green / blue 0, f_1f0 = 60) and its trail
        /// snapshot. → epilogue (default duration 500).
        /// </summary>
        [FxEffectProcess(0xd4)]
        public FxCaseResult Case_Npc_DarkBreath(FxRuntime rt)
        {
            if (m_stateCnt != 0) return FxCaseResult.Continue;
            Prim p = LaunchLast(rt, 5);
            p.m_duration = 0x41;
            p = m_lastPrim; p.f_1c4 |= 0x200;
            p = m_lastPrim; p.f_398 = p.f_398 - 20.0f;
            p = m_lastPrim; p.f_318 = 0.8f;
            p = m_lastPrim; p.f_1e8 = 1;
            p = m_lastPrim; p.f_240 = 0;
            p = m_lastPrim; p.f_244 = 0;
            p = m_lastPrim; p.f_1f0 = 0x3c;
            rt.SetSprName(m_lastPrim.A, NpcFxSprDarkBreath);
            rt.SetActName(m_lastPrim.A, NpcFxActDarkBreath);
            m_lastPrim.SnapshotTrail_ad58d0();
            return FxCaseResult.Continue;
        }

        // ---- EF 216 / 217 drains ------------------------------------------------------------------------------

        /// <summary>EF 216 EF_BLOODDRAIN: case 0xb58067, <c>fn_b332d0(this, 3)</c>.</summary>
        [FxEffectProcess(0xd8)]
        public FxCaseResult Case_Npc_BloodDrain(FxRuntime rt) { Fn_b332d0(rt, 3); return FxCaseResult.Continue; }

        /// <summary>EF 217 EF_ENERGYDRAIN: case 0xb58079, <c>fn_b38190(this)</c>.</summary>
        [FxEffectProcess(0xd9)]
        public FxCaseResult Case_Npc_EnergyDrain(FxRuntime rt) { Fn_b38190(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b332d0 (0xb332d0) — at tick 0, <paramref name="count"/> (&gt; 0) drain orbs fanned −90, 0, +90, …
        /// (<see cref="LaunchDrainOrbNpc"/>) tinted RGB (250, 100, 100). <c>ret 4</c>.
        /// </summary>
        public void Fn_b332d0(FxRuntime rt, int count)
        {
            if (m_stateCnt != 0 || count <= 0) return;
            int angle = -0x5a;
            do
            {
                LaunchDrainOrbNpc(rt, angle, 0xfa, 100, 100);
                angle += 0x5a;
                count--;
            } while (count != 0);
        }

        /// <summary>
        /// CRagEffect::fn_b38190 (0xb38190) — at tick 0 three drain orbs fanned −90, 0, +90 (loop while the angle is
        /// below 180) tinted RGB (100, 100, 250).
        /// </summary>
        public void Fn_b38190(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            int angle = -0x5a;
            do
            {
                LaunchDrainOrbNpc(rt, angle, 100, 100, 0xfa);
                angle += 0x5a;
            } while (angle < 0xb4);
        }

        /// <summary>
        /// The loop body of fn_b332d0 / fn_b38190: a particle1 sprite (prim 8, 60 ticks) 10 above the effect, flying to the
        /// effect target (f_180, f_188): f_348 = DistanceXZ (FUN_00bce640, x87 result stored as float), FaceTowards
        /// (FUN_00bce590), f_30c = −heading, speed f_2bc = 2·dist/60, rise f_2ec = 2 easing out (f_2f0 = 2·−(2/60)),
        /// f_334 = −2 (f_340 = 2·(2/60)), fan angle f_314; y rotation, start offset = rot·(0, 0, 1) + translation into
        /// f_3a0.. and +0x290.., colour, scale 1.7 (0x3fd9999a), 7 trail entries.
        /// </summary>
        void LaunchDrainOrbNpc(FxRuntime rt, int angle, int r, int g, int b)
        {
            Prim p = LaunchLast(rt, 8);
            p.m_duration = 0x3c;
            p = m_lastPrim;
            p.f_394 = 0.0f;
            p.f_398 = -10.0f;
            p.f_39c = 0.0f;
            p.f_348 = rt.DistanceXZ(p.A, f_180, f_188);
            rt.FaceTowards(p.A, f_180, f_188);
            p.f_30c = -p.f_4c;
            float v = p.f_348 / (float)p.m_duration;
            p.f_2bc = v + v;
            p.f_2ec = 2.0f;
            v = -(p.f_2ec / (float)p.m_duration);
            p.f_2f0 = v + v;
            p.f_334 = -2.0f;
            v = -(p.f_334 / (float)p.m_duration);
            p.f_340 = v + v;
            p.f_314 = (float)angle;
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            p.f_3a0 = p.f_16c * 0.0f + p.f_160 * 0.0f + p.f_178 + p.f_184;
            p.f_3a4 = p.f_170 * 0.0f + p.f_164 * 0.0f + p.f_17c + p.f_188;
            p.f_3a8 = p.f_174 * 0.0f + p.f_168 * 0.0f + p.f_180 + p.f_18c;
            M.U32(p.A + 0x290, M.U32(p.A + 0x3a0));
            M.U32(p.A + 0x294, M.U32(p.A + 0x3a4));
            M.U32(p.A + 0x298, M.U32(p.A + 0x3a8));
            M.I32(p.A + 0x23c, r);
            p.f_240 = g;
            p.f_244 = b;
            p.f_318 = 1.7f;
            rt.SetSprName(p.A, Effects1Strings.SprParticle1);
            rt.SetActName(p.A, Effects1Strings.ActParticle1);
            p.f_1f4 = 7;
            p.SnapshotTrail_ad58d0();
        }

        // ---- EF 450..455 dark / NPC variants of player skills ---------------------------------------------------

        /// <summary>
        /// EF 450 EF_GRANDCROSS2 (NPC_DARKCROSS / NPC_GRANDDARKNESS): case 0xb564e8, <c>fn_b77820(this, 0, 1);
        /// fn_b77650(this, 1); break</c> (args from the asm: push 1, push 0 / push 1) — Grand Cross with ring_black.
        /// </summary>
        [FxEffectProcess(0x1c2)]
        public FxCaseResult Case_Npc_GrandCross2(FxRuntime rt)
        {
            Fn_b77820(rt, 0, 1);
            Fn_b77650(rt, 1);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 451 EF_SOULSTRIKE2 (NPC_DARKSTRIKE): case 0xb52cce, <c>fn_b60bb0(this, 1)</c> — Soul Strike with particle5.</summary>
        [FxEffectProcess(0x1c3)]
        public FxCaseResult Case_Npc_SoulStrike2(FxRuntime rt) { Fn_b60bb0(rt, 1); return FxCaseResult.Continue; }

        /// <summary>EF 452 EF_YUFITEL2 (NPC_DARKTHUNDER): case 0xb55830, <c>fn_b65d20(this, 1)</c> — Jupitel Thunder's variant 1.</summary>
        [FxEffectProcess(0x1c4)]
        public FxCaseResult Case_Npc_Yufitel2(FxRuntime rt) { Fn_b65d20(rt, 1); return FxCaseResult.Continue; }

        /// <summary>EF 454 EF_DARKCASTING (NPC_UNDEADATTACK): case 0xb5468d, <c>fn_b70590(this, 0)</c> (also runs on 0-tick frames).</summary>
        [FxEffectProcess(0x1c6)]
        public FxCaseResult Case_Npc_DarkCasting(FxRuntime rt) { Fn_b70590(rt, 0); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b70590 (0xb70590) — follows the owner; at tick 0 (unless <paramref name="silent"/>)
        /// <c>effect\EF_BeginSpell.wav</c>, then two ring prims (fn_b88410, effects-4) of ring_black.tga at angles 45 and 25,
        /// variant 6. <c>ret 4</c>.
        /// </summary>
        public void Fn_b70590(FxRuntime rt, byte silent)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            if (silent == 0)
                rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Fn_b88410(rt, 0x2d, "effect\\ring_black.tga", 6);
            Fn_b88410(rt, 0x19, "effect\\ring_black.tga", 6);
        }

        /// <summary>
        /// EF 455 EF_GUMGANGNPC (NPC_POWERUP): case 0xb563d5, <c>fn_b7a730(this, 2, 1)</c> (push 1, push 2) — the spirit
        /// spheres prim 0x23 with two slots tagged 1.
        /// </summary>
        [FxEffectProcess(0x1c7)]
        public FxCaseResult Case_Npc_GumgangNpc(FxRuntime rt) { Fn_b7a730(rt, 2, 1); return FxCaseResult.Continue; }
    }
}
