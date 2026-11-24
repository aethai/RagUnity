using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UIClanInfoManageWnd 0xed (session-windows-spec §4), 400 × 317, Ctrl+G (with or without a clan): the clan record of
    /// ZC_CLANINFO 0x098a (id, name, master, map, allies, antagonists), ZC_NOTIFY_CLAN_CONNECTINFO 0x0988 (online / total)
    /// and ZC_ACK_CLAN_LEAVE 0x0989 (cleared, the window stays). One "Clan Info" tab; the emblem
    /// clan_system\clan_emblem%02d and the illustration clan_illust%02d. Esc closes; the place lives for the session.
    /// </summary>
    public sealed class RoClassicClan {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const int W = 400, H = 317, CloseId = 0xc5;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Strip = new Color32(0xb4, 0xb4, 0xb4, 255);
        private static readonly Color32 Gray = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static readonly Color32 Blue = new Color32(0x70, 0x9f, 0xed, 255);
        private static RoClassicClan instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private Vector2Int place = new Vector2Int(0, 34);   // mgr + 0x9f4, the session's rect
        private uint clanId;
        private bool inClan;
        private string name = string.Empty, master = string.Empty, map = string.Empty;
        private int online, total;
        private readonly List<string> allies = new List<string>(), hostiles = new List<string>();

        private static RoClassicClan Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicClan { system = system };
            return instance;
        }

        /// <summary>byte [DAT_014ef984 + 0x5c]: in a clan.</summary>
        public static bool InClan => instance != null && instance.inClan;

        public static bool Handles(ushort id) => id == 0x098a || id == 0x0988 || id == 0x0989;

        public static void Handle(byte[] b) {
            RoClassicClan c = Ensure();
            if (c == null || b == null || b.Length < 2) return;
            switch (BitConverter.ToUInt16(b, 0)) {
                case 0x098a when b.Length >= 0x4a: {
                    // 0xc85290: {len, ClanID @4, name[24] @8, master[24] @0x20, map[16] @0x38, allies @0x48, antagonists @0x49, names}
                    c.clanId = BitConverter.ToUInt32(b, 4);
                    c.name = Text(b, 8, 24);
                    c.master = Text(b, 0x20, 24);
                    c.map = Text(b, 0x38, 16);
                    int allyCount = b[0x48], hostileCount = b[0x49];
                    c.allies.Clear();
                    c.hostiles.Clear();
                    for (int i = 0; i < allyCount + hostileCount && 0x4a + 24 * (i + 1) <= b.Length; i++) {
                        (i < allyCount ? c.allies : c.hostiles).Add(Text(b, 0x4a + 24 * i, 24));
                    }

                    c.inClan = true;
                    break;
                }
                case 0x0988 when b.Length >= 6:
                    c.online = BitConverter.ToInt16(b, 2);
                    c.total = BitConverter.ToInt16(b, 4);
                    break;
                case 0x0989:
                    // 0xc85100 → 0xa0c940: the record cleared
                    c.clanId = 0;
                    c.inClan = false;
                    c.name = c.master = c.map = string.Empty;
                    c.online = c.total = 0;
                    c.allies.Clear();
                    c.hostiles.Clear();
                    break;
            }

            c.window?.Invalidate();
        }

        /// <summary>Ctrl+G (hotkey action 44): DeleteWindow(0xed) else MakeWindow(0xed).</summary>
        public static bool Toggle() {
            RoClassicClan c = Ensure();
            if (c == null) return false;
            if (c.window != null && c.window.Visible) {
                c.Close();
                return true;
            }

            c.Open();
            return true;
        }

        private void Open() {
            if (window == null) {
                var layout = new RoClassicWindowLayout { Class = "UIClanInfoManageWnd", WindowId = 0xed, Width = W, Height = H };
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = 386, Y = 3, W = 11, H = 11, Tooltip = "Ctrl+G" };
                close.States["normal"] = Basic + "sys_close_off.bmp";
                close.States["hover"] = close.States["pressed"] = Basic + "sys_close_on.bmp";
                layout.Children.Add(close);
                window = new RoClassicWindow(system, layout) { Visible = false };
                window.DrawDynamic += Draw;
                window.Clicked += child => {
                    if (child.Id == CloseId) Close();
                };
                window.EscClose = () => {
                    Close();
                    return true;
                };
            }

            // msg 0x22: the saved rect when on screen, else (0, 34)
            Vector2Int client = system.ClientSize;
            if (place.x < 0 || place.y < 0 || place.x + W > client.x || place.y + H > client.y) place = new Vector2Int(0, 34);
            window.MoveTo(place.x, place.y);
            window.Visible = true;
            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
            window.Invalidate();
        }

        private void Close() {
            if (window == null || !window.Visible) return;
            place = window.Position;   // vf11 0x7b04b0
            window.Visible = false;
        }

        /// <summary>UIClanWnd::OnDraw 0x7b01a0 then UIClanInfoManageWnd::OnDraw 0x7af980.</summary>
        private void Draw(RoClassicWindow w) {
            w.FillRect(0, 17, W, 24, Strip);
            w.FillRect(1, 18, 64, 23, White);
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.FillRect(0, 41, W, H - 41, Gray);
            w.FillRect(1, 41, W - 2, H - 41, White);
            // the button bar (0x82d150(2)): btnbar_left2 / right2 21 × 29, mid2 tiled
            w.DrawBitmapRaw(0, H - 29, Basic + "btnbar_left2.bmp");
            for (int x = 21; x < W - 21; x += 21) w.DrawBitmapRaw(x, H - 29, Basic + "btnbar_mid2.bmp");
            w.DrawBitmapRaw(W - 21, H - 29, Basic + "btnbar_right2.bmp");
            w.TextOut(3 + (62 - w.FontFor(0, 12).Measure(Msg(0x933, "Clan Info"))) / 2, 22, Msg(0x933, "Clan Info"), 0, 12, Black);

            w.TextOut(8, 49, Msg(0x935, "Clan Name") + " : " + name, 0, 12, Black);
            w.TextOut(8, 65, Msg(0x934, "Clan Level") + " : " + (inClan ? "1" : "0"), 0, 12, Black);
            w.TextOut(8, 81, Msg(0x93a, "Clan Leader") + " : " + master, 0, 12, Black);
            w.TextOut(8, 97, Msg(0x93b, "Number of Members") + " : ", 0, 12, Black);
            w.DrawBitmap(128, 95, Basic + "grp_online.bmp");
            w.TextOut(138, 97, "   " + online.ToString(CultureInfo.InvariantCulture), 0, 12, Black);
            string castle = map.Length == 0 ? string.Empty : RoClassicUiHost.MapDisplayName(map.EndsWith(".gat", StringComparison.OrdinalIgnoreCase) ? map.Substring(0, map.Length - 4) + ".rsw" : map);
            w.TextOut(8, 113, Msg(0x93c, "Castles Owned") + " : " + castle, 0, 12, Black);
            string id = ((int) clanId).ToString("00", CultureInfo.InvariantCulture);
            if (clanId == 0) w.DrawBitmap(50, 145, Ui + "illust\\펫_포포링.bmp");
            else w.DrawBitmap(8, 145, Ui + "clan_system\\clan_illust" + id + ".bmp");
            w.TextOut(200, 65, Msg(0x936, "Clan Emblem"), 0, 12, Black);
            w.FillRect(279, 64, 26, 26, Blue);
            if (clanId != 0) w.DrawBitmap(280, 65, Ui + "clan_system\\clan_emblem" + id + ".bmp");
            w.TextOut(200, 139, Msg(0x937, "Ally Clan"), 0, 12, Black);
            Relations(w, 155, allies);
            w.TextOut(200, 213, Msg(0x938, "Hostile Clan"), 0, 12, Black);
            Relations(w, 229, hostiles);
        }

        private static void Relations(RoClassicWindow w, int y, List<string> names) {
            w.FillRect(200, y, 168, 48, Gray);
            for (int k = 0; k < names.Count && k < 3; k++) w.TextOut(204, y + 2 + 16 * k, names[k], 0, 12, Black);
        }

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        private static string Text(byte[] b, int at, int length) {
            int end = at;
            while (end < at + length && end < b.Length && b[end] != 0) end++;
            Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
            return Encoding.GetEncoding(1252).GetString(b, at, end - at);
        }

        internal static string Sweep(string args) {
            if ((args ?? string.Empty).Trim() == "toggle") Toggle();
            RoClassicClan c = instance;
            return c == null ? "none" : "clan=" + c.clanId + " '" + c.name + "' master='" + c.master + "' map=" + c.map + " online=" + c.online + "/" + c.total +
                " allies=" + c.allies.Count + " hostiles=" + c.hostiles.Count + " open=" + (c.window != null && c.window.Visible);
        }
    }
}
