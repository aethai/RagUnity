// Synced from reference/port/src/RoClassicFx/Prim/mapfx/Prim.Type54.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x54 — the circling ghost / bat sprites of EF 358 / 359 / 360 (launched by CRagEffect::fn_b77d60). Slot 0:
    /// counter +0x3f4, orbit centre +0x46c.., orbit angle +0x3f8 (speed +0x450), radius +0x468, bob angle +0x466 / height
    /// +0x3f0, target alpha +0x400, kind +0x452; draw position +0x484.. (= +0x388..).
    /// </summary>
    public readonly partial struct Prim
    {
        /// <summary>CEffectPrim::OnProcess case 0x54: <c>fn_ae55a0(this); break;</c> (the result is ignored).</summary>
        [FxPrimProcess(0x54)]
        public FxCaseResult Case54(FxRuntime rt)
        {
            Process_ae55a0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>CEffectPrim::Render case 0x54 (with 0x55, 0x7c): fn_af24e0 (prims-sprites, the sprite trail renderer).</summary>
        [FxPrimRender(0x54)]
        public void Render54(FxRuntime rt, uint viewMatrix) => Render_af24e0(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::fn_ae55a0 (0xae55a0) — position = the master's; slot 0 live (+0x3e8 == 1): counter +0x3f4 +1; up to
        /// tick 250 the alpha +0x324 rises by 1 while below the target +0x400; from tick 2600 kinds ≤ 1 speed up (+0x450 +1
        /// every 360 / speed ticks) and widen (radius ·= 1.002 every tick), and every 15th tick the alpha drops by 1 (kind 2:
        /// 5), clamped at 0. Bob angle +0x466 += 3, orbit angle +0x3f8 += speed (both −360 at ≥ 360); position x = centre x +
        /// radius · cos[angle], z = centre z + radius · sin[angle] (kind 2: sin[2·angle mod 360] — a figure eight), y = centre
        /// y + sin[bob] · height. Facing (action +0x38): kind 0 by the angle + camera yaw (view +0x30) mod 360: 180..269 → 6,
        /// 90..179 → 0, else 6 above 89 / 0; kinds ≠ 0: rand % 999 == 1 → 1, then rand % 1001 == 0 → 0. Then m_pos = +0x388..,
        /// alpha / ARGB fn_aed050, trail snapshot fn_b0d680, ACT frame advance fn_ad5600 (its result returned).
        /// </summary>
        public bool Process_ae55a0(FxRuntime rt)
        {
            FollowMaster_P1();
            if (M.U8(A + 0x3e8) == 1)
            {
                int cnt = M.I32(A + 0x3f4) + 1;
                M.I32(A + 0x3f4, cnt);
                if (cnt <= 0xfa)
                {
                    float a = M.F32(A + 0x324);
                    if (M.F32(A + 0x400) > a) M.F32(A + 0x324, a + 1f);                // COMISS; JBE
                }
                if (cnt >= 0xa28)
                {
                    sbyte kind = (sbyte)M.U8(A + 0x452);
                    if (kind <= 1)
                    {
                        sbyte speed = (sbyte)M.U8(A + 0x450);
                        int period = 0x168 / speed;
                        if (cnt % period == 0) M.U8(A + 0x450, (byte)(speed + 1));
                        M.F32(A + 0x468, M.F32(A + 0x468) * 1.002f);                   // [0xff7504]
                    }
                    if (cnt % 0xf == 0)
                    {
                        float a = M.F32(A + 0x324);
                        a = kind == 2 ? a - 5f : a - 1f;                                 // [0xf80720] / [0xf4a468]
                        M.F32(A + 0x324, a);
                        if (0f > a) M.F32(A + 0x324, 0f);
                    }
                }
                short bob = (short)(M.I16(A + 0x466) + 3);
                M.I16(A + 0x466, bob);
                if (bob >= 0x168) M.I16(A + 0x466, (short)(bob - 0x168));
                short ang = (short)((sbyte)M.U8(A + 0x450) + M.I16(A + 0x3f8));
                M.I16(A + 0x3f8, ang);
                if (ang >= 0x168) M.I16(A + 0x3f8, (short)(ang - 0x168));

                float cx = M.F32(A + 0x46c);
                M.F32(A + 0x484, cx);
                M.U32(A + 0x488, M.U32(A + 0x470));
                M.U32(A + 0x48c, M.U32(A + 0x474));
                int idx = M.I16(A + 0x3f8);
                float rad = M.F32(A + 0x468);
                float x = rad * CosAt(idx) + cx;
                M.F32(A + 0x484, x);
                if (M.U8(A + 0x452) == 2)
                {
                    int d = idx * 2;
                    idx = d < 0x168 ? d : d - 0x168;
                }
                float z = rad * SinAt(idx) + M.F32(A + 0x48c);
                M.F32(A + 0x48c, z);
                float y = SinAt(M.I16(A + 0x466)) * M.F32(A + 0x3f0) + M.F32(A + 0x488);
                M.F32(A + 0x488, y);
                M.F32(A + 0x388, x);
                M.F32(A + 0x38c, y);
                M.U32(A + 0x390, M.U32(A + 0x48c));

                uint view = M.U32(rt.Mode + 0xd0);
                int v = M.I16(A + 0x3f8) + FxOps.Cvtt(M.F32(view + 0x30));
                int w = v < 0 ? v + 0x168 : v;
                int dir = w < 0x168 ? w : w - 0x168;
                if (M.U8(A + 0x452) == 0)
                {
                    if ((uint)(dir - 0xb4) <= 0x59) M.I32(A + 0x38, 6);
                    else if ((uint)(dir - 0x5a) <= 0x59) M.I32(A + 0x38, 0);
                    else M.I32(A + 0x38, (uint)dir > 0x59 ? 6 : 0);
                }
                else
                {
                    int r = rt.Rand.Next() % 999;                                       // CMOVNS clamp at 0
                    if ((r < 0 ? 0 : r) == 1) M.I32(A + 0x38, 1);
                    r = rt.Rand.Next() % 0x3e9;
                    if ((r < 0 ? 0 : r) == 0) M.I32(A + 0x38, 0);
                }
            }
            M.U32(A + 0x10, M.U32(A + 0x388));
            M.U32(A + 0x14, M.U32(A + 0x38c));
            M.U32(A + 0x18, M.U32(A + 0x390));
            UpdateAlpha_aed050();
            SnapshotTrail_b0d680();
            return AdvanceFrame_ad5600(rt);
        }
    }
}
