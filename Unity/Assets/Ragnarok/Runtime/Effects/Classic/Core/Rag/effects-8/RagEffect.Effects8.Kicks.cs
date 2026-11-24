// Synced from reference/port/src/RoClassicFx/Rag/effects-8/RagEffect.Effects8.Kicks.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-8: the Taekwon kick trails (prim type 0x64, textured white02.bmp). Each launcher fills the four
    /// 0xb4-byte slots of one prim: +0 enable, +2 alpha (short), +4 colour kind (short), +8 tube radius, +0xc life
    /// (negative = delay), +0x14 swing angle history (dwords), +0x7e heading (short), +0x80 arc radius, +0x84 centre,
    /// +0x90 height factor, +0x94 trail length.
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 429 (0x1ad) Turn Kick / Stun — fn_b7db80(3).</summary>
        [FxEffectProcess(0x1ad)]
        public FxCaseResult Case_E8_TurnKickTrail(FxRuntime rt) { Fn_b7db80(rt, 3); return FxCaseResult.Continue; }

        /// <summary>EF 430 (0x1ae) Counter Kick — fn_b7df80(3).</summary>
        [FxEffectProcess(0x1ae)]
        public FxCaseResult Case_E8_CounterKickTrail(FxRuntime rt) { Fn_b7df80(rt, 3); return FxCaseResult.Continue; }

        /// <summary>EF 431 (0x1af) Down Kick / Heel Drop — fn_b7d570(3).</summary>
        [FxEffectProcess(0x1af)]
        public FxCaseResult Case_E8_DownKickTrail(FxRuntime rt) { Fn_b7d570(rt, 3); return FxCaseResult.Continue; }

        /// <summary>The kick prim preamble: prim 0x64, flags 5, duration f_17c, one texture white02.bmp, f_318 = 0.</summary>
        Prim E8_LaunchKick(FxRuntime rt)
        {
            Prim p = LaunchEffectPrim(rt, 100, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture("effect\\white02.bmp"));
            p.f_318 = 0f;
            return p;
        }

        /// <summary>A kick slot's tail: centre = effect position, trail length, cleared history (+0x18..+0x67, bytes +0x69..+0x7c), kind.</summary>
        void E8_KickSlotTail(uint s, float length, sbyte kind)
        {
            M.F32(s + 0x46c, m_pos_x);
            M.F32(s + 0x470, m_pos_y);
            M.F32(s + 0x474, m_pos_z);
            M.F32(s + 0x47c, length);
            for (uint o = 0x400; o < 0x450; o += 4) M.U32(s + o, 0);
            for (uint o = 0x451; o < 0x465; o++) M.U8(s + o, 0);
            M.I16(s + 0x3ec, kind);
        }

        /// <summary>
        /// CRagEffect::fn_b7db80 (0xb7db80) — Turn Kick: follows the owner; on each of ticks 6..9 launches a prim 0x64
        /// with four arcs: delay 0..30, tube radius 0.05 (kind 3) or 0.1, alpha 125..245, random heading, arc radius
        /// 6..11, height factor 0.8..1.4, trail length 4.
        /// </summary>
        public void Fn_b7db80(FxRuntime rt, sbyte kind)
        {
            FollowMaster();
            if ((uint)(m_stateCnt - 6) >= 4) return;
            Prim p = E8_LaunchKick(rt);
            for (uint o = 0x3e8; o < 0x6b8; o += 0xb4) M.U8(p.A + o, 0);
            for (uint k = 0; k < 4; k++)
            {
                uint s = p.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, -RandMod(rt, 0x1f));
                M.U32(s + 0x3f0, kind == 3 ? 0x3d4ccccdu : 0x3dcccccdu);   // 0.05 / 0.1
                M.I16(s + 0x3ea, (short)(RandMod(rt, 0x79) + 0x7d));
                M.I16(s + 0x466, (short)RandMod(rt, 0x168));
                M.F32(s + 0x468, (float)RandMod(rt, 6) + 6.0f);
                M.U32(s + 0x3fc, 0);
                M.F32(s + 0x478, (float)RandMod(rt, 7) * 0.1f + 0.8f);
                E8_KickSlotTail(s, 4.0f, kind);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b7df80 (0xb7df80) — Counter Kick: follows the owner; on ticks 0..2 launches a prim 0x64 aimed
        /// away from the target (prim +0x388 = f_180..f_188, heading = FUN_00ad5710(pos − target) + (−60..60), wrapped
        /// to 0..359): delay 15..40 (25..45 for kind 3), tube radius 0.07 (0.06), alpha 125..245, arc radius 6..12,
        /// height factor 0.8..1.4, trail length 3.
        /// </summary>
        public void Fn_b7df80(FxRuntime rt, sbyte kind)
        {
            FollowMaster();
            if ((uint)m_stateCnt >= 3) return;
            Prim p = E8_LaunchKick(rt);
            p.f_388 = f_180;
            p.f_38c = f_184;
            p.f_390 = f_188;
            p.m_pos_x = m_pos_x;
            p.m_pos_y = m_pos_y;
            p.m_pos_z = m_pos_z;
            for (uint o = 0x3e8; o < 0x6b8; o += 0xb4) M.U8(p.A + o, 0);
            for (uint k = 0; k < 4; k++)
            {
                uint s = p.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                if (kind == 3)
                {
                    M.I32(s + 0x3f4, -0x19 - RandMod(rt, 0x15));
                    M.U32(s + 0x3f0, 0x3d75c28f);   // 0.06
                }
                else
                {
                    M.I32(s + 0x3f4, -0xf - RandMod(rt, 0x1a));
                    M.U32(s + 0x3f0, 0x3d8f5c29);   // 0.07
                }
                M.I16(s + 0x3ea, (short)(RandMod(rt, 0x79) + 0x7d));
                M.I16(s + 0x466, FxMath.DirectionToAngle(m_pos_x - f_180, m_pos_z - f_188));
                M.I16(s + 0x466, (short)(M.I16(s + 0x466) + (short)(RandMod(rt, 0x79) - 0x3c)));
                if (M.I16(s + 0x466) >= 0x168) M.I16(s + 0x466, (short)(M.I16(s + 0x466) - 0x168));
                if (M.I16(s + 0x466) < 0) M.I16(s + 0x466, (short)(M.I16(s + 0x466) + 0x168));
                M.F32(s + 0x468, (float)RandMod(rt, 7) + 6.0f);
                M.U32(s + 0x3fc, 0);
                M.F32(s + 0x478, (float)RandMod(rt, 7) * 0.1f + 0.8f);
                E8_KickSlotTail(s, 3.0f, kind);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b7d570 (0xb7d570) — Down Kick / Heel Drop: follows the owner; launches a prim 0x64 on each tick
        /// of [0, 2) for kind 1 or [0, 5) for kind 3 (none otherwise): delay 0..10 (kind 1), 30..70 (kind 3) or
        /// 0..25, tube radius 0.1 (0.07 for kind 3), alpha 125..175 (kind ≤ 2) or 125..245, random heading, arc
        /// radius 5..9 with trail 3 (kind 1) or 6..11 with trail 4 (kind 3) / 5, height factor 0.6..1.2. Kind 1
        /// pre-advances the swing 10 ticks (angle +6 up to 180, history shifted by the trail length each time).
        /// </summary>
        public void Fn_b7d570(FxRuntime rt, int kind)
        {
            FollowMaster();
            sbyte k8 = (sbyte)kind;
            int lo, hi;
            if (k8 == 1) { lo = 0; hi = 2; }
            else if (k8 == 3) { lo = 0; hi = 5; }
            else { lo = kind; hi = kind; }
            if (m_stateCnt < lo || m_stateCnt >= hi) return;
            Prim p = E8_LaunchKick(rt);
            for (uint k = 0; k < 4; k++)
            {
                uint s = p.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                int spread;
                if (k8 == 1)
                {
                    M.I32(s + 0x3f4, -RandMod(rt, 0xb));
                    M.U32(s + 0x3f0, 0x3dcccccd);   // 0.1
                    spread = 0x33;
                }
                else if (k8 == 3)
                {
                    M.I32(s + 0x3f4, -0x1e - RandMod(rt, 0x29));
                    M.U32(s + 0x3f0, 0x3d8f5c29);   // 0.07
                    spread = 0x79;
                }
                else
                {
                    M.I32(s + 0x3f4, -RandMod(rt, 0x1a));
                    M.U32(s + 0x3f0, 0x3dcccccd);
                    spread = k8 < 3 ? 0x33 : 0x79;
                }
                M.I16(s + 0x3ea, (short)(RandMod(rt, spread) + 0x7d));
                M.I16(s + 0x466, (short)RandMod(rt, 0x168));
                float length;
                if (k8 == 1)
                {
                    M.F32(s + 0x468, (float)RandMod(rt, 5) + 5.0f);
                    length = 3.0f;
                }
                else
                {
                    M.F32(s + 0x468, (float)RandMod(rt, 6) + 6.0f);
                    length = k8 == 3 ? 4.0f : 5.0f;
                }
                M.U32(s + 0x3fc, 0);
                M.F32(s + 0x478, (float)RandMod(rt, 7) * 0.1f + 0.6f);
                E8_KickSlotTail(s, length, k8);
                if (k8 != 1) continue;
                for (int step = 10; step != 0; step--)
                {
                    M.F32(s + 0x3fc, M.F32(s + 0x3fc) + 6.0f);
                    if (M.F32(s + 0x3fc) > 180.0f) M.U32(s + 0x3fc, 0x43340000);
                    int n = FxOps.Cvtt(M.F32(s + 0x47c));
                    for (int i = n; i >= 1; i--) M.U32(s + 0x3fc + (uint)i * 4, M.U32(s + 0x3f8 + (uint)i * 4));
                }
            }
        }
    }
}
