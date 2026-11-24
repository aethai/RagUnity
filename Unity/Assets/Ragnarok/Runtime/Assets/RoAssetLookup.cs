using System;
using System.Collections.Generic;

namespace Ragnarok.Assets {
    public enum RoAssetLookupOutcome {
        NotFound = 0,
        Direct = 1,
        Alias = 2,
        SuffixFallback = 3,
        ContainsFallback = 4,
        AmbiguousFallback = 5
    }

    public sealed class RoAssetLookupReport {
        public RoAssetLookupReport(
            string requestedPath,
            IReadOnlyList<string> directCandidates,
            IReadOnlyList<string> aliasCandidates,
            IReadOnlyList<string> legacyKoreanCandidates,
            IReadOnlyList<string> fallbackCandidates,
            IReadOnlyList<string> fallbackAttempts,
            string resolvedPath,
            RoAssetLookupOutcome outcome) {
            RequestedPath = requestedPath ?? string.Empty;
            DirectCandidates = Copy(directCandidates);
            AliasCandidates = Copy(aliasCandidates);
            LegacyKoreanCandidates = Copy(legacyKoreanCandidates);
            FallbackCandidates = Copy(fallbackCandidates);
            FallbackAttempts = Copy(fallbackAttempts);
            ResolvedPath = resolvedPath;
            Outcome = outcome;

            var all = new List<string>();
            AddRange(all, DirectCandidates);
            AddRange(all, AliasCandidates);
            AddRange(all, FallbackCandidates);
            Candidates = all.ToArray();
        }

        public string RequestedPath { get; }
        public IReadOnlyList<string> Candidates { get; }
        public IReadOnlyList<string> DirectCandidates { get; }
        public IReadOnlyList<string> AliasCandidates { get; }
        public IReadOnlyList<string> LegacyKoreanCandidates { get; }
        public IReadOnlyList<string> FallbackCandidates { get; }
        public IReadOnlyList<string> FallbackAttempts { get; }
        public string ResolvedPath { get; }
        public RoAssetLookupOutcome Outcome { get; }
        public bool IsResolved => !string.IsNullOrWhiteSpace(ResolvedPath);

        private static string[] Copy(IReadOnlyList<string> values) {
            if (values == null || values.Count == 0) {
                return Array.Empty<string>();
            }

            var copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++) {
                copy[i] = values[i];
            }

            return copy;
        }

        private static void AddRange(List<string> target, IReadOnlyList<string> values) {
            if (values == null) {
                return;
            }

            for (int i = 0; i < values.Count; i++) {
                string value = values[i];
                if (!string.IsNullOrWhiteSpace(value) && !target.Contains(value)) {
                    target.Add(value);
                }
            }
        }
    }
}
