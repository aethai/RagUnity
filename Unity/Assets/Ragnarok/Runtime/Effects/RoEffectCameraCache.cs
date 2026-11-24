using Ragnarok.Diagnostics;
using UnityEngine;
using UnityCamera = global::UnityEngine.Camera;

namespace Ragnarok.Effects {
    internal static class RoEffectCameraCache {
        private static UnityCamera cachedMainCamera;
        private static int cachedMainCameraFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {
            cachedMainCamera = null;
            cachedMainCameraFrame = -1;
        }

        public static UnityCamera Resolve(UnityCamera preferredCamera) {
            if (preferredCamera != null) {
                return preferredCamera;
            }

            int frame = Time.frameCount;
            if (cachedMainCamera == null || cachedMainCameraFrame != frame) {
                RoRuntimePerfCounters.IncrementEffectCameraCacheMiss();
                cachedMainCamera = UnityCamera.main;
                cachedMainCameraFrame = frame;
            } else {
                RoRuntimePerfCounters.IncrementEffectCameraCacheHit();
            }

            return cachedMainCamera;
        }
    }
}
