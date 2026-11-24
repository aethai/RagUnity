// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.DirectedSprite.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b6d840 (0xb6d840) — at tick 0 a directional sprite prim (type 0x2e) between the owner and
        /// the target position f_180/f_184/f_188: sprite chosen by <paramref name="variant"/> (0 Occult Impaction
        /// with sound, 1 wink, 2 fvoice, 12 hundred spear, 13 wind cutter), action 0..3 picked from the screen
        /// direction (FUN_00ad5710 angle + camera angle at view +0x30).
        /// </summary>
        public void Fn_b6d840(FxRuntime rt, sbyte variant)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            if (variant == 0)
                rt.PlaySound3DRelative("effect\\mon_\u00c4\u00a7\u00c5\u00f5\u00b0\u00e6.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            Prim p = LaunchLast(rt, 0x2e);
            p.m_duration = f_17c;
            uint a = m_lastPrim.A;
            M.U8(a + 0x3e8, 1);
            M.F32(a + 0x3f0, 0.0f);
            M.U32(a + 0x484, M.U32(A + 0x180));
            M.F32(a + 0x488, f_184 - 0.0f);
            M.U32(a + 0x48c, M.U32(A + 0x188));
            if (variant == 0)
            {
                p = m_lastPrim; p.f_324 = 200.0f;
                rt.SetSprName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00c4\u00a7\u00c5\u00f5\u00b0\u00e6_.spr");
                rt.SetActName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00c4\u00a7\u00c5\u00f5\u00b0\u00e6_.act");
                p = m_lastPrim; p.f_1e8 = 2;
                p = m_lastPrim; p.f_318 = 2.5f;
            }
            else
            {
                string spr, act;
                switch (variant)
                {
                    case 1: spr = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\wink.spr"; act = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\wink.act"; break;
                    case 2: spr = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\fvoice.spr"; act = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\fvoice.act"; break;
                    case 0xc: spr = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00c7\u00e5\u00b5\u00e5\u00b7\u00b9\u00b5\u00e5\u00bd\u00ba\u00c7\u00c7\u00be\u00ee.spr"; act = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00c7\u00e5\u00b5\u00e5\u00b7\u00b9\u00b5\u00e5\u00bd\u00ba\u00c7\u00c7\u00be\u00ee.act"; break;
                    case 0xd: spr = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00c0\u00a9\u00b5\u00e5\u00c4\u00bf\u00c5\u00cd.spr"; act = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00c0\u00a9\u00b5\u00e5\u00c4\u00bf\u00c5\u00cd.act"; break;
                    default: spr = null; act = null; break;
                }
                if (spr != null)
                {
                    rt.SetSprName(m_lastPrim.A, spr);
                    rt.SetActName(m_lastPrim.A, act);
                    p = m_lastPrim; p.f_1e8 = 6;
                    p = m_lastPrim; p.f_318 = 1.0f;
                    M.U8(m_lastPrim.A + 0x254, 0);
                    p = m_lastPrim; p.f_1dc |= 0x200;
                }
            }
            // FUN_00ad5710 writes the low word of the (dword) variant argument; the add reads the whole dword.
            ushort dirAngle = (ushort)FxMath.DirectionToAngle(m_pos_x - M.F32(A + 0x180), m_pos_z - M.F32(A + 0x188));
            int arg = (int)(((uint)(int)variant & 0xffff0000u) | dirAngle);
            uint view = M.U32(rt.Mode + 0xd0);
            int sum = FxOps.Cvtt(M.F32(view + 0x30)) + arg;
            int rem = (short)sum % 0x168;
            ushort ang = (ushort)rem;
            if ((short)ang < 0) ang = (ushort)(ang + 0x168);
            p = m_lastPrim; p.f_1c4 |= 0x200;
            int action;
            if ((ushort)(ang - 0xb4) <= 0x59) action = 0;
            else if ((ushort)(ang - 0x5a) <= 0x59) action = 1;
            else if (ang <= 0x59) action = 2;
            else action = 3;
            rt.SetAction(m_lastPrim.A, action, 0x10, 1);
            m_lastPrim.SnapshotTrail_ad58d0();
            a = m_lastPrim.A;
            M.U8(a + 0x49c, 0);
            M.U8(a + 0x550, 0);
            M.U8(a + 0x604, 0);
        }

        /// <summary>Case 267 (0x10b) — Occult Impaction.</summary>
        [FxEffectProcess(0x10b)]
        public FxCaseResult Case_OccultImpaction(FxRuntime rt)
        {
            Fn_b6d840(rt, 0);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 606 (0x25e, vf15) — Pang Voice.</summary>
        [FxEffectProcess(0x25e)]
        public FxCaseResult Case_PangVoice(FxRuntime rt)
        {
            Fn_b6d840(rt, 2);
            return FxCaseResult.Continue;
        }

        /// <summary>Case 607 (0x25f, vf15) — Wink of Charm.</summary>
        [FxEffectProcess(0x25f)]
        public FxCaseResult Case_WinkOfCharm(FxRuntime rt)
        {
            Fn_b6d840(rt, 1);
            return FxCaseResult.Continue;
        }
    }
}
