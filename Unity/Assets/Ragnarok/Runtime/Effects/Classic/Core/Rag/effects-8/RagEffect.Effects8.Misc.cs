// Synced from reference/port/src/RoClassicFx/Rag/effects-8/RagEffect.Effects8.Misc.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>effects-8: Warm (Sun/Moon/Star), Shield Chain and Gravitation Field launchers.</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 418 (0x1a2) Sun/Moon/Star Warm — fn_b707d0(2, 2): two slots of rising "super" sprites, colour kind 2.</summary>
        [FxEffectProcess(0x1a2)]
        public FxCaseResult Case_E8_StarWarm(FxRuntime rt) { Fn_b707d0(rt, 2, 2); return FxCaseResult.Continue; }

        static readonly uint[] SlotHeightOff_b707d0 = { 0x488, 0x470, 0x494, 0x47c };

        /// <summary>
        /// CRagEffect::fn_b707d0 (0xb707d0) — follows the owner; at tick 0 a prim 0x63 (flags 5, textures
        /// super1..super5.bmp, f_318 = 0) with <paramref name="count"/> active slots of four sub-sprites: size 2,
        /// sub-sprite j gets life 10, alpha 40·(j + 1), a random texture 0..4, a random angle 0..359 and a random
        /// height −(0..9) (at +0x488/+0x470/+0x494/+0x47c); slot byte +0x6c = <paramref name="kind"/> (the colour
        /// scheme of the render, fn_afbec0).
        /// </summary>
        public void Fn_b707d0(FxRuntime rt, sbyte count, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 99, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 5;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture("effect\\super1.bmp"));
            M.U32((uint)p.f_144 + 4, rt.GetTexture("effect\\super2.bmp"));
            M.U32((uint)p.f_144 + 8, rt.GetTexture("effect\\super3.bmp"));
            M.U32((uint)p.f_144 + 0xc, rt.GetTexture("effect\\super4.bmp"));
            M.U32((uint)p.f_144 + 0x10, rt.GetTexture("effect\\super5.bmp"));
            p.f_318 = 0f;
            M.U8(p.A + 0x3e8, 0);
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            int n = count;
            uint[] height = SlotHeightOff_b707d0;
            for (uint s = p.A; n > 0; n--, s += 0xb4)
            {
                M.I32(s + 0x3f4, 0);
                M.U8(s + 0x3e8, 1);
                M.U32(s + 0x468, 0x40000000);   // 2.0
                for (uint j = 0; j < 4; j++)
                {
                    uint e = s + j * 0x10;
                    M.F32(s + height[j], (float)-RandMod(rt, 10));
                    M.F32(e + 0x3fc, 10.0f);
                    M.F32(e + 0x400, 40.0f * (j + 1));
                    M.F32(e + 0x404, (float)RandMod(rt, 5));
                    M.F32(e + 0x408, (float)RandMod(rt, 0x168));
                }
                M.I8(s + 0x454, kind);
            }
        }

        // ---- EF 520 Shield Chain ---------------------------------------------------------------------------------

        /// <summary>EF 520 (0x208, vf15) Shield Chain — fn_b8a560("effect\shield_boomerang.bmp"); returns true.</summary>
        [FxEffectProcess(0x208)]
        public FxCaseResult Case_E8_ShieldChain(FxRuntime rt) { Fn_b8a560(rt, "effect\\shield_boomerang.bmp"); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b8a560 (0xb8a560) — follows the owner; tick 0 plays cru_shield boomerang.wav; ticks 0, 3, 6,
        /// 9 and 12 each launch a prim 0x74 (flags 5, one texture): a spinning shield starting 8 below the owner and
        /// 2.5·15 behind its random heading (f_3f8 = rand % 360, speed f_400 = 2.5), size 5, alpha step 25, plus
        /// three trail blocks (enabled, alpha 0).
        /// </summary>
        public void Fn_b8a560(FxRuntime rt, string texture)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t == 0)
                rt.PlaySound3DRelative("effect\\cru_shield boomerang.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            else if (t != 3 && t != 6 && t != 9 && t != 0xc)
                return;
            Prim p = LaunchEffectPrim(rt, 0x74, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            uint a = p.A;
            M.F32(a + 0x318, 0f);
            M.U8(a + 0x3e8, 1);
            M.I32(a + 0x3f4, 0);
            M.F32(a + 0x46c, m_pos_x);
            M.F32(a + 0x474, m_pos_z);
            M.F32(a + 0x470, m_pos_y - 8.0f);
            M.F32(a + 0x468, 5.0f);
            M.F32(a + 0x41c, 0f);
            M.I16(a + 0x3ea, 0x19);
            M.I16(a + 0x3f8, E8_RandAngle(rt));
            M.F32(a + 0x400, 2.5f);
            short ang = M.I16(a + 0x3f8);
            M.F32(a + 0x46c, M.F32(a + 0x46c) - M.F32(a + 0x400) * 15.0f * FxMath.CosTableAt(ang));
            ang = M.I16(a + 0x3f8);
            M.F32(a + 0x474, M.F32(a + 0x474) - M.F32(a + 0x400) * 15.0f * FxMath.SinTableAt(ang));
            M.I16(a + 0x3ee, 0);
            M.U8(a + 0x450, 0);
            M.U8(a + 0x452, 0);
            M.U8(a + 0x49c, 1);
            M.I16(a + 0x49e, 0);
            M.U8(a + 0x550, 1);
            M.I16(a + 0x552, 0);
            M.U8(a + 0x604, 1);
            M.I16(a + 0x606, 0);
        }

        // ---- EF 522 Gravitation Field ----------------------------------------------------------------------------

        /// <summary>EF 522 (0x20a, vf15) Gravitation Field — fn_b79cf0(); returns true.</summary>
        [FxEffectProcess(0x20a)]
        public FxCaseResult Case_E8_GravitationField(FxRuntime rt) { Fn_b79cf0(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b79cf0 (0xb79cf0) — Gravitation Field: at tick 0 a screen quake (CGameMode msg 0x7a) and
        /// 5 × 5 × 4 debris prims 0x75 on a 5-unit grid (−10..10) around the effect: 10 up, duration 250 (fade from
        /// 240), size (300 + rand % 50) / 100, random yaw, pitch 60..99 (matrix X(pitch)·Y(yaw) copied to +0x190),
        /// speed 1.0 + 0.01 (+0x2bc/+0x2c0), reset to −1.2 / 0 at tick 12 (+0x200), alpha 20 fading at +0x1d4 = 15;
        /// textures stone.bmp (first two per cell) and ice.tga. Two extra rand() calls per prim (an unused angle).
        /// </summary>
        public void Fn_b79cf0(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
            for (int z = -10; z < 0xf; z += 5)
                for (int x = -10; x < 0xf; x += 5)
                    for (uint i = 0; i < 4; i++)
                    {
                        Prim p = LaunchEffectPrim(rt, 0x75, 0f, 0f, 0f);
                        m_lastPrim = p;
                        p.f_1dc = 1;
                        RandMod(rt, 0x168);          // the angle for GetSin/GetCos (results unused)
                        rt.Rand.Next();
                        uint a = p.A;
                        M.F32(a + 0x398, 10.0f);
                        M.F32(a + 0x394, (float)x);
                        M.F32(a + 0x39c, (float)z);
                        p.m_duration = 0xfa;
                        M.F32(a + 0x318, (float)(RandMod(rt, 0x32) + 300) / 100.0f);
                        M.F32(a + 0x360, 18.0f);
                        M.F32(a + 0x30c, (float)RandMod(rt, 0x168));
                        M.F32(a + 0x310, (float)(RandMod(rt, 0x28) + 0x3c));
                        FxMatrix.MakeRotationX(M, a + 0x160, FxMath.DegToRad(FxOps.Cvtt(M.F32(a + 0x310))));
                        FxMatrix.RotateY(M, a + 0x160, FxMath.DegToRad(FxOps.Cvtt(M.F32(a + 0x30c))));
                        for (uint o = 0; o < 0x30; o += 4) M.U32(a + 0x190 + o, M.U32(a + 0x160 + o));
                        M.F32(a + 0x2bc, 1.0f);
                        M.U32(a + 0x2c0, 0x3c23d70a);   // 0.01
                        M.F32(a + 0x324, 20.0f);
                        M.I32(a + 0x1d4, 0xf);
                        M.I32(a + 0x200, 0xc);
                        M.U32(a + 0x29c, 0xbf99999a);   // -1.2
                        M.F32(a + 0x2a0, 0f);
                        p.f_1f0 = p.m_duration - 10;
                        p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                        M.U32((uint)p.f_144, rt.GetTexture(i < 2 ? "effect\\stone.bmp" : "effect\\ice.tga"));
                    }
        }
    }
}
