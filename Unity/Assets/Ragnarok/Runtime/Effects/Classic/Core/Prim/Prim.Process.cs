// Synced from reference/port/src/RoClassicFx/Prim/Prim.Process.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::OnProcess (0xad5a30): prologue (master check, per-type no-tick rules, master-follow
        /// types), the ported main-switch case (FxDispatch) and the shared epilogue (age++, alive while age
        /// &lt;= duration). Unported types return false.
        /// </summary>
        public bool OnProcess(FxRuntime rt)
        {
            if (m_master_ptr == 0) return false;
            int type = m_type;
            switch (type)
            {
                case 0:
                    if ((!rt.ProcessOnTicklessFrames || M.I8(A + 0x3e8) < 'b') && rt.TicksThisFrame == 0) return true;
                    break;
                case 0x10:
                    if ((!rt.ProcessOnTicklessFrames || M.I8(A + 0x3e8) != 'c') && rt.TicksThisFrame == 0) return true;
                    break;
                case 0x17: case 0x1c: case 0x1d: case 0x1e: case 0x20: case 0x23: case 0x3c: case 0x44: case 0x63:
                case 0x68: case 0x69: case 0x6a: case 0x6c: case 0x6d: case 0x6f: case 0x82: case 0x83: case 0x84:
                case 0x8a: case 0x8b: case 0x90: case 0x91: case 0x92: case 0x93: case 0x95: case 0x97: case 0x9e:
                case 0xa1: case 0xab: case 0xad:
                {
                    uint master = m_master_ptr;
                    m_pos_x = M.F32(master + 0x10);
                    m_pos_y = M.F32(master + 0x14);
                    m_pos_z = M.F32(master + 0x18);
                    if (rt.TicksThisFrame == 0) return true;
                    break;
                }
                default:
                    if (rt.TicksThisFrame == 0) return true;
                    break;
            }

            FxCaseResult? result = FxDispatch.PrimCase(this, rt);
            if (!result.HasValue) return false;
            switch (result.Value)
            {
                case FxCaseResult.Dead: return false;
                case FxCaseResult.AliveNoIncrement: return true;
                default:
                    f_1e0 = f_1e0 + 1;
                    return f_1e0 <= m_duration;
            }
        }

        /// <summary>CEffectPrim::Render (0xaf4ce0) prologue (visibility, fn_b0dca0) + ported case.</summary>
        public void Render(FxRuntime rt, uint viewMatrix)
        {
            uint master = m_master_ptr;
            if (master == 0 || !rt.IsEffectVisible(new RagEffect(M, master))) return;
            FxDispatch.PrimRender(this, rt, viewMatrix);
        }

        [FxPrimProcess(9)]
        public FxCaseResult Case09(FxRuntime rt)
        {
            Process_adf860();
            return FxCaseResult.Continue;
        }

        [FxPrimRender(9, 0x75)]
        public void Render09(FxRuntime rt, uint viewMatrix) => Render_af2870(rt.Renderer, rt.Rand, viewMatrix);
    }
}
