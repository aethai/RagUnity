using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MoonSharp.Interpreter;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UITipboxWnd 0x13b (session-windows-spec §1), 480 × 365 at ((W − 640)/2 + 120, H·120/480): the tips of the client's
    /// System\tipbox.lub (AddTip / AddImgcoord / AddPageEx / AddPage from its main()), a random searchable tip below 1000 on
    /// create, its picture at (1, 17), the title (wrapped at 30 bytes, two lines, #2C4DA0 bold) and the page in a tag viewer
    /// (one message per LF piece, rows 15 + 6 px, ^RRGGBB, links), ◀ ▶ and "n/m"; the search field (titles with their
    /// #keywords) fills a list whose double-click shows a tip. Opens once per session at the first map entry while "Open on
    /// Startup" is on; Alt+D and /tip toggle it; &lt;TIPBOX&gt; links and ZC_UI_OPEN type 5 show a tip.
    /// </summary>
    public sealed class RoClassicTipBox : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string Tip = Ui + "tipbox\\";
        private const int W = 480, H = 365, CloseId = 0xc5, CloseButtonId = 0x513;
        private const int ViewX = 250, ViewW = 226, Wrap = 188, Gap = 6;
        private const string Prefs = "ro.ui.classic.tipbox.startup";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 TitleBlue = new Color32(0x2c, 0x4d, 0xa0, 255);
        private static readonly Color32 Placeholder = new Color32(0xb1, 0xb1, 0xb1, 255);
        private static readonly Color32 ListBack = new Color32(240, 240, 240, 255);
        private static readonly Color32 Highlight = new Color32(0x70, 0x9f, 0xed, 255);
        private static readonly Color32 Link = new Color32(0x4a, 0x93, 0xf4, 255);
        private static readonly string[] LinkTags = { "NAVI", "ITEM", "URL", "QUEST", "TIPBOX" };
        private static RoClassicTipBox instance;
        private static bool shownThisSession;   // 0x153bab4

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            shownThisSession = false;
        }

        private sealed class TipData {
            public string Title = string.Empty, Image = string.Empty;
            public bool Search;
            public int Position = 5, Size = 100;
            public readonly List<string> Pages = new List<string>();
            public readonly List<(int Effect, bool Twinkle, int X, int Y, int DestX, int DestY, float MoveTime)> Effects = new List<(int, bool, int, int, int, int, float)>();
        }

        private struct Piece {
            public string Text;
            public Color32 Color;
            public int X, Y;
            public string Link;
        }

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private RoClassicEditField search;
        private readonly SortedDictionary<int, TipData> tips = new SortedDictionary<int, TipData>();
        private bool loaded;
        private TipData current;
        private int page;
        private readonly List<string> titleLines = new List<string>();
        private readonly List<Piece> pieces = new List<Piece>();
        private int scroll, contentH;
        private bool listShown;
        private readonly List<(string Title, int Id)> results = new List<(string, int)>();
        private int listSel = -1, listFirst;
        private float effectStart;
        private bool closePressed;

        private static bool StartupFlag {
            get => PlayerPrefs.GetInt(Prefs, 1) != 0;
            set => PlayerPrefs.SetInt(Prefs, value ? 1 : 0);
        }

        private static RoClassicTipBox Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicTipBox { system = system };
                system.Register(instance);
            }

            return instance;
        }

        // ---------------------------------------------------------------- data (0x70b410)

        private void Load() {
            if (loaded) return;
            loaded = true;
            string root = RoClassicUiHost.GameAssets()?.ClientRootPath;
            string path = root == null ? null : Path.Combine(root, "System", "tipbox.lua");
            if (path == null || !File.Exists(path)) path = root == null ? null : Path.Combine(root, "System", "tipbox.lub");
            if (path == null || !File.Exists(path)) return;
            Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
            Encoding cp1252 = Encoding.GetEncoding(1252), cp949 = Encoding.GetEncoding(949);
            try {
                // the bytes as the client shows them (cp1252); the image path is CP949 in the GRF
                var script = new Script(CoreModules.Preset_SoftSandbox);
                script.Globals["AddTip"] = (Func<int, string, int, string, DynValue>) ((id, title, searchable, image) => {
                    tips[id] = new TipData { Title = title ?? string.Empty, Search = searchable == 1, Image = cp949.GetString(cp1252.GetBytes(image ?? string.Empty)) };
                    return DynValue.True;
                });
                script.Globals["AddImgcoord"] = (Func<int, int, int, DynValue>) ((id, position, size) => {
                    if (tips.TryGetValue(id, out TipData t)) (t.Position, t.Size) = (position, size);
                    return DynValue.True;
                });
                script.Globals["AddPageEx"] = (Func<CallbackArguments, DynValue>) (args => {
                    // the last 8 values: effect, twinkle, startX, startY, destX, destY, moveTime (the dummy row is dropped)
                    int n = args.Count;
                    if (n < 8) return DynValue.True;
                    int id = (int) args[n - 8].Number, effect = (int) args[n - 7].Number;
                    if (effect == -1 || id <= 0 || !tips.TryGetValue(id, out TipData t)) return DynValue.True;
                    t.Effects.Add((effect, args[n - 6].Number != 0, (int) args[n - 5].Number, (int) args[n - 4].Number, (int) args[n - 3].Number, (int) args[n - 2].Number, (float) args[n - 1].Number));
                    return DynValue.True;
                });
                script.Globals["AddPage"] = (Func<int, string, DynValue>) ((id, text) => {
                    // 0x70a9b0: each two-character "\n" becomes a line feed
                    if (tips.TryGetValue(id, out TipData t)) t.Pages.Add((text ?? string.Empty).Replace("\\n", "\n"));
                    return DynValue.True;
                });
                script.DoString(cp1252.GetString(File.ReadAllBytes(path)));
                script.Call(script.Globals.Get("main"));
            } catch (InterpreterException e) {
                Debug.LogWarning("[tipbox] " + e.DecoratedMessage);
            }
        }

        // ---------------------------------------------------------------- openers

        /// <summary>Alt+D (hotkey 54) and /tip: if (!DeleteWindow(0x13b)) MakeWindow(0x13b).</summary>
        public static bool Toggle() {
            RoClassicTipBox t = Ensure();
            if (t == null) return false;
            if (t.window != null && t.window.Visible) {
                t.window.Visible = false;
                return true;
            }

            t.Make();
            return true;
        }

        /// <summary>0x82b840 (a &lt;TIPBOX&gt; link, ZC_UI_OPEN type 5): MakeWindow(0x13b) then this tip.</summary>
        public static void ShowTip(int id) {
            RoClassicTipBox t = Ensure();
            if (t == null) return;
            if (t.window == null || !t.window.Visible) t.Make();
            t.Select(id);
            t.Refresh();
            t.system.Windows.BringToFront(t.window);
        }

        /// <summary>The first map entry of the session (0xbf91e0): open while "Open on Startup" is on; the flag is spent either way.</summary>
        public static void Tick(RoLoginShellSession session) {
            if (shownThisSession || session == null || !session.BasicInfoSnapshot().HasCharacter) return;
            shownThisSession = true;
            if (StartupFlag) Ensure()?.Make();
        }

        public static bool TryCommand(string line) {
            if (line == null || !line.Trim().Equals("/tip", StringComparison.OrdinalIgnoreCase)) return false;
            Toggle();
            return true;
        }

        /// <summary>MakeWindow(0x13b): a new window picks a random searchable tip below 1000 (OnCreate 0x70adc0).</summary>
        private void Make() {
            Load();
            if (window == null) Build();
            if (!window.Visible) {
                Vector2Int c = system.ClientSize;
                window.MoveTo((c.x - 640) / 2 + 120, c.y * 120 / 480);
                var pool = new List<int>();
                foreach (KeyValuePair<int, TipData> p in tips) {
                    if (p.Key < 1000 && p.Value.Search) pool.Add(p.Key);
                }

                if (pool.Count > 0) Select(pool[UnityEngine.Random.Range(0, pool.Count)]);
                search.SetText(string.Empty);
                Refresh();
            }

            window.Visible = true;
            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
        }

        private void Build() {
            var layout = new RoClassicWindowLayout { Class = "UITipboxWnd", WindowId = 0x13b, Width = W, Height = H };
            var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = 465, Y = 3, W = 11, H = 11 };
            close.States["normal"] = Basic + "sys_close_off.bmp";
            close.States["hover"] = close.States["pressed"] = Basic + "sys_close_on.bmp";
            layout.Children.Add(close);
            var find = new RoClassicChildLayout { Class = "UIBitmapButton", Id = 0x169, Path = "search", X = 448, Y = 21, W = 26, H = 18 };
            find.States["normal"] = Tip + "btn_searchbar_normal.bmp";
            find.States["hover"] = Tip + "btn_searchbar_over.bmp";
            find.States["pressed"] = Tip + "btn_searchbar_press.bmp";
            layout.Children.Add(find);
            window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == CloseId) window.Visible = false;
                else if (child.Id == 0x169) Search();
            };
            window.EscClose = () => {
                window.Visible = false;
                return true;
            };
            window.Pressed += (w, p) => {
                closePressed = new RectInt(415, 341, 60, 20).Contains(p);
                if (new RectInt(9, 347, 14 + w.FontFor(0, 12).Measure(Msg(0xf63, "Open on Startup")), 14).Contains(p)) {
                    StartupFlag = !StartupFlag;
                    w.Invalidate();
                }

                if (!listShown && current != null) {
                    if (new RectInt(415, 319, 11, 12).Contains(p) && page > 0) Turn(-1);
                    else if (new RectInt(462, 319, 11, 12).Contains(p) && page + 1 < current.Pages.Count) Turn(1);
                }

                if (listShown && new RectInt(250, 50, 216, 266).Contains(p)) {
                    int k = listFirst + (p.y - 50) / 16;
                    if (k < results.Count) {
                        listSel = k;
                        w.Invalidate();
                    }
                }
            };
            window.Released += (w, p) => {
                bool hit = closePressed && new RectInt(415, 341, 60, 20).Contains(p);
                closePressed = false;
                w.Invalidate();
                if (hit) w.Visible = false;
                string link = LinkAt(p);
                if (link != null) OpenLink(link);
            };
            window.DoubleClicked += (w, p) => {
                // 0xb4: the selected row shows its tip
                if (!listShown || listSel < 0 || listSel >= results.Count) return;
                Select(results[listSel].Id);
                listShown = false;
                Refresh();
            };
            window.Wheeled += (w, p, delta) => {
                if (listShown) {
                    listFirst = Mathf.Clamp(listFirst + (delta > 0 ? -1 : 1), 0, Math.Max(0, results.Count - 266 / 16));
                } else {
                    scroll = Mathf.Clamp(scroll + (delta > 0 ? -21 : 21), 0, Math.Max(0, contentH - ViewH));
                }

                w.Invalidate();
            };
            search = new RoClassicEditField { Rect = new RectInt(253, 22, 193, 15), MaxBytes = 150 };
            search.Submitted += _ => Search();
            search.Changed += _ => window.Invalidate();
            window.AddEditField(search);
        }

        // ---------------------------------------------------------------- selection and refresh

        /// <summary>Select 0x70be70: page 0; an unknown id keeps the current tip.</summary>
        private void Select(int id) {
            page = 0;
            if (tips.TryGetValue(id, out TipData t)) current = t;
        }

        private void Turn(int delta) {
            page = Mathf.Clamp(page + delta, 0, (current?.Pages.Count ?? 1) - 1);
            Refresh();
        }

        /// <summary>Refresh 0x835c90: the title lines, the page pieces, the effects restarted.</summary>
        private void Refresh() {
            listShown = false;
            effectStart = Time.unscaledTime;
            titleLines.Clear();
            scroll = 0;
            if (current != null) {
                string title = current.Title;
                int hash = title.IndexOf('#');
                if (hash >= 0) title = title.Substring(0, hash);
                var wrap = new List<(int Start, int End)>();
                RoClassicWindow.WrapBytes(title, 30, wrap);
                for (int i = 0; i < wrap.Count && i < 2; i++) titleLines.Add(title.Substring(wrap[i].Start, wrap[i].End - wrap[i].Start).TrimEnd('\n'));
            }

            LayoutPage();
            window?.Invalidate();
        }

        private int ViewY => titleLines.Count >= 2 ? 90 : 70;
        private int ViewH => titleLines.Count >= 2 ? 225 : 245;

        /// <summary>OnProcess 0x855850: the page split at each LF (each later piece starting with it), one viewer message per piece.</summary>
        private void LayoutPage() {
            pieces.Clear();
            contentH = 0;
            if (current == null || page >= current.Pages.Count) return;
            string text = current.Pages[page];
            var messages = new List<string>();
            int start = 0;
            for (int i = 0; i < text.Length; i++) {
                if (text[i] != '\n' || i == 0) continue;
                messages.Add(text.Substring(start, i - start));
                start = i;
            }

            messages.Add(text.Substring(start));
            RoClassicGdiFont font = system.Font(12);
            foreach (string message in messages) {
                int startY = contentH > 0 ? contentH + Gap : 0, x = 0, rowTop = 0;
                bool rowOpen = false, rowHasText = false;
                Color32 color = Black;
                int i = 0;
                void EndRow() {
                    rowTop += (rowHasText ? 15 : 0) + Gap;
                    x = 0;
                    rowOpen = rowHasText = false;
                }

                void Put(string s, Color32 c, string link) {
                    int w = font.Measure(s);
                    if (rowOpen && x + w > Wrap) EndRow();
                    pieces.Add(new Piece { Text = s, Color = c, X = x, Y = startY + rowTop, Link = link });
                    x += w;
                    rowOpen = rowHasText = true;
                }

                var word = new StringBuilder();
                void Flush() {
                    if (word.Length == 0) return;
                    Put(word.ToString(), color, null);
                    word.Clear();
                }

                while (i < message.Length) {
                    char c = message[i];
                    if (c == '<' && TryLink(message, i, out string label, out string link, out int end)) {
                        Flush();
                        Put(label, Link, link);
                        i = end;
                        continue;
                    }

                    if (c == '^' && i + 7 <= message.Length && IsHex(message, i + 1)) {
                        Flush();
                        uint rgb = uint.Parse(message.Substring(i + 1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        color = new Color32((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, 255);
                        i += 7;
                        continue;
                    }

                    if (c == '\n') {
                        Flush();
                        if (!rowOpen) rowOpen = true;
                        EndRow();
                        i++;
                        continue;
                    }

                    if (c != '\r') word.Append(c);
                    if (c == ' ') Flush();
                    i++;
                }

                Flush();
                if (rowOpen) EndRow();
                contentH = startY + rowTop - Gap;
            }
        }

        private static bool TryLink(string text, int start, out string label, out string link, out int end) {
            label = link = null;
            end = start;
            foreach (string tag in LinkTags) {
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
                return true;
            }

            return false;
        }

        private static bool IsHex(string s, int at) {
            for (int i = at; i < at + 6; i++) {
                if (!Uri.IsHexDigit(s[i])) return false;
            }

            return true;
        }

        private string LinkAt(Vector2Int p) {
            if (listShown || p.x < ViewX || p.x >= ViewX + ViewW || p.y < ViewY || p.y >= ViewY + ViewH) return null;
            RoClassicGdiFont font = system.Font(12);
            foreach (Piece piece in pieces) {
                if (piece.Link == null) continue;
                int x = ViewX + 3 + piece.X, y = ViewY + 4 + piece.Y - scroll;
                if (p.x >= x && p.x < x + font.Measure(piece.Text) && p.y >= y && p.y < y + 15) return piece.Link;
            }

            return null;
        }

        /// <summary>&lt;TIPBOX&gt; shows the tip here; &lt;NAVI&gt; navigates; &lt;ITEM&gt; describes; &lt;URL&gt; opens.</summary>
        private void OpenLink(string link) {
            int split = link.IndexOf('\u0001');
            string tag = link.Substring(0, split), info = link.Substring(split + 1).Trim();
            switch (tag) {
                case "TIPBOX":
                    if (int.TryParse(info, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) {
                        Select(id);
                        Refresh();
                    }

                    break;
                case "NAVI":
                    RoClassicNavigation.NavigateLink(info);
                    break;
                case "ITEM":
                    if (int.TryParse(info, NumberStyles.Integer, CultureInfo.InvariantCulture, out int item) && item > 0) {
                        RoClassicItemDescription.Toggle(Ragnarok.Data.RoItemInstance.CreateRegular(0, item, 1), RoClassicUiHost.GameAssets()?.GameData?.Items);
                    }

                    break;
                case "URL":
                    if (info.StartsWith("http", StringComparison.OrdinalIgnoreCase)) Application.OpenURL(info);
                    break;
            }
        }

        /// <summary>0x169: the list of tips whose full title (with its #keywords) contains the text, ASCII case-blind.</summary>
        private void Search() {
            string q = search.Text;
            if (q.Length == 0) return;
            results.Clear();
            listShown = true;
            listSel = -1;
            listFirst = 0;
            titleLines.Clear();
            pieces.Clear();
            search.SetText(string.Empty);
            foreach (KeyValuePair<int, TipData> p in tips) {
                if (!p.Value.Search || p.Value.Title.ToUpperInvariant().IndexOf(q.ToUpperInvariant(), StringComparison.Ordinal) < 0) continue;
                string title = p.Value.Title;
                int hash = title.IndexOf('#');
                if (hash >= 0) title = title.Substring(0, hash);
                if (title.Length > 0) results.Add((title, p.Key));
            }

            window.Invalidate();
        }

        // ---------------------------------------------------------------- drawing (OnDraw 0x852920)

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (window != null && window.Visible && current != null && current.Effects.Count > 0) window.Invalidate();
        }

        private void Draw(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            string title = Msg(0xca6, "TIPBOX");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            w.DrawBitmapRaw(0, 17, Tip + "tipboxbg.bmp");
            if (listShown) {
                w.DrawBitmapRaw(1, 17, Tip + (results.Count > 0 ? "img_loading.bmp" : "img_nosearchresults.bmp"));
            } else if (current != null) {
                if (current.Image.Length == 0) w.DrawBitmapRaw(1, 17, Tip + "img_noimage.bmp");
                else w.DrawBitmapRaw(1, 17, current.Image + ".bmp");
                DrawEffects(w);
            }

            w.DrawBitmapRaw(241, 17, Tip + "bg_tipbox.bmp");
            w.DrawBitmapRaw(0, 337, Tip + "btnbar_bottom.bmp");
            // the search field and its placeholder
            if (search.Text.Length == 0 && !search.HasFocus) w.TextOut(255, 23, Msg(0xcb5, "Please enter a search term"), 0, 12, Placeholder);

            if (listShown) {
                w.FillRect(250, 50, 216, 266, ListBack);
                for (int r = 0; r < 266 / 16 && listFirst + r < results.Count; r++) {
                    int k = listFirst + r;
                    if (k == listSel) w.FillRect(250, 50 + 16 * r, 216, 16, Highlight);
                    w.TextOut(253, 50 + 16 * r + 2, results[k].Title, 0, 12, k == listSel ? White : Black);
                }
            } else {
                for (int i = 0; i < titleLines.Count; i++) w.TextOut(254, 50 + 20 * i, titleLines[i], 0, 13, TitleBlue, true);
                int vy = ViewY, vh = ViewH;
                w.FillRect(ViewX, vy, ViewW, vh, White);
                w.ClipTo(ViewX, vy, ViewW, vh);
                foreach (Piece p in pieces) {
                    int y = vy + 4 + p.Y - scroll;
                    if (y + 15 < vy || y > vy + vh) continue;
                    w.TextOut(ViewX + 3 + p.X, y, p.Text, 0, 12, p.Color);
                    if (p.Link != null) w.FillRect(ViewX + 3 + p.X, y + 13, w.FontFor(0, 12).Measure(p.Text), 1, p.Color);
                }

                w.ResetClip();
                w.DrawScrollBar(ViewX + ViewW - 13, vy, vh, Math.Max(0, contentH - vh), contentH, vh, scroll);
                // ◀ ▶ and the page counter centred in the 36-px gap
                int pages = current?.Pages.Count ?? 0;
                w.DrawBitmapRaw(415, 319, Tip + (page > 0 ? "bt_arrowl_on.bmp" : "bt_arrowl_off.bmp"));
                w.DrawBitmapRaw(462, 319, Tip + (page + 1 < pages ? "bt_arrowr_on.bmp" : "bt_arrowr_off.bmp"));
                string n = (page + 1).ToString(CultureInfo.InvariantCulture), m = "/" + pages.ToString(CultureInfo.InvariantCulture);
                int wn = w.FontFor(0, 12).Measure(n), wm = w.FontFor(0, 12).Measure(m), x0 = (36 - wm - wn) / 2;
                w.TextOut(426 + x0, 320, n, 0, 12, TitleBlue, true);
                w.TextOut(426 + x0 + wn, 320, m, 0, 12, TitleBlue);
            }

            // "Open on Startup" and Close
            w.DrawBitmapRaw(9, 349, Tip + (StartupFlag ? "checkbox_check.bmp" : "checkbox.bmp"));
            w.TextOut(23, 347, Msg(0xf63, "Open on Startup"), 0, 12, Black);
            w.DrawBitmapRaw(415, 341, Tip + (closePressed ? "btn_close_press.bmp" : "btn_close_normal.bmp"));
            string close = Msg(0x513, "Close");
            w.TextOut(415 + (60 - w.FontFor(0, 12).Measure(close)) / 2, 344, close, 0, 12, Black);
        }

        /// <summary>The PageEX icons (0x853060): ico_effect&lt;N&gt; at their window place; twinkling 500 ms on / off after the first second.</summary>
        private void DrawEffects(RoClassicWindow w) {
            float t = (Time.unscaledTime - effectStart) * 1000f;
            foreach (var e in current.Effects) {
                if (e.Effect == 6) continue;
                if (e.Twinkle && t >= 1000f && ((int) (t - 1000f) / 500) % 2 == 0) continue;
                float k = e.MoveTime > 0 ? (t % (e.MoveTime * 1000f)) / (e.MoveTime * 1000f) : 0f;
                int x = e.X + (int) ((e.DestX - e.X) * k), y = e.Y + (int) ((e.DestY - e.Y) * k);
                w.DrawBitmap(x, y, Tip + "ico_effect" + e.Effect.ToString(CultureInfo.InvariantCulture) + ".bmp");
            }
        }

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            RoClassicTipBox b = Ensure();
            switch (t[0]) {
                case "toggle": Toggle(); break;
                case "show": ShowTip(int.Parse(t[1])); break;
                case "next": b?.Turn(1); break;
                case "linkat":
                    // window-local x y, as a release there would
                    string[] xy = t[1].Split(' ');
                    string link = b?.LinkAt(new Vector2Int(int.Parse(xy[0]), int.Parse(xy[1])));
                    if (link != null) b.OpenLink(link);
                    break;
                case "search":
                    if (b?.window != null) {
                        b.search.SetText(t.Length > 1 ? t[1] : string.Empty);
                        b.Search();
                    }

                    break;
                case "pick":
                    if (b != null && b.listShown && int.TryParse(t[1], out int row) && row < b.results.Count) {
                        b.Select(b.results[row].Id);
                        b.Refresh();
                    }

                    break;
            }

            return b == null ? "none" : "tips=" + b.tips.Count + " open=" + (b.window != null && b.window.Visible) + " title='" + (b.current?.Title ?? "") +
                "' page=" + (b.page + 1) + "/" + (b.current?.Pages.Count ?? 0) + " list=" + b.listShown + " results=" + b.results.Count + " startup=" + StartupFlag;
        }
    }
}
