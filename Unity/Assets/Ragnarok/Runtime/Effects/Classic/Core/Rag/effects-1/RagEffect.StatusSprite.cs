// Synced from reference/port/src/RoClassicFx/Rag/effects-1/RagEffect.StatusSprite.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// effects-1: CRagEffect::fn_b72000 (0xb72000), the "one sprite prim" effect table (item/status icons,
    /// Ninja/Gunslinger skill sprites, magic circles …) selected by a byte, and its users.
    /// </summary>
    public readonly partial struct RagEffect
    {
        static string S72(uint address) => Effects1Strings.B72000[address];

        // ---- users -------------------------------------------------------------------------------------

        /// <summary>vf15 case 0x248 — Full Buster: tick 5 snap + sound; fn_b72000(8).</summary>
        [FxEffectProcess(584)]
        public FxCaseResult Case_FullBuster(FxRuntime rt)
        {
            if (m_stateCnt == 5) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff8908, 0f);
            Fn_b72000(rt, 8);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(589)]
        public FxCaseResult Case_Eswoo_effects1(FxRuntime rt) { Fn_b72000(rt, 0xd); return FxCaseResult.Continue; }

        [FxEffectProcess(630)]
        public FxCaseResult Case_ShadowSlash(FxRuntime rt)
        {
            if (m_stateCnt == 0) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff8464, 0f);
            Fn_b72000(rt, 0xf);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(631)]
        public FxCaseResult Case_ImprovisedDefense(FxRuntime rt) { Fn_b72000(rt, 0x10); return FxCaseResult.Continue; }

        [FxEffectProcess(632)]
        public FxCaseResult Case_VanishingSlash(FxRuntime rt)
        {
            if (m_stateCnt == 0) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff8450, 0f);
            Fn_b72000(rt, 0x11);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(633)]
        public FxCaseResult Case_FinalStrike(FxRuntime rt)
        {
            if (m_stateCnt == 0) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff8440, 0f);
            Fn_b72000(rt, 0x12);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(634)]
        public FxCaseResult Case_CrimsonFireFormation(FxRuntime rt) { Fn_b72000(rt, 0x13); return FxCaseResult.Continue; }

        [FxEffectProcess(637)]
        public FxCaseResult Case_Desperado(FxRuntime rt)
        {
            if (m_stateCnt == 10) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff7eb4, 0f);
            Fn_b72000(rt, 0x14);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(643)]
        public FxCaseResult Case_RapidShower(FxRuntime rt)
        {
            if (m_stateCnt == 0x14) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff847c, 0f);
            Fn_b72000(rt, 0x1a);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(644)]
        public FxCaseResult Case_MagicalBullet(FxRuntime rt)
        {
            if (m_stateCnt == 0) FollowMasterAndPlay(rt, Effects1Strings.Wav_ff8494, 0f);
            Fn_b72000(rt, 0x1b);
            return FxCaseResult.Continue;
        }

        [FxEffectProcess(645)]
        public FxCaseResult Case_SpreadAttack(FxRuntime rt) { Fn_b72000(rt, 0x1c); return FxCaseResult.Continue; }

        [FxEffectProcess(647)]
        public FxCaseResult Case_Tracking_effects1(FxRuntime rt) { Fn_b72000(rt, 0x1d); return FxCaseResult.Continue; }

        /// <summary>vf15 case 0x288 — Triple Action: tick 25 snap + sound at (0,0,0) (not player-relative); fn_b72000(0x1e).</summary>
        [FxEffectProcess(648)]
        public FxCaseResult Case_TripleAction(FxRuntime rt)
        {
            if (m_stateCnt == 0x19)
            {
                FollowMaster();
                rt.Host?.PlaySound3D(Effects1Strings.Wav_ff84ac, 0f, 0f, 0f, 0xfa, 0x28, 1.0f, 0);
            }
            Fn_b72000(rt, 0x1e);
            return FxCaseResult.Continue;
        }

        // ---- fn_b72000 -----------------------------------------------------------------------------

        /// <summary>
        /// CRagEffect::fn_b72000 (0xb72000) — follows the owner; at tick <c>kind == 0x1e ? 30 : 0</c> (a tick
        /// counter of 99999998 wraps to 0 first) launches one sprite prim (type 5, lives as long as the effect)
        /// whose sprite/act, render mode (+0x1e8), flags and start action depend on the low byte of
        /// <paramref name="kind"/>; unknown kinds get a bare prim.
        /// </summary>
        public void Fn_b72000(FxRuntime rt, int kind)
        {
            sbyte k = (sbyte)kind;
            FollowMaster();
            int trigger = k == 0x1e ? 0x1e : 0;
            int t = m_stateCnt;
            if (t == 0x5f5e0fe)
            {
                m_stateCnt = 0;
                t = 0;
            }
            if (t != trigger) return;

            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = f_17c;
            p.f_398 = 0f;
            p.f_1c4 |= 0x200;
            p.f_1dc |= 0x200;
            p.f_1e8 = 2;
            M.U8(p.A + 0x254, 0);
            p.f_328 = 0f;
            uint a = p.A;

            switch ((int)k)
            {
                case 0x00: Spr(rt, a, 0xffc2cc); Act(rt, a, 0xffc2e0); break;
                case 0x01:
                    rt.PlaySound3DRelative(S72(0xffc2f4), m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                    Spr(rt, a, 0xffc30c); Act(rt, a, 0xffc324); break;
                case 0x02: Spr(rt, a, 0xffc33c); Act(rt, a, 0xffc354); break;
                case 0x03: Spr(rt, a, 0xffc36c); Act(rt, a, 0xffc384); break;
                case 0x04: Spr(rt, a, 0xffc39c); Act(rt, a, 0xffc3b4); break;
                case 0x05: Spr(rt, a, 0xffc3cc); Act(rt, a, 0xffc3e0); break;
                case 0x06: Spr(rt, a, 0xffc3f4); Act(rt, a, 0xffc40c); break;
                case 0x07:
                    rt.PlaySound3DRelative(S72(0xffc424), m_pos_x, 0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                    p.f_1e8 = 3;
                    p.f_1dc = 0;
                    p.f_324 = 220.0f;
                    Spr(rt, a, 0xffc43c); Act(rt, a, 0xffc450); break;
                case 0x08:
                {
                    p.f_1e8 = 4;
                    Spr(rt, a, 0xffc464);
                    rt.SetActName(a, S72(0xffc478));
                    ushort d = ScreenHeading(rt, kind);
                    int action = (ushort)(d - 0xb4) <= 0x59 ? 2 : (ushort)(d - 0x5a) <= 0x59 ? 3 : d <= 0x59 ? 0 : 1;
                    rt.SetAction(a, action, 0x10, 1);
                    p.SnapshotTrail_ad58d0();
                    return;
                }
                case 0x09: p.f_1e8 = 4; Spr(rt, a, 0xff55b4); Act(rt, a, 0xff55c8); break;
                case 0x0a:
                    p.f_1e8 = 4;
                    Spr(rt, a, 0xffc48c);
                    rt.SetActName(a, S72(0xffc4a0));
                    M.U8(a + 0x254, 1);
                    break;
                case 0x0b: p.f_1e8 = 4; Spr(rt, a, 0xffc4b4); Act(rt, a, 0xffc4c8); break;
                case 0x0c: p.f_1e8 = 4; Spr(rt, a, 0xffc4dc); Act(rt, a, 0xffc4f0); break;
                case 0x0d: p.f_1e8 = 4; Spr(rt, a, 0xffc504); Act(rt, a, 0xffc518); break;
                case 0x0e: Spr(rt, a, 0xffc52c); Act(rt, a, 0xffc540); break;
                case 0x0f:
                {
                    p.f_1e8 = 4;
                    Spr(rt, a, 0xffc554);
                    rt.SetActName(a, S72(0xffc56c));
                    int bit = rt.Rand.Next() & 1;
                    rt.SetAction(a, bit == 0 ? 4 : 0, 0x10, 1);
                    p.SnapshotTrail_ad58d0();
                    return;
                }
                case 0x10:
                    p.f_398 = -6.0f; p.f_1c4 = 1; p.f_1e8 = 6;
                    Spr(rt, a, 0xffc584); ActStart(rt, a, 0xffc598, 0x10); return;
                case 0x11:
                {
                    p.f_1e8 = 4;
                    Spr(rt, a, 0xffc5ac);
                    rt.SetActName(a, S72(0xffc5c0));
                    int bit = rt.Rand.Next() & 1;
                    rt.SetAction(a, bit == 0 ? 2 : 0, 0x10, 1);
                    p.SnapshotTrail_ad58d0();
                    return;
                }
                case 0x12:
                    p.f_398 = -6.0f; p.f_1e8 = 2;
                    Spr(rt, a, 0xffc5d4); ActStart(rt, a, 0xffc5e4, 0x10); return;
                case 0x13:
                    p.f_398 = -1.0f; p.f_1c4 = 1;
                    p.f_1e8 = RandMod(rt, 5) + 3;
                    M.U8(a + 0x254, 1);
                    Spr(rt, a, 0xffc5f4); ActStart(rt, a, 0xffc608, 0x10); return;
                case 0x14: p.f_1e8 = 4; Spr(rt, a, 0xffc61c); ActStart(rt, a, 0xffc634, 0x10); return;
                case 0x15: Ground(p); Spr(rt, a, 0xffc64c); ActStart(rt, a, 0xffc668, 0x10); return;
                case 0x16: Ground(p); Spr(rt, a, 0xffc684); ActStart(rt, a, 0xffc6a0, 0x10); return;
                case 0x17: Ground(p); Spr(rt, a, 0xffc6bc); ActStart(rt, a, 0xffc6d4, 0x10); return;
                case 0x18: Ground(p); Spr(rt, a, 0xffc6ec); ActStart(rt, a, 0xffc704, 0x10); return;
                case 0x19: Ground(p); Spr(rt, a, 0xffc71c); ActStart(rt, a, 0xffc734, 0x10); return;
                case 0x1a: p.f_1e8 = 4; Spr(rt, a, 0xffc74c); ActStart(rt, a, 0xffc764, 0x10); return;
                case 0x1b: p.f_1e8 = 2; Spr(rt, a, 0xffc77c); ActStart(rt, a, 0xffc794, 0x10); return;
                case 0x1c:
                {
                    p.f_1e8 = 4;
                    Spr(rt, a, 0xffc7ac);
                    rt.SetActName(a, S72(0xffc7c0));
                    ushort d = ScreenHeading(rt, kind);
                    int action = (ushort)(d - 0xb4) <= 0x59 ? 4 : (ushort)(d - 0x5a) <= 0x59 ? 6 : d <= 0x59 ? 0 : 2;
                    rt.SetAction(a, action, 0x10, 1);
                    p.SnapshotTrail_ad58d0();
                    return;
                }
                case 0x1d: p.f_1e8 = 2; Spr(rt, a, 0xffc7d4); ActStart(rt, a, 0xffc7e8, 0x10); return;
                case 0x1e: p.f_1e8 = 2; Spr(rt, a, 0xffc7fc); ActStart(rt, a, 0xffc814, 0x10); return;
                case 0x1f: p.f_1e8 = 4; Spr(rt, a, 0xffc82c); ActStart(rt, a, 0xffc844, 0x10); return;
                case 0x20: p.f_1e8 = 4; Spr(rt, a, 0xffc85c); ActStartSound(rt, a, 0xffc874, 0xffc88c); return;
                case 0x21: p.f_1e8 = 4; Spr(rt, a, 0xffc8a4); ActStartSound(rt, a, 0xffc8c0, 0xffc88c); return;
                case 0x22: p.f_1e8 = 4; Spr(rt, a, 0xffc8dc); ActStartSound(rt, a, 0xffc8f8, 0xffc88c); return;
                case 0x23: p.f_1e8 = 4; Spr(rt, a, 0xffc914); ActStartSound(rt, a, 0xffc92c, 0xffc88c); return;
                case 0x24: p.f_1e8 = 4; Spr(rt, a, 0xffc944); ActStartSound(rt, a, 0xffc960, 0xffc88c); return;
                case 0x25: p.f_1e8 = 4; Spr(rt, a, 0xffc97c); ActStart(rt, a, 0xffc994, 0x10); return;
                case 0x26: p.f_1e8 = 4; Spr(rt, a, 0xffc9ac); ActStartSound(rt, a, 0xffc9c0, 0xff81b8); return;
                case 0x27: p.f_1e8 = 4; Spr(rt, a, 0xffc9d4); ActStartSound(rt, a, 0xffc9e8, 0xff3c04); return;
                case 0x32: Ground(p); Spr(rt, a, 0xffc9fc); ActStart(rt, a, 0xffca10, 0x10); return;
                case 0x33: Ground(p); Spr(rt, a, 0xffca24); ActStart(rt, a, 0xffca3c, 0x10); return;
                case 0x34: Ground(p); Spr(rt, a, 0xffca54); ActStart(rt, a, 0xffca6c, 0x10); return;
                case 0x35: Ground(p); Spr(rt, a, 0xffca84); ActStart(rt, a, 0xffca9c, 0x10); return;
                case 0x36: Ground(p); Spr(rt, a, 0xffcab4); ActStart(rt, a, 0xffcacc, 0x10); return;
                case 0x3c:
                {
                    p.f_1e8 = 4;
                    Spr(rt, a, 0xffcae4);
                    rt.SetActName(a, S72(0xffcaf8));
                    p.f_398 = -8.0f;
                    int bit = rt.Rand.Next() & 1;
                    rt.SetAction(a, bit == 0 ? 0 : 7, 0x10, 1);
                    p.SnapshotTrail_ad58d0();
                    return;
                }
                case 0x3d:
                case 0x3e:
                {
                    p.f_398 = -1.0f; p.f_1c4 = 5;
                    M.U8(a + 0x254, 1);
                    p.f_1e8 = RandMod(rt, 3) + 8;
                    p.f_1dc = 0;
                    p.f_324 = (float)(rt.Rand.Next() % 0x47 + 0x46);
                    p.f_318 = (float)(rt.Rand.Next() % 0xb) * 0.04f + 0.4f;
                    M.U32(a + 0x23c, 0x55);
                    p.f_240 = 0xff;
                    p.f_244 = 0x55;
                    if (k == 0x3d) { Spr(rt, a, 0xffcb0c); rt.SetActName(a, S72(0xffcb24)); }
                    else { Spr(rt, a, 0xffcb3c); rt.SetActName(a, S72(0xffcb50)); }
                    int action = rt.Rand.Next() & 7;
                    rt.SetAction(a, k == 0x3d ? action : action + 0x20, 0x10, 1);
                    p.SnapshotTrail_ad58d0();
                    return;
                }
                case 0x3f:
                {
                    p.f_398 = -12.0f; p.f_1c4 = 5;
                    M.U8(a + 0x254, 1);
                    p.f_1e8 = 6;
                    p.f_1dc = 0;
                    p.f_324 = 255.0f;
                    p.f_318 = 0.7f;
                    M.U32(a + 0x23c, 0x9b);
                    p.f_240 = 0xff;
                    p.f_244 = 0x9b;
                    Spr(rt, a, 0xffcb64);
                    rt.SetActName(a, S72(0xffcb7c));
                    rt.SetAction(a, rt.Rand.Next() & 7, 0x10, 1);
                    p.SnapshotTrail_ad58d0();
                    return;
                }
                case 0x40:
                case 0x41:
                    p.f_398 = -1.0f; p.f_1c4 = 5;
                    M.U8(a + 0x254, 1);
                    p.f_1e8 = k == 0x40 ? 3 : 2;
                    p.f_1dc = 0;
                    p.f_324 = 205.0f;
                    p.f_318 = 2.5f;
                    M.U32(a + 0x23c, 0x9b);
                    p.f_240 = 0x9b;
                    p.f_244 = 0x9b;
                    Spr(rt, a, 0xffcb94);
                    if (k == 0x40) { ActStart(rt, a, 0xffcba8, 0x10); return; }
                    rt.SetActName(a, S72(0xffcba8));
                    rt.SetAction(a, 4, 0x10, 1);
                    p.SnapshotTrail_ad58d0();
                    return;
                case 0x46: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcbbc); Act(rt, a, 0xffcbd0); break;
                case 0x47: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcbe4); Act(rt, a, 0xffcbf8); break;
                case 0x48: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcc0c); Act(rt, a, 0xffcc24); break;
                case 0x49: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcc3c); Act(rt, a, 0xffcc50); break;
                case 0x4a: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcc64); Act(rt, a, 0xffcc7c); break;
                case 0x4b: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcc94); Act(rt, a, 0xffcca8); break;
                case 0x4c: M.U8(a + 0x254, 1); Spr(rt, a, 0xffccbc); Act(rt, a, 0xffccd4); break;
                case 0x4d: M.U8(a + 0x254, 1); Spr(rt, a, 0xffccec); Act(rt, a, 0xffccfc); break;
                case 0x4e: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcd0c); Act(rt, a, 0xffcd20); break;
                case 0x4f: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcd34); Act(rt, a, 0xffcd4c); break;
                case 0x50: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcd64); Act(rt, a, 0xffcd7c); break;
                case 0x51: M.U8(a + 0x254, 1); Spr(rt, a, 0xffcd94); Act(rt, a, 0xffcda4); break;
                case 0x52: p.f_1e8 = 4; M.U8(a + 0x254, 1); Spr(rt, a, 0xffcdb4); Act(rt, a, 0xffcdcc); break;
                case 0x53: p.f_1e8 = 4; M.U8(a + 0x254, 0); Spr(rt, a, 0xffcde4); Act(rt, a, 0xffcdfc); break;
                case 0x54:
                    p.f_398 = -1.0f; p.f_1c4 = 1; p.f_1e8 = 10;
                    M.U8(a + 0x254, 1);
                    Spr(rt, a, 0xffce14); Act(rt, a, 0xffce28); break;
                case 0x55:
                    p.f_398 = -1.0f; p.f_1c4 = 1; p.f_1e8 = 10;
                    M.U8(a + 0x254, 0);
                    Spr(rt, a, 0xffce3c); ActStart(rt, a, 0xffce50, 10); return;
                case 0x56: p.f_1e8 = 4; Spr(rt, a, 0xffce64); Act(rt, a, 0xffce88); break;
                case 0x57: p.f_1e8 = 4; Spr(rt, a, 0xffceac); Act(rt, a, 0xffcec0); break;
                case 0x58: p.f_1e8 = 4; M.U8(a + 0x254, 1); Spr(rt, a, 0xffced4); Act(rt, a, 0xffcee8); break;
                case 0x5a: Ground(p); Spr(rt, a, 0xffcefc); ActStart(rt, a, 0xffcf18, 0x10); return;
                case 0x5b: Circle4(p, false); Spr(rt, a, 0xffcf34); Act(rt, a, 0xffcf4c); break;
                case 0x5e: Circle4(p, false); Spr(rt, a, 0xffcf64); Act(rt, a, 0xffcf8c); break;
                case 0x5f: Circle4(p, false); Spr(rt, a, 0xffcfb4); Act(rt, a, 0xffcfe0); break;
                case 0x60: Circle4(p, true); Spr(rt, a, 0xffd00c); Act(rt, a, 0xffd040); break;
                case 0x61: Circle4(p, true); Spr(rt, a, 0xffd074); Act(rt, a, 0xffd0a8); break;
                case 0x62: Circle4(p, true); Spr(rt, a, 0xffd0dc); Act(rt, a, 0xffd110); break;
                case 0x63: Circle4(p, true); Spr(rt, a, 0xffd144); Act(rt, a, 0xffd178); break;
                case 0x64: Circle4(p, true); Spr(rt, a, 0xffd1ac); Act(rt, a, 0xffd1e0); break;
                case 0x65: Circle4(p, true); Spr(rt, a, 0xffd214); Act(rt, a, 0xffd248); break;
                case 0x66: Circle4(p, true); Spr(rt, a, 0xffd27c); Act(rt, a, 0xffd2b0); break;
                case 0x67: Circle4(p, true); Spr(rt, a, 0xffd2e4); Act(rt, a, 0xffd31c); break;
                case 0x68: Circle4(p, true); Spr(rt, a, 0xffd354); Act(rt, a, 0xffd388); break;
                case 0x69: Circle4(p, true); Spr(rt, a, 0xffd3bc); Act(rt, a, 0xffd3f0); break;
                default: break;   // LAB_00b73af8: bare prim
            }
            p.SnapshotTrail_ad58d0();
        }

        void Spr(FxRuntime rt, uint prim, uint name) => rt.SetSprName(prim, S72(name));

        /// <summary>code_r0x00b73aed: SetActName (the shared tail then snapshots the trail).</summary>
        void Act(FxRuntime rt, uint prim, uint name) => rt.SetActName(prim, S72(name));

        /// <summary>code_r0x00b725d8/b725e7: SetActName, SetAction(0, <paramref name="second"/>, 1), SnapshotTrail_effects1.</summary>
        void ActStart(FxRuntime rt, uint prim, uint name, int second)
        {
            rt.SetActName(prim, S72(name));
            rt.SetAction(prim, 0, second, 1);
            new Prim(M, prim).SnapshotTrail_ad58d0();
        }

        /// <summary>code_r0x00b72aa7: SetActName, SetAction(0, 0x10, 1), PlaySound3D(wav) at (0,0,0), SnapshotTrail_effects1.</summary>
        void ActStartSound(FxRuntime rt, uint prim, uint name, uint wav)
        {
            rt.SetActName(prim, S72(name));
            rt.SetAction(prim, 0, 0x10, 1);
            rt.Host?.PlaySound3D(S72(wav), 0f, 0f, 0f, 0xfa, 0x28, 1.0f, 0);
            new Prim(M, prim).SnapshotTrail_ad58d0();
        }

        /// <summary>The "ground decal" variant: y -1, f_1c4 = 1, +0x254 = 1.</summary>
        static void Ground(Prim p)
        {
            p.f_398 = -1.0f;
            p.f_1c4 = 1;
            p.M.U8(p.A + 0x254, 1);
        }

        /// <summary>Kinds 0x5b.. : +0x254 = 1, mode 4, and y -4 (Black Thunder …) or f_1c4 = 0xd (magic circles).</summary>
        static void Circle4(Prim p, bool magicCircle)
        {
            p.M.U8(p.A + 0x254, 1);
            if (magicCircle) p.f_1c4 = 0xd;
            p.f_1e8 = 4;
            if (!magicCircle) p.f_398 = -4.0f;
        }

        /// <summary>
        /// Kinds 8 / 0x1c: heading from the target to the caster (FUN_00ad5710) turned into screen space by the
        /// camera longitude (view +0x30), in [0, 360). The heading is written over the low word of the
        /// argument's stack slot, which is what gets added.
        /// </summary>
        ushort ScreenHeading(FxRuntime rt, int kind)
        {
            ushort ang = Prim.Fn_ad5710(m_pos_x - f_180, m_pos_z - f_188);
            int slot = (int)(((uint)kind & 0xffff0000u) | ang);
            uint view = M.U32(rt.Mode + 0xd0);
            int sum = FxOps.Cvtt(M.F32(view + 0x30)) + slot;
            int rem = (short)sum % 0x168;
            ushort d = (ushort)rem;
            if ((short)d < 0) d = (ushort)(d + 0x168);
            return d;
        }
    }
}
