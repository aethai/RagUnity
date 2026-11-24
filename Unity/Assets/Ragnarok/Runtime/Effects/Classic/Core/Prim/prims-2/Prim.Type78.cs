// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type78.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess case 0x78 (0xad9822, inline) — travelling emitter (fn_b89110): per active slot with
        /// started counter: on the first tick the heading towards the target (f_388/f_390 − position,
        /// FUN_00ad5710); then x/z (slot +0x84/+0x8c) += speed (slot +0x18) · cos/sin(heading).
        /// </summary>
        [FxPrimProcess(0x78)]
        public FxCaseResult Case78(FxRuntime rt)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + 0x3e8 + (uint)slot * 0xb4;
                if (M.U8(s) != 1) continue;
                int cnt = M.I32(s + 0xc) + 1;
                M.I32(s + 0xc, cnt);
                if (cnt <= 0) continue;
                if (cnt == 1) M.I16(s + 0x10, Heading_ad5710(f_388 - m_pos_x, f_390 - m_pos_z));
                MoveSlot_56(s);
            }
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x78)]
        public void Render78(FxRuntime rt, uint viewMatrix) => Render_b070a0(rt);

        /// <summary>
        /// CEffectPrim::fn_b070a0 (0xb070a0) — "Render" for prim type 0x78: draws nothing; while slot 0 is active it
        /// launches an effect on the local player (CAbleToMakeEffect::LaunchEffect 0xbd1f40) and sends it message
        /// 0xe with the slot position: kind (slot +0x68) 0 → effect 0x228 every 8th tick, 1 → 0x22c every frame,
        /// 'c' → 0x4b5 every 8th tick; kinds 0 / 'c' also pass (tick, tick % 16 ≠ 0 ? 1 : 0, 0).
        /// </summary>
        public void Render_b070a0(FxRuntime rt)
        {
            if (M.U8(A + 0x3e8) != 1) return;
            byte kind = M.U8(A + 0x450);
            int efId;
            if (kind == 0) efId = 0x228;
            else if (kind == 1) efId = 0x22c;
            else if (kind == 0x63) efId = 0x4b5;
            else return;
            int tick = M.I32(A + 0x3f4);
            if (kind != 1 && tick % 8 != 0) return;
            uint player = M.U32(M.U32(rt.Mode + 0xcc) + 0x2c);
            uint effect = rt.LaunchEffectOn(player, efId);
            if (effect == 0) return;
            // CRagEffect vf16 (0xb5e650) msg 0xe: +0x180.. = position, +0x18c.. = the extra triple (raw dwords) when given
            M.F32(effect + 0x180, M.F32(A + 0x46c));
            M.F32(effect + 0x184, M.F32(A + 0x470));
            M.F32(effect + 0x188, M.F32(A + 0x474));
            if (kind != 1)
            {
                tick = M.I32(A + 0x3f4);
                M.F32(effect + 0x18c, (float)tick);
                M.F32(effect + 0x190, tick % 16 != 0 ? 1.0f : 0f);
                M.F32(effect + 0x194, 0f);
            }
        }
    }
}
