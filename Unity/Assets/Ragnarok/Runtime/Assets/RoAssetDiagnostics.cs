using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ragnarok.Assets {
    public sealed class RoAssetDiagnostics {
        private readonly Dictionary<string, MissingAsset> missingAssets = new Dictionary<string, MissingAsset>(StringComparer.OrdinalIgnoreCase);

        public int MissingAssetCount => missingAssets.Count;

        public void Clear() {
            missingAssets.Clear();
        }

        public void RecordMissing(string requestedPath, string expectedType, IReadOnlyList<string> candidates) {
            RecordMissing(
                requestedPath,
                expectedType,
                new RoAssetLookupReport(
                    requestedPath,
                    candidates,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    resolvedPath: null,
                    RoAssetLookupOutcome.NotFound));
        }

        public void RecordMissing(string requestedPath, string expectedType, RoAssetLookupReport lookup) {
            if (string.IsNullOrWhiteSpace(requestedPath)) {
                return;
            }

            string key = RoAssetPath.Normalize(requestedPath);
            if (!missingAssets.TryGetValue(key, out MissingAsset missing)) {
                missing = new MissingAsset(requestedPath, expectedType);
                missingAssets.Add(key, missing);
            }

            missing.Count++;
            missing.ExpectedType = expectedType;
            missing.SetLookup(lookup);
        }

        public IReadOnlyList<RoMissingAssetSnapshot> SnapshotMissingAssets() {
            var snapshot = new List<RoMissingAssetSnapshot>(missingAssets.Count);
            foreach (MissingAsset missing in missingAssets.Values) {
                snapshot.Add(missing.ToSnapshot());
            }

            return snapshot;
        }

        public string BuildMissingSummary(int maxItems = 20) {
            if (missingAssets.Count == 0) {
                return "RO asset diagnostics: no missing assets recorded.";
            }

            var builder = new StringBuilder();
            builder.Append("RO asset diagnostics: ");
            builder.Append(missingAssets.Count);
            builder.AppendLine(" unique missing asset(s).");

            int index = 0;
            foreach (MissingAsset missing in missingAssets.Values) {
                if (index >= maxItems) {
                    builder.Append("... ");
                    builder.Append(missingAssets.Count - maxItems);
                    builder.AppendLine(" more omitted.");
                    break;
                }

                builder.Append("- ");
                builder.Append(missing.RequestedPath);
                builder.Append(" [");
                builder.Append(missing.ExpectedType);
                builder.Append("] x");
                builder.Append(missing.Count);
                builder.Append(" outcome: ");
                builder.Append(missing.Outcome);
                if (missing.Candidates.Count > 0) {
                    builder.Append(" candidates: ");
                    builder.Append(string.Join(", ", missing.Candidates));
                }

                if (missing.AliasCandidates.Count > 0) {
                    builder.Append(" aliases: ");
                    builder.Append(string.Join(", ", missing.AliasCandidates));
                }

                if (missing.LegacyKoreanCandidates.Count > 0) {
                    builder.Append(" cp949: ");
                    builder.Append(string.Join(", ", missing.LegacyKoreanCandidates));
                }

                if (missing.FallbackAttempts.Count > 0) {
                    builder.Append(" fallback: ");
                    builder.Append(string.Join(" | ", missing.FallbackAttempts));
                }

                builder.AppendLine();
                index++;
            }

            return builder.ToString().TrimEnd();
        }

        public void LogMissingSummary(UnityEngine.Object context = null, int maxItems = 20) {
            if (missingAssets.Count == 0) {
                return;
            }

            Debug.LogWarning(BuildMissingSummary(maxItems), context);
        }

        private sealed class MissingAsset {
            public MissingAsset(string requestedPath, string expectedType) {
                RequestedPath = requestedPath;
                ExpectedType = expectedType;
            }

            public string RequestedPath { get; }
            public string ExpectedType { get; set; }
            public int Count { get; set; }
            public List<string> Candidates { get; } = new List<string>();
            public List<string> AliasCandidates { get; } = new List<string>();
            public List<string> LegacyKoreanCandidates { get; } = new List<string>();
            public List<string> FallbackCandidates { get; } = new List<string>();
            public List<string> FallbackAttempts { get; } = new List<string>();
            public RoAssetLookupOutcome Outcome { get; private set; } = RoAssetLookupOutcome.NotFound;

            public void SetLookup(RoAssetLookupReport lookup) {
                Candidates.Clear();
                AliasCandidates.Clear();
                LegacyKoreanCandidates.Clear();
                FallbackCandidates.Clear();
                FallbackAttempts.Clear();
                Outcome = lookup?.Outcome ?? RoAssetLookupOutcome.NotFound;
                if (lookup == null) {
                    return;
                }

                CopyUnique(lookup.Candidates, Candidates);
                CopyUnique(lookup.AliasCandidates, AliasCandidates);
                CopyUnique(lookup.LegacyKoreanCandidates, LegacyKoreanCandidates);
                CopyUnique(lookup.FallbackCandidates, FallbackCandidates);
                CopyUnique(lookup.FallbackAttempts, FallbackAttempts);
            }

            public RoMissingAssetSnapshot ToSnapshot() {
                return new RoMissingAssetSnapshot(
                    RequestedPath,
                    ExpectedType,
                    Count,
                    Candidates.ToArray(),
                    AliasCandidates.ToArray(),
                    LegacyKoreanCandidates.ToArray(),
                    FallbackCandidates.ToArray(),
                    FallbackAttempts.ToArray(),
                    Outcome);
            }

            private static void CopyUnique(IReadOnlyList<string> source, List<string> target) {
                if (source == null) {
                    return;
                }

                for (int i = 0; i < source.Count; i++) {
                    if (!string.IsNullOrWhiteSpace(source[i]) && !target.Contains(source[i])) {
                        target.Add(source[i]);
                    }
                }
            }
        }
    }

    public sealed class RoMissingAssetSnapshot {
        public RoMissingAssetSnapshot(
            string requestedPath,
            string expectedType,
            int count,
            IReadOnlyList<string> candidates,
            IReadOnlyList<string> aliasCandidates,
            IReadOnlyList<string> legacyKoreanCandidates,
            IReadOnlyList<string> fallbackCandidates,
            IReadOnlyList<string> fallbackAttempts,
            RoAssetLookupOutcome outcome) {
            RequestedPath = requestedPath;
            ExpectedType = expectedType;
            Count = count;
            Candidates = candidates ?? Array.Empty<string>();
            AliasCandidates = aliasCandidates ?? Array.Empty<string>();
            LegacyKoreanCandidates = legacyKoreanCandidates ?? Array.Empty<string>();
            FallbackCandidates = fallbackCandidates ?? Array.Empty<string>();
            FallbackAttempts = fallbackAttempts ?? Array.Empty<string>();
            Outcome = outcome;
        }

        public string RequestedPath { get; }
        public string ExpectedType { get; }
        public int Count { get; }
        public IReadOnlyList<string> Candidates { get; }
        public IReadOnlyList<string> AliasCandidates { get; }
        public IReadOnlyList<string> LegacyKoreanCandidates { get; }
        public IReadOnlyList<string> FallbackCandidates { get; }
        public IReadOnlyList<string> FallbackAttempts { get; }
        public RoAssetLookupOutcome Outcome { get; }
    }
}
