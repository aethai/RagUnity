using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Profiling;

namespace Ragnarok.Diagnostics {
    [Serializable]
    public sealed class RoRuntimeDiagnosticsOptions {
        public bool EnablePacketTrace;
        public bool EnablePacketLogWindow;
        public bool EnableRuntimePerfLog;
        public bool EnableCombatTrace;
        public bool EnableCombatTraceConsoleLog;
        public bool EnableMovementTrace;
        public bool EnableMovementTraceConsoleLog;
        public bool EnableVerboseNetworkTraceMessages;

        public static RoRuntimeDiagnosticsOptions DisabledForRuntime() => new RoRuntimeDiagnosticsOptions();

        public RoRuntimeDiagnosticsOptions Clone() {
            return new RoRuntimeDiagnosticsOptions {
                EnablePacketTrace = EnablePacketTrace,
                EnablePacketLogWindow = EnablePacketLogWindow,
                EnableRuntimePerfLog = EnableRuntimePerfLog,
                EnableCombatTrace = EnableCombatTrace,
                EnableCombatTraceConsoleLog = EnableCombatTraceConsoleLog,
                EnableMovementTrace = EnableMovementTrace,
                EnableMovementTraceConsoleLog = EnableMovementTraceConsoleLog,
                EnableVerboseNetworkTraceMessages = EnableVerboseNetworkTraceMessages,
            };
        }

        public bool EffectivePacketTraceEnabled => EnablePacketTrace || EnablePacketLogWindow;
    }

    public static class RoRuntimeDiagnostics {
        private static volatile bool packetTraceEnabled;
        private static volatile bool packetLogWindowEnabled;
        private static volatile bool runtimePerfLogEnabled;
        private static volatile bool combatTraceEnabled;
        private static volatile bool combatTraceConsoleLogEnabled;
        private static volatile bool movementTraceEnabled;
        private static volatile bool movementTraceConsoleLogEnabled;
        private static volatile bool verboseNetworkTraceMessagesEnabled;

        public static bool PacketTraceEnabled => packetTraceEnabled;
        public static bool PacketLogWindowEnabled => packetLogWindowEnabled;
        public static bool RuntimePerfLogEnabled => runtimePerfLogEnabled;
        public static bool CombatTraceEnabled => combatTraceEnabled;
        public static bool CombatTraceConsoleLogEnabled => combatTraceConsoleLogEnabled;
        public static bool MovementTraceEnabled => movementTraceEnabled;
        public static bool MovementTraceConsoleLogEnabled => movementTraceConsoleLogEnabled;
        public static bool VerboseNetworkTraceMessagesEnabled => verboseNetworkTraceMessagesEnabled;

        public static RoRuntimeDiagnosticsOptions Normalize(RoRuntimeDiagnosticsOptions options) {
            RoRuntimeDiagnosticsOptions normalized = options != null
                ? options.Clone()
                : RoRuntimeDiagnosticsOptions.DisabledForRuntime();
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            normalized.EnablePacketTrace = false;
            normalized.EnablePacketLogWindow = false;
            normalized.EnableRuntimePerfLog = false;
            normalized.EnableCombatTrace = false;
            normalized.EnableCombatTraceConsoleLog = false;
            normalized.EnableMovementTrace = false;
            normalized.EnableMovementTraceConsoleLog = false;
            normalized.EnableVerboseNetworkTraceMessages = false;
#endif
            if (!normalized.EnableCombatTrace) {
                normalized.EnableCombatTraceConsoleLog = false;
            }

            if (!normalized.EnableMovementTrace) {
                normalized.EnableMovementTraceConsoleLog = false;
            }

            return normalized;
        }

        public static void Apply(RoRuntimeDiagnosticsOptions options) {
            RoRuntimeDiagnosticsOptions normalized = Normalize(options);
            packetTraceEnabled = normalized.EnablePacketTrace;
            packetLogWindowEnabled = normalized.EnablePacketLogWindow;
            runtimePerfLogEnabled = normalized.EnableRuntimePerfLog;
            combatTraceEnabled = normalized.EnableCombatTrace;
            combatTraceConsoleLogEnabled = normalized.EnableCombatTraceConsoleLog;
            movementTraceEnabled = normalized.EnableMovementTrace;
            movementTraceConsoleLogEnabled = normalized.EnableMovementTraceConsoleLog;
            verboseNetworkTraceMessagesEnabled = normalized.EnableVerboseNetworkTraceMessages;
        }

        public static bool ShouldLogVerboseDiagnostics => verboseNetworkTraceMessagesEnabled;

        public static void IncrementDebugLogLine() {
            RoRuntimePerfCounters.IncrementDebugLogLine();
        }
    }

