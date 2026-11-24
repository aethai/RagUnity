using System.Globalization;
using System.Threading.Tasks;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// The shortcut bar, the original client's UIShortCutWnd (window id 36), built from its reverse-engineered OnCreate /
    /// OnDraw 0x8908e0 / mouse handlers (reference ui-static ui/RUNTIME-RULES.md §3, positions checked against the
    /// oracle's export), and its two quick slots UIQuikSlotWnd 0x9f (RoClassicHotkeyBar.QuickSlots.cs). The slots, their
    /// use and change, the bar keys and the row count are <see cref="RoHotkeyBarModel"/>'s; the shell binds the bar on
    /// every map entry, RoClassicGameUi ticks it (the hover, a refresh when the session's hotkey sequence moves) and hands
    /// it the bar keys and F12.
    /// </summary>
    public sealed partial class RoClassicHotkeyBar {
        private const string ShortcutRoot = "유저인터페이스\\shortcut\\";
        private const string ItemIconRoot = "유저인터페이스\\item\\";
        private const int ClassicWidth = 290;
        private const int ClassicRowHeight = 33;
        private const int ClassicCloseId = 197;
        private const int ClassicPlusId = 212;
        private const int ClassicMinusId = 213;
        private const int ClassicSetToggleId = 545;
        private const float MetadataRetrySeconds = 1f;
        private const int MaxVisibleRows = RoHotkeyBarModel.MaxVisibleRows;
        private const int SlotsPerRow = RoHotkeyBarModel.SlotsPerRow;
        private const int VisibleSlotCount = RoHotkeyBarModel.VisibleSlotCount;
        private static readonly Color32 ClassicHoverFill = new Color32(0xb4, 0xff, 0xb4, 255);
        private static readonly Color32 ClassicLabelColor = new Color32(0x4a, 0x4a, 0x2f, 255);
        private static readonly Color32 ClassicBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 ClassicWhite = new Color32(255, 255, 255, 255);

        private static RoClassicHotkeyBar instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private readonly RoHotkeyBarModel model = new RoHotkeyBarModel();
        private RoClassicWindow classicBar;
        private RoClassicChildLayout classicMinus;
        private RoClassicChildLayout classicPlus;
        private RoClassicChildLayout classicSetToggle;
        private int seenSequence = int.MinValue;
        private float nextMetadataRetry;
        private bool closed;
        private int classicHoverCol = -1;
        private int classicHoverRow = -1;

        /// <summary>A hotkey slot being dragged inside / out of the bar.</summary>
        private sealed class HotkeySlotPayload : IRoClassicDragPayload {
            public RoClassicHotkeyBar Owner;
            public int Index;
            public RoShortcutKeyEntry Shortcut;
            public string IconPath { get; set; }
        }

        private RoGameplayHotkeySnapshot classicGameplay => model.Gameplay;
        private RoSkillMetadataTable skillMetadata => model.SkillMetadata;
        private RoItemMetadataTable itemMetadata => model.ItemMetadata;
        private int ClassicRows => Mathf.Clamp(model.VisibleRows, 1, MaxVisibleRows);

        /// <summary>Bound since the map entry (until <see cref="Close"/>).</summary>
        public static bool IsBound => instance != null;

        /// <summary>
        /// The shell on a map entry (and the first gameplay window pass): the session and the client settings with the
        /// bar's key bindings; the bar is redrawn at once.
        /// </summary>
        public static void Bind(RoLoginShellSession loginSession, RoClientSettingsModel settings) {
            if (instance == null) instance = new RoClassicHotkeyBar();
            instance.model.Bind(loginSession, settings);
            instance.RefreshNow();
        }

        /// <summary>
        /// Per frame (RoClassicGameUi): the hover cells and tooltips; the slots again when the session's hotkey sequence
        /// moved, or once the skill / item tables are loaded.
        /// </summary>
        public static void Tick() {
            RoClassicHotkeyBar b = instance;
            if (b == null) return;
            if (b.classicBar != null && b.classicBar.Visible) {
                b.UpdateClassicHover();
                b.UpdateQuickSlotHover();
            }

            RoLoginShellSession session = b.model.Session;
            int sequence = session != null ? session.HotkeyWindowChangeSequence : 0;
            bool metadataChanged = false;
            if (b.model.Assets == null && Time.unscaledTime >= b.nextMetadataRetry) {
                b.nextMetadataRetry = Time.unscaledTime + MetadataRetrySeconds;
                metadataChanged = b.model.ResolveMetadata();
            }

            if (sequence != b.seenSequence || metadataChanged) b.RefreshNow();
        }

        /// <summary>Leaving the map (character select, shell off): the bar and the quick slots go, the next entry starts afresh.</summary>
        public static void Close() {
            RoClassicHotkeyBar b = instance;
            if (b == null) return;
            instance = null;
            b.DisposeQuickSlots();
            b.classicBar?.Destroy();
            b.classicBar = null;
        }

        /// <summary>A shortcut bar key pressed this frame (RoClassicGameUi): uses that slot, also while the bar is closed.</summary>
        public static void TryUsePressedHotkey(Keyboard keyboard) {
            RoClassicHotkeyBar b = instance;
            if (b != null && b.model.Session != null && b.model.TryResolvePressedShortcutIndex(out int hotkeyIndex)) {
                b.model.TryUseHotkey(hotkeyIndex);
            }
        }

        /// <summary>
        /// /q1 and /q2 (0xc07540 / 0xc0b640 → hotkey window msg 0x29 with slot 6, 7 or 8): the slot of F7, F8 or F9 of the
        /// first row, also while the bar is closed; an empty slot does nothing.
        /// </summary>
        public static void UseQuickSpellSlot(int hotkeyIndex) {
            RoClassicHotkeyBar b = instance;
            if (b == null || b.model.Session == null || hotkeyIndex < 0 || hotkeyIndex >= b.model.KeyCount) return;
            RoClassicHeldSkill.MouseUse(b.model.Key(hotkeyIndex), b.model.Session);   // 0xc07540 / 0xc0b640
            if (b.model.Key(hotkeyIndex).IsEmpty) return;
            b.model.TryUseHotkey(hotkeyIndex);
        }

        /// <summary>F12 (RoClassicGameUi): the next row count of the bar (0 hides it), which also brings a closed bar back.</summary>
        public static void CycleVisibleRowsFromKey() {
            RoClassicHotkeyBar b = instance;
            if (b == null) return;
            b.closed = false;
            b.classicBar?.CloseTabsSetShow(true);   // ShowWindow(0x24) also shows a bar F11 hid
            b.model.CycleVisibleRows();
            b.ApplyVisibleRows();
        }

        /// <summary>Diagnostics (!hkbar): shows the bar with that many rows (0 hides it).</summary>
        public static void SweepSetVisibleRows(int rows) {
            RoClassicHotkeyBar b = instance;
            if (b == null) return;
            b.closed = false;
            b.model.VisibleRows = rows;
            b.RefreshNow();
        }

        /// <summary>Diagnostics (!hk): stores a bar slot through the server request.</summary>
        public static Task SweepSetShortcut(int slot, RoShortcutKeyEntry shortcut) {
            RoClassicHotkeyBar b = instance;
            return b != null ? b.SetShortcutAsync(slot, shortcut, removeRowDuplicates: false) : Task.CompletedTask;
        }

        /// <summary>Diagnostics: a classic drag released over slot <paramref name="slot"/> (the drop handler a mouse release calls).</summary>
        public static void SweepDropClassicPayload(int slot, IRoClassicDragPayload payload) {
            RoClassicHotkeyBar b = instance;
            if (b?.classicBar == null) return;
            b.OnClassicDropped(b.classicBar, new Vector2Int(16 + 29 * (slot % SlotsPerRow) + 12, 4 + ClassicRowHeight * (slot / SlotsPerRow) + 12), payload);
        }

        /// <summary>The slots from a fresh read of the model.</summary>
        private void RefreshNow() {
            RoRuntimePerfCounters.IncrementUiRefresh("Hotkey");
            RoLoginShellSession session = model.Session;
            seenSequence = session != null ? session.HotkeyWindowChangeSequence : 0;
            model.ResolveMetadata();
            model.Refresh();
            try {
                ApplyClassic();
            } catch (System.Exception e) {
                Debug.LogException(e);
            }
        }

        /// <summary>Stores a bar slot, then redraws from the session.</summary>
        private async Task SetShortcutAsync(int hotkeyIndex, RoShortcutKeyEntry shortcut, bool removeRowDuplicates) {
            await model.SetShortcutAsync(hotkeyIndex, shortcut, removeRowDuplicates);
            if (ReferenceEquals(instance, this)) RefreshNow();
        }

        private void ApplyClassic() {
            EnsureClassicBar();
            ApplyClassicRows();
            classicBar.Visible = !closed && model.VisibleRows > 0;
            classicBar.Invalidate();
            ApplyQuickSlots();
        }

        private void ApplyVisibleRows() {
            if (classicBar == null) return;
            ApplyClassicRows();
            classicBar.Visible = !closed && model.VisibleRows > 0;
            ApplyQuickSlots();
        }

        private void EnsureClassicBar() {
            if (classicBar != null) return;
            RoClassicUiSystem system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
            var layout = new RoClassicWindowLayout {
                Class = "UIShortCutWnd", WindowId = 36, Width = ClassicWidth, Height = ClassicRowHeight * MaxVisibleRows,
                DefaultPosition = new Vector2Int(230, 0), // the client's placement at 1024x768 (x = 230; y = 0)
            };
            classicSetToggle = Button(ClassicSetToggleId, "settoggle", 3, 4, 10, 26, "유저인터페이스\\shortcut_1_bts.bmp", null, null);
            layout.Children.Add(classicSetToggle);
            layout.Children.Add(Button(ClassicCloseId, "close", ClassicWidth - 10, 0, 11, 11, "유저인터페이스\\basic_interface\\sys_close_off.bmp",
                "유저인터페이스\\basic_interface\\sys_close_on.bmp", "유저인터페이스\\basic_interface\\sys_close_on.bmp"));
            classicMinus = Button(ClassicMinusId, "minus", 277, 0, 11, 11, ShortcutRoot + "btn_minus_normal.bmp",
                ShortcutRoot + "btn_minus_over.bmp", ShortcutRoot + "btn_minus_press.bmp");
            classicPlus = Button(ClassicPlusId, "plus", 277, 0, 11, 11, ShortcutRoot + "btn_plus_normal.bmp",
                ShortcutRoot + "btn_plus_over.bmp", ShortcutRoot + "btn_plus_press.bmp");
            layout.Children.Add(classicMinus);
            layout.Children.Add(classicPlus);

            classicBar = new RoClassicWindow(system, layout) {
                FrameDragArea = local => ClassicHitSlot(local.x, local.y, out _, out _) < 0, // slots drag their content
                DragSourceAt = ClassicDragSource,
            };
            classicBar.DrawDynamic += DrawClassicBar;
            classicBar.Clicked += OnClassicBarClicked;
            classicBar.DoubleClicked += (window, local) => {
                int slot = ClassicHitSlot(local.x, local.y, out _, out _);
                if (slot >= 0) RoClassicHeldSkill.MouseUse(model.Key(slot), model.Session);   // UIShortCutWnd 0x891720
                if (slot >= 0 && !model.Key(slot).IsEmpty) model.UseHotkey(slot);
            };
            classicBar.Dropped += OnClassicDropped;
            classicBar.DraggedOutside += (window, payload) => {
                // dragging a slot out of the bar clears it (FUN_00d26370(1, 0, 0, src))
                if (payload is HotkeySlotPayload slot && slot.Owner == this) {
                    _ = SetShortcutAsync(slot.Index, new RoShortcutKeyEntry(0, 0, 0), removeRowDuplicates: false);
                }
            };
        }

        private static RoClassicChildLayout Button(int id, string path, int x, int y, int w, int h, string normal, string hover, string pressed) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            button.States["normal"] = normal;
            if (hover != null) button.States["hover"] = hover;
            if (pressed != null) button.States["pressed"] = pressed;
            return button;
        }

        /// <summary>msg 0xe: 290 × 33·rows, minus at (w − 13, h − 28) only above one row, plus at (w − 13, h − 13).</summary>
        private void ApplyClassicRows() {
            int h = ClassicRowHeight * ClassicRows;
            classicBar.SetHeight(h);
            classicBar.SetChildPosition("minus", ClassicWidth - 11 - 2, h - 11 - 11 - 6);
            classicBar.SetChildPosition("plus", ClassicWidth - 11 - 2, h - 11 - 2);
            classicBar.SetChildVisible(classicMinus, ClassicRows != 1);
            // at four rows the plus shows btn_plus_disable (it still wraps the bar to one row and hides it, as in 0x89ca00)
            classicPlus.States["normal"] = ShortcutRoot + (ClassicRows == MaxVisibleRows ? "btn_plus_disable.bmp" : "btn_plus_normal.bmp");
        }

        /// <summary>
        /// HitSlot 0x87be60: the slot strip is 15 ≤ x &lt; 276, 3 &lt; y &lt; rows·33; row = (y − 4) / 33 (none in the last
        /// 2 px of a row), col = (x − 15) / 29 (none in the 3 px gaps). Returns 9·row + col or −1.
        /// </summary>
        private int ClassicHitSlot(int x, int y, out int col, out int row) {
            col = row = -1;
            if (y <= 3 || y >= ClassicRows * ClassicRowHeight || x < 15 || x >= 276) return -1;
            int r = (y - 4) / ClassicRowHeight;
            if (y > r * ClassicRowHeight + 30) return -1;
            int c = (x - 15) / 29;
            if (x > c * 29 + 41) return -1;
            col = c;
            row = r;
            return SlotsPerRow * r + c;
        }

        private IRoClassicDragPayload ClassicDragSource(Vector2Int local) {
            int slot = ClassicHitSlot(local.x, local.y, out _, out _);
            RoShortcutKeyEntry key = model.Key(slot);
            if (slot < 0 || key.IsEmpty) return null;
            string icon = ClassicIconPath(key, out _);
            return icon == null ? null : new HotkeySlotPayload { Owner = this, Index = slot, Shortcut = key, IconPath = icon };
        }

        private void OnClassicDropped(RoClassicWindow window, Vector2Int local, IRoClassicDragPayload payload) {
            int slot = ClassicHitSlot(local.x, local.y, out _, out _);
            if (slot < 0) return;
            if (payload is HotkeySlotPayload moved && moved.Owner == this && moved.Index >= VisibleSlotCount) {
                // a quick slot copied onto the bar
                _ = SetShortcutAsync(slot, moved.Shortcut, removeRowDuplicates: false);
            } else if (payload is HotkeySlotPayload moved2 && moved2.Owner == this) {
                _ = MoveSlotAsync(slot, moved2.Index, moved2.Shortcut);
            } else if (payload is RoClassicSkillPayload skill) {
                _ = DropSkillAsync(slot, skill.Skill);
            } else if (payload is RoClassicItemPayload item && item.Item != null) {
                // an inventory item dropped on a slot becomes its hotkey
                _ = SetShortcutAsync(slot, RoHotkeyBarModel.BuildShortcutAssignment(isSkill: false, item.Item.ItemId, 0), removeRowDuplicates: true);
            }
        }

        /// <summary>A skill from the skill list: stored at its selected level, the same skill leaving the rest of the row.</summary>
        private async Task DropSkillAsync(int slot, RoSkillHotbarPayload payload) {
            if (!payload.IsSkill) return;
            await model.SetSkillShortcutFromDropAsync(slot, payload.SkillId, payload.SelectedLevel > 0 ? payload.SelectedLevel : payload.Level);
            if (ReferenceEquals(instance, this)) RefreshNow();
        }

        /// <summary>A slot moved inside the bar: the source slot is cleared, then the target stores the entry.</summary>
        private async Task MoveSlotAsync(int targetIndex, int sourceIndex, RoShortcutKeyEntry shortcut) {
            if (targetIndex < 0 || targetIndex >= VisibleSlotCount || sourceIndex < 0 || sourceIndex >= VisibleSlotCount) return;
            if (targetIndex == sourceIndex) return;
            await SetShortcutAsync(sourceIndex, new RoShortcutKeyEntry(0, 0, 0), removeRowDuplicates: false);
            await SetShortcutAsync(targetIndex, shortcut, removeRowDuplicates: false);
        }

        private void OnClassicBarClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case ClassicCloseId:
                    // the bar closes (the keys keep working); F12 brings it back
                    closed = true;
                    classicBar.Visible = false;
                    ApplyQuickSlots();
                    break;
                case ClassicMinusId:
                    if (model.VisibleRows > 1) model.VisibleRows--;
                    ApplyVisibleRows();
                    break;
                case ClassicPlusId:
                    // past four rows the bar goes back to one row and hides (0x89ca00)
                    model.VisibleRows = ClassicRows + 1 > MaxVisibleRows ? 0 : ClassicRows + 1;
                    ApplyVisibleRows();
                    break;
            }
        }

        /// <summary>
        /// The bitmap of a hotkey: an item's inventory icon (only while it is in the inventory, amount = its count), or a
        /// skill's icon (only while learned and the stored level is within the learned one, amount = that level), with
        /// the client's fallback icon am_berserkpitcher.
        /// </summary>
        private string ClassicIconPath(RoShortcutKeyEntry key, out string amount) {
            amount = null;
            if (key.IsEmpty || key.Id == 0) return null;
            if (key.IsSkill) {
                int skillId = unchecked((int) key.Id);
                int learned = RoHotkeyBarModel.ResolveLearnedSkillLevel(classicGameplay, skillId);
                if (learned == 0 || key.Count == 0 || key.Count > learned) return null;
                amount = key.Count.ToString(CultureInfo.InvariantCulture);
                RoSkillMetadata skill = skillMetadata.GetSkillOrFallback(skillId);
                return skill != null && !string.IsNullOrWhiteSpace(skill.IconName) ? ItemIconRoot + skill.IconName + ".bmp" : ItemIconRoot + "am_berserkpitcher.bmp";
            }

            RoItemInstance item = RoHotkeyBarModel.FindInventoryItem(classicGameplay, unchecked((int) key.Id));
            if (item == null) return null;
            if (item.Count != 0) amount = item.Count.ToString(CultureInfo.InvariantCulture);
            RoItemMetadata metadata = item.ResolveMetadata(itemMetadata);
            return metadata != null ? ItemIconRoot + metadata.GetResourceName(item.IsIdentified) + ".bmp" : null;
        }

        /// <summary>
        /// OnDraw 0x8908e0: the frame per row, the hover cell (#b4ffb4, under the icon), each slot's icon with its amount
        /// right-aligned at (x + 27, y + 17) in black on a white outline (fontType 0, height 11), then the row numbers.
        /// </summary>
        private void DrawClassicBar(RoClassicWindow w) {
            int rows = ClassicRows;
            for (int r = 0; r < rows; r++) w.DrawBitmap(0, ClassicRowHeight * r, ShortcutRoot + "shortcut_frame.bmp");
            if (classicHoverCol >= 0 && classicHoverRow >= 0) {
                w.FillRect(16 + 29 * classicHoverCol, 5 + ClassicRowHeight * classicHoverRow, 24, 24, ClassicHoverFill);
            }

            for (int r = 0; r < rows; r++) {
                for (int c = 0; c < SlotsPerRow; c++) {
                    string icon = ClassicIconPath(model.Key(SlotsPerRow * r + c), out string amount);
                    if (icon == null) continue;
                    int x = 16 + 29 * c, y = 4 + ClassicRowHeight * r;
                    if (!w.DrawBitmap(x, y, icon)) w.DrawBitmap(x, y, ItemIconRoot + "am_berserkpitcher.bmp");
                    if (amount != null) w.TextOutRightOutlined(x + 27, y + 17, amount, 0, 11, ClassicBlack, ClassicWhite);
                }
            }

            for (int i = 1; i <= rows; i++) {
                w.TextOut(6, 13 + ClassicRowHeight * (i - 1), i.ToString(CultureInfo.InvariantCulture), 0, 11, ClassicLabelColor);
            }
        }

        /// <summary>OnProcess: the hover cell follows the mouse (the client redraws the bar every tick; here on change).</summary>
        private void UpdateClassicHover() {
            if (classicBar == null || !classicBar.Visible) return;
            RoClassicWindowManager windows = RoClassicUiSystem.Instance != null ? RoClassicUiSystem.Instance.Windows : null;
            int col = -1, row = -1;
            if (windows != null && windows.HoverWindow == classicBar) {
                Vector2Int local = windows.Mouse - classicBar.Position;
                ClassicHitSlot(local.x, local.y, out col, out row);
            }

            if (col >= 0 && row >= 0 && windows != null && windows.Dragging == null) ShowClassicSlotTooltip(SlotsPerRow * row + col, col);
            if (col == classicHoverCol && row == classicHoverRow) return;
            classicHoverCol = col;
            classicHoverRow = row;
            classicBar.Invalidate();
        }

        /// <summary>
        /// vf28 0x892dc0: the key's name, with the item's / skill's name through MsgStr 0xb8 when the slot holds one,
        /// in white at (15 + 29·col, −17) from the bar.
        /// </summary>
        private void ShowClassicSlotTooltip(int slot, int col) {
            RoShortcutKeyEntry key = model.Key(slot);
            string keyName = model.FormatHotkeyLabel(slot), text = keyName;
            if (!key.IsEmpty && key.IsSkill) {
                RoSkillMetadata skill = skillMetadata.GetSkillOrFallback(unchecked((int) key.Id));
                if (ClassicIconPath(key, out _) != null && skill != null) text = keyName + " " + skill.GetDisplayName();
            } else if (!key.IsEmpty) {
                // an item in the inventory: sprintf(MsgStr 0xb8 "%s %s: %d", key, name, amount)
                RoItemInstance item = RoHotkeyBarModel.FindInventoryItem(classicGameplay, unchecked((int) key.Id));
                if (item != null) {
                    string name = item.GetFullDisplayName(itemMetadata), format = null;
                    model.Assets?.Tables?.MsgStringTable?.TryGetValue("184", out format);
                    text = !string.IsNullOrEmpty(format) && format.Contains("%s")
                        ? ReplaceFirst(ReplaceFirst(ReplaceFirst(format.TrimEnd('#'), "%s", keyName), "%s", name), "%d",
                            item.Count.ToString(CultureInfo.InvariantCulture))
                        : keyName + " " + name + ": " + item.Count.ToString(CultureInfo.InvariantCulture);
                }
            }

            RoClassicTooltip.Shared.Show(text, new Color32(255, 255, 255, 255),
                classicBar.Position.x + 15 + 29 * col, classicBar.Position.y - 17);
        }

        private static string ReplaceFirst(string s, string token, string value) {
            int i = s.IndexOf(token, System.StringComparison.Ordinal);
            return i < 0 ? s : s.Substring(0, i) + value + s.Substring(i + token.Length);
        }
    }
}
