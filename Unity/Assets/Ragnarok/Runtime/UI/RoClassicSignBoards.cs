using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Maps;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The map signboards (hud-efst-banner-spec §3): SignBoardList rows 2..n of the current map (row 1 is the defaults:
    /// path, contents, colour), each a window at the bottom of the window list over the cell's ground raised by
    /// height − 3 world units (0x782e60).
    /// <list type="bullet">
    /// <item>IT_BMP (1, also any unknown type) — CBmpObjWnd: the bitmap at its size (0x784c50);</item>
    /// <item>IT_SIGNBOARD (3) — CSignBoardWnd 162×36: signboard\bg_signboard.bmp, the icon at (5, 5), the contents in Arial
    /// 12 at (30, 11) in the row's colour ("#0x00RRGGBB"), left-aligned, no shadow (0x784b50, 0x784c90);</item>
    /// <item>IT_SPRITE (2) — a world sprite, none in Custom's list (not drawn).</item>
    /// </list>
    /// A path without "유저인터페이스" gets that folder first. Placed every frame at (sx − w / 2, sy − h / 2 − K·scale), K·scale
    /// the 81 sprite pixels of the chat balloons, not clamped (0x784ed0 / 0x784fc0); under every window.
    /// </summary>
    public sealed class RoClassicSignBoards : IRoClassicUiProducer {
        private const string Interface = "유저인터페이스";
        private const string Background = Interface + "\\signboard\\bg_signboard.bmp";
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private sealed class Board {
            public Vector2Int Cell;
            public float Height;
            public RoSignBoardItemType Type;
            public string Path;
            public string Text;
            public Color32 Color;
            public int Anchor = -1;
            public int Width, Height2D;
        }

        private static RoClassicSignBoards instance;
        private static int boundFrame = -10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            boundFrame = -10;
        }

        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private readonly List<Board> boards = new List<Board>(16);
        private readonly Stack<int> freeAnchors = new Stack<int>();
        private RoOnlineWorldController world;
        private int worldLookupFrame = -100;
        private string shownMap;
        private bool built;
        private int retryFrame;

        /// <summary>Per frame from the chat window's classic ticks while in the game.</summary>
        public static void Tick() {
            boundFrame = Time.frameCount;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || (instance != null && instance.system == system)) return;
            instance = new RoClassicSignBoards { system = system };
            instance.layer = system.Renderer.CreateLayer("sign boards");
            instance.layer.AboveOverlayUi = true;
            system.Renderer.SendToBack(instance.layer);
            system.Register(instance);
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            RoOnlineWorldController w = World();
            RoGridSelector grid = w != null ? w.GridSelector : null;
            string map = w != null && w.RuntimeMapController != null ? w.RuntimeMapController.MapName : null;
            // the signboards are windows of the list in the client: F11 hides them with the rest (RoClassicCloseTabs)
            if (Time.frameCount - boundFrame > 2 || grid == null || grid.Altitude == null || string.IsNullOrEmpty(map) || RoClassicCloseTabs.OverheadHidden) {
                if (layer.Visible) layer.Visible = false;
                return;
            }

            if (!layer.Visible) layer.Visible = true;
            if (!string.Equals(map, shownMap, System.StringComparison.OrdinalIgnoreCase)) {
                shownMap = map;
                Load(map);
            }

            if (!built || (retryFrame != 0 && Time.frameCount >= retryFrame)) Build(ui);
            Place(ui, grid);
        }

        private RoOnlineWorldController World() {
            if (world == null && Time.frameCount - worldLookupFrame > 30) {
                worldLookupFrame = Time.frameCount;
                world = Object.FindFirstObjectByType<RoOnlineWorldController>();
            }

            return world;
        }

        /// <summary>0x783bd0 / 0x782e60: the rows of this map with the defaults row's fallbacks.</summary>
        private void Load(string map) {
            foreach (Board b in boards) {
                if (b.Anchor >= 0) {
                    system.Renderer.SetAnchor(b.Anchor, 0, 0, false);
                    freeAnchors.Push(b.Anchor);
                }
            }

            boards.Clear();
            built = false;
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null) {
                shownMap = null;
                return;
            }

            RoSignBoardMetadataTable table = RoSignBoardMetadataTable.Load(assets);
            RoSignBoardEntry defaults = table.Entries.Count > 0 && string.Equals(table.Entries[0].MapId, "Default", System.StringComparison.OrdinalIgnoreCase)
                ? table.Entries[0]
                : null;
            foreach (RoSignBoardEntry e in table.GetEntriesForMap(map)) {
                if (e == null || ReferenceEquals(e, defaults)) continue;
                RoSignBoardItemType type = e.ItemType == RoSignBoardItemType.Bitmap || e.ItemType == RoSignBoardItemType.Sprite ||
                                           e.ItemType == RoSignBoardItemType.SignBoard
                    ? e.ItemType
                    : RoSignBoardItemType.Bitmap;
                if (type == RoSignBoardItemType.Sprite) continue;
                string path = string.IsNullOrWhiteSpace(e.AssetPath) ? defaults?.AssetPath : e.AssetPath;
                string text = e.RawDisplayText;
                if (type == RoSignBoardItemType.SignBoard && string.IsNullOrEmpty(text)) text = defaults?.RawDisplayText;
                boards.Add(new Board {
                    Cell = new Vector2Int(e.CellX, e.CellY),
                    Height = e.Height,
                    Type = type,
                    Path = InterfacePath(path),
                    Text = text ?? string.Empty,
                    Color = e.TextColor,
                    Anchor = freeAnchors.Count > 0 ? freeAnchors.Pop() : system.Renderer.AllocateAnchor(),
                });
            }
        }

        private static string InterfacePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string p = path.Trim().Replace('/', '\\');
            return p.Contains(Interface) ? p : Interface + "\\" + p;
        }

        /// <summary>Each board's quads on its own anchor (its top-left corner); retried while a bitmap is not loaded yet.</summary>
        private void Build(RoClassicUiSystem ui) {
            built = true;
            retryFrame = 0;
            RoClassicUiImages images = system.Images;
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            RoClassicGdiFont font = ui.Font(12);
            foreach (Board b in boards) {
                q.Anchor = b.Anchor;
                b.Width = b.Height2D = 0;
                if (b.Type == RoSignBoardItemType.SignBoard) {
                    if (!images.TryGet(Background, out RoClassicUiImage bg)) {
                        if (images.Source == null) retryFrame = Time.frameCount + 30;
                        continue;
                    }

                    b.Width = bg.Width;
                    b.Height2D = bg.Height;
                    q.Image(0, 0, bg, White);
                    if (images.TryGet(b.Path, out RoClassicUiImage icon)) q.Image(5, 5, icon, White);
                    if (b.Text.Length > 0) q.Text(font, 30, b.Height2D / 2 - 7, b.Text, b.Color);
                } else if (images.TryGet(b.Path, out RoClassicUiImage bmp)) {
                    b.Width = bmp.Width;
                    b.Height2D = bmp.Height;
                    q.Image(0, 0, bmp, White);
                } else if (images.Source == null) {
                    retryFrame = Time.frameCount + 30;
                }
            }

            q.Anchor = 0;
            layer.MarkDirty();
        }

        /// <summary>C3DCooRendering vf01 / vf03: centred on the projected point, lifted by K·scale, not clamped.</summary>
        private void Place(RoClassicUiSystem ui, RoGridSelector grid) {
            UnityEngine.Camera camera = ui.Camera;
            if (camera == null) return;
            float s = ui.Scale;
            int screenH = camera.pixelHeight;
            Vector3 up = camera.transform.up * (LiftSpritePixels((int) (screenH / s)) / ROIO.Models.FileTypes.SPR.PIXELS_PER_UNIT);
            int gw = (int) grid.Altitude.width, gh = (int) grid.Altitude.height;
            foreach (Board b in boards) {
                if (b.Width == 0 || b.Cell.x < 0 || b.Cell.y < 0 || b.Cell.x >= gw || b.Cell.y >= gh) {
                    system.Renderer.SetAnchor(b.Anchor, 0, 0, false);
                    continue;
                }

                // the GAT heights are fifths of a world unit: (height − 3) world units up
                Vector3 point = grid.GetCellWorldCenter(b.Cell, (b.Height - 3f) * 0.2f);
                Vector3 screen = camera.WorldToScreenPoint(point);
                if (screen.z <= 0f) {
                    system.Renderer.SetAnchor(b.Anchor, 0, 0, false);
                    continue;
                }

                int sx = (int) (screen.x / s), sy = (int) ((screenH - screen.y) / s);
                int lift = Mathf.Max(0, (int) ((camera.WorldToScreenPoint(point + up).y - screen.y) / s));
                int x = sx - b.Width / 2, y = sy - b.Height2D / 2 - lift;
                system.Renderer.SetAnchor(b.Anchor, Mathf.RoundToInt(x * s), Mathf.RoundToInt(y * s), true);
            }
        }

        /// <summary>
        /// K·scale in actor-sprite pixels (signboard-lift notes): floor(81·H / 480) · 768 / (1.34·H), [0x1181958] being
        /// H·1.34 / 768 (SetSize 0x53d770) — about 96.7 sprite pixels at any height; projected at the anchor it is the lift
        /// of the boards, the chat balloons, the cast bars and the shop boards.
        /// </summary>
        internal static float LiftSpritePixels(int clientHeight) {
            int h = Mathf.Max(1, clientHeight);
            return 81 * h / 480 * 768f / (1.34f * h);
        }

        /// <summary>!sign: the boards of the map and where they are.</summary>
        internal static string Describe() {
            RoClassicSignBoards s = instance;
            if (s == null) return "no sign boards";
            var sb = new System.Text.StringBuilder();
            sb.Append(s.shownMap).Append(": ").Append(s.boards.Count);
            foreach (Board b in s.boards) sb.Append(" | ").Append(b.Type).Append(' ').Append(b.Cell).Append(' ').Append(b.Path).Append(' ').Append(b.Width).Append('x').Append(b.Height2D).Append(" '").Append(b.Text).Append('\'');
            return sb.ToString();
        }
    }
}
