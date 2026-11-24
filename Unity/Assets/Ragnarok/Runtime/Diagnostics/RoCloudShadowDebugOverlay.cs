using Ragnarok.Effects;
using Ragnarok.Maps;
using Ragnarok.UI;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Runtime tuning panel for world cloud shadows. Spawn from the
    /// "Ragnarok/Diagnostics/Cloud Shadow Debug" editor menu; toggle visibility with Ctrl+Alt+Shift+F10.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class RoCloudShadowDebugOverlay : MonoBehaviour {
        private const float RefreshIntervalSeconds = 0.5f;
        private const KeyCode ToggleKey = KeyCode.F10;
        private const EventModifiers ToggleModifiers = EventModifiers.Control | EventModifiers.Alt | EventModifiers.Shift;
        private static readonly Rect PanelRect = new Rect(10f, 70f, 390f, 450f);

        private static RoCloudShadowDebugOverlay instance;
        private static bool allowAwakeInstance;

        [System.NonSerialized]
        private bool createdByEnable;
        private RoRuntimeMapController mapController;
        private float nextRefreshTime;
        private bool visible = true;
        private bool panelPointerCaptured;
        private GUIStyle panelStyle;
        private GUIStyle labelStyle;

        public static bool IsActive => instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            DestroyAllOverlayInstances(forceImmediate: !Application.isPlaying);
            instance = null;
            allowAwakeInstance = false;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorCleanup() {
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupBeforeEditorReload;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupBeforeEditorReload;
            EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;

            if (!EditorApplication.isPlaying) {
                DestroyAllOverlayInstances(forceImmediate: true);
            }
        }

        private static void CleanupBeforeEditorReload() {
            DestroyAllOverlayInstances(forceImmediate: true);
        }

        private static void HandleEditorPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) {
                DestroyAllOverlayInstances(forceImmediate: true);
            }
        }
