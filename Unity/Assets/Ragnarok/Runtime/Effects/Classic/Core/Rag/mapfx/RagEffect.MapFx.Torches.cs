// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.Torches.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>EF 689 / 690 / 691 / 696 — the animated flat torch / dust flames of the maps (prim type 0x80, packages/mapfx).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 689 (0x2b1; vf15 case 0xb4ac18): fn_b713d0(this, 0) — dust01..09.bmp.</summary>
        [FxEffectProcess(0x2b1)]
        public FxCaseResult Case_Map689(FxRuntime rt) { Fn_b713d0(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 690 (0x2b2; vf15 case 0xb4ac24): fn_b713d0(this, 1) — torch_red01..13.bmp.</summary>
        [FxEffectProcess(0x2b2)]
        public FxCaseResult Case_Map690(FxRuntime rt) { Fn_b713d0(rt, 1); return FxCaseResult.Continue; }

        /// <summary>EF 691 (0x2b3; vf15 case 0xb4ac30): fn_b713d0(this, 2) — torch_green01..13.bmp.</summary>
        [FxEffectProcess(0x2b3)]
        public FxCaseResult Case_Map691(FxRuntime rt) { Fn_b713d0(rt, 2); return FxCaseResult.Continue; }

        /// <summary>EF 696 (0x2b8; vf15 case 0xb4ac3c): fn_b713d0(this, 3) — torch_violet01..13.bmp.</summary>
        [FxEffectProcess(0x2b8)]
        public FxCaseResult Case_Map696(FxRuntime rt) { Fn_b713d0(rt, 3); return FxCaseResult.Continue; }

        static readonly string[] Dust_b713d0 =
        {
            "effect\\dust01.bmp", "effect\\dust02.bmp", "effect\\dust03.bmp", "effect\\dust04.bmp", "effect\\dust05.bmp",
            "effect\\dust06.bmp", "effect\\dust07.bmp", "effect\\dust08.bmp", "effect\\dust09.bmp",
        };

        static readonly string[] TorchRed_b713d0 = TorchFrames_b713d0("red");
        static readonly string[] TorchGreen_b713d0 = TorchFrames_b713d0("green");
        static readonly string[] TorchViolet_b713d0 = TorchFrames_b713d0("violet");

        static readonly string[] LordStick_b713d0 =
        {
            "effect\\lord_stick_a.bmp", "effect\\lord_stick_b.bmp", "effect\\lord_stick_c.bmp", "effect\\lord_stick_e.bmp",
            "effect\\lord_stick_f.bmp", "effect\\lord_stick_g.bmp", "effect\\lord_stick_h.bmp", "effect\\lord_stick_plazma_a.bmp",
            "effect\\lord_stick_plazma_b.bmp", "effect\\lord_stick_plazma_c.bmp", "effect\\lord_blast_stick_a.bmp",
        };

        /// <summary>effect\torch_&lt;colour&gt;01.bmp … 13.bmp (0xffb944.. red, 0xffbb8c.. green, 0xffbcf8.. violet).</summary>
        static string[] TorchFrames_b713d0(string colour)
        {
            var r = new string[13];
            for (int i = 0; i < 13; i++) r[i] = "effect\\torch_" + colour + (i + 1).ToString("00") + ".bmp";
            return r;
        }

        /// <summary>
        /// CRagEffect::fn_b713d0 (0xb713d0) — follows the owner; on tick 0 one prim type 0x80 (RP flags +0x1c4 = 5, life =
        /// the effect's) with a texture animation in +0x144 (count +0x204): kind 0 dust01..09, 1 torch_red01..13,
        /// 2 torch_green01..13, 3 torch_violet01..13, 4 lord_stick a..h / plazma / blast (11), other kinds none. Slot 0
        /// only (+0x3e8 = 1, slots 1..3 off), counter +0x3f4 = 0, size +0x468 = the RSW param p0 (effect +0x13c; ≤ 0 → 20,
        /// NaN kept), y +0x488 = effect y − 10, style +0x452 = 4, angle step +0x424 = 0 (kind 4: 1), +0x41c = 0, start
        /// angle +0x3ee = 135 (kind 4: 120), alpha +0x3ea = 100 (kind 0) / 130, x/z = effect x/z, +0x3f8 = rand % 360
        /// (unused by type 0x80), frame +0x450 = 0, kind +0x454.
        /// </summary>
        public void Fn_b713d0(FxRuntime rt, byte kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x80, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            string[] tex = kind switch
            {
                0 => Dust_b713d0,
                1 => TorchRed_b713d0,
                2 => TorchGreen_b713d0,
                3 => TorchViolet_b713d0,
                4 => LordStick_b713d0,
                _ => null,
            };
            if (tex != null)
            {
                p.f_204 = tex.Length;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                for (int i = 0; i < tex.Length; i++) M.U32((uint)p.f_144 + (uint)i * 4, rt.GetTexture(tex[i]));
            }

            p.f_318 = 0f;
            M.U8(p.A + 0x3e8, 0);
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            M.I32(p.A + 0x3f4, 0);
            M.U8(p.A + 0x3e8, 1);
            float size = f_13c;
            if (0f < size || float.IsNaN(size)) M.U32(p.A + 0x468, M.U32(A + 0x13c));   // COMISS 0, p0; JC keeps it
            else M.F32(p.A + 0x468, 20f);                                               // [0xf7d134]
            M.F32(p.A + 0x488, m_pos_y - 10f);                                          // [0xf4b624]
            M.U8(p.A + 0x452, 4);
            if (kind == 4)
            {
                M.I16(p.A + 0x3ee, 0x78);
                M.F32(p.A + 0x424, 1f);
                M.F32(p.A + 0x41c, 0f);
                M.I16(p.A + 0x3ea, 0x82);
            }
            else
            {
                M.I16(p.A + 0x3ee, 0x87);
                M.F32(p.A + 0x424, 0f);
                M.F32(p.A + 0x41c, 0f);
                M.I16(p.A + 0x3ea, (short)(kind == 0 ? 100 : 0x82));
            }
            M.U32(p.A + 0x46c, M.U32(A + 0x10));
            M.U32(p.A + 0x474, M.U32(A + 0x18));
            M.I16(p.A + 0x3f8, (short)RandMod(rt, 0x168));
            M.U8(p.A + 0x450, 0);
            M.U8(p.A + 0x454, kind);
        }
    }
}
