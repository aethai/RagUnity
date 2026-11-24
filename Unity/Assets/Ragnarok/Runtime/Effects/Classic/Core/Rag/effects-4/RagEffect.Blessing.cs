// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.Blessing.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// Launches a prim and stores it as m_lastPrim (the client's <c>m_lastPrim = LaunchEffectPrim(this, type, 0, 0, 0)</c>).
        /// </summary>
        Prim LaunchLast(FxRuntime rt, int type, float dx = 0f, float dy = 0f, float dz = 0f)
        {
            Prim p = LaunchEffectPrim(rt, type, dx, dy, dz);
            m_lastPrim = p;
            return p;
        }

        /// <summary>
        /// The client's texture-array idiom: <c>prim.f_144 = new[] (f_204 * 4 bytes); prim.f_144[0] = GetTexture(name)</c>.
        /// </summary>
        static void SetPrimTexture(FxRuntime rt, Prim p, string name)
        {
            p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
            uint tex = rt.GetTexture(name);
            p.M.U32((uint)p.f_144, tex);
        }

        /// <summary>
        /// CRagEffect::fn_b32560 (0xb32560) — Blessing (EF 42): at tick 0 sound + a prim type 0xc (alpha_down.tga
        /// column); prims type 5 (sprite) at ticks 0, 3, 6, 9; every 4th tick a random rising sparkle (type 5).
        /// </summary>
        public void Fn_b32560(FxRuntime rt)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t == 0)
            {
                rt.PlaySound3DRelative("effect\\EF_Blessing.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);
                Prim p = LaunchLast(rt, 0xc);
                p.m_duration = f_17c;
                p = m_lastPrim; p.f_1dc |= 1;
                p = m_lastPrim; p.f_1c4 |= 0x200;
                p = m_lastPrim;
                p.f_348 = 10.0f;
                p.f_310 = 90.0f;
                M.I32(p.A + 0x23c, 0x20);
                p.f_240 = 0xb0;
                p.f_244 = 0xe8;
                p.f_324 = 0.0f;
                p.f_2d4 = 100.0f;
                p.f_328 = p.f_2d4 / 30.0f;
                p.f_1f0 = p.m_duration - 0x1e;
                p.SnapshotTrail_ad58d0();
                SetPrimTexture(rt, m_lastPrim, "effect\\alpha_down.tga");
                t = m_stateCnt;
            }
            if (t % 3 == 0 && t < 10)
            {
                Prim p = LaunchLast(rt, 5);
                p.m_duration = f_17c;
                p = m_lastPrim;
                p.f_1e8 = 2;
                p.f_324 = (float)(200 - m_stateCnt * 6);
                p.f_394 = 0.0f;
                p.f_398 = -25.0f;
                p.f_39c = 0.0f;
                rt.SetSprName(p.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00c3\u00e0\u00ba\u00b9.spr");
                rt.SetActName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00c3\u00e0\u00ba\u00b9.act");
                m_lastPrim.SnapshotTrail_ad58d0();
                t = m_stateCnt;
            }
            if ((t & 3) == 0)
            {
                Prim p = LaunchLast(rt, 5);
                p.m_duration = 0x50;
                p = m_lastPrim; p.f_1dc |= 0x80;
                p = m_lastPrim;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(RandMod(rt, 0x168)));
                p = m_lastPrim;
                float r = (float)(RandMod(rt, 7) + 2);
                p.f_394 = p.f_160 * 0.0f - p.f_16c * 25.0f + r * p.f_178 + p.f_184;
                p.f_398 = p.f_164 * 0.0f - p.f_170 * 25.0f + r * p.f_17c + p.f_188;
                p.f_39c = p.f_168 * 0.0f - p.f_174 * 25.0f + r * p.f_180 + p.f_18c;
                p = m_lastPrim;
                FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(-0x5a));
                int speed = RandMod(rt, 0x14);
                p = m_lastPrim;
                p.f_2bc = (float)(speed + 0x14) / 100.0f;
                p = m_lastPrim;
                p.f_2c0 = -(p.f_2bc / (float)p.m_duration) * 0.5f;
                p = m_lastPrim; p.f_324 = 0.0f;
                p = m_lastPrim; p.f_318 = 0.5f;
                p = m_lastPrim;
                p.f_1f0 = p.m_duration - p.m_duration / 5;
                p = m_lastPrim;
                p.f_328 = p.f_2d4 / 10.0f;
                rt.SetActName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle6.act");
                rt.SetSprName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle6.spr");
                m_lastPrim.SnapshotTrail_ad58d0();
            }
        }

        /// <summary>Case 42 (0x2a) — Blessing.</summary>
        [FxEffectProcess(0x2a)]
        public FxCaseResult Case_Blessing(FxRuntime rt)
        {
            Fn_b32560(rt);
            return FxCaseResult.Continue;
        }
    }
}
