using System;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// UISelectServerWnd 0x2 for the character servers (codex-ui-research RUNTIME-RULES-PRE-GAME.md "Server choice 0x2";
    /// OnCreate 0x815940, OnDraw 0x8193e0, messages 0x820450): 280 × 200 at ((W − 640) / 2 + 185, H · 300 / 480 − 80), the
    /// style-1 title bar with MsgStr 0x822 "Select Service:", the body (0x82cc10) and the style-2 button bar (0x82d150), the
    /// UIServerListBox 0xb4 at (12, 22, 256, 145) (UIListBox: #F0F0F0, 16 px rows, the selection #CADFFB), OK 0xb4 at
    /// (189, H − 24) and Cancel 0xb5 at (234, H − 24) (Exit 0xd9 starts outside the frame). The selection starts at the
    /// index saved by the last OK; OK with a row closes the window, plays the button sound, saves the index and connects
    /// (CLoginMode 0x2713); Cancel goes back to the login (0x2716). Enter is OK (the default button), the arrows move the
    /// selection.
    /// </summary>
    public sealed partial class RoClassicPreGame {
        private const int ServerW = 280, ServerH = 200, ServerOkId = 0xb4, ServerCancelId = 0xb5, ServerRowH = 16;
        private const string ServerIndexKey = "ro.ui.classic.serverIndex";
        private static readonly RectInt ServerList = new RectInt(12, 22, 256, 145);
        private static readonly Color32 ServerListBg = new Color32(0xf0, 0xf0, 0xf0, 255);
        private static readonly Color32 ServerSelectedBg = new Color32(0xca, 0xdf, 0xfb, 255);

        private RoClassicWindow server;
        private RoLoginCharServerInfo[] servers = Array.Empty<RoLoginCharServerInfo>();
        private int serverSelected = -1, serverTop;
        private bool serverSent;

        public static bool ServerVisible => instance?.server != null && instance.server.Visible;

        private void SetServer(bool visible) {
            if (!visible) {
                if (server != null) server.Visible = false;
                serverSent = false;
                return;
            }

            if (server == null) {
                var layout = new RoClassicWindowLayout { Class = "UISelectServerWnd", WindowId = 0x2, Width = ServerW, Height = ServerH };
                RoClassicFrames.TitleBar(layout.Ops, ServerW, true);
                RoClassicFrames.Body(layout.Ops, ServerW, ServerH);
                RoClassicFrames.ButtonBar(layout.Ops, ServerW, ServerH, 2);
                layout.Children.Add(ServerButton(ServerOkId, "ok", "btn_ok", 189));
                layout.Children.Add(ServerButton(ServerCancelId, "cancel", "btn_cancel", 234));
                server = new RoClassicWindow(system, layout) { Visible = false, Draggable = false };
                server.DrawDynamic += DrawServer;
                server.Clicked += child => {
                    if (child.Id == ServerOkId) ServerOk();
                    else if (child.Id == ServerCancelId) ServerCancel();
                };
                server.Pressed += (w, local) => ServerPick(local);
                server.DoubleClicked += (w, local) => {
                    if (ServerPick(local)) ServerOk();
                };
                server.Wheeled += (w, local, delta) => ServerScroll(delta > 0 ? -1 : 1);
            }

            Vector2Int client = system.ClientSize;
            server.MoveTo((client.x - 640) / 2 + 185, client.y * 300 / 480 - 80);
            if (server.Visible) return;
            // a new list: the saved choice, else the first row
            servers = shell.ServerListForPhase11Ui() ?? Array.Empty<RoLoginCharServerInfo>();
            serverSelected = servers.Length == 0 ? -1 : Mathf.Clamp(PlayerPrefs.GetInt(ServerIndexKey, 0), 0, servers.Length - 1);
            serverTop = 0;
            serverSent = false;
            ServerKeepVisible();
            server.Visible = true;
            system.Windows.BringToFront(server);
            server.Invalidate();
        }

        private static RoClassicChildLayout ServerButton(int id, string path, string art, int x) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = ServerH - 24, W = 42, H = 20 };
            button.States["normal"] = "유저인터페이스\\" + art + ".bmp";
            button.States["hover"] = "유저인터페이스\\" + art + "_a.bmp";
            button.States["pressed"] = "유저인터페이스\\" + art + "_b.bmp";
            return button;
        }

        private int ServerRows => ServerList.height / ServerRowH;

        /// <summary>OnDraw 0x8193e0 (the title text over the bar) and the list (UIListBox 0x7ec990).</summary>
        private void DrawServer(RoClassicWindow w) {
            string title = RoClassicUiHost.MsgStr(0x822, "Select Service:");
            w.TextOut(19, 4, title, 0, 12, new Color32(255, 255, 255, 255));
            w.TextOut(18, 3, title, 0, 12, new Color32(0, 0, 0, 255));
            w.FillRect(ServerList.x, ServerList.y, ServerList.width, ServerList.height, ServerListBg);
            w.ClipTo(ServerList.x, ServerList.y, ServerList.width, ServerList.height);
            int rows = ServerRows;
            for (int i = serverTop; i < servers.Length && i < serverTop + rows; i++) {
                int y = ServerList.y + ServerRowH * (i - serverTop);
                if (i == serverSelected) w.FillRect(ServerList.x, y, ServerList.width, ServerRowH - 1, ServerSelectedBg);
                w.TextOutColored(ServerList.x + 3, y, ServerRowText(servers[i]), 0, 12);
            }

            if (servers.Length > rows) w.DrawScrollBar(ServerList.x + ServerList.width - 13, ServerList.y, ServerList.height, servers.Length - rows, servers.Length, rows, serverTop);
            w.ResetClip();
        }

        // CLoginMode state 6 (0xcb38d6): the load colour codes 0x1001ac8..0x1001ae0 and Smooth / Normal / Busy / Crowded
        private static readonly string[] ServerLoadColors = { "^00B509", "^FF7800", "^FF0000", "^BA00FF" };
        private static readonly (int Id, string Fallback)[] ServerLoadTexts = { (0xcbd, "Smooth"), (0xcbe, "Normal"), (0xcbf, "Busy"), (0xcc0, "Crowded") };

        /// <summary>
        /// A character server's row (CLoginMode state 6 from the AC_ACCEPT_LOGIN entry): MsgStr 0x1e2 "[New Server]" before the
        /// name when the property is 1; users 0..3 (the server's load code) make "%s%s  ^000000%s" (the load colour, its
        /// text, the name in black); the type 2 / 3 / 4 appends MsgStr 0xcba / 0xcbb / 0xcbc (" - Age 18+", " - Paid server",
        /// " - Free server"); a non-empty result replaces the name (strcpy_s), drawn with its colour codes.
        /// </summary>
        private static string ServerRowText(RoLoginCharServerInfo info) {
            string name = (info.Property == 1 ? RoClassicUiHost.MsgStr(0x1e2, "[New Server]") : string.Empty) + (info.Name ?? string.Empty);
            string row = string.Empty;
            if (info.UserCount < ServerLoadColors.Length) {
                (int id, string fallback) = ServerLoadTexts[info.UserCount];
                row = ServerLoadColors[info.UserCount] + RoClassicUiHost.MsgStr(id, fallback) + "  ^000000" + name;
            }

            switch (info.State) {
                case 2:
                    row += RoClassicUiHost.MsgStr(0xcba, " - Age 18+");
                    break;
                case 3:
                    row += RoClassicUiHost.MsgStr(0xcbb, " - Paid server");
                    break;
                case 4:
                    row += RoClassicUiHost.MsgStr(0xcbc, " - Free server");
                    break;
            }

            return row.Length > 0 ? row : name;
        }

        private bool ServerPick(Vector2Int local) {
            if (!ServerList.Contains(local)) return false;
            int i = (local.y - ServerList.y) / ServerRowH + serverTop;
            if (i >= servers.Length) return false;
            serverSelected = i;
            server.Invalidate();
            return true;
        }

        private void ServerScroll(int rows) {
            int next = Mathf.Clamp(serverTop + rows, 0, Math.Max(0, servers.Length - ServerRows));
            if (next == serverTop) return;
            serverTop = next;
            server.Invalidate();
        }

        private void ServerMove(int delta) {
            if (servers.Length == 0) return;
            serverSelected = Mathf.Clamp(serverSelected < 0 ? (delta > 0 ? 0 : servers.Length - 1) : serverSelected + delta, 0, servers.Length - 1);
            ServerKeepVisible();
            server.Invalidate();
        }

        private void ServerKeepVisible() {
            if (serverSelected < 0) return;
            if (serverSelected < serverTop) serverTop = serverSelected;
            if (serverSelected >= serverTop + ServerRows) serverTop = serverSelected - ServerRows + 1;
        }

        /// <summary>
        /// A login made by the Custom launcher goes on to the characters by itself: with a single character server its
        /// list is answered as OK would answer it. False while the list is not up (or offers a choice).
        /// </summary>
        public static bool TryConfirmSingleServer() {
            RoClassicPreGame p = instance;
            if (p == null || p.server == null || !p.server.Visible || p.serverSent || p.servers.Length != 1) return false;
            p.serverSelected = 0;
            p.ServerOk();
            return true;
        }

        /// <summary>vf37 0x820450 event 6 / 0xb4: nothing without a selection, else close, the sound, the saved index, connect.</summary>
        private void ServerOk() {
            if (serverSent || serverSelected < 0 || serverSelected >= servers.Length) return;
            serverSent = true;
            Ragnarok.Audio.RoAudioEvents.PlayButtonSound();
            PlayerPrefs.SetInt(ServerIndexKey, serverSelected);
            server.Visible = false;
            shell.SelectServerFromPhase11Ui(serverSelected);
            seenSequence = int.MinValue;
        }

        /// <summary>0xb5: close, mode 0x2716 (back to the login).</summary>
        private void ServerCancel() {
            if (serverSent) return;
            server.Visible = false;
            shell.CancelServerSelectFromPhase11Ui();
            seenSequence = int.MinValue;
        }

        /// <summary>Enter sends the default button (OK); the arrows go to the list (Msg 0x12 / 0x13).</summary>
        private void ServerTick() {
            if (server == null || !server.Visible || RoClassicMessageBox.ModalOpen || RoClassicTextInput.HasFocus) return;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) ServerOk();
            else if (keyboard.upArrowKey.wasPressedThisFrame) ServerMove(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) ServerMove(1);
#endif
        }

        /// <summary>The sweep console's !pregame server pick n | ok | cancel.</summary>
        private string SweepServer(string[] a) {
            if (server == null) return "no server list";
            switch (a.Length > 1 ? a[1] : string.Empty) {
                case "pick":
                    if (a.Length > 2 && int.TryParse(a[2], out int i) && i >= 0 && i < servers.Length) {
                        serverSelected = i;
                        server.Invalidate();
                    }

                    break;
                case "ok":
                    ServerOk();
                    break;
                case "cancel":
                    ServerCancel();
                    break;
            }

            string rows = string.Empty;
            for (int i = 0; i < servers.Length; i++) rows += (i > 0 ? " | " : string.Empty) + ServerRowText(servers[i]) + " users=" + servers[i].UserCount + " state=" + servers[i].State + " property=" + servers[i].Property;
            return "server visible=" + server.Visible + " selected=" + serverSelected + " rows=" + rows;
        }
    }
}
