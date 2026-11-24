// Synced from reference/port/src/RoClassicFx/Rag/effects-3/RagEffect.Effects3.Batch2.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>0xffc184 / 0xffc1a0: CP949 "이펙트\\데모스트레이션.spr" / ".act" (Bomb) as byte strings.</summary>
        internal const string E3BombSpr = "ÀÌÆÑÆ®\\µ¥¸ó½ºÆ®·¹ÀÌ¼Ç.spr";
        internal const string E3BombAct = "ÀÌÆÑÆ®\\µ¥¸ó½ºÆ®·¹ÀÌ¼Ç.act";

        /// <summary>
        /// The Jupitel Thunder ball set-up shared by both prims of fn_b65700: faces the target (SetDirectionTo on the prim),
        /// yaw = −dir, prim dir = 180 − dir (wrapped), speed = −distance / duration, MakeRotationY(yaw).
        /// </summary>
        void E3JupitelAim(FxRuntime rt)
        {
            rt.FaceTowards(m_lastPrim.A, f_180, f_188);
            Prim p = m_lastPrim;
            p.f_30c = -p.f_4c;
            p = m_lastPrim;
            p.f_4c = 180.0f - p.f_4c;
            p = m_lastPrim;
            if (0.0f > p.f_4c)
            {
                p.f_4c = p.f_4c + 360.0f;
                p = m_lastPrim;
            }
            float dist = rt.DistanceXZ(p.A, f_180, f_188);
            float q = dist / (float)p.m_duration;
            p.f_2bc = -q;
            p = m_lastPrim;
            FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
        }

        /// <summary>
        /// CRagEffect::fn_b65700 (0xb65700) — EF 93 (Jupitel Thunder): at tick 0 the sound and two type-0xf prims flying
        /// from the caster to the target (+0x180/+0x188): a flat glow (thunder_center.bmp / twirl_soft.bmp) and an
        /// animated ball (6 textures).
        /// </summary>
        public void Fn_b65700(FxRuntime rt, byte variant)
        {
            if (m_stateCnt != 0) return;
            E3Sound(rt, "effect\\hunter_shockwavetrap.wav", 0.0f);
            if (m_stateCnt != 0) return;

            Prim p = LaunchEffectPrim(rt, 0xf, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0x14;
            { Prim lp_ = m_lastPrim; lp_.f_1dc |= 0x40; }
            { Prim lp_ = m_lastPrim; lp_.f_1dc |= 0x400; }
            { Prim lp_ = m_lastPrim; lp_.f_1c4 |= 2; }
            { Prim lp_ = m_lastPrim; lp_.f_354 = 3.5f; }
            { Prim lp_ = m_lastPrim; lp_.f_360 = 3.5f; }
            E3JupitelAim(rt);
            { Prim lp_ = m_lastPrim; lp_.f_398 = -5.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_324 = 170.0f; }
            E3SetTexture(rt, m_lastPrim, variant == 0 ? "effect\\thunder_center.bmp" : "effect\\twirl_soft.bmp");

            p = LaunchEffectPrim(rt, 0xf, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0x14;
            { Prim lp_ = m_lastPrim; lp_.f_1dc = 0x40; }
            { Prim lp_ = m_lastPrim; lp_.f_1dc |= 0x400; }
            { Prim lp_ = m_lastPrim; lp_.f_1c4 |= 2; }
            { Prim lp_ = m_lastPrim; lp_.f_254 = 1; }
            { Prim lp_ = m_lastPrim; lp_.f_354 = 4.5f; }
            { Prim lp_ = m_lastPrim; lp_.f_360 = 4.5f; }
            E3JupitelAim(rt);
            { Prim lp_ = m_lastPrim; lp_.f_398 = -5.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_1e8 = 1; }
            { Prim lp_ = m_lastPrim; lp_.f_204 = 6; }
            { Prim lp_ = m_lastPrim; lp_.f_144 = (int)rt.NewArray((ulong)(uint)m_lastPrim.f_204 * 4); }
            string[] names = variant == 0
                ? new[] { "effect\\thunder_ball_a.bmp", "effect\\thunder_ball_b.bmp", "effect\\thunder_ball_c.bmp", "effect\\thunder_ball_d.bmp", "effect\\thunder_ball_e.bmp" }
                : new[] { "effect\\twirl_soft.bmp", "effect\\thunder_ball_b.bmp", "effect\\twirl_soft.bmp", "effect\\thunder_ball_d.bmp", "effect\\twirl_soft.bmp" };
            for (uint i = 0; i < 5; i++)
            {
                uint tex = rt.GetTexture(names[i]);
                M.U32((uint)m_lastPrim.f_144 + i * 4, tex);
            }
            uint last = rt.GetTexture("effect\\thunder_ball_f.bmp");
            M.U32((uint)m_lastPrim.f_144 + 0x14, last);
        }

        /// <summary>
        /// CRagEffect::fn_b6aad0 (0xb6aad0) — EF 242 / 404 (Magnetic Earth, Fiber Lock): follows the master; at tick 0 one
        /// ground decal (prim 0x27) for the whole effect, size/alpha/colour by <paramref name="kind"/>.
        /// </summary>
        public void Fn_b6aad0(FxRuntime rt, string texture, sbyte kind)
        {
            E3FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x27, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_3e8 = 1; }
            { Prim lp_ = m_lastPrim; lp_.f_49c = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_550 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_604 = 0; }
            p = m_lastPrim;
            float size;
            if (kind == 2) { size = 12.0f; goto Default; }
            if (kind == 3)
            {
                p.f_468 = 10.0f;
                M.U16(m_lastPrim.A + 0x3f8, 0xe1);
                M.U16(m_lastPrim.A + 0x3ea, 0xfa);
                { Prim lp_ = m_lastPrim; lp_.f_318 = 111.0f; }
                goto Rand;
            }
            if (kind == 4)
            {
                p.f_468 = 12.0f;
                M.U16(m_lastPrim.A + 0x3f8, 0);
                M.U16(m_lastPrim.A + 0x3ea, 200);
                { Prim lp_ = m_lastPrim; lp_.f_318 = 4.0f; }
                goto Rand;
            }
            if (kind == 10) p.f_468 = 30.0f;
            else if (kind == 11) p.f_468 = 28.0f;
            else if (kind == 12) p.f_468 = 20.0f;
            else { size = 7.0f; goto Default; }
            M.U16(m_lastPrim.A + 0x3f8, 0);
            M.U16(m_lastPrim.A + 0x3ea, 200);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 10.0f; }
            goto Rand;
        Default:
            p.f_468 = size;
            M.U16(m_lastPrim.A + 0x3f8, 0);
            M.U16(m_lastPrim.A + 0x3ea, 100);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 5.0f; }
        Rand:
            int r = rt.Rand.Next() % 0x168;
            M.U16(m_lastPrim.A + 0x466, (ushort)(r >= 0 ? r : 0));
            { Prim lp_ = m_lastPrim; lp_.f_3fc = -2.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_40c = (float)kind; }
            if (kind == 10 || kind == 11 || kind == 12) M.U16(m_lastPrim.A + 0x466, 0x168);
        }

        /// <summary>
        /// CRagEffect::fn_b70650 (0xb70650) — EF 302 (Bomb): at tick 0 one sprite prim (0x33, the Demonstration
        /// spr/act) at the master, action 0.
        /// </summary>
        public void Fn_b70650(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            E3FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x33, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = f_17c;
            { Prim lp_ = m_lastPrim; lp_.f_3e8 = 1; }
            { Prim lp_ = m_lastPrim; lp_.f_3f4 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_3f0 = 0.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_318 = 1.2f; }
            p = m_lastPrim;
            p.f_484 = m_pos_x;
            p.f_488 = m_pos_y;
            p.f_48c = m_pos_z;
            { Prim lp_ = m_lastPrim; lp_.f_488 = m_lastPrim.f_488 - 1.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_454 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_324 = 200.0f; }
            rt.SetSprName(m_lastPrim.A, E3BombSpr);
            rt.SetActName(m_lastPrim.A, E3BombAct);
            { Prim lp_ = m_lastPrim; lp_.f_1e8 = 4; }
            rt.SetAction(m_lastPrim.A, 0, 0x10, 0);
            m_lastPrim.SnapshotTrail_ad58d0();
            { Prim lp_ = m_lastPrim; lp_.f_49c = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_550 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_604 = 0; }
        }

        /// <summary>
        /// CRagEffect::fn_b77370 (0xb77370) — EF 514 (Ki Explosion): follows the master; at tick 0 one prim 0x71
        /// with four ring layers (0xb4-byte blocks from +0x3e8): radius 20 − i/2, random phase, trail slots cleared.
        /// </summary>
        public void Fn_b77370(FxRuntime rt, string texture)
        {
            E3FollowMaster();
            if (m_stateCnt != 0) return;
            E3FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x71, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            E3SetTexture(rt, m_lastPrim, texture);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 4.0f; }
            int layer = 0;
            for (uint blk = 0; blk < 0x2d0; blk += 0xb4, layer++)
            {
                M.U8(m_lastPrim.A + blk + 0x3e8, 1);
                M.U32(m_lastPrim.A + blk + 0x3f4, 0);
                M.U16(m_lastPrim.A + blk + 0x3ee, 0x13b);
                M.F32(m_lastPrim.A + blk + 0x3f0, 15.0f);
                int r = rt.Rand.Next() % 0x168;
                M.U16(m_lastPrim.A + blk + 0x3f8, (ushort)(r >= 0 ? r : 0));
                M.U16(m_lastPrim.A + blk + 0x3ea, 0);
                M.F32(m_lastPrim.A + blk + 0x468, 20.0f - (float)layer * 0.5f);
                M.U16(m_lastPrim.A + blk + 0x466, 0x32);
                M.U32(m_lastPrim.A + blk + 0x470, 0);
                for (uint k = 0; k < 3; k++)
                {
                    uint d = m_lastPrim.A + blk + 0x3fc + k * 0x1c;
                    uint b = m_lastPrim.A + blk + 0x450 + k * 7;
                    for (uint j = 0; j < 7; j++)
                    {
                        M.U32(d + j * 4, 0);
                        M.U8(b + j, 0);
                    }
                }
            }
        }

        /// <summary>
        /// CRagEffect::fn_b7a4b0 (0xb7a4b0) — EF 336 (Guard, Parrying): follows the master; at tick 0 the kyrie sound and a
        /// shield prim (0x3d, two textures: <paramref name="texture"/> + guardK2.tga), variant byte +0x450.
        /// </summary>
        public void Fn_b7a4b0(FxRuntime rt, string texture, sbyte variant)
        {
            E3FollowMaster();
            if (m_stateCnt != 0) return;
            E3Sound(rt, "effect\\kyrie_guard.wav", 0.0f);
            Prim p = LaunchEffectPrim(rt, 0x3d, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp_ = m_lastPrim; lp_.m_duration = f_17c; }
            { Prim lp_ = m_lastPrim; lp_.f_204 = 2; }
            { Prim lp_ = m_lastPrim; lp_.f_144 = (int)rt.NewArray((ulong)(uint)m_lastPrim.f_204 * 4); }
            uint t0 = rt.GetTexture(texture);
            M.U32((uint)m_lastPrim.f_144, t0);
            uint t1 = rt.GetTexture("effect\\guardK2.tga");
            M.U32((uint)m_lastPrim.f_144 + 4, t1);
            { Prim lp_ = m_lastPrim; lp_.f_318 = 11.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_604 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_550 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_49c = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_3e8 = 1; }
            { Prim lp_ = m_lastPrim; lp_.f_3f4 = 0; }
            { Prim lp_ = m_lastPrim; lp_.f_3f0 = 7.8f; }
            M.U16(m_lastPrim.A + 0x3ea, 0);
            { Prim lp_ = m_lastPrim; lp_.f_468 = 1.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_3fc = 0.0f; }
            p = m_lastPrim;
            if (variant == 2) { p.f_450 = 2; return; }
            if (variant == 4) { p.f_450 = 4; return; }
            if (variant == 5) { p.f_450 = 5; return; }
            p.f_450 = 0;
            if (variant == 3) { Prim lp_ = m_lastPrim; lp_.f_450 = 3; }
        }

        /// <summary>CRagEffect::fn_b8e3a0 (0xb8e3a0) — EF 295/296: at tick 0 mode message 0xa5 (screen flash) with <paramref name="kind"/>.</summary>
        public void Fn_b8e3a0(FxRuntime rt, sbyte kind)
        {
            if (m_stateCnt == 0) rt.GameModeSendMsg(0xa5, kind, 0, 0, 0);
        }
    }
}
