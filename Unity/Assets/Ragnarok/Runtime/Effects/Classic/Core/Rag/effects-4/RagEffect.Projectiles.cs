// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.Projectiles.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b8adf0 (0xb8adf0) — at tick 0 a prim type 0x56 flying from the effect to the target
        /// position f_180/f_184/f_188 (four staggered blocks); variant 1 uses the four sonic_wave textures,
        /// otherwise purpleslash.tga.
        /// </summary>
        public void Fn_b8adf0(FxRuntime rt, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x56);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            p = m_lastPrim; p.f_204 = variant == 1 ? 4 : 1;
            p = m_lastPrim;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            if (variant == 1)
            {
                string[] names = { "effect\\sonic_wave1.tga", "effect\\sonic_wave2.tga", "effect\\sonic_wave3.tga", "effect\\sonic_wave4.tga" };
                for (uint i = 0; i < 4; i++)
                {
                    uint tex = rt.GetTexture(names[i]);
                    M.U32((uint)m_lastPrim.f_144 + i * 4, tex);
                }
            }
            else
            {
                uint tex = rt.GetTexture("effect\\purpleslash.tga");
                M.U32((uint)m_lastPrim.f_144, tex);
            }
            uint a = m_lastPrim.A;
            M.F32(a + 0x318, 0.0f);
            M.U32(a + 0x388, M.U32(A + 0x180));
            M.U32(a + 0x38c, M.U32(A + 0x184));
            M.U32(a + 0x390, M.U32(A + 0x188));
            M.U32(a + 0x10, M.U32(A + 0x10));
            M.U32(a + 0x14, M.U32(A + 0x14));
            M.U32(a + 0x18, M.U32(A + 0x18));
            for (int i = 0; i < 4; i++)
            {
                a = m_lastPrim.A + (uint)i * 0xb4;
                M.U8(a + 0x3e8, 1);
                M.I16(a + 0x3ea, 0);
                M.I32(a + 0x3f4, i - 0x1c);
                M.F32(a + 0x468, 20.0f);
                M.U32(a + 0x46c, M.U32(A + 0x10));
                M.U32(a + 0x474, M.U32(A + 0x18));
                int r = RandMod(rt, 5);
                M.F32(a + 0x470, (float)r + (m_pos_y - 10.0f));
                M.I16(a + 0x3f8, 0);
                M.F32(a + 0x400, 0.8f);
                M.U8(a + 0x450, 0);
                M.I8(a + 0x451, variant);
            }
        }

        /// <summary>
        /// Case 361 (0x169, vf14) — Soul Destroyer: sound at tick 20, magenta flash (flag 1) for t ≤ 10, and the
        /// purple slash flying to the target.
        /// </summary>
        [FxEffectProcess(0x169)]
        public FxCaseResult Case_SoulDestroyer(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0x14)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\\u00b1\u00e2\u00b0\u00f8\u00c6\u00f7.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if ((uint)t <= 0xa)
            {
                AddMasterFlag(1);
                MasterArgb(rt, -1, 0xff, 0, 0xff);
            }
            Fn_b8adf0(rt, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b89110 (0xb89110) — at tick 0 a texture-less prim type 0x78 flying to the target
        /// position (four blocks starting 3 ticks early), byte parameter <paramref name="variant"/>.
        /// </summary>
        public void Fn_b89110(FxRuntime rt, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchLast(rt, 0x78);
            p.m_duration = f_17c;
            uint a = m_lastPrim.A;
            M.I32(a + 0x204, 0);
            M.F32(m_lastPrim.A + 0x318, 0.0f);
            a = m_lastPrim.A;
            M.U32(a + 0x388, M.U32(A + 0x180));
            M.U32(a + 0x38c, M.U32(A + 0x184));
            M.U32(a + 0x390, M.U32(A + 0x188));
            M.U32(a + 0x10, M.U32(A + 0x10));
            M.U32(a + 0x14, M.U32(A + 0x14));
            M.U32(a + 0x18, M.U32(A + 0x18));
            for (int i = 0; i < 4; i++)
            {
                a = m_lastPrim.A + (uint)i * 0xb4;
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, i - 3);
                M.I16(a + 0x3ea, 0);
                M.U32(a + 0x46c, M.U32(A + 0x10));
                M.U32(a + 0x474, M.U32(A + 0x18));
                int r = RandMod(rt, 5);
                M.F32(a + 0x470, (float)r + (m_pos_y - 10.0f));
                M.I16(a + 0x3f8, 0);
                M.F32(a + 0x400, 0.8f);
                M.I8(a + 0x450, variant);
            }
        }

        /// <summary>Case 555 (0x22b, vf15) — Estun: sound at tick 0, energy wave to the target.</summary>
        [FxEffectProcess(0x22b)]
        public FxCaseResult Case_Estun(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\T_\u00bf\u00a1\u00b3\u00ca\u00c1\u00f6\u00b9\u00e6\u00c3\u00e2.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            Fn_b89110(rt, 1);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b8a880 (0xb8a880) — Shield Boomerang: sound and, at tick 0, a prim type 0x2a flying
        /// from the owner to the target position with a vertical slope matching the distance.
        /// </summary>
        public void Fn_b8a880(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\cru_shield boomerang.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchLast(rt, 0x2a);
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetPrimTexture(rt, m_lastPrim, texture);
            uint a = m_lastPrim.A;
            M.F32(a + 0x318, 0.0f);
            M.U32(a + 0x388, M.U32(A + 0x180));
            M.U32(a + 0x38c, M.U32(A + 0x184));
            M.U32(a + 0x390, M.U32(A + 0x188));
            float dx = f_180 - m_pos_x;
            float dz = f_188 - m_pos_z;
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.F32(a + 0x3f0, (float)System.Math.Sqrt((double)(dz * dz + dx * dx)));
            M.F32(a + 0x468, 7.0f);
            M.U32(a + 0x41c, 0);
            M.I16(a + 0x3ea, 0xff);
            M.U32(a + 0x46c, M.U32(A + 0x10));
            M.U32(a + 0x474, M.U32(A + 0x18));
            M.F32(a + 0x470, m_pos_y - 8.0f);
            M.I16(a + 0x3f8, 0);
            M.F32(a + 0x400, 2.0f);
            M.F32(a + 0x404, M.F32(a + 0x400) / M.F32(a + 0x3f0) * (f_184 - m_pos_y));
            M.I16(a + 0x3ee, 0);
            M.U8(a + 0x450, 0);
            M.U8(a + 0x451, 0);
            M.U8(a + 0x452, 0);
            M.U8(a + 0x49c, 1);
            M.I16(a + 0x49e, 0);
            M.U8(a + 0x550, 1);
            M.I16(a + 0x552, 0);
            M.U8(a + 0x604, 1);
            M.I16(a + 0x606, 0);
        }

        /// <summary>Case 249 (0xf9) — Shield Boomerang.</summary>
        [FxEffectProcess(0xf9)]
        public FxCaseResult Case_ShieldBoomerang(FxRuntime rt)
        {
            Fn_b8a880(rt, "effect\\shield_boomerang.bmp");
            return FxCaseResult.Continue;
        }
    }
}
