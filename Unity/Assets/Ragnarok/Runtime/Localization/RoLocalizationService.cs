using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Data;

namespace Ragnarok.Localization {
    public enum RoLocalizedStringSource {
        Missing = 0,
        UiStringTable,
        UiDefault,
        SystemMessageTable,
        ItemMetadata,
        SkillMetadata,
        EntityMetadata
    }

    public readonly struct RoLocalizedString {
        public RoLocalizedString(string key, string text, RoLocalizedStringSource source) {
            Key = key ?? string.Empty;
            Text = text ?? string.Empty;
            Source = source;
        }

        public string Key { get; }
        public string Text { get; }
        public RoLocalizedStringSource Source { get; }
        public bool IsMissing => Source == RoLocalizedStringSource.Missing;
        public RoTextDiagnosticReport Diagnostics => RoTextDiagnostics.Analyze(Text);

        public override string ToString() {
            return Text;
        }
    }

    public sealed class RoLocalizationService {
        private static readonly string[] UiStringCandidatePaths = {
            "data/localization/ui.txt",
            "data/localization/ui_kr.txt",
            "data/interface/ui_strings.txt",
            "System/localization/ui.txt",
            "System/ui_strings.txt",
        };

        public static readonly RoLocalizationService Empty = FromSources(null, null, null, null, null);

        private readonly Dictionary<string, string> uiDefaults;
        private readonly Dictionary<string, string> uiStrings;
        private readonly Dictionary<string, string> systemMessages;
        private readonly RoItemMetadataTable items;
        private readonly RoSkillMetadataTable skills;
        private readonly RoEntityDisplayMetadataTable entities;

        private RoLocalizationService(
            Dictionary<string, string> uiDefaults,
            Dictionary<string, string> uiStrings,
            Dictionary<string, string> systemMessages,
            RoItemMetadataTable items,
            RoSkillMetadataTable skills,
            RoEntityDisplayMetadataTable entities,
            string uiStringSourcePath
        ) {
            this.uiDefaults = uiDefaults ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.uiStrings = uiStrings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.systemMessages = systemMessages ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.items = items ?? RoItemMetadataTable.Empty;
            this.skills = skills ?? RoSkillMetadataTable.Empty;
            this.entities = entities ?? RoEntityDisplayMetadataTable.Empty;
            UiStringSourcePath = uiStringSourcePath;
        }

        public int UiDefaultCount => uiDefaults.Count;
        public int UiStringCount => uiStrings.Count;
        public int SystemMessageCount => systemMessages.Count;
        public int ItemCount => items.Count;
        public int SkillCount => skills.Count;
        public int EntityCount => entities.Count;
        public string UiStringSourcePath { get; }

        public static RoLocalizationService Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            Dictionary<string, string> loadedUiStrings = LoadUiStrings(assets, out string uiSourcePath);
            return FromSources(
                assets.Tables?.MsgStringTable,
                loadedUiStrings,
                assets.GameData?.Items,
                assets.GameData?.Skills,
                assets.GameData?.EntityDisplay,
                uiSourcePath
            );
        }

        public static RoLocalizationService FromSources(
            IReadOnlyDictionary<string, string> systemMessages,
            IReadOnlyDictionary<string, string> uiStrings,
            RoItemMetadataTable items,
            RoSkillMetadataTable skills,
            RoEntityDisplayMetadataTable entities,
            string uiStringSourcePath = null
        ) {
            Dictionary<string, string> defaults = CreateDefaultUiStrings();
            return new RoLocalizationService(
                defaults,
                CopyStrings(uiStrings),
                CopyStrings(systemMessages),
                items,
                skills,
                entities,
                uiStringSourcePath
            );
        }

        public bool TryGetString(string key, out RoLocalizedString localized) {
            if (string.IsNullOrWhiteSpace(key)) {
                localized = Missing(key);
                return false;
            }

            key = key.Trim();
            if (TryGetUiString(key, out localized)) {
                return true;
            }

            if (TryGetSystemMessage(key, out localized)) {
                return true;
            }

            if (TryGetItemString(key, out localized)) {
                return true;
            }

            if (TryGetSkillString(key, out localized)) {
                return true;
            }

            if (TryGetEntityString(key, out localized)) {
                return true;
            }

            localized = Missing(key);
            return false;
        }

        public string GetStringOrKey(string key) {
            return TryGetString(key, out RoLocalizedString localized)
                ? localized.Text
                : "[" + (key ?? string.Empty) + "]";
        }

        public bool TryGetUiString(string key, out RoLocalizedString localized) {
            localized = Missing(key);
            if (string.IsNullOrWhiteSpace(key)) {
                return false;
            }

            key = key.Trim();
            if (uiStrings.TryGetValue(key, out string text)) {
                localized = new RoLocalizedString(key, text, RoLocalizedStringSource.UiStringTable);
                return true;
            }

            if (uiDefaults.TryGetValue(key, out text)) {
                localized = new RoLocalizedString(key, text, RoLocalizedStringSource.UiDefault);
                return true;
            }

            return false;
        }

