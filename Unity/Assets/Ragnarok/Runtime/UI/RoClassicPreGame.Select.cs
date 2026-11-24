using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Entities;
using Ragnarok.Sprites;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UINewSelectCharWnd 0x115 (RUNTIME-RULES-PRE-GAME.md "characters"): 997 × 626 centred (signed truncation) over its
    /// UIRPImageWnd backdrop bg_back1.tga; 15 slots a page (x = 10 + 157c, y = 21 + 195r, 161 × 197, UISlotBmpButton
    /// 0x7efdc0: img_slot2_* while empty, img_slot_* with a character, img_slot_lock beyond the creatable slots; the
    /// selected one under img_slot2_select or the eight-frame img_slot_select%d; the "Click here to create a Character"
    /// balloon on the first while the account has none; Add_Ons.bmp while a slot change or rename is left) with the
    /// character drawn once into an image (feet at (80, 137) of the slot) and its name (bold 11, #374177) centred on
    /// 88 + 157c at y 189 + 195r; the info panel's twelve values; the page text and arrows; Game Start 0xb4 (white bold 18
    /// over a #D7D7D7 shadow, #DC747F while disabled; sound, CZ_SELECT_CHAR), Delete Character 0x193 / Cancel Deletion
    /// 0x194 (#313131 over the shadow, #909090 disabled), the close X 0xb5 (0xd68, back to the login).
    /// </summary>
    public sealed partial class RoClassicPreGame {
        private const int SelW = 997, SelH = 626, SlotsPerPage = 15;
        private const int GameStartId = 0xb4, SelCloseId = 0xb5, ReserveDeleteId = 0x193, CancelDeleteId = 0x194, DeleteId = 0xcf, NoticeId = 0x11a;
        private const int PageLeftId = 0x138, PageRightId = 0x139, SmallMakeId = 0x19c, SmallDeleteId = 0x19d;
        private const string Sel = "유저인터페이스\\select_character_ver3\\";
        private const int PortraitW = 120, PortraitH = 150, PortraitFeetY = 140, SlotFeetX = 80, SlotFeetY = 137;
        // UISlotBmpButton vf19: +0x134 counts process ticks, the frame is count / 5 of eight (FUN_007cfda0 sets the 5)
        private const float SlotFrameSeconds = 5f / 60f;
        private static readonly string[] SlotSelectFrames = {
            Sel + "img_slot_select0.bmp", Sel + "img_slot_select1.bmp", Sel + "img_slot_select2.bmp", Sel + "img_slot_select3.bmp",
            Sel + "img_slot_select4.bmp", Sel + "img_slot_select5.bmp", Sel + "img_slot_select6.bmp", Sel + "img_slot_select7.bmp",
        };
        private static readonly Color32 ButtonShadow = new Color32(0xd7, 0xd7, 0xd7, 255);

        private RoClassicWindow select;
        private RoLoginShellSnapshot selSnapshot;
        private int selPage, selSlot = -1, selHover = -1, selFrame;
        private bool selHoverPressed, selTaken;
        // the buttons' shown state: bit 0 a character, bit 1 queued for deletion (−1: not set up)
        private int selButtonState = -1;
        private float nextSlotFrame;
        private RoAssetDatabase portraitAssets;
        private readonly Dictionary<int, string> portraitKeys = new Dictionary<int, string>();
        private readonly HashSet<string> portraitPending = new HashSet<string>();
        private readonly HashSet<string> portraitReady = new HashSet<string>();
        private readonly List<string> balloonLines = new List<string>();
        private readonly Dictionary<int, (int Date, string Day, string Time)> deleteTexts = new Dictionary<int, (int, string, string)>();
        private string balloonSource;

        public static bool SelectVisible => instance?.select != null && instance.select.Visible;

        /// <summary>A window of the pre-game screens (login, wait, server list, character select / make, their box).</summary>
        public static bool OwnsWindow(RoClassicWindow w) {
            RoClassicPreGame p = instance;
            return p != null && w != null && (w == p.login || w == p.wait || w == p.server || w == p.select || w == p.make || w == p.message?.Window);
        }

        private void SetSelect(bool visible, RoLoginShellSnapshot snapshot) {
            if (!visible) {
                if (select != null) select.Visible = false;
                // the buttons are set up again when the window comes back (Game Start's bt_gameStart_on lasts till then)
                selButtonState = -1;
                return;
            }

            selSnapshot = snapshot;
            if (select == null) {
                TextAsset asset = Resources.Load<TextAsset>(Layouts + "UINewSelectCharWnd");
                if (asset == null) return;
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
                // the backdrop window under the transparent selection window
                layout.Ops.Insert(Math.Min(1, layout.Ops.Count), new RoClassicOp { Kind = RoClassicOpKind.Bitmap, Path = Sel + "bg_back1.tga", X = 0, Y = 0 });
                // slots, names, info values and the page texts are drawn from the data
                layout.Children.RemoveAll(c => c.Class == "UISlotBmpButton" || c.Class == "UIStaticText" || c.Class == "UIStaticBitmap");
                // FUN_007c9be0: Game Start's text is bold Arial 18, the info buttons' Arial 12
                layout.Fonts["gs"] = new RoClassicFontSpec("Arial", 21, 700, 0, (int) RoClassicGdiFont.NonAntialiasedQuality);
                layout.Fonts["b12"] = new RoClassicFontSpec("Arial", 15, 400, 0, (int) RoClassicGdiFont.NonAntialiasedQuality);
                foreach (RoClassicChildLayout c in layout.Children) {
                    if (c.Id == SmallMakeId || c.Id == SmallDeleteId || c.Id == NoticeId || c.Id == DeleteId) c.Visible = false;
                }

                select = new RoClassicWindow(system, layout) { Visible = false, Draggable = false, ClickableAt = _ => true };
                select.DrawDynamic += DrawSelect;
                select.Clicked += OnSelectClicked;
                select.Pressed += (w, local) => {
                    int slot = SlotAt(local);
                    if (slot >= 0) PickSlot(slot);
                };
                select.DoubleClicked += (w, local) => {
                    if (SlotAt(local) >= 0) GameStart();
                };
            }

            Vector2Int client = system.ClientSize;
            select.MoveTo((client.x - SelW) / 2, (client.y - SelH) / 2);
            if (selSlot < 0) {
                selSlot = Mathf.Clamp(PlayerPrefs.GetInt("ro.ui.classic.curslot", 0), 0, Math.Max(0, TotalSlots() - 1));
                selPage = selSlot / SlotsPerPage;
            }

            SyncSelectButtons();
            if (!select.Visible) {
                select.Visible = true;
                system.Windows.BringToFront(select);
            }

            select.Invalidate();
        }

        private int TotalSlots() {
            int total = selSnapshot != null ? Math.Max((int) selSnapshot.SlotLimits.TotalSlots, selSnapshot.CharacterListTotalSlots) : 0;
            foreach (RoLoginShellCharacter c in selSnapshot?.Characters ?? Array.Empty<RoLoginShellCharacter>()) total = Math.Max(total, c.Slot + 1);
            return Math.Max(SlotsPerPage, total);
        }

        private int PageCount() => Math.Max(1, (TotalSlots() + SlotsPerPage - 1) / SlotsPerPage);

        /// <summary>The creatable slots (HC_ACCEPT_ENTER2's producible count); without the packet every slot is open.</summary>
        private bool SlotLocked(int slot) {
            byte producible = selSnapshot != null ? selSnapshot.SlotLimits.ProducibleSlots : (byte) 0;
            return selSnapshot != null && selSnapshot.SlotLimits.PacketLength > 0 && slot >= producible;
        }

        private RoLoginShellCharacter? CharacterAt(int slot) {
            foreach (RoLoginShellCharacter c in selSnapshot?.Characters ?? Array.Empty<RoLoginShellCharacter>()) {
                if (c.Slot == slot && c.CharacterId > 0) return c;
            }

            return null;
        }

        private bool HasCharacters() {
            foreach (RoLoginShellCharacter c in selSnapshot?.Characters ?? Array.Empty<RoLoginShellCharacter>()) {
                if (c.CharacterId > 0) return true;
            }

            return false;
        }

        private int SlotAt(Vector2Int local) {
            for (int i = 0; i < SlotsPerPage; i++) {
                int c = i % 5, r = i / 5;
                if (new RectInt(10 + 157 * c, 21 + 195 * r, 161, 197).Contains(local)) return selPage * SlotsPerPage + i;
            }

            return -1;
        }

        /// <summary>Slot 200: select; an empty creatable slot (199) opens the creation screen.</summary>
        private void PickSlot(int slot) {
            selSlot = slot;
            PlayerPrefs.SetInt("ro.ui.classic.curslot", slot);
            RoLoginShellCharacter? c = CharacterAt(slot);
            if (c.HasValue) {
                shell.SelectCharacterFromPhase11Ui(c.Value.Name);
            } else if (!SlotLocked(slot)) {
                OpenCreate(slot);
                return;
            }

            SyncSelectButtons();
            select.Invalidate();
        }

        /// <summary>
        /// The buttons' art and text per state (FUN_007c9be0 entries 0..2 enabled with the #D7D7D7 shadow at +1, entry 3
        /// disabled): rebuilt only when what they show changes.
        /// </summary>
        private void SyncSelectButtons() {
            RoLoginShellCharacter? c = CharacterAt(selSlot);
            bool queued = c.HasValue && c.Value.DeleteRevisionDate > 0;
            bool due = queued && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= c.Value.DeleteRevisionDate;
            bool on = c.HasValue && !queued;
            int state = (c.HasValue ? 1 : 0) | (queued ? 2 : 0) | (due ? 4 : 0);
            selTaken = c.HasValue;
            if (state == selButtonState) {
                SyncPageArrows();
                return;
            }

            selButtonState = state;
            foreach (RoClassicChildLayout child in select.Layout.Children) {
                switch (child.Id) {
                    case GameStartId:
                        SetButtonArt(child, on, "bt_gameStart_off.bmp", "bt_gameStart_over.bmp", "bt_gameStart_press.bmp", "bt_gameStart_off.bmp");
                        SetButtonText(child, RoClassicUiHost.MsgStr(0xd0d, "Game Start"), "gs", 57, on, new Color32(255, 255, 255, 255), new Color32(0xdc, 0x74, 0x7f, 255));
                        break;
                    case ReserveDeleteId:
                        select.SetChildVisible(child, !queued);
                        SetButtonArt(child, c.HasValue, "bt_info_normal.bmp", "bt_info_over.bmp", "bt_info_press.bmp", "bt_info_normal.bmp");
                        SetButtonText(child, "Delete Character", "b12", 5, c.HasValue, new Color32(0x31, 0x31, 0x31, 255), new Color32(0x90, 0x90, 0x90, 255));
                        break;
                    case DeleteId:
                        // FUN_007351f0 / 0x730d80: under Cancel Deletion, shown only once the deletion date has passed
                        select.SetChildVisible(child, due);
                        SetButtonArt(child, true, "bt_info_normal.bmp", "bt_info_over.bmp", "bt_info_press.bmp", "bt_info_normal.bmp");
                        SetButtonText(child, RoClassicUiHost.MsgStr(0xd0e, "Delete"), "b12", 5, true, new Color32(0x31, 0x31, 0x31, 255), new Color32(0x90, 0x90, 0x90, 255));
                        break;
                    case CancelDeleteId:
                        select.SetChildVisible(child, queued);
                        SetButtonArt(child, true, "bt_info_normal.bmp", "bt_info_over.bmp", "bt_info_press.bmp", "bt_info_normal.bmp");
                        SetButtonText(child, "Cancel Deletion", "b12", 5, true, new Color32(0x31, 0x31, 0x31, 255), new Color32(0x90, 0x90, 0x90, 255));
                        break;
                }
            }

            SyncPageArrows();
            select.Invalidate();
        }

        /// <summary>0x138 / 0x139: bt_*_on / over / press while a page lies that way, bt_*_off otherwise.</summary>
        private void SyncPageArrows() {
            foreach (RoClassicChildLayout child in select.Layout.Children) {
                if (child.Id == PageLeftId) SetButtonArt(child, selPage > 0, "bt_left_on.bmp", "bt_left_over.bmp", "bt_left_press.bmp", "bt_left_off.bmp");
                else if (child.Id == PageRightId) SetButtonArt(child, selPage + 1 < PageCount(), "bt_right_on.bmp", "bt_right_over.bmp", "bt_right_press.bmp", "bt_right_off.bmp");
            }
        }

        private static void SetButtonArt(RoClassicChildLayout child, bool on, string normal, string hover, string pressed, string off) {
            child.States["normal"] = Sel + (on ? normal : off);
            child.States["hover"] = Sel + (on ? hover : off);
            child.States["pressed"] = Sel + (on ? pressed : off);
        }

        /// <summary>The button's recorded ops become its text: centred, over the shadow while enabled.</summary>
        private static void SetButtonText(RoClassicChildLayout child, string text, string font, int y, bool on, Color32 color, Color32 disabled) {
            child.Ops.Clear();
            if (on) child.Ops.Add(new RoClassicOp { Kind = RoClassicOpKind.Text, X = 1, Y = y + 1, BoxW = child.W, Align = RoClassicTextAlign.Center, Text = text, Font = font, Color = ButtonShadow });
            child.Ops.Add(new RoClassicOp { Kind = RoClassicOpKind.Text, X = 0, Y = y, BoxW = child.W, Align = RoClassicTextAlign.Center, Text = text, Font = font, Color = on ? color : disabled });
        }

        private void OnSelectClicked(RoClassicChildLayout child) {
            RoLoginShellCharacter? c = CharacterAt(selSlot);
            switch (child.Id) {
                case GameStartId:
                    GameStart();
                    break;
                case SelCloseId:
                    // 0x7338c0: CSV 0xd68 in the return-login state, then mode 0x271b
                    RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0xd68, "Do you want to return to the login screen?"), RoClassicMessageBox.TypeOkCancel, true, r => {
                        if (r == RoClassicMessageBox.IdYes) shell.ResetFromPhase11Ui();
                    });
                    break;
                case ReserveDeleteId:
                    if (c.HasValue) {
                        RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x13, "Are you sure that you want to delete this character?"), RoClassicMessageBox.TypeOkCancel, false, r => {
                            if (r == RoClassicMessageBox.IdYes) shell.ReserveDeleteCharacterFromPhase11Ui(c.Value.CharacterId);
                        });
                    }

                    break;
                case CancelDeleteId:
                    if (c.HasValue) shell.CancelDeleteCharacterFromPhase11Ui(c.Value.CharacterId);
                    break;
                case DeleteId:
                    // vf37 0x7338c0 case 0xcf: CSV 0x13 OK / Cancel, then the deletion password (UIEmailAddressWnd)
                    if (c.HasValue && c.Value.DeleteRevisionDate > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= c.Value.DeleteRevisionDate) {
                        int characterId = c.Value.CharacterId;
                        RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x13, "Are you sure that you want to delete this character?"), RoClassicMessageBox.TypeOkCancel, false, r => {
                            if (r == RoClassicMessageBox.IdYes) ShowDeletionPassword(characterId);
                        });
                    }

                    break;
                case PageLeftId:
                    if (selPage > 0) {
                        selPage--;
                        SyncPageArrows();
                        select.Invalidate();
                    }

                    break;
                case PageRightId:
                    if (selPage + 1 < PageCount()) {
                        selPage++;
                        SyncPageArrows();
                        select.Invalidate();
                    }

                    break;
            }
        }

        /// <summary>Game Start 0xb4: an existing character not queued for deletion; the sound, then CZ_SELECT_CHAR.</summary>
        private void GameStart() {
            // only from the shown selection: a second start once the map entry began would tear the live map session
            // down and log in again, which the server refuses while the account is still on the map (auth result 8)
            if (select == null || !select.Visible) return;
            RoLoginShellSnapshot snapshot = shell.GetSnapshotForPhase11Ui();
            if (snapshot == null || snapshot.Stage != RoLoginShellStage.CharacterSelect) return;
            RoLoginShellCharacter? c = CharacterAt(selSlot);
            if (!c.HasValue || c.Value.DeleteRevisionDate > 0) return;
            Ragnarok.Audio.RoAudioEvents.PlayButtonSound();
            // 0x7338c0: the button shows bt_gameStart_on in every state once the start is sent
            foreach (RoClassicChildLayout child in select.Layout.Children) {
                if (child.Id == GameStartId) SetButtonArt(child, true, "bt_gameStart_on.bmp", "bt_gameStart_on.bmp", "bt_gameStart_on.bmp", "bt_gameStart_on.bmp");
            }

            select.Invalidate();
            // CZ_SELECT_CHAR takes the slot: the shell enters with its own slot field, not the chosen name
            shell.CharacterSlot = c.Value.Slot;
            shell.SelectCharacterFromPhase11Ui(c.Value.Name);
            shell.EnterMapFromPhase11Ui();
        }

        private void DrawSelect(RoClassicWindow w) {
            int first = selPage * SlotsPerPage;
            var nameColor = new Color32(0x37, 0x41, 0x77, 255);
            bool none = !HasCharacters();
            for (int i = 0; i < SlotsPerPage; i++) {
                int slot = first + i, c = i % 5, r = i / 5, x = 10 + 157 * c, y = 21 + 195 * r;
                RoLoginShellCharacter? ch = CharacterAt(slot);
                bool locked = SlotLocked(slot), hover = slot == selHover;
                // the button's state art (+0x1ab: img_slot2_* empty, img_slot_* taken), then the selection over it
                string art = locked ? "img_slot_lock.bmp"
                    : (ch.HasValue ? "img_slot_" : "img_slot2_") + (hover && selHoverPressed ? "press.bmp" : hover ? "over.bmp" : "normal.bmp");
                w.DrawBitmap(x, y, Sel + art);
                if (!locked && slot == selSlot) w.DrawBitmap(x, y, ch.HasValue ? SlotSelectFrames[selFrame] : Sel + "img_slot2_select.bmp");
                if (slot == 0 && none) DrawBalloon(w, x, y);
                if (!ch.HasValue) continue;
                RoLoginShellCharacter s = ch.Value;
                if (s.SlotChangeCount > 0 || s.NameChangeCount > 0) w.DrawBitmap(x + 19, y + 25, "유저인터페이스\\Add_Ons.bmp");
                string key = Portrait(s);
                if (key != null) w.DrawBitmap(x + SlotFeetX - PortraitW / 2, y + SlotFeetY - PortraitFeetY, key);
                string name = s.Name ?? string.Empty;
                w.TextOut(88 + 157 * c - w.FontFor(0, 12).Measure(name) / 2, 189 + 195 * r, name, 0, 11, nameColor, true);
                if (s.DeleteRevisionDate > 0) DrawDeleteDate(w, x, y, s);
            }

            // the info panel (the static bitmap child, drawn here under its values): the map first, then job, level, base exp, HP, SP, STR..LUK (FUN_00735420)
            var black = new Color32(0, 0, 0, 255);
            w.DrawBitmap(809, 26, Sel + "img_info.bmp");
            RoLoginShellCharacter? sel = CharacterAt(selSlot);
            if (sel.HasValue) {
                RoLoginShellCharacter s = sel.Value;
                string[] values = {
                    JobName(s), s.BaseLevel.ToString(CultureInfo.InvariantCulture), s.Experience.ToString(CultureInfo.InvariantCulture),
                    s.Hp.ToString(CultureInfo.InvariantCulture), s.Sp.ToString(CultureInfo.InvariantCulture),
                    s.Strength.ToString(CultureInfo.InvariantCulture), s.Agility.ToString(CultureInfo.InvariantCulture), s.Vitality.ToString(CultureInfo.InvariantCulture),
                    s.Intelligence.ToString(CultureInfo.InvariantCulture), s.Dexterity.ToString(CultureInfo.InvariantCulture), s.Luck.ToString(CultureInfo.InvariantCulture),
                };
                // each value is a 107 × 15 static text: clipped to its box
                w.ClipTo(868, 116, 107, 15);
                w.TextOut(868, 116, RoClassicUiHost.MapDisplayName(s.MapName), 0, 11, black);
                w.ResetClip();
                for (int i = 0; i < values.Length; i++) {
                    w.ClipTo(868, 133 + 17 * i, 107, 15);
                    w.TextOut(868, 133 + 17 * i, values[i], 0, 11, black);
                    w.ResetClip();
                }
            }

            // the page: "Character List" #576691 and "%d / %d" white (the arrows are their buttons)
            w.TextOut(856, 538, "Character List", 0, 12, new Color32(0x57, 0x66, 0x91, 255));
            w.TextOut(879, 564, (selPage + 1).ToString(CultureInfo.InvariantCulture) + " / " + PageCount().ToString(CultureInfo.InvariantCulture), 0, 18, new Color32(255, 255, 255, 255));
        }

        /// <summary>
        /// 0x7efdc0 on the first slot while the account has no character: balloon.bmp at (w / 2 − 46, 26), CSV 0xaed wrapped
        /// at 20 bytes in white height 11, centred in 92 px from (33, 34) every 13 px.
        /// </summary>
        private void DrawBalloon(RoClassicWindow w, int x, int y) {
            w.DrawBitmap(x + 161 / 2 - 46, y + 26, Sel + "balloon.bmp");
            string text = RoClassicUiHost.MsgStr(0xaed, "Click here to create a Character");
            if (!ReferenceEquals(text, balloonSource)) {
                balloonSource = text;
                RoClassicText.ByteWrap(text, 20, wrapScratch);
                balloonLines.Clear();
                foreach ((int start, int end) in wrapScratch) balloonLines.Add(text.Substring(start, end - start).TrimEnd());
            }

            RoClassicGdiFont font = w.FontFor(0, 11);
            for (int i = 0; i < balloonLines.Count; i++) {
                w.TextOut(x + 33 + (92 - font.Measure(balloonLines[i])) / 2, y + 34 + 13 * i, balloonLines[i], 0, 11, new Color32(255, 255, 255, 255));
            }
        }

        /// <summary>
        /// 0x7efdc0 for a character queued for deletion: CSV 0xc20 "Deletable on: %02d/%02d" (month, day) and 0xd15
        /// "%02d:%02d:%02d" in local time, height 11 centred on x 79 at y 40 / 52; red until the date, blue from it.
        /// </summary>
        private void DrawDeleteDate(RoClassicWindow w, int x, int y, RoLoginShellCharacter s) {
            if (!deleteTexts.TryGetValue(s.CharacterId, out var texts) || texts.Date != s.DeleteRevisionDate) {
                DateTime local = DateTimeOffset.FromUnixTimeSeconds(s.DeleteRevisionDate).LocalDateTime;
                string day = string.Format(CultureInfo.InvariantCulture, CFormat(RoClassicUiHost.MsgStr(0xc20, "Deletable on: %02d/%02d")), local.Month, local.Day);
                string time = string.Format(CultureInfo.InvariantCulture, CFormat(RoClassicUiHost.MsgStr(0xd15, "%02d:%02d:%02d")), local.Hour, local.Minute, local.Second);
                texts = (s.DeleteRevisionDate, day, time);
                deleteTexts[s.CharacterId] = texts;
            }

            bool due = DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= s.DeleteRevisionDate;
            var color = due ? new Color32(0, 0, 255, 255) : new Color32(255, 0, 0, 255);
            RoClassicGdiFont font = w.FontFor(0, 11);
            w.TextOut(x + 79 - font.Measure(texts.Day) / 2, y + 40, texts.Day, 0, 11, color);
            w.TextOut(x + 79 - font.Measure(texts.Time) / 2, y + 52, texts.Time, 0, 11, color);
        }

        /// <summary>A C format with %02d / %d turned into .NET placeholders in order.</summary>
        private static string CFormat(string format) {
            var sb = new System.Text.StringBuilder(format.Length + 8);
            int n = 0;
            for (int i = 0; i < format.Length; i++) {
                if (format[i] == '%' && i + 1 < format.Length) {
                    if (format[i + 1] == '%') {
                        sb.Append('%');
                        i++;
                        continue;
                    }

                    int j = i + 1;
                    bool pad = j + 1 < format.Length && format[j] == '0' && format[j + 1] == '2';
                    if (pad) j += 2;
                    if (j < format.Length && format[j] == 'd') {
                        sb.Append('{').Append(n++).Append(pad ? ":00}" : "}");
                        i = j;
                        continue;
                    }
                }

                if (format[i] == '{' || format[i] == '}') sb.Append(format[i]);
                sb.Append(format[i]);
            }

            return sb.ToString();
        }

        private string JobName(RoLoginShellCharacter c) => JobNameOf(c.JobId);

        /// <summary>The character's image, rendered once per appearance.</summary>
        private string Portrait(RoLoginShellCharacter c) {
            int hash = HashCode.Combine(c.JobId, c.Sex, c.Head, c.HeadPalette, c.HairColor, c.BodyPalette, c.Accessory, HashCode.Combine(c.Accessory2, c.Accessory3, c.DeleteRevisionDate > 0));
            string key = "@charsel-" + c.CharacterId.ToString(CultureInfo.InvariantCulture) + "-" + hash.ToString("x", CultureInfo.InvariantCulture);
            if (portraitReady.Contains(key)) return key;
            if (portraitPending.Contains(key)) return null;
            if (portraitAssets == null && (shell == null || !shell.TryGetBrowserContentAssets(out portraitAssets))) return null;
            var request = new RoActorAppearanceRequest {
                kind = RoActorAppearanceKind.Player,
                serverId = c.CharacterId,
                classId = c.JobId,
                bodyStyleId = c.Body,
                isMale = c.Sex != 0,
                headId = Math.Max(0, (int) c.Head),
                hairId = Math.Max(0, (int) c.Head),
                hairColorId = c.HeadPalette != 0 ? c.HeadPalette : c.HairColor,
                bodyPaletteId = c.BodyPalette,
                headgearTopId = c.Accessory2,
                headgearMidId = c.Accessory3,
                headgearLowerId = c.Accessory,
                motionProfile = RoSpriteMotionProfile.Player,
                motion = c.DeleteRevisionDate > 0 ? RoSpriteMotion.Sit : RoSpriteMotion.Idle,
                facingDirection = RoActorDirection.South,
                headFacing = RoActorHeadFacing.Center,
            };
            portraitPending.Add(key);
            bool started = RoClassicActorPortrait.Render(portraitAssets, request, key, PortraitW, PortraitH, PortraitFeetY, ok => {
                portraitPending.Remove(key);
                if (ok) portraitReady.Add(key);
                select?.Invalidate();
            });
            if (!started) portraitPending.Remove(key);
            return null;
        }

        /// <summary>
        /// Enter (the default button, Game Start) on the selected character; the hover / press of the slots, and the selected
        /// character's frame (eight frames, one per 5 ticks).
        /// </summary>
        public void SelectTick() {
            if (select == null || !select.Visible) return;
            SyncSelectButtons();
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && !RoClassicMessageBox.ModalOpen && !RoClassicTextInput.HasFocus &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) {
                GameStart();
            }

            int hover = system.Windows.HoverWindow == select ? SlotAt(system.Windows.Mouse - select.Position) : -1;
            bool pressed = hover >= 0 && UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
            if (hover != selHover || pressed != selHoverPressed) {
                selHover = hover;
                selHoverPressed = pressed;
                select.Invalidate();
            }

            if (!selTaken || selSlot / SlotsPerPage != selPage) return;
            float now = Time.unscaledTime;
            if (now < nextSlotFrame) return;
            nextSlotFrame = now + SlotFrameSeconds;
            selFrame = (selFrame + 1) % SlotSelectFrames.Length;
            select.Invalidate();
        }
    }
}
