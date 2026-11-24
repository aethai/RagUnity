using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Ragnarok.Assets;
using Ragnarok.Maps;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>A towninfo record of the current map: the NPC's name, its cell and its TYPE (0..7 facility icons, 8 warp).</summary>
    public readonly struct RoMinimapTownInfoMarker {
        public RoMinimapTownInfoMarker(string name, int x, int y, int type) {
            Name = string.IsNullOrWhiteSpace(name) ? "Map location" : name.Trim();
            X = x;
            Y = y;
            Type = type;
        }

        public string Name { get; }
        public int X { get; }
        public int Y { get; }
        public int Type { get; }
    }

    /// <summary>
    /// The minimap's data, shared by the HUD minimap 0xe and the big minimap 0x111 (RoClassicMinimap): the bound world /
    /// runtime map, the map name (the session's map HUD while the world has no own actor or map yet), the player's cell
    /// and direction, the map's GAT size, the indoor gate, the towninfo records of the map and the compass marks
    /// (ZC_COMPASS). <see cref="NeedsRefresh"/> tells whether any of it moved since the last <see cref="Refresh"/>.
    /// </summary>
    public sealed class RoMinimapModel {
        private const float WorldControllerLookupRetrySeconds = 0.5f;
        private static readonly string[] TownInfoCandidatePaths = {
            "data/luafiles514/lua files/system/towninfo.lub",
            "data/lua files/system/towninfo.lub",
            "data/system/towninfo.lub",
            "System/Towninfo.lub",
            "SystemEN/Towninfo.lub",
            "towninfo.lub"
        };
        private static readonly Regex TownInfoEntryRegex = new Regex(
            "\\{[^{}]*?\\bname\\s*=\\s*(?:\\[=\\[(?<bracket>.*?)\\]=\\]|\\\"(?<double>[^\\\"]*)\\\"|'(?<single>[^']*)')[^{}]*?\\bX\\s*=\\s*(?<x>-?\\d+)[^{}]*?\\bY\\s*=\\s*(?<y>-?\\d+)[^{}]*?\\bTYPE\\s*=\\s*(?<type>-?\\d+)[^{}]*?\\}",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex AddTownInfoRegex = new Regex(
            "AddTownInfo\\s*\\(\\s*['\\\"](?<map>[^'\\\"]+)['\\\"]\\s*,\\s*(?:\\[=\\[(?<bracket>.*?)\\]=\\]|['\\\"](?<name>[^'\\\"]*)['\\\"])\\s*,\\s*(?<x>-?\\d+)\\s*,\\s*(?<y>-?\\d+)\\s*,\\s*(?<type>-?\\d+)\\s*\\)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private readonly List<RoOnlineMinimapMarkerSnapshot> markers = new List<RoOnlineMinimapMarkerSnapshot>(32);
        private readonly List<RoMinimapTownInfoMarker> townInfoMarkers = new List<RoMinimapTownInfoMarker>(64);
        private RoOnlineWorldController worldController;
        private RoRuntimeMapController runtimeMapController;
        private float nextWorldControllerLookupTime;
        private string townInfoKey = string.Empty;
        private int lastGameplayChangeSequence = int.MinValue;
        private bool lastRefreshedHasPlayerCell;
        private Vector2Int lastRefreshedPlayerCell;
        private int lastRefreshedPlayerDirection = int.MinValue;
        private int lastRefreshedMinimapMarkerRevision = int.MinValue;

        public RoLoginShellSession Session { get; private set; }

        /// <summary>The map of the last refresh, as the session / runtime map names it (may carry ".gat").</summary>
        public string MapName { get; private set; } = string.Empty;

        /// <summary>The last refresh's map in the client's lower-case stem form ("prontera").</summary>
        public string MapStem { get; private set; } = string.Empty;

        public bool HasPlayer { get; private set; }
        public Vector2Int Cell { get; private set; }
        public int Direction { get; private set; }
        public int MapWidth { get; private set; } = 1;
        public int MapHeight { get; private set; } = 1;

        /// <summary>The map is indoor (the runtime map's flag or the indoor resource table): no minimap.</summary>
        public bool HiddenForMap { get; private set; }

        /// <summary>The towninfo records of the last refresh's map, sorted by Y then X.</summary>
        public List<RoMinimapTownInfoMarker> TownInfoMarkers => townInfoMarkers;

        /// <summary>The compass marks (ZC_COMPASS) of the last refresh.</summary>
        public List<RoOnlineMinimapMarkerSnapshot> Markers => markers;

        /// <summary>The shell on a map entry: the session, the online world and the runtime map now drawn.</summary>
        public void Bind(RoLoginShellSession loginSession, RoOnlineWorldController onlineWorld, RoRuntimeMapController activeMap) {
            Session = loginSession;
            worldController = onlineWorld;
            nextWorldControllerLookupTime = 0f;
            runtimeMapController = activeMap != null
                ? activeMap
                : onlineWorld != null ? onlineWorld.RuntimeMapController : null;
        }

        /// <summary>Reads the map, the player, the GAT size, the towninfo of a new map and the compass marks.</summary>
        public void Refresh() {
            RoOnlineWorldController controller = ResolveWorldController();
            RoRuntimeMapController activeMap = ResolveRuntimeMapController(controller);
            RoShellMapHudSnapshot? snapshot = ShouldUseSessionMapHudFallback(controller, activeMap)
                ? Session.MapHudSnapshot()
                : (RoShellMapHudSnapshot?) null;
            string mapName = ResolveMapName(snapshot, activeMap);
            MapName = mapName ?? string.Empty;
            MapStem = NormalizeMapStem(mapName);
            bool hasPlayer = TryResolvePlayerCell(controller, snapshot, out Vector2Int cell, out int direction);
            HasPlayer = hasPlayer;
            Cell = cell;
            Direction = direction;
            lastRefreshedHasPlayerCell = hasPlayer;
            lastRefreshedPlayerCell = cell;
            lastRefreshedPlayerDirection = hasPlayer ? direction : int.MinValue;
            lastRefreshedMinimapMarkerRevision = controller != null ? controller.MinimapMarkerRevision : int.MinValue;
            lastGameplayChangeSequence = Session != null ? Session.GameplayChangeSequence : 0;
            ResolveMapDimensions(controller, cell, hasPlayer, out int width, out int height);
            MapWidth = width;
            MapHeight = height;
            LoadTownInfoForMap(mapName, activeMap);
            if (controller != null) controller.SnapshotMinimapMarkers(markers);
            else markers.Clear();
            HiddenForMap = ShouldHideMinimapForMap(mapName, activeMap);
        }

        /// <summary>The session's gameplay sequence, the player's cell / direction or the compass marks moved.</summary>
        public bool NeedsRefresh() {
            return Session == null || Session.GameplayChangeSequence != lastGameplayChangeSequence || HasWorldStateChanged();
        }

        private bool HasWorldStateChanged() {
            RoOnlineWorldController controller = ResolveWorldController();
            if (controller == null) {
                return lastRefreshedHasPlayerCell ||
                    lastRefreshedPlayerDirection != int.MinValue ||
                    lastRefreshedMinimapMarkerRevision != int.MinValue;
            }

            RoRuntimeMapController activeMap = ResolveRuntimeMapController(controller);
            RoShellMapHudSnapshot? snapshot = ShouldUseSessionMapHudFallback(controller, activeMap)
                ? Session.MapHudSnapshot()
                : (RoShellMapHudSnapshot?) null;
            bool hasPlayer = TryResolvePlayerCell(controller, snapshot, out Vector2Int playerCell, out int playerDirection);
            return hasPlayer != lastRefreshedHasPlayerCell ||
                (hasPlayer && (playerCell != lastRefreshedPlayerCell || playerDirection != lastRefreshedPlayerDirection)) ||
                controller.MinimapMarkerRevision != lastRefreshedMinimapMarkerRevision;
        }

        /// <summary>The bound online world, else a scene lookup at most every half second.</summary>
        public RoOnlineWorldController ResolveWorldController(bool forceLookup = false) {
            if (worldController != null) {
                return worldController;
            }

            float now = Time.unscaledTime;
            if (!forceLookup && now < nextWorldControllerLookupTime) {
                return null;
            }

            nextWorldControllerLookupTime = now + WorldControllerLookupRetrySeconds;
            worldController = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            return worldController;
        }

        public RoRuntimeMapController ResolveRuntimeMapController(RoOnlineWorldController controller) {
            if (runtimeMapController != null) {
                return runtimeMapController;
            }

            return controller != null ? controller.RuntimeMapController : null;
        }

        private bool ShouldUseSessionMapHudFallback(RoOnlineWorldController controller, RoRuntimeMapController mapController) {
            if (Session == null) {
                return false;
            }

            if (controller == null || controller.OwnActorId == 0) {
                return true;
            }

            return mapController == null || string.IsNullOrWhiteSpace(mapController.MapName);
        }

        private static string ResolveMapName(RoShellMapHudSnapshot? snapshot, RoRuntimeMapController mapController) {
            string snapshotMapName = snapshot.HasValue ? snapshot.Value.MapName : string.Empty;
            if (!string.IsNullOrWhiteSpace(snapshotMapName)) {
                return snapshotMapName;
            }

            return mapController != null ? mapController.MapName : string.Empty;
        }

        private static bool ShouldHideMinimapForMap(string mapName, RoRuntimeMapController mapController) {
            if (mapController == null) {
                return false;
            }

            return mapController.IsLoadedMapIndoor || mapController.IsIndoorMapResource(mapName);
        }

        private static bool TryResolvePlayerCell(
            RoOnlineWorldController controller,
            RoShellMapHudSnapshot? snapshot,
            out Vector2Int cell,
            out int direction
        ) {
            if (controller != null &&
                controller.OwnActorId != 0 &&
                controller.State.TryGetActor(controller.OwnActorId, out RoOnlineActorSnapshot actor)) {
                cell = new Vector2Int(actor.CellX, actor.CellY);
                direction = actor.Direction;
                return true;
            }

            if (snapshot.HasValue && snapshot.Value.HasMapEntryPosition) {
                RoShellMapHudSnapshot hud = snapshot.Value;
                cell = new Vector2Int(hud.MapEntryX, hud.MapEntryY);
                direction = hud.MapEntryDirection;
                return true;
            }

            cell = default;
            direction = 0;
            return false;
        }

        private static void ResolveMapDimensions(
            RoOnlineWorldController controller,
            Vector2Int playerCell,
            bool hasPlayer,
            out int width,
            out int height
        ) {
            RoGridSelector selector = controller != null ? controller.GridSelector : null;
            if (selector != null && selector.Altitude != null) {
                width = Mathf.Max(1, (int) selector.Altitude.width);
                height = Mathf.Max(1, (int) selector.Altitude.height);
                return;
            }

            width = hasPlayer ? Mathf.Max(300, playerCell.x + 32) : 300;
            height = hasPlayer ? Mathf.Max(300, playerCell.y + 32) : 300;
        }

        /// <summary>The towninfo records of the map, read once per map (and asset database).</summary>
        private void LoadTownInfoForMap(string mapName, RoRuntimeMapController mapController) {
            string mapStem = NormalizeMapStem(mapName);
            RoAssetDatabase assets = mapController != null && mapController.Content != null && mapController.Content.IsInitialized
                ? mapController.Content.Assets
                : null;
            string key = mapStem + "|" + (assets != null ? assets.GetHashCode().ToString() : "fallback");
            if (string.Equals(townInfoKey, key, StringComparison.Ordinal)) {
                return;
            }

            townInfoKey = key;
            townInfoMarkers.Clear();
            if (assets == null || string.IsNullOrWhiteSpace(mapStem)) {
                return;
            }

            for (int i = 0; i < TownInfoCandidatePaths.Length; i++) {
                string path = TownInfoCandidatePaths[i];
                if (!assets.Exists(path)) {
                    continue;
                }

                try {
                    string text = assets.ReadText(path);
                    if (TryParseTownInfoMarkersForMap(text, mapStem, townInfoMarkers)) {
                        return;
                    }
                } catch (Exception) {
                    townInfoMarkers.Clear();
                }
            }
        }

        public static bool TryParseTownInfoMarkersForMap(
            string text,
            string mapStem,
            List<RoMinimapTownInfoMarker> markers
        ) {
            if (markers == null) {
                throw new ArgumentNullException(nameof(markers));
            }

            markers.Clear();
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(mapStem)) {
                return false;
            }

            string normalizedMap = NormalizeMapStem(mapStem);
            if (TryExtractLuaMapBlock(text, normalizedMap, out string block)) {
                foreach (Match match in TownInfoEntryRegex.Matches(block)) {
                    string name = FirstNonEmpty(
                        match.Groups["bracket"].Value,
                        match.Groups["double"].Value,
                        match.Groups["single"].Value);
                    if (!TryParseInt(match.Groups["x"].Value, out int x) ||
                        !TryParseInt(match.Groups["y"].Value, out int y) ||
                        !TryParseInt(match.Groups["type"].Value, out int type)) {
                        continue;
                    }

                    markers.Add(new RoMinimapTownInfoMarker(name, x, y, type));
                }
            }

            foreach (Match match in AddTownInfoRegex.Matches(text)) {
                if (!string.Equals(NormalizeMapStem(match.Groups["map"].Value), normalizedMap, StringComparison.Ordinal)) {
                    continue;
                }

                string name = FirstNonEmpty(match.Groups["bracket"].Value, match.Groups["name"].Value);
                if (!TryParseInt(match.Groups["x"].Value, out int x) ||
                    !TryParseInt(match.Groups["y"].Value, out int y) ||
                    !TryParseInt(match.Groups["type"].Value, out int type)) {
                    continue;
                }

                markers.Add(new RoMinimapTownInfoMarker(name, x, y, type));
            }

            markers.Sort((left, right) => {
                int yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            });
            return markers.Count > 0;
        }

        private static bool TryExtractLuaMapBlock(string text, string mapStem, out string block) {
            block = string.Empty;
            Match match = Regex.Match(
                text,
                "(^|[,{])\\s*" + Regex.Escape(mapStem) + "\\s*=\\s*\\{",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (!match.Success) {
                return false;
            }

            int relativeOpen = match.Value.LastIndexOf('{');
            if (relativeOpen < 0) {
                return false;
            }

            int openIndex = match.Index + relativeOpen;
            return TryExtractBalancedBlock(text, openIndex, '{', '}', out block);
        }

        private static bool TryExtractBalancedBlock(
            string text,
            int openIndex,
            char openChar,
            char closeChar,
            out string block
        ) {
            block = string.Empty;
            int depth = 0;
            for (int i = openIndex; i < text.Length; i++) {
                char c = text[i];
                if (c == openChar) {
                    depth++;
                } else if (c == closeChar) {
                    depth--;
                    if (depth == 0) {
                        block = text.Substring(openIndex + 1, i - openIndex - 1);
                        return true;
                    }
                }
            }

            return false;
        }

        private static string FirstNonEmpty(params string[] values) {
            for (int i = 0; i < values.Length; i++) {
                if (!string.IsNullOrWhiteSpace(values[i])) {
                    return values[i];
                }
            }

            return string.Empty;
        }

        private static bool TryParseInt(string value, out int result) {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        /// <summary>"data\prontera.gat" / "Prontera.rsw" → "prontera".</summary>
        public static string NormalizeMapStem(string mapName) {
            if (string.IsNullOrWhiteSpace(mapName)) {
                return string.Empty;
            }

            string value = mapName.Trim().Replace('\\', '/');
            int slash = value.LastIndexOf('/');
            if (slash >= 0 && slash < value.Length - 1) {
                value = value.Substring(slash + 1);
            }

            int dot = value.LastIndexOf('.');
            if (dot > 0) {
                value = value.Substring(0, dot);
            }

            return value.Trim().ToLowerInvariant();
        }
    }
}
