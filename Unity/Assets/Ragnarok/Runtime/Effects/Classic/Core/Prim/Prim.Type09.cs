// Synced from reference/port/src/RoClassicFx/Prim/Prim.Type09.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::fn_adf860 (0xadf860) — OnProcess for prim type 9 (e.g. Storm Gust ice crystal):
        /// position = master + offset + accumulated matrix-driven velocity, then alpha keyframes/fade.
        /// </summary>
        public void Process_adf860()
        {
            RagEffect master = m_master;
            float my = master.m_pos_y, mz = master.m_pos_z;
            f_37c = master.m_pos_x + f_394;
            f_380 = my + f_398;
            f_384 = mz + f_39c;

            float speed, accel;
            if (f_1e0 == f_200)
            {
                speed = f_2a0;
                accel = f_29c;
                f_2c0 = speed;
            }
            else
            {
                speed = f_2c0;
                accel = f_2bc;
            }
            float v = speed + accel;
            f_2bc = v;

            float dx = f_16c * 0.0f + f_160 * 0.0f + v * f_178 + f_184;
            f_370 = dx;
            dx += f_388;
            float dy = f_170 * 0.0f + f_164 * 0.0f + v * f_17c + f_188;
            f_374 = dy;
            dy += f_38c;
            float dz = f_174 * 0.0f + f_168 * 0.0f + v * f_180 + f_18c;
            f_388 = dx;
            f_38c = dy;
            f_378 = dz;
            dz += f_390;
            f_390 = dz;
            m_pos_x = dx + f_37c;
            m_pos_y = dy + f_380;
            m_pos_z = dz + f_384;

            if ((f_1dc & 1) != 0 && f_1e0 == f_1d4)
            {
                f_2c0 = 0f;
                f_2bc = 0f;
            }
            if ((f_1dc & 2) != 0)
            {
                if (f_1e0 == m_duration - 0x1e)
                {
                    f_2bc = f_2a8;
                    f_2c0 = f_2b4;
                }
                return;
            }
            UpdateAlpha_aed050();
        }

        /// <summary>
        /// CEffectPrim::fn_aed050 (0xaed050) — alpha keyframes, ping-pong, fade to master end, fade-out, ARGB.
        /// Inlined by the compiler into several process functions.
        /// </summary>
        public void UpdateAlpha_aed050()
        {
            uint flags;
            bool fadeToMasterEnd;
            RagEffect master;
            if (f_154 == 0)
            {
                if (f_150 != 0)
                {
                    uint k = (uint)f_1d8;
                    uint keys = f_258_u;
                    if ((int)k < 0 || (uint)((f_25c - f_258) >> 2) <= k)
                    {
                        f_1d8 = 0;
                        k = 0;
                        keys = f_258_u;
                    }
                    if ((float)f_1e0 == M.F32(keys + k * 4))
                    {
                        f_328 = M.F32((uint)f_264 + k * 4);
                        f_1d8 = (int)(k + 1);
                    }
                }
                flags = (uint)f_1dc;
                PingPong(flags);
                if (f_15c != 0) goto FadeOut;
                master = m_master;
                fadeToMasterEnd = m_duration - f_1e0 > (master.f_17c - master.m_stateCnt) + 1;
            }
            else
            {
                uint k = (uint)f_1d8;
                uint keys = f_258_u;
                if ((int)k < 0 || (uint)((f_25c - f_258) >> 2) - 1u < k)
                {
                    f_1d8 = 0;
                    k = 0;
                    keys = f_258_u;
                }
                if ((float)f_1e0 == M.F32(keys + k * 4))
                {
                    f_324 = M.F32((uint)f_264 + k * 4);
                    f_2d4 = M.F32((uint)f_270 + k * 4);
                    f_1d8 = (int)(k + 1);
                }
                flags = (uint)f_1dc;
                PingPong(flags);
                if (f_15c != 0) goto FadeOut;
                master = m_master;
                fadeToMasterEnd = m_duration - f_1e0 > (master.f_17c - master.m_stateCnt) + 1;
            }
            if (fadeToMasterEnd)
            {
                flags = (uint)f_1dc & 0xffffff7f;
                f_15c = 1;
                f_1dc = (int)flags;
                f_328 = -(f_324 / (float)(master.f_17c - master.m_stateCnt));
            }
        FadeOut:
            float a;
            if (f_1e0 == f_1f0)
            {
                f_1dc = (int)(flags & 0xffffff7f);
                a = -(f_324 / (float)(m_duration - f_1e0)) * f_32c;
                f_328 = a;
            }
            else
            {
                a = f_328;
            }
            a += f_324;
            float lo = f_2d8;
            if (f_2d8 <= a) lo = a;
            a = f_2d4;
            if (lo <= f_2d4) a = lo;
            f_324 = a;
            m_argb = (int)(((uint)(FxOps.Cvtt(a) * 0x100 + (byte)f_23c) * 0x100 + (byte)f_240) * 0x100 + (byte)f_244);
        }

        void PingPong(uint flags)
        {
            if ((sbyte)flags < 0)
            {
                bool within = 0f <= f_328 ? f_324 < f_2d4 : f_2d8 < f_324;
                if (!within) f_328 = f_328 * -1f;
            }
        }

        uint f_258_u => (uint)f_258;
    }
}