    public readonly struct RoPerformanceCounterFrameSnapshot {
        public RoPerformanceCounterFrameSnapshot(
            long packetsProcessed,
            long snapshotsBuilt,
            long assetLoads,
            long assetLoadBytes,
            long actorOverlaySpriteCacheHits,
            long actorOverlaySpriteCacheMisses,
            long actorOverlaySpriteCacheBuildFailures,
            long spriteAnimatorApplyFrameCalls,
            long spriteAnimatorApplyFrameTicks,
            long spriteAnimatorMeshAssigns,
            long spriteAnimatorMeshAssignTicks,
            long spriteAnimatorTextureAssigns,
            long spriteAnimatorTextureAssignTicks,
            long spriteAnimatorMutableMeshUpdates,
            long spriteAnimatorMutableMeshUpdateTicks,
            long spriteFrameMeshBuilds,
            long spriteCompositedFrameBuilds,
            long spriteGeometryCacheHits,
            long spriteGeometryCacheMisses,
            long spriteGeometryPrewarmQueued,
            long spriteGeometryPrewarmBuilt,
            long spriteMaterialCacheHits,
            long spriteMaterialCacheMisses,
            long actorOverlayUpdates,
            long actorOverlayUpdateTicks,
            long actorOverlayApplyFrameCalls,
            long actorOverlayApplyFrameTicks,
            long spriteEffectUpdates,
            long spriteEffectUpdateTicks,
            long spriteEffectLateUpdates,
            long spriteEffectLateUpdateTicks,
            long primitiveEffectUpdates,
            long primitiveEffectUpdateTicks,
            long actorSortingUpdates,
            long actorSortingUpdateTicks,
            long spawnedObjects,
            long despawnedObjects,
            long pathfindingRequests,
            long pointerResolveTargets,
            long pointerSyncTransforms,
            long pointerResolveTicks,
            long pointerWorldTargetsTicks,
            long pointerColliderRefreshTicks,
            long pointerColliderRefreshNowTicks,
            long pointerRaycastTicks,
            long pointerGroundPointTicks,
            long pointerResolverTicks,
            long clickToMoveUpdateTicks,
            long clickHandleTicks,
            long clickMoveToTargetTicks,
            long uiPointerCaptureTicks,
            long cursorSetPointerTargetTicks,
            long cursorVisualLateUpdateTicks,
            long cursorVisualApplyPointerTicks,
            long pointerScreenReportGuiTicks,
            long clickIntentResolveTicks,
            long actorServiceUpdateTicks,
            long actorServiceLateUpdateTicks,
            long effectManagerUpdateTicks,
            long minimalLoginShellUpdateTicks,
            long minimalLoginShellStableLightPathTicks,
            long minimalLoginShellApplyLiveDrainTicks,
            long minimalLoginShellApplyWorldDrainTicks,
            long minimalLoginShellApplyLiveDrainPostSyncTicks,
            long minimalLoginShellEnsurePhase12Ticks,
            long minimalLoginShellEnsureGameplayContextTicks,
            long minimalLoginShellRefreshGameplayWindowsTicks,
            long minimalLoginShellScheduleUiRefreshTicks,
            long onlineWorldUpdateTicks,
            long cameraLateUpdateTicks,
            long gridSelectorUpdateTicks,
            long runtimeMapLateUpdateTicks,
            long onGuiCalls,
            long onGuiLayoutEvents,
            long onGuiRepaintEvents,
            long onGuiMouseMoveEvents,
            long onGuiTicks,
            long minimalLoginShellOnGuiTicks,
            long actorHoverLabelOnGuiTicks,
            long actorShowScriptOnGuiTicks,
            long actorLightingDebugOnGuiTicks,
            long shortcutSettingsOnGuiTicks,
            long clickToMoveOnGuiTicks,
            long cursorVisualOnGuiTicks,
            long gridSelectorOnGuiTicks,
            int activeSpritePointerShapeCount,
            int activeSpritePointerColliderCount,
            long spritePointerBroadphaseChecks,
            long spritePointerExactTests,
            long spritePointerAlphaTests,
            long spritePointerCacheHits,
            long spritePointerCacheMisses,
            long spritePointerColliderRaycasts,
            long spritePointerColliderHits,
            long spritePointerColliderRefreshes,
            long spritePointerRendererRecaches,
            long spritePointerColliderMeshSwaps
        ) {
            PacketsProcessed = packetsProcessed;
            SnapshotsBuilt = snapshotsBuilt;
            AssetLoads = assetLoads;
            AssetLoadBytes = assetLoadBytes;
            ActorOverlaySpriteCacheHits = actorOverlaySpriteCacheHits;
            ActorOverlaySpriteCacheMisses = actorOverlaySpriteCacheMisses;
            ActorOverlaySpriteCacheBuildFailures = actorOverlaySpriteCacheBuildFailures;
            SpriteAnimatorApplyFrameCalls = spriteAnimatorApplyFrameCalls;
            SpriteAnimatorApplyFrameTicks = spriteAnimatorApplyFrameTicks;
            SpriteAnimatorMeshAssigns = spriteAnimatorMeshAssigns;
            SpriteAnimatorMeshAssignTicks = spriteAnimatorMeshAssignTicks;
            SpriteAnimatorTextureAssigns = spriteAnimatorTextureAssigns;
            SpriteAnimatorTextureAssignTicks = spriteAnimatorTextureAssignTicks;
            SpriteAnimatorMutableMeshUpdates = spriteAnimatorMutableMeshUpdates;
            SpriteAnimatorMutableMeshUpdateTicks = spriteAnimatorMutableMeshUpdateTicks;
            SpriteFrameMeshBuilds = spriteFrameMeshBuilds;
            SpriteCompositedFrameBuilds = spriteCompositedFrameBuilds;
            SpriteGeometryCacheHits = spriteGeometryCacheHits;
            SpriteGeometryCacheMisses = spriteGeometryCacheMisses;
            SpriteGeometryPrewarmQueued = spriteGeometryPrewarmQueued;
            SpriteGeometryPrewarmBuilt = spriteGeometryPrewarmBuilt;
            SpriteMaterialCacheHits = spriteMaterialCacheHits;
            SpriteMaterialCacheMisses = spriteMaterialCacheMisses;
            ActorOverlayUpdates = actorOverlayUpdates;
            ActorOverlayUpdateTicks = actorOverlayUpdateTicks;
            ActorOverlayApplyFrameCalls = actorOverlayApplyFrameCalls;
            ActorOverlayApplyFrameTicks = actorOverlayApplyFrameTicks;
            SpriteEffectUpdates = spriteEffectUpdates;
            SpriteEffectUpdateTicks = spriteEffectUpdateTicks;
            SpriteEffectLateUpdates = spriteEffectLateUpdates;
            SpriteEffectLateUpdateTicks = spriteEffectLateUpdateTicks;
            PrimitiveEffectUpdates = primitiveEffectUpdates;
            PrimitiveEffectUpdateTicks = primitiveEffectUpdateTicks;
            ActorSortingUpdates = actorSortingUpdates;
            ActorSortingUpdateTicks = actorSortingUpdateTicks;
            SpawnedObjects = spawnedObjects;
            DespawnedObjects = despawnedObjects;
            PathfindingRequests = pathfindingRequests;
            PointerResolveTargets = pointerResolveTargets;
            PointerSyncTransforms = pointerSyncTransforms;
            PointerResolveTicks = pointerResolveTicks;
            PointerWorldTargetsTicks = pointerWorldTargetsTicks;
            PointerColliderRefreshTicks = pointerColliderRefreshTicks;
            PointerColliderRefreshNowTicks = pointerColliderRefreshNowTicks;
            PointerRaycastTicks = pointerRaycastTicks;
            PointerGroundPointTicks = pointerGroundPointTicks;
            PointerResolverTicks = pointerResolverTicks;
            ClickToMoveUpdateTicks = clickToMoveUpdateTicks;
            ClickHandleTicks = clickHandleTicks;
            ClickMoveToTargetTicks = clickMoveToTargetTicks;
            UiPointerCaptureTicks = uiPointerCaptureTicks;
            CursorSetPointerTargetTicks = cursorSetPointerTargetTicks;
            CursorVisualLateUpdateTicks = cursorVisualLateUpdateTicks;
            CursorVisualApplyPointerTicks = cursorVisualApplyPointerTicks;
            PointerScreenReportGuiTicks = pointerScreenReportGuiTicks;
            ClickIntentResolveTicks = clickIntentResolveTicks;
            ActorServiceUpdateTicks = actorServiceUpdateTicks;
            ActorServiceLateUpdateTicks = actorServiceLateUpdateTicks;
            EffectManagerUpdateTicks = effectManagerUpdateTicks;
            MinimalLoginShellUpdateTicks = minimalLoginShellUpdateTicks;
            MinimalLoginShellStableLightPathTicks = minimalLoginShellStableLightPathTicks;
            MinimalLoginShellApplyLiveDrainTicks = minimalLoginShellApplyLiveDrainTicks;
            MinimalLoginShellApplyWorldDrainTicks = minimalLoginShellApplyWorldDrainTicks;
            MinimalLoginShellApplyLiveDrainPostSyncTicks = minimalLoginShellApplyLiveDrainPostSyncTicks;
            MinimalLoginShellEnsurePhase12Ticks = minimalLoginShellEnsurePhase12Ticks;
            MinimalLoginShellEnsureGameplayContextTicks = minimalLoginShellEnsureGameplayContextTicks;
            MinimalLoginShellRefreshGameplayWindowsTicks = minimalLoginShellRefreshGameplayWindowsTicks;
            MinimalLoginShellScheduleUiRefreshTicks = minimalLoginShellScheduleUiRefreshTicks;
            OnlineWorldUpdateTicks = onlineWorldUpdateTicks;
            CameraLateUpdateTicks = cameraLateUpdateTicks;
            GridSelectorUpdateTicks = gridSelectorUpdateTicks;
            RuntimeMapLateUpdateTicks = runtimeMapLateUpdateTicks;
            OnGuiCalls = onGuiCalls;
            OnGuiLayoutEvents = onGuiLayoutEvents;
            OnGuiRepaintEvents = onGuiRepaintEvents;
            OnGuiMouseMoveEvents = onGuiMouseMoveEvents;
            OnGuiTicks = onGuiTicks;
            MinimalLoginShellOnGuiTicks = minimalLoginShellOnGuiTicks;
            ActorHoverLabelOnGuiTicks = actorHoverLabelOnGuiTicks;
            ActorShowScriptOnGuiTicks = actorShowScriptOnGuiTicks;
            ActorLightingDebugOnGuiTicks = actorLightingDebugOnGuiTicks;
            ShortcutSettingsOnGuiTicks = shortcutSettingsOnGuiTicks;
            ClickToMoveOnGuiTicks = clickToMoveOnGuiTicks;
            CursorVisualOnGuiTicks = cursorVisualOnGuiTicks;
            GridSelectorOnGuiTicks = gridSelectorOnGuiTicks;
            ActiveSpritePointerShapeCount = activeSpritePointerShapeCount;
            ActiveSpritePointerColliderCount = activeSpritePointerColliderCount;
            SpritePointerBroadphaseChecks = spritePointerBroadphaseChecks;
            SpritePointerExactTests = spritePointerExactTests;
            SpritePointerAlphaTests = spritePointerAlphaTests;
            SpritePointerCacheHits = spritePointerCacheHits;
            SpritePointerCacheMisses = spritePointerCacheMisses;
            SpritePointerColliderRaycasts = spritePointerColliderRaycasts;
            SpritePointerColliderHits = spritePointerColliderHits;
            SpritePointerColliderRefreshes = spritePointerColliderRefreshes;
            SpritePointerRendererRecaches = spritePointerRendererRecaches;
            SpritePointerColliderMeshSwaps = spritePointerColliderMeshSwaps;
        }

        public long PacketsProcessed { get; }
        public long SnapshotsBuilt { get; }
        public long AssetLoads { get; }
        public long AssetLoadBytes { get; }
        public long ActorOverlaySpriteCacheHits { get; }
        public long ActorOverlaySpriteCacheMisses { get; }
        public long ActorOverlaySpriteCacheBuildFailures { get; }
        public long SpriteAnimatorApplyFrameCalls { get; }
        public long SpriteAnimatorApplyFrameTicks { get; }
        public long SpriteAnimatorMeshAssigns { get; }
        public long SpriteAnimatorMeshAssignTicks { get; }
        public long SpriteAnimatorTextureAssigns { get; }
        public long SpriteAnimatorTextureAssignTicks { get; }
        public long SpriteAnimatorMutableMeshUpdates { get; }
        public long SpriteAnimatorMutableMeshUpdateTicks { get; }
        public long SpriteFrameMeshBuilds { get; }
        public long SpriteCompositedFrameBuilds { get; }
        public long SpriteGeometryCacheHits { get; }
        public long SpriteGeometryCacheMisses { get; }
        public long SpriteGeometryPrewarmQueued { get; }
        public long SpriteGeometryPrewarmBuilt { get; }
        public long SpriteMaterialCacheHits { get; }
        public long SpriteMaterialCacheMisses { get; }
        public long ActorOverlayUpdates { get; }
        public long ActorOverlayUpdateTicks { get; }
        public long ActorOverlayApplyFrameCalls { get; }
        public long ActorOverlayApplyFrameTicks { get; }
        public long SpriteEffectUpdates { get; }
        public long SpriteEffectUpdateTicks { get; }
        public long SpriteEffectLateUpdates { get; }
        public long SpriteEffectLateUpdateTicks { get; }
        public long PrimitiveEffectUpdates { get; }
        public long PrimitiveEffectUpdateTicks { get; }
        public long ActorSortingUpdates { get; }
        public long ActorSortingUpdateTicks { get; }
        public long SpawnedObjects { get; }
        public long DespawnedObjects { get; }
        public long PathfindingRequests { get; }
        public long PointerResolveTargets { get; }
        public long PointerSyncTransforms { get; }
        public long PointerResolveTicks { get; }
        public long PointerWorldTargetsTicks { get; }
        public long PointerColliderRefreshTicks { get; }
        public long PointerColliderRefreshNowTicks { get; }
        public long PointerRaycastTicks { get; }
        public long PointerGroundPointTicks { get; }
        public long PointerResolverTicks { get; }
        public long ClickToMoveUpdateTicks { get; }
        public long ClickHandleTicks { get; }
        public long ClickMoveToTargetTicks { get; }
        public long UiPointerCaptureTicks { get; }
        public long CursorSetPointerTargetTicks { get; }
        public long CursorVisualLateUpdateTicks { get; }
        public long CursorVisualApplyPointerTicks { get; }
        public long PointerScreenReportGuiTicks { get; }
        public long ClickIntentResolveTicks { get; }
        public long ActorServiceUpdateTicks { get; }
        public long ActorServiceLateUpdateTicks { get; }
        public long EffectManagerUpdateTicks { get; }
        public long MinimalLoginShellUpdateTicks { get; }
        public long MinimalLoginShellStableLightPathTicks { get; }
        public long MinimalLoginShellApplyLiveDrainTicks { get; }
        public long MinimalLoginShellApplyWorldDrainTicks { get; }
        public long MinimalLoginShellApplyLiveDrainPostSyncTicks { get; }
        public long MinimalLoginShellEnsurePhase12Ticks { get; }
        public long MinimalLoginShellEnsureGameplayContextTicks { get; }
        public long MinimalLoginShellRefreshGameplayWindowsTicks { get; }
        public long MinimalLoginShellScheduleUiRefreshTicks { get; }
        public long OnlineWorldUpdateTicks { get; }
        public long CameraLateUpdateTicks { get; }
        public long GridSelectorUpdateTicks { get; }
        public long RuntimeMapLateUpdateTicks { get; }
        public long OnGuiCalls { get; }
        public long OnGuiLayoutEvents { get; }
        public long OnGuiRepaintEvents { get; }
        public long OnGuiMouseMoveEvents { get; }
        public long OnGuiTicks { get; }
        public long MinimalLoginShellOnGuiTicks { get; }
        public long ActorHoverLabelOnGuiTicks { get; }
        public long ActorShowScriptOnGuiTicks { get; }
        public long ActorLightingDebugOnGuiTicks { get; }
        public long ShortcutSettingsOnGuiTicks { get; }
        public long ClickToMoveOnGuiTicks { get; }
        public long CursorVisualOnGuiTicks { get; }
        public long GridSelectorOnGuiTicks { get; }
        public int ActiveSpritePointerShapeCount { get; }
        public int ActiveSpritePointerColliderCount { get; }
        public long SpritePointerBroadphaseChecks { get; }
        public long SpritePointerExactTests { get; }
        public long SpritePointerAlphaTests { get; }
        public long SpritePointerCacheHits { get; }
        public long SpritePointerCacheMisses { get; }
        public long SpritePointerColliderRaycasts { get; }
        public long SpritePointerColliderHits { get; }
        public long SpritePointerColliderRefreshes { get; }
        public long SpritePointerRendererRecaches { get; }
        public long SpritePointerColliderMeshSwaps { get; }
    }

    public readonly struct RoAssetLoadEvent {
        public RoAssetLoadEvent(int frameIndex, string path, int byteCount) {
            FrameIndex = frameIndex;
            Path = path ?? string.Empty;
            ByteCount = byteCount;
        }

        public int FrameIndex { get; }
        public string Path { get; }
        public int ByteCount { get; }
    }

