using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>An inventory item carried by a classic drag (to the hotkey bar, a legacy window, the ground).</summary>
    internal sealed class RoClassicItemPayload : IRoClassicDragPayload {
        public object Owner;
        public RoItemInstance Item;
        public string IconPath { get; set; }
    }

    /// <summary>
    /// The inventory, the original client's UIItemWnd (window id 8) from its reverse-engineered OnCreate / msg 0xe sizing /
    /// OnDraw 0x8e2460 / mouse handlers (reference ui-static ui/RUNTIME-RULES-ITEMS.md §1), with the drop-count window
    /// UIItemDropCntWnd (id 15, oracle layout) and the shared item tooltip. Opened by the menu icon / Alt+E; the open
    /// window is rebuilt from the session's inventory sequence by RoClassicGameUi, which also follows the card compound
    /// list (UIItemCompositionWnd 0x4a) while the window is closed. The item rules and requests are
    /// <see cref="RoInventoryModel"/>.
    /// </summary>
    public sealed class RoClassicInventory {
        private static readonly Unity.Profiling.ProfilerMarker RefreshMarker = new Unity.Profiling.ProfilerMarker("RO.UI.Inventory.Refresh");
        private const string ClassicUi = "유저인터페이스\\";
        private const string ClassicBasic = ClassicUi + "basic_interface\\";
        private const string ClassicInventoryUi = ClassicUi + "inventory\\";
        private const string ClassicItemIcons = ClassicUi + "item\\";
        private const string ClassicPrefs = "ro.ui.classic.item.";
        private const int ClassicHeight = 255;              // always 6 rows: max(clamp(h, 102, 240), 217 + 38)
        private const int ClassicTitleHeight = 17;
        private const int ClassicCell = 32;
        private const int ClassicRows = 6;
        private const int ClassicTabExtent = 55;            // vTextH("Use") 45 + pad 10, tabs overlap by 1
        private const int ClassicMaxItems = 100;
        private const int ClassicSysBaseId = 584;
        private const int ClassicMiniId = 198;
        private const int ClassicCloseId = 197;
        private const int ClassicDropLockId = 209;
        private const int ClassicCompareId = 452;
        private const int ClassicExpandId = 541;
        private const int ClassicDealLockId = 503;
        private const int ClassicAlignId = 360;
        private static readonly string[] ClassicTabLabels = { "Use", "Eqp", "Etc", "Fav" };   // MsgStr 0x5b9 0x802 0x5bf 0x803
        private static readonly string[] ClassicButtonStates = { "normal", "hover", "pressed" };
        private static readonly Color32 ClassicBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 ClassicWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ClassicTabBorder = new Color32(0xc3, 0xc3, 0xc3, 255);
        private static readonly Color32 ClassicFavFill = new Color32(0xce, 0xde, 0xff, 255);
        private static readonly Color32 ClassicLockFill = new Color32(0xdd, 0xe7, 0xf9, 255);   // colorchip (0,8)
        private static readonly Color32 ClassicFrameLine = new Color32(0xc0, 0xc0, 0xc0, 255);
        private static readonly Color32 ClassicCountColor = new Color32(0x23, 0x23, 0x23, 255);
        private static readonly Color32 ClassicFull = new Color32(255, 0, 0, 255);
        private static readonly Color32[] ClassicEnchantColors = {
            new Color32(255, 255, 255, 255), new Color32(0xad, 0xff, 0x2f, 255), new Color32(0x00, 0xbf, 0xff, 255),
            new Color32(0xff, 0xa5, 0x00, 255), new Color32(0x94, 0x00, 0xd3, 255),
        };

        private static RoClassicInventory instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private readonly RoInventoryModel model = new RoInventoryModel();
        private readonly List<RoItemInstance> classicList = new List<RoItemInstance>(64);
        private readonly List<RoItemInstance> classicTargets = new List<RoItemInstance>(8);
        private RoClassicUiSystem system;
        private RoClassicWindow classicInv;
        private RoClassicChildLayout classicMini;
        private RoClassicChildLayout classicClose;
        private RoClassicChildLayout classicDropLock;
        private RoClassicChildLayout classicCompare;
        private RoClassicChildLayout classicExpand;
        private RoClassicChildLayout classicDealLock;
        private RoClassicChildLayout classicAlign;
        private int seenSequence = int.MinValue;
        private RoItemMetadataTable seenMetadata;
        private bool dropLocked;
        private bool compareEnabled;   // DAT_0155353e: off at every start, never saved
        private bool dealLocked;
        private int classicWidth = 280;
        private int classicTab;
        private int classicFirstRow;
        private int classicTotalCount;
        private bool classicMinimized;
        private bool classicFavSorted;
        private bool classicResizing;
        private int classicResizeStartX;
        private int classicResizeStartW;
        // the hovered item's tooltip text, kept while the same item stays under the mouse
        private RoItemInstance tipItem;
        private string tipText;

        private RoClassicInventory() { }

        public static bool IsOpen => instance?.classicInv != null && instance.classicInv.Visible;

        private int ClassicCols => (classicWidth - 40) / ClassicCell;
        private int ClassicInvHeight => classicMinimized ? ClassicTitleHeight : ClassicHeight;

        /// <summary>The menu icon / Alt+E: opens or closes the window.</summary>
        public static void Toggle(RoLoginShellSession loginSession) {
            if (IsOpen) {
                instance.Hide();
                return;
            }

            Open(loginSession);
        }

        /// <summary>MakeWindow 8: shows the window rebuilt from the session.</summary>
        public static void Open(RoLoginShellSession loginSession) {
            RoClassicInventory t = Ensure(loginSession);
            if (t == null) return;
            try {
                t.EnsureClassicInventory();
                t.seenSequence = loginSession.InventoryWindowChangeSequence;
                if (!t.classicInv.Visible) {
                    t.classicInv.Visible = true;
                    t.system.Windows.BringToFront(t.classicInv);
                }

                t.Refresh();
            } catch (Exception e) {
                Debug.LogException(e);
                t.Hide();
            }
        }

        /// <summary>
        /// Leaving the map: the window closes and its locks, the Fav order and the scroll are forgotten. The compare box stays
        /// (a client-wide flag, as RoClassicItemDescription.CompareEquips it sets).
        /// </summary>
        public static void Close() {
            RoClassicInventory t = instance;
            if (t == null) return;
            t.Hide();
            t.dropLocked = false;
            t.dealLocked = false;
            t.classicFavSorted = false;
            t.classicFirstRow = 0;
            t.tipItem = null;
            t.tipText = null;
            t.model.DismissedCardCompositionSequence = int.MinValue;
            t.seenSequence = int.MinValue;
            if (t.classicInv != null) t.ApplyClassicChildStates();
        }

        /// <summary>
        /// Per frame (RoClassicGameUi): when the session's inventory sequence (or the item table) moved, the card compound
        /// list follows it and the open window is rebuilt; the open window's resizer and hover tooltip.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession, RoItemMetadataTable metadata) {
            if (loginSession == null) return;
            RoClassicInventory t = Ensure(loginSession);
            if (t == null) return;
            t.model.Metadata = metadata ?? RoItemMetadataTable.Empty;
            int sequence = loginSession.InventoryWindowChangeSequence;
            if (sequence != t.seenSequence || !ReferenceEquals(t.model.Metadata, t.seenMetadata)) {
                t.seenSequence = sequence;
                t.seenMetadata = t.model.Metadata;
                t.Refresh();
            }

            if (t.classicInv != null && t.classicInv.Visible) t.UpdateClassicInventory();
        }

        private static RoClassicInventory Ensure(RoLoginShellSession loginSession) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || loginSession == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicInventory { system = system };
            instance.model.Bind(loginSession);
            return instance;
        }

        /// <summary>A fresh snapshot: the card compound list, and the open window's display list and redraw.</summary>
        private void Refresh() {
            using Unity.Profiling.ProfilerMarker.AutoScope refreshScope = RefreshMarker.Auto();
            RoLoginShellSession session = model.Session;
            RoGameplayInventorySnapshot gameplay = session != null ? session.InventorySnapshot() : RoGameplayInventorySnapshot.Empty;
            RoItemInstance[] items = gameplay.InventoryItems ?? Array.Empty<RoItemInstance>();
            SyncCardComposition(gameplay.CardComposition, items);
            if (classicInv == null || !classicInv.Visible) return;
            tipItem = null;
            RebuildClassicList(items);
            classicInv.Invalidate();
        }

        private void Hide() {
            HideClassicDropCount();
            classicResizing = false;
            if (classicInv != null) classicInv.Visible = false;
        }

        private void EnsureClassicInventory() {
            if (classicInv != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIItemWnd", WindowId = 8, Width = 312, Height = ClassicHeight };
            layout.Children.Add(InvButton(ClassicSysBaseId, "sysbase", 11, 11, ClassicBasic + "sys_base_off.bmp", ClassicBasic + "sys_base_on.bmp", ClassicBasic + "sys_base_on.bmp"));
            classicMini = InvButton(ClassicMiniId, "mini", 11, 11, ClassicBasic + "sys_mini_off.bmp", ClassicBasic + "sys_mini_on.bmp", ClassicBasic + "sys_mini_on.bmp");
            classicClose = InvButton(ClassicCloseId, "close", 11, 11, ClassicBasic + "sys_close_off.bmp", ClassicBasic + "sys_close_on.bmp", ClassicBasic + "sys_close_on.bmp");
            classicDropLock = InvButton(ClassicDropLockId, "droplock", 14, 11, null, null, null);
            classicCompare = InvButton(ClassicCompareId, "compare", 12, 12, null, null, null);
            classicExpand = InvButton(ClassicExpandId, "expand", 11, 14, ClassicInventoryUi + "icon_num.bmp", ClassicInventoryUi + "icon_num.bmp", ClassicInventoryUi + "icon_num.bmp");
            classicDealLock = InvButton(ClassicDealLockId, "deallock", 26, 17, null, null, null);
            classicAlign = InvButton(ClassicAlignId, "align", 25, 17, ClassicInventoryUi + "bt_sort.bmp", ClassicInventoryUi + "bt_sort_over.bmp", ClassicInventoryUi + "bt_sort_press.bmp");
            foreach (RoClassicChildLayout b in new[] { classicMini, classicClose, classicDropLock, classicCompare, classicExpand, classicDealLock, classicAlign }) layout.Children.Add(b);

            classicInv = new RoClassicWindow(system, layout) { Visible = false, DragSourceAt = ClassicInvDragSource };
            classicInv.DrawDynamic += DrawClassicInventory;
            classicInv.Clicked += OnClassicInvClicked;
            classicInv.Pressed += OnClassicInvPressed;
            classicInv.DoubleClicked += OnClassicInvDoubleClicked;
            classicInv.RightClicked += OnClassicInvRightClicked;
            classicInv.Wheeled += (window, local, delta) => ScrollClassicInventory(delta > 0 ? -Math.Min(ClassicRows - 1, 3) : Math.Min(ClassicRows - 1, 3));
            classicInv.DraggedOutside += OnClassicInvDraggedOutside;
            classicInv.Dropped += (window, local, payload) => {
                // a worn item dragged out of the equipment window and released here is taken off
                if (payload is RoClassicItemPayload worn && worn.Item != null && worn.Item.IsEquipped) model.Unequip(worn.Item.BagIndex);

                // a storage item is taken out (count window mode 6)
                if (payload is RoClassicStoragePayload stored) RoClassicStorage.TakeItem(stored.Item);

                // a RODEX attachment comes back (Compose Msg 0x68 → CZ_REQ_REMOVE_RODEX_ITEM)
                if (payload is RoClassicRodexPayload mailed) RoClassicRodex.RemoveAttachment(mailed);

                // a cart item comes back (CZ_MOVE_ITEM_FROM_CART_TO_BODY)
                if (payload is RoClassicCartPayload carted) RoClassicCart.TakeItem(carted.Item, false);
            };
            classicInv.Moved += window => {
                PlayerPrefs.SetInt(ClassicPrefs + "x", window.Position.x);
                PlayerPrefs.SetInt(ClassicPrefs + "y", window.Position.y);
            };

            // msg 0x22: the stored state (ITEMWNDINFO), or (0, 120) 280 wide on the first tab
            classicWidth = ClassicSnapWidth(PlayerPrefs.GetInt(ClassicPrefs + "w", 280));
            classicTab = Mathf.Clamp(PlayerPrefs.GetInt(ClassicPrefs + "tab", 0), 0, 3);
            classicMinimized = PlayerPrefs.GetInt(ClassicPrefs + "min", 0) != 0;
            Vector2Int screen = system.ClientSize;
            int x = PlayerPrefs.GetInt(ClassicPrefs + "x", 0), y = PlayerPrefs.GetInt(ClassicPrefs + "y", 120);
            if (x < 0 || y < 0 || x > screen.x - classicWidth || y > screen.y - ClassicHeight) {
                x = 0;
                y = 120;
            }

            ApplyClassicInventorySize();
            classicInv.MoveTo(x, y);
        }

        private static RoClassicChildLayout InvButton(int id, string path, int w, int h, string normal, string hover, string pressed) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, W = w, H = h };
            if (normal != null) {
                button.States["normal"] = normal;
                button.States["hover"] = hover;
                button.States["pressed"] = pressed;
            }

            return button;
        }

        /// <summary>msg 0xe: the width snaps to 248, 280 or 312 (6, 7 or 8 columns), toward 280.</summary>
        private static int ClassicSnapWidth(int w) {
            w = Mathf.Clamp(w, 232, 320) - 280;
            return w / ClassicCell * ClassicCell + 280;
        }

        /// <summary>Resize + child placement of msg 0xe (and the minimized title-bar-only state).</summary>
        private void ApplyClassicInventorySize() {
            int w = classicWidth, h = ClassicHeight;
            classicInv.Layout.Width = w;   // clamping and snapping use the current width
            classicInv.SetHeight(ClassicInvHeight);
            classicInv.HitArea = new RectInt(0, 0, w, ClassicInvHeight);
            classicInv.SetChildPosition("sysbase", 3, 3);
            classicInv.SetChildPosition("mini", w - 28, 3);
            classicInv.SetChildPosition("close", w - 14, 3);
            classicInv.SetChildPosition("droplock", 79, h - 16);
            classicInv.SetChildPosition("compare", 103, h - 17);
            classicInv.SetChildPosition("expand", 23, h - 17);
            classicInv.SetChildPosition("deallock", 124, h - 19);
            classicInv.SetChildPosition("align", 157, h - 19);
            ApplyClassicChildStates();
            UpdateClassicScroll();
            PlayerPrefs.SetInt(ClassicPrefs + "w", w);
            PlayerPrefs.SetInt(ClassicPrefs + "min", classicMinimized ? 1 : 0);
            classicInv.Invalidate();
        }

        /// <summary>Check box bitmaps follow their state; NPC-sale lock and Align exist on the Fav tab only.</summary>
        private void ApplyClassicChildStates() {
            string drop = ClassicInventoryUi + (dropLocked ? "item_drop_lock_on.bmp" : "item_drop_lock_off.bmp");
            string compare = ClassicInventoryUi + (compareEnabled ? "item_compare_on.bmp" : "item_compare_off.bmp");
            foreach (string state in ClassicButtonStates) {
                classicDropLock.States[state] = drop;
                classicCompare.States[state] = compare;
            }

            string deal = dealLocked ? "bt_itemDeal_lock_on" : "bt_itemDeal_lock_off";
            classicDealLock.States["normal"] = ClassicInventoryUi + deal + ".bmp";
            classicDealLock.States["hover"] = ClassicInventoryUi + (dealLocked ? "bt_itemDeal_lock_on_over.bmp" : "bt_itemDeal_lock_over.bmp");
            classicDealLock.States["pressed"] = ClassicInventoryUi + (dealLocked ? "bt_itemDeal_lock_on_press.bmp" : "bt_itemDeal_lock_press.bmp");
            classicInv.SetChildVisible(classicDealLock, classicTab == 3);
            classicInv.SetChildVisible(classicAlign, classicTab == 3);
            classicInv.Invalidate();
        }

        // ---- the card compound list (UIItemCompositionWnd 0x4a) ----

        /// <summary>
        /// ZC_ITEMCOMPOSITION_LIST: the equipment the card can go into is offered in RoClassicItemSelect; a pick sends
        /// CZ_REQ_ITEMCOMPOSITION, a close forgets the list. A list that is over, dismissed, or whose card is gone hides it.
        /// </summary>
        private void SyncCardComposition(RoCardCompositionSnapshot composition, RoItemInstance[] items) {
            composition ??= RoCardCompositionSnapshot.Empty;
            if (!composition.IsActive ||
                composition.TargetBagIndexes == null ||
                composition.TargetBagIndexes.Length == 0 ||
                composition.Sequence == model.DismissedCardCompositionSequence) {
                RoClassicItemSelect.Hide(RoClassicItemSelect.Composition);
                return;
            }

            if (!RoInventoryModel.TryFindItemByBagIndex(items, composition.CardBagIndex, out RoItemInstance card) ||
                !RoInventoryModel.IsCardItem(card, card.ResolveMetadata(model.Metadata))) {
                return;
            }

            classicTargets.Clear();
            for (int i = 0; i < composition.TargetBagIndexes.Length; i++) {
                if (RoInventoryModel.TryFindItemByBagIndex(items, composition.TargetBagIndexes[i], out RoItemInstance target)) classicTargets.Add(target);
            }

            int sequence = composition.Sequence;
            int cardBagIndex = composition.CardBagIndex;
            RoClassicItemSelect.Show(RoClassicItemSelect.Composition, sequence, classicTargets, model.Metadata, picked => {
                model.DismissedCardCompositionSequence = sequence;
                model.InsertCard(cardBagIndex, picked.BagIndex);
            }, () => model.CancelCardComposition(sequence));
        }

        // ---- display list (msg 0x17) ----

        /// <summary>
        /// The current tab's items in inventory order: Use = healing / usable / delayed / cash, Eqp = weapons and
        /// armour, Etc = the rest (cards, ammunition, eggs, misc); favourites only on Fav, sorted by kind then name
        /// once "Align items" was pressed. Worn items are not listed; hatched eggs are skipped when drawn.
        /// </summary>
        private void RebuildClassicList(RoItemInstance[] items) {
            classicList.Clear();
            classicTotalCount = 0;
            foreach (RoItemInstance item in items) {
                if (item == null || item.BagIndex <= 0) continue;
                classicTotalCount++;                          // the count line includes what is worn
                if (item.IsEquipped) continue;
                if (item.IsFavorite) {
                    if (classicTab == 3) classicList.Add(item);
                    continue;
                }

                if (model.TabOf(item) == classicTab) classicList.Add(item);
            }

            if (classicTab == 3 && classicFavSorted) {
                // stable merge sort by kind rank, then display name (byte compare)
                var keyed = new List<(int Rank, string Name, int Order, RoItemInstance Item)>(classicList.Count);
                for (int i = 0; i < classicList.Count; i++) {
                    RoItemInstance it = classicList[i];
                    keyed.Add((model.SortRank(it), it.GetFullDisplayName(model.Metadata) ?? string.Empty, i, it));
                }

                keyed.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : string.CompareOrdinal(a.Name, b.Name) is int c && c != 0 ? c : a.Order.CompareTo(b.Order));
                classicList.Clear();
                foreach (var k in keyed) classicList.Add(k.Item);
            }

            UpdateClassicScroll();
        }

        /// <summary>UpdateScroll 0x8eb870: rows beyond the six in view; the first row stays in range.</summary>
        private void UpdateClassicScroll() {
            int total = (classicList.Count + ClassicCols - 1) / ClassicCols;
            int range = Math.Max(0, total - ClassicRows);
            classicFirstRow = range == 0 ? 0 : Mathf.Clamp(classicFirstRow, 0, total - ClassicRows);
        }

        private void ScrollClassicInventory(int rows) {
            int total = (classicList.Count + ClassicCols - 1) / ClassicCols;
            if (total <= ClassicRows) return;
            int first = Mathf.Clamp(classicFirstRow + rows, 0, total - ClassicRows);
            if (first == classicFirstRow) return;
            classicFirstRow = first;
            classicInv.Invalidate();
        }

        // ---- drawing (OnDraw 0x8e2460, the tab control, scroll bar and resizer) ----

        private void DrawClassicInventory(RoClassicWindow w) {
            int W = classicWidth, H = ClassicHeight;
            // title bar 0x82f2d0
            w.DrawBitmapRaw(0, 0, ClassicBasic + "titlebar_left.bmp");
            w.DrawBitmapRaw(W - 12, 0, ClassicBasic + "titlebar_right.bmp");
            for (int k = 0, n = (W - 24 + 11) / 12; k < n; k++) w.DrawBitmapRaw(12 + 12 * k, 0, ClassicBasic + "titlebar_mid.bmp");
            w.TextOut(19, 4, "Inventory", 0, 12, ClassicWhite);   // MsgStr 0x6a
            w.TextOut(18, 3, "Inventory", 0, 12, ClassicBlack);
            if (classicMinimized) return;

            bool locked = classicTab == 3 && dealLocked;
            int n8 = (H - 38 + 7) / 8;
            for (int k = 0; k < n8; k++) w.DrawBitmapRaw(0, 17 + 8 * k, ClassicBasic + "itemwin_left.bmp");
            for (int k = 0; k < n8; k++) w.DrawBitmapRaw(W - 20, 17 + 8 * k, ClassicBasic + "itemwin_right.bmp");
            if (locked) {
                w.FillRect(20, 17, W - 21, H - 38, ClassicLockFill);
            } else {
                w.FillRect(20, 17, (W - 60) % ClassicCell, H - 38, ClassicWhite);
                w.FillRect(20, ClassicRows * ClassicCell + 17, W - 24, (H - 38) % ClassicCell, ClassicWhite);
            }

            // button bar
            w.DrawBitmapRaw(0, H - 21, ClassicBasic + "btnbar_left.bmp");
            w.DrawBitmapRaw(W - 21, H - 21, ClassicBasic + "btnbar_right.bmp");
            for (int k = 0, n = (W - 42 + 20) / 21; k < n; k++) w.DrawBitmapRaw(21 + 21 * k, H - 21, ClassicBasic + "btnbar_mid.bmp");
            w.FillRect(20, 17, 1, H - 38, ClassicFrameLine);
            w.FillRect(18, H - 21, 2, 1, ClassicWhite);

            int cols = ClassicCols;
            string cell = locked ? ClassicInventoryUi + "itemwin_mid_lock.bmp" : ClassicBasic + "itemwin_mid.bmp";
            for (int r = 0; r < ClassicRows; r++) {
                for (int c = 0; c < cols; c++) w.DrawBitmapRaw(40 + ClassicCell * c, 17 + ClassicCell * r, cell);
            }

            RoClassicGdiFont amountFont = w.FontFor(0, 11);
            int drawn = 0, skip = classicFirstRow * cols;
            for (int j = 0; j < classicList.Count && drawn < ClassicRows * cols; j++) {
                if (j < skip) continue;
                RoItemInstance item = classicList[j];
                if (RoInventoryModel.IsHatchedEgg(item)) continue;
                int ix = 40 + ClassicCell * (drawn % cols) + 4, iy = 17 + ClassicCell * (drawn / cols) + 4;
                string icon = ClassicIconPath(item);
                if (icon != null && !w.DrawBitmap(ix, iy, icon)) w.DrawBitmap(ix, iy, ClassicItemIcons + "am_berserkpitcher.bmp");
                if (item.Count != 0 && item.Count != -1) {
                    string amount = item.Count.ToString(CultureInfo.InvariantCulture);
                    int x = ix + (item.Count < 1000 ? 14 : 0);
                    w.TextOut(x + 1, iy + 15, amount, 0, 11, ClassicWhite);
                    w.TextOut(x - 1, iy + 15, amount, 0, 11, ClassicWhite);
                    w.TextOut(x, iy + 16, amount, 0, 11, ClassicWhite);
                    w.TextOut(x, iy + 14, amount, 0, 11, ClassicWhite);
                    w.TextOut(x, iy + 15, amount, 0, 11, ClassicBlack);
                }

                if (item.EnchantGrade > 0) w.DrawBitmap(ix, iy + 12, ClassicUi + "grade_enchant\\grade_icon" + item.EnchantGrade.ToString(CultureInfo.InvariantCulture) + ".bmp");
                drawn++;
            }

            // the count line: everything held (worn included) against the limit
            string have = classicTotalCount.ToString(CultureInfo.InvariantCulture);
            w.TextOut(35, H - 15, have, 0, 11, classicTotalCount >= ClassicMaxItems ? ClassicFull : ClassicCountColor);
            w.TextOut(35 + amountFont.Measure(have) + 1, H - 15, "/" + ClassicMaxItems.ToString(CultureInfo.InvariantCulture), 0, 11, ClassicBlack);

            DrawClassicTabs(w);
            int total = (classicList.Count + cols - 1) / cols;
            w.DrawScrollBar(W - 15, 17, H - 38, Math.Max(0, total - ClassicRows), total, ClassicRows, classicFirstRow);
            w.DrawBitmap(W - 15, H - 15, ClassicUi + "btn_resize.bmp");
        }

        /// <summary>
        /// UITabControl style 0 (vertical, 0x7e09e0) at (3, 18), 18 wide, tabs 55 tall overlapping by 1: the fill
        /// (#cedeff for Fav), a #c3c3c3 outline open on the right (the selected tab sits 1 px further left, the others
        /// get a closing line), the label one character per 15 px line centred in the tab, font 0 height 12, black.
        /// </summary>
        private void DrawClassicTabs(RoClassicWindow w) {
            const int ox = 3, oy = 18, tw = 18, th = ClassicTabExtent;
            RoClassicGdiFont font = w.FontFor(0, 12);
            w.ClipTo(ox, oy, tw, ClassicTabLabels.Length * (th - 1) + 1);
            for (int i = 0; i < ClassicTabLabels.Length; i++) {
                bool selected = i == classicTab;
                int x0 = ox + (selected ? 0 : 1), y0 = oy + (th - 1) * i;
                w.FillRect(x0 + 1, y0 + 1, tw - 1, th - 2, i == 3 ? ClassicFavFill : ClassicWhite);
                w.FillRect(x0 + 2, y0, tw - 2, 1, ClassicTabBorder);
                w.FillRect(x0 + 1, y0 + 1, 1, 1, ClassicTabBorder);
                w.FillRect(x0, y0 + 2, 1, th - 4, ClassicTabBorder);
                w.FillRect(x0 + 1, y0 + th - 2, 1, 1, ClassicTabBorder);
                w.FillRect(x0 + 2, y0 + th - 1, tw - 2, 1, ClassicTabBorder);
                if (!selected) w.FillRect(ox + tw - 1, y0, 1, th, ClassicTabBorder);

                string label = ClassicTabLabels[i];
                int widest = 0;
                foreach (char ch in label) widest = Math.Max(widest, font.Measure(ch.ToString()));
                int textH = 15 * label.Length;
                int x = ox + (tw - 12) / 2 + (selected ? 0 : 1), y = y0 + (th - textH) / 2 + (th - textH) % 2;
                foreach (char ch in label) {
                    string s = ch.ToString();
                    w.TextOut(x + (widest - font.Measure(s)) / 2, y, s, 0, 12, ClassicBlack);
                    y += 15;
                }
            }

            w.ResetClip();
        }

        private string ClassicIconPath(RoItemInstance item) {
            RoItemMetadata metadata = item.ResolveMetadata(model.Metadata);
            return metadata != null ? ClassicItemIcons + metadata.GetResourceName(item.IsIdentified) + ".bmp" : null;
        }

        // ---- mouse ----

        /// <summary>Cell hit test 0x8d5540: 40 ≤ x &lt; W − 20, 17 ≤ y &lt; rows·32 + 17, over the scrolled list.</summary>
        private RoItemInstance ClassicItemAt(Vector2Int local) {
            if (classicMinimized || local.x < 40 || local.x >= classicWidth - 20 || local.y < 17 || local.y >= ClassicRows * ClassicCell + 17) return null;
            int cols = ClassicCols;
            int cell = cols * ((local.y - 17) / ClassicCell) + (local.x - 40) / ClassicCell;
            int drawn = 0;
            for (int j = classicFirstRow * cols; j < classicList.Count; j++) {
                if (RoInventoryModel.IsHatchedEgg(classicList[j])) continue;
                if (drawn++ == cell) return classicList[j];
            }

            return null;
        }

        /// <summary>LBtnDown on an item: the drag starts at once (no threshold); Shift with a text field focused links it.</summary>
        private IRoClassicDragPayload ClassicInvDragSource(Vector2Int local) {
            RoItemInstance item = ClassicItemAt(local);
            if (item == null) return null;
            HideClassicDropCount();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.shiftKey.isPressed && item.IsIdentified && RoClassicTextInput.HasFocus) {
                RoClassicChat.Instance?.InsertItemLink(item);
            }

            string icon = ClassicIconPath(item);
            return new RoClassicItemPayload { Owner = this, Item = item, IconPath = icon };
        }

        private void OnClassicInvPressed(RoClassicWindow window, Vector2Int local) {
            if (classicMinimized) return;
            int W = classicWidth, H = ClassicHeight;
            // the vertical tab control (msg 0x16 → rebuild)
            if (local.x >= 3 && local.x < 21 && local.y >= 18 && local.y < 18 + ClassicTabLabels.Length * (ClassicTabExtent - 1) + 1) {
                int tab = Math.Min(ClassicTabLabels.Length - 1, (local.y - 18) / (ClassicTabExtent - 1));
                if (tab != classicTab) {
                    classicTab = tab;
                    classicFirstRow = 0;
                    PlayerPrefs.SetInt(ClassicPrefs + "tab", tab);
                    ApplyClassicChildStates();
                    Refresh();
                }

                return;
            }

            // the resizer: only the width follows the drag
            if (local.x >= W - 15 && local.x < W - 2 && local.y >= H - 15 && local.y < H - 2) {
                classicResizing = true;
                classicResizeStartX = system.Windows.Mouse.x;
                classicResizeStartW = classicWidth;
                return;
            }

            // the scroll bar: arrows a row, the track a page
            int total = (classicList.Count + ClassicCols - 1) / ClassicCols, range = total - ClassicRows;
            if (range > 0 && local.x >= W - 15 && local.x < W - 2 && local.y >= 17 && local.y < H - 21) {
                int by = local.y - 17, barH = H - 38;
                if (by < 13) {
                    ScrollClassicInventory(-1);
                } else if (by >= barH - 13) {
                    ScrollClassicInventory(1);
                } else {
                    int thumb = Math.Max(8, (barH - 26) * ClassicRows / total);
                    int top = 13 + classicFirstRow * (barH - 26 - thumb) / range;
                    ScrollClassicInventory(by < top ? -(ClassicRows - 1) : by >= top + thumb ? ClassicRows - 1 : 0);
                }
            }
        }

        private void OnClassicInvDoubleClicked(RoClassicWindow window, Vector2Int local) {
            if (local.y < ClassicTitleHeight) {
                ToggleClassicMinimized();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.shiftKey.isPressed) return;
            RoItemInstance item = ClassicItemAt(local);
            if (item != null) _ = model.ActivateAsync(item.BagIndex); // use / equip / card compound by type
        }

        /// <summary>RBtnDown: the item description window (window 12, msg 0x18).</summary>
        private void OnClassicInvRightClicked(RoClassicWindow window, Vector2Int local) {
            RoItemInstance item = ClassicItemAt(local);
            if (item != null) RoClassicItemDescription.Toggle(item, model.Metadata);
        }

        private void OnClassicInvClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case ClassicCloseId:
                    Hide();
                    break;
                case ClassicMiniId:
                    ToggleClassicMinimized();
                    break;
                case ClassicDropLockId:
                    dropLocked = !dropLocked;
                    ApplyClassicChildStates();
                    break;
                case ClassicCompareId:
                    compareEnabled = !compareEnabled;
                    RoClassicItemDescription.CompareEquips = compareEnabled;   // 0x8f0930: only the flag
                    ApplyClassicChildStates();
                    break;
                case ClassicDealLockId:
                    dealLocked = !dealLocked;
                    ApplyClassicChildStates();
                    break;
                case ClassicAlignId:
                    classicFavSorted = true;
                    Refresh();
                    break;
            }
        }

        private void ToggleClassicMinimized() {
            int before = ClassicInvHeight;
            classicMinimized = !classicMinimized;
            ApplyClassicInventorySize();
            if (ClassicInvHeight != before) classicInv.Invalidate();
        }

        /// <summary>
        /// A drag released outside every classic window drops the item on the world (CGameMode msg 0x11): at once when
        /// it is a single piece, else through the drop-count window. "Lock item drop" blocks the ground.
        /// </summary>
        private void OnClassicInvDraggedOutside(RoClassicWindow window, IRoClassicDragPayload payload) {
            if (!(payload is RoClassicItemPayload carried) || carried.Owner != this || carried.Item == null) return;
            RoItemInstance item = carried.Item;
            if (dropLocked || !model.CanSendCommand) return;
            if (item.Count <= 1) model.Drop(item.BagIndex, 1);
            else ShowClassicDropCount(item);
        }

        // ---- the drop-count window UIItemDropCntWnd (id 15) ----

        /// <summary>Drop mode: OK with 0 &lt; n ≤ the amount drops n (CGameMode msg 0x11).</summary>
        private void ShowClassicDropCount(RoItemInstance item) {
            RoClassicCountWindow.Show(item.GetFullDisplayName(model.Metadata), item.Count, n => {
                if (n <= 0 || n > item.Count) return;
                model.Drop(item.BagIndex, (int) n);
            });
        }

        private static void HideClassicDropCount() => RoClassicCountWindow.Hide();

        // ---- per frame: hover tooltip, resizer ----

        private void UpdateClassicInventory() {
            RoClassicWindowManager windows = system.Windows;
            if (windows == null) return;
            Mouse mouse = Mouse.current;
            if (classicResizing) {
                if (mouse == null || !mouse.leftButton.isPressed) {
                    classicResizing = false;
                } else {
                    int width = ClassicSnapWidth(classicResizeStartW + windows.Mouse.x - classicResizeStartX);
                    if (width != classicWidth) {
                        classicWidth = width;
                        ApplyClassicInventorySize();
                    }
                }

                return;
            }

            if (windows.HoverWindow != classicInv || windows.Dragging != null) return;
            Vector2Int local = windows.Mouse - classicInv.Position;
            RoItemInstance item = ClassicItemAt(local);
            if (item == null) return;
            if (!ReferenceEquals(item, tipItem)) {
                tipItem = item;
                tipText = ClassicTooltipText(item);
            }

            RoClassicTooltip.Shared.Show(tipText, ClassicTooltipColor(item),
                classicInv.Position.x + 40 + (local.x - 40) / ClassicCell * ClassicCell,
                classicInv.Position.y + 1 + (local.y - 17) / ClassicCell * ClassicCell);
        }

        /// <summary>MsgStr 0xb7 "%s: %d ea." or, with random options, 0x9f7 "%s [%d Option] : %d Quantity".</summary>
        private string ClassicTooltipText(RoItemInstance item) {
            string name = item.GetFullDisplayName(model.Metadata);
            int options = 0;
            if (item.IsIdentified && item.HasRandomOptions) {
                foreach (RoItemRandomOption option in item.CopyRandomOptions()) {
                    if (option.Index != 0) options++;
                }
            }

            return options > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} [{1} Option] : {2} Quantity", name, options, item.Count)
                : string.Format(CultureInfo.InvariantCulture, "{0}: {1} ea.", name, item.Count);
        }

        /// <summary>FUN_00ce9980: unidentified #aaaaaa, else the enchant colour (cards past the item's own slots), white without.</summary>
        private Color32 ClassicTooltipColor(RoItemInstance item) {
            if (!item.IsIdentified) return new Color32(0xaa, 0xaa, 0xaa, 255);
            RoItemMetadata metadata = item.ResolveMetadata(model.Metadata);
            int slots = metadata != null ? metadata.SlotCount : 0, enchants = 0;
            for (int i = slots; i < RoItemInstance.MaxCardSlots; i++) {
                if (item.GetCardId(i) > 0) enchants++;
            }

            return ClassicEnchantColors[Mathf.Clamp(enchants, 0, ClassicEnchantColors.Length - 1)];
        }

        // ---- diagnostics console (RoSkillSweepRunner "!inv ...") ----

        /// <summary>
        /// Drives the classic inventory through its mouse handlers (cell n = n-th drawn cell of the current tab):
        /// open | close | tab t | use n (double click) | world n (drag released over the world) | count &lt;text&gt; | ok |
        /// hotkey n slot | width w | desc n | state. Returns the state after the command.
        /// </summary>
        internal static string Sweep(RoLoginShellSession loginSession, string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            // the amount window belongs to whichever window opened it (a vending setup, a shop), not to the inventory
            if (instance?.classicInv == null && (a[0] == "count" || a[0] == "ok")) {
                if (a[0] == "count") RoClassicCountWindow.SetText(a.Length > 1 ? a[1] : string.Empty);
                else RoClassicCountWindow.Confirm();
                return "count window " + (RoClassicCountWindow.Visible ? "open: " + RoClassicCountWindow.Text : "closed");
            }

            if (a[0] == "open") Open(loginSession);
            else if (a[0] == "close") instance?.Hide();
            RoClassicInventory t = instance;
            if (t == null || t.classicInv == null) return "no classic inventory";
            return t.SweepClassicInventory(a);
        }

        private string SweepClassicInventory(string[] a) {
            int Arg(int i) => i < a.Length ? int.Parse(a[i], CultureInfo.InvariantCulture) : 0;
            Vector2Int CellLocal(int n) => new Vector2Int(40 + ClassicCell * (n % ClassicCols) + 16, 17 + ClassicCell * (n / ClassicCols) + 16);
            switch (a[0]) {
                case "tab":
                    OnClassicInvPressed(classicInv, new Vector2Int(10, 18 + (ClassicTabExtent - 1) * Arg(1) + 20));
                    break;
                case "use":
                    OnClassicInvDoubleClicked(classicInv, CellLocal(Arg(1)));
                    break;
                case "world":
                    if (ClassicInvDragSource(CellLocal(Arg(1))) is RoClassicItemPayload p) OnClassicInvDraggedOutside(classicInv, p);
                    break;
                case "count":
                    RoClassicCountWindow.SetText(a.Length > 1 ? a[1] : string.Empty);
                    break;
                case "ok":
                    RoClassicCountWindow.Confirm();
                    break;
                case "hotkey": {
                    if (ClassicInvDragSource(CellLocal(Arg(1))) is RoClassicItemPayload carried) RoClassicHotkeyBar.SweepDropClassicPayload(Arg(2), carried);
                    break;
                }
                case "width":
                    classicWidth = ClassicSnapWidth(Arg(1));
                    ApplyClassicInventorySize();
                    break;
                case "desc":
                    OnClassicInvRightClicked(classicInv, CellLocal(Arg(1)));
                    return "desc: " + RoClassicItemDescription.Describe();
            }

            var names = new System.Text.StringBuilder();
            for (int i = 0; i < classicList.Count && i < 12; i++) {
                names.Append(i == 0 ? string.Empty : ", ").Append(classicList[i].ItemId).Append('x').Append(classicList[i].Count).Append('@').Append(classicList[i].BagIndex);
            }

            return $"open={classicInv.Visible} tab={classicTab} width={classicWidth} cols={ClassicCols} first={classicFirstRow} total={classicTotalCount} " +
                $"list=[{names}] count={(RoClassicCountWindow.Visible ? "'" + RoClassicCountWindow.Text + "'" : "hidden")} droplock={dropLocked}";
        }
    }
}
