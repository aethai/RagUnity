using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ragnarok.Maps;
using Ragnarok.UI;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Runtime picker for RO map model surfaces. Spawn from
    /// "Ragnarok/Diagnostics/Map Model Picker"; click a visible model surface to copy its fingerprint.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class RoMapModelPickerOverlay : MonoBehaviour {
        private const KeyCode ToggleKey = KeyCode.F12;
        private const EventModifiers ToggleModifiers = EventModifiers.Control | EventModifiers.Alt | EventModifiers.Shift;
        private const int MaxCandidates = 8;
        private static readonly Rect PanelRect = new Rect(410f, 70f, 500f, 330f);

        private static RoMapModelPickerOverlay instance;
        private static bool allowAwakeInstance;

        [NonSerialized] private bool createdByEnable;
        private bool visible = true;
        private bool panelPointerCaptured;
        private GUIStyle panelStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallLabelStyle;
        private string lastPickSummary = "No pick yet.";
        private string lastPickClipboard = string.Empty;

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
            var go = new GameObject("RoMapModelPickerOverlay") { hideFlags = HideFlags.DontSave };
            allowAwakeInstance = true;
            try {
                instance = go.AddComponent<RoMapModelPickerOverlay>();
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

        private void OnDisable() {
            ReleasePanelPointerCapture();
        }

        private void OnDestroy() {
            ReleasePanelPointerCapture();
            createdByEnable = false;
            if (instance == this) {
                instance = null;
            }
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
            if (!pointerOverPanel && current.type == EventType.MouseDown && current.button == 0) {
                PickAtPointer(current.mousePosition);
                RoUiPointerCapture.CapturePrimaryUntilRelease();
                current.Use();
            }

            EnsureStyles();
            DrawPanel();
            if (pointerOverPanel && IsConsumablePointerEvent(current)) {
                current.Use();
            }
        }

        private void DrawPanel() {
            GUILayout.BeginArea(PanelRect, panelStyle);
            GUILayout.Label("[Ctrl+Alt+Shift+F12] Map Model Picker", labelStyle);
            GUILayout.Label("Click a map model surface. Best match is copied to clipboard.", smallLabelStyle);
            GUILayout.Space(8f);
            GUILayout.Label(lastPickSummary, smallLabelStyle);
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Last", GUILayout.Width(120f)) && !string.IsNullOrEmpty(lastPickClipboard)) {
                GUIUtility.systemCopyBuffer = lastPickClipboard;
            }

            if (GUILayout.Button("Clear", GUILayout.Width(80f))) {
                lastPickSummary = "No pick yet.";
                lastPickClipboard = string.Empty;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void PickAtPointer(Vector2 guiMousePosition) {
            if (!TryGetPointerScreenPosition(guiMousePosition, out Vector2 screenPosition)) {
                SetLastFailure("No pointer position available.");
                return;
            }

            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null) {
                SetLastFailure("No active camera found.");
                return;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            List<ModelPickCandidate> candidates = CollectCandidates(ray, out string mode);
            if (candidates.Count == 0) {
                SetLastFailure("No RoModelSurfaceInfo surfaces found under the loaded map.");
                return;
            }

            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            int count = Mathf.Min(MaxCandidates, candidates.Count);
            ModelPickCandidate best = candidates[0];
            lastPickClipboard = BuildClipboardText(best, candidates, count, mode, screenPosition);
            lastPickSummary = BuildPanelSummary(best, count, candidates.Count, mode);
            GUIUtility.systemCopyBuffer = lastPickClipboard;
            Debug.Log(lastPickClipboard, best.Renderer);

#if UNITY_EDITOR
            Selection.activeObject = best.Renderer != null ? best.Renderer.gameObject : best.Info.gameObject;
#endif
        }

        private static List<ModelPickCandidate> CollectCandidates(Ray ray, out string mode) {
            RoModelSurfaceInfo[] surfaces = FindObjectsByType<RoModelSurfaceInfo>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var candidates = new List<ModelPickCandidate>(Mathf.Min(MaxCandidates, surfaces.Length));

            for (int i = 0; i < surfaces.Length; i++) {
                RoModelSurfaceInfo info = surfaces[i];
                MeshRenderer renderer = info.GetComponent<MeshRenderer>();
                if (!IsPickable(renderer)) {
                    continue;
                }

                if (renderer.bounds.IntersectRay(ray, out float distance)) {
                    candidates.Add(new ModelPickCandidate(info, renderer, Mathf.Max(0f, distance), true));
                }
            }

            if (candidates.Count > 0) {
                mode = "bounds ray hit";
                return candidates;
            }

            if (!TryGetReferencePoint(ray, out Vector3 point)) {
                mode = "nearest bounds";
                return candidates;
            }

            for (int i = 0; i < surfaces.Length; i++) {
                RoModelSurfaceInfo info = surfaces[i];
                MeshRenderer renderer = info.GetComponent<MeshRenderer>();
                if (!IsPickable(renderer)) {
                    continue;
                }

                float distance = Mathf.Sqrt(renderer.bounds.SqrDistance(point));
                candidates.Add(new ModelPickCandidate(info, renderer, distance, false));
            }

            mode = $"nearest bounds around {FormatVector(point)}";
            return candidates;
        }

        private static bool IsPickable(MeshRenderer renderer) =>
            renderer != null &&
            renderer.enabled &&
            renderer.gameObject.activeInHierarchy &&
            renderer.sharedMaterial != null;

        private static bool TryGetReferencePoint(Ray ray, out Vector3 point) {
            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f);
            if (hits.Length > 0) {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                point = hits[0].point;
                return true;
            }

            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter)) {
                point = ray.GetPoint(enter);
                return true;
            }

            point = default;
            return false;
        }

        private static string BuildClipboardText(
            ModelPickCandidate best,
            IReadOnlyList<ModelPickCandidate> candidates,
            int count,
            string mode,
            Vector2 screenPosition
        ) {
            RoModelSurfaceInfo info = best.Info;
            Material material = best.Renderer != null ? best.Renderer.sharedMaterial : null;
            Texture texture = material != null ? material.mainTexture : null;
            Mesh mesh = ResolveSharedMesh(best.Renderer);
            string textureFileName = GetFileName(info.TextureName);

            var builder = new StringBuilder();
            builder.AppendLine("RO map model pick");
            builder.AppendLine($"mode: {mode}");
            builder.AppendLine($"screen: ({screenPosition.x:0.#}, {screenPosition.y:0.#})");
            builder.AppendLine($"modelFile: {info.ModelFile}");
            builder.AppendLine($"nodeName: {info.NodeName}");
            builder.AppendLine($"textureName: {info.TextureName}");
            builder.AppendLine($"textureFileName: {textureFileName}");
            builder.AppendLine($"instanceIndex: {info.InstanceIndex}");
            builder.AppendLine($"twoSided: {info.TwoSided}");
            builder.AppendLine($"materialMode: {info.MaterialMode}");
            builder.AppendLine($"material: {material?.name ?? "<missing>"}");
            builder.AppendLine($"mainTexture: {texture?.name ?? "<missing>"}");
            builder.AppendLine($"windSwayMode: {FormatMaterialFloat(material, "_RoWindSwayMode")}");
            builder.AppendLine($"windSwayAmplitude: {FormatMaterialFloat(material, "_RoWindSwayAmplitude")}");
            builder.AppendLine($"windSwayFrequency: {FormatMaterialFloat(material, "_RoWindSwayFrequency")}");
            builder.AppendLine($"meshColors: {BuildMeshColorSummary(mesh)}");
            builder.AppendLine($"vertices: {info.VertexCount}");
            builder.AppendLine($"triangles: {info.TriangleCount}");
            builder.AppendLine($"descriptorPos: {FormatVector(info.DescriptorPosition)}");
            builder.AppendLine($"descriptorRot: {FormatVector(info.DescriptorRotation)}");
            builder.AppendLine($"descriptorScale: {FormatVector(info.DescriptorScale)}");
            builder.AppendLine($"localBoundsCenter: {FormatVector(info.LocalMeshBounds.center)}");
            builder.AppendLine($"localBoundsSize: {FormatVector(info.LocalMeshBounds.size)}");
            builder.AppendLine($"worldBoundsCenter: {FormatVector(best.Renderer.bounds.center)}");
            builder.AppendLine($"worldBoundsSize: {FormatVector(best.Renderer.bounds.size)}");
            builder.AppendLine($"path: {BuildTransformPath(best.Renderer.transform)}");
            builder.AppendLine();
            builder.AppendLine("Wind catalog candidate:");
            builder.AppendLine("{");
            builder.AppendLine($"  \"textureContains\": \"{EscapeJson(textureFileName)}\",");
            builder.AppendLine("  \"mode\": \"Foliage\",");
            builder.AppendLine("  \"amplitude\": 0.06,");
            builder.AppendLine("  \"frequency\": 0.5,");
            builder.AppendLine("  \"anchor\": \"Radial\"");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("Feature database match candidate:");
            builder.AppendLine($"modelFile: {info.ModelFile}");
            builder.AppendLine($"nodeName: {info.NodeName}");
            builder.AppendLine($"textureName: {textureFileName}");
            builder.AppendLine();
            builder.AppendLine($"Nearest candidates ({count}/{candidates.Count}):");
            for (int i = 0; i < count; i++) {
                ModelPickCandidate candidate = candidates[i];
                RoModelSurfaceInfo candidateInfo = candidate.Info;
                builder.AppendLine(
                    $"- #{i + 1} distance={candidate.Distance:0.###} direct={candidate.DirectBoundsHit} instance={candidateInfo.InstanceIndex} model='{candidateInfo.ModelFile}' node='{candidateInfo.NodeName}' texture='{candidateInfo.TextureName}' mode={candidateInfo.MaterialMode} bounds={FormatVector(candidate.Renderer.bounds.size)}");
            }

            return builder.ToString();
        }

        private static string BuildPanelSummary(ModelPickCandidate best, int shown, int total, string mode) {
            RoModelSurfaceInfo info = best.Info;
            return
                $"Copied best pick ({mode})\n" +
                $"model: {info.ModelFile}\n" +
                $"node: {info.NodeName}\n" +
                $"texture: {info.TextureName}\n" +
                $"instance: {info.InstanceIndex}  mode: {info.MaterialMode}  twoSided: {info.TwoSided}\n" +
                $"candidates shown: {shown}/{total}";
        }

        private void SetLastFailure(string message) {
            lastPickSummary = message;
            lastPickClipboard = string.Empty;
            Debug.LogWarning($"RO map model picker: {message}", this);
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

        private static bool TryGetPointerScreenPosition(Vector2 guiMousePosition, out Vector2 position) {
            if (Mouse.current != null) {
                position = Mouse.current.position.ReadValue();
                return true;
            }

            position = new Vector2(guiMousePosition.x, Screen.height - guiMousePosition.y);
            return true;
        }

        private static UnityEngine.Camera ResolveCamera() {
            UnityEngine.Camera main = UnityEngine.Camera.main;
            if (main != null && main.isActiveAndEnabled) {
                return main;
            }

            UnityEngine.Camera[] cameras = UnityEngine.Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++) {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled) {
                    return cameras[i];
                }
            }

            return null;
        }

        private bool UpdatePanelPointerCapture(Event current) {
            bool pointerOverPanel = PanelRect.Contains(current.mousePosition);
            if (IsPointerPositionEvent(current)) {
                SetPanelPointerCapture(true);
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

        private static bool HasToggleModifiers(EventModifiers modifiers) =>
            (modifiers & ToggleModifiers) == ToggleModifiers;

        private static bool IsPointerPositionEvent(Event current) =>
            current.type == EventType.MouseMove ||
            current.type == EventType.MouseDown ||
            current.type == EventType.MouseUp ||
            current.type == EventType.MouseDrag;

        private static bool IsConsumablePointerEvent(Event current) =>
            current.type == EventType.MouseDown ||
            current.type == EventType.MouseUp ||
            current.type == EventType.MouseDrag ||
            current.type == EventType.ScrollWheel;

        private static string GetFileName(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            return Path.GetFileName(path.Replace('\\', '/'));
        }

        private static Mesh ResolveSharedMesh(MeshRenderer renderer) {
            if (renderer == null) {
                return null;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static string FormatMaterialFloat(Material material, string property) {
            if (material == null || !material.HasProperty(property)) {
                return "<missing>";
            }

            return material.GetFloat(property).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string BuildMeshColorSummary(Mesh mesh) {
            if (mesh == null) {
                return "<missing mesh>";
            }

            Color32[] colors = mesh.colors32;
            if (colors == null || colors.Length == 0) {
                return "none";
            }

            byte maxWeight = 0;
            byte maxPhase = 0;
            for (int i = 0; i < colors.Length; i++) {
                if (colors[i].r > maxWeight) {
                    maxWeight = colors[i].r;
                }

                if (colors[i].g > maxPhase) {
                    maxPhase = colors[i].g;
                }
            }

            return $"{colors.Length} colors, maxWeight={maxWeight}/255, maxPhase={maxPhase}/255";
        }

        private static string BuildTransformPath(Transform transform) {
            if (transform == null) {
                return string.Empty;
            }

            var names = new List<string>();
            Transform current = transform;
            while (current != null) {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string FormatVector(Vector3 value) =>
            $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";

        private static string EscapeJson(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private void EnsureStyles() {
            if (panelStyle != null) {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box) {
                padding = new RectOffset(8, 8, 8, 8),
                normal = { textColor = Color.white }
            };
            panelStyle.normal.background = MakeTexture(new Color(0f, 0f, 0f, 0.78f));

            labelStyle = new GUIStyle(GUI.skin.label) {
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            smallLabelStyle = new GUIStyle(GUI.skin.label) {
                normal = { textColor = Color.white },
                wordWrap = true
            };
        }

        private static Texture2D MakeTexture(Color color) {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void DestroyAllOverlayInstances(bool forceImmediate) {
            RoMapModelPickerOverlay[] overlays = FindObjectsByType<RoMapModelPickerOverlay>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < overlays.Length; i++) {
                DestroyOverlay(overlays[i], forceImmediate);
            }
        }

        private static void DestroyOverlay(RoMapModelPickerOverlay overlay, bool forceImmediate) {
            if (overlay == null) {
                return;
            }

            GameObject target = overlay.gameObject;
            if (target == null) {
                return;
            }

            if (forceImmediate || !Application.isPlaying) {
                DestroyImmediate(target);
            } else {
                Destroy(target);
            }
        }

        private readonly struct ModelPickCandidate {
            public ModelPickCandidate(RoModelSurfaceInfo info, MeshRenderer renderer, float distance, bool directBoundsHit) {
                Info = info;
                Renderer = renderer;
                Distance = distance;
                DirectBoundsHit = directBoundsHit;
            }

            public RoModelSurfaceInfo Info { get; }
            public MeshRenderer Renderer { get; }
            public float Distance { get; }
            public bool DirectBoundsHit { get; }
        }
    }
}
