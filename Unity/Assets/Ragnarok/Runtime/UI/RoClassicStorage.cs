using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>A storage item carried by a classic drag (DragStart type 6), taken out when dropped on the inventory.</summary>
    internal sealed class RoClassicStoragePayload : IRoClassicDragPayload {
        public RoItemInstance Item;
        public string IconPath { get; set; }
    }

    /// <summary>
    /// The classic storage: UIItemStoreWnd (id 0x21, ui/RUNTIME-RULES-STORE-SHOP.md §3) from the oracle's 280×458 layout:
    /// the "loading" notice until the count packet, then one item per 32 px row (12 rows) in the tab's category, the
    /// "count/max" line, eight category tabs on the left. Items go out by dragging them onto the inventory and in by
    /// dropping inventory items here (amounts through UIItemDropCntWnd modes 6 / 7). The server opens it: the window
    /// shows while the session's storage is open (from the item lists to ZC_CLOSE_STORE), followed on the storage's
    /// functional change sequence by <see cref="Tick"/>; Close sends CZ_CLOSE_STORE.
    /// </summary>
    public sealed class RoClassicStorage {
        private const string StoreLayoutPath = "Ragnarok/Generated/OriginalClient/ui/UIItemStoreWnd";
        private const string StorePrefs = "ro.ui.classic.store.";
        private const string StoreUi = "유저인터페이스\\";
        private const int StoreCloseId = 0xc5;
        private int StoreRows => ((classicStore != null ? classicStore.Height : StoreDefaultH) - 74) / 32;
        private const int StoreTabExtent = 235;
        private static readonly Color32 StoreBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 StoreCount = new Color32(0x23, 0x23, 0x23, 255);
        private static readonly Color32 StoreFull = new Color32(255, 0, 0, 255);

        private static RoClassicStorage instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            storeSortMode = 0;
            storeSortChosen = false;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private IRoSocialEconomyCommandExecutor commandExecutor;
        private RoItemMetadataTable itemMetadata = RoItemMetadataTable.Empty;
        private int seenSequence = int.MinValue;
        private readonly List<RoItemInstance> storeList = new List<RoItemInstance>();
        private readonly List<RoClassicOp> storeLoadingOps = new List<RoClassicOp>();
        private readonly List<RoClassicOp> storeLoadedOps = new List<RoClassicOp>();
        private RoClassicWindow classicStore;
        private RoClassicChildLayout storeTab;
        private bool classicStoreShown;
        private bool classicStoreFailed;
        private bool storeLoaded;
        private int storeTabIndex;
        private int storeFirst;
        private int storeAmount;
        private int storeMax;

        /// <summary>Whether the storage window is up (Alt + right click in the cart sends to it, the inventory drops into it).</summary>
        public static bool IsOpen => instance != null && instance.classicStoreShown && instance.classicStore != null && instance.classicStore.Visible;

        private static RoClassicStorage Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicStorage { system = system };
            return instance;
        }

        /// <summary>
        /// Per frame (RoClassicGameUi): on a storage change the window opens while the session's storage is open and goes
        /// once it is closed, and the open window takes the new list; the hover tooltip and the sort combo every frame.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession, RoItemMetadataTable metadata) {
            if (loginSession == null) return;
            RoClassicStorage s = Ensure();
            if (s == null) return;
            if (!ReferenceEquals(s.session, loginSession)) {
                s.HideClassicStorage();
                s.session = loginSession;
                s.commandExecutor = new RoLegacySocialEconomyCommandExecutor(loginSession);
                s.seenSequence = int.MinValue;
            }

            metadata ??= RoItemMetadataTable.Empty;
            bool metadataChanged = !ReferenceEquals(s.itemMetadata, metadata);
            s.itemMetadata = metadata;
            int sequence = loginSession.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Storage);
            if (sequence != s.seenSequence) {
                s.seenSequence = sequence;
                s.Follow();
            } else if (metadataChanged && s.classicStoreShown) {
                s.RefreshNow();
            }

            s.UpdateClassicStorage();
        }

        /// <summary>Leaving the map: the window and its category / search windows go.</summary>
        public static void Close() {
            instance?.HideClassicStorage();
        }

        /// <summary>The server's storage state: open shows the window (or refreshes it), closed hides it.</summary>
        private void Follow() {
            RoStorageSnapshot storage = session.StorageSnapshot() ?? RoStorageSnapshot.Empty;
            if (storage.IsOpen) {
                RefreshClassicStorage(storage);
            } else {
                HideClassicStorage();
            }
        }

        /// <summary>The open window takes the session's storage again (a tab, a sort, the item names).</summary>
        private void RefreshNow() {
            if (!classicStoreShown) return;
            RefreshClassicStorage(session != null ? session.StorageSnapshot() : RoStorageSnapshot.Empty);
        }

        /// <summary>Shows the window (once) and draws the storage list in the current tab.</summary>
        private void RefreshClassicStorage(RoStorageSnapshot storage) {
            if (classicStoreFailed) return;
            try {
                EnsureClassicStorage();
                if (!classicStoreShown) {
                    classicStoreShown = true;
                    classicStore.Visible = true;
                    system.Windows.BringToFront(classicStore);
                }

                storage ??= RoStorageSnapshot.Empty;
                // loaded (+0x170) once the list ended; the count packet ZC_NOTIFY_STOREITEM_COUNTINFO follows the list
                bool loaded = storage.MaxAmount > 0;
                if (loaded != storeLoaded) {
                    storeLoaded = loaded;
                    UseStoreOps();
                }

                storeAmount = storage.Amount;
                storeMax = storage.MaxAmount;
                SetStoreTitle(string.IsNullOrWhiteSpace(storage.Name) ? "Storage" : storage.Name);
                RebuildClassicStoreList(storage.Items ?? Array.Empty<RoItemInstance>());
                RefreshClassicStoreSubs();
                classicStore.Invalidate();
            } catch (Exception e) {
                classicStoreFailed = true;
                DisposeClassicStorage();
                Debug.LogException(e);
            }
        }

        private void EnsureClassicStorage() {
            if (classicStore != null) return;
            var asset = Resources.Load<TextAsset>(StoreLayoutPath);
            if (asset == null) throw new InvalidOperationException("missing classic layout UIItemStoreWnd");
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
            // the oracle caught the loading state at 280×458: its notice (round boxes, the poring, three lines) is kept, the
            // frame is generated for the current height (and checked against the capture at 458)
            var captured = new List<RoClassicOp>(layout.Ops);
            foreach (RoClassicOp op in layout.Ops) {
                bool notice = op.Kind == RoClassicOpKind.Text || (op.Path != null && op.Path.EndsWith("notice_poring.bmp", StringComparison.OrdinalIgnoreCase)) ||
                    (op.Kind == RoClassicOpKind.Fill && op.Y >= 131 && op.X >= 55);
                if (notice) storeNoticeOps.Add(op);
            }

            BuildStoreOps(StoreDefaultH, storeLoadingOps, storeLoadedOps);
            if (!RoClassicFrames.SameOps(storeLoadingOps, captured, out string difference)) Debug.LogWarning("classic storage frame differs from the oracle: " + difference);

            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UIScrollBar") child.Visible = false;   // drawn from the list
                if (child.Class == "UITab") storeTab = child;
                if (child.Class == "UIBitmapEditCtrl") child.States.Clear();   // not a button: the native edit field takes its clicks
                if (child.Class == "UIItemStoreComboBoxWnd") child.Visible = false;   // drawn natively (its label sits above the box)
            }

            classicStore = new RoClassicWindow(system, layout) { Visible = false, DragSourceAt = ClassicStoreDragSource };
            classicStore.DrawDynamic += DrawClassicStorage;
            classicStore.Clicked += OnClassicStoreClicked;
            classicStore.RightClicked += OnClassicStoreRightClicked;
            classicStore.Pressed += OnClassicStorePressed;
            classicStore.Released += OnClassicStoreReleased;
            classicStore.ClickableAt = local => InStoreCombo(local) || StoreComboRowAt(local) >= 0;
            classicStore.ResizerModeAt = StoreResizerAt;
            classicStore.ResizeRequested += OnClassicStoreResize;
            classicStore.Wheeled += (w, local, delta) => ScrollClassicStorage(delta > 0 ? -Math.Min(StoreRows - 1, 3) : Math.Min(StoreRows - 1, 3));
            classicStore.Dropped += OnClassicStoreDropped;
            classicStore.Moved += w => {
                PlayerPrefs.SetInt(StorePrefs + "x", w.Position.x);
                PlayerPrefs.SetInt(StorePrefs + "y", w.Position.y);
            };

            // msg 0x22: STOREWNDINFO, a fresh profile (360, 200)
            Vector2Int client = system.ClientSize;
            int x = PlayerPrefs.GetInt(StorePrefs + "x", 360), y = PlayerPrefs.GetInt(StorePrefs + "y", 200);
            classicStore.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - classicStore.Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - classicStore.Height)));
            storeLoaded = false;
            SetStoreTab(PlayerPrefs.GetInt(StorePrefs + "tab", 0));
            EnsureClassicStoreExtras();
            int savedH = PlayerPrefs.GetInt(StorePrefs + "h", StoreDefaultH);
            ApplyStoreSize(StoreDefaultH, false);
            if (savedH != StoreDefaultH) OnClassicStoreResize(classicStore, 3, 280, savedH);
        }

        private void SetStoreTitle(string title) {
            foreach (RoClassicChildLayout child in classicStore.Layout.Children) {
                if (child.Class != "UIStaticText") continue;
                bool changed = false;
                foreach (RoClassicOp op in child.Ops) {
                    if (op.Kind != RoClassicOpKind.Text || op.Text == title) continue;
                    op.Text = title;
                    changed = true;
                }

                if (changed) classicStore.Invalidate();
                return;
            }
        }

        /// <summary>msg 0x16: the tab (style 3 draws one picture per selected tab, tab_item_NN.bmp).</summary>
        private void SetStoreTab(int tab) {
            storeTabIndex = Mathf.Clamp(tab, 0, 7);
            PlayerPrefs.SetInt(StorePrefs + "tab", storeTabIndex);
            if (storeTab != null) {
                foreach (RoClassicOp op in storeTab.Ops) {
                    if (op.Kind == RoClassicOpKind.Bitmap) op.Path = StoreUi + "basic_interface\\tab_item_" + (storeTabIndex + 1).ToString("00", CultureInfo.InvariantCulture) + ".bmp";
                }
            }

            storeFirst = 0;
            classicStore?.Invalidate();
        }

        /// <summary>
        /// msg 0x17: the storage items of the tab, in packet order. Costumes only on tab 4; else by type: 0 Use (healing,
        /// usable, delayed), 1 Cash, 2 Armors, 3 Weapons, 5 Ammo, 6 Card, 7 Etc (etc, pet egg).
        /// </summary>
        private void RebuildClassicStoreList(RoItemInstance[] items) {
            storeList.Clear();
            foreach (RoItemInstance item in items) {
                if (item != null && StoreTabOf(item) == storeTabIndex) storeList.Add(item);
            }

            SortStoreItems(storeList);

            storeFirst = Mathf.Clamp(storeFirst, 0, Math.Max(0, storeList.Count - StoreRows));
        }

        private int StoreTabOf(RoItemInstance item) {
            RoItemMetadata metadata = item.ResolveMetadata(itemMetadata);
            RoItemEquipLocation locations = item.ResolveEquipLocations(metadata);
            const RoItemEquipLocation costume = RoItemEquipLocation.CostumeHeadTop | RoItemEquipLocation.CostumeHeadMid |
                RoItemEquipLocation.CostumeHeadBottom | RoItemEquipLocation.CostumeRobe;
            if ((locations & costume) != 0) return 4;
            // the item's type byte as the packets carry it (the client's ITEM_INFO type); 11 (delayed consumables) is
            // listed with the armours in 0x8f0xxx's table, kept with the consumables here until a capture shows otherwise
            switch (item.ItemTypeRaw) {
                case 0: case 1: case 2: case 11:
                    return 0;
                case 0x12:
                    return 1;
                case 4: case 0xc: case 0xd: case 0xe:
                    return 2;
                case 5: case 8: case 9: case 0xf:
                    return 3;
                case 0xa: case 0x10: case 0x11: case 0x13:
                    return 5;
                case 6:
                    return 6;
                case 3: case 7:
                    return 7;
                default:
                    return -1;
            }
        }

        // ---- size: msg 0xe from the UIResizer (mode 3), RUNTIME-RULES-STORE-SHOP §3b ----

        private const int StoreDefaultH = 458;
        private readonly List<RoClassicOp> storeNoticeOps = new List<RoClassicOp>();
        private bool storeResized;

        /// <summary>The frame of OnDraw 0x8e1e00 for height <paramref name="h"/>; the loading state adds the white list and the notice.</summary>
        private void BuildStoreOps(int h, List<RoClassicOp> loading, List<RoClassicOp> loaded) {
            const int w = 280;
            loaded.Clear();
            RoClassicFrames.TitleBar(loaded, w, false);
            RoClassicFrames.ItemColumns(loaded, w, h - 38);
            RoClassicFrames.ButtonBar(loaded, w, h, 3);
            loading.Clear();
            loading.AddRange(loaded);
            RoClassicFrames.Fill(loading, 40, 17, (w - 40) / 32 * 32, (h - 74) / 32 * 32, new Color32(255, 255, 255, 255));
            loading.AddRange(storeNoticeOps);
        }

        private void UseStoreOps() {
            classicStore.Layout.Ops.Clear();
            classicStore.Layout.Ops.AddRange(storeLoaded ? storeLoadedOps : storeLoadingOps);
            classicStore.Invalidate();
        }

        /// <summary>The resizer, 16×16 at (W − 15, H − 15).</summary>
        private int StoreResizerAt(Vector2Int local) =>
            local.x >= classicStore.Width - 15 && local.x < classicStore.Width + 1 && local.y >= classicStore.Height - 15 && local.y < classicStore.Height + 1 ? 3 : -1;

        /// <summary>msg 0xe (mode 3): H = 458 + trunc32(clamp(h, 458, screen H · 7/10) − 458), the width stays.</summary>
        private void OnClassicStoreResize(RoClassicWindow window, int mode, int w, int h) {
            if (mode != 3) return;
            int cap = RoClassicUiSystem.Instance.ClientSize.y * 7 / 10;
            int clamped = Math.Max(StoreDefaultH, Math.Min(h, cap));
            int height = StoreDefaultH + (clamped - StoreDefaultH) / 32 * 32;
            if (height == classicStore.Height) return;
            ApplyStoreSize(height, true);
            PlayerPrefs.SetInt(StorePrefs + "h", height);
        }

        /// <summary>
        /// The relayout of a new height: Close (220, H − 23), Search (185, H − 23), the category boxes (5 + 26i, H − 52), the
        /// combo (214, H − 52), the resizer (W − 15, H − 15), and the search edit at (77, H − 23) once resized (65 at creation:
        /// the client's own inconsistency).
        /// </summary>
        private void ApplyStoreSize(int height, bool resized) {
            storeResized |= resized;
            classicStore.SetHeight(height);
            BuildStoreOps(height, storeLoadingOps, storeLoadedOps);
            UseStoreOps();
            int editX = storeResized ? 77 : 65;
            foreach (RoClassicChildLayout child in classicStore.Layout.Children) {
                switch (child.Class) {
                    case "UIBitmapButton" when child.Y > 100:
                        classicStore.SetChildPosition(child.Path, child.X, height - 23);
                        break;
                    case "UICheckBox":
                        classicStore.SetChildPosition(child.Path, child.X, height - 52);
                        break;
                    case "UIResizer":
                        classicStore.SetChildPosition(child.Path, classicStore.Width - 15, height - 15);
                        break;
                    case "UIBitmapEditCtrl":
                        classicStore.SetChildPosition(child.Path, editX, height - 23);
                        break;
                }
            }

            if (storeSearchEdit != null) storeSearchEdit.Rect = new RectInt(editX, height - 23, 121, 18);
            storeFirst = Mathf.Clamp(storeFirst, 0, Math.Max(0, storeList.Count - StoreRows));
            classicStore.Invalidate();
        }

        // ---- OnDraw 0x8e1e00 (the frame comes from the layout's ops) ----

        private void DrawClassicStorage(RoClassicWindow w) {
            if (!storeLoaded) {
                DrawStoreCombo(w);
                return;
            }

            int W = w.Width, H = w.Height;
            for (int r = 0; r < StoreRows; r++) w.DrawBitmapRaw(40, 17 + 32 * r, StoreUi + "basic_interface\\itemwin_mid.bmp");
            w.FillRect(72, 17, 192, StoreRows * 32, new Color32(255, 255, 255, 255));
            for (int r = 0; r < StoreRows; r++) {
                int k = storeFirst + r;
                if (k >= storeList.Count) break;
                RoItemInstance item = storeList[k];
                RoClassicItemText.DrawIcon(w, 44, 21 + 32 * r, item, itemMetadata, item.Count);
                RoClassicItemText.DrawName(w, 80, 29 + 32 * r, 180, item, itemMetadata);
            }

            w.DrawBitmap(6, H - 21, StoreUi + "inventory\\icon_num.bmp");
            string have = storeAmount.ToString(CultureInfo.InvariantCulture);
            w.TextOut(22, H - 19, have, 0, 11, storeAmount >= storeMax ? StoreFull : StoreCount);
            w.TextOut(23 + w.FontFor(0, 11).Measure(have), H - 19, "/" + storeMax.ToString(CultureInfo.InvariantCulture), 0, 11, StoreBlack);
            w.DrawScrollBar(W - 15, 14, H - 74, Math.Max(0, storeList.Count - StoreRows), storeList.Count, StoreRows, storeFirst);
            DrawStoreCombo(w);
        }

        // ---- mouse (§3e) ----

        /// <summary>Hit test 0x8d5400: the icon column only, 40 ≤ x &lt; 72, 17 &lt; y &lt; rows·32 + 17.</summary>
        private RoItemInstance ClassicStoreItemAt(Vector2Int local) {
            if (!storeLoaded || local.x < 40 || local.x >= 72 || local.y <= 17 || local.y >= StoreRows * 32 + 17) return null;
            int k = storeFirst + (local.y - 17) / 32;
            return k < storeList.Count ? storeList[k] : null;
        }

        private IRoClassicDragPayload ClassicStoreDragSource(Vector2Int local) {
            RoItemInstance item = ClassicStoreItemAt(local);
            return item == null ? null : new RoClassicStoragePayload { Item = item, IconPath = RoClassicItemText.IconPath(item, itemMetadata) };
        }

        /// <summary>
        /// LBtnDown on the tab strip (UITab +0x148, 20×235 at (0, 18)): tab = clamp(y / (235 / 8), 0, 7). The strip has no
        /// state bitmaps, so it is hit here rather than as a clickable child.
        /// </summary>
        private void OnClassicStorePressed(RoClassicWindow window, Vector2Int local) {
            if (storeTab == null || !storeTab.Visible) return;
            if (local.x < storeTab.X || local.x >= storeTab.X + storeTab.W || local.y < storeTab.Y || local.y >= storeTab.Y + StoreTabExtent) return;
            SetStoreTab((local.y - storeTab.Y) / (StoreTabExtent / 8));
            RefreshNow();
        }

        /// <summary>The category / search buttons, Close (button or [x]: CZ_CLOSE_STORE).</summary>
        private void OnClassicStoreClicked(RoClassicChildLayout child) {
            int category = Array.IndexOf(StoreCategoryIds, child.Id);
            if (category >= 0) {
                SetStoreCategory(category, (PlayerPrefs.GetInt(StorePrefs + "categories", 0) & (1 << category)) == 0);
                return;
            }

            if (child.Id == StoreSearchId) SearchClassicStorage();
            if (child.Id == StoreCloseId) _ = CloseStorageAsync();
        }

        /// <summary>RBtnDown: the description; Alt with the inventory open takes the whole stack (msg 0x38).</summary>
        private void OnClassicStoreRightClicked(RoClassicWindow window, Vector2Int local) {
            RoItemInstance item = ClassicStoreItemAt(local);
            if (item == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.altKey.isPressed) {
                if (RoClassicInventory.IsOpen && CanSendCommand()) {
                    _ = RunEconomyCommandAsync(commandExecutor.MoveStorageItemToInventoryAsync(item.BagIndex, Math.Max(1, item.Count)), "moved to inventory");
                }

                return;
            }

            RoClassicItemDescription.Toggle(item, itemMetadata);
        }

        private void ScrollClassicStorage(int rows) {
            int first = Mathf.Clamp(storeFirst + rows, 0, Math.Max(0, storeList.Count - StoreRows));
            if (first == storeFirst) return;
            storeFirst = first;
            classicStore.Invalidate();
        }

        /// <summary>
        /// Drop on the storage (vf10): an inventory item goes in (CZ_MOVE_ITEM_FROM_BODY_TO_STORE): one piece at once,
        /// else through the amount window (mode 7, 1 ≤ n ≤ the stack).
        /// </summary>
        private void OnClassicStoreDropped(RoClassicWindow window, Vector2Int local, IRoClassicDragPayload payload) {
            if (payload is RoClassicCartPayload carted) {
                RoClassicCart.TakeItem(carted.Item, true); // CZ_MOVE_ITEM_FROM_CART_TO_STORE
                return;
            }

            if (!(payload is RoClassicItemPayload carried) || carried.Item == null || carried.Item.IsEquipped || !CanSendCommand()) return;
            RoItemInstance item = carried.Item;
            if (item.Count <= 1) {
                _ = RunEconomyCommandAsync(commandExecutor.MoveInventoryItemToStorageAsync(item.BagIndex, 1), "moved to storage");
                return;
            }

            RoClassicCountWindow.Show(item.GetFullDisplayName(itemMetadata), item.Count, n => {
                n = Math.Max(1, Math.Min(n, item.Count));
                if (CanSendCommand()) _ = RunEconomyCommandAsync(commandExecutor.MoveInventoryItemToStorageAsync(item.BagIndex, (int) n), "moved to storage");
            });
        }

        /// <summary>
        /// A storage item dropped on the inventory (inventory vf10, mode 6): one piece at once, else the amount window
        /// (n ≤ the stored amount, below 1 cancels) → CZ_MOVE_ITEM_FROM_STORE_TO_BODY.
        /// </summary>
        internal static void TakeItem(RoItemInstance item) {
            instance?.TakeClassicStorageItem(item);
        }

        private void TakeClassicStorageItem(RoItemInstance item) {
            if (item == null || !CanSendCommand()) return;
            if (item.Count <= 1) {
                _ = RunEconomyCommandAsync(commandExecutor.MoveStorageItemToInventoryAsync(item.BagIndex, 1), "moved to inventory");
                return;
            }

            RoClassicCountWindow.Show(item.GetFullDisplayName(itemMetadata), item.Count, n => {
                n = Math.Min(n, item.Count);
                if (n >= 1 && CanSendCommand()) _ = RunEconomyCommandAsync(commandExecutor.MoveStorageItemToInventoryAsync(item.BagIndex, (int) n), "moved to inventory");
            });
        }

        // ---- per frame: tooltip ----

        /// <summary>Hover 0x8e8820: the display name (or "%s [%d Option]") above the row, no amount.</summary>
        private void UpdateClassicStorage() {
            if (!classicStoreShown || classicStore == null) return;
            RoClassicWindowManager windows = RoClassicUiSystem.Instance != null ? RoClassicUiSystem.Instance.Windows : null;
            if (windows == null) return;
            UpdateStoreCombo(windows);
            if (windows.Dragging != null) return;
            UpdateClassicStoreSubs(windows);
            if (windows.HoverWindow != classicStore) return;
            Vector2Int local = windows.Mouse - classicStore.Position;
            RoItemInstance item = ClassicStoreItemAt(local);
            if (item == null) return;
            string text = StoreTooltipText(item);
            Vector2Int at = classicStore.Position + RoClassicItemText.RowTooltipAnchor(local);
            RoClassicTooltip.Shared.Show(text, RoClassicItemText.TooltipColor(item, itemMetadata), at.x, at.y);
        }

        private void HideClassicStorage() {
            if (!classicStoreShown) return;
            classicStoreShown = false;
            if (classicStore != null) classicStore.Visible = false;
            HideClassicStoreSubs();
        }

        // ---- commands ----

        /// <summary>Close (the button or [x]): CZ_CLOSE_STORE, and the window goes at once.</summary>
        private async Task CloseStorageAsync() {
            if (!CanSendCommand()) {
                HideClassicStorage();
                return;
            }

            RoEconomyCommandResult result = await commandExecutor.CloseStorageAsync();
            if (!result.Accepted) Debug.LogWarning("Storage close request failed: " + result.Status + " " + result.Message);
            HideClassicStorage();
        }

        private bool CanSendCommand() {
            return session != null && commandExecutor != null && session.CommandStateSnapshot().CanSendMapCommand;
        }

        private static async Task RunEconomyCommandAsync(Task<RoEconomyCommandResult> command, string action) {
            if (command == null) return;
            RoEconomyCommandResult result = await command;
            if (!result.Accepted) Debug.LogWarning("Storage item was not " + action + ": " + result.Status + " " + result.Message);
        }

        private void DisposeClassicStorage() {
            if (classicStore == null) return;
            DisposeClassicStoreSubs();
            classicStore.Destroy();
            classicStore = null;
            classicStoreShown = false;
            storeLoadingOps.Clear();
            storeLoadedOps.Clear();
            storeNoticeOps.Clear();
            storeResized = false;
        }

        // ---- category windows UIItemStoreSubWnd (0x92..0x98, 0x135) and the search window UIItemStoreFindWnd (0x99) ----

        private const int StoreSearchId = 0x169;
        private static readonly int[] StoreCategoryIds = { 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x135 };
        private static readonly string[] StoreCategoryLayouts = {
            "UIItemStoreSubWnd", "UIItemStoreSubWnd.id147", "UIItemStoreSubWnd.id148", "UIItemStoreSubWnd.id149",
            "UIItemStoreSubWnd.id150", "UIItemStoreSubWnd.id151", "UIItemStoreSubWnd.id152", "UIItemStoreSubWnd.id309",
        };

        /// <summary>A 140×145 list of storage items: one category (type 0..7) or the search results (type −1).</summary>
        private sealed class StoreSubWindow {
            public RoClassicWindow Window;
            public int Type;
            public string Search = string.Empty;
            public int First;
            public readonly List<RoItemInstance> List = new List<RoItemInstance>();
            public int Rows => (Window.Height - 38) / 32;
        }

        private readonly StoreSubWindow[] storeCategories = new StoreSubWindow[8];
        private StoreSubWindow storeFind;
        private RoClassicEditField storeSearchEdit;

        /// <summary>DAT_01553ab7..9: the sort picked in the combo box (0 Base = packet order, 1 Upgrade, 2 Downgrade).</summary>
        private static int storeSortMode;

        private void EnsureClassicStoreExtras() {
            if (storeSearchEdit != null) return;
            // the search edit (UIBitmapEditCtrl over bg_search.bmp, 20 bytes); Enter = the Search button
            storeSearchEdit = new RoClassicEditField { Rect = new RectInt(65, classicStore.Height - 23, 121, 18), MaxBytes = 20 };
            storeSearchEdit.Submitted += _ => OnClassicStoreClicked(new RoClassicChildLayout { Id = StoreSearchId });
            classicStore.AddEditField(storeSearchEdit);
            int mask = PlayerPrefs.GetInt(StorePrefs + "categories", 0);
            for (int i = 0; i < 8; i++) {
                if ((mask & (1 << i)) != 0) SetStoreCategory(i, true);
            }
        }

        /// <summary>A category check box (msg 6, 0x92..0x98 / 0x135): checked opens its window, the bit is kept for next time.</summary>
        private void SetStoreCategory(int type, bool open) {
            int mask = PlayerPrefs.GetInt(StorePrefs + "categories", 0);
            mask = open ? mask | (1 << type) : mask & ~(1 << type);
            PlayerPrefs.SetInt(StorePrefs + "categories", mask);
            foreach (RoClassicChildLayout child in classicStore.Layout.Children) {
                if (child.Id != StoreCategoryIds[type]) continue;
                foreach (RoClassicOp op in child.Ops) {
                    if (op.Kind == RoClassicOpKind.Bitmap && op.Path != null) op.Path = op.Path.Substring(0, op.Path.Length - 6) + (open ? "-1.bmp" : "-2.bmp");
                }
            }

            classicStore.Invalidate();
            if (open) {
                StoreSubWindow sub = storeCategories[type] ??= CreateStoreSubWindow(StoreCategoryLayouts[type], type, "cat" + type);
                RebuildStoreSub(sub);
                ShowStoreSub(sub, "cat" + type);
                RoClassicUiSystem.Instance.Windows.PushEsc(sub.Window);
            } else if (storeCategories[type] != null) {
                storeCategories[type].Window.Visible = false;
            }
        }

        private StoreSubWindow CreateStoreSubWindow(string layoutName, int type, string prefsName) {
            var asset = Resources.Load<TextAsset>("Ragnarok/Generated/OriginalClient/ui/" + layoutName);
            if (asset == null) throw new InvalidOperationException("missing classic layout " + layoutName);
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UIScrollBar") child.Visible = false;
            }

            var sub = new StoreSubWindow { Type = type };
            sub.Window = new RoClassicWindow(RoClassicUiSystem.Instance, layout) { Visible = false };
            sub.Window.DrawDynamic += w => DrawStoreSub(sub, w);
            // Esc (the storage itself is not in the Esc list): the newest category window unchecks its box, the search window closes
            sub.Window.EscClose = () => {
                if (sub == storeFind) sub.Window.Visible = false;
                else SetStoreCategory(sub.Type, false);
                return true;
            };
            sub.Window.DragSourceAt = local => {
                RoItemInstance item = StoreSubItemAt(sub, local);
                return item == null ? null : new RoClassicStoragePayload { Item = item, IconPath = RoClassicItemText.IconPath(item, itemMetadata) };
            };
            sub.Window.Dropped += OnClassicStoreDropped;
            sub.Window.RightClicked += (w, local) => {
                RoItemInstance item = StoreSubItemAt(sub, local);
                if (item != null) RoClassicItemDescription.Toggle(item, itemMetadata);
            };
            sub.Window.Wheeled += (w, local, delta) => {
                int first = Mathf.Clamp(sub.First + (delta > 0 ? -1 : 1) * Math.Min(sub.Rows - 1, 3), 0, Math.Max(0, sub.List.Count - sub.Rows));
                if (first == sub.First) return;
                sub.First = first;
                w.Invalidate();
            };
            sub.Window.Clicked += child => {
                // the search window's close (0xc5): DeleteWindow(0x99)
                if (child.Id == StoreCloseId && sub == storeFind) sub.Window.Visible = false;
            };
            sub.Window.Moved += w => {
                PlayerPrefs.SetInt(StorePrefs + prefsName + ".x", w.Position.x);
                PlayerPrefs.SetInt(StorePrefs + prefsName + ".y", w.Position.y);
            };

            // the frame generated for any size (checked against the oracle's 140×145), the resizer (mode 7)
            var generated = new List<RoClassicOp>();
            BuildStoreSubOps(140, 145, generated);
            if (!RoClassicFrames.SameOps(generated, layout.Ops, out string difference)) Debug.LogWarning("classic " + layoutName + " frame differs from the oracle: " + difference);
            sub.Window.ResizerModeAt = local =>
                local.x >= sub.Window.Width - 15 && local.x < sub.Window.Width + 1 && local.y >= sub.Window.Height - 15 && local.y < sub.Window.Height + 1 ? 7 : -1;
            sub.Window.ResizeRequested += (w, mode, width, height) => {
                if (mode != 7) return;
                // msg 0xe (7): W = 140 + trunc32(clamp(w, 140, 320) − 140), H = 145 + trunc32(clamp(h, 145, 336) − 145), first row 0
                int newW = 140 + (Mathf.Clamp(width, 140, 320) - 140) / 32 * 32, newH = 145 + (Mathf.Clamp(height, 145, 336) - 145) / 32 * 32;
                if (newW == sub.Window.Width && newH == sub.Window.Height) return;
                ApplyStoreSubSize(sub, newW, newH);
                PlayerPrefs.SetInt(StorePrefs + prefsName + ".w", newW);
                PlayerPrefs.SetInt(StorePrefs + prefsName + ".h", newH);
            };

            int savedW = PlayerPrefs.GetInt(StorePrefs + prefsName + ".w", 140), savedH = PlayerPrefs.GetInt(StorePrefs + prefsName + ".h", 145);
            if (savedW != 140 || savedH != 145) ApplyStoreSubSize(sub, savedW, savedH);
            return sub;
        }

        /// <summary>The category / search window frame (= oracle): title bar with sys_base, white body, button bar, icon cells.</summary>
        private static void BuildStoreSubOps(int w, int h, List<RoClassicOp> ops) {
            var white = new Color32(255, 255, 255, 255);
            int rows = (h - 38) / 32;
            ops.Clear();
            RoClassicFrames.TitleBar(ops, w, true);
            RoClassicFrames.Fill(ops, 0, 17, w, h - 38, white);
            RoClassicFrames.ButtonBar(ops, w, h, 0);
            RoClassicFrames.IconColumn(ops, 10, rows);
            RoClassicFrames.Fill(ops, 72, 17, (w - 40) / 32 * 32 - 32, rows * 32, white);
        }

        /// <summary>A new size: frame, resizer (W − 15, H − 15), title W − 32 wide, the search window's close at (W − 16, 3).</summary>
        private static void ApplyStoreSubSize(StoreSubWindow sub, int w, int h) {
            sub.Window.SetWidth(w);
            sub.Window.SetHeight(h);
            var ops = new List<RoClassicOp>();
            BuildStoreSubOps(w, h, ops);
            sub.Window.Layout.Ops.Clear();
            sub.Window.Layout.Ops.AddRange(ops);
            foreach (RoClassicChildLayout child in sub.Window.Layout.Children) {
                if (child.Class == "UIResizer") sub.Window.SetChildPosition(child.Path, w - 15, h - 15);
                else if (child.Class == "UIStaticText") child.W = w - 32;
                else if (child.Class == "UIBitmapButton") sub.Window.SetChildPosition(child.Path, w - 16, 3);
            }

            sub.First = 0;
            sub.Window.Invalidate();
        }

        private static void ShowStoreSub(StoreSubWindow sub, string prefsName) {
            Vector2Int client = RoClassicUiSystem.Instance.ClientSize;
            Vector2Int d = sub.Window.Layout.DefaultPosition;
            int x = PlayerPrefs.GetInt(StorePrefs + prefsName + ".x", d.x), y = PlayerPrefs.GetInt(StorePrefs + prefsName + ".y", d.y);
            sub.Window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - sub.Window.Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - sub.Window.Height)));
            sub.Window.Visible = true;
            RoClassicUiSystem.Instance.Windows.BringToFront(sub.Window);
            sub.Window.Invalidate();
        }

        /// <summary>
        /// msg 0x17 of a category / search window: its storage items (category rules of the main tabs; the search
        /// window: every item whose display name contains the text, case-sensitive), in the global sort.
        /// </summary>
        private void RebuildStoreSub(StoreSubWindow sub) {
            sub.List.Clear();
            RoItemInstance[] items = session != null ? session.StorageSnapshot().Items ?? Array.Empty<RoItemInstance>() : Array.Empty<RoItemInstance>();
            foreach (RoItemInstance item in items) {
                if (item == null) continue;
                if (sub.Type >= 0 ? StoreTabOf(item) != sub.Type : item.GetFullDisplayName(itemMetadata).IndexOf(sub.Search, StringComparison.Ordinal) < 0) continue;
                sub.List.Add(item);
            }

            SortStoreItems(sub.List);
            sub.First = Mathf.Clamp(sub.First, 0, Math.Max(0, sub.List.Count - sub.Rows));
            sub.Window.Invalidate();
        }

        /// <summary>
        /// SortByName 0x8f82e0: packet order (Base), or a stable byte-wise sort by the full display name (Upgrade), the
        /// same reversed (Downgrade).
        /// </summary>
        private void SortStoreItems(List<RoItemInstance> list) {
            list.Sort((a, b) => a.BagIndex.CompareTo(b.BagIndex));
            if (storeSortMode == 0) return;
            var keyed = new List<(string Name, int Order, RoItemInstance Item)>(list.Count);
            for (int i = 0; i < list.Count; i++) keyed.Add((list[i].GetFullDisplayName(itemMetadata), i, list[i]));
            keyed.Sort((a, b) => {
                int c = string.CompareOrdinal(a.Name, b.Name);
                return c != 0 ? c : a.Order.CompareTo(b.Order);
            });
            list.Clear();
            foreach ((string _, int _, RoItemInstance item) in keyed) list.Add(item);
            if (storeSortMode == 2) list.Reverse();
        }

        /// <summary>
        /// OnDraw (= oracle): the frame from the layout; itemwin_mid at (10, 17 + 32r), white at (72, 17), the icon at
        /// (14, 21 + 32r) and the name at (50, 29 + 32r) cut to W − 76.
        /// </summary>
        private void DrawStoreSub(StoreSubWindow sub, RoClassicWindow w) {
            int rows = sub.Rows;   // the frame and the icon cells are the layout's ops
            for (int r = 0; r < rows; r++) {
                int k = sub.First + r;
                if (k >= sub.List.Count) break;
                RoItemInstance item = sub.List[k];
                RoClassicItemText.DrawIcon(w, 14, 21 + 32 * r, item, itemMetadata, item.Count);
                RoClassicItemText.DrawName(w, 50, 29 + 32 * r, w.Width - 76, item, itemMetadata);
            }

            w.DrawScrollBar(w.Width - 15, 14, w.Height - 38, Math.Max(0, sub.List.Count - rows), sub.List.Count, rows, sub.First);
        }

        /// <summary>Hit test 0x8d52c0: the icon column 10 ≤ x &lt; 42, 17 &lt; y &lt; rows·32 + 17.</summary>
        private static RoItemInstance StoreSubItemAt(StoreSubWindow sub, Vector2Int local) {
            if (local.x < 10 || local.x >= 42 || local.y <= 17 || local.y >= sub.Rows * 32 + 17) return null;
            int k = sub.First + (local.y - 17) / 32;
            return k < sub.List.Count ? sub.List[k] : null;
        }

        /// <summary>The Search button (0x169): a non-empty text opens the search window on it and empties the edit.</summary>
        private void SearchClassicStorage() {
            string text = storeSearchEdit != null ? storeSearchEdit.Text : string.Empty;
            if (string.IsNullOrEmpty(text)) return;
            storeFind ??= CreateStoreSubWindow("UIItemStoreFindWnd", -1, "find");
            storeFind.Search = text;
            storeFind.First = 0;
            RebuildStoreSub(storeFind);
            if (!storeFind.Window.Visible) ShowStoreSub(storeFind, "find");
            RoClassicUiSystem.Instance.Windows.PushEsc(storeFind.Window);
            storeSearchEdit.SetText(string.Empty);
        }

        private void RefreshClassicStoreSubs() {
            foreach (StoreSubWindow sub in storeCategories) {
                if (sub != null && sub.Window.Visible) RebuildStoreSub(sub);
            }

            if (storeFind != null && storeFind.Window.Visible) RebuildStoreSub(storeFind);
        }

        private void HideClassicStoreSubs() {
            foreach (StoreSubWindow sub in storeCategories) {
                if (sub != null) sub.Window.Visible = false;
            }

            if (storeFind != null) storeFind.Window.Visible = false;
            if (storeSearchEdit != null && storeSearchEdit.HasFocus) RoClassicTextInput.Focus(null);
        }

        private void DisposeClassicStoreSubs() {
            for (int i = 0; i < storeCategories.Length; i++) {
                storeCategories[i]?.Window.Destroy();
                storeCategories[i] = null;
            }

            storeFind?.Window.Destroy();
            storeFind = null;
            storeSearchEdit = null;
        }

        /// <summary>Hover tooltips over the category / search windows, as the main window's.</summary>
        private void UpdateClassicStoreSubs(RoClassicWindowManager windows) {
            foreach (StoreSubWindow sub in storeCategories) {
                if (sub != null && windows.HoverWindow == sub.Window) StoreSubTooltip(sub, windows);
            }

            if (storeFind != null && windows.HoverWindow == storeFind.Window) StoreSubTooltip(storeFind, windows);
        }

        private void StoreSubTooltip(StoreSubWindow sub, RoClassicWindowManager windows) {
            Vector2Int local = windows.Mouse - sub.Window.Position;
            RoItemInstance item = StoreSubItemAt(sub, local);
            if (item == null) return;
            Vector2Int at = sub.Window.Position + new Vector2Int(10 + (local.x - 10) / 32 * 32, 1 + (local.y - 17) / 32 * 32);
            RoClassicTooltip.Shared.Show(StoreTooltipText(item), RoClassicItemText.TooltipColor(item, itemMetadata), at.x, at.y);
        }

        private string StoreTooltipText(RoItemInstance item) {
            string name = item.GetFullDisplayName(itemMetadata);
            int options = RoClassicItemText.OptionCount(item);
            return options > 0 ? RoClassicUiHost.MsgStr(0xa28, "%s [%d Option]").Replace("%s", name).Replace("%d", options.ToString(CultureInfo.InvariantCulture)) : name;
        }

        // ---- the sort combo UIItemStoreComboBoxWnd + UIItemStoreComboBox (ui/RUNTIME-RULES-STORE-EXTRA.md §1) ----

        private const int StoreComboX = 214;
        private static readonly int[] StoreSortNames = { 0xccc, 0xcca, 0xccb };
        private static readonly string[] StoreSortFallback = { "Base", "Upgrade", "Downgrade" };

        /// <summary>Whether a sort was picked this session (the label reads "(NO SELECT)" before; the flags are memory-only).</summary>
        private static bool storeSortChosen;

        private bool storeComboOpen;
        private int storeComboPicture;
        private int storeComboArrowState;   // 0 normal, 1 hover, 2 pressed

        private int StoreComboY => classicStore.Height - 52;

        /// <summary>
        /// The closed box: bg_setting.bmp (44×18) with the arrow combo_btn_a/b/c at (44, 0) and the choice at (2, 3) black on
        /// a white shadow at (3, 4). Open, the 62×45 list grows upward from y − 45: the last row picture
        /// combobox_blue%d.bmp (58×45, kept while nothing is hovered), the names at (3, 2 + 15·row).
        /// </summary>
        private void DrawStoreCombo(RoClassicWindow w) {
            int x = StoreComboX, y = StoreComboY;
            w.DrawBitmapRaw(x, y, StoreUi + "basic_interface\\bg_setting.bmp");
            string label = storeSortChosen ? StoreSortName(storeSortMode) : "(NO SELECT)";
            w.ClipTo(x, y, 44, 18);   // the label stays inside the box, the arrow beside it
            w.TextOut(x + 3, y + 4, label, 0, 12, new Color32(255, 255, 255, 255));
            w.TextOut(x + 2, y + 3, label, 0, 12, StoreBlack);
            w.ResetClip();
            string arrow = storeComboArrowState == 2 ? "combo_btn_c.bmp" : storeComboArrowState == 1 ? "combo_btn_b.bmp" : "combo_btn_a.bmp";
            w.DrawBitmapRaw(x + 44, y, StoreUi + "basic_interface\\" + arrow);
            if (!storeComboOpen) return;
            int top = y - 45;
            w.DrawBitmap(x, top, StoreUi + "basic_interface\\combobox_blue" + storeComboPicture.ToString(CultureInfo.InvariantCulture) + ".bmp");
            for (int row = 0; row < 3; row++) w.TextOut(x + 3, top + 2 + 15 * row, StoreSortName(row), 0, 12, StoreBlack);
        }

        private static string StoreSortName(int mode) => RoClassicUiHost.MsgStr(StoreSortNames[mode], StoreSortFallback[mode]);

        private bool InStoreCombo(Vector2Int local) =>
            local.x >= StoreComboX && local.x < StoreComboX + 62 && local.y >= StoreComboY && local.y < StoreComboY + 18;

        /// <summary>The open list's row under a point, −1 outside it.</summary>
        private int StoreComboRowAt(Vector2Int local) {
            int top = StoreComboY - 45;
            if (!storeComboOpen || local.x < StoreComboX || local.x >= StoreComboX + 62 || local.y < top || local.y >= top + 45) return -1;
            return Mathf.Clamp((local.y - top - 2) / 15, 0, 2);
        }

        /// <summary>LBtnUp: on the box / arrow / label toggles the list (msg 6, 0x10d); on a row picks it (msg 0x27).</summary>
        private void OnClassicStoreReleased(RoClassicWindow window, Vector2Int local) {
            int row = StoreComboRowAt(local);
            if (row >= 0) {
                storeComboOpen = false;
                SetStoreSort(row);
                return;
            }

            if (InStoreCombo(local)) {
                storeComboOpen = !storeComboOpen;
                storeComboPicture = storeSortMode;
                classicStore.Invalidate();
            }
        }

        /// <summary>msg 0x27 from the combo: the sort flags, the label, the main list and the open category / search windows re-sorted.</summary>
        private void SetStoreSort(int mode) {
            storeSortMode = Mathf.Clamp(mode, 0, 2);
            storeSortChosen = true;
            RebuildClassicStoreList(session != null ? session.StorageSnapshot().Items ?? Array.Empty<RoItemInstance>() : Array.Empty<RoItemInstance>());
            RefreshClassicStoreSubs();
            classicStore.Invalidate();
        }

        /// <summary>Per frame: the arrow's hover / pressed bitmap, the hovered row's picture (kept after the mouse leaves).</summary>
        private void UpdateStoreCombo(RoClassicWindowManager windows) {
            Vector2Int local = windows.Mouse - classicStore.Position;
            bool over = windows.HoverWindow == classicStore;
            bool onArrow = over && local.x >= StoreComboX + 44 && local.x < StoreComboX + 62 && local.y >= StoreComboY && local.y < StoreComboY + 18;
            Mouse mouse = Mouse.current;
            int arrow = onArrow ? (mouse != null && mouse.leftButton.isPressed ? 2 : 1) : 0;
            int row = over ? StoreComboRowAt(local) : -1;
            bool changed = arrow != storeComboArrowState || (row >= 0 && row != storeComboPicture);
            storeComboArrowState = arrow;
            if (row >= 0) storeComboPicture = row;
            if (changed) classicStore.Invalidate();
        }

        // ---- diagnostics console (RoSkillSweepRunner "!st ...") ----

        /// <summary>tab t | take row [n] | put bagIndex [n] | desc row | close | state (row = drawn row of the tab).</summary>
        internal static string Sweep(string command) {
            RoClassicStorage s = instance;
            return s?.classicStore == null ? "no classic storage" : s.SweepClassicStorage(command);
        }

        private string SweepClassicStorage(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length ? int.Parse(a[i], CultureInfo.InvariantCulture) : 0;
            Vector2Int Row(int k) => new Vector2Int(56, 17 + 32 * (k - storeFirst) + 16);
            void Count() {
                if (a.Length > 2 && RoClassicCountWindow.Visible) {
                    RoClassicCountWindow.SetText(a[2]);
                    RoClassicCountWindow.Confirm();
                }
            }

            switch (a[0]) {
                case "tab":
                    SetStoreTab(Arg(1));
                    RefreshNow();
                    break;
                case "take":
                    if (ClassicStoreDragSource(Row(Arg(1))) is RoClassicStoragePayload taken) {
                        TakeClassicStorageItem(taken.Item);
                        Count();
                    }

                    break;
                case "put": {
                    RoItemInstance[] bag = session != null ? session.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>() : Array.Empty<RoItemInstance>();
                    RoItemInstance item = Array.Find(bag, i => i != null && i.BagIndex == Arg(1));
                    if (item != null) {
                        OnClassicStoreDropped(classicStore, Vector2Int.zero, new RoClassicItemPayload { Item = item });
                        Count();
                    }

                    break;
                }
                case "desc":
                    OnClassicStoreRightClicked(classicStore, Row(Arg(1)));
                    break;
                case "close":
                    OnClassicStoreClicked(new RoClassicChildLayout { Id = StoreCloseId });
                    break;
                case "cat":
                    OnClassicStoreClicked(new RoClassicChildLayout { Id = StoreCategoryIds[Arg(1)] });
                    break;
                case "search":
                    storeSearchEdit?.SetText(a.Length > 1 ? string.Join(" ", a, 1, a.Length - 1) : string.Empty);
                    OnClassicStoreClicked(new RoClassicChildLayout { Id = StoreSearchId });
                    break;
                case "sort":
                    // the combo: open the list, pick row m (two releases like the mouse)
                    OnClassicStoreReleased(classicStore, new Vector2Int(StoreComboX + 50, StoreComboY + 9));
                    OnClassicStoreReleased(classicStore, new Vector2Int(StoreComboX + 10, StoreComboY - 45 + 2 + 15 * Mathf.Clamp(Arg(1), 0, 2) + 7));
                    break;
                case "combo":
                    OnClassicStoreReleased(classicStore, new Vector2Int(StoreComboX + 10, StoreComboY + 9));
                    break;
            }

            var names = new System.Text.StringBuilder();
            for (int i = 0; i < storeList.Count && i < 12; i++) names.Append(i == 0 ? "" : ", ").Append(storeList[i].ItemId).Append('x').Append(storeList[i].Count);
            var subs = new System.Text.StringBuilder();
            for (int i = 0; i < storeCategories.Length; i++) {
                if (storeCategories[i] != null && storeCategories[i].Window.Visible) subs.Append(" cat").Append(i).Append('=').Append(storeCategories[i].List.Count);
            }

            if (storeFind != null && storeFind.Window.Visible) subs.Append(" find(").Append(storeFind.Search).Append(")=").Append(storeFind.List.Count);
            return $"shown={classicStoreShown && classicStore.Visible} loaded={storeLoaded} tab={storeTabIndex} {storeAmount}/{storeMax} first={storeFirst} sort={storeSortMode}{subs} " +
                $"list=[{names}] count={(RoClassicCountWindow.Visible ? "'" + RoClassicCountWindow.Text + "'" : "hidden")}";
        }
    }
}