    public readonly struct RoRuntimePerfCounterSnapshot {
        internal RoRuntimePerfCounterSnapshot(
            long sessionSnapshots,
            long actorUpdateTicks,
            long actorLateTicks,
            long animatorTicks,
            long forcedPresenterSyncs,
            long forcedActorMotions,
            long actorPresenterSyncs,
            long actorPresenterActorsProcessed,
            long actorPresenterActorsChanged,
            long schedulerTicks,
            long schedulerRefreshes,
            long schedulerSkipped,
            long canvasWillRenderEvents,
            int activeCanvasCount,
            int activeGraphicRaycasterCount,
            int activeSpritePointerShapeCount,
            int activeAnimatorUpdateTickCount,
            int actorUpdateRegisteredCount,
            int actorUpdateVisibleCount,
            int actorUpdateOffscreenCount,
            int actorUpdateInactiveCount,
            int actorUpdateUnknownCount,
            int actorLateRegisteredCount,
            int actorLateVisibleCount,
            int actorLateOffscreenCount,
            int actorLateInactiveCount,
            int actorLateUnknownCount,
            long spritePointerBroadphaseChecks,
            long spritePointerExactTests,
            long spritePointerAlphaTests,
            long spritePointerCacheHits,
            long spritePointerCacheMisses,
            long spritePointerColliderRaycasts,
            long spritePointerColliderHits,
            int activeSpritePointerColliderCount,
            long spritePointerColliderRefreshes,
            long spritePointerRendererRecaches,
            long spritePointerColliderMeshSwaps,
            long spriteFrameMeshBuilds,
            long spriteCompositedFrameBuilds,
            long spriteGeometryCacheHits,
            long spriteGeometryCacheMisses,
            long spriteGeometryPrewarmQueued,
            long spriteGeometryPrewarmBuilt,
            long spriteMaterialCacheHits,
            long spriteMaterialCacheMisses,
            long effectCameraCacheHits,
            long effectCameraCacheMisses,
            long spriteBatchInstances,
            long spriteBatchBatches,
            long spriteBatchFallbacks,
            long effectPoolCreated,
            long effectPoolReused,
            long effectPoolDropped,
            long packetTraceWrites,
            long packetTraceSkipped,
            long packetLogRefreshes,
            long packetLogSnapshotEntries,
            long drainToResultCalls,
            long drainToResultArrayCount,
            long drainToResultElementCount,
            long drainToResultEstimatedMinBytes,
            long snapshotRecentCalls,
            long snapshotRecentArrayCount,
            long snapshotRecentElementCount,
            long snapshotRecentEstimatedMinBytes,
            long combatTraceRecords,
            long debugLogLines,
            int dayNightMaterialCount,
            int dayNightLightCount,
            long dayNightMaterialApplies,
            long dayNightLightApplies,
            int staticChunksVisible,
            int staticChunksTotal,
            Dictionary<string, long> uiRefreshCounts,
            Dictionary<string, long> uiRefreshReasonCounts,
            Dictionary<string, long> snapshotCounts,
            Dictionary<string, long> textAssignmentCounts,
            Dictionary<string, long> actorLateTickCounts
        ) {
            SessionSnapshots = sessionSnapshots;
            ActorUpdateTicks = actorUpdateTicks;
            ActorLateTicks = actorLateTicks;
            AnimatorTicks = animatorTicks;
            ForcedPresenterSyncs = forcedPresenterSyncs;
            ForcedActorMotions = forcedActorMotions;
            ActorPresenterSyncs = actorPresenterSyncs;
            ActorPresenterActorsProcessed = actorPresenterActorsProcessed;
            ActorPresenterActorsChanged = actorPresenterActorsChanged;
            SchedulerTicks = schedulerTicks;
            SchedulerRefreshes = schedulerRefreshes;
            SchedulerSkipped = schedulerSkipped;
            CanvasWillRenderEvents = canvasWillRenderEvents;
            ActiveCanvasCount = activeCanvasCount;
            ActiveGraphicRaycasterCount = activeGraphicRaycasterCount;
            ActiveSpritePointerShapeCount = activeSpritePointerShapeCount;
            ActiveAnimatorUpdateTickCount = activeAnimatorUpdateTickCount;
            ActorUpdateRegisteredCount = actorUpdateRegisteredCount;
            ActorUpdateVisibleCount = actorUpdateVisibleCount;
            ActorUpdateOffscreenCount = actorUpdateOffscreenCount;
            ActorUpdateInactiveCount = actorUpdateInactiveCount;
            ActorUpdateUnknownCount = actorUpdateUnknownCount;
            ActorLateRegisteredCount = actorLateRegisteredCount;
            ActorLateVisibleCount = actorLateVisibleCount;
            ActorLateOffscreenCount = actorLateOffscreenCount;
            ActorLateInactiveCount = actorLateInactiveCount;
            ActorLateUnknownCount = actorLateUnknownCount;
            SpritePointerBroadphaseChecks = spritePointerBroadphaseChecks;
            SpritePointerExactTests = spritePointerExactTests;
            SpritePointerAlphaTests = spritePointerAlphaTests;
            SpritePointerCacheHits = spritePointerCacheHits;
            SpritePointerCacheMisses = spritePointerCacheMisses;
            SpritePointerColliderRaycasts = spritePointerColliderRaycasts;
            SpritePointerColliderHits = spritePointerColliderHits;
            ActiveSpritePointerColliderCount = activeSpritePointerColliderCount;
            SpritePointerColliderRefreshes = spritePointerColliderRefreshes;
            SpritePointerRendererRecaches = spritePointerRendererRecaches;
            SpritePointerColliderMeshSwaps = spritePointerColliderMeshSwaps;
            SpriteFrameMeshBuilds = spriteFrameMeshBuilds;
            SpriteCompositedFrameBuilds = spriteCompositedFrameBuilds;
            SpriteGeometryCacheHits = spriteGeometryCacheHits;
            SpriteGeometryCacheMisses = spriteGeometryCacheMisses;
            SpriteGeometryPrewarmQueued = spriteGeometryPrewarmQueued;
            SpriteGeometryPrewarmBuilt = spriteGeometryPrewarmBuilt;
            SpriteMaterialCacheHits = spriteMaterialCacheHits;
            SpriteMaterialCacheMisses = spriteMaterialCacheMisses;
            EffectCameraCacheHits = effectCameraCacheHits;
            EffectCameraCacheMisses = effectCameraCacheMisses;
            SpriteBatchInstances = spriteBatchInstances;
            SpriteBatchBatches = spriteBatchBatches;
            SpriteBatchFallbacks = spriteBatchFallbacks;
            EffectPoolCreated = effectPoolCreated;
            EffectPoolReused = effectPoolReused;
            EffectPoolDropped = effectPoolDropped;
            PacketTraceWrites = packetTraceWrites;
            PacketTraceSkipped = packetTraceSkipped;
            PacketLogRefreshes = packetLogRefreshes;
            PacketLogSnapshotEntries = packetLogSnapshotEntries;
            DrainToResultCalls = drainToResultCalls;
            DrainToResultArrayCount = drainToResultArrayCount;
            DrainToResultElementCount = drainToResultElementCount;
            DrainToResultEstimatedMinBytes = drainToResultEstimatedMinBytes;
            SnapshotRecentCalls = snapshotRecentCalls;
            SnapshotRecentArrayCount = snapshotRecentArrayCount;
            SnapshotRecentElementCount = snapshotRecentElementCount;
            SnapshotRecentEstimatedMinBytes = snapshotRecentEstimatedMinBytes;
            CombatTraceRecords = combatTraceRecords;
            DebugLogLines = debugLogLines;
            DayNightMaterialCount = dayNightMaterialCount;
            DayNightLightCount = dayNightLightCount;
            DayNightMaterialApplies = dayNightMaterialApplies;
            DayNightLightApplies = dayNightLightApplies;
            StaticChunksVisible = staticChunksVisible;
            StaticChunksTotal = staticChunksTotal;
            UiRefreshCounts = uiRefreshCounts;
            UiRefreshReasonCounts = uiRefreshReasonCounts;
            SnapshotCounts = snapshotCounts;
            TextAssignmentCounts = textAssignmentCounts;
            ActorLateTickCounts = actorLateTickCounts;
        }

        public long SessionSnapshots { get; }
        public long ActorUpdateTicks { get; }
        public long ActorLateTicks { get; }
        public long AnimatorTicks { get; }
        public long ForcedPresenterSyncs { get; }
        public long ForcedActorMotions { get; }
        public long ActorPresenterSyncs { get; }
        public long ActorPresenterActorsProcessed { get; }
        public long ActorPresenterActorsChanged { get; }
        public long SchedulerTicks { get; }
        public long SchedulerRefreshes { get; }
        public long SchedulerSkipped { get; }
        public long CanvasWillRenderEvents { get; }
        public int ActiveCanvasCount { get; }
        public int ActiveGraphicRaycasterCount { get; }
        public int ActiveSpritePointerShapeCount { get; }
        public int ActiveAnimatorUpdateTickCount { get; }
        public int ActorUpdateRegisteredCount { get; }
        public int ActorUpdateVisibleCount { get; }
        public int ActorUpdateOffscreenCount { get; }
        public int ActorUpdateInactiveCount { get; }
        public int ActorUpdateUnknownCount { get; }
        public int ActorLateRegisteredCount { get; }
        public int ActorLateVisibleCount { get; }
        public int ActorLateOffscreenCount { get; }
        public int ActorLateInactiveCount { get; }
        public int ActorLateUnknownCount { get; }
        public long SpritePointerBroadphaseChecks { get; }
        public long SpritePointerExactTests { get; }
        public long SpritePointerAlphaTests { get; }
        public long SpritePointerCacheHits { get; }
        public long SpritePointerCacheMisses { get; }
        public long SpritePointerColliderRaycasts { get; }
        public long SpritePointerColliderHits { get; }
        public int ActiveSpritePointerColliderCount { get; }
        public long SpritePointerColliderRefreshes { get; }
        public long SpritePointerRendererRecaches { get; }
        public long SpritePointerColliderMeshSwaps { get; }
        public long SpriteFrameMeshBuilds { get; }
        public long SpriteCompositedFrameBuilds { get; }
        public long SpriteGeometryCacheHits { get; }
        public long SpriteGeometryCacheMisses { get; }
        public long SpriteGeometryPrewarmQueued { get; }
        public long SpriteGeometryPrewarmBuilt { get; }
        public long SpriteMaterialCacheHits { get; }
        public long SpriteMaterialCacheMisses { get; }
        public long EffectCameraCacheHits { get; }
        public long EffectCameraCacheMisses { get; }
        public long SpriteBatchInstances { get; }
        public long SpriteBatchBatches { get; }
        public long SpriteBatchFallbacks { get; }
        public long EffectPoolCreated { get; }
        public long EffectPoolReused { get; }
        public long EffectPoolDropped { get; }
        public long PacketTraceWrites { get; }
        public long PacketTraceSkipped { get; }
        public long PacketLogRefreshes { get; }
        public long PacketLogSnapshotEntries { get; }
        public long DrainToResultCalls { get; }
        public long DrainToResultArrayCount { get; }
        public long DrainToResultElementCount { get; }
        public long DrainToResultEstimatedMinBytes { get; }
        public long SnapshotRecentCalls { get; }
        public long SnapshotRecentArrayCount { get; }
        public long SnapshotRecentElementCount { get; }
        public long SnapshotRecentEstimatedMinBytes { get; }
        public long CombatTraceRecords { get; }
        public long DebugLogLines { get; }
        public int DayNightMaterialCount { get; }
        public int DayNightLightCount { get; }
        public long DayNightMaterialApplies { get; }
        public long DayNightLightApplies { get; }
        public int StaticChunksVisible { get; }
        public int StaticChunksTotal { get; }
        public IReadOnlyDictionary<string, long> UiRefreshCounts { get; }
        public IReadOnlyDictionary<string, long> UiRefreshReasonCounts { get; }
        public IReadOnlyDictionary<string, long> SnapshotCounts { get; }
        public IReadOnlyDictionary<string, long> TextAssignmentCounts { get; }
        public IReadOnlyDictionary<string, long> ActorLateTickCounts { get; }
    }

    public static class RoRuntimePerfCounters {
        private const int MaxCaptureAssetLoadEvents = 4096;
        private static readonly object Sync = new object();
        private static readonly RoAssetLoadEvent[] AssetLoadEvents = new RoAssetLoadEvent[MaxCaptureAssetLoadEvents];
        private static readonly Dictionary<string, long> UiRefreshCounts = new Dictionary<string, long>();
        private static readonly Dictionary<string, long> UiRefreshReasonCounts = new Dictionary<string, long>();
        private static readonly Dictionary<string, long> SnapshotCounts = new Dictionary<string, long>();
        private static readonly Dictionary<string, long> TextAssignmentCounts = new Dictionary<string, long>();
        private static readonly Dictionary<string, long> ActorLateTickCounts = new Dictionary<string, long>();

