using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Ragnarok.Assets;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The castle list of the info tab (guild-emblem-castle-spec B): UIListBoxAgit 0x22a, 95 × 158, white, just after
    /// "Castles Owned" at y 129 (over the manage-land text), 13-px rows of the castles of ZC_GUILD_AGIT_INFO 0x0b27 named
    /// by agitconfig (unnamed ones skipped), selected #CADFFB. A right press on a row opens UIListBoxAgitMenu 0x22b (120 × 35,
    /// "Go to destination" / "Commercial/Defense Check", hover #709FED); the move asks UIAgitMessageBox (310 × 130) before
    /// CZ_REQ_MOVE_GUILD_AGIT 0x0b28, the check sends CZ_REQ_AGIT_INVESTMENT 0x0b2c. The answers: 0x0b2d's message box
    /// (250 × 130) and 0x0b2e's red lines.
    /// </summary>
    public sealed partial class RoClassicGuild {
        private const int CastleW = 95, CastleH = 158, CastleRowH = 13, CastleRows = CastleH / CastleRowH, CastleY = 129;
        private const int AgitOkId = 0xb7, AgitCancelId = 0xb8;
        private static readonly Color32 CastleSelected = new Color32(0xca, 0xdf, 0xfb, 255);
        private static Dictionary<int, (string Name, int Cost, int SiegeCost)> agits;
        private static RoAssetDatabase agitAssets;

        private readonly List<string> castleNames = new List<string>();
        private int castleFirst, castleSelected = -1;
        private RoClassicWindow castleMenu;
        private int castleMenuHover = -1, castleMenuRow = -1;

        private sealed class AgitBox {
            public RoClassicWindow Window;
            public readonly List<(int Start, int End)> Lines = new List<(int, int)>();
            public string Text = string.Empty;
            public int Scroll;
            public Action OnOk;
        }

        private AgitBox agitConfirm, agitInfo;

        /// <summary>agitconfig.lub's tbl_AgitInfo: Number → Name, Warp.CostZeny, Warp.CostZenySeigeTime.</summary>
        private static (string Name, int Cost, int SiegeCost) Agit(int castle) {
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (agits == null || !ReferenceEquals(assets, agitAssets)) {
                agitAssets = assets;
                agits = new Dictionary<int, (string, int, int)>();
                string text = assets?.ReadText("data/luafiles514/lua files/agit/agitconfig.lub");
                if (!string.IsNullOrEmpty(text)) {
                    foreach (Match m in Regex.Matches(text, @"\[""[^""]+""\]\s*=\s*\{\s*Number\s*=\s*(\d+)\s*,\s*Name\s*=\s*""([^""]*)""(.*?)AdditionalEFST", RegexOptions.Singleline)) {
                        Match cost = Regex.Match(m.Groups[3].Value, @"CostZeny\s*=\s*(\d+)");
                        Match siege = Regex.Match(m.Groups[3].Value, @"CostZenySeigeTime\s*=\s*(\d+)");
                        agits[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)] = (m.Groups[2].Value,
                            cost.Success ? int.Parse(cost.Groups[1].Value, CultureInfo.InvariantCulture) : 0,
                            siege.Success ? int.Parse(siege.Groups[1].Value, CultureInfo.InvariantCulture) : 0);
                    }
                }
            }

            return agits.TryGetValue(castle, out var a) ? a : (string.Empty, 0, 0);
        }

        /// <summary>0x8b9690 (OnCreate, Msg 0x3c): the named castles in 0x0b27's order.</summary>
        private void RebuildCastleRows() {
            castleNames.Clear();
            foreach (byte castle in guild.Castles) {
                string name = Agit(castle).Name;
                if (!string.IsNullOrEmpty(name)) castleNames.Add(name);
            }

            if (castleSelected >= castleNames.Count) castleSelected = -1;
            castleFirst = Mathf.Clamp(castleFirst, 0, Math.Max(0, castleNames.Count - CastleRows));
        }

        /// <summary>Move(TextWidth(MSI 0x14d, 12 px) + 0x13, 0x81).</summary>
        private int CastleX(RoClassicWindow w) => w.FontFor(0, 12).Measure(Msg(0x14d, "Castles Owned")) + 0x13;

        /// <summary>UIListBox OnDraw 0x7ec990: white, the selected row (0, 13·i, 95, 12) #CADFFB, black text at (3, 13·i).</summary>
        private void DrawCastles(RoClassicWindow w) {
            int x0 = CastleX(w);
            w.FillRect(x0, CastleY, CastleW, CastleH, White);
            for (int r = 0; r < CastleRows; r++) {
                int i = castleFirst + r;
                if (i >= castleNames.Count) break;
                if (i == castleSelected) w.FillRect(x0, CastleY + CastleRowH * r, CastleW, 12, CastleSelected);
                w.TextOut(x0 + 3, CastleY + CastleRowH * r, castleNames[i], 0, 12, Black);
            }

            w.DrawScrollBar(x0 + CastleW - 13, CastleY, CastleH, Math.Max(0, castleNames.Count - CastleRows), castleNames.Count, CastleRows, castleFirst);
        }

        /// <summary>LBtnDown 0x8c1930 / RBtnDown 0x8c1d50 on the list; the parent's own presses (0x8c1590 / 0x8c1c20) hide the menu.</summary>
        private void CastlePressed(RoClassicWindow w, Vector2Int local, bool right) {
            HideCastleMenu();
            int x0 = CastleX(w);
            if (local.x < x0 || local.x >= x0 + CastleW || local.y < CastleY || local.y >= CastleY + CastleH) return;
            int my = local.y - CastleY, index = castleFirst + my / CastleRowH;
            if (index >= castleNames.Count) return;
            castleSelected = index;
            w.Invalidate();
            // Msg 0xa2(lb.x + 95 − 3, lb.y + (my − my % 13) + 2)
            if (right) ShowCastleMenu(w.Position.x + x0 + CastleW - 3, w.Position.y + CastleY + (my - my % CastleRowH) + 2, index);
        }

        private void CastleWheel(RoClassicWindow w, Vector2Int local, int delta) {
            int x0 = CastleX(w);
            if (local.x < x0 || local.x >= x0 + CastleW || local.y < CastleY || local.y >= CastleY + CastleH) return;
            int first = Mathf.Clamp(castleFirst + (delta > 0 ? -1 : 1), 0, Math.Max(0, castleNames.Count - CastleRows));
            if (first == castleFirst) return;
            castleFirst = first;
            w.Invalidate();
        }

        private void ShowCastleMenu(int x, int y, int row) {
            if (castleMenu == null) {
                var layout = new RoClassicWindowLayout { Class = "UIListBoxAgitMenu", WindowId = 0x22b, Width = 120, Height = 35 };
                castleMenu = new RoClassicWindow(system, layout) { Visible = false, Draggable = false, FrameDragArea = _ => false };
                castleMenu.DrawDynamic += DrawCastleMenu;
                castleMenu.Pressed += (mw, p) => PickCastleMenu(Mathf.Clamp(p.y / 16, 0, 1));
            }

            castleMenuRow = row;
            castleMenuHover = -1;
            castleMenu.MoveTo(x, y);
            castleMenu.Visible = true;
            system.Windows.BringToFront(castleMenu);
            castleMenu.Invalidate();
        }

        private void HideCastleMenu() {
            if (castleMenu == null || !castleMenu.Visible) return;
            castleMenu.Visible = false;
            castleMenuRow = -1;
        }

        /// <summary>OnDraw 0x8c1280: white, the sysboxs corners, the hovered row #709FED, both texts black at (3, 16·i + 1).</summary>
        private void DrawCastleMenu(RoClassicWindow w) {
            w.FillRect(0, 0, w.Width, w.Height, White);
            w.DrawBitmapRaw(0, 0, Basic + "sysboxs_lu.bmp");
            w.DrawBitmapRaw(0, w.Height - 7, Basic + "sysboxs_ld.bmp");
            w.DrawBitmapRaw(w.Width - 7, 0, Basic + "sysboxs_ru.bmp");
            w.DrawBitmapRaw(w.Width - 7, w.Height - 7, Basic + "sysboxs_rd.bmp");
            if (castleMenuHover >= 0) w.FillRect(3, 16 * castleMenuHover + 1, w.Width - 6, 14, Selected);
            w.TextOut(3, 1, Msg(0xe2d, "Go to destination"), 0, 12, Black);
            w.TextOut(3, 17, Msg(0xe2e, "Commercial/Defense Check"), 0, 12, Black);
        }

        /// <summary>vf28 0x8c1a00: the hovered row, −1 outside.</summary>
        private void UpdateCastleMenuHover() {
            if (castleMenu == null || !castleMenu.Visible) return;
            Vector2Int p = system.Windows.Mouse - castleMenu.Position;
            int hover = p.x >= 0 && p.x < castleMenu.Width && p.y >= 0 && p.y < castleMenu.Height ? Mathf.Clamp(p.y / 16, 0, 1) : -1;
            if (hover == castleMenuHover) return;
            castleMenuHover = hover;
            castleMenu.Invalidate();
        }

        /// <summary>Msg 0x30 (vf37 0x8c6740): the castle is the vector entry at the list row (rows shift past unnamed castles, as in the client).</summary>
        private void PickCastleMenu(int item) {
            int row = castleMenuRow;
            HideCastleMenu();
            if (row < 0 || row >= guild.Castles.Length) return;
            int castle = guild.Castles[row];
            if (item == 1) {
                if (castle != 0) _ = session?.BeginClassicPacket(0x0b2c, new[] { (byte) castle }, "a castle check");
                return;
            }

            if (RoPvpState.IsSiege) {
                Chat(Msg(0xe31, "This feature is not available in WoE territory."), "#FF0000");
                return;
            }

            (string name, int cost, int siegeCost) = Agit(castle);
            if (name.Length == 0) return;
            string text = Msg(0xe2f, "Would you like to move to \"%s\" Management Territory?\\n%d Zeny will be consumed with every move.\\n(%d Zeny will be consumed during WoE times.)");
            text = Printf(text, name, cost.ToString(CultureInfo.InvariantCulture), siegeCost.ToString(CultureInfo.InvariantCulture));
            ShowAgitBox(ref agitConfirm, 310, 130, text, true, () => {
                if (castle != 0) _ = session?.BeginClassicPacket(0x0b28, new[] { (byte) castle }, "a castle move");
            });
        }

        /// <summary>The raw 0x0b2d / 0x0b2e answers.</summary>
        internal static void HandleAgit(byte[] b) {
            RoClassicGuild g = Ensure();
            if (g == null || b == null || b.Length < 4) return;
            ushort id = BitConverter.ToUInt16(b, 0);
            if (id == 0x0b2e) {
                // 0xc98e50: 1 not enough zeny, 2 WoE territory; red
                int result = BitConverter.ToInt16(b, 2);
                if (result == 1) g.Chat(Msg(0x442, "You do not have enough zeny."), "#FF0000");
                else if (result == 2) g.Chat(Msg(0xe31, "This feature is not available in WoE territory."), "#FF0000");
                return;
            }

            if (id != 0x0b2d || b.Length < 11) return;
            // 0xc98d20: {u8 castle, i32 economy, i32 defense} → MSI 0xe30 (name, defense, 100, economy, 100) in a 250 × 130 box
            int castleId = b[2], economy = BitConverter.ToInt32(b, 3), defense = BitConverter.ToInt32(b, 7);
            string text = Printf(Msg(0xe30, "\\nManagement Territory: \"%s\"\\n\t\\nDefense: %d / %d\\nCommercial: %d / %d"), Agit(castleId).Name,
                defense.ToString(CultureInfo.InvariantCulture), "100", economy.ToString(CultureInfo.InvariantCulture), "100");
            g.ShowAgitBox(ref g.agitInfo, 250, 130, text, false, null);
        }

        /// <summary>sprintf over %s / %d in order; the table's literal "\n" breaks the line, a tab is a blank.</summary>
        private static string Printf(string pattern, params string[] args) {
            var sb = new System.Text.StringBuilder();
            int a = 0;
            for (int i = 0; i < pattern.Length; i++) {
                if (pattern[i] == '%' && i + 1 < pattern.Length && (pattern[i + 1] == 's' || pattern[i + 1] == 'd')) {
                    sb.Append(a < args.Length ? args[a++] : string.Empty);
                    i++;
                } else {
                    sb.Append(pattern[i]);
                }
            }

            return sb.ToString().Replace("\\n", "\n").Replace("\t", " ");
        }

        /// <summary>
        /// UIAgitMessageBox (ctor 0x804a20, layout 0x8b79e0) and the plain "Message" box: the UIMessageBox frame at W × H,
        /// centred; the viewer (10, 22, W − 20, H − 55) wraps at W / 6 bytes on 18-px lines; OK (0xb7) / Cancel (0xb8) 42 × 20
        /// at x = W − 47·k, y = H − 24.
        /// </summary>
        private void ShowAgitBox(ref AgitBox box, int width, int height, string text, bool cancel, Action onOk) {
            if (box == null) {
                var created = new AgitBox();
                var layout = new RoClassicWindowLayout { Class = "UIAgitMessageBox", Width = width, Height = height };
                layout.Children.Add(AgitButton(AgitOkId, "btn_ok", width - 47 * (cancel ? 2 : 1), height - 24));
                if (cancel) layout.Children.Add(AgitButton(AgitCancelId, "btn_cancel", width - 47, height - 24));
                created.Window = new RoClassicWindow(system, layout) { Visible = false };
                created.Window.DrawDynamic += w => DrawAgitBox(w, created);
                created.Window.Clicked += child => {
                    if (child.Id != AgitOkId && child.Id != AgitCancelId) return;
                    created.Window.Visible = false;
                    if (child.Id == AgitOkId) created.OnOk?.Invoke();
                };
                created.Window.Wheeled += (w, local, delta) => {
                    int visible = (height - 55) / 18;
                    int scroll = Mathf.Clamp(created.Scroll + (delta > 0 ? -1 : 1), 0, Math.Max(0, created.Lines.Count - visible));
                    if (scroll == created.Scroll) return;
                    created.Scroll = scroll;
                    w.Invalidate();
                };
                created.Window.EscClose = () => {
                    created.Window.Visible = false;
                    return true;
                };
                box = created;
            }

            box.Text = text;
            box.OnOk = onOk;
            box.Scroll = 0;
            RoClassicWindow.WrapBytes(text, width / 6, box.Lines);
            Vector2Int client = system.ClientSize;
            box.Window.MoveTo(client.x / 2 - width / 2, client.y / 2 - height / 2);
            box.Window.Visible = true;
            system.Windows.BringToFront(box.Window);
            box.Window.Invalidate();
        }

        private static RoClassicChildLayout AgitButton(int id, string name, int x, int y) {
            var b = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = name, X = x, Y = y, W = 42, H = 20 };
            b.States["normal"] = Ui + name + ".bmp";
            b.States["hover"] = Ui + name + "_a.bmp";
            b.States["pressed"] = Ui + name + "_b.bmp";
            return b;
        }

        /// <summary>The UIMessageBox frame (its 280 × 120 capture's ops at this size) and the viewer's lines.</summary>
        private void DrawAgitBox(RoClassicWindow w, AgitBox box) {
            int width = w.Width, height = w.Height;
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < width - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(width - 12, 0, Basic + "titlebar_right.bmp");
            w.TextOut(19, 4, "Message", 0, 12, White);
            w.TextOut(18, 3, "Message", 0, 12, Black);
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            // the round box: #C8C8C8 edges around a white body
            w.FillRect(0, 17, width, 5, Gray);
            w.FillRect(3, 17, width - 6, 1, Gray);
            w.FillRect(3, height - 1, width - 6, 1, Gray);
            w.FillRect(0, 20, 1, height - 23, Gray);
            w.FillRect(width - 1, 20, 1, height - 23, Gray);
            w.FillRect(1, 18, 2, 1, Gray);
            w.FillRect(1, height - 2, 2, 1, Gray);
            w.FillRect(width - 3, 18, 2, 1, Gray);
            w.FillRect(width - 3, height - 2, 2, 1, Gray);
            w.FillRect(1, 17, width - 2, height - 19, White);
            w.FillRect(2, height - 2, width - 4, 1, White);
            int visible = (height - 55) / 18;
            for (int i = 0; i < visible && box.Scroll + i < box.Lines.Count; i++) {
                (int start, int end) = box.Lines[box.Scroll + i];
                while (end > start && (box.Text[end - 1] == '\n' || box.Text[end - 1] == '\r')) end--;
                w.TextOut(10 + 3, 22 + 4 + 18 * i, box.Text.Substring(start, end - start), 0, 12, Black);
            }

            w.DrawScrollBar(10 + width - 20 - 13, 22, height - 55, Math.Max(0, box.Lines.Count - visible), box.Lines.Count, visible, box.Scroll);
        }

        /// <summary>Sweep: castle sel i | castle menu i | castle pick n | castle ok | castle cancel | castle state.</summary>
        private string CastleSweep(string[] parts) {
            int arg = parts.Length > 2 && int.TryParse(parts[2], out int v) ? v : 0;
            RoClassicWindow w = windows[TabInfo];
            switch (parts.Length > 1 ? parts[1] : "state") {
                case "sel":
                case "menu":
                    if (w == null || !w.Visible) return "info tab not open";
                    CastlePressed(w, new Vector2Int(CastleX(w) + 5, CastleY + CastleRowH * (arg - castleFirst) + 3), parts[1] == "menu");
                    break;
                case "pick":
                    PickCastleMenu(arg);
                    break;
                case "ok":
                case "cancel":
                    AgitBox box = agitConfirm != null && agitConfirm.Window.Visible ? agitConfirm : agitInfo;
                    if (box != null && box.Window.Visible) {
                        box.Window.Visible = false;
                        if (parts[1] == "ok") box.OnOk?.Invoke();
                    }

                    break;
            }

            return "castles=[" + string.Join(",", guild.Castles) + "] rows=[" + string.Join(",", castleNames) + "] sel=" + castleSelected +
                " menu=" + (castleMenu != null && castleMenu.Visible) + "@" + castleMenuRow + " confirm=" + (agitConfirm != null && agitConfirm.Window.Visible) +
                " info=" + (agitInfo != null && agitInfo.Window.Visible) + " siege=" + RoPvpState.IsSiege;
        }
    }
}
