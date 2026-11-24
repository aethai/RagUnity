// Synced from reference/port/src/RoClassicFx/Rag/effects-2/RagEffect.Effects2.SpiritSphere.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b7ad40 (0xb7ad40) — spirit-sphere style aura: tick 0 builds a 4-ring prim 0x3c
        /// (size 4 for variant 2, else 9; faster rings for variant != 0; "_heal_effect.wav" when variant &lt; 2);
        /// variants 0/1 add "pok" bursts (prim 0x3b) every 6 ticks before 51; variant 1 adds an "alpha_down.tga"
        /// ring at tick 1 and extra bursts at ticks ≡ 2 (mod 9). Absorb Spirit Sphere uses variant 2.
        /// Returns the value the client leaves in EAX (callers ignore it).
        /// </summary>
        public void Fn_b7ad40(FxRuntime rt, string texture, byte variant)
        {
            E2_FollowMaster();
            int tick = m_stateCnt;
            if (tick == 0)
            {
                Prim p = E2_Launch(rt, 0x3c);
                p.f_1c4 = 5;
                p.m_duration = f_17c;
                E2_AllocTextures(rt, p);
                E2_SetTexture(rt, p, 0, texture);
                uint a = p.A;
                p.f_318 = variant == 2 ? 4.0f : 9.0f;
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, 0);
                M.I16(a + 0x3ee, 0x168);
                M.F32(a + 0x3f0, 60.0f);
                M.I16(a + 0x3f8, 0);
                M.I16(a + 0x3ea, 0);
                M.F32(a + 0x468, 4.6f);
                M.I16(a + 0x466, 0x5a);
                M.F32(a + 0x470, 0f);
                M.I16(a + 0x3ec, (short)(variant != 0 ? 0x28 : 0x3c));
                E2_FillSubEmitter(a, 0, 0);

                M.U8(a + 0x49c, 1);
                M.I32(a + 0x4a8, 0);
                M.I16(a + 0x4a2, 0x168);
                M.F32(a + 0x4a4, 60.0f);
                M.I16(a + 0x4ac, 0xb4);
                M.I16(a + 0x49e, 0);
                M.F32(a + 0x51c, 5.0f);
                M.I16(a + 0x51a, 0x5a);
                M.U32(a + 0x524, 0);
                M.I16(a + 0x4a0, (short)(variant != 0 ? 0x28 : 0x3c));
                E2_FillSubEmitter(a, 1, 0);

                M.U8(a + 0x550, 1);
                M.I32(a + 0x55c, 0);
                M.I16(a + 0x556, 0x168);
                M.F32(a + 0x558, variant != 0 ? 18.0f : 12.0f);
                M.I16(a + 0x560, (short)E2_Rand(rt, 0x168));
                M.I16(a + 0x552, 0);
                M.F32(a + 0x5d0, 5.0f);
                M.I16(a + 0x5ce, 0x32);
                M.U32(a + 0x5d8, 0);
                M.I16(a + 0x554, (short)(variant != 0 ? 0x28 : 0x3c));
                E2_FillSubEmitter(a, 2, 0);

                M.U8(a + 0x604, 1);
                M.I32(a + 0x610, 0);
                M.I16(a + 0x60a, 0x168);
                M.F32(a + 0x60c, variant == 0 ? 12.0f : 18.0f);
                M.I16(a + 0x614, (short)E2_Rand(rt, 0x168));
                M.I16(a + 0x606, 0);
                M.F32(a + 0x684, 5.2f);
                M.I16(a + 0x682, 0x30);
                M.U32(a + 0x68c, 0);
                M.I16(a + 0x608, (short)(variant != 0 ? 0x28 : 0x3c));
                E2_FillSubEmitter(a, 3, 0);

                if ((sbyte)variant < 2) E2_Sound(rt, "_heal_effect.wav");
            }
            else if (tick < 0x33 && tick % 6 == 0 && (sbyte)variant < 2)
            {
                E2_PokBurst(rt, variant == 0 ? 0.4f : 0.6f, 2.0f);
            }
            if (variant == 1)
            {
                tick = m_stateCnt;
                if (tick == 1)
                {
                    Prim p = E2_Launch(rt, 0x3c);
                    p.f_1c4 = 5;
                    p.m_duration = f_17c;
                    E2_AllocTextures(rt, p);
                    E2_SetTexture(rt, p, 0, "effect\\alpha_down.tga");
                    uint a = p.A;
                    p.f_318 = 9.0f;
                    M.U8(a + 0x3e8, 1);
                    M.I32(a + 0x3f4, 0);
                    M.I16(a + 0x3ee, 0x168);
                    M.F32(a + 0x3f0, 90.0f);
                    M.I16(a + 0x3f8, 0);
                    M.I16(a + 0x3ea, 0);
                    M.F32(a + 0x468, 4.2f);
                    M.I16(a + 0x466, 0x5a);
                    M.I16(a + 0x3ec, 0x28);
                    M.F32(a + 0x470, 0f);
                    E2_FillSubEmitter(a, 0, 0);

                    M.U8(a + 0x49c, 1);
                    M.I32(a + 0x4a8, 0);
                    M.I16(a + 0x4a2, 0x168);
                    M.F32(a + 0x4a4, 90.0f);
                    M.I16(a + 0x4ac, 0x5a);
                    M.I16(a + 0x49e, 0);
                    M.F32(a + 0x51c, 4.3f);
                    M.I16(a + 0x51a, 0x5a);
                    M.I16(a + 0x4a0, 0x28);
                    M.U32(a + 0x524, 0);
                    E2_FillSubEmitter(a, 1, 0);

                    M.U8(a + 0x550, 1);
                    M.I32(a + 0x55c, 0);
                    M.I16(a + 0x556, 0x168);
                    M.F32(a + 0x558, 10.0f);
                    M.I16(a + 0x560, (short)E2_Rand(rt, 0x168));
                    M.I16(a + 0x552, 0);
                    M.F32(a + 0x5d0, 5.3f);
                    M.I16(a + 0x5ce, 0x2e);
                    M.I16(a + 0x554, 0x1e);
                    M.U32(a + 0x5d8, 0);
                    E2_FillSubEmitter(a, 2, 0);

                    M.U8(a + 0x604, 1);
                    M.I32(a + 0x610, 0);
                    M.I16(a + 0x60a, 0x168);
                    M.F32(a + 0x60c, 10.0f);
                    M.I16(a + 0x614, (short)E2_Rand(rt, 0x168));
                    M.I16(a + 0x606, 0);
                    M.F32(a + 0x684, 5.4f);
                    M.I16(a + 0x682, 0x2c);
                    M.I16(a + 0x608, 0x1e);
                    M.U32(a + 0x68c, 0);
                    E2_FillSubEmitter(a, 3, 0);
                    tick = m_stateCnt;
                }
                if (tick < 0x33 && tick % 9 == 2) E2_PokBurst(rt, 0.4f, 2.4f);
            }
        }

        /// <summary>The "pok1/pok3" burst of fn_b7ad40: prim 0x3b with four random sub-emitters.</summary>
        void E2_PokBurst(FxRuntime rt, float riseBase, float speedBase)
        {
            Prim p = E2_Launch(rt, 0x3b);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 2;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, "effect\\pok1.tga");
            E2_SetTexture(rt, p, 1, "effect\\pok3.tga");
            uint a = p.A;
            p.f_318 = 0f;
            p.m_pos_x = m_pos_x;
            p.m_pos_y = m_pos_y;
            p.m_pos_z = m_pos_z;
            for (uint i = 0; i < 0x2d0; i += Blk)
            {
                M.U8(a + 0x3e8 + i, 1);
                M.I32(a + 0x3f4 + i, 0);
                M.F32(a + 0x468 + i, (float)E2_Rand(rt, 10) * 0.1f + speedBase);
                M.U32(a + 0x41c + i, 0);
                M.I16(a + 0x3ea + i, 0);
                M.F32(a + 0x46c + i, ((float)E2_Rand(rt, 0xd) + m_pos_x) - 6.0f);
                M.F32(a + 0x474 + i, ((float)E2_Rand(rt, 0xd) + m_pos_z) - 6.0f);
                M.F32(a + 0x470 + i, m_pos_y);
                M.I16(a + 0x3f8 + i, 0);
                M.U32(a + 0x400 + i, 0);
                M.F32(a + 0x404 + i, (float)E2_Rand(rt, 0x28) * 0.01f + riseBase);
                M.I16(a + 0x3ee + i, 0);
                M.U8(a + 0x450 + i, 0);
                M.U8(a + 0x451 + i, 1);
                M.U8(a + 0x452 + i, 0xb);
                M.U32(a + 0x418 + i, 0);
            }
        }
    }
}
