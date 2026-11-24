using System;
using Ragnarok.Data;
using Ragnarok.Network;

namespace Ragnarok.Gameplay {
    public enum RoGameplayActionKind {
        None = 0,
        UseItem = 1,
        UseSkill = 2
    }

    public readonly struct RoGameplayHotkeyAction {
        public static readonly RoGameplayHotkeyAction None = new RoGameplayHotkeyAction(
            RoGameplayActionKind.None,
            -1,
            default,
            -1,
            0,
            0,
            0);

        public RoGameplayHotkeyAction(
            RoGameplayActionKind kind,
            int hotkeyIndex,
            RoShortcutKeyEntry shortcut,
            int bagIndex,
            int itemId,
            int skillId,
            int skillLevel) {
            Kind = kind;
            HotkeyIndex = hotkeyIndex;
            Shortcut = shortcut;
            BagIndex = bagIndex;
            ItemId = itemId;
            SkillId = skillId;
            SkillLevel = skillLevel;
        }

        public RoGameplayActionKind Kind { get; }
        public bool IsResolved => Kind != RoGameplayActionKind.None;
        public int HotkeyIndex { get; }
        public RoShortcutKeyEntry Shortcut { get; }
        public int BagIndex { get; }
        public int ItemId { get; }
        public int SkillId { get; }
        public int SkillLevel { get; }
    }

    public static class RoGameplayActionResolver {
        private const int TfHidingSkillId = 51;
        private const int NvTrickDeadSkillId = 143;
        private const int TfPickStoneSkillId = 151;

        public static bool TryResolveHotkey(
            RoGameplaySnapshot snapshot,
            int hotkeyIndex,
            out RoGameplayHotkeyAction action) {
            action = RoGameplayHotkeyAction.None;
            if (snapshot == null || hotkeyIndex < 0 || hotkeyIndex >= snapshot.ShortcutKeys.Length) {
                return false;
            }

            RoShortcutKeyEntry shortcut = snapshot.ShortcutKeys[hotkeyIndex];
            if (shortcut.IsEmpty || shortcut.Id == 0) {
                return false;
            }

            if (shortcut.IsSkill) {
                return TryResolveSkillHotkey(snapshot, hotkeyIndex, shortcut, out action);
            }

            return TryResolveItemHotkey(snapshot, hotkeyIndex, shortcut, out action);
        }

        public static bool TryBuildHotkeyPacket(
            RoGameplaySnapshot snapshot,
            int hotkeyIndex,
            int localAid,
            int targetAid,
            out byte[] packet) {
            packet = Array.Empty<byte>();
            if (localAid <= 0 || !TryResolveHotkey(snapshot, hotkeyIndex, out RoGameplayHotkeyAction action)) {
                return false;
            }

            switch (action.Kind) {
                case RoGameplayActionKind.UseItem:
                    packet = RoOutboundPackets.BuildCzUseItem2(action.BagIndex, localAid);
                    return true;
                case RoGameplayActionKind.UseSkill:
                    int resolvedTargetAid = ResolveHotkeySkillTargetAid(snapshot, action.SkillId, localAid, targetAid);
                    if (resolvedTargetAid < 0) {
                        return false;
                    }

                    packet = RoOutboundPackets.BuildCzUseSkill2(action.SkillLevel, action.SkillId, resolvedTargetAid);
                    return true;
                default:
                    return false;
            }
        }

        public static int ResolveHotkeySkillTargetAid(
            RoGameplaySnapshot snapshot,
            int skillId,
            int localAid,
            int targetAid) {
            if (localAid <= 0) {
                return -1;
            }

            return ShouldForceSelfSkillTarget(snapshot, skillId)
                ? localAid
                : targetAid > 0 ? targetAid : localAid;
        }

        private static bool ShouldForceSelfSkillTarget(RoGameplaySnapshot snapshot, int skillId) {
            if (TryFindSkill(snapshot?.Skills, skillId, out RoGameplaySkillInfo skill)) {
                RoSkillTargetFlags targetFlags = RoSkillMetadata.FromServerSkillType(skill.Type);
                if (targetFlags != RoSkillTargetFlags.None) {
                    return targetFlags == RoSkillTargetFlags.Self;
                }
            }

            return IsKnownSelfTargetSkill(skillId);
        }

        private static bool IsKnownSelfTargetSkill(int skillId) {
            return skillId == TfHidingSkillId ||
                skillId == NvTrickDeadSkillId ||
                skillId == TfPickStoneSkillId;
        }

