// Synced from reference/port/src/RoClassicFx/Prim/prims-1/Prim.Prims1.Process.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CEffectPrim::OnProcess (0xad5a30) cases of the prims-1 types. Most of these types keep up to four
    /// sub-blocks of 0xb4 bytes at +0x3e8 (B+0 active, +2 alpha, +4/+6 angles, +0xc age, +0x80 size,
    /// +0x84.. position, +0x68 texture index).
    /// </summary>
    public readonly partial struct Prim
    {
        // ---------------------------------------------------------------- inline switch cases

        /// <summary>Type 0x22 (inline, 0xad7f33): per block age++, size -= 0.01, alpha in/out, 21 radii = sin[age] * B+8.</summary>
        [FxPrimProcess(0x22)]
        public FxCaseResult Case22(FxRuntime rt)
        {
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                M.F32(b + 0x80, M.F32(b + 0x80) - 0.01f);
                ushort alpha = M.U16(b + 2);
                if (age < 10)
                {
                    M.U16(b + 2, (ushort)(alpha + 10));
                }
                else
                {
                    short na = (short)(alpha - 1);
                    M.I16(b + 2, na);
                    if (na <= 0)
                    {
                        M.I16(b + 2, 0);
                        if (age > 0x1e) M.U8(b, 0);
                        M.F32(b + 0x80, 20f);
                        M.I16(b + 0x7e, 0x5a);
                    }
                }
                for (uint k = 0; k < 21; k++)
                    M.F32(b + 0x14 + k * 4, SinAt(M.I32(b + 0xc)) * M.F32(b + 8));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x2b (inline, 0xad902d): camera angles; per block drift, alpha in/out, spin.</summary>
        [FxPrimProcess(0x2b)]
        public FxCaseResult Case2B(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int old = M.I32(b + 0xc);
                int age = old + 1;
                M.I32(b + 0xc, age);
                if (age > 0) M.F32(b + 0x80, M.F32(b + 0x80) + M.F32(b + 0x14));
                if ((uint)old <= 9)
                {
                    M.U16(b + 2, (ushort)(M.U16(b + 2) + (M.U8(b + 0x68) != 0 ? 0x19 : 0xf)));
                }
                else if (age > 0x19)
                {
                    short a = (short)(M.U16(b + 2) - 7);
                    M.I16(b + 2, a);
                    if (a <= 0) M.I16(b + 2, 0);
                }
                byte kind = M.U8(b + 0x68);
                if (kind == 2) M.U16(b + 0x10, (ushort)(M.U16(b + 0x10) + 3));
                short spin = M.I16(b + 0x10);
                if (spin >= 0x168) M.I16(b + 0x10, (short)(spin - 0x168));
                float f = M.F32(b + 0x1c) + (kind == 2 ? 3f : 5f);
                M.F32(b + 0x1c, f);
                if (f >= 360f) M.F32(b + 0x1c, f - 360f);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x30 (inline, 0xadb109): follow master; per block age++, spin +4/+2, radius = sin * 10 + 20.</summary>
        [FxPrimProcess(0x30)]
        public FxCaseResult Case30(FxRuntime rt)
        {
            FollowMaster_P1();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                M.I32(b + 0xc, M.I32(b + 0xc) + 1);
                short s = (short)(M.U16(b + 6) + (M.F32(b + 0x14) == 1f ? 4 : 2));
                M.I16(b + 6, s);
                if (s >= 0x168) M.I16(b + 6, (short)(s - 0x168));
                M.F32(b + 8, SinAt(M.I16(b + 6)) * 10f + 20f);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x31 (inline, 0xadb1b7): camera angles, follow master; per block fade, sink, spin.</summary>
        [FxPrimProcess(0x31)]
        public FxCaseResult Case31(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                ushort alpha = M.U16(b + 2);
                if (age <= 10)
                {
                    M.U16(b + 2, (ushort)(alpha + 0x19));
                }
                else
                {
                    short a = (short)(alpha - 5);
                    M.I16(b + 2, a);
                    if (a <= 0)
                    {
                        M.I16(b + 2, 0);
                        M.I32(b + 0xc, 0);
                        float y = m_pos_y;
                        if (M.U8(b + 0x69) != 0) y -= 5f;
                        M.F32(b + 0x88, y);
                    }
                    M.F32(b + 0x88, M.F32(b + 0x88) - 0.2f);
                }
                float f = M.F32(b + 0x34) + 5f;
                M.F32(b + 0x34, f);
                if (f >= 360f) M.F32(b + 0x34, f - 360f);
                if (M.U8(b + 0x69) == 1)
                {
                    short s = (short)(M.U16(b + 6) + 5);
                    M.I16(b + 6, s);
                    if (s >= 0x168) M.I16(b + 6, (short)(s - 0x168));
                }
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x36 (inline, 0xada453): camera angles; four blocks fade in (≤10) / out (&gt;20).</summary>
        [FxPrimProcess(0x36)]
        public FxCaseResult Case36(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                if (age <= 10)
                {
                    M.U16(b + 2, (ushort)(M.U16(b + 2) + 0x12));
                }
                else if (age > 0x14)
                {
                    short a = (short)(M.U16(b + 2) - 0xf);
                    M.I16(b + 2, a);
                    if (a < 0) M.I16(b + 2, 0);
                }
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x3a (inline, 0xada303): camera angles, follow master; block 0 spins +3, fades in ≤20, out &gt;100.</summary>
        [FxPrimProcess(0x3a)]
        public FxCaseResult Case3A(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            if (M.U8(A + 0x3e8) == 1)
            {
                int age = M.I32(A + 0x3f4) + 1;
                M.I32(A + 0x3f4, age);
                float f = M.F32(A + 0x41c) + 3f;
                M.F32(A + 0x41c, f);
                if (f >= 360f) M.F32(A + 0x41c, f - 360f);
                if (age <= 0x14) M.U16(A + 0x3ea, (ushort)(M.U16(A + 0x3ea) + 4));
                if (age > 100)
                {
                    short a = (short)(M.U16(A + 0x3ea) - 1);
                    M.I16(A + 0x3ea, a);
                    if (a < 0) M.I16(A + 0x3ea, 0);
                }
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x3b (inline, 0xadab4b): camera angles, follow master; sparks with random heading, flicker flag.</summary>
        [FxPrimProcess(0x3b)]
        public FxCaseResult Case3B(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            int k = 0x24;
            for (int i = 0; k > 8; i++, k -= 7)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                if (age <= 0) continue;
                short s = (short)(M.U16(b + 6) - 5);
                M.I16(b + 6, s);
                if (s < 0) M.I16(b + 6, (short)(s + 0x168));
                if (age == 1)
                {
                    int r = rt.Rand.Next() % 0x168;
                    M.I16(b + 0x10, (short)(r >= 0 ? r : 0));
                }
                else if (age <= 10)
                {
                    M.U16(b + 2, (ushort)(M.U16(b + 2) + 0xf));
                }
                else
                {
                    if (M.U8(b + 0x69) == 1)
                        M.U8(b + 0x68, (byte)(age == k - 1 || age == k ? 1 : 0));
                    short a = (short)(M.U16(b + 2) - 3);
                    M.I16(b + 2, a);
                    if (a < 0)
                    {
                        M.I16(b + 2, 0);
                        M.U8(b, 0);
                    }
                }
                M.F32(b + 0x88, M.F32(b + 0x88) - M.F32(b + 0x1c));
                short dir = M.I16(b + 0x10);
                float speed = M.F32(b + 0x18);
                M.F32(b + 0x84, speed * CosAt(dir) + M.F32(b + 0x84));
                M.F32(b + 0x8c, speed * SinAt(dir) + M.F32(b + 0x8c));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x45 (inline, 0xad9c94): per block alpha curve by kind (B+0x24 = 5 / 2 / other), orbit position.</summary>
        [FxPrimProcess(0x45)]
        public FxCaseResult Case45(FxRuntime rt)
        {
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if ((sbyte)M.U8(b) <= 0) continue;
                M.I32(b + 0xc, M.I32(b + 0xc) + 1);
                if (M.I32(b + 0xc) <= 0) continue;
                float kind = M.F32(b + 0x24);
                int age = M.I32(b + 0xc);
                ushort alpha = M.U16(b + 2);
                bool clamp;
                if (kind == 5f)
                {
                    if (age <= 0x14) { alpha += 8; clamp = false; }
                    else { alpha -= 2; clamp = true; }
                }
                else if (age <= 5)
                {
                    alpha = (ushort)(alpha + (kind == 2f ? 2 : 8));
                    clamp = false;
                }
                else
                {
                    alpha = (ushort)(alpha - (kind == 2f ? 1 : 4));
                    clamp = true;
                }
                if (clamp)
                {
                    M.U16(b + 2, alpha);
                    if ((short)alpha < 0) M.U16(b + 2, 0);
                }
                else M.U16(b + 2, alpha);
                short ang = M.I16(b + 0x10);
                float r = M.F32(b + 0x80);
                M.F32(b + 0x90, r * CosAt(ang) + M.F32(b + 0x84));
                M.F32(b + 0x98, r * SinAt(ang) + M.F32(b + 0x8c));
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x46 (inline, 0xad6a2d): per block fade after 35, phase +8 wrapping at 180 (counter B+0x68), 20-entry history shift.</summary>
        [FxPrimProcess(0x46)]
        public FxCaseResult Case46(FxRuntime rt)
        {
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                if (age <= 0) continue;
                if (age > 0x23) M.U16(b + 2, (ushort)(M.U16(b + 2) - 5));
                if (M.I16(b + 2) < 0) M.I16(b + 2, 0);
                float f = M.F32(b + 0x14) + 8f;
                M.F32(b + 0x14, f);
                if (f > 180f)
                {
                    M.F32(b + 0x14, f - 180f);
                    M.U8(b + 0x68, (byte)(M.U8(b + 0x68) + 1));
                }
                for (uint k = 0; k < 0x14; k++)
                {
                    M.U32(b + 0x64 - k * 4, M.U32(b + 0x60 - k * 4));
                    M.U8(b + 0x7c - k, M.U8(b + 0x7b - k));
                }
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x49 (inline, 0xad6922): follow master; per block alpha up to 0x50 / 0x28 for 8 ticks, then fade out.</summary>
        [FxPrimProcess(0x49)]
        public FxCaseResult Case49(FxRuntime rt)
        {
            FollowMaster_P1();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                if (M.I16(b + 0x7e) < 0) M.I16(b + 0x7e, 0);
                short s = M.I16(b + 0x10);
                if (s >= 0x168) M.I16(b + 0x10, (short)(s - 0x168));
                if (age < 9)
                {
                    if (M.I16(b + 4) == 0)
                    {
                        short a = (short)(M.U16(b + 2) + 10);
                        M.I16(b + 2, a);
                        if (a > 0x50) M.I16(b + 2, 0x50);
                    }
                    else
                    {
                        short a = (short)(M.U16(b + 2) + 5);
                        M.I16(b + 2, a);
                        if (a > 0x28) M.I16(b + 2, 0x28);
                    }
                }
                else
                {
                    short a = (short)(M.U16(b + 2) - 0x1e);
                    M.I16(b + 2, a);
                    if (a < 0)
                    {
                        M.I16(b + 2, 0);
                        M.U8(b, 0);
                    }
                }
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x6d (inline, 0xad7a52): camera angles, follow master; per block grow/shrink, slow spin, bobbing offsets.</summary>
        [FxPrimProcess(0x6d)]
        public FxCaseResult Case6D(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                if (age <= 0) continue;
                if (age <= 10)
                {
                    M.F32(b + 0x48, M.F32(b + 0x48) + 20f);
                }
                else if (age > 0xaa)
                {
                    float f = M.F32(b + 0x48) - 5f;
                    M.F32(b + 0x48, f);
                    if (0f > f) M.F32(b + 0x48, 0f);
                }
                float x = (float)age / 10f / 3f + M.F32(b + 0x90);
                M.F32(b + 0x90, x);
                if (x >= 359f)
                {
                    M.F32(b + 0x90, 359f);
                    x = 359f;
                }
                int ia = FxOps.Cvtt(x);
                M.F32(b + 0x9c, (float)ia / 6f - 30f);
                M.F32(b + 0xa0, -(SinAt(ia) * 20f) - 40f);
                M.F32(b + 0xa4, 0f);
            }
            return FxCaseResult.Continue;
        }

        /// <summary>Type 0x77 (inline, 0xad9481): homing bolt toward (f_388, f_390) with a 3-deep trail.</summary>
        [FxPrimProcess(0x77)]
        public FxCaseResult Case77(FxRuntime rt)
        {
            if (M.U8(A + 0x3e8) != 1) return FxCaseResult.Continue;
            int age = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, age);
            if (age <= 0) return FxCaseResult.Continue;
            short spin = (short)(M.U16(A + 0x466) + 0x1e);
            M.I16(A + 0x466, spin);
            if (spin >= 0x168) M.I16(A + 0x466, (short)(spin - 0x168));
            float speed;
            if (age == 1)
            {
                M.I16(A + 0x3f8, Direction_ad5710(f_388 - m_pos_x, f_390 - m_pos_z));
                speed = f_400;
                age = M.I32(A + 0x3f4);
            }
            else if (age <= 10)
            {
                short a = (short)(M.U16(A + 0x3ea) + 0x19);
                M.I16(A + 0x3ea, a);
                if (a > 0xfa) M.I16(A + 0x3ea, 0xfa);
                speed = f_400 * 1.02f;
                f_400 = speed;
            }
            else
            {
                short a = (short)(M.U16(A + 0x3ea) - 5);
                M.I16(A + 0x3ea, a);
                speed = f_400 * 1.04f;
                f_400 = speed;
                if (a < 0)
                {
                    M.I16(A + 0x3ea, 0);
                    M.U8(A + 0x3e8, 0);
                }
            }
            short dir = M.I16(A + 0x3f8);
            M.F32(A + 0x46c, speed * CosAt(dir) + M.F32(A + 0x46c));
            M.F32(A + 0x474, speed * SinAt(dir) + M.F32(A + 0x474));
            if (age % 3 == 0) TrailShift_P1();
            return FxCaseResult.Continue;
        }

        /// <summary>
        /// Inlined trail shift of types 0x77 / 0x7a / 0x7d: blocks 3←2←1←0: position (+0x84..+0x8c), alpha * 0.8,
        /// spin (+0x7e).
        /// </summary>
        void TrailShift_P1()
        {
            for (int d = 3; d >= 1; d--)
            {
                uint dst = Blk(d), src = Blk(d - 1);
                M.U32(dst + 0x84, M.U32(src + 0x84));
                M.U32(dst + 0x88, M.U32(src + 0x88));
                M.U32(dst + 0x8c, M.U32(src + 0x8c));
                M.I16(dst + 2, (short)FxOps.Cvtt((float)M.I16(src + 2) * 0.8f));
                M.U16(dst + 0x7e, M.U16(src + 0x7e));
            }
        }

        // ---------------------------------------------------------------- function cases

        /// <summary>Type 0x2a: CEffectPrim::fn_ae9fb0.</summary>
        [FxPrimProcess(0x2a)]
        public FxCaseResult Case2A(FxRuntime rt) { Process_ae9fb0(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x2d: CEffectPrim::fn_aeb660.</summary>
        [FxPrimProcess(0x2d)]
        public FxCaseResult Case2D(FxRuntime rt) { Process_aeb660(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x2f: CEffectPrim::fn_ae1e10.</summary>
        [FxPrimProcess(0x2f)]
        public FxCaseResult Case2F(FxRuntime rt) { Process_ae1e10(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x35: CEffectPrim::fn_aeb980.</summary>
        [FxPrimProcess(0x35)]
        public FxCaseResult Case35(FxRuntime rt) { Process_aeb980(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x3e: CEffectPrim::fn_aec690 (its return value is ignored).</summary>
        [FxPrimProcess(0x3e)]
        public FxCaseResult Case3E(FxRuntime rt) { Process_aec690(); return FxCaseResult.Continue; }

        /// <summary>Type 0x57: CEffectPrim::fn_ae82d0.</summary>
        [FxPrimProcess(0x57)]
        public FxCaseResult Case57(FxRuntime rt) { Process_ae82d0(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x66: CEffectPrim::fn_ae4270.</summary>
        [FxPrimProcess(0x66)]
        public FxCaseResult Case66(FxRuntime rt) { Process_ae4270(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x7a: CEffectPrim::fn_ae8da0.</summary>
        [FxPrimProcess(0x7a)]
        public FxCaseResult Case7A(FxRuntime rt) { Process_ae8da0(rt); return FxCaseResult.Continue; }

        /// <summary>Type 0x7d: CEffectPrim::fn_ae91d0.</summary>
        [FxPrimProcess(0x7d)]
        public FxCaseResult Case7D(FxRuntime rt) { Process_ae91d0(rt); return FxCaseResult.Continue; }

        /// <summary>
        /// Trail copy used by several process functions: block <paramref name="d"/> ← block <paramref name="s"/>
        /// (spin +6, size +0x80 (+0.5 when <paramref name="half"/>), angle +0x34, alpha +2 minus
        /// <paramref name="alphaSub"/> clamped at 0, position +0x84..+0x8c, texture index +0x68).
        /// </summary>
        void TrailCopy_P1(int d, int s, int alphaSub, bool half)
        {
            uint dst = Blk(d), src = Blk(s);
            M.U16(dst + 6, M.U16(src + 6));
            if (half) M.F32(dst + 0x80, M.F32(src + 0x80) + 0.5f);
            else M.U32(dst + 0x80, M.U32(src + 0x80));
            M.U32(dst + 0x34, M.U32(src + 0x34));
            short a = (short)(M.U16(src + 2) - alphaSub);
            M.I16(dst + 2, a);
            if (a < 0) M.I16(dst + 2, 0);
            M.U32(dst + 0x84, M.U32(src + 0x84));
            M.U32(dst + 0x88, M.U32(src + 0x88));
            M.U32(dst + 0x8c, M.U32(src + 0x8c));
            M.U16(dst + 6, M.U16(src + 6));
            M.U8(dst + 0x68, M.U8(src + 0x68));
        }

        /// <summary>CEffectPrim::fn_ae1e10 (type 0x2f): orbiting, bobbing sprite with a 3-deep trail every 6 ticks.</summary>
        public void Process_ae1e10(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            if (M.U8(A + 0x3e8) != 1) return;
            M.I32(A + 0x3f4, M.I32(A + 0x3f4) + 1);
            if (M.I32(A + 0x3f4) % 6 == 0)
            {
                if ((sbyte)M.U8(A + 0x604) > 0)
                {
                    TrailCopy_P1(3, 2, 0x32, true);
                    M.U8(A + 0x66e, 2);
                    M.I32(A + 0x634, 0);
                }
                if ((sbyte)M.U8(A + 0x550) > 0)
                {
                    TrailCopy_P1(2, 1, 0x32, true);
                    M.U8(A + 0x5ba, 2);
                    M.I32(A + 0x580, 0);
                }
                if ((sbyte)M.U8(A + 0x49c) > 0)
                {
                    TrailCopy_P1(1, 0, 0x32, true);
                    M.U8(A + 0x506, 2);
                    M.I32(A + 0x4cc, 0);
                }
            }
            float f = f_41c + 5f;
            f_41c = f;
            if (f >= 360f) f_41c = f - 360f;
            short s = (short)(M.U16(A + 0x3f8) + 1);
            M.I16(A + 0x3f8, s);
            if (s >= 0x168)
            {
                s = (short)(s - 0x168);
                M.I16(A + 0x3f8, s);
            }
            M.U32(A + 0x46c, M.U32(A + 0x10));
            M.U32(A + 0x474, M.U32(A + 0x18));
            float y = m_pos_y - 6f;
            f_488 = y;
            f_470 = SinAt(s) * 3f + y;
            if (M.U8(A + 0x452) == 7)
            {
                short t = (short)(M.U16(A + 0x3ee) + 10);
                M.I16(A + 0x3ee, t);
                if (t >= 0x168) M.I16(A + 0x3ee, (short)(t - 0x168));
            }
        }

        /// <summary>CEffectPrim::fn_aeb660 (type 0x2d): four sparks thrown from the master toward (f_388, f_390) ±30°.</summary>
        public void Process_aeb660(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int old = M.I32(b + 0xc);
                M.I32(b + 0xc, old + 1);
                if (old + 1 <= 0) continue;
                short s = (short)(M.U16(b + 6) - 5);
                M.I16(b + 6, s);
                if (s < 0) M.I16(b + 6, (short)(s + 0x168));
                if (old == 0)
                {
                    M.I16(b + 0x10, Direction_ad5710(f_388 - m_pos_x, f_390 - m_pos_z));
                    int r = rt.Rand.Next() % 0x3c;
                    short d = (short)((r >= 0 ? r : 0) - 0x1e + M.I16(b + 0x10));
                    M.I16(b + 0x10, d);
                    if (d >= 0x168)
                    {
                        d = (short)(d - 0x168);
                        M.I16(b + 0x10, d);
                    }
                    if (d < 0) M.I16(b + 0x10, (short)(d + 0x168));
                }
                else if (old < 10)
                {
                    M.U16(b + 2, (ushort)(M.U16(b + 2) + 0xf));
                }
                else
                {
                    short a = (short)(M.U16(b + 2) - 3);
                    M.I16(b + 2, a);
                    if (a < 0)
                    {
                        M.I16(b + 2, 0);
                        M.U8(b, 0);
                    }
                }
                M.F32(b + 0x88, M.F32(b + 0x1c) + M.F32(b + 0x88));
                short dir = M.I16(b + 0x10);
                float speed = M.F32(b + 0x18);
                M.F32(b + 0x84, speed * CosAt(dir) + M.F32(b + 0x84));
                M.F32(b + 0x8c, speed * SinAt(dir) + M.F32(b + 0x8c));
            }
        }

        /// <summary>CEffectPrim::fn_aeb980 (type 0x35): arcing projectile toward (f_388, f_390) with a trail on odd ticks.</summary>
        public void Process_aeb980(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            if (M.U8(A + 0x3e8) != 1) return;
            int age = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, age);
            if (age % 2 == 1)
            {
                if (age > 3) TrailCopy_P1(3, 2, 0x19, false);
                if (age > 2) { TrailCopy_P1(2, 1, 0x19, false); age = M.I32(A + 0x3f4); }
                if (age > 1) { TrailCopy_P1(1, 0, 0x96, false); age = M.I32(A + 0x3f4); }
            }
            M.U16(A + 0x3ee, (ushort)(M.U16(A + 0x3ee) + (M.U8(A + 0x452) == 0xf ? 0x2d : 10)));
            short s = M.I16(A + 0x3ee);
            if (s >= 0x168) M.I16(A + 0x3ee, (short)(s - 0x168));
            if (age == 1)
            {
                M.I16(A + 0x3f8, Direction_ad5710(f_388 - f_46c, f_390 - f_474));
                age = M.I32(A + 0x3f4);
            }
            float ratio = (float)age / (float)(sbyte)M.U8(A + 0x451);
            if (ratio > 1.05f)
            {
                short a = (short)(M.U16(A + 0x3ea) - 0x14);
                M.I16(A + 0x3ea, a);
                if (a < 0) M.I16(A + 0x3ea, 0);
                return;
            }
            float m = 1f < ratio ? 1f : ratio;      // MINSS 1.0, ratio (NaN → ratio)
            float y = f_404 + f_488;
            f_488 = y;
            int idx = FxOps.Cvtt(m * -180f);
            float lift = SinAt(-idx);
            byte kind = M.U8(A + 0x452);
            if (kind == 0) lift *= 10f;
            else if (kind != 0xf) lift *= 30f;
            f_470 = y - lift;
            short dir = M.I16(A + 0x3f8);
            float speed = f_400;
            f_46c = speed * CosAt(dir) + f_46c;
            f_474 = speed * SinAt(dir) + f_474;
        }

        /// <summary>CEffectPrim::fn_aec690 (type 0x3e): per block spin +5, width grows to 180/120, alpha by kind (B+4). Returns true.</summary>
        public bool Process_aec690()
        {
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                if (age <= 0) continue;
                short s = (short)(M.U16(b + 0x10) + 5);
                M.I16(b + 0x10, s);
                if (s >= 0x168) M.I16(b + 0x10, (short)(s - 0x168));
                short kind = M.I16(b + 4);
                short w = (short)(M.U16(b + 6) + 3);
                M.I16(b + 6, w);
                bool grow = false;
                if (kind == 2)
                {
                    if (w > 0xb4) M.I16(b + 6, 0xb4);
                    if (age > 0x32)
                    {
                        short a = (short)(M.U16(b + 2) - 1);
                        M.I16(b + 2, a);
                        if (a < 0) M.I16(b + 2, 0);
                    }
                    grow = age > 0xc;
                }
                else
                {
                    if (w > 0x78) M.I16(b + 6, 0x78);
                    if (kind == 1 && age > 0x14)
                    {
                        short a = (short)(M.U16(b + 2) - 2);
                        M.I16(b + 2, a);
                        if (a < 0) M.I16(b + 2, 0);
                        grow = true;
                    }
                }
                if (grow) M.F32(b + 0x80, M.F32(b + 0x80) + 0.1f);
                if (age > 0x578)
                {
                    if (kind != 3) M.U16(b + 2, (ushort)(M.U16(b + 2) - 3));
                    if (M.I16(b + 2) < 0) M.I16(b + 2, 0);
                }
                if (kind == 2)
                {
                    if (age < 0xc) M.U16(b + 2, (ushort)(M.U16(b + 2) + 1));
                }
                else if (age < 0xc)
                {
                    short a = (short)(M.U16(b + 2) + 10);
                    M.I16(b + 2, a);
                    if (a > 0xfa) M.I16(b + 2, 0xfa);
                }
            }
            return true;
        }

        /// <summary>CEffectPrim::fn_ae82d0 (type 0x57): falling / rising sprite with trail; sends mode message 0x7a on landing.</summary>
        public void Process_ae82d0(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            if (M.U8(A + 0x3e8) != 1) return;
            int age = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, age);
            if (age < 1) return;
            if (age > 3) TrailCopy_P1(3, 2, 0x19, false);
            if (age > 2) { TrailCopy_P1(2, 1, 0x19, false); age = M.I32(A + 0x3f4); }
            if (age > 1) { TrailCopy_P1(1, 0, 100, false); age = M.I32(A + 0x3f4); }
            if (age <= 10) M.U16(A + 0x3ea, (ushort)(M.U16(A + 0x3ea) + 0x14));
            byte state = M.U8(A + 0x454);
            if (state == 4)
            {
                SpinDown_ae82d0();
                float y = f_400 + f_488;
                f_488 = y;
                f_474 = f_48c + m_pos_z;
                f_46c = f_484 + m_pos_x;
                f_470 = y + m_pos_y;
                if (f_488 >= -60f && (ushort)(M.U16(A + 0x3ee) - 0xfa) <= 0x1e)
                {
                    M.U8(A + 0x454, 0);
                    rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
                }
                return;
            }
            if (age <= 0x20)
            {
                if (state == 0 || age <= 0x18)
                {
                    SpinDown_ae82d0();
                    f_488 = f_400 + f_488;
                }
                f_46c = f_484 + m_pos_x;
                f_470 = f_488 + m_pos_y;
                f_474 = f_48c + m_pos_z;
                return;
            }
            if (state == 0 && age == 0x21) rt.GameModeSendMsg(0x7a, 0, 0, 0, 0);
            short a = (short)(M.U16(A + 0x3ea) - 5);
            M.I16(A + 0x3ea, a);
            if (a < 0) M.I16(A + 0x3ea, 0);
        }

        void SpinDown_ae82d0()
        {
            short s = (short)(M.U16(A + 0x3ee) - 0xf);
            M.I16(A + 0x3ee, s);
            if (s < 0) M.I16(A + 0x3ee, (short)(s + 0x168));
        }

        /// <summary>CEffectPrim::fn_ae4270 (type 0x66): per block grow, alpha flicker, 21-step angle ring shift on the first tick, +8° per segment afterwards.</summary>
        public void Process_ae4270(FxRuntime rt)
        {
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                if (age <= 0) continue;
                M.F32(b + 0x80, M.F32(b + 0x80) + 0.6f);
                if (age <= 10)
                {
                    int r = rt.Rand.Next() % 6;
                    M.U16(b + 2, (ushort)(M.U16(b + 2) + (ushort)((r >= 0 ? r : 0) + 0xf)));
                    age = M.I32(b + 0xc);
                }
                ushort alpha = M.U16(b + 2);
                if (age > 0x14)
                {
                    alpha = (ushort)(alpha - 5);
                    M.U16(b + 2, alpha);
                }
                if ((short)alpha < 0) M.I16(b + 2, 0);
                if (age == 1)
                {
                    for (int it = 0; it < 21; it++)
                    {
                        float f = M.F32(b + 0x14) + 18f;
                        M.F32(b + 0x14, f);
                        if (f >= 360f)
                        {
                            M.F32(b + 0x14, f - 360f);
                            continue;
                        }
                        int n = FxOps.Cvtt(M.F32(b + 0x94));
                        // memmove(&seg[1], &seg[0], n * 4) done backwards: seg[k] = seg[k-1], k = n..1
                        for (int k = n; k >= 1; k--)
                            M.U32(b + 0x14 + (uint)k * 4, M.U32(b + 0x10 + (uint)k * 4));
                    }
                }
                else if (M.F32(b + 0x94) >= 0f)
                {
                    int k = 0;
                    do
                    {
                        uint p = b + 0x14 + (uint)k * 4;
                        float v = M.F32(p) + 8f;
                        M.F32(p, v);
                        if (v >= 360f)
                        {
                            v -= 360f;
                            M.F32(p, v);
                        }
                        if (0f > v) M.F32(p, v + 360f);
                        k++;
                    } while (M.F32(b + 0x94) >= (float)k);
                }
            }
        }

        /// <summary>CEffectPrim::fn_ae9fb0 (type 0x2a): projectile toward (f_388, f_390) that may veer once (kind f_452 == 1) and homes back to the master.</summary>
        public void Process_ae9fb0(FxRuntime rt)
        {
            CameraAngles_P1(rt);
            FollowMaster_P1();
            if (M.U8(A + 0x3e8) != 1) return;
            int age = M.I32(A + 0x3f4) + 1;
            M.I32(A + 0x3f4, age);
            if (age < 1) return;
            byte kind = M.U8(A + 0x452);
            if (kind == 1)
            {
                if (age <= 10) M.U16(A + 0x3ea, (ushort)(M.U16(A + 0x3ea) + 0x19));
                if (M.I16(A + 0x3ea) >= 0xff) M.I16(A + 0x3ea, 0xff);
            }
            if (age % ((kind != 1 ? 1 : 0) * 2 + 1) == 0)
            {
                if (age > 3) TrailCopy_P1(3, 2, 0x19, false);
                if (age > 2) { TrailCopy_P1(2, 1, 0x19, false); age = M.I32(A + 0x3f4); }
                if (age > 1) { TrailCopy_P1(1, 0, 0x96, false); age = M.I32(A + 0x3f4); }
            }
            short s = (short)(M.U16(A + 0x3ee) - 0xf);
            M.I16(A + 0x3ee, s);
            if (s < 0) M.I16(A + 0x3ee, (short)(s + 0x168));
            if (age == 1) M.I16(A + 0x3f8, Direction_ad5710(f_388 - f_46c, f_390 - f_474));

            byte phase = M.U8(A + 0x451);
            if (phase == 0)
            {
                float travelled = (float)M.I32(A + 0x3f4) * f_400;
                if (travelled > f_3f0)
                {
                    if (M.U8(A + 0x452) == 1)
                    {
                        M.U8(A + 0x451, 2);
                        f_400 = f_400 * 3f;
                        int coin = rt.Rand.Next() & 1;
                        if (coin == 0)
                        {
                            int r = rt.Rand.Next() % 0x2d;
                            short d = (short)((r >= 0 ? r : 0) + M.I16(A + 0x3f8));
                            M.I16(A + 0x3f8, d);
                            if (d >= 0x168) M.I16(A + 0x3f8, (short)(d - 0x168));
                        }
                        else
                        {
                            int r = rt.Rand.Next() % 0x2d;
                            short d = (short)(M.I16(A + 0x3f8) - (r >= 0 ? r : 0));
                            M.I16(A + 0x3f8, d);
                            if (d < 0) M.I16(A + 0x3f8, (short)(d + 0x168));
                        }
                    }
                    else
                    {
                        M.U8(A + 0x451, 1);
                    }
                }
                f_470 = f_404 + f_470;
            }
            else if (phase == 1)
            {
                short want = Direction_ad5710(m_pos_x - f_46c, m_pos_z - f_474);
                short d = M.I16(A + 0x3f8);
                if (d < want - 4 || d > want + 4)
                {
                    short n = (short)(d + 5);
                    M.I16(A + 0x3f8, n);
                    if (n >= 0x168) M.I16(A + 0x3f8, (short)(n - 0x168));
                }
                else
                {
                    M.U8(A + 0x451, 2);
                }
            }
            else
            {
                short a = (short)(M.U16(A + 0x3ea) - 0xc);
                M.I16(A + 0x3ea, a);
                if (a < 0) M.I16(A + 0x3ea, 0);
                f_470 = f_470 - f_404;
            }
            short dir = M.I16(A + 0x3f8);
            float speed = f_400;
            f_46c = speed * CosAt(dir) + f_46c;
            f_474 = speed * SinAt(dir) + f_474;
        }

        /// <summary>
        /// The actor a homing prim tracks: the local player when f_38c == (float)PlayerAid, otherwise
        /// GetGameActorByAID((uint)f_38c).
        /// </summary>
        uint HomingTarget_P1(FxRuntime rt)
        {
            float target = (float)(double)rt.PlayerAid;
            if (f_38c == target) return M.U32(M.U32(rt.Mode + 0xcc) + 0x2c);
            return rt.FindActorByAid(FxOps.FloatToUInt_d48d60(f_38c));
        }

        /// <summary>Fades all four blocks by 5 (deactivating at 0) — the "dissolve" step of 0x7a / 0x7d.</summary>
        void FadeAllBlocks_P1()
        {
            for (int i = 0; i < 4; i++)
            {
                uint b = Blk(i);
                short a = (short)(M.U16(b + 2) - 5);
                M.I16(b + 2, a);
                if (a < 0)
                {
                    M.I16(b + 2, 0);
                    M.U8(b, 0);
                }
            }
        }

        /// <summary>Common tail of 0x7a / 0x7d: move block 0 along its heading, trail shift every 3 ticks.</summary>
        void HomingMove_P1()
        {
            uint b = Blk(0);
            short dir = M.I16(b + 0x10);
            float speed = M.F32(b + 0x18);
            M.F32(b + 0x84, speed * CosAt(dir) + M.F32(b + 0x84));
            M.F32(b + 0x8c, speed * SinAt(dir) + M.F32(b + 0x8c));
            if (M.I32(b + 0xc) % 3 == 0) TrailShift_P1();
        }

        /// <summary>CEffectPrim::fn_ae8da0 (type 0x7a): circling homing missile (state B+0x69: 0 seek, 10 dissolve, 20 return to master).</summary>
        public void Process_ae8da0(FxRuntime rt)
        {
            uint b = Blk(0);
            if (M.U8(b) != 1) return;
            M.I32(b + 0xc, M.I32(b + 0xc) + 1);
            if (M.I32(b + 0xc) <= 0) return;
            uint actor = HomingTarget_P1(rt);
            if (actor == 0 && (sbyte)M.U8(b + 0x69) < 10)
            {
                M.U8(b + 0x69, 0x14);
                if (M.I32(b + 0xc) == 1) return;
            }
            short spin = (short)(M.U16(b + 0x7e) + 0x1e);
            M.I16(b + 0x7e, spin);
            if (spin >= 0x168) M.I16(b + 0x7e, (short)(spin - 0x168));
            bool ccw = M.U8(b + 0x6a) != 0;
            int age = M.I32(b + 0xc);
            if (age == 1)
            {
                M.I16(b + 0x10, Direction_ad5710(M.F32(actor + 0x10) - m_pos_x, M.F32(actor + 0x18) - m_pos_z));
                ushort d = M.U16(b + 0x10);
                if (!ccw)
                {
                    short n = (short)(d + 0x5a);
                    M.I16(b + 0x10, n);
                    if (n >= 0x168) M.I16(b + 0x10, (short)(n - 0x168));
                }
                else
                {
                    short n = (short)(d - 0x5a);
                    M.I16(b + 0x10, n);
                    if (n < 0) M.I16(b + 0x10, (short)(n + 0x168));
                }
            }
            else if (age <= 0x14)
            {
                M.U16(b + 2, (ushort)(M.U16(b + 2) + 0xf));
                if (M.I16(b + 2) > 0xfa) M.I16(b + 2, 0xfa);
            }
            else if (age > 0x8c || (age > 0x5a && M.U8(b + 0x6c) == 1))
            {
                M.U8(b + 0x69, 10);
            }
            if (M.I32(b + 0xc) > 5)
            {
                byte state = M.U8(b + 0x69);
                if (state == 0)
                {
                    short want = Direction_ad5710(M.F32(actor + 0x10) - M.F32(b + 0x84), M.F32(actor + 0x18) - M.F32(b + 0x8c));
                    SteerToward_ae8da0(b, want, ccw);
                }
                else if (state == 10)
                {
                    FadeAllBlocks_P1();
                    ushort d = M.U16(b + 0x10);
                    if (!ccw)
                    {
                        short n = (short)(d - 1);
                        M.I16(b + 0x10, n);
                        if (n < 0) M.I16(b + 0x10, (short)(n + 0x168));
                    }
                    else
                    {
                        short n = (short)(d + 1);
                        M.I16(b + 0x10, n);
                        if (n >= 0x168) M.I16(b + 0x10, (short)(n - 0x168));
                    }
                }
                else if (state == 0x14)
                {
                    short want = Direction_ad5710(m_pos_x - M.F32(b + 0x84), m_pos_z - M.F32(b + 0x8c));
                    SteerToward_ae8da0(b, want, ccw);
                    if (M.I32(b + 0xc) > 0x32) M.U8(b + 0x69, 10);
                }
            }
            HomingMove_P1();
        }

        /// <summary>Turns the heading 5° (clockwise when <paramref name="ccw"/> is false... as the client does) unless within ±3° of <paramref name="want"/>.</summary>
        void SteerToward_ae8da0(uint b, short want, bool ccw)
        {
            ushort d = M.U16(b + 0x10);
            int di = (short)d;
            if (di >= want - 3 && di <= want + 3) return;
            if (!ccw)
            {
                short n = (short)(d - 5);
                M.I16(b + 0x10, n);
                if (n < 0) M.I16(b + 0x10, (short)(n + 0x168));
            }
            else
            {
                short n = (short)(d + 5);
                M.I16(b + 0x10, n);
                if (n >= 0x168) M.I16(b + 0x10, (short)(n - 0x168));
            }
        }

        /// <summary>CEffectPrim::fn_ae91d0 (type 0x7d): homing missile launched ±(25..45)° off the target direction.</summary>
        public void Process_ae91d0(FxRuntime rt)
        {
            uint b = Blk(0);
            if (M.U8(b) != 1) return;
            M.I32(b + 0xc, M.I32(b + 0xc) + 1);
            if (M.I32(b + 0xc) <= 0) return;
            uint actor = HomingTarget_P1(rt);
            if (actor == 0)
            {
                M.U8(b + 0x69, 10);
                if (M.I32(b + 0xc) == 1) return;
            }
            short spin = (short)(M.U16(b + 0x7e) + 0x1e);
            M.I16(b + 0x7e, spin);
            if (spin >= 0x168) M.I16(b + 0x7e, (short)(spin - 0x168));
            bool ccw = M.U8(b + 0x6a) != 0;
            int age = M.I32(b + 0xc);
            if (age == 1)
            {
                M.I16(b + 0x10, Direction_ad5710(M.F32(actor + 0x10) - m_pos_x, M.F32(actor + 0x18) - m_pos_z));
                if (!ccw)
                {
                    int r = rt.Rand.Next() % 0x15;
                    short n = (short)((r >= 0 ? r : 0) + 0x19 + M.I16(b + 0x10));
                    M.I16(b + 0x10, n);
                    if (n >= 0x168) M.I16(b + 0x10, (short)(n - 0x168));
                }
                else
                {
                    int r = rt.Rand.Next() % 0x15;
                    short rr = (short)(r >= 0 ? r : 0);
                    short d = M.I16(b + 0x10);
                    short n = (short)(d - rr - 0x19);
                    M.I16(b + 0x10, n);
                    if (n < 0) M.I16(b + 0x10, (short)(d - rr + 0x14f));
                }
            }
            else if (age <= 0x14)
            {
                M.U16(b + 2, (ushort)(M.U16(b + 2) + 8));
                if (M.I16(b + 2) > 0x78) M.I16(b + 2, 0x78);
            }
            else
            {
                M.U8(b + 0x69, 10);
            }
            if (M.I32(b + 0xc) > 5)
            {
                byte state = M.U8(b + 0x69);
                if (state == 0)
                {
                    short want = Direction_ad5710(M.F32(actor + 0x10) - M.F32(b + 0x84), M.F32(actor + 0x18) - M.F32(b + 0x8c));
                    ushort d = M.U16(b + 0x10);
                    int di = (short)d;
                    if (di < want - 3 || di > want + 3)
                    {
                        if (!ccw) SetHeadingDown_ae91d0(b, (short)(d - 5));
                        else SetHeadingUp_ae91d0(b, (short)(d + 5));
                    }
                    else
                    {
                        M.U8(b + 0x69, 10);
                    }
                }
                else if (state == 10)
                {
                    FadeAllBlocks_P1();
                    ushort d = M.U16(b + 0x10);
                    if (ccw) SetHeadingUp_ae91d0(b, (short)(d + 1));
                    else SetHeadingDown_ae91d0(b, (short)(d - 1));
                }
            }
            HomingMove_P1();
        }

        void SetHeadingDown_ae91d0(uint b, short n)
        {
            M.I16(b + 0x10, n);
            if (n < 0) M.I16(b + 0x10, (short)(n + 0x168));
        }

        void SetHeadingUp_ae91d0(uint b, short n)
        {
            M.I16(b + 0x10, n);
            if (n >= 0x168) M.I16(b + 0x10, (short)(n - 0x168));
        }
    }
}
