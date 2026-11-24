// Synced from reference/port/src/RoClassicFx/Rag/effects-3/RagEffect.Effects3.FrostSight.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>0xffa6cc / 0xffa6e0: CP949 "이펙트\\Sight.spr" / ".act" as byte strings.</summary>
        internal const string E3SightSpr = "ÀÌÆÑÆ®\\Sight.spr";
        internal const string E3SightAct = "ÀÌÆÑÆ®\\Sight.act";

        /// <summary>
        /// CRagEffect::fn_b3be30 (0xb3be30) — EF 27 (Frost Diver): an ice trail from the caster towards the target
        /// (effect +0x180/+0x188). Tick 0 turns the effect towards the target (SetDirectionTo) and derives the step
        /// (+0x198..+0x1a0, back-off 5) and stride (+0x18c..+0x194, floats although the field table types them int).
        /// Every tick until the head reaches the target (distance ≤ 2.5 → +0x170 = 1) one ice crystal (prim 9, ice.tga).
        /// </summary>
        public void Fn_b3be30(FxRuntime rt, byte variant)
        {
            if (rt.MinEffect) return;
            if (m_stateCnt == 0)
            {
                rt.FaceTowards(A, f_180, f_188);
                float rad = FxMath.DegToRad(FxOps.Cvtt(-M.F32(A + 0x4c)));
                // MakeRotationY into a stack matrix (identity, then c/−s/s/c)
                float s = (float)System.Math.Sin(rad), c = (float)System.Math.Cos(rad);
                float m0 = c, m1 = 0f, m2 = -s, m3 = 0f, m4 = 1f, m5 = 0f, m6 = s, m7 = 0f, m8 = c, m9 = 0f, m10 = 0f, m11 = 0f;
                const float z = 0.0f;
                float a = m3 * z + m0 * z;
                float b = m4 * z + m1 * z;
                float cc = m5 * z + m2 * z;
                f_198 = f_198 - (m6 * 5.0f + a + m9);
                f_19c = f_19c - (m7 * 5.0f + b + m10);
                f_1a0 = f_1a0 - (m8 * 5.0f + cc + m11);
                M.F32(A + 0x18c, m6 * 2.0f + a + m9);
                M.F32(A + 0x190, m7 * 2.0f + b + m10);
                M.F32(A + 0x194, m8 * 2.0f + cc + m11);
            }
            if (M.I32(A + 0x170) != 0) return;

            float px = m_pos_x, py = m_pos_y, pz = m_pos_z;
            m_pos_x = f_198 + px;
            m_pos_y = f_19c + m_pos_y;
            m_pos_z = f_1a0 + m_pos_z;
            float dist = rt.DistanceXZ(A, f_180, f_188);
            if (!(2.5f < dist || float.IsNaN(dist))) M.I32(A + 0x170, 1);
            m_pos_x = px;
            m_pos_y = py;
            m_pos_z = pz;

            Prim p = LaunchEffectPrim(rt, 9, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0x28;
            { Prim lp_ = m_lastPrim; lp_.f_1dc |= 1; }
            { Prim lp_ = m_lastPrim; lp_.f_450 = (sbyte)(variant == 3 ? 3 : 0); }
            int angle = E3RandMod(rt, 0x168);
            float r = (float)(E3RandMod(rt, 10) + 5) / 10.0f;
            float sn = FxMath.GetSin(angle);
            float cs = FxMath.GetCos(angle);
            p = m_lastPrim;
            p.f_398 = 30.0f;
            p.f_394 = sn * r;
            p.f_39c = -r * cs;
            p = m_lastPrim;
            p.f_394 = f_198 + p.f_394;
            p.f_398 = f_19c + p.f_398;
            p.f_39c = f_1a0 + p.f_39c;
            { Prim lp_ = m_lastPrim; lp_.f_318 = (float)(E3RandMod(rt, 0x28) + 0x3c) / 100.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_360 = (float)(E3RandMod(rt, 0x1e) + 0x96) / 10.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_30c = (float)E3RandMod(rt, 0x168); }
            { Prim lp_ = m_lastPrim; lp_.f_310 = (float)(E3RandMod(rt, 0x1e) + 0x4b); }
            p = m_lastPrim;
            FxMatrix.MakeRotationX(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_310)));
            p = m_lastPrim;
            FxMatrix.RotateY(M, p.A + 0x160, FxMath.DegToRad(FxOps.Cvtt(p.f_30c)));
            p = m_lastPrim;
            M.Copy(p.A + 0x190, p.A + 0x160, 0x30);
            { Prim lp_ = m_lastPrim; lp_.f_2bc = 3.0f; }
            p = m_lastPrim;
            float v = -(p.f_2bc / (float)p.m_duration);
            p.f_2c0 = v + v;
            { Prim lp_ = m_lastPrim; lp_.f_1d4 = 0x14; }
            { Prim lp_ = m_lastPrim; lp_.f_1f0 = m_lastPrim.m_duration - 10; }
            E3SetTexture(rt, m_lastPrim, "effect\\ice.tga");
            f_198 = f_198 - M.F32(A + 0x18c);
            f_19c = f_19c - M.F32(A + 0x190);
            f_1a0 = f_1a0 - M.F32(A + 0x194);
        }

        /// <summary>
        /// CRagEffect::fn_b5ed30 (0xb5ed30) — EF 22 (Sight): follows the master; every even tick two sprite prims
        /// (type 5: Sight.spr/act and Shadow.spr/act) on a circle of radius 15 turning 5° per tick.
        /// </summary>
        public void Fn_b5ed30(FxRuntime rt)
        {
            E3FollowMaster();
            E3ProjectMaster(rt);
            if ((m_stateCnt & 1) != 0) return;
            int deg = FxOps.Cvtt((float)m_stateCnt * -5.0f);
            float s = FxMath.GetSin(deg);
            float c = FxMath.GetCos(deg);
            FxMath.DegToRad(deg);   // result discarded (fstp st(0))

            Prim p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            s = s * 15.0f;
            m_lastPrim = p;
            p.m_duration = 0x14;
            p = m_lastPrim;
            p.f_394 = s;
            c = c * -15.0f;
            p.f_398 = -20.0f;
            p.f_39c = c;
            { Prim lp_ = m_lastPrim; lp_.f_3b0 = 0.1f; }
            { Prim lp_ = m_lastPrim; lp_.f_1e8 = 3; }
            { Prim lp_ = m_lastPrim; lp_.f_318 = 2.5f; }
            { Prim lp_ = m_lastPrim; lp_.f_31c = -0.1f; }
            { Prim lp_ = m_lastPrim; lp_.f_324 = 150.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_328 = -3.0f; }
            rt.SetSprName(m_lastPrim.A, E3SightSpr);
            rt.SetActName(m_lastPrim.A, E3SightAct);
            m_lastPrim.SnapshotTrail_ad58d0();

            p = LaunchEffectPrim(rt, 5, 0f, 0f, 0f);
            m_lastPrim = p;
            p.m_duration = 0x14;
            p = m_lastPrim;
            p.f_394 = s;
            p.f_398 = 0.0f;
            p.f_39c = c;
            { Prim lp_ = m_lastPrim; lp_.f_1e8 = 2; }
            { Prim lp_ = m_lastPrim; lp_.f_318 = 1.5f; }
            { Prim lp_ = m_lastPrim; lp_.f_31c = -0.06f; }
            { Prim lp_ = m_lastPrim; lp_.f_324 = 120.0f; }
            { Prim lp_ = m_lastPrim; lp_.f_328 = -1.0f; }
            rt.SetSprName(m_lastPrim.A, "Shadow.spr");
            rt.SetActName(m_lastPrim.A, "Shadow.act");
            m_lastPrim.SnapshotTrail_ad58d0();
        }
    }
}
