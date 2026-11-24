using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The barter shop (npc-barter-market-spec §1): the shop list UIBartermarketWnd 0x14e (a UIItemShopWnd without title
    /// child, check box or buttons: the items with their stock, the price item's icon and amount on the right) and the cart
    /// UIBarterItemPurchaseWnd 0x14f ("Items you want to exchange", Buy / Cancel). The data is CBarterMarketItemMgr's
    /// (0x011a624c): the shop list, the cart and the currency totals; items move list → cart (drag type 0x1f, amount
    /// window mode 0x18) and back (type 4, mode 0x17). Buy sends CZ_NPC_BARTER_MARKET_PURCHASE 0x0b0f then closes like
    /// Cancel: CZ_NPC_BARTER_MARKET_CLOSE 0x0b12 and the manager cleared.
    /// </summary>
    public sealed partial class RoClassicNpcMarket {
        /// <summary>An entry of the manager (0x48 bytes): a shop row (Amount = stock) or a cart line (Amount = count).</summary>
        private sealed class BarterEntry {
            public RoItemInstance Item;
            public RoItemInstance Currency;     // placeholder ITEM_INFO of the price item (descriptions, icon)
            public uint ItemId;
            public int Type;                    // +0x1c
            public int Amount;                  // +0x18
            public bool Unlimited;              // +0x38, amount == −1
            public uint CurrencyId;             // +0x2c
            public int CurrencyAmount;          // +0x28
            public int Weight;                  // +0x30, the packet's weight / 10
            public uint ShopIndex;              // +0x3c
            public uint Location;               // +0x40

            public BarterEntry Copy(int amount) => new BarterEntry {
                Item = Item, Currency = Currency, ItemId = ItemId, Type = Type, Amount = amount, Unlimited = Unlimited, CurrencyId = CurrencyId,
                CurrencyAmount = CurrencyAmount, Weight = Weight, ShopIndex = ShopIndex, Location = Location,
            };
        }

        private readonly List<BarterEntry> barterShop = new List<BarterEntry>();
        private readonly List<BarterEntry> barterCart = new List<BarterEntry>();
        private RoClassicWindow barterList;     // 0x14e
        private RoClassicWindow barterBuy;      // 0x14f
        private bool barterShown;
        private int barterListFirst;
        private int barterListSel = -1;         // +0xe0
        private int barterCartFirst;
        private int barterCartSel = -1;         // +0xd8

        private int BarterListRows => (barterList.Height - 46) / 32;
        private int BarterCartRows => (barterBuy.Height - 38) / 32;

        // ================================================================ open / close

        /// <summary>
        /// Handler 0xc43a10: mgr.Clear() (shop, cart, currency totals), the entries in packet order, then MakeWindow(0x14e)
        /// and MakeWindow(0x14f) with msg 0x17 (an open pair refreshes in place).
        /// </summary>
        private void OpenBarter(RoNpcBarterItem[] items) {
            barterShop.Clear();
            barterCart.Clear();
            foreach (RoNpcBarterItem item in items ?? Array.Empty<RoNpcBarterItem>()) {
                uint currencyId = item.Currencies.Length > 0 ? item.Currencies[0].ItemId : 0u;
                uint currencyAmount = item.Currencies.Length > 0 ? item.Currencies[0].Amount : 0u;
                bool unlimited = item.Amount == uint.MaxValue;
                int amount = unlimited ? -1 : (int) Math.Min(item.Amount, int.MaxValue);
                barterShop.Add(new BarterEntry {
                    Item = ShopItem(item.ItemId, item.ItemTypeRaw, amount, item.Location),
                    Currency = currencyId != 0 ? ShopItem(currencyId, 0, 0) : null,
                    ItemId = item.ItemId, Type = item.ItemTypeRaw, Amount = amount, Unlimited = unlimited, CurrencyId = currencyId,
                    CurrencyAmount = (int) Math.Min(currencyAmount, int.MaxValue), Weight = (int) (item.Weight / 10), ShopIndex = item.ShopIndex,
                    Location = item.Location,
                });
            }

            EnsureBarterWindows();
            barterListSel = barterCartSel = -1;
            barterCartFirst = 0;
            barterListFirst = Mathf.Clamp(barterListFirst, 0, Math.Max(0, barterShop.Count - BarterListRows));
            if (!barterShown) {
                barterShown = true;
                barterListFirst = 0;
                // MakeWindow: Create(280, 400) Move(0, 200) / Create(280, 120) Move(300, 200), then the info records' positions
                // (the list's size too; the purchase window ignores its record's size, msg 0xe v1 7 vs its mode 3)
                ApplyBarterListSize(PlayerPrefs.GetInt(ShopPrefs + "list.w", 280), PlayerPrefs.GetInt(ShopPrefs + "list.h", 312));
                ApplyBarterBuySize(280, 120);
                Place(barterList, PlayerPrefs.GetInt(ShopPrefs + "list.x", 0), PlayerPrefs.GetInt(ShopPrefs + "list.y", 120));
                Place(barterBuy, PlayerPrefs.GetInt(ShopPrefs + "buy.x", 280), PlayerPrefs.GetInt(ShopPrefs + "buy.y", 280));
                barterList.Visible = true;
                barterBuy.Visible = true;
                system.Windows.BringToFront(barterList);
                system.Windows.BringToFront(barterBuy);
                system.Windows.PushEsc(barterList);
                system.Windows.PushEsc(barterBuy);
            }

            barterList.Invalidate();
            barterBuy.Invalidate();
        }

        private void EnsureBarterWindows() {
            if (barterList != null) return;
            var listLayout = new RoClassicWindowLayout { Class = "UIBartermarketWnd", WindowId = 0x14e, Width = 280, Height = 312 };
            barterList = new RoClassicWindow(system, listLayout) { Visible = false, DragSourceAt = BarterListDragSource };
            barterList.DrawDynamic += DrawBarterList;
            barterList.Pressed += (w, local) => SelectBarterRow(true, local);
            barterList.RightClicked += (w, local) => DescribeBarterRow(true, local);
            barterList.Wheeled += (w, local, delta) => ScrollBarter(true, delta);
            barterList.Dropped += OnBarterListDropped;
            barterList.EscClose = () => {
                CancelBarter();
                return true;
            };
            SavePosition(barterList, "list");
            Resizer(barterList, 7);
            barterList.ResizeRequested += (w, mode, width, height) => {
                if (mode != 7) return;
                int newW = ShopWidth(width), newH = ShopHeight(height);
                if (newW == barterList.Width && newH == barterList.Height) return;
                ApplyBarterListSize(newW, newH);
                // StoreInfo vf11 0x8f64e0: the size goes back into the plain list's record
                PlayerPrefs.SetInt(ShopPrefs + "list.w", newW);
                PlayerPrefs.SetInt(ShopPrefs + "list.h", newH);
            };

            var buyLayout = new RoClassicWindowLayout { Class = "UIBarterItemPurchaseWnd", WindowId = 0x14f, Width = 280, Height = 120 };
            buyLayout.Children.Add(BitmapButton(BuyId, "buy", "btn_buy", 280 - 106, 120 - 24));
            buyLayout.Children.Add(BitmapButton(CancelId, "cancel", "btn_cancel", 280 - 61, 120 - 24));
            barterBuy = new RoClassicWindow(system, buyLayout) { Visible = false, DragSourceAt = BarterCartDragSource };
            barterBuy.DrawDynamic += DrawBarterCart;
            barterBuy.Pressed += (w, local) => SelectBarterRow(false, local);
            barterBuy.RightClicked += (w, local) => DescribeBarterRow(false, local);
            barterBuy.Wheeled += (w, local, delta) => ScrollBarter(false, delta);
            barterBuy.Dropped += OnBarterCartDropped;
            barterBuy.Clicked += child => {
                if (child.Id == BuyId) BuyBarter();
                else if (child.Id == CancelId) CancelBarter();
            };
            // Esc on either window: the Cancel path (the client deletes only the front one; spec §1.7 lets the port close both)
            barterBuy.EscClose = () => {
                CancelBarter();
                return true;
            };
            SavePosition(barterBuy, "buy");
            Resizer(barterBuy, 3);
            barterBuy.ResizeRequested += (w, mode, width, height) => {
                if (mode != 3) return;
                // msg 0xe v1 3 (§1.4): W 248 / 280 / 312 and H 120..336 by the list's formulas; the size is not kept
                int newW = ShopWidth(width), newH = ShopHeight(height);
                if (newW != barterBuy.Width || newH != barterBuy.Height) ApplyBarterBuySize(newW, newH);
            };
        }

        /// <summary>msg 0xe (v1 7): rows (H − 46) / 32, the resizer (W − 15, H − 15), the bar (W − 15, 14) over H − 46.</summary>
        private void ApplyBarterListSize(int w, int h) {
            w = ShopWidth(w);
            h = ShopHeight(h);
            barterList.SetWidth(w);
            barterList.SetHeight(h);
            SetOps(barterList, ShopListOps(w, h));
            barterListFirst = Mathf.Clamp(barterListFirst, 0, Math.Max(0, barterShop.Count - BarterListRows));
        }

        /// <summary>
        /// UIBarterItemPurchaseWnd's frame (OnDraw 0x7a5030) at (w, h): title bar, the item columns over H − 38, the button
        /// bar, white at (40, 17) W − 50 wide over the rows, the icon cells; the buttons at (W − 106 / W − 61, H − 24).
        /// </summary>
        private void ApplyBarterBuySize(int w, int h) {
            barterBuy.SetWidth(w);
            barterBuy.SetHeight(h);
            var ops = new List<RoClassicOp>();
            int rows = (h - 38) / 32;
            RoClassicFrames.TitleBar(ops, w, false);
            RoClassicFrames.ItemColumns(ops, w, h - 38);
            RoClassicFrames.ButtonBar(ops, w, h, 2);
            RoClassicFrames.Fill(ops, 40, 17, w - 50, rows * 32, White);
            RoClassicFrames.IconColumn(ops, 40, rows);
            SetOps(barterBuy, ops);
            barterBuy.SetChildPosition("buy", w - 106, h - 24);
            barterBuy.SetChildPosition("cancel", w - 61, h - 24);
            barterCartFirst = Mathf.Clamp(barterCartFirst, 0, Math.Max(0, barterCart.Count - BarterCartRows));
        }

        /// <summary>Both windows go, the manager is cleared (nothing is sent: see <see cref="CancelBarter"/>).</summary>
        private void HideBarter() {
            if (!barterShown) return;
            barterShown = false;
            barterShop.Clear();
            barterCart.Clear();
            if (barterList != null) {
                barterList.Visible = false;
                system.Windows.RemoveEsc(barterList);
            }

            if (barterBuy != null) {
                barterBuy.Visible = false;
                system.Windows.RemoveEsc(barterBuy);
            }

            RoClassicCountWindow.Hide();
        }

        /// <summary>
        /// Cancel 0xb5 (0x14f msg 6): Msg(0x28) deletes 0x14e and 0x14f, whose DeleteWindow sends Msg(0x135) =
        /// CZ_NPC_BARTER_MARKET_CLOSE 0x0b12; then mgr.Clear() — and the session's barter list goes with it.
        /// </summary>
        private void CancelBarter() {
            if (!barterShown) return;
            HideBarter();
            session?.ClearNpcCommerceLocally(RoNpcShopMode.Barter);
            if (session != null) SendSafely(session.BeginNpcBarterClose, "barter close");
        }

        // ================================================================ drawing

        /// <summary>UIBartermarketWnd::OnDraw 0x7a5540 (the frame is the window's ops).</summary>
        private void DrawBarterList(RoClassicWindow w) {
            DrawTitle(w, Msg(0xba, "Shop Items"));
            int rows = BarterListRows;
            for (int r = 0; r < rows; r++) {
                int k = barterListFirst + r;
                if (k >= barterShop.Count) break;
                BarterEntry e = barterShop[k];
                int y = 32 * r;
                if (k == barterListSel) w.FillRect(72, y + 19, w.Width - 88, 28, Selected);
                RoClassicItemText.DrawIcon(w, 44, y + 21, e.Item, metadata, e.Unlimited ? 0 : e.Amount);
                RoClassicItemText.DrawName(w, 76, y + 29, 120, e.Item, metadata);
                if (e.Currency != null) RoClassicItemText.DrawIcon(w, w.Width - 70, y + 21, e.Currency, metadata, 0);
                if (e.CurrencyAmount > 0) w.TextOutRight(w.Width - 25, y + 29, Number(e.CurrencyAmount), 0, 12, Black);
            }

            w.DrawScrollBar(w.Width - 15, 14, w.Height - 46, Math.Max(0, barterShop.Count - rows), barterShop.Count, rows, barterListFirst);
            DrawResizer(w);
        }

        /// <summary>UIBarterItemPurchaseWnd::OnDraw 0x7a5030: the cart rows, the price × count right-aligned at W − 25.</summary>
        private void DrawBarterCart(RoClassicWindow w) {
            DrawTitle(w, Msg(0xe69, "Items you want to exchange"));
            int rows = BarterCartRows;
            for (int r = 0; r < rows; r++) {
                int k = barterCartFirst + r;
                if (k >= barterCart.Count) break;
                BarterEntry e = barterCart[k];
                int y = 32 * r;
                if (k == barterCartSel) w.FillRect(72, y + 19, 191, 28, Selected);
                RoClassicItemText.DrawIcon(w, 44, y + 21, e.Item, metadata, e.Amount);
                RoClassicItemText.DrawName(w, 76, y + 29, 100, e.Item, metadata);
                if (e.Currency != null) RoClassicItemText.DrawIcon(w, w.Width - 70, y + 21, e.Currency, metadata, 0);
                if (e.CurrencyAmount > 0) w.TextOutRight(w.Width - 25, y + 29, Number((long) e.Amount * e.CurrencyAmount), 0, 12, Black);
            }

            w.DrawScrollBar(w.Width - 15, 14, w.Height - 38, Math.Max(0, barterCart.Count - rows), barterCart.Count, rows, barterCartFirst);
            DrawResizer(w);
        }

        // ================================================================ mouse

        /// <summary>
        /// The row under a point: the list's drag hit 0x7a3610 (40 ≤ x &lt; W − 20) or the cart's 0x8d4e00 (x ≥ 40), both
        /// 17 ≤ y &lt; rows·32 + 17.
        /// </summary>
        private int BarterRowAt(bool list, Vector2Int local) {
            RoClassicWindow w = list ? barterList : barterBuy;
            int rows = list ? BarterListRows : BarterCartRows;
            if (local.x < 40 || (list && local.x >= w.Width - 20) || local.y < 17 || local.y >= rows * 32 + 17) return -1;
            int k = (list ? barterListFirst : barterCartFirst) + (local.y - 17) / 32;
            return k < (list ? barterShop : barterCart).Count ? k : -1;
        }

        /// <summary>LBtnDown: the list selects when 39 &lt; x &lt; W − 20 and 16 &lt; y &lt; rows·32 + 17 (+0xe0); the cart by 0x8d4e00 (+0xd8).</summary>
        private void SelectBarterRow(bool list, Vector2Int local) {
            int k;
            if (list) {
                if (local.x <= 39 || local.x >= barterList.Width - 20 || local.y <= 16 || local.y >= BarterListRows * 32 + 17) return;
                k = barterListFirst + (local.y - 17) / 32;
                if (k >= barterShop.Count || k == barterListSel) return;
                barterListSel = k;
                barterList.Invalidate();
                return;
            }

            k = BarterRowAt(false, local);
            if (k < 0 || k == barterCartSel) return;
            barterCartSel = k;
            barterBuy.Invalidate();
        }

        /// <summary>RBtnDown 0x7a6810 / 0x7a6730: the icon cell (40 ≤ x &lt; 72) describes the item, the rest of the row the price item.</summary>
        private void DescribeBarterRow(bool list, Vector2Int local) {
            int k = BarterRowAt(list, local);
            if (k < 0) return;
            BarterEntry e = (list ? barterShop : barterCart)[k];
            if (local.x < 72) RoClassicItemDescription.Toggle(e.Item, metadata);
            else if (e.Currency != null) RoClassicItemDescription.Toggle(e.Currency, metadata);
        }

        /// <summary>Wheel (UIItemBaseWnd 0x8eb530): ± min(rows − 1, 3).</summary>
        private void ScrollBarter(bool list, int delta) {
            int rows = list ? BarterListRows : BarterCartRows;
            int count = (list ? barterShop : barterCart).Count;
            int step = delta > 0 ? -Math.Min(rows - 1, 3) : Math.Min(rows - 1, 3);
            int first = Mathf.Clamp((list ? barterListFirst : barterCartFirst) + step, 0, Math.Max(0, count - rows));
            if (list) barterListFirst = first;
            else barterCartFirst = first;
            (list ? barterList : barterBuy).Invalidate();
        }

        /// <summary>LBtnDown on a list row: DragStart(type 0x1f, entry, 1) (0xc20cd0, the shop index → mode+0x3b8); no stock check.</summary>
        private IRoClassicDragPayload BarterListDragSource(Vector2Int local) {
            int k = BarterRowAt(true, local);
            if (k < 0) return null;
            BarterEntry e = barterShop[k];
            return new RoClassicNpcMarketPayload { Type = DragListToCart, Entry = e, Amount = 1, IconPath = RoClassicItemText.IconPath(e.Item, metadata) };
        }

        /// <summary>LBtnDown on a cart row: DragStart(type 4, entry, the cart amount).</summary>
        private IRoClassicDragPayload BarterCartDragSource(Vector2Int local) {
            int k = BarterRowAt(false, local);
            if (k < 0) return null;
            BarterEntry e = barterCart[k];
            return new RoClassicNpcMarketPayload { Type = DragCartToList, Entry = e, Amount = e.Amount, IconPath = RoClassicItemText.IconPath(e.Item, metadata) };
        }

        /// <summary>
        /// Drop on 0x14f (vf10 0x7a3480): with the trade window up nothing happens; a list row of equipment (or type 0x12)
        /// goes at once, else the amount window (mode 0x18): over 30000 → MsgStr 0x327; the stock (−1 → 30000) caps it.
        /// </summary>
        private void OnBarterCartDropped(RoClassicWindow w, Vector2Int local, IRoClassicDragPayload payload) {
            if (!(payload is RoClassicNpcMarketPayload carried) || carried.Type != DragListToCart || !(carried.Entry is BarterEntry e)) return;
            if (RoClassicTrade.IsOpen) return;
            if (IsEquipType(e.Type) || IsSingleType(e.Type)) {
                AddToBarterCart(e.ShopIndex, 1);
                return;
            }

            RoClassicCountWindow.Show(e.Item.GetFullDisplayName(metadata), carried.Amount, n => {
                if (n <= 0) return;
                if (n > MaxAmount) {
                    Say(Msg(0x327, "You cannot buy more than 30,000ea items at once."));
                    return;
                }

                BarterEntry shop = barterShop.Find(s => s.ShopIndex == e.ShopIndex);
                int stock = shop == null ? 0 : shop.Unlimited ? MaxAmount : shop.Amount;   // 0x59d550
                if (stock == 0) return;
                AddToBarterCart(e.ShopIndex, (int) Math.Min(n, stock));
            });
        }

        /// <summary>
        /// 0x14f msg 0x26 (0x7a69a4): the checks in the client's order — the row still in the shop (0xde2), the price item
        /// not worn (0xe51), one equipment / type-0x12 item per cart (0x77 / 0xdf1), 30000 of an item (0x6a8), the weight
        /// (0x38) — then mgr.addToCart and both windows refreshed. No zeny is involved.
        /// </summary>
        private void AddToBarterCart(uint shopIndex, int n) {
            BarterEntry e = barterShop.Find(s => s.ShopIndex == shopIndex);
            if (e == null) {
                Say(Msg(0xde2, "Exchange failed."));
                return;
            }

            RoItemInstance[] inventory = Inventory();
            if (e.CurrencyId != 0 && IsWorn(inventory, e.CurrencyId)) {
                Say(Msg(0xe51, "Equipped items cannot be exchanged. Please try after unequipping it."));
                return;
            }

            long sameId = 0;
            foreach (BarterEntry c in barterCart) {
                if (c.ItemId == e.ItemId) sameId += c.Amount;
            }

            if (sameId > 0 && IsEquipType(e.Type)) {
                Say(Msg(0x77, "Please avoid buying 2 of the same items at one time."));
                return;
            }

            if (sameId > 0 && IsSingleType(e.Type)) {
                Say(Msg(0xdf1, "You can only buy one item at a time."));
                return;
            }

            if (sameId + n > MaxAmount) {
                Say(Format(Msg(0x6a8, "It is not possible to purchase the same item more than %d pieces at a time"), MaxAmount));
                return;
            }

            int unit = e.Weight;
            if (unit < 0) {
                Say(Msg(0xde2, "Exchange failed."));
                return;
            }

            if (unit > 0) {
                // maxWeight 0x0154f3ac − curWeight 0x0154f3b0 − the cart's weight, all in tenths
                long cartWeight = 0;
                foreach (BarterEntry c in barterCart) cartWeight += (long) c.Weight * c.Amount;
                RoPlayerStatusSnapshot status = Status();
                if ((status.MaxWeight - status.Weight - cartWeight) / unit < n) {
                    Say(Msg(0x38, "You are over your Weight Limit."));
                    return;
                }
            }

            // 0x59d250: merge by shop index or append; a limited stock goes down and an empty row leaves the shop list
            BarterEntry line = barterCart.Find(c => c.ShopIndex == shopIndex);
            if (line != null) line.Amount += n;
            else barterCart.Add(e.Copy(n));
            if (!e.Unlimited) {
                e.Amount -= n;
                if (e.Amount < 1) {
                    barterShop.Remove(e);
                    barterListSel = -1;
                    barterListFirst = Mathf.Clamp(barterListFirst, 0, Math.Max(0, barterShop.Count - BarterListRows));
                }
            }

            barterList.Invalidate();
            barterBuy.Invalidate();
        }

        /// <summary>
        /// Drop on 0x14e (vf10 0x7a3550): a cart line of one piece comes back at once, else the amount window (mode 0x17,
        /// at most the cart amount); other drags end.
        /// </summary>
        private void OnBarterListDropped(RoClassicWindow w, Vector2Int local, IRoClassicDragPayload payload) {
            if (!(payload is RoClassicNpcMarketPayload carried) || carried.Type != DragCartToList || !(carried.Entry is BarterEntry e)) return;
            if (carried.Amount == 1) {
                ReturnFromBarterCart(e.ShopIndex, 1);
                return;
            }

            RoClassicCountWindow.Show(e.Item.GetFullDisplayName(metadata), carried.Amount, n => {
                BarterEntry line = barterCart.Find(c => c.ShopIndex == e.ShopIndex);
                if (n <= 0 || line == null || line.Amount <= 0) return;
                ReturnFromBarterCart(e.ShopIndex, (int) Math.Min(n, line.Amount));
            });
        }

        /// <summary>0x14e msg 0x26 → mgr.returnFromCart 0x59d350: the cart line shrinks, the shop row gets its stock back (or comes back at the end).</summary>
        private void ReturnFromBarterCart(uint shopIndex, int n) {
            BarterEntry line = barterCart.Find(c => c.ShopIndex == shopIndex);
            if (line == null) {
                Say(Msg(0xde2, "Exchange failed."));
                return;
            }

            n = Math.Min(n, line.Amount);
            line.Amount -= n;
            if (line.Amount < 1) {
                barterCart.Remove(line);
                barterCartSel = -1;
                barterCartFirst = Mathf.Clamp(barterCartFirst, 0, Math.Max(0, barterCart.Count - BarterCartRows));
            }

            BarterEntry row = barterShop.Find(s => s.ShopIndex == shopIndex);
            if (row != null) {
                if (!row.Unlimited) row.Amount += n;
            } else {
                barterShop.Add(line.Copy(n));
            }

            barterList.Invalidate();
            barterBuy.Invalidate();
        }

        // ================================================================ buy

        /// <summary>The currency map (+0x18, 0x59ce80): Σ currencyAmount × count per price item, ascending id.</summary>
        private SortedDictionary<uint, long> BarterCurrencyTotals() {
            var totals = new SortedDictionary<uint, long>();
            foreach (BarterEntry c in barterCart) {
                if (c.CurrencyId == 0) continue;
                totals.TryGetValue(c.CurrencyId, out long t);
                totals[c.CurrencyId] = t + (long) c.CurrencyAmount * c.Amount;
            }

            return totals;
        }

        /// <summary>
        /// Buy 0xb4: the checks of 0x7a2e90 (a failed one closes everything like Cancel), then Msg(0x134) =
        /// CZ_NPC_BARTER_MARKET_PURCHASE 0x0b0f in cart order (the price item's first bag index), then Msg(0x28) →
        /// 0x0b12 and mgr.Clear(). The result comes as ZC_PC_PURCHASE_RESULT.
        /// </summary>
        private void BuyBarter() {
            if (!barterShown) return;
            RoItemInstance[] inventory = Inventory();
            int slots = PossessionCount(inventory);
            foreach (BarterEntry c in barterCart) {
                if (IsEquipType(c.Type) || IsSingleType(c.Type) || InventoryAmount(inventory, c.ItemId) == 0) slots++;
            }

            // the inventory expansion (DAT_015556f4) is not known to this client: 100 slots
            if (slots > BaseInventorySlots) {
                Say(Msg(0xdf0, "It is not possible to purchase as it will exceeds the possession limit."));
                CancelBarter();
                return;
            }

            foreach (KeyValuePair<uint, long> total in BarterCurrencyTotals()) {
                if (InventoryAmount(inventory, total.Key) < total.Value) {
                    Say(Msg(0xde4, "Not enough items to exchange."));
                    CancelBarter();
                    return;
                }
            }

            var perItem = new Dictionary<uint, long>();
            foreach (BarterEntry c in barterCart) {
                perItem.TryGetValue(c.ItemId, out long t);
                perItem[c.ItemId] = t + c.Amount;
            }

            foreach (KeyValuePair<uint, long> item in perItem) {
                if (item.Value + InventoryAmount(inventory, item.Key) > MaxAmount) {
                    Say(Format(Msg(0xdef, "You can't have more the same %d item at a time."), MaxAmount));
                    CancelBarter();
                    return;
                }
            }

            // Msg 0x134: not sent for an empty cart or a packet over 0x800 bytes
            if (barterCart.Count > 0 && 4 + 14 * barterCart.Count <= 0x800 && session != null) {
                var requests = new RoNpcBarterPurchaseRequest[barterCart.Count];
                for (int i = 0; i < barterCart.Count; i++) {
                    BarterEntry c = barterCart[i];
                    requests[i] = new RoNpcBarterPurchaseRequest(c.ItemId, (uint) c.Amount, FirstInventoryIndex(inventory, c.CurrencyId), c.ShopIndex);
                }

                SendSafely(() => session.BeginNpcBarterPurchase(requests), "barter purchase");
            }

            CancelBarter();
        }

        // ================================================================ tooltips

        private void HoverBarter(RoClassicWindow over, Vector2Int local) {
            bool list = over == barterList;
            if (!list && Within(local, over.Width - 106, over.Height - 24, 42, 20)) {
                // UIPurchaseButton hover 0x7a6260: one line per price item of the currency map
                var lines = new List<string>();
                foreach (KeyValuePair<uint, long> total in BarterCurrencyTotals()) {
                    lines.Add(Format(Msg(0xde1, "%s %d ea"), ItemName(total.Key), total.Value));
                }

                ShowButtonLines(over, over.Width - 106, over.Height - 24, lines);
                return;
            }

            int k = BarterRowAt(list, local);
            if (k < 0) return;
            BarterEntry e = (list ? barterShop : barterCart)[k];
            if (e.CurrencyId == 0) return;
            // 0x7a6000 / 0x7a5d70: the price, "%s %d ea" (the cart: × its count), in the item's name colour
            long amount = list ? e.CurrencyAmount : (long) e.CurrencyAmount * e.Amount;
            string text = Format(Msg(0xde1, "%s %d ea"), ItemName(e.CurrencyId), amount);
            Vector2Int at = over.Position + RoClassicItemText.RowTooltipAnchor(local);
            RoClassicTooltip.Shared.Show(text, RoClassicItemText.TooltipColor(e.Item, metadata), at.x, at.y);
        }

        // ================================================================ diagnostics (!barter ...)

        /// <summary>
        /// state | select row | add row [n] (a list row dropped on the cart, n into the amount window) | remove row [n] (a
        /// cart line back) | desc row | buy | cancel | esc.
        /// </summary>
        internal static string SweepBarter(string command) {
            RoClassicNpcMarket m = instance;
            if (m == null) return "no classic npc market";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            Vector2Int Row(int k, bool list) => new Vector2Int(60, 17 + 32 * (k - (list ? m.barterListFirst : m.barterCartFirst)) + 16);
            if (m.barterShown) {
                switch (a[0]) {
                    case "select": m.SelectBarterRow(true, Row(Arg(a, 1), true)); break;
                    case "add":
                        if (m.BarterListDragSource(Row(Arg(a, 1), true)) is RoClassicNpcMarketPayload add) {
                            m.OnBarterCartDropped(m.barterBuy, Vector2Int.zero, add);
                            AnswerCount(a, 2);
                        }

                        break;
                    case "remove":
                        if (m.BarterCartDragSource(Row(Arg(a, 1), false)) is RoClassicNpcMarketPayload back) {
                            m.OnBarterListDropped(m.barterList, Vector2Int.zero, back);
                            AnswerCount(a, 2);
                        }

                        break;
                    case "desc": m.DescribeBarterRow(true, Row(Arg(a, 1), true)); break;
                    case "buy": m.BuyBarter(); break;
                    case "cancel": m.CancelBarter(); break;
                    case "esc": m.system.Windows.HandleEscape(); break;
                }
            }

            var s = new StringBuilder();
            s.Append("shown=").Append(m.barterShown);
            if (m.barterList != null) s.Append(" list=").Append(m.barterList.Width).Append('x').Append(m.barterList.Height).Append('@').Append(m.barterList.Position);
            if (m.barterBuy != null) s.Append(" cart=").Append(m.barterBuy.Width).Append('x').Append(m.barterBuy.Height).Append('@').Append(m.barterBuy.Position);
            s.Append(" shop=[");
            for (int i = 0; i < m.barterShop.Count; i++) {
                BarterEntry e = m.barterShop[i];
                s.Append(i == 0 ? "" : ", ").Append(e.ItemId).Append('x').Append(e.Unlimited ? "inf" : Number(e.Amount))
                    .Append(" for ").Append(e.CurrencyId).Append('x').Append(e.CurrencyAmount).Append(" #").Append(e.ShopIndex);
            }

            s.Append("] cart=[");
            for (int i = 0; i < m.barterCart.Count; i++) s.Append(i == 0 ? "" : ", ").Append(m.barterCart[i].ItemId).Append('x').Append(m.barterCart[i].Amount);
            s.Append("] totals=[");
            foreach (KeyValuePair<uint, long> t in m.BarterCurrencyTotals()) s.Append(t.Key).Append('x').Append(t.Value).Append(' ');
            s.Append("] count=").Append(RoClassicCountWindow.Visible ? "'" + RoClassicCountWindow.Text + "'" : "hidden");
            RoNpcShopSnapshot shop = m.session?.NpcShopSnapshot() ?? RoNpcShopSnapshot.Empty;
            s.Append(" session: mode=").Append(shop.Mode).Append(" barterItems=").Append(shop.BarterItems.Length).Append(" seq=").Append(shop.BarterSequence);
            return s.ToString();
        }
    }
}
