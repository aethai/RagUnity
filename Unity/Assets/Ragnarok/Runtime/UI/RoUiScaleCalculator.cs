using System;

namespace Ragnarok.UI {
    public static class RoUiScaleCalculator {
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;
        public const float ReferenceScale = 1f;
        public const float DefaultMinScale = 0.75f;
        public const float DefaultMaxScale = 2.25f;
        public const float ReferenceDpi = 96f;
        public const float MinReliableDpi = 80f;
        public const float MaxReliableDpi = 320f;

        private const float QuadHdResolutionScale = 4f / 3f;
        private const float QuadHdUiScale = 1.25f;
        private const float FourKResolutionScale = 2f;
        private const float FourKUiScale = 1.8f;
        private const float LowResolutionExponent = 0.85f;
        private const float BeyondFourKGrowth = 0.35f;
        private const float DpiInfluence = 0.15f;
        private const float MinDpiAdjustment = -0.05f;
        private const float MaxDpiAdjustment = 0.15f;

        public static float CalculateAutoScale(int width, int height, float dpi) {
            return CalculateAutoScale(width, height, dpi, DefaultMinScale, DefaultMaxScale);
        }

        public static float CalculateAutoScale(int width, int height, float dpi, float minScale, float maxScale) {
            NormalizeBounds(ref minScale, ref maxScale);

            if (width <= 0 || height <= 0) {
                return Clamp(ReferenceScale, minScale, maxScale);
            }

            float widthScale = width / (float) ReferenceWidth;
            float heightScale = height / (float) ReferenceHeight;
            float limitingResolutionScale = Math.Min(widthScale, heightScale);
            float resolutionScale = CalculateResolutionScale(limitingResolutionScale);
            float dpiAdjustment = CalculateDpiAdjustment(dpi);

            return Clamp(resolutionScale + dpiAdjustment, minScale, maxScale);
        }

        public static bool IsReliableDpi(float dpi) {
            return IsFinite(dpi) && dpi >= MinReliableDpi && dpi <= MaxReliableDpi;
        }

        private static float CalculateResolutionScale(float limitingResolutionScale) {
            if (!IsFinite(limitingResolutionScale) || limitingResolutionScale <= 0f) {
                return ReferenceScale;
            }

            if (limitingResolutionScale <= ReferenceScale) {
                return (float) Math.Pow(limitingResolutionScale, LowResolutionExponent);
            }

            if (limitingResolutionScale <= QuadHdResolutionScale) {
                float t = InverseLerp(ReferenceScale, QuadHdResolutionScale, limitingResolutionScale);
                return Lerp(ReferenceScale, QuadHdUiScale, t);
            }

            if (limitingResolutionScale <= FourKResolutionScale) {
                float t = InverseLerp(QuadHdResolutionScale, FourKResolutionScale, limitingResolutionScale);
                return Lerp(QuadHdUiScale, FourKUiScale, t);
            }

            return FourKUiScale + (limitingResolutionScale - FourKResolutionScale) * BeyondFourKGrowth;
        }

        private static float CalculateDpiAdjustment(float dpi) {
            if (!IsReliableDpi(dpi)) {
                return 0f;
            }

            float relativeDpi = (dpi - ReferenceDpi) / ReferenceDpi;
            return Clamp(relativeDpi * DpiInfluence, MinDpiAdjustment, MaxDpiAdjustment);
        }

        private static void NormalizeBounds(ref float minScale, ref float maxScale) {
            if (!IsFinite(minScale) || minScale <= 0f) {
                minScale = DefaultMinScale;
            }

            if (!IsFinite(maxScale) || maxScale <= 0f) {
                maxScale = DefaultMaxScale;
            }

            if (maxScale < minScale) {
                maxScale = minScale;
            }
        }

        private static float InverseLerp(float from, float to, float value) {
            return Clamp((value - from) / (to - from), 0f, 1f);
        }

        private static float Lerp(float from, float to, float t) {
            return from + (to - from) * Clamp(t, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max) {
            if (value < min) {
                return min;
            }

            if (value > max) {
                return max;
            }

            return value;
        }

        private static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
