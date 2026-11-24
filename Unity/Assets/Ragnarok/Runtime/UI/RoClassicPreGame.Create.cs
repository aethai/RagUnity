using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Entities;
using Ragnarok.Sprites;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UINewMakeCharWnd 0x116 (RUNTIME-RULES-PRE-GAME.md "0x116"): 794 × 422 at ((W − 794) / 2, (H − 422) / 2 − 70) over
    /// make_character_ver2\bg_makeBg.bmp, the title 0xd1b at (44, 17) in #5F5F5F; the race controls Human (21, 36) and Doram
    /// (21, 202), Male / Female (437 / 500, 59), the rotation arrows (426 / 551, 179), the name edit (435, 268, 130 × 20, 23
    /// bytes), Hair Style 0xd12 with 24 style buttons (606 + 38·(i % 4), 59 + 37·⌊i / 4⌋; 23 human / 6 doram styles), Hair
    /// Color 0xd13 with nine colours (619 + 27·(i % 5), 306 + 24·⌊i / 5⌋), Return 0xd17 (25, 379) / Create 0xd11 (616, 379)
    /// and the close X (768, 9). Create checks the name (shorter than 4: 0x15, longer than 23: 0x213) and sends CZ_MAKE_CHAR.
    /// </summary>
    public sealed partial class RoClassicPreGame {
        private const int MakeW = 794, MakeH = 422;
        private const string Make = "유저인터페이스\\make_character_ver2\\";
        private const int MakeCreateId = 0xb4, MakeCloseId = 0xb5, MakeReturnId = 0xb6, RotateLeftId = 0xdf, RotateRightId = 0xde;
        private const int PreviewW = 120, PreviewH = 150, PreviewFeetY = 140, PreviewFeetAtY = 228;
        // DAT_0155569c: the Doram race is open (Custom creates Doram; closed, its control shows img_doram_comingSoon)
        private const bool DoramOpen = true;
        private const int RaceHuman = 0, RaceDoram = 1, DoramJob = 0x107a;
        private const int RaceX = 21, RaceW = 364, RaceH = 166;
        private static readonly int[] RaceY = { 36, 202 };
        private static readonly int[] RaceStyles = { 23, 6 };
        private static readonly string[] RaceOn = { Make + "img_human_on.bmp", Make + "img_doram_on.bmp" };
        private static readonly string[] RaceOff = { Make + "img_human_off.bmp", DoramOpen ? Make + "img_doram_off.bmp" : Make + "img_doram_comingSoon.bmp" };
        private static readonly string[] ColorOn = Numbered(9, i => Make + "color" + i.ToString("00", CultureInfo.InvariantCulture) + "_on.bmp");
        private static readonly string[] ColorOff = Numbered(9, i => Make + "color" + i.ToString("00", CultureInfo.InvariantCulture) + "_off.bmp");
        // UIHairBitmap (UIHairStyleButton msg 0x28) per race and sex: [race * 2 + (male ? 1 : 0)][style − 1]
        private static readonly string[][] HairHeads = {
            Numbered(23, i => Make + "img_hairStyle_girl" + i.ToString("00", CultureInfo.InvariantCulture) + ".bmp"),
            Numbered(23, i => Make + "img_hairStyle" + i.ToString("00", CultureInfo.InvariantCulture) + ".bmp"),
            Numbered(6, i => Make + "img_hairStyle_doramGirl" + i.ToString("00", CultureInfo.InvariantCulture) + ".bmp"),
            Numbered(6, i => Make + "img_hairStyle_doramBoy" + i.ToString("00", CultureInfo.InvariantCulture) + ".bmp"),
        };

        private RoClassicWindow make;
        private RoClassicEditField makeName;
        private bool createOpen, createSent, makeMale = true;
        private int createSlot, makeRace = RaceHuman, makeStyle = 1, makeColor, makeRotation, previewVersion, previewShown = -1;
        private string previewKey;

        // UIDescriptRace (FUN_00801b50): each race's novice stands (action 1: idle, direction 1); the selected one walks
        // (action 9), a frame every 60 ms. Images per race × sex × walk: [race * 4 + (male ? 2 : 0) + (walk ? 1 : 0)].
        private const float RaceFrameSeconds = 0.06f;
        private readonly string[][] raceFrameKeys = new string[8][];
        // 0: not rendered, −1: in flight, −2: failed, n: ready with n frames
        private readonly int[] raceFrameCounts = new int[8];
        private int raceFrame;
        private float nextRaceFrame;
        private static readonly List<(int Start, int End)> wrapScratch = new List<(int Start, int End)>();
        private static readonly List<string>[] raceLines = { new List<string>(), new List<string>() };
        private static readonly string[] raceDescription = new string[2];

        public static bool CreateVisible => instance?.make != null && instance.make.Visible;

        private static string[] Numbered(int count, Func<int, string> name) {
            var names = new string[count];
            for (int i = 0; i < count; i++) names[i] = name(i + 1);
            return names;
        }

        /// <summary>An empty creatable slot (199): MakeWindow(0x116), DeleteWindow(0x115).</summary>
        private void OpenCreate(int slot) {
            createSlot = slot;
            createOpen = true;
            createSent = false;
            makeRace = RaceHuman;
            makeStyle = 1;
            makeColor = 0;
            makeRotation = 0;
            makeName?.SetText(string.Empty);
            previewVersion++;
            seenSequence = int.MinValue;
        }

        private void CloseCreate() {
            createOpen = false;
            if (make != null) make.Visible = false;
            seenSequence = int.MinValue;
        }

        private static RoClassicChildLayout MakeButton(int id, string path, int x, int y, int w, int h, string normal, string hover, string pressed) {
            var b = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            b.States["normal"] = Make + normal;
            b.States["hover"] = Make + hover;
            b.States["pressed"] = Make + pressed;
            return b;
        }

        private static RoClassicChildLayout Labelled(RoClassicChildLayout button, string text) {
            button.Ops.Add(new RoClassicOp { Kind = RoClassicOpKind.Text, X = 0, Y = 6, BoxW = button.W, Align = RoClassicTextAlign.Center, Text = text, Font = "f14", Color = new Color32(0x31, 0x31, 0x31, 255) });
            return button;
        }

        private void SetCreate(bool visible) {
            if (!visible) {
                if (make != null) make.Visible = false;
                return;
            }

            if (make == null) {
                var layout = new RoClassicWindowLayout { Class = "UINewMakeCharWnd", WindowId = 0x116, Width = MakeW, Height = MakeH };
                RoClassicFrames.Bitmap(layout.Ops, Make + "bg_makeBg.bmp", 0, 0);
                layout.Children.Add(MakeButton(MakeCloseId, "close", 768, 9, 17, 16, "bt_close_normal.bmp", "bt_close_over.bmp", "bt_close_press.bmp"));
                // the text buttons' labels (CSV 0xd17 / 0xd11, height 14, #313131) are their own ops, over their bitmaps
                layout.Fonts["f14"] = new RoClassicFontSpec("Arial", 17, 400, 0, 3);
                layout.Children.Add(Labelled(MakeButton(MakeReturnId, "return", 25, 379, 152, 27, "bt_make_normal.bmp", "bt_make_over.bmp", "bt_make_press.bmp"), RoClassicUiHost.MsgStr(0xd17, "Return")));
                layout.Children.Add(Labelled(MakeButton(MakeCreateId, "create", 616, 379, 152, 27, "bt_make_normal.bmp", "bt_make_over.bmp", "bt_make_press.bmp"), RoClassicUiHost.MsgStr(0xd11, "Create")));
                layout.Children.Add(MakeButton(RotateLeftId, "left", 426, 179, 22, 23, "bt_leftTurn_normal.bmp", "bt_leftTurn_over.bmp", "bt_leftTurn_press.bmp"));
                layout.Children.Add(MakeButton(RotateRightId, "right", 551, 179, 22, 23, "bt_rightTurn_normal.bmp", "bt_rightTurn_over.bmp", "bt_rightTurn_press.bmp"));
                make = new RoClassicWindow(system, layout) { Visible = false, Draggable = false, ClickableAt = _ => true };
                make.DrawDynamic += DrawCreate;
                make.Clicked += OnCreateClicked;
                make.Pressed += (w, local) => CreatePressed(local);
                makeName = new RoClassicEditField { Rect = new RectInt(435, 268, 130, 20), MaxBytes = 23 };
                makeName.Submitted += _ => Create();
                make.AddEditField(makeName);
            }

            Vector2Int client = system.ClientSize;
            make.MoveTo((client.x - MakeW) / 2, (client.y - MakeH) / 2 - 70);
            if (!make.Visible) {
                make.Visible = true;
                system.Windows.BringToFront(make);
                makeName.Focus();
            }

            make.Invalidate();
        }

        private void OnCreateClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case MakeCreateId:
                    Create();
                    break;
                case MakeCloseId:
                case MakeReturnId:
                    // case 0xb5: mode 0x2714, back to the selection
                    CloseCreate();
                    break;
                case RotateLeftId:
                    makeRotation = (makeRotation + 7) % 8;
                    previewVersion++;
                    break;
                case RotateRightId:
                    makeRotation = (makeRotation + 1) % 8;
                    previewVersion++;
                    break;
            }

            make.Invalidate();
        }

        private void CreatePressed(Vector2Int local) {
            if (new RectInt(437, 59, 63, 25).Contains(local)) makeMale = true;
            else if (new RectInt(500, 59, 62, 25).Contains(local)) makeMale = false;
            else if (new RectInt(RaceX, RaceY[RaceHuman], RaceW, RaceH).Contains(local)) SelectRace(RaceHuman);
            else if (new RectInt(RaceX, RaceY[RaceDoram], RaceW, RaceH).Contains(local)) SelectRace(RaceDoram);
            else {
                // msg 0x208: a style past the race's count is refused
                for (int i = 0; i < RaceStyles[makeRace]; i++) {
                    if (new RectInt(606 + 38 * (i % 4), 59 + 37 * (i / 4), 36, 37).Contains(local)) makeStyle = i + 1;
                }

                for (int i = 0; i < 9; i++) {
                    if (new RectInt(619 + 27 * (i % 5), 306 + 24 * (i / 5), 22, 22).Contains(local)) makeColor = i;
                }
            }

            previewVersion++;
            make.Invalidate();
        }

        /// <summary>Msg 200: the race control (Doram only while open); the style goes back to 1, the start job to 0 / 0x107a.</summary>
        private void SelectRace(int race) {
            if (race == RaceDoram && !DoramOpen) return;
            if (race == makeRace) return;
            makeRace = race;
            makeStyle = 1;
            raceFrame = 0;
        }

        /// <summary>Create 0xb4: the name checks, then mode 0x2715 → CZ_MAKE_CHAR (the race's novice).</summary>
        private void Create() {
            string name = makeName.Text.Trim();
            if (name.Length < 4) {
                RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x15, "Character Name must be at least 4 characters long."), RoClassicMessageBox.TypeOk, true, _ => makeName.Focus());
                return;
            }

            if (name.Length > 23) {
                RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x213, "The name is too long."), RoClassicMessageBox.TypeOk, true, _ => makeName.Focus());
                return;
            }

            shell.CharacterName = name;
            shell.CharacterSlot = createSlot;
            shell.HairColor = (ushort) makeColor;
            shell.HairStyle = (ushort) makeStyle;
            shell.StartJob = makeRace == RaceDoram ? DoramJob : 0;
            shell.CreateSex = (byte) (makeMale ? 1 : 0);
            createSent = true;
            shell.CreateCharacterFromPhase11Ui();
        }

        private void DrawCreate(RoClassicWindow w) {
            var dark = new Color32(0x5f, 0x5f, 0x5f, 255);
            var label = new Color32(64, 64, 64, 255);
            w.TextOut(44, 17, RoClassicUiHost.MsgStr(0xd1b, "Character Creation"), 0, 12, dark);
            DrawRace(w, RaceHuman);
            DrawRace(w, RaceDoram);
            w.DrawBitmap(437, 59, Make + (makeMale ? "bt_male_on.bmp" : "bt_male_off.bmp"));
            w.DrawBitmap(500, 59, Make + (makeMale ? "bt_female_off.bmp" : "bt_female_on.bmp"));
            w.TextOut(609, 46, RoClassicUiHost.MsgStr(0xd12, "Hair Style"), 0, 11, label);
            string[] heads = HairHeads[makeRace * 2 + (makeMale ? 1 : 0)];
            for (int i = 0; i < 24; i++) {
                int x = 606 + 38 * (i % 4), y = 59 + 37 * (i / 4);
                w.DrawBitmap(x, y, Make + (i + 1 == makeStyle ? "bt_hairStyle_select.bmp" : "bt_hairStyle_normal.bmp"));
                // UIHairBitmap: the style's head over the button; none past the race's styles
                if (i < heads.Length) w.DrawBitmap(x, y, heads[i]);
            }

            w.TextOut(609, 287, RoClassicUiHost.MsgStr(0xd13, "Hair Color"), 0, 11, label);
            for (int i = 0; i < 9; i++) {
                w.DrawBitmap(619 + 27 * (i % 5), 306 + 24 * (i / 5), i == makeColor ? ColorOn[i] : ColorOff[i]);
            }

            string key = Preview();
            // the preview stands on the backdrop's pedestal
            if (key != null) w.DrawBitmap(499 - PreviewW / 2, PreviewFeetAtY - PreviewFeetY, key);
        }

        /// <summary>
        /// UIDescriptRace: its on / off bitmap, then (FUN_00801b50) the race name CSV 0xbc8 / 0xbca at (50, 23) in #4C6399 under
        /// (49, 22) in white, the description CSV 0xbcc / 0xbcd wrapped at 40 bytes from (112, 22) every 20 px in black, and the
        /// novice with his feet at (60, 140).
        /// </summary>
        private void DrawRace(RoClassicWindow w, int race) {
            int x = RaceX, y = RaceY[race];
            bool selected = makeRace == race;
            w.DrawBitmap(x, y, selected ? RaceOn[race] : RaceOff[race]);
            if (race == RaceDoram && !DoramOpen) return;
            string name = race == RaceHuman ? RoClassicUiHost.MsgStr(0xbc8, "Human") : RoClassicUiHost.MsgStr(0xbca, "Doram");
            w.TextOut(x + 50, y + 23, name, 0, 12, new Color32(0x4c, 0x63, 0x99, 255));
            w.TextOut(x + 49, y + 22, name, 0, 12, new Color32(255, 255, 255, 255));
            string description = race == RaceHuman
                ? RoClassicUiHost.MsgStr(0xbcc, "Dominant race of Rune Midgard. Unlimited potential. Excellent adaptability to solve problems.")
                : RoClassicUiHost.MsgStr(0xbcd, "Race of Pasta continent with natural born curiosity and sparkle character.");
            List<string> lines = raceLines[race];
            if (!ReferenceEquals(raceDescription[race], description)) {
                raceDescription[race] = description;
                RoClassicText.ByteWrap(description, 40, wrapScratch);
                lines.Clear();
                foreach ((int start, int end) in wrapScratch) lines.Add(description.Substring(start, end - start));
            }

            for (int i = 0; i < lines.Count; i++) w.TextOut(x + 112, y + 22 + 20 * i, lines[i], 0, 12, new Color32(0, 0, 0, 255));

            string[] frames = RaceFrames(race, selected);
            if (frames != null) w.DrawBitmap(x + 60 - PreviewW / 2, y + 140 - PreviewFeetY, frames[selected ? raceFrame % frames.Length : 0]);
        }

        /// <summary>The race control's novice for the current sex, rendered once per race × sex × motion; null until ready.</summary>
        private string[] RaceFrames(int race, bool walk) {
            int index = race * 4 + (makeMale ? 2 : 0) + (walk ? 1 : 0);
            if (raceFrameCounts[index] > 0) return raceFrameKeys[index];
            if (raceFrameCounts[index] != 0) return null;
            if (portraitAssets == null && (shell == null || !shell.TryGetBrowserContentAssets(out portraitAssets))) return null;
            var request = new RoActorAppearanceRequest {
                kind = RoActorAppearanceKind.Player,
                classId = race == RaceDoram ? DoramJob : 0,
                isMale = makeMale,
                headId = 1,
                hairId = 1,
                hairColorId = 0,
                motionProfile = RoSpriteMotionProfile.Player,
                motion = walk ? RoSpriteMotion.Walk : RoSpriteMotion.Idle,
                facingDirection = RoActorDirection.SouthWest,
                headFacing = RoActorHeadFacing.Center,
            };
            string key = "@charmake-race-" + index.ToString(CultureInfo.InvariantCulture);
            raceFrameCounts[index] = -1;
            bool started = RoClassicActorPortrait.RenderFrames(portraitAssets, request, key, PreviewW, PreviewH, PreviewFeetY, count => {
                if (count > 0) {
                    var keys = new string[count];
                    for (int f = 0; f < count; f++) keys[f] = RoClassicActorPortrait.FrameKey(key, f);
                    raceFrameKeys[index] = keys;
                }

                raceFrameCounts[index] = count > 0 ? count : -2;
                make?.Invalidate();
            });
            if (!started) raceFrameCounts[index] = -2;
            return null;
        }

        /// <summary>Advances the selected race's walk while the creation window is up (the original's 60 ms frame step).</summary>
        private void CreateTick() {
            if (make == null || !make.Visible) return;
            int index = makeRace * 4 + (makeMale ? 2 : 0) + 1;
            if (raceFrameCounts[index] <= 1) return;
            float now = Time.unscaledTime;
            if (now < nextRaceFrame) return;
            nextRaceFrame = now + RaceFrameSeconds;
            raceFrame = (raceFrame + 1) % raceFrameCounts[index];
            make.Invalidate();
        }

        /// <summary>The preview of the current choice, rendered again when it changes (the last finished one stays meanwhile).</summary>
        private string Preview() {
            if (previewShown == previewVersion) return previewKey;
            if (portraitAssets == null && (shell == null || !shell.TryGetBrowserContentAssets(out portraitAssets))) return previewKey;
            int version = previewVersion;
            string key = "@charmake-" + (version & 1).ToString(CultureInfo.InvariantCulture);
            var request = new RoActorAppearanceRequest {
                kind = RoActorAppearanceKind.Player,
                classId = makeRace == RaceDoram ? DoramJob : 0,
                isMale = makeMale,
                headId = makeStyle,
                hairId = makeStyle,
                hairColorId = makeColor,
                motionProfile = RoSpriteMotionProfile.Player,
                motion = RoSpriteMotion.Idle,
                facingDirection = (RoActorDirection) makeRotation,
                headFacing = RoActorHeadFacing.Center,
            };
            previewShown = version;
            RoClassicActorPortrait.Render(portraitAssets, request, key, PreviewW, PreviewH, PreviewFeetY, ok => {
                if (ok && version == previewVersion) previewKey = key;
                make?.Invalidate();
            });
            return previewKey;
        }

        private static RoJobMetadataTable jobTable;

        private string JobNameOf(int job) {
            if (jobTable == null && portraitAssets != null) jobTable = RoJobMetadataTable.Load(portraitAssets);
            if (jobTable != null && jobTable.TryGetJob(job, out RoJobMetadata meta) && meta != null) {
                string formatted = RoJobMetadata.FormatJobName(meta.ConstantName);
                if (!string.IsNullOrEmpty(formatted)) return formatted;
            }

            string name = RoJobMetadataTable.DefaultName(job);
            return string.IsNullOrEmpty(name) ? job.ToString(CultureInfo.InvariantCulture) : name;
        }
    }
}
