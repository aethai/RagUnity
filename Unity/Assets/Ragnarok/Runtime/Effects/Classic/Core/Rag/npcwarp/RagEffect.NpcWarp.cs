// Synced from reference/port/src/RoClassicFx/Rag/npcwarp/RagEffect.NpcWarp.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// npcwarp: EF 321 (0x141), the portal under a map warp NPC (job 45). CNpc::vf15_cc2340 launches it once per warp
    /// NPC (LaunchEffect(this, 0x141, vec3(0), 0)); FUN_00b3c420 gives it 99999999 ticks. CRagEffect::OnProcess hands
    /// ids 300..373 to vf14 (0xb46290), whose case 0x15 (@0xb46722) is <c>push 0; push 0xf7ddac; call fn_b8fa90; mov al, 1</c>.
    /// </summary>
    public readonly partial struct RagEffect
    {
        /// <summary>CP949 이펙트\particle1.spr (0xffa408) / .act (0xffa420), one char per byte.</summary>
        const string NpcWarpParticleSpr = "ÀÌÆÑÆ®\\particle1.spr";
        const string NpcWarpParticleAct = "ÀÌÆÑÆ®\\particle1.act";

        /// <summary>EF 321 (0x141; vf14 case 0x15 @0xb46722): fn_b8fa90(this, "effect\ring_blue.tga", 0), returns 1.</summary>
        [FxEffectProcess(0x141)]
        public FxCaseResult Case_NpcWarpPortal(FxRuntime rt)
        {
            Fn_b8fa90(rt, "effect\\ring_blue.tga", 0);
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// CRagEffect::fn_b8fa90 (0xb8fa90, thiscall (texture, char <paramref name="kind"/>), RET 8) — the warp portal.
        /// stateCnt 99999998 wraps to 0 and replays tick 0 (so the 99999999-tick effect never ends).
        /// Tick 0: prim 0xc ground disc (alpha_down.tga, duration 99999999, f_1dc |= 1, f_348 15, f_398 −1.5,
        /// f_310 90, f_324 0, f_2d4 128, f_328 12.8, fade-out from duration − 10); the effect position is not touched.
        /// Ticks 1 and 2: the owner's position is copied into the effect, then one prim 0x41 ring set each (f_1c4 5,
        /// the effect's life, the texture, f_318 = 11 / 4 for kind 0, 18 for kind 1; anything else leaves it):
        /// tick 1 radius 2.5 / 5 / 7.5 / 10, start angle 270 / 0 / 90 / 180, tilt 53 / 60 / 55 / 50;
        /// tick 2 radius 2.7 / 5.2 / 7.7 / 10.2, start angle 271 / 1 / 91 / 181, tilt 52 / 59 / 54 / 49.
        /// Every 10th tick (tick 0 included): one prim 7 particle1 sprite, 70 ticks, at 4.5 or 5.25 from the centre in a
        /// random direction, rising (f_2ec −0.3, decelerating), spinning ±(1.5..2.5), size 0.7, fading the last third.
        /// No sound. Returns the low byte of the last value computed (vf14 ignores it).
        /// </summary>
        public void Fn_b8fa90(FxRuntime rt, string texture, byte kind)
        {
            int t = m_stateCnt;
            if (t == 0x5f5e0fe)
            {
                m_stateCnt = 0;
            }
            else
            {
                if (t == 1)
                {
                    FollowMaster();
                    uint a = LaunchNpcWarpRings(rt, texture, kind, 11.0f);
                    NpcWarpBlock(a + 0x3e8, 0x10e, 2.5f, 0x35);
                    NpcWarpBlock(a + 0x49c, 0, 5.0f, 0x3c);
                    NpcWarpBlock(a + 0x550, 0x5a, 7.5f, 0x37);
                    NpcWarpBlock(a + 0x604, 0xb4, 10.0f, 0x32);
                    t = m_stateCnt;
                }
                if (t == 2)
                {
                    FollowMaster();
                    uint a = LaunchNpcWarpRings(rt, texture, kind, 4.0f);
                    NpcWarpBlock(a + 0x3e8, 0x10f, 2.7f, 0x34);
                    NpcWarpBlock(a + 0x49c, 1, 5.2f, 0x3b);
                    NpcWarpBlock(a + 0x550, 0x5b, 7.7f, 0x36);
                    NpcWarpBlock(a + 0x604, 0xb5, 10.2f, 0x31);
                    t = m_stateCnt;
                }
                if (t != 0) goto particle;
            }

            {
                Prim p = LaunchEffectPrim(rt, 0xc, 0f, 0f, 0f);
                m_lastPrim = p;
                p.m_duration = 99999999;
                p = m_lastPrim; p.f_1dc |= 1;
                p = m_lastPrim; p.f_348 = 15.0f;
                p = m_lastPrim; p.f_398 = p.f_398 - 1.5f;
                p = m_lastPrim; p.f_310 = 90.0f;
                p = m_lastPrim; p.f_324 = 0.0f;
                p = m_lastPrim; p.f_2d4 = 128.0f;
                p = m_lastPrim; p.f_328 = p.f_2d4 / 10.0f;
                p = m_lastPrim; p.f_1f0 = p.m_duration + -10;
                m_lastPrim.SnapshotTrail_ad58d0();
                SetFirstTexture(rt, m_lastPrim, "effect\\alpha_down.tga");
                t = m_stateCnt;
            }

        particle:
            if (t % 10 != 0) return;
            {
                Prim p = LaunchEffectPrim(rt, 7, 0f, 0f, 0f);
                m_lastPrim = p;
                p.f_348 = (float)(RandMod(rt, 200) / 100) * 0.75f + 4.5f;
                p = m_lastPrim; p.f_34c = 0.0005f;
                p = m_lastPrim; p.m_duration = 0x46;
                int ang = RandMod(rt, 0x168);
                p = m_lastPrim; p.f_30c = (float)ang;
                if (RandMod(rt, 2) == 0)
                {
                    int n = RandMod(rt, 0xb);
                    p = m_lastPrim; p.f_330 = 2.5f - (float)n * 0.1f;
                }
                else
                {
                    int n = RandMod(rt, 0xb);
                    p = m_lastPrim; p.f_330 = (float)n * 0.1f - 2.5f;
                }
                p = m_lastPrim; p.f_2ec = -0.3f;
                p = m_lastPrim; p.f_2f0 = -(p.f_2ec / (float)p.m_duration) / 1.5f;
                p = m_lastPrim;
                FxMatrix.MakeRotationY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
                p = m_lastPrim;
                float s = p.f_348;
                uint m = p.A + 0x160;
                float x = M.F32(m + 0xc) * 0f + M.F32(m) * 0f;
                x += s * M.F32(m + 0x18);
                x += M.F32(m + 0x24);
                p.f_394 = x;
                float y = M.F32(m + 0x10) * 0f + M.F32(m + 4) * 0f;
                y += s * M.F32(m + 0x1c);
                y += M.F32(m + 0x28);
                p.f_398 = y;
                float z = M.F32(m + 0x14) * 0f + M.F32(m + 8) * 0f;
                z += s * M.F32(m + 0x20);
                z += M.F32(m + 0x2c);
                p.f_39c = z;
                int sz = RandMod(rt, 10);
                p = m_lastPrim; p.f_318 = (float)(sz / 10) + 0.7f;
                p = m_lastPrim;
                int d = p.m_duration;
                p.f_1f0 = d - d / 3;
                rt.SetSprName(m_lastPrim.A, NpcWarpParticleSpr);
                rt.SetActName(m_lastPrim.A, NpcWarpParticleAct);
                m_lastPrim.SnapshotTrail_ad58d0();
            }
        }

        /// <summary>
        /// fn_b8fa90's ring launch: prim 0x41, f_1c4 = 5, the effect's life, one texture, f_318 = <paramref name="f318"/>
        /// for kind 0 / 18 for kind 1 (other kinds leave the launch value).
        /// </summary>
        uint LaunchNpcWarpRings(FxRuntime rt, string texture, byte kind, float f318)
        {
            Prim p = LaunchEffectPrim(rt, 0x41, 0f, 0f, 0f);
            m_lastPrim = p;
            p.f_1c4 = 5;
            p = m_lastPrim; p.m_duration = f_17c;
            SetFirstTexture(rt, m_lastPrim, texture);
            p = m_lastPrim;
            if (kind == 0) p.f_318 = f318;
            else if (kind == 1) p.f_318 = 18.0f;
            return m_lastPrim.A;
        }

        /// <summary>
        /// One fn_b8fa90 ring block: enable, age 0, span 360, start angle, alpha 0, radius (+0x80) and band length (+8)
        /// = <paramref name="radius"/>, tilt, +0x88 = 0, the 21 segment lengths and the 21 segment bytes cleared.
        /// </summary>
        void NpcWarpBlock(uint b, short start, float radius, short tilt)
        {
            M.U8(b, 1);
            M.I32(b + 0xc, 0);
            M.U16(b + 6, 0x168);
            M.I16(b + 0x10, start);
            M.I16(b + 2, 0);
            M.F32(b + 0x80, radius);
            M.F32(b + 8, M.F32(b + 0x80));
            M.I16(b + 0x7e, tilt);
            M.U32(b + 0x88, 0);
            for (uint j = 0; j < 21; j++)
            {
                M.U32(b + 0x14 + j * 4, 0);
                M.U8(b + 0x68 + j, 0);
            }
        }
    }
}
