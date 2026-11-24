// Synced from reference/port/src/RoClassicFx/Rag/effects-9/RagEffect.Effects9.Helpers.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>Byte strings (CP949, one char per byte) used by effects-9, keyed by their game.exe address.</summary>
    public static class E9Names
    {
        /// <summary>0xff8628 effect\T_따뜻한마법.wav (Warm, EF 572).</summary>
        public const string WavWarm = "effect\\T_\u00b5\u00fb\u00b5\u00ed\u00c7\u00d1\u00b8\u00b6\u00b9\u00fd.wav";
        /// <summary>0xff85cc effect\피어싱샷.wav (Piercing Shot, EF 623).</summary>
        public const string WavPiercingShot = "effect\\\u00c7\u00c7\u00be\u00ee\u00bd\u00cc\u00bc\u00a6.wav";
        /// <summary>0xff85a0 effect\t_바람방출.wav (Kamaitachi, EF 624).</summary>
        public const string WavKamaitachi = "effect\\t_\u00b9\u00d9\u00b6\u00f7\u00b9\u00e6\u00c3\u00e2.wav";
        /// <summary>0xff85b8 effect\270바람.tga (Kamaitachi blade texture).</summary>
        public const string TexWind270 = "effect\\270\u00b9\u00d9\u00b6\u00f7.tga";
        /// <summary>0xff8948 effect\mon_금강불괴.wav (SKA, EF 1132).</summary>
        public const string WavSka = "effect\\mon_\u00b1\u00dd\u00b0\u00ad\u00ba\u00d2\u00b1\u00ab.wav";
        /// <summary>0xffb684 유저인터페이스\item\하얀허브.bmp (Twilight Alchemy 1).</summary>
        public const string TexWhiteHerb = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00c7\u00cf\u00be\u00e1\u00c7\u00e3\u00ba\u00ea.bmp";
        /// <summary>0xff87d8 유저인터페이스\item\화이트슬림포션.bmp (Twilight Alchemy 2).</summary>
        public const string TexWhiteSlimPotion = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00c8\u00ad\u00c0\u00cc\u00c6\u00ae\u00bd\u00bd\u00b8\u00b2\u00c6\u00f7\u00bc\u00c7.bmp";
        /// <summary>0xff62b4 유저인터페이스\item\화염병.bmp (Twilight Alchemy 3, one of three).</summary>
        public const string TexFireBottle = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00c8\u00ad\u00bf\u00b0\u00ba\u00b4.bmp";
        /// <summary>0xff62d4 유저인터페이스\item\염산병.bmp (Twilight Alchemy 3, one of three).</summary>
        public const string TexAcidBottle = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00bf\u00b0\u00bb\u00ea\u00ba\u00b4.bmp";
        /// <summary>0xffb6a8 유저인터페이스\item\알코올.bmp (Twilight Alchemy 3, one of three).</summary>
        public const string TexAlcohol = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00be\u00cb\u00c4\u00da\u00bf\u00c3.bmp";
    }

    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b70c60 (0xb70c60): follows the owner; at tick 0 launches one prim 0x52 (a textured column /
        /// ring around the owner) with <paramref name="texture"/>. <paramref name="kind"/> picks the size (+0x468),
        /// base height (+0x488, relative to the owner), style byte (+0x452) and start angle (+0x3ee; fixed or random);
        /// kinds 10..13/16..18/14/15 scale with f_13c when it is positive. +0x454 = kind (4 → 1).
        /// </summary>
        public void Fn_b70c60(FxRuntime rt, string texture, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x52, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            p.f_318 = 0f;
            p.f_3e8 = 0;
            p.f_49c = 0;
            p.f_550 = 0;
            p.f_604 = 0;
            p.f_3e8 = 1;
            p.f_3f4 = 0;

            short angle;
            int style;   // 0: fixed angle + keep f_424, 1: f_424 = 0 + fixed angle, 2: f_424 = 0 + random angle
            float y = m_pos_y;
            switch ((int)kind)
            {
                case 1:
                    p.f_468 = 8.0f; p.f_488 = y - 30.0f; p.f_452 = 0; p.f_424 = 1.0f;
                    angle = 0x78; style = 0; break;
                case 2:
                    p.f_468 = 10.0f; p.f_488 = y - 10.0f; p.f_452 = 2;
                    angle = 0x87; style = 1; break;
                case 3:
                    p.f_468 = 8.0f; p.f_488 = y - 30.0f; p.f_452 = 3; p.f_424 = 2.0f;
                    angle = 0x9b; style = 0; break;
                case 4:
                    p.f_468 = 6.0f; p.f_488 = y - 30.0f; p.f_452 = 0;
                    angle = 0x87; style = 1; break;
                case 10:
                case 11:
                    p.f_468 = Scaled13c(0.7f, 70.0f); p.f_488 = y; p.f_452 = 10;
                    angle = 0; style = 2; break;
                case 12:
                    p.f_468 = Scaled13c(1.2f, 120.0f); p.f_488 = y; p.f_452 = 12;
                    angle = 0; style = 2; break;
                case 13:
                    p.f_468 = Scaled13c(3.0f, 300.0f); p.f_488 = y; p.f_452 = 12;
                    angle = 0; style = 2; break;
                case 14:
                    p.f_468 = Raw13c(30.0f); p.f_488 = y; p.f_452 = 4;
                    angle = 0; style = 2; break;
                case 15:
                    p.f_468 = Raw13c(30.0f); p.f_488 = y; p.f_452 = 0xf;
                    angle = 0; style = 2; break;
                case 16:
                case 17:
                    p.f_468 = Scaled13c(0.9f, 9.5f);
                    if (kind == 0x11) p.f_468 = p.f_468 * 0.85f;
                    p.f_488 = y; p.f_452 = 5;
                    angle = 0; style = 2; break;
                case 18:
                    p.f_468 = Scaled13c(0.9f, 9.0f); p.f_488 = y; p.f_452 = 5;
                    angle = 0; style = 2; break;
                case 19:
                    p.f_468 = 8.0f; p.f_488 = y - 30.0f; p.f_452 = 0xf;
                    angle = 0; style = 2; break;
                case 20:
                    p.f_468 = 2.4f; p.f_488 = y - 27.0f; p.f_452 = 0;
                    angle = 0x87; style = 1; break;
                case 21:
                    p.f_468 = 24.0f; p.f_488 = y - 10.0f; p.f_452 = 4;
                    angle = 0x5a; style = 1; break;
                case 22:
                    p.f_468 = 4.0f;
                    p.f_488 = ActorTopOffset_b70c60(rt) + m_pos_y;
                    p.f_452 = 0x10;
                    angle = 0x87; style = 1; break;
                case 30:
                    p.f_468 = 24.0f; p.f_488 = y - 30.0f; p.f_452 = 0x13;
                    angle = 0x87; style = 1; break;
                case 31:
                    p.f_468 = 2.0f; p.f_488 = y - 30.0f; p.f_452 = 0;
                    angle = 0x87; style = 1; break;
                case 32:
                    p.f_468 = 8.0f; p.f_488 = y - 30.0f; p.f_452 = 0x20;
                    angle = 0; style = 2; break;
                default:
                    p.f_468 = 5.0f; p.f_488 = y - 10.0f; p.f_452 = 2;
                    angle = 0x87; style = 1; break;
            }
            if (style != 0) p.f_424 = 0f;
            if (style == 2) angle = (short)RandMod(rt, 0x168);

            p.f_3ee = angle;
            p.f_41c = 0f;
            p.f_3ea = 0;
            M.U32(p.A + 0x46c, M.U32(A + 0x10));
            M.U32(p.A + 0x474, M.U32(A + 0x18));
            p.f_3f8 = (short)RandMod(rt, 0x168);
            p.f_450 = 0;
            p.f_454 = kind != 4 ? kind : (sbyte)1;
            p.f_438 = m_pos_y - p.f_488;
        }

        /// <summary>fn_b70c60's "f_13c &gt; 0 ? f_13c * factor : fallback" (COMISS 0, f_13c; JB — NaN takes the product).</summary>
        float Scaled13c(float factor, float fallback)
        {
            float v = f_13c;
            return (0.0f < v || float.IsNaN(v)) ? v * factor : fallback;
        }

        /// <summary>fn_b70c60's "f_13c &gt; 0 ? f_13c : fallback" (NaN keeps f_13c).</summary>
        float Raw13c(float fallback)
        {
            float v = f_13c;
            return (0.0f < v || float.IsNaN(v)) ? v : fallback;
        }

        /// <summary>
        /// fn_b70c60 kind 22: owner ACT (FUN_00bd0720) → action 0 motion 0 (FUN_006c1790) → clip 0 (FUN_006c1670),
        /// clip +4 * 0.29 - 4; -30 when any of them is missing or the product is exactly 0 (UCOMISS/LAHF/JNP).
        /// </summary>
        float ActorTopOffset_b70c60(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            if (master == 0) return -30.0f;
            uint act = rt.GetActRes(master);
            if (act == 0) return -30.0f;
            uint motion = rt.ActGetMotion(act, 0, 0);
            if (motion == 0) return -30.0f;
            uint clip = rt.MotionGetClip(motion, 0);
            if (clip == 0) return -30.0f;
            float v = (float)M.I32(clip + 4) * 0.29f;
            if (v == 0.0f) return -30.0f;
            return v - 4.0f;
        }

        /// <summary>
        /// CRagEffect::fn_b8e7d0 (0xb8e7d0) — Twilight Alchemy: position = the LOCAL PLAYER (world +0x2c), not the owner;
        /// at tick 0 one prim 0x7b of 4 falling item icons (kind 0 white herb, 1 white slim potion, 2 random of fire
        /// bottle / acid bottle / alcohol; other kinds no texture) scattered ±80 around the player, starting 5..70 below
        /// the ground height (GetHeight 0x6c32a0) of their cell.
        /// </summary>
        public void Fn_b8e7d0(FxRuntime rt, sbyte kind)
        {
            uint player = M.U32(M.U32(rt.Mode + 0xcc) + 0x2c);
            M.U32(A + 0x10, M.U32(player + 0x10));
            M.U32(A + 0x14, M.U32(player + 0x14));
            M.U32(A + 0x18, M.U32(player + 0x18));
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x7b, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            string tex = null;
            if (kind == 0) tex = E9Names.TexWhiteHerb;
            else if (kind == 1) tex = E9Names.TexWhiteSlimPotion;
            else if (kind == 2)
            {
                int c = RandMod(rt, 3);
                tex = c == 0 ? E9Names.TexFireBottle : c == 1 ? E9Names.TexAcidBottle : E9Names.TexAlcohol;
            }
            if (tex != null) M.U32((uint)p.f_144, rt.GetTexture(tex));

            for (uint s = 0; s < 0x2d0; s += 0xb4)
            {
                uint b = p.A + s;
                M.U8(b + 0x3e8, 1);
                M.I32(b + 0x3f4, 0);
                M.F32(b + 0x418, 3.0f);
                M.I16(b + 0x3ee, (short)RandMod(rt, 0x168));
                M.I16(b + 0x3ea, 0);
                M.U8(b + 0x450, 0);
                M.I16(b + 0x3f8, (short)((short)f_17c - 0x32));
                M.F32(b + 0x46c, (float)(RandMod(rt, 0xa0) - 0x50));
                M.F32(b + 0x474, (float)(RandMod(rt, 0xa0) - 0x50));
                uint attr = M.U32(M.U32(rt.Mode + 0xcc) + 0x30);
                int depth = RandMod(rt, 0x42);
                float h = rt.GatGetHeight_6c32a0(attr, M.F32(b + 0x46c) + m_pos_x, M.F32(b + 0x474) + m_pos_z);
                M.F32(b + 0x470, (float)(((double)h - 5.0) - (double)(float)depth));   // x87 FSUB, FSUB, FSTP
                M.F32(b + 0x46c, m_pos_x + M.F32(b + 0x46c));
                M.F32(b + 0x474, m_pos_z + M.F32(b + 0x474));
                M.F32(b + 0x3fc, (float)RandMod(rt, 0x168));
                M.F32(b + 0x400, (float)RandMod(rt, 0x168));
                M.F32(b + 0x404, (float)RandMod(rt, 0x168));
                M.F32(b + 0x408, (float)RandMod(rt, 0x168));
                M.F32(b + 0x468, (float)RandMod(rt, 0xb) * 0.1f + 3.0f);
                M.F32(b + 0x41c, (float)RandMod(rt, 0x168));
                M.U8(b + 0x452, 7);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b89b90 (0xb89b90) — Kamaitachi: follows the owner; on ticks 0..4 one prim 0x7e each (4 wind
        /// blades flying from the owner towards the target f_180/f_188, the start offset per tick: centre, +x, -x, +z,
        /// -z by 5), slot k starts after 3 - k ticks.
        /// </summary>
        public void Fn_b89b90(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt >= 5) return;
            Prim p = LaunchEffectPrim(rt, 0x7e, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            p.f_318 = 0f;
            M.U32(p.A + 0x388, M.U32(A + 0x180));
            M.U32(p.A + 0x38c, M.U32(A + 0x184));
            M.U32(p.A + 0x390, M.U32(A + 0x188));
            M.U32(p.A + 0x10, M.U32(A + 0x10));
            M.U32(p.A + 0x14, M.U32(A + 0x14));
            M.U32(p.A + 0x18, M.U32(A + 0x18));
            for (int k = 0; k < 4; k++)
            {
                uint b = p.A + (uint)(k * 0xb4);
                M.U8(b + 0x3e8, 1);
                M.I32(b + 0x3f4, k - 3);
                M.F32(b + 0x468, 20.0f);
                M.I16(b + 0x3ea, 0);
                int t = m_stateCnt;
                if (t == 0) { M.U32(b + 0x46c, M.U32(A + 0x10)); M.U32(b + 0x474, M.U32(A + 0x18)); }
                else if (t == 1) { M.F32(b + 0x46c, m_pos_x + 5.0f); M.U32(b + 0x474, M.U32(A + 0x18)); }
                else if (t == 2) { M.F32(b + 0x46c, m_pos_x - 5.0f); M.U32(b + 0x474, M.U32(A + 0x18)); }
                else
                {
                    M.U32(b + 0x46c, M.U32(A + 0x10));
                    M.F32(b + 0x474, t == 3 ? m_pos_z + 5.0f : m_pos_z - 5.0f);
                }
                float baseY = m_pos_y - 10.0f;
                M.F32(b + 0x470, (float)RandMod(rt, 5) + baseY);
                M.I16(b + 0x3f8, (short)RandMod(rt, 0x168));
                M.I16(b + 0x466, (short)k);
                M.F32(b + 0x400, 1.5f);
                M.U8(b + 0x450, 0);
                M.U8(b + 0x453, 1);
            }
        }

        static readonly short[] BubbleStart_b93140 = { 0xb4, 0x10e, 0, 0x5a };
        static readonly short[] BubbleRadius_b93140 = { 0x4b, 0x3f, 0x34, 0x28 };

        /// <summary>
        /// CRagEffect::fn_b93140 (0xb93140) — Bard/Dancer performance cast: follows the owner; at tick 0 plays
        /// EF_BeginSpell.wav (y -100), raises the lifetime to at least 120 ticks and launches one prim 0x95: a ring of
        /// 27 bubble frames (effect\W_bubble01..27.tga; <paramref name="kind"/> 10: 4 fire frames YBfire_01..04.bmp),
        /// 4 slots at angles 180/270/0/90 with radii 75/63/52/40, slot +0x3ec = kind. The first two arguments are unused.
        /// </summary>
        public void Fn_b93140(FxRuntime rt, int unusedSize, string unusedName, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, -100.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            if (f_17c < 0x78) f_17c = 0x78;
            Prim p = LaunchEffectPrim(rt, 0x95, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            if (kind == 10)
            {
                p.f_204 = 4;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                for (int i = 1; i <= 4; i++)
                    M.U32((uint)p.f_144 + (uint)(i - 1) * 4, rt.GetTexture("effect\\YBfire_" + i.ToString("00") + ".bmp"));
            }
            else
            {
                p.f_204 = 0x1b;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                for (int i = 1; i <= 0x1b; i++)
                    M.U32((uint)p.f_144 + (uint)(i - 1) * 4, rt.GetTexture("effect\\W_bubble" + i.ToString("00") + ".tga"));
            }
            p.f_318 = 0f;
            short[] start = BubbleStart_b93140;
            short[] radius = BubbleRadius_b93140;
            for (int k = 0; k < 4; k++)
            {
                uint b = p.A + (uint)(k * 0xb4);
                M.U8(b + 0x3e8, 1);
                M.I32(b + 0x3f4, 0);
                M.I16(b + 0x3ee, 0x168);
                M.F32(b + 0x3f0, 23.0f);
                M.I16(b + 0x3f8, start[k]);
                M.I16(b + 0x3ea, 0);
                M.F32(b + 0x468, 4.3f);
                M.I16(b + 0x466, radius[k]);
            }
            for (uint s = 0; s < 0x2d0; s += 0xb4)
            {
                uint b = p.A + s;
                M.I16(b + 0x3ec, kind);
                M.I32(b + 0x470, 0);
                for (uint i = 0; i < 0x15; i++)
                {
                    M.I32(b + 0x3fc + i * 4, 0);
                    M.U8(b + 0x450 + i, 0);
                }
            }
        }

        /// <summary>
        /// CRagEffect::fn_b99990 (0xb99990) — Adaptation particles: owner screen position into f_154/f_158, follows the
        /// owner; on even ticks one prim 0x10 (ac_center2.tga, 50 ticks) placed 3..9 units out at a random heading,
        /// 5 lower, rising (f_2c0 -0.015) — <paramref name="kind"/> 2: colour (r, r, 255), else (255, g, g), r/g random.
        /// </summary>
        public void Fn_b99990(FxRuntime rt, sbyte kind)
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
            p.f_398 = p.f_398 - 5.0f;
            p = m_lastPrim;
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(-0x5a));
            p.f_2c0 = -0.015f;
            p.f_354 = (float)(RandMod(rt, 0x3c) + 0x1e) / 10.0f;
            p.f_360 = 0.18f;
            p.f_324 = 0f;
            p.f_2d4 = 200.0f;
            if (kind == 2)
            {
                int r = rt.Rand.Next() & 0xff;
                M.I32(p.A + 0x23c, r);
                p.f_240 = M.I32(p.A + 0x23c);
                p.f_244 = 0xff;
            }
            else
            {
                M.I32(p.A + 0x23c, 0xff);
                p.f_240 = rt.Rand.Next() & 0xff;
                p.f_244 = p.f_240;
            }
            p.f_328 = p.f_2d4 / 20.0f;
            p.f_1f0 = p.m_duration - 0x14;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture("effect\\ac_center2.tga"));
            p.f_3e8 = 0;
        }
    }
}
