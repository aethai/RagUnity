// Synced from reference/port/src/RoClassicFx/Rag/effects-10/RagEffect.Effects10.Notify.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>Sprite names of fn_b8bf60 (angel / feathers; 이팩트\천사, 천사날개, 깃털1..3, 한복천사, 한복천사(날개)).</summary>
    public static class E10AngelNames
    {
        const string Efx = "ÀÌÆÑÆ®\\";   // 이팩트\
        public const string Angel = Efx + "Ãµ»ç";
        public const string AngelWings = Efx + "Ãµ»ç³¯°³";
        public const string Feather1 = Efx + "±êÅÐ1", Feather2 = Efx + "±êÅÐ2", Feather3 = Efx + "±êÅÐ3";
        public const string HanbokAngel = Efx + "ÇÑº¹Ãµ»ç";
        public const string HanbokAngelWings = Efx + "ÇÑº¹Ãµ»ç(³¯°³)";

        /// <summary>(game.exe address, value) of the .spr / .act names (checked by the tests).</summary>
        public static readonly (uint Address, string Value)[] Table =
        {
            (0x00ffb4a4, Angel + ".spr"), (0x00ffb4b4, Angel + ".act"), (0x00ffb4c4, AngelWings + ".spr"), (0x00ffb4d8, AngelWings + ".act"),
            (0x00ffb4ec, Feather1 + ".spr"), (0x00ffb500, Feather1 + ".act"), (0x00ffb514, Feather2 + ".spr"), (0x00ffb528, Feather2 + ".act"),
            (0x00ffb53c, Feather3 + ".spr"), (0x00ffb550, Feather3 + ".act"), (0x00ffb564, HanbokAngel + ".spr"), (0x00ffb578, HanbokAngel + ".act"),
            (0x00ffb58c, HanbokAngelWings + ".spr"), (0x00ffb5a8, HanbokAngelWings + ".act"),
            (0x00ff3940, E10AngelNames.WavInhale),
        };

        /// <summary>0xff3940 effect\흡기.wav (fn_b991f0 kind 1).</summary>
        public const string WavInhale = "effect\\Èí±â.wav";
    }

    public readonly partial struct RagEffect
    {
        // ---- ZC_NOTIFY_EFFECT (0x19b) / NOTIFY_EFFECT2/3 ids ------------------------------------------------

        /// <summary>EF 331 (0x14b, vf14 case 0xb467a4): fn_b991f0(this, "effect\white02.bmp", 0) — rising sparkles, heal sound.</summary>
        [FxEffectProcess(0x14b)]
        public FxCaseResult Case_E10_331(FxRuntime rt) { Fn_b991f0(rt, "effect\\white02.bmp", 0); return FxCaseResult.Continue; }

        /// <summary>EF 332 (0x14c, vf14 case 0xb467b9): fn_b991f0(this, "effect\white02.bmp", 1) — the same, 흡기.wav.</summary>
        [FxEffectProcess(0x14c)]
        public FxCaseResult Case_E10_332(FxRuntime rt) { Fn_b991f0(rt, "effect\\white02.bmp", 1); return FxCaseResult.Continue; }

        /// <summary>EF 337 (0x151, vf14 case 0xb46811): fn_b7ebb0(0) then fn_b7ebb0(0x2d); vf14 returns 1 (the STR result is ignored).</summary>
        [FxEffectProcess(0x151)]
        public FxCaseResult Case_E10_337(FxRuntime rt)
        {
            Fn_b7ebb0(rt, 0);
            Fn_b7ebb0(rt, 0x2d);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 338 (0x152, vf14 case 0xb4682a): fn_b8bf60(0..3, 0) — the angel, its wings and two feathers.</summary>
        [FxEffectProcess(0x152)]
        public FxCaseResult Case_E10_338(FxRuntime rt)
        {
            Fn_b8bf60(rt, 0);
            Fn_b8bf60(rt, 1);
            Fn_b8bf60(rt, 2);
            Fn_b8bf60(rt, 3);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 582 (0x246, vf15 case 0xb4c218): fn_b8bf60(0xb, 0) then fn_b8bf60(0xa, 0) — the hanbok angel.</summary>
        [FxEffectProcess(0x246)]
        public FxCaseResult Case_E10_582(FxRuntime rt)
        {
            Fn_b8bf60(rt, 0xb);
            Fn_b8bf60(rt, 0xa);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 752 (0x2f0, vf15 case 0xb4bdaa): fn_b81680 — Mirror-Image-like after-images in (225, 225, 255).</summary>
        [FxEffectProcess(0x2f0)]
        public FxCaseResult Case_E10_752(FxRuntime rt) { Fn_b81680(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// EF 864 (0x360, vf15 case 0xb48e72): ticks 4..90 blink the owner (even: flag 1 + ARGB(−1, 0xb6, 0x59, 0xff),
        /// odd: white), fn_b99990(2) (effects-9) and fn_b966c0(6) every tick.
        /// </summary>
        [FxEffectProcess(0x360)]
        public FxCaseResult Case_E10_864(FxRuntime rt)
        {
            int t = m_stateCnt;
            if ((uint)(t - 4) <= 0x56)
            {
                uint master = M.U32(A + 0x138);
                if ((t & 1) == 0)
                {
                    uint flags = M.U32(master + 0xe8);
                    if ((flags & 1) == 0)
                    {
                        M.U32(master + 0xe8, flags + 1);
                        master = M.U32(A + 0x138);
                    }
                    rt.SetArgb(master, -1, 0xb6, 0x59, 0xff);
                }
                else rt.SetArgb(master, -1, 0xff, 0xff, 0xff);
            }
            Fn_b99990(rt, 2);
            Fn_b966c0(rt, 6);
            return FxCaseResult.Continue;
        }

        // ---- helpers ------------------------------------------------------------------------------------------

        /// <summary>
        /// CRagEffect::fn_b81680 (0xb81680) — the effect position is the after-image colour (225, 225, 255); the owner
        /// is tinted (−1, 0xe1, 0xe1, 0xff) every tick; counter 9998 wraps to 0; every 10th tick a CBlurPC clone
        /// (0x508, ctor 0xcbda20) gets SendMsg(0, colour) and joins the world render list.
        /// </summary>
        public void Fn_b81680(FxRuntime rt)
        {
            m_pos_x = 225.0f;
            m_pos_z = 255.0f;
            m_pos_y = 225.0f;
            rt.SetArgb(M.U32(A + 0x138), -1, 0xe1, 0xe1, 0xff);
            int t = m_stateCnt;
            if (t == 0x270e) m_stateCnt = 0;
            else if (t % 10 != 0) return;
            uint blur = M.Alloc(0x508);
            if (blur != 0) rt.ConstructBlurPc_cbda20(blur, M.U32(A + 0x138));
            long x = FxOps.Cvtt(m_pos_x), y = FxOps.Cvtt(m_pos_y), z = FxOps.Cvtt(m_pos_z);
            rt.BlurPcSendMsg_cc13a0(blur, A, 0, 0, x, y, z);
            rt.ListPushBack(M.U32(rt.Mode + 0xcc) + 0x8, blur);
        }

        /// <summary>
        /// CRagEffect::fn_b7ebb0 (0xb7ebb0, thiscall (angle), ret 4) — follows the owner; at tick 5 one prim 0x4b
        /// (magic_green.tga, style 9) with four slots at angle + 0/90/180/270; with angle 0 it also runs the STR
        /// player at (0, −80, 0) and returns its result (1 otherwise).
        /// </summary>
        public bool Fn_b7ebb0(FxRuntime rt, int angle)
        {
            FollowMaster();
            if (m_stateCnt == 5)
            {
                Prim p = LaunchEffectPrim(rt, 0x4b, 0f, 0f, 0f);
                m_lastPrim = p;
                p.f_1c4 = 5;
                { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
                { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
                uint tex = rt.GetTexture("effect\\magic_green.tga");
                M.U32((uint)m_lastPrim.f_144, tex);
                M.F32(m_lastPrim.A + 0x318, 9.0f);
                RingSlots_b7ebb0(angle);
            }
            if (angle != 0) return true;
            return rt.StrPlayer(this, 0f, -80.0f, 0f);
        }

        /// <summary>The four-slot ring block of fn_b7ebb0 / fn_b8bf60: slot k on, age 0, +0x3f0 = 2, angle +0x3f8 = 90k + base, alpha 0, size 2, +0x3fc = 1, +0x450 = 0.</summary>
        void RingSlots_b7ebb0(int baseAngle)
        {
            uint a = m_lastPrim.A;
            for (int k = 0; k < 4; k++)
            {
                uint o = a + (uint)k * 0xb4;
                M.U8(o + 0x3e8, 1);
                M.I32(o + 0x3f4, 0);
                M.F32(o + 0x3f0, 2.0f);
                M.I16(o + 0x3f8, (short)(k * 0x5a + baseAngle));
                M.U16(o + 0x3ea, 0);
                M.F32(o + 0x468, 2.0f);
                M.F32(o + 0x3fc, 1.0f);
                M.U8(o + 0x450, 0);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b966c0 (0xb966c0, thiscall (unused), ret 4) — like fn_b99990: owner screen position into
        /// f_154/f_158, follows the owner; on even ticks one prim 0x10 (ac_center2.tga, 50 ticks) 3..9 units out at a
        /// random heading, 20 lower, sinking (f_2c0 = +0.015), each colour channel 0xff or 0x8c (rand() % 3 != 0).
        /// </summary>
        public void Fn_b966c0(FxRuntime rt, int unused)
        {
            ProjectMasterToScreen(rt);
            FollowMaster();
            if ((m_stateCnt & 1) != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x10, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0x32;
            float heading = FxMath.DegToRad(RandMod(rt, 0x168));
            FxMatrix.MakeRotationY(M, p.A + 0x160, heading);
            p = m_lastPrim;
            float forward = (float)(RandMod(rt, 7) + 3);
            RotatedOffset(p, forward, 0x394);
            p = m_lastPrim;
            p.f_398 = p.f_398 - 20.0f;
            p = m_lastPrim;
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(-0x5a));
            p.f_2c0 = 0.015f;
            p.f_354 = (float)(RandMod(rt, 0x3c) + 0x1e) / 10.0f;
            p.f_360 = 0.18f;
            p.f_324 = 0f;
            p.f_2d4 = 200.0f;
            M.I32(p.A + 0x23c, rt.Rand.Next() % 3 == 0 ? 0xff : 0x8c);
            p.f_240 = rt.Rand.Next() % 3 == 0 ? 0xff : 0x8c;
            p.f_244 = rt.Rand.Next() % 3 == 0 ? 0xff : 0x8c;
            p.f_328 = p.f_2d4 / 20.0f;
            p.f_1f0 = p.m_duration - 0x14;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture("effect\\ac_center2.tga"));
            p.f_3e8 = 0;
        }

        /// <summary>
        /// CRagEffect::fn_b991f0 (0xb991f0, thiscall (texture, kind), ret 8) — follows the owner; tick 0 sound
        /// (_heal_effect.wav at y −40 for kind 0, 흡기.wav at y −80 for kind 1); on ticks 0, 4 .. 20 one prim 0x3b
        /// (pok1.tga + pok3.tga, or the texture twice for kind 2) with four sparkles around the owner: size 1.5..2.4
        /// (kind 2: 2.0..3.0 for the first two, the others off), offset ±3 (kind 2: ±9), height −6, speed 0.2..0.39,
        /// style +0x452 = 11 (kinds 0 / 2), 18 (kind 3) or 1.
        /// </summary>
        public void Fn_b991f0(FxRuntime rt, string texture, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt == 0)
            {
                if (kind == 0) rt.PlaySound3DRelative("_heal_effect.wav", m_pos_x, -40.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                else if (kind == 1) rt.PlaySound3DRelative(E10AngelNames.WavInhale, m_pos_x, -80.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            int t = m_stateCnt;
            if (t > 0x14 || (t & 3) != 0) return;   // (t & 0x80000003) fix-up: non-zero remainder for every t % 4 != 0
            Prim p = LaunchEffectPrim(rt, 0x3b, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
            { Prim lp = m_lastPrim; lp.f_204 = 2; }
            { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
            string second;
            if (kind == 2)
            {
                M.U32((uint)m_lastPrim.f_144, rt.GetTexture(texture));
                second = texture;
            }
            else
            {
                uint t0 = rt.GetTexture("effect\\pok1.tga");
                M.U32((uint)m_lastPrim.f_144, t0);
                second = "effect\\pok3.tga";
            }
            uint t1 = rt.GetTexture(second);
            M.U32((uint)m_lastPrim.f_144 + 4, t1);
            M.F32(m_lastPrim.A + 0x318, 0f);
            M.U32(m_lastPrim.A + 0x10, M.U32(A + 0x10));
            M.U32(m_lastPrim.A + 0x14, M.U32(A + 0x14));
            M.U32(m_lastPrim.A + 0x18, M.U32(A + 0x18));
            for (int k = 0; k < 4; k++)
            {
                uint o = m_lastPrim.A + (uint)k * 0xb4;
                M.U8(o + 0x3e8, 1);
                M.I32(o + 0x3f4, 0);
                float z;
                if (kind == 2)
                {
                    if (k >= 2) M.U8(m_lastPrim.A + (uint)k * 0xb4 + 0x3e8, 0);
                    else M.F32(m_lastPrim.A + (uint)k * 0xb4 + 0x468, (float)RandMod(rt, 0xb) * 0.1f + 2.0f);
                    float x = (float)RandMod(rt, 0x13) + m_pos_x;
                    M.F32(m_lastPrim.A + (uint)k * 0xb4 + 0x46c, x - 9.0f);
                    z = ((float)RandMod(rt, 0x13) + m_pos_z) - 9.0f;
                }
                else
                {
                    M.F32(m_lastPrim.A + (uint)k * 0xb4 + 0x468, (float)RandMod(rt, 10) * 0.1f + 1.5f);
                    float x = (float)RandMod(rt, 7) + m_pos_x;
                    M.F32(m_lastPrim.A + (uint)k * 0xb4 + 0x46c, x - 3.0f);
                    z = ((float)RandMod(rt, 7) + m_pos_z) - 3.0f;
                }
                o = m_lastPrim.A + (uint)k * 0xb4;
                M.F32(o + 0x474, z);
                M.U32(o + 0x41c, 0);
                M.U16(o + 0x3ea, 0);
                M.F32(o + 0x470, m_pos_y - 6.0f);
                M.U16(o + 0x3f8, 0);
                M.U32(o + 0x400, 0);
                M.F32(o + 0x404, (float)RandMod(rt, 0x14) * 0.01f + 0.2f);
                M.U16(o + 0x3ee, 0);
                M.U8(o + 0x450, 0);
                M.U8(o + 0x451, 1);
                if (kind == 0 || kind == 2) M.U8(o + 0x452, 0xb);
                else M.U8(o + 0x452, (byte)(kind == 3 ? 0x12 : 1));
                M.U32(o + 0x418, 0);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b8bf60 (0xb8bf60, thiscall (kind, unused), ret 8) — follows the owner; at tick 0 (kinds 1 / 11
        /// also every 3rd tick up to 18) one sprite prim 0x55 40 units above the owner (kinds 10 / 11: 30) with the
        /// kind's sprite (0 천사, 1 천사날개, 2..4 깃털1..3, 10 한복천사, 11 한복천사(날개)), SetAction(0, 16, 1); kinds 0 /
        /// 10 add two magic rings (prim 0x4b, ring_blue.tga, style 4) at ticks 65 and 66 (+45°). Returns the last prim
        /// (ignored by the cases).
        /// </summary>
        public void Fn_b8bf60(FxRuntime rt, sbyte kind)
        {
            FollowMaster();
            int t = m_stateCnt;
            bool launch = t == 0 || (t <= 0x12 && (kind == 1 || kind == 0xb) && t % 3 == 0);
            if (launch)
            {
                Prim p = LaunchEffectPrim(rt, 0x55, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = f_17c;
                uint a = m_lastPrim.A;
                M.I32(a + 0x3f4, 0);
                M.U8(a + 0x3e8, 1);
                M.F32(a + 0x318, 1.0f);
                M.U32(a + 0x46c, M.U32(A + 0x10));
                M.F32(a + 0x470, m_pos_y - 40.0f);
                M.U32(a + 0x474, M.U32(A + 0x18));
                M.U8(a + 0x450, (byte)kind);
                M.U8(a + 0x451, (byte)m_stateCnt);
                M.F32(a + 0x324, 0f);
                string name = null;
                switch ((int)kind)
                {
                    case 0: name = E10AngelNames.Angel; break;
                    case 1: name = E10AngelNames.AngelWings; break;
                    case 2: name = E10AngelNames.Feather1; break;
                    case 3: name = E10AngelNames.Feather2; break;
                    case 4: name = E10AngelNames.Feather3; break;
                }
                if (name != null)
                {
                    rt.SetSprName(a, name + ".spr");
                    rt.SetActName(m_lastPrim.A, name + ".act");
                    M.U32(m_lastPrim.A + 0x1c4, 5);
                }
                else if (kind == 0xa)
                {
                    rt.SetSprName(a, E10AngelNames.HanbokAngel + ".spr");
                    rt.SetActName(m_lastPrim.A, E10AngelNames.HanbokAngel + ".act");
                    M.U32(m_lastPrim.A + 0x1c4, 1);
                    M.F32(m_lastPrim.A + 0x470, m_pos_y - 30.0f);
                    M.U32(m_lastPrim.A + 0x1c4, M.U32(m_lastPrim.A + 0x1c4) | 0x200);
                }
                else if (kind == 0xb)
                {
                    rt.SetSprName(a, E10AngelNames.HanbokAngelWings + ".spr");
                    rt.SetActName(m_lastPrim.A, E10AngelNames.HanbokAngelWings + ".act");
                    M.U32(m_lastPrim.A + 0x1c4, 5);
                    M.F32(m_lastPrim.A + 0x470, m_pos_y - 30.0f);
                }
                rt.SetAction(m_lastPrim.A, 0, 0x10, 1);
                m_lastPrim.SnapshotTrail_ad58d0();
                a = m_lastPrim.A;
                M.U8(a + 0x254, 1);
                M.U32(a + 0x38, 0);
                M.U8(a + 0x49c, 0);
                M.U8(a + 0x550, 0);
                M.U8(a + 0x604, 0);
            }
            if (kind != 0 && kind != 0xa) return;
            t = m_stateCnt;
            if (t == 0x41)
            {
                LaunchRing_b8bf60(rt, 0);
                t = m_stateCnt;
            }
            if (t == 0x42) LaunchRing_b8bf60(rt, 0x2d);
        }

        /// <summary>fn_b8bf60's magic ring: prim 0x4b, flags 5, ring_blue.tga, style 4, four slots from <paramref name="baseAngle"/>.</summary>
        void LaunchRing_b8bf60(FxRuntime rt, int baseAngle)
        {
            Prim p = LaunchEffectPrim(rt, 0x4b, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
            { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
            uint tex = rt.GetTexture("effect\\ring_blue.tga");
            M.U32((uint)m_lastPrim.f_144, tex);
            M.F32(m_lastPrim.A + 0x318, 4.0f);
            RingSlots_b7ebb0(baseAngle);
        }
    }
}
