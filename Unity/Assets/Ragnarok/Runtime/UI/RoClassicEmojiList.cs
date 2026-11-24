using System;
using System.Collections.Generic;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The "Emoji List", UICashEmotionListWnd 0x57 (docs/audits/chat-windows-2026-10-04.md §4.2, §8): 298 × 380, Alt+L
    /// (Interface 11, only while Basic Info exists) or the Shortcut List's view button 0x117 make or delete it.
    /// <list type="bullet">
    /// <item>OnDraw 0x7241c0: the title bar from x 29, 268 wide (0x82f440, MsgStr 0x21e), the body (0x82cd60), the search
    /// edit's frame (60, 22, 161, 18) #B5B5B5, the line (29, 46, 268, 1) #CECECE and the style-2 button bar (0x82d410);</item>
    /// <item>OnCreate 0x723a70: close (284, 3) "Alt+L", sys_base (32, 3), four UIBmpRadioBtn2 tabs
    /// <c>cashemotion\tap_{basic,bought,unbought,search}_{normal,press}</c> from (0, 46) down, arrow-back (36, 22) id 0xb9,
    /// search (221, 22) id 0xba, the Shortcut List button (252, 22) id 0xbb, buy (247, 355) id 0x20a, the search edit
    /// (61, 23) 160 × 16 of 20 bytes, the four 271 × 303 panes at (28, 47) and the status text (39, 359) hidden;</item>
    /// <item>the panes (UICashEmotionScrollWnd): Basic — the basic pack's emotions in its order list, 40 × 40 at
    /// (15 + 47·col, 4 + 47·row); Bought / Unbought — the owned / not owned packs (mgr +0x1a4 / +0x1b8), which only the owned
    /// packs packet fills — Custom's server sends none, so both stay empty as in the client; Search — rows of the found
    /// emotions (basic, bought, not bought) under their titles. A vertical UICashEmotionScrollBar (11 px, cashemotion art)
    /// at (W − 13, 0), grey while everything fits; wheel ±15 px, arrows ±135, the track ∓1.</item>
    /// <item>an emotion: hover → a #C8C8C8 rounded outline (0x7282d0); a click → CCashEmotionAction_Register on Basic (the
    /// record's text, e.g. "/lv", into the chat edit, or into the focused line of the open Shortcut List), Play on Search;
    /// a double click on Basic → CCashEmotionAction_Play (<see cref="RoEmotionSender.Play"/>).</item>
    /// </list>
    /// Buying (0x20a, packet 0x0bec) needs a pack chosen on Unbought, which never happens here: the button stays grey and
    /// nothing is sent.
    /// </summary>
    public sealed partial class RoClassicEmojiList : IRoClassicUiProducer {
        public const int WindowId = 0x57;
        private const int W = 298, H = 380;
        private const string Ui = "유저인터페이스\\";
        private const string Ce = Ui + "cashemotion\\";
        private const string BasicArt = Ui + "basic_interface\\";
        private const string Prefs = "ro.ui.classic.emojilist.";
        private const int CloseId = 0xc5;
        private const int PaneX = 28, PaneY = 47;
        private const int BarX = RoEmojiListRules.PaneWidth - RoEmojiListRules.BarWidth - 2;   // 0x71ff30: W − 11 − 2
        private const int TabY = 46, TabW = 30, TabH = 66;
        private static readonly string[] TabNames = { null, "basic", "bought", "unbought", "search" };
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Grey = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static readonly Color32 FrameGrey = new Color32(0xb5, 0xb5, 0xb5, 255);
        private static readonly Color32 LineGrey = new Color32(0xce, 0xce, 0xce, 255);
        private static readonly Color32 BarOutline = new Color32(0xbd, 0xce, 0xde, 255);
        private static readonly Color32 EditWhite = new Color32(255, 255, 255, 255);   // UIEditCtrl 0x7b1e50: no border, white
        private static readonly Color32 EditOff = new Color32(225, 225, 225, 255);

        // hit keys
        private const int HitNone = -1, HitTab = 100, HitArrowBack = 200, HitSearch = 201, HitShortcuts = 202, HitBuy = 203;
        private const int HitBarUp = 300, HitBarAbove = 301, HitThumb = 302, HitBarBelow = 303, HitBarDown = 304, HitPiece = 1000;

        private sealed class Piece {
            public int Pack, Id;
            public RectInt Rect;   // pane content coordinates
        }

        private sealed class Row {
            public string Title;
            public RectInt Rect;
        }

        private sealed class Pane {
            public readonly List<Piece> Pieces = new List<Piece>();
            public readonly List<Row> Rows = new List<Row>();
            public string SearchText = string.Empty;   // +0xc0 (vf55)
            public int Offset;                         // +0xa0
            public int Content;                        // vf53 0x90ef10
            public bool Scrolls => Content > RoEmojiListRules.PaneHeight;
        }

        private sealed class Button {
            public int Hit;
            public RectInt Rect;
            public string Art;          // "%s" format of the state names
            public bool HasDisable;
            public bool Enabled = true;
        }

        private static RoClassicEmojiList instance;
        private static RoLoginShellSession boundSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            boundSession = null;
        }

        /// <summary>The shell's session, handed over with the interface keys (an emotion's Play sends through it).</summary>
        public static void Bind(RoLoginShellSession session) => boundSession = session;

        private RoClassicUiSystem system;
        private RoLoginShellSession sessionSet;
        private RoLoginShellSession session => sessionSet ?? boundSession;
        private RoClassicWindow window;
        private RoClassicEditField searchEdit;
        private readonly Pane[] panes = { null, new Pane(), new Pane(), new Pane(), new Pane() };
        private readonly List<Button> buttons = new List<Button>(4);
        private int tab;                 // +0xb0
        private int hover = HitNone;
        private int pressed = HitNone;
        private bool swallowRelease;     // a double click's release is not a click (vf26 took it)
        private int thumbGrab = -1;      // the press point's distance into the thumb while dragging it
        private readonly List<(int Pack, int Id)> foundBasic = new List<(int, int)>(), foundBought = new List<(int, int)>(),
            foundUnbought = new List<(int, int)>();
        private readonly List<RectInt> extents = new List<RectInt>(96);

        private static RoClassicEmojiList Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicEmojiList { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Whether the window is open.</summary>
        public static bool IsOpen => instance?.window != null && instance.window.Visible;

        /// <summary>
        /// Alt+L (Interface 11, hotkey 0x6f) and the Shortcut List's 0x117: DeleteWindow(0x57) when it exists, else
        /// MakeWindow(0x57). Alt+L does nothing without Basic Info (the caller checks).
        /// </summary>
        public static bool Toggle(RoLoginShellSession session) {
            RoClassicEmojiList e = Ensure();
            if (e == null) return false;
            if (session != null) e.sessionSet = session;
            if (e.window != null && e.window.Visible) e.Close();
            else e.Open();
            return true;
        }

        private void Open() {
            if (window == null) Build();
            Vector2Int client = system.ClientSize;
            // MakeWindow(0x57): Move(scaledX(185), scaledY(400) − 60), then msg 0x22's remembered place
            int x = PlayerPrefs.GetInt(Prefs + "x", (client.x - 640) / 2 + 185), y = PlayerPrefs.GetInt(Prefs + "y", client.y * 400 / 480 - 60);
            window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - W)), Mathf.Clamp(y, 0, Math.Max(0, client.y - H)));
            // a fresh window: the panes filled anew, the Basic tab selected (SendMsg(6, 0xd3) for tab 1)
            for (int t = RoEmojiListRules.Basic; t <= RoEmojiListRules.Search; t++) {
                panes[t].SearchText = string.Empty;
                panes[t].Offset = 0;
                Fill(t, null);
            }

            tab = 0;
            SelectTab(RoEmojiListRules.Basic);
            window.Visible = true;
            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
        }

        private void Close() {
            if (window == null || !window.Visible) return;
            if (searchEdit.HasFocus) RoClassicTextInput.Focus(null);
            thumbGrab = -1;
            pressed = HitNone;
            window.Visible = false;
            system.Windows.RemoveEsc(window);
        }

        private void Build() {
            var layout = new RoClassicWindowLayout { Class = "UICashEmotionListWnd", WindowId = WindowId, Width = W, Height = H };
            // 0x82ba30("Alt+L"): sys_close at (W − 14, 3), id 0xc5
            var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = W - 14, Y = 3, W = 11, H = 11, Tooltip = "Alt+L" };
            close.States["normal"] = BasicArt + "sys_close_off.bmp";
            close.States["hover"] = BasicArt + "sys_close_on.bmp";
            close.States["pressed"] = BasicArt + "sys_close_on.bmp";
            layout.Children.Add(close);
            // 0x82b880: the sys_base button, moved to (32, 3)
            var sysBase = new RoClassicChildLayout { Class = "UIBitmapButton", Id = 0, Path = "base", X = 32, Y = 3, W = 11, H = 11 };
            sysBase.States["normal"] = BasicArt + "sys_base_off.bmp";
            sysBase.States["hover"] = BasicArt + "sys_base_on.bmp";
            sysBase.States["pressed"] = BasicArt + "sys_base_on.bmp";
            layout.Children.Add(sysBase);
            buttons.Clear();
            buttons.Add(new Button { Hit = HitArrowBack, Rect = new RectInt(36, 22, 20, 18), Art = Ce + "btn_arrowback_{0}.bmp" });
            buttons.Add(new Button { Hit = HitSearch, Rect = new RectInt(221, 22, 26, 18), Art = Ce + "btn_search_{0}.bmp", HasDisable = true });
            buttons.Add(new Button { Hit = HitShortcuts, Rect = new RectInt(252, 22, 37, 18), Art = Ce + "btn_shortui_{0}.bmp" });
            buttons.Add(new Button { Hit = HitBuy, Rect = new RectInt(247, 355, 42, 20), Art = Ce + "btn_buy_{0}.bmp", HasDisable = true });
            window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = p => p.y < 17 && p.x >= 29 && HitAt(p) == HitNone };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == CloseId) Close();   // msg 6 / 0xc5: DeleteWindow(0x57)
            };
            window.EscClose = () => {
                Close();
                return true;
            };
            window.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "y", w.Position.y);
            };
            window.Pressed += OnPressed;
            window.Released += OnReleased;
            window.DoubleClicked += OnDoubleClicked;
            window.Wheeled += OnWheeled;
            searchEdit = new RoClassicEditField { Rect = new RectInt(61, 23, 160, 16), MaxBytes = 20 };
            searchEdit.Submitted += _ => RunSearch();   // the edit's Enter: msg 0 → 0x724270
            window.AddEditField(searchEdit);
        }

        // ---- the tabs and the panes

        /// <summary>Msg 6 / 0xd3 → 0x724da0: the tab's pane shown, the controls of the tab set, the edit holding the pane's search.</summary>
        private void SelectTab(int next) {
            if (next == tab || next < RoEmojiListRules.Basic || next > RoEmojiListRules.Search) return;
            tab = next;
            bool searchOn = RoEmojiListRules.SearchControlsOn(tab);
            foreach (Button b in buttons) {
                if (b.Hit == HitArrowBack || b.Hit == HitSearch) b.Enabled = searchOn;
                if (b.Hit == HitBuy) b.Enabled = RoEmojiListRules.BuyOn(tab, 0);   // nothing is ever chosen on Unbought here
            }

            if (searchEdit.HasFocus) RoClassicTextInput.Focus(null);
            searchEdit.ReadOnly = !searchOn;
            searchEdit.Background = searchOn ? EditWhite : EditOff;   // UIEditCtrl vf20 0x7be290: +0xd4 draws the fill 30 darker
            searchEdit.SetText(searchOn ? panes[tab].SearchText : string.Empty);
            thumbGrab = -1;
            window?.Invalidate();
        }

        /// <summary>The search button 0xba, msg 0 (the edit's Enter): the current pane's vf54 with the edit's text.</summary>
        private void RunSearch() {
            if (tab < RoEmojiListRules.Basic) return;
            Fill(tab, searchEdit.ReadOnly ? null : searchEdit.Text);
            window.Invalidate();
        }

        /// <summary>The pane's vf54: rebuilt for a search text (null: none), back to the top.</summary>
        private void Fill(int which, string text) {
            Pane pane = panes[which];
            pane.Pieces.Clear();
            pane.Rows.Clear();
            pane.Offset = 0;
            pane.SearchText = text ?? string.Empty;
            RoCashEmotionData data = RoCashEmotionData.Shared;
            if (data != null) {
                switch (which) {
                    case RoEmojiListRules.Basic:
                        // vf54 0x7206b0: the basic order list (0x576170), the emotions it has a record of
                        int k = 0;
                        foreach (int id in data.Order(data.BasicPack)) {
                            if (!data.TryGet(data.BasicPack, id, out _)) continue;
                            pane.Pieces.Add(new Piece { Pack = data.BasicPack, Id = id, Rect = RoEmojiListRules.BasicPiece(k++) });
                        }

                        break;
                    case RoEmojiListRules.Bought:
                    case RoEmojiListRules.Unbought:
                        // vf54 0x7212d0 / 0x722ae0: the packs of mgr +0x1a4 / +0x1b8 whose name holds the text — both lists are
                        // filled only by the owned packs packet (0xc46190 → 0xc3a210), which Custom's server never sends
                        break;
                    case RoEmojiListRules.Search:
                        FillSearch(pane, data, text);
                        break;
                }
            }

            extents.Clear();
            foreach (Piece p in pane.Pieces) extents.Add(p.Rect);
            foreach (Row r in pane.Rows) extents.Add(r.Rect);
            pane.Content = RoEmojiListRules.ContentHeight(extents);
        }

        /// <summary>
        /// Search vf54 0x7220b0: 0x576e70 over the word list; when it finds anything, three rows (0x721f40) at (9, y) one under
        /// another — the basic pack's, the bought packs', the rest — each with its title and its emotions.
        /// </summary>
        private void FillSearch(Pane pane, RoCashEmotionData data, string text) {
            RoEmojiListRules.SearchWords(data.SearchWords, text, data.BasicPack, RoEmotionSender.OwnedPacks, foundBasic, foundBought, foundUnbought);
            if (foundBasic.Count == 0 && foundBought.Count == 0 && foundUnbought.Count == 0) return;
            int y = 0;
            AddSearchRow(pane, data.Message("EMSG_SEARCH_WND_BASIC_TITLE"), foundBasic, ref y);
            AddSearchRow(pane, data.Message("EMSG_SEARCH_WND_PURCHASE_TITLE"), foundBought, ref y);
            AddSearchRow(pane, data.Message("EMSG_SEARCH_WND_UNPURCHASED_TITLE"), foundUnbought, ref y);
        }

        private static void AddSearchRow(Pane pane, string title, List<(int Pack, int Id)> found, ref int y) {
            int height = RoEmojiListRules.SearchRowHeight(found.Count);
            var row = new Row { Title = title, Rect = new RectInt(9, y, RoEmojiListRules.SearchRowWidth, height) };
            pane.Rows.Add(row);
            for (int i = 0; i < found.Count; i++) {
                RectInt r = RoEmojiListRules.SearchRowPiece(i);
                pane.Pieces.Add(new Piece { Pack = found[i].Pack, Id = found[i].Id, Rect = new RectInt(row.Rect.x + r.x, row.Rect.y + r.y, r.width, r.height) });
            }

            y += height;
        }

        // ---- input

        /// <summary>What is under a window point: a tab, a button, the scroll bar's parts or a shown emotion.</summary>
        private int HitAt(Vector2Int p) {
            if (p.x >= 0 && p.x < TabW && p.y >= TabY && p.y < TabY + 4 * TabH) return HitTab + 1 + (p.y - TabY) / TabH;
            foreach (Button b in buttons) {
                if (b.Rect.Contains(p)) return b.Enabled ? b.Hit : HitNone;
            }

            if (tab < RoEmojiListRules.Basic) return HitNone;
            Pane pane = panes[tab];
            int lx = p.x - PaneX, ly = p.y - PaneY;
            if (lx < 0 || ly < 0 || lx >= RoEmojiListRules.PaneWidth || ly >= RoEmojiListRules.PaneHeight) return HitNone;
            if (lx >= BarX && lx < BarX + RoEmojiListRules.BarWidth) {
                if (!pane.Scrolls) return HitNone;
                return HitBarUp + RoEmojiListRules.BarPart(ly, RoEmojiListRules.PaneHeight, pane.Content, RoEmojiListRules.PaneHeight, pane.Offset);
            }

            int cy = ly + pane.Offset;
            for (int i = 0; i < pane.Pieces.Count; i++) {
                if (pane.Pieces[i].Rect.Contains(new Vector2Int(lx, cy))) return HitPiece + i;
            }

            return HitNone;
        }

        private void OnPressed(RoClassicWindow w, Vector2Int local) {
            int hit = HitAt(local);
            pressed = hit;
            swallowRelease = false;
            if (hit > HitTab && hit <= HitTab + 4) {
                SelectTab(hit - HitTab);   // UIBmpRadioBtn vf25 0x7f1580: on the press
                pressed = HitNone;
                return;
            }

            Pane pane = tab >= RoEmojiListRules.Basic ? panes[tab] : null;
            switch (hit) {
                case HitBarUp:
                    Scroll(pane, -RoEmojiListRules.ArrowStep);
                    break;
                case HitBarDown:
                    Scroll(pane, RoEmojiListRules.ArrowStep);
                    break;
                case HitBarAbove:
                    Scroll(pane, -RoEmojiListRules.TrackStep);
                    break;
                case HitBarBelow:
                    Scroll(pane, RoEmojiListRules.TrackStep);
                    break;
                case HitThumb:
                    (int offset, _) = RoEmojiListRules.Thumb(RoEmojiListRules.PaneHeight, pane.Content, RoEmojiListRules.PaneHeight, pane.Offset);
                    thumbGrab = local.y - PaneY - (RoEmojiListRules.BarArrow + offset);
                    break;
            }

            w.Invalidate();
        }

        private void OnReleased(RoClassicWindow w, Vector2Int local) {
            int was = pressed;
            pressed = HitNone;
            thumbGrab = -1;
            w.Invalidate();
            if (swallowRelease) {
                swallowRelease = false;
                return;
            }

            if (was == HitNone || was != HitAt(local)) return;
            switch (was) {
                case HitArrowBack:
                    // 0xb9: the edit emptied, the current pane's search cleared (vf54(0))
                    searchEdit.SetText(string.Empty);
                    Fill(tab, null);
                    break;
                case HitSearch:
                    RunSearch();
                    break;
                case HitShortcuts:
                    RoClassicShortcutList.Toggle();   // 0xbb: DeleteWindow(0x56) when it exists, else MakeWindow(0x56)
                    break;
                case HitBuy:
                    break;   // 0x20a: no pack chosen ([0x11a6124]+0x1c4 < 1) → nothing (0x0bec is never sent)
                default:
                    if (was >= HitPiece) ClickPiece(panes[tab].Pieces[was - HitPiece]);
                    break;
            }
        }

        /// <summary>
        /// A piece's sprite button (id 0xb9) clicked → msg 0xae up to the pane: Basic runs CCashEmotionAction_Register, Search
        /// CCashEmotionAction_Play (its +0xbc); Bought / Unbought have no pieces here.
        /// </summary>
        private void ClickPiece(Piece piece) {
            if (tab == RoEmojiListRules.Basic) Register(piece.Pack, piece.Id);
            else if (tab == RoEmojiListRules.Search) RoEmotionSender.Play(session, piece.Pack, piece.Id);
        }

        /// <summary>Piece vf26 0x728340: msg 0xaf; the Basic pane plays it (CCashEmotionAction_Play), the search rows ignore it.</summary>
        private void OnDoubleClicked(RoClassicWindow w, Vector2Int local) {
            int hit = HitAt(local);
            swallowRelease = true;
            pressed = hit;
            if (hit >= HitPiece && tab == RoEmojiListRules.Basic) {
                Piece piece = panes[tab].Pieces[hit - HitPiece];
                RoEmotionSender.Play(session, piece.Pack, piece.Id);
            } else if (hit > HitTab && hit <= HitTab + 4) {
                SelectTab(hit - HitTab);
            }
        }

        private void OnWheeled(RoClassicWindow w, Vector2Int local, int delta) {
            if (tab < RoEmojiListRules.Basic) return;
            Pane pane = panes[tab];
            int lx = local.x - PaneX, ly = local.y - PaneY;
            if (lx < 0 || ly < 0 || lx >= RoEmojiListRules.PaneWidth || ly >= RoEmojiListRules.PaneHeight || !pane.Scrolls) return;
            Scroll(pane, delta < 0 ? RoEmojiListRules.WheelStep : -RoEmojiListRules.WheelStep);
        }

        private void Scroll(Pane pane, int delta) {
            if (pane == null || !pane.Scrolls) return;
            int next = RoEmojiListRules.Scroll(pane.Offset, delta, pane.Content, RoEmojiListRules.PaneHeight);
            if (next == pane.Offset) return;
            pane.Offset = next;
            window.Invalidate();
        }

        /// <summary>
        /// CCashEmotionAction_Register::vf01 0x56e770: the record's text (0x5765b0) goes into the focused line of the Shortcut
        /// List when that window exists (0x804350), else it becomes the chat edit's text.
        /// </summary>
        private static void Register(int pack, int id) {
            RoCashEmotionData data = RoCashEmotionData.Shared;
            if (data == null || !data.TryGet(pack, id, out RoCashEmotionData.Emotion emotion) || string.IsNullOrEmpty(emotion.Text)) return;
            if (RoClassicShortcutList.IsOpen) {
                RoClassicShortcutList.SetFocusedLine(emotion.Text);
                return;
            }

            RoClassicChat.Instance?.SetClassicMessageText(emotion.Text);
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            TickMini(ui);
            if (window == null || !window.Visible) return;
            Vector2Int mouse = ui.Windows.Mouse - window.Position;
            if (thumbGrab >= 0 && tab >= RoEmojiListRules.Basic) {
                // the thumb follows the pointer (UIScrollBar vf27 0x7f3310 while captured)
                Pane pane = panes[tab];
                (_, int length) = RoEmojiListRules.Thumb(RoEmojiListRules.PaneHeight, pane.Content, RoEmojiListRules.PaneHeight, pane.Offset);
                int track = RoEmojiListRules.PaneHeight - 2 * RoEmojiListRules.BarArrow - length, range = pane.Content - RoEmojiListRules.PaneHeight;
                if (track > 0 && range > 0) {
                    int top = mouse.y - PaneY - RoEmojiListRules.BarArrow - thumbGrab;
                    int next = Mathf.Clamp((int) Math.Round((double) top * range / track), 0, range);
                    if (next != pane.Offset) {
                        pane.Offset = next;
                        window.Invalidate();
                    }
                }
            }

            int over = ui.Windows.HoverWindow == window ? HitAt(mouse) : HitNone;
            if (over != hover) {
                hover = over;
                window.Invalidate();   // a redraw only when what is under the pointer changes
            }
        }

        // ---- drawing

        private void Draw(RoClassicWindow w) {
            // OnDraw 0x7241c0
            TitleBar(w, 29, 268, RoClassicUiHost.MsgStr(0x21e, "Emoji List"));
            Body(w, 29, 17, 268);
            w.FillRect(60, 22, 161, 1, FrameGrey);   // 0x9ac1e0: the search edit's frame
            w.FillRect(60, 22, 1, 18, FrameGrey);
            w.FillRect(60 + 161 - 1, 22, 1, 18, FrameGrey);
            w.FillRect(60, 22 + 18 - 1, 161, 1, FrameGrey);
            w.FillRect(29, 46, 268, 1, LineGrey);
            ButtonBar(w, 29, 268);

            for (int t = RoEmojiListRules.Basic; t <= RoEmojiListRules.Search; t++) {
                // UIBmpRadioBtn: the checked one draws its "press" art, the others "normal"
                w.DrawBitmap(0, TabY + TabH * (t - 1), Ce + "tap_" + TabNames[t] + "_" + (t == tab ? "press" : "normal") + ".bmp");
            }

            foreach (Button b in buttons) {
                string state = !b.Enabled ? (b.HasDisable ? "disable" : "normal") : pressed == b.Hit && hover == b.Hit ? "press" : hover == b.Hit ? "over" : "normal";
                w.DrawBitmap(b.Rect.x, b.Rect.y, string.Format(b.Art, state));
            }

            if (tab >= RoEmojiListRules.Basic) DrawPane(w, panes[tab]);
        }

        private void DrawPane(RoClassicWindow w, Pane pane) {
            RoCashEmotionData data = RoCashEmotionData.Shared;
            int viewW = pane.Scrolls ? RoEmojiListRules.PaneWidth - RoEmojiListRules.BarWidth : RoEmojiListRules.PaneWidth;
            foreach (Row row in pane.Rows) {
                int ry = PaneY + row.Rect.y - pane.Offset;
                if (ry + row.Rect.height <= PaneY || ry >= PaneY + RoEmojiListRules.PaneHeight) continue;
                w.ClipTo(PaneX, PaneY, viewW, RoEmojiListRules.PaneHeight);
                // UICashEmotionPack_Search::vf20 0x727700: bg_list_search at (0, 0); the title's static text at (33, 14)
                w.DrawBitmap(PaneX + row.Rect.x, ry, Ce + "bg_list_search.bmp");
                if (!string.IsNullOrEmpty(row.Title)) w.TextOut(PaneX + row.Rect.x + 33, ry + 14, row.Title, 0, 12, Black);
            }

            for (int i = 0; i < pane.Pieces.Count; i++) {
                Piece piece = pane.Pieces[i];
                int px = PaneX + piece.Rect.x, py = PaneY + piece.Rect.y - pane.Offset;
                if (py + piece.Rect.height <= PaneY || py >= PaneY + RoEmojiListRules.PaneHeight) continue;
                // the piece's own surface inside the pane's view
                int cx0 = Math.Max(px, PaneX), cy0 = Math.Max(py, PaneY);
                int cx1 = Math.Min(px + piece.Rect.width, PaneX + viewW), cy1 = Math.Min(py + piece.Rect.height, PaneY + RoEmojiListRules.PaneHeight);
                if (cx1 <= cx0 || cy1 <= cy0) continue;
                w.ClipTo(cx0, cy0, cx1 - cx0, cy1 - cy0);
                if (hover == HitPiece + i) w.RoundOutline(px, py, piece.Rect.width, piece.Rect.height, Grey);   // 0x7282d0
                if (data != null && data.TryGet(piece.Pack, piece.Id, out RoCashEmotionData.Emotion emotion)) {
                    RoClassicActSprite sprite = RoClassicActSprite.Get(SpritePath(emotion.Act));
                    sprite?.DrawButton(w, emotion.Action < 0 ? 0 : emotion.Action, emotion.Motion < 0 ? 0 : emotion.Motion, new RectInt(px, py, piece.Rect.width, piece.Rect.height));
                }
            }

            w.ResetClip();
            DrawBar(w, pane);
        }

        /// <summary>UICashEmotionScrollBar::vf20 0x728600 at the pane's (W − 13, 0).</summary>
        private void DrawBar(RoClassicWindow w, Pane pane) {
            int x = PaneX + BarX, y = PaneY, h = RoEmojiListRules.PaneHeight, bw = RoEmojiListRules.BarWidth;
            w.FillRect(x, y, bw, 1, BarOutline);
            w.FillRect(x, y, 1, h, BarOutline);
            w.FillRect(x + bw - 1, y, 1, h, BarOutline);
            w.FillRect(x, y + h - 1, bw, 1, BarOutline);
            bool on = pane.Scrolls;
            string up = !on ? "scroll_btn_up_diasble" : pressed == HitBarUp && hover == HitBarUp ? "scroll_btn_up_press" : hover == HitBarUp ? "scroll_btn_up_over" : "scroll_btn_up_normal";
            string down = !on ? "scroll_btn_down_disable" : pressed == HitBarDown && hover == HitBarDown ? "scroll_btn_down_press" : hover == HitBarDown ? "scroll_btn_down_over" : "scroll_btn_down_normal";
            w.DrawBitmap(x, y - 1, Ce + up + ".bmp");
            w.DrawBitmap(x, y + h - RoEmojiListRules.BarArrow + 1, Ce + down + ".bmp");
            if (!on) return;
            (int offset, int length) = RoEmojiListRules.Thumb(h, pane.Content, h, pane.Offset);
            int top = y + offset + RoEmojiListRules.BarArrow - 2;
            const int piece = 3;   // scrollbar_top / _mid / _bottom are 11 × 3
            int end = top + length + 4 - piece;
            w.DrawBitmap(x, top, Ce + "scrollbar_top.bmp");
            for (int t = top + piece; t < end; t += piece) w.DrawBitmap(x, t, Ce + "scrollbar_mid.bmp");
            w.DrawBitmap(x, end, Ce + "scrollbar_bottom.bmp");
        }

        /// <summary>The record's act / spr name ("emotion\basic.act") as the sprite's path under data\sprite\, no extension.</summary>
        private static string SpritePath(string act) {
            string name = act ?? string.Empty;
            int dot = name.LastIndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            return "data/sprite/" + name.Replace('\\', '/');
        }

        /// <summary>0x82f440: titlebar_left at x, titlebar_right at x + w − its width, titlebar_mid tiled between; the title with a white shadow at (x + 19, 4), black at (x + 18, 3).</summary>
        private void TitleBar(RoClassicWindow w, int x, int width, string title) {
            int left = ImageWidth(BasicArt + "titlebar_left.bmp", 12), right = ImageWidth(BasicArt + "titlebar_right.bmp", 12), mid = ImageWidth(BasicArt + "titlebar_mid.bmp", 12);
            w.DrawBitmapRaw(x, 0, BasicArt + "titlebar_left.bmp");
            w.DrawBitmapRaw(x - right + width, 0, BasicArt + "titlebar_right.bmp");
            int n = width - right - left;
            n = n <= 0 ? 0 : n / mid + (n % mid != 0 ? 1 : 0);
            for (int i = 0; i < n; i++) w.DrawBitmapRaw(x + left + mid * i, 0, BasicArt + "titlebar_mid.bmp");
            w.TextOut(x + 19, 4, title, 0, 12, White);
            w.TextOut(x + 18, 3, title, 0, 12, Black);
        }

        /// <summary>0x82cd60 without a picture: the colorchip(14, 6) rounded outline and top band, colorchip(2, 2) inside.</summary>
        private static void Body(RoClassicWindow w, int x, int y, int width) {
            int h = H - y;
            w.RoundOutline(x, y, width, h, Grey);
            w.FillRect(x, y, width, 5, Grey);
            w.FillRect(x + 1, y, width - 2, h - 2, White);
            w.FillRect(x + 2, H - 2, width - 4, 1, White);
        }

        /// <summary>0x82d410(2, x, w, 1): btnbar_left2 / right2 at the bottom, btnbar_mid2 tiled between.</summary>
        private void ButtonBar(RoClassicWindow w, int x, int width) {
            const string left = BasicArt + "btnbar_left2.bmp", right = BasicArt + "btnbar_right2.bmp", mid = BasicArt + "btnbar_mid2.bmp";
            int lw = ImageWidth(left, 21), rw = ImageWidth(right, 21), mw = ImageWidth(mid, 21), bh = ImageHeight(left, 29);
            w.DrawBitmapRaw(x, H - bh, left);
            w.DrawBitmapRaw(x - rw + width, H - bh, right);
            int n = width - rw - lw;
            n = n <= 0 ? 0 : n / mw + (n % mw != 0 ? 1 : 0);
            for (int i = 0; i < n; i++) w.DrawBitmapRaw(x + lw + mw * i, H - bh, mid);
        }

        private int ImageWidth(string path, int fallback) => system.Images.TryGet(path, out RoClassicUiImage image) ? image.Width : fallback;

        private int ImageHeight(string path, int fallback) => system.Images.TryGet(path, out RoClassicUiImage image) ? image.Height : fallback;

        /// <summary>The sweep console: toggle | tab n | search text | back | wheel ±n | click i | dclick i | state.</summary>
        public static string Sweep(string command, RoLoginShellSession session) {
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            RoClassicEmojiList e = Ensure();
            if (e == null) return "no classic UI";
            if (session != null) e.sessionSet = session;
            int Arg() => a.Length > 1 && int.TryParse(a[1], out int v) ? v : 0;
            switch (a[0]) {
                case "toggle":
                    Toggle(session);
                    break;
                case "tab" when e.window != null:
                    e.SelectTab(Arg());
                    break;
                case "search" when e.window != null:
                    e.searchEdit.SetText(a.Length > 1 ? a[1] : string.Empty);
                    e.RunSearch();
                    break;
                case "back" when e.window != null && e.tab != RoEmojiListRules.Basic:
                    e.searchEdit.SetText(string.Empty);
                    e.Fill(e.tab, null);
                    e.window.Invalidate();
                    break;
                case "wheel" when e.window != null && e.tab >= RoEmojiListRules.Basic:
                    e.Scroll(e.panes[e.tab], Arg() * RoEmojiListRules.WheelStep);
                    break;
                case "click" when e.window != null && e.tab >= RoEmojiListRules.Basic && Arg() >= 0 && Arg() < e.panes[e.tab].Pieces.Count:
                    e.ClickPiece(e.panes[e.tab].Pieces[Arg()]);
                    break;
                case "dclick" when e.window != null && e.tab == RoEmojiListRules.Basic && Arg() >= 0 && Arg() < e.panes[e.tab].Pieces.Count:
                    Piece piece = e.panes[e.tab].Pieces[Arg()];
                    RoEmotionSender.Play(e.session, piece.Pack, piece.Id);
                    break;
            }

            if (e.window == null || !e.window.Visible) return "closed";
            Pane pane = e.panes[e.tab];
            var sb = new System.Text.StringBuilder();
            sb.Append("tab=").Append(e.tab).Append(" pieces=").Append(pane.Pieces.Count).Append(" rows=").Append(pane.Rows.Count)
                .Append(" content=").Append(pane.Content).Append(" offset=").Append(pane.Offset).Append(" search=\"").Append(pane.SearchText).Append('"');
            for (int i = 0; i < pane.Pieces.Count && i < 12; i++) sb.Append(' ').Append(pane.Pieces[i].Pack).Append('/').Append(pane.Pieces[i].Id);
            return sb.ToString();
        }
    }
}
