using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Ragnarok.Assets;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>One RecommendedQuestInfoList entry (InsertRecommededQuestInfo + AddRecommendedQuestInfo groups 0..2).</summary>
    public sealed class RoRecommendedQuest {
        public uint Id;
        public string Title = string.Empty, IconName = string.Empty, Summary = string.Empty, BgName = string.Empty;
        public string NpcSpr = string.Empty, NpcNavi = string.Empty;
        public int NpcPosX, NpcPosY;
        public readonly List<string>[] Groups = { new List<string>(), new List<string>(), new List<string>() };
        public bool Active;
    }

    /// <summary>
    /// questinfo_f.lub's RecommendedQuestInfoLoad: RecommendedQuests_C merged over RecommendedQuestInfoList (System\
    /// RecommendedQuestInfoList_True.lub), RecommendedActiveList marks the active ones. Kept by id (the client's tree).
    /// </summary>
    public static class RoClassicQuestRecommended {
        private static RoAssetDatabase loadedFor;
        private static readonly SortedDictionary<uint, RoRecommendedQuest> quests = new SortedDictionary<uint, RoRecommendedQuest>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            loadedFor = null;
            quests.Clear();
        }

        public static IEnumerable<RoRecommendedQuest> All {
            get {
                Load();
                return quests.Values;
            }
        }

        public static RoRecommendedQuest Find(uint id) {
            Load();
            return quests.TryGetValue(id, out RoRecommendedQuest q) ? q : null;
        }

        private static void Load() {
            var map = UnityEngine.Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            if (assets == null || !assets.IsInitialized || ReferenceEquals(assets, loadedFor)) return;
            loadedFor = assets;
            quests.Clear();
            foreach (string path in new[] { "System/RecommendedQuestInfoList_True.lub", "System/RecommendedQuests_C.lub" }) {
                if (assets.Exists(path)) Parse(assets.ReadText(path), quests);
            }
        }

        /// <summary>
        /// <c>[id] = { Title = "…", …, QuestInfo1 = { "…", … }, … }</c> blocks; a later file's entry replaces the earlier
        /// one (F_LuaTableMerge), RecommendedActiveList = { ids } sets Active.
        /// </summary>
        public static void Parse(string lua, SortedDictionary<uint, RoRecommendedQuest> into) {
            if (string.IsNullOrEmpty(lua)) return;
            lua = RoClassicQuestData.StripBlockComments(lua);
            RoRecommendedQuest current = null;
            int group = -1;
            bool activeList = false;
            foreach (string raw in lua.Split('\n')) {
                string line = raw.Trim();
                if (line.StartsWith("--", StringComparison.Ordinal)) continue;
                if (line.StartsWith("RecommendedActiveList", StringComparison.Ordinal)) activeList = true;
                if (activeList) {
                    foreach (Match id in Regex.Matches(line, @"\b(\d+)\b")) {
                        if (into.TryGetValue(uint.Parse(id.Groups[1].Value, CultureInfo.InvariantCulture), out RoRecommendedQuest q)) q.Active = true;
                    }

                    if (line.Contains("}")) activeList = false;
                    continue;
                }

                Match head = Regex.Match(line, @"^\[(\d+)\]\s*=\s*\{");
                if (head.Success) {
                    current = new RoRecommendedQuest { Id = uint.Parse(head.Groups[1].Value, CultureInfo.InvariantCulture) };
                    into[current.Id] = current;
                    group = -1;
                    continue;
                }

                if (current == null) continue;
                Match info = Regex.Match(line, @"^QuestInfo([123])\s*=\s*\{");
                if (info.Success) {
                    group = info.Groups[1].Value[0] - '1';
                    AddStrings(line.Substring(info.Length), current.Groups[group]);
                    if (line.EndsWith("}", StringComparison.Ordinal) || line.EndsWith("},", StringComparison.Ordinal)) group = -1;
                    continue;
                }

                if (group >= 0) {
                    if (line.StartsWith("}", StringComparison.Ordinal)) {
                        group = -1;
                        continue;
                    }

                    AddStrings(line, current.Groups[group]);
                    continue;
                }

                Match field = Regex.Match(line, "^([A-Za-z]+)\\s*=\\s*(\"(.*)\"|(-?\\d+))");
                if (!field.Success) continue;
                string text = field.Groups[3].Success ? field.Groups[3].Value : string.Empty;
                int number = field.Groups[4].Success ? int.Parse(field.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
                switch (field.Groups[1].Value) {
                    case "Title": current.Title = text; break;
                    case "IconName": current.IconName = text; break;
                    case "Summary": current.Summary = text; break;
                    case "BgName": current.BgName = text; break;
                    case "NpcSpr": current.NpcSpr = text; break;
                    case "NpcNavi": current.NpcNavi = text; break;
                    case "NpcPosX": current.NpcPosX = number; break;
                    case "NpcPosY": current.NpcPosY = number; break;
                }
            }
        }

        private static void AddStrings(string line, List<string> into) {
            foreach (Match s in Regex.Matches(line, "\"((?:[^\"\\\\]|\\\\.)*)\"")) into.Add(s.Groups[1].Value);
        }
    }
}
