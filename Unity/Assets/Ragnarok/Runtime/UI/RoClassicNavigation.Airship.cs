using System;
using System.Collections.Generic;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The private airship (UIPrivateAirplaneWnd, navi-share-worldmap-spec §3), from the navigation's Move button 0x1ef: a
    /// modal 208×110 box centred on the screen, bg_pay.bmp, "Payment" (MsgStr 0xd02), a 192×20 "Use %s" button per needed
    /// item (6909 Silvervine Cat Fruit, 25464 World Tour Ticket — PrivateAirplane_True.lub's NeedItem, shown whether owned or
    /// not) at (8, 26·(i+1)); a red #B51235 message beside ico_notic at (7, 86). The checks: an NPC target only warns
    /// (0xd3b); the same map (0xd19) and a dead character (0xd1a) disable the buttons (the start / end map lists are the
    /// server's: it answers 3 / 4). A button sends CZ_PRIVATE_AIRSHIP_REQUEST 0x0a49 {map[16] "dest.gat", u32 item} (6909
    /// after a Yes); ZC_PRIVATE_AIRSHIP_RESPONSE 0x0a4a: 0 closes, 1..5 show 0xb09 / 0xd05 / 0xd04 / 0xd03 / 0xd18 (+ red
    /// chat 0x783).
    /// </summary>
    public sealed partial class RoClassicNavigation {
        private const int AirshipW = 208, AirshipH = 110, AirshipItemId = 0x1f0;
        private static readonly int[] AirshipItems = { 6909, 25464 };
        private static readonly Color32 AirshipRed = new Color32(0xb5, 0x12, 0x35, 255);

        private RoClassicWindow airship;
        private string airshipMessage = string.Empty;
        private bool airshipDisabled;
        private string airshipTarget;

        /// <summary>0x1ef: the window with the checks (the last failing message wins).</summary>
        private void OpenAirship() {
            RoNaviObject sel = Selected;
            airshipTarget = sel?.Map;
            airshipMessage = string.Empty;
            airshipDisabled = false;
            if (sel != null && sel.Kind == RoNaviKind.Npc) airshipMessage = RoClassicUiHost.MsgStr(0xd3b, "NPC will move to random coondinate of the map.");
            if (string.IsNullOrEmpty(airshipTarget)) Fail(RoClassicUiHost.MsgStr(0xd04, "You can't teleport in this map"));
            else if (string.Equals(CurrentMapName(), airshipTarget, StringComparison.OrdinalIgnoreCase)) Fail(RoClassicUiHost.MsgStr(0xd19, "Can not move to the same map."));
            RoOnlineWorldController w = World();
            if (w != null && w.State.TryGetActor(w.OwnActorId, out RoOnlineActorSnapshot self) && self.Motion == RoOnlineActorMotion.Dead) {
                Fail(RoClassicUiHost.MsgStr(0xd1a, "It is impossible to move when your character KO'ed"));
            }

            if (airship == null) {
                var layout = new RoClassicWindowLayout { Class = "UIPrivateAirplaneWnd", WindowId = 0x7fff, Width = AirshipW, Height = AirshipH };
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = AirshipW - 14, Y = 3, W = 11, H = 11 };
                close.States["normal"] = Basic + "sys_close_off.bmp";
                close.States["hover"] = Basic + "sys_close_on.bmp";
                close.States["pressed"] = Basic + "sys_close_on.bmp";
                layout.Children.Add(close);
                airship = new RoClassicWindow(system, layout) { Visible = false };
                airship.DrawDynamic += DrawAirship;
                airship.Clicked += child => {
                    if (child.Id == CloseId) airship.Visible = false;
                };
                airship.Pressed += (w2, local) => {
                    int k = AirshipButtonAt(local);
                    if (k >= 0 && !airshipDisabled) RequestAirship(AirshipItems[k]);
                };
                airship.EscClose = () => {
                    airship.Visible = false;
                    return true;
                };
            }

            Vector2Int client = system.ClientSize;
            airship.MoveTo((client.x - AirshipW) / 2, (client.y - AirshipH) / 2);
            airship.Visible = true;
            system.Windows.BringToFront(airship);
            airship.Invalidate();

            void Fail(string text) {
                airshipMessage = text;
                airshipDisabled = true;
            }
        }

        private static int AirshipButtonAt(Vector2Int p) {
            for (int i = 0; i < AirshipItems.Length; i++) {
                if (ItemName(AirshipItems[i]) == null) continue;
                if (p.x >= 8 && p.x < 200 && p.y >= 26 * (i + 1) && p.y < 26 * (i + 1) + 20) return i;
            }

            return -1;
        }

        /// <summary>0x65d4a0: the item's name, or null when the client does not know the item (its button is skipped, its slot kept).</summary>
        private static string ItemName(int id) {
            RoItemMetadataTable items = RoClassicUiHost.GameAssets()?.GameData?.Items;
            return items != null && items.TryGetItem(id, out RoItemMetadata item) && !string.IsNullOrEmpty(item.IdentifiedDisplayName) ? item.IdentifiedDisplayName : null;
        }

        /// <summary>OnDraw 0x58f7a0: the background, the title, the item buttons, the message beside the notice icon.</summary>
        private void DrawAirship(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Ui + "private_airplane\\bg_pay.bmp");
            string title = RoClassicUiHost.MsgStr(0xd02, "Payment");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            RoClassicGdiFont font = w.FontFor(0, 12);
            for (int i = 0; i < AirshipItems.Length; i++) {
                int y = 26 * (i + 1);
                string name = ItemName(AirshipItems[i]);
                if (name == null) continue;
                string text = RoClassicUiHost.MsgStr(0xd01, "Use %s").Replace("%s", name);
                w.FillRect(8, y, 192, 20, airshipDisabled ? new Color32(0xe0, 0xe0, 0xe0, 255) : White);
                w.RoundOutline(8, y, 192, 20, Line);
                w.TextOut(8 + (192 - font.Measure(text)) / 2, y + 4, text, 0, 12, airshipDisabled ? new Color32(0x80, 0x80, 0x80, 255) : Black);
            }

            if (airshipMessage.Length == 0) return;
            w.DrawBitmap(7, 86, Ui + "private_airplane\\ico_notic.bmp");
            // 0x9ae260: wrapped to 164 px
            var lines = new List<string>();
            string rest = airshipMessage;
            while (rest.Length > 0) {
                int cut = rest.Length;
                while (cut > 1 && font.Measure(rest.Substring(0, cut)) > 164) cut--;
                if (cut < rest.Length) {
                    int space = rest.LastIndexOf(' ', cut - 1, cut);
                    if (space > 0) cut = space + 1;
                }

                lines.Add(rest.Substring(0, cut).TrimEnd());
                rest = rest.Substring(cut);
            }

            int n = lines.Count;
            for (int k = 0; k < n; k++) w.TextOut(26, (32 - 12 * n) / 2 + 78 + 12 * k, lines[k], 0, 12, AirshipRed);
        }

        /// <summary>0xabf1b0: 6909 asks first (UIPayConfirmWnd, its MsgStr 0xfc1 empty in Custom), then 0x0a49.</summary>
        private void RequestAirship(int itemId) {
            if (session == null || string.IsNullOrEmpty(airshipTarget)) return;
            void Send() {
                var body = new byte[20];
                byte[] map = RoLoginShellSession.TextBytes(airshipTarget + ".gat");
                Array.Copy(map, body, Math.Min(15, map.Length));
                BitConverter.GetBytes((uint) itemId).CopyTo(body, 16);
                _ = session.BeginEntryQueuePacket(0x0a49, body);
            }

            if (itemId != 6909) {
                Send();
                return;
            }

            RoClassicMessageBox.Show(ItemName(itemId) ?? string.Empty, RoClassicMessageBox.TypeOkCancel, true, result => {
                if (result == RoClassicMessageBox.IdYes) Send();
            });
        }

        /// <summary>ZC_PRIVATE_AIRSHIP_RESPONSE 0x0a4a {u32 flag} while the window exists (0xc8a580).</summary>
        internal static void HandleAirshipResponse(byte[] packet) {
            RoClassicNavigation n = instance;
            if (n?.airship == null || !n.airship.Visible || packet == null || packet.Length < 6) return;
            uint flag = BitConverter.ToUInt32(packet, 2);
            switch (flag) {
                case 0:
                    n.airship.Visible = false;
                    return;
                case 1: n.airshipMessage = RoClassicUiHost.MsgStr(0xb09, "Please try again in a moment."); break;
                case 2: n.airshipMessage = RoClassicUiHost.MsgStr(0xd05, "Insufficient item."); break;
                case 3: n.airshipMessage = RoClassicUiHost.MsgStr(0xd04, "You can't teleport in this map"); break;
                case 4: n.airshipMessage = RoClassicUiHost.MsgStr(0xd03, "This map isn't available for restart."); break;
                case 5:
                    n.airshipMessage = RoClassicUiHost.MsgStr(0xd18, "You can not use items.");
                    Chat(RoClassicUiHost.MsgStr(0x783, "Any work in progress (NPC dialog, manufacturing ...) quit and try again.").Replace("\n", " "));
                    break;
                default: return;
            }

            n.airship.Invalidate();
        }
    }
}
