using System;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Entities;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.Sprites;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Another player's equipment (UIEquipWnd with +0x94 = 1, window id 0x8b; scratchpad equipview-spec): made by
    /// ZC_EQUIPWIN_MICROSCOPE 0x0b03 / 0x0b37 when it is not open (an open one is only redrawn with the new data). 280×167
    /// at (185, 300), never saved; "%s's Equipment" (MsgStr 0x551); equipwin_bg.bmp; two tabs, General (normal items)
    /// and Costume (costume and shadow items); the own window's slots, names and enchant grades, no ammo slot; the
    /// character standing south with the feet at (141, 149): body, head, the tab's top / mid / low headgear and garment
    /// views from its items, no weapon or shield. No item drag, no unequip, drops ignored; hover tooltips and the
    /// right-click item description work; a title double-click minimizes.
    /// </summary>
    public sealed class RoClassicViewEquip : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const int W = 280, H = 167, D = 20;
        private const int CloseId = 0xc5, MiniId = 0xc6;
        private const int TabCell = 131;   // two equal tabs in the 261-px rule of the own window's four (UNCONFIRMED)
        private const int PreviewW = 55, PreviewH = 125, PreviewFeetY = 103;
        private const string PreviewKey = "@view-equip-preview";
        private static readonly string[] TabLabels = { "General", "Costume" };   // MsgStr 0x70d 0x973
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Panel = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static readonly Color32 Line = new Color32(0xc3, 0xc3, 0xc3, 255);

        private static RoClassicViewEquip instance;
        private static RoLoginShellSession lastSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            lastSession = null;
        }

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private readonly RoItemInstance[] normal = new RoItemInstance[10];
        private readonly RoItemInstance[] costume = new RoItemInstance[10];
        private RoZcEquipWinMicroscope look;
        private int lastSequence = -1;
        private int tab;
        private bool minimized;
        private int previewHash = int.MinValue, previewVersion;
        private string previewShown;

        /// <summary>Per frame from the chat window's classic ticks: a new ZC_EQUIPWIN_MICROSCOPE opens or refreshes the window.</summary>
        public static void Tick(RoLoginShellSession session, RoItemMetadataTable table) {
            if (session == null) return;
            lastSession = session;
            int sequence = session.PlayerViewEquipmentSequence;
            if (instance == null) {
                RoClassicUiSystem system = RoClassicUiSystem.Instance;
                if (system == null) return;
                instance = new RoClassicViewEquip { system = system, lastSequence = sequence };
                system.Register(instance);
                return;
            }

            if (sequence == instance.lastSequence) return;
            instance.lastSequence = sequence;
            instance.metadata = table ?? RoItemMetadataTable.Empty;
            instance.Apply(session.PlayerViewEquipmentSnapshot());
        }

        /// <summary>0xc4a9d0 / 0xc4af30: the slot arrays from the records (a costume mask goes to the costume array), then the window.</summary>
        private void Apply(RoZcEquipWinMicroscope packet) {
            look = packet;
            Array.Clear(normal, 0, normal.Length);
            Array.Clear(costume, 0, costume.Length);
            foreach (RoZcInventoryItem record in packet.Items ?? Array.Empty<RoZcInventoryItem>()) {
                if (record.WearStateRaw == 0) continue;
                RoItemInstance item = RoGameplayState.ConvertInventoryItem(record);
                // the ammo record (0x8000) would land in the own costume slot 5 in the original: dropped
                for (int s = 0; s < 10; s++) {
                    if ((item.WearState & RoClassicEquipment.EqGeneral[s]) != 0) normal[s] = item;
                    if ((item.WearState & RoClassicEquipment.EqCostume[s]) != 0) costume[s] = item;
                }
            }

            if (window == null) Create();
            if (!window.Visible) {
                tab = 0;
                minimized = false;
                ApplySize();
                window.MoveTo(185, 300);
                window.Visible = true;
            }

            system.Windows.BringToFront(window);
            previewHash = int.MinValue;
            UpdatePreview();
            window.Invalidate();
        }

        private void Create() {
            var layout = new RoClassicWindowLayout { Class = "UIEquipWnd", WindowId = 0x8b, Width = W, Height = H };
            layout.Children.Add(Button(MiniId, "mini", 252, Basic + "sys_mini_off.bmp", Basic + "sys_mini_on.bmp"));
            layout.Children.Add(Button(CloseId, "close", 266, Basic + "sys_close_off.bmp", Basic + "sys_close_on.bmp"));
            window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == CloseId) window.Visible = false;
                else if (child.Id == MiniId) ToggleMinimized();
            };
            window.Pressed += (w, local) => {
                if (minimized || local.y < 17 || local.y >= 35 || local.x >= TabLabels.Length * (TabCell - 1) + 1) return;
                int next = Math.Min(TabLabels.Length - 1, local.x / (TabCell - 1));
                if (next == tab) return;
                tab = next;
                UpdatePreview();
                window.Invalidate();
            };
            window.DoubleClicked += (w, local) => {
                if (local.y < 17) ToggleMinimized();
            };
            window.RightClicked += (w, local) => {
                RoItemInstance item = ItemAt(RowSlotAt(local));
                if (item != null) RoClassicItemDescription.Toggle(item, metadata);
            };
            window.EscClose = () => {
                window.Visible = false;
                return true;
            };
        }

        private static RoClassicChildLayout Button(int id, string path, int x, string normal, string hover) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = 3, W = 11, H = 11 };
            button.States["normal"] = normal;
            button.States["hover"] = hover;
            button.States["pressed"] = hover;
            return button;
        }

        private void ToggleMinimized() {
            minimized = !minimized;
            ApplySize();
            window.Invalidate();
        }

        private void ApplySize() {
            int h = minimized ? 17 : H;
            window.SetHeight(h);
            window.HitArea = new RectInt(0, 0, W, h);
        }

        private RoItemInstance[] Slots => tab == 1 ? costume : normal;

        // ---- drawing (OnDraw 0x84ced0 with +0x94 = 1) ----

        private void Draw(RoClassicWindow w) {
            string name = look.CharacterName ?? string.Empty;
            string pattern = RoClassicUiHost.MsgStr(0x551, "%s's Equipment");
            int at = pattern.IndexOf("%s", StringComparison.Ordinal);
            string title = at < 0 ? pattern : pattern.Substring(0, at) + name + pattern.Substring(at + 2);
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_fix.bmp");
            w.TextOut(17, 4, title, 0, 12, White);
            w.TextOut(16, 3, title, 0, 12, Black);
            if (minimized) return;

            w.RoundOutline(0, 17, W, H - 17, Panel);
            w.FillRect(0, 17, W, 5, Panel);
            w.FillRect(1, 17, W - 2, H - 19, White);
            w.FillRect(2, H - 2, W - 4, 1, White);
            int tabsW = TabLabels.Length * (TabCell - 1) + 1;
            w.FillRect(tabsW, 34, W - tabsW, 1, Line);
            w.DrawBitmapRaw(0, 37, Basic + "equipwin_bg.bmp");
            DrawTabs(w);

            if (tab == 1) w.DrawBitmapRaw(0, D + 14, Basic + "equipwin_special.bmp");
            RoItemInstance[] slots = Slots;
            for (int s = 0; s < 10; s++) {
                RoItemInstance item = slots[s];
                if (item == null) continue;
                RoClassicEquipment.EqSlotRects(s, out Vector2Int icon, out Vector2Int label, out int width);
                RoClassicEquipment.EqDrawIconFor(w, icon.x, icon.y, item, metadata);
                RoClassicEquipment.EqDrawNameFor(w, label.x, label.y, width, item, metadata);
                if (item.EnchantGrade > 0) w.DrawBitmap(icon.x, icon.y + 12, Ui + "grade_enchant\\grade_icon" + item.EnchantGrade.ToString(CultureInfo.InvariantCulture) + ".bmp");
            }

            if (previewShown != null) w.DrawBitmap(W / 2 + 1 - PreviewW / 2, 149 - PreviewFeetY, previewShown);
        }

        /// <summary>The tab control (style 1) at (0, 17) with its two tabs, labels cut to fit.</summary>
        private void DrawTabs(RoClassicWindow w) {
            RoClassicGdiFont font = w.FontFor(0, 12);
            for (int pass = 0; pass < 2; pass++) {
                for (int i = 0; i < TabLabels.Length; i++) {
                    bool selected = i == tab;
                    if (selected != (pass == 1)) continue;
                    int x = (TabCell - 1) * i, y = 17 + (selected ? 0 : 1);
                    w.FillRect(x + 2, y, TabCell - 4, 1, Line);
                    w.FillRect(x + 1, y + 1, 1, 1, Line);
                    w.FillRect(x, y + 2, 1, 15, Line);
                    w.FillRect(x + TabCell - 2, y + 1, 1, 1, Line);
                    w.FillRect(x + TabCell - 1, y + 2, 1, 15, Line);
                    if (!selected) w.FillRect(x, 17 + 16, TabCell, 1, Line);
                    string label = RoClassicEquipment.EqFit(RoClassicUiHost.MsgStr(i == 0 ? 0x70d : 0x973, TabLabels[i]), TabCell - 15, font);
                    int tw = font.Measure(label);
                    w.TextOut(x + (TabCell - tw) / 2 + (TabCell - tw) % 2, 17 + (selected ? 2 : 3), label, 0, 12, Black);
                }
            }
        }

        // ---- the character (FuncDrawWNDChar 0x743340 / 0x743950 with the packet's look) ----

        /// <summary>
        /// 0x869f80 / 0x834690: the packet's job, sex, hair, colours and body style; top = slot 8's view, mid = slot 9's
        /// unless it is the top's item, low = slot 0's unless it is the top's or the mid's, garment = slot 2's - all from
        /// the shown tab's items. Rendered once per look into an image (no weapon, no shield).
        /// </summary>
        private void UpdatePreview() {
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null || !assets.IsInitialized) return;
            RoItemInstance[] slots = Slots;
            RoItemInstance top = slots[8], mid = slots[9], low = slots[0];
            if (mid != null && top != null && mid.BagIndex == top.BagIndex) mid = null;
            if (low != null && ((top != null && low.BagIndex == top.BagIndex) || (mid != null && low.BagIndex == mid.BagIndex))) low = null;
            int hair = Mathf.Max(0, (int) look.Head);
            var request = new RoActorAppearanceRequest {
                kind = RoActorAppearanceKind.Player,
                classId = look.Job,
                bodyStyleId = look.BodyStyle,
                isMale = look.Sex != 0,
                headId = hair,
                hairId = hair,
                hairColorId = look.HeadPalette,
                bodyPaletteId = look.BodyPalette,
                robeId = slots[2] != null ? slots[2].ViewIdHint : 0,
                headgearTopId = top != null ? top.ViewIdHint : 0,
                headgearMidId = mid != null ? mid.ViewIdHint : 0,
                headgearLowerId = low != null ? low.ViewIdHint : 0,
                motionProfile = RoSpriteMotionProfile.Player,
                motion = RoSpriteMotion.Idle,
                facingDirection = RoActorDirection.South,
                headFacing = RoActorHeadFacing.Center,
            };
            int hash;
            unchecked {
                hash = 17;
                hash = hash * 397 ^ request.classId;
                hash = hash * 397 ^ request.bodyStyleId;
                hash = hash * 397 ^ (request.isMale ? 1 : 0);
                hash = hash * 397 ^ request.headId;
                hash = hash * 397 ^ request.hairColorId;
                hash = hash * 397 ^ request.bodyPaletteId;
                hash = hash * 397 ^ request.robeId;
                hash = hash * 397 ^ request.headgearTopId;
                hash = hash * 397 ^ request.headgearMidId;
                hash = hash * 397 ^ request.headgearLowerId;
            }

            if (hash == previewHash) return;
            previewHash = hash;
            int version = ++previewVersion;
            string key = PreviewKey + "-" + (version & 1).ToString(CultureInfo.InvariantCulture);
            bool started = RoClassicActorPortrait.Render(assets, request, key, PreviewW, PreviewH, PreviewFeetY, ok => {
                if (!ok || version != previewVersion) return;
                previewShown = key;
                window?.Invalidate();
            });
            if (!started) previewHash = int.MinValue;
        }

        // ---- mouse (the own window's hit tests 0x834770 / 0x834f80 on the other arrays) ----

        private int RowSlotAt(Vector2Int p) {
            if (minimized || p.y < D + 17 || p.y > D + 145) return -1;
            int k = (p.y - D - 17) / 26;
            if (p.x > 5 && p.x < 106) return k == 0 ? 8 : k - 1;
            if (p.x > 169 && p.x < W - 6) return k == 0 ? 9 : k + 3;
            return -1;
        }

        private RoItemInstance ItemAt(int slot) => slot >= 0 && slot < 10 ? Slots[slot] : null;

        /// <summary>vf28: the hovered slot's name (or "%s [%d Option]") as the tooltip, at the own window's places.</summary>
        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (window == null || !window.Visible) return;
            RoClassicWindowManager windows = ui.Windows;
            if (windows.HoverWindow != window || windows.Dragging != null) return;
            Vector2Int local = windows.Mouse - window.Position;
            RoItemInstance item = ItemAt(RowSlotAt(local));
            if (item == null) return;
            int tx = local.x < 106 ? 6 : local.x < 170 ? W / 2 - 20 : 175;
            int ty = (local.y - D - 17) / 24 * 26 + D - 3;
            RoClassicTooltip.Shared.Show(RoClassicEquipment.EqTooltipTextFor(item, metadata), RoClassicEquipment.EqTooltipColorFor(item, metadata),
                window.Position.x + tx, window.Position.y + ty);
        }

        // ---- diagnostics (RoSkillSweepRunner "!veq ...") ----

        /// <summary>req aid (CZ_EQUIPWIN_MICROSCOPE) | tab t | min | close | state.</summary>
        internal static string Sweep(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            if (a[0] == "req" && a.Length > 1 && lastSession != null) {
                _ = lastSession.BeginLegacyCheckPlayerEquipment(uint.Parse(a[1], CultureInfo.InvariantCulture));
                return "requested " + a[1];
            }

            RoClassicViewEquip v = instance;
            if (v == null || v.window == null) return "no view-equip window";
            switch (a[0]) {
                case "tab":
                    v.tab = a.Length > 1 && a[1] == "1" ? 1 : 0;
                    v.UpdatePreview();
                    v.window.Invalidate();
                    break;
                case "min":
                    v.ToggleMinimized();
                    break;
                case "close":
                    v.window.Visible = false;
                    break;
            }

            var worn = new System.Text.StringBuilder();
            RoItemInstance[] slots = v.Slots;
            for (int s = 0; s < slots.Length; s++) {
                if (slots[s] != null) worn.Append(s).Append('=').Append(slots[s].ItemId).Append(" v").Append(slots[s].ViewIdHint).Append(' ');
            }

            return $"open={v.window.Visible} name='{v.look.CharacterName}' job={v.look.Job} tab={v.tab} min={v.minimized} preview={v.previewShown != null} worn=[{worn.ToString().Trim()}] pos={v.window.Position}";
        }
    }
}
