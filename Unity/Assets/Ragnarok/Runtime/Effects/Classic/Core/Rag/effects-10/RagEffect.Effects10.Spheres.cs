// Synced from reference/port/src/RoClassicFx/Rag/effects-10/RagEffect.Effects10.Spheres.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// EF 833 (0x341, EF_CHOOKGI_N; case 0xb48772): fn_b95210(this, 3) — spirit spheres around the owner (EFST 350);
        /// the sphere count is f_180 (set by the launcher, see NOTES), 1..5.
        /// </summary>
        [FxEffectProcess(0x341)]
        public FxCaseResult Case_E10_Spheres(FxRuntime rt) { Fn_b95210(rt, 3); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b95210 (0xb95210, thiscall (unused), ret 4) — follows the owner; at tick 0 launches
        /// n = min(cvtt(f_180), 5) prims 0x91 (none when n &lt; 1), flags 5, living as long as the effect: slot 0 on,
        /// six random angles (rand() % 360 each: +0x3fc, +0x400, +0x404, +0x408, +0x40c, +0x410), orbit start
        /// +0x414 = 360·i / f_180, height +0x418 = −15, size +0x468 = 1, +0x450 = 1, random spin +0x41c, alpha 0,
        /// textures thunder_center.bmp + freeze_a.bmp, and four trail rings (age −20k, random phase +0x3ee, radius 3,
        /// random angle +0x3f8, alpha +0x466 = 200).
        /// </summary>
        public void Fn_b95210(FxRuntime rt, int unused)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            int n = FxOps.Cvtt(f_180);
            if (n > 5) n = 5;
            else if (n <= 0) return;
            int orbit = 0;
            do
            {
                Prim p = LaunchEffectPrim(rt, 0x91, 0f, 0f, 0f);
                m_lastPrim = p;
                p.f_1c4 = 5;
                { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
                M.U8(m_lastPrim.A + 0x3e8, 1);
                M.F32(m_lastPrim.A + 0x3fc, (float)Rand360_b95210(rt));
                M.F32(m_lastPrim.A + 0x400, (float)Rand360_b95210(rt));
                M.F32(m_lastPrim.A + 0x404, (float)Rand360_b95210(rt));
                M.F32(m_lastPrim.A + 0x408, (float)Rand360_b95210(rt));
                M.F32(m_lastPrim.A + 0x40c, (float)Rand360_b95210(rt));
                M.F32(m_lastPrim.A + 0x410, (float)Rand360_b95210(rt));
                M.F32(m_lastPrim.A + 0x414, (float)orbit / f_180);
                M.F32(m_lastPrim.A + 0x418, -15.0f);
                M.F32(m_lastPrim.A + 0x468, 1.0f);
                M.F32(m_lastPrim.A + 0x424, 0f);
                M.U8(m_lastPrim.A + 0x450, 1);
                M.F32(m_lastPrim.A + 0x41c, (float)Rand360_b95210(rt));
                M.U16(m_lastPrim.A + 0x3ea, 0);
                M.I32(m_lastPrim.A + 0x204, 2);
                { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
                uint t0 = rt.GetTexture("effect\\thunder_center.bmp");
                M.U32((uint)m_lastPrim.f_144, t0);
                uint t1 = rt.GetTexture("effect\\freeze_a.bmp");
                M.U32((uint)m_lastPrim.f_144 + 4, t1);
                uint off = 0;
                int age = 0;
                do
                {
                    M.I32(m_lastPrim.A + off + 0x3f4, age);
                    int r = rt.Rand.Next() % 0x168;
                    M.U16(m_lastPrim.A + off + 0x3ee, (ushort)(r < 0 ? 0 : r));
                    M.F32(m_lastPrim.A + off + 0x3f0, 3.0f);
                    r = rt.Rand.Next() % 0x168;
                    age -= 0x14;
                    M.U16(m_lastPrim.A + off + 0x3f8, (ushort)(r < 0 ? 0 : r));
                    M.U16(m_lastPrim.A + off + 0x466, 200);
                    off += 0xb4;
                } while (age > -0x50);
                orbit += 0x168;
                n--;
            } while (n != 0);
        }

        // Ported by hand from game.exe (OnProcess case 0xb56fac, vf15 cases 0xb4cad3 / 0xb4b81a, fn_b6db80).

        /// <summary>EF 228 (0xe4, EF_CHOOKGI; case 0xb56fac): fn_b6db80(this, 0) — the ZC_SPIRITS spheres of most jobs.</summary>
        [FxEffectProcess(0xe4)]
        public FxCaseResult Case_E10_Chookgi(FxRuntime rt) { Fn_b6db80(rt, 0); return FxCaseResult.Continue; }

        /// <summary>EF 504 (0x1f8, EF_CHOOKGI2; vf15 case 0xb4cad3): fn_b6db80(this, 1) — the spheres of job 4038 (Baby Monk).</summary>
        [FxEffectProcess(0x1f8)]
        public FxCaseResult Case_E10_Chookgi2(FxRuntime rt) { Fn_b6db80(rt, 1); return FxCaseResult.Continue; }

        /// <summary>EF 629 (0x275, EF_CHOOKGI_FIRE; vf15 case 0xb4b81a): fn_b6db80(this, 3) — the Gunslinger line's coins.</summary>
        [FxEffectProcess(0x275)]
        public FxCaseResult Case_E10_ChookgiCoins(FxRuntime rt) { Fn_b6db80(rt, 3); return FxCaseResult.Continue; }

        /// <summary>
        /// CRagEffect::fn_b6db80 (0xb6db80, thiscall, ret 4: kind) — follows the owner; at tick 0 launches
        /// n = min(cvtt(f_180), 5) prims 0x20 (none when n &lt; 1), flags 5, living as long as the effect: slot 0 on, six
        /// random angles (+0x3fc..+0x410), random spin +0x41c, alpha and age 0. Kind 1 (Monk): orbit 72·i, height −14,
        /// size 1.3, +0x424 = 1, +0x450 = 0. Kinds 3 / 4 (coins): orbit 360·i / f_180 (72·i past 5), height −15, size 1
        /// (2 for the orbs standing for more than one past 5), +0x450 = 1. Other kinds: orbit 72·i, height −15, size 2,
        /// +0x450 = 0, or past 5 spheres 0x14 / 0x15 for the orbs holding two / three; kinds ≥ 10 add a random orbit
        /// offset, +0x450 = kind, size 6, one texture. Texture: kind 4 rcoin1, 10..13 fire / lightning / water / stone,
        /// else thunder_center.bmp.
        /// </summary>
        public void Fn_b6db80(FxRuntime rt, sbyte kind)
        {
            FollowMaster();
            if (m_stateCnt != 0) return;
            int n = FxOps.Cvtt(f_180);
            if (n > 5) n = 5;
            else if (n <= 0) return;
            int orbit = 0;        // EBX, +72 per orb
            int coinOrbit = 0;    // [ebp-8], +360 per orb
            for (int i = 0; i < n; i++)
            {
                Prim p = LaunchEffectPrim(rt, 0x20, 0f, 0f, 0f);
                m_lastPrim = p;
                p.f_1c4 = 5;
                { Prim lp = m_lastPrim; lp.m_duration = f_17c; }
                uint a = m_lastPrim.A;
                M.U8(a + 0x3e8, 1);
                M.F32(a + 0x3fc, (float)Rand360_b95210(rt));
                M.F32(a + 0x400, (float)Rand360_b95210(rt));
                M.F32(a + 0x404, (float)Rand360_b95210(rt));
                M.F32(a + 0x408, (float)Rand360_b95210(rt));
                M.F32(a + 0x40c, (float)Rand360_b95210(rt));
                M.F32(a + 0x410, (float)Rand360_b95210(rt));
                float count = f_180;
                if (kind == 1)
                {
                    M.F32(a + 0x414, (float)orbit);
                    M.F32(a + 0x418, -14.0f);
                    M.F32(a + 0x468, 1.3f);
                    M.F32(a + 0x424, 1.0f);
                    M.U8(a + 0x450, 0);
                }
                else if (kind == 3 || kind == 4)
                {
                    M.F32(a + 0x414, 5.0f >= count ? (float)coinOrbit / count : (float)orbit);
                    M.F32(a + 0x418, -15.0f);
                    M.F32(a + 0x468, 1.0f);
                    if (count > 5.0f && !(count - i < 6.0f)) M.F32(a + 0x468, 2.0f);
                    M.F32(a + 0x424, 0f);
                    M.U8(a + 0x450, 1);
                }
                else
                {
                    M.F32(a + 0x414, (float)orbit);
                    M.F32(a + 0x418, -15.0f);
                    M.F32(a + 0x468, 2.0f);
                    M.F32(a + 0x424, 0f);
                    M.U8(a + 0x450, 0);
                    if (count > 10.0f)
                    {
                        M.F32(a + 0x468, 2.0f);
                        M.U8(a + 0x450, (byte)(count - i < 11.0f ? 0x14 : 0x15));
                    }
                    else if (count > 5.0f && !(count - i < 6.0f))
                    {
                        M.F32(a + 0x468, 2.0f);
                        M.U8(a + 0x450, 0x14);
                    }

                    if (kind >= 0xa)
                    {
                        float o = (float)(rt.Rand.Next() % 0x168) + M.F32(a + 0x414);
                        M.F32(a + 0x414, o);
                        if (!(o < 360.0f)) M.F32(a + 0x414, o - 360.0f);
                        M.U8(a + 0x450, (byte)kind);
                        M.F32(a + 0x468, 6.0f);
                        M.I32(a + 0x204, 1);
                    }
                }

                M.F32(a + 0x41c, (float)Rand360_b95210(rt));
                M.U16(a + 0x3ea, 0);
                M.I32(a + 0x3f4, 0);
                { Prim lp = m_lastPrim; lp.f_144 = (int)rt.NewArray((ulong)(uint)lp.f_204 * 4); }
                string texture;
                switch (kind)
                {
                    case 4: texture = "effect\\RL_richs_coin_B\\rcoin1.bmp"; break;
                    case 10: texture = "effect\\fire-1.bmp"; break;
                    case 11: texture = "effect\\lightning-1.bmp"; break;
                    case 12: texture = "effect\\water-1.bmp"; break;
                    case 13: texture = "effect\\stone-1.bmp"; break;
                    default: texture = "effect\\thunder_center.bmp"; break;
                }

                M.U32((uint)m_lastPrim.f_144, rt.GetTexture(texture));
                orbit += 0x48;
                coinOrbit += 0x168;
            }
        }

        /// <summary>rand() % 360, negative remainders → 0 (never for the CRT rand()).</summary>
        static int Rand360_b95210(FxRuntime rt)
        {
            int r = rt.Rand.Next() % 0x168;
            return r < 0 ? 0 : r;
        }
    }
}
