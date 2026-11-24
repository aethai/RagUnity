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
    /// <summary>A shop item dragged between the classic shop (0x2b) and the purchase list (0x2c).</summary>
    internal sealed class RoClassicShopPayload : IRoClassicDragPayload {
        public bool FromPurchase;
        public int Index;
        public string IconPath { get; set; }
    }

    /// <summary>A vending item dragged between the classic sale setup (0x29) and its cart mirror (0x2a).</summary>
    internal sealed class RoClassicVendingPayload : IRoClassicDragPayload {
        public bool FromSetup;
        public RoItemInstance Item;
        public string IconPath { get; set; }
    }

    /// <summary>
    /// The classic vending windows of the seller (codex-ui-research RUNTIME-RULES-TRADE-CART-VENDING-CHATROOM.md
    /// "Vending and buying stores"): UIMerchantShopMakeWnd 0x29 (sale setup, oracle layout 400 × 260: the shop name,
    /// one row per allowed slot with the item and its price field, 4 rows visible, the total, OK / Cancel) with
    /// UIMerchantMirrorItemWnd 0x2a (the cart items still available, oracle layout 280 × 120); items move between them
    /// by drag and drop (a stack through the amount window). OK sends CZ_REQ_OPENSTORE2 with the result byte 1, Cancel
    /// and Esc with 0. UIMerchantItemMyShopWnd 0x2d is the open shop (oracle 280 × 120: "My Shop", the items with their
    /// price as the tooltip, Close = CZ_REQ_CLOSESTORE, Esc ignored). Driven by the legacy RoVendingSetupWindow /
    /// RoActiveVendingWindow (shown by the shell on the server's vending state); the legacy session sends the packets.
    /// </summary>
    public sealed class RoClassicVending : IRoClassicUiProducer {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string Ui = "유저인터페이스\\";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const string Prefs = "ro.ui.classic.vending.";
        private const int OkId = 0xb4;
        private const int CancelId = 0xb5;
        private const int CheckId = 0xd1;
        private const int CloseId = 0xc5;
        private const int RowH = 42;
        private const int VisibleRows = 4;
        private const int Cell = 32;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private static readonly Color32 EditFill = new Color32(0xe8, 0xe8, 0xe8, 255);

        private sealed class Entry {
            public RoItemInstance Item;
            public int Amount;
            public string Price = string.Empty;
        }

        private static RoClassicVending instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private IRoSocialEconomyCommandExecutor commands;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;

        // 0x29 / 0x2a
        private RoClassicWindow make;
        private RoClassicWindow mirror;
        private RoClassicEditField titleEdit;
        private readonly RoClassicEditField[] priceEdits = new RoClassicEditField[VisibleRows];
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<RoItemInstance> mirrorList = new List<RoItemInstance>();
        private int slotLimit;
        private int makeFirst;
        private int mirrorFirstRow;
        private bool safeCheck;
        private bool setupShown;
        private int seenCartSequence = -1;
        private bool sending;

        // 0x2d
        private RoClassicWindow myShop;
        private RoVendingShopItemSnapshot[] shopItems = Array.Empty<RoVendingShopItemSnapshot>();
        private int shopFirstRow;
        // DAT_01553554, the open shop's check box: effect\ef_steal.wav on every sale (0x137 → 0xc2a568), on by default
        // (the client saves bSoundEffectInItemSellWnd, its inverse); seenSales counts the session's sale reports
        private bool notifySoldOut = PlayerPrefs.GetInt(SoldSoundPrefs, 1) != 0;
        private int seenSales;
        private const string SoldSoundPrefs = "ro.ui.classic.vending.soldSound";

        private static RoClassicVending Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicVending { system = system };
                system.Register(instance);
            }

            return instance;
        }

        private void Bind(RoLoginShellSession boundSession, IRoSocialEconomyCommandExecutor boundCommands, RoItemMetadataTable boundMetadata) {
            session = boundSession;
            commands = boundCommands;
            metadata = boundMetadata ?? RoItemMetadataTable.Empty;
        }

        // ================================================================ 0x29 / 0x2a: the sale setup

        /// <summary>The shell's vending state allows a setup (ZC_OPENSTORE): true when the classic windows show it.</summary>
        public static bool ShowSetup(RoLoginShellSession session, IRoSocialEconomyCommandExecutor commands, RoItemMetadataTable metadata, RoVendingSnapshot vending) {
            RoClassicVending v = Ensure();
            if (v == null) return false;
            v.Bind(session, commands, metadata);
            v.OpenSetup(vending.SetupSlotLimit);
            return true;
        }

        public static void HideSetup() => instance?.CloseSetup();

        private void OpenSetup(int slots) {
            slotLimit = Mathf.Clamp(slots, 0, 13);
            if (setupShown) {
                RebuildMirror();
                make.Invalidate();
                return;
            }

            EnsureSetupWindows();
            setupShown = true;
            sending = false;
            entries.Clear();
            makeFirst = 0;
            mirrorFirstRow = 0;
            titleEdit.SetText(string.Empty);
            foreach (RoClassicEditField edit in priceEdits) edit.SetText(string.Empty);
            seenCartSequence = -1;
            RebuildMirror();
            BindPriceEdits();
            Vector2Int client = system.ClientSize;
            Place(mirror, PlayerPrefs.GetInt(Prefs + "mirror.x", 0), PlayerPrefs.GetInt(Prefs + "mirror.y", 120), client);
            Place(make, PlayerPrefs.GetInt(Prefs + "make.x", 0), PlayerPrefs.GetInt(Prefs + "make.y", 240), client);
            mirror.Visible = true;
            make.Visible = true;
            system.Windows.BringToFront(mirror);
            system.Windows.BringToFront(make);
            system.Windows.PushEsc(make);
            titleEdit.Focus();
        }

        private static void Place(RoClassicWindow window, int x, int y, Vector2Int client) =>
            window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - window.Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - window.Height)));

        private void EnsureSetupWindows() {
            if (make != null) return;
            RoClassicWindowLayout makeLayout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIMerchantShopMakeWnd").text);
            // the capture drew four rows and its texts: the frame stays, rows and texts are drawn per state
            makeLayout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text || (op.Path != null && op.Path.EndsWith("itemwin_mid.bmp", StringComparison.OrdinalIgnoreCase)));
            foreach (RoClassicChildLayout child in makeLayout.Children) {
                if (child.Class == "UIEditCtrl" || child.Class == "UIScrollBar") child.Visible = false;
            }

            make = new RoClassicWindow(system, makeLayout) { Visible = false, DragSourceAt = MakeDragSource };
            make.DrawDynamic += DrawMake;
            make.Clicked += child => {
                if (child.Id == OkId) _ = SetupOkAsync();
                else if (child.Id == CancelId) _ = SetupCancelAsync();
            };
            make.Pressed += (w, local) => {
                RoClassicChildLayout check = Find(make, CheckId);
                if (check != null && local.x >= check.X && local.x < check.X + check.W && local.y >= check.Y && local.y < check.Y + check.H) {
                    safeCheck = !safeCheck;
                    SyncCheckBox(make, CheckId, safeCheck);
                }
            };
            make.Wheeled += (w, local, delta) => ScrollMake(delta > 0 ? -1 : 1);
            make.Dropped += (w, local, payload) => {
                if (payload is RoClassicVendingPayload moved && !moved.FromSetup) AddToSetup(moved.Item);
            };
            make.RightClicked += (w, local) => {
                Entry e = MakeEntryAt(local);
                if (e != null) RoClassicItemDescription.Toggle(e.Item, metadata);
            };
            make.EscClose = () => {
                _ = SetupCancelAsync(); // the manager sends SendMsg(6, 0xb5) to 0x29
                return true;
            };
            make.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "make.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "make.y", w.Position.y);
            };
            titleEdit = new RoClassicEditField { Rect = new RectInt(41, 22, 334, 16), Background = EditFill, MaxBytes = 79 };
            make.AddEditField(titleEdit);
            for (int r = 0; r < VisibleRows; r++) {
                int row = r;
                var edit = new RoClassicEditField { Rect = new RectInt(400 - 119, 54 + RowH * r, 94, 16), Background = EditFill, MaxBytes = 10 };
                edit.Changed += field => {
                    string digits = Digits(field.Text);
                    if (digits != field.Text) field.SetText(digits);
                    if (makeFirst + row < entries.Count) entries[makeFirst + row].Price = digits;
                    make.Invalidate();
                };
                priceEdits[r] = edit;
                make.AddEditField(edit);
            }

            RoClassicWindowLayout mirrorLayout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIMerchantMirrorItemWnd").text);
            mirrorLayout.Ops.RemoveAll(op => op.Path != null && op.Path.EndsWith("itemwin_mid.bmp", StringComparison.OrdinalIgnoreCase));
            foreach (RoClassicChildLayout child in mirrorLayout.Children) {
                if (child.Class == "UIScrollBar") child.Visible = false;
            }

            mirror = new RoClassicWindow(system, mirrorLayout) { Visible = false, DragSourceAt = MirrorDragSource };
            mirror.DrawDynamic += DrawMirror;
            mirror.Wheeled += (w, local, delta) => ScrollMirror(delta > 0 ? -1 : 1);
            mirror.Dropped += (w, local, payload) => {
                if (payload is RoClassicVendingPayload moved && moved.FromSetup) RemoveFromSetup(moved.Item);
            };
            mirror.RightClicked += (w, local) => {
                RoItemInstance item = MirrorItemAt(local);
                if (item != null) RoClassicItemDescription.Toggle(item, metadata);
            };
            mirror.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "mirror.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "mirror.y", w.Position.y);
            };
        }

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

        /// <summary>The cart items with what is not yet in the setup (the mirror's list, packet order).</summary>
        private void RebuildMirror() {
            mirrorList.Clear();
            RoCartInventorySnapshot cart = session != null ? session.CartSnapshot() : RoCartInventorySnapshot.Empty;
            foreach (RoItemInstance item in cart.Items ?? Array.Empty<RoItemInstance>()) {
                if (item == null || item.Count <= 0) continue;
                int used = 0;
                foreach (Entry e in entries) {
                    if (e.Item.BagIndex == item.BagIndex) used += e.Amount;
                }

                if (item.Count - used > 0) mirrorList.Add(used == 0 ? item : item.WithCount(item.Count - used));
            }

            // an entry whose item left the cart goes too
            entries.RemoveAll(e => {
                foreach (RoItemInstance item in cart.Items ?? Array.Empty<RoItemInstance>()) {
                    if (item != null && item.BagIndex == e.Item.BagIndex) return false;
                }

                return true;
            });
            mirrorFirstRow = Mathf.Clamp(mirrorFirstRow, 0, Math.Max(0, MirrorTotalRows - MirrorRows));
            mirror?.Invalidate();
            make?.Invalidate();
        }

        private int MirrorCols => (mirror.Width - 40) / Cell;
        private int MirrorRows => (mirror.Height - 38) / Cell;
        private int MirrorTotalRows => (mirrorList.Count + Math.Max(1, MirrorCols) - 1) / Math.Max(1, MirrorCols);

        private void DrawMirror(RoClassicWindow w) {
            int cols = MirrorCols, rows = MirrorRows;
            for (int r = 0; r < rows; r++) {
                for (int c = 0; c < cols; c++) w.DrawBitmapRaw(40 + Cell * c, 17 + Cell * r, Basic + "itemwin_mid.bmp");
            }

            int skip = mirrorFirstRow * cols;
            for (int k = 0; k < rows * cols && skip + k < mirrorList.Count; k++) {
                RoItemInstance item = mirrorList[skip + k];
                RoClassicItemText.DrawIcon(w, 44 + Cell * (k % cols), 21 + Cell * (k / cols), item, metadata, item.Count);
            }

            w.DrawScrollBar(w.Width - 15, 14, w.Height - 38, Math.Max(0, MirrorTotalRows - rows), MirrorTotalRows, rows, mirrorFirstRow);
        }

        private RoItemInstance MirrorItemAt(Vector2Int local) {
            int cols = MirrorCols;
            if (local.x < 40 || local.x >= 40 + cols * Cell || local.y < 17 || local.y >= 17 + MirrorRows * Cell) return null;
            int k = mirrorFirstRow * cols + (local.y - 17) / Cell * cols + (local.x - 40) / Cell;
            return k < mirrorList.Count ? mirrorList[k] : null;
        }

        private IRoClassicDragPayload MirrorDragSource(Vector2Int local) {
            RoItemInstance item = MirrorItemAt(local);
            return item == null ? null : new RoClassicVendingPayload { FromSetup = false, Item = item, IconPath = RoClassicItemText.IconPath(item, metadata) };
        }

        private void ScrollMirror(int rows) {
            int first = Mathf.Clamp(mirrorFirstRow + rows, 0, Math.Max(0, MirrorTotalRows - MirrorRows));
            if (first == mirrorFirstRow) return;
            mirrorFirstRow = first;
            mirror.Invalidate();
        }

        /// <summary>
        /// OnDraw 0x8e3f70 (sale): title MsgStr 0xe0 at (16, 3) over its white shadow, "Name" MsgStr 0x11a at (2, 25),
        /// rows from y 54 every 42 px (the cell, the item, "Price:"), the total line MsgStr 0xc97 in red at (5, 217).
        /// </summary>
        private void DrawMake(RoClassicWindow w) {
            string title = RoClassicUiHost.MsgStr(0xe0, "Opening a stall");
            w.TextOut(17, 4, title, 0, 12, White);
            w.TextOut(16, 3, title, 0, 12, Black);
            w.TextOut(2, 25, RoClassicUiHost.MsgStr(0x11a, "Name"), 0, 12, Black);
            int rows = Math.Min(VisibleRows, Math.Max(0, slotLimit - makeFirst));
            for (int r = 0; r < rows; r++) {
                int y = 54 + RowH * r, k = makeFirst + r;
                w.DrawBitmapRaw(3, y, Basic + "itemwin_mid.bmp");
                w.TextOut(235, y + 3, "Price:", 0, 12, Black);
                if (k >= entries.Count) continue;
                Entry e = entries[k];
                RoClassicItemText.DrawIcon(w, 7, y + 4, e.Item, metadata, e.Amount);
                RoClassicItemText.DrawName(w, 40, y + 3, 190, e.Item, metadata);
                w.TextOut(40, y + 19, "x " + e.Amount.ToString(CultureInfo.InvariantCulture), 0, 12, Black);
            }

            ulong total = 0;
            foreach (Entry e in entries) {
                if (ulong.TryParse(e.Price, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong price)) total += price * (ulong) e.Amount;
            }

            string line = RoClassicUiHost.MsgStr(0xc97, "~ When selling items, total amount of zeny received ");
            w.TextOut(5, 217, line, 0, 12, Red);
            w.TextOut(5 + w.FontFor(0, 12).Measure(line), 217, RoClassicItemText.Commas((long) Math.Min(total, long.MaxValue)) + " Zeny", 0, 12, Red);
            int range = Math.Max(0, slotLimit - VisibleRows);
            w.DrawScrollBar(385, 17, 202, range, slotLimit, VisibleRows, makeFirst);
        }

        private Entry MakeEntryAt(Vector2Int local) {
            if (local.y < 54 || local.y >= 54 + RowH * VisibleRows || local.x < 3 || local.x >= 230) return null;
            int k = makeFirst + (local.y - 54) / RowH;
            return k < entries.Count ? entries[k] : null;
        }

        private IRoClassicDragPayload MakeDragSource(Vector2Int local) {
            Entry e = MakeEntryAt(local);
            return e == null ? null : new RoClassicVendingPayload { FromSetup = true, Item = e.Item, IconPath = RoClassicItemText.IconPath(e.Item, metadata) };
        }

        private void ScrollMake(int rows) {
            int first = Mathf.Clamp(makeFirst + rows, 0, Math.Max(0, slotLimit - VisibleRows));
            if (first == makeFirst) return;
            makeFirst = first;
            BindPriceEdits();
            make.Invalidate();
        }

        /// <summary>The visible rows' price fields show their entries' prices; rows without an entry hide theirs.</summary>
        private void BindPriceEdits() {
            for (int r = 0; r < VisibleRows; r++) {
                int k = makeFirst + r;
                bool slot = k < slotLimit, filled = k < entries.Count;
                priceEdits[r].Visible = slot && filled;
                priceEdits[r].SetText(filled ? entries[k].Price : string.Empty);
            }
        }

        /// <summary>Case 0x26 from the mirror: the item into a free row (a stack through the amount window).</summary>
        private void AddToSetup(RoItemInstance item) {
            if (item == null || sending) return;
            void Add(int amount) {
                amount = Math.Max(1, Math.Min(amount, item.Count));
                foreach (Entry existing in entries) {
                    if (existing.Item.BagIndex == item.BagIndex) {
                        existing.Amount += amount;
                        RebuildMirror();
                        return;
                    }
                }

                if (entries.Count >= slotLimit) return; // no free row
                RoCartInventorySnapshot cart = session != null ? session.CartSnapshot() : RoCartInventorySnapshot.Empty;
                RoItemInstance full = item;
                foreach (RoItemInstance c in cart.Items ?? Array.Empty<RoItemInstance>()) {
                    if (c != null && c.BagIndex == item.BagIndex) full = c;
                }

                entries.Add(new Entry { Item = full, Amount = amount });
                RebuildMirror();
                BindPriceEdits();
                int k = entries.Count - 1;
                if (k >= makeFirst && k < makeFirst + VisibleRows) priceEdits[k - makeFirst].Focus();
            }

            if (item.Count <= 1) {
                Add(1);
                return;
            }

            RoClassicCountWindow.Show(item.GetFullDisplayName(metadata), item.Count, n => {
                if (n >= 1) Add((int) n);
            });
        }

        private void RemoveFromSetup(RoItemInstance item) {
            if (item == null || sending) return;
            entries.RemoveAll(e => e.Item.BagIndex == item.BagIndex);
            makeFirst = Mathf.Clamp(makeFirst, 0, Math.Max(0, slotLimit - VisibleRows));
            RebuildMirror();
            BindPriceEdits();
        }

        /// <summary>OK: the name and every row's price are required; then CZ_REQ_OPENSTORE2 (result 1).</summary>
        private async Task SetupOkAsync() {
            if (sending || commands == null) return;
            string title = titleEdit.Text.Trim();
            if (title.Length == 0) {
                Chat(RoClassicUiHost.MsgStr(0xe1, "Please enter the stall's name"));
                return;
            }

            if (entries.Count == 0) return;
            var requests = new RoVendingOpenStoreItem[entries.Count];
            for (int i = 0; i < entries.Count; i++) {
                uint.TryParse(entries[i].Price, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint price);
                if (price == 0) {
                    Chat(RoClassicUiHost.MsgStr(0xaa0, "There are items priced 0 Crackers. You cannot open vending."));
                    return;
                }

                requests[i] = new RoVendingOpenStoreItem((ushort) entries[i].Item.BagIndex, (ushort) entries[i].Amount, Math.Min(price, 1000000000u));
            }

            sending = true;
            RoAdvancedCommandResult result = await commands.OpenVendingStoreAsync(title, requests);
            if (!result.IsAccepted) {
                sending = false;
                Chat(result.Diagnostic);
            }
        }

        /// <summary>Cancel / Esc: CZ_REQ_OPENSTORE2 with the result byte 0 and the window goes.</summary>
        private async Task SetupCancelAsync() {
            if (commands == null) return;
            string title = titleEdit != null ? titleEdit.Text.Trim() : string.Empty;
            CloseSetup();
            await commands.CancelVendingSetupAsync(title);
        }

        private void CloseSetup() {
            if (!setupShown) return;
            setupShown = false;
            if (RoClassicTextInput.Focused != null && (RoClassicTextInput.Focused == titleEdit || Array.IndexOf(priceEdits, RoClassicTextInput.Focused) >= 0)) {
                RoClassicTextInput.Focus(null);
            }

            if (make != null) {
                make.Visible = false;
                system.Windows.RemoveEsc(make);
            }

            if (mirror != null) mirror.Visible = false;
        }

        private static void Chat(string text) {
            if (!string.IsNullOrEmpty(text)) RoClassicChat.Instance?.AddLine(text, "#FF0000");
        }

        // ================================================================ 0x2d: the open shop

        /// <summary>The shell's vending state has the own shop open (ZC_ACK_OPENSTORE2 / ZC_PC_PURCHASE_MYITEMLIST).</summary>
        public static bool ShowOwnShop(RoLoginShellSession session, IRoSocialEconomyCommandExecutor commands, RoItemMetadataTable metadata, RoVendingSnapshot vending) {
            RoClassicVending v = Ensure();
            if (v == null) return false;
            v.Bind(session, commands, metadata);
            v.CloseSetup();
            v.OpenOwnShop(vending);
            return true;
        }

        public static void HideOwnShop() {
            if (instance?.myShop == null) return;
            instance.myShop.Visible = false;
        }

        private void OpenOwnShop(RoVendingSnapshot vending) {
            shopItems = vending.OwnStoreItems ?? Array.Empty<RoVendingShopItemSnapshot>();
            if (myShop == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIMerchantItemMyShopWnd").text);
                layout.Ops.RemoveAll(op => op.Path != null && op.Path.EndsWith("itemwin_mid.bmp", StringComparison.OrdinalIgnoreCase));
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIScrollBar") child.Visible = false;
                }

                myShop = new RoClassicWindow(system, layout) { Visible = false };
                myShop.DrawDynamic += DrawMyShop;
                myShop.Clicked += child => {
                    if (child.Id == CloseId && commands != null) _ = commands.CloseVendingStoreAsync();
                };
                myShop.Pressed += (w, local) => {
                    RoClassicChildLayout check = Find(myShop, CheckId);
                    if (check != null && local.x >= check.X && local.x < check.X + check.W && local.y >= check.Y && local.y < check.Y + check.H) {
                        notifySoldOut = !notifySoldOut;
                        PlayerPrefs.SetInt(SoldSoundPrefs, notifySoldOut ? 1 : 0);
                        SyncCheckBox(myShop, CheckId, notifySoldOut);
                    }
                };
                myShop.RightClicked += (w, local) => {
                    int k = ShopIndexAt(local);
                    if (k >= 0) RoClassicItemDescription.Toggle(ShopItem(shopItems[k]), metadata);
                };
                myShop.Wheeled += (w, local, delta) => {
                    int total = (shopItems.Length + ShopCols - 1) / ShopCols;
                    shopFirstRow = Mathf.Clamp(shopFirstRow + (delta > 0 ? -1 : 1), 0, Math.Max(0, total - ShopRows));
                    myShop.Invalidate();
                };
                myShop.Moved += w => {
                    PlayerPrefs.SetInt(Prefs + "shop.x", w.Position.x);
                    PlayerPrefs.SetInt(Prefs + "shop.y", w.Position.y);
                };
                Place(myShop, PlayerPrefs.GetInt(Prefs + "shop.x", (system.ClientSize.x - 640) / 2 + 200), PlayerPrefs.GetInt(Prefs + "shop.y", 120 * system.ClientSize.y / 480), system.ClientSize);
                SyncCheckBox(myShop, CheckId, notifySoldOut);
            }

            int sold = vending.Sales?.Length ?? 0;
            if (myShop.Visible && sold > seenSales && notifySoldOut) Ragnarok.Audio.RoAudioEvents.PlayUiSound("effect\\ef_steal.wav");
            seenSales = sold;
            if (!myShop.Visible && !myShop.HiddenByCloseTabs) {   // a refresh leaves F11's hiding alone
                myShop.Visible = true;
                system.Windows.BringToFront(myShop);
            }

            myShop.Invalidate();
        }

        private int ShopCols => (myShop.Width - 40) / Cell;
        private int ShopRows => (myShop.Height - 38) / Cell;

        internal static RoItemInstance ShopItem(RoVendingShopItemSnapshot s) {
            int itemId = unchecked((int) s.ItemId);
            return RoEconomyItemSnapshot.IsKnownEquipmentItemType(s.ItemTypeRaw)
                ? RoItemInstance.CreateEquipment(s.Index, itemId, Math.Max(1, s.Amount), s.IsIdentified, refineLevel: s.RefineLevel,
                    cardIds: s.CardIds, itemTypeRaw: s.ItemTypeRaw, isDamaged: s.IsDamaged)
                : RoItemInstance.CreateRegular(s.Index, itemId, s.Amount, s.IsIdentified, itemTypeRaw: s.ItemTypeRaw, isDamaged: s.IsDamaged);
        }

        private void DrawMyShop(RoClassicWindow w) {
            int cols = ShopCols, rows = ShopRows;
            for (int r = 0; r < rows; r++) {
                for (int c = 0; c < cols; c++) w.DrawBitmapRaw(40 + Cell * c, 17 + Cell * r, Basic + "itemwin_mid.bmp");
            }

            int skip = shopFirstRow * cols;
            for (int k = 0; k < rows * cols && skip + k < shopItems.Length; k++) {
                RoClassicItemText.DrawIcon(w, 44 + Cell * (k % cols), 21 + Cell * (k / cols), ShopItem(shopItems[skip + k]), metadata, shopItems[skip + k].Amount);
            }

            int total = (shopItems.Length + cols - 1) / Math.Max(1, cols);
            w.DrawScrollBar(w.Width - 15, 14, w.Height - 38, Math.Max(0, total - rows), total, rows, shopFirstRow);
        }

        private int ShopIndexAt(Vector2Int local) {
            int cols = ShopCols;
            if (local.x < 40 || local.x >= 40 + cols * Cell || local.y < 17 || local.y >= 17 + ShopRows * Cell) return -1;
            int k = shopFirstRow * cols + (local.y - 17) / Cell * cols + (local.x - 40) / Cell;
            return k < shopItems.Length ? k : -1;
        }

        // ================================================================ 0x2b / 0x2c / 0xe6: buying from a shop

        private RoClassicWindow shopWin;
        private RoClassicWindow purchaseWin;
        private RoClassicWindow confirmWin;
        private RoPlayerShopSnapshot customerShop;
        private bool customerShown;
        private int customerSelected = -1;   // +0xdc
        private int customerFirst;
        private string customerOwnerName = string.Empty;
        private int purchaseFirst;
        private readonly List<(int Index, int Amount)> purchase = new List<(int, int)>();
        private readonly Dictionary<uint, int> dismissedShops = new Dictionary<uint, int>();

        /// <summary>
        /// A seller's list (ZC_PC_PURCHASE_ITEMLIST_FROMMC2): UIMerchantItemShopWnd 0x2b (the items, 280 wide at (0, 200),
        /// titled "%s - %s": MsgStr 0xe3 and the seller's name) and UIMerchantItemPurchaseWnd 0x2c (what to buy). Both draw
        /// one 32 px row per item (0x8e33e0 / 0x8e2ed0), at least four, over a 56 px frame. A shop closed with Cancel stays closed
        /// until its board is clicked again (a new list).
        /// </summary>
        public static bool ShowCustomerShop(RoLoginShellSession session, IRoSocialEconomyCommandExecutor commands, RoItemMetadataTable metadata, RoPlayerShopSnapshot shop) {
            RoClassicVending v = Ensure();
            if (v == null) return false;
            v.Bind(session, commands, metadata);
            if (v.dismissedShops.TryGetValue(shop.OwnerAid, out int serial) && serial == shop.ListSerial) return true;
            v.OpenCustomerShop(shop);
            return true;
        }

        public static void HideCustomerShop(uint ownerAid) {
            if (instance == null || !instance.customerShown || instance.customerShop.OwnerAid != ownerAid) return;
            instance.CloseCustomerShop(false);
        }

        /// <summary>Mode msg 0xf5 (/shopping: a right press on a shop board, 0x7c13f0): 0x2b and 0x2c close like Cancel.</summary>
        public static void CloseCustomerShopWindows() => instance?.CloseCustomerShop(true);

        private void OpenCustomerShop(RoPlayerShopSnapshot shop) {
            bool newShop = !customerShown || customerShop.OwnerAid != shop.OwnerAid || customerShop.ListSerial != shop.ListSerial;
            customerShop = shop;
            EnsureCustomerWindows();
            if (newShop) {
                purchase.Clear();
                customerSelected = -1;
                customerFirst = 0;
                purchaseFirst = 0;
                customerOwnerName = FindActorName(shop.OwnerAid);
                Vector2Int client = system.ClientSize;
                Place(shopWin, PlayerPrefs.GetInt(Prefs + "itemshop.x", 0), PlayerPrefs.GetInt(Prefs + "itemshop.y", 200), client);
                Place(purchaseWin, PlayerPrefs.GetInt(Prefs + "purchase.x", 280), PlayerPrefs.GetInt(Prefs + "purchase.y", 280), client);
            }

            // the stock changed: the purchase list keeps what is still there
            purchase.RemoveAll(p => ShopEntry(p.Index) == null);
            for (int i = 0; i < purchase.Count; i++) {
                RoVendingShopItemSnapshot? s = ShopEntry(purchase[i].Index);
                if (s.HasValue && purchase[i].Amount > s.Value.Amount) purchase[i] = (purchase[i].Index, s.Value.Amount);
            }

            if (newShop) RoClassicMerchantList.SizeToItems(shopWin, customerShop.Items?.Length ?? 0, system.ClientSize);
            SizePurchase();
            customerShown = true;
            shopWin.Visible = true;
            purchaseWin.Visible = true;
            if (newShop) {
                system.Windows.BringToFront(shopWin);
                system.Windows.BringToFront(purchaseWin);
                system.Windows.PushEsc(purchaseWin);
            }

            UpdatePurchaseTotal();
            shopWin.Invalidate();
            purchaseWin.Invalidate();
        }

        private RoVendingShopItemSnapshot? ShopEntry(int index) {
            foreach (RoVendingShopItemSnapshot s in customerShop.Items ?? Array.Empty<RoVendingShopItemSnapshot>()) {
                if (s.Index == index && s.Amount > 0) return s;
            }

            return null;
        }

        private void EnsureCustomerWindows() {
            if (shopWin != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIMerchantItemShopWnd", WindowId = 0x2b, Width = ItemShopWidth, Height = RoClassicMerchantList.HeightForItems(0) };
            RoClassicMerchantList.BuildFrame(layout.Ops, layout.Width, layout.Height);
            shopWin = new RoClassicWindow(system, layout) { Visible = false, DragSourceAt = ShopDragSource };
            shopWin.DrawDynamic += DrawItemShop;
            RoClassicMerchantList.Configure(shopWin, Prefs + "itemshop", false, () => {
                customerFirst = Mathf.Clamp(customerFirst, 0, Math.Max(0, (customerShop.Items?.Length ?? 0) - ItemShopRows));
            });
            shopWin.Wheeled += (w, local, delta) => {
                customerFirst = Mathf.Clamp(customerFirst + (delta > 0 ? -ItemShopRows : ItemShopRows), 0,
                    Math.Max(0, (customerShop.Items?.Length ?? 0) - ItemShopRows));
                w.Invalidate();
            };
            shopWin.Pressed += (w, local) => {
                int k = ItemShopIndexAt(local);
                if (k < 0 || k == customerSelected) return;
                customerSelected = k;
                shopWin.Invalidate();
            };
            shopWin.RightClicked += (w, local) => {
                int k = ItemShopIndexAt(local);
                if (k < 0) return;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.altKey.isPressed) {
                    AddToPurchase(customerShop.Items[k].Index); // vf32 0x8eae20: Alt moves it
                    return;
                }
#endif
                RoClassicItemDescription.Toggle(ShopItem(customerShop.Items[k]), metadata);
            };
            shopWin.Dropped += (w, local, payload) => {
                if (payload is RoClassicShopPayload moved && moved.FromPurchase) RemoveFromPurchase(moved.Index);
            };
            shopWin.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "itemshop.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "itemshop.y", w.Position.y);
            };

            RoClassicWindowLayout purchaseLayout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIMerchantItemPurchaseWnd").text);
            purchaseLayout.Ops.RemoveAll(op => op.Path != null && op.Path.EndsWith("itemwin_mid.bmp", StringComparison.OrdinalIgnoreCase));
            foreach (RoClassicChildLayout child in purchaseLayout.Children) {
                if (child.Class != "UIStaticText" || child.Y <= 100) continue;
                child.Fields["text"] = "Total";
                child.W = Math.Max(child.W, 160); // the capture sized it for "Total : 0 Zeny"
                child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text); // the captured "Total : 0 Zeny"
            }

            purchaseWin = new RoClassicWindow(system, purchaseLayout) { Visible = false, DragSourceAt = PurchaseDragSource };
            RoClassicMerchantList.Configure(purchaseWin, Prefs + "purchase", false, () => {
                purchaseFirst = Mathf.Clamp(purchaseFirst, 0, Math.Max(0, purchase.Count - PurchaseRows));
            });
            purchaseWin.DrawDynamic += DrawPurchase;
            purchaseWin.Clicked += child => {
                if (child.Id == OkId) OpenConfirm();
                else if (child.Id == CancelId) CloseCustomerShop(true);
            };
            purchaseWin.Wheeled += (w, local, delta) => {
                purchaseFirst = Mathf.Clamp(purchaseFirst + (delta > 0 ? -PurchaseRows : PurchaseRows), 0, Math.Max(0, purchase.Count - PurchaseRows));
                purchaseWin.Invalidate();
            };
            purchaseWin.Dropped += (w, local, payload) => {
                if (payload is RoClassicShopPayload moved && !moved.FromPurchase) AddToPurchase(moved.Index);
            };
            purchaseWin.RightClicked += (w, local) => {
                int r = PurchaseRowAt(local);
                if (r < 0) return;
                RoVendingShopItemSnapshot? s = ShopEntry(purchase[r].Index);
                if (s.HasValue) RoClassicItemDescription.Toggle(ShopItem(s.Value), metadata);
            };
            purchaseWin.EscClose = () => {
                CloseCustomerShop(true); // the manager sends SendMsg(6, 0xb5) to 0x2c
                return true;
            };
            purchaseWin.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "purchase.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "purchase.y", w.Position.y);
            };
        }

        private const int ItemShopWidth = 280;
        private int ItemShopRows => RoClassicMerchantList.Rows(shopWin.Height);
        private int PurchaseRows => RoClassicMerchantList.Rows(purchaseWin.Height);

        private void SizePurchase() {
            RoClassicMerchantList.SizeToItems(purchaseWin, purchase.Count, system.ClientSize);
            purchaseFirst = Mathf.Clamp(purchaseFirst, 0, Math.Max(0, purchase.Count - PurchaseRows));
        }

        private int AvailableAmount(RoVendingShopItemSnapshot item) {
            int selected = 0;
            foreach ((int index, int amount) in purchase) {
                if (index == item.Index) selected += amount;
            }
            return Math.Max(0, item.Amount - selected);
        }

        private void DrawItemShop(RoClassicWindow w) {
            // 0x8e33e0, a player's shop (+0xec = 0): "%s - %s" with MsgStr 0xe3 and the seller's name
            string shopName = RoClassicUiHost.MsgStr(0xe3, "Merchant Shop");
            string title = customerOwnerName.Length > 0 ? shopName + " - " + customerOwnerName : shopName;
            w.ClipTo(16, 2, w.Width - 32, 15);
            w.TextOut(17, 3, title, 0, 12, White);
            w.TextOut(16, 2, title, 0, 12, Black);
            w.ResetClip();
            RoVendingShopItemSnapshot[] items = customerShop.Items ?? Array.Empty<RoVendingShopItemSnapshot>();
            for (int row = 0; row < ItemShopRows && customerFirst + row < items.Length; row++) {
                int index = customerFirst + row;
                RoVendingShopItemSnapshot item = items[index];
                RoClassicMerchantList.DrawRow(w, row, ShopItem(item), metadata, AvailableAmount(item), item.Price, true, index == customerSelected);
            }
            w.DrawScrollBar(w.Width - 15, 14, ItemShopRows * Cell, Math.Max(0, items.Length - ItemShopRows), items.Length, ItemShopRows, customerFirst);
        }

        private static string FindActorName(uint aid) {
            if (aid == 0) return string.Empty;
            var world = UnityEngine.Object.FindFirstObjectByType<Ragnarok.World.RoOnlineWorldController>();
            if (world == null) return string.Empty;
            var actors = new List<Ragnarok.World.RoOnlineActorSnapshot>();
            world.SnapshotActors(actors);
            foreach (Ragnarok.World.RoOnlineActorSnapshot actor in actors) {
                if ((actor.ServerId == aid || actor.AccountId == aid) && !string.IsNullOrWhiteSpace(actor.Name)) return actor.Name;
            }

            return string.Empty;
        }

        private int ItemShopIndexAt(Vector2Int local) =>
            RoClassicMerchantList.RowAt(local, shopWin.Width, shopWin.Height, customerFirst, customerShop.Items?.Length ?? 0);

        private IRoClassicDragPayload ShopDragSource(Vector2Int local) {
            int k = ItemShopIndexAt(local);
            if (k < 0) return null;
            RoVendingShopItemSnapshot s = customerShop.Items[k];
            return new RoClassicShopPayload { FromPurchase = false, Index = s.Index, IconPath = RoClassicItemText.IconPath(ShopItem(s), metadata) };
        }

        /// <summary>
        /// 0x8e2ed0: one row per item to buy, the icon column at x 40, the name at y 29 + 32 r, the price line right-aligned
        /// at W − 18 (" %s Zeny"); the total in the static text at (12, 132).
        /// </summary>
        private void DrawPurchase(RoClassicWindow w) {
            int rows = PurchaseRows;
            for (int row = 0; row < rows && purchaseFirst + row < purchase.Count; row++) {
                RoVendingShopItemSnapshot? item = ShopEntry(purchase[purchaseFirst + row].Index);
                if (!item.HasValue) continue;
                RoClassicMerchantList.DrawRow(w, row, ShopItem(item.Value), metadata, purchase[purchaseFirst + row].Amount, item.Value.Price, false, false);
            }
            w.DrawScrollBar(w.Width - 15, 14, rows * Cell, Math.Max(0, purchase.Count - rows), purchase.Count, rows, purchaseFirst);
        }

        private int PurchaseRowAt(Vector2Int local) =>
            RoClassicMerchantList.RowAt(local, purchaseWin.Width, purchaseWin.Height, purchaseFirst, purchase.Count);

        private IRoClassicDragPayload PurchaseDragSource(Vector2Int local) {
            int r = PurchaseRowAt(local);
            if (r < 0) return null;
            RoVendingShopItemSnapshot? s = ShopEntry(purchase[r].Index);
            return s.HasValue ? new RoClassicShopPayload { FromPurchase = true, Index = purchase[r].Index, IconPath = RoClassicItemText.IconPath(ShopItem(s.Value), metadata) } : null;
        }

        /// <summary>Case 0x26: an item into the purchase list; a stack asks the amount (at most the stock and 30000).</summary>
        private void AddToPurchase(int index) {
            RoVendingShopItemSnapshot? found = ShopEntry(index);
            if (!found.HasValue) return;
            RoVendingShopItemSnapshot s = found.Value;
            int available = AvailableAmount(s);
            if (available <= 0) return;
            void Add(int amount) {
                amount = Math.Max(1, Math.Min(amount, Math.Min(available, 30000)));
                int at = purchase.FindIndex(p => p.Index == index);
                if (at >= 0) purchase[at] = (index, Math.Min(s.Amount, purchase[at].Amount + amount));
                else purchase.Add((index, amount));
                SizePurchase();
                UpdatePurchaseTotal();
                shopWin.Invalidate();
                purchaseWin.Invalidate();
            }

            if (available <= 1) {
                Add(1);
                return;
            }

            RoClassicCountWindow.Show(ShopItem(s).GetFullDisplayName(metadata), available, n => {
                if (n >= 1) Add((int) n);
            });
        }

        private void RemoveFromPurchase(int index) {
            purchase.RemoveAll(p => p.Index == index);
            SizePurchase();
            shopWin.Invalidate();
            UpdatePurchaseTotal();
            purchaseWin.Invalidate();
        }

        private long PurchaseTotal() {
            long total = 0;
            foreach ((int index, int amount) in purchase) {
                RoVendingShopItemSnapshot? s = ShopEntry(index);
                if (s.HasValue) total += (long) s.Value.Price * amount;
            }

            return total;
        }

        private void UpdatePurchaseTotal() => purchaseWin?.Set("Total", "Total : " + RoClassicItemText.Commas(PurchaseTotal()) + " Zeny");

        /// <summary>Buy (mode 0): the confirmation UIMerchantItemPurchaseMSGWnd 0xe6 (oracle 300 × 150, centred) with the total.</summary>
        private void OpenConfirm() {
            if (purchase.Count == 0) return;
            if (confirmWin == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIMerchantItemPurchaseMSGWnd").text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIStaticText" && child.Y == 40) child.Fields["text"] = "Cost";
                }

                confirmWin = new RoClassicWindow(system, layout) { Visible = false };
                confirmWin.Clicked += child => {
                    if (child.Id == OkId) _ = BuyAsync();
                    else if (child.Id == CancelId) CloseConfirm();
                };
                confirmWin.EscClose = () => {
                    CloseConfirm();
                    return true;
                };
            }

            confirmWin.Set("Cost", RoClassicItemText.Commas(PurchaseTotal()) + " Zeny");
            Vector2Int client = system.ClientSize;
            confirmWin.MoveTo(client.x / 2 - 150, client.y / 2 - 75);
            confirmWin.Visible = true;
            system.Windows.BringToFront(confirmWin);
            system.Windows.PushEsc(confirmWin);
        }

        private void CloseConfirm() {
            if (confirmWin == null || !confirmWin.Visible) return;
            confirmWin.Visible = false;
            system.Windows.RemoveEsc(confirmWin);
        }

        /// <summary>The confirmation's Buy: CZ_PC_PURCHASE_ITEMLIST_FROMMC2 with (amount, index) per row; the windows close.</summary>
        private async Task BuyAsync() {
            CloseConfirm();
            if (commands == null || purchase.Count == 0) return;
            var requests = new RoPlayerShopPurchaseRequest[purchase.Count];
            for (int i = 0; i < purchase.Count; i++) requests[i] = new RoPlayerShopPurchaseRequest((ushort) purchase[i].Amount, (ushort) purchase[i].Index);
            uint owner = customerShop.OwnerAid, uid = customerShop.UniqueId;
            CloseCustomerShop(true);
            RoAdvancedCommandResult result = await commands.PurchaseFromPlayerShopAsync(owner, uid, requests);
            if (!result.IsAccepted) Chat(result.Diagnostic);
        }

        /// <summary>Cancel / Esc / after Buy: 0x2b and 0x2c close; the shop stays closed until a new list comes.</summary>
        private void CloseCustomerShop(bool dismiss) {
            if (!customerShown) return;
            customerShown = false;
            if (dismiss) dismissedShops[customerShop.OwnerAid] = customerShop.ListSerial;
            CloseConfirm();
            if (shopWin != null) shopWin.Visible = false;
            if (purchaseWin != null) {
                purchaseWin.Visible = false;
                system.Windows.RemoveEsc(purchaseWin);
            }

            purchase.Clear();
        }

        // ================================================================ per frame

        public void ClassicUiTick(RoClassicUiSystem uiSystem) {
            if (setupShown && session != null) {
                int sequence = session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Cart);
                if (sequence != seenCartSequence) {
                    seenCartSequence = sequence;
                    RebuildMirror();
                }
            }

            RoClassicWindowManager windows = uiSystem.Windows;
            if (windows.Dragging != null) return;
            if (mirror != null && mirror.Visible && windows.HoverWindow == mirror) {
                Vector2Int local = windows.Mouse - mirror.Position;
                RoItemInstance item = MirrorItemAt(local);
                if (item != null) {
                    // vf28 0x8e9760: "%s [%d ea.] %d ea." (MsgStr 0xac4)
                    string text = string.Format(CultureInfo.InvariantCulture, "{0}: {1} ea.", item.GetFullDisplayName(metadata), item.Count);
                    RoClassicTooltip.Shared.Show(text, RoClassicItemText.TooltipColor(item, metadata),
                        mirror.Position.x + 40 + (local.x - 40) / Cell * Cell, mirror.Position.y + 1 + (local.y - 17) / Cell * Cell);
                }
            } else if (shopWin != null && shopWin.Visible && windows.HoverWindow == shopWin) {
                Vector2Int local = windows.Mouse - shopWin.Position;
                int k = ItemShopIndexAt(local);
                if (k >= 0) {
                    RoVendingShopItemSnapshot s = customerShop.Items[k];
                    string text = RoClassicUiHost.MsgStr(0xac5, "%s [%d ea.] %s Zeny");
                    text = ReplaceFirst(ReplaceFirst(ReplaceFirst(text, "%s", ShopItem(s).GetFullDisplayName(metadata)), "%d", AvailableAmount(s).ToString(CultureInfo.InvariantCulture)),
                        "%s", RoClassicItemText.Commas(s.Price));
                    RoClassicTooltip.Shared.Show(text, White, shopWin.Position.x + 70, shopWin.Position.y + 25 + (local.y - 17) / Cell * Cell);
                }
            } else if (myShop != null && myShop.Visible && windows.HoverWindow == myShop) {
                Vector2Int local = windows.Mouse - myShop.Position;
                int k = ShopIndexAt(local);
                if (k >= 0) {
                    // vf28 0x8e8c70 (sale): MsgStr 0xac5 "%s [%d ea.] %s Zeny"
                    RoVendingShopItemSnapshot s = shopItems[k];
                    RoItemInstance item = ShopItem(s);
                    string text = RoClassicUiHost.MsgStr(0xac5, "%s [%d ea.] %s Zeny");
                    text = ReplaceFirst(ReplaceFirst(ReplaceFirst(text, "%s", item.GetFullDisplayName(metadata)), "%d", s.Amount.ToString(CultureInfo.InvariantCulture)),
                        "%s", RoClassicItemText.Commas(s.Price));
                    RoClassicTooltip.Shared.Show(text, White, myShop.Position.x + 40 + (local.x - 40) / Cell * Cell, myShop.Position.y + 1 + (local.y - 17) / Cell * Cell);
                }
            }
        }

        private static string ReplaceFirst(string text, string token, string value) {
            int i = text.IndexOf(token, StringComparison.Ordinal);
            return i < 0 ? text : text.Substring(0, i) + value + text.Substring(i + token.Length);
        }

        // ================================================================ diagnostics (!vend ...)

        public static string Sweep(string command) {
            if ((command ?? string.Empty).Trim() == "boards") return RoClassicActorBoards.Describe();
            RoClassicVending v = instance;
            if (v == null) return "no classic vending";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            switch (a[0]) {
                case "title":
                    v.titleEdit?.SetText(a.Length > 1 ? string.Join(" ", a, 1, a.Length - 1) : string.Empty);
                    break;
                case "add":
                    // add <mirror index>: as if dragged into the setup
                    if (Arg(1) < v.mirrorList.Count) v.AddToSetup(v.mirrorList[Arg(1)]);
                    break;
                case "price":
                    // price <row> <zeny>
                    if (Arg(1) < v.entries.Count) {
                        v.entries[Arg(1)].Price = Arg(2).ToString(CultureInfo.InvariantCulture);
                        v.BindPriceEdits();
                    }

                    break;
                case "remove":
                    if (Arg(1) < v.entries.Count) v.RemoveFromSetup(v.entries[Arg(1)].Item);
                    break;
                case "ok":
                    _ = v.SetupOkAsync();
                    break;
                case "cancel":
                    _ = v.SetupCancelAsync();
                    break;
                case "close":
                    if (v.commands != null) _ = v.commands.CloseVendingStoreAsync();
                    break;
                case "pick":
                    // pick <shop item k> [amount]: as if dragged into the purchase list (a stack then answers the amount window)
                    if (v.customerShown && Arg(1) < v.customerShop.Items.Length) v.AddToPurchase(v.customerShop.Items[Arg(1)].Index);
                    break;
                case "unpick":
                    if (Arg(1) < v.purchase.Count) v.RemoveFromPurchase(v.purchase[Arg(1)].Index);
                    break;
                case "buy":
                    v.OpenConfirm();
                    break;
                case "confirm":
                    _ = v.BuyAsync();
                    break;
                case "shopcancel":
                    v.CloseCustomerShop(true);
                    break;
            }

            var s = new StringBuilder();
            s.Append("setup=").Append(v.setupShown).Append(" slots=").Append(v.slotLimit).Append(" title='").Append(v.titleEdit?.Text).Append("' rows=");
            foreach (Entry e in v.entries) s.Append(e.Item.ItemId).Append('x').Append(e.Amount).Append('@').Append(e.Price).Append(' ');
            s.Append("mirror=");
            foreach (RoItemInstance item in v.mirrorList) s.Append(item.ItemId).Append('x').Append(item.Count).Append(' ');
            s.Append("customer=").Append(v.customerShown ? v.customerShop.OwnerAid + "#" + v.customerShop.ListSerial : "-").Append(" purchase=");
            foreach ((int index, int amount) in v.purchase) s.Append(index).Append('x').Append(amount).Append(' ');
            s.Append("total=").Append(v.customerShown ? v.PurchaseTotal() : 0).Append(" confirm=").Append(v.confirmWin != null && v.confirmWin.Visible).Append(' ');
            s.Append("shop=").Append(v.myShop != null && v.myShop.Visible).Append(' ');
            foreach (RoVendingShopItemSnapshot item in v.shopItems) s.Append(item.ItemId).Append('x').Append(item.Amount).Append('@').Append(item.Price).Append(' ');
            return s.ToString().TrimEnd();
        }
    }
}
