using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Data.Lua51;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// CCashEmotionMgr's data ([0x11a6124], loader 0x5775c0): the emotion packs and their emotions as the client's Lua builds
    /// them — data\luafiles514\lua files\cashemotion\ CashEmotionListDefine(_F), CashEmotionList(_F) (main_emotion) and
    /// CashEmotionClientSetting, with Lua Files\Common for <c>enum</c> — run through our Lua 5.1 VM with the c_ bindings
    /// recording what they get; plus simplemsg\msg_emotion.csv (the EMSG_ texts, base64, the third column). Pure data.
    /// </summary>
    public sealed class RoCashEmotionData {
        public const string LuaRoot = "data\\luafiles514\\lua files\\";
        public const string MessagePath = "data\\simplemsg\\msg_emotion.csv";

        /// <summary>One record of c_InsertEmotionListTable / c_InsertEmotionVarientListTable (FUN_00578130).</summary>
        public sealed class Emotion {
            public int Pack;
            public int Id;
            public string MsgKey = string.Empty;   // "MSI_EMOTION_…" / "EMSG_…" (the list table's name)
            public int Action;                    // ACT action (+0x20)
            public int Motion;                    // the frame the list shows (+0x24)
            public string Act = string.Empty;     // relative to data\sprite\
            public string Spr = string.Empty;
            public bool Variant;                  // a cash pack's skin of an emotion of the basic pack
            public readonly List<string> Words = new List<string>(2);   // c_InsertEmotionMsgListTable: the chat words
            /// <summary>
            /// The text the record keeps (FUN_00578130's third argument, 0x57c750): msg_emotion.csv's line for the key, else the
            /// msgstringtable line of the MSI name ("/lv" for MSI_EMOTION_THROB); what a click on it puts in the chat.
            /// </summary>
            public string Text = string.Empty;
        }

        /// <summary>One c_InsertPackInfo (0x57d520) record.</summary>
        public sealed class Pack {
            public int Id;
            public string NameKey = string.Empty;  // EMSG_EPT_…
            public int SaleType;                   // 0 free, 1 sold
            public int Price;                      // Nyangvine fruits
            public int DateType;                   // 0 no end date, 1 an end date
            public int SaleStart;                  // yyyymmdd, 0 none
            public int SaleEnd;
            public int UseType;                    // 0 kept, 1 for UseDays days
            public int UseDays;
            public int Shown;                      // the 10th argument
            public readonly List<int> Order = new List<int>(16);   // c_InsertOrderList
        }

        private readonly Dictionary<long, Emotion> emotions = new Dictionary<long, Emotion>();
        private readonly Dictionary<int, Pack> packs = new Dictionary<int, Pack>();
        private readonly List<int> packIds = new List<int>(4);
        private readonly Dictionary<string, int> enums = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> messages = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, (int Pack, int Id)> words = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        private readonly List<(string Word, int Pack, int Id)> searchWords = new List<(string, int, int)>(128);
        private readonly Dictionary<string, List<(int Pack, int Id)>> wordMap = new Dictionary<string, List<(int, int)>>(StringComparer.Ordinal);
        private Func<int, string> msgStr;

        /// <summary>The MSI_EMOTION_ names of the client's name table (0xfbd420, read by 0xa2a670) and their msgstringtable lines.</summary>
        public static readonly IReadOnlyDictionary<string, int> MsiIds = new Dictionary<string, int>(StringComparer.Ordinal) {
            ["MSI_EMOTION_SURPRISE"] = 544, ["MSI_EMOTION_QUESTION"] = 545, ["MSI_EMOTION_DELIGHT"] = 546,
            ["MSI_EMOTION_THROB"] = 547, ["MSI_EMOTION_BIGTHROB"] = 548, ["MSI_EMOTION_SWEAT"] = 549, ["MSI_EMOTION_AHA"] = 550,
            ["MSI_EMOTION_FRET"] = 551, ["MSI_EMOTION_ANGER"] = 552, ["MSI_EMOTION_MONEY"] = 553, ["MSI_EMOTION_THINK"] = 554,
            ["MSI_EMOTION_THANKS"] = 555, ["MSI_EMOTION_KEK"] = 556, ["MSI_EMOTION_SORRY"] = 557, ["MSI_EMOTION_SMILE"] = 558,
            ["MSI_EMOTION_PROFUSELY_SWEAT"] = 559, ["MSI_EMOTION_SCRATCH"] = 560, ["MSI_EMOTION_BEST"] = 561,
            ["MSI_EMOTION_STARE_ABOUT"] = 562, ["MSI_EMOTION_HUK"] = 563, ["MSI_EMOTION_O"] = 564, ["MSI_EMOTION_X"] = 565,
            ["MSI_EMOTION_HELP"] = 566, ["MSI_EMOTION_GO"] = 567, ["MSI_EMOTION_CRY"] = 568, ["MSI_EMOTION_KIK"] = 569,
            ["MSI_EMOTION_CHUP"] = 570, ["MSI_EMOTION_CHUPCHUP"] = 571, ["MSI_EMOTION_HNG"] = 572, ["MSI_EMOTION_OK"] = 573,
            ["MSI_EMOTION_STARE"] = 772, ["MSI_EMOTION_HUNGRY"] = 773, ["MSI_EMOTION_COOL"] = 774, ["MSI_EMOTION_MERONG"] = 775,
            ["MSI_EMOTION_SHY"] = 776, ["MSI_EMOTION_GOODBOY"] = 777, ["MSI_EMOTION_SPTIME"] = 778, ["MSI_EMOTION_SEXY"] = 779,
            ["MSI_EMOTION_COMEON"] = 780, ["MSI_EMOTION_SLEEPY"] = 781, ["MSI_EMOTION_CONGRATULATION"] = 782,
            ["MSI_EMOTION_HPTIME"] = 783, ["MSI_EMOTION_SPARK"] = 945, ["MSI_EMOTION_CONFUSE"] = 946, ["MSI_EMOTION_OHNO"] = 947,
            ["MSI_EMOTION_HUM"] = 948, ["MSI_EMOTION_BLABLA"] = 949, ["MSI_EMOTION_OTL"] = 950, ["MSI_EMOTION_DICE"] = 951,
            ["MSI_EMOTION_ROCK"] = 1501, ["MSI_EMOTION_SCISSOR"] = 1502, ["MSI_EMOTION_WRAP"] = 1503, ["MSI_EMOTION_LUV"] = 1504,
            ["MSI_EMOTION_MOBILE"] = 1505, ["MSI_EMOTION_MAIL"] = 1506, ["MSI_EMOTION_ANTENNA0"] = 1507,
            ["MSI_EMOTION_ANTENNA1"] = 1508, ["MSI_EMOTION_ANTENNA2"] = 1509, ["MSI_EMOTION_ANTENNA3"] = 1510,
            ["MSI_EMOTION_HUM2"] = 1511, ["MSI_EMOTION_ABS"] = 1512, ["MSI_EMOTION_OOPS"] = 1513, ["MSI_EMOTION_SPIT"] = 1514,
            ["MSI_EMOTION_ENE"] = 1515, ["MSI_EMOTION_PANIC"] = 1516, ["MSI_EMOTION_WHISP"] = 1517,
        };

        /// <summary>
        /// The chat words in the order c_InsertEmotionMsgListTable gave them, as written ("/heart"), with their pack and emotion:
        /// the list (mgr +0x74) the Search tab walks.
        /// </summary>
        public IReadOnlyList<(string Word, int Pack, int Id)> SearchWords => searchWords;

        /// <summary>
        /// Mgr +0x158 (0x578060): a word without its leading '/' → the (pack, emotion)s it names, in order — every word of
        /// c_InsertEmotionMsgListTable, and for c_InsertEmotionMsgListTableForCustom the basic emotion's text without its
        /// first character.
        /// </summary>
        public IReadOnlyList<(int Pack, int Id)> WordTargets(string word) =>
            word != null && wordMap.TryGetValue(word, out List<(int, int)> list) ? list : (IReadOnlyList<(int Pack, int Id)>) Array.Empty<(int, int)>();

        /// <summary>The basic pack ([0x11a6124]+0xc; EMOTION_PACK_TYPE.EPT_BASIC).</summary>
        public int BasicPack { get; private set; }

        /// <summary>The packs in the order the Lua inserted them (the basic one first).</summary>
        public IReadOnlyList<int> PackIds => packIds;

        public bool TryGetPack(int pack, out Pack info) => packs.TryGetValue(pack, out info);

        /// <summary>FUN_00576540: the record of (pack, emotion); negative keys find nothing.</summary>
        public bool TryGet(int pack, int id, out Emotion emotion) {
            emotion = null;
            return pack >= 0 && id >= 0 && emotions.TryGetValue(Key(pack, id), out emotion);
        }

        /// <summary>The emotions of a pack in its order list (the list's order; records without an order entry are left out).</summary>
        public IReadOnlyList<int> Order(int pack) => packs.TryGetValue(pack, out Pack info) ? info.Order : (IReadOnlyList<int>) Array.Empty<int>();

        /// <summary>The EMOTION_TYPE value of an "ET_…" name (c_InsertEmotionEnum), or −1.</summary>
        public int EnumValue(string name) => name != null && enums.TryGetValue(name, out int value) ? value : -1;

        /// <summary>An EMSG_ text of msg_emotion.csv (0x708ba0), or <paramref name="fallback"/>.</summary>
        public string Message(string key, string fallback = "") => key != null && messages.TryGetValue(key, out string text) ? text : fallback;

        /// <summary>The (pack, emotion) a chat word ("/heart") names, from c_InsertEmotionMsgListTable.</summary>
        public bool TryGetWord(string word, out int pack, out int id) {
            pack = id = -1;
            if (word == null || !words.TryGetValue(word, out (int Pack, int Id) found)) return false;
            pack = found.Pack;
            id = found.Id;
            return true;
        }

        private static long Key(int pack, int id) => ((long) pack << 32) | (uint) id;

        // ---- building ----

        public void AddEnum(string name, int value) {
            if (!string.IsNullOrEmpty(name)) enums[name] = value;
        }

        public void AddPack(Pack pack) {
            if (pack == null) return;
            if (!packs.ContainsKey(pack.Id)) packIds.Add(pack.Id);
            if (packs.TryGetValue(pack.Id, out Pack old)) pack.Order.AddRange(old.Order);
            packs[pack.Id] = pack;
        }

        private Pack EnsurePack(int id) {
            if (packs.TryGetValue(id, out Pack pack)) return pack;
            pack = new Pack { Id = id };
            packs[id] = pack;
            packIds.Add(id);
            return pack;
        }

        public void AddEmotion(Emotion emotion) {
            if (emotion == null) return;
            EnsurePack(emotion.Pack);
            if (emotions.TryGetValue(Key(emotion.Pack, emotion.Id), out Emotion old)) emotion.Words.AddRange(old.Words);
            emotions[Key(emotion.Pack, emotion.Id)] = emotion;
        }

        public void AddOrder(int pack, int id) => EnsurePack(pack).Order.Add(id);

        /// <summary>c_InsertEmotionMsgListTable (0x57cad0): each word into the search list as written, and into the word map without its '/'.</summary>
        public void AddWords(int pack, int id, IEnumerable<string> list) {
            emotions.TryGetValue(Key(pack, id), out Emotion emotion);
            foreach (string word in list) {
                if (string.IsNullOrEmpty(word)) continue;
                emotion?.Words.Add(word);
                if (!words.ContainsKey(word)) words[word] = (pack, id);
                searchWords.Add((word, pack, id));
                AddMapWord(word[0] == '/' ? word.Substring(1) : word, pack, id);
            }
        }

        /// <summary>
        /// c_InsertEmotionMsgListTableForCustom (0x57d030): the basic emotion's text without its first character names the
        /// custom emotion too (word map only; nothing for a text of one character or less).
        /// </summary>
        public void AddCustomWord(int pack, int id, int basicEmotion) {
            if (!TryGet(BasicPack, basicEmotion, out Emotion basic) || basic.Text.Length <= 1) return;
            AddMapWord(basic.Text.Substring(1), pack, id);
        }

        /// <summary>0x578060: a word's list gets the (pack, emotion) once.</summary>
        private void AddMapWord(string word, int pack, int id) {
            if (word.Length == 0) return;
            if (!wordMap.TryGetValue(word, out List<(int, int)> list)) wordMap[word] = list = new List<(int, int)>(2);
            if (!list.Contains((pack, id))) list.Add((pack, id));
        }

        /// <summary>A record's text (0x57c750): msg_emotion.csv's line for the key, else the MSI name's msgstringtable line, else empty.</summary>
        public string ResolveText(string key) {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (messages.TryGetValue(key, out string text)) return text;
            return msgStr != null && MsiIds.TryGetValue(key, out int id) ? msgStr(id) ?? string.Empty : string.Empty;
        }

        public void AddMessage(string key, string text) {
            if (!string.IsNullOrEmpty(key)) messages[key] = text ?? string.Empty;
        }

        // ---- loading ----

        private static RoCashEmotionData shared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => shared = null;

        /// <summary>The client's data from the GRFs, loaded once (null while the game's assets are not up).</summary>
        public static RoCashEmotionData Shared {
            get {
                if (shared != null) return shared;
                Ragnarok.Assets.RoAssetDatabase assets = RoClassicUiHost.GameAssets();
                if (assets == null) return null;
                shared = Load(path => {
                    try {
                        return assets.ReadBytes(path);
                    } catch (Exception) {
                        return null;
                    }
                }, id => RoClassicUiHost.MsgStr(id, string.Empty));
                return shared;
            }
        }

        /// <summary>Runs the Lua files (missing ones skipped) and reads the message table through <paramref name="read"/>.</summary>
        public static RoCashEmotionData Load(Func<string, byte[]> read, Func<int, string> msgStr = null) {
            // the texts first: c_InsertEmotionListTable resolves each record's text when it runs (0x57c750)
            var data = new RoCashEmotionData { msgStr = msgStr };
            data.LoadMessages(read(MessagePath));
            try {
                var lua = new LuaState { InstructionLimit = 50_000_000 };
                data.RegisterBindings(lua);
                foreach (string file in new[] {
                    "common.lub", "cashemotion\\cashemotionlistdefine.lub", "cashemotion\\cashemotionlistdefine_f.lub",
                    "cashemotion\\cashemotionlist_f.lub", "cashemotion\\cashemotionlist.lub", "cashemotion\\cashemotionclientsetting.lub",
                }) {
                    byte[] chunk = read(LuaRoot + file);
                    if (chunk != null && chunk.Length > 4 && chunk[0] == 0x1b) lua.DoChunk(chunk, file);
                }

                LuaValue basic = lua.GetPath("EMOTION_PACK_TYPE.EPT_BASIC");
                if (basic.IsNumber) data.BasicPack = basic.ToInteger();
                LuaValue main = lua.GetGlobal("main_emotion");
                if (main.IsFunction) lua.Call(main);
                if (!data.packs.ContainsKey(data.BasicPack)) data.EnsurePack(data.BasicPack);
            } catch (Exception e) {
                Debug.LogWarning("[cashemotion] " + e.Message);
            }

            return data;
        }

        /// <summary>A Lua string's bytes (our VM keeps one char per byte) read as UTF-8, as the client's FUN_00a23430 does first.</summary>
        private static string Utf8(LuaValue v) {
            string s = v.AsString;
            if (string.IsNullOrEmpty(s)) return v.IsNumber ? v.ToInteger().ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
            var bytes = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) bytes[i] = (byte) s[i];
            return Encoding.UTF8.GetString(bytes);
        }

        private void RegisterBindings(LuaState lua) {
            lua.Register("c_InsertEmotionEnum", a => {
                AddEnum(Utf8(a[0]), a[1].ToInteger());
                return LuaReturn.None;
            });
            lua.Register("c_InsertPackInfo", a => {
                AddPack(new Pack {
                    Id = a[0].ToInteger(), NameKey = Utf8(a[1]), SaleType = a[2].ToInteger(), Price = a[3].ToInteger(), DateType = a[4].ToInteger(),
                    SaleStart = a[5].ToInteger(), SaleEnd = a[6].ToInteger(), UseType = a[7].ToInteger(), UseDays = a[8].ToInteger(), Shown = a[9].ToInteger(),
                });
                return LuaReturn.None;
            });
            lua.Register("c_InsertEmotionListTable", a => {
                string key = Utf8(a[2]);
                AddEmotion(new Emotion {
                    Pack = a[0].ToInteger(), Id = a[1].ToInteger(), MsgKey = key, Action = a[3].ToInteger(), Motion = a[4].ToInteger(),
                    Act = Utf8(a[5]), Spr = Utf8(a[6]), Text = ResolveText(key),
                });
                return LuaReturn.None;
            });
            lua.Register("c_InsertEmotionVarientListTable", a => {
                AddEmotion(new Emotion {
                    Pack = a[0].ToInteger(), Id = a[1].ToInteger(), Action = a[2].ToInteger(), Motion = a[3].ToInteger(),
                    Act = Utf8(a[4]), Spr = Utf8(a[5]), Variant = true,
                });
                return LuaReturn.None;
            });
            lua.Register("c_InsertEmotionMsgListTable", a => {
                // (pack, emotion, ansi, n, word…)
                int n = a[3].ToInteger();
                var list = new List<string>(n);
                for (int i = 0; i < n; i++) list.Add(Utf8(a[4 + i]));
                AddWords(a[0].ToInteger(), a[1].ToInteger(), list);
                return LuaReturn.None;
            });
            lua.Register("c_InsertEmotionMsgListTableForCustom", a => {
                // (pack, emotion, basic emotion): the basic emotion's text names the custom one in the word map
                AddCustomWord(a[0].ToInteger(), a[1].ToInteger(), a[2].ToInteger());
                return LuaReturn.None;
            });
            lua.Register("c_InsertOrderList", a => {
                AddOrder(a[0].ToInteger(), a[1].ToInteger());
                return LuaReturn.None;
            });
            foreach (string name in new[] { "c_InsertEmotionGroup", "c_InsertSequenceEmotion", "c_InsertEmotionGroupInfo" }) {
                lua.Register(name, a => LuaReturn.None);
            }
        }

        /// <summary>msg_emotion.csv: "base64(key),base64(note),base64(text)" per line, UTF-8; "\n" and "\"" in the text unescaped.</summary>
        public void LoadMessages(byte[] csv) {
            if (csv == null) return;
            string text = Encoding.ASCII.GetString(csv);
            foreach (string line in text.Split('\n')) {
                string[] cols = line.Trim('\r', ' ').Split(',');
                if (cols.Length < 3) continue;
                string key = DecodeBase64(cols[0]), value = DecodeBase64(cols[2]);
                if (key.Length == 0) continue;
                AddMessage(key, value.Replace("\\n", "\n").Replace("\\\"", "\""));
            }
        }

        private static string DecodeBase64(string s) {
            try {
                return Encoding.UTF8.GetString(Convert.FromBase64String(s.Trim()));
            } catch (FormatException) {
                return string.Empty;
            }
        }
    }
}
