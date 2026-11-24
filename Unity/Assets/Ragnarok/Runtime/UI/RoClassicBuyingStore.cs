using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>An item dragged between the buying store windows (setup 0xae / its mirror 0xaf, sell list 0xb2 / mirror 0xb3).</summary>
    internal sealed class RoClassicBuyingPayload : IRoClassicDragPayload {
        public int Source; // 0 setup mirror 0xaf, 1 setup 0xae, 2 seller mirror 0xb3, 3 sell list 0xb2
        public RoItemInstance Item;
        public string IconPath { get; set; }
    }

    /// <summary>
    /// The classic buying stores (codex-ui-research RUNTIME-RULES-TRADE-CART-VENDING-CHATROOM.md "Vending and buying stores",
    /// the 0xae–0xb3 range of the merchant classes). The buyer: UIMerchantShopMakeWnd 0xae (oracle .id174, 360 × 289: the
    /// store name, one row per granted slot with the wanted amount and the price, the Zeny limit, Money and Weight) with
    /// UIMerchantMirrorItemWnd 0xaf (the inventory, .id175); OK sends CZ_REQ_OPEN_BUYING_STORE with result 1, Cancel with 0.
    /// UIMerchantItemMyShopWnd 0xb0 (.id176) is the open store (the wanted items, "Price limit: %s Zeny", Close =
    /// CZ_REQ_CLOSE_BUYING_STORE). The seller: UIMerchantItemShopWnd 0xb1 (the buyer's list, 344 wide at (0, 120), the price
    /// limit at (12, H − 21)), UIMerchantItemPurchaseWnd 0xb2 (.id178, what to sell; Sell checks the buyer's limit, MsgStr
    /// 0x6cc, then CZ_REQ_TRADE_BUYING_STORE) and UIMerchantMirrorItemWnd 0xb3 (.id179, the own items the buyer wants).
    /// The seller's offer and selection panes follow reconstruction/apps/native_merchant_shop.cpp: one item per 32px
    /// row, at least four rows plus 56px frame padding, names and prices alongside the icons, totals in the footer.
    /// Driven by the session's buying store state, ticked by the chat window while the classic UI is on.
    /// </summary>
    public sealed class RoClassicBuyingStore : IRoClassicUiProducer {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string Ui = "유저인터페이스\\";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const string Prefs = "ro.ui.classic.buyingstore.";
        private const int OkId = 0xb4;
        private const int CancelId = 0xb5;
        private const int CheckId = 0xd1;
        private const int CloseId = 0xc5;
        private const int RowH = 42;
        private const int VisibleRows = 4;
        private const int Cell = 32;
        private const uint MaxPrice = 99990000;   // BUYINGSTORE_MAX_PRICE
        private const int MaxAmount = 9999;       // BUYINGSTORE_MAX_AMOUNT
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private static readonly Color32 EditFill = new Color32(0xe8, 0xe8, 0xe8, 255);

        private sealed class Entry {
            public RoItemInstance Item;
            public string Amount = string.Empty;
            public string Price = string.Empty;
        }

        private static RoClassicBuyingStore instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private RoBuyingStoreSnapshot state = RoBuyingStoreSnapshot.Empty;
        private int seenSequence = -1;
        private int seenInventorySequence = -1;
        private int seenSetupSerial;
        private int seenFailureSerial;
        private int seenShopSerial;
        private int seenBuyerSerial;
        private int seenSellerSerial;
        private int seenSoldSerial;

        // 0xae / 0xaf
        private RoClassicWindow make;
        private RoClassicWindow mirror;
        private RoClassicEditField titleEdit;
        private RoClassicEditField limitEdit;
        private readonly RoClassicEditField[] amountEdits = new RoClassicEditField[VisibleRows];
        private readonly RoClassicEditField[] priceEdits = new RoClassicEditField[VisibleRows];
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<RoItemInstance> mirrorList = new List<RoItemInstance>();
        private int slotLimit;
        private int makeFirst;
        private int mirrorFirstRow;
        private bool setupShown;

        // 0xb0
        private RoClassicWindow myShop;
        private int myShopFirstRow;
        private bool notifyBought;

        // 0xb1 / 0xb2 / 0xb3
        private RoClassicWindow wantWin;
        private RoClassicWindow sellWin;
        private RoClassicWindow sellMirror;
        private readonly List<(RoItemInstance Item, int Amount)> sellList = new List<(RoItemInstance, int)>();
        private readonly List<RoItemInstance> sellMirrorList = new List<RoItemInstance>();
        private readonly Dictionary<int, RoItemInstance> soldItems = new Dictionary<int, RoItemInstance>();
        private int wantFirstRow;
        private int wantSelected = -1;
        private int sellFirst;
        private int sellMirrorFirstRow;
        private bool sellerShown;

        private static RoClassicBuyingStore Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicBuyingStore { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Per frame from the chat window while the classic UI is on.</summary>
        public static void Tick(RoLoginShellSession session, RoClassicChat chat, RoItemMetadataTable metadata) {
            if (session == null) {
                instance?.HideAll();
                return;
            }

            RoClassicBuyingStore b = Ensure();
            if (b == null) return;
            if (!ReferenceEquals(b.session, session)) {
                b.HideAll();
                b.session = session;
                b.seenSequence = -1;
                RoBuyingStoreSnapshot first = session.BuyingStoreSnapshot();
                b.seenSetupSerial = first.SetupSerial;
                b.seenFailureSerial = first.OpenFailureSerial;
                b.seenShopSerial = first.ShopSerial;
                b.seenBuyerSerial = first.BuyerResultSerial;
                b.seenSellerSerial = first.SellerFailureSerial;
                b.seenSoldSerial = first.SoldSerial;
            }

            b.chat = chat;
            b.metadata = metadata ?? RoItemMetadataTable.Empty;
            int sequence = session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.BuyingStore);
            if (sequence != b.seenSequence) {
                b.seenSequence = sequence;
                b.Apply(session.BuyingStoreSnapshot());
            }

            if (b.setupShown || b.sellerShown) {
                int inventory = session.InventoryWindowChangeSequence;
                if (inventory != b.seenInventorySequence) {
                    b.seenInventorySequence = inventory;
                    if (b.setupShown) b.RebuildMirror();
                    if (b.sellerShown) b.RebuildSellMirror();
                }
            }
        }

        private void HideAll() {
            CloseSetup();
            if (myShop != null) myShop.Visible = false;
            CloseSeller();
            RoClassicActorBoards.SyncBuying(Array.Empty<RoBuyingStoreBoard>(), 0, null);
        }

        private void Apply(RoBuyingStoreSnapshot snapshot) {
            state = snapshot ?? RoBuyingStoreSnapshot.Empty;
            if (state.SetupSerial != seenSetupSerial) {
                seenSetupSerial = state.SetupSerial;
                // 0x0810 → 0xc5be60: MakeWindow(0xae) with the granted slots (0xaf comes with it)
                if (state.SetupSlots > 0) OpenSetup(state.SetupSlots);
            }

            if (state.OpenFailureSerial != seenFailureSerial) {
                seenFailureSerial = state.OpenFailureSerial;
                ShowOpenFailure(state.LastOpenFailure);
            }

            if (state.IsOwnStoreOpen) {
                // 0x0813 → 0xc54790: the setup closes, 0xb0 opens
                CloseSetup();
                OpenMyShop();
            } else if (myShop != null && myShop.Visible) {
                myShop.Visible = false;
            }

            if (state.BuyerResultSerial != seenBuyerSerial) {
                seenBuyerSerial = state.BuyerResultSerial;
                // 0x081a (0xc4bd00): 3 MsgStr 0x6cf, 4 0x6d0, then 0xb0 goes
                if (state.LastBuyerResult == 3) Chat(RoClassicUiHost.MsgStr(0x6cf, "You have purchased all items within the limited price."), "#FF0000");
                else if (state.LastBuyerResult == 4) Chat(RoClassicUiHost.MsgStr(0x6d0, "You purchased all items."), "#FF0000");
            }

            if (state.ShopSerial != seenShopSerial) {
                seenShopSerial = state.ShopSerial;
                // 0x0818 → 0xc3f510: 0xb1 / 0xb2 / 0xb3
                OpenSeller();
            } else if (sellerShown) {
                wantWin.Invalidate();
            }

            if (state.SellerFailureSerial != seenSellerSerial) {
                seenSellerSerial = state.SellerFailureSerial;
                ShowSellerFailure(state.LastSellerFailure);
            }

            if (state.SoldSerial != seenSoldSerial) {
                int fresh = Math.Min(state.Sold.Length, state.SoldSerial - seenSoldSerial);
                seenSoldSerial = state.SoldSerial;
                for (int i = state.Sold.Length - fresh; i < state.Sold.Length; i++) ShowSold(state.Sold[i]);
            }

            myShop?.Invalidate();
            RoClassicActorBoards.SyncBuying(BoardsWithOwn(), session != null ? session.LocalActorId : 0, aid => {
                if (session != null) _ = session.BeginLegacyClickBuyingStore(aid);
            });
        }

        /// <summary>The boards in sight and the own one (the server announces it to the others only).</summary>
        private RoBuyingStoreBoard[] BoardsWithOwn() {
            if (!state.IsOwnStoreOpen || state.OwnStoreAid == 0) return state.Boards;
            foreach (RoBuyingStoreBoard board in state.Boards) {
                if (board.OwnerAid == state.OwnStoreAid) return state.Boards;
            }

            var boards = new RoBuyingStoreBoard[state.Boards.Length + 1];
            Array.Copy(state.Boards, boards, state.Boards.Length);
            boards[boards.Length - 1] = new RoBuyingStoreBoard(state.OwnStoreAid, state.OwnTitle);
            return boards;
        }

        private void Chat(string text, string color) {
            if (string.IsNullOrEmpty(text)) return;
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, color);
        }

        private string ItemName(uint itemId) {
            RoItemInstance item = RoItemInstance.CreateRegular(0, unchecked((int) itemId), 1);
            return item.GetFullDisplayName(metadata);
        }

        private static string Format(string pattern, params string[] values) {
            var s = new StringBuilder(pattern);
            foreach (string value in values) {
                string text = s.ToString();
                int at = -1;
                for (int i = 0; i + 1 < text.Length; i++) {
                    if (text[i] == '%' && (text[i + 1] == 's' || text[i + 1] == 'd')) {
                        at = i;
                        break;
                    }
                }

                if (at < 0) break;
                s.Remove(at, 2).Insert(at, value);
            }

            return s.ToString();
        }

        /// <summary>0x0812 (0xc4bb40): 1 MsgStr 0x6cd, 2 a 280 × 120 box with 0x6ce, 9 0x705, 10 0xc9d and the setup closes.</summary>
        private void ShowOpenFailure(int result) {
            switch (result) {
                case 1:
                    Chat(RoClassicUiHost.MsgStr(0x6cd, "Failed to open purchase shop."), "#FF0000");
                    break;
                case 2:
                    RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x6ce, "You exceed the total amount of items."), RoClassicMessageBox.TypeOk, true, _ => { });
                    break;
                case 9:
                    Chat(RoClassicUiHost.MsgStr(0x705, "No sales information."), "#FF0000");
                    break;
                case 10:
                    Chat(RoClassicUiHost.MsgStr(0xc9d, "Unable to open a shop at the current location"), "#FF0000");
                    CloseSetup();
                    break;
            }
        }

        /// <summary>0x0824 (0xc4bda0): 5 MsgStr 0x39, 6 0x6d3 with the item, 7 0x6d1; then 0xb1 / 0xb2 / 0xb3 close.</summary>
        private void ShowSellerFailure(RoZcBuyingStoreResult failure) {
            switch (failure.Result) {
                case 5:
                    Chat(RoClassicUiHost.MsgStr(0x39, "The deal has failed."), "#FF0000");
                    break;
                case 6:
                    Chat(Format(RoClassicUiHost.MsgStr(0x6d3, "%s item could not be sold because you do not have the wanted amount of items."), ItemName(failure.Value)), "#FF0000");
                    break;
                case 7:
                    Chat(RoClassicUiHost.MsgStr(0x6d1, "Failed to deal because you have insufficient zeny."), "#FF0000");
                    break;
                default:
                    return;
            }

            CloseSeller();
        }

        /// <summary>0x081c (0xc4f4e0): MsgStr 0x6d2 "You have sold %s. Amount: %d. Total Zeny: %dz" in 0x00FFFF64.</summary>
        private void ShowSold(RoZcBuyingStoreSold sale) {
            string name = soldItems.TryGetValue(sale.BagIndex, out RoItemInstance item) ? item.GetFullDisplayName(metadata) : "#" + sale.BagIndex.ToString(CultureInfo.InvariantCulture);
            Chat(Format(RoClassicUiHost.MsgStr(0x6d2, "You have sold %s. Amount: %d. Total Zeny: %dz"), name, sale.Amount.ToString(CultureInfo.InvariantCulture),
                ((long) sale.Amount * sale.Price).ToString(CultureInfo.InvariantCulture)), "#64FFFF");
        }

        private static void Place(RoClassicWindow window, int x, int y, Vector2Int client) =>
            window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - window.Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - window.Height)));

        private static RoClassicChildLayout Find(RoClassicWindow window, int id) {
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                if (child.Id == id) return child;
            }

            return null;
        }

        private static void SyncCheckBox(RoClassicWindow window, int id, bool on) {
            RoClassicChildLayout check = Find(window, id);
            if (check == null) return;
            foreach (RoClassicOp op in check.Ops) {
                if (op.Kind == RoClassicOpKind.Bitmap && op.Path != null) op.Path = Ui + (on ? "checkbox_1.bmp" : "checkbox_0.bmp");
            }

            window.Invalidate();
        }

        private static string Digits(string text) {
            var s = new StringBuilder(text.Length);
            foreach (char ch in text) {
                if (ch >= '0' && ch <= '9') s.Append(ch);
            }

            return s.ToString();
        }

        private static uint ParseUInt(string text) =>
            uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint n) ? n : 0;

        private RoItemInstance[] Inventory() =>
            (session != null ? session.InventorySnapshot().InventoryItems : null) ?? Array.Empty<RoItemInstance>();

        private int Owned(int itemId) {
            int n = 0;
            foreach (RoItemInstance item in Inventory()) {
                if (item != null && item.ItemId == itemId) n += item.Count;
            }

            return n;
        }

        private static void DrawGrid(RoClassicWindow w, int cols, int rows) {
            for (int r = 0; r < rows; r++) {
                for (int c = 0; c < cols; c++) w.DrawBitmapRaw(40 + Cell * c, 17 + Cell * r, Basic + "itemwin_mid.bmp");
            }
        }

        private static int GridIndexAt(Vector2Int local, int cols, int rows, int firstRow, int count) {
            if (local.x < 40 || local.x >= 40 + cols * Cell || local.y < 17 || local.y >= 17 + rows * Cell) return -1;
            int k = firstRow * cols + (local.y - 17) / Cell * cols + (local.x - 40) / Cell;
            return k < count ? k : -1;
        }

        private RoClassicWindow BuildMirror(string layoutName, Func<RoClassicWindow, List<RoItemInstance>> list, Func<int> firstRow, Action<int> scroll, int source) {
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + layoutName).text);
            layout.Ops.RemoveAll(op => op.Path != null && op.Path.EndsWith("itemwin_mid.bmp", StringComparison.OrdinalIgnoreCase));
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UIScrollBar") child.Visible = false;
            }

            RoClassicWindow window = null;
            window = new RoClassicWindow(system, layout) {
                Visible = false,
                DragSourceAt = local => {
                    List<RoItemInstance> items = list(window);
                    int k = GridIndexAt(local, (window.Width - 40) / Cell, (window.Height - 38) / Cell, firstRow(), items.Count);
                    return k < 0 ? null : new RoClassicBuyingPayload { Source = source, Item = items[k], IconPath = RoClassicItemText.IconPath(items[k], metadata) };
                },
            };
            window.DrawDynamic += w => {
                List<RoItemInstance> items = list(w);
                int cols = (w.Width - 40) / Cell, rows = (w.Height - 38) / Cell;
                DrawGrid(w, cols, rows);
                int skip = firstRow() * cols;
                for (int k = 0; k < rows * cols && skip + k < items.Count; k++) {
                    RoClassicItemText.DrawIcon(w, 44 + Cell * (k % cols), 21 + Cell * (k / cols), items[skip + k], metadata, items[skip + k].Count);
                }

                int total = (items.Count + cols - 1) / Math.Max(1, cols);
                w.DrawScrollBar(w.Width - 15, 14, w.Height - 38, Math.Max(0, total - rows), total, rows, firstRow());
            };
            window.Wheeled += (w, local, delta) => scroll(delta > 0 ? -1 : 1);
            window.RightClicked += (w, local) => {
                List<RoItemInstance> items = list(w);
                int k = GridIndexAt(local, (w.Width - 40) / Cell, (w.Height - 38) / Cell, firstRow(), items.Count);
                if (k >= 0) RoClassicItemDescription.Toggle(items[k], metadata);
            };
            return window;
        }

        // ================================================================ 0xae / 0xaf: the buying setup

        private void OpenSetup(int slots) {
            slotLimit = Mathf.Clamp(slots, 1, 5); // 0x8dd7b0 caps buying rows at 5
            EnsureSetupWindows();
            setupShown = true;
            entries.Clear();
            makeFirst = 0;
            mirrorFirstRow = 0;
            titleEdit.SetText(string.Empty);
            limitEdit.SetText(string.Empty);
            seenInventorySequence = -1;
            RebuildMirror();
            BindRowEdits();
            Vector2Int client = system.ClientSize;
            Place(mirror, PlayerPrefs.GetInt(Prefs + "mirror.x", 0), PlayerPrefs.GetInt(Prefs + "mirror.y", 120), client);
            Place(make, PlayerPrefs.GetInt(Prefs + "make.x", 0), PlayerPrefs.GetInt(Prefs + "make.y", 240), client);
            mirror.Visible = true;
            make.Visible = true;
            system.Windows.BringToFront(mirror);
            system.Windows.BringToFront(make);
            system.Windows.PushEsc(make);
            UpdateStatusSlots();
            titleEdit.Focus();
        }

        private void EnsureSetupWindows() {
            if (make != null) return;
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIMerchantShopMakeWnd.id174").text);
            // the capture drew four rows ("Price: " / "ea" / the cell): drawn per state; the frame and labels stay
            layout.Ops.RemoveAll(op => (op.Path != null && op.Path.EndsWith("itemwin_mid.bmp", StringComparison.OrdinalIgnoreCase)) ||
                                       (op.Kind == RoClassicOpKind.Text && (op.X == 184 || op.X == 205) && op.Y >= 54 && op.Y < 54 + RowH * VisibleRows));
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UIEditCtrl" || child.Class == "UIEditCtrlWheel" || child.Class == "UIScrollBar") child.Visible = false;
            }

            make = new RoClassicWindow(system, layout) { Visible = false, DragSourceAt = MakeDragSource };
            make.DrawDynamic += DrawMake;
            make.Clicked += child => {
                if (child.Id == OkId) SetupOk();
                else if (child.Id == CancelId) SetupCancel();
            };
            make.Wheeled += (w, local, delta) => ScrollMake(delta > 0 ? -1 : 1);
            make.Dropped += (w, local, payload) => {
                if (payload is RoClassicBuyingPayload moved && moved.Source == 0) AddToSetup(moved.Item);
            };
            make.RightClicked += (w, local) => {
                Entry e = MakeEntryAt(local);
                if (e != null) RoClassicItemDescription.Toggle(e.Item, metadata);
            };
            make.EscClose = () => {
                SetupCancel(); // the manager sends SendMsg(6, 0xb5) to 0xae
                return true;
            };
            make.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "make.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "make.y", w.Position.y);
            };
            titleEdit = new RoClassicEditField { Rect = new RectInt(41, 22, 294, 16), Background = EditFill, MaxBytes = 79 };
            make.AddEditField(titleEdit);
            limitEdit = new RoClassicEditField { Rect = new RectInt(122, 272, 94, 16), Background = EditFill, MaxBytes = 10 };
            limitEdit.Changed += field => {
                string digits = Digits(field.Text);
                if (digits != field.Text) field.SetText(digits);
            };
            make.AddEditField(limitEdit);
            for (int r = 0; r < VisibleRows; r++) {
                int row = r;
                // 0x8dd7b0: the amount (W − 231, 54 + 42r, 42, 16) and the price (W − 119, 54 + 42r, 94, 16)
                var amount = new RoClassicEditField { Rect = new RectInt(360 - 231, 54 + RowH * r, 42, 16), Background = EditFill, MaxBytes = 4 };
                var price = new RoClassicEditField { Rect = new RectInt(360 - 119, 54 + RowH * r, 94, 16), Background = EditFill, MaxBytes = 9 };
                amount.Changed += field => OnRowEdit(field, row, false);
                price.Changed += field => OnRowEdit(field, row, true);
                amountEdits[r] = amount;
                priceEdits[r] = price;
                make.AddEditField(amount);
                make.AddEditField(price);
            }

            mirror = BuildMirror("UIMerchantMirrorItemWnd.id175", _ => mirrorList, () => mirrorFirstRow, ScrollMirror, 0);
            mirror.Dropped += (w, local, payload) => {
                if (payload is RoClassicBuyingPayload moved && moved.Source == 1) RemoveFromSetup(moved.Item);
            };
            mirror.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "mirror.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "mirror.y", w.Position.y);
            };
        }

        private void OnRowEdit(RoClassicEditField field, int row, bool price) {
            string digits = Digits(field.Text);
            if (digits != field.Text) field.SetText(digits);
            if (makeFirst + row < entries.Count) {
                if (price) entries[makeFirst + row].Price = digits;
                else entries[makeFirst + row].Amount = digits;
            }

            make.Invalidate();
        }

        /// <summary>BuyingStoreItemList.txt's IDs: '#'-terminated tokens read with atol, "//" comments, zero skipped.</summary>
        public static System.Collections.Generic.HashSet<int> ParseItemList(string text) {
            var ids = new System.Collections.Generic.HashSet<int>();
            if (string.IsNullOrEmpty(text)) return ids;
            foreach (string raw in text.Split('\n')) {
                string line = raw;
                int comment = line.IndexOf("//", System.StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment);
                foreach (string token in line.Split('#')) {
                    // atol: leading blanks, an optional sign, then digits
                    string t = token.Trim();
                    int n = 0, i = 0;
                    bool negative = i < t.Length && t[i] == '-';
                    if (i < t.Length && (t[i] == '-' || t[i] == '+')) i++;
                    int start = i;
                    while (i < t.Length && t[i] >= '0' && t[i] <= '9') n = unchecked(n * 10 + (t[i++] - '0'));
                    if (i > start && n != 0) ids.Add(negative ? -n : n);
                }
            }

            return ids;
        }

        /// <summary>BuyingStoreItemList.txt membership (FUN_00d032b0 / FUN_00d03470); everything passes before the list loads.</summary>
        private static bool Buyable(int itemId) {
            System.Collections.Generic.HashSet<int> allowed = RoClassicUiHost.BuyingStoreItemIds();
            return allowed == null || allowed.Contains(itemId);
        }

        /// <summary>
        /// The inventory items not yet wanted (0xaf's list, FUN_00d032b0): owned items whose ID is in BuyingStoreItemList.txt,
        /// no item-type test (codex-ui-research "buying-inventory-filter").
        /// </summary>
        private void RebuildMirror() {
            mirrorList.Clear();
            foreach (RoItemInstance item in Inventory()) {
                if (item == null || item.Count <= 0 || item.IsEquipped || !Buyable(item.ItemId)) continue;
                if (entries.Exists(e => e.Item.ItemId == item.ItemId)) continue;
                mirrorList.Add(item);
            }

            int cols = Math.Max(1, (mirror.Width - 40) / Cell), rows = (mirror.Height - 38) / Cell;
            mirrorFirstRow = Mathf.Clamp(mirrorFirstRow, 0, Math.Max(0, (mirrorList.Count + cols - 1) / cols - rows));
            mirror.Invalidate();
            make.Invalidate();
            UpdateStatusSlots();
        }

        private void ScrollMirror(int rows) {
            int cols = Math.Max(1, (mirror.Width - 40) / Cell), visible = (mirror.Height - 38) / Cell;
            int first = Mathf.Clamp(mirrorFirstRow + rows, 0, Math.Max(0, (mirrorList.Count + cols - 1) / cols - visible));
            if (first == mirrorFirstRow) return;
            mirrorFirstRow = first;
            mirror.Invalidate();
        }

        /// <summary>Money and Weight of the layout's bound slots ("{Zeny:n0} Zeny", "Weight : {Weight,3} / {MaxWeight}").</summary>
        private void UpdateStatusSlots() {
            if (make == null || session == null) return;
            RoPlayerStatusSnapshot status = session.InventorySnapshot().Status;
            if (status == null) return;
            make.Set("Zeny", status.Zeny);
            make.Set("Weight", status.Weight / 10); // tenths on the wire, whole units on screen (Basic Info, cart)
            make.Set("MaxWeight", status.MaxWeight / 10);
        }

        /// <summary>
        /// OnDraw 0x8e3f70 (buying): title 0x6b8 over its shadow, the "Name" heading, one row per slot from y 54 every 42 px
        /// (the cell, the item, "ea" and "Price:"), the red total MsgStr 0xc98.
        /// </summary>
        private void DrawMake(RoClassicWindow w) {
            int rows = Math.Min(VisibleRows, Math.Max(0, slotLimit - makeFirst));
            string price = RoClassicUiHost.MsgStr(0x6b9, "Price: ");
            for (int r = 0; r < rows; r++) {
                int y = 54 + RowH * r, k = makeFirst + r;
                w.DrawBitmapRaw(3, y, Basic + "itemwin_mid.bmp");
                w.TextOut(184, y + 3, "ea", 0, 12, Black);
                w.TextOut(205, y + 3, price, 0, 12, Black);
                if (k >= entries.Count) continue;
                Entry e = entries[k];
                RoClassicItemText.DrawIcon(w, 7, y + 4, e.Item, metadata, 0);
                RoClassicItemText.DrawName(w, 40, y + 3, 86, e.Item, metadata);
            }

            ulong total = 0;
            foreach (Entry e in entries) total += (ulong) ParseUInt(e.Price) * ParseUInt(e.Amount);
            string line = RoClassicUiHost.MsgStr(0xc98, "~ When buying items, total amount of zeny reduced ");
            w.TextOut(5 + w.FontFor(0, 12).Measure(line), 217, RoClassicItemText.Commas((long) Math.Min(total, long.MaxValue)) + " Zeny", 0, 12, Red);
            w.DrawScrollBar(345, 17, 231, Math.Max(0, slotLimit - VisibleRows), slotLimit, VisibleRows, makeFirst);
        }

        private Entry MakeEntryAt(Vector2Int local) {
            if (local.y < 54 || local.y >= 54 + RowH * VisibleRows || local.x < 3 || local.x >= 128) return null;
            int k = makeFirst + (local.y - 54) / RowH;
            return k < entries.Count ? entries[k] : null;
        }

        private IRoClassicDragPayload MakeDragSource(Vector2Int local) {
            Entry e = MakeEntryAt(local);
            return e == null ? null : new RoClassicBuyingPayload { Source = 1, Item = e.Item, IconPath = RoClassicItemText.IconPath(e.Item, metadata) };
        }

        private void ScrollMake(int rows) {
            int first = Mathf.Clamp(makeFirst + rows, 0, Math.Max(0, slotLimit - VisibleRows));
            if (first == makeFirst) return;
            makeFirst = first;
            BindRowEdits();
            make.Invalidate();
        }

        private void BindRowEdits() {
            for (int r = 0; r < VisibleRows; r++) {
                int k = makeFirst + r;
                bool filled = k < slotLimit && k < entries.Count;
                amountEdits[r].Visible = filled;
                priceEdits[r].Visible = filled;
                amountEdits[r].SetText(filled ? entries[k].Amount : string.Empty);
                priceEdits[r].SetText(filled ? entries[k].Price : string.Empty);
            }
        }

        /// <summary>Case 0x26 from 0xaf: the item into a free row (one row per item id).</summary>
        private void AddToSetup(RoItemInstance item) {
            if (item == null || entries.Count >= slotLimit || entries.Exists(e => e.Item.ItemId == item.ItemId)) return;
            entries.Add(new Entry { Item = item.WithCount(1) });
            RebuildMirror();
            BindRowEdits();
            int k = entries.Count - 1;
            if (k >= makeFirst && k < makeFirst + VisibleRows) amountEdits[k - makeFirst].Focus();
        }

        private void RemoveFromSetup(RoItemInstance item) {
            if (item == null) return;
            entries.RemoveAll(e => e.Item.ItemId == item.ItemId);
            makeFirst = Mathf.Clamp(makeFirst, 0, Math.Max(0, slotLimit - VisibleRows));
            RebuildMirror();
            BindRowEdits();
        }

        /// <summary>
        /// OK (vf19 0x8e9b20): an item is required (0x6bc); per row the amount (0x6bf, owned + wanted ≤ 9999: 0x6c0) and the
        /// price (0x6bd, ≤ 99,990,000: 0x6be); the Zeny limit (0x6c2) within the Zeny held (0x6c3). Then mode 0x10f →
        /// CZ_REQ_OPEN_BUYING_STORE with result 1; the server's list (0x0813) closes the setup.
        /// </summary>
        private void SetupOk() {
            if (session == null) return;
            if (entries.Count == 0) {
                Chat(RoClassicUiHost.MsgStr(0x6bc, "Please register the item first that has to be purchased."), "#FF0000");
                return;
            }

            var items = new RoBuyingStoreItem[entries.Count];
            for (int i = 0; i < entries.Count; i++) {
                Entry e = entries[i];
                string name = e.Item.GetFullDisplayName(metadata);
                uint amount = ParseUInt(e.Amount), price = ParseUInt(e.Price);
                if (amount == 0) {
                    Chat(Format(RoClassicUiHost.MsgStr(0x6bf, "Please enter the quantity for %s."), name), "#FF0000");
                    return;
                }

                if (Owned(e.Item.ItemId) + amount > MaxAmount) {
                    Chat(Format(RoClassicUiHost.MsgStr(0x6c0, "The total quantity of %s in possession is more than 9999. Please enter value less than 9999."), name), "#FF0000");
                    return;
                }

                if (price == 0) {
                    Chat(Format(RoClassicUiHost.MsgStr(0x6bd, "Please enter the price for item %s."), name), "#FF0000");
                    return;
                }

                if (price > MaxPrice) {
                    Chat(Format(RoClassicUiHost.MsgStr(0x6be, "Please enter the price for item %s. It must be less than 9999 million Zeny."), name), "#FF0000");
                    return;
                }

                items[i] = new RoBuyingStoreItem(unchecked((uint) e.Item.ItemId), (int) amount, price, 0);
            }

            uint limit = ParseUInt(limitEdit.Text);
            if (limit == 0) {
                Chat(RoClassicUiHost.MsgStr(0x6c2, "Enter the limited price."), "#FF0000");
                return;
            }

            RoPlayerStatusSnapshot status = session.InventorySnapshot().Status;
            if (status != null && limit > status.Zeny) {
                Chat(RoClassicUiHost.MsgStr(0x6c3, "You have entered a greater amount of zeny than you have. Please check your zeny."), "#FF0000");
                return;
            }

            string title = titleEdit.Text.Trim();
            if (title.Length == 0) title = RoClassicUiHost.MsgStr(0x6b8, "Buying Store Window");
            _ = session.BeginLegacyOpenBuyingStore(limit, true, title, items);
        }

        /// <summary>Cancel / Esc: mode 0x10f with result 0 and the setup goes.</summary>
        private void SetupCancel() {
            string title = titleEdit != null ? titleEdit.Text.Trim() : string.Empty;
            CloseSetup();
            if (session != null) _ = session.BeginLegacyOpenBuyingStore(0, false, title, Array.Empty<RoBuyingStoreItem>());
        }

        private void CloseSetup() {
            if (!setupShown) return;
            setupShown = false;
            RoClassicEditField focused = RoClassicTextInput.Focused;
            if (focused != null && (focused == titleEdit || focused == limitEdit || Array.IndexOf(amountEdits, focused) >= 0 || Array.IndexOf(priceEdits, focused) >= 0)) {
                RoClassicTextInput.Focus(null);
            }

            if (make != null) {
                make.Visible = false;
                system.Windows.RemoveEsc(make);
            }

            if (mirror != null) mirror.Visible = false;
        }

        // ================================================================ 0xb0: the open buying store

        private RoItemInstance WantedItem(RoBuyingStoreItem wanted, int index) =>
            RoItemInstance.CreateRegular(index, unchecked((int) wanted.ItemId), Math.Max(1, wanted.Amount), itemTypeRaw: wanted.ItemType);

        private void OpenMyShop() {
            if (myShop == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIMerchantItemMyShopWnd.id176").text);
                layout.Ops.RemoveAll(op => op.Path != null && op.Path.EndsWith("itemwin_mid.bmp", StringComparison.OrdinalIgnoreCase));
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIScrollBar") child.Visible = false;
                    if (child.Class == "UIStaticText" && child.Y > 50) {
                        // 0x8daea0 / case 0x77: "Price limit: %s Zeny" (MsgStr 0x6c7)
                        child.Fields["text"] = "Limit";
                        child.W = Math.Max(child.W, 220);
                        child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                    }
                }

                myShop = new RoClassicWindow(system, layout) { Visible = false };
                myShop.DrawDynamic += DrawMyShop;
                myShop.Clicked += child => {
                    if (child.Id == CloseId && session != null) _ = session.BeginLegacyCloseBuyingStore();
                };
                myShop.Pressed += (w, local) => {
                    RoClassicChildLayout check = Find(myShop, CheckId);
                    if (check != null && local.x >= check.X && local.x < check.X + check.W && local.y >= check.Y && local.y < check.Y + check.H) {
                        notifyBought = !notifyBought;
                        SyncCheckBox(myShop, CheckId, notifyBought);
                    }
                };
                myShop.RightClicked += (w, local) => {
                    int k = MyShopIndexAt(local);
                    if (k >= 0) RoClassicItemDescription.Toggle(WantedItem(state.OwnItems[k], k), metadata);
                };
                myShop.Wheeled += (w, local, delta) => {
                    int cols = Math.Max(1, (myShop.Width - 40) / Cell), rows = (myShop.Height - 38) / Cell;
                    int total = (state.OwnItems.Length + cols - 1) / cols;
                    myShopFirstRow = Mathf.Clamp(myShopFirstRow + (delta > 0 ? -1 : 1), 0, Math.Max(0, total - rows));
                    myShop.Invalidate();
                };
                myShop.Moved += w => {
                    PlayerPrefs.SetInt(Prefs + "shop.x", w.Position.x);
                    PlayerPrefs.SetInt(Prefs + "shop.y", w.Position.y);
                };
                Place(myShop, PlayerPrefs.GetInt(Prefs + "shop.x", 200), PlayerPrefs.GetInt(Prefs + "shop.y", 120), system.ClientSize);
                SyncCheckBox(myShop, CheckId, notifyBought);
            }

            myShop.Set("Limit", Format(RoClassicUiHost.MsgStr(0x6c7, "Price limit: %s Zeny"), RoClassicItemText.Commas(state.OwnZenyLimit)));
            if (!myShop.Visible && !myShop.HiddenByCloseTabs) {   // a refresh leaves F11's hiding alone
                myShop.Visible = true;
                system.Windows.BringToFront(myShop);
            }

            myShop.Invalidate();
        }

        private void DrawMyShop(RoClassicWindow w) {
            int cols = (w.Width - 40) / Cell, rows = (w.Height - 38) / Cell;
            DrawGrid(w, cols, rows);
            RoBuyingStoreItem[] items = state.OwnItems;
            int skip = myShopFirstRow * cols;
            for (int k = 0; k < rows * cols && skip + k < items.Length; k++) {
                RoClassicItemText.DrawIcon(w, 44 + Cell * (k % cols), 21 + Cell * (k / cols), WantedItem(items[skip + k], skip + k), metadata, items[skip + k].Amount);
            }

            int total = (items.Length + cols - 1) / Math.Max(1, cols);
            w.DrawScrollBar(w.Width - 15, 14, w.Height - 38, Math.Max(0, total - rows), total, rows, myShopFirstRow);
        }

        private int MyShopIndexAt(Vector2Int local) =>
            GridIndexAt(local, (myShop.Width - 40) / Cell, (myShop.Height - 38) / Cell, myShopFirstRow, state.OwnItems.Length);

        // ================================================================ 0xb1 / 0xb2 / 0xb3: selling to a buyer

        private void OpenSeller() {
            EnsureSellerWindows();
            bool fresh = !sellerShown;
            sellList.Clear();
            sellFirst = 0;
            wantFirstRow = 0;
            wantSelected = -1;
            sellMirrorFirstRow = 0;
            sellerShown = true;
            seenInventorySequence = -1;
            RebuildSellMirror();
            RoClassicMerchantList.SizeToItems(wantWin, state.Shop.Items.Length, system.ClientSize);
            SizeSell();
            wantWin.Set("Limit", Format(RoClassicUiHost.MsgStr(0x6c7, "Price limit: %s Zeny"), RoClassicItemText.Commas(state.Shop.ZenyLimit)));
            UpdateSellTotal();
            if (fresh) {
                Vector2Int client = system.ClientSize;
                Place(wantWin, PlayerPrefs.GetInt(Prefs + "want.x", 0), PlayerPrefs.GetInt(Prefs + "want.y", 120), client);
                Place(sellWin, PlayerPrefs.GetInt(Prefs + "sell.x", 280), PlayerPrefs.GetInt(Prefs + "sell.y", 280), client);
                Place(sellMirror, PlayerPrefs.GetInt(Prefs + "sellmirror.x", 0), PlayerPrefs.GetInt(Prefs + "sellmirror.y", 120), client);
            }

            wantWin.Visible = true;
            sellMirror.Visible = true;
            sellWin.Visible = true;
            system.Windows.BringToFront(wantWin);
            system.Windows.BringToFront(sellMirror);
            system.Windows.BringToFront(sellWin);
            system.Windows.PushEsc(sellWin);
            wantWin.Invalidate();
            sellWin.Invalidate();
        }

        private void EnsureSellerWindows() {
            if (wantWin != null) return;
            // NativeMerchantShop: a single-column offer list; source width 350 snaps to 344, at least four rows.
            var layout = new RoClassicWindowLayout { Class = "UIMerchantItemShopWnd", WindowId = 0xb1, Width = 344, Height = RoClassicMerchantList.HeightForItems(0) };
            RoClassicMerchantList.BuildFrame(layout.Ops, layout.Width, layout.Height);
            var limit = new RoClassicChildLayout { Class = "UIStaticText", Id = 0x248, X = 12, Y = layout.Height - 21, W = 160, H = 15 };
            limit.Fields["text"] = "Limit";
            layout.Children.Add(limit);
            wantWin = new RoClassicWindow(system, layout) { Visible = false };
            wantWin.DrawDynamic += DrawWanted;
            RoClassicMerchantList.Configure(wantWin, Prefs + "want", true, () => {
                wantFirstRow = Mathf.Clamp(wantFirstRow, 0, Math.Max(0, state.Shop.Items.Length - WantedRows));
            });
            wantWin.Pressed += (w, local) => {
                wantSelected = WantedIndexAt(local);
                w.Invalidate();
            };
            wantWin.Wheeled += (w, local, delta) => {
                wantFirstRow = Mathf.Clamp(wantFirstRow + (delta > 0 ? -WantedRows : WantedRows), 0, Math.Max(0, state.Shop.Items.Length - WantedRows));
                wantWin.Invalidate();
            };
            wantWin.RightClicked += (w, local) => {
                int k = WantedIndexAt(local);
                if (k >= 0) RoClassicItemDescription.Toggle(WantedItem(state.Shop.Items[k], k), metadata);
            };
            wantWin.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "want.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "want.y", w.Position.y);
            };

            sellMirror = BuildMirror("UIMerchantMirrorItemWnd.id179", _ => sellMirrorList, () => sellMirrorFirstRow, ScrollSellMirror, 2);
            sellMirror.Dropped += (w, local, payload) => {
                if (payload is RoClassicBuyingPayload moved && moved.Source == 3) RemoveFromSell(moved.Item);
            };
            sellMirror.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "sellmirror.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "sellmirror.y", w.Position.y);
            };

            RoClassicWindowLayout sellLayout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIMerchantItemPurchaseWnd.id178").text);
            sellLayout.Ops.RemoveAll(op => op.Path != null && op.Path.EndsWith("itemwin_mid.bmp", StringComparison.OrdinalIgnoreCase));
            foreach (RoClassicChildLayout child in sellLayout.Children) {
                if (child.Class != "UIStaticText" || child.Y <= 100) continue;
                child.Fields["text"] = "Total";
                child.W = Math.Max(child.W, 160);
                child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
            }

            sellWin = new RoClassicWindow(system, sellLayout) { Visible = false, DragSourceAt = SellDragSource };
            RoClassicMerchantList.Configure(sellWin, Prefs + "sell", false, () => {
                sellFirst = Mathf.Clamp(sellFirst, 0, Math.Max(0, sellList.Count - SellRows));
            });
            sellWin.DrawDynamic += DrawSell;
            sellWin.Clicked += child => {
                if (child.Id == OkId) SellOk();
                else if (child.Id == CancelId) CloseSeller();
            };
            sellWin.Wheeled += (w, local, delta) => {
                sellFirst = Mathf.Clamp(sellFirst + (delta > 0 ? -SellRows : SellRows), 0, Math.Max(0, sellList.Count - SellRows));
                sellWin.Invalidate();
            };
            sellWin.Dropped += (w, local, payload) => {
                if (payload is RoClassicBuyingPayload moved && moved.Source == 2) AddToSell(moved.Item);
            };
            sellWin.RightClicked += (w, local) => {
                int r = SellRowAt(local);
                if (r >= 0) RoClassicItemDescription.Toggle(sellList[r].Item, metadata);
            };
            sellWin.EscClose = () => {
                CloseSeller(); // the manager sends SendMsg(6, 0xb5) to 0xb2
                return true;
            };
            sellWin.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "sell.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "sell.y", w.Position.y);
            };
        }

        private RoBuyingStoreItem? Wanted(int itemId) {
            foreach (RoBuyingStoreItem wanted in state.Shop.Items) {
                if (wanted.ItemId == unchecked((uint) itemId) && wanted.Amount > 0) return wanted;
            }

            return null;
        }

        /// <summary>
        /// The own items the buyer wants, minus what is already in the sell list (0xb3's list, FUN_00d03470: the same
        /// BuyingStoreItemList.txt pass, then the buyer's wanted list). The worn-item test stands in for FUN_00d2ba20, whose
        /// slot meaning is not traced yet.
        /// </summary>
        private void RebuildSellMirror() {
            sellMirrorList.Clear();
            foreach (RoItemInstance item in Inventory()) {
                if (item == null || item.Count <= 0 || item.IsEquipped || !Buyable(item.ItemId) || !Wanted(item.ItemId).HasValue) continue;
                int used = 0;
                foreach ((RoItemInstance sold, int amount) in sellList) {
                    if (sold.BagIndex == item.BagIndex) used += amount;
                }

                if (item.Count - used > 0) sellMirrorList.Add(used == 0 ? item : item.WithCount(item.Count - used));
            }

            sellMirror?.Invalidate();
        }

        private void ScrollSellMirror(int rows) {
            int cols = Math.Max(1, (sellMirror.Width - 40) / Cell), visible = (sellMirror.Height - 38) / Cell;
            int first = Mathf.Clamp(sellMirrorFirstRow + rows, 0, Math.Max(0, (sellMirrorList.Count + cols - 1) / cols - visible));
            if (first == sellMirrorFirstRow) return;
            sellMirrorFirstRow = first;
            sellMirror.Invalidate();
        }

        private int WantedRows => RoClassicMerchantList.Rows(wantWin.Height);
        private int SellRows => RoClassicMerchantList.Rows(sellWin.Height);

        private void SizeSell() {
            RoClassicMerchantList.SizeToItems(sellWin, sellList.Count, system.ClientSize);
            sellFirst = Mathf.Clamp(sellFirst, 0, Math.Max(0, sellList.Count - SellRows));
        }

        private int WantedAmount(RoBuyingStoreItem wanted) {
            int selected = 0;
            foreach ((RoItemInstance item, int amount) in sellList) {
                if (unchecked((uint) item.ItemId) == wanted.ItemId) selected += amount;
            }
            return Math.Max(0, wanted.Amount - selected);
        }

        private void DrawWanted(RoClassicWindow w) {
            string title = RoClassicUiHost.MsgStr(0xba, "Buy");
            w.ClipTo(16, 2, w.Width - 32, 15);
            w.TextOut(17, 3, title, 0, 12, White);
            w.TextOut(16, 2, title, 0, 12, Black);
            w.ResetClip();
            RoBuyingStoreItem[] items = state.Shop.Items;
            for (int row = 0; row < WantedRows && wantFirstRow + row < items.Length; row++) {
                int index = wantFirstRow + row;
                RoBuyingStoreItem wanted = items[index];
                RoClassicMerchantList.DrawRow(w, row, WantedItem(wanted, index), metadata, WantedAmount(wanted), wanted.Price, true, index == wantSelected);
            }
            w.DrawScrollBar(w.Width - 15, 14, WantedRows * Cell, Math.Max(0, items.Length - WantedRows), items.Length, WantedRows, wantFirstRow);
        }

        private int WantedIndexAt(Vector2Int local) =>
            RoClassicMerchantList.RowAt(local, wantWin.Width, wantWin.Height, wantFirstRow, state.Shop.Items.Length);

        private void DrawSell(RoClassicWindow w) {
            int rows = SellRows;
            for (int row = 0; row < rows && sellFirst + row < sellList.Count; row++) {
                (RoItemInstance item, int amount) = sellList[sellFirst + row];
                RoBuyingStoreItem? wanted = Wanted(item.ItemId);
                RoClassicMerchantList.DrawRow(w, row, item, metadata, amount, wanted.HasValue ? wanted.Value.Price : 0, false, false);
            }
            w.DrawScrollBar(w.Width - 15, 14, rows * Cell, Math.Max(0, sellList.Count - rows), sellList.Count, rows, sellFirst);
        }

        private int SellRowAt(Vector2Int local) =>
            RoClassicMerchantList.RowAt(local, sellWin.Width, sellWin.Height, sellFirst, sellList.Count);

        private IRoClassicDragPayload SellDragSource(Vector2Int local) {
            int r = SellRowAt(local);
            return r < 0 ? null : new RoClassicBuyingPayload { Source = 3, Item = sellList[r].Item, IconPath = RoClassicItemText.IconPath(sellList[r].Item, metadata) };
        }

        /// <summary>Case 0x26 (0x8f1a80): an own item into the sell list; a stack asks the amount (at most what the buyer wants).</summary>
        private void AddToSell(RoItemInstance item) {
            RoBuyingStoreItem? wanted = item != null ? Wanted(item.ItemId) : null;
            if (!wanted.HasValue) return;
            RoItemInstance full = item;
            foreach (RoItemInstance own in Inventory()) {
                if (own != null && own.BagIndex == item.BagIndex) full = own;
            }

            int max = Math.Min(Math.Min(item.Count, WantedAmount(wanted.Value)), 30000);
            if (max <= 0) return;
            void Add(int amount) {
                amount = Math.Max(1, Math.Min(amount, max));
                int at = sellList.FindIndex(s => s.Item.BagIndex == full.BagIndex);
                if (at >= 0) sellList[at] = (full, Math.Min(wanted.Value.Amount, sellList[at].Amount + amount));
                else sellList.Add((full, amount));
                RebuildSellMirror();
                SizeSell();
                wantWin.Invalidate();
                UpdateSellTotal();
                sellWin.Invalidate();
            }

            if (max <= 1) {
                Add(1);
                return;
            }

            RoClassicCountWindow.Show(full.GetFullDisplayName(metadata), max, n => {
                if (n >= 1) Add((int) n);
            });
        }

        private void RemoveFromSell(RoItemInstance item) {
            sellList.RemoveAll(s => s.Item.BagIndex == item.BagIndex);
            SizeSell();
            wantWin.Invalidate();
            RebuildSellMirror();
            UpdateSellTotal();
            sellWin.Invalidate();
        }

        private long SellTotal() {
            long total = 0;
            foreach ((RoItemInstance item, int amount) in sellList) {
                RoBuyingStoreItem? wanted = Wanted(item.ItemId);
                if (wanted.HasValue) total += (long) wanted.Value.Price * amount;
            }

            return total;
        }

        private void UpdateSellTotal() => sellWin?.Set("Total", "Total : " + RoClassicItemText.Commas(SellTotal()) + " Zeny");

        /// <summary>Sell (mode 1, 0x8f1a80): the total against the buyer's limit (MsgStr 0x6cc), then 0x111 → CZ_REQ_TRADE_BUYING_STORE.</summary>
        private void SellOk() {
            if (session == null || sellList.Count == 0) return;
            if (SellTotal() > state.Shop.ZenyLimit) {
                Chat(RoClassicUiHost.MsgStr(0x6cc, "Buyer has insufficient zeny, lower the amount of items you're selling."), "#FF0000");
                return;
            }

            var items = new (ushort BagIndex, uint ItemId, int Amount)[sellList.Count];
            for (int i = 0; i < sellList.Count; i++) {
                RoItemInstance item = sellList[i].Item;
                items[i] = ((ushort) item.BagIndex, unchecked((uint) item.ItemId), sellList[i].Amount);
                soldItems[item.BagIndex] = item; // the sale notice names it by index
            }

            uint buyer = state.Shop.OwnerAid, storeId = state.Shop.StoreId;
            CloseSeller();
            _ = session.BeginLegacyTradeBuyingStore(buyer, storeId, items);
        }

        /// <summary>Mode msg 0xf5 (/shopping: a right press on a shop board, 0x7c13f0): 0xb1 / 0xb2 / 0xb3 close.</summary>
        public static void CloseSellerWindows() => instance?.CloseSeller();

        private void CloseSeller() {
            if (!sellerShown) return;
            sellerShown = false;
            sellList.Clear();
            if (wantWin != null) wantWin.Visible = false;
            if (sellMirror != null) sellMirror.Visible = false;
            if (sellWin != null) {
                sellWin.Visible = false;
                system.Windows.RemoveEsc(sellWin);
            }
        }

        // ================================================================ per frame: hover tooltips

        public void ClassicUiTick(RoClassicUiSystem ui) {
            RoClassicWindowManager windows = ui.Windows;
            if (windows.Dragging != null) return;
            RoClassicWindow over = windows.HoverWindow;
            if (over == null) return;
            Vector2Int local = windows.Mouse - over.Position;
            if (over == myShop && myShop.Visible) {
                int k = MyShopIndexAt(local);
                if (k >= 0) ShowWantedTooltip(myShop, local, state.OwnItems[k], k);
            } else if (over == wantWin && wantWin.Visible) {
                int k = WantedIndexAt(local);
                if (k >= 0) ShowWantedTooltip(wantWin, local, state.Shop.Items[k], k);
            } else if ((over == mirror && mirror.Visible) || (over == sellMirror && sellMirror.Visible)) {
                List<RoItemInstance> items = over == mirror ? mirrorList : sellMirrorList;
                int k = GridIndexAt(local, (over.Width - 40) / Cell, (over.Height - 38) / Cell, over == mirror ? mirrorFirstRow : sellMirrorFirstRow, items.Count);
                if (k >= 0) {
                    RoItemInstance item = items[k];
                    string text = string.Format(CultureInfo.InvariantCulture, "{0}: {1} ea.", item.GetFullDisplayName(metadata), item.Count);
                    RoClassicTooltip.Shared.Show(text, RoClassicItemText.TooltipColor(item, metadata),
                        over.Position.x + 40 + (local.x - 40) / Cell * Cell, over.Position.y + 1 + (local.y - 17) / Cell * Cell);
                }
            }
        }

        /// <summary>vf28 0x8e8c70 (buying): MsgStr 0x6c8 "Buying %s for %s Zeny. Amount: %d."</summary>
        private void ShowWantedTooltip(RoClassicWindow window, Vector2Int local, RoBuyingStoreItem wanted, int index) {
            RoItemInstance item = WantedItem(wanted, index);
            bool offer = window == wantWin;
            string text = Format(RoClassicUiHost.MsgStr(0x6c8, "Buying %s for %s Zeny. Amount: %d."), item.GetFullDisplayName(metadata),
                RoClassicItemText.Commas(wanted.Price), (offer ? WantedAmount(wanted) : wanted.Amount).ToString(CultureInfo.InvariantCulture));
            RoClassicTooltip.Shared.Show(text, White, window.Position.x + (offer ? 70 : 40 + (local.x - 40) / Cell * Cell),
                window.Position.y + (offer ? 25 : 1) + (local.y - 17) / Cell * Cell);
        }

        // ================================================================ diagnostics (!bs ...)

        /// <summary>
        /// The sweep console: title t | add mirrorIndex | amount row n | price row zeny | limit zeny | remove row | ok | cancel |
        /// close | view aid | sell mirrorIndex [n] | unsell row | sellok | sellcancel | state.
        /// </summary>
        public static string Sweep(string command) {
            RoClassicBuyingStore b = Ensure();
            if (b == null) return "no classic ui";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            switch (a[0]) {
                case "title":
                    b.titleEdit?.SetText(a.Length > 1 ? string.Join(" ", a, 1, a.Length - 1) : string.Empty);
                    break;
                case "add":
                    if (b.setupShown && Arg(1) < b.mirrorList.Count) b.AddToSetup(b.mirrorList[Arg(1)]);
                    break;
                case "amount":
                case "price":
                    if (Arg(1) < b.entries.Count) {
                        if (a[0] == "price") b.entries[Arg(1)].Price = Arg(2).ToString(CultureInfo.InvariantCulture);
                        else b.entries[Arg(1)].Amount = Arg(2).ToString(CultureInfo.InvariantCulture);
                        b.BindRowEdits();
                        b.make.Invalidate();
                    }

                    break;
                case "limit":
                    b.limitEdit?.SetText(Arg(1).ToString(CultureInfo.InvariantCulture));
                    break;
                case "remove":
                    if (Arg(1) < b.entries.Count) b.RemoveFromSetup(b.entries[Arg(1)].Item);
                    break;
                case "ok":
                    b.SetupOk();
                    break;
                case "cancel":
                    if (b.setupShown) b.SetupCancel();
                    break;
                case "close":
                    if (b.session != null) _ = b.session.BeginLegacyCloseBuyingStore();
                    break;
                case "view":
                    if (b.session != null && uint.TryParse(a.Length > 1 ? a[1] : string.Empty, out uint aid)) _ = b.session.BeginLegacyClickBuyingStore(aid);
                    break;
                case "sell":
                    // sell <mirror index> [amount]: as if dragged into 0xb2 (a stack answers the amount window when given)
                    if (b.sellerShown && Arg(1) < b.sellMirrorList.Count) {
                        b.AddToSell(b.sellMirrorList[Arg(1)]);
                        if (a.Length > 2) {
                            RoClassicCountWindow.SetText(a[2]);
                            RoClassicCountWindow.Confirm();
                        }
                    }

                    break;
                case "unsell":
                    if (Arg(1) < b.sellList.Count) b.RemoveFromSell(b.sellList[Arg(1)].Item);
                    break;
                case "sellok":
                    b.SellOk();
                    break;
                case "sellcancel":
                    b.CloseSeller();
                    break;
            }

            RoBuyingStoreSnapshot s = b.state;
            var text = new StringBuilder();
            text.Append("setup=").Append(b.setupShown).Append(" slots=").Append(b.slotLimit).Append(" rows=");
            foreach (Entry e in b.entries) text.Append(e.Item.ItemId).Append('x').Append(e.Amount).Append('@').Append(e.Price).Append(' ');
            text.Append("limit=").Append(b.limitEdit?.Text).Append(" mirror=").Append(b.mirrorList.Count);
            text.Append(" own=").Append(s.IsOwnStoreOpen ? "'" + s.OwnTitle + "' limit " + s.OwnZenyLimit + " " : "- ");
            foreach (RoBuyingStoreItem item in s.OwnItems) text.Append(item.ItemId).Append('x').Append(item.Amount).Append('@').Append(item.Price).Append(' ');
            text.Append("seller=").Append(b.sellerShown ? s.Shop.OwnerAid + "#" + s.Shop.StoreId + " limit " + s.Shop.ZenyLimit : "-").Append(" sell=");
            foreach ((RoItemInstance item, int amount) in b.sellList) text.Append(item.ItemId).Append('x').Append(amount).Append(' ');
            text.Append("sellmirror=").Append(b.sellMirrorList.Count).Append(" boards=");
            foreach (RoBuyingStoreBoard board in s.Boards) text.Append(board.OwnerAid).Append(" '").Append(board.Title).Append("' ");
            text.Append(" fail=").Append(s.LastOpenFailure).Append(" buyer=").Append(s.LastBuyerResult).Append(" seller=").Append(s.LastSellerFailure.Result)
                .Append(" sold=").Append(s.SoldSerial);
            return text.ToString();
        }
    }
}
