using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Entities;
using Ragnarok.Gameplay;
using Ragnarok.Sprites;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The try-on preview UIPreviewEquipWnd 0x126 (preview-equip-spec): opened from the item description's Preview button
    /// (0x15e) for an identified head / garment item with a view (not a hat with a client effect); 280×260,
    /// equip_preview\bg_mounting2.bmp as the whole frame, "Equip", the own character as the world shows it (no weapon or
    /// shield) standing with its feet at (137, 198), the previewed items accumulating on the low / top / mid / garment
    /// layers by the location table of SendMsg 0x17 (with the location masks of 0x7582c0), the rotate arrows 0x17c (+1) /
    /// 0x17d (−1) at (13, 151) / (247, 151) and Return 0x167 (the look re-read, the direction kept). No packets. The
    /// figure is drawn once per change; headgear animations and the baby-job scale are not reproduced.
    /// </summary>
    public sealed class RoClassicPreviewEquip {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string Prev = Ui + "equip_preview\\";
        private const int W = 280, H = 260, FeetX = 137, FeetY = 198;
        private const int ImageW = 220, ImageH = 230, ImageFeetY = 196;
        private const int CloseId = 0xc5, LeftId = 0x17c, RightId = 0x17d;
        private const string Prefs = "ro.ui.classic.previewequip.";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private static RoClassicPreviewEquip instance;
        private static HashSet<int> effectHats;
        private static RoLoginShellSession boundSession;

        /// <summary>Per frame from the chat window's classic ticks: the session the worn items come from.</summary>
        public static void Bind(RoLoginShellSession loginSession) => boundSession = loginSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            effectHats = null;
            boundSession = null;
        }

        private readonly RoClassicUiSystem system;
        private readonly RoClassicWindow window;
        private RoLoginShellSession session;
        private int dir;
        private int low, top, mid, garment;        // R+0x28 / +0x2c / +0x30 / +0xbc
        private int maskLow, maskTop, maskMid;     // R+0x34 / +0x38 / +0x3c
        private RoActorAppearanceRequest look;
        private int shownHash = int.MinValue, version;
        private string shownKey;
        private bool returnPressed;

        private RoClassicPreviewEquip(RoClassicUiSystem system) {
            this.system = system;
            var layout = new RoClassicWindowLayout { Class = "UIPreviewEquipWnd", WindowId = 0x126, Width = W, Height = H };
            layout.Children.Add(Button(CloseId, "close", W - 14, 3, 11, 11, Basic + "sys_close_off.bmp", Basic + "sys_close_on.bmp", Basic + "sys_close_on.bmp"));
            layout.Children.Add(Button(LeftId, "left", 13, 151, 20, 20, Prev + "chr_arrow_rotate_l_out.bmp", Prev + "chr_arrow_rotate_l_over.bmp", Prev + "chr_arrow_rotate_l_press.bmp"));
            layout.Children.Add(Button(RightId, "right", W - 20 - 13, 151, 20, 20, Prev + "chr_arrow_rotate_r_out.bmp", Prev + "chr_arrow_rotate_r_over.bmp", Prev + "chr_arrow_rotate_r_press.bmp"));
            window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                switch (child.Id) {
                    case CloseId:
                        Close();
                        break;
                    case LeftId:
                        dir = (dir + 1) & 7;
                        Render();
                        break;
                    case RightId:
                        dir = (dir + 7) & 7;
                        Render();
                        break;
                }
            };
            window.Pressed += (w, p) => {
                if (!ReturnAt(p)) return;
                returnPressed = true;
                w.Invalidate();
            };
            window.Released += (w, p) => {
                if (!returnPressed) return;
                returnPressed = false;
                if (ReturnAt(p)) Reset();   // 0x167
                w.Invalidate();
            };
            window.EscClose = () => {
                Close();
                return true;
            };
            window.Moved += w => {
                PlayerPrefs.SetInt(Prefs + "x", w.Position.x);
                PlayerPrefs.SetInt(Prefs + "y", w.Position.y);
            };
        }

        private static RoClassicChildLayout Button(int id, string path, int x, int y, int w, int h, string normal, string hover, string pressed) {
            var b = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            b.States["normal"] = normal;
            b.States["hover"] = hover;
            b.States["pressed"] = pressed;
            return b;
        }

        private static bool ReturnAt(Vector2Int p) => p.x >= 100 && p.x < 180 && p.y >= 230 && p.y < 250;

        /// <summary>
        /// The Preview button's condition (0x85d6c9..0x85d7f3): the location exactly one of the sixteen head / garment
        /// values, identified, a view id, and not in effectHatItemTable (IsEffectHatItem).
        /// </summary>
        public static bool CanPreview(RoItemInstance item, RoItemMetadata metadata) {
            if (item == null || metadata == null || !item.IsIdentified) return false;
            int loc = Location(item, metadata);
            switch (loc) {
                case 0x1: case 0x4: case 0x100: case 0x101: case 0x200: case 0x201: case 0x300: case 0x301:
                case 0x400: case 0x800: case 0xc00: case 0x1000: case 0x1400: case 0x1800: case 0x1c00: case 0x2000:
                    break;
                default:
                    return false;
            }

            return View(item, metadata) != 0 && !IsEffectHat(item.ItemId);
        }

        private static int Location(RoItemInstance item, RoItemMetadata metadata) =>
            (int) (item.EquipLocationHint != RoItemEquipLocation.None ? item.EquipLocationHint : metadata.EquipLocations);

        private static int View(RoItemInstance item, RoItemMetadata metadata) =>
            item.ViewIdHint != 0 ? item.ViewIdHint : metadata.ClassNumber != 0 ? metadata.ClassNumber : metadata.ViewId;

        /// <summary>hateffectinfo\effecthatitemtable.lub: the hats whose client effect the preview cannot show.</summary>
        private static bool IsEffectHat(int itemId) {
            if (effectHats == null) {
                RoAssetDatabase assets = RoClassicUiHost.GameAssets();
                if (assets == null) return false;
                effectHats = new HashSet<int>();
                string text = assets.ReadText("data/luafiles514/lua files/hateffectinfo/effecthatitemtable.lub");
                if (string.IsNullOrEmpty(text) && assets.TryFindFirstPathEndingWith("effecthatitemtable.lub", out string path)) text = assets.ReadText(path);
                if (!string.IsNullOrEmpty(text)) {
                    text = Regex.Replace(text, "--[^\n]*", string.Empty);
                    int open = text.IndexOf('{');
                    if (open >= 0) {
                        foreach (Match m in Regex.Matches(text.Substring(open), @"(?<![\[\w])(\d{3,})(?!\s*\])")) effectHats.Add(int.Parse(m.Value, CultureInfo.InvariantCulture));
                    }
                }
            }

            return effectHats.Contains(itemId);
        }

        /// <summary>Preview click: MakeWindow(0x126) (an open one is reused as it is), then SendMsg 0x17 with the item.</summary>
        public static void Preview(RoItemInstance item, RoItemMetadata metadata, RoLoginShellSession loginSession) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || item == null || metadata == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicPreviewEquip(system);
            RoClassicPreviewEquip p = instance;
            p.session = loginSession ?? boundSession;
            if (!p.window.Visible) {
                p.dir = 0;
                p.maskLow = p.maskTop = p.maskMid = 0;
                p.Reset();
                Vector2Int c = system.ClientSize;
                int x = PlayerPrefs.GetInt(Prefs + "x", (c.x - W) / 2), y = PlayerPrefs.GetInt(Prefs + "y", (c.y - H) / 2);
                if (x < 0 || y < 0 || x > c.x - W || y > c.y - H) {
                    x = (c.x - W) / 2;
                    y = (c.y - H) / 2;
                }

                p.window.MoveTo(x, y);
                p.window.Visible = true;
                system.Windows.BringToFront(p.window);
            }

            p.Apply(Location(item, metadata), View(item, metadata));
            p.Render();
        }

        private void Close() {
            window.Visible = false;
            look = null;
        }

        /// <summary>Reset 0x757ca0 (open, Return): the look the world shows, the masks seeded from the worn items (0x7582c0, not cleared first).</summary>
        private void Reset() {
            RoOnlineWorldController world = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            look = null;
            if (world == null || world.OwnActorId == 0 || !world.State.TryGetActor(world.OwnActorId, out RoOnlineActorSnapshot actor)) {
                window.Invalidate();
                return;
            }

            look = RoOnlineActorPresenter.BuildPlayerAppearanceRequest(actor);
            look.weaponId = 0;
            look.shieldId = 0;
            look.hasCart = false;   // FuncDrawWNDChar: body, head, three headgears and the garment only
            look.cartNum = 0;
            low = look.headgearLowerId;
            top = look.headgearTopId;
            mid = look.headgearMidId;
            garment = look.robeId;
            SeedMasks();
            Render();
        }

        private void SeedMasks() {
            RoGameplayEquipmentSnapshot equipment = session != null ? session.EquipmentSnapshot() : null;
            RoItemInstance[] items = equipment?.InventoryItems ?? Array.Empty<RoItemInstance>();
            foreach (RoItemInstance item in items) {
                if (item == null || item.WearState == RoItemEquipLocation.None || item.ViewIdHint == 0) continue;
                int worn = (int) item.WearState, loc = (int) item.EquipLocationHint;
                if (loc == 0) loc = worn;
                if (low != 0 && (worn & (0x1 | 0x1000)) != 0 && item.ViewIdHint == low) {
                    maskLow = 1;
                    if (loc == 0x301 || loc == 0x1c00) maskLow = maskTop = maskMid = 0x301;
                }

                if (top != 0 && (worn & (0x100 | 0x400)) != 0 && item.ViewIdHint == top) {
                    if (loc == 0x100 || loc == 0x400) maskTop = 0x100;
                    else if (loc == 0x101 || loc == 0x1400) maskTop = maskLow = 0x101;
                    else if (loc == 0x300 || loc == 0xc00) maskTop = maskMid = 0x300;
                    else if (loc == 0x301 || loc == 0x1c00) maskLow = maskTop = maskMid = 0x301;
                }

                if (mid != 0 && (worn & (0x200 | 0x800)) != 0 && item.ViewIdHint == mid) {
                    if (loc == 0x200 || loc == 0x800) maskMid = 0x200;
                    else if (loc == 0x201 || loc == 0x1800) maskMid = maskLow = 0x201;
                }
            }
        }

        /// <summary>SendMsg 0x17: the item's view onto the layers its location takes, clearing the conflicting ones.</summary>
        private void Apply(int loc, int v) {
            switch (loc) {
                case 0x1:
                case 0x1000:
                    low = v;
                    maskLow = 1;
                    if ((maskTop & 1) != 0) { maskTop = 0; top = 0; }
                    if ((maskMid & 1) != 0) { maskMid = 0; mid = 0; }
                    break;
                case 0x100:
                case 0x400:
                    top = v;
                    maskTop = 0x100;
                    if ((maskLow & 0x100) != 0) { maskLow = 0; low = 0; }
                    if ((maskMid & 0x100) != 0) { maskMid = 0; mid = 0; }
                    break;
                case 0x200:
                case 0x800:
                    mid = v;
                    maskMid = 0x200;
                    if ((maskLow & 0x200) != 0) { maskLow = 0; low = 0; }
                    if ((maskTop & 0x200) != 0) { maskTop = 0; top = 0; }
                    break;
                case 0x101:
                case 0x1400:
                    top = v;
                    low = 0;
                    maskTop = maskLow = 0x101;
                    if (maskMid != 0x200) { maskMid = 0; mid = 0; }
                    break;
                case 0x201:
                case 0x1800:
                    mid = v;
                    low = 0;
                    maskMid = maskLow = 0x201;
                    if (maskTop != 0x100) { maskTop = 0; top = 0; }
                    break;
                case 0x300:
                case 0xc00:
                    top = v;
                    mid = 0;
                    maskTop = maskMid = 0x300;
                    if (maskLow != 1) { maskLow = 0; low = 0; }
                    break;
                case 0x301:
                case 0x1c00:
                    low = v;
                    top = 0;
                    mid = 0;
                    maskLow = maskTop = maskMid = 0x301;
                    break;
                case 0x4:
                case 0x2000:
                    garment = v;
                    break;
            }
        }

        /// <summary>The figure into an image whenever the look or the direction changes (the client redraws it every frame).</summary>
        private void Render() {
            window.Invalidate();
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (look == null || assets == null) return;
            look.headgearLowerId = low;
            look.headgearTopId = top;
            look.headgearMidId = mid;
            look.robeId = garment;
            look.motion = RoSpriteMotion.Idle;
            look.facingDirection = (RoActorDirection) dir;
            look.headFacing = RoActorHeadFacing.Center;
            int hash;
            unchecked {
                hash = look.classId * 397 ^ look.hairId * 31 ^ look.hairColorId * 17 ^ look.bodyPaletteId * 7;
                hash = hash * 397 ^ low;
                hash = hash * 397 ^ top;
                hash = hash * 397 ^ mid;
                hash = hash * 397 ^ garment;
                hash = hash * 397 ^ dir;
            }

            if (hash == shownHash) return;
            shownHash = hash;
            int v = ++version;
            string key = "@preview-equip-" + (v & 1).ToString(CultureInfo.InvariantCulture);
            bool started = RoClassicActorPortrait.Render(assets, look, key, ImageW, ImageH, ImageFeetY, ok => {
                if (!ok || v != version) return;
                shownKey = key;
                window.Invalidate();
            });
            if (!started) shownHash = int.MinValue;
        }

        /// <summary>OnDraw 0x757bb0: the frame bitmap, the character at (137, 198), then the caption and the Return button.</summary>
        private void Draw(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Prev + "bg_mounting2.bmp");
            if (look != null && shownKey != null) {
                // Draw(flag 1): the figure scaled about the feet by 0xd0f5c0(job) — 0.8 Doram, 0.75 / 0.82 the baby jobs
                float sc = JobScale(look.classId);
                if (sc == 1f) w.DrawBitmap(FeetX - ImageW / 2, FeetY - ImageFeetY, shownKey);
                else w.DrawStretched((int) (FeetX - ImageW / 2 * sc), (int) (FeetY - ImageFeetY * sc), (int) (ImageW * sc), (int) (ImageH * sc), shownKey, White);
            }
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            string caption = RoClassicUiHost.MsgStr(0xb90, "Equip");
            w.TextOut(19, 4, caption, 0, 12, White);
            w.TextOut(18, 3, caption, 0, 12, Black);
            DrawBasicButton(w, 100, 230, 80, RoClassicUiHost.MsgStr(0xb9f, "Return"), returnPressed);
        }

        /// <summary>0xd0f5c0: 0.8 for the Doram jobs, 0.75 for the baby novice / first classes, 0.82 for the other small jobs, else 1.</summary>
        private static float JobScale(int job) {
            if (job == 4218 || job == 4220 || job == 4308 || job == 4316) return 0.8f;
            if ((job >= 0xfb7 && job <= 0xfbd) || job == 0xfcd || job == 0x105f) return 0.75f;
            if ((job >= 0xfbe && job <= 0xfcc) || (job >= 0x1000 && job <= 0x1010) || (job >= 0x103e && job <= 0x1056) || (job >= 0x1061 && job <= 0x1064) ||
                (job >= 0x106d && job <= 0x1072) || job == 0x107c || (job >= 0x107e && job <= 0x108e) || job == 0x1091 || job == 0x1092 || job == 0x1094 ||
                job == 0x1097 || job == 0x1098) return 0.82f;
            return 1f;
        }

        /// <summary>UIBasicButton 0x7b12d0: btn_{out,press}_{left,mid,right}, the text centred in font 0 h12 over its shadow.</summary>
        internal static void DrawBasicButton(RoClassicWindow w, int x, int y, int width, string text, bool pressed) {
            string state = pressed ? "press" : "out";
            w.DrawBitmap(x, y, Basic + "btn_" + state + "_left.bmp");
            for (int px = x + 6; px < x + width - 6; px += 6) w.DrawBitmap(px, y, Basic + "btn_" + state + "_mid.bmp");
            w.DrawBitmap(x + width - 6, y, Basic + "btn_" + state + "_right.bmp");
            RoClassicGdiFont font = w.FontFor(0, 12);
            int tx = x + (width - font.Measure(text)) / 2, ty = y + 3;
            w.TextOutPlain(tx + 1, ty + 1, text, font, pressed ? White : new Color32(0xd7, 0xd7, 0xd7, 255));
            w.TextOutPlain(tx, ty, text, font, Black);
        }

        /// <summary>The own inventory's item with that id (for the sweep), or null.</summary>
        internal static RoItemInstance OwnedItem(int id) {
            RoItemInstance[] owned = boundSession?.InventorySnapshot()?.InventoryItems ?? Array.Empty<RoItemInstance>();
            foreach (RoItemInstance own in owned) {
                if (own != null && own.ItemId == id) return own;
            }

            return null;
        }

        /// <summary>!preview item id|left|right|return|close|state for the sweep.</summary>
        internal static string Sweep(string args, RoLoginShellSession loginSession) {
            string[] t = (args ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            RoItemMetadataTable table = RoClassicUiHost.GameAssets()?.GameData?.Items;
            if (t.Length >= 2 && t[0] == "item" && table != null && int.TryParse(t[1], out int id) && table.TryGetItem(id, out RoItemMetadata meta)) {
                RoItemInstance item = null;
                RoItemInstance[] owned = (loginSession ?? boundSession)?.InventorySnapshot()?.InventoryItems ?? Array.Empty<RoItemInstance>();
                foreach (RoItemInstance own in owned) {
                    if (own != null && own.ItemId == id) item = own;
                }

                item ??= RoItemInstance.CreateRegular(-1, id, 1, true);
                if (!CanPreview(item, meta)) return "no preview for " + id + " (loc " + Location(item, meta).ToString("x", CultureInfo.InvariantCulture) + " view " + View(item, meta) + " effect " + IsEffectHat(id) + ")";
                Preview(item, meta, loginSession);
            }

            RoClassicPreviewEquip p = instance;
            if (p == null) return "no preview";
            if (t.Length >= 1) {
                switch (t[0]) {
                    case "left":
                        p.dir = (p.dir + 1) & 7;
                        p.Render();
                        break;
                    case "right":
                        p.dir = (p.dir + 7) & 7;
                        p.Render();
                        break;
                    case "return":
                        p.Reset();
                        break;
                    case "close":
                        p.Close();
                        break;
                }
            }

            return (p.window.Visible ? "open" : "closed") + " dir " + p.dir + " low " + p.low + " top " + p.top + " mid " + p.mid + " garment " + p.garment +
                   " masks " + p.maskLow.ToString("x", CultureInfo.InvariantCulture) + "/" + p.maskTop.ToString("x", CultureInfo.InvariantCulture) + "/" + p.maskMid.ToString("x", CultureInfo.InvariantCulture);
        }
    }
}
