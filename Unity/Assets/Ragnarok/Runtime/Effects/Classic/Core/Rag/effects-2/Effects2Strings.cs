// Synced from reference/port/src/RoClassicFx/Rag/effects-2/Effects2Strings.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Non-ASCII (CP949) resource names referenced by the effects-2 handlers, as engine byte strings (one char per CP949 byte, read from game.exe) and kept
    /// with their addresses. Verified byte-for-byte against the executable by Effects2StringTests.
    /// </summary>
    public static class Effects2Strings
    {
        /// <summary>0x00ff3940 "effect\흡기.wav" (Absorb Spirit Sphere).</summary>
        public const string Wav_ff3940 = "effect\\\u00c8\u00ed\u00b1\u00e2.wav";
        /// <summary>0x00ff7b48 "effect\복호격.wav" (Divest Shield).</summary>
        public const string Wav_ff7b48 = "effect\\\u00ba\u00b9\u00c8\u00a3\u00b0\u00dd.wav";
        /// <summary>0x00ff3c04 "effect\매직 크래쉬.wav" (Stave Crasher).</summary>
        public const string Wav_ff3c04 = "effect\\\u00b8\u00c5\u00c1\u00f7 \u00c5\u00a9\u00b7\u00a1\u00bd\u00ac.wav";
        /// <summary>0x00ff3ba4 "effect\샤프슈팅.wav" (Focused Arrow Strike).</summary>
        public const string Wav_ff3ba4 = "effect\\\u00bb\u00fe\u00c7\u00c1\u00bd\u00b4\u00c6\u00c3.wav";
        /// <summary>0x00ff960c "effect\T_내려찍기.wav" (Heel Drop).</summary>
        public const string Wav_ff960c = "effect\\T_\u00b3\u00bb\u00b7\u00c1\u00c2\u00ef\u00b1\u00e2.wav";
        /// <summary>0x00ff9580 "effect\아숨프티오.wav" (Assumptio).</summary>
        public const string Wav_ff9580 = "effect\\\u00be\u00c6\u00bc\u00fb\u00c7\u00c1\u00c6\u00bc\u00bf\u00c0.wav";
        /// <summary>0x00ff4b2c "effect\T_안락한마법.wav" (Warmth of the Sun/Moon/Stars).</summary>
        public const string Wav_ff4b2c = "effect\\T_\u00be\u00c8\u00b6\u00f4\u00c7\u00d1\u00b8\u00b6\u00b9\u00fd.wav";
        /// <summary>0x00ff80d8 "effect\T_날라차기.wav" (Esma).</summary>
        public const string Wav_ff80d8 = "effect\\T_\u00b3\u00af\u00b6\u00f3\u00c2\u00f7\u00b1\u00e2.wav";
        /// <summary>0x00ffc09c "effect\mon_탄지신통.wav" (Throw Spirit Sphere).</summary>
        public const string Wav_ffc09c = "effect\\mon_\u00c5\u00ba\u00c1\u00f6\u00bd\u00c5\u00c5\u00eb.wav";

        /// <summary>0x00ff63e0 "유저인터페이스\item\빈병.bmp" (Acid Terror).</summary>
        public const string Tex_ff63e0 = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00ba\u00f3\u00ba\u00b4.bmp";
        /// <summary>0x00ff6290 "유저인터페이스\item\빈포션병.bmp" (Aid Potion).</summary>
        public const string Tex_ff6290 = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00ba\u00f3\u00c6\u00f7\u00bc\u00c7\u00ba\u00b4.bmp";
        /// <summary>0x00ff6400 "유저인터페이스\item\돌.bmp" (Stone Fling).</summary>
        public const string Tex_ff6400 = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00b5\u00b9.bmp";
        /// <summary>0x00ff62f4 "유저인터페이스\item\버서크포션.bmp" (Aid Berserk Potion).</summary>
        public const string Tex_ff62f4 = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00b9\u00f6\u00bc\u00ad\u00c5\u00a9\u00c6\u00f7\u00bc\u00c7.bmp";
        /// <summary>0x00ff6318 "유저인터페이스\item\베넘나이프.bmp" (Throw Venom Knife).</summary>
        public const string Tex_ff6318 = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00ba\u00a3\u00b3\u00d1\u00b3\u00aa\u00c0\u00cc\u00c7\u00c1.bmp";
        /// <summary>0x00ff633c "유저인터페이스\item\수리검.bmp" (Throw Shuriken).</summary>
        public const string Tex_ff633c = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00bc\u00f6\u00b8\u00ae\u00b0\u00cb.bmp";
        /// <summary>0x00ff5198 "유저인터페이스\item\쿠나이_독.bmp" (Throw Kunai).</summary>
        public const string Tex_ff5198 = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00c4\u00ed\u00b3\u00aa\u00c0\u00cc_\u00b5\u00b6.bmp";
        /// <summary>0x00ff635c "유저인터페이스\item\풍마_열화.bmp" (Throw Huuma Shuriken).</summary>
        public const string Tex_ff635c = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\item\\\u00c7\u00b3\u00b8\u00b6_\u00bf\u00ad\u00c8\u00ad.bmp";
        /// <summary>0x00ffb85c "effect\레드젬스톤.bmp".</summary>
        public const string Tex_ffb85c = "effect\\\u00b7\u00b9\u00b5\u00e5\u00c1\u00aa\u00bd\u00ba\u00c5\u00e6.bmp";
        /// <summary>0x00ffb874 "effect\블루젬스톤.bmp".</summary>
        public const string Tex_ffb874 = "effect\\\u00ba\u00ed\u00b7\u00e7\u00c1\u00aa\u00bd\u00ba\u00c5\u00e6.bmp";
        /// <summary>0x00ffb88c "effect\옐로우젬스톤.bmp".</summary>
        public const string Tex_ffb88c = "effect\\\u00bf\u00bb\u00b7\u00ce\u00bf\u00ec\u00c1\u00aa\u00bd\u00ba\u00c5\u00e6.bmp";
        /// <summary>0x00ff85b8 "effect\270바람.tga" (Esma).</summary>
        public const string Tex_ff85b8 = "effect\\270\u00b9\u00d9\u00b6\u00f7.tga";

        /// <summary>0x00ffa7d8 / 0x00ffa7ec "이팩트\썬더스톰.spr/.act" (Lightning Strike of Destruction).</summary>
        public const string Spr_ffa7d8 = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00bd\u00e3\u00b4\u00f5\u00bd\u00ba\u00c5\u00e8.spr";
        public const string Act_ffa7ec = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\\u00bd\u00e3\u00b4\u00f5\u00bd\u00ba\u00c5\u00e8.act";
        /// <summary>0x00ffa6cc / 0x00ffa6e0 "이팩트\Sight.spr/.act" (Sightrasher).</summary>
        public const string Spr_ffa6cc = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\Sight.spr";
        public const string Act_ffa6e0 = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\Sight.act";
        /// <summary>0x00fe25a0 / 0x00fe25b8 "이팩트\particle7.spr/.act" (Steal).</summary>
        public const string Spr_fe25a0 = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle7.spr";
        public const string Act_fe25b8 = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\particle7.act";

        /// <summary>(address, string) pairs for the verification test.</summary>
        public static readonly (uint Address, string Value)[] All =
        {
            (0x00ff3940, Wav_ff3940), (0x00ff7b48, Wav_ff7b48), (0x00ff3c04, Wav_ff3c04), (0x00ff3ba4, Wav_ff3ba4),
            (0x00ff960c, Wav_ff960c), (0x00ff9580, Wav_ff9580), (0x00ff4b2c, Wav_ff4b2c), (0x00ff80d8, Wav_ff80d8),
            (0x00ffc09c, Wav_ffc09c),
            (0x00ff63e0, Tex_ff63e0), (0x00ff6290, Tex_ff6290), (0x00ff6400, Tex_ff6400), (0x00ff62f4, Tex_ff62f4),
            (0x00ff6318, Tex_ff6318), (0x00ff633c, Tex_ff633c), (0x00ff5198, Tex_ff5198), (0x00ff635c, Tex_ff635c),
            (0x00ffb85c, Tex_ffb85c), (0x00ffb874, Tex_ffb874), (0x00ffb88c, Tex_ffb88c), (0x00ff85b8, Tex_ff85b8),
            (0x00ffa7d8, Spr_ffa7d8), (0x00ffa7ec, Act_ffa7ec), (0x00ffa6cc, Spr_ffa6cc), (0x00ffa6e0, Act_ffa6e0),
            (0x00fe25a0, Spr_fe25a0), (0x00fe25b8, Act_fe25b8),
        };
    }
}
