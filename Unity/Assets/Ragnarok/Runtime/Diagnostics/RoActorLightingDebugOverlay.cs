using System.Collections.Generic;
using Ragnarok.Entities;
using Ragnarok.Maps;
using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Read-only runtime overlay that answers "why is this actor this bright?" for any actor on screen.
    /// Reads map-light multiply/add from each <see cref="RoActorMapLightProbe"/> and the live shader policy
    /// flags from the sprite renderer's MaterialPropertyBlock, then classifies via <see cref="RoActorLightingDebug"/>.
    /// Changes no rendering state. Spawn from the "Ragnarok/Diagnostics/Actor Lighting Debug" editor menu;
    /// toggle visibility in play with Ctrl+Alt+Shift+F9.
    /// </summary>
    public sealed class RoActorLightingDebugOverlay : MonoBehaviour {
        private const float ProbeRefreshInterval = 0.5f;
        private const float MaxMarkerDistance = 60f;
        private const KeyCode ToggleKey = KeyCode.F9;
        private const EventModifiers ToggleModifiers = EventModifiers.Control | EventModifiers.Alt | EventModifiers.Shift;

        private static readonly int ReceiveRealtimeShadowsId = Shader.PropertyToID("_RoReceiveRealtimeShadows");
        private static readonly int UseAdditionalLightsId = Shader.PropertyToID("_RoUseAdditionalLights");
        private static readonly int RealtimeShadowStrengthId = Shader.PropertyToID("_RoRealtimeShadowStrength");

        private static RoActorLightingDebugOverlay instance;

        private readonly List<RoActorMapLightProbe> probes = new List<RoActorMapLightProbe>(64);
        private MaterialPropertyBlock scratchBlock;
        private RoRuntimeMapController mapController;
        private float nextRefreshTime;
        private bool visible = true;
        private Texture2D swatch;
        private GUIStyle panelStyle;

        public static bool IsActive => instance != null;

        public static void Enable() {
            if (instance != null) {
                instance.visible = true;
                return;
            }

            var go = new GameObject("RoActorLightingDebugOverlay") { hideFlags = HideFlags.DontSave };
            instance = go.AddComponent<RoActorLightingDebugOverlay>();
        }

        public static void Disable() {
            if (instance == null) {
                return;
            }

            Destroy(instance.gameObject);
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
            instance = this;
            swatch = Texture2D.whiteTexture;
            EnsureScratchBlock();
        }

        private void OnDestroy() {
            if (instance == this) {
                instance = null;
            }
        }

        private void Update() {
            if (Time.unscaledTime >= nextRefreshTime) {
                nextRefreshTime = Time.unscaledTime + ProbeRefreshInterval;
                RefreshProbes();
            }
        }

        private void RefreshProbes() {
            probes.Clear();
            probes.AddRange(FindObjectsByType<RoActorMapLightProbe>(FindObjectsSortMode.None));
            if (mapController == null) {
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

                if (current.type == EventType.KeyDown && current.keyCode == ToggleKey && HasToggleModifiers(current.modifiers)) {
                    visible = !visible;
                    current.Use();
                }

                if (!visible) {
                    return;
                }

                EnsureStyles();
                UnityEngine.Camera cam = UnityEngine.Camera.main;
                // Event.current.mousePosition is already in GUI space (top-left origin), matching guiPos below.
                Vector2 mouse = current.mousePosition;

                int tracked = 0;
                int legacyTinted = 0;
                float bestMouseDistance = float.MaxValue;
                RoActorLightingDebugState focusState = default;
                string focusName = null;
                bool hasFocus = false;

                for (int i = 0; i < probes.Count; i++) {
                    RoActorMapLightProbe probe = probes[i];
                    if (probe == null) {
                        continue;
                    }

                    tracked++;
                    RoActorLightingDebugState state = BuildState(probe);
                    if (state.LegacyBakedTintActive) {
                        legacyTinted++;
                    }

                    if (cam == null) {
                        continue;
                    }

                    Vector3 worldPos = probe.transform.position;
                    if (Vector3.Distance(cam.transform.position, worldPos) > MaxMarkerDistance) {
                        continue;
                    }

                    Vector3 screen = cam.WorldToScreenPoint(worldPos + Vector3.up);
                    if (screen.z <= 0f) {
                        continue;
                    }

                    Vector2 guiPos = new Vector2(screen.x, Screen.height - screen.y);
                    DrawMarker(guiPos, state);

                    float mouseDistance = Vector2.Distance(guiPos, mouse);
                    if (mouseDistance < bestMouseDistance) {
                        bestMouseDistance = mouseDistance;
                        focusState = state;
                        focusName = probe.gameObject.name;
                        hasFocus = true;
                    }
                }

                DrawHeader(tracked, legacyTinted);
                if (hasFocus) {
                    DrawDetailPanel(focusName, focusState);
                }
            } finally {
                long elapsedTicks = RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(onGuiStartTicks);
                RoRuntimePerfCounters.AddOnGuiTicks(elapsedTicks);
                RoRuntimePerfCounters.AddActorLightingDebugOnGuiTicks(elapsedTicks);
            }
        }

        private RoActorLightingDebugState BuildState(RoActorMapLightProbe probe) {
            RoShadowRenderMode mode = ResolveMode(probe);
            bool receive = false;
            bool additional = false;
            float strength = 0f;

            MeshRenderer renderer = probe.GetComponentInChildren<MeshRenderer>(true);
            if (renderer != null) {
                EnsureScratchBlock();
                scratchBlock.Clear();
                renderer.GetPropertyBlock(scratchBlock);
                if (!scratchBlock.isEmpty) {
                    receive = scratchBlock.GetFloat(ReceiveRealtimeShadowsId) > 0.5f;
                    additional = scratchBlock.GetFloat(UseAdditionalLightsId) > 0.5f;
                    strength = scratchBlock.GetFloat(RealtimeShadowStrengthId);
                }
            }

            return RoActorLightingDebug.Classify(
                mode,
                probe.SmoothedMapLightMultiply,
                probe.SmoothedMapLightAdd,
                probe.SampleBakedShadows,
                receive,
                additional,
                strength);
        }

        private void EnsureScratchBlock() {
            scratchBlock ??= new MaterialPropertyBlock();
        }

        private RoShadowRenderMode ResolveMode(RoActorMapLightProbe probe) {
            RoActorLayeredVisual layered = probe.GetComponentInChildren<RoActorLayeredVisual>(true);
            if (layered != null) {
                return layered.ShadowRenderMode;
            }

            return mapController != null ? mapController.ShadowRenderMode : RoShadowRenderMode.Original;
        }

        private void DrawHeader(int tracked, int legacyTinted) {
            RoShadowRenderMode mode = mapController != null ? mapController.ShadowRenderMode : RoShadowRenderMode.Original;
            string header = $"[Ctrl+Alt+Shift+F9] Actor Lighting Debug\nmode: {mode}   actors: {tracked}   legacy-tinted: {legacyTinted}";
            var rect = new Rect(10f, 10f, 320f, 48f);
            Color previous = GUI.color;
            GUI.color = legacyTinted > 0 ? new Color(1f, 0.55f, 0.55f, 0.92f) : new Color(0.8f, 0.9f, 1f, 0.9f);
            GUI.Box(rect, header, panelStyle);
            GUI.color = previous;
        }

        private void DrawDetailPanel(string actorName, RoActorLightingDebugState state) {
            string body = actorName + "\n" + RoActorLightingDebug.Describe(state);
            var rect = new Rect(Screen.width - 360f, 10f, 350f, 230f);
            Color previous = GUI.color;
            GUI.color = state.LegacyBakedTintActive ? new Color(1f, 0.5f, 0.5f, 0.95f) : new Color(0.85f, 0.95f, 0.85f, 0.92f);
            GUI.Box(rect, body, panelStyle);
            GUI.color = previous;

            var swatchRect = new Rect(rect.x + rect.width - 34f, rect.y + 6f, 24f, 24f);
            GUI.color = state.MapLightMultiply;
            GUI.DrawTexture(swatchRect, swatch);
            GUI.color = previous;
        }

        private void DrawMarker(Vector2 guiPos, RoActorLightingDebugState state) {
            Color markerColor;
            if (state.LegacyBakedTintActive) {
                markerColor = Color.red;
            } else if (state.ReceivesRealtimeShadows) {
                markerColor = Color.green;
            } else {
                markerColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            }

            var rect = new Rect(guiPos.x - 5f, guiPos.y - 5f, 10f, 10f);
            Color previous = GUI.color;
            GUI.color = markerColor;
            GUI.DrawTexture(rect, swatch);
            GUI.color = previous;
        }

        private static bool HasToggleModifiers(EventModifiers modifiers) {
            return (modifiers & ToggleModifiers) == ToggleModifiers;
        }

        private void EnsureStyles() {
            if (panelStyle == null) {
                panelStyle = new GUIStyle(GUI.skin.box) {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    wordWrap = true,
                    richText = false
                };
                panelStyle.normal.textColor = Color.black;
            }
        }
    }
}
