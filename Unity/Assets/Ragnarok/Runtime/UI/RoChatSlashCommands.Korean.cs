using System.Collections.Generic;

namespace Ragnarok.UI {
    public static partial class RoChatSlashCommands {
        // ---- the Korean words of the talk-type table (0xcfaa30, CP949 in the client): the Hangul names and the 2-set
        // keyboard spellings of the English ones (/ㅗ = /h, /ㅈ = /w, ...). Searched after the ASCII words; where one word
        // is in the table twice, the first entry is kept (as for /help and /lv). ----
        private static readonly Dictionary<string, int> KoreanTable = new Dictionary<string, int>(System.StringComparer.Ordinal) {
            ["/ㅗ"] = 0xe, ["/채팅방"] = 0x17, ["/촘ㅅ"] = 0x17, ["/접속자"] = 0x6, ["/누구"] = 0x6, ["/ㅈ"] = 0x6,
            ["/좨"] = 0x6, ["/음악"] = 0x7, ["/ㅠ흐"] = 0x7, ["/이펙트"] = 0x8, ["/효과음"] = 0xb, ["/나ㅑㅔ"] = 0xa,
            ["/차단"] = 0xf, ["/해제전부"] = 0x11, ["/차단전부"] = 0x10, ["/앉기"] = 0x16, ["/서기"] = 0x15, ["/ㄴㅅ뭉"] = 0x15,
            ["/ㅂ"] = 0x18, ["/탈퇴"] = 0x1b, ["/주먹"] = 0x28, ["/보"] = 0x29, ["/가위"] = 0x27, ["/바위"] = 0x28,
            ["/좌표"] = 0x9, ["/장소"] = 0x9, ["/샤ㅔ"] = 0x2a, ["/기억"] = 0x2b, ["/누메"] = 0x2e, ["/스냅"] = 0x2e,
            ["/나ㅑㅣㅣ누메"] = 0x2d, ["/스킬스냅"] = 0x2d, ["/ㅑㅅ드누메"] = 0x2f, ["/아이템스냅"] = 0x2f, ["/갈무리"] = 0x33, ["/전장"] = 0xb8,
            ["/자동응답"] = 0x97, ["/호문지능"] = 0xa2, ["/용병지능"] = 0xac, ["/상태정보"] = 0xc7, ["/ㅡㅐㅜㄴㅅㄷ고ㅔ"] = 0xd3, ["/ㄳ"] = 0x42,
            ["/하하"] = 0x45, ["/ㅇ"] = 0x45, ["/ㄱ"] = 0x1f, ["/원츄"] = 0x48, ["/ㅊ"] = 0x48, ["/ㅎ"] = 0x4a,
            ["/ㅊㅊ"] = 0x4d, ["/킥"] = 0x51, ["/ㅋ"] = 0x51, ["/케케"] = 0x51, ["/끄덕"] = 0x55, ["/이모션"] = 0x56,
            ["/ㅉ"] = 0x79, ["/부"] = 0x7d, ["/엠"] = 0x7f, ["/ㅊㅋ"] = 0x83, ["/피"] = 0x84, ["/한숨"] = 0x9a,
            ["/시끌시끌"] = 0x9c, ["/러브"] = 0xa9, ["/흠"] = 0xb9, ["/멍"] = 0xba, ["/웁"] = 0xbb, ["/웩"] = 0xbc,
            ["/탈력"] = 0xbd, ["/공황"] = 0xbe, ["/위스퍼"] = 0xbf, ["/ㅗㅈ"] = 0xa1, ["/초대거부"] = 0xb5, ["/초대수락"] = 0xb4,
            ["/장비공개"] = 0xb7,
        };
    }
}
