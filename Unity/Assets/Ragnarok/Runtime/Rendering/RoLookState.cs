using System;

namespace Ragnarok.Rendering {
    /// <summary>
    /// The seam between the game and the look rig (Ragnarok.Look, which references URP; this assembly does not):
    /// the rig registers its console handler here, diagnostics call it (!lookset).
    /// </summary>
    public static class RoLookState {
        /// <summary>!lookset &lt;args&gt;: set by the look rig; returns what it did.</summary>
        public static Func<string, string> Tune;

        /// <summary>The rig's current values, one line (for logs and captures).</summary>
        public static Func<string> Describe;
    }
}
