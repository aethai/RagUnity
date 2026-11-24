using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The world map's hover routes (worldmap-routes-spec: 0x8a1840 / 0x893880 and 0x87c8e0 / 0x88c050), drawn in the
    /// routes layer between the base layer and the DIB, every frame while one shows.
    /// <para>Kafra moves: a hovered map that is a key of KafraMoveMapServiceList (datainfo\kaframovemapservicelist.lub,
    /// read once) gets an arrow to each destination found in Midgard: maparrow_body.bmp 10 px wide on the right of the
    /// centre line, pushed out of both tiles, growing over 1 s (phase += dt, then held at 1) with the 20 px
    /// maparrow_head.bmp while it grows, all at 0xAAFFFFFF; a destination Izlude starts at the source's right edge and
    /// ends at (L + 0.8 w, T). Then 4_f_kafra6 (action 0, layer 0 only, 4 s loop) at the tile's top-right corner, scaled
    /// by font index · 0.2 + 0.5 and kept below the title bar, its rectangle punched into the DIB.</para>
    /// <para>Airships: two CFlyingShipRouteInfo (type 1 drawn first, then type 0) whose hardcoded loops start at the
    /// hovered port (izlude / ra_fild12 / yuno; einbroch / lighthalzen / hugel / yuno): every segment as a 4 px
    /// wv_Airf3.bmp line at 0xAAFFFFFF, the current one travelled at 200 px/s with wv_Airf.bmp dashes (16 px, 8 px gaps,
    /// the last ≈ 100 px, alpha 0x33 … 0xFF) and the 40 px wv_airplen32.bmp ship in 24 px steps facing its way.</para>
    /// <para>Labels (0x8a1d10): every destination / other port once, in name order, the hovered tile skipped: a
    /// 0x66000000 fill under the routes' layer content and, on top of the DIB, a white rounded outline and the
    /// mapnametable name at (L + 2, T + 5) in bold #FFD200 with a black outline.</para>
    /// </summary>
    public sealed partial class RoClassicWorldMap {
        private const string KafraListPath = "data/luafiles514/lua files/datainfo/kaframovemapservicelist.lub";
        private const string KafraSpritePath = "data/sprite/npc/4_f_kafra6";
        private const string ArrowBody = "maparrow_body.bmp", ArrowHead = "maparrow_head.bmp";   // data\texture\, no 유저인터페이스
        private const float ArrowWidth = 10f, AirSpeed = 200f;
        private static readonly Color32 RouteColor = new Color32(255, 255, 255, 0xaa);
        private static readonly Color32 LabelFill = new Color32(0, 0, 0, 0x66);
        private static readonly Vector2 Uv00 = new Vector2(0f, 0f), Uv10 = new Vector2(1f, 0f), Uv01 = new Vector2(0f, 1f), Uv11 = new Vector2(1f, 1f);
        private static readonly Comparison<RoutePoint> ByStem = (a, b) => string.CompareOrdinal(a.Stem, b.Stem);

        // 0x87df10's ports and 0x87c8e0's loops, by route type (0: +0x184, 1: +0x134)
        private static readonly string[][] Ports = {
            new[] { "izlude", "ra_fild12", "yuno" },
            new[] { "einbroch", "lighthalzen", "hugel", "yuno" },
        };

        private static readonly Dictionary<string, string[]>[] Loops = {
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) {
                ["izlude"] = new[] { "izlude", "yuno", "ra_fild12", "izlude" },
                ["ra_fild12"] = new[] { "ra_fild12", "izlude", "yuno", "ra_fild12" },
                ["yuno"] = new[] { "yuno", "ra_fild12", "izlude", "yuno" },
            },
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) {
                ["yuno"] = new[] { "yuno", "einbroch", "lighthalzen", "einbroch", "yuno", "hugel", "yuno" },
                ["einbroch"] = new[] { "einbroch", "lighthalzen", "einbroch", "yuno", "hugel", "yuno", "einbroch" },
                ["lighthalzen"] = new[] { "lighthalzen", "einbroch", "yuno", "hugel", "yuno", "einbroch", "lighthalzen" },
                ["hugel"] = new[] { "hugel", "yuno", "einbroch", "lighthalzen", "einbroch", "yuno", "hugel" },
            },
        };

        /// <summary>The 0x3c-byte map point (0x87b7a0): the stem, the Midgard tile and its centre.</summary>
        private struct RoutePoint {
            public string Stem;
            public Edges Rect;
            public Vector2 Centre;
        }

        private sealed class AirRoute {
            public int Type;
            public bool Active, Built, Drawn;
            public int Index;
            public float Phase, Length, Distance;
            public readonly List<RoutePoint> Points = new List<RoutePoint>();
            public readonly List<RoutePoint> Labels = new List<RoutePoint>();
        }

        // g_kafra: source stem → destination stems in file order (the client re-reads it on every open and never clears
        // it, so each arrow is drawn once more per open; the first open is kept)
        private static Dictionary<string, string[]> kafraTable;
        private static RoAssetDatabase kafraTableAssets;

        private string kafraSource = string.Empty;
        private RoutePoint kafraFrom;
        private readonly List<RoutePoint> kafraTo = new List<RoutePoint>();
        private readonly List<RoutePoint> kafraLabels = new List<RoutePoint>();
        private float kafraPhase;
        private RectInt kafraSpriteRect;

        private readonly AirRoute[] airRoutes = { new AirRoute { Type = 1 }, new AirRoute { Type = 0 } };
        private bool routesDrawn, labelsChanged;

        // 4_f_kafra6: the SPR image of layer 0 of every frame of action 0, and the images as dynamic atlas entries
        private bool kafraSpriteTried;
        private int[] kafraFrameImage;
        private Vector2Int[] kafraImageSize;
        private string[] kafraImageKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRouteStatics() {
            kafraTable = null;
            kafraTableAssets = null;
        }

        private void ResetRoutes() {
            kafraSource = string.Empty;
            kafraTo.Clear();
            kafraLabels.Clear();
            kafraPhase = 0f;
            kafraSpriteRect = default;
            foreach (AirRoute route in airRoutes) ResetAirRoute(route);
            routesDrawn = false;
            if (routesLayer != null) {
                routesLayer.Quads.Clear();
                routesLayer.MarkDirty();
            }
        }

        private static void ResetAirRoute(AirRoute route) {
            route.Active = route.Built = route.Drawn = false;
            route.Index = 0;
            route.Distance = route.Length = route.Phase = 0f;
            route.Points.Clear();
            route.Labels.Clear();
        }

        /// <summary>Per frame while the map shows: the Kafra state (0x893880), the airship updates, the routes layer.</summary>
        private void TickRoutes(float dt) {
            if (routesLayer == null || worlds.Count == 0) return;
            // the routes look their maps up in World_List[0]; their names are Midgard's only
            string hovered = shown == 0 && hoverMap != null && worlds[0].TransformedFor == laidOutFor ? hoverMap.Stem : null;
            UpdateKafra(hovered, dt);
            bool any = kafraTo.Count > 0;
            foreach (AirRoute route in airRoutes) {
                route.Drawn = UpdateAirRoute(route, hovered, dt);
                any |= route.Drawn;
            }

            if (labelsChanged) {
                labelsChanged = false;
                window.Invalidate();
            }

            if (any) {
                DrawRoutes();
                routesDrawn = true;
            } else if (routesDrawn) {
                routesDrawn = false;
                routesLayer.Quads.Clear();
                routesLayer.MarkDirty();
                SetKafraSpriteRect(default);
            }
        }

        private void UpdateKafra(string hovered, float dt) {
            Dictionary<string, string[]> table = KafraTable();
            if (hovered == null || !table.TryGetValue(hovered, out string[] destinations)) {
                if (kafraSource.Length > 0 || kafraTo.Count > 0) {
                    kafraSource = string.Empty;
                    kafraTo.Clear();
                    kafraLabels.Clear();
                    labelsChanged = true;
                }

                return;
            }

            if (!string.Equals(kafraSource, hovered, StringComparison.OrdinalIgnoreCase)) {
                kafraSource = hovered;
                kafraTo.Clear();
                foreach (string destination in destinations) {
                    if (TryRoutePoint(destination, out RoutePoint point)) kafraTo.Add(point);
                }

                kafraFrom = MakePoint(hovered, hoverMap.Rect);
                kafraPhase = 0f;
                BuildLabels(kafraTo, 0, kafraLabels);
                labelsChanged = true;
            }

            kafraPhase = Mathf.Min(kafraPhase + dt, 1f);
        }

        /// <summary>0x87c8e0 and the update inlined in 0x878e50: true when the route draws this frame.</summary>
        private bool UpdateAirRoute(AirRoute route, string hovered, float dt) {
            if (hovered == null || Array.IndexOf(Ports[route.Type], hovered) < 0) {
                if (route.Active || route.Built) {
                    ResetAirRoute(route);
                    labelsChanged = true;
                }

                return false;
            }

            if (!route.Built) {
                route.Points.Clear();
                if (Loops[route.Type].TryGetValue(hovered, out string[] loop)) {
                    foreach (string stem in loop) {
                        if (TryRoutePoint(stem, out RoutePoint point)) route.Points.Add(point);
                    }
                }

                route.Built = true;
                route.Index = 0;
                route.Distance = route.Length = 0f;
                BuildLabels(route.Points, 1, route.Labels);
                labelsChanged = true;
            }

            int n = route.Points.Count;
            if (n - 1 > 0) {
                route.Active = true;
                // strict: the segment ends only once the distance is past its length (no overshoot carried)
                if (route.Distance > route.Length) {
                    if (route.Index >= n - 2) route.Index = -1;
                    route.Index++;
                    route.Distance = 0f;
                }

                if (route.Distance == 0f) route.Length = Vector2.Distance(route.Points[route.Index].Centre, route.Points[route.Index + 1].Centre);
                route.Distance += dt * AirSpeed;
                route.Phase = route.Length > 0f ? route.Distance / route.Length : 1f;
            }

            return true;
        }

        /// <summary>0x8a1d10's map of names: each name once, in name order, from <paramref name="first"/> on; the hovered tile is skipped when drawn.</summary>
        private static void BuildLabels(List<RoutePoint> points, int first, List<RoutePoint> into) {
            into.Clear();
            for (int i = first; i < points.Count; i++) {
                bool seen = false;
                foreach (RoutePoint label in into) {
                    if (string.Equals(label.Stem, points[i].Stem, StringComparison.OrdinalIgnoreCase)) {
                        seen = true;
                        break;
                    }
                }

                if (!seen) into.Add(points[i]);
            }

            into.Sort(ByStem);
        }

        private bool TryRoutePoint(string stem, out RoutePoint point) {
            MapEntry map = FindMap(worlds[0], stem);
            if (map == null) {
                point = default;
                return false;
            }

            point = MakePoint(map.Stem, map.Rect);
            return true;
        }

        private static RoutePoint MakePoint(string stem, Edges rect) => new RoutePoint {
            Stem = stem,
            Rect = rect,
            Centre = new Vector2(rect.L + rect.W * 0.5f, rect.T + rect.H * 0.5f),
        };

        private bool IsHoveredTile(Edges rect) => hoverMap != null && hoverMap.Rect.SameAs(rect);

        // ================================================================ drawing (routes layer)

        private void DrawRoutes() {
            RoClassicUiQuads q = routesLayer.Quads;
            q.Clear();
            if (kafraTo.Count > 0) {
                // 0x8a1840: the arrows in the list's order, the Kafra, then the label fills
                if (system.Images.TryGet(ArrowBody, out RoClassicUiImage body) && system.Images.TryGet(ArrowHead, out RoClassicUiImage head)) {
                    foreach (RoutePoint destination in kafraTo) DrawArrow(q, kafraFrom, destination, kafraPhase, body, head);
                }

                DrawKafra(q);
                DrawLabelFills(q, kafraLabels);
            } else {
                SetKafraSpriteRect(default);
            }

            foreach (AirRoute route in airRoutes) {
                if (!route.Drawn) continue;
                DrawAirRoute(q, route);
                DrawLabelFills(q, route.Labels);
            }

            routesLayer.MarkDirty();
        }

        /// <summary>0x877590: the arrow from <paramref name="s"/> to <paramref name="d"/> at <paramref name="phase"/>.</summary>
        private static void DrawArrow(RoClassicUiQuads q, RoutePoint s, RoutePoint d, float phase, in RoClassicUiImage body, in RoClassicUiImage head) {
            Vector2 p1 = s.Centre, p2 = d.Centre;
            if (string.Equals(d.Stem, "izlude", StringComparison.OrdinalIgnoreCase)) {
                p1.x = s.Rect.R;
                p2 = new Vector2(d.Rect.L + 0.8f * d.Rect.W, d.Rect.T);
            }

            Vector2 delta = p2 - p1;
            float length = delta.magnitude;
            if (length < 0.001f) return;
            Vector2 dir = delta / length;
            var normal = new Vector2(-dir.y, dir.x);   // the right-hand side of the way on a y-down screen
            Vector2 wn = normal * ArrowWidth;
            Vector2 e1s = p1, e2s = p1 + wn, e1e = p2, e2e = p2 + wn;
            PushOut(ref e1s, s.Rect, dir);
            PushOut(ref e1e, d.Rect, -dir);
            PushOut(ref e2s, s.Rect, dir);
            PushOut(ref e2e, d.Rect, -dir);
            Vector2 tip = e1s + (e1e - e1s) * phase;
            Vector2 tip2 = phase >= 1f ? e2e : tip + wn;
            q.Strip(e1s, Uv00, e2s, Uv10, tip, Uv01, tip2, Uv11, body, RouteColor);
            if (phase >= 1f) return;
            // the head while it grows: base in the middle of the body, 10 px behind the tip, apex 20 px ahead of the base
            Vector2 b = tip + 0.5f * wn - 10f * dir;
            q.Strip(b, Uv00, b - wn, Uv10, b + wn, Uv01, b + 20f * dir, Uv11, head, RouteColor);
        }

        /// <summary>Steps of one unit along <paramref name="dir"/> while the point is strictly inside the tile.</summary>
        private static void PushOut(ref Vector2 p, Edges r, Vector2 dir) {
            for (int guard = 0; guard < 4096 && r.L < p.x && p.x < r.R && r.T < p.y && p.y < r.B; guard++) p += dir;
        }

        /// <summary>0x8781e0 at the source's top-right corner: layer 0 of 4_f_kafra6's action 0, 4 s a loop.</summary>
        private void DrawKafra(RoClassicUiQuads q) {
            EnsureKafraSprite();
            if (kafraFrameImage == null || kafraFrameImage.Length == 0) {
                SetKafraSpriteRect(default);
                return;
            }

            int frames = kafraFrameImage.Length;
            long t = (long) (Time.realtimeSinceStartupAsDouble * 1000.0) % 4000L;
            int f = t != 0 ? (int) (t / (4000 / frames)) : 0;
            f = Mathf.Clamp(f, 0, frames - 1);
            int index = kafraFrameImage[f], first = kafraFrameImage[0];
            if (index < 0 || index >= kafraImageKey.Length || kafraImageKey[index] == null ||
                !system.Images.TryGet(kafraImageKey[index], out RoClassicUiImage image)) {
                SetKafraSpriteRect(default);
                return;
            }

            float s = fontIndex * 0.2f + 0.5f;
            float x = kafraFrom.Rect.R, y = kafraFrom.Rect.T;
            int h0 = first >= 0 && first < kafraImageSize.Length ? kafraImageSize[first].y : kafraImageSize[index].y;
            int dy = (int) (y - (h0 * s + 23f));
            if (dy > 0) dy = 0;
            Vector2Int size = kafraImageSize[index];
            int hw = (int) (size.x * s * 0.5f), hh = (int) (size.y * s);
            int left = (int) x - hw, right = (int) x + hw, top = (int) y - hh - dy, bottom = (int) y - dy;
            q.Image(left, top, right - left, bottom - top, image, White);
            SetKafraSpriteRect(new RectInt(left, top, right - left, bottom - top));
        }

        /// <summary>RECT +0x204: the DIB is punched there, so it redraws when the rectangle moves.</summary>
        private void SetKafraSpriteRect(RectInt rect) {
            if (rect.Equals(kafraSpriteRect)) return;
            kafraSpriteRect = rect;
            window.Invalidate();
        }

        private void EnsureKafraSprite() {
            if (kafraSpriteTried) return;
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null) return;
            kafraSpriteTried = true;
            try {
                SPR spr = assets.Load<SPR>(KafraSpritePath + ".spr");
                ACT act = assets.Load<ACT>(KafraSpritePath + ".act");
                if (spr == null || act == null || act.actions == null || act.actions.Length == 0 || act.actions[0].frames == null) return;
                ACT.Frame[] frames = act.actions[0].frames;
                kafraFrameImage = new int[frames.Length];
                int images = 0;
                for (int f = 0; f < frames.Length; f++) {
                    ACT.Layer layer = frames[f] != null && frames[f].layers != null && frames[f].layers.Length > 0 ? frames[f].layers[0] : null;
                    kafraFrameImage[f] = layer != null && layer.sprType == 0 ? layer.index : -1;
                    images = Math.Max(images, kafraFrameImage[f] + 1);
                }

                kafraImageSize = new Vector2Int[images];
                kafraImageKey = new string[images];
                for (int i = 0; i < images; i++) {
                    if (Array.IndexOf(kafraFrameImage, i) < 0) continue;
                    if (!spr.TryGetFrame(i, out SPR.Frame image) || image == null || image.type != SPR.TYPE_PAL) continue;
                    int w = image.originalWidth > 0 ? image.originalWidth : image.width;
                    int h = image.originalHeight > 0 ? image.originalHeight : image.height;
                    byte[] palette = spr.palette;
                    var pixels = new Color32[w * h];
                    for (int p = 0; p < pixels.Length && p < image.data.Length; p++) {
                        int c = image.data[p];
                        if (c != 0) pixels[p] = new Color32(palette[c * 4], palette[c * 4 + 1], palette[c * 4 + 2], 255);
                    }

                    string key = "@worldmap-kafra-" + i.ToString(CultureInfo.InvariantCulture);
                    if (!system.Images.SetDynamic(key, w, h, pixels)) continue;
                    kafraImageKey[i] = key;
                    kafraImageSize[i] = new Vector2Int(w, h);
                }
            } catch (Exception e) {
                Debug.LogWarning("World map Kafra sprite: " + e.Message);
                kafraFrameImage = null;
            }
        }

        /// <summary>0x88c050: the loop's lines, then the dashes and the ship on the current segment (0x877ba0).</summary>
        private void DrawAirRoute(RoClassicUiQuads q, AirRoute route) {
            int n = route.Points.Count;
            if (n < 2 || !system.Images.TryGet(Wv + "wv_Airf3.bmp", out RoClassicUiImage line)) return;
            for (int i = 0; i < n - 1; i++) {
                Vector2 a = route.Points[i].Centre, b = route.Points[i + 1].Centre;
                Vector2 delta = b - a;
                float length = delta.magnitude;
                if (length < 0.001f) continue;
                Vector2 normal = new Vector2(-delta.y, delta.x) / length * 2f;
                q.Strip(a - normal, Uv00, a + normal, Uv10, b - normal, Uv01, b + normal, Uv11, line, RouteColor);
            }

            if (!route.Active || route.Index < 0 || route.Index + 1 >= n) return;
            Vector2 p1 = route.Points[route.Index].Centre, p2 = route.Points[route.Index + 1].Centre;
            Vector2 d = p2 - p1;
            float segment = d.magnitude;
            if (segment < 0.001f) return;
            d /= segment;
            var nn = new Vector2(-d.y, d.x);
            float travelled = segment * route.Phase;
            Vector2 head = p1 + travelled * d;
            Vector2 q0 = p1 - 6f * nn, q1 = q0 + 16f * d;
            int alpha = 0x33;
            bool dashes = system.Images.TryGet(Wv + "wv_Airf.bmp", out RoClassicUiImage dash);
            for (int guard = 0; guard < 4096 && (q1 - p1).magnitude < travelled; guard++) {
                if (dashes && (head - q0).magnitude < 100f) {
                    q.Strip(q0, Uv00, q0 + 16f * nn, Uv10, q1, Uv01, q1 + 16f * nn, Uv11, dash, new Color32(255, 255, 255, (byte) Math.Min(255, alpha)));
                    alpha += 0x33;
                }

                q0 = q1 + 8f * d;
                q1 = q0 + 16f * d;
            }

            if (route.Phase >= 1f || !system.Images.TryGet(Wv + "wv_airplen32.bmp", out RoClassicUiImage ship)) return;
            Vector2 at = q0 + 8f * nn;
            q.Strip(at + 20f * nn + 40f * d, Uv00, at - 20f * nn + 40f * d, Uv10, at + 20f * nn, Uv01, at - 20f * nn, Uv11, ship, White);
        }

        private void DrawLabelFills(RoClassicUiQuads q, List<RoutePoint> labels) {
            foreach (RoutePoint label in labels) {
                if (IsHoveredTile(label.Rect)) continue;
                Fill(q, label.Rect.L, label.Rect.T, label.Rect.W, label.Rect.H, LabelFill);
            }
        }

        // ================================================================ the DIB part

        /// <summary>0x8a1d10's surface part for the shown routes: the white rounded outline and the name at (L + 2, T + 5).</summary>
        private void DrawRouteLabels(RoClassicWindow w) {
            if (kafraTo.Count > 0) DrawLabelTexts(w, kafraLabels);
            foreach (AirRoute route in airRoutes) {
                if (route.Built) DrawLabelTexts(w, route.Labels);
            }
        }

        private void DrawLabelTexts(RoClassicWindow w, List<RoutePoint> labels) {
            foreach (RoutePoint label in labels) {
                if (IsHoveredTile(label.Rect)) continue;
                Edges r = label.Rect;
                w.RoundOutline(r.L, r.T, r.W, r.H, White);
                w.TextOutOutline(r.L + 2, r.T + 5, RoClassicUiHost.MapDisplayName(label.Stem), 0, 12, DisplayName, Black, true);
            }
        }

        // ================================================================ data

        /// <summary>KafraMoveMapServiceList (overlay plain text, data.grf bytecode), read once per asset database.</summary>
        private static Dictionary<string, string[]> KafraTable() {
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (kafraTable != null && ReferenceEquals(kafraTableAssets, assets)) return kafraTable;
            kafraTableAssets = assets;
            kafraTable = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (assets == null || !assets.IsInitialized || !assets.TryResolvePath(KafraListPath, out string resolved)) return kafraTable;
            byte[] bytes = assets.ReadBytes(KafraListPath);
            if (bytes == null || bytes.Length == 0) return kafraTable;
            try {
                RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(new[] { new RoWorldMapLuaFile(resolved ?? KafraListPath, bytes) });
                foreach (KeyValuePair<string, LuaValue> global in context.Globals) {
                    if (!string.Equals(global.Key, "KafraMoveMapServiceList", StringComparison.OrdinalIgnoreCase) || global.Value.Kind != LuaValueKind.Table) continue;
                    foreach (KeyValuePair<string, LuaValue> source in global.Value.Table.Fields) {
                        if (source.Value.Kind != LuaValueKind.Table) continue;
                        var destinations = new List<string>();
                        for (int i = 1; i <= source.Value.Table.ArrayCount; i++) {
                            string destination = RoWorldMapDataUtility.NormalizeMapId(source.Value.Table.Get(i).ToDisplayString());
                            if (destination.Length > 0) destinations.Add(destination);
                        }

                        kafraTable[RoWorldMapDataUtility.NormalizeMapId(source.Key)] = destinations.ToArray();
                    }
                }
            } catch (Exception e) {
                Debug.LogWarning("World map Kafra list: " + e.Message);
            }

            return kafraTable;
        }

        /// <summary>The sweep console's state line: the shown routes.</summary>
        private void DescribeRoutes(StringBuilder s) {
            s.Append(" kafraTable=").Append(KafraTable().Count);
            if (kafraTo.Count > 0) {
                s.Append(" kafra=").Append(kafraSource).Append(" phase=").Append(kafraPhase.ToString("0.00", CultureInfo.InvariantCulture)).Append(" to=");
                foreach (RoutePoint p in kafraTo) s.Append(p.Stem).Append(',');
                s.Append(" sprite=").Append(kafraSpriteRect.x).Append(',').Append(kafraSpriteRect.y).Append(' ').Append(kafraSpriteRect.width).Append('x').Append(kafraSpriteRect.height)
                    .Append(" frames=").Append(kafraFrameImage?.Length ?? 0);
            }

            foreach (AirRoute route in airRoutes) {
                if (!route.Built) continue;
                s.Append(" air").Append(route.Type).Append('=');
                foreach (RoutePoint p in route.Points) s.Append(p.Stem).Append('>');
                s.Append(" seg=").Append(route.Index).Append(" phase=").Append(route.Phase.ToString("0.00", CultureInfo.InvariantCulture));
            }
        }
    }
}
