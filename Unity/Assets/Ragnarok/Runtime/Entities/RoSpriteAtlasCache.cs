using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ragnarok.Assets;
using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using Unity.Profiling;
using UnityEngine;

namespace Ragnarok.Entities {
    public readonly struct RoSpriteAtlasCacheStats {
        public RoSpriteAtlasCacheStats(long hits, long misses, long builds, long sharedBuilds, int entries, int atlases) {
            Hits = hits;
            Misses = misses;
            Builds = builds;
            SharedBuilds = sharedBuilds;
            Entries = entries;
            Atlases = atlases;
        }

        public long Hits { get; }
        public long Misses { get; }
        public long Builds { get; }
        /// <summary>Misses served by an atlas that another path with the same .spr content had already built.</summary>
        public long SharedBuilds { get; }
        public int Entries { get; }
        /// <summary>Distinct atlases. This is fewer than <see cref="Entries"/> when sprites share content.</summary>
        public int Atlases { get; }
    }

    public sealed class RoSpriteAtlasCacheEntry {
        public RoSpriteAtlasCacheEntry(SPR sprite, ACT act, RoSpriteAtlas atlas) {
            Sprite = sprite;
            Act = act;
            Atlas = atlas;
        }

        public SPR Sprite { get; }
        public ACT Act { get; }
        public RoSpriteAtlas Atlas { get; }
    }

    /// <summary>
    /// Atlases per sprite layer. An entry belongs to a path (base path + palette) and carries that path's ACT. The atlas
    /// itself belongs to the .spr content (see <see cref="RoAssetContentKey"/>) + palette. A robe's sprite is the same
    /// block for every job and gender in data.grf, so those paths share one atlas. Their ACTs differ, and the frame
    /// geometry cache keys by the ACT's file name, so the geometry stays per path.
    /// </summary>
    public static class RoSpriteAtlasCache {
        private sealed class SharedAtlas {
            public SharedAtlas(SPR sprite, RoSpriteAtlas atlas) {
                Sprite = sprite;
                Atlas = atlas;
            }

            public SPR Sprite { get; }
            public RoSpriteAtlas Atlas { get; }
        }

        private sealed class CacheBucket {
            public readonly Dictionary<string, RoSpriteAtlasCacheEntry> Entries =
                new Dictionary<string, RoSpriteAtlasCacheEntry>(StringComparer.OrdinalIgnoreCase);

            // shared with the prefetch workers
            /// <summary>Built atlases per sprite key (.spr content + palette). Written on the main thread only.</summary>
            public readonly ConcurrentDictionary<string, SharedAtlas> Atlases =
                new ConcurrentDictionary<string, SharedAtlas>(StringComparer.OrdinalIgnoreCase);
            /// <summary>ACT per sprite base path: it does not depend on the palette, so palette variants share it.</summary>
            public readonly ConcurrentDictionary<string, ACT> Acts = new ConcurrentDictionary<string, ACT>(StringComparer.OrdinalIgnoreCase);
            /// <summary>Palette file bytes per path (null = missing).</summary>
            public readonly ConcurrentDictionary<string, byte[]> Palettes = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            /// <summary>SPRs compiled to RGBA by a worker, per sprite key, waiting for their atlas build.</summary>
            public readonly ConcurrentDictionary<string, SPR> Prepared = new ConcurrentDictionary<string, SPR>(StringComparer.OrdinalIgnoreCase);
            /// <summary>Prefetch requests already queued (base path | palette path), with their completion.</summary>
            public readonly ConcurrentDictionary<string, PrefetchTicket> Requested =
                new ConcurrentDictionary<string, PrefetchTicket>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>A background preparation of one sprite layer; Done once it finished, whether it prepared anything or not.</summary>
        public sealed class PrefetchTicket {
            internal static readonly PrefetchTicket Finished = new PrefetchTicket { done = true };
            volatile bool done;
            public bool Done => done;
            internal void Complete() => done = true;
        }

        private static readonly Dictionary<RoAssetDatabase, CacheBucket> Buckets =
            new Dictionary<RoAssetDatabase, CacheBucket>();

        private static long hits;
        private static long misses;
        private static long builds;
        private static long sharedBuilds;

        private static readonly ProfilerMarker BuildMarker = new ProfilerMarker("SpriteAtlasCache.Build");
        private static readonly ProfilerMarker LoadSprMarker = new ProfilerMarker("SpriteAtlasCache.LoadSpr");
        private static readonly ProfilerMarker LoadActMarker = new ProfilerMarker("SpriteAtlasCache.LoadAct");

