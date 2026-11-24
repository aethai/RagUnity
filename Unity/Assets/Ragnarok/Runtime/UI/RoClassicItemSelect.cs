using System;
using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The 200 × 200 UIItemIdentifyWnd family (codex-ui-research RUNTIME-RULES-ITEM-SERVICES.md
    /// "identify-composition-repair" and "production-arrow-spell"): Item Appraisal 0x49 (title MsgStr 0x209), Insert Card
    /// 0x4a (0x20a), the arrow / converter / spellbook list UIMakingArrowListWnd 0x5e (literal "LIST") and the Auto Spell
    /// list UISpellListWnd 0x63 (0x2b9 "Spell List"). Four 32 px rows from y 34: an item's icon at x 8 and name at x 40,
    /// a skill's icon at x 8 and name at (40, y + 4); the selection from (5, y − 3) to W − 24; the scrollbar (185, 34, 13, 145);
    /// OK / Cancel. A row click selects (elsewhere clears), a double-click is OK, the wheel scrolls; OK needs a selection.
    /// 0x49 / 0x4a keep their position (ITEMIDENTIFYWNDINFO / ITEMCOMPOSITIONWNDINFO); every one first opens at
    /// ((W − 640) / 2 + 200, H·200/480). Fed by the legacy selectors (identify 0x0177, cards 0x017b, arrows 0x01ad) and,
    /// for spells, ZC_AUTOSPELLLIST 0x0afb (FUN_00c8c650: only positive ids; OK sends CZ_SELECTAUTOSPELL, Cancel skill 0).
    /// </summary>
    public sealed class RoClassicItemSelect : IRoClassicUiProducer {
        public const int Identify = 0x49;
        public const int Composition = 0x4a;
        public const int MakingArrow = 0x5e;
        public const int SpellList = 0x63;
        public const int PetEgg = 0x5a;
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string Prefs = "ro.ui.classic.itemselect.";
        private const string SkillIcons = "유저인터페이스\\item\\";
        private const int OkId = 0xb4;
        private const int CancelId = 0xb5;
        private const int RowH = 32;
        private const int Rows = 4;
        private const int Top = 34;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Selected = new Color32(0xca, 0xdf, 0xfb, 255);

        private sealed class Selector {
            public int Id;
            public RoClassicWindow Window;
            public readonly List<RoItemInstance> Items = new List<RoItemInstance>();
            public readonly List<int> Skills = new List<int>();
            public RoItemMetadataTable Metadata = RoItemMetadataTable.Empty;
            public int Selected = -1;
            public int First;
            public int Sequence = int.MinValue;
            public int Answered = int.MinValue;
            public Action<int> Ok;
            public Action Cancel;
            public int Count => Id == SpellList ? Skills.Count : Items.Count;
        }

        private static RoClassicItemSelect instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private readonly Dictionary<int, Selector> selectors = new Dictionary<int, Selector>();
        private RoLoginShellSession spellSession;
        private int seenSpells = -1;

        private static RoClassicItemSelect Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicItemSelect { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>
        /// Shows (or keeps) the selector for an item list; a new <paramref name="sequence"/> rebuilds it, an answered one stays
        /// closed while its snapshot lingers. False while the classic UI is off (the legacy window shows it).
        /// </summary>
        internal static bool Show(int id, int sequence, IReadOnlyList<RoItemInstance> items, RoItemMetadataTable metadata, Action<RoItemInstance> ok, Action cancel) {
            RoClassicItemSelect s = Ensure();
            if (s == null) return false;
            Selector sel = s.Get(id);
            sel.Ok = row => ok?.Invoke(sel.Items[row]);
            sel.Cancel = cancel;
            sel.Metadata = metadata ?? RoItemMetadataTable.Empty;
            if (sequence == sel.Answered) return true;
            if (sel.Sequence != sequence || !sel.Window.Visible) {
                sel.Sequence = sequence;
                sel.Items.Clear();
                foreach (RoItemInstance item in items ?? Array.Empty<RoItemInstance>()) {
                    if (item != null) sel.Items.Add(item);
                }

                s.Open(sel);
            }

            return true;
        }

        internal static void Hide(int id) {
            if (instance != null && instance.selectors.TryGetValue(id, out Selector sel) && sel.Window.Visible) {
                sel.Window.Visible = false;
                sel.Sequence = int.MinValue;
            }
        }

        /// <summary>ZC_AUTOSPELLLIST: a new list with a positive id opens 0x63, rebuilt from scratch (FUN_00c8c650).</summary>
        internal static void TickSpells(RoLoginShellSession session) {
            if (session == null) return;
            RoClassicItemSelect s = Ensure();
            if (s == null) return;
            (int[] skills, int serial) = session.AutoSpellSnapshot();
            if (!ReferenceEquals(s.spellSession, session)) {
                s.spellSession = session;
                s.seenSpells = serial;
            }

            if (serial == s.seenSpells) return;
            s.seenSpells = serial;
            s.OpenSpells(session, skills, serial);
        }

        /// <summary>A new ZC_AUTOSPELLLIST: the spell selector with its skills, or hidden for an empty list.</summary>
        private void OpenSpells(RoLoginShellSession session, int[] skills, int serial) {
            Selector sel = Get(SpellList);
            sel.Skills.Clear();
            foreach (int skill in skills ?? Array.Empty<int>()) {
                if (skill > 0) sel.Skills.Add(skill);
            }

            if (sel.Skills.Count == 0) {
                sel.Window.Visible = false;
                return;
            }

            sel.Sequence = serial;
            sel.Ok = row => _ = session.BeginLegacySelectAutoSpell(sel.Skills[row]);
            sel.Cancel = () => _ = session.BeginLegacySelectAutoSpell(0);
            Open(sel);
        }

        private Selector Get(int id) {
            if (selectors.TryGetValue(id, out Selector sel)) return sel;
            sel = new Selector { Id = id };
            // the identify / composition captures are the same frame; the arrow and spell lists have their own
            string file = id == MakingArrow ? "UIMakingArrowListWnd" : id == SpellList ? "UISpellListWnd" : id == PetEgg ? "UIPetEggListWnd" : "UIItemIdentifyWnd";
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + file).text);
            layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UIScrollBar") child.Visible = false;
            }

            RoClassicWindow window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += w => Draw(sel, w);
            window.Clicked += child => {
                if (child.Id == OkId) Ok(sel);
                else if (child.Id == CancelId) Cancel(sel);
            };
            window.Pressed += (w, local) => Select(sel, local);
            window.DoubleClicked += (w, local) => {
                // vf26 0x853560: the row under the cursor, then msg 6 (OK)
                if (Select(sel, local)) Ok(sel);
            };
            window.Wheeled += (w, local, delta) => Scroll(sel, delta > 0 ? -1 : 1);
            if (Persists(id)) {
                window.Moved += w => {
                    PlayerPrefs.SetInt(Prefs + id + ".x", w.Position.x);
                    PlayerPrefs.SetInt(Prefs + id + ".y", w.Position.y);
                };
            }

            sel.Window = window;
            selectors[id] = sel;
            return sel;
        }

        private static bool Persists(int id) => id == Identify || id == Composition;

        private void Open(Selector sel) {
            sel.Selected = -1;
            sel.First = 0;
            Vector2Int client = system.ClientSize;
            int x = Persists(sel.Id) ? PlayerPrefs.GetInt(Prefs + sel.Id + ".x", int.MinValue) : int.MinValue;
            int y = Persists(sel.Id) ? PlayerPrefs.GetInt(Prefs + sel.Id + ".y", int.MinValue) : int.MinValue;
            // the saved record must be wholly on screen (0x907160), else the factory place
            if (x < 0 || y < 0 || x > client.x - 200 || y > client.y - 200) {
                x = (client.x - 640) / 2 + 200;
                y = client.y * 200 / 480;
            }

            sel.Window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - 200)), Mathf.Clamp(y, 0, Math.Max(0, client.y - 200)));
            sel.Window.Visible = true;
            system.Windows.BringToFront(sel.Window);
            sel.Window.Invalidate();
        }

        private static string Title(int id) {
            switch (id) {
                case Composition: return RoClassicUiHost.MsgStr(0x20a, "Insert Card");
                case MakingArrow: return "LIST";
                case SpellList: return RoClassicUiHost.MsgStr(0x2b9, "Spell List");
                case PetEgg: return RoClassicUiHost.MsgStr(0x257, "Pet List");
                default: return RoClassicUiHost.MsgStr(0x209, "Item Appraisal");
            }
        }

        /// <summary>OnDraw 0x84e540 (items) / 0x8501d0 (skills): the title, the visible rows and the scrollbar.</summary>
        private static void Draw(Selector sel, RoClassicWindow w) {
            string title = Title(sel.Id);
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            RoSkillMetadataTable skills = sel.Id == SpellList ? SkillTable() : null;
            for (int r = 0; r < Rows && sel.First + r < sel.Count; r++) {
                int k = sel.First + r, y = Top + RowH * r;
                if (k == sel.Selected) w.FillRect(5, y - 3, w.Width - 24 - 5, RowH, Selected);
                if (sel.Id == SpellList) {
                    RoSkillMetadata skill = skills?.GetSkillOrFallback(sel.Skills[k]);
                    string icon = skill != null && !string.IsNullOrWhiteSpace(skill.IconName) ? skill.IconName : null;
                    if (icon == null || !w.DrawBitmap(8, y, SkillIcons + icon + ".bmp")) w.DrawBitmap(8, y, SkillIcons + "am_berserkpitcher.bmp");
                    string name = skill != null ? skill.GetDisplayName() : "#" + sel.Skills[k];
                    w.TextOut(40, y + 4, name, 0, 12, Black);
                    continue;
                }

                RoItemInstance item = sel.Items[k];
                RoClassicItemText.DrawIcon(w, 8, y, item, sel.Metadata, 0);
                RoClassicItemText.DrawName(w, 40, y, w.Width - 24 - 40, item, sel.Metadata);
            }

            w.DrawScrollBar(185, Top, 145, Math.Max(0, sel.Count - Rows), sel.Count, Rows, sel.First);
        }

        private static RoSkillMetadataTable SkillTable() {
            var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            return assets?.GameData?.Skills;
        }

        /// <summary>vf25 0x853c50: x in (3, W − 14], y in the four rows selects; elsewhere clears.</summary>
        private static bool Select(Selector sel, Vector2Int local) {
            int r = local.y > Top - 3 && local.y < Top - 3 + RowH * Rows ? (local.y - (Top - 3)) / RowH : -1;
            int k = r >= 0 && local.x > 3 && local.x <= sel.Window.Width - 14 ? sel.First + r : -1;
            bool hit = k >= 0 && k < sel.Count;
            // the buttons sit below the rows: a press there keeps the selection
            if (local.y >= Top - 3 + RowH * Rows) return false;
            sel.Selected = hit ? k : -1;
            sel.Window.Invalidate();
            return hit;
        }

        private static void Scroll(Selector sel, int rows) {
            int first = Mathf.Clamp(sel.First + rows, 0, Math.Max(0, sel.Count - Rows));
            if (first == sel.First) return;
            sel.First = first;
            sel.Window.Invalidate();
        }

        /// <summary>Msg 6 / 0xb4: only with a selection; the window goes once the choice is out.</summary>
        private static void Ok(Selector sel) {
            if (sel.Selected < 0 || sel.Selected >= sel.Count) return;
            sel.Window.Visible = false;
            sel.Answered = sel.Sequence;
            sel.Ok?.Invoke(sel.Selected);
        }

        private static void Cancel(Selector sel) {
            sel.Window.Visible = false;
            sel.Answered = sel.Sequence;
            sel.Cancel?.Invoke();
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
        }

        /// <summary>The sweep console: pick id row | ok id | cancel id | state.</summary>
        public static string Sweep(string command) {
            if (instance == null) return "no selector";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i].StartsWith("0x", StringComparison.Ordinal) ? a[i].Substring(2) : a[i],
                a[i].StartsWith("0x", StringComparison.Ordinal) ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer, null, out int n) ? n : 0;
            if (a.Length > 1 && instance.selectors.TryGetValue(Arg(1), out Selector target)) {
                switch (a[0]) {
                    case "pick":
                        if (Arg(2) < target.Count) {
                            target.Selected = Arg(2);
                            target.Window.Invalidate();
                        }

                        break;
                    case "ok":
                        Ok(target);
                        break;
                    case "cancel":
                        Cancel(target);
                        break;
                }
            }

            var s = new System.Text.StringBuilder();
            foreach (Selector sel in instance.selectors.Values) {
                s.Append("0x").Append(sel.Id.ToString("x")).Append(": visible=").Append(sel.Window.Visible).Append(" selected=").Append(sel.Selected).Append(" items=");
                if (sel.Id == SpellList) {
                    foreach (int skill in sel.Skills) s.Append("skill ").Append(skill).Append(' ');
                } else {
                    foreach (RoItemInstance item in sel.Items) s.Append(item.ItemId).Append('#').Append(item.BagIndex).Append(' ');
                }

                s.Append("; ");
            }

            return s.ToString().TrimEnd();
        }
    }
}
