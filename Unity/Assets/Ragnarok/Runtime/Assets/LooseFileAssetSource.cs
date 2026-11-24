using System;
using System.Collections.Generic;
using System.IO;

namespace Ragnarok.Assets {
    public sealed class LooseFileAssetSource : IRoAssetSource {
        private readonly string rootPath;
        private readonly HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public LooseFileAssetSource(string rootPath) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Root path is required.", nameof(rootPath));
            }

            this.rootPath = Path.GetFullPath(rootPath);
            if (!Directory.Exists(this.rootPath)) {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(this.rootPath, "*", SearchOption.AllDirectories)) {
                string relative = Path.GetRelativePath(this.rootPath, file);
                files.Add(RoAssetPath.Normalize(relative));
            }
        }

        public string Name => "Loose files";
        public IReadOnlyCollection<string> Files => files;

        public bool Exists(string path) {
            return files.Contains(RoAssetPath.Normalize(path));
        }

        public byte[] ReadBytes(string path) {
            path = RoAssetPath.Normalize(path);
            if (!files.Contains(path)) {
                return null;
            }

            string fullPath = Path.Combine(rootPath, path.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        }

        public bool TryGetFingerprint(string path, out string fingerprint) {
            path = RoAssetPath.Normalize(path);
            if (!files.Contains(path)) {
                fingerprint = null;
                return false;
            }

            string fullPath = Path.Combine(rootPath, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) {
                fingerprint = null;
                return false;
            }

            var info = new FileInfo(fullPath);
            fingerprint = "loose|" +
                path +
                "|" +
                info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "|" +
                info.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        // every loose file is its own copy
        public bool TryGetContentBlock(string path, out ulong offset, out uint length) {
            offset = 0;
            length = 0;
            return false;
        }

        public bool TryResolveFullPath(string path, out string fullPath) {
            path = RoAssetPath.Normalize(path);
            if (!files.Contains(path)) {
                fullPath = null;
                return false;
            }

            string resolved = Path.GetFullPath(Path.Combine(rootPath, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsUnderRoot(resolved) || !File.Exists(resolved)) {
                fullPath = null;
                return false;
            }

            fullPath = resolved;
            return true;
        }

        public void Dispose() {
        }

        private bool IsUnderRoot(string fullPath) {
            return string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
