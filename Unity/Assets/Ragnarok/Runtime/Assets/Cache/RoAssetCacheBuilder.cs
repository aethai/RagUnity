using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.Assets {
    public sealed class RoAssetCacheBuilder {
        private static readonly string[] TextureExtensions = {
            "bmp",
            "tga",
            "png",
            "jpg",
            "jpeg"
        };

        private readonly RoAssetDatabase assets;

        public RoAssetCacheBuilder(RoAssetDatabase assets) {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        public string WriteManifest() {
            return assets.WriteManifestCache();
        }

        public RoAssetCacheBuildReport BuildTextureProbe(int maxTexturesPerExtension = 10, bool overwrite = false) {
            var report = new RoAssetCacheBuildReport();
            foreach (string extension in TextureExtensions) {
                BuildTexturesWithExtension(extension, maxTexturesPerExtension, overwrite, report);
            }

            return report;
        }

        public RoAssetCacheBuildReport BuildTexturesWithExtension(string extension, int maxTextures, bool overwrite) {
            var report = new RoAssetCacheBuildReport();
            BuildTexturesWithExtension(extension, maxTextures, overwrite, report);
            return report;
        }

        private void BuildTexturesWithExtension(string extension, int maxTextures, bool overwrite, RoAssetCacheBuildReport report) {
            if (maxTextures <= 0) {
                return;
            }

            List<string> files = assets.ListFilesWithExtension(extension);
            int count = files.Count < maxTextures ? files.Count : maxTextures;
            for (int i = 0; i < count; i++) {
                BuildTexture(files[i], overwrite, report);
            }
        }

        private void BuildTexture(string sourcePath, bool overwrite, RoAssetCacheBuildReport report) {
            report.RecordRequested();

            RoAssetDiskCache cache = assets.DiskCache;
            if (cache == null) {
                report.RecordFailed(sourcePath, "disk cache is not initialized");
                return;
            }

            if (!overwrite && cache.TryReadBytes(RoAssetCacheCategory.Texture, sourcePath, ".png", out _)) {
                report.RecordSkippedExisting();
                return;
            }

            Texture2D texture = assets.LoadTexture(sourcePath);
            if (texture == null) {
                report.RecordFailed(sourcePath, "texture load failed");
                return;
            }

            byte[] png;
            try {
                png = ImageConversion.EncodeToPNG(texture);
            } catch (Exception ex) {
                report.RecordFailed(sourcePath, "PNG encode failed: " + ex.Message);
                return;
            }

            if (png == null || png.Length == 0) {
                report.RecordFailed(sourcePath, "PNG encode returned empty data");
                return;
            }

            string path = cache.WriteBytes(RoAssetCacheCategory.Texture, sourcePath, ".png", png);
            report.RecordBuilt(path);
        }
    }
}