#endif

        public static void Enable() {
            if (!Application.isPlaying) {
                DestroyAllOverlayInstances(forceImmediate: true);
                return;
            }

            if (instance != null) {
                instance.visible = true;
                return;
            }

            DestroyAllOverlayInstances(forceImmediate: true);
            var go = new GameObject("RoCloudShadowDebugOverlay") { hideFlags = HideFlags.DontSave };
            allowAwakeInstance = true;
            try {
                instance = go.AddComponent<RoCloudShadowDebugOverlay>();
            } finally {
                allowAwakeInstance = false;
            }
        }

        public static void Disable() {
            if (instance == null) {
                return;
            }

            DestroyOverlay(instance, forceImmediate: false);
            instance = null;
        }

        public static void Toggle() {
            if (instance == null) {
                Enable();
            } else {
                Disable();
            }
        }

        private void Awake() {
            if (!allowAwakeInstance) {
                DestroyOverlay(this, forceImmediate: true);
                return;
            }

            createdByEnable = true;
            instance = this;
        }

        private void OnDestroy() {
            ReleasePanelPointerCapture();
            createdByEnable = false;
            if (instance == this) {
                instance = null;
            }
        }

        private void OnDisable() {
            ReleasePanelPointerCapture();
        }

        private void OnEnable() {
            EnsureRuntimeOwnedInstance();
        }

        private void Update() {
            if (!EnsureRuntimeOwnedInstance()) {
                return;
            }

            UpdatePanelPointerCaptureFromMouse();

            if (Time.unscaledTime >= nextRefreshTime) {
                nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
                mapController = FindFirstObjectByType<RoRuntimeMapController>();
            }
        }

        private void OnGUI() {
            long onGuiStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            Event current = Event.current;
            RoRuntimePerfCounters.IncrementOnGuiEvent(current != null ? (int)current.type : -1);
            try {
                if (current == null) {
                    return;
                }

                if (!EnsureRuntimeOwnedInstance()) {
                    return;
                }

                if (current.type == EventType.KeyDown && current.keyCode == ToggleKey && HasToggleModifiers(current.modifiers)) {
                    visible = !visible;
                    current.Use();
                }

                if (!visible) {
                    ReleasePanelPointerCapture();
                    return;
                }

                bool pointerOverPanel = UpdatePanelPointerCapture(current);
                EnsureStyles();
                DrawPanel();
                if (pointerOverPanel && IsConsumablePointerEvent(current)) {
                    current.Use();
                }
            } finally {
                long elapsedTicks = RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(onGuiStartTicks);
                RoRuntimePerfCounters.AddOnGuiTicks(elapsedTicks);
            }
        }

        private void DrawPanel() {
            RoCloudShadowSettings settings = mapController != null ? mapController.CloudShadowSettings : null;
            GUILayout.BeginArea(PanelRect, panelStyle);
            GUILayout.Label("[Ctrl+Alt+Shift+F10] Cloud Shadow Debug", labelStyle);

            if (settings == null) {
                GUILayout.Label("No active cloud shadow settings.", labelStyle);
                GUILayout.EndArea();
                return;
            }

            settings.EnsureCurrentDefaults();
            bool changed = false;
            bool enabled = GUILayout.Toggle(settings.enabled, "Enabled");
            if (enabled != settings.enabled) {
                settings.enabled = enabled;
                changed = true;
            }

            changed |= DrawSlider("World Strength", ref settings.worldStrength, 0f, 1f);
            changed |= DrawSlider("Actor Strength", ref settings.actorStrength, 0f, 1f);
            changed |= DrawSlider("Coverage", ref settings.coverage, 0f, 1f);
            changed |= DrawSlider("Softness", ref settings.softness, 0.0001f, 1f);
            changed |= DrawSlider("Scale", ref settings.scaleRepeatsPerWorldUnit, 0.001f, 0.05f);
            changed |= DrawSlider("Speed", ref settings.speedWorldUnitsPerSecond, 0f, 4f);
            bool followWind = GUILayout.Toggle(settings.followWindDirection, "Follow Wind");
            if (followWind != settings.followWindDirection) {
                settings.followWindDirection = followWind;
                changed = true;
            }

            changed |= DrawSlider("Direction", ref settings.directionDegrees, 0f, 360f);
            changed |= DrawSlider("Sunlit Mask", ref settings.sunlitMaskBlend, 0f, 1f);
            changed |= DrawSlider("Lightmap", ref settings.lightmapInfluence, 0f, 1f);
            changed |= DrawSlider("Overcast Coverage", ref settings.overcastCoverageShift, 0f, 0.5f);
            changed |= DrawSlider("Overcast Strength", ref settings.overcastStrengthMultiplier, 1f, 2f);

            RoCloudShadowController runtime = mapController != null ? mapController.CloudShadows : null;
            string state = runtime != null
                ? $"strength: {runtime.CurrentStrength:0.000} -> {runtime.TargetStrength:0.000}   fade: {runtime.CurrentFade:0.00}->{runtime.TargetFade:0.00}   daylight: {runtime.DaylightFactor:0.00}   overcast: {runtime.OvercastFactor:0.00}   skyless: {runtime.IsSkylessMap}"
                : "runtime: not initialized";
            GUILayout.Space(8f);
            GUILayout.Label(state, labelStyle);

            if (changed) {
                settings.EnsureCurrentDefaults();
                mapController?.Weather?.SyncRuntime();
            }

            GUILayout.EndArea();
        }

        private bool EnsureRuntimeOwnedInstance() {
            if (Application.isPlaying && createdByEnable) {
                if (instance == null) {
                    instance = this;
                }

                return true;
            }

            DestroyOverlay(this, forceImmediate: !Application.isPlaying);
            return false;
        }

        private static bool DrawSlider(string label, ref float value, float min, float max) {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:0.###}", GUILayout.Width(150f));
            float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(210f));
            GUILayout.EndHorizontal();
            if (Mathf.Approximately(next, value)) {
                return false;
            }

            value = next;
            return true;
        }

        private static bool HasToggleModifiers(EventModifiers modifiers) {
            return (modifiers & ToggleModifiers) == ToggleModifiers;
        }

        private bool UpdatePanelPointerCapture(Event current) {
            bool pointerOverPanel = PanelRect.Contains(current.mousePosition);
            if (IsPointerPositionEvent(current)) {
                SetPanelPointerCapture(pointerOverPanel);
            }

            if (pointerOverPanel && current.type == EventType.MouseDown && current.button == 0) {
                RoUiPointerCapture.CapturePrimaryUntilRelease();
            }

            return pointerOverPanel;
        }

        private void UpdatePanelPointerCaptureFromMouse() {
            if (!visible) {
                ReleasePanelPointerCapture();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) {
                return;
            }

            Vector2 screenPosition = mouse.position.ReadValue();
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            bool pointerOverPanel = PanelRect.Contains(guiPosition);
            SetPanelPointerCapture(pointerOverPanel);
            if (pointerOverPanel && mouse.leftButton.wasPressedThisFrame) {
                RoUiPointerCapture.CapturePrimaryUntilRelease();
            }
        }

        private void SetPanelPointerCapture(bool captured) {
            if (captured == panelPointerCaptured) {
                return;
            }

            RoUiPointerCapture.SetGlobalBlocker(this, captured);
            panelPointerCaptured = captured;
        }

        private void ReleasePanelPointerCapture() {
            if (!panelPointerCaptured) {
                return;
            }

            RoUiPointerCapture.SetGlobalBlocker(this, false);
            panelPointerCaptured = false;
        }

        private static bool IsPointerPositionEvent(Event current) {
            return current.type == EventType.MouseMove ||
                   current.type == EventType.MouseDown ||
                   current.type == EventType.MouseUp ||
                   current.type == EventType.MouseDrag;
        }

        private static bool IsConsumablePointerEvent(Event current) {
            return current.type == EventType.MouseDown ||
                   current.type == EventType.MouseUp ||
                   current.type == EventType.MouseDrag ||
                   current.type == EventType.ScrollWheel;
        }

        private static void DestroyOverlay(RoCloudShadowDebugOverlay overlay, bool forceImmediate) {
            if (overlay == null) {
                return;
            }

            overlay.ReleasePanelPointerCapture();
            if (Application.isPlaying && !forceImmediate) {
                Destroy(overlay.gameObject);
            } else {
                DestroyImmediate(overlay.gameObject);
            }
        }

        private static void DestroyAllOverlayInstances(bool forceImmediate) {
            RoCloudShadowDebugOverlay[] overlays = FindOverlayInstances();
            for (int i = 0; i < overlays.Length; i++) {
                DestroyOverlay(overlays[i], forceImmediate);
            }

            instance = null;
        }

        private static RoCloudShadowDebugOverlay[] FindOverlayInstances() {
#if UNITY_EDITOR
            return Resources.FindObjectsOfTypeAll<RoCloudShadowDebugOverlay>();
#else
            return FindObjectsByType<RoCloudShadowDebugOverlay>(FindObjectsSortMode.None);
#endif
        }

        private void EnsureStyles() {
            if (panelStyle == null) {
                panelStyle = new GUIStyle(GUI.skin.box) {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    padding = new RectOffset(10, 10, 8, 8)
                };
            }

            if (labelStyle == null) {
                labelStyle = new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 12
                };
            }
        }
    }
}
