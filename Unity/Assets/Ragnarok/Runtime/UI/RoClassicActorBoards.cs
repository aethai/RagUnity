using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Combat;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The title boards over vending players, UIMerchantShopTitle (codex-ui-research RUNTIME-RULES-TRADE-CART-VENDING-
    /// CHATROOM.md "Actor title boards"): 140 × 34 at (actor.x − 70, actor.y − K · scale − 34), the 140 × 28 panel white
    /// (#FFFA7D for the own shop) with the #C3C3C3 outline at (2, 2, 136, 24), shop.bmp at (3, 3) and the title from
    /// (31, 8) in 106 px. Every board is one quad group on a shared layer that follows its actor through a GPU anchor
    /// (moving the camera never re-uploads geometry); one invisible window follows the board under the cursor, so a
    /// board takes the mouse like the client's UIWindow (the full 140 × 34, hand cursor) and a double click (or a single
    /// press with /shopping on) asks the seller's list (Msg(0x54) → CZ_REQ_BUY_FROMMC; nothing for the own board); with
    /// /shopping on a right press closes the shop windows. Fed by the legacy RoVendingSignboardOverlay.
    /// The chat room boards, UIChatRoomTitle, share the size and the hit rectangle and sit 17 px lower: sysbox pieces with the
    /// arrow under them, chat_close.bmp (private) or chat_open.bmp at (3, 3) and "%s (%d/%d)" (the title alone for an NPC
    /// room without a limit) from (31, 8); only a double-click does something (0x7bfe60). Fed by RoClassicChatRoom.
    /// Buying store boards are the merchant board in mode 1 (buyingshop.bmp; a click asks the buyer's list, Msg(0x110) →
    /// CZ_REQ_CLICK_TO_BUYING_STORE). Fed by RoClassicBuyingStore.
    /// </summary>
    public sealed class RoClassicActorBoards : IRoClassicUiProducer {
        private const int BoardW = 140;
        private const int BoardH = 34;
        private const int PanelH = 28;
        private static readonly Color32 PanelWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 PanelOwn = new Color32(0xff, 0xfa, 0x7d, 255);
        private static readonly Color32 Border = new Color32(0xc3, 0xc3, 0xc3, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        private sealed class Board {
            public uint OwnerAid;
            public string Title = string.Empty;
            public bool Own;
            public bool Chat;
            public bool Buying;
            public RoChatRoomInfo Room;
            public int Anchor;
            public bool Visible;
            public int ClientX;
            public int ClientY;
        }

        private static RoClassicActorBoards instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private RoClassicWindow proxy;
        private readonly Dictionary<uint, Board> boards = new Dictionary<uint, Board>();
        private readonly Dictionary<uint, Board> chatBoards = new Dictionary<uint, Board>();
        private Action<RoChatRoomInfo> chatDoubleClicked;
        private readonly Dictionary<uint, Board> buyingBoards = new Dictionary<uint, Board>();
        private Action<uint> buyingClicked;
        private readonly List<uint> stale = new List<uint>();
        private bool geometryDirty;
        private int seenSequence = -1;
        private RoLoginShellSession session;
        private IRoSocialEconomyCommandExecutor commands;
        private RoOnlineWorldController world;
        private Board hovered;
        private float lastShopRequest = -10f;   // CGameMode+0x280, msg 0x54's once-a-second gate

        private static RoClassicActorBoards Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicActorBoards { system = system };
                instance.layer = system.Renderer.CreateLayer("actor boards");
                system.Renderer.SendToBack(instance.layer);
                instance.proxy = new RoClassicWindow(system, new RoClassicWindowLayout { Class = "UIMerchantShopTitle", Width = BoardW, Height = BoardH }) {
                    Visible = false,
                    Draggable = false,
                    ClickableAt = _ => true,
                };
                instance.proxy.Pressed += (w, local) => instance.Click();
                instance.proxy.DoubleClicked += (w, local) => instance.DoubleClick();
                instance.proxy.RightClicked += (w, local) => instance.RightClick();
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Per frame from the legacy overlay while the classic UI is on: the boards follow the vending state.</summary>
        public static bool Sync(RoLoginShellSession session, IRoSocialEconomyCommandExecutor commands, RoOnlineWorldController world) {
            if (session == null) return false;
            RoClassicActorBoards b = Ensure();
            if (b == null) return false;
            b.session = session;
            b.commands = commands;
            b.world = world;
            int sequence = session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Vending);
            if (sequence != b.seenSequence) {
                b.seenSequence = sequence;
                b.Rebuild(session.VendingSnapshot(), session.LocalActorId);
            }

            return true;
        }

        /// <summary>The chat room boards in sight (the session's chat room state); a double-click calls back.</summary>
        public static void SyncChats(RoChatRoomInfo[] rooms, uint localAid, Action<RoChatRoomInfo> doubleClicked) {
            RoClassicActorBoards b = rooms != null && rooms.Length > 0 ? Ensure() : instance;
            if (b == null) return;
            b.chatDoubleClicked = doubleClicked;
            if (b.world == null && rooms != null && rooms.Length > 0) b.world = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            b.stale.Clear();
            foreach (uint owner in b.chatBoards.Keys) b.stale.Add(owner);
            foreach (RoChatRoomInfo room in rooms ?? Array.Empty<RoChatRoomInfo>()) {
                if (room.OwnerAid == 0) continue;
                b.stale.Remove(room.OwnerAid);
                // SendMsg(0x22): the title up to its first carriage return, "%s (%d/%d)" except type 3
                string title = room.Title;
                int cut = title.IndexOfAny(new[] { '\r', '\n' });
                if (cut >= 0) title = title.Substring(0, cut);
                if (room.Type != 3) title = string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, room.Users, room.Limit);
                if (!b.chatBoards.TryGetValue(room.OwnerAid, out Board board)) {
                    board = new Board { OwnerAid = room.OwnerAid, Chat = true, Anchor = b.system.Renderer.AllocateAnchor() };
                    b.chatBoards[room.OwnerAid] = board;
                    b.geometryDirty = true;
                }

                if (board.Title != title || board.Room.IsPrivate != room.IsPrivate) b.geometryDirty = true;
                board.Title = title;
                board.Own = room.OwnerAid == localAid;
                board.Room = room;
            }

            foreach (uint owner in b.stale) {
                if (b.hovered != null && b.hovered.Chat && b.hovered.OwnerAid == owner) b.hovered = null;
                b.system.Renderer.FreeAnchor(b.chatBoards[owner].Anchor);
                b.chatBoards.Remove(owner);
                b.geometryDirty = true;
            }
        }

        /// <summary>The buying store boards in sight (the own one included); a click on another's calls back with its owner.</summary>
        public static void SyncBuying(RoBuyingStoreBoard[] rooms, uint localAid, Action<uint> clicked) {
            RoClassicActorBoards b = rooms != null && rooms.Length > 0 ? Ensure() : instance;
            if (b == null) return;
            b.buyingClicked = clicked;
            if (b.world == null && rooms != null && rooms.Length > 0) b.world = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            b.stale.Clear();
            foreach (uint owner in b.buyingBoards.Keys) b.stale.Add(owner);
            foreach (RoBuyingStoreBoard s in rooms ?? Array.Empty<RoBuyingStoreBoard>()) {
                if (s.OwnerAid == 0) continue;
                b.stale.Remove(s.OwnerAid);
                string title = s.Title;
                int cut = title.IndexOfAny(new[] { '\r', '\n' });
                if (cut >= 0) title = title.Substring(0, cut);
                if (!b.buyingBoards.TryGetValue(s.OwnerAid, out Board board)) {
                    board = new Board { OwnerAid = s.OwnerAid, Buying = true, Anchor = b.system.Renderer.AllocateAnchor() };
                    b.buyingBoards[s.OwnerAid] = board;
                    b.geometryDirty = true;
                }

                bool own = s.OwnerAid == localAid;
                if (board.Title != title || board.Own != own) b.geometryDirty = true;
                board.Title = title;
                board.Own = own;
            }

            foreach (uint owner in b.stale) {
                if (b.hovered != null && b.hovered.Buying && b.hovered.OwnerAid == owner) b.hovered = null;
                b.system.Renderer.FreeAnchor(b.buyingBoards[owner].Anchor);
                b.buyingBoards.Remove(owner);
                b.geometryDirty = true;
            }
        }

        public static void Clear() {
            if (instance == null) return;
            foreach (Board board in instance.boards.Values) instance.system.Renderer.FreeAnchor(board.Anchor);
            instance.boards.Clear();
            instance.proxy.Visible = false;
            instance.hovered = null;
            instance.geometryDirty = true;
            instance.seenSequence = -1;
        }

        private void Rebuild(RoVendingSnapshot vending, uint localAid) {
            stale.Clear();
            foreach (uint owner in boards.Keys) stale.Add(owner);
            foreach (RoVendingBoardSnapshot s in vending.Boards ?? Array.Empty<RoVendingBoardSnapshot>()) {
                if (s.OwnerAid == 0 || (s.OwnerAid == localAid && !vending.IsOwnStoreOpen)) continue;
                stale.Remove(s.OwnerAid);
                bool own = s.OwnerAid == localAid || (vending.IsOwnStoreOpen && s.OwnerAid == vending.OwnStoreOwnerAid);
                // SendMsg(0x22): the title up to its first line break
                string title = s.Title ?? string.Empty;
                int cut = title.IndexOfAny(new[] { '\r', '\n' });
                if (cut >= 0) title = title.Substring(0, cut);
                if (!boards.TryGetValue(s.OwnerAid, out Board board)) {
                    board = new Board { OwnerAid = s.OwnerAid, Anchor = system.Renderer.AllocateAnchor() };
                    boards[s.OwnerAid] = board;
                    geometryDirty = true;
                }

                if (board.Title != title || board.Own != own) {
                    board.Title = title;
                    board.Own = own;
                    geometryDirty = true;
                }
            }

            // the own shop: the client puts its own board up on the open answer (the server sends ZC_STORE_ENTRY to the others)
            uint ownAid = vending.OwnStoreOwnerAid != 0 ? vending.OwnStoreOwnerAid : localAid;
            if (vending.IsOwnStoreOpen && ownAid != 0) {
                stale.Remove(ownAid);
                string title = vending.OwnStoreTitle ?? string.Empty;
                if (!boards.TryGetValue(ownAid, out Board own)) {
                    own = new Board { OwnerAid = ownAid, Anchor = system.Renderer.AllocateAnchor() };
                    boards[ownAid] = own;
                    geometryDirty = true;
                }

                if (!own.Own || (own.Title.Length == 0 && title.Length > 0)) {
                    own.Own = true;
                    if (own.Title.Length == 0) own.Title = title;
                    geometryDirty = true;
                }
            }

            foreach (uint owner in stale) {
                if (hovered != null && hovered.OwnerAid == owner) hovered = null;
                system.Renderer.FreeAnchor(boards[owner].Anchor);
                boards.Remove(owner);
                geometryDirty = true;
            }
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            // F11 hides the boards with the other windows of the list (the shop boards 0xbd by 0x9bc750(0)) until it shows them
            bool hidden = RoClassicCloseTabs.OverheadHidden;
            if (layer.Visible == hidden) layer.Visible = !hidden;
            if (hidden) {
                if (proxy.Visible) proxy.Visible = false;
                hovered = null;
                return;
            }

            RoClassicUiRenderer renderer = ui.Renderer;
            UnityEngine.Camera camera = ui.Camera;
            var context = world as IRoCombatPresentationContext;
            float s = renderer.Scale;
            int clientH = camera != null ? (int) (camera.pixelHeight / s) : 480;
            Vector3 up = camera != null ? camera.transform.up * (RoClassicSignBoards.LiftSpritePixels(clientH) / ROIO.Models.FileTypes.SPR.PIXELS_PER_UNIT) : Vector3.zero;
            foreach (Board board in boards.Values) Follow(board, renderer, camera, context, s, up, ShopDrop);
            foreach (Board board in chatBoards.Values) Follow(board, renderer, camera, context, s, up, ChatDrop);
            foreach (Board board in buyingBoards.Values) Follow(board, renderer, camera, context, s, up, ShopDrop);

            FollowMouse(ui);
            if (!geometryDirty) return;
            geometryDirty = false;
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            RoClassicGdiFont font = ui.Font(12);
            ui.Images.TryGet("유저인터페이스\\shop.bmp", out RoClassicUiImage icon);
            ui.Images.TryGet("유저인터페이스\\buyingshop.bmp", out RoClassicUiImage buyingIcon);
            foreach (Board board in buyingBoards.Values) DrawMerchantBoard(ui, q, font, board, buyingIcon);
            foreach (Board board in chatBoards.Values) DrawChatBoard(ui, q, font, board);
            foreach (Board board in boards.Values) {
                q.Anchor = board.Anchor;
                // OnDraw 0x7be710: the panel, its outline, the icon, the title (static text (31, 8, 106, 15))
                q.Image(0, 0, BoardW, PanelH, ui.White, board.Own ? PanelOwn : PanelWhite);
                q.Image(2, 2, BoardW - 4, 1, ui.White, Border);
                q.Image(2, PanelH - 3, BoardW - 4, 1, ui.White, Border);
                q.Image(2, 2, 1, PanelH - 4, ui.White, Border);
                q.Image(BoardW - 3, 2, 1, PanelH - 4, ui.White, Border);
                if (icon.Width > 0) q.Image(3, 3, icon, new Color32(255, 255, 255, 255));
                q.Text(font, 31, 8, Fit(font, board.Title, 106), Black);
            }

            q.Anchor = 0;
            layer.MarkDirty();
        }

        /// <summary>OnDraw 0x7be710 in mode 1: the merchant panel with buyingshop.bmp.</summary>
        private static void DrawMerchantBoard(RoClassicUiSystem ui, RoClassicUiQuads q, RoClassicGdiFont font, Board board, RoClassicUiImage icon) {
            q.Anchor = board.Anchor;
            q.Image(0, 0, BoardW, PanelH, ui.White, board.Own ? PanelOwn : PanelWhite);
            q.Image(2, 2, BoardW - 4, 1, ui.White, Border);
            q.Image(2, PanelH - 3, BoardW - 4, 1, ui.White, Border);
            q.Image(2, 2, 1, PanelH - 4, ui.White, Border);
            q.Image(BoardW - 3, 2, 1, PanelH - 4, ui.White, Border);
            if (icon.Width > 0) q.Image(3, 3, icon, new Color32(255, 255, 255, 255));
            q.Text(font, 31, 8, Fit(font, board.Title, 106), Black);
        }

        /// <summary>
        /// CGameActor 0xbd3ef0 places the boards every tick at (x − 70, y − K · scale − drop), K · scale being the chat
        /// balloons' 81 sprite pixels as the camera draws the actor: the chat room title (+0x268) 17 px higher, the
        /// merchant (+0x26c) and buying store (+0x274) titles 34. The scaledY(110) of their creation (0xbdb05f) only
        /// lasts until that first tick.
        /// </summary>
        private const int ChatDrop = 17, ShopDrop = 34;

        private static void Follow(Board board, RoClassicUiRenderer renderer, UnityEngine.Camera camera, IRoCombatPresentationContext context, float s, Vector3 up, int drop) {
            board.Visible = false;
            if (camera != null && context != null && context.TryResolveActorFeedbackAnchor(board.OwnerAid, out Transform actor, out _) && actor != null) {
                // the actor's projected ground point (CGameActor +0xac / +0xb0)
                Vector3 world = actor.position;
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z > 0f) {
                    int lift = Mathf.Max(0, (int) ((camera.WorldToScreenPoint(world + up).y - screen.y) / s));
                    board.ClientX = (int) (screen.x / s) - BoardW / 2;
                    board.ClientY = (int) ((camera.pixelHeight - screen.y) / s) - lift - drop;
                    board.Visible = true;
                }
            }

            renderer.SetAnchor(board.Anchor, Mathf.RoundToInt(board.ClientX * s), Mathf.RoundToInt(board.ClientY * s), board.Visible);
        }

        /// <summary>
        /// OnDraw 0x7bdce0: sysbox_lu (0, 0), sysbox_ru (126, 0), sysbox_mu every 12 px from x 12; the same bottom row at
        /// y 14; sysbox_arr_l at (63, 20); chat_close.bmp (type 0) or chat_open.bmp at (3, 3); the text from (31, 8).
        /// </summary>
        private static void DrawChatBoard(RoClassicUiSystem ui, RoClassicUiQuads q, RoClassicGdiFont font, Board board) {
            q.Anchor = board.Anchor;
            var white = new Color32(255, 255, 255, 255);
            void Piece(string name, int x, int y) {
                if (ui.Images.TryGet("유저인터페이스\\" + name, out RoClassicUiImage image) && image.Width > 0) q.Image(x, y, image, white);
            }

            for (int i = 0, n = (BoardW - 24 + 11) / 12; i < n; i++) {
                Piece("sysbox_mu.bmp", 12 + 12 * i, 0);
                Piece("sysbox_md.bmp", 12 + 12 * i, 14);
            }

            Piece("sysbox_lu.bmp", 0, 0);
            Piece("sysbox_ru.bmp", BoardW - 14, 0);
            Piece("sysbox_ld.bmp", 0, 14);
            Piece("sysbox_rd.bmp", BoardW - 14, 14);
            Piece("sysbox_arr_l.bmp", 63, 20);
            Piece(board.Room.IsPrivate ? "chat_close.bmp" : "chat_open.bmp", 3, 3);
            q.Text(font, 31, 8, Fit(font, board.Title, BoardW - 34), Black);
        }

        private static string Fit(RoClassicGdiFont font, string text, int width) {
            if (font.Measure(text) <= width) return text;
            int n = text.Length;
            while (n > 0 && font.Measure(text.Substring(0, n) + "...") > width) n--;
            return text.Substring(0, n) + "...";
        }

        /// <summary>The invisible window sits on the board under the cursor (when no other window is there).</summary>
        private void FollowMouse(RoClassicUiSystem ui) {
            RoClassicWindowManager windows = ui.Windows;
            Vector2Int mouse = windows.Mouse;
            Board found = null;
            RoClassicWindow over = windows.HoverWindow;
            if (over == null || over == proxy) {
                foreach (Board board in buyingBoards.Values) {
                    if (board.Visible && mouse.x >= board.ClientX && mouse.x < board.ClientX + BoardW && mouse.y >= board.ClientY && mouse.y < board.ClientY + BoardH) {
                        found = board;
                    }
                }

                foreach (Board board in chatBoards.Values) {
                    if (board.Visible && mouse.x >= board.ClientX && mouse.x < board.ClientX + BoardW && mouse.y >= board.ClientY && mouse.y < board.ClientY + BoardH) {
                        found = board;
                    }
                }

                foreach (Board board in boards.Values) {
                    if (board.Visible && mouse.x >= board.ClientX && mouse.x < board.ClientX + BoardW && mouse.y >= board.ClientY && mouse.y < board.ClientY + BoardH) {
                        found = board;
                    }
                }
            }

            hovered = found;
            if (found == null) {
                if (proxy.Visible) proxy.Visible = false;
                return;
            }

            proxy.MoveTo(found.ClientX, found.ClientY);
            if (!proxy.Visible) proxy.Visible = true;
        }

        /// <summary>vf25 0x7c01e0: a single press opens the board's shop only with /shopping on ([0x01552d54]).</summary>
        private void Click() {
            if (RoClassicCommandFlags.Shopping) OpenHovered();
        }

        /// <summary>
        /// A shop board's shop: Msg(0x110) for a buying store, Msg(0x54) for a seller (0xc1bcae: never the own AID, at most
        /// once a second, mode+0x280).
        /// </summary>
        private void OpenHovered() {
            if (hovered != null && hovered.Buying) {
                if (!hovered.Own) buyingClicked?.Invoke(hovered.OwnerAid);
                return;
            }

            if (hovered == null || hovered.Chat || hovered.Own || commands == null) return;
            float now = Time.unscaledTime;
            if (now - lastShopRequest <= 1f) return;
            lastShopRequest = now;
            _ = commands.RequestPlayerShopAsync(hovered.OwnerAid);
        }

        /// <summary>vf26: a chat board's double-click (0x7bfe60); a shop board's (0x7bfec0) opens the shop whatever /shopping is.</summary>
        private void DoubleClick() {
            if (hovered != null && hovered.Chat) {
                chatDoubleClicked?.Invoke(hovered.Room);
                return;
            }

            OpenHovered();
        }

        /// <summary>vf32 0x7c13f0: with /shopping on a right press on a shop board sends mode msg 0xf5 (the shop windows close).</summary>
        private void RightClick() {
            if (hovered == null || hovered.Chat || !RoClassicCommandFlags.Shopping) return;
            RoClassicVending.CloseCustomerShopWindows();
            RoClassicBuyingStore.CloseSellerWindows();
        }

        /// <summary>Diagnostics: the boards and where they are.</summary>
        public static string Describe() {
            if (instance == null) return "no boards";
            var s = new System.Text.StringBuilder();
            foreach (Board board in instance.buyingBoards.Values) {
                s.Append("[buying ").Append(board.OwnerAid).Append(" '").Append(board.Title).Append("' ")
                    .Append(board.Visible ? board.ClientX + "," + board.ClientY : "off").Append(board.Own ? " own" : string.Empty).Append("] ");
            }

            foreach (Board board in instance.chatBoards.Values) {
                s.Append("[chat ").Append(board.Room.ChatId).Append('@').Append(board.OwnerAid).Append(" '").Append(board.Title).Append("' ")
                    .Append(board.Visible ? board.ClientX + "," + board.ClientY : "off").Append(board.Own ? " own" : string.Empty).Append("] ");
            }

            foreach (Board board in instance.boards.Values) {
                s.Append('[').Append(board.OwnerAid).Append(" '").Append(board.Title).Append("' ").Append(board.Visible ? board.ClientX + "," + board.ClientY : "off")
                    .Append(board.Own ? " own" : string.Empty).Append("] ");
            }

            return s.Length == 0 ? "no boards" : s.ToString().TrimEnd();
        }
    }
}
