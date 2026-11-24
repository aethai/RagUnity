using Ragnarok.Data;
using Ragnarok.UI.Classic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// The classic UI's per-frame driver next to the shell: the gameplay keys that are not a window's own (Esc, the
    /// shortcut bar keys) and the data ticks of the classic windows that follow the session state. Runs before the
    /// other UI scripts so a key is handled once.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RoClassicGameUi : MonoBehaviour {
        private static readonly ProfilerMarker TicksMarker = new ProfilerMarker("RO.UI.Classic.Ticks");
        // each data tick under its own marker (gcprobe / cpuprobe attribute by them)
        private static readonly ProfilerMarker[] TickMarkers = {
            new ProfilerMarker("RO.UI.Classic.Tick.ChatRoom"), new ProfilerMarker("RO.UI.Classic.Tick.BuyingStore"),
            new ProfilerMarker("RO.UI.Classic.Tick.Notify"), new ProfilerMarker("RO.UI.Classic.Tick.Repair"),
            new ProfilerMarker("RO.UI.Classic.Tick.Spells"), new ProfilerMarker("RO.UI.Classic.Tick.Merge"),
            new ProfilerMarker("RO.UI.Classic.Tick.Pet"), new ProfilerMarker("RO.UI.Classic.Tick.Companion"),
            new ProfilerMarker("RO.UI.Classic.Tick.Guild"), new ProfilerMarker("RO.UI.Classic.Tick.Rodex"),
            new ProfilerMarker("RO.UI.Classic.Tick.PetTaming"), new ProfilerMarker("RO.UI.Classic.Tick.Quest"),
            new ProfilerMarker("RO.UI.Classic.Tick.QuestTracker"), new ProfilerMarker("RO.UI.Classic.Tick.ViewEquip"),
        };

        private RoMinimalLoginShell shell;

        public static RoClassicGameUi Instance { get; private set; }

        /// <summary>The shell it drives (the chat's /sit and /stand go through its sit / stand requests).</summary>
        internal RoMinimalLoginShell Shell => shell;

        public static RoClassicGameUi Ensure(RoMinimalLoginShell owner) {
            RoClassicGameUi ui = owner.GetComponent<RoClassicGameUi>();
            if (ui == null) ui = owner.gameObject.AddComponent<RoClassicGameUi>();
            ui.shell = owner;
            Instance = ui;
            return ui;
        }

        private void OnDestroy() {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        private void Update() {
            RoClassicCommandFlags.EnsureLoaded();   // the slash-command flags kept from the last run reach their systems
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) TryHandleEscape();

            RoLoginShellSession session = shell != null ? shell.ClassicSession : null;
            RoClassicChat chat = shell != null ? shell.ClassicChat : null;
            if (session == null || chat == null) return; // the gameplay UI is up from the map entry to leaving it

            if (keyboard != null) {
                RoClassicHeldSkill.KeyDowns(keyboard, session);   // 0x9d60a0, before the key's own use
                HandleShortcutBarKeys(keyboard);
                RoClassicHeldSkill.KeyUps(keyboard, session);     // WM_KEYUP, after it
            }

            Tick(session, chat, shell.GameplayItemMetadata);
        }

        private void OnApplicationFocus(bool hasFocus) {
            if (!hasFocus) RoClassicHeldSkill.FocusLost();   // WM_ACTIVATE 0xd44890
        }

        /// <summary>
        /// Esc (0x9d3830): a text field keeps it; else it cancels the targeting, closes the top classic window, and only
        /// when nothing closed toggles the Esc menu (or the death menu).
        /// </summary>
        public bool TryHandleEscape() {
            if (RoClassicTextInput.CapturesKeyPress()) return false;

            RoSkillTargetSelection targeting = RoSkillTargetSelection.Current;
            if (targeting != null && targeting.HasActiveSelection) {
                targeting.Cancel();
                return true;
            }

            RoClassicUiSystem classicUi = RoClassicUiSystem.Instance;
            if (classicUi != null && classicUi.Windows.HandleEscape()) return true;
            return shell != null && shell.TryHandleGameplayEscapeFromUiRoot();
        }

        /// <summary>The shortcut bar keys (F1-F9 and the extra rows by their bindings, F12 cycles the rows).</summary>
        private void HandleShortcutBarKeys(Keyboard keyboard) {
            if (RoClassicTextInput.CapturesKeyPress() || RoClassicShortcutSettings.ShouldSuppressGameplayHotkeys) return;
            if (keyboard.f12Key.wasPressedThisFrame) {
                RoClassicHotkeyBar.CycleVisibleRowsFromKey();
                return;
            }

            RoClassicHotkeyBar.TryUsePressedHotkey(keyboard);
        }

        private static void Tick(RoLoginShellSession session, RoClassicChat chat, RoItemMetadataTable metadata) {
            using (TicksMarker.Auto()) {
                RoClassicChat.Tick();
                using (TickMarkers[0].Auto()) RoClassicChatRoom.Tick(session, chat);
                using (TickMarkers[1].Auto()) RoClassicBuyingStore.Tick(session, chat, metadata);
                using (TickMarkers[2].Auto()) RoClassicNotify.Tick(session, chat);
                using (TickMarkers[3].Auto()) RoClassicRepair.Tick(session, chat, metadata);
                using (TickMarkers[4].Auto()) RoClassicItemSelect.TickSpells(session);
                using (TickMarkers[5].Auto()) RoClassicMerge.Tick(session, chat, metadata);
                using (TickMarkers[6].Auto()) RoClassicPet.Tick(session, chat, metadata);
                using (TickMarkers[7].Auto()) RoClassicCompanion.Tick(session, chat);
                using (TickMarkers[8].Auto()) RoClassicGuild.Tick(session, chat);
                using (TickMarkers[9].Auto()) RoClassicRodex.Tick(session, chat, metadata);
                using (TickMarkers[10].Auto()) RoClassicPetTaming.Tick(session);
                using (TickMarkers[11].Auto()) RoClassicQuest.Tick(session, metadata);
                using (TickMarkers[12].Auto()) RoClassicQuestTracker.Tick(session);
                using (TickMarkers[13].Auto()) RoClassicViewEquip.Tick(session, metadata);
                RoClassicNavigation.Bind(session, chat);
                RoClassicPvpOverlay.Tick();
                RoClassicStatusIcons.Tick(session, chat, metadata);
                RoClassicSignBoards.Tick();
                RoClassicSkillLevel.Tick();
                RoClassicBank.Tick(session, chat);
                RoClassicAttendance.Tick(session, chat, metadata);
                RoClassicPreviewEquip.Bind(session);
                RoClassicAchievements.Tick(session);
                RoClassicEntryQueue.Tick(session, chat);
                RoClassicSiegeEmblems.Tick();
                RoClassicBattlefieldMarks.Tick();
                RoClassicMemorial.Tick(session);
                RoEquipProperties.Tick(session);
                RoClassicTipBox.Tick(session);
                RoClassicBook.Tick();
                RoClassicPrompts.Tick();
                RoClassicStatus.Tick(session);
                RoClassicSkillTree.Tick(session);
                RoClassicPartyFriends.Tick(session, chat);
                RoClassicInventory.Tick(session, metadata);
                RoClassicEquipment.Tick(session, metadata);
                RoClassicMinimap.Tick();
                RoClassicGameSettings.Tick();
                RoClassicShortcutSettings.Tick();
                RoClassicCart.Tick(session, metadata);
                RoClassicStorage.Tick(session, metadata);
                RoClassicTrade.Tick(session, chat, metadata);
                RoClassicNpcMarket.Tick(session, chat, metadata);
                RoClassicNpcDialog.Tick(session, metadata);
                RoClassicNpcShop.Tick(session, chat, metadata);
                RoClassicHotkeyBar.Tick();
                RoClassicBasicInfo.Tick(session);
            }
        }
    }
}
