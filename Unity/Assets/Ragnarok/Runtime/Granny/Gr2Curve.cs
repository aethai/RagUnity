// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/GrannyAnimation.js (curve codecs + evaluateCurve; itself a port of Rasetsuu/blendergranny, MIT).
// See LICENSE-granny-ro-js.txt.

using System;

namespace Ragnarok.Granny {
    /// <summary>
    /// One decoded B-spline curve. Supported codecs: LegacyCurve32f (all of RO), D3K16uC16u, D3I1K16uC16u,
    /// D4nK8uC7u, D4nK16uC15u, *Constant32f and DaIdentity; anything else decodes empty and samples as identity.
    /// </summary>
    public sealed class Gr2Curve {
        public string Codec;
        /// <summary>Granny format byte (-1 for legacy curves).</summary>
        public int Format;
        /// <summary>0 = step, 1 = linear, 2 = quadratic, 3+ = nearest control.</summary>
        public int Degree;
        /// <summary>Values per control: 3 position / uniform scale, 4 quaternion, 9 scale-shear.</summary>
        public int Dimension;
        public int KnotControlCount;
        /// <summary>Fixed value of constant / identity curves (length Dimension), else empty.</summary>
        public float[] SampleValue;
        public float[] Knots;
        /// <summary>Flattened controls: Controls[knot * Dimension + d].</summary>
        public float[] Controls;

        /// <summary>
        /// Samples the curve at <paramref name="t"/> (seconds) into <paramref name="output"/> (at least Dimension
        /// long) and returns the number of values written: 0 when the curve has nothing to sample (callers then use
        /// identity). Quaternion blends (dimension 4, degree 1..3) get granny2.dll's fast renormalisation
        /// q *= (3 - |q|^2) / 2. Times outside the knots clamp. Allocation-free.
        /// </summary>
        public int Evaluate(double t, Span<double> output) {
            int dimension = Dimension;
            if (Knots.Length == 0 || Controls.Length == 0) {
                if (SampleValue.Length > output.Length) {
                    return 0;
                }

                for (int i = 0; i < SampleValue.Length; i++) {
                    output[i] = SampleValue[i];
                }

                return SampleValue.Length;
            }

            if (dimension <= 0 || dimension > output.Length || Controls.Length < dimension) {
                return 0;
            }

            float[] controls = Controls;
            int controlKnots = controls.Length / dimension;
            int degree = Degree > 0 ? Degree : 0;
            int knotCount = Knots.Length;
            int knotIndex = FindKnot(t);
            if (degree == 0 || knotCount == 1) {
                CopyControl(ControlBase(knotIndex, controlKnots), output);
                return dimension;
            }

            int clamped = knotIndex < knotCount ? knotIndex : knotCount - 1;
            int baseKnot = clamped - degree;
            if (degree == 1) {
                int k0 = ClampKnot(baseKnot);
                int k1 = ClampKnot(baseKnot + 1);
                double blend = SafeDiv(t - Knots[k0], Knots[k1] - Knots[k0]);
                double c0 = 1.0 - blend;
                int b0 = ControlBase(k0, controlKnots);
                int b1 = ControlBase(k1, controlKnots);
                for (int d = 0; d < dimension; d++) {
                    output[d] = c0 * controls[b0 + d] + blend * controls[b1 + d];
                }
            } else if (degree == 2) {
                int km2 = ClampKnot(baseKnot);
                int km1 = ClampKnot(baseKnot + 1);
                int k0 = ClampKnot(baseKnot + 2);
                int k1 = ClampKnot(baseKnot + 3);
                double ti2 = Knots[km2];
                double ti1 = Knots[km1];
                double ti = Knots[k0];
                double ti1Next = Knots[k1];
                double l0 = SafeDiv(t - ti1, ti - ti1);
                double l11 = SafeDiv(t - ti2, ti - ti2);
                double l12 = SafeDiv(t - ti1, ti1Next - ti1);
                double ci2PlusCi1 = (l11 + l0) - l0 * l11;
                double ci = l0 * l12;
                double ci1 = ci2PlusCi1 - ci;
                double ci2 = 1.0 - ci2PlusCi1;
                int bm2 = ControlBase(km2, controlKnots);
                int bm1 = ControlBase(km1, controlKnots);
                int b0 = ControlBase(k0, controlKnots);
                for (int d = 0; d < dimension; d++) {
                    output[d] = ci2 * controls[bm2 + d] + ci1 * controls[bm1 + d] + ci * controls[b0 + d];
                }
            } else {
                CopyControl(ControlBase(ClampKnot(baseKnot + degree), controlKnots), output);
            }

            if (dimension == 4) {
                double x = output[0];
                double y = output[1];
                double z = output[2];
                double w = output[3];
                double factor = (3.0 - (x * x + y * y + z * z + w * w)) * 0.5;
                output[0] = x * factor;
                output[1] = y * factor;
                output[2] = z * factor;
                output[3] = w * factor;
            }

            return dimension;
        }

        // Set by the reader when the knots are non-decreasing: FindKnot can then binary-search (same result).
        internal bool KnotsSorted;

        internal static bool IsNonDecreasing(float[] knots) {
            for (int i = 1; i < knots.Length; i++) {
                if (!(knots[i] >= knots[i - 1])) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Index of the first knot after t, else the last knot (the JS's linear scan).</summary>
        int FindKnot(double t) {
            float[] knots = Knots;
            if (KnotsSorted) {
                int lo = 0;
                int hi = knots.Length;
                while (lo < hi) {
                    int mid = (lo + hi) >> 1;
                    if (knots[mid] > t) {
                        hi = mid;
                    } else {
                        lo = mid + 1;
                    }
                }

                return lo < knots.Length ? lo : (knots.Length > 0 ? knots.Length - 1 : 0);
            }

            for (int i = 0; i < knots.Length; i++) {
                if (knots[i] > t) {
                    return i;
                }
            }

            return knots.Length > 0 ? knots.Length - 1 : 0;
        }

        int ClampKnot(int source) {
            if (source < 0) {
                return 0;
            }

            return source > Knots.Length - 1 ? Knots.Length - 1 : source;
        }

        /// <summary>Start of a knot's control, clamped to the last full control (the JS controlAt).</summary>
        int ControlBase(int knotIndex, int controlKnots) {
            return (knotIndex < controlKnots ? knotIndex : controlKnots - 1) * Dimension;
        }

        void CopyControl(int start, Span<double> output) {
            for (int d = 0; d < Dimension; d++) {
                output[d] = Controls[start + d];
            }
        }

        static double SafeDiv(double numerator, double denominator) {
            return Math.Abs(denominator) < 1e-12 ? 0.0 : numerator / denominator;
        }
    }
}
