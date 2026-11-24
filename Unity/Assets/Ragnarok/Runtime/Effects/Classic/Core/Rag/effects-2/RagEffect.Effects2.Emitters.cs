// Synced from reference/port/src/RoClassicFx/Rag/effects-2/RagEffect.Effects2.Emitters.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-2 handlers that set up "sub-emitter" prims: four 0xb4-byte blocks from prim +0x3e8
    /// (+0 active byte, +2/+4/+6 shorts, +8 float, +0xc int, +0x10 short, +0x14.. floats, +0x68.. bytes,
    /// +0x7e short, +0x80.. floats). Bard/dancer songs, throws, spirit spheres, arrows.
    /// </summary>
    public readonly partial struct RagEffect
    {
        const uint Blk = 0xb4;

        /// <summary>CRagEffect::fn_b6ad90 (0xb6ad90) — song ring (prim 0x32): Down Tempo, Acoustic Rhythm.</summary>
        public void Fn_b6ad90(FxRuntime rt, string texture, sbyte size)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0x32);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, texture);
            uint a = p.A;
            p.f_318 = (float)(int)size;
            M.U8(a + 0x3e8, 0);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.I16(a + 0x3ee, 0x13b);
            M.F32(a + 0x3f0, (float)E2_Rand(rt, 10) + 25.0f);
            M.I16(a + 0x3f8, (short)E2_Rand(rt, 0x168));
            M.I16(a + 0x3ea, (short)(0x32 - (short)E2_Rand(rt, 10)));
            M.I16(a + 0x466, 0x5a);
            M.I16(a + 0x3ec, (short)E2_Rand(rt, 0x168));
            M.F32(a + 0x470, 0f);
            E2_FillSubEmitter(a, 0, 0);
        }

        /// <summary>
        /// CRagEffect::fn_b6b460 (0xb6b460) — floating song symbols (prim 0x2f). Variant selects the texture
        /// (2: random melody, 5: random gemstone, 8: random spell rune), speed and motion pattern.
        /// </summary>
        public void Fn_b6b460(FxRuntime rt, string texture, sbyte variant)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0x2f);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            if (variant == 5)
            {
                int r = E2_Rand(rt, 3);
                texture = r == 0 ? Effects2Strings.Tex_ffb85c : r == 1 ? Effects2Strings.Tex_ffb874 : Effects2Strings.Tex_ffb88c;
            }
            else if (variant == 2)
            {
                int r = rt.Rand.Next() % 2;
                texture = (r >= 0 ? r : 0) == 0 ? "effect\\melody_a.bmp" : "effect\\melody_b.bmp";
            }
            else if (variant == 8)
            {
                int r = rt.Rand.Next() % 8;
                if (r < 0) r = 0;
                texture = r switch
                {
                    0 => "effect\\spell_01.bmp", 1 => "effect\\spell_02.bmp", 2 => "effect\\spell_03.bmp", 3 => "effect\\spell_04.bmp",
                    4 => "effect\\spell_05.bmp", 5 => "effect\\spell_06.bmp", 6 => "effect\\spell_07.bmp", _ => "effect\\spell_08.bmp",
                };
            }
            E2_SetTexture(rt, p, 0, texture);
            uint a = p.A;
            p.f_318 = 0f;
            M.U8(a + 0x3e8, 0);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            float speed = variant == 1 ? 5.0f : variant == 4 ? 15.0f : variant == 6 ? 8.0f : variant == 9 ? 1.5f : 3.0f;
            M.F32(a + 0x468, speed);
            M.F32(a + 0x41c, 0f);
            M.I16(a + 0x3ea, 200);
            M.F32(a + 0x46c, m_pos_x);
            M.F32(a + 0x474, m_pos_z);
            M.F32(a + 0x488, m_pos_y - 6.0f);
            M.I16(a + 0x3f8, (short)E2_Rand(rt, 0x168));
            M.I16(a + 0x3ee, 0x2d);
            M.U8(a + 0x450, 0);
            M.U8(a + 0x452, 2);
            M.F32(a + 0x418, 0f);
            switch (variant)
            {
                case 4: M.F32(a + 0x420, 10.0f); break;
                case 2:
                    M.U8(a + 0x49c, 1); M.I16(a + 0x49e, 0);
                    M.U8(a + 0x550, 1); M.I16(a + 0x552, 0);
                    M.U8(a + 0x604, 1); M.I16(a + 0x606, 0);
                    break;
                case 3: M.U8(a + 0x452, 3); break;
                case 5: case 6: M.U8(a + 0x452, 4); break;
                case 7: M.U8(a + 0x452, 7); break;
                case 9: M.U8(a + 0x452, 9); M.U8(a + 0x453, 6); break;
            }
        }

        /// <summary>CRagEffect::fn_b6b8b0 (0xb6b8b0) — rising song lines (prim 0x31): Classical Pluck.</summary>
        public void Fn_b6b8b0(FxRuntime rt, string texture, sbyte variant)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0x31);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, texture);
            uint a = p.A;
            M.U8(a + 0x3e8, 0);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
            int count = (variant != 1 ? 1 : 0) + 1;
            for (uint i = 0; count != 0; i += Blk, count--)
            {
                M.U8(a + 0x3e8 + i, 1);
                M.I32(a + 0x3f4 + i, E2_Rand(rt, 0xb));
                if (variant == 0) M.F32(a + 0x468 + i, (float)E2_Rand(rt, 6) + 5.0f);
                else if (variant == 1) M.F32(a + 0x468 + i, (float)E2_Rand(rt, 0x14) * 0.1f + 3.0f);
                M.I32(a + 0x41c + i, 0);
                M.I16(a + 0x3ea + i, 0);
                M.F32(a + 0x46c + i, m_pos_x);
                M.F32(a + 0x474 + i, m_pos_z);
                if (variant == 0) M.F32(a + 0x488 + i, m_pos_y);
                else if (variant == 1) M.F32(a + 0x488 + i, m_pos_y - 5.0f);
                M.I16(a + 0x3ee + i, 0x2d);
                M.U8(a + 0x450 + i, 0);
                M.U8(a + 0x452 + i, 2);
                M.I32(a + 0x418 + i, 0);
                M.I8(a + 0x451 + i, variant);
            }
        }

        /// <summary>CRagEffect::fn_b6bce0 (0xb6bce0) — dancing ground marks (prim 0x30): Impressive Riff, Slow Grace, Gypsy's Kiss.</summary>
        public void Fn_b6bce0(FxRuntime rt, string texture, sbyte variant)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0x30);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, texture);
            uint a = p.A;
            p.f_318 = 0f;
            int k = 0;
            for (uint i = 0; i < 0x2d0; i += Blk, k++)
            {
                if (variant == 0)
                {
                    M.U8(a + 0x3e8 + i, 1);
                    M.I32(a + 0x3f4 + i, 0);
                    M.I16(a + 0x3ee + i, (short)E2_Rand(rt, 0x168));
                    M.F32(a + 0x484 + i, (float)(E2_Rand(rt, 0x28) - 0x14));
                    M.F32(a + 0x48c + i, (float)(E2_Rand(rt, 0x28) - 0x14));
                    M.U32(a + 0x46c + i, 0);
                    M.U32(a + 0x474 + i, 0);
                    M.I16(a + 0x3ea + i, 0xf);
                    continue;
                }
                short life;
                if (variant == 1 || variant == 2)
                {
                    M.U8(a + 0x3e8 + i, 1);
                    M.I32(a + 0x3f4 + i, 0);
                    M.I16(a + 0x3ee + i, (short)E2_Rand(rt, 0x168));
                    if (k < 2)
                    {
                        M.F32(a + 0x484 + i, (float)E2_Rand(rt, 7) - 3.0f);
                        M.U32(a + 0x48c + i, 0xc0400000);
                        M.U32(a + 0x46c + i, M.U32(a + 0x484 + i) ^ 0x80000000);
                        M.U32(a + 0x474 + i, 0x40400000);
                    }
                    else
                    {
                        M.U32(a + 0x484 + i, 0xc0400000);
                        M.F32(a + 0x48c + i, (float)E2_Rand(rt, 7) - 3.0f);
                        M.U32(a + 0x46c + i, 0x40400000);
                        M.U32(a + 0x474 + i, M.U32(a + 0x48c + i) ^ 0x80000000);
                    }
                    life = 0x32;
                }
                else if (variant == 3 || variant == 4)
                {
                    M.U8(a + 0x3e8 + i, (byte)(k < 2 ? 1 : 0));
                    M.I32(a + 0x3f4 + i, 0);
                    M.I16(a + 0x3ee + i, (short)E2_Rand(rt, 0x168));
                    if (k < 1)
                    {
                        M.U32(a + 0x484 + i, 0x40c00000);
                        M.U32(a + 0x48c + i, 0);
                        M.U32(a + 0x46c + i, 0xc0c00000);
                        M.U32(a + 0x474 + i, 0);
                    }
                    else
                    {
                        M.U32(a + 0x484 + i, 0);
                        M.U32(a + 0x48c + i, 0xc0c00000);
                        M.U32(a + 0x46c + i, 0);
                        M.U32(a + 0x474 + i, 0x40c00000);
                    }
                    life = 100;
                }
                else continue;
                M.I16(a + 0x3ea + i, life);
                M.F32(a + 0x3fc + i, (float)(int)variant);
            }
        }

        /// <summary>CRagEffect::fn_b76990 (0xb76990) — four concentric rings (prim 0x6c): Warmth of the Sun/Moon/Stars.</summary>
        public void Fn_b76990(FxRuntime rt, string texture, sbyte size, sbyte variant)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0x6c);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, texture);
            uint a = p.A;
            p.f_318 = (float)(int)size;
            for (int k = 0; k < 4; k++)
            {
                uint i = (uint)k * Blk;
                M.U8(a + 0x3e8 + i, 1);
                M.I32(a + 0x3f4 + i, 0);
                M.I16(a + 0x3ee + i, 0x13b);
                M.I16(a + 0x3ea + i, 0);
                M.F32(a + 0x470 + i, 0f);
                if (variant == 0 || variant == 1)
                {
                    float speed, radius; short life, angle;
                    if (variant == 0)
                    {
                        switch (k)
                        {
                            case 0: speed = 4.5f; life = 0x55; radius = 27.0f; angle = 0; break;
                            case 1: speed = 5.0f; life = 0x50; radius = 28.0f; angle = 0x5a; break;
                            case 2: speed = 5.5f; life = 0x46; radius = 29.0f; angle = 0xb4; break;
                            default: speed = 5.0f; life = 0x4b; radius = 30.0f; angle = 0x10e; break;
                        }
                    }
                    else
                    {
                        switch (k)
                        {
                            case 0: speed = 5.5f; life = 0x32; radius = 30.0f; angle = 0x5a; break;
                            case 1: speed = 6.0f; life = 0x37; radius = 29.0f; angle = 0xb4; break;
                            case 2: speed = 6.5f; life = 0x41; radius = 28.0f; angle = 0x10e; break;
                            default: speed = 6.0f; life = 0x3c; radius = 27.0f; angle = 0; break;
                        }
                    }
                    M.F32(a + 0x468 + i, speed);
                    M.I16(a + 0x466 + i, life);
                    M.F32(a + 0x3f0 + i, radius);
                    M.I16(a + 0x3f8 + i, angle);
                }
                M.F32(a + 0x478 + i, (float)(int)M.I16(a + 0x466 + i));
                M.U32(a + 0x47c + i, M.U32(a + 0x3f0 + i));
                E2_FillSubEmitter(a, k, 0);
            }
        }

        /// <summary>CRagEffect::fn_b7a290 (0xb7a290) — Preserve: tick 0 sound + a 2-texture aura (prim 0x3d).</summary>
        public void Fn_b7a290(FxRuntime rt, string texture)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            E2_Sound(rt, "effect\\black_maximize_power_sword_bic.wav");
            Prim p = E2_Launch(rt, 0x3d);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 2;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, texture);
            E2_SetTexture(rt, p, 1, "effect\\a01.bmp");
            uint a = p.A;
            p.f_318 = 11.0f;
            M.U8(a + 0x604, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.F32(a + 0x3f0, 10.0f);
            M.I16(a + 0x3ea, 0);
            M.F32(a + 0x468, 1.0f);
            M.F32(a + 0x3fc, 0f);
            M.U8(a + 0x450, 1);
        }

        /// <summary>CRagEffect::fn_b89610 (0xb89610) — Esma wind strands (prim 0x7a).</summary>
        public void Fn_b89610(FxRuntime rt, string texture, sbyte variant)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0x7a);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, texture);
            uint a = p.A;
            p.f_318 = 0f;
            p.f_388 = f_180;
            p.f_38c = f_184;
            p.f_390 = f_188;
            p.m_pos_x = m_pos_x;
            p.m_pos_y = m_pos_y;
            p.m_pos_z = m_pos_z;
            short k = 0;
            for (uint i = 0; i < 0x2d0; i += Blk, k++)
            {
                M.U8(a + 0x3e8 + i, 1);
                M.I32(a + 0x3f4 + i, 0);
                if (variant == 4 || variant == 5) { M.U32(a + 0x468 + i, 0x41c80000); M.U8(a + 0x453 + i, 3); }
                else if (variant < 2) { M.U32(a + 0x468 + i, 0x41c80000); M.U8(a + 0x453 + i, 0); }
                else { M.U32(a + 0x468 + i, 0x40c00000); M.U8(a + 0x453 + i, 2); }
                M.I16(a + 0x3ea + i, 0);
                M.F32(a + 0x46c + i, m_pos_x);
                M.F32(a + 0x474 + i, m_pos_z);
                M.F32(a + 0x470 + i, m_pos_y - 10.0f);
                M.I16(a + 0x3f8 + i, 0);
                M.I16(a + 0x466 + i, k);
                M.U32(a + 0x400 + i, 0x40000000);
                M.U8(a + 0x450 + i, 0);
                M.U8(a + 0x451 + i, 0);
                if (variant == 0 || variant == 2 || variant == 4) M.U8(a + 0x452 + i, 0);
                else if (variant == 1 || variant == 3 || variant == 5) M.U8(a + 0x452 + i, 1);
                M.U8(a + 0x454 + i, (byte)(variant == 2 || variant == 3 ? 1 : 0));
            }
        }

        /// <summary>CRagEffect::fn_b8baa0 (0xb8baa0) — Sheltering Bliss fire ivy (prim 0x4d), angle offset per call.</summary>
        public void Fn_b8baa0(FxRuntime rt, string texture, short angle, sbyte big)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0x4d);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, texture);
            uint a = p.A;
            p.f_318 = big == 1 ? 10.0f : 4.0f;
            for (uint i = 0; i < 0x2d0; i += Blk) M.U8(a + 0x3e8 + i, 0);
            short k = 0;
            for (uint i = 0; i < 0x2d0; i += Blk)
            {
                M.U8(a + 0x3e8 + i, 1);
                M.I32(a + 0x3f4 + i, 0);
                M.F32(a + 0x3f0 + i, (float)E2_Rand(rt, 0xb) * 0.2f + 4.0f);
                M.U32(a + 0x468 + i, 0x41600000);
                M.U32(a + 0x3fc + i, 0xc1600000);
                M.I16(a + 0x3f8 + i, (short)E2_Rand(rt, 0x168));
                M.I16(a + 0x466 + i, (short)(k * 0x2d + angle));
                M.I16(a + 0x3ea + i, 100);
                M.I16(a + 0x3ee + i, (short)((short)E2_Rand(rt, 0x5b) + 0xb4));
                int r = rt.Rand.Next() % 4;
                k++;
                M.F32(a + 0x400 + i, (float)((r >= 0 ? r : 0) + 3));
            }
        }

        /// <summary>CRagEffect::fn_b8c810 (0xb8c810) — Throw Spirit Sphere: tick 0 sound + a flying "blue_ivy" orb (prim 0x2a) towards the target.</summary>
        public void Fn_b8c810(FxRuntime rt)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            E2_Sound(rt, Effects2Strings.Wav_ffc09c);
            Prim p = E2_Launch(rt, 0x2a);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, "effect\\blue_ivy.bmp");
            uint a = p.A;
            p.f_318 = 0f;
            p.f_388 = f_180;
            p.f_38c = f_184;
            p.f_390 = f_188;
            float dx = f_180 - m_pos_x;
            float dz = f_188 - m_pos_z;
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, -0xf);
            M.F32(a + 0x3f0, FxRuntime.SqrtF(dz * dz + dx * dx));
            M.F32(a + 0x468, (float)f_174 * 0.4f + 2.0f);
            M.F32(a + 0x41c, 0f);
            M.I16(a + 0x3ea, 5);
            M.F32(a + 0x46c, m_pos_x);
            M.F32(a + 0x474, m_pos_z);
            M.F32(a + 0x470, m_pos_y - 8.0f);
            M.I16(a + 0x3f8, 0);
            M.F32(a + 0x400, 2.0f);
            M.F32(a + 0x404, (M.F32(a + 0x400) / M.F32(a + 0x3f0)) * (f_184 - m_pos_y));
            M.I16(a + 0x3ee, 0);
            M.U8(a + 0x450, 0);
            M.U8(a + 0x451, 0);
            M.U8(a + 0x452, 1);
            M.F32(a + 0x418, 0f);
            M.U8(a + 0x49c, 1); M.I16(a + 0x49e, 0);
            M.U8(a + 0x550, 1); M.I16(a + 0x552, 0);
            M.U8(a + 0x604, 1); M.I16(a + 0x606, 0);
        }

        /// <summary>CRagEffect::fn_b8cb60 (0xb8cb60) — spirit burst (prim 0x2c), called 20× per tick by Raging Quadruple Blow.</summary>
        public void Fn_b8cb60(FxRuntime rt, string texture, sbyte kind)
        {
            E2_FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = E2_Launch(rt, 0x2c);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, texture);
            uint a = p.A;
            m_pos_y = m_pos_y - 9.0f;
            for (uint i = 0; i < 0x2d0; i += Blk)
            {
                M.U8(a + 0x3e8 + i, 1);
                M.I32(a + 0x3f4 + i, -10 - E2_Rand(rt, 0x19));
                M.U32(a + 0x468 + i, 0xc1200000);
                M.U32(a + 0x3fc + i, 0x40400000);
                M.I16(a + 0x466 + i, (short)E2_Rand(rt, 0x168));
                M.I16(a + 0x3ee + i, (short)E2_Rand(rt, 0x168));
                M.U32(a + 0x400 + i, 0x3f333333);
                M.I16(a + 0x3ea + i, 0);
                M.I8(a + 0x450 + i, kind);
                M.U8(a + 0x451 + i, 0x18);
            }
        }

        /// <summary>CRagEffect::fn_b8d6e0 (0xb8d6e0) — Focused Arrow Strike trails (prim 0x45) during ticks 0..3.</summary>
        public void Fn_b8d6e0(FxRuntime rt)
        {
            E2_FollowMaster();
            if (m_stateCnt >= 4) return;
            Prim p = E2_Launch(rt, 0x45);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, "effect\\alpha_center.tga");
            uint a = p.A;
            p.f_318 = 0f;
            p.f_388 = f_180;
            p.f_38c = f_184;
            p.f_390 = f_188;
            float dz = f_188 - m_pos_z;
            float dx = f_180 - m_pos_x;
            float dist = FxRuntime.SqrtF(dz * dz + dx * dx);
            for (uint i = 0; i < 0x2d0; i += Blk)
            {
                M.U8(a + 0x3e8 + i, 1);
                M.I32(a + 0x3f4 + i, -0x2d - E2_Rand(rt, 0x14));
                Prim.DirToAngle_ad5710(M, dx, dz, a + 0x3f8 + i);
                M.I16(a + 0x3ea + i, 0);
                M.F32(a + 0x46c + i, ((float)E2_Rand(rt, 100) * 0.08f - 4.0f) + m_pos_x);
                M.F32(a + 0x474 + i, ((float)E2_Rand(rt, 100) * 0.08f - 4.0f) + m_pos_z);
                M.F32(a + 0x470 + i, m_pos_y);
                M.F32(a + 0x400 + i, (float)E2_Rand(rt, 100) * 0.15f);
                short ang = M.I16(a + 0x3f8 + i);
                M.F32(a + 0x46c + i, M.F32(a + 0x46c + i) - FxMath.GetCos(ang) * M.F32(a + 0x400 + i));
                ang = M.I16(a + 0x3f8 + i);
                M.F32(a + 0x474 + i, M.F32(a + 0x474 + i) - FxMath.GetSin(ang) * M.F32(a + 0x400 + i));
                M.U32(a + 0x478 + i, M.U32(a + 0x46c + i));
                M.U32(a + 0x480 + i, M.U32(a + 0x474 + i));
                M.F32(a + 0x47c + i, m_pos_y);
                M.U32(a + 0x3f0 + i, 0x3d4ccccd);
                M.F32(a + 0x3fc + i, (float)E2_Rand(rt, 100) * 0.04f + 8.0f);
                M.F32(a + 0x468 + i, (float)E2_Rand(rt, 100) * 0.6f + dist);
                M.U32(a + 0x40c + i, 0x40000000);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b8e3d0 (0xb8e3d0) — thrown item (prim 0x35) flying to the target (f_180..f_188):
        /// Acid Terror, Aid Potion, Stone Fling, Venom Knife, shuriken/kunai/huuma/zeny throws. Variant 10/11
        /// delay the launch to tick 5/10.
        /// </summary>
        public void Fn_b8e3d0(FxRuntime rt, string texture, sbyte variant)
        {
            E2_FollowMaster();
            int launchTick = variant == 10 ? 5 : variant == 11 ? 10 : 0;
            if (m_stateCnt != launchTick) return;
            Prim p = E2_Launch(rt, 0x35);
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            E2_AllocTextures(rt, p);
            E2_SetTexture(rt, p, 0, texture);
            uint a = p.A;
            p.f_318 = 0f;
            p.f_388 = f_180;
            p.f_38c = f_184;
            p.f_390 = f_188;
            float dz = f_188 - m_pos_z;
            float dx = f_180 - m_pos_x;
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.F32(a + 0x3f0, FxRuntime.SqrtF(dz * dz + dx * dx));
            M.F32(a + 0x468, variant == 2 ? 2.0f : variant == 3 ? 4.0f : 3.0f);
            M.F32(a + 0x41c, 0f);
            M.I16(a + 0x3ea, 0xff);
            M.F32(a + 0x46c, m_pos_x);
            M.F32(a + 0x474, m_pos_z);
            M.F32(a + 0x470, 0f);
            M.F32(a + 0x488, m_pos_y - 8.0f);
            M.I16(a + 0x3f8, 0);
            float step = variant == 2 ? 2.0f : variant == 3 ? 3.0f : M.F32(a + 0x3f0) / 25.0f;
            M.F32(a + 0x400, step);
            M.F32(a + 0x404, (M.F32(a + 0x400) / M.F32(a + 0x3f0)) * (f_184 - m_pos_y));
            if (variant == 3)
            {
                M.I16(a + 0x3ee, 0x14a);
                M.U8(a + 0x450, 0);
                M.F32(a + 0x418, 0f);
                M.U8(a + 0x452, 0xf);
            }
            else
            {
                M.I16(a + 0x3ee, 0);
                M.U8(a + 0x450, 0);
                M.F32(a + 0x418, 0f);
                if (variant == 0) M.U8(a + 0x452, 8);
                else if (variant == 1) { M.U8(a + 0x452, 9); M.U8(a + 0x453, (byte)f_174); }
                else if (variant < 10) { if (variant == 2) M.U8(a + 0x452, 0); }
                else { M.U8(a + 0x452, 9); M.U8(a + 0x453, 4); }
            }
            for (int n = 0; n < 100; n++)
            {
                if (M.F32(a + 0x3f0) < (float)n * M.F32(a + 0x400))
                {
                    M.U8(a + 0x451, (byte)n);
                    break;
                }
            }
            M.U8(a + 0x49c, 1); M.I16(a + 0x49e, 0);
            M.U8(a + 0x550, 1); M.I16(a + 0x552, 0);
            M.U8(a + 0x604, 1); M.I16(a + 0x606, 0);
        }
    }
}
