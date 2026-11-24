using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The quest overlay UIQuestDisplay 0xcc (codex-ui-research RUNTIME-RULES-QUESTS-EMOTIONS-NOTIFICATIONS.md "Tracker 0xcc"):
    /// 250 × 400 at (screen width − 320, 150), shown by the persisted QUESTDISPWNDINFO flag (the list's Show Quest box). The
    /// draw (vf20 0x8aec10) walks the quests by id and takes those with the active byte and the display byte 1, at most
    /// five: the title #E1D600 at (0, y), the summary white at (0, y + 15), each hunt with a required count white at
    /// (10, y + 28 + 13i) as "%s ( %d / %d )" or "%s ( Complete )" (0x7ee) — every text with a black shadow at +1. A block is
    /// 40 px without objectives (or more than three), 50 with one, 80 with two or three. Right-button-up (vf33) on a block's
    /// first 11 rows opens Quest information 0x656 (detail 0x2719) / Delete 0x15f (CZ_ACTIVE_QUEST with the active byte
    /// negated — not a deletion).
    /// </summary>
    public sealed class RoClassicQuestTracker : IRoClassicUiProducer {
        public const int WindowId = 0xcc;
        private const int Width = 250, Height = 400, MaxBlocks = 5;
        private static readonly Color32 Title = new Color32(0xe1, 0xd6, 0x00, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        private static RoClassicQuestTracker instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        /// <summary>Quest information (0x656): the ongoing detail of that quest.</summary>
        public static Action<uint> OpenDetail;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicWindow window;
        private int seenSequence = int.MinValue;
        private bool placed;
        private RoQuestRuntimeEntrySnapshot[] quests = Array.Empty<RoQuestRuntimeEntrySnapshot>();

        public static bool Visible {
            get => RoQuestTrackingPreferences.IsTrackerVisible;
            set {
                RoQuestTrackingPreferences.SetTrackerVisible(value);
                instance?.Sync(true);
            }
        }

        /// <summary>A display byte changed: the overlay draws again.</summary>
        public static void Refresh() => instance?.Sync(true);

        public static void Tick(RoLoginShellSession session) {
            if (session == null) return;
            RoClassicQuestTracker t = Ensure();
            if (t == null) return;
            t.session = session;
            t.Sync(false);
        }

        private static RoClassicQuestTracker Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicQuestTracker { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Re-reads the quests when the session's quest sequence moves (or on demand) and follows the visible flag.</summary>
        public void Sync(bool force) {
            if (session == null) return;
            EnsureWindow();
            bool visible = Visible;
            if (window.Visible != visible) {
                window.Visible = visible;
                force = true;
            }

            int sequence = session.QuestWindowChangeSequence;
            if (!force && sequence == seenSequence) return;
            seenSequence = sequence;
            RoQuestRuntimeEntrySnapshot[] all = session.QuestSnapshot().Quests ?? Array.Empty<RoQuestRuntimeEntrySnapshot>();
            quests = (RoQuestRuntimeEntrySnapshot[]) all.Clone();
            // the client's quest tree is a std::map by quest id
            Array.Sort(quests, (a, b) => a.QuestId.CompareTo(b.QuestId));
            window.Invalidate();
        }

        private void EnsureWindow() {
            if (window != null) return;
            var layout = new RoClassicWindowLayout { Class = "UIQuestDisplay", WindowId = WindowId, Width = Width, Height = Height };
            window = new RoClassicWindow(system, layout) { Visible = false, Draggable = false, ClickableAt = local => BlockAt(local.y) >= 0 };
            window.DrawDynamic += Draw;
            window.RightClicked += (w, local) => OpenMenu(BlockAt(local.y));
            if (!placed) {
                placed = true;
                window.MoveTo(Math.Max(0, system.ClientSize.x - 320), 150);
            }
        }

        private void Draw(RoClassicWindow w) {
            RoQuestMetadataTable metadata = RoClassicQuestData.Metadata();
            int y = 0, drawn = 0;
            foreach (RoQuestRuntimeEntrySnapshot quest in quests) {
                if (drawn >= MaxBlocks) break;
                if (quest.ActiveRaw != 1 || !RoClassicQuestData.IsDisplayed(session, quest.QuestId)) continue;
                drawn++;
                RoQuestMetadata info = metadata.TryGetQuest(quest.QuestId, out RoQuestMetadata found) ? found : null;
                string title = info != null && !string.IsNullOrEmpty(info.Title)
                    ? info.Title
                    : "Unknown Quest, ID = " + quest.QuestId.ToString(CultureInfo.InvariantCulture);
                Shadowed(w, 0, y, RoClassicQuestData.StripColours(title), Title);
                string summary = RoClassicQuestData.StripColours(info?.Summary);
                if (summary.Length > 0) Shadowed(w, 0, y + 15, summary, White);
                RoQuestObjectiveRuntimeSnapshot[] objectives = quest.Objectives ?? Array.Empty<RoQuestObjectiveRuntimeSnapshot>();
                int count = objectives.Length;
                for (int i = 0; i < Math.Min(3, count); i++) {
                    RoQuestObjectiveRuntimeSnapshot o = objectives[i];
                    if (o.Total <= 0) continue;
                    string label = RoClassicQuestData.StripColours(RoClassicQuestData.HuntLabel(o));
                    string text = o.Current == o.Total
                        ? label + " ( " + RoClassicUiHost.MsgStr(0x7ee, "Complete") + " )"
                        : label + " ( " + o.Current.ToString(CultureInfo.InvariantCulture) + " / " + o.Total.ToString(CultureInfo.InvariantCulture) + " )";
                    Shadowed(w, 10, y + 28 + 13 * i, text, White);
                }

                y += count == 0 || count > 3 ? 40 : count == 1 ? 50 : 80;
            }
        }

        private static void Shadowed(RoClassicWindow w, int x, int y, string text, Color32 color) {
            w.TextOut(x + 1, y + 1, text, 0, 12, Black);
            w.TextOut(x, y, text, 0, 12, color);
        }

        /// <summary>
        /// vf33's hit test: up to five records with nonzero active and display bytes, each band [blockY, blockY + 10]; a block
        /// with two or more objectives counts 80 px here (the draw gives more than three only 40).
        /// </summary>
        private int BlockAt(int localY) {
            int y = 0, seen = 0;
            for (int k = 0; k < quests.Length && seen < MaxBlocks; k++) {
                RoQuestRuntimeEntrySnapshot quest = quests[k];
                if (quest.ActiveRaw == 0 || !RoClassicQuestData.IsDisplayed(session, quest.QuestId)) continue;
                seen++;
                if (localY >= y && localY <= y + 10) return k;
                int count = quest.Objectives?.Length ?? 0;
                y += count == 0 ? 40 : count == 1 ? 50 : 80;
            }

            return -1;
        }

        /// <summary>Msg 0x31: UIMenuWnd with Quest information (0x31) and Delete (0x30) beside the pointer.</summary>
        private void OpenMenu(int k) {
            if (k < 0 || k >= quests.Length) return;
            RoQuestRuntimeEntrySnapshot quest = quests[k];
            RoClassicMenu.Begin();
            RoClassicMenu.Add(RoClassicUiHost.MsgStr(0x656, "Quest information"));
            RoClassicMenu.Add(RoClassicUiHost.MsgStr(0x15f, "Delete"));
            Vector2Int mouse = system.Windows.Mouse;
            RoClassicMenu.Open(mouse.x, mouse.y, row => {
                if (row == 0) OpenDetail?.Invoke(quest.QuestId);
                else if (row == 1) _ = session?.BeginQuestActive(quest.QuestId, quest.ActiveRaw == 0);
            });
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
        }

        /// <summary>The sweep console: show | hide | menu n | pick row | state.</summary>
        public static string Sweep(string command) {
            RoClassicQuestTracker t = Ensure();
            if (t == null || t.session == null) return "no tracker";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int arg = a.Length > 1 && int.TryParse(a[1], out int v) ? v : 0;
            switch (a[0]) {
                case "show":
                    Visible = true;
                    break;
                case "hide":
                    Visible = false;
                    break;
                case "menu":
                    t.OpenMenu(t.BlockAt(arg));
                    break;
            }

            var parts = new List<string>();
            foreach (RoQuestRuntimeEntrySnapshot q in t.quests) {
                parts.Add(q.QuestId + (q.ActiveRaw != 0 ? "+" : "-") + (RoClassicQuestData.IsDisplayed(t.session, q.QuestId) ? "d" : string.Empty) + "#" + (q.Objectives?.Length ?? 0));
            }

            return "visible=" + (t.window != null && t.window.Visible) + " quests=" + string.Join(" ", parts);
        }
    }
}
