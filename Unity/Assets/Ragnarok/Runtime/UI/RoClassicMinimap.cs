using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// The HUD minimap (reference ui-static ui/RUNTIME-RULES-MAPS.md §0–§1): window 0xe
    /// (UIMinimapZoomWnd, 128×140 at (W − 145, 17): the " x  y" cell text, five 12×12 buttons, the town NPC icons) over the
    /// map picture the world render draws first (DrawMinimap 0xbf4100: map\&lt;map&gt;.bmp, 128×128 at (W − 144, 16), opaque or
    /// alpha 0xAA, 1/zoom of the picture centred on the player and clamped at the edges) with its marks (Mark 0xbf5c10):
    /// the rotated player arrow and its shadow, town icons or edge arrows, blinking compass crosses. Ctrl+Tab cycles
    /// opaque → translucent → closed. The data (map, player cell, towninfo, compass marks) is RoMinimapModel's, bound by
    /// the shell on every map entry and re-read when the session's gameplay sequence, the player's cell / direction or the
    /// compass marks move (checked every 0.1 s), or the server's marks change (every frame). The big minimap 0x111 is the
    /// other half of this class (RoClassicMinimap.Big.cs).
    /// </summary>
    public sealed partial class RoClassicMinimap {
        private static readonly ProfilerMarker RefreshMarker = new ProfilerMarker("RO.UI.Minimap.Refresh");
        private const float RefreshIntervalSeconds = 0.1f;
        private const string MiniUi = "유저인터페이스\\";
        private const string MiniPrefs = "ro.ui.classic.minimap.";
        private const int MiniObjectId = 0x1c0;
        private const int MiniPlusId = 0xd4;
        private const int MiniMinusId = 0xd5;
        private const int MiniMaxId = 0xb9;
        private const int MiniWorldId = 0x1c2;
        private const int MiniIconBaseId = 1000;
        private static readonly string[] MiniTownIcons = { "Store", "Weaponshop", "ArmorShops", "Smithy", "Guide", "Inn", "kafra", "style", "warp" };

        private static RoClassicMinimap instance;
        // F11's DeleteWindow(0xe) (RoClassicCloseTabs): gone until its MakeWindow(0xe) or Ctrl+Tab; the on / off choice is kept
        private static bool closeTabsDeleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            closeTabsDeleted = false;
        }

        /// <summary>F11's DeleteWindow(0xe): the minimap goes, Ctrl+Tab's choice (mode +0x254) stays.</summary>
        public static void CloseTabsDelete() {
            closeTabsDeleted = true;
            instance?.RefreshNow();
        }

        /// <summary>F11's MakeWindow(0xe) when mode +0x254 == 1: the minimap back unless Ctrl+Tab turned it off.</summary>
        public static void CloseTabsMake() {
            if (!closeTabsDeleted) return;
            closeTabsDeleted = false;
            instance?.RefreshNow();
        }

        private readonly RoMinimapModel model = new RoMinimapModel();
        private float nextRefreshTime;
        private RoClassicUiSystem miniSystem;
        private RoClassicWindow classicMini;
        private RoClassicUiLayer miniLayer;
        private bool classicMiniFailed;
        private bool miniClosed;
        private bool miniOpaque;
        private bool miniNpcIcons = true;
        private float miniZoom = 1f;
        private string miniPictureStem;
        private Color32[] miniPicture;
        private int miniPictureW, miniPictureH;
        private string miniViewKey;
        private readonly Color32[] miniView = new Color32[128 * 128];
        private readonly List<RoClassicChildLayout> miniIconButtons = new List<RoClassicChildLayout>();
        private readonly List<(RectInt Rect, string Name)> miniShownIcons = new List<(RectInt, string)>();
        private readonly Dictionary<uint, float> miniCompassSeen = new Dictionary<uint, float>();
        // the server's marks (RoMinimapMarkerState): guildmates, quest NPCs, the boss while the own status 253 is on
        private readonly List<(uint AccountId, int X, int Y)> miniGuild = new List<(uint, int, int)>();
        private readonly List<RoMinimapMarkerState.QuestMark> miniQuests = new List<RoMinimapMarkerState.QuestMark>();
        private readonly HashSet<uint> miniPartyIds = new HashSet<uint>();
        // the party marks (mode+0x1b4): members on this map with a position, AID ascending, the palette colour of the list index
        private readonly List<(uint AccountId, int X, int Y, Color32 Color)> miniParty = new List<(uint, int, int, Color32)>();
        // battleground teammates (0x2df, also mode+0x1b4): their colour comes from the party lookup 0xcec750, which leaves it
        // unset for an AID outside the party - drawn here in the palette's fallback (50, 50, 50)
        private readonly List<(uint AccountId, int X, int Y)> miniBattlefield = new List<(uint, int, int)>();
        private static readonly Color32 MiniBattlefieldColor = new Color32(0x32, 0x32, 0x32, 0xff);

        /// <summary>FUN_00cec660: the party member colour by its index in the session list (≥ 16 → (50, 50, 50)).</summary>
        private static readonly uint[] MiniPartyPalette = {
            0xFF0A3278, 0xFF007878, 0xFF000000, 0xFF780000, 0xFF007800, 0xFF787800, 0xFF780078, 0xFF783200,
            0xFF787878, 0xFF000078, 0xFF003232, 0xFF320000, 0xFF003200, 0xFF323200, 0xFF320032, 0xFF780000,
        };
        private readonly List<RoStatusIconSnapshot> miniStatusIcons = new List<RoStatusIconSnapshot>();
        private bool miniBossShown;
        private int miniBossX, miniBossY, miniMarkerSequence = int.MinValue, miniStatusSequence = int.MinValue, miniPartySequence = int.MinValue;
        private const ushort BossAlarmStatus = 253;   // SI_CASH_BOSS_ALARM (Convex Mirror)
        private static readonly Color32 MiniGuildColor = new Color32(0xff, 0xa6, 0xd2, 0xff);   // 0xffffa6d2

        private RoLoginShellSession session => model.Session;
        private Vector2Int miniCell => model.Cell;
        private int miniDirection => model.Direction;
        private bool miniHasPlayer => model.HasPlayer;
        private int miniMapW => model.MapWidth;
        private int miniMapH => model.MapHeight;
        private List<RoMinimapTownInfoMarker> townInfoMarkers => model.TownInfoMarkers;
        private List<RoOnlineMinimapMarkerSnapshot> minimapMarkerBuffer => model.Markers;

        /// <summary>
        /// The shell on a map entry (and the first gameplay window pass): the session, the online world and the runtime
        /// map now drawn; the minimap is redrawn at once.
        /// </summary>
        public static void Bind(RoLoginShellSession loginSession, RoOnlineWorldController onlineWorld, RoRuntimeMapController activeMap) {
            if (instance == null) instance = new RoClassicMinimap();
            instance.model.Bind(loginSession, onlineWorld, activeMap);
            instance.RefreshNow();
        }

        /// <summary>Bound since the map entry (until <see cref="Close"/>).</summary>
        public static bool IsBound => instance != null;

        /// <summary>
        /// Per frame (RoClassicGameUi): Ctrl+Tab, the tooltips, the blinking compass crosses and the server's marks; every
        /// 0.1 s the model's change check (gameplay sequence, player cell / direction, compass marks).
        /// </summary>
        public static void Tick() {
            RoClassicMinimap m = instance;
            if (m == null || m.classicMiniFailed) return;
            m.UpdateClassicMinimap();
            float now = Time.unscaledTime;
            if (now < m.nextRefreshTime) return;
            m.nextRefreshTime = now + RefreshIntervalSeconds;
            if (m.model.NeedsRefresh()) m.RefreshNow();
        }

        /// <summary>Leaving the map (character select, shell off): both minimaps and the memo editor go, the next entry starts afresh.</summary>
        public static void Close() {
            RoClassicMinimap m = instance;
            if (m == null) return;
            instance = null;
            m.DisposeClassicMinimap();
        }

        /// <summary>The window, the picture and the marks from a fresh read of the model.</summary>
        private void RefreshNow() {
            if (classicMiniFailed) return;
            using (RefreshMarker.Auto()) {
                try {
                    EnsureClassicMinimap();
                    model.Refresh();
                    string mapName = model.MapName;
                    CopyClassicServerMarks();
                    bool show = !miniClosed && !closeTabsDeleted && !string.IsNullOrEmpty(mapName) && !model.HiddenForMap;
                    LoadClassicMinimapPicture(model.MapStem);
                    classicMini.MoveTo(miniSystem.ClientSize.x - 145, 17);   // MakeWindow(0xe): Move(scrW − 145, 17)
                    classicMini.Visible = show;
                    miniLayer.Visible = show;
                    if (show) DrawClassicMinimapLayer();
                    UpdateClassicBigMinimap();
                    classicMini.Invalidate();
                } catch (Exception e) {
                    classicMiniFailed = true;
                    DisposeClassicMinimap();
                    Debug.LogException(e);
                }
            }
        }

        private void EnsureClassicMinimap() {
            if (classicMini != null) return;
            RoClassicUiSystem system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
            miniSystem = system;
            var layout = new RoClassicWindowLayout { Class = "UIMinimapZoomWnd", WindowId = 0xe, Width = 128, Height = 140 };
            // OnCreate 0x840640: object, plus, minus, mini, viewon at y 128, 12×12, minimap\i_<name>_1/_2/_3.bmp
            layout.Children.Add(MiniButton(MiniObjectId, "object", 1));
            layout.Children.Add(MiniButton(MiniPlusId, "plus", 73));
            layout.Children.Add(MiniButton(MiniMinusId, "minus", 87));
            layout.Children.Add(MiniButton(MiniMaxId, "mini", 101));
            layout.Children.Add(MiniButton(MiniWorldId, "viewon", 115));
            classicMini = new RoClassicWindow(system, layout) { Draggable = false, Visible = false };
            classicMini.DrawDynamic += DrawClassicMinimapWindow;
            classicMini.Clicked += OnClassicMinimapClicked;

            // the picture and the marks: the world render's, under every window
            miniLayer = system.Renderer.CreateLayer("minimap picture");
            miniLayer.AboveOverlayUi = true;
            system.Renderer.SendToBack(miniLayer);

            // registry M_MINIMAPARGB (default translucent), m_minimapZoomWnd (1.0); the buttons option DAT_01553ab0
            miniOpaque = PlayerPrefs.GetInt(MiniPrefs + "opaque", 0) != 0;
            miniZoom = Mathf.Clamp(PlayerPrefs.GetFloat(MiniPrefs + "zoom", 1f), 1f, 4f);
            SetClassicMinimapButtons(PlayerPrefs.GetInt(MiniPrefs + "buttons", 1) != 0);
        }

        private static RoClassicChildLayout MiniButton(int id, string name, int x) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = name, X = x, Y = 128, W = 12, H = 12 };
            button.States["normal"] = MiniUi + "minimap\\i_" + name + "_1.bmp";
            button.States["hover"] = MiniUi + "minimap\\i_" + name + "_2.bmp";
            button.States["pressed"] = MiniUi + "minimap\\i_" + name + "_3.bmp";
            return button;
        }

        /// <summary>msg 6 / 0x1f2: the five buttons shown and enabled only with the "minimap buttons" option.</summary>
        private void SetClassicMinimapButtons(bool on) {
            foreach (RoClassicChildLayout child in classicMini.Layout.Children) {
                if (child.Id < MiniIconBaseId) child.Visible = on;
            }

            classicMini.Invalidate();
        }

        /// <summary>/minimap (0xc0ac12): the window gets msg 6 / 0x1f2 again with the option the command just flipped.</summary>
        public static void ApplyButtonsOption(bool on) {
            RoClassicMinimap m = instance;
            if (m == null || m.classicMini == null) return;
            m.SetClassicMinimapButtons(on);
        }

        /// <summary>The PlayerPrefs key of the "minimap buttons" option ([0x01553ab0]); /minimap writes it.</summary>
        public const string ButtonsOptionKey = MiniPrefs + "buttons";

        /// <summary>The map picture, map\&lt;map&gt;.bmp (512×512), decoded once per map for the CPU view.</summary>
        private void LoadClassicMinimapPicture(string stem) {
            if (string.Equals(stem, miniPictureStem, StringComparison.OrdinalIgnoreCase)) return;
            miniPictureStem = stem;
            miniPicture = null;
            miniViewKey = null;
            Func<string, byte[]> source = RoClassicUiImages.DefaultSourceProvider?.Invoke();
            if (source == null || string.IsNullOrEmpty(stem)) {
                miniPictureStem = null; // assets not ready: try again
                return;
            }

            byte[] data = source("data\\texture\\" + MiniUi + "map\\" + stem + ".bmp");
            if (data != null && RoClassicUiImages.TryDecode(data, out int w, out int h, out Color32[] pixels)) {
                miniPicture = pixels;
                miniPictureW = w;
                miniPictureH = h;
            }
        }

        /// <summary>DrawMinimap 0xbf4100's UV window: 1/zoom of the picture centred on the player, clamped at the edges.</summary>
        private Vector4 ClassicMinimapUv() {
            float half = 1f / (miniZoom * 2f);
            float u0 = (float) miniCell.x / miniMapW - half, u1 = (float) miniCell.x / miniMapW + half;
            float v0 = (float) (miniMapH - miniCell.y) / miniMapH - half, v1 = (float) (miniMapH - miniCell.y) / miniMapH + half;
            if (u0 < 0) { u0 = 0; u1 = Mathf.Min(2 * half, 1); }
            if (v0 < 0) { v0 = 0; v1 = Mathf.Min(2 * half, 1); }
            if (u1 > 1) { u1 = 1; u0 = Mathf.Max(1 - 2 * half, 0); }
            if (v1 > 1) { v1 = 1; v0 = Mathf.Max(1 - 2 * half, 0); }
            return new Vector4(u0, v0, u1, v1);
        }

        /// <summary>The picture, then the marks in the render's order (player arrow, town icons, compass crosses).</summary>
        private void DrawClassicMinimapLayer() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            int left = system.ClientSize.x - 144;
            Vector4 uv = ClassicMinimapUv();
            RoClassicUiQuads q = miniLayer.Quads;
            q.Clear();
            byte alpha = miniOpaque ? (byte) 0xff : (byte) 0xaa;
            if (miniPicture != null && miniHasPlayer) {
                UpdateClassicMinimapView(uv);
                if (system.Images.TryGet("classic-minimap-view", out RoClassicUiImage view)) {
                    q.Image(left, 16, 128, 128, view, new Color32(255, 255, 255, alpha));
                }
            }

            miniShownIcons.Clear();
            foreach (RoClassicChildLayout icon in miniIconButtons) icon.Visible = false;
            if (miniHasPlayer) {
                // 1. the player: map_arrow.bmp in a 10×10 square turned (180 + 45·direction)° clockwise, its shadow first
                if (ClassicMinimapMark(uv, left, miniCell.x, miniCell.y, 5, false, out RectInt r) &&
                    system.Images.TryGet(MiniUi + "map\\map_arrow.bmp", out RoClassicUiImage arrow)) {
                    float angle = ((miniDirection + 4) % 8) * 45f;
                    q.RotatedQuad(r.x - 2, r.y - 2, r.width + 4, r.height + 6, angle, arrow.Uv, arrow.Page, new Color32(0x33, 0x33, 0x33, 0x77));
                    q.RotatedQuad(r.x, r.y, r.width, r.height, angle, arrow.Uv, arrow.Page, new Color32(255, 255, 255, 255));
                }
            }

            // 3. the boss (status 253 on and a live position): bossmonster.bmp clamped to the view, without an edge arrow
            if (miniBossShown && ClassicMinimapMarkClamped(uv, left, miniBossX, miniBossY, 6, out RectInt bossRect, out _) &&
                system.Images.TryGet(MiniUi + "map\\bossmonster.bmp", out RoClassicUiImage boss)) {
                q.Image(bossRect.x, bossRect.y, bossRect.width, bossRect.height, boss, new Color32(255, 255, 255, 255));
            }

            // 4. town NPC icons (while "Show information" is on): the window's icon buttons, or an edge arrow when off the view
            if (miniNpcIcons) {
                for (int i = 0; i < townInfoMarkers.Count; i++) {
                    RoMinimapTownInfoMarker marker = townInfoMarkers[i];
                    if (marker.Type < 0 || marker.Type >= MiniTownIcons.Length - 1) break;   // the loop stops at type 8 / 9
                    if (!ClassicMinimapMarkClamped(uv, left, marker.X, marker.Y, 6, out RectInt r, out int side)) continue;
                    if (side != 0) {
                        if (system.Images.TryGet(MiniUi + "basic_interface\\quest_arrow" + side.ToString(CultureInfo.InvariantCulture) + ".bmp", out RoClassicUiImage edge)) {
                            q.Image(r.x, r.y, r.width, r.height, edge, new Color32(255, 255, 255, 255));
                        }

                        continue;
                    }

                    RoClassicChildLayout button = ClassicMinimapIcon(i, marker.Type);
                    button.Visible = true;
                    classicMini.SetChildPosition(button.Path, r.x - classicMini.Position.x, r.y - classicMini.Position.y);
                    miniShownIcons.Add((r, marker.Name));
                }
            }

            // 5. 0xbf36b0: the party first — a 6×6 0x99FFFFFF square under a 4×4 one in the member's colour (type 2, half 2,
            // culled, no edge arrow), AID ascending
            foreach ((uint _, int px, int py, Color32 color) in miniParty) {
                if (px < 0 || py < 0 || !ClassicMinimapMark(uv, left, px, py, 2, true, out RectInt pr)) continue;
                q.Image(pr.x - 1, pr.y - 1, pr.width + 2, pr.height + 2, system.White, new Color32(255, 255, 255, 0x99));
                q.Image(pr.x, pr.y, pr.width, pr.height, system.White, color);
            }

            foreach ((uint id, int bx, int by) in miniBattlefield) {
                if (bx < 0 || by < 0 || miniPartyIds.Contains(id) || !ClassicMinimapMark(uv, left, bx, by, 2, true, out RectInt br)) continue;
                q.Image(br.x - 1, br.y - 1, br.width + 2, br.height + 2, system.White, new Color32(255, 255, 255, 0x99));
                q.Image(br.x, br.y, br.width, br.height, system.White, MiniBattlefieldColor);
            }

            // then guildmates not in the party as triangles (type 1, half 2: a 0xAAFFFFFF one of half 3 under the
            // 0xFFFFA6D2 one), then the quest NPCs (type 0, basic_interface\quest_<colour>.bmp, half 4, clamped to edge arrows)
            foreach ((uint id, int gx, int gy) in miniGuild) {
                if (gx < 0 || gy < 0 || miniPartyIds.Contains(id)) continue;
                if (!ClassicMinimapMark(uv, left, gx, gy, 2, true, out _)) continue;
                Vector2 p = ClassicMinimapPoint(uv, left, gx, gy);
                ClassicMinimapTriangle(q, system, p, 3f, new Color32(255, 255, 255, 0xaa));
                ClassicMinimapTriangle(q, system, p, 2f, MiniGuildColor);
            }

            foreach (RoMinimapMarkerState.QuestMark mark in miniQuests) {
                if (mark.Color == 0 || mark.X < 0 || mark.Y < 0) continue;
                if (!ClassicMinimapMarkClamped(uv, left, mark.X, mark.Y, 4, out RectInt qr, out int side)) continue;
                string art = side != 0
                    ? MiniUi + "basic_interface\\quest_arrow" + side.ToString(CultureInfo.InvariantCulture) + ".bmp"
                    : MiniUi + "basic_interface\\quest_" + mark.Color.ToString(CultureInfo.InvariantCulture) + ".bmp";
                if (system.Images.TryGet(art, out RoClassicUiImage questArt)) q.Image(qr.x, qr.y, qr.width, qr.height, questArt, new Color32(255, 255, 255, 255));
            }

            // 6. compass marks: a cross 8 px across, blinking 500 ms on / 500 ms off
            float now = Time.unscaledTime;
            foreach (RoOnlineMinimapMarkerSnapshot mark in minimapMarkerBuffer) {
                uint key = ((uint) mark.MarkerId << 24) ^ mark.SourceId;
                if (!miniCompassSeen.TryGetValue(key, out float since)) miniCompassSeen[key] = since = now;
                if ((now - since) % 1f >= 0.5f) continue;
                if (!ClassicMinimapMark(uv, left, mark.CellX, mark.CellY, 1, true, out RectInt r)) continue;
                uint raw = mark.ColorRaw;
                var color = raw == 0 ? new Color32(255, 219, 56, 255) : new Color32((byte) (raw >> 16), (byte) (raw >> 8), (byte) raw, 255);
                float sx = r.x + 1f, sy = r.y + 1f;
                q.Image(sx - 4, r.y, 8, r.height, system.White, color);
                q.Image(r.x, sy - 4, r.width, 8, system.White, color);
            }

            miniLayer.MarkDirty();
        }

        /// <summary>
        /// The 128×128 view of the picture for the UV window, sampled bilinearly (the renderer's filtered quad), rebuilt only
        /// when the window moves (cell, zoom, map).
        /// </summary>
        private void UpdateClassicMinimapView(Vector4 uv) {
            string key = miniPictureStem + "|" + uv.ToString("F5");
            if (key == miniViewKey) return;
            miniViewKey = key;
            int pw = miniPictureW, ph = miniPictureH;
            for (int y = 0; y < 128; y++) {
                float fy = (uv.y + (uv.w - uv.y) * (y + 0.5f) / 128f) * ph - 0.5f;
                int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, ph - 1), y1 = Mathf.Min(y0 + 1, ph - 1);
                float ty = Mathf.Clamp01(fy - y0);
                for (int x = 0; x < 128; x++) {
                    float fx = (uv.x + (uv.z - uv.x) * (x + 0.5f) / 128f) * pw - 0.5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, pw - 1), x1 = Mathf.Min(x0 + 1, pw - 1);
                    float tx = Mathf.Clamp01(fx - x0);
                    Color32 a = miniPicture[y0 * pw + x0], b = miniPicture[y0 * pw + x1], c = miniPicture[y1 * pw + x0], d = miniPicture[y1 * pw + x1];
                    miniView[y * 128 + x] = Color32.Lerp(Color32.Lerp(a, b, tx), Color32.Lerp(c, d, tx), ty);
                }
            }

            RoClassicUiSystem.Instance.Images.SetDynamic("classic-minimap-view", 128, 128, miniView);
        }

        /// <summary>The sweep console's !wm: the classic world map with the minimap's session.</summary>
        public static string SweepWorldMap(string command) =>
            instance != null ? RoClassicWorldMap.Sweep(instance.session, command) : "no minimap window";

        /// <summary>The session's marks for this refresh: guildmates, quest NPCs, the party's AIDs and the boss gate.</summary>
        private void CopyClassicServerMarks() {
            miniGuild.Clear();
            miniQuests.Clear();
            miniBattlefield.Clear();
            miniPartyIds.Clear();
            miniBossShown = false;
            if (session == null) return;
            miniMarkerSequence = session.MinimapMarkers.Sequence;
            miniPartySequence = session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Party);
            miniStatusSequence = session.StatusIconChangeSequence;
            session.MinimapMarkers.CopyGuild(miniGuild);
            session.MinimapMarkers.CopyQuests(miniQuests);
            session.MinimapMarkers.CopyBattlefield(miniBattlefield);
            miniParty.Clear();
            RoPartyMemberRuntimeSnapshot[] members = session.PartySnapshot().Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>();
            uint selfChar = session.LocalCharacterIds().CharId;
            string stem = miniPictureStem ?? string.Empty;
            for (int i = 0; i < members.Length; i++) {
                RoPartyMemberRuntimeSnapshot m = members[i];
                miniPartyIds.Add(m.AccountId);
                // 0x0107 never comes for the own character; offline members and other maps are erased (Msg 0x40)
                if (m.CharacterId == selfChar || m.State != 0) continue;
                string map = m.MapName ?? string.Empty;
                int dot = map.LastIndexOf('.');
                if (dot > 0) map = map.Substring(0, dot);
                if (!string.Equals(map, stem, StringComparison.OrdinalIgnoreCase)) continue;
                uint argb = i < MiniPartyPalette.Length ? MiniPartyPalette[i] : 0xFF323232;
                miniParty.Add((m.AccountId, m.X, m.Y, new Color32((byte) (argb >> 16), (byte) (argb >> 8), (byte) argb, 255)));
            }

            miniParty.Sort((a, b) => a.AccountId.CompareTo(b.AccountId));
            session.StatusIconSnapshot(miniStatusIcons);
            bool alarm = false;
            foreach (RoStatusIconSnapshot icon in miniStatusIcons) {
                if (icon.StatusId == BossAlarmStatus && icon.Active) alarm = true;
            }

            miniBossShown = alarm && session.MinimapMarkers.TryGetBoss(out miniBossX, out miniBossY);
        }

        /// <summary>0xbf4780's triangle: (x, y − 2r), (x − √3·r, y + r), (x + √3·r, y + r).</summary>
        private static void ClassicMinimapTriangle(RoClassicUiQuads q, RoClassicUiSystem system, Vector2 c, float r, Color32 color) {
            const float Sqrt3 = 1.7320508f;
            var top = new Vector2(c.x, c.y - 2f * r);
            var right = new Vector2(c.x + Sqrt3 * r, c.y + r);
            var leftCorner = new Vector2(c.x - Sqrt3 * r, c.y + r);
            q.QuadCorners(top, top, right, leftCorner, system.White.Uv, system.White.Page, color);
        }

        /// <summary>Mark 0xbf5c10's position: the cell in the letterboxed 128 px fit, through the UV window.</summary>
        private Vector2 ClassicMinimapPoint(Vector4 uv, int left, int cellX, int cellY) {
            int dmax = Math.Max(miniMapW, miniMapH);
            double fw = miniMapW * 128.0 / dmax, fh = miniMapH * 128.0 / dmax;
            double mx = cellX * fw / miniMapW + (128 - fw) / 2, my = fh - cellY * fh / miniMapH + (128 - fh) / 2;
            double du = uv.z - uv.x, dv = uv.w - uv.y;
            return new Vector2((float) (left - uv.x * 128 / du + mx / du), (float) (16 - uv.y * 128 / dv + my / dv));
        }

        /// <summary>An unclamped mark: its square (truncated), or false when it leaves the 128×128 view (unless <paramref name="always"/>).</summary>
        private bool ClassicMinimapMark(Vector4 uv, int left, int cellX, int cellY, int half, bool cull, out RectInt rect) {
            Vector2 s = ClassicMinimapPoint(uv, left, cellX, cellY);
            rect = new RectInt((int) (s.x - half), (int) (s.y - half), 2 * half, 2 * half);
            return !cull || !(s.x - half < left || s.x + half > left + 128 || s.y - half < 16 || s.y + half > 144);
        }

        /// <summary>A clamped mark: kept inside the view, side 1/2/3/4 (left/right/top/bottom; a horizontal side wins) when it was outside.</summary>
        private bool ClassicMinimapMarkClamped(Vector4 uv, int left, int cellX, int cellY, int half, out RectInt rect, out int side) {
            Vector2 s = ClassicMinimapPoint(uv, left, cellX, cellY);
            side = 0;
            if (s.x < left + half) { s.x = left + half; side = 1; }
            else if (s.x > left + 128 - half) { s.x = left + 128 - half; side = 2; }
            if (s.y < 16 + half) { s.y = 16 + half; if (side == 0) side = 3; }
            else if (s.y > 144 - half) { s.y = 144 - half; if (side == 0) side = 4; }
            rect = new RectInt((int) (s.x - half), (int) (s.y - half), 2 * half, 2 * half);
            return true;
        }

        /// <summary>The icon button of town record <paramref name="index"/> (id 1000 + i, all states the type's Information\*.bmp).</summary>
        private RoClassicChildLayout ClassicMinimapIcon(int index, int type) {
            while (miniIconButtons.Count <= index) {
                var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = MiniIconBaseId + miniIconButtons.Count, Path = "icon" + miniIconButtons.Count, W = 12, H = 12, Visible = false };
                miniIconButtons.Add(button);
                classicMini.Layout.Children.Add(button);
            }

            RoClassicChildLayout icon = miniIconButtons[index];
            string path = MiniUi + "Information\\" + MiniTownIcons[Mathf.Clamp(type, 0, MiniTownIcons.Length - 1)] + ".bmp";
            icon.States["normal"] = icon.States["hover"] = icon.States["pressed"] = path;
            return icon;
        }

        /// <summary>OnDraw 0x84ede0: " %d  %d" at (19, 128) black, then (20, 128) white.</summary>
        private void DrawClassicMinimapWindow(RoClassicWindow w) {
            if (!miniHasPlayer) return;
            string text = " " + miniCell.x.ToString(CultureInfo.InvariantCulture) + "  " + miniCell.y.ToString(CultureInfo.InvariantCulture);
            w.TextOut(19, 128, text, 0, 12, new Color32(0, 0, 0, 255));
            w.TextOut(20, 128, text, 0, 12, new Color32(255, 255, 255, 255));
        }

        private void OnClassicMinimapClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case MiniObjectId:
                    miniNpcIcons = !miniNpcIcons;
                    break;
                case MiniPlusId:
                    miniZoom = Mathf.Min(4f, miniZoom + (miniZoom / 3f + miniZoom) * 0.25f);
                    break;
                case MiniMinusId:
                    miniZoom = Mathf.Max(1f, miniZoom - miniZoom * 0.25f);
                    break;
                case MiniWorldId:
                    RoClassicWorldMap.Toggle(session);
                    return;
                case MiniMaxId:
                    // 0xd01880 decides (ExceptionMinimapNameTable.txt); then MakeWindow / DeleteWindow(0x111)
                    ToggleClassicBigMinimap();
                    return;
                default:
                    // vf37 0x85fad0: a TownInfo icon (id 1000 + record) guides to its cell without the window
                    int record = child.Id - MiniIconBaseId;
                    if (record >= 0 && record < townInfoMarkers.Count) RoClassicNavigation.NavigateHere(townInfoMarkers[record].X, townInfoMarkers[record].Y);
                    return;
            }

            PlayerPrefs.SetFloat(MiniPrefs + "zoom", miniZoom);
            RefreshNow();
        }

        /// <summary>Per frame: Ctrl+Tab (action 0x71) and the town icon tooltips (their NPC names).</summary>
        private void UpdateClassicMinimap() {
            if (classicMini == null) return;
            TickClassicBigMinimap();
            if (session != null && (session.MinimapMarkers.Sequence != miniMarkerSequence || session.StatusIconChangeSequence != miniStatusSequence ||
                                    session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Party) != miniPartySequence)) {
                RefreshNow();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && keyboard.ctrlKey.isPressed && !RoClassicTextInput.HasFocus) {
                // 0x9d44f8: opaque → translucent; translucent → closed; closed → opaque
                closeTabsDeleted = false;   // Ctrl+Tab makes / deletes the window itself
                if (miniClosed) {
                    miniClosed = false;
                    miniOpaque = true;
                } else if (miniOpaque) {
                    miniOpaque = false;
                } else {
                    miniClosed = true;
                }

                PlayerPrefs.SetInt(MiniPrefs + "opaque", miniOpaque ? 1 : 0);
                RefreshNow();
            }

            // compass crosses blink: redraw while any is up
            if (minimapMarkerBuffer.Count > 0 && classicMini.Visible) DrawClassicMinimapLayer();

            RoClassicWindowManager windows = RoClassicUiSystem.Instance != null ? RoClassicUiSystem.Instance.Windows : null;
            if (windows == null || windows.HoverWindow != classicMini) return;
            foreach ((RectInt rect, string name) in miniShownIcons) {
                if (!rect.Contains(windows.Mouse)) continue;
                RoClassicTooltip.Shared.Show(name, new Color32(255, 255, 255, 255), rect.x, rect.y + 14);
                return;
            }
        }

        private void DisposeClassicMinimap() {
            classicMini?.Destroy();
            classicMini = null;
            classicBig?.Destroy();
            classicBig = null;
            bigPictureStem = null;
            CloseMemoEditor();
            memoEditor?.Destroy();
            memoEditor = null;
            memoEdit = null;
            if (miniLayer != null && miniSystem != null) miniSystem.Renderer.RemoveLayer(miniLayer);
            miniLayer = null;
            miniIconButtons.Clear();
        }

        // ---- diagnostics console (RoSkillSweepRunner "!mm ...") ----

        /// <summary>zoom in|out | cycle | icons | party | talk x y | big ... | state.</summary>
        public static string Sweep(string command) => instance != null ? instance.SweepClassicMinimap(command) : "no minimap window";

        private string SweepClassicMinimap(string command) {
            if (classicMini == null) return "no classic minimap";
            string trimmed = (command ?? string.Empty).Trim();
            if (trimmed.StartsWith("big", StringComparison.Ordinal)) return SweepClassicBigMinimap(trimmed.Substring(3));
            if (trimmed.StartsWith("talk ", StringComparison.Ordinal)) {
                // talk x y: CZ_CONTACTNPC to the NPC nearest to cell (x, y) (the NPC windows exist only after a first talk)
                string[] p = trimmed.Split(' ');
                int tx = int.Parse(p[1], CultureInfo.InvariantCulture), ty = int.Parse(p[2], CultureInfo.InvariantCulture);
                var actors = new List<RoOnlineActorSnapshot>();
                model.ResolveWorldController()?.SnapshotActors(actors);
                uint best = 0;
                int bestD = int.MaxValue;
                foreach (RoOnlineActorSnapshot actor in actors) {
                    if (actor.ObjectType != Ragnarok.Network.RoMapActorObjectType.Npc) continue;
                    int d = Math.Abs(actor.CellX - tx) + Math.Abs(actor.CellY - ty);
                    if (d < bestD) {
                        bestD = d;
                        best = actor.ServerId;
                    }
                }

                if (best != 0) session?.BeginContactNpc(unchecked((int) best));
                return "talk: npc " + best + " at distance " + bestD;
            }

            if (trimmed == "party") {
                var sb = new System.Text.StringBuilder("party marks " + miniParty.Count + ":");
                foreach ((uint id, int px, int py, Color32 c) in miniParty) sb.Append(' ').Append(id).Append('@').Append(px).Append(',').Append(py).Append('#').Append(c.r).Append('/').Append(c.g).Append('/').Append(c.b);
                sb.Append(" | members:");
                foreach (RoPartyMemberRuntimeSnapshot m in session?.PartySnapshot().Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>()) {
                    sb.Append(' ').Append(m.Name).Append('(').Append(m.AccountId).Append('/').Append(m.CharacterId).Append(") ").Append(m.MapName).Append(' ').Append(m.X).Append(',').Append(m.Y).Append(" state ").Append(m.State);
                }

                return sb.ToString();
            }

            switch (trimmed) {
                case "zoom in": OnClassicMinimapClicked(new RoClassicChildLayout { Id = MiniPlusId }); break;
                case "zoom out": OnClassicMinimapClicked(new RoClassicChildLayout { Id = MiniMinusId }); break;
                case "icons": OnClassicMinimapClicked(new RoClassicChildLayout { Id = MiniObjectId }); break;
                case "cycle":
                    if (miniClosed) { miniClosed = false; miniOpaque = true; } else if (miniOpaque) miniOpaque = false; else miniClosed = true;
                    RefreshNow();
                    break;
            }

            Vector4 uv = ClassicMinimapUv();
            return $"shown={classicMini.Visible} cell={miniCell.x},{miniCell.y} dir={miniDirection} map={miniPictureStem} {miniMapW}x{miniMapH} " +
                $"picture={(miniPicture != null ? miniPictureW + "x" + miniPictureH : "none")} zoom={miniZoom:F3} opaque={miniOpaque} closed={miniClosed} " +
                $"uv={uv.x:F3},{uv.y:F3},{uv.z:F3},{uv.w:F3} icons={miniShownIcons.Count} marks={minimapMarkerBuffer.Count}";
        }
    }
}
