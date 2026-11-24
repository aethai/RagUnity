using System;
using System.Collections.Generic;
using System.IO;

namespace Ragnarok.Assets {
    public sealed class RoResnametableAliasResolver {
        public static readonly RoResnametableAliasResolver Empty =
            new RoResnametableAliasResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        private readonly Dictionary<string, string> aliases;

        public RoResnametableAliasResolver(IReadOnlyDictionary<string, string> aliases) {
            this.aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (aliases == null) {
                return;
            }

            foreach (KeyValuePair<string, string> pair in aliases) {
                AddAlias(pair.Key, pair.Value);
            }
        }

        public int Count => aliases.Count;

        public static RoResnametableAliasResolver Parse(string text) {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) {
                return new RoResnametableAliasResolver(values);
            }

            using var reader = new StringReader(text);
            while (reader.ReadLine() is string rawLine) {
                string line = rawLine.Trim();
                if (line.Length == 0 ||
                    line.StartsWith("//", StringComparison.Ordinal) ||
                    line.StartsWith("#", StringComparison.Ordinal)) {
                    continue;
                }

                string[] parts = line.Split('#');
                if (parts.Length < 2) {
                    continue;
                }

                string source = NormalizeOrNull(parts[0]);
                string target = NormalizeOrNull(parts[1]);
                if (source == null || target == null) {
                    continue;
                }

                AddAlias(values, source, target);
            }

            return new RoResnametableAliasResolver(values);
        }

        public IReadOnlyList<string> ResolveAliases(IReadOnlyList<string> paths) {
            var resolved = new List<string>();
            if (paths == null) {
                return resolved;
            }

            for (int i = 0; i < paths.Count; i++) {
                AddResolvedAliasVariants(resolved, paths[i]);
            }

            return resolved;
        }

        public bool TryResolve(string path, out string resolvedPath) {
            resolvedPath = null;
            string normalized = NormalizeOrNull(path);
            if (normalized == null) {
                return false;
            }

            return TryResolveNormalized(normalized, out resolvedPath);
        }

        private void AddResolvedAliasVariants(List<string> resolved, string path) {
            string normalized = NormalizeOrNull(path);
            if (normalized == null || !TryResolveNormalized(normalized, out string target)) {
                return;
            }

            AddCandidate(resolved, target);
            AddCandidate(resolved, WithDataPrefix(target));
            AddCandidate(resolved, WithoutDataPrefix(target));

            IReadOnlyList<string> koreanCandidates = RoAssetPath.GetLookupCandidates(target);
            for (int i = 0; i < koreanCandidates.Count; i++) {
                AddCandidate(resolved, koreanCandidates[i]);
            }
        }

        private bool TryResolveNormalized(string normalized, out string resolvedPath) {
            if (aliases.TryGetValue(normalized, out resolvedPath)) {
                return true;
            }

            string withoutData = WithoutDataPrefix(normalized);
            if (!string.Equals(withoutData, normalized, StringComparison.OrdinalIgnoreCase) &&
                aliases.TryGetValue(withoutData, out resolvedPath)) {
                return true;
            }

            string withData = WithDataPrefix(normalized);
            if (!string.Equals(withData, normalized, StringComparison.OrdinalIgnoreCase) &&
                aliases.TryGetValue(withData, out resolvedPath)) {
                return true;
            }

            resolvedPath = null;
            return false;
        }

        private void AddAlias(string source, string target) {
            string normalizedSource = NormalizeOrNull(source);
            string normalizedTarget = NormalizeOrNull(target);
            if (normalizedSource == null || normalizedTarget == null) {
                return;
            }

            AddAlias(aliases, normalizedSource, normalizedTarget);
        }

        private static void AddAlias(Dictionary<string, string> target, string source, string value) {
            AddAliasCore(target, source, value);
            AddAliasCore(target, WithDataPrefix(source), WithDataPrefix(value));
            AddAliasCore(target, WithoutDataPrefix(source), WithoutDataPrefix(value));
        }

        private static void AddAliasCore(Dictionary<string, string> target, string source, string value) {
            if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(value)) {
                target[source] = value;
            }
        }

        private static string NormalizeOrNull(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return null;
            }

            try {
                return RoAssetPath.Normalize(path);
            } catch (ArgumentException) {
                return null;
            }
        }

        private static string WithDataPrefix(string path) {
            string normalized = NormalizeOrNull(path);
            if (normalized == null) {
                return null;
            }

            return normalized.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : "data/" + normalized;
        }

        private static string WithoutDataPrefix(string path) {
            string normalized = NormalizeOrNull(path);
            if (normalized == null) {
                return null;
            }

            return normalized.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(5)
                : normalized;
        }

        private static void AddCandidate(List<string> candidates, string candidate) {
            if (!string.IsNullOrWhiteSpace(candidate) && !candidates.Contains(candidate)) {
                candidates.Add(candidate);
            }
        }
    }
}