        public bool TryGetSystemMessage(string key, out RoLocalizedString localized) {
            localized = Missing(key);
            if (string.IsNullOrWhiteSpace(key)) {
                return false;
            }

            key = StripPrefix(key.Trim(), "system.");
            key = StripPrefix(key, "msg.");
            if (!systemMessages.TryGetValue(key, out string text)) {
                return false;
            }

            localized = new RoLocalizedString("system." + key, text, RoLocalizedStringSource.SystemMessageTable);
            return true;
        }

        public bool TryGetItemName(int itemId, bool identified, out RoLocalizedString localized) {
            localized = Missing(BuildItemKey(itemId, "name", identified));
            if (!items.TryGetItem(itemId, out RoItemMetadata item) || item == null) {
                return false;
            }

            localized = new RoLocalizedString(BuildItemKey(itemId, "name", identified), item.GetDisplayName(identified), RoLocalizedStringSource.ItemMetadata);
            return true;
        }

        public bool TryGetItemDescription(int itemId, bool identified, out RoLocalizedString localized) {
            localized = Missing(BuildItemKey(itemId, "description", identified));
            if (!items.TryGetItem(itemId, out RoItemMetadata item) || item == null) {
                return false;
            }

            localized = new RoLocalizedString(BuildItemKey(itemId, "description", identified), JoinLines(item.GetDescription(identified)), RoLocalizedStringSource.ItemMetadata);
            return true;
        }

        public bool TryGetSkillName(int skillId, out RoLocalizedString localized) {
            localized = Missing("skill." + skillId.ToString(CultureInfo.InvariantCulture) + ".name");
            if (!skills.TryGetSkill(skillId, out RoSkillMetadata skill) || skill == null) {
                return false;
            }

            localized = new RoLocalizedString(localized.Key, skill.GetDisplayName(), RoLocalizedStringSource.SkillMetadata);
            return true;
        }

        public bool TryGetSkillDescription(int skillId, out RoLocalizedString localized) {
            localized = Missing("skill." + skillId.ToString(CultureInfo.InvariantCulture) + ".description");
            if (!skills.TryGetSkill(skillId, out RoSkillMetadata skill) || skill == null) {
                return false;
            }

            localized = new RoLocalizedString(localized.Key, skill.DescriptionText, RoLocalizedStringSource.SkillMetadata);
            return true;
        }

        public bool TryGetEntityName(int classId, out RoLocalizedString localized) {
            localized = Missing("entity." + classId.ToString(CultureInfo.InvariantCulture) + ".name");
            if (!entities.TryGetEntity(classId, out RoEntityDisplayMetadata entity) || entity == null) {
                return false;
            }

            localized = new RoLocalizedString(localized.Key, entity.DisplayName, RoLocalizedStringSource.EntityMetadata);
            return true;
        }

        public string BuildSummary() {
            var builder = new StringBuilder();
            builder.Append("localization: ui=");
            builder.Append(UiStringCount);
            builder.Append("+defaults:");
            builder.Append(UiDefaultCount);
            builder.Append(", system=");
            builder.Append(SystemMessageCount);
            builder.Append(", items=");
            builder.Append(ItemCount);
            builder.Append(", skills=");
            builder.Append(SkillCount);
            builder.Append(", entities=");
            builder.Append(EntityCount);

            if (!string.IsNullOrEmpty(UiStringSourcePath)) {
                builder.Append(", uiSource='");
                builder.Append(UiStringSourcePath);
                builder.Append('\'');
            }

            return builder.ToString();
        }

        private bool TryGetItemString(string key, out RoLocalizedString localized) {
            localized = Missing(key);
            string[] parts = SplitKey(key);
            if (parts.Length < 3 ||
                !string.Equals(parts[0], "item", StringComparison.OrdinalIgnoreCase) ||
                !TryParsePositiveInt(parts[1], out int itemId)) {
                return false;
            }

            bool identified = parts.Length < 4 || !string.Equals(parts[3], "unidentified", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(parts[2], "name", StringComparison.OrdinalIgnoreCase)) {
                return TryGetItemName(itemId, identified, out localized);
            }

            if (string.Equals(parts[2], "description", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parts[2], "desc", StringComparison.OrdinalIgnoreCase)) {
                return TryGetItemDescription(itemId, identified, out localized);
            }

            return false;
        }

