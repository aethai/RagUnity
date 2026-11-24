// Synced from reference/port/src/RoClassicFx/Rag/effects-5/RagEffect.Effects5.Earth.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public sealed partial class FxRuntime
    {
        /// <summary>DAT_01519180 — Blitz Beat falcon counter (fn_b32ad0), a client global shared by all effects.</summary>
        public int BlitzBeatCounter;
    }

    public readonly partial struct RagEffect
    {
        /// <summary>The "copy rotation matrix +0x160 → +0x190" (3 × movups) of the stone prims.</summary>
        void CopyPrimMatrix(Prim p) => M.Copy(p.A + 0x190, p.A + 0x160, 0x30);

        /// <summary>
        /// CRagEffect::fn_b36850 (0xb36850): Earth Spike (mode 0) / Spear of Ice (mode 2) — at tick 0 the sound, a
        /// screen shake + ambient tint (SendMsg 0x7a / 0x87), a big central shard and 6 surrounding shards (prim 9).
        /// </summary>
        public void Fn_b36850(FxRuntime rt, sbyte mode)
        {
            if (m_stateCnt != 0) return;
            string texture = null;
            if (mode == 0)
            {
                rt.PlaySound3DRelative("effect\\wizard_earthspike.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                texture = "effect\\stone.bmp";
            }
            else if (mode == 2)
            {
                int k = RandMod(rt, 3) + 1;
                rt.PlaySound3DRelative("effect\\EF_IceArrow" + k.ToString() + ".wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                texture = "effect\\ice.tga";
            }
            rt.GameModeSendMsg(0x7a, 1, 0, 0, 0);
            rt.GameModeSendMsg(0x87, 0x3f800000, 0x3e75c28f, 0x3e4ccccd, 0);

            Prim p = LaunchEffectPrim(rt, 9, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0xf0;
            p.f_1dc = 1;
            p.f_1dc |= 2;
            int a = RandMod(rt, 0x168);
            rt.Rand.Next();
            FxMath.GetSin(a);
            FxMath.GetCos(a);
            p.f_394 = 0f;
            p.f_398 = 10.0f;
            p.f_39c = 0f;
            p.f_318 = (float)(RandMod(rt, 0x32) + 300) / 100.0f;
            p.f_360 = 18.0f;
            p.f_30c = (float)RandMod(rt, 0x168);
            p.f_310 = (float)(RandMod(rt, 0x14) + 0x50);
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
            FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            CopyPrimMatrix(p);
            p.f_2bc = 1.0f;
            p.f_2c0 = 0.01f;
            p.f_324 = 250.0f;
            p.f_1d4 = 0xf;
            p.f_200 = 0xc;
            p.f_29c = -1.2f;
            p.f_2a0 = 0f;
            p.f_1f0 = p.m_duration - 10;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(texture));

            for (int k = 0x3c; k < 0x1a4; k += 0x3c)
            {
                int spread = k == 0 ? 0 : RandMod(rt, k);
                p = LaunchEffectPrim(rt, 9, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0xf0;
                p.f_1dc |= 1;
                a = RandMod(rt, 0x168);
                rt.Rand.Next();
                float s = FxMath.GetSin(a);
                float c = FxMath.GetCos(a);
                p.f_398 = 20.0f;
                p.f_394 = s * 3.5f;
                p.f_39c = c * -3.5f;
                p.f_318 = (float)(RandMod(rt, 100) + 400) / 100.0f;
                p.f_360 = 20.0f;
                float ang = (float)(spread + (k - 0x3c));
                p.f_30c = 360.0f < ang ? 360.0f : ang;                 // MINSS 360, v
                p.f_310 = 100.0f;
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                CopyPrimMatrix(p);
                p.f_2bc = 1.5f;
                p.f_2c0 = 0f;
                p.f_324 = 250.0f;
                p.f_1d4 = 2;
                p.f_1f0 = p.m_duration - 10;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                M.U32((uint)p.f_144, rt.GetTexture(texture));
            }
        }

        /// <summary>EF 0x4f (Earth Spike): fn_b36850(0).</summary>
        [FxEffectProcess(0x4f)]
        public FxCaseResult Case_EarthSpike(FxRuntime rt) { Fn_b36850(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 0x26b (Spear of Ice, vf15): fn_b36850(2).</summary>
        [FxEffectProcess(0x26b)]
        public FxCaseResult Case_SpearOfIce(FxRuntime rt) { Fn_b36850(rt, 2); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b3e070 (0xb3e070): Heaven's Drive — follows the owner; at tick 0 sound, screen shake, duration
        /// +50 and a 5×5 grid of stone shards (prim 9).
        /// </summary>
        public void Fn_b3e070(FxRuntime rt)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\wizard_earthspike.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
            f_17c = f_17c + 0x32;
            for (int z = -10; z < 0xf; z += 5)
            {
                for (int x = -10; x < 0xf; x += 5)
                {
                    Prim p = LaunchEffectPrim(rt, 9, 0f, 0f, 0f);
                    m_lastPrim = p;
                    p.m_duration = RandMod(rt, 0x1e) + (f_17c - 10);
                    p.f_1dc = 1;
                    p.f_1dc |= 2;
                    int a = RandMod(rt, 0x168);
                    rt.Rand.Next();
                    FxMath.GetSin(a);
                    FxMath.GetCos(a);
                    int r60 = rt.Rand.Next() % 0x3c;
                    p.f_398 = 10.0f;
                    p.f_394 = (float)x;
                    p.f_39c = (float)z;
                    p.f_318 = (float)(RandMod(rt, 0x32) + 300) / 100.0f;
                    p.f_360 = (float)((r60 < 0 ? 0 : r60) + 1) / 10.0f + 9.0f;
                    p.f_30c = (float)RandMod(rt, 0x168);
                    p.f_310 = (float)(RandMod(rt, 0x14) + 0x50);
                    FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                    FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                    CopyPrimMatrix(p);
                    p.f_2a8 = -1.0f;
                    p.f_2b4 = -0.01f;
                    p.f_2bc = 1.0f;
                    p.f_2c0 = 0.01f;
                    p.f_200 = 0xb;
                    p.f_29c = -1.2f;
                    p.f_2a0 = 0f;
                    p.f_1d4 = 0xe;
                    p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                    M.U32((uint)p.f_144, rt.GetTexture("effect\\stone.bmp"));
                }
            }
        }

        /// <summary>EF 0x8e (Heaven's Drive): fn_b3e070.</summary>
        [FxEffectProcess(0x8e)]
        public FxCaseResult Case_HeavensDrive(FxRuntime rt) { Fn_b3e070(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b3d200 (0xb3d200): Grimtooth — at tick 0 three stone spikes (prim 9) around the owner,
        /// effect duration 1000; each iteration also moves f_198/f_19c/f_1a0 back by f_18c/f_190/f_194.
        /// </summary>
        public void Fn_b3d200(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            for (int i = 0; i < 3; i++)
            {
                Prim p = LaunchEffectPrim(rt, 9, 0f, 0f, 0f);
                m_lastPrim = p;
                p.f_1dc |= 1;
                p.f_1c4 |= 0x200;
                f_17c = 1000;
                p.m_duration = 1000;
                FxMath.GetSin(i * 0x78);
                FxMath.GetCos(i * 0x78);
                if (i == 0) { p.f_394 = 0f; p.f_39c = -12.0f; p.f_398 = 40.0f; }
                else if (i == 1) { p.f_394 = 12.0f; p.f_39c = 6.0f; p.f_398 = 40.0f; }
                else if (i == 2) { p.f_394 = -12.0f; p.f_39c = 6.0f; p.f_398 = 40.0f; }
                p.f_318 = 0.9f;
                p.f_360 = 25.0f;
                p.f_30c = (float)((6 - i) * 0x78);
                p.f_310 = 75.0f;
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                CopyPrimMatrix(p);
                p.f_2bc = 3.5f;
                p.f_2c0 = 0.001f;
                p.f_1d4 = 10;
                p.f_1f0 = p.m_duration - 10;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                uint tex = rt.GetTexture("effect\\stone.bmp");
                // f_18c/f_190/f_194 are floats (SUBSS from memory), not the ints the field table declares.
                f_198 = f_198 - M.F32(A + 0x18c);
                M.U32((uint)p.f_144, tex);
                f_19c = f_19c - M.F32(A + 0x190);
                f_1a0 = f_1a0 - M.F32(A + 0x194);
            }
        }

        /// <summary>EF 0x84 (Grimtooth): fn_b3d200.</summary>
        [FxEffectProcess(0x84)]
        public FxCaseResult Case_Grimtooth(FxRuntime rt) { Fn_b3d200(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b3afd0 (0xb3afd0): Fire Pillar (mode 0: magic_red) — at tick 0 three nested ground circles (prim 0xd).
        /// </summary>
        public void Fn_b3afd0(FxRuntime rt, sbyte mode)
        {
            if (m_stateCnt != 0) return;
            int i = 1, w = 6, h = 4;
            for (int n = 3; n != 0; n--)
            {
                Prim p = LaunchEffectPrim(rt, 0xd, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = f_17c;
                p.f_330 = -3.0f;
                p.f_2f8 = (float)(i + 3);
                p.f_2f4 = (float)((double)i + 1.5);
                p.f_360 = (float)(w * h + 1) + 2.5f;
                p.f_338 = -0.5f;
                p.f_324 = (float)(0xf0 - m_stateCnt * 7);
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                uint tex = rt.GetTexture(mode == 0 ? "effect\\magic_red.tga" : "effect\\ring_white.tga");
                i++;
                w -= 2;
                h--;
                M.U32((uint)p.f_144, tex);
            }
        }

        /// <summary>EF 0x8a (Fire Pillar): fn_b3afd0(0).</summary>
        [FxEffectProcess(0x8a)]
        public FxCaseResult Case_FirePillar(FxRuntime rt) { Fn_b3afd0(rt, 0); return FxCaseResult.Continue; }

        /// <summary>The Magnum Break ring (prim 0xc) shared by fn_b44c80 / fn_b34290.</summary>
        Prim LaunchMagnumRing(FxRuntime rt, int duration, float f2d4OrNaN, float f2f4, float div)
        {
            Prim p = LaunchEffectPrim(rt, 0xc, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = duration;
            p.f_1c4 |= 0x200;
            if (!float.IsNaN(f2d4OrNaN)) p.f_2d4 = f2d4OrNaN;
            p.f_310 = 90.0f;
            p.f_34c = 1.75f;
            p.f_350 = -(p.f_34c / (float)p.m_duration) * 0.5f;
            p.f_2f4 = f2f4;
            p.f_324 = 0f;
            p.f_328 = p.f_2d4 / div;
            p.f_1f0 = p.m_duration - 0xf;
            p.SnapshotTrail_ad58d0();
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture("effect\\ring_yellow.tga"));
            return p;
        }

        /// <summary>The Magnum Break explosion (prim 0xe, 대폭발.tga).</summary>
        void LaunchMagnumBlast(FxRuntime rt, int duration, float size, float f2d4OrNaN, float div)
        {
            Prim p = LaunchEffectPrim(rt, 0xe, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = duration;
            p.f_34c = size;
            p.f_350 = -(p.f_34c / (float)p.m_duration) * 0.5f;
            p.f_330 = 3.0f;
            p.f_324 = 0f;
            if (!float.IsNaN(f2d4OrNaN)) p.f_2d4 = f2d4OrNaN;
            p.f_328 = p.f_2d4 / div;
            p.f_1f0 = p.m_duration - 0xf;
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            M.U32((uint)p.f_144, rt.GetTexture(Fx5Names.BigExplosionTga));
        }

        /// <summary>CRagEffect::fn_b44c80 (0xb44c80): Magnum Break — at tick 0 sound, ring, blast and a second short ring.</summary>
        public void Fn_b44c80(FxRuntime rt)
        {
            if (m_stateCnt != 0) return;
            rt.PlaySound3DRelative("effect\\EF_MagnumBreak.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            LaunchMagnumRing(rt, f_17c, float.NaN, 12.0f, 15.0f);
            LaunchMagnumBlast(rt, f_17c, 1.15f, float.NaN, 15.0f);
            LaunchMagnumRing(rt, 0x1e, float.NaN, 12.0f, 15.0f);
        }

        /// <summary>EF 0x11 (Magnum Break): fn_b44c80.</summary>
        [FxEffectProcess(0x11)]
        public FxCaseResult Case_MagnumBreak(FxRuntime rt) { Fn_b44c80(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b34290 (0xb34290): Cart Revolution — at ticks 7 and 20 sound, ring and blast; dies at tick 300,
        /// else the STR player (whose result is ignored).
        /// </summary>
        public bool Fn_b34290(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 7 || t == 0x14)
            {
                rt.PlaySound3DRelative("effect\\EF_MagnumBreak.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                LaunchMagnumRing(rt, 0x14, 180.0f, 5.0f, 15.0f);
                LaunchMagnumBlast(rt, 0x14, 1.35f, 240.0f, 7.0f);
                t = m_stateCnt;
            }
            if (t > 299) return false;
            StrPlayerDefault(rt);
            return true;
        }

        /// <summary>EF 0xaa (Cart Revolution): fn_b34290.</summary>
        [FxEffectProcess(0xaa)]
        public FxCaseResult Case_CartRevolution(FxRuntime rt) => Fn_b34290(rt) ? FxCaseResult.Continue : FxCaseResult.Dead;

        /// <summary>
        /// CRagEffect::fn_b32ad0 (0xb32ad0): Blitz Beat — sounds at 5 / 30; follows the owner; at tick 0 ten feathers
        /// (prim 0x10) and the falcon counter reset; after tick 35, every 3rd tick while counter &lt; f_174 a hit flash (prim 0xd).
        /// </summary>
        public void Fn_b32ad0(FxRuntime rt)
        {
            int t = m_stateCnt;
            if (t == 5)
            {
                rt.PlaySound3DRelative("effect\\hunter_blitzbeat_1st.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                t = m_stateCnt;
            }
            if (t == 0x1e)
                rt.PlaySound3DRelative("effect\\hunter_blitzbeat.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
            FollowMaster();
            t = m_stateCnt;
            if (t == 0)
            {
                rt.BlitzBeatCounter = t;
                for (int n = 10; n != 0; n--)
                {
                    Prim p = LaunchEffectPrim(rt, 0x10, 0f, 0f, 0f);
                    m_lastPrim = p;
                    p.m_duration = 0x14;
                    p.f_1c4 |= 0x200;
                    M.U32(p.A + 0x30c, M.U32(M.U32(A + 0x138) + 0x4c));
                    FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                    int deg = FxOps.Cvtt((float)RandMod(rt, 0x168));
                    float s = FxMath.GetSin(deg);
                    float c = FxMath.GetCos(deg);
                    p.f_370 = p.f_16c * 0.0f + p.f_160 * 0.0f + p.f_178 * 15.0f + p.f_184;
                    p.f_374 = p.f_170 * 0.0f + p.f_164 * 0.0f + p.f_17c * 15.0f + p.f_188;
                    p.f_378 = p.f_174 * 0.0f + p.f_168 * 0.0f + p.f_180 * 15.0f + p.f_18c;
                    p.f_388 = p.f_388 + p.f_370;
                    p.f_38c = p.f_374 + p.f_38c;
                    p.f_390 = p.f_378 + p.f_390;
                    p.f_398 = -7.0f;
                    p.f_394 = s * 3.5f;
                    p.f_39c = c * -3.5f;
                    p.f_2bc = -1.2f;
                    p.f_2c0 = -0.1f;
                    p.f_354 = (float)(RandMod(rt, 0x28) + 0x14) / 10.0f;
                    p.f_360 = 0.2f;
                    p.f_358 = 0.1f;
                    p.f_35c = -0.01f;
                    p.f_324 = 0f;
                    p.f_2d4 = 250.0f;
                    p.f_200 = 10;
                    p.f_29c = -0.3f;
                    p.f_2a0 = 0.001f;
                    p.f_328 = p.f_2d4 * 0.5f;
                    p.f_1f0 = p.m_duration - 2;
                    p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                    M.U32((uint)p.f_144, rt.GetTexture("effect\\ac_center2.tga"));
                    M.U8(p.A + 0x3e8, 0);
                }
                t = m_stateCnt;
            }
            if (t > 0x23 && t % 3 == 0 && rt.BlitzBeatCounter < f_174)
            {
                int heading = FxOps.Cvtt(M.F32(M.U32(A + 0x138) + 0x4c));
                int r10 = rt.Rand.Next() % 10;
                int r20 = rt.Rand.Next() % 0x14;
                rt.Rand.Next();
                Prim p = LaunchEffectPrim(rt, 0xd, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 0xc;
                p.f_30c = (float)heading;
                p.f_310 = -90.0f;
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                p.f_370 = (p.f_160 * 0.0f - p.f_16c * 2.0f) + p.f_178 + p.f_184;
                p.f_374 = (p.f_164 * 0.0f - p.f_170 * 2.0f) + p.f_17c + p.f_188;
                p.f_378 = (p.f_168 * 0.0f - p.f_174 * 2.0f) + p.f_180 + p.f_18c;
                p.f_388 = p.f_370 + p.f_388;
                p.f_38c = p.f_374 + p.f_38c;
                p.f_390 = p.f_378 + p.f_390;
                p.f_39c = 0f;
                p.f_394 = (float)(((r10 < 0 ? 0 : r10) + 1) / 5);
                p.f_398 = (float)(((r20 < 0 ? 0 : r20) + 1) / 2 - 0xc);
                p.f_2bc = 0.4f;
                p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
                p.f_2f8 = 1.5f;
                p.f_2f4 = 0.5f;
                p.f_360 = 3.5f;
                int d = p.m_duration;
                p.f_1f0 = d - d / 2;
                p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                uint tex = rt.GetTexture("effect\\magic_green.tga");
                rt.BlitzBeatCounter = rt.BlitzBeatCounter + 1;
                M.U32((uint)p.f_144, tex);
            }
        }

        /// <summary>EF 0x73 (Blitz Beat): fn_b32ad0.</summary>
        [FxEffectProcess(0x73)]
        public FxCaseResult Case_BlitzBeat(FxRuntime rt) { Fn_b32ad0(rt); return FxCaseResult.Continue; }
    }
}
