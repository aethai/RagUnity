using UnityEngine;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// The build's identity at the top of every Player.log (and so in Unity's crash reports, which carry the log): product,
    /// version (Application.version, set by the build runner: the release version or the project version plus the commit),
    /// release or development, and the platform. A support request or a crash folder then says which build it came from.
    /// </summary>
    public static class RoBuildInfo {
        public static string Describe() =>
            Application.productName + " " + Application.version + " (" + (Debug.isDebugBuild ? "development" : "release") + ", Unity " +
            Application.unityVersion + ", " + SystemInfo.operatingSystem + ", " + SystemInfo.graphicsDeviceName + ")";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LogAtStart() {
            if (Application.isEditor) return;
            Debug.Log("[Build] " + Describe());
        }
    }
}
