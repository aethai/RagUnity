using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Entities;
using Ragnarok.Gameplay;
using Ragnarok.Sprites;
using Ragnarok.World;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>A skill carried by a classic drag to the hotkey bar (DragStart type 8: id and use level).</summary>
    internal sealed class RoClassicSkillPayload : IRoClassicDragPayload {
        public RoSkillHotbarPayload Skill;
        public string IconPath { get; set; }
    }

    /// <summary>
    /// The skill window, the original client's UINewSkillListWnd (window id 37) in its tree view (560×400, 7×6 cells) and
    /// list view (330 wide, 32-px rows), from the reverse-engineered msg 0xe layout, OnDraw 0x913a40, DrawTree 0x9112c0,
    /// DrawList 0x910c80 and mouse handlers (reference ui-static ui/RUNTIME-RULES-SKILLS.md §1), with the skill
    /// description window UISkillDescribeWnd (id 46, oracle layout). Opened by the menu icon / Alt+S and the job level-up
    /// notice; the open window is rebuilt from the session's skill sequence by RoClassicGameUi. The data, the tree layout,
    /// the upgrade plan and the skill requests are <see cref="RoSkillTreeModel"/>.
    /// </summary>
    public sealed class RoClassicSkillTree {
        private const string SkUi = "유저인터페이스\\";
        private const string SkBasic = SkUi + "basic_interface\\";
        private const string SkIcons = SkUi + "item\\";
        private const string SkPrefs = "ro.ui.classic.skill.";
        private const int SkTabWidth = 18;          // T: the vertical tab control (tabH 17)
        private const int SkTreeW = 560;
        private const int SkTreeH = 400;
        private const int SkListW = 330;
        private const int SkCols = 7;
        private const int SkTreeRows = 6;
        private const int SkEtcTab = RoSkillTreeLayout.TabCount;   // tabs 1..4 = job classes, 5 = Etc.
        private const int SkCloseId = 0xc5;
        private const int SkViewId = 0xc6;
        private const int SkBaseId = 0x248;
        private const int SkInfoId = 0xd1;
        private const int SkApplyId = 0x10b;
        private const int SkResetId = 0x167;
        private const float SkLookCheckSeconds = 0.5f;
        private static readonly Color32 SkBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 SkWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 SkFrame = new Color32(0xc3, 0xc3, 0xc3, 255);
        private static readonly Color32 SkGrey = new Color32(0x66, 0x66, 0x66, 255);
        private static readonly Color32 SkUnlearned = new Color32(0x78, 0x78, 0x78, 255);
        private static readonly Color32 SkPreFill = new Color32(0xff, 0xc0, 0xcc, 255);
        private static readonly Color32 SkPreLine = new Color32(0xff, 0xb6, 0xc1, 255);
        private static readonly Color32 SkUpFill = new Color32(0xaf, 0xc4, 0xf0, 255);
        private static readonly Color32 SkRed = new Color32(255, 0, 0, 255);
        private static readonly Color32 SkSelLevel = new Color32(0xff, 0x3c, 0x7f, 255);
        private static readonly Color32 SkPlanned = new Color32(0, 0, 255, 255);
        private static readonly Color32 SkTabTint = new Color32(0xfc, 0xe7, 0xe1, 255);
        private static readonly string[] SkButtonStates = { "normal", "hover", "pressed" };
        private static readonly List<RoSkillDisplayEntry> SkNoEntries = new List<RoSkillDisplayEntry>(0);

        private static RoClassicSkillTree instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private readonly RoSkillTreeModel model = new RoSkillTreeModel();
        private readonly Dictionary<int, List<RoSkillDisplayEntry>> skByTab = new Dictionary<int, List<RoSkillDisplayEntry>>();
        private readonly Dictionary<int, RoGameplaySkillInfo> skLearned = new Dictionary<int, RoGameplaySkillInfo>();
        private readonly Dictionary<int, int> skOrder = new Dictionary<int, int>();
        private readonly Dictionary<int, int> skPrereq = new Dictionary<int, int>();   // hovered skill + prerequisites → required level
        private readonly List<RoGameplaySkillInfo> skEtc = new List<RoGameplaySkillInfo>();
        private readonly List<int> skTabs = new List<int>(5);
        private RoClassicUiSystem system;
        private RoClassicWindow classicSkill;
        private int seenSequence = int.MinValue;
        private int seenAssetsVersion = int.MinValue;

        // DrawCharacter 0x9102b0 (the tree view): the player's body and head only, ACT action 0x17 (sitting, facing south-
        // east), feet at (48, H − 25); frame = ⌊(now − clock) / 300 ms⌋ / n (n = the action's frame count), the clock restarting
        // once that reaches 3. Rendered once per look into images (RoClassicActorPortrait), nothing per frame; the look is
        // checked twice a second.
        private const int SkCharW = 70, SkCharH = 110, SkCharFeetY = 98;
        private string[] skCharFrames;
        private int skCharHash = int.MinValue, skCharVersion, skCharShown = -1;
        private float skCharClock;
        private float skCharNextLookCheck;
        private RoAssetDatabase skCharAssets;
        private RoClassicChildLayout skApply;
        private RoClassicChildLayout skReset;
        private RoClassicChildLayout skInfo;
        private RoClassicWindow skDescribe;
        private RoClassicChildLayout skDescribeText;
        private bool skListView;
        private bool skShowInfo = true;      // m_bShowSkillDescript (user option 1)
        private int skTab = -1;
        private int skFirst;
        private int skHover = -1;
        private bool skPrereqLocked;
        private int skDescribedId;

        private RoClassicSkillTree() {
            model.Changed = Rebuild;
        }

        public static bool IsOpen => instance?.classicSkill != null && instance.classicSkill.Visible;

        private int SkW => skListView ? SkListW : SkTreeW;
        private int SkH => SkTreeH;
        private int SkListRows => (SkH - 32 - 30) / 32;
        private int SkRemaining => model.Remaining;
        private bool SkPlanPending => model.PlanPending;

        /// <summary>The menu icon / Alt+S: opens or closes the window.</summary>
        public static void Toggle(RoLoginShellSession loginSession) {
            if (IsOpen) {
                instance.Hide();
                return;
            }

            Open(loginSession);
        }

        /// <summary>MakeWindow 0x25 (the menu icon, the job level-up notice): shows the window rebuilt from the session.</summary>
        public static void Open(RoLoginShellSession loginSession) {
            RoClassicSkillTree t = Ensure(loginSession);
            if (t == null) return;
            try {
                t.EnsureClassicSkill();
                t.seenSequence = loginSession.SkillWindowChangeSequence;
                t.skCharNextLookCheck = 0f;   // the look is checked on the first frame
                t.Rebuild();
                if (!t.classicSkill.Visible) {
                    t.classicSkill.Visible = true;
                    t.system.Windows.BringToFront(t.classicSkill);
                }
            } catch (Exception e) {
                Debug.LogException(e);
                t.Hide();
            }
        }

        /// <summary>Leaving the map: the window closes and the plan and the chosen use levels are forgotten.</summary>
        public static void Close() {
            if (instance == null) return;
            instance.Hide();
            instance.model.Reset();
        }

        /// <summary>
        /// Per frame (RoClassicGameUi): the skills the server starts (ZC_AUTORUN_SKILL) go to the targeting; the open window
        /// follows the session's skill sequence (and the asset database once it loads), its hover and its character.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession) {
            if (loginSession == null) return;
            if (loginSession.HasPendingAutoRunSkills) Ensure(loginSession)?.model.DrainPendingAutoRunSkills();
            RoClassicSkillTree t = instance;
            if (t == null || t.classicSkill == null || !t.classicSkill.Visible) return;
            t.model.Session = loginSession;
            t.model.RefreshAssets();
            int sequence = loginSession.SkillWindowChangeSequence;
            if (sequence != t.seenSequence || t.model.AssetsVersion != t.seenAssetsVersion) {
                t.seenSequence = sequence;
                t.Rebuild();
            }

            t.UpdateClassicSkillHover();
            t.UpdateClassicSkillCharacter();
        }

        private static RoClassicSkillTree Ensure(RoLoginShellSession loginSession) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || loginSession == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicSkillTree { system = system };
            instance.model.Session = loginSession;
            return instance;
        }

        /// <summary>The tab lists from a fresh snapshot, the layout and a redraw (on the sequence, the plan, a use level).</summary>
        private void Rebuild() {
            if (classicSkill == null) return;
            RoGameplaySkillSnapshot gameplay = model.TakeSnapshot();
            seenAssetsVersion = model.AssetsVersion;
            BuildClassicSkillTabs(gameplay.Skills ?? Array.Empty<RoGameplaySkillInfo>(), gameplay.Status ?? RoPlayerStatusSnapshot.Empty);
            ApplyClassicSkillLayout();
            classicSkill.Invalidate();
        }

        private void EnsureClassicSkill() {
            if (classicSkill != null) return;
            var layout = new RoClassicWindowLayout { Class = "UINewSkillListWnd", WindowId = 37, Width = SkTreeW, Height = SkTreeH };
            layout.Children.Add(SkButton(SkBaseId, "base", 11, 11, SkBasic + "sys_base_off.bmp", SkBasic + "sys_base_on.bmp"));
            skInfo = SkButton(SkInfoId, "info", 12, 12, null, null);
            layout.Children.Add(skInfo);
            layout.Children.Add(SkButton(SkViewId, "view", 11, 11, SkBasic + "sys_mini_off.bmp", SkBasic + "sys_mini_on.bmp"));
            layout.Children.Add(SkButton(SkCloseId, "close", 11, 11, SkBasic + "sys_close_off.bmp", SkBasic + "sys_close_on.bmp"));
            skApply = SkButton(SkApplyId, "apply", 42, 20, SkUi + "btn_apply.bmp", SkUi + "btn_apply_a.bmp", SkUi + "btn_apply_b.bmp");
            skReset = SkButton(SkResetId, "reset", 42, 20, SkUi + "btn_reset.bmp", SkUi + "btn_reset_a.bmp", SkUi + "btn_reset_b.bmp");
            layout.Children.Add(skApply);
            layout.Children.Add(skReset);

            // the title strip is the frame drag area (y < 20)
            classicSkill = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = local => local.y < 20, DragSourceAt = ClassicSkillDragSource };
            classicSkill.DrawDynamic += DrawClassicSkill;
            classicSkill.Clicked += OnClassicSkillClicked;
            classicSkill.Pressed += OnClassicSkillPressed;
            classicSkill.Released += OnClassicSkillReleased;
            classicSkill.RightClicked += OnClassicSkillRightClicked;
            classicSkill.Wheeled += (window, local, delta) => ScrollClassicSkills(delta > 0 ? -Math.Min(SkVisibleRows - 1, 3) : Math.Min(SkVisibleRows - 1, 3));
            classicSkill.Moved += window => {
                PlayerPrefs.SetInt(SkPrefs + "x", window.Position.x);
                PlayerPrefs.SetInt(SkPrefs + "y", window.Position.y);
            };

            // msg 0x22: SKILLLISTWNDINFO, or (360, 200); the view from m_isSimpleSkillWnd (user: tree)
            skListView = PlayerPrefs.GetInt(SkPrefs + "list", 0) != 0;
            skShowInfo = PlayerPrefs.GetInt(SkPrefs + "info", 1) != 0;
            Vector2Int screen = system.ClientSize;
            int x = PlayerPrefs.GetInt(SkPrefs + "x", 360), y = PlayerPrefs.GetInt(SkPrefs + "y", 200);
            if (x < 0 || y < 0 || x > screen.x - SkW || y > screen.y - SkH) {
                x = 360;
                y = 200;
            }

            classicSkill.MoveTo(x, y);
        }

        private static RoClassicChildLayout SkButton(int id, string path, int w, int h, string normal, string hover, string pressed = null) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, W = w, H = h };
            if (normal != null) {
                button.States["normal"] = normal;
                button.States["hover"] = hover;
                button.States["pressed"] = pressed ?? hover;
            }

            return button;
        }

        // ---- data: the tab lists (the job's tree layout + the session's skills) ----

        private void BuildClassicSkillTabs(RoGameplaySkillInfo[] skills, RoPlayerStatusSnapshot status) {
            foreach (KeyValuePair<int, List<RoSkillDisplayEntry>> pair in skByTab) pair.Value.Clear();
            skLearned.Clear();
            skOrder.Clear();
            for (int i = 0; i < skills.Length; i++) {
                skLearned[skills[i].SkillId] = skills[i];
                if (!skOrder.ContainsKey(skills[i].SkillId)) skOrder[skills[i].SkillId] = i;
            }

            RoSkillTreeCell[] layout = model.GetLayoutForJob(status.JobId);
            foreach (RoSkillTreeCell cell in layout) {
                // only the job trees (SKILL_TREEVIEW_FOR_JOB); the layout's own "Etc" table is not a tree
                if (cell.Tab < 1 || cell.Tab >= SkEtcTab) continue;
                skLearned.TryGetValue(cell.SkillId, out RoGameplaySkillInfo skill);
                AddEntry(model.BuildDisplayEntry(cell.SkillId, cell.Tab, cell.Slot, skill, skill.SkillId != 0));
                skLearned.Remove(cell.SkillId);
            }

            // "Etc.": the character's skills in no tree, in packet order, level > 0 only
            skEtc.Clear();
            skEtc.AddRange(skLearned.Values);
            skEtc.Sort((a, b) => skOrder[a.SkillId].CompareTo(skOrder[b.SkillId]));
            foreach (RoGameplaySkillInfo skill in skEtc) {
                if (skill.Level == 0) continue;
                AddEntry(model.BuildDisplayEntry(skill.SkillId, SkEtcTab, -1, skill, true));
            }

            // a tab per non-empty job class, "Etc." always last; the window opens on the highest job tab
            skTabs.Clear();
            for (int t = 1; t < SkEtcTab; t++) {
                if (skByTab.TryGetValue(t, out List<RoSkillDisplayEntry> list) && list.Count > 0) skTabs.Add(t);
            }

            skTabs.Add(SkEtcTab);
            if (!skTabs.Contains(skTab)) skTab = skTabs.Count > 1 ? skTabs[skTabs.Count - 2] : SkEtcTab;
        }

        private void AddEntry(RoSkillDisplayEntry entry) {
            if (!skByTab.TryGetValue(entry.Tab, out List<RoSkillDisplayEntry> list)) {
                list = new List<RoSkillDisplayEntry>();
                skByTab[entry.Tab] = list;
            }

            list.Add(entry);
        }

        private List<RoSkillDisplayEntry> SkCurrentEntries =>
            skByTab.TryGetValue(skTab, out List<RoSkillDisplayEntry> list) ? list : SkNoEntries;

        private int SkVisibleRows => skListView ? SkListRows : SkTreeRows;

        private int SkTotalRows {
            get {
                List<RoSkillDisplayEntry> entries = SkCurrentEntries;
                if (skListView) return entries.Count;
                int max = -1;
                foreach (RoSkillDisplayEntry e in entries) max = Math.Max(max, e.Slot);
                return (max + 1 + SkCols - 1) / SkCols;
            }
        }

        /// <summary>msg 0xe → Layout 0x914860 / Buttons 0x9149e0: size, scroll range, child positions per view.</summary>
        private void ApplyClassicSkillLayout() {
            int W = SkW, H = SkH;
            classicSkill.Layout.Width = W;
            classicSkill.SetHeight(H);
            classicSkill.SetChildPosition("base", 22, 5);
            classicSkill.SetChildPosition("info", W - 130, 5);
            classicSkill.SetChildPosition("view", W - 30, 5);
            classicSkill.SetChildPosition("close", W - 16, 5);
            classicSkill.SetChildPosition("apply", W - (skListView ? 115 : 100), H - 27);
            classicSkill.SetChildPosition("reset", W - (skListView ? 65 : 50), H - 27);
            string info = SkUi + (skShowInfo ? "checkbox_1.bmp" : "checkbox_0.bmp");
            foreach (string state in SkButtonStates) skInfo.States[state] = info;
            int range = Math.Max(0, SkTotalRows - SkVisibleRows);
            skFirst = range == 0 ? 0 : Mathf.Clamp(skFirst, 0, range);
        }

        private void ScrollClassicSkills(int rows) {
            int range = Math.Max(0, SkTotalRows - SkVisibleRows);
            int first = Mathf.Clamp(skFirst + rows, 0, range);
            if (first == skFirst) return;
            skFirst = first;
            classicSkill.Invalidate();
        }

        // ---- drawing (OnDraw 0x913a40) ----

        private void DrawClassicSkill(RoClassicWindow w) {
            int W = SkW, H = SkH, T = SkTabWidth;
            w.FillRect(T, 20, W - T, H - 52, SkWhite);
            w.RoundOutline(T, 0, W - T, H, SkFrame);
            w.RoundOutline(T + 2, 2, W - T - 4, H - 4, SkFrame);
            w.DrawBitmapRaw(T, 0, SkBasic + "skill_upbar_left.bmp");
            w.DrawBitmapRaw(W - 12, 0, SkBasic + "skill_upbar_right.bmp");
            for (int k = 0, n = (W - T - 24 + 4) / 5; k < n; k++) w.DrawBitmapRaw(T + 12 + 5 * k, 0, SkBasic + "skill_upbar_mid.bmp");
            w.DrawBitmapRaw(T, H - 32, SkBasic + "skill_botbar_left.bmp");
            w.DrawBitmapRaw(W - 12, H - 32, SkBasic + "skill_botbar_right.bmp");
            for (int k = 0, n = (W - T - 24 + 4) / 5; k < n; k++) w.DrawBitmapRaw(T + 12 + 5 * k, H - 32, SkBasic + "skill_botbar_mid.bmp");
            w.TextOut(41, 6, "Skill Tree", 0, 12, SkWhite);   // MsgStr 0x11b
            w.TextOut(40, 5, "Skill Tree", 0, 12, SkBlack);
            // "View Skill Info" check box label (MsgStr 0x560) next to its 10×10 box
            w.TextOut(W - 130 + 14, 4, "View Skill Info", 0, 12, SkBlack);

            int P = model.SkillPoints, remaining = SkRemaining;
            if (skListView) {
                w.TextOut(30, H - 23, "Skill Point: " + remaining.ToString(CultureInfo.InvariantCulture), 0, 12, SkBlack);   // MsgStr 0x11c
                DrawClassicSkillList(w);
            } else {
                string points = remaining < P
                    ? string.Format(CultureInfo.InvariantCulture, "Skill Points : ^0000ff{0}^000000(^ff0000{1}^000000) / {2}", remaining, P - remaining, P)
                    : "Skill Points : " + P.ToString(CultureInfo.InvariantCulture);   // MsgStr 0x927
                w.TextOutColored(80, H - 23, points, 0, 12);
                DrawClassicSkillTree(w);
                DrawClassicSkillCharacter(w, H);
            }

            int range = Math.Max(0, SkTotalRows - SkVisibleRows);
            w.DrawScrollBar(W - 15, 20, H - 52, range, SkTotalRows, SkVisibleRows, skFirst);
            DrawClassicSkillTabs(w);
        }

        private void DrawClassicSkillCharacter(RoClassicWindow w, int h) {
            if (skCharFrames == null || skCharShown < 0) return;
            w.DrawBitmap(48 - SkCharW / 2, h - 25 - SkCharFeetY, skCharFrames[skCharShown]);
        }

        /// <summary>Per frame while the window shows: the look's images when it changed, the frame by the 300 ms clock.</summary>
        private void UpdateClassicSkillCharacter() {
            if (skListView || classicSkill == null) return;
            if (Time.unscaledTime >= skCharNextLookCheck) {
                skCharNextLookCheck = Time.unscaledTime + SkLookCheckSeconds;
                UpdateClassicSkillCharacterLook();
            }

            if (skCharFrames == null) return;
            int n = skCharFrames.Length;
            int frame = (int) ((Time.unscaledTime - skCharClock) * 1000f / 300f) / n;
            if (frame >= 3) {
                frame = 0;
                skCharClock = Time.unscaledTime;
            }

            // a frame past the action's last is its first (0x6c1790)
            int shown = frame < n ? frame : 0;
            if (shown == skCharShown) return;
            skCharShown = shown;
            classicSkill.Invalidate();
        }

        private void UpdateClassicSkillCharacterLook() {
            RoAssetDatabase assets = model.ResolveAssetDatabase();
            RoOnlineWorldController controller = model.ResolveWorldController();
            if (assets == null || controller == null || controller.OwnActorId == 0 ||
                !controller.State.TryGetActor(controller.OwnActorId, out RoOnlineActorSnapshot actor)) {
                return;
            }

            RoActorAppearanceRequest full = RoOnlineActorPresenter.BuildPlayerAppearanceRequest(actor);
            // two layers in the client: the body and the head (hair style and colour), no headgear, weapon or shield
            var request = new RoActorAppearanceRequest {
                kind = RoActorAppearanceKind.Player,
                serverId = full.serverId,
                baseClassId = full.baseClassId,
                classId = full.classId,
                bodyStyleId = full.bodyStyleId,
                isMale = full.isMale,
                headId = full.headId,
                hairId = full.hairId,
                hairColorId = full.hairColorId,
                bodyPaletteId = full.bodyPaletteId,
                motionProfile = RoSpriteMotionProfile.Player,
                motion = RoSpriteMotion.Sit,
                facingDirection = RoActorDirection.SouthEast,
                headFacing = RoActorHeadFacing.Center,
            };
            int hash;
            unchecked {
                hash = ((((request.classId * 397 ^ request.bodyStyleId) * 397 ^ request.headId) * 397 ^ request.hairColorId) * 397 ^
                        request.bodyPaletteId) * 397 ^ (request.isMale ? 1 : 0);
            }

            if (hash == skCharHash && ReferenceEquals(assets, skCharAssets)) return;
            skCharHash = hash;
            skCharAssets = assets;
            int version = ++skCharVersion;
            string key = "@skill-character-" + (version & 1).ToString(CultureInfo.InvariantCulture);
            bool started = RoClassicActorPortrait.RenderFrames(assets, request, key, SkCharW, SkCharH, SkCharFeetY, count => {
                if (version != skCharVersion) return;
                if (count <= 0) {
                    skCharHash = int.MinValue;
                    return;
                }

                var keys = new string[count];
                for (int f = 0; f < count; f++) keys[f] = RoClassicActorPortrait.FrameKey(key, f);
                skCharFrames = keys;
                skCharClock = Time.unscaledTime;
                skCharShown = -1;
            });
            if (!started) skCharHash = int.MinValue;
        }

        private static string SkIcon(RoSkillDisplayEntry e) {
            string name = e.Metadata != null && !string.IsNullOrWhiteSpace(e.Metadata.IconName) ? e.Metadata.IconName : null;
            return SkIcons + (name ?? "am_berserkpitcher") + ".bmp";
        }

        private static void SkDrawIcon(RoClassicWindow w, int x, int y, RoSkillDisplayEntry e, int tint) {
            string icon = SkIcon(e);
            if (tint != 0) icon = RoClassicUiImages.Tinted(icon, tint);
            if (!w.DrawBitmap(x, y, icon)) w.DrawBitmap(x, y, tint != 0 ? RoClassicUiImages.Tinted(SkIcons + "am_berserkpitcher.bmp", tint) : SkIcons + "am_berserkpitcher.bmp");
        }

        /// <summary>DrawTree 0x9112c0: empty cells, then each skill of the tab at its tree position.</summary>
        private void DrawClassicSkillTree(RoClassicWindow w) {
            for (int p = 0; p < SkCols * SkTreeRows; p++) w.DrawBitmap(76 + 68 * (p % SkCols), 44 + 56 * (p / SkCols), SkBasic + "no_skill.bmp");
            RoClassicGdiFont small = w.FontFor(0, 11), bold = w.FontFor(0, 11, true);
            int remaining = SkRemaining;
            foreach (RoSkillDisplayEntry e in SkCurrentEntries) {
                int p = e.Slot - skFirst * SkCols;
                if (e.Slot < 0 || p < 0 || p >= SkCols * SkTreeRows) continue;
                int bx = 72 + 68 * (p % SkCols), by = 56 * (p / SkCols);
                bool inPre = skTab != SkEtcTab && skPrereq.ContainsKey(e.SkillId);
                bool canUp = remaining > 0 && e.Learned && e.Skill.CanUpgrade &&
                             (e.Metadata == null || e.Metadata.MaxLevel <= 0 || e.EffectiveLevel < e.Metadata.MaxLevel);
                if (inPre) {
                    w.FillRect(bx + 3, by + 43, 26, 26, SkPreFill);
                    w.RoundOutline(bx + 2, by + 42, 28, 28, SkPreLine);
                } else if (canUp) {
                    w.FillRect(bx + 3, by + 43, 26, 26, SkUpFill);
                    w.RoundOutline(bx + 2, by + 42, 28, 28, SkUpFill);
                } else {
                    w.FillRect(bx + 3, by + 43, 26, 26, SkWhite);
                }

                Color32 g = SkGrey;
                int tint = inPre ? 2 : canUp ? 4 : 0;
                if (!inPre && !canUp && e.Level == 0) {
                    tint = 1;
                    g = SkUnlearned;
                }

                SkDrawIcon(w, bx + 4, by + 44, e, tint);
                if (inPre && skPrereq[e.SkillId] > 0) {
                    w.TextOutOutline(bx + 30, by + 58, skPrereq[e.SkillId].ToString(CultureInfo.InvariantCulture), 0, 11, SkWhite, SkBlack, true);
                }

                if (e.PlannedDelta > 0) w.TextOut(bx + 30, by + 44, "+" + e.PlannedDelta.ToString(CultureInfo.InvariantCulture), 0, 11, SkRed);
                if (e.Level > 0) {
                    if (e.CanSelectLevel) {
                        string sel = e.SelectedLevel.ToString(CultureInfo.InvariantCulture);
                        string max = Math.Max(e.EffectiveLevel, e.Level).ToString(CultureInfo.InvariantCulture);
                        int x = bx + 4 + (24 - bold.Measure(sel + " / " + max)) / 2;
                        w.TextOutOutline(x, by + 70, sel, 0, 11, e.SelectedLevel == e.Level ? g : SkSelLevel, SkWhite, true);
                        x += bold.Measure(sel);
                        w.TextOutOutline(x, by + 70, " / ", 0, 11, g, SkWhite, true);
                        x += bold.Measure(" / ");
                        w.TextOutOutline(x, by + 70, max, 0, 11, g, SkWhite, true);
                    } else {
                        string level = e.Level.ToString(CultureInfo.InvariantCulture);
                        w.TextOutOutline(bx + 4 + (24 - bold.Measure(level)) / 2, by + 70, level, 0, 11, g, SkWhite, true);
                    }
                }

                string name = SkEllipsize(e.DisplayName, 48, small);
                w.TextOut(bx - 8 + Math.Max(0, (48 - small.Measure(name)) / 2), by + 30, name, 0, 11, g);
                if (e.Skill.Level2 < e.Skill.Level) w.DrawBitmap(bx + 8, by + 58, SkBasic + "item_skill.bmp");

                // level arrows of a level-use skill (no plan pending)
                if (!SkPlanPending && e.CanSelectLevel) {
                    w.DrawBitmap(56 + 68 * (p % SkCols), 70 + 56 * (p / SkCols), SkBasic + "arw_left.bmp");
                    w.DrawBitmap(111 + 68 * (p % SkCols), 70 + 56 * (p / SkCols), SkBasic + "arw_right.bmp");
                }
            }
        }

        /// <summary>0x9b2e60: the text cut to fit the width, with "..." appended when cut.</summary>
        private static string SkEllipsize(string s, int width, RoClassicGdiFont font) {
            if (string.IsNullOrEmpty(s) || font.Measure(s) <= width) return s ?? string.Empty;
            int dots = font.Measure("...");
            int n = s.Length;
            while (n > 0 && font.Measure(s.Substring(0, n)) + dots > width) n--;
            return s.Substring(0, n) + "...";
        }

        /// <summary>DrawList 0x910c80: 32-px rows from the first visible one.</summary>
        private void DrawClassicSkillList(RoClassicWindow w) {
            List<RoSkillDisplayEntry> entries = SkCurrentEntries;
            RoClassicGdiFont font = w.FontFor(0, 12);
            int remaining = SkRemaining;
            for (int i = 0; i < SkListRows && skFirst + i < entries.Count; i++) {
                RoSkillDisplayEntry e = entries[skFirst + i];
                int ry = 32 * i;
                Color32 g = e.Level > 0 ? SkBlack : SkUnlearned;
                Color32 lvColor = e.PlannedDelta > 0 ? SkPlanned : g;
                if (skHover == skFirst + i) {
                    Color32 fill = e.Level == 0 ? new Color32(0xb4, 0xb4, 0xb4, 255) : e.SpCost == 0 ? new Color32(0x70, 0xd7, 0xed, 255) : new Color32(0x70, 0x9f, 0xed, 255);
                    w.FillRect(104, ry + 32, 164, 28, fill);
                }

                SkDrawIcon(w, 44, ry + 34, e, e.Level == 0 ? 1 : 0);
                w.TextOut(112, ry + 35, e.DisplayName, 0, 12, g);
                if (e.Level > 0 || e.PlannedDelta > 0) {
                    bool levelUse = e.CanSelectLevel;
                    w.TextOut(SkPlanPending || !levelUse ? 113 : 124, ry + 47, "Lv :", 0, 12, g);
                    int shown = e.EffectiveLevel;
                    if (levelUse) {
                        int x = 112 + (SkPlanPending ? 25 : 37);
                        string sel = e.SelectedLevel.ToString("D", CultureInfo.InvariantCulture).PadLeft(2);
                        w.TextOut(x, ry + 47, sel, 0, 12, e.SelectedLevel == e.Level ? g : SkRed);
                        x += 7 + font.Measure(sel);
                        w.TextOut(x, ry + 47, "/ ", 0, 12, g);
                        x += font.Measure("/ ");
                        w.TextOut(x, ry + 47, shown.ToString(CultureInfo.InvariantCulture).PadLeft(2), 0, 12, lvColor);
                    } else {
                        w.TextOut(137, ry + 47, shown.ToString(CultureInfo.InvariantCulture), 0, 12, lvColor);
                    }

                    string sp = e.Passive ? "Passive" : "Sp : " + e.SpCost.ToString(CultureInfo.InvariantCulture);   // MsgStr 0x11e
                    w.TextOut(212, ry + 47, sp, 0, 12, g);
                }

                // skill-up (planned in this window) and the level arrows
                bool canUp = remaining > 0 && e.Learned && e.Skill.CanUpgrade &&
                             (e.Metadata == null || e.Metadata.MaxLevel <= 0 || e.EffectiveLevel < e.Metadata.MaxLevel);
                if (canUp) w.DrawBitmap(78, 34 + 32 * i, SkBasic + "skill_up_a.bmp");
                if (!SkPlanPending && e.CanSelectLevel) {
                    w.DrawBitmap(112, 48 + 32 * i, SkBasic + "arw_left.bmp");
                    w.DrawBitmap(192, 48 + 32 * i, SkBasic + "arw_right.bmp");
                }
            }
        }

        /// <summary>The vertical tab control at (0, 6): style 0, pad 15, unequal extents, prerequisite tabs tinted.</summary>
        private void DrawClassicSkillTabs(RoClassicWindow w) {
            RoClassicGdiFont font = w.FontFor(0, 12);
            int y0 = 6;
            for (int i = 0; i < skTabs.Count; i++) {
                int tab = skTabs[i];
                string label = SkTabLabel(tab);
                int extent = 15 * label.Length + 15;
                bool selected = tab == skTab;
                bool tinted = !selected && SkTabHasPrerequisite(tab);
                int x0 = selected ? 0 : 1;
                w.FillRect(x0 + 1, y0 + 1, SkTabWidth - 1, extent - 2, tinted ? SkTabTint : SkWhite);
                w.FillRect(x0 + 2, y0, SkTabWidth - 2, 1, SkFrame);
                w.FillRect(x0 + 1, y0 + 1, 1, 1, SkFrame);
                w.FillRect(x0, y0 + 2, 1, extent - 4, SkFrame);
                w.FillRect(x0 + 1, y0 + extent - 2, 1, 1, SkFrame);
                w.FillRect(x0 + 2, y0 + extent - 1, SkTabWidth - 2, 1, SkFrame);
                if (!selected) w.FillRect(SkTabWidth - 1, y0, 1, extent, SkFrame);
                int widest = 0;
                foreach (char ch in label) widest = Math.Max(widest, font.Measure(ch.ToString()));
                int x = (SkTabWidth - 12) / 2 + (selected ? 0 : 1), y = y0 + (extent - 15 * label.Length) / 2;
                foreach (char ch in label) {
                    string s = ch.ToString();
                    w.TextOut(x + (widest - font.Measure(s)) / 2, y, s, 0, 12, SkBlack);
                    y += 15;
                }

                y0 += extent - 1;
            }
        }

        private static string SkTabLabel(int tab) => tab == SkEtcTab ? "Etc." : RoSkillTreeLayout.TabLabels[Mathf.Clamp(tab - 1, 0, 3)];

        private bool SkTabHasPrerequisite(int tab) {
            if (skPrereq.Count == 0 || !skByTab.TryGetValue(tab, out List<RoSkillDisplayEntry> list)) return false;
            foreach (RoSkillDisplayEntry e in list) {
                if (skPrereq.ContainsKey(e.SkillId)) return true;
            }

            return false;
        }

        // ---- mouse (§1i) ----

        /// <summary>The tab-list index under a window-local point (tree: by position with the narrow hit band), or -1.</summary>
        private int SkIndexAt(Vector2Int local) {
            if (skListView) {
                if (local.y < 20 || local.y >= SkListRows * 32 + 34 || local.x < 40) return -1;
                int r = (local.y - 34) / 32;   // C truncation: row 0 covers 20..65
                int i = skFirst + r;
                return i < SkCurrentEntries.Count ? i : -1;
            }

            if (local.y < 20 || local.y >= 380 || local.x < 76 || local.x > 504) return -1;
            int c = (local.x - 76) / 68, row = (local.y - 44) / 56;
            if (local.x > 68 * c + 100 || local.y > 56 * row + 58) return -1;
            int pos = (skFirst + row) * SkCols + c;
            List<RoSkillDisplayEntry> entries = SkCurrentEntries;
            for (int i = 0; i < entries.Count; i++) {
                if (entries[i].Slot == pos) return i;
            }

            return -1;
        }

        private bool SkTryEntry(int index, out RoSkillDisplayEntry entry) {
            List<RoSkillDisplayEntry> entries = SkCurrentEntries;
            entry = index >= 0 && index < entries.Count ? entries[index] : default;
            return index >= 0 && index < entries.Count;
        }

        private static RoClassicSkillPayload SkPayload(RoSkillDisplayEntry e) => new RoClassicSkillPayload {
            Skill = new RoSkillHotbarPayload("skill", "SkillList", e.SkillId, e.Level, e.SelectedLevel, e.DisplayName),
            IconPath = SkIcon(e),
        };

        /// <summary>LBtnDown on a learned, non-passive skill (no plan pending): the drag starts at once with its use level.</summary>
        private IRoClassicDragPayload ClassicSkillDragSource(Vector2Int local) {
            if (SkPlanPending || !SkTryEntry(SkIndexAt(local), out RoSkillDisplayEntry e) || !e.Draggable || e.Level <= 0 || e.Passive) return null;
            return SkPayload(e);
        }

        private void OnClassicSkillPressed(RoClassicWindow window, Vector2Int local) {
            skPrereqLocked = false;
            // tabs: unequal extents from (0, 6)
            if (local.x < SkTabWidth) {
                int y0 = 6;
                foreach (int tab in skTabs) {
                    int extent = 15 * SkTabLabel(tab).Length + 15;
                    if (local.y >= y0 && local.y < y0 + extent) {
                        if (tab != skTab) {
                            skTab = tab;
                            skFirst = 0;
                            skHover = -1;
                            ApplyClassicSkillLayout();
                            classicSkill.Invalidate();
                        }

                        return;
                    }

                    y0 += extent - 1;
                }

                return;
            }

            // level arrows (ids 0..83): the use level within 1..level
            if (!SkPlanPending) {
                List<RoSkillDisplayEntry> entries = SkCurrentEntries;
                for (int i = 0; i < entries.Count; i++) {
                    RoSkillDisplayEntry e = entries[i];
                    if (!e.CanSelectLevel) continue;
                    int lx, rx, ay;
                    if (skListView) {
                        int row = i - skFirst;
                        if (row < 0 || row >= SkListRows) continue;
                        lx = 112;
                        rx = 192;
                        ay = 48 + 32 * row;
                    } else {
                        int p = e.Slot - skFirst * SkCols;
                        if (e.Slot < 0 || p < 0 || p >= SkCols * SkTreeRows) continue;
                        lx = 56 + 68 * (p % SkCols);
                        rx = 111 + 68 * (p % SkCols);
                        ay = 70 + 56 * (p / SkCols);
                    }

                    if (local.y < ay || local.y >= ay + 11) continue;
                    if (local.x >= lx && local.x < lx + 11) {
                        model.AdjustSelectedUseLevel(e.SkillId, -1);
                        return;
                    }

                    if (local.x >= rx && local.x < rx + 11) {
                        model.AdjustSelectedUseLevel(e.SkillId, 1);
                        return;
                    }
                }
            }

            // list view: the skill-up buttons (78, 34 + 32i) plan one level
            if (skListView && local.x >= 78 && local.x < 102) {
                int row = (local.y - 34) / 32;
                if (local.y >= 34 && row < SkListRows && local.y < 34 + 32 * row + 24 && SkTryEntry(skFirst + row, out RoSkillDisplayEntry up) &&
                    SkRemaining > 0 && up.Learned && up.Skill.CanUpgrade) {
                    model.PlanSkillUpgrade(up);
                }
            }
        }

        /// <summary>LBtnUp (tree view): one more level for the skill, missing prerequisite levels planned with it.</summary>
        private void OnClassicSkillReleased(RoClassicWindow window, Vector2Int local) {
            if (skListView || !SkTryEntry(SkIndexAt(local), out RoSkillDisplayEntry e) || !e.Learned) return;
            model.PlanSkillUpgrade(e);
        }

        /// <summary>RBtnUp: lock / unlock the prerequisite highlight of the skill.</summary>
        private void OnClassicSkillRightClicked(RoClassicWindow window, Vector2Int local) {
            int index = SkIndexAt(local);
            if (index < 0) {
                skPrereqLocked = false;
                skPrereq.Clear();
            } else {
                skPrereqLocked = !(skPrereqLocked && index == skHover);
                SkSetHover(index);
            }

            classicSkill.Invalidate();
        }

        private void OnClassicSkillClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case SkCloseId:
                    Hide();
                    break;
                case SkViewId:
                    skListView = !skListView;
                    PlayerPrefs.SetInt(SkPrefs + "list", skListView ? 1 : 0);
                    skFirst = 0;
                    ApplyClassicSkillLayout();
                    classicSkill.Invalidate();
                    break;
                case SkInfoId:
                    skShowInfo = !skShowInfo;
                    PlayerPrefs.SetInt(SkPrefs + "info", skShowInfo ? 1 : 0);
                    if (!skShowInfo) HideClassicSkillDescribe();
                    ApplyClassicSkillLayout();
                    classicSkill.Invalidate();
                    break;
                case SkApplyId:
                    // MessageBoxModal(MsgStr 0x561, 2, 1, ...) == 0xb7 → CZ_UPGRADE_SKILLLEVEL per planned level
                    if (!SkPlanPending) break;
                    string confirm = null;
                    model.Assets?.Tables?.MsgStringTable?.TryGetValue("1377", out confirm);
                    RoClassicMessageBox.Show(confirm ?? "Once used, skill points cannot be re-allocated. Would you like to use the skill points?",
                        RoClassicMessageBox.TypeOkCancel, true, result => {
                            if (result == RoClassicMessageBox.IdYes && SkPlanPending) model.ApplyPlannedUpgrades();
                        });
                    break;
                case SkResetId:
                    if (SkPlanPending) model.ResetUpgradePlan();
                    break;
            }
        }

        /// <summary>The hovered skill and all its prerequisites with their required levels (pink highlight, tab tint).</summary>
        private void SkSetHover(int index) {
            if (index == skHover && skPrereq.Count > 0) return;
            skHover = index;
            if (skPrereqLocked && skPrereq.Count > 0) return;
            skPrereq.Clear();
            if (!SkTryEntry(index, out RoSkillDisplayEntry e)) return;
            skPrereq[e.SkillId] = 0;
            SkAddPrerequisites(e.SkillId, 0);
        }

        private void SkAddPrerequisites(int skillId, int depth) {
            RoSkillMetadataTable metadataTable = model.Metadata;
            if (depth > 16 || metadataTable == null || !metadataTable.TryGetSkill(skillId, out RoSkillMetadata metadata) || metadata == null) return;
            foreach (RoSkillRequirement req in metadata.Prerequisites) {
                if (!skPrereq.TryGetValue(req.SkillId, out int level) || level < req.Level) skPrereq[req.SkillId] = req.Level;
                SkAddPrerequisites(req.SkillId, depth + 1);
            }
        }

        // ---- per frame: hover (prerequisites, list highlight, the description window) ----

        private void UpdateClassicSkillHover() {
            RoClassicWindowManager windows = system.Windows;
            int index = -1;
            Vector2Int local = windows.Mouse - classicSkill.Position;
            if (windows.HoverWindow == classicSkill && windows.Dragging == null) {
                index = SkIndexAt(local);
            }

            if (index != skHover) {
                SkSetHover(index);
                classicSkill.Invalidate();
            }

            // the description follows the hover; in the list view only over the icon column (x ≤ 96)
            if (skShowInfo && (!skListView || local.x <= 96) && SkTryEntry(index, out RoSkillDisplayEntry e)) ShowClassicSkillDescribe(e, windows.Mouse);
            else HideClassicSkillDescribe();
        }

        /// <summary>UISkillDescribeWnd (id 46) at the cursor + 24 px, moved left / up when it would leave the screen.</summary>
        private void ShowClassicSkillDescribe(RoSkillDisplayEntry e, Vector2Int mouse) {
            if (skDescribe == null) {
                var asset = Resources.Load<TextAsset>("Ragnarok/Generated/OriginalClient/ui/UISkillDescribeWnd");
                if (asset == null) return;
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
                layout.Ops.Clear();   // the frame follows the height (drawn below)
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UITextViewer") {
                        skDescribeText = child;
                        child.Fields["text"] = "Describe";
                        if (child.Props != null) child.Props["wrapBytes"] = 45;
                    } else if (child.Class == "UIBitmapButton") {
                        child.Visible = false;   // the close button is parked when opened from a hover
                    }
                }

                skDescribe = new RoClassicWindow(system, layout) { Draggable = false, HitArea = new RectInt(0, 0, 0, 0) };
                skDescribe.DrawUnderlay += w => w.FillRect(0, 0, w.Width, w.Height, new Color32(0, 0, 0, 0x66));
                skDescribe.DrawDynamic += w => {
                    w.RoundOutline(0, 0, w.Width, w.Height, SkFrame);
                    w.RoundOutline(2, 2, w.Width - 4, w.Height - 4, SkWhite);
                };
            }

            if (skDescribedId != e.SkillId) {
                skDescribedId = e.SkillId;
                string text = e.Metadata != null && e.Metadata.HasDescription ? e.Metadata.DescriptionText : e.DisplayName;
                skDescribe.Set("Describe", text.Replace("\r", string.Empty));
                var lines = new List<(int, int)>();
                RoClassicWindow.WrapBytes(text, 45, lines);
                int h = lines.Count * 18 + 10;
                skDescribe.SetHeight(h);
                skDescribeText.H = h - 6;
            }

            int x = mouse.x + 24, y = mouse.y;
            Vector2Int client = system.ClientSize;
            if (x + skDescribe.Width > client.x) x -= 48 + skDescribe.Width;
            if (y + skDescribe.Height > client.y) y -= skDescribe.Height;
            skDescribe.MoveTo(x, Math.Max(0, y));
            if (!skDescribe.Visible) {
                skDescribe.Visible = true;
                system.Windows.BringToFront(skDescribe);
            }
        }

        private void HideClassicSkillDescribe() {
            skDescribedId = 0;
            if (skDescribe != null) skDescribe.Visible = false;
        }

        private void Hide() {
            HideClassicSkillDescribe();
            if (classicSkill != null) classicSkill.Visible = false;
        }

        // ---- diagnostics console (RoSkillSweepRunner "!sk ...") ----

        /// <summary>
        /// Drives the classic skill window through its handlers: open | close | tab t (index in the tab bar) | plan id
        /// (LBtnUp on the skill) | apply | reset | lvl id ±1 | hover id (prerequisites + description) | view | info |
        /// hotkey id slot | state. Returns the state after the command.
        /// </summary>
        internal static string Sweep(RoLoginShellSession loginSession, string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            if (a[0] == "open") Open(loginSession);
            else if (a[0] == "close") instance?.Hide();
            RoClassicSkillTree t = instance;
            if (t == null || t.classicSkill == null) return "no classic skill window";
            return t.SweepClassicSkills(a);
        }

        private string SweepClassicSkills(string[] a) {
            int Arg(int i) => i < a.Length ? int.Parse(a[i], CultureInfo.InvariantCulture) : 0;
            int IndexOf(int skillId) {
                List<RoSkillDisplayEntry> entries = SkCurrentEntries;
                for (int i = 0; i < entries.Count; i++) {
                    if (entries[i].SkillId == skillId) return i;
                }

                return -1;
            }

            switch (a[0]) {
                case "tab":
                    if (Arg(1) < skTabs.Count) {
                        skTab = skTabs[Arg(1)];
                        skFirst = 0;
                        ApplyClassicSkillLayout();
                        classicSkill.Invalidate();
                    }

                    break;
                case "plan":
                    if (SkTryEntry(IndexOf(Arg(1)), out RoSkillDisplayEntry planned) && planned.Learned) model.PlanSkillUpgrade(planned);
                    break;
                case "apply":
                    OnClassicSkillClicked(skApply);
                    break;
                case "reset":
                    OnClassicSkillClicked(skReset);
                    break;
                case "lvl":
                    model.AdjustSelectedUseLevel(Arg(1), Arg(2));
                    break;
                case "hover":
                    SkSetHover(IndexOf(Arg(1)));
                    if (SkTryEntry(IndexOf(Arg(1)), out RoSkillDisplayEntry hovered)) ShowClassicSkillDescribe(hovered, classicSkill.Position + new Vector2Int(100, 100));
                    classicSkill.Invalidate();
                    break;
                case "view":
                    OnClassicSkillClicked(new RoClassicChildLayout { Id = SkViewId });
                    break;
                case "info":
                    OnClassicSkillClicked(skInfo);
                    break;
                case "hotkey": {
                    int index = IndexOf(Arg(1));
                    if (SkTryEntry(index, out RoSkillDisplayEntry dragged) && dragged.Level > 0 && !dragged.Passive) {
                        RoClassicHotkeyBar.SweepDropClassicPayload(Arg(2), SkPayload(dragged));
                    }

                    break;
                }
            }

            var plan = new System.Text.StringBuilder();
            IReadOnlyList<int> order = model.PlannedOrder;
            for (int i = 0; i < order.Count; i++) plan.Append(order[i]).Append(' ');
            return $"open={IsOpen} tab={SkTabLabel(skTab)} tabs={skTabs.Count} list={skListView} entries={SkCurrentEntries.Count} points={model.SkillPoints} " +
                $"remaining={SkRemaining} plan=[{plan.ToString().Trim()}] prereq={skPrereq.Count} describe={(skDescribe != null && skDescribe.Visible ? skDescribedId.ToString(CultureInfo.InvariantCulture) : "hidden")}";
        }
    }
}
