using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using B83.Image.BMP;
using Newtonsoft.Json.Linq;
using Ragnarok.Audio;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Localization;
using Ragnarok.Maps;
using ROIO.GRF;
using ROIO.Loaders;
using ROIO.Models.FileTypes;
using ROIO.Utils;
using Unity.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Ragnarok.Assets {
    public sealed class RoAssetDatabase : IDisposable {
        private const string LooseFileSourceName = "Loose files";
        private const string ManifestCacheKey = "asset-manifest";
        private const int ManifestCacheMagic = 0x464E414D; // MANF
        private const int ManifestCacheVersion = 2;
        private static readonly ProfilerMarker BuildManifestMarker = new ProfilerMarker("Assets.BuildManifest");
        private static readonly ProfilerMarker ManifestCacheReadMarker = new ProfilerMarker("Assets.ManifestCache.Read");
        private static readonly ProfilerMarker ManifestCacheWriteMarker = new ProfilerMarker("Assets.ManifestCache.Write");
        private static readonly ProfilerMarker LoadAliasesMarker = new ProfilerMarker("Assets.LoadResnametableAliases");
        private static readonly ProfilerMarker LoadTablesMarker = new ProfilerMarker("Assets.LoadTables");
        private static readonly ProfilerMarker LoadGameDataMarker = new ProfilerMarker("Assets.LoadGameData");
        private static readonly ProfilerMarker LoadLocalizationMarker = new ProfilerMarker("Assets.LoadLocalization");
        private static readonly object ManifestMemoryCacheSync = new object();
        private static readonly object StartupDataMemoryCacheSync = new object();
        private static readonly Dictionary<string, StartupDataMemoryCacheSnapshot> StartupDataMemoryCaches =
            new Dictionary<string, StartupDataMemoryCacheSnapshot>(StringComparer.Ordinal);
        private static ManifestMemoryCacheSnapshot manifestMemoryCache;
        private const int MaxStartupDataMemoryCacheProfiles = 4;

        private readonly List<IRoAssetSource> sources = new List<IRoAssetSource>();
        private Dictionary<string, int> manifest = new Dictionary<string, int>(StringComparer.Ordinal);
        private Dictionary<string, List<string>> extensionFileCache = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> availableExtensions = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> sortedExtensionFileCaches = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, RoAssetLookupReport> lookupCache = new Dictionary<string, RoAssetLookupReport>(StringComparer.Ordinal);
        private readonly Dictionary<string, FallbackLookupResult> suffixFallbackCache = new Dictionary<string, FallbackLookupResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, FallbackLookupResult> containsFallbackCache = new Dictionary<string, FallbackLookupResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioClip> audioClipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioClip> rawAudioClipCache = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly object lookupCacheSync = new object();
        private readonly object extensionFileCacheSync = new object();
        private readonly object fallbackCacheSync = new object();
        private readonly BMPLoader bmpLoader = new BMPLoader();
        private RoAssetDiskCache diskCache;
        private RoResnametableAliasResolver resnametableAliases = RoResnametableAliasResolver.Empty;
        private RoAssetTables tables = RoAssetTables.Empty;
        private RoGameDataService gameData = RoGameDataService.Empty;
        private RoLocalizationService localization = RoLocalizationService.Empty;
        private RoAssetStartupTiming startupTiming;
        private bool initialized;

        public bool IsInitialized => initialized;
        public int SourceCount => sources.Count;
        public int FileCount => manifest.Count;
        public int TextureCacheCount => textureCache.Count;
        public int AudioClipCacheCount => audioClipCache.Count;
        public bool LoadedManifestFromCache { get; private set; }
        public RoAssetDiskCache DiskCache => diskCache;
        public RoAssetTables Tables => tables;

        /// <summary>The client folder the GRFs were opened from (its loose files, _tmpEmblem, GuildTip.txt …).</summary>
        public string ClientRootPath { get; private set; }
        public RoGameDataService GameData => gameData;
        public RoLocalizationService Localization => localization;
        public RoAssetDiagnostics Diagnostics { get; } = new RoAssetDiagnostics();

        public RoAssetCacheBuilder CreateCacheBuilder() {
            EnsureInitialized();
            return new RoAssetCacheBuilder(this);
        }

        public static bool CanLoadGrfIndexesFromCache(
            string rootPath,
            IReadOnlyList<string> grfFiles,
            string cacheRootPath = null) {
            if (grfFiles == null || grfFiles.Count == 0) {
                return false;
            }

            if (string.IsNullOrWhiteSpace(rootPath)) {
                return false;
            }

            try {
                string fullRootPath = Path.GetFullPath(rootPath);
                List<string> fullGrfPaths = ResolveGrfFiles(fullRootPath, grfFiles);
                if (fullGrfPaths.Count == 0) {
                    return false;
                }

                RoAssetDiskCache cache = string.IsNullOrWhiteSpace(cacheRootPath)
                    ? RoAssetDiskCache.Create(fullRootPath, fullGrfPaths)
                    : RoAssetDiskCache.Create(fullRootPath, fullGrfPaths, cacheRootPath);
                for (int i = 0; i < fullGrfPaths.Count; i++) {
                    RoGrfIndexRecord record = RoGrfIndexRecord.ForArchive(fullGrfPaths[i], i);
                    if (!RoGrfIndexCache.HasIndexFile(cache, record)) {
                        return false;
                    }
                }

                return true;
            } catch {
                return false;
            }
        }

        public void Initialize(
            string rootPath,
            IReadOnlyList<string> grfFiles,
            Action<string> grfIndexRebuildStarted = null,
            string cacheRootPath = null) {
            if (grfFiles == null || grfFiles.Count == 0) {
                throw new ArgumentException("At least one GRF file is required.", nameof(grfFiles));
            }

            var timing = RoAssetStartupTiming.Start();
            startupTiming = timing;
            bool succeeded = false;
            try {
                DisposeSources();
                diskCache = null;
                manifest = new Dictionary<string, int>(StringComparer.Ordinal);
                extensionFileCache.Clear();
                availableExtensions.Clear();
                sortedExtensionFileCaches.Clear();
                lookupCache.Clear();
                suffixFallbackCache.Clear();
                containsFallbackCache.Clear();
                textureCache.Clear();
                audioClipCache.Clear();
                rawAudioClipCache.Clear();
                LoadedManifestFromCache = false;
                resnametableAliases = RoResnametableAliasResolver.Empty;
                tables = RoAssetTables.Empty;
                gameData = RoGameDataService.Empty;
                localization = RoLocalizationService.Empty;
                Diagnostics.Clear();

                string fullRootPath = Path.GetFullPath(rootPath);
                List<string> legacyGrfFiles = ResolveGrfFiles(fullRootPath, grfFiles);
                if (legacyGrfFiles.Count == 0) {
                    throw new ArgumentException("At least one non-empty GRF file entry is required.", nameof(grfFiles));
                }

                diskCache = string.IsNullOrWhiteSpace(cacheRootPath)
                    ? RoAssetDiskCache.Create(fullRootPath, legacyGrfFiles)
                    : RoAssetDiskCache.Create(fullRootPath, legacyGrfFiles, cacheRootPath);

                ClientRootPath = fullRootPath;
                using (timing.MeasureLooseSource()) {
                    AddLooseSource(fullRootPath);
                }

                using (timing.MeasureGrfSources()) {
                    AddGrfSources(legacyGrfFiles, grfIndexRebuildStarted);
                }

                using (BuildManifestMarker.Auto())
                using (timing.MeasureManifest()) {
                    // built from the sources' file lists, which are in memory already: faster than reading and parsing the
                    // disk copy (29 MB, every path a second string: ~190 vs ~360 ms at start), and never stale (that copy
                    // was keyed by the GRFs alone, so a change of the loose files kept serving the old list)
                    if (!TryLoadManifestMemoryCache()) {
                        BuildManifest();
                        TryStoreManifestMemoryCache();
                    }
                }

                initialized = true;
                using (LoadAliasesMarker.Auto())
                using (timing.MeasureAliases()) {
                    if (!TryLoadCachedResnametableAliases(out resnametableAliases)) {
                        resnametableAliases = LoadResnametableAliases();
                        StoreCachedResnametableAliases(resnametableAliases);
                    }
                }

                lookupCache.Clear();
                using (LoadTablesMarker.Auto())
                using (timing.MeasureTables()) {
                    if (!TryLoadCachedTables(out tables)) {
                        tables = RoAssetTables.Load(this);
                        StoreCachedTables(tables);
                    }
                }

                using (LoadGameDataMarker.Auto())
                using (timing.MeasureGameData()) {
                    gameData = RoGameDataService.Load(this, timing);
                }

                using (LoadLocalizationMarker.Auto())
                using (timing.MeasureLocalization()) {
                    if (!TryLoadCachedLocalization(out localization)) {
                        localization = RoLocalizationService.Load(this);
                        StoreCachedLocalization(localization);
                    }
                }

                succeeded = true;
            } finally {
                if (ReferenceEquals(startupTiming, timing)) {
                    startupTiming = null;
                }

                timing.Log(succeeded, SourceCount, FileCount);
            }
        }

        public bool Exists(string path) {
            EnsureInitialized();
            return ResolveLookupCandidates(path).IsResolved;
        }

        public bool TryGetManifestEntry(string path, out RoAssetManifestEntry entry) {
            EnsureInitialized();
            return TryGetManifestEntry(path, out entry, out _);
        }

        public RoAssetLookupReport ResolveLookupCandidates(string path) {
            EnsureInitialized();
            string key = RoAssetPath.Normalize(path);
            lock (lookupCacheSync) {
                if (lookupCache.TryGetValue(key, out RoAssetLookupReport cachedReport)) {
                    return cachedReport;
                }
            }

            RoAssetLookupReport report;
            using (BuildLookupMarker.Auto()) {
                report = BuildLookupReport(path);
            }

            lock (lookupCacheSync) {
                if (lookupCache.TryGetValue(key, out RoAssetLookupReport cachedReport)) {
                    return cachedReport;
                }

                lookupCache[key] = report;
            }

            return report;
        }

        public bool TryResolvePath(string path, out string resolvedPath) {
            RoAssetLookupReport lookup = ResolveLookupCandidates(path);
            resolvedPath = lookup.ResolvedPath;
            return lookup.IsResolved;
        }

        public bool TryFindFirstPathEndingWith(string suffix, out string resolvedPath) {
            FallbackLookupResult result = FindUniquePathEndingWith(suffix);
            resolvedPath = result.ResolvedPath;
            return result.IsResolved;
        }

        public bool TryFindFirstPathContaining(string contains, string requiredEnding, out string resolvedPath) {
            FallbackLookupResult result = FindUniquePathContaining(contains, requiredEnding);
            resolvedPath = result.ResolvedPath;
            return result.IsResolved;
        }

        public List<RoAssetManifestEntry> FindSourceEntries(string path) {
            EnsureInitialized();
            var entries = new List<RoAssetManifestEntry>();
            IReadOnlyList<string> candidates = ResolveLookupCandidates(path).Candidates;
            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++) {
                IRoAssetSource source = sources[sourceIndex];
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++) {
                    string candidate = candidates[candidateIndex];
                    if (source.Exists(candidate)) {
                        entries.Add(new RoAssetManifestEntry(candidate, source.Name, sourceIndex));
                        break;
                    }
                }
            }

            return entries;
        }

        public bool TryResolveLooseFilePath(string path, out string fullPath) {
            EnsureInitialized();
            IReadOnlyList<string> candidates = ResolveLookupCandidates(path).Candidates;
            for (int c = 0; c < candidates.Count; c++) {
                string candidate = candidates[c];
                for (int i = 0; i < sources.Count; i++) {
                    if (sources[i] is LooseFileAssetSource looseSource &&
                        looseSource.TryResolveFullPath(candidate, out fullPath)) {
                        return true;
                    }
                }
            }

            fullPath = null;
            return false;
        }

        public bool TryGetAssetFingerprint(string path, out string resolvedPath, out string fingerprint) {
            EnsureInitialized();
            if (!TryGetManifestEntry(path, out RoAssetManifestEntry entry, out _)) {
                resolvedPath = null;
                fingerprint = null;
                return false;
            }

            if (entry.SourceIndex < 0 ||
                entry.SourceIndex >= sources.Count ||
                !sources[entry.SourceIndex].TryGetFingerprint(entry.Path, out fingerprint)) {
                resolvedPath = null;
                fingerprint = null;
                return false;
            }

            resolvedPath = entry.Path;
            return true;
        }

        /// <summary>
        /// The identity of the bytes that <paramref name="path"/> resolves to; see <see cref="RoAssetContentKey"/>.
        /// Returns false for loose files and missing assets. Safe to call from worker threads.
        /// </summary>
        public bool TryGetContentKey(string path, out RoAssetContentKey key) {
            EnsureInitialized();
            if (TryGetManifestEntry(path, out RoAssetManifestEntry entry, out _) &&
                entry.SourceIndex >= 0 &&
                entry.SourceIndex < sources.Count &&
                sources[entry.SourceIndex].TryGetContentBlock(entry.Path, out ulong offset, out uint length)) {
                key = new RoAssetContentKey(entry.SourceIndex, offset, length);
                return true;
            }

            key = default;
            return false;
        }

        public IReadOnlyCollection<string> ListFiles() {
            EnsureInitialized();
            return manifest.Keys;
        }

        public List<string> ListFilesWithExtension(string extension) {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension is required.", nameof(extension));
            }

            extension = extension.TrimStart('.').ToLowerInvariant();
            lock (extensionFileCacheSync) {
                if (extensionFileCache.TryGetValue(extension, out List<string> cachedFiles)) {
                    if (!sortedExtensionFileCaches.Contains(extension)) {
                        long sortStartTimestamp = Stopwatch.GetTimestamp();
                        cachedFiles.Sort(StringComparer.OrdinalIgnoreCase);
                        sortedExtensionFileCaches.Add(extension);
                        startupTiming?.RecordListFilesWithExtension(
                            extension,
                            cachedFiles.Count,
                            RoAssetStartupTiming.ElapsedMilliseconds(sortStartTimestamp));
                    }

                    return new List<string>(cachedFiles);
                }
            }

            long startTimestamp = Stopwatch.GetTimestamp();
            if (!availableExtensions.Contains(extension)) {
                var empty = new List<string>();
                lock (extensionFileCacheSync) {
                    if (extensionFileCache.TryGetValue(extension, out List<string> cachedFiles)) {
                        return new List<string>(cachedFiles);
                    }

                    extensionFileCache[extension] = empty;
                    sortedExtensionFileCaches.Add(extension);
                    startupTiming?.RecordListFilesWithExtension(
                        extension,
                        empty.Count,
                        RoAssetStartupTiming.ElapsedMilliseconds(startTimestamp));
                }

                return new List<string>(empty);
            }

            var files = new List<string>();
            foreach (string path in manifest.Keys) {
                if (HasExtension(path, extension)) {
                    files.Add(path);
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            lock (extensionFileCacheSync) {
                if (extensionFileCache.TryGetValue(extension, out List<string> cachedFiles)) {
                    return new List<string>(cachedFiles);
                }

                extensionFileCache[extension] = files;
                sortedExtensionFileCaches.Add(extension);
                startupTiming?.RecordListFilesWithExtension(
                    extension,
                    files.Count,
                    RoAssetStartupTiming.ElapsedMilliseconds(startTimestamp));
            }

            return new List<string>(files);
        }

        public bool TryFindFirstFileWithExtension(string extension, out string path) {
            List<string> files = ListFilesWithExtension(extension);
            if (files.Count > 0) {
                path = files[0];
                return true;
            }

            path = null;
            return false;
        }

        public List<string> ValidateRequiredFiles(IReadOnlyList<string> requiredPaths) {
            EnsureInitialized();
            var missing = new List<string>();
            if (requiredPaths == null) {
                return missing;
            }

            for (int i = 0; i < requiredPaths.Count; i++) {
                string path = requiredPaths[i];
                if (!string.IsNullOrWhiteSpace(path) && !Exists(path)) {
                    missing.Add(path);
                }
            }

            return missing;
        }

        public byte[] ReadBytes(string path) {
            EnsureInitialized();
            RoAssetLookupReport lookup = ResolveLookupCandidates(path);
            if (TryReadBytes(lookup.Candidates, out byte[] data)) {
                return data;
            }

            Diagnostics.RecordMissing(path, "bytes", lookup);
            return null;
        }

        public string ReadText(string path, Encoding encoding = null) {
            byte[] data = ReadBytes(path);
            return data == null ? null : encoding == null ? RoTextEncoding.Decode(data) : encoding.GetString(data);
        }

        /// <summary>
        /// For worker threads (sprite prefetch): like <see cref="ReadBytes"/> / <see cref="Load{T}"/> but a missing file is
        /// not recorded (the diagnostics dictionary is main-thread only; the main-thread load records it later).
        /// </summary>
        public byte[] ReadBytesQuiet(string path) {
            EnsureInitialized();
            return TryReadBytes(ResolveLookupCandidates(path).Candidates, out byte[] data) ? data : null;
        }

        /// <inheritdoc cref="ReadBytesQuiet"/>
        public T LoadQuiet<T>(string path) where T : class {
            EnsureInitialized();
            return LoadObject(path, typeof(T), recordMissing: false) as T;
        }

        public byte[] LoadRawBytes(string path) {
            return ReadBytes(path);
        }

        public byte[] LoadFontBytes(string path) {
            return ReadBytes(path);
        }

        public T Load<T>(string path) where T : class {
            EnsureInitialized();
            object loaded = LoadObject(path, typeof(T));
            return loaded as T;
        }

        public bool TryLoad<T>(string path, out T asset, out string error) where T : class {
            EnsureInitialized();
            if (TryLoadObject(path, typeof(T), out object loaded, out error, recordMissing: true, out _)) {
                asset = loaded as T;
                if (asset != null) {
                    return true;
                }

                error = $"Loaded RO asset '{path}' is not assignable to {typeof(T).Name}.";
            }

            asset = null;
            return false;
        }

        public Texture2D LoadTexture(string path) {
            EnsureInitialized();
            string requestKey = RoAssetPath.Normalize(path);
            if (textureCache.TryGetValue(requestKey, out Texture2D requestCached)) {
                return requestCached;
            }

            if (!TryGetManifestEntry(path, out RoAssetManifestEntry entry, out RoAssetLookupReport lookup)) {
                Diagnostics.RecordMissing(path, nameof(Texture2D), lookup);
                return null;
            }

            if (textureCache.TryGetValue(entry.Path, out Texture2D cached)) {
                textureCache[requestKey] = cached;
                return cached;
            }

            // decoded straight from the archive every session: the fast BMP/TGA decoder (plus native inflate) beats
            // reading a PNG copy back (~0.15 vs ~0.32 ms per effect texture), and no PNG encode on first use
            Texture2D texture = LoadObject(entry.Path, typeof(Texture2D), false) as Texture2D;
            if (texture != null) {
                texture.name = entry.Path;
                textureCache[entry.Path] = texture;
                textureCache[requestKey] = texture;
                return texture;
            }

            Diagnostics.RecordMissing(path, nameof(Texture2D), lookup);
            return null;
        }

        public AudioClip LoadAudioClip(string path) {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(path)) {
                return null;
            }

            // the callers pass the same few path strings (RoAudioEvents normalises each wav name once): looked up as
            // given first, so a played sound costs no normalised copy of its path
            if (rawAudioClipCache.TryGetValue(path, out AudioClip raw)) {
                return raw;
            }

            AudioClip clip = LoadAudioClipNormalized(path);
            rawAudioClipCache[path] = clip;
            return clip;
        }

        private AudioClip LoadAudioClipNormalized(string path) {

            string requestKey = RoAssetPath.Normalize(path);
            if (audioClipCache.TryGetValue(requestKey, out AudioClip requestCached)) {
                return requestCached;   // null for a sound that is missing or does not decode: tried once
            }

            if (!TryGetManifestEntry(path, out RoAssetManifestEntry entry, out RoAssetLookupReport lookup)) {
                Diagnostics.RecordMissing(path, nameof(AudioClip), lookup);
                audioClipCache[requestKey] = null;
                return null;
            }

            if (audioClipCache.TryGetValue(entry.Path, out AudioClip cached)) {
                audioClipCache[requestKey] = cached;
                return cached;
            }

            AudioClip clip = LoadObject(entry.Path, typeof(AudioClip), false) as AudioClip;
            if (clip != null) {
                clip.name = entry.Path;
            }

            audioClipCache[entry.Path] = clip;
            audioClipCache[requestKey] = clip;
            return clip;
        }

        public void ClearTextureCache() {
            textureCache.Clear();
        }

        public void ClearAudioClipCache() {
            audioClipCache.Clear();
            rawAudioClipCache.Clear();
        }

        public void ClearDiagnostics() {
            Diagnostics.Clear();
        }

        public bool ClearDiskCache() {
            EnsureInitialized();
            return diskCache == null || diskCache.Clear();
        }

        public string WriteManifestCache() {
            EnsureInitialized();
            return WriteManifestCacheUnchecked();
        }

        private string WriteManifestCacheUnchecked() {
            if (diskCache == null) {
                throw new InvalidOperationException("RO disk cache has not been initialized.");
            }

            byte[] data = BuildManifestCacheBinary();
            return diskCache.WriteBytes(RoAssetCacheCategory.Manifest, ManifestCacheKey, ".bin", data);
        }

        private bool TryWriteManifestCache() {
            if (diskCache == null) {
                return false;
            }

            try {
                using (ManifestCacheWriteMarker.Auto()) {
                    WriteManifestCacheUnchecked();
                }

                return true;
            } catch (Exception ex) {
                Debug.LogWarning($"Could not write RO asset manifest cache: {ex.Message}");
                return false;
            }
        }

        public string BuildSummary() {
            var builder = new StringBuilder();
            builder.Append("RO asset database: ");
            builder.Append(FileCount);
            builder.Append(" manifest file(s), ");
            builder.Append(SourceCount);
            builder.AppendLine(" source(s).");

            for (int i = 0; i < sources.Count; i++) {
                builder.Append("- ");
                builder.Append(i);
                builder.Append(": ");
                builder.Append(sources[i].Name);
                builder.Append(" (");
                builder.Append(sources[i].Files.Count);
                builder.AppendLine(" file(s))");
            }

            if (diskCache != null) {
                builder.Append("- disk cache: ");
                builder.AppendLine(diskCache.RootPath);
            }

            builder.Append("- resnametable aliases: ");
            builder.AppendLine(resnametableAliases.Count.ToString());

            if (tables != null) {
                builder.Append("- ");
                builder.AppendLine(tables.BuildSummary());
            }

            if (gameData != null) {
                builder.Append("- ");
                builder.AppendLine(gameData.BuildSummary());
            }

            if (localization != null) {
                builder.Append("- ");
                builder.AppendLine(localization.BuildSummary());
            }

            return builder.ToString().TrimEnd();
        }

        public void LogSummary(UnityEngine.Object context = null) {
            Debug.Log(BuildSummary(), context);
        }

        public void LogMissingSummary(UnityEngine.Object context = null, int maxItems = 20) {
            Diagnostics.LogMissingSummary(context, maxItems);
        }

        [Obsolete("Legacy FileManager access is process-global. Add a typed RoAssetDatabase entry point instead.")]
        public T LoadLegacy<T>(string path) where T : class {
            throw new NotSupportedException("Legacy FileManager access is process-global. Add a typed RoAssetDatabase entry point instead.");
        }

        public void Dispose() {
            RoRuntimeMapAssetCache.Clear(this);
            DisposeSources();
            manifest = new Dictionary<string, int>(StringComparer.Ordinal);
            extensionFileCache.Clear();
            availableExtensions.Clear();
            sortedExtensionFileCaches.Clear();
            lookupCache.Clear();
            suffixFallbackCache.Clear();
            containsFallbackCache.Clear();
            textureCache.Clear();
            audioClipCache.Clear();
            rawAudioClipCache.Clear();
            Diagnostics.Clear();
            diskCache = null;
            resnametableAliases = RoResnametableAliasResolver.Empty;
            tables = RoAssetTables.Empty;
            gameData = RoGameDataService.Empty;
            localization = RoLocalizationService.Empty;
            initialized = false;
        }

        private object LoadObject(string path, Type expectedType, bool recordMissing = true) {
            if (TryLoadObject(path, expectedType, out object loaded, out string error, recordMissing, out bool readAttempted)) {
                return loaded;
            }

            if (readAttempted && !string.IsNullOrEmpty(error)) {
                Debug.LogWarning($"Could not load RO asset '{path}' as {expectedType.Name}: {error}");
            }

            return null;
        }

        private static readonly Unity.Profiling.ProfilerMarker BuildLookupMarker = new Unity.Profiling.ProfilerMarker("RO.Asset.BuildLookup");
        private static readonly Unity.Profiling.ProfilerMarker LookupMarker = new Unity.Profiling.ProfilerMarker("RO.Asset.Lookup");

        private bool TryLoadObject(
            string path,
            Type expectedType,
            out object loaded,
            out string error,
            bool recordMissing,
            out bool readAttempted) {
            loaded = null;
            error = string.Empty;
            readAttempted = false;
            RoAssetLookupReport lookup;
            using (LookupMarker.Auto()) {
                lookup = ResolveLookupCandidates(path);
            }

            if (!TryReadBytes(lookup.Candidates, out byte[] data)) {
                if (recordMissing) {
                    Diagnostics.RecordMissing(path, expectedType.Name, lookup);
                }

                error = $"RO asset '{path}' could not be resolved.";
                return false;
            }

            readAttempted = true;
            string resolvedPath = lookup.ResolvedPath ?? path;
            string extension = Path.GetExtension(resolvedPath).TrimStart('.').ToLowerInvariant();
            using var reader = new MemoryStreamReader(data);

            try {
                if (expectedType == typeof(byte[])) {
                    loaded = data;
                    return true;
                }

                switch (extension) {
                    case "jpg":
                    case "jpeg":
                    case "png":
                        loaded = LoadTextureFromEncodedImage(resolvedPath, data);
                        return true;
                    case "bmp":
                        loaded = RoFastImageTexture.TryCreate(data, tga: false) ?? bmpLoader.LoadBMP(reader).ToTexture2D();
                        return true;
                    case "tga":
                        loaded = RoFastImageTexture.TryCreate(data, tga: true) ?? TGALoader.LoadTGA(reader).ToTexture2D();
                        return true;
                    case "txt":
                    case "xml":
                    case "lua":
                        loaded = RoTextEncoding.Decode(data);
                        return true;
                    case "spr":
                        if (!SpriteLoader.TryLoad(reader, out SPR spr, out error)) {
                            return false;
                        }

                        spr.filename = resolvedPath;
                        loaded = spr;
                        return true;
                    case "str":
                        loaded = LoadEffect(resolvedPath, reader);
                        return true;
                    case "act":
                        if (!ActionLoader.TryLoad(reader, out ACT act, out error)) {
                            return false;
                        }

                        loaded = act;
                        return true;
                    case "gat":
                        if (!AltitudeLoader.TryLoad(reader, out GAT gat, out error)) {
                            return false;
                        }

                        loaded = gat;
                        return true;
                    case "rsw":
                        if (!WorldLoader.TryLoad(reader, resolvedPath, out RSW rsw, out error)) {
                            return false;
                        }

                        loaded = rsw;
                        return true;
                    case "gnd":
                        if (!GroundLoader.TryLoad(reader, out GND gnd, out error)) {
                            return false;
                        }

                        loaded = gnd;
                        return true;
                    case "rsm":
                        if (!ModelLoader.TryLoad(reader, out RSM rsm, out error)) {
                            return false;
                        }

                        loaded = rsm;
                        return true;
                    case Rsm2ModelLoader.Extension:
                        if (!Rsm2ModelLoader.TryLoad(reader, out RSM rsm2, out error)) {
                            return false;
                        }

                        loaded = rsm2;
                        return true;
                    case "wav":
                        if (!RoWavDecoder.TryDecode(data, out RoPcm pcm, out error)) {
                            return false;
                        }

                        AudioClip clip = AudioClip.Create(resolvedPath, Math.Max(1, pcm.Frames), pcm.Channels, pcm.SampleRate, false);
                        if (pcm.Samples.Length > 0) {
                            clip.SetData(pcm.Samples, 0);
                        }

                        loaded = clip;
                        return true;
                    case "json":
                        loaded = JObject.Parse(Encoding.UTF8.GetString(data));
                        return true;
                    case "pal":
                    case "mp3":
                    case "ogg":
                    case "lub":
                    case "fnt":
                    case "ttf":
                    case "otf":
                        loaded = data;
                        return true;
                    default:
                        error = $"Unsupported RO asset format '{extension}' for '{path}'.";
                        return false;
                }
            } catch (Exception ex) {
                error = ex.Message;
                return false;
            }
        }

        private bool TryLoadManifestCache() {
            if (diskCache == null) {
                return false;
            }

            try {
                using (ManifestCacheReadMarker.Auto()) {
                    if (!diskCache.TryReadBytes(
                            RoAssetCacheCategory.Manifest,
                            ManifestCacheKey,
                            ".bin",
                            out byte[] data)) {
                        return false;
                    }

                    if (!TryReadManifestCacheBinary(data, out Dictionary<string, int> cachedManifest, out HashSet<string> cachedExtensions)) {
                        return false;
                    }

                    manifest = cachedManifest;
                    extensionFileCache = new Dictionary<string, List<string>>(64, StringComparer.Ordinal);
                    availableExtensions.Clear();
                    foreach (string extension in cachedExtensions) {
                        availableExtensions.Add(extension);
                    }

                    sortedExtensionFileCaches.Clear();
                    LoadedManifestFromCache = true;
                    TryStoreManifestMemoryCache();
                    return true;
                }
            } catch {
                return false;
            }
        }

        private bool TryLoadManifestMemoryCache() {
            ManifestMemoryCacheSnapshot snapshot;
            lock (ManifestMemoryCacheSync) {
                snapshot = manifestMemoryCache;
            }

            if (!IsManifestMemoryCacheValid(snapshot)) {
                return false;
            }

            manifest = snapshot.Manifest;
            extensionFileCache = new Dictionary<string, List<string>>(64, StringComparer.Ordinal);
            availableExtensions.Clear();
            foreach (string extension in snapshot.AvailableExtensions) {
                availableExtensions.Add(extension);
            }

            sortedExtensionFileCaches.Clear();
            LoadedManifestFromCache = true;
            return true;
        }

        private bool IsManifestMemoryCacheValid(ManifestMemoryCacheSnapshot snapshot) {
            if (snapshot == null ||
                diskCache == null ||
                !string.Equals(snapshot.ProfileHash, diskCache.ProfileHash, StringComparison.Ordinal) ||
                snapshot.SourceNames.Length != sources.Count ||
                snapshot.SourceFileCounts.Length != sources.Count) {
                return false;
            }

            for (int i = 0; i < sources.Count; i++) {
                if (!string.Equals(snapshot.SourceNames[i], sources[i].Name, StringComparison.Ordinal) ||
                    snapshot.SourceFileCounts[i] != sources[i].Files.Count) {
                    return false;
                }
            }

            return true;
        }

        private void TryStoreManifestMemoryCache() {
            if (diskCache == null || manifest == null || manifest.Count == 0) {
                return;
            }

            var sourceNames = new string[sources.Count];
            var sourceFileCounts = new int[sources.Count];
            for (int i = 0; i < sources.Count; i++) {
                sourceNames[i] = sources[i].Name;
                sourceFileCounts[i] = sources[i].Files.Count;
            }

            var snapshot = new ManifestMemoryCacheSnapshot(
                diskCache.ProfileHash,
                string.Empty,
                0,
                0,
                manifest,
                new HashSet<string>(availableExtensions, StringComparer.Ordinal),
                sourceNames,
                sourceFileCounts);

            lock (ManifestMemoryCacheSync) {
                manifestMemoryCache = snapshot;
            }
        }

        private bool TryGetManifestCacheFileState(out string cachePath, out long cacheLength, out long cacheLastWriteUtcTicks) {
            cachePath = null;
            cacheLength = 0;
            cacheLastWriteUtcTicks = 0;
            if (diskCache == null) {
                return false;
            }

            try {
                cachePath = diskCache.GetPath(RoAssetCacheCategory.Manifest, ManifestCacheKey, ".bin");
                var info = new FileInfo(cachePath);
                if (!info.Exists) {
                    return false;
                }

                cacheLength = info.Length;
                cacheLastWriteUtcTicks = info.LastWriteTimeUtc.Ticks;
                return true;
            } catch {
                cachePath = null;
                cacheLength = 0;
                cacheLastWriteUtcTicks = 0;
                return false;
            }
        }

        private byte[] BuildManifestCacheBinary() {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                writer.Write(ManifestCacheMagic);
                writer.Write(ManifestCacheVersion);
                WriteManifestCacheString(writer, diskCache?.ProfileHash);
                writer.Write(RoAssetDiskCache.CacheFormatVersion);
                writer.Write(sources.Count);
                for (int i = 0; i < sources.Count; i++) {
                    writer.Write(i);
                    WriteManifestCacheString(writer, sources[i].Name);
                    writer.Write(sources[i].Files.Count);
                }

                writer.Write(manifest.Count);
                foreach (KeyValuePair<string, int> file in manifest) {
                    WriteManifestCacheString(writer, file.Key);
                    writer.Write(file.Value);
                }
            }

            return stream.ToArray();
        }

        private bool TryReadManifestCacheBinary(
            byte[] data,
            out Dictionary<string, int> cachedManifest,
            out HashSet<string> cachedExtensions) {
            cachedManifest = null;
            cachedExtensions = null;
            if (data == null || data.Length == 0 || diskCache == null) {
                return false;
            }

            var reader = new ManifestCacheByteReader(data);
            if (reader.ReadInt32() != ManifestCacheMagic ||
                reader.ReadInt32() != ManifestCacheVersion ||
                !string.Equals(reader.ReadString(), diskCache.ProfileHash, StringComparison.Ordinal) ||
                reader.ReadInt32() != RoAssetDiskCache.CacheFormatVersion) {
                return false;
            }

            int sourceCount = reader.ReadInt32();
            if (sourceCount != sources.Count) {
                return false;
            }

            for (int i = 0; i < sources.Count; i++) {
                int sourceIndex = reader.ReadInt32();
                string sourceName = reader.ReadString();
                int sourceFileCount = reader.ReadInt32();
                if (sourceIndex != i ||
                    sourceFileCount != sources[i].Files.Count ||
                    !string.Equals(sourceName, sources[i].Name, StringComparison.Ordinal)) {
                    return false;
                }
            }

            int expectedFileCount = reader.ReadInt32();
            if (expectedFileCount < 0 || expectedFileCount > TotalSourceFileCount()) {
                return false;
            }

            var loadedManifest = expectedFileCount > 0
                ? new Dictionary<string, int>(expectedFileCount, StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal);
            var loadedExtensions = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < expectedFileCount; i++) {
                string path = reader.ReadString();
                int sourceIndex = reader.ReadInt32();
                if (string.IsNullOrWhiteSpace(path) ||
                    sourceIndex < 0 ||
                    sourceIndex >= sources.Count ||
                    loadedManifest.ContainsKey(path)) {
                    return false;
                }

                loadedManifest.Add(path, sourceIndex);
                AddExtensionAvailability(loadedExtensions, path);
            }

            if (loadedManifest.Count != expectedFileCount || !reader.IsAtEnd) {
                return false;
            }

            cachedManifest = loadedManifest;
            cachedExtensions = loadedExtensions;
            return true;
        }

        private static void WriteManifestCacheString(BinaryWriter writer, string value) {
            if (string.IsNullOrEmpty(value)) {
                writer.Write(0);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private sealed class ManifestMemoryCacheSnapshot {
            public ManifestMemoryCacheSnapshot(
                string profileHash,
                string cachePath,
                long cacheLength,
                long cacheLastWriteUtcTicks,
                Dictionary<string, int> manifest,
                HashSet<string> availableExtensions,
                string[] sourceNames,
                int[] sourceFileCounts) {
                ProfileHash = profileHash ?? string.Empty;
                CachePath = cachePath ?? string.Empty;
                CacheLength = cacheLength;
                CacheLastWriteUtcTicks = cacheLastWriteUtcTicks;
                Manifest = manifest ?? new Dictionary<string, int>(StringComparer.Ordinal);
                AvailableExtensions = availableExtensions ?? new HashSet<string>(StringComparer.Ordinal);
                SourceNames = sourceNames ?? Array.Empty<string>();
                SourceFileCounts = sourceFileCounts ?? Array.Empty<int>();
            }

            public string ProfileHash { get; }
            public string CachePath { get; }
            public long CacheLength { get; }
            public long CacheLastWriteUtcTicks { get; }
            public Dictionary<string, int> Manifest { get; }
            public HashSet<string> AvailableExtensions { get; }
            public string[] SourceNames { get; }
            public int[] SourceFileCounts { get; }
        }

        private sealed class StartupDataMemoryCacheSnapshot {
            public StartupDataMemoryCacheSnapshot(string profileHash) {
                ProfileHash = profileHash ?? string.Empty;
            }

            public string ProfileHash { get; }
            public RoResnametableAliasResolver Aliases { get; set; }
            public RoAssetTables Tables { get; set; }
            public RoLocalizationService Localization { get; set; }
            public RoAssetTables LocalizationTables { get; set; }
            public RoItemMetadataTable LocalizationItems { get; set; }
            public RoSkillMetadataTable LocalizationSkills { get; set; }
            public RoEntityDisplayMetadataTable LocalizationEntities { get; set; }
        }

        private struct ManifestCacheByteReader {
            private readonly byte[] data;
            private int offset;

            public ManifestCacheByteReader(byte[] data) {
                this.data = data ?? throw new ArgumentNullException(nameof(data));
                offset = 0;
            }

            public bool IsAtEnd => offset == data.Length;

            public int ReadInt32() {
                EnsureAvailable(4);
                int value = data[offset] |
                    (data[offset + 1] << 8) |
                    (data[offset + 2] << 16) |
                    (data[offset + 3] << 24);
                offset += 4;
                return value;
            }

            public string ReadString() {
                int byteCount = ReadInt32();
                if (byteCount < 0) {
                    throw new InvalidDataException("Cached manifest string length is invalid.");
                }

                EnsureAvailable(byteCount);
                string value = byteCount == 0
                    ? string.Empty
                    : Encoding.UTF8.GetString(data, offset, byteCount);
                offset += byteCount;
                return value;
            }

            private void EnsureAvailable(int byteCount) {
                if (byteCount < 0 || offset > data.Length - byteCount) {
                    throw new EndOfStreamException();
                }
            }
        }

        private int TotalSourceFileCount() {
            int count = 0;
            for (int i = 0; i < sources.Count; i++) {
                count += sources[i].Files.Count;
            }

            return count;
        }

        private bool TryGetManifestEntry(string path, out RoAssetManifestEntry entry, out RoAssetLookupReport lookup) {
            lookup = ResolveLookupCandidates(path);
            if (!string.IsNullOrWhiteSpace(lookup.ResolvedPath) &&
                manifest.TryGetValue(lookup.ResolvedPath, out int sourceIndex)) {
                entry = CreateManifestEntry(lookup.ResolvedPath, sourceIndex);
                return true;
            }

            entry = null;
            return false;
        }

        private RoAssetManifestEntry CreateManifestEntry(string path, int sourceIndex) {
            return new RoAssetManifestEntry(path, GetSourceName(sourceIndex), sourceIndex);
        }

        private string GetSourceName(int sourceIndex) {
            return sourceIndex >= 0 && sourceIndex < sources.Count
                ? sources[sourceIndex].Name
                : string.Empty;
        }

        private RoAssetLookupReport BuildLookupReport(string path) {
            var direct = new List<string>(RoAssetPath.GetLookupCandidates(path));
            var legacyKorean = new List<string>();
            for (int i = 0; i < direct.Count; i++) {
                if (!string.IsNullOrEmpty(RoAssetPath.TryDecodeLegacyBytesAsKorean(direct[i]))) {
                    AddCandidate(legacyKorean, direct[i]);
                }
            }

            string resolvedPath = FindFirstManifestPath(direct);
            if (resolvedPath != null) {
                return new RoAssetLookupReport(
                    path,
                    direct,
                    Array.Empty<string>(),
                    legacyKorean,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    resolvedPath,
                    RoAssetLookupOutcome.Direct);
            }

            var aliases = new List<string>(resnametableAliases.ResolveAliases(direct));
            for (int i = 0; i < aliases.Count; i++) {
                IReadOnlyList<string> aliasCandidates = RoAssetPath.GetLookupCandidates(aliases[i]);
                for (int c = 0; c < aliasCandidates.Count; c++) {
                    AddCandidate(aliases, aliasCandidates[c]);
                    if (!string.IsNullOrEmpty(RoAssetPath.TryDecodeLegacyBytesAsKorean(aliasCandidates[c]))) {
                        AddCandidate(legacyKorean, aliasCandidates[c]);
                    }
                }
            }

            resolvedPath = FindFirstManifestPath(aliases);
            if (resolvedPath != null) {
                return new RoAssetLookupReport(
                    path,
                    direct,
                    aliases,
                    legacyKorean,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    resolvedPath,
                    RoAssetLookupOutcome.Alias);
            }

            var fallbackCandidates = new List<string>();
            var fallbackAttempts = new List<string>();
            RoAssetLookupOutcome fallbackOutcome = RoAssetLookupOutcome.NotFound;
            foreach (string candidate in EnumerateDirectAndAliasCandidates(direct, aliases)) {
                if (!ShouldUseAutomaticFallback(candidate)) {
                    continue;
                }

                FallbackLookupResult suffix = FindUniquePathEndingWith(candidate);
                AddFallbackAttempt(fallbackAttempts, "suffix", candidate, suffix);
                if (suffix.IsAmbiguous) {
                    fallbackOutcome = RoAssetLookupOutcome.AmbiguousFallback;
                } else if (suffix.IsResolved) {
                    AddCandidate(fallbackCandidates, suffix.ResolvedPath);
                    return new RoAssetLookupReport(
                        path,
                        direct,
                        aliases,
                        legacyKorean,
                        fallbackCandidates,
                        fallbackAttempts,
                        suffix.ResolvedPath,
                        RoAssetLookupOutcome.SuffixFallback);
                }

                if (TrySplitContainsFallback(candidate, out string contains, out string requiredEnding)) {
                    FallbackLookupResult containsResult = FindUniquePathContaining(contains, requiredEnding);
                    AddFallbackAttempt(fallbackAttempts, "contains", $"{contains}|{requiredEnding}", containsResult);
                    if (containsResult.IsAmbiguous) {
                        fallbackOutcome = RoAssetLookupOutcome.AmbiguousFallback;
                    } else if (containsResult.IsResolved) {
                        AddCandidate(fallbackCandidates, containsResult.ResolvedPath);
                        return new RoAssetLookupReport(
                            path,
                            direct,
                            aliases,
                            legacyKorean,
                            fallbackCandidates,
                            fallbackAttempts,
                            containsResult.ResolvedPath,
                            RoAssetLookupOutcome.ContainsFallback);
                    }
                }
            }

            return new RoAssetLookupReport(
                path,
                direct,
                aliases,
                legacyKorean,
                fallbackCandidates,
                fallbackAttempts,
                resolvedPath: null,
                fallbackOutcome);
        }

        private RoResnametableAliasResolver LoadResnametableAliases() {
            IReadOnlyList<string> candidates = RoAssetPath.GetLookupCandidates("data/resnametable.txt");
            return TryReadBytes(candidates, out byte[] data)
                ? RoResnametableAliasResolver.Parse(RoTextEncoding.Decode(data))
                : RoResnametableAliasResolver.Empty;
        }

        private bool TryLoadCachedResnametableAliases(out RoResnametableAliasResolver cachedAliases) {
            cachedAliases = null;
            StartupDataMemoryCacheSnapshot snapshot = GetStartupDataMemoryCacheSnapshot();
            if (snapshot?.Aliases == null) {
                return false;
            }

            cachedAliases = snapshot.Aliases;
            return true;
        }

        private void StoreCachedResnametableAliases(RoResnametableAliasResolver aliases) {
            if (aliases == null) {
                return;
            }

            UpdateStartupDataMemoryCache(snapshot => snapshot.Aliases = aliases);
        }

        private bool TryLoadCachedTables(out RoAssetTables cachedTables) {
            cachedTables = null;
            StartupDataMemoryCacheSnapshot snapshot = GetStartupDataMemoryCacheSnapshot();
            if (snapshot?.Tables == null) {
                return false;
            }

            cachedTables = snapshot.Tables;
            return true;
        }

        private void StoreCachedTables(RoAssetTables tablesToCache) {
            if (tablesToCache == null) {
                return;
            }

            UpdateStartupDataMemoryCache(snapshot => snapshot.Tables = tablesToCache);
        }

        private bool TryLoadCachedLocalization(out RoLocalizationService cachedLocalization) {
            cachedLocalization = null;
            StartupDataMemoryCacheSnapshot snapshot = GetStartupDataMemoryCacheSnapshot();
            if (snapshot?.Localization == null ||
                !ReferenceEquals(snapshot.LocalizationTables, tables) ||
                !ReferenceEquals(snapshot.LocalizationItems, gameData?.Items) ||
                !ReferenceEquals(snapshot.LocalizationSkills, gameData?.Skills) ||
                !ReferenceEquals(snapshot.LocalizationEntities, gameData?.EntityDisplay)) {
                return false;
            }

            cachedLocalization = snapshot.Localization;
            return true;
        }

        private void StoreCachedLocalization(RoLocalizationService localizationToCache) {
            if (localizationToCache == null) {
                return;
            }

            RoAssetTables currentTables = tables;
            RoItemMetadataTable currentItems = gameData?.Items;
            RoSkillMetadataTable currentSkills = gameData?.Skills;
            RoEntityDisplayMetadataTable currentEntities = gameData?.EntityDisplay;
            UpdateStartupDataMemoryCache(snapshot => {
                snapshot.Localization = localizationToCache;
                snapshot.LocalizationTables = currentTables;
                snapshot.LocalizationItems = currentItems;
                snapshot.LocalizationSkills = currentSkills;
                snapshot.LocalizationEntities = currentEntities;
            });
        }

        private StartupDataMemoryCacheSnapshot GetStartupDataMemoryCacheSnapshot() {
            string profileHash = diskCache?.ProfileHash;
            if (string.IsNullOrWhiteSpace(profileHash)) {
                return null;
            }

            lock (StartupDataMemoryCacheSync) {
                StartupDataMemoryCaches.TryGetValue(profileHash, out StartupDataMemoryCacheSnapshot snapshot);
                return snapshot;
            }
        }

        private void UpdateStartupDataMemoryCache(Action<StartupDataMemoryCacheSnapshot> update) {
            string profileHash = diskCache?.ProfileHash;
            if (string.IsNullOrWhiteSpace(profileHash) || update == null) {
                return;
            }

            lock (StartupDataMemoryCacheSync) {
                if (!StartupDataMemoryCaches.TryGetValue(profileHash, out StartupDataMemoryCacheSnapshot snapshot)) {
                    if (StartupDataMemoryCaches.Count >= MaxStartupDataMemoryCacheProfiles) {
                        StartupDataMemoryCaches.Clear();
                    }

                    snapshot = new StartupDataMemoryCacheSnapshot(profileHash);
                    StartupDataMemoryCaches[profileHash] = snapshot;
                }

                update(snapshot);
            }
        }

        private string FindFirstManifestPath(IReadOnlyList<string> candidates) {
            if (candidates == null) {
                return null;
            }

            for (int i = 0; i < candidates.Count; i++) {
                if (manifest.ContainsKey(candidates[i])) {
                    return candidates[i];
                }
            }

            return null;
        }

        private static bool ShouldUseAutomaticFallback(string candidate) {
            if (string.IsNullOrWhiteSpace(candidate)) {
                return false;
            }

            string normalized = RoAssetPath.Normalize(candidate);
            return !normalized.StartsWith("data/", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(Path.GetExtension(normalized));
        }

        private FallbackLookupResult FindUniquePathEndingWith(string suffix) {
            string normalizedSuffix = NormalizeSearchFragment(suffix);
            if (normalizedSuffix.Length == 0) {
                return FallbackLookupResult.Miss;
            }

            lock (fallbackCacheSync) {
                if (suffixFallbackCache.TryGetValue(normalizedSuffix, out FallbackLookupResult cached)) {
                    return cached;
                }
            }

            long startTimestamp = Stopwatch.GetTimestamp();
            FallbackLookupResult result = FindUniquePath(
                GetFallbackSearchScope(Path.GetExtension(normalizedSuffix)),
                path => path.EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase));
            startupTiming?.RecordSuffixFallbackScan(
                normalizedSuffix,
                result.IsResolved,
                result.IsAmbiguous,
                result.Matches.Count,
                RoAssetStartupTiming.ElapsedMilliseconds(startTimestamp));
            lock (fallbackCacheSync) {
                if (suffixFallbackCache.TryGetValue(normalizedSuffix, out FallbackLookupResult cached)) {
                    return cached;
                }

                suffixFallbackCache[normalizedSuffix] = result;
            }

            return result;
        }

        private FallbackLookupResult FindUniquePathContaining(string contains, string requiredEnding) {
            string normalizedContains = NormalizeSearchFragment(contains);
            string normalizedEnding = NormalizeSearchFragment(requiredEnding);
            if (normalizedContains.Length == 0 || normalizedEnding.Length == 0) {
                return FallbackLookupResult.Miss;
            }

            string cacheKey = normalizedContains + "|" + normalizedEnding;
            lock (fallbackCacheSync) {
                if (containsFallbackCache.TryGetValue(cacheKey, out FallbackLookupResult cached)) {
                    return cached;
                }
            }

            long startTimestamp = Stopwatch.GetTimestamp();
            FallbackLookupResult result = FindUniquePath(
                GetFallbackSearchScope(normalizedEnding),
                path => path.EndsWith(normalizedEnding, StringComparison.OrdinalIgnoreCase) &&
                    path.IndexOf(normalizedContains, StringComparison.OrdinalIgnoreCase) >= 0);
            startupTiming?.RecordContainsFallbackScan(
                normalizedContains,
                normalizedEnding,
                result.IsResolved,
                result.IsAmbiguous,
                result.Matches.Count,
                RoAssetStartupTiming.ElapsedMilliseconds(startTimestamp));
            lock (fallbackCacheSync) {
                if (containsFallbackCache.TryGetValue(cacheKey, out FallbackLookupResult cached)) {
                    return cached;
                }

                containsFallbackCache[cacheKey] = result;
            }

            return result;
        }

        private IEnumerable<string> GetFallbackSearchScope(string extensionOrEnding) {
            if (string.IsNullOrWhiteSpace(extensionOrEnding)) {
                return manifest.Keys;
            }

            string extension = extensionOrEnding.TrimStart('.');
            return string.IsNullOrWhiteSpace(extension)
                ? (IEnumerable<string>) manifest.Keys
                : ListFilesWithExtension(extension);
        }

        private FallbackLookupResult FindUniquePath(IEnumerable<string> paths, Predicate<string> predicate) {
            var matches = new List<string>();
            foreach (string path in paths) {
                if (predicate(path)) {
                    matches.Add(path);
                }
            }

            if (matches.Count == 0) {
                return FallbackLookupResult.Miss;
            }

            matches.Sort(StringComparer.OrdinalIgnoreCase);
            return matches.Count == 1
                ? FallbackLookupResult.Resolved(matches[0])
                : FallbackLookupResult.Ambiguous(matches.ToArray());
        }

        private static IEnumerable<string> EnumerateDirectAndAliasCandidates(
            IReadOnlyList<string> direct,
            IReadOnlyList<string> aliases) {
            if (direct != null) {
                for (int i = 0; i < direct.Count; i++) {
                    yield return direct[i];
                }
            }

            if (aliases != null) {
                for (int i = 0; i < aliases.Count; i++) {
                    yield return aliases[i];
                }
            }
        }

        private static bool TrySplitContainsFallback(
            string path,
            out string contains,
            out string requiredEnding) {
            contains = null;
            requiredEnding = null;
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }

            string normalized = RoAssetPath.Normalize(path);
            int slash = normalized.LastIndexOf('/');
            string fileName = slash >= 0 ? normalized.Substring(slash + 1) : normalized;
            string extension = Path.GetExtension(fileName);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(stem)) {
                return false;
            }

            contains = stem;
            requiredEnding = extension;
            return true;
        }

        private static string NormalizeSearchFragment(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            return RoAssetPath.Normalize(path);
        }

        private static void AddFallbackAttempt(
            List<string> attempts,
            string kind,
            string query,
            FallbackLookupResult result) {
            if (attempts == null) {
                return;
            }

            string status = result.IsResolved
                ? "resolved:" + result.ResolvedPath
                : result.IsAmbiguous ? "ambiguous:" + string.Join(",", result.Matches) : "miss";
            AddCandidate(attempts, $"{kind}({query})={status}");
        }

        private static void AddCandidate(List<string> candidates, string candidate) {
            if (!string.IsNullOrWhiteSpace(candidate) && !candidates.Contains(candidate)) {
                candidates.Add(candidate);
            }
        }

        private static List<string> ResolveGrfFiles(string fullRootPath, IReadOnlyList<string> grfFiles) {
            var resolved = new List<string>(grfFiles.Count);
            foreach (string grfFile in grfFiles) {
                if (string.IsNullOrWhiteSpace(grfFile)) {
                    continue;
                }

                string fullGrfPath = RoAssetPath.ResolveContentFile(fullRootPath, grfFile);
                if (!File.Exists(fullGrfPath)) {
                    throw new FileNotFoundException($"Configured GRF file does not exist: {fullGrfPath}", fullGrfPath);
                }

                resolved.Add(fullGrfPath);
            }

            return resolved;
        }

        private bool TryReadBytes(IReadOnlyList<string> candidates, out byte[] data) {
            for (int c = 0; c < candidates.Count; c++) {
                string candidate = candidates[c];
                for (int i = 0; i < sources.Count; i++) {
                    using (RoProfilerMarkers.GrfLoad.Auto()) {
                        data = sources[i].ReadBytes(candidate);
                    }

                    if (data != null) {
                        RoRuntimePerfCounters.RecordAssetLoad(candidate, data.Length);
                        return true;
                    }
                }
            }

            data = null;
            return false;
        }

        private static Texture2D LoadTextureFromEncodedImage(string path, byte[] data) {
            // linear: false preserves encoded image payloads (PNG/JPG/WebP) as sRGB.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear: false) {
                name = path
            };

            if (ImageConversion.LoadImage(texture, data, false)) {
                return texture;
            }

            UnityEngine.Object.Destroy(texture);
            return null;
        }

        private STR LoadEffect(string path, MemoryStreamReader reader) {
            string directory = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? string.Empty;
            STR effect = EffectLoader.Load(reader, directory, LoadTexture);
            effect.name = path;
            return effect;
        }

        private void AddLooseSource(string fullRootPath) {
            if (!Directory.Exists(fullRootPath)) {
                return;
            }

            var source = new LooseFileAssetSource(fullRootPath);
            if (source.Files.Count > 0) {
                sources.Add(source);
            }
        }

        private void AddGrfSources(IReadOnlyList<string> grfFiles, Action<string> grfIndexRebuildStarted) {
            if (grfFiles == null || grfFiles.Count == 0) {
                return;
            }

            if (grfFiles.Count == 1) {
                sources.Add(new GrfAssetSource(grfFiles[0], diskCache, 0, grfIndexRebuildStarted));
                return;
            }

            var tasks = new Task<GrfAssetSource>[grfFiles.Count];
            try {
                for (int i = 0; i < grfFiles.Count; i++) {
                    int sourceOrder = i;
                    string grfPath = grfFiles[i];
                    tasks[i] = Task.Run(() => new GrfAssetSource(grfPath, diskCache, sourceOrder, grfIndexRebuildStarted));
                }

                Task.WaitAll(tasks);
            } catch (AggregateException ex) {
                DisposeCompletedGrfSources(tasks);
                ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions[0]).Throw();
                throw;
            } catch {
                DisposeCompletedGrfSources(tasks);
                throw;
            }

            for (int i = 0; i < tasks.Length; i++) {
                sources.Add(tasks[i].Result);
            }
        }

        private static void DisposeCompletedGrfSources(IReadOnlyList<Task<GrfAssetSource>> tasks) {
            if (tasks == null) {
                return;
            }

            for (int i = 0; i < tasks.Count; i++) {
                Task<GrfAssetSource> task = tasks[i];
                if (task == null || task.Status != TaskStatus.RanToCompletion) {
                    continue;
                }

                try {
                    task.Result?.Dispose();
                } catch {
                    // Keep the original GRF load failure as the observable error.
                }
            }
        }

        private void BuildManifest() {
            int estimatedFileCount = EstimateSourceFileCount();
            manifest = estimatedFileCount > 0
                ? new Dictionary<string, int>(estimatedFileCount, StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal);
            extensionFileCache = new Dictionary<string, List<string>>(64, StringComparer.Ordinal);
            availableExtensions.Clear();
            sortedExtensionFileCaches.Clear();

            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++) {
                IRoAssetSource source = sources[sourceIndex];
                foreach (string file in source.Files) {
                    if (!manifest.ContainsKey(file)) {
                        manifest.Add(file, sourceIndex);
                        AddExtensionAvailability(file);
                    }
                }
            }
        }

        private int EstimateSourceFileCount() {
            long total = 0;
            for (int i = 0; i < sources.Count; i++) {
                total += sources[i].Files?.Count ?? 0;
                if (total >= int.MaxValue) {
                    return int.MaxValue;
                }
            }

            return (int) total;
        }

        private void AddExtensionAvailability(string path) {
            AddExtensionAvailability(availableExtensions, path);
        }

        private static void AddExtensionAvailability(HashSet<string> extensions, string path) {
            string extension = GetPathExtensionForCache(path);
            if (string.IsNullOrEmpty(extension)) {
                return;
            }

            extensions.Add(extension);
        }

        private static string GetPathExtensionForCache(string path) {
            if (string.IsNullOrEmpty(path)) {
                return string.Empty;
            }

            int lastSlash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            int dot = path.LastIndexOf('.');
            if (dot <= lastSlash || dot >= path.Length - 1) {
                return string.Empty;
            }

            return GetCommonExtension(path, dot + 1) ?? path.Substring(dot + 1);
        }

        private static bool HasExtension(string path, string extension) {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(extension)) {
                return false;
            }

            int extensionStart = path.Length - extension.Length;
            return extensionStart > 0 &&
                path[extensionStart - 1] == '.' &&
                string.Compare(path, extensionStart, extension, 0, extension.Length, StringComparison.Ordinal) == 0;
        }

        private static string GetCommonExtension(string path, int extensionStart) {
            int length = path.Length - extensionStart;
            if (length == 3) {
                char a = path[extensionStart];
                char b = path[extensionStart + 1];
                char c = path[extensionStart + 2];
                if (a == 'a' && c == 't') {
                    return b == 'c' ? "act" : b == 'g' ? "agt" : null;
                }

                if (a == 'b' && b == 'm' && c == 'p') {
                    return "bmp";
                }

                if (a == 'f' && b == 'n' && c == 't') {
                    return "fnt";
                }

                if (a == 'g') {
                    if (b == 'a' && c == 't') {
                        return "gat";
                    }

                    if (b == 'n' && c == 'd') {
                        return "gnd";
                    }

                    if (b == 'r' && c == 'f') {
                        return "grf";
                    }
                }

                if (a == 'j' && b == 'p' && c == 'g') {
                    return "jpg";
                }

                if (a == 'l' && b == 'u') {
                    return c == 'a' ? "lua" : c == 'b' ? "lub" : null;
                }

                if (a == 'm' && b == 'p' && c == '3') {
                    return "mp3";
                }

                if (a == 'o' && b == 't' && c == 'f') {
                    return "otf";
                }

                if (a == 'p' && b == 'n' && c == 'g') {
                    return "png";
                }

                if (a == 'r' && b == 's' && c == 'w') {
                    return "rsw";
                }

                if (a == 's') {
                    if (b == 'p' && c == 'r') {
                        return "spr";
                    }

                    if (b == 't' && c == 'r') {
                        return "str";
                    }
                }

                if (a == 't') {
                    if (b == 'g' && c == 'a') {
                        return "tga";
                    }

                    if (b == 't' && c == 'f') {
                        return "ttf";
                    }

                    if (b == 'x' && c == 't') {
                        return "txt";
                    }
                }

                if (a == 'w' && b == 'a' && c == 'v') {
                    return "wav";
                }

                if (a == 'x' && b == 'm' && c == 'l') {
                    return "xml";
                }

                if (a == 'y' && b == 'm' && c == 'l') {
                    return "yml";
                }

                return null;
            }

            if (length == 4) {
                if (path[extensionStart] == 'j' &&
                    path[extensionStart + 1] == 's' &&
                    path[extensionStart + 2] == 'o' &&
                    path[extensionStart + 3] == 'n') {
                    return "json";
                }

                if (path[extensionStart] == 'y' &&
                    path[extensionStart + 1] == 'a' &&
                    path[extensionStart + 2] == 'm' &&
                    path[extensionStart + 3] == 'l') {
                    return "yaml";
                }
            }

            return null;
        }

        private void EnsureInitialized() {
            if (!initialized) {
                throw new InvalidOperationException("RO asset database has not been initialized.");
            }
        }

        private void DisposeSources() {
            foreach (IRoAssetSource source in sources) {
                source.Dispose();
            }

            sources.Clear();
        }

        private readonly struct FallbackLookupResult {
            public static readonly FallbackLookupResult Miss =
                new FallbackLookupResult(null, Array.Empty<string>(), false);

            private FallbackLookupResult(string resolvedPath, IReadOnlyList<string> matches, bool isAmbiguous) {
                ResolvedPath = resolvedPath;
                Matches = matches ?? Array.Empty<string>();
                IsAmbiguous = isAmbiguous;
            }

            public string ResolvedPath { get; }
            public IReadOnlyList<string> Matches { get; }
            public bool IsResolved => !string.IsNullOrWhiteSpace(ResolvedPath);
            public bool IsAmbiguous { get; }

            public static FallbackLookupResult Resolved(string path) {
                return new FallbackLookupResult(path, new[] { path }, false);
            }

            public static FallbackLookupResult Ambiguous(IReadOnlyList<string> matches) {
                return new FallbackLookupResult(null, matches, true);
            }
        }
    }

    internal sealed class RoAssetStartupTiming {
        private const long SlowPhaseThresholdMilliseconds = 100;
        private const int MaxLookupDetails = 10;

        private readonly Stopwatch totalStopwatch = Stopwatch.StartNew();
        private readonly int startPayloadReads;
        private readonly int startPayloadInflates;
        private readonly int startTableInflates;
        private readonly List<string> lookupDetails = new List<string>();
        private readonly object sync = new object();

        private RoAssetStartupTiming() {
            startPayloadReads = Grf.PayloadReadCountForDiagnostics;
            startPayloadInflates = Grf.PayloadInflateCountForDiagnostics;
            startTableInflates = Grf.TableInflateCountForDiagnostics;
        }

        public long LooseSourceMilliseconds { get; private set; }
        public long GrfSourcesMilliseconds { get; private set; }
        public long ManifestMilliseconds { get; private set; }
        public long AliasesMilliseconds { get; private set; }
        public long TablesMilliseconds { get; private set; }
        public long GameDataMilliseconds { get; private set; }
        public long LocalizationMilliseconds { get; private set; }
        public long GameDataItemsMilliseconds { get; private set; }
        public long GameDataActorAppearanceMilliseconds { get; private set; }
        public long GameDataJobsMilliseconds { get; private set; }
        public long GameDataEntityDisplayMilliseconds { get; private set; }
        public long GameDataSkillsMilliseconds { get; private set; }
        public long GameDataQuestsMilliseconds { get; private set; }
        public long GameDataDiagnosticsMilliseconds { get; private set; }
        public int ListFilesWithExtensionBuildCount { get; private set; }
        public long ListFilesWithExtensionMilliseconds { get; private set; }
        public int SuffixFallbackScanCount { get; private set; }
        public long SuffixFallbackMilliseconds { get; private set; }
        public int ContainsFallbackScanCount { get; private set; }
        public long ContainsFallbackMilliseconds { get; private set; }

        public static RoAssetStartupTiming Start() {
            return new RoAssetStartupTiming();
        }

        public IDisposable MeasureLooseSource() {
            return Measure(milliseconds => LooseSourceMilliseconds += milliseconds);
        }

        public IDisposable MeasureGrfSources() {
            return Measure(milliseconds => GrfSourcesMilliseconds += milliseconds);
        }

        public IDisposable MeasureManifest() {
            return Measure(milliseconds => ManifestMilliseconds += milliseconds);
        }

        public IDisposable MeasureAliases() {
            return Measure(milliseconds => AliasesMilliseconds += milliseconds);
        }

        public IDisposable MeasureTables() {
            return Measure(milliseconds => TablesMilliseconds += milliseconds);
        }

        public IDisposable MeasureGameData() {
            return Measure(milliseconds => GameDataMilliseconds += milliseconds);
        }

        public IDisposable MeasureLocalization() {
            return Measure(milliseconds => LocalizationMilliseconds += milliseconds);
        }

        public IDisposable MeasureGameDataItems() {
            return Measure(milliseconds => GameDataItemsMilliseconds += milliseconds);
        }

        public IDisposable MeasureGameDataActorAppearance() {
            return Measure(milliseconds => GameDataActorAppearanceMilliseconds += milliseconds);
        }

        public IDisposable MeasureGameDataJobs() {
            return Measure(milliseconds => GameDataJobsMilliseconds += milliseconds);
        }

        public IDisposable MeasureGameDataEntityDisplay() {
            return Measure(milliseconds => GameDataEntityDisplayMilliseconds += milliseconds);
        }

        public IDisposable MeasureGameDataSkills() {
            return Measure(milliseconds => GameDataSkillsMilliseconds += milliseconds);
        }

        public IDisposable MeasureGameDataQuests() {
            return Measure(milliseconds => GameDataQuestsMilliseconds += milliseconds);
        }

        public IDisposable MeasureGameDataDiagnostics() {
            return Measure(milliseconds => GameDataDiagnosticsMilliseconds += milliseconds);
        }

        public void RecordListFilesWithExtension(string extension, int fileCount, long milliseconds) {
            lock (sync) {
                ListFilesWithExtensionBuildCount++;
                ListFilesWithExtensionMilliseconds += milliseconds;
                AddLookupDetail(milliseconds, $"ext .{extension}:{fileCount} files/{milliseconds} ms");
            }
        }

        public void RecordSuffixFallbackScan(
            string suffix,
            bool resolved,
            bool ambiguous,
            int matchCount,
            long milliseconds) {
            lock (sync) {
                SuffixFallbackScanCount++;
                SuffixFallbackMilliseconds += milliseconds;
                AddLookupDetail(
                    milliseconds,
                    $"suffix '{suffix}'={FormatFallbackResult(resolved, ambiguous, matchCount)}/{milliseconds} ms");
            }
        }

        public void RecordContainsFallbackScan(
            string contains,
            string requiredEnding,
            bool resolved,
            bool ambiguous,
            int matchCount,
            long milliseconds) {
            lock (sync) {
                ContainsFallbackScanCount++;
                ContainsFallbackMilliseconds += milliseconds;
                AddLookupDetail(
                    milliseconds,
                    $"contains '{contains}|{requiredEnding}'={FormatFallbackResult(resolved, ambiguous, matchCount)}/{milliseconds} ms");
            }
        }

        public void Log(bool succeeded, int sourceCount, int fileCount) {
            totalStopwatch.Stop();
            long totalMilliseconds = totalStopwatch.ElapsedMilliseconds;
            long maxPhaseMilliseconds = MaxPhaseMilliseconds(totalMilliseconds);
            if (!ShouldLogForDiagnostics(maxPhaseMilliseconds)) {
                return;
            }

            var builder = new StringBuilder();
            builder.Append("RO content startup timings: status=");
            builder.Append(succeeded ? "ready" : "failed");
            builder.Append(", total=");
            builder.Append(totalMilliseconds);
            builder.Append(" ms, loose=");
            builder.Append(LooseSourceMilliseconds);
            builder.Append(" ms, grfSources=");
            builder.Append(GrfSourcesMilliseconds);
            builder.Append(" ms, manifest=");
            builder.Append(ManifestMilliseconds);
            builder.Append(" ms, aliases=");
            builder.Append(AliasesMilliseconds);
            builder.Append(" ms, tables=");
            builder.Append(TablesMilliseconds);
            builder.Append(" ms, gameData=");
            builder.Append(GameDataMilliseconds);
            builder.Append(" ms, localization=");
            builder.Append(LocalizationMilliseconds);
            builder.Append(" ms, sources=");
            builder.Append(sourceCount);
            builder.Append(", files=");
            builder.Append(fileCount);
            builder.Append(", grf=tableInflates:");
            builder.Append(Math.Max(0, Grf.TableInflateCountForDiagnostics - startTableInflates));
            builder.Append(",payloadReads:");
            builder.Append(Math.Max(0, Grf.PayloadReadCountForDiagnostics - startPayloadReads));
            builder.Append(",payloadInflates:");
            builder.Append(Math.Max(0, Grf.PayloadInflateCountForDiagnostics - startPayloadInflates));
            builder.Append(", lookups=listExt:");
            builder.Append(ListFilesWithExtensionBuildCount);
            builder.Append('/');
            builder.Append(ListFilesWithExtensionMilliseconds);
            builder.Append(" ms,suffix:");
            builder.Append(SuffixFallbackScanCount);
            builder.Append('/');
            builder.Append(SuffixFallbackMilliseconds);
            builder.Append(" ms,contains:");
            builder.Append(ContainsFallbackScanCount);
            builder.Append('/');
            builder.Append(ContainsFallbackMilliseconds);
            builder.Append(" ms, gameDataDetails=items:");
            builder.Append(GameDataItemsMilliseconds);
            builder.Append(" ms,actorAppearance:");
            builder.Append(GameDataActorAppearanceMilliseconds);
            builder.Append(" ms,jobs:");
            builder.Append(GameDataJobsMilliseconds);
            builder.Append(" ms,entityDisplay:");
            builder.Append(GameDataEntityDisplayMilliseconds);
            builder.Append(" ms,skills:");
            builder.Append(GameDataSkillsMilliseconds);
            builder.Append(" ms,quests:");
            builder.Append(GameDataQuestsMilliseconds);
            builder.Append(" ms,diagnostics:");
            builder.Append(GameDataDiagnosticsMilliseconds);
            builder.Append(" ms");

            if (lookupDetails.Count > 0) {
                builder.Append(", lookupDetails=[");
                builder.Append(string.Join("; ", lookupDetails));
                builder.Append(']');
            }

            Debug.Log(builder.ToString());
        }

        public static long ElapsedMilliseconds(long startTimestamp) {
            return (long)((Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency);
        }

        public static bool ShouldLogForDiagnostics(long maxPhaseMilliseconds) {
            return IsVerboseDiagnosticsBuild || maxPhaseMilliseconds >= SlowPhaseThresholdMilliseconds;
        }

        private IDisposable Measure(Action<long> addElapsedMilliseconds) {
            return new TimingScope(addElapsedMilliseconds);
        }

        private void AddLookupDetail(long milliseconds, string detail) {
            if (!ShouldLogForDiagnostics(milliseconds) || lookupDetails.Count >= MaxLookupDetails) {
                return;
            }

            lookupDetails.Add(detail);
        }

        private long MaxPhaseMilliseconds(long totalMilliseconds) {
            long max = totalMilliseconds;
            max = Math.Max(max, LooseSourceMilliseconds);
            max = Math.Max(max, GrfSourcesMilliseconds);
            max = Math.Max(max, ManifestMilliseconds);
            max = Math.Max(max, AliasesMilliseconds);
            max = Math.Max(max, TablesMilliseconds);
            max = Math.Max(max, GameDataMilliseconds);
            max = Math.Max(max, LocalizationMilliseconds);
            max = Math.Max(max, ListFilesWithExtensionMilliseconds);
            max = Math.Max(max, SuffixFallbackMilliseconds);
            max = Math.Max(max, ContainsFallbackMilliseconds);
            max = Math.Max(max, GameDataItemsMilliseconds);
            max = Math.Max(max, GameDataActorAppearanceMilliseconds);
            max = Math.Max(max, GameDataJobsMilliseconds);
            max = Math.Max(max, GameDataEntityDisplayMilliseconds);
            max = Math.Max(max, GameDataSkillsMilliseconds);
            max = Math.Max(max, GameDataQuestsMilliseconds);
            max = Math.Max(max, GameDataDiagnosticsMilliseconds);
            return max;
        }

        private static string FormatFallbackResult(bool resolved, bool ambiguous, int matchCount) {
            if (resolved) {
                return "resolved";
            }

            if (ambiguous) {
                return "ambiguous:" + matchCount;
            }

            return "miss";
        }

        private static bool IsVerboseDiagnosticsBuild {
            get {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        private sealed class TimingScope : IDisposable {
            private readonly Action<long> addElapsedMilliseconds;
            private readonly long startTimestamp;
            private bool disposed;

            public TimingScope(Action<long> addElapsedMilliseconds) {
                this.addElapsedMilliseconds = addElapsedMilliseconds;
                startTimestamp = Stopwatch.GetTimestamp();
            }

            public void Dispose() {
                if (disposed) {
                    return;
                }

                disposed = true;
                addElapsedMilliseconds?.Invoke(ElapsedMilliseconds(startTimestamp));
            }
        }
    }
}
