using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UIQuikSlotWnd 0x9f (session-windows-spec §3): the two quick slots, hotkey indices 36 and 37 (Hercules MAX_HOTKEYS 38
    /// = 4 bars of 9 + 2 quick slots), shown and hidden with the shortcut bar. basic_interface\quickslot.bmp 77 × 34 at the
    /// saved place, default (W − 124, 0); slot i at (5 + 29i, 5), the hover cell #B4FFB4, an item's icon with its inventory
    /// count (black, h11) and grade, a skill's icon with its level outlined; the strip x ≥ 60 drags the window. Double-click
    /// uses the slot, right-click describes it, a drop from the inventory / skill list / bar stores it (removing the same
    /// entry from the other slot) with CZ_SHORTCUT_KEY_CHANGE2 index 36 + i; dragging a slot out clears it.
    /// </summary>
    public sealed partial class RoClassicHotkeyBar {
        private const int QuickSlotBase = RoHotkeyBarModel.QuickSlotBase;
        private const string QuickPrefsX = "ro.ui.classic.quickslot.x", QuickPrefsY = "ro.ui.classic.quickslot.y";
        private static readonly Color32 QuickSkillDescribeFrame = new Color32(0x6b, 0x6b, 0x6b, 255);
        private RoClassicWindow quickSlots;
        private RoClassicWindow quickDescribe;
        private int quickDescribedSkill;
        private int quickHover = -1;

        /// <summary>The quick slot under a window point (vf28 / vf25 hit test): y &lt; 24, x &lt; 59, i = (x − 4) / 29, x ≤ 29i + 30.</summary>
        private static int QuickSlotAt(Vector2Int p) {
            if (p.y < 0 || p.y >= 24 || p.x < 4 || p.x >= 59) return -1;
            int i = (p.x - 4) / 29;
            return i <= 1 && p.x <= 29 * i + 30 ? i : -1;
        }

        private void EnsureQuickSlots() {
            if (quickSlots != null) return;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            var layout = new RoClassicWindowLayout { Class = "UIQuikSlotWnd", WindowId = 0x9f, Width = 77, Height = 34 };
            quickSlots = new RoClassicWindow(system, layout) {
                Visible = false,
                FrameDragArea = p => p.x >= 60,
                DragSourceAt = p => {
                    int i = QuickSlotAt(p);
                    RoShortcutKeyEntry key = model.Key(QuickSlotBase + i);
                    if (i < 0 || key.IsEmpty) return null;
                    string icon = ClassicIconPath(key, out _);
                    return icon == null ? null : new HotkeySlotPayload { Owner = this, Index = QuickSlotBase + i, Shortcut = key, IconPath = icon };
                },
            };
            Vector2Int client = system.ClientSize;
            int x = PlayerPrefs.GetInt(QuickPrefsX, client.x - 124), y = PlayerPrefs.GetInt(QuickPrefsY, 0);
            // msg 0x22: an off-screen saved rect goes back to the default
            if (x < 0 || y < 0 || x + 77 > client.x || y + 34 > client.y) (x, y) = (client.x - 124, 0);
            quickSlots.MoveTo(x, y);
            quickSlots.Moved += w => {
                PlayerPrefs.SetInt(QuickPrefsX, w.Position.x);
                PlayerPrefs.SetInt(QuickPrefsY, w.Position.y);
            };
            quickSlots.DrawDynamic += DrawQuickSlots;
            quickSlots.DoubleClicked += (w, p) => {
                // SendMsg(0x29, i): use, by the shortcut bar's rules
                int i = QuickSlotAt(p);
                if (i >= 0) RoClassicHeldSkill.MouseUse(model.Key(QuickSlotBase + i), model.Session);   // UIQuikSlotWnd 0x891440
                if (i >= 0 && !model.Key(QuickSlotBase + i).IsEmpty && ClassicIconPath(model.Key(QuickSlotBase + i), out _) != null) model.UseHotkey(QuickSlotBase + i);
            };
            quickSlots.RightClicked += (w, p) => QuickDescribe(QuickSlotAt(p));
            quickSlots.Dropped += (w, p, payload) => {
                int i = QuickSlotAt(p);
                if (i < 0) return;
                if (payload is HotkeySlotPayload moved && moved.Owner == this) {
                    if (moved.Index == QuickSlotBase + i) return;
                    if (moved.Index < QuickSlotBase) _ = QuickStore(i, moved.Shortcut);
                    else _ = QuickSwap(moved.Index, QuickSlotBase + i, moved.Shortcut);
                } else if (payload is RoClassicSkillPayload skill && skill.Skill.IsSkill) {
                    int level = skill.Skill.SelectedLevel > 0 ? skill.Skill.SelectedLevel : skill.Skill.Level;
                    _ = QuickStore(i, RoHotkeyBarModel.BuildShortcutAssignment(isSkill: true, skill.Skill.SkillId, System.Math.Max(1, level)));
                } else if (payload is RoClassicItemPayload item && item.Item != null) {
                    _ = QuickStore(i, RoHotkeyBarModel.BuildShortcutAssignment(isSkill: false, item.Item.ItemId, 0));
                }
            };
            quickSlots.DraggedOutside += (w, payload) => {
                if (payload is HotkeySlotPayload slot && slot.Owner == this && slot.Index >= QuickSlotBase) _ = QuickSet(slot.Index, new RoShortcutKeyEntry(0, 0, 0));
            };
        }

        /// <summary>0xd26260: the same entry leaves the other slot, then this slot stores it.</summary>
        private async System.Threading.Tasks.Task QuickStore(int i, RoShortcutKeyEntry entry) {
            int other = QuickSlotBase + (1 - i);
            RoShortcutKeyEntry o = model.Key(other);
            if (!o.IsEmpty && o.IsSkill == entry.IsSkill && o.Id == entry.Id) await QuickSet(other, new RoShortcutKeyEntry(0, 0, 0));
            await QuickSet(QuickSlotBase + i, entry);
        }

        private async System.Threading.Tasks.Task QuickSwap(int from, int to, RoShortcutKeyEntry entry) {
            await QuickSet(from, new RoShortcutKeyEntry(0, 0, 0));
            await QuickSet(to, entry);
        }

        private async System.Threading.Tasks.Task QuickSet(int index, RoShortcutKeyEntry entry) {
            try {
                await model.SetSlotAsync(index, entry);
            } catch (System.Exception e) {
                Debug.LogWarning("[quickslot] " + e.Message);
            }

            if (ReferenceEquals(instance, this)) RefreshNow();
        }

        /// <summary>Shown with the shortcut bar (mgr msg 0x24: MakeWindow(0x9f); SetShow(param)).</summary>
        private void ApplyQuickSlots() {
            EnsureQuickSlots();
            if (quickSlots == null) return;
            bool show = classicBar != null && classicBar.Visible;
            if (show) quickSlots.CloseTabsSetShow(true);   // the slots follow the bar, also out of F11's hiding
            if (quickSlots.Visible != show) quickSlots.Visible = show;
            quickSlots.Invalidate();
        }

        private void DisposeQuickSlots() {
            quickSlots?.Destroy();
            quickDescribe?.Destroy();
            quickSlots = quickDescribe = null;
        }

        /// <summary>OnDraw 0x88e6b0.</summary>
        private void DrawQuickSlots(RoClassicWindow w) {
            w.DrawBitmap(0, 0, "유저인터페이스\\basic_interface\\quickslot.bmp");
            if (quickHover >= 0) w.FillRect(5 + 29 * quickHover, 5, 24, 24, ClassicHoverFill);
            for (int i = 0; i < 2; i++) {
                RoShortcutKeyEntry key = model.Key(QuickSlotBase + i);
                string icon = ClassicIconPath(key, out string amount);
                if (icon == null) continue;
                int x = 5 + 29 * i;
                if (!w.DrawBitmap(x, 5, icon)) w.DrawBitmap(x, 5, ItemIconRoot + "am_berserkpitcher.bmp");
                if (key.IsSkill) {
                    // the level outlined: white at ±1, black over it, at (x + 14, 20)
                    if (amount != null && key.Count != 0) {
                        int right = x + 14 + w.FontFor(0, 11).Measure(amount);
                        w.TextOutRightOutlined(right, 20, amount, 0, 11, ClassicBlack, ClassicWhite);
                    }
                } else {
                    RoItemInstance item = RoHotkeyBarModel.FindInventoryItem(classicGameplay, unchecked((int) key.Id));
                    if (item != null) {
                        w.TextOut(x + 16, 25, item.Count.ToString(CultureInfo.InvariantCulture), 0, 11, ClassicBlack);
                        if (item.EnchantGrade > 0) w.DrawBitmap(x, 17, "유저인터페이스\\grade_enchant\\grade_icon" + item.EnchantGrade.ToString(CultureInfo.InvariantCulture) + ".bmp");
                    }
                }
            }
        }

        /// <summary>vf28: the hover cell and the tooltip at (slot x, −17): an item "%s: %d ea." (MsgStr 0xb7), a skill its name and level.</summary>
        private void UpdateQuickSlotHover() {
            if (quickSlots == null || !quickSlots.Visible) return;
            RoClassicWindowManager windows = RoClassicUiSystem.Instance != null ? RoClassicUiSystem.Instance.Windows : null;
            int i = windows != null && windows.HoverWindow == quickSlots && windows.Dragging == null ? QuickSlotAt(windows.Mouse - quickSlots.Position) : -1;
            if (i >= 0) {
                RoShortcutKeyEntry key = model.Key(QuickSlotBase + i);
                string text = null;
                if (!key.IsEmpty && ClassicIconPath(key, out _) != null) {
                    if (key.IsSkill) {
                        RoSkillMetadata skill = skillMetadata.GetSkillOrFallback(unchecked((int) key.Id));
                        text = (skill != null ? skill.GetDisplayName() : key.Id.ToString(CultureInfo.InvariantCulture)) + " Lv " + key.Count.ToString(CultureInfo.InvariantCulture);
                    } else {
                        RoItemInstance item = RoHotkeyBarModel.FindInventoryItem(classicGameplay, unchecked((int) key.Id));
                        if (item != null) {
                            string format = RoClassicUiHost.MsgStr(0xb7, "%s: %d ea.");
                            text = ReplaceFirst(ReplaceFirst(format, "%s", item.GetFullDisplayName(itemMetadata)), "%d", item.Count.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }

                if (text != null) RoClassicTooltip.Shared.Show(text, ClassicWhite, quickSlots.Position.x + 5 + 29 * i, quickSlots.Position.y - 17);
            }

            if (i == quickHover) return;
            quickHover = i;
            quickSlots.Invalidate();
        }

        /// <summary>Right click: an item opens its description (MakeWindow 0xc), a skill toggles its description (0x2e) at the cursor.</summary>
        private void QuickDescribe(int i) {
            if (i < 0) return;
            RoShortcutKeyEntry key = model.Key(QuickSlotBase + i);
            if (key.IsEmpty || ClassicIconPath(key, out _) == null) return;
            if (!key.IsSkill) {
                RoItemInstance item = RoHotkeyBarModel.FindInventoryItem(classicGameplay, unchecked((int) key.Id));
                if (item != null) RoClassicItemDescription.Toggle(item, itemMetadata);
                return;
            }

            int skillId = unchecked((int) key.Id);
            if (quickDescribe != null && quickDescribe.Visible && quickDescribedSkill == skillId) {
                quickDescribe.Visible = false;
                return;
            }

            RoSkillMetadata metadata = skillMetadata.GetSkillOrFallback(skillId);
            if (metadata == null) return;
            if (quickDescribe == null) {
                var asset = Resources.Load<TextAsset>("Ragnarok/Generated/OriginalClient/ui/UISkillDescribeWnd");
                if (asset == null) return;
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
                layout.Ops.Clear();
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UITextViewer") {
                        child.Fields["text"] = "Describe";
                        if (child.Props != null) child.Props["wrapBytes"] = 45;
                    } else if (child.Class == "UIBitmapButton") {
                        child.Path = "close";
                    }
                }

                quickDescribe = new RoClassicWindow(RoClassicUiSystem.Instance, layout) { Visible = false };
                quickDescribe.DrawUnderlay += w => w.FillRect(0, 0, w.Width, w.Height, new Color32(0, 0, 0, 0x66));
                quickDescribe.DrawDynamic += w => {
                    w.RoundOutline(0, 0, w.Width, w.Height, QuickSkillDescribeFrame);
                    w.RoundOutline(2, 2, w.Width - 4, w.Height - 4, ClassicWhite);
                };
                quickDescribe.Clicked += child => quickDescribe.Visible = false;
                quickDescribe.EscClose = () => {
                    quickDescribe.Visible = false;
                    return true;
                };
            }

            quickDescribedSkill = skillId;
            string text = (metadata.HasDescription ? metadata.DescriptionText : metadata.GetDisplayName()).Replace("\r", string.Empty);
            quickDescribe.Set("Describe", text);
            var lines = new List<(int, int)>();
            RoClassicWindow.WrapBytes(text, 45, lines);
            int h = lines.Count * 18 + 10;
            quickDescribe.SetHeight(h);
            foreach (RoClassicChildLayout child in quickDescribe.Layout.Children) {
                if (child.Class == "UITextViewer") child.H = h - 6;
            }

            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            Vector2Int mouse = system.Windows.Mouse, client = system.ClientSize;
            int x = mouse.x + 24, y = mouse.y;
            if (x + quickDescribe.Width > client.x) x -= 48 + quickDescribe.Width;
            if (y + quickDescribe.Height > client.y) y -= quickDescribe.Height;
            quickDescribe.MoveTo(x, System.Math.Max(0, y));
            quickDescribe.Visible = true;
            system.Windows.BringToFront(quickDescribe);
            system.Windows.PushEsc(quickDescribe);
        }

        /// <summary>Diagnostics: "drop i item id | drop i skill id lv | use i | clear i | describe i | state".</summary>
        public static string SweepQuickSlots(string command) {
            RoClassicHotkeyBar b = instance;
            return b != null ? b.SweepQuick(command) : "no bar";
        }

        private string SweepQuick(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int k) => k < a.Length ? int.Parse(a[k], CultureInfo.InvariantCulture) : 0;
            switch (a[0]) {
                case "drop":
                    _ = QuickStore(Arg(1), a.Length > 2 && a[2] == "skill"
                        ? RoHotkeyBarModel.BuildShortcutAssignment(isSkill: true, Arg(3), System.Math.Max(1, Arg(4)))
                        : RoHotkeyBarModel.BuildShortcutAssignment(isSkill: false, Arg(3), 0));
                    break;
                case "use": model.UseHotkey(QuickSlotBase + Arg(1)); break;
                case "clear": _ = QuickSet(QuickSlotBase + Arg(1), new RoShortcutKeyEntry(0, 0, 0)); break;
                case "describe": QuickDescribe(Arg(1)); break;
            }

            string Slot(int i) {
                RoShortcutKeyEntry k = model.Key(QuickSlotBase + i);
                return k.IsEmpty ? "-" : (k.IsSkill ? "skill " : "item ") + k.Id + "x" + k.Count + (ClassicIconPath(k, out _) == null ? "(hidden)" : string.Empty);
            }

            return "quick=" + (quickSlots != null && quickSlots.Visible) + (quickSlots != null ? " at " + quickSlots.Position : string.Empty) +
                " keys=" + model.KeyCount + " [0]=" + Slot(0) + " [1]=" + Slot(1) + " describe=" + (quickDescribe != null && quickDescribe.Visible);
        }
    }
}
