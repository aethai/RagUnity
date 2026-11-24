using System;

namespace Ragnarok.Combat {
    /// <summary>Diagnostics switches of the combat presentation (the presentation itself follows the original client).</summary>
    [Serializable]
    public sealed class RoCombatPresentationOptions {
        public bool EnableCombatTrace;
        public bool EnableCombatTraceConsoleLog;
    }
}
