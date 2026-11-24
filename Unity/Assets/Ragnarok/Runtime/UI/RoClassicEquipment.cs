using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Effects.Classic;
using Ragnarok.Entities;
using Ragnarok.Gameplay;
using Ragnarok.Sprites;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// The equipment window, the original client's UIEquipWnd (window id 10) from its reverse-engineered OnCreate / OnDraw
    /// 0x84ced0 / slot drawing 0x82e4f0 / item names 0x82dff0 / mouse handlers (reference ui-static
    /// ui/RUNTIME-RULES-ITEMS.md §2), with its Title and Damage Indicator tabs. Opened by the menu icon / Alt+Q; the open
    /// window is rebuilt from the session's equipment sequence by RoClassicGameUi. The character preview is drawn once per
    /// appearance into an image (RoClassicActorPortrait). The equip / take-off requests and the look are
    /// <see cref="RoInventoryModel"/>.
    /// </summary>
    public sealed class RoClassicEquipment {
        private const string EqUi = "유저인터페이스\\";
        private const string EqBasic = EqUi + "basic_interface\\";
        private const string EqIcons = EqUi + "item\\";
        private const string EqPrefs = "ro.ui.classic.equip.";
        private const string EqPreviewKey = "@equip-preview";
        private const int EqW = 280;
        private const int EqH = 187;          // 167 + the 20-px bottom strip of the own window
        private const int EqBody = 20;        // +0xd4: y offset of the body
        private const int EqTabCell = 66;     // 4 equal tabs in 261 px (survey), labels cut at 51 px
        private const int EqAmmoSlot = 15;
        private const int EqCloseId = 0xc5;
        private const int EqMiniId = 0xc6;
        private const int EqShowEquipId = 0xd1;
        private const int EqUnequipAllId = 0x23b;
        private const int EqDsApplyId = 0x10b;
        private const string EqDamage = EqUi + "showdamage\\";
        private static readonly string[] EqButtonStates = { "normal", "hover", "pressed" };
        private static readonly Color32 EqDsName = new Color32(0x32, 0x32, 0x32, 255);
        private static readonly Color32 EqDsNameShadow = new Color32(0xba, 0xba, 0xba, 255);
        private static readonly int[] EqDsRadioX = { 8, 88, 148, 208 };
        private static readonly int[] EqDsRadioMsg = { 0xf9a, 0xf99, 0xf98, 0xf97 };
        private static readonly string[] EqDsRadioText = { "Default", "Left", "Center", "Right" };
        private static readonly string[] EqTabLabels = { "General", "Costume", "Title", "Damage Indicator" };   // MsgStr 0x70d 0x973 0xa7d 0xf95
        private static readonly Color32 EqBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 EqWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 EqPanel = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static readonly Color32 EqLine = new Color32(0xc3, 0xc3, 0xc3, 255);
        private static readonly Color32 EqShadow = new Color32(0xe0, 0xe0, 0xe0, 255);
        private static readonly Color32[] EqNameColors = {
            new Color32(0, 0, 0, 255), new Color32(0x66, 0x99, 0x00, 255), new Color32(0x00, 0xbf, 0xff, 255),
            new Color32(0xff, 0xa5, 0x00, 255), new Color32(0x94, 0x00, 0xd3, 255),
        };

        // slot (bit index of the location mask) -> location on the General / Costume tab
        internal static readonly RoItemEquipLocation[] EqGeneral = {
            RoItemEquipLocation.HeadBottom, RoItemEquipLocation.Weapon, RoItemEquipLocation.Garment, RoItemEquipLocation.Accessory1,
            RoItemEquipLocation.Armor, RoItemEquipLocation.Shield, RoItemEquipLocation.Shoes, RoItemEquipLocation.Accessory2,
            RoItemEquipLocation.HeadTop, RoItemEquipLocation.HeadMid,
        };

        internal static readonly RoItemEquipLocation[] EqCostume = {
            RoItemEquipLocation.CostumeHeadBottom, RoItemEquipLocation.ShadowWeapon, RoItemEquipLocation.CostumeRobe, RoItemEquipLocation.ShadowAccessoryRight,
            RoItemEquipLocation.ShadowArmor, RoItemEquipLocation.ShadowShield, RoItemEquipLocation.ShadowShoes, RoItemEquipLocation.ShadowAccessoryLeft,
            RoItemEquipLocation.CostumeHeadTop, RoItemEquipLocation.CostumeHeadMid,
        };

        private static RoClassicEquipment instance;
        private static RoItemMetadataTable tickMetadata = RoItemMetadataTable.Empty;   // the item table of the last frame (a window opens with it)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            tickMetadata = RoItemMetadataTable.Empty;
        }

        private readonly RoInventoryModel model = new RoInventoryModel();
        private readonly RoItemInstance[] eqSlots = new RoItemInstance[16];
        private RoClassicUiSystem system;
        private RoClassicWindow classicEquip;
        private RoClassicChildLayout eqShowEquip;
        private RoClassicChildLayout eqUnequipAll;
        private int seenSequence = int.MinValue;
        private RoItemMetadataTable seenMetadata;
        private RoAssetDatabase seenAssets;
        private bool showEquipConfig;   // CZ_CONFIG type 0, as last asked for (the server's answer is not read back)
        private bool eqMinimized;
        private int eqTab;
        private bool eqPreviewReady;
        // the preview image: rendered once per appearance (RoClassicActorPortrait), two keys so the last one shows meanwhile
        private int eqPreviewHash = int.MinValue, eqPreviewVersion;
        private string eqPreviewShown;
        private readonly int[] eqHighlight = { -1, -1, -1 };
        // the Damage Indicator tab: +0x134 first visible skin, +0x138 pending skin, +0x13c pending direction
        private int eqDsFirst, eqDsPending, eqDsDirection;
        // the Title tab's UIListBoxTitle +0x100 (id 0x1dc): rows (title ids, 0 = "Disable Title"), selection, first row
        private readonly List<int> eqTitleRows = new List<int>();
        private int eqTitleSel = -1, eqTitleFirst, eqTitleVersion = -1;
        private const int EqTitleListX = 3, EqTitleListY = 59, EqTitleListW = 274, EqTitleListH = 105, EqTitleRowH = 15;
        private static readonly Color32 EqTitleBack = new Color32(0xe1, 0xe1, 0xe1, 255);
        private static readonly Color32 EqTitleSelect = new Color32(0xca, 0xdf, 0xfb, 255);
        private static readonly Color32 EqTitleNew = new Color32(0xfa, 0xc0, 0x92, 255);
        private static readonly Color32 EqTitleBorder = new Color32(0x64, 0x64, 0x64, 255);
        // the hovered item's tooltip text, kept while the same item stays under the mouse
        private RoItemInstance tipItem;
        private string tipText;

        private RoClassicEquipment() { }

        public static bool IsOpen => instance?.classicEquip != null && instance.classicEquip.Visible;

        private int EqHeight => eqMinimized ? 17 : EqH;

        /// <summary>The menu icon / Alt+Q: opens or closes the window.</summary>
        public static void Toggle(RoLoginShellSession loginSession) {
            if (IsOpen) {
                instance.Hide();
                return;
            }

            Open(loginSession);
        }

        /// <summary>MakeWindow 10: shows the window rebuilt from the session; the Damage Indicator tab starts from the applied skin.</summary>
        public static void Open(RoLoginShellSession loginSession) {
            RoClassicEquipment t = Ensure(loginSession);
            if (t == null) return;
            try {
                t.EnsureClassicEquipment();
                t.seenSequence = loginSession.EquipmentWindowChangeSequence;
                if (!t.classicEquip.Visible) {
                    t.ResetClassicDamageSkinSelection();
                    t.classicEquip.Visible = true;
                    t.system.Windows.BringToFront(t.classicEquip);
                }

                t.Refresh();
            } catch (Exception e) {
                Debug.LogException(e);
                t.Hide();
            }
        }

        /// <summary>Leaving the map: the window closes on its first tab and the preview and the display option are forgotten.</summary>
        public static void Close() {
            RoClassicEquipment t = instance;
            if (t == null) return;
            t.Hide();
            t.eqTab = 0;
            t.showEquipConfig = false;
            t.eqPreviewReady = false;
            t.eqPreviewHash = int.MinValue;
            t.eqPreviewShown = null;
            t.tipItem = null;
            t.tipText = null;
            t.seenSequence = int.MinValue;
            if (t.classicEquip != null) t.ApplyClassicEquipState();
        }

        /// <summary>
        /// Per frame (RoClassicGameUi): the open window follows the session's equipment sequence (and the item table and
        /// the map's assets once they load), the Title list, the drag highlight and the hover tooltip.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession, RoItemMetadataTable metadata) {
            tickMetadata = metadata ?? RoItemMetadataTable.Empty;
            RoClassicEquipment t = instance;
            if (loginSession == null || t == null || t.classicEquip == null || !t.classicEquip.Visible) return;
            t.model.Bind(loginSession);
            t.model.Metadata = tickMetadata;
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            int sequence = loginSession.EquipmentWindowChangeSequence;
            if (sequence != t.seenSequence || !ReferenceEquals(t.model.Metadata, t.seenMetadata) || !ReferenceEquals(assets, t.seenAssets)) {
                t.seenSequence = sequence;
                t.Refresh();
            }

            t.UpdateClassicEquipment();
        }

        private static RoClassicEquipment Ensure(RoLoginShellSession loginSession) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || loginSession == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicEquipment { system = system };
            instance.model.Bind(loginSession);
            instance.model.Metadata = tickMetadata;
            return instance;
        }

        private void Hide() {
            // DeleteWindow(10) 0x9bd7ed: closing the window forgets the "new" titles
            RoClassicAchievements.ClearNewTitles();
            if (classicEquip != null) classicEquip.Visible = false;
        }

        /// <summary>Slots from the equipment snapshot, the preview kept rendering.</summary>
        private void Refresh() {
            if (classicEquip == null) return;
            seenMetadata = model.Metadata;
            seenAssets = RoClassicUiHost.GameAssets();
            RoLoginShellSession session = model.Session;
            RoGameplayEquipmentSnapshot gameplay = session != null ? session.EquipmentSnapshot() : RoGameplayEquipmentSnapshot.Empty;
            RoItemInstance[] items = gameplay.InventoryItems ?? Array.Empty<RoItemInstance>();
            Array.Clear(eqSlots, 0, eqSlots.Length);
            tipItem = null;
            RoItemEquipLocation[] map = eqTab == 1 ? EqCostume : EqGeneral;
            foreach (RoItemInstance item in items) {
                if (item == null || !item.IsEquipped) continue;
                for (int s = 0; s < map.Length; s++) {
                    if ((item.WearState & map[s]) != 0) eqSlots[s] = item;
                }

                if (eqTab == 0 && (item.WearState & RoItemEquipLocation.Ammo) != 0) eqSlots[EqAmmoSlot] = item;
            }

            bool anyWorn = false;
            for (int s = 0; s < 10; s++) anyWorn |= eqSlots[s] != null;
            eqUnequipAll.States["normal"] = EqBasic + (anyWorn ? "btn_e_off_a.bmp" : "btn_e_off_c.bmp");
            UpdateClassicPreview(seenAssets);
            classicEquip.Invalidate();
        }

        private void EnsureClassicEquipment() {
            if (classicEquip != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIEquipWnd", WindowId = 10, Width = EqW, Height = EqH };
            layout.Children.Add(EqButton(584, "base", 3, 3, 11, 11, EqBasic + "sys_base_off.bmp", EqBasic + "sys_base_on.bmp", EqBasic + "sys_base_on.bmp"));
            layout.Children.Add(EqButton(EqMiniId, "mini", 252, 3, 11, 11, EqBasic + "sys_mini_off.bmp", EqBasic + "sys_mini_on.bmp", EqBasic + "sys_mini_on.bmp"));
            layout.Children.Add(EqButton(EqCloseId, "close", 266, 3, 11, 11, EqBasic + "sys_close_off.bmp", EqBasic + "sys_close_on.bmp", EqBasic + "sys_close_on.bmp"));
            eqShowEquip = EqButton(EqShowEquipId, "showequip", 10, 172, 10, 10, null, null, null);
            layout.Children.Add(eqShowEquip);
            layout.Children.Add(EqButton(0x242, "ability", 167, 167, 23, 20, EqBasic + "btn_e_ability_a.bmp", EqBasic + "btn_e_ability_b.bmp", EqBasic + "btn_e_ability_b.bmp"));
            layout.Children.Add(EqButton(0x1f8, "swap", 193, 167, 40, 20, EqBasic + "btn_e_change_a.bmp", EqBasic + "btn_e_change_b.bmp", EqBasic + "btn_e_change_b.bmp"));
            eqUnequipAll = EqButton(EqUnequipAllId, "unequipall", 235, 167, 40, 20, EqBasic + "btn_e_off_a.bmp", EqBasic + "btn_e_off_b.bmp", EqBasic + "btn_e_off_b.bmp");
            layout.Children.Add(eqUnequipAll);
            layout.Children.Add(EqButton(EqDsApplyId, "dsapply", 160, 167, 42, 20, EqUi + "btn_apply.bmp", EqUi + "btn_apply_a.bmp", EqUi + "btn_apply_b.bmp"));
            layout.Children.Add(EqButton(EqCloseId, "dsclose", 220, 167, 42, 20, EqUi + "btn_close.bmp", EqUi + "btn_close_a.bmp", EqUi + "btn_close_b.bmp"));

            classicEquip = new RoClassicWindow(system, layout) { Visible = false, DragSourceAt = ClassicEquipDragSource };
            classicEquip.DrawDynamic += DrawClassicEquipment;
            classicEquip.Clicked += OnClassicEquipClicked;
            classicEquip.Pressed += OnClassicEquipPressed;
            classicEquip.DoubleClicked += OnClassicEquipDoubleClicked;
            classicEquip.RightClicked += OnClassicEquipRightClicked;
            classicEquip.Dropped += OnClassicEquipDropped;
            classicEquip.Wheeled += OnClassicEquipWheeled;
            classicEquip.Moved += window => {
                PlayerPrefs.SetInt(EqPrefs + "x", window.Position.x);
                PlayerPrefs.SetInt(EqPrefs + "y", window.Position.y);
            };

            // msg 0x22: EQUIPWNDINFO or (185, 300)
            eqMinimized = PlayerPrefs.GetInt(EqPrefs + "min", 0) != 0;
            Vector2Int screen = system.ClientSize;
            int x = PlayerPrefs.GetInt(EqPrefs + "x", 185), y = PlayerPrefs.GetInt(EqPrefs + "y", 300);
            if (x < 0 || y < 0 || x > screen.x - EqW || y > screen.y - EqH) {
                x = 185;
                y = 300;
            }

            classicEquip.MoveTo(x, y);
            ApplyClassicEquipState();
        }

        private static RoClassicChildLayout EqButton(int id, string path, int x, int y, int w, int h, string normal, string hover, string pressed) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            if (normal != null) {
                button.States["normal"] = normal;
                button.States["hover"] = hover;
                button.States["pressed"] = pressed;
            }

            return button;
        }

        /// <summary>Minimized (title bar only) or full; the "Show Equip" box follows its option.</summary>
        private void ApplyClassicEquipState() {
            classicEquip.SetHeight(EqHeight);
            classicEquip.HitArea = new RectInt(0, 0, EqW, EqHeight);
            string box = EqUi + (showEquipConfig ? "checkbox_1.bmp" : "checkbox_0.bmp");
            foreach (string state in EqButtonStates) eqShowEquip.States[state] = box;
            bool bottom = eqTab <= 1;
            foreach (RoClassicChildLayout child in classicEquip.Layout.Children) {
                if (child.Path == "showequip" || child.Path == "ability" || child.Path == "swap" || child.Path == "unequipall") {
                    classicEquip.SetChildVisible(child, bottom && (child.Path != "showequip" || eqTab == 0));
                } else if (child.Path == "dsapply" || child.Path == "dsclose") {
                    classicEquip.SetChildVisible(child, eqTab == 3 && !eqMinimized);
                }
            }

            PlayerPrefs.SetInt(EqPrefs + "min", eqMinimized ? 1 : 0);
            classicEquip.Invalidate();
        }

        // ---- drawing (OnDraw 0x84ced0) ----

        private void DrawClassicEquipment(RoClassicWindow w) {
            int h = EqH, d = EqBody;
            w.DrawBitmapRaw(0, 0, EqBasic + "titlebar_fix.bmp");
            w.TextOut(17, 4, "Equip", 0, 12, EqWhite);   // MsgStr 0x68
            w.TextOut(16, 3, "Equip", 0, 12, EqBlack);
            if (eqMinimized) return;

            // Panel(0x11) 0x82cc10, then the line right of the tabs
            w.RoundOutline(0, 17, EqW, h - 17, EqPanel);
            w.FillRect(0, 17, EqW, 5, EqPanel);
            w.FillRect(1, 17, EqW - 2, h - 19, EqWhite);
            w.FillRect(2, h - 2, EqW - 4, 1, EqWhite);
            int tabsW = EqTabLabels.Length * (EqTabCell - 1) + 1;
            w.FillRect(tabsW, 34, EqW - tabsW, 1, EqLine);
            w.DrawBitmapRaw(0, d + 17, EqBasic + "equipwin_bg2.bmp");
            DrawClassicEquipTabs(w);
            // the "Show Equip" check box label (MsgStr 0x552) right of its 10×10 box, General tab only
            if (eqTab == 0) w.TextOut(24, 170, "Show Equip", 0, 12, EqBlack);
            if (eqTab == 3) DrawClassicDamageSkins(w);
            if (eqTab == 2) DrawClassicTitles(w);
            if (eqTab > 1) return;   // Title / Damage Indicator: their panels only

            // the slots the dragged inventory item would take
            foreach (int s in eqHighlight) {
                if (s < 0) continue;
                int x = s < 8 ? (s / 4 == 0 ? 4 : 248) : (s % 4 == 0 ? 4 : 248);
                int y = s < 8 ? d + 51 + 26 * (s % 4) : d + 25;
                w.DrawBitmap(x, y, EqBasic + "item_invert.bmp");
            }

            if (eqTab == 1) w.DrawBitmapRaw(0, d + 14, EqBasic + "equipwin_special.bmp");
            for (int s = 0; s < 10; s++) {
                RoItemInstance item = eqSlots[s];
                if (item == null) continue;
                EqSlotRects(s, out Vector2Int icon, out Vector2Int name, out int width);
                if (item.Count == 1 || item.Count == 0) {
                    EqDrawIcon(w, icon.x, icon.y, item);
                    EqDrawName(w, name.x, name.y, width, item);
                }

                if (item.EnchantGrade > 0) w.DrawBitmap(icon.x, icon.y + 12, EqUi + "grade_enchant\\grade_icon" + item.EnchantGrade.ToString(CultureInfo.InvariantCulture) + ".bmp");
            }

            // the character preview: feet at (w/2 + 1, 149); the image has them at (27, 103)
            if (eqPreviewReady && eqPreviewShown != null) w.DrawBitmap(EqW / 2 + 1 - EqPreviewW / 2, 149 - EqPreviewFeetY, eqPreviewShown);
            if (eqTab == 0 && eqSlots[EqAmmoSlot] != null) {
                RoItemInstance ammo = eqSlots[EqAmmoSlot];
                EqDrawIcon(w, EqW / 2 - 25, d + 44, ammo);
                EqDrawName(w, EqW / 2 - 25, d + 68, 40, ammo);
            }
        }

        private void EqDrawIcon(RoClassicWindow w, int x, int y, RoItemInstance item) => EqDrawIconFor(w, x, y, item, model.Metadata);

        internal static void EqDrawIconFor(RoClassicWindow w, int x, int y, RoItemInstance item, RoItemMetadataTable itemMetadata) {
            RoItemMetadata metadata = item.ResolveMetadata(itemMetadata);
            string icon = metadata != null ? EqIcons + metadata.GetResourceName(item.IsIdentified) + ".bmp" : null;
            if (icon == null || !w.DrawBitmap(x, y, icon)) w.DrawBitmap(x, y, EqIcons + "am_berserkpitcher.bmp");
        }

        /// <summary>Icon and name-box positions of a slot (§2e).</summary>
        internal static void EqSlotRects(int s, out Vector2Int icon, out Vector2Int name, out int width) {
            width = 70;
            if (s == 8) { icon = new Vector2Int(6, 38); name = new Vector2Int(33, 45); return; }
            if (s == 9) { icon = new Vector2Int(251, 38); name = new Vector2Int(172, 45); return; }
            if (s == EqAmmoSlot) { icon = new Vector2Int(115, 64); name = new Vector2Int(115, 88); width = 40; return; }
            int r = s % 4;
            icon = s < 4 ? new Vector2Int(6, 64 + 26 * r) : new Vector2Int(251, 64 + 26 * r);
            name = s < 4 ? new Vector2Int(33, 71 + 26 * r) : new Vector2Int(172, 71 + 26 * r);
        }

        /// <summary>
        /// ItemName 0x82dff0 → 0x86a880: font 0 h12 with an #e0e0e0 shadow at (+1, +1), in the item's enchant colour
        /// (#adff2f drawn #669900, black without); one line when it fits, else up to two lines from 6 px higher, 13 px
        /// apart, the second cut with "..." when the rest does not fit.
        /// </summary>
        private void EqDrawName(RoClassicWindow w, int x, int y, int width, RoItemInstance item) => EqDrawNameFor(w, x, y, width, item, model.Metadata);

        internal static void EqDrawNameFor(RoClassicWindow w, int x, int y, int width, RoItemInstance item, RoItemMetadataTable itemMetadata) {
            string name = item.GetFullDisplayName(itemMetadata) ?? string.Empty;
            Color32 color = EqNameColorFor(item, itemMetadata);
            RoClassicGdiFont font = w.FontFor(0, 12);
            if (font.Measure(name) < width) {
                w.TextOut(x + 1, y + 1, name, 0, 12, EqShadow);
                w.TextOut(x, y, name, 0, 12, color);
                return;
            }

            y -= 6;
            int line = 1, pen = 0;
            for (int i = 0; i < name.Length; i++) {
                string ch = name[i].ToString();
                int cw = font.Measure(ch);
                if (line == 2 && pen + cw >= width - 15 && pen + font.Measure(name.Substring(i)) > width) {
                    w.TextOut(x + pen + 1, y + 1, "...", 0, 12, EqShadow);
                    w.TextOut(x + pen, y, "...", 0, 12, color);
                    return;
                }

                w.TextOut(x + pen + 1, y + 1, ch, 0, 12, EqShadow);
                w.TextOut(x + pen, y, ch, 0, 12, color);
                pen += cw;
                if (pen > width) {
                    pen = 0;
                    y += 13;
                    if (++line > 2) return;
                }
            }
        }

        /// <summary>FUN_0065d2e0: the enchant count (cards past the item's own slots) picks the colour.</summary>
        internal static Color32 EqNameColorFor(RoItemInstance item, RoItemMetadataTable itemMetadata) {
            if (!item.IsIdentified) return EqBlack;
            RoItemMetadata metadata = item.ResolveMetadata(itemMetadata);
            int slots = metadata != null ? metadata.SlotCount : 0, enchants = 0;
            for (int i = slots; i < RoItemInstance.MaxCardSlots; i++) {
                if (item.GetCardId(i) > 0) enchants++;
            }

            return EqNameColors[Mathf.Clamp(enchants, 0, EqNameColors.Length - 1)];
        }

        /// <summary>The horizontal tab control (style 1) at (0, 17): 4 equal tabs, labels cut to fit.</summary>
        private void DrawClassicEquipTabs(RoClassicWindow w) {
            RoClassicGdiFont font = w.FontFor(0, 12);
            for (int pass = 0; pass < 2; pass++) {
                for (int i = 0; i < EqTabLabels.Length; i++) {
                    bool selected = i == eqTab;
                    if (selected != (pass == 1)) continue;
                    int x = (EqTabCell - 1) * i, y = 17 + (selected ? 0 : 1);
                    Color32 c = EqLine;
                    w.FillRect(x + 2, y, EqTabCell - 4, 1, c);
                    w.FillRect(x + 1, y + 1, 1, 1, c);
                    w.FillRect(x, y + 2, 1, 15, c);
                    w.FillRect(x + EqTabCell - 2, y + 1, 1, 1, c);
                    w.FillRect(x + EqTabCell - 1, y + 2, 1, 15, c);
                    if (!selected) w.FillRect(x, 17 + 16, EqTabCell, 1, c);
                    string label = EqFit(EqTabLabels[i], EqTabCell - 15, font);
                    int tw = font.Measure(label);
                    w.TextOut(x + (EqTabCell - tw) / 2 + (EqTabCell - tw) % 2, 17 + (selected ? 2 : 3), label, 0, 12, EqBlack);
                }
            }
        }

        internal static string EqFit(string s, int width, RoClassicGdiFont font) {
            if (font.Measure(s) <= width) return s;
            int n = s.Length;
            while (n > 0 && font.Measure(s.Substring(0, n) + "...") > width) n--;
            return s.Substring(0, n) + "...";
        }

        // ---- mouse (§2f) ----

        /// <summary>Icon hit 0x834770: the icon columns (left 5..30, right 251..275) by 26-px rows, plus the ammo rect.</summary>
        private int EqIconSlotAt(Vector2Int p) {
            int d = EqBody;
            if (eqTab == 0 && p.x > EqW / 2 - 25 && p.x < EqW / 2 - 1 && p.y > d + 44 && p.y < d + 68) return EqAmmoSlot;
            if (p.y < d + 17 || p.y > d + 145) return -1;
            int k = (p.y - d - 17) / 26;
            if (p.x > 5 && p.x < 30) return k == 0 ? 8 : k - 1;
            if (p.x >= 251 && p.x < 275) return k == 0 ? 9 : k + 3;
            return -1;
        }

        /// <summary>Hover / right-click hit 0x834f80: the whole slot rows (left 5..106, right 169..w−6).</summary>
        private int EqRowSlotAt(Vector2Int p) {
            int slot = EqIconSlotAt(p);
            if (slot >= 0) return slot;
            int d = EqBody;
            if (p.y < d + 17 || p.y > d + 145) return -1;
            int k = (p.y - d - 17) / 26;
            if (p.x > 5 && p.x < 106) return k == 0 ? 8 : k - 1;
            if (p.x > 169 && p.x < EqW - 6) return k == 0 ? 9 : k + 3;
            return -1;
        }

        private RoItemInstance EqItemAt(int slot) => slot >= 0 && slot < eqSlots.Length && eqTab <= 1 && !eqMinimized ? eqSlots[slot] : null;

        /// <summary>LBtnDown on a worn item: dragged out (released over the inventory = take it off).</summary>
        private IRoClassicDragPayload ClassicEquipDragSource(Vector2Int local) {
            RoItemInstance item = EqItemAt(EqIconSlotAt(local));
            if (item == null) return null;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.shiftKey.isPressed && item.IsIdentified && RoClassicTextInput.HasFocus) {
                RoClassicChat.Instance?.InsertItemLink(item);
            }

            RoItemMetadata metadata = item.ResolveMetadata(model.Metadata);
            return new RoClassicItemPayload {
                Item = item,
                IconPath = metadata != null ? EqIcons + metadata.GetResourceName(item.IsIdentified) + ".bmp" : null,
            };
        }

        private void OnClassicEquipPressed(RoClassicWindow window, Vector2Int local) {
            if (!eqMinimized && eqTab == 3 && PressClassicDamageSkins(local)) return;
            if (!eqMinimized && eqTab == 2 && PressClassicTitles(local)) return;
            // the tab control (0, 17), 4 equal cells
            if (eqMinimized || local.y < 17 || local.y >= 35 || local.x >= EqTabLabels.Length * (EqTabCell - 1) + 1) return;
            int tab = Math.Min(EqTabLabels.Length - 1, local.x / (EqTabCell - 1));
            if (tab == eqTab) return;
            eqTab = tab;
            if (eqTab == 2) RefreshClassicTitles();
            ApplyClassicEquipState();
            Refresh();
        }

        /// <summary>LBtnDblClk: the title bar minimizes; a worn item is taken off.</summary>
        private void OnClassicEquipDoubleClicked(RoClassicWindow window, Vector2Int local) {
            if (local.y < 17) {
                ToggleClassicEquipMinimized();
                return;
            }

            if (!eqMinimized && eqTab == 2) {
                DoubleClickClassicTitles(local);
                return;
            }

            RoItemInstance item = EqItemAt(EqIconSlotAt(local));
            if (item != null) model.Unequip(item.BagIndex);
        }

        private void OnClassicEquipRightClicked(RoClassicWindow window, Vector2Int local) {
            RoItemInstance item = EqItemAt(EqRowSlotAt(local));
            if (item != null) RoClassicItemDescription.Toggle(item, model.Metadata); // window 12, msg 0x18
        }

        /// <summary>An inventory item dropped on the window is put on (where its locations allow).</summary>
        private void OnClassicEquipDropped(RoClassicWindow window, Vector2Int local, IRoClassicDragPayload payload) {
            eqHighlight[0] = eqHighlight[1] = eqHighlight[2] = -1;
            if (!(payload is RoClassicItemPayload carried) || carried.Item == null || carried.Item.IsEquipped || eqTab > 1) return;
            RoItemInstance item = carried.Item;
            int row = EqRowSlotAt(local);
            RoItemEquipLocation target = EqItemAt(row) == null && row >= 0 && row < 10
                ? (eqTab == 1 ? EqCostume : EqGeneral)[row]
                : RoItemEquipLocation.None;
            RoItemEquipLocation allowed = item.ResolveEquipLocations(model.Metadata);
            if (target == RoItemEquipLocation.None || (allowed & target) == 0) target = allowed;
            model.TryEquipAt(item.BagIndex, target);
        }

        private void OnClassicEquipClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case EqCloseId:
                    Hide();
                    break;
                case EqMiniId:
                    ToggleClassicEquipMinimized();
                    break;
                case EqShowEquipId:
                    ToggleShowEquip();
                    break;
                case EqDsApplyId:
                    ApplyClassicDamageSkin();
                    break;
                case 0x242:
                    // the ability button (RUNTIME-RULES-ITEMS §2g): CUIEquipmentPropertiesWnd 0x2711 on / off
                    RoClassicEquipProperties.Toggle();
                    break;
                case EqUnequipAllId:
                    // 0x0bf5 {mask} is one packet in the client; here every worn item of the tab is taken off
                    if (!model.CanSendCommand) break;
                    for (int s = 0; s < 10; s++) {
                        if (eqSlots[s] != null) model.Unequip(eqSlots[s].BagIndex);
                    }

                    break;
            }
        }

        /// <summary>
        /// /eqopen (0xc0a746: mode msg 0xfd with the option flipped → CZ_CONFIG type 0): the same as the window's "Show
        /// Equip" box, also while the window was never opened.
        /// </summary>
        public static void ToggleShowEquipConfig(RoLoginShellSession loginSession) => Ensure(loginSession)?.ToggleShowEquip();

        /// <summary>"Show Equip" (CZ_CONFIG type 0): the box flips at once and flips back when the request does not go out.</summary>
        private async void ToggleShowEquip() {
            if (!model.CanSendCommand) return;
            bool previous = showEquipConfig;
            showEquipConfig = !showEquipConfig;
            if (classicEquip != null) ApplyClassicEquipState();
            if (await model.SetEquipmentConfigAsync(RoInventoryModel.ShowEquipConfigType, showEquipConfig ? 1 : 0)) return;
            showEquipConfig = previous;
            if (classicEquip != null) ApplyClassicEquipState();
        }

        // ---- the Title tab (equip-title-spec: UIListBoxTitle +0x100, RefreshTitleList 0x856e10) ----

        /// <summary>
        /// RefreshTitleList 0x856e10: "Disable Title" (MsgStr 0xa7e, id 0) then the owned titles; the selection goes and the
        /// list scrolls back to the top. Also msg 0x3c (a new owned title, ZC_ACK_CHANGE_TITLE) while the tab is open.
        /// </summary>
        private void RefreshClassicTitles() {
            eqTitleRows.Clear();
            eqTitleRows.Add(0);
            foreach (int id in RoClassicAchievements.OwnedTitles) eqTitleRows.Add(id);
            eqTitleSel = -1;
            eqTitleFirst = 0;
            eqTitleVersion = RoClassicAchievements.TitlesVersion;
            classicEquip?.Invalidate();
        }

        private string ClassicTitleRowText(int row) {
            int id = eqTitleRows[row];
            return id == 0 ? RoClassicUiHost.MsgStr(0xa7e, "Disable Title") : RoClassicAchievements.TitleNameOf(id);
        }

        /// <summary>
        /// OnDraw 0x84ced0, tab 2: the white panel (1, 35, 278×131), the #646464 border round the list, the worn title's
        /// name centred bold (height 13) at y 40; then the list (vf20 0x7ed330): #E1E1E1, 15-px rows (7 visible), the
        /// selection #CADFFB / a "new" title #FAC092 in 14-px bars, black text (height 12) at x 3, the scroll bar at 261.
        /// </summary>
        private void DrawClassicTitles(RoClassicWindow w) {
            if (eqTitleVersion != RoClassicAchievements.TitlesVersion) RefreshClassicTitles();
            w.FillRect(1, EqBody + 15, EqW - 2, 131, EqWhite);
            int bx = EqTitleListX - 1, by = EqTitleListY - 1, bw = EqTitleListW + 2, bh = EqTitleListH + 2;
            w.FillRect(bx, by, bw, 1, EqTitleBorder);
            w.FillRect(bx, by + bh - 1, bw, 1, EqTitleBorder);
            w.FillRect(bx, by, 1, bh, EqTitleBorder);
            w.FillRect(bx + bw - 1, by, 1, bh, EqTitleBorder);
            string current = RoClassicAchievements.TitleNameOf(RoClassicAchievements.CurrentTitle);
            if (!string.IsNullOrEmpty(current)) {
                int tw = w.FontFor(0, 13, true).Measure(current);
                w.TextOut(1 + (EqW - 2 - tw) / 2, EqBody + 20, current, 0, 13, EqBlack, true);
            }

            w.FillRect(EqTitleListX, EqTitleListY, EqTitleListW, EqTitleListH, EqTitleBack);
            int visible = EqTitleListH / EqTitleRowH;
            int last = Math.Min(eqTitleRows.Count, eqTitleFirst + visible);
            for (int i = eqTitleFirst; i < last; i++) {
                int y = EqTitleListY + (i - eqTitleFirst) * EqTitleRowH;
                if (i == eqTitleSel) w.FillRect(EqTitleListX, y, EqTitleListW, EqTitleRowH - 1, EqTitleSelect);
                else if (RoClassicAchievements.IsNewTitle(eqTitleRows[i])) w.FillRect(EqTitleListX, y, EqTitleListW, EqTitleRowH - 1, EqTitleNew);
                w.TextOutColored(EqTitleListX + 3, y, ClassicTitleRowText(i), 0, 12);
            }

            int excess = Math.Max(0, eqTitleRows.Count - visible);
            w.DrawScrollBar(EqTitleListX + EqTitleListW - 13, EqTitleListY, EqTitleListH, excess, eqTitleRows.Count, visible, eqTitleFirst);
        }

        private bool InClassicTitleList(Vector2Int local) =>
            local.x >= EqTitleListX && local.x < EqTitleListX + EqTitleListW - 13 && local.y >= EqTitleListY && local.y < EqTitleListY + EqTitleListH;

        /// <summary>LBtnDown 0x7f1740: the row under the mouse is selected (none below the last row); nothing else.</summary>
        private bool PressClassicTitles(Vector2Int local) {
            if (!InClassicTitleList(local)) return false;
            int row = (local.y - EqTitleListY) / EqTitleRowH + eqTitleFirst;
            eqTitleSel = row < eqTitleRows.Count ? row : -1;
            classicEquip.Invalidate();
            return true;
        }

        /// <summary>
        /// LBtnDblClk 0x7f1360 → the window's msg 6 (id 0x1dc): CZ_REQ_CHANGE_TITLE 0x0a2e {u32 title} at once (0 removes
        /// the title), no confirmation; the client waits for ZC_ACK_CHANGE_TITLE.
        /// </summary>
        private void DoubleClickClassicTitles(Vector2Int local) {
            RoLoginShellSession session = model.Session;
            if (!InClassicTitleList(local) || eqTitleSel < 0 || eqTitleSel >= eqTitleRows.Count || session == null) return;
            _ = session.BeginEntryQueuePacket(0x0a2e, BitConverter.GetBytes(eqTitleRows[eqTitleSel]));
        }

        /// <summary>Wheel 0x7f4020: three rows per notch while the list scrolls.</summary>
        private void WheelClassicTitles(int delta) {
            int visible = EqTitleListH / EqTitleRowH;
            if (delta == 0 || eqTitleRows.Count <= visible) return;
            int first = Mathf.Clamp(eqTitleFirst + (delta < 0 ? 3 : -3), 0, eqTitleRows.Count - visible);
            if (first == eqTitleFirst) return;
            eqTitleFirst = first;
            classicEquip.Invalidate();
        }

        // ---- the Damage Indicator tab (FUN_0082d760; reference packages/damageskin NOTES §5) ----

        /// <summary>
        /// bg_damage under the tabs, four 64×92 cells from the first visible skin (applied = pick, pending = press), each with
        /// its name centred in 64 px (14 bold when applied, else 12; #BABABA shadow at (+1, +1), #323232) and its 36×36
        /// icon; the four direction radios (the box at (0, 1), the label black at (13, 0), as the client's check boxes).
        /// </summary>
        private void DrawClassicDamageSkins(RoClassicWindow w) {
            w.DrawBitmapRaw(0, EqBody + 14, EqDamage + "bg_damage.bmp");
            RoDamageSkinCatalog skins = RoClassicEffectBridge.DamageSkins;
            int applied = RoClassicEffectBridge.DamageSkin;
            for (int k = 0; k < 4; k++) {
                int i = eqDsFirst + k;
                if (i >= skins.Count) break;
                int x = 6 + 68 * k;
                w.DrawBitmapRaw(x, 58, EqDamage + (i == applied ? "btn_damage_pick.bmp" : i == eqDsPending ? "btn_damage_press.bmp" : "btn_damage.bmp"));
                RoDamageSkin skin = skins.Skins[i];
                bool bold = i == applied;
                int height = bold ? 14 : 12;
                string name = skin.DisplayName ?? string.Empty;
                int tx = x + (int) ((64 - w.FontFor(0, height, bold).Measure(name)) * 0.5f);
                w.TextOut(tx + 1, 69, name, 0, height, EqDsNameShadow, bold);
                w.TextOut(tx, 68, name, 0, height, EqDsName, bold);
                if (!string.IsNullOrEmpty(skin.IconName)) w.DrawBitmap(x + 14, 90, skin.IconName + ".bmp");
            }

            for (int r = 0; r < EqDsRadioX.Length; r++) {
                int x = EqDsRadioX[r], y = EqBody + 20;
                w.DrawBitmap(x, y + 1, EqUi + (r == eqDsDirection ? "tipbox\\checkBox_check.bmp" : "tipbox\\checkBox.bmp"));
                w.TextOut(x + 13, y, RoClassicUiHost.MsgStr(EqDsRadioMsg[r], EqDsRadioText[r]), 0, 12, EqBlack);
            }
        }

        /// <summary>LBtnDown on tab 3 (vf31 0x853f10 / radio msg 0xd3): a cell makes its skin pending, a radio the direction.</summary>
        private bool PressClassicDamageSkins(Vector2Int local) {
            for (int r = 0; r < EqDsRadioX.Length; r++) {
                if (local.x >= EqDsRadioX[r] && local.x < EqDsRadioX[r] + 60 && local.y >= EqBody + 20 && local.y < EqBody + 32) {
                    eqDsDirection = r;
                    classicEquip.Invalidate();
                    return true;
                }
            }

            if (local.y < 58 || local.y > 150) return false;
            for (int k = 0; k < 4; k++) {
                if (local.x < 6 + 68 * k || local.x > 70 + 68 * k) continue;
                // the client sets +0x138 without a bound check; past the last skin nothing is drawn, so nothing is picked here
                if (eqDsFirst + k < RoClassicEffectBridge.DamageSkins.Count) {
                    eqDsPending = eqDsFirst + k;
                    classicEquip.Invalidate();
                }

                return true;
            }

            return false;
        }

        /// <summary>vf35 0x856180: the wheel scrolls one skin when there are more than four.</summary>
        private void OnClassicEquipWheeled(RoClassicWindow window, Vector2Int local, int delta) {
            if (!eqMinimized && eqTab == 2) {
                WheelClassicTitles(delta);
                return;
            }

            int count = RoClassicEffectBridge.DamageSkins.Count;
            if (eqMinimized || eqTab != 3 || count <= 4 || delta == 0) return;
            int first = Mathf.Clamp(eqDsFirst + (delta > 0 ? -1 : 1), 0, count - 4);
            if (first == eqDsFirst) return;
            eqDsFirst = first;
            classicEquip.Invalidate();
        }

        /// <summary>Apply 0x10b: the pending skin and direction become the client's (the window stays open).</summary>
        private void ApplyClassicDamageSkin() {
            RoClientSettingsModel settings = RoClassicUiHost.ClientSettings;
            if (settings != null) {
                settings.SetDamageSkin(eqDsPending);
                settings.SetDamageMotion(eqDsDirection);
            } else {
                RoClassicEffectBridge.DamageSkin = eqDsPending;
                RoClassicEffectBridge.DamageDriftMode = eqDsDirection;
            }

            classicEquip?.Invalidate();
        }

        /// <summary>The ctor copies the applied skin and direction: a newly opened window starts from them.</summary>
        private void ResetClassicDamageSkinSelection() {
            eqDsPending = RoClassicEffectBridge.DamageSkin;
            eqDsDirection = RoClassicEffectBridge.DamageDriftMode;
            eqDsFirst = Mathf.Clamp(eqDsPending - 3, 0, Mathf.Max(0, RoClassicEffectBridge.DamageSkins.Count - 4));
        }

        private void ToggleClassicEquipMinimized() {
            eqMinimized = !eqMinimized;
            ApplyClassicEquipState();
        }

        // ---- per frame: tooltip, drag highlight ----

        private void UpdateClassicEquipment() {
            // msg 0x3c (a new owned title, ZC_ACK_CHANGE_TITLE): the open Title tab rebuilds its list and redraws
            if (eqTab == 2 && eqTitleVersion != RoClassicAchievements.TitlesVersion) RefreshClassicTitles();
            RoClassicWindowManager windows = system.Windows;
            if (windows == null) return;
            Vector2Int local = windows.Mouse - classicEquip.Position;
            bool over = windows.HoverWindow == classicEquip;

            // while an inventory item is dragged over the window, the slots it would take are marked
            int h0 = -1, h1 = -1, h2 = -1;
            if (over && windows.Dragging is RoClassicItemPayload dragged && dragged.Item != null && !dragged.Item.IsEquipped && eqTab <= 1) {
                RoItemEquipLocation allowed = dragged.Item.ResolveEquipLocations(model.Metadata);
                RoItemEquipLocation[] map = eqTab == 1 ? EqCostume : EqGeneral;
                for (int s = 0; s < map.Length; s++) {
                    if ((allowed & map[s]) == 0) continue;
                    if (h0 < 0) h0 = s;
                    else if (h1 < 0) h1 = s;
                    else if (h2 < 0) h2 = s;
                }
            }

            if (h0 != eqHighlight[0] || h1 != eqHighlight[1] || h2 != eqHighlight[2]) {
                eqHighlight[0] = h0;
                eqHighlight[1] = h1;
                eqHighlight[2] = h2;
                classicEquip.Invalidate();
            }

            if (over && windows.Dragging == null) {
                RoItemInstance item = EqItemAt(EqRowSlotAt(local));
                if (item != null) {
                    if (!ReferenceEquals(item, tipItem)) {
                        tipItem = item;
                        tipText = EqTooltipTextFor(item, model.Metadata);
                    }

                    int tx = local.x < 106 ? 6 : local.x < 170 ? EqW / 2 - 20 : 175;
                    int ty = (local.y - EqBody - 17) / 24 * 26 + EqBody - 3;
                    RoClassicTooltip.Shared.Show(tipText, EqTooltipColorFor(item, model.Metadata), classicEquip.Position.x + tx, classicEquip.Position.y + ty);
                }
            }
        }

        private const int EqPreviewW = 55, EqPreviewH = 125, EqPreviewFeetY = 103;

        /// <summary>The character standing south, drawn once into an image whenever the worn appearance changes (no per-frame render or read-back).</summary>
        private void UpdateClassicPreview(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized || !model.TryBuildPreviewAppearance(out RoActorAppearanceRequest request, out int hash)) return;
            // FuncDrawWNDChar draws no weapon or shield; the headgears and the garment are the shown tab's items' views
            // (0x834690: top = slot 8, mid = slot 9 unless the top's item, low = slot 0 unless either's, garment = slot 2)
            RoItemInstance top = eqSlots[8], mid = eqSlots[9], low = eqSlots[0];
            if (mid != null && top != null && mid.BagIndex == top.BagIndex) mid = null;
            if (low != null && ((top != null && low.BagIndex == top.BagIndex) || (mid != null && low.BagIndex == mid.BagIndex))) low = null;
            request.weaponId = 0;
            request.shieldId = 0;
            request.headgearTopId = top != null ? top.ViewIdHint : 0;
            request.headgearMidId = mid != null ? mid.ViewIdHint : 0;
            request.headgearLowerId = low != null ? low.ViewIdHint : 0;
            request.robeId = eqSlots[2] != null ? eqSlots[2].ViewIdHint : 0;
            unchecked {
                hash = hash * 397 ^ request.headgearTopId;
                hash = hash * 397 ^ request.headgearMidId * 31;
                hash = hash * 397 ^ request.headgearLowerId * 17;
                hash = hash * 397 ^ request.robeId * 7;
            }

            if (hash == eqPreviewHash) return;
            eqPreviewHash = hash;
            int version = ++eqPreviewVersion;
            string key = EqPreviewKey + "-" + (version & 1).ToString(CultureInfo.InvariantCulture);
            request.motion = RoSpriteMotion.Idle;
            request.facingDirection = RoActorDirection.South;
            bool started = RoClassicActorPortrait.Render(assets, request, key, EqPreviewW, EqPreviewH, EqPreviewFeetY, ok => {
                if (!ok || version != eqPreviewVersion) return;
                eqPreviewShown = key;
                eqPreviewReady = true;
                classicEquip?.Invalidate();
            });
            if (!started) eqPreviewHash = int.MinValue;
        }

        internal static string EqTooltipTextFor(RoItemInstance item, RoItemMetadataTable itemMetadata) {
            string name = item.GetFullDisplayName(itemMetadata);
            int options = 0;
            if (item.IsIdentified && item.HasRandomOptions) {
                foreach (RoItemRandomOption option in item.CopyRandomOptions()) {
                    if (option.Index != 0) options++;
                }
            }

            if (item.Count >= 2) {
                return options > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} [{1} Option] : {2} Quantity", name, options, item.Count)
                    : string.Format(CultureInfo.InvariantCulture, "{0}: {1} ea.", name, item.Count);
            }

            return options > 0 ? string.Format(CultureInfo.InvariantCulture, "{0} [{1} Option]", name, options) : name;   // MsgStr 0xa28
        }

        internal static Color32 EqTooltipColorFor(RoItemInstance item, RoItemMetadataTable itemMetadata) {
            if (!item.IsIdentified) return new Color32(0xaa, 0xaa, 0xaa, 255);
            Color32 c = EqNameColorFor(item, itemMetadata);
            return c.Equals(EqBlack) ? EqWhite : c.Equals(EqNameColors[1]) ? new Color32(0xad, 0xff, 0x2f, 255) : c;
        }

        // ---- diagnostics console (RoSkillSweepRunner "!eq ...") ----

        /// <summary>
        /// Drives the classic equipment window through its handlers: open | close | tab t | off slot (double click on the
        /// slot icon) | wear bagIndex (an inventory item dropped on the window) | min | ds pick k / dir r / apply | state.
        /// </summary>
        internal static string Sweep(RoLoginShellSession loginSession, string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            if (a[0] == "open") Open(loginSession);
            else if (a[0] == "close") instance?.Hide();
            RoClassicEquipment t = instance;
            if (t == null || t.classicEquip == null) return "no classic equipment window";
            return t.SweepClassicEquipment(a);
        }

        private string SweepClassicEquipment(string[] a) {
            int Arg(int i) => i < a.Length ? int.Parse(a[i], CultureInfo.InvariantCulture) : 0;
            switch (a[0]) {
                case "tab":
                    OnClassicEquipPressed(classicEquip, new Vector2Int((EqTabCell - 1) * Arg(1) + 10, 25));
                    break;
                case "off": {
                    EqSlotRects(Arg(1), out Vector2Int icon, out _, out _);
                    OnClassicEquipDoubleClicked(classicEquip, icon + new Vector2Int(10, 10));
                    break;
                }
                case "wear": {
                    RoLoginShellSession session = model.Session;
                    RoGameplayEquipmentSnapshot gameplay = session != null ? session.EquipmentSnapshot() : RoGameplayEquipmentSnapshot.Empty;
                    foreach (RoItemInstance item in gameplay.InventoryItems ?? Array.Empty<RoItemInstance>()) {
                        if (item != null && item.BagIndex == Arg(1)) OnClassicEquipDropped(classicEquip, new Vector2Int(140, 100), new RoClassicItemPayload { Item = item });
                    }

                    break;
                }
                case "min":
                    ToggleClassicEquipMinimized();
                    break;
                case "ds":
                    // ds pick k | ds dir r | ds apply: the Damage Indicator tab's cell k / radio r / Apply button
                    if (a.Length > 1 && a[1] == "pick") OnClassicEquipPressed(classicEquip, new Vector2Int(6 + 68 * Arg(2) + 30, 100));
                    else if (a.Length > 1 && a[1] == "dir") OnClassicEquipPressed(classicEquip, new Vector2Int(EqDsRadioX[Mathf.Clamp(Arg(2), 0, 3)] + 5, EqBody + 25));
                    else if (a.Length > 1 && a[1] == "apply") ApplyClassicDamageSkin();
                    return $"tab={eqTab} pending={eqDsPending} dir={eqDsDirection} first={eqDsFirst} applied={RoClassicEffectBridge.DamageSkin}/{RoClassicEffectBridge.DamageDriftMode} of {RoClassicEffectBridge.DamageSkins.Count}";
            }

            var worn = new System.Text.StringBuilder();
            for (int s = 0; s < eqSlots.Length; s++) {
                if (eqSlots[s] != null) worn.Append(s).Append('=').Append(eqSlots[s].ItemId).Append('#').Append(eqSlots[s].BagIndex).Append(' ');
            }

            return $"open={classicEquip.Visible} tab={eqTab} min={eqMinimized} preview={eqPreviewReady} worn=[{worn.ToString().Trim()}] pos={classicEquip.Position}";
        }
    }
}
