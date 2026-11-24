using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>A RODEX attachment carried out of the writer (drag source 0xf); dropped on the inventory it is removed.</summary>
    internal sealed class RoClassicRodexPayload : IRoClassicDragPayload {
        public int BagIndex;
        public int Amount;
        public string Name;
        public string IconPath { get; set; }
    }

    /// <summary>
    /// RODEX after codex-ui-research RUNTIME-RULES-COMPANIONS-MAIL.md "rodex" and its follow-up: the arrival icon
    /// UIOpenMailBoxWnd 0x106, the Inbox UIRodexInboxWnd 0x107 (opens with CZ_OPEN_RODEXBOX), the read window
    /// UIRodexContentsWnd 0x109 and the writer UIRodexComposeWnd 0x108, over the session's RoRodexRuntime. While the
    /// classic UI is on, the legacy RODEX windows stay closed.
    /// </summary>
    public sealed class RoClassicRodex : IRoClassicUiProducer {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string Rx = Basic + "rodexsystem\\renewal\\";
        private const int CloseId = 0xc5, IconButtonId = 0xbc, ValidateId = 0xd1, SendId = 0x155;
        private const int Rows = 6, RowPitch = 44;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 TabText = new Color32(0xad, 0xb5, 0xcc, 255);
        private static readonly Color32 TabTextOn = new Color32(0x1f, 0x3e, 0x9c, 255);
        private static readonly Color32 SenderColor = new Color32(0x42, 0x4a, 0x84, 255);
        private static readonly Color32 HoverColor = new Color32(0x7e, 0x41, 0x51, 255);
        private static readonly Color32 ExpireColor = new Color32(0xe9, 0x0a, 0x0a, 255);
        private static readonly Color32 ReturnedColor = new Color32(0x73, 0x66, 0x66, 255);
        private static readonly Color32 QuotaColor = new Color32(0x82, 0x94, 0xc8, 255);
        private static readonly Color32 BodyFill = new Color32(0xbd, 0xce, 0xf7, 255);
        private static readonly Color32 BodyText = new Color32(0x31, 0x31, 0x31, 255);
        private static readonly Color32 CounterColor = new Color32(0x44, 0x6e, 0xd6, 255);
        private static readonly Color32 Gray73 = new Color32(0x73, 0x73, 0x73, 255);
        private static readonly Color32 StatusRed = new Color32(0xc6, 0x28, 0x28, 255);
        private const string AckColor = "#FFFF64";

        private static RoClassicRodex instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private RoRodexSnapshot rodex = RoRodexSnapshot.Empty;
        private int seenSequence = -1, seenAck = -1, seenIcon = -1;
        private ulong shownRead;
        private bool composeShown;
        private RoClassicWindow icon, inbox, contents, compose;

        public static void Tick(RoLoginShellSession session, RoClassicChat chat, RoItemMetadataTable metadata) {
            if (session == null) return;
            RoClassicRodex r = Ensure();
            if (r == null) return;
            r.session = session;
            r.chat = chat;
            r.metadata = metadata ?? RoItemMetadataTable.Empty;
            RoMailSnapshot mail = session.MailSnapshot();
            if (mail.RodexIconUpdateCount != r.seenIcon) {
                r.seenIcon = mail.RodexIconUpdateCount;
                // 0x09e7 (FUN_00c97300): byte 1 makes 0x106, anything else deletes it
                if (mail.HasUnreadRodexMail == true) r.ShowIcon();
                else if (r.icon != null) r.icon.Visible = false;
            }

            if (session.RodexSequence == r.seenSequence) return;   // the snapshot copies every mail: only on a change
            RoRodexSnapshot s = session.RodexSnapshot();
            if (s.Sequence == r.seenSequence) return;
            r.seenSequence = s.Sequence;
            r.rodex = s;
            if (r.seenAck >= 0 && s.AckSerial != r.seenAck) r.Announce(s);
            r.seenAck = s.AckSerial;
            r.SyncWindows();
        }

        private static RoClassicRodex Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicRodex { system = system };
                system.Register(instance);
                // the guild member menu's "Send a mail" (mode 0x10c with the name)
                RoClassicGuild.MailComposer = name => instance?.OpenWrite(name);
            }

            return instance;
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            // hover colours of the text controls
            if (inbox != null && inbox.Visible && ui.Windows.HoverWindow == inbox) inbox.Invalidate();
        }

        /// <summary>The ack messages (0x09ed FUN_00c8b240, 0x09f4 / 0x09f2, 0x0a51 with char id 0) in #FFFF64.</summary>
        private void Announce(RoRodexSnapshot s) {
            switch (s.AckKind) {
                case RoRodexAckKind.Send: {
                    int[] ids = { 0xa1b, 0xa25, 0xa2b, 0xa26, 0xa27 };
                    string[] fb = {
                        "Your mail has been sent.", "Failed to send the mail.", "You have exceeded the number of mails you can send.",
                        "The item cannot be sent.", "The recipient does not exist."
                    };
                    if (s.AckResult < ids.Length) Chat(Msg(ids[s.AckResult], fb[s.AckResult]));
                    break;
                }
                case RoRodexAckKind.Items: {
                    int[] ids = { 0xa1c, 0xa1d, 0xa1e };
                    string[] fb = { "This item has been moved to the inventory.", "Failed to get items.", "Please empty your inventory." };
                    if (s.AckResult < ids.Length) Chat(Msg(ids[s.AckResult], fb[s.AckResult]));
                    break;
                }
                case RoRodexAckKind.Zeny: {
                    int[] ids = { 0xa1f, 0xa20, 0xa21 };
                    string[] fb = { "Zeny received.", "Failed to get zeny.", "Exceeded the limit of zeny." };
                    if (s.AckResult < ids.Length) Chat(Msg(ids[s.AckResult], fb[s.AckResult]));
                    break;
                }
                case RoRodexAckKind.NameCheck:
                    Chat(Msg(0xa37, "The recipient's name does not exist."));
                    break;
            }
        }

        private void Chat(string text) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, AckColor);
        }

        private void SyncWindows() {
            inbox?.Invalidate();
            RoRodexReadSnapshot read = rodex.Read;
            if (read.HasMail && read.MailId != shownRead) {
                shownRead = read.MailId;
                OpenContents();
            } else if (!read.HasMail && shownRead != 0) {
                shownRead = 0;
                if (contents != null) contents.Visible = false;
            }

            contents?.Invalidate();
            if (rodex.Compose.IsOpen && !composeShown) {
                composeShown = true;
                OpenCompose();
            } else if (!rodex.Compose.IsOpen && composeShown) {
                composeShown = false;
                if (compose != null) compose.Visible = false;
            }

            SyncCompose();
        }

        // ================================================================ 0x106

        /// <summary>UIOpenMailBoxWnd: 43 × 43 at (W − 234, 16); its button 0xbc toggles the Inbox.</summary>
        private void ShowIcon() {
            if (icon == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIOpenMailBoxWnd").text);
                icon = new RoClassicWindow(system, layout) { Visible = false, Draggable = false };
                icon.Clicked += child => {
                    if (child.Id == IconButtonId) ToggleInbox();
                };
            }

            Vector2Int client = system.ClientSize;
            icon.MoveTo(client.x - 234, 16);
            icon.Visible = true;
            icon.Invalidate();
        }

        // ================================================================ 0x107

        private int bucket, page, hoverRow = -1;
        private bool searchActive, searchBySender;
        private readonly bool[] checks = new bool[Rows];
        private readonly List<RoRodexMailSnapshot> rows = new List<RoRodexMailSnapshot>();
        private readonly List<RoRodexMailSnapshot> searchResults = new List<RoRodexMailSnapshot>();
        private RoClassicEditField searchEdit;

        /// <summary>The icon / Basic Info mail button: MakeWindow(0x107) or DeleteWindow(0x107).</summary>
        public static bool ToggleInbox() {
            RoClassicRodex r = Ensure();
            if (r == null || r.session == null) return false;
            if (r.inbox != null && r.inbox.Visible) r.CloseInbox();
            else r.OpenInbox();
            return true;
        }

        /// <summary>
        /// 309 × 416 at ((W − 640) / 2 + 100, H · 100 / 480); the constructor FUN_00768920 sends CZ_OPEN_RODEXBOX with the
        /// three upper mail ids.
        /// </summary>
        private void OpenInbox() {
            if (inbox == null) {
                var layout = new RoClassicWindowLayout { Class = "UIRodexInboxWnd", WindowId = 0x107, Width = 309, Height = 416 };
                RoClassicFrames.Bitmap(layout.Ops, Rx + "bg_rodex_list.bmp", 0, 0);
                inbox = new RoClassicWindow(system, layout) { Visible = false };
                inbox.DrawDynamic += DrawInbox;
                inbox.Pressed += (w, local) => InboxPressed(local);
                inbox.EscClose = () => {
                    CloseInbox();
                    return true;
                };
                // the search edit (133, 65, 132, 18); Enter runs the search like its button 0x13d
                searchEdit = new RoClassicEditField { Rect = new RectInt(133, 65, 132, 18), MaxBytes = 23 };
                searchEdit.Submitted += _ => RunSearch();
                inbox.AddEditField(searchEdit);
            }

            Array.Clear(checks, 0, checks.Length);
            page = 0;
            Vector2Int client = system.ClientSize;
            inbox.MoveTo((client.x - 640) / 2 + 100, client.y * 100 / 480);
            inbox.Visible = true;
            system.Windows.BringToFront(inbox);
            inbox.Invalidate();
            session.OpenRodexLocally();
            _ = session.BeginLegacyOpenRodex();
        }

        /// <summary>DeleteWindow(0x107): CZ_CLOSE_MAILBOX, then the icon goes too.</summary>
        private void CloseInbox() {
            if (inbox == null || !inbox.Visible) return;
            inbox.Visible = false;
            if (contents != null) contents.Visible = false;
            _ = session?.BeginLegacyCloseRodex();
            session?.CloseRodexLocally();
            if (icon != null) icon.Visible = false;
        }

        private static readonly (int Bucket, int X, int Msg, string Fallback)[] Tabs = {
            (1, 19, 0xdda, "Notice"), (0, 87, 0xddb, "General"), (2, 155, 0xddc, "Return"), (3, 223, 0xddd, "Search"),
        };

        private List<RoRodexMailSnapshot> BucketRows() {
            rows.Clear();
            if (bucket == 3) {
                rows.AddRange(searchResults);
                return rows;
            }

            foreach (RoRodexMailSnapshot m in rodex.Mails) {
                if (m.OpenType == bucket) rows.Add(m);
            }

            return rows;
        }

        /// <summary>OnDraw 0x76a8d0 with the row helpers 0x769320 (icons), 0x76d1f0 (expiry), 0x76cc40 (texts).</summary>
        private void DrawInbox(RoClassicWindow w) {
            w.DrawBitmap(w.Width - 14, 3, Basic + "sys_close_off.bmp");
            DrawSearchBar(w);
            Vector2Int mouse = Local(w);
            foreach (var tab in Tabs) {
                bool on = tab.Bucket == bucket, over = !on && Inside(mouse, tab.X, 86, 68, 28);
                w.DrawBitmap(tab.X, 86, Rx + "btn_listtab_empty_" + (on ? "press" : over ? "over" : "out") + ".bmp");
                string text = Msg(tab.Msg, tab.Fallback);
                RoClassicGdiFont font = w.FontFor(0, 12, true);
                w.TextOutPlain(tab.X + (68 - font.Measure(text)) / 2, 86 + (28 - 15) / 2, text, font, on || over ? TabTextOn : TabText);
            }

            w.DrawBitmap(248, 20, Rx + "btn_refresh_" + (Inside(mouse, 248, 20, 24, 26) ? "over" : "out") + ".bmp");
            w.DrawBitmap(277, 18, Rx + "btn_write_" + (Inside(mouse, 277, 18, 24, 26) ? "over" : "out") + ".bmp");
            if (!rodex.IsOpen) {
                // loading (DAT_01272704 + 0x1c == 0): img_notice.bmp, 0xc76 wrapped on 248 px with 15 px lines, 0xc77
                w.DrawBitmap(31, 154, Rx + "img_notice.bmp");
                string loading = Msg(0xc76, "Loading mailbox, Please don't delete the^c92114 message^000000~!!");
                var lines = new List<(int Start, int End)>();
                WrapPixels(w.FontFor(0, 12), loading, 248, lines);
                for (int i = 0; i < lines.Count; i++) w.TextOutColored(31, 229 + 15 * i, loading, lines[i].Start, lines[i].End, 0, 12, ref DefaultColor);
                RoClassicGdiFont big = w.FontFor(0, 18, true);
                string now = Msg(0xc77, "NOW LOADING..");
                w.TextOutPlain(31 + (248 - big.Measure(now)) / 2, 270, now, big, QuotaColor);
            } else {
                List<RoRodexMailSnapshot> list = BucketRows();
                hoverRow = -1;
                for (int i = 0; i < Rows; i++) {
                    int k = page * Rows + i;
                    if (k >= list.Count) break;
                    DrawRow(w, i, list[k], mouse);
                }
            }

            // the bottom: Delete all, Retrieve all / selected, Prev / Next
            DrawBasicButton(w, 4, 392, 70, Msg(0xe05, "Delete all"), mouse);
            bool any = Array.Exists(checks, c => c);
            DrawBasicButton(w, 84, 392, 70, any ? Msg(0xe07, "Retrieve selected") : Msg(0xe08, "Retrieve all"), mouse);
            w.DrawBitmap(255, 390, Rx + "btn_prev_" + (Inside(mouse, 255, 390, 24, 24) ? "over" : "out") + ".bmp");
            w.DrawBitmap(279, 390, Rx + "btn_next_" + (Inside(mouse, 279, 390, 24, 24) ? "over" : "out") + ".bmp");
            // the sent-today quota "%d" right on 273 and "/100" from 273 (font 0 / 11, #8294C8)
            w.TextOutRight(273, 46, "0", 0, 11, QuotaColor);
            w.TextOut(273, 46, Msg(0xbda, "/100"), 0, 11, QuotaColor);
        }

        private static Color32 DefaultColor = new Color32(0, 0, 0, 255);

        /// <summary>The search radios Title / Sender (22, 68) / (73, 68), the edit and the search button (264, 65).</summary>
        private void DrawSearchBar(RoClassicWindow w) {
            Color32 label = new Color32(0x39, 0x49, 0x65, 255);
            w.DrawBitmap(22, 68, Rx + "checkbox_search_" + (!searchBySender ? "on" : "off") + ".bmp");
            w.TextOut(36, 67, Msg(0xc78, "Title"), 0, 12, label);
            w.DrawBitmap(73, 68, Rx + "checkbox_search_" + (searchBySender ? "on" : "off") + ".bmp");
            w.TextOut(87, 67, Msg(0xc79, "Sender"), 0, 12, label);
            w.DrawBitmap(264, 65, Rx + "btn_search_out.bmp");
        }

        private void DrawRow(RoClassicWindow w, int i, RoRodexMailSnapshot m, Vector2Int mouse) {
            int y = 121 + RowPitch * i;
            bool returned = bucket == 3 && m.OpenType == 2;
            w.DrawBitmap(36, 124 + RowPitch * i, Rx + (returned ? "icon_status_mail_returned.bmp" : !m.IsRead ? "icon_status_mail_received.bmp" : "icon_status_mail_read.bmp"));
            if ((m.Type & 6) != 0) {
                w.DrawBitmap(216, 139 + RowPitch * i, Rx + "shadow_item_on.bmp");
                w.DrawBitmap(219, 127 + RowPitch * i, Rx + ((m.Type & 6) == 6 ? "icon_zeny_n_item.bmp" : (m.Type & 2) != 0 ? "icon_zeny.bmp" : "icon_item.bmp"));
            }

            DrawExpiry(w, i, m.ExpireDateTime, returned);
            bool general = m.OpenType == 0 && bucket != 3;
            Color32 titleColor = general ? (m.IsRead ? new Color32(0xe6, 0xc8, 0xc8, 255) : new Color32(0xff, 0, 0, 255))
                : (m.IsRead ? new Color32(0xc8, 0xc8, 0xc8, 255) : Black);
            // system mails carry message-table tags (<MSG>3455</MSG> = "Officer")
            string title = Clip(w.FontFor(0, 12), RoClassicUiHost.ResolveMsgTags(m.Title), 140);
            string sender = Clip(w.FontFor(0, 11), RoClassicUiHost.ResolveMsgTags(m.SenderName), 140);
            bool overTitle = Inside(mouse, 69, y, w.FontFor(0, 12).Measure(title) + 2, 14);
            bool overSender = Inside(mouse, 71, y + 20, w.FontFor(0, 11).Measure(sender) + 2, 13);
            w.TextOut(70, y + 1, title, 0, 12, overTitle ? HoverColor : titleColor);
            w.TextOut(72, y + 21, sender, 0, 11, overSender ? HoverColor : SenderColor);
            w.DrawBitmap(17, 128 + RowPitch * i, Rx + (checks[i] ? "checkbox_on.bmp" : "checkbox_off.bmp"));
            if (general) w.DrawBitmap(186, 131 + RowPitch * i, Rx + "btn_return_" + (Inside(mouse, 186, 131 + RowPitch * i, 42, 20) ? "over" : "out") + ".bmp");
        }

        /// <summary>FUN_0076d1f0: days (0xb24) / hours (0xb23) / minutes (0xb22) left; returned rows add 0xb25.</summary>
        private static void DrawExpiry(RoClassicWindow w, int i, int seconds, bool returned) {
            if (seconds <= 0) return;
            string text = seconds >= 86400 ? Msg(0xb24, "%d day").Replace("%d", Num(seconds / 86400))
                : seconds >= 3600 ? Msg(0xb23, "%d hour").Replace("%d", Num(seconds / 3600))
                : Msg(0xb22, "%d minute").Replace("%d", Num(seconds / 60));
            if (returned) {
                w.TextOut(260, 125 + RowPitch * i, Msg(0xb25, "Returned"), 0, 11, ReturnedColor);
                w.TextOut(260, 138 + RowPitch * i, text, 0, 11, ReturnedColor);
                return;
            }

            RoClassicGdiFont bold = w.FontFor(0, 11, true);
            w.TextOutPlain(260, 130 + RowPitch * i, text, bold, ExpireColor);
        }

        private void InboxPressed(Vector2Int local) {
            if (Inside(local, inbox.Width - 14, 3, 11, 11)) {
                CloseInbox();
                return;
            }

            foreach (var tab in Tabs) {
                if (!Inside(local, tab.X, 86, 68, 28)) continue;
                // 0xd3: the bucket, the checks and the page reset
                bucket = tab.Bucket;
                Array.Clear(checks, 0, checks.Length);
                page = 0;
                inbox.Invalidate();
                return;
            }

            if (Inside(local, 248, 20, 24, 26)) {
                // 0x19b: CZ_REQ_REFRESH_RODEX with the three upper ids
                _ = session.BeginLegacyRefreshRodex(Upper(0), Upper(2), Upper(1));
                return;
            }

            if (Inside(local, 277, 18, 24, 26)) {
                OpenWrite(string.Empty);
                return;
            }

            if (Inside(local, 22, 68, 51, 12) || Inside(local, 73, 68, 51, 12)) {
                searchBySender = local.x >= 73;
                inbox.Invalidate();
                return;
            }

            if (Inside(local, 264, 65, 32, 18)) {
                RunSearch();
                return;
            }

            List<RoRodexMailSnapshot> list = BucketRows();
            for (int i = 0; i < Rows; i++) {
                int k = page * Rows + i, y = 121 + RowPitch * i;
                if (k >= list.Count) break;
                RoRodexMailSnapshot m = list[k];
                if (Inside(local, 17, 128 + RowPitch * i, 14, 14)) {
                    checks[i] = !checks[i];
                    inbox.Invalidate();
                    return;
                }

                RoClassicGdiFont titleFont = inbox.FontFor(0, 12), senderFont = inbox.FontFor(0, 11);
                if (Inside(local, 69, y, titleFont.Measure(Clip(titleFont, RoClassicUiHost.ResolveMsgTags(m.Title), 140)) + 2, 14)) {
                    // 0x13f → mode 0xc2 → CZ_REQ_READ_RODEX
                    _ = session.BeginLegacyReadRodex(m.OpenType, m.MailId);
                    return;
                }

                if (m.OpenType == 0 && bucket == 0 && Inside(local, 71, y + 20, senderFont.Measure(Clip(senderFont, RoClassicUiHost.ResolveMsgTags(m.SenderName), 140)) + 2, 13)) {
                    // 0x13e → mode 0x10c: reply to the sender
                    OpenWrite(m.SenderName);
                    return;
                }
            }

            if (Inside(local, 4, 392, 70, 20)) {
                RoClassicMessageBox.Show(Msg(0x164, "Are you sure that you want to delete it?"), RoClassicMessageBox.TypeOkCancel, true, result => {
                    if (result != RoClassicMessageBox.IdYes) return;
                    // FUN_00769140: every record without zeny / items
                    foreach (RoRodexMailSnapshot m in BucketRows()) {
                        if ((m.Type & 6) == 0) _ = session.BeginLegacyDeleteRodex(m.OpenType, m.MailId);
                    }
                });
                return;
            }

            if (Inside(local, 84, 392, 70, 20)) {
                // 0x220 retrieve all / 0x21f retrieve selected (FUN_0076b210): bit 4 items, bit 2 zeny
                bool any = Array.Exists(checks, c => c);
                for (int k = 0; k < list.Count; k++) {
                    int row = k - page * Rows;
                    if (any && (row < 0 || row >= Rows || !checks[row])) continue;
                    RoRodexMailSnapshot m = list[k];
                    if ((m.Type & 4) != 0) _ = session.BeginLegacyRetrieveRodexItems(m.OpenType, m.MailId);
                    if ((m.Type & 2) != 0) _ = session.BeginLegacyRetrieveRodexZeny(m.OpenType, m.MailId);
                }

                return;
            }

            if (Inside(local, 255, 390, 24, 24) && page > 0) {
                page--;
                Array.Clear(checks, 0, checks.Length);
                inbox.Invalidate();
            } else if (Inside(local, 279, 390, 24, 24) && (page + 1) * Rows < list.Count) {
                page++;
                Array.Clear(checks, 0, checks.Length);
                inbox.Invalidate();
            }
        }

        private ulong Upper(byte openType) {
            ulong id = 0;
            foreach (RoRodexMailSnapshot m in rodex.Mails) {
                if (m.OpenType == openType && m.MailId > id) id = m.MailId;
            }

            return id;
        }

        /// <summary>0x13d (FUN_0076daf0 / FUN_0076d800): the received rows whose title / sender contain the text.</summary>
        private void RunSearch() {
            string text = searchEdit?.Text ?? string.Empty;
            searchResults.Clear();
            foreach (RoRodexMailSnapshot m in rodex.Mails) {
                string field = RoClassicUiHost.ResolveMsgTags(searchBySender ? m.SenderName : m.Title);
                if (text.Length == 0 || field.IndexOf(text, StringComparison.Ordinal) >= 0) searchResults.Add(m);
            }

            bucket = 3;
            page = 0;
            Array.Clear(checks, 0, checks.Length);
            inbox?.Invalidate();
        }

        // ================================================================ 0x109

        private RoClassicEditField body;

        /// <summary>300 × 400 at ((W − 640) / 2 + 120, (H − 480) / 2 + 120), bg_rodex_read.bmp, the read-only body editor.</summary>
        private void OpenContents() {
            if (contents == null) {
                var layout = new RoClassicWindowLayout { Class = "UIRodexContentsWnd", WindowId = 0x109, Width = 300, Height = 400 };
                RoClassicFrames.Bitmap(layout.Ops, Rx + "bg_rodex_read.bmp", 0, 0);
                contents = new RoClassicWindow(system, layout) { Visible = false };
                contents.DrawDynamic += DrawContents;
                contents.Pressed += (w, local) => ContentsPressed(local);
                contents.Wheeled += (w, local, delta) => body.ScrollLines(delta > 0 ? -3 : 3);
                contents.EscClose = () => {
                    CloseContents();
                    return true;
                };
                body = new RoClassicEditField {
                    Rect = new RectInt(15, 81, 267, 192), Background = BodyFill, TextColor = BodyText, MaxBytes = 1000, Multiline = true,
                    ReadOnly = true, ScrollBarWidth = 13
                };
                contents.AddEditField(body);
            }

            body.SetText(RoClassicUiHost.ResolveMsgTags(rodex.Read.Body));
            Vector2Int client = system.ClientSize;
            contents.MoveTo((client.x - 640) / 2 + 120, (client.y - 480) / 2 + 120);
            contents.Visible = true;
            system.Windows.BringToFront(contents);
            contents.Invalidate();
        }

        private void CloseContents() {
            if (contents != null) contents.Visible = false;
            shownRead = 0;
            session?.CloseRodexReadLocally();
        }

        private RoRodexMailSnapshot CurrentMail() {
            foreach (RoRodexMailSnapshot m in rodex.Mails) {
                if (m.MailId == rodex.Read.MailId) return m;
            }

            return default;
        }

        /// <summary>OnDraw 0x766020: sender (8, 29), title (9, 52), zeny icon / text, five shadow slots from x 26 every 34 at y 319.</summary>
        private void DrawContents(RoClassicWindow w) {
            Vector2Int mouse = Local(w);
            RoRodexReadSnapshot read = rodex.Read;
            RoRodexMailSnapshot m = CurrentMail();
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            w.DrawBitmap(286, 3, Basic + "sys_close_off.bmp");
            w.TextOut(8, 29, RoClassicUiHost.ResolveMsgTags(m.SenderName), 0, 12, Black);
            w.TextOut(9, 52, RoClassicUiHost.ResolveMsgTags(m.Title), 0, 12, Black);
            DrawRodexScroll(w, body, 15 + 262, 81);
            w.DrawBitmap(19, 342, Rx + "icon_zeny.bmp");
            w.TextOut(50, 350, RoClassicItemText.Commas(read.Zeny), 0, 11, Black);
            RoZcInventoryItem[] items = read.Items ?? Array.Empty<RoZcInventoryItem>();
            for (int n = 0; n < 5; n++) {
                w.DrawBitmap(26 + 34 * n, 319, Rx + (n < items.Length ? "shadow_item_on.bmp" : "shadow_item_off.bmp"));
                if (n < items.Length) RoClassicItemText.DrawIcon(w, 30 + 34 * n, 309, ToItem(items[n]), metadata, items[n].Count);
            }

            if (items.Length > 0) w.DrawBitmap(252, 312, Rx + "btn_receive_" + (Inside(mouse, 252, 312, 26, 20) ? "over" : "out") + ".bmp");
            if (read.Zeny > 0) w.DrawBitmap(252, 345, Rx + "btn_receive_" + (Inside(mouse, 252, 345, 26, 20) ? "over" : "out") + ".bmp");
            bool general = read.OpenType == 0 && (m.Type & 8) == 0;
            if (general) {
                DrawBasicButton(w, 224, 376, 70, Msg(0xdd7, "Reply"), mouse);
                DrawBasicButton(w, 224, 29, 70, Msg(0xddc, "Return"), mouse);
            }

            DrawBasicButton(w, 6, 376, 50, Msg(0xdd6, "Delete"), mouse);
        }

        private void ContentsPressed(Vector2Int local) {
            RoRodexReadSnapshot read = rodex.Read;
            RoRodexMailSnapshot m = CurrentMail();
            if (Inside(local, 286, 3, 11, 11)) {
                CloseContents();
                return;
            }

            if (read.Items != null && read.Items.Length > 0 && Inside(local, 252, 312, 26, 20)) {
                _ = session.BeginLegacyRetrieveRodexItems(read.OpenType, read.MailId);
                return;
            }

            if (read.Zeny > 0 && Inside(local, 252, 345, 26, 20)) {
                _ = session.BeginLegacyRetrieveRodexZeny(read.OpenType, read.MailId);
                return;
            }

            bool general = read.OpenType == 0 && (m.Type & 8) == 0;
            if (general && Inside(local, 224, 376, 70, 20)) {
                OpenWrite(m.SenderName);
                return;
            }

            if (Inside(local, 6, 376, 50, 20)) {
                // 0xcf: 0x164, then mode 0xc4 unless zeny / items remain
                RoClassicMessageBox.Show(Msg(0x164, "Are you sure that you want to delete it?"), RoClassicMessageBox.TypeOkCancel, true, result => {
                    if (result != RoClassicMessageBox.IdYes || (CurrentMail().Type & 6) != 0) return;
                    _ = session.BeginLegacyDeleteRodex(read.OpenType, read.MailId);
                    CloseContents();
                });
            }
        }

        private static RoItemInstance ToItem(RoZcInventoryItem item) =>
            RoItemInstance.CreateRegular(item.BagIndex, unchecked((int) item.ItemId), item.Count, item.IsIdentified, itemTypeRaw: item.ItemTypeRaw);

        // ================================================================ 0x108

        private RoClassicEditField receiver, title, mailBody, zeny;
        private bool composeValidated;

        /// <summary>Opens the compose window for a receiver (the messenger / party "Send a mail").</summary>
        public static void OpenWriteTo(string name) => instance?.OpenWrite(name);

        /// <summary>Mode 0x10c: CZ_REQ_OPEN_WRITE_RODEX with the receiver (a name of 24 bytes or more gets 0x213 instead).</summary>
        private void OpenWrite(string name) {
            if (session == null) return;
            if (!string.IsNullOrEmpty(name) && name.Length >= 24) {
                RoClassicMessageBox.Show(Msg(0x213, "The name is too long."), RoClassicMessageBox.TypeOk, true, _ => { });
                return;
            }

            _ = session.BeginLegacyOpenWriteRodex(name ?? string.Empty);
        }

        /// <summary>
        /// UIRodexComposeWnd after the oracle capture (300 × 400 at ((W − 640) / 2 + 120, (H − 480) / 2 + 120)): receiver edit
        /// (8, 26, 150, 18), Validate 0xd1, the three status labels, title edit (6, 50, 155, 14), body UIRodexEditCtrl3 0x1e7
        /// (15, 81, 267, 192) #BDCEF7 / #313131, 1000 bytes, Zeny edit (45, 348, 84, 14), Send 0x155.
        /// </summary>
        private void OpenCompose() {
            if (compose == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIRodexComposeWnd").text);
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class.StartsWith("UIEditCtrl", StringComparison.Ordinal) || child.Class == "UIRodexEditCtrl3" || child.Class == "UIStaticText") child.Visible = false;
                }

                compose = new RoClassicWindow(system, layout) { Visible = false };
                compose.DrawDynamic += DrawCompose;
                compose.Clicked += child => {
                    if (child.Id == CloseId) CloseCompose();
                    else if (child.Id == ValidateId) Validate();
                    else if (child.Id == SendId) Send();
                };
                compose.Wheeled += (w, local, delta) => {
                    if (mailBody.LineCount > mailBody.VisibleLines) mailBody.ScrollLines(delta > 0 ? -3 : 3);
                };
                compose.Dropped += (w, local, payload) => {
                    if (payload is RoClassicItemPayload carried && carried.Item != null) AddAttachment(carried.Item);
                };
                compose.DragSourceAt = local => AttachmentAt(local);
                compose.EscClose = () => {
                    CloseCompose();
                    return true;
                };
                receiver = new RoClassicEditField { Rect = new RectInt(9, 27, 148, 16), MaxBytes = 23 };
                receiver.Submitted += _ => Validate();
                receiver.Changed += _ => composeValidated = false;
                title = new RoClassicEditField { Rect = new RectInt(6, 50, 155, 14), MaxBytes = 50 };
                mailBody = new RoClassicEditField {
                    Rect = new RectInt(15, 81, 267, 192), Background = BodyFill, TextColor = BodyText, MaxBytes = 1000, Multiline = true
                };
                mailBody.Changed += _ => compose.Invalidate();
                zeny = new RoClassicEditField { Rect = new RectInt(45, 348, 84, 14), Background = White, MaxBytes = 10 };
                zeny.Changed += _ => compose.Invalidate();
                compose.AddEditField(receiver);
                compose.AddEditField(title);
                compose.AddEditField(mailBody);
                compose.AddEditField(zeny);
            }

            RoRodexComposeSnapshot c = rodex.Compose;
            receiver.SetText(c.ReceiverName ?? string.Empty);
            title.SetText(string.Empty);
            mailBody.SetText(string.Empty);
            zeny.SetText("0");
            composeValidated = false;
            Vector2Int client = system.ClientSize;
            compose.MoveTo((client.x - 640) / 2 + 120, (client.y - 480) / 2 + 120);
            compose.Visible = true;
            system.Windows.BringToFront(compose);
            mailBody.Focus();
            compose.Invalidate();
        }

        private void SyncCompose() {
            if (compose == null || !compose.Visible) return;
            RoRodexComposeSnapshot c = rodex.Compose;
            if (c.ReceiverValidated && !composeValidated) {
                composeValidated = true;
                if (!receiver.HasFocus) receiver.SetText(c.ReceiverName);
            }

            compose.Invalidate();
        }

        /// <summary>Validate 0xd1: a nonempty receiver below 24 bytes → the name check (0x0a13 / 0x0b97).</summary>
        private void Validate() {
            string name = receiver.Text.Trim();
            if (name.Length == 0 || name.Length >= 24 || session == null) return;
            _ = session.BeginLegacyCheckRodexReceiver(name);
        }

        /// <summary>Send 0x155 (vf37 0x764e80): a validated receiver, a title of 4..50 bytes, Zeny plus fee covered → mode 0xc0.</summary>
        private void Send() {
            RoRodexComposeSnapshot c = rodex.Compose;
            if (session == null || !c.ReceiverValidated) return;
            string t = title.Text;
            if (t.Length < 4 || t.Length > 50) return;
            long amount = ParseZeny();
            string sender = session.SelectedCharacterName;
            _ = session.BeginLegacySendRodexMail(c.ReceiverName, sender, amount, t, mailBody.Text, c.ReceiverCharacterId);
            // the send path deletes 0x108 at once (manager teardown → CZ_REQ_CANCEL_WRITE_RODEX)
            CloseCompose();
        }

        private long ParseZeny() => long.TryParse(zeny.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) && v > 0 ? v : 0;

        private void CloseCompose() {
            if (compose != null) compose.Visible = false;
            composeShown = false;
            _ = session?.BeginLegacyCancelWriteRodex();
            session?.CloseRodexComposeLocally();
        }

        /// <summary>
        /// OnDraw 0x7641a0: the receiver box (or the validated name and the three red labels "Lv%d", the class, the char id at
        /// (172, 25 / 37 / 49)), the body counter (258 / 261, 276, font 0/10, #446ED6), the weight "%d" / "/2000" (244 / 245, 316),
        /// the fee from 2500 per item + 2 % of the Zeny, the five attachment slots.
        /// </summary>
        private void DrawCompose(RoClassicWindow w) {
            RoRodexComposeSnapshot c = rodex.Compose;
            RoClassicChildLayout validate = w.Layout.Children.Find(ch => ch.Id == ValidateId);
            if (c.ReceiverValidated) {
                // FUN_00763180: the input hides, Validate goes, the name and the three red labels show
                w.SetChildVisible(validate, false);
                w.TextOut(10, 29, c.ReceiverName, 0, 12, Black);
                receiver.Visible = false;
                w.TextOut(172, 25, "Lv" + Num(c.ReceiverLevel), 0, 11, StatusRed);
                w.TextOut(172, 37, JobName(c.ReceiverJob), 0, 11, StatusRed);
                w.TextOut(172, 49, Num((int) c.ReceiverCharacterId), 0, 11, StatusRed);
            } else {
                w.SetChildVisible(validate, true);
                receiver.Visible = true;
                w.FillRect(8, 26, 150, 18, new Color32(0xa6, 0xa6, 0xa6, 255));
                w.FillRect(9, 27, 148, 16, White);
            }

            // the title edit's white field with its "TITLE" text while empty
            w.FillRect(6, 50, 155, 14, White);
            if (title.Text.Length == 0 && !title.HasFocus) w.TextOut(9, 52, "TITLE", 0, 12, Black);
            DrawRodexScroll(w, mailBody, 15 + 262, 81);
            w.TextOutRight(258, 276, Num(mailBody.Text.Length), 0, 10, CounterColor);
            w.TextOut(261, 276, "/ 1000", 0, 10, CounterColor);
            w.TextOutRight(244, 316, Num(c.Weight), 0, 11, Gray73);
            w.TextOut(245, 316, "/2000", 0, 11, Gray73);
            long amount = ParseZeny();
            RoRodexComposeAttachmentSnapshot[] items = c.Attachments ?? Array.Empty<RoRodexComposeAttachmentSnapshot>();
            long fee = 2500L * items.Length + amount * 2 / 100;
            w.TextOut(135, 350, "/", 0, 11, Gray73);
            w.TextOutPlain(144, 350, RoClassicItemText.Commas(amount + fee) + " Zeny", w.FontFor(0, 11, true), Gray73);
            w.TextOutRight(104, 382, RoClassicItemText.Commas(fee), 0, 11, Black);
            for (int n = 0; n < 5; n++) {
                w.DrawBitmap(26 + 34 * n, 319, Rx + (n < items.Length ? "shadow_item_on.bmp" : "shadow_item_off.bmp"));
                if (n < items.Length) RoClassicItemText.DrawIcon(w, 30 + 34 * n, 309, ToItem(items[n].Item), metadata, items[n].Amount);
            }
        }

        /// <summary>Msg 0x26 (drop of an inventory item): one item at once, a stack through UIItemDropCntWnd 0x0f → mode 0xc3.</summary>
        private void AddAttachment(RoItemInstance item) {
            if (session == null || item.IsEquipped) return;
            if (item.Count <= 1) {
                _ = session.BeginLegacyAddRodexItem(item.BagIndex, 1);
                return;
            }

            RoClassicCountWindow.Show(item.GetFullDisplayName(metadata), item.Count, n => {
                if (n >= 1) _ = session.BeginLegacyAddRodexItem(item.BagIndex, (int) Math.Min(n, item.Count));
            });
        }

        /// <summary>Left-down on an occupied slot (y 309..334, x 30 + 34i .. 54 + 34i) starts drag source 0xf.</summary>
        private IRoClassicDragPayload AttachmentAt(Vector2Int local) {
            if (local.y < 309 || local.y >= 334) return null;
            RoRodexComposeAttachmentSnapshot[] items = rodex.Compose.Attachments ?? Array.Empty<RoRodexComposeAttachmentSnapshot>();
            for (int i = 0; i < items.Length && i < 5; i++) {
                if (local.x <= 30 + 34 * i || local.x >= 54 + 34 * i) continue;
                RoItemInstance item = ToItem(items[i].Item);
                return new RoClassicRodexPayload {
                    BagIndex = items[i].BagIndex, Amount = items[i].Amount, Name = item.GetFullDisplayName(metadata),
                    IconPath = RoClassicItemText.IconPath(item, metadata)
                };
            }

            return null;
        }

        /// <summary>Compose Msg 0x68 (the attachment dropped on the inventory) → mode 0x126; a stack asks the amount first.</summary>
        internal static void RemoveAttachment(RoClassicRodexPayload payload) {
            RoClassicRodex r = instance;
            if (r?.session == null || payload == null) return;
            if (payload.Amount <= 1) {
                _ = r.session.BeginLegacyRemoveRodexItem(payload.BagIndex, 1);
                return;
            }

            RoClassicCountWindow.Show(payload.Name, payload.Amount, n => {
                if (n >= 1) _ = r.session.BeginLegacyRemoveRodexItem(payload.BagIndex, (int) Math.Min(n, payload.Amount));
            });
        }

        // ================================================================ shared drawing

        /// <summary>UIRodexScrollBar (vf20 0x7649b0): a 5 px white track, the arrows at the ends, a #E9E9E9 thumb of at least 5 px.</summary>
        private static void DrawRodexScroll(RoClassicWindow w, RoClassicEditField edit, int x, int y) {
            if (edit == null || edit.LineCount <= edit.VisibleLines) return;
            int h = edit.Rect.height;
            w.FillRect(x, y, 5, h, White);
            w.DrawBitmap(x, y, Rx + "scrollbar_arrow_upper.bmp");
            w.DrawBitmap(x, y + h - 5, Rx + "scrollbar_arrow_down.bmp");
            int track = h - 10, total = edit.LineCount, visible = edit.VisibleLines;
            int thumb = Math.Max(5, track * visible / Math.Max(1, total));
            int range = Math.Max(1, total - visible);
            int ty = y + 5 + (track - thumb) * edit.FirstVisible / range;
            w.FillRect(x, ty, 5, thumb, new Color32(0xe9, 0xe9, 0xe9, 255));
        }

        /// <summary>
        /// UIBasicButton (vf20 0x7bc8a0): btn_out / btn_over _left / _mid / _right, the text centred in font 0/12 over its
        /// (1,1) shadow (#D7D7D7, hover #96A7C7).
        /// </summary>
        private static void DrawBasicButton(RoClassicWindow w, int x, int y, int width, string text, Vector2Int mouse) {
            bool over = Inside(mouse, x, y, width, 20);
            string state = over ? "over" : "out";
            w.DrawBitmap(x, y, Basic + "btn_" + state + "_left.bmp");
            for (int px = x + 6; px < x + width - 6; px += 6) w.DrawBitmap(px, y, Basic + "btn_" + state + "_mid.bmp");
            w.DrawBitmap(x + width - 6, y, Basic + "btn_" + state + "_right.bmp");
            RoClassicGdiFont font = w.FontFor(0, 12);
            int tx = x + (width - font.Measure(text)) / 2, ty = y + 3;
            w.TextOutPlain(tx + 1, ty + 1, text, font, over ? new Color32(0x96, 0xa7, 0xc7, 255) : new Color32(0xd7, 0xd7, 0xd7, 255));
            w.TextOutPlain(tx, ty, text, font, Black);
        }

        /// <summary>FUN_009b2e60: the longest prefix within the width, "..." appended when cut.</summary>
        private static string Clip(RoClassicGdiFont font, string text, int width) {
            text = text ?? string.Empty;
            if (font.Measure(text) <= width) return text;
            for (int n = text.Length - 1; n > 0; n--) {
                string cut = text.Substring(0, n) + "...";
                if (font.Measure(cut) <= width) return cut;
            }

            return "...";
        }

        private static void WrapPixels(RoClassicGdiFont font, string text, int width, List<(int, int)> lines) {
            lines.Clear();
            int start = 0, last = 0;
            for (int i = 1; i <= text.Length; i++) {
                string line = StripCodes(text.Substring(start, i - start));
                if (font.Measure(line) > width && i - 1 > start) {
                    lines.Add((start, i - 1));
                    start = i - 1;
                }

                last = i;
            }

            if (start < last) lines.Add((start, last));
        }

        private static string StripCodes(string s) {
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == '^' && i + 6 < s.Length) {
                    i += 6;
                    continue;
                }

                sb.Append(s[i]);
            }

            return sb.ToString();
        }

        private Vector2Int Local(RoClassicWindow w) =>
            system.Windows.HoverWindow == w ? system.Windows.Mouse - w.Position : new Vector2Int(-1000, -1000);

        private static bool Inside(Vector2Int p, int x, int y, int w, int h) => p.x >= x && p.y >= y && p.x < x + w && p.y < y + h;

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static RoJobMetadataTable jobs;

        private static string JobName(int job) {
            if (jobs == null) {
                var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
                Ragnarok.Assets.RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
                if (assets != null && assets.IsInitialized) jobs = RoJobMetadataTable.Load(assets);
            }

            if (jobs != null && jobs.TryGetJob(job, out RoJobMetadata meta) && meta != null) {
                string formatted = RoJobMetadata.FormatJobName(meta.ConstantName);
                if (!string.IsNullOrEmpty(formatted)) return formatted;
            }

            return RoJobMetadataTable.DefaultName(job) ?? Num(job);
        }

        /// <summary>The sweep console: inbox | tab n | read row | check row | next | prev | refresh | write name | reply |
        /// take items|zeny | delete | send receiver|title|body|zeny | validate name | state.</summary>
        public static string Sweep(string command) {
            RoClassicRodex r = Ensure();
            if (r == null || r.session == null) return "no classic ui or session yet";
            string[] parts = command.Trim().Split(' ');
            int arg = parts.Length > 1 && int.TryParse(parts[1], out int v) ? v : 0;
            switch (parts[0]) {
                case "inbox":
                    ToggleInbox();
                    break;
                case "tab":
                    r.InboxPressed(new Vector2Int(Array.Find(Tabs, t => t.Bucket == arg).X + 5, 90));
                    break;
                case "read": {
                    List<RoRodexMailSnapshot> list = r.BucketRows();
                    int k = r.page * Rows + arg;
                    if (k < list.Count) _ = r.session.BeginLegacyReadRodex(list[k].OpenType, list[k].MailId);
                    break;
                }
                case "check":
                    r.InboxPressed(new Vector2Int(20, 130 + RowPitch * arg));
                    break;
                case "next":
                    r.InboxPressed(new Vector2Int(282, 395));
                    break;
                case "prev":
                    r.InboxPressed(new Vector2Int(258, 395));
                    break;
                case "refresh":
                    r.InboxPressed(new Vector2Int(250, 25));
                    break;
                case "write":
                    r.OpenWrite(parts.Length > 1 ? parts[1] : string.Empty);
                    break;
                case "take":
                    r.ContentsPressed(arg == 1 || (parts.Length > 1 && parts[1] == "zeny") ? new Vector2Int(255, 350) : new Vector2Int(255, 315));
                    break;
                case "delete":
                    r.ContentsPressed(new Vector2Int(10, 380));
                    break;
                case "validate":
                    if (r.receiver != null && parts.Length > 1) {
                        r.receiver.SetText(parts[1]);
                        r.Validate();
                    }

                    break;
                case "attach":
                    // attach <bag index> [amount]: an inventory item into the open mail (as its drop with the amount window answered)
                    if (r.session != null && parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bag)) {
                        int amount = parts.Length > 2 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int a) ? a : 1;
                        _ = r.session.BeginLegacyAddRodexItem(bag, amount);
                    }

                    break;
                case "send": {
                    // send title|body|zeny
                    string[] pieces = command.Trim().Substring(4).Trim().Split('|');
                    if (r.title == null) break;
                    r.title.SetText(pieces[0]);
                    if (pieces.Length > 1) r.mailBody.SetText(pieces[1]);
                    if (pieces.Length > 2) r.zeny.SetText(pieces[2]);
                    r.Send();
                    break;
                }
                case "close":
                    r.CloseCompose();
                    break;
            }

            RoRodexSnapshot s = r.session.RodexSnapshot();
            return "open=" + s.IsOpen + " mails=" + s.Mails.Length + " bucket=" + r.bucket + " page=" + r.page + " read=" + s.Read.MailId +
                " readItems=" + (s.Read.Items?.Length ?? 0) + " readZeny=" + s.Read.Zeny + " compose=" + s.Compose.IsOpen + " receiver=" + s.Compose.ReceiverName +
                " validated=" + s.Compose.ReceiverValidated + " attachments=" + (s.Compose.Attachments?.Length ?? 0) + " ack=" + s.AckSerial + "/" + s.AckKind + "/" + s.AckResult +
                " inbox=" + (r.inbox != null && r.inbox.Visible) + " contents=" + (r.contents != null && r.contents.Visible) + " composeWnd=" + (r.compose != null && r.compose.Visible) +
                " icon=" + (r.icon != null && r.icon.Visible) + " last=" + s.LastMessage;
        }
    }
}
