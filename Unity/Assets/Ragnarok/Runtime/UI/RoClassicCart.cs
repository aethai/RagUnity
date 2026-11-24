using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>A cart item dragged out of the classic cart window (drag source 9).</summary>
    internal sealed class RoClassicCartPayload : IRoClassicDragPayload {
        public RoItemInstance Item;
        public string IconPath { get; set; }
    }

    /// <summary>
    /// The classic cart window, UIMerchantItemWnd 0x28 (codex-ui-research RUNTIME-RULES-TRADE-CART-VENDING-CHATROOM.md
    /// "0x28 cart contents"): the UIItemBaseWnd frame (the oracle's 280 × 120 capture of the same base class,
    /// UIMerchantMirrorItemWnd, is its template), title MsgStr 0xde at (16, 2), the 32 px item grid, the count and weight
    /// counters (OnDraw 0x8e3cb0), a scroll bar and the resizer (mode 7: 248..312 × 120..216 in 32 px steps). Drops: an
    /// inventory item goes into the cart, a storage item too; right click shows the description, Alt + right click takes
    /// the stack to the storage when it is open, else to the inventory. Esc closes it (no cart-off). Opened by the
    /// "View Cart" key (Alt+W) and the equipment window's cart button; the items come from the session's cart, refreshed
    /// on its functional change sequence while the window is open.
    /// </summary>
    public sealed class RoClassicCart {
        private const string CartPrefs = "ro.ui.classic.cart.";
        private const string CartUi = "유저인터페이스\\";
        private const string CartBasic = "유저인터페이스\\basic_interface\\";
        private const int CartCloseId = 0xc5;
        private const int CartCell = 32;
        private static readonly Color32 CartWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 CartBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 CartCount = new Color32(0x23, 0x23, 0x23, 255);
        private static readonly Color32 CartFull = new Color32(255, 0, 0, 255);   // COLORREF 0x0000ff

        private static RoClassicCart instance;
        private static RoItemMetadataTable metadata = RoItemMetadataTable.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            metadata = RoItemMetadataTable.Empty;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private IRoSocialEconomyCommandExecutor commands;
        private RoClassicWindow classicCart;
        private RoItemMetadataTable drawnMetadata;
        private int seenSequence = int.MinValue;
        private readonly List<RoItemInstance> cartList = new List<RoItemInstance>();
        private int cartFirstRow;
        private int cartCount, cartMaxCount, cartWeight, cartMaxWeight;

        private int CartWidth => classicCart != null ? classicCart.Width : 280;
        private int CartHeight => classicCart != null ? classicCart.Height : 120;
        private int CartCols => (CartWidth - 40) / CartCell;
        private int CartRows => (CartHeight - 38) / CartCell;

        public static bool IsOpen => instance?.classicCart != null && instance.classicCart.Visible;

        // ---- opening, closing, following the session ----

        /// <summary>Per frame (RoClassicGameUi): the open window follows the session's cart and shows the hovered item.</summary>
        public static void Tick(RoLoginShellSession loginSession, RoItemMetadataTable itemMetadata) {
            metadata = itemMetadata ?? RoItemMetadataTable.Empty;
            RoClassicCart c = instance;
            if (c == null || loginSession == null) return;
            if (!ReferenceEquals(c.session, loginSession)) {
                Close();
                c.Bind(loginSession);
            }

            if (c.classicCart == null || !c.classicCart.Visible) return;
            int sequence = loginSession.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Cart);
            if (sequence != c.seenSequence || !ReferenceEquals(c.drawnMetadata, metadata)) {
                c.seenSequence = sequence;
                c.Apply();
            }

            c.TickClassicCart();
        }

        /// <summary>The "View Cart" key / the equipment window's cart button: opens or closes the window.</summary>
        public static void Toggle(RoLoginShellSession loginSession) {
            if (IsOpen) {
                Close();
                return;
            }

            Open(loginSession);
        }

        public static void Open(RoLoginShellSession loginSession) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || loginSession == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicCart { system = system };
            RoClassicCart c = instance;
            if (!ReferenceEquals(c.session, loginSession)) c.Bind(loginSession);
            c.EnsureClassicCart();
            c.seenSequence = loginSession.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Cart);
            c.Apply();
            if (c.classicCart.Visible) return;
            c.classicCart.Visible = true;
            system.Windows.BringToFront(c.classicCart);
            system.Windows.PushEsc(c.classicCart);
        }

        /// <summary>The close button, Esc, leaving the map: the window goes (the cart stays on).</summary>
        public static void Close() {
            RoClassicCart c = instance;
            if (c?.classicCart == null || !c.classicCart.Visible) return;
            c.classicCart.Visible = false;
            RoClassicUiSystem.Instance?.Windows.RemoveEsc(c.classicCart);
        }

        private void Bind(RoLoginShellSession loginSession) {
            session = loginSession;
            commands = loginSession != null ? new RoLegacySocialEconomyCommandExecutor(loginSession) : null;
            seenSequence = int.MinValue;
        }

        /// <summary>The cart's items and counters from the session.</summary>
        private void Apply() {
            RoCartInventorySnapshot cart = session != null ? session.CartSnapshot() : RoCartInventorySnapshot.Empty;
            cart ??= RoCartInventorySnapshot.Empty;
            drawnMetadata = metadata;
            cartList.Clear();
            if (cart.Items != null) {
                foreach (RoItemInstance item in cart.Items) {
                    if (item != null) cartList.Add(item);
                }
            }

            cartList.Sort((a, b) => a.BagIndex.CompareTo(b.BagIndex));
            cartCount = cart.CurrentCount;
            cartMaxCount = cart.MaxCount;
            cartWeight = cart.CurrentWeight;
            cartMaxWeight = cart.MaxWeight;
            ClampCartScroll();
            classicCart.Invalidate();
        }

        private void EnsureClassicCart() {
            if (classicCart != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIMerchantItemWnd", WindowId = 0x28, Width = 280, Height = 120 };
            var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CartCloseId, Path = "close", X = 266, Y = 3, W = 11, H = 11 };
            close.States["normal"] = CartBasic + "sys_close_off.bmp";
            close.States["hover"] = CartBasic + "sys_close_on.bmp";
            close.States["pressed"] = CartBasic + "sys_close_on.bmp";
            layout.Children.Add(close);

            classicCart = new RoClassicWindow(system, layout) { Visible = false, DragSourceAt = ClassicCartDragSource };
            classicCart.DrawDynamic += DrawClassicCart;
            classicCart.Clicked += child => {
                if (child.Id == CartCloseId) Close();
            };
            classicCart.RightClicked += OnClassicCartRightClicked;
            classicCart.Wheeled += (w, local, delta) => ScrollClassicCart(delta > 0 ? -Math.Min(Math.Max(1, CartRows - 1), 3) : Math.Min(Math.Max(1, CartRows - 1), 3));
            classicCart.Dropped += OnClassicCartDropped;
            classicCart.ResizerModeAt = local =>
                local.x >= classicCart.Width - 15 && local.x < classicCart.Width + 1 && local.y >= classicCart.Height - 15 && local.y < classicCart.Height + 1 ? 7 : -1;
            classicCart.ResizeRequested += (w, mode, width, height) => {
                if (mode == 7) ApplyClassicCartSize(width, height, true);
            };
            classicCart.EscClose = () => {
                Close();
                return true;
            };
            classicCart.Moved += w => {
                PlayerPrefs.SetInt(CartPrefs + "x", w.Position.x);
                PlayerPrefs.SetInt(CartPrefs + "y", w.Position.y);
            };

            // msg 0x22: MERCHANTITEMWNDINFO, else (scaledX(200), scaledY(120)) at 280 × 120
            ApplyClassicCartSize(PlayerPrefs.GetInt(CartPrefs + "w", 280), PlayerPrefs.GetInt(CartPrefs + "h", 120), false);
            Vector2Int client = system.ClientSize;
            int x = PlayerPrefs.GetInt(CartPrefs + "x", (client.x - 640) / 2 + 200), y = PlayerPrefs.GetInt(CartPrefs + "y", 120 * client.y / 480);
            classicCart.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - classicCart.Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - classicCart.Height)));
        }

        /// <summary>msg 0xe (mode 7): the width clamped to 232..320 and the height to 120..240, each in 32 px steps from 280 × 120.</summary>
        public static Vector2Int SnapClassicCartSize(int w, int h) {
            int cw = Math.Max(232, Math.Min(320, w)), ch = Math.Max(120, Math.Min(240, h));
            return new Vector2Int(280 + (cw - 280) / 32 * 32, 120 + (ch - 120) / 32 * 32);
        }

        /// <summary>The UIItemBaseWnd frame for a size (title bar, item columns, button bar style 2); the grid is drawn per frame.</summary>
        public static void BuildClassicCartFrame(List<RoClassicOp> ops, int w, int h) {
            ops.Clear();
            RoClassicFrames.TitleBar(ops, w, false);
            RoClassicFrames.ItemColumns(ops, w, h - 38);
            RoClassicFrames.ButtonBar(ops, w, h, 2);
        }

        private void ApplyClassicCartSize(int w, int h, bool save) {
            Vector2Int size = SnapClassicCartSize(w, h);
            classicCart.Layout.Width = size.x;
            classicCart.Layout.Height = size.y;
            classicCart.SetHeight(size.y);
            BuildClassicCartFrame(classicCart.Layout.Ops, size.x, size.y);
            classicCart.SetChildPosition("close", size.x - 14, 3);
            if (save) {
                PlayerPrefs.SetInt(CartPrefs + "w", size.x);
                PlayerPrefs.SetInt(CartPrefs + "h", size.y);
            }

            ClampCartScroll();
            classicCart.Invalidate();
        }

        // ---- OnDraw: UIItemBaseWnd 0x8df970 + UIMerchantItemWnd 0x8e3cb0 ----

        private void DrawClassicCart(RoClassicWindow w) {
            int W = w.Width, H = w.Height, cols = CartCols, rows = CartRows;
            string title = RoClassicUiHost.MsgStr(0xde, "Cart Items");
            w.TextOut(17, 3, title, 0, 12, CartWhite);
            w.TextOut(16, 2, title, 0, 12, CartBlack);
            for (int r = 0; r < rows; r++) {
                for (int c = 0; c < cols; c++) w.DrawBitmapRaw(40 + CartCell * c, 17 + CartCell * r, CartBasic + "itemwin_mid.bmp");
            }

            int skip = cartFirstRow * cols;
            for (int k = 0; k < rows * cols && skip + k < cartList.Count; k++) {
                RoItemInstance item = cartList[skip + k];
                RoClassicItemText.DrawIcon(w, 44 + CartCell * (k % cols), 21 + CartCell * (k / cols), item, metadata, item.Count);
            }

            // the counters: icon_num, count / max; icon_weight, weight / max (the current value red once at the limit)
            RoClassicGdiFont font = w.FontFor(0, 11);
            w.DrawBitmap(10, H - 22, CartUi + "inventory\\icon_num.bmp");
            string count = cartCount.ToString(CultureInfo.InvariantCulture);
            w.TextOut(23, H - 19, count, 0, 11, cartMaxCount > 0 && cartCount >= cartMaxCount ? CartFull : CartCount);
            w.TextOut(23 + font.Measure(count), H - 19, "/" + cartMaxCount.ToString(CultureInfo.InvariantCulture), 0, 11, CartBlack);
            w.DrawBitmap(75, H - 20, CartUi + "inventory\\icon_weight.bmp");
            string weight = (cartWeight / 10).ToString(CultureInfo.InvariantCulture);
            w.TextOut(90, H - 19, weight, 0, 11, cartMaxWeight > 0 && cartWeight >= cartMaxWeight ? CartFull : CartCount);
            w.TextOut(90 + font.Measure(weight), H - 19, "/" + (cartMaxWeight / 10).ToString(CultureInfo.InvariantCulture), 0, 11, CartBlack);

            int total = (cartList.Count + cols - 1) / Math.Max(1, cols);
            w.DrawScrollBar(W - 15, 14, H - 38, Math.Max(0, total - rows), total, rows, cartFirstRow);
            w.DrawBitmap(W - 15, H - 15, CartUi + "btn_resize.bmp");
        }

        // ---- mouse ----

        private RoItemInstance ClassicCartItemAt(Vector2Int local) {
            int cols = CartCols;
            if (local.x < 40 || local.x >= 40 + cols * CartCell || local.y < 17 || local.y >= 17 + CartRows * CartCell) return null;
            int k = cartFirstRow * cols + (local.y - 17) / CartCell * cols + (local.x - 40) / CartCell;
            return k < cartList.Count ? cartList[k] : null;
        }

        /// <summary>vf25 0x8e6a30: an item starts a drag (source 9).</summary>
        private IRoClassicDragPayload ClassicCartDragSource(Vector2Int local) {
            RoItemInstance item = ClassicCartItemAt(local);
            return item == null ? null : new RoClassicCartPayload { Item = item, IconPath = RoClassicItemText.IconPath(item, metadata) };
        }

        /// <summary>vf32 0x8eaf60: the description; Alt sends the stack to the storage when it is open, else to the inventory.</summary>
        private void OnClassicCartRightClicked(RoClassicWindow window, Vector2Int local) {
            RoItemInstance item = ClassicCartItemAt(local);
            if (item == null) return;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.altKey.isPressed) {
                if (!CanSendCartCommand()) return;
                if (RoClassicStorage.IsOpen) {
                    _ = RunEconomyCommandAsync(commands.MoveCartItemToStorageAsync(item.BagIndex, Math.Max(1, item.Count)), "moved to storage");
                } else {
                    _ = RunEconomyCommandAsync(commands.MoveCartItemToInventoryAsync(item.BagIndex, Math.Max(1, item.Count)), "moved to inventory");
                }

                return;
            }
