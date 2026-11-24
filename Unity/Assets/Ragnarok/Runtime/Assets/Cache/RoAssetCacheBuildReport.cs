using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Assets {
    public sealed class RoAssetCacheBuildReport {
        private readonly List<string> writtenPaths = new List<string>();
        private readonly List<string> failures = new List<string>();

        public int Requested { get; private set; }
        public int Built { get; private set; }
        public int SkippedExisting { get; private set; }
        public int Failed { get; private set; }
        public IReadOnlyList<string> WrittenPaths => writtenPaths;
        public IReadOnlyList<string> Failures => failures;

        public void RecordRequested() {
            Requested++;
        }

        public void RecordBuilt(string path) {
            Built++;
            if (!string.IsNullOrWhiteSpace(path)) {
                writtenPaths.Add(path);
            }
        }

        public void RecordSkippedExisting() {
            SkippedExisting++;
        }

        public void RecordFailed(string sourcePath, string reason) {
            Failed++;
            failures.Add($"{sourcePath}: {reason}");
        }

        public string BuildSummary(int maxItems = 20) {
            var builder = new StringBuilder();
            builder.Append("RO cache build: ");
            builder.Append(Requested);
            builder.Append(" requested, ");
            builder.Append(Built);
            builder.Append(" built, ");
            builder.Append(SkippedExisting);
            builder.Append(" skipped existing, ");
            builder.Append(Failed);
            builder.AppendLine(" failed.");

            AppendList(builder, "Written", writtenPaths, maxItems);
            AppendList(builder, "Failures", failures, maxItems);
            return builder.ToString().TrimEnd();
        }

        private static void AppendList(StringBuilder builder, string label, IReadOnlyList<string> items, int maxItems) {
            if (items.Count == 0) {
                return;
            }

            builder.AppendLine();
            builder.AppendLine(label + ":");
            int count = items.Count < maxItems ? items.Count : maxItems;
            for (int i = 0; i < count; i++) {
                builder.Append("- ");
                builder.AppendLine(items[i]);
            }

            if (items.Count > count) {
                builder.Append("- ... ");
                builder.Append(items.Count - count);
                builder.AppendLine(" more");
            }
        }
    }
}