        private static long sessionSnapshots;
        private static long actorUpdateTicks;
        private static long actorLateTicks;
        private static long animatorTicks;
        private static long forcedPresenterSyncs;
        private static long forcedActorMotions;
        private static long actorPresenterSyncs;
        private static long actorPresenterActorsProcessed;
        private static long actorPresenterActorsChanged;
        private static long schedulerTicks;
        private static long schedulerRefreshes;
        private static long schedulerSkipped;
        private static long canvasWillRenderEvents;
        private static int activeCanvasCount;
        private static int activeGraphicRaycasterCount;
        private static int activeSpritePointerShapeCount;
        private static int activeAnimatorUpdateTickCount;
        private static int actorUpdateRegisteredCount;
        private static int actorUpdateVisibleCount;
        private static int actorUpdateOffscreenCount;
        private static int actorUpdateInactiveCount;
        private static int actorUpdateUnknownCount;
        private static int actorLateRegisteredCount;
        private static int actorLateVisibleCount;
        private static int actorLateOffscreenCount;
        private static int actorLateInactiveCount;
        private static int actorLateUnknownCount;
        private static long spritePointerBroadphaseChecks;
        private static long spritePointerExactTests;
        private static long spritePointerAlphaTests;
        private static long spritePointerCacheHits;
        private static long spritePointerCacheMisses;
        private static long spritePointerColliderRaycasts;
        private static long spritePointerColliderHits;
        private static int activeSpritePointerColliderCount;
        private static long spritePointerColliderRefreshes;
        private static long spritePointerRendererRecaches;
        private static long spritePointerColliderMeshSwaps;
        private static long spriteFrameMeshBuilds;
        private static long spriteCompositedFrameBuilds;
        private static long spriteGeometryCacheHits;
        private static long spriteGeometryCacheMisses;
        private static long spriteGeometryPrewarmQueued;
        private static long spriteGeometryPrewarmBuilt;
        private static long spriteMaterialCacheHits;
        private static long spriteMaterialCacheMisses;
        private static long effectCameraCacheHits;
        private static long effectCameraCacheMisses;
        private static long spriteBatchInstances;
        private static long spriteBatchBatches;
        private static long spriteBatchFallbacks;
        private static long effectPoolCreated;
        private static long effectPoolReused;
        private static long effectPoolDropped;
        private static long packetTraceWrites;
        private static long packetTraceSkipped;
        private static long packetLogRefreshes;
        private static long packetLogSnapshotEntries;
        private static long drainToResultCalls;
        private static long drainToResultArrayCount;
        private static long drainToResultElementCount;
        private static long drainToResultEstimatedMinBytes;
        private static long snapshotRecentCalls;
        private static long snapshotRecentArrayCount;
        private static long snapshotRecentElementCount;
        private static long snapshotRecentEstimatedMinBytes;
        private static long combatTraceRecords;
        private static long debugLogLines;
        private static int dayNightMaterialCount;
        private static int dayNightLightCount;
        private static long dayNightMaterialApplies;
        private static long dayNightLightApplies;
        private static int staticChunksVisible;
        private static int staticChunksTotal;
        private static long packetsProcessed;
        private static long snapshotsBuilt;
        private static long assetLoads;
        private static long assetLoadBytes;
        private static long actorOverlaySpriteCacheHits;
        private static long actorOverlaySpriteCacheMisses;
        private static long actorOverlaySpriteCacheBuildFailures;
        private static long spriteAnimatorApplyFrameCalls;
        private static long spriteAnimatorApplyFrameTicks;
        private static long spriteAnimatorMeshAssigns;
        private static long spriteAnimatorMeshAssignTicks;
        private static long spriteAnimatorTextureAssigns;
        private static long spriteAnimatorTextureAssignTicks;
        private static long spriteAnimatorMutableMeshUpdates;
        private static long spriteAnimatorMutableMeshUpdateTicks;
        private static long actorOverlayUpdates;
        private static long actorOverlayUpdateTicks;
        private static long actorOverlayApplyFrameCalls;
        private static long actorOverlayApplyFrameTicks;
        private static long spriteEffectUpdates;
        private static long spriteEffectUpdateTicks;
        private static long spriteEffectLateUpdates;
        private static long spriteEffectLateUpdateTicks;
        private static long primitiveEffectUpdates;
        private static long primitiveEffectUpdateTicks;
        private static long actorSortingUpdates;
        private static long actorSortingUpdateTicks;
        private static int assetLoadEventCount;
        private static int assetLoadEventOverflow;
        private static int captureFrameIndex = -1;
        private static long spawnedObjects;
        private static long despawnedObjects;
        private static long pathfindingRequests;
        private static long pointerResolveTargets;
        private static long pointerSyncTransforms;
        private static long pointerResolveTicks;
        private static long pointerWorldTargetsTicks;
        private static long pointerColliderRefreshTicks;
        private static long pointerColliderRefreshNowTicks;
        private static long pointerRaycastTicks;
        private static long pointerGroundPointTicks;
        private static long pointerResolverTicks;
        private static long clickToMoveUpdateTicks;
        private static long clickHandleTicks;
        private static long clickMoveToTargetTicks;
        private static long uiPointerCaptureTicks;
        private static long cursorSetPointerTargetTicks;
        private static long cursorVisualLateUpdateTicks;
        private static long cursorVisualApplyPointerTicks;
        private static long pointerScreenReportGuiTicks;
        private static long clickIntentResolveTicks;
        private static long actorServiceUpdateTicks;
        private static long actorServiceLateUpdateTicks;
        private static long effectManagerUpdateTicks;
        private static long minimalLoginShellUpdateTicks;
        private static long minimalLoginShellStableLightPathTicks;
        private static long minimalLoginShellApplyLiveDrainTicks;
        private static long minimalLoginShellApplyWorldDrainTicks;
        private static long minimalLoginShellApplyLiveDrainPostSyncTicks;
        private static long minimalLoginShellEnsurePhase12Ticks;
        private static long minimalLoginShellEnsureGameplayContextTicks;
        private static long minimalLoginShellRefreshGameplayWindowsTicks;
        private static long minimalLoginShellScheduleUiRefreshTicks;
        private static long onlineWorldUpdateTicks;
        private static long cameraLateUpdateTicks;
        private static long gridSelectorUpdateTicks;
        private static long runtimeMapLateUpdateTicks;
        private static long onGuiCalls;
        private static long onGuiLayoutEvents;
        private static long onGuiRepaintEvents;
        private static long onGuiMouseMoveEvents;
        private static long onGuiTicks;
        private static long minimalLoginShellOnGuiTicks;
        private static long actorHoverLabelOnGuiTicks;
        private static long actorShowScriptOnGuiTicks;
        private static long actorLightingDebugOnGuiTicks;
        private static long shortcutSettingsOnGuiTicks;
        private static long clickToMoveOnGuiTicks;
        private static long cursorVisualOnGuiTicks;
        private static long gridSelectorOnGuiTicks;
        private static volatile bool performanceCaptureTimingEnabled;

        public static bool DiagnosticsEnabled { get; set; }
        public static bool PerformanceCaptureTimingEnabled {
            get => performanceCaptureTimingEnabled;
            set => performanceCaptureTimingEnabled = value;
        }

        public static long GetCaptureTimingStartTicks() {
            return performanceCaptureTimingEnabled
                ? System.Diagnostics.Stopwatch.GetTimestamp()
                : 0L;
        }

        public static long GetCaptureTimingElapsedTicks(long startTicks) {
            return startTicks > 0L
                ? System.Diagnostics.Stopwatch.GetTimestamp() - startTicks
                : 0L;
        }

        public static void IncrementUiRefresh(string windowName) {
            IncrementNamed(UiRefreshCounts, windowName);
        }

        public static void IncrementUiRefreshReason(string windowName, string reason) {
            IncrementNamed(UiRefreshReasonCounts, (windowName ?? "unknown") + ":" + (reason ?? "unknown"));
        }

        public static void IncrementSnapshot(string kind) {
            Interlocked.Increment(ref sessionSnapshots);
            IncrementSnapshotsBuilt();
            IncrementNamed(SnapshotCounts, kind);
        }

        public static void IncrementPacketsProcessed() {
            Interlocked.Increment(ref packetsProcessed);
        }

        public static void AddPacketsProcessed(int count) {
            if (count > 0) {
                Interlocked.Add(ref packetsProcessed, count);
            }
        }

        public static void IncrementSnapshotsBuilt() {
            Interlocked.Increment(ref snapshotsBuilt);
        }

        public static void IncrementAssetLoads() {
            Interlocked.Increment(ref assetLoads);
        }

        public static void SetCaptureFrameIndex(int frameIndex) {
            Interlocked.Exchange(ref captureFrameIndex, frameIndex);
        }

        public static void RecordAssetLoad(string path, int byteCount) {
            Interlocked.Increment(ref assetLoads);
            if (byteCount > 0) {
                Interlocked.Add(ref assetLoadBytes, byteCount);
            }

            if (!performanceCaptureTimingEnabled) {
                return;
            }

            int eventIndex = Interlocked.Increment(ref assetLoadEventCount) - 1;
            if ((uint)eventIndex < MaxCaptureAssetLoadEvents) {
                int frameIndex = Interlocked.CompareExchange(ref captureFrameIndex, 0, 0);
                AssetLoadEvents[eventIndex] = new RoAssetLoadEvent(frameIndex, path, byteCount);
                return;
            }

            Interlocked.Increment(ref assetLoadEventOverflow);
        }

        public static void IncrementActorOverlaySpriteCacheHit() {
            Interlocked.Increment(ref actorOverlaySpriteCacheHits);
        }

        public static void IncrementActorOverlaySpriteCacheMiss() {
            Interlocked.Increment(ref actorOverlaySpriteCacheMisses);
        }

        public static void IncrementActorOverlaySpriteCacheBuildFailure() {
            Interlocked.Increment(ref actorOverlaySpriteCacheBuildFailures);
        }

        public static void AddSpriteAnimatorApplyFrameTicks(long ticks) {
            AddCallTicks(ref spriteAnimatorApplyFrameCalls, ref spriteAnimatorApplyFrameTicks, ticks);
        }

        public static void AddSpriteAnimatorMeshAssignTicks(long ticks) {
            AddCallTicks(ref spriteAnimatorMeshAssigns, ref spriteAnimatorMeshAssignTicks, ticks);
        }

        public static void AddSpriteAnimatorTextureAssignTicks(long ticks) {
            AddCallTicks(ref spriteAnimatorTextureAssigns, ref spriteAnimatorTextureAssignTicks, ticks);
        }

        public static void AddSpriteAnimatorMutableMeshUpdateTicks(long ticks) {
            AddCallTicks(ref spriteAnimatorMutableMeshUpdates, ref spriteAnimatorMutableMeshUpdateTicks, ticks);
        }

        public static void AddActorOverlayUpdateTicks(long ticks) {
            AddCallTicks(ref actorOverlayUpdates, ref actorOverlayUpdateTicks, ticks);
        }

        public static void AddActorOverlayApplyFrameTicks(long ticks) {
            AddCallTicks(ref actorOverlayApplyFrameCalls, ref actorOverlayApplyFrameTicks, ticks);
        }

        public static void AddSpriteEffectUpdateTicks(long ticks) {
            AddCallTicks(ref spriteEffectUpdates, ref spriteEffectUpdateTicks, ticks);
        }

        public static void AddSpriteEffectLateUpdateTicks(long ticks) {
            AddCallTicks(ref spriteEffectLateUpdates, ref spriteEffectLateUpdateTicks, ticks);
        }

        public static void AddPrimitiveEffectUpdateTicks(long ticks) {
            AddCallTicks(ref primitiveEffectUpdates, ref primitiveEffectUpdateTicks, ticks);
        }

        public static void AddActorSortingUpdateTicks(long ticks) {
            AddCallTicks(ref actorSortingUpdates, ref actorSortingUpdateTicks, ticks);
        }

        public static void ClearCaptureAssetLoadEvents() {
            Interlocked.Exchange(ref assetLoadEventCount, 0);
            Interlocked.Exchange(ref assetLoadEventOverflow, 0);
            Interlocked.Exchange(ref captureFrameIndex, -1);
        }

        public static RoAssetLoadEvent[] AssetLoadEventsSnapshot(out int overflowCount) {
            overflowCount = Interlocked.CompareExchange(ref assetLoadEventOverflow, 0, 0);
            int count = Interlocked.CompareExchange(ref assetLoadEventCount, 0, 0);
            if (count < 0) {
                count = 0;
            }

            if (count > MaxCaptureAssetLoadEvents) {
                count = MaxCaptureAssetLoadEvents;
            }

            var snapshot = new RoAssetLoadEvent[count];
            for (int i = 0; i < count; i++) {
                snapshot[i] = AssetLoadEvents[i];
            }

            return snapshot;
        }

