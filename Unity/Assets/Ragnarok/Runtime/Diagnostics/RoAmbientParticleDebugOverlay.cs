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
    /// Runtime tuning panel for ambient weather particles. Spawn from the
    /// "Ragnarok/Diagnostics/Ambient Particles Debug" editor menu; toggle visibility with Ctrl+Alt+Shift+F12.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class RoAmbientParticleDebugOverlay : MonoBehaviour {
        private const float RefreshIntervalSeconds = 0.5f;
        private const KeyCode ToggleKey = KeyCode.F12;
        private const EventModifiers ToggleModifiers = EventModifiers.Control | EventModifiers.Alt | EventModifiers.Shift;
        private static readonly Rect PanelRect = new Rect(410f, 70f, 390f, 250f);

        private static RoAmbientParticleDebugOverlay instance;
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
            var go = new GameObject("RoAmbientParticleDebugOverlay") { hideFlags = HideFlags.DontSave };
            allowAwakeInstance = true;
            try {
                instance = go.AddComponent<RoAmbientParticleDebugOverlay>();
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
            RoAmbientParticleSettings settings = mapController != null ? mapController.AmbientParticleSettings : null;
            GUILayout.BeginArea(PanelRect, panelStyle);
            GUILayout.Label("[Ctrl+Alt+Shift+F12] Ambient Particles Debug", labelStyle);

            if (settings == null) {
                GUILayout.Label("No active ambient particle settings.", labelStyle);
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

            changed |= DrawSlider("Leaves / Tree / Min", ref settings.leavesPerTreePerMinute, 0f, 240f);
            float maxParticles = settings.maxLeafParticles;
            if (DrawSlider("Max Particles", ref maxParticles, 1f, 1000f)) {
                settings.maxLeafParticles = Mathf.RoundToInt(maxParticles);
                changed = true;
            }

            changed |= DrawSlider("Wind Influence", ref settings.windInfluence, 0f, 3f);
            changed |= DrawSlider("Lifetime", ref settings.leafLifetimeSeconds, 0.5f, 20f);
            changed |= DrawSlider("Radius", ref settings.emitterActivationRadius, 5f, 120f);

            RoAmbientParticleController runtime = mapController != null ? mapController.Weather?.AmbientParticleRuntime : null;
            string state = runtime != null
                ? $"cached trees: {runtime.CachedEmitterCount}   active: {runtime.ActiveEmitterCount}   live: {runtime.LiveParticleCount}   skyless: {runtime.IsSkylessMap}"
                : "runtime: not initialized";
            GUILayout.Space(8f);
            GUILayout.Label(state, labelStyle);
            GUILayout.Label("day/night tint: later candidate", labelStyle);

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

        private static void DestroyOverlay(RoAmbientParticleDebugOverlay overlay, bool forceImmediate) {
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
            RoAmbientParticleDebugOverlay[] overlays = FindOverlayInstances();
            for (int i = 0; i < overlays.Length; i++) {
                DestroyOverlay(overlays[i], forceImmediate);
            }

            instance = null;
        }

        private static RoAmbientParticleDebugOverlay[] FindOverlayInstances() {
#if UNITY_EDITOR
            return Resources.FindObjectsOfTypeAll<RoAmbientParticleDebugOverlay>();
#else
            return FindObjectsByType<RoAmbientParticleDebugOverlay>(FindObjectsSortMode.None);
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
