using System;
using Ragnarok.Assets;

namespace Ragnarok.UI {
    /// <summary>The client's interface folder in the GRFs: data\texture\유저인터페이스\…</summary>
    public static class RoInterfacePath {
        /// <summary>A path under the interface folder ("loading00.jpg" → data/texture/유저인터페이스/loading00.jpg); "data/…" as is.</summary>
        public static string Build(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative interface path is required.", nameof(relativePath));
            }

            relativePath = relativePath.Trim().Replace('\\', '/').TrimStart('/');
            if (relativePath.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) {
                return RoAssetPath.Normalize(relativePath);
            }

            return RoAssetPath.Normalize("data/texture/" + InterfaceSegment + "/" + relativePath);
        }

        // 유저인터페이스, spelled as the CP949 bytes of the GRF names and decoded like them
        private static readonly string InterfaceSegment = LegacyKorean(0xC0, 0xAF, 0xC0, 0xFA, 0xC0, 0xCE, 0xC5, 0xCD, 0xC6, 0xE4, 0xC0, 0xCC, 0xBD, 0xBA);

        private static string LegacyKorean(params int[] bytes) {
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) {
                chars[i] = (char) bytes[i];
            }

            string legacy = new string(chars);
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(legacy);
            return string.IsNullOrEmpty(decoded) ? legacy : decoded;
        }
    }
}
