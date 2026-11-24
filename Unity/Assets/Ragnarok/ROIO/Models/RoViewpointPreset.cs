using System;

namespace ROIO.Models {
    /// <summary>
    /// Per-map camera viewpoint preset loaded from data/viewpointtable.txt.
    ///
    /// Distance values are stored in kRO world units (5 world units = 1 GAT tile).
    /// At our scene scale (1 GAT tile ≈ 1 Unity unit), normalized units = Unity units.
    /// Altitude values are polar angles in kRO convention (negative, due to inverted Y-axis).
    ///
    /// Source: ragnarokresearchlab.github.io/rendering/camera-controls/
    /// </summary>
    public readonly struct RoViewpointPreset : IEquatable<RoViewpointPreset> {
        private const float KroWorldUnitsPerNormalizedUnit = 5f;

        /// <summary>Minimum radial distance (kRO world units).</summary>
        public readonly int Range;
        /// <summary>Maximum additional radial distance on top of Range (kRO world units).</summary>
        public readonly int Scope;
        /// <summary>Initial radial distance (kRO world units).</summary>
        public readonly int RangeIn;
        /// <summary>Minimum azimuth / yaw angle (degrees).</summary>
        public readonly int RotationFrom;
        /// <summary>Maximum azimuth / yaw angle (degrees).</summary>
        public readonly int RotationTo;
        /// <summary>Initial azimuth / yaw angle (degrees).</summary>
        public readonly int RotationIn;
        /// <summary>Minimum polar angle in kRO convention (negative degrees, e.g. -50).</summary>
        public readonly int AltitudeFrom;
        /// <summary>Maximum polar angle in kRO convention (negative degrees, e.g. -65).</summary>
        public readonly int AltitudeTo;
        /// <summary>Initial polar angle in kRO convention (negative degrees, e.g. -65).</summary>
        public readonly int AltitudeIn;

        public RoViewpointPreset(
            int range, int scope, int rangeIn,
            int rotationFrom, int rotationTo, int rotationIn,
            int altitudeFrom, int altitudeTo, int altitudeIn
        ) {
            Range       = range;
            Scope       = scope;
            RangeIn     = rangeIn;
            RotationFrom = rotationFrom;
            RotationTo   = rotationTo;
            RotationIn   = rotationIn;
            AltitudeFrom = altitudeFrom;
            AltitudeTo   = altitudeTo;
            AltitudeIn   = altitudeIn;
        }

        // ── Normalized / Unity helpers ────────────────────────────────────────

        /// <summary>Minimum camera distance in normalized units (= Unity units at our scale).</summary>
        public float MinDistanceNormalized => Range / KroWorldUnitsPerNormalizedUnit;

        /// <summary>Maximum camera distance in normalized units.</summary>
        public float MaxDistanceNormalized => (Range + Scope) / KroWorldUnitsPerNormalizedUnit;

        /// <summary>Initial camera distance in normalized units.</summary>
        public float InitialDistanceNormalized => RangeIn / KroWorldUnitsPerNormalizedUnit;

        /// <summary>Minimum pitch in degrees (Unity positive-down convention).</summary>
        public float MinPitchDegrees => Math.Abs(AltitudeFrom);

        /// <summary>Maximum pitch in degrees.</summary>
        public float MaxPitchDegrees => Math.Abs(AltitudeTo);

        /// <summary>Initial pitch in degrees.</summary>
        public float InitialPitchDegrees => Math.Abs(AltitudeIn);

        // ── Equality ─────────────────────────────────────────────────────────

        public bool Equals(RoViewpointPreset other) =>
            Range == other.Range && Scope == other.Scope && RangeIn == other.RangeIn &&
            RotationFrom == other.RotationFrom && RotationTo == other.RotationTo && RotationIn == other.RotationIn &&
            AltitudeFrom == other.AltitudeFrom && AltitudeTo == other.AltitudeTo && AltitudeIn == other.AltitudeIn;

        public override bool Equals(object obj) => obj is RoViewpointPreset p && Equals(p);

        public override int GetHashCode() {
            unchecked {
                int h = Range;
                h = h * 397 ^ Scope;
                h = h * 397 ^ RangeIn;
                h = h * 397 ^ RotationFrom;
                h = h * 397 ^ RotationTo;
                h = h * 397 ^ RotationIn;
                h = h * 397 ^ AltitudeFrom;
                h = h * 397 ^ AltitudeTo;
                h = h * 397 ^ AltitudeIn;
                return h;
            }
        }

        public override string ToString() =>
            $"dist=[{MinDistanceNormalized:0.#}–{MaxDistanceNormalized:0.#}] init={InitialDistanceNormalized:0.#} " +
            $"yaw=[{RotationFrom}°–{RotationTo}°] init={RotationIn}° " +
            $"pitch=[{MinPitchDegrees:0.#}°–{MaxPitchDegrees:0.#}°] init={InitialPitchDegrees:0.#}°";
    }
}