        public static bool TryBuildUseInventoryItemPacket(
            RoGameplaySnapshot snapshot,
            int bagIndex,
            int localAid,
            out RoItemInstance item,
            out byte[] packet) {
            item = null;
            packet = Array.Empty<byte>();
            if (snapshot == null || localAid <= 0 || !TryFindUsableInventoryItem(snapshot.InventoryItems, bagIndex, out item)) {
                return false;
            }

            packet = RoOutboundPackets.BuildCzUseItem2(item.BagIndex, localAid);
            return true;
        }

        public static bool TryBuildAttackPacket(int targetAid, bool repeatAttack, out byte[] packet) {
            packet = Array.Empty<byte>();
            if (targetAid <= 0) {
                return false;
            }

            packet = RoOutboundPackets.BuildCzActionRequest2(targetAid, repeatAttack ? (byte) 7 : (byte) 0);
            return true;
        }

        public static bool TryBuildUseSkillPacket(
            RoGameplaySnapshot snapshot,
            int skillId,
            int skillLevel,
            int targetAid,
            out RoGameplaySkillInfo skill,
            out byte[] packet) {
            packet = Array.Empty<byte>();
            skill = default;
            if (!TryResolveManualSkill(snapshot, skillId, skillLevel, out skill, out int resolvedLevel) || targetAid <= 0) {
                return false;
            }

            packet = RoOutboundPackets.BuildCzUseSkill2(resolvedLevel, skillId, targetAid);
            return true;
        }

        public static bool TryBuildUseSkillToGroundPacket(
            RoGameplaySnapshot snapshot,
            int skillId,
            int skillLevel,
            int x,
            int y,
            out RoGameplaySkillInfo skill,
            out byte[] packet) {
            packet = Array.Empty<byte>();
            skill = default;
            if (!TryResolveManualSkill(snapshot, skillId, skillLevel, out skill, out int resolvedLevel) || x < 0 || y < 0) {
                return false;
            }

            packet = RoOutboundPackets.BuildCzUseSkillToGround2(resolvedLevel, skillId, x, y);
            return true;
        }

        public static bool TryBuildWearInventoryItemPacket(
            RoGameplaySnapshot snapshot,
            int bagIndex,
            out RoItemInstance item,
            out uint position,
            out byte[] packet) {
            item = null;
            position = 0;
            packet = Array.Empty<byte>();
            if (snapshot == null || !TryFindWearableInventoryItem(snapshot.InventoryItems, bagIndex, out item)) {
                return false;
            }

            position = unchecked((uint) item.EquipLocationHint);
            if (position == 0) {
                return false;
            }

            packet = RoOutboundPackets.BuildCzReqWearEquipV5(item.BagIndex, position);
            return true;
        }

        public static bool TryBuildDropInventoryItemPacket(
            RoGameplaySnapshot snapshot,
            int bagIndex,
            int count,
            out RoItemInstance item,
            out byte[] packet) {
            item = null;
            packet = Array.Empty<byte>();
            if (snapshot == null ||
                count <= 0 ||
                !TryFindDroppableInventoryItem(snapshot.InventoryItems, bagIndex, out item) ||
                count > item.Count) {
                return false;
            }

            packet = RoOutboundPackets.BuildCzItemThrow2(item.BagIndex, count);
            return true;
        }

        public static bool TryBuildTakeoffInventoryItemPacket(
            RoGameplaySnapshot snapshot,
            int bagIndex,
            out RoItemInstance item,
            out byte[] packet) {
            item = null;
            packet = Array.Empty<byte>();
            if (snapshot == null || !TryFindEquippedInventoryItem(snapshot.InventoryItems, bagIndex, out item)) {
                return false;
            }

            packet = RoOutboundPackets.BuildCzReqTakeoffEquip(item.BagIndex);
            return true;
        }

