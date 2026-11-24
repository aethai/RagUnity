using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Ragnarok.Assets;
using ROIO.Loaders;
using ROIO.Models.FileTypes;
using Unity.Profiling;
using UnityEngine;

namespace Ragnarok.Maps {
    public readonly struct RoRuntimeMapAssetCacheStats {
        public RoRuntimeMapAssetCacheStats(
            long mapAssetHits,
            long mapAssetMisses,
            long compiledGroundHits,
            long compiledGroundMisses,
            long rawModelHits,
            long rawModelMisses,
            long compiledModelHits,
            long compiledModelMisses,
            long compiledModelBuilds,
            long compiledModelBuildMilliseconds,
            long groundMeshHits,
            long groundMeshMisses,
            long groundMeshBuilds,
            long groundAtlasHits,
            long groundAtlasMisses,
            long groundAtlasBuilds,
            long waterMeshHits,
            long waterMeshMisses,
            long waterMeshBuilds,
            long waterHits,
            long waterMisses,
            long waterBuilds,
            int entries
        ) {
            MapAssetHits = mapAssetHits;
            MapAssetMisses = mapAssetMisses;
            CompiledGroundHits = compiledGroundHits;
            CompiledGroundMisses = compiledGroundMisses;
            RawModelHits = rawModelHits;
            RawModelMisses = rawModelMisses;
            CompiledModelHits = compiledModelHits;
            CompiledModelMisses = compiledModelMisses;
            CompiledModelBuilds = compiledModelBuilds;
            CompiledModelBuildMilliseconds = compiledModelBuildMilliseconds;
            GroundMeshHits = groundMeshHits;
            GroundMeshMisses = groundMeshMisses;
            GroundMeshBuilds = groundMeshBuilds;
            GroundAtlasHits = groundAtlasHits;
            GroundAtlasMisses = groundAtlasMisses;
            GroundAtlasBuilds = groundAtlasBuilds;
            WaterMeshHits = waterMeshHits;
            WaterMeshMisses = waterMeshMisses;
            WaterMeshBuilds = waterMeshBuilds;
            WaterHits = waterHits;
            WaterMisses = waterMisses;
            WaterBuilds = waterBuilds;
            Entries = entries;
        }

        public long MapAssetHits { get; }
        public long MapAssetMisses { get; }
        public long CompiledGroundHits { get; }
        public long CompiledGroundMisses { get; }
        public long RawModelHits { get; }
        public long RawModelMisses { get; }
        public long CompiledModelHits { get; }
        public long CompiledModelMisses { get; }
        public long CompiledModelBuilds { get; }
        public long CompiledModelBuildMilliseconds { get; }
        public long GroundMeshHits { get; }
        public long GroundMeshMisses { get; }
        public long GroundMeshBuilds { get; }
        public long GroundAtlasHits { get; }
        public long GroundAtlasMisses { get; }
        public long GroundAtlasBuilds { get; }
        public long WaterMeshHits { get; }
        public long WaterMeshMisses { get; }
        public long WaterMeshBuilds { get; }
        public long WaterHits { get; }
        public long WaterMisses { get; }
        public long WaterBuilds { get; }
        public int Entries { get; }
    }

    public static class RoRuntimeMapAssetCache {
        private sealed class CacheBucket {
            public readonly Dictionary<string, object> MapAssets =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, GND.Mesh> CompiledGround =
                new Dictionary<string, GND.Mesh>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, RSM> RawModels =
                new Dictionary<string, RSM>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, RSM.CompiledModel> CompiledModels =
                new Dictionary<string, RSM.CompiledModel>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<GroundMeshCacheKey, Mesh> GroundMeshes =
                new Dictionary<GroundMeshCacheKey, Mesh>();
            public readonly Dictionary<string, Texture2D> GroundAtlases =
                new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<GND.Mesh, Mesh> WaterMeshes =
                new Dictionary<GND.Mesh, Mesh>();
            public readonly Dictionary<string, Texture2D[]> WaterTextures =
                new Dictionary<string, Texture2D[]>(StringComparer.OrdinalIgnoreCase);

            public int Count =>
                MapAssets.Count +
                CompiledGround.Count +
                RawModels.Count +
                CompiledModels.Count +
                GroundMeshes.Count +
                GroundAtlases.Count +
                WaterMeshes.Count +
                WaterTextures.Count;
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<RoAssetDatabase, CacheBucket> Buckets =
            new Dictionary<RoAssetDatabase, CacheBucket>();

