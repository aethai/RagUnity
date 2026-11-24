using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The rare prompts (misc-windows-spec §6): the Talkie Box UITalkboxTrapInputWnd 0x54 (HT_TALKIEBOX 125) and the Graffiti
    /// box UIGraffiStrboxWnd 0x65 (RG_GRAFFITI 220) opened by the ground click instead of the cast, 280 × 120 at
    /// (sx(185), sy(300) − 60), a 20-byte edit, OK → CZ_USE_SKILL_TOGROUND_WITHTALKBOX2 0x0367 {level, skill, x, y,
    /// contents[21]} (31 B); and Star Gladiator's UIStarPlaceAcceptWnd 0x77 from ZC_STARPLACE 0x0253 (the accept-box layout,
    /// 0x4ef for which 10, else 0x404), OK → CZ_AGREE_STARPLACE 0x0254 {which}, Cancel sends nothing.
    /// </summary>
    public static class RoClassicPrompts {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const int W = 280, H = 120, OkId = 0xb4, CancelId = 0xb5;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Frame = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static readonly List<RoClassicWindow> talkBoxes = new List<RoClassicWindow>();
        private static RoClassicWindow star;
        private static int starWhich;
        private static string starText = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            talkBoxes.Clear();
            sweepHooks.Clear();
            star = null;
        }

        /// <summary>The ground click of HT_TALKIEBOX / RG_GRAFFITI (0xcd662b): true when a box took the cast.</summary>
        public static bool TryTalkBox(int skillId, int level, int x, int y) {
            if ((skillId != 125 && skillId != 220) || RoClassicUiSystem.Instance == null) return false;
            MakeTalkBox(skillId, level, x, y);
            return true;
        }

        /// <summary>Factory 0x9ccf45: not a singleton, each cast stacks a new box; OnCreate 0x83dad0, OnDraw 0x84df90.</summary>
        private static void MakeTalkBox(int skillId, int level, int cellX, int cellY) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            bool talkie = skillId == 125;
            var layout = new RoClassicWindowLayout { Class = talkie ? "UITalkboxTrapInputWnd" : "UIGraffiStrboxWnd", WindowId = talkie ? 0x54 : 0x65, Width = W, Height = H };
            layout.Children.Add(BitmapButton(OkId, "ok", 189, 96, "btn_ok"));
            layout.Children.Add(BitmapButton(CancelId, "cancel", 234, 96, "btn_cancel"));
            var window = new RoClassicWindow(system, layout) { FrameDragArea = p => p.y < 17 };
            var edit = new RoClassicEditField { Rect = new RectInt(22, 50, 240, 16), MaxBytes = 20, Background = new Color32(242, 242, 242, 255) };
            window.AddEditField(edit);
            void Ok() {
                // mode Msg 0x83 (0xc1b7c8): the text cut to 20 bytes; level 0 comes from the skill list
                byte[] text = RoLoginShellSession.TextBytes(edit.Text);
                // 31 bytes in this client's length table: contents is the 21-byte _snprintf buffer
                var body = new byte[29];
                BitConverter.GetBytes((ushort) level).CopyTo(body, 0);
                BitConverter.GetBytes((ushort) skillId).CopyTo(body, 2);
                BitConverter.GetBytes((ushort) cellX).CopyTo(body, 4);
                BitConverter.GetBytes((ushort) cellY).CopyTo(body, 6);
                Array.Copy(text, 0, body, 8, Math.Min(20, text.Length));
                _ = RoClassicEntryQueue.CurrentSession?.BeginClassicPacket(0x0367, body, talkie ? "a Talkie Box message" : "a Graffiti text");
                RemoveTalkBox(window);
            }

            edit.Submitted += _ => Ok();
            sweepHooks[window] = (edit, Ok);
            window.Clicked += child => {
                if (child.Id == OkId) Ok();
                else if (child.Id == CancelId) RemoveTalkBox(window);
            };
            window.DrawDynamic += w => {
                DrawFrame(w, true);
                if (talkie) {
                    string caption = RoClassicUiHost.MsgStr(0x1b3, "Record a message in the Talkie Box");
                    w.TextOut(17, 3, caption, 0, 12, White);
                    w.TextOut(16, 2, caption, 0, 12, Black);
                }

                string prompt = RoClassicUiHost.MsgStr(0x1b4, "Please type a message for the Talkie Box");
                w.TextOut(17, 23, prompt, 0, 12, White);
                w.TextOut(16, 22, prompt, 0, 12, Black);
            };
            Vector2Int client = system.ClientSize;
            window.MoveTo((client.x - 640) / 2 + 185, client.y * 300 / 480 - 60);
            window.Visible = true;
            system.Windows.BringToFront(window);
            edit.Focus();
            talkBoxes.Add(window);
        }

        private static void RemoveTalkBox(RoClassicWindow w) {
            talkBoxes.Remove(w);
            sweepHooks.Remove(w);
            w.Visible = false;
            w.Destroy();
        }

        private static RoClassicChildLayout BitmapButton(int id, string path, int x, int y, string bitmap) {
            var b = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = 42, H = 20 };
            b.States["normal"] = Ui + bitmap + ".bmp";
            b.States["hover"] = Ui + bitmap + "_a.bmp";
            b.States["pressed"] = Ui + bitmap + "_b.bmp";
            return b;
        }

        /// <summary>DrawTitle(0, "", 0) + DrawBody(17, 0, "") + DrawButtonBar(2, 0, 1).</summary>
        private static void DrawFrame(RoClassicWindow w, bool buttonBar) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.RoundOutline(0, 17, W, H - 17, Frame);
            w.FillRect(0, 17, W, 5, Frame);
            w.FillRect(1, 17, W - 2, H - 19, White);
            w.FillRect(2, H - 2, W - 4, 1, White);
            if (!buttonBar) return;
            w.DrawBitmapRaw(0, H - 29, Basic + "btnbar_left2.bmp");
            for (int x = 21; x < W - 21; x += 21) w.DrawBitmapRaw(x, H - 29, Basic + "btnbar_mid2.bmp");
            w.DrawBitmapRaw(W - 21, H - 29, Basic + "btnbar_right2.bmp");
        }

        /// <summary>ZC_STARPLACE 0x0253 {u8 which} (0xc66420): MakeWindow(0x77) → msg 0x5b(which).</summary>
        public static void HandleStarPlace(byte[] b) {
            if (b == null || b.Length < 3) return;
            ShowStar((sbyte) b[2]);
        }

        /// <summary>The accept-box family's layout (0x83c560 / 0x84c7c0) at (sx(200), sy(200)); the text wrapped at 38 bytes.</summary>
        private static void ShowStar(int which) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (star == null || star.Layout == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>("Ragnarok/Generated/OriginalClient/ui/UIAllyGuildAcceptWnd").text);
                layout.Class = "UIStarPlaceAcceptWnd";
                layout.WindowId = 0x77;
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class != "UITextViewer") continue;
                    child.Fields["text"] = "Message";
                    if (child.Props != null) child.Props["wrapBytes"] = 0x26;
                    child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                    // UIScrollBar's own bitmaps (the capture scrolled nothing): 0x404 wraps to 4 lines, the view shows 3
                    foreach (RoClassicChildLayout bar in child.Children) {
                        if (bar.Class != "UIScrollBar" || bar.Props == null) continue;
                        var bitmaps = new Newtonsoft.Json.Linq.JObject();
                        foreach ((string role, string file) in new[] {
                                     ("scroll0up", "scroll0up"), ("scroll0mid", "scroll0mid"), ("scroll0down", "scroll0down"),
                                     ("up", "scroll0bar_up"), ("mid", "scroll0bar_mid"), ("down", "scroll0bar_down")
                                 }) {
                            bitmaps[role] = new Newtonsoft.Json.Linq.JArray(Ui + file + ".bmp");
                        }

                        bar.Props["bitmaps"] = bitmaps;
                    }
                }

                star = new RoClassicWindow(system, layout) { Visible = false };
                star.Wheeled += (w, local, delta) => {
                    RoClassicChildLayout viewer = w.Layout.Children.Find(c => c.Class == "UITextViewer");
                    if (viewer?.Props == null) return;
                    var lines = new List<(int, int)>();
                    RoClassicWindow.WrapBytes(starText, 0x26, lines);
                    viewer.Props["scroll"] = Mathf.Clamp(((int?) viewer.Props["scroll"] ?? 0) + (delta > 0 ? -1 : 1), 0, Math.Max(0, lines.Count - viewer.H / 18));
                    w.Invalidate();
                };
                star.Clicked += child => {
                    if (child.Id != OkId && child.Id != CancelId) return;
                    star.Visible = false;
                    // OK → mode Msg 0xbe → CZ_AGREE_STARPLACE 0x0254 {which}; Cancel sends nothing
                    if (child.Id == OkId) _ = RoClassicEntryQueue.CurrentSession?.BeginClassicPacket(0x0254, new[] { (byte) starWhich }, "a Feel confirmation");
                };
            }

            starWhich = which;
            starText = which == 10
                ? RoClassicUiHost.MsgStr(0x4ef, "Do you agree to cast the magic spell that consumes 1 Black Gemstone and 1,000,000 Zeny?")
                : RoClassicUiHost.MsgStr(0x404, "You cannot change a map's designation once it is designated. Are you sure that you want to designate this map?");
            star.Set("Message", starText);
            RoClassicChildLayout view = star.Layout.Children.Find(c => c.Class == "UITextViewer");
            if (view?.Props != null) view.Props["scroll"] = 0;
            Vector2Int client = system.ClientSize;
            star.MoveTo((client.x - 640) / 2 + 200, client.y * 200 / 480);
            star.Visible = true;
            system.Windows.BringToFront(star);
            star.Invalidate();
        }

        private static readonly Dictionary<RoClassicWindow, (RoClassicEditField Edit, Action Ok)> sweepHooks = new Dictionary<RoClassicWindow, (RoClassicEditField, Action)>();

        private static string starBalloon;
        private static float starBalloonAt;

        /// <summary>
        /// ZC_STARSKILL 0x020e {name[24] @2, class @0x1a, star @0x1e, result @0x1f} (0xc66450): the mode's balloon
        /// (wrap 72 bytes, #C8C8FF, centred at y 51, gone 20 s later): result 0 / 1 the place (0x345.. / 0x348..), 10 / 11 the
        /// monster (0x34b.. / 0x34e..) with the login id and the name, the effect 0x21f and effect\T_피리.wav; 20 the Taekwon
        /// mission 0x39f, 21 the taming mission 0x4a6, 22 the collector item 0x4f1 (effect 0x21f); 30 the reset 0x409 (effect 0x1b0).
        /// </summary>
        public static void HandleStarSkill(byte[] b) {
            if (b == null || b.Length < 32) return;
            int end = 2;
            while (end < 26 && b[end] != 0) end++;
            Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
            string name = System.Text.Encoding.GetEncoding(1252).GetString(b, 2, end - 2);
            int cls = BitConverter.ToInt32(b, 0x1a), star = b[0x1e], result = b[0x1f];
            string login = UnityEngine.Object.FindFirstObjectByType<RoMinimalLoginShell>()?.Account ?? string.Empty;
            string text = null;
            int effect = 0x21f;
            bool sound = false;
            string Two(int msg, string fallback, string a, string c) {
                string f = RoClassicUiHost.MsgStr(msg, fallback);
                int i = f.IndexOf("%s", StringComparison.Ordinal);
                if (i >= 0) f = f.Substring(0, i) + a + f.Substring(i + 2);
                i = f.IndexOf("%s", StringComparison.Ordinal);
                return i >= 0 ? f.Substring(0, i) + c + f.Substring(i + 2) : f;
            }

            if ((result == 10 || result == 11) && name.Length == 0) {
                name = RoClassicUiHost.GameAssets()?.GameData?.EntityDisplay?.GetEntityOrFallback(cls)?.DisplayName ?? string.Empty;
            }

            switch (result) {
                case 0 when star <= 2:
                    text = Two(0x345 + star, "%s has been designated as Gravity %s's Solar Space.", login, name);
                    sound = true;
                    break;
                case 1 when star <= 2:
                    text = Two(0x348 + star, "Gravity %s's Solar Space: %s", login, name);
                    sound = true;
                    break;
                case 10 when star <= 2:
                    text = Two(0x34b + star, "%s has been designated as Gravity %s's Solar Monster.", login, name);
                    sound = true;
                    break;
                case 11 when star <= 2:
                    text = Two(0x34e + star, "Gravity %s's Solar Monster: %s", login, name);
                    sound = true;
                    break;
                case 20: {
                    string f = RoClassicUiHost.MsgStr(0x39f, "[Taekwon Mission] Target Monster: %s (%d%%)");
                    int i = f.IndexOf("%s", StringComparison.Ordinal);
                    if (i >= 0) f = f.Substring(0, i) + name + f.Substring(i + 2);
                    i = f.IndexOf("%d", StringComparison.Ordinal);
                    if (i >= 0) f = f.Substring(0, i) + star.ToString(CultureInfo.InvariantCulture) + f.Substring(i + 2);
                    text = f.Replace("%%", "%");
                    break;
                }
                case 21:
                    text = Two(0x4a6, "[Taming Mission] Target Monster: %s", name, string.Empty);
                    break;
                case 22: {
                    Ragnarok.Data.RoItemMetadataTable items = RoClassicUiHost.GameAssets()?.GameData?.Items;
                    text = Two(0x4f1, "[Collector Rank] Target Item: %s", items != null ? items.GetItemOrFallback(cls).GetDisplayName(true) : cls.ToString(CultureInfo.InvariantCulture), string.Empty);
                    break;
                }
                case 30:
                    text = RoClassicUiHost.MsgStr(0x409, "[Solar, Lunar and Stellar Angel] Designated places and monsters have been reset.");
                    effect = 0x1b0;
                    break;
            }

            // a result outside the cases moves the old balloon but changes nothing else
            starBalloonAt = Time.unscaledTime;
            if (text == null) return;
            starBalloon = text;
            RoLoginShellSession session = RoClassicEntryQueue.CurrentSession;
            uint self = session != null ? session.LocalCharacterIds().AccountId : 0;
            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            Ragnarok.World.RoOnlineWorldController world = UnityEngine.Object.FindFirstObjectByType<Ragnarok.World.RoOnlineWorldController>();
            if (bridge != null && world != null && self != 0 && world.TryResolveActorAnchor(self, out Transform anchor, out Vector3 position)) {
                bridge.TrySpawn(effect, position, new Ragnarok.Effects.RoEffectSpawnOptions { FollowTarget = anchor, TargetActorId = self });
            }

            if (sound) Ragnarok.Audio.RoAudioEvents.PlayUiSound("effect\\T_피리.wav");
        }

        /// <summary>The game mode's OnProcess: the star balloon goes 20 s after the last 0x020e (0xc04eac).</summary>
        public static void Tick() {
            if (starBalloon == null) return;
            if (Time.unscaledTime > starBalloonAt + 20f) {
                starBalloon = null;
                return;
            }

            Vector2Int client = RoClassicUiSystem.Instance != null ? RoClassicUiSystem.Instance.ClientSize : Vector2Int.zero;
            RoClassicTooltip.Named("starskill").Show(starBalloon, new Color32(0xc8, 0xc8, 0xff, 255), (client.x - 640) / 2, 51, 72, 640);
        }

        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            int Arg(int k, int fallback) => k < t.Length ? int.Parse(t[k], CultureInfo.InvariantCulture) : fallback;
            RoClassicWindow last = talkBoxes.Count > 0 ? talkBoxes[talkBoxes.Count - 1] : null;
            switch (t[0]) {
                case "star": ShowStar(Arg(1, 0)); break;
                case "starskill": {
                    // starskill result star [name]
                    var p = new byte[32];
                    BitConverter.GetBytes((ushort) 0x020e).CopyTo(p, 0);
                    byte[] n = System.Text.Encoding.ASCII.GetBytes(t.Length > 3 ? t[3] : "prontera.gat");
                    Array.Copy(n, 0, p, 2, Math.Min(23, n.Length));
                    p[0x1e] = (byte) Arg(2, 0);
                    p[0x1f] = (byte) Arg(1, 0);
                    HandleStarSkill(p);
                    break;
                }
                case "talkie": MakeTalkBox(125, Arg(3, 1), Arg(1, 150), Arg(2, 150)); break;
                case "graffiti": MakeTalkBox(220, Arg(3, 1), Arg(1, 150), Arg(2, 150)); break;
                case "text" when last != null && sweepHooks.ContainsKey(last):
                    sweepHooks[last].Edit.SetText(string.Join(" ", t, 1, t.Length - 1));
                    break;
                case "ok" when last != null && sweepHooks.ContainsKey(last):
                    sweepHooks[last].Ok();
                    break;
                case "ok" when star != null && star.Visible:
                case "cancel" when star != null && star.Visible:
                    star.Visible = false;
                    if (t[0] == "ok") _ = RoClassicEntryQueue.CurrentSession?.BeginClassicPacket(0x0254, new[] { (byte) starWhich }, "a Feel confirmation");
                    break;
            }

            return "talkboxes=" + talkBoxes.Count + " star=" + (star != null && star.Visible) + " which=" + starWhich;
        }
    }
}
