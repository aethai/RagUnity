using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UIBookWnd 0x6a, the book reader (misc-windows-spec §2): opened by ZC_READ_BOOK 0x0294 {bookId, page} and by the item
    /// description's Read / Auto Read buttons. data\book\&lt;id&gt;.txt (a missing file prints "책이 없습니다" in red), an optional
    /// "%RRGGBB" first line for the page colour, the text wrapped at 530 px into 19-line pages; 600 × 420 at ((W − 640)/2, 0),
    /// the title "name ( p / n )", the bookmark / previous / next / close sprite buttons. Close saves the bookmark (per
    /// account and title, default page 1). Auto Read hides the window and shows one line every 4 s in #FFE4E1 in the chat
    /// and in a balloon at the top centre, then closes.
    /// </summary>
    public sealed class RoClassicBook {
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const string Sprites = "data/sprite/book/";
        private const int W = 600, H = 420, LinesPerPage = 19, ReadId = 0x11b, AutoReadId = 0x11c;
        private const int BookmarkId = 0x160, PrevId = 0x15e, NextId = 0x15f, CloseId = 0xc5;
        private const string Prefs = "ro.ui.classic.book.";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Frame = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static RoClassicBook instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        // the four UISpriteButtons: sprite, position, tooltip
        private static readonly (int Id, string Sprite, int X, int Y, int Tip, string TipText)[] Buttons = {
            (BookmarkId, "책갈피", 10, 25, 0x510, "Bookmark"),
            (PrevId, "책왼쪽", W - 57, 25, 0x511, "Previous"),
            (NextId, "책오른쪽", W - 35, 25, 0x512, "Next"),
            (CloseId, "책닫기", W - 25, H - 25, 0x513, "Close"),
        };

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private string bookId = string.Empty, title = string.Empty;
        private readonly List<string> lines = new List<string>();
        private Color32 paper = White;
        private int page = 1, pageCount = 1;
        private bool autoReading;
        private float autoStart;
        private int cursor;
        private int pressed = -1;
        private string balloon;

        private static RoClassicBook Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicBook { system = system };
            return instance;
        }

        /// <summary>The book's text file, or null (0x55a730).</summary>
        private static byte[] ReadFile(int itemId) {
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            string path = "data/book/" + itemId.ToString(CultureInfo.InvariantCulture) + ".txt";
            return assets != null && assets.Exists(path) ? assets.ReadBytesQuiet(path) : null;
        }

        /// <summary>The literal "책이 없습니다" in #FF0000, as the client's CP949 bytes read in this build's code page.</summary>
        private static void NoBook() {
            RoTextEncoding.EnsureCodePagesRegistered();
            string text = Encoding.GetEncoding(1252).GetString(Encoding.GetEncoding(949).GetBytes("책이 없습니다"));
            Chat()?.AddLine(text, "#FF0000");
        }

        private static RoClassicChat Chat() => RoClassicChat.Instance;

        /// <summary>ZC_READ_BOOK 0x0294 (0xc5f420): the file checked, then MakeWindow(0x6a) → msg 0x5e("bookId", 0x11b, page).</summary>
        public static void HandleReadBook(byte[] b) {
            if (b == null || b.Length < 10) return;
            int id = BitConverter.ToInt32(b, 2), p = BitConverter.ToInt32(b, 6);
            Open(id, ReadId, p);
        }

        /// <summary>The item description's 0x11b Read / 0x11c Auto Read (0x85c46c): the same check, then msg 0x5e(itemId, button, 0).</summary>
        public static void Open(int itemId, int mode, int pageArg) {
            byte[] data = ReadFile(itemId);
            if (data == null) {
                NoBook();
                return;
            }

            Ensure()?.Load(itemId, data, mode, pageArg);
        }

        /// <summary>Whether data\bookitemnametable.txt lists the item (0x660700): the description shows the book buttons.</summary>
        public static bool IsBook(int itemId) {
            if (bookItems == null) {
                bookItems = new HashSet<int>();
                RoAssetDatabase assets = RoClassicUiHost.GameAssets();
                byte[] table = assets != null && assets.Exists("data/bookitemnametable.txt") ? assets.ReadBytesQuiet("data/bookitemnametable.txt") : null;
                if (table == null) {
                    bookItems = null;
                    return false;
                }

                foreach (string row in Encoding.ASCII.GetString(table).Split('\n')) {
                    int hash = row.IndexOf('#');
                    if (int.TryParse(hash >= 0 ? row.Substring(0, hash) : row.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) bookItems.Add(id);
                }
            }

            return bookItems.Contains(itemId);
        }

        private static HashSet<int> bookItems;

        /// <summary>msg 0x5e (0x857900): the same title keeps the page (a Read while reading the same book does nothing); else re-read.</summary>
        private void Load(int itemId, byte[] data, int mode, int pageArg) {
            RoItemMetadataTable items = RoClassicUiHost.GameAssets()?.GameData?.Items;
            string name = items != null ? items.GetItemOrFallback(itemId).GetDisplayName(true) : itemId.ToString(CultureInfo.InvariantCulture);
            int p;
            if (name == title && window != null) {
                if (!autoReading && mode == ReadId && window.Visible) return;
                p = page;
            } else {
                p = pageArg;
            }

            title = name;
            bookId = itemId.ToString(CultureInfo.InvariantCulture);
            Parse(data);
            if (mode == ReadId) ShowPage(p);
            else ToggleAutoRead(p);
        }

        /// <summary>The "%RRGGBB" paper line, then PixelWrap(text, 530 px) (0x9b27a0, "^RRGGBB" takes no width) into lines.</summary>
        private void Parse(byte[] data) {
            RoTextEncoding.EnsureCodePagesRegistered();
            string text = Encoding.GetEncoding(1252).GetString(data).Replace("\r", string.Empty);
            paper = White;
            if (text.Length >= 7 && text[0] == '%' && IsHex(text, 1)) {
                uint rgb = uint.Parse(text.Substring(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                paper = new Color32((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, 255);
                int nl = text.IndexOf('\n');
                text = nl >= 0 ? text.Substring(nl + 1) : string.Empty;
            }

            lines.Clear();
            RoClassicGdiFont font = system.Font(12);
            int start = 0, width = 0;
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c == '\n') {
                    lines.Add(text.Substring(start, i - start));
                    start = i + 1;
                    width = 0;
                    continue;
                }

                if (c == '^' && i + 7 <= text.Length && IsHex(text, i + 1)) {
                    i += 6;
                    continue;
                }

                font.GetGlyph(c, out RoClassicGdiFont.Glyph g);
                if (width + g.Advance > 530 && i > start) {
                    lines.Add(text.Substring(start, i - start));
                    start = i;
                    width = 0;
                }

                width += g.Advance;
            }

            if (start < text.Length) lines.Add(text.Substring(start));
            pageCount = Math.Max(1, (lines.Count + LinesPerPage - 1) / LinesPerPage);
        }

        private static bool IsHex(string s, int at) {
            if (at + 6 > s.Length) return false;
            for (int i = at; i < at + 6; i++) {
                if (!Uri.IsHexDigit(s[i])) return false;
            }

            return true;
        }

        private string BookmarkKey {
            get {
                uint account = RoClassicEntryQueue.CurrentSession?.LocalCharacterIds().AccountId ?? 0;
                return Prefs + account.ToString(CultureInfo.InvariantCulture) + "." + title;
            }
        }

        /// <summary>msg 0x5c: window mode at page p (0 = the bookmark, past the end = 0, clamped to 1 as the port's guard).</summary>
        private void ShowPage(int p) {
            autoReading = false;
            balloon = null;
            if (p == 0) p = PlayerPrefs.GetInt(BookmarkKey, 1);
            if (p > pageCount) p = 0;
            page = Math.Max(1, p);
            EnsureWindow();
            if (!window.Visible) {
                window.MoveTo((system.ClientSize.x - 640) / 2, 0);
                window.Visible = true;
            }

            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
            window.Invalidate();
        }

        /// <summary>msg 0x5d: Auto Read on (the window hides, a line every 4 s from the page's first line) or off (closes).</summary>
        private void ToggleAutoRead(int p) {
            if (autoReading) {
                Close(false);
                return;
            }

            if (p == 0) p = PlayerPrefs.GetInt(BookmarkKey, 1);
            if (p > pageCount) p = 0;
            page = Math.Max(1, p);
            EnsureWindow();
            autoReading = true;
            window.Visible = false;
            autoStart = Time.unscaledTime - 4f;
            cursor = LinesPerPage * page - LinesPerPage;
            if (cursor >= lines.Count) cursor = 0;
        }

        private void Close(bool saveBookmark) {
            if (saveBookmark && title.Length > 0) PlayerPrefs.SetInt(BookmarkKey, page);
            autoReading = false;
            balloon = null;
            if (window != null) window.Visible = false;
            title = string.Empty;
        }

        private void EnsureWindow() {
            if (window != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIBookWnd", WindowId = 0x6a, Width = W, Height = H };
            window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = p => p.y < 17 };
            window.DrawDynamic += Draw;
            window.EscClose = () => {
                Button(CloseId);
                return true;
            };
            window.Pressed += (w, p) => pressed = ButtonAt(p);
            window.Released += (w, p) => {
                int b = ButtonAt(p);
                if (b >= 0 && b == pressed) Button(Buttons[b].Id);
                pressed = -1;
            };
            window.Wheeled += (w, p, delta) => Button(delta > 0 ? PrevId : NextId);
        }

        private static int ButtonAt(Vector2Int p) {
            for (int i = 0; i < Buttons.Length; i++) {
                RoClassicActSprite s = RoClassicActSprite.Get(Sprites + Buttons[i].Sprite);
                Vector2Int size = s != null && s.Ready ? s.Size(0, 0) : new Vector2Int(18, 18);
                if (new RectInt(Buttons[i].X, Buttons[i].Y, size.x, size.y).Contains(p)) return i;
            }

            return -1;
        }

        /// <summary>msg 6 (0x857900): previous / next (ignored while auto-reading), the bookmark, close (bookmark first).</summary>
        private void Button(int id) {
            switch (id) {
                case PrevId when !autoReading:
                    ShowPage(page == 1 ? 1 : page - 1);
                    break;
                case NextId when !autoReading:
                    ShowPage(page == pageCount ? page : page + 1);
                    break;
                case BookmarkId:
                    if (title.Length > 0) PlayerPrefs.SetInt(BookmarkKey, page);
                    break;
                case CloseId:
                    Close(true);
                    break;
            }
        }

        /// <summary>OnProcess 0x855240: one line every 4 s while auto-reading (a blank line skipped once), then close.</summary>
        public static void Tick() {
            RoClassicBook b = instance;
            if (b == null) return;
            if (b.window != null && b.window.Visible) b.Hover();
            if (b.balloon != null) {
                Vector2Int c = b.system.ClientSize;
                RoClassicTooltip.Named("book").Show(b.balloon, new Color32(0xff, 0xe4, 0xe1, 255), (c.x - 640) / 2, 81, 100, 640);
            }

            if (!b.autoReading || Time.unscaledTime <= b.autoStart + 4f) return;
            if (b.cursor >= b.lines.Count) {
                b.Close(false);
                return;
            }

            string line = b.lines[b.cursor];
            if ((line.Length == 0 || line == " ") && b.cursor < b.lines.Count - 1) line = b.lines[++b.cursor];
            string stripped = Strip(line);
            Chat()?.AddLine(stripped, "#FFE4E1");
            b.balloon = stripped;
            b.cursor++;
            b.autoStart = Time.unscaledTime;
        }

        private void Hover() {
            if (system.Windows.HoverWindow != window) return;
            int i = ButtonAt(system.Windows.Mouse - window.Position);
            if (i >= 0) RoClassicTooltip.Shared.Show(RoClassicUiHost.MsgStr(Buttons[i].Tip, Buttons[i].TipText), White, system.Windows.Mouse.x + 10, system.Windows.Mouse.y + 10);
        }

        private static string Strip(string s) {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == '^' && IsHex(s, i + 1)) {
                    i += 6;
                    continue;
                }

                sb.Append(s[i]);
            }

            return sb.ToString();
        }

        /// <summary>OnDraw 0x84c500: the bare title bar, the body, "title ( p / n )" at (228, 2), the paper, the page's lines, the buttons.</summary>
        private void Draw(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.RoundOutline(0, 17, W, H - 17, Frame);
            w.FillRect(0, 17, W, 5, Frame);
            w.FillRect(1, 17, W - 2, H - 19, White);
            w.FillRect(2, H - 2, W - 4, 1, White);
            w.TextOut(W / 2 - 72, 2, title + " ( " + page.ToString(CultureInfo.InvariantCulture) + " / " + pageCount.ToString(CultureInfo.InvariantCulture) + " )", 0, 12, Black);
            w.FillRect(1, 17, W - 2, H - 18, paper);
            // the viewer (30, 45) 540 × 360: lines at x 3, y 4 + 18i, the colour carried from line to line
            Color32 color = Black;
            int first = LinesPerPage * (page - 1);
            for (int i = 0; i < LinesPerPage && first + i < lines.Count; i++) {
                string line = lines[first + i];
                w.TextOutColored(30 + 3, 45 + 4 + 18 * i, line, 0, line.Length, 0, 12, ref color);
            }

            foreach (var b in Buttons) RoClassicActSprite.Get(Sprites + b.Sprite)?.DrawIn(w, 0, 0, b.X + Size(b.Sprite).x / 2, b.Y + Size(b.Sprite).y / 2);
        }

        private static Vector2Int Size(string sprite) {
            RoClassicActSprite s = RoClassicActSprite.Get(Sprites + sprite);
            return s != null && s.Ready ? s.Size(0, 0) : Vector2Int.zero;
        }

        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            int Arg(int k) => k < t.Length ? int.Parse(t[k], CultureInfo.InvariantCulture) : 0;
            switch (t[0]) {
                case "read": Open(Arg(1), ReadId, Arg(2)); break;
                case "auto": Open(Arg(1), AutoReadId, Arg(2)); break;
                case "packet":
                    var p = new byte[10];
                    BitConverter.GetBytes((ushort) 0x0294).CopyTo(p, 0);
                    BitConverter.GetBytes(Arg(1)).CopyTo(p, 2);
                    BitConverter.GetBytes(Arg(2)).CopyTo(p, 6);
                    HandleReadBook(p);
                    break;
                case "next": instance?.Button(NextId); break;
                case "prev": instance?.Button(PrevId); break;
                case "mark": instance?.Button(BookmarkId); break;
                case "close": instance?.Button(CloseId); break;
                case "isbook": return "isbook " + Arg(1) + "=" + IsBook(Arg(1));
                case "desc":
                    RoClassicItemDescription.Toggle(RoItemInstance.CreateRegular(-1, Arg(1), 1, true), RoClassicUiHost.GameAssets()?.GameData?.Items);
                    break;
            }

            RoClassicBook b = instance;
            return b == null ? "none" : "title='" + b.title + "' page=" + b.page + "/" + b.pageCount + " lines=" + b.lines.Count + " open=" + (b.window != null && b.window.Visible) +
                " auto=" + b.autoReading + " cursor=" + b.cursor + " paper=" + ((Color) b.paper);
        }
    }
}
