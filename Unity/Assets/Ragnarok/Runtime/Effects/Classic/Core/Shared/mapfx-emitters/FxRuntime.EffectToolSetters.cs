// Synced from reference/port/src/RoClassicFx/Shared/mapfx-emitters/FxRuntime.EffectToolSetters.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The Lua C functions the map EffectTool scripts call for EF 1073, 1039 and 1412 (registered by FUN_00a28770; package
    /// mapfx-emitters): <c>SetAnimatedEmitterInfo</c> (FUN_00aa7040), <c>SetEvilsPawInfo</c> (FUN_00aa7c50) and
    /// <c>SetEZ2STRInfo</c> (FUN_00aa7520). Like <see cref="SetEmitterInfo_aa7640"/>: <c>stack[i]</c> is what FUN_005023a0
    /// (lua_tonumber) returns for Lua slot i + 1 (missing / nil = 0), <c>text</c> FUN_00502340 (lua_tostring) of slot 3;
    /// the index (slot 1) picks the index-th effect of the id in CEffectMgr's per-id vectors (FUN_00a4de70); no effect →
    /// nothing (false). Each returns 0 results to Lua.
    /// </summary>
    public sealed partial class FxRuntime
    {
        /// <summary>EF 1073 (CAnimatedEmitterEffect), EF 1039 (CEvilsPawEffect), EF 1412 (CEZ2STREffect).</summary>
        public const int AnimatedEmitterEffectId = 0x431, EvilsPawEffectId = 0x40f, MapStrEffectId = 0x584;

        static double StackNum(double[] stack, int slot) => stack != null && slot - 1 < stack.Length ? stack[slot - 1] : 0.0;

        /// <summary>
        /// FUN_00aa7040 — <c>SetAnimatedEmitterInfo(index, MSG, values…)</c>: SetEmitterInfo's handlers for MSG 0..14 and 43
        /// only (jump tables 0xaa74c8 / byte table 0xaa74f4): 0 INIT → vf07; 1..5 three floats (FSTP dword) → spawn dir 1 /
        /// dir 2 / gravity / position (+8) / emit radius; 6 COLOR four ints (CVTTSD2SI, &gt; 255 → 255) stored alpha first;
        /// 7 / 8 / 9 two floats → rate / size / life; 10 TEXTURE (lua_tostring → by-value std::string → FUN_00aa6fd0); 11 SPEED
        /// (float) → FUN_00a78870; 12 / 13 _ftol2 of the double → src / dest blend; 14 MAXPARTICLE (CVTTSD2SI) → FUN_00a78840
        /// (vf05 + vf01 when it changes: the frame sets grow by ten names); 43 ZENABLE (CVTTSD2SI ≠ 0). 15..42 and everything
        /// above 43 (BILLBOARD_OFF, ROTATE_ANGLE, ETERNITY, SCALE included) do nothing. The index is looked up before MSG is read.
        /// </summary>
        public bool SetAnimatedEmitterInfo_aa7040(double[] stack, string text = null)
        {
            uint e = EffectByIdIndex_a4de70(AnimatedEmitterEffectId, (uint)FxOps.Cvtt(StackNum(stack, 1)));
            if (e == 0) return false;
            var fx = new CEffect(M, e);
            int msg = FxOps.Cvtt(StackNum(stack, 2));
            if ((uint)msg > 0x2b) return true;
            switch (msg)
            {
                case 0:
                    fx.Vf07Reinit(this);
                    break;
                case 1: case 2: case 3: case 4: case 5:
                {
                    float a = (float)StackNum(stack, 3), b = (float)StackNum(stack, 4), c = (float)StackNum(stack, 5);
                    uint at = msg == 1 ? 0xccu : msg == 2 ? 0xd8u : msg == 3 ? 0xe4u : msg == 4 ? 8u : 0xf0u;
                    M.F32(e + at, a);
                    M.F32(e + at + 4, b);
                    M.F32(e + at + 8, c);
                    break;
                }
                case 6:
                {
                    int r = Math.Min(FxOps.Cvtt(StackNum(stack, 3)), 0xff);
                    int g = Math.Min(FxOps.Cvtt(StackNum(stack, 4)), 0xff);
                    int b = Math.Min(FxOps.Cvtt(StackNum(stack, 5)), 0xff);
                    int a = Math.Min(FxOps.Cvtt(StackNum(stack, 6)), 0xff);
                    M.I32(e + 0x130, a);
                    M.I32(e + 0x124, r);
                    M.I32(e + 0x128, g);
                    M.I32(e + 0x12c, b);
                    break;
                }
                case 7: case 8: case 9:
                {
                    float a = (float)StackNum(stack, 3), b = (float)StackNum(stack, 4);
                    uint at = msg == 7 ? 0x114u : msg == 8 ? 0x104u : 0x10cu;
                    M.F32(e + at, a);
                    M.F32(e + at + 4, b);
                    break;
                }
                case 10:
                    if (text == null) throw new ArgumentNullException(nameof(text), "MSG_SET_TEXTURE without a string: the client builds a std::string from a null pointer");
                    fx.EmitterSetTextureMsg(this, text);
                    break;
                case 11:
                    fx.EmitterSetSpeedMsg((float)StackNum(stack, 3));
                    break;
                case 12:
                    M.I32(e + 0x14c, Ftol_d49720(StackNum(stack, 3)));
                    break;
                case 13:
                    M.I32(e + 0x150, Ftol_d49720(StackNum(stack, 3)));
                    break;
                case 14:
                    fx.EmitterSetMaxCountMsg(this, FxOps.Cvtt(StackNum(stack, 3)));
                    break;
                case 0x2b:
                    M.U8(e + 0x15c, FxOps.Cvtt(StackNum(stack, 3)) != 0 ? (byte)1 : (byte)0);
                    break;
            }
            return true;
        }

        /// <summary>
        /// FUN_00aa7c50 — <c>SetEvilsPawInfo(index, MSG, values…)</c> (byte table 0xaa7d78, MSG ≤ 44): 0 INIT → vf07
        /// (CEvilsPawEffect::vf07_67bfb0); 8 SIZE → +0xdc and 11 SPEED → +0xd8 = _ftol2 of the value rounded to float (FSTP
        /// dword, FLD dword); 44 OFFSET_POS → +0xe4/+0xe8/+0xec = three floats. Everything else does nothing.
        /// </summary>
        public bool SetEvilsPawInfo_aa7c50(double[] stack)
        {
            int index = FxOps.Cvtt(StackNum(stack, 1));
            double msgValue = StackNum(stack, 2);
            uint e = EffectByIdIndex_a4de70(EvilsPawEffectId, (uint)index);
            if (e == 0) return false;
            int msg = FxOps.Cvtt(msgValue);
            if ((uint)msg > 0x2c) return true;
            switch (msg)
            {
                case 0:
                    new CEffect(M, e).Vf07Reinit(this);
                    break;
                case 8:
                    M.I32(e + 0xdc, Ftol_d49720((float)StackNum(stack, 3)));
                    break;
                case 0xb:
                    M.I32(e + 0xd8, Ftol_d49720((float)StackNum(stack, 3)));
                    break;
                case 0x2c:
                {
                    float x = (float)StackNum(stack, 3), y = (float)StackNum(stack, 4), z = (float)StackNum(stack, 5);
                    M.F32(e + 0xe4, x);
                    M.F32(e + 0xe8, y);
                    M.F32(e + 0xec, z);
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// FUN_00aa7520 — <c>SetEZ2STRInfo(index, MSG, value)</c> (MSG 47..50, jump table 0xaa762c): 47 STR_FILENAME →
        /// vf18(lua_tostring(3)); 48 RENDER_FLAG → vf19(FUN_00d48ea0 = double → uint); 49 SCALE_RATIO → vf20((float)v);
        /// 50 ALPHA_RATIO → vf21(v ≥ 1 → 1, else MAXSS(0, v): negatives 0, NaN kept). Others do nothing.
        /// </summary>
        public bool SetEZ2STRInfo_aa7520(double[] stack, string text = null)
        {
            int index = FxOps.Cvtt(StackNum(stack, 1));
            double msgValue = StackNum(stack, 2);
            uint e = EffectByIdIndex_a4de70(MapStrEffectId, (uint)index);
            if (e == 0) return false;
            var fx = new CEffect(M, e);
            int msg = FxOps.Cvtt(msgValue) - 0x2f;
            if ((uint)msg > 3) return true;
            switch (msg)
            {
                case 0:
                    if (text == null) throw new ArgumentNullException(nameof(text), "MSG_SET_STR_FILENAME without a string: the client takes strlen(NULL)");
                    fx.EZ2STRSetStrName_a8de00(this, text);
                    break;
                case 1:
                    fx.EZ2STRSetRenderFlag_a8dd10(DoubleToUInt_d48ea0(StackNum(stack, 3)));
                    break;
                case 2:
                    fx.EZ2STRSetScaleRatio_a8dd90((float)StackNum(stack, 3));
                    break;
                case 3:
                {
                    float v = (float)StackNum(stack, 3);
                    float a;
                    if (v >= 1.0f) a = 1.0f;                                // COMISS v, 1.0; JAE (NaN: not taken)
                    else a = 0f > v ? 0f : v;                               // MAXSS 0, v: the source unless 0 > v
                    fx.EZ2STRSetAlphaRatio_a8dbe0(a);
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// FUN_00d48ea0 (__dtoui3, the legacy path taken when __isa_available &lt; 6): double → uint32. Truncates; values
        /// in [2^31, 2^32) convert exactly; negatives above −1 give 0, x &lt;= −1, x &gt;= 2^32, ±inf and NaN 0xffffffff.
        /// </summary>
        public static uint DoubleToUInt_d48ea0(double v)
        {
            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(v);
            uint lo = (uint)bits, hi = (uint)(bits >> 32);
            bool negative = (hi & 0x80000000u) != 0;
            hi &= 0x7fffffffu;
            if (negative) return hi < 0x3ff00000u ? 0u : 0xffffffffu;
            if (hi < 0x41e00000u) return (uint)FxOps.Cvtt(v);
            if (hi < 0x41f00000u) return (lo >> 21) | (hi << 11) | 0x80000000u;
            return 0xffffffffu;
        }
    }

    public readonly partial struct CEffect
    {
        /// <summary>vf07 of CEvilsPawEffect (vf07_67bfb0); the emitter classes' vf07 is in FxRuntime.Emitter.cs.</summary>
        internal void EvilsPawReinitMsg() => EvilsPawReinit_67bfb0();
    }
}
