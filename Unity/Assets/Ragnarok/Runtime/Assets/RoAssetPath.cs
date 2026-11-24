using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ragnarok.Assets {
    public static class RoAssetPath {
        public static string Normalize(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Asset path is required.", nameof(path));
            }

            path = path.Trim().Replace('\\', '/');
            path = path.TrimStart('/');
            while (path.StartsWith("./", StringComparison.Ordinal)) {
                path = path.Substring(2);
            }

            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Asset path is required.", nameof(path));
            }

            while (path.Contains("//")) {
                path = path.Replace("//", "/");
            }

            return LowerAscii(path);
        }

        public static IReadOnlyList<string> GetLookupCandidates(string path) {
            var candidates = new List<string>();
            string normalized = Normalize(path);
            AddCandidate(candidates, normalized);

            string decodedKorean = TryDecodeLegacyBytesAsKorean(normalized);
            if (!string.IsNullOrEmpty(decodedKorean)) {
                AddCandidate(candidates, Normalize(decodedKorean));
            }

            string legacyKorean = TryEncodeKoreanAsLegacyBytes(normalized);
            if (!string.IsNullOrEmpty(legacyKorean)) {
                AddCandidate(candidates, Normalize(legacyKorean));
            }

            return candidates;
        }

        public static string TryDecodeLegacyBytesAsKorean(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return null;
            }

            bool hasLegacyByte = false;
            var bytes = new byte[path.Length];
            for (int i = 0; i < path.Length; i++) {
                char character = path[i];
                if (character > byte.MaxValue) {
                    return null;
                }

                if (character > 127) {
                    hasLegacyByte = true;
                }

                bytes[i] = (byte) character;
            }

            if (!hasLegacyByte) {
                return null;
            }

            try {
                return Encoding.GetEncoding(949).GetString(bytes);
            } catch (Exception) {
                return null;
            }
        }

        public static string BuildLookupReport(string path) {
            var builder = new StringBuilder();
            builder.AppendLine("RO asset path lookup");
            builder.AppendLine("Input: " + path);

            string decoded = TryDecodeLegacyBytesAsKorean(path);
            if (!string.IsNullOrEmpty(decoded) && !string.Equals(decoded, path, StringComparison.Ordinal)) {
                builder.AppendLine("CP949 display: " + decoded);
            }

            IReadOnlyList<string> candidates = GetLookupCandidates(path);
            builder.AppendLine("Candidates:");
            for (int i = 0; i < candidates.Count; i++) {
                builder.Append("- ");
                builder.Append(i);
                builder.Append(": ");
                builder.AppendLine(candidates[i]);
            }

            return builder.ToString().TrimEnd();
        }

        public static string NormalizeRoot(string rootPath) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Content root path is required.", nameof(rootPath));
            }

            rootPath = rootPath.Trim().Replace('\\', '/');
            return rootPath.EndsWith("/", StringComparison.Ordinal) ? rootPath : rootPath + "/";
        }

        public static string ResolveContentFile(string rootPath, string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            filePath = filePath.Trim();
            if (Path.IsPathRooted(filePath)) {
                return Path.GetFullPath(filePath);
            }

            return Path.GetFullPath(Path.Combine(rootPath, filePath));
        }

        private static void AddCandidate(List<string> candidates, string candidate) {
            if (!string.IsNullOrWhiteSpace(candidate) && !candidates.Contains(candidate)) {
                candidates.Add(candidate);
            }
        }

        private static string LowerAscii(string path) {
            char[] chars = null;
            for (int i = 0; i < path.Length; i++) {
                char character = path[i];
                if (character < 128) {
                    char lower = char.ToLowerInvariant(character);
                    if (lower != character) {
                        chars ??= path.ToCharArray();
                        chars[i] = lower;
                    }
                }
            }

            return chars == null ? path : new string(chars);
        }

        private static string TryEncodeKoreanAsLegacyBytes(string path) {
            bool hasWideCharacter = false;
            for (int i = 0; i < path.Length; i++) {
                if (path[i] > byte.MaxValue) {
                    hasWideCharacter = true;
                    break;
                }
            }

            if (!hasWideCharacter) {
                return null;
            }

            try {
                Encoding encoding = Encoding.GetEncoding(949);
                byte[] bytes = encoding.GetBytes(path);
                char[] chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++) {
                    chars[i] = (char) bytes[i];
                }

                return new string(chars);
            } catch (Exception) {
                return null;
            }
        }
    }
}
