using System.Collections.Generic;

namespace Ragnarok.UI.Classic {
    /// <summary>Text helpers shared by classic windows.</summary>
    public static class RoClassicText {
        /// <summary>
        /// FUN_00915a50 (chat lines at 94 bytes, tooltips at 35, race descriptions at 40): steps one character (one byte
        /// under langtype 1), then over a run of spaces, which marks the next word's start; when that position reaches line
        /// start + <paramref name="limit"/>, or at '\n', the line ends at the word start (keeping its trailing spaces), or
        /// right there when the word began the line. A line that ended at '\n' starts with it in the original; the '\n' is
        /// left out of the returned range but still counts towards the next line's limit.
        /// </summary>
        public static void ByteWrap(string s, int limit, List<(int Start, int End)> lines) {
            lines.Clear();
            int n = s.Length, lineStart = 0, lineEnd = limit, wordStart = 0, pos = 0, last = 0;
            while (pos < n) {
                pos++;
                while (pos < n && s[pos] == ' ') wordStart = ++pos;
                bool newline = pos < n && s[pos] == '\n';
                if (!newline && pos < lineEnd) continue;
                if (newline) wordStart = pos++;
                int end = wordStart <= lineStart ? pos : wordStart;
                Add(s, lineStart, end, lines);
                last = lineStart = end;
                lineEnd = end + limit;
            }

            if (last < pos) Add(s, last, pos, lines);
        }

        private static void Add(string s, int start, int end, List<(int Start, int End)> lines) {
            if (start < end && s[start] == '\n') start++;
            lines.Add((start, end));
        }
    }
}
