using Ragnarok.Gameplay;
using Ragnarok.Data;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal readonly struct RoShellStatusUpdate {
        public RoShellStatusUpdate(string statusText, string errorText) {
            StatusText = statusText ?? string.Empty;
            ErrorText = errorText ?? string.Empty;
        }

        public string StatusText { get; }
        public string ErrorText { get; }
    }

    internal static class RoLiveDrainStatusResolver {
        public static bool ContainsStatusUpdateEvent(RoMapEntryDrainResult drain) {
            if (drain?.OrderedEvents == null || drain.OrderedEvents.Length == 0) {
                return false;
            }

            for (int i = 0; i < drain.OrderedEvents.Length; i++) {
                if (CanResolveStatusUpdate(drain.OrderedEvents[i].Kind)) {
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolve(
            RoMapEntryDrainResult drain,
            RoGameplayInventorySnapshot snapshot,
            out RoShellStatusUpdate update) {
            if (drain?.OrderedEvents == null || drain.OrderedEvents.Length == 0) {
                update = default;
                return false;
            }

            bool hasUpdate = false;
            update = default;
            for (int i = 0; i < drain.OrderedEvents.Length; i++) {
                if (TryResolve(drain.OrderedEvents[i], snapshot, out RoShellStatusUpdate candidate)) {
                    update = candidate;
                    hasUpdate = true;
                }
            }

            return hasUpdate;
        }

        private static bool CanResolveStatusUpdate(RoMapEntryDrainEventKind kind) {
            return kind == RoMapEntryDrainEventKind.ItemThrowAck ||
                kind == RoMapEntryDrainEventKind.WearEquipmentAck ||
                kind == RoMapEntryDrainEventKind.TakeoffEquipmentAck;
        }

        private static bool TryResolve(
            RoMapEntryDrainEvent drainEvent,
            RoGameplayInventorySnapshot snapshot,
            out RoShellStatusUpdate update) {
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.ItemThrowAck:
                    RoZcItemThrowAck throwAck = drainEvent.ItemThrowAck;
                    RoItemInstance droppedItem = FindInventoryItem(snapshot, throwAck.BagIndex);
                    update = throwAck.IsSuccess
                        ? new RoShellStatusUpdate(RoShellStatusTextFormatter.FormatInventoryItemStatus(droppedItem, "dropped") + $" x{throwAck.Count}", string.Empty)
                        : new RoShellStatusUpdate(RoShellStatusTextFormatter.FormatInventoryItemStatus(droppedItem, "drop rejected for"), $"Drop rejected for slot {throwAck.BagIndex}.");
                    return true;
                case RoMapEntryDrainEventKind.WearEquipmentAck:
                    RoZcWearEquipmentAck wearAck = drainEvent.WearEquipmentAck;
                    RoItemInstance wornItem = FindInventoryItem(snapshot, wearAck.BagIndex);
                    update = wearAck.IsSuccess
                        ? new RoShellStatusUpdate(RoShellStatusTextFormatter.FormatInventoryItemStatus(wornItem, "equipped"), string.Empty)
                        : new RoShellStatusUpdate(RoShellStatusTextFormatter.FormatInventoryItemStatus(wornItem, "equip rejected for"), $"Equip rejected for slot {wearAck.BagIndex}.");
                    return true;
                case RoMapEntryDrainEventKind.TakeoffEquipmentAck:
                    RoZcTakeoffEquipmentAck takeoffAck = drainEvent.TakeoffEquipmentAck;
                    RoItemInstance takenOffItem = FindInventoryItem(snapshot, takeoffAck.BagIndex);
                    update = takeoffAck.IsSuccess
                        ? new RoShellStatusUpdate(RoShellStatusTextFormatter.FormatInventoryItemStatus(takenOffItem, "took off"), string.Empty)
                        : new RoShellStatusUpdate(RoShellStatusTextFormatter.FormatInventoryItemStatus(takenOffItem, "takeoff rejected for"), $"Takeoff rejected for slot {takeoffAck.BagIndex}.");
                    return true;
                default:
                    update = default;
                    return false;
            }
        }

        private static RoItemInstance FindInventoryItem(RoGameplayInventorySnapshot snapshot, int bagIndex) {
            if (snapshot?.InventoryItems == null) {
                return null;
            }

            for (int i = 0; i < snapshot.InventoryItems.Length; i++) {
                RoItemInstance item = snapshot.InventoryItems[i];
                if (item != null && item.BagIndex == bagIndex) {
                    return item;
                }
            }

            return null;
        }
    }
}
