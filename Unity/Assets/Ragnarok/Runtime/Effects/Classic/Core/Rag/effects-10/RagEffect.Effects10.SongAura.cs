// Synced from reference/port/src/RoClassicFx/Rag/effects-10/RagEffect.Effects10.SongAura.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// EF 861 / 865 / 867 / 871 / 873 / 875 / 877 (0x35d, 0x361, 0x363, 0x367, 0x369, 0x36b, 0x36d; vf15 cases
        /// 0xb483a6 .. 0xb4840c — EF_CIRCLEPOWER*): the song / dance aura on every affected actor (EFST 70..84, 439..454),
        /// fn_b95ae0(this, "effect\freeze_a.bmp", 2..8) — the colour scheme is the id's position in that list.
        /// </summary>
        [FxEffectProcess(0x35d, 0x361, 0x363, 0x367, 0x369, 0x36b, 0x36d)]
        public FxCaseResult Case_E10_SongAura(FxRuntime rt)
        {
            sbyte scheme;
            switch (m_type)
            {
                case 0x35d: scheme = 2; break;
                case 0x361: scheme = 3; break;
                case 0x363: scheme = 4; break;
                case 0x367: scheme = 5; break;
                case 0x369: scheme = 6; break;
                case 0x36b: scheme = 7; break;
                default: scheme = 8; break;   // 0x36d
            }
            Fn_b95ae0(rt, "effect\\freeze_a.bmp", scheme);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b95ae0 (0xb95ae0, thiscall (texture, scheme), ret 8) — follows the owner; at ticks 0 and 10
        /// launches one prim 0x84 (flags 1|5, lives as long as the effect) with four rising rings: slot k (0..3) starts
        /// at age −20k, random phase +0x3ee and angle +0x3f8 (rand() % 360 each, in that order), radius 10, alpha 100,
        /// +0x40c = 10, +0x408 = k and +0x410 = scheme (the colour selector of fn_afa520).
        /// </summary>
        public void Fn_b95ae0(FxRuntime rt, string texture, sbyte scheme)
        {
            FollowMaster();
            int t = m_stateCnt;
            if (t == 0)
            {
                LaunchSongRings_b95ae0(rt, texture, scheme);
                t = m_stateCnt;
            }
            if (t == 10) LaunchSongRings_b95ae0(rt, texture, scheme);
        }

        /// <summary>The (duplicated) launch block of fn_b95ae0.</summary>
        void LaunchSongRings_b95ae0(FxRuntime rt, string texture, sbyte scheme)
        {
            Prim p = LaunchEffectPrim(rt, 0x84, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 0;
            { Prim lp = m_lastPrim; lp.f_1c4 = lp.f_1c4 | 1; }
            { Prim lp = m_lastPrim; lp.f_1c4 = lp.f_1c4 | 5; }
            { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
            { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
            uint tex = rt.GetTexture(texture);
            M.U32((uint)m_lastPrim.f_144, tex);
            { Prim lp = m_lastPrim; lp.f_318 = 1.0f; }
            int k = 0, age = 0;
            uint off = 0;
            do
            {
                uint b = m_lastPrim.A + off;
                M.U8(b + 0x3e8, 1);
                M.I32(m_lastPrim.A + off + 0x3f4, age);
                int r = rt.Rand.Next() % 0x168;
                M.U16(m_lastPrim.A + off + 0x3ee, (ushort)(r < 0 ? 0 : r));
                M.F32(m_lastPrim.A + off + 0x3f0, 10.0f);
                r = rt.Rand.Next() % 0x168;
                M.U16(m_lastPrim.A + off + 0x3f8, (ushort)(r < 0 ? 0 : r));
                M.U16(m_lastPrim.A + off + 0x3ea, 100);
                M.F32(m_lastPrim.A + off + 0x40c, 10.0f);
                float fk = (float)k;
                k++;
                age -= 0x14;
                M.F32(m_lastPrim.A + off + 0x408, fk);
                M.F32(m_lastPrim.A + off + 0x410, (float)(int)scheme);
                off += 0xb4;
            } while (age > -0x50);
        }
    }
}
