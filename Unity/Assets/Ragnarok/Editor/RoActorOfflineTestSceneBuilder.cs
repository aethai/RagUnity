using Ragnarok.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ragnarok.Editor {
    public static class RoActorOfflineTestSceneBuilder {
        private const string ScenePath = "Assets/Scenes/ROActorOfflineTest.unity";

        [MenuItem("Ragnarok/Actor/Create Offline Actor Test Scene")]
        public static void CreateScene() {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "ROActorOfflineTest";

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.085f, 0.09f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 1.8f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            cameraObject.transform.position = new Vector3(0f, 0.9f, -6f);
            cameraObject.transform.rotation = Quaternion.identity;

            var rigObject = new GameObject("RO Offline Actor Test Rig");
            var rig = rigObject.AddComponent<RoActorOfflineTestRig>();
            var serializedRig = new SerializedObject(rig);
            serializedRig.FindProperty("spawnOnStart").boolValue = true;
            serializedRig.FindProperty("autoPopulateDefaultPcLayers").boolValue = true;
            serializedRig.FindProperty("actorLocalOffset").vector3Value = new Vector3(0f, 0.36f, 0f);
            serializedRig.FindProperty("showDiagnostics").boolValue = true;
            serializedRig.FindProperty("frameCameraOnSpawn").boolValue = true;
            SerializedProperty layers = serializedRig.FindProperty("layerSpecs");
            layers.arraySize = 0;
            serializedRig.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = rigObject;
            EditorGUIUtility.PingObject(rigObject);
            Debug.Log($"Created RO offline actor test scene at {ScenePath}. The selected rig auto-populates default PC body/head/hair layers from the active content profile on Play.");
        }
    }
}
