using Ragnarok.Network;

namespace Ragnarok.Combat {
    public static partial class RoOriginalCombatRules {
        // ---- vanish / arrival ground effects (ZC_NOTIFY_VANISH handler FUN_00c5adc0, NEWENTRY 0xccb518; combat-timing §8) ----

        /// <summary>EF_TELEPORTATION2: the teleport departure columns.</summary>
        public const int EfTeleportation2 = 304;
        /// <summary>EF_EXIT2: the logout columns.</summary>
        public const int EfExit2 = 314;
        /// <summary>EF_ENTRY2: a player's arrival (NEWENTRY).</summary>
        public const int EfEntry2 = 344;
        /// <summary>EF_TOPRANK "PvP circle": under a PvP player ranked 10th or better (ZC_NOTIFY_RANKING 0xc59238).</summary>
        public const int EfTopRank = 159;
        /// <summary>EF_DEVIL: the local player's death (CPlayer::SetAction 0xcd71e7).</summary>
        public const int EfDevil = 372;
        /// <summary>
        /// EF 321, the map warp NPC's portal (job 45): CNpc::SetAction (0xcc2340) launches it once per warp object
        /// (flag +0x420) while the NPC is visible; the NPC's own sprite is never drawn.
        /// </summary>
        public const int EfWarpNpc = 321;

        /// <summary>Option bit 0x40: a GM's invisibility (no departure or arrival effect).</summary>
        public const int OptionInvisible = 0x40;

        /// <summary>
        /// The ground effect a vanish leaves at the actor's last position (FUN_00c028a0 on a CDummyEffectLauncher, life in
        /// 24 ms ticks): logged out → EF 314 (holder 600 ticks), teleported → EF 304 (300 ticks) unless the actor is
        /// invisible (option &amp; 0x40). Out of sight, died and trick dead leave none. 0 = no effect.
        /// </summary>
        public static int VanishGroundEffect(RoMapActorVanishType type, int option, out int holderTicks) {
            switch (type) {
                case RoMapActorVanishType.LoggedOut:
                    holderTicks = 600;
                    return EfExit2;
                case RoMapActorVanishType.Teleport when (option & OptionInvisible) == 0:
                    holderTicks = 300;
                    return EfTeleportation2;
                default:
                    holderTicks = 0;
                    return 0;
            }
        }

        /// <summary>A player's NEWENTRY (msg 0x79 par 2) launches EF 344 on it unless it is invisible; other entries none.</summary>
        public static bool ShowsEntryEffect(RoMapActorEntryKind kind, RoMapActorObjectType objectType, int option) =>
            kind == RoMapActorEntryKind.New && objectType == RoMapActorObjectType.Player && (option & OptionInvisible) == 0;

        // ---- the max-level aura (dispatcher FUN_00bcf420, actor msg handlers 0xbdb9b6 / 0xbdbc73; combat-timing §7.2) ----

        /// <summary>CLevel99Effect: the ground sparkle discs and three spinning blue bands.</summary>
        public const int EfLevel99 = 200;
        /// <summary>CLevel99Orb1Effect: the rising lights (whitelight.tga).</summary>
        public const int EfLevel99Orb1 = 976;
        /// <summary>CLevel99Orb2Effect: the babies' rising lights (freezing_circle.bmp).</summary>
        public const int EfLevel99Orb2 = 977;
        /// <summary>The 3rd jobs' aura pair.</summary>
        public const int EfLevel200 = 1226, EfLevel200Sub = 1227;
        /// <summary>HomunMaxLvAura: homunculi and 4th jobs.</summary>
        public const int EfHomunMaxLevel = 2275, EfHomunMaxLevelSub = 2276;

        /// <summary>The options that hide the aura: invisible 0x40, hide 0x02, cloak 0x04.</summary>
        public const int LevelAuraBlockingOptions = 0x46;

        /// <summary>
        /// The aura pair of an actor (main, sub; 0 / 0 = none). Only at exactly the category's max level (Custom Lua
        /// values): 4th jobs at 250 → 2275 / 2276; 3rd jobs, Extend2, the expanded upper jobs and Doram at 200 → 1226 /
        /// 1227; homunculi at 99 → 2275 / 2276; babies at 99 → 200 / 977; other players and mercenaries at 99 → 200 / 976.
        /// Monsters and NPCs never. The full aura launches both, the simple one (/aura) only the sub id.
        /// </summary>
        public static void LevelAuraIds(int job, int clevel, out int main, out int sub) {
            main = sub = 0;
            if (IsFourthJob(job)) {
                if (clevel == 250) {
                    main = EfHomunMaxLevel;
                    sub = EfHomunMaxLevelSub;
                }
            } else if (IsThirdJobOrLater(job)) {
                if (clevel == 200) {
                    main = EfLevel200;
                    sub = EfLevel200Sub;
                }
            } else if (IsHomunculusJob(job)) {
                if (clevel == 99) {
                    main = EfHomunMaxLevel;
                    sub = EfHomunMaxLevelSub;
                }
            } else if (IsBabyJob(job)) {
                if (clevel == 99) {
                    main = EfLevel99;
                    sub = EfLevel99Orb2;
                }
            } else if (IsPlayerJob(job) || IsMercenaryJob(job)) {
                if (clevel == 99) {
                    main = EfLevel99;
                    sub = EfLevel99Orb1;
                }
            }
        }

        /// <summary>Hide, cloak and GM invisibility take the aura off (SetOption FUN_00be3d80 re-dispatches).</summary>
        public static bool LevelAuraBlocked(int option) => (option & LevelAuraBlockingOptions) != 0;

        static bool IsFourthJob(int job) => (job >= 4252 && job <= 4299) || (job >= 4302 && job <= 4329);

        /// <summary>3rd and baby 3rd jobs, Extend2 (4190..4193), Kagerou / Oboro, Rebellion, Doram, Star Emperor / Soul Reaper.</summary>
        static bool IsThirdJobOrLater(int job) =>
            (job >= 4054 && job <= 4112) || (job >= 4190 && job <= 4193) || job == 4211 || job == 4212 || job == 4215 ||
            job == 4218 || job == 4220 || (job >= 4239 && job <= 4242);

        static bool IsBabyJob(int job) => job >= 4023 && job <= 4045;

        static bool IsHomunculusJob(int job) => (job >= 6001 && job <= 6016) || (job >= 6048 && job <= 6052);

        static bool IsMercenaryJob(int job) => job >= 6017 && job <= 6046;
    }
}