        private static readonly ProfilerMarker ModelHitMarker = new ProfilerMarker("Map.Cache.ModelHit");
        private static readonly ProfilerMarker ModelMissMarker = new ProfilerMarker("Map.Cache.ModelMiss");
        private static readonly ProfilerMarker GroundAtlasHitMarker = new ProfilerMarker("Map.Cache.GroundAtlasHit");
        private static readonly ProfilerMarker GroundAtlasMissMarker = new ProfilerMarker("Map.Cache.GroundAtlasMiss");
        private static readonly ProfilerMarker WaterHitMarker = new ProfilerMarker("Map.Cache.WaterHit");
        private static readonly ProfilerMarker WaterMissMarker = new ProfilerMarker("Map.Cache.WaterMiss");
        private static readonly ProfilerMarker CompileModelMarker = new ProfilerMarker("Map.Cache.ModelLoader.Compile");

        private static long mapAssetHits;
        private static long mapAssetMisses;
        private static long compiledGroundHits;
        private static long compiledGroundMisses;
        private static long rawModelHits;
        private static long rawModelMisses;
        private static long compiledModelHits;
        private static long compiledModelMisses;
        private static long compiledModelBuilds;
        private static long compiledModelBuildMilliseconds;
        private static long groundMeshHits;
        private static long groundMeshMisses;
        private static long groundMeshBuilds;
        private static long groundAtlasHits;
        private static long groundAtlasMisses;
        private static long groundAtlasBuilds;
        private static long waterMeshHits;
        private static long waterMeshMisses;
        private static long waterMeshBuilds;
        private static long waterHits;
        private static long waterMisses;
        private static long waterBuilds;

        public static T GetOrLoadMapAsset<T>(RoAssetDatabase assets, string path, Func<T> loader) where T : class {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (loader == null) {
                throw new ArgumentNullException(nameof(loader));
            }

            string key = typeof(T).FullName + "|" + NormalizeKey(path);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.MapAssets.TryGetValue(key, out object cached) && cached is T typed) {
                    Interlocked.Increment(ref mapAssetHits);
                    return typed;
                }
            }

            Interlocked.Increment(ref mapAssetMisses);
            T loaded = loader();
            if (loaded == null) {
                return null;
            }

            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.MapAssets.TryGetValue(key, out object existing) && existing is T typed) {
                    return typed;
                }

