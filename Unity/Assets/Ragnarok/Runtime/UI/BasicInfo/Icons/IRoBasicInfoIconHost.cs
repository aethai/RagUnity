namespace Ragnarok.UI {
    /// <summary>What the menu icons need from the shell beyond the classic windows' own static APIs.</summary>
    public interface IRoBasicInfoIconHost {
        /// <summary>The Option icon: the Esc menu (as Esc would open it).</summary>
        void ToggleGameOptions();
    }
}
