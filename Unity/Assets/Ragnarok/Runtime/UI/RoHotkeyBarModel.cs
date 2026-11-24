using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Input;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.World;
using UnityEngine;
using UnityEngine.InputSystem.Controls;

namespace Ragnarok.UI {
    /// <summary>
    /// The shortcut bar's data and commands, without UI (drawn by <see cref="RoClassicHotkeyBar"/>): the server's 38
    /// slots (ZC_SHORTCUT_KEY_LIST in the session's hotkey snapshot: 4 rows of 9 + the 2 quick slots, Hercules
    /// MAX_HOTKEYS 38), a slot's use (the hotkey executor's CZ_USE_SKILL / CZ_USE_ITEM, or the skill targeting when the
    /// skill needs a target), a slot's change (CZ_SHORTCUT_KEY_CHANGE through the executor, the same entry leaving the
    /// rest of its row), the bar keys (the shortcut bindings of the client settings, F1–F9 / 1–9 / Q–O / A–L without
    /// them) and the row count cycling.
    /// </summary>
    public sealed class RoHotkeyBarModel {
        private const int SkillTargetEnemyFlag = 1;
        private const int SkillTargetPlaceFlag = 2;
        private const int SkillTargetSelfFlag = 4;
        private const int SkillTargetFriendFlag = 16;
        private const int SkillTargetTrapFlag = 32;
        private const int SkillTargetPetFlag = 64;
        private const int SkillTargetHomunFlag = 128;
        private const int MerchantIdentifySkillId = 40;
        private const int MerchantChangeCartSkillId = 154;
        private const int MerchantCartDecorateSkillId = 2544;
        private const int ThiefFindStoneSkillId = 151;
        private const float DependencyLookupRetrySeconds = 0.5f;

        public const int MaxVisibleRows = 4;
        public const int SlotsPerRow = 9;
        public const int VisibleSlotCount = MaxVisibleRows * SlotsPerRow;

        /// <summary>The first quick slot (UIQuikSlotWnd 0x9f): slots 36 and 37.</summary>
        public const int QuickSlotBase = VisibleSlotCount;

        /// <summary>The bar keys without client settings, slot by slot: F1–F9, 1–9, Q–O, A–L.</summary>
        private static readonly KeyCode[] DefaultSlotKeys = {
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F9,
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.I, KeyCode.O,
            KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L,
        };

        /// <summary>Stateless: one instance for every frame's modifier read.</summary>
        private static readonly RoUnityKeyboardInputState KeyboardInput = new RoUnityKeyboardInputState();

        private static RoOnlineWorldController cachedWorldController;
        private static RoRuntimeMapController cachedRuntimeMapController;
        private static float nextWorldControllerLookupTime;
        private static float nextRuntimeMapControllerLookupTime;

        private RoLoginShellSession session;
        private IRoHotkeyCommandExecutor commandExecutor;
        private RoClientSettingsModel settings;
        private RoSkillMetadataTable skillMetadata = RoSkillMetadataTable.Empty;
        private RoItemMetadataTable itemMetadata = RoItemMetadataTable.Empty;
        private RoAssetDatabase activeAssets;
        private RoGameplayHotkeySnapshot gameplay = RoGameplayHotkeySnapshot.Empty;
        private RoShortcutKeyEntry[] keys = Array.Empty<RoShortcutKeyEntry>();
        private int visibleRows = 1;

