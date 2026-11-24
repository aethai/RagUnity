using System;
using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Gameplay;
using Ragnarok.UI.Classic;
using ROIO.Models;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// UIChooseWarpWnd 0x27 (codex-ui-research RUNTIME-RULES-QUESTS-EMOTIONS-NOTIFICATIONS.md "notices-warp"): the warp /
    /// teleport destination list of ZC_WARPLIST (0x0abe → 0xcaa640), 280 × 120 at (W/2 − 120, H·5/8), the sysbox frame, title
    /// "Select an Area to Warp", the list (12, 26, 256, 65; UIListBox 0x7ec990: #F0F0F0, rows 16 px, selection #CADFFB) with
    /// "Random" shown as MsgStr 0xd4, the appended "cancel" as 0xda and maps by their table names. OK sends CZ_SELECT_WARPPOINT
    /// with the selected raw name (nothing without a selection), Cancel with "cancel"; Enter is OK, the arrows move the
    /// selection, Esc does not close it (a special id). Fed by the legacy RoTeleportMenuWindow.
    /// </summary>
    public sealed class RoClassicWarpList : IRoClassicUiProducer {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string CancelName = "cancel";
        private const int OkId = 0xb4;
        private const int CancelId = 0xb5;
        private const int RowH = 16;
        private static readonly RectInt List = new RectInt(12, 26, 256, 65);
        private static readonly Color32 ListBg = new Color32(0xf0, 0xf0, 0xf0, 255);
        private static readonly Color32 SelectedBg = new Color32(0xca, 0xdf, 0xfb, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        private static RoClassicWarpList instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private RoLoginShellSession session;
        private readonly List<string> raw = new List<string>();
        private readonly List<string> shown = new List<string>();
        private ushort skillId;
        private int selected = -1;
        private int top;
        private int shownSequence = -1;
        private bool sending;

        /// <summary>Per sync of the legacy window: true when the classic list owns the teleport menu.</summary>
        internal static bool Sync(RoLoginShellSession session, RoAssetDatabase assets, RoTeleportMenuSnapshot teleport) {

            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return false;
            if (instance == null || instance.system != system) {
                instance = new RoClassicWarpList { system = system };
                system.Register(instance);
            }

            RoClassicWarpList w = instance;
            w.session = session;
            teleport ??= RoTeleportMenuSnapshot.Empty;
            if (!teleport.IsActive) {
                w.Close();
                w.shownSequence = -1;
                return true;
            }

            if (teleport.Sequence != w.shownSequence) {
                w.shownSequence = teleport.Sequence;
                w.Open(teleport, assets);
            }

            return true;
        }

        private void Open(RoTeleportMenuSnapshot teleport, RoAssetDatabase assets) {
            if (window == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIChooseWarpWnd").text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIListBox") child.Visible = false; // drawn per state
                }

                window = new RoClassicWindow(system, layout) { Visible = false };
                window.DrawDynamic += Draw;
                window.Clicked += child => {
                    if (child.Id == OkId) Ok();
                    else if (child.Id == CancelId) Send(CancelName);
                };
                window.Pressed += (w, local) => Select(local);
                window.DoubleClicked += (w, local) => {
                    if (Select(local)) Ok();
                };
                window.Wheeled += (w, local, delta) => Scroll(delta > 0 ? -1 : 1);
            }

            skillId = teleport.SkillId;
            raw.Clear();
            shown.Clear();
            foreach (string name in teleport.MapNames ?? Array.Empty<string>()) {
                string trimmed = name?.Trim() ?? string.Empty;
                if (trimmed.Length == 0) continue;
                raw.Add(trimmed);
                shown.Add(DisplayName(trimmed, assets));
            }

            // 0xcaa640 appends "cancel" after the server's names
            raw.Add(CancelName);
            shown.Add(RoClassicUiHost.MsgStr(0xda, "Cancel"));
            selected = -1;
            top = 0;
            sending = false;
            Vector2Int client = system.ClientSize;
            window.MoveTo(Mathf.Clamp(client.x / 2 - 120, 0, Math.Max(0, client.x - window.Width)), Mathf.Clamp(client.y * 5 / 8, 0, Math.Max(0, client.y - window.Height)));
            window.Visible = true;
            system.Windows.BringToFront(window);
            window.Invalidate();
        }

        /// <summary>vf37 0x859300 msg 0x1b: "Random" → MsgStr 0xd4, "cancel" → 0xda, a map → its mapnametable name.</summary>
        public static string DisplayName(string rawName, RoAssetDatabase assets) {
            if (string.Equals(rawName, "Random", StringComparison.OrdinalIgnoreCase)) return RoClassicUiHost.MsgStr(0xd4, "Random Area");
            if (string.Equals(rawName, CancelName, StringComparison.OrdinalIgnoreCase)) return RoClassicUiHost.MsgStr(0xda, "Cancel");
            string name = rawName.Replace('\\', '/');
            int slash = name.LastIndexOf('/');
            if (slash >= 0) name = name.Substring(slash + 1);
            string bare = name.EndsWith(".gat", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 4)
                : name;
            RoAssetTables tables = assets != null && assets.IsInitialized ? assets.Tables : null;
            if (tables != null) {
                foreach (string key in new[] { name, bare, bare + ".rsw", bare + ".gat" }) {
                    if (tables.TryGetMapTable(key, out MapTableStruct table) && !string.IsNullOrWhiteSpace(table.name)) return table.name;
                }
            }

            return bare;
        }

        private int VisibleRows => List.height / RowH;

        private void Draw(RoClassicWindow w) {
            w.FillRect(List.x, List.y, List.width, List.height, ListBg);
            w.ClipTo(List.x, List.y, List.width, List.height);
            int rows = VisibleRows;
            for (int i = top; i < shown.Count && i < top + rows; i++) {
                int y = List.y + RowH * (i - top);
                if (i == selected) w.FillRect(List.x, y, List.width, 15, SelectedBg);
                w.TextOut(List.x + 3, y, shown[i], 0, 12, Black);
            }

            if (shown.Count > rows) w.DrawScrollBar(List.x + List.width - 13, List.y, List.height, shown.Count - rows, shown.Count, rows, top);
            w.ResetClip();
        }

        private bool Select(Vector2Int local) {
            if (!List.Contains(local)) return false;
            int i = (local.y - List.y) / RowH + top;
            if (i >= shown.Count) return false;
            selected = i;
            window.Invalidate();
            return true;
        }

        private void Scroll(int rows) {
            int next = Mathf.Clamp(top + rows, 0, Math.Max(0, shown.Count - VisibleRows));
            if (next == top) return;
            top = next;
            window.Invalidate();
        }

        private void Move(int delta) {
            if (shown.Count == 0) return;
            selected = Mathf.Clamp(selected < 0 ? (delta > 0 ? 0 : shown.Count - 1) : selected + delta, 0, shown.Count - 1);
            if (selected < top) top = selected;
            if (selected >= top + VisibleRows) top = selected - VisibleRows + 1;
            window.Invalidate();
        }

        /// <summary>OK: only with a selected row (list +0x94 != −1); the raw name goes out.</summary>
        private void Ok() {
            if (selected < 0 || selected >= raw.Count) return;
            Send(raw[selected]);
        }

        private void Send(string mapName) {
            if (sending || session == null) return;
            sending = true;
            Close();
            ushort skill = skillId;
            _ = SendAsync(skill, mapName);
        }

        private async System.Threading.Tasks.Task SendAsync(ushort skill, string mapName) {
            try {
                await session.BeginLegacySelectWarpPoint(skill, mapName);
            } catch (Exception e) {
                Debug.LogWarning("Warp list request was not accepted: " + e.Message);
                session.ClearLegacyTeleportMenuSelection();
            } finally {
                sending = false;
            }
        }

        private void Close() {
            if (window != null) window.Visible = false;
        }

        /// <summary>Manager +0x20c: Enter sends msg 0 (OK), the arrows msg 0x12 / 0x13 to the list.</summary>
        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (window == null || !window.Visible || RoClassicMessageBox.ModalOpen || RoClassicTextInput.HasFocus) return;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Ok();
            else if (keyboard.upArrowKey.wasPressedThisFrame) Move(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Move(1);
#endif
        }

        /// <summary>The sweep console: pick i | ok | cancel | state.</summary>
        public static string Sweep(string command) {
            RoClassicWarpList w = instance;
            if (w == null || w.window == null) return "no warp list";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            switch (a[0]) {
                case "pick":
                    if (int.TryParse(a.Length > 1 ? a[1] : string.Empty, out int i) && i >= 0 && i < w.shown.Count) {
                        w.selected = i;
                        w.window.Invalidate();
                    }

                    break;
                case "ok":
                    w.Ok();
                    break;
                case "cancel":
                    w.Send(CancelName);
                    break;
            }

            return "visible=" + w.window.Visible + " skill=" + w.skillId + " selected=" + w.selected + " rows=" + string.Join(" | ", w.shown) + " raw=" + string.Join(",", w.raw);
        }
    }
}
