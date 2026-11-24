using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The chat's side of the cash emotion windows (docs/audits/chat-windows-2026-10-04.md §8): the chat edit's text set by
    /// CCashEmotionAction_Register / the mini window, and the edit's changes feeding 0x575f70 (the mini window, chat +0x11c).
    /// </summary>
    public sealed partial class RoClassicChat {
        private bool classicEmotionMiniHooked;

        /// <summary>The chat edit (+0x9c)'s SetText (vf 0xd4) and its redraw: the Emoji List's click, the mini window's choice.</summary>
        public bool SetClassicMessageText(string text) {
            if (closed) return instance != null && instance.SetClassicMessageText(text);
            if (classicMessageEdit == null) return false;
            classicMessageEdit.SetText(text ?? string.Empty);
            classicMessageEdit.MoveCaret(int.MaxValue, false);
            classicChat?.Invalidate();
            return true;
        }

        /// <summary>Once the chat edit exists: its every change goes to 0x575f70 (CLanguage's edits call it).</summary>
        private void HookClassicEmotionMini() {
            if (classicEmotionMiniHooked || classicMessageEdit == null) return;
            classicEmotionMiniHooked = true;
            classicMessageEdit.Changed += field => {
                if (classicChat == null) return;
                RectInt r = field.Rect;
                RoClassicEmojiList.OnChatText(field.Text, new RectInt(classicChat.Position.x + r.x, classicChat.Position.y + r.y, r.width, r.height));
            };
        }

        /// <summary>Esc in chat mode: a shown mini window hides first (true when it did).</summary>
        private static bool HideClassicEmotionMini() {
            if (!RoClassicEmojiList.MiniShown) return false;
            RoClassicEmojiList.HideMiniWindow();
            return true;
        }
    }
}
