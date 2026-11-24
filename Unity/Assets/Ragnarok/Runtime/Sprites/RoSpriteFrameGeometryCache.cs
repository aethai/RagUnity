using System;
using System.Collections.Generic;
using Ragnarok.Diagnostics;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Sprites {
    public readonly struct RoSpriteFrameGeometryKey : IEquatable<RoSpriteFrameGeometryKey> {
        public RoSpriteFrameGeometryKey(int atlasId, int actionIndex, int frameIndex, int compositionMode = 0, int renderMode = 0) {
            AtlasId = atlasId;
            ActionIndex = actionIndex;
            FrameIndex = frameIndex;
            CompositionMode = compositionMode;
            RenderMode = renderMode;
        }

        private int AtlasId { get; }
        private int ActionIndex { get; }
        private int FrameIndex { get; }
        private int CompositionMode { get; }
        private int RenderMode { get; }

        public bool Equals(RoSpriteFrameGeometryKey other) {
            return AtlasId == other.AtlasId &&
                ActionIndex == other.ActionIndex &&
                FrameIndex == other.FrameIndex &&
                CompositionMode == other.CompositionMode &&
                RenderMode == other.RenderMode;
        }

        public override bool Equals(object obj) => obj is RoSpriteFrameGeometryKey other && Equals(other);

        public override int GetHashCode() {
            unchecked {
                int hash = AtlasId;
                hash = (hash * 397) ^ ActionIndex;
                hash = (hash * 397) ^ FrameIndex;
                hash = (hash * 397) ^ CompositionMode;
                hash = (hash * 397) ^ RenderMode;
                return hash;
            }
        }
    }

    public static class RoSpriteFrameGeometryCache {
        // per SPR/ACT pair (palette variants share it): a town crowd uses ~120 pairs x ~90 frames (walk and stand in every
        // direction), past 8192; beyond the cap every animator builds and keeps its own copies, which costs more memory and
        // build time than one shared copy
        private const int MaxEntries = 32768;
        private const int MaxQueuedPrewarmEntries = 4096;
        private const float MinPrewarmFrameRate = 55f;
        /// <summary>Runtime frame-mesh builds get this much time per frame (and at least MinRuntimeBuildsPerFrame) whatever the frame rate: a
        /// frame-rate gate starved them in crowds (below 45 fps nothing was built and layers stayed invisible).</summary>
        private const double RuntimeBuildBudgetMs = 1.0;
        private const int MinRuntimeBuildsPerFrame = 2;
        private const int CrowdBuildThreshold = 48;

        private sealed class PrewarmRequest {
            public RoSpriteFrameGeometryKey Key;
            public ACT.Frame Frame;
            public RoSpriteAtlas Atlas;
        }

        private static readonly Dictionary<RoSpriteFrameGeometryKey, Mesh> RenderMeshes =
            new Dictionary<RoSpriteFrameGeometryKey, Mesh>();
        private static readonly Queue<PrewarmRequest> PrewarmQueue = new Queue<PrewarmRequest>();
        private static readonly HashSet<RoSpriteFrameGeometryKey> QueuedPrewarmKeys =
            new HashSet<RoSpriteFrameGeometryKey>();
        private static int prewarmFrame = -1;
        private static int runtimeBuildFrame = -1;
        private static int runtimeBuildsThisFrame;
        private static long runtimeBuildTicksThisFrame;
        private static readonly long RuntimeBuildBudgetTicks = (long) (RuntimeBuildBudgetMs * System.Diagnostics.Stopwatch.Frequency / 1000.0);

        public static bool PrewarmEnabled { get; set; } = true;
        public static int MaxPrewarmBuildsPerFrame { get; set; } = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration() {
            Clear();
            PrewarmEnabled = true;
            MaxPrewarmBuildsPerFrame = 1;
            prewarmFrame = -1;
            runtimeBuildFrame = -1;
            runtimeBuildsThisFrame = 0;
        }

        public static bool TryGetRenderMesh(RoSpriteFrameGeometryKey key, out Mesh mesh) {
            if (RenderMeshes.TryGetValue(key, out mesh) && mesh != null) {
                RoRuntimePerfCounters.IncrementSpriteGeometryCacheHit();
                return true;
            }

            RoRuntimePerfCounters.IncrementSpriteGeometryCacheMiss();
            mesh = null;
            return false;
        }

        public static bool StoreRenderMesh(RoSpriteFrameGeometryKey key, Mesh mesh) {
            if (mesh == null) {
                return false;
            }

            if (RenderMeshes.Count >= MaxEntries && !RenderMeshes.ContainsKey(key)) {
                return false;
            }

            RenderMeshes[key] = mesh;
            QueuedPrewarmKeys.Remove(key);
            return true;
        }

        public static void EnqueueRenderMeshPrewarm(RoSpriteFrameGeometryKey key, ACT.Frame frame, RoSpriteAtlas atlas) {
            if (!PrewarmEnabled || frame == null || atlas == null || RenderMeshes.ContainsKey(key) || QueuedPrewarmKeys.Contains(key)) {
                return;
            }

            if (PrewarmQueue.Count >= MaxQueuedPrewarmEntries) {
                return;
            }

            QueuedPrewarmKeys.Add(key);
            PrewarmQueue.Enqueue(new PrewarmRequest {
                Key = key,
                Frame = frame,
                Atlas = atlas
            });
            RoRuntimePerfCounters.AddSpriteGeometryPrewarmQueued(1);
        }

        public static void ProcessPrewarmBudget() {
            if (!PrewarmEnabled || MaxPrewarmBuildsPerFrame <= 0 || PrewarmQueue.Count == 0) {
                return;
            }

            if (RoActorTickService.RegisteredAnimatorUpdateTickCount >= CrowdBuildThreshold) {
                return;
            }

            if (Application.isPlaying &&
                Time.unscaledDeltaTime > 0f &&
                Time.unscaledDeltaTime > 1f / MinPrewarmFrameRate) {
                return;
            }

            int frame = Application.isPlaying ? Time.frameCount : -1;
            if (frame == prewarmFrame) {
                return;
            }

            prewarmFrame = frame;
            int budget = Math.Max(1, MaxPrewarmBuildsPerFrame);
            while (budget > 0 && PrewarmQueue.Count > 0) {
                PrewarmRequest request = PrewarmQueue.Dequeue();
                QueuedPrewarmKeys.Remove(request.Key);
                if (request.Frame == null || request.Atlas == null || RenderMeshes.ContainsKey(request.Key)) {
                    continue;
                }

                Mesh mesh = RoSpriteMeshBuilder.Build(request.Frame, request.Atlas);
                if (!StoreRenderMesh(request.Key, mesh)) {
                    DestroyRuntimeObject(mesh);
                    continue;
                }

                RoRuntimePerfCounters.IncrementSpriteFrameMeshBuild();
                RoRuntimePerfCounters.IncrementSpriteGeometryPrewarmBuilt();
                budget--;
            }
        }

        public static bool TryReserveRuntimeMeshBuild() {
            if (!Application.isPlaying) {
                return true;
            }

            int frame = Time.frameCount;
            if (runtimeBuildFrame != frame) {
                runtimeBuildFrame = frame;
                runtimeBuildsThisFrame = 0;
                runtimeBuildTicksThisFrame = 0;
            }

            if (runtimeBuildsThisFrame >= MinRuntimeBuildsPerFrame && runtimeBuildTicksThisFrame >= RuntimeBuildBudgetTicks) {
                return false;
            }

            runtimeBuildsThisFrame++;
            return true;
        }

        /// <summary>The time a reserved build took (counts against this frame's build budget).</summary>
        public static void AddRuntimeMeshBuildTicks(long ticks) {
            runtimeBuildTicksThisFrame += ticks;
        }

        public static void Clear() {
            foreach (Mesh mesh in RenderMeshes.Values) {
                DestroyRuntimeObject(mesh);
            }

            RenderMeshes.Clear();
            PrewarmQueue.Clear();
            QueuedPrewarmKeys.Clear();
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
    }
}
