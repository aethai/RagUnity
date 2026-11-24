using System.Collections.Generic;

namespace Ragnarok.UI {
    public sealed partial class RoClassicStatusIcons {
        /// <summary>
        /// 0xd16760's hard-coded fallback when GetEFSTImgFileName has no file: (efst, priority, file under effect), every
        /// arm of the priority switch (the 0x123..0x243 part of priority 2 through the byte table at 0xd18d04), read by
        /// running the exe's code. Each EFST has one priority. Blessing (10) and Increase AGI (12) are WHITE.
        /// </summary>
        private static readonly (int Efst, int Priority, string File)[] FallbackImages = {
            // priority 0
            (762, 0, "SWORDCLAN.TGA"), (763, 0, "ARCWANDCLAN.TGA"), (764, 0, "GOLDENMACECLAN.TGA"), (765, 0, "CROSSBOWCLAN.TGA"),
            // priority 1
            (8, 1, "콰그마이어.tga"), (13, 1, "민첩성감소.tga"), (32, 1, "갑옷파괴.tga"), (33, 1, "무기파괴.tga"),
            (34, 1, "환각.tga"), (35, 1, "무게50이상.tga"), (36, 1, "무게90이상.tga"), (50, 1, "스트립웨폰.tga"),
            (51, 1, "스트립쉴드.tga"), (52, 1, "스트립아머.tga"), (53, 1, "스트립헬름.tga"), (90, 1, "프레임런쳐.tga"),
            (241, 1, "Str_gogi.tga"), (242, 1, "Agi_gogi.tga"), (243, 1, "Vit_gogi.tga"), (244, 1, "Dex_gogi.tga"),
            (245, 1, "Int_gogi.tga"), (246, 1, "Luk_gogi.tga"), (247, 1, "gogi.tga"), (248, 1, "gogi.tga"),
            (249, 1, "gogi.tga"), (250, 1, "exp.tga"), (251, 1, "death.tga"), (252, 1, "item.tga"),
            (271, 1, "Str_gogi.tga"), (272, 1, "Agi_gogi.tga"), (273, 1, "Vit_gogi.tga"), (274, 1, "Dex_gogi.tga"),
            (275, 1, "Int_gogi.tga"), (276, 1, "Luk_gogi.tga"), (298, 1, "멘탈포션.tga"), (312, 1, "job.tga"),
            (313, 1, "gogi.tga"), (401, 1, "민첩성감소.tga"), (476, 1, "npc_hellpower.tga"), (491, 1, "Str_gogi.tga"),
            (492, 1, "Int_gogi.tga"), (493, 1, "Vit_gogi.tga"), (494, 1, "Dex_gogi.tga"), (495, 1, "Agi_gogi.tga"),
            (496, 1, "Luk_gogi.tga"), (621, 1, "변신.tga"), (636, 1, "job.tga"), (637, 1, "NORECOVER.TGA"),
            (662, 1, "icon02.tga"), (663, 1, "icon02.tga"), (664, 1, "rwcscroll.tga"), (721, 1, "REBOUND.tga"),
            (730, 1, "darkcrow.tga"),
            // priority 2
            (0, 2, "프로보크.tga"), (1, 2, "인듀어.tga"), (2, 2, "투핸드퀴큰.tga"), (4, 2, "하이딩.tga"),
            (5, 2, "클로킹.tga"), (6, 2, "인찬트포이즌.tga"), (7, 2, "포이즌리액트.tga"), (23, 2, "아드레날린러쉬.tga"),
            (24, 2, "웨폰퍼펙션.tga"), (25, 2, "오버트러스트.tga"), (26, 2, "맥시마이즈파워.tga"), (58, 2, "오토가드.tga"),
            (59, 2, "리플렉트쉴드.tga"), (62, 2, "디펜더.tga"), (65, 2, "오토스펠.tga"), (68, 2, "스피어퀴큰.tga"),
            (91, 2, "프로스트웨폰.tga"), (92, 2, "라이트닝로더.tga"), (97, 2, "글로리아.tga"), (104, 2, "ICON10.tga"),
            (106, 2, "ICON08.tga"), (110, 2, "ICON05.tga"), (112, 2, "I_장판.tga"), (114, 2, "I_치독.tga"),
            (115, 2, "ICON09.tga"), (116, 2, "ICON06.tga"), (117, 2, "ICON03.tga"), (118, 2, "ICON07.tga"),
            (120, 2, "ICON04.tga"), (121, 2, "ICON01.tga"), (122, 2, "ICON01.tga"), (124, 2, "출혈상태.tga"),
            (125, 2, "관절공격.tga"), (130, 2, "엄마아빠사랑해요.tga"), (132, 2, "금강불괴.tga"), (135, 2, "i_STORMKICK.tga"),
            (137, 2, "i_DOWNKICK.tga"), (139, 2, "i_TURNKICK.tga"), (141, 2, "i_COUNTER.tga"), (143, 2, "i_DODGE.tga"),
            (145, 2, "i_RUN.tga"), (146, 2, "i_p_DARK.tga"), (147, 2, "아드레날린러쉬.tga"), (148, 2, "i_p_TELE.tga"),
            (150, 2, "PLUSATTACKPOWER.tga"), (156, 2, "i_KAIZEL.tga"), (157, 2, "i_KAAHI.tga"), (158, 2, "i_KAUPE.tga"),
            (161, 2, "i_ONEHAND.tga"), (169, 2, "i_SUNCOMFORT.tga"), (170, 2, "i_MOONCOMFORT.tga"), (171, 2, "i_STARCOMFORT.tga"),
            (181, 2, "I_프리저브.tga"), (182, 2, "I_체이스힘.tga"), (186, 2, "I_더블캐스팅.tga"), (188, 2, "I_오버맥스.tga"),
            (191, 2, "I_타로카드.tga"), (197, 2, "i_shrink.tga"), (198, 2, "i_sightblaster.tga"), (200, 2, "i_closeconfine.tga"),
            (201, 2, "i_closeconfine.tga"), (203, 2, "i_MADNESS.tga"), (204, 2, "i_FEVER.tga"), (205, 2, "글로리아.tga"),
            (206, 2, "i_MAEMI.tga"), (207, 2, "i_BUNSIN.tga"), (208, 2, "i_NEN.tga"), (209, 2, "i_ADJUSTMENT.tga"),
            (210, 2, "i_ACCURACY.tga"), (212, 2, "글로리아.tga"), (213, 2, "글로리아.tga"), (214, 2, "글로리아.tga"),
            (215, 2, "글로리아.tga"), (216, 2, "글로리아.tga"), (217, 2, "글로리아.tga"), (218, 2, "글로리아.tga"),
            (219, 2, "글로리아.tga"), (220, 2, "글로리아.tga"), (221, 2, "글로리아.tga"), (222, 2, "글로리아.tga"),
            (223, 2, "글로리아.tga"), (224, 2, "글로리아.tga"), (225, 2, "글로리아.tga"), (226, 2, "글로리아.tga"),
            (227, 2, "글로리아.tga"), (228, 2, "글로리아.tga"), (229, 2, "글로리아.tga"), (230, 2, "글로리아.tga"),
            (231, 2, "글로리아.tga"), (232, 2, "글로리아.tga"), (233, 2, "글로리아.tga"), (234, 2, "글로리아.tga"),
            (235, 2, "글로리아.tga"), (236, 2, "글로리아.tga"), (239, 2, "글로리아.tga"), (240, 2, "글로리아.tga"),
            (254, 2, "글로리아.tga"), (255, 2, "글로리아.tga"), (256, 2, "글로리아.tga"), (257, 2, "글로리아.tga"),
            (258, 2, "글로리아.tga"), (259, 2, "글로리아.tga"), (260, 2, "글로리아.tga"), (261, 2, "글로리아.tga"),
            (262, 2, "글로리아.tga"), (263, 2, "죽은척하기.tga"), (264, 2, "글로리아.tga"), (265, 2, "글로리아.tga"),
            (266, 2, "글로리아.tga"), (267, 2, "글로리아.tga"), (268, 2, "글로리아.tga"), (269, 2, "글로리아.tga"),
            (270, 2, "글로리아.tga"), (282, 2, "slowcast.tga"), (286, 2, "criticalwound.tga"), (290, 2, "물리방어포션.tga"),
            (291, 2, "마법방어포션.tga"), (292, 2, "회복력향상포션.tga"), (293, 2, "소형생명수.tga"), (294, 2, "중형생명수.tga"),
            (295, 2, "연마제.tga"), (296, 2, "환영의술잔.tga"), (297, 2, "대환단.tga"), (299, 2, "태청단.tga"),
            (300, 2, "SP소모량감소포션.tga"), (301, 2, "올상태저항포션.tga"), (302, 2, "캐시갑옷속성부여.tga"), (304, 2, "npc_hellpower.tga"),
            (314, 2, "엄마아빠사랑해요.tga"), (316, 2, "RK_ENCHANT.tga"), (318, 2, "RK_NAUTHIZ.tga"), (319, 2, "RK_TURISUSS.tga"),
            (320, 2, "RK_HAGALAZ.tga"), (321, 2, "RK_ISHA.tga"), (322, 2, "RK_EISIR.tga"), (323, 2, "RK_URUZ.tga"),
            (333, 2, "포이즈닝웨폰.tga"), (334, 2, "할루시네이션워크.tga"), (336, 2, "레노바티오.tga"), (337, 2, "웨폰블로킹.tga"),
            (341, 2, "감각마비독.tga"), (342, 2, "신경마비독.tga"), (343, 2, "감각마비독.tga"), (344, 2, "쇠약독.tga"),
            (345, 2, "웃음버섯.tga"), (346, 2, "상처오염독.tga"), (347, 2, "열병독.tga"), (348, 2, "망각의재.tga"),
            (349, 2, "거머리.tga"), (351, 2, "FROSTMISTY.tga"), (352, 2, "RA_FEARBREEZE.tga"), (354, 2, "MARSHOFABYSS.tga"),
            (355, 2, "RECOGNIZEDSPELL.tga"), (361, 2, "엑셀레이션.tga"), (362, 2, "호버링.tga"), (368, 2, "MVPCARD_TAOGUNKA.tga"),
            (369, 2, "MVPCARD_MISTRESS.tga"), (370, 2, "MVPCARD_ORCHERO.tga"), (371, 2, "MVPCARD_ORCLORD.tga"), (373, 2, "오버히트.tga"),
            (374, 2, "셰이프쉬프트.tga"), (375, 2, "인프라레드스캔.tga"), (377, 2, "뉴트럴배리어.tga"), (379, 2, "스텔스필드.tga"),
            (381, 2, "EFST_ATK.tga"), (382, 2, "EFST_DEF.tga"), (383, 2, "EFST_ATK.tga"), (384, 2, "EFST_DEF.tga"),
            (385, 2, "리프로듀스.tga"), (386, 2, "EFST_MATK.tga"), (387, 2, "EFST_MATK.tga"), (388, 2, "str_gogi.tga"),
            (389, 2, "int_gogi.tga"), (390, 2, "LG_REFLECTDAMAGE.tga"), (391, 2, "포스오브뱅가드.tga"), (393, 2, "오토쉐도우스펠.tga"),
            (394, 2, "쉐도우폼.tga"), (396, 2, "LG_SHIELDSPELL_방.tga"), (397, 2, "LG_SHIELDSPELL_마.tga"), (398, 2, "LG_SHIELDSPELL_연.tga"),
            (399, 2, "바디페인팅.tga"), (402, 2, "LG_PRESTIGE.tga"), (403, 2, "인비지빌리티.tga"), (404, 2, "데들리인펙트.tga"),
            (405, 2, "LG_BANDING.tga"), (407, 2, "LG_INSPIRATION.tga"), (408, 2, "Masquerade_ENERVATION.tga"), (409, 2, "Masquerade_GROOMY.tga"),
            (410, 2, "잠룡승천.tga"), (411, 2, "Masquerade_IGNORANCE.tga"), (412, 2, "Masquerade_LAZINESS.tga"), (413, 2, "섬전보.tga"),
            (414, 2, "i_ONEHAND.tga"), (415, 2, "Masquerade_UNLUCKY.tga"), (418, 2, "Masquerade_WEAKNESS.tga"), (421, 2, "스트립액세서리.tga"),
            (423, 2, "ICON02.tga"), (425, 2, "점혈구.tga"), (426, 2, "점혈반.tga"), (427, 2, "점혈활.tga"),
            (429, 2, "스윙댄스.tga"), (430, 2, "연인들을위한심포니.tga"), (435, 2, "안식의자장가.tga"), (436, 2, "순환하는자연의소리.tga"),
            (437, 2, "다이아몬드더스트.tga"), (438, 2, "수줍은하루의우울.tga"), (439, 2, "마나의노래.tga"), (440, 2, "클라우드킬.tga"),
            (441, 2, "워그와함께춤을.tga"), (442, 2, "풍차를향해돌격.tga"), (443, 2, "메아리의노래.tga"), (444, 2, "하모나이즈.tga"),
            (445, 2, "스트라이킹.tga"), (446, 2, "워머.tga"), (447, 2, "달빛의세레나데.tga"), (448, 2, "새터데이나이트피버.tga"),
            (450, 2, "애널라이즈.tga"), (451, 2, "레라드의이슬.tga"), (452, 2, "멜로디오브싱크.tga"), (453, 2, "비욘드오브워크라이.tga"),
            (454, 2, "언리미티드허밍보이스.tga"), (461, 2, "카트부스트.tga"), (463, 2, "가시나무덫.tga"), (472, 2, "세크라멘트.tga"),
            (473, 2, "ICON05.tga"), (573, 2, "I_타로카드.tga"), (578, 2, "중형생명수.tga"), (579, 2, "ICON02.tga"),
            (583, 2, "all_odins_power.tga"), (611, 2, "EFST_MATK.tga"), (622, 2, "앉기.tga"), (632, 2, "ICON08.tga"),
            (633, 2, "ICON08.tga"), (634, 2, "ICON08.tga"), (635, 2, "ICON08.tga"), (638, 2, "SETDEF.TGA"),
            (640, 2, "SETDEF.TGA"), (670, 2, "소형생명수.tga"), (695, 2, "ArrowN.tga"), (697, 2, "EFST_ATK.tga"),
            (698, 2, "EFST_MATK.tga"), (699, 2, "EFST_DEF.tga"), (700, 2, "EXP.tga"), (869, 2, "할루시네이션워크.tga"),
            (1121, 2, "ICON05.tga"),
            // priority 3
            (3, 3, "집중력향상.tga"), (27, 3, "라이딩.tga"), (28, 3, "팔콘.tga"), (29, 3, "죽은척하기.tga"),
            (30, 3, "고성지르기.tga"), (31, 3, "에너지코트.tga"), (37, 3, "공속물약.tga"), (38, 3, "공속물약.tga"),
            (39, 3, "공속물약.tga"), (40, 3, "공속물약.tga"), (54, 3, "케미컬프로텍션[웨폰].tga"), (55, 3, "케미컬프로텍션[쉴드].tga"),
            (56, 3, "케미컬프로텍션[아머].tga"), (57, 3, "케미컬프로텍션[헬름].tga"), (93, 3, "사이즈믹웨폰.tga"), (151, 3, "PLUSMAGICPOWER.tga"),
            (357, 3, "wolfmount.tga"), (613, 3, "라이딩.tga"), (639, 3, "SETMDEF.TGA"), (641, 3, "SETMDEF.TGA"),
            (647, 3, "ASPDCASH.TGA"), (650, 3, "ELDICASTES.TGA"),
            // priority 4
            (9, 4, "안젤루스.tga"), (10, 4, "블레싱.tga"), (11, 4, "시그넘크루시스.tga"), (12, 4, "민첩성증가.tga"),
            (14, 4, "슬로우포이즌.tga"), (15, 4, "임포시티오마누스.tga"), (16, 4, "수프라기움.tga"), (17, 4, "아스페르시오.tga"),
            (18, 4, "성체강복.tga"), (19, 4, "기리에엘레이손.tga"), (20, 4, "마니피캇.tga"), (21, 4, "글로리아.tga"),
            (22, 4, "렉스에테르나.tga"), (41, 4, "민첩성증가.tga"), (42, 4, "민첩성증가.tga"), (61, 4, "신의뜻.tga"),
            (64, 4, "아스페르시오.tga"), (87, 4, "STEELBODY.tga"), (289, 4, "민첩성증가.tga"), (577, 4, "세라4.tga"),
            (580, 4, "에이라1.tga"), (581, 4, "에이라2.tga"), (584, 4, "에레노아1_1.tga"), (605, 4, "디이터1.tga"),
            (606, 4, "디이터2.tga"), (607, 4, "디이터4.tga"), (608, 4, "디이터5.tga"), (618, 4, "exp_말랑도_고양이캔.tga"),
            (658, 4, "PHIDEMON.tga"), (715, 4, "FRIGG_SONG.tga"), (716, 4, "OFFERTORIUM.tga"), (717, 4, "TELEKINESIS_INTENSE.tga"),
            (720, 4, "FULL_THROTTLE.tga"), (722, 4, "UNLIMIT.tga"), (723, 4, "KINGS_GRACE.tga"),
        };

