using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The geometry and rules of the "Emoji List" (UICashEmotionListWnd 0x57, docs/audits/chat-windows-2026-10-04.md §4.2,
    /// §8) as pure functions: where its panes put the emotions, how far they scroll, what the search finds and which
    /// controls are on.
    /// </summary>
    public static class RoEmojiListRules {
        public const int Basic = 1, Bought = 2, Unbought = 3, Search = 4;   // the tabs' keys (+0xa8 map, msg 0xd)
        public const int PaneWidth = 271, PaneHeight = 303;
        public const int PieceSize = 40, PieceStep = 47, PiecesPerRow = 5;
        public const int SearchRowWidth = 243;
        public const int BarWidth = 11, BarArrow = 13;
        public const int WheelStep = 15;            // Abstract vf35 0x720070: msg 7 ±15
        public const int ArrowStep = 0x87;          // UICashEmotionScrollBar +0xac / +0xb0 (0x7284c0)
        public const int TrackStep = 1;             // msg 9 / 10 (UIScrollWnd::vf37 0x90f370): ∓1

        /// <summary>The Basic pane's k-th emotion (vf54 0x7206b0): 40 × 40 at (15 + 47·col, 4 + 47·row), five a row.</summary>
        public static RectInt BasicPiece(int index) =>
            new RectInt(15 + PieceStep * (index % PiecesPerRow), 4 + PieceStep * (index / PiecesPerRow), PieceSize, PieceSize);

        /// <summary>A search row's k-th emotion (0x727500): 40 × 40 at (8 + 47·col, 39 + 47·row) inside the row.</summary>
        public static RectInt SearchRowPiece(int index) =>
            new RectInt(8 + PieceStep * (index % PiecesPerRow), 39 + PieceStep * (index / PiecesPerRow), PieceSize, PieceSize);

        /// <summary>0x727500's height of a search row: its last piece row's y + 50, or 39 + 10 when it holds nothing.</summary>
        public static int SearchRowHeight(int count) => count < 1 ? 39 + 10 : 39 + PieceStep * ((count - 1) / PiecesPerRow) + 50;

        /// <summary>The lowest bottom of the shown children (UIScrollWnd::vf53 0x90ef10): the content's height.</summary>
        public static int ContentHeight(IReadOnlyList<RectInt> children) {
            int bottom = 0;
            foreach (RectInt r in children) bottom = Math.Max(bottom, r.yMax);
            return bottom;
        }

        /// <summary>Msg 7 / 9 / 10 (0x7200b0, 0x90f370): the offset moved and kept within [0, content − view].</summary>
        public static int Scroll(int offset, int delta, int content, int view) {
            int next = offset + delta;
            if (next < 0) next = 0;
            if (next > content - view) next = Math.Max(0, content - view);
            return next;
        }

        /// <summary>
        /// UIScrollBar vf53 0x7e2630 and 0x7e25d0: the thumb's length ((h − 2·arrow)·visible / total, at least 8) and its
        /// offset past the up arrow (pos·(h − 2·arrow − length) / range).
        /// </summary>
        public static (int Offset, int Length) Thumb(int height, int content, int view, int pos) {
            int track = height - 2 * BarArrow;
            int length = content != 0 ? track * view / content : track;
            if (length < 8) length = 8;
            int range = content - view;
            int offset = range > 0 ? pos * (track - length) / range : 0;
            return (offset, length);
        }

        /// <summary>
        /// 0x576e70: the (pack, emotion)s whose chat word contains <paramref name="text"/> (byte for byte, case-sensitive),
        /// once each, in the order of the word list — the basic pack's, the owned packs', the others'. Nothing for no text.
        /// </summary>
        public static void SearchWords(IReadOnlyList<(string Word, int Pack, int Id)> words, string text, int basicPack, ICollection<int> owned,
            List<(int Pack, int Id)> basic, List<(int Pack, int Id)> bought, List<(int Pack, int Id)> unbought) {
            basic.Clear();
            bought.Clear();
            unbought.Clear();
            if (string.IsNullOrEmpty(text)) return;
            foreach ((string word, int pack, int id) in words) {
                if (word == null || word.IndexOf(text, StringComparison.Ordinal) < 0) continue;
                List<(int, int)> into = pack == basicPack ? basic : owned != null && owned.Contains(pack) ? bought : unbought;
                if (!into.Contains((pack, id))) into.Add((pack, id));
            }
        }

        /// <summary>0x724da0: the search edit, its arrow-back and search buttons work on every tab but Basic.</summary>
        public static bool SearchControlsOn(int tab) => tab != Basic;

        /// <summary>0x724da0: Buy works on the Unbought tab with a pack chosen ([0x11a6124]+0x1c4 &gt; 0).</summary>
        public static bool BuyOn(int tab, int selectedPack) => tab == Unbought && selectedPack > 0;

        /// <summary>The scroll bar hit parts (0x7e20b0): 0 up arrow, 1 track above the thumb, 2 thumb, 3 track below, 4 down arrow.</summary>
        public static int BarPart(int y, int height, int content, int view, int pos) {
            if (y < BarArrow) return 0;
            if (y >= height - BarArrow) return 4;
            (int offset, int length) = Thumb(height, content, view, pos);
            int top = BarArrow + offset;
            if (y < top) return 1;
            return y < top + length ? 2 : 3;
        }
    }
}
