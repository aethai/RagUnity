using System;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Diagnostics;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Maps {
    public static class RoGroundTextureAtlasBuilder {
        private const int SourceTextureSize = 256;
        private const int AtlasCellSize = 258;
        private const int AtlasAnisoLevel = 8;

        private static FilterMode MipmappedFilter(FilterMode filterMode) {
            return filterMode == FilterMode.Bilinear ? FilterMode.Trilinear : filterMode;
        }
        private const int AtlasPadding = 1;
        private const int DiskAtlasCacheVersion = 2;
        private const string DiskAtlasCacheExtension = ".png";
        private static int bypassDiskAtlasCache;

        public static Texture2D Build(GND.Mesh ground, RoAssetDatabase assets) {
            return Build(ground, assets, FilterMode.Bilinear);
        }

        public static Texture2D Build(GND.Mesh ground, RoAssetDatabase assets, FilterMode filterMode) {
            if (ground == null) {
                throw new ArgumentNullException(nameof(ground));
            }

            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (ground.textures == null || ground.textures.Length == 0) {
                return Texture2D.whiteTexture;
            }

            return RoRuntimeMapAssetCache.GetOrBuildGroundAtlas(
                assets,
                ground,
                filterMode,
                () => BuildCachedOrUncached(ground, assets, filterMode));
        }

        private static Texture2D BuildCachedOrUncached(GND.Mesh ground, RoAssetDatabase assets, FilterMode filterMode) {
            if (!ShouldUseDiskAtlasCache(ground, assets) || ShouldBypassDiskAtlasCache() || HdScale(ground) > 1) {
                return BuildUncached(ground, assets, filterMode);
            }

            string diskKey = BuildDiskCacheKey(ground, filterMode);
            if (TryLoadDiskCachedAtlas(assets, diskKey, filterMode, out Texture2D cached)) {
                return cached;
            }

            Texture2D atlas = BuildUncached(ground, assets, filterMode);
            TryWriteDiskCachedAtlas(assets, diskKey, atlas);
            return atlas;
        }

        /// <summary>
        /// 2 when the HD pack has any of the map's ground textures: the atlas is the exact 2x of the classic layout
        /// (cells 516, textures 512, padding 2), so the compiled ground UVs stay valid.
        /// </summary>
        private static int HdScale(GND.Mesh ground) {
            if (!RoHdTextures.Enabled || ground.textures == null) return 1;
            foreach (string texture in ground.textures) {
                if (RoHdTextures.Has("data/texture/" + texture) || RoHdTextures.Has(texture)) return 2;
            }

            return 1;
        }

        private static Texture2D BuildUncached(GND.Mesh ground, RoAssetDatabase assets, FilterMode filterMode) {
            int scale = HdScale(ground);
            int cellSize = AtlasCellSize * scale, textureSize = SourceTextureSize * scale, padding = AtlasPadding * scale;
            int columns = (int) Math.Round(Math.Sqrt(ground.textures.Length));
            int rows = (int) Math.Ceiling(Math.Sqrt(ground.textures.Length));
            int width = NextPowerOfTwo(columns * AtlasCellSize) * scale;
            int height = NextPowerOfTwo(rows * AtlasCellSize) * scale;

            // linear: false = sRGB. BMP/TGA source files are gamma-encoded; marking the
            // atlas as sRGB lets the GPU do the sRGB→linear conversion on sampling so
            // shader math stays in linear space. Without this the atlas is treated as
            // linear data and the whole ground renders ~50% too dark (double-gamma).
            // mipmaps + anisotropic filtering: without them the ground shimmered while the camera moved and the far half of
            // the screen aliased; the 1-texel padding of each cell keeps the first mips clean, deeper mips blend
            // neighbouring cells a little only far away
            var atlas = new Texture2D(width, height, TextureFormat.RGBA32, true, linear: false) {
                name = "RO Ground Texture Atlas",
                filterMode = MipmappedFilter(filterMode),
                anisoLevel = AtlasAnisoLevel,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] clear = new Color32[width * height];
            for (int i = 0; i < clear.Length; i++) {
                clear[i] = new Color32(0, 0, 0, 0);
            }

            atlas.SetPixels32(clear);

            int loaded = 0;
            int missing = 0;
            var missingSamples = new System.Text.StringBuilder();
            for (int i = 0; i < ground.textures.Length; i++) {
                Texture2D texture = scale > 1 ? LoadHdTexture(ground.textures[i]) : null;
                bool hd = texture != null;
                if (texture == null) texture = LoadTexture(assets, ground.textures[i]);
                if (texture == null) {
                    missing++;
                    if (missing <= 12) {
                        missingSamples.AppendLine("- " + ground.textures[i]);
                    }

                    continue;
                }

                loaded++;
                int x = (i % columns) * cellSize + padding;
                int y = (i / columns) * cellSize + padding;
                CopyTextureWithPadding(texture, atlas, x, y, textureSize, padding);
                if (hd) UnityEngine.Object.Destroy(texture);
            }

            if (missing > 0) {
                Debug.LogWarning(
                    $"RO ground atlas loaded {loaded}/{ground.textures.Length} texture(s); missing {missing}.\n" +
                    missingSamples.ToString().TrimEnd()
                );
            } else {
                if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                    RoRuntimeDiagnostics.IncrementDebugLogLine();
                    Debug.Log($"RO ground atlas loaded {loaded}/{ground.textures.Length} texture(s).");
                }
            }

            atlas.Apply(true, false);
            return atlas;
        }

        private static Texture2D LoadTexture(RoAssetDatabase assets, string path) {
            try {
                return assets.LoadTexture(path);
            } catch (Exception ex) {
                Debug.LogWarning($"Could not load ground texture '{path}': {ex.Message}");
                return null;
            }
        }

        private static Texture2D LoadHdTexture(string name) {
            if (RoHdTextures.TryLoad("data/texture/" + name, out Texture2D texture, out _)) return texture;
            return RoHdTextures.TryLoad(name, out texture, out _) ? texture : null;
        }

        private static void CopyTextureWithPadding(Texture2D source, Texture2D atlas, int atlasX, int atlasY, int size, int padding) {
            Color32[] pixels = ReadPixels(source, size);
            if (pixels == null || pixels.Length == 0) {
                return;
            }

            atlas.SetPixels32(atlasX, atlasY, size, size, pixels);
            for (int p = 1; p <= padding; p++) {
                CopyHorizontalEdge(atlas, atlasX, pixels, 0, atlasY - p, size);
                CopyHorizontalEdge(atlas, atlasX, pixels, size - 1, atlasY + size - 1 + p, size);
                CopyVerticalEdge(atlas, atlasY, pixels, 0, atlasX - p, size);
                CopyVerticalEdge(atlas, atlasY, pixels, size - 1, atlasX + size - 1 + p, size);
                for (int q = 1; q <= padding; q++) {
                    atlas.SetPixel(atlasX - p, atlasY - q, pixels[0]);
                    atlas.SetPixel(atlasX + size - 1 + p, atlasY - q, pixels[size - 1]);
                    atlas.SetPixel(atlasX - p, atlasY + size - 1 + q, pixels[(size - 1) * size]);
                    atlas.SetPixel(atlasX + size - 1 + p, atlasY + size - 1 + q, pixels[pixels.Length - 1]);
                }
            }
        }

        private static Color32[] ReadPixels(Texture2D texture, int size) {
            if (texture.width == size && texture.height == size) {
                return FlipRows(texture.GetPixels32());
            }

            var scaled = new Texture2D(size, size, TextureFormat.RGBA32, false, linear: false);
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    scaled.SetPixel(x, y, texture.GetPixelBilinear(u, v));
                }
            }

            scaled.Apply(false, false);
            Color32[] pixels = FlipRows(scaled.GetPixels32());
            if (Application.isPlaying) {
                UnityEngine.Object.Destroy(scaled);
            } else {
                UnityEngine.Object.DestroyImmediate(scaled);
            }

            return pixels;
        }

        private static Color32[] FlipRows(Color32[] pixels) {
            var flipped = new Color32[pixels.Length];
            int size = (int) Math.Round(Math.Sqrt(pixels.Length));
            for (int y = 0; y < size; y++) {
                int sourceRow = y * size;
                int targetRow = (size - 1 - y) * size;
                Array.Copy(pixels, sourceRow, flipped, targetRow, size);
            }

            return flipped;
        }

        private static void CopyHorizontalEdge(Texture2D atlas, int atlasX, Color32[] pixels, int sourceY, int targetY, int size) {
            var edge = new Color32[size];
            Array.Copy(pixels, sourceY * size, edge, 0, size);
            atlas.SetPixels32(atlasX, targetY, size, 1, edge);
        }

        private static void CopyVerticalEdge(Texture2D atlas, int atlasY, Color32[] pixels, int sourceX, int targetX, int size) {
            var edge = new Color32[size];
            for (int y = 0; y < size; y++) {
                edge[y] = pixels[sourceX + y * size];
            }

            atlas.SetPixels32(targetX, atlasY, 1, size, edge);
        }

        private static int NextPowerOfTwo(int value) {
            int result = 1;
            while (result < value) {
                result <<= 1;
            }

            return result;
        }

        private static string BuildDiskCacheKey(GND.Mesh ground, FilterMode filterMode) {
            var builder = new StringBuilder();
            builder.Append("ground-atlas-v");
            builder.Append(DiskAtlasCacheVersion);
            builder.Append("|filter=");
            builder.Append(filterMode);
            builder.Append("|textures=");
            if (ground?.textures != null) {
                for (int i = 0; i < ground.textures.Length; i++) {
                    if (i > 0) {
                        builder.Append(';');
                    }

                    builder.Append(NormalizePath(ground.textures[i]));
                }
            }

            return builder.ToString();
        }

        private static bool ShouldUseDiskAtlasCache(GND.Mesh ground, RoAssetDatabase assets) {
            if (assets?.DiskCache == null || ground?.textures == null) {
                return false;
            }

            for (int i = 0; i < ground.textures.Length; i++) {
                if (assets.TryResolveLooseFilePath(ground.textures[i], out _)) {
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldBypassDiskAtlasCache() {
            if (bypassDiskAtlasCache != 0) {
                return bypassDiskAtlasCache == 1;
            }

            bool enabled = false;
            try {
                string[] args = Environment.GetCommandLineArgs();
                if (args != null) {
                    for (int i = 0; i < args.Length; i++) {
                        string arg = args[i];
                        if (string.Equals(arg, "-roBypassGroundAtlasDiskCache", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "-ro-bypass-ground-atlas-disk-cache", StringComparison.OrdinalIgnoreCase)) {
                            enabled = true;
                            break;
                        }
                    }
                }
            } catch {
                enabled = false;
            }

            if (!enabled) {
                try {
                    string env = Environment.GetEnvironmentVariable("RO_BYPASS_GROUND_ATLAS_DISK_CACHE");
                    enabled = string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(env, "yes", StringComparison.OrdinalIgnoreCase);
                } catch {
                    enabled = false;
                }
            }

            bypassDiskAtlasCache = enabled ? 1 : 2;
            return enabled;
        }

        private static bool TryLoadDiskCachedAtlas(
            RoAssetDatabase assets,
            string diskKey,
            FilterMode filterMode,
            out Texture2D atlas
        ) {
            atlas = null;
            RoAssetDiskCache diskCache = assets?.DiskCache;
            if (diskCache == null || string.IsNullOrEmpty(diskKey)) {
                return false;
            }

            try {
                if (!diskCache.TryReadBytes(RoAssetCacheCategory.Map, diskKey, DiskAtlasCacheExtension, out byte[] data) ||
                    data == null ||
                    data.Length == 0) {
                    return false;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear: false) {
                    name = "RO Ground Texture Atlas (cached)",
                    filterMode = MipmappedFilter(filterMode),
                    anisoLevel = AtlasAnisoLevel,
                    wrapMode = TextureWrapMode.Clamp
                };

                if (ImageConversion.LoadImage(texture, data, false)) {
                    texture.Apply(true, false);
                    atlas = texture;
                    return true;
                }

                DestroyRuntimeObject(texture);
            } catch (Exception ex) {
                Debug.LogWarning($"Could not read cached RO ground atlas: {ex.Message}");
            }

            atlas = null;
            return false;
        }

        private static void TryWriteDiskCachedAtlas(RoAssetDatabase assets, string diskKey, Texture2D atlas) {
            RoAssetDiskCache diskCache = assets?.DiskCache;
            if (diskCache == null || string.IsNullOrEmpty(diskKey) || atlas == null || ReferenceEquals(atlas, Texture2D.whiteTexture)) {
                return;
            }

            try {
                byte[] png = ImageConversion.EncodeToPNG(atlas);
                if (png != null && png.Length > 0) {
                    diskCache.WriteBytes(RoAssetCacheCategory.Map, diskKey, DiskAtlasCacheExtension, png);
                }
            } catch (Exception ex) {
                Debug.LogWarning($"Could not write cached RO ground atlas: {ex.Message}");
            }
        }

        private static string NormalizePath(string path) =>
            string.IsNullOrEmpty(path) ? string.Empty : path.Trim().Replace('\\', '/');

        private static void DestroyRuntimeObject(UnityEngine.Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                UnityEngine.Object.Destroy(target);
            } else {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
