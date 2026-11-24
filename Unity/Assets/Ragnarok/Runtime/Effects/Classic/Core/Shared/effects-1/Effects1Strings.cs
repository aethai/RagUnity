// Synced from reference/port/src/RoClassicFx/Shared/effects-1/Effects1Strings.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Non-ASCII resource names used by the effects-1 handlers: the client's CP949 bytes mapped 1:1 to
    /// chars (Latin-1), exactly as the engine passes them to GetTexture / PlaySound3D / SetSprName.
    /// <see cref="Table"/> lists the game.exe address of each (verified byte-for-byte by the tests).
    /// </summary>
    public static partial class Effects1Strings
    {
        const string Efx = "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\";   // 이팩트\ (sprite folder)
        const string Ui = "\u00c0\u00af\u00c0\u00fa\u00c0\u00ce\u00c5\u00cd\u00c6\u00e4\u00c0\u00cc\u00bd\u00ba\\";   // 유저인터페이스\

        public const string SprParticle2 = Efx + "particle2.spr";
        public const string ActParticle2 = Efx + "particle2.act";
        public const string SprParticle3 = Efx + "particle3.spr";
        public const string ActParticle3 = Efx + "particle3.act";
        public const string SprCoin = Efx + "\u00b5\u00bf\u00c0\u00fc.spr";      // 동전
        public const string ActCoin = Efx + "\u00b5\u00bf\u00c0\u00fc.act";
        public const string SprParticle1 = Efx + "particle1.spr";
        public const string ActParticle1 = Efx + "particle1.act";
        public const string SprWaterball = Efx + "Waterball.spr";
        public const string ActWaterball = Efx + "Waterball.act";
        public const string SprSpear = Efx + "\u00c3\u00a2.spr";   // 창
        public const string ActSpear = Efx + "\u00c3\u00a2.act";
        public const string SprFireball = Efx + "fireball.spr";
        public const string ActFireball = Efx + "fireball.act";

        public const string Wav_ff7b48 = "effect\\\u00ba\u00b9\u00c8\u00a3\u00b0\u00dd.wav";                    // 봉황검
        public const string Wav_ff95f4 = "effect\\\u00bc\u00d2\u00bf\u00ef \u00c3\u00bc\u00c0\u00ce\u00c1\u00f6.wav"; // 소울 체인지
        public const string Wav_fe26ec = "effect\\T_\u00c0\u00fc\u00b1\u00e2.wav";                               // T_전기
        public const string Wav_ff8890 = "effect\\T_\u00b9\u00d9\u00b6\u00f7\u00b8\u00b6\u00b9\u00fd.wav";       // T_바람마법
        public const string Wav_ff8908 = "effect\\\u00c7\u00ae\u00b9\u00f6\u00bd\u00ba\u00c5\u00cd.wav";         // 풀버스터
        public const string Wav_ff8500 = "effect\\\u00b5\u00f0\u00bd\u00ba\u00be\u00cf.wav";                     // 디스암
        public const string Wav_ff8464 = "effect\\\u00b1\u00d7\u00b8\u00b2\u00c0\u00da\u00ba\u00a3\u00b1\u00e2.wav"; // 그림자베기
        public const string Wav_ff8450 = "effect\\\u00be\u00c8\u00b0\u00b3\u00ba\u00a3\u00b1\u00e2.wav";         // 안개베기
        public const string Wav_ff8440 = "effect\\\u00c0\u00cf\u00bc\u00b6.wav";                                 // 일섬
        public const string Wav_ff84c4 = "effect\\\u00c6\u00f8\u00bf\u00b0\u00b7\u00e6.wav";                     // 폭염룡
        public const string Wav_ff7eb4 = "effect\\\u00b5\u00a5\u00bd\u00ba\u00c6\u00e4\u00b6\u00f3\u00b5\u00b5.wav"; // 데스페라도
        public const string Wav_ff847c = "effect\\\u00b7\u00a1\u00c7\u00c7\u00b5\u00e5\u00bb\u00fe\u00bf\u00f6.wav"; // 래피드샤워
        public const string Wav_ff8494 = "effect\\\u00b8\u00c5\u00c1\u00f6\u00c4\u00c3\u00ba\u00ed\u00b8\u00b4.wav"; // 매지컬블릿
        public const string Wav_ff84ac = "effect\\\u00c6\u00ae\u00b8\u00ae\u00c7\u00c3\u00be\u00d7\u00bc\u00c7.wav"; // 트리플액션

        public const string Tga_ff85b8 = "effect\\270\u00b9\u00d9\u00b6\u00f7.tga";                                 // 270바람
        public const string Bmp_ff8514 = Ui + "item\\\u00b0\u00f1\u00b5\u00e5\u00b7\u00e8\u00bd\u00ba.bmp";
        public const string Bmp_ff8538 = Ui + "item\\\u00b4\u00f5\u00b0\u00b3\u00b8\u00ae\u00bd\u00bc.bmp";
        public const string Bmp_ff855c = Ui + "item\\\u00b4\u00f5\u00ba\u00ed\u00bc\u00a6\u00b0\u00c7.bmp";
        public const string Bmp_ff8580 = Ui + "item\\\u00b4\u00f5\u00bd\u00ba\u00c5\u00a9.bmp";

        public static readonly (uint Address, string Value)[] Table =
        {
            (0x00f7e6e0, SprParticle2), (0x00f7e6f8, ActParticle2), (0x00f7d9b0, SprParticle3), (0x00f7d9c8, ActParticle3),
            (0x00ffa680, SprCoin), (0x00ffa690, ActCoin), (0x00ffa724, SprFireball), (0x00ffa738, ActFireball),
            (0x00ffa408, SprParticle1), (0x00ffa420, ActParticle1), (0x00ffac5c, SprWaterball), (0x00ffac74, ActWaterball),
            (0x00ffaabc, SprSpear), (0x00ffaacc, ActSpear),
            (0x00ff7b48, Wav_ff7b48), (0x00ff95f4, Wav_ff95f4), (0x00fe26ec, Wav_fe26ec), (0x00ff8890, Wav_ff8890),
            (0x00ff8908, Wav_ff8908), (0x00ff8500, Wav_ff8500), (0x00ff8464, Wav_ff8464), (0x00ff8450, Wav_ff8450),
            (0x00ff8440, Wav_ff8440), (0x00ff84c4, Wav_ff84c4), (0x00ff7eb4, Wav_ff7eb4), (0x00ff847c, Wav_ff847c),
            (0x00ff8494, Wav_ff8494), (0x00ff84ac, Wav_ff84ac),
            (0x00ff85b8, Tga_ff85b8), (0x00ff8514, Bmp_ff8514), (0x00ff8538, Bmp_ff8538), (0x00ff855c, Bmp_ff855c), (0x00ff8580, Bmp_ff8580),
        };
    }
}
