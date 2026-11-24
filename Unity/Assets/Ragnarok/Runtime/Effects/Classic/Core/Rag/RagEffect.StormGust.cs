// Synced from reference/port/src/RoClassicFx/Rag/RagEffect.StormGust.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::fn_b62540 (0xb62540) — OnProcess of EF_STORMGUST (89): sound at tick 0, two ice
        /// crystals (prim type 9) every 29 ticks for t in [31, 150], then the StormGust.str layer
        /// driven by the STR player at offset (0, -80, 0).
        /// </summary>
        public bool Process_StormGust(FxRuntime rt)
        {
            if (m_stateCnt == 0)
                rt.PlaySound3DRelative("effect\\wizard_stormgust.wav", m_pos_x, 0.0f, m_pos_z, 0xfa, 0x28, 1.0f, 0);

            if (!rt.MinEffect && m_stateCnt % 0x1d == 0 && (uint)(m_stateCnt - 0x1f) < 0x78u)
            {
                for (int i = 0; i < 2; i++)
                {
                    int rotRand = rt.Rand.Next();
                    int radRand = rt.Rand.Next();
                    int radius = radRand % 0x19 >= 0 ? radRand % 0x19 : 0;

                    Prim p = LaunchEffectPrim(rt, 9, 0f, 0f, 0f);
                    m_lastPrim = p;
                    p.m_duration = 0xd7 - m_stateCnt;
                    p.f_1dc |= 1;

                    int angRand = rt.Rand.Next();
                    int ang = angRand % 0x168 >= 0 ? angRand % 0x168 : 0;
                    int angDeg = FxOps.Cvtt((float)ang);
                    float s = FxMath.GetSin(angDeg);
                    float c = FxMath.GetCos(angDeg);
                    p.f_398 = 20.0f;
                    p.f_394 = s * (float)(radius + 3);
                    p.f_39c = -(float)(radius + 3) * c;

                    int sizeRand = rt.Rand.Next();
                    int size = sizeRand % 100 >= 0 ? sizeRand % 100 : 0;
                    p.f_318 = (float)(size + 400) / 100.0f;
                    p.f_360 = 20.0f;
                    int rot = rotRand % 0x168;
                    p.f_30c = (float)(rot >= 0 ? rot : 0);
                    p.f_310 = 100.0f;
                    FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
                    FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                    M.Copy(p.A + 0x190, p.A + 0x160, 0x30);
                    p.f_2bc = 1.5f;
                    p.f_2c0 = 0f;
                    p.f_324 = 250.0f;
                    p.f_1d4 = 3;
                    p.f_1f0 = p.m_duration - 10;
                    p.f_144 = (int)rt.NewArray((ulong)(uint)p.f_204 * 4);
                    M.U32((uint)p.f_144, rt.GetTexture("effect\\ice.tga"));
                }
            }
            return rt.StrPlayer(this, 0f, -80.0f, 0f);
        }
    }
}
