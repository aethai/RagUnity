using System;
using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The server broadcast boxes (broadcast-spec §0-§2): two UITransBalloonText owned by the game mode, one message each —
    /// box A (mode+0x2b4) for ZC_BROADCAST2 0x01c3 and the 0x9a "ssss" / "micc" / "name#…" forms, box B (mode+0x5f4) for
    /// the plain, "blue" and "tool" 0x9a forms. A new message replaces the box's text and restarts its 20-s life; no fade,
    /// no queue. The box: black at alpha 0x66 with a rounded #5C5C5C outline, Arial (height + 3, default 12) lines wrapped
    /// at 72 bytes at (6, 4 + 19 i) over a black (+1, +1) shadow, the widest line + 12 wide; at
    /// (trunc((W − 640) / 2) − trunc(w / 2) + 320, 51), box A under a live box B (+17) when a 0x9a lays them out; under every
    /// window. 0x01c3 keeps the earlier per-line colours and takes the packet's font size (only those of its fields are read).
    /// The chat line is written here too, with the form's chat type (broadcast-spec §1: "ssss" 14, "micc" 0, the others
    /// and 0x01c3 24 = every tab); the shell's drain leaves these two packets' text to this class.
    /// </summary>
    public sealed class RoClassicBroadcast : IRoClassicUiProducer {
        private const int WrapBytes = 72;
        private const float LifeSeconds = 20f;
        private static readonly Color32 Box = new Color32(0, 0, 0, 0x66);
        private static readonly Color32 Outline = new Color32(0x5c, 0x5c, 0x5c, 255);
        private static readonly Color32 Shadow = new Color32(0, 0, 0, 255);
        private static readonly Color32 Yellow = new Color32(255, 255, 0, 255);
        private static readonly Color32 Cyan = new Color32(0, 255, 255, 255);

        private sealed class Balloon {
            public RoClassicUiLayer Layer;
            public bool Alive;
            public float Born;
            public int Height = 12;                       // +0x80
            public Color32 Color = new Color32(255, 255, 255, 255);   // +0x90
            public readonly List<string> Lines = new List<string>();
            public readonly List<Color32?> LineColors = new List<Color32?>();   // the per-line colour vector
            public int X, Y = 51, W, H;
            public bool Dirty;
        }

        private static RoClassicBroadcast instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private readonly Balloon a = new Balloon(), b = new Balloon();
        private readonly List<(int Start, int End)> wrap = new List<(int, int)>(4);

        private static RoClassicBroadcast Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicBroadcast { system = system };
                instance.a.Layer = system.Renderer.CreateLayer("broadcast A");
                instance.b.Layer = system.Renderer.CreateLayer("broadcast B");
                foreach (Balloon x in new[] { instance.a, instance.b }) {
                    x.Layer.AboveOverlayUi = true;
                    x.Layer.Visible = false;
                    system.Renderer.SendToBack(x.Layer);
                }

                system.Register(instance);
            }

            return instance;
        }

        /// <summary>The raw 0x009a / 0x01c3 frames (the entry queue's carrier).</summary>
        internal static void Handle(byte[] packet) {
            if (packet == null || packet.Length < 4) return;
            int id = BitConverter.ToUInt16(packet, 0);
            int len = Math.Min(BitConverter.ToUInt16(packet, 2), packet.Length);
            // the chat line first (it needs no box): mgr msg 3, then msg 1 with the form's colour and type
            string chatLine = ChatLine(packet, out string chatColour, out int chatType, out string chatName);
            if (chatLine != null) RoClassicChat.Instance?.AddTypedLine(chatLine, chatColour, chatType, popOpen: true, name: chatName);
            RoClassicBroadcast r = Ensure();
            if (r == null) return;
            if (id == 0x009a) r.Broadcast(packet, len);
            else if (id == 0x01c3 && len >= 0x10) r.Broadcast2(packet, len);
            else if ((id == 0x07fd || id == 0x0bba) && len >= 10) r.SpecialItem(packet, len, id == 0x0bba);
            else if (id == 0x0ada && len >= 32) r.Refine(packet);
        }

        private static string Text(byte[] p, int start, int end) {
            int n = 0;
            while (start + n < end && p[start + n] != 0) n++;
            if (n <= 0) return string.Empty;
            var bytes = new byte[n];
            Array.Copy(p, start, bytes, 0, n);
            return RoTextEncoding.Decode(bytes) ?? string.Empty;
        }

        private static Color32 Hex(byte[] p, int at, Color32 fallback) {
            string hex = System.Text.Encoding.ASCII.GetString(p, at, 6);
            return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint rgb)
                ? new Color32((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, 255)
                : fallback;
        }

        /// <summary>
        /// The chat line of a ZC_BROADCAST 0x009a / ZC_BROADCAST2 0x01c3 frame (broadcast-spec §1; 0xc45358 / 0xc451a7 /
        /// 0xc448d8, the text before the box's item-link conversion): its text, colour (#RRGGBB) and chat type — 0x009a
        /// "ssss" 14 (WOE Information), "micc" 0 with the sender as the name, every other form and 0x01c3 24 (every tab).
        /// Null for another packet.
        /// </summary>
        public static string ChatLine(byte[] packet, out string colour, out int type, out string name) {
            colour = null;
            type = RoChatLineTypes.Public;
            name = null;
            if (packet == null || packet.Length < 4) return null;
            int id = BitConverter.ToUInt16(packet, 0);
            int len = Math.Min(BitConverter.ToUInt16(packet, 2), packet.Length);
            if (id == 0x009a) {
                string s = Parse(packet, len, out Color32 c, out _, out string form, out name);
                colour = Html(c);
                type = RoChatLineTypes.BroadcastType(form);
                return s;
            }

            if (id == 0x01c3 && len >= 0x10) {
                colour = Html(new Color32(packet[6], packet[5], packet[4], 255));
                type = RoChatLineTypes.EveryTab;
                return StripHashName(Text(packet, 0x10, len));
            }

            return null;
        }

        /// <summary>"Name#suffix : text" → "Name : text" (a '#' past the first character; the text from the first ':').</summary>
        private static string StripHashName(string s) {
            int hash = s.IndexOf('#'), colon = s.IndexOf(':');
            if (hash <= 0) return s;
            string t = s.Substring(0, hash);
            if (colon > 0) t += s.Substring(colon);
            return t;
        }

        /// <summary>0xc449a0's prefix forms (the lowercased first 4 bytes): the text, its colour, box B or A, the form and the megaphone's sender.</summary>
        private static string Parse(byte[] p, int len, out Color32 colour, out bool toB, out string form, out string name) {
            int end = Math.Min(len, 4 + 0x3ff);
            string p4 = end >= 8 ? System.Text.Encoding.ASCII.GetString(p, 4, 4).ToLowerInvariant() : string.Empty;
            bool korBlue = end >= 8 && p[4] == 0xba && p[5] == 0xed && p[6] == 0xb7 && p[7] == 0xe7;   // CP949 "블루"
            colour = Yellow;
            toB = true;
            form = string.Empty;
            name = null;
            string s;
            if (p4 == "tool" && end >= 14) {
                colour = Hex(p, 8, colour);
                s = Text(p, 14, end);
            } else if (p4 == "blue" || korBlue) {
                colour = Cyan;
                s = Text(p, 8, end);
            } else if (p4 == "micc" && end >= 4 + 0x22) {
                // megaphone: name[24] at +4, RRGGBB at +0x1c, the text at +0x22 (the block list and the filter are not ported)
                colour = Hex(p, 4 + 0x1c, colour);
                s = Text(p, 4 + 0x22, end);
                name = Text(p, 4, 4 + 24);
                form = p4;
                toB = false;
            } else if (p4 == "ssss") {
                s = Text(p, 8, end);
                form = p4;
                toB = false;
            } else {
                s = Text(p, 4, end);
                if (s.IndexOf('#') > 0) {
                    s = StripHashName(s);
                    toB = false;
                }
            }

            return s;
        }

        /// <summary>0xc449a0: the prefix forms, then the box and the layout.</summary>
        private void Broadcast(byte[] p, int len) {
            string s = Links(Parse(p, len, out Color32 colour, out bool toB, out _, out _));
            Balloon box = toB ? b : a;
            StartBox(box);
            // SetFirstLine + AddLine: the lines and the per-line colours replaced
            box.Lines.Clear();
            box.LineColors.Clear();
            foreach (string line in WrapText(s)) {
                box.Lines.Add(line);
                box.LineColors.Add(colour);
            }

            box.Dirty = true;
            Layout(true);
        }

        /// <summary>0xc442f0: always box A; the colour, the text (the per-line colours kept), the font size, then y 51.</summary>
        private void Broadcast2(byte[] p, int len) {
            var colour = new Color32(p[6], p[5], p[4], 255);   // wire 0x00RRGGBB, little endian
            int fontSize = BitConverter.ToInt16(p, 0xa);
            string s = Links(StripHashName(Text(p, 0x10, len)));
            StartBox(a);
            a.Color = colour;
            if (s.Length > 0) {
                a.Lines.Clear();
                a.Lines.AddRange(WrapText(s));
            }

            a.Height = fontSize;
            a.Dirty = true;
            Layout(false);
        }

        /// <summary>A colour as the chat's #RRGGBB (0 → white, as mgr msg 1 does).</summary>
        private static string Html(Color32 c) =>
            c.r == 0 && c.g == 0 && c.b == 0 ? "#FFFFFF" : "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");

        /// <summary>ConvertItemLinks 0x87b120 in the box text: "<name>" inline, cut to half the chat box (296 px).</summary>
        private string Links(string s) =>
            s.IndexOf("<ITEML>", StringComparison.Ordinal) >= 0
                ? RoClassicItemLink.Convert(s, RoClassicUiHost.GameAssets()?.GameData?.Items, system.Font(12), 296, null)
                : s;

        private static readonly Color32 Orange = new Color32(255, 176, 98, 255);   // COLORREF 0x0062B0FF

        private static string ItemName(int id) {
            RoItemMetadataTable items = RoClassicUiHost.GameAssets()?.GameData?.Items;
            return items != null ? RoItemInstance.CreateRegular(0, id, 1).GetFullDisplayName(items) ?? string.Empty : id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// ZC_BROADCASTING_SPECIAL_ITEM_OBTAIN 0x07fd (0xc45b20): {type, u32 item, u8 len, name[len], …}; type 0 a box
        /// (u8, u32 box item), 1 a monster (u8 len, name), 2 an NPC → MsgStr 0x65c "%s has opened [%s] and received [%s]." or
        /// 0x74c "Congratulation %s,  Acquired [%s] !" in box A in orange (font height kept), the chat line in yellow.
        /// </summary>
        private void SpecialItem(byte[] p, int len, bool withRefine) {
            int type = p[4], item = BitConverter.ToInt32(p, 5), nameLen = Math.Max(0, (int) (sbyte) p[9]);
            string who = Text(p, 10, Math.Min(len, 10 + nameLen));
            int after = 10 + nameLen;
            string via = string.Empty;
            if (type == 0 && after + 5 <= len) via = ItemName(BitConverter.ToInt32(p, after + 1));
            else if (type == 1 && after + 1 <= len) via = Text(p, after + 1, Math.Min(len, after + 1 + Math.Max(0, (int) (sbyte) p[after])));
            // 0x0bba (variant 1): a box's refine level {u8 len, u32 refine} after the box item; above 0 → 0xf91
            int refine = withRefine && type == 0 && after + 10 <= len ? BitConverter.ToInt32(p, after + 6) : 0;
            string text = type == 2
                ? Format(RoClassicUiHost.MsgStr(0x74c, "Congratulation %s,  Acquired [%s] !"), who, ItemName(item))
                : refine > 0
                    ? Format(RoClassicUiHost.MsgStr(0xf91, "[%s] obtained '+%d %s' from [%s]."), who, refine.ToString(System.Globalization.CultureInfo.InvariantCulture), ItemName(item), via)
                    : Format(RoClassicUiHost.MsgStr(0x65c, "%s has opened [%s] and received [%s]."), who, via, ItemName(item));
            Announce(text, Orange, "#FFFF00");
        }

        /// <summary>ZC_BROADCAST_ITEMREFINING_RESULT 0x0ada (0xc8c870): {name[24], u32 item, s8 refine, u8 success} → 0xcc7 / 0xcc8, orange.</summary>
        private void Refine(byte[] p) {
            string who = Text(p, 2, 26);
            int item = BitConverter.ToInt32(p, 26), refine = (sbyte) p[30];
            string text = p[31] != 0
                ? Format(RoClassicUiHost.MsgStr(0xcc7, "[%s] successfully upgrades an equipment, [+%d %s] obtained."), who, refine.ToString(System.Globalization.CultureInfo.InvariantCulture), ItemName(item))
                : Format(RoClassicUiHost.MsgStr(0xcc8, "[%s] fails to upgrade [+%d %s]."), who, refine.ToString(System.Globalization.CultureInfo.InvariantCulture), ItemName(item));
            Announce(text, Orange, "#FFB062");
        }

        /// <summary>sprintf over %s / %d in order.</summary>
        private static string Format(string pattern, params string[] args) {
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

            return sb.ToString();
        }

        /// <summary>Box A like 0x01c3 (the text replaced, the per-line colours kept, y 51) and the chat line (type 0).</summary>
        private void Announce(string text, Color32 colour, string chatColour) {
            StartBox(a);
            a.Color = colour;
            if (text.Length > 0) {
                a.Lines.Clear();
                a.Lines.AddRange(WrapText(text));
            }

            a.Dirty = true;
            Layout(false);
            RoClassicChat.Instance?.AddLine(text, chatColour);
        }

        private void StartBox(Balloon box) {
            if (!box.Alive) {
                box.Alive = true;
                box.Height = 12;
                box.Color = new Color32(255, 255, 255, 255);
                box.Lines.Clear();
                box.LineColors.Clear();
            }

            box.Born = Time.unscaledTime;
        }

        private List<string> WrapText(string s) {
            var lines = new List<string>(2);
            RoClassicText.ByteWrap(s, WrapBytes, wrap);
            foreach ((int start, int end) in wrap) lines.Add(s.Substring(start, end - start));
            return lines;
        }

        /// <summary>The sizes, then the placement: centred at y 51; a 0x9a puts a live A under a live B (+17).</summary>
        private void Layout(bool fromBroadcast) {
            Vector2Int client = system.ClientSize;
            foreach (Balloon box in new[] { a, b }) {
                if (!box.Alive) continue;
                RoClassicGdiFont font = system.Font(box.Height);
                int widest = 0;
                foreach (string line in box.Lines) widest = Math.Max(widest, font.Measure(line));
                box.W = widest + 12;
                box.H = 4 + box.Lines.Count * (4 + font.LineHeight);
                box.X = (client.x - 640) / 2 - box.W / 2 + 320;
            }

            b.Y = 51;
            if (!fromBroadcast) a.Y = 51;
            else a.Y = b.Alive ? b.Y + b.H + 17 : 51;
            a.Dirty |= a.Alive;
            b.Dirty |= b.Alive;
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            foreach (Balloon box in new[] { a, b }) {
                if (!box.Alive) continue;
                if (Time.unscaledTime - box.Born > LifeSeconds) {
                    box.Alive = false;
                    box.Layer.Visible = false;
                    continue;
                }

                if (!box.Dirty) continue;
                box.Dirty = false;
                Draw(ui, box);
            }
        }

        /// <summary>UITransBalloonText OnDraw 0x7bf620: the box, the outline, each line's shadow and colour.</summary>
        private static void Draw(RoClassicUiSystem ui, Balloon box) {
            RoClassicGdiFont font = ui.Font(box.Height);
            int pitch = 4 + font.LineHeight;
            RoClassicUiQuads q = box.Layer.Quads;
            q.Clear();
            q.Image(box.X, box.Y, box.W, box.H, ui.White, Box);
            foreach (RectInt r in RoClassicWindow.RoundOutlineRects(box.X, box.Y, box.W, box.H)) q.Image(r.x, r.y, r.width, r.height, ui.White, Outline);
            for (int i = 0; i < box.Lines.Count; i++) {
                Color32 c = i < box.LineColors.Count && box.LineColors[i].HasValue ? box.LineColors[i].Value : box.Color;
                q.Text(font, box.X + 7, box.Y + 4 + pitch * i + 1, box.Lines[i], Shadow);
                q.Text(font, box.X + 6, box.Y + 4 + pitch * i, box.Lines[i], c);
            }

            box.Layer.Visible = true;
            box.Layer.MarkDirty();
        }

        /// <summary>!bcast a|b text: a 0x01c3 (gold, size 12) or a plain 0x009a played locally; box name item: a 0x07fd box line; refine name item n 0|1: a 0x0ada; state.</summary>
        internal static string Sweep(string args) {
            string t = (args ?? string.Empty).Trim();
            if (t.StartsWith("a ", StringComparison.Ordinal)) {
                byte[] text = System.Text.Encoding.UTF8.GetBytes(t.Substring(2) + "\0");
                var p = new byte[0x10 + text.Length];
                BitConverter.GetBytes((ushort) 0x01c3).CopyTo(p, 0);
                BitConverter.GetBytes((ushort) p.Length).CopyTo(p, 2);
                BitConverter.GetBytes(0xFFD700u).CopyTo(p, 4);
                BitConverter.GetBytes((short) 0x190).CopyTo(p, 8);
                BitConverter.GetBytes((short) 12).CopyTo(p, 0xa);
                text.CopyTo(p, 0x10);
                Handle(p);
            } else if (t.StartsWith("b ", StringComparison.Ordinal)) {
                byte[] text = System.Text.Encoding.UTF8.GetBytes(t.Substring(2) + "\0");
                var p = new byte[4 + text.Length];
                BitConverter.GetBytes((ushort) 0x009a).CopyTo(p, 0);
                BitConverter.GetBytes((ushort) p.Length).CopyTo(p, 2);
                text.CopyTo(p, 4);
                Handle(p);
            }

            string[] w = t.Split(' ');
            if (w.Length >= 3 && w[0] == "box") {
                byte[] name = System.Text.Encoding.ASCII.GetBytes(w[1]);
                var p = new byte[10 + 24 + 5];
                BitConverter.GetBytes((ushort) 0x07fd).CopyTo(p, 0);
                BitConverter.GetBytes((ushort) p.Length).CopyTo(p, 2);
                p[4] = 0;
                BitConverter.GetBytes(int.Parse(w[2])).CopyTo(p, 5);
                p[9] = 24;
                Array.Copy(name, 0, p, 10, Math.Min(23, name.Length));
                p[34] = 4;
                BitConverter.GetBytes(w.Length > 3 ? int.Parse(w[3]) : 616).CopyTo(p, 35);
                Handle(p);
            } else if (w.Length >= 5 && w[0] == "refine") {
                byte[] name = System.Text.Encoding.ASCII.GetBytes(w[1]);
                var p = new byte[32];
                BitConverter.GetBytes((ushort) 0x0ada).CopyTo(p, 0);
                Array.Copy(name, 0, p, 2, Math.Min(23, name.Length));
                BitConverter.GetBytes(int.Parse(w[2])).CopyTo(p, 26);
                p[30] = (byte) int.Parse(w[3]);
                p[31] = (byte) int.Parse(w[4]);
                Handle(p);
            }

            RoClassicBroadcast r = instance;
            if (r == null) return "no boxes";
            string Describe(Balloon x) => x.Alive ? x.X + "," + x.Y + " " + x.W + "x" + x.H + " [" + string.Join(" | ", x.Lines) + "]" : "-";
            return "A " + Describe(r.a) + "  B " + Describe(r.b);
        }
    }
}
