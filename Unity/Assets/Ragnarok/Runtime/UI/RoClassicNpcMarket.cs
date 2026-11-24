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
    /// A row carried by a classic drag between the NPC barter / expanded barter / market lists and their carts. Type is the
    /// client's drag type (mode+0x310): 0x1f barter / expanded list → cart, 4 barter / market cart → list, 0x20 expanded
    /// cart → list, 3 market list → cart; Amount the drag amount (mode+0x320).
    /// </summary>
    internal sealed class RoClassicNpcMarketPayload : IRoClassicDragPayload {
        public int Type;
        public object Entry;
        public int Amount;
        public string IconPath { get; set; }
    }

    /// <summary>
    /// The NPC shops paid with items or sold from a stock (npc-barter-market-spec, Custom game.exe 2024-06-14):
    /// the barter shop UIBartermarketWnd 0x14e + UIBarterItemPurchaseWnd 0x14f (ZC_NPC_BARTER_MARKET_ITEMINFO 0x0b78), the
    /// expanded barter shop UIExpandedBartermarketWnd 0x155 + UIExpandedBarterItemPurchaseWnd 0x156 (0x0b79) and the NPC
    /// market UIParaItemShopWnd 0xfe + UIParaItemPurchaseWnd 0xff + UIParaResultWnd 0x100 (ZC_NPC_MARKET_OPEN 0x0b7a,
    /// ZC_NPC_MARKET_PURCHASE_RESULT 0x0b4e). Like the client the windows are made only by those packets (each new packet
    /// clears the shop's item manager and refreshes the windows in place) and own the shop list and the cart from then on;
    /// every close (Cancel, Buy, Esc, OK) clears the session's item list of the shop as the client clears its manager, so a
    /// closed shop never shows again until the NPC sends a new list, and sends the shop's close packet (barter 0x0b12,
    /// expanded 0x0b58, market 0x09d8). The barter results (ZC_PC_PURCHASE_RESULT 0x00ca 11..14) are reported by the plain
    /// shop (RoClassicNpcShop).
    /// </summary>
    public sealed partial class RoClassicNpcMarket {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        // the barter list and the market list share the plain shop list's info record (mgr+0x700, "Shop Items"), the
        // barter purchase and the market cart the plain purchase window's (mgr+0x720): the classic plain shop's keys
        private const string ShopPrefs = "ro.ui.classic.shop.";
        private const int BuyId = 0xb4;
        private const int CancelId = 0xb5;
        private const int OkId = 0xb4;
        private const int MaxAmount = 30000;
        private const int BaseInventorySlots = 100;
        private const int DragListToCart = 0x1f;
        private const int DragCartToList = 4;
        private const int DragExpandedCartToList = 0x20;
        private const int DragMarketListToCart = 3;
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 Selected = new Color32(0x70, 0x9f, 0xed, 255);   // colorchip(6,2)
        private const string Red = "#ff0000";
        private const string Cyan = "#00ffff";

        private static RoClassicNpcMarket instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private int seenNpcSequence = int.MinValue;
        private bool seeded;
        private int seenBarter;
        private int seenExpanded;
        private int seenMarket;
        private int seenMarketResult;

        /// <summary>Whether any of the barter / expanded barter / market windows is up.</summary>
        public static bool IsOpen => instance != null && (instance.barterShown || instance.expandedShown || instance.MarketAnyShown);

        private static RoClassicNpcMarket Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicNpcMarket { system = system };
            return instance;
        }

        /// <summary>
        /// Per frame (RoClassicGameUi): when the session's NPC change sequence moves, a new barter / expanded barter / market
        /// packet opens (or refreshes) its windows and a market result is reported; the hovered rows show their tooltips.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession, RoClassicChat chatWindow, RoItemMetadataTable itemMetadata) {
            if (loginSession == null) return;
            RoClassicNpcMarket m = Ensure();
            if (m == null) return;
            if (!ReferenceEquals(m.session, loginSession)) {
                m.session = loginSession;
                m.seeded = false;
                m.seenNpcSequence = int.MinValue;
            }

            m.chat = chatWindow;
            itemMetadata ??= RoItemMetadataTable.Empty;
            if (!ReferenceEquals(m.metadata, itemMetadata)) {
                m.metadata = itemMetadata;
                m.InvalidateAll();
            }

            int sequence = loginSession.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Npc);
            if (sequence != m.seenNpcSequence) {
                m.seenNpcSequence = sequence;
                m.OnShopChanged(loginSession.NpcShopSnapshot() ?? RoNpcShopSnapshot.Empty);
            }

            if (m.barterShown || m.expandedShown || m.MarketAnyShown) m.TickHover();
        }

        /// <summary>
        /// Leaving the map: every window goes and the shops' lists are dropped; nothing is sent (the map server forgets the
        /// shop with the map). The next tick takes the session's packet counts as seen.
        /// </summary>
        public static void Close() {
            RoClassicNpcMarket m = instance;
            if (m == null) return;
            m.HideBarter();
            m.HideExpanded();
            m.HideMarket();
            m.HideMarketResult();
            m.seeded = false;
            m.seenNpcSequence = int.MinValue;
        }

        private void OnShopChanged(RoNpcShopSnapshot shop) {
            if (!seeded) {
                // nothing from before: the windows open only on packets that arrive while the gameplay UI is up
                seeded = true;
                seenBarter = shop.BarterSequence;
                seenExpanded = shop.ExpandedBarterSequence;
                seenMarket = shop.MarketSequence;
                seenMarketResult = shop.MarketResultSequence;
                return;
            }

            if (shop.BarterSequence != seenBarter) {
                seenBarter = shop.BarterSequence;
                OpenBarter(shop.BarterItems);
            }

            if (shop.ExpandedBarterSequence != seenExpanded) {
                seenExpanded = shop.ExpandedBarterSequence;
                OpenExpanded(shop.ExpandedBarterItems);
            }

            if (shop.MarketSequence != seenMarket) {
                seenMarket = shop.MarketSequence;
                OpenMarket(shop.MarketItems);
            }

            if (shop.MarketResultSequence != seenMarketResult) {
                seenMarketResult = shop.MarketResultSequence;
                if (shop.HasMarketResult) OnMarketResult(shop.MarketResult, shop.MarketResultItems);
            }
        }

        private void InvalidateAll() {
            barterList?.Invalidate();
            barterBuy?.Invalidate();
            expandedList?.Invalidate();
            expandedBuy?.Invalidate();
            marketList?.Invalidate();
            marketCart?.Invalidate();
            marketResult?.Invalidate();
        }

        private void TickHover() {
            RoClassicWindowManager windows = system.Windows;
            if (windows.Dragging != null) return;
            RoClassicWindow over = windows.HoverWindow;
            if (over == null || !over.Visible) return;
            Vector2Int local = windows.Mouse - over.Position;
            if (over == barterList || over == barterBuy) HoverBarter(over, local);
            else if (over == expandedList || over == expandedBuy) HoverExpanded(over, local);
            else if (over == marketList || over == marketCart) HoverMarket(over, local);
        }

        // ================================================================ shared helpers

        private void Say(string text, bool good = false) {
            if (string.IsNullOrEmpty(text)) return;
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, good ? Cyan : Red);
        }

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        /// <summary>sprintf with the MsgStr template's %s / %d filled in order.</summary>
        private static string Format(string template, params object[] args) {
            var s = new StringBuilder(template.Length + 16);
            int a = 0;
            for (int i = 0; i < template.Length; i++) {
                if (template[i] == '%' && i + 1 < template.Length && (template[i + 1] == 's' || template[i + 1] == 'd') && a < args.Length) {
                    s.Append(Convert.ToString(args[a++], CultureInfo.InvariantCulture));
                    i++;
                    continue;
                }

                s.Append(template[i]);
            }

            return s.ToString();
        }

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>0xd1e010: the equipment item types (4, 5, 8, 9, 0xb..0xf), one piece per cart line.</summary>
        private static bool IsEquipType(int type) => type == 4 || type == 5 || type == 8 || type == 9 || (type >= 0xb && type <= 0xf);

        /// <summary>0xd1de60: item type 0x12, one per purchase as well.</summary>
        private static bool IsSingleType(int type) => type == 0x12;

        private RoItemInstance[] Inventory() =>
            session != null ? session.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>() : Array.Empty<RoItemInstance>();

        private RoPlayerStatusSnapshot Status() =>
            session != null ? session.InventorySnapshot().Status ?? RoPlayerStatusSnapshot.Empty : RoPlayerStatusSnapshot.Empty;

        /// <summary>0x7a4260: how many of an item the bag holds (worn pieces are not in the client's item list).</summary>
        private static long InventoryAmount(RoItemInstance[] inventory, uint itemId) {
            long amount = 0;
            foreach (RoItemInstance item in inventory) {
                if (item != null && !item.IsEquipped && (uint) item.ItemId == itemId) amount += Math.Max(0, item.Count);
            }

            return amount;
        }

        /// <summary>0xd02fa0: the ammo slot or any worn equipment / costume holds the item.</summary>
        private static bool IsWorn(RoItemInstance[] inventory, uint itemId) {
            foreach (RoItemInstance item in inventory) {
                if (item != null && item.IsEquipped && (uint) item.ItemId == itemId) return true;
            }

            return false;
        }

        /// <summary>0xce99c0: the server index of the first bag item with the id (0 when there is none).</summary>
        private static ushort FirstInventoryIndex(RoItemInstance[] inventory, uint itemId) {
            foreach (RoItemInstance item in inventory) {
                if (item != null && !item.IsEquipped && (uint) item.ItemId == itemId && item.BagIndex >= 0 && item.BagIndex <= ushort.MaxValue) {
                    return (ushort) item.BagIndex;
                }
            }

            return 0;
        }

        private const RoItemEquipLocation CostumeLocations = RoItemEquipLocation.CostumeHeadTop | RoItemEquipLocation.CostumeHeadMid |
            RoItemEquipLocation.CostumeHeadBottom | RoItemEquipLocation.CostumeRobe | RoItemEquipLocation.CostumeFloor;

        /// <summary>
        /// The possession count of the purchase checks (0x7a2e90 step 1): the bag's items (+0x16c4) plus the distinct ids
        /// of the worn equipment (0xd297e0) and of the worn costumes (0xd296d0).
        /// </summary>
        private static int PossessionCount(RoItemInstance[] inventory) {
            int count = 0;
            var equip = new HashSet<int>();
            var costume = new HashSet<int>();
            foreach (RoItemInstance item in inventory) {
                if (item == null) continue;
                if (!item.IsEquipped) count++;
                else if ((item.WearState & CostumeLocations) != 0) costume.Add(item.ItemId);
                else equip.Add(item.ItemId);
            }

            return count + equip.Count + costume.Count;
        }

        /// <summary>The ITEM_INFO a shop row draws with (0xa14ba0): identified, the wear mask from the packet's location.</summary>
        private static RoItemInstance ShopItem(uint itemId, int typeRaw, int amount, uint location = 0, int refine = 0) {
            int id = unchecked((int) itemId);
            var where = (RoItemEquipLocation) location;
            return refine > 0
                ? RoItemInstance.CreateEquipment(-1, id, Math.Max(1, amount), true, where, refineLevel: refine, itemTypeRaw: unchecked((byte) typeRaw))
                : RoItemInstance.CreateRegular(-1, id, amount, true, where, itemTypeRaw: unchecked((byte) typeRaw));
        }

        private string ItemName(uint itemId) => ShopItem(itemId, 0, 1).GetFullDisplayName(metadata) ?? itemId.ToString(CultureInfo.InvariantCulture);

        /// <summary>A UIBitmapButton with its three bitmaps (normal, _a hover, _b pressed), 42 × 20.</summary>
        private static RoClassicChildLayout BitmapButton(int id, string key, string bitmap, int x, int y) {
            var b = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = key, X = x, Y = y, W = 42, H = 20 };
            b.States["normal"] = Ui + bitmap + ".bmp";
            b.States["hover"] = Ui + bitmap + "_a.bmp";
            b.States["pressed"] = Ui + bitmap + "_b.bmp";
            return b;
        }

        private void Place(RoClassicWindow window, int x, int y) {
            Vector2Int client = system.ClientSize;
            window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - window.Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - window.Height)));
        }

        private static void SavePosition(RoClassicWindow window, string prefsName) {
            window.Moved += w => {
                PlayerPrefs.SetInt(ShopPrefs + prefsName + ".x", w.Position.x);
                PlayerPrefs.SetInt(ShopPrefs + prefsName + ".y", w.Position.y);
            };
        }

        /// <summary>The UIResizer at (W − 15, H − 15) (btn_resize.bmp, 13 × 13): its mode there, −1 elsewhere.</summary>
        private static void Resizer(RoClassicWindow window, int mode) {
            window.ResizerModeAt = local =>
                local.x >= window.Width - 15 && local.x < window.Width + 1 && local.y >= window.Height - 15 && local.y < window.Height + 1 ? mode : -1;
        }

        private static void DrawResizer(RoClassicWindow w) => w.DrawBitmap(w.Width - 15, w.Height - 15, Ui + "btn_resize.bmp");

        /// <summary>TitleBar 0x82f2d0's text: (19, 4) white under (18, 3) black.</summary>
        private static void DrawTitle(RoClassicWindow w, string title) {
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
        }

        /// <summary>Plain shop list sizes (msg 0xe v1 7): W = 280 + trunc32(clamp(w, 232, 320) − 280), H = 120 + trunc32(clamp(h, 120, 336) − 120).</summary>
        private static int ShopWidth(int w) => 280 + (Mathf.Clamp(w, 232, 320) - 280) / 32 * 32;

        private static int ShopHeight(int h) => 120 + (Mathf.Clamp(h, 120, 336) - 120) / 32 * 32;

        private static void SetOps(RoClassicWindow window, List<RoClassicOp> ops) {
            window.Layout.Ops.Clear();
            window.Layout.Ops.AddRange(ops);
            window.Invalidate();
        }

        /// <summary>
        /// The UIItemShopWnd frame (0x8e1140, also UIBartermarketWnd 0x7a5540): title bar, the item columns over H − 46,
        /// the 29 px button bar, the icon cells at x 40 and the white name area.
        /// </summary>
        private static List<RoClassicOp> ShopListOps(int w, int h) {
            var ops = new List<RoClassicOp>();
            int body = h - 46, rows = body / 32;
            RoClassicFrames.TitleBar(ops, w, false);
            RoClassicFrames.ItemColumns(ops, w, body);
            RoClassicFrames.ButtonBar(ops, w, h, 2);
            RoClassicFrames.IconColumn(ops, 40, rows);
            RoClassicFrames.Fill(ops, 72, 17, (w - 40) / 32 * 32 - 32, rows * 32, White);
            return ops;
        }

        /// <summary>The tooltip lines of a buy button (0x7a6260 / 0x7aaf60): at most 30, then "...".</summary>
        private static string JoinLines(List<string> lines) {
            var s = new StringBuilder();
            for (int i = 0; i < lines.Count; i++) {
                if (i == 30) {
                    s.Append("\n...");
                    break;
                }

                if (i > 0) s.Append('\n');
                s.Append(lines[i]);
            }

            return s.ToString();
        }

        /// <summary>Shows a buy button's tooltip at the button's screen position + (0, −16·n − 4).</summary>
        private static void ShowButtonLines(RoClassicWindow w, int bx, int by, List<string> lines) {
            if (lines.Count == 0) return;
            int n = Math.Min(lines.Count, 30) + (lines.Count > 30 ? 1 : 0);
            RoClassicTooltip.Shared.Show(JoinLines(lines), White, w.Position.x + bx, w.Position.y + by - 16 * n - 4);
        }

        private static bool Within(Vector2Int local, int x, int y, int w, int h) =>
            local.x >= x && local.x < x + w && local.y >= y && local.y < y + h;

        private static void SendSafely(Func<System.Threading.Tasks.Task> send, string what) {
            try {
                send();
            } catch (Exception ex) {
                Debug.LogWarning($"NPC {what} was not sent: {ex.Message}");
            }
        }

        /// <summary>Diagnostics: the count window answers with <paramref name="text"/> (when it is up).</summary>
        private static void AnswerCount(string[] a, int index) {
            if (index < a.Length && RoClassicCountWindow.Visible) {
                RoClassicCountWindow.SetText(a[index]);
                RoClassicCountWindow.Confirm();
            }
        }

        private static int Arg(string[] a, int i) =>
            i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
    }
}
