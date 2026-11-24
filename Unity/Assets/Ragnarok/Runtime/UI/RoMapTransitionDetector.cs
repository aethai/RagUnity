using Ragnarok.Network;

namespace Ragnarok.UI {
    internal readonly struct RoPendingMapTransition {
        private RoPendingMapTransition(
            RoLiveMapSession liveMapSession,
            RoLoginEnterMapResult previousResult,
            RoZcNpcMapMove mapMove,
            RoZcNpcServerMove serverMove,
            bool reconnectToMapServer,
            bool sameMapMove) {
            LiveMapSession = liveMapSession;
            PreviousResult = previousResult;
            MapMove = mapMove;
            ServerMove = serverMove;
            ReconnectToMapServer = reconnectToMapServer;
            IsSameMapMove = sameMapMove;
        }

        public RoLiveMapSession LiveMapSession { get; }
        public RoLoginEnterMapResult PreviousResult { get; }
        public RoZcNpcMapMove MapMove { get; }
        public RoZcNpcServerMove ServerMove { get; }
        public bool ReconnectToMapServer { get; }
        public bool IsSameMapMove { get; }
        public bool RequiresRuntimeMapLoad => ReconnectToMapServer || !IsSameMapMove;
        public bool ShowsLoadingScreen => RequiresRuntimeMapLoad;

        public static RoPendingMapTransition SameServer(
            RoLiveMapSession liveMapSession,
            RoLoginEnterMapResult previousResult,
            RoZcNpcMapMove mapMove,
            bool sameMapMove) {
            return new RoPendingMapTransition(liveMapSession, previousResult, mapMove, default, false, sameMapMove);
        }

        public static RoPendingMapTransition Redirect(
            RoLiveMapSession liveMapSession,
            RoLoginEnterMapResult previousResult,
            RoZcNpcServerMove serverMove) {
            return new RoPendingMapTransition(liveMapSession, previousResult, default, serverMove, true, false);
        }
    }

    internal static class RoMapTransitionDetector {
        public static bool TryResolve(
            RoLiveMapSession liveMapSession,
            RoLoginEnterMapResult previousResult,
            RoMapEntryDrainResult drain,
            out RoPendingMapTransition transition,
            out string statusText) {
            if (drain?.OrderedEvents != null) {
                for (int i = 0; i < drain.OrderedEvents.Length; i++) {
                    RoMapEntryDrainEvent entry = drain.OrderedEvents[i];
                    if (entry.Kind == RoMapEntryDrainEventKind.NpcMapMove) {
                        RoZcNpcMapMove mapMove = entry.NpcMapMove;
                        transition = RoPendingMapTransition.SameServer(
                            liveMapSession,
                            previousResult,
                            mapMove,
                            IsSameMapMove(previousResult, mapMove.MapName));
                        statusText = $"changing map to {mapMove.MapName} {mapMove.X},{mapMove.Y}";
                        return true;
                    }

                    if (entry.Kind == RoMapEntryDrainEventKind.NpcServerMove) {
                        RoZcNpcServerMove serverMove = entry.NpcServerMove;
                        transition = RoPendingMapTransition.Redirect(liveMapSession, previousResult, serverMove);
                        statusText = $"changing map server to {serverMove.MapName} {serverMove.X},{serverMove.Y}";
                        return true;
                    }
                }
            }

            transition = default;
            statusText = string.Empty;
            return false;
        }

        private static bool IsSameMapMove(RoLoginEnterMapResult previousResult, string targetMapName) {
            string currentMapName = previousResult?.CharacterResult?.MapHandoff.MapName;
            if (string.IsNullOrWhiteSpace(currentMapName) || string.IsNullOrWhiteSpace(targetMapName)) {
                return false;
            }

            return string.Equals(
                NormalizeComparableMapName(currentMapName),
                NormalizeComparableMapName(targetMapName),
                System.StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeComparableMapName(string mapName) {
            string normalized = (mapName ?? string.Empty).Trim().Replace('\\', '/');
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0) {
                normalized = normalized.Substring(slashIndex + 1);
            }

            if (normalized.EndsWith(".gat", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".rsw", System.StringComparison.OrdinalIgnoreCase)) {
                normalized = normalized.Substring(0, normalized.Length - 4);
            }

            return normalized;
        }
    }
}