        private static bool TryResolveItemHotkey(
            RoGameplaySnapshot snapshot,
            int hotkeyIndex,
            RoShortcutKeyEntry shortcut,
            out RoGameplayHotkeyAction action) {
            action = RoGameplayHotkeyAction.None;
            if (shortcut.Id > int.MaxValue) {
                return false;
            }

            int itemId = unchecked((int) shortcut.Id);
            RoItemInstance[] items = snapshot.InventoryItems;
            for (int i = 0; i < items.Length; i++) {
                RoItemInstance item = items[i];
                if (item.ItemId == itemId && item.Count > 0) {
                    action = new RoGameplayHotkeyAction(
                        RoGameplayActionKind.UseItem,
                        hotkeyIndex,
                        shortcut,
                        item.BagIndex,
                        itemId,
                        0,
                        0);
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveSkillHotkey(
            RoGameplaySnapshot snapshot,
            int hotkeyIndex,
            RoShortcutKeyEntry shortcut,
            out RoGameplayHotkeyAction action) {
            action = RoGameplayHotkeyAction.None;
            if (shortcut.Id > ushort.MaxValue) {
                return false;
            }

            int skillId = unchecked((int) shortcut.Id);
            if (!TryFindSkill(snapshot.Skills, skillId, out RoGameplaySkillInfo skill)) {
                return false;
            }

            // Current skill-list packets carry the actionable level in Level2 when Level is not populated.
            int knownLevel = skill.Level > 0 ? skill.Level : skill.Level2;
            if (knownLevel <= 0 || knownLevel > ushort.MaxValue) {
                return false;
            }

            int skillLevel = shortcut.Count > 0 ? Math.Min(shortcut.Count, knownLevel) : knownLevel;
            if (skillLevel <= 0 || skillLevel > ushort.MaxValue) {
                return false;
            }

            action = new RoGameplayHotkeyAction(
                RoGameplayActionKind.UseSkill,
                hotkeyIndex,
                shortcut,
                -1,
                0,
                skillId,
                skillLevel);
            return true;
        }

        private static bool TryResolveManualSkill(
            RoGameplaySnapshot snapshot,
            int skillId,
            int requestedLevel,
            out RoGameplaySkillInfo skill,
            out int resolvedLevel) {
            skill = default;
            resolvedLevel = requestedLevel;
            if (skillId <= 0 || skillId > ushort.MaxValue) {
                return false;
            }

            if (snapshot != null && TryFindSkill(snapshot.Skills, skillId, out skill)) {
                int knownLevel = skill.Level > 0 ? skill.Level : skill.Level2;
                if (resolvedLevel <= 0) {
                    resolvedLevel = knownLevel;
                } else if (knownLevel > 0) {
                    resolvedLevel = Math.Min(resolvedLevel, knownLevel);
                }
            }

            return resolvedLevel > 0 && resolvedLevel <= ushort.MaxValue;
        }

        private static bool TryFindSkill(RoGameplaySkillInfo[] skills, int skillId, out RoGameplaySkillInfo skill) {
            if (skills != null) {
                for (int i = 0; i < skills.Length; i++) {
                    if (skills[i].SkillId == skillId) {
                        skill = skills[i];
                        return true;
                    }
                }
            }

            skill = default;
            return false;
        }

        private static bool TryFindUsableInventoryItem(RoItemInstance[] items, int bagIndex, out RoItemInstance item) {
            item = null;
            if (items == null) {
                return false;
            }

            for (int i = 0; i < items.Length; i++) {
                RoItemInstance candidate = items[i];
                if (candidate == null ||
                    candidate.BagIndex != bagIndex ||
                    candidate.Count <= 0 ||
                    candidate.IsEquipped ||
                    candidate.EquipLocationHint != RoItemEquipLocation.None) {
                    continue;
                }

                item = candidate;
                return true;
            }

            return false;
        }

        private static bool TryFindDroppableInventoryItem(RoItemInstance[] items, int bagIndex, out RoItemInstance item) {
            item = null;
            if (items == null) {
                return false;
            }

            for (int i = 0; i < items.Length; i++) {
                RoItemInstance candidate = items[i];
                if (candidate == null ||
                    candidate.BagIndex != bagIndex ||
                    candidate.Count <= 0 ||
                    candidate.IsEquipped) {
                    continue;
                }

                item = candidate;
                return true;
            }

            return false;
        }

        private static bool TryFindWearableInventoryItem(RoItemInstance[] items, int bagIndex, out RoItemInstance item) {
            item = null;
            if (items == null) {
                return false;
            }

            for (int i = 0; i < items.Length; i++) {
                RoItemInstance candidate = items[i];
                if (candidate == null ||
                    candidate.BagIndex != bagIndex ||
                    candidate.Count <= 0 ||
                    candidate.IsEquipped ||
                    candidate.EquipLocationHint == RoItemEquipLocation.None) {
                    continue;
                }

                item = candidate;
                return true;
            }

            return false;
        }

        private static bool TryFindEquippedInventoryItem(RoItemInstance[] items, int bagIndex, out RoItemInstance item) {
            item = null;
            if (items == null) {
                return false;
            }

            for (int i = 0; i < items.Length; i++) {
                RoItemInstance candidate = items[i];
                if (candidate == null ||
                    candidate.BagIndex != bagIndex ||
                    candidate.Count <= 0 ||
                    !candidate.IsEquipped) {
                    continue;
                }

                item = candidate;
                return true;
            }

            return false;
        }
    }
}
