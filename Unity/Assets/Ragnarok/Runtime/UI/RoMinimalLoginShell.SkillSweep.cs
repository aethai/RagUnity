using System.Threading.Tasks;

namespace Ragnarok.UI {
    /// <summary>
    /// Entry points for <see cref="Ragnarok.Diagnostics.RoSkillSweepRunner"/>, the automated in-game skill sweep
    /// that logs in against the local lab server and casts every skill of a plan.
    /// </summary>
    public sealed partial class RoMinimalLoginShell {
        internal RoLoginShellStage SweepStage => session != null ? session.CommandStateSnapshot().Stage : RoLoginShellStage.Login;

        internal bool SweepIsBusy {
            get {
                if (session == null) {
                    return true;
                }

                RoShellCommandStateSnapshot state = session.CommandStateSnapshot();
                return state.IsBusy || state.IsMapTransitionPending;
            }
        }

        /// <summary>The hotkey bar, bound on demand when the map entry has not bound it yet (diagnostics console).</summary>
        internal bool SweepEnsureHotkeyBar() {
            if (!RoClassicHotkeyBar.IsBound && session != null) {
                RoClassicHotkeyBar.Bind(session, clientSettings);
            }

            return RoClassicHotkeyBar.IsBound;
        }

        internal void SweepLogin(string sweepAccount, string sweepPassword) {
            account = sweepAccount ?? string.Empty;
            password = sweepPassword ?? string.Empty;
            LoadCharactersFromPhase11Ui();
        }

        internal void SweepEnterMap(string sweepCharacter) {
            SelectCharacterFromPhase11Ui(sweepCharacter);
            EnterMapFromPhase11Ui();
        }

        /// <summary>Diagnostics: CZ_REQ_BUY_FROMMC for a seller, as a click on its board.</summary>
        internal bool SweepRequestPlayerShop(uint ownerAid) => session != null && session.BeginLegacyRequestPlayerShop(ownerAid) != null;

        internal string SweepSelectCart(string command) => RoClassicSelectCart.Sweep(command, session, ResolveGameplayUiAssetDatabase());

        internal string SweepNotify(string command) {
            Ragnarok.Assets.RoAssetDatabase assets = ResolveGameplayUiAssetDatabase();
            return RoClassicNotify.Sweep(command, assets != null && assets.IsInitialized ? assets.GameData?.Items : null);
        }

        internal bool SweepSendChat(string message) {
            if (session == null) {
                return false;
            }

            Task sent = session.BeginSendChatMessage(message);
            return sent != null;
        }

        internal bool SweepUseTargetSkill(int skillId, int skillLevel, int targetAid) {
            return session != null && session.TrySendTargetSkillRequest(skillId, skillLevel, targetAid);
        }

        internal bool SweepUseGroundSkill(int skillId, int skillLevel, int x, int y) {
            if (session == null) {
                return false;
            }

            // the path the map HUD uses; the runtime ground-skill executor is not wired on the live map
            Task sent = session.BeginLegacyUseSkillOnGround(skillId, skillLevel, x, y);
            return sent != null;
        }

        internal RoLoginShellSnapshot SweepSnapshot() {
            return session?.Snapshot();
        }

        internal bool SweepMove(int x, int y) {
            return session != null && session.TrySendMoveRequest(x, y);
        }
    }
}