        public static void IncrementSpawnedObjects() {
            Interlocked.Increment(ref spawnedObjects);
        }

        public static void AddSpawnedObjects(int count) {
            if (count > 0) {
                Interlocked.Add(ref spawnedObjects, count);
            }
        }

        public static void IncrementDespawnedObjects() {
            Interlocked.Increment(ref despawnedObjects);
        }

        public static void AddDespawnedObjects(int count) {
            if (count > 0) {
                Interlocked.Add(ref despawnedObjects, count);
            }
        }

        public static void IncrementPathRequests() {
            Interlocked.Increment(ref pathfindingRequests);
        }

        public static void IncrementPointerResolveTarget() {
            Interlocked.Increment(ref pointerResolveTargets);
        }

        public static void IncrementPointerSyncTransforms() {
            Interlocked.Increment(ref pointerSyncTransforms);
        }

        public static void AddPointerResolveTicks(long ticks) {
            AddPositiveTicks(ref pointerResolveTicks, ticks);
        }

        public static void AddPointerWorldTargetsTicks(long ticks) {
            AddPositiveTicks(ref pointerWorldTargetsTicks, ticks);
        }

        public static void AddPointerColliderRefreshTicks(long ticks) {
            AddPositiveTicks(ref pointerColliderRefreshTicks, ticks);
        }

        public static void AddPointerColliderRefreshNowTicks(long ticks) {
            AddPositiveTicks(ref pointerColliderRefreshNowTicks, ticks);
        }

        public static void AddPointerRaycastTicks(long ticks) {
            AddPositiveTicks(ref pointerRaycastTicks, ticks);
        }

        public static void AddPointerGroundPointTicks(long ticks) {
            AddPositiveTicks(ref pointerGroundPointTicks, ticks);
        }

        public static void AddPointerResolverTicks(long ticks) {
            AddPositiveTicks(ref pointerResolverTicks, ticks);
        }

        public static void AddClickToMoveUpdateTicks(long ticks) {
            AddPositiveTicks(ref clickToMoveUpdateTicks, ticks);
        }

        public static void AddClickHandleTicks(long ticks) {
            AddPositiveTicks(ref clickHandleTicks, ticks);
        }

        public static void AddClickMoveToTargetTicks(long ticks) {
            AddPositiveTicks(ref clickMoveToTargetTicks, ticks);
        }

        public static void AddUiPointerCaptureTicks(long ticks) {
            AddPositiveTicks(ref uiPointerCaptureTicks, ticks);
        }

        public static void AddCursorSetPointerTargetTicks(long ticks) {
            AddPositiveTicks(ref cursorSetPointerTargetTicks, ticks);
        }

        public static void AddCursorVisualLateUpdateTicks(long ticks) {
            AddPositiveTicks(ref cursorVisualLateUpdateTicks, ticks);
        }

        public static void AddCursorVisualApplyPointerTicks(long ticks) {
            AddPositiveTicks(ref cursorVisualApplyPointerTicks, ticks);
        }

        public static void AddPointerScreenReportGuiTicks(long ticks) {
            AddPositiveTicks(ref pointerScreenReportGuiTicks, ticks);
        }

        public static void AddClickIntentResolveTicks(long ticks) {
            AddPositiveTicks(ref clickIntentResolveTicks, ticks);
        }

        public static void AddActorServiceUpdateTicks(long ticks) {
            AddPositiveTicks(ref actorServiceUpdateTicks, ticks);
        }

        public static void AddActorServiceLateUpdateTicks(long ticks) {
            AddPositiveTicks(ref actorServiceLateUpdateTicks, ticks);
        }

        public static void AddEffectManagerUpdateTicks(long ticks) {
            AddPositiveTicks(ref effectManagerUpdateTicks, ticks);
        }

