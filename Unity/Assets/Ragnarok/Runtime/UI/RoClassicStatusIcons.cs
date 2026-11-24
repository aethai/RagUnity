using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// The local player's status icons (hud-efst-banner-spec §1): the EF 0xa4 container's prims, rebuilt by
    /// CRagEffect 0xb61db0 and drawn under every window.
    /// <list type="bullet">
    /// <item>Order: priority GOLD, RED, BLUE, GREEN, WHITE (StateIconImgList, else 0xd16760's hard-coded table), then the
    /// list order, a re-sent EFST moving to the end (0xc767a0). EFST 0x2a shows as 0xc, an id is laid out once, 0x40 is
    /// val1 + 0x381.</item>
    /// <item>Layout: 32×32 at the centre (W − 32 − 45·col, 150 + 35·(k + 1)); a column takes icons while
    /// rows·35 + 150 &lt; H − 50, the next one 45 px to the left; 99 ids recorded for hovering.</item>
    /// <item>The clock wipe (0xbcb1d0 / 0xbca1b0 / 0xbcb6e0): the elapsed part as a pie from 12 o'clock clockwise over
    /// the icon's square in 0xAAFFFFFF; with a minute left it restarts as a 60-s wipe in 0xAAFF6633. No digits.</item>
    /// <item>/stateinfo (0xc21250): hovering an icon shows a UITransBalloonText (height 11, outline #C0C0C0) with the
    /// GetStateIconDescript lines in their colours, the posTimeLimitStr line's %s the time left, left of the icon's
    /// column with its top at the icon's.</item>
    /// </list>
    /// </summary>
    public sealed partial class RoClassicStatusIcons : IRoClassicUiProducer {
        private const string StateInfoPrefs = "ro.ui.classic.cmd./stateinfo";   // RoClassicCommandList's local flag
        private const int MaxHoverIds = 99;
        private const int Infinite = 9999;
        private static readonly Color32 IconColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 WipeColor = new Color32(255, 255, 255, 0xaa);
        private static readonly Color32 LastMinuteColor = new Color32(0xff, 0x66, 0x33, 0xaa);
        private static readonly Color32 TipBox = new Color32(0, 0, 0, 0x88);
        private static readonly Color32 TipOutline = new Color32(0xc0, 0xc0, 0xc0, 255);
        private static readonly Color32 TipShadow = new Color32(0, 0, 0, 255);
        private const string StateInfoChat = "#FFFF64";   // COLORREF 0x64ffff

        private struct Record {
            public ushort Id;
            public long Seq;
            public RoStatusIconSnapshot Status;
        }

        private static RoClassicStatusIcons instance;
        private static RoLoginShellSession session;
        private static RoClassicChat chat;
        private static RoItemMetadataTable items;
        private static int boundFrame = -10;
        private static Vector2Int forcedHover = new Vector2Int(-1, -1);   // the sweep's mouse

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            session = null;
            chat = null;
            items = null;
            boundFrame = -10;
            forcedHover = new Vector2Int(-1, -1);
        }

        private static readonly Comparison<Record> BySeq = (a, b) => a.Seq.CompareTo(b.Seq);

        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private RoClassicUiLayer tipLayer;
        private RoStatusIconDatabase database;
        private RoAssetDatabase databaseAssets;
        private readonly List<RoStatusIconSnapshot> snapshots = new List<RoStatusIconSnapshot>(32);
        private readonly List<Record> records = new List<Record>(32);
        private readonly Dictionary<ushort, KeyValuePair<long, long>> order = new Dictionary<ushort, KeyValuePair<long, long>>();
        private readonly List<ushort> stale = new List<ushort>(8);
        private readonly Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly int[] hoverIds = new int[100];
        private int hoverCount;
        private long nextSeq;
        private long shownKey = long.MinValue;
        private int retryFrame;
        private bool stateInfo = true;
        private int stateInfoReadFrame = -1000;

        // the tooltip (0x1f8: the id shown last frame)
        private readonly List<string> tipLines = new List<string>(8);
        private readonly List<Color32> tipColors = new List<Color32>(8);
        private long tipKey = long.MinValue;

        /// <summary>Per frame from the chat window's classic ticks while in the game.</summary>
        public static void Tick(RoLoginShellSession loginSession, RoClassicChat chatWindow, RoItemMetadataTable metadata) {
            session = loginSession;
            chat = chatWindow;
            items = metadata;
            boundFrame = Time.frameCount;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || (instance != null && instance.system == system)) return;
            instance = new RoClassicStatusIcons { system = system };
            instance.layer = system.Renderer.CreateLayer("status icons");
            instance.layer.AboveOverlayUi = true;
            system.Renderer.SendToBack(instance.layer);
            instance.tipLayer = system.Renderer.CreateLayer("status icon tip");
            instance.tipLayer.AboveOverlayUi = true;
            instance.tipLayer.Visible = false;
            system.Register(instance);
        }

        /// <summary>"/stateinfo" (0xc0a76b): toggles the tooltip, MsgStr 0x6a6 / 0x6a7 in the chat. False for other lines.</summary>
        public static bool TryCommand(string line) {
            if (line == null || !string.Equals(line.Trim(), "/stateinfo", StringComparison.OrdinalIgnoreCase)) return false;
            bool on = PlayerPrefs.GetInt(StateInfoPrefs, 1) == 0;
            PlayerPrefs.SetInt(StateInfoPrefs, on ? 1 : 0);
            if (instance != null) instance.stateInfoReadFrame = -1000;
            string text = on
                ? RoClassicUiHost.MsgStr(0x6a6, "Status Information On: Status icon description is enabled.")
                : RoClassicUiHost.MsgStr(0x6a7, "Status Information Off: Status icon description is disabled.");
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, StateInfoChat);
            return true;
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            // F11 removes EF_STATUS_STATE, the strip, until it is launched again (RoClassicCloseTabs)
            if (session == null || Time.frameCount - boundFrame > 2 || RoClassicCloseTabs.StatusIconsHidden) {
                if (layer.Visible) layer.Visible = false;
                if (tipLayer.Visible) tipLayer.Visible = false;
                shownKey = long.MinValue;
                return;
            }

            if (!layer.Visible) layer.Visible = true;
            if (!EnsureDatabase()) return;
            long now = RoStatusIconTracker.DefaultMonotonicMilliseconds();
            CollectRecords();
            Vector2Int client = ui.ClientSize;
            long key = LayoutKey(client, now);
            if (key != shownKey || (retryFrame != 0 && Time.frameCount >= retryFrame)) {
                shownKey = key;
                retryFrame = 0;
                Build(client, now);
            }

            UpdateTip(ui, client, now);
        }

        private bool EnsureDatabase() {
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null || !assets.IsInitialized) return false;
            if (database == null || databaseAssets != assets) {
                database = new RoStatusIconDatabase(assets);
                databaseAssets = assets;
                database.Preload();
            }

            return true;
        }

        // EFST 373 (OVERHEAT) case 0xc77939: the limit (DAT_015555f8) and when it was set (DAT_015555fc)
        private int overheatLimit = -1;
        private float overheatAt = -100f;

        /// <summary>The list V in its order: a record whose start changed was re-added (0xc767a0 removes, then appends).</summary>
        private void CollectRecords() {
            session.StatusIconSnapshot(snapshots);
            records.Clear();
            bool overheat = false;
            for (int i = 0; i < snapshots.Count; i++) {
                RoStatusIconSnapshot s = snapshots[i];
                ushort id = s.StatusId == 0x40 ? (ushort) (s.Value1 + 0x381) : s.StatusId;
                if (id == 373) {
                    overheat = true;
                    if (s.Value1 != overheatLimit) {
                        overheatLimit = s.Value1;
                        overheatAt = Time.unscaledTime;
                    }
                }

                if (!order.TryGetValue(id, out KeyValuePair<long, long> o) || o.Key != s.StartedAtMilliseconds) {
                    o = new KeyValuePair<long, long>(s.StartedAtMilliseconds, ++nextSeq);
                    order[id] = o;
                }

                records.Add(new Record { Id = id, Seq = o.Value, Status = s });
            }

            if (!overheat && overheatLimit != -1) {
                overheatLimit = -1;
                overheatAt = -100f;
            }

            records.Sort(BySeq);
            if (order.Count > records.Count + 16) {
                stale.Clear();
                foreach (ushort id in order.Keys) {
                    bool live = false;
                    for (int i = 0; i < records.Count && !live; i++) live = records[i].Id == id;
                    if (!live) stale.Add(id);
                }

                for (int i = 0; i < stale.Count; i++) order.Remove(stale[i]);
            }
        }

        /// <summary>What is on the screen: the records, their order, the client size and each wipe's step.</summary>
        private long LayoutKey(Vector2Int client, long now) {
            long key = 17;
            unchecked {
                key = key * 31 + client.x * 4099L + client.y;
                for (int i = 0; i < records.Count; i++) {
                    Record r = records[i];
                    key = key * 31 + r.Id;
                    key = key * 31 + r.Seq;
                    if (Wipe(r.Status, now, out float fraction, out bool lastMinute)) key = key * 31 + (int) (fraction * 1440f) + (lastMinute ? 7919 : 0);
                }
            }

            return key;
        }

        /// <summary>0xbca1b0: the elapsed fraction of the wipe, restarted as a 60-s wipe once a minute is left.</summary>
        private static bool Wipe(in RoStatusIconSnapshot s, long now, out float fraction, out bool lastMinute) {
            fraction = 0f;
            lastMinute = false;
            if (!s.HasFiniteDuration || s.IsInfiniteDuration || s.TotalMilliseconds == 0u || s.TotalMilliseconds == Infinite) return false;
            uint remaining = s.ResolveRemainingMilliseconds(now);
            if (remaining == 0u) return false;
            if (remaining <= 60000u) {
                lastMinute = true;
                fraction = (60000f - remaining) / 60000f;
            } else {
                fraction = (s.TotalMilliseconds - (float) remaining) / s.TotalMilliseconds;
            }

            fraction = Mathf.Clamp01(fraction);
            return true;
        }

        /// <summary>0xb61db0's layout and the prims' render (icon, then its wipe).</summary>
        private void Build(Vector2Int client, long now) {
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            for (int i = 0; i < hoverIds.Length; i++) hoverIds[i] = -1;
            hoverCount = 0;
            int w = client.x, h = client.y;
            int rowsInCol = 0, yOff = 0, xOff = 0;
            RoClassicUiImages images = system.Images;
            for (int prio = 0; prio < 5; prio++) {
                for (int i = 0; i < records.Count; i++) {
                    Record r = records[i];
                    if (!ImageFor(r.Id, out int p, out string file) || p != prio) continue;
                    if (!images.TryGet(EffectPath(file), out RoClassicUiImage image)) {
                        if (images.Source == null) retryFrame = Time.frameCount + 30;   // the assets are not ready yet
                        continue;
                    }

                    int id = r.Id == 0x2a ? 0xc : r.Id;
                    bool seen = false;
                    for (int k = 0; k < hoverCount && !seen; k++) seen = hoverIds[k] == id;
                    if (seen) continue;
                    if (rowsInCol * 35 + 150 < h - 50) {
                        yOff += 35;
                    } else {
                        rowsInCol = 0;
                        yOff = 35;
                        xOff -= 45;
                    }

                    float cx = w - 32 + xOff, cy = yOff + 150;
                    q.Image(cx - 16f, cy - 16f, 32f, 32f, image, IconColor);
                    if (Wipe(r.Status, now, out float fraction, out bool lastMinute)) Pie(q, cx, cy, fraction, lastMinute ? LastMinuteColor : WipeColor);
                    if (hoverCount < MaxHoverIds) {
                        hoverIds[hoverCount++] = id;
                        rowsInCol++;
                    }
                }
            }

            layer.MarkDirty();
        }

        private string EffectPath(string file) {
            if (!paths.TryGetValue(file, out string path)) paths[file] = path = "effect\\" + file;
            return path;
        }

        /// <summary>
        /// The first priority with a file, the Lua (GetEFSTImgFileName) before the hard-coded switch at the same priority;
        /// neither → no icon.
        /// </summary>
        private bool ImageFor(ushort id, out int priority, out string file) {
            bool lua = database.TryResolveImage(id, out int luaPriority, out string luaFile);
            bool hard = TryFallbackImage(id, out int hardPriority, out string hardFile);
            if (lua && (!hard || luaPriority <= hardPriority)) {
                priority = luaPriority;
                file = luaFile;
                return true;
            }

            priority = hardPriority;
            file = hardFile;
            if (!hard) return false;
            if (id == 90 || id == 91 || id == 92 || id == 93 || id == 17 || id == 64) {
                file = FallbackForJob(id, session != null ? session.BasicInfoSnapshot().Status.JobId : 0) ?? file;
            }

            return true;
        }

        /// <summary>0xbcb6e0 / 0xbcbc30 mode 1: eight triangles fanned from the centre to the 32×32 square, clockwise from 12 o'clock.</summary>
        private void Pie(RoClassicUiQuads q, float cx, float cy, float fraction, Color32 color) {
            if (fraction <= 0f) return;
            float end = fraction * 360f;
            var c = new Vector2(cx, cy);
            RoClassicUiImage white = system.White;
            for (int k = 0; k < 8 && k * 45f < end; k++) {
                Vector2 a = Edge(c, k * 45f), b = Edge(c, Mathf.Min((k + 1) * 45f, end));
                q.QuadCorners(c, a, b, b, white.Uv, white.Page, color);
            }
        }

        private static Vector2 Edge(Vector2 c, float degrees) {
            float rad = degrees * Mathf.Deg2Rad;
            float dx = Mathf.Sin(rad), dy = -Mathf.Cos(rad);
            float scale = 16f / Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            return new Vector2(c.x + dx * scale, c.y + dy * scale);
        }

        // ---------------------------------------------------------------- the /stateinfo tooltip (0xc21250)

        private void UpdateTip(RoClassicUiSystem ui, Vector2Int client, long now) {
            if (Time.frameCount - stateInfoReadFrame > 60) {
                stateInfo = PlayerPrefs.GetInt(StateInfoPrefs, 1) != 0;
                stateInfoReadFrame = Time.frameCount;
            }

            Mouse mouse = Mouse.current;
            int id = -1, col = 0, row = 0;
            if (stateInfo && (mouse != null || forcedHover.x >= 0)) {
                Vector2Int at = forcedHover.x >= 0 ? forcedHover : ui.ScreenToClient(mouse.position.ReadValue());
                id = HoverId(at, client, out col, out row);
            }
            if (id < 0 || !BuildTipText(id, now)) {
                tipKey = long.MinValue;
                if (tipLayer.Visible) tipLayer.Visible = false;
                return;
            }

            long key = 17;
            unchecked {
                key = key * 31 + id;
                key = key * 31 + col * 1000 + row;
                key = key * 31 + client.x * 4099L + client.y;
                for (int i = 0; i < tipLines.Count; i++) key = key * 31 + tipLines[i].GetHashCode();
            }

            if (!tipLayer.Visible) {
                tipLayer.Visible = true;
                system.Renderer.BringToFront(tipLayer);
            }

            if (key == tipKey) return;
            tipKey = key;
            RoClassicGdiFont font = ui.Font(11);
            int tw = 0;
            for (int i = 0; i < tipLines.Count; i++) tw = Math.Max(tw, font.Measure(tipLines[i]));
            tw += 12;
            int pitch = 4 + font.LineHeight;
            int th = 4 + tipLines.Count * pitch;
            int x = client.x - 18 - 30 - 45 * col - tw, y = 171 + 35 * row;
            if (y + th > client.y) y = client.y - th;
            RoClassicUiQuads q = tipLayer.Quads;
            q.Clear();
            q.Image(x, y, tw, th, ui.White, TipBox);
            foreach (RectInt r in RoClassicWindow.RoundOutlineRects(x, y, tw, th)) q.Image(r.x, r.y, r.width, r.height, ui.White, TipOutline);
            for (int i = 0; i < tipLines.Count; i++) {
                q.Text(font, x + 7, y + 4 + pitch * i + 1, tipLines[i], TipShadow);
                q.Text(font, x + 6, y + 4 + pitch * i, tipLines[i], tipColors[i]);
            }

            tipLayer.MarkDirty();
        }

        /// <summary>The hit test over N list records, R = (H − 169) / 35 rows a column, 15-px gaps rejected.</summary>
        private int HoverId(Vector2Int p, Vector2Int client, out int col, out int row) {
            col = row = 0;
            int n = records.Count, rows = (client.y - 169) / 35;
            if (n == 0 || rows <= 0) return -1;
            int cols = (n + rows - 1) / rows;
            int right = client.x - 18;
            if (p.x < right - cols * 45 + 15 || p.x > right || p.y < 168 || p.y > rows * 35 + 168) return -1;
            int d = right - p.x, e = p.y - 168;
            if (d % 45 > 30 || e % 35 > 30) return -1;
            col = d / 45;
            row = e / 35;
            int index = col * rows + row;
            return index < n && index < hoverIds.Length ? hoverIds[index] : -1;
        }

        /// <summary>GetTimeLimitInfo / GetStateIconDescript for the id; false when it has no line.</summary>
        private bool BuildTipText(int id, long now) {
            tipLines.Clear();
            tipColors.Clear();
            bool found = false;
            Record record = default;
            for (int i = 0; i < records.Count && !found; i++) {
                int rid = records[i].Id == 0x2a ? 0xc : records[i].Id;
                if (rid != id) continue;
                record = records[i];
                found = true;
            }

            if (!found) return false;
            if (id == 695) {
                // CLIENT_ONLY_EQUIP_ARROW: "%s : %d", the equipped ammunition and its amount
                RoItemInstance ammo = EquippedAmmo();
                if (ammo == null) return false;
                tipLines.Add(ammo.GetFullDisplayName(items ?? RoItemMetadataTable.Empty) + " : " + ammo.Count.ToString(CultureInfo.InvariantCulture));
                tipColors.Add(new Color32(190, 190, 250, 255));
                return true;
            }

            if (!database.TryResolveDefinition((ushort) id, out RoStatusIconDefinition definition)) return false;
            RoStatusIconDescriptionLine[] lines = definition.DescriptionLines;
            string time = definition.HaveTimeLimit ? TimeText(record.Status, now) : string.Empty;
            for (int i = 0; i < lines.Length; i++) {
                string text = lines[i].Text;
                if (string.IsNullOrEmpty(text)) break;
                if (i + 1 == definition.PosTimeLimitStr) text = text.Replace("%s", time);
                tipLines.Add(text);
                tipColors.Add(LineColor(lines[i].ColorKey));
            }

            // OVERHEAT: "Overheat limit : %d" (MsgStr 0xbd2) while the limit was set within the last 10 s
            if (id == 373 && overheatLimit > 0 && Time.unscaledTime - overheatAt < 10f) {
                tipLines.Add(RoClassicUiHost.MsgStr(0xbd2, "Overheat limit : %d").Replace("%d", overheatLimit.ToString(CultureInfo.InvariantCulture)));
                tipColors.Add(new Color32(255, 255, 255, 255));
            }

            return tipLines.Count > 0;
        }

        /// <summary>0xc13350: "%d %s" hours (MsgStr 0x6a1), " %d %s" minutes (0x70f), " %d %s" seconds (0x710), each when set; empty without a time limit.</summary>
        private static string TimeText(in RoStatusIconSnapshot s, long now) {
            if (!s.HasFiniteDuration || s.IsInfiniteDuration) return string.Empty;
            long seconds = s.ResolveRemainingMilliseconds(now) / 1000;
            long h = seconds / 3600, m = seconds % 3600 / 60, sec = seconds % 60;
            string text = string.Empty;
            if (h > 0) text += h.ToString(CultureInfo.InvariantCulture) + " " + RoClassicUiHost.MsgStr(0x6a1, "hour(s)");
            if (m > 0) text += " " + m.ToString(CultureInfo.InvariantCulture) + " " + RoClassicUiHost.MsgStr(0x70f, "minute(s)");
            if (sec > 0) text += " " + sec.ToString(CultureInfo.InvariantCulture) + " " + RoClassicUiHost.MsgStr(0x710, "second(s)");
            return text;
        }

        /// <summary>stateiconinfo.lub's colours; COLOR_SYSTEM is not defined there, so it is the default white.</summary>
        private static Color32 LineColor(string key) {
            switch (key) {
                case "COLOR_TITLE_BUFF": return new Color32(155, 202, 155, 255);
                case "COLOR_TITLE_DEBUFF": return new Color32(250, 100, 100, 255);
                case "COLOR_TITLE_TOGGLE": return new Color32(190, 190, 250, 255);
                case "COLOR_TITLE_SYSTEM": return new Color32(255, 255, 0, 255);
                case "COLOR_TIME": return new Color32(255, 176, 98, 255);
                default: return new Color32(255, 255, 255, 255);
            }
        }

        private static RoItemInstance EquippedAmmo() {
            RoItemInstance[] inventory = session?.InventorySnapshot()?.InventoryItems ?? Array.Empty<RoItemInstance>();
            for (int i = 0; i < inventory.Length; i++) {
                RoItemInstance item = inventory[i];
                if (item != null && item.Count > 0 && (item.WearState & RoItemEquipLocation.Ammo) != 0) return item;
            }

            return null;
        }

        // ---------------------------------------------------------------- sweep

        /// <summary>!efst state | hover x y (client pixels; -1 -1 = the real mouse) | tip.</summary>
        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length >= 3 && t[0] == "hover") {
                forcedHover = new Vector2Int(int.Parse(t[1], CultureInfo.InvariantCulture), int.Parse(t[2], CultureInfo.InvariantCulture));
                return "hover " + forcedHover;
            }

            if (t.Length >= 1 && t[0] == "tip") {
                RoClassicStatusIcons s = instance;
                return s == null || !s.tipLayer.Visible ? "no tip" : "tip: " + string.Join(" | ", s.tipLines);
            }

            return Describe();
        }

        /// <summary>The laid-out ids and the client centre of each, for the sweep.</summary>
        internal static string Describe() {
            RoClassicStatusIcons s = instance;
            if (s == null) return "no status icons";
            var sb = new System.Text.StringBuilder();
            sb.Append("records ").Append(s.records.Count).Append(", shown ").Append(s.hoverCount).Append(", stateinfo ").Append(s.stateInfo ? "on" : "off").Append(", list");
            for (int i = 0; i < s.records.Count; i++) {
                bool image = s.ImageFor(s.records[i].Id, out int prio, out string file);
                sb.Append(' ').Append(s.records[i].Id).Append(image ? "/" + prio + "/" + file : "/-");
            }

            sb.Append(':');
            Vector2Int client = s.system.ClientSize;
            int rows = 0, yOff = 0, xOff = 0;
            for (int i = 0; i < s.hoverCount; i++) {
                if (rows * 35 + 150 < client.y - 50) yOff += 35;
                else {
                    rows = 0;
                    yOff = 35;
                    xOff -= 45;
                }

                rows++;
                sb.Append(' ').Append(s.hoverIds[i]).Append('@').Append(client.x - 32 + xOff).Append(',').Append(yOff + 150);
            }

            return sb.ToString();
        }
    }
}
