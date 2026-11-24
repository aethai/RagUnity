using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal static class RoShellStatusTextFormatter {
        public static string FormatHotkeyActionStatus(RoGameplayHotkeyAction action, string verb) {
            string key = action.HotkeyIndex >= 0 ? $"F{action.HotkeyIndex + 1}" : "hotkey";
            switch (action.Kind) {
                case RoGameplayActionKind.UseItem:
                    return $"{verb} {key}: {FormatUnknownItem(action.ItemId)}";
                case RoGameplayActionKind.UseSkill:
                    return $"{verb} {key}: skill {action.SkillId} Lv{action.SkillLevel}";
                default:
                    return $"{verb} {key}";
            }
        }

        public static string FormatShortcutChangeStatus(int hotkeyIndex, RoShortcutKeyEntry shortcut, string verb) {
            string key = hotkeyIndex >= 0 ? $"F{hotkeyIndex + 1}" : "hotkey";
            if (shortcut.IsEmpty) {
                return $"{verb} {key}: empty";
            }

            string count = shortcut.Count > 0 ? $" x{shortcut.Count}" : string.Empty;
            string value = shortcut.IsSkill ? $"skill {shortcut.Id}" : FormatUnknownItem(shortcut.Id);
            return $"{verb} {key}: {value}{count}";
        }

        public static string FormatInventoryItemStatus(RoItemInstance item, string verb) {
            if (item == null) {
                return $"{verb} item";
            }

            return $"{verb} {FormatItemName(item)} from slot {item.BagIndex}";
        }

        private static string FormatItemName(RoItemInstance item) {
            string name = item.GetDisplayName(RoItemMetadataTable.Empty);
            if (string.IsNullOrWhiteSpace(name) ||
                string.Equals(name.Trim(), "Unknown Item", System.StringComparison.OrdinalIgnoreCase)) {
                return FormatUnknownItem(item.ItemId);
            }

            return name;
        }

        private static string FormatUnknownItem(long itemId) {
            return itemId > 0 ? $"Unknown item #{itemId}" : "Unknown item";
        }
    }
}
