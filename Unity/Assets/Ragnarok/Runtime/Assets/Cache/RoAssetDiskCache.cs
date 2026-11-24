using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Ragnarok.Assets {
    public sealed class RoAssetDiskCache {
        private const string CacheProductFolder = "RagnarokUnity";
        private const string CacheRootFolder = "AssetCache";
        public const int CacheFormatVersion = 3;

        private RoAssetDiskCache(string rootPath, string profileHash) {
            RootPath = rootPath;
            ProfileHash = profileHash;
        }

        public string RootPath { get; }
        public string ProfileHash { get; }
        public static string DefaultRootPath => Path.Combine(Application.persistentDataPath, CacheProductFolder, CacheRootFolder);

        public static RoAssetDiskCache Create(string contentRootPath, IReadOnlyList<string> grfFiles) {
            return Create(contentRootPath, grfFiles, DefaultRootPath);
        }

        public static RoAssetDiskCache Create(string contentRootPath, IReadOnlyList<string> grfFiles, string cacheRootPath) {
            if (string.IsNullOrWhiteSpace(cacheRootPath)) {
                throw new ArgumentException("Cache root path is required.", nameof(cacheRootPath));
            }

            string profileHash = BuildContentProfileHash(contentRootPath, grfFiles);
            string rootPath = Path.Combine(Path.GetFullPath(cacheRootPath), profileHash);
            return new RoAssetDiskCache(rootPath, profileHash);
        }

        public string GetPath(RoAssetCacheCategory category, string sourcePath, string extension) {
            var key = new RoAssetCacheKey(category, sourcePath, extension);
            string categoryFolder = GetCategoryFolder(category);
            string shard = key.Hash.Substring(0, 2);
            return Path.Combine(RootPath, categoryFolder, shard, key.Hash + key.Extension);
        }

        public bool TryReadBytes(RoAssetCacheCategory category, string sourcePath, string extension, out byte[] data) {
            string path = GetPath(category, sourcePath, extension);
            if (!File.Exists(path)) {
                data = null;
                return false;
            }

            data = File.ReadAllBytes(path);
            return true;
        }

        public string WriteBytes(RoAssetCacheCategory category, string sourcePath, string extension, byte[] data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            string path = GetPath(category, sourcePath, extension);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            string tempPath = path + ".tmp";
            File.WriteAllBytes(tempPath, data);

            if (File.Exists(path)) {
                File.Delete(path);
            }

            File.Move(tempPath, path);
            return path;
        }

        public bool Clear() {
            if (!Directory.Exists(RootPath)) {
                return true;
            }

            if (!IsSafeCachePath(RootPath)) {
                Debug.LogWarning($"Refusing to clear unsafe RO asset cache path: {RootPath}");
                return false;
            }

            Directory.Delete(RootPath, true);
            return true;
        }

        public string BuildSummary() {
            return $"RO disk cache v{CacheFormatVersion}, profile={ProfileHash}, root='{RootPath}'";
        }

        private static string BuildContentProfileHash(string contentRootPath, IReadOnlyList<string> grfFiles) {
            var builder = new StringBuilder();
            builder.Append("cache-format|");
            builder.AppendLine(CacheFormatVersion.ToString());

            string fullRoot = string.IsNullOrWhiteSpace(contentRootPath)
                ? string.Empty
                : Path.GetFullPath(contentRootPath);

            builder.AppendLine(fullRoot.ToLowerInvariant());

            if (grfFiles != null) {
                for (int i = 0; i < grfFiles.Count; i++) {
                    string grfFile = grfFiles[i];
                    if (string.IsNullOrWhiteSpace(grfFile)) {
                        continue;
                    }

                    string fullPath = RoAssetPath.ResolveContentFile(fullRoot, grfFile);
                    builder.Append(i);
                    builder.Append('|');
                    builder.Append(fullPath.ToLowerInvariant());
                    builder.Append('|');

                    if (File.Exists(fullPath)) {
                        var info = new FileInfo(fullPath);
                        builder.Append(info.Length);
                        builder.Append('|');
                        builder.Append(info.LastWriteTimeUtc.Ticks);
                    } else {
                        builder.Append("missing");
                    }

                    builder.AppendLine();
                }
            }

            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
            byte[] hash = sha.ComputeHash(bytes);

            var hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) {
                hex.Append(hash[i].ToString("x2"));
            }

            return hex.ToString().Substring(0, 16);
        }

        private static string GetCategoryFolder(RoAssetCacheCategory category) {
            return category switch {
                RoAssetCacheCategory.Raw => "raw",
                RoAssetCacheCategory.Texture => "textures",
                RoAssetCacheCategory.SpriteAtlas => "sprite-atlases",
                RoAssetCacheCategory.Map => "maps",
                RoAssetCacheCategory.Audio => "audio",
                RoAssetCacheCategory.Effect => "effects",
                RoAssetCacheCategory.Manifest => "manifests",
                RoAssetCacheCategory.GrfIndex => "grf-indexes",
                RoAssetCacheCategory.GameData => "game-data",
                _ => "misc"
            };
        }

        private static bool IsSafeCachePath(string path) {
            string fullPath = Path.GetFullPath(path);
            string normalized = fullPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string marker = Path.DirectorySeparatorChar + CacheProductFolder + Path.DirectorySeparatorChar + CacheRootFolder + Path.DirectorySeparatorChar;
            return normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0
                && Path.GetFileName(normalized).Length >= 8;
        }
    }
}
