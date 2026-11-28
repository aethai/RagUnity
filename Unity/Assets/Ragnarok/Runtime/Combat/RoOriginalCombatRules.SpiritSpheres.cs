namespace Ragnarok.Combat {
    public static partial class RoOriginalCombatRules {
        // ---- spirit spheres (ZC_SPIRITS 0x01d0 / ZC_SPIRITS2 0x01e1) ----
        // The client's handler (game.exe fn_c99ba0) ends the holder's sphere effect, then, unless the count is 0 or the
        // holder is hiding / cloaking, launches the effect for the holder's job with effect msg 0xe(count, 0, 0):
        // 228 = fn_b6db80(0), 504 = fn_b6db80(1), 629 = fn_b6db80(3) coins.

        /// <summary>EF_CHOOKGI: the spheres of every job without its own (Monk, Champion, Sura, Royal Guard, ...).</summary>
        public const int SpiritSphereEffectDefault = 228;
        /// <summary>EF_CHOOKGI2: only job 4038 (Baby Monk) in the client.</summary>
        public const int SpiritSphereEffectBabyMonk = 504;
        /// <summary>EF_CHOOKGI_FIRE: the coins of the Gunslinger and the Baby Gunslinger.</summary>
        public const int SpiritSphereEffectCoins = 629;

        /// <summary>The sphere effect for a holder of <paramref name="baseJob"/> (mount / costume removed); -1 = none drawn.</summary>
        public static int ResolveSpiritSphereEffectId(short baseJob) {
            switch (baseJob) {
                case 4038:    // Baby Monk
                    return SpiritSphereEffectBabyMonk;
                case 24:      // Gunslinger
                case 4228:    // Baby Gunslinger
                    return SpiritSphereEffectCoins;
                case 4215:    // Rebellion, Baby Rebellion, Night Watch: new-system effect 0x46f
                case 4229:
                case 4306:
                case 4252:    // 4th classes with new-system effects 0x69e / 0x83d
                case 4260:
                case 4265:
                case 4280:
                    return -1;   // the client's new effect system is not ported
                default:
                    return SpiritSphereEffectDefault;
            }
        }
    }
}
