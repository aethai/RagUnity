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
    /// The NPC market, the client's "Para" windows (npc-barter-market-spec §3): UIParaItemShopWnd 0xfe (the shop list,
    /// a UIItemShopWnd with its own OnDraw 0x90a340: the icon column at x 30, the stock as the icon's amount, grey when sold
    /// out), UIParaItemPurchaseWnd 0xff (UIItemPurchaseWnd: "Buying Items", "Total : %s Zeny", Buy / Cancel) and
    /// UIParaResultWnd 0x100 ("Item purchase successful", the bought items, OK). Items move list → cart (drag type 3,
    /// amount window mode 0x15) and back (type 4, mode 0x16). Buy sends CZ_NPC_MARKET_PURCHASE 0x09d6 and hides the cart;
    /// ZC_NPC_MARKET_PURCHASE_RESULT 0x0b4e reports the result and lists what was bought. Cancel, OK and an empty result
    /// send CZ_NPC_MARKET_CLOSE 0x09d8.
    /// </summary>
    public sealed partial class RoClassicNpcMarket {
        private const string LayoutRoot = "Ragnarok/Generated/OriginalClient/ui/";

        /// <summary>A row of the session's shop list (ITEM_INFO from 0xce42a0) or a cart line.</summary>
        private sealed class MarketEntry {
            public RoItemInstance Item;
            public uint ItemId;
            public int Type;
            public long Price;
            public int Amount;      // list: the stock left (−1 unlimited); cart / result: the count
            public int Weight;      // the packet's weight / 10
            public bool Equipment;

            public MarketEntry Copy(int amount) => new MarketEntry {
                Item = Item, ItemId = ItemId, Type = Type, Price = Price, Amount = amount, Weight = Weight, Equipment = Equipment,
            };
        }

        private readonly List<MarketEntry> marketShop = new List<MarketEntry>();
        private readonly List<MarketEntry> marketCartLines = new List<MarketEntry>();
        private readonly List<MarketEntry> marketResultLines = new List<MarketEntry>();
        private RoClassicWindow marketList;     // 0xfe
        private RoClassicWindow marketCart;     // 0xff
        private RoClassicWindow marketResult;   // 0x100
        private bool marketShown;               // 0xfe and 0xff up
        private bool marketCartHidden;          // 0xff hidden by Buy (SetShow 0), waiting for the result
        private bool marketResultShown;
        private int marketListFirst;
        private int marketListSel = -1;
        private int marketCartFirst;
        private int marketCartSel = -1;
        private int marketResultFirst;

        private bool MarketAnyShown => marketShown || marketResultShown;
        private int MarketListRows => (marketList.Height - 46) / 32;
        private int MarketCartRows => (marketCart.Height - 58) / 32;
        private int MarketResultRows => (marketResult.Height - 46) / 32;

        // ================================================================ open / close

        /// <summary>
        /// Handler 0xc5b400: session.ClearShopLists() (0xce5220), every entry as ITEM_INFO {index −1, price, price, type,
        /// identified} with its stock (0xce42a0), then MakeWindow(0xfe) / MakeWindow(0xff) with msg 0x24(0) and 0x17 (a hidden
        /// 0xff shows again).
        /// </summary>
        private void OpenMarket(RoNpcMarketItem[] items) {
            marketShop.Clear();
            marketCartLines.Clear();
            foreach (RoNpcMarketItem item in items ?? Array.Empty<RoNpcMarketItem>()) {
                int stock = item.Stock == uint.MaxValue ? -1 : (int) Math.Min(item.Stock, int.MaxValue);
                RoItemInstance shown = ShopItem(item.ItemId, item.ItemTypeRaw, stock, item.Location);
                marketShop.Add(new MarketEntry {
                    Item = shown, ItemId = item.ItemId, Type = item.ItemTypeRaw, Price = item.Price, Amount = stock, Weight = (int) (item.Weight / 10),
                    Equipment = IsEquipType(item.ItemTypeRaw),
                });
            }

            EnsureMarketWindows();
            marketListSel = marketCartSel = -1;
            marketListFirst = marketCartFirst = 0;
            if (!marketShown) {
                marketShown = true;
                // Create(280, 120) Move(0, 200) / Move(300, 200), then the plain shop's info records (both sizes restored)
                ApplyMarketListSize(PlayerPrefs.GetInt(ShopPrefs + "list.w", 280), PlayerPrefs.GetInt(ShopPrefs + "list.h", 312));
                ApplyMarketCartSize(PlayerPrefs.GetInt(ShopPrefs + "buy.h", 152));
                Place(marketList, PlayerPrefs.GetInt(ShopPrefs + "list.x", 0), PlayerPrefs.GetInt(ShopPrefs + "list.y", 120));
                Place(marketCart, PlayerPrefs.GetInt(ShopPrefs + "buy.x", 280), PlayerPrefs.GetInt(ShopPrefs + "buy.y", 280));
            }

            marketCartHidden = false;
            marketList.Visible = true;
            marketCart.Visible = true;
            system.Windows.BringToFront(marketList);
            system.Windows.BringToFront(marketCart);
            system.Windows.PushEsc(marketList);
            system.Windows.PushEsc(marketCart);
            UpdateMarketTotal();
            marketList.Invalidate();
        }

        private void EnsureMarketWindows() {
            if (marketList != null) return;
            var listLayout = new RoClassicWindowLayout { Class = "UIParaItemShopWnd", WindowId = 0xfe, Width = 280, Height = 120 };
            marketList = new RoClassicWindow(system, listLayout) { Visible = false, DragSourceAt = MarketListDragSource };
            marketList.DrawDynamic += DrawMarketList;
            marketList.Pressed += (w, local) => SelectMarketRow(true, local);
            marketList.RightClicked += (w, local) => DescribeMarketRow(true, local);
            marketList.Wheeled += (w, local, delta) => ScrollMarket(true, delta);
            marketList.Dropped += OnMarketListDropped;
            // Esc: the client deletes the front window only (0xff → 0x09d8, 0xfe silently); here either closes both (Cancel)
            marketList.EscClose = () => {
                CancelMarket();
                return true;
            };
            SavePosition(marketList, "list");
            Resizer(marketList, 7);
            marketList.ResizeRequested += (w, mode, width, height) => {
                if (mode != 7) return;
                int newW = ShopWidth(width), newH = ShopHeight(height);
                if (newW == marketList.Width && newH == marketList.Height) return;
                ApplyMarketListSize(newW, newH);
                PlayerPrefs.SetInt(ShopPrefs + "list.w", newW);
                PlayerPrefs.SetInt(ShopPrefs + "list.h", newH);
            };

            // 0xff: UIItemPurchaseWnd's OnCreate / OnDraw (the oracle's layout of the plain purchase window)
            var asset = Resources.Load<TextAsset>(LayoutRoot + "UIItemPurchaseWnd");
            RoClassicWindowLayout cartLayout = asset != null
                ? RoClassicWindowLayout.Parse(asset.text)
                : new RoClassicWindowLayout { Class = "UIItemPurchaseWnd", WindowId = 0xff, Width = 280, Height = 152 };
            foreach (RoClassicChildLayout child in cartLayout.Children) {
                if (child.Class == "UIScrollBar" || child.Class == "UIEditCtrl") child.Visible = false;
                if (child.Class != "UIStaticText" || child.Y <= 17) continue;
                // the total label (+0xd0): bound like the vending purchase list's (RoClassicVending)
                child.Fields["text"] = "Total";
                child.W = Math.Max(child.W, 160);
                child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
            }

            if (asset == null) {
                cartLayout.Children.Add(BitmapButton(BuyId, "buy", "btn_buy", 174, 128));
                cartLayout.Children.Add(BitmapButton(CancelId, "cancel", "btn_cancel", 219, 128));
            }

            marketCart = new RoClassicWindow(system, cartLayout) { Visible = false, DragSourceAt = MarketCartDragSource };
            marketCart.DrawDynamic += DrawMarketCart;
            marketCart.Pressed += (w, local) => SelectMarketRow(false, local);
            marketCart.RightClicked += (w, local) => DescribeMarketRow(false, local);
            marketCart.Wheeled += (w, local, delta) => ScrollMarket(false, delta);
            marketCart.Dropped += OnMarketCartDropped;
            marketCart.Clicked += child => {
                if (child.Id == BuyId) BuyMarket();
                else if (child.Id == CancelId) CancelMarket();
            };
            marketCart.EscClose = () => {
                CancelMarket();
                return true;
            };
            SavePosition(marketCart, "buy");
            Resizer(marketCart, 3);
            marketCart.ResizeRequested += (w, mode, width, height) => {
                if (mode != 3) return;
                int newH = ShopHeight(height);
                if (newH == marketCart.Height) return;
                ApplyMarketCartSize(newH);
                PlayerPrefs.SetInt(ShopPrefs + "buy.w", marketCart.Width);
                PlayerPrefs.SetInt(ShopPrefs + "buy.h", newH);
            };
        }

        /// <summary>
        /// UIParaItemShopWnd's frame (OnDraw 0x90a340) at (w, h): TitleBar(0, "") (its title is the "Shop Items" static text),
        /// BtnBar — +0x98 picks style 2 or 0, taken as 2 (the 29 px bar of the plain list; inferred) — white under the title,
        /// itemwin_left / itemwin_right down the sides and the icon cells at x 30.
        /// </summary>
        private void ApplyMarketListSize(int w, int h) {
            w = ShopWidth(w);
            h = ShopHeight(h);
            marketList.SetWidth(w);
            marketList.SetHeight(h);
            var ops = new List<RoClassicOp>();
            int body = h - 29 - 17, n = (body + 7) / 8;
            RoClassicFrames.TitleBar(ops, w, false);
            RoClassicFrames.ButtonBar(ops, w, h, 2);
            RoClassicFrames.Fill(ops, 0, 17, w, body, White);
            for (int k = 0; k < n; k++) RoClassicFrames.Bitmap(ops, Basic + "itemwin_left.bmp", 0, 17 + 8 * k);
            for (int k = 0; k < n; k++) RoClassicFrames.Bitmap(ops, Basic + "itemwin_right.bmp", w - 20, 17 + 8 * k);
            for (int r = 0; r < body / 32; r++) RoClassicFrames.Bitmap(ops, Basic + "itemwin_mid.bmp", 30, 17 + 32 * r);
            SetOps(marketList, ops);
            marketListFirst = Mathf.Clamp(marketListFirst, 0, Math.Max(0, marketShop.Count - MarketListRows));
        }

        /// <summary>
        /// UIItemPurchaseWnd at height h (msg 0xe v1 3, the width stays): the frame of 0x8dfd20, the resizer, "Total" at
        /// (12, H − 20), Buy / Cancel at (W − 106 / W − 61, H − 24).
        /// </summary>
        private void ApplyMarketCartSize(int h) {
            int w = marketCart.Width;
            h = ShopHeight(h);
            marketCart.SetHeight(h);
            var ops = new List<RoClassicOp>();
            RoClassicFrames.TitleBar(ops, w, false);
            RoClassicFrames.ItemColumns(ops, w, h - 38);
            RoClassicFrames.ButtonBar(ops, w, h, 2);
            RoClassicFrames.Fill(ops, 40, 17, w - 50, (h - 38) / 32 * 32, White);
            RoClassicFrames.IconColumn(ops, 40, (h - 58) / 32);
            SetOps(marketCart, ops);
            foreach (RoClassicChildLayout child in marketCart.Layout.Children) {
                if (child.Class == "UIResizer") marketCart.SetChildPosition(child.Path, w - 15, h - 15);
                else if (child.Class == "UIStaticText" && child.Y > 17) marketCart.SetChildPosition(child.Path, 12, h - 20);
                else if (child.Class == "UIBitmapButton") marketCart.SetChildPosition(child.Path, child.Id == BuyId ? w - 106 : w - 61, h - 24);
            }

            marketCartFirst = Mathf.Clamp(marketCartFirst, 0, Math.Max(0, marketCartLines.Count - MarketCartRows));
        }

        private void HideMarket() {
            marketShop.Clear();
            marketCartLines.Clear();
            marketCartHidden = false;
            if (!marketShown) return;
            marketShown = false;
            marketList.Visible = false;
            marketCart.Visible = false;
            system.Windows.RemoveEsc(marketList);
            system.Windows.RemoveEsc(marketCart);
            RoClassicCountWindow.Hide();
        }

        private void HideMarketResult() {
            marketResultLines.Clear();
            if (!marketResultShown) return;
            marketResultShown = false;
            marketResult.Visible = false;
            system.Windows.RemoveEsc(marketResult);
        }

        /// <summary>
        /// Cancel 0xb5: Msg(0x124) = CZ_NPC_MARKET_CLOSE 0x09d8, Msg(0x28) (0xfe and 0xff deleted; the client's DeleteWindow(0xff)
        /// sends a second 0x09d8, one is sent here) and the shop lists cleared.
        /// </summary>
        private void CancelMarket() {
            if (!marketShown) return;
            HideMarket();
            session?.ClearNpcCommerceLocally(RoNpcShopMode.Market);
            if (session != null) SendSafely(session.BeginNpcMarketClose, "market close");
        }

        // ================================================================ drawing

        private static string MarketPrice(long price) => " " + RoClassicItemText.Commas(price) + " Z";

        /// <summary>
        /// OnDraw 0x90a340: the "Shop Items" title text at (16, 2), per row (y = 17 + 32r) the selection, the icon at
        /// (34, y + 4) with the stock (grey at 0), the name at (70, y + 12) 120 wide and " %s Z" right-aligned at W − 18.
        /// </summary>
        private void DrawMarketList(RoClassicWindow w) {
            string title = Msg(0xba, "Shop Items");
            w.TextOut(17, 3, title, 0, 12, White);
            w.TextOut(16, 2, title, 0, 12, Black);
            int rows = MarketListRows;
            for (int r = 0; r < rows; r++) {
                int k = marketListFirst + r;
                if (k >= marketShop.Count) break;
                MarketEntry e = marketShop[k];
                int y = 17 + 32 * r;
                if (k == marketListSel) w.FillRect(72, y + 2, 191, 28, Selected);
                RoClassicItemText.DrawIcon(w, 34, y + 4, e.Item, metadata, e.Amount, e.Amount == 0);
                RoClassicItemText.DrawName(w, 70, y + 12, 120, e.Item, metadata);
                w.TextOutRight(w.Width - 18, y + 12, MarketPrice(e.Price), 0, 12, Black);
            }

            w.DrawScrollBar(w.Width - 15, 14, w.Height - 46, Math.Max(0, marketShop.Count - rows), marketShop.Count, rows, marketListFirst);
            DrawResizer(w);
        }

        /// <summary>UIItemPurchaseWnd 0x8dfd20: per cart line the icon with its count, the name 110 wide, " %s Z" at W − 18.</summary>
        private void DrawMarketCart(RoClassicWindow w) {
            int rows = MarketCartRows;
            for (int r = 0; r < rows; r++) {
                int k = marketCartFirst + r;
                if (k >= marketCartLines.Count) break;
                MarketEntry e = marketCartLines[k];
                if (k == marketCartSel) w.FillRect(72, 19 + 32 * r, 191, 28, Selected);
                RoClassicItemText.DrawIcon(w, 44, 21 + 32 * r, e.Item, metadata, e.Amount);
                RoClassicItemText.DrawName(w, 76, 29 + 32 * r, 110, e.Item, metadata);
                w.TextOutRight(w.Width - 18, 29 + 32 * r, MarketPrice(e.Price), 0, 12, Black);
            }

            w.DrawScrollBar(w.Width - 15, 14, w.Height - 58, Math.Max(0, marketCartLines.Count - rows), marketCartLines.Count, rows, marketCartFirst);
        }

        private long MarketCartZeny() {
            long total = 0;
            foreach (MarketEntry c in marketCartLines) total += c.Price * c.Amount;
            return total;
        }

        /// <summary>msg 0x17: the label "Total : %s Zeny" (Commas Σ count × price) and the cart redrawn.</summary>
        private void UpdateMarketTotal() {
            if (marketCart == null) return;
            marketCart.Set("Total", "Total : " + RoClassicItemText.Commas(MarketCartZeny()) + " Zeny");
            marketCart.Invalidate();
        }

        // ================================================================ mouse

        /// <summary>The inherited row hit test 0x8d4e00: x ≥ 40, 17 ≤ y &lt; rows·32 + 17.</summary>
        private int MarketRowAt(bool list, Vector2Int local) {
            int rows = list ? MarketListRows : MarketCartRows;
            if (local.x < 40 || local.y < 17 || local.y >= rows * 32 + 17) return -1;
            int k = (list ? marketListFirst : marketCartFirst) + (local.y - 17) / 32;
            return k < (list ? marketShop : marketCartLines).Count ? k : -1;
        }

        private void SelectMarketRow(bool list, Vector2Int local) {
            int k = MarketRowAt(list, local);
            if (k < 0) return;
            if (list) marketListSel = k;
            else marketCartSel = k;
            (list ? marketList : marketCart).Invalidate();
        }

        private void DescribeMarketRow(bool list, Vector2Int local) {
            int k = MarketRowAt(list, local);
            if (k < 0) return;
            SelectMarketRow(list, local);
            RoClassicItemDescription.Toggle((list ? marketShop : marketCartLines)[k].Item, metadata);
        }

        private void ScrollMarket(bool list, int delta) {
            int rows = list ? MarketListRows : MarketCartRows;
            int count = (list ? marketShop : marketCartLines).Count;
            int step = delta > 0 ? -Math.Min(rows - 1, 3) : Math.Min(rows - 1, 3);
            int first = Mathf.Clamp((list ? marketListFirst : marketCartFirst) + step, 0, Math.Max(0, count - rows));
            if (list) marketListFirst = first;
            else marketCartFirst = first;
            (list ? marketList : marketCart).Invalidate();
        }

        /// <summary>A list row: DragStart type 3, amount 1 (the entry's index is −1).</summary>
        private IRoClassicDragPayload MarketListDragSource(Vector2Int local) {
            int k = MarketRowAt(true, local);
            if (k < 0) return null;
            MarketEntry e = marketShop[k];
            return new RoClassicNpcMarketPayload { Type = DragMarketListToCart, Entry = e, Amount = 1, IconPath = RoClassicItemText.IconPath(e.Item, metadata) };
        }

        /// <summary>A cart line: DragStart type 4 with its count.</summary>
        private IRoClassicDragPayload MarketCartDragSource(Vector2Int local) {
            int k = MarketRowAt(false, local);
            if (k < 0) return null;
            MarketEntry e = marketCartLines[k];
            return new RoClassicNpcMarketPayload { Type = DragCartToList, Entry = e, Amount = e.Amount, IconPath = RoClassicItemText.IconPath(e.Item, metadata) };
        }

        /// <summary>
        /// Drop on 0xff (0x909d10): equipment at once, else the amount window (mode 0x15: 30000 and more → 30000 with
        /// MsgStr 0x6a8).
        /// </summary>
        private void OnMarketCartDropped(RoClassicWindow w, Vector2Int local, IRoClassicDragPayload payload) {
            if (!(payload is RoClassicNpcMarketPayload carried) || carried.Type != DragMarketListToCart || !(carried.Entry is MarketEntry e)) return;
            if (e.Equipment) {
                AddToMarketCart(e, 1);
                return;
            }

            RoClassicCountWindow.Show(e.Item.GetFullDisplayName(metadata), carried.Amount, n => {
                if (n < 1) return;
                if (n >= MaxAmount) {
                    n = MaxAmount;
                    Say(Format(Msg(0x6a8, "It is not possible to purchase the same item more than %d pieces at a time"), MaxAmount));
                }

                AddToMarketCart(e, (int) n);
            });
        }

        /// <summary>
        /// 0xff msg 0x26 (0x90b5b0): one equipment per item (0x77); whether the zeny suffice is decided before the clamps;
        /// 30000 per item (0x6a8), the stock (0x9e3) clamp the amount; then the zeny (0x37) and the weight (0x9e8); the cart
        /// takes it (0xce7100) and the stock goes down (0xce4450).
        /// </summary>
        private void AddToMarketCart(MarketEntry e, int n) {
            if (!marketShop.Contains(e)) return;
            MarketEntry line = marketCartLines.Find(c => c.ItemId == e.ItemId);
            if (e.Equipment && line != null && line.Amount > 0) {
                Say(Msg(0x77, "Please avoid buying 2 of the same items at one time."));
                return;
            }

            RoPlayerStatusSnapshot status = Status();
            bool enough = e.Price * n + MarketCartZeny() <= status.Zeny;
            int have = line?.Amount ?? 0;
            if (have + n > MaxAmount) {
                n = MaxAmount - have;
                Say(Format(Msg(0x6a8, "It is not possible to purchase the same item more than %d pieces at a time"), MaxAmount));
            }

            if (e.Amount != -1 && e.Amount < n) {
                n = e.Amount;
                Say(Msg(0x9e3, "You do not have enough items."));
            }

            if (n <= 0) return;
            if (!enough) {
                Say(Msg(0x37, "You do not have enough zeny."));
                return;
            }

            long cartWeight = 0;
            foreach (MarketEntry c in marketCartLines) cartWeight += (long) c.Weight * c.Amount;
            if ((long) e.Weight * n + cartWeight + status.Weight > status.MaxWeight) {
                Say(Msg(0x9e8, "Cannot purchase item, You exceeded the weight limit."));
                return;
            }

            if (line != null) line.Amount += n;
            else marketCartLines.Add(e.Copy(n));
            if (e.Amount != -1) e.Amount -= n;
            UpdateMarketTotal();
            marketList.Invalidate();
        }

        /// <summary>Drop on 0xfe (0x909e00): a cart line of one piece comes back at once, else the amount window (mode 0x16, at most its count).</summary>
        private void OnMarketListDropped(RoClassicWindow w, Vector2Int local, IRoClassicDragPayload payload) {
            if (!(payload is RoClassicNpcMarketPayload carried) || carried.Type != DragCartToList || !(carried.Entry is MarketEntry e)) return;
            if (carried.Amount == 1) {
                ReturnFromMarketCart(e, 1);
                return;
            }

            RoClassicCountWindow.Show(e.Item.GetFullDisplayName(metadata), carried.Amount, n => {
                if (n >= 1) ReturnFromMarketCart(e, (int) Math.Min(n, e.Amount));
            });
        }

        /// <summary>0xfe msg 0x26: the cart line shrinks (0xce71f0) and the list gets its stock back (0xce43b0).</summary>
        private void ReturnFromMarketCart(MarketEntry line, int n) {
            if (!marketCartLines.Contains(line)) return;
            n = Math.Min(n, line.Amount);
            if (n <= 0) return;
            line.Amount -= n;
            if (line.Amount <= 0) {
                marketCartLines.Remove(line);
                marketCartSel = -1;
                marketCartFirst = Mathf.Clamp(marketCartFirst, 0, Math.Max(0, marketCartLines.Count - MarketCartRows));
            }

            MarketEntry row = marketShop.Find(s => s.ItemId == line.ItemId);
            if (row != null && row.Amount != -1) row.Amount += n;
            UpdateMarketTotal();
            marketList.Invalidate();
        }

        // ================================================================ buy / result

        /// <summary>
        /// Buy 0xb4 (Msg 0x106): with a cart, CZ_NPC_MARKET_PURCHASE 0x09d6 {id, count} per line, 0xff hidden (not deleted) and
        /// the description window closed; without, DeleteWindow(0xff) → 0x09d8. Both: the shop lists cleared, 0xfe deleted.
        /// </summary>
        private void BuyMarket() {
            if (!marketShown) return;
            if (marketCartLines.Count == 0) {
                CancelMarket();
                return;
            }

            var requests = new RoNpcMarketPurchaseRequest[marketCartLines.Count];
            for (int i = 0; i < requests.Length; i++) requests[i] = new RoNpcMarketPurchaseRequest(marketCartLines[i].ItemId, marketCartLines[i].Amount);
            if (session != null) SendSafely(() => session.BeginNpcMarketPurchase(requests), "market purchase");
            RoClassicItemDescription.Close();
            HideMarket();
            marketCartHidden = true;   // HideMarket left it false: 0xff stays (hidden) until the result
            session?.ClearNpcCommerceLocally(RoNpcShopMode.Market);
        }

        /// <summary>
        /// ZC_NPC_MARKET_PURCHASE_RESULT (0xc5b770): the result line (0 MsgStr 0x36 in #00ffff, 1 0x37, 2 0x38, 3 0xdd,
        /// 9 0x794, 0xffff 0xbe3 in red); no items → 0x09d8 (the hidden cart stays hidden); items → UIParaResultWnd 0x100
        /// with them, max(n − 2, 0)·32 + 120 high.
        /// </summary>
        private void OnMarketResult(ushort result, RoNpcMarketResultItem[] items) {
            switch (result) {
                case 0: Say(Msg(0x36, "The deal has successfully completed."), true); break;
                case 1: Say(Msg(0x37, "You do not have enough zeny.")); break;
                case 2: Say(Msg(0x38, "You are over your Weight Limit.")); break;
                case 3: Say(Msg(0xdd, "Out of the maximum capacity")); break;
                case 9: Say(Msg(0x794, "Amounts are exceeded the possession of the item is not available for purchase.")); break;
                case 0xffff: Say(Msg(0xbe3, "Purchase failed")); break;
            }

            items ??= Array.Empty<RoNpcMarketResultItem>();
            if (items.Length == 0) {
                if (session != null) SendSafely(session.BeginNpcMarketClose, "market close");
                return;
            }

            marketResultLines.Clear();
            foreach (RoNpcMarketResultItem item in items) {
                marketResultLines.Add(new MarketEntry {
                    Item = ShopItem(item.ItemId, 0, item.Amount), ItemId = item.ItemId, Price = item.Price, Amount = item.Amount,
                });
            }

            EnsureMarketResultWindow();
            marketResultFirst = 0;
            ApplyMarketResultSize(Math.Max(items.Length - 2, 0) * 32 + 120);
            marketResultShown = true;
            marketResult.Visible = true;
            system.Windows.BringToFront(marketResult);
            system.Windows.PushEsc(marketResult);
            marketResult.Invalidate();
        }

        private void EnsureMarketResultWindow() {
            if (marketResult != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIParaResultWnd", WindowId = 0x100, Width = 280, Height = 120 };
            layout.Children.Add(BitmapButton(OkId, "ok", "btn_ok", 280 - 61, 120 - 24));
            marketResult = new RoClassicWindow(system, layout) { Visible = false };
            marketResult.DrawDynamic += DrawMarketResult;
            marketResult.Wheeled += (w, local, delta) => {
                int rows = MarketResultRows, step = delta > 0 ? -Math.Min(rows - 1, 3) : Math.Min(rows - 1, 3);
                marketResultFirst = Mathf.Clamp(marketResultFirst + step, 0, Math.Max(0, marketResultLines.Count - rows));
                marketResult.Invalidate();
            };
            marketResult.RightClicked += (w, local) => {
                int rows = MarketResultRows;
                if (local.x < 40 || local.y < 17 || local.y >= rows * 32 + 17) return;
                int k = marketResultFirst + (local.y - 17) / 32;
                if (k < marketResultLines.Count) RoClassicItemDescription.Toggle(marketResultLines[k].Item, metadata);
            };
            marketResult.Clicked += child => {
                if (child.Id == OkId) CloseMarketResult();
            };
            // Esc deletes 0x100 in the client without a packet; here it is OK, so the server's market ends too
            marketResult.EscClose = () => {
                CloseMarketResult();
                return true;
            };
            Resizer(marketResult, 7);
            marketResult.ResizeRequested += (w, mode, width, height) => {
                if (mode == 7) ApplyMarketResultSize(height);
            };
        }

        /// <summary>
        /// msg 0x24(h) → msg 0xe(7, W, h): the plain list's size rules, then centred (Move(scrW/2 − W/2, scrH/2 − H/2)). The
        /// frame is taken as the plain list's (inferred), the label at (12, H − 21), OK at (W − 61, H − 24).
        /// </summary>
        private void ApplyMarketResultSize(int height) {
            int w = 280, h = ShopHeight(height);
            marketResult.SetWidth(w);
            marketResult.SetHeight(h);
            SetOps(marketResult, ShopListOps(w, h));
            marketResult.SetChildPosition("ok", w - 61, h - 24);
            marketResultFirst = Mathf.Clamp(marketResultFirst, 0, Math.Max(0, marketResultLines.Count - MarketResultRows));
            Vector2Int client = system.ClientSize;
            marketResult.MoveTo(client.x / 2 - w / 2, client.y / 2 - h / 2);
        }

        /// <summary>OnDraw 0x90abf0: "Item purchase successful" (MsgStr 0x9e2) at (16, 2), the rows like the plain list, the total.</summary>
        private void DrawMarketResult(RoClassicWindow w) {
            string title = Msg(0x9e2, "Item purchase successful");
            w.TextOut(17, 3, title, 0, 12, White);
            w.TextOut(16, 2, title, 0, 12, Black);
            int rows = MarketResultRows;
            long total = 0;
            foreach (MarketEntry e in marketResultLines) total += e.Price * e.Amount;
            for (int r = 0; r < rows; r++) {
                int k = marketResultFirst + r;
                if (k >= marketResultLines.Count) break;
                MarketEntry e = marketResultLines[k];
                RoClassicItemText.DrawIcon(w, 44, 21 + 32 * r, e.Item, metadata, e.Amount);
                RoClassicItemText.DrawName(w, 76, 29 + 32 * r, 110, e.Item, metadata);
                w.TextOutRight(w.Width - 18, 29 + 32 * r, MarketPrice(e.Price), 0, 12, Black);
            }

            w.DrawScrollBar(w.Width - 15, 14, w.Height - 46, Math.Max(0, marketResultLines.Count - rows), marketResultLines.Count, rows, marketResultFirst);
            w.TextOut(12, w.Height - 21, "Total : " + RoClassicItemText.Commas(total) + " Zeny", 0, 12, Black);
            DrawResizer(w);
        }

        /// <summary>OK: Msg(0x124) = 0x09d8 and Msg(0x28) (0x100 and the hidden 0xff deleted — a second 0x09d8 in the client).</summary>
        private void CloseMarketResult() {
            if (!marketResultShown) return;
            HideMarketResult();
            marketCartHidden = false;
            if (session != null) SendSafely(session.BeginNpcMarketClose, "market close");
        }

        // ================================================================ tooltips

        /// <summary>The inherited row tooltip (0x8e6fb0): "name price Zeny".</summary>
        private void HoverMarket(RoClassicWindow over, Vector2Int local) {
            bool list = over == marketList;
            int k = MarketRowAt(list, local);
            if (k < 0) return;
            MarketEntry e = (list ? marketShop : marketCartLines)[k];
            string text = e.Item.GetFullDisplayName(metadata) + " " + RoClassicItemText.Commas(e.Price) + " Zeny";
            Vector2Int at = over.Position + RoClassicItemText.RowTooltipAnchor(local);
            RoClassicTooltip.Shared.Show(text, RoClassicItemText.TooltipColor(e.Item, metadata), at.x, at.y);
        }

        // ================================================================ diagnostics (!market ...)

        /// <summary>state | select row | add row [n] | remove row [n] | desc row | buy | cancel | esc | ok (the result window).</summary>
        internal static string SweepMarket(string command) {
            RoClassicNpcMarket m = instance;
            if (m == null) return "no classic npc market";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            Vector2Int Row(int k, bool list) => new Vector2Int(60, 17 + 32 * (k - (list ? m.marketListFirst : m.marketCartFirst)) + 16);
            if (m.marketShown) {
                switch (a[0]) {
                    case "select": m.SelectMarketRow(true, Row(Arg(a, 1), true)); break;
                    case "add":
                        if (m.MarketListDragSource(Row(Arg(a, 1), true)) is RoClassicNpcMarketPayload add) {
                            m.OnMarketCartDropped(m.marketCart, Vector2Int.zero, add);
                            AnswerCount(a, 2);
                        }

                        break;
                    case "remove":
                        if (m.MarketCartDragSource(Row(Arg(a, 1), false)) is RoClassicNpcMarketPayload back) {
                            m.OnMarketListDropped(m.marketList, Vector2Int.zero, back);
                            AnswerCount(a, 2);
                        }

                        break;
                    case "desc": m.DescribeMarketRow(true, Row(Arg(a, 1), true)); break;
                    case "buy": m.BuyMarket(); break;
                    case "cancel": m.CancelMarket(); break;
                    case "esc": m.system.Windows.HandleEscape(); break;
                }
            } else if (a[0] == "ok") {
                m.CloseMarketResult();
            } else if (a[0] == "esc") {
                m.system.Windows.HandleEscape();
            }

            var s = new StringBuilder();
            s.Append("shown=").Append(m.marketShown).Append(" cartHidden=").Append(m.marketCartHidden).Append(" result=").Append(m.marketResultShown);
            if (m.marketList != null) s.Append(" list=").Append(m.marketList.Width).Append('x').Append(m.marketList.Height).Append('@').Append(m.marketList.Position);
            if (m.marketCart != null) s.Append(" cart=").Append(m.marketCart.Width).Append('x').Append(m.marketCart.Height).Append('@').Append(m.marketCart.Position);
            s.Append(" shop=[");
            for (int i = 0; i < m.marketShop.Count; i++) {
                MarketEntry e = m.marketShop[i];
                s.Append(i == 0 ? "" : ", ").Append(e.ItemId).Append('x').Append(e.Amount == -1 ? "inf" : Number(e.Amount)).Append('@').Append(e.Price);
            }

            s.Append("] cart=[");
            for (int i = 0; i < m.marketCartLines.Count; i++) s.Append(i == 0 ? "" : ", ").Append(m.marketCartLines[i].ItemId).Append('x').Append(m.marketCartLines[i].Amount);
            s.Append("] total=").Append(m.MarketCartZeny()).Append(" bought=[");
            for (int i = 0; i < m.marketResultLines.Count; i++) s.Append(i == 0 ? "" : ", ").Append(m.marketResultLines[i].ItemId).Append('x').Append(m.marketResultLines[i].Amount);
            s.Append("] count=").Append(RoClassicCountWindow.Visible ? "'" + RoClassicCountWindow.Text + "'" : "hidden");
            RoNpcShopSnapshot shop = m.session?.NpcShopSnapshot() ?? RoNpcShopSnapshot.Empty;
            s.Append(" session: mode=").Append(shop.Mode).Append(" marketItems=").Append(shop.MarketItems.Length).Append(" seq=").Append(shop.MarketSequence)
                .Append(" result=").Append(shop.HasMarketResult ? Number(shop.MarketResult) : "-").Append('#').Append(shop.MarketResultSequence);
            return s.ToString();
        }
    }
}