        public static bool TryGet(
            RoAssetDatabase assets,
            string basePath,
            out RoSpriteAtlasCacheEntry entry,
            byte[] paletteOverride = null
        ) {
            entry = null;
            if (assets == null || string.IsNullOrWhiteSpace(basePath)) {
                return false;
            }

            string normalizedBasePath = NormalizeBasePath(basePath);
            if (string.IsNullOrEmpty(normalizedBasePath)) {
                return false;
            }

            string cacheKey = BuildCacheKey(normalizedBasePath, paletteOverride);
            return Buckets.TryGetValue(assets, out CacheBucket bucket) &&
                bucket.Entries.TryGetValue(cacheKey, out entry) &&
                entry != null;
        }

        public static bool TryGetOrBuild(
            RoAssetDatabase assets,
            string basePath,
            out RoSpriteAtlasCacheEntry entry,
            byte[] paletteOverride = null
        ) {
            entry = null;
            if (assets == null || string.IsNullOrWhiteSpace(basePath)) {
                return false;
            }

            string normalizedBasePath = NormalizeBasePath(basePath);
            if (string.IsNullOrEmpty(normalizedBasePath)) {
                return false;
            }

            string cacheKey = BuildCacheKey(normalizedBasePath, paletteOverride);
            CacheBucket bucket = GetBucket(assets);
            if (bucket.Entries.TryGetValue(cacheKey, out entry)) {
                hits++;
                return true;
            }

            misses++;
            string spriteKey = BuildSpriteKey(assets, normalizedBasePath, paletteOverride);
            bucket.Atlases.TryGetValue(spriteKey, out SharedAtlas shared);
            SPR sprite = shared?.Sprite;
            ACT act;
            if (sprite == null && !bucket.Prepared.TryRemove(spriteKey, out sprite)) {
                using (LoadSprMarker.Auto()) {
                    sprite = assets.Load<SPR>(RoSpritePairPath.SpritePath(normalizedBasePath));
                }
            }

            using (LoadActMarker.Auto()) {
                act = GetSharedAct(bucket, assets, normalizedBasePath, quiet: false);
            }
            if (sprite == null || act == null) {
                return false;
            }

            if (shared != null) {
                // a prefetch of this path may have compiled the same content again: not needed
                bucket.Prepared.TryRemove(spriteKey, out _);
                sharedBuilds++;
            } else {
                using (BuildMarker.Auto()) {
                    shared = new SharedAtlas(sprite, RoSpriteAtlasBuilder.Build(sprite, paletteOverride));
                    bucket.Atlases[spriteKey] = shared;
                    builds++;
                }
            }

            entry = new RoSpriteAtlasCacheEntry(shared.Sprite, act, shared.Atlas);
            bucket.Entries[cacheKey] = entry;
            return true;
        }

        public static RoSpriteAtlasCacheStats SnapshotStats(RoAssetDatabase assets = null) {
            int entries = 0;
            int atlases = 0;
            if (assets != null) {
                if (Buckets.TryGetValue(assets, out CacheBucket bucket)) {
                    entries = bucket.Entries.Count;
                    atlases = bucket.Atlases.Count;
                }
            } else {
                foreach (CacheBucket bucket in Buckets.Values) {
                    entries += bucket.Entries.Count;
                    atlases += bucket.Atlases.Count;
                }
            }

            return new RoSpriteAtlasCacheStats(hits, misses, builds, sharedBuilds, entries, atlases);
        }

        public static void Clear(RoAssetDatabase assets) {
            if (assets == null || !Buckets.TryGetValue(assets, out CacheBucket bucket)) {
                return;
            }

            DisposeBucket(bucket);
            Buckets.Remove(assets);
        }

