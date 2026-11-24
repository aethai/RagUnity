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
    /// The expanded barter shop (npc-barter-market-spec §2): UIExpandedBartermarketWnd 0x155 (the shop, 366 × 366 at
    /// (200, 200)) and UIExpandedBarterItemPurchaseWnd 0x156 (the cart, 366 × 366 at (586, 200), Buy / Cancel), both
    /// UIFrameWnds with 64 px rows: the item, its stock (grey when sold out), its zeny price and a row of price items
    /// (each with its amount and refine). CExpandedBarterMarketItemMgr (0x011a6258) keeps the shop, the cart, the
    /// currency totals by (id, refine) and the zeny total. Items move list → cart (drag type 0x1f, amount window mode
    /// 0x1a) and back (type 0x20, mode 0x19). Buy sends CZ_NPC_EXPANDED_BARTER_MARKET_PURCHASE 0x0b57 then closes:
    /// CZ_NPC_EXPANDED_BARTER_MARKET_CLOSE 0x0b58. Neither window keeps its position or size (no info record).
    /// </summary>
    public sealed partial class RoClassicNpcMarket {
        /// <summary>An entry (0x4c bytes): a shop row (Amount = stock) or a cart line (Amount = count).</summary>
        private sealed class ExpandedEntry {
            public RoItemInstance Item;
            public uint ItemId;
            public int Type;                    // +0x00
            public int Amount;                  // +0x04
            public long Zeny;                   // +0x08, per piece
            public float Weight;                // +0x2c, the packet's weight / 10
            public bool Unlimited;              // +0x30
            public uint ShopIndex;              // +0x34
            public RoNpcBarterCurrency[] Currencies;   // +0x40 {nameid, refine, amount, type}
            public RoItemInstance[] CurrencyItems;

            public ExpandedEntry Copy(int amount) => new ExpandedEntry {
                Item = Item, ItemId = ItemId, Type = Type, Amount = amount, Zeny = Zeny, Weight = Weight, Unlimited = Unlimited,
                ShopIndex = ShopIndex, Currencies = Currencies, CurrencyItems = CurrencyItems,
            };
        }

        /// <summary>A currency total (+0x24 vector): {id, refine, total, type}.</summary>
        private sealed class ExpandedTotal {
            public uint ItemId;
            public int Refine;
            public long Total;
            public int Type;
        }

        private readonly List<ExpandedEntry> expandedShop = new List<ExpandedEntry>();
        private readonly List<ExpandedEntry> expandedCart = new List<ExpandedEntry>();
        private readonly List<ExpandedTotal> expandedTotals = new List<ExpandedTotal>();
        private long expandedZeny;              // +0x38
        private RoClassicWindow expandedList;   // 0x155
        private RoClassicWindow expandedBuy;    // 0x156
        private bool expandedShown;
        private int expandedListFirst;          // +0xa4
        private int expandedListSel = -1;       // +0xac
        private int expandedCartFirst;
        private int expandedCartSel = -1;

        private static int ExpandedRows(RoClassicWindow w) => (w.Height - 46) / 64;

        // ================================================================ open / close

        /// <summary>
        /// Handler 0xc4b550: mgr.Clear() (0x59fe80), the entries, then MakeWindow(0x155) / MakeWindow(0x156) with msg 0x17.
        /// MakeWindow(0x155) closes an open bank (0x113) with MsgStr 0xf7e. (The client's MakeWindow(0x156) returns 0x155
        /// when 0x156 exists, so a repeated list leaves the cart window unrefreshed; here both refresh.)
        /// </summary>
        private void OpenExpanded(RoNpcBarterItem[] items) {
            expandedShop.Clear();
            expandedCart.Clear();
            expandedTotals.Clear();
            expandedZeny = 0;
            foreach (RoNpcBarterItem item in items ?? Array.Empty<RoNpcBarterItem>()) {
                bool unlimited = item.Amount == uint.MaxValue;
                RoNpcBarterCurrency[] currencies = item.Currencies ?? Array.Empty<RoNpcBarterCurrency>();
                var currencyItems = new RoItemInstance[currencies.Length];
                for (int j = 0; j < currencies.Length; j++) {
                    currencyItems[j] = ShopItem(currencies[j].ItemId, currencies[j].ItemTypeRaw, (int) Math.Min(currencies[j].Amount, int.MaxValue),
                        refine: currencies[j].RefineLevel);
                }

                int amount = unlimited ? -1 : (int) Math.Min(item.Amount, int.MaxValue);
                expandedShop.Add(new ExpandedEntry {
                    Item = ShopItem(item.ItemId, item.ItemTypeRaw, amount, item.Location), ItemId = item.ItemId, Type = item.ItemTypeRaw,
                    Amount = amount, Zeny = item.Price, Weight = item.Weight / 10f, Unlimited = unlimited, ShopIndex = item.ShopIndex,
                    Currencies = currencies, CurrencyItems = currencyItems,
                });
            }

            EnsureExpandedWindows();
            expandedListSel = expandedCartSel = -1;
            expandedCartFirst = 0;
            expandedListFirst = Mathf.Clamp(expandedListFirst, 0, Math.Max(0, expandedShop.Count - ExpandedRows(expandedList)));
            if (!expandedShown) {
                if (RoClassicBank.IsOpen) {
                    RoClassicBank.Hide();
                    Say(Msg(0xf7e, "Bank cannot be used when Expanded Barter Shop is open. The bank was forcibly closed."));
                }

                expandedShown = true;
                expandedListFirst = 0;
                // Create(366, 366): the size of a fresh window (the resize rule would round 366 down to 320)
                ApplyExpandedSize(expandedList, 366, 366);
                ApplyExpandedSize(expandedBuy, 366, 366);
                Place(expandedList, 200, 200);
                Place(expandedBuy, 586, 200);
                expandedList.Visible = true;
                expandedBuy.Visible = true;
                system.Windows.BringToFront(expandedList);
                system.Windows.BringToFront(expandedBuy);
                system.Windows.PushEsc(expandedList);
                system.Windows.PushEsc(expandedBuy);
            }

            expandedList.Invalidate();
            expandedBuy.Invalidate();
        }

        private void EnsureExpandedWindows() {
            if (expandedList != null) return;
            var listLayout = new RoClassicWindowLayout { Class = "UIExpandedBartermarketWnd", WindowId = 0x155, Width = 366, Height = 366 };
            expandedList = new RoClassicWindow(system, listLayout) { Visible = false, DragSourceAt = ExpandedListDragSource };
            expandedList.DrawDynamic += w => DrawExpanded(w, true);
            expandedList.Pressed += (w, local) => SelectExpandedRow(true, local);
            expandedList.RightClicked += (w, local) => DescribeExpandedRow(true, local);
            expandedList.Wheeled += (w, local, delta) => ScrollExpanded(true, delta);
            expandedList.Dropped += OnExpandedListDropped;
            // Esc (DeleteWindow of either): each deletes the other, 0x156 sends 0x0b58 once
            expandedList.EscClose = () => {
                CancelExpanded();
                return true;
            };
            Resizer(expandedList, 7);
            expandedList.ResizeRequested += (w, mode, width, height) => {
                if (mode != 7) return;
                // msg 0xe v1 7: W = clamp(w, 366, 439), H = clamp(h, 366, 549) & ~63 (320 / 384 / 448 / 512)
                int newW = Mathf.Clamp(width, 366, 439), newH = Mathf.Clamp(height, 366, 549) & ~63;
                if (newW != expandedList.Width || newH != expandedList.Height) ApplyExpandedSize(expandedList, newW, newH);
            };

            var buyLayout = new RoClassicWindowLayout { Class = "UIExpandedBarterItemPurchaseWnd", WindowId = 0x156, Width = 366, Height = 366 };
            buyLayout.Children.Add(BitmapButton(BuyId, "buy", "btn_buy", 366 - 105, 366 - 24));
            buyLayout.Children.Add(BitmapButton(CancelId, "cancel", "btn_cancel", 366 - 60, 366 - 24));
            expandedBuy = new RoClassicWindow(system, buyLayout) { Visible = false, DragSourceAt = ExpandedCartDragSource };
            expandedBuy.DrawDynamic += w => DrawExpanded(w, false);
            expandedBuy.Pressed += (w, local) => SelectExpandedRow(false, local);
            expandedBuy.RightClicked += (w, local) => DescribeExpandedRow(false, local);
            expandedBuy.Wheeled += (w, local, delta) => ScrollExpanded(false, delta);
            expandedBuy.Dropped += OnExpandedCartDropped;
            expandedBuy.Clicked += child => {
                if (child.Id == BuyId) BuyExpanded();
                else if (child.Id == CancelId) CancelExpanded();
            };
            expandedBuy.EscClose = () => {
                CancelExpanded();
                return true;
            };
            Resizer(expandedBuy, 3);
            expandedBuy.ResizeRequested += (w, mode, width, height) => {
                if (mode != 3) return;
                // msg 0xe v1 3: the width stays, the height by the list's rule
                int newH = Mathf.Clamp(height, 366, 549) & ~63;
                if (newH != expandedBuy.Height) ApplyExpandedSize(expandedBuy, expandedBuy.Width, newH);
            };
        }

        /// <summary>
        /// The frame of OnDraw 0x7aa320 / 0x7a9d20 at (w, h): the title bar, itemwin_left / itemwin_right every 8 px down
        /// H − 46, white at (20, 17) W − 40 × H − 46, the 29 px button bar; the resizer, the bar and the cart's buttons
        /// (W − 105 / W − 60, H − 24) follow.
        /// </summary>
        private void ApplyExpandedSize(RoClassicWindow window, int w, int h) {
            window.SetWidth(w);
            window.SetHeight(h);
            var ops = new List<RoClassicOp>();
            RoClassicFrames.TitleBar(ops, w, false);
            int n = (h - 46 + 7) / 8;
            for (int k = 0; k < n; k++) RoClassicFrames.Bitmap(ops, Basic + "itemwin_left.bmp", 0, 17 + 8 * k);
            for (int k = 0; k < n; k++) RoClassicFrames.Bitmap(ops, Basic + "itemwin_right.bmp", w - 20, 17 + 8 * k);
            RoClassicFrames.Fill(ops, 20, 17, w - 40, h - 46, White);
            RoClassicFrames.ButtonBar(ops, w, h, 2);
            SetOps(window, ops);
            if (window == expandedBuy) {
                window.SetChildPosition("buy", w - 105, h - 24);
                window.SetChildPosition("cancel", w - 60, h - 24);
                expandedCartFirst = Mathf.Clamp(expandedCartFirst, 0, Math.Max(0, expandedCart.Count - ExpandedRows(window)));
            } else {
                expandedListFirst = Mathf.Clamp(expandedListFirst, 0, Math.Max(0, expandedShop.Count - ExpandedRows(window)));
            }
        }

        private void HideExpanded() {
            if (!expandedShown) return;
            expandedShown = false;
            expandedShop.Clear();
            expandedCart.Clear();
            expandedTotals.Clear();
            expandedZeny = 0;
            if (expandedList != null) {
                expandedList.Visible = false;
                system.Windows.RemoveEsc(expandedList);
            }

            if (expandedBuy != null) {
                expandedBuy.Visible = false;
                system.Windows.RemoveEsc(expandedBuy);
            }

            RoClassicCountWindow.Hide();
        }

        /// <summary>
        /// Cancel 0xb5 (0x156 msg 6) and Esc: Msg(0x28) deletes 0x155 and 0x156, whose DeleteWindow sends Msg(0x142) =
        /// CZ_NPC_EXPANDED_BARTER_MARKET_CLOSE 0x0b58; mgr.Clear() and the session's list go too.
        /// </summary>
        private void CancelExpanded() {
            if (!expandedShown) return;
            HideExpanded();
            session?.ClearNpcCommerceLocally(RoNpcShopMode.ExpandedBarter);
            if (session != null) SendSafely(session.BeginNpcExpandedBarterClose, "expanded barter close");
        }

        // ================================================================ drawing

        /// <summary>
        /// OnDraw 0x7aa320 (list) / 0x7a9d20 (cart): per row (y0 = 21 + 64r) the icon cell and one cell per price item, the
        /// selection, the item (list: no amount when unlimited, grey when sold out; cart: its count), the name, the price
        /// items with their amounts (cart: × the count) and "+refine", and "%d z" right-aligned at W − 20; the empty rows
        /// keep three cells.
        /// </summary>
        private void DrawExpanded(RoClassicWindow w, bool list) {
            DrawTitle(w, list ? Msg(0xba, "Shop Items") : Msg(0xe69, "Items you want to exchange"));
            List<ExpandedEntry> entries = list ? expandedShop : expandedCart;
            int rows = ExpandedRows(w), first = list ? expandedListFirst : expandedCartFirst, sel = list ? expandedListSel : expandedCartSel;
            int drawn = 0;
            for (int r = 0; r < rows; r++) {
                int k = first + r;
                if (k >= entries.Count) break;
                drawn++;
                ExpandedEntry e = entries[k];
                int y0 = 21 + 64 * r;
                w.DrawBitmapRaw(40, y0 - 4, Basic + "itemwin_mid.bmp");
                for (int j = 0; j < e.Currencies.Length; j++) w.DrawBitmapRaw(72 + 42 * j, y0 + 28, Basic + "itemwin_mid.bmp");
                if (k == sel) w.FillRect(72, y0 - 2, w.Width - 90, 60, Selected);
                bool soldOut = list && !e.Unlimited && e.Amount == 0;
                if (list) RoClassicItemText.DrawIcon(w, 44, y0, e.Item, metadata, e.Unlimited ? 0 : e.Amount, soldOut);
                else RoClassicItemText.DrawIcon(w, 44, y0, e.Item, metadata, e.Amount);
                RoClassicItemText.DrawName(w, 76, y0 + 8, 100, e.Item, metadata);
                for (int j = 0; j < e.Currencies.Length; j++) {
                    RoNpcBarterCurrency c = e.Currencies[j];
                    long amount = list ? c.Amount : (long) c.Amount * e.Amount;
                    RoClassicItemText.DrawIcon(w, 76 + 42 * j, y0 + 28, e.CurrencyItems[j], metadata, (int) Math.Min(amount, int.MaxValue), soldOut);
                    if (c.RefineLevel > 0) {
                        // 0x9b4d40: "+%d" white with an outline (its colour and the bold weight are inferred)
                        w.TextOutOutline(84 + 42 * j, y0 + 28, "+" + Number(c.RefineLevel), 0, 12, White, Black, true);
                    }
                }

                long zeny = list ? e.Zeny : e.Zeny * e.Amount;
                w.TextOutRight(w.Width - 20, y0 + 39, Number(zeny) + " z", 0, 12, Black);
            }

            for (int r = drawn; r < rows; r++) {
                w.DrawBitmapRaw(40, 17 + 64 * r, Basic + "itemwin_mid.bmp");
                w.DrawBitmapRaw(72, 49 + 64 * r, Basic + "itemwin_mid.bmp");
                w.DrawBitmapRaw(114, 49 + 64 * r, Basic + "itemwin_mid.bmp");
            }

            w.DrawScrollBar(w.Width - 15, 14, w.Height - 46, Math.Max(0, entries.Count - rows), entries.Count, rows, first);
            DrawResizer(w);
        }

        // ================================================================ mouse

        private int ExpandedRowAt(bool list, Vector2Int local) {
            RoClassicWindow w = list ? expandedList : expandedBuy;
            if (local.x < 40 || local.x >= w.Width - 20 || local.y < 17 || local.y >= ExpandedRows(w) * 64 + 17) return -1;
            int k = (list ? expandedListFirst : expandedCartFirst) + (local.y - 17) / 64;
            return k < (list ? expandedShop : expandedCart).Count ? k : -1;
        }

        /// <summary>LBtnDown 0x7aaaf0 / 0x7aa9b0: select when 39 &lt; x &lt; W − 20 and 16 &lt; y &lt; rows·64 + 17.</summary>
        private void SelectExpandedRow(bool list, Vector2Int local) {
            RoClassicWindow w = list ? expandedList : expandedBuy;
            if (local.x <= 39 || local.x >= w.Width - 20 || local.y <= 16 || local.y >= ExpandedRows(w) * 64 + 17) return;
            int k = (list ? expandedListFirst : expandedCartFirst) + (local.y - 17) / 64;
            if (k >= (list ? expandedShop : expandedCart).Count) return;
            if (list) expandedListSel = k;
            else expandedCartSel = k;
            w.Invalidate();
        }

        /// <summary>
        /// RBtnDown 0x7ab720 / 0x7ab640 (hit 0x7a8ec0): in the lower half of a row over price item j (72 + 42j &lt; x &lt;
        /// 114 + 42j) that item with its refine, else 40 &lt; x &lt; 72 the row's item.
        /// </summary>
        private void DescribeExpandedRow(bool list, Vector2Int local) {
            int k = ExpandedRowAt(list, local);
            if (k < 0) return;
            ExpandedEntry e = (list ? expandedShop : expandedCart)[k];
            if ((local.y - 17) / 32 % 2 == 1) {
                for (int j = 0; j < e.Currencies.Length; j++) {
                    if (local.x > 72 + 42 * j && local.x < 114 + 42 * j) {
                        RoClassicItemDescription.Toggle(e.CurrencyItems[j], metadata);
                        return;
                    }
                }
            }

            if (local.x > 40 && local.x < 72) RoClassicItemDescription.Toggle(e.Item, metadata);
        }

        /// <summary>Wheel (0x7ab850 / 0x7ab800): when it scrolls, ∓ min(rows − 1, 3), clamped to [0, count − rows].</summary>
        private void ScrollExpanded(bool list, int delta) {
            RoClassicWindow w = list ? expandedList : expandedBuy;
            int rows = ExpandedRows(w), count = (list ? expandedShop : expandedCart).Count;
            if (count <= rows) return;
            int step = delta > 0 ? -Math.Min(rows - 1, 3) : Math.Min(rows - 1, 3);
            int first = Mathf.Clamp((list ? expandedListFirst : expandedCartFirst) + step, 0, Math.Max(0, count - rows));
            if (list) expandedListFirst = first;
            else expandedCartFirst = first;
            w.Invalidate();
        }

        /// <summary>List hit 0x7a8a40: a sold-out row says MsgStr 0xde5 instead of starting DragStart(type 0x1f, entry, 1).</summary>
        private IRoClassicDragPayload ExpandedListDragSource(Vector2Int local) {
            int k = ExpandedRowAt(true, local);
            if (k < 0) return null;
            ExpandedEntry e = expandedShop[k];
            if (!e.Unlimited && e.Amount == 0) {
                Say(Msg(0xde5, "The item has been sold and out of stock."));
                return null;
            }

            return new RoClassicNpcMarketPayload { Type = DragListToCart, Entry = e, Amount = 1, IconPath = RoClassicItemText.IconPath(e.Item, metadata) };
        }

        /// <summary>A cart row: DragStart(type 0x20, entry, its count).</summary>
        private IRoClassicDragPayload ExpandedCartDragSource(Vector2Int local) {
            int k = ExpandedRowAt(false, local);
            if (k < 0) return null;
            ExpandedEntry e = expandedCart[k];
            return new RoClassicNpcMarketPayload { Type = DragExpandedCartToList, Entry = e, Amount = e.Amount, IconPath = RoClassicItemText.IconPath(e.Item, metadata) };
        }

        /// <summary>Drop on 0x156 (0x7a8270): equipment / type 0x12 at once, else the amount window (mode 0x1a, 30000 at most, the stock caps it).</summary>
        private void OnExpandedCartDropped(RoClassicWindow w, Vector2Int local, IRoClassicDragPayload payload) {
            if (!(payload is RoClassicNpcMarketPayload carried) || carried.Type != DragListToCart || !(carried.Entry is ExpandedEntry e)) return;
            if (IsEquipType(e.Type) || IsSingleType(e.Type)) {
                AddToExpandedCart(e.ShopIndex, 1);
                return;
            }

            RoClassicCountWindow.Show(e.Item.GetFullDisplayName(metadata), carried.Amount, n => {
                if (n <= 0) return;
                if (n > MaxAmount) {
                    Say(Msg(0x327, "You cannot buy more than 30,000ea items at once."));
                    return;
                }

                ExpandedEntry shop = expandedShop.Find(s => s.ShopIndex == e.ShopIndex);
                int stock = shop == null ? 0 : shop.Unlimited ? MaxAmount : shop.Amount;   // 0x59f810
                if (stock == 0) return;
                AddToExpandedCart(e.ShopIndex, (int) Math.Min(n, stock));
            });
        }

        /// <summary>
        /// 0x156 msg 0x26 (0x7aba36): the row (0xde2), the price items not only worn (0xe51), one equipment / type-0x12 item
        /// (0x77 / 0xdf1), 30000 per line (0xe6c), the weight (0x38), the zeny (0x37); then mgr add 0x59f4a0.
        /// </summary>
        private void AddToExpandedCart(uint shopIndex, int n) {
            ExpandedEntry e = expandedShop.Find(s => s.ShopIndex == shopIndex);
            if (e == null) {
                Say(Msg(0xde2, "Exchange failed."));
                return;
            }

            RoItemInstance[] inventory = Inventory();
            foreach (RoNpcBarterCurrency c in e.Currencies) {
                // 0xce9c60 / 0xd030c0 (partly inferred): a price item held only as worn pieces cannot pay
                bool unworn = false, worn = false;
                foreach (RoItemInstance item in inventory) {
                    if (item == null || (uint) item.ItemId != c.ItemId || item.RefineLevel < c.RefineLevel) continue;
                    if (item.IsEquipped) worn = true;
                    else if (item.Count > 0) unworn = true;
                }

                if (worn && !unworn) {
                    Say(Msg(0xe51, "Equipped items cannot be exchanged. Please try after unequipping it."));
                    return;
                }
            }

            bool inCart = expandedCart.Exists(c => c.ItemId == e.ItemId);
            if (inCart && IsEquipType(e.Type)) {
                Say(Msg(0x77, "Please avoid buying 2 of the same items at one time."));
                return;
            }

            if (inCart && IsSingleType(e.Type)) {
                Say(Msg(0xdf1, "You can only buy one item at a time."));
                return;
            }

            ExpandedEntry line = expandedCart.Find(c => c.ShopIndex == shopIndex);
            if ((line?.Amount ?? 0) + n > MaxAmount) {
                Say(Format(Msg(0xe6c, "Up to %d items can be exchanged at once."), MaxAmount));
                return;
            }

            float unit = e.Weight;   // 0x59fa40
            if (unit < 0f) {
                Say(Msg(0xde2, "Exchange failed."));
                return;
            }

            RoPlayerStatusSnapshot status = Status();
            if (unit > 0f) {
                float cartWeight = 0f;
                foreach (ExpandedEntry c in expandedCart) cartWeight += c.Weight * c.Amount;
                if ((status.MaxWeight - status.Weight - cartWeight) / unit < n) {
                    Say(Msg(0x38, "You are over your Weight Limit."));
                    return;
                }
            }

            if (n * e.Zeny + expandedZeny > status.Zeny) {
                Say(Msg(0x37, "You do not have enough zeny."));
                return;
            }

            // 0x59f4a0: merge by shop index; the stock goes down to 0 at most (the row stays, drawn grey)
            if (line != null) line.Amount += n;
            else expandedCart.Add(e.Copy(n));
            if (!e.Unlimited) e.Amount = Math.Max(0, e.Amount - n);
            foreach (RoNpcBarterCurrency c in e.Currencies) AddExpandedTotal(c, (long) c.Amount * n);
            RecomputeExpandedZeny();
            expandedList.Invalidate();
            expandedBuy.Invalidate();
        }

        private void AddExpandedTotal(RoNpcBarterCurrency c, long delta) {
            ExpandedTotal t = expandedTotals.Find(x => x.ItemId == c.ItemId && x.Refine == c.RefineLevel);
            if (t == null) {
                if (delta <= 0) return;
                expandedTotals.Add(new ExpandedTotal { ItemId = c.ItemId, Refine = c.RefineLevel, Total = delta, Type = c.ItemTypeRaw });
                return;
            }

            t.Total += delta;
            if (t.Total < 1) expandedTotals.Remove(t);
        }

        private void RecomputeExpandedZeny() {
            expandedZeny = 0;
            foreach (ExpandedEntry c in expandedCart) expandedZeny += c.Zeny * c.Amount;
        }

        /// <summary>Drop on 0x155 (0x7a8320): a cart line of one piece comes back at once, else the amount window (mode 0x19, at most its count).</summary>
        private void OnExpandedListDropped(RoClassicWindow w, Vector2Int local, IRoClassicDragPayload payload) {
            if (!(payload is RoClassicNpcMarketPayload carried) || carried.Type != DragExpandedCartToList || !(carried.Entry is ExpandedEntry e)) return;
            if (carried.Amount == 1) {
                ReturnFromExpandedCart(e.ShopIndex, 1);
                return;
            }

            RoClassicCountWindow.Show(e.Item.GetFullDisplayName(metadata), carried.Amount, n => {
                ExpandedEntry line = expandedCart.Find(c => c.ShopIndex == e.ShopIndex);
                if (n <= 0 || line == null) return;
                ReturnFromExpandedCart(e.ShopIndex, (int) Math.Min(n, line.Amount));
            });
        }

        /// <summary>0x155 msg 0x26 (0x7ac330) → 0x59f290: the line shrinks, the stock comes back, the totals go down (silent when the line is gone).</summary>
        private void ReturnFromExpandedCart(uint shopIndex, int n) {
            ExpandedEntry line = expandedCart.Find(c => c.ShopIndex == shopIndex);
            if (line == null) return;
            n = Math.Min(n, line.Amount);
            line.Amount -= n;
            if (line.Amount < 1) {
                expandedCart.Remove(line);
                expandedCartSel = -1;
                expandedCartFirst = Mathf.Clamp(expandedCartFirst, 0, Math.Max(0, expandedCart.Count - ExpandedRows(expandedBuy)));
            }

            ExpandedEntry row = expandedShop.Find(s => s.ShopIndex == shopIndex);
            if (row != null) {
                if (!row.Unlimited) row.Amount += n;
            } else {
                expandedShop.Add(line.Copy(n));
            }

            foreach (RoNpcBarterCurrency c in line.Currencies) AddExpandedTotal(c, -(long) c.Amount * n);
            RecomputeExpandedZeny();
            expandedList.Invalidate();
            expandedBuy.Invalidate();
        }

        // ================================================================ buy

        /// <summary>
        /// Buy 0xb4: the checks of 0x7a7b50 (a failed one closes like Cancel) — the possession count (equipment / type 0x12
        /// lines only: the client drops its stackable lookup), the price items (0x59fb30: unworn copies with the id and at
        /// least the refine, used up greedily), 30000 per item id — then Msg(0x141) = 0x0b57 and Msg(0x28) → 0x0b58.
        /// </summary>
        private void BuyExpanded() {
            if (!expandedShown) return;
            RoItemInstance[] inventory = Inventory();
            int slots = PossessionCount(inventory);
            foreach (ExpandedEntry c in expandedCart) {
                if (IsEquipType(c.Type) || IsSingleType(c.Type)) slots++;
            }

            if (slots > BaseInventorySlots) {
                Say(Msg(0xdf0, "It is not possible to purchase as it will exceeds the possession limit."));
                CancelExpanded();
                return;
            }

            var pool = new List<(uint Id, int Refine, long Count)>();
            foreach (RoItemInstance item in inventory) {
                if (item != null && !item.IsEquipped && item.Count > 0) pool.Add(((uint) item.ItemId, item.RefineLevel, item.Count));
            }

            foreach (ExpandedTotal t in expandedTotals) {
                long need = t.Total;
                for (int i = 0; i < pool.Count && need > 0; i++) {
                    if (pool[i].Id != t.ItemId || pool[i].Refine < t.Refine || pool[i].Count <= 0) continue;
                    long take = Math.Min(need, pool[i].Count);
                    pool[i] = (pool[i].Id, pool[i].Refine, pool[i].Count - take);
                    need -= take;
                }

                if (need > 0) {
                    Say(Msg(0xde4, "Not enough items to exchange."));
                    CancelExpanded();
                    return;
                }
            }

            var perItem = new Dictionary<uint, long>();
            foreach (ExpandedEntry c in expandedCart) {
                perItem.TryGetValue(c.ItemId, out long t);
                perItem[c.ItemId] = t + c.Amount;
            }

            foreach (KeyValuePair<uint, long> item in perItem) {
                if (item.Value + InventoryAmount(inventory, item.Key) > MaxAmount) {
                    Say(Format(Msg(0xe6b, "You can have up to %d of the same item."), MaxAmount));
                    CancelExpanded();
                    return;
                }
            }

            if (expandedCart.Count > 0 && 4 + 12 * expandedCart.Count <= 0x800 && session != null) {
                var requests = new RoNpcExpandedBarterPurchaseRequest[expandedCart.Count];
                for (int i = 0; i < expandedCart.Count; i++) {
                    requests[i] = new RoNpcExpandedBarterPurchaseRequest(expandedCart[i].ItemId, expandedCart[i].ShopIndex, (uint) expandedCart[i].Amount);
                }

                SendSafely(() => session.BeginNpcExpandedBarterPurchase(requests), "expanded barter purchase");
            }

            CancelExpanded();
        }

        // ================================================================ tooltips

        private string CurrencyLine(uint itemId, int refine, long amount) =>
            (refine > 0 ? "+" + Number(refine) + " " : string.Empty) + Format(Msg(0xde1, "%s %d ea"), ItemName(itemId), amount);

        private void HoverExpanded(RoClassicWindow over, Vector2Int local) {
            bool list = over == expandedList;
            if (!list && Within(local, over.Width - 105, over.Height - 24, 42, 20)) {
                // UIExpandedBarterPurchaseButton hover 0x7aaf60: the totals, then "Total: %d z" when there is zeny
                var lines = new List<string>();
                foreach (ExpandedTotal t in expandedTotals) lines.Add(CurrencyLine(t.ItemId, t.Refine, t.Total));
                if (expandedZeny > 0) lines.Add(Format(Msg(0xe73, "Total: %d z"), expandedZeny));
                ShowButtonLines(over, over.Width - 105, over.Height - 24, lines);
                return;
            }

            // 0x7ab350 / 0x7aac70: over the icon cell (41 ≤ x ≤ 71), one line per price item
            if (local.x < 41 || local.x > 71) return;
            int k = ExpandedRowAt(list, local);
            if (k < 0) return;
            ExpandedEntry e = (list ? expandedShop : expandedCart)[k];
            if (e.Currencies.Length == 0) return;
            var text = new StringBuilder();
            foreach (RoNpcBarterCurrency c in e.Currencies) {
                if (text.Length > 0) text.Append('\n');
                text.Append(CurrencyLine(c.ItemId, c.RefineLevel, list ? c.Amount : (long) c.Amount * e.Amount));
            }

            RoClassicTooltip.Shared.Show(text.ToString(), RoClassicItemText.TooltipColor(e.Item, metadata),
                over.Position.x + local.x, over.Position.y + 1 + (local.y - 17) / 64 * 64);
        }

        // ================================================================ diagnostics (!xbarter ...)

        /// <summary>state | select row | add row [n] | remove row [n] | desc row | buy | cancel | esc.</summary>
        internal static string SweepExpanded(string command) {
            RoClassicNpcMarket m = instance;
            if (m == null) return "no classic npc market";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            Vector2Int Row(int k, bool list) => new Vector2Int(56, 17 + 64 * (k - (list ? m.expandedListFirst : m.expandedCartFirst)) + 16);
            if (m.expandedShown) {
                switch (a[0]) {
                    case "select": m.SelectExpandedRow(true, Row(Arg(a, 1), true)); break;
                    case "add":
                        if (m.ExpandedListDragSource(Row(Arg(a, 1), true)) is RoClassicNpcMarketPayload add) {
                            m.OnExpandedCartDropped(m.expandedBuy, Vector2Int.zero, add);
                            AnswerCount(a, 2);
                        }

                        break;
                    case "remove":
                        if (m.ExpandedCartDragSource(Row(Arg(a, 1), false)) is RoClassicNpcMarketPayload back) {
                            m.OnExpandedListDropped(m.expandedList, Vector2Int.zero, back);
                            AnswerCount(a, 2);
                        }

                        break;
                    case "desc": m.DescribeExpandedRow(true, Row(Arg(a, 1), true)); break;
                    case "buy": m.BuyExpanded(); break;
                    case "cancel": m.CancelExpanded(); break;
                    case "esc": m.system.Windows.HandleEscape(); break;
                }
            }

            var s = new StringBuilder();
            s.Append("shown=").Append(m.expandedShown);
            if (m.expandedList != null) s.Append(" list=").Append(m.expandedList.Width).Append('x').Append(m.expandedList.Height).Append('@').Append(m.expandedList.Position);
            if (m.expandedBuy != null) s.Append(" cart=").Append(m.expandedBuy.Width).Append('x').Append(m.expandedBuy.Height).Append('@').Append(m.expandedBuy.Position);
            s.Append(" shop=[");
            for (int i = 0; i < m.expandedShop.Count; i++) {
                ExpandedEntry e = m.expandedShop[i];
                s.Append(i == 0 ? "" : ", ").Append(e.ItemId).Append('x').Append(e.Unlimited ? "inf" : Number(e.Amount)).Append(' ').Append(e.Zeny).Append("z for");
                foreach (RoNpcBarterCurrency c in e.Currencies) s.Append(' ').Append(c.RefineLevel > 0 ? "+" + c.RefineLevel : "").Append(c.ItemId).Append('x').Append(c.Amount);
                s.Append(" #").Append(e.ShopIndex);
            }

            s.Append("] cart=[");
            for (int i = 0; i < m.expandedCart.Count; i++) s.Append(i == 0 ? "" : ", ").Append(m.expandedCart[i].ItemId).Append('x').Append(m.expandedCart[i].Amount);
            s.Append("] totals=[");
            foreach (ExpandedTotal t in m.expandedTotals) s.Append(t.Refine > 0 ? "+" + t.Refine : "").Append(t.ItemId).Append('x').Append(t.Total).Append(' ');
            s.Append("] zeny=").Append(m.expandedZeny).Append(" count=").Append(RoClassicCountWindow.Visible ? "'" + RoClassicCountWindow.Text + "'" : "hidden");
            RoNpcShopSnapshot shop = m.session?.NpcShopSnapshot() ?? RoNpcShopSnapshot.Empty;
            s.Append(" session: mode=").Append(shop.Mode).Append(" expandedItems=").Append(shop.ExpandedBarterItems.Length).Append(" seq=").Append(shop.ExpandedBarterSequence);
            return s.ToString();
        }
    }
}
