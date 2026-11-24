// Synced from reference/port/src/RoClassicFx/Rag/effects-8/RagEffect.Effects8.Cases.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-8: effect ids the original client launches for tracker skills (game.exe skill→effect table) that the
    /// first packages did not cover. Case arguments come from the jump-table blocks (OnProcess 0xb5a19c + id·4,
    /// vf14 0xb472e4 + (id − 300)·4, vf15 0xb50a04 + (id − 0x1eb)·4), disassembled with port/tools/Disasm.
    /// </summary>
    public readonly partial struct RagEffect
    {
        // ---- thin cases over helpers other packages already ported ----------------------------------------

        /// <summary>EF 203 (0xcb) Explosion Spirits — fn_b7a730(1, 0): one spirit-sphere slot on the owner.</summary>
        [FxEffectProcess(0xcb)]
        public FxCaseResult Case_E8_ExplosionSpirits(FxRuntime rt) { Fn_b7a730(rt, 1, 0); return FxCaseResult.Continue; }

        /// <summary>EF 264 (0x108) Investigate — 5 × fn_b8cd50 (lightning strands towards the target); goto 0xb52cab.</summary>
        [FxEffectProcess(0x108)]
        public FxCaseResult Case_E8_Investigate(FxRuntime rt)
        {
            for (int i = 5; i != 0; i--) Fn_b8cd50(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 266 (0x10a) Asura Strike hit — 24 × fn_b8cb60("effect\lens1.tga", 1); goto 0xb52cab.</summary>
        [FxEffectProcess(0x10a)]
        public FxCaseResult Case_E8_AsuraStrikeHit(FxRuntime rt)
        {
            for (int i = 0x18; i != 0; i--) Fn_b8cb60(rt, "effect\\lens1.tga", 1);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 461 (0x1cd) Seven Wind — fn_b8bd10("effect\storm2.tga", 3), fn_b867b0(2), fn_b867b0(3).</summary>
        [FxEffectProcess(0x1cd)]
        public FxCaseResult Case_E8_SevenWind(FxRuntime rt)
        {
            Fn_b8bd10(rt, "effect\\storm2.tga", 3);
            Fn_b867b0(rt, 2);
            Fn_b867b0(rt, 3);
            return FxCaseResult.Continue;
        }

        // ---- owner render flags (owner +0xe8, cleared by the OnProcess prologue when owner +0xec == 1) ---------

        /// <summary>EF 375 (0x177) Assumptio — owner flag 8 every tick (the actor draws the aura).</summary>
        [FxEffectProcess(0x177)]
        public FxCaseResult Case_E8_Assumptio(FxRuntime rt) { AddMasterFlag(8); return FxCaseResult.Continue; }

        /// <summary>EF 382 (0x17e) Magic Power (Mystical Amplification) — owner flag 4 every tick.</summary>
        [FxEffectProcess(0x17e)]
        public FxCaseResult Case_E8_MagicPower(FxRuntime rt) { AddMasterFlag(4); return FxCaseResult.Continue; }

        /// <summary>EF 474 (0x1da) Aura Blade — owner flag 2 every tick.</summary>
        [FxEffectProcess(0x1da)]
        public FxCaseResult Case_E8_AuraBladeFlag(FxRuntime rt) { AddMasterFlag(2); return FxCaseResult.Continue; }

        /// <summary>EF 407 (0x197) Soul Change — only the STR layer (Init: "사람효과.str"), result ignored (code_r0x00b56bd3).</summary>
        [FxEffectProcess(0x197)]
        public FxCaseResult Case_E8_SoulChange(FxRuntime rt) => StrIgnoreResult(rt);

        /// <summary>"effect\T_돌려차기.wav" (0xff9624).</summary>
        public const string Wav_TurnKick_ff9624 = "effect\\T_µ¹·ÁÂ÷±â.wav";

        /// <summary>
        /// EF 466 (0x1d2) Turn Kick — at tick 5 snap to the owner + T_돌려차기.wav; while flag 0x40 is clear and
        /// t ∈ [14, 49]: flag 0x40 and owner +0xf4 = (short)(t − 14) (the actor's spin counter).
        /// </summary>
        [FxEffectProcess(0x1d2)]
        public FxCaseResult Case_E8_TurnKick(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 5)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Wav_TurnKick_ff9624, m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 0x40) == 0 && (uint)(t - 0xe) <= 0x23)
            {
                M.U32(master + 0xe8, f + 0x40);
                M.I16(M.U32(A + 0x138) + 0xf4, (short)(m_stateCnt - 0xe));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 502 (0x1f6, vf15) Spiral Pierce — tick 0: snap + EF_BeginSpell.wav; for t ≤ 140: flag 8, and on even
        /// ticks flag 1 + owner tint (250, 250, 100), on odd ticks white (code_r0x00b4ca1c / 0xb4e168).
        /// </summary>
        [FxEffectProcess(0x1f6)]
        public FxCaseResult Case_E8_SpiralPierce(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if ((uint)t > 0x8c) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            uint f = M.U32(master + 0xe8);
            if ((f & 8) == 0)
            {
                M.U32(master + 0xe8, f + 8);
                t = m_stateCnt;
            }
            if ((t & 1) == 0)
            {
                AddMasterFlag(1);
                MasterArgb(rt, -1, 0xfa, 0xfa, 100);
            }
            else MasterArgb(rt, -1, 0xff, 0xff, 0xff);
            return FxCaseResult.Continue;
        }

        // ---- vf14 cases ---------------------------------------------------------------------------------------

        /// <summary>EF 328 (0x148, vf14) Asura Strike cast — fn_b68d00(0); returns true.</summary>
        [FxEffectProcess(0x148)]
        public FxCaseResult Case_E8_AsuraStrikeCast(FxRuntime rt) { Fn_b68d00(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 342 (0x156, vf14) Resurrect Homunculus — fn_b6fe70("effect\ring_red.tga"); returns true.</summary>
        [FxEffectProcess(0x156)]
        public FxCaseResult Case_E8_ResurrectHomunculus(FxRuntime rt) { Fn_b6fe70(rt, "effect\\ring_red.tga"); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b6fe70 (0xb6fe70) — follows the owner; at tick 0 EF_BeginSpell.wav and two ring prims
        /// (fn_b88410 at 45° and 25°, variant 3) of <paramref name="texture"/>.
        /// </summary>
        public void Fn_b6fe70(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Fn_b88410(rt, 0x2d, texture, 3);
            Fn_b88410(rt, 0x19, texture, 3);
        }

        /// <summary>
        /// CRagEffect::fn_b68d00 (0xb68d00) — Asura Strike charge: follows the owner; tick 0 stretches the effect to
        /// ≥ 350 ticks, EF_BeginSpell.wav and two white rings (fn_b88410 45°/25°, variant 2); ticks 1 and 21 each
        /// launch a prim 0x44 (flags 5, three textures asura3/2/1 then asura4/5/6 — asura13/12/11, 14/15/16 when
        /// <paramref name="alt"/> ≠ 0) with three rising planes: block k gets f_3f4 = −40 + 20k (tick 1) or −40 − 20k
        /// (tick 21), size 18 (24 alt), f_3fc = ∓(6 + 12k) (∓(8 + 16k) alt), f_450 = k, f_454 = 0 (3 alt).
        /// </summary>
        public void Fn_b68d00(FxRuntime rt, sbyte alt)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t == 0)
            {
                if (f_17c < 0x15e) f_17c = 0x15e;
                rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Fn_b88410(rt, 0x2d, "effect\\ring_white.tga", 2);
                Fn_b88410(rt, 0x19, "effect\\ring_white.tga", 2);
                t = m_stateCnt;
            }
            if (t == 1)
            {
                Prim p = E8_AsuraPrim_b68d00(rt, alt != 0
                    ? new[] { "effect\\asura13.tga", "effect\\asura12.tga", "effect\\asura11.tga" }
                    : new[] { "effect\\asura3.tga", "effect\\asura2.tga", "effect\\asura1.tga" });
                int life = -0x28, off = -6, offAlt = -8;
                sbyte k = 0;
                uint b = 0;
                do
                {
                    E8_AsuraBlock_b68d00(p, b, life, k, alt, alt == 0 ? off : offAlt);
                    off -= 0xc;
                    k++;
                    offAlt -= 0x10;
                    life += 0x14;
                    b += 0xb4;
                } while (-0x2a < off);
                t = m_stateCnt;
            }
            if (t == 0x15)
            {
                Prim p = E8_AsuraPrim_b68d00(rt, alt != 0
                    ? new[] { "effect\\asura14.tga", "effect\\asura15.tga", "effect\\asura16.tga" }
                    : new[] { "effect\\asura4.tga", "effect\\asura5.tga", "effect\\asura6.tga" });
                int life = -0x28, off = 6, offAlt = 8;
                sbyte k = 0;
                uint b = 0;
                do
                {
                    E8_AsuraBlock_b68d00(p, b, life, k, alt, alt == 0 ? off : offAlt);
                    offAlt += 0x10;
                    k++;
                    life -= 0x14;
                    off += 0xc;
                    b += 0xb4;
                } while (-100 < life);
            }
        }

        /// <summary>fn_b68d00's prim 0x44 preamble: flags (0 | 1 | 5), duration, three textures, f_318 = 0, 4 blocks off.</summary>
        Prim E8_AsuraPrim_b68d00(FxRuntime rt, string[] tex)
        {
            Prim p = LaunchEffectPrim(rt, 0x44, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 0;
            p.f_1c4 |= 1;
            p.f_1c4 |= 5;
            p.m_duration = f_17c;
            p.f_204 = 3;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(tex[0]));
            M.U32((uint)p.f_144 + 4, rt.GetTexture(tex[1]));
            M.U32((uint)p.f_144 + 8, rt.GetTexture(tex[2]));
            p.f_318 = 0f;
            for (uint o = 0; o < 0x2d0; o += 0xb4) M.U8(p.A + 0x3e8 + o, 0);
            return p;
        }

        /// <summary>One fn_b68d00 plane block (at prim +0x3e8 + <paramref name="b"/>).</summary>
        void E8_AsuraBlock_b68d00(Prim p, uint b, int life, sbyte k, sbyte alt, int offset)
        {
            uint s = p.A + b;
            M.U8(s + 0x3e8, 1);
            M.I32(s + 0x3f4, life);
            M.I16(s + 0x3ea, 0);
            M.I8(s + 0x450, k);
            if (alt == 0)
            {
                M.U32(s + 0x468, 0x41900000);
                M.F32(s + 0x3fc, (float)offset);
                M.U8(s + 0x454, 0);
            }
            else
            {
                M.U32(s + 0x468, 0x41c00000);
                M.F32(s + 0x3fc, (float)offset);
                M.U8(s + 0x454, 3);
            }
            for (uint o = 0x400; o <= 0x424; o += 4) M.U32(s + o, 0);
        }
    }
}
