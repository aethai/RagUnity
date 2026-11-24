using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The attendance check, UICheckAttendanceWnd 0x148 (bank-attendance-achieve-spec §2). The menu icon 0x218 closes an
    /// open window or sends CZ_OPEN_UI 0x0a68 {5}; ZC_UI_OPEN 0x0ae2 type 7 makes it (or updates it) with data = claimed
    /// days · 10 + claimed today; a press on today's cell sends CZ_REQ_ATTENDANCE_REWARD 0x0aef while claimable;
    /// ZC_UI_ACTION 0x0af0 type 0 marks the day claimed (data · 10 + 1), type 1 prints MsgStr 0xd90 in red and closes it.
    /// 488×413 centred: attendance_bg.bmp, the server's title and the event period (System\CheckAttendance.lub, the
    /// client's own reward list), "Day" and the days left, the status text wrapped to 112 px, 5 × 4 reward cells 62 × 64
    /// from (26, 95) — today's blinking bt_slot_a / bt_slot_b every 100 ms while claimable, the rewards' icons and
    /// "Day %d", claimed cells under bt_slot_off.tga (the last one bt_slot_complete.tga once today is claimed); the Close
    /// button at (170, 376). Hovering a cell names its reward, a right click describes it. Custom has no banners.
    /// </summary>
    public sealed class RoClassicAttendance : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Att = Ui + "check_attendance\\";
        private const int W = 488, H = 413;
        private const string Red = "#FF0000";
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 TitleColor = Ref(0x203f66), PeriodColor = Ref(0x3950a4), DayColor = Ref(0x595939);
        private static readonly Color32 StatusColor = Ref(0x613ca5), CellColor = Ref(0xa44f3b), CloseColor = Ref(0x313131);

        /// <summary>A COLORREF 0x00BBGGRR.</summary>
        private static Color32 Ref(int bgr) => new Color32((byte) bgr, (byte) (bgr >> 8), (byte) (bgr >> 16), 255);

        private static RoClassicAttendance instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoItemMetadataTable items;
        private RoClassicWindow window;

        // CCheckAttendanceMgr: System\CheckAttendance.lub
        private bool loaded;
        private int startDate, endDate;
        private readonly SortedDictionary<int, (int Id, int Count)> rewards = new SortedDictionary<int, (int, int)>();

        private int claimed;       // +0x9c
        private bool claimable;    // +0x98
        private int daysLeft;      // +0xa0
        private int todayState;    // +0xa4: 0 / 1 blink, 2 pressed
        private bool closePressed;

        private int Total => rewards.Count;   // +0x94
        // the reward items and the "Day %d" labels made once: today's slot blinks every 100 ms and every blink repaints the window
        private readonly Dictionary<int, RoItemInstance> rewardItems = new Dictionary<int, RoItemInstance>();
        private readonly Dictionary<int, string> dayLabels = new Dictionary<int, string>();
        // the texts of the window, made again only when what they say changes (not at every blink)
        private string textTitle, textPeriod, textDaysLeft;
        private List<string> textStatusLines;
        private (string Server, int Claimed, bool Claimable, int DaysLeft, RoClassicGdiFont Font) textKey;

        public static void Tick(RoLoginShellSession loginSession, RoClassicChat chatWindow, RoItemMetadataTable metadata) {
            RoClassicAttendance a = Ensure();
            if (a == null) return;
            a.session = loginSession;
            a.chat = chatWindow;
            a.items = metadata;
        }

        private static RoClassicAttendance Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicAttendance { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Menu icon 0x218 (0x7aef46): an open window closes, else CZ_OPEN_UI 0x0a68 {UIType 5}.</summary>
        public static void Toggle(RoLoginShellSession loginSession) {
            RoClassicAttendance a = Ensure();
            if (a?.window != null && a.window.Visible) {
                a.window.Visible = false;
                return;
            }

            _ = loginSession?.BeginEntryQueuePacket(0x0a68, new byte[] { 5 });
        }

        /// <summary>The raw 0x0ae2 / 0x0af0 frames (the entry queue's carrier).</summary>
        internal static void Handle(byte[] packet) {
            RoClassicAttendance a = Ensure();
            if (a == null || packet == null || packet.Length < 2) return;
            int id = BitConverter.ToUInt16(packet, 0);
            if (id == 0x0ae2 && packet.Length >= 7) {
                // 0xc87030: type 7 → MakeWindow(0x148), SendMsg 0x22 data
                if (packet[2] == 7) a.Update(BitConverter.ToInt32(packet, 3));
                else if (packet[2] == 5) RoClassicTipBox.ShowTip(BitConverter.ToInt32(packet, 3));   // type 5: the tip box
            } else if (id == 0x0af0 && packet.Length >= 10) {
                // 0xc88af0
                int type = BitConverter.ToInt32(packet, 2), data = BitConverter.ToInt32(packet, 6);
                if (type == 0) {
                    if (a.window != null && a.window.Visible) a.Update(data * 10 + 1);
                } else if (type == 1) {
                    a.Say(RoClassicUiHost.MsgStr(0xd90, "Attendance check failed. Please try again."));
                    if (a.window != null) a.window.Visible = false;
                }
            }
        }

        private void Say(string text) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, Red);
        }

        /// <summary>SendMsg 0x22 (0x73a2b0): the window made if needed, the count and whether today is claimable.</summary>
        private void Update(int data) {
            Load();
            bool created = window == null || !window.Visible;
            if (window == null) Create();
            claimed = data / 10;
            claimable = data % 10 == 0 && claimed < Total;
            if (created) {
                todayState = 0;
                daysLeft = DaysLeft();
                window.MoveTo((int) ((system.ClientSize.x - W) * 0.5), (int) ((system.ClientSize.y - H) * 0.5));
                window.Visible = true;
                system.Windows.BringToFront(window);
            }

            window.Invalidate();
        }

        /// <summary>OnCreate 0x739040: mktime(EndDate 15:00 local) − now, read back as a timestamp's day of the year (sic).</summary>
        private int DaysLeft() {
            if (endDate <= 0) return 0;
            DateTime end;
            try {
                end = new DateTime(endDate / 10000, endDate / 100 % 100, endDate % 100, 15, 0, 0, DateTimeKind.Local);
            } catch (ArgumentOutOfRangeException) {
                return 0;
            }

            long d = (long) (end - DateTime.Now).TotalSeconds;
            return d < 1 ? 0 : DateTimeOffset.FromUnixTimeSeconds(d).ToLocalTime().DayOfYear - 1;
        }

        /// <summary>CCheckAttendanceMgr 0x5fd350: Config's last two values and Reward {day, item, count}.</summary>
        private void Load() {
            if (loaded) return;
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null) return;
            loaded = true;
            string text = assets.ReadText("System/CheckAttendance.lub");
            if (string.IsNullOrEmpty(text) && assets.TryFindFirstPathEndingWith("CheckAttendance.lub", out string path)) text = assets.ReadText(path);
            if (string.IsNullOrEmpty(text)) return;
            Match start = Regex.Match(text, @"StartDate\s*=\s*(\d+)"), end = Regex.Match(text, @"EndDate\s*=\s*(\d+)");
            if (start.Success) startDate = int.Parse(start.Groups[1].Value, CultureInfo.InvariantCulture);
            if (end.Success) endDate = int.Parse(end.Groups[1].Value, CultureInfo.InvariantCulture);
            foreach (Match m in Regex.Matches(text, @"\{\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\}")) {
                rewards[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)] =
                    (int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
            }
        }

        private void Create() {
            var layout = new RoClassicWindowLayout { Class = "UICheckAttendanceWnd", WindowId = 0x148, Width = W, Height = H };
            window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += Draw;
            window.Pressed += (w, p) => {
                if (CloseAt(p)) {
                    closePressed = true;
                    w.Invalidate();
                } else if (claimable && CellAt(p) == claimed) {
                    todayState = 2;
                    w.Invalidate();
                }
            };
            window.Released += (w, p) => {
                if (closePressed) {
                    closePressed = false;
                    if (CloseAt(p)) w.Visible = false;
                    else w.Invalidate();
                    return;
                }

                bool wasPressed = todayState == 2;
                if (wasPressed) todayState = 0;
                if (CellAt(p) == claimed && claimable) _ = session?.BeginEntryQueuePacket(0x0aef, Array.Empty<byte>());
                if (wasPressed) w.Invalidate();
            };
            window.RightClicked += (w, p) => {
                int cell = CellAt(p);
                if (cell >= 0 && cell < Total && rewards.TryGetValue(cell + 1, out (int Id, int Count) r)) {
                    RoClassicItemDescription.Toggle(RoItemInstance.CreateRegular(-1, r.Id, 0, true), items ?? RoItemMetadataTable.Empty);
                }
            };
            window.EscClose = () => {
                window.Visible = false;
                return true;
            };
        }

        private static bool CloseAt(Vector2Int p) => p.x >= 170 && p.x < 170 + 146 && p.y >= 376 && p.y < 376 + 30;

        /// <summary>0x738f50: the cell under a window pixel, 15-px gaps excluded, else −1.</summary>
        private static int CellAt(Vector2Int p) {
            int lx = p.x - 26, ly = p.y - 95;
            if (lx < 0 || lx >= 307 || ly < 0 || p.y > 347) return -1;
            if (lx % 62 >= 59 || ly % 64 >= 61) return -1;
            return lx / 62 % 5 + (ly / 64 & 3) * 5;
        }

        /// <summary>OnProcess 0x739f60 (the blink) and the hover tooltip 0x739dd0.</summary>
        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (window == null || !window.Visible) return;
            if (claimable && todayState != 2) {
                int blink = (int) ((uint) Environment.TickCount / 100u & 1u);
                if (blink != todayState) {
                    todayState = blink;
                    window.Invalidate();
                }
            }

            Vector2Int mouse = ui.Windows.Mouse;
            Vector2Int local = mouse - window.Position;
            if (local.x < 0 || local.y < 0 || local.x >= W || local.y >= H) return;
            int cell = CellAt(local);
            if (cell < 0 || cell >= Total || !rewards.TryGetValue(cell + 1, out (int Id, int Count) r)) return;
            string name = RoItemInstance.CreateRegular(0, r.Id, 1).GetFullDisplayName(items ?? RoItemMetadataTable.Empty);
            RoClassicTooltip.Shared.Show(name, White, mouse.x, mouse.y - 20);
        }

        /// <summary>TextOutBox: centred in a box when it has a width, else at x.</summary>
        private static void TextBox(RoClassicWindow w, int x, int y, string text, int boxW, int height, Color32 color) {
            if (string.IsNullOrEmpty(text)) return;
            int tx = boxW > 0 ? x + (boxW - w.FontFor(0, height).Measure(text)) / 2 : x;
            w.TextOut(tx, y, text, 0, height, color, true);
        }

        /// <summary>OnDraw 0x7393d0, the Close button and DrawAfter 0x738dd0.</summary>
        private void Draw(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Att + "attendance_bg.bmp");
            RoClassicGdiFont statusFont = w.FontFor(0, 12);
            var key = (session?.SelectedServerName ?? string.Empty, claimed, claimable, daysLeft, statusFont);
            if (textStatusLines == null || !key.Equals(textKey)) {
                textKey = key;
                textTitle = RoClassicUiHost.MsgStr(0xd89, "Server %s Attendance Reward").Replace("%s", key.Item1);
                string period = string.Format(CultureInfo.InvariantCulture, "{0:00}/{1:00} ~ {2,2}/{3,2}", startDate / 100 % 100, startDate % 100, endDate / 100 % 100, endDate % 100);
                string periodText = RoClassicUiHost.MsgStr(0xd8a, "Event period: %02d/%02d ~ %2d/%2d 24:00");
                int cut = periodText.IndexOf("%02d", StringComparison.Ordinal);
                int last = periodText.LastIndexOf("%2d", StringComparison.Ordinal);
                if (cut >= 0 && last > cut) periodText = periodText.Substring(0, cut) + period + periodText.Substring(last + 3);
                textPeriod = periodText;
                textDaysLeft = daysLeft.ToString(CultureInfo.InvariantCulture);
                string status = claimable
                    ? RoClassicUiHost.MsgStr(0xd8c, "Click the item to claim day %d reward").Replace("%d", (claimed + 1).ToString(CultureInfo.InvariantCulture))
                    : RoClassicUiHost.MsgStr(0xd8d, "You have claimed day %d reward").Replace("%d", claimed.ToString(CultureInfo.InvariantCulture));
                textStatusLines = Wrap(statusFont, status, 112);
            }

            TextBox(w, 0, 41, textTitle, W, 13, TitleColor);
            TextBox(w, 0, 70, textPeriod, W, 12, PeriodColor);
            TextBox(w, 345, 289, RoClassicUiHost.MsgStr(0xd8e, "Day"), 109, 14, DayColor);
            if (daysLeft > 0) {
                TextBox(w, 368, 309, textDaysLeft, 54, 22, White);
                TextBox(w, 425, 318, RoClassicUiHost.MsgStr(0xd8f, string.Empty), 0, 12, DayColor);
            }

            List<string> lines = textStatusLines;
            int y0 = (int) ((77 - 15 * lines.Count) * 0.5 + 95);
            for (int k = 0; k < lines.Count; k++) TextBox(w, 343, y0 + 3 * (Math.Max(k - 1, 0) + 4 * k), lines[k], 112, 12, StatusColor);
            if (claimable) {
                string slot = todayState == 2 ? "bt_slot_press.bmp" : todayState == 1 ? "bt_slot_b.bmp" : "bt_slot_a.bmp";
                w.DrawBitmap(26 + 62 * (claimed % 5), 95 + 64 * (claimed / 5), Att + slot);
            }

            RoItemMetadataTable table = items ?? RoItemMetadataTable.Empty;
            for (int i = 1; i <= Total; i++) {
                int c = (i - 1) % 5, r = (i - 1) / 5;
                if (rewards.TryGetValue(i, out (int Id, int Count) reward) && reward.Id > 0) {
                    if (!rewardItems.TryGetValue(i, out RoItemInstance item) || item.ItemId != reward.Id) {
                        item = RoItemInstance.CreateRegular(0, reward.Id, reward.Count);
                        rewardItems[i] = item;
                    }

                    RoClassicItemText.DrawIcon(w, 43 + 62 * c, 109 + 64 * r, item, table, reward.Count);
                }

                if (!dayLabels.TryGetValue(i, out string day)) {
                    day = RoClassicUiHost.MsgStr(0xd8b, "Day %d").Replace("%d", i.ToString(CultureInfo.InvariantCulture));
                    dayLabels[i] = day;
                }

                TextBox(w, 26 + 62 * c, 140 + 64 * r, day, 58, 12, CellColor);
            }

            // the image-text Close button 0xc5: bt_ok_normal (states 0, 1) / bt_ok_press (2), "Close" h14 bold
            w.DrawBitmap(170, 376, Att + (closePressed ? "bt_ok_press.bmp" : "bt_ok_normal.bmp"));
            TextBox(w, 170, 376 + (30 - 17) / 2, RoClassicUiHost.MsgStr(0xbe5, "Close"), 146, 14, CloseColor);

            // DrawAfter: the claimed cells
            for (int j = 0; j < claimed && j < 20; j++) {
                bool complete = j == claimed - 1 && !claimable;
                w.DrawBitmap(26 + 62 * (j % 5), 95 + 64 * (j / 5), Att + (complete ? "bt_slot_complete.tga" : "bt_slot_off.tga"));
            }
        }

        /// <summary>0x9ae260: words wrapped to a pixel width.</summary>
        private static List<string> Wrap(RoClassicGdiFont font, string text, int width) {
            var lines = new List<string>();
            string rest = text ?? string.Empty;
            while (rest.Length > 0) {
                int cut = rest.Length;
                while (cut > 1 && font.Measure(rest.Substring(0, cut)) > width) cut--;
                if (cut < rest.Length) {
                    int space = rest.LastIndexOf(' ', cut - 1, cut);
                    if (space > 0) cut = space + 1;
                }

                lines.Add(rest.Substring(0, cut).TrimEnd());
                rest = rest.Substring(cut).TrimStart();
            }

            return lines;
        }

        /// <summary>!att open|inject data|claim|press x y|state for the sweep.</summary>
        internal static string Sweep(string args) {
            RoClassicAttendance a = Ensure();
            if (a == null) return "no ui";
            string[] t = (args ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length >= 1 && t[0] == "open") Toggle(a.session);
            else if (t.Length >= 2 && t[0] == "inject") a.Update(int.Parse(t[1], CultureInfo.InvariantCulture));
            else if (t.Length >= 3 && t[0] == "press" && a.window != null) {
                var p = new Vector2Int(int.Parse(t[1], CultureInfo.InvariantCulture), int.Parse(t[2], CultureInfo.InvariantCulture));
                int cell = CellAt(p);
                if (CloseAt(p)) a.window.Visible = false;
                else if (cell == a.claimed && a.claimable) _ = a.session?.BeginEntryQueuePacket(0x0aef, Array.Empty<byte>());
            }

            return (a.window != null && a.window.Visible ? "open" : "closed") + " claimed " + a.claimed + " claimable " + a.claimable + " total " + a.Total +
                   " period " + a.startDate + "-" + a.endDate + " left " + a.daysLeft + " server '" + (a.session?.SelectedServerName ?? string.Empty) + "'";
        }
    }
}
