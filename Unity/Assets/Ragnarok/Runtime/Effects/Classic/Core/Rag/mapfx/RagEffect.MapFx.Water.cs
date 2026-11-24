// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.Water.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>EF 821 / 838 — the animated water plates of Malaya (prim type 0x8e) and the Brasilis waterfall curtains (prim type 0x8f), packages/mapfx.</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 821 (0x335; vf15 case 0xb48b74): FUN_00ba0190(this) (thiscall, a dummy 0 pushed, RET 4).</summary>
        [FxEffectProcess(0x335)]
        public FxCaseResult Case_Map821(FxRuntime rt) { Fn_ba0190(rt); return FxCaseResult.Continue; }

        /// <summary>EF 838 (0x346; vf15 case 0xb48b80): fn_b93c20(this, "" (0xf432d8, unused), 2, 0).</summary>
        [FxEffectProcess(0x346)]
        public FxCaseResult Case_Map838(FxRuntime rt) { Fn_b93c20(rt, 2, 0); return FxCaseResult.Continue; }

        /// <summary>sprintf(<paramref name="format"/> with "%02d" = first … first + count − 1).</summary>
        static string[] Frames_mapfx(string prefix, string suffix, int first, int count)
        {
            var r = new string[count];
            for (int i = 0; i < count; i++) r[i] = prefix + (first + i).ToString("00") + suffix;
            return r;
        }

        /// <summary>The 117 textures of prim 0x8e: W_C_water_00..29, W_B_water_00..29, W_bubble01..27, W_A_water_00..29.</summary>
        static readonly string[] Water_ba0190 = BuildWater_ba0190();

        static string[] BuildWater_ba0190()
        {
            var r = new string[0x75];
            Frames_mapfx("effect\\W_C_water_", ".tga", 0, 0x1e).CopyTo(r, 0);                   // 0xffd618
            Frames_mapfx("effect\\W_B_water_", ".tga", 0, 0x1e).CopyTo(r, 0x1e);                // 0xffd634
            Frames_mapfx("effect\\W_bubble", ".tga", 1, 0x1b).CopyTo(r, 0x3c);                  // 0xfe20ec
            Frames_mapfx("effect\\W_A_water_", ".tga", 0, 0x1e).CopyTo(r, 0x57);                // 0xffd650
            return r;
        }

        /// <summary>effect\W_c_00..50.tga (0xffd688).</summary>
        static readonly string[] Curtain_b93c20 = Frames_mapfx("effect\\W_c_", ".tga", 0, 0x33);

        /// <summary>
        /// FUN_00ba0190 (0xba0190; a CRagEffect thiscall) — on tick 0 only: position = the owner's, one prim type 0x8e (RP flags
        /// +0x1c4 = 5, life = the effect's) with 117 textures (+0x204 = 0x75, <see cref="Water_ba0190"/>); slot 0 only
        /// (+0x3e8 = 1, slots 1..3 off), counter +0x3f4 = 0, alpha +0x3ea = 230, +0x474 = 0, size +0x468 = p0 (effect +0x13c;
        /// below 20 → 40, NaN kept: COMISS 20, p0; JBE), angle +0x3f8 = (short)(int)p1 (+0x140) when p1 &lt; 360 or NaN
        /// (→ 0x80000000 → 0), else 0.
        /// </summary>
        public void Fn_ba0190(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x8e, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_204 = 0x75;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            for (int i = 0; i < Water_ba0190.Length; i++) M.U32((uint)p.f_144 + (uint)i * 4, rt.GetTexture(Water_ba0190[i]));
            M.U8(p.A + 0x3e8, 0);
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            M.U8(p.A + 0x3e8, 1);
            M.I32(p.A + 0x3f4, 0);
            M.I16(p.A + 0x3ea, 0xe6);
            M.U32(p.A + 0x474, 0);
            float size = f_13c;
            if (20f > size) M.F32(p.A + 0x468, 40f);                                    // [0xf7d134] = 20; COMISS 20, p0; JBE
            else M.U32(p.A + 0x468, M.U32(A + 0x13c));
            float ang = f_140;
            int a = (ang < 360f || float.IsNaN(ang)) ? FxOps.Cvtt(ang) : 0;             // [0xf56528] = 360; COMISS p1, 360; JB
            M.I16(p.A + 0x3f8, (short)a);
        }

        /// <summary>
        /// CRagEffect::fn_b93c20 (0xb93c20; the texture argument is never read) — follows the owner; on tick 0 only one prim type
        /// 0x8f (RP flags +0x1c4 = 5, life = the effect's) with a texture animation: kind 1 torch_red01..13 (13), kind 2
        /// W_c_00..50 (51), else dust01..09 (9). Slot 0 only (+0x3e8 = 1, slots 1..3 off), counter +0x3f4 = 0, alpha +0x3ea =
        /// 200. Kind 2: size +0x468 = p0 (effect +0x13c; below 5 → 20, NaN kept), angle +0x3f8 = (short)(int)p1 (+0x140; below 0
        /// or ≥ 360 → 45, NaN → 0), +0x404 = 1 (static: no spin in fn_b0ed30); other kinds: size 18, angle =
        /// <paramref name="angle"/>, +0x404 = 0. Then +0x466 = rand() % 360 (16-bit), +0x3fc = −2, +0x400 = 0.
        /// </summary>
        public void Fn_b93c20(FxRuntime rt, sbyte kind, short angle)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            Prim p = LaunchEffectPrim(rt, 0x8f, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            string[] tex = kind == 1 ? TorchRed_b713d0 : kind == 2 ? Curtain_b93c20 : Dust_b713d0;
            p.f_204 = tex.Length;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            for (int i = 0; i < tex.Length; i++) M.U32((uint)p.f_144 + (uint)i * 4, rt.GetTexture(tex[i]));
            M.U8(p.A + 0x3e8, 1);
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
            M.I32(p.A + 0x3f4, 0);
            if (kind == 2)
            {
                if (5f > f_13c) M.F32(p.A + 0x468, 20f);                                // [0xf80720] = 5, [0xf7d134] = 20; JBE keeps NaN
                else M.U32(p.A + 0x468, M.U32(A + 0x13c));
                float a = f_140;
                int v;
                if (0f > a) v = 0x2d;                                                   // COMISS 0, p1; JA
                else if (a < 360f || float.IsNaN(a)) v = FxOps.Cvtt(a);                 // COMISS p1, 360; JB
                else v = 0x2d;
                M.I16(p.A + 0x3f8, (short)v);
                M.I16(p.A + 0x3ea, 200);
                M.F32(p.A + 0x404, 1f);
            }
            else
            {
                M.U32(p.A + 0x468, 0x41900000);                                         // 18.0
                M.I16(p.A + 0x3f8, angle);
                M.I16(p.A + 0x3ea, 200);
                M.F32(p.A + 0x404, 0f);
            }
            M.I16(p.A + 0x466, (short)RandMod(rt, 0x168));
            M.U32(p.A + 0x3fc, 0xc0000000);                                             // -2.0
            M.U32(p.A + 0x400, 0);
        }
    }
}
