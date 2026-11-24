using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The classic Party / Friend window, UIMessengerGroupWnd 0x22 (ui/RUNTIME-RULES-PARTY.md §1): the Friend | Party radios,
    /// party rows with the job icon, crown, "Lv.N name(map)", the HP gauge of members in view and the state icon, friend
    /// rows with the online mark, the eleven-button bar, the compact / normal view, height-only resize snapped to rows,
    /// the context menu on a row (§3b), a double click whispering. Opened by the menu icon / Alt+Z, the Friend / Setup
    /// shortcuts and a question's OK. The small party windows (.Mini), the party / friend dialogs and the server's two
    /// questions (.Dialogs) live here too; all of them follow the session's party / friend sequence from <see cref="Tick"/>
    /// (RoClassicGameUi), whether the window is open or not. The lists, rules and requests are <see cref="RoPartyFriendsModel"/>.
    /// </summary>
    public sealed partial class RoClassicPartyFriends {
        private const string MsUi = "유저인터페이스\\";
        private const string MsBasic = MsUi + "basic_interface\\";
        private const string MsParty = MsUi + "renewalparty\\";
        private const string MsPrefs = "ro.ui.classic.messenger.";
        private const int MsFriendRowH = 23;
        private const int MsMaxRows = 12;
        private const int MsCompactId = 0xc6;
        private const int MsNormalId = 0x207;
        private const int MsCloseId = 0xc5;
        private const int MsAgencyId = 0x21c;
        private const int MsChatId = 0xe4;
        private const int MsInfoId = 0xe5;
        private const int MsSetupId = 0xe6;
        private const int MsDeleteId = 0xe7;
        private const int MsLockId = 0x175;
        private const int MsCreateId = 0x1b9;
        private const int MsLeaveId = 0x1ba;
        private const int MsInviteId = 0x1bb;
        private const int MsSortId = 0x168;

        private static readonly Color32 MsBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 MsWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 MsLine = new Color32(0xc0, 0xc0, 0xc0, 255);
        private static readonly Color32 MsSelected = new Color32(0x70, 0x9f, 0xed, 255);
        private static readonly Color32 MsMini = new Color32(0xdd, 0xe8, 0xff, 255);
        private static readonly Color32 MsLevel = new Color32(0x37, 0x3a, 0x49, 255);
        private static readonly Color32 MsOffline = new Color32(0x86, 0x8a, 0xa3, 255);
        private static readonly Color32 MsGaugeBorder = new Color32(0x14, 0x19, 0x9b, 255);
        private static readonly Color32 MsGaugeEmpty = new Color32(0x44, 0x44, 0x44, 255);
        private static readonly Color32 MsGaugeHp = new Color32(0x16, 0xea, 0x25, 255);
        private static readonly Color32 MsGaugeLow = new Color32(0xea, 0x16, 0x16, 255);
        // the per-view row layout {x, y, party row height} (OnCreate +0x24c)
        private static readonly (int X, int Y, int RowH)[] MsLayouts = { (5, 5, 34), (4, 1, 27) };

        private static RoClassicPartyFriends instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private readonly RoPartyFriendsModel model = new RoPartyFriendsModel();
        private readonly PartyDialogs pd;
        private RoClassicUiSystem system;
        private RoClassicChat chat;
        private int seenSequence = int.MinValue;
        private RoClassicWindow classicMs;
        private int msViewMode;                 // 0 normal, 1 compact
        private int msHeight = 326;             // the requested height (snapped by MsLayout)
        private int msFirstRow;
        private int msSelected = -1;
        private RoPartyRuntimeSnapshot msParty;
        private readonly List<RoFriendRuntimeSnapshot> msFriends = new List<RoFriendRuntimeSnapshot>(32);

        private RoClassicPartyFriends() {
            pd = new PartyDialogs(this);
        }

        /// <summary>The Party / Friend window is open.</summary>
        public static bool IsOpen => instance != null && instance.MsShown;

        public static RoPartyFriendsTab ActiveTab => instance != null ? instance.model.ActiveTab : RoPartyFriendsTab.Party;

        private bool MsShown => classicMs != null && classicMs.Visible;

        private bool MsPartyTab => model.ActiveTab == RoPartyFriendsTab.Party;

        private bool Locked => model.Locked;

        private int MsRowH => MsPartyTab ? MsLayouts[msViewMode].RowH : MsFriendRowH;

        private int MsCount => MsPartyTab ? (msParty.Members?.Length ?? 0) : msFriends.Count;

        // ---- open / close ----

        /// <summary>The menu icon / Alt+Z: opens the window on the tab, or closes it when it is open on that tab.</summary>
        public static void Toggle(RoLoginShellSession loginSession, RoPartyFriendsTab tab, IRoSocialEconomyCommandExecutor commands = null) {
            if (IsOpen && instance.model.ActiveTab == tab) {
                instance.HideClassicMessenger();
                return;
            }

            Open(loginSession, tab, commands);
        }

        /// <summary>MakeWindow 0x22 and SendMsg(6, 0xd3): the window opens (or comes to the front) on the tab.</summary>
        public static void Open(RoLoginShellSession loginSession, RoPartyFriendsTab tab, IRoSocialEconomyCommandExecutor commands = null) {
            Ensure(loginSession, commands)?.ShowMessenger(tab);
        }

        /// <summary>The Party Setup shortcut: the window on the Party tab and Party Setup beside it (in a party).</summary>
        public static void OpenPartySetup(RoLoginShellSession loginSession) {
            RoClassicPartyFriends p = Ensure(loginSession, null);
            if (p == null) return;
            p.ShowMessenger(RoPartyFriendsTab.Party);
            if (!p.pd.IsPartySettingOpen(PdSetupVariant)) p.ClassicSetupButton();
        }

        /// <summary>The Friend Setup shortcut: the window on the Friend tab and Friend Setup beside it.</summary>
        public static void OpenFriendSetup(RoLoginShellSession loginSession) {
            RoClassicPartyFriends p = Ensure(loginSession, null);
            if (p == null) return;
            p.ShowMessenger(RoPartyFriendsTab.Friends);
            if (!p.pd.IsFriendOptionOpen) p.ClassicSetupButton();
        }

        /// <summary>Leaving the map: every window and question closes, the small windows go, nothing is remembered.</summary>
        public static void Close() {
            instance?.CloseEverything();
        }

        /// <summary>A Friend Setup option of the session's character (the whisper window and its alarm read them).</summary>
        public static bool LoadFriendSetupPreference(RoLoginShellSession loginSession, string preferenceName, bool defaultValue) =>
            RoPartyFriendsModel.LoadFriendSetupPreference(loginSession != null ? loginSession.SelectedCharacterName : null, preferenceName, defaultValue);

        /// <summary>
        /// Per frame (RoClassicGameUi): when the session's party / friend sequence moved, the server's questions, the small
        /// windows and the open window follow the new lists; then the dialogs' keys and the hovered party row.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession, RoClassicChat chatWindow) {
            if (loginSession == null) return;
            RoClassicPartyFriends p = Ensure(loginSession, null);
            if (p == null) return;
            p.chat = chatWindow;
            int sequence = p.model.Sequence;
            if (sequence != p.seenSequence) {
                p.seenSequence = sequence;
                p.Refresh();
            }

            p.pd.Tick();
            p.TickClassicMessenger();
        }

        private static RoClassicPartyFriends Ensure(RoLoginShellSession loginSession, IRoSocialEconomyCommandExecutor commands) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || loginSession == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicPartyFriends { system = system };
            if (instance.model.Session != null && !ReferenceEquals(instance.model.Session, loginSession)) instance.CloseEverything();
            instance.model.Bind(loginSession, commands);
            return instance;
        }

        private void CloseEverything() {
            HideClassicMessenger();
            pd.CloseAll();
            DestroyClassicMinis();
            model.Reset();
            seenSequence = int.MinValue;
        }

        /// <summary>The lists from the session; the questions, the small windows and the open window follow them.</summary>
        private void Refresh() {
            int before = MsCount;
            msParty = model.ResolvePartySnapshot();
            model.ResolveFriendSnapshot(msFriends);
            pd.Follow(msParty, msFriends);
            RefreshClassicMinis(msParty);
            if (!MsShown) return;
            if (MsCount != before) LayoutClassicMessenger(); // the title and the bar follow the counts
            if (msSelected >= MsCount) msSelected = -1; // a deleted / expelled row takes the selection with it
            ClampMsScroll();
            classicMs.Invalidate();
        }

        private void ShowMessenger(RoPartyFriendsTab tab) {
            if (MsShown) {
                SelectMsTab(tab);
                system.Windows.BringToFront(classicMs);
                return;
            }

            model.ShowTab(tab);
            ShowClassicMessenger();
        }

        private void ShowClassicMessenger() {
            EnsureClassicMessenger();
            Vector2Int client = system.ClientSize;
            msViewMode = PlayerPrefs.GetInt(MsPrefs + "view", 0) == 1 ? 1 : 0;
            msHeight = PlayerPrefs.GetInt(MsPrefs + "h", 326);
            msFirstRow = 0;
            msSelected = -1;
            msParty = model.ResolvePartySnapshot();
            model.ResolveFriendSnapshot(msFriends);
            LayoutClassicMessenger();
            // MakeWindow(0x22): (scaledX(200), scaledY(200)), then the position memory
            int x = PlayerPrefs.GetInt(MsPrefs + "x", (client.x - 640) / 2 + 200), y = PlayerPrefs.GetInt(MsPrefs + "y", 200 * client.y / 480);
            classicMs.MoveTo(Mathf.Clamp(x, 0, Mathf.Max(0, client.x - classicMs.Width)), Mathf.Clamp(y, 0, Mathf.Max(0, client.y - classicMs.Height)));
            classicMs.Visible = true;
            classicMs.Invalidate();
            system.Windows.BringToFront(classicMs);
        }

        private void HideClassicMessenger() {
            if (!MsShown) return;
            classicMs.Visible = false;
            RoClassicMenu.Close();
            ClassicDialogsFollowMessengerHidden();
        }

        /// <summary>A line in the chat window (the dialogs' and the menus' notices), of the caller's chat type (0 "Public" by default).</summary>
        private void Chat(string text, string color, int type = RoChatLineTypes.Public, bool popOpen = false) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddTypedLine(text, color, type, popOpen);
        }

        // ---- the window ----

        private void EnsureClassicMessenger() {
            if (classicMs != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIMessengerGroupWnd", WindowId = 0x22, Width = 273, Height = 296 };
            layout.Children.Add(MsButton(MsCompactId, "compact", 245, 3, 11, 11, MsBasic + "sys_mini_off.bmp", MsBasic + "sys_mini_on.bmp", MsBasic + "sys_mini_on.bmp"));
            layout.Children.Add(MsButton(MsNormalId, "normal", 245, 3, 10, 10, MsParty + "btn_titlebar_maximum_out.bmp", MsParty + "btn_titlebar_maximum_over.bmp", MsParty + "btn_titlebar_maximum_press.bmp"));
            layout.Children.Add(MsButton(MsCloseId, "close", 259, 3, 11, 11, MsBasic + "sys_close_off.bmp", MsBasic + "sys_close_on.bmp", MsBasic + "sys_close_on.bmp"));
            // the bar (§1f): 26×20 each; Agency and Information are re-skinned per tab
            layout.Children.Add(MsBar(MsAgencyId, "agency", "mesbtn_partymaster_01"));
            layout.Children.Add(MsBar(MsChatId, "chat", "mesbtn_02"));
            layout.Children.Add(MsBar(MsInfoId, "info", "mesbtn_03_disable", true));
            layout.Children.Add(MsBar(MsSetupId, "setup", "mesbtn_04"));
            layout.Children.Add(MsBar(MsDeleteId, "delete", "mesbtn_05"));
            layout.Children.Add(MsBar(MsLockId, "unlock", "mesbtn_06"));
            layout.Children.Add(MsBar(MsLockId, "lock", "mesbtn_07"));
            layout.Children.Add(MsBar(MsCreateId, "create", "mesbtn_08"));
            layout.Children.Add(MsBar(MsLeaveId, "leave", "mesbtn_09"));
            layout.Children.Add(MsBar(MsInviteId, "invite", "mesbtn_010"));
            layout.Children.Add(MsBar(MsSortId, "sort", "mesbtn_011"));

            // Esc-excluded, not in the Enter chain; dragged by its title bar
            classicMs = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = local => local.y < 17 };
            classicMs.DrawDynamic += DrawClassicMessenger;
            classicMs.Clicked += OnMsButtonClicked;
            classicMs.Pressed += (w, local) => OnMsPressed(local);
            classicMs.DoubleClicked += (w, local) => MsWhisperSelected();
            classicMs.RightClicked += (w, local) => OnMsRightClicked(local);
            classicMs.Wheeled += (w, local, delta) => ScrollMs(delta > 0 ? -MsWheelStep : MsWheelStep);
            classicMs.Moved += w => {
                PlayerPrefs.SetInt(MsPrefs + "x", w.Position.x);
                PlayerPrefs.SetInt(MsPrefs + "y", w.Position.y);
            };
            // UIResizer mode 7 at (W − 15, H − 15): the height only, snapped to whole rows
            classicMs.ResizerModeAt = local => local.x >= classicMs.Width - 15 && local.y >= classicMs.Height - 15 ? 7 : -1;
            classicMs.ResizeRequested += (w, mode, width, height) => {
                msHeight = height;
                PlayerPrefs.SetInt(MsPrefs + "h", height);
                LayoutClassicMessenger();
            };
        }

        private static RoClassicChildLayout MsButton(int id, string path, int x, int y, int w, int h, string normal, string hover, string pressed) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            button.States["normal"] = normal;
            button.States["hover"] = hover;
            button.States["pressed"] = pressed;
            return button;
        }

        private static RoClassicChildLayout MsBar(int id, string path, string bitmap, bool disabled = false) =>
            disabled
                ? MsButton(id, path, -100, -100, 26, 20, MsBasic + bitmap + ".bmp", MsBasic + bitmap + ".bmp", MsBasic + bitmap + ".bmp")
                : MsButton(id, path, -100, -100, 26, 20, MsBasic + bitmap + ".bmp", MsBasic + bitmap + "_a.bmp", MsBasic + bitmap + "_b.bmp");

        private static void MsSkin(RoClassicChildLayout button, string bitmap, bool disabled) {
            button.States["normal"] = MsBasic + bitmap + ".bmp";
            button.States["hover"] = MsBasic + bitmap + (disabled ? ".bmp" : "_a.bmp");
            button.States["pressed"] = MsBasic + bitmap + (disabled ? ".bmp" : "_b.bmp");
        }

        /// <summary>
        /// msg 0xe (v1 = 7): 273 / 243 wide by view, the height at least 326 / 179, the list snapped to whole rows (at
        /// most 12); the frame, the title buttons, the radios and the bar laid out (§1d, §1f).
        /// </summary>
        private void LayoutClassicMessenger() {
            int w = msViewMode == 0 ? 273 : 243;
            int h = Math.Max(msHeight, msViewMode == 0 ? 326 : 179);
            int rowH = MsRowH;
            int listH = h - 58;
            if (listH / rowH > MsMaxRows) {
                listH = MsMaxRows * rowH;
                h = listH + 58;
            }

            h -= listH % rowH;
            bool resized = w != classicMs.Width || h != classicMs.Height;
            classicMs.Layout.Width = w;
            classicMs.Layout.Height = h;
            classicMs.SetHeight(h);
            if (resized) msFirstRow = 0;

            List<RoClassicOp> ops = classicMs.Layout.Ops;
            ops.Clear();
            RoClassicFrames.TitleBar(ops, w, true);
            RoClassicFrames.Fill(ops, 0, 17, w, h - 38, MsWhite);
            RoClassicFrames.Fill(ops, 0, 17, w, 1, MsLine);
            RoClassicFrames.Fill(ops, 0, 17, 1, h - 38, MsLine);
            RoClassicFrames.Fill(ops, w - 1, 17, 1, h - 38, MsLine);
            RoClassicFrames.Fill(ops, 0, h - 22, w, 1, MsLine);
            RoClassicFrames.ButtonBar(ops, w, h, 0);
            RoClassicFrames.Bitmap(ops, MsBasic + "mesbtn_left.bmp", 0, h - 40);
            RoClassicFrames.Bitmap(ops, MsBasic + "mesbtn_right.bmp", w - 7, h - 40);
            for (int k = 0; k < (w - 14 + 6) / 7; k++) RoClassicFrames.Bitmap(ops, MsBasic + "mesbtn_mid.bmp", 7 + 7 * k, h - 40);
            if (!MsPartyTab && msViewMode == 0) {
                RoClassicFrames.Bitmap(ops, MsParty + "img_friend1.bmp", 147, 36, false);
                RoClassicFrames.Bitmap(ops, MsParty + "img_friend2.bmp", 99, 193, false);
            }

            foreach (RoClassicChildLayout child in classicMs.Layout.Children) {
                switch (child.Id) {
                    case MsCompactId:
                        classicMs.SetChildPosition(child.Path, w - 28, 3);
                        classicMs.SetChildVisible(child, msViewMode == 0);
                        break;
                    case MsNormalId:
                        classicMs.SetChildPosition(child.Path, w - 28, 3);
                        classicMs.SetChildVisible(child, msViewMode == 1);
                        break;
                    case MsCloseId:
                        classicMs.SetChildPosition(child.Path, w - 14, 3);
                        break;
                }
            }

            LayoutMsButtons();
            ClampMsScroll();
            classicMs.Invalidate();
        }

        /// <summary>LayoutButtons 0x6b7e50: the tab's buttons packed from x 5, Create or Invite + Leave + Sort by party, the lock.</summary>
        private void LayoutMsButtons() {
            int h = classicMs.Height;
            bool party = MsPartyTab, inParty = party && (msParty.Members?.Length ?? 0) > 0;
            void Place(string path, int x) {
                foreach (RoClassicChildLayout child in classicMs.Layout.Children) {
                    if (child.Path != path) continue;
                    classicMs.SetChildVisible(child, x >= 0);
                    classicMs.SetChildPosition(path, x >= 0 ? x : -100, x >= 0 ? h - 40 : -100);
                }
            }

            foreach (RoClassicChildLayout child in classicMs.Layout.Children) {
                if (child.Id == MsAgencyId) MsSkin(child, party ? "mesbtn_partymaster_01" : "mesbtn_01_disable", !party);
            }

            Place("agency", 5);
            Place("chat", party ? -1 : 31);
            Place("info", party ? -1 : 57);
            Place("setup", party ? 31 : 83);
            Place("delete", party ? -1 : 109);
            int lockX = party ? 57 : 135;
            Place("unlock", Locked ? lockX : -1);
            Place("lock", Locked ? -1 : lockX);
            Place("create", party && !inParty ? 83 : -1);
            Place("invite", inParty ? 83 : -1);
            Place("leave", inParty ? 109 : -1);
            Place("sort", inParty ? 135 : -1);
        }

        private int MsVisibleRows {
            get {
                int sbH = classicMs.Height - 58;
                return MsPartyTab ? sbH / MsRowH : (classicMs.Height - 180) / MsFriendRowH + 5;
            }
        }

        private int MsWheelStep => Math.Min(Math.Max(1, MsVisibleRows - 1), 3);

        private void ClampMsScroll() => msFirstRow = Mathf.Clamp(msFirstRow, 0, Math.Max(0, MsCount - MsVisibleRows));

        private void ScrollMs(int rows) {
            int before = msFirstRow;
            msFirstRow += rows;
            ClampMsScroll();
            if (msFirstRow != before) classicMs.Invalidate();
        }

        // ---- drawing (OnDraw 0x6b61e0) ----

        private void DrawClassicMessenger(RoClassicWindow w) {
            int width = w.Width, h = w.Height;
            RoClassicGdiFont regular = w.FontFor(0, 12, false), bold = w.FontFor(0, 12, true);

            // the title (UIStaticText (16, 3), (W − 57)×12, white shadow): "Party" / "Party(name)" / "Friend(n/40)"
            string title = MsPartyTab
                ? (string.IsNullOrEmpty(msParty.PartyName) ? RoClassicUiHost.MsgStr(0x67, "Party") : RoClassicUiHost.MsgStr(0x67, "Party") + "(" + msParty.PartyName + ")")
                : RoClassicUiHost.MsgStr(0x66, "Friend") + "(" + msFriends.Count.ToString(CultureInfo.InvariantCulture) + "/40)";
            w.ClipTo(16, 3, width - 57, 12);
            w.TextOutPlain(17, 4, title, regular, MsWhite);
            w.TextOutPlain(16, 3, title, regular, MsBlack);
            w.ResetClip();

            var (lx, ly, _) = MsLayouts[msViewMode];
            int rowH = MsRowH, visible = MsVisibleRows, count = MsCount;
            uint me = model.LocalAccountId;
            w.ClipTo(1, 18, width - 16, h - 40);
            for (int i = msFirstRow, drawn = 0; i < count && drawn < visible; i++, drawn++) {
                int rowY = rowH * drawn + 17;
                if (MsPartyTab) DrawMsPartyRow(w, regular, bold, msParty.Members[i], i, lx, rowY, ly + rowY, rowH, me);
                else DrawMsFriendRow(w, regular, msFriends[i], i, lx, rowY);
            }

            w.ResetClip();
            if (MsPartyTab && count > 0) {
                // the footer (drawn with the party rows): "Cap" and "n/12"
                w.TextOut(width - 96, h - 16, RoClassicUiHost.MsgStr(0xc9f, "Cap"), 0, 12, MsBlack);
                w.DrawBitmap(width - 58, h - 18, MsParty + "bg_partymember.bmp");
                w.TextOut(width - 51, h - 16, count.ToString(CultureInfo.InvariantCulture) + "/12", 0, 12, MsWhite);
            }

            // the radios Friend (4, H − 16) and Party (64, H − 16): radiobtn_on/off and the text at (13, 0)
            w.DrawBitmap(4, h - 16, MsUi + (MsPartyTab ? "radiobtn_off.bmp" : "radiobtn_on.bmp"));
            w.TextOut(4 + 13, h - 16, RoClassicUiHost.MsgStr(0x66, "Friend"), 0, 12, MsBlack);
            w.DrawBitmap(64, h - 16, MsUi + (MsPartyTab ? "radiobtn_on.bmp" : "radiobtn_off.bmp"));
            w.TextOut(64 + 13, h - 16, RoClassicUiHost.MsgStr(0x67, "Party"), 0, 12, MsBlack);

            int rows = count, range = Math.Max(0, rows - visible);
            w.DrawScrollBar(width - 15, 17, h - 58, range, rows, visible, msFirstRow);
            w.DrawBitmap(width - 15, h - 15, MsUi + "btn_resize.bmp");
        }

        private void DrawMsPartyRow(RoClassicWindow w, RoClassicGdiFont regular, RoClassicGdiFont bold, RoPartyMemberRuntimeSnapshot m,
            int i, int x0, int rowY, int yI, int rowH, uint me) {
            int width = w.Width;
            bool online = RoPartyFriendsModel.IsOnlinePartyMember(m);
            if (model.IsDetached(m.AccountId)) w.FillRect(0, rowY + 2, width, rowH, MsMini);
            if (i == msSelected) w.FillRect(0, rowY + 2, width, rowH, MsSelected);

            // the job icon (25×25; grey offline, the _die art dead) and the crown of the leader
            string job = MsParty + "icon_jobs_" + m.ClassId.ToString(CultureInfo.InvariantCulture) + (m.IsDead ? "_die" : string.Empty) + ".bmp";
            w.DrawBitmap(x0, yI, online ? job : RoClassicUiImages.Tinted(job, 1));
            if (RoPartyFriendsModel.IsPartyLeaderMember(msParty, m)) w.DrawBitmap(x0 + 2, yI - 8, MsParty + "ico_partyCrown.bmp");

            string lv = "Lv." + m.BaseLevel.ToString(CultureInfo.InvariantCulture);
            string map = online ? RoClassicUiHost.MapDisplayName(m.MapName) : string.Empty;
            string name = string.IsNullOrEmpty(map) ? m.Name : m.Name + "(" + map + ")";
            name = MsFit(name, bold, (int) (width * 0.8 - 80.0));
            w.TextOutPlain(x0 + 28, yI + 3, lv, bold, online ? MsLevel : MsOffline);
            w.TextOutPlain(x0 + 33 + bold.Measure(lv), yI + 3, name, bold, online ? MsBlack : MsOffline);

            // the HP gauge (UIPcGage 60×5) and "hp/max" in h10: only for a member online and in view
            if (online && m.MaxHp > 0 && model.IsInView(m.AccountId)) {
                int gx = 47, gy = (int) (rowH * 0.76f + rowY);
                w.FillRect(gx, gy, 60, 5, MsGaugeBorder);
                w.FillRect(gx + 1, gy + 1, 58, 3, MsGaugeEmpty);
                int pct = m.Hp * 100 / m.MaxHp, fill = 60 * pct / 100 - 2;
                if (fill > 0) w.FillRect(gx + 1, gy + 1, fill, 3, pct >= 25 ? MsGaugeHp : MsGaugeLow);
                w.TextOut(gx + 66, gy - 2, m.Hp.ToString(CultureInfo.InvariantCulture) + "/" + m.MaxHp.ToString(CultureInfo.InvariantCulture), 0, 10, MsBlack);
            }

            string state = !online ? "icon_party_off.bmp" : m.AccountId == me ? "icon_party_me.bmp" : "icon_party_on.bmp";
            w.DrawBitmap((int) (width * 0.8f), (int) (rowH * 0.65f + rowY), MsParty + state);
        }

        private void DrawMsFriendRow(RoClassicWindow w, RoClassicGdiFont regular, RoFriendRuntimeSnapshot f, int i, int x0, int rowY) {
            string name = string.IsNullOrEmpty(f.Name) ? RoClassicUiHost.MsgStr(0x245, "Nameless") : f.Name;
            if (i == msSelected) w.FillRect(x0 + 17, rowY + 4, regular.Measure(name) + 3, 16, MsSelected);
            w.TextOutPlain(x0 + 19, rowY + 6, name, regular, MsBlack);
            if (f.IsOnline) w.DrawBitmap(x0 + 2, rowY + 4, MsBasic + "grp_online.bmp");
        }

        private static string MsFit(string text, RoClassicGdiFont font, int width) {
            if (string.IsNullOrEmpty(text) || font.Measure(text) <= width) return text;
            for (int n = text.Length - 1; n > 0; n--) {
                string cut = text.Substring(0, n) + "...";
                if (font.Measure(cut) <= width) return cut;
            }

            return "...";
        }

        // ---- input (§1g, §1h) ----

        /// <summary>Hit test 0x6b4c00: 1 &lt; x ≤ W − 2, 16 &lt; y &lt; rows·rowH + 17 → the row index from the first shown.</summary>
        private int MsRowAt(Vector2Int local) {
            int rowH = MsRowH;
            if (local.x <= 1 || local.x > classicMs.Width - 2 || local.y <= 16 || local.y >= MsVisibleRows * rowH + 17) return -1;
            int row = (local.y - 17) / rowH + msFirstRow;
            return row < MsCount ? row : -1;
        }

        private void OnMsPressed(Vector2Int local) {
            if (local.y < 17) return;
            int h = classicMs.Height;
            // the radios (Friend 48×12 at (4, H − 16), Party at (64, H − 16)): msg 6 / 0xd3
            if (local.y >= h - 16 && local.y < h - 4) {
                if (local.x >= 4 && local.x < 52) {
                    SelectMsTab(RoPartyFriendsTab.Friends);
                    return;
                }

                if (local.x >= 64 && local.x < 110) {
                    SelectMsTab(RoPartyFriendsTab.Party);
                    return;
                }
            }

            if (local.x >= classicMs.Width - 15 && local.y >= 17 && local.y < h - 41) {
                PressMsScrollBar(local.y - 17, h - 58);
                return;
            }

            int row = MsRowAt(local);
            if (row != msSelected) {
                msSelected = row;
                classicMs.Invalidate();
            }
        }

        private void PressMsScrollBar(int y, int barH) {
            int visible = MsVisibleRows, range = Math.Max(0, MsCount - visible);
            if (range == 0) return;
            const int b = 13;
            int len = Math.Max(1, (barH - 2 * b) * visible / Math.Max(1, MsCount));
            int top = b + msFirstRow * (barH - 2 * b - len) / range;
            ScrollMs(y < b ? -1 : y >= barH - b ? 1 : y < top ? -(visible - 1) : y >= top + len ? visible - 1 : 0);
        }

        private void SelectMsTab(RoPartyFriendsTab tab) {
            if (model.ActiveTab != tab) {
                model.ShowTab(tab);
                msSelected = -1;
                msFirstRow = 0;
            }

            LayoutClassicMessenger();
        }

        /// <summary>LBtnDblClk (any y): the 1:1 chat with the selected row (not oneself), unless locked.</summary>
        private void MsWhisperSelected() {
            if (Locked || msSelected < 0 || msSelected >= MsCount) return;
            uint me = model.LocalAccountId;
            if (MsPartyTab) {
                RoPartyMemberRuntimeSnapshot m = msParty.Members[msSelected];
                if (m.AccountId != me) OpenWhisper(m.Name);
            } else {
                OpenWhisper(msFriends[msSelected].Name);
            }
        }

        private void OnMsButtonClicked(RoClassicChildLayout child) {
            bool lockedOut = Locked && (child.Id == MsChatId || child.Id == MsInfoId || child.Id == MsSetupId || child.Id == MsDeleteId ||
                child.Id == MsCreateId || child.Id == MsLeaveId || child.Id == MsInviteId || child.Id == MsSortId);
            if (lockedOut) return;
            switch (child.Id) {
                case MsCloseId:
                    HideClassicMessenger();
                    break;
                case MsCompactId:
                    msViewMode = 1;
                    msHeight = 179;
                    PlayerPrefs.SetInt(MsPrefs + "view", 1);
                    LayoutClassicMessenger();
                    break;
                case MsNormalId:
                    msViewMode = 0;
                    msHeight = 326;
                    PlayerPrefs.SetInt(MsPrefs + "view", 0);
                    LayoutClassicMessenger();
                    break;
                case MsChatId:
                    MsWhisperSelected();
                    break;
                case MsSetupId:
                    ClassicSetupButton();
                    break;
                case MsDeleteId:
                    MsConfirmDeleteFriend();
                    break;
                case MsLockId:
                    model.ToggleLock();
                    LayoutMsButtons();
                    break;
                case MsCreateId:
                    ClassicCreateButton();
                    break;
                case MsLeaveId:
                    // MsgStr 0x165 "Are you sure that you want to leave?" → mode->Msg(0x3d)
                    RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x165, "Are you sure that you want to leave?"), RoClassicMessageBox.TypeOkCancel, true, result => {
                        if (result == RoClassicMessageBox.IdYes) _ = model.LeavePartyAsync();
                    });
                    break;
                case MsInviteId:
                    if (model.IsLocalLeader(msParty)) ClassicInviteButton();
                    else RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x816, "Only the leader can invite."), RoClassicMessageBox.TypeOk, true, null);
                    break;
                case MsSortId:
                    SortClassicMinis();
                    break;
            }
        }

        private void MsConfirmDeleteFriend() {
            if (MsPartyTab || msSelected < 0 || msSelected >= msFriends.Count) return;
            RoFriendRuntimeSnapshot friend = msFriends[msSelected];
            // MsgStr 0x164 "Are you sure that you want to delete it?" → CZ_DELETE_FRIENDS
            RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x164, "Are you sure that you want to delete it?"), RoClassicMessageBox.TypeOkCancel, true, result => {
                if (result == RoClassicMessageBox.IdYes) _ = model.RemoveFriendAsync(friend);
            });
        }

        /// <summary>RBtnUp 0x6b7c70 → msg 0x31: the row's context menu (§3b), unless locked.</summary>
        private void OnMsRightClicked(Vector2Int local) {
            if (Locked) return;
            int row = MsRowAt(local);
            if (row < 0) return;
            msSelected = row;
            classicMs.Invalidate();
            uint me = model.LocalAccountId;
            var actions = new List<Action>();
            RoClassicMenu.Begin();
            void Item(int msgId, string fallback, Action action) {
                RoClassicMenu.Add(RoClassicUiHost.MsgStr(msgId, fallback));
                actions.Add(action);
            }

            if (MsPartyTab) {
                RoPartyMemberRuntimeSnapshot m = msParty.Members[row];
                bool self = m.AccountId == me, leader = model.IsLocalLeader(msParty), online = RoPartyFriendsModel.IsOnlinePartyMember(m);
                Item(0xaf8, "Send a mail", () => OpenMail(m.Name));
                if (!self) Item(0x168, "Open 1:1 Chat", () => OpenWhisper(m.Name));
                if (!self && leader) {
                    Item(0x61, "Expel from party", () => RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x16b, "Are you sure that you want to expel?"),
                        RoClassicMessageBox.TypeOkCancel, true, r => {
                            if (r == RoClassicMessageBox.IdYes) _ = model.ExpelPartyMemberAsync(m);
                        }));
                    Item(0x5fc, "Assign Party Leader", () => {
                        if (!online) {
                            // 0x6b93ad: type 3 "Party Chat", red
                            Chat(RoClassicUiHost.MsgStr(0x5fe, "Unable to assign a party leader to a member who is offline."), "#FF0000", RoChatLineTypes.Party);
                            return;
                        }

                        RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x5fd, "Switch Party Leader?"), RoClassicMessageBox.TypeOkCancel, true, r => {
                            if (r == RoClassicMessageBox.IdYes) _ = model.ChangePartyLeaderAsync(m);
                        });
                    });
                }

                if (self) {
                    Item(0x60, "Leave party", () => RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x165, "Are you sure that you want to leave?"),
                        RoClassicMessageBox.TypeOkCancel, true, r => {
                            if (r == RoClassicMessageBox.IdYes) _ = model.LeavePartyAsync();
                        }));
                }

                if (online) {
                    Item(0xc1d, "Add small party window", () => DetachMember(m));
                    Item(0xc1e, "Remove small party window", () => RemoveDetachedMember(m.AccountId));
                }
            } else {
                RoFriendRuntimeSnapshot f = msFriends[row];
                Item(0x168, "Open 1:1 Chat", () => OpenWhisper(f.Name));
                Item(0xaf8, "Send a mail", () => OpenMail(f.Name));
                Item(0x15f, "Delete", MsConfirmDeleteFriend);
                if (f.IsOnline && model.IsLocalLeader(msParty) && !MsInParty(f.Name)) Item(0x532, "Request to Join Party", () => model.InviteByName(f.Name));
            }

            Vector2Int mouse = system.Windows.Mouse;
            Vector2Int client = system.ClientSize;
            RoClassicMenu.Open(Mathf.Clamp(mouse.x + 1, 1, client.x), Mathf.Clamp(mouse.y + 1, 1, client.y), item => {
                if (item >= 0 && item < actions.Count) actions[item]();
            });
        }

        private bool MsInParty(string name) {
            if (msParty.Members == null) return false;
            foreach (RoPartyMemberRuntimeSnapshot m in msParty.Members) {
                if (string.Equals(m.Name, name, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>Per frame while shown: in the party tab the selected row follows the mouse (Hover 0x6b78f0).</summary>
        private void TickClassicMessenger() {
            if (!MsShown || !MsPartyTab || RoClassicMenu.IsOpen) return;
            if (system.Windows.HoverWindow != classicMs) return;
            int row = MsRowAt(system.Windows.Mouse - classicMs.Position);
            if (row != msSelected) {
                msSelected = row;
                classicMs.Invalidate();
            }
        }

        // ---- the rows' actions ----

        /// <summary>Open 1:1 Chat: the classic UIWhisperWnd with the name.</summary>
        private void OpenWhisper(string characterName) {
            if (string.IsNullOrWhiteSpace(characterName)) return;
            RoClassicWhisper.Bind(model.Session, null);
            RoClassicWhisper.OpenManual(characterName, 0);
        }

        /// <summary>Send a mail: UIRodexComposeWnd with the receiver.</summary>
        private static void OpenMail(string characterName) {
            if (string.IsNullOrWhiteSpace(characterName)) return;
            RoClassicRodex.OpenWriteTo(characterName.Trim());
        }

        /// <summary>Add small party window (0xc1d): an online member gets its UIMiniPartyWnd (not while locked).</summary>
        private void DetachMember(RoPartyMemberRuntimeSnapshot member) {
            if (!model.Detach(member)) return;
            ClassicMiniAttach(member);
            classicMs?.Invalidate();
        }

        /// <summary>Remove small party window (0xc1e).</summary>
        private void RemoveDetachedMember(uint accountId) {
            model.RemoveDetached(accountId);
            ClassicMiniRemove(accountId);
            classicMs?.Invalidate();
        }

        // ---- diagnostics console (RoSkillSweepRunner "!pf ...") ----

        /// <summary>open [friend] | close | tab friend|party | press x y | right x y | pick n | dbl | button id | dlg ... | mini ... | state.</summary>
        public static string Sweep(RoLoginShellSession loginSession, string command) {
            RoClassicPartyFriends p = Ensure(loginSession, null);
            return p != null ? p.SweepClassicMessenger(command) : "no session / classic UI";
        }

        private string SweepClassicMessenger(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length ? int.Parse(a[i].StartsWith("0x") ? a[i].Substring(2) : a[i],
                a[i].StartsWith("0x") ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture) : 0;
            if (a[0] == "dlg") return pd.Sweep(a);
            if (a[0] == "mini") return SweepClassicMini(string.Join(" ", a, 1, a.Length - 1));
            if (a[0] == "open") ShowMessenger(a.Length > 1 && a[1] == "friend" ? RoPartyFriendsTab.Friends : RoPartyFriendsTab.Party);
            else if (a[0] == "close") HideClassicMessenger();
            if (classicMs == null) return "no classic messenger";
            switch (a[0]) {
                case "tab":
                    SelectMsTab(a.Length > 1 && a[1] == "friend" ? RoPartyFriendsTab.Friends : RoPartyFriendsTab.Party);
                    break;
                case "press":
                    OnMsPressed(new Vector2Int(Arg(1), Arg(2)));
                    break;
                case "right":
                    OnMsRightClicked(new Vector2Int(Arg(1), Arg(2)));
                    break;
                case "pick":
                    RoClassicMenu.PickForTests(Arg(1));
                    break;
                case "dbl":
                    MsWhisperSelected();
                    break;
                case "button":
                    foreach (RoClassicChildLayout child in classicMs.Layout.Children) {
                        if (child.Id == Arg(1) && child.Visible) {
                            OnMsButtonClicked(child);
                            break;
                        }
                    }

                    break;
            }

            var s = new StringBuilder();
            s.Append("shown=").Append(MsShown).Append(" tab=").Append(model.ActiveTab).Append(" view=").Append(msViewMode)
                .Append(" size=").Append(classicMs.Width).Append('x').Append(classicMs.Height).Append(" pos=").Append(classicMs.Position)
                .Append(" rows=").Append(MsCount).Append('/').Append(MsVisibleRows).Append(" sel=").Append(msSelected).Append(" locked=").Append(Locked)
                .Append(" party='").Append(msParty.PartyName).Append("' menu=").Append(RoClassicMenu.Describe())
                .Append(" status='").Append(model.LastCommandStatus).Append('\'');
            return s.ToString();
        }
    }
}
