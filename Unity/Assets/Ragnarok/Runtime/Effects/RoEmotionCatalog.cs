using Ragnarok.Assets;

namespace Ragnarok.Effects {
    public readonly struct RoEmotionDefinition {
        public RoEmotionDefinition(int emotionId, int spriteActionIndex, int interfaceIndex, string primaryCommand) {
            EmotionId = emotionId;
            SpriteActionIndex = spriteActionIndex;
            InterfaceIndex = interfaceIndex;
            PrimaryCommand = primaryCommand ?? string.Empty;
        }

        public int EmotionId { get; }
        public int SpriteActionIndex { get; }
        public int InterfaceIndex { get; }
        public string PrimaryCommand { get; }
    }

    public static class RoEmotionCatalog {
        private static readonly RoEmotionDefinition[] Definitions = {
            new RoEmotionDefinition(0, 0, 0, "!"),
            new RoEmotionDefinition(1, 1, 1, "?"),
            new RoEmotionDefinition(2, 2, 2, "ho"),
            new RoEmotionDefinition(3, 3, 3, "lv"),
            new RoEmotionDefinition(4, 5, 5, "swt"),
            new RoEmotionDefinition(5, 6, 6, "ic"),
            new RoEmotionDefinition(6, 7, 7, "an"),
            new RoEmotionDefinition(7, 8, 8, "ag"),
            new RoEmotionDefinition(8, 9, 9, "$"),
            new RoEmotionDefinition(9, 10, 10, "..."),
            new RoEmotionDefinition(10, 12, 12, "scissors"),
            new RoEmotionDefinition(11, 11, 11, "rock"),
            new RoEmotionDefinition(12, 13, 13, "paper"),
            new RoEmotionDefinition(13, 14, -1, string.Empty),
            new RoEmotionDefinition(14, 4, 4, "lv2"),
            new RoEmotionDefinition(15, 15, 14, "thx"),
            new RoEmotionDefinition(16, 16, 15, "wah"),
            new RoEmotionDefinition(17, 17, 16, "sry"),
            new RoEmotionDefinition(18, 18, 17, "heh"),
            new RoEmotionDefinition(19, 19, 18, "swt2"),
            new RoEmotionDefinition(20, 20, 19, "hmm"),
            new RoEmotionDefinition(21, 21, 20, "no1"),
            new RoEmotionDefinition(22, 22, 21, "??"),
            new RoEmotionDefinition(23, 23, 22, "omg"),
            new RoEmotionDefinition(24, 24, 23, "oh"),
            new RoEmotionDefinition(25, 25, 24, "X"),
            new RoEmotionDefinition(26, 26, 25, "hlp"),
            new RoEmotionDefinition(27, 27, 26, "go"),
            new RoEmotionDefinition(28, 28, 27, "sob"),
            new RoEmotionDefinition(29, 29, 28, "gg"),
            new RoEmotionDefinition(30, 30, 29, "kis"),
            new RoEmotionDefinition(31, 31, 30, "kis2"),
            new RoEmotionDefinition(32, 32, 31, "pif"),
            new RoEmotionDefinition(33, 33, 32, "ok"),
            new RoEmotionDefinition(34, 1000, -1, string.Empty),
            new RoEmotionDefinition(35, 34, -1, string.Empty),
            new RoEmotionDefinition(36, 35, 33, "bzz"),
            new RoEmotionDefinition(37, 36, 34, "rice"),
            new RoEmotionDefinition(38, 37, 35, "awsm"),
            new RoEmotionDefinition(39, 38, 36, "meh"),
            new RoEmotionDefinition(40, 39, 37, "shy"),
            new RoEmotionDefinition(41, 40, 38, "pat"),
            new RoEmotionDefinition(42, 41, 39, "mp"),
            new RoEmotionDefinition(43, 42, 40, "slur"),
            new RoEmotionDefinition(44, 43, 41, "com"),
            new RoEmotionDefinition(45, 44, 42, "yawn"),
            new RoEmotionDefinition(46, 45, 43, "grat"),
            new RoEmotionDefinition(47, 46, 44, "hp"),
            new RoEmotionDefinition(48, 47, -1, string.Empty),
            new RoEmotionDefinition(49, 48, -1, string.Empty),
            new RoEmotionDefinition(50, 49, -1, string.Empty),
            new RoEmotionDefinition(51, 50, -1, string.Empty),
            new RoEmotionDefinition(52, 51, 45, "fsh"),
            new RoEmotionDefinition(53, 52, 46, "spin"),
            new RoEmotionDefinition(54, 53, 47, "sigh"),
            new RoEmotionDefinition(55, 54, 48, "dum"),
            new RoEmotionDefinition(56, 55, 49, "crwd"),
            new RoEmotionDefinition(57, 56, 50, "desp"),
            new RoEmotionDefinition(58, 57, -1, "dice"),
            new RoEmotionDefinition(59, 58, -1, string.Empty),
            new RoEmotionDefinition(60, 59, -1, string.Empty),
            new RoEmotionDefinition(61, 60, -1, string.Empty),
            new RoEmotionDefinition(62, 61, -1, string.Empty),
            new RoEmotionDefinition(63, 62, -1, string.Empty),
            new RoEmotionDefinition(64, 63, -1, string.Empty),
            new RoEmotionDefinition(65, 64, 51, "love"),
            new RoEmotionDefinition(66, 65, -1, string.Empty),
            new RoEmotionDefinition(67, 66, -1, string.Empty),
            new RoEmotionDefinition(68, 67, 52, "mobile"),
            new RoEmotionDefinition(69, 68, 53, "mail"),
            new RoEmotionDefinition(70, 69, -1, "antenna0"),
            new RoEmotionDefinition(71, 70, 54, "antenna1"),
            new RoEmotionDefinition(72, 71, 55, "antenna2"),
            new RoEmotionDefinition(73, 72, 56, "antenna3"),
            new RoEmotionDefinition(74, 73, 57, "hum"),
            new RoEmotionDefinition(75, 74, 58, "abs"),
            new RoEmotionDefinition(76, 75, 59, "oops"),
            new RoEmotionDefinition(77, 76, 60, "spit"),
            new RoEmotionDefinition(78, 77, 61, "ene"),
            new RoEmotionDefinition(79, 78, 62, "panic"),
            new RoEmotionDefinition(80, 79, 63, "whisp"),
            new RoEmotionDefinition(81, 80, -1, string.Empty),
            new RoEmotionDefinition(82, 81, -1, string.Empty),
            new RoEmotionDefinition(83, 82, -1, string.Empty),
            new RoEmotionDefinition(84, 83, -1, string.Empty),
            new RoEmotionDefinition(85, 84, -1, string.Empty),
            new RoEmotionDefinition(86, 85, -1, string.Empty),
            new RoEmotionDefinition(87, 86, -1, string.Empty)
        };

