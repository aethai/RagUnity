using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UIMonsterInfoWnd 0x4d, Sense / Monster Observation (misc-windows-spec §4): every ZC_MONSTER_INFO 0x018c makes a new
    /// 400 × 200 window at (0, 0) (not a singleton; classes 0x505..0x507, 0x725, 0x726 are ignored). "Monster Info", the
    /// monster's action 0 frame 0 fitted into (2, 19) 116 × 152, Name / Level / HP / DEF / Size / Race / Element / MDEF and
    /// the nine element boxes "%s:%d" in #42A562; the name is the actor the client last sent WZ_ESTIMATION at (party members
    /// who only receive the packet see their own last target's), cut at "#" and ellipsised to 100 px. OK closes that window.
    /// </summary>
    public static class RoClassicMonsterInfo {
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const int W = 400, H = 200, OkId = 0xb4;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Frame = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static readonly Color32 Chip = new Color32(0x42, 0x62, 0xa5, 255);
        private static readonly Color32 ChipText = new Color32(0x42, 0xa5, 0x62, 255);
        private static readonly List<RoClassicWindow> open = new List<RoClassicWindow>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            open.Clear();
            hovers.Clear();
        }

        private sealed class Info {
            public int Class, Level, Size, Race, Def, Mdef, Element;
            public uint Hp;
            public readonly byte[] Pct = new byte[9];
            public string Name = string.Empty, FullName = string.Empty;
            public string Sprite;
            public bool OkPressed;
        }

        // (MsgStr, x, y) of the nine boxes: water, earth, fire, wind, poison, holy, shadow, ghost, undead
        private static readonly (int Msg, string Text, int X, int Y)[] Boxes = {
            (0xf3a, "Water", 120, 110), (0xf3b, "Earth", 120, 128), (0xf3c, "Fire", 120, 146),
            (0xf3d, "Wind", 200, 110), (0xf3e, "Poison", 200, 128), (0xf3f, "Holy", 200, 146),
            (0xf40, "Shadow", 280, 110), (0xf41, "Ghost", 280, 128), (0xf42, "Undead", 280, 146),
        };

        /// <summary>ZC_MONSTER_INFO 0x018c (0xc71060).</summary>
        public static void Handle(byte[] b) {
            if (b == null || b.Length < 29) return;
            int cls = BitConverter.ToUInt16(b, 2);
            if (cls == 0x505 || cls == 0x506 || cls == 0x507 || cls == 0x725 || cls == 0x726) return;
            var info = new Info {
                Class = cls, Level = BitConverter.ToInt16(b, 4), Size = BitConverter.ToUInt16(b, 6), Hp = BitConverter.ToUInt32(b, 8),
                Def = BitConverter.ToInt16(b, 12), Race = BitConverter.ToUInt16(b, 14), Mdef = BitConverter.ToInt16(b, 16), Element = BitConverter.ToUInt16(b, 18),
            };
            Array.Copy(b, 20, info.Pct, 0, 9);
            Make(info);
        }

        private static void Make(Info info) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            // the name: the last WZ_ESTIMATION target's actor name, before "#"
            string name = ActorName(RoOutboundPackets.LastEstimationTargetAid);
            int hash = name.IndexOf('#');
            if (hash >= 0) name = name.Substring(0, hash);
            info.FullName = name;
            RoClassicGdiFont font = system.Font(12);
            if (font.Measure(name) > 100) {
                int n = name.Length;
                while (n > 0 && font.Measure(name.Substring(0, n) + "...") > 100) n--;
                name = name.Substring(0, n) + "...";
            }

            info.Name = name;
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets?.GameData?.EntityDisplay != null && assets.GameData.EntityDisplay.TryResolveSpriteBasePath(assets, info.Class, out string basePath)) info.Sprite = basePath;

            var layout = new RoClassicWindowLayout { Class = "UIMonsterInfoWnd", WindowId = 0x4d, Width = W, Height = H };
            var window = new RoClassicWindow(system, layout) { FrameDragArea = p => p.y < 17 };
            window.DrawDynamic += w => Draw(w, info);
            window.Pressed += (w, p) => {
                info.OkPressed = OkRect.Contains(p);
                w.Invalidate();
            };
            window.Released += (w, p) => {
                bool hit = info.OkPressed && OkRect.Contains(p);
                info.OkPressed = false;
                w.Invalidate();
                if (hit) Remove(w);
            };
            window.MoveTo(0, 0);
            window.Visible = true;
            system.Windows.BringToFront(window);
            open.Add(window);
            var hover = new HoverProducer(window, info);
            hovers[window] = hover;
            system.Register(hover);
        }

        private static readonly RectInt OkRect = new RectInt(W - 46, H - 24, 42, 20);

        private static readonly Dictionary<RoClassicWindow, HoverProducer> hovers = new Dictionary<RoClassicWindow, HoverProducer>();

        private static void Remove(RoClassicWindow w) {
            open.Remove(w);
            if (hovers.TryGetValue(w, out HoverProducer hover)) {
                RoClassicUiSystem.Instance?.Unregister(hover);
                hovers.Remove(w);
            }

            w.Visible = false;
            w.Destroy();
        }

        /// <summary>vf28 0x819700: over the name a cut name shows in full at the cursor.</summary>
        private sealed class HoverProducer : IRoClassicUiProducer {
            private readonly RoClassicWindow window;
            private readonly Info info;

            public HoverProducer(RoClassicWindow window, Info info) {
                this.window = window;
                this.info = info;
            }

            public void ClassicUiTick(RoClassicUiSystem ui) {
                if (!open.Contains(window) || ui.Windows.HoverWindow != window || info.Name == info.FullName) return;
                Vector2Int p = ui.Windows.Mouse - window.Position;
                if (p.x >= 161 && p.x <= 259 && p.y >= 31 && p.y <= 41) RoClassicTooltip.Shared.Show(info.FullName, White, ui.Windows.Mouse.x + 10, ui.Windows.Mouse.y + 10);
            }
        }

        private static string ActorName(uint aid) {
            if (aid == 0) return string.Empty;
            RoOnlineWorldController controller = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            if (controller == null) return string.Empty;
            var actors = new List<RoOnlineActorSnapshot>();
            controller.SnapshotActors(actors);
            foreach (RoOnlineActorSnapshot a in actors) {
                if (a.ServerId == aid && !string.IsNullOrWhiteSpace(a.Name)) return a.Name;
            }

            return string.Empty;
        }

        private static string Msg(int id, string fallback) => RoClassicUiHost.MsgStr(id, fallback);

        /// <summary>OnDraw 0x818200.</summary>
        private static void Draw(RoClassicWindow w, Info info) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.RoundOutline(0, 17, W, H - 17, Frame);
            w.FillRect(0, 17, W, 5, Frame);
            w.FillRect(1, 17, W - 2, H - 19, White);
            w.FillRect(2, H - 2, W - 4, 1, White);
            w.DrawBitmapRaw(0, H - 29, Basic + "btnbar_left2.bmp");
            for (int x = 21; x < W - 21; x += 21) w.DrawBitmapRaw(x, H - 29, Basic + "btnbar_mid2.bmp");
            w.DrawBitmapRaw(W - 21, H - 29, Basic + "btnbar_right2.bmp");
            string title = Msg(0x196, "Monster Info");
            w.TextOut(17, 3, title, 0, 12, White);
            w.TextOut(16, 2, title, 0, 12, Black);
            if (info.Sprite != null) RoClassicActSprite.Get(info.Sprite)?.DrawFitted(w, 0, 0, new RectInt(2, 19, 116, 152));

            void Row(int msg, string label, int x, int y, string value) {
                w.TextOut(x, y, Msg(msg, label), 0, 12, Black);
                w.TextOut(x + 40, y, value, 0, 12, Black);
            }

            string Pick(int v, int first, string[] names) => v >= 0 && v < names.Length ? Msg(first + v, names[v]) : string.Empty;
            Row(0x197, "Name", 120, 30, info.Name);
            Row(0x198, "Level", 120, 50, info.Level.ToString(CultureInfo.InvariantCulture));
            Row(0x199, "HP", 120, 70, unchecked((int) info.Hp).ToString(CultureInfo.InvariantCulture));
            Row(0xf46, "DEF", 120, 90, info.Def.ToString(CultureInfo.InvariantCulture));
            Row(0x19a, "Size", 260, 30, Pick(info.Size, 0xf43, new[] { "Small", "Medium", "Large" }));
            Row(0x19b, "Race", 260, 50, Pick(info.Race, 0xf2f, new[] { "Formless", "Undead", "Brute", "Plant", "Insect", "Fish", "Demon", "Demi-Human", "Angel", "Dragon" }));
            Row(0x19d, "Element", 260, 70, Pick(info.Element % 20, 0xf39, new[] { "Neutral", "Water", "Earth", "Fire", "Wind", "Poison", "Holy", "Shadow", "Ghost", "Undead" }));
            Row(0x19c, "MDEF", 260, 90, info.Mdef.ToString(CultureInfo.InvariantCulture));

            // the element table (0x80b580): 70 × 16 boxes, "%s:%d" centred in #42A562
            RoClassicGdiFont font = w.FontFor(0, 12);
            for (int i = 0; i < Boxes.Length; i++) {
                (int msg, string text, int x, int y) = Boxes[i];
                w.FillRect(x, y, 70, 16, Chip);
                w.FillRect(x + 1, y + 1, 68, 14, White);
                string s = Msg(msg, text) + ":" + info.Pct[i].ToString(CultureInfo.InvariantCulture);
                w.TextOut(x + (70 - font.Measure(s)) / 2, y + 2, s, 0, 12, ChipText);
            }

            w.DrawBitmap(OkRect.x, OkRect.y, "유저인터페이스\\" + (info.OkPressed ? "btn_ok_b.bmp" : "btn_ok.bmp"));
        }

        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            if (t[0] == "close") {
                foreach (RoClassicWindow w in open.ToArray()) Remove(w);
            } else if (t.Length >= 8) {
                int Arg(int k) => int.Parse(t[k], CultureInfo.InvariantCulture);
                if (t.Length > 8) RoOutboundPackets.LastEstimationTargetAid = uint.Parse(t[8], CultureInfo.InvariantCulture);
                var p = new byte[29];
                BitConverter.GetBytes((ushort) 0x018c).CopyTo(p, 0);
                BitConverter.GetBytes((ushort) Arg(0)).CopyTo(p, 2);
                BitConverter.GetBytes((short) Arg(1)).CopyTo(p, 4);
                BitConverter.GetBytes((ushort) Arg(2)).CopyTo(p, 6);
                BitConverter.GetBytes(Arg(3)).CopyTo(p, 8);
                BitConverter.GetBytes((short) Arg(4)).CopyTo(p, 12);
                BitConverter.GetBytes((ushort) Arg(5)).CopyTo(p, 14);
                BitConverter.GetBytes((short) Arg(6)).CopyTo(p, 16);
                BitConverter.GetBytes((ushort) Arg(7)).CopyTo(p, 18);
                for (int i = 0; i < 9; i++) p[20 + i] = (byte) (25 * (i % 5));
                Handle(p);
            }

            return "open=" + open.Count + " target=" + RoOutboundPackets.LastEstimationTargetAid;
        }
    }
}
