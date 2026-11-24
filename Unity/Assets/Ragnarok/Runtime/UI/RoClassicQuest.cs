using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The quest log (codex-ui-research RUNTIME-RULES-QUESTS-EMOTIONS-NOTIFICATIONS.md "follow-up 2026-10-02"): CUIRenewQuestUI
    /// 0x2718 (381 × 466; title bar at 29, bg_quest, four 30 × 100 tabs — process / recommended / hold / wait —, the 330 × 46
    /// rows at (38, 17), Show Quest, the category strip, search and close) with the ongoing detail CUIOngoingQuestInfo 0x2719
    /// and the recommended detail CUIRecommendedQuestInfo 0x271a (342 × 412, at the list's right edge or centred).
    /// FUN_009a90a0 fills the tabs: process = active and not cool-time, hold = inactive and not cool-time, wait = cool-time,
    /// recommended = the recommendation list (active first); FUN_009a8220 applies the category (FUN_00613b20) and the
    /// search over the title and the summary.
    /// </summary>
    public sealed class RoClassicQuest : IRoClassicUiProducer {
        public const int ListId = 0x2718, OngoingId = 0x2719, RecommendedId = 0x271a;
        private const int ListW = 381, ListH = 466, DetailW = 342, DetailH = 412;
        private const int TabProcess = 1, TabRecommended = 2, TabHold = 4, TabWait = 8;
        private const int RowsX = 38, RowsY = 17, RowsW = 343, RowsH = 394, RowW = 330, RowH = 46;
        private const int CloseId = 1, SearchId = 2, DetailCloseId = 3;
        private const string Q = "유저인터페이스\\Renew_QuestUI\\";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const string Prefs = "ro.ui.classic.quest.";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 QuestName = new Color32(0x1d, 0x32, 0x55, 255);
        private static readonly Color32 ViewerText = new Color32(0x2e, 0x30, 0x38, 255);
        // CUIStaticText colours (FUN_00946810; the 0xffffff of FUN_009468a0 / 0x946880 is the disabled shadow / outline)
        private static readonly Color32 RowTitle = new Color32(0x22, 0x53, 0x95, 255);
        private static readonly Color32 Amount = new Color32(0x1d, 0x55, 0xa0, 255);
        private static readonly Color32 Deadline = new Color32(0xd2, 0x1b, 0x16, 255);
        private static readonly int[] TabMasks = { TabProcess, TabRecommended, TabHold, TabWait };
        private static readonly string[] TabNames = { "process", "recommended", "hold", "wait" };
        // visual order of the category strip and the CUITabButton ids (all 0, normal 2, sub 3, daily 4, event 5, etc 1)
        private static readonly string[] Categories = { "all", "normal", "sub", "daily", "event", "etc" };
        private static readonly int[] CategoryIds = { 0, 2, 3, 4, 5, 1 };

        private static RoClassicQuest instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private struct Row {
            public uint Id;
            public bool Recommended;
            public string Icon, Title, Summary;
            public bool Active;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private RoClassicWindow list, ongoing, recommended;
        private RoClassicEditField search;
        private int tab = TabProcess, category, scroll, selected = -1, pressed = -1, seenSequence = int.MinValue;
        private string query = string.Empty;
        private readonly List<Row> rows = new List<Row>();
        private uint ongoingQuest, recommendedQuest;
        private int ongoingScroll, recommendedScroll, ongoingContent, recommendedContent;

        public static void Tick(RoLoginShellSession session, RoItemMetadataTable metadata) {
            if (session == null) return;
            RoClassicQuest q = Ensure();
            if (q == null) return;
            q.session = session;
            q.metadata = metadata ?? RoItemMetadataTable.Empty;
            RoClassicQuestTracker.OpenDetail = q.openDetail ??= q.OpenOngoing;
            if (session.QuestWindowChangeSequence != q.seenSequence) {
                q.seenSequence = session.QuestWindowChangeSequence;
                if (q.list != null && q.list.Visible) q.Refresh();
                q.ongoing?.Invalidate();
            }
        }

        private Action<uint> openDetail;

        private static RoClassicQuest Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicQuest { system = system };
                system.Register(instance);
            }

            return instance;
        }

        public static bool IsOpen => instance?.list != null && instance.list.Visible;

        /// <summary>Alt+U / the Basic Info button: MakeWindow(0x2718) / DeleteWindow(0x2718).</summary>
        public static bool Toggle() {
            RoClassicQuest q = Ensure();
            if (q == null || q.session == null) return false;
            if (q.list != null && q.list.Visible) q.CloseList();
            else q.OpenList();
            return true;
        }

        // ------------------------------------------------------------------ 0x2718

        private void OpenList() {
            if (list == null) {
                var layout = new RoClassicWindowLayout { Class = "CUIRenewQuestUI", WindowId = ListId, Width = ListW, Height = ListH };
                RoClassicFrames.Bitmap(layout.Ops, Basic + "titlebar_left.bmp", 29, 0);
                for (int x = 29 + 12; x < 29 + 352 - 12; x += 12) RoClassicFrames.Bitmap(layout.Ops, Basic + "titlebar_mid.bmp", x, 0);
                RoClassicFrames.Bitmap(layout.Ops, Basic + "titlebar_right.bmp", 29 + 352 - 12, 0);
                RoClassicFrames.Bitmap(layout.Ops, Q + "bg_quest.bmp", 29, 17);
                RoClassicFrames.Bitmap(layout.Ops, Q + "bg_search.bmp", 29, 411);
                layout.Children.Add(Button(CloseId, Basic + "btn_close", 327, 442, 42, 20));
                layout.Children.Add(SearchButton());
                list = new RoClassicWindow(system, layout) { Visible = false, ClickableAt = _ => true };
                list.DrawDynamic += DrawList;
                list.Pressed += (w, local) => ListPressed(local);
                list.Released += (w, local) => ListReleased(local);
                list.Wheeled += (w, local, delta) => {
                    if (new RectInt(RowsX, RowsY, RowsW, RowsH).Contains(local)) ScrollRows(-Math.Sign(delta) * RowH);
                };
                list.Clicked += child => {
                    if (child.Id == CloseId) CloseList();
                    else if (child.Id == SearchId) Search();
                };
                list.Moved += w => {
                    PlayerPrefs.SetInt(Prefs + "x", w.Position.x);
                    PlayerPrefs.SetInt(Prefs + "y", w.Position.y);
                };
                list.EscClose = () => {
                    CloseList();
                    return true;
                };
                search = new RoClassicEditField { Rect = new RectInt(223, 417, 119, 16), MaxBytes = 50 };
                search.Submitted += _ => Search();
                list.AddEditField(search);
            }

            // the factory moves it to (0, 0); a saved position replaces that
            Vector2Int client = system.ClientSize;
            int px = PlayerPrefs.GetInt(Prefs + "x", 0), py = PlayerPrefs.GetInt(Prefs + "y", 0);
            list.MoveTo(Mathf.Clamp(px, 0, Math.Max(0, client.x - ListW)), Mathf.Clamp(py, 0, Math.Max(0, client.y - ListH)));
            list.Visible = true;
            system.Windows.BringToFront(list);
            system.Windows.PushEsc(list);
            Refresh();
        }

        /// <summary>vf00: closing the list deletes both details.</summary>
        private void CloseList() {
            if (list == null) return;
            list.Visible = false;
            system.Windows.RemoveEsc(list);
            CloseDetail(ongoing);
            CloseDetail(recommended);
        }

        private static RoClassicChildLayout Button(int id, string path, int x, int y, int w, int h) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            button.States["normal"] = path + ".bmp";
            button.States["hover"] = path + "_a.bmp";
            button.States["pressed"] = path + "_b.bmp";
            return button;
        }

        private static RoClassicChildLayout SearchButton() {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = SearchId, Path = "search", X = 342, Y = 416, W = 23, H = 18 };
            button.States["normal"] = Q + "btn_search_normal.bmp";
            button.States["hover"] = Q + "btn_search_over.bmp";
            button.States["pressed"] = Q + "btn_search_press.bmp";
            return button;
        }

        /// <summary>The search button / Enter in the edit: the stored query, then FUN_009a90a0(0xf).</summary>
        private void Search() {
            query = search?.Text ?? string.Empty;
            scroll = 0;
            Refresh();
        }

        /// <summary>FUN_009a90a0 for the shown tab.</summary>
        private void Refresh() {
            rows.Clear();
            selected = -1;
            if (session == null) return;
            if (tab == TabRecommended) {
                // active recommendations first, then the rest
                for (int pass = 0; pass < 2; pass++) {
                    foreach (RoRecommendedQuest r in RoClassicQuestRecommended.All) {
                        if (r.Active != (pass == 0)) continue;
                        Add(new Row { Id = r.Id, Recommended = true, Icon = r.IconName, Title = r.Title, Summary = r.Summary, Active = r.Active });
                    }
                }
            } else {
                RoQuestMetadataTable table = RoClassicQuestData.Metadata();
                RoQuestRuntimeEntrySnapshot[] quests = (RoQuestRuntimeEntrySnapshot[]) (session.QuestSnapshot().Quests ?? Array.Empty<RoQuestRuntimeEntrySnapshot>()).Clone();
                Array.Sort(quests, (a, b) => a.QuestId.CompareTo(b.QuestId));
                foreach (RoQuestRuntimeEntrySnapshot q in quests) {
                    bool coolTime = RoClassicQuestData.IsCoolTime(q.QuestId);
                    bool take = tab == TabWait ? coolTime : !coolTime && (tab == TabProcess ? q.ActiveRaw != 0 : q.ActiveRaw == 0);
                    if (!take) continue;
                    RoQuestMetadata info = table.TryGetQuest(q.QuestId, out RoQuestMetadata found) ? found : null;
                    string title = info != null && !string.IsNullOrEmpty(info.Title) ? info.Title : "Unknown Quest - ID: " + q.QuestId.ToString(CultureInfo.InvariantCulture);
                    Add(new Row { Id = q.QuestId, Icon = info?.IconName, Title = title, Summary = info?.Summary ?? string.Empty, Active = q.ActiveRaw != 0 });
                }
            }

            scroll = Mathf.Clamp(scroll, 0, MaxScroll());
            list?.Invalidate();
        }

        /// <summary>FUN_009a8220: the category, then a non-empty query must be in the title or the summary.</summary>
        private void Add(Row row) {
            if (string.IsNullOrEmpty(row.Icon)) row.Icon = "ico_nq.bmp";
            if (!RoClassicQuestData.PassesFilter(CategoryIds[category], row.Icon)) return;
            if (query.Length > 0 && (row.Title ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                (row.Summary ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) return;
            rows.Add(row);
        }

        private int MaxScroll() => Math.Max(0, rows.Count * RowH + 7 - RowsH);

        private void ScrollRows(int delta) {
            scroll = Mathf.Clamp(scroll + delta, 0, MaxScroll());
            list?.Invalidate();
        }

        private void DrawList(RoClassicWindow w) {
            // the title text is the title bar's child at (18, 0) 100 × 17, left / vertically centred
            TitleText(w, 29 + 18);
            for (int k = 0; k < 4; k++) {
                string state = TabMasks[k] == tab ? "_on" : "_off";
                w.DrawBitmap(0, 17 + 101 * k, Q + "tab_" + TabNames[k] + state + ".bmp");
            }

            w.ClipTo(RowsX, RowsY, RowsW, RowsH);
            for (int i = 0; i < rows.Count; i++) {
                int y = RowsY + i * RowH - scroll;
                if (y + RowH <= RowsY || y >= RowsY + RowsH) continue;
                DrawRow(w, RowsX + (i == pressed ? 1 : 0), y + (i == pressed ? 1 : 0), i);
            }

            w.ResetClip();
            DrawScroll(w, RowsX + 336, RowsY, RowsH, MaxScroll(), scroll);
            w.DrawBitmap(38, 448, Q + (RoClassicQuestTracker.Visible ? "checkbox_on.bmp" : "checkbox_off.bmp"));
            w.TextOut(52, 447, RoClassicUiHost.MsgStr(0x7e3, "Show Quest"), 0, 12, Black);
            for (int k = 0; k < Categories.Length; k++) {
                w.DrawBitmap(35 + 31 * k, 414, Q + "btn_" + Categories[k] + (k == category ? "_on.bmp" : "_off.bmp"));
            }
        }

        /// <summary>
        /// CUIQuestItem (0x9a8a20 / 0x9a83d0): the row sprite (pressed / selected / normal), the icon at (8, 7), the title at
        /// (42, 6) bold #225395 and the summary at (42, 24) #2E3038; the check (312, 3) and the lock (306, 22) outside the wait tab — a
        /// recommendation has the lock only.
        /// </summary>
        private void DrawRow(RoClassicWindow w, int x, int y, int i) {
            Row row = rows[i];
            string sprite = i == pressed ? "bg_questList_press.bmp" : i == selected ? "bg_questList_check.bmp" : "bg_questList.bmp";
            w.DrawBitmap(x, y, Q + sprite);
            w.DrawBitmap(x + 8, y + 7, Q + row.Icon);
            RoClassicGdiFont bold = w.FontFor(0, 12, true), regular = w.FontFor(0, 12);
            w.TextOutPlain(x + 42, y + 6, Fit(bold, RoClassicQuestData.StripColours(row.Title), 260), bold, RowTitle);
            w.TextOutPlain(x + 42, y + 24, Fit(regular, RoClassicQuestData.StripColours(row.Summary), 260), regular, ViewerText);
            if (tab == TabWait) return;
            if (!row.Recommended) w.DrawBitmap(x + 312, y + 3, Q + (RoClassicQuestData.IsDisplayed(session, row.Id) ? "bt_check_on.bmp" : "bt_check_off.bmp"));
            w.DrawBitmap(x + 306, y + 22, Q + (row.Active ? "bt_lock_open.bmp" : "bt_lock.bmp"));
        }

        /// <summary>CUIStaticText "Quest information": msg 0xd48, black, height 13, vertically centred in the 17 px bar.</summary>
        private static void TitleText(RoClassicWindow w, int x) {
            RoClassicGdiFont font = w.FontFor(0, 13);
            int y = (int) Math.Round((17 - font.LineHeight) * 0.5, MidpointRounding.AwayFromZero);
            w.TextOutPlain(x, y, RoClassicUiHost.MsgStr(0xd48, "Quest information"), font, Black);
        }

        /// <summary>SetFit 0x9b1120: a text wider than the control is cut and ends in "...".</summary>
        private static string Fit(RoClassicGdiFont font, string text, int width) {
            if (string.IsNullOrEmpty(text) || font.Measure(text) <= width) return text ?? string.Empty;
            int n = text.Length;
            while (n > 0 && font.Measure(text.Substring(0, n) + "...") > width) n--;
            return text.Substring(0, n) + "...";
        }

        /// <summary>The Renew_QuestUI scroll bar: arrows (7 × 3) at the ends, the thumb from bt_scrollBar_up / mid / down.</summary>
        private static void DrawScroll(RoClassicWindow w, int x, int y, int h, int max, int pos) {
            w.DrawBitmap(x, y, Q + "bt_scrollbar_arrow_upper.bmp");
            w.DrawBitmap(x, y + h - 3, Q + "bt_scrollbar_arrow_down.bmp");
            if (max <= 0) return;
            int track = h - 6, content = h + max;
            int thumb = Math.Max(8, track * h / content);
            int top = y + 3 + (track - thumb) * pos / max;
            w.DrawBitmap(x, top, Q + "bt_scrollBar_up.bmp");
            for (int t = top + 4; t < top + thumb - 4; t += 4) w.DrawBitmap(x, t, Q + "bt_scrollBar_mid.bmp");
            w.DrawBitmap(x, top + thumb - 4, Q + "bt_scrollBar_down.bmp");
        }

        private int RowAt(Vector2Int local) {
            if (!new RectInt(RowsX, RowsY, RowW, RowsH).Contains(local)) return -1;
            int i = (local.y - RowsY + scroll) / RowH;
            return i >= 0 && i < rows.Count ? i : -1;
        }

        private void ListPressed(Vector2Int local) {
            for (int k = 0; k < 4; k++) {
                if (!new RectInt(0, 17 + 101 * k, 30, 100).Contains(local)) continue;
                // the tab callbacks: Msg 0x16 with the tab's mask
                tab = TabMasks[k];
                scroll = 0;
                Refresh();
                return;
            }

            for (int k = 0; k < Categories.Length; k++) {
                if (!new RectInt(35 + 31 * k, 414, 26, 22).Contains(local)) continue;
                category = k;
                scroll = 0;
                Refresh();
                return;
            }

            if (new RectInt(38, 448, 10 + 4 + 100, 12).Contains(local)) {
                // FUN_009a8000: the overlay 0xcc follows the box
                RoClassicQuestTracker.Visible = !RoClassicQuestTracker.Visible;
                list.Invalidate();
                return;
            }

            int i = RowAt(local);
            if (i < 0) return;
            int ry = RowsY + i * RowH - scroll;
            Vector2Int inRow = new Vector2Int(local.x - RowsX, local.y - ry);
            Row row = rows[i];
            if (tab != TabWait && !row.Recommended && new RectInt(312, 3, 14, 14).Contains(inRow)) {
                // FUN_009a8090: the display byte, then the tracker
                RoClassicQuestData.SetDisplayed(session, row.Id, !RoClassicQuestData.IsDisplayed(session, row.Id));
                RoClassicQuestTracker.Refresh();
                list.Invalidate();
                return;
            }

            if (tab != TabWait && new RectInt(306, 22, 20, 17).Contains(inRow)) {
                // FUN_009a7f50: a recommendation flips locally, a quest sends CZ_ACTIVE_QUEST with the toggled byte
                if (row.Recommended) {
                    RoRecommendedQuest r = RoClassicQuestRecommended.Find(row.Id);
                    if (r != null) r.Active = !r.Active;
                    Refresh();
                } else {
                    _ = session?.BeginQuestActive(row.Id, !row.Active);
                }

                return;
            }

            // vf25: pressed until the button comes up
            pressed = i;
            list.Invalidate();
        }

        private void ListReleased(Vector2Int local) {
            int i = pressed;
            pressed = -1;
            list?.Invalidate();
            if (i < 0 || i != RowAt(local)) return;
            selected = i;
            Row row = rows[i];
            if (row.Recommended) OpenRecommended(row.Id);
            else OpenOngoing(row.Id);
        }

        // ------------------------------------------------------------------ details

        private RoClassicWindow MakeDetail(int id, string background) {
            var layout = new RoClassicWindowLayout { Class = id == OngoingId ? "CUIOngoingQuestInfo" : "CUIRecommendedQuestInfo", WindowId = id, Width = DetailW, Height = DetailH };
            RoClassicFrames.TitleBar(layout.Ops, DetailW, false);
            RoClassicFrames.Bitmap(layout.Ops, Q + background, 0, 17);
            layout.Children.Add(Button(DetailCloseId, Basic + "btn_close", 288, 388, 42, 20));
            var w = new RoClassicWindow(system, layout) { Visible = false, ClickableAt = _ => true };
            w.Clicked += child => {
                if (child.Id == DetailCloseId) CloseDetail(w);
            };
            w.EscClose = () => {
                CloseDetail(w);
                return true;
            };
            return w;
        }

        private void CloseDetail(RoClassicWindow w) {
            if (w == null || !w.Visible) return;
            w.Visible = false;
            system.Windows.RemoveEsc(w);
        }

        /// <summary>vf16: at the list's right edge when the list is open, else centred; the other detail hands over its place.</summary>
        private void PlaceDetail(RoClassicWindow w, RoClassicWindow other) {
            Vector2Int client = system.ClientSize;
            if (other != null && other.Visible) {
                w.MoveTo(other.Position.x, other.Position.y);
                CloseDetail(other);
            } else if (list != null && list.Visible) {
                w.MoveTo(list.Position.x + ListW, list.Position.y);
            } else {
                w.MoveTo((client.x - DetailW) / 2, (client.y - DetailH) / 2);
            }

            w.Visible = true;
            system.Windows.BringToFront(w);
            system.Windows.PushEsc(w);
            w.Invalidate();
        }

        /// <summary>FUN_0099db20 / FUN_0099dc20 through FUN_00614060 or a list row.</summary>
        public void OpenOngoing(uint questId) {
            if (ongoing == null) {
                ongoing = MakeDetail(OngoingId, "bg_detail_quest.bmp");
                ongoing.DrawDynamic += DrawOngoing;
                ongoing.Wheeled += (w, local, delta) => {
                    ongoingScroll = Mathf.Clamp(ongoingScroll - Math.Sign(delta) * 18, 0, Math.Max(0, ongoingContent - 322));
                    w.Invalidate();
                };
            }

            ongoingQuest = questId;
            ongoingScroll = 0;
            PlaceDetail(ongoing, recommended);
        }

        private void OpenRecommended(uint questId) {
            if (recommended == null) {
                recommended = MakeDetail(RecommendedId, "bg_detail_recommended.bmp");
                recommended.DrawDynamic += DrawRecommended;
                recommended.Wheeled += (w, local, delta) => {
                    recommendedScroll = Mathf.Clamp(recommendedScroll - Math.Sign(delta) * 18, 0, Math.Max(0, recommendedContent - 318));
                    w.Invalidate();
                };
            }

            recommendedQuest = questId;
            recommendedScroll = 0;
            PlaceDetail(recommended, ongoing);
        }

        private static void DrawDetailTitle(RoClassicWindow w, string name) {
            TitleText(w, 18);
            name = RoClassicQuestData.StripColours(name);
            RoClassicGdiFont bold = w.FontFor(0, 12, true);
            name = Fit(bold, name, DetailW);
            w.TextOutPlain((int) Math.Round((DetailW - bold.Measure(name)) * 0.5, MidpointRounding.AwayFromZero), 39, name, bold, QuestName);
        }

        /// <summary>
        /// The ongoing detail: the quest name (#1D3255) at y 39, the scroll window (0, 62, 342, 322) with Description,
        /// Monster and Reward panels (img_poring at (9, 9), the black heading at (25, 9), the #2E3038 viewer at (32, 30), 290
        /// wide with 4 px padding and 6 px line gap; a panel ends 9 px under its last child, img_line between panels), the
        /// deadline at (13, 393) in #D21B16; reward amounts #1D55A0.
        /// </summary>
        private void DrawOngoing(RoClassicWindow w) {
            RoQuestMetadata info = RoClassicQuestData.Metadata().TryGetQuest(ongoingQuest, out RoQuestMetadata found) ? found : null;
            RoQuestRuntimeEntrySnapshot quest = default;
            foreach (RoQuestRuntimeEntrySnapshot q in session?.QuestSnapshot().Quests ?? Array.Empty<RoQuestRuntimeEntrySnapshot>()) {
                if (q.QuestId == ongoingQuest) quest = q;
            }

            DrawDetailTitle(w, info?.Title ?? "Unknown Quest - ID: " + ongoingQuest.ToString(CultureInfo.InvariantCulture));
            RoClassicGdiFont font = w.FontFor(0, 12);
            int top = 62 - ongoingScroll;
            w.ClipTo(0, 62, 335, 322);
            int y = top;
            var description = new List<string>();
            foreach (string line in info?.DescriptionLines ?? Array.Empty<string>()) Wrap(font, line, 282, description);
            y = DrawPanel(w, y, RoClassicUiHost.MsgStr(0xd49, "Description"), description);
            w.DrawBitmap(1, y, Q + "img_line.bmp");
            y += 1;
            var monsters = new List<string>();
            RoQuestObjectiveRuntimeSnapshot[] objectives = quest.Objectives ?? Array.Empty<RoQuestObjectiveRuntimeSnapshot>();
            for (int i = 0; i < Math.Min(3, objectives.Length); i++) {
                RoQuestObjectiveRuntimeSnapshot o = objectives[i];
                string label = RoClassicQuestData.HuntLabel(o);
                monsters.Add(o.Current == o.Total
                    ? label + " ( " + RoClassicUiHost.MsgStr(0x7ee, "Complete") + " )"
                    : label + " ( " + o.Current.ToString(CultureInfo.InvariantCulture) + " / " + o.Total.ToString(CultureInfo.InvariantCulture) + " )");
            }

            y = DrawPanel(w, y, RoClassicUiHost.MsgStr(0xd4a, "Monster"), monsters);
            w.DrawBitmap(1, y, Q + "img_line.bmp");
            y += 1;
            y = DrawRewards(w, y, info);
            w.ResetClip();
            ongoingContent = y - top;
            DrawScroll(w, 335, 62, 322, Math.Max(0, ongoingContent - 322), ongoingScroll);
            if (quest.EndTime > 0) {
                DateTime end = DateTimeOffset.FromUnixTimeSeconds(quest.EndTime).LocalDateTime;
                if (end.Year >= 1971) {
                    string deadline = RoClassicUiHost.MsgStr(0x893, "Deadline") + " [" + end.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture) + "]";
                    w.TextOut(13, 393, deadline, 0, 12, Deadline);
                }
            }
        }

        /// <summary>A contents panel at y: its icon, heading and viewer lines; returns the panel's bottom (last child + 9).</summary>
        private static int DrawPanel(RoClassicWindow w, int y, string heading, List<string> lines) {
            w.DrawBitmap(9, y + 9, Q + "img_poring.bmp");
            w.TextOut(25, y + 9, heading, 0, 12, Black);
            for (int i = 0; i < lines.Count; i++) ViewerLine(w, 32 + 4, y + 30 + 4 + 18 * i, lines[i], ViewerText);
            int viewer = Math.Max(20, 8 + 18 * lines.Count - (lines.Count > 0 ? 6 : 0));
            return Math.Max(y + 59, y + 30 + viewer + 9);
        }

        /// <summary>The Reward panel: Base Exp (32, 30) / (110, 30), Job Exp (32, 51) / (110, 51), Item (32, 72) and the item cards.</summary>
        private int DrawRewards(RoClassicWindow w, int y, RoQuestMetadata info) {
            w.DrawBitmap(9, y + 9, Q + "img_poring.bmp");
            w.TextOut(25, y + 9, RoClassicUiHost.MsgStr(0xd4b, "Reward"), 0, 12, Black);
            w.TextOut(32, y + 30, RoClassicUiHost.MsgStr(0xd4c, "Base Exp"), 0, 12, Black);
            w.TextOut(110, y + 30, (info?.RewardEXP ?? 0).ToString(CultureInfo.InvariantCulture), 0, 12, Amount);
            w.TextOut(32, y + 51, RoClassicUiHost.MsgStr(0xd4d, "Job Exp"), 0, 12, Black);
            w.TextOut(110, y + 51, (info?.RewardJEXP ?? 0).ToString(CultureInfo.InvariantCulture), 0, 12, Amount);
            w.TextOut(32, y + 72, RoClassicUiHost.MsgStr(0xd4e, "Item"), 0, 12, Black);
            int cy = y + 72;
            foreach (RoQuestRewardItem reward in info?.RewardItems ?? Array.Empty<RoQuestRewardItem>()) {
                RoItemInstance item = RoItemInstance.CreateRegular(0, unchecked((int) reward.ItemId), Math.Max(1, reward.ItemNum));
                w.DrawBitmap(110, cy, Q + "img_questIocn.bmp");
                RoClassicItemText.DrawIcon(w, 110 + 7, cy + 7, item, metadata, 0);
                w.TextOut(110 + 46, cy + 5, item.GetFullDisplayName(metadata), 0, 12, Black);
                w.TextOut(110 + 46, cy + 20, reward.ItemNum.ToString(CultureInfo.InvariantCulture), 0, 12, Amount);
                cy += 41;
            }

            return Math.Max(y + 122, cy + 9);
        }

        /// <summary>
        /// The recommended detail: the scroll viewport (3, 2, 336, 318) inside (0, 62) with the upper art bg_quest01 (9, 8),
        /// the lower art bg_quest02 (9, 221) and the black viewer (0, 0, 300) with 6 px line gap holding QuestInfo1..3.
        /// </summary>
        private void DrawRecommended(RoClassicWindow w) {
            RoRecommendedQuest r = RoClassicQuestRecommended.Find(recommendedQuest);
            DrawDetailTitle(w, r?.Title ?? string.Empty);
            int ox = 3, top = 62 + 2 - recommendedScroll;
            w.ClipTo(ox, 64, 336, 318);
            w.DrawBitmap(ox + 9, top + 8, Q + "bg_quest01.png");
            if (!string.IsNullOrEmpty(r?.BgName)) w.DrawBitmap(ox + 9, top + 8, Q + r.BgName);
            w.DrawBitmap(ox + 9, top + 221, Q + "bg_quest02.png");
            var lines = new List<string>();
            RoClassicGdiFont font = w.FontFor(0, 12);
            if (r != null) {
                foreach (List<string> group in r.Groups) {
                    foreach (string text in group) Wrap(font, text, 300, lines);
                }
            }

            for (int i = 0; i < lines.Count; i++) ViewerLine(w, ox, top + 18 * i, lines[i], Black);
            w.ResetClip();
            recommendedContent = Math.Max(313, 18 * lines.Count);
            DrawScroll(w, 335, 62, 322, Math.Max(0, recommendedContent - 318), recommendedScroll);
        }

        /// <summary>A CUITextViewer line: its base colour, ^RRGGBB switching it.</summary>
        private static void ViewerLine(RoClassicWindow w, int x, int y, string text, Color32 colour) {
            w.TextOutColored(x, y, text, 0, text.Length, 0, 12, ref colour);
        }

        /// <summary>FUN_009400d0's lines: words wrapped at the width in pixels (colour codes take no width).</summary>
        private static void Wrap(RoClassicGdiFont font, string text, int width, List<string> into) {
            text ??= string.Empty;
            int start = 0;
            while (start <= text.Length) {
                int end = start, lastSpace = -1;
                while (end < text.Length && font.Measure(RoClassicQuestData.StripColours(text.Substring(start, end - start + 1))) <= width) {
                    if (text[end] == ' ') lastSpace = end;
                    end++;
                }

                if (end >= text.Length) {
                    into.Add(text.Substring(start));
                    return;
                }

                int cut = lastSpace > start ? lastSpace : Math.Max(end, start + 1);
                into.Add(text.Substring(start, cut - start));
                start = lastSpace > start ? cut + 1 : cut;
            }
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
        }

        /// <summary>The sweep console: open | close | tab 1|2|4|8 | cat n | search text | row n | check n | lock n | detail id | wheel n | state.</summary>
        public static string Sweep(string command) {
            RoClassicQuest q = Ensure();
            if (q == null || q.session == null) return "no quest log";
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            int Arg() => a.Length > 1 && int.TryParse(a[1], out int v) ? v : 0;
            switch (a[0]) {
                case "open":
                    if (q.list == null || !q.list.Visible) q.OpenList();
                    break;
                case "close":
                    q.CloseList();
                    break;
                case "tab":
                    q.tab = Arg();
                    q.Refresh();
                    break;
                case "cat":
                    q.category = Mathf.Clamp(Arg(), 0, Categories.Length - 1);
                    q.Refresh();
                    break;
                case "search":
                    q.search?.SetText(a.Length > 1 ? a[1] : string.Empty);
                    q.Search();
                    break;
                case "row":
                case "check":
                case "lock": {
                    int i = Arg();
                    if (i < 0 || i >= q.rows.Count || q.list == null) break;
                    int ry = RowsY + i * RowH - q.scroll;
                    var at = a[0] == "row" ? new Vector2Int(RowsX + 100, ry + 20) : a[0] == "check" ? new Vector2Int(RowsX + 318, ry + 9) : new Vector2Int(RowsX + 315, ry + 30);
                    q.ListPressed(at);
                    if (a[0] == "row") q.ListReleased(at);
                    break;
                }
                case "detail":
                    q.OpenOngoing((uint) Arg());
                    break;
                case "wheel":
                    q.ScrollRows(Arg());
                    break;
            }

            var parts = new List<string>();
            foreach (Row r in q.rows) parts.Add(r.Id + (r.Active ? "+" : "-") + (r.Recommended ? "r" : string.Empty) + "[" + r.Icon + "]:" + r.Title);
            return "list=" + (q.list != null && q.list.Visible) + " tab=" + q.tab + " cat=" + q.category + " query=" + q.query + " scroll=" + q.scroll +
                   " ongoing=" + (q.ongoing != null && q.ongoing.Visible ? q.ongoingQuest.ToString(CultureInfo.InvariantCulture) : "-") +
                   " recommended=" + (q.recommended != null && q.recommended.Visible ? q.recommendedQuest.ToString(CultureInfo.InvariantCulture) : "-") +
                   " rows=" + string.Join(" | ", parts);
        }
    }
}
