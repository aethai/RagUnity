using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    public readonly struct RoMapRentalJobResolution {
        public RoMapRentalJobResolution(short baseJob, short costumeJob, short visualJob) {
            BaseJob = baseJob;
            CostumeJob = costumeJob;
            VisualJob = visualJob;
        }

        public short BaseJob { get; }
        public short CostumeJob { get; }
        public short VisualJob { get; }
    }

    public readonly struct RoMapRentalAttachmentState {
        public RoMapRentalAttachmentState(bool hasFalcon, bool hasWug) {
            HasFalcon = hasFalcon;
            HasWug = hasWug;
        }

        public bool HasFalcon { get; }
        public bool HasWug { get; }
    }

    public static class RoMapRentalVisualSemantics {
        public const int EffectStateCart1 = 0x00000008;
        public const int EffectStateFalcon = 0x00000010;
        public const int EffectStateRiding = 0x00000020;
        public const int EffectStateCart2 = 0x00000080;
        public const int EffectStateCart3 = 0x00000100;
        public const int EffectStateCart4 = 0x00000200;
        public const int EffectStateCart5 = 0x00000400;
        public const int EffectStateWedding = 0x00001000;
        public const int EffectStateXmas = 0x00010000;
        public const int EffectStateSummer = 0x00040000;
        public const int EffectStateDragon1 = 0x00080000;
        public const int EffectStateWug = 0x00100000;
        public const int EffectStateWugRider = 0x00200000;
        public const int EffectStateMadogear = 0x00400000;
        public const int EffectStateDragon2 = 0x00800000;
        public const int EffectStateDragon3 = 0x01000000;
        public const int EffectStateDragon4 = 0x02000000;
        public const int EffectStateDragon5 = 0x04000000;

        public const ushort StatusRiding = 27;
        public const ushort StatusFalcon = 28;
        public const ushort StatusWugRider = 357;
        public const ushort StatusAllRiding = 613;
        public const ushort StatusOnPushCart = 673;

        public const ushort SkillHunterBlitzBeat = 129;
        public const ushort SkillHunterDetecting = 130;
        public const ushort SkillSniperFalconAssault = 381;
        public const ushort SkillRangerWugStrike = 2243;
        public const ushort SkillRangerWugBite = 2244;

        public const float FalconAltitudeOffset = 5f;
        public const int FalconRenderOrderOffset = 1000;
        /// <summary>
        /// How fast the falcon closes on its hunter, per second (exponential follow, applied every frame). Measured live
        /// against the 2024 client (28.11): it leaves with the hunter at once, trails about one cell behind a walking hunter
        /// and settles over a stopped one within ~0.3 s; 6/s gives a 1.1-cell trail at 150 ms per cell.
        /// </summary>
        public const float FalconFollowRate = 6f;
        public const float FalconDashSpeedMilliseconds = 35f;
        public const float FalconDashActionSeconds = 0.432f;

        private const short JobWedding = 22;
        private const short JobXmas = 26;
        private const short JobSummer = 27;

        private const int RidingEffectMask =
            EffectStateRiding |
            EffectStateDragon1 |
            EffectStateDragon2 |
            EffectStateDragon3 |
            EffectStateDragon4 |
            EffectStateDragon5 |
            EffectStateWugRider |
            EffectStateMadogear;

        private const int AllRidingBlockingEffectMask =
            RidingEffectMask |
            EffectStateWedding |
            EffectStateXmas |
            EffectStateSummer;

        public const int CartEffectMask =
            EffectStateCart1 |
            EffectStateCart2 |
            EffectStateCart3 |
            EffectStateCart4 |
            EffectStateCart5;

        private const int EquipmentRemoveOptionEffectMask =
            EffectStateFalcon |
            EffectStateRiding |
            EffectStateDragon1 |
            EffectStateDragon2 |
            EffectStateDragon3 |
            EffectStateDragon4 |
            EffectStateDragon5 |
            EffectStateMadogear |
            CartEffectMask;

        private static readonly (short BaseJob, short MountJob)[] MountPairs = {
            (7, 13),
            (4008, 4014),
            (4030, 4036),
            (14, 21),
            (4015, 4022),
            (4037, 4044),
            (4054, 4080),
            (4060, 4081),
            (4096, 4109),
            (4332, 4345),
            (4066, 4082),
            (4073, 4083),
            (4102, 4110),
            (4338, 4348),
            (4056, 4084),
            (4062, 4085),
            (4098, 4111),
            (4337, 4346),
            (4058, 4086),
            (4064, 4087),
            (4100, 4112),
            (4333, 4347),
            (4257, 4278),
            (4253, 4279),
            (4252, 4280),
            (4258, 4281)
        };

        private static readonly (short BaseJob, short MountJob)[] AllMountPairs = {
            (0, 4124),
            (4, 4126),
            (3, 4122),
            (2, 4130),
            (5, 4119),
            (1, 4116),
            (6, 4141),
            (8, 4156),
            (11, 4154),
            (9, 4134),
            (10, 4138),
            (7, 4199),
            (12, 4145),
            (15, 4125),
            (19, 4149),
            (20, 4147),
            (16, 4131),
            (18, 4137),
            (14, 4203),
            (17, 4142),
            (4057, 4129),
            (4056, 4198),
            (4055, 4133),
            (4058, 4197),
            (4054, 4202),
            (4059, 4140),
            (4070, 4127),
            (4068, 4148),
            (4069, 4151),
            (4067, 4132),
            (4071, 4120),
            (4066, 4201),
            (4072, 4143),
            (23, 4128),
            (25, 4114),
            (24, 4115),
            (4046, 4155),
            (4047, 4123),
            (4049, 4117),
            (4190, 4192),
            (4211, 4213),
            (4212, 4214),
            (4215, 4216),
            (4240, 4245),
            (4239, 4243),
            (4252, 4265),
            (4253, 4266),
            (4254, 4267),
            (4255, 4268),
            (4256, 4269),
            (4257, 4270),
            (4258, 4271),
            (4259, 4272),
            (4260, 4273),
            (4261, 4274),
            (4262, 4275),
            (4263, 4276),
            (4264, 4277),
            (4351, 4358),
            (4355, 4360),
            (4353, 4359),
            (4001, 4183),
            (4005, 4187),
            (4004, 4186),
            (4003, 4185),
            (4006, 4188),
            (4002, 4184),
            (4007, 4189),
            (4009, 4157),
            (4012, 4150),
            (4010, 4136),
            (4011, 4118),
            (4008, 4200),
            (4013, 4146),
            (4016, 4139),
            (4020, 4153),
            (4021, 4152),
            (4017, 4135),
            (4019, 4121),
            (4015, 4204),
            (4018, 4144),
            (4023, 4158),
            (4027, 4162),
            (4026, 4161),
            (4025, 4160),
            (4028, 4163),
            (4024, 4159),
            (4029, 4181),
            (4031, 4194),
            (4034, 4164),
            (4032, 4195),
            (4033, 4196),
            (4030, 4207),
            (4035, 4165),
            (4038, 4166),
            (4042, 4170),
            (4043, 4171),
            (4039, 4167),
            (4041, 4169),
            (4037, 4210),
            (4040, 4168),
            (4099, 4174),
            (4098, 4206),
            (4097, 4173),
            (4100, 4205),
            (4096, 4209),
            (4101, 4175),
            (4106, 4179),
            (4104, 4177),
            (4105, 4178),
            (4103, 4176),
            (4107, 4180),
            (4102, 4208),
            (4108, 4182),
            (4045, 4172),
            (4191, 4193),
            (4242, 4246),
            (4241, 4244),
            (4063, 4129),
            (4062, 4198),
            (4061, 4133),
            (4064, 4197),
            (4060, 4202),
            (4065, 4140),
            (4077, 4127),
            (4075, 4148),
            (4076, 4151),
            (4074, 4132),
            (4078, 4120),
            (4073, 4201),
            (4079, 4143),
            (4336, 4129),
            (4337, 4198),
            (4335, 4133),
            (4333, 4197),
            (4332, 4202),
            (4334, 4140),
            (4342, 4127),
            (4343, 4148),
            (4344, 4151),
            (4341, 4132),
            (4339, 4120),
            (4348, 4201),
            (4340, 4143)
        };

        private static readonly Dictionary<short, short> MountByBaseJob = BuildMap(MountPairs);
        private static readonly Dictionary<short, short> AllMountByBaseJob = BuildMap(AllMountPairs);
        private static readonly Dictionary<short, short> BaseJobByMountJob = BuildReverseMap(MountPairs);
        private static readonly Dictionary<short, short> BaseJobByAllMountJob = BuildReverseMap(AllMountPairs);
        private static readonly HashSet<short> FalconVisualJobs = new HashSet<short> {
            11,
            4012,
            4034,
            4056,
            4062,
            4098,
            4257,
            4270,
            4278
        };

        public static RoMapRentalJobResolution NormalizeServerJob(short rawJob, int effectState = 0, bool allRidingState = false) {
            short baseJob = rawJob;
            short costumeJob = 0;
            if (BaseJobByMountJob.TryGetValue(rawJob, out short classicBaseJob)) {
                baseJob = classicBaseJob;
                costumeJob = rawJob;
            } else if (BaseJobByAllMountJob.TryGetValue(rawJob, out short allMountBaseJob)) {
                baseJob = allMountBaseJob;
                costumeJob = rawJob;
            }

            short visualJob = ResolveVisualJob(baseJob, effectState, allRidingState, costumeJob);
            return new RoMapRentalJobResolution(baseJob, costumeJob, visualJob);
        }

        public static short ResolveVisualJob(short baseJob, int effectState, bool allRidingState, short explicitCostumeJob = 0) {
            short costume = explicitCostumeJob;
            if ((effectState & RidingEffectMask) != 0 && MountByBaseJob.TryGetValue(baseJob, out short classicMountJob)) {
                costume = classicMountJob;
            }

            if ((effectState & EffectStateWedding) != 0) {
                costume = JobWedding;
            }

            if ((effectState & EffectStateXmas) != 0) {
                costume = JobXmas;
            }

            if ((effectState & EffectStateSummer) != 0) {
                costume = JobSummer;
            }

            if (allRidingState &&
                (effectState & AllRidingBlockingEffectMask) == 0 &&
                AllMountByBaseJob.TryGetValue(baseJob, out short allMountJob)) {
                costume = allMountJob;
            }

            return costume != 0 ? costume : baseJob;
        }

        public static RoMapRentalAttachmentState ResolveAttachments(int effectState) {
            return new RoMapRentalAttachmentState(
                (effectState & EffectStateFalcon) != 0,
                (effectState & EffectStateWug) != 0);
        }

        public static bool ShouldShowEquipmentRemoveOption(int effectState, bool hasCart) {
            return hasCart || (effectState & EquipmentRemoveOptionEffectMask) != 0;
        }

        public static bool ShouldShowEquipmentCartItems(int effectState, bool hasCart) {
            return hasCart || (effectState & CartEffectMask) != 0;
        }

        public static bool HasClassicRidingEffect(int effectState) {
            return (effectState & RidingEffectMask) != 0;
        }

        public static bool IsAllRidingBlockedByEffectState(int effectState) {
            return (effectState & AllRidingBlockingEffectMask) != 0;
        }

        public static bool IsRentalVisualJob(short job) {
            return BaseJobByMountJob.ContainsKey(job) ||
                BaseJobByAllMountJob.ContainsKey(job) ||
                job == JobWedding ||
                job == JobXmas ||
                job == JobSummer;
        }

        public static bool TryGetBaseJobFromVisualJob(short visualJob, out short baseJob) {
            if (BaseJobByMountJob.TryGetValue(visualJob, out baseJob) ||
                BaseJobByAllMountJob.TryGetValue(visualJob, out baseJob)) {
                return true;
            }

            baseJob = 0;
            return false;
        }

        public static bool TryGetClassicMountJob(short baseJob, out short mountJob) {
            return MountByBaseJob.TryGetValue(baseJob, out mountJob);
        }

        public static bool TryGetAllMountJob(short baseJob, out short mountJob) {
            return AllMountByBaseJob.TryGetValue(baseJob, out mountJob);
        }

        public static bool IsNoviceCartJob(short baseJob) {
            return baseJob == 0 ||
                baseJob == 23 ||
                baseJob == 4045 ||
                baseJob == 4190 ||
                baseJob == 4191;
        }

        public static int ResolveCartIndex(short baseJob, bool hasCart, int cartNum) {
            if (!hasCart) {
                return -1;
            }

            return IsNoviceCartJob(baseJob)
                ? 0
                : Math.Max(0, Math.Min(13, cartNum));
        }

        public static int ResolveCartEffectMask(int cartVariant) {
            switch (cartVariant) {
                case 2:
                    return EffectStateCart2;
                case 3:
                    return EffectStateCart3;
                case 4:
                    return EffectStateCart4;
                case 5:
                    return EffectStateCart5;
                default:
                    return EffectStateCart1;
            }
        }

        public static int ResolveCartVariantFromEffectState(int effectState) {
            if ((effectState & EffectStateCart5) != 0) {
                return 5;
            }

            if ((effectState & EffectStateCart4) != 0) {
                return 4;
            }

            if ((effectState & EffectStateCart3) != 0) {
                return 3;
            }

            if ((effectState & EffectStateCart2) != 0) {
                return 2;
            }

            return (effectState & EffectStateCart1) != 0 ? 1 : -1;
        }

        // the paths are built once: the presenter asks for them every frame for every synced player, and building them
        // (LegacyPathSegment's params array, char array and string, then the concatenation) was most of a walking crowd's garbage
        private static readonly string[] CartBasePaths = new string[14];
        private static readonly Dictionary<short, string> FalconBasePaths = new Dictionary<short, string>();
        private static readonly Dictionary<short, string> WugBasePaths = new Dictionary<short, string>();

        public static string ResolveCartBasePath(int cartIndex) {
            int index = Math.Max(0, Math.Min(13, cartIndex));
            string path = CartBasePaths[index];
            if (path == null) {
                CartBasePaths[index] = path = BuildCartBasePath(index);
            }

            return path;
        }

        public static string ResolveFalconBasePath(short baseJob) => Cached(FalconBasePaths, baseJob, false);

        public static string ResolveWugBasePath(short baseJob) => Cached(WugBasePaths, baseJob, true);

        private static string Cached(Dictionary<short, string> cache, short baseJob, bool wug) {
            lock (cache) {
                if (!cache.TryGetValue(baseJob, out string path)) {
                    cache[baseJob] = path = wug ? BuildWugBasePath(baseJob) : BuildFalconBasePath(baseJob);
                }

                return path;
            }
        }

        private static string BuildCartBasePath(int cartIndex) {
            switch (cartIndex) {
                case 0:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBD, 0xB4, 0xB3, 0xEB, 0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9);
                case 1:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9);
                case 2:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9, 0x31);
                case 3:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9, 0x32);
                case 4:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9, 0x33);
                case 5:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9, 0x34);
                case 6:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9, 0x35);
                case 7:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9, 0x36);
                case 8:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9, 0x37);
                case 9:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xD5, 0xBC, 0xF6, 0xB7, 0xB9, 0x38);
                case 10:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xBC, 0xB1, 0xB9, 0xB0, 0xBB, 0xF3, 0xC0, 0xDA, 0xC4, 0xAB, 0xC6, 0xAE);
                case 11:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xC6, 0xF7, 0xB8, 0xB5, 0xBD, 0xC6, 0xC0, 0xBA, 0xC4, 0xAB, 0xC6, 0xAE);
                case 12:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xC6, 0xF7, 0xB8, 0xB5, 0xC4, 0xAB, 0xC6, 0xAE);
                default:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xB8, 0xB6, 0xB5, 0xB5, 0xC4, 0xAB, 0xC6, 0xAE);
            }
        }

        public static string CartShadowBasePath => "data/sprite/shadow";

        public static bool TryResolveCartOffsetPixels(int direction, out int x, out int y) {
            switch (PositiveModulo(direction, 8)) {
                case 0:
                    x = 0;
                    y = -30;
                    return true;
                case 1:
                    x = 30;
                    y = -10;
                    return true;
                case 2:
                    x = 40;
                    y = 0;
                    return true;
                case 3:
                    x = 30;
                    y = 10;
                    return true;
                case 4:
                    x = 0;
                    y = 20;
                    return true;
                case 5:
                    x = -30;
                    y = 10;
                    return true;
                case 6:
                    x = -40;
                    y = 0;
                    return true;
                case 7:
                    x = -30;
                    y = -10;
                    return true;
                default:
                    x = 0;
                    y = 0;
                    return false;
            }
        }

        public static bool IsCartInFrontOfBody(int direction) {
            int normalized = PositiveModulo(direction, 8);
            return normalized > 2 && normalized < 6;
        }

        private static string BuildFalconBasePath(short baseJob) {
            switch (baseJob) {
                case 11:
                case 4034:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xB8, 0xC5);
                case 4012:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xB8, 0xC5, 0x32);
                case 4056:
                case 4062:
                case 4098:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/owl";
                case 4257:
                case 4270:
                case 4278:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/windhawk_hawk";
                default:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/" + LegacyPathSegment(0xB8, 0xC5);
            }
        }

        public static bool CanUseFalconAttachmentVisual(short baseJob) {
            return FalconVisualJobs.Contains(baseJob);
        }

        private static string BuildWugBasePath(short baseJob) {
            switch (baseJob) {
                case 4257:
                    return "data/sprite/" + LegacyPathSegment(0xC0, 0xCC, 0xC6, 0xD1, 0xC6, 0xAE) + "/windhawk_wolf";
                default:
                    return "data/sprite/" + LegacyPathSegment(0xB8, 0xF3, 0xBD, 0xBA, 0xC5, 0xCD) + "/" + LegacyPathSegment(0xBF, 0xF6, 0xB1, 0xD7);
            }
        }

        public static bool IsFalconSkill(ushort skillId) {
            return skillId == SkillHunterBlitzBeat ||
                skillId == SkillHunterDetecting ||
                skillId == SkillSniperFalconAssault;
        }

        public static bool IsWugSkill(ushort skillId) {
            return skillId == SkillRangerWugStrike ||
                skillId == SkillRangerWugBite;
        }

        private static int PositiveModulo(int value, int divisor) {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static Dictionary<short, short> BuildMap((short BaseJob, short MountJob)[] pairs) {
            var result = new Dictionary<short, short>(pairs.Length);
            for (int i = 0; i < pairs.Length; i++) {
                result[pairs[i].BaseJob] = pairs[i].MountJob;
            }

            return result;
        }

        private static Dictionary<short, short> BuildReverseMap((short BaseJob, short MountJob)[] pairs) {
            var result = new Dictionary<short, short>(pairs.Length);
            for (int i = 0; i < pairs.Length; i++) {
                if (!result.ContainsKey(pairs[i].MountJob)) {
                    result[pairs[i].MountJob] = pairs[i].BaseJob;
                }
            }

            return result;
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
