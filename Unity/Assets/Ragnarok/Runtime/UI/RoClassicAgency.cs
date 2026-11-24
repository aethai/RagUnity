using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The Adventurer Agency (adventurer-agency-spec): the board UIAdvenPartyBoardWnd 0x144 (Ctrl+Z; Custom has no menu
    /// icon for it), the leader's UIRegisterPartyWnd 0x143, the listing details UIApplyForPartyWnd 0x159 and the leader's
    /// UIRequestJoinPartyWnd 0x146. Listing, search, register, cancel and the join look-up are web API calls
    /// (/party/list, /party/search, /party/add, /party/del, /party/get, /party/info); the join itself is
    /// CZ 0x0ae6 → ZC 0x0ae7 (to the leader) → CZ 0x0af8 → ZC 0x0afa (the chat lines). Custom's api-server quirks stay:
    /// search answers nothing, /party/info has no data ("No party can be found.") although the leader gets the request.
    /// </summary>
    public sealed class RoClassicAgency : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Ag = Ui + "uiadventureragency\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string Prefs = "ro.ui.classic.agency.";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private static readonly Color32 Hint = new Color32(0x64, 0x64, 0x64, 255);
        private static readonly Color32 BoardEdit = new Color32(242, 242, 242, 255);
        private static readonly Color32 RegisterEdit = new Color32(232, 232, 232, 255);
        private static readonly Color32 Selected = new Color32(0x70, 0x9f, 0xed, 255);
        private static readonly int[] PurposeMsg = { 0xd31, 0xd20, 0xd21, 0xd22, 0xd23, 0xd24, 0xd25 };
        private static readonly string[] PurposeText = { "All", "Quest", "Field", "Dungeon", "MD", "Paradise", "Other" };
        private static readonly string[] RoleIcon = { "def", "heal", "atk", "buf" };
        private static readonly (int Msg, string Text)[] RoleTip = { (0xd5c, "Tank"), (0xda3, "Healer"), (0xd74, "Dealer"), (0xdb4, "Support") };

        private static RoClassicAgency instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        /// <summary>CAdvenPartyInfo: roles Tank / Healer / Dealer / Support, levels, purpose, memo, the leader.</summary>
        private sealed class Listing {
            public readonly bool[] Roles = new bool[4];
            public int MinLv, MaxLv = 999, Type;
            public string Memo = string.Empty, Name = string.Empty;
            public uint Aid, Gid;

            public Listing Copy() {
                var c = (Listing) MemberwiseClone();
                Array.Copy(Roles, c.Roles, 4);
                return c;
            }
        }

        /// <summary>A button drawn by hand: its rect, state bitmaps (normal, over, press, disable) or a basic text button.</summary>
        private sealed class Button {
            public RectInt Rect;
            public string Normal, Over, Press, Disable, Text;
            public bool Basic, Enabled = true, Shown = true;
            public Action Click;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private Button pressed;

        // ---- the board 0x144
        private RoClassicWindow board;
        private readonly List<Button> boardButtons = new List<Button>();
        private RoClassicEditField minEdit, maxEdit, searchEdit;
        private readonly bool[] filter = new bool[4];
        private int purpose, page = 1, pages = 1;
        private bool searchMode, registered;
        private readonly Listing[] rows = new Listing[10];
        private Listing myListing;
        private Button prev, next, refresh, myRow, registerRow, cancelRecruit;
        private readonly Button[] rowButtons = new Button[10];

        // ---- the register / edit form 0x143
        private RoClassicWindow form;
        private readonly List<Button> formButtons = new List<Button>();
        private RoClassicEditField formMin, formMax, formNote;
        private readonly bool[] formRoles = new bool[4];
        private int formPurpose;
        private string formLeader = string.Empty;
        private Button formSubmit;

        // ---- the details 0x159
        private RoClassicWindow details;
        private readonly List<Button> detailButtons = new List<Button>();
        private Listing shown;

        // ---- the leader's request 0x146
        private RoClassicWindow request;
        private readonly List<Button> requestButtons = new List<Button>();
        private readonly List<(string Name, uint Grid, uint Aid, short Job, short Level)> queue = new List<(string, uint, uint, short, short)>();
        private readonly HashSet<uint> blocked = new HashSet<uint>();
        private (string Name, uint Grid, uint Aid, short Job, short Level) asking;
        private bool block;

        // ---- the purpose combo (UIComboBox 0x7ce100) popup
        private RoClassicWindow combo;
        private Action<int> comboPick;
        private int comboHover = -1;

        private static RoClassicAgency Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicAgency { system = system };
                system.Register(instance);
            }

            instance.session = RoClassicEntryQueue.CurrentSession ?? instance.session;
            instance.chat = RoClassicChat.Instance;
            return instance;
        }

        // ================================================================ the board

        /// <summary>Ctrl+Z (hotkey action 57): if (!DeleteWindow(0x144)) MakeWindow(0x144).</summary>
        public static bool Toggle() {
            RoClassicAgency a = Ensure();
            if (a == null || a.session == null) return false;
            if (a.board != null && a.board.Visible) {
                a.CloseBoard();
                return true;
            }

            a.OpenBoard();
            return true;
        }

        private int BaseLevel => session != null ? session.BasicInfoSnapshot().Status?.BaseLevel ?? 1 : 1;

        private bool IsLeader {
            get {
                if (session == null) return false;
                RoPartyRuntimeSnapshot party = session.PartySnapshot();
                return party.IsInParty && party.LeaderAccountId != 0 && party.LeaderAccountId == session.LocalActorId;
            }
        }

        private void OpenBoard() {
            if (board == null) BuildBoard();
            Vector2Int c = system.ClientSize;
            int x = PlayerPrefs.GetInt(Prefs + "board.x", c.x / 2 - 240), y = PlayerPrefs.GetInt(Prefs + "board.y", c.y / 2 - 170);
            if (x < 0 || y < 0 || x + 480 > c.x || y + 340 > c.y) (x, y) = (c.x / 2 - 240, c.y / 2 - 170);
            board.MoveTo(x, y);
            // OnCreate: the defaults, then page 1
            minEdit.SetText(Math.Max(1, BaseLevel - 5).ToString(CultureInfo.InvariantCulture));
            maxEdit.SetText((BaseLevel + 5).ToString(CultureInfo.InvariantCulture));
            searchEdit.SetText(string.Empty);
            Array.Clear(filter, 0, 4);
            purpose = 0;
            page = pages = 1;
            searchMode = false;
            Array.Clear(rows, 0, rows.Length);
            myRow.Shown = registerRow.Shown = false;
            cancelRecruit.Shown = false;
            board.Visible = true;
            system.Windows.BringToFront(board);
            system.Windows.PushEsc(board);
            LoadPage(1);
            board.Invalidate();
        }

        private void CloseBoard() {
            if (board == null || !board.Visible) return;
            board.Visible = false;
            CloseCombo();
        }

        private Button Bitmap(List<Button> list, int x, int y, int w, int h, string normal, string over, string press, string disable, string text, Action click) {
            var b = new Button { Rect = new RectInt(x, y, w, h), Normal = normal, Over = over ?? normal, Press = press ?? over ?? normal, Disable = disable, Text = text, Click = click };
            list.Add(b);
            return b;
        }

        private Button BasicButton(List<Button> list, int x, int y, int width, string text, Action click) {
            var b = new Button { Rect = new RectInt(x, y, width, 20), Basic = true, Text = text, Click = click };
            list.Add(b);
            return b;
        }

        private RoClassicWindow MakeWindow(string cls, int id, int w, int h, List<Button> buttons, Action<RoClassicWindow> draw, Func<bool> esc, Action<RoClassicWindow> after = null) {
            var win = new RoClassicWindow(system, new RoClassicWindowLayout { Class = cls, WindowId = id, Width = w, Height = h }) {
                Visible = false, FrameDragArea = local => local.y < 17 && ButtonAt(buttons, local) == null,
            };
            win.DrawDynamic += wnd => {
                draw(wnd);
                DrawButtons(wnd, buttons);
                after?.Invoke(wnd);
            };
            win.Pressed += (wnd, local) => {
                pressed = ButtonAt(buttons, local);
                wnd.Invalidate();
            };
            win.Released += (wnd, local) => {
                Button b = pressed;
                pressed = null;
                wnd.Invalidate();
                if (b != null && b == ButtonAt(buttons, local)) b.Click?.Invoke();
            };
            win.EscClose = esc;
            return win;
        }

        private static Button ButtonAt(List<Button> buttons, Vector2Int p) {
            for (int i = buttons.Count - 1; i >= 0; i--) {
                Button b = buttons[i];
                if (b.Shown && b.Enabled && b.Rect.Contains(p)) return b;
            }

            return null;
        }

        private void DrawButtons(RoClassicWindow w, List<Button> buttons) {
            Vector2Int mouse = system.Windows.Mouse - w.Position;
            bool hoverWindow = system.Windows.HoverWindow == w;
            foreach (Button b in buttons) {
                if (!b.Shown) continue;
                bool over = hoverWindow && b.Enabled && b.Rect.Contains(mouse);
                if (b.Basic) {
                    RoClassicPreviewEquip.DrawBasicButton(w, b.Rect.x, b.Rect.y, b.Rect.width, b.Text, b == pressed && over);
                    continue;
                }

                string image = !b.Enabled && b.Disable != null ? b.Disable : b == pressed && over ? b.Press : over ? b.Over : b.Normal;
                if (image != null) w.DrawBitmap(b.Rect.x, b.Rect.y, image);
                if (!string.IsNullOrEmpty(b.Text)) {
                    int tw = w.FontFor(0, 12).Measure(b.Text);
                    w.TextOut(b.Rect.x + (b.Rect.width - tw) / 2, b.Rect.y + (b.Rect.height - 12) / 2, b.Text, 0, 12, b.Enabled ? Black : Hint);
                }
            }
        }

        private void BuildBoard() {
            board = MakeWindow("UIAdvenPartyBoardWnd", 0x144, 480, 340, boardButtons, DrawBoard, () => {
                CloseBoard();
                return true;
            }, DrawRowContents);
            board.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "board.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "board.y", w.Position.y);
            };
            board.RightClicked += (w, local) => {
                for (int i = 0; i < rows.Length; i++) {
                    if (rows[i] != null && rowButtons[i].Rect.Contains(local)) Whisper(rows[i]);
                }
            };
            minEdit = new RoClassicEditField { Rect = new RectInt(10, 24, 25, 14), MaxBytes = 3, Background = BoardEdit };
            maxEdit = new RoClassicEditField { Rect = new RectInt(46, 24, 25, 14), MaxBytes = 3, Background = BoardEdit };
            // its white background is drawn under the hint (MSI 0xd5b in #646464 while empty)
            searchEdit = new RoClassicEditField { Rect = new RectInt(238, 24, 116, 14), MaxBytes = 150 };
            searchEdit.Submitted += _ => Search(false);
            searchEdit.Changed += _ => board.Invalidate();
            board.AddEditField(minEdit);
            board.AddEditField(maxEdit);
            board.AddEditField(searchEdit);
            for (int i = 0; i < 4; i++) {
                int k = i;
                Bitmap(boardButtons, 160 + 18 * i, 23, 16, 16, null, null, null, null, null, () => {
                    filter[k] = !filter[k];
                    board.Invalidate();
                });
            }

            Bitmap(boardButtons, 76, 22, 76, 18, null, null, null, null, null, () => OpenCombo(board, 76, 40, p => purpose = p));
            Bitmap(boardButtons, 354, 22, 40, 18, Ag + "btn_search.bmp", Ag + "btn_search_over.bmp", Ag + "btn_search_press.bmp", Ag + "btn_search_disable.bmp",
                Msg(0xd26, "Search"), () => Search(false));
            Bitmap(boardButtons, 401, 22, 44, 18, Ag + "btn_initialize.bmp", Ag + "btn_initialize_over.bmp", Ag + "btn_initialize_press.bmp", Ag + "btn_initialize_disable.bmp",
                Msg(0xd27, "Reset"), Reset);
            refresh = Bitmap(boardButtons, 452, 22, 18, 18, Ag + "btn_refresh.bmp", Ag + "btn_refresh_over.bmp", Ag + "btn_refresh_press.bmp", Ag + "btn_refresh_disable.bmp",
                null, () => {
                    page = 1;
                    searchMode = false;
                    LoadPage(1);
                });
            myRow = Bitmap(boardButtons, 1, 49, 478, 22, Ag + "btn_recruit_on.bmp", Ag + "btn_recruit_on.bmp", Ag + "btn_recruit_press.bmp", Ag + "btn_recruit_disable.bmp",
                null, () => OpenForm(myListing));
            registerRow = Bitmap(boardButtons, 1, 49, 478, 22, Ag + "btn_recruit.bmp", Ag + "btn_recruit.bmp", Ag + "btn_recruit_press.bmp", Ag + "btn_recruit_disable.bmp",
                string.Empty, () => OpenForm(null));
            for (int i = 0; i < 10; i++) {
                int k = i;
                rowButtons[i] = Bitmap(boardButtons, 1, 73 + 24 * i, 478, 22, Ag + "btn_list.bmp", Ag + "btn_list_over.bmp", Ag + "btn_list_press.bmp", Ag + "btn_list_disable.bmp",
                    null, () => RowClicked(k));
            }

            prev = Bitmap(boardButtons, 199, 319, 16, 16, Ag + "btn_list_left.bmp", Ag + "btn_list_left.bmp", Ag + "btn_list_left.bmp", Ag + "btn_list_left_disable.bmp", null, () => {
                page--;
                if (searchMode) Search(true);
                else LoadPage(page);
            });
            next = Bitmap(boardButtons, 265, 319, 16, 16, Ag + "btn_list_right.bmp", Ag + "btn_list_right.bmp", Ag + "btn_list_right.bmp", Ag + "btn_list_right_disable.bmp", null, () => {
                page++;
                if (searchMode) Search(true);
                else LoadPage(page);
            });
            BasicButton(boardButtons, 428, 317, 42, Msg(0xbe5, "Close"), CloseBoard);
            string cancel = Msg(0xd2f, "Cancel Recruitment");
            int cw = system.Font(12).Measure(cancel) + 10;
            cancelRecruit = BasicButton(boardButtons, 428 - cw - 5, 317, cw, cancel, CancelRecruitment);
        }

        /// <summary>OnDraw 0x71c930 and the rows 0x71ccd0.</summary>
        private void DrawBoard(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Ag + "bg_agencylist.bmp");
            DrawTitle(w, Msg(0xdb0, "Adventurer's Agency"));
            w.TextOut(36, 25, "~", 0, 12, Black);
            // the purpose combo box and the role filters
            DrawCombo(w, 76, 22, 76, purpose);
            for (int i = 0; i < 4; i++) w.DrawBitmap(160 + 18 * i, 23, Ag + "btn_job_" + RoleIcon[i] + (filter[i] ? "_on.bmp" : "_off.bmp"));
            w.FillRect(238, 24, 116, 14, White);
            if (searchEdit.Text.Length == 0 && !searchEdit.HasFocus) w.TextOut(240, 25, Msg(0xd5b, "Enter search details"), 0, 12, Hint);
            for (int i = 0; i < rows.Length; i++) {
                rowButtons[i].Shown = rows[i] != null;
                rowButtons[i].Enabled = rows[i] != null;
            }

            // the page label "%d / %d" centred at y 319, the arrows beside it
            string label = page.ToString(CultureInfo.InvariantCulture) + " / " + pages.ToString(CultureInfo.InvariantCulture);
            int tw = w.FontFor(0, 12).Measure(label);
            w.TextOut(240 - tw / 2, 319, label, 0, 12, Black);
            prev.Rect.x = 240 - 16 - tw / 2 - 3;
            next.Rect.x = 240 + 3 + tw / 2;
            // the rows' contents come after the buttons: drawn by RowContents
            registerRow.Text = registered || !IsLeader ? string.Empty : Msg(0xd1c, "Register in the Adventurer's Agency");
            if (!IsLeader) registerRow.Text = string.Empty;
        }

        /// <summary>The rows' texts and role icons over their bitmaps (UIPartyInfoWnd 0x71ccd0).</summary>
        private void DrawRowContents(RoClassicWindow w) {
            for (int i = 0; i < rows.Length; i++) {
                if (rows[i] != null) DrawListing(w, rows[i], 1, 73 + 24 * i);
            }

            if (myRow.Shown && myListing != null) DrawListing(w, myListing, 1, 49);
        }

        private void DrawListing(RoClassicWindow w, Listing l, int x, int y) {
            w.TextOut(x + 11, y + 5, l.MinLv + "~" + l.MaxLv, 0, 12, Black);
            w.TextOut(x + 64, y + 5, PurposeName(l.Type), 0, 12, Black);
            for (int r = 0; r < 4; r++) w.DrawBitmap(x + 109 + 18 * r, y + 3, Ag + "btn_job_" + RoleIcon[r] + (l.Roles[r] ? "_on.bmp" : "_off.bmp"));
            w.TextOut(x + 192, y + 5, Cut(w, l.Name, 119), 0, 12, Black);
            w.TextOut(x + 315, y + 5, l.Memo, 0, 12, Black);
        }

        private static string PurposeName(int type) => type >= 0 && type < PurposeMsg.Length ? Msg(PurposeMsg[type], PurposeText[type]) : string.Empty;

        private static string Cut(RoClassicWindow w, string text, int width) {
            RoClassicGdiFont font = w.FontFor(0, 12);
            if (font.Measure(text) <= width) return text;
            int n = text.Length;
            while (n > 0 && font.Measure(text.Substring(0, n)) > width) n--;
            return text.Substring(0, n);
        }

        /// <summary>DrawTitle 0x82f2d0(1, text): the title in black over a white shadow at (18, 3).</summary>
        private static void DrawTitle(RoClassicWindow w, string title) {
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
        }

        private void DrawCombo(RoClassicWindow w, int x, int y, int width, int selected) {
            w.FillRect(x, y, width, 18, new Color32(0xbd, 0xbd, 0xbd, 255));
            w.FillRect(x + 1, y + 1, width - 2, 16, White);
            w.TextOut(x + 4, y + 3, PurposeName(selected), 0, 12, Black);
            w.DrawBitmap(x + width - 17, y + 1, Ag + "btn_dropdown_out.bmp");
        }

        /// <summary>0x719420(page): prev / next / refresh off, /party/list, and /party/get for a leader.</summary>
        private void LoadPage(int at) {
            prev.Enabled = next.Enabled = refresh.Enabled = false;
            board?.Invalidate();
            if (session == null) return;
            var fields = RoClassicWebApi.Common(session, true);
            fields.Add(("page", at.ToString(CultureInfo.InvariantCulture)));
            RoClassicWebApi.Post(session, "/party/list", fields, null, (ok, body) => ListArrived(ok, body));
            if (!IsLeader) return;
            RoClassicWebApi.Post(session, "/party/get", RoClassicWebApi.Common(session, true), null, (ok, body) => {
                JObject json = RoClassicWebApi.Json(body);
                if (!RoClassicWebApi.Succeeded(ok, json)) return;
                JObject data = json["data"] as JObject;
                registered = data != null;
                myListing = data != null ? Parse(data) : null;
                MyStrip();
            });
        }

        /// <summary>/party/list and /party/search: the page replaced from data, totalPage when not 0, then msg 0x3c and 0xa5.</summary>
        private void ListArrived(bool ok, byte[] body) {
            JObject json = RoClassicWebApi.Json(body);
            if (!RoClassicWebApi.Succeeded(ok, json)) return;
            Array.Clear(rows, 0, rows.Length);
            if (json["data"] is JArray list) {
                for (int i = 0; i < list.Count && i < rows.Length; i++) {
                    if (list[i] is JObject o) rows[i] = Parse(o);
                }
            }

            int total = json.Value<int?>("totalPage") ?? 0;
            if (total != 0) pages = total;
            if (board == null || !board.Visible) OpenBoard();
            MyStrip();
            // msg 0xa5: the page within the total, next while more, prev past page 1, refresh back
            if (page > pages) page = pages;
            next.Enabled = page < pages;
            prev.Enabled = page != 1;
            refresh.Enabled = true;
            board.Invalidate();
        }

        private static Listing Parse(JObject o) {
            var l = new Listing {
                Name = o.Value<string>("CharName") ?? string.Empty,
                Aid = (uint) (o.Value<long?>("AID") ?? 0),
                Gid = (uint) (o.Value<long?>("GID") ?? 0),
                MinLv = o.Value<int?>("MinLV") ?? 0,
                MaxLv = o.Value<int?>("MaxLV") ?? 999,
                Type = o.Value<int?>("Type") ?? 0,
                Memo = o.Value<string>("Memo") ?? string.Empty,
            };
            l.Roles[0] = (o.Value<int?>("Tanker") ?? 0) != 0;
            l.Roles[1] = (o.Value<int?>("Healer") ?? 0) != 0;
            l.Roles[2] = (o.Value<int?>("Dealer") ?? 0) != 0;
            l.Roles[3] = (o.Value<int?>("Assist") ?? 0) != 0;
            return l;
        }

        /// <summary>0x71f500: my listing's row, the register strip and Cancel Recruitment by leadership and registration.</summary>
        private void MyStrip() {
            if (board == null) return;
            bool leader = IsLeader;
            myRow.Shown = leader && registered;
            myRow.Enabled = myRow.Shown;
            registerRow.Shown = !leader || !registered;
            registerRow.Enabled = registerRow.Shown;
            cancelRecruit.Shown = leader && registered;
            board.Invalidate();
        }

        /// <summary>0x169: invalid levels reset, the filter as a listing, /party/search (page 1 unless an arrow sent it).</summary>
        private void Search(bool fromArrow) {
            searchMode = true;
            if (!AllDigits(minEdit.Text)) minEdit.SetText(Math.Max(1, BaseLevel - 5).ToString(CultureInfo.InvariantCulture));
            if (!AllDigits(maxEdit.Text)) maxEdit.SetText((BaseLevel + 5).ToString(CultureInfo.InvariantCulture));
            prev.Enabled = next.Enabled = refresh.Enabled = false;
            if (!fromArrow) page = 1;
            board.Invalidate();
            var fields = RoClassicWebApi.Common(session, true);
            fields.Add(("MinLV", minEdit.Text));
            fields.Add(("MaxLV", maxEdit.Text));
            string[] names = { "Tanker", "Healer", "Dealer", "Assist" };
            for (int i = 0; i < 4; i++) {
                if (filter[i]) fields.Add((names[i], "1"));
            }

            if (purpose != 0) fields.Add(("Type", purpose.ToString(CultureInfo.InvariantCulture)));
            if (searchEdit.Text.Length > 0) fields.Add(("Memo", searchEdit.Text));
            fields.Add(("page", page.ToString(CultureInfo.InvariantCulture)));
            RoClassicWebApi.Post(session, "/party/search", fields, null, (ok, body) => ListArrived(ok, body));
        }

        private static bool AllDigits(string s) {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) {
                if (c < '0' || c > '9') return false;
            }

            return true;
        }

        /// <summary>0x167: the filter back to its defaults; no request.</summary>
        private void Reset() {
            minEdit.SetText(Math.Max(1, BaseLevel - 5).ToString(CultureInfo.InvariantCulture));
            maxEdit.SetText((BaseLevel + 5).ToString(CultureInfo.InvariantCulture));
            purpose = 0;
            Array.Clear(filter, 0, 4);
            searchEdit.SetText(string.Empty);
            searchMode = false;
            board.Invalidate();
        }

        /// <summary>0x177: my own listing opens the edit form, another one the details 0x159.</summary>
        private void RowClicked(int i) {
            Listing l = rows[i];
            if (l == null) return;
            if (l.Aid == session.LocalActorId) {
                CloseDetails();
                OpenForm(l);
                return;
            }

            CloseForm();
            OpenDetails(l);
        }

        /// <summary>RBtnUp 0x71d280: another leader's row opens a 1:1 whisper with them.</summary>
        private void Whisper(Listing l) {
            (uint charId, uint accountId) = session.LocalCharacterIds();
            if (l.Aid == accountId || l.Gid == charId || string.IsNullOrEmpty(l.Name)) return;
            RoClassicWhisper.Bind(session, chat);
            RoClassicWhisper.OpenManual(l.Name, l.Aid);
        }

        /// <summary>0x196 (0x595ee0): /party/del while registered; the answer prints 0xd83 cyan or 0xdb3 red.</summary>
        private void CancelRecruitment() {
            if (!registered || session == null) return;
            uint me = session.LocalActorId;
            var fields = RoClassicWebApi.Common(session, false);
            fields.Add(("MasterAID", me.ToString(CultureInfo.InvariantCulture)));
            bool sent = RoClassicWebApi.Post(session, "/party/del", fields, null, (ok, body) => {
                if (RoClassicWebApi.Succeeded(ok, RoClassicWebApi.Json(body))) {
                    Chat(Msg(0xd83, "Party recruitment has been canceled."), "#64FFFF");
                    registered = false;
                    myListing = null;
                    if (board != null && board.Visible) {
                        MyStrip();
                        LoadPage(1);
                    }
                } else {
                    Chat(Msg(0xdb3, "You can not stop the party recruitment."), "#FF0000");
                }
            });
            if (!sent) Chat(Msg(0xdb3, "You can not stop the party recruitment."), "#FF0000");
        }

        // ================================================================ the register form 0x143

        private void OpenForm(Listing edit) {
            if (form == null) BuildForm();
            Vector2Int c = system.ClientSize;
            int x = PlayerPrefs.GetInt(Prefs + "form.x", c.x / 2 + 240), y = PlayerPrefs.GetInt(Prefs + "form.y", c.y / 2 + 170);
            form.MoveTo(x, y);
            int lv = BaseLevel;
            formLeader = session?.SelectedCharacterName ?? string.Empty;
            formMin.SetText(Math.Max(1, lv - 5).ToString(CultureInfo.InvariantCulture));
            formMax.SetText((lv + 5).ToString(CultureInfo.InvariantCulture));
            formNote.SetText(string.Empty);
            formPurpose = 0;
            for (int i = 0; i < 4; i++) formRoles[i] = true;
            if (edit != null) {
                // 0x71f390: the listing's values
                for (int i = 0; i < 4; i++) formRoles[i] = edit.Roles[i];
                formMin.SetText(edit.MinLv.ToString(CultureInfo.InvariantCulture));
                formMax.SetText(edit.MaxLv.ToString(CultureInfo.InvariantCulture));
                formPurpose = edit.Type;
                formNote.SetText(edit.Memo);
                formLeader = edit.Name;
            }

            formSubmit.Text = registered ? Msg(0xd30, "Edit") : Msg(0xd2b, "Register");
            formSubmit.Rect.width = system.Font(12).Measure(formSubmit.Text) + 10;
            form.Visible = true;
            system.Windows.BringToFront(form);
            system.Windows.PushEsc(form);
            form.Invalidate();
        }

        private void CloseForm() {
            if (form == null || !form.Visible) return;
            form.Visible = false;
            CloseCombo();
        }

        private void BuildForm() {
            form = MakeWindow("UIRegisterPartyWnd", 0x143, 220, 215, formButtons, DrawForm, () => {
                CloseForm();
                return true;
            });
            form.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "form.x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "form.y", w.Position.y);
            };
            formMin = new RoClassicEditField { Rect = new RectInt(60, 47, 25, 16), MaxBytes = 3, Background = RegisterEdit };
            formMax = new RoClassicEditField { Rect = new RectInt(96, 47, 25, 16), MaxBytes = 3, Background = RegisterEdit };
            formNote = new RoClassicEditField { Rect = new RectInt(60, 119, 139, 14), MaxBytes = 24, Background = RegisterEdit };
            form.AddEditField(formMin);
            form.AddEditField(formMax);
            form.AddEditField(formNote);
            for (int i = 0; i < 4; i++) {
                int k = i;
                Bitmap(formButtons, 60 + 18 * i, 95, 16, 16, null, null, null, null, null, () => {
                    formRoles[k] = !formRoles[k];
                    form.Invalidate();
                });
            }

            Bitmap(formButtons, 60, 70, 63, 18, null, null, null, null, null, () => OpenCombo(form, 60, 88, p => formPurpose = p));
            formSubmit = BasicButton(formButtons, 108, 165, 50, Msg(0xd2b, "Register"), Submit);
            Bitmap(formButtons, 173, 165, 42, 20, Basic + "btn_close.bmp", Basic + "btn_close_a.bmp", Basic + "btn_close_b.bmp", null, null, CloseForm);
        }

        private void DrawForm(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Ag + "bg_agency_register.bmp");
            DrawTitle(w, Msg(0xd1c, "Register in the Adventurer's Agency"));
            w.TextOut(86, 50, "~", 0, 12, Black);
            w.TextOut(10, 23, Msg(0xd57, "Leader"), 0, 12, Black);
            w.TextOut(60, 23, Cut(w, formLeader, 119), 0, 12, Black);
            w.TextOut(10, 49, Msg(0x198, "Level"), 0, 12, Black);
            w.TextOut(10, 73, Msg(0xd29, "Purpose"), 0, 12, Black);
            w.TextOut(10, 97, Msg(0x1f8, "Job"), 0, 12, Black);
            w.TextOut(10, 121, Msg(0xd1f, "Note"), 0, 12, Black);
            DrawCombo(w, 60, 70, 63, formPurpose);
            for (int i = 0; i < 4; i++) w.DrawBitmap(60 + 18 * i, 95, Ag + "btn_job_" + RoleIcon[i] + (formRoles[i] ? "_on.bmp" : "_off.bmp"));
            DrawRules(w);
        }

        /// <summary>MSI 0xd2a wrapped at 40 bytes, 11-px red lines from (9, 136).</summary>
        private static void DrawRules(RoClassicWindow w) {
            string rules = Msg(0xd2a, "* Please respect the rules of the server.");
            var wrap = new List<(int Start, int End)>();
            RoClassicWindow.WrapBytes(rules, 40, wrap);
            for (int i = 0; i < wrap.Count; i++) w.TextOut(9, 136 + 12 * i, rules.Substring(wrap[i].Start, wrap[i].End - wrap[i].Start), 0, 11, Red);
        }

        /// <summary>0x230 / 0x231 (vf37 0x71e800): leader only, levels in order, then /party/add and the form closes.</summary>
        private void Submit() {
            if (!IsLeader) {
                Chat(Msg(0xdb2, "If you are not a party leader, you can not register party at the Adventurer's Agency."), "#FF0000");
                CloseForm();
                return;
            }

            if (!AllDigits(formMin.Text)) formMin.SetText(Math.Max(1, BaseLevel - 5).ToString(CultureInfo.InvariantCulture));
            if (!AllDigits(formMax.Text)) formMax.SetText((BaseLevel + 5).ToString(CultureInfo.InvariantCulture));
            int min = int.Parse(formMin.Text, CultureInfo.InvariantCulture), max = int.Parse(formMax.Text, CultureInfo.InvariantCulture);
            if (min > max) {
                // MSI 0xdbc (this table's text, a translation slip the client shows as is)
                Chat(Msg(0xdbc, "Registering at the Adventurer's Agency. Please wait."), "#FF0000");
                return;
            }

            var fields = RoClassicWebApi.Common(session, true);
            fields.Add(("CharName", session.SelectedCharacterName ?? string.Empty));
            fields.Add(("MinLV", min.ToString(CultureInfo.InvariantCulture)));
            fields.Add(("MaxLV", max.ToString(CultureInfo.InvariantCulture)));
            string[] names = { "Tanker", "Healer", "Dealer", "Assist" };
            for (int i = 0; i < 4; i++) fields.Add((names[i], formRoles[i] ? "1" : "0"));
            fields.Add(("Type", formPurpose.ToString(CultureInfo.InvariantCulture)));
            fields.Add(("Memo", formNote.Text));
            RoClassicWebApi.Post(session, "/party/add", fields, null, (ok, body) => {
                if (!RoClassicWebApi.Succeeded(ok, RoClassicWebApi.Json(body))) {
                    Chat(Msg(0xd59, "Failed to register for the Adventurer's Agency."), "#FF0000");
                    return;
                }

                Chat(Msg(0xd5a, "You're party has been registered in the Adventurer's Agency."), "#64FFFF");
                registered = true;
                if (board != null && board.Visible) {
                    page = 1;
                    searchMode = false;
                    LoadPage(1);
                } else {
                    OpenBoard();
                }
            });
            CloseForm();
        }

        // ================================================================ the details 0x159

        private void OpenDetails(Listing l) {
            if (details == null) {
                details = MakeWindow("UIApplyForPartyWnd", 0x159, 220, 190, detailButtons, DrawDetails, () => {
                    CloseDetails();
                    return true;
                });
                details.Moved += w => {
                    PlayerPrefs.SetInt(Prefs + "details.x", w.Position.x);
                    PlayerPrefs.SetInt(Prefs + "details.y", w.Position.y);
                };
                RoClassicGdiFont font = system.Font(12);
                string pm = Msg(0xd2e, "PM"), join = Msg(0xd2d, "Join"), close = Msg(0xbe5, "Close");
                BasicButton(detailButtons, 51, 165, font.Measure(pm) + 10, pm, () => Whisper(shown));
                BasicButton(detailButtons, 108, 165, font.Measure(join) + 10, join, Join);
                BasicButton(detailButtons, 173, 165, font.Measure(close) + 10, close, CloseDetails);
            }

            shown = l.Copy();
            Vector2Int c = system.ClientSize;
            details.MoveTo(PlayerPrefs.GetInt(Prefs + "details.x", c.x / 2 + 240), PlayerPrefs.GetInt(Prefs + "details.y", c.y / 2 - 170));
            details.Visible = true;
            system.Windows.BringToFront(details);
            system.Windows.PushEsc(details);
            details.Invalidate();
        }

        private void CloseDetails() {
            if (details != null) details.Visible = false;
        }

        private void DrawDetails(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Ag + "bg_agency_register.bmp");
            DrawTitle(w, Msg(0xd28, "Registration for Party"));
            if (shown == null) return;
            w.TextOut(10, 25, Msg(0xd57, "Leader"), 0, 12, Black);
            w.TextOut(60, 25, Cut(w, shown.Name, 119), 0, 12, Black);
            w.TextOut(10, 49, Msg(0x198, "Level"), 0, 12, Black);
            w.TextOut(60, 49, shown.MinLv + "~" + shown.MaxLv, 0, 12, Black);
            w.TextOut(10, 73, Msg(0xd29, "Purpose"), 0, 12, Black);
            w.TextOut(60, 73, PurposeName(shown.Type), 0, 12, Black);
            w.TextOut(10, 97, Msg(0x1f8, "Job"), 0, 12, Black);
            for (int i = 0; i < 4; i++) w.DrawBitmap(60 + 18 * i, 95, Ag + "btn_job_" + RoleIcon[i] + (shown.Roles[i] ? "_on.bmp" : "_off.bmp"));
            w.TextOut(10, 121, Msg(0xd1f, "Note"), 0, 12, Black);
            w.TextOut(60, 121, shown.Memo, 0, 12, Black);
            DrawRules(w);
        }

        /// <summary>
        /// 0x10b (0x594d40): /party/info for the leader; no data → "No party can be found." and 0x159 closes; data → the
        /// level and party checks, then CZ_ADVENTURER_AGENCY_JOIN_REQ 0x0ae6 {GID, AID} and 0x159 closes.
        /// </summary>
        private void Join() {
            if (shown == null) return;
            uint leader = shown.Aid;
            var fields = RoClassicWebApi.Common(session, true);
            fields.Add(("QueryAID", leader.ToString(CultureInfo.InvariantCulture)));
            RoClassicWebApi.Post(session, "/party/info", fields, null, (ok, body) => {
                JObject json = RoClassicWebApi.Json(body);
                if (!(json?["data"] is JObject data)) {
                    Chat(Msg(0xd73, "No party can be found."), "#FF0000");
                    CloseDetails();
                    return;
                }

                Listing l = Parse(data);
                for (int i = 0; i < rows.Length; i++) {
                    if (rows[i] != null && rows[i].Aid == l.Aid) rows[i] = l;
                }

                board?.Invalidate();
                if (details != null && details.Visible) {
                    shown = l.Copy();
                    details.Invalidate();
                }

                int lv = BaseLevel;
                if (lv < l.MinLv || lv > l.MaxLv) {
                    Chat(Msg(0xd58, "You don't meet the required level to join the party."), "#FF0000");
                    return;
                }

                if (session.PartySnapshot().IsInParty) {
                    Chat(Msg(0x4f, "The Character is already in a party."), "#FF0000");
                    return;
                }

                var packet = new byte[8];
                BitConverter.GetBytes(l.Gid).CopyTo(packet, 0);
                BitConverter.GetBytes(l.Aid).CopyTo(packet, 4);
                _ = session.BeginClassicPacket(0x0ae6, packet, "an agency join request");
                CloseDetails();
            });
        }

        // ================================================================ the leader's request 0x146 and the results

        public static bool Handles(ushort id) => id == 0x0ae7 || id == 0x0afa;

        public static void Handle(byte[] b) {
            RoClassicAgency a = Ensure();
            if (a == null || b == null || b.Length < 4) return;
            ushort id = BitConverter.ToUInt16(b, 0);
            if (id == 0x0ae7 && b.Length >= 38) {
                // 0x71d4e0: a blocked requester is ignored; else the window or the queue
                var record = (Text(b, 10, 24), BitConverter.ToUInt32(b, 2), BitConverter.ToUInt32(b, 6), BitConverter.ToInt16(b, 36), BitConverter.ToInt16(b, 34));
                if (a.blocked.Contains(record.Item3)) return;
                if (a.request != null && a.request.Visible) a.queue.Add(record);
                else a.Ask(record);
            } else if (id == 0x0afa && b.Length >= 58) {
                a.Result(Text(b, 2, 24), Text(b, 26, 24), BitConverter.ToUInt32(b, 50), BitConverter.ToInt32(b, 54));
            }
        }

        private void Ask((string Name, uint Grid, uint Aid, short Job, short Level) record) {
            if (request == null) {
                request = MakeWindow("UIRequestJoinPartyWnd", 0x146, 220, 160, requestButtons, DrawRequest, () => {
                    Answer(false);
                    return true;
                });
                RoClassicGdiFont font = system.Font(12);
                string accept = Msg(0xda1, "Accept"), refuse = Msg(0xda2, "Refuse");
                BasicButton(requestButtons, 122, 136, font.Measure(accept) + 10, accept, () => Answer(true));
                BasicButton(requestButtons, 169, 136, font.Measure(refuse) + 10, refuse, () => Answer(false));
                Bitmap(requestButtons, 7, 81, 24, 47, null, null, null, null, null, () => {
                    block = !block;
                    request.Invalidate();
                });
            }

            asking = record;
            block = false;
            Vector2Int c = system.ClientSize;
            request.MoveTo(c.x / 2 - 110, c.y / 2 - 80);
            request.Visible = true;
            system.Windows.BringToFront(request);
            system.Windows.PushEsc(request);
            request.Invalidate();
        }

        private void DrawRequest(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Ag + "bg_agency_admit.bmp");
            DrawTitle(w, Msg(0xdb1, "Request to join the party"));
            w.DrawBitmap(12, 29, Ui + "renewalparty\\icon_jobs_" + asking.Job.ToString(CultureInfo.InvariantCulture) + ".bmp");
            string level = asking.Level.ToString(CultureInfo.InvariantCulture);
            w.TextOut(25 - w.FontFor(0, 12).Measure(level) / 2, 56, level, 0, 12, Black);
            string text = Msg(0xd32, "[%s] has requested to join the party.").Replace("%s", asking.Name);
            var wrap = new List<(int Start, int End)>();
            RoClassicWindow.WrapBytes(text, 26, wrap);
            int top = wrap.Count <= 1 ? 30 : 20;
            w.FillRect(43, top, 167, 60, White);
            for (int i = 0; i < wrap.Count && i < 3; i++) w.TextOut(46, top + 4 + 18 * i, text.Substring(wrap[i].Start, wrap[i].End - wrap[i].Start), 0, 12, Black);
            w.DrawBitmap(7, 81, Ag + (block ? "btn_block_on.bmp" : "btn_block_off.bmp"));
            w.FillRect(31, 81, 182, 47, new Color32(0xd7, 0xc6, 0xbc, 255));
            string note = Msg(0xdbe, "Block party request from this player.");
            RoClassicWindow.WrapBytes(note, 30, wrap);
            for (int i = 0; i < wrap.Count && i < 3; i++) w.TextOut(34, 85 + 15 * i, note.Substring(wrap[i].Start, wrap[i].End - wrap[i].Start), 0, 12, Black);
        }

        /// <summary>0x219 / 0x21a (Close and Esc refuse): CZ_ADVENTURER_AGENCY_JOIN_RESULT 0x0af8 {GRID, AID, accept}, then the next request.</summary>
        private void Answer(bool accept) {
            if (!accept && block) blocked.Add(asking.Aid);
            var packet = new byte[9];
            BitConverter.GetBytes(asking.Grid).CopyTo(packet, 0);
            BitConverter.GetBytes(asking.Aid).CopyTo(packet, 4);
            packet[8] = (byte) (accept ? 1 : 0);
            _ = session?.BeginClassicPacket(0x0af8, packet, "an agency join answer");
            block = false;
            while (queue.Count > 0) {
                var nextRequest = queue[0];
                queue.RemoveAt(0);
                if (blocked.Contains(nextRequest.Aid)) continue;
                Ask(nextRequest);
                return;
            }

            request.Visible = false;
        }

        /// <summary>0xc5d0f0: ZC_ADVENTURER_AGENCY_JOIN_RESULT's line by result (cyan for 0, red otherwise).</summary>
        private void Result(string player, string party, uint aid, int result) {
            bool leader = IsLeader;
            string text;
            switch (result) {
                case 0: text = leader ? Msg(0xdad, "[%s] accepted your party invitation.").Replace("%s", player) : Msg(0xdaa, "[%s]'s party leader has approved your join request.").Replace("%s", party); break;
                case 1: text = leader ? Msg(0xdae, "[%s] refused your party invitation.").Replace("%s", player) : Msg(0xdab, "[%s]'s party leader has rejected your join request.").Replace("%s", party); break;
                case 2: text = Msg(0xda9, "Party number exceeded."); break;
                case 3: text = Msg(0xd72, "The party master cannot accept the request."); break;
                case 4: text = Msg(0xda8, "The current requestor is in an area where it's not possible to join the party."); break;
                case 5:
                case 6: text = Msg(0xda4, "The character could not be found."); break;
                case 7: text = Msg(0xdac, "[%s] is already in the party.").Replace("%s", player); break;
                case 8: text = Msg(0xdaf, "Since you can not receive party request, %s's party invitation has been rejected.").Replace("%s", player); break;
                case 9:
                    text = Msg(0xda6, "Unable to find applicable party.");
                    // 0x595de0: the stale listing removed
                    var fields = RoClassicWebApi.Common(session, false);
                    fields.Add(("MasterAID", aid.ToString(CultureInfo.InvariantCulture)));
                    RoClassicWebApi.Post(session, "/party/del", fields, null, (ok, body) => { });
                    break;
                case 10: text = Msg(0xd73, "No party can be found."); break;
                case 11: text = Msg(0xda7, "You can not join the party because your job level is low."); break;
                case 12: text = Msg(0x260, "Character in the same account already joined."); break;
                case 13: text = Msg(0x718, "Now Logging Out."); break;
                default: return;
            }

            Chat(text, result == 0 ? "#64FFFF" : "#FF0000");
        }

        // ================================================================ the purpose combo

        private void OpenCombo(RoClassicWindow owner, int x, int y, Action<int> pick) {
            if (combo == null) {
                combo = new RoClassicWindow(system, new RoClassicWindowLayout { Class = "UIComboBoxWnd", WindowId = 0x1a, Width = 126, Height = 16 * PurposeMsg.Length }) {
                    Visible = false, Draggable = false, ClickableAt = _ => true,
                };
                combo.DrawDynamic += w => {
                    w.FillRect(0, 0, w.Width, w.Height, White);
                    w.DrawBitmapRaw(0, 0, Basic + "sysboxs_lu.bmp");
                    w.DrawBitmapRaw(0, w.Height - 7, Basic + "sysboxs_ld.bmp");
                    w.DrawBitmapRaw(w.Width - 7, 0, Basic + "sysboxs_ru.bmp");
                    w.DrawBitmapRaw(w.Width - 7, w.Height - 7, Basic + "sysboxs_rd.bmp");
                    comboHover = system.Windows.HoverWindow == w ? (system.Windows.Mouse.y - w.Position.y) / 16 : -1;
                    for (int i = 0; i < PurposeMsg.Length; i++) {
                        bool on = i == comboHover;
                        if (on) w.FillRect(2, 16 * i + 1, w.Width - 4, 14, Selected);
                        w.TextOut(4, 16 * i + 2, PurposeName(i), 0, 12, on ? White : Black);
                    }
                };
                combo.Pressed += (w, local) => {
                    int k = local.y / 16;
                    CloseCombo();
                    if (k >= 0 && k < PurposeMsg.Length) {
                        comboPick?.Invoke(k);
                        board?.Invalidate();
                        form?.Invalidate();
                    }
                };
                combo.EscClose = () => {
                    CloseCombo();
                    return true;
                };
            }

            comboPick = pick;
            combo.MoveTo(owner.Position.x + x, owner.Position.y + y);
            combo.Visible = true;
            system.Windows.BringToFront(combo);
            system.Windows.PushEsc(combo);
            combo.Invalidate();
        }

        private void CloseCombo() {
            if (combo == null || !combo.Visible) return;
            combo.Visible = false;
            system.Windows.RemoveEsc(combo);
        }

        // ================================================================ per frame

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (combo != null && combo.Visible) combo.Invalidate();
            foreach (RoClassicWindow w in new[] { board, form, details, request }) {
                if (w != null && w.Visible && ui.Windows.HoverWindow == w) w.Invalidate();
            }

            // the role tooltips: the filters, the form's toggles and the details' icons (0x71d0b0)
            RoClassicWindow over = ui.Windows.HoverWindow;
            if (over == null || (over != board && over != form && over != details)) return;
            Vector2Int p = ui.Windows.Mouse - over.Position;
            int x0 = over == board ? 160 : 60, y0 = over == board ? 23 : 95;
            if (p.y < y0 || p.y >= y0 + 16 || p.x < x0 || p.x >= x0 + 72) return;
            int i = (p.x - x0) / 18;
            RoClassicTooltip.Shared.Show(Msg(RoleTip[i].Msg, RoleTip[i].Text), White, over.Position.x + x0 + 18 * i, over.Position.y + y0 - 20);
        }

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        private static string Text(byte[] b, int at, int length) {
            int end = at;
            while (end < at + length && end < b.Length && b[end] != 0) end++;
            Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
            return Encoding.GetEncoding(1252).GetString(b, at, end - at);
        }

        private void Chat(string text, string colour) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, colour);
        }

        /// <summary>!agency toggle | search | reset | row i | rrow i | register | submit | cancel | details join|pm|close | ask name job lv | answer 0|1 | block | result n | state.</summary>
        internal static string Sweep(string args) {
            RoClassicAgency a = Ensure();
            if (a == null) return "no ui";
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            int n = t.Length > 1 && int.TryParse(t[1], out int v) ? v : 0;
            switch (t[0]) {
                case "toggle": Toggle(); break;
                case "search": a.Search(false); break;
                case "reset": a.Reset(); break;
                case "row": a.RowClicked(n); break;
                case "rrow": if (a.rows[n] != null) a.Whisper(a.rows[n]); break;
                case "register": a.OpenForm(a.registered ? a.myListing : null); break;
                case "submit": a.Submit(); break;
                case "cancel": a.CancelRecruitment(); break;
                case "join": a.Join(); break;
                case "ask": {
                    var p = new byte[38];
                    BitConverter.GetBytes((ushort) 0x0ae7).CopyTo(p, 0);
                    BitConverter.GetBytes(9001u).CopyTo(p, 2);
                    BitConverter.GetBytes(2000010u).CopyTo(p, 6);
                    byte[] name = Encoding.ASCII.GetBytes(t.Length > 1 ? t[1] : "FxBuddy");
                    Array.Copy(name, 0, p, 10, Math.Min(23, name.Length));
                    BitConverter.GetBytes((short) (t.Length > 3 ? int.Parse(t[3]) : 50)).CopyTo(p, 34);
                    BitConverter.GetBytes((short) (t.Length > 2 ? int.Parse(t[2]) : 7)).CopyTo(p, 36);
                    Handle(p);
                    break;
                }
                case "answer": a.Answer(n == 1); break;
                case "block": a.block = !a.block; break;
                case "result": a.Result("Player", "Party", 0, n); break;
            }

            var rowsText = new StringBuilder();
            foreach (Listing l in a.rows) {
                if (l != null) rowsText.Append(l.Name).Append('[').Append(l.MinLv).Append('-').Append(l.MaxLv).Append(" t").Append(l.Type).Append("] ");
            }

            return "board=" + (a.board != null && a.board.Visible) + " page=" + a.page + "/" + a.pages + " rows=" + rowsText + "registered=" + a.registered +
                " leader=" + a.IsLeader + " form=" + (a.form != null && a.form.Visible) + " details=" + (a.details != null && a.details.Visible) +
                " request=" + (a.request != null && a.request.Visible) + " queue=" + a.queue.Count + " webapi=" + RoClassicWebApi.Available(a.session);
        }
    }
}