        public static void AddMinimalLoginShellUpdateTicks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellUpdateTicks, ticks);
        }

        public static void AddMinimalLoginShellStableLightPathTicks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellStableLightPathTicks, ticks);
        }

        public static void AddMinimalLoginShellApplyLiveDrainTicks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellApplyLiveDrainTicks, ticks);
        }

        public static void AddMinimalLoginShellApplyWorldDrainTicks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellApplyWorldDrainTicks, ticks);
        }

        public static void AddMinimalLoginShellApplyLiveDrainPostSyncTicks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellApplyLiveDrainPostSyncTicks, ticks);
        }

        public static void AddMinimalLoginShellEnsurePhase12Ticks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellEnsurePhase12Ticks, ticks);
        }

        public static void AddMinimalLoginShellEnsureGameplayContextTicks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellEnsureGameplayContextTicks, ticks);
        }

        public static void AddMinimalLoginShellRefreshGameplayWindowsTicks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellRefreshGameplayWindowsTicks, ticks);
        }

        public static void AddMinimalLoginShellScheduleUiRefreshTicks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellScheduleUiRefreshTicks, ticks);
        }

        public static void AddOnlineWorldUpdateTicks(long ticks) {
            AddPositiveTicks(ref onlineWorldUpdateTicks, ticks);
        }

        public static void AddCameraLateUpdateTicks(long ticks) {
            AddPositiveTicks(ref cameraLateUpdateTicks, ticks);
        }

        public static void AddGridSelectorUpdateTicks(long ticks) {
            AddPositiveTicks(ref gridSelectorUpdateTicks, ticks);
        }

        public static void AddRuntimeMapLateUpdateTicks(long ticks) {
            AddPositiveTicks(ref runtimeMapLateUpdateTicks, ticks);
        }

        public static void IncrementOnGuiEvent(int eventType) {
            if (!performanceCaptureTimingEnabled) {
                return;
            }

            Interlocked.Increment(ref onGuiCalls);
            switch (eventType) {
                case 7:
                    Interlocked.Increment(ref onGuiRepaintEvents);
                    break;
                case 8:
                    Interlocked.Increment(ref onGuiLayoutEvents);
                    break;
                case 2:
                    Interlocked.Increment(ref onGuiMouseMoveEvents);
                    break;
            }
        }

        public static void AddOnGuiTicks(long ticks) {
            AddPositiveTicks(ref onGuiTicks, ticks);
        }

        public static void AddMinimalLoginShellOnGuiTicks(long ticks) {
            AddPositiveTicks(ref minimalLoginShellOnGuiTicks, ticks);
        }

        public static void AddActorHoverLabelOnGuiTicks(long ticks) {
            AddPositiveTicks(ref actorHoverLabelOnGuiTicks, ticks);
        }

        public static void AddActorShowScriptOnGuiTicks(long ticks) {
            AddPositiveTicks(ref actorShowScriptOnGuiTicks, ticks);
        }

        public static void AddActorLightingDebugOnGuiTicks(long ticks) {
            AddPositiveTicks(ref actorLightingDebugOnGuiTicks, ticks);
        }

        public static void AddShortcutSettingsOnGuiTicks(long ticks) {
            AddPositiveTicks(ref shortcutSettingsOnGuiTicks, ticks);
        }

        public static void AddClickToMoveOnGuiTicks(long ticks) {
            AddPositiveTicks(ref clickToMoveOnGuiTicks, ticks);
        }

        public static void AddCursorVisualOnGuiTicks(long ticks) {
            AddPositiveTicks(ref cursorVisualOnGuiTicks, ticks);
        }

        public static void AddGridSelectorOnGuiTicks(long ticks) {
            AddPositiveTicks(ref gridSelectorOnGuiTicks, ticks);
        }

        public static void IncrementTextAssignment(string kind) {
            IncrementNamed(TextAssignmentCounts, kind);
        }

        public static void AddActorUpdateTicks(int count) {
            if (count > 0) {
                Interlocked.Add(ref actorUpdateTicks, count);
            }
        }

        public static void AddActorLateTicks(int count) {
            if (count > 0) {
                Interlocked.Add(ref actorLateTicks, count);
            }
        }

        public static void AddActorLateTickTypeCount(string kind, int count) {
            if (count <= 0) {
                return;
            }

            IncrementNamed(ActorLateTickCounts, kind, count);
        }

        public static void AddAnimatorTicks(int count) {
            if (count > 0) {
                Interlocked.Add(ref animatorTicks, count);
            }
        }

        public static void IncrementForcedPresenterSync() {
            Interlocked.Increment(ref forcedPresenterSyncs);
        }

        public static void IncrementForcedActorMotion() {
            Interlocked.Increment(ref forcedActorMotions);
        }

        public static void AddActorPresenterSync(int actorsProcessed, int actorsChanged) {
            Interlocked.Increment(ref actorPresenterSyncs);
            if (actorsProcessed > 0) {
                Interlocked.Add(ref actorPresenterActorsProcessed, actorsProcessed);
            }

            if (actorsChanged > 0) {
                Interlocked.Add(ref actorPresenterActorsChanged, actorsChanged);
            }
        }

        public static void IncrementSchedulerTick() {
            Interlocked.Increment(ref schedulerTicks);
        }

        public static void IncrementSchedulerRefresh() {
            Interlocked.Increment(ref schedulerRefreshes);
        }

        public static void IncrementSchedulerSkipped() {
            Interlocked.Increment(ref schedulerSkipped);
        }

        public static void IncrementCanvasWillRenderEvent() {
            Interlocked.Increment(ref canvasWillRenderEvents);
        }

        public static void SetActiveUiObjectCounts(int canvasCount, int graphicRaycasterCount) {
            Interlocked.Exchange(ref activeCanvasCount, canvasCount);
            Interlocked.Exchange(ref activeGraphicRaycasterCount, graphicRaycasterCount);
        }

        public static void SetActiveSpritePointerShapeCount(int count) {
            Interlocked.Exchange(ref activeSpritePointerShapeCount, count);
        }

        public static void SetActiveAnimatorUpdateTickCount(int count) {
            Interlocked.Exchange(ref activeAnimatorUpdateTickCount, count);
        }

        public static void SetActorUpdateVisibilityCounts(int registered, int visible, int offscreen, int inactive, int unknown) {
            Interlocked.Exchange(ref actorUpdateRegisteredCount, MathfMax(0, registered));
            Interlocked.Exchange(ref actorUpdateVisibleCount, MathfMax(0, visible));
            Interlocked.Exchange(ref actorUpdateOffscreenCount, MathfMax(0, offscreen));
            Interlocked.Exchange(ref actorUpdateInactiveCount, MathfMax(0, inactive));
            Interlocked.Exchange(ref actorUpdateUnknownCount, MathfMax(0, unknown));
        }

        public static void SetActorLateVisibilityCounts(int registered, int visible, int offscreen, int inactive, int unknown) {
            Interlocked.Exchange(ref actorLateRegisteredCount, MathfMax(0, registered));
            Interlocked.Exchange(ref actorLateVisibleCount, MathfMax(0, visible));
            Interlocked.Exchange(ref actorLateOffscreenCount, MathfMax(0, offscreen));
            Interlocked.Exchange(ref actorLateInactiveCount, MathfMax(0, inactive));
            Interlocked.Exchange(ref actorLateUnknownCount, MathfMax(0, unknown));
        }

        public static void IncrementSpritePointerBroadphaseCheck() {
            Interlocked.Increment(ref spritePointerBroadphaseChecks);
        }

        public static void IncrementSpritePointerExactTest() {
            Interlocked.Increment(ref spritePointerExactTests);
        }

        public static void IncrementSpritePointerAlphaTest() {
            Interlocked.Increment(ref spritePointerAlphaTests);
        }

        public static void IncrementSpritePointerCacheHit() {
            Interlocked.Increment(ref spritePointerCacheHits);
        }

        public static void IncrementSpritePointerCacheMiss() {
            Interlocked.Increment(ref spritePointerCacheMisses);
        }

        public static void IncrementSpritePointerColliderRaycast() {
            Interlocked.Increment(ref spritePointerColliderRaycasts);
        }

        public static void AddSpritePointerColliderHits(int count) {
            if (count > 0) {
                Interlocked.Add(ref spritePointerColliderHits, count);
            }
        }

        public static void SetActiveSpritePointerColliderCount(int count) {
            Interlocked.Exchange(ref activeSpritePointerColliderCount, count);
        }

        public static void IncrementSpritePointerColliderRefresh() {
            Interlocked.Increment(ref spritePointerColliderRefreshes);
        }

        public static void IncrementSpritePointerRendererRecache() {
            Interlocked.Increment(ref spritePointerRendererRecaches);
        }

        public static void IncrementSpritePointerColliderMeshSwap() {
            Interlocked.Increment(ref spritePointerColliderMeshSwaps);
        }

        public static void IncrementSpriteFrameMeshBuild() {
            Interlocked.Increment(ref spriteFrameMeshBuilds);
        }

        public static void IncrementSpriteCompositedFrameBuild() {
            Interlocked.Increment(ref spriteCompositedFrameBuilds);
        }

        public static void IncrementSpriteGeometryCacheHit() {
            Interlocked.Increment(ref spriteGeometryCacheHits);
        }

        public static void IncrementSpriteGeometryCacheMiss() {
            Interlocked.Increment(ref spriteGeometryCacheMisses);
        }

        public static void AddSpriteGeometryPrewarmQueued(int count) {
            if (count > 0) {
                Interlocked.Add(ref spriteGeometryPrewarmQueued, count);
            }
        }

        public static void IncrementSpriteGeometryPrewarmBuilt() {
            Interlocked.Increment(ref spriteGeometryPrewarmBuilt);
        }

        public static void IncrementSpriteMaterialCacheHit() {
            Interlocked.Increment(ref spriteMaterialCacheHits);
        }

        public static void IncrementSpriteMaterialCacheMiss() {
            Interlocked.Increment(ref spriteMaterialCacheMisses);
        }

        public static void IncrementEffectCameraCacheHit() {
            Interlocked.Increment(ref effectCameraCacheHits);
        }

        public static void IncrementEffectCameraCacheMiss() {
            Interlocked.Increment(ref effectCameraCacheMisses);
        }

        public static void AddSpriteBatch(int instances, int batches, int fallbacks) {
            if (instances > 0) {
                Interlocked.Add(ref spriteBatchInstances, instances);
            }

            if (batches > 0) {
                Interlocked.Add(ref spriteBatchBatches, batches);
            }

            if (fallbacks > 0) {
                Interlocked.Add(ref spriteBatchFallbacks, fallbacks);
            }
        }

        public static void IncrementEffectPoolCreated() {
            Interlocked.Increment(ref effectPoolCreated);
        }

        public static void IncrementEffectPoolReused() {
            Interlocked.Increment(ref effectPoolReused);
        }

        public static void IncrementEffectPoolDropped() {
            Interlocked.Increment(ref effectPoolDropped);
        }

        public static void IncrementPacketTraceWrite() {
            Interlocked.Increment(ref packetTraceWrites);
        }

        public static void IncrementPacketTraceSkipped() {
            Interlocked.Increment(ref packetTraceSkipped);
        }

        public static void IncrementPacketLogRefresh() {
            Interlocked.Increment(ref packetLogRefreshes);
        }

        public static void AddPacketLogSnapshotEntries(int count) {
            if (count > 0) {
                Interlocked.Add(ref packetLogSnapshotEntries, count);
            }
        }

        public static void AddDrainToResultSnapshot(
            int packetCount,
            int playerChatCount,
            int timeNotificationCount,
            int actorEntryCount,
            int actorMoveCount,
            int playerMoveCount,
            int stopMoveCount,
            int actorVanishCount,
            int orderedEventCount
        ) {
            const int arrayCount = 9;
            int elementCount =
                MathfMax(0, packetCount) +
                MathfMax(0, playerChatCount) +
                MathfMax(0, timeNotificationCount) +
                MathfMax(0, actorEntryCount) +
                MathfMax(0, actorMoveCount) +
                MathfMax(0, playerMoveCount) +
                MathfMax(0, stopMoveCount) +
                MathfMax(0, actorVanishCount) +
                MathfMax(0, orderedEventCount);

            Interlocked.Increment(ref drainToResultCalls);
            Interlocked.Add(ref drainToResultArrayCount, arrayCount);
            if (elementCount > 0) {
                Interlocked.Add(ref drainToResultElementCount, elementCount);
            }

            Interlocked.Add(ref drainToResultEstimatedMinBytes, EstimateArraySnapshotBytes(arrayCount, elementCount));
        }

        public static void AddSnapshotRecent(int elementCount) {
            int safeElementCount = MathfMax(0, elementCount);
            Interlocked.Increment(ref snapshotRecentCalls);
            Interlocked.Increment(ref snapshotRecentArrayCount);
            if (safeElementCount > 0) {
                Interlocked.Add(ref snapshotRecentElementCount, safeElementCount);
            }

            Interlocked.Add(ref snapshotRecentEstimatedMinBytes, EstimateArraySnapshotBytes(1, safeElementCount));
        }

        public static void IncrementCombatTraceRecord() {
            Interlocked.Increment(ref combatTraceRecords);
        }

        public static void IncrementDebugLogLine() {
            Interlocked.Increment(ref debugLogLines);
        }

        public static void SetDayNightCounts(int materialCount, int lightCount) {
            Interlocked.Exchange(ref dayNightMaterialCount, MathfMax(0, materialCount));
            Interlocked.Exchange(ref dayNightLightCount, MathfMax(0, lightCount));
        }

        public static void AddDayNightMaterialApplies(int count) {
            if (count > 0) {
                Interlocked.Add(ref dayNightMaterialApplies, count);
            }
        }

        public static void AddDayNightLightApplies(int count) {
            if (count > 0) {
                Interlocked.Add(ref dayNightLightApplies, count);
            }
        }

        public static void SetStaticChunkCounts(int visible, int total) {
            Interlocked.Exchange(ref staticChunksVisible, MathfMax(0, visible));
            Interlocked.Exchange(ref staticChunksTotal, MathfMax(0, total));
        }

        public static RoRuntimePerfCounterSnapshot Snapshot() {
            lock (Sync) {
                return new RoRuntimePerfCounterSnapshot(
                    Interlocked.Read(ref sessionSnapshots),
                    Interlocked.Read(ref actorUpdateTicks),
                    Interlocked.Read(ref actorLateTicks),
                    Interlocked.Read(ref animatorTicks),
                    Interlocked.Read(ref forcedPresenterSyncs),
                    Interlocked.Read(ref forcedActorMotions),
                    Interlocked.Read(ref actorPresenterSyncs),
                    Interlocked.Read(ref actorPresenterActorsProcessed),
                    Interlocked.Read(ref actorPresenterActorsChanged),
                    Interlocked.Read(ref schedulerTicks),
                    Interlocked.Read(ref schedulerRefreshes),
                    Interlocked.Read(ref schedulerSkipped),
                    Interlocked.Read(ref canvasWillRenderEvents),
                    activeCanvasCount,
                    activeGraphicRaycasterCount,
                    activeSpritePointerShapeCount,
                    activeAnimatorUpdateTickCount,
                    actorUpdateRegisteredCount,
                    actorUpdateVisibleCount,
                    actorUpdateOffscreenCount,
                    actorUpdateInactiveCount,
                    actorUpdateUnknownCount,
                    actorLateRegisteredCount,
                    actorLateVisibleCount,
                    actorLateOffscreenCount,
                    actorLateInactiveCount,
                    actorLateUnknownCount,
                    Interlocked.Read(ref spritePointerBroadphaseChecks),
                    Interlocked.Read(ref spritePointerExactTests),
                    Interlocked.Read(ref spritePointerAlphaTests),
                    Interlocked.Read(ref spritePointerCacheHits),
                    Interlocked.Read(ref spritePointerCacheMisses),
                    Interlocked.Read(ref spritePointerColliderRaycasts),
                    Interlocked.Read(ref spritePointerColliderHits),
                    activeSpritePointerColliderCount,
                    Interlocked.Read(ref spritePointerColliderRefreshes),
                    Interlocked.Read(ref spritePointerRendererRecaches),
                    Interlocked.Read(ref spritePointerColliderMeshSwaps),
                    Interlocked.Read(ref spriteFrameMeshBuilds),
                    Interlocked.Read(ref spriteCompositedFrameBuilds),
                    Interlocked.Read(ref spriteGeometryCacheHits),
                    Interlocked.Read(ref spriteGeometryCacheMisses),
                    Interlocked.Read(ref spriteGeometryPrewarmQueued),
                    Interlocked.Read(ref spriteGeometryPrewarmBuilt),
                    Interlocked.Read(ref spriteMaterialCacheHits),
                    Interlocked.Read(ref spriteMaterialCacheMisses),
                    Interlocked.Read(ref effectCameraCacheHits),
                    Interlocked.Read(ref effectCameraCacheMisses),
                    Interlocked.Read(ref spriteBatchInstances),
                    Interlocked.Read(ref spriteBatchBatches),
                    Interlocked.Read(ref spriteBatchFallbacks),
                    Interlocked.Read(ref effectPoolCreated),
                    Interlocked.Read(ref effectPoolReused),
                    Interlocked.Read(ref effectPoolDropped),
                    Interlocked.Read(ref packetTraceWrites),
                    Interlocked.Read(ref packetTraceSkipped),
                    Interlocked.Read(ref packetLogRefreshes),
                    Interlocked.Read(ref packetLogSnapshotEntries),
                    Interlocked.Read(ref drainToResultCalls),
                    Interlocked.Read(ref drainToResultArrayCount),
                    Interlocked.Read(ref drainToResultElementCount),
                    Interlocked.Read(ref drainToResultEstimatedMinBytes),
                    Interlocked.Read(ref snapshotRecentCalls),
                    Interlocked.Read(ref snapshotRecentArrayCount),
                    Interlocked.Read(ref snapshotRecentElementCount),
                    Interlocked.Read(ref snapshotRecentEstimatedMinBytes),
                    Interlocked.Read(ref combatTraceRecords),
                    Interlocked.Read(ref debugLogLines),
                    dayNightMaterialCount,
                    dayNightLightCount,
                    Interlocked.Read(ref dayNightMaterialApplies),
                    Interlocked.Read(ref dayNightLightApplies),
                    staticChunksVisible,
                    staticChunksTotal,
                    new Dictionary<string, long>(UiRefreshCounts),
                    new Dictionary<string, long>(UiRefreshReasonCounts),
                    new Dictionary<string, long>(SnapshotCounts),
                    new Dictionary<string, long>(TextAssignmentCounts),
                    new Dictionary<string, long>(ActorLateTickCounts));
            }
        }

        public static RoPerformanceCounterFrameSnapshot FrameSnapshot() {
            return new RoPerformanceCounterFrameSnapshot(
                Interlocked.Read(ref packetsProcessed),
                Interlocked.Read(ref snapshotsBuilt),
                Interlocked.Read(ref assetLoads),
                Interlocked.Read(ref assetLoadBytes),
                Interlocked.Read(ref actorOverlaySpriteCacheHits),
                Interlocked.Read(ref actorOverlaySpriteCacheMisses),
                Interlocked.Read(ref actorOverlaySpriteCacheBuildFailures),
                Interlocked.Read(ref spriteAnimatorApplyFrameCalls),
                Interlocked.Read(ref spriteAnimatorApplyFrameTicks),
                Interlocked.Read(ref spriteAnimatorMeshAssigns),
                Interlocked.Read(ref spriteAnimatorMeshAssignTicks),
                Interlocked.Read(ref spriteAnimatorTextureAssigns),
                Interlocked.Read(ref spriteAnimatorTextureAssignTicks),
                Interlocked.Read(ref spriteAnimatorMutableMeshUpdates),
                Interlocked.Read(ref spriteAnimatorMutableMeshUpdateTicks),
                Interlocked.Read(ref spriteFrameMeshBuilds),
                Interlocked.Read(ref spriteCompositedFrameBuilds),
                Interlocked.Read(ref spriteGeometryCacheHits),
                Interlocked.Read(ref spriteGeometryCacheMisses),
                Interlocked.Read(ref spriteGeometryPrewarmQueued),
                Interlocked.Read(ref spriteGeometryPrewarmBuilt),
                Interlocked.Read(ref spriteMaterialCacheHits),
                Interlocked.Read(ref spriteMaterialCacheMisses),
                Interlocked.Read(ref actorOverlayUpdates),
                Interlocked.Read(ref actorOverlayUpdateTicks),
                Interlocked.Read(ref actorOverlayApplyFrameCalls),
                Interlocked.Read(ref actorOverlayApplyFrameTicks),
                Interlocked.Read(ref spriteEffectUpdates),
                Interlocked.Read(ref spriteEffectUpdateTicks),
                Interlocked.Read(ref spriteEffectLateUpdates),
                Interlocked.Read(ref spriteEffectLateUpdateTicks),
                Interlocked.Read(ref primitiveEffectUpdates),
                Interlocked.Read(ref primitiveEffectUpdateTicks),
                Interlocked.Read(ref actorSortingUpdates),
                Interlocked.Read(ref actorSortingUpdateTicks),
                Interlocked.Read(ref spawnedObjects),
                Interlocked.Read(ref despawnedObjects),
                Interlocked.Read(ref pathfindingRequests),
                Interlocked.Read(ref pointerResolveTargets),
                Interlocked.Read(ref pointerSyncTransforms),
                Interlocked.Read(ref pointerResolveTicks),
                Interlocked.Read(ref pointerWorldTargetsTicks),
                Interlocked.Read(ref pointerColliderRefreshTicks),
                Interlocked.Read(ref pointerColliderRefreshNowTicks),
                Interlocked.Read(ref pointerRaycastTicks),
                Interlocked.Read(ref pointerGroundPointTicks),
                Interlocked.Read(ref pointerResolverTicks),
                Interlocked.Read(ref clickToMoveUpdateTicks),
                Interlocked.Read(ref clickHandleTicks),
                Interlocked.Read(ref clickMoveToTargetTicks),
                Interlocked.Read(ref uiPointerCaptureTicks),
                Interlocked.Read(ref cursorSetPointerTargetTicks),
                Interlocked.Read(ref cursorVisualLateUpdateTicks),
                Interlocked.Read(ref cursorVisualApplyPointerTicks),
                Interlocked.Read(ref pointerScreenReportGuiTicks),
                Interlocked.Read(ref clickIntentResolveTicks),
                Interlocked.Read(ref actorServiceUpdateTicks),
                Interlocked.Read(ref actorServiceLateUpdateTicks),
                Interlocked.Read(ref effectManagerUpdateTicks),
                Interlocked.Read(ref minimalLoginShellUpdateTicks),
                Interlocked.Read(ref minimalLoginShellStableLightPathTicks),
                Interlocked.Read(ref minimalLoginShellApplyLiveDrainTicks),
                Interlocked.Read(ref minimalLoginShellApplyWorldDrainTicks),
                Interlocked.Read(ref minimalLoginShellApplyLiveDrainPostSyncTicks),
                Interlocked.Read(ref minimalLoginShellEnsurePhase12Ticks),
                Interlocked.Read(ref minimalLoginShellEnsureGameplayContextTicks),
                Interlocked.Read(ref minimalLoginShellRefreshGameplayWindowsTicks),
                Interlocked.Read(ref minimalLoginShellScheduleUiRefreshTicks),
                Interlocked.Read(ref onlineWorldUpdateTicks),
                Interlocked.Read(ref cameraLateUpdateTicks),
                Interlocked.Read(ref gridSelectorUpdateTicks),
                Interlocked.Read(ref runtimeMapLateUpdateTicks),
                Interlocked.Read(ref onGuiCalls),
                Interlocked.Read(ref onGuiLayoutEvents),
                Interlocked.Read(ref onGuiRepaintEvents),
                Interlocked.Read(ref onGuiMouseMoveEvents),
                Interlocked.Read(ref onGuiTicks),
                Interlocked.Read(ref minimalLoginShellOnGuiTicks),
                Interlocked.Read(ref actorHoverLabelOnGuiTicks),
                Interlocked.Read(ref actorShowScriptOnGuiTicks),
                Interlocked.Read(ref actorLightingDebugOnGuiTicks),
                Interlocked.Read(ref shortcutSettingsOnGuiTicks),
                Interlocked.Read(ref clickToMoveOnGuiTicks),
                Interlocked.Read(ref cursorVisualOnGuiTicks),
                Interlocked.Read(ref gridSelectorOnGuiTicks),
                activeSpritePointerShapeCount,
                activeSpritePointerColliderCount,
                Interlocked.Read(ref spritePointerBroadphaseChecks),
                Interlocked.Read(ref spritePointerExactTests),
                Interlocked.Read(ref spritePointerAlphaTests),
                Interlocked.Read(ref spritePointerCacheHits),
                Interlocked.Read(ref spritePointerCacheMisses),
                Interlocked.Read(ref spritePointerColliderRaycasts),
                Interlocked.Read(ref spritePointerColliderHits),
                Interlocked.Read(ref spritePointerColliderRefreshes),
                Interlocked.Read(ref spritePointerRendererRecaches),
                Interlocked.Read(ref spritePointerColliderMeshSwaps));
        }

        public static void Reset() {
            Interlocked.Exchange(ref sessionSnapshots, 0);
            Interlocked.Exchange(ref packetsProcessed, 0);
            Interlocked.Exchange(ref snapshotsBuilt, 0);
            Interlocked.Exchange(ref assetLoads, 0);
            Interlocked.Exchange(ref assetLoadBytes, 0);
            Interlocked.Exchange(ref actorOverlaySpriteCacheHits, 0);
            Interlocked.Exchange(ref actorOverlaySpriteCacheMisses, 0);
            Interlocked.Exchange(ref actorOverlaySpriteCacheBuildFailures, 0);
            Interlocked.Exchange(ref spriteAnimatorApplyFrameCalls, 0);
            Interlocked.Exchange(ref spriteAnimatorApplyFrameTicks, 0);
            Interlocked.Exchange(ref spriteAnimatorMeshAssigns, 0);
            Interlocked.Exchange(ref spriteAnimatorMeshAssignTicks, 0);
            Interlocked.Exchange(ref spriteAnimatorTextureAssigns, 0);
            Interlocked.Exchange(ref spriteAnimatorTextureAssignTicks, 0);
            Interlocked.Exchange(ref spriteAnimatorMutableMeshUpdates, 0);
            Interlocked.Exchange(ref spriteAnimatorMutableMeshUpdateTicks, 0);
            Interlocked.Exchange(ref actorOverlayUpdates, 0);
            Interlocked.Exchange(ref actorOverlayUpdateTicks, 0);
            Interlocked.Exchange(ref actorOverlayApplyFrameCalls, 0);
            Interlocked.Exchange(ref actorOverlayApplyFrameTicks, 0);
            Interlocked.Exchange(ref spriteEffectUpdates, 0);
            Interlocked.Exchange(ref spriteEffectUpdateTicks, 0);
            Interlocked.Exchange(ref spriteEffectLateUpdates, 0);
            Interlocked.Exchange(ref spriteEffectLateUpdateTicks, 0);
            Interlocked.Exchange(ref primitiveEffectUpdates, 0);
            Interlocked.Exchange(ref primitiveEffectUpdateTicks, 0);
            Interlocked.Exchange(ref actorSortingUpdates, 0);
            Interlocked.Exchange(ref actorSortingUpdateTicks, 0);
            ClearCaptureAssetLoadEvents();
            Interlocked.Exchange(ref spawnedObjects, 0);
            Interlocked.Exchange(ref despawnedObjects, 0);
            Interlocked.Exchange(ref pathfindingRequests, 0);
            Interlocked.Exchange(ref pointerResolveTargets, 0);
            Interlocked.Exchange(ref pointerSyncTransforms, 0);
            Interlocked.Exchange(ref pointerResolveTicks, 0);
            Interlocked.Exchange(ref pointerWorldTargetsTicks, 0);
            Interlocked.Exchange(ref pointerColliderRefreshTicks, 0);
            Interlocked.Exchange(ref pointerColliderRefreshNowTicks, 0);
            Interlocked.Exchange(ref pointerRaycastTicks, 0);
            Interlocked.Exchange(ref pointerGroundPointTicks, 0);
            Interlocked.Exchange(ref pointerResolverTicks, 0);
            Interlocked.Exchange(ref clickToMoveUpdateTicks, 0);
            Interlocked.Exchange(ref clickHandleTicks, 0);
            Interlocked.Exchange(ref clickMoveToTargetTicks, 0);
            Interlocked.Exchange(ref uiPointerCaptureTicks, 0);
            Interlocked.Exchange(ref cursorSetPointerTargetTicks, 0);
            Interlocked.Exchange(ref cursorVisualLateUpdateTicks, 0);
            Interlocked.Exchange(ref cursorVisualApplyPointerTicks, 0);
            Interlocked.Exchange(ref pointerScreenReportGuiTicks, 0);
            Interlocked.Exchange(ref clickIntentResolveTicks, 0);
            Interlocked.Exchange(ref actorServiceUpdateTicks, 0);
            Interlocked.Exchange(ref actorServiceLateUpdateTicks, 0);
            Interlocked.Exchange(ref effectManagerUpdateTicks, 0);
            Interlocked.Exchange(ref minimalLoginShellUpdateTicks, 0);
            Interlocked.Exchange(ref minimalLoginShellStableLightPathTicks, 0);
            Interlocked.Exchange(ref minimalLoginShellApplyLiveDrainTicks, 0);
            Interlocked.Exchange(ref minimalLoginShellApplyWorldDrainTicks, 0);
            Interlocked.Exchange(ref minimalLoginShellApplyLiveDrainPostSyncTicks, 0);
            Interlocked.Exchange(ref minimalLoginShellEnsurePhase12Ticks, 0);
            Interlocked.Exchange(ref minimalLoginShellEnsureGameplayContextTicks, 0);
            Interlocked.Exchange(ref minimalLoginShellRefreshGameplayWindowsTicks, 0);
            Interlocked.Exchange(ref minimalLoginShellScheduleUiRefreshTicks, 0);
            Interlocked.Exchange(ref onlineWorldUpdateTicks, 0);
            Interlocked.Exchange(ref cameraLateUpdateTicks, 0);
            Interlocked.Exchange(ref gridSelectorUpdateTicks, 0);
            Interlocked.Exchange(ref runtimeMapLateUpdateTicks, 0);
            Interlocked.Exchange(ref onGuiCalls, 0);
            Interlocked.Exchange(ref onGuiLayoutEvents, 0);
            Interlocked.Exchange(ref onGuiRepaintEvents, 0);
            Interlocked.Exchange(ref onGuiMouseMoveEvents, 0);
            Interlocked.Exchange(ref onGuiTicks, 0);
            Interlocked.Exchange(ref minimalLoginShellOnGuiTicks, 0);
            Interlocked.Exchange(ref actorHoverLabelOnGuiTicks, 0);
            Interlocked.Exchange(ref actorShowScriptOnGuiTicks, 0);
            Interlocked.Exchange(ref actorLightingDebugOnGuiTicks, 0);
            Interlocked.Exchange(ref shortcutSettingsOnGuiTicks, 0);
            Interlocked.Exchange(ref clickToMoveOnGuiTicks, 0);
            Interlocked.Exchange(ref cursorVisualOnGuiTicks, 0);
            Interlocked.Exchange(ref gridSelectorOnGuiTicks, 0);
            Interlocked.Exchange(ref actorUpdateTicks, 0);
            Interlocked.Exchange(ref actorLateTicks, 0);
            Interlocked.Exchange(ref animatorTicks, 0);
            Interlocked.Exchange(ref forcedPresenterSyncs, 0);
            Interlocked.Exchange(ref forcedActorMotions, 0);
            Interlocked.Exchange(ref actorPresenterSyncs, 0);
            Interlocked.Exchange(ref actorPresenterActorsProcessed, 0);
            Interlocked.Exchange(ref actorPresenterActorsChanged, 0);
            Interlocked.Exchange(ref schedulerTicks, 0);
            Interlocked.Exchange(ref schedulerRefreshes, 0);
            Interlocked.Exchange(ref schedulerSkipped, 0);
            Interlocked.Exchange(ref canvasWillRenderEvents, 0);
            Interlocked.Exchange(ref spritePointerBroadphaseChecks, 0);
            Interlocked.Exchange(ref spritePointerExactTests, 0);
            Interlocked.Exchange(ref spritePointerAlphaTests, 0);
            Interlocked.Exchange(ref spritePointerCacheHits, 0);
            Interlocked.Exchange(ref spritePointerCacheMisses, 0);
            Interlocked.Exchange(ref spritePointerColliderRaycasts, 0);
            Interlocked.Exchange(ref spritePointerColliderHits, 0);
            Interlocked.Exchange(ref spritePointerColliderRefreshes, 0);
            Interlocked.Exchange(ref spritePointerRendererRecaches, 0);
            Interlocked.Exchange(ref spritePointerColliderMeshSwaps, 0);
            Interlocked.Exchange(ref spriteFrameMeshBuilds, 0);
            Interlocked.Exchange(ref spriteCompositedFrameBuilds, 0);
            Interlocked.Exchange(ref spriteGeometryCacheHits, 0);
            Interlocked.Exchange(ref spriteGeometryCacheMisses, 0);
            Interlocked.Exchange(ref spriteGeometryPrewarmQueued, 0);
            Interlocked.Exchange(ref spriteGeometryPrewarmBuilt, 0);
            Interlocked.Exchange(ref spriteMaterialCacheHits, 0);
            Interlocked.Exchange(ref spriteMaterialCacheMisses, 0);
            Interlocked.Exchange(ref effectCameraCacheHits, 0);
            Interlocked.Exchange(ref effectCameraCacheMisses, 0);
            Interlocked.Exchange(ref spriteBatchInstances, 0);
            Interlocked.Exchange(ref spriteBatchBatches, 0);
            Interlocked.Exchange(ref spriteBatchFallbacks, 0);
            Interlocked.Exchange(ref effectPoolCreated, 0);
            Interlocked.Exchange(ref effectPoolReused, 0);
            Interlocked.Exchange(ref effectPoolDropped, 0);
            Interlocked.Exchange(ref packetTraceWrites, 0);
            Interlocked.Exchange(ref packetTraceSkipped, 0);
            Interlocked.Exchange(ref packetLogRefreshes, 0);
            Interlocked.Exchange(ref packetLogSnapshotEntries, 0);
            Interlocked.Exchange(ref drainToResultCalls, 0);
            Interlocked.Exchange(ref drainToResultArrayCount, 0);
            Interlocked.Exchange(ref drainToResultElementCount, 0);
            Interlocked.Exchange(ref drainToResultEstimatedMinBytes, 0);
            Interlocked.Exchange(ref snapshotRecentCalls, 0);
            Interlocked.Exchange(ref snapshotRecentArrayCount, 0);
            Interlocked.Exchange(ref snapshotRecentElementCount, 0);
            Interlocked.Exchange(ref snapshotRecentEstimatedMinBytes, 0);
            Interlocked.Exchange(ref combatTraceRecords, 0);
            Interlocked.Exchange(ref debugLogLines, 0);
            Interlocked.Exchange(ref dayNightMaterialApplies, 0);
            Interlocked.Exchange(ref dayNightLightApplies, 0);
            lock (Sync) {
                UiRefreshCounts.Clear();
                UiRefreshReasonCounts.Clear();
                SnapshotCounts.Clear();
                TextAssignmentCounts.Clear();
                ActorLateTickCounts.Clear();
            }
        }

        private static void IncrementNamed(Dictionary<string, long> counters, string key) {
            IncrementNamed(counters, key, 1);
        }

        private static void IncrementNamed(Dictionary<string, long> counters, string key, long amount) {
            if (string.IsNullOrEmpty(key)) {
                key = "unknown";
            }

            lock (Sync) {
                counters.TryGetValue(key, out long value);
                counters[key] = value + amount;
            }
        }

        private static void AddPositiveTicks(ref long counter, long ticks) {
            if (performanceCaptureTimingEnabled && ticks > 0) {
                Interlocked.Add(ref counter, ticks);
            }
        }

        private static void AddCallTicks(ref long calls, ref long ticksCounter, long ticks) {
            if (performanceCaptureTimingEnabled && ticks > 0) {
                Interlocked.Increment(ref calls);
                Interlocked.Add(ref ticksCounter, ticks);
            }
        }

        private static long EstimateArraySnapshotBytes(int arrayCount, int elementCount) {
            const int arrayHeaderBytes = 24;
            return ((long)MathfMax(0, arrayCount) * arrayHeaderBytes) + ((long)MathfMax(0, elementCount) * IntPtr.Size);
        }

        private static int MathfMax(int left, int right) => left > right ? left : right;
    }

    public static class RoPerformanceCounters {
        public static void IncrementPacketsProcessed() => RoRuntimePerfCounters.IncrementPacketsProcessed();
        public static void IncrementSnapshotsBuilt() => RoRuntimePerfCounters.IncrementSnapshotsBuilt();
        public static void IncrementAssetLoads() => RoRuntimePerfCounters.IncrementAssetLoads();
        public static void IncrementSpawnedObjects() => RoRuntimePerfCounters.IncrementSpawnedObjects();
        public static void IncrementDespawnedObjects() => RoRuntimePerfCounters.IncrementDespawnedObjects();
        public static void IncrementPathRequests() => RoRuntimePerfCounters.IncrementPathRequests();
        public static void IncrementPointerResolveTarget() => RoRuntimePerfCounters.IncrementPointerResolveTarget();
        public static void IncrementPointerSyncTransforms() => RoRuntimePerfCounters.IncrementPointerSyncTransforms();
        public static void AddPointerResolveTicks(long ticks) => RoRuntimePerfCounters.AddPointerResolveTicks(ticks);
        public static void AddPointerWorldTargetsTicks(long ticks) => RoRuntimePerfCounters.AddPointerWorldTargetsTicks(ticks);
        public static void AddPointerColliderRefreshTicks(long ticks) => RoRuntimePerfCounters.AddPointerColliderRefreshTicks(ticks);
        public static void AddPointerColliderRefreshNowTicks(long ticks) => RoRuntimePerfCounters.AddPointerColliderRefreshNowTicks(ticks);
        public static void AddPointerRaycastTicks(long ticks) => RoRuntimePerfCounters.AddPointerRaycastTicks(ticks);
        public static void AddPointerGroundPointTicks(long ticks) => RoRuntimePerfCounters.AddPointerGroundPointTicks(ticks);
        public static void AddPointerResolverTicks(long ticks) => RoRuntimePerfCounters.AddPointerResolverTicks(ticks);
        public static void AddClickToMoveUpdateTicks(long ticks) => RoRuntimePerfCounters.AddClickToMoveUpdateTicks(ticks);
        public static void AddClickHandleTicks(long ticks) => RoRuntimePerfCounters.AddClickHandleTicks(ticks);
        public static void AddClickMoveToTargetTicks(long ticks) => RoRuntimePerfCounters.AddClickMoveToTargetTicks(ticks);
        public static void AddUiPointerCaptureTicks(long ticks) => RoRuntimePerfCounters.AddUiPointerCaptureTicks(ticks);
        public static void AddCursorSetPointerTargetTicks(long ticks) => RoRuntimePerfCounters.AddCursorSetPointerTargetTicks(ticks);
        public static void AddCursorVisualLateUpdateTicks(long ticks) => RoRuntimePerfCounters.AddCursorVisualLateUpdateTicks(ticks);
        public static void AddCursorVisualApplyPointerTicks(long ticks) => RoRuntimePerfCounters.AddCursorVisualApplyPointerTicks(ticks);
        public static void AddPointerScreenReportGuiTicks(long ticks) => RoRuntimePerfCounters.AddPointerScreenReportGuiTicks(ticks);
        public static void AddClickIntentResolveTicks(long ticks) => RoRuntimePerfCounters.AddClickIntentResolveTicks(ticks);
    }

    public static class RoProfilerMarkers {
        public static readonly ProfilerMarker ActorTick = new ProfilerMarker("RO.ActorTick");
        public static readonly ProfilerMarker MobAiTick = new ProfilerMarker("RO.MobAI.Tick");
        public static readonly ProfilerMarker Pathfinding = new ProfilerMarker("RO.Pathfinding");
        public static readonly ProfilerMarker SpriteAnimatorTick = new ProfilerMarker("RO.SpriteAnimator.Tick");
        public static readonly ProfilerMarker SpriteAnimatorApplyFrame = new ProfilerMarker("RO.SpriteAnimator.ApplyFrame");
        public static readonly ProfilerMarker EffectSystemTick = new ProfilerMarker("RO.EffectSystem.Tick");
        public static readonly ProfilerMarker SpriteEffectUpdate = new ProfilerMarker("RO.SpriteEffect.Update");
        public static readonly ProfilerMarker SpriteEffectLateUpdate = new ProfilerMarker("RO.SpriteEffect.LateUpdate");
        public static readonly ProfilerMarker PrimitiveEffectUpdate = new ProfilerMarker("RO.PrimitiveEffect.Update");
        public static readonly ProfilerMarker ActorOverlayUpdate = new ProfilerMarker("RO.ActorOverlay.Update");
        public static readonly ProfilerMarker ActorOverlayApplyFrame = new ProfilerMarker("RO.ActorOverlay.ApplyFrame");
        public static readonly ProfilerMarker SkillEffectSpawn = new ProfilerMarker("RO.SkillEffect.Spawn");
        public static readonly ProfilerMarker DamageNumbersSpawn = new ProfilerMarker("RO.DamageNumbers.Spawn");
        public static readonly ProfilerMarker DamageNumbersUpdate = new ProfilerMarker("RO.DamageNumbers.Update");
        public static readonly ProfilerMarker DamageNumbersApplyFrame = new ProfilerMarker("RO.DamageNumbers.ApplyFrame");
        public static readonly ProfilerMarker UiRefresh = new ProfilerMarker("RO.UI.Refresh");
        public static readonly ProfilerMarker NameplatesRefresh = new ProfilerMarker("RO.Nameplates.Refresh");
        public static readonly ProfilerMarker NetworkPacketBatch = new ProfilerMarker("RO.Network.PacketBatch");
        public static readonly ProfilerMarker SessionBuildSnapshot = new ProfilerMarker("RO.Session.BuildSnapshot");
        public static readonly ProfilerMarker GrfLoad = new ProfilerMarker("RO.GRF.Load");
        public static readonly ProfilerMarker AssetCacheMiss = new ProfilerMarker("RO.AssetCache.Miss");
        public static readonly ProfilerMarker SortingDepthUpdate = new ProfilerMarker("RO.SortingDepth.Update");
        public static readonly ProfilerMarker PointerResolveTarget = new ProfilerMarker("RO.Pointer.ResolveTarget");
        public static readonly ProfilerMarker PointerSyncTransforms = new ProfilerMarker("RO.Pointer.SyncTransforms");
        public static readonly ProfilerMarker PointerRaycast = new ProfilerMarker("RO.Pointer.Raycast");
        public static readonly ProfilerMarker PointerSpriteBroadphase = new ProfilerMarker("RO.Pointer.SpriteBroadphase");
        public static readonly ProfilerMarker PointerSpriteExact = new ProfilerMarker("RO.Pointer.SpriteExact");
    }
}