                bucket.MapAssets[key] = loaded;
                return loaded;
            }
        }

        public static GND.Mesh GetOrBuildCompiledGround(RoAssetDatabase assets, string key, Func<GND.Mesh> builder) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            string cacheKey = NormalizeKey(key);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.CompiledGround.TryGetValue(cacheKey, out GND.Mesh cached)) {
                    Interlocked.Increment(ref compiledGroundHits);
                    return cached;
                }
            }

            Interlocked.Increment(ref compiledGroundMisses);
            GND.Mesh built = builder();
            if (built == null) {
                return null;
            }

            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.CompiledGround.TryGetValue(cacheKey, out GND.Mesh existing)) {
                    return existing;
                }

                bucket.CompiledGround[cacheKey] = built;
                return built;
            }
        }

        public static RSM.CompiledModel GetOrCompileModelView(
            RoAssetDatabase assets,
            string path,
            string filename,
            IReadOnlyList<RSW.ModelDescriptor> instances,
            Func<RSM> loader
        ) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (loader == null) {
                throw new ArgumentNullException(nameof(loader));
            }

            string cacheKey = NormalizeKey(path);
            RSM.CompiledModel compiled;
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.CompiledModels.TryGetValue(cacheKey, out compiled)) {
                    Mark(ModelHitMarker);
                    Interlocked.Increment(ref compiledModelHits);
                    return CreateCompiledModelView(compiled, filename, instances);
                }
            }

            Mark(ModelMissMarker);
            Interlocked.Increment(ref compiledModelMisses);
            RSM rawModel = GetOrLoadRawModel(assets, path, filename, loader);
            if (rawModel == null) {
                return null;
            }

            var stopwatch = Stopwatch.StartNew();
            using (CompileModelMarker.Auto()) {
                compiled = ModelLoader.Compile(rawModel);
            }

            stopwatch.Stop();
            Interlocked.Increment(ref compiledModelBuilds);
            Interlocked.Add(ref compiledModelBuildMilliseconds, stopwatch.ElapsedMilliseconds);

            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (!bucket.CompiledModels.TryGetValue(cacheKey, out RSM.CompiledModel existing)) {
                    bucket.CompiledModels[cacheKey] = compiled;
                    existing = compiled;
                }

                return CreateCompiledModelView(existing, filename, instances);
            }
        }

        public static Mesh GetOrBuildGroundMesh(
            RoAssetDatabase assets,
            GND.Mesh ground,
            RoGroundMeshBuilder.GroundTopDiagonalMode diagonalMode,
            Func<Mesh> builder
        ) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (ground == null) {
                throw new ArgumentNullException(nameof(ground));
            }

            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            var key = new GroundMeshCacheKey(ground, diagonalMode);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.GroundMeshes.TryGetValue(key, out Mesh cached)) {
                    if (cached != null) {
                        Interlocked.Increment(ref groundMeshHits);
                        return cached;
                    }

                    bucket.GroundMeshes.Remove(key);
                }
            }

            Interlocked.Increment(ref groundMeshMisses);
            Mesh built = builder();
            if (built == null) {
                return null;
            }

            Interlocked.Increment(ref groundMeshBuilds);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.GroundMeshes.TryGetValue(key, out Mesh existing) && existing != null) {
                    DestroyRuntimeObject(built);
                    return existing;
                }

                bucket.GroundMeshes[key] = built;
                return built;
            }
        }

        public static Texture2D GetOrBuildGroundAtlas(
            RoAssetDatabase assets,
            GND.Mesh ground,
            FilterMode filterMode,
            Func<Texture2D> builder
        ) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            string key = BuildGroundAtlasKey(ground, filterMode);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.GroundAtlases.TryGetValue(key, out Texture2D cached) && cached != null) {
                    Mark(GroundAtlasHitMarker);
                    Interlocked.Increment(ref groundAtlasHits);
                    return cached;
                }
            }

            Mark(GroundAtlasMissMarker);
            Interlocked.Increment(ref groundAtlasMisses);
            Texture2D atlas = builder();
            if (atlas == null) {
                return null;
            }

            Interlocked.Increment(ref groundAtlasBuilds);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.GroundAtlases.TryGetValue(key, out Texture2D existing) && existing != null) {
                    DestroyRuntimeObject(atlas);
                    return existing;
                }

                bucket.GroundAtlases[key] = atlas;
                return atlas;
            }
        }

        public static Mesh GetOrBuildWaterMesh(RoAssetDatabase assets, GND.Mesh ground, Func<Mesh> builder) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (ground == null) {
                throw new ArgumentNullException(nameof(ground));
            }

            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.WaterMeshes.TryGetValue(ground, out Mesh cached)) {
                    if (cached != null) {
                        Interlocked.Increment(ref waterMeshHits);
                        return cached;
                    }

                    bucket.WaterMeshes.Remove(ground);
                }
            }

            Interlocked.Increment(ref waterMeshMisses);
            Mesh built = builder();
            if (built == null) {
                return null;
            }

            Interlocked.Increment(ref waterMeshBuilds);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.WaterMeshes.TryGetValue(ground, out Mesh existing) && existing != null) {
                    DestroyRuntimeObject(built);
                    return existing;
                }

                bucket.WaterMeshes[ground] = built;
                return built;
            }
        }

        public static Texture2D[] GetOrBuildWaterTextures(
            RoAssetDatabase assets,
            RSW.WaterInfo waterInfo,
            Func<Texture2D[]> builder
        ) {
            if (assets == null) {
                throw new ArgumentNullException(nameof(assets));
            }

            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            }

            string key = BuildWaterKey(waterInfo);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.WaterTextures.TryGetValue(key, out Texture2D[] cached) && cached != null) {
                    Mark(WaterHitMarker);
                    Interlocked.Increment(ref waterHits);
                    return cached;
                }
            }

            Mark(WaterMissMarker);
            Interlocked.Increment(ref waterMisses);
            Texture2D[] textures = builder();
            if (textures == null) {
                return null;
            }

            Interlocked.Increment(ref waterBuilds);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.WaterTextures.TryGetValue(key, out Texture2D[] existing) && existing != null) {
                    DestroyWaterTextures(textures);
                    return existing;
                }

                bucket.WaterTextures[key] = textures;
                return textures;
            }
        }

        public static RoRuntimeMapAssetCacheStats SnapshotStats(RoAssetDatabase assets = null) {
            int entries = 0;
            lock (Sync) {
                if (assets != null) {
                    if (Buckets.TryGetValue(assets, out CacheBucket bucket)) {
                        entries = bucket.Count;
                    }
                } else {
                    foreach (CacheBucket bucket in Buckets.Values) {
                        entries += bucket.Count;
                    }
                }
            }

            return new RoRuntimeMapAssetCacheStats(
                Interlocked.Read(ref mapAssetHits),
                Interlocked.Read(ref mapAssetMisses),
                Interlocked.Read(ref compiledGroundHits),
                Interlocked.Read(ref compiledGroundMisses),
                Interlocked.Read(ref rawModelHits),
                Interlocked.Read(ref rawModelMisses),
                Interlocked.Read(ref compiledModelHits),
                Interlocked.Read(ref compiledModelMisses),
                Interlocked.Read(ref compiledModelBuilds),
                Interlocked.Read(ref compiledModelBuildMilliseconds),
                Interlocked.Read(ref groundMeshHits),
                Interlocked.Read(ref groundMeshMisses),
                Interlocked.Read(ref groundMeshBuilds),
                Interlocked.Read(ref groundAtlasHits),
                Interlocked.Read(ref groundAtlasMisses),
                Interlocked.Read(ref groundAtlasBuilds),
                Interlocked.Read(ref waterMeshHits),
                Interlocked.Read(ref waterMeshMisses),
                Interlocked.Read(ref waterMeshBuilds),
                Interlocked.Read(ref waterHits),
                Interlocked.Read(ref waterMisses),
                Interlocked.Read(ref waterBuilds),
                entries);
        }

        public static void Clear(RoAssetDatabase assets) {
            if (assets == null) {
                return;
            }

            CacheBucket bucket;
            lock (Sync) {
                if (!Buckets.TryGetValue(assets, out bucket)) {
                    return;
                }

                Buckets.Remove(assets);
            }

            DisposeBucket(bucket);
        }

        public static void ClearAll() {
            List<CacheBucket> buckets;
            lock (Sync) {
                buckets = new List<CacheBucket>(Buckets.Values);
                Buckets.Clear();
            }

            for (int i = 0; i < buckets.Count; i++) {
                DisposeBucket(buckets[i]);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() {
            ClearAll();
            Interlocked.Exchange(ref mapAssetHits, 0);
            Interlocked.Exchange(ref mapAssetMisses, 0);
            Interlocked.Exchange(ref compiledGroundHits, 0);
            Interlocked.Exchange(ref compiledGroundMisses, 0);
            Interlocked.Exchange(ref rawModelHits, 0);
            Interlocked.Exchange(ref rawModelMisses, 0);
            Interlocked.Exchange(ref compiledModelHits, 0);
            Interlocked.Exchange(ref compiledModelMisses, 0);
            Interlocked.Exchange(ref compiledModelBuilds, 0);
            Interlocked.Exchange(ref compiledModelBuildMilliseconds, 0);
            Interlocked.Exchange(ref groundMeshHits, 0);
            Interlocked.Exchange(ref groundMeshMisses, 0);
            Interlocked.Exchange(ref groundMeshBuilds, 0);
            Interlocked.Exchange(ref groundAtlasHits, 0);
            Interlocked.Exchange(ref groundAtlasMisses, 0);
            Interlocked.Exchange(ref groundAtlasBuilds, 0);
            Interlocked.Exchange(ref waterMeshHits, 0);
            Interlocked.Exchange(ref waterMeshMisses, 0);
            Interlocked.Exchange(ref waterMeshBuilds, 0);
            Interlocked.Exchange(ref waterHits, 0);
            Interlocked.Exchange(ref waterMisses, 0);
            Interlocked.Exchange(ref waterBuilds, 0);
        }

        private static RSM GetOrLoadRawModel(RoAssetDatabase assets, string path, string filename, Func<RSM> loader) {
            string cacheKey = NormalizeKey(path);
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.RawModels.TryGetValue(cacheKey, out RSM cached)) {
                    Interlocked.Increment(ref rawModelHits);
                    return cached;
                }
            }

            Interlocked.Increment(ref rawModelMisses);
            RSM loaded = loader();
            if (loaded == null) {
                return null;
            }

            loaded.filename = filename;
            lock (Sync) {
                CacheBucket bucket = GetBucketLocked(assets);
                if (bucket.RawModels.TryGetValue(cacheKey, out RSM existing)) {
                    return existing;
                }

                bucket.RawModels[cacheKey] = loaded;
                return loaded;
            }
        }

        private static RSM.CompiledModel CreateCompiledModelView(
            RSM.CompiledModel cached,
            string filename,
            IReadOnlyList<RSW.ModelDescriptor> instances
        ) {
            if (cached?.rsm == null) {
                return null;
            }

            RSM source = cached.rsm;
            var view = new RSM {
                version = source.version,
                name = source.name,
                instances = CopyInstances(instances),
                textures = source.textures,
                shadeType = source.shadeType,
                alpha = source.alpha,
                animLen = source.animLen,
                nodes = source.nodes,
                mainNode = source.mainNode,
                box = source.box,
                volumeBoxes = source.volumeBoxes,
                posKeyframes = source.posKeyframes,
                isRsm2 = source.isRsm2,
                fps = source.fps,
                rootNames = source.rootNames,
                filename = string.IsNullOrWhiteSpace(filename) ? source.filename : filename
            };

            return new RSM.CompiledModel {
                rsm = view,
                nodesData = cached.nodesData
            };
        }

        private static List<RSW.ModelDescriptor> CopyInstances(IReadOnlyList<RSW.ModelDescriptor> instances) {
            var copy = new List<RSW.ModelDescriptor>(instances?.Count ?? 0);
            if (instances == null) {
                return copy;
            }

            for (int i = 0; i < instances.Count; i++) {
                copy.Add(instances[i]);
            }

            return copy;
        }

        private static string BuildGroundAtlasKey(GND.Mesh ground, FilterMode filterMode) {
            var builder = new StringBuilder();
            builder.Append("filter=");
            builder.Append(filterMode);
            builder.Append("|textures=");
            if (ground?.textures != null) {
                for (int i = 0; i < ground.textures.Length; i++) {
                    if (i > 0) {
                        builder.Append(';');
                    }

                    builder.Append(NormalizeKey(ground.textures[i]));
                }
            }

            return builder.ToString();
        }

        private static string BuildWaterKey(RSW.WaterInfo waterInfo) {
            var builder = new StringBuilder();
            builder.Append("type=");
            builder.Append(waterInfo != null ? waterInfo.type : -1);
            builder.Append("|size=");
            builder.Append(waterInfo != null ? waterInfo.textureSize : 0);
            builder.Append("|images=");
            if (waterInfo?.images != null) {
                for (int i = 0; i < waterInfo.images.Length; i++) {
                    if (i > 0) {
                        builder.Append(';');
                    }

                    builder.Append(NormalizeKey(waterInfo.images[i]));
                }
            }

            return builder.ToString();
        }

        private readonly struct GroundMeshCacheKey : IEquatable<GroundMeshCacheKey> {
            public GroundMeshCacheKey(GND.Mesh ground, RoGroundMeshBuilder.GroundTopDiagonalMode diagonalMode) {
                Ground = ground;
                DiagonalMode = diagonalMode;
            }

            private GND.Mesh Ground { get; }
            private RoGroundMeshBuilder.GroundTopDiagonalMode DiagonalMode { get; }

            public bool Equals(GroundMeshCacheKey other) =>
                ReferenceEquals(Ground, other.Ground) && DiagonalMode == other.DiagonalMode;

            public override bool Equals(object obj) => obj is GroundMeshCacheKey other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    return (RuntimeHelpers.GetHashCode(Ground) * 397) ^ (int) DiagonalMode;
                }
            }
        }

        private static CacheBucket GetBucketLocked(RoAssetDatabase assets) {
            if (!Buckets.TryGetValue(assets, out CacheBucket bucket)) {
                bucket = new CacheBucket();
                Buckets.Add(assets, bucket);
            }

            return bucket;
        }

        private static string NormalizeKey(string key) {
            return (key ?? string.Empty).Trim().Replace('\\', '/');
        }

        private static void DisposeBucket(CacheBucket bucket) {
            if (bucket == null) {
                return;
            }

            foreach (Mesh mesh in bucket.GroundMeshes.Values) {
                DestroyRuntimeObject(mesh);
            }

            foreach (Texture2D texture in bucket.GroundAtlases.Values) {
                DestroyRuntimeObject(texture);
            }

            foreach (Mesh mesh in bucket.WaterMeshes.Values) {
                DestroyRuntimeObject(mesh);
            }

            foreach (Texture2D[] textures in bucket.WaterTextures.Values) {
                DestroyWaterTextures(textures);
            }

            bucket.MapAssets.Clear();
            bucket.CompiledGround.Clear();
            bucket.RawModels.Clear();
            bucket.CompiledModels.Clear();
            bucket.GroundMeshes.Clear();
            bucket.GroundAtlases.Clear();
            bucket.WaterMeshes.Clear();
            bucket.WaterTextures.Clear();
        }

        private static void DestroyWaterTextures(Texture2D[] textures) {
            if (textures == null) {
                return;
            }

            var destroyed = new HashSet<Texture2D>();
            for (int i = 0; i < textures.Length; i++) {
                Texture2D texture = textures[i];
                if (texture == null || !destroyed.Add(texture)) {
                    continue;
                }

                if ((texture.hideFlags & HideFlags.DontSave) == 0) {
                    continue;
                }

                DestroyRuntimeObject(texture);
            }
        }

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

        private static void Mark(ProfilerMarker marker) {
            marker.Begin();
            marker.End();
        }
    }
}
