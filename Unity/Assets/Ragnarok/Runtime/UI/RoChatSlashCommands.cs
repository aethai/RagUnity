using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The client's own slash commands (Custom game.exe, docs/audits/chat-parity-2026-10-03.md §2): a typed line that
    /// starts with '/' never goes to the server. The chat send (msg 6 / 0xb4, asm 0x899053) hands it to CSession's parser
    /// 0xced690, which lower-cases the first word (0xa22b50, ASCII only), tries its named forms first (/organize, /expel,
    /// /invite, /guildinvite, /cl, /showname, /navi, ...), then the talk-type table that 0xcfaa30 builds into session+0x4e04
    /// (a sorted vector searched by 0xcebe36; Custom's patch 0x233a920 answers /range ahead of it); a word in neither is
    /// talk type 3, which prints "Invalid Command". CGameMode::SendMsg(0x2a, type) (0xc07a00) then runs the type. This is
    /// the non-UI part: the tables, the parse, the emotion ids and the option flags with the chat lines they print;
    /// RoClassicChat runs the commands.
    /// </summary>
    public static partial class RoChatSlashCommands {
        // ---- talk types (the cases of 0xc07a00) ----
        public const int TypeChat = 0x00;
        public const int TypeInvalid = 0x03;
        public const int TypeWho = 0x06;
        public const int TypeBgm = 0x07;
        public const int TypeEffect = 0x08;
        public const int TypeWhere = 0x09;
        public const int TypeSkip = 0x0a;
        public const int TypeSound = 0x0b;
        public const int TypeHelp = 0x0e;
        public const int TypeBlockList = 0x0f;
        public const int TypeBlockAll = 0x10;
        public const int TypeAllowAll = 0x11;
        public const int TypeStand = 0x15;
        public const int TypeSit = 0x16;
        public const int TypeChatRoom = 0x17;
        public const int TypeLeaveRoom = 0x18;
        public const int TypeOrganize = 0x1a;
        public const int TypeLeaveParty = 0x1b;
        public const int TypeExpel = 0x1c;
        public const int TypeTip = 0x2a;
        public const int TypeMemo = 0x2b;
        public const int TypeSkillSnap = 0x2d;
        public const int TypeSnap = 0x2e;
        public const int TypeItemSnap = 0x2f;
        public const int TypeSaveChat = 0x33;
        public const int TypeMiss = 0x35;
        public const int TypeCamera = 0x36;
        public const int TypeBreakGuild = 0x38;
        public const int TypeFog = 0x3c;
        public const int TypeEmblem = 0x40;
        public const int TypeEmotionList = 0x56;
        public const int TypeFont = 0x5c;
        public const int TypeAutoSaveChat = 0x66;
        public const int TypeAutoMsg = 0x97;
        public const int TypeNoShift = 0x67;
        public const int TypeAura = 0x68;
        public const int TypeAura2 = 0x69;
        public const int TypeShowName = 0x6a;
        public const int TypeNoCtrl = 0x6b;
        public const int TypeDoriDori = 0x6c;
        public const int TypeBingBing = 0x6d;
        public const int TypeBangBang = 0x6e;
        public const int TypeSkillFail = 0x6f;
        public const int TypeNoTalkMsg = 0x70;
        public const int TypeSet1 = 0x71;
        public const int TypeLightmap = 0x75;
        public const int TypeQuickSpell = 0x76;
        public const int TypeQuickSpell2 = 0x77;
        public const int TypeQuickSpell3 = 0x78;
        public const int TypeBattleMode = 0x85;
        public const int TypeNoTrade = 0x86;
        public const int TypeSet2 = 0x88;
        public const int TypeWindow = 0x8b;
        public const int TypeNoTalkMsg2 = 0x92;
        public const int TypeHi = 0x95;
        public const int TypeHoAi = 0xa2;
        public const int TypeLoginOut = 0xa3;
        public const int TypeSet3 = 0xa4;
        public const int TypeShopping = 0xa5;
        public const int TypeMerAi = 0xac;
        public const int TypeAccept = 0xb4;
        public const int TypeRefuse = 0xb5;
        public const int TypeInvite = 0xb6;
        public const int TypeEquipOpen = 0xb7;
        public const int TypeBattleChat = 0xb8;
        public const int TypeTingOnly = 0xc1;
        public const int TypeStateInfo = 0xc7;
        public const int TypeNavi = 0xcf;
        public const int TypeNavi2 = 0xd0;
        public const int TypeGuildInvite = 0xd1;
        public const int TypeMonsterHp = 0xd3;
        public const int TypeClanChat = 0xd4;
        public const int TypeNaviOpen = 0xd7;
        public const int TypeCashShop = 0xd8;
        public const int TypeGoldPc = 0xd9;
        public const int TypeRoulette = 0xda;
        public const int TypeQuake = 0xdb;
        public const int TypeMinimap = 0xdd;
        public const int TypeMinEffect = 0xe3;
        public const int TypeMable = 0xee;
        public const int TypeZoom = 0xef;
        public const int TypeFrame = 0xf1;

        /// <summary>Custom's /range (0x233a920): toggles its skill-range flag and returns 0x7fff, a type nothing runs.</summary>
        public const int TypeRange = 0x7fff;

        /// <summary>The colours of the lines the commands print (COLORREF 0x64ffff, 0xffff64, 0xffff, 0xffff00, 0xff, 0xff00).</summary>
        public const string OptionColor = "#FFFF64";
        public const string ListColor = "#64FFFF";
        public const string YellowColor = "#FFFF00";
        public const string CyanColor = "#00FFFF";
        public const string RedColor = "#FF0000";
        public const string GreenColor = "#00FF00";

        // ---- the talk-type table (0xcfaa30): the ASCII words; the Korean ones (and the few with capitals, which the
        // lower-cased word never meets) are left out. "/help" and "/lv" are in it twice (0xe / 0x4d, 0x20 / 0x93): the
        // documented meaning is kept (command help, the "/lv" emotion). ----
        private static readonly Dictionary<string, int> Table = new Dictionary<string, int>(StringComparer.Ordinal) {
            ["/stat"] = 0x02, ["/killall"] = 0x05,
            ["/who"] = 0x06, ["/w"] = 0x06, ["/wjqthrwk"] = 0x06, ["/snrn"] = 0x06,
            ["/bgm"] = 0x07, ["/dmadkr"] = 0x07,
            ["/effect"] = 0x08, ["/dlvprxm"] = 0x08,
            ["/where"] = 0x09, ["/whkvy"] = 0x09, ["/wkdth"] = 0x09,
            ["/skip"] = 0x0a,
            ["/sound"] = 0x0b, ["/gyrhkdma"] = 0x0b,
            ["/help"] = 0x0e, ["/h"] = 0x0e,
            ["/ex"] = 0x0f, ["/exall"] = 0x10, ["/inall"] = 0x11,
            ["/stand"] = 0x15, ["/tjrl"] = 0x15,
            ["/sit"] = 0x16, ["/dkswrl"] = 0x16,
            ["/chat"] = 0x17, ["/coxldqkd"] = 0x17,
            ["/q"] = 0x18,
            ["/leave"] = 0x1b, ["/xkfxhl"] = 0x1b,
            ["/!"] = 0x1d, ["/?"] = 0x1e,
            ["/ho"] = 0x1f, ["/delight"] = 0x1f,
            ["/lv"] = 0x20, ["/heart"] = 0x20, ["/gkxm"] = 0x20,
            ["/swt"] = 0x21, ["/sweat"] = 0x21,
            ["/ic"] = 0x22, ["/aha"] = 0x22, ["/dkgk"] = 0x22,
            ["/an"] = 0x23, ["/fret"] = 0x23,
            ["/ag"] = 0x24, ["/anger"] = 0x24, ["/ghk"] = 0x24,
            ["/$"] = 0x25, ["/money"] = 0x25, ["/ehs"] = 0x25,
            ["/..."] = 0x26,
            ["/gawi"] = 0x27, ["/rkdnl"] = 0x27,
            ["/bawi"] = 0x28, ["/qkdnl"] = 0x28, ["/wnajr"] = 0x28,
            ["/bo"] = 0x29, ["/qh"] = 0x29,
            ["/tip"] = 0x2a,
            ["/memo"] = 0x2b, ["/rldjr"] = 0x2b,
            ["/skillsnap"] = 0x2d, ["/tmzlftmsoq"] = 0x2d,
            ["/snap"] = 0x2e, ["/tmsoq"] = 0x2e,
            ["/itemsnap"] = 0x2f, ["/dkdlxpatmsoq"] = 0x2f,
            ["/savechat"] = 0x33, ["/rkfanfl"] = 0x33,
            ["/miss"] = 0x35, ["/camera"] = 0x36, ["/tp"] = 0x3b, ["/fog"] = 0x3c, ["/objlight"] = 0x41,
            ["/thx"] = 0x42, ["/wah"] = 0x43, ["/sry"] = 0x44, ["/sorry"] = 0x44, ["/heh"] = 0x45, ["/smile"] = 0x45,
            ["/swt2"] = 0x46, ["/hmm"] = 0x47, ["/no1"] = 0x48, ["/??"] = 0x49, ["/omg"] = 0x4a, ["/oh"] = 0x4b, ["/o"] = 0x4b,
            ["/x"] = 0x4c, ["/hlp"] = 0x4d, ["/go"] = 0x4e, ["/lv2"] = 0x4f, ["/sob"] = 0x50, ["/gg"] = 0x51,
            ["/kis"] = 0x52, ["/kis2"] = 0x53, ["/pif"] = 0x54, ["/ok"] = 0x55,
            ["/emotion"] = 0x56, ["/autosavechat"] = 0x66,
            ["/noshift"] = 0x67, ["/ns"] = 0x67, ["/aura"] = 0x68, ["/aura2"] = 0x69,
            ["/noctrl"] = 0x6b, ["/nc"] = 0x6b,
            ["/doridori"] = 0x6c, ["/bingbing"] = 0x6d, ["/bangbang"] = 0x6e,
            ["/skillfail"] = 0x6f, ["/sf"] = 0x6f,
            ["/notalkmsg"] = 0x70, ["/nm"] = 0x70,
            ["/set1"] = 0x71, ["/clipmouse"] = 0x74, ["/lightmap"] = 0x75,
            ["/quickspell"] = 0x76, ["/q1"] = 0x76, ["/quickspell2"] = 0x77, ["/q2"] = 0x77, ["/q3"] = 0x78,
            ["/bzz"] = 0x79, ["/e1"] = 0x79, ["/rice"] = 0x7a, ["/e2"] = 0x7a, ["/awsm"] = 0x7b, ["/e3"] = 0x7b,
            ["/meh"] = 0x7c, ["/e4"] = 0x7c, ["/shy"] = 0x7d, ["/e5"] = 0x7d, ["/pat"] = 0x7e, ["/e6"] = 0x7e,
            ["/mp"] = 0x7f, ["/e7"] = 0x7f, ["/slur"] = 0x80, ["/e8"] = 0x80, ["/com"] = 0x81, ["/e9"] = 0x81,
            ["/yawn"] = 0x82, ["/e10"] = 0x82, ["/grat"] = 0x83, ["/e11"] = 0x83, ["/hp"] = 0x84, ["/e12"] = 0x84,
            ["/battlemode"] = 0x85, ["/bm"] = 0x85,
            ["/notrade"] = 0x86, ["/nt"] = 0x86,
            ["/set2"] = 0x88, ["/window"] = 0x8b, ["/wi"] = 0x8b, ["/loading"] = 0x8f,
            ["/blacksmith"] = 0x90, ["/alchemist"] = 0x91,
            ["/notalkmsg2"] = 0x92, ["/nm2"] = 0x92,
            ["/sp"] = 0x94, ["/taekwon"] = 0x96,
            ["/automsg"] = 0x97, ["/am"] = 0x97,
            ["/fsh"] = 0x98, ["/e13"] = 0x98, ["/spin"] = 0x99, ["/e14"] = 0x99, ["/sigh"] = 0x9a, ["/e15"] = 0x9a,
            ["/dum"] = 0x9b, ["/e16"] = 0x9b, ["/crwd"] = 0x9c, ["/e17"] = 0x9c,
            ["/desp"] = 0x9d, ["/e18"] = 0x9d, ["/otl"] = 0x9d,
            ["/dice"] = 0x9e, ["/e19"] = 0x9e,
            ["/pk"] = 0x9f, ["/traceai"] = 0xa0, ["/hw"] = 0xa1, ["/hoai"] = 0xa2,
            ["/loginout"] = 0xa3, ["/li"] = 0xa3, ["/set3"] = 0xa4, ["/shopping"] = 0xa5, ["/sh"] = 0xa5,
            ["/e20"] = 0xa9, ["/minigame"] = 0xab, ["/merai"] = 0xac, ["/cn"] = 0xb3,
            ["/accept"] = 0xb4, ["/refuse"] = 0xb5,
            ["/eqopen"] = 0xb7, ["/wkdqlrhdro"] = 0xb7, ["/battlechat"] = 0xb8,
            ["/hum"] = 0xb9, ["/e27"] = 0xb9, ["/abs"] = 0xba, ["/e28"] = 0xba, ["/oops"] = 0xbb, ["/e29"] = 0xbb,
            ["/spit"] = 0xbc, ["/e30"] = 0xbc, ["/ene"] = 0xbd, ["/e31"] = 0xbd, ["/panic"] = 0xbe, ["/e32"] = 0xbe,
            ["/whisp"] = 0xbf, ["/e33"] = 0xbf,
            ["/tingonly"] = 0xc1, ["/stateinfo"] = 0xc7, ["/show_addressbar"] = 0xcb, ["/buildinfo"] = 0xcd,
            ["/monsterhp"] = 0xd3, ["/minimap"] = 0xdd, ["/viewpointvalue"] = 0xe1,
            ["/mable"] = 0xee, ["/zoom"] = 0xef, ["/frame"] = 0xf1,
        };

        // ---- the named forms 0xced690 tries before the table (the GM broadcasts' "/b" family is compared inline) ----
        private static readonly Dictionary<string, int> Named = new Dictionary<string, int>(StringComparer.Ordinal) {
            ["/b"] = 0x01, ["/lb"] = 0x57, ["/nb"] = 0x5a, ["/nlb"] = 0x5b, ["/bb"] = 0x64, ["/mb"] = 0x65, ["/kill"] = 0x04,
            ["/gocp"] = 0x38, ["/breakguild"] = 0x38,
            ["/ruftjd"] = 0x1a, ["/organize"] = 0x1a,
            ["/item"] = 0x30, ["/monster"] = 0x30,
            ["/mapmove"] = 0x31, ["/mm"] = 0x31,
            ["/wpaud"] = 0x1c, ["/expel"] = 0x1c,
            ["/sm"] = 0x3a, ["/resetstate"] = 0x3d, ["/resetskill"] = 0x3e, ["/changemaptype"] = 0x3f, ["/cmt"] = 0x3f,
            ["/emblem"] = 0x40, ["/dpaqmffpa"] = 0x40, ["/doaqmffpa"] = 0x40,
            ["/hide"] = 0x58, ["/font"] = 0x5c, ["/mineffect"] = 0xe3, ["/remove"] = 0x5d,
            ["/summon"] = 0x60, ["/shift"] = 0x5e, ["/recall"] = 0x5f, ["/check"] = 0x8d, ["/rc"] = 0x8e,
            ["/showname"] = 0x6a,
            ["/str+"] = 0x87, ["/agi+"] = 0x87, ["/luk+"] = 0x87, ["/int+"] = 0x87, ["/vit+"] = 0x87, ["/dex+"] = 0x87,
            ["/pow+"] = 0x87, ["/sta+"] = 0x87, ["/wis+"] = 0x87, ["/spl+"] = 0x87, ["/con+"] = 0x87, ["/crt+"] = 0x87,
            ["/hi"] = 0x95,
            ["/cheo"] = 0xb6, ["/invite"] = 0xb6, ["/rlfemcheo"] = 0xd1, ["/guildinvite"] = 0xd1,
            ["/web"] = 0xce, ["/navi"] = 0xcf, ["/navigation"] = 0xcf, ["/navi2"] = 0xd0, ["/navigation2"] = 0xd0,
            ["/naviopen"] = 0xd7, ["/cl"] = 0xd4, ["/clanchat"] = 0xd4,
            ["/cashshop"] = 0xd8, ["/goldpc"] = 0xd9, ["/roulette"] = 0xda, ["/quake"] = 0xdb, ["/twitter"] = 0xdc,
            ["/macro_register"] = 0xde, ["/mr"] = 0xde, ["/macro_preview"] = 0xdf, ["/mp"] = 0xdf,
            ["/macro_detector"] = 0xe0, ["/md"] = 0xe0, ["/setcamera"] = 0xe2,
            ["/resetcooltime"] = 0xe4, ["/rct"] = 0xe4, ["/call"] = 0xe5,
            ["/changedress"] = 0xed, ["/nocosplay"] = 0xed, ["/showshop"] = 0xf8,
            ["/accountlimitedtool"] = 0xfa, ["/agency"] = 0xeb, ["/macrochecker"] = 0x102,
            ["/range"] = TypeRange,
        };

        /// <summary>
        /// 0xced690 for a line that starts with '/': the talk type and the text after the first word and its spaces
        /// (cefa4c). Not a '/' line → TypeChat; a word in neither table → TypeInvalid.
        /// </summary>
        public static int Parse(string line, out string argument) {
            argument = string.Empty;
            if (string.IsNullOrEmpty(line) || line[0] != '/') return TypeChat;
            int end = 0;
            while (end < line.Length && !char.IsWhiteSpace(line[end])) end++;
            string word = LowerAscii(line.Substring(0, end));
            int rest = end;
            while (rest < line.Length && line[rest] == ' ') rest++;
            argument = rest < line.Length ? line.Substring(rest).TrimEnd() : string.Empty;
            if (word.Length < 2) return TypeInvalid;
            if (Named.TryGetValue(word, out int type)) return type;
            return Table.TryGetValue(word, out type) || KoreanTable.TryGetValue(word, out type) ? type : TypeInvalid;
        }

        /// <summary>0xa22b50: 'A'..'Z' to lower case, nothing else (no culture).</summary>
        public static string LowerAscii(string s) {
            for (int i = 0; i < s.Length; i++) {
                if (s[i] >= 'A' && s[i] <= 'Z') {
                    char[] c = s.ToCharArray();
                    for (int k = i; k < c.Length; k++) {
                        if (c[k] >= 'A' && c[k] <= 'Z') c[k] = (char) (c[k] + 32);
                    }

                    return new string(c);
                }
            }

            return s;
        }

        // ---- emotions: the cases push "ET_..." for mode msg 0x149 (CZ_REQ_EMOTION) ----
        private static readonly Dictionary<int, int> Emotions = new Dictionary<int, int> {
            [0x1d] = 0, [0x1e] = 1, [0x1f] = 2, [0x20] = 3, [0x21] = 4, [0x22] = 5, [0x23] = 6, [0x24] = 7, [0x25] = 8, [0x26] = 9,
            [0x27] = 10, [0x28] = 11, [0x29] = 12, [0x4f] = 14, [0x42] = 15, [0x43] = 16, [0x44] = 17, [0x45] = 18, [0x46] = 19,
            [0x47] = 20, [0x48] = 21, [0x49] = 22, [0x4a] = 23, [0x4b] = 24, [0x4c] = 25, [0x4d] = 26, [0x4e] = 27, [0x50] = 28,
            [0x51] = 29, [0x52] = 30, [0x53] = 31, [0x54] = 32, [0x55] = 33, [0x79] = 36, [0x7a] = 37, [0x7b] = 38, [0x7c] = 39,
            [0x7d] = 40, [0x7e] = 41, [0x7f] = 42, [0x80] = 43, [0x81] = 44, [0x82] = 45, [0x83] = 46, [0x84] = 47, [0x98] = 52,
            [0x99] = 53, [0x9a] = 54, [0x9b] = 55, [0x9c] = 56, [0x9d] = 57, [0x9e] = 58, [0xa9] = 65, [0xad] = 68, [0xae] = 69,
            [0xaf] = 70, [0xb0] = 71, [0xb1] = 72, [0xb2] = 73, [0xb9] = 74, [0xba] = 75, [0xbb] = 76, [0xbc] = 77, [0xbd] = 78,
            [0xbe] = 79, [0xbf] = 80,
        };

        /// <summary>The emotion a talk type sends (ET_DICE1 for /dice: the server rolls the face), or false.</summary>
        public static bool TryGetEmotion(int type, out int emotion) => Emotions.TryGetValue(type, out emotion);

        /// <summary>/emotion (case 0x56): MsgStr 0x25d "[New Emoji List]", then 0x224, 0x22b…0x23d and 0x304…0x30f, one a line.</summary>
        public static IEnumerable<int> EmotionListMsgIds() {
            yield return 0x25d;
            yield return 0x224;
            for (int id = 0x22b; id <= 0x23d; id++) yield return id;
            for (int id = 0x304; id <= 0x30f; id++) yield return id;
        }

        // ---- option flags (the toggles of 0xc07a00 that print MsgStr(on ? a : b) in RGB(255, 255, 100)) ----

        /// <summary>
        /// One on/off command: its name (also RoClassicCommandList's PlayerPrefs key), talk type, the default of a flag kept
        /// here (optioninfo.lub), the line for on / off (MsgStr id, English fallback; −1 = the fallback is literal, none =
        /// silent), and for the Game Settings ones the setting it is.
        /// </summary>
        public sealed class Option {
            internal Option(string name, int type, bool defaultOn, int msgOn, string textOn, int msgOff, string textOff,
                Func<RoClientSettingsSnapshot, bool> get = null, Action<RoClientSettingsModel, bool> set = null, string key = null) {
                Name = name;
                Type = type;
                Default = defaultOn;
                MsgOn = msgOn;
                TextOn = textOn;
                MsgOff = msgOff;
                TextOff = textOff;
                Get = get;
                Set = set;
                Key = key ?? PrefsPrefix + name;
            }

            public string Name { get; }

            /// <summary>Where a flag kept here lives: "ro.ui.classic.cmd./name", or the key of the window that shares it.</summary>
            public string Key { get; }
            public int Type { get; }
            public bool Default { get; }
            public int MsgOn { get; }
            public string TextOn { get; }
            public int MsgOff { get; }
            public string TextOff { get; }
            public bool IsSetting => Get != null;
            internal Func<RoClientSettingsSnapshot, bool> Get { get; }
            internal Action<RoClientSettingsModel, bool> Set { get; }
        }

        public const string PrefsPrefix = "ro.ui.classic.cmd.";

        private static readonly Option[] Options = {
            new Option("/effect", TypeEffect, true, 0x17, "Effects On", 0x18, "Effects Off", s => s.ShowSkillEffects, (m, v) => m.SetShowSkillEffects(v)),
            new Option("/bgm", TypeBgm, true, 0x1f, "BGM On", 0x20, "BGM Off", s => !s.BgmMuted, (m, v) => m.SetBgmMuted(!v)),
            new Option("/sound", TypeSound, true, 0x1b, "Sound Effects On", 0x1c, "Sound Effects Off", s => !s.EffectsMuted, (m, v) => m.SetEffectsMuted(!v)),
            // /skip: [0x011842f8] starts at 1 (.data) and is not in CmdOnOffList, so every start has frame skip on
            new Option("/skip", TypeSkip, true, 0x1d, "Frame Skip On", 0x1e, "Frame Skip Off"),
            new Option("/miss", TypeMiss, true, 0x13d, "Shows ''Miss'' On", 0x13e, "Shows ''Miss'' Off", s => s.ShowMiss, (m, v) => m.SetShowMiss(v)),
            new Option("/noshift", TypeNoShift, false, 0x2bd, "[no shift] option activated. [ON]", 0x2be, "[no shift] option deactivated. [OFF]", s => s.NoShift, (m, v) => m.SetNoShift(v)),
            new Option("/noctrl", TypeNoCtrl, false, 0x2cd, "Use auto attack without Ctrl. [Auto attack ON]", 0x2ce, "Use auto attack with Ctrl. [Auto attack OFF]", s => s.NoCtrl, (m, v) => m.SetNoCtrl(v)),
            new Option("/window", TypeWindow, false, 0x352, "Display window docking enabled. [/wi ON]", 0x353, "Display window docking disabled. [/wi OFF]"),
            // /aura on = the normal aura (not simplified): MsgStr 0x2c7 "Simplified Aura [OFF]"
            new Option("/aura", TypeAura, true, 0x2c7, "Simplified Aura [OFF]", 0x2c8, "Simplified Aura [ON]", s => !s.SimpleAura, (m, v) => m.SetSimpleAura(!v)),
            // /aura2 on = the aura hidden
            new Option("/aura2", TypeAura2, false, 0xbb3, "Aura effect is OFF", 0xbb2, "Aura effect is ON", s => !s.ShowAura, (m, v) => m.SetShowAura(!v)),
            new Option("/skillfail", TypeSkillFail, true, 0x2da, "Skill fail messages will be displayed. [Display On]", 0x2db, "Skill fail messages will not be displayed. [Display OFF]"),
            new Option("/loginout", TypeLoginOut, true, 0x414, "Display online status of friends in Chat Window. [/li ON]", 0x415, "Do not display online status of friends in Chat Window. [/li OFF]"),
            new Option("/shopping", TypeShopping, true, 0x454, "You can now open a shop with a single left-click and close your shop with a single right-click. [sh ON].", 0x455, "You can open a shop by double-clicking. [/sh OFF]"),
            new Option("/q1", TypeQuickSpell, false, 0x2fd, "Right click menu skills for F9 are Enabled.[/q1 ON]", 0x2fe, "Right click menu skills for F9 are Disabled.[/q1 OFF]"),
            new Option("/q2", TypeQuickSpell2, false, 0x300, "Mouse wheel skills for F7 and F8 are Enabled.[/q2 ON]", 0x301, "Mouse wheel skills for F7 and F8 are Disabled.[/q2 OFF]"),
            new Option("/battlemode", TypeBattleMode, true, 0x311, "Skills assigned to shortcut windows 1, 2, 3 are Enabled. [/bm ON]", 0x312, "Skills assigned to shortcut windows 1, 2, 3 are Disabled. [/bm OFF]",
                s => s.BattleModeEnabled, (m, v) => m.SetBattleModeEnabled(v)),
            // /notalkmsg(2): SETNZ, the "not displayed" line is the on one
            new Option("/notalkmsg", TypeNoTalkMsg, false, 0x2de, "Chat content will not be displayed in the chat window. [Display OFF]", 0x2dd, "Chat content will be displayed in the chat window. [Display ON]"),
            new Option("/notalkmsg2", TypeNoTalkMsg2, false, 0x38d, "Hide chat messages(including guild chat) [/nm2 OFF]", 0x38c, "Show chat messages. [/nm2 ON]"),
            new Option("/notrade", TypeNoTrade, false, 0x325, "Auto decline trade offers has been Enabled. [/nt ON]", 0x326, "Auto decline trade offers has been Disabled. [/nt OFF]"),
            new Option("/hoai", TypeHoAi, false, 0x3ff, "Homunculus has been customized.", 0x400, "Homunculus has been activated with the basic AI."),
            new Option("/merai", TypeMerAi, false, 0x4f9, "Mercenary will follow custom AI.", 0x4fa, "Mercenary will follow basic AI."),
            new Option("/camera", TypeCamera, false, 0x13f, "Camera Zooming On", 0x140, "Camera Zooming Off"),
            new Option("/snap", TypeSnap, false, 0x10f, "Attack Snap On", 0x110, "Attack Snap Off", s => s.MouseSnapMonster, (m, v) => m.SetMouseSnapMonster(v)),
            new Option("/itemsnap", TypeItemSnap, false, 0x114, "Item Snap On", 0x115, "Item Snap Off", s => s.MouseSnapPickupItem, (m, v) => m.SetMouseSnapPickupItem(v)),
            new Option("/skillsnap", TypeSkillSnap, false, 0x111, "Skill Snap On", 0x112, "Skill Snap Off", s => s.MouseSnapSkill, (m, v) => m.SetMouseSnapSkill(v)),
            new Option("/tingonly", TypeTingOnly, false, 0x5da, "Chat room entry sound [ON]", 0x5db, "Chat room entry sound [OFF]"),
            new Option("/monsterhp", TypeMonsterHp, true, 0x884, "Show monster HP bar when attacking.", 0x885, "Hide monster HP bar when attacking."),
            // /minimap is the minimap window's own "buttons" option ([0x01553ab0])
            new Option("/minimap", TypeMinimap, true, 0xb2e, "Show minimap buttons", 0xb2f, "Hide minimap buttons", key: RoClassicMinimap.ButtonsOptionKey),
            new Option("/showname", TypeShowName, true, 0x2d2, "Normal Font Displayed. [showname type 1]", 0x2d3, "Font will be thin and party name will be shown [showname type 2]"),
            new Option("/mineffect", TypeMinEffect, false, 0x2af, "Effect Simplification On", 0x2b0, "Effect Simplification Off"),
            new Option("/quake", TypeQuake, true, 0xaf5, "Screen shake ON", 0xaf6, "Screen shake OFF"),
            new Option("/mable", TypeMable, true, 0xd7b, "Sprite Marble effect is ON", 0xd7c, "Sprite Marble effect is OFF"),
            new Option("/zoom", TypeZoom, false, 0xdb7, "Zoom Out is turned (On)", 0xdb8, "Zoom Out is turned (Off)", s => s.ZoomOut, (m, v) => m.SetZoomOut(v)),
            // /frame is Game Settings' "Emblem Border" ([0x015556bc])
            new Option("/frame", TypeFrame, false, 0xdc7, "Emblem displayed with border", 0xdc8, "Emblem displayed without border", key: RoClassicGameSettings.EmblemBorderKey),
            // /fog: literal "Fog On" / "Fog Off" (0xfffa28 / 0xfffa30)
            new Option("/fog", TypeFog, true, -1, "Fog On", -1, "Fog Off", s => s.FogEffect, (m, v) => m.SetFogEffect(v)),
            // silent toggles: /lightmap (0x01184300, the option window refreshed), the three cash / PC-cafe buttons
            new Option("/lightmap", TypeLightmap, true, -2, null, -2, null, s => s.ShowShadow, (m, v) => m.SetShowShadow(v)),
            new Option("/cashshop", TypeCashShop, true, -2, null, -2, null),
            new Option("/goldpc", TypeGoldPc, true, -2, null, -2, null),
            new Option("/roulette", TypeRoulette, true, -2, null, -2, null),
            // Custom's /range (0x233a920): "Skill range [ON]" / "Skill range [OFF]" in green
            new Option("/range", TypeRange, false, -1, "Skill range [ON]", -1, "Skill range [OFF]"),
        };

        private static Dictionary<int, Option> optionsByType;
        private static Dictionary<string, Option> optionsByName;

        private static void IndexOptions() {
            if (optionsByType != null) return;
            var byType = new Dictionary<int, Option>();
            var byName = new Dictionary<string, Option>(StringComparer.Ordinal);
            foreach (Option o in Options) {
                byType[o.Type] = o;
                byName[o.Name] = o;
            }

            optionsByName = byName;
            optionsByType = byType;
        }

        public static bool TryGetOption(int type, out Option option) {
            IndexOptions();
            return optionsByType.TryGetValue(type, out option);
        }

        public static bool TryGetOption(string name, out Option option) {
            IndexOptions();
            return optionsByName.TryGetValue(name ?? string.Empty, out option);
        }

        /// <summary>
        /// Where the flags kept here live (PlayerPrefs "ro.ui.classic.cmd./name", RoClassicCommandList's keys); a test swaps
        /// it for a dictionary so the Editor's prefs stay untouched.
        /// </summary>
        public static Func<string, bool, bool> ReadFlag = (key, fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;

        public static Action<string, bool> WriteFlag = (key, value) => PlayerPrefs.SetInt(key, value ? 1 : 0);

        /// <summary>The option's state: its Game Settings value, else the flag kept here (its default when never set).</summary>
        public static bool GetOption(Option option, RoClientSettingsModel settings) {
            if (option.Get != null && settings != null) return option.Get(settings.Snapshot());
            return ReadFlag(option.Key, option.Default);
        }

        /// <summary>The option's state from a settings snapshot taken once (windows that draw many of them).</summary>
        public static bool GetOptionFrom(Option option, RoClientSettingsSnapshot snapshot) {
            if (option.Get != null && snapshot != null) return option.Get(snapshot);
            return ReadFlag(option.Key, option.Default);
        }

        public static bool GetOption(string name, RoClientSettingsModel settings) =>
            TryGetOption(name, out Option option) && GetOption(option, settings);

        /// <summary>/notalkmsg or /notalkmsg2 (0x01552d68 / 0x01552d6c): other players' public and battlefield chat is dropped.</summary>
        public static bool HidesPublicChat() => GetOption("/notalkmsg", null) || GetOption("/notalkmsg2", null);

        /// <summary>/notalkmsg2: guild, alliance and clan chat is dropped too.</summary>
        public static bool HidesGuildChat() => GetOption("/notalkmsg2", null);

        public static void SetOption(Option option, RoClientSettingsModel settings, bool on) {
            if (option.Set != null && settings != null) option.Set(settings, on);
            else WriteFlag(option.Key, on);
            // the systems that read the flags take the new value (camera, window snap, balloons, effects, ...)
            RoClassicCommandFlags.Reload();
        }

        /// <summary>
        /// The talk type of an option: its flag flips and the line it prints comes back (null for the silent ones).
        /// /aura with the aura hidden (/aura2 on) changes nothing and answers MsgStr 0xbb4. /mineffect flips the effective
        /// value (0xc0aead: [0x01552d7c] = [0x01552d80] = !d7c), so a map that forced it on is turned off at once.
        /// </summary>
        public static string ToggleOption(Option option, RoClientSettingsModel settings, Func<int, string, string> msgStr, out bool on) {
            msgStr = msgStr ?? ((id, fallback) => fallback);
            if (option.Type == TypeAura && TryGetOption(TypeAura2, out Option aura2) && GetOption(aura2, settings)) {
                on = GetOption(option, settings);
                return msgStr(0xbb4, "Aura effect is OFF. Please turn it ON and try again");
            }

            if (option.Type == TypeMinEffect) {
                on = !RoClassicCommandFlags.MinEffect;
                Ragnarok.World.RoPvpState.ClearForcedMinEffect();
            } else {
                on = !GetOption(option, settings);
            }

            SetOption(option, settings, on);
            int id = on ? option.MsgOn : option.MsgOff;
            string text = on ? option.TextOn : option.TextOff;
            if (id == -2) return null;
            return id < 0 ? text : msgStr(id, text);
        }

        // ---- packets the commands send (lengths from RoPacketLengthTable20200401) ----

        /// <summary>CZ_REQ_USER_COUNT 0x00c1 (2): /who, /w.</summary>
        public const ushort CzReqUserCount = 0x00c1;

        /// <summary>ZC_USER_COUNT 0x00c2 (6): "There are %d Players Currently Connected." (MsgStr 0xb2) in yellow.</summary>
        public const ushort ZcUserCount = 0x00c2;

        /// <summary>CZ_REMEMBER_WARPPOINT 0x011d (2): /memo.</summary>
        public const ushort CzRememberWarpPoint = 0x011d;

        /// <summary>ZC_ACK_REMEMBER_WARPPOINT 0x011e (3): 0 MsgStr 0xd9 (cyan), 1 0xd6, 2 0xd8, 3 0x9e7 (red).</summary>
        public const ushort ZcAckRememberWarpPoint = 0x011e;

        /// <summary>CZ_PARTY_CONFIG 0x02c8 (3) {u8 refuse}: /accept 0, /refuse 1.</summary>
        public const ushort CzPartyConfig = 0x02c8;

        /// <summary>CZ_REQ_JOIN_GUILD2 0x0916 (26) {name[24]}: /guildinvite name.</summary>
        public const ushort CzReqJoinGuild2 = 0x0916;

        /// <summary>The line ZC_USER_COUNT prints (case 0xc2 of the packet switch, 0xc2da61).</summary>
        public static string UserCountLine(int count, Func<int, string, string> msgStr) {
            string format = (msgStr ?? ((id, f) => f))(0xb2, "There are %d Players Currently Connected.");
            return format.Replace("%d", count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>The line and colour of ZC_ACK_REMEMBER_WARPPOINT (0xc308a1).</summary>
        public static string MemoResultLine(int type, Func<int, string, string> msgStr, out string color) {
            msgStr = msgStr ?? ((id, f) => f);
            color = RedColor;
            switch (type) {
                case 0:
                    color = CyanColor;
                    return msgStr(0xd9, "Current location is saved as a Memo Point for Warp Skill.");
                case 1:
                    return msgStr(0xd6, "Skill Level is not high enough");
                case 2:
                    return msgStr(0xd8, "You haven't learned Warp skill.");
                case 3:
                    return msgStr(0x9e7, "Cannot save location as a Memo Point at current location.");
                default:
                    return null;
            }
        }

        /// <summary>/where (case 9, sprintf "%s(%s) : %d, %d"): the map's display name, its name without ".rsw", the cell.</summary>
        public static string WhereLine(string displayName, string mapName, int x, int y) {
            string map = mapName ?? string.Empty;
            if (map.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase) || map.EndsWith(".gat", StringComparison.OrdinalIgnoreCase)) map = map.Substring(0, map.Length - 4);
            return (string.IsNullOrEmpty(displayName) ? map : displayName) + "(" + map + ") : " +
                x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " + y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>A name for a 24-byte packet field (the client's code page, cut at 24 bytes).</summary>
        public static byte[] Name24(string name) {
            var body = new byte[24];
            byte[] bytes = RoLoginShellSession.TextBytes(name ?? string.Empty);
            Array.Copy(bytes, body, Math.Min(24, bytes.Length));
            return body;
        }
    }
}
