using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The NPC zeny shop (ui/RUNTIME-RULES-STORE-SHOP.md §4): the deal choice UIChooseSellBuyWnd (id 0x19, made by
    /// ZC_SELECT_DEALTYPE), the item list UIItemShopWnd (0x16, buy and sell), the buy cart UIItemPurchaseWnd (0x17) and the
    /// sell cart UIItemSellWnd (0x18) (made by ZC_PC_PURCHASE_ITEMLIST / ZC_PC_SELL_ITEMLIST), all from the oracle's
    /// default-size layouts with the rows drawn from the session's shop lists. Items move between list and cart by dragging
    /// (amounts through UIItemDropCntWnd); Buy / Sell send the cart. RoClassicGameUi ticks it: the windows follow the
    /// session's shop when its NPC sequence moves, and the purchase / sell results (ZC_PC_PURCHASE_RESULT 0x00ca, which the
    /// barter shops answer with too, ZC_PC_SELL_RESULT) print their chat line. The requests and the local closes are
    /// <see cref="RoNpcInteractionModel"/>'s; the barter, expanded barter and market shops are RoClassicNpcMarket's.
    /// </summary>
    public sealed class RoClassicNpcShop {
        private const string ShopLayoutRoot = "Ragnarok/Generated/OriginalClient/ui/";
        private const string ShopPrefs = "ro.ui.classic.shop.";
        private const int ShopBuyId = 0xdc;
        private const int ShopSellId = 0xdb;
        private const int ShopCancelId = 0xb5;
        private const int ShopOkId = 0xb4;
        private const int ShopToggleId = 0xd1;
        private const int ShopMaxAmount = 30000;
        private static readonly Color32 ShopSelected = new Color32(0x70, 0x9f, 0xed, 255);   // colorchip(6,2)
        private static readonly Color32 ShopBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 ShopWhite = new Color32(255, 255, 255, 255);

        /// <summary>One row: a buy list entry, an item of the sell list, or a cart line.</summary>
        private sealed class ClassicShopEntry {
            public RoItemInstance Item;
            public uint ItemId;
            public ushort BagIndex;
            public uint Price;      // buy: price, sell: base price
            public uint Final;      // buy: discount price, sell: overcharge price
            public int Amount;      // list (sell): what is left in the bag; cart: the amount
            public bool Equipment;
        }

        /// <summary>A row carried by a classic drag: from the list (kind 3), the buy cart (4) or the sell cart (5).</summary>
        private sealed class ClassicShopPayload : IRoClassicDragPayload {
            public int Kind;
            public ClassicShopEntry Entry;
            public int Amount;
            public string IconPath { get; set; }
        }

        /// <summary>DAT_01272f44, the "Toggle Item Amount." check box: whole stacks move without the amount window.</summary>
        private static bool shopToggleAmount;

        private static RoClassicNpcShop instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            shopToggleAmount = false;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private int seenSequence = int.MinValue;
        private bool seeded;

        private readonly List<ClassicShopEntry> shopList = new List<ClassicShopEntry>();
        private readonly List<ClassicShopEntry> shopCart = new List<ClassicShopEntry>();
        private RoClassicWindow shopChoose;
        private RoClassicWindow shopListWnd;
        private RoClassicWindow shopBuyWnd;
        private RoClassicWindow shopSellWnd;
        private RoNpcShopMode shopMode;
        private string shopSignature = string.Empty;
        private uint shopChooseHiddenFor;
        private int shopListFirst;
        private int shopListSelected = -1;
        private int shopCartFirst;
        private int shopCartSelected = -1;
        private bool shopFailed;
        private bool shopShown;
        private bool shopHadBuyResult;
        private bool shopHadSellResult;
        private RoItemMetadataTable shopItems = RoItemMetadataTable.Empty;

        /// <summary>True while the deal choice or the buy / sell windows are up.</summary>
        public static bool IsOpen => instance != null && instance.shopShown;

        private bool ShopSell => shopMode == RoNpcShopMode.Sell;
        private RoClassicWindow ShopCartWnd => ShopSell ? shopSellWnd : shopBuyWnd;
        private int ShopListRows => (shopListWnd.Height - 29 - 17) / 32;          // (H − e4 − e8 − 17)/32
        private int ShopCartRows => ShopSell ? (shopSellWnd.Height - 38) / 32 : (shopBuyWnd.Height - 58) / 32;

        /// <summary>
        /// Per frame (RoClassicGameUi): when the session's NPC sequence moved, the results print their line and the windows
        /// follow the shop; the row tooltips while they are up.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession, RoClassicChat chatWindow, RoItemMetadataTable itemMetadata) {
            if (loginSession == null) return;
            RoClassicNpcShop s = Ensure();
            if (s == null) return;
            if (!ReferenceEquals(s.session, loginSession)) {
                s.session = loginSession;
                s.seeded = false;
                s.seenSequence = int.MinValue;
            }

            s.chat = chatWindow;
            s.shopItems = itemMetadata ?? RoItemMetadataTable.Empty;
            int sequence = loginSession.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Npc);
            if (sequence != s.seenSequence) {
                s.seenSequence = sequence;
                s.OnShopChanged(loginSession.NpcShopSnapshot() ?? RoNpcShopSnapshot.Empty);
            }

            if (s.shopShown) s.UpdateClassicShop();
        }

        /// <summary>
        /// Leaving the map: every window goes, nothing is sent (the map server forgets the shop with the map). The next tick
        /// takes the session's results as seen.
        /// </summary>
        public static void Close() {
            RoClassicNpcShop s = instance;
            if (s == null) return;
            s.HideClassicShop();
            s.seeded = false;
            s.seenSequence = int.MinValue;
        }

        private static RoClassicNpcShop Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicNpcShop { system = system };
            return instance;
        }

        private void OnShopChanged(RoNpcShopSnapshot shop) {
            if (!seeded) {
                // results from before the gameplay UI came up are not printed
                seeded = true;
                shopHadBuyResult = shop.HasBuyResult;
                shopHadSellResult = shop.HasSellResult;
            } else {
                ReportClassicShopResults(shop);
            }

            RefreshClassicShop(shop);
        }

        /// <summary>
        /// The deal choice while ZC_SELECT_DEALTYPE is pending (hidden once Buy / Sell asked for the list), else the buy / sell
        /// windows while the session holds a buy or sell list; nothing otherwise.
        /// </summary>
        private void RefreshClassicShop(RoNpcShopSnapshot shop) {
            if (shopFailed) {
                HideClassicShop();
                return;
            }

            bool storeOpen = shop.BuyItems.Length > 0 || shop.SellItems.Length > 0;
            bool dealOpen = !storeOpen && shop.IsDealTypePending;
            try {
                bool zenyStore = storeOpen && (shop.Mode == RoNpcShopMode.Buy || shop.Mode == RoNpcShopMode.Sell);
                if (!dealOpen && !zenyStore) {
                    HideClassicShop();
                    return;
                }

                EnsureClassicShop();
                if (dealOpen) {
                    HideClassicShopStore();
                    bool hidden = shopChooseHiddenFor != 0 && shopChooseHiddenFor == shop.NpcId;
                    if (!hidden && !shopChoose.Visible) {
                        Vector2Int client = RoClassicUiSystem.Instance.ClientSize;
                        shopChoose.MoveTo((client.x - 640) / 2 + 185, 300 * client.y / 480);   // Move(scaled 185, scaled 300)
                        shopChoose.Visible = true;
                        RoClassicUiSystem.Instance.Windows.BringToFront(shopChoose);
                        RoClassicUiSystem.Instance.Windows.PushEsc(shopChoose);
                    }

                    shopShown = true;
                    return;
                }

                shopChoose.Visible = false;
                shopChooseHiddenFor = 0;
                string signature = BuildStoreSignature(shop);
                if (signature != shopSignature || shop.Mode != shopMode || !shopListWnd.Visible) OpenClassicShopStore(shop, signature);
                shopShown = true;
            } catch (Exception e) {
                shopFailed = true;
                HideClassicShop();
                Debug.LogException(e);
            }
        }

        /// <summary>The list's identity: a new list (other items or prices) opens the windows afresh with an empty cart.</summary>
        private static string BuildStoreSignature(RoNpcShopSnapshot shop) {
            var signature = new System.Text.StringBuilder();
            signature.Append(shop.Mode).Append('|');
            foreach (RoNpcShopBuyItem item in shop.BuyItems) {
                signature.Append("B:").Append(item.ItemId).Append(':').Append(item.Price).Append(':').Append(item.DiscountPrice).Append(':')
                    .Append(item.ItemTypeRaw).Append(':').Append(item.ViewSprite).Append('|');
            }

            foreach (RoNpcShopSellItem item in shop.SellItems) {
                signature.Append("S:").Append(item.BagIndex).Append(':').Append(item.Price).Append(':').Append(item.OverchargePrice).Append('|');
            }

            return signature.ToString();
        }

        private static RoItemInstance FindInventoryItem(ushort bagIndex, RoItemInstance[] inventoryItems) {
            foreach (RoItemInstance item in inventoryItems) {
                if (item != null && item.BagIndex == bagIndex) return item;
            }

            return null;
        }

        private void EnsureClassicShop() {
            if (shopChoose != null) return;
            shopChoose = ShopWindow(system, "UIChooseSellBuyWnd", null);
            shopChoose.Draggable = false;
            shopChoose.Clicked += OnClassicShopChooseClicked;
            // Esc on the deal choice (0x19, first in the Esc list) = its Cancel; once Buy / Sell hid it, it still heads the
            // list and ignores msg 6 while hidden, so Esc does nothing (RUNTIME-RULES-STORE-EXTRA §4)
            shopChoose.EscHoldsWhileHidden = true;
            shopChoose.EscClose = () => {
                if (shopChoose.Visible) OnClassicShopChooseClicked(new RoClassicChildLayout { Id = ShopCancelId });
                return true;
            };

            shopListWnd = ShopWindow(system, "UIItemShopWnd", "list");
            shopListWnd.DrawDynamic += DrawClassicShopList;
            shopListWnd.Clicked += child => {
                if (child.Id == ShopToggleId) ToggleClassicShopAmount();
            };
            shopListWnd.Pressed += (w, local) => SelectClassicShopRow(true, local);
            shopListWnd.RightClicked += (w, local) => DescribeClassicShopRow(true, local);
            shopListWnd.DoubleClicked += OnClassicShopListDoubleClicked;
            shopListWnd.Wheeled += (w, local, delta) => ScrollClassicShop(true, delta);
            shopListWnd.DragSourceAt = local => ClassicShopDragSource(true, local);
            shopListWnd.Dropped += OnClassicShopListDropped;

            shopBuyWnd = ShopWindow(system, "UIItemPurchaseWnd", "buy");
            shopSellWnd = ShopWindow(system, "UIItemSellWnd", "sell");
            foreach (RoClassicWindow cart in new[] { shopBuyWnd, shopSellWnd }) {
                cart.DrawDynamic += DrawClassicShopCart;
                cart.Clicked += OnClassicShopCartClicked;
                cart.Pressed += (w, local) => SelectClassicShopRow(false, local);
                cart.RightClicked += (w, local) => DescribeClassicShopRow(false, local);
                cart.DoubleClicked += OnClassicShopCartDoubleClicked;
                cart.Wheeled += (w, local, delta) => ScrollClassicShop(false, delta);
                cart.DragSourceAt = local => ClassicShopDragSource(false, local);
                cart.Dropped += OnClassicShopCartDropped;
            }

            ConfigureShopSize(shopListWnd, "UIItemShopWnd", "list", 7);
            ConfigureShopSize(shopBuyWnd, "UIItemPurchaseWnd", "buy", 3);
            ConfigureShopSize(shopSellWnd, "UIItemSellWnd", "sell", 3);
            SetClassicShopCheckBox();
        }

        // ---- sizes: msg 0xe from the resizers, list mode 7 (W 248/280/312), carts mode 3 (RUNTIME-RULES-STORE-SHOP §4b–d) ----

        /// <summary>The frame OnDraw draws for (w, h): list 0x8e1140, purchase 0x8dfd20, sell 0x8e0780.</summary>
        private static void BuildShopOps(string layoutName, int w, int h, List<RoClassicOp> ops) {
            var white = new Color32(255, 255, 255, 255);
            ops.Clear();
            RoClassicFrames.TitleBar(ops, w, false);
            switch (layoutName) {
                case "UIItemShopWnd": {
                    int body = h - 29 - 17, rows = body / 32;
                    RoClassicFrames.ItemColumns(ops, w, body);
                    RoClassicFrames.ButtonBar(ops, w, h, 2);
                    RoClassicFrames.IconColumn(ops, 40, rows);
                    RoClassicFrames.Fill(ops, 72, 17, (w - 40) / 32 * 32 - 32, rows * 32, white);
                    break;
                }
                case "UIItemPurchaseWnd":
                    RoClassicFrames.ItemColumns(ops, w, h - 38);
                    RoClassicFrames.ButtonBar(ops, w, h, 2);
                    RoClassicFrames.Fill(ops, 40, 17, w - 50, (h - 38) / 32 * 32, white);
                    RoClassicFrames.IconColumn(ops, 40, (h - 58) / 32);
                    break;
                case "UIItemSellWnd": {
                    int rows = (h - 38) / 32;
                    RoClassicFrames.ItemColumns(ops, w, h - 38);
                    RoClassicFrames.ButtonBar(ops, w, h, 2);
                    RoClassicFrames.IconColumn(ops, 40, rows);
                    RoClassicFrames.Fill(ops, 72, 17, (w - 40) / 32 * 32 - 32, rows * 32, white);
                    break;
                }
            }
        }

        /// <summary>The window's frame generated (checked against the oracle at its size), its resizer, the saved size.</summary>
        private void ConfigureShopSize(RoClassicWindow window, string layoutName, string prefsName, int mode) {
            var generated = new List<RoClassicOp>();
            BuildShopOps(layoutName, window.Width, window.Height, generated);
            if (!RoClassicFrames.SameOps(generated, window.Layout.Ops, out string difference)) {
                Debug.LogWarning("classic " + layoutName + " frame differs from the oracle: " + difference);
            }

            window.ResizerModeAt = local =>
                local.x >= window.Width - 15 && local.x < window.Width + 1 && local.y >= window.Height - 15 && local.y < window.Height + 1 ? mode : -1;
            window.ResizeRequested += (w, m, width, height) => {
                if (m != mode) return;
                // list: W = 280 + trunc32(clamp(w, 232, 320) − 280); both: H = 120 + trunc32(clamp(h, 120, 336) − 120)
                int newW = mode == 7 ? 280 + (Mathf.Clamp(width, 232, 320) - 280) / 32 * 32 : window.Width;
                int newH = 120 + (Mathf.Clamp(height, 120, 336) - 120) / 32 * 32;
                if (newW == window.Width && newH == window.Height) return;
                ApplyShopSize(window, layoutName, newW, newH);
                PlayerPrefs.SetInt(ShopPrefs + prefsName + ".w", newW);
                PlayerPrefs.SetInt(ShopPrefs + prefsName + ".h", newH);
            };

            int savedW = PlayerPrefs.GetInt(ShopPrefs + prefsName + ".w", window.Width), savedH = PlayerPrefs.GetInt(ShopPrefs + prefsName + ".h", window.Height);
            if (savedW != window.Width || savedH != window.Height) ApplyShopSize(window, layoutName, savedW, savedH);
        }

        /// <summary>
        /// A new size: the frame ops, the resizer (W − 15, H − 15); the list's check box (12, H − 20); the carts' total
        /// (12, H − 20) and buttons (W − 106 / W − 61, H − 24).
        /// </summary>
        private void ApplyShopSize(RoClassicWindow window, string layoutName, int w, int h) {
            window.SetWidth(w);
            window.SetHeight(h);
            var ops = new List<RoClassicOp>();
            BuildShopOps(layoutName, w, h, ops);
            window.Layout.Ops.Clear();
            window.Layout.Ops.AddRange(ops);
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                if (child.Class == "UIResizer") window.SetChildPosition(child.Path, w - 15, h - 15);
                else if (child.Class == "UICheckBox") window.SetChildPosition(child.Path, 12, h - 20);
                else if (child.Class == "UIStaticText" && child.Y > 17) window.SetChildPosition(child.Path, 12, h - 20);
                else if (child.Class == "UIBitmapButton") window.SetChildPosition(child.Path, child.Id == ShopOkId ? w - 106 : w - 61, h - 24);
            }

            shopListFirst = Mathf.Clamp(shopListFirst, 0, Math.Max(0, shopList.Count - ShopListRows));
            shopCartFirst = Mathf.Clamp(shopCartFirst, 0, Math.Max(0, shopCart.Count - ShopCartRows));
            window.Invalidate();
        }

        /// <summary>An oracle layout with its captured scroll bars hidden (drawn from the list instead).</summary>
        private static RoClassicWindow ShopWindow(RoClassicUiSystem system, string layoutName, string prefsName) {
            var asset = Resources.Load<TextAsset>(ShopLayoutRoot + layoutName);
            if (asset == null) throw new InvalidOperationException("missing classic layout " + layoutName);
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UIScrollBar" || child.Class == "UIEditCtrl") child.Visible = false;
            }

            var window = new RoClassicWindow(system, layout) { Visible = false };
            if (prefsName != null) {
                window.Moved += w => {
                    PlayerPrefs.SetInt(ShopPrefs + prefsName + ".x", w.Position.x);
                    PlayerPrefs.SetInt(ShopPrefs + prefsName + ".y", w.Position.y);
                };
            }

            return window;
        }

        private static void ShowShopWindow(RoClassicWindow window, string prefsName, int defaultX, int defaultY) {
            Vector2Int client = RoClassicUiSystem.Instance.ClientSize;
            int x = PlayerPrefs.GetInt(ShopPrefs + prefsName + ".x", defaultX), y = PlayerPrefs.GetInt(ShopPrefs + prefsName + ".y", defaultY);
            window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - window.Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - window.Height)));
            window.Visible = true;
            RoClassicUiSystem.Instance.Windows.BringToFront(window);
            window.Invalidate();
        }

        /// <summary>
        /// ZC_PC_PURCHASE_ITEMLIST / ZC_PC_SELL_ITEMLIST: the list window (msg 0x24 sell, title MsgStr 0xba / 0xb9) and
        /// the matching cart, emptied. The sell list is the inventory items the packet names, with their amounts.
        /// </summary>
        private void OpenClassicShopStore(RoNpcShopSnapshot shop, string signature) {
            shopSignature = signature;
            shopMode = shop.Mode;
            shopList.Clear();
            shopCart.Clear();
            shopListFirst = shopCartFirst = 0;
            shopListSelected = shopCartSelected = -1;
            if (ShopSell) {
                RoItemInstance[] inventory = session != null ? session.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>() : Array.Empty<RoItemInstance>();
                foreach (RoNpcShopSellItem sell in shop.SellItems) {
                    RoItemInstance item = FindInventoryItem(sell.BagIndex, inventory);
                    // Hercules lists worn equipment too (and sells it); the client's sell list is the inventory window's
                    // items, where worn gear is not - only equipped ammo stays there (playtest: the worn Knife was offered)
                    if (item == null || (item.WearState & ~RoItemEquipLocation.Ammo) != RoItemEquipLocation.None) continue;
                    shopList.Add(new ClassicShopEntry {
                        Item = item, ItemId = (uint) item.ItemId, BagIndex = sell.BagIndex, Price = sell.Price, Final = sell.OverchargePrice, Amount = item.Count,
                    });
                }
            } else {
                foreach (RoNpcShopBuyItem buy in shop.BuyItems) {
                    RoItemInstance item = RoItemInstance.CreateRegular(-1, (int) buy.ItemId, 0, true, itemTypeRaw: buy.ItemTypeRaw);
                    RoItemMetadata metadata = item.ResolveMetadata(shopItems);
                    shopList.Add(new ClassicShopEntry {
                        Item = item, ItemId = buy.ItemId, Price = buy.Price, Final = buy.DiscountPrice,
                        Equipment = metadata != null && metadata.IsEquipable && metadata.ItemType != RoItemType.Ammo,
                    });
                }
            }

            SetClassicShopText(shopListWnd, "Item Shop", RoClassicUiHost.MsgStr(ShopSell ? 0xb9 : 0xba, ShopSell ? "Available Items to sell" : "Shop Items"));
            ShowShopWindow(shopListWnd, "list", 0, 120);
            (ShopSell ? shopBuyWnd : shopSellWnd).Visible = false;
            ShowShopWindow(ShopCartWnd, ShopSell ? "sell" : "buy", 280, 280);
            UpdateClassicShopTotal();
        }

        /// <summary>Replaces the text of a static text child (title, total) whose recorded string starts with <paramref name="captured"/>.</summary>
        private static void SetClassicShopText(RoClassicWindow window, string captured, string text) {
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                if (child.Class != "UIStaticText") continue;
                bool match = false;
                foreach (RoClassicOp op in child.Ops) {
                    if (op.Kind == RoClassicOpKind.Text && op.Text != null && (op.Text.StartsWith(captured, StringComparison.Ordinal) || child.Path == (string) child.Props?["shopKey"])) match = true;
                }

                if (!match) continue;
                child.Props ??= new Newtonsoft.Json.Linq.JObject();
                child.Props["shopKey"] = child.Path; // found again once the text changed
                foreach (RoClassicOp op in child.Ops) {
                    if (op.Kind == RoClassicOpKind.Text) op.Text = text;
                }

                child.W = Math.Max(child.W, window.FontFor(0, 12).Measure(text) + 2);
                window.Invalidate();
                return;
            }
        }

        private void SetClassicShopCheckBox() {
            foreach (RoClassicChildLayout child in shopListWnd.Layout.Children) {
                if (child.Id != ShopToggleId) continue;
                foreach (RoClassicOp op in child.Ops) {
                    if (op.Kind == RoClassicOpKind.Bitmap && op.Path != null && op.Path.Contains("checkbox_")) {
                        op.Path = "유저인터페이스\\" + (shopToggleAmount ? "checkbox_1.bmp" : "checkbox_0.bmp");
                    }
                }
            }

            shopListWnd.Invalidate();
        }

        private void ToggleClassicShopAmount() {
            shopToggleAmount = !shopToggleAmount;
            SetClassicShopCheckBox();
        }

        private void HideClassicShopStore() {
            shopCart.Clear();
            shopSignature = string.Empty;
            if (shopListWnd == null) return;
            shopListWnd.Visible = false;
            shopBuyWnd.Visible = false;
            shopSellWnd.Visible = false;
        }

        private void HideClassicShop() {
            shopChooseHiddenFor = 0;
            if (!shopShown) return;
            shopShown = false;
            HideClassicShopStore();
            if (shopChoose != null) {
                shopChoose.Visible = false;
                RoClassicUiSystem.Instance?.Windows.RemoveEsc(shopChoose);
            }

            RoClassicCountWindow.Hide();
        }

        /// <summary>ZC_PC_PURCHASE_RESULT / ZC_PC_SELL_RESULT: the client's chat line for the result (0xc5dbe0 / 0xc5dfe0).</summary>
        private void ReportClassicShopResults(RoNpcShopSnapshot shop) {
            if (shop.HasBuyResult && !shopHadBuyResult) {
                int msg;
                bool good = false;
                switch (shop.BuyResult) {
                    case 0: msg = 0x36; good = true; break;
                    case 1: msg = 0x37; break;
                    case 2: msg = 0x38; break;
                    case 3: msg = 0xdd; break;
                    case 9: msg = 0x794; break;
                    case 10: msg = 0xb65; good = true; break;
                    case 11: msg = 0xde2; break;
                    case 12: msg = 0xde3; good = true; break;
                    case 13: msg = 0xde5; break;
                    case 14: msg = 0xde4; break;
                    default: msg = 0; break;
                }

                if (msg != 0) ShopChat(RoClassicUiHost.MsgStr(msg, "The deal has failed."), good);
            }

            if (shop.HasSellResult && !shopHadSellResult) {
                ShopChat(shop.SellResult == 0
                    ? RoClassicUiHost.MsgStr(0x36, "The deal has successfully completed.")
                    : RoClassicUiHost.MsgStr(0x39, "The deal has failed."), shop.SellResult == 0);
            }

            shopHadBuyResult = shop.HasBuyResult;
            shopHadSellResult = shop.HasSellResult;
        }

        private static void ShopChat(string text, bool good) =>
            RoClassicChat.Instance?.AddLine(text, good ? "#00ffff" : "#ff0000");

        // ---- UIChooseSellBuyWnd ----

        /// <summary>Buy (0xdc) / Sell (0xdb) ask for the list and hide the window; Cancel (0xb5) ends the deal.</summary>
        private void OnClassicShopChooseClicked(RoClassicChildLayout child) {
            uint npc = session?.NpcShopSnapshot()?.NpcId ?? 0;
            switch (child.Id) {
                case ShopBuyId:
                    shopChooseHiddenFor = npc;
                    shopChoose.Visible = false;
                    RoNpcInteractionModel.OpenBuy(session);
                    break;
                case ShopSellId:
                    shopChooseHiddenFor = npc;
                    shopChoose.Visible = false;
                    RoNpcInteractionModel.OpenSell(session);
                    break;
                case ShopCancelId:
                    shopChoose.Visible = false;
                    RoNpcInteractionModel.CancelDeal(session);
                    break;
            }
        }

        // ---- drawing ----

        /// <summary>UIItemShopWnd::OnDraw 0x8e1140: the selected row, icon, name and the price right-aligned at W − 18.</summary>
        private void DrawClassicShopList(RoClassicWindow w) {
            int rows = ShopListRows;
            for (int r = 0; r < rows; r++) {
                int k = shopListFirst + r;
                if (k >= shopList.Count) break;
                ClassicShopEntry e = shopList[k];
                if (k == shopListSelected) w.FillRect(72, 19 + 32 * r, 191, 28, ShopSelected);
                RoClassicItemText.DrawIcon(w, 44, 21 + 32 * r, e.Item, shopItems, ShopSell ? e.Amount : 0);
                RoClassicItemText.DrawName(w, 76, 29 + 32 * r, ShopNameWidth(e, 100), e.Item, shopItems);
                w.TextOutRight(w.Width - 18, 29 + 32 * r, ShopPrice(e, "Z"), 0, 12, ShopBlack);
            }

            w.DrawScrollBar(w.Width - 15, 14, w.Height - 29 - 17, Math.Max(0, shopList.Count - rows), shopList.Count, rows, shopListFirst);
        }

        /// <summary>UIItemPurchaseWnd 0x8dfd20 / UIItemSellWnd 0x8e0780: the cart rows (name width base 110 / 100).</summary>
        private void DrawClassicShopCart(RoClassicWindow w) {
            if (w != ShopCartWnd) return;
            int rows = ShopCartRows;
            for (int r = 0; r < rows; r++) {
                int k = shopCartFirst + r;
                if (k >= shopCart.Count) break;
                ClassicShopEntry e = shopCart[k];
                if (k == shopCartSelected) w.FillRect(72, 19 + 32 * r, 191, 28, ShopSelected);
                RoClassicItemText.DrawIcon(w, 44, 21 + 32 * r, e.Item, shopItems, e.Amount);
                RoClassicItemText.DrawName(w, 76, 29 + 32 * r, ShopNameWidth(e, ShopSell ? 100 : 110), e.Item, shopItems);
                w.TextOutRight(w.Width - 18, 29 + 32 * r, ShopPrice(e, ShopSell ? "Zeny" : "Z"), 0, 12, ShopBlack);
            }

            int barH = ShopSell ? w.Height - 38 : w.Height - 58;
            w.DrawScrollBar(w.Width - 15, 14, barH, Math.Max(0, shopCart.Count - rows), shopCart.Count, rows, shopCartFirst);
        }

        /// <summary>The name gives way to a "price -> final" text: 45 / 55 / 70 / 80 / base px by the price's digits.</summary>
        private static int ShopNameWidth(ClassicShopEntry e, int full) =>
            e.Price == e.Final ? full : e.Price >= 10000 ? 45 : e.Price >= 1000 ? 55 : e.Price >= 100 ? 70 : e.Price >= 10 ? 80 : full;

        private static string ShopPrice(ClassicShopEntry e, string unit) => e.Price == e.Final
            ? " " + RoClassicItemText.Commas(e.Price) + " " + unit
            : " " + e.Price.ToString(CultureInfo.InvariantCulture) + " -> " + RoClassicItemText.Commas(e.Final) + " " + unit;

        private void UpdateClassicShopTotal() {
            long total = 0;
            foreach (ClassicShopEntry e in shopCart) total += (long) e.Amount * e.Final;
            SetClassicShopText(ShopCartWnd, "Total :", "Total : " + RoClassicItemText.Commas(total) + " Zeny");
            ShopCartWnd.Invalidate();
        }

        // ---- mouse ----

        /// <summary>Row hit test 0x8d4e00: x ≥ 40, 17 ≤ y &lt; rows·32 + 17, over the scrolled list.</summary>
        private int ClassicShopRowAt(bool list, Vector2Int local) {
            int rows = list ? ShopListRows : ShopCartRows;
            if (local.x < 40 || local.y < 17 || local.y >= rows * 32 + 17) return -1;
            int k = (list ? shopListFirst : shopCartFirst) + (local.y - 17) / 32;
            return k < (list ? shopList : shopCart).Count ? k : -1;
        }

        private void SelectClassicShopRow(bool list, Vector2Int local) {
            int k = ClassicShopRowAt(list, local);
            if (k < 0) return;
            if (list) shopListSelected = k;
            else shopCartSelected = k;
            (list ? shopListWnd : ShopCartWnd).Invalidate();
        }

        /// <summary>RBtnDown: select and describe (window 12).</summary>
        private void DescribeClassicShopRow(bool list, Vector2Int local) {
            int k = ClassicShopRowAt(list, local);
            if (k < 0) return;
            SelectClassicShopRow(list, local);
            RoClassicItemDescription.Toggle((list ? shopList : shopCart)[k].Item, shopItems);
        }

        /// <summary>LBtnDown on a row: DragStart type 3 (list; amount = the stack when it comes from the bag, else 1) or 4 / 5 (cart).</summary>
        private IRoClassicDragPayload ClassicShopDragSource(bool list, Vector2Int local) {
            int k = ClassicShopRowAt(list, local);
            if (k < 0) return null;
            ClassicShopEntry e = (list ? shopList : shopCart)[k];
            int kind = list ? 3 : ShopSell ? 5 : 4;
            int amount = list ? (ShopSell ? e.Amount : 1) : e.Amount;
            return new ClassicShopPayload { Kind = kind, Entry = e, Amount = Math.Max(1, amount), IconPath = RoClassicItemText.IconPath(e.Item, shopItems) };
        }

        private void ScrollClassicShop(bool list, int delta) {
            int rows = list ? ShopListRows : ShopCartRows;
            int count = (list ? shopList : shopCart).Count;
            int step = delta > 0 ? -Math.Min(rows - 1, 3) : Math.Min(rows - 1, 3);
            int first = Mathf.Clamp((list ? shopListFirst : shopCartFirst) + step, 0, Math.Max(0, count - rows));
            if (list) shopListFirst = first;
            else shopCartFirst = first;
            (list ? shopListWnd : ShopCartWnd).Invalidate();
        }

        /// <summary>DblClk on the sell list with the toggle on: the whole stack goes to the sell cart.</summary>
        private void OnClassicShopListDoubleClicked(RoClassicWindow w, Vector2Int local) {
            if (!ShopSell || !shopToggleAmount) return;
            int k = ClassicShopRowAt(true, local);
            if (k >= 0) MoveToSellCart(shopList[k], shopList[k].Amount);
        }

        /// <summary>DblClk on the sell cart with the toggle on: back to the list.</summary>
        private void OnClassicShopCartDoubleClicked(RoClassicWindow w, Vector2Int local) {
            if (!ShopSell || !shopToggleAmount) return;
            int k = ClassicShopRowAt(false, local);
            if (k >= 0) RemoveFromCart(shopCart[k], shopCart[k].Amount);
        }

        /// <summary>
        /// Drop on the list (vf10): a cart line comes back — one piece (or the toggle in sell mode) at once, else through
        /// the amount window (modes 3 / 4).
        /// </summary>
        private void OnClassicShopListDropped(RoClassicWindow w, Vector2Int local, IRoClassicDragPayload payload) {
            if (!(payload is ClassicShopPayload carried) || carried.Kind == 3) return;
            if (carried.Amount == 1 || (ShopSell && shopToggleAmount)) {
                RemoveFromCart(carried.Entry, carried.Amount);
                return;
            }

            RoClassicCountWindow.Show(carried.Entry.Item.GetFullDisplayName(shopItems), carried.Amount, n => {
                if (n >= 1) RemoveFromCart(carried.Entry, (int) Math.Min(n, carried.Entry.Amount));
            });
        }

        /// <summary>
        /// Drop on a cart (vf10, type 3 from the list). Buy: equipment one piece at once, else the amount window (mode 1,
        /// at most 30000). Sell: one piece or the toggle at once, else the amount window (mode 2, at most the stack).
        /// </summary>
        private void OnClassicShopCartDropped(RoClassicWindow w, Vector2Int local, IRoClassicDragPayload payload) {
            if (!(payload is ClassicShopPayload carried) || carried.Kind != 3) return;
            ClassicShopEntry e = carried.Entry;
            string name = e.Item.GetFullDisplayName(shopItems);
            if (ShopSell) {
                if (carried.Amount == 1 || shopToggleAmount) {
                    MoveToSellCart(e, carried.Amount);
                    return;
                }

                RoClassicCountWindow.Show(name, carried.Amount, n => {
                    n = Math.Min(n, e.Amount);
                    if (n >= 1) MoveToSellCart(e, (int) n);
                });
                return;
            }

            if (e.Equipment) {
                AddToBuyCart(e, 1);
                return;
            }

            RoClassicCountWindow.Show(name, 1, n => {
                if (n < 1) return;
                if (n >= ShopMaxAmount) {
                    n = ShopMaxAmount;
                    ShopChat(ShopMaxAmountText(), false);
                }

                AddToBuyCart(e, (int) n);
            });
        }

        private static string ShopMaxAmountText() =>
            RoClassicUiHost.MsgStr(0x6a8, "It is not possible to purchase the same item more than %d pieces at a time")
                .Replace("%d", ShopMaxAmount.ToString(CultureInfo.InvariantCulture));

        /// <summary>UIItemPurchaseWnd msg 0x26: one equipment per item, 30000 per line, and only what the zeny pay for.</summary>
        private void AddToBuyCart(ClassicShopEntry e, int n) {
            ClassicShopEntry existing = shopCart.Find(c => c.ItemId == e.ItemId);
            if (e.Equipment && existing != null) {
                ShopChat(RoClassicUiHost.MsgStr(0x77, "Please avoid buying 2 of the same items at one time."), false);
                return;
            }

            long total = 0;
            foreach (ClassicShopEntry c in shopCart) total += (long) c.Amount * c.Final;
            long zeny = session != null ? session.InventorySnapshot().Status.Zeny : 0;
            if (existing != null && existing.Amount + n > ShopMaxAmount) {
                n = ShopMaxAmount - existing.Amount;
                ShopChat(ShopMaxAmountText(), false);
            }

            if (n <= 0) return;
            if (zeny < total + (long) n * e.Final) {
                ShopChat(RoClassicUiHost.MsgStr(0x37, "You do not have enough zeny."), false);
                return;
            }

            if (existing != null) existing.Amount += n;
            else shopCart.Add(new ClassicShopEntry { Item = e.Item, ItemId = e.ItemId, Price = e.Price, Final = e.Final, Amount = n, Equipment = e.Equipment });
            UpdateClassicShopTotal();
        }

        /// <summary>UIItemSellWnd msg 0x26: n of a list stack moves to the sell cart (the list keeps what is left).</summary>
        private void MoveToSellCart(ClassicShopEntry e, int n) {
            n = Math.Min(n, e.Amount);
            if (n <= 0 || !shopList.Contains(e)) return;
            ClassicShopEntry existing = shopCart.Find(c => c.BagIndex == e.BagIndex);
            if (existing != null) existing.Amount += n;
            else shopCart.Add(new ClassicShopEntry { Item = e.Item, ItemId = e.ItemId, BagIndex = e.BagIndex, Price = e.Price, Final = e.Final, Amount = n });
            e.Amount -= n;
            if (e.Amount <= 0) {
                shopList.Remove(e);
                shopListSelected = -1;
            }

            shopListWnd.Invalidate();
            UpdateClassicShopTotal();
        }

        /// <summary>msg 0x26 on the list: n of a cart line goes away (sell: back to the list).</summary>
        private void RemoveFromCart(ClassicShopEntry line, int n) {
            n = Math.Min(n, line.Amount);
            if (n <= 0 || !shopCart.Contains(line)) return;
            line.Amount -= n;
            if (line.Amount <= 0) {
                shopCart.Remove(line);
                shopCartSelected = -1;
            }

            if (ShopSell) {
                ClassicShopEntry back = shopList.Find(l => l.BagIndex == line.BagIndex);
                if (back != null) back.Amount += n;
                else shopList.Add(new ClassicShopEntry { Item = line.Item, ItemId = line.ItemId, BagIndex = line.BagIndex, Price = line.Price, Final = line.Final, Amount = n });
                shopListWnd.Invalidate();
            }

            shopCartFirst = Mathf.Clamp(shopCartFirst, 0, Math.Max(0, shopCart.Count - ShopCartRows));
            UpdateClassicShopTotal();
        }

        /// <summary>
        /// Buy / Sell (0xb4): the cart goes to the server (CZ_PC_PURCHASE_ITEMLIST / CZ_PC_SELL_ITEMLIST) and every deal
        /// window closes; Cancel (0xb5, or Buy / Sell with an empty cart) closes them with CZ_NPC_TRADE_QUIT. The session's
        /// list goes either way.
        /// </summary>
        private void OnClassicShopCartClicked(RoClassicChildLayout child) {
            RoNpcShopMode mode = shopMode;
            if (child.Id == ShopOkId && shopCart.Count > 0 && session != null) {
                try {
                    if (ShopSell) {
                        var sell = new List<RoNpcShopSellRequest>();
                        foreach (ClassicShopEntry e in shopCart) sell.Add(new RoNpcShopSellRequest(e.BagIndex, (ushort) Mathf.Clamp(e.Amount, 1, ushort.MaxValue)));
                        session.BeginNpcShopSell(sell.ToArray());
                    } else {
                        var buy = new List<RoNpcShopPurchaseRequest>();
                        foreach (ClassicShopEntry e in shopCart) buy.Add(new RoNpcShopPurchaseRequest((ushort) Mathf.Clamp(e.Amount, 1, ushort.MaxValue), e.ItemId));
                        session.BeginNpcShopBuy(buy.ToArray());
                    }
                } catch (Exception ex) {
                    Debug.LogWarning($"NPC shop cart was not sent: {ex.Message}");
                }

                HideClassicShop();
                RoNpcInteractionModel.CloseStore(session, mode, false);
                return;
            }

            if (child.Id == ShopOkId || child.Id == ShopCancelId) {
                HideClassicShop();
                RoNpcInteractionModel.CloseStore(session, mode, true);
            }
        }

        // ---- per frame: tooltips (Esc: the classic Esc list, the deal choice holding its front, see EnsureClassicShop) ----

        /// <summary>
        /// Row tooltips (MsgStr-less formats of 0x8e6fb0): buy "name price Zeny" / "name base -> final Zeny"; sell
        /// "name amount ea price Zeny".
        /// </summary>
        private void UpdateClassicShop() {
            if (!shopShown || shopListWnd == null) return;
            RoClassicWindow over = system.Windows.HoverWindow;
            bool list = over == shopListWnd;
            if ((!list && over != ShopCartWnd) || system.Windows.Dragging != null || !over.Visible) return;
            Vector2Int local = system.Windows.Mouse - over.Position;
            int k = ClassicShopRowAt(list, local);
            if (k < 0) return;
            ClassicShopEntry e = (list ? shopList : shopCart)[k];
            string name = e.Item.GetFullDisplayName(shopItems);
            string text = ShopSell
                ? e.Price == e.Final
                    ? $"{name} {e.Amount} {RoClassicUiHost.MsgStr(0x24c, "ea")} {RoClassicItemText.Commas(e.Final)} Zeny"
                    : $"{name} {e.Amount} {RoClassicUiHost.MsgStr(0x24c, "ea")} {e.Price} -> {RoClassicItemText.Commas(e.Final)} Zeny"
                : e.Price == e.Final
                    ? $"{name} {RoClassicItemText.Commas(e.Final)} Zeny"
                    : $"{name} {e.Price} -> {RoClassicItemText.Commas(e.Final)} Zeny";
            Vector2Int at = over.Position + RoClassicItemText.RowTooltipAnchor(local);
            RoClassicTooltip.Shared.Show(text, RoClassicItemText.TooltipColor(e.Item, shopItems), at.x, at.y);
        }

        // ---- diagnostics console (RoSkillSweepRunner "!shop ...") ----

        /// <summary>
        /// buy | sell | cancel (deal choice) | add row [n] (list row to the cart, n through the amount window) | remove row |
        /// ok | close | toggle | desc row | talk x y | state.
        /// </summary>
        public static string Sweep(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            if (a[0] == "talk") return Talk(instance?.session ?? RoClassicEntryQueue.CurrentSession, a);
            RoClassicNpcShop s = Ensure();
            if (s == null) return "no classic UI";
            s.EnsureClassicShop();
            return s.SweepClassicShop(a);
        }

        /// <summary>talk x y: CZ_CONTACTNPC to the NPC nearest to cell (x, y).</summary>
        private static string Talk(RoLoginShellSession loginSession, string[] a) {
            int Arg(int i) => i < a.Length ? int.Parse(a[i], CultureInfo.InvariantCulture) : 0;
            var actors = new List<RoOnlineActorSnapshot>();
            UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>()?.SnapshotActors(actors);
            uint best = 0;
            int bestD = int.MaxValue;
            foreach (RoOnlineActorSnapshot actor in actors) {
                if (actor.ObjectType != RoMapActorObjectType.Npc) continue;
                int d = Math.Abs(actor.CellX - Arg(1)) + Math.Abs(actor.CellY - Arg(2));
                if (d < bestD) {
                    bestD = d;
                    best = actor.ServerId;
                }
            }

            if (best != 0) loginSession?.BeginContactNpc(unchecked((int) best));
            return "talk: npc " + best + " at distance " + bestD;
        }

        private string SweepClassicShop(string[] a) {
            int Arg(int i) => i < a.Length ? int.Parse(a[i], CultureInfo.InvariantCulture) : 0;
            Vector2Int Row(int k, bool list) => new Vector2Int(60, 17 + 32 * (k - (list ? shopListFirst : shopCartFirst)) + 16);
            switch (a[0]) {
                case "buy": OnClassicShopChooseClicked(new RoClassicChildLayout { Id = ShopBuyId }); break;
                case "sell": OnClassicShopChooseClicked(new RoClassicChildLayout { Id = ShopSellId }); break;
                case "cancel": OnClassicShopChooseClicked(new RoClassicChildLayout { Id = ShopCancelId }); break;
                case "add":
                    if (ClassicShopDragSource(true, Row(Arg(1), true)) is ClassicShopPayload add) {
                        OnClassicShopCartDropped(ShopCartWnd, Vector2Int.zero, add);
                        if (a.Length > 2 && RoClassicCountWindow.Visible) {
                            RoClassicCountWindow.SetText(a[2]);
                            RoClassicCountWindow.Confirm();
                        }
                    }

                    break;
                case "remove":
                    if (ClassicShopDragSource(false, Row(Arg(1), false)) is ClassicShopPayload back) {
                        OnClassicShopListDropped(shopListWnd, Vector2Int.zero, back);
                        if (a.Length > 2 && RoClassicCountWindow.Visible) {
                            RoClassicCountWindow.SetText(a[2]);
                            RoClassicCountWindow.Confirm();
                        }
                    }

                    break;
                case "ok": OnClassicShopCartClicked(new RoClassicChildLayout { Id = ShopOkId }); break;
                case "close": OnClassicShopCartClicked(new RoClassicChildLayout { Id = ShopCancelId }); break;
                case "toggle": ToggleClassicShopAmount(); break;
                case "desc": DescribeClassicShopRow(true, Row(Arg(1), true)); break;
            }

            var s = new System.Text.StringBuilder();
            s.Append("mode=").Append(shopMode).Append(" choose=").Append(shopChoose != null && shopChoose.Visible)
                .Append(" list=").Append(shopListWnd != null && shopListWnd.Visible ? shopList.Count.ToString(CultureInfo.InvariantCulture) : "hidden").Append(" [");
            for (int i = 0; i < shopList.Count && i < 8; i++) s.Append(i == 0 ? "" : ", ").Append(shopList[i].ItemId).Append('x').Append(shopList[i].Amount).Append('@').Append(shopList[i].Final);
            s.Append("] cart=[");
            for (int i = 0; i < shopCart.Count; i++) s.Append(i == 0 ? "" : ", ").Append(shopCart[i].ItemId).Append('x').Append(shopCart[i].Amount);
            s.Append("] toggle=").Append(shopToggleAmount).Append(" count=").Append(RoClassicCountWindow.Visible ? "'" + RoClassicCountWindow.Text + "'" : "hidden");
            return s.ToString();
        }
    }
}
