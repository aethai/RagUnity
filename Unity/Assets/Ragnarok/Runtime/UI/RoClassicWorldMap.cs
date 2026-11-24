using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// The world map UIRoMapWnd 0x8c with the classic UI on (codex-ui-research RUNTIME-RULES-MAPS.md and the world map spec
    /// of 0x5d9a90 / 0x5dad20 / 0x88ee30), screen-sized at (0, 0) and not draggable.
    /// <para>Data: worldviewdata_list / _table / _language (overlay, plain Lua). A map row is skipped when mapnametable.txt
    /// has no name for it (0x5dbf90); rows join their group (id &lt; 100 a dungeon, whose maps share the label box and whose
    /// Dun row gives the 20 px dot, the label and the level range; ≥ 100 a field group, the union of its map tiles). The
    /// background (1280 × 1024) is stretched over (0, 17, W, H − 17) and every rectangle goes through 0x5dad20:
    /// L' = (int)(L · W / texW + 0.5), T' = (int)(T · (H − 17) / texH + 0.5) + 17.</para>
    /// <para>Direct pass (under the DIB): the background, the party faces (wv_face1 on the current map, wv_face elsewhere,
    /// 2r square, r = font index · 2 + 8), the dungeon art (line 0xB4FF0000 / hovered 0xFFE66400, wv_dunPos in the dot,
    /// wv_dunSelect in the box at 0xB4FFFFFF / hovered opaque), the hover fill 0x46950F0F with the 130 px preview
    /// (map\&lt;name&gt;.bmp on 0x88000000) and the info box fill 0x66000000. DIB: the dungeon labels (#C8C8C8 over a #282828
    /// shadow), the party outlines (#E8AE23 current map, #F7E27E others), the Monster overlay (level ranges; field tiles
    /// with a #505050 outline, the name #80FF80 and the bold level), the info box (party names, own in #F08080), the hover
    /// outlines (#E66400) and texts (file name, mapnametable name #FFD200), the style-1 title bar and the #C6DFF7
    /// separator. The world combo (17, 0) 110 × 18, the Monster(Tab) check box (W − 100, 3) and the close button (W − 18, 0).</para>
    /// <para>Clicks: a dungeon label opens the floor list 0x1a; a field tile and a floor go to <see cref="NavigateRequested"/>
    /// (the navigation is not ported: nothing happens without a handler). Search, navigation buttons and their layers
    /// are not drawn. The hover routes (Kafra moves, airships) are in RoClassicWorldMap.Routes.cs.</para>
    /// <para>Layers: the direct pass's static part (background, faces, dungeon art, hover fill, preview, info fill) is the
    /// base layer, redrawn on a hover / world / party change; the routes layer above it is redrawn every frame while a
    /// route animates; the window's own surface is the DIB on top. Both layers are the window's UnderLayers.</para>
    /// </summary>
    public sealed partial class RoClassicWorldMap : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string Wv = Ui + "worldview_interface\\";
        private const string BackgroundKey = "@worldmap-background";
        private const string PreviewKey = "@worldmap-preview";
        private const int TitleH = 17, Preview = 130, MaxRows = 399;
        private const int CloseId = 0xc5;
        private const int ComboX = 17, ComboW = 110, ComboH = 18, ComboListH = 0x43, ComboRowH = 16, ComboButton = 18;
        private const int PopupRowH = 16, PopupMaxH = 160;

        // worldviewdata_info.lub: fonts by the screen-width index (0x5daf30), colours (the MobName shadow never arrives: 0)
        private static readonly int[] DungeonFont = { 9, 10, 11, 12 };
        private static readonly int[] MobNameFont = { 9, 10, 11, 12 };
        private static readonly int[] MobLvFont = { 12, 14, 20, 25 };
        private static readonly Color32 DungeonText = new Color32(200, 200, 200, 255);
        private static readonly Color32 DungeonShadow = new Color32(40, 40, 40, 255);
        private static readonly Color32 MobName = new Color32(128, 255, 128, 255);
        private static readonly Color32 MobBoxLine = new Color32(80, 80, 80, 255);
        private static readonly Color32 Select = new Color32(230, 100, 0, 255);
        private static readonly Color32 DungeonLine = new Color32(255, 0, 0, 180);
        private static readonly Color32 DungeonArt = new Color32(255, 255, 255, 180);
        private static readonly Color32 SelectBox = new Color32(0x95, 0x0f, 0x0f, 0x46);
        private static readonly Color32 PreviewBackdrop = new Color32(0, 0, 0, 0x88);
        private static readonly Color32 InfoFill = new Color32(0, 0, 0, 0x66);
        private static readonly Color32 Face = new Color32(255, 255, 255, 0xee);
        private static readonly Color32 PartyHere = new Color32(0xe8, 0xae, 0x23, 255);
        private static readonly Color32 PartyThere = new Color32(0xf7, 0xe2, 0x7e, 255);
        private static readonly Color32 Separator = new Color32(0xc6, 0xdf, 0xf7, 255);
        private static readonly Color32 OwnName = new Color32(0xf0, 0x80, 0x80, 255);
        private static readonly Color32 StemOutline = new Color32(0x5c, 0x5c, 0x5c, 255);
        private static readonly Color32 DisplayName = new Color32(0xff, 0xd2, 0x00, 255);
        private static readonly Color32 ComboFrame = new Color32(100, 100, 100, 255);
        private static readonly Color32 ComboFill = new Color32(232, 232, 232, 255);
        private static readonly Color32 ListHighlight = new Color32(0x70, 0x9f, 0xed, 255);   // colorchip (6, 2)
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        /// <summary>Where a field tile or a dungeon floor click goes (the map name without ".rsw"); none = nothing happens.</summary>
        public static Action<string> NavigateRequested;

        private static RoClassicWorldMap instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            NavigateRequested = null;
        }

        /// <summary>Screen-space left / top / right / bottom; the client's hit tests are strict.</summary>
        private struct Edges {
            public int L, T, R, B;

            public Edges(int l, int t, int r, int b) {
                L = l;
                T = t;
                R = r;
                B = b;
            }

            public int W => R - L;
            public int H => B - T;
            public bool Contains(int x, int y) => L < x && x < R && T < y && y < B;
            public bool SameAs(Edges o) => L == o.L && T == o.T && R == o.R && B == o.B;
        }

        private sealed class MapEntry {
            public string Stem, Label, Level;
            public Edges Raw, Rect;
        }

        private sealed class Group {
            public int Id;
            public string Label = string.Empty, Level = string.Empty;
            public Edges Raw, Rect;
            public readonly List<MapEntry> Maps = new List<MapEntry>();
            public bool IsDungeon => Id < 100;

            /// <summary>The label box: the first map's rectangle (a dungeon's maps share it), else the group's own.</summary>
            public Edges Box => Maps.Count > 0 ? Maps[0].Rect : Rect;
        }

        private sealed class World {
            public string Name, Background;
            public readonly List<Group> Groups = new List<Group>();
            public Vector2Int TransformedFor = new Vector2Int(-1, -1);
            public Vector2Int TextureSize;
        }

        /// <summary>0x87b8a0's location node: a map with the party members (or the own character) on it.</summary>
        private sealed class Node {
            public int World;
            public MapEntry Map;
            public readonly List<string> Names = new List<string>();
        }

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private RoClassicUiLayer baseLayer, routesLayer;
        private bool baseDirty;
        private RoClassicWindow popup;
        private RoLoginShellSession session;
        private RoWorldMapCatalog builtFrom;
        private readonly List<World> worlds = new List<World>();
        private readonly List<Node> nodes = new List<Node>();
        private int shown;
        private bool monster;
        private int fontIndex;
        private Vector2Int laidOutFor;
        private string currentStem = string.Empty, ownName = string.Empty;
        private float nextPartyPoll;
        private string partyKey = string.Empty;

        // background: the world whose picture sits in the dynamic image
        private int backgroundWorld = -1;
        private bool backgroundReady;

        // hover (0x893520): the map under the mouse, its group, the party node, the preview and info rectangles
        private Vector2Int lastMouse = new Vector2Int(int.MinValue, int.MinValue);
        private bool hoverPinned;   // the sweep console's hover holds until "hover off" (the real cursor is elsewhere)
        private Group hoverGroup;
        private MapEntry hoverMap;
        private Node hoverNode;
        private Edges previewRect;
        private bool previewImage;
        private string previewStem;
        private RectInt infoRect;
        private bool infoShown;
        private int infoLineH;

        // the world combo and its list, the floor popup
        private bool comboOpen;
        private int comboRow = -1;
        private Group popupGroup;
        private int popupFirst, popupHover = -1, popupWidth;

        /// <summary>Ctrl + ` / the menu icon / the minimap's World button: true when the classic map took it.</summary>
        public static bool Toggle(RoLoginShellSession session) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return false;
            if (instance == null || instance.system != system) {
                instance = new RoClassicWorldMap { system = system };
                system.Register(instance);
            }

            RoClassicWorldMap map = instance;
            map.session = session;
            if (map.window != null && map.window.Visible) map.CloseWindow();
            else map.Open();
            return true;
        }

        /// <summary>The map closes with the gameplay windows (map change to the login, character select).</summary>
        public static void Close() => instance?.CloseWindow();

        private void Open() {
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null) return;
            RoWorldMapCatalog catalog = RoWorldMapDataLoader.Load(assets);
            if (!ReferenceEquals(catalog, builtFrom)) Build(catalog, assets);
            if (worlds.Count == 0) {
                Debug.LogWarning("World map: no worldviewdata worlds (" + string.Join("; ", catalog.Diagnostics) + ")");
                return;
            }

            EnsureWindow();
            RoShellMapHudSnapshot hud = session != null ? session.MapHudSnapshot() : default;
            currentStem = RoWorldMapDataUtility.NormalizeMapId(hud.MapName);
            ownName = session != null ? session.SelectedCharacterName ?? string.Empty : string.Empty;
            // 0x5db8d0: the world holding the current map, else the first; +0xc0 (Monster) is off at each open
            shown = 0;
            for (int i = 0; i < worlds.Count && shown == 0; i++) {
                if (FindMap(worlds[i], currentStem) != null) shown = i;
            }

            monster = false;
            comboOpen = false;
            comboRow = shown;
            ClosePopup();
            RebuildNodes();
            ClearHover();
            laidOutFor = new Vector2Int(-1, -1);
            ResetRoutes();
            window.Visible = true;
            baseLayer.Visible = true;
            routesLayer.Visible = true;
            // the base and the routes right under the window's surface, all three on top
            system.Windows.BringToFront(window);
            system.Renderer.BringToFront(baseLayer);
            system.Renderer.BringToFront(routesLayer);
            system.Renderer.BringToFront(window.Layer);
            system.Windows.PushEsc(window);
            Relayout();
        }

        private void CloseWindow() {
            ClosePopup();
            if (window == null || !window.Visible) return;
            window.Visible = false;
            baseLayer.Visible = false;
            routesLayer.Visible = false;
            system.Windows.RemoveEsc(window);
            comboOpen = false;
            ResetRoutes();
        }

        /// <summary>The base layer and the DIB redraw (hover, world, party, screen changes).</summary>
        private void Invalidate() {
            baseDirty = true;
            window.Invalidate();
        }

        // ================================================================ data (0x5d9a90)

        private void Build(RoWorldMapCatalog catalog, RoAssetDatabase assets) {
            builtFrom = catalog;
            worlds.Clear();
            backgroundWorld = -1;
            RoAssetTables tables = assets.Tables;
            foreach (RoWorldMapDefinition definition in catalog.Worlds) {
                var world = new World { Name = definition.Name, Background = definition.BackgroundAsset };
                int rows = 0;
                foreach (RoWorldMapLocation location in definition.Locations) {
                    if (location.Type != 0 || rows++ >= MaxRows) continue;
                    // 0x5dbf90: a map mapnametable does not name ("Unknown Area") is left out
                    if (tables == null || (!tables.TryGetMapTable(location.MapId + ".rsw", out _) && !tables.TryGetMapTable(location.MapId, out _))) continue;
                    Group group = FindGroup(world, location.Index);
                    var raw = new Edges((int) location.Left, (int) location.Top, (int) (location.Left + location.Width), (int) (location.Top + location.Height));
                    if (group == null) {
                        group = new Group { Id = location.Index, Raw = raw };
                        world.Groups.Add(group);
                    } else {
                        group.Raw = new Edges(Math.Min(group.Raw.L, raw.L), Math.Min(group.Raw.T, raw.T), Math.Max(group.Raw.R, raw.R), Math.Max(group.Raw.B, raw.B));
                    }

                    group.Maps.Add(new MapEntry { Stem = location.MapId, Label = location.DisplayName ?? string.Empty, Level = location.MobLevel ?? string.Empty, Raw = raw, Rect = raw });
                }

                // 0x5d8410: a Dun row gives its group the label, the level range and the dot
                foreach (RoWorldMapLocation location in definition.Locations) {
                    if (location.Type != 1) continue;
                    Group group = FindGroup(world, location.Index);
                    if (group == null) continue;
                    group.Label = location.DisplayName ?? string.Empty;
                    group.Level = location.MobLevel ?? string.Empty;
                    group.Raw = new Edges((int) location.Left, (int) location.Top, (int) (location.Left + location.Width), (int) (location.Top + location.Height));
                }

                foreach (Group group in world.Groups) group.Rect = group.Raw;
                worlds.Add(world);
            }
        }

        private static Group FindGroup(World world, int id) {
            foreach (Group group in world.Groups) {
                if (group.Id == id) return group;
            }

            return null;
        }

        private static MapEntry FindMap(World world, string stem) {
            if (string.IsNullOrEmpty(stem)) return null;
            foreach (Group group in world.Groups) {
                foreach (MapEntry map in group.Maps) {
                    if (string.Equals(map.Stem, stem, StringComparison.OrdinalIgnoreCase)) return map;
                }
            }

            return null;
        }

        /// <summary>0x5dad20 for the shown world at this screen size (the client did it once; redoing it on resize is harmless).</summary>
        private void Transform(World world, Vector2Int client) {
            if (world.TransformedFor == client || world.TextureSize.x <= 0 || world.TextureSize.y <= 0) return;
            world.TransformedFor = client;
            float sx = (float) client.x / world.TextureSize.x, sy = (float) (client.y - TitleH) / world.TextureSize.y;
            Edges Map(Edges r) => new Edges((int) (r.L * sx + 0.5f), (int) (r.T * sy + 0.5f) + TitleH, (int) (r.R * sx + 0.5f), (int) (r.B * sy + 0.5f) + TitleH);
            foreach (Group group in world.Groups) {
                group.Rect = Map(group.Raw);
                foreach (MapEntry map in group.Maps) map.Rect = Map(map.Raw);
            }
        }

        /// <summary>The shown world's picture into the one dynamic image (0x5d8550 caches one per world; one at a time here).</summary>
        private void EnsureBackground(World world) {
            if (backgroundWorld == shown) return;
            backgroundWorld = shown;
            backgroundReady = false;
            Func<string, byte[]> source = RoClassicUiImages.DefaultSourceProvider?.Invoke();
            byte[] data = source?.Invoke("data\\texture\\" + Ui + world.Background);
            if (data == null) {
                backgroundWorld = -1;
                return;
            }

            int w, h;
            Color32[] pixels;
            if (data.Length > 2 && data[0] == 'B' && data[1] == 'M') {
                if (!RoClassicUiImages.TryDecode(data, out w, out h, out pixels)) return;
            } else {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try {
                    if (!texture.LoadImage(data)) return;
                    w = texture.width;
                    h = texture.height;
                    Color32[] bottomUp = texture.GetPixels32();
                    pixels = new Color32[w * h];
                    for (int row = 0; row < h; row++) Array.Copy(bottomUp, (h - 1 - row) * w, pixels, row * w, w);
                } finally {
                    UnityEngine.Object.Destroy(texture);
                }
            }

            world.TextureSize = new Vector2Int(w, h);
            backgroundReady = system.Images.SetDynamic(BackgroundKey, w, h, pixels);
        }

        /// <summary>0x87b8a0: one node per map with party members online on it, or only the current map with the own name.</summary>
        private void RebuildNodes() {
            nodes.Clear();
            RoPartyMemberRuntimeSnapshot[] members = (session != null ? session.PartySnapshot().Members : null) ?? Array.Empty<RoPartyMemberRuntimeSnapshot>();
            var key = new System.Text.StringBuilder(currentStem);
            if (members.Length == 0) {
                AddToNode(currentStem, ownName);
                key.Append('|').Append(ownName);
            } else {
                foreach (RoPartyMemberRuntimeSnapshot member in members) {
                    key.Append('|').Append(member.Name).Append('@').Append(member.MapName).Append(member.State);
                    if (member.State != 0) continue;   // 1: offline
                    AddToNode(RoWorldMapDataUtility.NormalizeMapId(member.MapName), member.Name);
                }
            }

            partyKey = key.ToString();
        }

        private void AddToNode(string stem, string name) {
            if (string.IsNullOrEmpty(stem)) return;
            foreach (Node node in nodes) {
                if (string.Equals(node.Map.Stem, stem, StringComparison.OrdinalIgnoreCase)) {
                    node.Names.Add(name ?? string.Empty);
                    return;
                }
            }

            for (int i = 0; i < worlds.Count; i++) {
                MapEntry map = FindMap(worlds[i], stem);
                if (map == null) continue;
                var created = new Node { World = i, Map = map };
                created.Names.Add(name ?? string.Empty);
                nodes.Add(created);
                return;
            }
        }

        // ================================================================ window

        private void EnsureWindow() {
            if (window != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIRoMapWnd", WindowId = 0x8c, Width = 640, Height = 480 };
            var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = 640 - 18, Y = 0, W = 14, H = 16 };
            close.States["normal"] = Basic + "world_close_off.bmp";
            close.States["hover"] = Basic + "world_close_on.bmp";
            close.States["pressed"] = Basic + "world_close_on.bmp";
            layout.Children.Add(close);
            AddNaviControls(layout);
            window = new RoClassicWindow(system, layout) {
                Visible = false,
                Draggable = false,
                FrameDragArea = _ => false,
            };
            baseLayer = system.Renderer.CreateLayer("world map");
            routesLayer = system.Renderer.CreateLayer("world map routes");
            baseLayer.Visible = routesLayer.Visible = false;
            // in the windows' pass (above the legacy UGUI), or the HUD windows would come between them and the surface
            baseLayer.AboveOverlayUi = routesLayer.AboveOverlayUi = true;
            window.UnderLayers.Add(baseLayer);
            window.UnderLayers.Add(routesLayer);
            window.DrawDynamic += DrawDib;
            window.Clicked += child => {
                if (NaviClicked(child)) return;
                if (child.Id == CloseId) CloseWindow();
            };
            AddNaviEdit();
            window.Released += (w, local) => LButtonUp(local);
            window.RightClicked += (w, local) => ClosePopup();
            window.EscClose = () => {
                CloseWindow();
                return true;
            };
        }

        /// <summary>The screen size, the font index and the transform; then the hover-dependent rectangles.</summary>
        private void Relayout() {
            Vector2Int client = system.ClientSize;
            World world = worlds[shown];
            EnsureBackground(world);
            if (client != laidOutFor) {
                laidOutFor = client;
                window.Layout.Width = client.x;
                window.Layout.Height = client.y;
                window.SetHeight(client.y);
                window.MoveTo(0, 0);
                window.SetChildPosition("close", client.x - 18, 0);
                fontIndex = client.x <= 640 ? 0 : client.x <= 800 ? 1 : client.x <= 1024 ? 2 : 3;
            }

            Transform(world, client);
            LayoutHover(client);
            Invalidate();
        }

        private void ClearHover() {
            hoverGroup = null;
            hoverMap = null;
            hoverNode = null;
            infoShown = false;
            lastMouse = new Vector2Int(int.MinValue, int.MinValue);
        }

        /// <summary>0x893520: the party node and the map under (x, y) in the shown world (strict tests).</summary>
        private bool HitTest(int x, int y) {
            Group group = null;
            MapEntry map = null;
            Node node = null;
            foreach (Node n in nodes) {
                if (n.World == shown && n.Map.Rect.Contains(x, y)) {
                    node = n;
                    break;
                }
            }

            foreach (Group g in worlds[shown].Groups) {
                if (!(g.IsDungeon ? g.Box : g.Rect).Contains(x, y)) continue;
                foreach (MapEntry m in g.Maps) {
                    if (!m.Rect.Contains(x, y)) continue;
                    group = g;
                    map = m;
                    break;
                }

                if (map != null) break;
            }

            bool changed = group != hoverGroup || map != hoverMap || node != hoverNode;
            hoverGroup = group;
            hoverMap = map;
            hoverNode = node;
            return changed;
        }

        /// <summary>0x878590's preview placement and 0x879470's info box for the current hover.</summary>
        private void LayoutHover(Vector2Int client) {
            infoShown = false;
            if (hoverMap == null) {
                previewStem = null;
                previewImage = false;
                return;
            }

            Edges m = hoverMap.Rect;
            int px = m.R + 140 > client.x ? m.L - 140 : m.R + 10;
            int py = m.T + 140 <= client.y ? m.T + 10 : client.y - Preview;
            previewRect = new Edges(px, py, px + Preview, py + Preview);
            if (!string.Equals(previewStem, hoverMap.Stem, StringComparison.OrdinalIgnoreCase)) {
                previewStem = hoverMap.Stem;
                previewImage = LoadPreview(hoverMap.Stem);
            }

            if (hoverNode == null || hoverNode.Names.Count == 0) return;
            RoClassicGdiFont bold = window.FontFor(0, 12, true);
            int width = 0;
            foreach (string name in hoverNode.Names) width = Math.Max(width, bold.Measure(name));
            width += 10;
            infoLineH = bold.LineHeight;
            int height = hoverNode.Names.Count * (infoLineH + 3) + 7;
            bool right = m.R <= previewRect.R;
            int x = right ? previewRect.R + 10 : previewRect.L - 10 - width, y = previewRect.T;
            if (client.x < x + width) x = m.L - width - 10;
            else if (right) y += 17;
            if (client.y < y + height) y = client.y - height + 10;
            infoRect = new RectInt(x, y, width, height);
            infoShown = true;
        }

        /// <summary>map\&lt;name&gt;.bmp sampled bilinearly into the fixed 130 × 130 preview image (one atlas slot for every map).</summary>
        private bool LoadPreview(string stem) {
            Func<string, byte[]> source = RoClassicUiImages.DefaultSourceProvider?.Invoke();
            byte[] data = source?.Invoke("data\\texture\\" + Ui + "map\\" + stem + ".bmp");
            if (data == null || !RoClassicUiImages.TryDecode(data, out int w, out int h, out Color32[] pixels)) return false;
            var scaled = new Color32[Preview * Preview];
            for (int y = 0; y < Preview; y++) {
                float fy = Mathf.Max(0f, (y + 0.5f) * h / Preview - 0.5f);
                int y0 = Math.Min(h - 1, (int) fy), y1 = Math.Min(h - 1, y0 + 1);
                float ty = fy - y0;
                for (int x = 0; x < Preview; x++) {
                    float fx = Mathf.Max(0f, (x + 0.5f) * w / Preview - 0.5f);
                    int x0 = Math.Min(w - 1, (int) fx), x1 = Math.Min(w - 1, x0 + 1);
                    float tx = fx - x0;
                    Color32 top = Color32.Lerp(pixels[y0 * w + x0], pixels[y0 * w + x1], tx);
                    Color32 bottom = Color32.Lerp(pixels[y1 * w + x0], pixels[y1 * w + x1], tx);
                    scaled[y * Preview + x] = Color32.Lerp(top, bottom, ty);
                }
            }

            return system.Images.SetDynamic(PreviewKey, Preview, Preview, scaled);
        }

        // ================================================================ drawing

        /// <summary>0x878e50 up to the info fill: the D3D quads under the DIB, into the base layer.</summary>
        private void DrawBase() {
            baseDirty = false;
            RoClassicUiQuads q = baseLayer.Quads;
            q.Clear();
            Vector2Int client = laidOutFor;
            World world = worlds[shown];
            if (backgroundReady) Stretched(q, 0, TitleH, client.x, client.y - TitleH, BackgroundKey, White);

            // 0x878e50: the navigation's search boxes, route and selection under the faces
            DrawNaviLayers(q, world);

            // 0x87aa90: the party / own faces
            int r = fontIndex * 2 + 8;
            foreach (Node node in nodes) {
                if (node.World != shown) continue;
                Edges e = node.Map.Rect;
                bool here = string.Equals(node.Map.Stem, currentStem, StringComparison.OrdinalIgnoreCase);
                Stretched(q, e.L + e.W / 2 - r, e.T + e.H / 2 - r, r * 2, r * 2, Wv + (here ? "wv_face1.bmp" : "wv_face.bmp"), Face);
            }

            // 0x87a280: the dungeon art
            foreach (Group group in world.Groups) {
                if (!group.IsDungeon) continue;
                Edges box = group.Box, dot = group.Rect;
                bool hovered = hoverMap != null && hoverMap.Rect.SameAs(box);
                Line(q, box.L + box.W / 2f, box.T + box.H / 2f, dot.L + dot.W / 2f, dot.T + dot.H / 2f + 1f, hovered ? Select : DungeonLine);
                Stretched(q, dot.L, dot.T, dot.W, dot.H, Wv + "wv_dunPos.bmp", hovered ? White : DungeonArt);
                Stretched(q, box.L, box.T, box.W, box.H, Wv + "wv_dunSelect.bmp", hovered ? White : DungeonArt);
            }

            if (hoverMap != null) {
                Edges m = hoverMap.Rect;
                Fill(q, m.L, m.T, m.W, m.H, SelectBox);
                if (previewImage) {
                    Fill(q, previewRect.L, previewRect.T, Preview, Preview, PreviewBackdrop);
                    Stretched(q, previewRect.L, previewRect.T, Preview, Preview, PreviewKey, White);
                }
            }

            if (infoShown) Fill(q, infoRect.x, infoRect.y, infoRect.width, infoRect.height, InfoFill);
            baseLayer.MarkDirty();
        }

        /// <summary>A texture stretched over (x, y, w, h) (CTexture quad 0x52cdf0).</summary>
        private void Stretched(RoClassicUiQuads q, int x, int y, int w, int h, string path, Color32 color) {
            if (w > 0 && h > 0 && system.Images.TryGet(path, out RoClassicUiImage image)) q.Image(x, y, w, h, image, color);
        }

        /// <summary>ScreenFill 0x52c510.</summary>
        private void Fill(RoClassicUiQuads q, int x, int y, int w, int h, Color32 color) {
            if (w > 0 && h > 0) q.Image(x, y, w, h, system.White, color);
        }

        /// <summary>A 1 px line (0x52c360 with the white texture).</summary>
        private void Line(RoClassicUiQuads q, float x0, float y0, float x1, float y1, Color32 color) {
            float dx = x1 - x0, dy = y1 - y0, length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f) return;
            var n = new Vector2(-dy / length * 0.5f, dx / length * 0.5f);
            var a = new Vector2(x0, y0);
            var b = new Vector2(x1, y1);
            q.QuadCorners(a + n, b + n, b - n, a - n, system.White.Uv, system.White.Page, color);
        }

        /// <summary>0x88ee30: the DIB over the quads, then the title bar and the controls.</summary>
        private void DrawDib(RoClassicWindow w) {
            DrawNaviEdit(w);
            Vector2Int client = laidOutFor;
            World world = worlds[shown];
            int dungeonSize = DungeonFont[fontIndex];

            // 0x879720: the dungeon labels at the top of their box, centred
            RoClassicGdiFont dungeonFont = w.FontFor(0, dungeonSize, false);
            foreach (Group group in world.Groups) {
                if (!group.IsDungeon || string.IsNullOrEmpty(group.Label)) continue;
                Edges box = group.Box;
                int x = Centre(dungeonFont, group.Label, box);
                w.TextOut(x + 1, box.T + 3, group.Label, 0, dungeonSize, DungeonShadow);
                w.TextOut(x, box.T + 2, group.Label, 0, dungeonSize, DungeonText);
            }

            // 0x87a0f0: the party outlines
            foreach (Node node in nodes) {
                if (node.World != shown) continue;
                Edges e = node.Map.Rect;
                bool here = string.Equals(node.Map.Stem, currentStem, StringComparison.OrdinalIgnoreCase);
                w.RoundOutline(e.L, e.T, e.W, e.H, here ? PartyHere : PartyThere);
            }

            if (monster) DrawMonsterOverlay(w, world, dungeonFont, dungeonSize);

            // 0x879470: the info box (party names on the hovered map)
            if (infoShown && hoverNode != null) {
                for (int i = 0; i < hoverNode.Names.Count; i++) {
                    string name = hoverNode.Names[i];
                    w.TextOutOutline(infoRect.x + 5, infoRect.y + 5 + i * (infoLineH + 3), name, 0, 12, name == ownName ? OwnName : White, Black, true);
                }

                w.RoundOutline(infoRect.x, infoRect.y, infoRect.width, infoRect.height, White);
            }

            // 0x878590: the hover outlines and the preview's texts
            if (hoverMap != null) {
                if (hoverGroup != null && hoverGroup.IsDungeon) w.RoundOutline(hoverGroup.Rect.L, hoverGroup.Rect.T, hoverGroup.Rect.W, hoverGroup.Rect.H, Select);
                Edges m = hoverMap.Rect;
                w.RoundOutline(m.L, m.T, m.W, m.H, Select);
                // the preview's square is punched into the DIB (magenta): the labels under it give way to the picture
                w.PunchRect(previewRect.L, previewRect.T, Preview, Preview);
                w.TextOutOutline(previewRect.L + 3, previewRect.T + 115, hoverMap.Stem, 0, 12, White, StemOutline, true);
                w.TextOutOutline(previewRect.L + 3, previewRect.T + 3, RoClassicUiHost.MapDisplayName(hoverMap.Stem), 0, 12, DisplayName, Black, true);
            }

            // OnDraw step 7: the Kafra sprite's RECT +0x204 punched (magenta), so the sprite shows over this surface
            if (kafraSpriteRect.width > 0) w.PunchRect(kafraSpriteRect.x, kafraSpriteRect.y, kafraSpriteRect.width, kafraSpriteRect.height);

            // 0x82f2d0 style 1 with an empty caption, then the separator over the background's first rows
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < client.x - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(client.x - 12, 0, Basic + "titlebar_right.bmp");
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            w.FillRect(0, 16, client.x, 2, Separator);

            // 0x8a1d10's outlines and names, written by the direct pass over everything of the surface
            DrawRouteLabels(w);

            // the Monster(Tab) check box 0xbc (UICheckBox: the box at (0, 1), the label black at (13, 0))
            w.ClipTo(client.x - 100, 3, 80, 12);   // the control's own 80 x 12 surface
            w.DrawBitmap(client.x - 100, 4, Ui + (monster ? "checkbox_1.bmp" : "checkbox_0.bmp"));
            w.TextOut(client.x - 87, 3, RoClassicUiHost.MsgStr(0x988, "Monster(Tab)"), 0, 12, Black);
            w.ResetClip();
            DrawCombo(w);
        }

        /// <summary>0x8799d0: the dungeons' level ranges; the field tiles' outlines, names and levels (alternately at the bottom / top).</summary>
        private void DrawMonsterOverlay(RoClassicWindow w, World world, RoClassicGdiFont dungeonFont, int dungeonSize) {
            int nameSize = MobNameFont[fontIndex], levelSize = MobLvFont[fontIndex];
            RoClassicGdiFont nameFont = w.FontFor(0, nameSize, false), levelFont = w.FontFor(0, levelSize, true);
            bool bottom = false;
            foreach (Group group in world.Groups) {
                if (group.IsDungeon) {
                    if (string.IsNullOrEmpty(group.Level)) continue;
                    Edges box = group.Box;
                    int x = Centre(dungeonFont, group.Level, box), y = box.T + 2 + dungeonSize;
                    w.TextOut(x + 1, y + 1, group.Level, 0, dungeonSize, DungeonShadow);
                    w.TextOut(x, y, group.Level, 0, dungeonSize, DungeonText);
                    continue;
                }

                foreach (MapEntry map in group.Maps) {
                    if (string.IsNullOrEmpty(map.Label)) continue;
                    Edges e = map.Rect;
                    w.RoundOutline(e.L, e.T, e.W, e.H, MobBoxLine);
                    bottom = !bottom;
                    int nameY = bottom ? e.B - nameSize - 3 : e.T + 3;
                    int levelY = bottom ? nameY - levelSize - 3 : nameY + nameSize + 3;
                    int x = Centre(nameFont, map.Label, e);
                    w.TextOut(x + 1, nameY + 1, map.Label, 0, nameSize, Black);
                    w.TextOut(x - 1, nameY - 1, map.Label, 0, nameSize, Black);
                    w.TextOut(x, nameY, map.Label, 0, nameSize, MobName);
                    if (string.IsNullOrEmpty(map.Level)) continue;
                    w.TextOutOutline(Centre(levelFont, map.Level, e), levelY, map.Level, 0, levelSize, White, Black, true);
                }
            }
        }

        /// <summary>|(int)((textW − R − L) · 0.5)|: the text centred over the box.</summary>
        private static int Centre(RoClassicGdiFont font, string text, Edges box) => Math.Abs((int) ((font.Measure(text) - box.R - box.L) * 0.5f));

        /// <summary>
        /// UIComboBoxDefWnd 0x7eaf80 (#646464 frame, #E8E8E8 inside), txtbox_btn_a/b/c at (W − 18, 0), the label black over a
        /// white shadow at (2, 3); open, the UIComboBoxEx list (0x7eb580) of 0x43 px under it: white, the sysboxs corners, the
        /// row under the mouse on colorchip (6, 2), rows of 16 px at (3, 16·r + 2).
        /// </summary>
        private void DrawCombo(RoClassicWindow w) {
            w.FillRect(ComboX, 0, ComboW, ComboH, ComboFrame);
            w.FillRect(ComboX + 1, 1, ComboW - 2, ComboH - 2, ComboFill);
            Vector2Int mouse = system.Windows.Mouse;
            bool overButton = system.Windows.HoverWindow == window && mouse.x >= ComboX + ComboW - ComboButton && mouse.x < ComboX + ComboW && mouse.y >= 0 && mouse.y < ComboH;
            string button = overButton ? (LeftHeld() ? "txtbox_btn_c.bmp" : "txtbox_btn_b.bmp") : "txtbox_btn_a.bmp";
            w.DrawBitmap(ComboX + ComboW - ComboButton, 0, Basic + button);
            w.ClipTo(ComboX + 2, 3, ComboW - ComboButton, ComboH);
            w.TextOut(ComboX + 3, 4, worlds[shown].Name, 0, 12, White);
            w.TextOut(ComboX + 2, 3, worlds[shown].Name, 0, 12, Black);
            w.ResetClip();
            if (!comboOpen) return;
            int y0 = ComboH;
            w.FillRect(ComboX, y0, ComboW, ComboListH, White);
            w.DrawBitmapRaw(ComboX, y0, Basic + "sysboxs_lu.bmp");
            w.DrawBitmapRaw(ComboX, y0 + ComboListH - 7, Basic + "sysboxs_ld.bmp");
            w.DrawBitmapRaw(ComboX + ComboW - 7, y0, Basic + "sysboxs_ru.bmp");
            w.DrawBitmapRaw(ComboX + ComboW - 7, y0 + ComboListH - 7, Basic + "sysboxs_rd.bmp");
            int rows = Math.Min(worlds.Count, ComboListH / ComboRowH);
            w.ClipTo(ComboX, y0, ComboW, ComboListH);
            for (int i = 0; i < rows; i++) {
                if (i == comboRow) w.FillRect(ComboX + 3, y0 + i * ComboRowH + 1, ComboW - 6, ComboRowH - 2, ListHighlight);
                w.TextOut(ComboX + 3, y0 + i * ComboRowH + 2, worlds[i].Name, 0, 12, Black);
            }

            w.ResetClip();
        }

        // ================================================================ input

        private static bool LeftHeld() {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return false;
#endif
        }

        private bool InCombo(int x, int y) => x >= ComboX && x < ComboX + ComboW && y >= 0 && y < ComboH + (comboOpen ? ComboListH : 0);
        private bool InCheck(int x, int y) => x >= laidOutFor.x - 100 && x < laidOutFor.x - 20 && y >= 3 && y < 15;

        /// <summary>vf31 0x892190 (and the controls' own button-up handlers).</summary>
        private void LButtonUp(Vector2Int p) {
            if (InCombo(p.x, p.y)) {
                if (p.y < ComboH) {
                    comboOpen = !comboOpen;
                } else {
                    int row = (p.y - ComboH) / ComboRowH;
                    if (row >= 0 && row < worlds.Count) {
                        comboOpen = false;
                        comboRow = row;
                        ShowWorld(row);
                        return;
                    }
                }

                window.Invalidate();
                return;
            }

            if (InCheck(p.x, p.y)) {
                ToggleMonster();
                return;
            }

            ClosePopup();
            // 0x5d8440: the first dungeon whose label box holds the point lists its floors
            foreach (Group group in worlds[shown].Groups) {
                if (!group.IsDungeon || !group.Box.Contains(p.x, p.y) || group.Maps.Count == 0) continue;
                OpenPopup(group, p);
                return;
            }

            if (hoverMap != null) Navigate(hoverMap.Stem);
        }

        private void ShowWorld(int index) {
            if (index < 0 || index >= worlds.Count) return;
            shown = index;
            ClosePopup();
            ClearHover();
            Relayout();
        }

        private void ToggleMonster() {
            monster = !monster;
            window.Invalidate();
        }

        private static void Navigate(string stem) => NavigateRequested?.Invoke(stem);

        /// <summary>MakeWindow(0x1a): the floors' labels, as wide as the widest at height 14, 16 px rows up to 160, at the click.</summary>
        private void OpenPopup(Group group, Vector2Int p) {
            if (popup == null) {
                popup = new RoClassicWindow(system, new RoClassicWindowLayout { Class = "UIComboBoxWnd", WindowId = 0x1a, Width = 100, Height = PopupMaxH }) {
                    Visible = false,
                    Draggable = false,
                    FrameDragArea = _ => false,
                    ClickableAt = _ => true,
                };
                popup.DrawDynamic += DrawPopup;
                popup.Pressed += (w, local) => {
                    int k = popupFirst + local.y / PopupRowH;
                    if (popupGroup != null && k >= 0 && k < popupGroup.Maps.Count) PickFloor(k);
                };
                popup.Wheeled += (w, local, delta) => {
                    if (popupGroup == null) return;
                    popupFirst = Mathf.Clamp(popupFirst + (delta > 0 ? -1 : 1), 0, Math.Max(0, popupGroup.Maps.Count - PopupMaxH / PopupRowH));
                    popup.Invalidate();
                };
                popup.EscClose = () => {
                    ClosePopup();
                    return true;
                };
            }

            popupGroup = group;
            popupFirst = 0;
            popupHover = -1;
            RoClassicGdiFont measure = window.FontFor(0, 14, false);
            popupWidth = 0;
            foreach (MapEntry map in group.Maps) popupWidth = Math.Max(popupWidth, measure.Measure(map.Label));
            int height = Math.Min(PopupRowH * group.Maps.Count, PopupMaxH);
            Vector2Int client = laidOutFor;
            int x = client.x - (p.x + 2) - popupWidth >= 0 ? p.x + 2 : client.x - popupWidth;
            int y = client.y - (p.y + 1) - height >= 0 ? p.y + 1 : client.y - height;
            popup.Layout.Width = popupWidth;
            popup.Layout.Height = height;
            popup.SetHeight(height);
            popup.MoveTo(x, y);
            popup.Visible = true;
            system.Windows.BringToFront(popup);
            system.Windows.PushEsc(popup);
            popup.Invalidate();
        }

        private void DrawPopup(RoClassicWindow w) {
            if (popupGroup == null) return;
            int width = w.Width, height = w.Height, count = popupGroup.Maps.Count, rows = Math.Max(1, PopupMaxH / PopupRowH);
            w.FillRect(0, 0, width, height, White);
            w.DrawBitmapRaw(0, 0, Basic + "sysboxs_lu.bmp");
            w.DrawBitmapRaw(0, height - 7, Basic + "sysboxs_ld.bmp");
            w.DrawBitmapRaw(width - 7, 0, Basic + "sysboxs_ru.bmp");
            w.DrawBitmapRaw(width - 7, height - 7, Basic + "sysboxs_rd.bmp");
            for (int r = 0; r < rows && popupFirst + r < count; r++) {
                int k = popupFirst + r;
                bool on = k == popupHover;
                if (on) w.FillRect(2, PopupRowH * r + 1, width - 4, PopupRowH - 2, ListHighlight);
                w.TextOut(4, PopupRowH * r + 2, popupGroup.Maps[k].Label, 0, 12, on ? White : Black);
            }

            w.DrawScrollBar(width - 13, 0, height, Math.Max(0, count - rows), count, rows, popupFirst);
        }

        /// <summary>Msg 0x27 from the list: the floor's map, without ".rsw", to the navigation.</summary>
        private void PickFloor(int index) {
            string stem = popupGroup.Maps[index].Stem;
            ClosePopup();
            Navigate(stem);
            window?.Invalidate();
        }

        private void ClosePopup() {
            popupGroup = null;
            if (popup == null || !popup.Visible) return;
            popup.Visible = false;
            system.Windows.RemoveEsc(popup);
        }

        /// <summary>Per frame while shown: the screen size, the hover, the combo and floor rows, the tooltips, Tab, the party.</summary>
        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (window == null || !window.Visible) return;

            if (ui.ClientSize != laidOutFor) Relayout();
            TickNavi();
            Vector2Int mouse = ui.Windows.Mouse;
            bool over = ui.Windows.HoverWindow == window;
            if (over && mouse != lastMouse && !hoverPinned) {
                lastMouse = mouse;
                int row = comboOpen && mouse.x >= ComboX && mouse.x < ComboX + ComboW && mouse.y >= ComboH && mouse.y < ComboH + ComboListH
                    ? (mouse.y - ComboH) / ComboRowH
                    : comboOpen ? -1 : comboRow;
                if (row >= worlds.Count) row = -1;
                if (row != comboRow && comboOpen) {
                    comboRow = row;
                    window.Invalidate();
                }

                // the controls take their own mouse moves: the map's hover stays as it was
                if (!InCombo(mouse.x, mouse.y) && !InCheck(mouse.x, mouse.y) && HitTest(mouse.x, mouse.y)) {
                    LayoutHover(laidOutFor);
                    Invalidate();
                }

                if (mouse.x >= ComboX + ComboW - ComboButton && mouse.x < ComboX + ComboW && mouse.y < ComboH) window.Invalidate();
            }

            // the close button's balloon (its hotkey) comes from the window manager (UIButton::vf28)
            if (over && InCheck(mouse.x, mouse.y)) {
                RoClassicTooltip.Shared.Show("Tab", White, mouse.x, mouse.y - 20);
            }

            if (popup != null && popup.Visible && popupGroup != null) {
                int hover = ui.Windows.HoverWindow == popup ? popupFirst + (mouse.y - popup.Position.y) / PopupRowH : -1;
                if (hover >= popupGroup.Maps.Count) hover = -1;
                if (hover != popupHover) {
                    popupHover = hover;
                    popup.Invalidate();
                }
            }

            // msg 0x3c: the party list changed (checked twice a second)
            if (Time.unscaledTime >= nextPartyPoll) {
                nextPartyPoll = Time.unscaledTime + 0.5f;
                string before = partyKey;
                RebuildNodes();
                if (partyKey != before) {
                    HitTest(lastMouse.x, lastMouse.y);
                    LayoutHover(laidOutFor);
                    Invalidate();
                }
            }

            TickRoutes(Time.unscaledDeltaTime);
            if (baseDirty) DrawBase();
            // the surface follows the layers in the same frame (the manager refreshed the windows before this tick)
            window.Refresh();

