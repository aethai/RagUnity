// Synced from reference/port/src/RoClassicFx/Rag/effects-10/RagEffect.Effects10.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Resource names used by the effects-10 cases: the client's CP949 bytes mapped 1:1 to chars (see PORTING.md),
    /// with the game.exe address of each.
    /// </summary>
    public static class E10Names
    {
        /// <summary>0xf7e6e0 이팩트\particle2.spr (Ruwach orb).</summary>
        public const string SprParticle2 = "ÀÌÆÑÆ®\\particle2.spr";
        /// <summary>0xf7e6f8 이팩트\particle2.act.</summary>
        public const string ActParticle2 = "ÀÌÆÑÆ®\\particle2.act";
        /// <summary>0xff80f0 effect\마법진1-3.bmp (Oratio, EF 755).</summary>
        public const string TexMagicCircle13 = "effect\\¸¶¹ýÁø1-3.bmp";
        /// <summary>0xff8108 effect\베놈_상태.bmp (Venom Impress, EF 788).</summary>
        public const string TexVenomState = "effect\\º£³ð_»óÅÂ.bmp";
        /// <summary>0xff8120 effect\빙결_상태.bmp (Frost Misty, EF 789).</summary>
        public const string TexFreezeState = "effect\\ºù°á_»óÅÂ.bmp";
        /// <summary>0xff8138 effect\얼음결정체1.tga (Cold status, EF 940).</summary>
        public const string TexIceCrystal1 = "effect\\¾óÀ½°áÁ¤Ã¼1.tga";
        /// <summary>0xff8150 effect\발화_상태.bmp (Burning, EF 790).</summary>
        public const string TexBurningState = "effect\\¹ßÈ­_»óÅÂ.bmp";
        /// <summary>0xff769c / 0xff76b0 Chase Walk footprints (EF 326 / 327).</summary>
        public const string TexFootLeft = "effect\\foot_l_b.tga", TexFootRight = "effect\\foot_r_b.tga";

        /// <summary>(game.exe address, value) of every non-trivial name above (checked byte for byte by the tests).</summary>
        public static readonly (uint Address, string Value)[] Table =
        {
            (0x00f7e6e0, SprParticle2), (0x00f7e6f8, ActParticle2), (0x00ff80f0, TexMagicCircle13),
            (0x00ff8108, TexVenomState), (0x00ff8120, TexFreezeState), (0x00ff8138, TexIceCrystal1),
            (0x00ff8150, TexBurningState), (0x00ff769c, TexFootLeft), (0x00ff76b0, TexFootRight),
        };
    }

    /// <summary>
    /// effects-10: classic effect ids the client launches from status / option / EFST packets
    /// (out/status_effects_coverage.tsv, status MISSING). Simple cases: owner body-state flags, owner tints,
    /// sound + STR tails and cases that call helpers earlier packages ported.
    /// </summary>
    public readonly partial struct RagEffect
    {
        // ---- CRagEffect::OnProcess (0xb527f0) cases -----------------------------------------------------------

        /// <summary>
        /// EF 33 (0x21, EF_RUWACH; case 0xb52d06): follows the owner; every 3rd tick one orbiting particle2 sprite
        /// (prim 5, 25 ticks) at angle cvtt((float)(stateCnt / 1.3 · −5)) radius 15, y −10, plus its Shadow.spr
        /// ground mark. Launched per tick by the actor while option 0x2000 is set (FUN_00bd4210, every 200 ticks).
        /// </summary>
        [FxEffectProcess(0x21)]
        public FxCaseResult Case_E10_Ruwach(FxRuntime rt)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t % 3 != 0) return FxCaseResult.Continue;
            int deg = FxOps.Cvtt((float)((double)t / 1.3 * -5.0));
            float s = FxMath.GetSin(deg);
            float c = FxMath.GetCos(deg);
            FxMath.DegToRad(deg);   // result discarded (fstp st0)

            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            s = s * 15.0f;
            m_lastPrim = p;
            p.m_duration = 0x19;
            p = m_lastPrim;
            p.f_394 = s;
            c = c * -15.0f;
            p.f_398 = -10.0f;
            p.f_39c = c;
            rt.SetSprName(m_lastPrim.A, E10Names.SprParticle2);
            rt.SetActName(m_lastPrim.A, E10Names.ActParticle2);
            { Prim lp = m_lastPrim; lp.f_318 = 3.0f; }
            { Prim lp = m_lastPrim; lp.f_31c = -0.1f; }
            { Prim lp = m_lastPrim; lp.f_324 = 250.0f; }
            { Prim lp = m_lastPrim; lp.f_328 = -3.0f; }
            { Prim lp = m_lastPrim; lp.f_1f0 = lp.m_duration - 6; }
            m_lastPrim.SnapshotTrail_ad58d0();

            p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0x19;
            p = m_lastPrim;
            p.f_394 = s;
            p.f_398 = 0.0f;
            p.f_39c = c;
            rt.SetSprName(m_lastPrim.A, "Shadow.spr");
            rt.SetActName(m_lastPrim.A, "Shadow.act");
            { Prim lp = m_lastPrim; lp.f_1e8 = 2; }
            { Prim lp = m_lastPrim; lp.f_318 = 1.5f; }
            { Prim lp = m_lastPrim; lp.f_31c = -0.06f; }
            { Prim lp = m_lastPrim; lp.f_324 = 150.0f; }
            { Prim lp = m_lastPrim; lp.f_328 = -2.5f; }
            { Prim lp = m_lastPrim; lp.f_1f0 = lp.m_duration - 6; }
            m_lastPrim.SnapshotTrail_ad58d0();
            return FxCaseResult.Continue;
        }

        /// <summary>EF 158 (0x9e, EF_JOBLVUP; case 0xb59c33): the STR player (JobLvUP.str through Init's alias to 0x151).</summary>
        [FxEffectProcess(0x9e)]
        public FxCaseResult Case_E10_JobLevelUp(FxRuntime rt) => Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// EF 426 (0x1aa, EF_QUAKEBODY; case 0xb5753a): while owner flag 0x2000 is clear, sets it and stores stateCnt in
        /// the owner word +0xf4 (the sprite-shake flag, see effects-9 notes).
        /// </summary>
        [FxEffectProcess(0x1aa)]
        public FxCaseResult Case_E10_QuakeBody(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x2000) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, flags + 0x2000);
            M.I16(M.U32(A + 0x138) + 0xf4, (short)m_stateCnt);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 453 (0x1c5, EF_NPC_STOP; case 0xb55d9d): fn_b82770(this, 0) (effects-3).</summary>
        [FxEffectProcess(0x1c5)]
        public FxCaseResult Case_E10_NpcStop(FxRuntime rt) { Fn_b82770(rt, 0); return FxCaseResult.Continue; }

        // ---- CRagEffect::vf14 (0xb46290) cases: a case that breaks runs the STR player (fn_b36820) -----------

        /// <summary>EF 305 (0x131, EF_PHARMACY_OK; case 0xb462d7): tick 0 follow + sound p_success.wav, then the STR.</summary>
        [FxEffectProcess(0x131)]
        public FxCaseResult Case_E10_PharmacyOk(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\p_success.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>EF 306 (0x132, EF_PHARMACY_FAIL; case 0xb4636e): tick 0 follow + sound p_failed.wav, then the STR.</summary>
        [FxEffectProcess(0x132)]
        public FxCaseResult Case_E10_PharmacyFail(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\p_failed.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>EF 371 (0x173, EF_ANGEL; case 0xb4635a): the STR player only (angel.str).</summary>
        [FxEffectProcess(0x173)]
        public FxCaseResult Case_E10_Angel(FxRuntime rt) => Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        // ---- CRagEffect::vf15 (0xb47410) cases: vf15 returns 1 after a break --------------------------------

        /// <summary>
        /// EF 540 (0x21c; case 0xb4c483): ticks 0..15, while owner flag 0x10 is clear: sets it, owner byte +0xf0 =
        /// 45 − 3·stateCnt.
        /// </summary>
        [FxEffectProcess(0x21c)]
        public FxCaseResult Case_E10_540(FxRuntime rt)
        {
            if (m_stateCnt > 0xf) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x10) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, flags + 0x10);
            byte t = (byte)m_stateCnt;
            M.U8(M.U32(A + 0x138) + 0xf0, (byte)(0x2d - (byte)(t + (byte)(t + t))));
            return FxCaseResult.Continue;
        }

        /// <summary>EF 568 (0x238, EF_HO_UP; case 0xb4d740): tick 0 follow + sound levelup.wav, then the STR (h_levelup.str).</summary>
        [FxEffectProcess(0x238)]
        public FxCaseResult Case_E10_HomunLevelUp(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("levelup.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>EF 586 (0x24a; case 0xb4d8f4): fn_b72000(10) (effects-1 status sprite).</summary>
        [FxEffectProcess(0x24a)]
        public FxCaseResult Case_E10_586(FxRuntime rt) { Fn_b72000(rt, 10); return FxCaseResult.Continue; }

        /// <summary>
        /// EF 667 (0x29b; case 0xb4af66): while owner flag 2 is clear, sets it and stores stateCnt · 8 (stateCnt &lt; 25)
        /// or 200 in the owner byte +0xf2.
        /// </summary>
        [FxEffectProcess(0x29b)]
        public FxCaseResult Case_E10_667(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 2) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, flags + 2);
            int t = m_stateCnt;
            if (t < 0x19) M.U8(M.U32(A + 0x138) + 0xf2, (byte)(t << 3));
            else M.U8(M.U32(A + 0x138) + 0xf2, 0xc8);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 744 (0x2e8; case 0xb4a3fc): fn_b347a0 — the Cloaking body (effects-5 EF 0x78): tick 0 sound, owner alpha 0x32.</summary>
        [FxEffectProcess(0x2e8)]
        public FxCaseResult Case_E10_744(FxRuntime rt) => Case_Cloaking(rt);

        /// <summary>EF 755 (0x2f3, EF_ORATIO; case 0xb4a0a7): fn_b70c60(this, "effect\마법진1-3.bmp", 0x13) (effects-9).</summary>
        [FxEffectProcess(0x2f3)]
        public FxCaseResult Case_E10_Oratio(FxRuntime rt) { Fn_b70c60(rt, E10Names.TexMagicCircle13, 0x13); return FxCaseResult.Continue; }

        /// <summary>EF 788 (0x314, EF_VENOMIMPRESS; case 0xb4a0b8): fn_b70c60(this, "effect\베놈_상태.bmp", 0x16).</summary>
        [FxEffectProcess(0x314)]
        public FxCaseResult Case_E10_VenomImpress(FxRuntime rt) { Fn_b70c60(rt, E10Names.TexVenomState, 0x16); return FxCaseResult.Continue; }

        /// <summary>EF 789 (0x315, EF_FROSTMISTY; case 0xb4a0c9): fn_b70c60(this, "effect\빙결_상태.bmp", 0x16).</summary>
        [FxEffectProcess(0x315)]
        public FxCaseResult Case_E10_FrostMisty(FxRuntime rt) { Fn_b70c60(rt, E10Names.TexFreezeState, 0x16); return FxCaseResult.Continue; }

        /// <summary>EF 790 (0x316, EF_BURNING; case 0xb4a0eb): fn_b70c60(this, "effect\발화_상태.bmp", 0x16).</summary>
        [FxEffectProcess(0x316)]
        public FxCaseResult Case_E10_Burning(FxRuntime rt) { Fn_b70c60(rt, E10Names.TexBurningState, 0x16); return FxCaseResult.Continue; }

        /// <summary>
        /// EF 799 (0x31f; case 0xb494e9, Stasis): tick 6 sound wl_stasis.wav at (0,0,0) (absolute); ticks 6..40 blink
        /// the owner (even: flag 1 + ARGB(−1, 0, 0, 0xff), odd: white); every tick six fn_b7a040 thunder balls.
        /// </summary>
        [FxEffectProcess(0x31f)]
        public FxCaseResult Case_E10_799(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 6)
            {
                rt.PlaySound3DAbsolute("effect\\wl_stasis.wav", 0f, 0f, 0f);
                t = m_stateCnt;
            }
            if ((uint)(t - 6) <= 0x22)
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
                    rt.SetArgb(master, -1, 0, 0, 0xff);
                }
                else rt.SetArgb(master, -1, 0xff, 0xff, 0xff);
            }
            Fn_b7a040(rt, "effect\\thunder_ball_0001.bmp", -10, 3);
            Fn_b7a040(rt, "effect\\thunder_ball_0002.bmp", -14, 3);
            Fn_b7a040(rt, "effect\\thunder_ball_0003.bmp", -18, 3);
            Fn_b7a040(rt, "effect\\thunder_ball_0004.bmp", -22, 3);
            Fn_b7a040(rt, "effect\\thunder_ball_0005.bmp", -26, 3);
            Fn_b7a040(rt, "effect\\thunder_ball_0006.bmp", -30, 3);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 802 (0x322; case 0xb49438, White Imprison, opt1 8): tick 6 sound wl_whiteimprison.wav at (0,0,0)
        /// (absolute); fn_b67e70("effect\alpha_down.tga", 0x14) and (…, 0x15) every tick (effects-3).
        /// </summary>
        [FxEffectProcess(0x322)]
        public FxCaseResult Case_E10_WhiteImprison(FxRuntime rt)
        {
            if (m_stateCnt == 6) rt.PlaySound3DAbsolute("effect\\wl_whiteimprison.wav", 0f, 0f, 0f);
            Fn_b67e70(rt, "effect\\alpha_down.tga", 0x14);
            Fn_b67e70(rt, "effect\\alpha_down.tga", 0x15);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 809 (0x329; case 0xb4905f): fn_b95ef0 — tick 0 sound assasin_cloaking.wav (player-relative), owner
        /// ARGB(0x32, 0, 0, 0) every tick, effect byte +0xa4 = 0 (the Cloaking body with a black tint).
        /// </summary>
        [FxEffectProcess(0x329)]
        public FxCaseResult Case_E10_809(FxRuntime rt) { Fn_b95ef0(rt); return FxCaseResult.Continue; }

        /// <summary>CRagEffect::fn_b95ef0 (0xb95ef0) — see <see cref="Case_E10_809"/>.</summary>
        public void Fn_b95ef0(FxRuntime rt)
        {
            if (m_stateCnt == 0)
                rt.PlaySound3DRelative("effect\\assasin_cloaking.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            rt.SetArgb(M.U32(A + 0x138), 0x32, 0, 0, 0);
            M.U8(A + 0xa4, 0);
        }

        /// <summary>EF 940 (0x3ac, EF_COLDSTATUS; case 0xb4a0da): fn_b70c60(this, "effect\얼음결정체1.tga", 0x16).</summary>
        [FxEffectProcess(0x3ac)]
        public FxCaseResult Case_E10_ColdStatus(FxRuntime rt) { Fn_b70c60(rt, E10Names.TexIceCrystal1, 0x16); return FxCaseResult.Continue; }

        /// <summary>EF 963 (0x3c3; case 0xb474be): SetARGB(owner, −1, 0x69, 0x69, 0x69) every tick.</summary>
        [FxEffectProcess(0x3c3)]
        public FxCaseResult Case_E10_963(FxRuntime rt)
        {
            rt.SetArgb(M.U32(A + 0x138), -1, 0x69, 0x69, 0x69);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 1003 (0x3eb; case 0xb47476): while owner flag 0x4000 is clear, sets it and owner byte +0xf6 = 20 (blue glow).</summary>
        [FxEffectProcess(0x3eb)]
        public FxCaseResult Case_E10_1003(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x4000) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, flags + 0x4000);
            M.U8(M.U32(A + 0x138) + 0xf6, 0x14);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 1019 / 1021 / 1055 (0x3fb / 0x3fd / 0x41f; label 0xb4be3d): the STR player only.</summary>
        [FxEffectProcess(0x3fb, 0x3fd, 0x41f)]
        public FxCaseResult Case_E10_StrOnly(FxRuntime rt) => Fn_b36820(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>EF 1037 (0x40d; case 0xb4dba9): SetARGB(owner, −1, 0xfb, 200, 4) every tick.</summary>
        [FxEffectProcess(0x40d)]
        public FxCaseResult Case_E10_1037(FxRuntime rt)
        {
            rt.SetArgb(M.U32(A + 0x138), -1, 0xfb, 200, 4);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 1040 (0x410, EF_GC_DARKCROW; case 0xb4dbe8): the STR player at (0, 40, 0).</summary>
        [FxEffectProcess(0x410)]
        public FxCaseResult Case_E10_DarkCrow(FxRuntime rt) => rt.StrPlayer(this, 0f, 40.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>EF 1048 (0x418, EF_WL_TELEKINESIS_INTENSE; case 0xb4de8d): the STR player at (0, 0, 0).</summary>
        [FxEffectProcess(0x418)]
        public FxCaseResult Case_E10_TelekinesisIntense(FxRuntime rt) => rt.StrPlayer(this, 0f, 0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// EF 1049 (0x419, EF_GN_ILLUSIONDOPING; case 0xb4deb1): tick 0 sound gn_illusiondoping.wav (player-relative, no
        /// follow), then the STR player at (0, −80, 0).
        /// </summary>
        [FxEffectProcess(0x419)]
        public FxCaseResult Case_E10_IllusionDoping(FxRuntime rt)
        {
            if (m_stateCnt == 0)
                rt.PlaySound3DRelative("effect\\gn_illusiondoping.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            return rt.StrPlayer(this, 0f, -80.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>EF 1054 (0x41e; case 0xb4e13d): fn_b72000(0x58) (effects-1 status sprite).</summary>
        [FxEffectProcess(0x41e)]
        public FxCaseResult Case_E10_1054(FxRuntime rt) { Fn_b72000(rt, 0x58); return FxCaseResult.Continue; }

        /// <summary>
        /// EF 1057 (0x421, EF_AB_OFFERTORIUM_RING; case 0xb4de49): the STR player at (0, −5 − FUN_00bd09c0(owner), 0) —
        /// the owner's mount/job height step (int, see <see cref="FxRuntime.ActorMountHeight_bd09c0"/>).
        /// </summary>
        [FxEffectProcess(0x421)]
        public FxCaseResult Case_E10_OffertoriumRing(FxRuntime rt)
        {
            int h = rt.ActorMountHeight_bd09c0(M.U32(A + 0x138));
            return rt.StrPlayer(this, 0f, -5.0f - (float)h, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }
    }
}

namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// EF 80 (0x50, EF_SPEARBMR; OnProcess case 0xb557dc): fn_b61000 (effects-1). Registered by effects-10: the
        /// SetAction the helper calls on the effect itself reads the effect's default ACT (+0x108, the CRenderObject
        /// ctor's NPC\1_ETC_01.act — <see cref="FxRuntime.DefaultEffectAct"/>), so the original does not fault.
        /// </summary>
        [FxEffectProcess(0x50)]
        public FxCaseResult Case_E10_SpearBoomerang(FxRuntime rt) { Fn_b61000(rt); return FxCaseResult.Continue; }
    }
}
