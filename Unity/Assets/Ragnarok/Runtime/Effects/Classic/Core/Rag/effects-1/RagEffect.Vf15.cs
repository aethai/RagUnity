// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.Vf15.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-1: ids &gt; 0x1ea (CRagEffect::vf15 0xb47410). vf15 returns 1 for every case below
    /// (STR results inside it are ignored), so they all map to Continue.
    /// </summary>
    public readonly partial struct RagEffect
    {
        // ---- EF 495 Divest All / EF 627 Disarm ---------------------------------------------------

        /// <summary>vf15 case 0x1ef — Divest All: sound at ticks 1/11/21/31; 4× (shield, sword) debris prims.</summary>
        [FxEffectProcess(495)]
        public FxCaseResult Case_DivestAll(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 1 || t == 0xb || t == 0x15 || t == 0x1f) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff7b48, 0f);
            for (int n = 4; n != 0; n--)
            {
                Fn_b86e70(rt, "effect\\shield.bmp", 5.0f, 0);
                Fn_b86e70(rt, "effect\\sword.bmp", 9.0f, 0);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>vf15 case 0x273 — Disarm: sound at tick 21; four item-icon debris prims (5.0).</summary>
        [FxEffectProcess(627)]
        public FxCaseResult Case_Disarm(FxRuntime rt)
        {
            if (m_stateCnt == 0x15) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff8500, 0f);
            Fn_b86e70(rt, Effects1Strings.Bmp_ff8514, 5.0f, 0);
            Fn_b86e70(rt, Effects1Strings.Bmp_ff8538, 5.0f, 0);
            Fn_b86e70(rt, Effects1Strings.Bmp_ff855c, 5.0f, 0);
            Fn_b86e70(rt, Effects1Strings.Bmp_ff8580, 5.0f, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b86e70 (0xb86e70) — at tick 0: follow the owner, prim 0x2b textured
        /// <paramref name="texture"/>, effect y -= 12, four flying debris blocks aimed at +0x180..+0x188 with
        /// random delay (-18..-47), headings and spin; <paramref name="speed"/> at block +0x18.
        /// </summary>
        public void Fn_b86e70(FxRuntime rt, string texture, float speed, byte kind)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x2b, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            SetFirstTexture(rt, p, texture);
            m_pos_y = m_pos_y - 12.0f;
            for (uint i = 0; i < 4; i++)
            {
                uint b = p.A + 0x3e8 + i * 0xb4;
                M.U8(b + 0x0, 1);
                M.U32(b + 0x84, M.U32(A + 0x180));
                M.U32(b + 0x88, M.U32(A + 0x184));
                M.U32(b + 0x8c, M.U32(A + 0x188));
                M.I32(b + 0xc, -0x12 - RandMod(rt, 0x1e));
                M.U32(b + 0x80, 0x41400000);
                M.U32(b + 0x14, 0x3f800000);
                M.U16(b + 0x7e, (ushort)RandMod(rt, 0x168));
                M.U16(b + 0x6, (ushort)RandMod(rt, 0x168));
                M.U16(b + 0x10, (ushort)RandMod(rt, 0x168));
                M.F32(b + 0x1c, (float)RandMod(rt, 0x168));
                M.F32(b + 0x18, speed);
                M.U16(b + 0x2, 0);
                M.U8(b + 0x68, kind == 1 ? (byte)6 : kind == 7 ? (byte)7 : (byte)2);
            }
        }

        // ---- EF 501 Stealth ------------------------------------------------------------------------

        /// <summary>
        /// vf15 case 0x1f5 — Stealth: tick 0 snap + sound; unless the owner is busy (+0x70), spin the owner's
        /// heading +0x4c by 12/18/24/48 degrees per tick (ticks ≤15/≤30/≤40/later), wrapped above 360.
        /// </summary>
        [FxEffectProcess(501)]
        public FxCaseResult Case_Stealth(FxRuntime rt)
        {
            if (m_stateCnt == 0) FollowMasterAndPlay(rt, "effect\\EF_BeginSpell.wav", 0f);
            uint master = M.U32(A + 0x138);
            if (M.I32(master + 0x70) != 0) return FxCaseResult.Continue;
            int t = m_stateCnt;
            float step = t <= 0xf ? 12.0f : t <= 0x1e ? 18.0f : t <= 0x28 ? 24.0f : 48.0f;
            M.F32(master + 0x4c, M.F32(master + 0x4c) + step);
            master = M.U32(A + 0x138);
            float h = M.F32(master + 0x4c);
            if (h > 360.0f) M.F32(master + 0x4c, h - 360.0f);
            return FxCaseResult.Continue;
        }

        // ---- EF 599 Shrink -------------------------------------------------------------------------

        /// <summary>vf15 case 0x257 — Shrink: sound at tick 5; ticks 5..25 blink the owner (flag 1, 250/250/100 vs white).</summary>
        [FxEffectProcess(599)]
        public FxCaseResult Case_Shrink(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 5)
            {
                rt.PlaySound3DRelative("effect\\EF_BeginSpell.wav", m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if ((uint)(t - 5) > 0x14u) return FxCaseResult.Continue;
            uint master = M.U32(A + 0x138);
            if ((t & 1) != 0)
            {
                rt.SetArgb(master, -1, 0xff, 0xff, 0xff);
                return FxCaseResult.Continue;
            }
            AddMasterFlag_effects1(1);
            rt.SetArgb(M.U32(A + 0x138), -1, 0xfa, 0xfa, 100);
            return FxCaseResult.Continue;
        }

        // ---- EF 635 Raging Fire Dragon / EF 649 Bulls Eye -------------------------------------------

        /// <summary>vf15 case 0x27b — tick 0 snap + sound; STR (result ignored).</summary>
        [FxEffectProcess(635)]
        public FxCaseResult Case_RagingFireDragon(FxRuntime rt)
        {
            if (m_stateCnt == 0) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff84c4, 0f);
            rt.StrPlayer(this, 0f, -80.0f, 0f);
            return FxCaseResult.Continue;
        }

        /// <summary>vf15 case 0x289 (shared label code_r0x00b48e3a): STR only, result ignored.</summary>
        [FxEffectProcess(649)]
        public FxCaseResult Case_BullsEye(FxRuntime rt)
        {
            rt.StrPlayer(this, 0f, -80.0f, 0f);
            return FxCaseResult.Continue;
        }

        // ---- EF 518 Cart Termination -------------------------------------------------------------

        /// <summary>vf15 case 0x206 — Cart Termination: tick 30 snap + fire pillar sound; 4× fn_b83440(whitelight).</summary>
        [FxEffectProcess(518)]
        public FxCaseResult Case_CartTermination(FxRuntime rt)
        {
            if (m_stateCnt == 0x1e) FollowMasterAndPlay(rt, "effect\\wizard_fire_pillar_b.wav", 0f);
            for (int n = 4; n != 0; n--) Fn_b83440(rt, "effect\\whitelight.tga", 0);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b83440 (0xb83440) — follows the owner; at tick 30 a prim 0x73 with four blocks of
        /// three random rotation triples (0..359, 0..179, radius 230) and a random 1..3 at +0x50.
        /// (The second argument is not used.)
        /// </summary>
        public void Fn_b83440(FxRuntime rt, string texture, int unused)
        {
            FollowMaster();
            if (m_stateCnt != 0x1e) return;
            Prim p = LaunchEffectPrim(rt, 0x73, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            SetFirstTexture(rt, p, texture);
            M.U32(p.A + 0x318, 0);
            for (uint i = 0; i < 4; i++)
            {
                uint b = p.A + 0x3e8 + i * 0xb4;
                M.U8(b + 0x0, 1);
                M.U32(b + 0x8, 0x3f800000);
                M.F32(b + 0x80, (float)RandMod(rt, 6) * 0.1f + 2.5f);
                M.U32(b + 0xc, 0);
                M.U32(b + 0x9c, 0); M.U32(b + 0xa0, 0); M.U32(b + 0xa4, 0);
                M.F32(b + 0x14, (float)RandMod(rt, 0x168));
                M.F32(b + 0x1c, (float)RandMod(rt, 0xb4));
                M.U32(b + 0x44, 0x43660000);
                M.U32(b + 0x84, 0); M.U32(b + 0x88, 0); M.U32(b + 0x8c, 0);
                M.F32(b + 0x24, (float)RandMod(rt, 0x168));
                M.F32(b + 0x2c, (float)RandMod(rt, 0xb4));
                M.U32(b + 0x48, 0x43660000);
                M.U32(b + 0xa8, 0); M.U32(b + 0xac, 0); M.U32(b + 0xb0, 0);
                M.F32(b + 0x34, (float)RandMod(rt, 0x168));
                M.F32(b + 0x3c, (float)RandMod(rt, 0xb4));
                M.U32(b + 0x4c, 0x43660000);
                M.F32(b + 0x50, (float)(RandMod(rt, 3) + 1));
            }
        }

        // ---- EF 547 Estin --------------------------------------------------------------------------

        /// <summary>vf15 case 0x223 — Estin: tick 10 snap + sound; fn_b89e40(270바람.tga).</summary>
        [FxEffectProcess(547)]
        public FxCaseResult Case_Estin(FxRuntime rt)
        {
            if (m_stateCnt == 10) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff8890, 0f);
            Fn_b89e40(rt, Effects1Strings.Tga_ff85b8);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b89e40 (0xb89e40) — follows the owner; at tick 0 a prim 0x77 placed on the effect,
        /// aimed at +0x180..+0x188, with four staggered (delay i-3) wind blocks around the effect position.
        /// </summary>
        public void Fn_b89e40(FxRuntime rt, string texture)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x77, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 1;
            SetFirstTexture(rt, p, texture);
            M.U32(p.A + 0x318, 0);
            M.U32(p.A + 0x388, M.U32(A + 0x180));
            M.U32(p.A + 0x38c, M.U32(A + 0x184));
            M.U32(p.A + 0x390, M.U32(A + 0x188));
            M.U32(p.A + 0x10, M.U32(A + 0x10));
            M.U32(p.A + 0x14, M.U32(A + 0x14));
            M.U32(p.A + 0x18, M.U32(A + 0x18));
            for (int i = 0; i < 4; i++)
            {
                uint b = p.A + 0x3e8 + (uint)i * 0xb4;
                M.U8(b + 0x0, 1);
                M.I32(b + 0xc, i - 3);
                M.U32(b + 0x80, 0x41a00000);
                M.U16(b + 0x2, 0);
                M.U32(b + 0x84, M.U32(A + 0x10));
                M.U32(b + 0x8c, M.U32(A + 0x18));
                int r = RandMod(rt, 5);
                float y = m_pos_y - 10.0f;
                M.F32(b + 0x88, (float)r + y);
                M.U16(b + 0x10, 0);
                M.U16(b + 0x7e, (ushort)i);
                M.U32(b + 0x18, 0x3f4ccccd);
                M.U8(b + 0x68, 0);
                M.U8(b + 0x6b, 0);
            }
        }
    }
}
