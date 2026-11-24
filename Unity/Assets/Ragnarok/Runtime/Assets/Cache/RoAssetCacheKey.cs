using System;
using System.Security.Cryptography;
using System.Text;

namespace Ragnarok.Assets {
    public readonly struct RoAssetCacheKey {
        public RoAssetCacheKey(RoAssetCacheCategory category, string sourcePath, string extension) {
            Category = category;
            SourcePath = RoAssetPath.Normalize(sourcePath ?? string.Empty);
            Extension = NormalizeExtension(extension);
            Hash = ComputeHash($"{Category}|{SourcePath}|{Extension}");
        }

        public RoAssetCacheCategory Category { get; }
        public string SourcePath { get; }
        public string Extension { get; }
        public string Hash { get; }

        private static string NormalizeExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return ".bin";
            }

            extension = extension.Trim();
            if (!extension.StartsWith(".", StringComparison.Ordinal)) {
                extension = "." + extension;
            }

            return extension.ToLowerInvariant();
        }

        private static string ComputeHash(string value) {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] hash = sha.ComputeHash(bytes);

            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
