// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.HunterBlacksmith.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// The client's inlined screen anchor: ProjectVertex (0x53b370) of the owner position with the view
        /// matrix (GetWorld()+0xd0 → +0x98); screen x/y are stored in f_154 / f_158.
        /// </summary>
        void ProjectMasterToF154(FxRuntime rt)
        {
            uint view = M.U32(rt.Mode + 0xd0);
            rt.ProjectTl(view + 0x98, M.U32(A + 0x138) + 0x10, out float sx, out float sy, out _);
            f_154 = sx;
            f_158 = sy;
        }

        /// <summary>
        /// The 20 "sparks" (prim type 3, alpha_center.tga) shared by Flasher and Adrenaline Rush; they differ only
        /// in two random ranges and the fade point.
        /// </summary>
        void LaunchSparks(FxRuntime rt, bool flasher)
        {
            for (int i = 0x14; i != 0; i--)
            {
                Prim p = LaunchLast(rt, 3);
                p.m_duration = flasher ? 0x46 : 0x50;
                p = m_lastPrim; p.f_398 = -40.0f;
                int ang = RandMod(rt, 0x168);
                p = m_lastPrim; p.f_30c = (float)ang;
                int sp = RandMod(rt, 0x3c);
                p = m_lastPrim; p.f_330 = (float)(sp + 10) / 10.0f;
                p = m_lastPrim; p.f_33c = -(p.f_330 / (float)p.m_duration) / 1.5f;
                int w = RandMod(rt, 0x19);
                p = m_lastPrim; p.f_288 = (float)(w + 5) / 10.0f;
                if (flasher)
                {
                    int h = RandMod(rt, 0x28);
                    p = m_lastPrim; p.f_360 = (float)(h + 0x14);
                    int g = RandMod(rt, 0x1e);
                    p = m_lastPrim; p.f_364 = (float)(g + 0x32) / 10.0f;
                    p = m_lastPrim; p.f_324 = 0.0f;
                    p = m_lastPrim; p.f_2d4 = 200.0f;
                    p = m_lastPrim; p.f_328 = p.f_2d4 / 10.0f;
                    p = m_lastPrim; p.f_1f0 = p.m_duration - p.m_duration / 3;
                }
                else
                {
                    p = m_lastPrim; p.f_360 = 0.0f;
                    int g = RandMod(rt, 0x14);
                    p = m_lastPrim; p.f_364 = (float)(g + 0x32) / 13.0f;
                    p = m_lastPrim; p.f_200 = 0x28;
                    int a = RandMod(rt, 10);
                    p = m_lastPrim; p.f_29c = (float)(a + 0xf) / 10.0f;
                    p = m_lastPrim; p.f_2a0 = 0.0f;
                    p = m_lastPrim; p.f_2cc = 0.0f;
                }
                SetPrimTexture(rt, m_lastPrim, "EFFECT\\alpha_center.tga");
            }
        }

        /// <summary>
        /// CRagEffect::fn_b3b3c0 (0xb3b3c0) — Flasher (EF 99): sound at tick 0; screen anchor every tick; at tick 0
        /// a flash (prim 0xf with alpha keyframes pushed into its vectors +0x258/+0x264/+0x270) and 20 sparks.
        /// </summary>
        public void Fn_b3b3c0(FxRuntime rt)
        {
            if (m_stateCnt == 0)
                rt.PlaySound3DRelative("effect\\hunter_flasher.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            ProjectMasterToF154(rt);
            if (m_stateCnt != 0) return;

            Prim p = LaunchLast(rt, 0xf);
            p.m_duration = 0x46;
            p = m_lastPrim; p.f_1dc = 0x41;
            p = m_lastPrim; p.f_1c4 |= 2;
            p = m_lastPrim; p.f_398 = -10.0f;
            p = m_lastPrim; p.f_354 = 20.0f;
            p = m_lastPrim; p.f_360 = 20.0f;
            p = m_lastPrim; p.f_324 = 0.0f;
            p = m_lastPrim; p.f_2d4 = 200.0f;
            p = m_lastPrim; p.f_328 = p.f_2d4 / 6.0f;
            p = m_lastPrim; p.f_154 = 1;
            rt.FloatVectorPushBack(m_lastPrim.A + 0x258, 40.0f);
            rt.FloatVectorPushBack(m_lastPrim.A + 0x264, 120.0f);
            rt.FloatVectorPushBack(m_lastPrim.A + 0x270, 200.0f);
            rt.FloatVectorPushBack(m_lastPrim.A + 0x258, 100.0f);
            rt.FloatVectorPushBack(m_lastPrim.A + 0x264, 0.0f);
            rt.FloatVectorPushBack(m_lastPrim.A + 0x270, 80.0f);
            p = m_lastPrim; p.f_1f0 = p.m_duration - 10;
            SetPrimTexture(rt, m_lastPrim, "effect\\thunder_center.bmp");
            LaunchSparks(rt, true);
        }

        /// <summary>Case 99 (0x63) — Flasher.</summary>
        [FxEffectProcess(0x63)]
        public FxCaseResult Case_Flasher(FxRuntime rt)
        {
            Fn_b3b3c0(rt);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b3d4e0 (0xb3d4e0) — Adrenaline Rush (EF 98): duration 300 and sound at tick 0, second
        /// sound at tick 30; screen anchor every tick; at tick 0 20 sparks and 4 rotating particle1 sprites (type 7).
        /// </summary>
        public void Fn_b3d4e0(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 0)
            {
                f_17c = 300;
                rt.PlaySound3DRelative("effect\\black_adrenalinerush_a.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t == 0x1e)
                rt.PlaySound3DRelative("effect\\black_adrenalinerush_b.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            ProjectMasterToF154(rt);
            if (m_stateCnt != 0) return;
            LaunchSparks(rt, false);
            if (m_stateCnt != 0) return;
            for (int ang = 0; ang < 0x168; ang += 0x5a)
            {
                Prim p = LaunchLast(rt, 7);
                p.m_duration = 100;
                p = m_lastPrim; p.f_348 = 7.0f;
                p = m_lastPrim; p.f_2ec = -0.1f;
                p = m_lastPrim; p.f_2f0 = -0.002f;
                p = m_lastPrim; p.f_30c = (float)ang;
                p = m_lastPrim; p.f_33c = 0.2f;
                p = m_lastPrim; p.f_330 = (float)m_stateCnt * p.f_33c + 0.3f;
                p = m_lastPrim; p.f_318 = 1.2f;
                p = m_lastPrim;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                p = m_lastPrim; p.f_1f0 = p.m_duration - p.m_duration / 10;
                rt.SetSprName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle1.spr");
                rt.SetActName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle1.act");
                m_lastPrim.SnapshotTrail_ad58d0();
            }
        }

        /// <summary>Case 98 (0x62) — Adrenaline Rush.</summary>
        [FxEffectProcess(0x62)]
        public FxCaseResult Case_AdrenalineRush(FxRuntime rt)
        {
            Fn_b3d4e0(rt);
            return FxCaseResult.Continue;
        }
    }
}
