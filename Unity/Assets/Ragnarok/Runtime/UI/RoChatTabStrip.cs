using System;
using System.Collections.Generic;

namespace Ragnarok.UI {
    /// <summary>
    /// The chat window's UITabControl (vtable 0x00f9c4cc, made by FUN_007dddd0 with style 1, height 16, pad 10, equal
    /// widths, rename on) as state: the names it shows (+0xb4), the full names kept for its tooltip (+0xdc) and the tab
    /// widths (+0xcc). AddTab 0x7fd480 cuts every shown name wider than parentW / count − 10 to that width with "..."
    /// (FUN_009b3c60), keeping the uncut name for the tooltip of a tab that is not the one being added; RemoveTab 0x7dbe10
    /// drops the shown name and the width but not the tooltip name (so the tooltip names of later tabs stay where they
    /// were); a rename (vf37 0x7faa00) replaces the shown name only. Every width is the widest shown name + 10
    /// (0x7f80e0), set again by AddTab and a rename, not by RemoveTab. Pure data: the chat draws and hit-tests with it.
    /// </summary>
    public sealed class RoChatTabStrip {
        /// <summary>+0x88: the padding added to a name's width (and taken off the cut limit).</summary>
        public const int Pad = 10;

        /// <summary>The control's height (tab height 16 + 1).</summary>
        public const int Height = 17;

        /// <summary>DAT_00f48a3c, appended to a cut name.</summary>
        public const string Ellipsis = "...";

        private readonly List<string> shown = new List<string>(10);
        private readonly List<string> full = new List<string>(10);
        private readonly List<int> widths = new List<int>(10);

        public int Count => shown.Count;

        /// <summary>The name drawn on tab <paramref name="index"/> (cut with "..." when it did not fit).</summary>
        public string Shown(int index) => index >= 0 && index < shown.Count ? shown[index] : string.Empty;

        /// <summary>Tab <paramref name="index"/>'s width (every tab has the same one).</summary>
        public int TabWidth(int index) => index >= 0 && index < widths.Count ? widths[index] : 0;

        /// <summary>The x of tab <paramref name="index"/> in the control: the widths before it, 1 px overlapping.</summary>
        public int TabX(int index) {
            int x = 0;
            for (int i = 0; i < index && i < widths.Count; i++) x += widths[i] - 1;
            return x;
        }

        /// <summary>0x7f8b90: the control's width, Σ(width − 1) + 1 (0 without tabs).</summary>
        public int ControlWidth => widths.Count == 0 ? 0 : TabX(widths.Count) + 1;

        /// <summary>
        /// vf28 0x7f3060: the text of the hover tooltip over tab <paramref name="index"/> — its kept full name when there is
        /// one and it differs from the shown name, else null.
        /// </summary>
        public string Tooltip(int index) {
            if (index < 0 || index >= shown.Count || index >= full.Count) return null;
            string name = full[index];
            return name.Length != 0 && !string.Equals(name, shown[index], StringComparison.Ordinal) ? name : null;
        }

        /// <summary>
        /// 0x7e2510: the tab under a control-local x (each tab's full width taken off in turn, so a hit drifts 1 px per tab
        /// from the drawn overlap), −1 past the last.
        /// </summary>
        public int HitTest(int x) {
            for (int i = 0; i < widths.Count; i++) {
                x -= widths[i];
                if (x < 0) return i;
            }

            return -1;
        }

        /// <summary>
        /// vf27 0x7f3510 while a tab press holds the mouse: the parent gets msg 0x75 when nothing is kept yet (+0xac and +0xb0
        /// both 0) or the mouse is more than 5 px left of or above the point kept (control-local); the caller keeps the new
        /// point either way.
        /// </summary>
        public static bool PullsOut(int lastX, int lastY, int x, int y) => (lastX == 0 && lastY == 0) || lastX - x > 5 || lastY - y > 5;

        /// <summary>No tabs (a new chat window builds the control again from its tab records).</summary>
        public void Clear() {
            shown.Clear();
            full.Clear();
            widths.Clear();
        }

        /// <summary>
        /// AddTab 0x7fd480 (with an empty tooltip text, as the chat always passes): the name goes in; with the control in a
        /// window of width <paramref name="parentWidth"/> every shown name wider than parentWidth / count − 10 is cut,
        /// a tab other than the new one first keeping its uncut name for the tooltip when it has none yet; then the widths.
        /// </summary>
        public void Add(string name, Func<string, int> measure, int parentWidth) {
            name = name ?? string.Empty;
            shown.Add(name);
            full.Add(string.Empty);
            int count = shown.Count;
            if (parentWidth > 0) {
                int limit = parentWidth / count - Pad;
                for (int i = 0; i < count; i++) {
                    if (Measure(measure, shown[i]) > limit && i != count - 1 && full[i].Length == 0) full[i] = shown[i];
                    shown[i] = Truncate(shown[i], limit, measure);
                }
            }

            widths.Add(Measure(measure, name) + Pad);
            Relayout(measure);
        }

        /// <summary>RemoveTab 0x7dbe10: the shown name and the width go; the tooltip names and the other widths stay.</summary>
        public void Remove(int index) {
            if (index < 0 || index >= shown.Count) return;
            shown.RemoveAt(index);
            widths.RemoveAt(index);
        }

        /// <summary>vf37 0x7faa00 on Enter in the rename edit: the shown name becomes <paramref name="name"/>, the widths follow.</summary>
        public void Rename(int index, string name, Func<string, int> measure) {
            if (index < 0 || index >= shown.Count) return;
            shown[index] = name ?? string.Empty;
            Relayout(measure);
        }

        /// <summary>0x7f80e0 (equal widths): every tab as wide as the widest shown name + 10.</summary>
        private void Relayout(Func<string, int> measure) {
            int widest = 0;
            for (int i = 0; i < shown.Count; i++) widest = Math.Max(widest, Measure(measure, shown[i]) + Pad);
            for (int i = 0; i < widths.Count; i++) widths[i] = widest;
        }

        /// <summary>
        /// FUN_009b3c60: a text that fits <paramref name="limit"/> pixels stays; else characters come off its end, "..."
        /// is put after the rest and measured again, until it fits — a text down to one character keeps it and gets the
        /// "..." whatever its width.
        /// </summary>
        public static string Truncate(string text, int limit, Func<string, int> measure) {
            text = text ?? string.Empty;
            if (Measure(measure, text) <= limit) return text;
            string rest = text;
            while (true) {
                if (rest.Length < 2) return rest + Ellipsis;
                rest = rest.Substring(0, rest.Length - 1);
                string cut = rest + Ellipsis;
                if (Measure(measure, cut) <= limit) return cut;
            }
        }

        private static int Measure(Func<string, int> measure, string text) => measure != null ? measure(text) : text.Length * 6;
    }
}