#endif
            RoClassicItemDescription.Toggle(item, metadata);
        }

        /// <summary>
        /// Drop 0x26: an inventory item (source 1) → CZ_MOVE_ITEM_FROM_BODY_TO_CART, a storage item (source 6) →
        /// CZ_MOVE_ITEM_FROM_STORE_TO_CART; one piece at once, a stack through the amount window.
        /// </summary>
        private void OnClassicCartDropped(RoClassicWindow window, Vector2Int local, IRoClassicDragPayload payload) {
            if (!CanSendCartCommand()) return;
            if (payload is RoClassicItemPayload carried && carried.Item != null && !carried.Item.IsEquipped) {
                RoItemInstance item = carried.Item;
                CartAmount(item, n => _ = RunEconomyCommandAsync(commands.MoveInventoryItemToCartAsync(item.BagIndex, n), "moved to cart"));
            } else if (payload is RoClassicStoragePayload stored && stored.Item != null) {
                RoItemInstance item = stored.Item;
                CartAmount(item, n => _ = RunEconomyCommandAsync(commands.MoveStorageItemToCartAsync(item.BagIndex, n), "moved to cart"));
            }
        }

        private void CartAmount(RoItemInstance item, Action<int> send) {
            if (item.Count <= 1) {
                send(1);
                return;
            }

            RoClassicCountWindow.Show(item.GetFullDisplayName(metadata), item.Count, n => {
                n = Math.Min(n, item.Count);
                if (n >= 1 && CanSendCartCommand()) send((int) n);
            });
        }

        /// <summary>A cart item dropped on the inventory or the storage: one piece at once, else the amount window.</summary>
        internal static void TakeItem(RoItemInstance item, bool toStorage) {
            RoClassicCart c = instance;
            if (c == null || item == null || !c.CanSendCartCommand()) return;
            c.CartAmount(item, n => _ = RunEconomyCommandAsync(toStorage
                ? c.commands.MoveCartItemToStorageAsync(item.BagIndex, n)
                : c.commands.MoveCartItemToInventoryAsync(item.BagIndex, n), toStorage ? "moved to storage" : "moved to inventory"));
        }

        private void ScrollClassicCart(int rows) {
            int total = (cartList.Count + CartCols - 1) / Math.Max(1, CartCols);
            int first = Mathf.Clamp(cartFirstRow + rows, 0, Math.Max(0, total - CartRows));
            if (first == cartFirstRow) return;
            cartFirstRow = first;
            classicCart.Invalidate();
        }

        private void ClampCartScroll() {
            int total = (cartList.Count + CartCols - 1) / Math.Max(1, CartCols);
            cartFirstRow = Mathf.Clamp(cartFirstRow, 0, Math.Max(0, total - CartRows));
        }

        /// <summary>vf28 hover: the item's name and amount above its cell.</summary>
        private void TickClassicCart() {
            RoClassicWindowManager windows = system.Windows;
            if (windows == null || windows.HoverWindow != classicCart || windows.Dragging != null) return;
            Vector2Int local = windows.Mouse - classicCart.Position;
            RoItemInstance item = ClassicCartItemAt(local);
            if (item == null) return;
            string text = string.Format(CultureInfo.InvariantCulture, "{0}: {1} ea.", item.GetFullDisplayName(metadata), item.Count);
            RoClassicTooltip.Shared.Show(text, RoClassicItemText.TooltipColor(item, metadata),
                classicCart.Position.x + 40 + (local.x - 40) / CartCell * CartCell,
                classicCart.Position.y + 1 + (local.y - 17) / CartCell * CartCell);
        }

        // ---- commands ----

        private bool CanSendCommand() {
            return session != null && commands != null && session.CommandStateSnapshot().CanSendMapCommand;
        }

        private bool CanSendCartCommand() {
            if (!CanSendCommand()) return false;
            RoCartInventorySnapshot cart = session.CartSnapshot() ?? RoCartInventorySnapshot.Empty;
            return cart.HasCart || cart.IsOpen || (cart.Items != null && cart.Items.Length > 0);
        }

        private static async Task RunEconomyCommandAsync(Task<RoEconomyCommandResult> command, string action) {
            if (command == null) return;
            RoEconomyCommandResult result = await command;
            if (!result.Accepted) Debug.LogWarning("Cart item was not " + action + ": " + result.Status + " " + result.Message);
        }

        // ---- diagnostics console (RoSkillSweepRunner "!cart ...") ----

        /// <summary>open | close | size w h | wheel n | take i [storage] | put bagIndex [n] | state.</summary>
        internal static string Sweep(RoLoginShellSession loginSession, string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
            if (a[0] == "open") Open(loginSession);
            else if (a[0] == "close") Close();
            RoClassicCart c = instance;
            if (c?.classicCart == null) return "no classic cart (open it with Alt+W)";
            switch (a[0]) {
                case "size":
                    c.ApplyClassicCartSize(Arg(1), Arg(2), true);
                    break;
                case "wheel":
                    c.ScrollClassicCart(Arg(1));
                    break;
                case "take":
                    TakeItem(Arg(1) < c.cartList.Count ? c.cartList[Arg(1)] : null, a.Length > 2 && a[2] == "storage");
                    break;
                case "put":
                    // put <inventory bag index> [count]: as if dropped from the inventory
                    if (c.CanSendCartCommand()) _ = RunEconomyCommandAsync(c.commands.MoveInventoryItemToCartAsync(Arg(1), Math.Max(1, Arg(2))), "moved to cart");
                    break;
            }

            var s = new StringBuilder();
            s.Append("shown=").Append(c.classicCart.Visible).Append(" size=").Append(c.classicCart.Width).Append('x').Append(c.classicCart.Height)
                .Append(" grid=").Append(c.CartCols).Append('x').Append(c.CartRows).Append(" first=").Append(c.cartFirstRow)
                .Append(" count=").Append(c.cartCount).Append('/').Append(c.cartMaxCount).Append(" weight=").Append(c.cartWeight).Append('/').Append(c.cartMaxWeight)
                .Append(" items=");
            foreach (RoItemInstance item in c.cartList) s.Append(item.BagIndex).Append(':').Append(item.ItemId).Append('x').Append(item.Count).Append(' ');
            return s.ToString().TrimEnd();
        }
    }
}
