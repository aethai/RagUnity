namespace Ragnarok.UI {
    /// <summary><see cref="IRoBasicInfoIconHost"/> for the shell: the menu icons open the classic windows themselves.</summary>
    public sealed partial class RoMinimalLoginShell {
        void IRoBasicInfoIconHost.ToggleGameOptions() {
            ShowGameOptionsWindow();
        }
    }
}
