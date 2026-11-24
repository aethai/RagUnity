using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UIAchievementWnd 0x10e (achievement-spec §2-§7, §9): 750×468 centred, white, "Achievements" title, the upper bar
    /// (rank gauge, score, rank; in a category the Complete / Incomplete filter, the trophy, the category's gauge), the tab
    /// tree (Summary, General, Adventure, Battle, Quest, Memorial, Feat with their sub tabs, three visible), the list (rows
    /// of 81 px, five visible, the icon a trace toggle, a 3-row wheel), the details view (stamp, title, details, objectives
    /// with bullets / gauges / strike-through, reward tiles, Receive → CZ_REQ_ACH_REWARD 0x0a25) and the summary (seven
    /// gauges, the two most recent). The tracker 0x10f (up to three traced achievements, a transparent 300×150 overlay) and
    /// the completion banner are in the same class.
    /// </summary>
    public sealed partial class RoClassicAchievements : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string A = Ui + "achievement_re\\";
        private const int W = 750, H = 468;
        private const int UpperY = 17, TabY = 63, ListX = 102, ViewX = 422;
        private const int Rows = 5, RowH = 81;
        private const int CloseId = 0xc5;
        private static readonly int[] SubCounts = { 0, 3, 6, 2, 2, 3, 0 };
        private static readonly int[] MainNames = { 0xa60, 0xa61, 0xa65, 0xa6c, 0xa6f, 0xa72, 0xa76 };
        private static readonly string[] MainDefaults = { "Summary", "General", "Adventure", "Battle", "Quest", "Memorial", "Feat" };
        private static readonly int[][] SubNames = {
            new int[0], new[] { 0xa62, 0xa63, 0xa64 }, new[] { 0xa66, 0xa67, 0xa68, 0xa69, 0xa6a, 0xa6b }, new[] { 0xa6d, 0xa6e },
            new[] { 0xa70, 0xa71 }, new[] { 0xa73, 0xa74, 0xa75 }, new int[0],
        };

        private static readonly string[][] SubDefaults = {
            new string[0], new[] { "Character", "Action", "Other" },
            new[] { "Rune Midgarts", "Schwartzvald", "Arunafeltz", "New World", "Localizing", "Dungeon" }, new[] { "Duel", "Training" },
            new[] { "Episode", "General" }, new[] { "Midgard", "New World", "Other" }, new string[0],
        };

        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Navy = Ref(0x582708), RankColor = Ref(0x044c58), RowText = Ref(0xa55239), DateColor = Ref(0x635aa5);
        private static readonly Color32 MainSel = Ref(0x9c1810), MainOther = Ref(0x9c6b5a), SubColor = Ref(0x9b6a59);
        private static readonly Color32 DetailText = Ref(0xc9977d), ObjIdle = Ref(0x995736), ObjBusy = Ref(0x4189e6), SummaryLink = Ref(0x997836);

        private static Color32 Ref(int bgr) => new Color32((byte) bgr, (byte) (bgr >> 8), (byte) (bgr >> 16), 255);

        private static RoClassicAchievements instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicWindow window;
        private RoClassicWindow tracker;
        private RoClassicUiLayer bannerLayer;
        private string bannerText;
        private float bannerTime = -100f;
        private bool bannerDirty;

        private int selMajor, selMinor = -1, mode;
        private readonly List<int> vec = new List<int>();
        private int mapCount, top, selected, viewId = -1;
        private int hoverMain = -1, hoverSub = -1, subOffset;
        private bool receivePressed;
        private int pressedIcon = -1;
        private readonly List<(int Id, string Title, RectInt Rect)> recent = new List<(int, string, RectInt)>();

        // ---------------------------------------------------------------- lifetime

        private static RoClassicAchievements Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicAchievements { system = system };
                instance.bannerLayer = system.Renderer.CreateLayer("achievement banner");
                instance.bannerLayer.AboveOverlayUi = true;
                instance.bannerLayer.Visible = false;
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Per frame from the chat window's classic ticks.</summary>
        public static void Tick(RoLoginShellSession loginSession) {
            RoClassicAchievements a = Ensure();
            if (a != null) a.session = loginSession;
        }

        /// <summary>Menu icon 0x1d5: toggles the window (MakeWindow clears the "new" badge).</summary>
        public static void Toggle(RoLoginShellSession loginSession) {
            RoClassicAchievements a = Ensure();
            if (a == null) return;
            if (loginSession != null) a.session = loginSession;
            if (a.window != null && a.window.Visible) {
                a.window.Visible = false;
                return;
            }

            a.Open();
        }

        private void Open() {
            newBadge = false;
            if (window == null) Create();
            Vector2Int c = system.ClientSize;
            window.MoveTo((c.x - W) / 2, (c.y - H) / 2);
            window.Visible = true;
            system.Windows.BringToFront(window);
            subOffset = 0;
            SelectTab(0, -1);
        }

        private void Create() {
            var layout = new RoClassicWindowLayout { Class = "UIAchievementWnd", WindowId = 0x10e, Width = W, Height = H };
            var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = 736, Y = 3, W = 11, H = 11 };
            close.States["normal"] = Basic + "sys_close_off.bmp";
            close.States["hover"] = close.States["pressed"] = Basic + "sys_close_on.bmp";
            layout.Children.Add(close);
            window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = p => p.y < UpperY };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == CloseId) window.Visible = false;
            };
            window.Pressed += (w, p) => Press(p);
            window.Released += (w, p) => Release(p);
            window.Wheeled += (w, p, delta) => Wheel(p, delta);
            window.EscClose = () => {
                window.Visible = false;
                return true;
            };
        }

        private static void RefreshWindow() {
            RoClassicAchievements a = instance;
            if (a?.window != null && a.window.Visible) a.window.Invalidate();
        }

        // ---------------------------------------------------------------- tabs and the list (§2.1, §4.2)

        /// <summary>SelectTab 0x716130.</summary>
        private void SelectTab(int major, int minor) {
            selMajor = major;
            selMinor = minor;
            if (major != 0) Rebuild();
            window.Invalidate();
        }

        /// <summary>Rebuild 0x715b80: mode 0 every id (completed first, newest first), mode 1 the claimable and the unfinished.</summary>
        private void Rebuild() {
            List<int> ids = TabIds(selMajor, selMinor);
            mapCount = ids.Count;
            vec.Clear();
            var done = new List<int>();
            foreach (int id in ids) {
                State s = StateOf(id);
                if (!s.Completed) continue;
                if (mode == 1 && !(TryInfo(id, out Info info) && info.HasReward && !s.Rewarded)) continue;
                done.Add(id);
            }

            // stable by completedAt descending (ties keep ascending id)
            var ordered = new List<(int Id, uint At, int Order)>();
            for (int i = 0; i < done.Count; i++) ordered.Add((done[i], StateOf(done[i]).CompletedAt, i));
            ordered.Sort((x, y) => x.At != y.At ? y.At.CompareTo(x.At) : x.Order.CompareTo(y.Order));
            foreach ((int id, uint _, int _) in ordered) vec.Add(id);
            foreach (int id in ids) {
                if (!StateOf(id).Completed) vec.Add(id);
            }

            top = 0;
            selected = 0;
            viewId = vec.Count > 0 ? vec[0] : -1;
        }

        private int MaxTop => Math.Max(0, mapCount - Rows);

        /// <summary>JumpTo 0x715010: the tab of the id, the row selected and scrolled into view.</summary>
        private void JumpTo(int id) {
            if (!TabOf(id, out int major, out int minor)) return;
            SelectTab(major, minor);
            int idx = vec.IndexOf(id);
            if (idx < 0) idx = vec.Count;
            selected = idx;
            top = idx < Rows ? 0 : Math.Min(idx, MaxTop);
            viewId = idx < vec.Count ? vec[idx] : -1;
            window.Invalidate();
        }

        // ---------------------------------------------------------------- input

        private void Press(Vector2Int p) {
            // the radios (category mode)
            if (selMajor != 0 && p.x >= 150 && p.x < 162) {
                int radio = p.y >= UpperY + 10 && p.y < UpperY + 22 ? 0 : p.y >= UpperY + 25 && p.y < UpperY + 37 ? 1 : -1;
                if (radio >= 0) {
                    mode = radio;
                    Rebuild();
                    window.Invalidate();
                    return;
                }
            }

            // the tab tree (and the sub tabs' scroll bar)
            if (p.x >= 0 && p.x <= 102 && p.y >= TabY && p.y < TabY + 405) {
                Vector2Int t = new Vector2Int(p.x, p.y - TabY);
                if (SubScrollable && p.x < 11 && t.y >= (selMajor + 1) * 40 && t.y < (selMajor + 1) * 40 + 78) {
                    int sy = t.y - (selMajor + 1) * 40;
                    ScrollSubs(sy < 11 ? -1 : sy >= 67 ? 1 : sy < 39 ? -1 : 1);
                    return;
                }

                (int main, int sub) = TabHit(t);
                if (main >= 0 && main < 7 && (sub < 0 || sub < SubCounts[main])) {
                    if (main != selMajor) subOffset = 0;
                    SelectTab(main, sub);
                }

                return;
            }

            if (selMajor == 0) {
                // the summary's two recent-achievement links (UITextButton → JumpTo)
                Vector2Int s = new Vector2Int(p.x - ListX, p.y - TabY);
                foreach ((int id, string _, RectInt r) in recent) {
                    if (id > 0 && r.Contains(s)) {
                        JumpTo(id);
                        return;
                    }
                }

                return;
            }

            // the list
            if (p.x >= ListX && p.x < ListX + 320 && p.y >= TabY && p.y < TabY + 405) {
                int lx = p.x - ListX, ly = p.y - TabY;
                if (lx >= 309) {
                    if (MaxTop > 0) ScrollList(ly < 11 ? -1 : ly >= 394 ? 1 : ly < 11 + ThumbTop() ? -(Rows - 1) : Rows - 1);
                    return;
                }

                int row = ly / RowH;
                if (lx >= 5 && lx < 35 && ly - RowH * row >= 5 && ly - RowH * row < 35 && top + row < mapCount) {
                    pressedIcon = row;
                    return;
                }

                if (row < mapCount) {
                    selected = top + row;
                    viewId = selected < vec.Count ? vec[selected] : -1;
                    window.Invalidate();
                }

                return;
            }

            // the view's Receive button
            if (ReceiveShown(out bool enabled) && enabled && p.x >= ViewX + 90 && p.x < ViewX + 238 && p.y >= TabY + 372 && p.y < TabY + 392) {
                receivePressed = true;
                window.Invalidate();
            }
        }

        private void Release(Vector2Int p) {
            if (pressedIcon >= 0) {
                int row = pressedIcon;
                pressedIcon = -1;
                int lx = p.x - ListX, ly = p.y - TabY - RowH * row;
                if (lx >= 5 && lx < 35 && ly >= 5 && ly < 35 && top + row < vec.Count) {
                    if (ToggleTrace(vec[top + row])) OpenTracker();
                    window.Invalidate();
                    tracker?.Invalidate();
                }

                return;
            }

            if (!receivePressed) return;
            receivePressed = false;
            window.Invalidate();
            if (p.x >= ViewX + 90 && p.x < ViewX + 238 && p.y >= TabY + 372 && p.y < TabY + 392 && viewId > 0) {
                _ = session?.BeginEntryQueuePacket(0x0a25, BitConverter.GetBytes(viewId));
            }
        }

        private void Wheel(Vector2Int p, int delta) {
            if (selMajor != 0 && p.x >= ViewX + 12 && p.x < ViewX + 312 && p.y >= TabY + 34 && p.y < TabY + 94 && detailLines > 3) {
                int top = Mathf.Clamp(detailTop + (delta < 0 ? 3 : -3), 0, detailLines - 3);
                if (top != detailTop) {
                    detailTop = top;
                    window.Invalidate();
                }

                return;
            }

            if (p.x >= ListX && p.x < ListX + 320 && p.y >= TabY && MaxTop > 0) ScrollList(delta < 0 ? 3 : -3);
            else if (p.x <= 102 && p.y >= TabY && SubScrollable) ScrollSubs(delta < 0 ? 1 : -1);
        }

        private void ScrollList(int d) {
            int t = Mathf.Clamp(top + d, 0, MaxTop);
            if (t == top) return;
            top = t;
            window.Invalidate();
        }

        private bool SubScrollable => SubCounts[Mathf.Clamp(selMajor, 0, 6)] > 3;

        private void ScrollSubs(int d) {
            int max = Math.Max(0, SubCounts[selMajor] - 3);
            int o = Mathf.Clamp(subOffset + d, 0, max);
            if (o == subOffset) return;
            subOffset = o;
            window.Invalidate();
        }

        /// <summary>The hit test 0x711740 (tab-local).</summary>
        private (int Main, int Sub) TabHit(Vector2Int t) {
            int s0 = (selMajor + 1) * 40, n = Math.Min(SubCounts[selMajor], 3);
            if (t.y > s0 && t.y < s0 + 26 * n) return (selMajor, (t.y - s0) / 26 + subOffset);
            int y = t.y >= s0 ? t.y - 26 * n : t.y;
            return (y / 40, -1);
        }

        private int ThumbTop() {
            int max = MaxTop;
            if (max <= 0) return 0;
            int len = Math.Max(8, (405 - 22) * Rows / Math.Max(1, mapCount));
            return top * (405 - 22 - len) / max;
        }

        // ---------------------------------------------------------------- drawing

        private void Draw(RoClassicWindow w) {
            w.FillRect(0, 0, W, H, White);
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            string title = RoClassicUiHost.MsgStr(0xa56, "Achievements");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            DrawUpper(w);
            DrawTabs(w);
            if (selMajor == 0) DrawSummary(w);
            else {
                DrawList(w);
                DrawView(w);
            }
        }

        private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>TextOutBox: centred in boxW.</summary>
        private static void Box(RoClassicWindow w, int x, int y, string s, int boxW, int h, Color32 c, bool bold) {
            int tw = w.FontFor(0, h, bold).Measure(s);
            w.TextOut(x + (boxW - tw) / 2, y, s, 0, h, c, bold);
        }

        /// <summary>UICustomGageBar 0x714a20: two rectangles, the background, the bar (p % of W, not clamped to the border).</summary>
        private static void Gauge(RoClassicWindow w, int x, int y, int gw, int gh, long cur, long max, bool summary) {
            if (cur < 0 || cur > max) return;   // drawn while 0 ≤ cur ≤ max, 0 / 0 included (it covers the baked bar)
            Color32 bar = summary ? Ref(0xc78a71) : Ref(0xce7b52), outer = summary ? White : Ref(0xefe7e7), inner = summary ? Ref(0xdaa896) : Ref(0xadadad);
            Rect(w, x, y, gw, gh, outer);
            Rect(w, x + 1, y + 1, gw - 2, gh - 2, inner);
            w.FillRect(x + 2, y + 2, gw - 4, gh - 4, White);
            int p = max > 0 ? (int) (cur * 100f / max) : 0;
            if (p > 0) w.FillRect(x + 2, y + 2, p * gw / 100, gh - 4, bar);
        }

        private static void Rect(RoClassicWindow w, int x, int y, int rw, int rh, Color32 c) {
            w.FillRect(x, y, rw, 1, c);
            w.FillRect(x, y + rh - 1, rw, 1, c);
            w.FillRect(x, y, 1, rh, c);
            w.FillRect(x + rw - 1, y, 1, rh, c);
        }

        private string Caption() {
            if (selMajor == 0) return RoClassicUiHost.MsgStr(0xa77, "Achieved challenges");
            if (selMinor < 0) return RoClassicUiHost.MsgStr(MainNames[selMajor], MainDefaults[selMajor]);
            return RoClassicUiHost.MsgStr(SubNames[selMajor][selMinor], SubDefaults[selMajor][selMinor]);
        }

        /// <summary>UIAchUpperWnd draw 0x7143b0.</summary>
        private void DrawUpper(RoClassicWindow w) {
            int y0 = UpperY;
            w.DrawBitmapRaw(0, y0, A + "bg_upper.bmp");
            Gauge(w, 47, y0 + 24, 84, 10, rankPoints, nextRankPoints, false);
            if (selMajor != 0) {
                w.DrawBitmap(575, y0 + 3, A + "upper_trophy.bmp");
                w.TextOut(169, y0 + 11, RoClassicUiHost.MsgStr(0xc22, "Complete"), 0, 12, Black);
                w.TextOut(169, y0 + 26, RoClassicUiHost.MsgStr(0xc23, "Incomplete"), 0, 12, Black);
                w.TextOut(614, y0 + 15, Caption(), 0, 11, Navy);
            }

            w.TextOutRight(130, y0 + 13, Num(rankPoints) + "/" + Num(nextRankPoints), 0, 11, Black);
            if (selMajor != 0) {
                (int done, int total) = Count(selMajor, selMinor);
                w.TextOutRight(737, y0 + 15, "(" + done + "/" + total + ")", 0, 11, Navy);
                Gauge(w, 613, y0 + 29, 124, 8, done, total, false);
                w.DrawBitmap(150, y0 + 10, Ui + (mode == 0 ? "radiobtn_on.bmp" : "radiobtn_off.bmp"));
                w.DrawBitmap(150, y0 + 25, Ui + (mode == 1 ? "radiobtn_on.bmp" : "radiobtn_off.bmp"));
            }

            Box(w, 353, y0 + 6, Num(totalScore), 46, 12, Navy, true);
            Box(w, 16, y0 + 19, Num(rank), 15, 12, RankColor, true);
        }

        /// <summary>UIAchTab draw 0x714300.</summary>
        private void DrawTabs(RoClassicWindow w) {
            int y0 = TabY;
            w.DrawBitmapRaw(0, y0, A + "bg_tab.bmp");
            w.ClipTo(0, y0, 102, 405);
            int n = Math.Min(SubCounts[selMajor], 3);
            int th = w.FontFor(0, 12, true).LineHeight;
            for (int m = 0; m < 7; m++) {
                int y = 40 * m + (m > selMajor ? 26 * n : 0);
                string name = RoClassicUiHost.MsgStr(MainNames[m], MainDefaults[m]);
                if (m == selMajor) {
                    w.DrawBitmap(0, y0 + y, A + "tab_press.bmp");
                    w.TextOut(11, y0 + y + (40 - th) / 2, name, 0, 12, MainSel, true);
                    for (int v = 0; v < n; v++) {
                        int k = v + subOffset;
                        if (k >= SubCounts[m]) break;
                        int ys = (selMajor + 1) * 40 + 26 * v;
                        bool sel = k == selMinor;
                        w.DrawBitmap(0, y0 + ys, A + (sel ? "tab_sub_press.bmp" : k == hoverSub && hoverMain == m ? "tab_sub_over.bmp" : "tab_sub_out.bmp"));
                        if (sel) w.DrawBitmap(12, y0 + ys + 7, A + "tab_subarrow.bmp");
                        w.TextOut(26, y0 + ys + (26 - th) / 2, RoClassicUiHost.MsgStr(SubNames[m][k], SubDefaults[m][k]), 0, 12, SubColor);
                    }

                    if (SubScrollable) DrawScroll(w, 0, y0 + (selMajor + 1) * 40, 78, subOffset, SubCounts[m] - 3, 3, SubCounts[m]);
                } else {
                    w.DrawBitmap(0, y0 + y, A + (m == hoverMain && hoverSub < 0 ? "tab_over.bmp" : "tab_out.bmp"));
                    w.TextOut(11, y0 + y + (40 - th) / 2, name, 0, 12, MainOther, true);
                }
            }

            w.ResetClip();
        }

        /// <summary>UIAchScrollBar draw 0x713990 (only with a range).</summary>
        private static void DrawScroll(RoClassicWindow w, int x, int y, int h, int pos, int max, int page, int total) {
            if (max <= 0) return;
            for (int t = 0; t < h / 11; t++) w.DrawBitmap(x, y + 11 * t, A + "scroll_v_bg_mid.bmp");
            w.DrawBitmap(x, y + h - 11, A + "scroll_v_bg_down.bmp");
            w.DrawBitmap(x, y, A + "scroll_v_arrow_up.bmp");
            w.DrawBitmap(x, y + h - 11, A + "scroll_v_arrow_down.bmp");
            int len = Math.Max(8, (h - 22) * page / Math.Max(1, total));
            int t0 = 11 + pos * (h - 22 - len) / max;
            w.DrawBitmap(x, y + t0, A + "scroll_v_thumb_up.bmp");
            for (int ty = t0 + 4; ty < t0 + len - 4; ty += 4) w.DrawBitmap(x, y + ty, A + "scroll_v_thumb_mid.bmp");
            w.DrawBitmap(x, y + t0 + len - 4, A + "scroll_v_thumb_down.bmp");
        }

        private static string Date(State s) =>
            string.Format(CultureInfo.InvariantCulture, "{0,4}.{1,2}.{2,2}", s.Year, s.Month, s.Day);

        /// <summary>UIAchListWnd draw 0x713390.</summary>
        private void DrawList(RoClassicWindow w) {
            int x0 = ListX, y0 = TabY;
            w.DrawBitmapRaw(x0, y0, A + "bg_list.bmp");
            for (int i = 0; i < Rows; i++) {
                int idx = top + i;
                if (idx >= vec.Count || !TryInfo(vec[idx], out Info info)) continue;
                State s = StateOf(info.Id);
                int y = y0 + RowH * i;
                string row = idx == selected ? s.Completed ? "list_complete_press.bmp" : "list_press.bmp" : s.Completed ? "list_complete_out.bmp" : "list_out.bmp";
                w.DrawBitmapRaw(x0, y, A + row);
                w.DrawBitmap(x0 + 8, y + 3, A + "icon_" + info.Group.ToLowerInvariant() + ".bmp");
                if (IsTraced(info.Id)) w.DrawBitmap(x0 + 8, y + 3, A + "list_pin.bmp");
                w.TextOut(x0 + 53, y + 14, info.Title, 0, 12, RowText, true);
                Box(w, x0 + 258, y + 14, info.Score.ToString("D", CultureInfo.InvariantCulture).PadLeft(3), 40, 12, RowText, true);
                w.TextOut(x0 + 10, y + 54, info.Summary, 0, 12, RowText);
                if (s.Completed) w.DrawBitmap(x0 + 250, y + 5, A + "badge_complete.bmp");
                if (info.HasReward && !s.Rewarded) w.DrawBitmap(x0 + 263, y + 49, A + (s.Completed ? "list_rewardbox_not_receive.bmp" : "list_rewardbox_default.bmp"));
                if (s.Completed) {
                    string d = Date(s);
                    int dw = w.FontFor(0, 10).Measure(d);
                    w.TextOutOutline((int) (x0 + 276 - dw * 0.5f), y + 55, d, 0, 10, DateColor, White);
                }
            }

            DrawScroll(w, x0 + 309, y0, 405, top, MaxTop, Rows, mapCount);
        }

        private bool ReceiveShown(out bool enabled) {
            enabled = false;
            if (viewId <= 0 || !TryInfo(viewId, out Info info)) return false;
            State s = StateOf(viewId);
            enabled = s.Completed;
            return info.HasReward && !s.Rewarded;
        }

        private readonly List<(int Start, int End)> wrap = new List<(int, int)>();

        /// <summary>UIAchViewWnd draw 0x7147c0 and its viewers (UIAchTV 0x714150) after Refresh 0x716780.</summary>
        private void DrawView(RoClassicWindow w) {
            int x0 = ViewX, y0 = TabY;
            w.DrawBitmapRaw(x0, y0, A + "bg_detail.bmp");
            if (viewId <= 0 || !TryInfo(viewId, out Info info)) return;
            State s = StateOf(viewId);
            if (s.Completed) w.DrawBitmap(x0 + 264, y0 + 3, A + "detail_stamp.bmp");
            w.TextOut(x0 + 15, y0 + 14, info.Title, 0, 12, RowText, true);
            // the details: 50-byte lines, three visible, #7D97C9 outlined in white
            RoClassicWindow.WrapBytes(info.Details, 50, wrap);
            detailLines = wrap.Count;
            detailTop = Mathf.Clamp(detailTop, 0, Math.Max(0, detailLines - 3));
            for (int k = 0; k < 3 && detailTop + k < wrap.Count; k++) {
                (int start, int end) = wrap[detailTop + k];
                w.TextOutOutline(x0 + 12 + 3, y0 + 34 + 4 + 18 * k, info.Details.Substring(start, end - start), 0, 12, DetailText, White);
            }

            // the viewer's 13-px scroll bar at (W − 13, 0), drawn only with more lines than fit (⌊60 / 18⌋ = 3)
            w.DrawScrollBar(x0 + 12 + 300 - 13, y0 + 34, 60, Math.Max(0, detailLines - 3), detailLines, 3, detailTop);

            for (int i = 0; i < info.Resources.Count && i < 10; i++) {
                Resource r = info.Resources[i];
                uint obj = s.Obj(i);
                string text;
                int vy;
                bool done;
                Color32 c;
                if (info.UiType == 1) {
                    text = r.Text + " ( " + obj + " / " + r.Count + " )";
                    vy = 105 + 36 * i;
                    done = obj == r.Count;
                    c = done ? DetailText : obj == 0 ? ObjIdle : ObjBusy;
                    w.DrawBitmap(x0 + 20, y0 + 111 + 36 * i, A + "detail_bullet.bmp");
                    Gauge(w, x0 + 28, y0 + 123 + 36 * i, 254, 10, obj, r.Count, true);
                } else {
                    text = r.Text;
                    vy = 97 + 19 * i;
                    done = obj > 0;
                    c = done ? DetailText : ObjIdle;
                    w.DrawBitmap(x0 + 20, y0 + 103 + 19 * i, A + "detail_bullet.bmp");
                }

                RoClassicWindow.WrapBytes(text, 56, wrap);
                RoClassicGdiFont font = w.FontFor(0, 12);
                for (int k = 0; k < wrap.Count && k < 2; k++) {
                    string line = text.Substring(wrap[k].Start, wrap[k].End - wrap[k].Start);
                    w.TextOut(x0 + 28 + 3, y0 + vy + 4 + 18 * k, line, 0, 12, c);
                    if (done) w.FillRect(x0 + 28 + 2, y0 + vy + 18 * k + 10, font.Measure(line) + 1, 1, c);
                }
            }

            // the reward tiles: title, buff, item, 62 px each and 8 apart, centred at y 304
            int n = (info.RewardTitle > 0 ? 1 : 0) + (info.RewardBuff > 0 ? 1 : 0) + (info.RewardItem > 0 ? 1 : 0);
            int tx = (int) ((328 - 8 * n - 62 * n + 8) * 0.5f);
            tiles.Clear();
            if (info.RewardTitle > 0) {
                tiles.Add((new RectInt(x0 + tx, y0 + 304, 62, 62), TitleName(info.RewardTitle)));
                w.DrawBitmapRaw(x0 + tx, y0 + 304, A + "reward_title.bmp");
                tx += 70;
            }

            if (info.RewardBuff > 0) {
                tiles.Add((new RectInt(x0 + tx, y0 + 304, 62, 62), BuffTitle(info.RewardBuff)));
                w.DrawBitmapRaw(x0 + tx, y0 + 304, A + "reward_buff.bmp");
                tx += 70;
            }

            if (info.RewardItem > 0) {
                w.DrawBitmapRaw(x0 + tx, y0 + 304, A + "reward_item.bmp");
                RoItemMetadataTable items = RoClassicUiHost.GameAssets()?.GameData?.Items;
                if (items != null && items.TryGetItem(info.RewardItem, out RoItemMetadata meta)) w.DrawBitmap(x0 + tx + 20, y0 + 304 + 15, Ui + "item\\" + meta.GetResourceName(true) + ".bmp");
            }

            if (ReceiveShown(out bool enabled)) {
                Vector2Int m = system.Windows.Mouse - window.Position;
                bool over = m.x >= x0 + 90 && m.x < x0 + 238 && m.y >= y0 + 372 && m.y < y0 + 392;
                string state = !enabled ? "disable" : receivePressed ? "press" : over ? "over" : "out";
                w.DrawBitmap(x0 + 90, y0 + 372, A + "btn_receive_" + state + ".bmp");
            }
        }

        private int detailTop, detailLines, detailFor = -1;
        private readonly List<(RectInt Rect, string Tip)> tiles = new List<(RectInt, string)>();
        private static Dictionary<int, string> titleNames;
        private static RoStatusIconDatabase buffNames;

        /// <summary>0xd192c0 → Lua GetTitleString(id): datainfo\titletable.lub's title_tbl; "" for an unknown id (no tooltip).</summary>
        private static string TitleName(int id) {
            if (titleNames == null) {
                titleNames = new Dictionary<int, string>();
                RoAssetDatabase assets = RoClassicUiHost.GameAssets();
                string text = assets?.ReadText("data/luafiles514/lua files/datainfo/titletable.lub");
                if (string.IsNullOrEmpty(text) && assets != null && assets.TryFindFirstPathEndingWith("datainfo/titletable.lub", out string path)) text = assets.ReadText(path);
                if (!string.IsNullOrEmpty(text)) {
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\[\s*(\d+)\s*\]\s*=\s*""([^""]*)""")) {
                        titleNames[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)] = m.Groups[2].Value;
                    }
                }
            }

            return titleNames.TryGetValue(id, out string name) ? name : string.Empty;
        }

        /// <summary>GetStateIconDescript(buff, 1)'s text: the buff's title line.</summary>
        private static string BuffTitle(int efst) {
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null) return string.Empty;
            buffNames ??= new RoStatusIconDatabase(assets);
            return buffNames.TryResolveDefinition((ushort) efst, out RoStatusIconDefinition def) && def.DescriptionLines.Length > 0 ? def.DescriptionLines[0].Text : string.Empty;
        }

        /// <summary>UIAchSummaryWnd draw 0x713b50 after Refresh 0x716370.</summary>
        private void DrawSummary(RoClassicWindow w) {
            int x0 = ListX, y0 = TabY;
            w.DrawBitmapRaw(x0, y0, A + "bg_summary.bmp");
            (int d0, int t0) = Count(0, -1);
            w.TextOut(x0 + 82, y0 + 27, RoClassicUiHost.MsgStr(0xa77, "Achieved challenges"), 0, 12, RowText, true);
            w.TextOutRight(x0 + 600, y0 + 28, d0 + "/" + t0, 0, 11, DateColor);
            Gauge(w, x0 + 43, y0 + 49, 558, 10, d0, t0, true);
            for (int k = 0; k < 6; k++) {
                (int dk, int tk) = Count(k + 1, -1);
                int cx = 302 * (k & 1), cy = 50 * (k >> 1);
                w.TextOut(x0 + 82 + cx, y0 + 107 + cy, RoClassicUiHost.MsgStr(MainNames[k + 1], MainDefaults[k + 1]), 0, 12, RowText, true);
                w.TextOutRight(x0 + 296 + 304 * (k & 1), y0 + 108 + cy, dk + "/" + tk, 0, 11, DateColor);
                Gauge(w, x0 + 43 + 304 * (k & 1), y0 + 123 + cy, 254, 10, dk, tk, true);
            }

            w.TextOut(x0 + 9, y0 + 292, RoClassicUiHost.MsgStr(0xa5b, "Recent completed achievements"), 0, 12, Ref(0x9c6b5a), true);
            // 0xd13190: the two newest completions, left the most recent
            int a = -1, b = -1;
            uint at = 0, bt = 0;
            foreach (KeyValuePair<int, State> e in states) {
                if (!e.Value.Completed) continue;
                if (a < 0 || e.Value.CompletedAt > at) {
                    b = a;
                    bt = at;
                    a = e.Key;
                    at = e.Value.CompletedAt;
                } else if (b < 0 || e.Value.CompletedAt > bt) {
                    b = e.Key;
                    bt = e.Value.CompletedAt;
                }
            }

            recent.Clear();
            int[] ids = { a, b };
            for (int j = 0; j < 2; j++) {
                if (ids[j] < 0 || !TryInfo(ids[j], out Info info)) continue;
                State s = StateOf(ids[j]);
                int bx = 260 + 316 * j;
                w.DrawBitmap(x0 + bx - 242, y0 + 314, A + "icon_" + info.Group.ToLowerInvariant() + ".bmp");
                w.DrawBitmap(x0 + bx, y0 + 316, A + "badge_complete.bmp");
                w.TextOut(x0 + bx + 23, y0 + 325, Num(info.Score), 0, 12, RowText, true);
                w.TextOut(x0 + bx - 241, y0 + 365, info.Summary, 0, 12, DetailText);
                if (info.HasReward && !s.Rewarded) w.DrawBitmap(x0 + bx + 12, y0 + 360, A + "list_rewardbox_not_receive.bmp");
                w.TextOutOutline(x0 + bx - 3, y0 + 366, Date(s), 0, 10, DateColor, White);
                // the title as an outlined bold UITextButton at (62 | 378, 325)
                int lx = j == 0 ? 62 : 378;
                Vector2Int m = system.Windows.Mouse - window.Position - new Vector2Int(x0, y0);
                int tw = w.FontFor(0, 12, true).Measure(info.Title) + 2;
                var rect = new RectInt(lx, 325, tw, 14);
                w.TextOutOutline(x0 + lx + 1, y0 + 325 + 1, info.Title, 0, 12, rect.Contains(m) ? DateColor : SummaryLink, White, true);
                recent.Add((ids[j], info.Title, rect));
            }
        }

        // ---------------------------------------------------------------- per frame: hover, banner

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (bannerText != null && Time.unscaledTime - bannerTime > 10f) {
                bannerText = null;
                bannerLayer.Visible = false;
            } else if (bannerDirty) {
                bannerDirty = false;
                DrawBanner(ui);
            }

            if (window == null || !window.Visible) return;
            Vector2Int m = ui.Windows.Mouse - window.Position;
            if (m.x >= 0 && m.x <= 102 && m.y >= TabY && m.y < TabY + 405) {
                (int main, int sub) = TabHit(new Vector2Int(m.x, m.y - TabY));
                if (main != hoverMain || sub != hoverSub) {
                    hoverMain = main;
                    hoverSub = sub;
                    window.Invalidate();
                }
            }

            if (viewId != detailFor) {
                detailFor = viewId;
                detailTop = 0;
            }

            if (selMajor != 0) {
                foreach ((RectInt rect, string tip) in tiles) {
                    if (!rect.Contains(m) || string.IsNullOrEmpty(tip)) continue;
                    RoClassicTooltip.Shared.Show(tip, White, window.Position.x + rect.x, window.Position.y + rect.y - 20);
                    break;
                }
            }

            // the Receive button's hover and the summary links' hover repaint
            if (m.y >= TabY + 300 && m.y < TabY + 400 && m.x >= ListX && m.x < W) window.Invalidate();
        }

        /// <summary>UIAchBalloonText: a 50-px band at y 50 the width of the screen, black at 40 %, white bold 20 lines with a shadow.</summary>
        private static void ShowBanner(string text) {
            RoClassicAchievements a = Ensure();
            if (a == null) return;
            // a new UIAchBalloonText joins the window manager on top of the open windows (0x9bc030); a completion
            // within 10 s reuses it where it is
            if (!a.bannerLayer.Visible) a.system.Renderer.BringToFront(a.bannerLayer);
            a.bannerText = text;
            a.bannerTime = Time.unscaledTime;
            a.bannerDirty = true;
        }

        private void DrawBanner(RoClassicUiSystem ui) {
            Vector2Int c = ui.ClientSize;
            RoClassicUiQuads q = bannerLayer.Quads;
            q.Clear();
            q.Image(0, 50, c.x, 50, ui.White, new Color32(0, 0, 0, 0x66));
            RoClassicGdiFont font = ui.Font(20, true);
            var lines = new List<(int Start, int End)>();
            RoClassicText.ByteWrap(bannerText, 72, lines);
            int lh = font.LineHeight, n = lines.Count;
            int y0 = (50 - (lh + 4) * n + 4) / 2;
            for (int k = 0; k < n; k++) {
                string line = bannerText.Substring(lines[k].Start, lines[k].End - lines[k].Start);
                int y = 50 + (k == 0 ? y0 : y0 - 4 + (lh + 4) * k);
                int x = (c.x - font.Measure(line)) / 2;
                q.Text(font, x + 1, y + 1, line, Black);
                q.Text(font, x, y, line, White);
            }

            bannerLayer.Visible = true;
            bannerLayer.MarkDirty();
        }

        // ---------------------------------------------------------------- the tracker 0x10f (§8.2)

        private static void OpenTracker() {
            RoClassicAchievements a = Ensure();
            if (a == null) return;
            if (a.tracker == null) a.CreateTracker();
            if (!a.tracker.Visible) {
                Vector2Int c = a.system.ClientSize;
                a.tracker.MoveTo(c.x - 350, (c.y - 150) / 2);
                a.tracker.Visible = true;
            }

            a.tracker.Invalidate();
        }

        private static void CloseTracker() {
            if (instance?.tracker != null) instance.tracker.Visible = false;
        }

        private const string Bullet = "¡Ü", Arrow = "¢º";   // A1 DC / A2 BA

        private readonly List<(RectInt Rect, int Id, bool Arrow)> trackerButtons = new List<(RectInt, int, bool)>();

        private void CreateTracker() {
            var layout = new RoClassicWindowLayout { Class = "UIAchTracingWnd", WindowId = 0x10f, Width = 300, Height = 150 };
            tracker = new RoClassicWindow(system, layout) { Visible = false };
            tracker.DrawDynamic += DrawTracker;
            tracker.Pressed += (w, p) => {
                foreach ((RectInt rect, int id, bool arrow) in trackerButtons) {
                    if (!rect.Contains(p)) continue;
                    if (arrow) {
                        if (window == null || !window.Visible) Open();
                        JumpTo(id);
                    } else {
                        Traced t = traced.Find(e => e.Id == id);
                        if (t != null) t.Expanded = !t.Expanded;
                        tracker.Invalidate();
                    }

                    return;
                }
            };
        }

        /// <summary>OnDraw 0x7178a0: no background; unfinished entries first, "● [title]" (+n) and "▶", expanded lines.</summary>
        private void DrawTracker(RoClassicWindow w) {
            trackerButtons.Clear();
            int y = 10;
            for (int pass = 0; pass < 2; pass++) {
                foreach (Traced t in traced) {
                    if (t.Completed != (pass == 1)) continue;
                    // the exe's CP949 "● [%s]" / "▶" bytes drawn in the system ANSI page, as a Western Windows shows them
                    string caption = Bullet + " [" + t.Title + "]";
                    int bw = w.FontFor(0, 12, true).Measure(caption) + 2;
                    w.TextOutOutline(10 + 1, y + 1, caption, 0, 12, Black, White, true);
                    trackerButtons.Add((new RectInt(10, y, bw, 14), t.Id, false));
                    w.TextOutOutline(280 + 1, y + 1, Arrow, 0, 12, Black, White, true);
                    trackerButtons.Add((new RectInt(280, y, 16, 14), t.Id, true));
                    if (!t.Expanded) {
                        string more = "(+" + t.Lines.Count + ")";
                        w.TextOut(bw + 16, y + 1, more, 0, 12, White, true);
                        w.TextOut(bw + 15, y, more, 0, 12, Black, true);
                    }

                    y += 15;
                    if (t.Expanded) {
                        foreach ((string text, bool done) in t.Lines) {
                            w.TextOut(21, y + 1, text, 0, 12, Black);
                            w.TextOut(20, y, text, 0, 12, done ? new Color32(0x6f, 0xff, 0x0d, 255) : White);
                            y += 15;
                        }
                    }

                    y += 5;
                }
            }

            // clicks pass through outside the drawn rows
            w.HitArea = new RectInt(0, 0, 300, Math.Max(1, Math.Min(150, y)));
        }

        // ---------------------------------------------------------------- sweep

        /// <summary>!ach open|tab m s|mode n|row i|icon i|receive|jump id|inject hex|state.</summary>
        internal static string Sweep(string args, RoLoginShellSession loginSession) {
            RoClassicAchievements a = Ensure();
            if (a == null) return "no ui";
            string[] t = (args ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int Arg(int i) => t.Length > i && int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : -1;
            if (t.Length > 0) {
                switch (t[0]) {
                    case "open":
                        Toggle(loginSession ?? a.session);
                        break;
                    case "tab":
                        if (a.window != null) a.SelectTab(Arg(1), Arg(2));
                        break;
                    case "mode":
                        a.mode = Arg(1) == 1 ? 1 : 0;
                        if (a.selMajor != 0) a.Rebuild();
                        a.window?.Invalidate();
                        break;
                    case "row":
                        if (a.window != null) a.Press(new Vector2Int(ListX + 100, TabY + RowH * Arg(1) + 40));
                        break;
                    case "icon":
                        if (a.window != null) {
                            var p = new Vector2Int(ListX + 15, TabY + RowH * Arg(1) + 15);
                            a.Press(p);
                            a.Release(p);
                        }

                        break;
                    case "receive":
                        if (a.window != null) {
                            var rp = new Vector2Int(ViewX + 100, TabY + 380);
                            a.Press(rp);
                            a.Release(rp);
                        }

                        break;
                    case "jump":
                        if (a.window != null) a.JumpTo(Arg(1));
                        break;
                    case "inject":
                        if (t.Length > 1) {
                            string hex = t[1];
                            var bytes = new byte[hex.Length / 2];
                            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(2 * i, 2), 16);
                            Handle(bytes);
                        }

                        break;
                }
            }

            Load();
            return (a.window != null && a.window.Visible ? "open" : "closed") + " tab " + a.selMajor + "/" + a.selMinor + " mode " + a.mode + " list " + a.vec.Count + "/" + a.mapCount +
                   " top " + a.top + " sel " + a.selected + " view " + a.viewId + " infos " + infos.Count + " states " + states.Count + " score " + totalScore + " rank " + rank +
                   " rewarded " + (a.viewId > 0 && StateOf(a.viewId).Rewarded) + " traced " + traced.Count + " badge " + newBadge + " banner " + (a.bannerText ?? "-") + " details " + a.detailTop + "/" + a.detailLines + " tiles [" + string.Join(" | ", a.tiles.ConvertAll(t => t.Tip)) + "]";
        }
    }
}
