using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>One evolution route of PetEvolutionCln_true.lub: the source and target egg, the materials in file order.</summary>
    public sealed class RoPetEvolutionRoute {
        public RoPetEvolutionRoute(int source, int target) {
            Source = source;
            Target = target;
        }

        public int Source { get; }
        public int Target { get; }
        public List<(int Item, int Amount)> Materials { get; } = new List<(int Item, int Amount)>();
    }

    /// <summary>
    /// Pet evolution (codex-ui-research RUNTIME-RULES-COMPANIONS-MAIL.md "pet evolution 0x105"): the Pet Info command list
    /// offers one "Evolution - %s." (0xa08) row per route of the current egg; with intimacy over 900 it opens
    /// UIPetEvolutionWnd 0x105 (290 × 400, centred), else 0xa10 in red. The window (vf20 0x90ce10) draws the two
    /// illustrations, img_next, the material text buttons with "%dea" and OK / Cancel; OK (vf37 0x90d210) checks each
    /// material's count (0xa0e) and the accessory (0xa2f), sends CZ_PET_EVOLUTION and closes at once. ZC_PET_EVOLUTION_RESULT
    /// 0..5 shows 0xa0b..0xa10 in red; 6 (Hercules' success) and 7 show nothing (FUN_00c5e510).
    /// </summary>
    public sealed class RoClassicPetEvolution : IRoClassicUiProducer {
        public const int WindowId = 0x105;
        private const int Width = 290, Height = 400;
        private const int OkId = 0xb4, CancelId = 0xb5;
        private const string Ui = "유저인터페이스\\";
        private const string NoImage = "userinterface\\illust\\pet_noimage.bmp";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Grey = new Color32(0x9b, 0x9b, 0x9b, 255);
        private static readonly Color32 TextBlue = new Color32(0x64, 0x64, 0xff, 255);

        private static RoClassicPetEvolution instance;
        private static RoAssetDatabase recipesFor;
        private static Dictionary<int, List<RoPetEvolutionRoute>> recipes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            recipesFor = null;
            recipes = null;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private RoClassicWindow window;
        private int seenResult = -1;
        private System.Threading.Tasks.Task lastSend;
        private int sourceEgg, targetEgg;
        private readonly List<(int Item, int Amount)> materials = new List<(int Item, int Amount)>();
        // the UITextButtons: rect, item id (egg or material), text
        private readonly List<(RectInt Rect, int Item, string Text)> labels = new List<(RectInt Rect, int Item, string Text)>();

        public static void Tick(RoLoginShellSession session, RoClassicChat chat, RoItemMetadataTable metadata) {
            RoClassicPetEvolution e = Ensure();
            if (e == null) return;
            e.session = session;
            e.chat = chat;
            e.metadata = metadata ?? RoItemMetadataTable.Empty;
            (uint result, int serial) = session.PetEvolutionSnapshot();
            if (e.seenResult >= 0 && serial != e.seenResult) e.OnResult(result);
            e.seenResult = serial;
        }

        private static RoClassicPetEvolution Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicPetEvolution { system = system };
                system.Register(instance);
            }

            return instance;
        }

        // ------------------------------------------------------------------ recipes

        /// <summary>
        /// InsertEvolutionRecipeLGU(source, target, material, amount) rows (FUN_005ffdb0: four numbers, a positive amount);
        /// the routes of a source keep the order their target first appears in, the materials their row order.
        /// </summary>
        public static Dictionary<int, List<RoPetEvolutionRoute>> ParseRecipes(string lua) {
            var result = new Dictionary<int, List<RoPetEvolutionRoute>>();
            foreach (Match m in Regex.Matches(lua ?? string.Empty, @"^\s*InsertEvolutionRecipeLGU\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(-?\d+)\s*\)", RegexOptions.Multiline)) {
                int source = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int target = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                int item = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                int amount = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
                if (amount <= 0) continue;
                if (!result.TryGetValue(source, out List<RoPetEvolutionRoute> routes)) result[source] = routes = new List<RoPetEvolutionRoute>();
                RoPetEvolutionRoute route = routes.Find(r => r.Target == target);
                if (route == null) routes.Add(route = new RoPetEvolutionRoute(source, target));
                route.Materials.Add((item, amount));
            }

            return result;
        }

        /// <summary>FUN_005ff310 loads System\PetEvolutionCln_true.lub (not PetEvolution.lub).</summary>
        private static void LoadRecipes() {
            var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            if (assets == null || !assets.IsInitialized || ReferenceEquals(assets, recipesFor)) return;
            recipesFor = assets;
            const string path = "System/PetEvolutionCln_true.lub";
            recipes = ParseRecipes(assets.Exists(path) ? assets.ReadText(path) : null);
        }

        /// <summary>FUN_005fee40: the routes of an egg (none when the egg has no recipe).</summary>
        public static IReadOnlyList<RoPetEvolutionRoute> RoutesOf(int egg) {
            LoadRecipes();
            return recipes != null && recipes.TryGetValue(egg, out List<RoPetEvolutionRoute> routes) ? routes : (IReadOnlyList<RoPetEvolutionRoute>) Array.Empty<RoPetEvolutionRoute>();
        }

        // ------------------------------------------------------------------ 0x105

        /// <summary>MakeWindow(0x105) and Msg 0x8e (source egg, target egg): the route's materials and the text buttons.</summary>
        public static void Open(int source, int target) {
            RoClassicPetEvolution e = Ensure();
            e?.OpenWindow(source, target);
        }

        private void OpenWindow(int source, int target) {
            if (window == null) {
                var layout = new RoClassicWindowLayout { Class = "UIPetEvolutionWnd", WindowId = WindowId, Width = Width, Height = Height };
                // the root clears white, then the title bar with sys_base (FUN_0082f2d0(1, 0xa07))
                RoClassicFrames.Fill(layout.Ops, 0, 0, Width, Height, White);
                RoClassicFrames.TitleBar(layout.Ops, Width, true);
                layout.Children.Add(Button(OkId, "ok", 70));
                layout.Children.Add(Button(CancelId, "cancel", 180));
                window = new RoClassicWindow(system, layout) { Visible = false, ClickableAt = local => LabelAt(local) >= 0 };
                window.DrawDynamic += Draw;
                window.Clicked += child => {
                    if (child.Id == OkId) Ok();
                    else if (child.Id == CancelId) Close();
                };
                window.Pressed += (w, local) => {
                    // Msg 0x62: the item's description (window 0x0c, Msg 0x18)
                    int k = LabelAt(local);
                    if (k >= 0) RoClassicItemDescription.Toggle(RoItemInstance.CreateRegular(0, labels[k].Item, 1), metadata);
                };
                window.EscClose = () => {
                    Close();
                    return true;
                };
            }

            sourceEgg = source;
            targetEgg = target;
            materials.Clear();
            foreach (RoPetEvolutionRoute route in RoutesOf(source)) {
                if (route.Target == target) materials.AddRange(route.Materials);
            }

            BuildLabels();
            Vector2Int client = system.ClientSize;
            window.MoveTo(Math.Max(0, (client.x - Width) / 2), Math.Max(0, (client.y - Height) / 2));
            window.Visible = true;
            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
            window.Invalidate();
        }

        private static RoClassicChildLayout Button(int id, string name, int x) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = name, X = x, Y = 370, W = 100, H = 20 };
            button.States["normal"] = Ui + "btn_big_" + name + ".bmp";
            button.States["hover"] = Ui + "btn_big_" + name + "_a.bmp";
            button.States["pressed"] = Ui + "btn_big_" + name + "_b.bmp";
            return button;
        }

        /// <summary>
        /// FUN_0090d6d0: the egg labels (PetStringTable name, only when there is one) at (10, 171) and (W − 100, 171), 90 wide;
        /// the materials' item names at (10, 230 + 20i). FUN_007ddf70 sizes a bold 12 px button 6 per byte + 16 by 14.
        /// </summary>
        private void BuildLabels() {
            labels.Clear();
            string from = RoClassicPet.PetName(RoClassicPet.EggJob(sourceEgg));
            if (from != null) labels.Add((new RectInt(10, 171, 90, 14), sourceEgg, from));
            string to = RoClassicPet.PetName(RoClassicPet.EggJob(targetEgg));
            if (to != null) labels.Add((new RectInt(Width - 100, 171, 90, 14), targetEgg, to));
            for (int i = 0; i < materials.Count; i++) {
                string name = RoItemInstance.CreateRegular(0, materials[i].Item, 1).GetFullDisplayName(metadata);
                labels.Add((new RectInt(10, 230 + 20 * i, ByteWidth(name), 14), materials[i].Item, name));
            }
        }

        // langtype 1 (Windows-1252): one byte per character
        private static int ByteWidth(string text) => (text ?? string.Empty).Length * 6 + 16;

        private int LabelAt(Vector2Int local) {
            for (int k = 0; k < labels.Count; k++) {
                if (labels[k].Rect.Contains(local)) return k;
            }

            return -1;
        }

        private void Close() {
            if (window == null) return;
            window.Visible = false;
            system.Windows.RemoveEsc(window);
        }

        private void Draw(RoClassicWindow w) {
            string title = RoClassicUiHost.MsgStr(0xa07, "Pet Evolution");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            DrawIllustration(w, sourceEgg, false);
            if (system.Images.TryGet(Ui + "img_next.bmp", out RoClassicUiImage next)) {
                w.DrawBitmap((int) ((Width - next.Width) * 0.5), (int) ((0x86 - next.Height) * 0.5 + 27.0), Ui + "img_next.bmp");
            }

            DrawIllustration(w, targetEgg, true);
            w.TextOut(10, 200, RoClassicUiHost.MsgStr(0xa09, "Evolution requires the following ingredients:"), 0, 12, Black);
            int hover = system.Windows.HoverWindow == w ? LabelAt(system.Windows.Mouse - w.Position) : -1;
            for (int k = 0; k < labels.Count; k++) {
                // UITextButton 0x7f09a0: outlined bold text at (1, 1), the underline on its last row; hover #9B9B9B
                (RectInt rect, _, string text) = labels[k];
                Color32 color = k == hover ? Grey : TextBlue;
                w.TextOutOutline(rect.x + 1, rect.y + 1, text, 0, 12, color, White, true);
                w.FillRect(rect.x, rect.y + rect.height - 1, rect.width, 1, color);
            }

            int first = labels.Count - materials.Count;
            for (int i = 0; i < materials.Count; i++) {
                RectInt rect = labels[first + i].Rect;
                string amount = materials[i].Amount.ToString(CultureInfo.InvariantCulture) + "ea";
                w.TextOutPlain(rect.x + rect.width + 10, rect.y + 1, amount, w.FontFor(0, 12, true), Black);
            }

            w.TextOut(10, Height - 52, RoClassicUiHost.MsgStr(0xa0a, "Do you want to evolve your pet?"), 0, 12, Black);
        }

        /// <summary>The egg's illustration (or pet_noimage) with its grey shadow at +2 and a black border; the target at the right.</summary>
        private void DrawIllustration(RoClassicWindow w, int egg, bool right) {
            int job = RoClassicPet.EggJob(egg);
            if (job == -1) return;
            string path = RoClassicPet.Illustration(job);
            if (!system.Images.TryGet(path, out RoClassicUiImage image)) {
                path = NoImage;
                if (!system.Images.TryGet(path, out image)) return;
            }

            int x = right ? Width - image.Width - 10 : 10;
            w.FillRect(x + 2, 29, image.Width, image.Height, Grey);
            w.DrawBitmap(x, 27, path);
            w.FillRect(x, 27, image.Width, 1, Black);
            w.FillRect(x, 27 + image.Height - 1, image.Width, 1, Black);
            w.FillRect(x, 27, 1, image.Height, Black);
            w.FillRect(x + image.Width - 1, 27, 1, image.Height, Black);
        }

        /// <summary>OK: each material's count (0xa0e), the accessory (0xa2f), then CZ_PET_EVOLUTION and the window goes.</summary>
        private void Ok() {
            var list = new List<(int Index, int Amount)>();
            RoItemInstance[] inventory = session?.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>();
            foreach ((int item, int amount) in materials) {
                RoItemInstance found = Array.Find(inventory, it => it != null && it.ItemId == item);
                if (found == null || found.Count < amount) {
                    Chat(RoClassicUiHost.MsgStr(0xa0e, "Evolution material is low."));
                    return;
                }

                list.Add((found.BagIndex, amount));
            }

            if (session == null) return;
            if (session.PetSnapshot().Pet.Accessory != 0) {
                Chat(RoClassicUiHost.MsgStr(0xa2f, "Unequip pet accessory first to begin pet evolution"));
                return;
            }

            lastSend = session.BeginLegacyPetEvolution(unchecked((uint) targetEgg), list);
            Close();
        }

        /// <summary>FUN_00c5e510: 0..5 → 0xa0b..0xa10 in red; 6, 7 and anything else show nothing.</summary>
        private void OnResult(uint result) {
            if (result > 5) return;
            string[] fallback = {
                "Unknown error.", "Pet is not called.", "Pet egg is not found.", "Evolution material is low.",
                "Lack of required materials for evolution.", "Loyal intimacy is required to evolve.",
            };
            Chat(RoClassicUiHost.MsgStr(0xa0b + (int) result, fallback[result]));
        }

        private void Chat(string text) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, "#FF0000");
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            // the text buttons follow the mouse
            if (window != null && window.Visible && system.Windows.HoverWindow == window) window.Invalidate();
        }

        /// <summary>The sweep console: open source target | ok | cancel | result n | routes egg | state.</summary>
        public static string Sweep(string command) {
            RoClassicPetEvolution e = Ensure();
            if (e == null) return "no classic ui";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => a.Length > i && int.TryParse(a[i], out int v) ? v : 0;
            switch (a[0]) {
                case "open":
                    e.OpenWindow(Arg(1), Arg(2));
                    break;
                case "ok":
                    e.Ok();
                    break;
                case "cancel":
                    e.Close();
                    break;
                case "result":
                    e.OnResult((uint) Arg(1));
                    break;
                case "routes":
                    var parts = new List<string>();
                    foreach (RoPetEvolutionRoute r in RoutesOf(Arg(1))) {
                        parts.Add(r.Target + ":" + string.Join("+", r.Materials.ConvertAll(m => m.Item + "x" + m.Amount)));
                    }

                    return "routes " + Arg(1) + " = " + string.Join(" ", parts);
            }

            string send = e.lastSend == null ? "-" : e.lastSend.Status + (e.lastSend.Exception != null ? ":" + e.lastSend.Exception.GetBaseException().Message : string.Empty);
            return "send=" + send + " result=" + e.session?.PetEvolutionSnapshot() + " visible=" + (e.window != null && e.window.Visible) + " source=" + e.sourceEgg + " target=" + e.targetEgg + " materials=" +
                   string.Join(",", e.materials.ConvertAll(m => m.Item + "x" + m.Amount)) + " labels=" + string.Join("|", e.labels.ConvertAll(l => l.Text + "@" + l.Rect.x + "," + l.Rect.y + "," + l.Rect.width));
        }
    }
}
