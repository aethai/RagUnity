using System;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Effects {
    public sealed class RoEffectAssetDiagnostic {
        public RoEffectAssetDiagnostic(
            RoEffectAssetDiagnosticKind kind,
            string requestedPath,
            string assetType,
            string resolvedPath = null,
            string context = null,
            IReadOnlyList<string> candidatePaths = null
        ) {
            Kind = kind;
            RequestedPath = requestedPath ?? string.Empty;
            AssetType = assetType ?? string.Empty;
            ResolvedPath = resolvedPath ?? string.Empty;
            Context = context ?? string.Empty;
            CandidatePaths = CopyCandidates(candidatePaths);
        }

        public RoEffectAssetDiagnosticKind Kind { get; }
        public string RequestedPath { get; }
        public string AssetType { get; }
        public string ResolvedPath { get; }
        public string Context { get; }
        public IReadOnlyList<string> CandidatePaths { get; }

        public bool IsResolved => Kind == RoEffectAssetDiagnosticKind.StrFound ||
            Kind == RoEffectAssetDiagnosticKind.TextureFound ||
            Kind == RoEffectAssetDiagnosticKind.AudioRequested ||
            Kind == RoEffectAssetDiagnosticKind.PrimitiveAdapterUsed;

        public bool IsWarning => Kind == RoEffectAssetDiagnosticKind.AssetDatabaseMissing ||
            Kind == RoEffectAssetDiagnosticKind.StrMissing ||
            Kind == RoEffectAssetDiagnosticKind.StrLoadFailed ||
            Kind == RoEffectAssetDiagnosticKind.TextureMissing ||
            Kind == RoEffectAssetDiagnosticKind.TextureLoadFailed ||
            Kind == RoEffectAssetDiagnosticKind.EffectDefinitionMissing;

        public string BuildMessage() {
            var builder = new StringBuilder();
            builder.Append("RO effect asset ");
            builder.Append(Kind);
            if (!string.IsNullOrWhiteSpace(AssetType)) {
                builder.Append(" [");
                builder.Append(AssetType);
                builder.Append("]");
            }

            if (!string.IsNullOrWhiteSpace(RequestedPath)) {
                builder.Append(" requested='");
                builder.Append(RequestedPath);
                builder.Append("'");
            }

            if (!string.IsNullOrWhiteSpace(ResolvedPath)) {
                builder.Append(" resolved='");
                builder.Append(ResolvedPath);
                builder.Append("'");
            }

            if (!string.IsNullOrWhiteSpace(Context)) {
                builder.Append(" context='");
                builder.Append(Context);
                builder.Append("'");
            }

            if (CandidatePaths.Count > 0) {
                builder.Append(" candidates=[");
                builder.Append(string.Join(", ", CandidatePaths));
                builder.Append("]");
            }

            if (Kind == RoEffectAssetDiagnosticKind.TextureMissing ||
                Kind == RoEffectAssetDiagnosticKind.TextureLoadFailed ||
                (Kind == RoEffectAssetDiagnosticKind.AssetDatabaseMissing &&
                    string.Equals(AssetType, "Texture2D", StringComparison.Ordinal))) {
                builder.Append(" fallback=transparent");
            }

            return builder.ToString();
        }

        private static IReadOnlyList<string> CopyCandidates(IReadOnlyList<string> candidates) {
            if (candidates == null || candidates.Count == 0) {
                return Array.Empty<string>();
            }

            var copy = new List<string>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++) {
                string candidate = candidates[i];
                if (!string.IsNullOrWhiteSpace(candidate) && !copy.Contains(candidate)) {
                    copy.Add(candidate);
                }
            }

            return copy;
        }
    }
}