        private bool TryGetSkillString(string key, out RoLocalizedString localized) {
            localized = Missing(key);
            string[] parts = SplitKey(key);
            if (parts.Length < 3 ||
                !string.Equals(parts[0], "skill", StringComparison.OrdinalIgnoreCase) ||
                !TryParsePositiveInt(parts[1], out int skillId)) {
                return false;
            }

            if (string.Equals(parts[2], "name", StringComparison.OrdinalIgnoreCase)) {
                return TryGetSkillName(skillId, out localized);
            }

            if (string.Equals(parts[2], "description", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parts[2], "desc", StringComparison.OrdinalIgnoreCase)) {
                return TryGetSkillDescription(skillId, out localized);
            }

            return false;
        }

        private bool TryGetEntityString(string key, out RoLocalizedString localized) {
            localized = Missing(key);
            string[] parts = SplitKey(key);
            if (parts.Length < 3 ||
                !string.Equals(parts[0], "entity", StringComparison.OrdinalIgnoreCase) ||
                !TryParsePositiveInt(parts[1], out int classId)) {
                return false;
            }

            if (string.Equals(parts[2], "name", StringComparison.OrdinalIgnoreCase)) {
                return TryGetEntityName(classId, out localized);
            }

            return false;
        }

        private static Dictionary<string, string> LoadUiStrings(RoAssetDatabase assets, out string sourcePath) {
            sourcePath = null;
            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < UiStringCandidatePaths.Length; i++) {
                string candidate = UiStringCandidatePaths[i];
                if (!assets.Exists(candidate)) {
                    continue;
                }

                string text = assets.ReadText(candidate);
                if (string.IsNullOrWhiteSpace(text)) {
                    continue;
                }

                int countBefore = output.Count;
                ParseUiStringTable(text, output);
                if (output.Count == countBefore) {
                    continue;
                }

                sourcePath = candidate;
                break;
            }

            return output;
        }

        private static void ParseUiStringTable(string text, IDictionary<string, string> output) {
            if (string.IsNullOrWhiteSpace(text) || output == null) {
                return;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal)) {
                    continue;
                }

                int separator = line.IndexOf('#');
                if (separator < 0) {
                    separator = line.IndexOf('=');
                }

                if (separator <= 0 || separator + 1 >= line.Length) {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (value.EndsWith("#", StringComparison.Ordinal)) {
                    value = value.Substring(0, value.Length - 1).TrimEnd();
                }

                if (key.Length > 0) {
                    output[key] = value.Replace("\\n", "\n");
                }
            }
        }

        private static Dictionary<string, string> CreateDefaultUiStrings() {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["ui.login.title"] = "Ragnarok Online",
                ["ui.login.account"] = "Account",
                ["ui.login.password"] = "Password",
                ["ui.login.connect"] = "Connect",
                ["ui.login.cancel"] = "Cancel",
                ["ui.login.server"] = "Server",
                ["ui.login.status"] = "Status",
                ["ui.char.select"] = "Select Character",
                ["ui.char.create"] = "Create",
                ["ui.char.name"] = "Character Name",
                ["ui.char.enter"] = "Enter",
                ["ui.char.slot"] = "Slot",
                ["ui.map.loading"] = "Loading...",
                ["ui.map.entered"] = "Map entered.",
                ["ui.network.packetLog"] = "Packet Log",
                ["ui.network.charactersLoaded"] = "Characters loaded.",
                ["ui.network.characterCreated"] = "Character created.",
                ["ui.network.connecting"] = "Connecting...",
                ["ui.network.enteringMap"] = "Entering map...",
                ["ui.error.connection"] = "Connection failed.",
            };
        }

        private static Dictionary<string, string> CopyStrings(IReadOnlyDictionary<string, string> source) {
            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null) {
                return output;
            }

            foreach (KeyValuePair<string, string> entry in source) {
                if (!string.IsNullOrWhiteSpace(entry.Key)) {
                    output[entry.Key.Trim()] = entry.Value ?? string.Empty;
                }
            }

            return output;
        }

        private static string[] SplitKey(string key) {
            return string.IsNullOrWhiteSpace(key)
                ? Array.Empty<string>()
                : key.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool TryParsePositiveInt(string text, out int value) {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        private static string StripPrefix(string value, string prefix) {
            return !string.IsNullOrEmpty(value) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(prefix.Length)
                : value;
        }

        private static string BuildItemKey(int itemId, string field, bool identified) {
            return "item." +
                itemId.ToString(CultureInfo.InvariantCulture) +
                "." +
                field +
                "." +
                (identified ? "identified" : "unidentified");
        }

        private static string JoinLines(IReadOnlyList<string> lines) {
            if (lines == null || lines.Count == 0) {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < lines.Count; i++) {
                if (i > 0) {
                    builder.Append('\n');
                }

                builder.Append(lines[i] ?? string.Empty);
            }

            return builder.ToString();
        }

        private static RoLocalizedString Missing(string key) {
            return new RoLocalizedString(key, "[" + (key ?? string.Empty) + "]", RoLocalizedStringSource.Missing);
        }
    }
}
