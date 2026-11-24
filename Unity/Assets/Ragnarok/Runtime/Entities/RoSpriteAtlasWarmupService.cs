using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ragnarok.Assets;
using Unity.Profiling;
using UnityEngine;

namespace Ragnarok.Entities {
    [DefaultExecutionOrder(10010)]
    public sealed class RoSpriteAtlasWarmupService : MonoBehaviour {
        private const int MaxBuildsPerFrame = 1;
        private const int MaxQueuedEntries = 256;
        private const float MinWarmupFrameRate = 55f;

        private static readonly Queue<WarmupRequest> WarmupQueue = new Queue<WarmupRequest>();
        private static readonly HashSet<WarmupKey> QueuedKeys = new HashSet<WarmupKey>();
        private static readonly ProfilerMarker BuildMarker = new ProfilerMarker("SpriteAtlasWarmup.Build");
        private static RoSpriteAtlasWarmupService instance;

        public static void Queue(RoAssetDatabase assets, string basePath, byte[] paletteOverride = null) {
            if (assets == null || string.IsNullOrWhiteSpace(basePath)) {
                return;
            }

            string normalizedBasePath = RoActorSpriteVisual.NormalizeBasePath(basePath);
            if (string.IsNullOrEmpty(normalizedBasePath) ||
                RoSpriteAtlasCache.TryGet(assets, normalizedBasePath, out _, paletteOverride)) {
                return;
            }

            if (!Application.isPlaying) {
                RoSpriteAtlasCache.TryGetOrBuild(assets, normalizedBasePath, out _, paletteOverride);
                return;
            }

            byte[] paletteCopy = CopyPalette(paletteOverride);
            var key = new WarmupKey(assets, normalizedBasePath, CalculatePaletteHash(paletteCopy));
            if (QueuedKeys.Contains(key) || WarmupQueue.Count >= MaxQueuedEntries) {
                return;
            }

            EnsureInstance();
            QueuedKeys.Add(key);
            WarmupQueue.Enqueue(new WarmupRequest {
                Key = key,
                Assets = assets,
                BasePath = normalizedBasePath,
                PaletteOverride = paletteCopy
            });
        }

        private void Update() {
            if (WarmupQueue.Count == 0) {
                return;
            }

            if (Time.unscaledDeltaTime > 0f &&
                Time.unscaledDeltaTime > 1f / MinWarmupFrameRate) {
                return;
            }

            int budget = MaxBuildsPerFrame;
            while (budget > 0 && WarmupQueue.Count > 0) {
                WarmupRequest request = WarmupQueue.Dequeue();
                QueuedKeys.Remove(request.Key);
                if (request.Assets == null ||
                    string.IsNullOrEmpty(request.BasePath) ||
                    RoSpriteAtlasCache.TryGet(request.Assets, request.BasePath, out _, request.PaletteOverride)) {
                    continue;
                }

                using (BuildMarker.Auto()) {
                    RoSpriteAtlasCache.TryGetOrBuild(
                        request.Assets,
                        request.BasePath,
                        out _,
                        request.PaletteOverride);
                }

                budget--;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() {
            WarmupQueue.Clear();
            QueuedKeys.Clear();
            instance = null;
        }

        private static void EnsureInstance() {
            if (instance != null) {
                return;
            }

            var gameObject = new GameObject("RO Sprite Atlas Warmup Service") {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(gameObject);
            instance = gameObject.AddComponent<RoSpriteAtlasWarmupService>();
        }

        private static byte[] CopyPalette(byte[] paletteOverride) {
            if (paletteOverride == null || paletteOverride.Length == 0) {
                return null;
            }

            var copy = new byte[paletteOverride.Length];
            Buffer.BlockCopy(paletteOverride, 0, copy, 0, paletteOverride.Length);
            return copy;
        }

        private static ulong CalculatePaletteHash(byte[] paletteOverride) {
            if (paletteOverride == null || paletteOverride.Length == 0) {
                return 0;
            }

            const ulong offset = 14695981039346656037ul;
            const ulong prime = 1099511628211ul;
            ulong hash = offset;
            for (int i = 0; i < paletteOverride.Length; i++) {
                hash ^= paletteOverride[i];
                hash *= prime;
            }

            return hash;
        }

        private sealed class WarmupRequest {
            public WarmupKey Key;
            public RoAssetDatabase Assets;
            public string BasePath;
            public byte[] PaletteOverride;
        }

        private readonly struct WarmupKey : IEquatable<WarmupKey> {
            public WarmupKey(RoAssetDatabase assets, string basePath, ulong paletteHash) {
                Assets = assets;
                BasePath = basePath ?? string.Empty;
                PaletteHash = paletteHash;
            }

            private RoAssetDatabase Assets { get; }
            private string BasePath { get; }
            private ulong PaletteHash { get; }

            public bool Equals(WarmupKey other) {
                return ReferenceEquals(Assets, other.Assets) &&
                    string.Equals(BasePath, other.BasePath, StringComparison.OrdinalIgnoreCase) &&
                    PaletteHash == other.PaletteHash;
            }

            public override bool Equals(object obj) => obj is WarmupKey other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    int hash = Assets != null ? RuntimeHelpers.GetHashCode(Assets) : 0;
                    hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(BasePath);
                    hash = (hash * 397) ^ PaletteHash.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
