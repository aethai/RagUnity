// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.RingPrims.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-1: handlers that launch one "ring" prim (types 0x18, 0x22, 0x25, 0x4b, 0x65) and fill its
    /// four 0xb4-byte ring blocks at +0x3e8/+0x49c/+0x550/+0x604. Field widths follow the assembly
    /// (byte enable +0, shorts +2/+4/+6/+0x10/+0x7e, floats +8/+0x80, dwords +0xc/+0x88 …).
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>The ring-prim preamble: launch, f_1c4, duration = effect duration, texture array.</summary>
        Prim LaunchRingPrim(FxRuntime rt, int type, uint f1c4Value, bool orFlags)
        {
            Prim p = LaunchEffectPrim(rt, type, 0f, 0f, 0f);
            m_lastPrim = p;
            if (orFlags)
            {
                p.f_1c4 = 0;
                p.f_1c4 |= 1;
                p.f_1c4 |= 5;
            }
            else p.f_1c4 = f1c4Value;
            p.m_duration = f_17c;
            return p;
        }

        // ---- EF 222 Defending Aura / EF 252 Shield Reflect --------------------------------------

        [FxEffectProcess(222)]
        public FxCaseResult Case_DefendingAura(FxRuntime rt) { Fn_b6ff20(rt, "effect\\ring_black.tga"); return FxCaseResult.Continue; }

        [FxEffectProcess(252)]
        public FxCaseResult Case_ShieldReflect(FxRuntime rt) { Fn_b6ff20(rt, "effect\\ring_yellow.tga"); return FxCaseResult.Continue; }

        static readonly uint[] RingRadius_b6ff20 = { 0x42200000, 0x421c0000, 0x42180000, 0x42140000 };
        static readonly ushort[] RingAngle_b6ff20 = { 0xb4, 0x10e, 0, 0x5a };
        static readonly uint[] RingHeight_b6ff20 = { 0x41000000, 0x40fccccd, 0x40f9999a, 0x40f66666 };

        /// <summary>
        /// CRagEffect::fn_b6ff20 (0xb6ff20) — follows the owner; at tick 0 a prim 0x18 with four
        /// concentric rings (radius 40/39/38/37, heights 8/7.9/7.8/7.7) textured <paramref name="texture"/>.
        /// </summary>
        public void Fn_b6ff20(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchRingPrim(rt, 0x18, 5, false);
            SetFirstTexture(rt, p, texture);
            uint a = p.A;
            M.U32(a + 0x318, 0);
            // (block base, radius bits, angle, height bits)
            uint[] radius = RingRadius_b6ff20;
            ushort[] angle = RingAngle_b6ff20;
            uint[] height = RingHeight_b6ff20;
            for (uint i = 0; i < 4; i++)
            {
                uint b = a + 0x3e8 + i * 0xb4;
                M.U8(b + 0x0, 1);
                M.U32(b + 0xc, 0);
                M.U16(b + 0x6, 0x168);
                M.U32(b + 0x8, radius[i]);
                M.U16(b + 0x10, angle[i]);
                M.U16(b + 0x2, 0);
                M.U32(b + 0x80, height[i]);
                M.U16(b + 0x7e, 0x5a);
                M.U32(b + 0x88, 0);
                for (uint k = 0x14; k <= 0x64; k += 4) M.U32(b + k, 0);
                for (uint k = 0x68; k <= 0x7c; k++) M.U8(b + k, 0);
            }
        }

        // ---- EF 226 Grand Cross -------------------------------------------------------------------

        [FxEffectProcess(226)]
        public FxCaseResult Case_GrandCross(FxRuntime rt)
        {
            Fn_b77820(rt, 0, 0);
            Fn_b77650(rt, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b77820 (0xb77820) — Grand Cross cross: at tick 0 sound + prim 0x22 with four
        /// rings of radius 120 (ring_white, or ring_black when <paramref name="dark"/>), one per arm.
        /// </summary>
        public void Fn_b77820(FxRuntime rt, short startAngle, byte dark)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\cru_grand cross.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchRingPrim(rt, 0x22, 0, true);
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            if (dark == 0)
            {
                M.U32((uint)p.f_144, rt.GetTexture("effect\\ring_white.tga"));
                M.U32(p.A + 0x318, 0x3f800000);
            }
            else
            {
                M.U32((uint)p.f_144, rt.GetTexture("effect\\ring_black.tga"));
                M.U32(p.A + 0x318, 0x41400000);
            }
            uint a = p.A;
            for (uint i = 0; i < 4; i++)
            {
                uint b = a + 0x3e8 + i * 0xb4;
                M.U8(b + 0x0, 1);
                M.U32(b + 0xc, 0);
                M.U16(b + 0x6, 0x5a);
                M.U32(b + 0x8, 0x42f00000);
                M.I16(b + 0x2, startAngle);
                M.U32(b + 0x80, 0x419f3333);
                M.U16(b + 0x7e, 0x5a);
                M.U8(b + 0x6c, dark);
            }
            M.U16(a + 0x3f8, 0xb4);
            M.U8(a + 0x450, 0x17);
            M.U8(a + 0x451, 0x17);
            M.U16(a + 0x4ac, 0x5a);
            M.U8(a + 0x504, 0x17);
            M.U8(a + 0x505, 0xe9);
            M.U16(a + 0x560, 0);
            M.U8(a + 0x5b8, 0xe9);
            M.U8(a + 0x5b9, 0xe9);
            M.U16(a + 0x614, 0x10e);
            M.U8(a + 0x66c, 0xe9);
            M.U8(a + 0x66d, 0x17);
        }

        /// <summary>
        /// CRagEffect::fn_b77650 (0xb77650) — Grand Cross ground ring: at tick 0 prim 0x25 with two rings
        /// (radius 60, ring_yellow, or ring_black when <paramref name="dark"/>).
        /// </summary>
        public void Fn_b77650(FxRuntime rt, byte dark)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchRingPrim(rt, 0x25, 0, true);
            SetFirstTexture(rt, p, dark == 0 ? "effect\\ring_yellow.tga" : "effect\\ring_black.tga");
            M.U32(p.A + 0x318, 0x3f800000);
            for (uint i = 0; i < 2; i++)
            {
                uint b = p.A + 0x3e8 + i * 0xb4;
                M.U8(b + 0x0, 1);
                M.U32(b + 0xc, 0);
                M.U32(b + 0x8, 0x42700000);
                M.U16(b + 0x10, (ushort)(i * 0x5a));
                M.U16(b + 0x2, 0);
                M.U32(b + 0x14, 0x41c00000);
                M.U32(b + 0x18, 0x404ccccd);
                M.U32(b + 0x1c, 0);
            }
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
        }

        // ---- EF 432 Feeling the Sun, Moon and Stars ----------------------------------------------

        [FxEffectProcess(432)]
        public FxCaseResult Case_FeelingSunMoonStars(FxRuntime rt)
        {
            if (m_stateCnt == 0x19) FollowMasterAndPlay(rt, Effects1Strings.Wav_fe26ec, 0f);
            Fn_b74770(rt, 0, 0);
            Fn_b74770(rt, 0, 0xb4);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b74770 (0xb74770) — for ticks 20..23 an electric ring prim (0x65, elec1.tga):
        /// four blocks at headings angle + 45·i, tiny random speed, anchored at the effect position.
        /// </summary>
        public void Fn_b74770(FxRuntime rt, sbyte tilt, int angle)
        {
            FollowMaster();
            if ((uint)(m_stateCnt - 0x14) > 3u) return;
            Prim p = LaunchRingPrim(rt, 0x65, 5, false);
            p.f_204 = 1;
            SetFirstTexture(rt, p, "effect\\elec1.tga");
            M.U32(p.A + 0x318, 0);
            for (uint i = 0; i < 4; i++) M.U8(p.A + 0x3e8 + i * 0xb4, 0);
            short tiltWord = tilt;
            for (int i = 0; i < 4; i++)
            {
                uint b = p.A + 0x3e8 + (uint)i * 0xb4;
                M.U8(b + 0x0, 1);
                M.U32(b + 0xc, 0);
                M.U32(b + 0x8, 0x3dcccccd);
                M.U16(b + 0x2, 0);
                M.U16(b + 0x7e, (ushort)(i * 0x2d + angle));
                M.U32(b + 0x80, 0);
                M.U32(b + 0x14, 0);
                float speed = (float)RandMod(rt, 0xb) * 0.01f + 0f;
                M.F32(b + 0x90, speed);
                M.U32(b + 0x84, M.U32(A + 0x10));
                M.U32(b + 0x88, M.U32(A + 0x14));
                M.U32(b + 0x8c, M.U32(A + 0x18));
                M.U32(b + 0x94, 0x41a00000);
                for (uint k = 0x18; k <= 0x64; k += 4) M.U32(b + k, 0);
                for (uint k = 0x69; k <= 0x7c; k++) M.U8(b + k, 0);
                M.U8(b + 0x68, 0);
                M.I16(b + 0x4, tiltWord);
                M.U16(b + 0x6, 0);
            }
        }

        // ---- EF 590 Kaizel ------------------------------------------------------------------------

        /// <summary>vf15 case 0x24e — Kaizel: at tick 15 two blue magic-circle prims (headings 0 and 45).</summary>
        [FxEffectProcess(590)]
        public FxCaseResult Case_Kaizel(FxRuntime rt)
        {
            if (m_stateCnt == 0xf)
            {
                Fn_b7ed60(rt, 0);
                Fn_b7ed60(rt, 0x2d);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>CRagEffect::fn_b7ed60 (0xb7ed60) — prim 0x4b, magic_blue.tga, four blocks at heading + 90·i.</summary>
        public bool Fn_b7ed60(FxRuntime rt, int heading)
        {
            FollowMaster();
            Prim p = LaunchRingPrim(rt, 0x4b, 5, false);
            SetFirstTexture(rt, p, "effect\\magic_blue.tga");
            M.U32(p.A + 0x318, 0x40800000);
            for (int i = 0; i < 4; i++)
            {
                uint b = p.A + 0x3e8 + (uint)i * 0xb4;
                M.U8(b + 0x0, 1);
                M.U32(b + 0xc, 0);
                M.U32(b + 0x8, 0x3fc00000);
                M.U16(b + 0x10, (ushort)(i * 0x5a + heading));
                M.U16(b + 0x2, 0);
                M.U32(b + 0x80, 0x40000000);
                M.U32(b + 0x14, 0x3f800000);
                M.U8(b + 0x68, 0);
            }
            return true;
        }
    }
}
