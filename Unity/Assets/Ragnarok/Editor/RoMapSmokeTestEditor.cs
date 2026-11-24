using Ragnarok.Assets;
using Ragnarok.Content;
using Ragnarok.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ragnarok.Editor {
    [CustomEditor(typeof(RoMapSmokeTest))]
    internal sealed class RoMapSmokeTestEditor : UnityEditor.Editor {
        private void OnSceneGUI() {
            Event current = Event.current;
            if (current == null) {
                return;
            }

            SceneView sceneView = SceneView.currentDrawingSceneView;
            if (sceneView != null) {
                sceneView.wantsMouseMove = true;
            }

            var smokeTest = (RoMapSmokeTest) target;
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (current.type == EventType.MouseMove ||
                current.type == EventType.MouseDrag ||
                current.type == EventType.MouseDown ||
                current.type == EventType.Repaint) {
                smokeTest.RecordHoverCellFromEditorRay(ray);
                Repaint();
            }

            if (current.type != EventType.KeyDown) {
                return;
            }

            if (current.keyCode == KeyCode.F6) {
                Undo.RecordObject(smokeTest, "Set RO spawn cell");
                smokeTest.UseSpawnCellFromEditorRay(ray, "selected Scene view");
                MarkDirty(smokeTest);
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.F7) {
                smokeTest.PrintGroundTileFromEditorRay(ray);
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.F8) {
                smokeTest.PrintRendererFromEditorRay(ray);
                current.Use();
            }
        }

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            var smokeTest = (RoMapSmokeTest) target;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("RO Spawn Tools", EditorStyles.boldLabel);
            string lastHoverCell = smokeTest.HasLastHoverCellForEditor
                ? smokeTest.LastHoverCellForEditor.ToString()
                : "<none>";
            EditorGUILayout.LabelField("Last Scene/Game hover cell", lastHoverCell);

            using (new EditorGUI.DisabledScope(!smokeTest.HasLastHoverCellForEditor)) {
                if (GUILayout.Button("Use Current/Last Hover Cell As Spawn")) {
                    Undo.RecordObject(smokeTest, "Use RO hover cell as spawn");
                    smokeTest.UseCurrentOrLastHoverCellAsSpawnFromEditor();
                    MarkDirty(smokeTest);
                }
            }

            if (GUILayout.Button("Use Scene View Center As Spawn")) {
                UseSceneViewCenterAsSpawn(smokeTest);
            }

            if (GUILayout.Button("Frame Scene View")) {
                FrameSceneView(smokeTest);
            }

            EditorGUILayout.HelpBox(
                "For Scene view: move the mouse over a walkable tile, then click the hover-cell button here. If function keys are swallowed by Unity/OS, use these buttons instead.",
                MessageType.Info
            );

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("RO Sprite Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Print First Sprite Pairs")) {
                smokeTest.PrintSpritePairsForEditor();
            }

            if (GUILayout.Button("Use Preferred Novice Body Sprite As Player")) {
                if (UseFirstSpritePairAsPlayer(smokeTest)) {
                    serializedObject.Update();
                }
            }

            EditorGUILayout.HelpBox(
                "Use these when you do not know a .spr/.act path. The first button prints valid base paths to Console. The second prefers the human novice body sprite, fills Player Sprite Base Path, and enables Use Sprite Player Placeholder.",
                MessageType.Info
            );
        }

        private static void UseSceneViewCenterAsSpawn(RoMapSmokeTest smokeTest) {
            SceneView sceneView = SceneView.lastActiveSceneView;
            UnityEngine.Camera camera = sceneView != null ? sceneView.camera : null;
            if (camera == null) {
                Debug.LogWarning("Cannot set spawn from Scene view center: no active Scene view camera is available.", smokeTest);
                return;
            }

            Undo.RecordObject(smokeTest, "Set RO spawn cell from Scene view center");
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            smokeTest.UseSpawnCellFromEditorRay(ray, "Scene view center");
            MarkDirty(smokeTest);
        }

        private static void FrameSceneView(RoMapSmokeTest smokeTest) {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || !smokeTest.TryCalculateCurrentMapWorldBounds(out Bounds bounds)) {
                return;
            }

            float size = Mathf.Max(bounds.size.x, bounds.size.z);
            float distance = Mathf.Clamp(size * 0.45f, 35f, 180f);
            sceneView.LookAt(
                bounds.center,
                Quaternion.Euler(45f, 1f, 0f),
                distance,
                false,
                true
            );
            sceneView.Repaint();
        }

        private static bool UseFirstSpritePairAsPlayer(RoMapSmokeTest smokeTest) {
            if (!smokeTest.TryGetDiagnosticAssets(out RoAssetDatabase assets, out RagnarokContentService temporaryContent, out string report)) {
                Debug.LogWarning(report, smokeTest);
                return false;
            }

            try {
                Undo.RecordObject(smokeTest, "Use first RO sprite pair as player");
                if (!smokeTest.TryUseFirstSpritePairAsPlayer(assets, out string basePath)) {
                    Debug.LogWarning($"{report}\nNo matching RO .spr/.act pair was found in the configured content.", smokeTest);
                    return false;
                }

                MarkDirty(smokeTest);
                Debug.Log($"{report}\nPlayer sprite placeholder layers were auto-filled from '{basePath}'. Reload the map to recreate the placeholder with these paths.", smokeTest);
                return true;
            } finally {
                temporaryContent?.Dispose();
            }
        }

        private static void MarkDirty(RoMapSmokeTest smokeTest) {
            EditorUtility.SetDirty(smokeTest);
            if (!Application.isPlaying && smokeTest.gameObject.scene.IsValid()) {
                EditorSceneManager.MarkSceneDirty(smokeTest.gameObject.scene);
            }
        }
    }
}
