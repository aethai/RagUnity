// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.LightShafts.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>EF 307 / 322 / 323 / 324 — slanted light shafts (cloud11.tga tubes) of the maps (prim type 0x42, packages/mapfx).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 307 (0x133; vf14 case 0xb46569): fn_b77010(this, "effect\cloud11.tga", 0); <c>return true</c>.</summary>
        [FxEffectProcess(0x133)]
        public FxCaseResult Case_Map307(FxRuntime rt) { Fn_b77010(rt, MapFxNames.Cloud11, 0); return FxCaseResult.Continue; }

        /// <summary>EF 322 (0x142; vf14 case 0xb4657e): fn_b77010(this, "effect\cloud11.tga", 1); <c>return true</c>.</summary>
        [FxEffectProcess(0x142)]
        public FxCaseResult Case_Map322(FxRuntime rt) { Fn_b77010(rt, MapFxNames.Cloud11, 1); return FxCaseResult.Continue; }

        /// <summary>EF 323 (0x143; vf14 case 0xb46593): fn_b77010(this, "effect\cloud11.tga", 2); <c>return true</c>.</summary>
        [FxEffectProcess(0x143)]
        public FxCaseResult Case_Map323(FxRuntime rt) { Fn_b77010(rt, MapFxNames.Cloud11, 2); return FxCaseResult.Continue; }

        /// <summary>EF 324 (0x144; vf14 case 0xb465a8): fn_b77010(this, "effect\cloud11.tga", 10); <c>return true</c>.</summary>
        [FxEffectProcess(0x144)]
        public FxCaseResult Case_Map324(FxRuntime rt) { Fn_b77010(rt, MapFxNames.Cloud11, 10); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b77010 (0xb77010) — on tick 0 only: position = the owner's, one prim type 0x42 (RP flags +0x1c4 = 5,
        /// life = the effect's) with texture 0 = <paramref name="texture"/> (array of +0x204 entries). Four slots of 0xb4
        /// (all cleared, then each): live +0x3e8 = 1, counter +0x3f4 = 0, alpha +0x3ea = 30 (kinds 2 / 12 / 13), 25 (10),
        /// 0 (11) or 40, +0x468 = 0, radius +0x3f0 = k·2 + 4 (kinds 1 / 11), k·0.5 + 1 (2 / 12 / 13) or k + 2 (slot 3:
        /// 4 / 1 / 2 and its counter +0x3f4 = 180), +0x3fc = the radius, angle +0x3f8 = 25k, bottom ring centre
        /// +0x478.. = the effect position, top ring centre +0x490.. = the effect position − (70, 300, 70) (kind 10:
        /// − (15, 60, 15); also left in the effect's +0x180..+0x188), kind +0x450. No RSW param is read.
        /// </summary>
        public void Fn_b77010(FxRuntime rt, string texture, byte kind)
        {
            if (m_stateCnt != 0) return;
            FollowMaster();
            Prim p = LaunchEffectPrim(rt, 0x42, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p.m_duration = f_17c;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));
            for (uint o = 0; o < 0x2d0; o += 0xb4) M.U8(p.A + o + 0x3e8, 0);

            bool narrow = kind == 2 || kind == 0xc || kind == 0xd;
            bool wide = kind == 1 || kind == 0xb;
            for (int k = 0; k < 4; k++)
            {
                uint s = p.A + (uint)k * 0xb4;
                M.U8(s + 0x3e8, 1);
                M.I32(s + 0x3f4, 0);
                short alpha = narrow ? (short)0x1e : kind == 0xa ? (short)0x19 : kind == 0xb ? (short)0 : (short)0x28;
                M.I16(s + 0x3ea, alpha);
                M.U32(s + 0x468, 0);
                if (k == 3)
                {
                    M.I32(p.A + 0x610, 0xb4);
                    M.F32(p.A + 0x60c, wide ? 4f : narrow ? 1f : 2f);
                }
                else
                {
                    float r;
                    if (wide) r = (float)(k * 2) + 4f;                                  // [0xf4a764]
                    else if (narrow) r = (float)k * 0.5f + 1f;                          // [0xf4a460], [0xf4a468]
                    else r = (float)k + 2f;                                             // [0xf4b450]
                    M.F32(s + 0x3f0, r);
                }
                M.U32(s + 0x3fc, M.U32(s + 0x3f0));
                M.I16(s + 0x3f8, (short)(k * 0x19));
                M.U32(s + 0x478, M.U32(A + 0x10));
                M.U32(s + 0x47c, M.U32(A + 0x14));
                M.U32(s + 0x480, M.U32(A + 0x18));
                float x = m_pos_x;
                M.F32(A + 0x180, x);
                M.U32(A + 0x184, M.U32(A + 0x14));
                M.U32(A + 0x188, M.U32(A + 0x18));
                float y = M.F32(A + 0x184), z = M.F32(A + 0x188);
                if (kind == 0xa)
                {
                    y = y - 60f;                                                        // [0xf7cd54]
                    z = z - 15f;                                                        // [0xf7e280]
                    x = x - 15f;
                }
                else
                {
                    y = y - 300f;                                                       // [0xfadc88]
                    z = z - 70f;                                                        // [0xff5368]
                    x = x - 70f;
                }
                M.F32(A + 0x184, y);
                M.F32(A + 0x180, x);
                M.F32(A + 0x188, z);
                M.U32(s + 0x490, M.U32(A + 0x180));
                M.U32(s + 0x494, M.U32(A + 0x184));
                M.U32(s + 0x498, M.U32(A + 0x188));
                M.U8(s + 0x450, kind);
            }
        }
    }
}
