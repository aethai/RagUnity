using Ragnarok.Maps;
using Ragnarok.UI;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Runtime tuning panel for catalog-driven RSM wind sway. Spawn from the
    /// "Ragnarok/Diagnostics/Wind Sway Debug" editor menu; toggle visibility with Ctrl+Alt+Shift+F11.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class RoWindSwayDebugOverlay : MonoBehaviour {
        private const KeyCode ToggleKey = KeyCode.F11;
        private const EventModifiers ToggleModifiers = EventModifiers.Control | EventModifiers.Alt | EventModifiers.Shift;
        private static readonly Rect PanelRect = new Rect(10f, 530f, 390f, 226f);
        private static readonly int WeatherWindDirectionId = Shader.PropertyToID("_RoWeatherWindDirection");
        private static readonly int WeatherWindStrengthId = Shader.PropertyToID("_RoWeatherWindStrength");

        private static RoWindSwayDebugOverlay instance;
        private static bool allowAwakeInstance;

        [System.NonSerialized]
        private bool createdByEnable;
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
            RoWindSwayGlobals.ResetGlobals();
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
            var go = new GameObject("RoWindSwayDebugOverlay") { hideFlags = HideFlags.DontSave };
            allowAwakeInstance = true;
            try {
                instance = go.AddComponent<RoWindSwayDebugOverlay>();
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

        private void OnEnable() {
            EnsureRuntimeOwnedInstance();
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

        private void Update() {
            EnsureRuntimeOwnedInstance();
        }

        private void OnGUI() {
            Event current = Event.current;
            if (current == null || !EnsureRuntimeOwnedInstance()) {
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
        }

        private void DrawPanel() {
            Vector4 parameters = RoWindSwayGlobals.CurrentParams;
            float amplitudeMultiplier = parameters.x;
            float frequencyMultiplier = parameters.y;
            float baseBreeze = parameters.z;
            float gustStrength = parameters.w;

            GUILayout.BeginArea(PanelRect, panelStyle);
            GUILayout.Label("[Ctrl+Alt+Shift+F11] Wind Sway Debug", labelStyle);
            bool changed = false;
            changed |= DrawSlider("Amplitude", ref amplitudeMultiplier, 0f, 3f);
            changed |= DrawSlider("Frequency", ref frequencyMultiplier, 0f, 3f);
            changed |= DrawSlider("Base Breeze", ref baseBreeze, 0f, 1f);
            changed |= DrawSlider("Gust Strength", ref gustStrength, 0f, 1f);

            Vector4 windDirection = Shader.GetGlobalVector(WeatherWindDirectionId);
            float windStrength = Shader.GetGlobalFloat(WeatherWindStrengthId);
            float gust = 0.7f * Mathf.Sin(Time.time * 0.31f) + 0.3f * Mathf.Sin(Time.time * 0.53f);
            float gustFactor = Mathf.Max(0f, 1f + gustStrength * 0.5f * gust);
            GUILayout.Space(8f);
            GUILayout.Label($"wind dir: ({windDirection.x:0.00}, {windDirection.z:0.00})   strength: {windStrength:0.###}", labelStyle);
            GUILayout.Label($"gust strength: {gustStrength:0.###}   sample factor: {gustFactor:0.###}", labelStyle);
            GUILayout.Label($"environment scale: {RoWindSwayGlobals.CurrentEnvironmentScale:0.###}", labelStyle);

            if (changed) {
                RoWindSwayGlobals.Apply(amplitudeMultiplier, frequencyMultiplier, baseBreeze, gustStrength);
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

        private static void DestroyOverlay(RoWindSwayDebugOverlay overlay, bool forceImmediate) {
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
            RoWindSwayDebugOverlay[] overlays = FindOverlayInstances();
            for (int i = 0; i < overlays.Length; i++) {
                DestroyOverlay(overlays[i], forceImmediate);
            }

            instance = null;
        }

        private static RoWindSwayDebugOverlay[] FindOverlayInstances() {
#if UNITY_EDITOR
            return Resources.FindObjectsOfTypeAll<RoWindSwayDebugOverlay>();
#else
            return FindObjectsByType<RoWindSwayDebugOverlay>(FindObjectsSortMode.None);
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