        private readonly struct TargetSelectionRequest {
            public TargetSelectionRequest(int skillId, int level, string displayName, RoSkillTargetFlags targetFlags) {
                SkillId = skillId;
                Level = Math.Max(1, level);
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Skill " + skillId.ToString() : displayName;
                TargetFlags = targetFlags;
            }

            public int SkillId { get; }
            public int Level { get; }
            public string DisplayName { get; }
            public RoSkillTargetFlags TargetFlags { get; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCache() {
            cachedWorldController = null;
            cachedRuntimeMapController = null;
            nextWorldControllerLookupTime = 0f;
            nextRuntimeMapControllerLookupTime = 0f;
        }

        public RoLoginShellSession Session => session;
        public RoSkillMetadataTable SkillMetadata => skillMetadata;
        public RoItemMetadataTable ItemMetadata => itemMetadata;
        public RoAssetDatabase Assets => activeAssets;

        /// <summary>The hotkey snapshot of the last <see cref="Refresh"/> (slots, learned skills, inventory).</summary>
        public RoGameplayHotkeySnapshot Gameplay => gameplay;

        /// <summary>The bar's row count, 0 (hidden) to 4.</summary>
        public int VisibleRows {
            get => visibleRows;
            set => visibleRows = Mathf.Clamp(value, 0, MaxVisibleRows);
        }

        /// <summary>The session (and its map-command executor) and the client settings holding the bar's key bindings.</summary>
        public void Bind(RoLoginShellSession loginSession, RoClientSettingsModel clientSettings) {
            Bind(loginSession, CreateLegacyExecutor(loginSession), clientSettings);
        }

        public void Bind(RoLoginShellSession loginSession, IRoHotkeyCommandExecutor commands, RoClientSettingsModel clientSettings = null) {
            session = loginSession;
            commandExecutor = commands;
            settings = clientSettings ?? settings;
            ResolveMetadata();
        }

        /// <summary>The skill / item tables of the initialized asset database; true when they changed.</summary>
        public bool ResolveMetadata() {
            RoAssetDatabase assets = ResolveInitializedAssetDatabase();
            if (ReferenceEquals(activeAssets, assets)) {
                return false;
            }

            activeAssets = assets;
            skillMetadata = activeAssets != null ? activeAssets.GameData?.Skills ?? RoSkillMetadataTable.Empty : RoSkillMetadataTable.Empty;
            itemMetadata = activeAssets != null ? activeAssets.GameData?.Items ?? RoItemMetadataTable.Empty : RoItemMetadataTable.Empty;
            return true;
        }

        /// <summary>A fresh read of the session's hotkey snapshot.</summary>
        public void Refresh() {
            gameplay = session != null ? session.HotkeySnapshot() : RoGameplayHotkeySnapshot.Empty;
            gameplay = gameplay ?? RoGameplayHotkeySnapshot.Empty;
            keys = gameplay.ShortcutKeys ?? Array.Empty<RoShortcutKeyEntry>();
        }

        /// <summary>The slot's entry as of the last <see cref="Refresh"/> (empty outside the list).</summary>
        public RoShortcutKeyEntry Key(int slot) => slot >= 0 && slot < keys.Length ? keys[slot] : default;

        public int KeyCount => keys.Length;

        /// <summary>The next row count of the bar (F12): 0 → 1 → … → 4 → 0.</summary>
        public void CycleVisibleRows() {
            visibleRows = ResolveNextVisibleRows(visibleRows);
        }

        /// <summary>A bar key: the slot's use while the session can send map commands.</summary>
        public void TryUseHotkey(int hotkeyIndex) {
            try {
                RoShellCommandStateSnapshot commandState = session != null
                    ? session.CommandStateSnapshot()
                    : default;
                if (!commandState.CanSendMapCommand) {
                    return;
                }

                UseHotkey(hotkeyIndex);
            } catch (Exception ex) {
                Debug.LogWarning($"Hotkey {FormatHotkeyLabel(hotkeyIndex)} was not sent: {ex.Message}");
            }
        }

        /// <summary>
        /// Uses a slot: a skill that needs a target starts the skill targeting at the slot's level, anything else goes to
        /// the server through the executor.
        /// </summary>
        public async void UseHotkey(int hotkeyIndex) {
            if (TryBuildTargetSelectionRequest(hotkeyIndex, out TargetSelectionRequest request)) {
                RoSkillTargetSelection.EnsureInScene().Begin(
                    session,
                    request.SkillId,
                    request.Level,
                    request.DisplayName,
                    request.TargetFlags,
                    selectableLevel: false,
                    selectableMaxLevel: request.Level);
                return;
            }

            if (commandExecutor == null) {
                return;
            }

            try {
                await commandExecutor.UseHotkeyAsync(hotkeyIndex);
            } catch (Exception ex) {
                Debug.LogWarning($"Hotkey {FormatHotkeyLabel(hotkeyIndex)} was not sent: {ex.Message}");
            }
        }

        /// <summary>A skill dropped on a bar slot, at its selected level; the same skill leaves the rest of the row.</summary>
        public Task SetSkillShortcutFromDropAsync(int hotkeyIndex, int skillId, int selectedLevel) {
            RoShortcutKeyEntry shortcut = BuildShortcutAssignment(isSkill: true, skillId, Math.Max(1, selectedLevel));
            return SetShortcutAsync(hotkeyIndex, shortcut, removeRowDuplicates: true);
        }

        /// <summary>Stores a bar slot (0–35), optionally clearing the same entry elsewhere in its row first.</summary>
        public async Task SetShortcutAsync(int hotkeyIndex, RoShortcutKeyEntry shortcut, bool removeRowDuplicates) {
            if (commandExecutor == null || hotkeyIndex < 0 || hotkeyIndex >= VisibleSlotCount) {
                return;
            }

            if (removeRowDuplicates) {
                RoGameplayHotkeySnapshot current = session != null ? session.HotkeySnapshot() : RoGameplayHotkeySnapshot.Empty;
                int[] duplicates = FindRowLocalDuplicateSlots(current.ShortcutKeys, hotkeyIndex, shortcut);
                for (int i = 0; i < duplicates.Length; i++) {
                    await commandExecutor.SetHotkeyAsync(duplicates[i], new RoShortcutKeyEntry(0, 0, 0));
                }
            }

            await commandExecutor.SetHotkeyAsync(hotkeyIndex, shortcut);
        }

        /// <summary>Stores any slot (the quick slots 36 / 37 included) as is.</summary>
        public Task SetSlotAsync(int hotkeyIndex, RoShortcutKeyEntry shortcut) {
            return commandExecutor != null ? commandExecutor.SetHotkeyAsync(hotkeyIndex, shortcut) : Task.CompletedTask;
        }

        public static RoShortcutKeyEntry BuildShortcutAssignment(bool isSkill, int id, int count) {
            if (id <= 0) {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (count < 0 || count > short.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            return new RoShortcutKeyEntry(isSkill ? (sbyte) 1 : (sbyte) 0, unchecked((uint) id), (short) count);
        }

        public static int ResolveNextVisibleRows(int currentRows) {
            return (Mathf.Clamp(currentRows, 0, MaxVisibleRows) + 1) % (MaxVisibleRows + 1);
        }

        public static int[] FindRowLocalDuplicateSlots(RoShortcutKeyEntry[] keys, int targetSlot, RoShortcutKeyEntry shortcut) {
            if (keys == null || shortcut.IsEmpty || targetSlot < 0) {
                return Array.Empty<int>();
            }

            int rowStart = targetSlot / SlotsPerRow * SlotsPerRow;
            int rowEnd = Math.Min(rowStart + SlotsPerRow, Math.Min(keys.Length, VisibleSlotCount));
            var duplicates = new List<int>();
            for (int i = rowStart; i < rowEnd; i++) {
                if (i == targetSlot) {
                    continue;
                }

                RoShortcutKeyEntry candidate = keys[i];
                if (candidate.TypeRaw == shortcut.TypeRaw && candidate.Id == shortcut.Id && candidate.Count == shortcut.Count) {
                    duplicates.Add(i);
                }
            }

            return duplicates.ToArray();
        }

        public static bool RequiresExplicitTargetSelection(
            RoShortcutKeyEntry shortcut,
            RoSkillMetadata metadata,
            bool hasSelectedTarget) {
            if (!shortcut.IsSkill || metadata == null) {
                return false;
            }

            return RequiresExplicitTargetSelection(metadata.TargetFlags);
        }

        public static RoSkillTargetType ResolveTargetTypeFromServerSkillType(int skillType) {
            return RoSkillMetadata.ToLegacyTargetType(ResolveTargetFlagsFromServerSkillType(skillType));
        }

        public static RoSkillTargetFlags ResolveTargetFlagsFromServerSkillType(int skillType) {
            var flags = RoSkillTargetFlags.None;
            if ((skillType & SkillTargetEnemyFlag) != 0) {
                flags |= RoSkillTargetFlags.Enemy;
            }

            if ((skillType & SkillTargetPlaceFlag) != 0) {
                flags |= RoSkillTargetFlags.Place;
            }

            if ((skillType & SkillTargetSelfFlag) != 0) {
                flags |= RoSkillTargetFlags.Self;
            }

            if ((skillType & SkillTargetFriendFlag) != 0) {
                flags |= RoSkillTargetFlags.Friend;
            }

            if ((skillType & SkillTargetTrapFlag) != 0) {
                flags |= RoSkillTargetFlags.Trap;
            }

            if ((skillType & SkillTargetPetFlag) != 0) {
                flags |= RoSkillTargetFlags.Pet;
            }

            if ((skillType & SkillTargetHomunFlag) != 0) {
                flags |= RoSkillTargetFlags.Homun;
            }

            return flags;
        }

        /// <summary>The slot's item in the inventory of the snapshot, by item id (null when there is none).</summary>
        public static RoItemInstance FindInventoryItem(RoGameplayHotkeySnapshot gameplay, int itemId) {
            RoItemInstance[] items = gameplay != null ? gameplay.InventoryItems : null;
            if (items == null) {
                return null;
            }

            for (int i = 0; i < items.Length; i++) {
                if (items[i] != null && items[i].ItemId == itemId) {
                    return items[i];
                }
            }

            return null;
        }

        /// <summary>The learned level of a skill in the snapshot (0 when it is not learned).</summary>
        public static int ResolveLearnedSkillLevel(RoGameplayHotkeySnapshot gameplay, int skillId) {
            RoGameplaySkillInfo[] skills = gameplay != null ? gameplay.Skills : null;
            if (skills == null) {
                return 0;
            }

            for (int i = 0; i < skills.Length; i++) {
                if (skills[i].SkillId == skillId) {
                    return skills[i].Level > 0 ? skills[i].Level : skills[i].Level2;
                }
            }

            return 0;
        }

        private bool TryBuildTargetSelectionRequest(int hotkeyIndex, out TargetSelectionRequest request) {
            request = default;
            RoGameplayHotkeySnapshot current = session != null ? session.HotkeySnapshot() : null;
            RoShortcutKeyEntry[] currentKeys = current != null ? current.ShortcutKeys : null;
            if (currentKeys == null || hotkeyIndex < 0 || hotkeyIndex >= currentKeys.Length) {
                return false;
            }

            RoShortcutKeyEntry shortcut = currentKeys[hotkeyIndex];
            if (!shortcut.IsSkill) {
                return false;
            }

            int skillId = unchecked((int) shortcut.Id);
            RoGameplaySkillInfo skillInfo = FindGameplaySkill(current, skillId);
            if (skillInfo.SkillId == 0) {
                return false;
            }

            RoSkillMetadata metadata = skillMetadata.GetSkillOrFallback(skillId);
            RoSkillTargetFlags targetFlags = ResolveHotkeyTargetFlags(metadata, skillInfo);
            if (!RequiresExplicitTargetSelection(targetFlags)) {
                return false;
            }

            string skillName = ResolveHotkeySkillDisplayName(skillId, metadata, skillInfo);
            int level = shortcut.Count > 0 ? shortcut.Count : ResolveLearnedSkillLevel(current, skillId);
            request = new TargetSelectionRequest(skillId, level > 0 ? level : 1, skillName, targetFlags);
            return true;
        }

        private static bool RequiresExplicitTargetSelection(RoSkillTargetFlags targetFlags) {
            return (targetFlags & (
                RoSkillTargetFlags.Enemy |
                RoSkillTargetFlags.Place |
                RoSkillTargetFlags.Friend |
                RoSkillTargetFlags.Trap |
                RoSkillTargetFlags.Pet |
                RoSkillTargetFlags.Homun)) != 0;
        }

        private static RoSkillTargetFlags ResolveHotkeyTargetFlags(RoSkillMetadata metadata, RoGameplaySkillInfo skillInfo) {
            RoSkillTargetFlags serverTargetFlags = skillInfo.SkillId != 0
                ? ResolveTargetFlagsFromServerSkillType(skillInfo.Type)
                : RoSkillTargetFlags.None;
            if (serverTargetFlags != RoSkillTargetFlags.None) {
                return serverTargetFlags;
            }

            if (skillInfo.SkillId == MerchantIdentifySkillId ||
                skillInfo.SkillId == MerchantChangeCartSkillId ||
                skillInfo.SkillId == MerchantCartDecorateSkillId ||
                skillInfo.SkillId == ThiefFindStoneSkillId) {
                return RoSkillTargetFlags.Self;
            }

            if (metadata != null) {
                return metadata.TargetFlags;
            }

            return RoSkillTargetFlags.None;
        }

        private static RoGameplaySkillInfo FindGameplaySkill(RoGameplayHotkeySnapshot gameplay, int skillId) {
            RoGameplaySkillInfo[] skills = gameplay != null ? gameplay.Skills : null;
            if (skills == null) {
                return default;
            }

            for (int i = 0; i < skills.Length; i++) {
                if (skills[i].SkillId == skillId) {
                    return skills[i];
                }
            }

            return default;
        }

        private static string ResolveHotkeySkillDisplayName(int skillId, RoSkillMetadata metadata, RoGameplaySkillInfo skillInfo) {
            if (metadata != null) {
                string displayName = metadata.GetDisplayName();
                if (!string.Equals(displayName, "Unknown Skill", StringComparison.OrdinalIgnoreCase) &&
                    !RoSkillTreeModel.IsRawAegisSkillName(displayName)) {
                    return displayName;
                }
            }

            if (!string.IsNullOrWhiteSpace(skillInfo.Name) && !RoSkillTreeModel.IsRawAegisSkillName(skillInfo.Name)) {
                return skillInfo.Name;
            }

            string aegisName = !string.IsNullOrWhiteSpace(skillInfo.Name)
                ? skillInfo.Name
                : metadata != null ? metadata.AegisName : null;
            string classicName = RoSkillTreeModel.ResolveClassicSkillDisplayName(aegisName);
            if (!string.IsNullOrWhiteSpace(classicName)) {
                return classicName;
            }

            return metadata != null ? metadata.GetDisplayName() : "Skill " + skillId.ToString();
        }

        /// <summary>The bar slot whose key was pressed this frame: the settings' bindings (modifiers included), else the default keys.</summary>
        public bool TryResolvePressedShortcutIndex(out int index) {
            if (settings != null) {
                return TryResolvePressedShortcutIndexFromSettings(out index);
            }

            for (int i = 0; i < DefaultSlotKeys.Length; i++) {
                KeyControl key = RoUnityKeyboardInputState.GetKeyControl(DefaultSlotKeys[i]);
                if (key != null && key.wasPressedThisFrame) {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private bool TryResolvePressedShortcutIndexFromSettings(out int index) {
            index = -1;
            if (settings == null) {
                return false;
            }

            EventModifiers modifiers = RoBasicInfoIconHotkeyExecutor.GetCurrentKeyboardModifiers(KeyboardInput);
            for (int i = 0; i < VisibleSlotCount; i++) {
                if (!settings.TryGetShortcutBinding(RoClientSettingsModel.HotkeyActionId(i), out RoShortcutBindingSnapshot binding) ||
                    !binding.RuntimeSupported ||
                    !binding.Binding.IsAssigned ||
                    binding.Binding.Modifiers != modifiers) {
                    continue;
                }

                KeyControl control = RoUnityKeyboardInputState.GetKeyControl(binding.Binding.Key);
                if (control != null && control.wasPressedThisFrame) {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolveSelectedSkillTargetAid(RoOnlineTargetSelection selected, out int targetAid) {
            targetAid = 0;
            if (!selected.HasTarget ||
                selected.ServerId > int.MaxValue ||
                selected.PointerKind == RoPointerTargetKind.Item ||
                selected.PointerKind == RoPointerTargetKind.Npc ||
                selected.PointerKind == RoPointerTargetKind.Portal) {
                return false;
            }

            if (selected.Interaction != RoOnlineTargetInteraction.Attack &&
                selected.PointerKind != RoPointerTargetKind.Monster &&
                selected.PointerKind != RoPointerTargetKind.Player) {
                return false;
            }

            targetAid = unchecked((int) selected.ServerId);
            return targetAid > 0;
        }

        private static IRoHotkeyCommandExecutor CreateLegacyExecutor(RoLoginShellSession loginSession) {
            return loginSession != null
                ? new RoLegacyHotkeyCommandExecutor(loginSession, ResolveSelectedSkillTargetAid)
                : null;
        }

        private static int ResolveSelectedSkillTargetAid() {
            RoOnlineWorldController controller = ResolveCachedWorldController(forceLookup: true);
            return controller != null && TryResolveSelectedSkillTargetAid(controller.SelectedTarget, out int targetAid)
                ? targetAid
                : 0;
        }

        private static RoAssetDatabase ResolveInitializedAssetDatabase() {
            RoRuntimeMapController map = ResolveCachedRuntimeMapController();
            RoAssetDatabase assets = map != null && map.Content != null ? map.Content.Assets : null;
            return assets != null && assets.IsInitialized ? assets : null;
        }

        private static RoOnlineWorldController ResolveCachedWorldController(bool forceLookup) {
            if (cachedWorldController != null) {
                return cachedWorldController;
            }

            float now = Time.unscaledTime;
            if (!forceLookup && now < nextWorldControllerLookupTime) {
                return null;
            }

            cachedWorldController = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            nextWorldControllerLookupTime = cachedWorldController != null
                ? 0f
                : now + DependencyLookupRetrySeconds;
            return cachedWorldController;
        }

        private static RoRuntimeMapController ResolveCachedRuntimeMapController() {
            if (cachedRuntimeMapController != null) {
                return cachedRuntimeMapController;
            }

            RoOnlineWorldController controller = ResolveCachedWorldController(forceLookup: false);
            cachedRuntimeMapController = controller != null ? controller.RuntimeMapController : null;
            if (cachedRuntimeMapController != null) {
                nextRuntimeMapControllerLookupTime = 0f;
                return cachedRuntimeMapController;
            }

            float now = Time.unscaledTime;
            if (now < nextRuntimeMapControllerLookupTime) {
                return null;
            }

            cachedRuntimeMapController = UnityEngine.Object.FindFirstObjectByType<RoRuntimeMapController>();
            nextRuntimeMapControllerLookupTime = cachedRuntimeMapController != null
                ? 0f
                : now + DependencyLookupRetrySeconds;
            return cachedRuntimeMapController;
        }

        /// <summary>The slot's key as the settings bind it (e.g. "F1", "Alt+Q"), else the default key's name.</summary>
        public string FormatHotkeyLabel(int index) {
            if (settings != null &&
                settings.TryGetShortcutBinding(RoClientSettingsModel.HotkeyActionId(index), out RoShortcutBindingSnapshot binding)) {
                return binding.Binding.ToDisplayString();
            }

            return FormatHotkey(index);
        }

        private static string FormatHotkey(int index) {
            if (index < 0) {
                return "-";
            }

            int row = index / SlotsPerRow;
            int column = index % SlotsPerRow;
            switch (row) {
                case 0:
                    return "F" + (column + 1).ToString();
                case 1:
                    return (column + 1).ToString();
                case 2:
                    return "QWERTYUIO"[column].ToString();
                case 3:
                    return "ASDFGHJKL"[column].ToString();
                default:
                    return (index + 1).ToString();
            }
        }
    }
}
