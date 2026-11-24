using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// The NPC conversation (reference ui-static ui/RUNTIME-RULES-NPC.md): the say box UISayDialogWnd (0x10, 280×180, text
    /// pixel-wrapped at 215 px, 21 px rows, 6 px between messages, no auto-scroll; next / close button at (234, 156)), the
    /// menu UIChoose3Wnd (0x11, 16 px rows, grows from the 5th item), the number and text inputs (0x38, 0x64) and the cut-in
    /// (0x4e). The dialog state is the session's (ZC_SAY_DIALOG, ZC_WAIT_DIALOG, ZC_CLOSE_DIALOG, ZC_MENU_LIST,
    /// ZC_OPEN_EDITDLG(STR), ZC_SHOW_IMAGE2); RoClassicGameUi ticks the windows, which follow it whenever the session's NPC
    /// sequence moves, send the answers and take the client's keys (Enter, Up/Down; Esc does nothing).
    /// </summary>
    public sealed class RoClassicNpcDialog {
        private const string NpcUi = "유저인터페이스\\";
        private const int NpcSayButtonId = 0xd6;
        private const int NpcOkId = 0xb4;
        private const int NpcCancelId = 0xb5;
        private const int NpcCloseId = 0xc5;
        private const int NpcSayW = 280, NpcSayH = 180;
        private const int NpcViewX = 10, NpcViewY = 10, NpcViewW = 260, NpcViewH = 140;
        private const int NpcWrap = 215;          // W − 65
        private const int NpcGap = 6;             // line and paragraph gap
        private const int NpcRowH = 16;           // menu rows
        private static readonly Color32 NpcBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 NpcWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 NpcViewBg = new Color32(0xf2, 0xf2, 0xf2, 255);
        private static readonly Color32 NpcListBg = new Color32(0xf0, 0xf0, 0xf0, 255);
        private static readonly Color32 NpcSelected = new Color32(0xca, 0xdf, 0xfb, 255);

        /// <summary>Window-info structs mgr+0x5e8 / +0x5f8: where the box and the menu reopen, for the session.</summary>
        private static Vector2Int? npcSayPosition, npcMenuPosition;
        private static float npcLastClick = -10f;   // g_npcClick, the 300 ms debounce shared by next / close / menu OK

        private static RoClassicNpcDialog instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            npcSayPosition = npcMenuPosition = null;
            npcLastClick = -10f;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoItemMetadataTable items = RoItemMetadataTable.Empty;
        private int seenSequence = int.MinValue;

        private readonly struct NpcPiece {
            public NpcPiece(string text, Color32 color, int x, int y, string link) {
                Text = text;
                Color = color;
                X = x;
                Y = y;
                Link = link;
            }

            public string Text { get; }
            public Color32 Color { get; }
            public int X { get; }
            public int Y { get; }

            /// <summary>A link piece's "TAG\u0001info" (NAVI, ITEM, URL, QUEST, TIPBOX), else null.</summary>
            public string Link { get; }
        }

        private static readonly Color32 NpcLink = new Color32(0x4a, 0x93, 0xf4, 255);       // 0x00f4934a
        private static readonly string[] NpcLinkTags = { "NAVI", "ITEM", "URL", "QUEST", "TIPBOX" };

        private readonly List<NpcPiece> npcPieces = new List<NpcPiece>();
        private readonly List<string> npcMenuItems = new List<string>();
        private RoClassicWindow npcSay, npcMenu, npcNumber, npcText, npcCutin;
        private RoClassicChildLayout npcSayButton;
        private RoClassicEditField npcNumberEdit, npcTextEdit;
        private string npcLinesKey = string.Empty;
        private int npcContentBottom;
        private int npcScroll;
        private bool npcNextSent;
        private int npcWaitCount, npcNextSentWait; // the dialog ZC_WAIT_DIALOG count last seen / when next was clicked
        private string npcMenuKey;
        private string npcMenuAnsweredKey;
        private int npcMenuSel, npcMenuTop;
        private bool npcInputAnswered;
        private string npcCutinKey;
        private string npcCutinClosedKey;
        private bool npcClassicShown;

        private bool NpcSayOpen => npcSay != null && npcSay.Visible;
        private bool NpcMenuOpen => npcMenu != null && npcMenu.Visible;

        /// <summary>True while the say box or the menu is up.</summary>
        public static bool IsOpen => instance != null && (instance.NpcSayOpen || instance.NpcMenuOpen);

        /// <summary>
        /// Per frame (RoClassicGameUi): the windows follow the session's dialog when its NPC sequence moved (nothing is built
        /// before the first dialog), then the client's keys.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession, RoItemMetadataTable itemMetadata) {
            if (loginSession == null) return;
            RoClassicNpcDialog d = Ensure();
            if (d == null) return;
            if (!ReferenceEquals(d.session, loginSession)) {
                d.session = loginSession;
                d.seenSequence = int.MinValue;
            }

            d.items = itemMetadata ?? RoItemMetadataTable.Empty;
            int sequence = loginSession.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Npc);
            if (sequence != d.seenSequence) {
                d.seenSequence = sequence;
                RoNpcDialogSnapshot dialog = loginSession.NpcDialogSnapshot() ?? RoNpcDialogSnapshot.Empty;
                if (dialog.IsOpen || d.npcSay != null) d.RefreshClassicDialog(dialog);
            }

            d.HandleClassicDialogKeys();
        }

        /// <summary>Leaving the map: every NPC window goes; the next tick takes the session's dialog afresh.</summary>
        public static void Close() {
            RoClassicNpcDialog d = instance;
            if (d == null) return;
            d.HideClassicDialog();
            d.npcMenuAnsweredKey = null;
            d.npcCutinClosedKey = null;
            d.npcInputAnswered = false;
            d.npcNextSent = false;
            d.seenSequence = int.MinValue;
        }

        private static RoClassicNpcDialog Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                if (instance != null) RoClassicTextInput.RemoveKeyboardClaim(instance.npcKeyboardClaim);
                instance = new RoClassicNpcDialog { system = system };
            }

            return instance;
        }

        /// <summary>
        /// The say box, the menu, the inputs and the cut-in follow the dialog state (every ZC packet of the conversation and
        /// the local next / close / cancel).
        /// </summary>
        private void RefreshClassicDialog(RoNpcDialogSnapshot dialog) {
            EnsureClassicDialog();
            npcClassicShown = true;
            npcKeyboardClaim ??= () => (NpcSayOpen || NpcMenuOpen);
            RoClassicTextInput.AddKeyboardClaim(npcKeyboardClaim);

            // the say box: text, next after ZC_WAIT_DIALOG (gone once clicked), close after ZC_CLOSE_DIALOG
            bool box = dialog.Lines.Length > 0 || dialog.CanContinue || dialog.CanClose;
            // a new ZC_WAIT_DIALOG brings a new Next even when its page arrived in the same drain (no frame without CanContinue)
            npcWaitCount = dialog.WaitCount;
            if (!dialog.CanContinue || npcWaitCount != npcNextSentWait) npcNextSent = false;
            if (box) {
                string key = string.Join("\u0001", dialog.Lines);
                if (key != npcLinesKey) {
                    npcLinesKey = key;
                    LayoutClassicSayText(dialog.Lines);
                }

                string button = dialog.CanClose ? "btn_close" : dialog.CanContinue && !npcNextSent ? "btn_next" : null;
                SetClassicSayButton(button);
                if (!npcSay.Visible) ShowNpcWindow(npcSay, npcSayPosition ?? new Vector2Int(200, 100), true);
            } else {
                npcSay.Visible = false;
                npcLinesKey = string.Empty;
            }

            // the menu: rebuilt per ZC_MENU_LIST, hidden once answered until the server moves on
            string menuKey = dialog.HasMenu ? string.Join("\u0001", dialog.MenuChoices) + "|" + dialog.NpcId : null;
            if (menuKey == null) npcMenuAnsweredKey = null;
            if (menuKey != null && menuKey != npcMenuAnsweredKey) {
                if (menuKey != npcMenuKey) BuildClassicMenu(dialog.MenuChoices);
                npcMenuKey = menuKey;
                if (!npcMenu.Visible) ShowNpcWindow(npcMenu, npcMenuPosition ?? new Vector2Int(200, 300), false);
            } else {
                npcMenu.Visible = false;
                npcMenuKey = menuKey;
            }

            // the inputs (ZC_OPEN_EDITDLG / ZC_OPEN_EDITDLGSTR)
            if (!dialog.WantsInput) npcInputAnswered = false;
            ShowClassicInput(npcNumber, npcNumberEdit, dialog.WantsNumberInput && !npcInputAnswered, "0");
            ShowClassicInput(npcText, npcTextEdit, dialog.WantsTextInput && !npcInputAnswered, string.Empty);

            RefreshClassicCutin(dialog);
        }

        private void EnsureClassicDialog() {
            if (npcSay != null) return;

            var sayLayout = new RoClassicWindowLayout { Class = "UISayDialogWnd", WindowId = 0x10, Width = NpcSayW, Height = NpcSayH };
            npcSayButton = NpcButton(NpcSayButtonId, "say", NpcSayW - 46, NpcSayH - 24, "btn_next");
            npcSayButton.Visible = false;
            sayLayout.Children.Add(npcSayButton);
            npcSay = new RoClassicWindow(system, sayLayout) { Visible = false, FrameDragArea = _ => true };
            npcSay.DrawDynamic += DrawClassicSay;
            npcSay.Clicked += child => {
                if (child.Id == NpcSayButtonId) PressClassicSayButton(false);
            };
            npcSay.Wheeled += (w, local, delta) => ScrollClassicSay(delta > 0 ? -30 : 30);
            npcSay.Pressed += (w, local) => {
                string link = NpcLinkAt(local);
                if (link != null) OpenNpcLink(link);
            };
            npcSay.Moved += w => npcSayPosition = w.Position;

            var menuLayout = new RoClassicWindowLayout { Class = "UIChoose3Wnd", WindowId = 0x11, Width = 280, Height = 120 };
            menuLayout.Children.Add(NpcButton(NpcOkId, "ok", 188, 96, "btn_ok"));
            menuLayout.Children.Add(NpcButton(NpcCancelId, "cancel", 234, 96, "btn_cancel"));
            npcMenu = new RoClassicWindow(system, menuLayout) { Visible = false };
            npcMenu.FrameDragArea = local => !InClassicMenuList(local);
            npcMenu.DrawDynamic += DrawClassicMenu;
            npcMenu.Clicked += child => {
                if (child.Id == NpcOkId) ClassicMenuOk();
                else if (child.Id == NpcCancelId) ClassicMenuCancel();
            };
            npcMenu.Pressed += (w, local) => SelectClassicMenuRow(local);
            npcMenu.DoubleClicked += (w, local) => {
                if (InClassicMenuList(local) && npcMenuSel >= 0) ClassicMenuOk();
            };
            npcMenu.Wheeled += (w, local, delta) => ScrollClassicMenu(delta > 0 ? -3 : 3);
            npcMenu.Moved += w => npcMenuPosition = w.Position;

            npcNumber = NpcInputWindow(system, "UINpcEditDialogWnd", 0x38, 176, 12, out npcNumberEdit);
            npcText = NpcInputWindow(system, "UINpcTextEditDialogWnd", 0x64, 346, 70, out npcTextEdit);
        }

        private static RoClassicChildLayout NpcButton(int id, string path, int x, int y, string bitmap) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = 42, H = 20 };
            SetNpcButtonBitmap(button, bitmap);
            return button;
        }

        private static void SetNpcButtonBitmap(RoClassicChildLayout button, string bitmap) {
            button.States["normal"] = NpcUi + bitmap + ".bmp";
            button.States["hover"] = NpcUi + bitmap + "_a.bmp";
            button.States["pressed"] = NpcUi + bitmap + "_b.bmp";
        }

        private static void ShowNpcWindow(RoClassicWindow window, Vector2Int position, bool validate) {
            Vector2Int client = RoClassicUiSystem.Instance.ClientSize;
            // 0x907a50: the box's struct is reset to (200, 100) when it would leave the screen; the menu's check is jumped over
            if (validate && (position.x < 0 || position.y < 0 || position.x > client.x - window.Width || position.y > client.y - window.Height)) {
                position = new Vector2Int(200, 100);
            }

            window.MoveTo(position.x, position.y);
            window.Visible = true;
            RoClassicUiSystem.Instance.Windows.BringToFront(window);
            window.Invalidate();
        }

        /// <summary>The frame box 0x809d50: 14 px sysbox_* tiles around a white body.</summary>
        private static void DrawNpcFrame(RoClassicWindow w) => w.DrawSysBox(NpcUi + "sysbox_", 0, 0, w.Width, w.Height, false);

        // ---- the say box: UITagTextViewer layout (§4) ----

        /// <summary>
        /// Every message of the page (one ZC_SAY_DIALOG each) laid out as in CCreateLineInfo: words with their trailing space,
        /// ^RRGGBB colours (black again at each message), "\n" rows, a word wider than the wrap split by characters; rows
        /// 15 + 6 px apart, messages 6 px apart, an empty message adding nothing.
        /// </summary>
        private void LayoutClassicSayText(string[] messages) {
            npcPieces.Clear();
            RoClassicGdiFont font = RoClassicUiSystem.Instance.Font(12);
            int contentH = 0;
            var segments = new List<(string Text, Color32 Color, bool Break, string Link)>();
            foreach (string raw in messages) {
                Tokenize(raw ?? string.Empty, segments);
                int startY = contentH > 0 ? contentH + NpcGap : 0;
                var rows = new List<List<(string Text, Color32 Color, int X, string Link)>>();
                List<(string Text, Color32 Color, int X, string Link)> row = null;
                int x = 0;
                void NewRow() {
                    row = new List<(string, Color32, int, string)>();
                    rows.Add(row);
                    x = 0;
                }

                foreach ((string text, Color32 color, bool isBreak, string link) in segments) {
                    if (isBreak) {
                        if (row == null) NewRow();
                        row = null;
                        continue;
                    }

                    int w = font.Measure(text);
                    if (w > NpcWrap) {
                        // 0x79c2a0 / 0x79be60: fill the current row, then wrap-wide rows of characters
                        int i = 0;
                        if (row == null) NewRow();
                        while (i < text.Length) {
                            int j = i;
                            while (j < text.Length && x + font.Measure(text.Substring(i, j + 1 - i)) <= NpcWrap) j++;
                            if (j == i && x == 0) j = i + 1;   // one character wider than the wrap
                            if (j > i) {
                                string part = text.Substring(i, j - i);
                                row.Add((part, color, x, link));
                                x += font.Measure(part);
                                i = j;
                            }

                            if (i < text.Length) NewRow();
                        }

                        continue;
                    }

                    if (row == null) NewRow();
                    else if (x + w > NpcWrap) NewRow();
                    row.Add((text, color, x, link));
                    x += w;
                }

                int rowTop = 0;
                foreach (List<(string Text, Color32 Color, int X, string Link)> r in rows) {
                    int rowH = r.Count > 0 ? 15 : 0;   // the pieces' TextExtent height, Arial 15
                    foreach ((string text, Color32 color, int px, string link) in r) npcPieces.Add(new NpcPiece(text, color, px, startY + rowTop, link));
                    rowTop += rowH + NpcGap;
                }

                contentH = startY + rowTop - NpcGap;   // an empty message: −6, cancelling the gap
            }

            // UpdateScroll 0x7a19d0: the position is kept (no auto-scroll), clamped to the new range
            npcContentBottom = Math.Max(NpcViewH, 4 + contentH);
            npcScroll = Mathf.Clamp(npcScroll, 0, Math.Max(0, npcContentBottom - NpcViewH));
            npcSay?.Invalidate();
        }

        /// <summary>The pre-pass (\r \n \" escapes) and the scan: colour codes, LF rows, CR skipped, words ending at a space.</summary>
        private static void Tokenize(string text, List<(string Text, Color32 Color, bool Break, string Link)> segments) {
            segments.Clear();
            text = text.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\\"", "\"");
            var current = new StringBuilder();
            Color32 color = NpcBlack;
            void Flush() {
                if (current.Length == 0) return;
                segments.Add((current.ToString(), color, false, null));
                current.Clear();
            }

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                // 0x79ed90: a link tag "<TAG>label<INFO>info</INFO></TAG>" becomes one link piece with the label verbatim
                if (c == '<' && TryReadNpcLink(text, i, out string label, out string link, out int end)) {
                    Flush();
                    segments.Add((label, NpcLink, false, link));
                    i = end - 1;
                    continue;
                }

                if (c == '^' && i + 7 <= text.Length && IsNpcHex(text, i + 1)) {
                    Flush();
                    uint rgb = uint.Parse(text.Substring(i + 1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    color = new Color32((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, 255);
                    i += 6;
                    continue;
                }

                if (c == '\n') {
                    Flush();
                    segments.Add((string.Empty, color, true, null));
                    continue;
                }

                if (c == '\r') continue;
                current.Append(c);
                if (c == ' ') Flush();
            }

            Flush();
        }

        /// <summary>
        /// "&lt;TAG&gt;label&lt;INFO&gt;info&lt;/INFO&gt;&lt;/TAG&gt;" at <paramref name="start"/> (NAVI, ITEM, URL, QUEST, TIPBOX; the
        /// &lt;INFO&gt; part optional): the label, "TAG\u0001info" and the index after the closing tag.
        /// </summary>
        private static bool TryReadNpcLink(string text, int start, out string label, out string link, out int end) {
            label = link = null;
            end = start;
            foreach (string tag in NpcLinkTags) {
                string open = "<" + tag + ">", close = "</" + tag + ">";
                if (string.CompareOrdinal(text, start, open, 0, open.Length) != 0) continue;
                int closeAt = text.IndexOf(close, start + open.Length, StringComparison.Ordinal);
                if (closeAt < 0) return false;
                string inner = text.Substring(start + open.Length, closeAt - start - open.Length);
                string info = string.Empty;
                int infoAt = inner.IndexOf("<INFO>", StringComparison.Ordinal);
                if (infoAt >= 0) {
                    int infoEnd = inner.IndexOf("</INFO>", infoAt, StringComparison.Ordinal);
                    info = infoEnd > infoAt ? inner.Substring(infoAt + 6, infoEnd - infoAt - 6) : inner.Substring(infoAt + 6);
                    inner = inner.Substring(0, infoAt);
                }

                label = inner;
                link = tag + "\u0001" + info;
                end = closeAt + close.Length;
                return label.Length > 0;
            }

            return false;
        }

        /// <summary>
        /// A link piece clicked (UITextViewer msg 0x62): NAVI starts the navigation (0x1b2), ITEM opens the item's description,
        /// URL opens the address (ShellExecute "open", no confirmation), TIPBOX shows the tip (0x82b840); QUEST has no window here yet.
        /// </summary>
        private void OpenNpcLink(string link) {
            int split = link.IndexOf('\u0001');
            string tag = link.Substring(0, split), info = link.Substring(split + 1);
            switch (tag) {
                case "NAVI":
                    RoClassicNavigation.NavigateLink(info);
                    break;
                case "ITEM":
                    if (int.TryParse(info.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId) && itemId > 0) {
                        RoClassicItemDescription.Toggle(Ragnarok.Data.RoItemInstance.CreateRegular(0, itemId, 1), items);
                    }

                    break;
                case "TIPBOX":
                    if (int.TryParse(info.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int tipId)) RoClassicTipBox.ShowTip(tipId);
                    break;
                case "URL":
                    if (info.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || info.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                        Application.OpenURL(info.Trim());
                    }

                    break;
            }
        }

        /// <summary>The link piece under a say-box point, or null.</summary>
        private string NpcLinkAt(Vector2Int local) {
            if (local.x < NpcViewX || local.x >= NpcViewX + NpcViewW || local.y < NpcViewY || local.y >= NpcViewY + NpcViewH) return null;
            RoClassicGdiFont font = RoClassicUiSystem.Instance.Font(12);
            foreach (NpcPiece p in npcPieces) {
                if (p.Link == null) continue;
                int x = NpcViewX + 3 + p.X, y = NpcViewY + 4 + p.Y - npcScroll;
                if (local.x >= x && local.x < x + font.Measure(p.Text) && local.y >= y && local.y < y + 15) return p.Link;
            }

            return null;
        }

        private static bool IsNpcHex(string s, int start) {
            for (int i = start; i < start + 6; i++) {
                if (!Uri.IsHexDigit(s[i])) return false;
            }

            return true;
        }

        /// <summary>OnDraw: the frame, the viewer (#F2F2F2) with the visible pieces, the scroll bar when the text overflows.</summary>
        private void DrawClassicSay(RoClassicWindow w) {
            DrawNpcFrame(w);
            w.FillRect(NpcViewX, NpcViewY, NpcViewW, NpcViewH, NpcViewBg);
            w.ClipTo(NpcViewX, NpcViewY, NpcViewW, NpcViewH);
            foreach (NpcPiece p in npcPieces) {
                int y = NpcViewY + 4 + p.Y - npcScroll;
                if (y + 15 <= NpcViewY || y >= NpcViewY + NpcViewH) continue;
                w.TextOut(NpcViewX + 3 + p.X, y, p.Text, 0, 12, p.Color);
                // a link piece: the label underlined in its colour (1 px, the label's width)
                if (p.Link != null) w.FillRect(NpcViewX + 3 + p.X, y + 14, w.FontFor(0, 12).Measure(p.Text), 1, p.Color);
            }

            int range = npcContentBottom - NpcViewH;
            if (range > 0) w.DrawScrollBar(NpcViewX + NpcViewW - 13, NpcViewY, NpcViewH, range, npcContentBottom, NpcViewH, npcScroll);
            w.ResetClip();
        }

        private void ScrollClassicSay(int delta) {
            int scroll = Mathf.Clamp(npcScroll + delta, 0, Math.Max(0, npcContentBottom - NpcViewH));
            if (scroll == npcScroll) return;
            npcScroll = scroll;
            npcSay.Invalidate();
        }

        /// <summary>msg 0x55 / 0x56: the one button at (W − 46, H − 24), next or close, or none.</summary>
        private void SetClassicSayButton(string bitmap) {
            bool visible = bitmap != null;
            if (visible) SetNpcButtonBitmap(npcSayButton, bitmap);
            if (npcSayButton.Visible == visible && (!visible || npcSayButton.States["normal"] == NpcUi + bitmap + ".bmp")) return;
            npcSayButton.Visible = visible;
            npcSay.Invalidate();
        }

        /// <summary>
        /// The button (msg 6, id 0xd6, 300 ms debounce) or Enter: next → CZ_REQ_NEXT_SCRIPT, the button goes and the next text
        /// clears the box; close → CZ_CLOSE_DIALOG and the box and the menu go. Enter on close is not debounced.
        /// </summary>
        private void PressClassicSayButton(bool enter) {
            if (!npcSayButton.Visible) return;
            bool close = npcSayButton.States["normal"].EndsWith("btn_close.bmp", StringComparison.Ordinal);
            if (!(enter && close)) {
                if (Time.unscaledTime - npcLastClick < 0.3f) return;
                npcLastClick = Time.unscaledTime;
            }

            if (close) {
                npcSay.Visible = false;
                npcMenu.Visible = false;
                CloseFromUi();
                return;
            }

            npcNextSent = true;
            npcNextSentWait = npcWaitCount;
            SetClassicSayButton(null);
            NextFromUi();
        }

        /// <summary>CZ_REQ_NEXT_SCRIPT (the session clears the page locally until the next ZC_SAY_DIALOG).</summary>
        private void NextFromUi() {
            try {
                session?.BeginNpcDialogNext();
            } catch (Exception ex) {
                Debug.LogWarning($"NPC next was not sent: {ex.Message}");
            }
        }

        /// <summary>CZ_CLOSE_DIALOG (the session drops the dialog locally).</summary>
        private void CloseFromUi() {
            try {
                session?.BeginNpcDialogClose();
            } catch (Exception ex) {
                Debug.LogWarning($"NPC close was not sent: {ex.Message}");
            }
        }

        /// <summary>CZ_CHOOSE_MENU 0xff (the session drops the dialog locally).</summary>
        private void CancelMenuFromUi() {
            try {
                session?.BeginNpcDialogMenuChoice(byte.MaxValue);
            } catch (Exception ex) {
                Debug.LogWarning($"NPC menu cancel was not sent: {ex.Message}");
            }
        }

        // ---- the menu (§5) ----

        /// <summary>
        /// ZC_MENU_LIST: the items without "- [Not equipped]" (MsgStr 0x29a); ^nItemID^n shows the item's name. 16 px more per
        /// item from the 5th to the 15th, wider for long items; the first item selected.
        /// </summary>
        private void BuildClassicMenu(string[] choices) {
            npcMenuItems.Clear();
            string notEquipped = RoClassicUiHost.MsgStr(0x29a, "- [Not equipped]");
            RoClassicGdiFont font = RoClassicUiSystem.Instance.Font(12);
            int widest = 0;
            foreach (string choice in choices) {
                if (string.IsNullOrEmpty(choice) || choice == notEquipped) continue;
                string text = choice;
                if (text.StartsWith("^nItemID^", StringComparison.Ordinal)) {
                    int end = 9;
                    while (end < text.Length && end < 18 && char.IsDigit(text[end])) end++;
                    if (end > 9 && int.TryParse(text.Substring(9, end - 9), out int itemId) && itemId > 0) {
                        text = items.GetItemOrFallback(itemId).GetDisplayName(true);
                    }
                }

                npcMenuItems.Add(text);
                widest = Math.Max(widest, font.Measure(PlainNpcText(text)));
            }

            int count = npcMenuItems.Count;
            int dw = Math.Max(0, widest + 2 * 3 + 13 - 256);
            int dh = NpcRowH * Mathf.Clamp(count - 4, 0, 11);
            npcMenu.SetWidth(280 + dw);
            npcMenu.SetHeight(120 + dh);
            npcMenu.SetChildPosition("ok", npcMenu.Width - 92, npcMenu.Height - 24);
            npcMenu.SetChildPosition("cancel", npcMenu.Width - 46, npcMenu.Height - 24);
            npcMenuSel = count > 0 ? 0 : -1;
            npcMenuTop = 0;
            npcMenu.Invalidate();
        }

        private static string PlainNpcText(string s) {
            var plain = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == '^' && i + 7 <= s.Length && IsNpcHex(s, i + 1)) {
                    i += 6;
                    continue;
                }

                plain.Append(s[i]);
            }

            return plain.ToString();
        }

        private RectInt ClassicMenuList => new RectInt(12, 26, Math.Max(npcMenu.Width - 24, 20), Math.Max(npcMenu.Height - 55, 30));
        private int ClassicMenuVisibleRows => ClassicMenuList.height / NpcRowH;

        private bool InClassicMenuList(Vector2Int local) => ClassicMenuList.Contains(local);

        /// <summary>UIListBox OnDraw 0x7ec990: #F0F0F0, the selected row #CADFFB, items in colour from x 3, 16 px apart.</summary>
        private void DrawClassicMenu(RoClassicWindow w) {
            DrawNpcFrame(w);
            RectInt list = ClassicMenuList;
            w.FillRect(list.x, list.y, list.width, list.height, NpcListBg);
            w.ClipTo(list.x, list.y, list.width, list.height);
            int rows = ClassicMenuVisibleRows;
            for (int i = npcMenuTop; i < npcMenuItems.Count && i < npcMenuTop + rows; i++) {
                int r = i - npcMenuTop;
                if (i == npcMenuSel) w.FillRect(list.x, list.y + NpcRowH * r, list.width, 15, NpcSelected);
                w.TextOutColored(list.x + 3, list.y + NpcRowH * r, npcMenuItems[i], 0, 12);
            }

            if (npcMenuItems.Count > rows) {
                w.DrawScrollBar(list.x + list.width - 13, list.y, list.height, npcMenuItems.Count - rows, npcMenuItems.Count, rows, npcMenuTop);
            }

            w.ResetClip();
        }

        private void SelectClassicMenuRow(Vector2Int local) {
            if (!InClassicMenuList(local)) return;
            int i = (local.y - ClassicMenuList.y) / NpcRowH + npcMenuTop;
            npcMenuSel = i < npcMenuItems.Count ? i : -1;
            npcMenu.Invalidate();
        }

        private void ScrollClassicMenu(int rows) {
            int top = Mathf.Clamp(npcMenuTop + rows, 0, Math.Max(0, npcMenuItems.Count - ClassicMenuVisibleRows));
            if (top == npcMenuTop) return;
            npcMenuTop = top;
            npcMenu.Invalidate();
        }

        /// <summary>Up / Down: the selection ∓ 1, the view following.</summary>
        private void MoveClassicMenuSelection(int delta) {
            if (npcMenuItems.Count == 0) return;
            npcMenuSel = Mathf.Clamp(npcMenuSel + delta, 0, npcMenuItems.Count - 1);
            if (npcMenuSel < npcMenuTop) npcMenuTop = npcMenuSel;
            if (npcMenuSel >= npcMenuTop + ClassicMenuVisibleRows) npcMenuTop = npcMenuSel - ClassicMenuVisibleRows + 1;
            npcMenu.Invalidate();
        }

        /// <summary>OK (debounced): CZ_CHOOSE_MENU with the visible position + 1; an empty menu cancels.</summary>
        private void ClassicMenuOk() {
            if (Time.unscaledTime - npcLastClick < 0.3f) return;
            npcLastClick = Time.unscaledTime;
            if (npcMenuItems.Count == 0) {
                ClassicMenuCancel();
                return;
            }

            if (npcMenuSel < 0) return;
            npcMenuAnsweredKey = npcMenuKey;
            npcMenu.Visible = false;
            try {
                session?.BeginNpcDialogMenuChoice(npcMenuSel + 1);
            } catch (Exception ex) {
                Debug.LogWarning($"NPC menu choice was not sent: {ex.Message}");
            }
        }

        /// <summary>Cancel (mode 0x28): CZ_CHOOSE_MENU 0xff, then with the say box open CZ_CLOSE_DIALOG; both windows go.</summary>
        private void ClassicMenuCancel() {
            npcMenuAnsweredKey = npcMenuKey;
            npcMenu.Visible = false;
            bool box = NpcSayOpen;
            CancelMenuFromUi();
            if (box) {
                npcSay.Visible = false;
                CloseFromUi();
            }
        }

        // ---- the inputs (§6) ----

        private RoClassicWindow NpcInputWindow(RoClassicUiSystem system, string name, int id, int width, int maxBytes, out RoClassicEditField edit) {
            var layout = new RoClassicWindowLayout { Class = name, WindowId = id, Width = width, Height = 46 };
            layout.Children.Add(NpcButton(NpcOkId, "ok", width - 55, 14, "btn_ok"));
            var window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = _ => true };
            var field = new RoClassicEditField { Rect = new RectInt(15, 22, width - 77, 16), Background = new Color32(0xe8, 0xe8, 0xe8, 255), MaxBytes = maxBytes };
            window.AddEditField(field);
            bool number = id == 0x38;
            window.DrawDynamic += w => {
                DrawNpcFrame(w);
                if (number) w.TextOut(15, 6, "Input number ", 0, 12, NpcBlack);
            };
            window.Clicked += child => {
                if (child.Id == NpcOkId) SubmitClassicInput(number);
            };
            field.Submitted += _ => SubmitClassicInput(number);
            edit = field;
            return window;
        }

        private void ShowClassicInput(RoClassicWindow window, RoClassicEditField edit, bool show, string initial) {
            if (show == window.Visible) return;
            if (!show) {
                if (edit.HasFocus) RoClassicTextInput.Focus(null);
                window.Visible = false;
                return;
            }

            // (scaledX(200), scaledY(200)); the number starts at "0", fully selected
            Vector2Int client = RoClassicUiSystem.Instance.ClientSize;
            window.MoveTo((client.x - 640) / 2 + 200, client.y * 200 / 480);
            edit.SetText(initial);
            window.Visible = true;
            RoClassicUiSystem.Instance.Windows.BringToFront(window);
            edit.Focus();
            window.Invalidate();
        }

        /// <summary>
        /// OK / Enter: the number through atol (leading sign and digits, anything else 0) → CZ_INPUT_EDITDLG; the text up to its
        /// first LF → CZ_INPUT_EDITDLGSTR. An empty field does nothing.
        /// </summary>
        private void SubmitClassicInput(bool number) {
            RoClassicEditField edit = number ? npcNumberEdit : npcTextEdit;
            string text = edit.Text;
            if (string.IsNullOrEmpty(text)) return;
            npcInputAnswered = true;
            ShowClassicInput(number ? npcNumber : npcText, edit, false, null);
            try {
                if (number) {
                    session?.BeginNpcDialogNumberInput(Atol(text));
                } else {
                    int lf = text.IndexOf('\n');
                    session?.BeginNpcDialogTextInput(lf >= 0 ? text.Substring(0, lf) : text);
                }
            } catch (Exception ex) {
                Debug.LogWarning($"NPC input was not sent: {ex.Message}");
            }
        }

        private static int Atol(string s) {
            int i = 0;
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            bool negative = i < s.Length && s[i] == '-';
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            long value = 0;
            while (i < s.Length && char.IsDigit(s[i]) && value < int.MaxValue) value = value * 10 + (s[i++] - '0');
            value = negative ? -value : value;
            return (int) Math.Max(int.MinValue, Math.Min(int.MaxValue, value));
        }

        // ---- the cut-in (§7) ----

        /// <summary>
        /// ZC_SHOW_IMAGE2: illust\&lt;name&gt; (else cardbmp\&lt;name&gt;), ".bmp" unless named; the window takes the picture's size.
        /// 0 / 1 / 2 bottom-aligned, centred at x 200 / W/2 / W − 200 and click-through; 3 centred with a title bar and a close
        /// button, 4 centred with a close button, both draggable.
        /// </summary>
        private void RefreshClassicCutin(RoNpcDialogSnapshot dialog) {
            int type = dialog.CutinTypeRaw;
            bool show = dialog.HasCutin && !string.IsNullOrWhiteSpace(dialog.CutinImageName) && type <= 4;
            string name = show ? dialog.CutinImageName.Trim().ToLowerInvariant() : null;
            if (name != null && !name.EndsWith(".bmp", StringComparison.Ordinal) && !name.EndsWith(".png", StringComparison.Ordinal)) name += ".bmp";
            string key = name + "|" + type;
            if (!show || key == npcCutinClosedKey) {
                if (npcCutin != null) npcCutin.Visible = false;
                if (!show) npcCutinClosedKey = null;
                npcCutinKey = null;
                return;
            }

            if (key == npcCutinKey && npcCutin != null && npcCutin.Visible) return;   // the same picture again: nothing
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            string path = NpcUi + "illust\\" + name;
            if (!system.Images.TryGet(path, out RoClassicUiImage image)) {
                path = NpcUi + "cardbmp\\" + name;
                if (!system.Images.TryGet(path, out image)) return;   // missing: nothing drawn
            }

            npcCutinKey = key;
            bool title = type == 3, interactive = type == 3 || type == 4;
            int w = image.Width, h = image.Height + (title ? 17 : 0);
            if (npcCutin == null) {
                var layout = new RoClassicWindowLayout { Class = "UIIllustWnd", WindowId = 0x4e, Width = w, Height = h };
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = NpcCloseId, Path = "close", W = 11, H = 11 };
                close.States["normal"] = NpcUi + "basic_interface\\sys_close_off.bmp";
                close.States["hover"] = close.States["pressed"] = NpcUi + "basic_interface\\sys_close_on.bmp";
                layout.Children.Add(close);
                npcCutin = new RoClassicWindow(system, layout) { Visible = false };
                npcCutin.Clicked += child => {
                    if (child.Id != NpcCloseId) return;
                    npcCutinClosedKey = npcCutinKey;   // only this cut-in goes (mode 0x64 "", −1, 1)
                    npcCutin.Visible = false;
                };
            }

            string bitmap = path;
            bool withTitle = title;
            npcCutin.DrawDynamic -= npcCutinDraw;
            npcCutinDraw = cw => {
                int y = 0;
                if (withTitle) {
                    cw.DrawBitmapRaw(0, 0, NpcUi + "basic_interface\\titlebar_left.bmp");
                    for (int x = 12; x < cw.Width - 12; x += 12) cw.DrawBitmapRaw(x, 0, NpcUi + "basic_interface\\titlebar_mid.bmp");
                    cw.DrawBitmapRaw(cw.Width - 12, 0, NpcUi + "basic_interface\\titlebar_right.bmp");
                    y = 17;
                }

                cw.DrawBitmap(0, y, bitmap);
            };
            npcCutin.DrawDynamic += npcCutinDraw;
            npcCutin.SetWidth(w);
            npcCutin.SetHeight(h);
            foreach (RoClassicChildLayout child in npcCutin.Layout.Children) child.Visible = interactive;
            npcCutin.SetChildPosition("close", w - 16, 3);
            npcCutin.Draggable = interactive;
            npcCutin.FrameDragArea = interactive ? (Func<Vector2Int, bool>) (_ => true) : _ => false;
            npcCutin.HitArea = interactive ? (RectInt?) null : new RectInt(0, 0, 0, 0);   // 0–2 are click-through
            Vector2Int client = system.ClientSize;
            int cx = type == 0 ? 200 : type == 2 ? client.x - 200 : client.x / 2;
            int top = type <= 2 ? client.y - h : client.y / 2 - h / 2;
            npcCutin.MoveTo(cx - w / 2, top);
            npcCutin.Visible = true;
            npcCutin.Invalidate();
        }

        private Action<RoClassicWindow> npcCutinDraw;

        // ---- keys (§8) and lifecycle ----

        /// <summary>
        /// Enter: the first of number / text input (their fields take it), menu (OK), say box (next / close). Up / Down: the
        /// menu's selection, else the say text ±30 px. Esc and Space: nothing.
        /// </summary>
        private void HandleClassicDialogKeys() {
            if (!npcClassicShown || RoClassicMessageBox.ModalOpen || RoClassicTextInput.HasFocus) return;
            if (!NpcSayOpen && !NpcMenuOpen) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            if (enter) {
                if (NpcMenuOpen) ClassicMenuOk();
                else PressClassicSayButton(true);
            }

            int arrow = keyboard.upArrowKey.wasPressedThisFrame ? -1 : keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0;
            if (arrow != 0) {
                if (NpcMenuOpen) MoveClassicMenuSelection(arrow);
                else ScrollClassicSay(30 * arrow);
            }
        }

        private void HideClassicDialog() {
            if (!npcClassicShown) return;
            npcClassicShown = false;
            foreach (RoClassicWindow w in new[] { npcSay, npcMenu, npcNumber, npcText, npcCutin }) {
                if (w != null) w.Visible = false;
            }

            if (npcNumberEdit != null && npcNumberEdit.HasFocus) RoClassicTextInput.Focus(null);
            if (npcTextEdit != null && npcTextEdit.HasFocus) RoClassicTextInput.Focus(null);
            npcLinesKey = string.Empty;
            npcMenuKey = null;
            npcCutinKey = null;
        }

        private Func<bool> npcKeyboardClaim;

        // ---- diagnostics console (RoSkillSweepRunner "!npc ...") ----

        /// <summary>talk x y | next | close | pick n (visible row) | cancel | up | down | input text | scroll ±px | lines text | press x y | state.</summary>
        public static string Sweep(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            if (a[0] == "talk") return RoClassicNpcShop.Sweep("talk " + (a.Length > 1 ? a[1] : "0 0"));
            RoClassicNpcDialog d = Ensure();
            if (d == null) return "no classic UI";
            d.EnsureClassicDialog();
            return d.SweepClassicDialog(a);
        }

        private string SweepClassicDialog(string[] a) {
            string s0 = string.Empty;
            switch (a[0]) {
                case "next":
                case "close":
                    npcLastClick = -10f;
                    PressClassicSayButton(false);
                    break;
                case "pick":
                    npcLastClick = -10f;
                    npcMenuSel = Mathf.Clamp(int.Parse(a[1], CultureInfo.InvariantCulture), 0, Math.Max(0, npcMenuItems.Count - 1));
                    ClassicMenuOk();
                    break;
                case "cancel": ClassicMenuCancel(); break;
                case "up": MoveClassicMenuSelection(-1); break;
                case "down": MoveClassicMenuSelection(1); break;
                case "scroll": ScrollClassicSay(int.Parse(a[1], CultureInfo.InvariantCulture)); break;
                case "lines":
                    // the say box with a given text (link tags included), without a server dialog
                    LayoutClassicSayText(new[] { a.Length > 1 ? a[1] : string.Empty });
                    if (!npcSay.Visible) ShowNpcWindow(npcSay, npcSayPosition ?? new Vector2Int(200, 100), true);
                    break;
                case "press": {
                    string[] p = (a.Length > 1 ? a[1] : "0 0").Split(' ');
                    string link = NpcLinkAt(new Vector2Int(int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture)));
                    if (link != null) OpenNpcLink(link);
                    s0 = "link=" + (link ?? "-").Replace('\u0001', '|') + " ";
                    break;
                }
                case "input":
                    if (npcNumber != null && npcNumber.Visible) {
                        npcNumberEdit.SetText(a.Length > 1 ? a[1] : string.Empty);
                        SubmitClassicInput(true);
                    } else if (npcText != null && npcText.Visible) {
                        npcTextEdit.SetText(a.Length > 1 ? a[1] : string.Empty);
                        SubmitClassicInput(false);
                    }

                    break;
            }

            var s = new StringBuilder(s0);
            s.Append("say=").Append(NpcSayOpen).Append(" button=").Append(npcSayButton != null && npcSayButton.Visible ? npcSayButton.States["normal"] : "none")
                .Append(" pieces=").Append(npcPieces.Count).Append(" bottom=").Append(npcContentBottom).Append(" scroll=").Append(npcScroll)
                .Append(" menu=").Append(NpcMenuOpen ? string.Join("|", npcMenuItems) + " sel=" + npcMenuSel : "closed")
                .Append(" number=").Append(npcNumber != null && npcNumber.Visible).Append(" text=").Append(npcText != null && npcText.Visible)
                .Append(" cutin=").Append(npcCutin != null && npcCutin.Visible ? npcCutinKey : "none");
            if (npcPieces.Count > 0) {
                s.Append(" first='");
                for (int i = 0; i < npcPieces.Count && i < 8; i++) s.Append(npcPieces[i].Text);
                s.Append('\'');
            }

            return s.ToString();
        }
    }
}
