// Synced from reference/port/src/RoClassicFx/Rag/effects-8/RagEffect.Effects8.Rings.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-8: the persistent skill-unit ground effects Safety Wall (EF 315) and Warp Portal (EF 316 / 317).
    /// They launch "ring" prims (types 0x3c / 0x3e / 0x3f) whose four 0xb4-byte blocks at +0x3e8/+0x49c/+0x550/+0x604
    /// hold: +0 enable byte, +2 alpha (short), +4 alpha limit (short), +6 arc (short), +8 radius (float), +0xc life
    /// (int), +0x10 start angle (short), +0x14..+0x67 21 per-segment dwords, +0x68..+0x7c 21 per-segment bytes,
    /// +0x7e short, +0x80 height (float), +0x88 dword.
    ///
    /// Lifetime: the unit (CSkill, one per ZC_SKILL_ENTRY unit id 0x7e Safety Wall, 0x80 / 0x81 Warp Portal) relaunches
    /// these every DefaultDuration − k CSkill::OnProcess ticks (k = 7 for 315, 8 for 316 / 317; out/skill_units_exe.json),
    /// so each instance simply runs out its own duration (315: 9999, 316: 200, 317: 9999 ticks). When the unit
    /// disappears (ZC_SKILL_DISAPPEAR 0x120) the CSkill is deleted and CAbleToMakeEffect::~CAbleToMakeEffect (0xbcdc60)
    /// sends effect msg 0x18 to every effect in its list: CRagEffect::SendMsg (vf16 0xb5e650, case 1) sets the owner
    /// (+0x138) to 0 and the next OnProcess returns false (see FxWorld.ReleaseOwner).
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 315 (0x13b, vf14) Safety Wall — fn_b77fa0("effect\alpha_down.tga"); alive while its result is true.</summary>
        [FxEffectProcess(0x13b)]
        public FxCaseResult Case_E8_SafetyWall(FxRuntime rt)
            => Fn_b77fa0(rt, "effect\\alpha_down.tga") ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>EF 316 (0x13c, vf14) Warp Portal (open) — fn_b87500(); returns true.</summary>
        [FxEffectProcess(0x13c)]
        public FxCaseResult Case_E8_WarpPortal(FxRuntime rt) { Fn_b87500(rt); return FxCaseResult.Continue; }

        /// <summary>EF 317 (0x13d, vf14) Warp Portal (opening) — fn_b848e0(0); returns true.</summary>
        [FxEffectProcess(0x13d)]
        public FxCaseResult Case_E8_WarpPortalOpening(FxRuntime rt) { Fn_b848e0(rt, 0); return FxCaseResult.Continue; }

        /// <summary>Block base (prim +0x3e8 + i·0xb4).</summary>
        static uint E8_RingBlock(Prim p, int i) => p.A + 0x3e8 + (uint)i * 0xb4;

        /// <summary>The fill loop of a ring block: the 21 dwords at +0x14 and the 21 bytes at +0x68.</summary>
        void E8_RingFill(uint s, uint dwordValue, byte byteValue)
        {
            for (uint o = 0x14; o < 0x68; o += 4) M.U32(s + o, dwordValue);
            for (uint o = 0x68; o < 0x7d; o++) M.U8(s + o, byteValue);
        }

        /// <summary>m_lastPrim = LaunchEffectPrim(type) with flags 5, duration = f_17c, one texture (f_204 as launched).</summary>
        Prim E8_LaunchRing(FxRuntime rt, int type, string texture)
        {
            Prim p = LaunchEffectPrim(rt, type, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            return p;
        }

        /// <summary>rand() % 360 clamped at 0, as a short (the start angles of fn_b77fa0 / fn_b848e0).</summary>
        static short E8_RandAngle(FxRuntime rt)
        {
            int r = rt.Rand.Next() % 0x168;
            return r >= 0 ? (short)r : (short)0;
        }

        /// <summary>
        /// CRagEffect::fn_b77fa0 (0xb77fa0) — Safety Wall: follows the owner; at tick 0 EF_GlassWall.wav and a prim
        /// 0x3c (f_318 = 10) with three stacked glass rings (radius 34/37/40, heights 3.6/3.3/3.0, random start
        /// angles, alpha limits 5500/3080/3080). Then the STR player (0, −80, 0; result ignored). Returns
        /// m_stateCnt != f_17c.
        /// </summary>
        public bool Fn_b77fa0(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt == 0)
            {
                rt.PlaySound3DRelative("effect\\EF_GlassWall.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Prim p = E8_LaunchRing(rt, 0x3c, texture);
                p.f_318 = 10.0f;
                // block 0
                uint s = E8_RingBlock(p, 0);
                M.U8(s, 1);
                M.I32(s + 0xc, 0);
                M.I16(s + 6, 0x168);
                M.U32(s + 8, 0x42080000);            // 34.0
                M.I16(s + 0x10, E8_RandAngle(rt));
                M.I16(s + 2, 0);
                M.U32(s + 0x80, 0x40666666);         // 3.6
                M.I16(s + 0x7e, 0x5a);
                M.I16(s + 4, 0x157c);
                M.U32(s + 0x88, 0);
                E8_RingFill(s, 0, 0);
                // block 1
                s = E8_RingBlock(p, 1);
                M.U8(s, 1);
                M.I32(s + 0xc, 0);
                M.I16(s + 6, 0x168);
                M.U32(s + 8, 0x42140000);            // 37.0
                M.I16(s + 0x10, E8_RandAngle(rt));
                M.I16(s + 2, 0);
                M.U32(s + 0x80, 0x40533333);         // 3.3
                M.I16(s + 0x7e, 0x5a);
                M.I16(s + 4, 0xc08);
                M.U32(s + 0x88, 0);
                E8_RingFill(s, 0, 0);
                // block 2
                s = E8_RingBlock(p, 2);
                M.U8(s, 1);
                M.I32(s + 0xc, 0);
                M.I16(s + 6, 0x168);
                M.U32(s + 8, 0x42200000);            // 40.0
                M.I16(s + 0x10, E8_RandAngle(rt));
                M.I16(s + 2, 0);
                M.U32(s + 0x80, 0x40400000);         // 3.0
                M.I16(s + 0x7e, 0x5a);
                M.I16(s + 4, 0xc08);
                M.U32(s + 0x88, 0);
                E8_RingFill(s, 0, 0);
                M.U8(E8_RingBlock(p, 3), 0);
            }
            rt.StrPlayer(this, 0f, -80.0f, 0f);
            return m_stateCnt != f_17c;
        }

        /// <summary>
        /// CRagEffect::fn_b87500 (0xb87500) — Warp Portal: every 14 ticks snap to the owner + EF_ReadyPortal.wav; at
        /// tick 0 a prim 0x3f (ring_blue.tga, f_318 = 5) with three rising cylinders (radius 6.001, start life
        /// 0/−10/−20, heights step 2/3/4, alpha limit 100, all segment bytes 1) and block 3 mode (+0x608) = 1.
        /// </summary>
        public void Fn_b87500(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t % 0xe == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\EF_ReadyPortal.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t != 0) return;
            Prim p = E8_LaunchRing(rt, 0x3f, "effect\\ring_blue.tga");
            p.f_318 = 5.0f;
            E8_WarpCylinders(p, 0, -10, -0x14, 100, 1);
            M.U8(E8_RingBlock(p, 3), 0);
            M.I32(E8_RingBlock(p, 3) + 0xc, 0);
            M.I16(E8_RingBlock(p, 3) + 4, 1);   // +0x608: the mode fn_ae8040 switches on
        }

        /// <summary>
        /// The three cylinder blocks of the Warp Portal prim 0x3f (fn_b87500 / fn_b848e0 tick 1): radius 6.001, arc
        /// 360, start angles 0/25/50, heights 0 with steps (+0x7e) 2/3/4, the given start lives and alpha limit.
        /// </summary>
        void E8_WarpCylinders(Prim p, int life0, int life1, int life2, short limit, byte segByte)
        {
            uint s = E8_RingBlock(p, 0);
            M.U8(s, 1);
            M.I32(s + 0xc, life0);
            M.I16(s + 6, 0x168);
            M.U32(s + 8, 0x40c00831);            // 6.001
            M.I16(s + 0x10, 0);
            M.I16(s + 2, 0);
            M.U32(s + 0x80, 0);
            M.I16(s + 0x7e, 2);
            M.I16(s + 4, limit);
            M.U32(s + 0x88, 0);
            E8_RingFill(s, 0, segByte);
            s = E8_RingBlock(p, 1);
            M.U8(s, 1);
            M.I32(s + 0xc, life1);
            M.I16(s + 6, 0x168);
            M.U32(s + 8, 0x40c00831);
            M.I16(s + 0x10, 0x19);
            M.I16(s + 2, 0);
            M.U32(s + 0x80, 0);
            M.I16(s + 0x7e, 3);
            M.I16(s + 4, limit);
            M.U32(s + 0x88, 0);
            E8_RingFill(s, 0, segByte);
            s = E8_RingBlock(p, 2);
            M.U8(s, 1);
            M.I32(s + 0xc, life2);
            M.I16(s + 6, 0x168);
            M.U32(s + 8, 0x40c00831);
            M.I16(s + 0x10, 0x32);
            M.I16(s + 2, 0);
            M.U32(s + 0x80, 0);
            M.I16(s + 0x7e, 4);
            M.I16(s + 4, limit);
            M.U32(s + 0x88, 0);
            E8_RingFill(s, 0, segByte);
        }

        static readonly float[] RingHeights3_b848e0 = { 4.0f, 3.0f, 2.0f };
        static readonly float[] CloudRadius_b848e0 = { 1.5f, 3.5f, 5.5f, 7.5f };
        static readonly short[] CloudStart_b848e0 = { 0, 0x5a, 0xb4, 0x10e };
        static readonly float[] CloudHeight_b848e0 = { 9.0f, 7.5f, 6.0f, 4.5f };

        /// <summary>
        /// CRagEffect::fn_b848e0 (0xb848e0) — Warp Portal opening (<paramref name="kind"/> 3 = red variant):
        /// tick 0 a prim 0x3c ground circle (magic_violet / ring_red, three rings of radius 50 at heights 4/3/2 with
        /// random start angles, alpha limit 1400), tick 1 (after snapping to the owner) a prim 0x3f of rising
        /// cylinders (ring_blue / ring_red, alpha limit 3111, block 3 mode = kind), tick 2 a prim 0x3e of four cloud
        /// rings (cloud11.tga, radius 1.5/3.5/5.5/7.5, segment dwords 1.5).
        /// </summary>
        public void Fn_b848e0(FxRuntime rt, sbyte kind)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                Prim p;
                if (kind == 3)
                {
                    p = E8_LaunchRing(rt, 0x3c, "effect\\ring_red.tga");
                    p.f_318 = 10.0f;
                }
                else
                {
                    p = E8_LaunchRing(rt, 0x3c, "effect\\magic_violet.tga");
                    p.f_318 = 4.0f;
                }
                float[] height = RingHeights3_b848e0;
                for (int i = 0; i < 3; i++)
                {
                    uint s = E8_RingBlock(p, i);
                    M.U8(s, 1);
                    M.I32(s + 0xc, 0);
                    M.I16(s + 6, 0x168);
                    M.F32(s + 8, 50.0f);
                    M.I16(s + 0x10, E8_RandAngle(rt));
                    M.I16(s + 2, 0);
                    M.F32(s + 0x80, height[i]);
                    M.I16(s + 0x7e, 0x5a);
                    M.I16(s + 4, 0x578);
                    M.U32(s + 0x88, 0);
                    E8_RingFill(s, 0, 1);
                }
                M.U8(E8_RingBlock(p, 3), 0);
                t = m_stateCnt;
            }
            if (t == 1)
            {
                FollowMaster();
                Prim p;
                if (kind == 3)
                {
                    p = E8_LaunchRing(rt, 0x3f, "effect\\ring_red.tga");
                    p.f_318 = 10.0f;
                }
                else
                {
                    p = E8_LaunchRing(rt, 0x3f, "effect\\ring_blue.tga");
                    p.f_318 = 4.0f;
                }
                E8_WarpCylinders(p, 0, kind == 3 ? -0x14 : -10, kind == 3 ? -0x28 : -0x14, 0xc27, 1);
                M.U8(E8_RingBlock(p, 3), 0);
                M.I32(E8_RingBlock(p, 3) + 0xc, 0);
                M.I16(E8_RingBlock(p, 3) + 4, kind);
                t = m_stateCnt;
            }
            if (t == 2)
            {
                Prim p = E8_LaunchRing(rt, 0x3e, "effect\\cloud11.tga");
                p.f_318 = kind == 3 ? 10.0f : 4.0f;
                float[] radius = CloudRadius_b848e0;
                short[] start = CloudStart_b848e0;
                float[] height = CloudHeight_b848e0;
                for (int i = 0; i < 4; i++)
                {
                    uint s = E8_RingBlock(p, i);
                    M.U8(s, 1);
                    M.I32(s + 0xc, 0);
                    M.I16(s + 6, 0x1e);
                    M.F32(s + 8, radius[i]);
                    M.I16(s + 0x10, start[i]);
                    M.I16(s + 2, 0);
                    M.F32(s + 0x80, height[i]);
                    M.I16(s + 0x7e, 0x5a);
                    M.I16(s + 4, 0);
                    E8_RingFill(s, 0x3fc00000, 0);
                }
            }
        }
    }
}
