using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Maps;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// The Game Settings window, CUIGameSettingsUI 0x271e (ui/RUNTIME-RULES-OPTIONS.md §5, RUNTIME-RULES-CUI.md):
    /// 400×350, tabs Basic / Effect / Control / Graphics over 400×288 pages, its CUI controls (radio buttons, check boxes,
    /// sliders, combo boxes) drawn and hit-tested here. Everything off the Graphics tab applies on click; the Graphics tab
    /// stages until Apply. Additions of this client: the UI Scale / Anti-Aliasing groups under Trilinear Filtering. Toggled
    /// by the Esc menu's button; the values are the shell's settings model, the UI scale goes through the UI root's scale
    /// manager (the scale the classic UI draws at).
    /// </summary>
    public sealed class RoClassicGameSettings {
        /// <summary>The screen the Graphics tab reads its current size from (a fake one in tests).</summary>
        public interface IScreenSettingsAdapter {
            int Width { get; }
            int Height { get; }
            bool FullScreen { get; }
            void SetResolution(int width, int height, bool fullScreen);
        }

        private sealed class UnityScreenSettingsAdapter : IScreenSettingsAdapter {
            public int Width => Screen.width;
            public int Height => Screen.height;
            public bool FullScreen => Screen.fullScreen;

            public void SetResolution(int width, int height, bool fullScreen) {
                Screen.SetResolution(width, height, fullScreen);
            }
        }

        private const float UiScaleSliderStep = 0.05f;
        private const int GsW = 400;
        private const int GsH = 350;
        private const int GsPageY = 34;          // the pages: 400×288 at (0, 34)
        private const int GsTabsY = 17;
        private const int GsRowH = 16;
        private const int GsDropRows = 5;
        private const string GsUi = "유저인터페이스\\";
        private const string GsBasic = GsUi + "basic_interface\\";
        private const string GsPrefs = "ro.ui.classic.gamesettings.";
        private const int GsBaseId = 1;
        private const int GsCloseXId = 2;
        private const int GsResetId = 3;
        private const int GsAdditionalId = 4;
        private const int GsApplyId = 5;
        private const int GsCloseId = 6;
        private const int GsScaleSteps = 31;     // 0.75 … 2.25 by 0.05

        private static readonly RoCameraAntialiasingMode[] AntialiasingModes = {
            RoCameraAntialiasingMode.None,
            RoCameraAntialiasingMode.Fxaa,
            RoCameraAntialiasingMode.SmaaLow,
            RoCameraAntialiasingMode.SmaaMedium,
            RoCameraAntialiasingMode.SmaaHigh
        };
        private static readonly string[] AntialiasingLabels = {
            "Off",
            "FXAA",
            "SMAA Low",
            "SMAA Medium",
            "SMAA High"
        };

        private static readonly Color32 GsBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 GsWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 GsFrameLine = new Color32(0xc8, 0xc8, 0xc8, 255);
        private static readonly Color32 GsTabLine = new Color32(0xc3, 0xc3, 0xc3, 255);
        private static readonly Color32 GsPageLine = new Color32(0xce, 0xce, 0xce, 255);
        private static readonly Color32 GsComboLine = new Color32(0xc6, 0xc6, 0xc6, 255);
        private static readonly Color32 GsRowSelected = new Color32(0xce, 0xde, 0xff, 255);
        private static readonly Color32 GsRowHover = new Color32(0x73, 0x9c, 0xef, 255);
        private static readonly (int Id, string Fallback)[] GsTabNames = {
            (0x102e, "Basic"), (0x102f, "Effect"), (0x1030, "Control"), (0x1031, "Graphics"),
        };

        // ---- the controls (window coordinates; group y + page y 34) ----

        private abstract class GsItem {
            public int Tab;
            public int X;
            public int Y;
        }

        private sealed class GsLabel : GsItem {
            public int W;
            public int MsgId;
            public string Fallback;
            public bool Bold;
            public Func<string> Dynamic;
        }

        /// <summary>CUIRadioButton 12×12 / CUICheckBox 10×10: toggles on press, only the box itself takes the click.</summary>
        private sealed class GsToggle : GsItem {
            public bool Radio;
            public Func<bool> Checked;
            public Action Press;
            public int Size => Radio ? 12 : 10;
        }

        /// <summary>CUIHScrollBar: arrows 13×13 at both ends, the thumb visible·track/total wide.</summary>
        private sealed class GsSlider : GsItem {
            public int W;
            public int Total;
            public int Visible;
            public Func<int> Get;
            public Action<int> Set;
            public Func<bool> Grey;
            public int Max => Total - Visible;
            public int Track => W - 26;
            public int ThumbW => Math.Max(1, (int) ((float) (Visible * Track) / Total));
            public int ThumbX => (int) ((float) Track / Total * Get() + 13);
        }

        /// <summary>The combo box: 182×18 (162×16 item at (1, 1), the 18×18 drop button at (164, 0)).</summary>
        private sealed class GsCombo : GsItem {
            public Func<IReadOnlyList<string>> Items;
            public Func<int> Get;
            public Action<int> Pick;
            public int Scroll;   // the list's scroll position in pixels
        }

        // the Graphics tab's values: the staged ones (shown) and the applied ones (Apply shows while they differ)
        private struct GsGraphics : IEquatable<GsGraphics> {
            public int Resolution;
            public bool Windowed;
            public bool Trilinear;
            public int Sprite;
            public int Texture;
            public bool AutoScale;
            public int Scale;
            public int Antialiasing;
            public int Refresh;      // index into gsRefreshHz (0 = Desktop)
            public bool VSync;
            public int FrameLimit;   // index into RoDisplaySettings.FrameLimits

            public bool Equals(GsGraphics o) =>
                Resolution == o.Resolution && Windowed == o.Windowed && Trilinear == o.Trilinear && Sprite == o.Sprite &&
                Texture == o.Texture && AutoScale == o.AutoScale && Scale == o.Scale && Antialiasing == o.Antialiasing &&
                Refresh == o.Refresh && VSync == o.VSync && FrameLimit == o.FrameLimit;
        }

        // this client's frame rows (Refresh Rate, VSync, Frame Limit) make the Graphics page three rows taller
        private const int GsGraphicsExtra = 96;

        private static RoClassicGameSettings instance;
        private static IScreenSettingsAdapter screenAdapter = new UnityScreenSettingsAdapter();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            screenAdapter = new UnityScreenSettingsAdapter();
        }

        private RoClassicUiSystem system;
        private RoClientSettingsModel settings;
        private readonly List<GsItem> gsItems = new List<GsItem>();
        private GsGraphics gsStaged;
        private GsGraphics gsApplied;
        private readonly List<string> gsSkins = new List<string>();
        private readonly List<string> gsApis = new List<string>();
        private readonly List<string> gsDevices = new List<string>();
        private readonly List<string> gsResolutions = new List<string>();
        private readonly List<Vector2Int> gsResolutionSizes = new List<Vector2Int>();
        private readonly List<string> gsAntialiasing = new List<string>();
        private readonly List<string> gsRefreshRates = new List<string>();
        private readonly List<int> gsRefreshHz = new List<int>();
        private readonly List<string> gsFrameLimits = new List<string>();

        private RoClassicWindow gsWindow;
        private RoClassicWindow gsDrop;
        private bool gsShown;
        private int gsTab;
        private RoClientSettingsSnapshot gsSnap;

        // the press being followed: an arrow (a step on its release), a slider thumb (dragged), a combo (toggles on release)
        private GsItem gsPressItem;
        private int gsPressPart;     // slider: 1 left arrow, 2 right arrow, 3 thumb; combo: 1
        private int gsThumbDownX;    // thumb-local x of the press
        private GsCombo gsOpenCombo;
        private int gsDropHover = -1;

        /// <summary>The PlayerPrefs key of the emblem border option ([0x015556bc]); /frame toggles the same value.</summary>
        public const string EmblemBorderKey = GsPrefs + "emblemBorder";

        private static int GsEmblem {
            get => PlayerPrefs.GetInt(EmblemBorderKey, 0);
            set {
                PlayerPrefs.SetInt(EmblemBorderKey, value);
                RoClassicCommandFlags.Reload();
            }
        }

        private static int GsRodexBlock {
            get => PlayerPrefs.GetInt(GsPrefs + "rodexBlock", 0);
            set => PlayerPrefs.SetInt(GsPrefs + "rodexBlock", value);
        }

        private static int GsPriority {
            get => PlayerPrefs.GetInt(GsPrefs + "priority", 0x20);
            set => PlayerPrefs.SetInt(GsPrefs + "priority", value);
        }

        public static bool IsOpen => instance != null && instance.gsShown;

        /// <summary>The settings model the window edits (the one of its last open), else the shell's.</summary>
        internal static RoClientSettingsModel Settings => instance?.settings ?? RoClassicUiHost.ClientSettings;

        /// <summary>Tests: the screen the Graphics tab reads (null = Unity's).</summary>
        public static void SetScreenAdapterForTests(IScreenSettingsAdapter adapter) {
            screenAdapter = adapter ?? new UnityScreenSettingsAdapter();
        }

        // ---- open / close ----

        /// <summary>The Esc menu's button (window 0x271e): an open window closes, else it opens on the Basic tab.</summary>
        public static void Toggle(RoClientSettingsModel clientSettings) {
            if (IsOpen) {
                Close();
                return;
            }

            Open(clientSettings);
        }

        /// <summary>
        /// MakeWindow(0x271e): the window on the Basic tab, where the player left it or centred; already open, it stays as
        /// it is.
        /// </summary>
        public static void Open(RoClientSettingsModel clientSettings) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicGameSettings { system = system };
            RoClassicGameSettings g = instance;
            g.Bind(clientSettings);
            if (!g.gsShown) g.Show();
        }

        public static void Close() => instance?.Hide();

        /// <summary>0x9dba20 → 0x98df70: another window changed a value (the Command List); the open window shows it.</summary>
        internal static void RefreshValues() {
            RoClassicGameSettings g = instance;
            if (g == null || !g.gsShown) return;
            g.gsSnap = g.Snapshot();
            g.gsWindow?.Invalidate();
        }

        /// <summary>Per frame (RoClassicGameUi): the thumb drag, the list's hover and the press outside it while open.</summary>
        public static void Tick() {
            RoClassicGameSettings g = instance;
            if (g == null || !g.gsShown) return;
            g.Update();
        }

        private void Bind(RoClientSettingsModel clientSettings) {
            RoClientSettingsModel previous = settings;
            settings = clientSettings ?? settings ?? new RoClientSettingsModel();
            BindUiScaleManager(!ReferenceEquals(previous, settings));
        }

        private void Show() {
            EnsureWindow();
            RefreshGameSettingsLists();
            gsTab = 0;
            ApplyGsTabButtons();
            Vector2Int client = system.ClientSize;
            // Localize 0x98b310: centred ((W − 400)/2, (H − 350)/2) unless TblPos[10014] holds a position
            int x = PlayerPrefs.GetInt(GsPrefs + "x", (client.x - GsW) / 2), y = PlayerPrefs.GetInt(GsPrefs + "y", (client.y - GsH) / 2);
            gsWindow.MoveTo(Mathf.Clamp(x, 0, Mathf.Max(0, client.x - GsW)), Mathf.Clamp(y, 0, Mathf.Max(0, client.y - GsH)));
            gsWindow.Visible = true;
            gsWindow.Invalidate();
            system.Windows.BringToFront(gsWindow);
            system.Windows.PushEsc(gsWindow);
            gsShown = true;
        }

        private void Hide() {
            if (!gsShown) return;
            gsShown = false;
            CloseGsDrop();
            gsPressItem = null;
            if (gsWindow != null) {
                gsWindow.Visible = false;
                system.Windows.RemoveEsc(gsWindow);
            }
        }

        private void EnsureWindow() {
            if (gsWindow != null) return;
            var layout = new RoClassicWindowLayout { Class = "CUIGameSettingsUI", WindowId = 0x271e, Width = GsW, Height = GsH };
            BuildGsFrame(layout.Ops, GsH);

            layout.Children.Add(GsButton(GsBaseId, "base", 3, 3, 11, 11, GsBasic + "sys_base_off.bmp", GsBasic + "sys_base_on.bmp", GsBasic + "sys_base_on.bmp"));
            layout.Children.Add(GsButton(GsCloseXId, "closex", 386, 3, 11, 11, GsBasic + "sys_close_off.bmp", GsBasic + "sys_close_on.bmp", GsBasic + "sys_close_on.bmp"));
            layout.Children.Add(GsButton(GsResetId, "reset", 5, 326, 42, 20, GsUi + "btn_reset.bmp", GsUi + "btn_reset_a.bmp", GsUi + "btn_reset_b.bmp"));
            layout.Children.Add(GsButton(GsAdditionalId, "additional", 52, 326, 90, 20,
                GsUi + "btn_addsetting_normal.bmp", GsUi + "btn_addsetting_over.bmp", GsUi + "btn_addsetting_press.bmp"));
            layout.Children.Add(GsButton(GsApplyId, "apply", 301, 326, 42, 20, GsUi + "btn_apply.bmp", GsUi + "btn_apply_a.bmp", GsUi + "btn_apply_b.bmp"));
            layout.Children.Add(GsButton(GsCloseId, "close", 348, 326, 42, 20, GsUi + "btn_close.bmp", GsUi + "btn_close_a.bmp", GsUi + "btn_close_b.bmp"));

            gsWindow = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = local => local.y < 17 };
            gsWindow.DrawDynamic += DrawClassicGameSettings;
            gsWindow.Clicked += OnGsButtonClicked;
            gsWindow.Pressed += (w, local) => OnGsPressed(local);
            gsWindow.Released += (w, local) => OnGsReleased(local);
            gsWindow.Wheeled += (w, local, delta) => OnGsWheel(local, delta);
            gsWindow.Moved += w => {
                PlayerPrefs.SetInt(GsPrefs + "x", w.Position.x);
                PlayerPrefs.SetInt(GsPrefs + "y", w.Position.y);
                CloseGsDrop();
            };
            gsWindow.EscClose = () => {
                Hide();
                return true;
            };

            // the drop-down box (CUIDropDownBox, one for every combo, above everything)
            var dropLayout = new RoClassicWindowLayout { Class = "CUIDropDownBox", WindowId = 0, Width = 177, Height = 2 + GsRowH };
            gsDrop = new RoClassicWindow(system, dropLayout) { Visible = false, Draggable = false, FrameDragArea = _ => false };
            gsDrop.DrawDynamic += DrawGsDrop;
            gsDrop.Pressed += (w, local) => OnGsDropPressed(local);
            gsDrop.Wheeled += (w, local, delta) => ScrollGsDrop(delta > 0 ? -GsRowH : GsRowH);

            BuildGsItems();
        }

        private static RoClassicChildLayout GsButton(int id, string path, int x, int y, int w, int h, string normal, string hover, string pressed) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            button.States["normal"] = normal;
            button.States["hover"] = hover;
            button.States["pressed"] = pressed;
            return button;
        }

        // ---- the groups (OnCreate of each GameSettingsUI::CUIGroup*, RUNTIME-RULES-OPTIONS.md §5c–§5f) ----

        private void BuildGsItems() {
            gsItems.Clear();
            // Basic: Skin (0, 36), Sound (36, 60), Emblem (96, 36), RODEX (132, 60), process priority (192, 36)
            GsAddLabel(0, 19, 46, 75, 0x5d9, "Skin", true);
            GsAddCombo(0, 110, 43, () => gsSkins, () => 0, i => { });
            GsAddLabel(0, 19, 82, 75, 0x5cd, "Audio Settings", true);
            GsAddLabel(0, 110, 82, 50, 0x5d7, "    BGM");
            GsAddSlider(0, 152, 81, 174, 135, 8, () => Mathf.RoundToInt(gsSnap.BgmVolume * 127f), p => GsSet(() => settings.SetBgmVolume(p / 127f)), () => gsSnap.BgmMuted);
            GsAddCheck(0, 334, 83, () => gsSnap.BgmMuted, v => GsSet(() => settings.SetBgmMuted(v)));
            GsAddLabel(0, 348, 82, 50, 0x1034, "Off");
            GsAddLabel(0, 110, 106, 50, 0x5d8, "    SFX");
            GsAddSlider(0, 152, 105, 174, 135, 8, () => Mathf.RoundToInt(gsSnap.EffectsVolume * 127f), p => GsSet(() => settings.SetEffectsVolume(p / 127f)), () => gsSnap.EffectsMuted);
            GsAddCheck(0, 334, 107, () => gsSnap.EffectsMuted, v => GsSet(() => settings.SetEffectsMuted(v)));
            GsAddLabel(0, 348, 106, 50, 0x1034, "Off");
            GsAddLabel(0, 19, 142, 85, 0x1032, "Emblem Border", true);
            GsAddRadio(0, 110, 142, () => GsEmblem == 1, () => SetGsEmblem(1));
            GsAddLabel(0, 127, 142, 50, 0x1035, "Show");
            GsAddRadio(0, 205, 142, () => GsEmblem == 0, () => SetGsEmblem(0));
            GsAddLabel(0, 222, 142, 100, 0x1036, "Hide");
            GsAddLabel(0, 19, 178, 70, 0xc40, "Mail", true);
            GsAddRadio(0, 110, 178, () => GsRodexBlock == 0, () => GsSet(() => GsRodexBlock = 0));
            GsAddLabel(0, 127, 178, 100, 0x1038, "Receive RODEX");
            GsAddRadio(0, 110, 202, () => GsRodexBlock == 1, () => GsSet(() => GsRodexBlock = 1));
            GsAddLabel(0, 127, 202, 130, 0x1037, "Block RODEX");
            GsAddLabel(0, 19, 238, 100, 0x1039, "Default Priority", true);
            GsAddRadio(0, 110, 238, () => GsPriority == 0x80, () => SetGsPriority(0x80));
            GsAddLabel(0, 127, 238, 100, 0x103a, "High");
            GsAddRadio(0, 205, 238, () => GsPriority == 0x20, () => SetGsPriority(0x20));
            GsAddLabel(0, 222, 238, 100, 0x103c, "Normal");
            GsAddRadio(0, 300, 238, () => GsPriority == 0x40, () => SetGsPriority(0x40));
            GsAddLabel(0, 317, 238, 100, 0x103e, "Low");

            // Effect: eight radio pairs, 28 high
            GsEffectPair(0, 75, 0x1040, "Show Aura", 0x1035, "Show", 0x1036, "Hide", () => gsSnap.ShowAura, v => settings.SetShowAura(v));
            GsEffectPair(1, 100, 0x1041, "Aura Display", 0x1042, "Original", 0x1043, "Simple Aura", () => !gsSnap.SimpleAura, v => settings.SetSimpleAura(!v));
            GsEffectPair(2, 100, 0xc64, "Skill Effects", 0x1035, "Show", 0x1036, "Hide", () => gsSnap.ShowSkillEffects, v => settings.SetShowSkillEffects(v));
            GsEffectPair(3, 100, 0xc65, "Shadow", 0x1035, "Show", 0x1036, "Hide", () => gsSnap.ShowShadow, v => settings.SetShowShadow(v));
            GsEffectPair(4, 100, 0x1044, "Damage Display", 0x1045, "Expand", 0x1046, "Default", () => gsSnap.ExpandDamageDisplay, v => settings.SetExpandDamageDisplay(v));
            GsEffectPair(5, 100, 0x1047, "Map Display on movement", 0x1048, "Show Map Name", 0x1049, "Do not show Map Name", () => gsSnap.ShowMapName, v => settings.SetShowMapName(v));
            GsEffectPair(6, 100, 0x104a, "Fog Effect", 0x1035, "Show", 0x1036, "Hide", () => gsSnap.FogEffect, v => settings.SetFogEffect(v));
            GsEffectPair(7, 100, 0x104b, "Footprint Effect", 0x104c, "Show all", 0x1011, "Show footprints", () => gsSnap.ShowAllFootprints, v => settings.SetShowAllFootprints(v));

            // Control: five groups, 32 high, title (19, 16), check box (105, 17), label (122, 16)
            GsAddLabel(2, 19, 50, 75, 0x104d, "Mouse Snap", true);
            GsAddCheck(2, 105, 51, () => gsSnap.MouseSnapMonster, v => GsSet(() => settings.SetMouseSnapMonster(v)));
            GsAddLabel(2, 122, 50, 85, 0x104e, "Monster");
            GsAddCheck(2, 200, 51, () => gsSnap.MouseSnapSkill, v => GsSet(() => settings.SetMouseSnapSkill(v)));
            GsAddLabel(2, 217, 50, 85, 0x104f, "Skill");
            GsAddCheck(2, 295, 51, () => gsSnap.MouseSnapPickupItem, v => GsSet(() => settings.SetMouseSnapPickupItem(v)));
            GsAddLabel(2, 312, 50, 100, 0x1050, "Pickup Item");
            GsControlCheck(1, 75, 0x1051, "No Ctrl", 250, 0x1052, "Enables to auto attack without holding Ctrl.", () => gsSnap.NoCtrl, v => settings.SetNoCtrl(v));
            GsControlCheck(2, 100, 0x1053, "No Shift", 250, 0x1054, "Enables to cast healing skills on monsters without holding Shift.", () => gsSnap.NoShift, v => settings.SetNoShift(v));
            GsControlCheck(3, 75, 0xdb5, "Zoom Out", 250, 0x1055, "When zoomed out, the screen appears slightly wider", () => gsSnap.ZoomOut, v => settings.SetZoomOut(v));
            GsControlCheck(4, 130, 0xc6a, "Enable", 270, 0x1056, "In windowed mode, the mouse cursor does not go out of the window.", () => gsSnap.WindowCursorLock, v => settings.SetWindowCursorLock(v));

            // Graphics: API (0), device + resolution (32, 64), sprite (96), texture (128), window mode (160), trilinear (192);
            // this client's UI scale (224) and anti-aliasing (256)
            GsAddLabel(3, 19, 46, 130, 0x1001, "Graphics API", true);
            GsAddCombo(3, 150, 43, () => gsApis, () => 0, _ => { });
            GsAddLabel(3, 19, 78, 130, 0xc6d, "Graphic Device", true);
            GsAddCombo(3, 150, 75, () => gsDevices, () => 0, _ => { });
            GsAddLabel(3, 19, 110, 130, 0xc6e, "Resolution", true);
            GsAddCombo(3, 150, 107, () => gsResolutions, () => gsStaged.Resolution, i => GsStage(() => gsStaged.Resolution = i));
            GsAddLabel(3, 19, 142, 130, 0x144, "Sprite Resolution", true);
            GsAddSlider(3, 153, 141, 175, 3, 1, () => gsStaged.Sprite, p => GsStage(() => gsStaged.Sprite = p), () => false);
            GsAddLabel(3, 19, 174, 130, 0x145, "Texture Resolution", true);
            GsAddSlider(3, 153, 173, 175, 3, 1, () => gsStaged.Texture, p => GsStage(() => gsStaged.Texture = p), () => false);
            GsAddLabel(3, 19, 206, 130, 0x1057, "Window Mode", true);
            GsAddRadio(3, 150, 206, () => !gsStaged.Windowed, () => GsStage(() => gsStaged.Windowed = false));
            GsAddLabel(3, 167, 206, 150, 0x1058, "Fullscreen");
            GsAddRadio(3, 270, 206, () => gsStaged.Windowed, () => GsStage(() => gsStaged.Windowed = true));
            GsAddLabel(3, 287, 206, 100, 0x1059, "Windowed");
            GsAddLabel(3, 19, 238, 130, 0x105a, "Trilinear Filtering", true);
            GsAddRadio(3, 150, 238, () => gsStaged.Trilinear, () => GsStage(() => gsStaged.Trilinear = true));
            GsAddLabel(3, 167, 238, 150, 0x1033, "On");
            GsAddRadio(3, 270, 238, () => !gsStaged.Trilinear, () => GsStage(() => gsStaged.Trilinear = false));
            GsAddLabel(3, 287, 238, 100, 0x1034, "Off");
            gsItems.Add(new GsLabel {
                Tab = 3, X = 19, Y = 270, W = 130, Bold = true,
                Dynamic = () => "UI Scale (" + Mathf.RoundToInt(GsScaleOf(gsStaged.Scale) * 100f).ToString(CultureInfo.InvariantCulture) + "%)",
            });
            GsAddSlider(3, 153, 269, 175, GsScaleSteps, 1, () => gsStaged.Scale, p => GsStage(() => gsStaged.Scale = p), () => gsStaged.AutoScale);
            GsAddCheck(3, 334, 271, () => gsStaged.AutoScale, v => GsStage(() => gsStaged.AutoScale = v));
            gsItems.Add(new GsLabel { Tab = 3, X = 348, Y = 270, W = 50, Dynamic = () => "Auto" });
            gsItems.Add(new GsLabel { Tab = 3, X = 19, Y = 302, W = 130, Bold = true, Dynamic = () => "Anti-Aliasing" });
            GsAddCombo(3, 150, 299, () => gsAntialiasing, () => gsStaged.Antialiasing, i => GsStage(() => gsStaged.Antialiasing = i));
            // the frame rows (RoDisplaySettings): the full screen refresh rate, VSync, the frame limit while VSync is off
            gsItems.Add(new GsLabel { Tab = 3, X = 19, Y = 334, W = 130, Bold = true, Dynamic = () => "Refresh Rate" });
            GsAddCombo(3, 150, 331, () => gsRefreshRates, () => gsStaged.Refresh, i => GsStage(() => gsStaged.Refresh = i));
            gsItems.Add(new GsLabel { Tab = 3, X = 19, Y = 366, W = 130, Bold = true, Dynamic = () => "VSync" });
            GsAddRadio(3, 150, 366, () => gsStaged.VSync, () => GsStage(() => gsStaged.VSync = true));
            GsAddLabel(3, 167, 366, 150, 0x1033, "On");
            GsAddRadio(3, 270, 366, () => !gsStaged.VSync, () => GsStage(() => gsStaged.VSync = false));
            GsAddLabel(3, 287, 366, 100, 0x1034, "Off");
            gsItems.Add(new GsLabel { Tab = 3, X = 19, Y = 398, W = 130, Bold = true, Dynamic = () => gsStaged.VSync ? "Frame Limit (VSync)" : "Frame Limit" });
            GsAddCombo(3, 150, 395, () => gsFrameLimits, () => gsStaged.FrameLimit, i => GsStage(() => gsStaged.FrameLimit = i));
        }

        private void GsAddLabel(int tab, int x, int y, int w, int msgId, string fallback, bool bold = false) =>
            gsItems.Add(new GsLabel { Tab = tab, X = x, Y = y, W = w, MsgId = msgId, Fallback = fallback, Bold = bold });

        private void GsAddRadio(int tab, int x, int y, Func<bool> isChecked, Action select) =>
            gsItems.Add(new GsToggle { Tab = tab, X = x, Y = y, Radio = true, Checked = isChecked, Press = () => { if (!isChecked()) select(); } });

        private void GsAddCheck(int tab, int x, int y, Func<bool> isChecked, Action<bool> set) =>
            gsItems.Add(new GsToggle { Tab = tab, X = x, Y = y, Checked = isChecked, Press = () => set(!isChecked()) });

        private void GsAddSlider(int tab, int x, int y, int w, int total, int visible, Func<int> get, Action<int> set, Func<bool> grey) =>
            gsItems.Add(new GsSlider { Tab = tab, X = x, Y = y, W = w, Total = total, Visible = visible, Get = get, Set = set, Grey = grey });

        private void GsAddCombo(int tab, int x, int y, Func<IReadOnlyList<string>> items, Func<int> get, Action<int> pick) =>
            gsItems.Add(new GsCombo { Tab = tab, X = x, Y = y, Items = items, Get = get, Pick = pick });

        /// <summary>An Effect page group (k-th, 28 high): title (19, 15), radio A (110, 15) + label (127, 15), B (205, 15) + (222, 15).</summary>
        private void GsEffectPair(int k, int titleW, int titleId, string title, int aId, string a, int bId, string b, Func<bool> aChecked, Action<bool> setA) {
            int y = GsPageY + 28 * k + 15;
            GsAddLabel(1, 19, y, titleW, titleId, title, true);
            GsAddRadio(1, 110, y, aChecked, () => GsSet(() => setA(true)));
            GsAddLabel(1, 127, y, 100, aId, a);
            GsAddRadio(1, 205, y, () => !aChecked(), () => GsSet(() => setA(false)));
            GsAddLabel(1, 222, y, 100, bId, b);
        }

        /// <summary>A Control page group (k-th, 32 high): title (19, 16), check box (105, 17), its text (122, 16).</summary>
        private void GsControlCheck(int k, int titleW, int titleId, string title, int textW, int textId, string text, Func<bool> get, Action<bool> set) {
            int y = GsPageY + 32 * k + 16;
            GsAddLabel(2, 19, y, titleW, titleId, title, true);
            GsAddCheck(2, 105, y + 1, get, v => GsSet(() => set(v)));
            GsAddLabel(2, 122, y, textW, textId, text);
        }

        // ---- values ----

        private RoClientSettingsSnapshot Snapshot() {
            return settings != null ? settings.Snapshot() : new RoClientSettingsModel().Snapshot();
        }

        private void GsSet(Action change) {
            change();
            gsSnap = Snapshot();
            gsWindow?.Invalidate();
        }

        private void GsStage(Action change) {
            change();
            ApplyGsTabButtons();
            gsWindow?.Invalidate();
        }

        private static float GsScaleOf(int step) => RoClientSettingsModel.MinUiScale + UiScaleSliderStep * step;

        /// <summary>Emblem border (0x15556bc) and the chat line MsgStr 0xdc7 / 0xdc8 in #FFFF64.</summary>
        private void SetGsEmblem(int value) {
            GsSet(() => GsEmblem = value);
            RoClassicChat.Instance?.AddLine(
                RoClassicUiHost.MsgStr(value == 1 ? 0xdc7 : 0xdc8, value == 1 ? "Emblem displayed with border" : "Emblem displayed without border"), "#FFFF64");
        }

        /// <summary>Process priority (0x15556cc): SetPriorityClass at once — HIGH 0x80, NORMAL 0x20, IDLE 0x40 (not in the Editor).</summary>
        private void SetGsPriority(int value) {
            GsSet(() => GsPriority = value);
#if !UNITY_EDITOR
            try {
                System.Diagnostics.Process.GetCurrentProcess().PriorityClass = value == 0x80
                    ? System.Diagnostics.ProcessPriorityClass.High
                    : value == 0x40 ? System.Diagnostics.ProcessPriorityClass.Idle : System.Diagnostics.ProcessPriorityClass.Normal;
            } catch (Exception e) {
                Debug.LogWarning("Process priority was not changed: " + e.Message);
            }
#endif
        }

        /// <summary>The window's Localize: every group's refresh (vf55), which fills and selects the lists.</summary>
        private void RefreshGameSettingsLists() {
            gsSnap = Snapshot();
            gsSkins.Clear();
            gsSkins.Add(RoClassicUiHost.MsgStr(0xc12, "<Basic Skin>"));
            gsApis.Clear();
            gsApis.Add(SystemInfo.graphicsDeviceType.ToString());
            gsDevices.Clear();
            gsDevices.Add(SystemInfo.graphicsDeviceName);

            // the modes at least 664 high, one per size, by width then height, "{w} x {h}"
            gsResolutionSizes.Clear();
            foreach (Resolution r in Screen.resolutions) {
                var size = new Vector2Int(r.width, r.height);
                if (r.height >= 664 && !gsResolutionSizes.Contains(size)) gsResolutionSizes.Add(size);
            }

            var current = new Vector2Int(screenAdapter.Width, screenAdapter.Height);
            if (gsResolutionSizes.Count == 0) gsResolutionSizes.Add(current);
            gsResolutionSizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            gsResolutions.Clear();
            foreach (Vector2Int size in gsResolutionSizes) gsResolutions.Add(size.x.ToString(CultureInfo.InvariantCulture) + " x " + size.y.ToString(CultureInfo.InvariantCulture));
            int resolution = gsResolutionSizes.IndexOf(current);
            if (resolution < 0) resolution = gsResolutionSizes.IndexOf(new Vector2Int(1024, 768));
            if (resolution < 0) resolution = 0;

            gsAntialiasing.Clear();
            gsAntialiasing.AddRange(AntialiasingLabels);

            // "Desktop" (borderless at the desktop's rate), then the display's rates; a saved rate the display lost is kept
            gsRefreshHz.Clear();
            gsRefreshHz.Add(0);
            gsRefreshHz.AddRange(RoDisplaySettings.AvailableRefreshRates());
            if (!gsRefreshHz.Contains(RoDisplaySettings.RefreshRate)) gsRefreshHz.Add(RoDisplaySettings.RefreshRate);
            gsRefreshRates.Clear();
            foreach (int hz in gsRefreshHz) gsRefreshRates.Add(hz == 0 ? "Desktop" : hz.ToString(CultureInfo.InvariantCulture) + " Hz");
            gsFrameLimits.Clear();
            foreach (int fps in RoDisplaySettings.FrameLimits) gsFrameLimits.Add(fps == 0 ? "Unlimited" : fps.ToString(CultureInfo.InvariantCulture) + " fps");
            int frameLimit = Array.IndexOf(RoDisplaySettings.FrameLimits, RoDisplaySettings.FrameLimit);
            if (frameLimit < 0) frameLimit = Array.IndexOf(RoDisplaySettings.FrameLimits, RoDisplaySettings.DefaultFrameLimit);

            gsApplied = new GsGraphics {
                Refresh = Math.Max(0, gsRefreshHz.IndexOf(RoDisplaySettings.RefreshRate)),
                VSync = RoDisplaySettings.VSync,
                FrameLimit = frameLimit,
                Resolution = resolution,
                Windowed = gsSnap.Windowed,
                Trilinear = gsSnap.TrilinearFiltering,
                Sprite = Mathf.Clamp(gsSnap.SpriteResolution, 0, 2),
                Texture = Mathf.Clamp(gsSnap.TextureResolution, 0, 2),
                AutoScale = gsSnap.UiScaleMode == RoUiScaleMode.Auto,
                Scale = Mathf.Clamp(Mathf.RoundToInt((gsSnap.EffectiveUiScale - RoClientSettingsModel.MinUiScale) / UiScaleSliderStep), 0, GsScaleSteps - 1),
                Antialiasing = Math.Max(0, Array.IndexOf(AntialiasingModes, gsSnap.CameraAntialiasing)),
            };
            gsStaged = gsApplied;

            // each group's refresh scrolls its list so the selection is the first visible row
            foreach (GsItem item in gsItems) {
                if (item is GsCombo combo) {
                    int count = combo.Items().Count;
                    combo.Scroll = Mathf.Clamp(GsRowH * combo.Get(), 0, Math.Max(0, count - GsDropRows) * GsRowH);
                }
            }
        }

        /// <summary>
        /// Apply 0x989c80: MsgStr 0xc5f "Set to these values?", Yes → the staged Graphics values take effect (here no
        /// value needs the client's restart: the resolution and window mode switch in place).
        /// </summary>
        private void ApplyGsGraphics() {
            RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0xc5f, "Set to these values?"), RoClassicMessageBox.TypeOkCancel, true, result => {
                if (result != RoClassicMessageBox.IdYes || !gsShown) return;
                GsGraphics s = gsStaged;
                if (s.Sprite != gsApplied.Sprite) settings.SetSpriteResolution(s.Sprite);
                if (s.Texture != gsApplied.Texture) settings.SetTextureResolution(s.Texture);
                if (s.Trilinear != gsApplied.Trilinear) settings.SetTrilinearFiltering(s.Trilinear);
                int hz = s.Refresh < gsRefreshHz.Count ? gsRefreshHz[s.Refresh] : 0;
                if (s.Refresh != gsApplied.Refresh || s.VSync != gsApplied.VSync || s.FrameLimit != gsApplied.FrameLimit) {
                    RoDisplaySettings.Set(s.VSync, RoDisplaySettings.FrameLimits[Mathf.Clamp(s.FrameLimit, 0, RoDisplaySettings.FrameLimits.Length - 1)], hz);
                }

                if (s.Resolution != gsApplied.Resolution || s.Windowed != gsApplied.Windowed || s.Refresh != gsApplied.Refresh) {
                    settings.SetWindowed(s.Windowed);
                    Vector2Int size = s.Resolution < gsResolutionSizes.Count ? gsResolutionSizes[s.Resolution] : new Vector2Int(screenAdapter.Width, screenAdapter.Height);
                    if (s.Windowed) Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                    // fullscreen = the borderless desktop mode, or exclusive full screen at a chosen refresh rate
                    else if (hz > 0) Screen.SetResolution(size.x, size.y, FullScreenMode.ExclusiveFullScreen, RoDisplaySettings.ToRefreshRate(hz));
                    else Screen.SetResolution(size.x, size.y, FullScreenMode.FullScreenWindow);
                }

                if (s.Antialiasing != gsApplied.Antialiasing && s.Antialiasing < AntialiasingModes.Length) SetAntialiasing(AntialiasingModes[s.Antialiasing]);
                if (s.AutoScale != gsApplied.AutoScale || (!s.AutoScale && s.Scale != gsApplied.Scale)) {
                    if (s.AutoScale) ResetUiScaleToAuto();
                    else SetManualUiScale(GsScaleOf(s.Scale));
                }

                gsApplied = s;
                gsSnap = Snapshot();
                ApplyGsTabButtons();
                gsWindow?.Invalidate();
            });
        }

        /// <summary>Reset 0x989d20: MsgStr 0xc5e, Yes → the Basic, Effect and Control groups' defaults (not Graphics).</summary>
        private void ResetGsPages() {
            RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0xc5e, "Set Basic Settings?"), RoClassicMessageBox.TypeOkCancel, true, result => {
                if (result != RoClassicMessageBox.IdYes || !gsShown) return;
                GsSet(() => {
                    settings.ResetBasicSettings();
                    settings.ResetEffectSettings();
                    settings.ResetControlSettings();
                    GsRodexBlock = 0;
                });
                SetGsEmblem(0);
                SetGsPriority(0x20);
            });
        }

        /// <summary>
        /// lambda_3 0x98df00 + UpdateApply 0x98cf00: Reset and Additional settings off the Graphics tab only; Apply on it
        /// only while a staged value differs from the applied one.
        /// </summary>
        /// <summary>CUIFrameWnd::OnDraw 0x93de90: white inside the #C8C8C8 rounded outline; then the title bar (a child
        /// surface, so its bitmaps compose keyed).</summary>
        private static void BuildGsFrame(List<RoClassicOp> ops, int h) {
            ops.Clear();
            RoClassicFrames.Fill(ops, 1, 1, GsW - 2, h - 2, GsWhite);
            foreach (RectInt r in RoClassicWindow.RoundOutlineRects(0, 0, GsW, h)) RoClassicFrames.Fill(ops, r.x, r.y, r.width, r.height, GsFrameLine);
            RoClassicFrames.Bitmap(ops, GsBasic + "titlebar_left.bmp", 0, 0, false);
            for (int x = 12; x < GsW - 12; x += 12) RoClassicFrames.Bitmap(ops, GsBasic + "titlebar_mid.bmp", x, 0, false);
            RoClassicFrames.Bitmap(ops, GsBasic + "titlebar_right.bmp", GsW - 12, 0, false);
        }

        /// <summary>The window is the original 400×350 on every tab but Graphics, which this client's frame rows make taller.</summary>
        private int GsHeightNow => gsTab == 3 ? GsH + GsGraphicsExtra : GsH;

        private void SizeGsWindow() {
            int h = GsHeightNow;
            if (gsWindow.Height == h) return;
            gsWindow.SetHeight(h);
            BuildGsFrame(gsWindow.Layout.Ops, h);
            foreach (RoClassicChildLayout child in gsWindow.Layout.Children) {
                if (child.Id == GsResetId || child.Id == GsAdditionalId || child.Id == GsApplyId || child.Id == GsCloseId) child.Y = h - 24;
            }

            Vector2Int client = system.ClientSize;
            gsWindow.MoveTo(Mathf.Clamp(gsWindow.Position.x, 0, Mathf.Max(0, client.x - GsW)), Mathf.Clamp(gsWindow.Position.y, 0, Mathf.Max(0, client.y - h)));
        }

        private void ApplyGsTabButtons() {
            if (gsWindow == null) return;
            SizeGsWindow();
            bool graphics = gsTab == 3;
            foreach (RoClassicChildLayout child in gsWindow.Layout.Children) {
                if (child.Id == GsResetId || child.Id == GsAdditionalId) gsWindow.SetChildVisible(child, !graphics);
                else if (child.Id == GsApplyId) gsWindow.SetChildVisible(child, graphics && !gsStaged.Equals(gsApplied));
            }

            gsWindow.Invalidate();
        }

        // ---- the applied graphics values: anti-aliasing on the map camera, the UI scale through the UI root ----

        private void SetAntialiasing(RoCameraAntialiasingMode mode) {
            settings.SetCameraAntialiasing(mode);
            RoRuntimeMapController map = UnityEngine.Object.FindFirstObjectByType<RoRuntimeMapController>();
            map?.SetCameraAntialiasingMode(mode, enablePostProcessingForAntialiasing: true);
        }

        /// <summary>The classic UI scale follows this settings model; on the first bind the saved scale is loaded into it.</summary>
        private void BindUiScaleManager(bool loadPersistentSettings) {
            if (settings != null) RoClassicUiScale.Bind(settings, loadPersistentSettings);
        }

        private void SetManualUiScale(float value) {
            if (!ReferenceEquals(RoClassicUiScale.Settings, settings)) BindUiScaleManager(loadPersistentSettings: false);
            RoClassicUiScale.SetManualScale(value);
        }

        private void ResetUiScaleToAuto() {
            if (!ReferenceEquals(RoClassicUiScale.Settings, settings)) BindUiScaleManager(loadPersistentSettings: false);
            RoClassicUiScale.ResetToAuto();
        }

        // ---- drawing ----

        private void DrawClassicGameSettings(RoClassicWindow w) {
            RoClassicGdiFont regular = w.FontFor(0, 12, false), bold = w.FontFor(0, 12, true);

            // the title (CUIStaticText (18, 0) 100×17, centred vertically: y 1, white shadow at +1)
            string title = RoClassicUiHost.MsgStr(0x946, "Game Settings");
            w.ClipTo(18, 0, 100, 17);
            w.TextOutPlain(19, 2, title, regular, GsWhite);
            w.TextOutPlain(18, 1, title, regular, GsBlack);
            w.ResetClip();

            // the page's #CECECE frame, then the tab bar over its first row (added after the pages)
            int pageH = 288 + (gsTab == 3 ? GsGraphicsExtra : 0);
            w.FillRect(0, GsPageY, GsW, 1, GsPageLine);
            w.FillRect(0, GsPageY + pageH - 1, GsW, 1, GsPageLine);
            w.FillRect(0, GsPageY, 1, pageH, GsPageLine);
            w.FillRect(GsW - 1, GsPageY, 1, pageH, GsPageLine);
            for (int t = 0; t < GsTabNames.Length; t++) DrawGsTab(w, regular, t);

            foreach (GsItem item in gsItems) {
                if (item.Tab != gsTab) continue;
                switch (item) {
                    case GsLabel label:
                        string text = label.Dynamic != null ? label.Dynamic() : RoClassicUiHost.MsgStr(label.MsgId, label.Fallback);
                        // a 13 px box with a 15 px cell at its top: the descenders are cut
                        w.ClipTo(label.X, label.Y, label.W, 13);
                        w.TextOutPlain(label.X, label.Y, text, label.Bold ? bold : regular, GsBlack);
                        w.ResetClip();
                        break;
                    case GsToggle toggle:
                        string bitmap = toggle.Radio
                            ? (toggle.Checked() ? "radiobtn_on.bmp" : "radiobtn_off.bmp")
                            : (toggle.Checked() ? "checkbox_1.bmp" : "checkbox_0.bmp");
                        w.DrawBitmap(toggle.X, toggle.Y, GsUi + bitmap);
                        break;
                    case GsSlider slider:
                        DrawGsSlider(w, slider);
                        break;
                    case GsCombo combo:
                        DrawGsCombo(w, regular, combo);
                        break;
                }
            }
        }

        /// <summary>CUITabButton::OnDraw (frame mode): #C3C3C3 rounded edges, the selected tab a row taller and open below.</summary>
        private void DrawGsTab(RoClassicWindow w, RoClassicGdiFont font, int t) {
            const int tw = 72, th = 18;
            int x = 71 * t, y = GsTabsY;
            bool selected = t == gsTab;
            if (selected) {
                w.FillRect(x + 1, y + 1, tw - 2, th - 1, GsWhite);
                w.FillRect(x + 2, y, tw - 4, 1, GsTabLine);
                w.FillRect(x + 1, y + 1, 1, 1, GsTabLine);
                w.FillRect(x + tw - 2, y + 1, 1, 1, GsTabLine);
                w.FillRect(x, y + 2, 1, th - 2, GsTabLine);
                w.FillRect(x + tw - 1, y + 2, 1, th - 2, GsTabLine);
            } else {
                w.FillRect(x + 1, y + 2, tw - 2, th - 3, GsWhite);
                w.FillRect(x + 2, y + 1, tw - 4, 1, GsTabLine);
                w.FillRect(x + 1, y + 2, 1, 1, GsTabLine);
                w.FillRect(x + tw - 2, y + 2, 1, 1, GsTabLine);
                w.FillRect(x, y + 3, 1, th - 3, GsTabLine);
                w.FillRect(x + tw - 1, y + 3, 1, th - 3, GsTabLine);
                w.FillRect(x, y + th - 1, tw, 1, GsTabLine);
            }

            // CUITextButton: integer centring (72 − width)/2, (18 − 15)/2; the selected text one pixel down-right
            string text = RoClassicUiHost.MsgStr(GsTabNames[t].Id, GsTabNames[t].Fallback);
            int d = selected ? 1 : 0;
            w.ClipTo(x, y, tw, th);
            w.TextOutPlain(x + (tw - font.Measure(text)) / 2 + d, y + (th - 15) / 2 + d, text, font, GsBlack);
            w.ResetClip();
        }

        /// <summary>CUIHScrollBar::OnDraw + CUIHThumb (three-part): grey (TintBlit mode 1) while its value is locked.</summary>
        private static void DrawGsSlider(RoClassicWindow w, GsSlider s) {
            bool grey = s.Grey();
            string Bmp(string name) => grey ? RoClassicUiImages.Tinted(GsUi + name, 1) : GsUi + name;
            int track = s.Track;
            for (int k = 0; 13 * k < track - 13; k++) w.DrawBitmap(s.X + 13 + 13 * k, s.Y, Bmp("scroll1mid.bmp"));
            w.DrawBitmap(s.X + s.W - 26, s.Y, Bmp("scroll1mid.bmp"));
            w.DrawBitmap(s.X, s.Y, Bmp("scroll1left.bmp"));
            w.DrawBitmap(s.X + s.W - 13, s.Y, Bmp("scroll1right.bmp"));
            if (s.Visible >= s.Total) return; // SetShow(visible < total)
            int tx = s.X + s.ThumbX, tw = s.ThumbW;
            w.ClipTo(tx, s.Y, tw, 13);
            w.DrawBitmap(tx, s.Y, Bmp("scroll1bar_left.bmp"));
            for (int x = 4; x < tw - 4; x += 4) w.DrawBitmap(tx + x, s.Y, Bmp("scroll1bar_mid.bmp"));
            w.DrawBitmap(tx + tw - 4, s.Y, Bmp("scroll1bar_right.bmp"));
            w.ResetClip();
        }

        /// <summary>CUIComboBox::OnDraw: white, a #C6C6C6 frame, the picked text at (4, 2) cut to 155 px, the drop button at (164, 0).</summary>
        private static void DrawGsCombo(RoClassicWindow w, RoClassicGdiFont font, GsCombo c) {
            w.FillRect(c.X, c.Y, 182, 18, GsWhite);
            w.FillRect(c.X, c.Y, 182, 1, GsComboLine);
            w.FillRect(c.X, c.Y + 17, 182, 1, GsComboLine);
            w.FillRect(c.X, c.Y, 1, 18, GsComboLine);
            w.FillRect(c.X + 181, c.Y, 1, 18, GsComboLine);
            IReadOnlyList<string> items = c.Items();
            int index = c.Get();
            if (index >= 0 && index < items.Count) {
                w.ClipTo(c.X + 4, c.Y + 1, 155, 16);
                w.TextOutPlain(c.X + 4, c.Y + 2, GsFit(items[index], font, 155), font, GsBlack);
                w.ResetClip();
            }

            w.DrawBitmap(c.X + 164, c.Y, GsBasic + "txtbox_btn_a.bmp");
        }

        /// <summary>The fit of CUIStaticText (0x9b1120): too wide → cut and "..." appended.</summary>
        private static string GsFit(string text, RoClassicGdiFont font, int width) {
            if (string.IsNullOrEmpty(text) || font.Measure(text) <= width) return text;
            for (int n = text.Length - 1; n > 0; n--) {
                string cut = text.Substring(0, n) + "...";
                if (font.Measure(cut) <= width) return cut;
            }

            return "...";
        }

        /// <summary>
        /// CUIDropDownBox (177 × rows·16 + 2, white, #C6C6C6 frame) with the list at (1, 1): rows 162×16 (selected
        /// #CEDEFF, hovered #739CEF), text at row (3, 1) cut to 155 px, the scroll0 bar at (163, 1) beyond 5 items.
        /// </summary>
        private void DrawGsDrop(RoClassicWindow w) {
            if (gsOpenCombo == null) return;
            IReadOnlyList<string> items = gsOpenCombo.Items();
            int rows = Math.Min(GsDropRows, items.Count), listH = rows * GsRowH;
            w.FillRect(0, 0, w.Width, w.Height, GsWhite);
            w.FillRect(0, 0, w.Width, 1, GsComboLine);
            w.FillRect(0, w.Height - 1, w.Width, 1, GsComboLine);
            w.FillRect(0, 0, 1, w.Height, GsComboLine);
            w.FillRect(w.Width - 1, 0, 1, w.Height, GsComboLine);
            RoClassicGdiFont font = w.FontFor(0, 12, false);
            int p = gsOpenCombo.Scroll, selected = gsOpenCombo.Get();
            w.ClipTo(1, 1, 175, listH);
            for (int k = 0; ; k++) {
                int index = p / GsRowH + k, y = 1 + k * GsRowH - p % GsRowH;
                if (index >= items.Count || y >= 1 + listH) break;
                if (index == selected) w.FillRect(1, y, 162, GsRowH, GsRowSelected);
                else if (index == gsDropHover) w.FillRect(1, y, 162, GsRowH, GsRowHover);
                w.ClipTo(4, Math.Max(1, y), 155, Math.Min(GsRowH, 1 + listH - y));
                w.TextOutPlain(4, y + 1, GsFit(items[index], font, 155), font, GsBlack);
                w.ClipTo(1, 1, 175, listH);
            }

            w.ResetClip();
            if (items.Count > GsDropRows) {
                // CUIVScrollBar at list (162, 0): arrows ±16, thumb visible/total·track high
                int bx = 163, by = 1, total = items.Count * GsRowH, track = listH - 26;
                for (int k = 0; 13 * k < track - 13; k++) w.DrawBitmap(bx, by + 13 + 13 * k, GsUi + "scroll0mid.bmp");
                w.DrawBitmap(bx, by + listH - 26, GsUi + "scroll0mid.bmp");
                w.DrawBitmap(bx, by, GsUi + "scroll0up.bmp");
                w.DrawBitmap(bx, by + listH - 13, GsUi + "scroll0down.bmp");
                int thumbH = Math.Max(1, (int) ((float) listH / total * track)), thumbY = (int) ((float) track / total * p + 13);
                w.ClipTo(bx, by + thumbY, 13, thumbH);
                w.DrawBitmap(bx, by + thumbY, GsUi + "scroll0bar_up.bmp");
                for (int y = 4; y < thumbH - 4; y += 4) w.DrawBitmap(bx, by + thumbY + y, GsUi + "scroll0bar_mid.bmp");
                w.DrawBitmap(bx, by + thumbY + thumbH - 4, GsUi + "scroll0bar_down.bmp");
                w.ResetClip();
            }
        }

        // ---- input ----

        private void OnGsButtonClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case GsCloseXId:
                case GsCloseId:
                    Hide();
                    break;
                case GsResetId:
                    ResetGsPages();
                    break;
                case GsApplyId:
                    ApplyGsGraphics();
                    break;
                case GsAdditionalId:
                    RoClassicCommandList.Toggle(settings); // window 0x70, the slash-command list
                    break;
            }
        }

        /// <summary>LBtnDown on the window (forwarded up from its controls).</summary>
        private void OnGsPressed(Vector2Int local) {
            gsPressItem = null;
            GsCombo combo = GsComboAt(local);
            // UIWindowMgr 0x9d5d00: a press anywhere but the list and its own combo closes the list
            if (gsOpenCombo != null && combo != gsOpenCombo) CloseGsDrop();

            // the tabs switch on press (later tabs lie over earlier ones)
            if (local.y >= GsTabsY && local.y < GsTabsY + 18) {
                for (int t = GsTabNames.Length - 1; t >= 0; t--) {
                    if (local.x < 71 * t || local.x >= 71 * t + 72) continue;
                    if (t != gsTab) {
                        gsTab = t;
                        ApplyGsTabButtons();
                    }

                    return;
                }
            }

            if (combo != null) {
                gsPressItem = combo; // toggles on release
                gsPressPart = 1;
                return;
            }

            foreach (GsItem item in gsItems) {
                if (item.Tab != gsTab) continue;
                if (item is GsToggle toggle && Inside(local, toggle.X, toggle.Y, toggle.Size, toggle.Size)) {
                    toggle.Press();
                    gsWindow.Invalidate();
                    return;
                }

                if (item is GsSlider slider && Inside(local, slider.X, slider.Y, slider.W, 13)) {
                    PressGsSlider(slider, local.x - slider.X);
                    return;
                }
            }
        }

        private void PressGsSlider(GsSlider s, int x) {
            if (x < 13) {
                gsPressItem = s;
                gsPressPart = 1;
            } else if (x >= s.W - 13) {
                gsPressItem = s;
                gsPressPart = 2;
            } else if (s.Visible < s.Total && x >= s.ThumbX && x < s.ThumbX + s.ThumbW) {
                gsPressItem = s;
                gsPressPart = 3;
                gsThumbDownX = x - s.ThumbX;
            } else if (s.Visible < s.Total) {
                // the track: one step towards the press, no repeat
                SetGsSlider(s, x < s.ThumbX ? s.Get() - 1 : s.Get() + 1);
            }
        }

        /// <summary>SetPos 0x947ab0: clamped to 0..total − visible, unchanged while the slider is locked.</summary>
        private void SetGsSlider(GsSlider s, int pos) {
            if (s.Grey()) return;
            pos = Mathf.Clamp(pos, 0, s.Max);
            if (pos == s.Get()) return;
            s.Set(pos);
            gsWindow.Invalidate();
        }

        private void OnGsReleased(Vector2Int local) {
            GsItem pressed = gsPressItem;
            int part = gsPressPart;
            gsPressItem = null;
            if (pressed is GsSlider s && part != 3 && Inside(local, s.X + (part == 1 ? 0 : s.W - 13), s.Y, 13, 13)) {
                SetGsSlider(s, s.Get() + (part == 1 ? -1 : 1)); // the arrows step on release
            } else if (pressed is GsCombo combo && GsComboAt(local) == combo) {
                // the box or its drop button (on release): an open list closes, else this one opens
                if (gsOpenCombo != null) CloseGsDrop();
                else OpenGsDrop(combo);
            }
        }

        private void OnGsWheel(Vector2Int local, int delta) {
            foreach (GsItem item in gsItems) {
                if (item.Tab == gsTab && item is GsSlider s && Inside(local, s.X, s.Y, s.W, 13)) {
                    SetGsSlider(s, s.Get() + (delta > 0 ? -1 : 1));
                    return;
                }
            }
        }

        private GsCombo GsComboAt(Vector2Int local) {
            foreach (GsItem item in gsItems) {
                if (item.Tab == gsTab && item is GsCombo combo && Inside(local, combo.X, combo.Y, 182, 18)) return combo;
            }

            return null;
        }

        private static bool Inside(Vector2Int p, int x, int y, int w, int h) => p.x >= x && p.y >= y && p.x < x + w && p.y < y + h;

        /// <summary>OpenPopup 0x9d3180: the drop-down at the combo's (0, 17), 177 × rows·16 + 2, on top.</summary>
        private void OpenGsDrop(GsCombo combo) {
            int count = combo.Items().Count;
            if (count == 0) return;
            gsOpenCombo = combo;
            gsDropHover = -1;
            gsDrop.SetHeight(Math.Min(GsDropRows, count) * GsRowH + 2);
            gsDrop.Layout.Height = Math.Min(GsDropRows, count) * GsRowH + 2;
            gsDrop.MoveTo(gsWindow.Position.x + combo.X, gsWindow.Position.y + combo.Y + 17);
            gsDrop.Visible = true;
            gsDrop.Invalidate();
            system.Windows.BringToFront(gsDrop);
        }

        private void CloseGsDrop() {
            if (gsOpenCombo == null) return;
            gsOpenCombo = null;
            if (gsDrop != null) gsDrop.Visible = false;
        }

        /// <summary>A row's LBtnDown picks it (the selected row again: nothing, the list stays); the scroll column scrolls.</summary>
        private void OnGsDropPressed(Vector2Int local) {
            if (gsOpenCombo == null) return;
            IReadOnlyList<string> items = gsOpenCombo.Items();
            int listH = Math.Min(GsDropRows, items.Count) * GsRowH;
            if (items.Count > GsDropRows && local.x >= 163) {
                int y = local.y - 1, total = items.Count * GsRowH, track = listH - 26;
                int thumbH = Math.Max(1, (int) ((float) listH / total * track)), thumbY = (int) ((float) track / total * gsOpenCombo.Scroll + 13);
                ScrollGsDrop(y < 13 || (y < thumbY && y < listH - 13) ? -GsRowH : y >= listH - 13 || y >= thumbY + thumbH ? GsRowH : 0);
                return;
            }

            int index = GsDropRowAt(local);
            if (index < 0 || index >= items.Count || index == gsOpenCombo.Get()) return;
            GsCombo combo = gsOpenCombo;
            CloseGsDrop();
            combo.Pick(index);
            gsWindow?.Invalidate();
        }

        private int GsDropRowAt(Vector2Int local) {
            if (gsOpenCombo == null || local.x < 1 || local.x >= 163 || local.y < 1) return -1;
            int listH = Math.Min(GsDropRows, gsOpenCombo.Items().Count) * GsRowH;
            if (local.y >= 1 + listH) return -1;
            return (local.y - 1 + gsOpenCombo.Scroll) / GsRowH;
        }

        private void ScrollGsDrop(int pixels) {
            if (gsOpenCombo == null || pixels == 0) return;
            int count = gsOpenCombo.Items().Count;
            if (count <= GsDropRows) return;
            gsOpenCombo.Scroll = Mathf.Clamp(gsOpenCombo.Scroll + pixels, 0, (count - GsDropRows) * GsRowH);
            gsDropHover = -1;
            gsDrop.Invalidate();
        }

        /// <summary>Per frame while open: the thumb drag, the list's hover and the press outside it.</summary>
        private void Update() {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2Int m = system.Windows.Mouse;
            if (gsPressItem is GsSlider s && gsPressPart == 3) {
                if (mouse.leftButton.isPressed) {
                    // 0x948440: the thumb-local offset from the press, in units as it is (step 1)
                    int dx = m.x - gsWindow.Position.x - s.X - s.ThumbX - gsThumbDownX;
                    if (dx != 0) SetGsSlider(s, s.Get() + dx);
                } else {
                    gsPressItem = null;
                }
            } else if (gsPressItem != null && !mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame) {
                gsPressItem = null; // released outside the window
            }

            if (gsOpenCombo != null) {
                RoClassicWindow over = system.Windows.HoverWindow;
                if (mouse.leftButton.wasPressedThisFrame && over != gsDrop && over != gsWindow) {
                    CloseGsDrop();
                } else if (over == gsDrop) {
                    int row = GsDropRowAt(m - gsDrop.Position);
                    if (row >= 0 && row != gsDropHover && row != gsOpenCombo.Get()) {
                        gsDropHover = row;
                        gsDrop.Invalidate();
                    }
                }
            }
        }

        // ---- diagnostics console (RoSkillSweepRunner "!gs ...") ----

        /// <summary>open | tab t | press x y | release x y | click x y | wheel x y d | pick i | button id | state (x, y window-local).</summary>
        internal static string Sweep(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length ? int.Parse(a[i], CultureInfo.InvariantCulture) : 0;
            if (a[0] == "open") {
                if (!IsOpen) Open(Settings);
            } else if (instance != null && instance.gsWindow != null) {
                RoClassicGameSettings g = instance;
                switch (a[0]) {
                    case "tab":
                        g.OnGsPressed(new Vector2Int(71 * Arg(1) + 30, GsTabsY + 9));
                        break;
                    case "press":
                        g.OnGsPressed(new Vector2Int(Arg(1), Arg(2)));
                        break;
                    case "release":
                        g.OnGsReleased(new Vector2Int(Arg(1), Arg(2)));
                        break;
                    case "click":
                        g.OnGsPressed(new Vector2Int(Arg(1), Arg(2)));
                        g.OnGsReleased(new Vector2Int(Arg(1), Arg(2)));
                        break;
                    case "wheel":
                        g.OnGsWheel(new Vector2Int(Arg(1), Arg(2)), Arg(3));
                        break;
                    case "pick":
                        if (g.gsOpenCombo != null) g.OnGsDropPressed(new Vector2Int(20, 1 + Arg(1) * GsRowH - g.gsOpenCombo.Scroll + 4));
                        break;
                    case "button":
                        g.OnGsButtonClicked(new RoClassicChildLayout { Id = Arg(1) });
                        break;
                }
            }

            RoClassicGameSettings gs = instance;
            if (gs == null) return "shown=False (open it: !gs open, or from the Esc menu: !esc click 0x1c6)";
            var s = new StringBuilder();
            s.Append("shown=").Append(gs.gsShown).Append(" tab=").Append(gs.gsTab);
            if (gs.gsWindow != null) s.Append(" pos=").Append(gs.gsWindow.Position);
            s.Append(" drop=").Append(gs.gsOpenCombo != null ? string.Join("|", gs.gsOpenCombo.Items()) + " scroll=" + gs.gsOpenCombo.Scroll : "closed");
            s.Append(" apply=").Append(!gs.gsStaged.Equals(gs.gsApplied));
            if (gs.gsSnap != null) {
                s.Append(" bgm=").Append(Mathf.RoundToInt(gs.gsSnap.BgmVolume * 127f)).Append(gs.gsSnap.BgmMuted ? "(off)" : string.Empty)
                    .Append(" aura=").Append(gs.gsSnap.ShowAura).Append(" snap=").Append(gs.gsSnap.MouseSnapMonster)
                    .Append(" staged=").Append(gs.gsStaged.Resolution).Append('/').Append(gs.gsStaged.Windowed ? "win" : "full").Append('/')
                    .Append(gs.gsStaged.Sprite).Append('/').Append(gs.gsStaged.Texture).Append('/').Append(gs.gsStaged.AutoScale ? "auto" : GsScaleOf(gs.gsStaged.Scale).ToString("0.00", CultureInfo.InvariantCulture))
                    .Append('/').Append(gs.gsStaged.Antialiasing);
            }

            return s.ToString();
        }
    }
}
