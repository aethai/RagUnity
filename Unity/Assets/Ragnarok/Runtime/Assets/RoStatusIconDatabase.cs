using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Ragnarok.Assets {
    public readonly struct RoStatusIconDescriptionLine {
        public RoStatusIconDescriptionLine(string text, string colorKey = null) {
            Text = text ?? string.Empty;
            ColorKey = colorKey ?? string.Empty;
        }

        public string Text { get; }
        public string ColorKey { get; }
    }

    public sealed class RoStatusIconDefinition {
        public RoStatusIconDefinition(
            ushort efstId,
            string symbol,
            string iconName,
            bool haveTimeLimit,
            int posTimeLimitStr,
            RoStatusIconDescriptionLine[] descriptionLines) {
            EfstId = efstId;
            Symbol = symbol ?? string.Empty;
            IconName = iconName ?? string.Empty;
            HaveTimeLimit = haveTimeLimit;
            PosTimeLimitStr = posTimeLimitStr;
            DescriptionLines = descriptionLines ?? Array.Empty<RoStatusIconDescriptionLine>();
        }

        public ushort EfstId { get; }
        public string Symbol { get; }
        public string IconName { get; }
        public bool HaveTimeLimit { get; }
        public int PosTimeLimitStr { get; }
        public RoStatusIconDescriptionLine[] DescriptionLines { get; }
    }

    public sealed class RoStatusIconDatabase {
        private static readonly string[] EfstIdCandidatePaths = {
            "data/luafiles514/lua files/stateicon/efstids.lub",
            "data/luafiles514/lua files/stateicon/efstids.lua",
            "System/stateicon/efstids.lub",
            "System/LuaFiles514/Lua Files/stateicon/efstids.lub"
        };

        private static readonly string[] StateIconInfoCandidatePaths = {
            "data/luafiles514/lua files/stateicon/stateiconinfo.lub",
            "data/luafiles514/lua files/stateicon/stateiconinfo_f.lub",
            "System/stateicon/stateiconinfo.lub",
            "System/stateicon/stateiconinfo_f.lub",
            "System/LuaFiles514/Lua Files/stateicon/stateiconinfo.lub",
            "System/LuaFiles514/Lua Files/stateicon/stateiconinfo_f.lub"
        };

        private static readonly string[] StateIconImgInfoCandidatePaths = {
            "data/luafiles514/lua files/stateicon/stateiconimginfo.lub",
            "System/stateicon/stateiconimginfo.lub",
            "System/LuaFiles514/Lua Files/stateicon/stateiconimginfo.lub"
        };

        private readonly RoAssetDatabase assets;
        private readonly Dictionary<string, ushort> efstIdsBySymbol = new Dictionary<string, ushort>(StringComparer.Ordinal);
        private readonly Dictionary<ushort, string> symbolsByEfstId = new Dictionary<ushort, string>();
        private readonly Dictionary<ushort, RoStatusIconDefinition> definitionsByEfstId =
            new Dictionary<ushort, RoStatusIconDefinition>();
        private readonly Dictionary<ushort, KeyValuePair<int, string>> imagesByEfstId =
            new Dictionary<ushort, KeyValuePair<int, string>>();
        private bool isLoaded;

        public RoStatusIconDatabase(RoAssetDatabase assets) {
            this.assets = assets;
        }

        private RoStatusIconDatabase() {
        }

        public string LastLoadError { get; private set; } = string.Empty;

        public IReadOnlyDictionary<ushort, RoStatusIconDefinition> DefinitionsByEfstId {
            get {
                EnsureLoaded();
                return definitionsByEfstId;
            }
        }

        public static RoStatusIconDatabase FromText(
            string efstIdsText,
            string stateIconInfoText,
            string stateIconImgInfoText) {
            var database = new RoStatusIconDatabase();
            database.LoadDefinitions(efstIdsText, stateIconInfoText, stateIconImgInfoText);
            database.isLoaded = true;
            return database;
        }

        public void Preload() {
            EnsureLoaded();
        }

        public bool TryResolveDefinition(ushort efstId, out RoStatusIconDefinition definition) {
            EnsureLoaded();
            return definitionsByEfstId.TryGetValue(efstId, out definition);
        }

        /// <summary>
        /// StateIconImgList[priority][efst] (stateiconimginfo.lub; GOLD 0, RED 1, BLUE 2, GREEN 3, WHITE 4): the first
        /// priority that has the EFST and its file there, as GetEFSTImgFileName answers the client's priority loop.
        /// </summary>
        public bool TryResolveImage(ushort efstId, out int priority, out string fileName) {
            EnsureLoaded();
            if (imagesByEfstId.TryGetValue(efstId, out KeyValuePair<int, string> image)) {
                priority = image.Key;
                fileName = image.Value;
                return true;
            }

            priority = -1;
            fileName = null;
            return false;
        }

        public bool TryResolveEfstId(string symbol, out ushort efstId) {
            EnsureLoaded();
            return efstIdsBySymbol.TryGetValue(symbol ?? string.Empty, out efstId);
        }

        public bool TryResolveSymbol(ushort efstId, out string symbol) {
            EnsureLoaded();
            return symbolsByEfstId.TryGetValue(efstId, out symbol);
        }

        private void EnsureLoaded() {
            if (isLoaded) {
                return;
            }

            isLoaded = true;
            try {
                LoadDefinitions(
                    TryReadText("efstids.lub", EfstIdCandidatePaths, out string efstText) ? efstText : string.Empty,
                    TryReadText("stateiconinfo.lub", StateIconInfoCandidatePaths, out string infoText) ? infoText : string.Empty,
                    TryReadText("stateiconimginfo.lub", StateIconImgInfoCandidatePaths, out string imageText) ? imageText : string.Empty);
                LastLoadError = string.Empty;
            } catch (Exception exception) {
                efstIdsBySymbol.Clear();
                symbolsByEfstId.Clear();
                definitionsByEfstId.Clear();
                imagesByEfstId.Clear();
                LastLoadError = exception.Message;
            }
        }

        private void LoadDefinitions(string efstIdText, string stateIconInfoText, string stateIconImgInfoText) {
            efstIdsBySymbol.Clear();
            symbolsByEfstId.Clear();
            definitionsByEfstId.Clear();
            imagesByEfstId.Clear();

            ParseEfstIds(efstIdText);
            ParseImagePriorities(stateIconImgInfoText);
            var buildersByEfstId = new Dictionary<ushort, StatusIconInfoBuilder>();
            var iconsByEfstId = new Dictionary<ushort, string>();
            ParseStateIconInfo(stateIconInfoText, buildersByEfstId);
            ParseStateIconImageInfo(stateIconImgInfoText, iconsByEfstId);

            var allIds = new HashSet<ushort>();
            foreach (ushort efstId in buildersByEfstId.Keys) {
                allIds.Add(efstId);
            }

            foreach (ushort efstId in iconsByEfstId.Keys) {
                allIds.Add(efstId);
            }

            foreach (string fallbackSymbol in RoLegacyStatusIconFallbacks.Symbols) {
                if (!efstIdsBySymbol.TryGetValue(fallbackSymbol, out ushort efstId)) {
                    continue;
                }

                allIds.Add(efstId);
                if (!iconsByEfstId.ContainsKey(efstId) &&
                    RoLegacyStatusIconFallbacks.TryResolveIconName(fallbackSymbol, out string fallbackIconName)) {
                    iconsByEfstId[efstId] = fallbackIconName;
                }
            }

            foreach (ushort efstId in allIds) {
                buildersByEfstId.TryGetValue(efstId, out StatusIconInfoBuilder builder);
                iconsByEfstId.TryGetValue(efstId, out string iconName);
                if (string.IsNullOrWhiteSpace(iconName)) {
                    iconName = builder?.IconName ?? string.Empty;
                }

                symbolsByEfstId.TryGetValue(efstId, out string symbol);
                definitionsByEfstId[efstId] = new RoStatusIconDefinition(
                    efstId,
                    symbol ?? string.Empty,
                    iconName ?? string.Empty,
                    builder?.HaveTimeLimit ?? false,
                    builder?.PosTimeLimitStr ?? 0,
                    builder?.ToDescriptionArray() ?? Array.Empty<RoStatusIconDescriptionLine>());
            }
        }

        /// <summary>The "[PRIORITY_X] = {" blocks of StateIconImgList, line by line; the first priority of an EFST wins.</summary>
        private void ParseImagePriorities(string text) {
            int priority = -1;
            foreach (string raw in StripComments(text).Split('\n')) {
                string line = raw.Trim();
                if (line.StartsWith("[PRIORITY_", StringComparison.Ordinal)) {
                    int close = line.IndexOf(']');
                    string name = close > 10 ? line.Substring(10, close - 10).Trim() : string.Empty;
                    priority = name == "GOLD" ? 0 : name == "RED" ? 1 : name == "BLUE" ? 2 : name == "GREEN" ? 3 : name == "WHITE" ? 4 : -1;
                    continue;
                }

                if (priority < 0 || !line.StartsWith("[", StringComparison.Ordinal)) continue;
                int end = line.IndexOf(']');
                int q0 = line.IndexOf('"', end + 1);
                int q1 = q0 >= 0 ? line.IndexOf('"', q0 + 1) : -1;
                if (end < 0 || q1 < 0) continue;
                if (TryResolveEfstIdExpression(line.Substring(1, end - 1), out ushort efstId) && !imagesByEfstId.ContainsKey(efstId)) {
                    imagesByEfstId[efstId] = new KeyValuePair<int, string>(priority, line.Substring(q0 + 1, q1 - q0 - 1));
                }
            }
        }

        private void ParseEfstIds(string text) {
            string sanitized = StripComments(text);
            if (!TryExtractAssignedTableBody(sanitized, "EFST_IDs", out string tableBody)) {
                return;
            }

            List<string> entries = SplitTopLevelEntries(tableBody);
            for (int index = 0; index < entries.Count; index++) {
                if (!TryParseNamedAssignment(entries[index], out string key, out string valueExpression) ||
                    !key.StartsWith("EFST_", StringComparison.Ordinal) ||
                    !TryParseInteger(valueExpression, out int resolvedValue) ||
                    resolvedValue < 0 ||
                    resolvedValue > ushort.MaxValue) {
                    continue;
                }

                ushort efstId = (ushort) resolvedValue;
                efstIdsBySymbol[key] = efstId;
                symbolsByEfstId[efstId] = key;
            }
        }

        private void ParseStateIconInfo(string text, Dictionary<ushort, StatusIconInfoBuilder> buildersByEfstId) {
            string sanitized = StripComments(text);
            if (TryExtractAssignedTableBody(sanitized, "StateIconList", out string rootTableBody)) {
                ParseStatusInfoTableBody(rootTableBody, buildersByEfstId);
            }

            foreach (TableAssignment assignment in ExtractIndexedAssignments(sanitized, "StateIconList")) {
                if (!assignment.IsTableValue || !TryResolveEfstIdExpression(assignment.KeyExpression, out ushort efstId)) {
                    continue;
                }

                buildersByEfstId[efstId] = ParseStatusInfoBlock(assignment.TableBody);
            }
        }

        private void ParseStatusInfoTableBody(string tableBody, Dictionary<ushort, StatusIconInfoBuilder> buildersByEfstId) {
            List<string> entries = SplitTopLevelEntries(tableBody);
            for (int index = 0; index < entries.Count; index++) {
                if (!TryParseKeyedAssignment(entries[index], out string keyExpression, out string valueExpression) ||
                    !TryResolveEfstIdExpression(keyExpression, out ushort efstId) ||
                    !TryExtractInlineTableBody(valueExpression, out string blockBody)) {
                    continue;
                }

                buildersByEfstId[efstId] = ParseStatusInfoBlock(blockBody);
            }
        }

        private static StatusIconInfoBuilder ParseStatusInfoBlock(string blockBody) {
            var builder = new StatusIconInfoBuilder();
            List<string> entries = SplitTopLevelEntries(blockBody);
            for (int index = 0; index < entries.Count; index++) {
                if (!TryParseNamedAssignment(entries[index], out string name, out string valueExpression)) {
                    continue;
                }

                switch (name) {
                    case "haveTimeLimit":
                        builder.HaveTimeLimit = ParseBooleanLike(valueExpression);
                        break;
                    case "posTimeLimitStr":
                        if (TryParseInteger(valueExpression, out int lineIndex)) {
                            builder.PosTimeLimitStr = lineIndex;
                        }

                        break;
                    case "icon":
                        if (TryParseStringLiteral(valueExpression, out string iconName)) {
                            builder.IconName = iconName;
                        }

                        break;
                    case "descript":
                        if (TryExtractInlineTableBody(valueExpression, out string descriptionBody)) {
                            builder.DescriptionLines = ParseDescriptionLines(descriptionBody);
                        }

                        break;
                }
            }

            return builder;
        }

        private static List<RoStatusIconDescriptionLine> ParseDescriptionLines(string descriptionBody) {
            var descriptionLines = new List<RoStatusIconDescriptionLine>();
            List<string> lineEntries = SplitTopLevelEntries(descriptionBody);
            for (int index = 0; index < lineEntries.Count; index++) {
                string entry = lineEntries[index];
                if (TryParseStringLiteral(entry, out string lineText)) {
                    descriptionLines.Add(new RoStatusIconDescriptionLine(lineText));
                    continue;
                }

                if (!TryExtractInlineTableBody(entry, out string lineBody)) {
                    continue;
                }

                List<string> lineParts = SplitTopLevelEntries(lineBody);
                if (lineParts.Count == 0 || !TryParseStringLiteral(lineParts[0], out lineText)) {
                    continue;
                }

                string colorKey = lineParts.Count > 1 ? NormalizeSymbolToken(lineParts[1]) : string.Empty;
                descriptionLines.Add(new RoStatusIconDescriptionLine(lineText, colorKey));
            }

            return descriptionLines;
        }

        private void ParseStateIconImageInfo(string text, Dictionary<ushort, string> iconsByEfstId) {
            string sanitized = StripComments(text);
            if (TryExtractAssignedTableBody(sanitized, "StateIconImgList", out string rootTableBody)) {
                ParseIconMappingsFromTableBody(rootTableBody, iconsByEfstId);
            }

            foreach (TableAssignment assignment in ExtractIndexedAssignments(sanitized, "StateIconImgList")) {
                if (assignment.IsTableValue) {
                    ParseIconMappingsFromTableBody(assignment.TableBody, iconsByEfstId);
                    continue;
                }

                if (TryResolveEfstIdExpression(assignment.KeyExpression, out ushort efstId) &&
                    TryParseStringLiteral(assignment.ValueExpression, out string iconName)) {
                    iconsByEfstId[efstId] = iconName;
                }
            }
        }

        private void ParseIconMappingsFromTableBody(string tableBody, Dictionary<ushort, string> iconsByEfstId) {
            List<string> entries = SplitTopLevelEntries(tableBody);
            for (int index = 0; index < entries.Count; index++) {
                string entry = entries[index];
                if (TryExtractInlineTableBody(entry, out string nestedArrayBody)) {
                    ParseIconMappingsFromTableBody(nestedArrayBody, iconsByEfstId);
                    continue;
                }

                if (!TryParseKeyedAssignment(entry, out string keyExpression, out string valueExpression)) {
                    continue;
                }

                if (TryExtractInlineTableBody(valueExpression, out string nestedBody)) {
                    ParseIconMappingsFromTableBody(nestedBody, iconsByEfstId);
                    continue;
                }

                if (TryResolveEfstIdExpression(keyExpression, out ushort efstId) &&
                    TryParseStringLiteral(valueExpression, out string iconName)) {
                    iconsByEfstId[efstId] = iconName;
                }
            }
        }

        private bool TryResolveEfstIdExpression(string expression, out ushort efstId) {
            string normalized = NormalizeSymbolToken(expression);
            if (normalized.StartsWith("EFST_IDs.", StringComparison.Ordinal)) {
                normalized = normalized.Substring("EFST_IDs.".Length);
            }

            if (efstIdsBySymbol.TryGetValue(normalized, out efstId)) {
                return true;
            }

            if (TryParseInteger(normalized, out int numericValue) &&
                numericValue >= 0 &&
                numericValue <= ushort.MaxValue) {
                efstId = (ushort) numericValue;
                return true;
            }

            efstId = 0;
            return false;
        }

        private bool TryReadText(string fileName, string[] candidatePaths, out string text) {
            text = string.Empty;
            if (assets == null || !assets.IsInitialized) {
                return false;
            }

            for (int index = 0; index < candidatePaths.Length; index++) {
                text = assets.ReadText(candidatePaths[index]);
                if (!string.IsNullOrEmpty(text)) {
                    return true;
                }
            }

            string suffix = ("stateicon/" + fileName).Replace('\\', '/');
            if (assets.TryFindFirstPathEndingWith(suffix, out string resolvedPath)) {
                text = assets.ReadText(resolvedPath);
                return !string.IsNullOrEmpty(text);
            }

            return false;
        }

        private static string StripComments(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            var buffer = new char[text.Length];
            int outputLength = 0;
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            for (int index = 0; index < text.Length; index++) {
                char current = text[index];
                char next = index + 1 < text.Length ? text[index + 1] : '\0';
                if (!inSingleQuote && !inDoubleQuote && current == '-' && next == '-') {
                    while (index < text.Length && text[index] != '\n') {
                        index++;
                    }

                    if (index < text.Length) {
                        buffer[outputLength++] = text[index];
                    }

                    continue;
                }

                if (current == '\'' && !inDoubleQuote && !IsEscaped(text, index)) {
                    inSingleQuote = !inSingleQuote;
                } else if (current == '"' && !inSingleQuote && !IsEscaped(text, index)) {
                    inDoubleQuote = !inDoubleQuote;
                }

                buffer[outputLength++] = current;
            }

            return new string(buffer, 0, outputLength);
        }

        private static bool TryExtractAssignedTableBody(string text, string identifier, out string tableBody) {
            int searchIndex = 0;
            while ((searchIndex = text.IndexOf(identifier, searchIndex, StringComparison.Ordinal)) >= 0) {
                if (!IsIdentifierBoundary(text, searchIndex - 1) ||
                    !IsIdentifierBoundary(text, searchIndex + identifier.Length)) {
                    searchIndex += identifier.Length;
                    continue;
                }

                int cursor = SkipWhitespace(text, searchIndex + identifier.Length);
                if (cursor >= text.Length || text[cursor] != '=') {
                    searchIndex += identifier.Length;
                    continue;
                }

                cursor = SkipWhitespace(text, cursor + 1);
                if (cursor >= text.Length || text[cursor] != '{') {
                    searchIndex += identifier.Length;
                    continue;
                }

                int endIndex = FindMatchingDelimiter(text, cursor, '{', '}');
                tableBody = text.Substring(cursor + 1, endIndex - cursor - 1);
                return true;
            }

            tableBody = string.Empty;
            return false;
        }

        private static List<TableAssignment> ExtractIndexedAssignments(string text, string tableName) {
            var assignments = new List<TableAssignment>();
            int searchIndex = 0;
            while ((searchIndex = text.IndexOf(tableName, searchIndex, StringComparison.Ordinal)) >= 0) {
                if (!IsIdentifierBoundary(text, searchIndex - 1) ||
                    !IsIdentifierBoundary(text, searchIndex + tableName.Length)) {
                    searchIndex += tableName.Length;
                    continue;
                }

                int cursor = SkipWhitespace(text, searchIndex + tableName.Length);
                if (cursor >= text.Length || text[cursor] != '[') {
                    searchIndex += tableName.Length;
                    continue;
                }

                int keyEnd = FindMatchingDelimiter(text, cursor, '[', ']');
                string keyExpression = text.Substring(cursor + 1, keyEnd - cursor - 1).Trim();
                cursor = SkipWhitespace(text, keyEnd + 1);
                if (cursor >= text.Length || text[cursor] != '=') {
                    searchIndex += tableName.Length;
                    continue;
                }

                cursor = SkipWhitespace(text, cursor + 1);
                if (cursor >= text.Length) {
                    break;
                }

                if (text[cursor] == '{') {
                    int blockEnd = FindMatchingDelimiter(text, cursor, '{', '}');
                    assignments.Add(TableAssignment.FromTable(keyExpression, text.Substring(cursor + 1, blockEnd - cursor - 1)));
                    searchIndex = blockEnd + 1;
                    continue;
                }

                int valueEnd = FindExpressionEnd(text, cursor);
                assignments.Add(TableAssignment.FromValue(keyExpression, text.Substring(cursor, valueEnd - cursor)));
                searchIndex = valueEnd;
            }

            return assignments;
        }

        private static bool TryParseKeyedAssignment(string entry, out string keyExpression, out string valueExpression) {
            string trimmed = entry?.Trim() ?? string.Empty;
            if (trimmed.Length == 0) {
                keyExpression = string.Empty;
                valueExpression = string.Empty;
                return false;
            }

            if (trimmed[0] == '[') {
                int keyEnd = FindMatchingDelimiter(trimmed, 0, '[', ']');
                int cursor = SkipWhitespace(trimmed, keyEnd + 1);
                if (cursor >= trimmed.Length || trimmed[cursor] != '=') {
                    keyExpression = string.Empty;
                    valueExpression = string.Empty;
                    return false;
                }

                keyExpression = trimmed.Substring(1, keyEnd - 1).Trim();
                valueExpression = trimmed.Substring(cursor + 1).Trim();
                return true;
            }

            return TryParseNamedAssignment(trimmed, out keyExpression, out valueExpression);
        }

        private static bool TryParseNamedAssignment(string entry, out string name, out string valueExpression) {
            string trimmed = entry?.Trim() ?? string.Empty;
            int equalsIndex = FindTopLevelEquals(trimmed);
            if (equalsIndex <= 0) {
                name = string.Empty;
                valueExpression = string.Empty;
                return false;
            }

            name = trimmed.Substring(0, equalsIndex).Trim();
            valueExpression = trimmed.Substring(equalsIndex + 1).Trim();
            return name.Length > 0;
        }

        private static int FindTopLevelEquals(string text) {
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            int curlyDepth = 0;
            int squareDepth = 0;
            int roundDepth = 0;
            for (int index = 0; index < text.Length; index++) {
                char current = text[index];
                if (current == '\'' && !inDoubleQuote && !IsEscaped(text, index)) {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (current == '"' && !inSingleQuote && !IsEscaped(text, index)) {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote) {
                    continue;
                }

                switch (current) {
                    case '{':
                        curlyDepth++;
                        break;
                    case '}':
                        curlyDepth = Math.Max(0, curlyDepth - 1);
                        break;
                    case '[':
                        squareDepth++;
                        break;
                    case ']':
                        squareDepth = Math.Max(0, squareDepth - 1);
                        break;
                    case '(':
                        roundDepth++;
                        break;
                    case ')':
                        roundDepth = Math.Max(0, roundDepth - 1);
                        break;
                    case '=' when curlyDepth == 0 && squareDepth == 0 && roundDepth == 0:
                        return index;
                }
            }

            return -1;
        }

        private static List<string> SplitTopLevelEntries(string body) {
            var entries = new List<string>();
            if (string.IsNullOrWhiteSpace(body)) {
                return entries;
            }

            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            int curlyDepth = 0;
            int squareDepth = 0;
            int roundDepth = 0;
            int entryStart = 0;

            for (int index = 0; index < body.Length; index++) {
                char current = body[index];
                if (current == '\'' && !inDoubleQuote && !IsEscaped(body, index)) {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (current == '"' && !inSingleQuote && !IsEscaped(body, index)) {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote) {
                    continue;
                }

                switch (current) {
                    case '{':
                        curlyDepth++;
                        break;
                    case '}':
                        curlyDepth = Math.Max(0, curlyDepth - 1);
                        break;
                    case '[':
                        squareDepth++;
                        break;
                    case ']':
                        squareDepth = Math.Max(0, squareDepth - 1);
                        break;
                    case '(':
                        roundDepth++;
                        break;
                    case ')':
                        roundDepth = Math.Max(0, roundDepth - 1);
                        break;
                    case ',' when curlyDepth == 0 && squareDepth == 0 && roundDepth == 0:
                        AddEntry(body, entryStart, index - entryStart, entries);
                        entryStart = index + 1;
                        break;
                }
            }

            AddEntry(body, entryStart, body.Length - entryStart, entries);
            return entries;
        }

        private static void AddEntry(string body, int startIndex, int length, List<string> entries) {
            if (length <= 0) {
                return;
            }

            string entry = body.Substring(startIndex, length).Trim();
            if (entry.Length > 0) {
                entries.Add(entry);
            }
        }

        private static bool TryExtractInlineTableBody(string valueExpression, out string tableBody) {
            string trimmed = valueExpression?.Trim() ?? string.Empty;
            if (trimmed.Length >= 2 &&
                trimmed[0] == '{' &&
                FindMatchingDelimiter(trimmed, 0, '{', '}') == trimmed.Length - 1) {
                tableBody = trimmed.Substring(1, trimmed.Length - 2);
                return true;
            }

            tableBody = string.Empty;
            return false;
        }

        private static bool TryParseStringLiteral(string valueExpression, out string value) {
            string trimmed = valueExpression?.Trim() ?? string.Empty;
            if (trimmed.Length < 2) {
                value = string.Empty;
                return false;
            }

            char quote = trimmed[0];
            if ((quote != '"' && quote != '\'') || trimmed[trimmed.Length - 1] != quote) {
                value = string.Empty;
                return false;
            }

            value = UnescapeLuaString(trimmed.Substring(1, trimmed.Length - 2));
            return true;
        }

        private static string NormalizeSymbolToken(string expression) {
            string normalized = expression?.Trim() ?? string.Empty;
            if (normalized.EndsWith(",", StringComparison.Ordinal)) {
                normalized = normalized.Substring(0, normalized.Length - 1).Trim();
            }

            return TryParseStringLiteral(normalized, out string stringValue) ? stringValue : normalized;
        }

        private static bool ParseBooleanLike(string expression) {
            string normalized = NormalizeSymbolToken(expression);
            if (string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            return TryParseInteger(normalized, out int numericValue) && numericValue != 0;
        }

        private static bool TryParseInteger(string expression, out int value) {
            string normalized = NormalizeSymbolToken(expression);
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                return int.TryParse(
                    normalized.Substring(2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out value);
            }

            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static int SkipWhitespace(string text, int index) {
            while (index < text.Length && char.IsWhiteSpace(text[index])) {
                index++;
            }

            return index;
        }

        private static int FindExpressionEnd(string text, int index) {
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            int curlyDepth = 0;
            int squareDepth = 0;
            int roundDepth = 0;
            for (int cursor = index; cursor < text.Length; cursor++) {
                char current = text[cursor];
                if (current == '\'' && !inDoubleQuote && !IsEscaped(text, cursor)) {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (current == '"' && !inSingleQuote && !IsEscaped(text, cursor)) {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote) {
                    continue;
                }

                switch (current) {
                    case '{':
                        curlyDepth++;
                        break;
                    case '}':
                        if (curlyDepth == 0 && squareDepth == 0 && roundDepth == 0) {
                            return cursor;
                        }

                        curlyDepth = Math.Max(0, curlyDepth - 1);
                        break;
                    case '[':
                        squareDepth++;
                        break;
                    case ']':
                        squareDepth = Math.Max(0, squareDepth - 1);
                        break;
                    case '(':
                        roundDepth++;
                        break;
                    case ')':
                        roundDepth = Math.Max(0, roundDepth - 1);
                        break;
                    case ',' when curlyDepth == 0 && squareDepth == 0 && roundDepth == 0:
                        return cursor;
                }
            }

            return text.Length;
        }

        private static int FindMatchingDelimiter(string text, int startIndex, char openDelimiter, char closeDelimiter) {
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            int depth = 0;
            for (int index = startIndex; index < text.Length; index++) {
                char current = text[index];
                if (current == '\'' && !inDoubleQuote && !IsEscaped(text, index)) {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (current == '"' && !inSingleQuote && !IsEscaped(text, index)) {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote) {
                    continue;
                }

                if (current == openDelimiter) {
                    depth++;
                    continue;
                }

                if (current == closeDelimiter) {
                    depth--;
                    if (depth == 0) {
                        return index;
                    }
                }
            }

            throw new InvalidDataException($"Unbalanced '{openDelimiter}' in status-icon definition.");
        }

        private static bool IsEscaped(string text, int index) {
            int slashCount = 0;
            for (int cursor = index - 1; cursor >= 0 && text[cursor] == '\\'; cursor--) {
                slashCount++;
            }

            return (slashCount & 1) != 0;
        }

        private static bool IsIdentifierBoundary(string text, int index) {
            if (index < 0 || index >= text.Length) {
                return true;
            }

            char current = text[index];
            return !char.IsLetterOrDigit(current) && current != '_';
        }

        private static string UnescapeLuaString(string raw) {
            if (string.IsNullOrEmpty(raw)) {
                return string.Empty;
            }

            var buffer = new char[raw.Length];
            int outputLength = 0;
            for (int index = 0; index < raw.Length; index++) {
                char current = raw[index];
                if (current != '\\' || index + 1 >= raw.Length) {
                    buffer[outputLength++] = current;
                    continue;
                }

                char escaped = raw[++index];
                switch (escaped) {
                    case 'a':
                        buffer[outputLength++] = '\a';
                        break;
                    case 'b':
                        buffer[outputLength++] = '\b';
                        break;
                    case 'f':
                        buffer[outputLength++] = '\f';
                        break;
                    case 'n':
                        buffer[outputLength++] = '\n';
                        break;
                    case 'r':
                        buffer[outputLength++] = '\r';
                        break;
                    case 't':
                        buffer[outputLength++] = '\t';
                        break;
                    case 'v':
                        buffer[outputLength++] = '\v';
                        break;
                    case '\\':
                    case '"':
                    case '\'':
                        buffer[outputLength++] = escaped;
                        break;
                    default:
                        if (char.IsDigit(escaped)) {
                            int codePoint = escaped - '0';
                            int digits = 1;
                            while (digits < 3 && index + 1 < raw.Length && char.IsDigit(raw[index + 1])) {
                                codePoint = (codePoint * 10) + (raw[index + 1] - '0');
                                index++;
                                digits++;
                            }

                            if (codePoint < 0) {
                                codePoint = 0;
                            } else if (codePoint > 255) {
                                codePoint = 255;
                            }

                            buffer[outputLength++] = (char) codePoint;
                        } else {
                            buffer[outputLength++] = escaped;
                        }

                        break;
                }
            }

            return new string(buffer, 0, outputLength);
        }

        private sealed class StatusIconInfoBuilder {
            public bool HaveTimeLimit { get; set; }
            public int PosTimeLimitStr { get; set; }
            public string IconName { get; set; } = string.Empty;
            public List<RoStatusIconDescriptionLine> DescriptionLines { get; set; } =
                new List<RoStatusIconDescriptionLine>();

            public RoStatusIconDescriptionLine[] ToDescriptionArray() {
                return DescriptionLines?.ToArray() ?? Array.Empty<RoStatusIconDescriptionLine>();
            }
        }

        private readonly struct TableAssignment {
            private TableAssignment(string keyExpression, string valueExpression, string tableBody, bool isTableValue) {
                KeyExpression = keyExpression ?? string.Empty;
                ValueExpression = valueExpression ?? string.Empty;
                TableBody = tableBody ?? string.Empty;
                IsTableValue = isTableValue;
            }

            public string KeyExpression { get; }
            public string ValueExpression { get; }
            public string TableBody { get; }
            public bool IsTableValue { get; }

            public static TableAssignment FromValue(string keyExpression, string valueExpression) {
                return new TableAssignment(keyExpression, valueExpression, string.Empty, false);
            }

            public static TableAssignment FromTable(string keyExpression, string tableBody) {
                return new TableAssignment(keyExpression, string.Empty, tableBody, true);
            }
        }
    }

    internal static class RoLegacyStatusIconFallbacks {
        // Adapted from Custom's roBrowser legacy status-info fallback table.
        private static readonly Dictionary<string, string> IconHexBySymbol =
            new Dictionary<string, string>(StringComparer.Ordinal) {
                { "EFST_ADRENALINE", "BEC6B5E5B7B9B3AFB8B0B7AFBDAC2E746761" },
                { "EFST_ADRENALINE2", "BEC6B5E5B7B9B3AFB8B0B7AFBDAC2E746761" },
                { "EFST_ANGELUS", "BEC8C1A9B7E7BDBA2E746761" },
                { "EFST_ASPERSIO", "BEC6BDBAC6E4B8A3BDC3BFC02E746761" },
                { "EFST_ASSUMPTIO", "69636F6E30352E746761" },
                { "EFST_ASSUMPTIO2", "69636F6E30352E746761" },
                { "EFST_ATTHASTE_POTION1", "B0F8BCD3B9B0BEE02E746761" },
                { "EFST_ATTHASTE_POTION2", "B0F8BCD3B9B0BEE02E746761" },
                { "EFST_ATTHASTE_POTION3", "B0F8BCD3B9B0BEE02E746761" },
                { "EFST_AUTOBERSERK", "B1DDB0ADBAD2B1AB2E746761" },
                { "EFST_AUTOGUARD", "BFC0C5E4B0A1B5E52E746761" },
                { "EFST_BLESSING", "BAEDB7B9BDCC2E746761" },
                { "EFST_CASH_PLUSEXP", "6578702E746761" },
                { "EFST_CASH_PLUSONLYJOBEXP", "6A6F622E746761" },
                { "EFST_CASH_RECEIVEITEM", "6974656D2E746761" },
                { "EFST_CLIENT_ONLY_EQUIP_ARROW", "4172726F774E2E746761" },
                { "EFST_CLOAKING", "C5ACB7CEC5B72E746761" },
                { "EFST_CONCENTRATION", "C1FDC1DFB7C2C7E2BBF32E746761" },
                { "EFST_DEC_AGI", "B9CEC3B8BCBAB0A8BCD22E746761" },
                { "EFST_DEFENDER", "B5F0C6E6B4F52E746761" },
                { "EFST_DEVOTION", "C7E5BDC52E746761" },
                { "EFST_EDP", "695FC4A1B5B62E746761" },
                { "EFST_ENCHANTPOISON", "C0CEC2F9C6AEC6F7C0CCC1F02E746761" },
                { "EFST_ENDURE", "C0CEB5E0BEEE2E746761" },
                { "EFST_ENERGYCOAT", "BFA1B3CAC1F6C4DAC6AE2E746761" },
                { "EFST_FALCON", "C6C8C4DC2E746761" },
                { "EFST_GLORIA", "B1DBB7CEB8AEBEC62E746761" },
                { "EFST_HIDING", "C7CFC0CCB5F92E746761" },
                { "EFST_IMPOSITIO", "C0D3C6F7BDC3C6BCBFC0B8B6B4A9BDBA2E746761" },
                { "EFST_INC_AGI", "B9CEC3B8BCBAC1F5B0A12E746761" },
                { "EFST_KYRIE", "B1E2B8AEBFA1BFA4B7B9C0CCBCD52E746761" },
                { "EFST_LEXAETERNA", "B7BABDBABFA1C5D7B8A3B3AA2E746761" },
                { "EFST_MAGNIFICAT", "B8B6B4CFC7C7C4B12E746761" },
                { "EFST_MAXIMIZE", "B8C6BDC3B8B6C0CCC1EEC6C4BFF62E746761" },
                { "EFST_MELTDOWN", "69636F6E30332E746761" },
                { "EFST_OVERTHRUST", "BFC0B9F6C6AEB7AFBDBAC6AE2E746761" },
                { "EFST_OVERTHRUSTMAX", "695FBFC0B9F6B8C6BDBA2E746761" },
                { "EFST_PARRYING", "69636F6E31302E746761" },
                { "EFST_POISONREACT", "C6F7C0CCC1F0B8AEBED7C6AE2E746761" },
                { "EFST_PROVOKE", "C7C1B7CEBAB8C5A92E746761" },
                { "EFST_REFLECTSHIELD", "B8AEC7C3B7BAC6AEBDAFB5E52E746761" },
                { "EFST_SLOWPOISON", "BDBDB7CEBFECC6F7C0CCC1F02E746761" },
                { "EFST_SPEARQUICKEN", "BDBAC7C7BEEEC4FBC5AB2E746761" },
                { "EFST_SUFFRAGIUM", "BCF6C1DDC0BAC7CFB7E7C0C7BFECBFEF2E746761" },
                { "EFST_TENSIONRELAX", "69636F6E30382E746761" },
                { "EFST_TRUESIGHT", "69636F6E30392E746761" },
                { "EFST_TWOHANDQUICKEN", "C5F5C7DAB5E5C4FBC5AB2E746761" },
                { "EFST_WEAPONPERFECT", "BFFEC6F9C6DBC6E5BCC72E746761" },
                { "EFST_WEIGHTOVER50", "B9ABB0D43530C0CCBBF32E746761" },
                { "EFST_WEIGHTOVER90", "B9ABB0D43930C0CCBBF32E746761" },
                { "EFST_WINDWALK", "69636F6E30362E746761" },
                { "EFST_WZ_SIGHTBLASTER", "695F7369676874626C61737465722E746761" }
            };

        private static readonly Dictionary<string, string> DecodedIconNamesBySymbol =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public static IEnumerable<string> Symbols => IconHexBySymbol.Keys;

        public static bool TryResolveIconName(string symbol, out string iconName) {
            if (string.IsNullOrWhiteSpace(symbol) || !IconHexBySymbol.TryGetValue(symbol, out string iconHex)) {
                iconName = string.Empty;
                return false;
            }

            if (DecodedIconNamesBySymbol.TryGetValue(symbol, out iconName)) {
                return !string.IsNullOrWhiteSpace(iconName);
            }

            iconName = DecodeHexIconName(iconHex);
            DecodedIconNamesBySymbol[symbol] = iconName;
            return !string.IsNullOrWhiteSpace(iconName);
        }

        private static string DecodeHexIconName(string iconHex) {
            if (string.IsNullOrWhiteSpace(iconHex) || (iconHex.Length % 2) != 0) {
                return string.Empty;
            }

            int byteCount = iconHex.Length / 2;
            var bytes = new byte[byteCount];
            for (int index = 0; index < byteCount; index++) {
                bytes[index] = Convert.ToByte(iconHex.Substring(index * 2, 2), 16);
            }

            return RoTextEncoding.Decode(bytes);
        }
    }
}
