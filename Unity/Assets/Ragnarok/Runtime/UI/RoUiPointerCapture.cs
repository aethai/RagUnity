using System.Collections.Generic;
using Ragnarok.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// Whether the pointer belongs to the UI rather than the world (click-to-move, the cursor, the grid selector and the
    /// camera ask): a global blocker (the loading screen, a fade), a primary press the UI took until its release, or a
    /// classic window / drag / modal box under the point.
    /// </summary>
    public static class RoUiPointerCapture {
        private static readonly HashSet<int> GlobalBlockers = new HashSet<int>();
        private static bool capturePrimaryUntilRelease;

        static RoUiPointerCapture() {
            InitializeCameraBridge();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeCameraBridge() {
            GlobalBlockers.Clear();
            capturePrimaryUntilRelease = false;
            Ragnarok.Camera.RoCameraController.PointerOverUiResolver = IsPointerCaptured;
        }

        public static void CapturePrimaryUntilRelease() {
            capturePrimaryUntilRelease = true;
        }

        public static void SetGlobalBlocker(UnityEngine.Object owner, bool blocked) {
            if (owner == null) {
                return;
            }

            int ownerId = owner.GetInstanceID();
            if (blocked) {
                GlobalBlockers.Add(ownerId);
            } else {
                GlobalBlockers.Remove(ownerId);
            }
        }

        public static bool IsPointerCaptured(Vector2 screenPosition) {
            long pointerCaptureStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                if (GlobalBlockers.Count > 0) {
                    return true;
                }

                if (capturePrimaryUntilRelease) {
                    Mouse mouse = Mouse.current;
                    if (mouse != null && mouse.leftButton.isPressed) {
                        return true;
                    }

                    capturePrimaryUntilRelease = false;
                }

                Ragnarok.UI.Classic.RoClassicUiSystem classic = Ragnarok.UI.Classic.RoClassicUiSystem.Instance;
                return classic != null && classic.Windows.HitTest(screenPosition);
            } finally {
                RoRuntimePerfCounters.AddUiPointerCaptureTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(pointerCaptureStartTicks));
            }
        }

        /// <summary>Diagnostics (!ptr): which gate captures the pointer at <paramref name="screenPosition"/>.</summary>
        public static string Describe(Vector2 screenPosition) {
            var sb = new System.Text.StringBuilder();
            sb.Append("blockers=").Append(GlobalBlockers.Count).Append(" primaryHold=").Append(capturePrimaryUntilRelease);
            var classic = Ragnarok.UI.Classic.RoClassicUiSystem.Instance;
            if (classic != null) {
                sb.Append(" classic=").Append(classic.Windows.HitTest(screenPosition)).Append(" (").Append(classic.Windows.DescribeHit(screenPosition)).Append(')');
            } else {
                sb.Append(" classic=none");
            }

            return sb.ToString();
        }
    }
}
