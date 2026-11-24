// Synced from reference/port/src/RoClassicFx/Rag/aura-vanish/RagEffect.AuraVanish.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// aura-vanish: the ground effects of ZC_NOTIFY_VANISH and the arrival effect of NEWENTRY
    /// (packages/combat-timing §8), CRagEffect::vf14 (0xb46290) cases 4 / 0xe / 0x2c. Each launches one ring prim
    /// (0x39 or 0x3c) on its first tick and returns 1, so the effect lives f_17c ticks (FUN_00b3c420: 200 / 200 / 100).
    /// Ring blocks (stride 0xb4 at prim +0x3e8): +0 enable, +2 alpha (short), +4 short, +6 span (short), +8 float,
    /// +0xc age, +0x10 start angle (short), +0x14..+0x67 21 per-segment floats, +0x68..+0x7c 21 bytes, +0x7e tilt
    /// (short), +0x80 radius, +0x88 dword.
    /// </summary>
    public readonly partial struct RagEffect
    {
        const string TeleportWav = "effect\\EF_Teleportation.wav";

        /// <summary>EF 304 (0x130, EF_TELEPORTATION2; vf14 case 4 @0xb462c2): fn_b8d220(this, "effect\Magic_Violet.tga", 0).</summary>
        [FxEffectProcess(0x130)]
        public FxCaseResult Case_Teleportation2(FxRuntime rt)
        {
            Fn_b8d220(rt, "effect\\Magic_Violet.tga", 0);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 314 (0x13a, EF_EXIT2; vf14 case 0xe @0xb466a3): fn_b75af0(this, "effect\ring_purple.tga").</summary>
        [FxEffectProcess(0x13a)]
        public FxCaseResult Case_Exit2(FxRuntime rt)
        {
            Fn_b75af0(rt, "effect\\ring_purple.tga");
            return FxCaseResult.Continue;
        }

        /// <summary>EF 344 (0x158, EF_ENTRY2; vf14 case 0x2c @0xb46b8a): fn_b75380(this).</summary>
        [FxEffectProcess(0x158)]
        public FxCaseResult Case_Entry2(FxRuntime rt)
        {
            Fn_b75380(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>The common launch of the three helpers: prim, f_1c4 = 5, its life = the effect's, one texture, f_318 = 4.</summary>
        Prim LaunchVanishRing(FxRuntime rt, int type, string texture)
        {
            Prim p = LaunchEffectPrim(rt, type, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            SetFirstTexture(rt, p, texture);
            M.U32(p.A + 0x318, 0x40800000);   // 4.0
            return p;
        }

        /// <summary>
        /// One ring block of fn_b75af0 / fn_b75380: enable, age 0, span 360, +8, start angle, alpha 0, radius, tilt,
        /// +4, +0x88 = 0, then the 21 segment floats cleared and the 21 segment bytes set to <paramref name="segByte"/>.
        /// </summary>
        void VanishBlock(uint b, uint f8Bits, short start, uint radiusBits, short tilt, short f4, byte segByte)
        {
            M.U8(b, 1);
            M.U32(b + 0xc, 0);
            M.U16(b + 6, 0x168);
            M.U32(b + 8, f8Bits);
            M.I16(b + 0x10, start);
            M.U16(b + 2, 0);
            M.U32(b + 0x80, radiusBits);
            M.I16(b + 0x7e, tilt);
            M.I16(b + 4, f4);
            M.U32(b + 0x88, 0);
            for (uint j = 0; j < 21; j++)
            {
                M.U32(b + 0x14 + j * 4, 0);
                M.U8(b + 0x68 + j, segByte);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b8d220 (0xb8d220, thiscall (texture, short <paramref name="lift"/>), RET 8) — EF 304. Only on
        /// tick 0: the position is taken from the owner once (the effect stays where the player vanished), sound
        /// EF_Teleportation.wav, prim 0x39 with four rising rings: radius (+8) 100 / 70 / 40 / 15 (+ 60 · lift),
        /// height speed (+0x80) 1.5 / 3 / 4 / 5 (− 2 · lift), start angles 180 / 270 / 0 / 90, alphas 100 / 80 / 60 / 40;
        /// byte +0x69 = (char)(int)radius (the swell amplitude the prim reads).
        /// </summary>
        public void Fn_b8d220(FxRuntime rt, string texture, short lift)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            rt.PlaySound3DRelative(TeleportWav, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchVanishRing(rt, 0x39, texture);
            uint a = p.A;
            float up = (float)(lift * 60);
            float down = (float)(lift + lift);

            M.U8(a + 0x3e8, 1);
            M.U32(a + 0x3f4, 0);
            M.U16(a + 0x3ee, 0x168);
            M.F32(a + 0x3f0, up + 100.0f);
            M.U16(a + 0x3f8, 0xb4);
            M.U16(a + 0x3ea, 100);
            M.F32(a + 0x468, 1.5f - down);
            M.U16(a + 0x466, 0x5a);
            M.U32(a + 0x470, 0);

            M.U8(a + 0x49c, 1);
            M.U32(a + 0x4a8, 0);
            M.U16(a + 0x4a2, 0x168);
            M.F32(a + 0x4a4, up + 70.0f);
            M.U16(a + 0x4ac, 0x10e);
            M.U16(a + 0x49e, 0x50);
            M.F32(a + 0x51c, 3.0f - down);
            M.U16(a + 0x51a, 0x59);
            M.U32(a + 0x524, 0);

            M.U8(a + 0x550, 1);
            M.U32(a + 0x55c, 0);
            M.U16(a + 0x556, 0x168);
            M.F32(a + 0x558, up + 40.0f);
            M.U16(a + 0x560, 0);
            M.U16(a + 0x552, 0x3c);
            M.F32(a + 0x5d0, 4.0f - down);
            M.U16(a + 0x5ce, 0x58);
            M.U32(a + 0x5d8, 0);

            M.U8(a + 0x604, 1);
            M.U32(a + 0x610, 0);
            M.U16(a + 0x60a, 0x168);
            M.F32(a + 0x60c, up + 15.0f);
            M.U16(a + 0x614, 0x5a);
            M.U16(a + 0x606, 0x28);
            M.F32(a + 0x684, 5.0f - down);
            M.U16(a + 0x682, 0x57);
            M.U32(a + 0x68c, 0);

            for (uint k = 0; k < 0x2d0; k += 0xb4)
            {
                for (uint j = 0; j < 21; j++) M.U32(a + k + 0x3fc + j * 4, 0);
                M.U8(a + k + 0x450, 0);
                M.U8(a + k + 0x451, (byte)FxOps.Cvtt(M.F32(a + k + 0x3f0)));
            }
        }

        /// <summary>
        /// CRagEffect::fn_b75af0 (0xb75af0, thiscall (texture), RET 4) — EF 314. Follows the owner every tick; on tick 0
        /// sound EF_Teleportation.wav and prim 0x3c (fn_ae60e0) with four rings: +8 = 180 / 70 / 45 / 20, start angles
        /// 270 / 180 / 90 / 0, radius 1.8 / 2.0 / 2.2 / 2.4, tilt 90 / 88 / 86 / 84, +4 = 58 / 60 / 60 / 60.
        /// </summary>
        public void Fn_b75af0(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative(TeleportWav, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            uint a = LaunchVanishRing(rt, 0x3c, texture).A;
            VanishBlock(a + 0x604, 0x41a00000, 0, 0x4019999a, 0x54, 0x3c, 0);       // 20, 2.4
            VanishBlock(a + 0x550, 0x42340000, 0x5a, 0x400ccccd, 0x56, 0x3c, 0);    // 45, 2.2
            VanishBlock(a + 0x49c, 0x428c0000, 0xb4, 0x40000000, 0x58, 0x3c, 0);    // 70, 2.0
            VanishBlock(a + 0x3e8, 0x43340000, 0x10e, 0x3fe66666, 0x5a, 0x3a, 0);   // 180, 1.8
        }

        /// <summary>
        /// CRagEffect::fn_b75380 (0xb75380) — EF 344. Follows the owner every tick; on tick 0 sound EF_Portal.wav (150
        /// below the effect) and prim 0x3c with four rings of ring_blue.tga: +8 = 30 / 30 / 4 / 4, random start angles
        /// (rand() % 360, one call per ring in ring order), radius 3.7 / 3.4 / 3.6 / 3.7, tilt 90 / 90 / 10 / 5,
        /// +4 = 1400, segment bytes 2.
        /// </summary>
        public void Fn_b75380(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\EF_Portal.wav", m_pos_x, -150.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            uint a = LaunchVanishRing(rt, 0x3c, "effect\\ring_blue.tga").A;
            VanishBlock(a + 0x3e8, 0x41f00000, RandAngle_b75380(rt), 0x406ccccd, 0x5a, 0x578, 2);   // 30, 3.7
            VanishBlock(a + 0x49c, 0x41f00000, RandAngle_b75380(rt), 0x4059999a, 0x5a, 0x578, 2);   // 30, 3.4
            VanishBlock(a + 0x550, 0x40800000, RandAngle_b75380(rt), 0x40666666, 10, 0x578, 2);     // 4, 3.6
            VanishBlock(a + 0x604, 0x40800000, RandAngle_b75380(rt), 0x406ccccd, 5, 0x578, 2);      // 4, 3.7
        }

        /// <summary>rand() % 360 as a short (MOVZX of DX, CMOVNS: a negative remainder would give 0).</summary>
        static short RandAngle_b75380(FxRuntime rt)
        {
            int r = rt.Rand.Next() % 0x168;
            return r >= 0 ? (short)(ushort)r : (short)0;
        }
    }
}
