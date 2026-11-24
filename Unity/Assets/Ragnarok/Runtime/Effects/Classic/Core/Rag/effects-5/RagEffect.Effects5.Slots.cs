// Synced from reference/port/src/RoClassicFx/Rag/effects-5/RagEffect.Effects5.Slots.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {

        /// <summary>rand() % 2^k computed with the compiler's AND/fix-up sequence (non-negative for rand()).</summary>
        static int RandPow2(FxRuntime rt, int n) { int r = rt.Rand.Next() % n; return r < 0 ? 0 : r; }

        /// <summary>Variant of <see cref="LaunchSlotPrim"/> with a different f_1c4 value and/or f_204 = 1 before the allocation.</summary>
        Prim LaunchSlotPrimEx(FxRuntime rt, int type, uint flags1c4, bool oneTexture, string texture)
        {
            Prim p = LaunchEffectPrim(rt, type, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = flags1c4;
            p.m_duration = f_17c;
            if (oneTexture) p.f_204 = 1;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            uint tex = rt.GetTexture(texture);
            M.U32((uint)p.f_144, tex);
            return p;
        }

        /// <summary>
        /// CRagEffect::fn_b6d260 (0xb6d260): follows the owner; at tick 0 launches prim 0x38 (4 speed lines towards
        /// the target f_180/f_188). <paramref name="kind"/> % 10 picks the side (0 left, 1 right), the decade the
        /// palette (&lt;10 line3, &lt;20 line3y small, else line3y large).
        /// </summary>
        public void Fn_b6d260(FxRuntime rt, short kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchSlotPrimEx(rt, 0x38, 0x201, false, kind < 10 ? "effect\\line3.tga" : "effect\\line3y.tga");
            M.U8(p.A + 0x3e8, 0);
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            int side = kind % 10;
            for (uint k = 0; k < 4; k++)
            {
                uint s = p.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, 0);
                M.F32(s + 0x468, 100.0f);
                if (side == 0) M.F32(s + 0x3fc, -15.0f - (float)RandMod(rt, 0xb));
                else if (side == 1) M.F32(s + 0x3fc, (float)RandMod(rt, 0xb) + 15.0f);
                M.F32(s + 0x400, (float)RandMod(rt, 200));
                float len;
                if (kind < 10)
                {
                    M.F32(s + 0x3f0, (float)RandPow2(rt, 4) * 0.1f + 0.3f);
                    M.F32(s + 0x404, (float)RandMod(rt, 0x1f) + 20.0f);
                    M.U8(s + 0x450, (byte)RandMod(rt, 3));
                    len = (float)RandMod(rt, 0x15) * 0.1f + 1.5f;
                }
                else if (kind < 20)
                {
                    M.F32(s + 0x3f0, (float)RandMod(rt, 6) * 0.1f + 0.5f);
                    M.F32(s + 0x404, (float)RandMod(rt, 0x1f) + 40.0f);
                    M.U8(s + 0x450, (byte)(RandMod(rt, 3) + 10));
                    len = (float)RandMod(rt, 0xb) * 0.1f + 1.0f;
                }
                else
                {
                    M.F32(s + 0x3f0, (float)RandPow2(rt, 4) * 0.1f + 0.4f);
                    M.F32(s + 0x404, (float)RandMod(rt, 0x1f) + 30.0f);
                    M.U8(s + 0x450, (byte)(RandMod(rt, 3) + 0x14));
                    len = (float)RandMod(rt, 0x15) * 0.1f + 1.5f;
                }
                M.F32(s + 0x408, len);
                M.I16(s + 0x466, FxMath.DirectionToAngle(f_180 - m_pos_x, f_188 - m_pos_z));
                M.I16(s + 0x3ea, 0);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b73cc0 (0xb73cc0): follows the owner; at tick 0 launches prim 0x66 (4 electric arcs
        /// facing the target) with elec1.tga; <paramref name="variant"/> is stored at +0x3ec of every slot.
        /// </summary>
        public void Fn_b73cc0(FxRuntime rt, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchSlotPrimEx(rt, 0x66, 5, true, "effect\\elec1.tga");
            p.f_318 = 0f;
            M.U8(p.A + 0x3e8, 0);
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            for (int k = 0; k < 4; k++)
            {
                int off = k * 0xb4;
                uint s = p.A + (uint)off;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, 0);
                M.F32(s + 0x3f0, 0.1f);
                M.I16(s + 0x3ea, 0);
                M.I16(s + 0x466, FxMath.DirectionToAngle(f_180 - m_pos_x, f_188 - m_pos_z));
                M.I32(s + 0x468, 0);
                M.F32(s + 0x3fc, (float)(k >= 2 ? off - 0x168 : off));
                M.F32(s + 0x478, (float)RandMod(rt, 6) * 0.01f + 0.2f);
                M.F32(s + 0x46c, m_pos_x);
                M.F32(s + 0x470, m_pos_y);
                M.F32(s + 0x474, m_pos_z);
                M.F32(s + 0x47c, 20.0f);
                for (uint i = 0x400; i <= 0x44c; i += 4) M.U32(s + i, 0);
                M.I16(s + 0x3ec, variant);
            }
        }

        /// <summary>CRagEffect::fn_b76700 (0xb76700): First Aid — follows the owner; at tick 0 sound + prim 0x3a (sparkle).</summary>
        public void Fn_b76700(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("_heal_effect.wav", m_pos_x, 150.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchSlotPrimEx(rt, 0x3a, 5, false, "effect\\pikapika2.bmp");
            p.f_318 = 0f;
            M.U8(p.A + 0x3e8, 1);
            M.I32(p.A + 0x3f4, 0);
            M.F32(p.A + 0x468, 10.0f);
            M.F32(p.A + 0x41c, 0f);
            M.I16(p.A + 0x3ea, 0);
            M.F32(p.A + 0x46c, m_pos_x);
            M.F32(p.A + 0x474, m_pos_z);
            M.F32(p.A + 0x470, m_pos_y - 12.0f);
            M.F32(p.A + 0x414, 0.02f);
            M.I16(p.A + 0x3ee, (short)RandMod(rt, 0x168));
            M.U8(p.A + 0x450, 0);
            M.U8(p.A + 0x452, 1);
            M.F32(p.A + 0x41c, (float)RandMod(rt, 0x168));
            M.U8(p.A + 0x49c, 0);
            M.I16(p.A + 0x49e, 0);
            M.U8(p.A + 0x550, 0);
            M.I16(p.A + 0x552, 0);
            M.U8(p.A + 0x604, 0);
            M.I16(p.A + 0x606, 0);
        }

        /// <summary>
        /// CRagEffect::fn_b7a730 (0xb7a730): follows the owner; at tick 0 launches prim 0x23 (spirit spheres,
        /// textures super1..5) with <paramref name="count"/> slots; <paramref name="tag"/> goes to +0x454.
        /// </summary>
        public void Fn_b7a730(FxRuntime rt, sbyte count, byte tag)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x23, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 0;
            p.f_1c4 |= 1;
            p.f_1c4 |= 5;
            p.m_duration = f_17c;
            p.f_204 = 5;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture("effect\\super1.bmp"));
            M.U32((uint)p.f_144 + 4, rt.GetTexture("effect\\super2.bmp"));
            M.U32((uint)p.f_144 + 8, rt.GetTexture("effect\\super3.bmp"));
            M.U32((uint)p.f_144 + 0xc, rt.GetTexture("effect\\super4.bmp"));
            M.U32((uint)p.f_144 + 0x10, rt.GetTexture("effect\\super5.bmp"));
            M.U8(p.A + 0x3e8, 0);
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            int n = count;
            for (uint k = 0; n > 0; k++, n--)
            {
                uint s = p.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.F32(s + 0x468, 3.5f);
                M.F32(s + 0x488, (float)(-RandMod(rt, 10)));
                M.F32(s + 0x3fc, 10.0f);
                M.F32(s + 0x400, 70.0f);
                M.F32(s + 0x404, (float)RandMod(rt, 5));
                M.F32(s + 0x408, (float)RandMod(rt, 0x168));
                M.F32(s + 0x470, (float)(-RandMod(rt, 10)));
                M.F32(s + 0x40c, 10.0f);
                M.F32(s + 0x410, 130.0f);
                M.F32(s + 0x414, (float)RandMod(rt, 5));
                M.F32(s + 0x418, (float)RandMod(rt, 0x168));
                M.F32(s + 0x494, (float)(-RandMod(rt, 10)));
                M.F32(s + 0x41c, 10.0f);
                M.F32(s + 0x420, 190.0f);
                M.F32(s + 0x424, (float)RandMod(rt, 5));
                M.F32(s + 0x428, (float)RandMod(rt, 0x168));
                M.F32(s + 0x47c, (float)(-RandMod(rt, 10)));
                M.F32(s + 0x42c, 10.0f);
                M.F32(s + 0x430, 250.0f);
                M.F32(s + 0x434, (float)RandMod(rt, 5));
                M.F32(s + 0x438, (float)RandMod(rt, 0x168));
                M.U8(s + 0x454, tag);
            }
        }

        /// <summary>The 0 → 0x23 / 1 → 0 / else (short) trigger tick of fn_b83b10 / fn_b84010.</summary>
        static int TriggerTick(int param)
        {
            if ((sbyte)param == 0) return 0x23;
            if ((sbyte)param == 1) return 0;
            return (short)param;
        }

        /// <summary>
        /// CRagEffect::fn_b83b10 (0xb83b10): at the trigger tick launches prim 0x5e (4 kick flares) with
        /// <paramref name="texture"/>, directions spread over [<paramref name="baseAngle"/>, +90).
        /// </summary>
        public void Fn_b83b10(FxRuntime rt, string texture, short baseAngle, int when)
        {
            if (m_stateCnt != TriggerTick(when)) return;
            Prim p = LaunchSlotPrim(rt, 0x5e, texture);
            p.f_318 = 0f;
            for (uint k = 0; k < 4; k++)
            {
                uint s = p.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.F32(s + 0x468, (float)RandMod(rt, 0xb) * 0.2f + 2.5f);
                M.I16(s + 0x3ea, 0);
                short a = (short)((short)RandMod(rt, 0x5a) + baseAngle);
                if (!(a < 0x168)) a = (short)(a - 0x168);
                float c = FxMath.CosTableAt(a);
                float sn = FxMath.SinTableAt(a);
                M.I32(s + 0x3f4, -RandMod(rt, 0xb));
                M.I16(s + 0x466, a);
                M.F32(s + 0x3f0, 0.03f);
                M.F32(s + 0x484, c * 4.0f);
                M.F32(s + 0x48c, sn * 4.0f);
                M.I32(s + 0x488, 0);
                M.F32(s + 0x470, -4.0f);
                M.F32(s + 0x3fc, (float)RandMod(rt, 0x168));
                M.F32(s + 0x400, (float)RandMod(rt, 0x168));
                M.F32(s + 0x404, (float)RandMod(rt, 0x168));
                M.F32(s + 0x408, (float)RandMod(rt, 0x168));
            }
        }

        /// <summary>
        /// CRagEffect::fn_b84010 (0xb84010): at the trigger tick launches prim 0x60 (dust) with <paramref name="texture"/>:
        /// one slot for <paramref name="when"/> 0, four for 1.
        /// </summary>
        public void Fn_b84010(FxRuntime rt, string texture, int when)
        {
            if (m_stateCnt != TriggerTick(when)) return;
            Prim p = LaunchSlotPrim(rt, 0x60, texture);
            p.f_318 = 0f;
            M.U8(p.A + 0x3e8, 0);
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            if ((sbyte)when == 0)
            {
                M.U8(p.A + 0x3e8, 1);
                M.F32(p.A + 0x3f0, (float)RandPow2(rt, 16) * 0.01f + 0.1f);
                M.F32(p.A + 0x468, (float)RandMod(rt, 0xb) * 0.1f + 3.0f);
                M.I32(p.A + 0x3f4, -RandMod(rt, 0x1a));
                M.I16(p.A + 0x3ea, 0);
                M.F32(p.A + 0x488, (float)(-RandPow2(rt, 8)));
                int a = RandMod(rt, 0x168);
                float c = FxMath.CosTableAt(a), sn = FxMath.SinTableAt(a);
                M.F32(p.A + 0x484, c * 2.5f);
                M.F32(p.A + 0x48c, sn * 2.5f);
                M.F32(p.A + 0x3fc, (float)RandMod(rt, 0x168));
                M.F32(p.A + 0x400, (float)RandMod(rt, 0x168));
                M.F32(p.A + 0x404, (float)RandMod(rt, 0x168));
                M.F32(p.A + 0x408, (float)RandMod(rt, 0x168));
                M.U8(p.A + 0x450, 1);
                return;
            }
            if ((sbyte)when == 1)
            {
                for (uint k = 0; k < 4; k++)
                {
                    uint s = p.A + k * 0xb4;
                    M.U8(s + 0x3e8, 1);
                    M.F32(s + 0x3f0, (float)RandMod(rt, 0xb) * 0.005f + 0.08f);
                    M.F32(s + 0x468, (float)RandMod(rt, 0xb) * 0.05f + 2.0f);
                    M.I32(s + 0x3f4, -RandMod(rt, 0x1a));
                    M.I16(s + 0x3ea, 0);
                    M.F32(s + 0x488, (float)(-RandPow2(rt, 8)));
                    int a = RandMod(rt, 0x168);
                    float c = FxMath.CosTableAt(a), sn = FxMath.SinTableAt(a);
                    M.F32(s + 0x484, c * 3.5f);
                    M.F32(s + 0x48c, sn * 3.5f);
                    M.F32(s + 0x3fc, (float)RandMod(rt, 0x168));
                    M.F32(s + 0x400, (float)RandMod(rt, 0x168));
                    M.F32(s + 0x404, (float)RandMod(rt, 0x168));
                    M.F32(s + 0x408, (float)RandMod(rt, 0x168));
                    M.U8(s + 0x450, 2);
                }
            }
        }

        /// <summary>CRagEffect::fn_b8abe0 (0xb8abe0): follows the owner; at tick 0 launches prim 0x5c (4 purple slashes) at <paramref name="angle"/>.</summary>
        public void Fn_b8abe0(FxRuntime rt, short angle)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchSlotPrimEx(rt, 0x5c, 5, true, "effect\\purpleslash.tga");
            p.f_318 = 0f;
            int jitter = RandMod(rt, 6);
            int baseH = -0x1c - jitter;
            for (int k = 0; k < 4; k++)
            {
                uint s = p.A + (uint)k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, baseH + k);
                M.F32(s + 0x468, 20.0f);
                M.I16(s + 0x3ea, 0);
                M.F32(s + 0x46c, m_pos_x);
                M.F32(s + 0x474, m_pos_z);
                int r = RandMod(rt, 5);
                M.F32(s + 0x470, (float)r + (m_pos_y - 10.0f));
                M.I16(s + 0x3f8, angle);
                M.F32(s + 0x400, 0.8f);
                M.U8(s + 0x450, 0);
                M.U8(s + 0x451, 0);
            }
        }

        /// <summary>CRagEffect::fn_b8cd50 (0xb8cd50): follows the owner; at tick 0 launches prim 0x2d (4 lightning strands) towards the target.</summary>
        public void Fn_b8cd50(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchSlotPrim(rt, 0x2d, "effect\\thunder_center.bmp");
            p.f_318 = 0f;
            p.f_388 = f_180;
            p.f_38c = f_184;
            p.f_390 = f_188;
            p.m_pos_x = m_pos_x;
            p.m_pos_y = m_pos_y;
            p.m_pos_z = m_pos_z;
            for (uint k = 0; k < 4; k++)
            {
                uint s = p.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, RandMod(rt, 10) - 0x28);
                M.F32(s + 0x468, (float)RandPow2(rt, 16) * 0.05f + 0.1f);
                M.I32(s + 0x41c, 0);
                M.I16(s + 0x3ea, 0);
                M.F32(s + 0x46c, f_180);
                M.F32(s + 0x474, f_188);
                M.F32(s + 0x470, f_184 - 8.0f);
                M.I16(s + 0x3f8, 0);
                M.F32(s + 0x400, (float)RandMod(rt, 0x32) * 0.01f + 0.75f);
                M.F32(s + 0x404, (float)RandMod(rt, 200) * 0.01f - 1.0f);
                M.I16(s + 0x3ee, 0);
                M.U8(s + 0x450, 0);
                M.U8(s + 0x451, 0);
                M.U8(s + 0x452, 2);
                M.I32(s + 0x418, 0);
            }
        }

        /// <summary>CRagEffect::fn_b8d030 (0xb8d030): at tick 0 (owner position, y - 9) launches prim 0x2c (4 lens flares) with <paramref name="texture"/>.</summary>
        public void Fn_b8d030(FxRuntime rt, string texture, byte tag)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchSlotPrim(rt, 0x2c, texture);
            m_pos_y = m_pos_y - 9.0f;
            for (uint k = 0; k < 4; k++)
            {
                uint s = p.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, -0x14 - RandMod(rt, 10));
                M.F32(s + 0x468, -10.0f);
                M.F32(s + 0x3fc, 2.0f);
                M.I16(s + 0x466, (short)RandMod(rt, 0x168));
                M.I16(s + 0x3ee, (short)RandMod(rt, 0x168));
                M.F32(s + 0x400, 1.0f);
                M.I16(s + 0x3ea, 0);
                M.U8(s + 0x450, tag);
                M.U8(s + 0x451, 0x10);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b8dab0 (0xb8dab0): follows the owner; for ticks 0..8 launches prim 0x45 (4 arrows flying from
        /// around the owner towards the target f_180/f_188).
        /// </summary>
        public void Fn_b8dab0(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt >= 9) return;
            Prim p = LaunchSlotPrim(rt, 0x45, "effect\\alpha_center.tga");
            p.f_318 = 0f;
            p.f_388 = f_180;
            p.f_38c = f_184;
            p.f_390 = f_188;
            float dz = f_188 - m_pos_z;
            float dx = f_180 - m_pos_x;
            float dist = FxRuntime.SqrtF(dz * dz + dx * dx);
            for (uint k = 0; k < 4; k++)
            {
                uint s = p.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, -0x1e - RandMod(rt, 0x6e));
                M.I16(s + 0x3f8, FxMath.DirectionToAngle(dx, dz));
                M.I16(s + 0x3ea, 0);
                M.F32(s + 0x46c, ((float)RandMod(rt, 100) * 0.08f - 4.0f) + m_pos_x);
                M.F32(s + 0x474, ((float)RandMod(rt, 100) * 0.08f - 4.0f) + m_pos_z);
                M.F32(s + 0x470, m_pos_y);
                M.F32(s + 0x400, (float)RandMod(rt, 100) * 0.15f);
                short a = M.I16(s + 0x3f8);
                M.F32(s + 0x46c, M.F32(s + 0x46c) - FxMath.CosTableAt(a) * M.F32(s + 0x400));
                a = M.I16(s + 0x3f8);
                M.F32(s + 0x474, M.F32(s + 0x474) - FxMath.SinTableAt(a) * M.F32(s + 0x400));
                M.U32(s + 0x478, M.U32(s + 0x46c));
                M.U32(s + 0x480, M.U32(s + 0x474));
                M.F32(s + 0x47c, m_pos_y);
                M.F32(s + 0x3f0, (float)RandMod(rt, 0xb) * 0.008f + 0.02f);
                M.F32(s + 0x3fc, (float)RandMod(rt, 100) * 0.06f + 4.0f);
                M.F32(s + 0x468, (float)RandMod(rt, 100) * 0.3f + dist);
                M.F32(s + 0x40c, 3.0f);
            }
        }

        // ---- cases -------------------------------------------------------------------------

        /// <summary>EF 0x113 (Back Stab): sound at 0x14 at the owner, 20 lightning prims at tick 0.</summary>
        [FxEffectProcess(0x113)]
        public FxCaseResult Case_BackStab(FxRuntime rt)
        {
            if (m_stateCnt == 0x14)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\rog_back stap.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            for (int i = 0; i < 0x14; i++) Fn_b8cd50(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x114 (Sightless Mind): 20 lens flare prims (lens2.tga, tag 2) at tick 0.</summary>
        [FxEffectProcess(0x114)]
        public FxCaseResult Case_SightlessMind(FxRuntime rt)
        {
            for (int i = 0; i < 0x14; i++) Fn_b8d030(rt, "effect\\lens2.tga", 2);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x135 (First Aid, vf14): fn_b76700.</summary>
        [FxEffectProcess(0x135)]
        public FxCaseResult Case_FirstAid(FxRuntime rt) { Fn_b76700(rt); return FxCaseResult.Continue; }

        /// <summary>EF 0xfe (Mental Strength): fn_b7a730(4, 0).</summary>
        [FxEffectProcess(0xfe)]
        public FxCaseResult Case_MentalStrength(FxRuntime rt) { Fn_b7a730(rt, 4, 0); return FxCaseResult.Continue; }

        /// <summary>EF 0x189 (Vulcan Arrow): sound at 0x1e at the owner, arrow prims for ticks 0..8.</summary>
        [FxEffectProcess(0x189)]
        public FxCaseResult Case_VulcanArrow(FxRuntime rt)
        {
            if (m_stateCnt == 0x1e)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavArrowVulcan, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            Fn_b8dab0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x199 (Meteor Assault): sound at 0, owner flag 1 + tint for ticks &lt; 11, 8 slash prims at 45° steps.</summary>
        [FxEffectProcess(0x199)]
        public FxCaseResult Case_MeteorAssault(FxRuntime rt)
        {
            uint t = (uint)m_stateCnt;
            if (t == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavMeteorAssault, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = (uint)m_stateCnt;
            }
            if (t < 0xb)
            {
                SetMasterFlag(1);
                rt.SetArgb(M.U32(A + 0x138), -1, 0xff, 0, 0xff);
            }
            for (int a = 0; a < 0x168; a += 0x2d) Fn_b8abe0(rt, (short)a);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x19b (Taekwon Jump): 30 dust prims at tick 0x23 and 4×4 kick flares (peong1/2/3/2) per quadrant.</summary>
        [FxEffectProcess(0x19b)]
        public FxCaseResult Case_TaekwonJump(FxRuntime rt)
        {
            for (int i = 0; i < 0x1e; i++) Fn_b84010(rt, "effect\\whitelight.tga", 0);
            for (int q = 0; q < 4; q++)
            {
                short a = (short)(q * 0x5a);
                Fn_b83b10(rt, "effect\\peong1.tga", a, 0);
                Fn_b83b10(rt, "effect\\peong2.tga", a, 0);
                Fn_b83b10(rt, "effect\\peong3.tga", a, 0);
                Fn_b83b10(rt, "effect\\peong2.tga", a, 0);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x1b1 (Union of the Sun, Moon and Stars): fn_b73cc0(0) twice.</summary>
        [FxEffectProcess(0x1b1)]
        public FxCaseResult Case_UnionSunMoonStars(FxRuntime rt)
        {
            Fn_b73cc0(rt, 0);
            Fn_b73cc0(rt, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x1b7 (Flying Kick): 4× speed lines left + right.</summary>
        [FxEffectProcess(0x1b7)]
        public FxCaseResult Case_FlyingKickLines(FxRuntime rt)
        {
            for (int i = 0; i < 4; i++)
            {
                Fn_b6d260(rt, 0);
                Fn_b6d260(rt, 1);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 0x1c9 (Flying Kick): sound at tick 5 at the owner plus owner state 0x1b and a render pulse
        /// (FUN_00be5f40), owner state back to 2 at tick 0x23.
        /// </summary>
        [FxEffectProcess(0x1c9)]
        public FxCaseResult Case_FlyingKick(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 5)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavFlyingKick, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
                if (t == 5)
                {
                    M.I32(M.U32(A + 0x138) + 0x70, 0x1b);
                    rt.StartRenderPulse(M.U32(A + 0x138), 0x60, 5, 5, 0x60, 5, 1, 0x19, 0);
                    t = m_stateCnt;
                }
            }
            if (t == 0x23 && M.I32(M.U32(A + 0x138) + 0x70) == 0x1b)
                M.I32(M.U32(A + 0x138) + 0x70, 2);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// EF 0x200 (Chain Crush Combo, vf15): 4× yellow speed lines; at tick 0x2c SendMsg(0x7a) and the sound;
        /// owner flag 8 + tint for ticks in [20, 100].
        /// </summary>
        [FxEffectProcess(0x200)]
        public FxCaseResult Case_ChainCrushCombo(FxRuntime rt)
        {
            for (int i = 0; i < 4; i++)
            {
                Fn_b6d260(rt, 10);
                Fn_b6d260(rt, 11);
            }
            int t = m_stateCnt;
            if (t == 0x2c)
            {
                rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavChainCrush, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if ((uint)(t - 0x14) > 0x50) return FxCaseResult.Continue;
            SetMasterFlag(8);
            rt.SetArgb(M.U32(A + 0x138), -1, 0xff, 0xff, 0x96);
            return FxCaseResult.Continue;
        }
    }
}
