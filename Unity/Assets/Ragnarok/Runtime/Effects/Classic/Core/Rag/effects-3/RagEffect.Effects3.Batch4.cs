// Synced from reference/port/src/RoClassicFx/Rag/effects-3/RagEffect.Effects3.Batch4.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>The four-letter "soul" word prim (0x44) of fn_b68750: 4 textures, 4 letter layers.</summary>
        void E3SoulWord(FxRuntime rt, string t0, string t1, string t2, string t3, int start, int end, int x0)
        {
            Prim p = LaunchEffectPrim(rt, 0x44, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            { Prim lp_ = m_lastPrim; lp_.f_204 = 4; }
            { Prim lp_ = m_lastPrim; lp_.f_144 = (int)rt.NewArray((ulong)(uint)m_lastPrim.f_204 * 4); }
            string[] names = { t0, t1, t2, t3 };
            for (uint i = 0; i < 4; i++)
            {
                uint tex = rt.GetTexture(names[i]);
                M.U32((uint)m_lastPrim.f_144 + i * 4, tex);
            }
            { Prim lp_ = m_lastPrim; lp_.f_318 = 0.0f; }
            for (uint blk = 0; blk < 0x2d0; blk += 0xb4) M.U8(m_lastPrim.A + blk + 0x3e8, 0);
            int delay = start;
            uint b = 0;
            int x = x0;
            byte letter = 0;
            do
            {
                uint a = m_lastPrim.A + b;
                M.U8(a + 0x3e8, 1);
                M.I32(a + 0x3f4, delay);
                M.F32(a + 0x468, 5.0f);
                M.U16(a + 0x3ea, 0);
                M.F32(a + 0x3fc, (float)x);
                M.U8(a + 0x450, letter);
                delay += 0x14;
                M.U8(a + 0x454, 2);
                for (uint k = 0; k < 10; k++) M.U32(a + 0x400 + k * 4, 0);
                b += 0xb4;
                x -= 6;
                letter++;
            } while (delay < end);
        }

        /// <summary>
        /// CRagEffect::fn_b68750 (0xb68750) — EF 503 (Spirit): follows the master; "SOUL" letters (prim 0x44) at tick 1
        /// and "LINK" at tick 21, each letter delayed by 20 ticks. The stack argument is unused.
        /// </summary>
        public void Fn_b68750(FxRuntime rt, int unused)
        {
            E3FollowMaster();
            int t = m_stateCnt;
            if (t == 1)
            {
                E3SoulWord(rt, "effect\\soul_l.tga", "effect\\soul_u.tga", "effect\\soul_o.tga", "effect\\soul_s.tga", -0x3c, 0x14, -4);
                t = m_stateCnt;
            }
            if (t == 0x15)
                E3SoulWord(rt, "effect\\soul_k.tga", "effect\\soul_n.tga", "effect\\soul_i.tga", "effect\\soul_l.tga", -0x78, -0x28, 0x16);
        }

        /// <summary>One ring layer of fn_b69460 at block offset <paramref name="blk"/> (0xb4 bytes from +0x3e8).</summary>
        void E3FireRingLayer(uint blk, ushort span, float radius, ushort phase, float width, ushort speed)
        {
            uint a = m_lastPrim.A + blk;
            M.U8(a + 0x3e8, 1);
            M.U32(m_lastPrim.A + blk + 0x3f4, 0);
            M.U16(m_lastPrim.A + blk + 0x3ee, span);
            M.F32(m_lastPrim.A + blk + 0x3f0, radius);
            M.U16(m_lastPrim.A + blk + 0x3f8, phase);
            M.U16(m_lastPrim.A + blk + 0x3ea, 0);
            M.F32(m_lastPrim.A + blk + 0x468, width);
            M.U16(m_lastPrim.A + blk + 0x466, speed);
            M.U32(m_lastPrim.A + blk + 0x470, 0);
            E3ClearLayerTrail(m_lastPrim.A, blk);
        }

        /// <summary>
        /// CRagEffect::fn_b69460 (0xb69460) — EF 537 (Acid Demonstration): at tick 0 the fire-wall sound, then one prim 0x76
        /// at the master with four fire rings. The second stack argument is unused.
        /// </summary>
        public void Fn_b69460(FxRuntime rt, string texture, int unused)
        {
            if (m_stateCnt != 0) return;
            E3Sound(rt, "effect\\EF_FireWall.wav", 0.0f);
            E3FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x76, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 13.0f; }
            E3FireRingLayer(0x000, 0x13b, 25.0f, 0, 2.4f, 0x46);
            E3FireRingLayer(0x0b4, 0x13b, 22.0f, 0x5a, 2.7f, 0x39);
            E3FireRingLayer(0x168, 0x13b, 19.0f, 0xb4, 3.0f, 0x2d);
            E3FireRingLayer(0x21c, 0x168, 30.0f, 0, 2.2f, 0x5a);
        }
    }
}
