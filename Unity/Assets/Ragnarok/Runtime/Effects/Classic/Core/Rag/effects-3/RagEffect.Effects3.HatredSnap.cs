// Synced from reference/port/src/RoClassicFx/Rag/effects-3/RagEffect.Effects3.HatredSnap.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>One ring prim (0x6b) of fn_b66f60: four layers, radius = width, phases/speeds per layer.</summary>
        void E3HatredRing(FxRuntime rt, string texture, ushort p0, float w0, ushort s0, ushort p1, float w1, ushort s1,
            ushort p2, float w2, ushort s2, ushort p3, float w3, ushort s3)
        {
            Prim p = LaunchEffectPrim(rt, 0x6b, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 12.0f; }
            E3FireRingLayer(0x000, 0x168, w0, p0, w0, s0);
            E3FireRingLayer(0x0b4, 0x168, w1, p1, w1, s1);
            E3FireRingLayer(0x168, 0x168, w2, p2, w2, s2);
            E3FireRingLayer(0x21c, 0x168, w3, p3, w3, s3);
        }

        /// <summary>
        /// CRagEffect::fn_b66f60 (0xb66f60) — EF 487 (Hatred of the Sun Moon and Stars): follows the master; two ring prims
        /// (0x6b, 4 layers each) at ticks 1 and 2.
        /// </summary>
        public void Fn_b66f60(FxRuntime rt, string texture)
        {
            E3FollowMaster();
            int t = m_stateCnt;
            if (t == 1)
            {
                E3HatredRing(rt, texture, 0x10e, 2.5f, 0x35, 0, 5.0f, 0x3c, 0x5a, 7.5f, 0x37, 0xb4, 10.0f, 0x32);
                t = m_stateCnt;
            }
            if (t == 2)
                E3HatredRing(rt, texture, 0x10f, 2.7f, 0x34, 1, 5.2f, 0x3b, 0x5b, 7.7f, 0x36, 0xb5, 10.2f, 0x31);
        }

        /// <summary>SetARGB(obj, -1, (int)(master byte + pos) &gt;&gt; 1 …) of the Snap tint (bytes +0x95/+0x96/+0x97 of the master).</summary>
        void E3SnapHalfTint(FxRuntime rt, uint obj, uint master)
        {
            int b = FxOps.Cvtt((float)M.U8(master + 0x97) + m_pos_z) >> 1;
            int g = FxOps.Cvtt((float)M.U8(master + 0x96) + m_pos_y) >> 1;
            int r = FxOps.Cvtt((float)M.U8(master + 0x95) + m_pos_x) >> 1;
            rt.SetArgb(obj, -1, r, g, b);
        }

        /// <summary>
        /// CRagEffect::fn_b45180 (0xb45180) — EF 166 (Snap): tints the master (the effect's position fields carry the colour)
        /// and every 5th tick (every tick when the duration is 30) leaves an after-image (CBlurPC, 0x508 bytes, ctor 0xcbda20)
        /// in the world's render list. The return value is ignored by the case.
        /// </summary>
        public void Fn_b45180(FxRuntime rt)
        {
            uint master = m_master;
            uint obj;
            if (M.U8(master + 0x90) != 0)
            {
                E3SnapHalfTint(rt, master, master);
                int t = m_stateCnt;
                if (t == 0x270e) m_stateCnt = 0;
                else if (t % 5 != 0) return;
                obj = M.Alloc(0x508);
                rt.ConstructBlurPc_cbda20(obj, m_master);
                E3SnapHalfTint(rt, obj, m_master);
                E3SnapAddToWorld(rt, obj);
                return;
            }

            rt.SetArgb(master, -1, FxOps.Cvtt(m_pos_x), FxOps.Cvtt(m_pos_y), FxOps.Cvtt(m_pos_z));
            int cnt = m_stateCnt;
            if (cnt == 0x270e)
            {
                m_stateCnt = 0;
                cnt = 0;
            }
            float x = m_pos_x;
            if (x == -1.0f)
            {
                int v = rt.Rand.Next() % 0xe <= 0 ? 0xfa : 0xc8;
                rt.SetArgb(m_master, -1, 0xfa, v, v);
                return;
            }
            if (x == -2.0f)
            {
                rt.SetArgb(m_master, -1, 0xfa, 0xfa, 0xc8);
                return;
            }
            if (f_17c != 0x1e)
            {
                if (cnt % 5 != 0) return;
                int job = M.I32(m_master + 0x70);
                if (job == 0 || job == 10 || job == 6 || job == 0xb || job == 0xd) return;
            }
            obj = M.Alloc(0x508);
            rt.ConstructBlurPc_cbda20(obj, m_master);
            int z = FxOps.Cvtt(m_pos_z);
            int y = FxOps.Cvtt(m_pos_y);
            int xi = FxOps.Cvtt(m_pos_x);
            rt.BlurPcSendMsg_cc13a0(obj, A, 0, 0, xi, y, z);
            E3SnapAddToWorld(rt, obj);
        }

        /// <summary>GetWorld()->+0xcc world, render list +8: std::list push_back (0x783f90).</summary>
        static void E3SnapAddToWorld(FxRuntime rt, uint obj)
            => rt.ListPushBack(rt.M.U32(rt.Mode + 0xcc) + 8, obj);
    }
}
