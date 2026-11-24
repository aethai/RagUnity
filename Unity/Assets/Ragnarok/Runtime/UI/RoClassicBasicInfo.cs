using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Gameplay;
using Ragnarok.Maps;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Basic Info, the original client's UIBasicInfoWnd, drawn from the layouts exported by the oracle
    /// (Resources/Ragnarok/Generated/OriginalClient/ui/UIBasicInfoWnd.{maximized,minimized}.json), with the round-icon
    /// menu UIMenuIconWnd docked under it (RoClassicBasicInfo.Menu.cs). The data is the session's basic info (the
    /// character, ZC_PAR_CHANGE / ZC_LONGPAR_CHANGE ...) with the own actor's HP from the online world; the shell binds the
    /// window on every map entry and RoClassicGameUi re-reads it when the session's basic info sequence or the own
    /// actor's vitals move.
    /// </summary>
    public sealed partial class RoClassicBasicInfo {
        private const string ClassicLayoutRoot = "Ragnarok/Generated/OriginalClient/ui/";
        private const int ClassicMinimizeButtonId = 198; // sys_mini_off/on button of UIBasicInfoWnd
        private const string ClassicZenySlot = "Zeny";
        private const string ClassicWeightSlot = "MaxWeight+Weight";
        private const string ClassicBaseExpSlot = "BaseExp+BaseExpNext";
        private const string ClassicJobExpSlot = "JobExpNext+JobExp";
        private const string ClassicBaseExpBar = "+0xd0"; // UIINT64BarGraph members of UIBasicInfoWnd
        private const string ClassicJobExpBar = "+0xd4";
        private const string DefaultDisplayName = "Basic Info";
        private static readonly Color32 ClassicTextBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 ClassicTextRed = new Color32(255, 0, 0, 255);

        /// <summary>
        /// Weight turns red from this percentage (client global 0x15556ac, replaced by ZC 0x0ADE when the server
        /// sends one; 50 is the classic overweight limit).
        /// </summary>
        public static int ClassicOverweightPercent = 50;

        private static RoClassicBasicInfo instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            classicMenuExpanded = false;
        }

        private RoLoginShellSession session;
        private RoOnlineWorldController worldController;
        private IRoBasicInfoIconHost iconHost;
        private RoJobMetadataTable jobMetadata = RoJobMetadataTable.Empty;
        private bool jobMetadataLookupAttempted;
        private RoClassicWindow classicWindow;
        private RoClassicWindowLayout classicMaximized;
        private RoClassicWindowLayout classicMinimized;
        private bool classicMinimizedState;
        private bool classicLayoutsMissing;
        private bool applied;
        private bool vitalsChanged;
        private int seenSequence = int.MinValue;

        /// <summary>Bound since the map entry (until <see cref="Close"/>).</summary>
        public static bool IsBound => instance != null;

        /// <summary>
        /// The shell on a map entry: the session, the online world (the own actor's HP) and the shell as the menu icons'
        /// host; the window is shown and redrawn at once.
        /// </summary>
        public static void Bind(RoLoginShellSession loginSession, RoOnlineWorldController onlineWorld, IRoBasicInfoIconHost host) {
            if (instance == null) instance = new RoClassicBasicInfo();
            RoClassicBasicInfo b = instance;
            b.session = loginSession;
            b.SubscribeToWorldController(onlineWorld);
            if (host != null) b.iconHost = host;
            // a fresh bind may come with a new runtime map: the job table is looked up again
            b.jobMetadataLookupAttempted = false;
            b.Refresh();
        }

        /// <summary>Per frame (RoClassicGameUi): the window again when the basic info sequence or the own actor's vitals moved.</summary>
        public static void Tick(RoLoginShellSession loginSession) {
            RoClassicBasicInfo b = instance;
            if (b == null || loginSession == null) return;
            if (!ReferenceEquals(b.session, loginSession)) {
                b.session = loginSession;
                b.seenSequence = int.MinValue;
            }

            if (b.vitalsChanged || loginSession.BasicInfoChangeSequence != b.seenSequence) b.Refresh();
            else b.TickMenuBadges();
        }

        /// <summary>The shell after world drains that touch the own actor's HP (ZC_NOTIFY_ACT, ZC_HP_INFO, party HP).</summary>
        public static void Invalidate() {
            if (instance != null) instance.vitalsChanged = true;
        }

        /// <summary>Msg (6, 0xda)'s 0x8f99c0(+0x25) (F11): Basic Info at its minimized size, whatever it was.</summary>
        public static void Collapse() {
            RoClassicBasicInfo b = instance;
            if (b == null || b.classicWindow == null || b.classicMinimized == null || b.classicMinimizedState) return;
            b.classicMinimizedState = true;
            b.classicWindow.SetLayout(b.classicMinimized);
            b.DockClassicMenu();
        }

        /// <summary>Leaving the map (character select, shell off): Basic Info and its menu go, the next entry starts afresh.</summary>
        public static void Close() {
            RoClassicBasicInfo b = instance;
            if (b == null) return;
            instance = null;
            b.SubscribeToWorldController(null);
            b.classicMenu?.Destroy();
            b.classicMenu = null;
            b.classicWindow?.Destroy();
            b.classicWindow = null;
        }

        private void SubscribeToWorldController(RoOnlineWorldController next) {
            if (ReferenceEquals(worldController, next)) return;
            if (worldController != null) worldController.OwnActorVitalsChanged -= OnOwnActorVitalsChanged;
            worldController = next;
            if (worldController != null) worldController.OwnActorVitalsChanged += OnOwnActorVitalsChanged;
        }

        private void OnOwnActorVitalsChanged(RoOwnActorVitalsSnapshot snapshot) {
            vitalsChanged = true;
        }

        private void Refresh() {
            vitalsChanged = false;
            seenSequence = session != null ? session.BasicInfoChangeSequence : 0;
            RoRuntimePerfCounters.IncrementUiRefresh("BasicInfo");
            RoShellBasicInfoSnapshot basicInfo = session != null ? session.BasicInfoSnapshot() : default;
            RoPlayerStatusSnapshot status = basicInfo.Status ?? RoPlayerStatusSnapshot.Empty;
            // nothing known yet (no character, no status): the window keeps what it shows
            if (!basicInfo.HasCharacter && !HasRenderableGameplayStatus(status) && applied) return;
            try {
                Apply(basicInfo, status);
                applied = true;
            } catch (System.Exception e) {
                classicLayoutsMissing = true;
                Debug.LogException(e);
            }
        }

        private void Apply(RoShellBasicInfoSnapshot basicInfo, RoPlayerStatusSnapshot status) {
            if (!EnsureClassicWindow()) {
                return;
            }

            RoLoginShellCharacter character = basicInfo.Character;
            int hp = character.Hp, maxHp = character.MaxHp, sp = character.Sp, maxSp = character.MaxSp;
            if (status.MaxHp > 0) {
                hp = status.Hp;
                maxHp = status.MaxHp;
            }

            if (status.MaxSp > 0) {
                sp = status.Sp;
                maxSp = status.MaxSp;
            }

            // the own actor's HP from the online world wins over the session's (it follows ZC_NOTIFY_ACT / ZC_HP_INFO)
            RoOnlineWorldController controller = worldController;
            if (controller != null && controller.OwnActorId != 0 &&
                controller.State.TryGetActor(controller.OwnActorId, out RoOnlineActorSnapshot ownActor) && ownActor.MaxHp > 0) {
                hp = ownActor.Hp;
                maxHp = ownActor.MaxHp;
            }

            int baseLevel = status.BaseLevel > 0 ? status.BaseLevel : character.BaseLevel;
            int jobLevel = status.JobLevel > 0 ? status.JobLevel : character.JobLevel;
            int jobId = status.HasKnownJobId ? status.JobId : character.JobId;
            jobMetadata = ResolveJobMetadata(controller, jobMetadata, ref jobMetadataLookupAttempted);

            RoClassicWindow w = classicWindow;
            w.Set("Name", !string.IsNullOrWhiteSpace(character.Name) ? character.Name : DefaultDisplayName);
            w.Set("JobName", ResolveJobName(jobId));
            w.Set("BaseLevel", baseLevel);
            w.Set("JobLevel", jobLevel);
            w.Set("Hp", hp);
            w.Set("MaxHp", maxHp);
            w.Set("Sp", sp);
            w.Set("MaxSp", maxSp);
            w.Set("HpPercent", maxHp > 0 ? (long) hp * 100 / maxHp : 0);
            w.Set("SpPercent", maxSp > 0 ? (long) sp * 100 / maxSp : 0);
            w.Set("BaseExp", status.BaseExperience);
            w.Set("BaseExpNext", status.NextBaseExperience);
            w.Set("JobExp", status.JobExperience);
            w.Set("JobExpNext", status.NextJobExperience);
            w.Set("BaseExpPercent", MinimizedExpPercent(status.BaseExperience, status.NextBaseExperience).ToString("0.0", CultureInfo.InvariantCulture));
            w.Set("Zeny", status.Zeny);
            // the server counts weight in tenths (a Knife is 400); the window shows whole units, as the cart does
            w.Set("Weight", status.Weight / 10);
            w.Set("MaxWeight", status.MaxWeight / 10);
            // FUN_008f99c0 parks an exp bar off-window at the job's max level (the server's next exp is 0 there)
            bool baseBar = status.NextBaseExperience > 0, jobBar = status.NextJobExperience > 0;
            w.SetChildPosition(ClassicBaseExpBar, baseBar ? 0x55 : -200, baseBar ? 13 + 0x4c : 0x4e);
            w.SetChildPosition(ClassicJobExpBar, jobBar ? 0x55 : -200, jobBar ? 13 + 0x58 : 0x58);
            // Custom's patch at 0x233d7d0 draws the exp numbers only when the exp or the next exp is non-zero
            w.SetSlotVisible(ClassicBaseExpSlot, status.BaseExperience != 0 || status.NextBaseExperience != 0);
            w.SetSlotVisible(ClassicJobExpSlot, status.JobExperience != 0 || status.NextJobExperience != 0);
            // FUN_008fa902: zeny right-aligned at w − 5, weight right-aligned 10 px left of the zeny text
            w.SetSlotX(ClassicWeightSlot, w.Width - 5 - w.MeasureSlot(ClassicZenySlot) - 5);
            long maxWeight = status.MaxWeight < 1 ? 1 : status.MaxWeight;
            w.SetSlotColor(ClassicWeightSlot, status.Weight * 100L / maxWeight >= ClassicOverweightPercent ? ClassicTextRed : ClassicTextBlack);
            w.Visible = true;
            classicMenu.Visible = true;
            DockClassicMenu();
            UpdateClassicMenuBadges();
        }

        private bool EnsureClassicWindow() {
            if (classicWindow != null) return true;
            if (classicLayoutsMissing) return false;
            classicMaximized = LoadClassicLayout("UIBasicInfoWnd.maximized");
            classicMinimized = LoadClassicLayout("UIBasicInfoWnd.minimized");
            if (classicMaximized == null) {
                classicLayoutsMissing = true;
                Debug.LogWarning("[ClassicUi] UIBasicInfoWnd layout not found under Resources/" + ClassicLayoutRoot);
                return false;
            }

            RoClassicUiSystem system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
            classicWindow = new RoClassicWindow(system, classicMaximized);
            classicWindow.Clicked += OnClassicClicked;
            classicWindow.Moved += _ => DockClassicMenu(); // the menu follows Basic Info (OnMouseMove 0x8face0)
            EnsureClassicMenu(system);
            return true;
        }

        private void OnClassicClicked(RoClassicChildLayout child) {
            if (child.Id == ClassicMinimizeButtonId && classicMinimized != null) {
                classicMinimizedState = !classicMinimizedState;
                classicWindow.SetLayout(classicMinimizedState ? classicMinimized : classicMaximized);
                DockClassicMenu();
            }
        }

        private static RoClassicWindowLayout LoadClassicLayout(string name) {
            var asset = Resources.Load<TextAsset>(ClassicLayoutRoot + name);
            if (asset == null) return null;
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
            // the exp bars read the session globals directly (no exported fields): bind them by owner member
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Owner == ClassicBaseExpBar) BindBar(child, "BaseExp", "BaseExpNext");
                else if (child.Owner == ClassicJobExpBar) BindBar(child, "JobExp", "JobExpNext");
            }

            return layout;
        }

        private static void BindBar(RoClassicChildLayout child, string value, string max) {
            child.Fields["value"] = value;
            child.Fields["max"] = max;
        }

        private static bool HasRenderableGameplayStatus(RoPlayerStatusSnapshot gameplayStatus) {
            return gameplayStatus != null &&
                (gameplayStatus.BaseLevel > 0 ||
                    gameplayStatus.JobLevel > 0 ||
                    gameplayStatus.HasKnownJobId ||
                    gameplayStatus.MaxHp > 0 ||
                    gameplayStatus.MaxSp > 0);
        }

        /// <summary>
        /// The minimized window's "Exp. %.1lf %%" (0x8fa364): exp / next · 1000 cut to an int, times 0.1f as a float, so
        /// 49.37 % shows 49.3; at the job's max base level 100.0. The client takes a next exp &lt;= 0 as exp + 1 and then
        /// compares the level with its per-job max table; the server sends next 0 only at that max level, so it is 100 (I).
        /// </summary>
        public static double MinimizedExpPercent(long exp, long next) {
            if (next <= 0) return 100.0;
            int tenths = (int) ((double) exp / next * 1000.0);
            return (double) (float) ((float) tenths * 0.1f);
        }

        private string ResolveJobName(int jobId) {
            if (jobMetadata != null && jobMetadata.TryGetJob(jobId, out RoJobMetadata job) && job != null) {
                string formatted = RoJobMetadata.FormatJobName(job.ConstantName);
                if (!string.IsNullOrEmpty(formatted)) {
                    return formatted;
                }
            }

            string defaultName = RoJobMetadataTable.DefaultName(jobId);
            if (!string.IsNullOrEmpty(defaultName)) {
                return defaultName;
            }

            return "Job " + jobId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The job table of the runtime map's content (looked up in the scene once per bind while it is missing).</summary>
        private static RoJobMetadataTable ResolveJobMetadata(
            RoOnlineWorldController controller,
            RoJobMetadataTable current,
            ref bool lookupAttempted
        ) {
            if (current != null && current.IsLoaded) {
                return current;
            }

            RoRuntimeMapController runtimeMap = controller != null
                ? controller.GetComponentInParent<RoRuntimeMapController>()
                : null;
            if (runtimeMap == null && !lookupAttempted) {
                lookupAttempted = true;
                runtimeMap = Object.FindFirstObjectByType<RoRuntimeMapController>();
            }

            RoJobMetadataTable resolved = runtimeMap?.Content?.Assets?.GameData?.Jobs;
            return resolved ?? current ?? RoJobMetadataTable.Empty;
        }
    }
}
