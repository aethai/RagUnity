using Ragnarok.Camera;
using Ragnarok.Maps;
using Ragnarok.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ragnarok.Editor {
    public static class RoRuntimeMapSceneBuilder {
        private const string ScenePath = "Assets/Scenes/RORuntimeMap.unity";

        [MenuItem("Ragnarok/Game/Create Runtime Map Scene")]
        public static void CreateScene() {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RORuntimeMap";

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = RoCameraController.ClassicGravityNearPlane;
            camera.farClipPlane = RoCameraController.ClassicGravityFarPlane;
            camera.fieldOfView = RoCameraController.ClassicGravityFieldOfView;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<RoCameraController>().ApplyClassicGravityPreset();

            var runtimeObject = new GameObject("RO Runtime Map");
            runtimeObject.AddComponent<RoRuntimeMapController>();

            var shellObject = new GameObject("RO Minimal Login Shell");
            shellObject.AddComponent<RoMinimalLoginShell>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = runtimeObject;
            EditorGUIUtility.PingObject(runtimeObject);
            Debug.Log($"Created RO runtime map scene at {ScenePath}. The scene loads the saved content profile, prontera.rsw, and the live login shell on Play.");
        }
    }
}
