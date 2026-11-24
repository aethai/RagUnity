using Ragnarok.World;

namespace Ragnarok.UI {
    /// <summary>
    /// Snapshot of host state passed to every <see cref="IRoBasicInfoIcon"/>
    /// when its menu button is clicked. Read-only and by-ref so icons can
    /// inspect session/world without copying.
    /// </summary>
    public readonly struct RoBasicInfoIconContext {
        public RoBasicInfoIconContext(
            RoLoginShellSession session,
            RoOnlineWorldController worldController,
            IRoBasicInfoIconHost host
        ) {
            Session = session;
            WorldController = worldController;
            Host = host;
        }

        public RoLoginShellSession Session { get; }
        public RoOnlineWorldController WorldController { get; }
        public IRoBasicInfoIconHost Host { get; }
    }
}
