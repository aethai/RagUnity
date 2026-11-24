using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Entities;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The large minimap UIMiniMapWnd 0x111 (codex-ui-research RUNTIME-RULES-BIG-MINIMAP-WORLD-MAP.md "Large minimap" and
    /// "Part 1"): 516 × 559 at the saved place or (screen width − 536, 190); the style-1 title "%s ( %s ) %d %d" (display
    /// name, map, cell), the body, the style-1 button bar, the close X at (W − 14, 3). vf20 0x8fe000 fills (2, 19, 512, 512)
    /// #AAAAAA, key-blits map\&lt;map&gt;.bmp and darkens it by 50 per channel, then the TownInfo TYPE 8 warp points
    /// (always), the facility icons (Facility) and the party points (Guild/Party); vf15 0x8fdca0 fits the GAT into 512 px
    /// (scale = 512 / max(W, H), x0 = 2, y0 = 531, the shorter axis centred). Quest 0x165, Facility 0xbe and Guild/Party
    /// 0x171 check boxes at (20 / 120 / 220, H − 19), the Record button 0x160 at (W − 120, H − 23); the player 0xc4 is a
    /// UIHeadsprButton (the own head sprite, ACT action 0 frame 0) at (x0 + x·scale − 13, y0 − (y·scale + 13)). Record arms
    /// the next body click: the memo editor 0x112 opens at the cursor and Store appends "x\ty\ttext\r\n" to
    /// Memo\$$&lt;map&gt;.txt; up to 20 memo points (memopoint_*, the text as their tooltip), a click asks 0xabf and rewrites
    /// the file without it. Maps of data\ExceptionMinimapNameTable.txt refuse to open (0xacf).
    /// Not ported yet: guild positions (0x01eb), quest NPC icons (0x0446), the boss button's source (0x0293 + status 253)
    /// and the navigation from a facility click (0xac0830).
    /// </summary>
    public sealed partial class RoClassicMinimap {
        private const int BigW = 516, BigH = 559, BigCloseId = 0xc5, BigPlayerId = 0xc4, BigRecordId = 0x160, BigBossId = 0xc9;
        private const int BigQuestId = 0x165, BigFacilityId = 0xbe, BigSocialId = 0x171;
        private const int MemoFirstId = 0x249, MemoCount = 20, MemoW = 250, MemoH = 42, MemoStoreId = 0xb4, MemoCloseId = 0xc5;
        private const string BigPrefs = "ro.ui.classic.bigmap.";

        private RoClassicWindow classicBig;
        private string bigPictureStem;
        private bool bigQuest = true, bigFacility = true, bigSocial = true;
        private readonly Color32[] bigPicture = new Color32[512 * 512];
        private readonly List<Vector2Int> bigParty = new List<Vector2Int>();

        // memo: the armed Record button, the clicked map-local point, the loaded points (20 at most) and their captions
        private bool bigMemoArmed;
        private Vector2Int bigMemoPoint;
        private RoClassicWindow memoEditor;
        private RoClassicEditField memoEdit;
        private string bigMemoStem;
        private readonly Vector2Int[] bigMemoPoints = new Vector2Int[MemoCount];
        private readonly string[] bigMemoTexts = new string[MemoCount];
        private int bigMemoCount;

        // the player button's head image
        private string bigHeadKey;
        private static HashSet<string> exceptionMaps;

        /// <summary>The HUD's Maximize button 0xb9 (and action 0x96): toggles 0x111 unless 0xd01880 refuses the map.</summary>
        private void ToggleClassicBigMinimap() {
            if (classicBig != null && classicBig.Visible) {
                CloseClassicBigMinimap();
                return;
            }

            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (IsMinimapExcluded(miniPictureStem)) {
                RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0xacf, "In this map You can't memorize and zoom the minimap."), RoClassicMessageBox.TypeOk, true, null);
                return;
            }

            if (classicBig == null) {
                var layout = new RoClassicWindowLayout { Class = "UIMiniMapWnd", WindowId = 0x111, Width = BigW, Height = BigH };
                RoClassicFrames.TitleBar(layout.Ops, BigW, true);
                RoClassicFrames.Body(layout.Ops, BigW, BigH);
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = BigCloseId, Path = "close", X = BigW - 14, Y = 3, W = 11, H = 11 };
                close.States["normal"] = MiniUi + "basic_interface\\sys_close_off.bmp";
                close.States["hover"] = MiniUi + "basic_interface\\sys_close_on.bmp";
                close.States["pressed"] = MiniUi + "basic_interface\\sys_close_on.bmp";
                layout.Children.Add(close);
                layout.Children.Add(BigButton(BigRecordId, "record", "memo_ready", BigW - 120, BigH - 23, 100, 20));
                layout.Children.Add(BigButton(BigBossId, "boss", "boss", -100, -100, 24, 24));
                // the player: a UIHeadsprButton, its art set once the head image exists
                layout.Children.Add(new RoClassicChildLayout { Class = "UIBitmapButton", Id = BigPlayerId, Path = "player", X = -100, Y = -100, W = 2, H = 2 });
                for (int i = 0; i < MemoCount; i++) {
                    layout.Children.Add(BigButton(MemoFirstId + i, "memo" + i.ToString(CultureInfo.InvariantCulture), "memopoint", -100, -100, 15, 13));
                }

                classicBig = new RoClassicWindow(system, layout) { Visible = false };
                classicBig.DrawDynamic += DrawClassicBigMinimap;
                classicBig.Clicked += OnClassicBigClicked;
                classicBig.Pressed += (w, local) => BigPressed(local);
                classicBig.Moved += w => {
                    PlayerPrefs.SetInt(BigPrefs + "x", w.Position.x);
                    PlayerPrefs.SetInt(BigPrefs + "y", w.Position.y);
                };
                // MINIMAPWNDINFO SHOWQUEST / WHOWNPC / WHOWMEMBER
                bigQuest = PlayerPrefs.GetInt(BigPrefs + "quest", 1) != 0;
                bigFacility = PlayerPrefs.GetInt(BigPrefs + "npc", 1) != 0;
                bigSocial = PlayerPrefs.GetInt(BigPrefs + "member", 1) != 0;
            }

            // 0x907420: a saved place wholly on the screen, else 0x908340's (W − 536, 190)
            Vector2Int client = system.ClientSize;
            int x = PlayerPrefs.GetInt(BigPrefs + "x", -1), y = PlayerPrefs.GetInt(BigPrefs + "y", -1);
            if (x < 0 || y < 0 || x + BigW > client.x || y + BigH > client.y) {
                x = client.x - 536;
                y = 190;
            }

            classicBig.MoveTo(x, y);
            classicBig.Visible = true;
            system.Windows.BringToFront(classicBig);
            bigMemoStem = null;
            UpdateClassicBigMinimap();
        }

        private void CloseClassicBigMinimap() {
            if (classicBig != null) classicBig.Visible = false;
            bigMemoArmed = false;
            CloseMemoEditor();
        }

        private static RoClassicChildLayout BigButton(int id, string path, string art, int x, int y, int w, int h) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            button.States["normal"] = MiniUi + "minimap\\" + art + "_1.bmp";
            button.States["hover"] = MiniUi + "minimap\\" + art + "_2.bmp";
            button.States["pressed"] = MiniUi + "minimap\\" + art + "_3.bmp";
            return button;
        }

        /// <summary>
        /// 0xd01880: the map against data\ExceptionMinimapNameTable.txt (overlay; '#'-terminated names, CR / LF ignored),
        /// loaded once.
        /// </summary>
        private static bool IsMinimapExcluded(string map) {
            if (string.IsNullOrEmpty(map)) return false;
            if (exceptionMaps == null) {
                Func<string, byte[]> source = RoClassicUiImages.DefaultSourceProvider?.Invoke();
                if (source == null) return false;
                exceptionMaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                byte[] data = source("data\\exceptionminimapnametable.txt");
                if (data != null) {
                    foreach (string token in Encoding.ASCII.GetString(data).Split('#')) {
                        string name = token.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
                        if (name.Length > 0 && !name.StartsWith("//", StringComparison.Ordinal)) exceptionMaps.Add(name);
                    }
                }
            }

            return exceptionMaps.Contains(map);
        }

        /// <summary>vf15's fit: the scale and the origin (x0 left, y0 bottom) of the 512 px square.</summary>
        private void BigFit(out float scale, out float x0, out float y0) {
            int w = Math.Max(1, miniMapW), h = Math.Max(1, miniMapH);
            scale = 512f / Math.Max(w, h);
            x0 = 2f;
            y0 = 531f;
            if (w < h) x0 = (512f - w * scale) * 0.5f + 2f;
            else if (h < w) y0 = 531f - (512f - h * scale) * 0.5f;
        }

        /// <summary>The map picture over #AAAAAA, darkened by 50 a channel, rebuilt per map.</summary>
        private bool EnsureBigPicture(RoClassicUiSystem system) {
            if (miniPicture == null || string.IsNullOrEmpty(miniPictureStem)) return false;
            if (miniPictureStem == bigPictureStem) return true;
            bigPictureStem = miniPictureStem;
            for (int y = 0; y < 512; y++) {
                for (int x = 0; x < 512; x++) {
                    Color32 c = new Color32(0xaa, 0xaa, 0xaa, 255);
                    if (x < miniPictureW && y < miniPictureH) {
                        Color32 p = miniPicture[y * miniPictureW + x];
                        if (p.a != 0) c = p;
                    }

                    bigPicture[y * 512 + x] = new Color32((byte) Math.Max(0, c.r - 50), (byte) Math.Max(0, c.g - 50), (byte) Math.Max(0, c.b - 50), 255);
                }
            }

            return system.Images.SetDynamic("classic-bigmap", 512, 512, bigPicture);
        }

        private void DrawClassicBigMinimap(RoClassicWindow w) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            string stem = miniPictureStem ?? string.Empty;
            string title = RoClassicUiHost.MapDisplayName(stem) + " ( " + stem + " ) " +
                           miniCell.x.ToString(CultureInfo.InvariantCulture) + " " + miniCell.y.ToString(CultureInfo.InvariantCulture);
            // FUN_0082f2d0(1, title): white at (19, 4), black at (18, 3)
            w.TextOut(19, 4, title, 0, 12, new Color32(255, 255, 255, 255));
            w.TextOut(18, 3, title, 0, 12, new Color32(0, 0, 0, 255));
            DrawBigButtonBar(w, system);
            w.FillRect(2, 19, 512, 512, new Color32(0xaa, 0xaa, 0xaa, 255));
            if (EnsureBigPicture(system)) w.DrawBitmap(2, 19, "classic-bigmap");
            BigFit(out float scale, out float x0, out float y0);
            // 0x8fcfe0: TYPE 8 records are the warp points (drawn whatever the boxes say); the others facility icons
            foreach (RoMinimapTownInfoMarker marker in townInfoMarkers) {
                int px = (int) (x0 + marker.X * scale - 6), py = (int) (y0 - (marker.Y * scale + 6));
                if (marker.Type == 8) w.DrawBitmap(px, py, MiniUi + "minimap\\warp.bmp");
                else if (bigFacility && marker.Type >= 0 && marker.Type < MiniTownIcons.Length) w.DrawBitmap(px, py, MiniUi + "Information\\" + MiniTownIcons[marker.Type] + ".bmp");
            }

            if (bigSocial) {
                // Msg 0x1df kind 1 (guildmates outside the party) before kind 0 (the party), points ≤ 0 skipped
                foreach ((uint id, int gx, int gy) in miniGuild) {
                    if (gx <= 0 || gy <= 0 || miniPartyIds.Contains(id)) continue;
                    w.DrawBitmap((int) (gx * scale - 6 + x0), (int) (y0 - (gy * scale + 6)), MiniUi + "minimap\\guild.bmp");
                }

                foreach (Vector2Int p in bigParty) {
                    if (p.x <= 0 || p.y <= 0) continue;
                    w.DrawBitmap((int) (p.x * scale - 6 + x0), (int) (y0 - (p.y * scale + 6)), MiniUi + "minimap\\Party.bmp");
                }
            }

            // Msg 0x1e1: the quest NPCs (minimap\Quest_<state>_<colour>.bmp) while Quest is checked
            if (bigQuest) {
                foreach (RoMinimapMarkerState.QuestMark mark in miniQuests) {
                    if (mark.Color == 0 || mark.X < 0 || mark.Y < 0) continue;
                    w.DrawBitmap((int) (mark.X * scale - 6 + x0), (int) (y0 - (mark.Y * scale + 6)), BigQuestArt(mark));
                }
            }

            DrawBigCheck(w, 20, BigH - 19, bigQuest, RoClassicUiHost.MsgStr(0xab6, "Quest"));
            DrawBigCheck(w, 120, BigH - 19, bigFacility, RoClassicUiHost.MsgStr(0xab8, "Facility"));
            DrawBigCheck(w, 220, BigH - 19, bigSocial, RoClassicUiHost.MsgStr(0xaba, "Guild/Party"));
        }

        /// <summary>FUN_0082d150(1, 0, 1): btnbar_left1 / mid1 / right1 along the bottom.</summary>
        private static void DrawBigButtonBar(RoClassicWindow w, RoClassicUiSystem system) {
            string basic = MiniUi + "basic_interface\\";
            if (!system.Images.TryGet(basic + "btnbar_left1.bmp", out RoClassicUiImage left)) return;
            int y = BigH - left.Height;
            w.DrawBitmap(0, y, basic + "btnbar_left1.bmp");
            int right = system.Images.TryGet(basic + "btnbar_right1.bmp", out RoClassicUiImage r) ? r.Width : left.Width;
            if (system.Images.TryGet(basic + "btnbar_mid1.bmp", out RoClassicUiImage mid) && mid.Width > 0) {
                for (int x = left.Width; x < BigW - right; x += mid.Width) w.DrawBitmap(x, y, basic + "btnbar_mid1.bmp");
            }

            w.DrawBitmap(BigW - right, y, basic + "btnbar_right1.bmp");
        }

        /// <summary>UICheckBox: checkbox_0 / _1 at (0, 1), the label black at (13, 0).</summary>
        private static void DrawBigCheck(RoClassicWindow w, int x, int y, bool on, string label) {
            w.DrawBitmap(x, y + 1, MiniUi + (on ? "checkbox_1.bmp" : "checkbox_0.bmp"));
            w.TextOut(x + 13, y, label, 0, 12, new Color32(0, 0, 0, 255));
        }

        private void OnClassicBigClicked(RoClassicChildLayout child) {
            if (child.Id == BigCloseId) {
                CloseClassicBigMinimap();
                return;
            }

            if (child.Id == BigRecordId) {
                // +0xf8: the next body click places the memo
                bigMemoArmed = true;
                return;
            }

            int memo = child.Id - MemoFirstId;
            if (memo < 0 || memo >= bigMemoCount) return;
            RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0xabf, "Do you want to delete it?"), RoClassicMessageBox.TypeOkCancel, false, r => {
                if (r == RoClassicMessageBox.IdYes) DeleteMemo(memo);
            });
        }

        private void BigPressed(Vector2Int local) {
            if (bigMemoArmed) {
                // vf25 0x8fe2c0: 1 < x ≤ W − 2, 18 < y ≤ H − 19; the point is (max(0, x − 6), y − 6), the editor at the cursor
                if (local.x > 1 && local.x <= BigW - 2 && local.y > 18 && local.y <= BigH - 19) {
                    bigMemoArmed = false;
                    bigMemoPoint = new Vector2Int(Math.Max(0, local.x - 6), local.y - 6);
                    OpenMemoEditor();
                }

                return;
            }

            // 0x8feaf9: a facility icon (TownInfo record) or a quest mark guides to its cell without the window
            BigFit(out float scale, out float x0, out float y0);
            if (bigFacility) {
                foreach (RoMinimapTownInfoMarker marker in townInfoMarkers) {
                    if (marker.Type == 8) continue;
                    int px = (int) (x0 + marker.X * scale - 6), py = (int) (y0 - (marker.Y * scale + 6));
                    if (local.x < px || local.y < py || local.x >= px + 12 || local.y >= py + 12) continue;
                    RoClassicNavigation.NavigateHere(marker.X, marker.Y);
                    return;
                }
            }

            if (bigQuest) {
                foreach (RoMinimapMarkerState.QuestMark mark in miniQuests) {
                    if (mark.Color == 0 || mark.X < 0 || mark.Y < 0) continue;
                    int qx = (int) (x0 + mark.X * scale - 6), qy = (int) (y0 - (mark.Y * scale + 6));
                    if (local.x < qx || local.y < qy || local.x >= qx + 14 || local.y >= qy + 14) continue;
                    RoClassicNavigation.NavigateHere(mark.X, mark.Y);
                    return;
                }
            }

            int y = BigH - 19;
            if (new RectInt(20, y, 100, 12).Contains(local)) {
                bigQuest = !bigQuest;
                PlayerPrefs.SetInt(BigPrefs + "quest", bigQuest ? 1 : 0);
            } else if (new RectInt(120, y, 100, 12).Contains(local)) {
                bigFacility = !bigFacility;
                PlayerPrefs.SetInt(BigPrefs + "npc", bigFacility ? 1 : 0);
            } else if (new RectInt(220, y, 100, 12).Contains(local)) {
                bigSocial = !bigSocial;
                PlayerPrefs.SetInt(BigPrefs + "member", bigSocial ? 1 : 0);
            } else {
                return;
            }

            classicBig.Invalidate();
        }

        /// <summary>Per refresh: the party members on this map, the memo points of a new map, the player button; a redraw.</summary>
        private void UpdateClassicBigMinimap() {
            if (classicBig == null || !classicBig.Visible) return;
            bigParty.Clear();
            RoPartyMemberRuntimeSnapshot[] members = session?.PartySnapshot().Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>();
            uint self = session?.LocalCharacterIds().CharId ?? 0;
            string stem = miniPictureStem ?? string.Empty;
            foreach (RoPartyMemberRuntimeSnapshot m in members) {
                if (m.CharacterId == self || m.IsDead) continue;
                string map = m.MapName ?? string.Empty;
                int dot = map.LastIndexOf('.');
                if (dot > 0) map = map.Substring(0, dot);
                if (!string.Equals(map, stem, StringComparison.OrdinalIgnoreCase)) continue;
                bigParty.Add(new Vector2Int(m.X, m.Y));
            }

            if (!string.Equals(bigMemoStem, stem, StringComparison.OrdinalIgnoreCase)) LoadMemos(stem);
            PlaceBigPlayer();
            // Msg 0x1de: the boss button follows the boss while the status 253 gate is open, else it stays parked
            BigFit(out float bossScale, out float bossX0, out float bossY0);
            if (miniBossShown) classicBig.SetChildPosition("boss", (int) (bossX0 + miniBossX * bossScale - 13), (int) (bossY0 - (miniBossY * bossScale + 13)));
            else classicBig.SetChildPosition("boss", -100, -100);
            classicBig.Invalidate();
        }

        /// <summary>Per frame while open: the memo points' and facility icons' tooltips.</summary>
        private void TickClassicBigMinimap() {
            if (classicBig == null || !classicBig.Visible) return;
            RoClassicWindowManager windows = RoClassicUiSystem.Instance.Windows;
            if (windows.HoverWindow != classicBig) return;
            Vector2Int local = windows.Mouse - classicBig.Position;
            for (int i = 0; i < bigMemoCount; i++) {
                Vector2Int p = bigMemoPoints[i];
                if (local.x < p.x || local.y < p.y || local.x >= p.x + 15 || local.y >= p.y + 13) continue;
                if (!string.IsNullOrEmpty(bigMemoTexts[i])) RoClassicTooltip.Shared.Show(bigMemoTexts[i], new Color32(255, 255, 255, 255), windows.Mouse.x, windows.Mouse.y - 20);
                return;
            }

            BigFit(out float scale, out float x0, out float y0);
            if (bigQuest) {
                foreach (RoMinimapMarkerState.QuestMark mark in miniQuests) {
                    if (mark.Color == 0 || mark.X < 0 || mark.Y < 0) continue;
                    int qx = (int) (x0 + mark.X * scale - 6), qy = (int) (y0 - (mark.Y * scale + 6));
                    if (local.x < qx || local.y < qy || local.x >= qx + 14 || local.y >= qy + 14) continue;
                    RoClassicTooltip.Shared.Show(RoClassicUiHost.MsgStr(0xac0, "Quest NPC"), new Color32(255, 255, 255, 255), windows.Mouse.x, windows.Mouse.y - 20);
                    return;
                }
            }

            if (!bigFacility) return;
            foreach (RoMinimapTownInfoMarker marker in townInfoMarkers) {
                if (marker.Type == 8) continue;
                int px = (int) (x0 + marker.X * scale - 6), py = (int) (y0 - (marker.Y * scale + 6));
                if (local.x < px || local.y < py || local.x >= px + 12 || local.y >= py + 12) continue;
                RoClassicTooltip.Shared.Show(marker.Name, new Color32(255, 255, 255, 255), windows.Mouse.x, windows.Mouse.y - 20);
                return;
            }
        }

        /// <summary>0x8fef4b: 유저인터페이스\minimap\Quest_%d_%d.bmp from the mark's state and colour.</summary>
        private static string BigQuestArt(RoMinimapMarkerState.QuestMark mark) =>
            MiniUi + "minimap\\Quest_" + mark.State.ToString(CultureInfo.InvariantCulture) + "_" + mark.Color.ToString(CultureInfo.InvariantCulture) + ".bmp";

        // ---- the player button: UIHeadsprButton 0x7be550 (ACT action 0, frame 0, the first drawn layer of the head sprite) ----

        private void PlaceBigPlayer() {
            RoClassicChildLayout player = FindBigChild(BigPlayerId);
            if (player == null) return;
            if (!miniHasPlayer || !EnsureBigHead(out int width, out int height)) {
                classicBig.SetChildPosition("player", -100, -100);
                return;
            }

            if (player.States.Count == 0 || player.W != width || player.H != height) {
                player.States["normal"] = player.States["hover"] = player.States["pressed"] = bigHeadKey;
                player.W = width;
                player.H = height;
            }

            BigFit(out float scale, out float x0, out float y0);
            classicBig.SetChildPosition("player", (int) (x0 + miniCell.x * scale - 13), (int) (y0 - (miniCell.y * scale + 13)));
        }

        private RoClassicChildLayout FindBigChild(int id) {
            foreach (RoClassicChildLayout c in classicBig.Layout.Children) {
                if (c.Id == id) return c;
            }

            return null;
        }

        /// <summary>The own head sprite in its hair palette, decoded once per look.</summary>
        private bool EnsureBigHead(out int width, out int height) {
            width = height = 0;
            RoOnlineWorldController world = model.ResolveWorldController();
            RoRuntimeMapController map = model.ResolveRuntimeMapController(world);
            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            if (world == null || assets == null || !assets.IsInitialized || !world.State.TryGetActor(world.OwnActorId, out RoOnlineActorSnapshot self)) return false;
            string key = "@bigmap-head-" + self.Sex.ToString(CultureInfo.InvariantCulture) + "-" + self.Head.ToString(CultureInfo.InvariantCulture) + "-" +
                         self.HeadPalette.ToString(CultureInfo.InvariantCulture) + "-" + self.Job.ToString(CultureInfo.InvariantCulture);
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (key == bigHeadKey && system.Images.TryGet(key, out RoClassicUiImage cached)) {
                width = cached.Width;
                height = cached.Height;
                return true;
            }

            var request = new RoActorAppearanceRequest { kind = RoActorAppearanceKind.Player, isMale = self.Sex == 1, headId = self.Head, hairId = self.Head, hairColorId = self.HeadPalette, classId = self.Job };
            try {
                RoActorAppearanceTables tables = RoActorAppearanceTables.Load(assets);
                if (!tables.TryResolveHeadBasePath(assets, request, out string basePath)) return false;
                SPR spr = assets.Load<SPR>(basePath + ".spr");
                ACT act = assets.Load<ACT>(basePath + ".act");
                if (spr == null || act == null || !act.TryGetMotion(0, 0, out ACT.Frame frame) || frame.layers == null) return false;
                SPR.Frame image = null;
                foreach (ACT.Layer layer in frame.layers) {
                    if (layer.index >= 0 && layer.sprType == 0 && spr.TryGetFrame(layer.index, out image) && image.type == SPR.TYPE_PAL) break;
                    image = null;
                }

                if (image == null) return false;
                byte[] palette = spr.palette;
                if (self.HeadPalette != 0 && tables.TryResolveHairPalettePath(assets, request, out string palettePath) && assets.Exists(palettePath)) {
                    byte[] custom = assets.ReadBytes(palettePath);
                    if (custom != null && custom.Length >= 1024) palette = custom;
                }

                width = image.originalWidth > 0 ? image.originalWidth : image.width;
                height = image.originalHeight > 0 ? image.originalHeight : image.height;
                var pixels = new Color32[width * height];
                for (int i = 0; i < pixels.Length && i < image.data.Length; i++) {
                    int p = image.data[i];
                    if (p != 0) pixels[i] = new Color32(palette[p * 4], palette[p * 4 + 1], palette[p * 4 + 2], 255);
                }

                if (!system.Images.SetDynamic(key, width, height, pixels)) return false;
                bigHeadKey = key;
                return true;
            } catch (Exception ex) {
                Debug.LogWarning("Big minimap head " + key + ": " + ex.Message);
                return false;
            }
        }

        // ---- memo: the editor 0x112 and Memo\$$<map>.txt ----

        /// <summary>The memo file: the original's "%s\Memo\$$%s.txt" under its start directory; here under the persistent data.</summary>
        private static string MemoPath(string map) => Path.Combine(Application.persistentDataPath, "Memo", "$$" + map + ".txt");

        /// <summary>
        /// UIEditctrlWnd 0x112: 250 × 42 at the cursor; style-1 title "Record" (0xac3), the edit (5, 19, 200 × 18, 20 bytes,
        /// #C86464 border over #E8E8E8), Store 0xb4 (memo_ok_*) at (205, 19), the X 0xc5 at (236, 3). Enter stores.
        /// </summary>
        private void OpenMemoEditor() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (memoEditor == null) {
                var layout = new RoClassicWindowLayout { Class = "UIEditctrlWnd", WindowId = 0x112, Width = MemoW, Height = MemoH };
                RoClassicFrames.TitleBar(layout.Ops, MemoW, true);
                RoClassicFrames.Body(layout.Ops, MemoW, MemoH);
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = MemoCloseId, Path = "close", X = MemoW - 14, Y = 3, W = 11, H = 11 };
                close.States["normal"] = MiniUi + "basic_interface\\sys_close_off.bmp";
                close.States["hover"] = MiniUi + "basic_interface\\sys_close_on.bmp";
                close.States["pressed"] = MiniUi + "basic_interface\\sys_close_on.bmp";
                layout.Children.Add(close);
                layout.Children.Add(BigButton(MemoStoreId, "store", "memo_ok", 205, 19, 42, 20));
                memoEditor = new RoClassicWindow(system, layout) { Visible = false };
                memoEditor.DrawDynamic += w => {
                    string caption = RoClassicUiHost.MsgStr(0xac3, "Record");
                    w.TextOut(19, 4, caption, 0, 12, new Color32(255, 255, 255, 255));
                    w.TextOut(18, 3, caption, 0, 12, new Color32(0, 0, 0, 255));
                    w.FillRect(5, 19, 200, 18, new Color32(0xc8, 0x64, 0x64, 255));
                };
                memoEditor.Clicked += child => {
                    if (child.Id == MemoStoreId) StoreMemo();
                    else if (child.Id == MemoCloseId) CloseMemoEditor();
                };
                memoEdit = new RoClassicEditField { Rect = new RectInt(6, 20, 198, 16), MaxBytes = 20, Background = new Color32(0xe8, 0xe8, 0xe8, 255) };
                memoEdit.Submitted += _ => {
                    if (memoEdit.Text.Length > 0) StoreMemo();
                };
                memoEditor.AddEditField(memoEdit);
            }

            memoEdit.SetText(string.Empty);
            Vector2Int cursor = system.Windows.Mouse;
            memoEditor.MoveTo(cursor.x, cursor.y);
            memoEditor.Visible = true;
            system.Windows.BringToFront(memoEditor);
            memoEdit.Focus();
        }

        private void CloseMemoEditor() {
            if (memoEditor == null) return;
            memoEditor.Visible = false;
            if (memoEdit.HasFocus) RoClassicTextInput.Focus(null);
        }

        /// <summary>Store (0x8fe500): appends "%d\t%d\t%s\r\n" (the clicked point, the text), reloads the points, closes.</summary>
        private void StoreMemo() {
            string stem = miniPictureStem;
            if (string.IsNullOrEmpty(stem)) return;
            try {
                string path = MemoPath(stem);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string line = bigMemoPoint.x.ToString(CultureInfo.InvariantCulture) + "\t" + bigMemoPoint.y.ToString(CultureInfo.InvariantCulture) + "\t" + memoEdit.Text + "\r\n";
                byte[] bytes = Encoding.UTF8.GetBytes(line);
                using (var file = new FileStream(path, FileMode.Append, FileAccess.Write)) file.Write(bytes, 0, bytes.Length);
            } catch (Exception ex) {
                // the original leaves the editor open when the file does not open
                Debug.LogWarning("Memo store: " + ex.Message);
                return;
            }

            LoadMemos(stem);
            CloseMemoEditor();
            classicBig?.Invalidate();
        }

        /// <summary>
        /// 0x8fd520: every point parked, then up to 20 points from at most 39 lines; a line splits on tab, space, comma, CR,
        /// LF; fewer than two tokens skip it, a point outside 1..W / 1..H stops the load; the caption is every later token
        /// with a space before it, appended while it is at most 30 bytes long.
        /// </summary>
        private void LoadMemos(string stem) {
            bigMemoStem = stem;
            bigMemoCount = 0;
            Array.Clear(bigMemoTexts, 0, bigMemoTexts.Length);
            if (!string.IsNullOrEmpty(stem)) {
                try {
                    string path = MemoPath(stem);
                    if (File.Exists(path)) {
                        string[] lines = Encoding.UTF8.GetString(File.ReadAllBytes(path)).Split('\n');
                        for (int read = 0; read < lines.Length && read < 39 && bigMemoCount < MemoCount; read++) {
                            string[] tokens = lines[read].Split(new[] { '\t', ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length < 2) continue;
                            int x = Atol(tokens[0]), y = Atol(tokens[1]);
                            if (x < 1 || x > BigW || y < 1 || y > BigH) break;
                            var caption = new StringBuilder();
                            for (int t = 2; t < tokens.Length && caption.Length <= 30; t++) caption.Append(' ').Append(tokens[t]);
                            bigMemoPoints[bigMemoCount] = new Vector2Int(x, y);
                            bigMemoTexts[bigMemoCount] = caption.ToString();
                            bigMemoCount++;
                        }
                    }
                } catch (Exception ex) {
                    Debug.LogWarning("Memo load: " + ex.Message);
                }
            }

            if (classicBig == null) return;
            for (int i = 0; i < MemoCount; i++) {
                Vector2Int p = i < bigMemoCount ? bigMemoPoints[i] : new Vector2Int(-100, -100);
                classicBig.SetChildPosition("memo" + i.ToString(CultureInfo.InvariantCulture), p.x, p.y);
            }
        }

        /// <summary>Yes on 0xabf: the point goes and the file is rewritten ("wb") from the points left, in slot order.</summary>
        private void DeleteMemo(int index) {
            string stem = miniPictureStem;
            if (string.IsNullOrEmpty(stem) || index >= bigMemoCount) return;
            var sb = new StringBuilder();
            for (int i = 0; i < bigMemoCount; i++) {
                if (i == index || bigMemoPoints[i].x <= 0 || bigMemoPoints[i].y <= 0) continue;
                sb.Append(bigMemoPoints[i].x.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(bigMemoPoints[i].y.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append((bigMemoTexts[i] ?? string.Empty).TrimStart(' ')).Append("\r\n");
            }

            try {
                string path = MemoPath(stem);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes(sb.ToString()));
            } catch (Exception ex) {
                Debug.LogWarning("Memo delete: " + ex.Message);
            }

            LoadMemos(stem);
            classicBig?.Invalidate();
        }

        private static int Atol(string s) {
            int i = 0, sign = 1, value = 0;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) sign = s[i++] == '-' ? -1 : 1;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') value = value * 10 + (s[i++] - '0');
            return sign * value;
        }

        internal string SweepClassicBigMinimap(string command) {
            string[] parts = (command ?? string.Empty).Trim().Split(' ');
            switch (parts[0]) {
                case "open":
                    if (classicBig == null || !classicBig.Visible) ToggleClassicBigMinimap();
                    break;
                case "close":
                    CloseClassicBigMinimap();
                    break;
                case "record" when parts.Length >= 3 && classicBig != null && classicBig.Visible:
                    // record x y: arm and click a body point (the editor opens at the cursor)
                    bigMemoArmed = true;
                    BigPressed(new Vector2Int(int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture)));
                    break;
                case "memo" when parts.Length >= 2 && memoEditor != null && memoEditor.Visible:
                    memoEdit.SetText(string.Join(" ", parts, 1, parts.Length - 1));
                    StoreMemo();
                    break;
                case "delete" when parts.Length >= 2:
                    DeleteMemo(int.Parse(parts[1], CultureInfo.InvariantCulture));
                    break;
            }

            UpdateClassicBigMinimap();
            BigFit(out float scale, out float x0, out float y0);
            return "big=" + (classicBig != null && classicBig.Visible) + " map=" + miniPictureStem + " " + miniMapW + "x" + miniMapH +
                   " scale=" + scale.ToString("F3", CultureInfo.InvariantCulture) + " origin=" + x0 + "," + y0 + " party=" + bigParty.Count +
                   " checks=" + bigQuest + "/" + bigFacility + "/" + bigSocial + " memos=" + bigMemoCount + " head=" + bigHeadKey +
                   " editor=" + (memoEditor != null && memoEditor.Visible) + " excluded=" + IsMinimapExcluded(miniPictureStem) +
                   " guild=" + miniGuild.Count + " quests=" + miniQuests.Count + " boss=" + miniBossShown + "@" + miniBossX + "," + miniBossY;
        }
    }
}
