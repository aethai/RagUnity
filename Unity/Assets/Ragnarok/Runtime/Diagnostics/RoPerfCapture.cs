using System;
using System.Globalization;
using System.IO;
using System.Text;
using Ragnarok.Effects;
using Ragnarok.UI;
using Ragnarok.World;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Fixed-window runtime performance capture for Play Mode. Samples scalar profiler counters once per
    /// frame into a preallocated buffer and writes report.md, samples.csv, and summary.json at the end.
    /// </summary>
    public sealed class RoPerfCapture : MonoBehaviour {
        private const int MaxBufferedFramesPerSecond = 300;
        private const int TopSpikeFrameCount = 20;
        private const long UnavailableLong = -1L;
        private const int UnavailableInt = -1;

        private const int MetricFrameTimeNs = 0;
        private const int MetricMainThreadNs = 1;
        private const int MetricRenderThreadNs = 2;
        private const int MetricGcAllocBytes = 3;
        private const int MetricTotalUsedMemory = 4;
        private const int MetricSystemUsedMemory = 5;
        private const int MetricGcReservedMemory = 6;
        private const int MetricDrawCalls = 7;
        private const int MetricBatches = 8;
        private const int MetricSetPassCalls = 9;
        private const int MetricTriangles = 10;
        private const int MetricVertices = 11;

        private enum MetricKind {
            TimeNs,
            Bytes,
            Count
        }

        private readonly struct RecorderSpec {
            public RecorderSpec(ProfilerCategory category, string statName) {
                Category = category;
                StatName = statName;
            }

            public ProfilerCategory Category { get; }
            public string StatName { get; }
        }

        private readonly struct MetricDefinition {
            public MetricDefinition(
                string key,
                string displayName,
                string unit,
                MetricKind kind,
                RecorderSpec spec0
            ) : this(key, displayName, unit, kind, spec0, default, 1) {
            }

            public MetricDefinition(
                string key,
                string displayName,
                string unit,
                MetricKind kind,
                RecorderSpec spec0,
                RecorderSpec spec1
            ) : this(key, displayName, unit, kind, spec0, spec1, 2) {
            }

            private MetricDefinition(
                string key,
                string displayName,
                string unit,
                MetricKind kind,
                RecorderSpec spec0,
                RecorderSpec spec1,
                int specCount
            ) {
                Key = key;
                DisplayName = displayName;
                Unit = unit;
                Kind = kind;
                Spec0 = spec0;
                Spec1 = spec1;
                SpecCount = specCount;
            }

            public string Key { get; }
            public string DisplayName { get; }
            public string Unit { get; }
            public MetricKind Kind { get; }
            public RecorderSpec Spec0 { get; }
            public RecorderSpec Spec1 { get; }
            public int SpecCount { get; }

            public RecorderSpec GetSpec(int index) => index == 0 ? Spec0 : Spec1;
        }

        private sealed class MetricRecorder {
            public MetricDefinition Definition;
            public ProfilerRecorder Recorder;
            public bool Available;
            public string ActiveStatName = string.Empty;
        }

        private readonly struct CounterDefinition {
            public CounterDefinition(string key, string status, string source) {
                Key = key;
                Status = status;
                Source = source;
            }

            public string Key { get; }
            public string Status { get; }
            public string Source { get; }
        }

        private struct FrameSample {
            public int FrameIndex;
            public float TimestampSeconds;
            public float DeltaTimeMs;
            public float Fps;
            public long FrameTimeNs;
            public long MainThreadNs;
            public long RenderThreadNs;
            public long GcAllocBytes;
            public long TotalUsedMemory;
            public long SystemUsedMemory;
            public long GcReservedMemory;
            public long DrawCalls;
            public long Batches;
            public long SetPassCalls;
            public long Triangles;
            public long Vertices;
            public int ActiveActors;
            public int ActiveMobs;
            public int ActiveEffects;
            public int ActiveDamageNumbers;
            public int ActiveUiWindows;
            public long PacketsProcessed;
            public long SnapshotsBuilt;
            public long AssetLoads;
            public long AssetLoadBytes;
            public long ActorOverlaySpriteCacheHits;
            public long ActorOverlaySpriteCacheMisses;
            public long ActorOverlaySpriteCacheBuildFailures;
            public long SpriteAnimatorApplyFrameCalls;
            public long SpriteAnimatorApplyFrameTicks;
            public long SpriteAnimatorMeshAssigns;
            public long SpriteAnimatorMeshAssignTicks;
            public long SpriteAnimatorTextureAssigns;
            public long SpriteAnimatorTextureAssignTicks;
            public long SpriteAnimatorMutableMeshUpdates;
            public long SpriteAnimatorMutableMeshUpdateTicks;
            public long SpriteFrameMeshBuilds;
            public long SpriteCompositedFrameBuilds;
            public long SpriteGeometryCacheHits;
            public long SpriteGeometryCacheMisses;
            public long SpriteGeometryPrewarmQueued;
            public long SpriteGeometryPrewarmBuilt;
            public long SpriteMaterialCacheHits;
            public long SpriteMaterialCacheMisses;
            public long ActorOverlayUpdates;
            public long ActorOverlayUpdateTicks;
            public long ActorOverlayApplyFrameCalls;
            public long ActorOverlayApplyFrameTicks;
            public long SpriteEffectUpdates;
            public long SpriteEffectUpdateTicks;
            public long SpriteEffectLateUpdates;
            public long SpriteEffectLateUpdateTicks;
            public long PrimitiveEffectUpdates;
            public long PrimitiveEffectUpdateTicks;
            public long ActorSortingUpdates;
            public long ActorSortingUpdateTicks;
            public long SpawnedObjects;
            public long DespawnedObjects;
            public long PathfindingRequests;
            public long PointerResolveTargets;
            public long PointerSyncTransforms;
            public int ActiveSpritePointerShapes;
            public int ActiveSpritePointerColliders;
            public long SpritePointerBroadphaseChecks;
            public long SpritePointerExactTests;
            public long SpritePointerAlphaTests;
            public long SpritePointerCacheHits;
            public long SpritePointerCacheMisses;
            public long SpritePointerColliderRaycasts;
            public long SpritePointerColliderHits;
            public long SpritePointerColliderRefreshes;
            public long PointerResolveTicks;
            public long PointerWorldTargetsTicks;
            public long PointerColliderRefreshTicks;
            public long PointerColliderRefreshNowTicks;
            public long PointerRaycastTicks;
            public long PointerGroundPointTicks;
            public long PointerResolverTicks;
            public long ClickToMoveUpdateTicks;
            public long ClickHandleTicks;
            public long ClickMoveToTargetTicks;
            public long UiPointerCaptureTicks;
            public long CursorSetPointerTargetTicks;
            public long CursorVisualLateUpdateTicks;
            public long CursorVisualApplyPointerTicks;
            public long PointerScreenReportGuiTicks;
            public long ClickIntentResolveTicks;
            public long ActorServiceUpdateTicks;
            public long ActorServiceLateUpdateTicks;
            public long EffectManagerUpdateTicks;
            public long MinimalLoginShellUpdateTicks;
            public long MinimalLoginShellStableLightPathTicks;
            public long MinimalLoginShellApplyLiveDrainTicks;
            public long MinimalLoginShellApplyWorldDrainTicks;
            public long MinimalLoginShellApplyLiveDrainPostSyncTicks;
            public long MinimalLoginShellEnsurePhase12Ticks;
            public long MinimalLoginShellEnsureGameplayContextTicks;
            public long MinimalLoginShellRefreshGameplayWindowsTicks;
            public long MinimalLoginShellScheduleUiRefreshTicks;
            public long OnlineWorldUpdateTicks;
            public long CameraLateUpdateTicks;
            public long GridSelectorUpdateTicks;
            public long RuntimeMapLateUpdateTicks;
            public long OnGuiCalls;
            public long OnGuiLayoutEvents;
            public long OnGuiRepaintEvents;
            public long OnGuiMouseMoveEvents;
            public long OnGuiTicks;
            public long MinimalLoginShellOnGuiTicks;
            public long ActorHoverLabelOnGuiTicks;
            public long ActorShowScriptOnGuiTicks;
            public long ActorLightingDebugOnGuiTicks;
            public long ShortcutSettingsOnGuiTicks;
            public long ClickToMoveOnGuiTicks;
            public long CursorVisualOnGuiTicks;
            public long GridSelectorOnGuiTicks;
        }

        private struct CaptureAggregates {
            public int FrameCount;
            public double ActualDurationSeconds;
            public double AverageFrameMs;
            public double AverageFps;
            public double AverageProfilerFrameTimeMs;
            public double MaxProfilerFrameTimeMs;
            public int MaxProfilerFrameTimeFrameIndex;
            public double OnePercentLowFps;
            public double PointOnePercentLowFps;
            public float WorstFrameMs;
            public int WorstFrameIndex;
            public long MaxGcAllocBytes;
            public int MaxGcAllocFrameIndex;
            public long MemoryStartBytes;
            public long MemoryPeakBytes;
            public long SystemMemoryPeakBytes;
            public long GcReservedMemoryPeakBytes;
            public double MaxMainThreadMs;
            public double MaxRenderThreadMs;
            public long PointerResolveTargetsTotal;
            public long PointerSyncTransformsTotal;
            public long SpritePointerBroadphaseChecksTotal;
            public long SpritePointerExactTestsTotal;
            public long SpritePointerAlphaTestsTotal;
            public long SpritePointerCacheHitsTotal;
            public long SpritePointerCacheMissesTotal;
            public long SpritePointerColliderRaycastsTotal;
            public long SpritePointerColliderHitsTotal;
            public long SpritePointerColliderRefreshesTotal;
            public long PointerResolveTicksTotal;
            public long PointerWorldTargetsTicksTotal;
            public long PointerColliderRefreshTicksTotal;
            public long PointerColliderRefreshNowTicksTotal;
            public long PointerRaycastTicksTotal;
            public long PointerGroundPointTicksTotal;
            public long PointerResolverTicksTotal;
            public long ClickToMoveUpdateTicksTotal;
            public long ClickHandleTicksTotal;
            public long ClickMoveToTargetTicksTotal;
            public long UiPointerCaptureTicksTotal;
            public long CursorSetPointerTargetTicksTotal;
            public long CursorVisualLateUpdateTicksTotal;
            public long CursorVisualApplyPointerTicksTotal;
            public long PointerScreenReportGuiTicksTotal;
            public long ClickIntentResolveTicksTotal;
            public long ActorOverlaySpriteCacheHitsTotal;
            public long ActorOverlaySpriteCacheMissesTotal;
            public long ActorOverlaySpriteCacheBuildFailuresTotal;
            public long SpriteAnimatorApplyFrameCallsTotal;
            public long SpriteAnimatorApplyFrameTicksTotal;
            public long SpriteAnimatorMeshAssignsTotal;
            public long SpriteAnimatorMeshAssignTicksTotal;
            public long SpriteAnimatorTextureAssignsTotal;
            public long SpriteAnimatorTextureAssignTicksTotal;
            public long SpriteAnimatorMutableMeshUpdatesTotal;
            public long SpriteAnimatorMutableMeshUpdateTicksTotal;
            public long SpriteFrameMeshBuildsTotal;
            public long SpriteCompositedFrameBuildsTotal;
            public long SpriteGeometryCacheHitsTotal;
            public long SpriteGeometryCacheMissesTotal;
            public long SpriteGeometryPrewarmQueuedTotal;
            public long SpriteGeometryPrewarmBuiltTotal;
            public long SpriteMaterialCacheHitsTotal;
            public long SpriteMaterialCacheMissesTotal;
            public long ActorOverlayUpdatesTotal;
            public long ActorOverlayUpdateTicksTotal;
            public long ActorOverlayApplyFrameCallsTotal;
            public long ActorOverlayApplyFrameTicksTotal;
            public long SpriteEffectUpdatesTotal;
            public long SpriteEffectUpdateTicksTotal;
            public long SpriteEffectLateUpdatesTotal;
            public long SpriteEffectLateUpdateTicksTotal;
            public long PrimitiveEffectUpdatesTotal;
            public long PrimitiveEffectUpdateTicksTotal;
            public long ActorSortingUpdatesTotal;
            public long ActorSortingUpdateTicksTotal;
            public long ActorServiceUpdateTicksTotal;
            public long ActorServiceLateUpdateTicksTotal;
            public long EffectManagerUpdateTicksTotal;
            public long MinimalLoginShellUpdateTicksTotal;
            public long MinimalLoginShellStableLightPathTicksTotal;
            public long MinimalLoginShellApplyLiveDrainTicksTotal;
            public long MinimalLoginShellApplyWorldDrainTicksTotal;
            public long MinimalLoginShellApplyLiveDrainPostSyncTicksTotal;
            public long MinimalLoginShellEnsurePhase12TicksTotal;
            public long MinimalLoginShellEnsureGameplayContextTicksTotal;
            public long MinimalLoginShellRefreshGameplayWindowsTicksTotal;
            public long MinimalLoginShellScheduleUiRefreshTicksTotal;
            public long OnlineWorldUpdateTicksTotal;
            public long CameraLateUpdateTicksTotal;
            public long GridSelectorUpdateTicksTotal;
            public long RuntimeMapLateUpdateTicksTotal;
            public long OnGuiCallsTotal;
            public long OnGuiLayoutEventsTotal;
            public long OnGuiRepaintEventsTotal;
            public long OnGuiMouseMoveEventsTotal;
            public long OnGuiTicksTotal;
            public long MinimalLoginShellOnGuiTicksTotal;
            public long ActorHoverLabelOnGuiTicksTotal;
            public long ActorShowScriptOnGuiTicksTotal;
            public long ActorLightingDebugOnGuiTicksTotal;
            public long ShortcutSettingsOnGuiTicksTotal;
            public long ClickToMoveOnGuiTicksTotal;
            public long CursorVisualOnGuiTicksTotal;
            public long GridSelectorOnGuiTicksTotal;
            public long MaxPointerResolveTargetsFrame;
            public long MaxPointerSyncTransformsFrame;
            public long MaxSpritePointerBroadphaseChecksFrame;
            public long MaxSpritePointerExactTestsFrame;
            public long MaxSpritePointerAlphaTestsFrame;
            public long MaxSpritePointerCacheHitsFrame;
            public long MaxSpritePointerCacheMissesFrame;
            public long MaxSpritePointerColliderRaycastsFrame;
            public long MaxSpritePointerColliderHitsFrame;
            public long MaxSpritePointerColliderRefreshesFrame;
            public long MaxPointerResolveTicksFrame;
            public long MaxPointerWorldTargetsTicksFrame;
            public long MaxPointerColliderRefreshTicksFrame;
            public long MaxPointerColliderRefreshNowTicksFrame;
            public long MaxPointerRaycastTicksFrame;
            public long MaxPointerGroundPointTicksFrame;
            public long MaxPointerResolverTicksFrame;
            public long MaxClickToMoveUpdateTicksFrame;
            public long MaxClickHandleTicksFrame;
            public long MaxClickMoveToTargetTicksFrame;
            public long MaxUiPointerCaptureTicksFrame;
            public long MaxCursorSetPointerTargetTicksFrame;
            public long MaxCursorVisualLateUpdateTicksFrame;
            public long MaxCursorVisualApplyPointerTicksFrame;
            public long MaxPointerScreenReportGuiTicksFrame;
            public long MaxClickIntentResolveTicksFrame;
            public long MaxActorOverlaySpriteCacheHitsFrame;
            public long MaxActorOverlaySpriteCacheMissesFrame;
            public long MaxActorOverlaySpriteCacheBuildFailuresFrame;
            public long MaxSpriteAnimatorApplyFrameCallsFrame;
            public long MaxSpriteAnimatorApplyFrameTicksFrame;
            public long MaxSpriteAnimatorMeshAssignsFrame;
            public long MaxSpriteAnimatorMeshAssignTicksFrame;
            public long MaxSpriteAnimatorTextureAssignsFrame;
            public long MaxSpriteAnimatorTextureAssignTicksFrame;
            public long MaxSpriteAnimatorMutableMeshUpdatesFrame;
            public long MaxSpriteAnimatorMutableMeshUpdateTicksFrame;
            public long MaxSpriteFrameMeshBuildsFrame;
            public long MaxSpriteCompositedFrameBuildsFrame;
            public long MaxSpriteGeometryCacheHitsFrame;
            public long MaxSpriteGeometryCacheMissesFrame;
            public long MaxSpriteGeometryPrewarmQueuedFrame;
            public long MaxSpriteGeometryPrewarmBuiltFrame;
            public long MaxSpriteMaterialCacheHitsFrame;
            public long MaxSpriteMaterialCacheMissesFrame;
            public long MaxActorOverlayUpdatesFrame;
            public long MaxActorOverlayUpdateTicksFrame;
            public long MaxActorOverlayApplyFrameCallsFrame;
            public long MaxActorOverlayApplyFrameTicksFrame;
            public long MaxSpriteEffectUpdatesFrame;
            public long MaxSpriteEffectUpdateTicksFrame;
            public long MaxSpriteEffectLateUpdatesFrame;
            public long MaxSpriteEffectLateUpdateTicksFrame;
            public long MaxPrimitiveEffectUpdatesFrame;
            public long MaxPrimitiveEffectUpdateTicksFrame;
            public long MaxActorSortingUpdatesFrame;
            public long MaxActorSortingUpdateTicksFrame;
            public long MaxActorServiceUpdateTicksFrame;
            public long MaxActorServiceLateUpdateTicksFrame;
            public long MaxEffectManagerUpdateTicksFrame;
            public long MaxMinimalLoginShellUpdateTicksFrame;
            public long MaxMinimalLoginShellStableLightPathTicksFrame;
            public long MaxMinimalLoginShellApplyLiveDrainTicksFrame;
            public long MaxMinimalLoginShellApplyWorldDrainTicksFrame;
            public long MaxMinimalLoginShellApplyLiveDrainPostSyncTicksFrame;
            public long MaxMinimalLoginShellEnsurePhase12TicksFrame;
            public long MaxMinimalLoginShellEnsureGameplayContextTicksFrame;
            public long MaxMinimalLoginShellRefreshGameplayWindowsTicksFrame;
            public long MaxMinimalLoginShellScheduleUiRefreshTicksFrame;
            public long MaxOnlineWorldUpdateTicksFrame;
            public long MaxCameraLateUpdateTicksFrame;
            public long MaxGridSelectorUpdateTicksFrame;
            public long MaxRuntimeMapLateUpdateTicksFrame;
            public long MaxOnGuiCallsFrame;
            public long MaxOnGuiLayoutEventsFrame;
            public long MaxOnGuiRepaintEventsFrame;
            public long MaxOnGuiMouseMoveEventsFrame;
            public long MaxOnGuiTicksFrame;
            public long MaxMinimalLoginShellOnGuiTicksFrame;
            public long MaxActorHoverLabelOnGuiTicksFrame;
            public long MaxActorShowScriptOnGuiTicksFrame;
            public long MaxActorLightingDebugOnGuiTicksFrame;
            public long MaxShortcutSettingsOnGuiTicksFrame;
            public long MaxClickToMoveOnGuiTicksFrame;
            public long MaxCursorVisualOnGuiTicksFrame;
            public long MaxGridSelectorOnGuiTicksFrame;
            public int MaxActiveSpritePointerShapes;
            public int MaxActiveSpritePointerColliders;
            public string Classification;
        }

        private struct AssetLoadFrameSummary {
            public int FrameIndex;
            public int Count;
            public long Bytes;
            public string Path0;
            public string Path1;
            public string Path2;
            public int ExtraPathCount;
        }

        private static readonly MetricDefinition[] MetricDefinitions = {
            new MetricDefinition("frameTimeNs", "Frame time / elapsed nanoseconds", "ns", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Internal, "Frame Time")),
            new MetricDefinition("mainThreadMs", "Main thread time", "ms", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Internal, "Main Thread")),
            new MetricDefinition("renderThreadMs", "Render thread time", "ms", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Render, "Render Thread"),
                new RecorderSpec(ProfilerCategory.Internal, "Render Thread")),
            new MetricDefinition("gcAllocBytes", "GC allocated in frame", "bytes", MetricKind.Bytes,
                new RecorderSpec(ProfilerCategory.Memory, "GC Allocated In Frame")),
            new MetricDefinition("totalUsedMemory", "Total used memory", "bytes", MetricKind.Bytes,
                new RecorderSpec(ProfilerCategory.Memory, "Total Used Memory")),
            new MetricDefinition("systemUsedMemory", "System used memory", "bytes", MetricKind.Bytes,
                new RecorderSpec(ProfilerCategory.Memory, "System Used Memory")),
            new MetricDefinition("gcReservedMemory", "GC reserved memory", "bytes", MetricKind.Bytes,
                new RecorderSpec(ProfilerCategory.Memory, "GC Reserved Memory")),
            new MetricDefinition("drawCalls", "Draw calls", "count", MetricKind.Count,
                new RecorderSpec(ProfilerCategory.Render, "Draw Calls Count")),
            new MetricDefinition("batches", "Batches", "count", MetricKind.Count,
                new RecorderSpec(ProfilerCategory.Render, "Batches Count")),
            new MetricDefinition("setPassCalls", "SetPass calls", "count", MetricKind.Count,
                new RecorderSpec(ProfilerCategory.Render, "SetPass Calls Count")),
            new MetricDefinition("triangles", "Triangles", "count", MetricKind.Count,
                new RecorderSpec(ProfilerCategory.Render, "Triangles Count")),
            new MetricDefinition("vertices", "Vertices", "count", MetricKind.Count,
                new RecorderSpec(ProfilerCategory.Render, "Vertices Count")),
            // the systems behind spikes in a player build (the CPU frame alone does not say which)
            new MetricDefinition("fxAdvanceMs", "Classic effects advance", "ms", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Scripts, "RO.ClassicFx.Advance"),
                new RecorderSpec(ProfilerCategory.Internal, "RO.ClassicFx.Advance")),
            new MetricDefinition("fxRenderMs", "Classic effects render", "ms", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Scripts, "RO.ClassicFx.Render"),
                new RecorderSpec(ProfilerCategory.Internal, "RO.ClassicFx.Render")),
            new MetricDefinition("fxDrawMs", "Classic effects draw", "ms", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Scripts, "RO.ClassicFx.Draw"),
                new RecorderSpec(ProfilerCategory.Internal, "RO.ClassicFx.Draw")),
            new MetricDefinition("uiRefreshMs", "Classic UI ticks", "ms", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Scripts, "RO.UI.Classic.Ticks"),
                new RecorderSpec(ProfilerCategory.Internal, "RO.UI.Classic.Ticks")),
            new MetricDefinition("atlasBuildMs", "Sprite atlas build", "ms", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Scripts, "SpriteAtlasCache.Build"),
                new RecorderSpec(ProfilerCategory.Internal, "SpriteAtlasCache.Build")),
            new MetricDefinition("actorSyncMs", "Actor presentation sync", "ms", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Scripts, "RO.Actor.Presentation.ApplyDirty"),
                new RecorderSpec(ProfilerCategory.Internal, "RO.Actor.Presentation.ApplyDirty")),
            new MetricDefinition("gcCollectMs", "GC collect", "ms", MetricKind.TimeNs,
                new RecorderSpec(ProfilerCategory.Memory, "GC.Collect"),
                new RecorderSpec(ProfilerCategory.Internal, "GC.Collect")),
        };

        private static readonly CounterDefinition[] CounterDefinitions = {
            new CounterDefinition("activeActors", "measured", "RoOnlineWorldState.ActorCount; blank when no online world is active."),
            new CounterDefinition("activeMobs", "measured", "RoOnlineWorldState.MonsterLikeActorCount; monster/pet/homunculus/mercenary/elemental taxonomy."),
            new CounterDefinition("activeEffects", "measured", "RoEffectManager.ActiveEffectCount; excludes damage numbers but includes actor overlays and weather layers."),
            new CounterDefinition("activeDamageNumbers", "measured", "RoClassicEffectBridge.MsgEffectCount (live CMsgEffect glyphs)."),
            new CounterDefinition("activeUIWindows", "measured", "Visible classic UI windows; blank when the classic UI is not up."),
            new CounterDefinition("packetsProcessed", "measured", "RoLoginShellSession.HandleLiveReceiveDrain packet count delta."),
            new CounterDefinition("snapshotsBuilt", "measured", "RoRuntimePerfCounters.IncrementSnapshot delta."),
            new CounterDefinition("assetLoads", "measured", "RoAssetDatabase successful source ReadBytes delta."),
            new CounterDefinition("assetLoadBytes", "measured", "RoAssetDatabase successful source ReadBytes byte count delta."),
            new CounterDefinition("actorOverlaySpriteCacheHits", "measured", "RoActorOverlayRenderer reused a cached actor overlay SPR/ACT/atlas entry."),
            new CounterDefinition("actorOverlaySpriteCacheMisses", "measured", "RoActorOverlayRenderer had to build or request an actor overlay SPR/ACT/atlas cache entry."),
            new CounterDefinition("actorOverlaySpriteCacheBuildFailures", "measured", "RoActorOverlayRenderer could not load/build the requested actor overlay sprite cache entry."),
            new CounterDefinition("spriteAnimatorApplyFrameCalls", "measured", "RoActSpriteAnimator.ApplyFrame call delta while performance capture timing is enabled."),
            new CounterDefinition("spriteAnimatorApplyFrameMs", "measured", "Stopwatch timing for RoActSpriteAnimator.ApplyFrame."),
            new CounterDefinition("spriteAnimatorMeshAssigns", "measured", "RoActSpriteAnimator meshFilter.sharedMesh assignment delta."),
            new CounterDefinition("spriteAnimatorMeshAssignMs", "measured", "Stopwatch timing for RoActSpriteAnimator meshFilter.sharedMesh assignments."),
            new CounterDefinition("spriteAnimatorTextureAssigns", "measured", "RoActSpriteAnimator material texture/shared-material assignment delta."),
            new CounterDefinition("spriteAnimatorTextureAssignMs", "measured", "Stopwatch timing for RoActSpriteAnimator texture/material assignment."),
            new CounterDefinition("spriteAnimatorMutableMeshUpdates", "measured", "RoActSpriteAnimator mutable runtime mesh copy delta."),
            new CounterDefinition("spriteAnimatorMutableMeshUpdateMs", "measured", "Stopwatch timing for RoActSpriteAnimator mutable runtime mesh copies."),
            new CounterDefinition("spriteFrameMeshBuilds", "measured", "RoSpriteFrameGeometryCache/RoActSpriteAnimator render mesh builds."),
            new CounterDefinition("spriteCompositedFrameBuilds", "measured", "RoActSpriteAnimator composited frame builds."),
            new CounterDefinition("spriteGeometryCacheHits", "measured", "RoSpriteFrameGeometryCache render mesh cache hit delta."),
            new CounterDefinition("spriteGeometryCacheMisses", "measured", "RoSpriteFrameGeometryCache render mesh cache miss delta."),
            new CounterDefinition("spriteGeometryPrewarmQueued", "measured", "RoSpriteFrameGeometryCache render mesh prewarm requests queued."),
            new CounterDefinition("spriteGeometryPrewarmBuilt", "measured", "RoSpriteFrameGeometryCache render mesh prewarm builds completed."),
            new CounterDefinition("spriteMaterialCacheHits", "measured", "RoSpriteMaterialCache hit delta."),
            new CounterDefinition("spriteMaterialCacheMisses", "measured", "RoSpriteMaterialCache miss delta."),
            new CounterDefinition("actorOverlayUpdates", "measured", "RoActorOverlayRenderer.Update call delta."),
            new CounterDefinition("actorOverlayUpdateMs", "measured", "Stopwatch timing for RoActorOverlayRenderer.Update."),
            new CounterDefinition("actorOverlayApplyFrameMs", "measured", "Stopwatch timing for RoActorOverlayRenderer.ApplyFrame."),
            new CounterDefinition("spriteEffectUpdates", "measured", "RoSpriteEffectRenderer.Update call delta."),
            new CounterDefinition("spriteEffectUpdateMs", "measured", "Stopwatch timing for RoSpriteEffectRenderer.Update."),
            new CounterDefinition("spriteEffectLateUpdateMs", "measured", "Stopwatch timing for RoSpriteEffectRenderer.LateUpdate."),
            new CounterDefinition("primitiveEffectUpdates", "measured", "RoPrimitiveEffectRenderer.Update call delta."),
            new CounterDefinition("primitiveEffectUpdateMs", "measured", "Stopwatch timing for RoPrimitiveEffectRenderer.Update."),
            new CounterDefinition("actorSortingUpdates", "measured", "RoOnlineActorPresenter dynamic actor sorting update delta."),
            new CounterDefinition("actorSortingUpdateMs", "measured", "Stopwatch timing for RoOnlineActorPresenter actor sorting updates."),
            new CounterDefinition("spawnedObjects", "placeholder", "Counts actor-state creates plus selected effect/damage spawns; not every GameObject path yet."),
            new CounterDefinition("despawnedObjects", "placeholder", "Counts actor-state releases; effect pool returns and object destroys need wider instrumentation."),
            new CounterDefinition("pathfindingRequests", "needs instrumentation", "No central path request boundary is instrumented in this patch."),
            new CounterDefinition("pointerResolveTargets", "measured", "RoClickToMoveController.ResolvePointerTarget call delta; expected to rise while pointer is over gameplay."),
            new CounterDefinition("pointerSyncTransforms", "measured", "Physics.SyncTransforms calls triggered by pointer target collider refresh."),
            new CounterDefinition("activeSpritePointerShapes", "measured", "RoSpritePointerTargetShape.ActiveShapes count sampled per frame."),
            new CounterDefinition("activeSpritePointerColliders", "measured", "RoSpritePointerBoundsCollider active collider count sampled per frame."),
            new CounterDefinition("spritePointerBroadphaseChecks", "measured", "RoSpritePointerTargetShape screen-bounds broadphase checks delta."),
            new CounterDefinition("spritePointerExactTests", "measured", "RoSpritePointerTargetShape mesh/triangle exact hit tests delta."),
            new CounterDefinition("spritePointerAlphaTests", "measured", "Alpha mask checks during alpha-perfect pointer targeting delta."),
            new CounterDefinition("spritePointerCacheHits", "measured", "Sprite pointer target cache hit delta."),
            new CounterDefinition("spritePointerCacheMisses", "measured", "Sprite pointer target cache miss delta."),
            new CounterDefinition("spritePointerColliderRaycasts", "measured", "Physics.RaycastNonAlloc calls for sprite pointer collider path delta."),
            new CounterDefinition("spritePointerColliderHits", "measured", "Accepted sprite pointer collider hits delta."),
            new CounterDefinition("spritePointerColliderRefreshes", "measured", "RoSpritePointerBoundsCollider.RefreshNow calls delta."),
            new CounterDefinition("pointerResolveMs", "measured", "Stopwatch timing for RoClickToMoveController.ResolvePointerTarget."),
            new CounterDefinition("pointerWorldTargetsMs", "measured", "Stopwatch timing for RoClickToMoveController.CollectWorldPointerTargets."),
            new CounterDefinition("pointerColliderRefreshMs", "measured", "Stopwatch timing for RoSpritePointerBoundsCollider.RefreshActiveColliders."),
            new CounterDefinition("pointerColliderRefreshNowMs", "measured", "Stopwatch timing for summed RoSpritePointerBoundsCollider.RefreshNow calls."),
            new CounterDefinition("pointerRaycastMs", "measured", "Stopwatch timing for sprite pointer Physics.RaycastNonAlloc."),
            new CounterDefinition("pointerGroundPointMs", "measured", "Stopwatch timing for pointer ground point resolution."),
            new CounterDefinition("pointerResolverMs", "measured", "Stopwatch timing for RoPointerTargetResolver.Resolve."),
            new CounterDefinition("clickToMoveUpdateMs", "measured", "Stopwatch timing for RoClickToMoveController.Update."),
            new CounterDefinition("clickHandleMs", "measured", "Stopwatch timing for RoClickToMoveController.HandleClick."),
            new CounterDefinition("clickMoveToTargetMs", "measured", "Stopwatch timing for RoClickToMoveController.MoveToTarget."),
            new CounterDefinition("uiPointerCaptureMs", "measured", "Stopwatch timing for RoUiPointerCapture.IsPointerCaptured."),
            new CounterDefinition("cursorSetPointerTargetMs", "measured", "Stopwatch timing for RoCursorStateController.SetPointerTarget calls from click-to-move."),
            new CounterDefinition("cursorVisualLateUpdateMs", "measured", "Stopwatch timing for RoCursorSpriteVisual.LateUpdate."),
            new CounterDefinition("cursorVisualApplyPointerMs", "measured", "Stopwatch timing for RoCursorSpriteVisual.ApplyPointerPosition."),
            new CounterDefinition("pointerScreenReportGuiMs", "measured", "Stopwatch timing for RoPointerScreenPosition.ReportGuiMousePosition OnGUI bridge."),
            new CounterDefinition("clickIntentResolveMs", "measured", "Stopwatch timing for RoClickIntentResolver.Resolve."),
            new CounterDefinition("actorServiceUpdateMs", "measured", "Stopwatch timing for RoActorTickService.Update."),
            new CounterDefinition("actorServiceLateUpdateMs", "measured", "Stopwatch timing for RoActorTickService.LateUpdate."),
            new CounterDefinition("effectManagerUpdateMs", "measured", "Stopwatch timing for RoEffectManager.Update."),
            new CounterDefinition("minimalLoginShellUpdateMs", "measured", "Stopwatch timing for RoMinimalLoginShell.Update."),
            new CounterDefinition("minimalLoginShellStableLightPathMs", "measured", "Stopwatch timing for the stable gameplay light-path branch in RoMinimalLoginShell.Update."),
            new CounterDefinition("minimalLoginShellApplyLiveDrainMs", "measured", "Stopwatch timing for RoMinimalLoginShell.ApplyLiveDrain."),
            new CounterDefinition("minimalLoginShellApplyWorldDrainMs", "measured", "Stopwatch timing for RoOnlineWorldController.ApplyDrain through RoMinimalLoginShell."),
            new CounterDefinition("minimalLoginShellApplyLiveDrainPostSyncMs", "measured", "Stopwatch timing for local player/UI post-sync after live drain world changes."),
            new CounterDefinition("minimalLoginShellEnsurePhase12Ms", "measured", "Stopwatch timing for Phase 12 gameplay window ensure calls from RoMinimalLoginShell."),
            new CounterDefinition("minimalLoginShellEnsureGameplayContextMs", "measured", "Stopwatch timing for gameplay context window ensure calls from RoMinimalLoginShell."),
            new CounterDefinition("minimalLoginShellRefreshGameplayWindowsMs", "measured", "Stopwatch timing for RefreshGameplayWindowsFromSequences call sites."),
            new CounterDefinition("minimalLoginShellScheduleUiRefreshMs", "measured", "Stopwatch timing for ScheduleUiRefresh including immediate fallback refresh."),
            new CounterDefinition("onlineWorldUpdateMs", "measured", "Stopwatch timing for RoOnlineWorldController.Update."),
            new CounterDefinition("cameraLateUpdateMs", "measured", "Stopwatch timing for RoCameraController.LateUpdate."),
            new CounterDefinition("gridSelectorUpdateMs", "measured", "Stopwatch timing for RoGridSelector.Update."),
            new CounterDefinition("runtimeMapLateUpdateMs", "measured", "Stopwatch timing for RoRuntimeMapController.LateUpdate."),
            new CounterDefinition("onGuiCalls", "measured", "Known Ragnarok MonoBehaviour OnGUI callback count during capture."),
            new CounterDefinition("onGuiLayoutEvents", "measured", "Known Ragnarok OnGUI callbacks where Event.current.type is Layout."),
            new CounterDefinition("onGuiRepaintEvents", "measured", "Known Ragnarok OnGUI callbacks where Event.current.type is Repaint."),
            new CounterDefinition("onGuiMouseMoveEvents", "measured", "Known Ragnarok OnGUI callbacks where Event.current.type is MouseMove."),
            new CounterDefinition("onGuiMs", "measured", "Summed Stopwatch timing for known Ragnarok OnGUI callbacks."),
            new CounterDefinition("minimalLoginShellOnGuiMs", "measured", "Stopwatch timing for RoMinimalLoginShell.OnGUI."),
            new CounterDefinition("actorHoverLabelOnGuiMs", "measured", "Stopwatch timing for RoActorHoverLabelOverlay.OnGUI."),
            new CounterDefinition("actorShowScriptOnGuiMs", "measured", "Stopwatch timing for RoActorShowScriptOverlay.OnGUI."),
            new CounterDefinition("actorLightingDebugOnGuiMs", "measured", "Stopwatch timing for RoActorLightingDebugOverlay.OnGUI."),
            new CounterDefinition("shortcutSettingsOnGuiMs", "measured", "Stopwatch timing for RoShortcutSettingsWindow.OnGUI."),
            new CounterDefinition("clickToMoveOnGuiMs", "measured", "Stopwatch timing for RoClickToMoveController.OnGUI pointer bridge."),
            new CounterDefinition("cursorVisualOnGuiMs", "measured", "Stopwatch timing for RoCursorSpriteVisual.OnGUI pointer bridge."),
            new CounterDefinition("gridSelectorOnGuiMs", "measured", "Stopwatch timing for RoGridSelector.OnGUI pointer bridge."),
        };

        private static readonly string[] MissingInstrumentation = {
            "Unity/Assets/Ragnarok/Runtime/Maps/RoRuntimeMapAssetCache.cs - add RO.AssetCache.Miss and cache hit/miss counters around every map asset cache miss path.",
            "Unity/Assets/Ragnarok/Runtime/Effects/RoEffectManager.cs - expand spawned/despawned object counters across all spawn methods and pool return/destroy callbacks.",
            "Unity/Assets/Ragnarok/Runtime/Entities/RoClickToMoveController.cs - increment pathfinding requests at the path request/build boundary.",
            "Unity/Assets/Ragnarok/Runtime/World/RoOnlineActorPresenter.cs - add finer markers for actor visual build, deferred build drain, resource bars, and nameplate sync.",
            "Unity/Assets/Ragnarok/Runtime/Sprites/RoActSpriteAnimator.cs - add central sub-markers around ApplyFrame mesh, texture, and mutable mesh phases.",
            "Unity/Assets/Ragnarok/Runtime/UI/RoLoginShellSession.cs and RoShellSnapshotBuilder.cs - split RO.Session.BuildSnapshot into full, functional, map HUD, and event snapshot submarkers.",
            "Unity/Assets/Ragnarok/Runtime/Network/RoLoginSessionClient.cs - optionally bridge RO.Network.PacketBatch deeper than the Unity-facing live drain boundary.",
            "Unity/Assets/Ragnarok/Runtime/Assets/RoAssetDatabase.cs and UI/RoUiSpriteAssetCache.cs - split asset load counters into source reads, texture cache hits, disk cache hits, and sprite cache misses.",
        };

        private static RoPerfCapture activeCapture;

        private MetricRecorder[] recorders;
        private FrameSample[] samples;
        private int[] topWorstSampleIndices;
        private RoAssetLoadEvent[] assetLoadEvents = Array.Empty<RoAssetLoadEvent>();
        private int assetLoadEventOverflow;
        private int sampleCount;
        private int sampleCapacity;
        private float durationSeconds;
        private float elapsedSeconds;
        private string captureDirectory = string.Empty;
        private string sceneName = string.Empty;
        private string stopReason = string.Empty;
        private bool capturing;
        private bool finishing;
        private bool bufferOverflow;
        private DateTime captureStartedLocal;
        private DateTime captureEndedLocal;
        private DateTime captureStartedUtc;
        private DateTime captureEndedUtc;
        private RoPerformanceCounterFrameSnapshot previousCounterSnapshot;
        private RoOnlineWorldController worldController;
        private RoEffectManager effectManager;
        private float nextSourceResolveTime;

        public bool IsCapturing => capturing;
        public static bool IsRecording => activeCapture != null && activeCapture.capturing;

        public static RoPerfCapture BeginCapture(float seconds, string outputDirectory) {
            if (IsRecording) {
                return null;
            }

            var go = new GameObject("RoPerfCapture") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            RoPerfCapture capture = go.AddComponent<RoPerfCapture>();
            capture.Begin(seconds, outputDirectory);
            activeCapture = capture;
            return capture;
        }

        public static bool StopActiveCapture() {
            if (!IsRecording) {
                return false;
            }

            activeCapture.Finish("manual-stop");
            return true;
        }

        private void Begin(float seconds, string outputDirectory) {
            durationSeconds = Mathf.Max(1f, seconds);
            captureDirectory = outputDirectory ?? string.Empty;
            elapsedSeconds = 0f;
            gpuFrameMsTotal = cpuMainThreadMsTotal = cpuRenderThreadMsTotal = gpuFrameMsMax = 0d;
            frameTimingSamples = 0;
            sampleCapacity = Mathf.CeilToInt(durationSeconds * MaxBufferedFramesPerSecond) + TopSpikeFrameCount + 8;
            samples = new FrameSample[sampleCapacity];
            topWorstSampleIndices = new int[TopSpikeFrameCount];
            for (int i = 0; i < topWorstSampleIndices.Length; i++) {
                topWorstSampleIndices[i] = -1;
            }

            captureStartedLocal = DateTime.Now;
            captureStartedUtc = DateTime.UtcNow;
            sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(sceneName)) {
                sceneName = "PlayMode";
            }

            StartRecorders();
            RoRuntimePerfCounters.ClearCaptureAssetLoadEvents();
            assetLoadEvents = Array.Empty<RoAssetLoadEvent>();
            assetLoadEventOverflow = 0;
            previousCounterSnapshot = RoRuntimePerfCounters.FrameSnapshot();
            ResolveRuntimeSources(force: true);
            RoRuntimePerfCounters.PerformanceCaptureTimingEnabled = true;
            capturing = true;
            Debug.Log("[RoPerfCapture] Recording " + durationSeconds.ToString("0", CultureInfo.InvariantCulture) + "s -> " + captureDirectory);
        }

        private void Update() {
            if (!capturing || finishing) {
                return;
            }

            float deltaSeconds = Time.unscaledDeltaTime;
            elapsedSeconds += deltaSeconds;
            CaptureFrameTiming();
            if (!TryCaptureFrame(deltaSeconds)) {
                bufferOverflow = true;
                Finish("sample-buffer-full");
                return;
            }

            if (elapsedSeconds >= durationSeconds) {
                Finish("duration-complete");
            }
        }

        // FrameTimingManager (needs the player's frame timing stats; build-player turns them on): the GPU time is what
        // the CPU frame time cannot show
        private readonly FrameTiming[] frameTimingBuffer = new FrameTiming[1];
        private double gpuFrameMsTotal;
        private double gpuFrameMsMax;
        private double cpuMainThreadMsTotal;
        private double cpuRenderThreadMsTotal;
        private int frameTimingSamples;

        private void CaptureFrameTiming() {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, frameTimingBuffer) == 0) {
                return;
            }

            FrameTiming timing = frameTimingBuffer[0];
            if (timing.gpuFrameTime <= 0d) {
                return;
            }

            frameTimingSamples++;
            gpuFrameMsTotal += timing.gpuFrameTime;
            gpuFrameMsMax = Math.Max(gpuFrameMsMax, timing.gpuFrameTime);
            cpuMainThreadMsTotal += timing.cpuMainThreadFrameTime;
            cpuRenderThreadMsTotal += timing.cpuRenderThreadFrameTime;
        }

        private void OnDisable() {
            if (capturing && !finishing) {
                Finish("component-disabled");
                return;
            }

            DisposeRecorders();
            if (ReferenceEquals(activeCapture, this)) {
                activeCapture = null;
            }
        }

        private void OnApplicationQuit() {
            if (capturing && !finishing) {
                Finish("application-quit");
            }
        }

        private void StartRecorders() {
            DisposeRecorders();
            recorders = new MetricRecorder[MetricDefinitions.Length];
            for (int i = 0; i < MetricDefinitions.Length; i++) {
                var recorder = new MetricRecorder { Definition = MetricDefinitions[i] };
                TryStartRecorder(recorder);
                recorders[i] = recorder;
            }
        }

        private static void TryStartRecorder(MetricRecorder recorder) {
            MetricDefinition definition = recorder.Definition;
            for (int i = 0; i < definition.SpecCount; i++) {
                RecorderSpec spec = definition.GetSpec(i);
                ProfilerRecorder profilerRecorder;
                try {
                    profilerRecorder = ProfilerRecorder.StartNew(spec.Category, spec.StatName);
                } catch {
                    continue;
                }

                if (!profilerRecorder.Valid) {
                    profilerRecorder.Dispose();
                    continue;
                }

                recorder.Recorder = profilerRecorder;
                recorder.Available = true;
                recorder.ActiveStatName = spec.StatName;
                return;
            }

            recorder.Available = false;
        }

        private bool TryCaptureFrame(float deltaSeconds) {
            if (sampleCount >= sampleCapacity) {
                return false;
            }

            ResolveRuntimeSources(force: false);

            FrameSample sample = default;
            sample.FrameIndex = Time.frameCount;
            RoRuntimePerfCounters.SetCaptureFrameIndex(sample.FrameIndex);
            sample.TimestampSeconds = elapsedSeconds;
            sample.DeltaTimeMs = Mathf.Max(0f, deltaSeconds * 1000f);
            sample.Fps = sample.DeltaTimeMs > 0.0001f ? 1000f / sample.DeltaTimeMs : 0f;
            sample.FrameTimeNs = ReadMetric(MetricFrameTimeNs);
            sample.MainThreadNs = ReadMetric(MetricMainThreadNs);
            sample.RenderThreadNs = ReadMetric(MetricRenderThreadNs);
            sample.GcAllocBytes = ReadMetric(MetricGcAllocBytes);
            sample.TotalUsedMemory = ReadMetric(MetricTotalUsedMemory);
            sample.SystemUsedMemory = ReadMetric(MetricSystemUsedMemory);
            sample.GcReservedMemory = ReadMetric(MetricGcReservedMemory);
            sample.DrawCalls = ReadMetric(MetricDrawCalls);
            sample.Batches = ReadMetric(MetricBatches);
            sample.SetPassCalls = ReadMetric(MetricSetPassCalls);
            sample.Triangles = ReadMetric(MetricTriangles);
            sample.Vertices = ReadMetric(MetricVertices);

            CaptureRuntimeCounts(ref sample);
            RoPerformanceCounterFrameSnapshot counters = RoRuntimePerfCounters.FrameSnapshot();
            sample.PacketsProcessed = DeltaCounter(counters.PacketsProcessed, previousCounterSnapshot.PacketsProcessed);
            sample.SnapshotsBuilt = DeltaCounter(counters.SnapshotsBuilt, previousCounterSnapshot.SnapshotsBuilt);
            sample.AssetLoads = DeltaCounter(counters.AssetLoads, previousCounterSnapshot.AssetLoads);
            sample.AssetLoadBytes = DeltaCounter(counters.AssetLoadBytes, previousCounterSnapshot.AssetLoadBytes);
            sample.ActorOverlaySpriteCacheHits = DeltaCounter(counters.ActorOverlaySpriteCacheHits, previousCounterSnapshot.ActorOverlaySpriteCacheHits);
            sample.ActorOverlaySpriteCacheMisses = DeltaCounter(counters.ActorOverlaySpriteCacheMisses, previousCounterSnapshot.ActorOverlaySpriteCacheMisses);
            sample.ActorOverlaySpriteCacheBuildFailures = DeltaCounter(counters.ActorOverlaySpriteCacheBuildFailures, previousCounterSnapshot.ActorOverlaySpriteCacheBuildFailures);
            sample.SpriteAnimatorApplyFrameCalls = DeltaCounter(counters.SpriteAnimatorApplyFrameCalls, previousCounterSnapshot.SpriteAnimatorApplyFrameCalls);
            sample.SpriteAnimatorApplyFrameTicks = DeltaCounter(counters.SpriteAnimatorApplyFrameTicks, previousCounterSnapshot.SpriteAnimatorApplyFrameTicks);
            sample.SpriteAnimatorMeshAssigns = DeltaCounter(counters.SpriteAnimatorMeshAssigns, previousCounterSnapshot.SpriteAnimatorMeshAssigns);
            sample.SpriteAnimatorMeshAssignTicks = DeltaCounter(counters.SpriteAnimatorMeshAssignTicks, previousCounterSnapshot.SpriteAnimatorMeshAssignTicks);
            sample.SpriteAnimatorTextureAssigns = DeltaCounter(counters.SpriteAnimatorTextureAssigns, previousCounterSnapshot.SpriteAnimatorTextureAssigns);
            sample.SpriteAnimatorTextureAssignTicks = DeltaCounter(counters.SpriteAnimatorTextureAssignTicks, previousCounterSnapshot.SpriteAnimatorTextureAssignTicks);
            sample.SpriteAnimatorMutableMeshUpdates = DeltaCounter(counters.SpriteAnimatorMutableMeshUpdates, previousCounterSnapshot.SpriteAnimatorMutableMeshUpdates);
            sample.SpriteAnimatorMutableMeshUpdateTicks = DeltaCounter(counters.SpriteAnimatorMutableMeshUpdateTicks, previousCounterSnapshot.SpriteAnimatorMutableMeshUpdateTicks);
            sample.SpriteFrameMeshBuilds = DeltaCounter(counters.SpriteFrameMeshBuilds, previousCounterSnapshot.SpriteFrameMeshBuilds);
            sample.SpriteCompositedFrameBuilds = DeltaCounter(counters.SpriteCompositedFrameBuilds, previousCounterSnapshot.SpriteCompositedFrameBuilds);
            sample.SpriteGeometryCacheHits = DeltaCounter(counters.SpriteGeometryCacheHits, previousCounterSnapshot.SpriteGeometryCacheHits);
            sample.SpriteGeometryCacheMisses = DeltaCounter(counters.SpriteGeometryCacheMisses, previousCounterSnapshot.SpriteGeometryCacheMisses);
            sample.SpriteGeometryPrewarmQueued = DeltaCounter(counters.SpriteGeometryPrewarmQueued, previousCounterSnapshot.SpriteGeometryPrewarmQueued);
            sample.SpriteGeometryPrewarmBuilt = DeltaCounter(counters.SpriteGeometryPrewarmBuilt, previousCounterSnapshot.SpriteGeometryPrewarmBuilt);
            sample.SpriteMaterialCacheHits = DeltaCounter(counters.SpriteMaterialCacheHits, previousCounterSnapshot.SpriteMaterialCacheHits);
            sample.SpriteMaterialCacheMisses = DeltaCounter(counters.SpriteMaterialCacheMisses, previousCounterSnapshot.SpriteMaterialCacheMisses);
            sample.ActorOverlayUpdates = DeltaCounter(counters.ActorOverlayUpdates, previousCounterSnapshot.ActorOverlayUpdates);
            sample.ActorOverlayUpdateTicks = DeltaCounter(counters.ActorOverlayUpdateTicks, previousCounterSnapshot.ActorOverlayUpdateTicks);
            sample.ActorOverlayApplyFrameCalls = DeltaCounter(counters.ActorOverlayApplyFrameCalls, previousCounterSnapshot.ActorOverlayApplyFrameCalls);
            sample.ActorOverlayApplyFrameTicks = DeltaCounter(counters.ActorOverlayApplyFrameTicks, previousCounterSnapshot.ActorOverlayApplyFrameTicks);
            sample.SpriteEffectUpdates = DeltaCounter(counters.SpriteEffectUpdates, previousCounterSnapshot.SpriteEffectUpdates);
            sample.SpriteEffectUpdateTicks = DeltaCounter(counters.SpriteEffectUpdateTicks, previousCounterSnapshot.SpriteEffectUpdateTicks);
            sample.SpriteEffectLateUpdates = DeltaCounter(counters.SpriteEffectLateUpdates, previousCounterSnapshot.SpriteEffectLateUpdates);
            sample.SpriteEffectLateUpdateTicks = DeltaCounter(counters.SpriteEffectLateUpdateTicks, previousCounterSnapshot.SpriteEffectLateUpdateTicks);
            sample.PrimitiveEffectUpdates = DeltaCounter(counters.PrimitiveEffectUpdates, previousCounterSnapshot.PrimitiveEffectUpdates);
            sample.PrimitiveEffectUpdateTicks = DeltaCounter(counters.PrimitiveEffectUpdateTicks, previousCounterSnapshot.PrimitiveEffectUpdateTicks);
            sample.ActorSortingUpdates = DeltaCounter(counters.ActorSortingUpdates, previousCounterSnapshot.ActorSortingUpdates);
            sample.ActorSortingUpdateTicks = DeltaCounter(counters.ActorSortingUpdateTicks, previousCounterSnapshot.ActorSortingUpdateTicks);
            sample.SpawnedObjects = DeltaCounter(counters.SpawnedObjects, previousCounterSnapshot.SpawnedObjects);
            sample.DespawnedObjects = DeltaCounter(counters.DespawnedObjects, previousCounterSnapshot.DespawnedObjects);
            sample.PathfindingRequests = UnavailableLong;
            sample.PointerResolveTargets = DeltaCounter(counters.PointerResolveTargets, previousCounterSnapshot.PointerResolveTargets);
            sample.PointerSyncTransforms = DeltaCounter(counters.PointerSyncTransforms, previousCounterSnapshot.PointerSyncTransforms);
            sample.ActiveSpritePointerShapes = counters.ActiveSpritePointerShapeCount;
            sample.ActiveSpritePointerColliders = counters.ActiveSpritePointerColliderCount;
            sample.SpritePointerBroadphaseChecks = DeltaCounter(counters.SpritePointerBroadphaseChecks, previousCounterSnapshot.SpritePointerBroadphaseChecks);
            sample.SpritePointerExactTests = DeltaCounter(counters.SpritePointerExactTests, previousCounterSnapshot.SpritePointerExactTests);
            sample.SpritePointerAlphaTests = DeltaCounter(counters.SpritePointerAlphaTests, previousCounterSnapshot.SpritePointerAlphaTests);
            sample.SpritePointerCacheHits = DeltaCounter(counters.SpritePointerCacheHits, previousCounterSnapshot.SpritePointerCacheHits);
            sample.SpritePointerCacheMisses = DeltaCounter(counters.SpritePointerCacheMisses, previousCounterSnapshot.SpritePointerCacheMisses);
            sample.SpritePointerColliderRaycasts = DeltaCounter(counters.SpritePointerColliderRaycasts, previousCounterSnapshot.SpritePointerColliderRaycasts);
            sample.SpritePointerColliderHits = DeltaCounter(counters.SpritePointerColliderHits, previousCounterSnapshot.SpritePointerColliderHits);
            sample.SpritePointerColliderRefreshes = DeltaCounter(counters.SpritePointerColliderRefreshes, previousCounterSnapshot.SpritePointerColliderRefreshes);
            sample.PointerResolveTicks = DeltaCounter(counters.PointerResolveTicks, previousCounterSnapshot.PointerResolveTicks);
            sample.PointerWorldTargetsTicks = DeltaCounter(counters.PointerWorldTargetsTicks, previousCounterSnapshot.PointerWorldTargetsTicks);
            sample.PointerColliderRefreshTicks = DeltaCounter(counters.PointerColliderRefreshTicks, previousCounterSnapshot.PointerColliderRefreshTicks);
            sample.PointerColliderRefreshNowTicks = DeltaCounter(counters.PointerColliderRefreshNowTicks, previousCounterSnapshot.PointerColliderRefreshNowTicks);
            sample.PointerRaycastTicks = DeltaCounter(counters.PointerRaycastTicks, previousCounterSnapshot.PointerRaycastTicks);
            sample.PointerGroundPointTicks = DeltaCounter(counters.PointerGroundPointTicks, previousCounterSnapshot.PointerGroundPointTicks);
            sample.PointerResolverTicks = DeltaCounter(counters.PointerResolverTicks, previousCounterSnapshot.PointerResolverTicks);
            sample.ClickToMoveUpdateTicks = DeltaCounter(counters.ClickToMoveUpdateTicks, previousCounterSnapshot.ClickToMoveUpdateTicks);
            sample.ClickHandleTicks = DeltaCounter(counters.ClickHandleTicks, previousCounterSnapshot.ClickHandleTicks);
            sample.ClickMoveToTargetTicks = DeltaCounter(counters.ClickMoveToTargetTicks, previousCounterSnapshot.ClickMoveToTargetTicks);
            sample.UiPointerCaptureTicks = DeltaCounter(counters.UiPointerCaptureTicks, previousCounterSnapshot.UiPointerCaptureTicks);
            sample.CursorSetPointerTargetTicks = DeltaCounter(counters.CursorSetPointerTargetTicks, previousCounterSnapshot.CursorSetPointerTargetTicks);
            sample.CursorVisualLateUpdateTicks = DeltaCounter(counters.CursorVisualLateUpdateTicks, previousCounterSnapshot.CursorVisualLateUpdateTicks);
            sample.CursorVisualApplyPointerTicks = DeltaCounter(counters.CursorVisualApplyPointerTicks, previousCounterSnapshot.CursorVisualApplyPointerTicks);
            sample.PointerScreenReportGuiTicks = DeltaCounter(counters.PointerScreenReportGuiTicks, previousCounterSnapshot.PointerScreenReportGuiTicks);
            sample.ClickIntentResolveTicks = DeltaCounter(counters.ClickIntentResolveTicks, previousCounterSnapshot.ClickIntentResolveTicks);
            sample.ActorServiceUpdateTicks = DeltaCounter(counters.ActorServiceUpdateTicks, previousCounterSnapshot.ActorServiceUpdateTicks);
            sample.ActorServiceLateUpdateTicks = DeltaCounter(counters.ActorServiceLateUpdateTicks, previousCounterSnapshot.ActorServiceLateUpdateTicks);
            sample.EffectManagerUpdateTicks = DeltaCounter(counters.EffectManagerUpdateTicks, previousCounterSnapshot.EffectManagerUpdateTicks);
            sample.MinimalLoginShellUpdateTicks = DeltaCounter(counters.MinimalLoginShellUpdateTicks, previousCounterSnapshot.MinimalLoginShellUpdateTicks);
            sample.MinimalLoginShellStableLightPathTicks = DeltaCounter(counters.MinimalLoginShellStableLightPathTicks, previousCounterSnapshot.MinimalLoginShellStableLightPathTicks);
            sample.MinimalLoginShellApplyLiveDrainTicks = DeltaCounter(counters.MinimalLoginShellApplyLiveDrainTicks, previousCounterSnapshot.MinimalLoginShellApplyLiveDrainTicks);
            sample.MinimalLoginShellApplyWorldDrainTicks = DeltaCounter(counters.MinimalLoginShellApplyWorldDrainTicks, previousCounterSnapshot.MinimalLoginShellApplyWorldDrainTicks);
            sample.MinimalLoginShellApplyLiveDrainPostSyncTicks = DeltaCounter(counters.MinimalLoginShellApplyLiveDrainPostSyncTicks, previousCounterSnapshot.MinimalLoginShellApplyLiveDrainPostSyncTicks);
            sample.MinimalLoginShellEnsurePhase12Ticks = DeltaCounter(counters.MinimalLoginShellEnsurePhase12Ticks, previousCounterSnapshot.MinimalLoginShellEnsurePhase12Ticks);
            sample.MinimalLoginShellEnsureGameplayContextTicks = DeltaCounter(counters.MinimalLoginShellEnsureGameplayContextTicks, previousCounterSnapshot.MinimalLoginShellEnsureGameplayContextTicks);
            sample.MinimalLoginShellRefreshGameplayWindowsTicks = DeltaCounter(counters.MinimalLoginShellRefreshGameplayWindowsTicks, previousCounterSnapshot.MinimalLoginShellRefreshGameplayWindowsTicks);
            sample.MinimalLoginShellScheduleUiRefreshTicks = DeltaCounter(counters.MinimalLoginShellScheduleUiRefreshTicks, previousCounterSnapshot.MinimalLoginShellScheduleUiRefreshTicks);
            sample.OnlineWorldUpdateTicks = DeltaCounter(counters.OnlineWorldUpdateTicks, previousCounterSnapshot.OnlineWorldUpdateTicks);
            sample.CameraLateUpdateTicks = DeltaCounter(counters.CameraLateUpdateTicks, previousCounterSnapshot.CameraLateUpdateTicks);
            sample.GridSelectorUpdateTicks = DeltaCounter(counters.GridSelectorUpdateTicks, previousCounterSnapshot.GridSelectorUpdateTicks);
            sample.RuntimeMapLateUpdateTicks = DeltaCounter(counters.RuntimeMapLateUpdateTicks, previousCounterSnapshot.RuntimeMapLateUpdateTicks);
            sample.OnGuiCalls = DeltaCounter(counters.OnGuiCalls, previousCounterSnapshot.OnGuiCalls);
            sample.OnGuiLayoutEvents = DeltaCounter(counters.OnGuiLayoutEvents, previousCounterSnapshot.OnGuiLayoutEvents);
            sample.OnGuiRepaintEvents = DeltaCounter(counters.OnGuiRepaintEvents, previousCounterSnapshot.OnGuiRepaintEvents);
            sample.OnGuiMouseMoveEvents = DeltaCounter(counters.OnGuiMouseMoveEvents, previousCounterSnapshot.OnGuiMouseMoveEvents);
            sample.OnGuiTicks = DeltaCounter(counters.OnGuiTicks, previousCounterSnapshot.OnGuiTicks);
            sample.MinimalLoginShellOnGuiTicks = DeltaCounter(counters.MinimalLoginShellOnGuiTicks, previousCounterSnapshot.MinimalLoginShellOnGuiTicks);
            sample.ActorHoverLabelOnGuiTicks = DeltaCounter(counters.ActorHoverLabelOnGuiTicks, previousCounterSnapshot.ActorHoverLabelOnGuiTicks);
            sample.ActorShowScriptOnGuiTicks = DeltaCounter(counters.ActorShowScriptOnGuiTicks, previousCounterSnapshot.ActorShowScriptOnGuiTicks);
            sample.ActorLightingDebugOnGuiTicks = DeltaCounter(counters.ActorLightingDebugOnGuiTicks, previousCounterSnapshot.ActorLightingDebugOnGuiTicks);
            sample.ShortcutSettingsOnGuiTicks = DeltaCounter(counters.ShortcutSettingsOnGuiTicks, previousCounterSnapshot.ShortcutSettingsOnGuiTicks);
            sample.ClickToMoveOnGuiTicks = DeltaCounter(counters.ClickToMoveOnGuiTicks, previousCounterSnapshot.ClickToMoveOnGuiTicks);
            sample.CursorVisualOnGuiTicks = DeltaCounter(counters.CursorVisualOnGuiTicks, previousCounterSnapshot.CursorVisualOnGuiTicks);
            sample.GridSelectorOnGuiTicks = DeltaCounter(counters.GridSelectorOnGuiTicks, previousCounterSnapshot.GridSelectorOnGuiTicks);
            previousCounterSnapshot = counters;

            int sampleIndex = sampleCount;
            samples[sampleIndex] = sample;
            sampleCount++;
            TrackWorstFrame(sampleIndex);
            return true;
        }

        private static long DeltaCounter(long current, long previous) {
            long delta = current - previous;
            return delta >= 0 ? delta : current;
        }

        private void CaptureRuntimeCounts(ref FrameSample sample) {
            sample.ActiveActors = UnavailableInt;
            sample.ActiveMobs = UnavailableInt;
            sample.ActiveEffects = UnavailableInt;
            sample.ActiveDamageNumbers = UnavailableInt;
            sample.ActiveUiWindows = CountActiveUiWindows();

            if (worldController != null) {
                try {
                    RoOnlineWorldState state = worldController.State;
                    sample.ActiveActors = state.ActorCount;
                    sample.ActiveMobs = state.MonsterLikeActorCount;
                } catch (Exception exception) when (exception is InvalidOperationException || exception is NullReferenceException) {
                    worldController = null;
                }
            }

            if (effectManager != null) {
                try {
                    sample.ActiveEffects = effectManager.ActiveEffectCount;
                    sample.ActiveDamageNumbers = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance != null ? Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance.MsgEffectCount : 0;
                } catch (Exception exception) when (exception is InvalidOperationException || exception is NullReferenceException) {
                    effectManager = null;
                }
            }
        }

        private static int CountActiveUiWindows() {
            var system = Ragnarok.UI.Classic.RoClassicUiSystem.Instance;
            if (system == null) {
                return UnavailableInt;
            }

            int count = 0;
            var windows = system.Windows.Windows;
            for (int i = 0; i < windows.Count; i++) {
                if (windows[i] != null && windows[i].Visible) {
                    count++;
                }
            }

            return count;
        }

        private void ResolveRuntimeSources(bool force) {
            if (!force && Time.unscaledTime < nextSourceResolveTime) {
                return;
            }

            nextSourceResolveTime = Time.unscaledTime + 1f;
            if (worldController == null) {
                worldController = FindFirstObjectByType<RoOnlineWorldController>();
            }

            if (effectManager == null) {
                effectManager = FindFirstObjectByType<RoEffectManager>();
            }
        }

        private long ReadMetric(int index) {
            if (recorders == null || index < 0 || index >= recorders.Length) {
                return UnavailableLong;
            }

            MetricRecorder recorder = recorders[index];
            if (recorder == null || !recorder.Available || !recorder.Recorder.Valid) {
                return UnavailableLong;
            }

            try {
                return recorder.Recorder.LastValue;
            } catch {
                recorder.Available = false;
                return UnavailableLong;
            }
        }

        private void TrackWorstFrame(int sampleIndex) {
            float frameMs = samples[sampleIndex].DeltaTimeMs;
            for (int i = 0; i < topWorstSampleIndices.Length; i++) {
                int existingIndex = topWorstSampleIndices[i];
                if (existingIndex >= 0 && samples[existingIndex].DeltaTimeMs >= frameMs) {
                    continue;
                }

                for (int j = topWorstSampleIndices.Length - 1; j > i; j--) {
                    topWorstSampleIndices[j] = topWorstSampleIndices[j - 1];
                }

                topWorstSampleIndices[i] = sampleIndex;
                return;
            }
        }

        private void Finish(string reason) {
            if (finishing) {
                return;
            }

            finishing = true;
            capturing = false;
            RoRuntimePerfCounters.PerformanceCaptureTimingEnabled = false;
            assetLoadEvents = RoRuntimePerfCounters.AssetLoadEventsSnapshot(out assetLoadEventOverflow);
            stopReason = reason ?? "unknown";
            captureEndedLocal = DateTime.Now;
            captureEndedUtc = DateTime.UtcNow;

            try {
                Directory.CreateDirectory(captureDirectory);
                CaptureAggregates aggregates = ComputeAggregates();
                File.WriteAllText(Path.Combine(captureDirectory, "samples.csv"), BuildSamplesCsv(), Encoding.UTF8);
                File.WriteAllText(Path.Combine(captureDirectory, "summary.json"), BuildSummaryJson(aggregates), Encoding.UTF8);
                File.WriteAllText(Path.Combine(captureDirectory, "report.md"), BuildReportMarkdown(aggregates), Encoding.UTF8);
                Debug.Log("[RoPerfCapture] Capture written: " + captureDirectory);
            } catch (Exception ex) {
                Debug.LogError("[RoPerfCapture] Failed to write capture '" + captureDirectory + "': " + ex.Message);
            } finally {
                DisposeRecorders();
                if (ReferenceEquals(activeCapture, this)) {
                    activeCapture = null;
                }

                Destroy(gameObject);
                finishing = false;
            }
        }

        private CaptureAggregates ComputeAggregates() {
            CaptureAggregates aggregates = default;
            aggregates.FrameCount = sampleCount;
            aggregates.ActualDurationSeconds = elapsedSeconds;
            aggregates.WorstFrameIndex = sampleCount > 0 ? samples[topWorstSampleIndices[0]].FrameIndex : 0;
            aggregates.MaxGcAllocBytes = UnavailableLong;
            aggregates.MaxGcAllocFrameIndex = 0;
            aggregates.MemoryStartBytes = UnavailableLong;
            aggregates.MemoryPeakBytes = UnavailableLong;
            aggregates.SystemMemoryPeakBytes = UnavailableLong;
            aggregates.GcReservedMemoryPeakBytes = UnavailableLong;
            aggregates.MaxMainThreadMs = -1d;
            aggregates.MaxRenderThreadMs = -1d;
            aggregates.AverageProfilerFrameTimeMs = -1d;
            aggregates.MaxProfilerFrameTimeMs = -1d;

            if (sampleCount == 0) {
                aggregates.Classification = "unknown";
                return aggregates;
            }

            var sortedFrameMs = new float[sampleCount];
            double totalMs = 0d;
            double totalProfilerFrameTimeMs = 0d;
            int profilerFrameTimeSampleCount = 0;
            for (int i = 0; i < sampleCount; i++) {
                FrameSample sample = samples[i];
                sortedFrameMs[i] = sample.DeltaTimeMs;
                totalMs += sample.DeltaTimeMs;
                if (sample.FrameTimeNs >= 0) {
                    double profilerFrameTimeMs = sample.FrameTimeNs / 1e6d;
                    totalProfilerFrameTimeMs += profilerFrameTimeMs;
                    profilerFrameTimeSampleCount++;
                    if (profilerFrameTimeMs > aggregates.MaxProfilerFrameTimeMs) {
                        aggregates.MaxProfilerFrameTimeMs = profilerFrameTimeMs;
                        aggregates.MaxProfilerFrameTimeFrameIndex = sample.FrameIndex;
                    }
                }

                if (sample.GcAllocBytes >= 0 && sample.GcAllocBytes > aggregates.MaxGcAllocBytes) {
                    aggregates.MaxGcAllocBytes = sample.GcAllocBytes;
                    aggregates.MaxGcAllocFrameIndex = sample.FrameIndex;
                }

                if (sample.TotalUsedMemory > 0) {
                    if (aggregates.MemoryStartBytes < 0) {
                        aggregates.MemoryStartBytes = sample.TotalUsedMemory;
                    }

                    if (sample.TotalUsedMemory > aggregates.MemoryPeakBytes) {
                        aggregates.MemoryPeakBytes = sample.TotalUsedMemory;
                    }
                }

                if (sample.SystemUsedMemory > 0 && sample.SystemUsedMemory > aggregates.SystemMemoryPeakBytes) {
                    aggregates.SystemMemoryPeakBytes = sample.SystemUsedMemory;
                }

                if (sample.GcReservedMemory > 0 && sample.GcReservedMemory > aggregates.GcReservedMemoryPeakBytes) {
                    aggregates.GcReservedMemoryPeakBytes = sample.GcReservedMemory;
                }

                if (sample.MainThreadNs >= 0) {
                    aggregates.MaxMainThreadMs = Math.Max(aggregates.MaxMainThreadMs, sample.MainThreadNs / 1e6d);
                }

                if (sample.RenderThreadNs >= 0) {
                    aggregates.MaxRenderThreadMs = Math.Max(aggregates.MaxRenderThreadMs, sample.RenderThreadNs / 1e6d);
                }

                aggregates.PointerResolveTargetsTotal += sample.PointerResolveTargets;
                aggregates.PointerSyncTransformsTotal += sample.PointerSyncTransforms;
                aggregates.SpritePointerBroadphaseChecksTotal += sample.SpritePointerBroadphaseChecks;
                aggregates.SpritePointerExactTestsTotal += sample.SpritePointerExactTests;
                aggregates.SpritePointerAlphaTestsTotal += sample.SpritePointerAlphaTests;
                aggregates.SpritePointerCacheHitsTotal += sample.SpritePointerCacheHits;
                aggregates.SpritePointerCacheMissesTotal += sample.SpritePointerCacheMisses;
                aggregates.SpritePointerColliderRaycastsTotal += sample.SpritePointerColliderRaycasts;
                aggregates.SpritePointerColliderHitsTotal += sample.SpritePointerColliderHits;
                aggregates.SpritePointerColliderRefreshesTotal += sample.SpritePointerColliderRefreshes;
                aggregates.PointerResolveTicksTotal += sample.PointerResolveTicks;
                aggregates.PointerWorldTargetsTicksTotal += sample.PointerWorldTargetsTicks;
                aggregates.PointerColliderRefreshTicksTotal += sample.PointerColliderRefreshTicks;
                aggregates.PointerColliderRefreshNowTicksTotal += sample.PointerColliderRefreshNowTicks;
                aggregates.PointerRaycastTicksTotal += sample.PointerRaycastTicks;
                aggregates.PointerGroundPointTicksTotal += sample.PointerGroundPointTicks;
                aggregates.PointerResolverTicksTotal += sample.PointerResolverTicks;
                aggregates.ClickToMoveUpdateTicksTotal += sample.ClickToMoveUpdateTicks;
                aggregates.ClickHandleTicksTotal += sample.ClickHandleTicks;
                aggregates.ClickMoveToTargetTicksTotal += sample.ClickMoveToTargetTicks;
                aggregates.UiPointerCaptureTicksTotal += sample.UiPointerCaptureTicks;
                aggregates.CursorSetPointerTargetTicksTotal += sample.CursorSetPointerTargetTicks;
                aggregates.CursorVisualLateUpdateTicksTotal += sample.CursorVisualLateUpdateTicks;
                aggregates.CursorVisualApplyPointerTicksTotal += sample.CursorVisualApplyPointerTicks;
                aggregates.PointerScreenReportGuiTicksTotal += sample.PointerScreenReportGuiTicks;
                aggregates.ClickIntentResolveTicksTotal += sample.ClickIntentResolveTicks;
                aggregates.ActorOverlaySpriteCacheHitsTotal += sample.ActorOverlaySpriteCacheHits;
                aggregates.ActorOverlaySpriteCacheMissesTotal += sample.ActorOverlaySpriteCacheMisses;
                aggregates.ActorOverlaySpriteCacheBuildFailuresTotal += sample.ActorOverlaySpriteCacheBuildFailures;
                aggregates.SpriteAnimatorApplyFrameCallsTotal += sample.SpriteAnimatorApplyFrameCalls;
                aggregates.SpriteAnimatorApplyFrameTicksTotal += sample.SpriteAnimatorApplyFrameTicks;
                aggregates.SpriteAnimatorMeshAssignsTotal += sample.SpriteAnimatorMeshAssigns;
                aggregates.SpriteAnimatorMeshAssignTicksTotal += sample.SpriteAnimatorMeshAssignTicks;
                aggregates.SpriteAnimatorTextureAssignsTotal += sample.SpriteAnimatorTextureAssigns;
                aggregates.SpriteAnimatorTextureAssignTicksTotal += sample.SpriteAnimatorTextureAssignTicks;
                aggregates.SpriteAnimatorMutableMeshUpdatesTotal += sample.SpriteAnimatorMutableMeshUpdates;
                aggregates.SpriteAnimatorMutableMeshUpdateTicksTotal += sample.SpriteAnimatorMutableMeshUpdateTicks;
                aggregates.SpriteFrameMeshBuildsTotal += sample.SpriteFrameMeshBuilds;
                aggregates.SpriteCompositedFrameBuildsTotal += sample.SpriteCompositedFrameBuilds;
                aggregates.SpriteGeometryCacheHitsTotal += sample.SpriteGeometryCacheHits;
                aggregates.SpriteGeometryCacheMissesTotal += sample.SpriteGeometryCacheMisses;
                aggregates.SpriteGeometryPrewarmQueuedTotal += sample.SpriteGeometryPrewarmQueued;
                aggregates.SpriteGeometryPrewarmBuiltTotal += sample.SpriteGeometryPrewarmBuilt;
                aggregates.SpriteMaterialCacheHitsTotal += sample.SpriteMaterialCacheHits;
                aggregates.SpriteMaterialCacheMissesTotal += sample.SpriteMaterialCacheMisses;
                aggregates.ActorOverlayUpdatesTotal += sample.ActorOverlayUpdates;
                aggregates.ActorOverlayUpdateTicksTotal += sample.ActorOverlayUpdateTicks;
                aggregates.ActorOverlayApplyFrameCallsTotal += sample.ActorOverlayApplyFrameCalls;
                aggregates.ActorOverlayApplyFrameTicksTotal += sample.ActorOverlayApplyFrameTicks;
                aggregates.SpriteEffectUpdatesTotal += sample.SpriteEffectUpdates;
                aggregates.SpriteEffectUpdateTicksTotal += sample.SpriteEffectUpdateTicks;
                aggregates.SpriteEffectLateUpdatesTotal += sample.SpriteEffectLateUpdates;
                aggregates.SpriteEffectLateUpdateTicksTotal += sample.SpriteEffectLateUpdateTicks;
                aggregates.PrimitiveEffectUpdatesTotal += sample.PrimitiveEffectUpdates;
                aggregates.PrimitiveEffectUpdateTicksTotal += sample.PrimitiveEffectUpdateTicks;
                aggregates.ActorSortingUpdatesTotal += sample.ActorSortingUpdates;
                aggregates.ActorSortingUpdateTicksTotal += sample.ActorSortingUpdateTicks;
                aggregates.ActorServiceUpdateTicksTotal += sample.ActorServiceUpdateTicks;
                aggregates.ActorServiceLateUpdateTicksTotal += sample.ActorServiceLateUpdateTicks;
                aggregates.EffectManagerUpdateTicksTotal += sample.EffectManagerUpdateTicks;
                aggregates.MinimalLoginShellUpdateTicksTotal += sample.MinimalLoginShellUpdateTicks;
                aggregates.MinimalLoginShellStableLightPathTicksTotal += sample.MinimalLoginShellStableLightPathTicks;
                aggregates.MinimalLoginShellApplyLiveDrainTicksTotal += sample.MinimalLoginShellApplyLiveDrainTicks;
                aggregates.MinimalLoginShellApplyWorldDrainTicksTotal += sample.MinimalLoginShellApplyWorldDrainTicks;
                aggregates.MinimalLoginShellApplyLiveDrainPostSyncTicksTotal += sample.MinimalLoginShellApplyLiveDrainPostSyncTicks;
                aggregates.MinimalLoginShellEnsurePhase12TicksTotal += sample.MinimalLoginShellEnsurePhase12Ticks;
                aggregates.MinimalLoginShellEnsureGameplayContextTicksTotal += sample.MinimalLoginShellEnsureGameplayContextTicks;
                aggregates.MinimalLoginShellRefreshGameplayWindowsTicksTotal += sample.MinimalLoginShellRefreshGameplayWindowsTicks;
                aggregates.MinimalLoginShellScheduleUiRefreshTicksTotal += sample.MinimalLoginShellScheduleUiRefreshTicks;
                aggregates.OnlineWorldUpdateTicksTotal += sample.OnlineWorldUpdateTicks;
                aggregates.CameraLateUpdateTicksTotal += sample.CameraLateUpdateTicks;
                aggregates.GridSelectorUpdateTicksTotal += sample.GridSelectorUpdateTicks;
                aggregates.RuntimeMapLateUpdateTicksTotal += sample.RuntimeMapLateUpdateTicks;
                aggregates.OnGuiCallsTotal += sample.OnGuiCalls;
                aggregates.OnGuiLayoutEventsTotal += sample.OnGuiLayoutEvents;
                aggregates.OnGuiRepaintEventsTotal += sample.OnGuiRepaintEvents;
                aggregates.OnGuiMouseMoveEventsTotal += sample.OnGuiMouseMoveEvents;
                aggregates.OnGuiTicksTotal += sample.OnGuiTicks;
                aggregates.MinimalLoginShellOnGuiTicksTotal += sample.MinimalLoginShellOnGuiTicks;
                aggregates.ActorHoverLabelOnGuiTicksTotal += sample.ActorHoverLabelOnGuiTicks;
                aggregates.ActorShowScriptOnGuiTicksTotal += sample.ActorShowScriptOnGuiTicks;
                aggregates.ActorLightingDebugOnGuiTicksTotal += sample.ActorLightingDebugOnGuiTicks;
                aggregates.ShortcutSettingsOnGuiTicksTotal += sample.ShortcutSettingsOnGuiTicks;
                aggregates.ClickToMoveOnGuiTicksTotal += sample.ClickToMoveOnGuiTicks;
                aggregates.CursorVisualOnGuiTicksTotal += sample.CursorVisualOnGuiTicks;
                aggregates.GridSelectorOnGuiTicksTotal += sample.GridSelectorOnGuiTicks;
                aggregates.MaxPointerResolveTargetsFrame = Math.Max(aggregates.MaxPointerResolveTargetsFrame, sample.PointerResolveTargets);
                aggregates.MaxPointerSyncTransformsFrame = Math.Max(aggregates.MaxPointerSyncTransformsFrame, sample.PointerSyncTransforms);
                aggregates.MaxSpritePointerBroadphaseChecksFrame = Math.Max(aggregates.MaxSpritePointerBroadphaseChecksFrame, sample.SpritePointerBroadphaseChecks);
                aggregates.MaxSpritePointerExactTestsFrame = Math.Max(aggregates.MaxSpritePointerExactTestsFrame, sample.SpritePointerExactTests);
                aggregates.MaxSpritePointerAlphaTestsFrame = Math.Max(aggregates.MaxSpritePointerAlphaTestsFrame, sample.SpritePointerAlphaTests);
                aggregates.MaxSpritePointerCacheHitsFrame = Math.Max(aggregates.MaxSpritePointerCacheHitsFrame, sample.SpritePointerCacheHits);
                aggregates.MaxSpritePointerCacheMissesFrame = Math.Max(aggregates.MaxSpritePointerCacheMissesFrame, sample.SpritePointerCacheMisses);
                aggregates.MaxSpritePointerColliderRaycastsFrame = Math.Max(aggregates.MaxSpritePointerColliderRaycastsFrame, sample.SpritePointerColliderRaycasts);
                aggregates.MaxSpritePointerColliderHitsFrame = Math.Max(aggregates.MaxSpritePointerColliderHitsFrame, sample.SpritePointerColliderHits);
                aggregates.MaxSpritePointerColliderRefreshesFrame = Math.Max(aggregates.MaxSpritePointerColliderRefreshesFrame, sample.SpritePointerColliderRefreshes);
                aggregates.MaxPointerResolveTicksFrame = Math.Max(aggregates.MaxPointerResolveTicksFrame, sample.PointerResolveTicks);
                aggregates.MaxPointerWorldTargetsTicksFrame = Math.Max(aggregates.MaxPointerWorldTargetsTicksFrame, sample.PointerWorldTargetsTicks);
                aggregates.MaxPointerColliderRefreshTicksFrame = Math.Max(aggregates.MaxPointerColliderRefreshTicksFrame, sample.PointerColliderRefreshTicks);
                aggregates.MaxPointerColliderRefreshNowTicksFrame = Math.Max(aggregates.MaxPointerColliderRefreshNowTicksFrame, sample.PointerColliderRefreshNowTicks);
                aggregates.MaxPointerRaycastTicksFrame = Math.Max(aggregates.MaxPointerRaycastTicksFrame, sample.PointerRaycastTicks);
                aggregates.MaxPointerGroundPointTicksFrame = Math.Max(aggregates.MaxPointerGroundPointTicksFrame, sample.PointerGroundPointTicks);
                aggregates.MaxPointerResolverTicksFrame = Math.Max(aggregates.MaxPointerResolverTicksFrame, sample.PointerResolverTicks);
                aggregates.MaxClickToMoveUpdateTicksFrame = Math.Max(aggregates.MaxClickToMoveUpdateTicksFrame, sample.ClickToMoveUpdateTicks);
                aggregates.MaxClickHandleTicksFrame = Math.Max(aggregates.MaxClickHandleTicksFrame, sample.ClickHandleTicks);
                aggregates.MaxClickMoveToTargetTicksFrame = Math.Max(aggregates.MaxClickMoveToTargetTicksFrame, sample.ClickMoveToTargetTicks);
                aggregates.MaxUiPointerCaptureTicksFrame = Math.Max(aggregates.MaxUiPointerCaptureTicksFrame, sample.UiPointerCaptureTicks);
                aggregates.MaxCursorSetPointerTargetTicksFrame = Math.Max(aggregates.MaxCursorSetPointerTargetTicksFrame, sample.CursorSetPointerTargetTicks);
                aggregates.MaxCursorVisualLateUpdateTicksFrame = Math.Max(aggregates.MaxCursorVisualLateUpdateTicksFrame, sample.CursorVisualLateUpdateTicks);
                aggregates.MaxCursorVisualApplyPointerTicksFrame = Math.Max(aggregates.MaxCursorVisualApplyPointerTicksFrame, sample.CursorVisualApplyPointerTicks);
                aggregates.MaxPointerScreenReportGuiTicksFrame = Math.Max(aggregates.MaxPointerScreenReportGuiTicksFrame, sample.PointerScreenReportGuiTicks);
                aggregates.MaxClickIntentResolveTicksFrame = Math.Max(aggregates.MaxClickIntentResolveTicksFrame, sample.ClickIntentResolveTicks);
                aggregates.MaxActorOverlaySpriteCacheHitsFrame = Math.Max(aggregates.MaxActorOverlaySpriteCacheHitsFrame, sample.ActorOverlaySpriteCacheHits);
                aggregates.MaxActorOverlaySpriteCacheMissesFrame = Math.Max(aggregates.MaxActorOverlaySpriteCacheMissesFrame, sample.ActorOverlaySpriteCacheMisses);
                aggregates.MaxActorOverlaySpriteCacheBuildFailuresFrame = Math.Max(aggregates.MaxActorOverlaySpriteCacheBuildFailuresFrame, sample.ActorOverlaySpriteCacheBuildFailures);
                aggregates.MaxSpriteAnimatorApplyFrameCallsFrame = Math.Max(aggregates.MaxSpriteAnimatorApplyFrameCallsFrame, sample.SpriteAnimatorApplyFrameCalls);
                aggregates.MaxSpriteAnimatorApplyFrameTicksFrame = Math.Max(aggregates.MaxSpriteAnimatorApplyFrameTicksFrame, sample.SpriteAnimatorApplyFrameTicks);
                aggregates.MaxSpriteAnimatorMeshAssignsFrame = Math.Max(aggregates.MaxSpriteAnimatorMeshAssignsFrame, sample.SpriteAnimatorMeshAssigns);
                aggregates.MaxSpriteAnimatorMeshAssignTicksFrame = Math.Max(aggregates.MaxSpriteAnimatorMeshAssignTicksFrame, sample.SpriteAnimatorMeshAssignTicks);
                aggregates.MaxSpriteAnimatorTextureAssignsFrame = Math.Max(aggregates.MaxSpriteAnimatorTextureAssignsFrame, sample.SpriteAnimatorTextureAssigns);
                aggregates.MaxSpriteAnimatorTextureAssignTicksFrame = Math.Max(aggregates.MaxSpriteAnimatorTextureAssignTicksFrame, sample.SpriteAnimatorTextureAssignTicks);
                aggregates.MaxSpriteAnimatorMutableMeshUpdatesFrame = Math.Max(aggregates.MaxSpriteAnimatorMutableMeshUpdatesFrame, sample.SpriteAnimatorMutableMeshUpdates);
                aggregates.MaxSpriteAnimatorMutableMeshUpdateTicksFrame = Math.Max(aggregates.MaxSpriteAnimatorMutableMeshUpdateTicksFrame, sample.SpriteAnimatorMutableMeshUpdateTicks);
                aggregates.MaxSpriteFrameMeshBuildsFrame = Math.Max(aggregates.MaxSpriteFrameMeshBuildsFrame, sample.SpriteFrameMeshBuilds);
                aggregates.MaxSpriteCompositedFrameBuildsFrame = Math.Max(aggregates.MaxSpriteCompositedFrameBuildsFrame, sample.SpriteCompositedFrameBuilds);
                aggregates.MaxSpriteGeometryCacheHitsFrame = Math.Max(aggregates.MaxSpriteGeometryCacheHitsFrame, sample.SpriteGeometryCacheHits);
                aggregates.MaxSpriteGeometryCacheMissesFrame = Math.Max(aggregates.MaxSpriteGeometryCacheMissesFrame, sample.SpriteGeometryCacheMisses);
                aggregates.MaxSpriteGeometryPrewarmQueuedFrame = Math.Max(aggregates.MaxSpriteGeometryPrewarmQueuedFrame, sample.SpriteGeometryPrewarmQueued);
                aggregates.MaxSpriteGeometryPrewarmBuiltFrame = Math.Max(aggregates.MaxSpriteGeometryPrewarmBuiltFrame, sample.SpriteGeometryPrewarmBuilt);
                aggregates.MaxSpriteMaterialCacheHitsFrame = Math.Max(aggregates.MaxSpriteMaterialCacheHitsFrame, sample.SpriteMaterialCacheHits);
                aggregates.MaxSpriteMaterialCacheMissesFrame = Math.Max(aggregates.MaxSpriteMaterialCacheMissesFrame, sample.SpriteMaterialCacheMisses);
                aggregates.MaxActorOverlayUpdatesFrame = Math.Max(aggregates.MaxActorOverlayUpdatesFrame, sample.ActorOverlayUpdates);
                aggregates.MaxActorOverlayUpdateTicksFrame = Math.Max(aggregates.MaxActorOverlayUpdateTicksFrame, sample.ActorOverlayUpdateTicks);
                aggregates.MaxActorOverlayApplyFrameCallsFrame = Math.Max(aggregates.MaxActorOverlayApplyFrameCallsFrame, sample.ActorOverlayApplyFrameCalls);
                aggregates.MaxActorOverlayApplyFrameTicksFrame = Math.Max(aggregates.MaxActorOverlayApplyFrameTicksFrame, sample.ActorOverlayApplyFrameTicks);
                aggregates.MaxSpriteEffectUpdatesFrame = Math.Max(aggregates.MaxSpriteEffectUpdatesFrame, sample.SpriteEffectUpdates);
                aggregates.MaxSpriteEffectUpdateTicksFrame = Math.Max(aggregates.MaxSpriteEffectUpdateTicksFrame, sample.SpriteEffectUpdateTicks);
                aggregates.MaxSpriteEffectLateUpdatesFrame = Math.Max(aggregates.MaxSpriteEffectLateUpdatesFrame, sample.SpriteEffectLateUpdates);
                aggregates.MaxSpriteEffectLateUpdateTicksFrame = Math.Max(aggregates.MaxSpriteEffectLateUpdateTicksFrame, sample.SpriteEffectLateUpdateTicks);
                aggregates.MaxPrimitiveEffectUpdatesFrame = Math.Max(aggregates.MaxPrimitiveEffectUpdatesFrame, sample.PrimitiveEffectUpdates);
                aggregates.MaxPrimitiveEffectUpdateTicksFrame = Math.Max(aggregates.MaxPrimitiveEffectUpdateTicksFrame, sample.PrimitiveEffectUpdateTicks);
                aggregates.MaxActorSortingUpdatesFrame = Math.Max(aggregates.MaxActorSortingUpdatesFrame, sample.ActorSortingUpdates);
                aggregates.MaxActorSortingUpdateTicksFrame = Math.Max(aggregates.MaxActorSortingUpdateTicksFrame, sample.ActorSortingUpdateTicks);
                aggregates.MaxActorServiceUpdateTicksFrame = Math.Max(aggregates.MaxActorServiceUpdateTicksFrame, sample.ActorServiceUpdateTicks);
                aggregates.MaxActorServiceLateUpdateTicksFrame = Math.Max(aggregates.MaxActorServiceLateUpdateTicksFrame, sample.ActorServiceLateUpdateTicks);
                aggregates.MaxEffectManagerUpdateTicksFrame = Math.Max(aggregates.MaxEffectManagerUpdateTicksFrame, sample.EffectManagerUpdateTicks);
                aggregates.MaxMinimalLoginShellUpdateTicksFrame = Math.Max(aggregates.MaxMinimalLoginShellUpdateTicksFrame, sample.MinimalLoginShellUpdateTicks);
                aggregates.MaxMinimalLoginShellStableLightPathTicksFrame = Math.Max(aggregates.MaxMinimalLoginShellStableLightPathTicksFrame, sample.MinimalLoginShellStableLightPathTicks);
                aggregates.MaxMinimalLoginShellApplyLiveDrainTicksFrame = Math.Max(aggregates.MaxMinimalLoginShellApplyLiveDrainTicksFrame, sample.MinimalLoginShellApplyLiveDrainTicks);
                aggregates.MaxMinimalLoginShellApplyWorldDrainTicksFrame = Math.Max(aggregates.MaxMinimalLoginShellApplyWorldDrainTicksFrame, sample.MinimalLoginShellApplyWorldDrainTicks);
                aggregates.MaxMinimalLoginShellApplyLiveDrainPostSyncTicksFrame = Math.Max(aggregates.MaxMinimalLoginShellApplyLiveDrainPostSyncTicksFrame, sample.MinimalLoginShellApplyLiveDrainPostSyncTicks);
                aggregates.MaxMinimalLoginShellEnsurePhase12TicksFrame = Math.Max(aggregates.MaxMinimalLoginShellEnsurePhase12TicksFrame, sample.MinimalLoginShellEnsurePhase12Ticks);
                aggregates.MaxMinimalLoginShellEnsureGameplayContextTicksFrame = Math.Max(aggregates.MaxMinimalLoginShellEnsureGameplayContextTicksFrame, sample.MinimalLoginShellEnsureGameplayContextTicks);
                aggregates.MaxMinimalLoginShellRefreshGameplayWindowsTicksFrame = Math.Max(aggregates.MaxMinimalLoginShellRefreshGameplayWindowsTicksFrame, sample.MinimalLoginShellRefreshGameplayWindowsTicks);
                aggregates.MaxMinimalLoginShellScheduleUiRefreshTicksFrame = Math.Max(aggregates.MaxMinimalLoginShellScheduleUiRefreshTicksFrame, sample.MinimalLoginShellScheduleUiRefreshTicks);
                aggregates.MaxOnlineWorldUpdateTicksFrame = Math.Max(aggregates.MaxOnlineWorldUpdateTicksFrame, sample.OnlineWorldUpdateTicks);
                aggregates.MaxCameraLateUpdateTicksFrame = Math.Max(aggregates.MaxCameraLateUpdateTicksFrame, sample.CameraLateUpdateTicks);
                aggregates.MaxGridSelectorUpdateTicksFrame = Math.Max(aggregates.MaxGridSelectorUpdateTicksFrame, sample.GridSelectorUpdateTicks);
                aggregates.MaxRuntimeMapLateUpdateTicksFrame = Math.Max(aggregates.MaxRuntimeMapLateUpdateTicksFrame, sample.RuntimeMapLateUpdateTicks);
                aggregates.MaxOnGuiCallsFrame = Math.Max(aggregates.MaxOnGuiCallsFrame, sample.OnGuiCalls);
                aggregates.MaxOnGuiLayoutEventsFrame = Math.Max(aggregates.MaxOnGuiLayoutEventsFrame, sample.OnGuiLayoutEvents);
                aggregates.MaxOnGuiRepaintEventsFrame = Math.Max(aggregates.MaxOnGuiRepaintEventsFrame, sample.OnGuiRepaintEvents);
                aggregates.MaxOnGuiMouseMoveEventsFrame = Math.Max(aggregates.MaxOnGuiMouseMoveEventsFrame, sample.OnGuiMouseMoveEvents);
                aggregates.MaxOnGuiTicksFrame = Math.Max(aggregates.MaxOnGuiTicksFrame, sample.OnGuiTicks);
                aggregates.MaxMinimalLoginShellOnGuiTicksFrame = Math.Max(aggregates.MaxMinimalLoginShellOnGuiTicksFrame, sample.MinimalLoginShellOnGuiTicks);
                aggregates.MaxActorHoverLabelOnGuiTicksFrame = Math.Max(aggregates.MaxActorHoverLabelOnGuiTicksFrame, sample.ActorHoverLabelOnGuiTicks);
                aggregates.MaxActorShowScriptOnGuiTicksFrame = Math.Max(aggregates.MaxActorShowScriptOnGuiTicksFrame, sample.ActorShowScriptOnGuiTicks);
                aggregates.MaxActorLightingDebugOnGuiTicksFrame = Math.Max(aggregates.MaxActorLightingDebugOnGuiTicksFrame, sample.ActorLightingDebugOnGuiTicks);
                aggregates.MaxShortcutSettingsOnGuiTicksFrame = Math.Max(aggregates.MaxShortcutSettingsOnGuiTicksFrame, sample.ShortcutSettingsOnGuiTicks);
                aggregates.MaxClickToMoveOnGuiTicksFrame = Math.Max(aggregates.MaxClickToMoveOnGuiTicksFrame, sample.ClickToMoveOnGuiTicks);
                aggregates.MaxCursorVisualOnGuiTicksFrame = Math.Max(aggregates.MaxCursorVisualOnGuiTicksFrame, sample.CursorVisualOnGuiTicks);
                aggregates.MaxGridSelectorOnGuiTicksFrame = Math.Max(aggregates.MaxGridSelectorOnGuiTicksFrame, sample.GridSelectorOnGuiTicks);
                aggregates.MaxActiveSpritePointerShapes = Math.Max(aggregates.MaxActiveSpritePointerShapes, sample.ActiveSpritePointerShapes);
                aggregates.MaxActiveSpritePointerColliders = Math.Max(aggregates.MaxActiveSpritePointerColliders, sample.ActiveSpritePointerColliders);
            }

            Array.Sort(sortedFrameMs);
            aggregates.AverageFrameMs = totalMs / sampleCount;
            aggregates.AverageFps = sampleCount / Math.Max(0.0001d, aggregates.ActualDurationSeconds);
            if (profilerFrameTimeSampleCount > 0) {
                aggregates.AverageProfilerFrameTimeMs = totalProfilerFrameTimeMs / profilerFrameTimeSampleCount;
            }

            aggregates.OnePercentLowFps = AverageSlowestFps(sortedFrameMs, 0.01d);
            aggregates.PointOnePercentLowFps = AverageSlowestFps(sortedFrameMs, 0.001d);
            aggregates.WorstFrameMs = sortedFrameMs[sortedFrameMs.Length - 1];
            aggregates.Classification = ClassifyCapture(aggregates);
            return aggregates;
        }

        private static double AverageSlowestFps(float[] sortedFrameMs, double fraction) {
            if (sortedFrameMs == null || sortedFrameMs.Length == 0) {
                return 0d;
            }

            int count = Math.Max(1, (int)Math.Ceiling(sortedFrameMs.Length * fraction));
            double totalMs = 0d;
            int start = Math.Max(0, sortedFrameMs.Length - count);
            for (int i = start; i < sortedFrameMs.Length; i++) {
                totalMs += sortedFrameMs[i];
            }

            double averageMs = totalMs / Math.Max(1, sortedFrameMs.Length - start);
            return averageMs > 0.0001d ? 1000d / averageMs : 0d;
        }

        private string ClassifyCapture(CaptureAggregates aggregates) {
            if (aggregates.MaxGcAllocBytes >= 1024L * 1024L &&
                aggregates.WorstFrameMs >= Math.Max(33.3d, aggregates.AverageFrameMs * 1.5d)) {
                return "GC spike";
            }

            if (aggregates.MemoryPeakBytes > 0 &&
                aggregates.MemoryStartBytes > 0 &&
                aggregates.MemoryPeakBytes - aggregates.MemoryStartBytes >= 128L * 1024L * 1024L) {
                return "memory spike";
            }

            if (aggregates.MaxRenderThreadMs >= Math.Max(20d, aggregates.AverageFrameMs * 1.35d) &&
                (aggregates.MaxMainThreadMs < 0d || aggregates.MaxRenderThreadMs > aggregates.MaxMainThreadMs * 1.1d)) {
                return "render spike";
            }

            if (aggregates.MaxMainThreadMs >= Math.Max(20d, aggregates.AverageFrameMs * 1.35d)) {
                return "CPU spike";
            }

            return "unknown";
        }

        private string ClassifyFrame(in FrameSample sample, long memoryPeakBytes) {
            if (sample.GcAllocBytes >= 1024L * 1024L) {
                return "GC spike";
            }

            double mainMs = sample.MainThreadNs >= 0 ? sample.MainThreadNs / 1e6d : -1d;
            double renderMs = sample.RenderThreadNs >= 0 ? sample.RenderThreadNs / 1e6d : -1d;
            if (renderMs >= 16.6d && renderMs > mainMs * 1.1d) {
                return "render spike";
            }

            if (mainMs >= 16.6d) {
                return "CPU spike";
            }

            if (memoryPeakBytes > 0 && sample.TotalUsedMemory == memoryPeakBytes) {
                return "memory spike";
            }

            return "unknown";
        }

        private string BuildSamplesCsv() {
            var sb = new StringBuilder(Math.Max(4096, sampleCount * 384));
            sb.AppendLine("frameIndex,timestamp,deltaTimeMs,fps,gcAllocBytes,mainThreadMs,renderThreadMs,totalUsedMemory,systemUsedMemory,drawCalls,batches,setPassCalls,triangles,vertices,activeActors,activeMobs,activeEffects,activeDamageNumbers,activeUIWindows,packetsProcessed,snapshotsBuilt,assetLoads,assetLoadBytes,actorOverlaySpriteCacheHits,actorOverlaySpriteCacheMisses,actorOverlaySpriteCacheBuildFailures,spriteAnimatorApplyFrameCalls,spriteAnimatorApplyFrameMs,spriteAnimatorMeshAssigns,spriteAnimatorMeshAssignMs,spriteAnimatorTextureAssigns,spriteAnimatorTextureAssignMs,spriteAnimatorMutableMeshUpdates,spriteAnimatorMutableMeshUpdateMs,spriteFrameMeshBuilds,spriteCompositedFrameBuilds,spriteGeometryCacheHits,spriteGeometryCacheMisses,spriteGeometryPrewarmQueued,spriteGeometryPrewarmBuilt,spriteMaterialCacheHits,spriteMaterialCacheMisses,actorOverlayUpdates,actorOverlayUpdateMs,actorOverlayApplyFrameCalls,actorOverlayApplyFrameMs,spriteEffectUpdates,spriteEffectUpdateMs,spriteEffectLateUpdates,spriteEffectLateUpdateMs,primitiveEffectUpdates,primitiveEffectUpdateMs,actorSortingUpdates,actorSortingUpdateMs,spawnedObjects,despawnedObjects,pathfindingRequests,pointerResolveTargets,pointerSyncTransforms,activeSpritePointerShapes,activeSpritePointerColliders,spritePointerBroadphaseChecks,spritePointerExactTests,spritePointerAlphaTests,spritePointerCacheHits,spritePointerCacheMisses,spritePointerColliderRaycasts,spritePointerColliderHits,spritePointerColliderRefreshes,pointerResolveMs,pointerWorldTargetsMs,pointerColliderRefreshMs,pointerColliderRefreshNowMs,pointerRaycastMs,pointerGroundPointMs,pointerResolverMs,clickToMoveUpdateMs,clickHandleMs,clickMoveToTargetMs,uiPointerCaptureMs,cursorSetPointerTargetMs,cursorVisualLateUpdateMs,cursorVisualApplyPointerMs,pointerScreenReportGuiMs,clickIntentResolveMs,actorServiceUpdateMs,actorServiceLateUpdateMs,effectManagerUpdateMs,minimalLoginShellUpdateMs,minimalLoginShellStableLightPathMs,minimalLoginShellApplyLiveDrainMs,minimalLoginShellApplyWorldDrainMs,minimalLoginShellApplyLiveDrainPostSyncMs,minimalLoginShellEnsurePhase12Ms,minimalLoginShellEnsureGameplayContextMs,minimalLoginShellRefreshGameplayWindowsMs,minimalLoginShellScheduleUiRefreshMs,onlineWorldUpdateMs,cameraLateUpdateMs,gridSelectorUpdateMs,runtimeMapLateUpdateMs,onGuiCalls,onGuiLayoutEvents,onGuiRepaintEvents,onGuiMouseMoveEvents,onGuiMs,minimalLoginShellOnGuiMs,actorHoverLabelOnGuiMs,actorShowScriptOnGuiMs,actorLightingDebugOnGuiMs,shortcutSettingsOnGuiMs,clickToMoveOnGuiMs,cursorVisualOnGuiMs,gridSelectorOnGuiMs");
            for (int i = 0; i < sampleCount; i++) {
                FrameSample sample = samples[i];
                sb.Append(sample.FrameIndex).Append(',');
                AppendFloat(sb, sample.TimestampSeconds).Append(',');
                AppendFloat(sb, sample.DeltaTimeMs).Append(',');
                AppendFloat(sb, sample.Fps).Append(',');
                AppendLongOrEmpty(sb, sample.GcAllocBytes).Append(',');
                AppendNsAsMsOrEmpty(sb, sample.MainThreadNs).Append(',');
                AppendNsAsMsOrEmpty(sb, sample.RenderThreadNs).Append(',');
                AppendLongOrEmpty(sb, sample.TotalUsedMemory).Append(',');
                AppendLongOrEmpty(sb, sample.SystemUsedMemory).Append(',');
                AppendLongOrEmpty(sb, sample.DrawCalls).Append(',');
                AppendLongOrEmpty(sb, sample.Batches).Append(',');
                AppendLongOrEmpty(sb, sample.SetPassCalls).Append(',');
                AppendLongOrEmpty(sb, sample.Triangles).Append(',');
                AppendLongOrEmpty(sb, sample.Vertices).Append(',');
                AppendIntOrEmpty(sb, sample.ActiveActors).Append(',');
                AppendIntOrEmpty(sb, sample.ActiveMobs).Append(',');
                AppendIntOrEmpty(sb, sample.ActiveEffects).Append(',');
                AppendIntOrEmpty(sb, sample.ActiveDamageNumbers).Append(',');
                AppendIntOrEmpty(sb, sample.ActiveUiWindows).Append(',');
                sb.Append(sample.PacketsProcessed).Append(',');
                sb.Append(sample.SnapshotsBuilt).Append(',');
                sb.Append(sample.AssetLoads).Append(',');
                sb.Append(sample.AssetLoadBytes).Append(',');
                sb.Append(sample.ActorOverlaySpriteCacheHits).Append(',');
                sb.Append(sample.ActorOverlaySpriteCacheMisses).Append(',');
                sb.Append(sample.ActorOverlaySpriteCacheBuildFailures).Append(',');
                sb.Append(sample.SpriteAnimatorApplyFrameCalls).Append(',');
                AppendTicksAsMs(sb, sample.SpriteAnimatorApplyFrameTicks).Append(',');
                sb.Append(sample.SpriteAnimatorMeshAssigns).Append(',');
                AppendTicksAsMs(sb, sample.SpriteAnimatorMeshAssignTicks).Append(',');
                sb.Append(sample.SpriteAnimatorTextureAssigns).Append(',');
                AppendTicksAsMs(sb, sample.SpriteAnimatorTextureAssignTicks).Append(',');
                sb.Append(sample.SpriteAnimatorMutableMeshUpdates).Append(',');
                AppendTicksAsMs(sb, sample.SpriteAnimatorMutableMeshUpdateTicks).Append(',');
                sb.Append(sample.SpriteFrameMeshBuilds).Append(',');
                sb.Append(sample.SpriteCompositedFrameBuilds).Append(',');
                sb.Append(sample.SpriteGeometryCacheHits).Append(',');
                sb.Append(sample.SpriteGeometryCacheMisses).Append(',');
                sb.Append(sample.SpriteGeometryPrewarmQueued).Append(',');
                sb.Append(sample.SpriteGeometryPrewarmBuilt).Append(',');
                sb.Append(sample.SpriteMaterialCacheHits).Append(',');
                sb.Append(sample.SpriteMaterialCacheMisses).Append(',');
                sb.Append(sample.ActorOverlayUpdates).Append(',');
                AppendTicksAsMs(sb, sample.ActorOverlayUpdateTicks).Append(',');
                sb.Append(sample.ActorOverlayApplyFrameCalls).Append(',');
                AppendTicksAsMs(sb, sample.ActorOverlayApplyFrameTicks).Append(',');
                sb.Append(sample.SpriteEffectUpdates).Append(',');
                AppendTicksAsMs(sb, sample.SpriteEffectUpdateTicks).Append(',');
                sb.Append(sample.SpriteEffectLateUpdates).Append(',');
                AppendTicksAsMs(sb, sample.SpriteEffectLateUpdateTicks).Append(',');
                sb.Append(sample.PrimitiveEffectUpdates).Append(',');
                AppendTicksAsMs(sb, sample.PrimitiveEffectUpdateTicks).Append(',');
                sb.Append(sample.ActorSortingUpdates).Append(',');
                AppendTicksAsMs(sb, sample.ActorSortingUpdateTicks).Append(',');
                sb.Append(sample.SpawnedObjects).Append(',');
                sb.Append(sample.DespawnedObjects).Append(',');
                AppendLongOrEmpty(sb, sample.PathfindingRequests).Append(',');
                sb.Append(sample.PointerResolveTargets).Append(',');
                sb.Append(sample.PointerSyncTransforms).Append(',');
                sb.Append(sample.ActiveSpritePointerShapes).Append(',');
                sb.Append(sample.ActiveSpritePointerColliders).Append(',');
                sb.Append(sample.SpritePointerBroadphaseChecks).Append(',');
                sb.Append(sample.SpritePointerExactTests).Append(',');
                sb.Append(sample.SpritePointerAlphaTests).Append(',');
                sb.Append(sample.SpritePointerCacheHits).Append(',');
                sb.Append(sample.SpritePointerCacheMisses).Append(',');
                sb.Append(sample.SpritePointerColliderRaycasts).Append(',');
                sb.Append(sample.SpritePointerColliderHits).Append(',');
                sb.Append(sample.SpritePointerColliderRefreshes).Append(',');
                AppendTicksAsMs(sb, sample.PointerResolveTicks).Append(',');
                AppendTicksAsMs(sb, sample.PointerWorldTargetsTicks).Append(',');
                AppendTicksAsMs(sb, sample.PointerColliderRefreshTicks).Append(',');
                AppendTicksAsMs(sb, sample.PointerColliderRefreshNowTicks).Append(',');
                AppendTicksAsMs(sb, sample.PointerRaycastTicks).Append(',');
                AppendTicksAsMs(sb, sample.PointerGroundPointTicks).Append(',');
                AppendTicksAsMs(sb, sample.PointerResolverTicks).Append(',');
                AppendTicksAsMs(sb, sample.ClickToMoveUpdateTicks).Append(',');
                AppendTicksAsMs(sb, sample.ClickHandleTicks).Append(',');
                AppendTicksAsMs(sb, sample.ClickMoveToTargetTicks).Append(',');
                AppendTicksAsMs(sb, sample.UiPointerCaptureTicks).Append(',');
                AppendTicksAsMs(sb, sample.CursorSetPointerTargetTicks).Append(',');
                AppendTicksAsMs(sb, sample.CursorVisualLateUpdateTicks).Append(',');
                AppendTicksAsMs(sb, sample.CursorVisualApplyPointerTicks).Append(',');
                AppendTicksAsMs(sb, sample.PointerScreenReportGuiTicks).Append(',');
                AppendTicksAsMs(sb, sample.ClickIntentResolveTicks).Append(',');
                AppendTicksAsMs(sb, sample.ActorServiceUpdateTicks).Append(',');
                AppendTicksAsMs(sb, sample.ActorServiceLateUpdateTicks).Append(',');
                AppendTicksAsMs(sb, sample.EffectManagerUpdateTicks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellUpdateTicks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellStableLightPathTicks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellApplyLiveDrainTicks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellApplyWorldDrainTicks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellApplyLiveDrainPostSyncTicks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellEnsurePhase12Ticks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellEnsureGameplayContextTicks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellRefreshGameplayWindowsTicks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellScheduleUiRefreshTicks).Append(',');
                AppendTicksAsMs(sb, sample.OnlineWorldUpdateTicks).Append(',');
                AppendTicksAsMs(sb, sample.CameraLateUpdateTicks).Append(',');
                AppendTicksAsMs(sb, sample.GridSelectorUpdateTicks).Append(',');
                AppendTicksAsMs(sb, sample.RuntimeMapLateUpdateTicks).Append(',');
                sb.Append(sample.OnGuiCalls).Append(',');
                sb.Append(sample.OnGuiLayoutEvents).Append(',');
                sb.Append(sample.OnGuiRepaintEvents).Append(',');
                sb.Append(sample.OnGuiMouseMoveEvents).Append(',');
                AppendTicksAsMs(sb, sample.OnGuiTicks).Append(',');
                AppendTicksAsMs(sb, sample.MinimalLoginShellOnGuiTicks).Append(',');
                AppendTicksAsMs(sb, sample.ActorHoverLabelOnGuiTicks).Append(',');
                AppendTicksAsMs(sb, sample.ActorShowScriptOnGuiTicks).Append(',');
                AppendTicksAsMs(sb, sample.ActorLightingDebugOnGuiTicks).Append(',');
                AppendTicksAsMs(sb, sample.ShortcutSettingsOnGuiTicks).Append(',');
                AppendTicksAsMs(sb, sample.ClickToMoveOnGuiTicks).Append(',');
                AppendTicksAsMs(sb, sample.CursorVisualOnGuiTicks).Append(',');
                AppendTicksAsMs(sb, sample.GridSelectorOnGuiTicks).AppendLine();
            }

            return sb.ToString();
        }

        private string BuildSummaryJson(CaptureAggregates aggregates) {
            var sb = new StringBuilder(8192);
            sb.Append("{\n");
            AppendJsonField(sb, "tool", "RoPerfCapture", 1, false);
            AppendJsonField(sb, "unityVersion", Application.unityVersion, 1, false);
            AppendJsonField(sb, "scene", sceneName, 1, false);
            AppendJsonField(sb, "captureStartedLocal", captureStartedLocal.ToString("o", CultureInfo.InvariantCulture), 1, false);
            AppendJsonField(sb, "captureEndedLocal", captureEndedLocal.ToString("o", CultureInfo.InvariantCulture), 1, false);
            AppendJsonField(sb, "captureStartedUtc", captureStartedUtc.ToString("o", CultureInfo.InvariantCulture), 1, false);
            AppendJsonField(sb, "captureEndedUtc", captureEndedUtc.ToString("o", CultureInfo.InvariantCulture), 1, false);
            AppendJsonField(sb, "stopReason", stopReason, 1, false);
            AppendJsonNumber(sb, "requestedDurationSeconds", durationSeconds, 1, false);
            AppendJsonNumber(sb, "actualDurationSeconds", aggregates.ActualDurationSeconds, 1, false);
            AppendJsonNumber(sb, "frameCount", aggregates.FrameCount, 1, false);
            AppendJsonBoolean(sb, "sampleBufferOverflow", bufferOverflow, 1, false);
            sb.Append("  \"aggregate\": {\n");
            AppendJsonNumber(sb, "averageFps", aggregates.AverageFps, 2, false);
            AppendJsonNumber(sb, "onePercentLowFps", aggregates.OnePercentLowFps, 2, false);
            AppendJsonNumber(sb, "pointOnePercentLowFps", aggregates.PointOnePercentLowFps, 2, false);
            AppendJsonNumber(sb, "averageFrameMs", aggregates.AverageFrameMs, 2, false);
            AppendJsonNullableDouble(sb, "averageProfilerFrameTimeMs", aggregates.AverageProfilerFrameTimeMs, 2, false);
            AppendJsonNullableDouble(sb, "maxProfilerFrameTimeMs", aggregates.MaxProfilerFrameTimeMs, 2, false);
            AppendJsonNullableInt(sb, "maxProfilerFrameTimeFrameIndex", aggregates.MaxProfilerFrameTimeMs >= 0d ? aggregates.MaxProfilerFrameTimeFrameIndex : UnavailableInt, 2, false);
            AppendJsonNumber(sb, "worstFrameMs", aggregates.WorstFrameMs, 2, false);
            AppendJsonNumber(sb, "worstFrameIndex", aggregates.WorstFrameIndex, 2, false);
            AppendJsonNumber(sb, "frameTimingSamples", frameTimingSamples, 2, false);
            AppendJsonNumber(sb, "averageGpuFrameMs", frameTimingSamples > 0 ? gpuFrameMsTotal / frameTimingSamples : -1d, 2, false);
            AppendJsonNumber(sb, "maxGpuFrameMs", frameTimingSamples > 0 ? gpuFrameMsMax : -1d, 2, false);
            AppendJsonNumber(sb, "averageCpuMainThreadMs", frameTimingSamples > 0 ? cpuMainThreadMsTotal / frameTimingSamples : -1d, 2, false);
            AppendJsonNumber(sb, "averageCpuRenderThreadMs", frameTimingSamples > 0 ? cpuRenderThreadMsTotal / frameTimingSamples : -1d, 2, false);
            AppendJsonNullableLong(sb, "maxGcAllocBytes", aggregates.MaxGcAllocBytes, 2, false);
            AppendJsonNullableInt(sb, "maxGcAllocFrameIndex", aggregates.MaxGcAllocBytes >= 0 ? aggregates.MaxGcAllocFrameIndex : UnavailableInt, 2, false);
            AppendJsonNullableLong(sb, "memoryPeakBytes", aggregates.MemoryPeakBytes, 2, false);
            AppendJsonNullableLong(sb, "systemMemoryPeakBytes", aggregates.SystemMemoryPeakBytes, 2, false);
            AppendJsonNullableLong(sb, "gcReservedMemoryPeakBytes", aggregates.GcReservedMemoryPeakBytes, 2, false);
            AppendJsonNullableDouble(sb, "maxMainThreadMs", aggregates.MaxMainThreadMs, 2, false);
            AppendJsonNullableDouble(sb, "maxRenderThreadMs", aggregates.MaxRenderThreadMs, 2, false);
            AppendJsonField(sb, "classification", aggregates.Classification, 2, true);
            sb.Append("  },\n");

            AppendUnavailableMetricsJson(sb);
            AppendCounterStatusesJson(sb);
            AppendPointerDiagnosticsJson(sb, aggregates);
            AppendActorOverlaySpriteCacheDiagnosticsJson(sb, aggregates);
            AppendSpriteCombatRendererDiagnosticsJson(sb, aggregates);
            AppendAssetLoadDiagnosticsJson(sb);
            AppendTopFramesJson(sb, aggregates);
            sb.Append("}\n");
            return sb.ToString();
        }

        private string BuildReportMarkdown(CaptureAggregates aggregates) {
            var sb = new StringBuilder(8192);
            sb.AppendLine("# Ragnarok Performance Capture");
            sb.AppendLine();
            sb.Append("- Capture start: ").AppendLine(captureStartedLocal.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
            sb.Append("- Capture end: ").AppendLine(captureEndedLocal.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
            sb.Append("- Duration: ").Append(FormatNumber(aggregates.ActualDurationSeconds)).AppendLine("s");
            sb.Append("- Scene: ").AppendLine(sceneName);
            sb.Append("- Stop reason: ").AppendLine(stopReason);
            sb.Append("- Sample buffer: ").Append(sampleCount).Append('/').Append(sampleCapacity).Append(bufferOverflow ? " (overflow)" : string.Empty).AppendLine();
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.Append("- Average FPS: ").AppendLine(FormatNumber(aggregates.AverageFps));
            sb.Append("- Average Profiler frame time: ").AppendLine(FormatMsOrUnavailable(aggregates.AverageProfilerFrameTimeMs));
            sb.Append("- Profiler frame time peak: ").Append(FormatMsOrUnavailable(aggregates.MaxProfilerFrameTimeMs));
            if (aggregates.MaxProfilerFrameTimeMs >= 0d) {
                sb.Append(" (frame ").Append(aggregates.MaxProfilerFrameTimeFrameIndex).Append(')');
            }

            sb.AppendLine();
            sb.Append("- 1% low: ").AppendLine(FormatNumber(aggregates.OnePercentLowFps));
            sb.Append("- 0.1% low: ").AppendLine(FormatNumber(aggregates.PointOnePercentLowFps));
            sb.Append("- Worst frame: ").Append(FormatNumber(aggregates.WorstFrameMs)).Append(" ms (frame ").Append(aggregates.WorstFrameIndex).AppendLine(")");
            sb.Append("- Max GC alloc frame: ").AppendLine(FormatBytesOrUnavailable(aggregates.MaxGcAllocBytes));
            sb.Append("- Memory peak: ").AppendLine(FormatBytesOrUnavailable(aggregates.MemoryPeakBytes));
            sb.Append("- System memory peak: ").AppendLine(FormatBytesOrUnavailable(aggregates.SystemMemoryPeakBytes));
            sb.Append("- GC reserved memory peak: ").AppendLine(FormatBytesOrUnavailable(aggregates.GcReservedMemoryPeakBytes));
            sb.Append("- Classification: ").AppendLine(aggregates.Classification);
            sb.AppendLine();
            AppendTopFramesMarkdown(sb, aggregates);
            AppendPointerDiagnosticsMarkdown(sb, aggregates);
            AppendActorOverlaySpriteCacheDiagnosticsMarkdown(sb, aggregates);
            AppendSpriteCombatRendererDiagnosticsMarkdown(sb, aggregates);
            AppendAssetLoadDiagnosticsMarkdown(sb);
            AppendUnavailableMetricsMarkdown(sb);
            AppendCounterStatusMarkdown(sb);
            sb.AppendLine("## Missing Instrumentation");
            for (int i = 0; i < MissingInstrumentation.Length; i++) {
                sb.Append("- ").AppendLine(MissingInstrumentation[i]);
            }

            return sb.ToString();
        }

        private void AppendTopFramesMarkdown(StringBuilder sb, CaptureAggregates aggregates) {
            sb.AppendLine("## Top 20 Worst Frames");
            sb.AppendLine("| Rank | Frame | Timestamp | Delta ms | FPS | Profiler frame ms | GC alloc | Asset loads | Asset bytes | Main ms | Render ms | Ptr resolve | Sync | Raycast | Broadphase | Exact | Ptr ms | World ms | Refresh ms | RefreshNow ms | Raycast ms | Ground ms | Resolver ms | CTM update ms | Handle ms | UI cap ms | Cursor late ms | Intent ms | Move ms | Actor ms | ActorLate ms | Effect ms | Shell ms | Shell light ms | LiveDrain ms | WorldDrain ms | PostSync ms | EnsureP12 ms | Context ms | RefreshWin ms | ScheduleRefresh ms | World ms | Camera ms | Grid ms | MapLate ms | OnGUI calls | OnGUI mouse | OnGUI ms | ShellGUI ms | HoverGUI ms | ClickGUI ms | CursorGUI ms | GridGUI ms | Sprite apply ms | Mesh assign ms | Tex assign ms | Mesh builds | Geom miss | Overlay upd ms | SpriteFX upd ms | Primitive upd ms | Sorting upd ms | Classification |");
            sb.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
            for (int i = 0; i < topWorstSampleIndices.Length; i++) {
                int sampleIndex = topWorstSampleIndices[i];
                if (sampleIndex < 0 || sampleIndex >= sampleCount) {
                    continue;
                }

                FrameSample sample = samples[sampleIndex];
                sb.Append("| ").Append(i + 1)
                    .Append(" | ").Append(sample.FrameIndex)
                    .Append(" | ").Append(FormatNumber(sample.TimestampSeconds))
                    .Append(" | ").Append(FormatNumber(sample.DeltaTimeMs))
                    .Append(" | ").Append(FormatNumber(sample.Fps))
                    .Append(" | ").Append(FormatMsOrUnavailable(sample.FrameTimeNs))
                    .Append(" | ").Append(FormatBytesOrUnavailable(sample.GcAllocBytes))
                    .Append(" | ").Append(sample.AssetLoads)
                    .Append(" | ").Append(FormatBytesOrUnavailable(sample.AssetLoadBytes))
                    .Append(" | ").Append(FormatMsOrUnavailable(sample.MainThreadNs))
                    .Append(" | ").Append(FormatMsOrUnavailable(sample.RenderThreadNs))
                    .Append(" | ").Append(sample.PointerResolveTargets)
                    .Append(" | ").Append(sample.PointerSyncTransforms)
                    .Append(" | ").Append(sample.SpritePointerColliderRaycasts)
                    .Append(" | ").Append(sample.SpritePointerBroadphaseChecks)
                    .Append(" | ").Append(sample.SpritePointerExactTests)
                    .Append(" | ").Append(FormatTicksAsMs(sample.PointerResolveTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.PointerWorldTargetsTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.PointerColliderRefreshTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.PointerColliderRefreshNowTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.PointerRaycastTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.PointerGroundPointTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.PointerResolverTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.ClickToMoveUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.ClickHandleTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.UiPointerCaptureTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.CursorVisualLateUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.ClickIntentResolveTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.ClickMoveToTargetTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.ActorServiceUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.ActorServiceLateUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.EffectManagerUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellStableLightPathTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellApplyLiveDrainTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellApplyWorldDrainTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellApplyLiveDrainPostSyncTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellEnsurePhase12Ticks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellEnsureGameplayContextTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellRefreshGameplayWindowsTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellScheduleUiRefreshTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.OnlineWorldUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.CameraLateUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.GridSelectorUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.RuntimeMapLateUpdateTicks))
                    .Append(" | ").Append(sample.OnGuiCalls)
                    .Append(" | ").Append(sample.OnGuiMouseMoveEvents)
                    .Append(" | ").Append(FormatTicksAsMs(sample.OnGuiTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.MinimalLoginShellOnGuiTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.ActorHoverLabelOnGuiTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.ClickToMoveOnGuiTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.CursorVisualOnGuiTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.GridSelectorOnGuiTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.SpriteAnimatorApplyFrameTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.SpriteAnimatorMeshAssignTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.SpriteAnimatorTextureAssignTicks))
                    .Append(" | ").Append(sample.SpriteFrameMeshBuilds)
                    .Append(" | ").Append(sample.SpriteGeometryCacheMisses)
                    .Append(" | ").Append(FormatTicksAsMs(sample.ActorOverlayUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.SpriteEffectUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.PrimitiveEffectUpdateTicks))
                    .Append(" | ").Append(FormatTicksAsMs(sample.ActorSortingUpdateTicks))
                    .Append(" | ").Append(ClassifyFrame(sample, aggregates.MemoryPeakBytes))
                    .AppendLine(" |");
            }

            sb.AppendLine();
        }

        private void AppendUnavailableMetricsMarkdown(StringBuilder sb) {
            sb.AppendLine("## Unavailable Metrics");
            bool any = false;
            for (int i = 0; i < recorders.Length; i++) {
                if (recorders[i].Available) {
                    continue;
                }

                any = true;
                sb.Append("- ").Append(recorders[i].Definition.DisplayName).Append(": metric unavailable (tried ");
                AppendTriedSpecs(sb, recorders[i].Definition);
                sb.AppendLine(")");
            }

            if (!any) {
                sb.AppendLine("- None");
            }

            sb.AppendLine();
        }

        private void AppendCounterStatusMarkdown(StringBuilder sb) {
            sb.AppendLine("## Runtime Counter Status");
            sb.AppendLine("| Counter | Status | Source / note |");
            sb.AppendLine("| --- | --- | --- |");
            for (int i = 0; i < CounterDefinitions.Length; i++) {
                CounterDefinition counter = CounterDefinitions[i];
                sb.Append("| ").Append(counter.Key).Append(" | ").Append(counter.Status).Append(" | ").Append(counter.Source).AppendLine(" |");
            }

            sb.AppendLine();
        }

        private void AppendUnavailableMetricsJson(StringBuilder sb) {
            sb.Append("  \"unavailableMetrics\": [\n");
            bool wrote = false;
            for (int i = 0; i < recorders.Length; i++) {
                if (recorders[i].Available) {
                    continue;
                }

                if (wrote) {
                    sb.Append(",\n");
                }

                sb.Append("    { \"key\": ").Append(Quote(recorders[i].Definition.Key))
                    .Append(", \"displayName\": ").Append(Quote(recorders[i].Definition.DisplayName))
                    .Append(", \"status\": \"metric unavailable\", \"tried\": ");
                AppendTriedSpecsJson(sb, recorders[i].Definition);
                sb.Append(" }");
                wrote = true;
            }

            sb.Append("\n  ],\n");
        }

        private void AppendCounterStatusesJson(StringBuilder sb) {
            sb.Append("  \"counterStatuses\": {\n");
            for (int i = 0; i < CounterDefinitions.Length; i++) {
                CounterDefinition counter = CounterDefinitions[i];
                sb.Append("    ").Append(Quote(counter.Key)).Append(": { \"status\": ")
                    .Append(Quote(counter.Status)).Append(", \"source\": ")
                    .Append(Quote(counter.Source)).Append(i == CounterDefinitions.Length - 1 ? " }\n" : " },\n");
            }

            sb.Append("  },\n");
        }

        private void AppendPointerDiagnosticsJson(StringBuilder sb, CaptureAggregates aggregates) {
            sb.Append("  \"pointerDiagnostics\": {\n");
            AppendJsonNumber(sb, "pointerResolveTargetsTotal", aggregates.PointerResolveTargetsTotal, 2, false);
            AppendJsonNumber(sb, "pointerSyncTransformsTotal", aggregates.PointerSyncTransformsTotal, 2, false);
            AppendJsonNumber(sb, "spritePointerBroadphaseChecksTotal", aggregates.SpritePointerBroadphaseChecksTotal, 2, false);
            AppendJsonNumber(sb, "spritePointerExactTestsTotal", aggregates.SpritePointerExactTestsTotal, 2, false);
            AppendJsonNumber(sb, "spritePointerAlphaTestsTotal", aggregates.SpritePointerAlphaTestsTotal, 2, false);
            AppendJsonNumber(sb, "spritePointerCacheHitsTotal", aggregates.SpritePointerCacheHitsTotal, 2, false);
            AppendJsonNumber(sb, "spritePointerCacheMissesTotal", aggregates.SpritePointerCacheMissesTotal, 2, false);
            AppendJsonNumber(sb, "spritePointerColliderRaycastsTotal", aggregates.SpritePointerColliderRaycastsTotal, 2, false);
            AppendJsonNumber(sb, "spritePointerColliderHitsTotal", aggregates.SpritePointerColliderHitsTotal, 2, false);
            AppendJsonNumber(sb, "spritePointerColliderRefreshesTotal", aggregates.SpritePointerColliderRefreshesTotal, 2, false);
            AppendJsonNumber(sb, "maxPointerResolveTargetsFrame", aggregates.MaxPointerResolveTargetsFrame, 2, false);
            AppendJsonNumber(sb, "maxPointerSyncTransformsFrame", aggregates.MaxPointerSyncTransformsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpritePointerBroadphaseChecksFrame", aggregates.MaxSpritePointerBroadphaseChecksFrame, 2, false);
            AppendJsonNumber(sb, "maxSpritePointerExactTestsFrame", aggregates.MaxSpritePointerExactTestsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpritePointerAlphaTestsFrame", aggregates.MaxSpritePointerAlphaTestsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpritePointerCacheHitsFrame", aggregates.MaxSpritePointerCacheHitsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpritePointerCacheMissesFrame", aggregates.MaxSpritePointerCacheMissesFrame, 2, false);
            AppendJsonNumber(sb, "maxSpritePointerColliderRaycastsFrame", aggregates.MaxSpritePointerColliderRaycastsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpritePointerColliderHitsFrame", aggregates.MaxSpritePointerColliderHitsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpritePointerColliderRefreshesFrame", aggregates.MaxSpritePointerColliderRefreshesFrame, 2, false);
            AppendJsonNumber(sb, "pointerResolveMsTotal", TicksToMs(aggregates.PointerResolveTicksTotal), 2, false);
            AppendJsonNumber(sb, "pointerWorldTargetsMsTotal", TicksToMs(aggregates.PointerWorldTargetsTicksTotal), 2, false);
            AppendJsonNumber(sb, "pointerColliderRefreshMsTotal", TicksToMs(aggregates.PointerColliderRefreshTicksTotal), 2, false);
            AppendJsonNumber(sb, "pointerColliderRefreshNowMsTotal", TicksToMs(aggregates.PointerColliderRefreshNowTicksTotal), 2, false);
            AppendJsonNumber(sb, "pointerRaycastMsTotal", TicksToMs(aggregates.PointerRaycastTicksTotal), 2, false);
            AppendJsonNumber(sb, "pointerGroundPointMsTotal", TicksToMs(aggregates.PointerGroundPointTicksTotal), 2, false);
            AppendJsonNumber(sb, "pointerResolverMsTotal", TicksToMs(aggregates.PointerResolverTicksTotal), 2, false);
            AppendJsonNumber(sb, "maxPointerResolveMsFrame", TicksToMs(aggregates.MaxPointerResolveTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxPointerWorldTargetsMsFrame", TicksToMs(aggregates.MaxPointerWorldTargetsTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxPointerColliderRefreshMsFrame", TicksToMs(aggregates.MaxPointerColliderRefreshTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxPointerColliderRefreshNowMsFrame", TicksToMs(aggregates.MaxPointerColliderRefreshNowTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxPointerRaycastMsFrame", TicksToMs(aggregates.MaxPointerRaycastTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxPointerGroundPointMsFrame", TicksToMs(aggregates.MaxPointerGroundPointTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxPointerResolverMsFrame", TicksToMs(aggregates.MaxPointerResolverTicksFrame), 2, false);
            AppendJsonNumber(sb, "clickToMoveUpdateMsTotal", TicksToMs(aggregates.ClickToMoveUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "clickHandleMsTotal", TicksToMs(aggregates.ClickHandleTicksTotal), 2, false);
            AppendJsonNumber(sb, "clickMoveToTargetMsTotal", TicksToMs(aggregates.ClickMoveToTargetTicksTotal), 2, false);
            AppendJsonNumber(sb, "uiPointerCaptureMsTotal", TicksToMs(aggregates.UiPointerCaptureTicksTotal), 2, false);
            AppendJsonNumber(sb, "cursorSetPointerTargetMsTotal", TicksToMs(aggregates.CursorSetPointerTargetTicksTotal), 2, false);
            AppendJsonNumber(sb, "cursorVisualLateUpdateMsTotal", TicksToMs(aggregates.CursorVisualLateUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "cursorVisualApplyPointerMsTotal", TicksToMs(aggregates.CursorVisualApplyPointerTicksTotal), 2, false);
            AppendJsonNumber(sb, "pointerScreenReportGuiMsTotal", TicksToMs(aggregates.PointerScreenReportGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "clickIntentResolveMsTotal", TicksToMs(aggregates.ClickIntentResolveTicksTotal), 2, false);
            AppendJsonNumber(sb, "maxClickToMoveUpdateMsFrame", TicksToMs(aggregates.MaxClickToMoveUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxClickHandleMsFrame", TicksToMs(aggregates.MaxClickHandleTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxClickMoveToTargetMsFrame", TicksToMs(aggregates.MaxClickMoveToTargetTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxUiPointerCaptureMsFrame", TicksToMs(aggregates.MaxUiPointerCaptureTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxCursorSetPointerTargetMsFrame", TicksToMs(aggregates.MaxCursorSetPointerTargetTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxCursorVisualLateUpdateMsFrame", TicksToMs(aggregates.MaxCursorVisualLateUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxCursorVisualApplyPointerMsFrame", TicksToMs(aggregates.MaxCursorVisualApplyPointerTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxPointerScreenReportGuiMsFrame", TicksToMs(aggregates.MaxPointerScreenReportGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxClickIntentResolveMsFrame", TicksToMs(aggregates.MaxClickIntentResolveTicksFrame), 2, false);
            AppendJsonNumber(sb, "actorServiceUpdateMsTotal", TicksToMs(aggregates.ActorServiceUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "actorServiceLateUpdateMsTotal", TicksToMs(aggregates.ActorServiceLateUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "effectManagerUpdateMsTotal", TicksToMs(aggregates.EffectManagerUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellUpdateMsTotal", TicksToMs(aggregates.MinimalLoginShellUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellStableLightPathMsTotal", TicksToMs(aggregates.MinimalLoginShellStableLightPathTicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellApplyLiveDrainMsTotal", TicksToMs(aggregates.MinimalLoginShellApplyLiveDrainTicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellApplyWorldDrainMsTotal", TicksToMs(aggregates.MinimalLoginShellApplyWorldDrainTicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellApplyLiveDrainPostSyncMsTotal", TicksToMs(aggregates.MinimalLoginShellApplyLiveDrainPostSyncTicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellEnsurePhase12MsTotal", TicksToMs(aggregates.MinimalLoginShellEnsurePhase12TicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellEnsureGameplayContextMsTotal", TicksToMs(aggregates.MinimalLoginShellEnsureGameplayContextTicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellRefreshGameplayWindowsMsTotal", TicksToMs(aggregates.MinimalLoginShellRefreshGameplayWindowsTicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellScheduleUiRefreshMsTotal", TicksToMs(aggregates.MinimalLoginShellScheduleUiRefreshTicksTotal), 2, false);
            AppendJsonNumber(sb, "onlineWorldUpdateMsTotal", TicksToMs(aggregates.OnlineWorldUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "cameraLateUpdateMsTotal", TicksToMs(aggregates.CameraLateUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "gridSelectorUpdateMsTotal", TicksToMs(aggregates.GridSelectorUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "runtimeMapLateUpdateMsTotal", TicksToMs(aggregates.RuntimeMapLateUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "maxActorServiceUpdateMsFrame", TicksToMs(aggregates.MaxActorServiceUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxActorServiceLateUpdateMsFrame", TicksToMs(aggregates.MaxActorServiceLateUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxEffectManagerUpdateMsFrame", TicksToMs(aggregates.MaxEffectManagerUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellUpdateMsFrame", TicksToMs(aggregates.MaxMinimalLoginShellUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellStableLightPathMsFrame", TicksToMs(aggregates.MaxMinimalLoginShellStableLightPathTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellApplyLiveDrainMsFrame", TicksToMs(aggregates.MaxMinimalLoginShellApplyLiveDrainTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellApplyWorldDrainMsFrame", TicksToMs(aggregates.MaxMinimalLoginShellApplyWorldDrainTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellApplyLiveDrainPostSyncMsFrame", TicksToMs(aggregates.MaxMinimalLoginShellApplyLiveDrainPostSyncTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellEnsurePhase12MsFrame", TicksToMs(aggregates.MaxMinimalLoginShellEnsurePhase12TicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellEnsureGameplayContextMsFrame", TicksToMs(aggregates.MaxMinimalLoginShellEnsureGameplayContextTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellRefreshGameplayWindowsMsFrame", TicksToMs(aggregates.MaxMinimalLoginShellRefreshGameplayWindowsTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellScheduleUiRefreshMsFrame", TicksToMs(aggregates.MaxMinimalLoginShellScheduleUiRefreshTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxOnlineWorldUpdateMsFrame", TicksToMs(aggregates.MaxOnlineWorldUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxCameraLateUpdateMsFrame", TicksToMs(aggregates.MaxCameraLateUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxGridSelectorUpdateMsFrame", TicksToMs(aggregates.MaxGridSelectorUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxRuntimeMapLateUpdateMsFrame", TicksToMs(aggregates.MaxRuntimeMapLateUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "onGuiCallsTotal", aggregates.OnGuiCallsTotal, 2, false);
            AppendJsonNumber(sb, "onGuiLayoutEventsTotal", aggregates.OnGuiLayoutEventsTotal, 2, false);
            AppendJsonNumber(sb, "onGuiRepaintEventsTotal", aggregates.OnGuiRepaintEventsTotal, 2, false);
            AppendJsonNumber(sb, "onGuiMouseMoveEventsTotal", aggregates.OnGuiMouseMoveEventsTotal, 2, false);
            AppendJsonNumber(sb, "onGuiMsTotal", TicksToMs(aggregates.OnGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "minimalLoginShellOnGuiMsTotal", TicksToMs(aggregates.MinimalLoginShellOnGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "actorHoverLabelOnGuiMsTotal", TicksToMs(aggregates.ActorHoverLabelOnGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "actorShowScriptOnGuiMsTotal", TicksToMs(aggregates.ActorShowScriptOnGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "actorLightingDebugOnGuiMsTotal", TicksToMs(aggregates.ActorLightingDebugOnGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "shortcutSettingsOnGuiMsTotal", TicksToMs(aggregates.ShortcutSettingsOnGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "clickToMoveOnGuiMsTotal", TicksToMs(aggregates.ClickToMoveOnGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "cursorVisualOnGuiMsTotal", TicksToMs(aggregates.CursorVisualOnGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "gridSelectorOnGuiMsTotal", TicksToMs(aggregates.GridSelectorOnGuiTicksTotal), 2, false);
            AppendJsonNumber(sb, "maxOnGuiCallsFrame", aggregates.MaxOnGuiCallsFrame, 2, false);
            AppendJsonNumber(sb, "maxOnGuiLayoutEventsFrame", aggregates.MaxOnGuiLayoutEventsFrame, 2, false);
            AppendJsonNumber(sb, "maxOnGuiRepaintEventsFrame", aggregates.MaxOnGuiRepaintEventsFrame, 2, false);
            AppendJsonNumber(sb, "maxOnGuiMouseMoveEventsFrame", aggregates.MaxOnGuiMouseMoveEventsFrame, 2, false);
            AppendJsonNumber(sb, "maxOnGuiMsFrame", TicksToMs(aggregates.MaxOnGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxMinimalLoginShellOnGuiMsFrame", TicksToMs(aggregates.MaxMinimalLoginShellOnGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxActorHoverLabelOnGuiMsFrame", TicksToMs(aggregates.MaxActorHoverLabelOnGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxActorShowScriptOnGuiMsFrame", TicksToMs(aggregates.MaxActorShowScriptOnGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxActorLightingDebugOnGuiMsFrame", TicksToMs(aggregates.MaxActorLightingDebugOnGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxShortcutSettingsOnGuiMsFrame", TicksToMs(aggregates.MaxShortcutSettingsOnGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxClickToMoveOnGuiMsFrame", TicksToMs(aggregates.MaxClickToMoveOnGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxCursorVisualOnGuiMsFrame", TicksToMs(aggregates.MaxCursorVisualOnGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxGridSelectorOnGuiMsFrame", TicksToMs(aggregates.MaxGridSelectorOnGuiTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxActiveSpritePointerShapes", aggregates.MaxActiveSpritePointerShapes, 2, false);
            AppendJsonNumber(sb, "maxActiveSpritePointerColliders", aggregates.MaxActiveSpritePointerColliders, 2, true);
            sb.Append("  },\n");
        }

        private static void AppendActorOverlaySpriteCacheDiagnosticsJson(StringBuilder sb, CaptureAggregates aggregates) {
            sb.Append("  \"actorOverlaySpriteCacheDiagnostics\": {\n");
            AppendJsonNumber(sb, "hitsTotal", aggregates.ActorOverlaySpriteCacheHitsTotal, 2, false);
            AppendJsonNumber(sb, "missesTotal", aggregates.ActorOverlaySpriteCacheMissesTotal, 2, false);
            AppendJsonNumber(sb, "buildFailuresTotal", aggregates.ActorOverlaySpriteCacheBuildFailuresTotal, 2, false);
            AppendJsonNumber(sb, "maxHitsFrame", aggregates.MaxActorOverlaySpriteCacheHitsFrame, 2, false);
            AppendJsonNumber(sb, "maxMissesFrame", aggregates.MaxActorOverlaySpriteCacheMissesFrame, 2, false);
            AppendJsonNumber(sb, "maxBuildFailuresFrame", aggregates.MaxActorOverlaySpriteCacheBuildFailuresFrame, 2, true);
            sb.Append("  },\n");
        }

        private static void AppendSpriteCombatRendererDiagnosticsJson(StringBuilder sb, CaptureAggregates aggregates) {
            sb.Append("  \"spriteCombatRendererDiagnostics\": {\n");
            AppendJsonNumber(sb, "spriteAnimatorApplyFrameCallsTotal", aggregates.SpriteAnimatorApplyFrameCallsTotal, 2, false);
            AppendJsonNumber(sb, "spriteAnimatorApplyFrameMsTotal", TicksToMs(aggregates.SpriteAnimatorApplyFrameTicksTotal), 2, false);
            AppendJsonNumber(sb, "spriteAnimatorMeshAssignsTotal", aggregates.SpriteAnimatorMeshAssignsTotal, 2, false);
            AppendJsonNumber(sb, "spriteAnimatorMeshAssignMsTotal", TicksToMs(aggregates.SpriteAnimatorMeshAssignTicksTotal), 2, false);
            AppendJsonNumber(sb, "spriteAnimatorTextureAssignsTotal", aggregates.SpriteAnimatorTextureAssignsTotal, 2, false);
            AppendJsonNumber(sb, "spriteAnimatorTextureAssignMsTotal", TicksToMs(aggregates.SpriteAnimatorTextureAssignTicksTotal), 2, false);
            AppendJsonNumber(sb, "spriteAnimatorMutableMeshUpdatesTotal", aggregates.SpriteAnimatorMutableMeshUpdatesTotal, 2, false);
            AppendJsonNumber(sb, "spriteAnimatorMutableMeshUpdateMsTotal", TicksToMs(aggregates.SpriteAnimatorMutableMeshUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "spriteFrameMeshBuildsTotal", aggregates.SpriteFrameMeshBuildsTotal, 2, false);
            AppendJsonNumber(sb, "spriteCompositedFrameBuildsTotal", aggregates.SpriteCompositedFrameBuildsTotal, 2, false);
            AppendJsonNumber(sb, "spriteGeometryCacheHitsTotal", aggregates.SpriteGeometryCacheHitsTotal, 2, false);
            AppendJsonNumber(sb, "spriteGeometryCacheMissesTotal", aggregates.SpriteGeometryCacheMissesTotal, 2, false);
            AppendJsonNumber(sb, "spriteGeometryPrewarmQueuedTotal", aggregates.SpriteGeometryPrewarmQueuedTotal, 2, false);
            AppendJsonNumber(sb, "spriteGeometryPrewarmBuiltTotal", aggregates.SpriteGeometryPrewarmBuiltTotal, 2, false);
            AppendJsonNumber(sb, "spriteMaterialCacheHitsTotal", aggregates.SpriteMaterialCacheHitsTotal, 2, false);
            AppendJsonNumber(sb, "spriteMaterialCacheMissesTotal", aggregates.SpriteMaterialCacheMissesTotal, 2, false);
            AppendJsonNumber(sb, "actorOverlayUpdatesTotal", aggregates.ActorOverlayUpdatesTotal, 2, false);
            AppendJsonNumber(sb, "actorOverlayUpdateMsTotal", TicksToMs(aggregates.ActorOverlayUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "actorOverlayApplyFrameCallsTotal", aggregates.ActorOverlayApplyFrameCallsTotal, 2, false);
            AppendJsonNumber(sb, "actorOverlayApplyFrameMsTotal", TicksToMs(aggregates.ActorOverlayApplyFrameTicksTotal), 2, false);
            AppendJsonNumber(sb, "spriteEffectUpdatesTotal", aggregates.SpriteEffectUpdatesTotal, 2, false);
            AppendJsonNumber(sb, "spriteEffectUpdateMsTotal", TicksToMs(aggregates.SpriteEffectUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "spriteEffectLateUpdatesTotal", aggregates.SpriteEffectLateUpdatesTotal, 2, false);
            AppendJsonNumber(sb, "spriteEffectLateUpdateMsTotal", TicksToMs(aggregates.SpriteEffectLateUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "primitiveEffectUpdatesTotal", aggregates.PrimitiveEffectUpdatesTotal, 2, false);
            AppendJsonNumber(sb, "primitiveEffectUpdateMsTotal", TicksToMs(aggregates.PrimitiveEffectUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "actorSortingUpdatesTotal", aggregates.ActorSortingUpdatesTotal, 2, false);
            AppendJsonNumber(sb, "actorSortingUpdateMsTotal", TicksToMs(aggregates.ActorSortingUpdateTicksTotal), 2, false);
            AppendJsonNumber(sb, "maxSpriteAnimatorApplyFrameCallsFrame", aggregates.MaxSpriteAnimatorApplyFrameCallsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpriteAnimatorApplyFrameMsFrame", TicksToMs(aggregates.MaxSpriteAnimatorApplyFrameTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxSpriteAnimatorMeshAssignsFrame", aggregates.MaxSpriteAnimatorMeshAssignsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpriteAnimatorMeshAssignMsFrame", TicksToMs(aggregates.MaxSpriteAnimatorMeshAssignTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxSpriteAnimatorTextureAssignsFrame", aggregates.MaxSpriteAnimatorTextureAssignsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpriteAnimatorTextureAssignMsFrame", TicksToMs(aggregates.MaxSpriteAnimatorTextureAssignTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxSpriteAnimatorMutableMeshUpdatesFrame", aggregates.MaxSpriteAnimatorMutableMeshUpdatesFrame, 2, false);
            AppendJsonNumber(sb, "maxSpriteAnimatorMutableMeshUpdateMsFrame", TicksToMs(aggregates.MaxSpriteAnimatorMutableMeshUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxSpriteFrameMeshBuildsFrame", aggregates.MaxSpriteFrameMeshBuildsFrame, 2, false);
            AppendJsonNumber(sb, "maxSpriteGeometryCacheMissesFrame", aggregates.MaxSpriteGeometryCacheMissesFrame, 2, false);
            AppendJsonNumber(sb, "maxActorOverlayUpdateMsFrame", TicksToMs(aggregates.MaxActorOverlayUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxActorOverlayApplyFrameMsFrame", TicksToMs(aggregates.MaxActorOverlayApplyFrameTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxSpriteEffectUpdateMsFrame", TicksToMs(aggregates.MaxSpriteEffectUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxSpriteEffectLateUpdateMsFrame", TicksToMs(aggregates.MaxSpriteEffectLateUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxPrimitiveEffectUpdateMsFrame", TicksToMs(aggregates.MaxPrimitiveEffectUpdateTicksFrame), 2, false);
            AppendJsonNumber(sb, "maxActorSortingUpdateMsFrame", TicksToMs(aggregates.MaxActorSortingUpdateTicksFrame), 2, true);
            sb.Append("  },\n");
        }

        private static void AppendPointerDiagnosticsMarkdown(StringBuilder sb, CaptureAggregates aggregates) {
            sb.AppendLine("## Pointer Diagnostics");
            sb.AppendLine("| Counter | Total | Max/frame |");
            sb.AppendLine("| --- | ---: | ---: |");
            AppendPointerCounterMarkdown(sb, "pointerResolveTargets", aggregates.PointerResolveTargetsTotal, aggregates.MaxPointerResolveTargetsFrame);
            AppendPointerCounterMarkdown(sb, "pointerSyncTransforms", aggregates.PointerSyncTransformsTotal, aggregates.MaxPointerSyncTransformsFrame);
            AppendPointerCounterMarkdown(sb, "spritePointerBroadphaseChecks", aggregates.SpritePointerBroadphaseChecksTotal, aggregates.MaxSpritePointerBroadphaseChecksFrame);
            AppendPointerCounterMarkdown(sb, "spritePointerExactTests", aggregates.SpritePointerExactTestsTotal, aggregates.MaxSpritePointerExactTestsFrame);
            AppendPointerCounterMarkdown(sb, "spritePointerAlphaTests", aggregates.SpritePointerAlphaTestsTotal, aggregates.MaxSpritePointerAlphaTestsFrame);
            AppendPointerCounterMarkdown(sb, "spritePointerCacheHits", aggregates.SpritePointerCacheHitsTotal, aggregates.MaxSpritePointerCacheHitsFrame);
            AppendPointerCounterMarkdown(sb, "spritePointerCacheMisses", aggregates.SpritePointerCacheMissesTotal, aggregates.MaxSpritePointerCacheMissesFrame);
            AppendPointerCounterMarkdown(sb, "spritePointerColliderRaycasts", aggregates.SpritePointerColliderRaycastsTotal, aggregates.MaxSpritePointerColliderRaycastsFrame);
            AppendPointerCounterMarkdown(sb, "spritePointerColliderHits", aggregates.SpritePointerColliderHitsTotal, aggregates.MaxSpritePointerColliderHitsFrame);
            AppendPointerCounterMarkdown(sb, "spritePointerColliderRefreshes", aggregates.SpritePointerColliderRefreshesTotal, aggregates.MaxSpritePointerColliderRefreshesFrame);
            AppendPointerCounterMarkdown(sb, "activeSpritePointerShapes", aggregates.MaxActiveSpritePointerShapes, aggregates.MaxActiveSpritePointerShapes);
            AppendPointerCounterMarkdown(sb, "activeSpritePointerColliders", aggregates.MaxActiveSpritePointerColliders, aggregates.MaxActiveSpritePointerColliders);
            sb.AppendLine();
            sb.AppendLine("### Pointer Timings");
            sb.AppendLine("| Timer | Total ms | Max/frame ms |");
            sb.AppendLine("| --- | ---: | ---: |");
            AppendPointerTimingMarkdown(sb, "pointerResolve", aggregates.PointerResolveTicksTotal, aggregates.MaxPointerResolveTicksFrame);
            AppendPointerTimingMarkdown(sb, "pointerWorldTargets", aggregates.PointerWorldTargetsTicksTotal, aggregates.MaxPointerWorldTargetsTicksFrame);
            AppendPointerTimingMarkdown(sb, "pointerColliderRefresh", aggregates.PointerColliderRefreshTicksTotal, aggregates.MaxPointerColliderRefreshTicksFrame);
            AppendPointerTimingMarkdown(sb, "pointerColliderRefreshNow", aggregates.PointerColliderRefreshNowTicksTotal, aggregates.MaxPointerColliderRefreshNowTicksFrame);
            AppendPointerTimingMarkdown(sb, "pointerRaycast", aggregates.PointerRaycastTicksTotal, aggregates.MaxPointerRaycastTicksFrame);
            AppendPointerTimingMarkdown(sb, "pointerGroundPoint", aggregates.PointerGroundPointTicksTotal, aggregates.MaxPointerGroundPointTicksFrame);
            AppendPointerTimingMarkdown(sb, "pointerResolver", aggregates.PointerResolverTicksTotal, aggregates.MaxPointerResolverTicksFrame);
            sb.AppendLine();
            sb.AppendLine("### Input Timings");
            sb.AppendLine("| Timer | Total ms | Max/frame ms |");
            sb.AppendLine("| --- | ---: | ---: |");
            AppendPointerTimingMarkdown(sb, "clickToMoveUpdate", aggregates.ClickToMoveUpdateTicksTotal, aggregates.MaxClickToMoveUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "clickHandle", aggregates.ClickHandleTicksTotal, aggregates.MaxClickHandleTicksFrame);
            AppendPointerTimingMarkdown(sb, "clickMoveToTarget", aggregates.ClickMoveToTargetTicksTotal, aggregates.MaxClickMoveToTargetTicksFrame);
            AppendPointerTimingMarkdown(sb, "uiPointerCapture", aggregates.UiPointerCaptureTicksTotal, aggregates.MaxUiPointerCaptureTicksFrame);
            AppendPointerTimingMarkdown(sb, "cursorSetPointerTarget", aggregates.CursorSetPointerTargetTicksTotal, aggregates.MaxCursorSetPointerTargetTicksFrame);
            AppendPointerTimingMarkdown(sb, "cursorVisualLateUpdate", aggregates.CursorVisualLateUpdateTicksTotal, aggregates.MaxCursorVisualLateUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "cursorVisualApplyPointer", aggregates.CursorVisualApplyPointerTicksTotal, aggregates.MaxCursorVisualApplyPointerTicksFrame);
            AppendPointerTimingMarkdown(sb, "pointerScreenReportGui", aggregates.PointerScreenReportGuiTicksTotal, aggregates.MaxPointerScreenReportGuiTicksFrame);
            AppendPointerTimingMarkdown(sb, "clickIntentResolve", aggregates.ClickIntentResolveTicksTotal, aggregates.MaxClickIntentResolveTicksFrame);
            sb.AppendLine();
            sb.AppendLine("### System Timings");
            sb.AppendLine("| Timer | Total ms | Max/frame ms |");
            sb.AppendLine("| --- | ---: | ---: |");
            AppendPointerTimingMarkdown(sb, "actorServiceUpdate", aggregates.ActorServiceUpdateTicksTotal, aggregates.MaxActorServiceUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "actorServiceLateUpdate", aggregates.ActorServiceLateUpdateTicksTotal, aggregates.MaxActorServiceLateUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "effectManagerUpdate", aggregates.EffectManagerUpdateTicksTotal, aggregates.MaxEffectManagerUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellUpdate", aggregates.MinimalLoginShellUpdateTicksTotal, aggregates.MaxMinimalLoginShellUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellStableLightPath", aggregates.MinimalLoginShellStableLightPathTicksTotal, aggregates.MaxMinimalLoginShellStableLightPathTicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellApplyLiveDrain", aggregates.MinimalLoginShellApplyLiveDrainTicksTotal, aggregates.MaxMinimalLoginShellApplyLiveDrainTicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellApplyWorldDrain", aggregates.MinimalLoginShellApplyWorldDrainTicksTotal, aggregates.MaxMinimalLoginShellApplyWorldDrainTicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellApplyLiveDrainPostSync", aggregates.MinimalLoginShellApplyLiveDrainPostSyncTicksTotal, aggregates.MaxMinimalLoginShellApplyLiveDrainPostSyncTicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellEnsurePhase12", aggregates.MinimalLoginShellEnsurePhase12TicksTotal, aggregates.MaxMinimalLoginShellEnsurePhase12TicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellEnsureGameplayContext", aggregates.MinimalLoginShellEnsureGameplayContextTicksTotal, aggregates.MaxMinimalLoginShellEnsureGameplayContextTicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellRefreshGameplayWindows", aggregates.MinimalLoginShellRefreshGameplayWindowsTicksTotal, aggregates.MaxMinimalLoginShellRefreshGameplayWindowsTicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellScheduleUiRefresh", aggregates.MinimalLoginShellScheduleUiRefreshTicksTotal, aggregates.MaxMinimalLoginShellScheduleUiRefreshTicksFrame);
            AppendPointerTimingMarkdown(sb, "onlineWorldUpdate", aggregates.OnlineWorldUpdateTicksTotal, aggregates.MaxOnlineWorldUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "cameraLateUpdate", aggregates.CameraLateUpdateTicksTotal, aggregates.MaxCameraLateUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "gridSelectorUpdate", aggregates.GridSelectorUpdateTicksTotal, aggregates.MaxGridSelectorUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "runtimeMapLateUpdate", aggregates.RuntimeMapLateUpdateTicksTotal, aggregates.MaxRuntimeMapLateUpdateTicksFrame);
            sb.AppendLine();

            sb.AppendLine("### IMGUI Diagnostics");
            sb.AppendLine("| Counter | Total | Max/frame |");
            sb.AppendLine("| --- | ---: | ---: |");
            AppendPointerCounterMarkdown(sb, "onGuiCalls", aggregates.OnGuiCallsTotal, aggregates.MaxOnGuiCallsFrame);
            AppendPointerCounterMarkdown(sb, "onGuiLayoutEvents", aggregates.OnGuiLayoutEventsTotal, aggregates.MaxOnGuiLayoutEventsFrame);
            AppendPointerCounterMarkdown(sb, "onGuiRepaintEvents", aggregates.OnGuiRepaintEventsTotal, aggregates.MaxOnGuiRepaintEventsFrame);
            AppendPointerCounterMarkdown(sb, "onGuiMouseMoveEvents", aggregates.OnGuiMouseMoveEventsTotal, aggregates.MaxOnGuiMouseMoveEventsFrame);
            sb.AppendLine();

            sb.AppendLine("| Timer | Total ms | Max/frame ms |");
            sb.AppendLine("| --- | ---: | ---: |");
            AppendPointerTimingMarkdown(sb, "onGui", aggregates.OnGuiTicksTotal, aggregates.MaxOnGuiTicksFrame);
            AppendPointerTimingMarkdown(sb, "minimalLoginShellOnGui", aggregates.MinimalLoginShellOnGuiTicksTotal, aggregates.MaxMinimalLoginShellOnGuiTicksFrame);
            AppendPointerTimingMarkdown(sb, "actorHoverLabelOnGui", aggregates.ActorHoverLabelOnGuiTicksTotal, aggregates.MaxActorHoverLabelOnGuiTicksFrame);
            AppendPointerTimingMarkdown(sb, "actorShowScriptOnGui", aggregates.ActorShowScriptOnGuiTicksTotal, aggregates.MaxActorShowScriptOnGuiTicksFrame);
            AppendPointerTimingMarkdown(sb, "actorLightingDebugOnGui", aggregates.ActorLightingDebugOnGuiTicksTotal, aggregates.MaxActorLightingDebugOnGuiTicksFrame);
            AppendPointerTimingMarkdown(sb, "shortcutSettingsOnGui", aggregates.ShortcutSettingsOnGuiTicksTotal, aggregates.MaxShortcutSettingsOnGuiTicksFrame);
            AppendPointerTimingMarkdown(sb, "clickToMoveOnGui", aggregates.ClickToMoveOnGuiTicksTotal, aggregates.MaxClickToMoveOnGuiTicksFrame);
            AppendPointerTimingMarkdown(sb, "cursorVisualOnGui", aggregates.CursorVisualOnGuiTicksTotal, aggregates.MaxCursorVisualOnGuiTicksFrame);
            AppendPointerTimingMarkdown(sb, "gridSelectorOnGui", aggregates.GridSelectorOnGuiTicksTotal, aggregates.MaxGridSelectorOnGuiTicksFrame);
            sb.AppendLine();
        }

        private static void AppendActorOverlaySpriteCacheDiagnosticsMarkdown(StringBuilder sb, CaptureAggregates aggregates) {
            sb.AppendLine("## Actor Overlay Sprite Cache Diagnostics");
            sb.AppendLine("| Counter | Total | Max/frame |");
            sb.AppendLine("| --- | ---: | ---: |");
            AppendPointerCounterMarkdown(sb, "actorOverlaySpriteCacheHits", aggregates.ActorOverlaySpriteCacheHitsTotal, aggregates.MaxActorOverlaySpriteCacheHitsFrame);
            AppendPointerCounterMarkdown(sb, "actorOverlaySpriteCacheMisses", aggregates.ActorOverlaySpriteCacheMissesTotal, aggregates.MaxActorOverlaySpriteCacheMissesFrame);
            AppendPointerCounterMarkdown(sb, "actorOverlaySpriteCacheBuildFailures", aggregates.ActorOverlaySpriteCacheBuildFailuresTotal, aggregates.MaxActorOverlaySpriteCacheBuildFailuresFrame);
            sb.AppendLine();
        }

        private static void AppendSpriteCombatRendererDiagnosticsMarkdown(StringBuilder sb, CaptureAggregates aggregates) {
            sb.AppendLine("## Sprite/Combat Renderer Diagnostics");
            sb.AppendLine("| Counter | Total | Max/frame |");
            sb.AppendLine("| --- | ---: | ---: |");
            AppendPointerCounterMarkdown(sb, "spriteAnimatorApplyFrameCalls", aggregates.SpriteAnimatorApplyFrameCallsTotal, aggregates.MaxSpriteAnimatorApplyFrameCallsFrame);
            AppendPointerCounterMarkdown(sb, "spriteAnimatorMeshAssigns", aggregates.SpriteAnimatorMeshAssignsTotal, aggregates.MaxSpriteAnimatorMeshAssignsFrame);
            AppendPointerCounterMarkdown(sb, "spriteAnimatorTextureAssigns", aggregates.SpriteAnimatorTextureAssignsTotal, aggregates.MaxSpriteAnimatorTextureAssignsFrame);
            AppendPointerCounterMarkdown(sb, "spriteAnimatorMutableMeshUpdates", aggregates.SpriteAnimatorMutableMeshUpdatesTotal, aggregates.MaxSpriteAnimatorMutableMeshUpdatesFrame);
            AppendPointerCounterMarkdown(sb, "spriteFrameMeshBuilds", aggregates.SpriteFrameMeshBuildsTotal, aggregates.MaxSpriteFrameMeshBuildsFrame);
            AppendPointerCounterMarkdown(sb, "spriteCompositedFrameBuilds", aggregates.SpriteCompositedFrameBuildsTotal, aggregates.MaxSpriteCompositedFrameBuildsFrame);
            AppendPointerCounterMarkdown(sb, "spriteGeometryCacheHits", aggregates.SpriteGeometryCacheHitsTotal, aggregates.MaxSpriteGeometryCacheHitsFrame);
            AppendPointerCounterMarkdown(sb, "spriteGeometryCacheMisses", aggregates.SpriteGeometryCacheMissesTotal, aggregates.MaxSpriteGeometryCacheMissesFrame);
            AppendPointerCounterMarkdown(sb, "spriteGeometryPrewarmQueued", aggregates.SpriteGeometryPrewarmQueuedTotal, aggregates.MaxSpriteGeometryPrewarmQueuedFrame);
            AppendPointerCounterMarkdown(sb, "spriteGeometryPrewarmBuilt", aggregates.SpriteGeometryPrewarmBuiltTotal, aggregates.MaxSpriteGeometryPrewarmBuiltFrame);
            AppendPointerCounterMarkdown(sb, "spriteMaterialCacheHits", aggregates.SpriteMaterialCacheHitsTotal, aggregates.MaxSpriteMaterialCacheHitsFrame);
            AppendPointerCounterMarkdown(sb, "spriteMaterialCacheMisses", aggregates.SpriteMaterialCacheMissesTotal, aggregates.MaxSpriteMaterialCacheMissesFrame);
            AppendPointerCounterMarkdown(sb, "actorOverlayUpdates", aggregates.ActorOverlayUpdatesTotal, aggregates.MaxActorOverlayUpdatesFrame);
            AppendPointerCounterMarkdown(sb, "actorOverlayApplyFrameCalls", aggregates.ActorOverlayApplyFrameCallsTotal, aggregates.MaxActorOverlayApplyFrameCallsFrame);
            AppendPointerCounterMarkdown(sb, "spriteEffectUpdates", aggregates.SpriteEffectUpdatesTotal, aggregates.MaxSpriteEffectUpdatesFrame);
            AppendPointerCounterMarkdown(sb, "spriteEffectLateUpdates", aggregates.SpriteEffectLateUpdatesTotal, aggregates.MaxSpriteEffectLateUpdatesFrame);
            AppendPointerCounterMarkdown(sb, "primitiveEffectUpdates", aggregates.PrimitiveEffectUpdatesTotal, aggregates.MaxPrimitiveEffectUpdatesFrame);
            AppendPointerCounterMarkdown(sb, "actorSortingUpdates", aggregates.ActorSortingUpdatesTotal, aggregates.MaxActorSortingUpdatesFrame);
            sb.AppendLine();

            sb.AppendLine("| Timer | Total ms | Max/frame ms |");
            sb.AppendLine("| --- | ---: | ---: |");
            AppendPointerTimingMarkdown(sb, "spriteAnimatorApplyFrame", aggregates.SpriteAnimatorApplyFrameTicksTotal, aggregates.MaxSpriteAnimatorApplyFrameTicksFrame);
            AppendPointerTimingMarkdown(sb, "spriteAnimatorMeshAssign", aggregates.SpriteAnimatorMeshAssignTicksTotal, aggregates.MaxSpriteAnimatorMeshAssignTicksFrame);
            AppendPointerTimingMarkdown(sb, "spriteAnimatorTextureAssign", aggregates.SpriteAnimatorTextureAssignTicksTotal, aggregates.MaxSpriteAnimatorTextureAssignTicksFrame);
            AppendPointerTimingMarkdown(sb, "spriteAnimatorMutableMeshUpdate", aggregates.SpriteAnimatorMutableMeshUpdateTicksTotal, aggregates.MaxSpriteAnimatorMutableMeshUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "actorOverlayUpdate", aggregates.ActorOverlayUpdateTicksTotal, aggregates.MaxActorOverlayUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "actorOverlayApplyFrame", aggregates.ActorOverlayApplyFrameTicksTotal, aggregates.MaxActorOverlayApplyFrameTicksFrame);
            AppendPointerTimingMarkdown(sb, "spriteEffectUpdate", aggregates.SpriteEffectUpdateTicksTotal, aggregates.MaxSpriteEffectUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "spriteEffectLateUpdate", aggregates.SpriteEffectLateUpdateTicksTotal, aggregates.MaxSpriteEffectLateUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "primitiveEffectUpdate", aggregates.PrimitiveEffectUpdateTicksTotal, aggregates.MaxPrimitiveEffectUpdateTicksFrame);
            AppendPointerTimingMarkdown(sb, "actorSortingUpdate", aggregates.ActorSortingUpdateTicksTotal, aggregates.MaxActorSortingUpdateTicksFrame);
            sb.AppendLine();
        }

        private static void AppendPointerCounterMarkdown(StringBuilder sb, string name, long total, long maxFrame) {
            sb.Append("| ").Append(name)
                .Append(" | ").Append(total)
                .Append(" | ").Append(maxFrame)
                .AppendLine(" |");
        }

        private static void AppendPointerTimingMarkdown(StringBuilder sb, string name, long totalTicks, long maxFrameTicks) {
            sb.Append("| ").Append(name)
                .Append(" | ").Append(FormatTicksAsMs(totalTicks))
                .Append(" | ").Append(FormatTicksAsMs(maxFrameTicks))
                .AppendLine(" |");
        }

        private int BuildAssetLoadFrameSummaries(AssetLoadFrameSummary[] summaries) {
            if (assetLoadEvents == null || summaries == null) {
                return 0;
            }

            int summaryCount = 0;
            for (int i = 0; i < assetLoadEvents.Length; i++) {
                RoAssetLoadEvent loadEvent = assetLoadEvents[i];
                int summaryIndex = -1;
                for (int s = 0; s < summaryCount; s++) {
                    if (summaries[s].FrameIndex == loadEvent.FrameIndex) {
                        summaryIndex = s;
                        break;
                    }
                }

                if (summaryIndex < 0) {
                    summaryIndex = summaryCount;
                    summaryCount++;
                    summaries[summaryIndex].FrameIndex = loadEvent.FrameIndex;
                }

                summaries[summaryIndex].Count++;
                if (loadEvent.ByteCount > 0) {
                    summaries[summaryIndex].Bytes += loadEvent.ByteCount;
                }

                AddAssetLoadPath(ref summaries[summaryIndex], loadEvent.Path);
            }

            return summaryCount;
        }

        private static void AddAssetLoadPath(ref AssetLoadFrameSummary summary, string path) {
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            if (summary.Path0 == null) {
                summary.Path0 = path;
                return;
            }

            if (summary.Path0 == path || string.Equals(summary.Path0, path, StringComparison.Ordinal)) {
                return;
            }

            if (summary.Path1 == null) {
                summary.Path1 = path;
                return;
            }

            if (summary.Path1 == path || string.Equals(summary.Path1, path, StringComparison.Ordinal)) {
                return;
            }

            if (summary.Path2 == null) {
                summary.Path2 = path;
                return;
            }

            if (summary.Path2 == path || string.Equals(summary.Path2, path, StringComparison.Ordinal)) {
                return;
            }

            summary.ExtraPathCount++;
        }

        private static void InsertTopAssetLoadFrame(AssetLoadFrameSummary[] summaries, int summaryIndex, int[] topIndices) {
            for (int i = 0; i < topIndices.Length; i++) {
                int existingIndex = topIndices[i];
                if (existingIndex >= 0 && !IsAssetLoadSummaryBetter(summaries[summaryIndex], summaries[existingIndex])) {
                    continue;
                }

                for (int j = topIndices.Length - 1; j > i; j--) {
                    topIndices[j] = topIndices[j - 1];
                }

                topIndices[i] = summaryIndex;
                return;
            }
        }

        private static bool IsAssetLoadSummaryBetter(AssetLoadFrameSummary candidate, AssetLoadFrameSummary existing) {
            if (candidate.Bytes != existing.Bytes) {
                return candidate.Bytes > existing.Bytes;
            }

            if (candidate.Count != existing.Count) {
                return candidate.Count > existing.Count;
            }

            return candidate.FrameIndex < existing.FrameIndex;
        }

        private void AppendAssetLoadDiagnosticsJson(StringBuilder sb) {
            AssetLoadFrameSummary[] summaries = BuildAssetLoadSummariesAndTopIndices(out int summaryCount, out int[] topIndices);
            sb.Append("  \"assetLoadDiagnostics\": {\n");
            AppendJsonNumber(sb, "recordedEvents", assetLoadEvents != null ? assetLoadEvents.Length : 0, 2, false);
            AppendJsonNumber(sb, "eventBufferOverflow", assetLoadEventOverflow, 2, false);
            sb.Append("    \"topFramesByBytes\": [\n");
            bool wrote = false;
            for (int i = 0; i < topIndices.Length; i++) {
                int summaryIndex = topIndices[i];
                if (summaryIndex < 0 || summaryIndex >= summaryCount) {
                    continue;
                }

                if (wrote) {
                    sb.Append(",\n");
                }

                AssetLoadFrameSummary summary = summaries[summaryIndex];
                sb.Append("      { \"rank\": ").Append(i + 1)
                    .Append(", \"frameIndex\": ").Append(summary.FrameIndex)
                    .Append(", \"loads\": ").Append(summary.Count)
                    .Append(", \"bytes\": ").Append(summary.Bytes)
                    .Append(", \"paths\": [");
                AppendAssetLoadPathJson(sb, summary.Path0, false);
                AppendAssetLoadPathJson(sb, summary.Path1, summary.Path0 != null);
                AppendAssetLoadPathJson(sb, summary.Path2, summary.Path0 != null || summary.Path1 != null);
                sb.Append("], \"extraPathCount\": ").Append(summary.ExtraPathCount).Append(" }");
                wrote = true;
            }

            sb.Append("\n    ]\n");
            sb.Append("  },\n");
        }

        private void AppendAssetLoadDiagnosticsMarkdown(StringBuilder sb) {
            AssetLoadFrameSummary[] summaries = BuildAssetLoadSummariesAndTopIndices(out int summaryCount, out int[] topIndices);
            sb.AppendLine("## Asset Load Diagnostics");
            sb.Append("- Recorded asset load events: ").Append(assetLoadEvents != null ? assetLoadEvents.Length : 0).AppendLine();
            sb.Append("- Event buffer overflow: ").Append(assetLoadEventOverflow).AppendLine();
            sb.AppendLine("- Note: asset load work can appear one frame before the large deltaTime frame because the capture samples frame delta on the next Update.");
            sb.AppendLine();
            sb.AppendLine("| Rank | Frame | Loads | Bytes | First paths |");
            sb.AppendLine("| ---: | ---: | ---: | ---: | --- |");
            for (int i = 0; i < topIndices.Length; i++) {
                int summaryIndex = topIndices[i];
                if (summaryIndex < 0 || summaryIndex >= summaryCount) {
                    continue;
                }

                AssetLoadFrameSummary summary = summaries[summaryIndex];
                sb.Append("| ").Append(i + 1)
                    .Append(" | ").Append(summary.FrameIndex)
                    .Append(" | ").Append(summary.Count)
                    .Append(" | ").Append(summary.Bytes)
                    .Append(" | ");
                AppendAssetLoadPathsMarkdown(sb, summary);
                sb.AppendLine(" |");
            }

            if (summaryCount == 0) {
                sb.AppendLine("|  |  |  |  | No asset load events recorded |");
            }

            sb.AppendLine();
        }

        private AssetLoadFrameSummary[] BuildAssetLoadSummariesAndTopIndices(out int summaryCount, out int[] topIndices) {
            int eventCount = assetLoadEvents != null ? assetLoadEvents.Length : 0;
            var summaries = new AssetLoadFrameSummary[eventCount];
            summaryCount = BuildAssetLoadFrameSummaries(summaries);
            int topCount = Math.Min(40, Math.Max(1, summaryCount));
            topIndices = new int[topCount];
            for (int i = 0; i < topIndices.Length; i++) {
                topIndices[i] = -1;
            }

            for (int i = 0; i < summaryCount; i++) {
                InsertTopAssetLoadFrame(summaries, i, topIndices);
            }

            return summaries;
        }

        private static void AppendAssetLoadPathJson(StringBuilder sb, string path, bool prependComma) {
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            if (prependComma) {
                sb.Append(", ");
            }

            sb.Append(Quote(path));
        }

        private static void AppendAssetLoadPathsMarkdown(StringBuilder sb, AssetLoadFrameSummary summary) {
            bool wrote = AppendAssetLoadPathMarkdown(sb, summary.Path0, false);
            wrote = AppendAssetLoadPathMarkdown(sb, summary.Path1, wrote) || wrote;
            wrote = AppendAssetLoadPathMarkdown(sb, summary.Path2, wrote) || wrote;
            if (summary.ExtraPathCount > 0) {
                if (wrote) {
                    sb.Append("<br>");
                }

                sb.Append("+").Append(summary.ExtraPathCount).Append(" more");
                wrote = true;
            }

            if (!wrote) {
                sb.Append("unknown");
            }
        }

        private static bool AppendAssetLoadPathMarkdown(StringBuilder sb, string path, bool prependBreak) {
            if (string.IsNullOrEmpty(path)) {
                return false;
            }

            if (prependBreak) {
                sb.Append("<br>");
            }

            for (int i = 0; i < path.Length; i++) {
                char c = path[i];
                sb.Append(c == '|' ? '/' : c);
            }

            return true;
        }

        private void AppendTopFramesJson(StringBuilder sb, CaptureAggregates aggregates) {
            sb.Append("  \"topSpikeFrames\": [\n");
            bool wrote = false;
            for (int i = 0; i < topWorstSampleIndices.Length; i++) {
                int sampleIndex = topWorstSampleIndices[i];
                if (sampleIndex < 0 || sampleIndex >= sampleCount) {
                    continue;
                }

                if (wrote) {
                    sb.Append(",\n");
                }

                FrameSample sample = samples[sampleIndex];
                sb.Append("    { ");
                sb.Append("\"rank\": ").Append(i + 1).Append(", ");
                sb.Append("\"frameIndex\": ").Append(sample.FrameIndex).Append(", ");
                sb.Append("\"timestamp\": ").Append(FormatNumber(sample.TimestampSeconds)).Append(", ");
                sb.Append("\"deltaTimeMs\": ").Append(FormatNumber(sample.DeltaTimeMs)).Append(", ");
                sb.Append("\"fps\": ").Append(FormatNumber(sample.Fps)).Append(", ");
                sb.Append("\"profilerFrameTimeMs\": ").Append(sample.FrameTimeNs >= 0 ? FormatNumber(sample.FrameTimeNs / 1e6d) : "null").Append(", ");
                sb.Append("\"gcAllocBytes\": ").Append(sample.GcAllocBytes >= 0 ? sample.GcAllocBytes.ToString(CultureInfo.InvariantCulture) : "null").Append(", ");
                sb.Append("\"assetLoads\": ").Append(sample.AssetLoads).Append(", ");
                sb.Append("\"assetLoadBytes\": ").Append(sample.AssetLoadBytes).Append(", ");
                sb.Append("\"actorOverlaySpriteCacheHits\": ").Append(sample.ActorOverlaySpriteCacheHits).Append(", ");
                sb.Append("\"actorOverlaySpriteCacheMisses\": ").Append(sample.ActorOverlaySpriteCacheMisses).Append(", ");
                sb.Append("\"actorOverlaySpriteCacheBuildFailures\": ").Append(sample.ActorOverlaySpriteCacheBuildFailures).Append(", ");
                sb.Append("\"spriteAnimatorApplyFrameCalls\": ").Append(sample.SpriteAnimatorApplyFrameCalls).Append(", ");
                sb.Append("\"spriteAnimatorApplyFrameMs\": ").Append(FormatTicksAsMs(sample.SpriteAnimatorApplyFrameTicks)).Append(", ");
                sb.Append("\"spriteAnimatorMeshAssigns\": ").Append(sample.SpriteAnimatorMeshAssigns).Append(", ");
                sb.Append("\"spriteAnimatorMeshAssignMs\": ").Append(FormatTicksAsMs(sample.SpriteAnimatorMeshAssignTicks)).Append(", ");
                sb.Append("\"spriteAnimatorTextureAssigns\": ").Append(sample.SpriteAnimatorTextureAssigns).Append(", ");
                sb.Append("\"spriteAnimatorTextureAssignMs\": ").Append(FormatTicksAsMs(sample.SpriteAnimatorTextureAssignTicks)).Append(", ");
                sb.Append("\"spriteAnimatorMutableMeshUpdates\": ").Append(sample.SpriteAnimatorMutableMeshUpdates).Append(", ");
                sb.Append("\"spriteAnimatorMutableMeshUpdateMs\": ").Append(FormatTicksAsMs(sample.SpriteAnimatorMutableMeshUpdateTicks)).Append(", ");
                sb.Append("\"spriteFrameMeshBuilds\": ").Append(sample.SpriteFrameMeshBuilds).Append(", ");
                sb.Append("\"spriteCompositedFrameBuilds\": ").Append(sample.SpriteCompositedFrameBuilds).Append(", ");
                sb.Append("\"spriteGeometryCacheHits\": ").Append(sample.SpriteGeometryCacheHits).Append(", ");
                sb.Append("\"spriteGeometryCacheMisses\": ").Append(sample.SpriteGeometryCacheMisses).Append(", ");
                sb.Append("\"spriteGeometryPrewarmQueued\": ").Append(sample.SpriteGeometryPrewarmQueued).Append(", ");
                sb.Append("\"spriteGeometryPrewarmBuilt\": ").Append(sample.SpriteGeometryPrewarmBuilt).Append(", ");
                sb.Append("\"spriteMaterialCacheHits\": ").Append(sample.SpriteMaterialCacheHits).Append(", ");
                sb.Append("\"spriteMaterialCacheMisses\": ").Append(sample.SpriteMaterialCacheMisses).Append(", ");
                sb.Append("\"actorOverlayUpdates\": ").Append(sample.ActorOverlayUpdates).Append(", ");
                sb.Append("\"actorOverlayUpdateMs\": ").Append(FormatTicksAsMs(sample.ActorOverlayUpdateTicks)).Append(", ");
                sb.Append("\"actorOverlayApplyFrameCalls\": ").Append(sample.ActorOverlayApplyFrameCalls).Append(", ");
                sb.Append("\"actorOverlayApplyFrameMs\": ").Append(FormatTicksAsMs(sample.ActorOverlayApplyFrameTicks)).Append(", ");
                sb.Append("\"spriteEffectUpdates\": ").Append(sample.SpriteEffectUpdates).Append(", ");
                sb.Append("\"spriteEffectUpdateMs\": ").Append(FormatTicksAsMs(sample.SpriteEffectUpdateTicks)).Append(", ");
                sb.Append("\"spriteEffectLateUpdates\": ").Append(sample.SpriteEffectLateUpdates).Append(", ");
                sb.Append("\"spriteEffectLateUpdateMs\": ").Append(FormatTicksAsMs(sample.SpriteEffectLateUpdateTicks)).Append(", ");
                sb.Append("\"primitiveEffectUpdates\": ").Append(sample.PrimitiveEffectUpdates).Append(", ");
                sb.Append("\"primitiveEffectUpdateMs\": ").Append(FormatTicksAsMs(sample.PrimitiveEffectUpdateTicks)).Append(", ");
                sb.Append("\"actorSortingUpdates\": ").Append(sample.ActorSortingUpdates).Append(", ");
                sb.Append("\"actorSortingUpdateMs\": ").Append(FormatTicksAsMs(sample.ActorSortingUpdateTicks)).Append(", ");
                sb.Append("\"mainThreadMs\": ").Append(sample.MainThreadNs >= 0 ? FormatNumber(sample.MainThreadNs / 1e6d) : "null").Append(", ");
                sb.Append("\"renderThreadMs\": ").Append(sample.RenderThreadNs >= 0 ? FormatNumber(sample.RenderThreadNs / 1e6d) : "null").Append(", ");
                sb.Append("\"pointerResolveTargets\": ").Append(sample.PointerResolveTargets).Append(", ");
                sb.Append("\"pointerSyncTransforms\": ").Append(sample.PointerSyncTransforms).Append(", ");
                sb.Append("\"spritePointerBroadphaseChecks\": ").Append(sample.SpritePointerBroadphaseChecks).Append(", ");
                sb.Append("\"spritePointerExactTests\": ").Append(sample.SpritePointerExactTests).Append(", ");
                sb.Append("\"spritePointerColliderRaycasts\": ").Append(sample.SpritePointerColliderRaycasts).Append(", ");
                sb.Append("\"spritePointerColliderRefreshes\": ").Append(sample.SpritePointerColliderRefreshes).Append(", ");
                sb.Append("\"pointerResolveMs\": ").Append(FormatTicksAsMs(sample.PointerResolveTicks)).Append(", ");
                sb.Append("\"pointerWorldTargetsMs\": ").Append(FormatTicksAsMs(sample.PointerWorldTargetsTicks)).Append(", ");
                sb.Append("\"pointerColliderRefreshMs\": ").Append(FormatTicksAsMs(sample.PointerColliderRefreshTicks)).Append(", ");
                sb.Append("\"pointerColliderRefreshNowMs\": ").Append(FormatTicksAsMs(sample.PointerColliderRefreshNowTicks)).Append(", ");
                sb.Append("\"pointerRaycastMs\": ").Append(FormatTicksAsMs(sample.PointerRaycastTicks)).Append(", ");
                sb.Append("\"pointerGroundPointMs\": ").Append(FormatTicksAsMs(sample.PointerGroundPointTicks)).Append(", ");
                sb.Append("\"pointerResolverMs\": ").Append(FormatTicksAsMs(sample.PointerResolverTicks)).Append(", ");
                sb.Append("\"clickToMoveUpdateMs\": ").Append(FormatTicksAsMs(sample.ClickToMoveUpdateTicks)).Append(", ");
                sb.Append("\"clickHandleMs\": ").Append(FormatTicksAsMs(sample.ClickHandleTicks)).Append(", ");
                sb.Append("\"clickMoveToTargetMs\": ").Append(FormatTicksAsMs(sample.ClickMoveToTargetTicks)).Append(", ");
                sb.Append("\"uiPointerCaptureMs\": ").Append(FormatTicksAsMs(sample.UiPointerCaptureTicks)).Append(", ");
                sb.Append("\"cursorSetPointerTargetMs\": ").Append(FormatTicksAsMs(sample.CursorSetPointerTargetTicks)).Append(", ");
                sb.Append("\"cursorVisualLateUpdateMs\": ").Append(FormatTicksAsMs(sample.CursorVisualLateUpdateTicks)).Append(", ");
                sb.Append("\"cursorVisualApplyPointerMs\": ").Append(FormatTicksAsMs(sample.CursorVisualApplyPointerTicks)).Append(", ");
                sb.Append("\"pointerScreenReportGuiMs\": ").Append(FormatTicksAsMs(sample.PointerScreenReportGuiTicks)).Append(", ");
                sb.Append("\"clickIntentResolveMs\": ").Append(FormatTicksAsMs(sample.ClickIntentResolveTicks)).Append(", ");
                sb.Append("\"actorServiceUpdateMs\": ").Append(FormatTicksAsMs(sample.ActorServiceUpdateTicks)).Append(", ");
                sb.Append("\"actorServiceLateUpdateMs\": ").Append(FormatTicksAsMs(sample.ActorServiceLateUpdateTicks)).Append(", ");
                sb.Append("\"effectManagerUpdateMs\": ").Append(FormatTicksAsMs(sample.EffectManagerUpdateTicks)).Append(", ");
                sb.Append("\"minimalLoginShellUpdateMs\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellUpdateTicks)).Append(", ");
                sb.Append("\"minimalLoginShellStableLightPathMs\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellStableLightPathTicks)).Append(", ");
                sb.Append("\"minimalLoginShellApplyLiveDrainMs\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellApplyLiveDrainTicks)).Append(", ");
                sb.Append("\"minimalLoginShellApplyWorldDrainMs\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellApplyWorldDrainTicks)).Append(", ");
                sb.Append("\"minimalLoginShellApplyLiveDrainPostSyncMs\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellApplyLiveDrainPostSyncTicks)).Append(", ");
                sb.Append("\"minimalLoginShellEnsurePhase12Ms\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellEnsurePhase12Ticks)).Append(", ");
                sb.Append("\"minimalLoginShellEnsureGameplayContextMs\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellEnsureGameplayContextTicks)).Append(", ");
                sb.Append("\"minimalLoginShellRefreshGameplayWindowsMs\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellRefreshGameplayWindowsTicks)).Append(", ");
                sb.Append("\"minimalLoginShellScheduleUiRefreshMs\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellScheduleUiRefreshTicks)).Append(", ");
                sb.Append("\"onlineWorldUpdateMs\": ").Append(FormatTicksAsMs(sample.OnlineWorldUpdateTicks)).Append(", ");
                sb.Append("\"cameraLateUpdateMs\": ").Append(FormatTicksAsMs(sample.CameraLateUpdateTicks)).Append(", ");
                sb.Append("\"gridSelectorUpdateMs\": ").Append(FormatTicksAsMs(sample.GridSelectorUpdateTicks)).Append(", ");
                sb.Append("\"runtimeMapLateUpdateMs\": ").Append(FormatTicksAsMs(sample.RuntimeMapLateUpdateTicks)).Append(", ");
                sb.Append("\"onGuiCalls\": ").Append(sample.OnGuiCalls).Append(", ");
                sb.Append("\"onGuiLayoutEvents\": ").Append(sample.OnGuiLayoutEvents).Append(", ");
                sb.Append("\"onGuiRepaintEvents\": ").Append(sample.OnGuiRepaintEvents).Append(", ");
                sb.Append("\"onGuiMouseMoveEvents\": ").Append(sample.OnGuiMouseMoveEvents).Append(", ");
                sb.Append("\"onGuiMs\": ").Append(FormatTicksAsMs(sample.OnGuiTicks)).Append(", ");
                sb.Append("\"minimalLoginShellOnGuiMs\": ").Append(FormatTicksAsMs(sample.MinimalLoginShellOnGuiTicks)).Append(", ");
                sb.Append("\"actorHoverLabelOnGuiMs\": ").Append(FormatTicksAsMs(sample.ActorHoverLabelOnGuiTicks)).Append(", ");
                sb.Append("\"actorShowScriptOnGuiMs\": ").Append(FormatTicksAsMs(sample.ActorShowScriptOnGuiTicks)).Append(", ");
                sb.Append("\"actorLightingDebugOnGuiMs\": ").Append(FormatTicksAsMs(sample.ActorLightingDebugOnGuiTicks)).Append(", ");
                sb.Append("\"shortcutSettingsOnGuiMs\": ").Append(FormatTicksAsMs(sample.ShortcutSettingsOnGuiTicks)).Append(", ");
                sb.Append("\"clickToMoveOnGuiMs\": ").Append(FormatTicksAsMs(sample.ClickToMoveOnGuiTicks)).Append(", ");
                sb.Append("\"cursorVisualOnGuiMs\": ").Append(FormatTicksAsMs(sample.CursorVisualOnGuiTicks)).Append(", ");
                sb.Append("\"gridSelectorOnGuiMs\": ").Append(FormatTicksAsMs(sample.GridSelectorOnGuiTicks)).Append(", ");
                sb.Append("\"classification\": ").Append(Quote(ClassifyFrame(sample, aggregates.MemoryPeakBytes)));
                sb.Append(" }");
                wrote = true;
            }

            sb.Append("\n  ]\n");
        }

        private static StringBuilder AppendFloat(StringBuilder sb, float value) {
            sb.Append(value.ToString("0.######", CultureInfo.InvariantCulture));
            return sb;
        }

        private static StringBuilder AppendLongOrEmpty(StringBuilder sb, long value) {
            if (value >= 0) {
                sb.Append(value.ToString(CultureInfo.InvariantCulture));
            }

            return sb;
        }

        private static StringBuilder AppendIntOrEmpty(StringBuilder sb, int value) {
            if (value >= 0) {
                sb.Append(value.ToString(CultureInfo.InvariantCulture));
            }

            return sb;
        }

        private static StringBuilder AppendNsAsMsOrEmpty(StringBuilder sb, long value) {
            if (value >= 0) {
                sb.Append(FormatNumber(value / 1e6d));
            }

            return sb;
        }

        private static StringBuilder AppendTicksAsMs(StringBuilder sb, long ticks) {
            sb.Append(FormatNumber(TicksToMs(ticks)));
            return sb;
        }

        private static string FormatTicksAsMs(long ticks) {
            return FormatNumber(TicksToMs(ticks));
        }

        private static double TicksToMs(long ticks) {
            return ticks > 0 ? ticks * 1000d / System.Diagnostics.Stopwatch.Frequency : 0d;
        }

        private static void AppendJsonField(StringBuilder sb, string name, string value, int indent, bool last) {
            AppendIndent(sb, indent).Append(Quote(name)).Append(": ").Append(Quote(value)).Append(last ? "\n" : ",\n");
        }

        private static void AppendJsonNumber(StringBuilder sb, string name, double value, int indent, bool last) {
            AppendIndent(sb, indent).Append(Quote(name)).Append(": ").Append(FormatNumber(value)).Append(last ? "\n" : ",\n");
        }

        private static void AppendJsonBoolean(StringBuilder sb, string name, bool value, int indent, bool last) {
            AppendIndent(sb, indent).Append(Quote(name)).Append(": ").Append(value ? "true" : "false").Append(last ? "\n" : ",\n");
        }

        private static void AppendJsonNullableLong(StringBuilder sb, string name, long value, int indent, bool last) {
            AppendIndent(sb, indent).Append(Quote(name)).Append(": ")
                .Append(value >= 0 ? value.ToString(CultureInfo.InvariantCulture) : "null")
                .Append(last ? "\n" : ",\n");
        }

        private static void AppendJsonNullableInt(StringBuilder sb, string name, int value, int indent, bool last) {
            AppendIndent(sb, indent).Append(Quote(name)).Append(": ")
                .Append(value >= 0 ? value.ToString(CultureInfo.InvariantCulture) : "null")
                .Append(last ? "\n" : ",\n");
        }

        private static void AppendJsonNullableDouble(StringBuilder sb, string name, double value, int indent, bool last) {
            AppendIndent(sb, indent).Append(Quote(name)).Append(": ")
                .Append(value >= 0d ? FormatNumber(value) : "null")
                .Append(last ? "\n" : ",\n");
        }

        private static StringBuilder AppendIndent(StringBuilder sb, int indent) {
            return sb.Append(' ', indent * 2);
        }

        private static void AppendTriedSpecs(StringBuilder sb, MetricDefinition definition) {
            for (int i = 0; i < definition.SpecCount; i++) {
                if (i > 0) {
                    sb.Append(", ");
                }

                sb.Append(definition.GetSpec(i).StatName);
            }
        }

        private static void AppendTriedSpecsJson(StringBuilder sb, MetricDefinition definition) {
            sb.Append('[');
            for (int i = 0; i < definition.SpecCount; i++) {
                if (i > 0) {
                    sb.Append(", ");
                }

                sb.Append(Quote(definition.GetSpec(i).StatName));
            }

            sb.Append(']');
        }

        private static string FormatMsOrUnavailable(long ns) {
            return ns >= 0 ? FormatNumber(ns / 1e6d) : "metric unavailable";
        }

        private static string FormatMsOrUnavailable(double ms) {
            return ms >= 0d ? FormatNumber(ms) : "metric unavailable";
        }

        private static string FormatBytesOrUnavailable(long bytes) {
            if (bytes < 0) {
                return "metric unavailable";
            }

            return bytes.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatNumber(double value) {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string Quote(string value) {
            if (value == null) {
                value = string.Empty;
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private void DisposeRecorders() {
            if (recorders == null) {
                return;
            }

            for (int i = 0; i < recorders.Length; i++) {
                MetricRecorder recorder = recorders[i];
                if (recorder != null && recorder.Recorder.Valid) {
                    recorder.Recorder.Dispose();
                }
            }
        }
    }
}
