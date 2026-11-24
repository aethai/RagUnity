using System.Runtime.CompilerServices;
using MoonSharp.Interpreter;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// MoonSharp's static setup picks its default script loader on first use, and on Unity that loader reads Resources,
    /// which works on the main thread only. The client's lua tables (equipment properties, tip box) load on worker
    /// threads, so the setup runs here first, on the main thread ("Error initializing UnityScriptLoader" otherwise).
    /// </summary>
    internal static class RoLuaStartup {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnMainThread() {
            RuntimeHelpers.RunClassConstructor(typeof(Script).TypeHandle);
        }
    }
}
