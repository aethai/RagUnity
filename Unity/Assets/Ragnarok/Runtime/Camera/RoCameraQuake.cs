using UnityEngine;

namespace Ragnarok.Camera {
    /// <summary>
    /// The original client's screen quake (CGameMode::SendMsg 0x7a → CView quake at 0xa0a180, applied by
    /// 0xa09e60): for 650 ms (argument 0) or 200 ms (argument 1) the eye position is displaced every logic tick
    /// by an alternating-sign amplitude plus a small random jitter ((rand % 5 − 2) / 10) along the screen's
    /// horizontal axis (amplitude 1.0 RO units) and vertically (0.24). Amplitudes are the SendMsg constants;
    /// the per-tick sign flip and jitter follow the update routine.
    /// </summary>
    public static class RoCameraQuake {
        const float RoUnitsPerUnity = 5f;
        const float TickSeconds = 1f / 60f;
        const float HorizontalAmplitude = 1.0f, VerticalAmplitude = 0.24f;

        static float endTime, nextTickTime;
        static float sign = 1f;
        static Vector2 offset;   // RO units: x = screen right, y = up

        public static bool Active => Time.unscaledTime < endTime;

        /// <summary>/quake ([0x01553a9c]): msg 0x7a starts nothing while it is off (0xc1410b); set by the game.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Starts (or extends) a quake; <paramref name="strength"/> is the message's first argument.</summary>
        public static void Start(int strength) {
            if (!Enabled) return;
            float duration = strength == 1 ? 0.2f : strength >= 650 ? strength / 1000f : 0.65f;
            endTime = Mathf.Max(endTime, Time.unscaledTime + duration);
        }

        /// <summary>World-space displacement to add to the camera this frame (zero when no quake runs).</summary>
        public static Vector3 Sample(Transform camera) {
            float now = Time.unscaledTime;
            if (now >= endTime) {
                offset = Vector2.zero;
                return Vector3.zero;
            }

            if (now >= nextTickTime) {
                nextTickTime = now + TickSeconds;
                sign = -sign;
                float jitterX = (Random.Range(0, 5) - 2) / 10f;
                float jitterY = (Random.Range(0, 5) - 2) / 10f;
                offset = new Vector2(sign * HorizontalAmplitude + jitterX, -sign * VerticalAmplitude + jitterY);
            }

            Vector3 right = camera.right;
            right.y = 0f;
            right.Normalize();
            return (right * offset.x + Vector3.up * offset.y) / RoUnitsPerUnity;
        }
    }
}
