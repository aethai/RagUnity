using Ragnarok.UI;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    public static class RoMinimalLoginShellSceneBuilder {
        [MenuItem("Ragnarok/Network/Create Minimal Login Shell")]
        public static void CreateShellObject() {
            var shellObject = new GameObject("RO Minimal Login Shell");
            shellObject.AddComponent<RoMinimalLoginShell>();
            Selection.activeGameObject = shellObject;
            EditorGUIUtility.PingObject(shellObject);
            Debug.Log("Created RO Minimal Login Shell object. Press Play to log in through the classic pre-game screens.");
        }
    }
}
