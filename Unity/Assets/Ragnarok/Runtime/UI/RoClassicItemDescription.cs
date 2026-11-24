using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// The item description window UIItemCollectionWnd (id 12, ui/RUNTIME-RULES-STORE-SHOP.md §2): one window, opened by
    /// a right click on an item (msg 0x18) and closed by a second right click on the same item. 280 wide: the
    /// collection picture at (10, 11), the name at (90, 10), the description in a text viewer at (91, 31) wrapping each
    /// line at 26 bytes; identified equipment adds the card row (175 tall), 18 px more per line beyond four, at most 450.
    /// The same class is the equip comparison 0xea (item-windows-spec §1: the worn counterpart left of 12 while "Compare
    /// equips" is on, comparison frames, no close, no drag, 12's alpha), the card description 0x52 (§3: a right press on a
    /// card cell, at (185, 300) on the 640 × 480 grid, not remembered) and its comparison twin 0xfa (§1j).
    /// </summary>
    internal sealed class RoClassicItemDescription : IRoClassicUiProducer {
        private const int W = 280;
        private const int MaxHeight = 450;
        private const int CloseId = 0xc5;
        private const int ViewId = 0xf6;
        private const int LineHeight = 18;
        private const int WrapBytes = 26;
        private const string Ui = "유저인터페이스\\";
        private const string Prefs = "ro.ui.classic.itemdesc.";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 DamagedShadow = new Color32(0xfa, 0x50, 0x50, 255);
        private static readonly Color32 Grey = new Color32(0x80, 0x80, 0x80, 255);
        private static readonly Color32 CardComparisonViewer = new Color32(0xc6, 0xdf, 0xf7, 255);
        private static readonly Color32[] NameColors = {
            new Color32(0, 0, 0, 255), new Color32(0x66, 0x99, 0x00, 255), new Color32(0x00, 0xbf, 0xff, 255),
            new Color32(0xff, 0xa5, 0x00, 255), new Color32(0x94, 0x00, 0xd3, 255),
        };

        public const int MainId = 12, ComparisonId = 0xea, CardId = 0x52, CardComparisonId = 0xfa;
        private static RoClassicItemDescription instance, comparison, card, cardComparison;

        /// <summary>"Compare equips" DAT_0155353e: the inventory's checkbox 0x1c4; off at every start, never saved.</summary>
        public static bool CompareEquips { get; set; }

        private readonly int id;
        private readonly RoClassicWindow window;
        private readonly RoClassicChildLayout view;
        private readonly List<(string Text, int Start, int End)> lines = new List<(string, int, int)>();
        private readonly List<List<string>> optionBlocks = new List<List<string>>();
        private readonly List<(int Start, int End)> wrap = new List<(int, int)>();
        private RoItemInstance item;
        private RoItemMetadata metadata;
        private RoItemMetadataTable table;
        private bool cardRow;
        private int optionsH;
        private int scroll;
        private int hoverCard = -1;
        private readonly List<TextLink> links = new List<TextLink>();
        private int hoverLink = -1;
        private int pressedLink = -1;
        private bool previewable;       // the Preview button 0x15e (msg 0x18, 0x85d6c9)
        private bool previewPressed;
        private bool v2;                // msg 0x18's second argument: no toggle-close, no illustration button
        private bool book;              // the Read / Auto Read sprite buttons 0x11b / 0x11c (bookitemnametable.txt, 0x660700)
        private int bookPressed = -1;
        private float bookFrameAt;
        private int bookFrame;

        private static readonly string[] LinkTags = { "URL", "ITEM", "TIPBOX", "NAVI", "QUEST" };
        private static readonly string[] PlainTags = { "ITEML", "NAVIL" };
        private static readonly Color32 UrlColor = new Color32(0x00, 0xa3, 0xe6, 255);
        private static readonly Color32 UrlHover = new Color32(0x00, 0xb7, 0xef, 255);
        private static readonly Color32 LinkColor = new Color32(0x4a, 0x93, 0xf4, 255);
        private static readonly Color32 LinkHover = new Color32(0x64, 0x78, 0xff, 255);

        /// <summary>A clickable label (UITextButton 0x7d0360) laid over a viewer line by a URL / ITEM / NAVI / TIPBOX / QUEST tag.</summary>
        private struct TextLink {
            public int Line;
            public int X;
            public int Width;
            public string Label;
            public string Info;
            public string Kind;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = comparison = card = cardComparison = null;
            CompareEquips = false;
        }

        private bool IsComparison => id == ComparisonId || id == CardComparisonId;

        private RoClassicItemDescription(RoClassicUiSystem system, int windowId) {
            id = windowId;
            var layout = new RoClassicWindowLayout { Class = id == ComparisonId ? "UIItemCollection_ComparisonWnd" : id == CardId ? "UICombinedCardItemCollectionWnd" :
                id == CardComparisonId ? "UICombinedCardItemCollection_ComparisonWnd" : "UIItemCollectionWnd", WindowId = id, Width = W, Height = 120 };
            // the comparisons hide their close button (OnCreate 0x83f210 / 0x83a500)
            var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = IsComparison ? -100 : W - 14, Y = 3, W = 11, H = 11 };
            close.States["normal"] = Ui + "basic_interface\\sys_close_off.bmp";
            close.States["hover"] = close.States["pressed"] = Ui + "basic_interface\\sys_close_on.bmp";
            view = new RoClassicChildLayout { Class = "UIBitmapButton", Id = ViewId, Path = "view", X = 6, Y = -94, W = 42, H = 20 };
            view.States["normal"] = Ui + "btn_view.bmp";
            view.States["hover"] = Ui + "btn_view_a.bmp";
            view.States["pressed"] = Ui + "btn_view_b.bmp";
            layout.Children.Add(view);
            layout.Children.Add(close);
            // LBtnDown drags the frame only right of the picture's column and above y 17; the comparisons' LBtnDown is empty
            window = new RoClassicWindow(system, layout) { FrameDragArea = local => !IsComparison && local.x > 60 && local.y < 17, Visible = false };
            window.DrawDynamic += Draw;
            window.DrawDynamic += DrawBookButtons;
            window.Clicked += OnClicked;
            window.EscClose = () => {
                Delete(id);
                return true;
            };
            window.RightClicked += (w, local) => CardPressed(local);
            window.Wheeled += (w, local, delta) => Scroll(delta > 0 ? -1 : 1);
            window.ClickableAt = local => LinkAt(local) >= 0 || BookButtonAt(local) >= 0;
            // the resizer (mode 7, height only): 16×16 at (W − 17, H − options − 17 | 46)
            window.ResizerModeAt = local => {
                int rx = W - 17, ry = window.Height - optionsH - (cardRow ? 46 : 17);
                return item != null && local.x >= rx && local.x < rx + 16 && local.y >= ry && local.y < ry + 16 ? 7 : -1;
            };
            window.ResizeRequested += (w, mode, width, height) => {
                if (mode != 7 || item == null) return;
                int h = ClampHeight(height);   // SetHeight 0x8570f0 (msg 0xe, v1 7)
                if (h == window.Height) return;
                window.SetHeight(h);
                scroll = Mathf.Clamp(scroll, 0, Math.Max(0, lines.Count - VisibleLines));
                window.Invalidate();
            };
            window.Pressed += (w, local) => {
                bookPressed = BookButtonAt(local);
                pressedLink = LinkAt(local);
                if (previewable && PreviewAt(local)) {
                    previewPressed = true;
                    w.Invalidate();
                }
            };
            window.Released += (w, local) => {
                int bookButton = BookButtonAt(local);
                if (bookButton >= 0 && bookButton == bookPressed && item != null) {
                    // 0x11b / 0x11c: the book's file checked, then window 0x6a (msg 0x5e(itemId, button, 0))
                    bookPressed = -1;
                    RoClassicBook.Open(item.ItemId, bookButton == 0 ? 0x11b : 0x11c, 0);
                    return;
                }

                bookPressed = -1;
                if (previewPressed) {
                    previewPressed = false;
                    w.Invalidate();
                    // msg 6 id 0x15e: MakeWindow(0x126) → SendMsg 0x17 with the shown item; this window stays
                    if (PreviewAt(local) && item != null) RoClassicPreviewEquip.Preview(item, metadata, null);
                    return;
                }

                int link = LinkAt(local);
                if (link >= 0 && link == pressedLink) ActivateLink(links[link]);
                pressedLink = -1;
            };
            window.Moved += w => {
                // 12's Move (0x8369d0) keeps 0xea at its left; 0x52's (0x836980) keeps 0xfa there
                if (id == MainId) {
                    PlayerPrefs.SetInt(Prefs + "x", w.Position.x);
                    PlayerPrefs.SetInt(Prefs + "y", w.Position.y);
                    if (comparison != null && comparison.window.Visible) comparison.window.MoveTo(w.Position.x - W, w.Position.y);
                } else if (id == CardId) {
                    // the original keeps 0x52's rect too (registry COMBINEDCARDITEMCOLLECTIONWNDINFO.X / .Y)
                    PlayerPrefs.SetInt(Prefs + "card.x", w.Position.x);
                    PlayerPrefs.SetInt(Prefs + "card.y", w.Position.y);
                    if (cardComparison != null && cardComparison.window.Visible) cardComparison.window.MoveTo(w.Position.x - W, w.Position.y);
                }
            };
            system.Register(this);
        }

        public static bool IsOpen => instance != null && instance.window.Visible;

        private static RoClassicItemDescription Make(ref RoClassicItemDescription slot, RoClassicUiSystem system, int windowId) {
            if (slot == null) slot = new RoClassicItemDescription(system, windowId);
            return slot;
        }

        /// <summary>DeleteWindow: 12 takes 0xea with it, 0xea takes 12 (0x9bde6d / 0x9bdead), 0x52's destructor takes 0xfa.</summary>
        private static void Delete(int windowId) {
            RoClassicItemDescription d = windowId == MainId ? instance : windowId == ComparisonId ? comparison : windowId == CardId ? card : cardComparison;
            if (d == null || !d.window.Visible) return;
            d.window.Visible = false;
            d.item = null;
            if (windowId == MainId) Delete(ComparisonId);
            else if (windowId == ComparisonId) Delete(MainId);
            else if (windowId == CardId) Delete(CardComparisonId);
        }

        /// <summary>
        /// msg 0x18: shows <paramref name="item"/>, or closes the window when it already shows the same item (id,
        /// refine and cards).
        /// </summary>
        public static void Toggle(RoItemInstance item, RoItemMetadataTable table) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (item == null || system == null) return;
            Make(ref instance, system, MainId).Msg18(system, item, table, false);
        }

        public static void Close() => Delete(MainId);

        /// <summary>
        /// msg 0x18 (0x85c310): the same item again with v2 = 0 closes the window; else it shows the item. Window 12 then
        /// opens the comparison 0xea with the worn counterpart (option on), or deletes it, which deletes 12 as well.
        /// </summary>
        private void Msg18(RoClassicUiSystem system, RoItemInstance shown, RoItemMetadataTable source, bool second) {
            if (!second && window.Visible && item != null && item.ItemId == shown.ItemId && item.RefineLevel == shown.RefineLevel && SameCards(item, shown)) {
                Delete(id);
                return;
            }

            v2 = second;
            Show(system, shown, source);
            if (id != MainId) return;
            RoItemInstance worn = FindWornForCompare(shown);
            if (CompareEquips && worn != null) {
                RoClassicItemDescription c = Make(ref comparison, system, ComparisonId);
                // MakeWindow(0xea): msg 0x22 puts it at (12.x - 12.w, 12.y), then msg 0x18 with v2 = 1
                c.window.MoveTo(window.Position.x - W, window.Position.y);
                c.Msg18(system, worn, source, true);
            } else if (comparison != null && comparison.window.Visible) {
                Delete(ComparisonId);
            }
        }

        /// <summary>0xce8a50: the worn item a described one is compared with (its wear mask's slot), null for worn gear and non-gear.</summary>
        private static RoItemInstance FindWornForCompare(RoItemInstance item) {
            RoItemInstance[] inventory = RoClassicEntryQueue.CurrentSession?.EquipmentSnapshot()?.InventoryItems;
            if (item == null || inventory == null) return null;
            if (item.WearState != RoItemEquipLocation.None) {
                if (item.WearState != RoItemEquipLocation.Ammo) return null;
                foreach (RoItemInstance ammo in inventory) {
                    if (ammo != null && ammo.WearState == RoItemEquipLocation.Ammo) return ammo.BagIndex == item.BagIndex ? null : ammo;
                }

                return null;
            }

            int slot;
            switch ((int) item.EquipLocationHint) {
                case 0x1: slot = 0x1; break;
                case 0x2: case 0x22: slot = 0x2; break;
                case 0x4: slot = 0x4; break;
                case 0x88: slot = 0x8; break;
                case 0x10: slot = 0x10; break;
                case 0x20: slot = 0x20; break;
                case 0x40: slot = 0x40; break;
                case 0x100: case 0x101: case 0x300: case 0x301: slot = 0x100; break;
                case 0x200: case 0x201: slot = 0x200; break;
                case 0x400: case 0xc00: case 0x1400: case 0x1c00: slot = 0x400; break;
                case 0x800: case 0x1800: slot = 0x800; break;
                case 0x1000: slot = 0x1000; break;
                case 0x2000: slot = 0x2000; break;
                case 0x10000: case 0x20000: case 0x40000: case 0x80000: case 0x100000: case 0x200000: slot = (int) item.EquipLocationHint; break;
                default: return null;
            }

            foreach (RoItemInstance worn in inventory) {
                if (worn != null && ((int) worn.WearState & slot) != 0 && worn.Count != 0) return worn;
            }

            return null;
        }

        /// <summary>
        /// RBtnDown on a card cell (0x833ca0 / 0x855cf0): x' = x - 3, below H - 26, n = the item's slots (forged) or 4; a
        /// card there opens 0x52 for it (v2 = past the slots), and from 12 with the comparison open also 0xfa for the
        /// comparison's card in the same cell.
        /// </summary>
        private void CardPressed(Vector2Int local) {
            if (item == null || !cardRow || local.y <= window.Height - 26 || local.x < 3) return;
            int n = IsForged() ? metadata.SlotCount : RoItemInstance.MaxCardSlots, i = (local.x - 3) / 25;
            if (n < 1 || i >= n || item.GetCardId(i) <= 0) return;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            RoClassicItemDescription c = Make(ref card, system, CardId);
            c.Msg18(system, RoItemInstance.CreateRegular(-1, item.GetCardId(i), 1, true), table, i >= metadata.SlotCount);
            if (id != MainId || comparison == null || !comparison.window.Visible || comparison.item == null) return;
            int other = comparison.item.GetCardId(i);
            if (other == 0 || other == 0xfe || other == 0xff) return;
            Make(ref cardComparison, system, CardComparisonId).Msg18(system, RoItemInstance.CreateRegular(-1, other, 1, true), table, true);
        }

        private static bool SameCards(RoItemInstance a, RoItemInstance b) {
            for (int i = 0; i < RoItemInstance.MaxCardSlots; i++) {
                if (a.GetCardId(i) != b.GetCardId(i)) return false;
            }

            return true;
        }

        private void Show(RoClassicUiSystem system, RoItemInstance shown, RoItemMetadataTable source) {
            item = shown;
            table = source ?? RoItemMetadataTable.Empty;
            metadata = item.ResolveMetadata(table) ?? table.GetItemOrFallback(item.ItemId);
            BuildOptions();
            // identified equipment (not a pet egg 9001..9499 nor pet armour 10000..10999)
            cardRow = item.IsIdentified && !(item.ItemId >= 9001 && item.ItemId <= 9499 || item.ItemId >= 10000 && item.ItemId <= 10999) &&
                metadata.ItemType != RoItemType.Card && IsCardSocketContainer(item, metadata);
            view.Y = v2 ? -100 : !string.IsNullOrWhiteSpace(metadata.CardIllustrationResourceName) ? 6 : -94;
            book = RoClassicBook.IsBook(item.ItemId);
            bookFrame = 0;
            BuildLines();
            previewable = RoClassicPreviewEquip.CanPreview(item, metadata);

            int h = ClampHeight(cardRow ? optionsH + 149 : optionsH + 120);
            int n = lines.Count;
            if (n > 4) h = ClampHeight(h + LineHeight * (n - 4));
            window.SetHeight(h);
            scroll = 0;
            hoverCard = -1;

            if (!window.Visible) {
                Vector2Int client = system.ClientSize;
                if (id == MainId) {
                    // MakeWindow(12): (185, 300) on the 640×480 grid, or where the player left it
                    int x = PlayerPrefs.GetInt(Prefs + "x", (client.x - 640) / 2 + 185), y = PlayerPrefs.GetInt(Prefs + "y", 300 * client.y / 480);
                    window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - W)), Mathf.Clamp(y, 0, Math.Max(0, client.y - h)));
                } else if (id == CardId) {
                    // a new 0x52: where the player left it (COMBINEDCARDITEMCOLLECTIONWNDINFO), else (185, 300) scaled
                    int x = PlayerPrefs.GetInt(Prefs + "card.x", (client.x - 640) / 2 + 185), y = PlayerPrefs.GetInt(Prefs + "card.y", 300 * client.y / 480);
                    window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - W)), Mathf.Clamp(y, 0, Math.Max(0, client.y - h)));
                } else if (id == CardComparisonId) {
                    // a new 0xfa: (185, 300) scaled, no msg 0x22 (it starts where a fresh 0x52 would)
                    window.MoveTo((client.x - 640) / 2 + 185, 300 * client.y / 480);
                }

                window.Visible = true;
            }

            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window); // MakeWindow(12): the Esc list front
            window.Invalidate();
        }

        /// <summary>SetHeight 0x8570f0's clamp: at least 120 (175 with the card row) plus the option blocks, at most 450.</summary>
        private int ClampHeight(int h) => Mathf.Clamp(h, Math.Max(cardRow ? 175 : 120, previewable ? 172 : 0) + optionsH, MaxHeight);

        private int ViewerHeight => window.Height - optionsH - (cardRow ? 69 : 40);

        private int VisibleLines => Math.Max(1, ViewerHeight / LineHeight);

        /// <summary>The option blocks: each option's text wrapped at 254 px, 15·lines + 14 tall.</summary>
        private void BuildOptions() {
            optionBlocks.Clear();
            optionsH = 0;
            if (!item.HasRandomOptions) return;
            RoClassicGdiFont font = RoClassicUiSystem.Instance.Font(12);
            for (int i = 0; i < RoItemInstance.MaxRandomOptions; i++) {
                RoItemRandomOption option = item.GetRandomOption(i);
                if (option.IsEmpty || option.Index == 0) continue;
                var block = new List<string>();
                string text = FormatRandomOption(option);
                int start = 0;
                while (start < text.Length) {
                    int end = start + 1;
                    while (end < text.Length && font.Measure(text.Substring(start, end + 1 - start)) <= 254) end++;
                    block.Add(text.Substring(start, end - start));
                    start = end;
                }

                optionBlocks.Add(block);
                optionsH += 15 * block.Count + 14;
            }
        }

        /// <summary>The viewer's lines: rental expiry (wrap 32), the description lines (wrap 26 each), an egg's intimacy.</summary>
        private void BuildLines() {
            lines.Clear();
            links.Clear();
            hoverLink = pressedLink = -1;
            if (item.HireExpireDate > 0) {
                DateTime expire = DateTimeOffset.FromUnixTimeSeconds(item.HireExpireDate).LocalDateTime;
                string date = expire.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture); // MsgStr 0x4ec "%m/%d %H:%M"
                AddLine(Format1(RoClassicUiHost.MsgStr(0x4e7, " ^ff0000Expiration Date: %s^000000"), date), 32);
            }

            IReadOnlyList<string> description = metadata.GetDescription(item.IsIdentified);
            if (description != null) {
                foreach (string line in description) AddTaggedLine(line ?? string.Empty, WrapBytes);
            }

            if (metadata.ItemType == RoItemType.PetEgg) {
                int[] names = { 0xb32, 0x2a0, 0x2a1, 0x29d, 0x2a2, 0x2a3 };
                int intimacy = item.GetCardId(2);
                int msg = intimacy >= 1 && intimacy <= 5 ? names[intimacy] : 0xb32;
                AddLine(Format1(RoClassicUiHost.MsgStr(0xb31, "^0000CCIntimacy:^000000 %s"), RoClassicUiHost.MsgStr(msg, "Unknown")), WrapBytes);
            }
        }

        private static string Format1(string format, string arg) {
            int at = format.IndexOf("%s", StringComparison.Ordinal);
            return at < 0 ? format : format.Substring(0, at) + arg + format.Substring(at + 2);
        }

        private void AddLine(string text, int wrapBytes) {
            text = text.Replace("\r", string.Empty);
            if (text.Length == 0) {
                lines.Add((text, 0, 0));
                return;
            }

            RoClassicWindow.WrapBytes(text, wrapBytes, wrap);
            foreach ((int start, int end) in wrap) lines.Add((text, start, end));
        }

        /// <summary>
        /// UITextViewer msg 0x1d (0x7fae60) with link tags (0x9b1ec0: URL, ITEM, TIPBOX, NAVI, ITEML, QUEST): literal
        /// "\n" / "\r" become line breaks, every tag and each &lt;INFO&gt;…&lt;/INFO&gt; is removed (the label stays,
        /// brackets and all; ITEML / NAVIL keep only their text), the rest wraps as usual and each link label gets a
        /// text button at x = 2 + the width before it, y = 3 + 18·row (ui/RUNTIME-RULES-TEXT-TAGS.md).
        /// </summary>
        private void AddTaggedLine(string text, int wrapBytes) {
            bool tagged = text.IndexOf("<ITEML>", StringComparison.Ordinal) >= 0;
            foreach (string tag in LinkTags) tagged |= text.IndexOf("<" + tag + ">", StringComparison.Ordinal) >= 0;
            if (!tagged) {
                AddLine(text, wrapBytes);
                return;
            }

            text = text.Replace("\\n", "\n").Replace("\\r", "\r");
            var display = new System.Text.StringBuilder(text.Length);
            var found = new List<(int Start, int End, string Kind, string Info)>();
            int open = -1;
            string openKind = null, openInfo = null;
            for (int i = 0; i < text.Length;) {
                int close = text[i] == '<' ? text.IndexOf('>', i + 1) : -1;
                if (close > i) {
                    string tag = text.Substring(i + 1, close - i - 1);
                    if (tag == "INFO") {
                        int end = text.IndexOf("</INFO>", close + 1, StringComparison.Ordinal);
                        if (end < 0) end = text.Length;
                        openInfo = text.Substring(close + 1, end - close - 1);
                        i = Math.Min(text.Length, end + 7);
                        continue;
                    }

                    bool closing = tag.StartsWith("/", StringComparison.Ordinal);
                    string name = closing ? tag.Substring(1) : tag;
                    if (Array.IndexOf(LinkTags, name) >= 0) {
                        if (!closing) {
                            open = display.Length;
                            openKind = name;
                            openInfo = null;
                        } else if (openKind == name) {
                            found.Add((open, display.Length, name, openInfo ?? string.Empty));
                            openKind = null;
                        }

                        i = close + 1;
                        continue;
                    }

                    if (Array.IndexOf(PlainTags, name) >= 0) {
                        i = close + 1;
                        continue;
                    }
                }

                if (text[i] != '\r') display.Append(text[i]);
                i++;
            }

            string shown = display.ToString();
            int first = lines.Count;
            AddLine(shown, wrapBytes);
            RoClassicGdiFont font = RoClassicUiSystem.Instance.Font(12);
            foreach ((int start, int end, string kind, string info) in found) {
                for (int j = first; j < lines.Count; j++) {
                    (string _, int lineStart, int lineEnd) = lines[j];
                    if (start < lineStart || (start >= lineEnd && j + 1 < lines.Count)) continue;
                    string label = PlainText(shown.Substring(start, end - start));
                    links.Add(new TextLink {
                        Line = j, X = 2 + font.Measure(PlainText(shown.Substring(lineStart, start - lineStart))), Width = font.Measure(label),
                        Label = label, Info = info, Kind = kind,
                    });
                    break;
                }
            }
        }

        /// <summary>The text without its ^RRGGBB colour codes and line breaks.</summary>
        private static string PlainText(string s) {
            var plain = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == '^' && i + 7 <= s.Length && IsHex(s, i + 1, 6)) {
                    i += 6;
                    continue;
                }

                if (s[i] != '\n') plain.Append(s[i]);
            }

            return plain.ToString();
        }

        private static bool IsHex(string s, int start, int count) {
            for (int i = start; i < start + count; i++) {
                if (!Uri.IsHexDigit(s[i])) return false;
            }

            return true;
        }

        /// <summary>The link under a window-local point (its button: label width + 1 by 14), visible rows only.</summary>
        private int LinkAt(Vector2Int local) {
            int visible = VisibleLines;
            for (int k = 0; k < links.Count; k++) {
                int row = links[k].Line - scroll;
                if (row < 0 || row >= visible) continue;
                int bx = 91 + links[k].X, by = 31 + 3 + LineHeight * row;
                if (local.x >= bx && local.x < bx + links[k].Width + 1 && local.y >= by && local.y < by + 14) return k;
            }

            return -1;
        }

        /// <summary>Click (msg 0x62): URL opens the INFO address (up to its first comma) at once; ITEM describes the item; TIPBOX shows the tip.</summary>
        private void ActivateLink(TextLink link) {
            string info = link.Info ?? string.Empty;
            int comma = info.IndexOf(',');
            string first = (comma >= 0 ? info.Substring(0, comma) : info).Trim();
            if (first.Length == 0) return;
            switch (link.Kind) {
                case "URL":
                    Application.OpenURL(first);
                    break;
                case "ITEM":
                    if (int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId) && itemId > 0) {
                        Toggle(RoItemInstance.CreateRegular(-1, itemId, 0, true), table);
                    }

                    break;
                case "TIPBOX":
                    if (int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tipId)) RoClassicTipBox.ShowTip(tipId);
                    break;
            }
        }

        private void Scroll(int delta) {
            int s = Mathf.Clamp(scroll + delta, 0, Math.Max(0, lines.Count - VisibleLines));
            if (s == scroll) return;
            scroll = s;
            window.Invalidate();
        }

        // ---- OnDraw 0x84e010 ----

        private void Draw(RoClassicWindow w) {
            if (item == null) return;
            int H = w.Height;
            int body = H - (item.IsIdentified && optionBlocks.Count > 0 ? optionsH : 0);
            if (cardRow) body -= 29;
            int grade = item.EnchantGrade;
            string prefix = grade > 0 ? Ui + "sysbox_g" + grade + "_" : Ui + "sysbox_";
            string bg = grade > 0 ? Ui + "basic_interface\\collection_bg_g" + grade + ".bmp" : Ui + "basic_interface\\collection_bg.bmp";
            // the comparisons (§1g): their own frame and background whatever the grade
            if (IsComparison) {
                prefix = Ui + "basic_interface\\sysbox_comparison_";
                bg = Ui + "basic_interface\\collection_comparison_bg.bmp";
                grade = 0;
            }

            // the option and card-row frames and the empty / disabled cells change for 0xea only
            string boxes = id == ComparisonId ? Ui + "basic_interface\\sysbox_comparison_" : Ui + "sysbox_";
            string cells = id == ComparisonId ? Ui + "basic_interface\\comparison_" : Ui;
            w.DrawSysBox(prefix, 0, 0, W, body, grade <= 0);
            w.DrawBitmap(0, 0, bg);
            w.DrawBitmap(10, 11, Ui + "collection\\" + metadata.GetResourceName(item.IsIdentified) + ".bmp");
            DrawName(w, 90, 10);

            if (cardRow) {
                int y = body;
                if (item.IsIdentified) {
                    foreach (List<string> block in optionBlocks) {
                        w.DrawSysBox(boxes, 0, y, W, 15 * block.Count + 14, true);
                        y += 8;
                        for (int i = 0; i < block.Count; i++) {
                            w.TextOut(12, y, block[i], 0, 12, Black);
                            y += i + 1 < block.Count ? 15 : 11;
                        }

                        y += 10;
                    }
                }

                if (!IsForged()) {
                    w.DrawSysBox(boxes, 0, y, W, 29, true);
                    int slots = metadata.SlotCount;
                    for (int i = 0; i < RoItemInstance.MaxCardSlots; i++) {
                        int card = item.GetCardId(i);
                        int x = 4 + 25 * i;
                        RoItemMetadata cardMeta = card > 0 ? table.GetItemOrFallback(card) : null;
                        if (cardMeta != null) w.DrawBitmap(x, y + 2, Ui + "item\\" + cardMeta.GetResourceName(true) + ".bmp");
                        else w.DrawBitmap(x, y + 2, cells + (i < slots ? "empty_card_slot.bmp" : "disable_card_slot.bmp"));
                    }
                }
            }

            // the text viewer child (91, 31), (W − 98) × (H − options − 40 | 69), white, lines at (3, 4 + 18·i)
            int vh = ViewerHeight, vw = W - 98;
            w.FillRect(91, 31, vw, vh, id == CardComparisonId ? CardComparisonViewer : White);
            w.ClipTo(91, 31, vw, vh);
            Color32 color = Black;
            int visible = VisibleLines;
            for (int i = 0; i < lines.Count && i < scroll + visible + 1; i++) {
                (string s, int start, int end) = lines[i];
                while (end > start && (s[end - 1] == '\n' || s[end - 1] == '\r')) end--;
                if (i < scroll) {
                    RoClassicWindow.ScanColourCodes(s, start, end, ref color); // codes of the lines scrolled past still apply
                    continue;
                }

                w.TextOutColored(94, 35 + LineHeight * (i - scroll), s, start, end, 0, 12, ref color);
            }

            // the link buttons: the label again at +1, +1 in the link colour, a 1 px underline on button row 13
            for (int k = 0; k < links.Count; k++) {
                int row = links[k].Line - scroll;
                if (row < 0 || row >= visible) continue;
                int bx = 91 + links[k].X, by = 31 + 3 + LineHeight * row;
                int x0 = Math.Max(bx, 91), y0 = Math.Max(by, 31), x1 = Math.Min(bx + links[k].Width + 1, 91 + vw), y1 = Math.Min(by + 14, 31 + vh);
                if (x1 <= x0 || y1 <= y0) continue;
                Color32 c = links[k].Kind == "URL" ? (k == hoverLink ? UrlHover : UrlColor) : (k == hoverLink ? LinkHover : LinkColor);
                w.ClipTo(x0, y0, x1 - x0, y1 - y0);
                w.TextOut(bx + 1, by + 1, links[k].Label, 0, 12, c);
                w.FillRect(bx, by + 13, links[k].Width + 1, 1, c);
                w.ClipTo(91, 31, vw, vh);
            }

            w.DrawScrollBar(91 + vw - 13, 31, vh, Math.Max(0, lines.Count - visible), lines.Count, visible, scroll);
            w.ResetClip();
            // the resizer (mode 7, height only)
            w.DrawBitmap(W - 17, H - optionsH - (cardRow ? 46 : 17), Ui + "btn_resize.bmp");
            if (previewable) RoClassicPreviewEquip.DrawBasicButton(w, 9, 117, 77, RoClassicUiHost.MsgStr(0xb6a, "Preview"), previewPressed);
        }

        private const string BookSprite = "data/sprite/book/책읽기";

        /// <summary>The book buttons (UISpriteButton, 책읽기 sized to its frame) at (6 + i·w, 6) for a book (msg 0x18), else parked.</summary>
        private int BookButtonAt(Vector2Int p) {
            if (!book || item == null) return -1;
            RoClassicActSprite s = RoClassicActSprite.Get(BookSprite);
            if (s == null || !s.Ready) return -1;
            Vector2Int size = s.Size(0, 0);
            for (int i = 0; i < 2; i++) {
                if (new RectInt(6 + i * size.x, 6, size.x, size.y).Contains(p)) return i;
            }

            return -1;
        }

        /// <summary>UISpriteButton OnDraw 0x7bec60: Read on frame 0, Auto Read (+0xc4 set for a book) one frame further each draw.</summary>
        private void DrawBookButtons(RoClassicWindow w) {
            if (!book || item == null) return;
            RoClassicActSprite s = RoClassicActSprite.Get(BookSprite);
            if (s == null || !s.Ready) return;
            Vector2Int size = s.Size(0, 0);
            int frames = Math.Max(1, s.FrameCount(0));
            s.DrawIn(w, 0, 0, 6 + size.x / 2, 6 + size.y / 2);
            s.DrawIn(w, 0, bookFrame % frames, 6 + size.x + size.x / 2, 6 + size.y / 2);
        }

        private static bool PreviewAt(Vector2Int p) => p.x >= 9 && p.x < 9 + 77 && p.y >= 117 && p.y < 137;

        /// <summary>Forged / named items (0x660780) keep their card row empty: card 0 is 0x00ff, 0x00fe or 0xff00.</summary>
        private bool IsForged() {
            int first = item.GetCardId(0);
            return first == 0x00ff || first == 0x00fe || first == 0xff00;
        }

        /// <summary>
        /// ItemName 0x82dc10: the name with a 1 px shadow at (+1, +1), font 0 h12, cut to W − x − 10 with "...". Colour:
        /// the enchant colour (black without) on a white shadow; a damaged item black (grey unidentified) on #fa5050.
        /// </summary>
        private void DrawName(RoClassicWindow w, int x, int y) {
            string name = item.GetFullDisplayName(metadata, table) ?? string.Empty;
            Color32 color, shadow;
            if (item.IsDamaged) {
                color = item.IsIdentified ? Black : Grey;
                shadow = DamagedShadow;
            } else {
                color = NameColor();
                shadow = White;
            }

            RoClassicGdiFont font = w.FontFor(0, 12);
            int width = W - x - 10;
            if (font.Measure(name) > width) {
                int cut = name.Length;
                while (cut > 0 && font.Measure(name.Substring(0, cut) + "...") > width) cut--;
                name = name.Substring(0, cut) + "...";
            }

            w.TextOut(x + 1, y + 1, name, 0, 12, shadow);
            w.TextOut(x, y, name, 0, 12, color);
        }

        /// <summary>FUN_0065d2e0: the enchant count (cards past the item's own slots) picks the colour.</summary>
        private Color32 NameColor() {
            if (!item.IsIdentified) return Black;
            int enchants = 0;
            for (int i = metadata.SlotCount; i < RoItemInstance.MaxCardSlots; i++) {
                if (item.GetCardId(i) > 0) enchants++;
            }

            return NameColors[Mathf.Clamp(enchants, 0, NameColors.Length - 1)];
        }

        private void OnClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case CloseId:
                    Delete(id);
                    break;
                case ViewId:
                    // MakeWindow(0x2f) -> msg 0x3e(id string, identified)
                    if (item != null) RoClassicCardIllust.Toggle(item, metadata, table);
                    break;
            }
        }

        // ---- per frame: card cell tooltips ----

        public void ClassicUiTick(RoClassicUiSystem system) {
            if (item == null || !window.Visible) return;
            if (book) {
                // OnProcess 0x8556f0: the Auto Read button redrawn every 100 ms, a frame further each draw
                if (Time.unscaledTime > bookFrameAt + 0.1f) {
                    bookFrameAt = Time.unscaledTime;
                    bookFrame++;
                    window.Invalidate();
                }

                int b = system.Windows.HoverWindow == window ? BookButtonAt(system.Windows.Mouse - window.Position) : -1;
                if (b >= 0) {
                    RoClassicTooltip.Shared.Show(b == 0 ? RoClassicUiHost.MsgStr(0x50e, "Read") : RoClassicUiHost.MsgStr(0x50f, "Auto Read"), White,
                        system.Windows.Mouse.x + 10, system.Windows.Mouse.y + 10);
                }
            }

            // 12's / 0x52's LBtnDown and LBtnUp copy their alpha (0x80 while dragged) to 0xea / 0xfa
            RoClassicItemDescription leader = id == ComparisonId ? instance : id == CardComparisonId ? card : null;
            if (leader != null) window.Alpha = leader.window.Alpha;
            // card cells (0x833ca0): i = (x − 3)/25, y > H − 26, a card in it → its name above the cell, in the
            // enchant colour past the item's own slots
            int link = system.Windows.HoverWindow == window ? LinkAt(system.Windows.Mouse - window.Position) : -1;
            if (link != hoverLink) {
                hoverLink = link;
                window.Invalidate();
            }

            hoverCard = -1;
            if (!cardRow || system.Windows.HoverWindow != window) return;
            Vector2Int local = system.Windows.Mouse - window.Position;
            int i = (local.x - 3) / 25;
            if (local.x < 3 || i >= RoItemInstance.MaxCardSlots || local.y <= window.Height - 26 || item.GetCardId(i) <= 0) return;
            hoverCard = i;
            RoItemMetadata cardMeta = table.GetItemOrFallback(item.GetCardId(i));
            RoClassicTooltip.Shared.Show(cardMeta.GetDisplayName(true), i >= metadata.SlotCount ? NameColor() : White,
                window.Position.x + 4 + 25 * i, window.Position.y + window.Height - 50);
        }

        /// <summary>
        /// Equipment that takes cards (the card row): weapons, armour and shadow gear by the table's or the server's type, else
        /// anything worn somewhere other than the ammunition slot; never usables, cash, eggs, pet armour, ammunition or misc.
        /// </summary>
        internal static bool IsCardSocketContainer(RoItemInstance inspectedItem, RoItemMetadata itemMetadata) {
            if (inspectedItem == null || itemMetadata == null) {
                return false;
            }

            RoItemType rawType = inspectedItem.ItemTypeRaw != 0 ? unchecked((RoItemType) inspectedItem.ItemTypeRaw) : RoItemType.Unknown;
            if (itemMetadata.ItemType == RoItemType.PetEgg ||
                itemMetadata.ItemType == RoItemType.PetArmor ||
                itemMetadata.ItemType == RoItemType.Ammo ||
                itemMetadata.ItemType == RoItemType.Etc ||
                itemMetadata.ItemType == RoItemType.Healing ||
                itemMetadata.ItemType == RoItemType.Usable ||
                itemMetadata.ItemType == RoItemType.DelayConsume ||
                itemMetadata.ItemType == RoItemType.Cash ||
                rawType == RoItemType.PetEgg ||
                rawType == RoItemType.PetArmor ||
                rawType == RoItemType.Ammo ||
                rawType == RoItemType.Etc ||
                rawType == RoItemType.Healing ||
                rawType == RoItemType.Usable ||
                rawType == RoItemType.DelayConsume ||
                rawType == RoItemType.Cash) {
                return false;
            }

            if (itemMetadata.ItemType == RoItemType.Weapon ||
                itemMetadata.ItemType == RoItemType.Armor ||
                itemMetadata.ItemType == RoItemType.ShadowGear ||
                rawType == RoItemType.Weapon ||
                rawType == RoItemType.Armor ||
                rawType == RoItemType.ShadowGear) {
                return true;
            }

            RoItemEquipLocation locations = inspectedItem.ResolveEquipLocations(itemMetadata);
            return locations != RoItemEquipLocation.None && locations != RoItemEquipLocation.Ammo;
        }

        private static readonly Dictionary<int, string> RandomOptionNames = new Dictionary<int, string> {
            { 1, "MaxHP +%d" },
            { 2, "MaxSP +%d" },
            { 3, "STR +%d" },
            { 4, "AGI +%d" },
            { 5, "VIT +%d" },
            { 6, "INT +%d" },
            { 7, "DEX +%d" },
            { 8, "LUK +%d" },
            { 9, "Atk +%d" },
            { 10, "Matk +%d" },
            { 11, "Def +%d" },
            { 12, "Mdef +%d" }
        };

        /// <summary>A random option's line: its name template with the value ("Option n +v" for an unknown index).</summary>
        internal static string FormatRandomOption(RoItemRandomOption option) {
            if (!RandomOptionNames.TryGetValue(option.Index, out string template)) {
                template = "Option " + option.Index + " +%d";
            }

            return template.Replace("%d", option.Value.ToString(CultureInfo.InvariantCulture)).Replace("%%", "%");
        }

        /// <summary>Diagnostics: what the window shows.</summary>
        public static string Describe() =>
            Describe(instance) + " | 0xea " + Describe(comparison) + " | 0x52 " + Describe(card) + " | 0xfa " + Describe(cardComparison) + " compare=" + CompareEquips;

        private static string Describe(RoClassicItemDescription d) {
            if (d == null || d.item == null || !d.window.Visible) return "closed";
            return $"item={d.item.ItemId} at={d.window.Position.x},{d.window.Position.y} size={W}x{d.window.Height} cardRow={d.cardRow} " +
                $"options={d.optionBlocks.Count}/{d.optionsH} lines={d.lines.Count} viewer={d.ViewerHeight} scroll={d.scroll} v2={d.v2}";
        }

        /// <summary>Sweep: the right press on card cell <paramref name="cell"/> of window 12 (or 0xea).</summary>
        /// <summary>Sweep: the view button of window 12 (or 0x52), and a move of 0x52.</summary>
        internal static void ViewForTests(bool card52) {
            RoClassicItemDescription d = card52 ? card : instance;
            if (d != null && d.window.Visible && d.view.Y >= 0) d.OnClicked(d.view);
        }

        internal static void MoveCardForTests(int x, int y) => card?.window.MoveTo(x, y);

        internal static void PressCardForTests(bool fromComparison, int cell) {
            RoClassicItemDescription d = fromComparison ? comparison : instance;
            if (d != null && d.window.Visible) d.CardPressed(new Vector2Int(5 + 25 * cell, d.window.Height - 10));
        }
    }
}
