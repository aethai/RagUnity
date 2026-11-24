using Ragnarok.Diagnostics;
using UnityEngine;

namespace Ragnarok.Maps {
    internal sealed class RoStaticModelBatchChunkStats : MonoBehaviour {
        private static int total;
        private static int visible;
        private static int generation;
        private bool registered;
        private bool isVisible;
        private int registeredGeneration = -1;
        private int visibleGeneration = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration() {
            ResetStats();
        }

        public static void ResetStats() {
            total = 0;
            visible = 0;
            generation++;
            RoRuntimePerfCounters.SetStaticChunkCounts(0, 0);
        }

        private void OnEnable() {
            Register();
        }

        private void OnDisable() {
            if (isVisible && visibleGeneration == generation) {
                isVisible = false;
                visibleGeneration = -1;
                visible = Mathf.Max(0, visible - 1);
            }

            if (registered && registeredGeneration == generation) {
                registered = false;
                registeredGeneration = -1;
                total = Mathf.Max(0, total - 1);
            }

            Publish();
        }

        private void OnBecameVisible() {
            Register();
            if (!isVisible || visibleGeneration != generation) {
                isVisible = true;
                visibleGeneration = generation;
                visible++;
                Publish();
            }
        }

        private void OnBecameInvisible() {
            if (isVisible && visibleGeneration == generation) {
                isVisible = false;
                visibleGeneration = -1;
                visible = Mathf.Max(0, visible - 1);
                Publish();
            }
        }

        private void Register() {
            if (registered && registeredGeneration == generation) {
                return;
            }

            registered = true;
            registeredGeneration = generation;
            total++;
            Publish();
        }

        private static void Publish() {
            RoRuntimePerfCounters.SetStaticChunkCounts(visible, total);
        }
    }
}
