// Synced from reference/port/src/RoClassicFx/Rag/effects-5/RagEffect.Effects5.Rings.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// The launch preamble shared by the multi-slot prims of this package: LaunchEffectPrim(type, 0, 0, 0),
        /// f_1c4 = 5, duration = effect duration, one texture array of f_204 entries (operator new[]) and
        /// texture 0 = <paramref name="texture"/>.
        /// </summary>
        Prim LaunchSlotPrim(FxRuntime rt, int type, string texture)
        {
            Prim p = LaunchEffectPrim(rt, type, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            uint tex = rt.GetTexture(texture);
            M.U32((uint)p.f_144, tex);
            return p;
        }

        /// <summary>
        /// CRagEffect::fn_b66d40 (0xb66d40): follows the owner; at tick 0 launches prim 0x59 (4 rotating slots)
        /// with <paramref name="texture"/>; <paramref name="index"/> offsets the slot angles, <paramref name="style"/>
        /// selects height (2/3/6: 0, else -24) and angle step (3: 7°, else 22°).
        /// </summary>
        public void Fn_b66d40(FxRuntime rt, string texture, sbyte index, sbyte style)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            LaunchSlotPrim(rt, 0x59, texture);
            float fStyle = (float)(int)style;
            for (uint k = 0; k < 4; k++)
            {
                uint s = m_lastPrim.A + k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.F32(m_lastPrim.A + k * 0xb4 + 0x40c, fStyle);
                short step;
                if (style == 2 || style == 3 || style == 6)
                {
                    M.I32(m_lastPrim.A + k * 0xb4 + 0x3f4, 0);
                    M.F32(m_lastPrim.A + k * 0xb4 + 0x468, 2.0f);
                    step = (short)(style == 3 ? index * 7 : index * 0x16);
                }
                else
                {
                    M.I32(m_lastPrim.A + k * 0xb4 + 0x3f4, -24);
                    M.F32(m_lastPrim.A + k * 0xb4 + 0x468, 2.0f);
                    step = (short)(index * 0x16);
                }
                M.I16(m_lastPrim.A + k * 0xb4 + 0x466, (short)(step + (short)(k * 0x5a)));
                int r = rt.Rand.Next() % 0x168;
                M.I16(m_lastPrim.A + k * 0xb4 + 0x3f8, (short)(r < 0 ? 0 : r));
                M.I16(m_lastPrim.A + k * 0xb4 + 0x3ea, 0);
                M.F32(m_lastPrim.A + k * 0xb4 + 0x3fc, -12.0f);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b8eed0 (0xb8eed0): follows the owner; at tick 0 launches prim 0x1f (4 expanding rings)
        /// with <paramref name="texture"/>; <paramref name="style"/> 2 = small (15, 70), 3 = f_318 17, else (25, 80).
        /// </summary>
        public void Fn_b8eed0(FxRuntime rt, string texture, sbyte style)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim lp = LaunchSlotPrim(rt, 0x1f, texture);
            lp.f_318 = 0f;
            if (style == 3) lp.f_318 = 17.0f;
            for (int k = 0; k < 4; k++)
            {
                uint s = (uint)k * 0xb4;
                M.U8(m_lastPrim.A + s + 0x3e8, 1);
                M.I32(m_lastPrim.A + s + 0x3f4, 0);
                M.I16(m_lastPrim.A + s + 0x3ee, 0x168);
                short w;
                if (style == 2) { M.F32(m_lastPrim.A + s + 0x3f0, 15.0f); w = 0x46; }
                else { M.F32(m_lastPrim.A + s + 0x3f0, 25.0f); w = 0x50; }
                M.I16(m_lastPrim.A + s + 0x466, w);
                M.I16(m_lastPrim.A + s + 0x3f8, (short)(k * 0x5a));
                M.I16(m_lastPrim.A + s + 0x3ea, 0);
                M.F32(m_lastPrim.A + s + 0x468, (float)k + 1.0f);
                M.I16(m_lastPrim.A + s + 0x3ec, style);
                M.I32(m_lastPrim.A + s + 0x470, 0);
                ClearSubSlots(m_lastPrim.A + s);
            }
        }

        /// <summary>The inlined 3×(7 dwords at +0x3fc, 7 bytes at +0x450) zeroing loop of the slot prims.</summary>
        void ClearSubSlots(uint slot)
        {
            for (uint j = 0; j < 3; j++)
            {
                for (uint i = 0; i < 7; i++)
                {
                    M.U32(slot + 0x3fc + j * 0x1c + i * 4, 0);
                    M.U8(slot + 0x450 + j * 7 + i, 0);
                }
            }
        }

        /// <summary>
        /// CRagEffect::fn_b6c9a0 (0xb6c9a0): follows the owner; at tick 0 launches prim 0x24 (one magic circle slot)
        /// with <paramref name="texture"/> and f_318 = <paramref name="size"/>.
        /// </summary>
        public void Fn_b6c9a0(FxRuntime rt, string texture, sbyte size)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            LaunchSlotPrim(rt, 0x24, texture);
            Prim p = m_lastPrim;
            p.f_318 = (float)(int)size;
            M.U8(m_lastPrim.A + 0x3e8, 0);
            M.U8(m_lastPrim.A + 0x49c, 0);
            M.U8(m_lastPrim.A + 0x550, 0);
            M.U8(m_lastPrim.A + 0x604, 0);
            M.U8(m_lastPrim.A + 0x3e8, 1);
            M.I32(m_lastPrim.A + 0x3f4, 0);
            M.I16(m_lastPrim.A + 0x3ee, 0x168);
            M.F32(m_lastPrim.A + 0x3f0, 15.0f);
            int r = rt.Rand.Next() % 0x168;
            M.I16(m_lastPrim.A + 0x3f8, (short)(r < 0 ? 0 : r));
            r = rt.Rand.Next() % 100;
            M.I16(m_lastPrim.A + 0x3ea, (short)(r < 0 ? 0 : r));
            M.F32(m_lastPrim.A + 0x468, 2.6f);
            M.I16(m_lastPrim.A + 0x466, 0x50);
            ClearSubSlots(m_lastPrim.A);
        }

        // ---- cases -------------------------------------------------------------------------

        [FxEffectProcess(0xdf)]
        public FxCaseResult Case_Ganbantein(FxRuntime rt) { Fn_b8eed0(rt, "effect\\ring_white.tga", 2); return FxCaseResult.Continue; }

        [FxEffectProcess(0xe1)]
        public FxCaseResult Case_VolcanoRing(FxRuntime rt) { Fn_b8eed0(rt, "effect\\ring_red.tga", 0); return FxCaseResult.Continue; }

        [FxEffectProcess(0xec)]
        public FxCaseResult Case_DelugeRing(FxRuntime rt) { Fn_b8eed0(rt, "effect\\ring_blue.tga", 0); return FxCaseResult.Continue; }

        [FxEffectProcess(0xed)]
        public FxCaseResult Case_WhirlwindRing(FxRuntime rt) { Fn_b8eed0(rt, "effect\\magic_green.tga", 0); return FxCaseResult.Continue; }

        [FxEffectProcess(0xee)]
        public FxCaseResult Case_MagneticEarthRing(FxRuntime rt) { Fn_b8eed0(rt, "effect\\ring_white.tga", 0); return FxCaseResult.Continue; }

        [FxEffectProcess(0x107)]
        public FxCaseResult Case_QuadrupleBlowRing(FxRuntime rt) { Fn_b8eed0(rt, "effect\\ring_yellow.tga", 1); return FxCaseResult.Continue; }

        [FxEffectProcess(0xef)]
        public FxCaseResult Case_VolcanoCircle(FxRuntime rt) { Fn_b6c9a0(rt, "effect\\ring_red.tga", 0); return FxCaseResult.Continue; }

        [FxEffectProcess(0xf0)]
        public FxCaseResult Case_DelugeCircle(FxRuntime rt) { Fn_b6c9a0(rt, "effect\\ring_blue.tga", 0); return FxCaseResult.Continue; }

        [FxEffectProcess(0xf1)]
        public FxCaseResult Case_WhirlwindCircle(FxRuntime rt) { Fn_b6c9a0(rt, "effect\\magic_green.tga", 0); return FxCaseResult.Continue; }

        /// <summary>EF 0x26c (Hidden Water, vf15): fn_b6c9a0(ring_blue, 14).</summary>
        [FxEffectProcess(0x26c)]
        public FxCaseResult Case_HiddenWater(FxRuntime rt) { Fn_b6c9a0(rt, "effect\\ring_blue.tga", 0xe); return FxCaseResult.Continue; }

        /// <summary>EF 0x16e (Martyr's Reckoning, vf14): sound at 0x16, 5 aura prims, owner flag 8 + tint in [20, 40].</summary>
        [FxEffectProcess(0x16e)]
        public FxCaseResult Case_MartyrsReckoning(FxRuntime rt)
        {
            if (m_stateCnt == 0x16)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavSacrifice, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            for (int i = 0; i < 5; i++) Fn_b66d40(rt, "effect\\alpha_center.tga", (sbyte)i, 0);
            if ((uint)(m_stateCnt - 0x14) > 0x14) return FxCaseResult.Continue;
            SetMasterFlag(8);
            rt.SetArgb(M.U32(A + 0x138), -1, 0xff, 200, 200);
            return FxCaseResult.Continue;
        }

        /// <summary>master+0xe8 += bit when the bit is clear.</summary>
        void SetMasterFlag(uint bit)
        {
            uint master = M.U32(A + 0x138);
            uint flags = M.U32(master + 0xe8);
            if ((flags & bit) == 0) M.U32(master + 0xe8, flags + bit);
        }

        /// <summary>EF 0x179 (Glacier Fist): sound at 5, 8 aura prims (style 2), owner flag 1 in [5, 35].</summary>
        [FxEffectProcess(0x179)]
        public FxCaseResult Case_GlacierFist(FxRuntime rt)
        {
            if (m_stateCnt == 5)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavGlacierFist, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            for (int i = 0; i < 8; i++) Fn_b66d40(rt, "effect\\alpha_center.tga", (sbyte)i, 2);
            if ((uint)(m_stateCnt - 5) < 0x1f) SetMasterFlag(1);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x182 (Falcon Eyes): sound at 0, 12 aura prims (style 3).</summary>
        [FxEffectProcess(0x182)]
        public FxCaseResult Case_FalconEyes(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative("effect\\hunter_detecting.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            for (int i = 0; i < 0xc; i++) Fn_b66d40(rt, "effect\\alpha_center.tga", (sbyte)i, 3);
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x18f (Traumatic Blow): sound at 0, 6 aura prims (style 4), owner flag 8 + tint in [20, 50].</summary>
        [FxEffectProcess(0x18f)]
        public FxCaseResult Case_TraumaticBlow(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavHeadCrush, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            for (int i = 0; i < 6; i++) Fn_b66d40(rt, "effect\\alpha_center.tga", (sbyte)i, 4);
            if ((uint)(m_stateCnt - 0x14) < 0x1f)
            {
                SetMasterFlag(8);
                rt.SetArgb(M.U32(A + 0x138), -1, 0xff, 0xff, 200);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x190 (Vital Strike): sound at 0x16, 6 aura prims (style 5), owner flag 8 + tint in [20, 50].</summary>
        [FxEffectProcess(0x190)]
        public FxCaseResult Case_VitalStrike(FxRuntime rt)
        {
            if (m_stateCnt == 0x16)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavJointBeat, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            for (int i = 0; i < 6; i++) Fn_b66d40(rt, "effect\\alpha_center.tga", (sbyte)i, 5);
            if ((uint)(m_stateCnt - 0x14) < 0x1f)
            {
                SetMasterFlag(8);
                rt.SetArgb(M.U32(A + 0x138), -1, 0xff, 0xff, 0xff);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>EF 0x274 (Dust, vf15): sound at 0, 6 aura prims (style 5).</summary>
        [FxEffectProcess(0x274)]
        public FxCaseResult Case_Dust(FxRuntime rt)
        {
            if (m_stateCnt == 0)
            {
                FollowMaster();
                rt.PlaySound3DRelative(Fx5Names.WavDust, m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            }
            for (int i = 0; i < 6; i++) Fn_b66d40(rt, "effect\\alpha_center.tga", (sbyte)i, 5);
            return FxCaseResult.Continue;
        }
    }
}
