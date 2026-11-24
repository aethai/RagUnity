using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ROIO.GRF;
using Unity.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Ragnarok.Assets {
    public sealed class GrfAssetSource : IRoAssetSource {
        private static readonly ProfilerMarker GrfOpenMarker = new ProfilerMarker("Assets.GRF.Open");
        private static readonly ProfilerMarker GrfIndexCacheReadMarker = new ProfilerMarker("Assets.GRF.IndexCache.Read");
        private static readonly ProfilerMarker GrfIndexCacheWriteMarker = new ProfilerMarker("Assets.GRF.IndexCache.Write");
        private static readonly ProfilerMarker GrfIndexRebuildMarker = new ProfilerMarker("Assets.GRF.IndexCache.Rebuild");

        private readonly Grf grf;
        private readonly GrfPathCollection files;
        private readonly string name;

        public GrfAssetSource(string grfPath)
            : this(grfPath, null, 0, null) {
        }

        public GrfAssetSource(
            string grfPath,
            RoAssetDiskCache diskCache,
            int sourceOrder,
            Action<string> indexRebuildStarted = null) {
            if (string.IsNullOrWhiteSpace(grfPath)) {
                throw new ArgumentException("GRF path is required.", nameof(grfPath));
            }

            name = Path.GetFileName(grfPath);
            var totalStopwatch = Stopwatch.StartNew();
            var cacheTiming = new RoGrfIndexCacheReadTiming();
            long cachePathMilliseconds = 0;
            long descriptorHydrateMilliseconds = 0;
            long rebuildMilliseconds = 0;
            long cacheWriteMilliseconds = 0;
            long pathHydrateMilliseconds = 0;
            try {
                RoGrfIndexRecord indexRecord = RoGrfIndexRecord.ForArchive(grfPath, sourceOrder);
                var stopwatch = Stopwatch.StartNew();
                using (GrfIndexCacheReadMarker.Auto()) {
                    if (RoGrfIndexCache.TryReadGrf(diskCache, indexRecord, grfPath, out Grf cachedGrf, cacheTiming)) {
                        grf = cachedGrf;
                        descriptorHydrateMilliseconds = 0;
                        LoadedFromIndexCache = true;
                        LoadedFromMemoryIndexCache = cacheTiming.MemoryCacheHit;
                    }
                }

                cachePathMilliseconds = cacheTiming.DiskReadMilliseconds +
                    cacheTiming.BinaryParseMilliseconds +
                    descriptorHydrateMilliseconds;

                if (grf == null) {
                    indexRebuildStarted?.Invoke(grfPath);
                    stopwatch.Restart();
                    using (GrfIndexRebuildMarker.Auto())
                    using (GrfOpenMarker.Auto()) {
                        grf = Grf.grf_callback_open(grfPath, "r", null);
                    }

                    rebuildMilliseconds = stopwatch.ElapsedMilliseconds;

                    if (diskCache != null && grf != null) {
                        stopwatch.Restart();
                        using (GrfIndexCacheWriteMarker.Auto()) {
                            RoGrfIndexCache.Write(diskCache, indexRecord, grf);
                        }

                        cacheWriteMilliseconds = stopwatch.ElapsedMilliseconds;
                    }
                }
            } catch (Exception ex) {
                grf?.Dispose();
                throw new InvalidDataException(BuildOpenFailureMessage(grfPath, ex), ex);
            }

            var pathStopwatch = Stopwatch.StartNew();
            files = new GrfPathCollection(grf);
            pathHydrateMilliseconds = pathStopwatch.ElapsedMilliseconds;
            totalStopwatch.Stop();

            LogIndexLoad(
                grfPath,
                totalStopwatch.ElapsedMilliseconds,
                cachePathMilliseconds,
                cacheTiming,
                descriptorHydrateMilliseconds,
                rebuildMilliseconds,
                cacheWriteMilliseconds,
                pathHydrateMilliseconds);
        }

        public string Name => name;
        public IReadOnlyCollection<string> Files => files;
        public bool LoadedFromIndexCache { get; }
        public bool LoadedFromMemoryIndexCache { get; }

        public bool Exists(string path) {
            IReadOnlyList<string> candidates = RoAssetPath.GetLookupCandidates(path);
            for (int i = 0; i < candidates.Count; i++) {
                if (grf.GetDescriptor(candidates[i]) != null) {
                    return true;
                }
            }

            return false;
        }

        public byte[] ReadBytes(string path) {
            IReadOnlyList<string> candidates = RoAssetPath.GetLookupCandidates(path);
            for (int i = 0; i < candidates.Count; i++) {
                GrfFile descriptor = grf.GetDescriptor(candidates[i]);
                if (descriptor != null) {
                    return grf.ReadData(descriptor);
                }
            }

            return null;
        }

        public bool TryGetFingerprint(string path, out string fingerprint) {
            IReadOnlyList<string> candidates = RoAssetPath.GetLookupCandidates(path);
            for (int i = 0; i < candidates.Count; i++) {
                GrfFile descriptor = grf.GetDescriptor(candidates[i]);
                if (descriptor == null) {
                    continue;
                }

                string descriptorPath = RoAssetPath.Normalize(descriptor.name);
                fingerprint = "grf|" +
                    name +
                    "|" +
                    descriptorPath +
                    "|" +
                    descriptor.compressed_len +
                    "|" +
                    descriptor.compressed_len_aligned +
                    "|" +
                    descriptor.real_len +
                    "|" +
                    descriptor.flags +
                    "|" +
                    descriptor.pos +
                    "|" +
                    descriptor.hash;
                return true;
            }

            fingerprint = null;
            return false;
        }

        public bool TryGetContentBlock(string path, out ulong offset, out uint length) {
            IReadOnlyList<string> candidates = RoAssetPath.GetLookupCandidates(path);
            for (int i = 0; i < candidates.Count; i++) {
                GrfFile descriptor = grf.GetDescriptor(candidates[i]);
                if (descriptor != null) {
                    offset = descriptor.pos;
                    length = descriptor.compressed_len;
                    return true;
                }
            }

            offset = 0;
            length = 0;
            return false;
        }

        public void Dispose() {
            grf?.Dispose();
        }

        private static string BuildOpenFailureMessage(string grfPath, Exception exception) {
            string header = ReadHeaderSummary(grfPath);
            return $"Could not open GRF '{grfPath}': {exception.Message}. {header}";
        }

        private void LogIndexLoad(
            string grfPath,
            long totalMilliseconds,
            long cachePathMilliseconds,
            RoGrfIndexCacheReadTiming cacheTiming,
            long descriptorHydrateMilliseconds,
            long rebuildMilliseconds,
            long cacheWriteMilliseconds,
            long pathHydrateMilliseconds) {
            long maxPhaseMilliseconds = Math.Max(
                totalMilliseconds,
                Math.Max(
                    Math.Max(cachePathMilliseconds, rebuildMilliseconds),
                    Math.Max(cacheWriteMilliseconds, pathHydrateMilliseconds)));
            if (!RoAssetStartupTiming.ShouldLogForDiagnostics(maxPhaseMilliseconds)) {
                return;
            }

            if (LoadedFromIndexCache) {
                if (cacheTiming.MemoryCacheHit) {
                    UnityEngine.Debug.Log(
                        $"RO GRF index memory cache hit: {Path.GetFileName(grfPath)}, files={files.Count}, total={totalMilliseconds} ms, " +
                        $"paths={pathHydrateMilliseconds} ms.");
                    return;
                }

                UnityEngine.Debug.Log(
                    $"RO GRF index cache hit: {Path.GetFileName(grfPath)}, files={files.Count}, total={totalMilliseconds} ms, " +
                    $"cache={cachePathMilliseconds} ms (disk={cacheTiming.DiskReadMilliseconds} ms/{FormatBytes(cacheTiming.CacheBytes)}, " +
                    $"parse={cacheTiming.BinaryParseMilliseconds} ms/{cacheTiming.DescriptorCount} descriptors, " +
                    $"hydrate={descriptorHydrateMilliseconds} ms), paths={pathHydrateMilliseconds} ms.");
                return;
            }

            UnityEngine.Debug.Log(
                $"RO GRF index cache rebuild: {Path.GetFileName(grfPath)}, files={files.Count}, total={totalMilliseconds} ms, " +
                $"open={rebuildMilliseconds} ms, write={cacheWriteMilliseconds} ms, paths={pathHydrateMilliseconds} ms.");
        }

        private static string FormatBytes(long byteCount) {
            if (byteCount >= 1024L * 1024L) {
                return (byteCount / (1024d * 1024d)).ToString("0.0") + " MB";
            }

            if (byteCount >= 1024L) {
                return (byteCount / 1024d).ToString("0.0") + " KB";
            }

            return byteCount + " B";
        }

        private sealed class GrfPathCollection : IReadOnlyCollection<string> {
            private readonly Grf grf;

            public GrfPathCollection(Grf grf) {
                this.grf = grf ?? throw new ArgumentNullException(nameof(grf));
            }

            public int Count => grf.FileCount;

            public IEnumerator<string> GetEnumerator() {
                return grf.EnumeratePaths().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        private static string ReadHeaderSummary(string grfPath) {
            try {
                using FileStream stream = File.OpenRead(grfPath);
                var header = new byte[Math.Min(46, (int) stream.Length)];
                int read = stream.Read(header, 0, header.Length);
                int magicLength = 0;
                int magicLimit = Math.Min(read, 15);
                while (magicLength < magicLimit && header[magicLength] != 0) {
                    magicLength++;
                }

                string magic = System.Text.Encoding.ASCII.GetString(header, 0, magicLength);
                uint version = read >= 46 ? BitConverter.ToUInt32(header, 42) : 0;
                return $"size={stream.Length} byte(s), magic='{magic}', version=0x{version:X}.";
            } catch (Exception headerException) {
                return $"Could not read GRF header: {headerException.Message}.";
            }
        }
    }
}
