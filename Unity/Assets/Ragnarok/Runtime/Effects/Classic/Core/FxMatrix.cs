// Synced from reference/port/src/RoClassicFx/FxMatrix.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// 4x3 row-major matrices (rows 0-2 rotation, row 3 translation) as used by the client
    /// (identity constant @0x1137438). Every product keeps the multiply-by-identity-constant
    /// terms of the original code so signed zeros and NaN propagation match.
    /// </summary>
    public static partial class FxMatrix
    {
        static readonly float[] I = { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };

        public static void Identity(FxMemory m, uint a)
        {
            for (uint i = 0; i < 12; i++) m.F32(a + i * 4, I[i]);
        }

        static float Sin(float rad) => (float)Math.Sin(rad); // x87 fsin on a float argument
        static float Cos(float rad) => (float)Math.Cos(rad);

        /// <summary>FUN_00533370 — identity, then rotation about X.</summary>
        public static void MakeRotationX(FxMemory m, uint a, float rad)
        {
            Identity(m, a);
            float s = Sin(rad), c = Cos(rad);
            m.F32(a + 0x10, c);
            m.F32(a + 0x20, c);
            m.F32(a + 0x14, s);
            m.F32(a + 0x1c, -s);
        }

        /// <summary>FUN_005333e0 — identity, then rotation about Y.</summary>
        public static void MakeRotationY(FxMemory m, uint a, float rad)
        {
            Identity(m, a);
            float s = Sin(rad), c = Cos(rad);
            m.F32(a + 0x00, c);
            m.F32(a + 0x20, c);
            m.F32(a + 0x08, -s);
            m.F32(a + 0x18, s);
        }

        /// <summary>FUN_00533450 — identity, then rotation about Z.</summary>
        public static void MakeRotationZ(FxMemory m, uint a, float rad)
        {
            Identity(m, a);
            float s = Sin(rad), c = Cos(rad);
            m.F32(a + 0x00, c);
            m.F32(a + 0x10, c);
            m.F32(a + 0x0c, -s);
            m.F32(a + 0x04, s);
        }

        /// <summary>FUN_00530f80 — M = M × RotX(rad); element 0 is left untouched exactly like the client.</summary>
        public static void RotateX(FxMemory m, uint a, float rad)
        {
            float s = Sin(rad), c = Cos(rad), ns = -s;
            float i0 = I[0], i1 = I[1], i2 = I[2], i3 = I[3], i6 = I[6], i9 = I[9], i10 = I[10], i11 = I[11];
            float m0 = m.F32(a), m1 = m.F32(a + 4), m7 = m.F32(a + 0x1c), m6 = m.F32(a + 0x18);
            float m10 = m.F32(a + 0x28), m9 = m.F32(a + 0x24), m2 = m.F32(a + 8), m4 = m.F32(a + 0x10);
            float m3 = m.F32(a + 0xc), m5 = m.F32(a + 0x14);
            m.F32(a + 0x04, m0 * i1 + m1 * c + ns * m2);
            m.F32(a + 0x08, m0 * i2 + m1 * s + m2 * c);
            m.F32(a + 0x0c, m3 * i0 + m4 * i3 + m5 * i6);
            float m8 = m.F32(a + 0x20);
            m.F32(a + 0x10, m3 * i1 + m4 * c + ns * m5);
            m.F32(a + 0x14, m3 * i2 + m4 * s + m5 * c);
            m.F32(a + 0x18, m6 * i0 + m7 * i3 + m8 * i6);
            m.F32(a + 0x1c, m6 * i1 + m7 * c + ns * m8);
            m.F32(a + 0x20, m6 * i2 + m7 * s + m8 * c);
            float m11 = m.F32(a + 0x2c);
            m.F32(a + 0x24, m9 * i0 + m10 * i3 + m11 * i6 + i9);
            m.F32(a + 0x28, m9 * i1 + m10 * c + ns * m11 + i10);
            m.F32(a + 0x2c, m9 * i2 + m10 * s + m11 * c + i11);
        }

        /// <summary>FUN_00531580 — M = M × RotZ(rad).</summary>
        public static void RotateZ(FxMemory m, uint a, float rad)
        {
            float s = Sin(rad), c = Cos(rad), ns = -s;
            float i2 = I[2], i5 = I[5], i6 = I[6], i7 = I[7], i8 = I[8], i9 = I[9], i10 = I[10], i11 = I[11];
            float m7 = m.F32(a + 0x1c), m0 = m.F32(a), m1 = m.F32(a + 4), m6 = m.F32(a + 0x18), m5 = m.F32(a + 0x14);
            float m10 = m.F32(a + 0x28), m2 = m.F32(a + 8), m9 = m.F32(a + 0x24), m3 = m.F32(a + 0xc), m4 = m.F32(a + 0x10);
            m.F32(a + 0x00, ns * m1 + m0 * c + m2 * i6);
            m.F32(a + 0x04, m0 * s + m1 * c + m2 * i7);
            m.F32(a + 0x08, m0 * i2 + m1 * i5 + m2 * i8);
            m.F32(a + 0x0c, ns * m4 + m3 * c + m5 * i6);
            float m8 = m.F32(a + 0x20);
            m.F32(a + 0x14, m3 * i2 + m4 * i5 + m5 * i8);
            m.F32(a + 0x10, m3 * s + m4 * c + m5 * i7);
            m.F32(a + 0x18, ns * m7 + m6 * c + m8 * i6);
            float m11 = m.F32(a + 0x2c);
            m.F32(a + 0x1c, m6 * s + m7 * c + m8 * i7);
            m.F32(a + 0x20, m6 * i2 + m7 * i5 + m8 * i8);
            m.F32(a + 0x24, ns * m10 + m9 * c + m11 * i6 + i9);
            m.F32(a + 0x28, m9 * s + m10 * c + m11 * i7 + i10);
            m.F32(a + 0x2c, m9 * i2 + m10 * i5 + m11 * i8 + i11);
        }

        /// <summary>FUN_00531280 — M = M × RotY(rad) (rotation part and translation row).</summary>
        public static void RotateY(FxMemory m, uint a, float rad)
        {
            float s = Sin(rad), c = Cos(rad), ns = -s;
            float i1 = I[1], i3 = I[3], i4 = I[4], i5 = I[5], i7 = I[7], i9 = I[9], i10 = I[10], i11 = I[11];
            float m0 = m.F32(a), m1 = m.F32(a + 4), m7 = m.F32(a + 0x1c), m6 = m.F32(a + 0x18);
            float m10 = m.F32(a + 0x28), m9 = m.F32(a + 0x24), m2 = m.F32(a + 8), m4 = m.F32(a + 0x10);
            float m3 = m.F32(a + 0xc), m5 = m.F32(a + 0x14);
            m.F32(a + 0x00, m0 * c + m1 * i3 + m2 * s);
            m.F32(a + 0x04, m0 * i1 + m1 * i4 + m2 * i7);
            m.F32(a + 0x08, ns * m0 + m1 * i5 + m2 * c);
            m.F32(a + 0x0c, m3 * c + m4 * i3 + m5 * s);
            m.F32(a + 0x10, m3 * i1 + m4 * i4 + m5 * i7);
            float m8 = m.F32(a + 0x20);
            m.F32(a + 0x14, ns * m3 + m4 * i5 + m5 * c);
            m.F32(a + 0x18, m6 * c + m7 * i3 + m8 * s);
            m.F32(a + 0x1c, m6 * i1 + m7 * i4 + m8 * i7);
            m.F32(a + 0x20, ns * m6 + m7 * i5 + m8 * c);
            float m11 = m.F32(a + 0x2c);
            m.F32(a + 0x24, m9 * c + m10 * i3 + m11 * s + i9);
            m.F32(a + 0x28, m9 * i1 + m10 * i4 + m11 * i7 + i10);
            m.F32(a + 0x2c, ns * m9 + m10 * i5 + m11 * c + i11);
        }
    }
}
