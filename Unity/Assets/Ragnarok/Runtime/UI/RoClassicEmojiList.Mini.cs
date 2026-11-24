using System;
using System.Collections.Generic;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// UICashEmotionMiniWnd, the chat's +0x11c (docs/audits/chat-windows-2026-10-04.md §4.3, §8): 192 × 44 at (the chat
    /// edit's x, its y − 44). 0x575f70 runs on every change of the chat edit: a text "/" + a word the usable word map
    /// (mgr +0x178) knows shows it with that word's emotions (0x89e5a0(1), 0x7257a0), anything else hides it. Three pieces
    /// 42 × 42 at (24 + 49k, 1), btn_arrow_l (0, 0) and bth_arrow_r (W − 19, 0), bg_preview with bg_preview_select under the
    /// chosen one (0x725c80); Left / Right move the choice (0x725830 / 0x725d30 → 0x725d60, the view following), Enter or a
    /// click takes it (0x7256e0: an emotion of the basic or an owned pack puts its word in the chat edit and hides the window;
    /// another opens the Emoji List on that pack).
    /// The usable map is built by 0x575880 only after the owned packs packet (0xc46190 → 0xc3a0f0 / 0xc3a210) — Custom's
    /// server sends none, so on Custom the map stays empty and the window never shows, as in the client.
    /// </summary>
    public sealed partial class RoClassicEmojiList {
        private const int MiniW = 192, MiniH = 44;
        private const int MiniPieces = 3, MiniPieceX = 24, MiniPieceStep = 49, MiniPieceSize = 42, MiniArrowW = 19;
        private const int MiniHitLeft = 1, MiniHitRight = 2, MiniHitPiece = 10;

        private RoClassicWindow mini;
        private string miniWord = string.Empty;
        private readonly List<(int Pack, int Id)> miniList = new List<(int, int)>(4);
        private int miniFirst;          // +0x98
        private int miniSelected = -1;  // +0x9c
        private int miniHover = -1, miniPressed = -1;
        private static readonly List<(int Pack, int Id)> MiniFound = new List<(int, int)>(4);

        /// <summary>
        /// Mgr +0x178 (0x575880): the words the mini window answers to — built only when the owned packs arrive; then a word's
        /// emotions of the basic pack and of the owned packs (I: the use-period checks of the other branch were not read).
        /// </summary>
        public static void UsableTargets(RoCashEmotionData data, string word, List<(int Pack, int Id)> into) {
            into.Clear();
            if (data == null || string.IsNullOrEmpty(word) || !RoEmotionSender.OwnedListReceived) return;
            foreach ((int pack, int id) in data.WordTargets(word)) {
                if (pack == data.BasicPack || RoEmotionSender.OwnedPacks.Contains(pack)) into.Add((pack, id));
            }
        }

        /// <summary>0x575f70 on a change of the chat edit's text (only while the chat and its mini window exist).</summary>
        public static void OnChatText(string text, RectInt editOnScreen) {
            RoClassicEmojiList e = Ensure();
            if (e == null) return;
            if (string.IsNullOrEmpty(text) || text.Length < 2 || text[0] != '/') {
                e.HideMini();
                return;
            }

            string word = text.Substring(1);
            UsableTargets(RoCashEmotionData.Shared, word, MiniFound);
            if (MiniFound.Count == 0) {
                e.HideMini();   // 0x575860
                return;
            }

            e.ShowMini(word, MiniFound, editOnScreen);
        }

        /// <summary>Whether the mini window shows (the chat's Esc and the keys go to it first).</summary>
        public static bool MiniShown => instance?.mini != null && instance.mini.Visible;

        /// <summary>Esc in chat mode, a click elsewhere: the mini window hides (0x89e5a0(0)).</summary>
        public static void HideMiniWindow() => instance?.HideMini();

        /// <summary>Msg 0xb0 with a key: Left 0x25, Right 0x27, Enter 0xd; true when the mini window took it.</summary>
        public static bool MiniKey(int vk) {
            RoClassicEmojiList e = instance;
            if (e?.mini == null || !e.mini.Visible) return false;
            switch (vk) {
                case 0x25:
                    if (e.miniSelected > 0) e.SelectMini(e.miniSelected - 1);   // 0x725830
                    return true;
                case 0x27:
                    if (e.miniSelected + 1 < e.miniList.Count) e.SelectMini(e.miniSelected + 1);   // 0x725d30
                    return true;
                case 0x0d:
                    e.TakeMini(e.miniSelected);
                    return true;
            }

            return false;
        }

        private void ShowMini(string word, List<(int Pack, int Id)> found, RectInt editOnScreen) {
            if (mini == null) BuildMini();
            miniWord = word;
            miniList.Clear();
            miniList.AddRange(found);
            // 0x7257a0: the list, the view and the choice at the first
            miniFirst = 0;
            miniSelected = 0;
            mini.MoveTo(editOnScreen.x, editOnScreen.y - MiniH);
            if (!mini.Visible) {
                mini.Visible = true;
                system.Windows.BringToFront(mini);
            }

            mini.Invalidate();
        }

        private void HideMini() {
            if (mini == null || !mini.Visible) return;
            mini.Visible = false;
            miniPressed = -1;
        }

        /// <summary>0x725d60: the choice, the view keeping it among its three pieces.</summary>
        private void SelectMini(int index) {
            if (index == miniSelected) return;
            miniSelected = index;
            if (index >= miniFirst + MiniPieces) miniFirst = index - (MiniPieces - 1);
            else if (index < miniFirst) miniFirst = index;
            mini.Invalidate();
        }

        /// <summary>0x7256e0: a usable emotion puts the word in the chat edit and hides the window; another opens the Emoji List.</summary>
        private void TakeMini(int index) {
            if (index < 0 || index >= miniList.Count) return;
            (int pack, int _) = miniList[index];
            RoCashEmotionData data = RoCashEmotionData.Shared;
            if (data != null && (pack == data.BasicPack || RoEmotionSender.OwnedPacks.Contains(pack))) {
                RoClassicChat.Instance?.SetClassicMessageText("/" + miniWord);
                HideMini();
                return;
            }

            // CCashEmotionAction_SelectedUnPurchasedPack (0x56e7f0): the Emoji List made (its Unbought pack list is empty here)
            if (!IsOpen) Toggle(session);
        }

        private void BuildMini() {
            var layout = new RoClassicWindowLayout { Class = "UICashEmotionMiniWnd", WindowId = RoClassicCloseTabs.DefaultWindowId, Width = MiniW, Height = MiniH };
            mini = new RoClassicWindow(system, layout) { Visible = false, Draggable = false, FrameDragArea = _ => false, ClickableAt = p => MiniHitAt(p) >= 0 };
            mini.DrawDynamic += DrawMini;
            mini.Pressed += (w, p) => {
                miniPressed = MiniHitAt(p);
                w.Invalidate();
            };
            mini.Released += (w, p) => {
                int was = miniPressed;
                miniPressed = -1;
                w.Invalidate();
                if (was < 0 || was != MiniHitAt(p)) return;
                if (was == MiniHitLeft) MiniKey(0x25);
                else if (was == MiniHitRight) MiniKey(0x27);
                else if (was >= MiniHitPiece) TakeMini(miniFirst + was - MiniHitPiece);
            };
        }

        private int MiniHitAt(Vector2Int p) {
            if (p.y < 0 || p.y >= MiniH) return -1;
            if (p.x >= 0 && p.x < MiniArrowW) return MiniHitLeft;
            if (p.x >= MiniW - MiniArrowW && p.x < MiniW) return MiniHitRight;
            for (int k = 0; k < MiniPieces; k++) {
                int x = MiniPieceX + MiniPieceStep * k;
                if (p.x >= x && p.x < x + MiniPieceSize && p.y >= 1 && p.y < 1 + MiniPieceSize && miniFirst + k < miniList.Count) return MiniHitPiece + k;
            }

            return -1;
        }

        private void TickMini(RoClassicUiSystem ui) {
            if (mini == null || !mini.Visible) return;
            int over = ui.Windows.HoverWindow == mini ? MiniHitAt(ui.Windows.Mouse - mini.Position) : -1;
            if (over == miniHover) return;
            miniHover = over;
            mini.Invalidate();
        }

        /// <summary>OnDraw 0x725c80, the arrows and the pieces.</summary>
        private void DrawMini(RoClassicWindow w) {
            w.DrawBitmap(0, 0, Ce + "bg_preview.bmp");
            int shown = miniSelected - miniFirst;
            if (miniSelected != -1 && shown >= 0 && shown < MiniPieces) w.DrawBitmap(MiniPieceX + MiniPieceStep * shown - 3, 0, Ce + "bg_preview_select.bmp");
            w.DrawBitmap(0, 0, Ce + "btn_arrow_l_" + MiniArrowState(MiniHitLeft) + ".bmp");
            w.DrawBitmap(MiniW - MiniArrowW, 0, Ce + "bth_arrow_r_" + MiniArrowState(MiniHitRight) + ".bmp");
            RoCashEmotionData data = RoCashEmotionData.Shared;
            for (int k = 0; k < MiniPieces && miniFirst + k < miniList.Count; k++) {
                (int pack, int id) = miniList[miniFirst + k];
                var rect = new RectInt(MiniPieceX + MiniPieceStep * k, 1, MiniPieceSize, MiniPieceSize);
                if (data == null || !data.TryGet(pack, id, out RoCashEmotionData.Emotion emotion)) continue;
                w.ClipTo(rect.x, rect.y, rect.width, rect.height);
                RoClassicActSprite.Get(SpritePath(emotion.Act))?.DrawButton(w, Math.Max(0, emotion.Action), Math.Max(0, emotion.Motion), rect);
                w.ResetClip();
            }
        }

        private string MiniArrowState(int hit) => miniPressed == hit && miniHover == hit ? "press" : miniHover == hit ? "over" : "normal";
    }
}
