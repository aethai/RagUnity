using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public enum RoEntityDisplayKind {
        Unknown = 0,
        Npc,
        Monster,
        Homunculus,
        Mercenary
    }

    public sealed class RoEntityDisplayMetadata {
        public RoEntityDisplayMetadata(
            int classId,
            RoEntityDisplayKind kind,
            string resourceName,
            string displayName = null,
            string aegisName = null,
            int level = 0
        ) {
            ClassId = Math.Max(0, classId);
            Kind = kind;
            SpriteName = NormalizeSpriteName(resourceName);
            int slashIndex = SpriteName.LastIndexOf('/');
            ResourceName = slashIndex >= 0 ? SpriteName.Substring(slashIndex + 1) : SpriteName;
            DisplayName = NormalizeText(string.IsNullOrWhiteSpace(displayName) ? ResourceName : displayName);
            AegisName = NormalizeText(aegisName);
            Level = Math.Max(0, level);
        }

        public int ClassId { get; }
        public RoEntityDisplayKind Kind { get; }
        /// <summary>The job table's name without any folder (a mercenary's <c>여\활용병</c> is <c>활용병</c>).</summary>
        public string ResourceName { get; }
        /// <summary>The job table's name as the client puts it into the sprite path, '/' separated (<c>여/활용병</c>).</summary>
        public string SpriteName { get; }
        public string DisplayName { get; }
        public string AegisName { get; }
        public int Level { get; }

        /// <summary>
        /// <c>data/sprite/{folder}/{name}</c> like CNpc::vf21 0xcc2230: the folder follows the kind
        /// (<see cref="RoEntitySpriteRules"/>), the name is the job table's, sub folders included.
        /// </summary>
        public string GetSpriteBasePath() {
            string folder = RoEntitySpriteRules.GetFolder(Kind);
            if (string.IsNullOrEmpty(SpriteName) || folder == null) {
                return string.Empty;
            }

            return "data/sprite/" + folder + "/" + SpriteName.ToLowerInvariant();
        }

        private static string NormalizeSpriteName(string value) {
            value = NormalizeText(value);
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            value = value.Replace('\\', '/').Trim().TrimStart('/');
            if (value.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".act", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(0, value.Length - 4);
            }

            return value;
        }

        private static string NormalizeText(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(value);
            return string.IsNullOrEmpty(decoded) ? value.Trim() : decoded.Trim();
        }
    }

    public sealed class RoEntityDisplayMetadataTable {
        private const int EntityDisplayCacheMagic = 0x544E4545; // EENT
        private const int EntityDisplayCacheVersion = 2; // 2: the client's job-range kinds (+ Mercenary), the sprite name with its sub folder
        private const int MaxCachedEntities = 1_000_000;

        private static readonly Regex ConstantAssignmentPattern = new Regex(
            @"(?<key>[A-Za-z_][A-Za-z0-9_\.]*)\s*=\s*(?<value>-?(?:0x[0-9A-Fa-f]+|\d+))"
        );

        private static readonly Regex IndexedStringPattern = new Regex(
            @"\[\s*(?<key>[^\]]+)\s*\]\s*=\s*(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>"
        );

        private static readonly Regex BareStringPattern = new Regex(
            @"(?<![\w\.])(?<key>[A-Za-z_][A-Za-z0-9_\.]*)\s*=\s*(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>"
        );

        private static readonly Dictionary<int, string> KnownNpcFallbackResourceNames = new Dictionary<int, string> {
            // Generated from roBrowser MonsterTable NPC-looking entries. This keeps
            // NPC sprites distinct when official GRFs ship compiled Lua tables.
            { 45, "1_ETC_01" },
            { 46, "1_ETC_01" },
            { 47, "1_M_01" },
            { 48, "1_M_02" },
            { 49, "1_M_03" },
            { 50, "1_M_04" },
            { 51, "1_M_BARD" },
            { 52, "1_M_HOF" },
            { 53, "1_M_INNKEEPER" },
            { 54, "1_M_JOBGUIDER" },
            { 55, "1_M_JOBTESTER" },
            { 56, "1_M_KNIGHTMASTER" },
            { 57, "1_M_LIBRARYMASTER" },
            { 58, "1_M_MERCHANT" },
            { 59, "1_M_ORIENT01" },
            { 60, "1_M_PASTOR" },
            { 61, "1_M_PUBMASTER" },
            { 62, "1_M_SIZ" },
            { 63, "1_M_SMITH" },
            { 64, "1_M_WIZARD" },
            { 65, "1_M_YOUNGKNIGHT" },
            { 66, "1_F_01" },
            { 67, "1_F_02" },
            { 68, "1_F_03" },
            { 69, "1_F_04" },
            { 70, "1_F_GYPSY" },
            { 71, "1_F_LIBRARYGIRL" },
            { 72, "1_F_MARIA" },
            { 73, "1_F_MERCHANT_01" },
            { 74, "1_F_MERCHANT_02" },
            { 75, "1_F_ORIENT_01" },
            { 76, "1_F_ORIENT_02" },
            { 77, "1_F_ORIENT_03" },
            { 78, "1_F_ORIENT_04" },
            { 79, "1_F_PRIEST" },
            { 80, "1_F_PUBGIRL" },
            { 81, "4_DOG01" },
            { 82, "4_KID01" },
            { 83, "4_M_01" },
            { 84, "4_M_02" },
            { 85, "4_M_03" },
            { 86, "4_M_04" },
            { 87, "4_M_BARBER" },
            { 88, "4_M_ORIENT01" },
            { 89, "4_M_ORIENT02" },
            { 90, "4_F_01" },
            { 91, "4_F_02" },
            { 92, "4_F_03" },
            { 93, "4_F_04" },
            { 94, "4_F_MAID" },
            { 95, "4_F_SISTER" },
            { 96, "4W_KID" },
            { 97, "4W_M_01" },
            { 98, "4W_M_02" },
            { 99, "4W_M_03" },
            { 100, "4W_SAILOR" },
            { 101, "4W_F_01" },
            { 102, "8_F" },
            { 103, "8_F_GRANDMOTHER" },
            { 104, "1_ETC_01" },
            { 105, "8W_SOLDIER" },
            { 106, "1_M_MOC_LORD" },
            { 107, "1_M_PAY_ELDER" },
            { 108, "1_M_PRON_KING" },
            { 109, "4_M_MANAGER" },
            { 110, "4_M_MINISTER" },
            { 112, "4_F_KAFRA6" },
            { 113, "4_F_KAFRA5" },
            { 114, "4_F_KAFRA4" },
            { 115, "4_F_KAFRA3" },
            { 116, "4_F_KAFRA2" },
            { 117, "4_F_KAFRA1" },
            { 118, "2_M_THIEFMASTER" },
            { 119, "2_M_SWORDMASTER" },
            { 120, "2_M_PHARMACIST" },
            { 121, "2_M_MOLGENSTEIN" },
            { 122, "2_M_DYEINGER" },
            { 123, "2_F_MAGICMASTER" },
            { 124, "4_F_TELEPORTER" },
            { 125, "4_M_TELEPORTER" },
            { 400, "Poring" },
            { 401, "4_M_MUT2" },
            { 402, "4_M_SCIENCE" },
            { 403, "4_F_VALKYRIE2" },
            { 404, "4_M_UNCLEKNIGHT" },
            { 405, "4_M_YOUNGKNIGHT" },
            { 406, "2_MONEMUS" },
            { 407, "4_M_ATEIL" },
            { 408, "4_F_ANNIVERSARY" },
            { 409, "4_M_GREATPO" },
            { 410, "4_M_NOVELIST" },
            { 411, "4_M_CHAMPSOUL" },
            { 412, "4_M_OLDFRIAR" },
            { 413, "4_M_CRU_SOLD" },
            { 414, "4_M_CRU_KNT" },
            { 415, "4_M_CRU_HEAD" },
            { 416, "4_M_CRU_CRUA" },
            { 417, "4_M_KY_SOLD" },
            { 418, "4_M_KY_KNT" },
            { 419, "4_M_KY_HEAD" },
            { 420, "4_M_KY_KIYOM" },
            { 421, "4_M_BOSSCAT" },
            { 422, "4_M_BABYCAT" },
            { 423, "4W_F_KAFRA2" },
            { 424, "4_F_MUNAK" },
            { 425, "4_M_BONGUN" },
            { 426, "4_BEAR" },
            { 427, "4_BLUEWOLF" },
            { 428, "4_PECOPECO" },
            { 429, "4_M_JP_MID" },
            { 430, "4_M_JP_RUN" },
            { 431, "4_ORCLADY" },
            { 432, "4_ORCLADY2" },
            { 433, "4_ORCWARRIOR" },
            { 434, "4_ORCWARRIOR2" },
            { 435, "4_F_FAIRY" },
            { 436, "4_F_FAIRYKID" },
            { 437, "4_F_FAIRYKID2" },
            { 438, "4_F_FAIRYKID3" },
            { 439, "4_F_FAIRYKID4" },
            { 440, "4_F_FAIRYKID5" },
            { 441, "4_F_FAIRYKID6" },
            { 442, "4_M_FAIRYKID" },
            { 443, "4_M_FAIRYKID2" },
            { 444, "4_M_FAIRYKID3" },
            { 445, "4_M_FAIRYKID4" },
            { 446, "4_M_FAIRYKID5" },
            { 447, "4_M_FAIRYSOLDIER" },
            { 448, "4_M_TUFFOLD" },
            { 449, "4_MAN_BENKUNI" },
            { 450, "4_MAN_GALTUN" },
            { 451, "4_MAN_JERUTOO" },
            { 452, "4_MAN_LAVAIL" },
            { 453, "4_MAN_NITT" },
            { 454, "4_MAN_PIOM" },
            { 455, "4_MAN_PIOM2" },
            { 456, "4_M_DSTMAN" },
            { 457, "4_M_DSTMANDEAD" },
            { 458, "4_BABYLEOPARD" },
            { 459, "4_M_REDSWORD" },
            { 460, "4_MAN_PIOM3" },
            { 461, "4_M_FAIRYSOLDIER2" },
            { 462, "4_F_FAIRYSOLDIER" },
            { 463, "4_DRAGON_EGG" },
            { 464, "4_MIMIC" },
            { 465, "4_F_FAIRY1" },
            { 466, "4_F_GUILLOTINE" },
            { 467, "4_M_GUILLOTINE" },
            { 468, "4_M_KNIGHT_BLACK" },
            { 469, "4_M_KNIGHT_GOLD" },
            { 470, "4_M_KNIGHT_SILVER" },
            { 471, "4_SKULL_MUD" },
            { 472, "4_M_BRZ_INDIAN" },
            { 473, "4_F_BRZ_INDIAN" },
            { 474, "4_F_BRZ_INDOLD" },
            { 475, "4_M_BRZ_JACI" },
            { 476, "4_M_BRZ_MAN1" },
            { 477, "4_M_BRZ_MAN2" },
            { 478, "4_F_BRZ_WOMAN" },
            { 479, "4_M_MINSTREL" },
            { 480, "4_M_MINSTREL1" },
            { 481, "4_M_SHADOWCHASER" },
            { 482, "4_F_SHADOWCHASER" },
            { 483, "4_M_SURA" },
            { 484, "4_F_SURA" },
            { 485, "4_F_WANDERER" },
            { 486, "4_M_BARD" },
            { 487, "1_FLAG_NOFEAR" },
            { 488, "4_M_NOFEARGUY" },
            { 489, "4_MAN_PIOM6" },
            { 490, "4_MAN_PIOM4" },
            { 491, "4_MAN_PIOM5" },
            { 492, "4_MAN_GALTUN1" },
            { 493, "4_HUMAN_GERUTOO" },
            { 494, "4_M_ROKI" },
            { 495, "4_M_MERCAT1" },
            { 496, "4_M_MERCAT2" },
            { 497, "4_M_CATMAN1" },
            { 498, "4_M_CATMAN2" },
            { 499, "4_F_BRZ_WOMAN2" },
            { 500, "4_M_JP_DISH" },
            { 501, "4_F_JP_NOAH" },
            { 502, "4_F_JP_OZ" },
            { 503, "4_F_JP_CHROME" },
            { 504, "4_F_JP_RINNE" },
            { 505, "4_WHITETIGER" },
            { 506, "4_VENDING_MACHINE" },
            { 507, "4_MISTY" },
            { 508, "4_NECORING" },
            { 509, "4_ELEPHANT" },
            { 510, "4_F_NYDHOG" },
            { 511, "4_F_NYDHOG2" },
            { 512, "4_M_ROKI2" },
            { 513, "4_M_DOGTRAVELER" },
            { 514, "4_M_DOGTRAVELER2" },
            { 515, "4_F_DOGTRAVELER" },
            { 516, "4_M_RAFLE_GR" },
            { 517, "4_M_RAFLE_OLD" },
            { 518, "4_F_RAFLE_PK" },
            { 519, "4_M_LYINGDOG" },
            { 520, "4_F_MORAFINE1" },
            { 521, "4_F_MORAFINE2" },
            { 522, "4_M_RAFLE_OR" },
            { 523, "4_F_RAFLE_YE" },
            { 524, "4_M_RAFLE_VI" },
            { 525, "4_F_RAFLE_VI" },
            { 526, "4_M_ARDHA" },
            { 527, "4_CREEPER" },
            { 528, "JP_RUFAKU" },
            { 529, "JP_SUPIKA" },
            { 530, "JP_SABIKU" },
            { 531, "JP_ARUGORU" },
            { 532, "JP_ARUNA" },
            { 533, "JP_AIRI" },
            { 534, "4_M_DEWOLDMAN" },
            { 535, "4_M_DEWOLDWOMAN" },
            { 536, "4_M_DEWMAN" },
            { 537, "4_M_DEWWOMAN" },
            { 538, "4_M_DEWBOY" },
            { 539, "4_M_DEWGIRL" },
            { 540, "4_M_DEWZATICHIEF" },
            { 541, "4_M_DEWZATIMAN" },
            { 542, "4_M_ALCHE_E" },
            { 543, "4_MASK_SMOKEY" },
            { 544, "4_CAT_SAILOR1" },
            { 545, "4_CAT_SAILOR2" },
            { 546, "4_CAT_SAILOR3" },
            { 547, "4_CAT_SAILOR4" },
            { 548, "4_CAT_CHEF" },
            { 549, "4_CAT_MERMASTER" },
            { 550, "4_CRACK" },
            { 551, "4_ASTER" },
            { 552, "4_F_STARFISHGIRL" },
            { 553, "4_CAT_DOWN" },
            { 554, "4_CAT_REST" },
            { 555, "4_CAT_3COLOR" },
            { 556, "4_CAT_ADMIRAL" },
            { 557, "4_SOIL" },
            { 558, "4_F_ALCHE_A" },
            { 559, "4_CAT_ADV1" },
            { 560, "4_CAT_ADV2" },
            { 561, "4_CAT_SAILOR5" },
            { 562, "2_DROP_MACHINE" },
            { 563, "2_SLOT_MACHINE" },
            { 564, "2_VENDING_MACHINE1" },
            { 565, "MOB_TOMB" },
            { 566, "MYSTCASE" },
            { 567, "4_M_SIT_NOVICE" },
            { 568, "4_OCTOPUS_LEG" },
            { 569, "4_F_NURSE" },
            { 570, "4_MAL_SOLDIER" },
            { 571, "4_MAL_CAPTAIN" },
            { 572, "4_MAL_BUDIDAI" },
            { 573, "4_M_MAYOR" },
            { 574, "4_M_BARYO_OLD" },
            { 575, "4_F_BARYO_OLD" },
            { 576, "4_F_BARYO_GIRL" },
            { 577, "4_M_BARYO_BOY" },
            { 578, "4_M_BARYO_MAN" },
            { 579, "4_F_BARYO_WOMAN" },
            { 580, "4_BARYO_CHIEF" },
            { 581, "4_MAL_KAFRA" },
            { 582, "4_M_MALAYA" },
            { 583, "4_F_MALAYA" },
            { 584, "4_F_PATIENT" },
            { 585, "4_M_PATIENT" },
            { 586, "4_F_KR_TIGER" },
            { 587, "4_M_KR_BOY" },
            { 588, "4_M_KAGE_OLD" },
            { 589, "4_WHIKEBAIN" },
            { 590, "4_EREND" },
            { 591, "4_RAWREL" },
            { 592, "4_ARMAIA" },
            { 593, "4_KAVAC" },
            { 594, "4_YGNIZEM" },
            { 595, "4_EREMES" },
            { 596, "4_MAGALETA" },
            { 597, "4_KATRINN" },
            { 598, "4_SHECIL" },
            { 599, "4_SEYREN" },
            { 600, "4_HARWORD" },
            { 601, "4_F_JP_CYNTHIA" },
            { 602, "4_M_JP_GUSTON" },
            { 603, "4_M_JP_BERKUT" },
            { 604, "4_F_JP_DARK_ADELAIDE" },
            { 605, "4_M_JP_DARK_DARIUS" },
            { 606, "4_M_JP_JESTER" },
            { 607, "XMAS_SMOKEY_B" },
            { 608, "XMAS_SMOKEY_R" },
            { 609, "XMAS_SMOKEY_Y" },
            { 610, "4_f_clockdoll" },
            { 611, "4_f_fairy2" },
            { 612, "4_f_pinkwoman" },
            { 613, "4_fairydeadleaf" },
            { 614, "4_frog" },
            { 615, "4_m_blackman" },
            { 616, "4_m_blueman" },
            { 617, "4_m_fairyang" },
            { 618, "4_m_fairyavant" },
            { 619, "4_m_fairyfreak" },
            { 620, "4_m_fairykid6" },
            { 621, "4_m_fairyscholar" },
            { 622, "4_m_fairyscholar_dirty" },
            { 623, "4_m_fariy_hisie" },
            { 624, "4_m_fariyking" },
            { 625, "4_m_newoz" },
            { 626, "4_m_oliver" },
            { 627, "4_m_professorworm" },
            { 628, "4_m_redman" },
            { 629, "4_F_GELKA" },
            { 630, "4_M_ROTERT" },
            { 631, "4_BLACKDRAGON" },
            { 632, "4_M_GUNSLINGER" },
            { 633, "4_F_GUNSLINGER" },
            { 634, "4_M_ARCHER" },
            { 635, "4_M_SWORDMAN" },
            { 636, "4_M_NINJA_RED" },
            { 637, "4_M_NINJA_BLUE" },
            { 638, "4_M_THIEF_RUMIN" },
            { 639, "4_M_NOV_RUMIN" },
            { 640, "4_F_MAYSEL" },
            { 641, "4_F_ACOLYTE" },
            { 642, "4_M_NOV_HUNT" },
            { 643, "4_F_GENETIC" },
            { 644, "4_F_TAEKWON" },
            { 645, "4_F_SWORDMAN" },
            { 646, "4_F_IU" },
            { 647, "4_M_RAGI" },
            { 648, "4_M_MELODY" },
            { 649, "4_TRACE" },
            { 650, "4_F_HIMEL" },
            { 651, "4_LEVITATEMAN" },
            { 652, "4_M_HEINRICH" },
            { 653, "4_M_ROYALGUARD" },
            { 654, "4_M_BARMUND" },
            { 655, "4_F_KHALITZBURG" },
            { 656, "4_F_HIMEL2" },
            { 657, "4_WHITEKNIGHT" },
            { 658, "4_COCO" },
            { 659, "4_M_ALADDIN" },
            { 660, "4_M_GENIE" },
            { 661, "4_F_GENIE" },
            { 662, "4_JP_MID_SWIM" },
            { 663, "4_JP_RUNE_SWIM" },
            { 664, "4_F_FENRIR" },
            { 665, "4_F_GEFFEN_FAY" },
            { 666, "4_F_IRIS" },
            { 667, "4_F_LUCILE" },
            { 668, "4_F_SARAH_BABY" },
            { 669, "4_GEFFEN_01" },
            { 670, "4_GEFFEN_02" },
            { 671, "4_GEFFEN_03" },
            { 672, "4_GEFFEN_04" },
            { 673, "4_GEFFEN_05" },
            { 674, "4_GEFFEN_06" },
            { 675, "4_GEFFEN_07" },
            { 676, "4_GEFFEN_08" },
            { 677, "4_GEFFEN_09" },
            { 678, "4_GEFFEN_10" },
            { 679, "4_GEFFEN_11" },
            { 680, "4_GEFFEN_12" },
            { 681, "4_GEFFEN_13" },
            { 682, "4_GEFFEN_14" },
            { 683, "4_M_CHAOS" },
            { 684, "4_M_CHIEF_IRIN" },
            { 685, "4_M_SAKRAY" },
            { 686, "4_M_SAKRAYROYAL" },
            { 687, "4_TOWER_01" },
            { 688, "4_TOWER_02" },
            { 689, "4_TOWER_03" },
            { 690, "4_TOWER_04" },
            { 691, "4_TOWER_05" },
            { 692, "4_TOWER_06" },
            { 693, "4_TOWER_07" },
            { 694, "4_TOWER_08" },
            { 695, "4_TOWER_09" },
            { 696, "4_TOWER_10" },
            { 697, "4_TOWER_11" },
            { 698, "4_TOWER_12" },
            { 699, "4_TOWER_13" },
            { 700, "8_F_GIRL" },
            { 701, "4_F_GODEMOM" },
            { 702, "4_F_GON" },
            { 703, "4_F_KID2" },
            { 704, "4_M_BIBI" },
            { 705, "4_M_GEF_SOLDIER" },
            { 706, "4_M_KID1" },
            { 707, "4_M_MOC_SOLDIER" },
            { 708, "4_M_PAY_SOLDIER" },
            { 709, "4_M_SEAMAN" },
            { 710, "4_M_SNOWMAN" },
            { 711, "4_F_05" },
            { 712, "4_M_05" },
            { 713, "4_M_06" },
            { 714, "4_F_06" },
            { 715, "4_M_PIERROT" },
            { 716, "4_M_KID2" },
            { 717, "4_F_KID3" },
            { 718, "4_M_SANTA" },
            { 719, "4_F_NACORURI" },
            { 720, "4_F_SHAMAN" },
            { 721, "4_F_KAFRA7" },
            { 722, "Guildflag90_1.gr2" },
            { 723, "1_SHADOW_NPC" },
            { 724, "4_F_07" },
            { 725, "4_F_JOB_ASSASSIN" },
            { 726, "4_F_JOB_BLACKSMITH" },
            { 727, "4_F_JOB_HUNTER" },
            { 728, "4_F_JOB_KNIGHT" },
            { 729, "4_F_NOVICE" },
            { 730, "4_M_JOB_ASSASSIN" },
            { 731, "4_M_JOB_BLACKSMITH" },
            { 732, "4_M_JOB_HUNTER" },
            { 733, "4_M_JOB_KNIGHT1" },
            { 734, "4_M_JOB_KNIGHT2" },
            { 735, "4_M_JOB_WIZARD" },
            { 736, "4_BAPHOMET" },
            { 737, "4_DARKLORD" },
            { 738, "4_DEVIRUCHI" },
            { 739, "8_DOPPEL" },
            { 740, "2_M_ALCHE" },
            { 741, "2_M_BARD_ORIENT" },
            { 742, "2_M_SAGE_B" },
            { 743, "2_M_SAGE_OLD" },
            { 744, "4_F_ALCHE" },
            { 745, "4_F_CRU" },
            { 746, "4_F_MONK" },
            { 747, "4_F_ROGUE" },
            { 748, "4_M_ALCHE_A" },
            { 749, "4_M_ALCHE_B" },
            { 750, "4_M_ALCHE_C" },
            { 751, "4_M_CRU" },
            { 752, "4_M_CRU_OLD" },
            { 753, "4_M_MONK" },
            { 754, "4_M_SAGE_A" },
            { 755, "4_M_SAGE_C" },
            { 756, "4_F_SON" },
            { 757, "4_F_JPN2" },
            { 758, "4_F_JPN" },
            { 759, "4_F_JPNCHIBI" },
            { 760, "4_F_JPNOBA2" },
            { 761, "4_F_JPNOBA" },
            { 762, "4_M_JPN2" },
            { 763, "4_M_JPN" },
            { 764, "4_M_JPNCHIBI" },
            { 765, "4_M_JPNOJI2" },
            { 766, "4_M_JPNOJI" },
            { 767, "8_M_JPNSOLDIER" },
            { 768, "8_M_JPNMASTER" },
            { 769, "4_F_JPNMU" },
            { 770, "4_F_TWGIRL" },
            { 771, "4_F_TWGRANDMOM" },
            { 772, "4_F_TWMASKGIRL" },
            { 773, "4_F_TWMIDWOMAN" },
            { 774, "4_M_TWBOY" },
            { 775, "4_M_TWMASKMAN" },
            { 776, "4_M_TWMIDMAN" },
            { 777, "4_M_TWOLDMAN" },
            { 778, "4_M_TWTEAMAN" },
            { 779, "4_M_YOYOROGUE" },
            { 780, "8_M_TWSOLDIER" },
            { 781, "4_F_UMGIRL" },
            { 782, "4_F_UMOLDWOMAN" },
            { 783, "4_F_UMWOMAN" },
            { 784, "4_M_UMCHIEF" },
            { 785, "4_M_UMDANCEKID2" },
            { 786, "4_M_UMDANCEKID" },
            { 787, "4_M_UMKID" },
            { 788, "4_M_UMOLDMAN" },
            { 789, "4_M_UMSOLDIER" },
            { 790, "4_M_SALVATION" },
            { 791, "4_F_NFDEADKAFRA" },
            { 792, "4_F_NFDEADMGCIAN" },
            { 793, "4_F_NFLOSTGIRL" },
            { 794, "4_M_NFDEADMAN2" },
            { 795, "4_M_NFDEADMAN" },
            { 796, "4_M_NFDEADSWDMAN" },
            { 797, "4_M_NFLOSTMAN" },
            { 798, "4_M_NFMAN" },
            { 799, "4_NFBAT" },
            { 800, "4_NFCOCK" },
            { 801, "4_NFCOFFIN" },
            { 802, "4_NFWISP" },
            { 803, "1_F_SIGNZISK" },
            { 804, "1_M_SIGN1" },
            { 805, "1_M_SIGNALCHE" },
            { 806, "1_M_SIGNART" },
            { 807, "1_M_SIGNMCNT" },
            { 808, "1_M_SIGNMONK2" },
            { 809, "1_M_SIGNMONK" },
            { 810, "1_M_SIGNROGUE" },
            { 811, "4_F_VALKYRIE" },
            { 812, "TW_TOWER" },
            { 813, "2_M_OLDBLSMITH" },
            { 814, "4_F_CHNDOCTOR" },
            { 815, "4_F_CHNDRESS1" },
            { 816, "4_F_CHNDRESS2" },
            { 817, "4_F_CHNDRESS3" },
            { 818, "4_F_CHNWOMAN" },
            { 819, "4_M_CHN8GUEK" },
            { 820, "4_M_CHNCOOK" },
            { 821, "4_M_CHNGENERL" },
            { 822, "4_M_CHNMAN" },
            { 823, "4_M_CHNMONK" },
            { 824, "4_M_CHNOLD" },
            { 825, "4_M_CHNSOLDIER" },
            { 826, "4_M_DWARF" },
            { 827, "4_M_GRANDMONK" },
            { 828, "4_M_ROGUE" },
            { 829, "4_M_DOMINO" },
            { 830, "4_F_DOMINO" },
            { 831, "4_F_ZONDAGIRL" },
            { 832, "4_M_REIDIN_KURS" },
            { 833, "4_M_ZONDAOYAJI" },
            { 834, "4_M_BUDDHIST" },
            { 835, "2_BOARD1" },
            { 836, "2_BOARD2" },
            { 837, "2_BULLETIN_BOARD" },
            { 838, "4_F_THAIAYO" },
            { 839, "4_F_THAIGIRL" },
            { 840, "4_F_THAISHAMAN" },
            { 841, "4_M_THAIAYO" },
            { 842, "4_M_THAIOLD" },
            { 843, "4_M_THAIONGBAK" },
            { 844, "CLEAR_NPC" },
            { 845, "4_F_RACING" },
            { 846, "4_F_EINOLD" },
            { 847, "4_M_EINOLD" },
            { 848, "4_M_EINMINER" },
            { 849, "4_M_DIEMAN" },
            { 850, "4_F_EINWOMAN" },
            { 851, "4_M_REPAIR" },
            { 852, "4_M_EIN_SOLDIER" },
            { 853, "4_M_YURI" },
            { 854, "4_M_EINMAN2" },
            { 855, "4_M_EINMAN" },
            { 856, "2_F_SIGN1" },
            { 857, "4_BOARD3" },
            { 858, "4_BULLETIN_BOARD2" },
            { 859, "4_F_AGENTKAFRA" },
            { 860, "4_F_KAFRA8" },
            { 861, "4_F_KAFRA9" },
            { 862, "4_F_LGTGIRL" },
            { 863, "4_F_LGTGRAND" },
            { 864, "4_F_OPERATION" },
            { 865, "4_LGTSCIENCE" },
            { 866, "4_M_LGTGRAND" },
            { 867, "4_M_LGTGUARD2" },
            { 868, "4_M_LGTGUARD" },
            { 869, "4_M_LGTMAN" },
            { 870, "4_M_LGTPOOR" },
            { 871, "4_M_OPERATION" },
            { 872, "4_M_PRESIDENT" },
            { 873, "4_M_REINDEER" },
            { 874, "4_M_ZONDAMAN" },
            { 875, "4_M_PECOKNIGHT" },
            { 876, "4_CAT" },
            { 877, "4_F_YUNYANG" },
            { 878, "4_M_OILMAN" },
            { 879, "4_F_CAPEGIRL" },
            { 880, "4_M_MASKMAN" },
            { 881, "4_M_SITDOWN" },
            { 882, "4_F_SITDOWN" },
            { 883, "4_M_ALCHE_D" },
            { 884, "4_M_ACROSS" },
            { 885, "4_F_ACROSS" },
            { 886, "4_COOK" },
            { 887, "4_M_LIEMAN" },
            { 888, "2_POSTBOX" },
            { 889, "4_BULL" },
            { 890, "4_LAM" },
            { 891, "4_F_HUGIRL" },
            { 892, "4_F_HUGRANMA" },
            { 893, "4_F_HUWOMAN" },
            { 894, "4_F_KHELLISIA" },
            { 895, "4_F_KHELLY" },
            { 896, "4_M_HUBOY" },
            { 897, "4_M_HUGRANFA" },
            { 898, "4_M_HUMAN_01" },
            { 899, "4_M_HUMAN_02" },
            { 900, "4_M_HUMERCHANT" },
            { 901, "4_M_HUOLDARMY" },
            { 902, "4_M_KHKIEL" },
            { 903, "4_M_KHKYEL" },
            { 904, "4_M_KHMAN" },
            { 905, "4_F_KHWOMAN" },
            { 906, "4_F_KHGIRL" },
            { 907, "4_M_KHBOY" },
            { 908, "4_M_PHILMAN" },
            { 909, "4_PORING" },
            { 910, "2_COLAVEND" },
            { 911, "4_F_SOCCER" },
            { 912, "4_M_SOCCER7" },
            { 913, "4_M_SOCCER9" },
            { 914, "4_F_CHILD" },
            { 915, "4_F_MADAME" },
            { 916, "4_F_MASK1" },
            { 917, "4_F_MASK" },
            { 918, "4_F_RACHOLD" },
            { 919, "4_F_SHABBY" },
            { 920, "4_F_TRAINEE" },
            { 921, "4_M_CHILD1" },
            { 922, "4_M_CHILD" },
            { 923, "4_M_DOCTOR" },
            { 924, "4_M_FROZEN1" },
            { 925, "4_M_FROZEN" },
            { 926, "4_M_MASK1" },
            { 927, "4_M_MASK" },
            { 928, "4_M_MIDDLE1" },
            { 929, "4_M_MIDDLE" },
            { 930, "4_M_RACHMAN2" },
            { 931, "4_M_RACHMAN1" },
            { 932, "4_M_RACHOLD1" },
            { 933, "4_M_RACHOLD" },
            { 934, "4_M_RASWORD" },
            { 935, "4_M_TRAINEE" },
            { 936, "4_F_ARUNA_POP" },
            { 937, "4_M_ARUNA_NFM1" },
            { 938, "4_DST_CAMEL" },
            { 939, "4_DST_SOLDIER" },
            { 940, "4_F_DESERT" },
            { 941, "4_F_DST_CHILD" },
            { 942, "4_F_DST_GRAND" },
            { 943, "4_M_DESERT" },
            { 944, "4_M_DST_CHILD" },
            { 945, "4_M_DST_GRAND" },
            { 946, "4_M_DST_MASTER" },
            { 947, "4_M_DST_TOUGH" },
            { 948, "4_ANGELING" },
            { 949, "4_ARCHANGELING" },
            { 950, "4_GHOSTRING" },
            { 951, "4_F_EDEN_MASTER" },
            { 952, "4_F_EDEN_OFFICER" },
            { 953, "4_M_EDEN_GUARDER" },
            { 954, "4_M_PATRICK" },
            { 955, "4_DONKEY" },
            { 956, "4_M_TRISTAN" },
            { 957, "4_WHITE_COW" },
            { 958, "4_F_RUSCHILD" },
            { 959, "4_F_RUSWOMAN1" },
            { 960, "4_F_RUSWOMAN2" },
            { 961, "4_F_RUSWOMAN3" },
            { 962, "4_M_RUSCHILD" },
            { 963, "4_M_GUSLIMAN" },
            { 964, "4_M_RUSBALD" },
            { 965, "4_M_RUSKING" },
            { 966, "4_M_RUSKNIGHT" },
            { 967, "4_M_RUSMAN1" },
            { 968, "4_M_RUSMAN2" },
            { 969, "4_M_DRAKE" },
            { 970, "4_F_BABAYAGA" },
            { 971, "4_F_RUSGREEN" },
            { 972, "4_RUS_DWOLF" },
            { 973, "1_FLAG_LION" },
            { 974, "1_FLAG_EAGLE" },
            { 975, "4_M_MIKID" },
            { 976, "4_BLUE_FLOWER" },
            { 977, "4_RED_FLOWER" },
            { 978, "4_YELL_FLOWER" },
            { 979, "4_F_CAVE1" },
            { 980, "4_F_MUT1" },
            { 981, "4_F_MUT2" },
            { 982, "4_F_SCIENCE" },
            { 983, "4_M_1STPRIN1" },
            { 984, "4_M_1STPRIN2" },
            { 985, "4_M_2NDPRIN1" },
            { 986, "4_M_2NDPRIN2" },
            { 987, "4_M_3RDPRIN1" },
            { 988, "4_M_3RDPRIN2" },
            { 989, "4_M_4THPRIN1" },
            { 990, "4_M_4THPRIN2" },
            { 991, "4_M_5THPRIN1" },
            { 992, "4_M_5THPRIN2" },
            { 993, "4_M_6THPRIN1" },
            { 994, "4_M_6THPRIN2" },
            { 995, "4_M_CASMAN1" },
            { 996, "4_M_CAVE1" },
            { 997, "4_M_MOCASS1" },
            { 998, "4_M_MOCASS2" },
            { 999, "4_M_MUT1" },
            { 2245, "8W_SOLDIER" }
        };

        private readonly Dictionary<int, RoEntityDisplayMetadata> entities;

        public static readonly RoEntityDisplayMetadataTable Empty = new RoEntityDisplayMetadataTable(
            new Dictionary<int, RoEntityDisplayMetadata>(),
            null,
            null,
            null,
            null,
            0,
            null,
            false
        );

        private RoEntityDisplayMetadataTable(
            Dictionary<int, RoEntityDisplayMetadata> entities,
            string jobIdentitySourcePath,
            string jobNameSourcePath,
            string npcIdentitySourcePath,
            string mobDbSourcePath,
            int mobDbCount,
            string loadError,
            bool loadedFromDiskCache = false
        ) {
            this.entities = entities ?? new Dictionary<int, RoEntityDisplayMetadata>();
            JobIdentitySourcePath = jobIdentitySourcePath;
            JobNameSourcePath = jobNameSourcePath;
            NpcIdentitySourcePath = npcIdentitySourcePath;
            MobDbSourcePath = mobDbSourcePath;
            MobDbCount = Math.Max(0, mobDbCount);
            LoadError = loadError;
            LoadedFromDiskCache = loadedFromDiskCache;
        }

        public IReadOnlyDictionary<int, RoEntityDisplayMetadata> Entities => entities;
        public int Count => entities.Count;
        public string JobIdentitySourcePath { get; }
        public string JobNameSourcePath { get; }
        public string NpcIdentitySourcePath { get; }
        public string MobDbSourcePath { get; }
        public int MobDbCount { get; }
        public string LoadError { get; }
        public bool IsLoaded => Count > 0;
        public bool LoadedFromDiskCache { get; }

        public static RoEntityDisplayMetadataTable Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            string error = null;
            string jobIdentityText = TryReadFirstTextTable(
                assets,
                LuaDataInfo("jobidentity"),
                out string jobIdentityPath,
                out byte[] jobIdentityBytes,
                ref error);
            string jobNameText = TryReadFirstTextTable(
                assets,
                LuaDataInfo("jobname", "jobname_f"),
                out string jobNamePath,
                out byte[] jobNameBytes,
                ref error);
            string npcIdentityText = TryReadFirstTextTable(
                assets,
                LuaDataInfo("npcidentity"),
                out string npcIdentityPath,
                out byte[] npcIdentityBytes,
                ref error);
            string mobDbText = TryReadFirstTextTable(
                assets,
                MobDbCandidates(),
                out string mobDbPath,
                out byte[] mobDbBytes,
                ref error);

            if (TryReadEntityDisplayCache(
                    assets,
                    jobIdentityPath,
                    jobIdentityBytes,
                    jobNamePath,
                    jobNameBytes,
                    npcIdentityPath,
                    npcIdentityBytes,
                    mobDbPath,
                    mobDbBytes,
                    out RoEntityDisplayMetadataTable cachedTable)) {
                return cachedTable;
            }

            RoEntityDisplayMetadataTable table = FromText(
                jobIdentityText,
                jobNameText,
                npcIdentityText,
                mobDbText,
                jobIdentityPath,
                jobNamePath,
                npcIdentityPath,
                mobDbPath,
                error);
            TryWriteEntityDisplayCache(
                assets,
                jobIdentityPath,
                jobIdentityBytes,
                jobNamePath,
                jobNameBytes,
                npcIdentityPath,
                npcIdentityBytes,
                mobDbPath,
                mobDbBytes,
                table);
            return table;
        }

        public static RoEntityDisplayMetadataTable FromText(
            string jobIdentityText = null,
            string jobNameText = null,
            string npcIdentityText = null,
            string mobDbText = null,
            string jobIdentitySourcePath = null,
            string jobNameSourcePath = null,
            string npcIdentitySourcePath = null,
            string mobDbSourcePath = null,
            string loadError = null
        ) {
            var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            ParseConstants(jobIdentityText, ids);
            ParseConstants(npcIdentityText, ids);

            var resourceNames = new Dictionary<int, string>();
            ParseIndexedStringTable(jobNameText, ids, resourceNames);

            Dictionary<int, MobDbRecord> mobDbRecords = ParseMobDb(mobDbText);
            var allIds = new HashSet<int>(resourceNames.Keys);
            foreach (int id in mobDbRecords.Keys) {
                allIds.Add(id);
            }

            var entities = new Dictionary<int, RoEntityDisplayMetadata>();
            foreach (int classId in allIds) {
                resourceNames.TryGetValue(classId, out string resourceName);
                // the client picks the folder from the job id alone (CNpc::vf21 0xcc2230), never from the names
                RoEntityDisplayKind kind = Classify(classId);
                if (kind == RoEntityDisplayKind.Unknown) {
                    continue;
                }

                mobDbRecords.TryGetValue(classId, out MobDbRecord mob);
                if (string.IsNullOrWhiteSpace(resourceName)) {
                    resourceName = mob.AegisName;
                }

                if (string.IsNullOrWhiteSpace(resourceName)) {
                    continue;
                }

                entities[classId] = new RoEntityDisplayMetadata(
                    classId,
                    kind,
                    resourceName,
                    mob.Name,
                    mob.AegisName,
                    mob.Level
                );
            }

            return new RoEntityDisplayMetadataTable(
                entities,
                jobIdentitySourcePath,
                jobNameSourcePath,
                npcIdentitySourcePath,
                mobDbSourcePath,
                mobDbRecords.Count,
                loadError,
                false
            );
        }

        public bool TryGetEntity(int classId, out RoEntityDisplayMetadata metadata) {
            return entities.TryGetValue(classId, out metadata);
        }

        public RoEntityDisplayMetadata GetEntityOrFallback(int classId) {
            if (entities.TryGetValue(classId, out RoEntityDisplayMetadata metadata)) {
                return metadata;
            }

            return BuildFallback(classId);
        }

        public bool TryResolveSpriteBasePath(RoAssetDatabase assets, int classId, out string basePath) {
            basePath = null;
            RoEntityDisplayMetadata metadata = GetEntityOrFallback(classId);
            if (metadata == null) {
                return false;
            }

            basePath = metadata.GetSpriteBasePath();
            return ValidateSpritePair(assets, basePath, metadata.Kind + " sprite");
        }

        /// <summary>
        /// The same path checked with <paramref name="exists"/>: like the client, exactly that .spr and .act or nothing
        /// (no base-name, suffix or other-folder fallback).
        /// </summary>
        public bool TryResolveSpriteBasePath(int classId, Func<string, bool> exists, out string basePath) {
            basePath = null;
            RoEntityDisplayMetadata metadata = GetEntityOrFallback(classId);
            if (metadata == null) {
                return false;
            }

            basePath = metadata.GetSpriteBasePath();
            if (string.IsNullOrWhiteSpace(basePath)) {
                return false;
            }

            return exists == null || (exists(basePath + ".spr") && exists(basePath + ".act"));
        }

        public static bool TryGetKnownNpcResourceName(int classId, out string resourceName) {
            return KnownNpcFallbackResourceNames.TryGetValue(classId, out resourceName);
        }

        /// <summary>
        /// The sprite folder kind of a non-player job id, as the client chooses it (<see cref="RoEntitySpriteRules.Classify"/>):
        /// npc 45..999 / 10001..19999, mercenary 6017..6046, homunculus 6001..6052, monster every other id (20373 too);
        /// player jobs (0..30, 4001..6000) are Unknown.
        /// </summary>
        public static RoEntityDisplayKind Classify(int classId) {
            return RoEntitySpriteRules.Classify(classId);
        }

        private RoEntityDisplayMetadata BuildFallback(int classId) {
            RoEntityDisplayKind kind = Classify(classId);
            switch (kind) {
                case RoEntityDisplayKind.Npc:
                    if (TryGetKnownNpcResourceName(classId, out string npcResourceName)) {
                        return new RoEntityDisplayMetadata(classId, RoEntityDisplayKind.Npc, npcResourceName, npcResourceName);
                    }

                    return entities.TryGetValue(46, out RoEntityDisplayMetadata npcFallback)
                        ? npcFallback
                        : new RoEntityDisplayMetadata(46, RoEntityDisplayKind.Npc, "1_ETC_01", "1_ETC_01");
                case RoEntityDisplayKind.Monster:
                    return entities.TryGetValue(1001, out RoEntityDisplayMetadata monsterFallback)
                        ? monsterFallback
                        : new RoEntityDisplayMetadata(1001, RoEntityDisplayKind.Monster, "Scorpion", "Scorpion");
                case RoEntityDisplayKind.Homunculus:
                    return entities.TryGetValue(1002, out RoEntityDisplayMetadata homunFallback)
                        ? new RoEntityDisplayMetadata(classId, RoEntityDisplayKind.Homunculus, homunFallback.ResourceName, homunFallback.DisplayName, homunFallback.AegisName, homunFallback.Level)
                        : new RoEntityDisplayMetadata(classId, RoEntityDisplayKind.Homunculus, "Poring", "Poring");
                default:
                    return null;
            }
        }

        private static bool ValidateSpritePair(RoAssetDatabase assets, string basePath, string expectedType) {
            if (assets == null) {
                return !string.IsNullOrWhiteSpace(basePath);
            }

            bool hasSprite = assets.Exists($"{basePath}.spr");
            bool hasAction = assets.Exists($"{basePath}.act");
            if (hasSprite && hasAction) {
                return true;
            }

            if (!hasSprite) {
                RecordMissing(assets, $"{basePath}.spr", expectedType);
            }

            if (!hasAction) {
                RecordMissing(assets, $"{basePath}.act", expectedType);
            }

            return false;
        }

        private static void RecordMissing(RoAssetDatabase assets, string path, string expectedType) {
            assets.Diagnostics.RecordMissing(path, expectedType, RoAssetPath.GetLookupCandidates(path));
        }

        private static void ParseConstants(string text, Dictionary<string, int> constants, ISet<int> parsedValues = null) {
            if (string.IsNullOrWhiteSpace(text) || constants == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match match in ConstantAssignmentPattern.Matches(stripped)) {
                string key = NormalizeLuaKey(match.Groups["key"].Value);
                if (!string.IsNullOrEmpty(key) && TryParseInteger(match.Groups["value"].Value, out int value)) {
                    constants[key] = value;
                    parsedValues?.Add(value);
                }
            }
        }

        private static void ParseIndexedStringTable(string text, IReadOnlyDictionary<string, int> constants, Dictionary<int, string> output) {
            if (string.IsNullOrWhiteSpace(text) || output == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match match in IndexedStringPattern.Matches(stripped)) {
                if (TryResolveTableKey(match.Groups["key"].Value, constants, out int id)) {
                    output[id] = UnescapeLuaString(match.Groups["value"].Value);
                }
            }

            foreach (Match match in BareStringPattern.Matches(stripped)) {
                if (TryResolveTableKey(match.Groups["key"].Value, constants, out int id)) {
                    output[id] = UnescapeLuaString(match.Groups["value"].Value);
                }
            }
        }

        private static Dictionary<int, MobDbRecord> ParseMobDb(string text) {
            var records = new Dictionary<int, MobDbRecord>();
            if (string.IsNullOrWhiteSpace(text)) {
                return records;
            }

            MobDbRecord current = default;
            bool hasCurrent = false;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                string line = StripYamlComment(lines[i]).Trim();
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }

                if (line.StartsWith("- Id:", StringComparison.Ordinal)) {
                    StoreMobRecord(records, current, hasCurrent);
                    current = new MobDbRecord();
                    hasCurrent = true;
                    current.Id = ReadYamlInt(line.Substring(5));
                    continue;
                }

                if (!hasCurrent) {
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon <= 0) {
                    continue;
                }

                string key = line.Substring(0, colon).Trim();
                string value = CleanYamlScalar(line.Substring(colon + 1));
                switch (key) {
                    case "AegisName":
                        current.AegisName = value;
                        break;
                    case "Name":
                        current.Name = value;
                        break;
                    case "JapaneseName":
                        if (string.IsNullOrEmpty(current.Name)) {
                            current.Name = value;
                        }
                        break;
                    case "Level":
                        current.Level = ReadYamlInt(value);
                        break;
                }
            }

            StoreMobRecord(records, current, hasCurrent);
            return records;
        }

        private static void StoreMobRecord(Dictionary<int, MobDbRecord> records, MobDbRecord record, bool hasRecord) {
            if (hasRecord && record.Id > 0) {
                records[record.Id] = record;
            }
        }

        private static string StripYamlComment(string line) {
            if (string.IsNullOrEmpty(line)) {
                return string.Empty;
            }

            bool inString = false;
            char quote = '\0';
            for (int i = 0; i < line.Length; i++) {
                char current = line[i];
                if (inString) {
                    if (current == quote) {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"' || current == '\'') {
                    inString = true;
                    quote = current;
                    continue;
                }

                if (current == '#') {
                    return line.Substring(0, i);
                }
            }

            return line;
        }

        private static int ReadYamlInt(string value) {
            return TryParseInteger(CleanYamlScalar(value), out int result) ? result : 0;
        }

        private static string CleanYamlScalar(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            value = value.Trim();
            if ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
                (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))) {
                value = value.Substring(1, value.Length - 2);
            }

            return UnescapeLuaString(value);
        }

        private static bool TryResolveTableKey(string rawKey, IReadOnlyDictionary<string, int> constants, out int id) {
            id = 0;
            string key = NormalizeLuaKey(rawKey);
            if (string.IsNullOrEmpty(key)) {
                return false;
            }

            if (TryParseInteger(key, out id)) {
                return true;
            }

            return constants != null && constants.TryGetValue(key, out id);
        }

        private static bool TryParseInteger(string text, out int value) {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) {
                return false;
            }

            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                return int.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string NormalizeLuaKey(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                return null;
            }

            key = key.Trim();
            if ((key.StartsWith("\"", StringComparison.Ordinal) && key.EndsWith("\"", StringComparison.Ordinal)) ||
                (key.StartsWith("'", StringComparison.Ordinal) && key.EndsWith("'", StringComparison.Ordinal))) {
                key = key.Substring(1, key.Length - 2);
            }

            int dotIndex = key.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex + 1 < key.Length) {
                key = key.Substring(dotIndex + 1);
            }

            return key.Trim();
        }

        private static string UnescapeLuaString(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            var chars = new List<char>(value.Length);
            for (int i = 0; i < value.Length; i++) {
                char current = value[i];
                if (current != '\\' || i + 1 >= value.Length) {
                    chars.Add(current);
                    continue;
                }

                char next = value[++i];
                switch (next) {
                    case '\\':
                    case '"':
                    case '\'':
                        chars.Add(next);
                        break;
                    case 'n':
                        chars.Add('\n');
                        break;
                    case 'r':
                        chars.Add('\r');
                        break;
                    case 't':
                        chars.Add('\t');
                        break;
                    case 'x':
                        if (i + 2 < value.Length &&
                            byte.TryParse(value.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte byteValue)) {
                            chars.Add((char) byteValue);
                            i += 2;
                        } else {
                            chars.Add(next);
                        }
                        break;
                    default:
                        if (char.IsDigit(next)) {
                            int start = i;
                            int length = 1;
                            while (length < 3 && i + 1 < value.Length && char.IsDigit(value[i + 1])) {
                                i++;
                                length++;
                            }

                            if (byte.TryParse(value.Substring(start, length), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte decimalByte)) {
                                chars.Add((char) decimalByte);
                            } else {
                                chars.Add(next);
                            }
                        } else {
                            chars.Add(next);
                        }
                        break;
                }
            }

            string unescaped = new string(chars.ToArray());
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(unescaped);
            return string.IsNullOrEmpty(decoded) ? unescaped : decoded;
        }

        private static string StripLineComments(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                int luaComment = lines[i].IndexOf("--", StringComparison.Ordinal);
                int slashComment = lines[i].IndexOf("//", StringComparison.Ordinal);
                int comment = -1;
                if (luaComment >= 0 && slashComment >= 0) {
                    comment = Math.Min(luaComment, slashComment);
                } else if (luaComment >= 0) {
                    comment = luaComment;
                } else if (slashComment >= 0) {
                    comment = slashComment;
                }

                if (comment >= 0) {
                    lines[i] = lines[i].Substring(0, comment);
                }
            }

            return string.Join("\n", lines);
        }

        private static string TryReadFirstTextTable(
            RoAssetDatabase assets,
            IReadOnlyList<string> candidatePaths,
            out string sourcePath,
            out byte[] sourceBytes,
            ref string loadError
        ) {
            sourcePath = null;
            sourceBytes = null;
            string pendingError = null;
            if (assets == null || candidatePaths == null) {
                return null;
            }

            for (int i = 0; i < candidatePaths.Count; i++) {
                string candidate = candidatePaths[i];
                if (!assets.Exists(candidate)) {
                    continue;
                }

                byte[] bytes;
                try {
                    bytes = assets.ReadBytes(candidate);
                } catch (Exception ex) {
                    pendingError = ex.Message;
                    continue;
                }

                if (bytes == null || bytes.Length == 0) {
                    continue;
                }

                byte[] text = bytes;
                if (bytes[0] == 0x1B) {
                    text = RoLuaBytecodeSource.ToSource(assets, candidate, bytes, out string luaError);
                    if (text == null) {
                        pendingError = luaError;
                        continue;
                    }
                }

                sourcePath = candidate;
                sourceBytes = bytes;
                return RoTextEncoding.Decode(text);
            }

            if (!string.IsNullOrEmpty(pendingError)) {
                loadError = pendingError;
            }

            return null;
        }

        private static bool TryReadEntityDisplayCache(
            RoAssetDatabase assets,
            string jobIdentitySourcePath,
            byte[] jobIdentityBytes,
            string jobNameSourcePath,
            byte[] jobNameBytes,
            string npcIdentitySourcePath,
            byte[] npcIdentityBytes,
            string mobDbSourcePath,
            byte[] mobDbBytes,
            out RoEntityDisplayMetadataTable table
        ) {
            table = null;
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null || !HasAnySourceBytes(jobIdentityBytes, jobNameBytes, npcIdentityBytes, mobDbBytes)) {
                return false;
            }

            string cacheKey = BuildEntityDisplayCacheKey(
                jobIdentitySourcePath,
                jobIdentityBytes,
                jobNameSourcePath,
                jobNameBytes,
                npcIdentitySourcePath,
                npcIdentityBytes,
                mobDbSourcePath,
                mobDbBytes);
            try {
                if (!cache.TryReadBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", out byte[] data)) {
                    return false;
                }

                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadInt32() != EntityDisplayCacheMagic ||
                    reader.ReadInt32() != EntityDisplayCacheVersion) {
                    return false;
                }

                string cachedJobIdentitySourcePath = reader.ReadString();
                string cachedJobNameSourcePath = reader.ReadString();
                string cachedNpcIdentitySourcePath = reader.ReadString();
                string cachedMobDbSourcePath = reader.ReadString();
                if (!IsSameSourcePath(cachedJobIdentitySourcePath, jobIdentitySourcePath) ||
                    !IsSameSourcePath(cachedJobNameSourcePath, jobNameSourcePath) ||
                    !IsSameSourcePath(cachedNpcIdentitySourcePath, npcIdentitySourcePath) ||
                    !IsSameSourcePath(cachedMobDbSourcePath, mobDbSourcePath)) {
                    return false;
                }

                int mobDbCount = reader.ReadInt32();
                int entityCount = reader.ReadInt32();
                if (mobDbCount < 0 || entityCount < 0 || entityCount > MaxCachedEntities) {
                    return false;
                }

                var entities = new Dictionary<int, RoEntityDisplayMetadata>(entityCount);
                for (int i = 0; i < entityCount; i++) {
                    RoEntityDisplayMetadata entity = ReadEntityDisplayCacheEntry(reader);
                    entities[entity.ClassId] = entity;
                }

                table = new RoEntityDisplayMetadataTable(
                    entities,
                    jobIdentitySourcePath,
                    jobNameSourcePath,
                    npcIdentitySourcePath,
                    mobDbSourcePath,
                    mobDbCount,
                    null,
                    true);
                return true;
            } catch {
                table = null;
                return false;
            }
        }

        private static void TryWriteEntityDisplayCache(
            RoAssetDatabase assets,
            string jobIdentitySourcePath,
            byte[] jobIdentityBytes,
            string jobNameSourcePath,
            byte[] jobNameBytes,
            string npcIdentitySourcePath,
            byte[] npcIdentityBytes,
            string mobDbSourcePath,
            byte[] mobDbBytes,
            RoEntityDisplayMetadataTable table
        ) {
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null ||
                table == null ||
                table.Count == 0 ||
                !HasAnySourceBytes(jobIdentityBytes, jobNameBytes, npcIdentityBytes, mobDbBytes)) {
                return;
            }

            string cacheKey = BuildEntityDisplayCacheKey(
                jobIdentitySourcePath,
                jobIdentityBytes,
                jobNameSourcePath,
                jobNameBytes,
                npcIdentitySourcePath,
                npcIdentityBytes,
                mobDbSourcePath,
                mobDbBytes);
            try {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                    writer.Write(EntityDisplayCacheMagic);
                    writer.Write(EntityDisplayCacheVersion);
                    writer.Write(jobIdentitySourcePath ?? string.Empty);
                    writer.Write(jobNameSourcePath ?? string.Empty);
                    writer.Write(npcIdentitySourcePath ?? string.Empty);
                    writer.Write(mobDbSourcePath ?? string.Empty);
                    writer.Write(table.MobDbCount);
                    writer.Write(table.entities.Count);
                    foreach (RoEntityDisplayMetadata entity in table.entities.Values) {
                        WriteEntityDisplayCacheEntry(writer, entity);
                    }
                }

                cache.WriteBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", stream.ToArray());
            } catch {
                // Parsed entity display cache is an optimization only; source data remains authoritative.
            }
        }

        private static RoEntityDisplayMetadata ReadEntityDisplayCacheEntry(BinaryReader reader) {
            return new RoEntityDisplayMetadata(
                reader.ReadInt32(),
                (RoEntityDisplayKind) reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadInt32());
        }

        private static void WriteEntityDisplayCacheEntry(BinaryWriter writer, RoEntityDisplayMetadata entity) {
            writer.Write(entity.ClassId);
            writer.Write((int) entity.Kind);
            // the constructor derives ResourceName (no folder) from the sprite name
            writer.Write(entity.SpriteName ?? string.Empty);
            writer.Write(entity.DisplayName ?? string.Empty);
            writer.Write(entity.AegisName ?? string.Empty);
            writer.Write(entity.Level);
        }

        private static string BuildEntityDisplayCacheKey(
            string jobIdentitySourcePath,
            byte[] jobIdentityBytes,
            string jobNameSourcePath,
            byte[] jobNameBytes,
            string npcIdentitySourcePath,
            byte[] npcIdentityBytes,
            string mobDbSourcePath,
            byte[] mobDbBytes
        ) {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                WriteSourceBytesForCacheKey(writer, jobIdentitySourcePath, jobIdentityBytes);
                WriteSourceBytesForCacheKey(writer, jobNameSourcePath, jobNameBytes);
                WriteSourceBytesForCacheKey(writer, npcIdentitySourcePath, npcIdentityBytes);
                WriteSourceBytesForCacheKey(writer, mobDbSourcePath, mobDbBytes);
            }

            string sourcePathKey = string.Join(
                "|",
                jobIdentitySourcePath ?? string.Empty,
                jobNameSourcePath ?? string.Empty,
                npcIdentitySourcePath ?? string.Empty,
                mobDbSourcePath ?? string.Empty);
            return RoItemMetadataTable.BuildParsedSourceCacheKey("entity-display", sourcePathKey, stream.ToArray());
        }

        private static void WriteSourceBytesForCacheKey(BinaryWriter writer, string sourcePath, byte[] sourceBytes) {
            writer.Write(sourcePath ?? string.Empty);
            if (sourceBytes == null) {
                writer.Write(-1);
                return;
            }

            writer.Write(sourceBytes.Length);
            writer.Write(sourceBytes);
        }

        private static bool HasAnySourceBytes(params byte[][] sources) {
            for (int i = 0; i < sources.Length; i++) {
                if (sources[i] != null && sources[i].Length > 0) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameSourcePath(string cachedPath, string sourcePath) {
            return string.Equals(cachedPath ?? string.Empty, sourcePath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string[] LuaDataInfo(params string[] names) {
            var paths = new List<string>();
            for (int i = 0; i < names.Length; i++) {
                AddLuaPathVariants(paths, "data/luafiles514/lua files/datainfo", names[i]);
                AddLuaPathVariants(paths, "data/lua files/datainfo", names[i]);
            }

            return paths.ToArray();
        }

        private static void AddLuaPathVariants(List<string> paths, string root, string name) {
            paths.Add(root + "/" + name + ".lub");
            paths.Add(root + "/" + name + ".lua");
        }

        private static string[] MobDbCandidates() {
            return new[] {
                "db/re/mob_db.yml",
                "db/re/mob_db.yaml",
                "db/pre-re/mob_db.yml",
                "db/pre-re/mob_db.yaml",
                "db/mob_db.yml",
                "db/mob_db.yaml",
                "data/db/re/mob_db.yml",
                "data/db/re/mob_db.yaml",
                "data/db/mob_db.yml",
                "data/db/mob_db.yaml",
                "mob_db.yml",
                "mob_db.yaml",
            };
        }

        private struct MobDbRecord {
            public int Id;
            public string AegisName;
            public string Name;
            public int Level;
        }
    }
}
