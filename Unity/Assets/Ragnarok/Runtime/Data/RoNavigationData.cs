using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public enum RoNavigationEntryKind {
        Map,
        Mob,
        Npc
    }

    public sealed class RoNavigationCatalog {
        public static readonly RoNavigationCatalog Empty = new RoNavigationCatalog(
            Array.Empty<RoNavigationMapEntry>(),
            Array.Empty<RoNavigationMobEntry>(),
            Array.Empty<RoNavigationNpcEntry>(),
            Array.Empty<RoNavigationLinkEntry>(),
            Array.Empty<string>());

        private readonly Dictionary<string, RoNavigationMapEntry> mapsById =
            new Dictionary<string, RoNavigationMapEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<RoNavigationLinkEntry>> linksBySourceMapId =
            new Dictionary<string, List<RoNavigationLinkEntry>>(StringComparer.OrdinalIgnoreCase);

        public RoNavigationCatalog(
            IReadOnlyList<RoNavigationMapEntry> maps,
            IReadOnlyList<RoNavigationMobEntry> mobs,
            IReadOnlyList<string> diagnostics
        ) : this(maps, mobs, Array.Empty<RoNavigationNpcEntry>(), Array.Empty<RoNavigationLinkEntry>(), diagnostics) {
        }

        public RoNavigationCatalog(
            IReadOnlyList<RoNavigationMapEntry> maps,
            IReadOnlyList<RoNavigationMobEntry> mobs,
            IReadOnlyList<RoNavigationNpcEntry> npcs,
            IReadOnlyList<string> diagnostics
        ) : this(maps, mobs, npcs, Array.Empty<RoNavigationLinkEntry>(), diagnostics) {
        }

        public RoNavigationCatalog(
            IReadOnlyList<RoNavigationMapEntry> maps,
            IReadOnlyList<RoNavigationMobEntry> mobs,
            IReadOnlyList<RoNavigationLinkEntry> links,
            IReadOnlyList<string> diagnostics
        ) : this(maps, mobs, Array.Empty<RoNavigationNpcEntry>(), links, diagnostics) {
        }

        public RoNavigationCatalog(
            IReadOnlyList<RoNavigationMapEntry> maps,
            IReadOnlyList<RoNavigationMobEntry> mobs,
            IReadOnlyList<RoNavigationNpcEntry> npcs,
            IReadOnlyList<RoNavigationLinkEntry> links,
            IReadOnlyList<string> diagnostics
        ) {
            Maps = maps ?? Array.Empty<RoNavigationMapEntry>();
            Mobs = mobs ?? Array.Empty<RoNavigationMobEntry>();
            Npcs = npcs ?? Array.Empty<RoNavigationNpcEntry>();
            Links = links ?? Array.Empty<RoNavigationLinkEntry>();
            Diagnostics = diagnostics ?? Array.Empty<string>();
            for (int i = 0; i < Maps.Count; i++) {
                RoNavigationMapEntry map = Maps[i];
                if (map != null && !string.IsNullOrWhiteSpace(map.MapId)) {
                    mapsById[map.MapId] = map;
                }
            }

            for (int i = 0; i < Links.Count; i++) {
                RoNavigationLinkEntry link = Links[i];
                if (link == null || string.IsNullOrWhiteSpace(link.SourceMapId) || string.IsNullOrWhiteSpace(link.TargetMapId)) {
                    continue;
                }

                if (!linksBySourceMapId.TryGetValue(link.SourceMapId, out List<RoNavigationLinkEntry> bucket)) {
                    bucket = new List<RoNavigationLinkEntry>();
                    linksBySourceMapId.Add(link.SourceMapId, bucket);
                }

                bucket.Add(link);
            }
        }

        public IReadOnlyList<RoNavigationMapEntry> Maps { get; }
        public IReadOnlyList<RoNavigationMobEntry> Mobs { get; }
        public IReadOnlyList<RoNavigationNpcEntry> Npcs { get; }
        public IReadOnlyList<RoNavigationLinkEntry> Links { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool HasEntries => Maps.Count > 0 || Mobs.Count > 0 || Npcs.Count > 0;

        public bool TryGetMap(string mapId, out RoNavigationMapEntry map) {
            string normalized = NormalizeMapId(mapId);
            return mapsById.TryGetValue(normalized, out map);
        }

        public bool TryFindMapRoute(string sourceMapId, string targetMapId, out RoNavigationRoute route) {
            return TryFindMapRoute(sourceMapId, targetMapId, includeServiceLinks: true, out route);
        }

        public bool TryFindMapRoute(
            string sourceMapId,
            string targetMapId,
            bool includeServiceLinks,
            out RoNavigationRoute route
        ) {
            return TryFindMapRoute(
                sourceMapId,
                targetMapId,
                includeServiceLinks,
                hasSourceCell: false,
                sourceX: 0,
                sourceY: 0,
                out route);
        }

        public bool TryFindMapRoute(
            string sourceMapId,
            string targetMapId,
            bool includeServiceLinks,
            int sourceX,
            int sourceY,
            out RoNavigationRoute route
        ) {
            return TryFindMapRoute(
                sourceMapId,
                targetMapId,
                includeServiceLinks,
                hasSourceCell: true,
                sourceX: Math.Max(0, sourceX),
                sourceY: Math.Max(0, sourceY),
                out route);
        }

        private bool TryFindMapRoute(
            string sourceMapId,
            string targetMapId,
            bool includeServiceLinks,
            bool hasSourceCell,
            int sourceX,
            int sourceY,
            out RoNavigationRoute route
        ) {
            string source = NormalizeMapId(sourceMapId);
            string target = NormalizeMapId(targetMapId);
            route = null;
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) {
                return false;
            }

            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) {
                route = new RoNavigationRoute(source, target, Array.Empty<RoNavigationLinkEntry>());
                return true;
            }

            if (linksBySourceMapId.Count == 0) {
                return false;
            }

            var open = new List<string> {
                source
            };
            var distances = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {
                [source] = 0
            };
            var previous = new Dictionary<string, RoNavigationLinkEntry>(StringComparer.OrdinalIgnoreCase);
            var closed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (open.Count > 0) {
                int bestOpenIndex = FindBestOpenIndex(open, distances);
                string current = open[bestOpenIndex];
                open.RemoveAt(bestOpenIndex);
                if (!closed.Add(current)) {
                    continue;
                }

                if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase)) {
                    route = BuildRoute(source, target, previous);
                    return route != null;
                }

                if (!linksBySourceMapId.TryGetValue(current, out List<RoNavigationLinkEntry> outgoing)) {
                    continue;
                }

                int currentDistance = distances[current];
                for (int i = 0; i < outgoing.Count; i++) {
                    RoNavigationLinkEntry link = outgoing[i];
                    if (!includeServiceLinks && link.IsServiceLink) {
                        continue;
                    }

                    string next = link.TargetMapId;
                    if (string.IsNullOrWhiteSpace(next) || closed.Contains(next)) {
                        continue;
                    }

                    int candidateDistance =
                        currentDistance +
                        EstimateApproachCost(current, source, hasSourceCell, sourceX, sourceY, previous, link) +
                        Math.Max(1, link.Cost);
                    if (distances.TryGetValue(next, out int existingDistance) && existingDistance <= candidateDistance) {
                        continue;
                    }

                    distances[next] = candidateDistance;
                    previous[next] = link;
                    if (!open.Contains(next)) {
                        open.Add(next);
                    }
                }
            }

            return false;
        }

        private static int EstimateApproachCost(
            string currentMapId,
            string sourceMapId,
            bool hasSourceCell,
            int sourceX,
            int sourceY,
            Dictionary<string, RoNavigationLinkEntry> previous,
            RoNavigationLinkEntry outgoing
        ) {
            if (outgoing == null) {
                return 0;
            }

            int originX;
            int originY;
            if (hasSourceCell && string.Equals(currentMapId, sourceMapId, StringComparison.OrdinalIgnoreCase)) {
                originX = sourceX;
                originY = sourceY;
            } else if (previous != null && previous.TryGetValue(currentMapId, out RoNavigationLinkEntry incoming)) {
                originX = incoming.TargetX;
                originY = incoming.TargetY;
            } else {
                return 0;
            }

            return Math.Abs(outgoing.SourceX - originX) + Math.Abs(outgoing.SourceY - originY);
        }

        private static int FindBestOpenIndex(List<string> open, Dictionary<string, int> distances) {
            int bestIndex = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < open.Count; i++) {
                string mapId = open[i];
                if (!distances.TryGetValue(mapId, out int distance)) {
                    distance = int.MaxValue;
                }

                if (distance < bestDistance) {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static RoNavigationRoute BuildRoute(
            string sourceMapId,
            string targetMapId,
            Dictionary<string, RoNavigationLinkEntry> previous
        ) {
            var reversed = new List<RoNavigationLinkEntry>();
            string cursor = targetMapId;
            int guard = Math.Max(1, previous.Count + 1);
            while (!string.Equals(cursor, sourceMapId, StringComparison.OrdinalIgnoreCase) && guard-- > 0) {
                if (!previous.TryGetValue(cursor, out RoNavigationLinkEntry link)) {
                    return null;
                }

                reversed.Add(link);
                cursor = link.SourceMapId;
            }

            if (!string.Equals(cursor, sourceMapId, StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            reversed.Reverse();
            return new RoNavigationRoute(sourceMapId, targetMapId, reversed);
        }

        public IReadOnlyList<RoNavigationSearchResult> Search(string query, string filter, int limit = 50) {
            string normalizedQuery = NormalizeSearch(query);
            if (string.IsNullOrEmpty(normalizedQuery)) {
                return Array.Empty<RoNavigationSearchResult>();
            }

            bool includeMaps = AllowsKind(filter, RoNavigationEntryKind.Map);
            bool includeMobs = AllowsKind(filter, RoNavigationEntryKind.Mob);
            bool includeNpcs = AllowsKind(filter, RoNavigationEntryKind.Npc);
            var results = new List<RoNavigationSearchResult>();

            if (includeMaps) {
                for (int i = 0; i < Maps.Count; i++) {
                    RoNavigationMapEntry map = Maps[i];
                    if (map != null && MatchesMap(map, normalizedQuery)) {
                        results.Add(RoNavigationSearchResult.FromMap(map));
                    }
                }
            }

            if (includeMobs) {
                for (int i = 0; i < Mobs.Count; i++) {
                    RoNavigationMobEntry mob = Mobs[i];
                    if (mob != null && MatchesMob(mob, normalizedQuery)) {
                        results.Add(RoNavigationSearchResult.FromMob(mob));
                    }
                }
            }

            if (includeNpcs) {
                for (int i = 0; i < Npcs.Count; i++) {
                    RoNavigationNpcEntry npc = Npcs[i];
                    if (npc != null && MatchesNpc(npc, normalizedQuery)) {
                        results.Add(RoNavigationSearchResult.FromNpc(npc));
                    }
                }
            }

            results.Sort(CompareSearchResults);
            if (limit > 0 && results.Count > limit) {
                results.RemoveRange(limit, results.Count - limit);
            }

            return results;
        }

        private static bool AllowsKind(string filter, RoNavigationEntryKind kind) {
            if (string.IsNullOrWhiteSpace(filter) ||
                string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return string.Equals(filter, kind.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesMap(RoNavigationMapEntry map, string normalizedQuery) {
            return map.MatchesSearch(normalizedQuery);
        }

        private static bool MatchesMob(RoNavigationMobEntry mob, string normalizedQuery) {
            return mob.MatchesSearch(normalizedQuery);
        }

        private static bool MatchesNpc(RoNavigationNpcEntry npc, string normalizedQuery) {
            return npc.MatchesSearch(normalizedQuery);
        }

        private static int CompareSearchResults(RoNavigationSearchResult left, RoNavigationSearchResult right) {
            if (left == null && right == null) {
                return 0;
            }

            if (left == null) {
                return 1;
            }

            if (right == null) {
                return -1;
            }

            int kind = left.Kind.CompareTo(right.Kind);
            return kind != 0 ? kind : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        internal static string NormalizeSearch(string value) {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        internal static string NormalizeMapId(string value) {
            return RoWorldMapDataUtility.NormalizeMapId(value);
        }
    }

    public sealed class RoNavigationSearchResult {
        private RoNavigationSearchResult(RoNavigationEntryKind kind, RoNavigationMapEntry map, RoNavigationMobEntry mob, RoNavigationNpcEntry npc) {
            Kind = kind;
            Map = map;
            Mob = mob;
            Npc = npc;
        }

        public RoNavigationEntryKind Kind { get; }
        public RoNavigationMapEntry Map { get; }
        public RoNavigationMobEntry Mob { get; }
        public RoNavigationNpcEntry Npc { get; }
        public string DisplayName {
            get {
                switch (Kind) {
                    case RoNavigationEntryKind.Map:
                        return Map?.MapId ?? string.Empty;
                    case RoNavigationEntryKind.Npc:
                        return Npc?.DisplayName ?? string.Empty;
                    default:
                        return Mob?.DisplayName ?? string.Empty;
                }
            }
        }

        public int Count {
            get {
                switch (Kind) {
                    case RoNavigationEntryKind.Map:
                        return Map?.Mobs.Count ?? 0;
                    case RoNavigationEntryKind.Npc:
                        return Npc?.NpcId ?? 0;
                    default:
                        return Mob?.Occurrences.Count ?? 0;
                }
            }
        }

        public static RoNavigationSearchResult FromMap(RoNavigationMapEntry map) {
            return new RoNavigationSearchResult(RoNavigationEntryKind.Map, map, null, null);
        }

        public static RoNavigationSearchResult FromMob(RoNavigationMobEntry mob) {
            return new RoNavigationSearchResult(RoNavigationEntryKind.Mob, null, mob, null);
        }

        public static RoNavigationSearchResult FromNpc(RoNavigationNpcEntry npc) {
            return new RoNavigationSearchResult(RoNavigationEntryKind.Npc, null, null, npc);
        }
    }

    public sealed class RoNavigationRoute {
        public RoNavigationRoute(string sourceMapId, string targetMapId, IReadOnlyList<RoNavigationLinkEntry> links) {
            SourceMapId = RoNavigationCatalog.NormalizeMapId(sourceMapId);
            TargetMapId = RoNavigationCatalog.NormalizeMapId(targetMapId);
            Links = links ?? Array.Empty<RoNavigationLinkEntry>();
        }

        public string SourceMapId { get; }
        public string TargetMapId { get; }
        public IReadOnlyList<RoNavigationLinkEntry> Links { get; }
        public bool IsSameMap => Links.Count == 0;
        public RoNavigationLinkEntry FirstLink => Links.Count > 0 ? Links[0] : null;
        public RoNavigationLinkEntry LastLink => Links.Count > 0 ? Links[Links.Count - 1] : null;
    }

    public sealed class RoNavigationLinkEntry {
        public RoNavigationLinkEntry(
            string sourceMapId,
            int sourceX,
            int sourceY,
            string targetMapId,
            int targetX,
            int targetY,
            string label,
            string linkType,
            int cost,
            bool isServiceLink = false
        ) {
            SourceMapId = RoNavigationCatalog.NormalizeMapId(sourceMapId);
            SourceX = Math.Max(0, sourceX);
            SourceY = Math.Max(0, sourceY);
            TargetMapId = RoNavigationCatalog.NormalizeMapId(targetMapId);
            TargetX = Math.Max(0, targetX);
            TargetY = Math.Max(0, targetY);
            Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
            LinkType = string.IsNullOrWhiteSpace(linkType) ? string.Empty : linkType.Trim();
            Cost = Math.Max(1, cost);
            IsServiceLink = isServiceLink;
        }

        public string SourceMapId { get; }
        public int SourceX { get; }
        public int SourceY { get; }
        public string TargetMapId { get; }
        public int TargetX { get; }
        public int TargetY { get; }
        public string Label { get; }
        public string LinkType { get; }
        public int Cost { get; }
        public bool IsServiceLink { get; }
    }

    public sealed class RoNavigationMapEntry {
        private readonly List<RoNavigationMobEntry> mobs = new List<RoNavigationMobEntry>();
        private readonly List<RoNavigationNpcEntry> npcs = new List<RoNavigationNpcEntry>();
        private readonly string normalizedMapId;
        private readonly string normalizedDisplayName;

        public RoNavigationMapEntry(string mapId, string displayName, int width, int height, IReadOnlyList<RoNavigationMobEntry> mapMobs = null) {
            MapId = RoNavigationCatalog.NormalizeMapId(mapId);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? MapId : displayName.Trim();
            normalizedMapId = RoNavigationCatalog.NormalizeSearch(MapId);
            normalizedDisplayName = RoNavigationCatalog.NormalizeSearch(DisplayName);
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
            if (mapMobs != null) {
                mobs.AddRange(mapMobs);
            }
        }

        public string MapId { get; }
        public string DisplayName { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<RoNavigationMobEntry> Mobs => mobs;
        public IReadOnlyList<RoNavigationNpcEntry> Npcs => npcs;

        internal void AddMob(RoNavigationMobEntry mob) {
            if (mob != null && !mobs.Contains(mob)) {
                mobs.Add(mob);
            }
        }

        internal void AddNpc(RoNavigationNpcEntry npc) {
            if (npc != null && !npcs.Contains(npc)) {
                npcs.Add(npc);
            }
        }

        internal bool MatchesSearch(string normalizedQuery) {
            return normalizedMapId.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0 ||
                normalizedDisplayName.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0;
        }
    }

    public sealed class RoNavigationNpcEntry {
        private readonly string normalizedMapId;
        private readonly string normalizedDisplayName;
        private readonly string normalizedDetailName;
        private readonly string normalizedNpcId;

        public RoNavigationNpcEntry(
            string mapId,
            int npcId,
            int npcType,
            int spriteId,
            string displayName,
            string detailName,
            int x,
            int y
        ) {
            MapId = RoNavigationCatalog.NormalizeMapId(mapId);
            NpcId = Math.Max(0, npcId);
            NpcType = Math.Max(0, npcType);
            SpriteId = Math.Max(0, spriteId);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "NPC" : displayName.Trim();
            DetailName = string.IsNullOrWhiteSpace(detailName) ? MapId : detailName.Trim();
            X = Math.Max(0, x);
            Y = Math.Max(0, y);
            normalizedMapId = RoNavigationCatalog.NormalizeSearch(MapId);
            normalizedDisplayName = RoNavigationCatalog.NormalizeSearch(DisplayName);
            normalizedDetailName = RoNavigationCatalog.NormalizeSearch(DetailName);
            normalizedNpcId = NpcId.ToString(CultureInfo.InvariantCulture);
        }

        public string MapId { get; }
        public int NpcId { get; }
        public int NpcType { get; }
        public int SpriteId { get; }
        public string DisplayName { get; }
        public string DetailName { get; }
        public int X { get; }
        public int Y { get; }

        internal bool MatchesSearch(string normalizedQuery) {
            return normalizedDisplayName.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0 ||
                normalizedDetailName.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0 ||
                normalizedMapId.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0 ||
                normalizedNpcId.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0;
        }
    }

    public sealed class RoNavigationMobEntry {
        private readonly List<RoNavigationMobOccurrence> occurrences = new List<RoNavigationMobOccurrence>();
        private readonly string normalizedAegisName;
        private readonly string normalizedLocalizedName;
        private readonly string normalizedDisplayName;

        public RoNavigationMobEntry(
            int classId,
            string aegisName,
            string localizedName,
            string displayName,
            int level,
            int race,
            int size,
            int element,
            int elementLevel,
            IReadOnlyList<RoNavigationMobOccurrence> mapOccurrences = null
        ) {
            ClassId = Math.Max(0, classId);
            AegisName = NormalizeText(aegisName);
            LocalizedName = NormalizeText(localizedName);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? FormatAegisName(AegisName) : displayName.Trim();
            normalizedAegisName = RoNavigationCatalog.NormalizeSearch(AegisName);
            normalizedLocalizedName = RoNavigationCatalog.NormalizeSearch(LocalizedName);
            normalizedDisplayName = RoNavigationCatalog.NormalizeSearch(DisplayName);
            Level = Math.Max(0, level);
            Race = race;
            Size = size;
            Element = element;
            ElementLevel = Math.Max(0, elementLevel);
            if (mapOccurrences != null) {
                occurrences.AddRange(mapOccurrences);
            }
        }

        public int ClassId { get; }
        public string AegisName { get; }
        public string LocalizedName { get; }
        public string DisplayName { get; }
        public int Level { get; }
        public int Race { get; }
        public int Size { get; }
        public int Element { get; }
        public int ElementLevel { get; }
        public IReadOnlyList<RoNavigationMobOccurrence> Occurrences => occurrences;
        public string RaceName => FormatRace(Race);
        public string SizeName => FormatSize(Size);
        public string ElementName => FormatElement(Element);

        public string BuildSpriteBasePath() {
            if (ClassId <= 0 || string.IsNullOrWhiteSpace(AegisName)) {
                return string.Empty;
            }

            return new RoEntityDisplayMetadata(
                ClassId,
                RoEntityDisplayKind.Monster,
                AegisName,
                DisplayName,
                AegisName,
                Level).GetSpriteBasePath();
        }

        internal void AddOccurrence(RoNavigationMobOccurrence occurrence) {
            if (occurrence != null) {
                occurrences.Add(occurrence);
            }
        }

        internal bool MatchesSearch(string normalizedQuery) {
            return normalizedDisplayName.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0 ||
                normalizedAegisName.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0 ||
                normalizedLocalizedName.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0;
        }

        private static string FormatRace(int race) {
            switch (race) {
                case 0: return "Formless";
                case 1: return "Undead";
                case 2: return "Brute";
                case 3: return "Plant";
                case 4: return "Insect";
                case 5: return "Fish";
                case 6: return "Demon";
                case 7: return "Demi-Human";
                case 8: return "Angel";
                case 9: return "Dragon";
                case 10: return "Player";
                default: return "Unknown";
            }
        }

        private static string FormatSize(int size) {
            switch (size) {
                case 0: return "Small";
                case 1: return "Medium";
                case 2: return "Large";
                default: return "Unknown";
            }
        }

        private static string FormatElement(int element) {
            switch (element) {
                case 0: return "Neutral";
                case 1: return "Water";
                case 2: return "Earth";
                case 3: return "Fire";
                case 4: return "Wind";
                case 5: return "Poison";
                case 6: return "Holy";
                case 7: return "Shadow";
                case 8: return "Ghost";
                case 9: return "Undead";
                default: return "Unknown";
            }
        }

        private static string NormalizeText(string value) {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        internal static string FormatAegisName(string aegisName) {
            if (string.IsNullOrWhiteSpace(aegisName)) {
                return string.Empty;
            }

            string value = aegisName.Trim();
            int prefix = value.IndexOf('_');
            if (prefix == 2 && value.Length > 3 && value[0] == 'C' && char.IsDigit(value[1])) {
                value = value.Substring(3);
            }

            string[] pieces = value.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pieces.Length; i++) {
                string piece = pieces[i].ToLowerInvariant();
                pieces[i] = piece.Length == 0
                    ? string.Empty
                    : char.ToUpperInvariant(piece[0]) + piece.Substring(1);
            }

            return string.Join(" ", pieces);
        }
    }

    public sealed class RoNavigationMobOccurrence {
        public RoNavigationMobOccurrence(string mapId, int spawnCount) {
            MapId = RoNavigationCatalog.NormalizeMapId(mapId);
            SpawnCount = Math.Max(0, spawnCount);
        }

        public string MapId { get; }
        public int SpawnCount { get; }
        public string SpawnLabel => FormatSpawnLabel(SpawnCount);

        private static string FormatSpawnLabel(int spawnCount) {
            if (spawnCount <= 0) {
                return "Unknown";
            }

            if (spawnCount <= 5) {
                return "Very Low";
            }

            if (spawnCount <= 15) {
                return "Low";
            }

            if (spawnCount <= 40) {
                return "Average";
            }

            return "High";
        }
    }

    public static class RoNavigationDataLoader {
        private static readonly ConditionalWeakTable<RoAssetDatabase, CacheEntry> Cache =
            new ConditionalWeakTable<RoAssetDatabase, CacheEntry>();

        public static readonly string[] DataSourceRelativePaths = {
            "data/luafiles514/lua files/navigation/navi_scroll_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_f_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_linkdistance_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_link_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_map_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_mob_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_npcdistance_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_npc_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_picknpc_krpri.lub"
        };

        public static RoNavigationCatalog Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return RoNavigationCatalog.Empty;
            }

            return Cache.GetValue(assets, key => new CacheEntry(LoadUncached(key))).Catalog;
        }

        public static RoNavigationCatalog ParseFilesForTests(IReadOnlyList<RoWorldMapLuaFile> files) {
            return BuildCatalog(RoWorldMapLuaParser.ParseContext(files), Array.Empty<string>());
        }

        private static RoNavigationCatalog LoadUncached(RoAssetDatabase assets) {
            var files = new List<RoWorldMapLuaFile>();
            var diagnostics = new List<string>();
            TryLoadLogicalFile(assets, "navi_map_krpri", files, diagnostics);
            TryLoadLogicalFile(assets, "navi_mob_krpri", files, diagnostics);
            TryLoadLogicalFile(assets, "navi_npc_krpri", files, diagnostics);
            TryLoadLogicalFile(assets, "navi_npcdistance_krpri", files, diagnostics);
            TryLoadLogicalFile(assets, "navi_picknpc_krpri", files, diagnostics);
            TryLoadLogicalFile(assets, "navi_link_krpri", files, diagnostics);
            TryLoadLogicalFile(assets, "navi_linkdistance_krpri", files, diagnostics);

            if (files.Count == 0) {
                diagnostics.Add("No original navigation map/mob/NPC LUB files were found in the active RO content profile.");
                return new RoNavigationCatalog(
                    Array.Empty<RoNavigationMapEntry>(),
                    Array.Empty<RoNavigationMobEntry>(),
                    Array.Empty<RoNavigationNpcEntry>(),
                    Array.Empty<RoNavigationLinkEntry>(),
                    diagnostics);
            }

            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(files);
            diagnostics.AddRange(context.Diagnostics);
            return BuildCatalog(context, diagnostics);
        }

        private static RoNavigationCatalog BuildCatalog(RoWorldMapLuaExecutionContext context, IReadOnlyList<string> diagnostics) {
            var mutableDiagnostics = new List<string>(diagnostics ?? Array.Empty<string>());
            var maps = new List<RoNavigationMapEntry>();
            var mapsById = new Dictionary<string, RoNavigationMapEntry>(StringComparer.OrdinalIgnoreCase);
            BuildMaps(context.GetGlobal("Navi_Map"), maps, mapsById);

            var mobs = new List<RoNavigationMobEntry>();
            BuildMobs(context.GetGlobal("Navi_Mob"), maps, mapsById, mobs);

            var npcs = new List<RoNavigationNpcEntry>();
            BuildNpcs(FindNavigationTable(context, "Navi_Npc", "Navi_NPC"), maps, mapsById, npcs);
            BuildNpcs(FindNavigationTable(context, "Navi_PickNpc", "Navi_PickNPC", "Navi_Pick_Npc"), maps, mapsById, npcs);

            Dictionary<string, int> linkDistances = BuildLinkDistances(FindNavigationTable(context, "Navi_LinkDistance", "Navi_Link_Distance", "Navi_Distance"));
            var links = new List<RoNavigationLinkEntry>();
            BuildLinks(FindNavigationTable(context, "Navi_Link"), mapsById, linkDistances, links);

            if (maps.Count == 0) {
                mutableDiagnostics.Add("Navi_Map parsed, but no map rows were recognized.");
            }

            if (mobs.Count == 0) {
                mutableDiagnostics.Add("Navi_Mob parsed, but no monster rows were recognized.");
            }

            if (npcs.Count == 0) {
                mutableDiagnostics.Add("Navi_Npc parsed, but no NPC rows were recognized.");
            }

            if (links.Count == 0) {
                mutableDiagnostics.Add("Navi_Link parsed, but no route links were recognized.");
            }

            return new RoNavigationCatalog(maps, mobs, npcs, links, mutableDiagnostics);
        }

        private static void BuildMaps(
            LuaValue value,
            List<RoNavigationMapEntry> maps,
            Dictionary<string, RoNavigationMapEntry> mapsById
        ) {
            if (value == null || value.Kind != LuaValueKind.Table) {
                return;
            }

            for (int i = 1; i <= value.Table.ArrayCount; i++) {
                LuaValue rowValue = value.Table.Get(i);
                if (rowValue.Kind != LuaValueKind.Table) {
                    continue;
                }

                LuaTableValue row = rowValue.Table;
                string mapId = ReadString(row, 1);
                if (string.IsNullOrWhiteSpace(mapId)) {
                    continue;
                }

                RoNavigationMapEntry map = new RoNavigationMapEntry(
                    mapId,
                    ReadString(row, 2),
                    ReadInt(row, 4),
                    ReadInt(row, 5));
                maps.Add(map);
                mapsById[map.MapId] = map;
            }
        }

        private static void BuildMobs(
            LuaValue value,
            List<RoNavigationMapEntry> maps,
            Dictionary<string, RoNavigationMapEntry> mapsById,
            List<RoNavigationMobEntry> mobs
        ) {
            if (value == null || value.Kind != LuaValueKind.Table) {
                return;
            }

            var byKey = new Dictionary<string, RoNavigationMobEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i <= value.Table.ArrayCount; i++) {
                LuaValue rowValue = value.Table.Get(i);
                if (rowValue.Kind != LuaValueKind.Table) {
                    continue;
                }

                LuaTableValue row = rowValue.Table;
                string mapId = RoNavigationCatalog.NormalizeMapId(ReadString(row, 1));
                string aegisName = ReadString(row, 6);
                if (string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(aegisName)) {
                    continue;
                }

                int packedSpawn = ReadInt(row, 4);
                int classId = packedSpawn & 0xffff;
                int spawnCount = (packedSpawn >> 16) & 0xffff;
                int attributes = ReadInt(row, 8);
                int race = attributes & 0xff;
                int size = (attributes >> 8) & 0xff;
                int elementPacked = (attributes >> 16) & 0xffff;
                int element = elementPacked % 20;
                int elementLevel = elementPacked / 20;
                string key = classId > 0 ? classId.ToString(CultureInfo.InvariantCulture) : aegisName;
                if (!byKey.TryGetValue(key, out RoNavigationMobEntry mob)) {
                    mob = new RoNavigationMobEntry(
                        classId,
                        aegisName,
                        ReadString(row, 5),
                        RoNavigationMobEntry.FormatAegisName(aegisName),
                        ReadInt(row, 7),
                        race,
                        size,
                        element,
                        elementLevel);
                    byKey.Add(key, mob);
                    mobs.Add(mob);
                }

                var occurrence = new RoNavigationMobOccurrence(mapId, spawnCount);
                mob.AddOccurrence(occurrence);
                RoNavigationMapEntry map = EnsureMap(mapId, maps, mapsById);
                map.AddMob(mob);
            }
        }

        private static void BuildNpcs(
            LuaValue value,
            List<RoNavigationMapEntry> maps,
            Dictionary<string, RoNavigationMapEntry> mapsById,
            List<RoNavigationNpcEntry> npcs
        ) {
            if (value == null || value.Kind != LuaValueKind.Table) {
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < npcs.Count; i++) {
                RoNavigationNpcEntry existing = npcs[i];
                if (existing != null) {
                    seen.Add(BuildNpcKey(existing.MapId, existing.NpcId, existing.X, existing.Y));
                }
            }

            for (int i = 1; i <= value.Table.ArrayCount; i++) {
                LuaValue rowValue = value.Table.Get(i);
                if (rowValue.Kind != LuaValueKind.Table) {
                    continue;
                }

                LuaTableValue row = rowValue.Table;
                string mapId = RoNavigationCatalog.NormalizeMapId(ReadString(row, 1));
                string displayName = ReadString(row, 5);
                int npcId = ReadInt(row, 2);
                int x = ReadInt(row, 7);
                int y = ReadInt(row, 8);
                if (string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(displayName) || x <= 0 || y <= 0) {
                    continue;
                }

                string key = BuildNpcKey(mapId, npcId, x, y);
                if (!seen.Add(key)) {
                    continue;
                }

                var npc = new RoNavigationNpcEntry(
                    mapId,
                    npcId,
                    ReadInt(row, 3),
                    ReadInt(row, 4),
                    displayName,
                    ReadString(row, 6),
                    x,
                    y);
                npcs.Add(npc);
                EnsureMap(mapId, maps, mapsById).AddNpc(npc);
            }
        }

        private static string BuildNpcKey(string mapId, int npcId, int x, int y) {
            return RoNavigationCatalog.NormalizeMapId(mapId) +
                "\n" +
                Math.Max(0, npcId).ToString(CultureInfo.InvariantCulture) +
                "\n" +
                Math.Max(0, x).ToString(CultureInfo.InvariantCulture) +
                "\n" +
                Math.Max(0, y).ToString(CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, int> BuildLinkDistances(LuaValue value) {
            var distances = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (value == null || value.Kind != LuaValueKind.Table) {
                return distances;
            }

            for (int i = 1; i <= value.Table.ArrayCount; i++) {
                LuaValue rowValue = value.Table.Get(i);
                if (rowValue.Kind != LuaValueKind.Table) {
                    continue;
                }

                ExtractStringsAndNumbers(rowValue.Table, out List<string> strings, out List<int> numbers);
                if (strings.Count < 2 || numbers.Count == 0) {
                    continue;
                }

                string source = RoNavigationCatalog.NormalizeMapId(strings[0]);
                string target = RoNavigationCatalog.NormalizeMapId(strings[1]);
                int cost = Math.Max(1, numbers[numbers.Count - 1]);
                if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target)) {
                    distances[BuildDistanceKey(source, target)] = cost;
                }
            }

            return distances;
        }

        private static void BuildLinks(
            LuaValue value,
            Dictionary<string, RoNavigationMapEntry> mapsById,
            Dictionary<string, int> linkDistances,
            List<RoNavigationLinkEntry> links
        ) {
            if (value == null || value.Kind != LuaValueKind.Table) {
                return;
            }

            for (int i = 1; i <= value.Table.ArrayCount; i++) {
                LuaValue rowValue = value.Table.Get(i);
                if (rowValue.Kind != LuaValueKind.Table) {
                    continue;
                }

                LuaTableValue row = rowValue.Table;
                string sourceMapId = RoNavigationCatalog.NormalizeMapId(ReadString(row, 1));
                string targetMapId = RoNavigationCatalog.NormalizeMapId(ReadString(row, 9));
                if (string.IsNullOrWhiteSpace(sourceMapId) || string.IsNullOrWhiteSpace(targetMapId)) {
                    continue;
                }

                int linkKind = ReadInt(row, 3);
                int sourceX = NormalizeLinkCoordinate(ReadInt(row, 7), mapsById, sourceMapId, xAxis: true);
                int sourceY = NormalizeLinkCoordinate(ReadInt(row, 8), mapsById, sourceMapId, xAxis: false);
                int targetX = NormalizeLinkCoordinate(ReadInt(row, 10), mapsById, targetMapId, xAxis: true);
                int targetY = NormalizeLinkCoordinate(ReadInt(row, 11), mapsById, targetMapId, xAxis: false);
                int cost = ResolveLinkCost(sourceMapId, targetMapId, sourceX, sourceY, targetX, targetY, linkDistances);
                string linkType = ReadString(row, 6);
                bool isServiceLink = linkKind != 200 || !string.IsNullOrWhiteSpace(linkType);
                links.Add(new RoNavigationLinkEntry(
                    sourceMapId,
                    sourceX,
                    sourceY,
                    targetMapId,
                    targetX,
                    targetY,
                    ReadString(row, 5),
                    linkType,
                    cost,
                    isServiceLink));
            }
        }

        private static LuaValue FindNavigationTable(RoWorldMapLuaExecutionContext context, params string[] preferredNames) {
            if (context == null) {
                return LuaValue.Nil;
            }

            for (int i = 0; i < preferredNames.Length; i++) {
                LuaValue value = context.GetGlobal(preferredNames[i]);
                if (value.Kind == LuaValueKind.Table) {
                    return value;
                }

                value = context.GetGlobal(RoWorldMapLuaExecutionContext.NavigationRowsGlobalPrefix + preferredNames[i]);
                if (value.Kind == LuaValueKind.Table) {
                    return value;
                }
            }

            foreach (KeyValuePair<string, LuaValue> global in context.Globals) {
                if (global.Value.Kind != LuaValueKind.Table) {
                    continue;
                }

                for (int i = 0; i < preferredNames.Length; i++) {
                    if (string.Equals(global.Key, preferredNames[i], StringComparison.OrdinalIgnoreCase)) {
                        return global.Value;
                    }
                }
            }

            return LuaValue.Nil;
        }

        private static int ResolveLinkCost(
            string sourceMapId,
            string targetMapId,
            int sourceX,
            int sourceY,
            int targetX,
            int targetY,
            Dictionary<string, int> linkDistances
        ) {
            if (linkDistances != null &&
                linkDistances.TryGetValue(BuildDistanceKey(sourceMapId, targetMapId), out int cost)) {
                return cost;
            }

            return Math.Max(1, Math.Abs(targetX - sourceX) + Math.Abs(targetY - sourceY) + 100);
        }

        private static string BuildDistanceKey(string sourceMapId, string targetMapId) {
            return RoNavigationCatalog.NormalizeMapId(sourceMapId) + "\n" + RoNavigationCatalog.NormalizeMapId(targetMapId);
        }

        private static int NormalizeLinkCoordinate(
            int value,
            Dictionary<string, RoNavigationMapEntry> mapsById,
            string mapId,
            bool xAxis
        ) {
            int normalized = Math.Max(0, value);
            if (mapsById == null || !mapsById.TryGetValue(mapId, out RoNavigationMapEntry map)) {
                return normalized;
            }

            int limit = xAxis ? map.Width : map.Height;
            if (limit <= 0 || normalized < limit) {
                return normalized;
            }

            int packedCandidate = normalized / 10;
            if (packedCandidate > 0 && packedCandidate < limit) {
                return packedCandidate;
            }

            return normalized;
        }

        private static void ExtractStringsAndNumbers(
            LuaTableValue row,
            out List<string> strings,
            out List<int> numbers
        ) {
            strings = new List<string>();
            numbers = new List<int>();
            if (row == null) {
                return;
            }

            for (int i = 1; i <= row.ArrayCount; i++) {
                LuaValue value = row.Get(i);
                if (value.Kind == LuaValueKind.String) {
                    string text = value.Text;
                    if (!string.IsNullOrWhiteSpace(text)) {
                        strings.Add(text);
                    }
                } else if (value.Kind == LuaValueKind.Number) {
                    numbers.Add(value.ToInt());
                }
            }
        }

        private static RoNavigationMapEntry EnsureMap(
            string mapId,
            List<RoNavigationMapEntry> maps,
            Dictionary<string, RoNavigationMapEntry> mapsById
        ) {
            string normalized = RoNavigationCatalog.NormalizeMapId(mapId);
            if (mapsById.TryGetValue(normalized, out RoNavigationMapEntry map)) {
                return map;
            }

            map = new RoNavigationMapEntry(normalized, normalized, 0, 0);
            maps.Add(map);
            mapsById[normalized] = map;
            return map;
        }

        private static string ReadString(LuaTableValue row, int index) {
            LuaValue value = row != null ? row.Get(index) : LuaValue.Nil;
            return value.Kind == LuaValueKind.String ? value.Text : value.ToDisplayString();
        }

        private static int ReadInt(LuaTableValue row, int index) {
            LuaValue value = row != null ? row.Get(index) : LuaValue.Nil;
            return value.ToInt();
        }

        internal static void TryLoadLogicalFile(
            RoAssetDatabase assets,
            string logicalName,
            List<RoWorldMapLuaFile> files,
            List<string> diagnostics
        ) {
            string[] candidates = {
                "data/luafiles514/lua files/navigation/" + logicalName + ".lub",
                "data/LuaFiles514/Lua Files/navigation/" + logicalName + ".lub",
                "data/lua files/navigation/" + logicalName + ".lub"
            };

            for (int i = 0; i < candidates.Length; i++) {
                if (!assets.TryResolvePath(candidates[i], out string resolvedPath)) {
                    continue;
                }

                byte[] bytes = assets.ReadBytes(candidates[i]);
                if (bytes == null || bytes.Length == 0) {
                    continue;
                }

                files.Add(new RoWorldMapLuaFile(resolvedPath ?? candidates[i], bytes));
                return;
            }

            string suffix = "navigation/" + logicalName + ".lub";
            if (assets.TryFindFirstPathEndingWith(suffix, out string suffixPath)) {
                byte[] bytes = assets.ReadBytes(suffixPath);
                if (bytes != null && bytes.Length > 0) {
                    files.Add(new RoWorldMapLuaFile(suffixPath, bytes));
                    return;
                }
            }

            diagnostics.Add("Missing original navigation data file '" + logicalName + ".lub'.");
        }

        private sealed class CacheEntry {
            public CacheEntry(RoNavigationCatalog catalog) {
                Catalog = catalog ?? RoNavigationCatalog.Empty;
            }

            public RoNavigationCatalog Catalog { get; }
        }
    }
}
