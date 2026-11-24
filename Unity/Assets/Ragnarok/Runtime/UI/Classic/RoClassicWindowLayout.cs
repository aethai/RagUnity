using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>A GDI font as the client creates it (CreateFontA arguments).</summary>
    public readonly struct RoClassicFontSpec : System.IEquatable<RoClassicFontSpec> {
        public RoClassicFontSpec(string face, int lfHeight, int weight, int charset, int quality) {
            Face = string.IsNullOrEmpty(face) ? "Arial" : face;
            LfHeight = lfHeight;
            Weight = weight;
            Charset = charset;
            Quality = quality;
        }

        public string Face { get; }
        public int LfHeight { get; }
        public int Weight { get; }
        public int Charset { get; }
        public int Quality { get; }

        // the font dictionary's key, looked up for every text draw: without these the struct was boxed and compared by
        // reflection at each lookup
        public bool Equals(RoClassicFontSpec other) =>
            LfHeight == other.LfHeight && Weight == other.Weight && Charset == other.Charset && Quality == other.Quality &&
            string.Equals(Face, other.Face, System.StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is RoClassicFontSpec other && Equals(other);

        public override int GetHashCode() {
            unchecked {
                int hash = Face != null ? Face.GetHashCode() : 0;
                hash = hash * 31 + LfHeight;
                hash = hash * 31 + Weight;
                hash = hash * 31 + Charset;
                return hash * 31 + Quality;
            }
        }
    }

    public enum RoClassicOpKind {
        Bitmap,
        Fill,
        Text,
    }

    public enum RoClassicTextAlign {
        Left,
        Right,
        Center,
    }

    /// <summary>An extra draw of a text before the main one (title shadows, outlines).</summary>
    public readonly struct RoClassicTextPass {
        public RoClassicTextPass(int dx, int dy, Color32 color) {
            Dx = dx;
            Dy = dy;
            Color = color;
        }

        public int Dx { get; }
        public int Dy { get; }
        public Color32 Color { get; }
    }

    /// <summary>One segment of a helper that draws a text piecewise (multi-colour text, per-glyph pens).</summary>
    public sealed class RoClassicTextRun {
        public int X;
        public int Y;
        public string Text;
        public string Font;
        public Color32 Color;
    }

    /// <summary>One recorded draw op of a window's own surface (window-local pixels).</summary>
    public sealed class RoClassicOp {
        public RoClassicOpKind Kind;
        public int X;
        public int Y;
        public int W;
        public int H;
        public string Path;
        public Color32 Color = new Color32(255, 255, 255, 255);
        public string Text;
        public string Font;
        public RoClassicTextAlign Align;
        /// <summary>Centre alignment box width: the text is centred in [x, x + BoxW).</summary>
        public int BoxW;
        /// <summary>Dynamic op: the data field it shows (null = static).</summary>
        public string Slot;
        /// <summary>Format template with {Field} / {Field:n0} / {Field,N} placeholders, for dynamic text.</summary>
        public string Format;
        /// <summary>Which properties vary for dynamic ops ("x,t", "w"...).</summary>
        public string Dynamic;
        /// <summary>Fill of the whole (child) surface, like ClearDC.</summary>
        public bool Whole;
        /// <summary>
        /// A fill with the colour key (the client's 0xFFFF00FF surface clear): transparent once composed, so it
        /// draws nothing.
        /// </summary>
        public bool Transparent;
        /// <summary>
        /// DrawBitmap without key skipping (noTrans 0): the bitmap's magenta stays in the surface and punches a
        /// transparent hole there. Drawn keyed here, which is identical unless it covers earlier drawing.
        /// </summary>
        public bool RawCopy;
        public RoClassicTextPass[] Passes;
        public RoClassicTextRun[] Runs;
    }

    public sealed class RoClassicChildLayout {
        public string Class;
        public int Id;
        /// <summary>Owning window's member holding the control (e.g. "+0xd0"), as named by the decompiled code.</summary>
        public string Owner;
        public string Path;
        /// <summary>"control" (implemented natively per class) or "window" (a nested frame window, unrolled).</summary>
        public string Kind;
        public int X;
        public int Y;
        public int W;
        public int H;
        /// <summary>False for children hidden (show 0) or not blitted into the parent (composed 0) in this state.</summary>
        public bool Visible = true;
        /// <summary>UIButton +0x94: the balloon its hover shows at the cursor (UIButton::vf28 0x7c0720); null / empty = none.</summary>
        public string Tooltip;
        public readonly Dictionary<string, string> States = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<RoClassicOp> Ops = new List<RoClassicOp>();
        public readonly List<RoClassicChildLayout> Children = new List<RoClassicChildLayout>();
        /// <summary>Data fields feeding the control by role (e.g. value → "Hp", max → "MaxHp").</summary>
        public readonly Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>How the control's text is made from its fields (e.g. "{Hp}  /  {MaxHp}").</summary>
        public string Format;
        /// <summary>Texts the control drew with the oracle's first seed (sample rendering).</summary>
        public string[] SampleTexts;
        /// <summary>Raw exported properties (bitmaps by role, colours, font...).</summary>
        public JObject Props;
    }

    /// <summary>
    /// Runtime layout of one window state, exported from the original client's own draw code executed in the
    /// oracle (format 1: <c>reference ui/runtime/&lt;Class&gt;[.&lt;state&gt;].json</c>, described in its FORMAT.md).
    /// Geometry, fonts, colours and bitmap paths are the client's; dynamic ops carry a slot name and format for the
    /// data binding.
    /// </summary>
    public sealed class RoClassicWindowLayout {
        public string Class;
        public int WindowId;
        public string State;
        public int Width;
        public int Height;
        public Vector2Int DefaultPosition;
        public readonly Dictionary<string, RoClassicFontSpec> Fonts = new Dictionary<string, RoClassicFontSpec>(StringComparer.Ordinal);
        public readonly List<RoClassicOp> Ops = new List<RoClassicOp>();
        public readonly List<RoClassicChildLayout> Children = new List<RoClassicChildLayout>();

        public static RoClassicWindowLayout Parse(string json) {
            JObject root = JObject.Parse(json);
            if ((int?) root["format"] != 1) throw new FormatException("unsupported classic layout format " + root["format"]);
            var layout = new RoClassicWindowLayout {
                Class = (string) root["class"],
                WindowId = (int?) root["windowId"] ?? -1,
                State = (string) root["state"],
                Width = (int?) root["w"] ?? 0,
                Height = (int?) root["h"] ?? 0,
            };
            if (root["defaultPos"] is JObject pos) {
                layout.DefaultPosition = new Vector2Int((int?) pos["x"] ?? 0, (int?) pos["y"] ?? 0);
            }

            if (root["fonts"] is JObject fonts) {
                foreach (JProperty f in fonts.Properties()) {
                    if (!(f.Value is JObject spec)) continue;
                    layout.Fonts[f.Name] = new RoClassicFontSpec((string) spec["face"], (int?) spec["lfHeight"] ?? 15,
                        (int?) spec["weight"] ?? 400, (int?) spec["charset"] ?? 0, (int?) spec["quality"] ?? 3);
                }
            }

            ReadOps(root["ops"] as JArray, layout.Ops);
            ReadChildren(root["children"] as JArray, layout.Children);
            return layout;
        }

        private static void ReadChildren(JArray array, List<RoClassicChildLayout> into) {
            if (array == null) return;
            foreach (JToken c in array) {
                if (!(c is JObject child)) continue;
                var node = new RoClassicChildLayout {
                    Class = (string) child["class"], Id = (int?) child["id"] ?? 0,
                    Owner = (string) child["ownerField"], Path = (string) child["path"], Kind = (string) child["kind"],
                    X = (int?) child["x"] ?? 0, Y = (int?) child["y"] ?? 0, W = (int?) child["w"] ?? 0, H = (int?) child["h"] ?? 0,
                    Visible = (int?) child["show"] != 0 && (int?) child["composed"] != 0,
                    Format = (string) child["fmt"],
                };
                if (child["states"] is JObject states) {
                    foreach (JProperty s in states.Properties()) node.States[s.Name] = FirstString(s.Value);
                }

                // exported controls carry their bitmaps by role in props.bitmaps (arrays of paths)
                if (child["props"] is JObject props && props["bitmaps"] is JObject bitmaps) {
                    foreach (JProperty b in bitmaps.Properties()) {
                        string path = FirstString(b.Value);
                        if (path == null) continue;
                        if (b.Name == "normal" || b.Name == "hover" || b.Name == "pressed" || b.Name == "disabled") {
                            node.States[b.Name] = path;
                        }
                    }
                }

                ReadOps(child["ops"] as JArray, node.Ops);
                ReadChildren(child["children"] as JArray, node.Children);
                if (child["fields"] is JObject fields) {
                    foreach (JProperty f in fields.Properties()) node.Fields[f.Name] = (string) f.Value;
                } else if (child["fields"] is JArray fieldList) {
                    for (int i = 0; i < fieldList.Count; i++) node.Fields[i.ToString(CultureInfo.InvariantCulture)] = (string) fieldList[i];
                }

                if (child["samples"]?["seedA"]?["texts"] is JArray texts) {
                    node.SampleTexts = new string[texts.Count];
                    for (int i = 0; i < texts.Count; i++) node.SampleTexts[i] = (string) texts[i];
                }

                node.Props = child["props"] as JObject;
                into.Add(node);
            }
        }

        private static void ReadOps(JArray array, List<RoClassicOp> into) {
            if (array == null) return;
            foreach (JToken token in array) {
                if (!(token is JObject o)) continue;
                string k = (string) o["k"];
                var op = new RoClassicOp {
                    X = (int?) o["x"] ?? 0, Y = (int?) o["y"] ?? 0, W = (int?) o["w"] ?? 0, H = (int?) o["h"] ?? 0,
                    Slot = (string) o["slot"], Format = (string) o["fmt"], Dynamic = JoinStrings(o["dyn"]),
                };
                switch (k) {
                    case "bmp":
                        op.Kind = RoClassicOpKind.Bitmap;
                        op.Path = (string) o["path"];
                        op.RawCopy = (int?) o["noTrans"] == 0;
                        break;
                    case "bmpRow": {
                        // n copies of a bitmap, step px apart (e.g. the title bar middle)
                        int n = (int?) o["n"] ?? 1, step = (int?) o["step"] ?? op.W;
                        for (int i = 0; i < n; i++) {
                            into.Add(new RoClassicOp {
                                Kind = RoClassicOpKind.Bitmap, Path = (string) o["path"], X = op.X + i * step, Y = op.Y, W = op.W, H = op.H,
                                RawCopy = (int?) o["noTrans"] == 0,
                            });
                        }

                        continue;
                    }
                    case "fill":
                        op.Kind = RoClassicOpKind.Fill;
                        op.Color = ParseColor((string) o["color"]);
                        op.Whole = (int?) o["whole"] == 1;
                        op.Transparent = RoClassicUiAtlas.IsClassicColorKey(op.Color);
                        break;
                    case "roundbox": {
                        // the exact rectangles the client's rounded box fills (same coordinate space as the op)
                        Color32 color = ParseColor((string) o["color"]);
                        if (o["rects"] is JArray rects) {
                            foreach (JToken r in rects) {
                                if (!(r is JArray rect) || rect.Count < 4) continue;
                                into.Add(new RoClassicOp {
                                    Kind = RoClassicOpKind.Fill, X = (int) rect[0], Y = (int) rect[1], W = (int) rect[2], H = (int) rect[3],
                                    Color = color, Transparent = RoClassicUiAtlas.IsClassicColorKey(color),
                                });
                            }
                        }

                        continue;
                    }
                    case "text":
                        op.Kind = RoClassicOpKind.Text;
                        op.Text = (string) o["t"] ?? string.Empty;
                        op.Font = (string) o["font"];
                        op.Color = ParseColor((string) o["color"]);
                        op.BoxW = (int?) o["boxW"] ?? 0;
                        string align = (string) o["align"];
                        op.Align = align == "right" ? RoClassicTextAlign.Right : align == "center" ? RoClassicTextAlign.Center : RoClassicTextAlign.Left;
                        if (align == "pen") op.X = (int?) o["penX"] ?? op.X;
                        op.Passes = ReadPasses(o["passes"] as JArray);
                        op.Runs = ReadRuns(o["runs"] as JArray, op.Font);
                        break;
                    default:
                        continue; // "raw": unrecognised surface primitives (listed as lossy by the exporter)
                }

                into.Add(op);
            }
        }

        private static RoClassicTextPass[] ReadPasses(JArray array) {
            if (array == null || array.Count == 0) return null;
            var passes = new List<RoClassicTextPass>(array.Count);
            foreach (JToken p in array) {
                if (p is JObject pass) passes.Add(new RoClassicTextPass((int?) pass["dx"] ?? 0, (int?) pass["dy"] ?? 0, ParseColor((string) pass["color"])));
            }

            return passes.ToArray();
        }

        private static RoClassicTextRun[] ReadRuns(JArray array, string defaultFont) {
            if (array == null || array.Count == 0) return null;
            var runs = new List<RoClassicTextRun>(array.Count);
            foreach (JToken r in array) {
                if (!(r is JObject run)) continue;
                runs.Add(new RoClassicTextRun {
                    X = (int?) run["x"] ?? 0, Y = (int?) run["y"] ?? 0, Text = (string) run["t"] ?? string.Empty,
                    Font = (string) run["font"] ?? defaultFont, Color = ParseColor((string) run["color"]),
                });
            }

            return runs.ToArray();
        }

        /// <summary>"dyn" is a list of the varying properties (e.g. ["x", "t"]); kept as "x,t".</summary>
        private static string JoinStrings(JToken token) {
            if (token is JArray array) {
                var parts = new string[array.Count];
                for (int i = 0; i < array.Count; i++) parts[i] = (string) array[i];
                return string.Join(",", parts);
            }

            return token != null && token.Type != JTokenType.Null ? (string) token : null;
        }

        private static string FirstString(JToken token) =>
            token is JArray array ? (array.Count > 0 ? (string) array[0] : null) : (string) token;

        public static Color32 ParseColor(string value) {
            if (string.IsNullOrEmpty(value)) return new Color32(0, 0, 0, 255);
            string hex = value.StartsWith("#", StringComparison.Ordinal) ? value.Substring(1)
                : value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value.Substring(2) : value;
            uint rgb = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color32((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, 255);
        }
    }
}
