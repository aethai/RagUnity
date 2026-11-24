using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    public readonly struct RoVendingShopItemSnapshot {
        public RoVendingShopItemSnapshot(
            int index,
            uint itemId,
            byte itemTypeRaw,
            int amount,
            uint price,
            bool isIdentified,
            bool isDamaged,
            byte refineLevel,
            int[] cardIds,
            RoPacketItemRandomOption[] randomOptions,
            uint location,
            ushort viewSprite,
            byte grade) {
            Index = index;
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            Amount = Math.Max(0, amount);
            Price = price;
            IsIdentified = isIdentified;
            IsDamaged = isDamaged;
            RefineLevel = refineLevel;
            CardIds = CopyCardIds(cardIds);
            RandomOptions = CopyRandomOptions(randomOptions);
            Location = location;
            ViewSprite = viewSprite;
            Grade = grade;
        }

        public int Index { get; }
        public uint ItemId { get; }
        public byte ItemTypeRaw { get; }
        public int Amount { get; }
        public uint Price { get; }
        public ulong LineTotal => (ulong) Price * (ulong) Math.Max(0, Amount);
        public bool IsIdentified { get; }
        public bool IsDamaged { get; }
        public byte RefineLevel { get; }
        public int[] CardIds { get; }
        public RoPacketItemRandomOption[] RandomOptions { get; }
        public uint Location { get; }
        public ushort ViewSprite { get; }
        public byte Grade { get; }

        public RoVendingShopItemSnapshot WithAmount(int amount) {
            return new RoVendingShopItemSnapshot(
                Index,
                ItemId,
                ItemTypeRaw,
                amount,
                Price,
                IsIdentified,
                IsDamaged,
                RefineLevel,
                CardIds,
                RandomOptions,
                Location,
                ViewSprite,
                Grade);
        }

        public static RoVendingShopItemSnapshot FromPacketItem(RoPlayerVendingItem item) {
            return new RoVendingShopItemSnapshot(
                item.Index,
                item.ItemId,
                item.ItemTypeRaw,
                item.Amount,
                item.Price,
                item.IsIdentified,
                item.IsDamaged,
                item.RefineLevel,
                item.CardIds,
                item.RandomOptions,
                item.Location,
                item.ViewSprite,
                item.Grade);
        }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[4];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }

        private static RoPacketItemRandomOption[] CopyRandomOptions(RoPacketItemRandomOption[] source) {
            var copy = new RoPacketItemRandomOption[5];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoVendingBoardSnapshot {
        public RoVendingBoardSnapshot(uint ownerAid, string title) {
            OwnerAid = ownerAid;
            Title = title ?? string.Empty;
        }

        public uint OwnerAid { get; }
        public string Title { get; }
    }

    public readonly struct RoPlayerShopSnapshot {
        public RoPlayerShopSnapshot(uint ownerAid, uint uniqueId, bool hasUniqueId, string title, RoVendingShopItemSnapshot[] items, int listSerial = 0) {
            ListSerial = listSerial;
            OwnerAid = ownerAid;
            UniqueId = uniqueId;
            HasUniqueId = hasUniqueId;
            Title = title ?? string.Empty;
            Items = Copy(items);
        }

        public uint OwnerAid { get; }
        public uint UniqueId { get; }
        public bool HasUniqueId { get; }
        public string Title { get; }
        public RoVendingShopItemSnapshot[] Items { get; }

        /// <summary>Which shop list this is (a new ZC_PC_PURCHASE_ITEMLIST_FROMMC, i.e. a new click on the board, counts up).</summary>
        public int ListSerial { get; }

        public RoPlayerShopSnapshot WithItems(RoVendingShopItemSnapshot[] items) {
            return new RoPlayerShopSnapshot(OwnerAid, UniqueId, HasUniqueId, Title, items, ListSerial);
        }

        private static RoVendingShopItemSnapshot[] Copy(RoVendingShopItemSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoVendingShopItemSnapshot>();
            }

            var copy = new RoVendingShopItemSnapshot[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }
    }

    public readonly struct RoVendingSaleSnapshot {
        public RoVendingSaleSnapshot(ushort index, ushort amount, uint buyerCid, uint date, uint zeny, string buyerName) {
            Index = index;
            Amount = amount;
            BuyerCid = buyerCid;
            Date = date;
            Zeny = zeny;
            BuyerName = buyerName ?? string.Empty;
        }

        public ushort Index { get; }
        public ushort Amount { get; }
        public uint BuyerCid { get; }
        public uint Date { get; }
        public uint Zeny { get; }
        public string BuyerName { get; }
    }

    public readonly struct RoVendingPurchaseResultSnapshot {
        public RoVendingPurchaseResultSnapshot(ushort index, ushort currentCount, byte resultRaw, string message) {
            Index = index;
            CurrentCount = currentCount;
            ResultRaw = resultRaw;
            Message = message ?? string.Empty;
        }

        public ushort Index { get; }
        public ushort CurrentCount { get; }
        public byte ResultRaw { get; }
        public bool IsSuccess => ResultRaw == 0;
        public string Message { get; }
    }

    public readonly struct RoVendingSnapshot {
        public RoVendingSnapshot(
            bool setupAllowed,
            int setupSlotLimit,
            bool isOwnStoreOpen,
            uint ownStoreOwnerAid,
            uint ownStoreUniqueId,
            bool ownStoreHasUniqueId,
            string ownStoreTitle,
            RoVendingShopItemSnapshot[] ownStoreItems,
            RoVendingBoardSnapshot[] boards,
            RoPlayerShopSnapshot[] playerShops,
            RoVendingSaleSnapshot[] sales,
            bool hasLastOpenAck,
            byte lastOpenAckResult,
            RoVendingPurchaseResultSnapshot lastPurchaseResult,
            bool hasLastPurchaseResult,
            string lastMessage,
            int sequence) {
            SetupAllowed = setupAllowed;
            SetupSlotLimit = Math.Max(0, setupSlotLimit);
            IsOwnStoreOpen = isOwnStoreOpen;
            OwnStoreOwnerAid = ownStoreOwnerAid;
            OwnStoreUniqueId = ownStoreUniqueId;
            OwnStoreHasUniqueId = ownStoreHasUniqueId;
            OwnStoreTitle = ownStoreTitle ?? string.Empty;
            OwnStoreItems = Copy(ownStoreItems);
            Boards = Copy(boards);
            PlayerShops = Copy(playerShops);
            Sales = Copy(sales);
            HasLastOpenAck = hasLastOpenAck;
            LastOpenAckResult = lastOpenAckResult;
            LastPurchaseResult = lastPurchaseResult;
            HasLastPurchaseResult = hasLastPurchaseResult;
            LastMessage = lastMessage ?? string.Empty;
            Sequence = sequence;
        }

        public static readonly RoVendingSnapshot Empty = new RoVendingSnapshot(
            setupAllowed: false,
            setupSlotLimit: 0,
            isOwnStoreOpen: false,
            ownStoreOwnerAid: 0,
            ownStoreUniqueId: 0,
            ownStoreHasUniqueId: false,
            ownStoreTitle: string.Empty,
            ownStoreItems: Array.Empty<RoVendingShopItemSnapshot>(),
            boards: Array.Empty<RoVendingBoardSnapshot>(),
            playerShops: Array.Empty<RoPlayerShopSnapshot>(),
            sales: Array.Empty<RoVendingSaleSnapshot>(),
            hasLastOpenAck: false,
            lastOpenAckResult: 0,
            lastPurchaseResult: default,
            hasLastPurchaseResult: false,
            lastMessage: string.Empty,
            sequence: 0);

        public bool SetupAllowed { get; }
        public int SetupSlotLimit { get; }
        public bool IsOwnStoreOpen { get; }
        public uint OwnStoreOwnerAid { get; }
        public uint OwnStoreUniqueId { get; }
        public bool OwnStoreHasUniqueId { get; }
        public string OwnStoreTitle { get; }
        public RoVendingShopItemSnapshot[] OwnStoreItems { get; }
        public RoVendingBoardSnapshot[] Boards { get; }
        public RoPlayerShopSnapshot[] PlayerShops { get; }
        public RoVendingSaleSnapshot[] Sales { get; }
        public bool HasLastOpenAck { get; }
        public byte LastOpenAckResult { get; }
        public RoVendingPurchaseResultSnapshot LastPurchaseResult { get; }
        public bool HasLastPurchaseResult { get; }
        public string LastMessage { get; }
        public int Sequence { get; }

        private static T[] Copy<T>(T[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<T>();
            }

            var copy = new T[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }
    }

    public sealed class RoVendingRuntime {
        private readonly Dictionary<uint, RoVendingBoardSnapshot> boards = new Dictionary<uint, RoVendingBoardSnapshot>();
        private readonly Dictionary<uint, RoPlayerShopSnapshot> playerShops = new Dictionary<uint, RoPlayerShopSnapshot>();
        private readonly List<RoVendingSaleSnapshot> sales = new List<RoVendingSaleSnapshot>();
        private bool setupAllowed;
        private int setupSlotLimit;
        private bool isOwnStoreOpen;
        private uint ownStoreOwnerAid;
        private uint ownStoreUniqueId;
        private bool ownStoreHasUniqueId;
        private string ownStoreTitle = string.Empty;
        private string requestedTitle = string.Empty;
        private RoVendingShopItemSnapshot[] ownStoreItems = Array.Empty<RoVendingShopItemSnapshot>();
        private bool hasLastOpenAck;
        private byte lastOpenAckResult;
        private bool hasLastPurchaseResult;
        private RoVendingPurchaseResultSnapshot lastPurchaseResult;
        private string lastMessage = string.Empty;
        private int sequence;
        private int playerShopListSerial;

        public RoVendingSnapshot Snapshot() {
            var boardValues = new RoVendingBoardSnapshot[boards.Count];
            boards.Values.CopyTo(boardValues, 0);
            var shopValues = new RoPlayerShopSnapshot[playerShops.Count];
            playerShops.Values.CopyTo(shopValues, 0);
            return new RoVendingSnapshot(
                setupAllowed,
                setupSlotLimit,
                isOwnStoreOpen,
                ownStoreOwnerAid,
                ownStoreUniqueId,
                ownStoreHasUniqueId,
                ownStoreTitle,
                ownStoreItems,
                boardValues,
                shopValues,
                sales.ToArray(),
                hasLastOpenAck,
                lastOpenAckResult,
                lastPurchaseResult,
                hasLastPurchaseResult,
                lastMessage,
                sequence);
        }

        public void RememberRequestedTitle(string title) {
            requestedTitle = title ?? string.Empty;
            Touch();
        }

        public void CancelSetupLocally() {
            setupAllowed = false;
            setupSlotLimit = 0;
            lastMessage = "Vending setup cancelled.";
            Touch();
        }

        public bool CloseOwnStoreLocally(uint localActorId = 0) {
            bool changed = isOwnStoreOpen || ownStoreOwnerAid != 0 || ownStoreItems.Length > 0;
            if (ownStoreOwnerAid != 0) {
                changed |= boards.Remove(ownStoreOwnerAid);
                playerShops.Remove(ownStoreOwnerAid);
            }

            if (localActorId != 0) {
                changed |= boards.Remove(localActorId);
                playerShops.Remove(localActorId);
            }

            if (!changed) {
                return false;
            }

            ClearOwnStore();
            lastMessage = "Your vending store closed.";
            Touch();
            return true;
        }

        public bool Apply(RoMapEntryDrainEvent drainEvent, uint localActorId) {
            if (drainEvent == null) {
                return false;
            }

            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.VendingSetupAllowed:
                    setupAllowed = true;
                    setupSlotLimit = drainEvent.VendingOpenStore.SlotCount;
                    hasLastOpenAck = false;
                    lastMessage = $"Vending setup allowed for {setupSlotLimit} slot(s).";
                    Touch();
                    return true;
                case RoMapEntryDrainEventKind.VendingBoardAdded:
                    ApplyBoardAdded(drainEvent.VendingStoreEntry, localActorId);
                    Touch();
                    return true;
                case RoMapEntryDrainEventKind.VendingBoardRemoved:
                    ApplyBoardRemoved(drainEvent.VendingDisappearEntry.OwnerAid, localActorId);
                    Touch();
                    return true;
                case RoMapEntryDrainEventKind.PlayerShopListReceived:
                    ApplyPlayerShopList(drainEvent.VendingItemList);
                    Touch();
                    return true;
                case RoMapEntryDrainEventKind.VendingOpenAccepted:
                    ApplyOwnShopList(drainEvent.VendingItemList, localActorId);
                    Touch();
                    return true;
                case RoMapEntryDrainEventKind.VendingOpenAck:
                    hasLastOpenAck = true;
                    lastOpenAckResult = drainEvent.VendingOpenAck.ResultRaw;
                    if (drainEvent.VendingOpenAck.IsSuccess) {
                        setupAllowed = false;
                    }

                    lastMessage = drainEvent.VendingOpenAck.IsSuccess
                        ? "Vending store accepted."
                        : FormatOpenStoreFailure(drainEvent.VendingOpenAck.ResultRaw);
                    Touch();
                    return true;
                case RoMapEntryDrainEventKind.PlayerShopPurchaseResult:
                    ApplyPurchaseResult(drainEvent.VendingPurchaseResult);
                    Touch();
                    return true;
                case RoMapEntryDrainEventKind.VendingSaleReported:
                    ApplySaleReport(drainEvent.VendingSaleReport);
                    Touch();
                    return true;
                case RoMapEntryDrainEventKind.VendingClosed:
                    ClearOwnStore();
                    Touch();
                    return true;
                default:
                    return false;
            }
        }

        public bool ApplyDrain(RoMapEntryDrainResult drain, uint localActorId) {
            if (drain?.OrderedEvents == null || drain.OrderedEvents.Length == 0) {
                return false;
            }

            bool changed = false;
            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                changed |= Apply(events[i], localActorId);
            }

            return changed;
        }

        public bool ClearMapScopedState() {
            if (!setupAllowed &&
                setupSlotLimit == 0 &&
                !isOwnStoreOpen &&
                boards.Count == 0 &&
                playerShops.Count == 0) {
                return false;
            }

            setupAllowed = false;
            setupSlotLimit = 0;
            boards.Clear();
            playerShops.Clear();
            ClearOwnStore();
            lastMessage = "Vending state cleared.";
            Touch();
            return true;
        }

        public static string FormatPurchaseResult(byte result) {
            switch (result) {
                case 0: return "Purchase completed.";
                case 1: return "Not enough zeny.";
                case 2: return "You are overweight.";
                case 4: return "The item is out of stock.";
                case 5: return "Cannot buy while trading.";
                case 6: return "Store information is incorrect.";
                case 7: return "No sales information is available.";
                default: return "Deal failed.";
            }
        }

        private void ApplyBoardAdded(RoZcStoreEntry storeEntry, uint localActorId) {
            if (localActorId != 0 && storeEntry.OwnerAid == localActorId && !isOwnStoreOpen) {
                ownStoreOwnerAid = 0;
                ownStoreTitle = string.Empty;
                lastMessage = "Ignored stale local vending signboard.";
                return;
            }

            boards[storeEntry.OwnerAid] = new RoVendingBoardSnapshot(storeEntry.OwnerAid, storeEntry.StoreName);
            if (storeEntry.OwnerAid == localActorId) {
                ownStoreOwnerAid = storeEntry.OwnerAid;
                ownStoreTitle = string.IsNullOrWhiteSpace(storeEntry.StoreName) ? ownStoreTitle : storeEntry.StoreName;
            }

            lastMessage = string.IsNullOrWhiteSpace(storeEntry.StoreName)
                ? "Vending signboard appeared."
                : $"Vending signboard appeared: {storeEntry.StoreName}";
        }

        private void ApplyBoardRemoved(uint ownerAid, uint localActorId) {
            boards.Remove(ownerAid);
            playerShops.Remove(ownerAid);
            if (ownerAid == localActorId || ownerAid == ownStoreOwnerAid) {
                ClearOwnStore();
                lastMessage = "Your vending store closed.";
            } else {
                lastMessage = "Vending signboard disappeared.";
            }
        }

        private void ApplyPlayerShopList(RoZcPlayerVendingItemList list) {
            string title = boards.TryGetValue(list.OwnerAid, out RoVendingBoardSnapshot board)
                ? board.Title
                : string.Empty;
            playerShops[list.OwnerAid] = new RoPlayerShopSnapshot(
                list.OwnerAid,
                list.UniqueId,
                list.HasUniqueId,
                title,
                ConvertItems(list.Items),
                ++playerShopListSerial);
            lastMessage = "Player shop list received.";
        }

        private void ApplyOwnShopList(RoZcPlayerVendingItemList list, uint localActorId) {
            isOwnStoreOpen = true;
            setupAllowed = false;
            ownStoreOwnerAid = list.OwnerAid != 0 ? list.OwnerAid : localActorId;
            ownStoreUniqueId = list.UniqueId;
            ownStoreHasUniqueId = list.HasUniqueId;
            if (boards.TryGetValue(ownStoreOwnerAid, out RoVendingBoardSnapshot board) && !string.IsNullOrWhiteSpace(board.Title)) {
                ownStoreTitle = board.Title;
            } else if (!string.IsNullOrWhiteSpace(requestedTitle)) {
                ownStoreTitle = requestedTitle;
            }

            ownStoreItems = ConvertItems(list.Items);
            lastMessage = "Your vending store is open.";
        }

        private void ApplyPurchaseResult(RoZcPlayerVendingPurchaseResult result) {
            string message = FormatPurchaseResult(result.ResultRaw);
            lastPurchaseResult = new RoVendingPurchaseResultSnapshot(result.Index, result.CurrentCount, result.ResultRaw, message);
            hasLastPurchaseResult = true;
            lastMessage = message;
            if (result.IsSuccess) {
                UpdatePlayerShopItemCount(result.Index, result.CurrentCount);
            }
        }

        private void ApplySaleReport(RoZcDeleteItemFromMcStore saleReport) {
            sales.Add(new RoVendingSaleSnapshot(
                saleReport.Index,
                saleReport.Amount,
                saleReport.BuyerCid,
                saleReport.Date,
                saleReport.Zeny,
                saleReport.BuyerCid == 0 ? string.Empty : $"CID {saleReport.BuyerCid}"));
            ownStoreItems = DecreaseItemCount(ownStoreItems, saleReport.Index, saleReport.Amount);
            lastMessage = saleReport.HasBuyerDetails
                ? $"Sold {saleReport.Amount} item(s) for {saleReport.Zeny} zeny."
                : $"Sold {saleReport.Amount} item(s).";
        }

        private void UpdatePlayerShopItemCount(ushort index, ushort currentCount) {
            if (playerShops.Count == 0) {
                return;
            }

            var keys = new List<uint>(playerShops.Keys);
            for (int i = 0; i < keys.Count; i++) {
                RoPlayerShopSnapshot shop = playerShops[keys[i]];
                RoVendingShopItemSnapshot[] updated = SetItemCount(shop.Items, index, currentCount);
                if (!ReferenceEquals(updated, shop.Items)) {
                    playerShops[keys[i]] = shop.WithItems(updated);
                }
            }
        }

        private static RoVendingShopItemSnapshot[] ConvertItems(RoPlayerVendingItem[] items) {
            if (items == null || items.Length == 0) {
                return Array.Empty<RoVendingShopItemSnapshot>();
            }

            var result = new RoVendingShopItemSnapshot[items.Length];
            for (int i = 0; i < items.Length; i++) {
                result[i] = RoVendingShopItemSnapshot.FromPacketItem(items[i]);
            }

            return result;
        }

        private static RoVendingShopItemSnapshot[] DecreaseItemCount(RoVendingShopItemSnapshot[] items, ushort index, ushort amount) {
            if (items == null || items.Length == 0) {
                return Array.Empty<RoVendingShopItemSnapshot>();
            }

            var updated = new List<RoVendingShopItemSnapshot>(items.Length);
            for (int i = 0; i < items.Length; i++) {
                RoVendingShopItemSnapshot item = items[i];
                if (item.Index == index) {
                    int nextAmount = Math.Max(0, item.Amount - amount);
                    if (nextAmount > 0) {
                        updated.Add(item.WithAmount(nextAmount));
                    }
                } else {
                    updated.Add(item);
                }
            }

            return updated.ToArray();
        }

        private static RoVendingShopItemSnapshot[] SetItemCount(RoVendingShopItemSnapshot[] items, ushort index, ushort count) {
            if (items == null || items.Length == 0) {
                return Array.Empty<RoVendingShopItemSnapshot>();
            }

            bool changed = false;
            var updated = new List<RoVendingShopItemSnapshot>(items.Length);
            for (int i = 0; i < items.Length; i++) {
                RoVendingShopItemSnapshot item = items[i];
                if (item.Index == index) {
                    changed = true;
                    if (count > 0) {
                        updated.Add(item.WithAmount(count));
                    }
                } else {
                    updated.Add(item);
                }
            }

            return changed ? updated.ToArray() : items;
        }

        private void ClearOwnStore() {
            isOwnStoreOpen = false;
            ownStoreOwnerAid = 0;
            ownStoreUniqueId = 0;
            ownStoreHasUniqueId = false;
            ownStoreTitle = string.Empty;
            ownStoreItems = Array.Empty<RoVendingShopItemSnapshot>();
        }

        private void Touch() {
            unchecked {
                sequence++;
            }
        }

        private static string FormatOpenStoreFailure(byte result) {
            return result == 0
                ? "Vending store accepted."
                : $"Vending store failed to open ({result}).";
        }
    }
}
