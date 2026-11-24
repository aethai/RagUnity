using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// What the client keeps per quest beside the packets (codex-ui-research RUNTIME-RULES-QUESTS-EMOTIONS-NOTIFICATIONS.md
    /// "quests"): GetOngoingQuestInfoByID (questinfo_f.lub) gives the display flag (GetOngoingSimpleView, cleared for
    /// CoolTimeQuest 1) and the cool-time mark; QuestClassificationInfo.lub's QuestFilterTable groups the icons for the
    /// category strip; FUN_00c43080 / FUN_006120f0 name the hunt objectives.
    /// </summary>
    public static class RoClassicQuestData {
        public const int FilterAll = 0, FilterEtc = 1;

        private static RoAssetDatabase loadedFor;
        private static readonly HashSet<uint> coolTime = new HashSet<uint>();
        private static readonly HashSet<uint> simpleView = new HashSet<uint>();
        private static readonly Dictionary<int, HashSet<string>> filterIcons = new Dictionary<int, HashSet<string>>();
        // record +0xb4: the per-quest display byte, set when the record is made, flipped by the list's check box
        private static readonly Dictionary<uint, bool> display = new Dictionary<uint, bool>();
        private static object displayOwner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            loadedFor = null;
            coolTime.Clear();
            simpleView.Clear();
            filterIcons.Clear();
            display.Clear();
            displayOwner = null;
        }

        private static void Load() {
            var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            if (assets == null || !assets.IsInitialized || ReferenceEquals(assets, loadedFor)) return;
            loadedFor = assets;
            coolTime.Clear();
            simpleView.Clear();
            filterIcons.Clear();
            foreach (string path in new[] { "System/OngoingQuestInfoList_True.lub", "System/OngoingQuestInfoList.lub", "System/OngoingQuests_C.lub" }) {
                if (assets.Exists(path)) ParseQuestList(assets.ReadText(path), coolTime, simpleView);
            }

            const string classification = "System/QuestClassificationInfo.lub";
            if (assets.Exists(classification)) ParseFilters(assets.ReadText(classification), filterIcons);
        }

        /// <summary>The <c>[id] = {</c> blocks with <c>CoolTimeQuest = 1</c>, and OngoingSimpleViewList's ids.</summary>
        public static void ParseQuestList(string lua, HashSet<uint> coolTimeIds, HashSet<uint> simpleViewIds) {
            if (string.IsNullOrEmpty(lua)) return;
            lua = StripBlockComments(lua);
            uint current = 0;
            bool inSimple = false;
            foreach (string raw in lua.Split('\n')) {
                string line = raw.Trim();
                if (line.StartsWith("OngoingSimpleViewList", StringComparison.Ordinal)) inSimple = true;
                if (inSimple) {
                    foreach (Match id in Regex.Matches(line, @"\b(\d+)\b")) simpleViewIds.Add(uint.Parse(id.Groups[1].Value, CultureInfo.InvariantCulture));
                    if (line.Contains("}")) inSimple = false;
                    continue;
                }

                Match head = Regex.Match(line, @"^\[(\d+)\]\s*=\s*\{");
                if (head.Success) {
                    current = uint.Parse(head.Groups[1].Value, CultureInfo.InvariantCulture);
                    continue;
                }

                Match cool = Regex.Match(line, @"^CoolTimeQuest\s*=\s*(\d+)");
                if (cool.Success && current != 0 && cool.Groups[1].Value == "1") coolTimeIds.Add(current);
            }
        }

        /// <summary>QuestFilterTable: <c>[IDs.NAME] = { … QuestIcons = { "a.bmp", … } }</c> with IDs' numbers.</summary>
        public static void ParseFilters(string lua, Dictionary<int, HashSet<string>> icons) {
            if (string.IsNullOrEmpty(lua)) return;
            lua = StripBlockComments(lua);
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(lua, @"\b([A-Z_]+)\s*=\s*(\d+)")) ids[m.Groups[1].Value] = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            int filter = -1;
            foreach (string raw in lua.Split('\n')) {
                string line = raw.Trim();
                Match head = Regex.Match(line, @"^\[IDs\.([A-Z_]+)\]\s*=\s*\{");
                if (head.Success) {
                    filter = ids.TryGetValue(head.Groups[1].Value, out int id) ? id : -1;
                    continue;
                }

                if (filter < 0 || !line.StartsWith("QuestIcons", StringComparison.Ordinal)) continue;
                if (!icons.TryGetValue(filter, out HashSet<string> set)) icons[filter] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match icon in Regex.Matches(line, "\"([^\"]+)\"")) set.Add(icon.Groups[1].Value);
            }
        }

        /// <summary>The --[[ … ]] blocks (the files' format templates) go.</summary>
        public static string StripBlockComments(string lua) =>
            string.IsNullOrEmpty(lua) ? lua : Regex.Replace(lua, @"--\[\[.*?\]\]", string.Empty, RegexOptions.Singleline);

        public static bool IsCoolTime(uint questId) {
            Load();
            return coolTime.Contains(questId);
        }

        /// <summary>The record's display byte: GetOngoingSimpleView (not in OngoingSimpleViewList), 0 for a cool-time quest.</summary>
        public static bool IsDisplayed(object owner, uint questId) {
            if (!ReferenceEquals(owner, displayOwner)) {
                display.Clear();
                displayOwner = owner;
            }

            if (display.TryGetValue(questId, out bool shown)) return shown;
            Load();
            shown = !simpleView.Contains(questId) && !coolTime.Contains(questId);
            display[questId] = shown;
            return shown;
        }

        /// <summary>FUN_006142e0: the list's check box writes the record's display byte.</summary>
        public static void SetDisplayed(object owner, uint questId, bool shown) {
            IsDisplayed(owner, questId);
            display[questId] = shown;
        }

        /// <summary>FUN_00613b20: 0 passes all, 1 an icon no other filter lists, 2..5 an icon of that filter.</summary>
        public static bool PassesFilter(int filter, string icon) {
            if (filter == FilterAll) return true;
            Load();
            if (filter == FilterEtc) {
                foreach (HashSet<string> set in filterIcons.Values) {
                    if (set.Contains(icon ?? string.Empty)) return false;
                }

                return true;
            }

            return filterIcons.TryGetValue(filter, out HashSet<string> icons) && icons.Contains(icon ?? string.Empty);
        }

        /// <summary>
        /// The hunt label (FUN_00c43080 / FUN_006120f0): a monster id (1001..3998, 20021..31999) keeps the packet's name, 0x1099 /
        /// 0x109a are msg 0xde6 / 0xde7, 0x287b names the map of "%s.rsw"; then the level bounds — upper only "%s Lv.%d" +
        /// A1 E5, lower only + A1 E3 (the CP949 arrows, drawn as their Windows-1252 bytes), both "%s Lv.%d~Lv.%d". The
        /// mob-type groups of the special marker 31999 (FUN_00610cd0) are not ported: it keeps the packet's name.
        /// </summary>
        public static string HuntLabel(RoQuestObjectiveRuntimeSnapshot objective) {
            int mob = unchecked((int) objective.MobId);
            string name = objective.MobName ?? string.Empty;
            bool monster = !((mob < 0x3e9 || mob > 0xf9e) && unchecked((uint) (mob - 0x4e35)) > 0x2eca);
            if (!monster) {
                if (mob == 0x1099) name = RoClassicUiHost.MsgStr(0xde6, name);
                else if (mob == 0x109a) name = RoClassicUiHost.MsgStr(0xde7, name);
                else if (mob == 0x287b) name = RoClassicUiHost.MapDisplayName(name);
            }

            int lo = objective.LevelMin, hi = objective.LevelMax;
            if (lo < 0 || hi < 0 || (lo == 0 && hi == 0)) return name;
            if (lo == 0) return name + " Lv." + hi.ToString(CultureInfo.InvariantCulture) + "¡å";
            if (hi == 0) return name + " Lv." + lo.ToString(CultureInfo.InvariantCulture) + "¡ã";
            return name + " Lv." + lo.ToString(CultureInfo.InvariantCulture) + "~Lv." + hi.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>FUN_009b73c0: the ^RRGGBB colour escapes go (seven bytes from the caret).</summary>
        public static string StripColours(string text) {
            if (string.IsNullOrEmpty(text) || text.IndexOf('^') < 0) return text ?? string.Empty;
            var s = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++) {
                if (text[i] == '^' && i + 6 < text.Length) {
                    i += 6;
                    continue;
                }

                s.Append(text[i]);
            }

            return s.ToString();
        }

        public static RoQuestMetadataTable Metadata() => RoQuestMetadataResolver.ResolveQuestMetadata();
    }
}