        public static string EmotionSpriteBasePath => RoAssetPath.Normalize("data/sprite/" + EffectSegment() + "/emotion");

        public static bool TryGetDefinition(int emotionId, out RoEmotionDefinition definition) {
            if (emotionId >= 200 && emotionId <= 205) {
                definition = new RoEmotionDefinition(emotionId, emotionId - 143, -1, "dice");
                return true;
            }

            if (emotionId >= 0 && emotionId < Definitions.Length) {
                definition = Definitions[emotionId];
                return true;
            }

            definition = default;
            return false;
        }

        public static int ResolveSpriteActionIndex(int emotionId) {
            return TryGetDefinition(emotionId, out RoEmotionDefinition definition)
                ? definition.SpriteActionIndex
                : -1;
        }

        public static string BuildDebugLabel(int emotionId) {
            return TryGetDefinition(emotionId, out RoEmotionDefinition definition) &&
                !string.IsNullOrWhiteSpace(definition.PrimaryCommand)
                    ? "/" + definition.PrimaryCommand
                    : "/e" + emotionId;
        }

        private static string EffectSegment() {
            return LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE);
        }

        private static string LegacyPathSegment(params int[] bytes) {
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) {
                chars[i] = (char) bytes[i];
            }

            return new string(chars);
        }
    }
}
