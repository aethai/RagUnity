using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// CMergeItemWnd 0xe3 (codex-ui-research RUNTIME-RULES-ITEM-SERVICES.md "0xe3 — item merge"): ZC_MERGE_ITEM_OPEN
    /// (FUN_00c909e0) opens it with the offered stacks, an empty offer sends CZ_CANCEL_MERGE_ITEM at once. The top-left
    /// starts at (W / 2, H / 2); 195 × 155, resizable in 35 px steps to 5..10 columns (195..370) and 3..6 rows (155..260).
    /// OnDraw 0x73c800: title MsgStr 0x879 "Item Merge" (sys_base), white from y 17, itemwin_mid cells at
    /// (10 + 35c, 20 + 35r), each offered item's icon at the cell + (5, 2) and its checkbox (checkbox_0 / checkbox_1) at
    /// + (0, 20), btnbar2. A click on a cell toggles its checkbox, the hover shows MsgStr 0xb7 "%s: %d ea.", the wheel and
    /// the scrollbar (W − 15, 17, H − 38) scroll rows. OK (FUN_0073d460) sends CZ_REQ_MERGE_ITEM with the checked indexes
    /// when two or more are checked, else prints 0x87a in red and stays; Cancel and X send CZ_CANCEL_MERGE_ITEM and close.
    /// ZC_ACK_MERGE_ITEM (FUN_00c8a030) prints 0x87b in COLORREF 0x64ffff (merged), 0x87c / 0x87d in red, and closes it.
    /// </summary>
    public sealed class RoClassicMerge : IRoClassicUiProducer {
        private const string Layout = "Ragnarok/Generated/OriginalClient/ui/CMergeItemWnd";
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const int OkId = 0xb4;
        private const int CancelId = 0xb5;
        private const int CloseId = 0xc5;
        private const int Pitch = 35, Top = 20, Left = 10, Right = 10, Bottom = 30;
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        private sealed class Entry {
            public short BagIndex;
            public bool Checked;
        }

        private static RoClassicMerge instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private RoClassicWindow window;
        private readonly List<Entry> entries = new List<Entry>();
        private int firstRow;
        private int seenOpen = -1;
        private int seenResult = -1;

        public static void Tick(RoLoginShellSession session, RoClassicChat chat, RoItemMetadataTable metadata) {
            if (session == null) return;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (instance == null || instance.system != system) {
                instance = new RoClassicMerge { system = system };
                system.Register(instance);
            }

            RoClassicMerge m = instance;
            (RoZcMergeItemOpen open, int openSerial, RoZcAckMergeItem result, int resultSerial) = session.MergeSnapshot();
            if (!ReferenceEquals(m.session, session)) {
                m.session = session;
                m.seenOpen = openSerial;
                m.seenResult = resultSerial;
            }

            m.chat = chat;
            m.metadata = metadata ?? RoItemMetadataTable.Empty;
            if (openSerial != m.seenOpen) {
                m.seenOpen = openSerial;
                m.Open(open);
            }

            if (resultSerial != m.seenResult) {
                m.seenResult = resultSerial;
                m.Result(result);
            }
        }

        private void Open(RoZcMergeItemOpen open) {
            short[] indexes = open.BagIndexes ?? Array.Empty<short>();
            if (indexes.Length == 0) {
                // FUN_00c909e0: nothing to merge → the cancel goes out at once, no window
                _ = session.BeginLegacyMergeItem(null);
                return;
            }

            Ensure();
            entries.Clear();
            foreach (short index in indexes) entries.Add(new Entry { BagIndex = index });
            firstRow = 0;
            Vector2Int client = system.ClientSize;
            ApplySize(195, 155);
            // factory 0x9ce890: W·0.5, H·0.5 truncated
            window.MoveTo(Mathf.Clamp(client.x / 2, 0, Math.Max(0, client.x - window.Width)), Mathf.Clamp(client.y / 2, 0, Math.Max(0, client.y - window.Height)));
            window.Visible = true;
            system.Windows.BringToFront(window);
            window.Invalidate();
        }

        /// <summary>FUN_00c8a030: the message, then the window goes whatever the reason.</summary>
        private void Result(RoZcAckMergeItem result) {
            switch (result.Reason) {
                case 0:
                    Chat(RoClassicUiHost.MsgStr(0x87b, "Item merge is successful."), "#FFFF64");
                    break;
                case 1:
                    Chat(RoClassicUiHost.MsgStr(0x87c, "Combining items will be only one kind at a time."), "#FF0000");
                    break;
                case 2:
                    Chat(RoClassicUiHost.MsgStr(0x87d, "You cannot have more than 30,000 stacked items."), "#FF0000");
                    break;
            }

            if (window != null) window.Visible = false;
        }

        private void Chat(string text, string color) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, color);
        }

        private void Ensure() {
            if (window != null) return;
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layout).text);
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UIScrollBar" || child.Class == "UIResizer") child.Visible = false;
            }

            window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == OkId) Ok();
                else if (child.Id == CancelId || child.Id == CloseId) Cancel();
            };
            window.Pressed += (w, local) => {
                // vf25 0x73cad0: below the title a click on an offered cell toggles its checkbox
                if (local.y < 17) return;
                int k = EntryAt(local);
                if (k < 0) return;
                entries[k].Checked = !entries[k].Checked;
                w.Invalidate();
            };
            window.Wheeled += (w, local, delta) => Scroll(delta > 0 ? -1 : 1);
            window.ResizerModeAt = local =>
                local.x >= window.Width - 15 && local.x < window.Width + 1 && local.y >= window.Height - 15 && local.y < window.Height + 1 ? 7 : -1;
            window.ResizeRequested += (w, mode, width, height) => {
                if (mode == 7) ApplySize(width, height);
            };
        }

        private int Columns => Math.Max(1, (window.Width - Right - Left) / Pitch);
        private int RowsVisible => Math.Max(1, (window.Height - Bottom - Top) / Pitch);
        private int TotalRows => (entries.Count + Columns - 1) / Columns;

        /// <summary>FUN_0073d010: snap to 35 px, 5..10 columns, 3..6 rows.</summary>
        public static Vector2Int SnapSize(int width, int height) {
            int w = width - Right - Left;
            w -= w % Pitch;
            w = Math.Min(Pitch * 10, Math.Max(Pitch * 5, w));
            int h = height - Bottom - Top;
            h = h - h % Pitch + Top;
            h = Math.Min(Top + Pitch * 6, Math.Max(Top + Pitch * 3, h));
            return new Vector2Int(Right + w + Left, Bottom + h);
        }

        /// <summary>vf01 0x73cf70: OK (W − 110, H − 25), Cancel (W − 60, H − 25), X (W − 14, 3); the frame for the size.</summary>
        private void ApplySize(int width, int height) {
            Vector2Int size = SnapSize(width, height);
            window.Layout.Width = size.x;
            window.Layout.Height = size.y;
            window.SetHeight(size.y);
            List<RoClassicOp> ops = window.Layout.Ops;
            ops.Clear();
            RoClassicFrames.TitleBar(ops, size.x, true);
            RoClassicFrames.Fill(ops, 0, 17, size.x, size.y - 17, White);
            int cols = (size.x - Right - Left) / Pitch, rows = (size.y - Bottom - Top) / Pitch;
            for (int i = 0; i < cols * rows; i++) RoClassicFrames.Bitmap(ops, Basic + "itemwin_mid.bmp", Left + Pitch * (i % cols), Top + Pitch * (i / cols));
            RoClassicFrames.ButtonBar(ops, size.x, size.y, 2);
            window.SetChildPosition("CMergeItemWnd/UIBitmapButton#0", size.x - 110, size.y - 25);
            window.SetChildPosition("CMergeItemWnd/UIBitmapButton#1", size.x - 60, size.y - 25);
            window.SetChildPosition("CMergeItemWnd/UIBitmapButton#2", size.x - 14, 3);
            firstRow = Mathf.Clamp(firstRow, 0, Math.Max(0, TotalRows - rows));
            window.Invalidate();
        }

        private void Draw(RoClassicWindow w) {
            string title = RoClassicUiHost.MsgStr(0x879, "Item Merge");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            RoItemInstance[] inventory = Inventory();
            int cols = Columns, visible = cols * RowsVisible;
            for (int i = 0; i < visible && firstRow * cols + i < entries.Count; i++) {
                Entry e = entries[firstRow * cols + i];
                int x = Left + Pitch * (i % cols), y = Top + Pitch * (i / cols);
                RoItemInstance item = Find(inventory, e.BagIndex);
                if (item != null) RoClassicItemText.DrawIcon(w, x + 5, y + 2, item, metadata, 0);
                w.DrawBitmap(x, y + 20, Ui + (e.Checked ? "checkbox_1.bmp" : "checkbox_0.bmp"));
            }

            if (TotalRows > RowsVisible) w.DrawScrollBar(w.Width - 15, 17, w.Height - 38, TotalRows - RowsVisible, TotalRows, RowsVisible, firstRow);
        }

        private int EntryAt(Vector2Int local) {
            if (local.x < Left || local.y < Top) return -1;
            int col = (local.x - Left) / Pitch, row = (local.y - Top) / Pitch;
            if (col >= Columns || row >= RowsVisible) return -1;
            int k = (firstRow + row) * Columns + col;
            return k < entries.Count ? k : -1;
        }

        private void Scroll(int rows) {
            int first = Mathf.Clamp(firstRow + rows, 0, Math.Max(0, TotalRows - RowsVisible));
            if (first == firstRow) return;
            firstRow = first;
            window.Invalidate();
        }

        private RoItemInstance[] Inventory() =>
            (session != null ? session.InventorySnapshot().InventoryItems : null) ?? Array.Empty<RoItemInstance>();

        private static RoItemInstance Find(RoItemInstance[] inventory, short bagIndex) {
            foreach (RoItemInstance item in inventory) {
                if (item != null && item.BagIndex == bagIndex) return item;
            }

            return null;
        }

        /// <summary>FUN_0073d460: two or more checked go out (the window waits for the answer), fewer print 0x87a.</summary>
        private void Ok() {
            var chosen = new List<short>();
            foreach (Entry e in entries) {
                if (e.Checked) chosen.Add(e.BagIndex);
            }

            if (chosen.Count < 2) {
                Chat(RoClassicUiHost.MsgStr(0x87a, "Two or more of the same type. Please select an item."), "#FF0000");
                return;
            }

            _ = session?.BeginLegacyMergeItem(chosen.ToArray());
        }

        private void Cancel() {
            if (window != null) window.Visible = false;
            _ = session?.BeginLegacyMergeItem(null);
        }

        /// <summary>vf28 0x73cb50: MsgStr 0xb7 "%s: %d ea." over the hovered cell.</summary>
        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (window == null || !window.Visible || ui.Windows.Dragging != null || ui.Windows.HoverWindow != window) return;
            Vector2Int local = ui.Windows.Mouse - window.Position;
            int k = EntryAt(local);
            if (k < 0) return;
            RoItemInstance item = Find(Inventory(), entries[k].BagIndex);
            if (item == null) return;
            string text = RoClassicUiHost.MsgStr(0xb7, "%s: %d ea.").Replace("%s", item.GetFullDisplayName(metadata))
                .Replace("%d", item.Count.ToString(CultureInfo.InvariantCulture));
            int col = (local.x - Left) / Pitch, row = (local.y - Top) / Pitch;
            RoClassicTooltip.Shared.Show(text, RoClassicItemText.TooltipColor(item, metadata),
                window.Position.x + Left + Pitch * col, window.Position.y + Top + Pitch * row);
        }

        /// <summary>The sweep console: toggle i | ok | cancel | size w h | state.</summary>
        public static string Sweep(string command) {
            RoClassicMerge m = instance;
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], out int n) ? n : -1;
            if (a[0] == "talk" && m != null && m.session != null) {
                // talk x y: CZ_CONTACTNPC to the NPC nearest to cell (x, y) (the Mergician) before any NPC dialog exists
                var world = UnityEngine.Object.FindFirstObjectByType<Ragnarok.World.RoOnlineWorldController>();
                uint best = 0;
                int bestD = int.MaxValue;
                foreach (Ragnarok.World.RoOnlineActorSnapshot actor in world != null ? world.SnapshotActors() : Array.Empty<Ragnarok.World.RoOnlineActorSnapshot>()) {
                    if (actor.ObjectType != RoMapActorObjectType.Npc) continue;
                    int d = Math.Abs(actor.CellX - Arg(1)) + Math.Abs(actor.CellY - Arg(2));
                    if (d < bestD) {
                        bestD = d;
                        best = actor.ServerId;
                    }
                }

                if (best != 0) _ = m.session.BeginContactNpc(unchecked((int) best));
                return "talk: npc " + best + " at distance " + bestD;
            }

            if (m == null || m.window == null) return "no merge window";
            switch (a[0]) {
                case "toggle":
                    if (Arg(1) >= 0 && Arg(1) < m.entries.Count) {
                        m.entries[Arg(1)].Checked = !m.entries[Arg(1)].Checked;
                        m.window.Invalidate();
                    }

                    break;
                case "ok":
                    m.Ok();
                    break;
                case "cancel":
                    m.Cancel();
                    break;
                case "size":
                    m.ApplySize(Arg(1), Arg(2));
                    break;
            }

            var s = new System.Text.StringBuilder();
            s.Append("visible=").Append(m.window.Visible).Append(" size=").Append(m.window.Width).Append('x').Append(m.window.Height).Append(" entries=");
            foreach (Entry e in m.entries) s.Append(e.BagIndex).Append(e.Checked ? "[x] " : "[ ] ");
            return s.ToString().TrimEnd();
        }
    }
}