        /// <summary>
        /// The arms that depend on the job (FUN_00cea6a0): Flame Launcher 90, Frost Weapon 91, Lightning Loader 92, Seismic
        /// Weapon 93 and Aspersio 17 / 64 show the i_p_* element icons for the Taekwon line.
        /// </summary>
        private static string FallbackForJob(int efst, int job) {
            bool taekwon = (job >= 0xfce && job <= 0xfd1) || (job >= 0x1081 && job <= 0x1083) || (job >= 0x108e && job <= 0x1094) ||
                job == 0x10ce || job == 0x10cf || job == 0x10dc;
            if (!taekwon) return null;
            switch (efst) {
                case 90: return "i_p_FIRE.tga";
                case 91: return "i_p_WATER.tga";
                case 92: return "i_p_WIND.tga";
                case 93: return "i_p_EARTH.tga";
                case 17:
                case 64: return "i_p_SAINT.tga";
                default: return null;
            }
        }

        private static Dictionary<int, KeyValuePair<int, string>> fallbackByEfst;

        private static bool TryFallbackImage(int efst, out int priority, out string file) {
            if (fallbackByEfst == null) {
                var map = new Dictionary<int, KeyValuePair<int, string>>(FallbackImages.Length);
                foreach ((int e, int p, string f) in FallbackImages) map[e] = new KeyValuePair<int, string>(p, f);
                fallbackByEfst = map;
            }

            if (fallbackByEfst.TryGetValue(efst, out KeyValuePair<int, string> hit)) {
                priority = hit.Key;
                file = hit.Value;
                return true;
            }

            priority = -1;
            file = null;
            return false;
        }
    }
}