#if ENABLE_INPUT_SYSTEM
            // Tab while the map is open: SendMsg(6, 0xbc) to the Monster check box (0x9d3830)
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && !keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed &&
                !RoClassicTextInput.HasFocus && !RoClassicMessageBox.ModalOpen) {
                ToggleMonster();
            }
#endif
        }

        /// <summary>The sweep console: open | close | world n | monster | hover x y (held) | hover off | click x y | rclick | pick n | groups | state.</summary>
        public static string Sweep(RoLoginShellSession session, string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            RoClassicWorldMap map = instance;
            switch (a[0]) {
                case "open":
                    if (map == null || map.window == null || !map.window.Visible) Toggle(session);
                    break;
                case "close":
                    Close();
                    break;
            }

            map = instance;
            if (map == null || map.window == null) return "no world map";
            int Arg(int i) => a.Length > i && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : -1;
            switch (a[0]) {
                case "world":
                    map.ShowWorld(Arg(1));
                    break;
                case "monster":
                    map.ToggleMonster();
                    break;
                case "hover":
                    map.hoverPinned = a.Length < 2 || a[1] != "off";
                    if (!map.hoverPinned) break;
                    map.lastMouse = new Vector2Int(Arg(1), Arg(2));
                    map.HitTest(Arg(1), Arg(2));
                    map.LayoutHover(map.laidOutFor);
                    map.Invalidate();
                    break;
                case "click":
                    map.LButtonUp(new Vector2Int(Arg(1), Arg(2)));
                    break;
                case "rclick":
                    map.ClosePopup();
                    break;
                case "pick":
                    if (map.popupGroup != null && Arg(1) >= 0 && Arg(1) < map.popupGroup.Maps.Count) map.PickFloor(Arg(1));
                    break;
            }

            if (map.worlds.Count == 0) return "visible=" + map.window.Visible + " no worlds";
            World world = map.worlds[map.shown];
            int dungeons = 0, fields = 0, maps = 0;
            foreach (Group group in world.Groups) {
                if (group.IsDungeon) dungeons++;
                else fields++;
                maps += group.Maps.Count;
            }

            var s = new System.Text.StringBuilder();
            s.Append("visible=").Append(map.window.Visible).Append(" world=").Append(map.shown).Append(' ').Append(world.Name)
                .Append(" bg=").Append(map.backgroundReady).Append(' ').Append(world.TextureSize.x).Append('x').Append(world.TextureSize.y)
                .Append(" screen=").Append(map.laidOutFor.x).Append('x').Append(map.laidOutFor.y).Append(" font=").Append(map.fontIndex)
                .Append(" groups=").Append(dungeons).Append('+').Append(fields).Append(" maps=").Append(maps)
                .Append(" monster=").Append(map.monster).Append(" combo=").Append(map.comboOpen).Append(" current=").Append(map.currentStem);
            s.Append(" nodes=");
            foreach (Node node in map.nodes) s.Append(node.Map.Stem).Append('@').Append(node.World).Append('[').Append(string.Join(",", node.Names)).Append("] ");
            if (map.hoverMap != null) {
                Edges m = map.hoverMap.Rect;
                s.Append(" hover=").Append(map.hoverMap.Stem).Append(" group=").Append(map.hoverGroup?.Id).Append(" rect=").Append(m.L).Append(',').Append(m.T).Append(',').Append(m.R).Append(',').Append(m.B)
                    .Append(" preview=").Append(map.previewRect.L).Append(',').Append(map.previewRect.T).Append(" image=").Append(map.previewImage);
            }

            if (map.popupGroup != null) {
                s.Append(" popup=").Append(map.popupGroup.Label).Append(" at ").Append(map.popup.Position.x).Append(',').Append(map.popup.Position.y).Append(" w=").Append(map.popupWidth).Append(" floors=");
                foreach (MapEntry floor in map.popupGroup.Maps) s.Append(floor.Stem).Append(':').Append(floor.Label).Append("; ");
            }

            map.DescribeRoutes(s);
            if (a[0] == "groups") {
                foreach (Group group in world.Groups) {
                    Edges b = group.Box, r = group.Rect;
                    s.Append("\n  ").Append(group.Id).Append(' ').Append(group.Label).Append(" lv=").Append(group.Level).Append(" box=").Append(b.L).Append(',').Append(b.T).Append(',').Append(b.R).Append(',').Append(b.B)
                        .Append(" rect=").Append(r.L).Append(',').Append(r.T).Append(',').Append(r.R).Append(',').Append(r.B).Append(" maps=").Append(group.Maps.Count);
                }
            }

            return s.ToString();
        }
    }
}
