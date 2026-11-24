// Synced from reference/port/src/RoClassicFx/Shared/mapfx-wind/FxRuntime.WindFloorInfo.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The Lua C functions <c>SetWindEffectInfo</c> (FUN_00aa8100) and <c>SetMagicFloorInfo</c> (FUN_00aa7db0), registered
    /// by FUN_00a28770, that the EffectTool readers <c>ReadWindEffectInfoz</c> / <c>ReadMagicFloorInfoz</c> call to configure
    /// the map's EF 2343 (CWindEffect) and EF 1025 (CMagicFloorEffect) effects through CEffectMgr's per-id vectors
    /// (FUN_00a4de70). Package mapfx-wind.
    /// </summary>
    public sealed partial class FxRuntime
    {
        /// <summary>EF 2343 (CWindEffect), the id SetWindEffectInfo looks up.</summary>
        public const int WindEffectId = 0x927;

        /// <summary>EF 1025 (CMagicFloorEffect), the id SetMagicFloorInfo looks up.</summary>
        public const int MagicFloorEffectId = 0x401;

        /// <summary>
        /// FUN_00aa8100 — <c>SetWindEffectInfo(index, MSG, values…)</c>. <paramref name="stack"/>[i] is what FUN_005023a0
        /// (lua_tonumber) returns for Lua stack slot i + 1 (missing or nil = 0); <paramref name="text"/> is FUN_00502340
        /// (lua_tostring) of slot 3, read only by MSG_SET_TEXTURE. Index and MSG truncated (CVTTSD2SI); the effect is
        /// <c>FUN_00a4de70(2343, index)</c> — none: nothing happens (returns false). MSG above 0x35 (unsigned) or without a
        /// case: nothing. Messages:
        /// 0 INIT → vf07 (vf05 + vf01: the streaks rebuilt, rand());
        /// 4 POS → three floats (FSTP dword) at +8 / +0xc / +0x10 (the effect moves);
        /// 6 COLOR → (byte)(int) of slots 3..6 into +0xce (alpha maximum), +0xcf, +0xd0, +0xd1 (r, g, b);
        /// 8 SIZE → +0xd4 (radius); 0x12 RISEANGLE → +0xd8 (thickness); 0x15 HEIGHT0 → +0xdc (height); 0xb SPEED → +0xe0;
        /// 0x14 FDA → u16 +0xe4 (full angle) and 0x35 NUMBER → u16 +0xcc (streak count), both through FUN_00d49860 (the low
        /// dword of a 64-bit truncation); 0xc SRCMODE / 0xd DESTMODE → +0xf4 / +0xf8 (FUN_00d49860);
        /// 0x2e ROTATE_ANGLE → +0xe8 = slot 3, +0xec = 0, +0xf0 = slot 4 (floats);
        /// 0xa TEXTURE → +0xfc assigned from a copy of a std::string built from the C string (two temporaries).
        /// The new values take effect at the next INIT (count, colour, radius, height, speed, full angle, blends, texture)
        /// or at once (position, thickness, rotation — read every tick).
        /// </summary>
        public bool SetWindEffectInfo_aa8100(double[] stack, string text = null)
        {
            double Num(int slot) => stack != null && slot - 1 < stack.Length ? stack[slot - 1] : 0.0;
            uint e = EffectByIdIndex_a4de70(WindEffectId, (uint)FxOps.Cvtt(Num(1)));
            if (e == 0) return false;
            int msg = FxOps.Cvtt(Num(2));
            if ((uint)msg > 0x35) return true;
            switch (msg)
            {
                case 0:
                    new CEffect(M, e).Vf07Reinit(this);
                    break;
                case 4:
                {
                    float x = (float)Num(3), y = (float)Num(4), z = (float)Num(5);
                    M.F32(e + 8, x);
                    M.F32(e + 0xc, y);
                    M.F32(e + 0x10, z);
                    break;
                }
                case 6:
                {
                    double a = Num(3), r = Num(4), g = Num(5), b = Num(6);
                    M.U8(e + 0xd0, (byte)FxOps.Cvtt(g));
                    M.U8(e + 0xce, (byte)FxOps.Cvtt(a));
                    M.U8(e + 0xd1, (byte)FxOps.Cvtt(b));
                    M.U8(e + 0xcf, (byte)FxOps.Cvtt(r));
                    break;
                }
                case 8:
                    M.F32(e + 0xd4, (float)Num(3));
                    break;
                case 0xa:
                    if (text == null) throw new ArgumentNullException(nameof(text), "MSG_SET_TEXTURE without a string: the client builds a std::string from a null pointer");
                    WindSetTexture_aa833f(e, text);
                    break;
                case 0xb:
                    M.F32(e + 0xe0, (float)Num(3));
                    break;
                case 0xc:
                    M.I32(e + 0xf4, FtolLow64_d49860(Num(3)));
                    break;
                case 0xd:
                    M.I32(e + 0xf8, FtolLow64_d49860(Num(3)));
                    break;
                case 0x12:
                    M.F32(e + 0xd8, (float)Num(3));
                    break;
                case 0x14:
                    M.U16(e + 0xe4, (ushort)FtolLow64_d49860(Num(3)));
                    break;
                case 0x15:
                    M.F32(e + 0xdc, (float)Num(3));
                    break;
                case 0x2e:
                {
                    float x = (float)Num(3), z = (float)Num(4);
                    M.F32(e + 0xe8, x);
                    M.U32(e + 0xec, 0);
                    M.F32(e + 0xf0, z);
                    break;
                }
                case 0x35:
                    M.U16(e + 0xcc, (ushort)FtolLow64_d49860(Num(3)));
                    break;
            }
            return true;
        }

        /// <summary>
        /// The MSG_SET_TEXTURE case of FUN_00aa8100 (0xaa833f): std::string local from the C string (FUN_004ccc60), a copy
        /// of it (FUN_004ccbd0), +0xfc.assign(copy) (FUN_004d9270), then the copy and the local destroyed — names of 16
        /// bytes and more allocate two temporary buffers.
        /// </summary>
        void WindSetTexture_aa833f(uint e, string name)
        {
            uint local = StrLocalAssign(name);
            uint copy = StrLocalAssign(name);
            StrAssign(e + 0xfc, name);
            StrLocalDestroy(name, copy);
            StrLocalDestroy(name, local);
        }

        /// <summary>
        /// FUN_00aa7db0 — <c>SetMagicFloorInfo(index, MSG, value)</c> (arguments as <see cref="SetWindEffectInfo_aa8100"/>).
        /// The effect is <c>FUN_00a4de70(1025, index)</c>; MSG above 0x2a (unsigned) or without a case: nothing. Messages:
        /// 0 INIT → vf07; 0x10 AREA → float +0x44; 0x2a SIZE2 → float +0x48; 0x11 ANGLE → int +0x4c, 0x13 ALPHA → int +0x54,
        /// 0x14 FDA → int +0x58 (all three _ftol2, FUN_00d49720); 0x12 RISEANGLE → +0x50 and 0x15..0x29 HEIGHT0..20 →
        /// +0x5c..+0xac as (float)(int) (CVTTSD2SI, CVTDQ2PS).
        /// </summary>
        public bool SetMagicFloorInfo_aa7db0(double[] stack)
        {
            double Num(int slot) => stack != null && slot - 1 < stack.Length ? stack[slot - 1] : 0.0;
            uint e = EffectByIdIndex_a4de70(MagicFloorEffectId, (uint)FxOps.Cvtt(Num(1)));
            if (e == 0) return false;
            int msg = FxOps.Cvtt(Num(2));
            if ((uint)msg > 0x2a) return true;
            switch (msg)
            {
                case 0:
                    new CEffect(M, e).Vf07Reinit(this);
                    break;
                case 0x10:
                    M.F32(e + 0x44, (float)Num(3));
                    break;
                case 0x2a:
                    M.F32(e + 0x48, (float)Num(3));
                    break;
                case 0x11:
                    M.I32(e + 0x4c, Ftol_d49720(Num(3)));
                    break;
                case 0x12:
                    M.F32(e + 0x50, (float)FxOps.Cvtt(Num(3)));
                    break;
                case 0x13:
                    M.I32(e + 0x54, Ftol_d49720(Num(3)));
                    break;
                case 0x14:
                    M.I32(e + 0x58, Ftol_d49720(Num(3)));
                    break;
                default:
                    if (msg >= 0x15 && msg <= 0x29) M.F32(e + (uint)msg * 4 + 8, (float)FxOps.Cvtt(Num(3)));
                    break;
            }
            return true;
        }

        /// <summary>
        /// FUN_00d49860 (_ftol2 variant returning EDX:EAX, ST0 in): the low dword of the 64-bit truncation. Both of its
        /// paths (FISTTP qword when the CPU level at 0x15562a4 is 2 or more, the legacy exponent / shift code otherwise)
        /// give 0x8000000000000000 for NaN and |v| ≥ 2^63, i.e. a low dword of 0.
        /// </summary>
        public static int FtolLow64_d49860(double v)
        {
            if (double.IsNaN(v) || v >= 9223372036854775808.0 || v < -9223372036854775808.0) return 0;
            return unchecked((int)(long)v);
        }
    }

    public readonly partial struct CEffect
    {
        /// <summary>
        /// vf07 (slot +0x1c) of the package's classes (the re-init MSG_SET_INIT calls): true when this is a CWindEffect
        /// (vf07_6ab340) or a CMagicFloorEffect (vf07_a70ad0) and it ran.
        /// </summary>
        internal bool WindFloorReinit(FxRuntime rt)
        {
            switch (Vtable)
            {
                case VtCWindEffect: WindReinit_6ab340(rt); return true;
                case VtCMagicFloorEffect: MagicFloorReinit_a70ad0(rt); return true;
                default: return false;
            }
        }
    }
}
