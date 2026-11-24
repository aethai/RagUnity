// Synced from reference/port/src/RoClassicFx/Rag/effects-7/RagEffect.Effects7.Cast.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-7: the cast circles EF_BEGINSPELL2..7 (EF 54..59, one per element) and the target lock-on EF_LOCKON
    /// (EF 60). The client keeps them for the cast time: see <see cref="SetDuration_5d6f20"/> for how the duration is
    /// set after launch (msgs 0x55 / 0x56 → msg 0x50 → vf18).
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b69b60 (0xb69b60, thiscall (texture, variant), ret 8) — the magic circle: stretches the effect
        /// to at least 70 ticks, launches a ring prim (type 0x17; variant '!' = 0x21 selects type 0x19 with variant 0)
        /// lasting the whole effect, texture <paramref name="texture"/>, palette f_318 = variant, and four concentric
        /// ring particles (+0x3e8 + k·0xb4: active, alpha, span 315°/360°, radius, start angle, tilt, inner radius).
        /// </summary>
        public void Fn_b69b60(FxRuntime rt, string texture, sbyte variant)
        {
            if (f_17c < 0x46) f_17c = 0x46;
            FollowMaster();
            int type = 0x17;
            if (variant == 0x21)
            {
                variant = 0;
                type = 0x19;
            }
            Prim p = LaunchLast(rt, type);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, texture);
            p = m_lastPrim;
            p.f_318 = (float)variant;
            uint a = p.A;

            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.I16(a + 0x3ee, 0x13b);
            M.F32(a + 0x3f0, 25.0f);
            M.U32(a + 0x470, 0);
            M.I16(a + 0x3f8, 0);
            M.I16(a + 0x3ea, 0xb4);
            M.F32(a + 0x468, 4.5f);
            M.I16(a + 0x466, 0x46);
            ClearRingProfileE7(a + 0x3e8);

            M.U8(a + 0x49c, 1);
            M.I32(a + 0x4a8, 0);
            M.I16(a + 0x4a2, 0x13b);
            M.I16(a + 0x49e, 0xb4);
            M.F32(a + 0x51c, 5.0f);
            M.F32(a + 0x4a4, 22.0f);
            M.I16(a + 0x4ac, 0x5a);
            M.I16(a + 0x51a, 0x39);
            M.U32(a + 0x524, 0);
            ClearRingProfileE7(a + 0x49c);

            M.U8(a + 0x550, 1);
            M.I32(a + 0x55c, 0);
            M.I16(a + 0x5ce, 0x2d);
            M.F32(a + 0x5d0, 5.5f);
            M.I16(a + 0x556, 0x13b);
            M.F32(a + 0x558, 19.0f);
            M.I16(a + 0x560, 0xb4);
            M.I16(a + 0x552, 0xb4);
            M.U32(a + 0x5d8, 0);
            ClearRingProfileE7(a + 0x550);

            M.U8(a + 0x604, 1);
            M.I32(a + 0x610, 0);
            M.F32(a + 0x684, 4.0f);
            M.I16(a + 0x682, 0x59);
            M.U32(a + 0x68c, 0);
            M.I16(a + 0x60a, 0x168);
            M.F32(a + 0x60c, 250.0f);
            M.I16(a + 0x614, 0);
            M.I16(a + 0x606, 0x46);
            ClearRingProfileE7(a + 0x604);
        }

        /// <summary>A ring particle's 21 profile floats (+0x14) and 21 done-flags (+0x68) zeroed.</summary>
        void ClearRingProfileE7(uint particle)
        {
            for (uint j = 0; j < 21; j++)
            {
                M.U32(particle + 0x14 + j * 4, 0);
                M.U8(particle + 0x68 + j, 0);
            }
        }

        /// <summary>The inlined cast-circle case: follow the owner; at tick 0 EF_BeginSpell.wav and fn_b69b60.</summary>
        FxCaseResult BeginSpellE7(FxRuntime rt, string texture, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt == 0)
            {
                rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Fn_b69b60(rt, texture, variant);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 54 (0x36, EF_BEGINSPELL2 — water): ring_blue circle, palette 2.</summary>
        [FxEffectProcess(0x36)]
        public FxCaseResult Case_BeginSpell2(FxRuntime rt) => BeginSpellE7(rt, "effect\\ring_blue.tga", 2);

        /// <summary>EF 55 (0x37, EF_BEGINSPELL3 — fire): ring_yellow circle, palette 1.</summary>
        [FxEffectProcess(0x37)]
        public FxCaseResult Case_BeginSpell3(FxRuntime rt) => BeginSpellE7(rt, "effect\\ring_yellow.tga", 1);

        /// <summary>EF 56 (0x38, EF_BEGINSPELL4 — earth): Magic_Green circle (ring type 0x19).</summary>
        [FxEffectProcess(0x38)]
        public FxCaseResult Case_BeginSpell4(FxRuntime rt) => BeginSpellE7(rt, "effect\\Magic_Green.tga", 0x21);

        /// <summary>EF 57 (0x39, EF_BEGINSPELL5 — wind): ring_yellow circle, palette 0.</summary>
        [FxEffectProcess(0x39)]
        public FxCaseResult Case_BeginSpell5(FxRuntime rt) => BeginSpellE7(rt, "effect\\ring_yellow.tga", 0);

        /// <summary>EF 58 (0x3a, EF_BEGINSPELL6 — holy): two white rotating ring sets (fn_b88410, variant 0) at tick 0.</summary>
        [FxEffectProcess(0x3a)]
        public FxCaseResult Case_BeginSpell6(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt == 0)
            {
                rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Fn_b88410(rt, 0x2d, "effect\\ring_white.tga", 0);
                Fn_b88410(rt, 0x19, "effect\\ring_white.tga", 0);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 59 (0x3b, EF_BEGINSPELL7 — shadow): Magic_Violet circle (ring type 0x19).</summary>
        [FxEffectProcess(0x3b)]
        public FxCaseResult Case_BeginSpell7(FxRuntime rt) => BeginSpellE7(rt, "effect\\Magic_Violet.tga", 0x21);

        // ---- EF 60 EF_LOCKON --------------------------------------------------------------------------------

        /// <summary>
        /// CRagEffect::fn_b44580 (0xb44580) — target lock-on: follows the owner; at tick 0 two LockOn128 crosshair
        /// quads (prim 0xf, 0° and 45°, lasting the whole effect, spinning in, colour 250/150/150 fading by 15); every
        /// tick the effect sits 2 above the ground under it (FUN_006c32a0 GetHeight of the world's GAT, x87 fsub).
        /// </summary>
        public void Fn_b44580(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt == 0)
            {
                LockOnQuadE7(rt, 0.0f, true);
                LockOnQuadE7(rt, 45.0f, false);
            }
            float h = rt.GatGetHeight_6c32a0(M.U32(rt.World + 0x30), m_pos_x, m_pos_z);
            m_pos_y = (float)((double)h - 2.0);
        }

        /// <summary>One lock-on quad; the first one allocates its texture array before the colour stores (as the client).</summary>
        void LockOnQuadE7(FxRuntime rt, float heading, bool arrayFirst)
        {
            Prim p = LaunchLast(rt, 0xf);
            p.m_duration = f_17c;
            p.f_1dc |= 0x800;
            p.f_30c = heading;
            p.f_330 = 4.5f;
            p.f_314 = 90.0f;
            p.f_2c4 = 0.0f;
            p.f_2d0 = 0.0f;
            p.f_354 = 30.0f;
            p.f_360 = 30.0f;
            p.f_358 = -1.0f;
            p.f_364 = -1.0f;
            p.f_200 = 0x14;
            p.f_2c8 = 0.0f;
            p.f_2cc = 0.0f;
            if (arrayFirst) p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            p.f_234 = 0x14;
            M.I32(p.A + 0x23c, 0xfa);
            p.f_244 = 0x96;
            p.f_250 = -0xf;
            p.f_240 = 0x96;
            p.f_24c = -0xf;
            if (!arrayFirst) p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            uint tex = rt.GetTexture("effect\\LockOn128.tga");
            M.U32((uint)m_lastPrim.f_144, tex);
        }

        /// <summary>EF 60 (0x3c, EF_LOCKON): fn_b44580.</summary>
        [FxEffectProcess(0x3c)]
        public FxCaseResult Case_LockOn(FxRuntime rt) { Fn_b44580(rt); return FxCaseResult.Continue; }
    }
}
