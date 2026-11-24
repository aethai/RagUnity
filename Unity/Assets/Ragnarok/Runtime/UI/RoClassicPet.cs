using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>The own pet as the client keeps it (DAT_0154ece4..0154ecf8): ZC_PROPERTY_PET, then ZC_CHANGESTATE_PET.</summary>
    public readonly struct RoClassicPetState {
        public static readonly RoClassicPetState None = new RoClassicPetState(false, string.Empty, false, 0, 0, 0, 0, -1, 0);

        public RoClassicPetState(bool active, string name, bool renamed, int level, int hunger, int intimacy, int accessory, int job, uint gid) {
            Active = active;
            Name = name ?? string.Empty;
            Renamed = renamed;
            Level = level;
            Hunger = hunger;
            Intimacy = intimacy;
            Accessory = accessory;
            Job = job;
            Gid = gid;
        }

        public bool Active { get; }
        public string Name { get; }
        public bool Renamed { get; }
        public int Level { get; }
        public int Hunger { get; }
        public int Intimacy { get; }
        public int Accessory { get; }
        public int Job { get; }
        public uint Gid { get; }

        public static RoClassicPetState From(RoZcPetProperty p, uint gid) =>
            new RoClassicPetState(true, p.Name, p.Modified, p.Level, p.Hunger, p.Intimacy, unchecked((int) p.ItemId), p.Job, gid);

        public RoClassicPetState WithName(string name) =>
            new RoClassicPetState(Active, name, true, Level, Hunger, Intimacy, Accessory, Job, Gid);

        public RoClassicPetState With(uint? gid = null, int? intimacy = null, int? hunger = null, int? accessory = null) =>
            new RoClassicPetState(Active, Name, Renamed, Level, hunger ?? Hunger, intimacy ?? Intimacy, accessory ?? Accessory, Job, gid ?? Gid);
    }

    /// <summary>
    /// The pet windows (codex-ui-research RUNTIME-RULES-COMPANIONS-MAIL.md "pets"): UIPetInfoWnd 0x58 (Alt+J; only with a
    /// pet, MakeWindow tests the class) and the incubator list UIPetEggListWnd 0x5a (the UIItemIdentifyWnd frame, title
    /// MsgStr 0x257 "Pet List"). OnDraw 0x818cd0: title 0x248 at (5, 2); Name 0x197 (100, 24), Level 0x198 (100, 44) with the
    /// level at (135, 44), Hunger 0x249 (100, 69) with 0x29b..0x29f at (145, 69), Intimacy 0x24a (100, 89) with
    /// 0x2a0 / 0x2a1 / 0x29d / 0x2a2 / 0x2a3 at (145, 89) (0x821a06: ..100 / ..250 / ..750 / ..900 / ..1000, else 0x2a4) and
    /// the UIPetInfoBarGraph (145, 105, 120, 9) filled by the progress inside that band; the illustration
    /// (petinfo GetPetIllustName_V3) at (5, 21); the command box #E6E6FA (160, 44, 110, 18) with "&lt; Command &gt;" centred on
    /// 206, y 48; Accessory 0x255 (100, 119) and Equipped / Unequipped 0x256 / 0x258 (100, 139). The name edit
    /// (135, 24, 80, 16) and Rename (0xb4, 23 bytes, confirmation 0xb73) send CZ_RENAME_PET; the command box opens the
    /// command list: Feed Pet 0x250 (confirmation 0x259, then CZ_COMMAND_PET 1 — without the food 0x24f), Performance
    /// 0x251 (2), Unequip Accessory 0x253 (4), Return to Egg 0x252 (3). The position is kept (PETINFOWNDINFO).
    /// </summary>
    public sealed class RoClassicPet : IRoClassicUiProducer {
        public const int PetEgg = 0x5a;
        private const string Layout = "Ragnarok/Generated/OriginalClient/ui/UIPetInfoWnd";
        private const string Prefs = "ro.ui.classic.petinfo.";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const int CloseId = 0xc5;
        private const int RenameId = 0xb4;
        private const int CommandId = 0x108;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 CommandFill = new Color32(0xe6, 0xe6, 0xfa, 255);
        private static readonly Color32 EditFill = new Color32(0xe8, 0xe8, 0xe8, 255);

        private static RoClassicPet instance;
        private static RoAssetDatabase tablesFor;
        private static Dictionary<int, string> illustrations;
        private static Dictionary<int, int> foods;
        private static Dictionary<int, string> names;
        private static Dictionary<int, int> eggJobs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            tablesFor = null;
            illustrations = null;
            foods = null;
            names = null;
            eggJobs = null;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private RoClassicWindow window;
        private RoClassicEditField nameEdit;
        private RoClassicPetState pet = RoClassicPetState.None;
        private int seenPet = -1;
        private int seenEggs = -1;
        private int seenEggClose = -1;
        private int seenFeed = -1;
        private RoClassicWindow combo;
        private readonly List<string> comboRows = new List<string>();
        private int comboHover = -1;

        public static void Tick(RoLoginShellSession session, RoClassicChat chat, RoItemMetadataTable metadata) {
            if (session == null) return;
            RoClassicPet p = Ensure();
            if (p == null) return;
            (RoClassicPetState state, int petSerial) = session.PetSnapshot();
            (int[] eggs, int eggSerial, int closeSerial) = session.PetEggSnapshot();
            if (!ReferenceEquals(p.session, session)) {
                p.session = session;
                p.seenEggs = eggSerial;
                p.seenEggClose = closeSerial;
            }

            p.chat = chat;
            p.metadata = metadata ?? RoItemMetadataTable.Empty;
            if (petSerial != p.seenPet) {
                p.seenPet = petSerial;
                p.pet = state;
                if (!state.Active && p.window != null) p.window.Visible = false;
                p.SyncName();
                p.window?.Invalidate();
            }

            if (eggSerial != p.seenEggs) {
                p.seenEggs = eggSerial;
                p.OpenEggs(eggs, eggSerial);
            }

            if (closeSerial != p.seenEggClose) {
                p.seenEggClose = closeSerial;
                RoClassicItemSelect.Hide(PetEgg);
            }

            RoClassicPetEvolution.Tick(session, chat, p.metadata);
            (bool fed, uint food, int feedSerial) = session.PetFeedSnapshot();
            if (p.seenFeed >= 0 && feedSerial != p.seenFeed) p.OnFeed(fed, food);
            p.seenFeed = feedSerial;
        }

        /// <summary>
        /// FUN_00c4bf50 (ZC_FEED_PET 0x01a3): 0 → 0x24f "Pet food [%s] is not available." in #F51E1E, otherwise 0xa12 "Your pet
        /// has been fed. %s (%d) remaining" (the food's name and its count in the inventory) in #64FFFF.
        /// </summary>
        private void OnFeed(bool fed, uint food) {
            string name = RoItemInstance.CreateRegular(0, unchecked((int) food), 1).GetFullDisplayName(metadata);
            if (!fed) {
                Chat(RoClassicUiHost.MsgStr(0x24f, "Pet food [%s] is not available.").Replace("%s", name), "#F51E1E");
                return;
            }

            int left = 0;
            foreach (RoItemInstance item in session?.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>()) {
                if (item != null && item.ItemId == unchecked((int) food)) {
                    left = item.Count;
                    break;
                }
            }

            string text = RoClassicUiHost.MsgStr(0xa12, "Your pet has been fed. %s (%d) remaining");
            int at = text.IndexOf("%s", StringComparison.Ordinal);
            if (at >= 0) text = text.Substring(0, at) + name + text.Substring(at + 2);
            Chat(text.Replace("%d", left.ToString(CultureInfo.InvariantCulture)), "#64FFFF");
        }

        private static RoClassicPet Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicPet { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Alt+J (interface action "pet"): toggles 0x58; MakeWindow refuses it without a pet (class −1).</summary>
        public static bool ToggleInfo() {
            RoClassicPet p = Ensure();
            if (p == null || p.session == null) return false;
            if (p.window != null && p.window.Visible) {
                p.window.Visible = false;
                return true;
            }

            if (!p.pet.Active) return true;
            p.OpenInfo();
            return true;
        }

        // ------------------------------------------------------------------ 0x58

        private void OpenInfo() {
            if (window == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layout).text);
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    // the edit is native, the bar is drawn with its value
                    if (child.Class == "UIEditCtrl2" || child.Class == "UIPetInfoBarGraph") child.Visible = false;
                }

                window = new RoClassicWindow(system, layout) { Visible = false };
                window.DrawDynamic += Draw;
                window.Clicked += child => {
                    if (child.Id == CloseId) window.Visible = false;
                    else if (child.Id == RenameId) Rename();
                    else if (child.Id == CommandId) OpenCommands();
                };
                window.Moved += w => {
                    PlayerPrefs.SetInt(Prefs + "x", w.Position.x);
                    PlayerPrefs.SetInt(Prefs + "y", w.Position.y);
                };
                window.EscClose = () => {
                    window.Visible = false;
                    return true;
                };
                nameEdit = new RoClassicEditField { Rect = new RectInt(135, 24, 80, 16), Background = EditFill, MaxBytes = 23 };
                nameEdit.Submitted += _ => Rename();
                window.AddEditField(nameEdit);
            }

            Vector2Int client = system.ClientSize;
            int x = PlayerPrefs.GetInt(Prefs + "x", 150), y = PlayerPrefs.GetInt(Prefs + "y", 100);
            window.MoveTo(Mathf.Clamp(x, 0, Math.Max(0, client.x - window.Width)), Mathf.Clamp(y, 0, Math.Max(0, client.y - window.Height)));
            SyncName();
            window.Visible = true;
            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
            window.Invalidate();
        }

        private void SyncName() {
            if (nameEdit != null && nameEdit.Text != pet.Name) nameEdit.SetText(pet.Name);
        }

        /// <summary>0x818cd0 "valid class": a monster id 1001..3998 or 20021..31999.</summary>
        private static bool ValidClass(int job) => job != -1 && ((job > 1000 && job < 3999) || unchecked((uint) (job - 0x4e35)) < 0x2ecb);

        private static string HungerLabel(int hunger) {
            if (hunger >= 0 && hunger <= 10) return RoClassicUiHost.MsgStr(0x29b, " Very Hungry");
            if (hunger >= 0 && hunger <= 25) return RoClassicUiHost.MsgStr(0x29c, " Hungry");
            if (hunger >= 0 && hunger <= 75) return RoClassicUiHost.MsgStr(0x29d, " Neutral");
            if (hunger >= 0 && hunger <= 90) return RoClassicUiHost.MsgStr(0x29e, " Satisfied");
            if (hunger >= 0 && hunger <= 100) return RoClassicUiHost.MsgStr(0x29f, " Stuffed");
            return RoClassicUiHost.MsgStr(0x2a4, "Unknown");
        }

        /// <summary>0x821a06: the intimacy band's label and the bar's value, the progress inside the band (0..100).</summary>
        public static (int Message, string Fallback, int Bar) IntimacyBand(int intimacy) {
            int lo, hi, message;
            string fallback;
            if (intimacy >= 0 && intimacy <= 100) (lo, hi, message, fallback) = (0, 100, 0x2a0, " Awkward");
            else if (intimacy > 100 && intimacy <= 250) (lo, hi, message, fallback) = (100, 250, 0x2a1, " Shy");
            else if (intimacy > 250 && intimacy <= 750) (lo, hi, message, fallback) = (250, 750, 0x29d, " Neutral");
            else if (intimacy > 750 && intimacy <= 900) (lo, hi, message, fallback) = (750, 900, 0x2a2, " Cordial");
            else if (intimacy > 900 && intimacy <= 1000) (lo, hi, message, fallback) = (900, 1000, 0x2a3, " Loyal");
            else return (0x2a4, "Unknown", 0);
            return (message, fallback, (int) ((float) (intimacy - lo) / (hi - lo) * 100f));
        }

        private void Draw(RoClassicWindow w) {
            w.TextOut(5, 2, RoClassicUiHost.MsgStr(0x248, "Pet Info"), 0, 12, Black);
            w.TextOut(100, 24, RoClassicUiHost.MsgStr(0x197, "Name"), 0, 12, Black);
            w.TextOut(100, 44, RoClassicUiHost.MsgStr(0x198, "Level"), 0, 12, Black);
            w.TextOut(100, 69, RoClassicUiHost.MsgStr(0x249, "Hunger"), 0, 12, Black);
            w.TextOut(100, 89, RoClassicUiHost.MsgStr(0x24a, "Intimacy"), 0, 12, Black);
            w.TextOut(135, 44, pet.Level.ToString(CultureInfo.InvariantCulture), 0, 12, Black);
            w.TextOut(145, 69, HungerLabel(pet.Hunger), 0, 12, Black);
            (int message, string fallback, int bar) = IntimacyBand(pet.Intimacy);
            w.TextOut(145, 89, RoClassicUiHost.MsgStr(message, fallback), 0, 12, Black);
            DrawBar(w, 145, 105, 120, bar);
            if (!ValidClass(pet.Job)) return;
            w.DrawBitmap(5, 21, Illustration(pet.Job));
            w.FillRect(160, 44, 110, 18, CommandFill);
            const string command = "< Command >";
            w.TextOut(206 - w.FontFor(0, 12).Measure(command) / 2, 48, command, 0, 12, Black);
            w.TextOut(100, 119, RoClassicUiHost.MsgStr(0x255, "Accessory"), 0, 12, Black);
            w.TextOut(100, 139, pet.Accessory == 0 ? RoClassicUiHost.MsgStr(0x258, "Unequipped") : RoClassicUiHost.MsgStr(0x256, "Equipped"), 0, 12, Black);
        }

        /// <summary>
        /// UIPetInfoBarGraph 0x7edb90: gze_bg left (2) / mid (1 px steps) / right (4), then gzeblue left (4) / mid / right (4)
        /// over value % of the inside.
        /// </summary>
        private static void DrawBar(RoClassicWindow w, int x, int y, int width, int value) {
            w.DrawBitmap(x, y, Basic + "gze_bg_left.bmp");
            for (int i = 0; i < width - 2 - 4; i++) w.DrawBitmap(x + 2 + i, y, Basic + "gze_bg_mid.bmp");
            w.DrawBitmap(x + width - 4, y, Basic + "gze_bg_right.bmp");
            if (value == 0) return;
            int n = (int) ((width - 4 - 4) * (value / 100f));
            w.DrawBitmap(x, y, Basic + "gzeblue_left.bmp");
            for (int i = 0; i < n; i++) w.DrawBitmap(x + 4 + i, y, Basic + "gzeblue_mid.bmp");
            w.DrawBitmap(x + n + 4, y, Basic + "gzeblue_right.bmp");
        }

        /// <summary>Rename 0xb4 (vf37 0x81ea20): at most 23 bytes, then 0xb73's confirmation and CZ_RENAME_PET.</summary>
        private void Rename() {
            string name = nameEdit?.Text?.Trim() ?? string.Empty;
            // ZC_PROPERTY_PET bModified: the server takes one rename only (pet_change_name answers nothing after it)
            if (pet.Renamed) {
                SyncName();
                return;
            }

            if (name.Length == 0 || name == pet.Name) return;
            if (name.Length > 23) name = name.Substring(0, 23);
            string question = RoClassicUiHost.MsgStr(0xb73, "Pet name will be changed to ^0000ff%s^000000, do you want to continue?").Replace("%s", name);
            RoClassicMessageBox.Show(question, RoClassicMessageBox.TypeOkCancel, true, result => {
                if (result == RoClassicMessageBox.IdYes) _ = session?.BeginLegacyRenamePet(name);
                else SyncName();
            });
        }

        /// <summary>
        /// Command 0x108: Feed Pet, Performance, Unequip Accessory, Return to Egg, in that order, then one "Evolution - %s."
        /// (0xa08, action 0x1d3) per route of the current egg (FUN_005fee40).
        /// </summary>
        private void OpenCommands() {
            comboRows.Clear();
            comboRows.Add(RoClassicUiHost.MsgStr(0x250, "Feed Pet"));
            comboRows.Add(RoClassicUiHost.MsgStr(0x251, "Performance"));
            comboRows.Add(RoClassicUiHost.MsgStr(0x253, "Unequip Accessory"));
            comboRows.Add(RoClassicUiHost.MsgStr(0x252, "Return to Egg"));
            foreach (RoPetEvolutionRoute route in RoClassicPetEvolution.RoutesOf(CurrentEgg())) {
                comboRows.Add(RoClassicUiHost.MsgStr(0xa08, "Evolution - %s.").Replace("%s", EvolutionName(route.Target)));
            }

            // UIComboBoxIDWnd 0x104: 110 × 16 · rows at Pet Info's origin + (160, 44)
            if (combo == null) {
                combo = new RoClassicWindow(system, new RoClassicWindowLayout { Class = "UIComboBoxIDWnd", WindowId = 0x104, Width = 110, Height = 16 }) {
                    Visible = false,
                    Draggable = false,
                    ClickableAt = _ => true,
                };
                combo.DrawDynamic += DrawCombo;
                combo.Pressed += (w, local) => {
                    // vf31 0x7f1d70: floor(y / 16) + top, Msg 0x27 to Pet Info
                    int row = local.y / 16;
                    CloseCombo();
                    if (row >= 0 && row < comboRows.Count) Command(row);
                };
            }

            combo.SetHeight(16 * comboRows.Count);
            combo.MoveTo(window.Position.x + 160, window.Position.y + 44);
            comboHover = -1;
            combo.Visible = true;
            system.Windows.BringToFront(combo);
            combo.Invalidate();
        }

        private void CloseCombo() {
            if (combo != null) combo.Visible = false;
        }

        /// <summary>
        /// UIComboBoxID 0x7eb9f0: white (colorchip 6,6 and 2,2), the sysboxs_lu / ld / ru / rd corners, the row under the pointer
        /// in colorchip (6,2) at (3, r · 16 + 1, W − 6, 14), the texts black at (3, r · 16 + 2).
        /// </summary>
        private void DrawCombo(RoClassicWindow w) {
            var white = new Color32(255, 255, 255, 255);
            w.FillRect(0, 0, w.Width, w.Height, white);
            const string box = Basic + "sysboxs_";
            w.DrawBitmap(0, 0, box + "lu.bmp");
            if (system.Images.TryGet(box + "ld.bmp", out RoClassicUiImage ld)) w.DrawBitmap(0, w.Height - ld.Height, box + "ld.bmp");
            if (system.Images.TryGet(box + "ru.bmp", out RoClassicUiImage ru)) w.DrawBitmap(w.Width - ru.Width, 0, box + "ru.bmp");
            if (system.Images.TryGet(box + "rd.bmp", out RoClassicUiImage rd)) w.DrawBitmap(w.Width - rd.Width, w.Height - rd.Height, box + "rd.bmp");
            for (int r = 0; r < comboRows.Count; r++) {
                if (r == comboHover) w.FillRect(3, r * 16 + 1, w.Width - 6, 14, new Color32(0x70, 0x9f, 0xed, 255));
                w.TextOut(3, r * 16 + 2, comboRows[r], 0, 12, Black);
            }
        }

        /// <summary>
        /// The menu's target name: PetStringTable of the egg's job (FUN_00d12c40); the client falls back to the job's name
        /// (FUN_00ceac40), the egg's item name stands in for it here.
        /// </summary>
        private string EvolutionName(int egg) =>
            PetName(EggJob(egg)) ?? RoItemInstance.CreateRegular(0, egg, 1).GetFullDisplayName(metadata);

        /// <summary>
        /// DAT_0154ecfc: the egg the inventory list marks damaged (the server hides the called pet's egg with ATTR_BROKEN);
        /// 0 without one.
        /// </summary>
        public int CurrentEgg() {
            LoadTables();
            foreach (RoItemInstance item in session?.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>()) {
                if (item != null && item.IsDamaged && (item.ItemTypeRaw == 7 || (eggJobs != null && eggJobs.ContainsKey(item.ItemId)))) return item.ItemId;
            }

            return 0;
        }

        /// <summary>Action 0x1d3: intimacy over 900 opens 0x105 with (current egg, target) and closes Pet Info; else 0xa10 in red.</summary>
        private void Evolve(int target) {
            if (pet.Intimacy <= 900) {
                Chat(RoClassicUiHost.MsgStr(0xa10, "Loyal intimacy is required to evolve."), "#FF0000");
                return;
            }

            // DeleteWindow(0x104), MakeWindow(0x105) with Msg 0x8e, then Pet Info's own close 0xc5
            CloseCombo();
            RoClassicPetEvolution.Open(CurrentEgg(), target);
            if (window != null) {
                window.Visible = false;
                system.Windows.RemoveEsc(window);
            }
        }

        private void Command(int row) {
            if (row >= 4) {
                IReadOnlyList<RoPetEvolutionRoute> routes = RoClassicPetEvolution.RoutesOf(CurrentEgg());
                if (row - 4 < routes.Count) Evolve(routes[row - 4].Target);
                return;
            }

            switch (row) {
                case 0: Feed(); break;
                case 1: _ = session?.BeginLegacyPetCommand(2); break;
                case 2: _ = session?.BeginLegacyPetCommand(4); break;
                case 3:
                    _ = session?.BeginLegacyPetCommand(3);
                    if (window != null) window.Visible = false;
                    break;
            }
        }

        /// <summary>Feed: 0x259's confirmation; mode 0x96 checks the food first (0x24f with its name when there is none).</summary>
        private void Feed() {
            RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x259, "Are you sure that you want to feed your pet?"), RoClassicMessageBox.TypeOkCancel, true, result => {
                if (result != RoClassicMessageBox.IdYes) return;
                int food = Food(pet.Job);
                if (food > 0 && !Owns(food)) {
                    string name = RoItemInstance.CreateRegular(0, food, 1).GetFullDisplayName(metadata);
                    Chat(RoClassicUiHost.MsgStr(0x24f, "Pet food [%s] is not available.").Replace("%s", name), "#FF0000");
                    return;
                }

                _ = session?.BeginLegacyPetCommand(1);
            });
        }

        private bool Owns(int itemId) {
            foreach (RoItemInstance item in session?.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>()) {
                if (item != null && item.ItemId == itemId && item.Count > 0) return true;
            }

            return false;
        }

        private void Chat(string text, string color) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, color);
        }

        // ------------------------------------------------------------------ petinfo tables

        private static void LoadTables() {
            var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            if (assets == null || !assets.IsInitialized || ReferenceEquals(assets, tablesFor)) return;
            tablesFor = assets;
            string identity = Read(assets, "data/luafiles514/lua files/datainfo/npcidentity.lub");
            string petInfo = Read(assets, "data/luafiles514/lua files/datainfo/petinfo.lub");
            ParsePetInfo(identity, petInfo, out illustrations, out foods, out names, out eggJobs);
        }

        private static string Read(RoAssetDatabase assets, string path) => assets.Exists(path) ? assets.ReadText(path) : null;

        /// <summary>
        /// npcidentity's <c>JT_NAME = id</c> constants, then petinfo's PetIllustNameTable_Eng / PetFoodTable rows
        /// <c>[jobtbl.JT_NAME] = value</c>.
        /// </summary>
        public static void ParsePetInfo(string identity, string petInfo, out Dictionary<int, string> illust, out Dictionary<int, int> food) =>
            ParsePetInfo(identity, petInfo, out illust, out food, out _, out _);

        /// <summary>The same with PetStringTable (job → name) and PetEggItemID_PetJobID (<c>[egg] = jobtbl.JT_NAME</c>).</summary>
        public static void ParsePetInfo(string identity, string petInfo, out Dictionary<int, string> illust, out Dictionary<int, int> food,
            out Dictionary<int, string> name, out Dictionary<int, int> eggJob) {
            illust = new Dictionary<int, string>();
            food = new Dictionary<int, int>();
            name = new Dictionary<int, string>();
            eggJob = new Dictionary<int, int>();
            var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(identity ?? string.Empty, @"\b(JT_[A-Za-z0-9_]+)\s*=\s*(\d+)")) {
                ids[m.Groups[1].Value] = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            string table = null;
            foreach (string raw in (petInfo ?? string.Empty).Split('\n')) {
                string line = raw.Trim();
                Match head = Regex.Match(line, @"^([A-Za-z_0-9]+)\s*=\s*\{");
                if (head.Success) {
                    table = head.Groups[1].Value;
                    continue;
                }

                if (table == "PetEggItemID_PetJobID") {
                    Match egg = Regex.Match(line, @"^\[(\d+)\]\s*=\s*jobtbl\.(JT_[A-Za-z0-9_]+)");
                    if (egg.Success && ids.TryGetValue(egg.Groups[2].Value, out int eggJobId)) eggJob[int.Parse(egg.Groups[1].Value, CultureInfo.InvariantCulture)] = eggJobId;
                    continue;
                }

                if (table != "PetIllustNameTable_Eng" && table != "PetFoodTable" && table != "PetStringTable") continue;
                Match row = Regex.Match(line, @"^\[jobtbl\.(JT_[A-Za-z0-9_]+)\]\s*=\s*(""([^""]*)""|(\d+))");
                if (!row.Success || !ids.TryGetValue(row.Groups[1].Value, out int job)) continue;
                if (table == "PetIllustNameTable_Eng" && row.Groups[3].Success) illust[job] = row.Groups[3].Value;
                else if (table == "PetStringTable" && row.Groups[3].Success) name[job] = row.Groups[3].Value;
                else if (table == "PetFoodTable" && row.Groups[4].Success) food[job] = int.Parse(row.Groups[4].Value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>GetPetIllustName_V3: "UserInterface\illust\" + PetIllustNameTable_Eng[job], else PET_NOIMAGE.bmp.</summary>
        internal static string Illustration(int job) {
            LoadTables();
            string name = illustrations != null && illustrations.TryGetValue(job, out string file) ? file : "PET_NOIMAGE.bmp";
            return "UserInterface\\illust\\" + name;
        }

        private static int Food(int job) {
            LoadTables();
            return foods != null && foods.TryGetValue(job, out int id) ? id : 0;
        }

        /// <summary>FUN_00d11b90: the egg's pet job (PetEggItemID_PetJobID), −1 for an unknown egg.</summary>
        internal static int EggJob(int egg) {
            LoadTables();
            return eggJobs != null && eggJobs.TryGetValue(egg, out int job) ? job : -1;
        }

        /// <summary>FUN_00d12c40: PetStringTable's name of the job, null without one.</summary>
        internal static string PetName(int job) {
            LoadTables();
            return names != null && names.TryGetValue(job, out string name) ? name : null;
        }

        // ------------------------------------------------------------------ 0x5a

        /// <summary>FUN_00c5e230: the listed eggs; OK sends CZ_SELECT_PETEGG with the row's index, Cancel only closes.</summary>
        private void OpenEggs(int[] list, int serial) {
            var eggs = new List<RoItemInstance>();
            RoItemInstance[] inventory = session.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>();
            foreach (int index in list ?? Array.Empty<int>()) {
                foreach (RoItemInstance item in inventory) {
                    if (item != null && item.BagIndex == index) {
                        eggs.Add(item);
                        break;
                    }
                }
            }

            RoClassicItemSelect.Show(PetEgg, serial, eggs, metadata, picked => _ = session?.BeginLegacySelectPetEgg(picked.BagIndex), () => { });
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (combo == null || !combo.Visible) return;
            // 0x9d5240: a left click outside the menu closes 0x104; the row under the pointer is highlighted
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && system.Windows.HoverWindow != combo) {
                CloseCombo();
                return;
            }

            int hover = system.Windows.HoverWindow == combo ? (system.Windows.Mouse.y - combo.Position.y) / 16 : -1;
            if (hover != comboHover) {
                comboHover = hover;
                combo.Invalidate();
            }
        }

        /// <summary>The sweep console: info | cmd n | menu | rename name | state.</summary>
        public static string Sweep(string command) {
            RoClassicPet p = instance;
            if (p == null) return "no pet producer";
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            switch (a[0]) {
                case "info":
                    ToggleInfo();
                    break;
                case "cmd":
                    if (a.Length > 1 && int.TryParse(a[1], out int row)) p.Command(row);
                    break;
                case "feedyes":
                    if (p.pet.Active) _ = p.session?.BeginLegacyPetCommand(1);
                    break;
                case "menu":
                    if (p.window != null && p.window.Visible) p.OpenCommands();
                    break;
                case "feedmsg":
                    p.OnFeed(a.Length > 1 && a[1] != "0", (uint) Food(p.pet.Job));
                    break;
                case "evo":
                    return "evo: egg=" + p.CurrentEgg() + " " + RoClassicPetEvolution.Sweep(a.Length > 1 ? a[1] : "state");
                case "rename":
                    if (a.Length > 1 && p.nameEdit != null) {
                        p.nameEdit.SetText(a[1]);
                        p.Rename();
                    }

                    break;
            }

            RoClassicPetState s = p.pet;
            return "visible=" + (p.window != null && p.window.Visible) + " active=" + s.Active + " name=" + s.Name + " renamed=" + s.Renamed +
                   " lv=" + s.Level + " hunger=" + s.Hunger + " intimacy=" + s.Intimacy + " acc=" + s.Accessory + " job=" + s.Job + " gid=" + s.Gid +
                   " illust=" + (ValidClass(s.Job) ? Illustration(s.Job) : "-") + " food=" + Food(s.Job);
        }
    }
}
