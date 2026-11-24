using UnityEngine;

namespace Ragnarok {
    /// <summary>
    /// GetComponent without the Editor's cost for a missing component: GetComponent&lt;T&gt;() returning nothing builds a
    /// "missing component" placeholder with its message in the Editor (~0.8 KB each), which per-frame actor lookups turned
    /// into tens of KB of garbage a frame. TryGetComponent allocates nothing either way.
    /// </summary>
    public static class RoComponentLookup {
        public static T FindComponent<T>(this GameObject gameObject) where T : Component =>
            gameObject != null && gameObject.TryGetComponent(out T component) ? component : null;

        public static T FindComponent<T>(this Component owner) where T : Component =>
            owner != null && owner.TryGetComponent(out T component) ? component : null;
    }
}
