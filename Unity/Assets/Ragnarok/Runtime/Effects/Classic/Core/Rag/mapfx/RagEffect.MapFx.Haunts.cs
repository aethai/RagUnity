// Synced from reference/port/src/RoClassicFx/Rag/mapfx/RagEffect.MapFx.Haunts.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>EF 358 / 359 / 360 — ghosts / bats circling a map spot (sprite prim type 0x54, packages/mapfx).</summary>
    public readonly partial struct RagEffect
    {
        /// <summary>EF 358 (0x166; vf14 case 0xb46c1c): fn_b77d60(this, 0) — ghosts; <c>return true</c>.</summary>
        [FxEffectProcess(0x166)]
        public FxCaseResult Case_Map358(FxRuntime rt) { Fn_b77d60(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 359 (0x167; vf14 case 0xb46c2c): fn_b77d60(this, 1) — bats; <c>return true</c>.</summary>
        [FxEffectProcess(0x167)]
        public FxCaseResult Case_Map359(FxRuntime rt) { Fn_b77d60(rt, 1); return FxCaseResult.Continue; }

        /// <summary>EF 360 (0x168; vf14 case 0xb46c3c): fn_b77d60(this, 2) — bats in figure eights; <c>return true</c>.</summary>
        [FxEffectProcess(0x168)]
        public FxCaseResult Case_Map360(FxRuntime rt) { Fn_b77d60(rt, 2); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b77d60 (0xb77d60) — follows the owner; on ticks 0..9 (signed counter &lt; 10) one sprite prim type 0x54
        /// per tick (life = the effect's): slot 0 live (+0x3e8 = 1, counter +0x3f4 = 0), scale +0x318 = 1.5, alpha +0x324 = 0,
        /// orbit centre +0x46c.. = the effect position, orbit angle +0x3f8 = 0, bob angle +0x466 = 0, angular speed +0x450 =
        /// 2 (kind 2) else 1, target alpha +0x400 = (10 − tick) · 3 (kind 2, tick 0: 250), kind +0x452, orbit radius +0x468 =
        /// 16, bob height +0x3f0 = 4; sprite 이팩트\유령.spr/.act (kind 0) or 이팩트\박쥐.spr/.act (kinds ≥ 1; negative kinds
        /// none); frame delay +0x1e8 = 2, +0x1c4 |= 0x200, SetAction(0, 0x10, 1) (vtable +0x1c), trail snapshot fn_ad58d0,
        /// slots 1..3 off. Every prim starts at the same spot, so the ten overlap until their alphas differ. No RSW param is read.
        /// </summary>
        public void Fn_b77d60(FxRuntime rt, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt >= 10) return;
            Prim p = LaunchEffectPrim(rt, 0x54, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = f_17c;
            M.I32(p.A + 0x3f4, 0);
            M.U8(p.A + 0x3e8, 1);
            p.f_318 = 1.5f;
            M.U32(p.A + 0x46c, M.U32(A + 0x10));
            M.U32(p.A + 0x470, M.U32(A + 0x14));
            M.U32(p.A + 0x474, M.U32(A + 0x18));
            M.I16(p.A + 0x3f8, 0);
            M.I16(p.A + 0x466, 0);
            if (kind == 2)
            {
                M.U8(p.A + 0x450, 2);
                if (m_stateCnt == 0) M.F32(p.A + 0x400, 250f);                          // [0xf87448]
                else M.F32(p.A + 0x400, (float)((10 - m_stateCnt) * 3));
            }
            else
            {
                M.U8(p.A + 0x450, 1);
                M.F32(p.A + 0x400, (float)((10 - m_stateCnt) * 3));
            }
            M.I8(p.A + 0x452, kind);
            M.F32(p.A + 0x468, 16f);
            M.F32(p.A + 0x3f0, 4f);
            p.f_324 = 0f;
            if (kind == 0)
            {
                rt.SetSprName(p.A, MapFxNames.GhostSpr);
                rt.SetActName(p.A, MapFxNames.GhostAct);
            }
            else if (kind >= 1)
            {
                rt.SetSprName(p.A, MapFxNames.BatSpr);
                rt.SetActName(p.A, MapFxNames.BatAct);
            }
            p.f_1e8 = 2;
            p.f_1c4 |= 0x200;
            rt.SetAction(p.A, 0, 0x10, 1);
            p.SnapshotTrail_ad58d0();
            M.U8(p.A + 0x49c, 0);
            M.U8(p.A + 0x550, 0);
            M.U8(p.A + 0x604, 0);
        }
    }
}
