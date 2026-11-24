using System;
using System.Globalization;
using System.Text;
using Ragnarok.Assets;

namespace Ragnarok.Localization {
    [Flags]
    public enum RoTextScriptCoverage {
        None = 0,
        Latin = 1 << 0,
        Hangul = 1 << 1,
        JapaneseKana = 1 << 2,
        CjkUnified = 1 << 3,
        Digits = 1 << 4,
        PunctuationOrSymbol = 1 << 5,
        Whitespace = 1 << 6,
        ReplacementCharacter = 1 << 7,
        Control = 1 << 8,
        Other = 1 << 9
    }

    public readonly struct RoTextDiagnosticReport {
        public RoTextDiagnosticReport(
            string text,
            RoTextScriptCoverage coverage,
            int codePointCount,
            int replacementCharacterCount,
            int controlCharacterCount,
            bool containsLikelyMojibake
        ) {
            Text = text ?? string.Empty;
            Coverage = coverage;
            CodePointCount = Math.Max(0, codePointCount);
            ReplacementCharacterCount = Math.Max(0, replacementCharacterCount);
            ControlCharacterCount = Math.Max(0, controlCharacterCount);
            ContainsLikelyMojibake = containsLikelyMojibake;
        }

        public string Text { get; }
        public RoTextScriptCoverage Coverage { get; }
        public int CodePointCount { get; }
        public int ReplacementCharacterCount { get; }
        public int ControlCharacterCount { get; }
        public bool ContainsLikelyMojibake { get; }
        public bool HasReplacementCharacters => ReplacementCharacterCount > 0;
        public bool HasProblematicControlCharacters => ControlCharacterCount > 0;
        public bool RequiresKoreanFallback => (Coverage & RoTextScriptCoverage.Hangul) != 0;
        public bool RequiresEastAsianFallback => (Coverage & (RoTextScriptCoverage.Hangul | RoTextScriptCoverage.JapaneseKana | RoTextScriptCoverage.CjkUnified)) != 0;
        public bool IsClean => !ContainsLikelyMojibake && !HasReplacementCharacters && !HasProblematicControlCharacters;

        public string BuildSummary() {
            var builder = new StringBuilder();
            builder.Append("text codepoints=");
            builder.Append(CodePointCount);
            builder.Append(", coverage=");
            builder.Append(Coverage);

            if (ContainsLikelyMojibake) {
                builder.Append(", mojibake");
            }

            if (ReplacementCharacterCount > 0) {
                builder.Append(", replacement=");
                builder.Append(ReplacementCharacterCount);
            }

            if (ControlCharacterCount > 0) {
                builder.Append(", control=");
                builder.Append(ControlCharacterCount);
            }

            return builder.ToString();
        }
    }

    public static class RoTextDiagnostics {
        public static RoTextDiagnosticReport Analyze(string text) {
            if (string.IsNullOrEmpty(text)) {
                return new RoTextDiagnosticReport(string.Empty, RoTextScriptCoverage.None, 0, 0, 0, false);
            }

            RoTextScriptCoverage coverage = RoTextScriptCoverage.None;
            int codePointCount = 0;
            int replacementCharacterCount = 0;
            int controlCharacterCount = 0;

            for (int i = 0; i < text.Length; i++) {
                int codePoint = ReadCodePoint(text, ref i);

                codePointCount++;
                RoTextScriptCoverage category = Classify(codePoint);
                coverage |= category;

                if ((category & RoTextScriptCoverage.ReplacementCharacter) != 0) {
                    replacementCharacterCount++;
                }

                if ((category & RoTextScriptCoverage.Control) != 0) {
                    controlCharacterCount++;
                }
            }

            return new RoTextDiagnosticReport(
                text,
                coverage,
                codePointCount,
                replacementCharacterCount,
                controlCharacterCount,
                RoTextEncoding.ContainsLikelyMojibake(text)
            );
        }

        private static RoTextScriptCoverage Classify(int codePoint) {
            if (codePoint == 0xFFFD) {
                return RoTextScriptCoverage.ReplacementCharacter;
            }

            if (IsPermittedWhitespace(codePoint)) {
                return RoTextScriptCoverage.Whitespace;
            }

            if (codePoint <= char.MaxValue && char.IsControl((char) codePoint)) {
                return RoTextScriptCoverage.Control;
            }

            if (codePoint >= '0' && codePoint <= '9') {
                return RoTextScriptCoverage.Digits;
            }

            if (IsHangul(codePoint)) {
                return RoTextScriptCoverage.Hangul;
            }

            if (IsJapaneseKana(codePoint)) {
                return RoTextScriptCoverage.JapaneseKana;
            }

            if (IsCjkUnified(codePoint)) {
                return RoTextScriptCoverage.CjkUnified;
            }

            if (IsLatin(codePoint)) {
                return RoTextScriptCoverage.Latin;
            }

            UnicodeCategory category = codePoint <= char.MaxValue
                ? char.GetUnicodeCategory((char) codePoint)
                : CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0);
            if (category == UnicodeCategory.ConnectorPunctuation ||
                category == UnicodeCategory.DashPunctuation ||
                category == UnicodeCategory.OpenPunctuation ||
                category == UnicodeCategory.ClosePunctuation ||
                category == UnicodeCategory.InitialQuotePunctuation ||
                category == UnicodeCategory.FinalQuotePunctuation ||
                category == UnicodeCategory.OtherPunctuation ||
                category == UnicodeCategory.MathSymbol ||
                category == UnicodeCategory.CurrencySymbol ||
                category == UnicodeCategory.ModifierSymbol ||
                category == UnicodeCategory.OtherSymbol) {
                return RoTextScriptCoverage.PunctuationOrSymbol;
            }

            return RoTextScriptCoverage.Other;
        }

        private static int ReadCodePoint(string text, ref int index) {
            char current = text[index];
            if (char.IsHighSurrogate(current) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])) {
                index++;
                return char.ConvertToUtf32(current, text[index]);
            }

            return current;
        }

        private static bool IsPermittedWhitespace(int codePoint) {
            return codePoint == ' ' || codePoint == '\t' || codePoint == '\n' || codePoint == '\r';
        }

        private static bool IsLatin(int codePoint) {
            return (codePoint >= 0x0041 && codePoint <= 0x005A) ||
                (codePoint >= 0x0061 && codePoint <= 0x007A) ||
                (codePoint >= 0x00C0 && codePoint <= 0x024F);
        }

        private static bool IsHangul(int codePoint) {
            return (codePoint >= 0x1100 && codePoint <= 0x11FF) ||
                (codePoint >= 0x3130 && codePoint <= 0x318F) ||
                (codePoint >= 0xAC00 && codePoint <= 0xD7AF);
        }

        private static bool IsJapaneseKana(int codePoint) {
            return (codePoint >= 0x3040 && codePoint <= 0x309F) ||
                (codePoint >= 0x30A0 && codePoint <= 0x30FF);
        }

        private static bool IsCjkUnified(int codePoint) {
            return (codePoint >= 0x3400 && codePoint <= 0x4DBF) ||
                (codePoint >= 0x4E00 && codePoint <= 0x9FFF) ||
                (codePoint >= 0xF900 && codePoint <= 0xFAFF);
        }
    }
}
