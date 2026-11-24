using System;
using System.Collections.Generic;
using System.IO;
using Ragnarok.Assets;
using ROIO.Models;

namespace Ragnarok.Audio {
    public static class RoMapBgmResolver {
        public static bool TryResolve(string mapName, RoAssetTables tables, out string mp3File, out string tableKey) {
            if (tables == null) {
                throw new ArgumentNullException(nameof(tables));
            }

            foreach (string candidate in BuildMapKeyCandidates(mapName)) {
                if (tables.TryGetMapTable(candidate, out MapTableStruct table) && !string.IsNullOrWhiteSpace(table.mp3)) {
                    mp3File = Path.GetFileName(table.mp3.Replace('\\', '/'));
                    tableKey = candidate;
                    return true;
                }
            }

            mp3File = null;
            tableKey = null;
            return false;
        }

        public static IReadOnlyList<string> BuildBgmPathCandidates(string mp3File) {
            string file = Path.GetFileName(mp3File.Replace('\\', '/'));
            return new[] {
                "BGM/" + file,
                "bgm/" + file,
                file
            };
        }

        private static IEnumerable<string> BuildMapKeyCandidates(string mapName) {
            if (string.IsNullOrWhiteSpace(mapName)) {
                yield break;
            }

            string normalized = mapName.Replace('\\', '/').Trim();
            yield return normalized;

            if (normalized.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) {
                normalized = normalized.Substring("data/".Length);
                yield return normalized;
            }

            string fileName = Path.GetFileName(normalized);
            if (!string.Equals(fileName, normalized, StringComparison.OrdinalIgnoreCase)) {
                yield return fileName;
            }

            string withoutExtension = Path.GetFileNameWithoutExtension(fileName);
            if (!string.IsNullOrWhiteSpace(withoutExtension)) {
                yield return withoutExtension;
                yield return withoutExtension + ".rsw";
            }
        }
    }
}
