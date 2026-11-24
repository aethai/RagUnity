using System.Text;
using Ragnarok.UI.Classic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// The Esc menu, UIEscOptionWnd 0x9b (ui/RUNTIME-RULES-OPTIONS.md §2): "Game Options (code)" with five buttons alive;
    /// dead, "Return to last save point" and "Return to game", with "Resurrection" on top when a resurrection item is
    /// held. The layout is picked when the menu opens and kept until it closes; the openers (Esc, the menu icon, the
    /// death) toggle it. The buttons send CZ_RESTART / CZ_STANDING_RESURRECTION through the shell.
    /// </summary>
    public sealed class RoClassicEscMenu {
        private const int EscW = 280;
        private const int EscButtonX = 29;
        private const int EscCharSelectId = 0x16f;
        private const int EscGameSettingsId = 0x1c6;
        private const int EscShortcutId = 0x16e;
        private const int EscExitId = 0xf7;
        private const int EscReturnId = 0xb5;
        private const int EscResurrectId = 0x15d;
        private const int EscSavePointId = 0x170;
        private const string EscUi = "유저인터페이스\\";

        // OnCreate 0x880780: every button 221×20 at (29, 22 + 23k) in the order of its layout
        private static readonly (int Id, string Bitmap)[] EscAlive = {
            (EscCharSelectId, "esc_01"), (EscGameSettingsId, "esc_001"), (EscShortcutId, "esc_08"), (EscExitId, "esc_09"), (EscReturnId, "esc_02"),
        };
        private static readonly (int Id, string Bitmap)[] EscDead = { (EscSavePointId, "esc_10"), (EscReturnId, "esc_02") };
        private static readonly (int Id, string Bitmap)[] EscDeadResurrection = {
            (EscResurrectId, "esc_05"), (EscSavePointId, "esc_10"), (EscReturnId, "esc_02"),
        };
        private static readonly (int Id, string Bitmap)[] EscAll = {
            (EscCharSelectId, "esc_01"), (EscGameSettingsId, "esc_001"), (EscShortcutId, "esc_08"), (EscExitId, "esc_09"),
            (EscReturnId, "esc_02"), (EscResurrectId, "esc_05"), (EscSavePointId, "esc_10"),
        };

        private static readonly Color32 EscTitleColor = new Color32(0, 0, 0, 255);
        private static readonly Color32 EscTitleShadow = new Color32(255, 255, 255, 255);

        private static RoClassicEscMenu instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoMinimalLoginShell shell;
        private RoClassicWindow window;
        private bool shown;
        private bool deathMenuMode;
        private bool deathMenuHasSiegfried;
        private string title = string.Empty;

        public static bool IsOpen => instance != null && instance.shown;

        /// <summary>The menu was opened for the dead character (the save point / resurrection layout).</summary>
        public static bool IsDeathMenuMode => instance != null && instance.deathMenuMode;

        /// <summary>
        /// Opens the menu in the layout for <paramref name="dead"/> (MakeWindow 0x9b); an open one closes instead (the
        /// client's openers toggle).
        /// </summary>
        public static void Toggle(RoMinimalLoginShell loginShell, bool dead, bool hasSiegfried = false) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || loginShell == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicEscMenu { system = system };
            RoClassicEscMenu m = instance;
            m.shell = loginShell;
            if (dead) {
                m.deathMenuMode = true;
                m.deathMenuHasSiegfried = hasSiegfried;
            }

            if (m.shown) {
                m.Hide();
                return;
            }

            m.Show(dead);
        }

        /// <summary>Keeps the resurrection button's availability for the next open (the open layout stays as it is).</summary>
        public static void RefreshDeathMenuSiegfried(bool hasSiegfried) {
            if (instance != null && instance.deathMenuMode) instance.deathMenuHasSiegfried = hasSiegfried;
        }

        /// <summary>Alive again (or leaving the map): back to the alive layout.</summary>
        public static void ResetDeathMenu() {
            if (instance == null) return;
            instance.deathMenuMode = false;
            instance.deathMenuHasSiegfried = false;
        }

        public static void Close() => instance?.Hide();

        private void Show(bool dead) {
            EnsureWindow();
            (int Id, string Bitmap)[] buttons = !dead ? EscAlive : deathMenuHasSiegfried ? EscDeadResurrection : EscDead;
            int h = 17 + 23 * buttons.Length + 7;  // 139 / 70 / 93
            window.Layout.Ops.Clear();
            RoClassicFrames.TitleBar(window.Layout.Ops, EscW, true);
            RoClassicFrames.Body(window.Layout.Ops, EscW, h);
            window.SetHeight(h);
            foreach (RoClassicChildLayout child in window.Layout.Children) {
                int k = System.Array.FindIndex(buttons, b => b.Id == child.Id);
                window.SetChildVisible(child, k >= 0);
                window.SetChildPosition(child.Path, k >= 0 ? EscButtonX : -100, k >= 0 ? 22 + 23 * k : -100);
            }

            // OnDraw 0x88ce30: sprintf("%s (%s)", MsgStr(0x5cb), Encode(GID))
            RoLoginShellSession session = shell.ClassicSession;
            int gid = session != null ? session.BasicInfoSnapshot().Character.CharacterId : 0;
            title = RoClassicUiHost.MsgStr(0x5cb, "Game Options") + " (" + EncodeCharacterId(gid) + ")";

            // re-created on every open at (scaledX(185), scaledY(300)): no position memory
            Vector2Int client = system.ClientSize;
            window.MoveTo(Mathf.Clamp((client.x - 640) / 2 + 185, 0, Mathf.Max(0, client.x - EscW)),
                Mathf.Clamp(300 * client.y / 480, 0, Mathf.Max(0, client.y - h)));
            window.Visible = true;
            window.Invalidate();
            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
            shown = true;
        }

        private void Hide() {
            if (!shown) return;
            shown = false;
            if (window != null) {
                window.Visible = false;
                system.Windows.RemoveEsc(window);
            }
        }

        private void EnsureWindow() {
            if (window != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIEscOptionWnd", WindowId = 0x9b, Width = EscW, Height = 139 };
            foreach ((int id, string bitmap) in EscAll) {
                var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = bitmap, X = -100, Y = -100, W = 221, H = 20 };
                button.States["normal"] = EscUi + bitmap + "a.bmp";
                button.States["hover"] = EscUi + bitmap + "b.bmp";
                button.States["pressed"] = EscUi + bitmap + "c.bmp";
                layout.Children.Add(button);
            }

            // a plain UIFrameWnd: dragged by its frame, no default button (Enter does nothing), in the Esc list
            window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = _ => true };
            window.DrawDynamic += w => {
                w.TextOut(19, 4, title, 0, 12, EscTitleShadow);
                w.TextOut(18, 3, title, 0, 12, EscTitleColor);
            };
            window.Clicked += OnClicked;
            window.EscClose = () => {
                Hide();
                return true;
            };
        }

        /// <summary>SendMsg 0x8965d0 (msg 6): the button's action.</summary>
        private void OnClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case EscCharSelectId:
                    Hide();
                    shell.TrySendCharacterSelectRestartRequest();      // CZ_RESTART type 1
                    break;
                case EscGameSettingsId:
                    ToggleGameSettings();                              // toggles window 0x271e; the menu stays
                    break;
                case EscShortcutId:
                    ToggleShortcutSettings();                          // toggles window 0x9c; the menu stays
                    break;
                case EscExitId:
                    Hide();
                    ExitToWindows();
                    break;
                case EscReturnId:
                    Hide();
                    break;
                case EscResurrectId:
                    Hide();
                    shell.TrySendStandingResurrectionRequest();        // CZ_STANDING_RESURRECTION
                    break;
                case EscSavePointId:
                    // MessageBox(MsgStr(0x60c), Yes/No): Yes → CZ_RESTART type 0, the menu closes; No → nothing
                    RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x60c, "Do you really want to go back to your savepoint?"),
                        RoClassicMessageBox.TypeOkCancel, true, result => {
                            if (result != RoClassicMessageBox.IdYes) return;
                            Hide();
                            shell.TrySendReturnToSavePointRequest();
                        });
                    break;
            }
        }

        private void ToggleGameSettings() => RoClassicGameSettings.Toggle(shell.ClassicClientSettings);

        private void ToggleShortcutSettings() => RoClassicShortcutSettings.Toggle(shell.ClassicClientSettings);

        private static void ExitToWindows() {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Encode 0xce6100: the GID's decimal digits without leading zeros, each through 0123456789 → 3867012495, with a '-'
        /// before the hundreds digit when something precedes it (150000 → "813-333").
        /// </summary>
        public static string EncodeCharacterId(int gid) {
            if (gid <= 0) return string.Empty;
            const string map = "3867012495";
            string digits = gid.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var s = new StringBuilder(digits.Length + 1);
            for (int i = 0; i < digits.Length; i++) {
                if (i == digits.Length - 3 && i > 0) s.Append('-');
                s.Append(map[digits[i] - '0']);
            }

            return s.ToString();
        }

        // ---- diagnostics console (RoSkillSweepRunner "!esc ...") ----

        /// <summary>open | dead | deadres | click id | state.</summary>
        internal static string Sweep(RoMinimalLoginShell loginShell, string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            switch (a[0]) {
                case "open":
                    if (!IsOpen) Toggle(loginShell, false);
                    break;
                case "dead":
                case "deadres":
                    Close();
                    Toggle(loginShell, true, a[0] == "deadres");
                    break;
                case "click":
                    int id = int.Parse(a[1].StartsWith("0x") ? a[1].Substring(2) : a[1],
                        a[1].StartsWith("0x") ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture);
                    instance?.OnClicked(new RoClassicChildLayout { Id = id });
                    break;
            }

            var s = new StringBuilder();
            RoClassicEscMenu m = instance;
            s.Append("shown=").Append(IsOpen).Append(" title='").Append(m?.title).Append('\'');
            if (m?.window != null) {
                s.Append(" pos=").Append(m.window.Position).Append(" h=").Append(m.window.Height).Append(" buttons=");
                foreach (RoClassicChildLayout child in m.window.Layout.Children) {
                    if (child.Visible) s.Append(child.Path).Append(' ');
                }
            }

            return s.ToString();
        }
    }
}
