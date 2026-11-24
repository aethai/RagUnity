// Synced from reference/port/src/RoClassicFx/Rag/effects-3/RagEffect.Effects3.Basilica.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b67e70 (0xb67e70) — EF 374 (Basilica, called with kinds 0 and 1): follows the master; at tick 0 one
        /// prim 0x5a for the whole effect with two wall layers (blocks 0-1) and two pillar layers (blocks 2-3).
        /// Translated from the assembly's label structure (kinds 10/11/20/21 are other callers' variants).
        /// </summary>
        public void Fn_b67e70(FxRuntime rt, string texture, sbyte kind)
        {
            E3FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x5a, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 1;
            { Prim lp_ = m_lastPrim; lp_.f_1c4 |= 5; }
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_3e8 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_49c = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_550 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_604 = 0; }
            float fkind = (float)(int)kind;
            bool shortLived = (byte)(kind - 10) <= 9;

            int i = 0;
            for (uint blk = 0; blk < 0x168; blk += 0xb4)
            {
                uint a = m_lastPrim.A + blk;
                M.U8(a + 0x3e8, 1);
                M.U32(m_lastPrim.A + blk + 0x3f4, 0);
                float fi = (float)i;
                float v;
                ushort code;
                if (kind == 0 || kind == 10 || kind == 20)
                {
                    M.F32(m_lastPrim.A + blk + 0x3f0, fi + 30.0f);
                    if (kind == 0 || kind == 10)
                    {
                        M.U16(m_lastPrim.A + blk + 0x3ea, 0x41);
                        code = 0x96f;
                        goto L681ad;
                    }
                    M.U16(m_lastPrim.A + blk + 0x3ea, 0x41);        // kind 20
                    M.U16(m_lastPrim.A + blk + 0x3ec, 0x87a);
                    goto L680b2;
                }
                M.F32(m_lastPrim.A + blk + 0x3f0, fi + 20.0f);
                M.U16(m_lastPrim.A + blk + 0x3ea, 0x20);
                if (kind == 0x15)
                {
                    M.U16(m_lastPrim.A + blk + 0x3ec, 0x85c);
                    goto L6804d;
                }
                code = 0x951;
            L681ad:
                M.U16(m_lastPrim.A + blk + 0x3ec, code);
                if (shortLived) M.I16(m_lastPrim.A + blk + 0x3ec, (short)(f_17c > 30000 ? 30000 : f_17c - 0x5a));
                if (kind == 10 || kind == 20) goto L680b2;
                if (kind == 11 || kind == 0x15) goto L6804d;
                v = fi * 0.4f + (kind == 0 ? 12.5f : 14.0f);
                goto L680c0;
            L6804d:
                v = fi * 0.4f + 8.5f;
                goto L680c0;
            L680b2:
                v = fi * 0.4f + 7.0f;
            L680c0:
                M.F32(m_lastPrim.A + blk + 0x3fc, v);
                i++;
                M.U32(m_lastPrim.A + blk + 0x400, M.U32(m_lastPrim.A + blk + 0x3fc));
                M.F32(m_lastPrim.A + blk + 0x40c, fkind);
            }

            int k = 2;
            for (uint blk = 0x168; blk < 0x2d0; blk += 0xb4)
            {
                M.U8(m_lastPrim.A + blk + 0x3e8, 1);
                M.U32(m_lastPrim.A + blk + 0x3f4, 0);
                float step = (float)(k - 2);
                if (kind == 0 || kind == 10 || kind == 20)
                {
                    M.F32(m_lastPrim.A + blk + 0x3f0, (float)k + 30.0f);
                    if (kind == 10 || kind == 20)
                    {
                        M.F32(m_lastPrim.A + blk + 0x468, step * 30.2f);
                        M.U32(m_lastPrim.A + blk + 0x3fc, 0x40f9999a);      // 7.8
                        goto L6834e;
                    }
                }
                else M.F32(m_lastPrim.A + blk + 0x3f0, (float)k + 20.0f);
                // 0xb68255
                if (kind == 11 || kind == 0x15)
                {
                    M.F32(m_lastPrim.A + blk + 0x468, step * 30.2f + 15.1f);
                    M.U32(m_lastPrim.A + blk + 0x3fc, 0x4114cccd);          // 9.3
                    goto L6834e;
                }
                float w = step * 53.2f;
                if (kind == 0)
                {
                    M.F32(m_lastPrim.A + blk + 0x468, w);
                    M.U32(m_lastPrim.A + blk + 0x3fc, 0x4154cccd);          // 13.3
                }
                else
                {
                    M.F32(m_lastPrim.A + blk + 0x468, w + 26.6f);
                    M.U32(m_lastPrim.A + blk + 0x3fc, 0x416ccccd);          // 14.8
                }
            L6834e:
                M.U32(m_lastPrim.A + blk + 0x400, M.U32(m_lastPrim.A + blk + 0x3fc));
                M.U32(m_lastPrim.A + blk + 0x408, kind < 10 ? 0x41d4cccdu : 0x4194cccdu);   // 26.6 / 18.6
                M.U16(m_lastPrim.A + blk + 0x3ea, 0xf);
                M.U16(m_lastPrim.A + blk + 0x3ec, (ushort)(kind < 0x14 ? 0x96f : 0x87a));
                if (shortLived) M.I16(m_lastPrim.A + blk + 0x3ec, (short)(f_17c > 30000 ? 30000 : f_17c - 0x5a));
                k++;
                M.F32(m_lastPrim.A + blk + 0x40c, fkind);
            }
        }
    }
}