        public static void ClearAll() {
            foreach (CacheBucket bucket in Buckets.Values) {
                DisposeBucket(bucket);
            }

            Buckets.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() {
            ClearAll();
            hits = 0;
            misses = 0;
            builds = 0;
            sharedBuilds = 0;
        }

        /// <summary>A palette file's bytes, read once per database (null when missing).</summary>
        public static byte[] LoadPalette(RoAssetDatabase assets, string palettePath) {
            if (assets == null || string.IsNullOrWhiteSpace(palettePath)) {
                return null;
            }

            CacheBucket bucket = GetBucket(assets);
            return LoadPalette(bucket, assets, palettePath, quiet: false);
        }

        /// <summary>
        /// Prepares a sprite layer in the background (main thread call): palette, ACT, and the SPR read, parsed and compiled
        /// to RGBA with that palette, so the later <see cref="TryGetOrBuild"/> only packs and uploads the atlas.
        /// </summary>
        public static PrefetchTicket Prefetch(RoAssetDatabase assets, string basePath, string palettePath) {
            if (assets == null || !assets.IsInitialized || string.IsNullOrWhiteSpace(basePath)) {
                return PrefetchTicket.Finished;
            }

            string normalizedBasePath = NormalizeBasePath(basePath);
            if (string.IsNullOrEmpty(normalizedBasePath)) {
                return PrefetchTicket.Finished;
            }

            CacheBucket bucket = GetBucket(assets);
            var ticket = new PrefetchTicket();
            PrefetchTicket queued = bucket.Requested.GetOrAdd(normalizedBasePath + "|" + (palettePath ?? string.Empty), ticket);
            if (queued != ticket) {
                return queued;
            }

            Task.Run(() => Prepare(bucket, assets, normalizedBasePath, palettePath, ticket));
            return ticket;
        }

        private static void Prepare(CacheBucket bucket, RoAssetDatabase assets, string normalizedBasePath, string palettePath,
            PrefetchTicket ticket) {
            try {
                byte[] palette = string.IsNullOrWhiteSpace(palettePath) ? null : LoadPalette(bucket, assets, palettePath, quiet: true);
                GetSharedAct(bucket, assets, normalizedBasePath, quiet: true);
                string spriteKey = BuildSpriteKey(assets, normalizedBasePath, palette);
                if (bucket.Atlases.ContainsKey(spriteKey) || bucket.Prepared.ContainsKey(spriteKey)) {
                    return;
                }

                SPR sprite = assets.LoadQuiet<SPR>(RoSpritePairPath.SpritePath(normalizedBasePath));
                if (sprite?.frames == null ||
                    (sprite.indexedCount > 0 && (palette == null || palette.Length == 0) && sprite.palette == null)) {
                    return;
                }

                sprite.CompileRgba(palette ?? Array.Empty<byte>());
                bucket.Prepared.TryAdd(spriteKey, sprite);
            } catch (Exception ex) {
                Debug.LogWarning($"Sprite prefetch of '{normalizedBasePath}' failed: {ex.Message}");
            } finally {
                ticket.Complete();
            }
        }

        private static byte[] LoadPalette(CacheBucket bucket, RoAssetDatabase assets, string palettePath, bool quiet) {
            if (bucket.Palettes.TryGetValue(palettePath, out byte[] palette)) {
                return palette;
            }

            palette = quiet ? assets.ReadBytesQuiet(palettePath) : assets.LoadRawBytes(palettePath);
            return bucket.Palettes.GetOrAdd(palettePath, palette);
        }

        private static ACT GetSharedAct(CacheBucket bucket, RoAssetDatabase assets, string normalizedBasePath, bool quiet) {
            if (bucket.Acts.TryGetValue(normalizedBasePath, out ACT act)) {
                return act;
            }

            string path = RoSpritePairPath.ActionPath(normalizedBasePath);
            act = quiet ? assets.LoadQuiet<ACT>(path) : assets.Load<ACT>(path);
            return act != null ? bucket.Acts.GetOrAdd(normalizedBasePath, act) : null;
        }

        private static CacheBucket GetBucket(RoAssetDatabase assets) {
            if (!Buckets.TryGetValue(assets, out CacheBucket bucket)) {
                bucket = new CacheBucket();
                Buckets.Add(assets, bucket);
            }

            return bucket;
        }

        private static void DisposeBucket(CacheBucket bucket) {
            foreach (SharedAtlas shared in bucket.Atlases.Values) {
                shared?.Atlas?.Dispose();
            }

            bucket.Atlases.Clear();
            bucket.Entries.Clear();
        }

        private static string NormalizeBasePath(string path) {
            string normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (normalized.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".act", StringComparison.OrdinalIgnoreCase)) {
                normalized = normalized.Substring(0, normalized.Length - 4);
            }

            return normalized;
        }

        /// <summary>
        /// The atlas key: the .spr content when the archive can identify it, otherwise the base path. A '#' cannot start
        /// a normalized path, so the two forms never collide.
        /// </summary>
        private static string BuildSpriteKey(RoAssetDatabase assets, string normalizedBasePath, byte[] palette) {
            string source = assets.TryGetContentKey(RoSpritePairPath.SpritePath(normalizedBasePath), out RoAssetContentKey content)
                ? "#" + content
                : RoSpritePairPath.SpriteBasePath(normalizedBasePath);
            return BuildCacheKey(source, palette);
        }

        private static string BuildCacheKey(string normalizedBasePath, byte[] paletteOverride) {
            if (paletteOverride == null || paletteOverride.Length == 0) {
                return normalizedBasePath + "|palette:default";
            }

            return normalizedBasePath + "|palette:" + CalculatePaletteHash(paletteOverride).ToString("x16");
        }

        private static ulong CalculatePaletteHash(byte[] paletteOverride) {
            const ulong offset = 14695981039346656037ul;
            const ulong prime = 1099511628211ul;
            ulong hash = offset;
            for (int i = 0; i < paletteOverride.Length; i++) {
                hash ^= paletteOverride[i];
                hash *= prime;
            }

            return hash;
        }
    }
}
