using System;
using Ragnarok.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.Input {
    public static class RoPointerScreenPosition {
        private const int MaxGuiSampleFrameAge = 60;

        private static Vector2 guiScreenPosition;
        private static int guiScreenPositionFrame = -1000;

        public static void ReportGuiMousePosition(Event currentEvent) {
            long reportGuiStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                if (currentEvent == null) {
                    return;
                }

                if (!CanUseGuiMousePosition(currentEvent.type)) {
                    return;
                }

                Vector2 guiPosition = currentEvent.mousePosition;
                if (!IsFinite(guiPosition)) {
                    return;
                }

                if (currentEvent.type == EventType.Repaint && guiPosition == Vector2.zero) {
                    return;
                }

                Vector2 screenPosition = new Vector2(guiPosition.x, Screen.height - guiPosition.y);
                if (!IsInsideScreen(screenPosition)) {
                    return;
                }

                guiScreenPosition = screenPosition;
                guiScreenPositionFrame = Time.frameCount;
            } finally {
                RoRuntimePerfCounters.AddPointerScreenReportGuiTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(reportGuiStartTicks));
            }
        }

        /// <summary>Diagnostics (!pointersweep): the pointer circles the screen centre, a third of the height away, every 4 s.</summary>
        public static bool DiagnosticSweep { get; set; }

        public static bool TryRead(InputActionReference actionReference, out Vector2 position) {
            if (DiagnosticSweep) {
                float angle = Time.unscaledTime * (Mathf.PI * 0.5f);
                float radius = Screen.height / 3f;
                position = new Vector2(Screen.width * 0.5f + Mathf.Cos(angle) * radius, Screen.height * 0.5f + Mathf.Sin(angle) * radius);
                return true;
            }

            InputAction action = actionReference != null ? actionReference.action : null;
            if (action != null && action.enabled) {
                position = action.ReadValue<Vector2>();
                return true;
            }

            if (TryReadRecentGuiPosition(out position)) {
                return true;
            }

            if (TryReadInputSystemMouse(out position)) {
                return true;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (TryReadLegacyMousePosition(out position)) {
                return true;
            }
#endif

            position = default;
            return false;
        }

        private static bool TryReadRecentGuiPosition(out Vector2 position) {
            if (Time.frameCount - guiScreenPositionFrame <= MaxGuiSampleFrameAge &&
                IsInsideScreen(guiScreenPosition)) {
                position = guiScreenPosition;
                return true;
            }

            position = default;
            return false;
        }

        private static bool TryReadInputSystemMouse(out Vector2 position) {
            Mouse mouse = Mouse.current;
            if (mouse == null) {
                position = default;
                return false;
            }

            position = mouse.position.ReadValue();
            return IsInsideScreen(position);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        // only with the old input manager on: with the Input System alone it throws, and the cursor outside the window
        // reached it every frame for each pointer reader (an exception each, 628 B)
        private static bool TryReadLegacyMousePosition(out Vector2 position) {
            try {
                Vector3 legacyPosition = UnityEngine.Input.mousePosition;
                position = new Vector2(legacyPosition.x, legacyPosition.y);
                return IsInsideScreen(position) && (position.x != 0f || position.y != 0f);
            } catch (InvalidOperationException) {
                position = default;
                return false;
            }
        }
#endif

        private static bool CanUseGuiMousePosition(EventType type) {
            return type == EventType.Repaint ||
                type == EventType.MouseMove ||
                type == EventType.MouseDown ||
                type == EventType.MouseUp ||
                type == EventType.MouseDrag;
        }

        private static bool IsInsideScreen(Vector2 position) {
            return IsFinite(position) &&
                position.x >= 0f &&
                position.y >= 0f &&
                position.x <= Mathf.Max(1, Screen.width) &&
                position.y <= Mathf.Max(1, Screen.height);
        }

        private static bool IsFinite(Vector2 value) {
            return !float.IsNaN(value.x) &&
                !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) &&
                !float.IsInfinity(value.y);
        }
    }
}
