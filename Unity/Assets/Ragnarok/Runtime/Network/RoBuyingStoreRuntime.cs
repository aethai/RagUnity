using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    /// <summary>One wanted item of a buying store (ZC_MYITEMLIST_BUYING_STORE / ZC_ACK_ITEMLIST_BUYING_STORE entry).</summary>
    public readonly struct RoBuyingStoreItem {
        public RoBuyingStoreItem(uint itemId, int amount, uint price, byte itemType) {
            ItemId = itemId;
            Amount = amount;
            Price = price;
            ItemType = itemType;
        }

        public uint ItemId { get; }
        public int Amount { get; }
        public uint Price { get; }
        public byte ItemType { get; }

        public RoBuyingStoreItem WithAmount(int amount) => new RoBuyingStoreItem(ItemId, amount, Price, ItemType);
    }

    /// <summary>A buying store's list: the own one (0x0813, no store id) or another player's (0x0818).</summary>
    public readonly struct RoZcBuyingStoreList {
        public RoZcBuyingStoreList(uint ownerAid, uint storeId, uint zenyLimit, RoBuyingStoreItem[] items) {
            OwnerAid = ownerAid;
            StoreId = storeId;
            ZenyLimit = zenyLimit;
            Items = items ?? Array.Empty<RoBuyingStoreItem>();
        }

        public uint OwnerAid { get; }
        public uint StoreId { get; }
        public uint ZenyLimit { get; }
        public RoBuyingStoreItem[] Items { get; }
    }

    /// <summary>
    /// A buying store answer with a code and a value: ZC_ACK_OPEN_BUYING_STORE (slots), ZC_FAILED_OPEN_BUYING_STORE (result,
    /// weight), ZC_FAILED_TRADE_BUYING_STORE_TO_BUYER (result), ZC_FAILED_TRADE_BUYING_STORE_TO_SELLER (result, item id).
    /// </summary>
    public readonly struct RoZcBuyingStoreResult {
        public RoZcBuyingStoreResult(int result, uint value) {
            Result = result;
            Value = value;
        }

        public int Result { get; }
        public uint Value { get; }
    }

    /// <summary>ZC_BUYING_STORE_ENTRY / ZC_DISAPPEAR_BUYING_STORE_ENTRY: a buyer's board (the title empty for a removal).</summary>
    public readonly struct RoZcBuyingStoreEntry {
        public RoZcBuyingStoreEntry(uint ownerAid, string title) {
            OwnerAid = ownerAid;
            Title = title ?? string.Empty;
        }

        public uint OwnerAid { get; }
        public string Title { get; }
    }

    /// <summary>ZC_DELETE_ITEM_FROM_BUYING_STORE: the seller's item left for the buyer (index as sent, amount, unit price).</summary>
    public readonly struct RoZcBuyingStoreSold {
        public RoZcBuyingStoreSold(ushort bagIndex, ushort amount, uint price) {
            BagIndex = bagIndex;
            Amount = amount;
            Price = price;
        }

        public ushort BagIndex { get; }
        public ushort Amount { get; }
        public uint Price { get; }
    }

    /// <summary>ZC_UPDATE_ITEM_FROM_BUYING_STORE: the buyer got <see cref="Amount"/> of an item for <see cref="Zeny"/>.</summary>
    public readonly struct RoZcBuyingStoreUpdate {
        public RoZcBuyingStoreUpdate(uint itemId, ushort amount, uint zeny, uint zenyLimit, uint sellerCharacterId) {
            ItemId = itemId;
            Amount = amount;
            Zeny = zeny;
            ZenyLimit = zenyLimit;
            SellerCharacterId = sellerCharacterId;
        }

        public uint ItemId { get; }
        public ushort Amount { get; }
        public uint Zeny { get; }
        public uint ZenyLimit { get; }
        public uint SellerCharacterId { get; }
    }

    public readonly struct RoBuyingStoreBoard {
        public RoBuyingStoreBoard(uint ownerAid, string title) {
            OwnerAid = ownerAid;
            Title = title ?? string.Empty;
        }

        public uint OwnerAid { get; }
        public string Title { get; }
    }

    public sealed class RoBuyingStoreSnapshot {
        public static readonly RoBuyingStoreSnapshot Empty = new RoBuyingStoreSnapshot();

        /// <summary>ZC_ACK_OPEN_BUYING_STORE: the slots the setup may use (0 = no setup).</summary>
        public int SetupSlots { get; internal set; }
        /// <summary>Counts the setup grants (each opens the setup window once).</summary>
        public int SetupSerial { get; internal set; }
        public int LastOpenFailure { get; internal set; } = -1;
        public uint LastOpenFailureWeight { get; internal set; }
        public int OpenFailureSerial { get; internal set; }
        public bool IsOwnStoreOpen { get; internal set; }
        public uint OwnStoreAid { get; internal set; }
        public string OwnTitle { get; internal set; } = string.Empty;
        public uint OwnZenyLimit { get; internal set; }
        public RoBuyingStoreItem[] OwnItems { get; internal set; } = Array.Empty<RoBuyingStoreItem>();
        public RoBuyingStoreBoard[] Boards { get; internal set; } = Array.Empty<RoBuyingStoreBoard>();
        /// <summary>The buyer's list a seller looks at (ZC_ACK_ITEMLIST_BUYING_STORE).</summary>
        public RoZcBuyingStoreList Shop { get; internal set; }
        /// <summary>Counts the lists received (a new one reopens the seller windows).</summary>
        public int ShopSerial { get; internal set; }
        public int LastBuyerResult { get; internal set; } = -1;
        public int BuyerResultSerial { get; internal set; }
        public RoZcBuyingStoreResult LastSellerFailure { get; internal set; }
        public int SellerFailureSerial { get; internal set; }
        /// <summary>The seller's sales, oldest first (at most 32); <see cref="SoldSerial"/> counts every one.</summary>
        public RoZcBuyingStoreSold[] Sold { get; internal set; } = Array.Empty<RoZcBuyingStoreSold>();
        public int SoldSerial { get; internal set; }
        public int Sequence { get; internal set; }
    }

    /// <summary>
    /// The buying stores (Hercules buyingstore.c / clif_buyingstore_*): the setup grant, the own store (its list, budget and
    /// board), the boards in sight, the list a seller opened and the answers of both sides. Not thread-safe: the session
    /// applies it under its lock.
    /// </summary>
    public sealed class RoBuyingStoreRuntime {
        private const int MaxSold = 32;
        private readonly Dictionary<uint, string> boards = new Dictionary<uint, string>();
        private readonly List<RoZcBuyingStoreSold> sold = new List<RoZcBuyingStoreSold>();
        private RoBuyingStoreItem[] ownItems = Array.Empty<RoBuyingStoreItem>();
        private int setupSlots;
        private int setupSerial;
        private int lastOpenFailure = -1;
        private uint lastOpenFailureWeight;
        private int openFailureSerial;
        private bool ownOpen;
        private uint ownAid;
        private string ownTitle = string.Empty;
        private string pendingTitle = string.Empty;
        private uint ownZenyLimit;
        private RoZcBuyingStoreList shop;
        private int shopSerial;
        private int lastBuyerResult = -1;
        private int buyerResultSerial;
        private RoZcBuyingStoreResult lastSellerFailure;
        private int sellerFailureSerial;
        private int soldSerial;
        private int sequence;

        public bool IsOwnStoreOpen => ownOpen;

        public RoBuyingStoreSnapshot Snapshot() {
            var list = new RoBuyingStoreBoard[boards.Count];
            int i = 0;
            foreach (KeyValuePair<uint, string> board in boards) list[i++] = new RoBuyingStoreBoard(board.Key, board.Value);
            return new RoBuyingStoreSnapshot {
                SetupSlots = setupSlots,
                SetupSerial = setupSerial,
                LastOpenFailure = lastOpenFailure,
                LastOpenFailureWeight = lastOpenFailureWeight,
                OpenFailureSerial = openFailureSerial,
                IsOwnStoreOpen = ownOpen,
                OwnStoreAid = ownAid,
                OwnTitle = ownTitle,
                OwnZenyLimit = ownZenyLimit,
                OwnItems = (RoBuyingStoreItem[]) ownItems.Clone(),
                Boards = list,
                Shop = shop,
                ShopSerial = shopSerial,
                LastBuyerResult = lastBuyerResult,
                BuyerResultSerial = buyerResultSerial,
                LastSellerFailure = lastSellerFailure,
                SellerFailureSerial = sellerFailureSerial,
                Sold = sold.ToArray(),
                SoldSerial = soldSerial,
                Sequence = sequence,
            };
        }

        /// <summary>ZC_ACK_OPEN_BUYING_STORE (0x0810): the setup may open with this many slots.</summary>
        public void ApplySetup(int slots) {
            setupSlots = Math.Max(0, slots);
            setupSerial++;
            sequence++;
        }

        /// <summary>CZ_REQ_OPEN_BUYING_STORE: the title for the own board (the answer carries none).</summary>
        public void RememberOpen(string title) {
            pendingTitle = title ?? string.Empty;
        }

        /// <summary>The setup was cancelled (CZ_REQ_OPEN_BUYING_STORE with result 0): the grant is used up.</summary>
        public void CancelSetup() {
            if (setupSlots == 0) return;
            setupSlots = 0;
            sequence++;
        }

        /// <summary>ZC_FAILED_OPEN_BUYING_STORE (0x0812).</summary>
        public void ApplyOpenFailed(int result, uint weight) {
            lastOpenFailure = result;
            lastOpenFailureWeight = weight;
            openFailureSerial++;
            setupSlots = 0;
            sequence++;
        }

        /// <summary>ZC_MYITEMLIST_BUYING_STORE (0x0813): the own store is open with this list and budget.</summary>
        public void ApplyOwnList(RoZcBuyingStoreList list, uint localAid) {
            ownOpen = true;
            ownAid = list.OwnerAid != 0 ? list.OwnerAid : localAid;
            ownZenyLimit = list.ZenyLimit;
            ownItems = list.Items;
            if (pendingTitle.Length > 0) ownTitle = pendingTitle;
            setupSlots = 0;
            sequence++;
        }

        /// <summary>ZC_UPDATE_ITEM_FROM_BUYING_STORE (0x09e6): an item came in; the budget is the server's.</summary>
        public void ApplyUpdate(RoZcBuyingStoreUpdate update) {
            for (int i = 0; i < ownItems.Length; i++) {
                if (ownItems[i].ItemId == update.ItemId) ownItems[i] = ownItems[i].WithAmount(Math.Max(0, ownItems[i].Amount - update.Amount));
            }

            ownZenyLimit = update.ZenyLimit;
            sequence++;
        }

        /// <summary>
        /// ZC_FAILED_TRADE_BUYING_STORE_TO_BUYER (0x081a): the store closed after a trade (3 budget spent, 4 all bought);
        /// the server removes the board only for the others.
        /// </summary>
        public void ApplyBuyerFailed(int result) {
            lastBuyerResult = result;
            buyerResultSerial++;
            CloseOwn();
            sequence++;
        }

        /// <summary>CZ_REQ_CLOSE_BUYING_STORE: the client closes its own store at once (the others get the removal).</summary>
        public void CloseOwnLocally() {
            if (!ownOpen) return;
            CloseOwn();
            sequence++;
        }

        private void CloseOwn() {
            ownOpen = false;
            ownItems = Array.Empty<RoBuyingStoreItem>();
            ownZenyLimit = 0;
            ownTitle = string.Empty;
            pendingTitle = string.Empty;
        }

        /// <summary>ZC_BUYING_STORE_ENTRY (0x0814).</summary>
        public void ApplyEntry(RoZcBuyingStoreEntry entry) {
            if (entry.OwnerAid == 0) return;
            boards[entry.OwnerAid] = entry.Title;
            sequence++;
        }

        /// <summary>ZC_DISAPPEAR_BUYING_STORE_ENTRY (0x0816).</summary>
        public void ApplyDisappear(uint ownerAid) {
            if (boards.Remove(ownerAid)) sequence++;
        }

        /// <summary>ZC_ACK_ITEMLIST_BUYING_STORE (0x0818): the list a seller opened.</summary>
        public void ApplyShop(RoZcBuyingStoreList list) {
            shop = list;
            shopSerial++;
            sequence++;
        }

        /// <summary>ZC_DELETE_ITEM_FROM_BUYING_STORE (0x081c): a sale of the seller.</summary>
        public void ApplySold(RoZcBuyingStoreSold sale) {
            sold.Add(sale);
            if (sold.Count > MaxSold) sold.RemoveAt(0);
            soldSerial++;
            sequence++;
        }

        /// <summary>ZC_FAILED_TRADE_BUYING_STORE_TO_SELLER (0x0824).</summary>
        public void ApplySellerFailed(RoZcBuyingStoreResult failure) {
            lastSellerFailure = failure;
            sellerFailureSerial++;
            sequence++;
        }

        public bool ClearMapScopedState() {
            if (boards.Count == 0 && !ownOpen && setupSlots == 0) return false;
            boards.Clear();
            CloseOwn();
            setupSlots = 0;
            sequence++;
            return true;
        }
    }
}
