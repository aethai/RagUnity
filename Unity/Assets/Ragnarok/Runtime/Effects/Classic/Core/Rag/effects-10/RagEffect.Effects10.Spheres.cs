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

        /// <summary>rand() % 360, negative remainders → 0 (never for the CRT rand()).</summary>
        static int Rand360_b95210(FxRuntime rt)
        {
            int r = rt.Rand.Next() % 0x168;
            return r < 0 ? 0 : r;
        }
    }
}
