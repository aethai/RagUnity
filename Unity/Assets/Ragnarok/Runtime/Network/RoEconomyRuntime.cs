using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public enum RoEconomyCommandStatus {
        Accepted = 0,
        InactiveMapSession = 1,
        InvalidState = 2,
        InvalidTarget = 3,
        MissingItem = 4,
        InvalidCount = 5,
        SendFailed = 6,
        Disposed = 7,
        UnsupportedProfile = 8
    }

    public readonly struct RoEconomyCommandResult {
        public RoEconomyCommandResult(RoEconomyCommandStatus status, string message) {
            Status = status;
            Message = message ?? string.Empty;
        }

        public RoEconomyCommandStatus Status { get; }
        public string Message { get; }
        public bool Accepted => Status == RoEconomyCommandStatus.Accepted;

        public static RoEconomyCommandResult AcceptedResult() {
            return new RoEconomyCommandResult(RoEconomyCommandStatus.Accepted, string.Empty);
        }

        public static RoEconomyCommandResult Failure(RoEconomyCommandStatus status, string message) {
            return new RoEconomyCommandResult(status, message);
        }
    }

    public readonly struct RoEconomyItemSnapshot {
        public RoEconomyItemSnapshot(
            int index,
            uint itemId,
            byte itemTypeRaw,
            int count,
            bool isIdentified,
            bool isDamaged,
            byte refineLevel,
            int[] cardIds,
            bool isEquipment) {
            Index = index;
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            Count = Math.Max(0, count);
            IsIdentified = isIdentified;
            IsDamaged = isDamaged;
            RefineLevel = refineLevel;
            CardIds = CopyCardIds(cardIds);
            IsEquipment = isEquipment;
        }

        public int Index { get; }
        public uint ItemId { get; }
        public byte ItemTypeRaw { get; }
        public int Count { get; }
        public bool IsIdentified { get; }
        public bool IsDamaged { get; }
        public byte RefineLevel { get; }
        public int[] CardIds { get; }
        public bool IsEquipment { get; }

        public RoEconomyItemSnapshot WithCount(int count) {
            return new RoEconomyItemSnapshot(
                Index,
                ItemId,
                ItemTypeRaw,
                count,
                IsIdentified,
                IsDamaged,
                RefineLevel,
                CardIds,
                IsEquipment);
        }

        public static bool IsKnownEquipmentItemType(byte itemTypeRaw) {
            return itemTypeRaw == 4 ||
                itemTypeRaw == 5 ||
                itemTypeRaw == 8 ||
                itemTypeRaw == 12;
        }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[4];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoRuntimeStorageSnapshot {
        public RoRuntimeStorageSnapshot(bool isOpen, int amount, int maxAmount, RoEconomyItemSnapshot[] items, string lastMessage) {
            IsOpen = isOpen;
            Amount = Math.Max(0, amount);
            MaxAmount = Math.Max(0, maxAmount);
            Items = Copy(items);
            LastMessage = lastMessage ?? string.Empty;
        }

        public bool IsOpen { get; }
        public int Amount { get; }
        public int MaxAmount { get; }
        public RoEconomyItemSnapshot[] Items { get; }
        public string LastMessage { get; }

        private static RoEconomyItemSnapshot[] Copy(RoEconomyItemSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoEconomyItemSnapshot>();
            }

            var copy = new RoEconomyItemSnapshot[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }
    }

    public readonly struct RoCartSnapshot {
        public RoCartSnapshot(
            bool hasCart,
            bool isOpen,
            int currentCount,
            int maxCount,
            int currentWeight,
            int maxWeight,
            int cartVariant,
            RoEconomyItemSnapshot[] items,
            string lastMessage) {
            HasCart = hasCart;
            IsOpen = isOpen;
            CurrentCount = Math.Max(0, currentCount);
            MaxCount = Math.Max(0, maxCount);
            CurrentWeight = Math.Max(0, currentWeight);
            MaxWeight = Math.Max(0, maxWeight);
            CartVariant = cartVariant;
            Items = Copy(items);
            LastMessage = lastMessage ?? string.Empty;
        }

        public bool HasCart { get; }
        public bool IsOpen { get; }
        public int CurrentCount { get; }
        public int MaxCount { get; }
        public int CurrentWeight { get; }
        public int MaxWeight { get; }
        public int CartVariant { get; }
        public RoEconomyItemSnapshot[] Items { get; }
        public string LastMessage { get; }

        private static RoEconomyItemSnapshot[] Copy(RoEconomyItemSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoEconomyItemSnapshot>();
            }

            var copy = new RoEconomyItemSnapshot[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }
    }

    public readonly struct RoTradeSnapshot {
        public RoTradeSnapshot(
            bool isOpen,
            bool hasPendingIncomingRequest,
            string pendingIncomingName,
            uint pendingIncomingTargetId,
            bool hasPendingOutgoingRequest,
            uint pendingOutgoingTargetId,
            string partnerName,
            uint partnerId,
            bool selfLocked,
            bool partnerLocked,
            bool wasCanceled,
            byte? lastRequestResult,
            byte? lastAddItemResult,
            byte? lastSubmitResult,
            int selfZeny,
            int partnerZeny,
            RoEconomyItemSnapshot[] selfItems,
            RoEconomyItemSnapshot[] partnerItems,
            string lastMessage) {
            IsOpen = isOpen;
            HasPendingIncomingRequest = hasPendingIncomingRequest;
            PendingIncomingName = pendingIncomingName ?? string.Empty;
            PendingIncomingTargetId = pendingIncomingTargetId;
            HasPendingOutgoingRequest = hasPendingOutgoingRequest;
            PendingOutgoingTargetId = pendingOutgoingTargetId;
            PartnerName = partnerName ?? string.Empty;
            PartnerId = partnerId;
            SelfLocked = selfLocked;
            PartnerLocked = partnerLocked;
            WasCanceled = wasCanceled;
            LastRequestResult = lastRequestResult;
            LastAddItemResult = lastAddItemResult;
            LastSubmitResult = lastSubmitResult;
            SelfZeny = Math.Max(0, selfZeny);
            PartnerZeny = Math.Max(0, partnerZeny);
            SelfItems = Copy(selfItems);
            PartnerItems = Copy(partnerItems);
            LastMessage = lastMessage ?? string.Empty;
        }

        public bool IsOpen { get; }
        public bool HasPendingIncomingRequest { get; }
        public string PendingIncomingName { get; }
        public uint PendingIncomingTargetId { get; }
        public bool HasPendingOutgoingRequest { get; }
        public uint PendingOutgoingTargetId { get; }
        public string PartnerName { get; }
        public uint PartnerId { get; }
        public bool SelfLocked { get; }
        public bool PartnerLocked { get; }
        public bool WasCanceled { get; }
        public byte? LastRequestResult { get; }
        public byte? LastAddItemResult { get; }
        public byte? LastSubmitResult { get; }
        public int SelfZeny { get; }
        public int PartnerZeny { get; }
        public RoEconomyItemSnapshot[] SelfItems { get; }
        public RoEconomyItemSnapshot[] PartnerItems { get; }
        public string LastMessage { get; }

        private static RoEconomyItemSnapshot[] Copy(RoEconomyItemSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoEconomyItemSnapshot>();
            }

            var copy = new RoEconomyItemSnapshot[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }
    }

    public readonly struct RoStorageMoveInventoryItemRequest {
        public RoStorageMoveInventoryItemRequest(int bagIndex, int amount) {
            BagIndex = bagIndex;
            Amount = amount;
        }

        public int BagIndex { get; }
        public int Amount { get; }
    }

    public readonly struct RoStorageMoveToInventoryRequest {
        public RoStorageMoveToInventoryRequest(int storageIndex, int amount) {
            StorageIndex = storageIndex;
            Amount = amount;
        }

        public int StorageIndex { get; }
        public int Amount { get; }
    }

    public readonly struct RoStorageMoveToCartRequest {
        public RoStorageMoveToCartRequest(int storageIndex, int amount) {
            StorageIndex = storageIndex;
            Amount = amount;
        }

        public int StorageIndex { get; }
        public int Amount { get; }
    }

    public readonly struct RoCartMoveInventoryItemRequest {
        public RoCartMoveInventoryItemRequest(int bagIndex, int amount) {
            BagIndex = bagIndex;
            Amount = amount;
        }

        public int BagIndex { get; }
        public int Amount { get; }
    }

    public readonly struct RoCartMoveToStorageRequest {
        public RoCartMoveToStorageRequest(int cartIndex, int amount) {
            CartIndex = cartIndex;
            Amount = amount;
        }

        public int CartIndex { get; }
        public int Amount { get; }
    }

    public readonly struct RoCartMoveToInventoryRequest {
        public RoCartMoveToInventoryRequest(int cartIndex, int amount) {
            CartIndex = cartIndex;
            Amount = amount;
        }

        public int CartIndex { get; }
        public int Amount { get; }
    }

    public readonly struct RoTradeRequestCommand {
        public RoTradeRequestCommand(uint targetActorId, string targetName) {
            TargetActorId = targetActorId;
            TargetName = targetName ?? string.Empty;
        }

        public uint TargetActorId { get; }
        public string TargetName { get; }
    }

    public readonly struct RoTradeAnswerCommand {
        public RoTradeAnswerCommand(bool accept) {
            Accept = accept;
        }

        public bool Accept { get; }
    }

    public readonly struct RoTradeAddItemCommand {
        public RoTradeAddItemCommand(int bagIndex, int amount) {
            BagIndex = bagIndex;
            Amount = amount;
        }

        public int BagIndex { get; }
        public int Amount { get; }
    }
}
