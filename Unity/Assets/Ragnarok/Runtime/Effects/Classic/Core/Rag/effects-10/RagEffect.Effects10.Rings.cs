// Synced from reference/port/src/RoClassicFx/Rag/effects-10/RagEffect.Effects10.Rings.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>EF 879 (0x36f; case 0xb49fe2, EFST 434): fn_b92420(this, "effect\kiss2.bmp", 2, 0).</summary>
        [FxEffectProcess(0x36f)]
        public FxCaseResult Case_E10_879(FxRuntime rt) { Fn_b92420(rt, "effect\\kiss2.bmp", 2); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b92420 (0xb92420, thiscall (texture, kind, unused), ret 0xc) — follows the owner; at tick 0
        /// launches one prim 0x93 (flags 5, lives as long as the effect; rendered by fn_af6810, ten concentric rings)
        /// with the texture: slot 0 on, size +0x468 = 21 and style +0x454 = 6 / 7 for kinds 1 / 2, else 13 and 5; the
        /// ring radii +0x3fc.. and the block +0x400..+0x44c cleared, alpha 0.
        /// </summary>
        public void Fn_b92420(FxRuntime rt, string texture, short kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x93, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
            { Prim lp = m_lastPrim; lp.f_204 = 1; }
            { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
            uint tex = rt.GetTexture(texture);
            M.U32((uint)m_lastPrim.f_144, tex);
            uint a = m_lastPrim.A;
            M.F32(a + 0x318, 0f);
            M.U8(a + 0x3e8, 0);
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            if (kind == 1) { M.F32(a + 0x468, 21.0f); M.U8(a + 0x454, 6); }
            else if (kind == 2) { M.F32(a + 0x468, 21.0f); M.U8(a + 0x454, 7); }
            else { M.F32(a + 0x468, 13.0f); M.U8(a + 0x454, 5); }
            M.F32(a + 0x3fc, 0f);
            M.U16(a + 0x3ea, 0);
            M.U8(a + 0x450, 0);
            for (uint off = 0x400; off < 0x450; off += 4) M.U32(a + off, 0);
        }

        /// <summary>
        /// EF 909 (0x38d; case 0xb481c0, EFST 405 — LG_BANDING): tick 10 follow + sound LG_BANDING.wav (player-relative);
        /// 40 × fn_b9a3f0(this, "effect\alpha_center.tga", 2, 0) — only tick 0 launches (40 prims 0x9f).
        /// </summary>
        [FxEffectProcess(0x38d)]
        public FxCaseResult Case_E10_Banding(FxRuntime rt)
        {
            if (m_stateCnt == 10)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\LG_BANDING.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            for (int i = 0; i < 0x28; i++) Fn_b9a3f0(rt, "effect\\alpha_center.tga", 2);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b9a3f0 (0xb9a3f0, thiscall (texture, colour, unused), ret 0xc) — follows the owner; at tick 0
        /// launches one prim 0x9f (flags 5, lives as long as the effect) whose four slots each start at a random point
        /// of a radius-70 sphere around (0, −5, 0) (angles +0x466 / +0x3f8 = rand() % 360), delayed by −(rand() % 101)
        /// ticks, size 4, speed 0.5, colour selector +0x452.
        /// </summary>
        public void Fn_b9a3f0(FxRuntime rt, string texture, sbyte colour)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x9f, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
            { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
            uint tex = rt.GetTexture(texture);
            M.U32((uint)m_lastPrim.f_144, tex);
            for (uint off = 0; off < 0x2d0; off += 0xb4)
            {
                M.U8(m_lastPrim.A + off + 0x3e8, 1);
                M.U32(m_lastPrim.A + off + 0x40c, 0);
                int r = rt.Rand.Next();
                M.I32(m_lastPrim.A + off + 0x3f4, (-r) % 0x65);
                M.F32(m_lastPrim.A + off + 0x468, 4.0f);
                M.U16(m_lastPrim.A + off + 0x3ea, 0);
                r = rt.Rand.Next() % 0x168;
                M.U16(m_lastPrim.A + off + 0x466, (ushort)(r < 0 ? 0 : r));
                r = rt.Rand.Next() % 0x168;
                M.U16(m_lastPrim.A + off + 0x3f8, (ushort)(r < 0 ? 0 : r));
                M.F32(m_lastPrim.A + off + 0x3fc, -5.0f);
                M.F32(m_lastPrim.A + off + 0x400, 0.5f);
                M.U8(m_lastPrim.A + off + 0x450, 0);
                M.U8(m_lastPrim.A + off + 0x451, 0);
                M.U8(m_lastPrim.A + off + 0x452, (byte)colour);
                uint b = m_lastPrim.A + off;
                int ph = M.I16(b + 0x466);
                float c1 = FxMath.CosTableAt(ph) * 70.0f;
                float s1 = FxMath.SinTableAt(ph) * 70.0f;
                int a2 = M.I16(b + 0x3f8);
                float x = c1 * FxMath.CosTableAt(a2);
                float y = (c1 * FxMath.SinTableAt(a2)) + M.F32(b + 0x3fc);
                M.F32(b + 0x474, s1);
                M.F32(b + 0x46c, x);
                M.F32(b + 0x470, y);
            }
        }
    }
}
