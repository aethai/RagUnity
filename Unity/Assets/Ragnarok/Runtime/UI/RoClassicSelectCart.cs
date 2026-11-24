using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UISelectCartWnd 0x5f, the cart list (codex-ui-research RUNTIME-RULES-TRADE-CART-VENDING-CHATROOM.md "0x5f cart
    /// chooser"): 385 wide, one 98 px band per five carts, title MsgStr 0x293 "Cart List", each cart a 100 × 100 SPR/ACT
    /// control (ID 9000 + type) at ((slot % 5)·70 + dx, (slot / 5)·82 + dy) with the per-type offsets of 0x82ada0. Change
    /// Cart offers the base carts 1…9 by base level (> 0, 40, 65, 80, 90, 100, 110, 120, 130) plus 13 above 100; a
    /// ZC_SELECTCART list (cart decoration) replaces them. A pick sends Msg(0x9a): CZ_SELECTCART with the identity, else
    /// CZ_REQ_CHANGECART; X closes; Esc does not (IsEscExcluded).
    /// </summary>
    public sealed class RoClassicSelectCart : IRoClassicUiProducer {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const int Width = 385;
        private const int BandH = 98;
        private const int Preview = 100;
        private const int CloseId = 0xc5;
        private static readonly int[] LevelThresholds = { 0, 40, 65, 80, 90, 100, 110, 120, 130 };
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private static RoClassicSelectCart instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private RoLoginShellSession session;
        private RoAssetDatabase assets;
        private readonly List<int> types = new List<int>();
        private readonly HashSet<int> previews = new HashSet<int>();
        private uint identity;

        /// <summary>The classic list instead of the legacy window; false while the classic UI is off.</summary>
        internal static bool Show(RoLoginShellSession session, RoAssetDatabase assets, int baseLevel, uint identity, int[] offered) {
            if (session == null) return false;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return false;
            if (instance == null || instance.system != system) {
                instance = new RoClassicSelectCart { system = system };
                system.Register(instance);
            }

            instance.Open(session, assets, BuildList(baseLevel, identity, offered), identity);
            return true;
        }

        /// <summary>vf15 0x846f00 (base carts by level) / msg 0x22 (the server's list, signed bytes).</summary>
        public static List<int> BuildList(int baseLevel, uint identity, int[] offered) {
            var list = new List<int>();
            if (identity != 0 || (offered != null && offered.Length > 0)) {
                foreach (int type in offered ?? Array.Empty<int>()) list.Add(unchecked((sbyte) type));
                return list;
            }

            for (int i = 0; i < LevelThresholds.Length; i++) {
                if (baseLevel > LevelThresholds[i]) list.Add(i + 1);
            }

            if (baseLevel > 100) list.Add(13);
            return list;
        }

        /// <summary>FUN_0082ada0: the control's top-left for a slot and type.</summary>
        public static Vector2Int PreviewOrigin(int slot, int type) {
            int x = slot % 5 * 70, y = slot / 5 * 82;
            switch (type) {
                case 1:
                case 2:
                case 10:
                    return new Vector2Int(x, y + 15);
                case 4: return new Vector2Int(x + 18, y + 18);
                case 5: return new Vector2Int(x + 15, y + 23);
                case 6:
                case 7:
                case 8:
                case 9:
                    return new Vector2Int(x + 13, y + 28);
                case 11: return new Vector2Int(x + 16, y + 15);
                case 12: return new Vector2Int(x, y + 5);
                case 13: return new Vector2Int(x + 4, y + 18);
                default: return new Vector2Int(x, y + 28);
            }
        }

        /// <summary>OnDraw 0x850090 without its title: the title bar with sys_base, the body, the bottom corners.</summary>
        public static void BuildFrame(List<RoClassicOp> ops, int h) {
            ops.Clear();
            RoClassicFrames.TitleBar(ops, Width, true);
            RoClassicFrames.Body(ops, Width, h);
            RoClassicFrames.Bitmap(ops, "유저인터페이스\\sysboxs_ld.bmp", 0, h - 7);
            RoClassicFrames.Bitmap(ops, "유저인터페이스\\sysboxs_rd.bmp", Width - 7, h - 7);
        }

        private void Open(RoLoginShellSession boundSession, RoAssetDatabase boundAssets, List<int> list, uint boundIdentity) {
            session = boundSession;
            if (!ReferenceEquals(assets, boundAssets)) previews.Clear();
            assets = boundAssets;
            identity = boundIdentity;
            types.Clear();
            types.AddRange(list);
            if (window == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UISelectCartWnd").text);
                layout.Children.RemoveAll(c => c.Class == "UISpriteButton");
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                window = new RoClassicWindow(system, layout) { Visible = false, ClickableAt = _ => true };
                window.DrawDynamic += Draw;
                window.Clicked += child => {
                    if (child.Id == CloseId) Close();
                };
                window.Pressed += (w, local) => {
                    int type = TypeAt(local);
                    if (type != 0) Pick(type);
                };
            }

            int h = Math.Max(1, (types.Count + 4) / 5) * BandH;
            window.Layout.Height = h;
            window.SetHeight(h);
            BuildFrame(window.Layout.Ops, h);
            foreach (int type in types) EnsurePreview(type);
            Vector2Int client = system.ClientSize;
            // the factory's common block: ((W − 640) / 2 + 200, H · 200 / 480)
            window.MoveTo(Mathf.Clamp(client.x / 2 - 120, 0, Math.Max(0, client.x - Width)), Mathf.Clamp(client.y * 200 / 480, 0, Math.Max(0, client.y - h)));
            window.Visible = true;
            system.Windows.BringToFront(window);
            window.Invalidate();
        }

        private static string PreviewKey(int type) => "@cart-preview-" + type.ToString(CultureInfo.InvariantCulture);

        /// <summary>FUN_00833d80 → the cart's .spr/.act, drawn into its 100 × 100 control around the control's foot point.</summary>
        private void EnsurePreview(int type) {
            if (previews.Contains(type) || assets == null || !assets.IsInitialized) return;
            string basePath = RoMapRentalVisualSemantics.ResolveCartBasePath(Mathf.Clamp(type, 0, 13));
            SPR spr = assets.Load<SPR>(basePath + ".spr");
            ACT act = assets.Load<ACT>(basePath + ".act");
            if (RoCartPreviewTextureBuilder.TryBuildPixels(spr, act, Preview, Preview, Preview / 2, Preview * 3 / 4, out Color32[] pixels) &&
                system.Images.SetDynamic(PreviewKey(type), Preview, Preview, pixels)) {
                previews.Add(type);
            }
        }

        private void Draw(RoClassicWindow w) {
            string title = RoClassicUiHost.MsgStr(0x293, "Cart List");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            for (int slot = 0; slot < types.Count; slot++) {
                if (!previews.Contains(types[slot])) continue;
                Vector2Int at = PreviewOrigin(slot, types[slot]);
                w.DrawBitmap(at.x, at.y, PreviewKey(types[slot]));
            }
        }

        /// <summary>The cart control under the cursor (a later control lies over an earlier one).</summary>
        private int TypeAt(Vector2Int local) {
            if (local.y < 17) return 0;
            for (int slot = types.Count - 1; slot >= 0; slot--) {
                Vector2Int at = PreviewOrigin(slot, types[slot]);
                if (local.x >= at.x && local.x < at.x + 70 && local.y >= at.y && local.y < Math.Min(at.y + Preview, (slot / 5 + 1) * BandH)) return types[slot];
            }

            return 0;
        }

        /// <summary>vf37 0x8651e0 message 6: an offered 9000 + type → Msg(0x9a, type, identity), then the window goes.</summary>
        private void Pick(int type) {
            if (!types.Contains(type) || session == null) return;
            Close();
            _ = identity != 0 ? session.BeginLegacySelectCartDecoration(identity, type) : session.BeginLegacyChangeCart(type);
        }

        private void Close() {
            if (window != null) window.Visible = false;
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
        }

        /// <summary>The sweep console: open level | deco identity t,t | pick type | close | state.</summary>
        public static string Sweep(string command, RoLoginShellSession session, RoAssetDatabase assets) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            switch (a[0]) {
                case "open":
                    Show(session, assets, Arg(1), 0, null);
                    break;
                case "deco":
                    var offered = new List<int>();
                    foreach (string t in (a.Length > 2 ? a[2] : string.Empty).Split(',')) {
                        if (int.TryParse(t, out int n)) offered.Add(n);
                    }

                    Show(session, assets, 0, (uint) Arg(1), offered.ToArray());
                    break;
                case "pick":
                    instance?.Pick(Arg(1));
                    break;
                case "close":
                    instance?.Close();
                    break;
            }

            if (instance == null || instance.window == null) return "no cart list";
            var s = new StringBuilder();
            s.Append("visible=").Append(instance.window.Visible).Append(" identity=").Append(instance.identity).Append(" size=")
                .Append(instance.window.Width).Append('x').Append(instance.window.Height).Append(" types=");
            foreach (int type in instance.types) s.Append(type).Append(instance.previews.Contains(type) ? "" : "(no preview)").Append(' ');
            return s.ToString().TrimEnd();
        }
    }
}
