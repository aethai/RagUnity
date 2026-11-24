using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The companion skill lists, UISkillListWnd 0x72 (homunculus, mode 0) and 0x7e (mercenary, mode 1)
    /// (codex-ui-research RUNTIME-RULES-COMPANIONS-MAIL.md "Shared skill list"), opened by the info windows' Skill button.
    /// The oracle frame (280 × 216; first at (360, 200)), title 0x3a9 / 0x4ee at (16, 3) over a white (17, 4) pass, the
    /// homunculus footer "Skill Point: %d" (0x11c) at (16, H − 20). OnDraw 0x8e4a30 per row r (y = 32 r): the selection
    /// (104, y + 19, 164, 28) in colorchip(6,2), (10,2) when unlearned or (14,2) without an SP cost; the icon at (44, y + 21);
    /// the name at (112, y + 22) (#787878 when unlearned); "Lv : %d" at (113, y + 34), or for a level-choosing skill
    /// "Lv :" (124), the chosen level "%2d" (149, red when not the top) and "/ %2d" (168); "Sp : %d" or "Passive" (0x11e) at
    /// (212, y + 34). FUN_008eba70: the skill-up button (78, y + 21) while skill points remain and the skill can rise
    /// (CZ_UPGRADE_SKILLLEVEL), the level arrows at (112 / 192, y + 34). Rows are learned or upgradable skills (Msg 0x17).
    /// Use / double-click casts through the target overlay (self skills at once); Close / X close.
    /// </summary>
    public sealed class RoClassicCompanionSkills : IRoClassicUiProducer {
        private const string Layout = "Ragnarok/Generated/OriginalClient/ui/UISkillListWnd";
        private const string SkillIcons = "유저인터페이스\\item\\";
        private const int UseId = 0xb4, CloseId = 0xc5;
        private const int UpFirst = 235, LeftFirst = 249, RightFirst = 256, ButtonRows = 7;
        private const int RowH = 32, ListTop = 17;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private static readonly Color32 Unlearned = new Color32(0x78, 0x78, 0x78, 255);
        private static readonly Color32 SelectedFill = new Color32(0x70, 0x9f, 0xed, 255);
        private static readonly Color32 SelectedUnlearned = new Color32(0xb4, 0xb4, 0xb4, 255);
        private static readonly Color32 SelectedNoSp = new Color32(0x70, 0xd7, 0xed, 255);

        private sealed class List {
            public int Mode;
            public RoClassicWindow Window;
            public readonly List<RoZcSkillInfo> Rows = new List<RoZcSkillInfo>();
            public readonly Dictionary<int, int> ChosenLevel = new Dictionary<int, int>();
            public int Selected = -1;
            public int First;
            public int Seen = -1;
        }

        private static RoClassicCompanionSkills instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private readonly List[] lists = { new List { Mode = 0 }, new List { Mode = 1 } };

        private static RoClassicCompanionSkills Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicCompanionSkills { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>The info windows' Skill button: opens or closes the list of that mode.</summary>
        public static void Toggle(RoLoginShellSession session, int mode) {
            RoClassicCompanionSkills c = Ensure();
            if (c == null || session == null) return;
            c.session = session;
            List l = c.lists[mode];
            if (l.Window != null && l.Window.Visible) {
                l.Window.Visible = false;
                return;
            }

            c.Open(l);
        }

        private void Open(List l) {
            if (l.Window == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layout).text);
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIScrollBar" || child.Class == "UIResizer") child.Visible = false;
                }

                RoClassicWindow w = new RoClassicWindow(system, layout) { Visible = false };
                w.DrawDynamic += win => Draw(l, win);
                w.Clicked += child => Clicked(l, child.Id);
                w.Pressed += (pw, local) => Select(l, local);
                w.DoubleClicked += (pw, local) => {
                    // vf26 0x8e5900: the row's skill is used when it can be
                    if (Select(l, local)) Use(l);
                };
                w.Wheeled += (pw, local, delta) => Scroll(l, delta > 0 ? -1 : 1);
                string prefs = "ro.ui.classic.companionskills." + l.Mode + ".";
                w.Moved += moved => {
                    PlayerPrefs.SetInt(prefs + "x", moved.Position.x);
                    PlayerPrefs.SetInt(prefs + "y", moved.Position.y);
                };
                l.Window = w;
            }

            Vector2Int client = system.ClientSize;
            string p = "ro.ui.classic.companionskills." + l.Mode + ".";
            int x = PlayerPrefs.GetInt(p + "x", 360), y = PlayerPrefs.GetInt(p + "y", 200);
            // 0x907b90: the record must lie on screen, else (360, 200)
            if (x < 0 || y < 0 || x + l.Window.Width > client.x || y + l.Window.Height > client.y) (x, y) = (360, 200);
            l.Window.MoveTo(x, y);
            l.Seen = -1;
            Refresh(l);
            l.Window.Visible = true;
            system.Windows.BringToFront(l.Window);
            l.Window.Invalidate();
        }

        private int VisibleRows(List l) => Math.Max(1, (l.Window.Height - 38) / RowH);

        /// <summary>Msg 0x17: the learned or upgradable skills of the mode, then the buttons for the rows shown.</summary>
        private void Refresh(List l) {
            if (session == null) return;
            (RoZcSkillInfo[] skills, int serial) = session.CompanionSkillsSnapshot(l.Mode);
            if (serial != l.Seen) {
                l.Seen = serial;
                l.Rows.Clear();
                foreach (RoZcSkillInfo s in skills ?? Array.Empty<RoZcSkillInfo>()) {
                    if (s.SkillId != 0 && (s.Level > 0 || s.Upgradable != 0)) l.Rows.Add(s);
                }

                if (l.Selected >= l.Rows.Count) l.Selected = -1;
                l.First = Mathf.Clamp(l.First, 0, Math.Max(0, l.Rows.Count - VisibleRows(l)));
            }

            PlaceButtons(l);
        }

        private int SkillPoints(List l) => l.Mode == 0 && session != null ? session.HomunSnapshot().Homun.SkillPoints : 0;

        /// <summary>FUN_008eba70: the up button and the level arrows of every shown row, the rest parked at x −200.</summary>
        private void PlaceButtons(List l) {
            int rows = VisibleRows(l), points = SkillPoints(l);
            for (int r = 0; r < ButtonRows; r++) {
                int k = l.First + r, y = 0x22 + RowH * r;
                bool shown = r < rows && k < l.Rows.Count;
                RoZcSkillInfo s = shown ? l.Rows[k] : default;
                bool up = shown && points != 0 && s.Upgradable != 0;
                l.Window.SetChildPosition("UISkillListWnd/UIBitmapButton#" + (6 + r), up ? 0x4e : -200, up ? y - 0xd : y - 0x11);
                bool arrows = shown && s.Level > 0 && CanChooseLevel(s.SkillId);
                l.Window.SetChildPosition("UISkillListWnd/UIBitmapButton#" + (13 + r), arrows ? 0x70 : -200, y);
                l.Window.SetChildPosition("UISkillListWnd/UIBitmapButton#" + (20 + r), arrows ? 0xc0 : -200, y);
            }
        }

        private static RoSkillMetadataTable SkillTable() {
            var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            return assets?.GameData?.Skills;
        }

        private static bool CanChooseLevel(int skillId) {
            RoSkillMetadataTable table = SkillTable();
            return table != null && table.TryGetSkill(skillId, out RoSkillMetadata meta) && meta.CanSelectLevel;
        }

        private int ChosenLevel(List l, RoZcSkillInfo s) =>
            l.ChosenLevel.TryGetValue(s.SkillId, out int chosen) && chosen >= 1 && chosen <= s.Level ? chosen : s.Level;

        private void Draw(List l, RoClassicWindow w) {
            string title = l.Mode == 0 ? RoClassicUiHost.MsgStr(0x3a9, "Homunculus Skill List") : RoClassicUiHost.MsgStr(0x4ee, "Mercenary Soldier Skill List");
            w.TextOut(17, 4, title, 0, 12, White);
            w.TextOut(16, 3, title, 0, 12, Black);
            if (l.Mode == 0) w.TextOut(16, w.Height - 20, RoClassicUiHost.MsgStr(0x11c, "Skill Point: %d").Replace("%d", SkillPoints(l).ToString(CultureInfo.InvariantCulture)), 0, 12, Black);
            RoSkillMetadataTable table = SkillTable();
            int rows = VisibleRows(l);
            for (int r = 0; r < rows && l.First + r < l.Rows.Count; r++) {
                int k = l.First + r, y = RowH * r;
                RoZcSkillInfo s = l.Rows[k];
                if (k == l.Selected) w.FillRect(104, y + 19, 164, 28, s.Level == 0 ? SelectedUnlearned : s.SpCost == 0 ? SelectedNoSp : SelectedFill);
                RoSkillMetadata meta = table != null ? table.GetSkillOrFallback(s.SkillId) : null;
                string icon = meta != null && !string.IsNullOrWhiteSpace(meta.IconName) ? meta.IconName : null;
                if (icon == null || !w.DrawBitmap(44, y + 21, SkillIcons + icon + ".bmp")) w.DrawBitmap(44, y + 21, SkillIcons + "am_berserkpitcher.bmp");
                string name = meta != null ? meta.GetDisplayName() : s.Name;
                w.ClipTo(112, y + 17, w.Width - 0x70 - 0x14, 32);
                w.TextOut(112, y + 22, name, 0, 12, s.Level == 0 ? Unlearned : Black);
                w.ResetClip();
                if (s.Level == 0) continue;
                if (CanChooseLevel(s.SkillId)) {
                    int chosen = ChosenLevel(l, s);
                    w.TextOut(124, y + 34, "Lv :", 0, 12, Black);
                    w.TextOut(149, y + 34, chosen.ToString(CultureInfo.InvariantCulture).PadLeft(2), 0, 12, chosen != s.Level ? Red : Black);
                    w.TextOut(168, y + 34, "/ " + s.Level.ToString(CultureInfo.InvariantCulture).PadLeft(2), 0, 12, Black);
                } else {
                    w.TextOut(113, y + 34, "Lv : " + s.Level.ToString(CultureInfo.InvariantCulture), 0, 12, Black);
                }

                string sp = s.Type == 0 ? RoClassicUiHost.MsgStr(0x11e, "Passive") : "Sp : " + s.SpCost.ToString(CultureInfo.InvariantCulture);
                w.TextOut(212, y + 34, sp, 0, 12, Black);
            }

            int maxFirst = Math.Max(0, l.Rows.Count - rows);
            if (maxFirst > 0) w.DrawScrollBar(265, 14, 178, maxFirst, l.Rows.Count, rows, l.First);
        }

        /// <summary>vf25 0x8e6ec0: a press on a row selects it.</summary>
        private bool Select(List l, Vector2Int local) {
            int rows = VisibleRows(l);
            if (local.y < ListTop || local.y >= ListTop + rows * RowH || local.x < 20 || local.x >= l.Window.Width - 20) return false;
            int k = l.First + (local.y - ListTop) / RowH;
            if (k >= l.Rows.Count) return false;
            l.Selected = k;
            l.Window.Invalidate();
            return true;
        }

        private void Scroll(List l, int rows) {
            int first = Mathf.Clamp(l.First + rows, 0, Math.Max(0, l.Rows.Count - VisibleRows(l)));
            if (first == l.First) return;
            l.First = first;
            PlaceButtons(l);
            l.Window.Invalidate();
        }

        private void Clicked(List l, int id) {
            if (id == CloseId) {
                l.Window.Visible = false;
                return;
            }

            if (id == UseId) {
                Use(l);
                return;
            }

            int k;
            if (id >= UpFirst && id < UpFirst + ButtonRows) {
                k = l.First + id - UpFirst;
                if (k < l.Rows.Count) _ = session?.BeginLegacyCompanionSkillUp(l.Rows[k].SkillId);
            } else if (id >= LeftFirst && id < LeftFirst + ButtonRows) {
                k = l.First + id - LeftFirst;
                if (k < l.Rows.Count) l.ChosenLevel[l.Rows[k].SkillId] = Math.Max(1, ChosenLevel(l, l.Rows[k]) - 1);
            } else if (id >= RightFirst && id < RightFirst + ButtonRows) {
                k = l.First + id - RightFirst;
                if (k < l.Rows.Count) l.ChosenLevel[l.Rows[k].SkillId] = Math.Min(l.Rows[k].Level, ChosenLevel(l, l.Rows[k]) + 1);
            }

            l.Window.Invalidate();
        }

        /// <summary>Use 0xb4 / Msg 0x3a: a usable learned skill goes to the target overlay; self skills cast at once.</summary>
        private void Use(List l) {
            if (session == null || l.Selected < 0 || l.Selected >= l.Rows.Count) return;
            RoZcSkillInfo s = l.Rows[l.Selected];
            if (s.Level == 0 || s.Type == 0) return;
            int level = ChosenLevel(l, s);
            RoSkillTargetFlags flags = RoSkillMetadata.FromServerSkillType(s.Type);
            uint self = l.Mode == 0 ? session.HomunSnapshot().Homun.Gid : session.MercSnapshot().Merc.Gid;
            RoSkillTargetFlags explicitTargets = RoSkillTargetFlags.Enemy | RoSkillTargetFlags.Place | RoSkillTargetFlags.Friend | RoSkillTargetFlags.Trap;
            if ((flags & RoSkillTargetFlags.Self) != 0 && (flags & explicitTargets) == 0) {
                // the server points self skills of a companion at the companion itself
                _ = session.BeginLegacyUseSkillOnTarget(s.SkillId, level, unchecked((int) self));
                return;
            }

            RoSkillMetadataTable table = SkillTable();
            string name = table != null ? table.GetSkillOrFallback(s.SkillId)?.GetDisplayName() : s.Name;
            RoSkillTargetSelection.EnsureInScene().Begin(session, s.SkillId, level, name, flags, CanChooseLevel(s.SkillId), Math.Max(1, (int) s.Level));
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            foreach (List l in lists) {
                if (l.Window == null || !l.Window.Visible || session == null) continue;
                (_, int serial) = session.CompanionSkillsSnapshot(l.Mode);
                if (serial != l.Seen) {
                    Refresh(l);
                    l.Window.Invalidate();
                }
            }
        }

        /// <summary>The sweep console: open 0|1 | pick row | up row | left row | right row | use | state.</summary>
        public static string Sweep(string command) {
            RoClassicCompanionSkills c = instance ?? Ensure();
            if (c == null) return "no classic ui";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], out int n) ? n : 0;
            List l = c.lists[0].Window != null && c.lists[0].Window.Visible ? c.lists[0] : c.lists[1];
            switch (a[0]) {
                case "open":
                    if (c.session == null) c.session = RoClassicCompanion.CurrentSession;
                    Toggle(c.session, Arg(1));
                    l = c.lists[Arg(1) == 1 ? 1 : 0];
                    break;
                case "pick":
                    if (Arg(1) < l.Rows.Count) {
                        l.Selected = Arg(1);
                        l.Window?.Invalidate();
                    }

                    break;
                case "up": c.Clicked(l, UpFirst + Arg(1) - l.First); break;
                case "left": c.Clicked(l, LeftFirst + Arg(1) - l.First); break;
                case "right": c.Clicked(l, RightFirst + Arg(1) - l.First); break;
                case "use": c.Use(l); break;
            }

            var s = new System.Text.StringBuilder();
            s.Append("mode=").Append(l.Mode).Append(" visible=").Append(l.Window != null && l.Window.Visible).Append(" selected=").Append(l.Selected)
                .Append(" points=").Append(c.session != null ? c.SkillPoints(l) : 0).Append(" rows=");
            foreach (RoZcSkillInfo row in l.Rows) s.Append(row.SkillId).Append(" lv").Append(row.Level).Append(" sp").Append(row.SpCost).Append(" up").Append(row.Upgradable).Append(" t").Append(row.Type).Append("; ");
            return s.ToString().TrimEnd();
        }
    }
}
