// Synced from reference/port/src/RoClassicFx/Rag/RagEffect.Process.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::OnProcess (0xb527f0): prologue (master check, ids processed even on frames without a
        /// logic tick, actor flag reset), the per-id case (ported handlers via FxDispatch, the generated
        /// "pure STR" table) and the shared epilogue. Returns false when the effect must be deleted.
        /// Unported ids return false (callers should not launch them; see FxWorld.IsPorted).
        /// </summary>
        public bool OnProcess(FxRuntime rt)
        {
            uint master = M.U32(A + 0x138);
            if (master == 0) return false;
            int type = m_type;
            if ((!rt.ProcessOnTicklessFrames || !ProcessesWithoutTick(type)) && rt.TicksThisFrame == 0) return true;
            if (0 < rt.TicksThisFrame && M.U8(master + 0xec) == 1)
            {
                M.U32(master + 0xe8, 0);
                M.U8(master + 0xec, 0);
            }

            FxCaseResult result;
            FxCaseResult? ported = FxDispatch.EffectCase(this, rt);
            if (ported.HasValue) result = ported.Value;
            else if (ClassicEffectTables.PureStr.TryGetValue(type, out var entry))
            {
                if (entry.Wav != null && m_stateCnt == entry.SoundTick)
                    rt.PlaySound3DRelative(entry.Wav, m_pos_x, entry.SoundY, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                result = rt.StrPlayer(this, 0f, -80.0f, 0f) ? FxCaseResult.Continue : FxCaseResult.Dead;
            }
            else return false;

            switch (result)
            {
                case FxCaseResult.Dead: return false;
                case FxCaseResult.AliveNoIncrement: return true;
                default:
                    m_stateCnt = m_stateCnt + 1;
                    return m_stateCnt < f_17c;
            }
        }

        /// <summary>The OnProcess prologue's lists: these ids run even when DAT_01555ec4 (ticks this frame) is 0.</summary>
        public static bool ProcessesWithoutTick(int type)
        {
            if (type < 0x139)
            {
                switch (type)
                {
                    case 0x138: case 0x19: case 0x25: case 0x31: case 0x33: case 0x34: case 0x35: case 0x93: case 0x98:
                    case 0xc8: case 0xc9: case 0xca: case 0xcb: case 0xe4: case 0xfe:
                        return true;
                    default: return false;   // including 0xa4
                }
            }
            if (type < 0x24f)
            {
                switch (type)
                {
                    case 0x24e: case 0x139: case 0x140: case 0x145: case 0x148: case 0x16a: case 0x18d: case 0x18e: case 0x1a2:
                    case 0x1b4: case 0x1b5: case 0x1b6: case 0x1b9: case 0x1c6: case 0x1d3: case 0x1d4: case 0x1d5: case 0x1d6:
                    case 0x1d7: case 0x1d8: case 0x1d9: case 0x1e8: case 0x1e9: case 0x1ea: case 0x1f7: case 0x1f8: case 0x1fa:
                    case 0x20b: case 0x20c: case 0x20d: case 0x20e: case 0x20f: case 0x210: case 0x211: case 0x212: case 0x213:
                    case 0x214: case 0x215: case 0x216: case 0x217: case 0x218:
                        return true;
                    default: return false;
                }
            }
            if (type < 0x35e)
            {
                switch (type)
                {
                    case 0x35d: case 0x25b: case 0x275: case 0x2a6: case 0x2a7: case 0x2a8: case 0x2f3: case 0x2f5: case 0x314:
                    case 0x315: case 0x316: case 0x33a: case 0x341: case 0x345: case 0x352:
                        return true;
                    default: return false;
                }
            }
            if (type < 0x463)
            {
                switch (type)
                {
                    case 0x462: case 0x361: case 0x363: case 0x367: case 0x369: case 0x36b: case 0x36d: case 0x371: case 0x38f:
                    case 0x398: case 0x3a4: case 0x3ac:
                        return true;
                    default: return false;
                }
            }
            return false;
        }

        /// <summary>CRagEffect::fn_b62540 case (EF_STORMGUST): result of the handler, then the shared tail.</summary>
        [FxEffectProcess(89)]
        public FxCaseResult Case_StormGust(FxRuntime rt) => Process_StormGust(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;
    }
}
