// Synced from reference/port/src/RoClassicFx/Rag/effects-3/RagEffect.Effects3.Batch3.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>Zeroes the three 7-slot trail records of ring layer <paramref name="blk"/> (dwords +0x3fc.., bytes +0x450..), in the original order.</summary>
        void E3ClearLayerTrail(uint prim, uint blk)
        {
            for (uint k = 0; k < 3; k++)
            {
                uint d = prim + blk + 0x3fc + k * 0x1c;
                uint b = prim + blk + 0x450 + k * 7;
                for (uint j = 0; j < 7; j++)
                {
                    M.U32(d + j * 4, 0);
                    M.U8(b + j, 0);
                }
            }
        }

        /// <summary>
        /// CRagEffect::fn_b86ba0 (0xb86ba0) — EF 248 (Resistant Souls): follows the master; at tick 0 one ring prim (0x29)
        /// for the whole effect; kind 0 sets up four expanding ring layers.
        /// </summary>
        public void Fn_b86ba0(FxRuntime rt, string texture, sbyte kind)
        {
            E3FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x29, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 5.0f; }
            if (kind != 0) return;
            int layer = 0;
            for (uint blk = 0; blk < 0x2d0; blk += 0xb4, layer++)
            {
                uint a = m_lastPrim.A + blk;
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, 200);
                M.U16(a + 0x3ee, 0x168);
                M.F32(a + 0x3f0, 120.0f);
                M.I16(a + 0x3f8, (short)(layer * 0x5a));
                M.U16(a + 0x3ea, 0x28);
                M.F32(a + 0x468, (float)layer * 0.5f + 6.0f);
                M.I16(a + 0x466, (short)(0x53 - layer));
                M.U16(a + 0x3ec, 1);
                M.U32(a + 0x470, 0);
                E3ClearLayerTrail(m_lastPrim.A, blk);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b870e0 (0xb870e0) — one coin shower prim (0x2b) of EF 274 (Mug): at tick 0, 4 coins flying from
        /// the master towards the target (+0x180..+0x188) with random arc/phase; lowers the effect by 12 each call.
        /// </summary>
        public void Fn_b870e0(FxRuntime rt, string texture)
        {
            if (m_stateCnt != 0) return;
            E3FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x2b, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            E3SetTexture(rt, m_lastPrim, texture);
            m_pos_y = m_pos_y - 12.0f;
            for (uint blk = 0; blk < 0x2d0; blk += 0xb4)
            {
                M.U8(m_lastPrim.A + blk + 0x3e8, 1);
                uint a = m_lastPrim.A + blk;
                M.F32(a + 0x46c, f_180);
                M.F32(a + 0x470, f_184);
                M.F32(a + 0x474, f_188);
                M.I32(m_lastPrim.A + blk + 0x3f4, -0x12 - E3RandMod(rt, 0x1e));
                M.F32(m_lastPrim.A + blk + 0x468, 12.0f);
                M.F32(m_lastPrim.A + blk + 0x3fc, 1.5f);
                M.U16(m_lastPrim.A + blk + 0x466, E3RandShort360(rt));
                M.U16(m_lastPrim.A + blk + 0x3ee, E3RandShort360(rt));
                M.U16(m_lastPrim.A + blk + 0x3f8, E3RandShort360(rt));
                M.F32(m_lastPrim.A + blk + 0x404, (float)E3RandMod(rt, 0x168));
                M.F32(m_lastPrim.A + blk + 0x400, 3.5f);
                M.U16(m_lastPrim.A + blk + 0x3ea, 0);
                M.U8(m_lastPrim.A + blk + 0x450, 1);
            }
        }

        /// <summary>rand() % 360 as the 16-bit store the engine does (movzx dx / cmovns).</summary>
        static ushort E3RandShort360(FxRuntime rt)
        {
            int r = rt.Rand.Next() % 0x168;
            return (ushort)(r >= 0 ? r : 0);
        }

        /// <summary>
        /// CRagEffect::fn_b898f0 (0xb898f0) — one Wind Blade prim (0x7d) of EF 621: at tick 0, four blades from the master
        /// towards the target (+0x180..+0x188), random size and speed; <paramref name="side"/> picks the curve direction.
        /// </summary>
        public void Fn_b898f0(FxRuntime rt, string texture, int side)
        {
            E3FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x7d, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            { Prim lp_ = m_lastPrim; lp_.f_204 = 1; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 0.0f; }
            p = m_lastPrim;
            p.f_388 = f_180;
            p.f_38c = f_184;
            p.f_390 = f_188;
            p = m_lastPrim;
            p.m_pos_x = m_pos_x;
            p.m_pos_y = m_pos_y;
            p.m_pos_z = m_pos_z;
            short index = 0;
            sbyte s = (sbyte)side;
            for (uint blk = 0; blk < 0x2d0; blk += 0xb4)
            {
                M.U8(m_lastPrim.A + blk + 0x3e8, 1);
                M.U32(m_lastPrim.A + blk + 0x3f4, 0);
                M.F32(m_lastPrim.A + blk + 0x468, (float)E3RandMod(rt, 0xb) * 0.5f + 10.0f);
                M.U16(m_lastPrim.A + blk + 0x3ea, 0);
                M.F32(m_lastPrim.A + blk + 0x46c, m_pos_x);
                M.F32(m_lastPrim.A + blk + 0x474, m_pos_z);
                M.F32(m_lastPrim.A + blk + 0x470, m_pos_y - 10.0f);
                M.U16(m_lastPrim.A + blk + 0x3f8, 0);
                M.I16(m_lastPrim.A + blk + 0x466, index);
                M.F32(m_lastPrim.A + blk + 0x400, (float)E3RandMod(rt, 0x15) * 0.1f + 1.0f);
                M.U8(m_lastPrim.A + blk + 0x450, 0);
                M.U8(m_lastPrim.A + blk + 0x451, 0);
                if (s == 0) M.U8(m_lastPrim.A + blk + 0x452, 0);
                else if (s == 1) M.U8(m_lastPrim.A + blk + 0x452, 1);
                index++;
                M.U8(m_lastPrim.A + blk + 0x453, 1);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b8b0d0 (0xb8b0d0) — EF 503 (Spirit): follows the master; at tick 0 one light prim (0x6d) with random
        /// rotation angles and a fixed offset (−sin 90° · 30, −sin 0° · 20 − 40, read straight from the sin table globals).
        /// </summary>
        public void Fn_b8b0d0(FxRuntime rt, string texture)
        {
            E3FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x6d, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 0;
            { Prim lp_ = m_lastPrim; lp_.f_1c4 |= 1; }
            { Prim lp_ = m_lastPrim; lp_.f_1c4 |= 5; }
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_3e8 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_49c = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_550 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_604 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_3e8 = 1; }
            { Prim lp_ = m_lastPrim; lp_.f_3f4 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_468 = 2.5f; }
            { Prim lp_ = m_lastPrim; lp_.f_424 = f_184; }
            M.U32(m_lastPrim.A + 0x430, 0);
            M.U32(m_lastPrim.A + 0x434, 0);
            { Prim lp_ = m_lastPrim; lp_.f_438 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_3fc = (float)E3RandMod(rt, 0x168); }
            { Prim lp_ = m_lastPrim; lp_.f_400 = (float)E3RandMod(rt, 0x168); }
            { Prim lp_ = m_lastPrim; lp_.f_404 = (float)E3RandMod(rt, 0x168); }
            M.F32(m_lastPrim.A + 0x408, (float)E3RandMod(rt, 0x168));
            { Prim lp_ = m_lastPrim; lp_.f_478 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_484 = -FxMath.GetSin(90) * 30.0f; }       // DAT_011a4b90 = sin table[90]
            { Prim lp_ = m_lastPrim; lp_.f_488 = -(FxMath.GetSin(0) * 20.0f) - 40.0f; }   // DAT_011a4a28 = sin table[0]
            { Prim lp_ = m_lastPrim; lp_.f_48c = 0.0f; }
        }

        /// <summary>
        /// CRagEffect::fn_b8ec00 (0xb8ec00) — EF 261 (Fury): follows the master; at tick 0 one ring prim (0x1f) with four
        /// layers (radius 1..4, 90° apart), mode byte +0x3ec = <paramref name="mode"/>.
        /// </summary>
        public void Fn_b8ec00(FxRuntime rt, string texture, sbyte mode)
        {
            E3FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x1f, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_2c0 = 99.0f; }
            int layer = 0;
            for (uint blk = 0; blk < 0x2d0; blk += 0xb4, layer++)
            {
                uint a = m_lastPrim.A + blk;
                M.U8(a + 0x3e8, 1);
                M.U32(a + 0x3f4, 0);
                M.U16(a + 0x3ee, 0x168);
                M.F32(a + 0x3f0, 25.0f);
                M.I16(a + 0x3f8, (short)(layer * 0x5a));
                M.U16(a + 0x3ea, 0);
                M.F32(a + 0x468, (float)layer + 1.0f);
                M.U16(a + 0x466, 0x50);
                M.I16(a + 0x3ec, mode);
                M.U32(a + 0x470, 0);
                E3ClearLayerTrail(m_lastPrim.A, blk);
            }
        }
    }
}
