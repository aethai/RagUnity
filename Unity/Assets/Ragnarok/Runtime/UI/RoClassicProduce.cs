using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UIMakeTargetProcessWnd 0x50 (codex-ui-research RUNTIME-RULES-ITEM-SERVICES.md "production-arrow-spell"): opened by
    /// the Manufacturing List 0x4f's OK with the chosen item, at the list's place. 280 × 150; OnDraw 0x905380 writes
    /// "&lt;name&gt;" + MsgStr 0x1aa " Create" at (16, 2) and "&lt;name&gt;" + 0x1ab "'s required materials" at (16, 22), black on a
    /// white (1, 1) shadow, then the item's lines of data\MetalProcessItemList.txt (FUN_0065e870) at (16, 42 + 20i), and,
    /// unless the item is Iron Ore 998 .. Star Crumb 1000 or an elemental stone 994 .. 997 (Steel 999 included), three
    /// itemwin_mid slots at (4 + 32i, 89) with the dropped materials' icons at (8 + 32i, 93). An inventory item dropped
    /// on the window fills the first empty slot (vf10 0x901b30); a click on a filled slot opens its description (vf25).
    /// OK sends CZ_REQMAKINGITEM with the item and the slots' item ids, Cancel with item 0 (vf37 0x906360); both close it.
    /// </summary>
    public sealed class RoClassicProduce : IRoClassicUiProducer {
        private const string Layout = "Ragnarok/Generated/OriginalClient/ui/UIMakeTargetProcessWnd";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const int OkId = 0xb4;
        private const int CancelId = 0xb5;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private static RoClassicProduce instance;
        private static RoAssetDatabase recipeAssets;
        private static Dictionary<int, List<string>> recipes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            recipeAssets = null;
            recipes = null;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private RoClassicWindow window;
        private uint itemId;
        private readonly RoItemInstance[] slots = new RoItemInstance[3];

        /// <summary>The list's OK: shows the materials step for <paramref name="item"/> at <paramref name="position"/>.</summary>
        internal static void Open(RoLoginShellSession session, RoItemMetadataTable metadata, uint item, Vector2Int position) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || session == null) return;
            if (instance == null || instance.system != system) {
                instance = new RoClassicProduce { system = system };
                system.Register(instance);
            }

            RoClassicProduce p = instance;
            p.session = session;
            p.metadata = metadata ?? RoItemMetadataTable.Empty;
            p.itemId = item;
            Array.Clear(p.slots, 0, p.slots.Length);
            p.Ensure();
            p.window.MoveTo(position.x, position.y);
            p.window.Visible = true;
            system.Windows.BringToFront(p.window);
            p.window.Invalidate();
        }

        private void Ensure() {
            if (window != null) return;
            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layout).text);
            // the item name, the slot tiles and their icons are drawn per item
            layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text || (op.Path != null && op.Path.EndsWith("itemwin_mid.bmp", StringComparison.Ordinal)));
            window = new RoClassicWindow(system, layout) { Visible = false };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == OkId) Send(itemId);
                else if (child.Id == CancelId) Send(0);
            };
            window.Pressed += (w, local) => {
                int k = SlotAt(local);
                if (k >= 0 && slots[k] != null) RoClassicItemDescription.Toggle(slots[k], metadata);
            };
            window.Dropped += (w, local, payload) => {
                if (payload is RoClassicItemPayload dropped && dropped.Item != null) AddMaterial(dropped.Item);
            };
        }

        /// <summary>Iron Ore 998, Steel 999, Star Crumb 1000 and the elemental stones 994 .. 997 take no extra material.</summary>
        private static bool Special(uint item) => item >= 994 && item <= 1000;

        private int SlotAt(Vector2Int local) {
            if (Special(itemId) || local.y < 89 || local.y >= 121 || local.x < 4 || local.x >= 100) return -1;
            return (local.x - 4) / 32;
        }

        /// <summary>vf10 0x901b30: the first empty of the three slots takes the dropped item.</summary>
        private void AddMaterial(RoItemInstance item) {
            if (Special(itemId)) return;
            for (int k = 0; k < slots.Length; k++) {
                if (slots[k] != null) continue;
                slots[k] = item.WithCount(1);
                window.Invalidate();
                return;
            }
        }

        private void Draw(RoClassicWindow w) {
            string name = itemId != 0 ? RoItemInstance.CreateRegular(0, unchecked((int) itemId), 1).GetFullDisplayName(metadata) : " ";
            int nameW = w.FontFor(0, 12).Measure(name);
            Shadowed(w, 16, 2, name);
            Shadowed(w, 16 + nameW, 2, RoClassicUiHost.MsgStr(0x1aa, " Create"));
            Shadowed(w, 16, 22, name);
            Shadowed(w, 16 + nameW, 22, RoClassicUiHost.MsgStr(0x1ab, "'s required materials"));
            List<string> lines = Recipe(unchecked((int) itemId));
            for (int i = 0; lines != null && i < lines.Count; i++) w.TextOut(16, 42 + 20 * i, lines[i], 0, 12, Black);
            if (Special(itemId)) return;
            for (int k = 0; k < slots.Length; k++) {
                w.DrawBitmap(4 + 32 * k, 89, Basic + "itemwin_mid.bmp");
                if (slots[k] != null) RoClassicItemText.DrawIcon(w, 8 + 32 * k, 93, slots[k], metadata, 0);
            }
        }

        private static void Shadowed(RoClassicWindow w, int x, int y, string text) {
            w.TextOut(x + 1, y + 1, text, 0, 12, White);
            w.TextOut(x, y, text, 0, 12, Black);
        }

        private void Send(uint item) {
            if (window != null) window.Visible = false;
            var materials = new uint[3];
            for (int k = 0; k < slots.Length; k++) materials[k] = slots[k] != null ? unchecked((uint) slots[k].ItemId) : 0;
            _ = session?.BeginLegacyMakingItem(item, materials);
        }

        /// <summary>
        /// FUN_0065e870 over the table FUN_006602xx loads from data\MetalProcessItemList.txt: a numeric token starts an item,
        /// the following text lines ("2 Iron") are its materials; '#' ends a token.
        /// </summary>
        private static List<string> Recipe(int item) {
            var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            if (assets == null || !assets.IsInitialized) return null;
            if (!ReferenceEquals(assets, recipeAssets)) {
                recipeAssets = assets;
                const string path = "data/metalprocessitemlist.txt";
                recipes = ParseRecipes(assets.Exists(path) ? assets.ReadText(path) : null);
            }

            return recipes != null && recipes.TryGetValue(item, out List<string> lines) ? lines : null;
        }

        public static Dictionary<int, List<string>> ParseRecipes(string text) {
            var table = new Dictionary<int, List<string>>();
            if (string.IsNullOrEmpty(text)) return table;
            int current = 0;
            foreach (string raw in text.Split('\n')) {
                foreach (string part in raw.Split('#')) {
                    string token = part.Trim();
                    if (token.Length == 0) continue;
                    if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) {
                        current = id;
                        continue;
                    }

                    if (current == 0) continue;
                    if (!table.TryGetValue(current, out List<string> lines)) table[current] = lines = new List<string>();
                    lines.Add(token);
                }
            }

            return table;
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
        }

        /// <summary>The sweep console: add itemId | remove slot | ok | cancel | state.</summary>
        public static string Sweep(string command) {
            RoClassicProduce p = instance;
            if (p == null || p.window == null) return "no materials window";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], out int n) ? n : -1;
            switch (a[0]) {
                case "add":
                    foreach (RoItemInstance item in p.session?.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>()) {
                        if (item != null && item.ItemId == Arg(1)) {
                            p.AddMaterial(item);
                            break;
                        }
                    }

                    break;
                case "remove":
                    if (Arg(1) >= 0 && Arg(1) < p.slots.Length) p.slots[Arg(1)] = null;
                    p.window.Invalidate();
                    break;
                case "ok":
                    p.Send(p.itemId);
                    break;
                case "cancel":
                    p.Send(0);
                    break;
            }

            var s = new System.Text.StringBuilder();
            s.Append("visible=").Append(p.window.Visible).Append(" item=").Append(p.itemId).Append(" slots=");
            foreach (RoItemInstance slot in p.slots) s.Append(slot != null ? slot.ItemId : 0).Append(' ');
            List<string> lines = Recipe(unchecked((int) p.itemId));
            s.Append("recipe=").Append(lines != null ? string.Join(" / ", lines) : "-");
            return s.ToString().TrimEnd();
        }
    }
}
