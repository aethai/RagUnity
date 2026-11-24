// Synced from reference/port/src/RoClassicFx/Rag/effects-5/RagEffect.Effects5.Simple.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>CP949 names used by package effects-5, one char per byte (see FxRuntime.Effects5).</summary>
    static class Fx5Names
    {
        /// <summary>"이팩트\" (effect sprite folder).</summary>
        public const string SprDir = "ÀÌÆÑÆ®\\";
        public const string Particle1Spr = SprDir + "particle1.spr";               // 0xffa408
        public const string Particle1Act = SprDir + "particle1.act";               // 0xffa420
        public const string Particle3Spr = SprDir + "particle3.spr";               // 0xf7d9b0
        public const string Particle3Act = SprDir + "particle3.act";               // 0xf7d9c8
        public const string Particle5Spr = SprDir + "particle5.spr";               // 0xffa558
        public const string Particle5Act = SprDir + "particle5.act";               // 0xffa570
        public const string Sakura01Spr = SprDir + "sakura01.spr";                 // 0xff5340
        public const string Sakura01Act = SprDir + "sakura01.act";                 // 0xff5354
        /// <summary>"이팩트\성수뜨기.spr" / ".act" (0xffa964 / 0xffa978).</summary>
        public const string HolyWaterSpr = SprDir + "¼º¼ö¶ß±â.spr";
        public const string HolyWaterAct = SprDir + "¼º¼ö¶ß±â.act";
        /// <summary>"effect\대폭발.tga" (0xfe237c).</summary>
        public const string BigExplosionTga = "effect\\´ëÆø¹ß.tga";
        /// <summary>"effect\복호격.wav" (0xff7b48).</summary>
        public const string WavStripWeapon = "effect\\º¹È£°Ý.wav";
        /// <summary>"effect\세크리파이스.wav" (0xff7734).</summary>
        public const string WavSacrifice = "effect\\¼¼Å©¸®ÆÄÀÌ½º.wav";
        /// <summary>"effect\mon_맹룡과강.wav" (0xff3b80).</summary>
        public const string WavGlacierFist = "effect\\mon_¸Í·æ°ú°­.wav";
        /// <summary>"effect\애로우 발칸.wav" (0xff95c8).</summary>
        public const string WavArrowVulcan = "effect\\¾Ö·Î¿ì ¹ßÄ­.wav";
        /// <summary>"effect\헤드 크러쉬.wav" (0xff9534).</summary>
        public const string WavHeadCrush = "effect\\Çìµå Å©·¯½¬.wav";
        /// <summary>"effect\비트 조인트.wav" (0xff954c).</summary>
        public const string WavJointBeat = "effect\\ºñÆ® Á¶ÀÎÆ®.wav";
        /// <summary>"effect\메테오 어썰트.wav" (0xff9598).</summary>
        public const string WavMeteorAssault = "effect\\¸ÞÅ×¿À ¾î½äÆ®.wav";
        /// <summary>"effect\T_날라차기.wav" (0xff80d8).</summary>
        public const string WavFlyingKick = "effect\\T_³¯¶óÂ÷±â.wav";
        /// <summary>"effect\연주붕격.wav" (0xff7770).</summary>
        public const string WavChainCrush = "effect\\¿¬ÁÖºØ°Ý.wav";
        /// <summary>"effect\더스트샷.wav" (0xff84ec).</summary>
        public const string WavDust = "effect\\´õ½ºÆ®¼¦.wav";
        /// <summary>"effect\빙정락.wav" (0xff84d8).</summary>
        public const string WavIceMeteor = "effect\\ºùÁ¤¶ô.wav";
    }

    public readonly partial struct RagEffect
    {
        /// <summary>fn_b36820 / fn_b62510: the STR player at (0, -80, 0), true while it runs.</summary>
        bool StrPlayerDefault(FxRuntime rt) => rt.StrPlayer(this, 0f, -80.0f, 0f);

        /// <summary>EF 0x17 (Stone Curse): fn_b62510 = STR player only.</summary>
        [FxEffectProcess(0x17)]
        public FxCaseResult Case_StoneCurse(FxRuntime rt) => StrPlayerDefault(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>EF 0x5f (Quagmire): 1-in-12 chance of the sound at tick 0, then the STR player.</summary>
        [FxEffectProcess(0x5f)]
        public FxCaseResult Case_Quagmire(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                int r = rt.Rand.Next() % 0xc;
                if ((r < 0 ? 0 : r) == 0)
                    rt.PlaySound3DRelative("effect\\wizard_quagmire.wav", m_pos_x, -100.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return StrPlayerDefault(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>EF 0x78 (Cloaking): CRagEffect::fn_b347a0 — sound at tick 0, owner alpha 0x32, f_a4 = 0.</summary>
        [FxEffectProcess(0x78)]
        public FxCaseResult Case_Cloaking(FxRuntime rt)
        {
            if (m_stateCnt == 0)
                rt.PlaySound3DRelative("effect\\assasin_cloaking.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            rt.SetArgb(M.U32(A + 0x138), 0x32, -1, -1, -1);
            f_a4 = 0;
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x91 (Shockwave Trap): the STR player, rewound (fn_b43cb0) whenever it ends — never dies by itself.</summary>
        [FxEffectProcess(0x91)]
        public FxCaseResult Case_ShockwaveTrap(FxRuntime rt)
        {
            if (!StrPlayerDefault(rt)) Fn_b43cb0();
            return FxCaseResult.Continue;
        }

        /// <summary>CRagEffect::fn_b43cb0 (0xb43cb0): rewinds the STR player (frame 0, last frame, per-layer key cursors).</summary>
        public void Fn_b43cb0()
        {
            uint str = M.U32(A + 0x7f0);
            M.I32(A + 0x119f4, 0);
            m_stateCnt = 0;
            int layers = M.I32(str + 8);
            M.I32(A + 0x119f8, layers);
            if (M.I32(str + 0x388) == 0) M.I32(A + 0x119f8, layers - 1);
            for (int i = 0; i < M.I32(M.U32(A + 0x7f0) + 8); i++)
                M.U32(A + 0x11a7c + (uint)i * 4, 0);
        }

        /// <summary>EF 0xff (Endow Blaze): sound at tick 10 (y -150) at the owner, then the STR player.</summary>
        [FxEffectProcess(0xff)]
        public FxCaseResult Case_EndowBlaze(FxRuntime rt)
        {
            if (m_stateCnt == 10)
            {
                FollowMaster();
                rt.PlaySound3DRelative("_enemy_hit_wind1.wav", m_pos_x, -150.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return StrPlayerDefault(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>EF 0x10d (Divest Weapon): sound at tick 6 at the owner, then the STR player.</summary>
        [FxEffectProcess(0x10d)]
        public FxCaseResult Case_DivestWeapon(FxRuntime rt)
        {
            if (m_stateCnt == 6)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavStripWeapon, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            return StrPlayerDefault(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
        }

        /// <summary>EF 0x19f (Counter Kick): hit sounds at ticks 0x11/0x19, owner flag 0x80 + frame counter from tick 30.</summary>
        [FxEffectProcess(0x19f)]
        public FxCaseResult Case_CounterKick(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x11)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\EF_hit2.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t == 0x19)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\EF_hit4.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            uint master = M.U32(A + 0x138);
            int flags = M.I32(master + 0xe8);
            if ((sbyte)flags >= 0 && t >= 0x1e)
            {
                M.I32(master + 0xe8, flags + 0x80);
                M.I16(M.U32(A + 0x138) + 0xf4, (short)(M.I16(A + 0x178) - 0x1e));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x1a9 (Esma): owner flag 0x1000 + frame counter from tick 30.</summary>
        [FxEffectProcess(0x1a9)]
        public FxCaseResult Case_Esma(FxRuntime rt)
        {
            if (m_stateCnt >= 0x1e)
            {
                uint master = M.U32(A + 0x138);
                M.U32(master + 0xe8, M.U32(master + 0xe8) | 0x1000);
                M.I16(M.U32(A + 0x138) + 0xf4, (short)(M.I16(A + 0x178) - 0x1e));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x271 (Madness Canceller, vf15): every 4th tick owner flag 0x4000 (once) and +0xf6 = 0x14.</summary>
        [FxEffectProcess(0x271)]
        public FxCaseResult Case_MadnessCanceller(FxRuntime rt)
        {
            if (m_stateCnt % 4 != 0) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & 0x4000) != 0) return FxCaseResult.Continue;
            M.U32(master + 0xe8, flags + 0x4000);
            M.U8(M.U32(A + 0x138) + 0xf6, 0x14);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x27c (Ice Meteor, vf15): sound at tick 0 at the owner, then the STR player (result ignored).</summary>
        [FxEffectProcess(0x27c)]
        public FxCaseResult Case_IceMeteor(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavIceMeteor, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            StrPlayerDefault(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x286 (Tracking, vf15): the STR player, result ignored.</summary>
        [FxEffectProcess(0x286)]
        public FxCaseResult Case_Tracking(FxRuntime rt)
        {
            StrPlayerDefault(rt);
            return FxCaseResult.Continue;
        }
    }
}
