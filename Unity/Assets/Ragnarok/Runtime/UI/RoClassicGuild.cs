using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Entities;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The guild manager (codex-ui-research RUNTIME-RULES-GUILD.md "info-members-ranks"): wrapper 0x3b opens the saved
    /// tab; UIGuildInfoManageWnd 0x3c, UIGuildMemberManageWnd 0x3d and UIGuildPositionManageWnd 0x3e are separate 400 × 317
    /// UIGuildWnd frames at one shared place. The common frame (vf20 0x8c1100): the 0,17,W,24 #B4B4B4 strip, six 64 × 23 tabs
    /// from x 1 every 66 px (the open one white, the rest #C8C8C8), the white body from y 41 and the btnbar strip. The common
    /// OnCreate FUN_008b97a0 sends CZ_REQ_GUILD_MENUINTERFACE while the tab mask is still -1; a tab click in y 17..41 goes
    /// through the mask (bits 1 members, 2 positions, 0x10 expelled, 0x80 notice; skills unchecked).
    /// </summary>
    public sealed partial class RoClassicGuild : IRoClassicUiProducer {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string Ui = "유저인터페이스\\";
        private const string Prefs = "ro.ui.classic.guild.";
        private const int CloseId = 0xc5, OkId = 0xb4, CancelId = 0xb5, DisbandId = 0x1bd;
        private const string Basic = Ui + "basic_interface\\";
        private const string DropButton = Basic + "txtbox_btn_a.bmp";
        private static readonly Color32 EditFill = new Color32(0xf2, 0xf2, 0xf2, 255);
        private const int TabInfo = 0, TabMembers = 1, TabPositions = 2, TabSkills = 3, TabBans = 4, TabNotice = 5, SendId = 0x111;
        private const int SkillRowH = 34, SkillRows = 7, BanRowH = 19, BanRows = 11, SkillUpId = 0xeb;
        private const string SkillIcons = "유저인터페이스\\item\\";
        private static readonly Color32 SelectedUnlearned = new Color32(0xb4, 0xb4, 0xb4, 255);
        private static readonly Color32 SelectedNoSp = new Color32(0x70, 0xd7, 0xed, 255);
        private static readonly Color32 Unlearned = new Color32(0x78, 0x78, 0x78, 255);
        private const int MemberRowH = 44, MemberRows = 5, PositionRowH = 19, PositionRows = 11;
        private static readonly int[] MemberColumns = { 98, 65, 53, 39, 39, 76 };
        private static readonly int[] PositionColumns = { 50, 100, 60, 60, 60, 60 };
        private static readonly int[] TabMaskBits = { 0, 1, 2, 0, 0x10, 0x80 };
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Gray = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static readonly Color32 Selected = new Color32(0x70, 0x9f, 0xed, 255);
        private static readonly Color32 Online = new Color32(0xe2, 0xff, 0xe4, 255);
        private static readonly Color32 SortedHeader = new Color32(0xce, 0xd8, 0xeb, 255);

        private static RoClassicGuild instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private readonly RoClassicWindow[] windows = new RoClassicWindow[6];
        private RoClassicGuildState guild = RoClassicGuildState.None;
        private int seenGuild = -1;
        private int memberFirst, memberSelected = 10, sortColumn = -1;
        private readonly Dictionary<int, int> sortDirection = new Dictionary<int, int>();
        private int positionFirst, positionSelected = -1;
        private int skillFirst, skillSelected = -1, banFirst, banSelected = -1;
        private RoGuildMember[] memberRows = Array.Empty<RoGuildMember>();
        private bool showLoginStatus;
        private readonly HashSet<string> portraits = new HashSet<string>();
        private readonly HashSet<string> missingPortraits = new HashSet<string>();
        private RoAssetDatabase portraitAssets;
        private RoActorAppearanceTables appearance;
        private RoJobMetadataTable jobs;
        private string portraitError = string.Empty;
        private RoClassicChat chat;
        private int seenEvent = -1;
        private int emblemSerial = -1, seenBelongVersion = -1;
        private RoGuildInfo seenInfo;
        // the master's unsent changes: positions by id (vf37 0x8c55d0), member positions by char id (vf37 0x8c3b40)
        private readonly Dictionary<int, RoGuildPosition> pendingPositions = new Dictionary<int, RoGuildPosition>();
        private readonly Dictionary<uint, (uint AccountId, uint CharId, int PositionId)> pendingMembers =
            new Dictionary<uint, (uint AccountId, uint CharId, int PositionId)>();
        private readonly RoClassicEditField[] positionNames = new RoClassicEditField[PositionRows];
        private readonly RoClassicEditField[] positionTaxes = new RoClassicEditField[PositionRows];
        private RoClassicWindow combo;
        private int comboMember = -1, comboRow, comboFirst, comboHover = -1;
        private RoClassicWindow leaveForm, banForm;
        private RoClassicEditField leaveReason, banReason;
        private (uint AccountId, uint CharId) banTarget;

        public static void Tick(RoLoginShellSession session, RoClassicChat chat) {
            if (session == null) return;
            RoClassicGuild g = Ensure();
            if (g == null) return;
            g.session = session;
            g.chat = chat;
            (RoClassicGuildState state, int serial) = session.GuildSnapshot();
            if (serial == g.seenGuild) return;
            g.seenGuild = serial;
            g.guild = state;
            if (g.seenEvent >= 0 && state.EventSerial != g.seenEvent) g.OnGuildEvent(state);
            g.seenEvent = state.EventSerial;
            g.RebuildMemberRows();
            g.RebuildCastleRows();
            // 0x016c's and the guild info's emblem versions (0x5eb7f0), each when its packet came (the later one wins)
            if (state.EmblemVersion != g.seenBelongVersion) {
                g.seenBelongVersion = state.EmblemVersion;
                RoClassicGuildEmblems.SetVersion(state.GuildId, state.EmblemVersion);
            }

            if (state.Info != null && !ReferenceEquals(state.Info, g.seenInfo)) {
                g.seenInfo = state.Info;
                if (state.Info.EmblemVersion > 0) RoClassicGuildEmblems.SetVersion(state.GuildId, state.Info.EmblemVersion);
            }
            if (!state.HasGuild) {
                g.pendingPositions.Clear();
                g.pendingMembers.Clear();
                g.CloseAll();
            }

            g.SyncPositionEdits();
            g.SyncOk();
            g.SyncSkillButtons();
            foreach (RoClassicWindow w in g.windows) w?.Invalidate();
        }

        /// <summary>
        /// 0x0a83 / 0x0a82 (FUN_00c6b4b0 / FUN_00c6a0f0): the local character's own leave 0x1e9 / expulsion 0x1ea in red;
        /// another member's 0x16c / 0x172 with the name and 0x16d / 0x173 with the reason in yellow.
        /// </summary>
        private void AnnounceEvent(RoClassicGuildState state) {
            bool left = state.EventKind == RoGuildUpdateKind.Left;
            if (!state.HasGuild) {
                Chat(left ? Msg(0x1e9, "You left the guild.") : Msg(0x1ea, "You have been expelled from the Guild."), "#FF0000", RoChatLineTypes.Guild);
                return;
            }

            string name = string.IsNullOrEmpty(state.EventName) ? Msg(0x245, "Nameless") : state.EventName;
            Chat(Format(left ? Msg(0x16c, "%s has withdrawn from the guild.") : Msg(0x172, "%s has been expelled from our guild."), name), "#FFFF00", RoChatLineTypes.Guild);
            Chat(Format(left ? Msg(0x16d, "% Reason: %s") : Msg(0x173, "% Expulsion Reason: %s"), state.EventReason), "#FFFF00", RoChatLineTypes.Guild);
        }

        private static string Format(string message, string value) {
            int at = message.IndexOf("%s", StringComparison.Ordinal);
            return at < 0 ? message : message.Substring(0, at) + value + message.Substring(at + 2);
        }

        /// <summary>A line in the chat window of the caller's chat type (0 "Public" by default; the guild handlers' lines are type 4).</summary>
        private void Chat(string text, string color, int type = RoChatLineTypes.Public) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddTypedLine(text, color, type);
        }

        private bool Master => session != null && guild.IsMaster(session.LocalCharacterIds().CharId);

        private static RoClassicGuild Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicGuild { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Alt+G / the Basic Info guild button: MakeWindow(0x3b) toggles the saved tab.</summary>
        public static bool Toggle() {
            RoClassicGuild g = Ensure();
            if (g == null || g.session == null) return false;
            if (Array.Exists(g.windows, w => w != null && w.Visible)) {
                g.CloseAll();
                return true;
            }

            if (!g.guild.HasGuild) {
                g.ToggleHelper();
                return true;
            }

            int tab = PlayerPrefs.GetInt(Prefs + "tab", TabInfo);
            g.Open(tab < 0 || tab > TabNotice ? TabInfo : tab);
            return true;
        }

        private void CloseAll() {
            foreach (RoClassicWindow w in windows) {
                if (w != null) w.Visible = false;
            }

            CloseCombo();
            HideCastleMenu();
            CloseEmblemList();
        }

        // ------------------------------------------------------------------ frame and tabs

        private RoClassicWindow Build(int tab) {
            string layoutName = tab == TabPositions ? "UIGuildPositionManageWnd" : tab == TabSkills ? "UIGuildSkillWnd" : tab == TabBans ? "UIGuildBanishedMemberWnd" : tab == TabNotice ? "UIGuildNoticeWnd" : "UIGuildMemberManageWnd";
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + layoutName).text);
            // the open tab is white (colorchip 2,2), the others #C8C8C8 (14,6)
            foreach (RoClassicOp op in layout.Ops) {
                if (op.Kind == RoClassicOpKind.Fill && op.Y == 18 && op.W == 64 && op.H == 23) op.Color = (op.X - 1) / 66 == tab ? White : Gray;
            }

            if (tab == TabInfo) {
                // no capture of 0x3c: the member frame without its grid, headers and row controls
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text || (op.Kind == RoClassicOpKind.Fill && op.Y >= 46 && op.Y < 288));
                for (int i = 7; i < layout.Children.Count; i++) layout.Children[i].Visible = false;
                // vf15 0x8ba8a0: the master's Disband 0x1bd (btn_disBand) at (W − w − 0xf, H − h − 5); the emblem Edit 0x112 (RoClassicGuild.Emblem)
                const string disband = Ui + "btn_disBand";
                if (system.Images.TryGet(disband + ".bmp", out RoClassicUiImage image)) {
                    var button = new RoClassicChildLayout {
                        Class = "UIBitmapButton", Id = DisbandId, X = -200, Y = -200, W = image.Width, H = image.Height, Kind = "control",
                        Path = "UIGuildInfoManageWnd/UIBitmapButton#disband"
                    };
                    button.States["normal"] = disband + ".bmp";
                    button.States["hover"] = disband + "_a.bmp";
                    button.States["pressed"] = disband + "_b.bmp";
                    layout.Children.Add(button);
                }

                AddEmblemEdit(layout);
            } else if (tab == TabMembers) {
                // the headers (with the sort mark), the row texts and the scroll bar are drawn here
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIStaticText" && child.Y > 22 || child.Class == "UIScrollBar" || child.Id == 0xb4) child.Visible = false;
                }
            } else if (tab == TabNotice) {
                // the captured texts ("Title:", "Contents", the counter) are drawn here
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIEditCtrlVarId" || child.Class == "UIEditCtrl3") child.Visible = false;
                }
            } else if (tab == TabSkills || tab == TabBans) {
                // the captured texts (headers, "SkillPoint : 0") are drawn here
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
            }

            if (tab == TabNotice) {
                // vf15 0x8bc570: the title (50, 66, 340, 16) up to 60 bytes, the body UIEditCtrl3 (5, 104, 390, 156) up to 119
                noticeTitle = new RoClassicEditField { Rect = new RectInt(50, 66, 340, 16), Background = EditFill, MaxBytes = 0x3c };
                noticeBody = new RoClassicEditField { Rect = new RectInt(5, 104, 390, 156), Background = EditFill, MaxBytes = 0x77, Multiline = true };
                noticeBody.Changed += _ => windows[TabNotice]?.Invalidate();
            }

            if (tab == TabSkills) {
                // the arrows 0xf9..0x106 stay parked (FUN_008c2730); the up buttons follow the rows
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIScrollBar" || child.Id >= 0xf9 && child.Id <= 0x106) child.Visible = false;
                }
            } else {
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIStaticText" && child.Y > 22 || child.Class == "UIScrollBar" || child.Id == 0xb4) child.Visible = false;
                }
            }

            var w = new RoClassicWindow(system, layout) { Visible = false, EscClose = () => { CloseAll(); return true; } };
            if (tab == TabPositions) {
                // master only (vf15 0x8bcc30): the title edit 0x10f (col 1 − 4 by 16) and the tax edit 0x110 (col 4 / 2 − 4 by 16), #F2F2F2
                for (int r = 0; r < PositionRows; r++) {
                    int row = r;
                    positionNames[r] = new RoClassicEditField { Rect = new RectInt(-200, -200, PositionColumns[1] - 4, 16), Background = EditFill, MaxBytes = 23, Visible = false };
                    positionNames[r].Changed += field => PositionNameChanged(row, field.Text);
                    w.AddEditField(positionNames[r]);
                    positionTaxes[r] = new RoClassicEditField { Rect = new RectInt(-200, -200, PositionColumns[4] / 2 - 4, 16), Background = EditFill, MaxBytes = 2, Visible = false };
                    positionTaxes[r].Changed += field => PositionTaxChanged(row, field);
                    w.AddEditField(positionTaxes[r]);
                }
            }
            w.Moved += moved => {
                PlayerPrefs.SetInt(Prefs + "x", moved.Position.x);
                PlayerPrefs.SetInt(Prefs + "y", moved.Position.y);
            };
            w.Clicked += child => {
                if (child.Id == CloseId) CloseAll();
                else if (child.Id == 0xd1 && tab == TabMembers) ToggleLoginStatus(w);
                else if (child.Id == OkId && tab == TabMembers) SendMemberPositions();
                else if (child.Id == OkId && tab == TabPositions) SendPositions();
                else if (child.Id == OkId && tab == TabSkills) UseSkill();
                else if (child.Id == DisbandId && tab == TabInfo) OpenNameForm(true);
                else if (child.Id == EmblemEditId && tab == TabInfo) PressEmblemEdit();
                else if (child.Id == SendId && tab == TabNotice) SendNotice();
                else if (tab == TabSkills && child.Id >= SkillUpId && child.Id < SkillUpId + SkillRows) SkillUp(child.Id - SkillUpId);
            };
            w.Pressed += (pw, local) => {
                if (local.y > 16 && local.y < 42) {
                    PickTab(local.x / 66);
                    return;
                }

                if (tab == TabInfo) CastlePressed(pw, local, false);
                else if (tab == TabMembers) MemberPressed(local);
                else if (tab == TabPositions) PositionPressed(local);
                else if (tab == TabSkills) SkillPressed(local);
                else if (tab == TabBans) BanPressed(local);
            };
            w.Wheeled += (ww, local, delta) => {
                if (tab == TabInfo) CastleWheel(ww, local, delta);
                else if (tab == TabMembers) ScrollMembers(delta > 0 ? -3 : 3);
                else if (tab == TabPositions) ScrollPositions(delta > 0 ? -3 : 3);
                else if (tab == TabSkills) ScrollSkills(delta > 0 ? -3 : 3);
                else if (tab == TabBans) ScrollBans(delta > 0 ? -3 : 3);
                else if (tab == TabNotice && noticeBody.LineCount > noticeBody.VisibleLines) noticeBody.ScrollLines(delta > 0 ? -3 : 3);
            };
            if (tab == TabNotice) {
                w.AddEditField(noticeTitle);
                w.AddEditField(noticeBody);
            }

            if (tab == TabMembers) w.RightClicked += (rw, local) => MemberRightClicked(local);
            if (tab == TabInfo) w.RightClicked += (rw, local) => CastlePressed(rw, local, true);
            if (tab == TabInfo) w.DrawDynamic += DrawInfo;
            else if (tab == TabMembers) w.DrawDynamic += DrawMembers;
            else if (tab == TabSkills) w.DrawDynamic += DrawSkills;
            else if (tab == TabBans) w.DrawDynamic += DrawBans;
            else if (tab == TabNotice) w.DrawDynamic += DrawNotice;
            else w.DrawDynamic += DrawPositions;
            return w;
        }

        /// <summary>vf25 0x8c18c0 → Msg 0x45 → vf37 0x8c6510: another allowed tab replaces this one at the same place.</summary>
        private void PickTab(int tab) {
            if (tab < 0 || tab > 5 || windows[tab] != null && windows[tab].Visible) return;
            if (TabMaskBits[tab] != 0 && (guild.Mask == -1 || (guild.Mask & TabMaskBits[tab]) == 0)) return;
            Open(tab);
        }

        private void Open(int tab) {
            Vector2Int at = new Vector2Int(PlayerPrefs.GetInt(Prefs + "x", 0), PlayerPrefs.GetInt(Prefs + "y", 34));
            foreach (RoClassicWindow other in windows) {
                if (other != null && other.Visible) {
                    at = other.Position;
                    other.Visible = false;
                }
            }

            if (windows[tab] == null) windows[tab] = Build(tab);
            RoClassicWindow w = windows[tab];
            Vector2Int client = system.ClientSize;
            if (at.x < 0 || at.y < 0 || at.x + w.Width > client.x || at.y + w.Height > client.y) at = new Vector2Int(0, 34);
            w.MoveTo(at.x, at.y);
            w.Visible = true;
            system.Windows.BringToFront(w);
            w.Invalidate();
            PlayerPrefs.SetInt(Prefs + "tab", tab);
            if (tab == TabNotice) SeedNotice();
            SyncSkillButtons();
            SyncPositionEdits();
            SyncOk();
            OnCreate(tab);
        }

        /// <summary>The OnCreate requests: the mask while unknown, then CZ_REQ_GUILD_MENU (an extra 0 while no master name).</summary>
        private void OnCreate(int tab) {
            if (session == null) return;
            if (guild.Mask == -1) _ = session.BeginLegacyGuildMenuInterface();
            if (string.IsNullOrEmpty(MasterName())) _ = session.BeginLegacyGuildMenu(0);
            switch (tab) {
                case TabInfo:
                    // vf15 0x8ba8a0: expelled 4, info 0, members 1
                    _ = session.BeginLegacyGuildMenu(4);
                    _ = session.BeginLegacyGuildMenu(0);
                    _ = session.BeginLegacyGuildMenu(1);
                    break;
                case TabMembers:
                    _ = session.BeginLegacyGuildMenu(1);
                    break;
                case TabPositions:
                    _ = session.BeginLegacyGuildMenu(2);
                    break;
                case TabSkills:
                    _ = session.BeginLegacyGuildMenu(3);
                    break;
                case TabBans:
                    _ = session.BeginLegacyGuildMenu(4);
                    break;
            }
        }

        /// <summary>DAT_014efaa8: 0x0b7b's master name, else the member list's name for its master char id.</summary>
        private string MasterName() {
            RoGuildInfo info = guild.Info;
            if (info == null) return string.Empty;
            if (!string.IsNullOrEmpty(info.MasterName)) return info.MasterName;
            foreach (RoGuildMember m in guild.Members) {
                if (m.CharId == info.MasterCharId) return m.Name;
            }

            return string.Empty;
        }

        // ------------------------------------------------------------------ 0x3c

        /// <summary>OnDraw 0x8bf430.</summary>
        private void DrawInfo(RoClassicWindow w) {
            RoGuildInfo info = guild.Info ?? new RoGuildInfo();
            string name = string.IsNullOrEmpty(info.Name) ? guild.Name : info.Name;
            w.TextOut(8, 49, Msg(0x148, "Guild Name") + " : " + name, 0, 12, Black);
            w.TextOut(8, 65, Msg(0x149, "Guild Level") + " : " + Num(info.Level), 0, 12, Black);
            string master = MasterName();
            w.TextOut(8, 81, Msg(0x14a, "Guild Master") + " : " + (string.IsNullOrEmpty(master) ? Msg(0x245, "Nameless") : master), 0, 12, Black);
            // members listed / max, then grp_online.bmp and the online count (langtype 1: after the text)
            string members = Msg(0x14b, "Members") + " : " + Num(guild.Members.Length) + " / " + Num(info.MaxMembers);
            w.TextOut(8, 97, members, 0, 12, Black);
            int x = w.FontFor(0, 12).Measure(members) + 16;
            const string onlineIcon = Ui + "basic_interface\\grp_online.bmp";
            if (w.DrawBitmap(x, 95, onlineIcon) && system.Images.TryGet(onlineIcon, out RoClassicUiImage icon)) x += icon.Width;
            w.TextOut(x, 97, " " + Num(info.ConnectedMembers), 0, 12, Black);
            w.TextOut(8, 113, Msg(0x14c, "Avg.lvl of Members") + " : " + Num(info.AverageLevel), 0, 12, Black);
            w.TextOut(8, 129, Msg(0x14d, "Castles Owned") + " : " + info.ManageLand, 0, 12, Black);
            // UIListBoxAgit 0x22a covers the manage land from "Castles Owned" + 19 on
            DrawCastles(w);
            // EXP in red from level 50
            w.TextOut(200, 49, Msg(0x14f, "EXP") + " : " + Num(info.Exp), 0, 12, info.Level > 49 ? Red : Black);
            w.TextOut(200, 73, Msg(0x150, "Emblem"), 0, 12, Black);
            // the emblem box (299, 64) 26 × 26, always; the emblem (300, 65) at its own size once cached (0x5eada0)
            w.FillRect(299, 64, 26, 26, Selected);
            string emblem = RoClassicGuildEmblems.Resolve(guild.GuildId);
            if (emblem != null) w.DrawBitmap(300, 65, emblem);
            w.TextOut(200, 139, Msg(0x152, "Alliances"), 0, 12, Black);
            DrawRelations(w, 155, 0);
            w.TextOut(200, 213, Msg(0x153, "Antagonists"), 0, 12, Black);
            DrawRelations(w, 229, 1);
        }

        /// <summary>The #C8C8C8 200,y,168,48 box with up to three related guilds on 16 px rows from (204, y + 2).</summary>
        private void DrawRelations(RoClassicWindow w, int y, int relation) {
            w.FillRect(200, y, 168, 48, Gray);
            int row = y + 2;
            foreach (RoGuildRelation r in guild.Relations) {
                if (r.Relation != relation) continue;
                if (row > y + 49) break;
                w.TextOut(204, row, r.Name, 0, 12, Black);
                row += 16;
            }
        }

        // ------------------------------------------------------------------ 0x3d

        private void RebuildMemberRows() {
            var rows = new List<RoGuildMember>(guild.Members);
            if (sortColumn >= 0) {
                int direction = sortDirection.TryGetValue(sortColumn, out int d) && d == 2 ? -1 : 1;
                rows.Sort((a, b) => direction * CompareMembers(a, b, sortColumn));
            }

            memberRows = rows.ToArray();
            memberFirst = Mathf.Clamp(memberFirst, 0, Math.Max(0, memberRows.Length - MemberRows));
        }

        private int CompareMembers(RoGuildMember a, RoGuildMember b, int column) {
            switch (column) {
                case 0: return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                case 1: return a.PositionId.CompareTo(b.PositionId);
                case 2: return a.Job.CompareTo(b.Job);
                case 3: return a.Level.CompareTo(b.Level);
                case 4: return PayRate(a).CompareTo(PayRate(b));
                default: return a.ContributionExp.CompareTo(b.ContributionExp);
            }
        }

        private int PayRate(RoGuildMember m) =>
            m.PositionId >= 0 && m.PositionId < guild.Positions.Length ? guild.Positions[m.PositionId].PayRate : 0;

        /// <summary>OnDraw 0x8bfbd0 with the row helper FUN_008b8200 (rows 44 px, from y 67).</summary>
        private void DrawMembers(RoClassicWindow w) {
            int x = 10;
            int[] headers = { 0x197, 0x1f7, 0x1f8, 0x198, 0x1fa, 0x1fb };
            string[] fallbacks = { "Name", "Position", "Job", "Level", "Tax", "Contribution" };
            int columnX = 5;
            for (int c = 0; c < 6; c++) {
                if (c == sortColumn) {
                    // the sorted column: #CED8EB under its header and guild_arrow_up / _down after the text
                    w.FillRect(columnX + 1, 47, MemberColumns[c], 19, SortedHeader);
                }

                columnX += MemberColumns[c] + 1;
            }

            for (int c = 0; c < 6; c++) {
                string text = Msg(headers[c], fallbacks[c]);
                w.TextOut(x, 51, text, 0, 12, Black);
                if (c == sortColumn) {
                    bool up = !sortDirection.TryGetValue(c, out int d) || d == 1;
                    w.DrawBitmap(x + w.FontFor(0, 12).Measure(text), 53, Ui + "guild\\guild_arrow_" + (up ? "up" : "down") + ".bmp");
                }

                x += MemberColumns[c] + 1;
            }

            for (int r = 0; r < MemberRows; r++) {
                int index = memberFirst + r;
                if (index >= memberRows.Length) break;
                RoGuildMember m = memberRows[index];
                int top = 67 + MemberRowH * r, y = 83 + MemberRowH * r;
                bool online = m.State == 1;
                if (online) w.FillRect(6, top, 375, 43, Online);
                if (index == memberSelected) w.FillRect(6, top, 375, 43, Selected);
                if (online) DrawPortrait(w, m, r);
                string name = string.IsNullOrEmpty(m.Name) ? Msg(0x245, "Nameless") : m.Name;
                w.ClipTo(43, 79 + MemberRowH * r, 51, 15);
                w.TextOut(43, 79 + MemberRowH * r, name, 0, 12, Black);
                w.ClipTo(109, y, 49, 15);
                w.TextOut(109, y, guild.PositionName(MemberPosition(m)), 0, 12, Black);
                w.ClipTo(175, y, 39, 15);
                w.TextOut(175, y, JobName(m.Job), 0, 12, Black);
                w.ResetClip();
                w.TextOutRight(260, y, Num(m.Level), 0, 12, Black);
                w.TextOutRight(300, y, PayRate(m).ToString("0", CultureInfo.InvariantCulture).PadLeft(3) + "%", 0, 12, Black);
                w.TextOutRight(377, y, Num(m.ContributionExp), 0, 12, Black);
            }

            if (Master) {
                // the master's position buttons 0x10d (basic_interface\txtbox_btn_a / _b / _c) at (150, 79 + 44 · row)
                for (int r = 0; r < MemberRows && memberFirst + r < memberRows.Length; r++) w.DrawBitmap(150, 79 + MemberRowH * r, DropButton);
            }

            w.DrawScrollBar(384, 43, 249, Math.Max(0, memberRows.Length - MemberRows), memberRows.Length, MemberRows, memberFirst);
        }

        private int MemberPosition(RoGuildMember m) => pendingMembers.TryGetValue(m.CharId, out var p) ? p.PositionId : m.PositionId;

        private void MemberPressed(Vector2Int local) {
            if (Master && local.x >= 150 && local.x < 150 + 13) {
                int r = (local.y - 79) / MemberRowH;
                if (local.y >= 79 && r < MemberRows && local.y - 79 - MemberRowH * r < 16 && memberFirst + r < memberRows.Length) {
                    memberSelected = memberFirst + r;
                    OpenCombo(memberSelected, r);
                    windows[TabMembers]?.Invalidate();
                    return;
                }
            }

            if (local.x >= 384 && local.x < 397 && local.y >= 43 && local.y < 292) {
                ScrollMembers(local.y < 56 ? -1 : local.y >= 279 ? 1 : local.y < 167 ? -MemberRows : MemberRows);
                return;
            }

            if (local.y >= 47 && local.y < 66 && local.x >= 5 && local.x < 383) {
                // a header: FUN_008c1f00 sorts by it, a second click turns the direction
                int column = 0, edge = 5 + MemberColumns[0] + 1;
                while (column < 5 && local.x >= edge) edge += MemberColumns[++column] + 1;
                sortDirection[column] = sortColumn == column && sortDirection.TryGetValue(column, out int d) && d == 1 ? 2 : 1;
                sortColumn = column;
                RebuildMemberRows();
                windows[TabMembers]?.Invalidate();
                return;
            }

            if (local.x >= 5 && local.x < 383 && local.y >= 66 && local.y < 283) {
                int index = (local.y - 66) / MemberRowH + memberFirst;
                if (index < memberRows.Length) {
                    memberSelected = index;
                    windows[TabMembers]?.Invalidate();
                }
            }
        }

        private void ScrollMembers(int delta) {
            int next = Mathf.Clamp(memberFirst + delta, 0, Math.Max(0, memberRows.Length - MemberRows));
            if (next == memberFirst) return;
            memberFirst = next;
            windows[TabMembers]?.Invalidate();
        }

        /// <summary>Checkbox 0xd1 flips DAT_01553ab4 (no packet).</summary>
        private void ToggleLoginStatus(RoClassicWindow w) {
            showLoginStatus = !showLoginStatus;
            w.SetChildState(0xd1, showLoginStatus ? "checkbox_1" : "checkbox_0");
            w.Invalidate();
        }

        /// <summary>
        /// An online member's head: action 0, frame 0, layer 0 of the hair sprite in the member's hair palette, unscaled,
        /// centred in the 44 px cell ((44 − w) / 2 + 6) from y 70 + 44 · row.
        /// </summary>
        private void DrawPortrait(RoClassicWindow w, RoGuildMember m, int row) {
            string key = PortraitKey(m);
            if (!portraits.Contains(key) && !BuildPortrait(m, key)) return;
            int width = system.Images.TryGet(key, out RoClassicUiImage image) ? image.Width : 0;
            w.DrawBitmap((MemberRowH - width) / 2 + 6, 70 + MemberRowH * row, key);
        }

        private static string PortraitKey(RoGuildMember m) =>
            "@guild-head-" + m.Sex.ToString(CultureInfo.InvariantCulture) + "-" + m.Head.ToString(CultureInfo.InvariantCulture) + "-" +
            m.HeadPalette.ToString(CultureInfo.InvariantCulture);

        private bool BuildPortrait(RoGuildMember m, string key) {
            if (missingPortraits.Contains(key)) return false;
            RoAssetDatabase assets = Assets();
            if (assets == null || !assets.IsInitialized) return false;
            if (!ReferenceEquals(assets, portraitAssets)) {
                portraitAssets = assets;
                appearance = RoActorAppearanceTables.Load(assets);
            }

            var request = new RoActorAppearanceRequest { isMale = m.Sex == 1, headId = m.Head, hairId = m.Head, hairColorId = m.HeadPalette, classId = m.Job };
            try {
                // the head sprite (머리통) is the hair style
                if (!appearance.TryResolveHeadBasePath(assets, request, out string basePath)) {
                    portraitError = key + ": no head path";
                    missingPortraits.Add(key);
                    return false;
                }

                SPR spr = assets.Load<SPR>(basePath + ".spr");
                ACT act = assets.Load<ACT>(basePath + ".act");
                if (spr == null || act == null || !act.TryGetMotion(0, 0, out ACT.Frame frame) || frame.layers == null || frame.layers.Length == 0 ||
                    frame.layers[0].sprType != 0 || !spr.TryGetFrame(frame.layers[0].index, out SPR.Frame image) || image.type != SPR.TYPE_PAL) {
                    portraitError = key + ": no frame in " + basePath;
                    missingPortraits.Add(key);
                    return false;
                }

                byte[] palette = spr.palette;
                if (m.HeadPalette != 0 && appearance.TryResolveHairPalettePath(assets, request, out string palettePath) && assets.Exists(palettePath)) {
                    byte[] custom = assets.ReadBytes(palettePath);
                    if (custom != null && custom.Length >= 1024) palette = custom;
                }

                int width = image.originalWidth > 0 ? image.originalWidth : image.width;
                int height = image.originalHeight > 0 ? image.originalHeight : image.height;
                var pixels = new Color32[width * height];
                for (int i = 0; i < pixels.Length && i < image.data.Length; i++) {
                    int p = image.data[i];
                    if (p == 0) continue;
                    pixels[i] = new Color32(palette[p * 4], palette[p * 4 + 1], palette[p * 4 + 2], 255);
                }

                if (!system.Images.SetDynamic(key, width, height, pixels)) {
                    portraitError = key + ": SetDynamic " + width + "x" + height;
                    return false;
                }

                portraits.Add(key);
                return true;
            } catch (Exception ex) {
                Debug.LogWarning("Guild portrait " + key + ": " + ex.Message);
                missingPortraits.Add(key);
                return false;
            }
        }

        // ------------------------------------------------------------------ 0x3e

        /// <summary>OnDraw 0x8c0260: 11 rows of 19 px from y 68 (rank, title, the three rights as checkbox_%d.bmp, the tax).</summary>
        private void DrawPositions(RoClassicWindow w) {
            RoGuildPosition[] positions = guild.Positions;
            for (int n = 0; n < PositionRows; n++) {
                int index = positionFirst + n;
                if (index >= positions.Length) break;
                RoGuildPosition p = DisplayPosition(index);
                int y = 68 + PositionRowH * n;
                if (index == positionSelected) w.FillRect(5, y - 3, w.Width - 24, 18, Selected);
                w.TextOut(8, y, Num(p.Id), 0, 12, Black);
                bool master = Master;
                if (!master) {
                    w.ClipTo(59, y - 2, 96, 15);
                    w.TextOut(59, y - 2, p.Name, 0, 12, Black);
                    w.ResetClip();
                }

                Checkbox(w, PositionColumns[0] + PositionColumns[1] + 10, y, (p.Rights & 1) != 0);
                Checkbox(w, PositionColumns[0] + 11 + PositionColumns[1] + PositionColumns[2], y, (p.Rights & 0x10) != 0);
                Checkbox(w, PositionColumns[0] + 12 + PositionColumns[2] + PositionColumns[3] + PositionColumns[1], y, (p.Rights & 0x100) != 0);
                if (master) w.TextOut(373, y, "%", 0, 12, Black);
                else w.TextOut(343, y - 2, Num(p.PayRate) + " %", 0, 12, Black);
            }

            w.DrawScrollBar(385, 41, 247, Math.Max(0, positions.Length - PositionRows), positions.Length, PositionRows, positionFirst);
        }

        private static void Checkbox(RoClassicWindow w, int x, int y, bool on) =>
            w.DrawBitmap(x, y, Ui + "checkbox_" + (on ? "1" : "0") + ".bmp");

        private RoGuildPosition DisplayPosition(int index) =>
            pendingPositions.TryGetValue(guild.Positions[index].Id, out RoGuildPosition p) ? p : guild.Positions[index];

        private static readonly int[] RightsBits = { 1, 0x10, 0x100 };

        private static int CheckboxX(int k) =>
            k == 0 ? PositionColumns[0] + PositionColumns[1] + 10 :
            k == 1 ? PositionColumns[0] + 11 + PositionColumns[1] + PositionColumns[2] :
            PositionColumns[0] + 12 + PositionColumns[2] + PositionColumns[3] + PositionColumns[1];

        private void PositionPressed(Vector2Int local) {
            if (Master && local.y >= 68) {
                int n = (local.y - 68) / PositionRowH, index = positionFirst + n;
                if (n < PositionRows && index < guild.Positions.Length && local.y - 68 - PositionRowH * n < 12) {
                    for (int k = 0; k < 3; k++) {
                        int x = CheckboxX(k);
                        if (local.x < x || local.x >= x + 12) continue;
                        // checkbox 0xd1: the row's right into the pending copy
                        RoGuildPosition p = DisplayPosition(index);
                        Pend(new RoGuildPosition(p.Id, p.Rights ^ RightsBits[k], p.Ranking, p.PayRate, p.Name));
                        return;
                    }
                }
            }

            if (local.x >= 385 && local.x < 398 && local.y >= 41 && local.y < 288) {
                ScrollPositions(local.y < 54 ? -1 : local.y >= 275 ? 1 : local.y < 164 ? -PositionRows : PositionRows);
                return;
            }

            if (local.x >= 5 && local.x < 382 && local.y >= 65 && local.y < 65 + PositionRowH * PositionRows) {
                int index = (local.y - 65) / PositionRowH + positionFirst;
                if (index < guild.Positions.Length) {
                    positionSelected = index;
                    windows[TabPositions]?.Invalidate();
                }
            }
        }

        private void ScrollPositions(int delta) {
            int next = Mathf.Clamp(positionFirst + delta, 0, Math.Max(0, guild.Positions.Length - PositionRows));
            if (next == positionFirst) return;
            positionFirst = next;
            SyncPositionEdits();
            windows[TabPositions]?.Invalidate();
        }

        private void Pend(RoGuildPosition p) {
            pendingPositions[p.Id] = p;
            SyncOk();
            windows[TabPositions]?.Invalidate();
        }

        /// <summary>The edits follow the visible rows (draw 0x8c0260 moves them to (59, y − 2) and (343, y − 2)).</summary>
        private void SyncPositionEdits() {
            bool master = Master;
            for (int r = 0; r < PositionRows; r++) {
                RoClassicEditField name = positionNames[r], tax = positionTaxes[r];
                if (name == null) continue;
                int index = positionFirst + r, y = 68 + PositionRowH * r;
                bool shown = master && index < guild.Positions.Length;
                name.Visible = tax.Visible = shown;
                if (!shown) continue;
                name.Rect = new RectInt(59, y - 2, name.Rect.width, 16);
                tax.Rect = new RectInt(343, y - 2, tax.Rect.width, 16);
                RoGuildPosition p = DisplayPosition(index);
                if (!name.HasFocus && name.Text != p.Name) name.SetText(p.Name);
                string rate = Num(p.PayRate);
                if (!tax.HasFocus && tax.Text != rate) tax.SetText(rate);
            }
        }

        /// <summary>Message 0x10f: the title edit writes the pending name.</summary>
        private void PositionNameChanged(int row, string text) {
            int index = positionFirst + row;
            if (index >= guild.Positions.Length) return;
            RoGuildPosition p = DisplayPosition(index);
            if (p.Name != text) Pend(new RoGuildPosition(p.Id, p.Rights, p.Ranking, p.PayRate, text));
        }

        /// <summary>Message 0x110: atol of the tax edit; outside 0..50 the red 0xd84 line and the clamped value.</summary>
        private void PositionTaxChanged(int row, RoClassicEditField field) {
            int index = positionFirst + row;
            if (index >= guild.Positions.Length) return;
            RoGuildPosition p = DisplayPosition(index);
            int rate = Atol(field.Text);
            if (rate < 0 || rate > 50) {
                Chat(Msg(0xd84, "You can't set the tax rate higher than %d%%.").Replace("%d", "50").Replace("%%", "%"), "#FF0000");
                rate = Mathf.Clamp(rate, 0, 50);
                field.SetText(Num(rate));
            }

            if (p.PayRate != rate) Pend(new RoGuildPosition(p.Id, p.Rights, p.Ranking, rate, p.Name));
        }

        private static int Atol(string text) {
            int i = 0, sign = 1, value = 0;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i < text.Length && (text[i] == '-' || text[i] == '+')) sign = text[i++] == '-' ? -1 : 1;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9') value = value * 10 + (text[i++] - '0');
            return sign * value;
        }

        /// <summary>OK 0xb4 at (W − 0x3d, H − 0x17) while the master has unsent changes, else parked at (−200, −200).</summary>
        private void SyncOk() {
            SyncDisband();
            bool master = Master;
            SetOk(windows[TabMembers], master && pendingMembers.Count > 0);
            SetOk(windows[TabPositions], master && pendingPositions.Count > 0);
        }

        /// <summary>The info tab's Disband for the master only.</summary>
        private void SyncDisband() {
            RoClassicWindow w = windows[TabInfo];
            RoClassicChildLayout button = w?.Layout.Children.Find(c => c.Id == DisbandId);
            if (button == null) return;
            bool shown = Master;
            button.X = shown ? w.Width - button.W - 0xf : -200;
            button.Y = shown ? w.Height - button.H - 5 : -200;
            w.Invalidate();
            SyncEmblemEdit();
        }

        private static void SetOk(RoClassicWindow w, bool shown) {
            RoClassicChildLayout ok = w?.Layout.Children.Find(c => c.Id == OkId);
            if (ok == null) return;
            ok.X = shown ? w.Width - 0x3d : -200;
            ok.Y = shown ? w.Height - 0x17 : -200;
            w.SetChildVisible(ok, shown);
            w.Invalidate();
        }

        /// <summary>OK → Msg 0x68 → CZ_REG_CHANGE_GUILD_POSITIONINFO with the pending positions.</summary>
        private void SendPositions() {
            if (pendingPositions.Count == 0 || session == null) return;
            var list = new List<RoGuildPosition>(pendingPositions.Values);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            pendingPositions.Clear();
            SyncOk();
            _ = session.BeginLegacyGuildPositions(list);
        }

        /// <summary>OK → Msg 0x67 → CZ_REQ_CHANGE_MEMBERPOS with the pending member positions.</summary>
        private void SendMemberPositions() {
            if (pendingMembers.Count == 0 || session == null) return;
            var list = new List<(uint AccountId, uint CharId, int PositionId)>(pendingMembers.Values);
            pendingMembers.Clear();
            SyncOk();
            _ = session.BeginLegacyGuildMemberPositions(list);
        }

        // ------------------------------------------------------------------ 0x1a: the member's position list

        /// <summary>
        /// Button 0x10d (vf37 0x8c3b40): selects the row and opens UIComboBoxWnd 0x1a with the position names ("%s"),
        /// 150 wide, 16 px rows up to 80 px, at the window's (0x58, 44 · row + 109) + 1.
        /// </summary>
        private void OpenCombo(int member, int row) {
            RoClassicWindow owner = windows[TabMembers];
            if (owner == null) return;
            if (combo == null) {
                combo = new RoClassicWindow(system, new RoClassicWindowLayout { Class = "UIComboBoxWnd", WindowId = 0x1a, Width = 150, Height = 80 }) {
                    Visible = false,
                    Draggable = false,
                    ClickableAt = _ => true,
                };
                combo.DrawDynamic += DrawCombo;
                combo.Pressed += (w, local) => {
                    int k = comboFirst + local.y / ComboRowH;
                    if (k < guild.Positions.Length) PickCombo(k);
                };
                combo.Wheeled += (w, local, delta) => {
                    comboFirst = Mathf.Clamp(comboFirst + (delta > 0 ? -1 : 1), 0, Math.Max(0, guild.Positions.Length - ComboRows));
                    combo.Invalidate();
                };
                combo.EscClose = () => {
                    CloseCombo();
                    return true;
                };
            }

            comboMember = member;
            comboRow = row;
            comboFirst = 0;
            comboHover = -1;
            int h = Math.Min(80, guild.Positions.Length * ComboRowH);
            combo.Layout.Height = h;
            combo.SetHeight(h);
            combo.MoveTo(owner.Position.x + 0x58 + 1, owner.Position.y + MemberRowH * row + 8 + 0x65 + 1);
            combo.Visible = true;
            system.Windows.BringToFront(combo);
            system.Windows.PushEsc(combo);
            combo.Invalidate();
        }

        private const int ComboRowH = 16, ComboRows = 5;

        private void DrawCombo(RoClassicWindow w) {
            int width = w.Width, height = w.Height;
            w.FillRect(0, 0, width, height, White);
            w.DrawBitmapRaw(0, 0, Basic + "sysboxs_lu.bmp");
            w.DrawBitmapRaw(0, height - 7, Basic + "sysboxs_ld.bmp");
            w.DrawBitmapRaw(width - 7, 0, Basic + "sysboxs_ru.bmp");
            w.DrawBitmapRaw(width - 7, height - 7, Basic + "sysboxs_rd.bmp");
            int count = guild.Positions.Length;
            Vector2Int mouse = system.Windows.Mouse;
            comboHover = system.Windows.HoverWindow == w ? comboFirst + (mouse.y - w.Position.y) / ComboRowH : -1;
            for (int r = 0; r < ComboRows && comboFirst + r < count; r++) {
                int k = comboFirst + r;
                bool on = k == comboHover;
                if (on) w.FillRect(2, ComboRowH * r + 1, width - 4, ComboRowH - 2, Selected);
                w.TextOut(4, ComboRowH * r + 2, guild.Positions[k].Name, 0, 12, on ? White : Black);
            }

            w.DrawScrollBar(width - 13, 0, height, Math.Max(0, count - ComboRows), count, ComboRows, comboFirst);
        }

        /// <summary>
        /// Callback 0x27: neither the master's row nor position 0 can be picked; another position goes into the pending
        /// map (account, char, position) and shows at once.
        /// </summary>
        private void PickCombo(int position) {
            CloseCombo();
            if (comboMember < 0 || comboMember >= memberRows.Length) return;
            RoGuildMember m = memberRows[comboMember];
            int current = MemberPosition(m);
            if (position == 0 || current == 0 || position == current) return;
            pendingMembers[m.CharId] = (m.AccountId, m.CharId, position);
            SyncOk();
            windows[TabMembers]?.Invalidate();
        }

        private void CloseCombo() {
            if (combo == null || !combo.Visible) return;
            combo.Visible = false;
            system.Windows.RemoveEsc(combo);
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (combo != null && combo.Visible) combo.Invalidate();
            UpdateCastleMenuHover();
            // 0x5eb7a0: a landed emblem repaints the info tab
            if (emblemSerial != RoClassicGuildEmblems.Serial) {
                emblemSerial = RoClassicGuildEmblems.Serial;
                windows[TabInfo]?.Invalidate();
            }
        }

        // ------------------------------------------------------------------ the member menu (Msg 0x31 / 0x30)

        /// <summary>Right-down selects the row; right-up builds menu 0x12 in this order, only the entries that apply.</summary>
        private void MemberRightClicked(Vector2Int local) {
            if (local.x < 5 || local.x >= 383 || local.y < 66 || local.y >= 283) return;
            int index = (local.y - 66) / MemberRowH + memberFirst;
            if (index >= memberRows.Length || session == null) return;
            memberSelected = index;
            windows[TabMembers]?.Invalidate();
            RoGuildMember target = memberRows[index];
            (uint myChar, uint myAccount) = session.LocalCharacterIds();
            bool master = guild.IsMaster(myChar);
            bool targetMaster = guild.Info != null && guild.Info.MasterCharId != 0 ? target.CharId == guild.Info.MasterCharId : target.PositionId == 0;
            var actions = new List<Action>();
            RoClassicMenu.Begin();
            if (target.CharId == myChar) {
                if (master) return;
                RoClassicMenu.Add(Msg(0x1fc, "Leave Guild"));
                actions.Add(() => {
                    if (target.AccountId == myAccount) OpenReasonForm(false, default);
                });
            } else {
                if (master || (guild.Rights & 0x10) != 0 && !targetMaster) {
                    RoClassicMenu.Add(Msg(0x1fd, "Expel"));
                    actions.Add(() => OpenReasonForm(true, (target.AccountId, target.CharId)));
                }

                if (master) {
                    RoClassicMenu.Add(Msg(0xb6b, "Assign Guild Leader"));
                    actions.Add(() => AssignLeader(target));
                }

                if (target.State == 1 && session.PartySnapshot().IsInParty && !InMyParty(target.Name)) {
                    RoClassicMenu.Add(Msg(0x532, "Request to Join Party"));
                    actions.Add(() => {
                        if (NameReady(target)) _ = session.BeginLegacyPartyInvite(target.Name);
                    });
                }

                if (target.State == 1) {
                    RoClassicMenu.Add(Msg(0x7fb, "1:1 Chat"));
                    actions.Add(() => {
                        if (!NameReady(target)) return;
                        // 0x8c4877: the window manager's msg 0xe, a 1:1 window for the member
                        RoClassicWhisper.Bind(session, chat);
                        RoClassicWhisper.OpenManual(target.Name, target.CharId);
                    });
                }

                RoClassicMenu.Add(Msg(0xaf8, "Send a mail"));
                actions.Add(() => {
                    if (NameReady(target)) MailComposer?.Invoke(target.Name);
                });
            }

            if (actions.Count == 0) return;
            Vector2Int mouse = system.Windows.Mouse;
            RoClassicMenu.Open(mouse.x + 1, mouse.y + 1, pick => {
                if (pick >= 0 && pick < actions.Count) actions[pick]();
            });
        }

        /// <summary>The RODEX compose opener for "Send a mail" (mode 0x10c with the name); set by the RODEX port.</summary>
        public static Action<string> MailComposer { get; set; }

        private bool InMyParty(string name) {
            foreach (var member in session.PartySnapshot().Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>()) {
                if (string.Equals(member.Name, name, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>An unresolved name blocks the action with 0xbe1 "Loading the player's name".</summary>
        private bool NameReady(RoGuildMember m) {
            if (!string.IsNullOrEmpty(m.Name)) return true;
            Chat(Msg(0xbe1, "Loading the player's name"), "#FF0000");
            return false;
        }

        /// <summary>0xb6c confirmation; anything but No queues the target at position 0 and sends 0x0155 at once.</summary>
        private void AssignLeader(RoGuildMember target) {
            string question = Format(Msg(0xb6c, "Do you want to assign %s as the guild leader?"), target.Name);
            RoClassicMessageBox.Show(question, RoClassicMessageBox.TypeOkCancel, true, result => {
                if (result == RoClassicMessageBox.IdNo || session == null) return;
                _ = session.BeginLegacyGuildMemberPositions(new[] { (target.AccountId, target.CharId, 0) });
            });
        }

        // ------------------------------------------------------------------ 0x4b / 0x4c: the reason forms

        /// <summary>
        /// UIGuildLeaveReasonDescWnd 0x4b / UIGuildBanReasonDescWnd 0x4c (280 × 80 at (W / 2 − 120, H / 4)): the 39-byte
        /// reason edit at (15, 22, 248, 16) #E8E8E8; OK sends CZ_REQ_LEAVE_GUILD / CZ_REQ_BAN_GUILD, Cancel only closes.
        /// </summary>
        private void OpenReasonForm(bool ban, (uint AccountId, uint CharId) target) {
            ref RoClassicWindow form = ref ban ? ref banForm : ref leaveForm;
            if (form == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + (ban ? "UIGuildBanReasonDescWnd" : "UIGuildLeaveReasonDescWnd")).text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIEditCtrl") child.Visible = false;
                }

                var w = new RoClassicWindow(system, layout) { Visible = false };
                var edit = new RoClassicEditField { Rect = new RectInt(15, 22, 248, 16), Background = new Color32(0xe8, 0xe8, 0xe8, 255), MaxBytes = 39 };
                edit.Submitted += _ => SubmitReason(ban);
                w.AddEditField(edit);
                w.Clicked += child => {
                    if (child.Id == OkId) SubmitReason(ban);
                    else if (child.Id == CancelId) w.Visible = false;
                };
                w.EscClose = () => {
                    w.Visible = false;
                    return true;
                };
                if (ban) banReason = edit;
                else leaveReason = edit;
                form = w;
            }

            if (ban) banTarget = target;
            RoClassicEditField field = ban ? banReason : leaveReason;
            field.SetText(string.Empty);
            Vector2Int client = system.ClientSize;
            form.MoveTo(client.x / 2 - 120, client.y / 4);
            form.Visible = true;
            system.Windows.BringToFront(form);
            field.Focus();
            form.Invalidate();
        }

        private void SubmitReason(bool ban) {
            RoClassicWindow form = ban ? banForm : leaveForm;
            if (form == null || !form.Visible || session == null) return;
            string reason = (ban ? banReason : leaveReason).Text;
            form.Visible = false;
            if (ban) _ = session.BeginLegacyGuildExpel(banTarget.AccountId, banTarget.CharId, reason);
            else _ = session.BeginLegacyGuildLeave(reason);
        }

        // ------------------------------------------------------------------ 0x3f

        /// <summary>
        /// OnDraw 0x8c09d0 (one column, 34 px rows): the selection (104, 43 + 34r, 164, 28) in colorchip (6,2), (10,2) for
        /// an unlearned skill, (14,2) without SP; the icon at (44, 45 + 34r); the name at (112, 46 + 34r) (#787878 while
        /// unlearned); "Lv : %d" (113, 58 + 34r) and "Sp : %d" / 0x11e passive (212, 58 + 34r); "SkillPoint : %d" at
        /// (W − 100, H − 20).
        /// </summary>
        private void DrawSkills(RoClassicWindow w) {
            RoSkillMetadataTable table = SkillTable();
            RoGuildSkill[] skills = guild.Skills;
            for (int r = 0; r < SkillRows && skillFirst + r < skills.Length; r++) {
                int k = skillFirst + r, y = SkillRowH * r;
                RoGuildSkill s = skills[k];
                if (k == skillSelected) w.FillRect(104, 43 + y, 164, 28, s.Level == 0 ? SelectedUnlearned : s.Sp == 0 ? SelectedNoSp : Selected);
                RoSkillMetadata meta = table != null ? table.GetSkillOrFallback(s.Id) : null;
                string icon = meta != null && !string.IsNullOrWhiteSpace(meta.IconName) ? meta.IconName : null;
                if (icon != null) w.DrawBitmap(44, 45 + y, SkillIcons + icon + ".bmp");
                string name = meta != null ? meta.GetDisplayName() : s.Name;
                w.TextOut(112, 46 + y, string.IsNullOrEmpty(name) ? s.Name : name, 0, 12, s.Level == 0 ? Unlearned : Black);
                if (s.Level == 0) continue;
                w.TextOut(113, 58 + y, "Lv : " + Num(s.Level), 0, 12, Black);
                w.TextOut(212, 58 + y, s.Inf == 0 ? Msg(0x11e, "Passive") : "Sp : " + Num(s.Sp), 0, 12, Black);
            }

            w.TextOut(w.Width - 100, w.Height - 20, "SkillPoint : " + Num(guild.SkillPoints), 0, 12, Black);
            w.DrawScrollBar(385, 41, 247, Math.Max(0, skills.Length - SkillRows), skills.Length, SkillRows, skillFirst);
        }

        /// <summary>FUN_008c2730: the row's up button at (78, 45 + 34r) for the master while points remain and the skill can rise.</summary>
        private void SyncSkillButtons() {
            RoClassicWindow w = windows[TabSkills];
            if (w == null) return;
            bool master = Master;
            for (int r = 0; r < SkillRows; r++) {
                RoClassicChildLayout up = w.Layout.Children.Find(c => c.Id == SkillUpId + r);
                if (up == null) continue;
                int k = skillFirst + r;
                bool shown = master && guild.SkillPoints != 0 && k < guild.Skills.Length && guild.Skills[k].Id != 0 && guild.Skills[k].Upgradable;
                up.X = shown ? 0x4e : -200;
                up.Y = shown ? SkillRowH * r + 0x2d : SkillRowH * r + 0x11;
            }

            w.Invalidate();
        }

        private void SkillPressed(Vector2Int local) {
            if (local.x >= 385 && local.x < 398 && local.y >= 41 && local.y < 288) {
                ScrollSkills(local.y < 54 ? -1 : local.y >= 275 ? 1 : local.y < 164 ? -SkillRows : SkillRows);
                return;
            }

            if (local.x < 5 || local.x >= 383 || local.y < 43 || local.y >= 43 + SkillRowH * SkillRows) return;
            int k = (local.y - 43) / SkillRowH + skillFirst;
            if (k >= guild.Skills.Length) return;
            skillSelected = k;
            windows[TabSkills]?.Invalidate();
        }

        private void ScrollSkills(int delta) {
            int next = Mathf.Clamp(skillFirst + delta, 0, Math.Max(0, guild.Skills.Length - SkillRows));
            if (next == skillFirst) return;
            skillFirst = next;
            SyncSkillButtons();
        }

        /// <summary>Msg 6 0xeb..0xf1: with guild points left, Msg 0x43 → CZ_UPGRADE_SKILLLEVEL for the row's skill.</summary>
        private void SkillUp(int row) {
            int k = skillFirst + row;
            if (session == null || guild.SkillPoints == 0 || k >= guild.Skills.Length) return;
            _ = session.BeginLegacyCompanionSkillUp(guild.Skills[k].Id);
        }

        /// <summary>Use 0xb4 → Msg 0x3a: a learned active skill; self skills are cast on the player, the rest go to the target overlay.</summary>
        private void UseSkill() {
            if (session == null || skillSelected < 0 || skillSelected >= guild.Skills.Length) return;
            RoGuildSkill s = guild.Skills[skillSelected];
            if (s.Level == 0 || s.Inf == 0) return;
            RoSkillTargetFlags flags = RoSkillMetadata.FromServerSkillType(s.Inf);
            RoSkillTargetFlags explicitTargets = RoSkillTargetFlags.Enemy | RoSkillTargetFlags.Place | RoSkillTargetFlags.Friend | RoSkillTargetFlags.Trap;
            if ((flags & RoSkillTargetFlags.Self) != 0 && (flags & explicitTargets) == 0) {
                _ = session.BeginLegacyUseSkillOnTarget(s.Id, s.Level, unchecked((int) session.LocalCharacterIds().AccountId));
                return;
            }

            RoSkillMetadataTable table = SkillTable();
            string name = table != null ? table.GetSkillOrFallback(s.Id)?.GetDisplayName() : s.Name;
            RoSkillTargetSelection.EnsureInScene().Begin(session, s.Id, s.Level, name, flags, false, Math.Max(1, s.Level));
        }

        private static RoSkillMetadataTable SkillTable() => Assets()?.GameData?.Skills;

        // ------------------------------------------------------------------ 0x40

        /// <summary>
        /// OnDraw 0x8beef0: the #C8C8C8 grid (4, 45, W − 22, H − 19 − 69) with white cells (columns 82 / 292, 18 px in 19 px
        /// rows), headers 0x197 / 0x1ce at (8, 49) / (91, 49), rows from y 68 (name at x 8, the reason at x 91).
        /// </summary>
        private void DrawBans(RoClassicWindow w) {
            w.FillRect(4, 45, w.Width - 22, w.Height - BanRowH - 0x45, Gray);
            for (int row = 0; row < 12; row++) {
                w.FillRect(5, 46 + BanRowH * row, 82, BanRowH - 1, White);
                w.FillRect(88, 46 + BanRowH * row, 292, BanRowH - 1, White);
            }

            w.TextOut(8, 49, Msg(0x197, "Name"), 0, 12, Black);
            w.TextOut(91, 49, Msg(0x1ce, "The Reason of Expulsion"), 0, 12, Black);
            RoGuildBan[] bans = guild.Bans;
            for (int n = 0; n < BanRows && banFirst + n < bans.Length; n++) {
                int k = banFirst + n, y = 68 + BanRowH * n;
                if (k == banSelected) w.FillRect(5, y - 3, w.Width - 24, 18, Selected);
                RoGuildBan b = bans[k];
                w.ClipTo(8, y, 78, 15);
                w.TextOut(8, y, string.IsNullOrEmpty(b.Name) ? Msg(0x245, "Nameless") : b.Name, 0, 12, Black);
                w.ClipTo(91, y, 288, 15);
                w.TextOut(91, y, b.Reason, 0, 12, Black);
                w.ResetClip();
            }

            w.DrawScrollBar(385, 63, 230, Math.Max(0, bans.Length - BanRows), bans.Length, BanRows, banFirst);
        }

        private void BanPressed(Vector2Int local) {
            if (local.x >= 385 && local.x < 398 && local.y >= 63 && local.y < 293) {
                ScrollBans(local.y < 76 ? -1 : local.y >= 280 ? 1 : local.y < 178 ? -BanRows : BanRows);
                return;
            }

            if (local.x < 5 || local.x >= 383 || local.y < 65 || local.y >= 65 + BanRowH * BanRows) return;
            int k = (local.y - 65) / BanRowH + banFirst;
            if (k >= guild.Bans.Length) return;
            banSelected = k;
            windows[TabBans]?.Invalidate();
        }

        private void ScrollBans(int delta) {
            int next = Mathf.Clamp(banFirst + delta, 0, Math.Max(0, guild.Bans.Length - BanRows));
            if (next == banFirst) return;
            banFirst = next;
            windows[TabBans]?.Invalidate();
        }

        // ------------------------------------------------------------------ 0x41

        private RoClassicEditField noticeTitle, noticeBody;

        /// <summary>vf15 0x8bc570: the edits start from the guild's notice; Send 0x111 at (W − 46, H − 24) for the master only.</summary>
        private void SeedNotice() {
            RoClassicWindow w = windows[TabNotice];
            if (w == null) return;
            noticeTitle.SetText(guild.NoticeSubject);
            noticeBody.SetText(guild.NoticeBody);
            noticeBody.Blur();
            RoClassicChildLayout send = w.Layout.Children.Find(c => c.Id == SendId);
            if (send != null) {
                bool master = Master;
                send.X = master ? w.Width - 46 : -200;
                send.Y = master ? w.Height - 24 : -200;
            }

            w.Invalidate();
        }

        /// <summary>OnDraw 0x8c0150: "%s:" of 0x203 at (5, 67), 0x204 at (5, 86), "[%d/%d]" (body bytes / 119) right on W − 7, y 266.</summary>
        private void DrawNotice(RoClassicWindow w) {
            w.TextOut(5, 67, Msg(0x203, "Title") + ":", 0, 12, Black);
            w.TextOut(5, 86, Msg(0x204, "Contents"), 0, 12, Black);
            w.TextOutRight(w.Width - 7, 266, "[" + Num(noticeBody.Text.Length) + "/" + Num(0x77) + "]", 0, 12, Black);
            if (noticeBody.LineCount > noticeBody.VisibleLines) {
                w.DrawScrollBar(5 + 390 - 13, 104, 156, noticeBody.LineCount - noticeBody.VisibleLines, noticeBody.LineCount, noticeBody.VisibleLines, noticeBody.FirstVisible);
            }
        }

        /// <summary>Send (vf37 0x8c53b0): master only → Msg 0x76 → CZ_GUILD_NOTICE (title 60, body 120 bytes).</summary>
        private void SendNotice() {
            if (session == null || !Master) return;
            _ = session.BeginLegacyGuildNotice(noticeTitle.Text, noticeBody.Text);
        }

        // ------------------------------------------------------------------ helpers

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static RoAssetDatabase Assets() {
            var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            return map != null ? map.Content?.Assets : null;
        }

        private string JobName(int job) {
            if (jobs == null) {
                RoAssetDatabase assets = Assets();
                if (assets != null && assets.IsInitialized) jobs = RoJobMetadataTable.Load(assets);
            }

            if (jobs != null && jobs.TryGetJob(job, out RoJobMetadata meta) && meta != null) {
                string formatted = RoJobMetadata.FormatJobName(meta.ConstantName);
                if (!string.IsNullOrEmpty(formatted)) return formatted;
            }

            return RoJobMetadataTable.DefaultName(job) ?? Num(job);
        }

        /// <summary>The sweep console: open [tab] | tab n | sort c | select row | wheel d | state | close.</summary>
        public static string Sweep(string command) {
            RoClassicGuild g = Ensure();
            if (g == null || g.session == null) return "no classic ui or session yet";
            Tick(g.session, g.chat);
            string[] parts = command.Trim().Split(' ');
            int arg = parts.Length > 1 && int.TryParse(parts[1], out int v) ? v : 0;
            switch (parts[0]) {
                case "castle":
                    return g.CastleSweep(parts);
                case "emblem":
                    return g.EmblemSweep(parts);
                case "open":
                    g.CloseAll();
                    if (!g.guild.HasGuild) return "no guild";
                    g.Open(Mathf.Clamp(arg, 0, TabNotice));
                    break;
                case "tab":
                    g.PickTab(arg);
                    break;
                case "sort":
                    g.MemberPressed(new Vector2Int(5 + 2 + SumColumns(MemberColumns, arg), 50));
                    break;
                case "select":
                    if (g.windows[TabSkills] != null && g.windows[TabSkills].Visible) g.SkillPressed(new Vector2Int(110, 45 + SkillRowH * arg));
                    else if (g.windows[TabBans] != null && g.windows[TabBans].Visible) g.BanPressed(new Vector2Int(10, 67 + BanRowH * arg));
                    else if (g.windows[TabPositions] != null && g.windows[TabPositions].Visible) g.PositionPressed(new Vector2Int(10, 66 + PositionRowH * arg));
                    else g.MemberPressed(new Vector2Int(10, 67 + MemberRowH * arg));
                    break;
                case "wheel":
                    if (g.windows[TabPositions] != null && g.windows[TabPositions].Visible) g.ScrollPositions(arg);
                    else g.ScrollMembers(arg);
                    break;
                case "close":
                    g.CloseAll();
                    break;
                case "check":
                    // check row k: toggles right k (0 invite, 1 expel, 2 storage) of the visible row
                    g.PositionPressed(new Vector2Int(CheckboxX(arg % 10) + 2, 68 + PositionRowH * (arg / 10) + 2));
                    break;
                case "ptitle":
                    if (parts.Length > 2 && g.positionNames[arg] != null) {
                        g.positionNames[arg].SetText(parts[2]);
                        g.PositionNameChanged(arg, parts[2]);
                    }

                    break;
                case "ptax":
                    if (parts.Length > 2 && g.positionTaxes[arg] != null) {
                        g.positionTaxes[arg].SetText(parts[2]);
                        g.PositionTaxChanged(arg, g.positionTaxes[arg]);
                    }

                    break;
                case "drop":
                    g.MemberPressed(new Vector2Int(152, 81 + MemberRowH * arg));
                    break;
                case "pick":
                    g.PickCombo(arg);
                    break;
                case "ok":
                    if (g.windows[TabPositions] != null && g.windows[TabPositions].Visible) g.SendPositions();
                    else g.SendMemberPositions();
                    break;
                case "menu":
                    g.MemberRightClicked(new Vector2Int(10, 67 + MemberRowH * arg));
                    break;
                case "leave":
                    g.OpenReasonForm(false, default);
                    g.leaveReason.SetText(parts.Length > 2 ? parts[2] : string.Empty);
                    if (parts.Length > 1 && parts[1] == "send") g.SubmitReason(false);
                    break;
                case "expel":
                    if (arg < g.memberRows.Length) {
                        g.OpenReasonForm(true, (g.memberRows[arg].AccountId, g.memberRows[arg].CharId));
                        g.banReason.SetText(parts.Length > 2 ? parts[2] : string.Empty);
                    }

                    break;
                case "invite":
                    _ = g.session.BeginLegacyGuildInvite(uint.Parse(parts[1], CultureInfo.InvariantCulture));
                    break;
                case "skillup":
                    g.SkillUp(arg);
                    break;
                case "use":
                    g.UseSkill();
                    break;
                case "helper":
                    g.ToggleHelper();
                    break;
                case "help":
                    g.ToggleTip();
                    break;
                case "create":
                    g.OpenNameForm(false);
                    if (parts.Length > 1) {
                        g.createName.SetText(parts[1]);
                        g.SubmitNameForm(false);
                    }

                    break;
                case "disband":
                    g.OpenNameForm(true);
                    if (parts.Length > 1) {
                        g.disbandName.SetText(parts[1]);
                        g.SubmitNameForm(true);
                    }

                    break;
                case "accept":
                    if (g.inviteWindow != null && g.inviteWindow.Visible) g.AnswerAccept(g.inviteWindow, parts.Length < 2 || parts[1] != "no");
                    else if (g.allyWindow != null && g.allyWindow.Visible) g.AnswerAccept(g.allyWindow, parts.Length < 2 || parts[1] != "no");
                    break;
                case "notice":
                    if (parts.Length > 1) {
                        string[] pieces = command.Trim().Substring(7).Split('|');
                        g.noticeTitle?.SetText(pieces[0]);
                        if (pieces.Length > 1) g.noticeBody?.SetText(pieces[1].Replace("\\n", "\r\n"));
                    }

                    break;
                case "noticesend":
                    g.SendNotice();
                    break;
                case "expelsend":
                    g.SubmitReason(true);
                    break;
            }

            RoClassicGuildState s = g.guild;
            int open = Array.FindIndex(g.windows, w => w != null && w.Visible);
            return "guild=" + s.GuildId + " name=" + s.Name + " mask=" + s.Mask.ToString("x", CultureInfo.InvariantCulture) + " info=" + (s.Info != null) +
                " members=" + s.Members.Length + " named=" + Array.FindAll(s.Members, m => m.Name.Length > 0).Length + " positions=" + s.Positions.Length +
                " relations=" + s.Relations.Length + " castles=" + s.Castles.Length + " master=" + g.MasterName() + " open=" + open +
                " first=" + g.memberFirst + " sel=" + g.memberSelected + " sort=" + g.sortColumn + " pfirst=" + g.positionFirst + " psel=" + g.positionSelected + " isMaster=" + g.Master + " pendP=" + g.pendingPositions.Count +
                " pendM=" + g.pendingMembers.Count + " combo=" + (g.combo != null && g.combo.Visible) + " event=" + s.EventSerial + "/" + s.EventKind + " skills=" + s.Skills.Length + " points=" + s.SkillPoints + " bans=" + s.Bans.Length +
                " ssel=" + g.skillSelected + " bsel=" + g.banSelected + " notice=" + s.NoticeSubject + "|" + s.NoticeBody.Replace("\r\n", "\\n") +
                (g.noticeBody != null ? " noticeLines=" + g.noticeBody.LineCount : string.Empty) +
                " portraits=" + g.portraits.Count + " portraitError=" + g.portraitError +
                (s.Members.Length > 0 ? " m0=" + s.Members[0].State + "/" + s.Members[0].Sex + "/" + s.Members[0].Head + "/" + s.Members[0].HeadPalette : string.Empty);
        }

        private static int SumColumns(int[] columns, int count) {
            int sum = 0;
            for (int i = 0; i < count && i < columns.Length; i++) sum += columns[i] + 1;
            return sum;
        }
    }
}
