using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The achievement data (achievement-spec §0, §1, §8.1): CAchievementMgr's System\achievement_list.lub (tabs keyed
    /// major · 10 + minor, hidden ones in no tab), the session's states from ZC_ALL_ACH_LIST 0x0a23 / ZC_ACH_UPDATE 0x0a24 /
    /// ZC_REQ_ACH_REWARD_ACK 0x0a26 and the tracing list (three at most, session memory). A completion seen in 0x0a24 shows
    /// the "&lt; %s &gt; achieved." banner for 10 s, plays EF_ACH_COMPLETE 1094 on the own character and lights the
    /// menu icon's "new" badge.
    /// </summary>
    public sealed partial class RoClassicAchievements {
        internal sealed class Resource {
            public string Text;
            public int Count;
            public int Shortcut = -1;
        }

        internal sealed class Info {
            public int Id;
            public string Title = string.Empty, Summary = string.Empty, Details = string.Empty, Group = string.Empty;
            public int Score, RewardItem, RewardTitle, RewardBuff, UiType;
            public readonly List<Resource> Resources = new List<Resource>();
            public bool HasReward => RewardItem > 0 || RewardTitle > 0 || RewardBuff > 0;
        }

        internal struct State {
            public bool Completed;
            public uint[] Objective;
            public uint CompletedAt;
            public bool Rewarded;
            public int Year, Month, Day;
            public uint Obj(int i) => Objective != null && i < Objective.Length ? Objective[i] : 0;
        }

        internal sealed class Traced {
            public bool Expanded, Completed;
            public int Id;
            public string Title = string.Empty;
            public readonly List<(string Text, bool Done)> Lines = new List<(string, bool)>();
        }

        private static readonly Dictionary<int, Info> infos = new Dictionary<int, Info>();
        private static readonly SortedDictionary<int, List<int>> tabs = new SortedDictionary<int, List<int>>();
        private static readonly Dictionary<int, State> states = new Dictionary<int, State>();
        private static readonly List<Traced> traced = new List<Traced>();
        private static bool loaded;
        private static uint totalScore, rankPoints, nextRankPoints;
        private static int rank;
        private static bool newBadge;
        // session +0x5d4c owned titles (sorted by name), +0x5d58 "new" titles, DAT_01553a48 the current title
        private static readonly List<int> ownedTitles = new List<int>();
        private static readonly List<int> newTitles = new List<int>();
        private static int currentTitle;
        private static int titlesVersion;

        /// <summary>The menu icon 0x1d5's "new" variant: on after a completion, off when the window opens.</summary>
        public static bool HasNewBadge => newBadge;

        /// <summary>The titles of the achievements whose reward was received, sorted by name (session +0x5d4c).</summary>
        internal static IReadOnlyList<int> OwnedTitles => ownedTitles;

        /// <summary>The worn title (DAT_01553a48): ZC_ACK_CHANGE_TITLE 0x0a2f, or the own ZC_ACK_REQNAMEALL2 0x0a30; 0 = none.</summary>
        internal static int CurrentTitle => currentTitle;

        /// <summary>Changes when the title list or the current title changes (the equip window's msg 0x3c).</summary>
        internal static int TitlesVersion => titlesVersion;

        /// <summary>Titles got from a reward ACK since the equip window was last closed (drawn on #FAC092).</summary>
        internal static bool IsNewTitle(int titleId) => newTitles.Contains(titleId);

        /// <summary>DeleteWindow(10) 0x9bd7ed: closing the equip window forgets the "new" titles.</summary>
        internal static void ClearNewTitles() {
            if (newTitles.Count == 0) return;
            newTitles.Clear();
            titlesVersion++;
        }

        /// <summary>The own ZC_ACK_REQNAMEALL2 0x0a30's title (0xc80390): no window refresh, the header follows on its next draw.</summary>
        internal static void SetCurrentTitle(int titleId) => currentTitle = titleId;

        /// <summary>0xd192c0 GetTitleString: the title table's name, "" for an unknown id.</summary>
        internal static string TitleNameOf(int titleId) => TitleName(titleId);

        /// <summary>
        /// AddOwnedTitle 0xd0d040: the achievement's title reward joins the owned titles (sorted by name, byte order; the
        /// id breaks ties); −1 when it has none or it is owned already.
        /// </summary>
        private static int AddOwnedTitle(int achievementId) {
            if (!TryInfo(achievementId, out Info info) || info.RewardTitle <= 0 || ownedTitles.Contains(info.RewardTitle)) return -1;
            ownedTitles.Add(info.RewardTitle);
            ownedTitles.Sort((a, b) => {
                int c = string.CompareOrdinal(TitleName(a), TitleName(b));
                return c != 0 ? c : a.CompareTo(b);
            });
            titlesVersion++;
            return info.RewardTitle;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetData() {
            infos.Clear();
            tabs.Clear();
            states.Clear();
            traced.Clear();
            loaded = false;
            totalScore = rankPoints = nextRankPoints = 0;
            rank = 0;
            newBadge = false;
            ownedTitles.Clear();
            newTitles.Clear();
            currentTitle = 0;
            titlesVersion = 0;
        }

        private static void Load() {
            if (loaded) return;
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null) return;
            loaded = true;
            byte[] bytes = assets.ReadBytes("System/achievement_list.lub");
            if ((bytes == null || bytes.Length == 0) && assets.TryFindFirstPathEndingWith("achievement_list.lub", out string path)) bytes = assets.ReadBytes(path);
            if (bytes == null || bytes.Length == 0) return;
            RoWorldMapLuaExecutionContext context = RoWorldMapLuaParser.ParseContext(new[] { new RoWorldMapLuaFile("achievement_list.lub", bytes) });
            LuaValue root = context.GetGlobal("achievement_tbl");
            if (root.Kind != LuaValueKind.Table) return;
            IReadOnlyList<LuaValue> array = root.Table.ArrayValues;
            for (int i = 0; i < array.Count; i++) {
                if (array[i].Kind == LuaValueKind.Table) Add(i + 1, array[i].Table);
            }

            foreach (KeyValuePair<string, LuaValue> field in root.Table.Fields) {
                if (field.Value.Kind == LuaValueKind.Table && int.TryParse(field.Key, out int id)) Add(id, field.Value.Table);
            }
        }

        /// <summary>main(): InsertAchieveInfo / Resource / Reward* / UIType, SetAchieveIDByTab unless hidden.</summary>
        private static void Add(int id, LuaTableValue t) {
            var info = new Info {
                Id = id, Title = Str(t.Get("title")), Score = (int) t.Get("score").Number, UiType = (int) t.Get("UI_Type").Number,
                Group = Str(t.Get("group")),
            };
            LuaValue content = t.Get("content");
            if (content.Kind == LuaValueKind.Table) {
                info.Summary = Str(content.Table.Get("summary"));
                info.Details = Str(content.Table.Get("details"));
            }

            LuaValue resources = t.Get("resource");
            if (resources.Kind == LuaValueKind.Table) {
                IReadOnlyList<LuaValue> list = resources.Table.ArrayValues;
                for (int i = 0; i < list.Count; i++) {
                    if (list[i].Kind != LuaValueKind.Table) break;   // ipairs stops at the first hole
                    LuaTableValue r = list[i].Table;
                    LuaValue shortcut = r.Get("shortcut");
                    info.Resources.Add(new Resource {
                        Text = Str(r.Get("text")), Count = info.UiType == 1 ? (int) r.Get("count").Number : 0,
                        Shortcut = shortcut.Kind == LuaValueKind.Number ? (int) shortcut.Number : -1,
                    });
                }
            }

            LuaValue reward = t.Get("reward");
            if (reward.Kind == LuaValueKind.Table) {
                info.RewardItem = (int) reward.Table.Get("item").Number;
                info.RewardTitle = (int) reward.Table.Get("title").Number;
                info.RewardBuff = (int) reward.Table.Get("buff").Number;
            }

            infos[id] = info;
            LuaValue hidden = t.Get("hidden");
            if (hidden.Kind == LuaValueKind.Boolean && hidden.Boolean) return;
            int key = (int) t.Get("major").Number * 10 + (int) t.Get("minor").Number;
            if (!tabs.TryGetValue(key, out List<int> ids)) tabs[key] = ids = new List<int>();
            if (!ids.Contains(id)) ids.Add(id);
        }

        private static string Str(LuaValue v) => v.Kind == LuaValueKind.String ? v.Text ?? string.Empty : v.Kind == LuaValueKind.Nil ? string.Empty : v.ToDisplayString();

        internal static bool TryInfo(int id, out Info info) {
            Load();
            return infos.TryGetValue(id, out info);
        }

        internal static State StateOf(int id) => states.TryGetValue(id, out State s) ? s : default;

        /// <summary>0x5f9a20: the ids of (major, minor), minor −1 = every minor of the major, ascending.</summary>
        internal static List<int> TabIds(int major, int minor) {
            Load();
            var ids = new List<int>();
            foreach (KeyValuePair<int, List<int>> tab in tabs) {
                if (minor < 0 ? tab.Key / 10 == major && tab.Key >= major * 10 && tab.Key < major * 10 + 10 : tab.Key == major * 10 + minor) ids.AddRange(tab.Value);
            }

            ids.Sort();
            return ids;
        }

        /// <summary>0x5fa6d0 / 0x5fa850: total and completed of (major, minor); major 0 = every achievement.</summary>
        internal static (int Completed, int Total) Count(int major, int minor) {
            Load();
            int done = 0, total = 0;
            if (major == 0) {
                foreach (int id in infos.Keys) {
                    total++;
                    if (StateOf(id).Completed) done++;
                }

                return (done, total);
            }

            foreach (int id in TabIds(major, minor)) {
                total++;
                if (StateOf(id).Completed) done++;
            }

            return (done, total);
        }

        /// <summary>0x5fac20: the first tab of an id (false for hidden ones).</summary>
        internal static bool TabOf(int id, out int major, out int minor) {
            Load();
            foreach (KeyValuePair<int, List<int>> tab in tabs) {
                if (!tab.Value.Contains(id)) continue;
                major = tab.Key / 10;
                minor = tab.Key % 10;
                return true;
            }

            major = minor = 0;
            return false;
        }

        // ---------------------------------------------------------------- packets (§1)

        /// <summary>The raw 0x0a23 / 0x0a24 / 0x0a26 / 0x0a2f frames (the entry queue's carrier).</summary>
        internal static void Handle(byte[] p) {
            if (p == null || p.Length < 2) return;
            Load();
            switch (BitConverter.ToUInt16(p, 0)) {
                case 0x0a23 when p.Length >= 22: {
                    // 0xc8b950
                    int count = BitConverter.ToInt32(p, 4);
                    Globals(p, 8);
                    for (int i = 0; i < count && 22 + 50 * (i + 1) <= p.Length; i++) {
                        Store(p, 22 + 50 * i);
                        // the received rewards' titles are owned
                        if (p[22 + 50 * i + 49] != 0) AddOwnedTitle(BitConverter.ToInt32(p, 22 + 50 * i));
                    }

                    RefreshWindow();
                    break;
                }
                case 0x0a24 when p.Length >= 66: {
                    // 0xc87360
                    int id = BitConverter.ToInt32(p, 16);
                    bool wasDone = StateOf(id).Completed;
                    if (!wasDone && p[20] != 0) Completed(id);
                    Globals(p, 2);
                    Store(p, 16);
                    RefreshWindow();
                    RefreshTraced(id);
                    if (traced.Count > 0) OpenTracker();
                    instance?.tracker?.Invalidate();
                    break;
                }
                case 0x0a26 when p.Length >= 7:
                    // 0xc878e0: refresh on success, nothing on failure; a new title also counts as "new" (0xd0df60)
                    if (p[2] == 0) {
                        int title = AddOwnedTitle(BitConverter.ToInt32(p, 3));
                        if (title > 0 && !newTitles.Contains(title)) newTitles.Add(title);
                        RefreshWindow();
                    }

                    break;
                case 0x0a2f when p.Length >= 7:
                    // ZC_ACK_CHANGE_TITLE 0xc88970 {u8 result; u32 title}: on success the current title and the own name
                    // card's title; nothing at all on failure
                    if (p[2] == 0) {
                        currentTitle = BitConverter.ToInt32(p, 3);
                        titlesVersion++;
                        UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>()?.SetOwnTitle(currentTitle, TitleName(currentTitle));
                    }

                    break;
            }
        }

        private static void Globals(byte[] p, int at) {
            totalScore = BitConverter.ToUInt32(p, at);
            rank = BitConverter.ToUInt16(p, at + 4);
            rankPoints = BitConverter.ToUInt32(p, at + 6);
            nextRankPoints = BitConverter.ToUInt32(p, at + 10);
        }

        /// <summary>0xd0db40: ach_list_info {u32 id; u8 completed; u32 objective[10]; u32 completed_at; u8 reward}.</summary>
        private static void Store(byte[] p, int at) {
            int id = BitConverter.ToInt32(p, at);
            var s = new State { Completed = p[at + 4] != 0, Objective = new uint[10], CompletedAt = BitConverter.ToUInt32(p, at + 45), Rewarded = p[at + 49] != 0 };
            for (int i = 0; i < 10; i++) s.Objective[i] = BitConverter.ToUInt32(p, at + 5 + 4 * i);
            DateTime local = DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, (long) s.CompletedAt)).LocalDateTime;
            s.Year = local.Year;
            s.Month = local.Month;
            s.Day = local.Day;
            states[id] = s;
        }

        /// <summary>The completion: the banner (reused within 10 s), EF_ACH_COMPLETE on the own character, the menu badge.</summary>
        private static void Completed(int id) {
            string title = TryInfo(id, out Info info) ? info.Title : string.Empty;
            ShowBanner(RoClassicUiHost.MsgStr(0xa79, "< %s > achieved.").Replace("%s", title));
            RoOnlineWorldController world = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            if (world != null && world.OwnActorId != 0) {
                world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcNotifyEffectById(world.OwnActorId, 1094, 0, false)));
            }

            newBadge = true;
        }

        // ---------------------------------------------------------------- tracing (§8.1)

        internal static bool IsTraced(int id) => traced.Exists(t => t.Id == id);

        /// <summary>0xd0d100: untrace, or trace (at most 3, else MsgStr 0xa78 in #FFFF64); true when the window stays.</summary>
        internal static bool ToggleTrace(int id) {
            int at = traced.FindIndex(t => t.Id == id);
            if (at >= 0) {
                traced.RemoveAt(at);
                if (traced.Count == 0) {
                    CloseTracker();
                    return false;
                }

                return true;
            }

            if (traced.Count >= 3) {
                RoClassicChat.Instance?.AddLine(RoClassicUiHost.MsgStr(0xa78, "You cannot add anymore."), "#FFFF64");
                return false;
            }

            var entry = new Traced { Id = id };
            traced.Add(entry);
            Rebuild(entry);
            return true;
        }

        private static void RefreshTraced(int id) {
            Traced entry = traced.Find(t => t.Id == id);
            if (entry != null) Rebuild(entry);
        }

        private static int ByteCount(string s) => RoLoginShellSession.TextBytes(s ?? string.Empty).Length;

        /// <summary>The traced lines: "- %s" (cut at 40 bytes with "..."), with "(%d/%d)" for counted objectives.</summary>
        private static void Rebuild(Traced entry) {
            entry.Lines.Clear();
            State s = StateOf(entry.Id);
            entry.Completed = s.Completed;
            if (!TryInfo(entry.Id, out Info info)) return;
            entry.Title = info.Title;
            for (int i = 0; i < info.Resources.Count && i < 10; i++) {
                Resource r = info.Resources[i];
                string text = r.Text ?? string.Empty;
                if (info.UiType == 1) {
                    string p = "(" + s.Obj(i) + "/" + r.Count + ")";
                    int room = 40 - ByteCount(p);
                    entry.Lines.Add((ByteCount(text) + ByteCount(p) < 41 ? "- " + text + " " + p : "- " + Cut(text, room) + "..." + p, s.Obj(i) == r.Count));
                } else {
                    entry.Lines.Add((ByteCount(text) < 41 ? "- " + text : "- " + Cut(text, 40) + "...", s.Obj(i) > 0));
                }
            }
        }

        private static string Cut(string text, int bytes) {
            int n = 0, i = 0;
            while (i < text.Length && n + ByteCount(text.Substring(i, 1)) <= bytes) {
                n += ByteCount(text.Substring(i, 1));
                i++;
            }

            return text.Substring(0, i);
        }
    }
}
