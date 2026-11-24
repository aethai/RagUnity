// Synced from reference/port/src/RoClassicFx/Rag/effects-3/RagEffect.Effects3.Batch5.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>0xffc1bc.. CP949 sprite names of fn_b82770 as byte strings ("이펙트\\cconfine.*", "이펙트\\스킬.*"? ).</summary>
        internal const string E3ConfineSpr = "ÀÌÆÑÆ®\\cconfine.spr";
        internal const string E3ConfineAct = "ÀÌÆÑÆ®\\cconfine.act";
        internal const string E3SmokeSpr = "ÀÌÆÑÆ®\\½ºÅé.spr";
        internal const string E3SmokeAct = "ÀÌÆÑÆ®\\½ºÅé.act";

        /// <summary>
        /// CRagEffect::fn_b82770 (0xb82770) — EF 604 (Close Confine, kind 2): follows the master; at tick 0 one sprite prim
        /// (0x33): kind 2 = cconfine.spr/act (12 frames, flag 0x800), otherwise the 7-lower sprite with action 0.
        /// </summary>
        public void Fn_b82770(FxRuntime rt, sbyte kind)
        {
            E3FollowMaster();
            if (m_stateCnt != 0) return;
            E3FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x33, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 |= 0x200;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            { Prim lp_ = m_lastPrim; lp_.f_3e8 = 1; }
            { Prim lp_ = m_lastPrim; lp_.f_3f4 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_3f0 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_318 = 1.0f; }
            p = m_lastPrim;
            p.f_484 = m_pos_x;
            p.f_488 = m_pos_y;
            p.f_48c = m_pos_z;
            p = m_lastPrim;
            if (kind == 2)
            {
                p.f_454 = 1;
                { Prim lp_ = m_lastPrim; lp_.f_324 = 100.0f; }
                rt.SetSprName(m_lastPrim.A, E3ConfineSpr);
                rt.SetActName(m_lastPrim.A, E3ConfineAct);
                { Prim lp_ = m_lastPrim; lp_.f_1e8 = 0xc; }
                { Prim lp_ = m_lastPrim; lp_.f_254 = 0; }
                { Prim lp_ = m_lastPrim; lp_.f_1dc = 0x800; }
            }
            else
            {
                p.f_488 = p.f_488 - 7.0f;
                { Prim lp_ = m_lastPrim; lp_.f_454 = 1; }
                { Prim lp_ = m_lastPrim; lp_.f_324 = 200.0f; }
                rt.SetSprName(m_lastPrim.A, E3SmokeSpr);
                rt.SetActName(m_lastPrim.A, E3SmokeAct);
                { Prim lp_ = m_lastPrim; lp_.f_1e8 = 4; }
                rt.SetAction(m_lastPrim.A, 0, 0x10, 0);
            }
            m_lastPrim.SnapshotTrail_ad58d0();
            { Prim lp_ = m_lastPrim; lp_.f_49c = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_550 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_604 = 0; }
        }

        /// <summary>
        /// CRagEffect::fn_b83790 (0xb83790) — EF 543/546 (Kaahi, Kaupe): follows the master; every 4th tick inside
        /// [0, 80] (kind 2: [40, 120]) one prim 0x3b of four rising sparks (pok1/pok3.tga, the texture argument is unused).
        /// </summary>
        public void Fn_b83790(FxRuntime rt, string unusedTexture, sbyte kind)
        {
            E3FollowMaster();
            int t = m_stateCnt;
            if (t < (kind == 2 ? 0x28 : 0)) return;
            if (t > (kind == 2 ? 0x78 : 0x50)) return;
            if (t % 4 != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x3b, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            { Prim lp_ = m_lastPrim; lp_.f_204 = 2; }
            { Prim lp_ = m_lastPrim; lp_.f_144 = (int)rt.NewArray((ulong)(uint)m_lastPrim.f_204 * 4); }
            uint t0 = rt.GetTexture("effect\\pok1.tga");
            M.U32((uint)m_lastPrim.f_144, t0);
            uint t1 = rt.GetTexture("effect\\pok3.tga");
            M.U32((uint)m_lastPrim.f_144 + 4, t1);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 0.0f; }
            p = m_lastPrim;
            p.m_pos_x = m_pos_x;
            p.m_pos_y = m_pos_y;
            p.m_pos_z = m_pos_z;
            for (uint blk = 0; blk < 0x2d0; blk += 0xb4)
            {
                uint a = m_lastPrim.A + blk;
                M.U8(a + 0x3e8, 1);
                M.U32(a + 0x3f4, 0);
                M.F32(m_lastPrim.A + blk + 0x468, (float)E3RandMod(rt, 10) * 0.1f + 2.0f);
                M.U32(m_lastPrim.A + blk + 0x41c, 0);
                M.U16(m_lastPrim.A + blk + 0x3ea, 0);
                M.F32(m_lastPrim.A + blk + 0x46c, (float)E3RandMod(rt, 0xb) + m_pos_x - 5.0f);
                M.F32(m_lastPrim.A + blk + 0x474, (float)E3RandMod(rt, 0xb) + m_pos_z - 5.0f);
                M.F32(m_lastPrim.A + blk + 0x470, m_pos_y - 6.0f);
                M.U16(m_lastPrim.A + blk + 0x3f8, 0);
                M.U32(m_lastPrim.A + blk + 0x400, 0);
                M.F32(m_lastPrim.A + blk + 0x404, (float)E3RandMod(rt, 0x14) * 0.01f + 0.1f);
                M.U16(m_lastPrim.A + blk + 0x3ee, 0);
                M.U8(m_lastPrim.A + blk + 0x450, 0);
                M.U8(m_lastPrim.A + blk + 0x451, 1);
                if (kind == 0) M.U8(m_lastPrim.A + blk + 0x452, 0xb);
                else if (kind == 1) M.U8(m_lastPrim.A + blk + 0x452, 1);
                else if (kind == 2) M.U8(m_lastPrim.A + blk + 0x452, 1);
                else if (kind == 3) M.U8(m_lastPrim.A + blk + 0x452, 0xd);
                M.U32(m_lastPrim.A + blk + 0x418, 0);
            }
        }
    }
}
