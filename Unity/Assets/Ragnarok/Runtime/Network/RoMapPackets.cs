using System;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Network {
    public readonly struct RoPacketItemRandomOption {
        public RoPacketItemRandomOption(short index, short value, byte param) {
            Index = index;
            Value = value;
            Param = param;
        }

        public short Index { get; }
        public short Value { get; }
        public byte Param { get; }
        public bool IsEmpty => Index == 0;
    }

    public readonly struct RoScNotifyBan {
        public RoScNotifyBan(byte errorCode) {
            ErrorCode = errorCode;
        }

        public byte ErrorCode { get; }
    }

    public readonly struct RoZcAid {
        public RoZcAid(int accountId) {
            AccountId = accountId;
        }

        public int AccountId { get; }
    }

    public readonly struct RoZcRefuseEnter {
        public RoZcRefuseEnter(byte errorCode) {
            ErrorCode = errorCode;
        }

        public byte ErrorCode { get; }
    }

    public readonly struct RoZcNotifyTime {
        public RoZcNotifyTime(uint serverTime) {
            ServerTime = serverTime;
        }

        public uint ServerTime { get; }
    }

    public readonly struct RoZcNotifyPlayerChat {
        public RoZcNotifyPlayerChat(string message, uint sourceActorId = 0) {
            Message = message ?? string.Empty;
            SourceActorId = sourceActorId;
        }

        public string Message { get; }
        public uint SourceActorId { get; }
    }

    public readonly struct RoZcSystemText {
        public RoZcSystemText(string message, uint colorRgb = 0, bool hasColor = false, ushort packetId = 0) {
            Message = message ?? string.Empty;
            ColorRgb = colorRgb;
            HasColor = hasColor;
            PacketId = packetId;
        }

        public string Message { get; }
        public uint ColorRgb { get; }
        public bool HasColor { get; }

        /// <summary>The packet the text came in (ZC_BROADCAST 0x009a, ZC_BROADCAST2 0x01c3, ZC_DEBUGMSG 0x0adb), 0 when not known.</summary>
        public ushort PacketId { get; }
    }

    public readonly struct RoZcNpcChat {
        public RoZcNpcChat(uint sourceId, uint colorBgr, string message) {
            SourceId = sourceId;
            ColorBgr = colorBgr;
            Message = message ?? string.Empty;
        }

        public uint SourceId { get; }
        public uint ColorBgr { get; }
        public string Message { get; }
    }

    public readonly struct RoZcWhisper {
        public RoZcWhisper(string sender, string message, uint senderActorId = 0, byte isAdmin = 0) {
            Sender = sender ?? string.Empty;
            Message = message ?? string.Empty;
            SenderActorId = senderActorId;
            IsAdmin = isAdmin;
        }

        public string Sender { get; }
        public string Message { get; }
        public uint SenderActorId { get; }
        public byte IsAdmin { get; }
    }

    public readonly struct RoZcWhisperAck {
        public RoZcWhisperAck(byte result, uint id = 0) {
            Result = result;
            Id = id;
        }

        public byte Result { get; }

        /// <summary>0x09df's dword at 3 (Hercules: the sender's own account id); 0 for 0x0098.</summary>
        public uint Id { get; }
        public bool IsSuccess => Result == 0;
    }

    public readonly struct RoZcMessage {
        public RoZcMessage(
            int messageId,
            int numberValue = 0,
            bool hasNumberValue = false,
            string stringValue = null,
            ushort skillId = 0,
            bool hasSkillId = false,
            uint colorRgb = 0,
            bool hasColor = false) {
            MessageId = messageId;
            NumberValue = numberValue;
            HasNumberValue = hasNumberValue;
            StringValue = stringValue ?? string.Empty;
            HasStringValue = stringValue != null;
            SkillId = skillId;
            HasSkillId = hasSkillId;
            ColorRgb = colorRgb;
            HasColor = hasColor;
        }

        public int MessageId { get; }
        public int NumberValue { get; }
        public bool HasNumberValue { get; }
        public string StringValue { get; }
        public bool HasStringValue { get; }
        public ushort SkillId { get; }
        public bool HasSkillId { get; }
        public uint ColorRgb { get; }
        public bool HasColor { get; }
    }

    public readonly struct RoZcNpcDialogText {
        public RoZcNpcDialogText(uint npcId, string message) {
            NpcId = npcId;
            Message = message ?? string.Empty;
        }

        public uint NpcId { get; }
        public string Message { get; }
    }

    public readonly struct RoZcNpcDialogSignal {
        public RoZcNpcDialogSignal(uint npcId) {
            NpcId = npcId;
        }

        public uint NpcId { get; }
    }

    public readonly struct RoZcShowScript {
        public RoZcShowScript(uint entityId, string message) {
            EntityId = entityId;
            Message = message ?? string.Empty;
        }

        public uint EntityId { get; }
        public string Message { get; }
    }

    public readonly struct RoZcNpcMapMove {
        public RoZcNpcMapMove(string mapName, ushort x, ushort y) {
            MapName = mapName ?? string.Empty;
            X = x;
            Y = y;
        }

        public string MapName { get; }
        public ushort X { get; }
        public ushort Y { get; }
    }

    public readonly struct RoZcNpcServerMove {
        public RoZcNpcServerMove(string mapName, ushort x, ushort y, string address, ushort port, string domain) {
            MapName = mapName ?? string.Empty;
            X = x;
            Y = y;
            Address = address ?? string.Empty;
            Port = port;
            Domain = domain ?? string.Empty;
        }

        public string MapName { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public string Address { get; }
        public ushort Port { get; }
        public string Domain { get; }
    }

    public readonly struct RoZcNpcCutin {
        public RoZcNpcCutin(string imageName, byte typeRaw) {
            ImageName = imageName ?? string.Empty;
            TypeRaw = typeRaw;
        }

        public string ImageName { get; }
        public byte TypeRaw { get; }
        public bool IsClear => TypeRaw == byte.MaxValue;
    }

    public readonly struct RoZcCompass {
        public RoZcCompass(uint npcAid, int typeRaw, int x, int y, byte markerId, uint colorRaw) {
            NpcAid = npcAid;
            TypeRaw = typeRaw;
            X = x;
            Y = y;
            MarkerId = markerId;
            ColorRaw = colorRaw;
        }

        public uint NpcAid { get; }
        public int TypeRaw { get; }
        public int X { get; }
        public int Y { get; }
        public byte MarkerId { get; }
        public uint ColorRaw { get; }
        public bool IsRemove => TypeRaw == 2;
    }

    public readonly struct RoZcUiOpen {
        public RoZcUiOpen(byte uiType, int data) {
            UiType = uiType;
            Data = data;
        }

        public byte UiType { get; }
        public int Data { get; }
        public bool IsAttendance => UiType == RoAttendanceRuntime.ServerAttendanceUiType;
    }

    public readonly struct RoZcAttendanceResponse {
        public RoZcAttendanceResponse(int unknown, int data) {
            Unknown = unknown;
            Data = data;
        }

        public int Unknown { get; }
        public int Data { get; }
    }

    public enum RoMapActorEntryKind {
        Unknown = 0,
        Moving = 1,
        New = 2,
        Standing = 3
    }

    public enum RoMapActorObjectType {
        Effect = -3,
        Unknown = -2,
        Warp = -1,
        Player = 0,
        Disguised = 1,
        Monster = 5,
        Npc = 6,
        Pet = 7,
        Homunculus = 8,
        Mercenary = 9,
        Elemental = 10,
        Item = 11,
        Npc2 = 12
    }

    public enum RoMapActorSpawnState : byte {
        Standing = 0,
        Dead = 1,
        Sitting = 2
    }

    public enum RoMapActorVanishType : byte {
        OutOfSight = 0,
        Died = 1,
        LoggedOut = 2,
        Teleport = 3,
        TrickDead = 4
    }

    public enum RoMapActorSpriteChangeType : byte {
        Job = 0,
        Head = 1,
        Weapon = 2,
        HeadgearLower = 3,
        HeadgearTop = 4,
        HeadgearMid = 5,
        HeadPalette = 6,
        BodyPalette = 7,
        Shield = 8,
        Shoes = 9,
        Body = 10,
        ResetCostumes = 11,
        Robe = 12
    }

    public readonly struct RoZcChangeDirection {
        public RoZcChangeDirection(uint aid, ushort headDirection, byte directionRaw) {
            Aid = aid;
            HeadDirection = headDirection;
            DirectionRaw = directionRaw;
        }

        public uint Aid { get; }
        public ushort HeadDirection { get; }
        public byte DirectionRaw { get; }
    }

    public enum RoMapActorActionType : byte {
        NormalDamage = 0,
        PickupItem = 1,
        SitDown = 2,
        StandUp = 3,
        Absorbed = 4,
        Splash = 5,
        Skill = 6,
        Repeat = 7,
        DoubleAttack = 8,
        Endure = 9,
        Critical = 10,
        LuckyDodge = 11,
        Touch = 12,
        MultiHitCritical = 13
    }

    public static class RoMapActorActionSemantics {
        public static bool IsKnown(byte raw) {
            return TryGetKnown(raw, out _);
        }

        public static bool IsDamage(byte raw) {
            switch (raw) {
                case (byte) RoMapActorActionType.NormalDamage:
                case (byte) RoMapActorActionType.Absorbed:
                case (byte) RoMapActorActionType.DoubleAttack:
                case (byte) RoMapActorActionType.Endure:
                case (byte) RoMapActorActionType.Critical:
                case (byte) RoMapActorActionType.LuckyDodge:
                case (byte) RoMapActorActionType.MultiHitCritical:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsCritical(byte raw) {
            return raw == (byte) RoMapActorActionType.Critical ||
                raw == (byte) RoMapActorActionType.MultiHitCritical;
        }

        public static bool IsPickup(byte raw) {
            return raw == (byte) RoMapActorActionType.PickupItem;
        }

        public static bool IsSit(byte raw) {
            return raw == (byte) RoMapActorActionType.SitDown;
        }

        public static bool IsStand(byte raw) {
            return raw == (byte) RoMapActorActionType.StandUp;
        }

        public static bool TryGetKnown(byte raw, out RoMapActorActionType action) {
            switch (raw) {
                case (byte) RoMapActorActionType.NormalDamage:
                case (byte) RoMapActorActionType.PickupItem:
                case (byte) RoMapActorActionType.SitDown:
                case (byte) RoMapActorActionType.StandUp:
                case (byte) RoMapActorActionType.Absorbed:
                case (byte) RoMapActorActionType.Splash:
                case (byte) RoMapActorActionType.Skill:
                case (byte) RoMapActorActionType.Repeat:
                case (byte) RoMapActorActionType.DoubleAttack:
                case (byte) RoMapActorActionType.Endure:
                case (byte) RoMapActorActionType.Critical:
                case (byte) RoMapActorActionType.LuckyDodge:
                case (byte) RoMapActorActionType.Touch:
                case (byte) RoMapActorActionType.MultiHitCritical:
                    action = (RoMapActorActionType) raw;
                    return true;
                default:
                    action = default;
                    return false;
            }
        }
    }

    public enum RoMapSkillActionMotion : byte {
        Idle = 0,
        Pickup = 1,
        Attack = 2,
        Attack2 = 3,
        Attack3 = 4,
        Casting = 5,
        Skill = 6,
        Throwing = 7
    }

    public enum RoMapWeaponType : int {
        None = 0,
        ShortSword = 1,
        Sword = 2,
        TwoHandSword = 3,
        Spear = 4,
        TwoHandSpear = 5,
        Axe = 6,
        TwoHandAxe = 7,
        Mace = 8,
        TwoHandMace = 9,
        Rod = 10,
        Bow = 11,
        Knuckle = 12,
        Instrument = 13,
        Whip = 14,
        Book = 15,
        Katar = 16,
        GunHandgun = 17,
        GunRifle = 18,
        GunGatling = 19,
        GunShotgun = 20,
        GunGrenade = 21,
        Shuriken = 22,
        TwoHandRod = 23,
        Last = 24,
        ShortSwordShortSword = 25,
        SwordSword = 26,
        AxeAxe = 27,
        ShortSwordSword = 28,
        ShortSwordAxe = 29,
        SwordAxe = 30,
        MainGauche = 31,
        Stiletto = 32,
        Gladius = 33,
        ZenyKnife = 34,
        PoisonKnife = 35,
        PrincessKnife = 36,
        Sasimi = 37,
        Lacma = 38,
        Tsurugi = 39,
        RingPommelSaber = 40,
        Haedonggum = 41,
        Saber = 42,
        JewelSword = 43,
        GaiaSword = 44,
        TwinEdgeB = 45,
        TwinEdgeR = 46,
        PriestSword = 47,
        Katana = 48,
        BastardSword = 49,
        BroadSword = 50,
        VioletFear = 51,
        Lance = 52,
        Partizan = 53,
        Trident = 54,
        Halberd = 55,
        CrescentScythe = 56,
        Zephyrus = 57,
        Hammer = 58,
        Buster = 59,
        BroodAxe = 60,
        RightEpsilon = 61,
        ExpandedMace = 62,
        SwordMace = 63,
        Chain = 64,
        Stunner = 65,
        GoldenMace = 66,
        IronDriver = 67,
        Spanner = 68,
        ArcWand = 69,
        MightyStaff = 70,
        BlessedWand = 71,
        BoneWand = 72,
        CrossBow = 73,
        Arbalest = 74,
        Kakkung = 75,
        HunterBow = 76,
        BowOfRudra = 77,
        Waghnakh = 78,
        KnuckleDuster = 79,
        Hora = 80,
        Fist = 81,
        Claw = 82,
        Finger = 83,
        KaiserKnuckle = 84,
        Berserk = 85,
        Rante = 86,
        Tail = 87,
        ExpandedWhip = 88,
        Bible = 89,
        BookOfBillows = 90,
        BookOfMotherEarth = 91,
        BookOfBlazingSun = 92,
        BookOfGustOfWind = 93,
        BookOfTheApocalypse = 94,
        GirlsDiary = 95,
        StaffOfSoul = 96,
        WizardyStaff = 97,
        Spoon = 98,
        FoxtailBrown = 99,
        FoxtailGreen = 100,
        CandyCaneRod = 101,
        FoxtailMetal = 102
    }

    public enum RoMapBasicAttackMotion : byte {
        Attack1 = 0,
        Attack2 = 1,
        Attack3 = 2
    }

    public readonly struct RoMapPresentedEquipment {
        public RoMapPresentedEquipment(uint weapon, uint shield) {
            Weapon = weapon;
            Shield = shield;
        }

        public uint Weapon { get; }
        public uint Shield { get; }
    }

    public static class RoMapBasicAttackTimingSemantics {
        public const int AverageAttackSpeedMilliseconds = 432;
        public const int MaxAttackMotionMilliseconds = AverageAttackSpeedMilliseconds * 2;
        public const double PlayerAttackVisualMotionScale = 1.1d;
        public const int PlayerAttackVisualMotionMaxExtraMilliseconds = 100;

        public static int ClampAttackMotionTime(int attackMotionTimeMilliseconds) {
            if (attackMotionTimeMilliseconds <= 0) {
                return 0;
            }

            return Math.Min(attackMotionTimeMilliseconds, MaxAttackMotionMilliseconds);
        }

        public static int ResolvePlayerVisualAttackMotionMilliseconds(int attackMotionTimeMilliseconds) {
            int attackMotionTime = ClampAttackMotionTime(attackMotionTimeMilliseconds);
            if (attackMotionTime <= 0) {
                return 0;
            }

            double scaled = attackMotionTime * PlayerAttackVisualMotionScale;
            int maxExtended = attackMotionTime + PlayerAttackVisualMotionMaxExtraMilliseconds;
            return Math.Max(1, (int) Math.Round(Math.Min(scaled, maxExtended)));
        }

        public static int ResolveTargetHitDurationMilliseconds(RoZcNotifyAct action, int fallbackMilliseconds = 300) {
            return action.TargetMotionTime > 0 ? action.TargetMotionTime : Math.Max(1, fallbackMilliseconds);
        }

        public static int ResolveSourceAttackDurationMilliseconds(
            RoZcNotifyAct action,
            RoMapActorObjectType objectType,
            short jobId,
            byte sex,
            uint weapon,
            int fallbackMilliseconds = 450) {
            int attackMotionTime = ClampAttackMotionTime(action.SourceMotionTime);
            if (attackMotionTime <= 0) {
                attackMotionTime = Math.Max(1, fallbackMilliseconds);
            }

            int visualAttackMotionTime = objectType == RoMapActorObjectType.Player
                ? ResolvePlayerVisualAttackMotionMilliseconds(attackMotionTime)
                : attackMotionTime;

            if (action.Damage2 <= 0) {
                return visualAttackMotionTime;
            }

            return visualAttackMotionTime + ResolveSecondAttackSourceDelayMilliseconds(
                attackMotionTime,
                objectType,
                jobId,
                sex,
                weapon);
        }

        public static int ResolveSecondAttackSourceDelayMilliseconds(
            int attackMotionTimeMilliseconds,
            RoMapActorObjectType objectType,
            short jobId,
            byte sex,
            uint weapon) {
            int attackMotionTime = ClampAttackMotionTime(attackMotionTimeMilliseconds);
            if (attackMotionTime <= 0) {
                return 0;
            }

            if (objectType != RoMapActorObjectType.Player) {
                return attackMotionTime;
            }

            double motionUnits = RoMapWeaponActionSemantics.ResolvePlayerAttackMotionUnits(jobId, sex, weapon);
            double speedFactor = attackMotionTime / (double) AverageAttackSpeedMilliseconds;
            return Math.Max(0, (int) Math.Round(motionUnits * speedFactor * 24d));
        }

    }

    public static class RoMapWeaponActionSemantics {
        private const int WeaponTypeMax = 103;

        public static RoMapWeaponType ResolveWeaponType(
            uint itemId,
            bool realType = false,
            bool considerDualHandIds = false) {
            int id = itemId > int.MaxValue ? int.MaxValue : (int) itemId;
            if (id < 0) {
                return RoMapWeaponType.None;
            }

            if (realType && TryResolveExpandedWeaponType(id, out RoMapWeaponType expandedType)) {
                return expandedType;
            }

            if (considerDualHandIds && id <= (int) RoMapWeaponType.SwordAxe) {
                return (RoMapWeaponType) id;
            }

            if (id < WeaponTypeMax) {
                return (RoMapWeaponType) id;
            }

            if (IsOneOf(id, 13157, 13158, 13159, 13172, 13177)) {
                return RoMapWeaponType.GunGatling;
            }

            if (IsOneOf(id, 13154, 13155, 13156, 13167, 13168, 13169, 13173, 13178)) {
                return RoMapWeaponType.GunShotgun;
            }

            if (IsOneOf(id, 13160, 13161, 13162, 13174, 13179)) {
                return RoMapWeaponType.GunGrenade;
            }

            if (id >= 1100 && id <= 1149) {
                return RoMapWeaponType.Sword;
            }

            if (id >= 1150 && id <= 1199) {
                return RoMapWeaponType.TwoHandSword;
            }

            if (id >= 1200 && id <= 1249) {
                return RoMapWeaponType.ShortSword;
            }

            if (id >= 1250 && id <= 1299) {
                return RoMapWeaponType.Katar;
            }

            if (id >= 1300 && id <= 1349) {
                return RoMapWeaponType.Axe;
            }

            if (id >= 1350 && id <= 1399) {
                return RoMapWeaponType.TwoHandAxe;
            }

            if (id >= 1400 && id <= 1449) {
                return RoMapWeaponType.Spear;
            }

            if (id >= 1450 && id <= 1499) {
                return RoMapWeaponType.TwoHandSpear;
            }

            if (id >= 1500 && id <= 1549) {
                return RoMapWeaponType.Mace;
            }

            if (id >= 1550 && id <= 1599) {
                return RoMapWeaponType.Book;
            }

            if (id >= 1600 && id <= 1699) {
                return RoMapWeaponType.Rod;
            }

            if (id >= 1700 && id <= 1749) {
                return RoMapWeaponType.Bow;
            }

            if (id >= 1750 && id <= 1799) {
                return RoMapWeaponType.None;
            }

            if (id >= 1800 && id <= 1849) {
                return RoMapWeaponType.Knuckle;
            }

            if (id >= 1900 && id <= 1949) {
                return RoMapWeaponType.Instrument;
            }

            if (id >= 1950 && id <= 1999) {
                return RoMapWeaponType.Whip;
            }

            if (id >= 20000 && id <= 20999) {
                return RoMapWeaponType.TwoHandRod;
            }

            if (id >= 13000 && id <= 13099) {
                return RoMapWeaponType.ShortSword;
            }

            if (id >= 13100 && id <= 13149) {
                return RoMapWeaponType.GunHandgun;
            }

            if (id >= 13150 && id <= 13199) {
                return RoMapWeaponType.GunRifle;
            }

            if (id >= 13300 && id <= 13399) {
                return RoMapWeaponType.Shuriken;
            }

            if (id >= 13400 && id <= 13499) {
                return RoMapWeaponType.Sword;
            }

            if (id >= 18100 && id <= 18499) {
                return RoMapWeaponType.Bow;
            }

            if (id >= 21000 && id <= 21999) {
                return RoMapWeaponType.TwoHandSword;
            }

            return RoMapWeaponType.None;
        }

        public static uint ResolveWeaponViewId(uint itemId) {
            if (itemId == 0) {
                return 0;
            }

            if (itemId < WeaponTypeMax) {
                return itemId;
            }

            return (uint) ResolveWeaponType(itemId);
        }

        public static bool IsShieldId(uint itemId) {
            return (itemId >= 2100 && itemId <= 2199) ||
                (itemId >= 28900 && itemId <= 28999) ||
                (itemId >= 460000 && itemId <= 460099);
        }

        public static bool IsAssassinJob(short jobId) {
            return ResolveWeaponActionProfile(jobId) == RoMapWeaponActionProfile.Assassin;
        }

        public static bool IsKagerouOrOboroJob(short jobId) {
            switch (jobId) {
                case 4211:
                case 4212:
                case 4223:
                case 4224:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsKatar(RoMapWeaponType weaponType) {
            return weaponType == RoMapWeaponType.Katar;
        }

        public static uint MountWeapon(uint weaponId, uint shieldId) {
            int weaponType = (int) ResolveWeaponType(weaponId, realType: true);
            int shieldType = (int) ResolveWeaponType(shieldId, realType: true);
            int viewId = weaponType + shieldType;
            switch (viewId) {
                case 2:
                    return (uint) RoMapWeaponType.ShortSwordShortSword;
                case 3:
                    return (uint) RoMapWeaponType.ShortSwordSword;
                case 4:
                    return (uint) RoMapWeaponType.SwordSword;
                case 7:
                    return (uint) RoMapWeaponType.ShortSwordAxe;
                case 8:
                    return (uint) RoMapWeaponType.SwordAxe;
                case 12:
                    return (uint) RoMapWeaponType.AxeAxe;
                default:
                    return (uint) Math.Max(0, viewId);
            }
        }

        public static RoMapPresentedEquipment NormalizePresentationEquipment(
            RoMapActorObjectType objectType,
            short jobId,
            uint weapon,
            uint shield) {
            if (objectType != RoMapActorObjectType.Player) {
                return new RoMapPresentedEquipment(weapon, shield);
            }

            if (weapon == 0 && shield != 0 && !IsShieldId(shield)) {
                weapon = shield;
                shield = 0;
            }

            RoMapWeaponType weaponType = ResolveWeaponType(weapon, realType: true);
            uint weaponViewId = ResolveWeaponViewId(weapon);
            if (IsAssassinJob(jobId) && shield != 0 && !IsShieldId(shield) && weapon != 0) {
                return new RoMapPresentedEquipment(MountWeapon(weapon, shield), 0);
            }

            if (IsKatar(weaponType)) {
                return new RoMapPresentedEquipment(weaponViewId, weaponViewId);
            }

            return new RoMapPresentedEquipment(weaponViewId, shield);
        }


        public static double ResolvePlayerAttackMotionUnits(short jobId, byte sex, uint weapon) {
            RoMapWeaponType weaponType = ResolveWeaponType(weapon, realType: true, considerDualHandIds: true);
            if (IsDualWeaponAnimationProfile(jobId, sex, weaponType)) {
                switch (ResolveWeaponActionProfile(jobId)) {
                    case RoMapWeaponActionProfile.Assassin:
                        switch (weaponType) {
                            case RoMapWeaponType.Katar:
                            case RoMapWeaponType.ShortSwordShortSword:
                            case RoMapWeaponType.SwordSword:
                            case RoMapWeaponType.AxeAxe:
                            case RoMapWeaponType.ShortSwordSword:
                            case RoMapWeaponType.ShortSwordAxe:
                            case RoMapWeaponType.SwordAxe:
                                return 3d;
                        }

                        break;
                    case RoMapWeaponActionProfile.Thief:
                        return 5.75d;
                    case RoMapWeaponActionProfile.Merchant:
                        return 5.85d;
                }
            } else {
                switch (ResolveWeaponActionProfile(jobId)) {
                    case RoMapWeaponActionProfile.Novice:
                    case RoMapWeaponActionProfile.SuperNovice:
                        if (sex == 1) {
                            return 5.85d;
                        }

                        break;
                    case RoMapWeaponActionProfile.Assassin:
                        switch (weaponType) {
                            case RoMapWeaponType.Katar:
                            case RoMapWeaponType.ShortSwordShortSword:
                            case RoMapWeaponType.SwordSword:
                            case RoMapWeaponType.AxeAxe:
                            case RoMapWeaponType.ShortSwordSword:
                            case RoMapWeaponType.ShortSwordAxe:
                            case RoMapWeaponType.SwordAxe:
                                return 3d;
                        }

                        break;
                }
            }

            return 6d;
        }

        public static bool IsDualWeaponAnimationProfile(short jobId, byte sex, RoMapWeaponType weaponType) {
            switch (ResolveWeaponActionProfile(jobId)) {
                case RoMapWeaponActionProfile.Gunslinger:
                    return weaponType == RoMapWeaponType.GunRifle ||
                        weaponType == RoMapWeaponType.GunGatling ||
                        weaponType == RoMapWeaponType.GunShotgun ||
                        weaponType == RoMapWeaponType.GunGrenade;
                case RoMapWeaponActionProfile.Ninja:
                    return weaponType == RoMapWeaponType.Shuriken;
                case RoMapWeaponActionProfile.Linker:
                case RoMapWeaponActionProfile.Wizard:
                    return (weaponType == RoMapWeaponType.ShortSword && sex == 1) ||
                        ((weaponType == RoMapWeaponType.Rod || weaponType == RoMapWeaponType.TwoHandRod) && sex == 0);
                case RoMapWeaponActionProfile.Swordman:
                    return weaponType == RoMapWeaponType.TwoHandSword ||
                        weaponType == RoMapWeaponType.TwoHandSpear;
                case RoMapWeaponActionProfile.Archer:
                    return weaponType != RoMapWeaponType.Bow;
                case RoMapWeaponActionProfile.Thief:
                    return weaponType == RoMapWeaponType.Bow;
                case RoMapWeaponActionProfile.Magician:
                    return weaponType == RoMapWeaponType.TwoHandRod;
                case RoMapWeaponActionProfile.Merchant:
                    return weaponType == RoMapWeaponType.TwoHandAxe;
                case RoMapWeaponActionProfile.Novice:
                case RoMapWeaponActionProfile.SuperNovice:
                    if (sex == 0) {
                        return weaponType == RoMapWeaponType.ShortSword;
                    }

                    return weaponType == RoMapWeaponType.TwoHandSword ||
                        weaponType == RoMapWeaponType.TwoHandAxe ||
                        weaponType == RoMapWeaponType.TwoHandRod ||
                        weaponType == RoMapWeaponType.TwoHandMace;
                case RoMapWeaponActionProfile.Knight:
                case RoMapWeaponActionProfile.Knight2:
                    return weaponType == RoMapWeaponType.TwoHandSpear ||
                        weaponType == RoMapWeaponType.TwoHandSword;
                case RoMapWeaponActionProfile.Priest:
                    return weaponType == RoMapWeaponType.Book;
                case RoMapWeaponActionProfile.Blacksmith:
                case RoMapWeaponActionProfile.Alchemist:
                    return weaponType == RoMapWeaponType.Sword ||
                        weaponType == RoMapWeaponType.Axe ||
                        weaponType == RoMapWeaponType.TwoHandAxe ||
                        weaponType == RoMapWeaponType.Mace;
                case RoMapWeaponActionProfile.Assassin:
                    return weaponType == RoMapWeaponType.Katar ||
                        weaponType == RoMapWeaponType.ShortSwordShortSword ||
                        weaponType == RoMapWeaponType.ShortSwordSword ||
                        weaponType == RoMapWeaponType.ShortSwordAxe ||
                        weaponType == RoMapWeaponType.SwordSword ||
                        weaponType == RoMapWeaponType.SwordAxe ||
                        weaponType == RoMapWeaponType.AxeAxe;
                case RoMapWeaponActionProfile.Hunter:
                case RoMapWeaponActionProfile.Rogue:
                case RoMapWeaponActionProfile.Bard:
                case RoMapWeaponActionProfile.Dancer:
                    return weaponType == RoMapWeaponType.Bow;
                case RoMapWeaponActionProfile.Sage:
                    return weaponType == RoMapWeaponType.Book ||
                        weaponType == RoMapWeaponType.Rod ||
                        weaponType == RoMapWeaponType.TwoHandRod ||
                        weaponType == RoMapWeaponType.TwoHandSpear;
                case RoMapWeaponActionProfile.Crusader:
                case RoMapWeaponActionProfile.Crusader2:
                    return weaponType == RoMapWeaponType.Spear ||
                        weaponType == RoMapWeaponType.TwoHandSpear;
                case RoMapWeaponActionProfile.Monk:
                    return weaponType == RoMapWeaponType.Knuckle ||
                        weaponType == RoMapWeaponType.None;
                default:
                    return false;
            }
        }

        private static int ResolveWeaponActionIndex(short jobId, byte sex, RoMapWeaponType weaponType) {
            switch (ResolveWeaponActionProfile(jobId)) {
                case RoMapWeaponActionProfile.Novice:
                    return ResolveNoviceWeaponAction(sex, weaponType);
                case RoMapWeaponActionProfile.Swordman:
                    return ResolveSwordmanWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Magician:
                    return ResolveMagicianWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Archer:
                    return ResolveArcherWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Acolyte:
                    return ResolveAcolyteWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Merchant:
                    return ResolveMerchantWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Thief:
                    return ResolveThiefWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Knight:
                    return ResolveKnightWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Priest:
                    return ResolvePriestWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Wizard:
                    return ResolveWizardWeaponAction(sex, weaponType);
                case RoMapWeaponActionProfile.Blacksmith:
                    return ResolveBlacksmithWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Hunter:
                    return ResolveHunterWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Assassin:
                    return ResolveAssassinWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Knight2:
                    return ResolveKnightWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Crusader:
                    return ResolveSwordmanWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Monk:
                    return ResolveMonkWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Sage:
                    return ResolveSageWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Rogue:
                    return ResolveThiefWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Alchemist:
                    return ResolveAlchemistWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Bard:
                    return ResolveBardWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Dancer:
                    return ResolveDancerWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Crusader2:
                    return ResolveCrusader2WeaponAction(weaponType);
                case RoMapWeaponActionProfile.SuperNovice:
                    return ResolveSuperNoviceWeaponAction(sex, weaponType);
                case RoMapWeaponActionProfile.Ninja:
                    return ResolveNinjaWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Gunslinger:
                    return ResolveGunslingerWeaponAction(weaponType);
                case RoMapWeaponActionProfile.Linker:
                    return ResolveWizardWeaponAction(sex, weaponType);
                default:
                    return 0;
            }
        }

        private static RoMapWeaponActionProfile ResolveWeaponActionProfile(short jobId) {
            switch (jobId) {
                case 0:
                case 4001:
                case 4023:
                    return RoMapWeaponActionProfile.Novice;
                case 1:
                case 4002:
                case 4024:
                    return RoMapWeaponActionProfile.Swordman;
                case 2:
                case 4003:
                case 4025:
                    return RoMapWeaponActionProfile.Magician;
                case 3:
                case 4004:
                case 4026:
                    return RoMapWeaponActionProfile.Archer;
                case 4:
                case 4005:
                case 4027:
                    return RoMapWeaponActionProfile.Acolyte;
                case 5:
                case 4006:
                case 4028:
                    return RoMapWeaponActionProfile.Merchant;
                case 6:
                case 4007:
                case 4029:
                    return RoMapWeaponActionProfile.Thief;
                case 7:
                case 4008:
                case 4030:
                case 4054:
                case 4060:
                case 4096:
                case 4252:
                case 4265:
                case 4332:
                    return RoMapWeaponActionProfile.Knight;
                case 8:
                case 4009:
                case 4031:
                case 4057:
                case 4063:
                case 4099:
                case 4256:
                case 4269:
                case 4336:
                    return RoMapWeaponActionProfile.Priest;
                case 9:
                case 4010:
                case 4032:
                case 4055:
                case 4061:
                case 4097:
                case 4255:
                case 4268:
                case 4335:
                    return RoMapWeaponActionProfile.Wizard;
                case 10:
                case 4011:
                case 4033:
                case 4058:
                case 4064:
                case 4100:
                case 4253:
                case 4266:
                case 4279:
                case 4333:
                case 4347:
                    return RoMapWeaponActionProfile.Blacksmith;
                case 11:
                case 4012:
                case 4034:
                case 4056:
                case 4062:
                case 4098:
                case 4257:
                case 4270:
                case 4278:
                case 4337:
                case 4346:
                    return RoMapWeaponActionProfile.Hunter;
                case 12:
                case 4013:
                case 4035:
                case 4059:
                case 4065:
                case 4101:
                case 4254:
                case 4267:
                case 4334:
                    return RoMapWeaponActionProfile.Assassin;
                case 13:
                case 4014:
                case 4036:
                case 4080:
                case 4081:
                case 4109:
                case 4280:
                case 4345:
                    return RoMapWeaponActionProfile.Knight2;
                case 14:
                case 4015:
                case 4037:
                case 4066:
                case 4073:
                case 4102:
                case 4258:
                case 4271:
                case 4338:
                    return RoMapWeaponActionProfile.Crusader;
                case 15:
                case 4016:
                case 4038:
                case 4070:
                case 4077:
                case 4106:
                case 4262:
                case 4275:
                case 4342:
                    return RoMapWeaponActionProfile.Monk;
                case 16:
                case 4017:
                case 4039:
                case 4067:
                case 4074:
                case 4103:
                case 4261:
                case 4274:
                case 4341:
                    return RoMapWeaponActionProfile.Sage;
                case 17:
                case 4018:
                case 4040:
                case 4072:
                case 4079:
                case 4108:
                case 4260:
                case 4273:
                case 4340:
                    return RoMapWeaponActionProfile.Rogue;
                case 18:
                case 4019:
                case 4041:
                case 4071:
                case 4078:
                case 4107:
                case 4259:
                case 4272:
                case 4339:
                    return RoMapWeaponActionProfile.Alchemist;
                case 19:
                case 4020:
                case 4042:
                case 4068:
                case 4075:
                case 4104:
                case 4263:
                case 4276:
                case 4343:
                    return RoMapWeaponActionProfile.Bard;
                case 20:
                case 4021:
                case 4043:
                case 4069:
                case 4076:
                case 4105:
                case 4264:
                case 4277:
                case 4344:
                    return RoMapWeaponActionProfile.Dancer;
                case 21:
                case 4022:
                case 4044:
                case 4082:
                case 4083:
                case 4110:
                case 4281:
                case 4348:
                    return RoMapWeaponActionProfile.Crusader2;
                case 23:
                case 4045:
                case 4190:
                case 4191:
                case 4307:
                    return RoMapWeaponActionProfile.SuperNovice;
                case 24:
                case 4215:
                case 4228:
                case 4229:
                case 4306:
                    return RoMapWeaponActionProfile.Gunslinger;
                case 25:
                case 4211:
                case 4212:
                case 4222:
                case 4223:
                case 4224:
                case 4304:
                case 4305:
                    return RoMapWeaponActionProfile.Ninja;
                case 4049:
                case 4227:
                case 4240:
                case 4242:
                case 4303:
                    return RoMapWeaponActionProfile.Linker;
                default:
                    return RoMapWeaponActionProfile.Unknown;
            }
        }

        private static int ResolveNoviceWeaponAction(byte sex, RoMapWeaponType weaponType) {
            if (sex == 0) {
                switch (weaponType) {
                    case RoMapWeaponType.Rod:
                    case RoMapWeaponType.TwoHandRod:
                    case RoMapWeaponType.Sword:
                    case RoMapWeaponType.TwoHandSword:
                    case RoMapWeaponType.Axe:
                    case RoMapWeaponType.TwoHandAxe:
                    case RoMapWeaponType.Mace:
                    case RoMapWeaponType.TwoHandMace:
                        return 1;
                    case RoMapWeaponType.ShortSword:
                        return 2;
                    default:
                        return 0;
                }
            }

            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                    return 1;
                case RoMapWeaponType.Rod:
                case RoMapWeaponType.TwoHandRod:
                case RoMapWeaponType.Sword:
                case RoMapWeaponType.TwoHandSword:
                case RoMapWeaponType.Axe:
                case RoMapWeaponType.TwoHandAxe:
                case RoMapWeaponType.Mace:
                case RoMapWeaponType.TwoHandMace:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveSwordmanWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                case RoMapWeaponType.Sword:
                case RoMapWeaponType.TwoHandSword:
                case RoMapWeaponType.Axe:
                case RoMapWeaponType.TwoHandAxe:
                case RoMapWeaponType.Mace:
                case RoMapWeaponType.TwoHandMace:
                    return 1;
                case RoMapWeaponType.Spear:
                case RoMapWeaponType.TwoHandSpear:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveMagicianWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.Rod:
                case RoMapWeaponType.TwoHandRod:
                    return 1;
                case RoMapWeaponType.ShortSword:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveArcherWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.Bow:
                    return 1;
                case RoMapWeaponType.ShortSword:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveAcolyteWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.Rod:
                case RoMapWeaponType.TwoHandRod:
                case RoMapWeaponType.Mace:
                case RoMapWeaponType.TwoHandMace:
                    return 1;
                default:
                    return 0;
            }
        }

        private static int ResolveMerchantWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.None:
                case RoMapWeaponType.Mace:
                case RoMapWeaponType.TwoHandMace:
                case RoMapWeaponType.Axe:
                case RoMapWeaponType.TwoHandAxe:
                case RoMapWeaponType.Sword:
                case RoMapWeaponType.TwoHandSword:
                    return 1;
                case RoMapWeaponType.ShortSword:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveThiefWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.Sword:
                case RoMapWeaponType.TwoHandSword:
                case RoMapWeaponType.ShortSword:
                    return 1;
                case RoMapWeaponType.Bow:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveKnightWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                case RoMapWeaponType.Sword:
                case RoMapWeaponType.TwoHandSword:
                case RoMapWeaponType.TwoHandMace:
                case RoMapWeaponType.Axe:
                case RoMapWeaponType.TwoHandAxe:
                case RoMapWeaponType.Mace:
                    return 1;
                case RoMapWeaponType.Spear:
                case RoMapWeaponType.TwoHandSpear:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolvePriestWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.Rod:
                case RoMapWeaponType.TwoHandRod:
                case RoMapWeaponType.Mace:
                case RoMapWeaponType.TwoHandMace:
                    return 1;
                case RoMapWeaponType.Book:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveWizardWeaponAction(byte sex, RoMapWeaponType weaponType) {
            if (sex == 0) {
                switch (weaponType) {
                    case RoMapWeaponType.ShortSword:
                        return 1;
                    case RoMapWeaponType.Rod:
                    case RoMapWeaponType.TwoHandRod:
                        return 2;
                    default:
                        return 0;
                }
            }

            switch (weaponType) {
                case RoMapWeaponType.Rod:
                case RoMapWeaponType.TwoHandRod:
                    return 1;
                case RoMapWeaponType.ShortSword:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveBlacksmithWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                    return 1;
                case RoMapWeaponType.Sword:
                case RoMapWeaponType.TwoHandSword:
                case RoMapWeaponType.Axe:
                case RoMapWeaponType.TwoHandAxe:
                case RoMapWeaponType.Mace:
                case RoMapWeaponType.TwoHandMace:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveHunterWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                    return 1;
                case RoMapWeaponType.Bow:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveAssassinWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.Axe:
                case RoMapWeaponType.Sword:
                case RoMapWeaponType.ShortSword:
                    return 1;
                case RoMapWeaponType.ShortSwordShortSword:
                case RoMapWeaponType.SwordSword:
                case RoMapWeaponType.AxeAxe:
                case RoMapWeaponType.ShortSwordSword:
                case RoMapWeaponType.ShortSwordAxe:
                case RoMapWeaponType.SwordAxe:
                case RoMapWeaponType.Katar:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveMonkWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.Rod:
                case RoMapWeaponType.TwoHandRod:
                case RoMapWeaponType.Mace:
                case RoMapWeaponType.TwoHandMace:
                    return 1;
                case RoMapWeaponType.Knuckle:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveSageWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                    return 1;
                case RoMapWeaponType.Rod:
                case RoMapWeaponType.TwoHandRod:
                case RoMapWeaponType.Book:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveAlchemistWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                    return 1;
                case RoMapWeaponType.TwoHandSword:
                case RoMapWeaponType.Sword:
                case RoMapWeaponType.Axe:
                case RoMapWeaponType.TwoHandAxe:
                case RoMapWeaponType.Mace:
                case RoMapWeaponType.TwoHandMace:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveBardWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                case RoMapWeaponType.Instrument:
                    return 1;
                case RoMapWeaponType.Bow:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveDancerWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.Whip:
                    return 1;
                case RoMapWeaponType.Bow:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveCrusader2WeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                case RoMapWeaponType.Sword:
                case RoMapWeaponType.TwoHandSword:
                case RoMapWeaponType.Axe:
                case RoMapWeaponType.TwoHandAxe:
                case RoMapWeaponType.Mace:
                    return 1;
                case RoMapWeaponType.Spear:
                case RoMapWeaponType.TwoHandSpear:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveSuperNoviceWeaponAction(byte sex, RoMapWeaponType weaponType) {
            if (sex == 0) {
                switch (weaponType) {
                    case RoMapWeaponType.Rod:
                    case RoMapWeaponType.TwoHandRod:
                    case RoMapWeaponType.Axe:
                    case RoMapWeaponType.TwoHandAxe:
                    case RoMapWeaponType.Mace:
                    case RoMapWeaponType.TwoHandMace:
                    case RoMapWeaponType.Sword:
                        return 1;
                    case RoMapWeaponType.ShortSword:
                        return 2;
                    default:
                        return 0;
                }
            }

            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                    return 1;
                case RoMapWeaponType.Rod:
                case RoMapWeaponType.TwoHandRod:
                case RoMapWeaponType.Axe:
                case RoMapWeaponType.TwoHandAxe:
                case RoMapWeaponType.Mace:
                case RoMapWeaponType.TwoHandMace:
                case RoMapWeaponType.Sword:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveNinjaWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.ShortSword:
                    return 1;
                case RoMapWeaponType.Shuriken:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int ResolveGunslingerWeaponAction(RoMapWeaponType weaponType) {
            switch (weaponType) {
                case RoMapWeaponType.None:
                case RoMapWeaponType.GunHandgun:
                case RoMapWeaponType.GunShotgun:
                    return 1;
                case RoMapWeaponType.GunGatling:
                case RoMapWeaponType.GunRifle:
                case RoMapWeaponType.GunGrenade:
                    return 2;
                default:
                    return 0;
            }
        }

        private static bool TryResolveExpandedWeaponType(int id, out RoMapWeaponType weaponType) {
            switch (id) {
                case 31:
                case 32:
                case 33:
                case 34:
                case 35:
                case 36:
                case 37:
                case 38:
                    weaponType = RoMapWeaponType.ShortSword;
                    return true;
                case 39:
                case 40:
                case 41:
                case 42:
                case 43:
                case 44:
                case 45:
                case 46:
                case 47:
                    weaponType = RoMapWeaponType.Sword;
                    return true;
                case 48:
                case 49:
                case 50:
                case 51:
                    weaponType = RoMapWeaponType.TwoHandSword;
                    return true;
                case 52:
                case 53:
                case 54:
                case 55:
                case 56:
                case 57:
                    weaponType = RoMapWeaponType.Spear;
                    return true;
                case 58:
                case 59:
                case 60:
                case 61:
                    weaponType = RoMapWeaponType.Axe;
                    return true;
                case 62:
                case 63:
                case 64:
                case 65:
                case 66:
                case 67:
                case 68:
                case 98:
                    weaponType = RoMapWeaponType.Mace;
                    return true;
                case 69:
                case 70:
                case 71:
                case 72:
                case 99:
                case 100:
                case 101:
                case 102:
                    weaponType = RoMapWeaponType.Rod;
                    return true;
                case 73:
                case 74:
                case 75:
                case 76:
                case 77:
                    weaponType = RoMapWeaponType.Bow;
                    return true;
                case 78:
                case 79:
                case 80:
                case 81:
                case 82:
                case 83:
                case 84:
                case 85:
                    weaponType = RoMapWeaponType.Knuckle;
                    return true;
                case 86:
                case 87:
                case 88:
                    weaponType = RoMapWeaponType.Whip;
                    return true;
                case 89:
                case 90:
                case 91:
                case 92:
                case 93:
                case 94:
                case 95:
                    weaponType = RoMapWeaponType.Book;
                    return true;
                case 96:
                case 97:
                    weaponType = RoMapWeaponType.TwoHandRod;
                    return true;
                default:
                    weaponType = RoMapWeaponType.None;
                    return false;
            }
        }

        private static bool IsOneOf(int value, params int[] values) {
            for (int i = 0; i < values.Length; i++) {
                if (value == values[i]) {
                    return true;
                }
            }

            return false;
        }

        private enum RoMapWeaponActionProfile {
            Unknown = 0,
            Novice = 1,
            Swordman = 2,
            Magician = 3,
            Archer = 4,
            Acolyte = 5,
            Merchant = 6,
            Thief = 7,
            Knight = 8,
            Priest = 9,
            Wizard = 10,
            Blacksmith = 11,
            Hunter = 12,
            Assassin = 13,
            Knight2 = 14,
            Crusader = 15,
            Monk = 16,
            Sage = 17,
            Rogue = 18,
            Alchemist = 19,
            Bard = 20,
            Dancer = 21,
            Crusader2 = 22,
            SuperNovice = 23,
            Ninja = 24,
            Gunslinger = 25,
            Linker = 26
        }
    }

    public readonly struct RoZcNotifyAct {
        public RoZcNotifyAct(
            uint sourceId,
            uint targetId,
            uint serverTick,
            int sourceMotionTime,
            int targetMotionTime,
            int damage,
            ushort hitCount,
            byte actionRaw,
            int damage2,
            bool isSpDamage
        ) {
            SourceId = sourceId;
            TargetId = targetId;
            ServerTick = serverTick;
            SourceMotionTime = sourceMotionTime;
            TargetMotionTime = targetMotionTime;
            Damage = damage;
            HitCount = hitCount;
            ActionRaw = actionRaw;
            Damage2 = damage2;
            IsSpDamage = isSpDamage;
        }

        public uint SourceId { get; }
        public uint TargetId { get; }
        public uint ServerTick { get; }
        public int SourceMotionTime { get; }
        public int TargetMotionTime { get; }
        public int Damage { get; }
        public ushort HitCount { get; }
        public byte ActionRaw { get; }
        public int Damage2 { get; }
        public bool IsSpDamage { get; }
        public RoMapActorActionType Action {
            get {
                return RoMapActorActionSemantics.TryGetKnown(ActionRaw, out RoMapActorActionType action)
                    ? action
                    : (RoMapActorActionType) ActionRaw;
            }
        }
    }

    public readonly struct RoZcEmotion {
        public RoZcEmotion(uint gid, byte emotionType) {
            Gid = gid;
            EmotionType = emotionType;
        }

        public uint Gid { get; }
        public byte EmotionType { get; }
    }

    public readonly struct RoZcMsgStateChange {
        public RoZcMsgStateChange(
            ushort statusId,
            uint aid,
            bool active,
            uint totalMilliseconds,
            uint remainingMilliseconds,
            int value1,
            int value2,
            int value3,
            ushort packetId = 0
        ) {
            PacketId = packetId;
            StatusId = statusId;
            Aid = aid;
            Active = active;
            TotalMilliseconds = totalMilliseconds;
            RemainingMilliseconds = remainingMilliseconds;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
        }

        public ushort StatusId { get; }
        public uint Aid { get; }
        public bool Active { get; }
        public uint TotalMilliseconds { get; }
        public uint RemainingMilliseconds { get; }
        public int Value1 { get; }
        public int Value2 { get; }
        public int Value3 { get; }

        /// <summary>
        /// The packet (0x0196 / 0x043f / 0x08ff / 0x0983 / 0x0984): the client reads 0x0196 with FUN_00c90ca0's tables and the
        /// others with FUN_00c767a0's, and takes 0x08ff / 0x0984 as "on" whatever their times (0xc8ddbb / 0xc859ac).
        /// </summary>
        public ushort PacketId { get; }
    }

    public readonly struct RoZcStateChange {
        public RoZcStateChange(
            uint aid,
            short bodyState,
            short healthState,
            int effectState,
            bool isPkModeOn,
            bool hasLongEffectState
        ) {
            Aid = aid;
            BodyState = bodyState;
            HealthState = healthState;
            EffectState = effectState;
            IsPkModeOn = isPkModeOn;
            HasLongEffectState = hasLongEffectState;
        }

        public uint Aid { get; }
        public short BodyState { get; }
        public short HealthState { get; }
        public int EffectState { get; }
        public bool IsPkModeOn { get; }
        public bool HasLongEffectState { get; }
    }

    public readonly struct RoZcSpriteChange {
        public RoZcSpriteChange(uint gid, byte typeRaw, uint value, bool packsWeaponAndShield) {
            Gid = gid;
            TypeRaw = typeRaw;
            Type = (RoMapActorSpriteChangeType) typeRaw;
            Value = value;
            PacksWeaponAndShield = packsWeaponAndShield;
        }

        public uint Gid { get; }
        public byte TypeRaw { get; }
        public RoMapActorSpriteChangeType Type { get; }
        public uint Value { get; }
        public bool PacksWeaponAndShield { get; }
    }

    public readonly struct RoZcItemEntry {
        public RoZcItemEntry(
            uint itemAid,
            uint itemId,
            int itemTypeRaw,
            bool isIdentified,
            ushort x,
            ushort y,
            byte subX,
            byte subY,
            short count,
            bool isFalling,
            bool showDropEffect,
            short dropEffectMode) {
            ItemAid = itemAid;
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            IsIdentified = isIdentified;
            X = x;
            Y = y;
            SubX = subX;
            SubY = subY;
            Count = count;
            IsFalling = isFalling;
            ShowDropEffect = showDropEffect;
            DropEffectMode = dropEffectMode;
        }

        public uint ItemAid { get; }
        public uint ItemId { get; }
        public int ItemTypeRaw { get; }
        public bool IsIdentified { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public byte SubX { get; }
        public byte SubY { get; }
        public short Count { get; }
        public bool IsFalling { get; }
        public bool ShowDropEffect { get; }
        public short DropEffectMode { get; }
    }

    public readonly struct RoZcItemDisappear {
        public RoZcItemDisappear(uint itemAid) {
            ItemAid = itemAid;
        }

        public uint ItemAid { get; }
    }

    public readonly struct RoZcNotifyVanish {
        public RoZcNotifyVanish(uint gid, byte typeRaw, RoMapActorVanishType type) {
            Gid = gid;
            TypeRaw = typeRaw;
            Type = type;
        }

        public uint Gid { get; }
        public byte TypeRaw { get; }
        public RoMapActorVanishType Type { get; }
    }

    public readonly struct RoZcResurrection {
        public RoZcResurrection(uint gid, ushort typeRaw) {
            Gid = gid;
            TypeRaw = typeRaw;
        }

        // Object id (GID) of the resurrected unit, as sent by ZC_RESURRECTION (rAthena bl->id).
        public uint Gid { get; }
        public ushort TypeRaw { get; }
    }

    public readonly struct RoZcNotifyMove {
        public RoZcNotifyMove(uint gid, RoPacketPositionPair movePath, uint moveStartTime) {
            Gid = gid;
            MovePath = movePath;
            MoveStartTime = moveStartTime;
        }

        public uint Gid { get; }
        public RoPacketPositionPair MovePath { get; }
        public uint MoveStartTime { get; }
    }

    public readonly struct RoZcNotifyPlayerMove {
        public RoZcNotifyPlayerMove(uint moveStartTime, RoPacketPositionPair movePath) {
            MoveStartTime = moveStartTime;
            MovePath = movePath;
        }

        public uint MoveStartTime { get; }
        public RoPacketPositionPair MovePath { get; }
    }

    public readonly struct RoZcStopMove {
        public RoZcStopMove(uint aid, ushort x, ushort y) {
            Aid = aid;
            X = x;
            Y = y;
        }

        public uint Aid { get; }
        public ushort X { get; }
        public ushort Y { get; }
    }

    public readonly struct RoZcActorIdentity {
        public RoZcActorIdentity(
            uint actorId,
            string name,
            string partyName = null,
            string guildName = null,
            string guildPositionName = null,
            string titleName = null,
            int titleId = 0,
            bool hasTitleId = false,
            int groupId = 0,
            bool hasGroupId = false) {
            ActorId = actorId;
            Name = name ?? string.Empty;
            PartyName = partyName ?? string.Empty;
            GuildName = guildName ?? string.Empty;
            GuildPositionName = guildPositionName ?? string.Empty;
            TitleName = titleName ?? string.Empty;
            TitleId = titleId;
            HasTitleId = hasTitleId;
            GroupId = groupId;
            HasGroupId = hasGroupId;
        }

        public uint ActorId { get; }
        public string Name { get; }
        public string PartyName { get; }
        public string GuildName { get; }
        public string GuildPositionName { get; }
        public string TitleName { get; }
        public int TitleId { get; }
        public bool HasTitleId { get; }
        public int GroupId { get; }
        public bool HasGroupId { get; }

        public bool TryParseHitPointsFromPartyInfo(out int hp, out int maxHp) {
            return TryParseHitPointsInfo(PartyName, out hp, out maxHp);
        }

        private static bool TryParseHitPointsInfo(string value, out int hp, out int maxHp) {
            hp = 0;
            maxHp = 0;
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            int hpLabelIndex = value.IndexOf("HP:", StringComparison.OrdinalIgnoreCase);
            if (hpLabelIndex < 0) {
                return false;
            }

            int index = hpLabelIndex + 3;
            SkipWhitespace(value, ref index);
            if (!TryReadNonNegativeInteger(value, ref index, out hp)) {
                return false;
            }

            SkipWhitespace(value, ref index);
            if (index >= value.Length || value[index] != '/') {
                return false;
            }

            index++;
            SkipWhitespace(value, ref index);
            return TryReadNonNegativeInteger(value, ref index, out maxHp) &&
                maxHp > 0 &&
                hp <= maxHp;
        }

        private static void SkipWhitespace(string value, ref int index) {
            while (index < value.Length && char.IsWhiteSpace(value[index])) {
                index++;
            }
        }

        private static bool TryReadNonNegativeInteger(string value, ref int index, out int result) {
            result = 0;
            int start = index;
            while (index < value.Length && char.IsDigit(value[index])) {
                int digit = value[index] - '0';
                if (result > (int.MaxValue - digit) / 10) {
                    return false;
                }

                result = (result * 10) + digit;
                index++;
            }

            return index > start;
        }
    }

    public readonly struct RoZcHpInfo {
        public RoZcHpInfo(uint actorId, int hp, int maxHp) {
            ActorId = actorId;
            Hp = hp;
            MaxHp = maxHp;
        }

        public uint ActorId { get; }
        public int Hp { get; }
        public int MaxHp { get; }
    }

    public readonly struct RoZcNotifyActorEntry {
        public RoZcNotifyActorEntry(
            RoMapActorEntryKind kind,
            sbyte objectTypeRaw,
            RoMapActorObjectType objectType,
            uint aid,
            uint gid,
            short speed,
            short bodyState,
            short healthState,
            int effectState,
            short job,
            ushort head,
            uint weapon,
            uint shield,
            ushort accessory,
            ushort accessory2,
            ushort accessory3,
            short headPalette,
            short bodyPalette,
            short headDirection,
            ushort robe,
            uint guildId,
            short guildEmblemVersion,
            short honor,
            int virtue,
            bool isPkModeOn,
            byte sex,
            bool hasMovePath,
            RoPacketPosition position,
            RoPacketPositionPair movePath,
            uint moveStartTime,
            byte xSize,
            byte ySize,
            byte stateRaw,
            RoMapActorSpawnState state,
            short clientLevel,
            short font,
            int maxHp,
            int hp,
            bool isBoss,
            ushort body,
            string name) {
            Kind = kind;
            ObjectTypeRaw = objectTypeRaw;
            ObjectType = objectType;
            Aid = aid;
            Gid = gid;
            Speed = speed;
            BodyState = bodyState;
            HealthState = healthState;
            EffectState = effectState;
            Job = job;
            Head = head;
            Weapon = weapon;
            Shield = shield;
            Accessory = accessory;
            Accessory2 = accessory2;
            Accessory3 = accessory3;
            HeadPalette = headPalette;
            BodyPalette = bodyPalette;
            HeadDirection = headDirection;
            Robe = robe;
            GuildId = guildId;
            GuildEmblemVersion = guildEmblemVersion;
            Honor = honor;
            Virtue = virtue;
            IsPkModeOn = isPkModeOn;
            Sex = sex;
            HasMovePath = hasMovePath;
            Position = position;
            MovePath = movePath;
            MoveStartTime = moveStartTime;
            XSize = xSize;
            YSize = ySize;
            StateRaw = stateRaw;
            State = state;
            ClientLevel = clientLevel;
            Font = font;
            MaxHp = maxHp;
            Hp = hp;
            IsBoss = isBoss;
            Body = body;
            Name = name ?? string.Empty;
        }

        public RoMapActorEntryKind Kind { get; }
        public sbyte ObjectTypeRaw { get; }
        public RoMapActorObjectType ObjectType { get; }
        public uint Aid { get; }
        public uint Gid { get; }
        public short Speed { get; }
        public short BodyState { get; }
        public short HealthState { get; }
        public int EffectState { get; }
        public short Job { get; }
        public ushort Head { get; }
        public uint Weapon { get; }
        public uint Shield { get; }
        public ushort Accessory { get; }
        public ushort Accessory2 { get; }
        public ushort Accessory3 { get; }
        public short HeadPalette { get; }
        public short BodyPalette { get; }
        public short HeadDirection { get; }
        public ushort Robe { get; }
        public uint GuildId { get; }
        public short GuildEmblemVersion { get; }
        public short Honor { get; }
        public int Virtue { get; }
        public bool IsPkModeOn { get; }
        public byte Sex { get; }
        public bool HasMovePath { get; }
        public RoPacketPosition Position { get; }
        public RoPacketPositionPair MovePath { get; }
        public uint MoveStartTime { get; }
        public byte XSize { get; }
        public byte YSize { get; }
        public byte StateRaw { get; }
        public RoMapActorSpawnState State { get; }
        public short ClientLevel { get; }
        public short Font { get; }
        public int MaxHp { get; }
        public int Hp { get; }
        public bool IsBoss { get; }
        public ushort Body { get; }
        public string Name { get; }
    }

    public readonly struct RoZcAcceptEnter2 {
        public RoZcAcceptEnter2(int startTime, RoPacketPosition position, byte xSize, byte ySize, ushort font) {
            StartTime = startTime;
            Position = position;
            XSize = xSize;
            YSize = ySize;
            Font = font;
        }

        public int StartTime { get; }
        public RoPacketPosition Position { get; }
        public byte XSize { get; }
        public byte YSize { get; }
        public ushort Font { get; }
    }

    public enum RoInventoryType : byte {
        Inventory = 0,
        Cart = 1,
        Storage = 2,
        GuildStorage = 3
    }

    public readonly struct RoZcInventoryStart {
        public RoZcInventoryStart(byte inventoryTypeRaw, string name) {
            InventoryTypeRaw = inventoryTypeRaw;
            InventoryType = (RoInventoryType) inventoryTypeRaw;
            Name = name ?? string.Empty;
        }

        public byte InventoryTypeRaw { get; }
        public RoInventoryType InventoryType { get; }
        public string Name { get; }
    }

    public readonly struct RoZcInventoryEnd {
        public RoZcInventoryEnd(byte inventoryTypeRaw, byte flag) {
            InventoryTypeRaw = inventoryTypeRaw;
            InventoryType = (RoInventoryType) inventoryTypeRaw;
            Flag = flag;
        }

        public byte InventoryTypeRaw { get; }
        public RoInventoryType InventoryType { get; }
        public byte Flag { get; }
    }

    public readonly struct RoZcInventoryItem {
        public RoZcInventoryItem(
            short bagIndex,
            uint itemId,
            byte itemTypeRaw,
            short count,
            uint equipLocationRaw,
            uint wearStateRaw,
            byte refineLevel,
            int[] cardIds,
            ushort viewIdHint,
            byte flag,
            bool isEquipment,
            byte enchantGrade = 0,
            RoPacketItemRandomOption[] randomOptions = null) {
            BagIndex = bagIndex;
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            Count = count;
            EquipLocationRaw = equipLocationRaw;
            WearStateRaw = wearStateRaw;
            RefineLevel = refineLevel;
            CardIds = CopyCardIds(cardIds);
            ViewIdHint = viewIdHint;
            Flag = flag;
            IsEquipment = isEquipment;
            EnchantGrade = enchantGrade;
            RandomOptions = CopyRandomOptions(randomOptions);
        }

        public short BagIndex { get; }
        public uint ItemId { get; }
        public byte ItemTypeRaw { get; }
        public short Count { get; }
        public uint EquipLocationRaw { get; }
        public uint WearStateRaw { get; }
        public byte RefineLevel { get; }
        public int[] CardIds { get; }
        public ushort ViewIdHint { get; }
        public byte Flag { get; }
        public bool IsEquipment { get; }
        public byte EnchantGrade { get; }
        public bool IsIdentified => (Flag & 0x01) != 0;
        public bool IsDamaged => (Flag & 0x02) != 0;
        public RoPacketItemRandomOption[] RandomOptions { get; }

        public int GetCardId(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= 4) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "RO item packet records expose exactly four card/socket fields.");
            }

            return CardIds[slotIndex];
        }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[4];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }

        private static RoPacketItemRandomOption[] CopyRandomOptions(RoPacketItemRandomOption[] source) {
            var copy = new RoPacketItemRandomOption[5];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoZcInventoryItemList {
        public RoZcInventoryItemList(byte inventoryTypeRaw, bool isEquipmentList, RoZcInventoryItem[] items, string name = "") {
            InventoryTypeRaw = inventoryTypeRaw;
            InventoryType = (RoInventoryType) inventoryTypeRaw;
            IsEquipmentList = isEquipmentList;
            Items = items ?? Array.Empty<RoZcInventoryItem>();
            Name = name ?? string.Empty;
        }

        public byte InventoryTypeRaw { get; }
        public RoInventoryType InventoryType { get; }
        public bool IsEquipmentList { get; }
        public RoZcInventoryItem[] Items { get; }
        public string Name { get; }
    }

    public readonly struct RoZcEquipWinMicroscope {
        public RoZcEquipWinMicroscope(
            string characterName,
            short job,
            short head,
            short headBottom,
            short headTop,
            short headMid,
            short robe,
            short headPalette,
            short bodyPalette,
            short bodyStyle,
            byte sex,
            RoZcInventoryItem[] items) {
            CharacterName = characterName ?? string.Empty;
            Job = job;
            Head = head;
            HeadBottom = headBottom;
            HeadTop = headTop;
            HeadMid = headMid;
            Robe = robe;
            HeadPalette = headPalette;
            BodyPalette = bodyPalette;
            BodyStyle = bodyStyle;
            Sex = sex;
            Items = items ?? Array.Empty<RoZcInventoryItem>();
        }

        public string CharacterName { get; }
        public short Job { get; }
        public short Head { get; }
        public short HeadBottom { get; }
        public short HeadTop { get; }
        public short HeadMid { get; }
        public short Robe { get; }
        public short HeadPalette { get; }
        public short BodyPalette { get; }
        public short BodyStyle { get; }
        public byte Sex { get; }
        public RoZcInventoryItem[] Items { get; }
    }

    /// <summary>
    /// ZC_NAVIGATION_ACTIVE 0x08e2 (27 bytes): type (0 cell, 1 map, 3 monster), the services as decimal digits (hundreds
    /// Kafra, tens scroll, units airship), hideWindow, map[16], x, y, mobId (Hercules clif_navigate_to).
    /// </summary>
    public readonly struct RoZcNavigationActive {
        public RoZcNavigationActive(byte type, byte flag, byte hideWindow, string map, short x, short y, short mobId) {
            Type = type;
            Flag = flag;
            HideWindow = hideWindow;
            Map = map ?? string.Empty;
            X = x;
            Y = y;
            MobId = mobId;
        }

        public byte Type { get; }
        public byte Flag { get; }
        public byte HideWindow { get; }
        public string Map { get; }
        public short X { get; }
        public short Y { get; }
        public short MobId { get; }
    }

    /// <summary>ZC_BATTLEFIELD_CHAT 0x02dc: the speaker's AID @4, name[24] @8 (unused by the client), the text from @32 ("Name : text").</summary>
    public readonly struct RoZcBattlefieldChat {
        public RoZcBattlefieldChat(uint accountId, string name, string message) {
            AccountId = accountId;
            Name = name ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public uint AccountId { get; }
        public string Name { get; }
        public string Message { get; }
    }

    /// <summary>A battleground entry-queue packet (0x08d8 / 08d9 / 08db / 08dc / 08de / 08df / 08e1 / 090e) as received; the queue UI reads it.</summary>
    public readonly struct RoZcEntryQueue {
        public RoZcEntryQueue(ushort packetId, byte[] bytes) {
            PacketId = packetId;
            Bytes = bytes ?? Array.Empty<byte>();
        }

        public ushort PacketId { get; }
        public byte[] Bytes { get; }
    }

    public readonly struct RoZcEquipSwitchListEntry {
        public RoZcEquipSwitchListEntry(short bagIndex, uint positionRaw) {
            BagIndex = bagIndex;
            PositionRaw = positionRaw;
        }

        public short BagIndex { get; }
        public uint PositionRaw { get; }
    }

    public readonly struct RoZcEquipSwitchList {
        public RoZcEquipSwitchList(RoZcEquipSwitchListEntry[] entries) {
            Entries = entries ?? Array.Empty<RoZcEquipSwitchListEntry>();
        }

        public RoZcEquipSwitchListEntry[] Entries { get; }
    }

    public readonly struct RoZcWearEquipmentAck {
        public RoZcWearEquipmentAck(short bagIndex, uint wearLocationRaw, ushort viewId, byte result) {
            BagIndex = bagIndex;
            WearLocationRaw = wearLocationRaw;
            ViewId = viewId;
            Result = result;
        }

        public short BagIndex { get; }
        public uint WearLocationRaw { get; }
        public ushort ViewId { get; }
        public byte Result { get; }
        public bool IsSuccess => Result == 0;
    }

    public readonly struct RoZcTakeoffEquipmentAck {
        public RoZcTakeoffEquipmentAck(short bagIndex, uint wearLocationRaw, byte result) {
            BagIndex = bagIndex;
            WearLocationRaw = wearLocationRaw;
            Result = result;
        }

        public short BagIndex { get; }
        public uint WearLocationRaw { get; }
        public byte Result { get; }
        public bool IsSuccess => Result == 0;
    }

    public readonly struct RoZcEquipArrow {
        public RoZcEquipArrow(int bagIndex) {
            BagIndex = bagIndex;
        }

        public int BagIndex { get; }
    }

    public readonly struct RoZcItemThrowAck {
        public RoZcItemThrowAck(short bagIndex, ushort count) {
            BagIndex = bagIndex;
            Count = count;
        }

        public short BagIndex { get; }
        public ushort Count { get; }
        public bool IsSuccess => Count > 0;
    }

    public readonly struct RoZcUseItemAck {
        public RoZcUseItemAck(ushort bagIndex, uint itemId, uint actorId, short count, byte result) {
            BagIndex = bagIndex;
            ItemId = itemId;
            ActorId = actorId;
            Count = count;
            Result = result;
        }

        public ushort BagIndex { get; }
        public uint ItemId { get; }
        public uint ActorId { get; }
        public short Count { get; }
        public byte Result { get; }
        public bool IsSuccess => Result != 0;
    }

    public readonly struct RoZcItemCompositionList {
        public RoZcItemCompositionList(int[] equipIndexes) {
            EquipIndexes = Copy(equipIndexes);
        }

        public int[] EquipIndexes { get; }

        private static int[] Copy(int[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<int>();
            }

            var copy = new int[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    public readonly struct RoZcItemIdentifyList {
        public RoZcItemIdentifyList(int[] bagIndexes) {
            BagIndexes = Copy(bagIndexes);
        }

        public int[] BagIndexes { get; }

        private static int[] Copy(int[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<int>();
            }

            var copy = new int[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    public readonly struct RoZcMakingArrowList {
        public RoZcMakingArrowList(uint[] itemIds) {
            ItemIds = Copy(itemIds);
        }

        public uint[] ItemIds { get; }

        private static uint[] Copy(uint[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<uint>();
            }

            var copy = new uint[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    public readonly struct RoZcSelectCart {
        public RoZcSelectCart(uint identity, int[] cartTypes) {
            Identity = identity;
            CartTypes = Copy(cartTypes);
        }

        public uint Identity { get; }
        public int[] CartTypes { get; }

        private static int[] Copy(int[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<int>();
            }

            var copy = new int[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    public readonly struct RoZcWarpList {
        public RoZcWarpList(ushort skillId, string[] mapNames) {
            SkillId = skillId;
            MapNames = CopyNonEmpty(mapNames);
        }

        public ushort SkillId { get; }
        public string[] MapNames { get; }

        private static string[] CopyNonEmpty(string[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<string>();
            }

            var copy = new List<string>(source.Length);
            for (int i = 0; i < source.Length; i++) {
                string value = source[i];
                if (!string.IsNullOrWhiteSpace(value)) {
                    copy.Add(value.Trim());
                }
            }

            return copy.Count == 0 ? Array.Empty<string>() : copy.ToArray();
        }
    }

    public readonly struct RoZcNotifyMapInfo {
        public RoZcNotifyMapInfo(short type) {
            Type = type;
        }

        public short Type { get; }
        public bool HasTeleportMessage => Type == 0 || Type == 1;
    }

    public readonly struct RoZcItemCompositionAck {
        public RoZcItemCompositionAck(ushort equipIndex, ushort cardIndex, byte result) {
            EquipIndex = equipIndex;
            CardIndex = cardIndex;
            Result = result;
        }

        public ushort EquipIndex { get; }
        public ushort CardIndex { get; }
        public byte Result { get; }
        public bool IsSuccess => Result == 0;
    }

    public readonly struct RoZcItemIdentifyAck {
        public RoZcItemIdentifyAck(short bagIndex, byte result) {
            BagIndex = bagIndex;
            Result = result;
        }

        public short BagIndex { get; }
        public byte Result { get; }
        public bool IsSuccess => Result == 0 && BagIndex >= 0;
    }

    public readonly struct RoZcItemPickupAck {
        public RoZcItemPickupAck(
            short bagIndex,
            ushort count,
            uint itemId,
            bool isIdentified,
            bool isDamaged,
            byte refineLevel,
            int[] cardIds,
            uint equipLocationRaw,
            byte itemTypeRaw,
            byte result,
            int hireExpireDate,
            ushort bindOnEquipType,
            byte favorite,
            ushort viewIdHint) {
            BagIndex = bagIndex;
            Count = count;
            ItemId = itemId;
            IsIdentified = isIdentified;
            IsDamaged = isDamaged;
            RefineLevel = refineLevel;
            CardIds = CopyCardIds(cardIds);
            EquipLocationRaw = equipLocationRaw;
            ItemTypeRaw = itemTypeRaw;
            Result = result;
            HireExpireDate = hireExpireDate;
            BindOnEquipType = bindOnEquipType;
            Favorite = favorite;
            ViewIdHint = viewIdHint;
        }

        public short BagIndex { get; }
        public ushort Count { get; }
        public uint ItemId { get; }
        public bool IsIdentified { get; }
        public bool IsDamaged { get; }
        public byte RefineLevel { get; }
        public int[] CardIds { get; }
        public uint EquipLocationRaw { get; }
        public byte ItemTypeRaw { get; }
        public byte Result { get; }
        public int HireExpireDate { get; }
        public ushort BindOnEquipType { get; }
        public byte Favorite { get; }
        public ushort ViewIdHint { get; }
        public bool IsSuccess => Result == 0;

        public int GetCardId(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= 4) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "RO pickup ack records expose exactly four card/socket fields.");
            }

            return CardIds[slotIndex];
        }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[4];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public enum RoPlayerStatusProperty : ushort {
        Speed = 0x00,
        BaseExperience = 0x01,
        JobExperience = 0x02,
        Hp = 0x05,
        MaxHp = 0x06,
        Sp = 0x07,
        MaxSp = 0x08,
        StatusPoints = 0x09,
        BaseLevel = 0x0b,
        SkillPoints = 0x0c,
        Str = 0x0d,
        Agi = 0x0e,
        Vit = 0x0f,
        Int = 0x10,
        Dex = 0x11,
        Luk = 0x12,
        Job = 0x13,
        Zeny = 0x14,
        NextBaseExperience = 0x16,
        NextJobExperience = 0x17,
        Weight = 0x18,
        MaxWeight = 0x19,
        NeedStr = 0x20,
        NeedAgi = 0x21,
        NeedVit = 0x22,
        NeedInt = 0x23,
        NeedDex = 0x24,
        NeedLuk = 0x25,
        Attack = 0x29,
        RefiningPower = 0x2a,
        MagicAttackMax = 0x2b,
        MagicAttackMin = 0x2c,
        Defense = 0x2d,
        PlusDefense = 0x2e,
        MagicDefense = 0x2f,
        PlusMagicDefense = 0x30,
        Hit = 0x31,
        Flee = 0x32,
        PlusFlee = 0x33,
        Critical = 0x34,
        AttackSpeed = 0x35,
        PlusAttackSpeed = 0x36,
        JobLevel = 0x37
    }

    public readonly struct RoZcCoupleStatus {
        public RoZcCoupleStatus(ushort statusTypeRaw, int defaultStatus, int plusStatus) {
            StatusTypeRaw = statusTypeRaw;
            Property = (RoPlayerStatusProperty) statusTypeRaw;
            DefaultStatus = defaultStatus;
            PlusStatus = plusStatus;
        }

        public ushort StatusTypeRaw { get; }
        public RoPlayerStatusProperty Property { get; }
        public int DefaultStatus { get; }
        public int PlusStatus { get; }
    }

    public readonly struct RoZcParameterChange {
        public RoZcParameterChange(ushort propertyRaw, long value) {
            PropertyRaw = propertyRaw;
            Property = (RoPlayerStatusProperty) propertyRaw;
            Value = value;
        }

        public ushort PropertyRaw { get; }
        public RoPlayerStatusProperty Property { get; }
        public long Value { get; }
    }

    public readonly struct RoZcRecovery2 {
        public RoZcRecovery2(ushort propertyRaw, uint amount) {
            PropertyRaw = propertyRaw;
            Property = (RoPlayerStatusProperty) propertyRaw;
            Amount = amount;
        }

        public ushort PropertyRaw { get; }
        public RoPlayerStatusProperty Property { get; }
        public uint Amount { get; }
    }

    public readonly struct RoZcStatusChangeAck {
        public RoZcStatusChangeAck(ushort statusId, byte result, byte value) {
            StatusId = statusId;
            Result = result;
            Value = value;
        }

        public ushort StatusId { get; }
        public byte Result { get; }
        public byte Value { get; }
    }

    public readonly struct RoZcStatusSnapshot {
        public RoZcStatusSnapshot(
            short statusPoints,
            byte str,
            byte needStr,
            byte agi,
            byte needAgi,
            byte vit,
            byte needVit,
            byte intelligence,
            byte needInt,
            byte dex,
            byte needDex,
            byte luk,
            byte needLuk,
            ushort attack,
            ushort refiningPower,
            ushort magicAttackMax,
            ushort magicAttackMin,
            ushort defense,
            ushort plusDefense,
            ushort magicDefense,
            ushort plusMagicDefense,
            ushort hit,
            ushort flee,
            ushort plusFlee,
            ushort critical,
            ushort attackSpeed,
            ushort plusAttackSpeed) {
            StatusPoints = statusPoints;
            Str = str;
            NeedStr = needStr;
            Agi = agi;
            NeedAgi = needAgi;
            Vit = vit;
            NeedVit = needVit;
            Int = intelligence;
            NeedInt = needInt;
            Dex = dex;
            NeedDex = needDex;
            Luk = luk;
            NeedLuk = needLuk;
            Attack = attack;
            RefiningPower = refiningPower;
            MagicAttackMax = magicAttackMax;
            MagicAttackMin = magicAttackMin;
            Defense = defense;
            PlusDefense = plusDefense;
            MagicDefense = magicDefense;
            PlusMagicDefense = plusMagicDefense;
            Hit = hit;
            Flee = flee;
            PlusFlee = plusFlee;
            Critical = critical;
            AttackSpeed = attackSpeed;
            PlusAttackSpeed = plusAttackSpeed;
        }

        public short StatusPoints { get; }
        public byte Str { get; }
        public byte NeedStr { get; }
        public byte Agi { get; }
        public byte NeedAgi { get; }
        public byte Vit { get; }
        public byte NeedVit { get; }
        public byte Int { get; }
        public byte NeedInt { get; }
        public byte Dex { get; }
        public byte NeedDex { get; }
        public byte Luk { get; }
        public byte NeedLuk { get; }
        public ushort Attack { get; }
        public ushort RefiningPower { get; }
        public ushort MagicAttackMax { get; }
        public ushort MagicAttackMin { get; }
        public ushort Defense { get; }
        public ushort PlusDefense { get; }
        public ushort MagicDefense { get; }
        public ushort PlusMagicDefense { get; }
        public ushort Hit { get; }
        public ushort Flee { get; }
        public ushort PlusFlee { get; }
        public ushort Critical { get; }
        public ushort AttackSpeed { get; }
        public ushort PlusAttackSpeed { get; }
    }

    public readonly struct RoZcStatusPropertyChange {
        public RoZcStatusPropertyChange(ushort propertyRaw, byte value) {
            PropertyRaw = propertyRaw;
            Property = (RoPlayerStatusProperty) propertyRaw;
            Value = value;
        }

        public ushort PropertyRaw { get; }
        public RoPlayerStatusProperty Property { get; }
        public byte Value { get; }
    }

    public readonly struct RoZcNotifyWeightPercentage {
        public RoZcNotifyWeightPercentage(int weightPercentage) {
            WeightPercentage = weightPercentage;
        }

        public int WeightPercentage { get; }
    }

    /// <summary>ZC_NOTIFY_EFFECT2 (0x01f3) / ZC_NOTIFY_EFFECT3 (0x0284): a visual effect by id on the actor.</summary>
    public readonly struct RoZcNotifyEffectById {
        public RoZcNotifyEffectById(uint actorId, int effectId, int value, bool hasValue) {
            ActorId = actorId;
            EffectId = effectId;
            Value = value;
            HasValue = hasValue;
        }

        public uint ActorId { get; }
        public int EffectId { get; }
        /// <summary>ZC_NOTIFY_EFFECT3's number (0x0284 only).</summary>
        public int Value { get; }
        public bool HasValue { get; }
    }

    /// <summary>ZC_NPC_SHOWEFST_UPDATE (0x028a, Hercules clif_changeoption2): option, level and opt3 of an actor.</summary>
    public readonly struct RoZcOptionChange2 {
        public RoZcOptionChange2(uint actorId, int option, int level, int opt3) {
            ActorId = actorId;
            Option = option;
            Level = level;
            Opt3 = opt3;
        }

        public uint ActorId { get; }
        public int Option { get; }
        public int Level { get; }
        public int Opt3 { get; }
    }

    /// <summary>ZC_DISPEL (0x01b9): the actor's cast was interrupted.</summary>
    public readonly struct RoZcDispel {
        public RoZcDispel(uint actorId) {
            ActorId = actorId;
        }

        public uint ActorId { get; }
    }

    /// <summary>ZC_SPIRITS (0x01d0) / ZC_SPIRITS2 (0x01e1): how many spirit spheres (or coins) the actor holds now.</summary>
    public readonly struct RoZcSpirits {
        public RoZcSpirits(uint actorId, int count) {
            ActorId = actorId;
            Count = count;
        }

        public uint ActorId { get; }
        public int Count { get; }
    }

    public readonly struct RoZcAttackRange {
        public RoZcAttackRange(short currentAttackRange) {
            CurrentAttackRange = currentAttackRange;
        }

        public short CurrentAttackRange { get; }
    }

    public readonly struct RoZcQuestObjective {
        public RoZcQuestObjective(
            uint huntIdent,
            uint huntIdent2,
            uint mobType,
            uint mobId,
            short levelMin,
            short levelMax,
            int current,
            int total,
            string mobName) {
            HuntIdent = huntIdent;
            HuntIdent2 = huntIdent2;
            MobType = mobType;
            MobId = mobId;
            LevelMin = levelMin;
            LevelMax = levelMax;
            Current = current;
            Total = total;
            MobName = mobName ?? string.Empty;
        }

        public uint HuntIdent { get; }
        public uint HuntIdent2 { get; }
        public uint MobType { get; }
        public uint MobId { get; }
        public short LevelMin { get; }
        public short LevelMax { get; }
        public int Current { get; }
        public int Total { get; }
        public string MobName { get; }
    }

    public readonly struct RoZcQuestEntry {
        public RoZcQuestEntry(uint questId, byte activeRaw, int serverTime, int endTime, RoZcQuestObjective[] objectives) {
            QuestId = questId;
            ActiveRaw = activeRaw;
            ServerTime = serverTime;
            EndTime = endTime;
            Objectives = Copy(objectives);
        }

        public uint QuestId { get; }
        public byte ActiveRaw { get; }
        public int ServerTime { get; }
        public int EndTime { get; }
        public RoZcQuestObjective[] Objectives { get; }

        private static RoZcQuestObjective[] Copy(RoZcQuestObjective[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoZcQuestObjective>();
            }

            var copy = new RoZcQuestObjective[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    public readonly struct RoZcQuestList {
        public RoZcQuestList(RoZcQuestEntry[] entries) {
            Entries = entries ?? Array.Empty<RoZcQuestEntry>();
        }

        public RoZcQuestEntry[] Entries { get; }
    }

    public readonly struct RoZcQuestAdd {
        public RoZcQuestAdd(RoZcQuestEntry entry) {
            Entry = entry;
        }

        public RoZcQuestEntry Entry { get; }
    }

    public readonly struct RoZcQuestDelete {
        public RoZcQuestDelete(uint questId) {
            QuestId = questId;
        }

        public uint QuestId { get; }
    }

    public readonly struct RoZcQuestActive {
        public RoZcQuestActive(uint questId, byte activeRaw) {
            QuestId = questId;
            ActiveRaw = activeRaw;
        }

        public uint QuestId { get; }
        public byte ActiveRaw { get; }
    }

    public readonly struct RoZcQuestObjectiveCounter {
        public RoZcQuestObjectiveCounter(uint questId, uint huntIdent, uint huntIdent2, uint mobId, int current, int total) {
            QuestId = questId;
            HuntIdent = huntIdent;
            HuntIdent2 = huntIdent2;
            MobId = mobId;
            Current = current;
            Total = total;
        }

        public uint QuestId { get; }
        public uint HuntIdent { get; }
        public uint HuntIdent2 { get; }
        public uint MobId { get; }
        public int Current { get; }
        public int Total { get; }
    }

    public readonly struct RoZcQuestObjectiveUpdate {
        public RoZcQuestObjectiveUpdate(RoZcQuestObjectiveCounter[] updates) {
            Updates = updates ?? Array.Empty<RoZcQuestObjectiveCounter>();
        }

        public RoZcQuestObjectiveCounter[] Updates { get; }
    }

    public readonly struct RoZcPartyOption {
        public RoZcPartyOption(uint expOption, byte itemPickupRule, byte itemDivisionRule)
            : this(expOption, itemPickupRule, itemDivisionRule, true) {
        }

        public RoZcPartyOption(uint expOption, byte itemPickupRule, byte itemDivisionRule, bool hasItemRules) {
            ExpOption = expOption;
            ItemPickupRule = itemPickupRule;
            ItemDivisionRule = itemDivisionRule;
            HasItemRules = hasItemRules;
        }

        public uint ExpOption { get; }
        public byte ItemPickupRule { get; }
        public byte ItemDivisionRule { get; }
        public bool HasItemRules { get; }
    }

    public readonly struct RoZcPartyCreateAck {
        public RoZcPartyCreateAck(byte result) {
            Result = result;
        }

        public byte Result { get; }
        public bool Accepted => Result == 0;
    }

    public readonly struct RoZcPartyConfig {
        public RoZcPartyConfig(bool refuseJoinMessages) {
            RefuseJoinMessages = refuseJoinMessages;
        }

        public bool RefuseJoinMessages { get; }
    }

    public readonly struct RoZcPartyLeaderChanged {
        public RoZcPartyLeaderChanged(uint oldMasterAccountId, uint newMasterAccountId) {
            OldMasterAccountId = oldMasterAccountId;
            NewMasterAccountId = newMasterAccountId;
        }

        public uint OldMasterAccountId { get; }
        public uint NewMasterAccountId { get; }
    }

    public readonly struct RoZcPartyMember {
        public RoZcPartyMember(uint accountId, uint characterId, string name, string mapName, byte role, byte state, int classId, int baseLevel, int x, int y) {
            AccountId = accountId;
            CharacterId = characterId;
            Name = name ?? string.Empty;
            MapName = mapName ?? string.Empty;
            Role = role;
            State = state;
            ClassId = classId;
            BaseLevel = baseLevel;
            X = x;
            Y = y;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
        public string Name { get; }
        public string MapName { get; }
        public byte Role { get; }
        public byte State { get; }
        public int ClassId { get; }
        public int BaseLevel { get; }
        public int X { get; }
        public int Y { get; }
    }

    public readonly struct RoZcPartyInfo {
        public RoZcPartyInfo(string partyName, RoZcPartyMember[] members) {
            PartyName = partyName ?? string.Empty;
            Members = members ?? Array.Empty<RoZcPartyMember>();
        }

        public string PartyName { get; }
        public RoZcPartyMember[] Members { get; }
    }

    public readonly struct RoZcPartyMemberInfo {
        public RoZcPartyMemberInfo(string partyName, RoZcPartyMember member, bool hasItemRules, byte itemPickupRule, byte itemDivisionRule) {
            PartyName = partyName ?? string.Empty;
            Member = member;
            HasItemRules = hasItemRules;
            ItemPickupRule = itemPickupRule;
            ItemDivisionRule = itemDivisionRule;
        }

        public string PartyName { get; }
        public RoZcPartyMember Member { get; }
        public bool HasItemRules { get; }
        public byte ItemPickupRule { get; }
        public byte ItemDivisionRule { get; }
    }

    public readonly struct RoZcPartyMemberLeave {
        public RoZcPartyMemberLeave(uint accountId, string name, byte result) {
            AccountId = accountId;
            Name = name ?? string.Empty;
            Result = result;
        }

        public uint AccountId { get; }
        public string Name { get; }
        public byte Result { get; }
    }

    public readonly struct RoZcPartyPosition {
        public RoZcPartyPosition(uint accountId, int x, int y) {
            AccountId = accountId;
            X = x;
            Y = y;
        }

        public uint AccountId { get; }
        public int X { get; }
        public int Y { get; }
    }

    /// <summary>ZC_NOTIFY_POSITION_TO_GUILDM 0x01eb: a guildmate on this map (x / y −1 on removal).</summary>
    public readonly struct RoZcGuildPosition {
        public RoZcGuildPosition(uint accountId, int x, int y) {
            AccountId = accountId;
            X = x;
            Y = y;
        }

        public uint AccountId { get; }
        public int X { get; }
        public int Y { get; }
    }

    /// <summary>ZC_QUEST_NOTIFY_EFFECT 0x0446: a quest NPC's minimap mark (state 9999 removes it; colour 0 draws nothing).</summary>
    public readonly struct RoZcQuestNotifyEffect {
        public RoZcQuestNotifyEffect(uint actorId, int x, int y, int state, int color) {
            ActorId = actorId;
            X = x;
            Y = y;
            State = state;
            Color = color;
        }

        public uint ActorId { get; }
        public int X { get; }
        public int Y { get; }
        public int State { get; }
        public int Color { get; }
    }

    /// <summary>ZC_BOSS_INFO 0x0293: type 0 none, 1 alive, 2 alive (first sighting), 3 dead; x / y for 1 and 2.</summary>
    public readonly struct RoZcBossInfo {
        public RoZcBossInfo(byte type, int x, int y, string name) {
            Type = type;
            X = x;
            Y = y;
            Name = name;
        }

        public byte Type { get; }
        public int X { get; }
        public int Y { get; }
        public string Name { get; }
    }

    /// <summary>0x09c1: a red square on the minimap for 120 draws (key i32 @2, x / y i16 @6 / @8).</summary>
    public readonly struct RoZcMinimapPing {
        public RoZcMinimapPing(int key, int x, int y) {
            Key = key;
            X = x;
            Y = y;
        }

        public int Key { get; }
        public int X { get; }
        public int Y { get; }
    }

    public readonly struct RoZcPartyHp {
        public RoZcPartyHp(uint accountId, int hp, int maxHp) {
            AccountId = accountId;
            Hp = hp;
            MaxHp = maxHp;
        }

        public uint AccountId { get; }
        public int Hp { get; }
        public int MaxHp { get; }
    }

    public readonly struct RoZcPartyAlive {
        public RoZcPartyAlive(uint accountId, bool isDead) {
            AccountId = accountId;
            IsDead = isDead;
        }

        public uint AccountId { get; }
        public bool IsDead { get; }
    }

    public readonly struct RoZcPartyJobAndLevel {
        public RoZcPartyJobAndLevel(uint accountId, int classId, int baseLevel) {
            AccountId = accountId;
            ClassId = classId;
            BaseLevel = baseLevel;
        }

        public uint AccountId { get; }
        public int ClassId { get; }
        public int BaseLevel { get; }
    }

    public readonly struct RoZcPartyInvite {
        public RoZcPartyInvite(uint partyId, string partyName) {
            PartyId = partyId;
            PartyName = partyName ?? string.Empty;
        }

        public uint PartyId { get; }
        public string PartyName { get; }
    }

    public readonly struct RoZcPartyInviteAck {
        public RoZcPartyInviteAck(string characterName, int result) {
            CharacterName = characterName ?? string.Empty;
            Result = result;
        }

        public string CharacterName { get; }
        public int Result { get; }
    }

    public readonly struct RoZcFriendEntry {
        public RoZcFriendEntry(uint accountId, uint characterId, string name, byte stateRaw) {
            AccountId = accountId;
            CharacterId = characterId;
            Name = name ?? string.Empty;
            StateRaw = stateRaw;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
        public string Name { get; }
        public byte StateRaw { get; }
    }

    public readonly struct RoZcFriendsList {
        public RoZcFriendsList(RoZcFriendEntry[] friends) {
            Friends = friends ?? Array.Empty<RoZcFriendEntry>();
        }

        public RoZcFriendEntry[] Friends { get; }
    }

    /// <summary>One repairable item of ZC_REPAIRITEMLIST (the target's zero-based inventory slot, as the server sent it).</summary>
    public readonly struct RoRepairItem {
        public RoRepairItem(short index, uint itemId, byte refine, uint[] cards) {
            Index = index;
            ItemId = itemId;
            Refine = refine;
            Cards = cards ?? new uint[4];
        }

        public short Index { get; }
        public uint ItemId { get; }
        public byte Refine { get; }
        public uint[] Cards { get; }
    }

    public readonly struct RoZcRepairItemList {
        public RoZcRepairItemList(RoRepairItem[] items) {
            Items = items ?? Array.Empty<RoRepairItem>();
        }

        public RoRepairItem[] Items { get; }
    }

    public readonly struct RoZcAckWeaponRefine {
        public RoZcAckWeaponRefine(int result, uint itemId) {
            Result = result;
            ItemId = itemId;
        }

        /// <summary>0 / 1 refined, 2 skill level too low, 3 the material (ItemId) is missing.</summary>
        public int Result { get; }
        public uint ItemId { get; }
    }

    public readonly struct RoZcPetEggList {
        public RoZcPetEggList(short[] bagIndexes) {
            BagIndexes = bagIndexes ?? Array.Empty<short>();
        }

        /// <summary>The eggs offered by the incubator, inventory index + 2 as the server sent them.</summary>
        public short[] BagIndexes { get; }
    }

    public readonly struct RoZcMakableItemList {
        public RoZcMakableItemList(uint[] itemIds) {
            ItemIds = itemIds ?? Array.Empty<uint>();
        }

        /// <summary>The makable items (each record's material ids are zero from Hercules and unused by the client list).</summary>
        public uint[] ItemIds { get; }
    }

    public readonly struct RoZcAckReqMakingItem {
        public RoZcAckReqMakingItem(short result, uint itemId) {
            Result = result;
            ItemId = itemId;
        }

        /// <summary>0 / 2 / 4 made, 1 / 3 / 5 failed (the variants pick the effect), 6 / 7 the cooking pair.</summary>
        public short Result { get; }
        public uint ItemId { get; }
    }

    public readonly struct RoZcMergeItemOpen {
        public RoZcMergeItemOpen(short[] bagIndexes) {
            BagIndexes = bagIndexes ?? Array.Empty<short>();
        }

        /// <summary>The stackable duplicates offered, inventory index + 2 as the server sent them.</summary>
        public short[] BagIndexes { get; }
    }

    public readonly struct RoZcAckMergeItem {
        public RoZcAckMergeItem(short bagIndex, short amount, byte reason) {
            BagIndex = bagIndex;
            Amount = amount;
            Reason = reason;
        }

        public short BagIndex { get; }
        public short Amount { get; }

        /// <summary>0 merged, 1 failed (only one kind at a time), 2 over the stack limit.</summary>
        public byte Reason { get; }
    }

    public readonly struct RoZcAutoSpellList {
        public RoZcAutoSpellList(int[] skillIds) {
            SkillIds = skillIds ?? Array.Empty<int>();
        }

        public int[] SkillIds { get; }
    }

    public readonly struct RoZcAckItemRepair {
        public RoZcAckItemRepair(ushort bagIndex, byte result) {
            BagIndex = bagIndex;
            Result = result;
        }

        public ushort BagIndex { get; }
        public byte Result { get; }
    }

    public readonly struct RoZcChatRoomResult {
        public RoZcChatRoomResult(byte result) {
            Result = result;
        }

        public byte Result { get; }
    }

    public readonly struct RoZcChatRoomEnter {
        public RoZcChatRoomEnter(uint chatId, RoChatRoomMember[] members) {
            ChatId = chatId;
            Members = members ?? Array.Empty<RoChatRoomMember>();
        }

        public uint ChatId { get; }
        public RoChatRoomMember[] Members { get; }
    }

    public readonly struct RoZcChatRoomMemberChange {
        public RoZcChatRoomMemberChange(int users, string name, bool kicked) {
            Users = users;
            Name = name ?? string.Empty;
            Kicked = kicked;
        }

        public int Users { get; }
        public string Name { get; }
        public bool Kicked { get; }
    }

    public readonly struct RoZcChatRoomRole {
        public RoZcChatRoomRole(uint role, string name) {
            Role = role;
            Name = name ?? string.Empty;
        }

        public uint Role { get; }
        public string Name { get; }
    }

    public readonly struct RoZcAckReqNameByGid {
        public RoZcAckReqNameByGid(uint characterId, string name) {
            CharacterId = characterId;
            Name = name ?? string.Empty;
        }

        public uint CharacterId { get; }
        public string Name { get; }
    }

    public readonly struct RoZcFriendState {
        public RoZcFriendState(uint accountId, uint characterId, byte stateRaw, string name) {
            AccountId = accountId;
            CharacterId = characterId;
            StateRaw = stateRaw;
            Name = name ?? string.Empty;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
        public byte StateRaw { get; }
        public string Name { get; }
    }

    public readonly struct RoZcFriendRequest {
        public RoZcFriendRequest(uint accountId, uint characterId, string name) {
            AccountId = accountId;
            CharacterId = characterId;
            Name = name ?? string.Empty;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
        public string Name { get; }
    }

    public readonly struct RoZcFriendAddResult {
        public RoZcFriendAddResult(short result, uint accountId, uint characterId, string name) {
            Result = result;
            AccountId = accountId;
            CharacterId = characterId;
            Name = name ?? string.Empty;
        }

        public short Result { get; }
        public uint AccountId { get; }
        public uint CharacterId { get; }
        public string Name { get; }
        public bool IsSuccess => Result == 0;
    }

    public readonly struct RoZcFriendDelete {
        public RoZcFriendDelete(uint accountId, uint characterId) {
            AccountId = accountId;
            CharacterId = characterId;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
    }

    public readonly struct RoZcSkillInfo {
        public RoZcSkillInfo(
            ushort skillId,
            int type,
            ushort level,
            ushort spCost,
            ushort attackRange,
            string name,
            byte upgradable,
            ushort level2) {
            SkillId = skillId;
            Type = type;
            Level = level;
            SpCost = spCost;
            AttackRange = attackRange;
            Name = name ?? string.Empty;
            Upgradable = upgradable;
            Level2 = level2;
        }

        public ushort SkillId { get; }
        public int Type { get; }
        public ushort Level { get; }
        public ushort SpCost { get; }
        public ushort AttackRange { get; }
        public string Name { get; }
        public byte Upgradable { get; }
        public ushort Level2 { get; }
    }

    public readonly struct RoZcSkillInfoList {
        public RoZcSkillInfoList(RoZcSkillInfo[] skills, bool hasEmbeddedNames) {
            Skills = skills ?? Array.Empty<RoZcSkillInfo>();
            HasEmbeddedNames = hasEmbeddedNames;
        }

        public RoZcSkillInfo[] Skills { get; }
        public bool HasEmbeddedNames { get; }
    }

    public readonly struct RoZcAckToUseSkill {
        public RoZcAckToUseSkill(ushort skillId, int value, byte resultRaw, byte causeRaw)
            : this(skillId, value, 0, resultRaw, causeRaw) {
        }

        public RoZcAckToUseSkill(ushort skillId, int value, uint itemId, byte resultRaw, byte causeRaw) {
            SkillId = skillId;
            Value = value;
            ItemId = itemId;
            ResultRaw = resultRaw;
            CauseRaw = causeRaw;
        }

        public ushort SkillId { get; }
        public int Value { get; }
        public uint ItemId { get; }
        public byte ResultRaw { get; }
        public byte CauseRaw { get; }
        public bool IsSuccess => ResultRaw != 0;
    }

    public readonly struct RoZcAttackFailureForDistance {
        public RoZcAttackFailureForDistance(uint targetAid, short targetX, short targetY, short x, short y, short range) {
            TargetAid = targetAid;
            TargetX = targetX;
            TargetY = targetY;
            X = x;
            Y = y;
            Range = range;
        }

        public uint TargetAid { get; }
        public short TargetX { get; }
        public short TargetY { get; }
        public short X { get; }
        public short Y { get; }
        public short Range { get; }
    }

    public readonly struct RoZcActionFailure {
        public RoZcActionFailure(ushort typeRaw) {
            TypeRaw = typeRaw;
        }

        public ushort TypeRaw { get; }
    }

    public readonly struct RoZcUseSkillAck {
        public RoZcUseSkillAck(
            uint sourceId,
            uint targetId,
            ushort x,
            ushort y,
            ushort skillId,
            uint element,
            uint delayTime,
            byte disposable,
            uint attackMotionTime) {
            SourceId = sourceId;
            TargetId = targetId;
            X = x;
            Y = y;
            SkillId = skillId;
            Element = element;
            DelayTime = delayTime;
            Disposable = disposable;
            AttackMotionTime = attackMotionTime;
        }

        public uint SourceId { get; }
        public uint TargetId { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public ushort SkillId { get; }
        public uint Element { get; }
        public uint DelayTime { get; }
        public byte Disposable { get; }
        public uint AttackMotionTime { get; }
    }

    public readonly struct RoZcNotifySkill {
        public RoZcNotifySkill(
            ushort skillId,
            uint sourceId,
            uint targetId,
            uint serverTick,
            int sourceDelay,
            int targetDelay,
            int damage,
            short level,
            short hitCount,
            sbyte actionRaw) {
            SkillId = skillId;
            SourceId = sourceId;
            TargetId = targetId;
            ServerTick = serverTick;
            SourceDelay = sourceDelay;
            TargetDelay = targetDelay;
            Damage = damage;
            Level = level;
            HitCount = hitCount;
            ActionRaw = actionRaw;
        }

        public ushort SkillId { get; }
        public uint SourceId { get; }
        public uint TargetId { get; }
        public uint ServerTick { get; }
        public int SourceDelay { get; }
        public int TargetDelay { get; }
        public int Damage { get; }
        public short Level { get; }
        public short HitCount { get; }
        public sbyte ActionRaw { get; }
    }

    public readonly struct RoZcNotifySkillPosition {
        public RoZcNotifySkillPosition(
            ushort skillId,
            uint sourceId,
            uint targetId,
            uint serverTick,
            int sourceDelay,
            int targetDelay,
            short x,
            short y,
            short damage,
            short level,
            short hitCount,
            sbyte actionRaw) {
            SkillId = skillId;
            SourceId = sourceId;
            TargetId = targetId;
            ServerTick = serverTick;
            SourceDelay = sourceDelay;
            TargetDelay = targetDelay;
            X = x;
            Y = y;
            Damage = damage;
            Level = level;
            HitCount = hitCount;
            ActionRaw = actionRaw;
        }

        public ushort SkillId { get; }
        public uint SourceId { get; }
        public uint TargetId { get; }
        public uint ServerTick { get; }
        public int SourceDelay { get; }
        public int TargetDelay { get; }
        public short X { get; }
        public short Y { get; }
        public short Damage { get; }
        public short Level { get; }
        public short HitCount { get; }
        public sbyte ActionRaw { get; }
    }

    public readonly struct RoZcUseSkillResult {
        public RoZcUseSkillResult(ushort skillId, int value, uint targetId, uint sourceId, byte resultRaw) {
            SkillId = skillId;
            Value = value;
            TargetId = targetId;
            SourceId = sourceId;
            ResultRaw = resultRaw;
        }

        public ushort SkillId { get; }
        public int Value { get; }
        public uint TargetId { get; }
        public uint SourceId { get; }
        public byte ResultRaw { get; }
        public bool IsSuccess => ResultRaw != 0;
    }

    public readonly struct RoZcNotifyEffect {
        public RoZcNotifyEffect(uint actorId, int effectId) {
            ActorId = actorId;
            EffectId = effectId;
        }

        public uint ActorId { get; }
        public int EffectId { get; }
    }

    public readonly struct RoZcNotifyGroundSkill {
        public RoZcNotifyGroundSkill(ushort skillId, uint sourceId, short level, short x, short y, uint serverTick) {
            SkillId = skillId;
            SourceId = sourceId;
            Level = level;
            X = x;
            Y = y;
            ServerTick = serverTick;
        }

        public ushort SkillId { get; }
        public uint SourceId { get; }
        public short Level { get; }
        public short X { get; }
        public short Y { get; }
        public uint ServerTick { get; }
    }

    public readonly struct RoZcSkillDisappear {
        public RoZcSkillDisappear(uint unitId) {
            UnitId = unitId;
        }

        public uint UnitId { get; }
    }

    public readonly struct RoZcSkillPostDelay {
        public RoZcSkillPostDelay(ushort skillId, uint delayTime) {
            SkillId = skillId;
            DelayTime = delayTime;
        }

        public ushort SkillId { get; }
        public uint DelayTime { get; }
    }

    public readonly struct RoZcSkillEntry {
        public RoZcSkillEntry(ushort skillId, uint creatorId, short x, short y, uint unitId, byte range, byte visible, byte skillLevel) {
            SkillId = skillId;
            CreatorId = creatorId;
            X = x;
            Y = y;
            UnitId = unitId;
            Range = range;
            Visible = visible;
            SkillLevel = skillLevel;
        }

        public ushort SkillId { get; }
        public uint CreatorId { get; }
        public short X { get; }
        public short Y { get; }
        public uint UnitId { get; }
        public byte Range { get; }
        public byte Visible { get; }
        public byte SkillLevel { get; }
    }

    public readonly struct RoShortcutKeyEntry {
        public RoShortcutKeyEntry(sbyte typeRaw, uint id, short count) {
            TypeRaw = typeRaw;
            Id = id;
            Count = count;
        }

        public sbyte TypeRaw { get; }
        public bool IsEmpty => TypeRaw == 0 && Id == 0 && Count == 0;
        public bool IsSkill => TypeRaw != 0 && Id != 0;
        public uint Id { get; }
        public short Count { get; }
    }

    public readonly struct RoZcShortcutKeyList {
        public RoZcShortcutKeyList(byte rotate, short tab, RoShortcutKeyEntry[] shortcuts) {
            Rotate = rotate;
            Tab = tab;
            Shortcuts = shortcuts ?? Array.Empty<RoShortcutKeyEntry>();
        }

        public byte Rotate { get; }
        public short Tab { get; }
        public RoShortcutKeyEntry[] Shortcuts { get; }
    }

    public readonly struct RoZcSelectDealType {
        public RoZcSelectDealType(uint npcId) {
            NpcId = npcId;
        }

        public uint NpcId { get; }
    }

    public enum RoNpcShopMode {
        None = 0,
        DealType = 1,
        Buy = 2,
        Sell = 3,
        Market = 4,
        Barter = 5,
        ExpandedBarter = 6
    }

    public readonly struct RoNpcShopBuyItem {
        public RoNpcShopBuyItem(uint itemId, uint price, uint discountPrice, byte itemTypeRaw, ushort viewSprite = 0, uint location = 0) {
            ItemId = itemId;
            Price = price;
            DiscountPrice = discountPrice;
            ItemTypeRaw = itemTypeRaw;
            ViewSprite = viewSprite;
            Location = location;
        }

        public uint ItemId { get; }
        public uint Price { get; }
        public uint DiscountPrice { get; }
        public byte ItemTypeRaw { get; }
        public ushort ViewSprite { get; }
        public uint Location { get; }
    }

    public readonly struct RoNpcShopSellItem {
        public RoNpcShopSellItem(ushort bagIndex, uint price, uint overchargePrice) {
            BagIndex = bagIndex;
            Price = price;
            OverchargePrice = overchargePrice;
        }

        public ushort BagIndex { get; }
        public uint Price { get; }
        public uint OverchargePrice { get; }
    }

    public readonly struct RoZcNpcShopBuyList {
        public RoZcNpcShopBuyList(RoNpcShopBuyItem[] items) {
            Items = items ?? Array.Empty<RoNpcShopBuyItem>();
        }

        public RoNpcShopBuyItem[] Items { get; }
    }

    public readonly struct RoZcNpcShopSellList {
        public RoZcNpcShopSellList(RoNpcShopSellItem[] items) {
            Items = items ?? Array.Empty<RoNpcShopSellItem>();
        }

        public RoNpcShopSellItem[] Items { get; }
    }

    public readonly struct RoZcNpcShopResult {
        public RoZcNpcShopResult(byte result) {
            Result = result;
        }

        public byte Result { get; }
        public bool IsSuccess => Result == 0;
    }

    public readonly struct RoZcOpenStore {
        public RoZcOpenStore(ushort slotCount) {
            SlotCount = slotCount;
        }

        public ushort SlotCount { get; }
    }

    public readonly struct RoZcStoreEntry {
        public RoZcStoreEntry(uint ownerAid, string storeName) {
            OwnerAid = ownerAid;
            StoreName = storeName ?? string.Empty;
        }

        public uint OwnerAid { get; }
        public string StoreName { get; }
    }

    public readonly struct RoZcDisappearEntry {
        public RoZcDisappearEntry(uint ownerAid) {
            OwnerAid = ownerAid;
        }

        public uint OwnerAid { get; }
    }

    public readonly struct RoPlayerVendingItem {
        public RoPlayerVendingItem(
            uint price,
            ushort amount,
            short index,
            byte itemTypeRaw,
            uint itemId,
            bool isIdentified,
            bool isDamaged,
            byte refineLevel,
            int[] cardIds,
            RoPacketItemRandomOption[] randomOptions = null,
            uint location = 0,
            ushort viewSprite = 0,
            byte grade = 0) {
            Price = price;
            Amount = amount;
            Index = index;
            ItemTypeRaw = itemTypeRaw;
            ItemId = itemId;
            IsIdentified = isIdentified;
            IsDamaged = isDamaged;
            RefineLevel = refineLevel;
            CardIds = CopyCardIds(cardIds);
            RandomOptions = CopyRandomOptions(randomOptions);
            Location = location;
            ViewSprite = viewSprite;
            Grade = grade;
        }

        public uint Price { get; }
        public ushort Amount { get; }
        public short Index { get; }
        public byte ItemTypeRaw { get; }
        public uint ItemId { get; }
        public bool IsIdentified { get; }
        public bool IsDamaged { get; }
        public byte RefineLevel { get; }
        public int[] CardIds { get; }
        public RoPacketItemRandomOption[] RandomOptions { get; }
        public uint Location { get; }
        public ushort ViewSprite { get; }
        public byte Grade { get; }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[4];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }

        private static RoPacketItemRandomOption[] CopyRandomOptions(RoPacketItemRandomOption[] source) {
            var copy = new RoPacketItemRandomOption[5];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoZcPlayerVendingItemList {
        public RoZcPlayerVendingItemList(uint ownerAid, uint uniqueId, bool hasUniqueId, RoPlayerVendingItem[] items) {
            OwnerAid = ownerAid;
            UniqueId = uniqueId;
            HasUniqueId = hasUniqueId;
            Items = items ?? Array.Empty<RoPlayerVendingItem>();
        }

        public uint OwnerAid { get; }
        public uint UniqueId { get; }
        public bool HasUniqueId { get; }
        public RoPlayerVendingItem[] Items { get; }
    }

    public readonly struct RoZcPlayerVendingPurchaseResult {
        public RoZcPlayerVendingPurchaseResult(ushort index, ushort currentCount, byte resultRaw) {
            Index = index;
            CurrentCount = currentCount;
            ResultRaw = resultRaw;
        }

        public ushort Index { get; }
        public ushort CurrentCount { get; }
        public byte ResultRaw { get; }
        public bool IsSuccess => ResultRaw == 0;
    }

    public readonly struct RoZcAckOpenStore2 {
        public RoZcAckOpenStore2(byte resultRaw) {
            ResultRaw = resultRaw;
        }

        public byte ResultRaw { get; }
        public bool IsSuccess => ResultRaw == 0;
    }

    public readonly struct RoZcDeleteItemFromMcStore {
        public RoZcDeleteItemFromMcStore(ushort index, ushort amount, uint buyerCid = 0, uint date = 0, uint zeny = 0, bool hasBuyerDetails = false) {
            Index = index;
            Amount = amount;
            BuyerCid = buyerCid;
            Date = date;
            Zeny = zeny;
            HasBuyerDetails = hasBuyerDetails;
        }

        public ushort Index { get; }
        public ushort Amount { get; }
        public uint BuyerCid { get; }
        public uint Date { get; }
        public uint Zeny { get; }
        public bool HasBuyerDetails { get; }
    }

    public readonly struct RoNpcMarketItem {
        public RoNpcMarketItem(uint itemId, byte itemTypeRaw, uint price, uint amount, uint stock, uint weight, uint location, uint shopIndex) {
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            Price = price;
            Amount = amount;
            Stock = stock;
            Weight = weight;
            Location = location;
            ShopIndex = shopIndex;
        }

        public uint ItemId { get; }
        public byte ItemTypeRaw { get; }
        public uint Price { get; }
        public uint Amount { get; }
        public uint Stock { get; }
        public uint Weight { get; }
        public uint Location { get; }
        public uint ShopIndex { get; }
    }

    public readonly struct RoZcNpcMarketList {
        public RoZcNpcMarketList(RoNpcMarketItem[] items) {
            Items = items ?? Array.Empty<RoNpcMarketItem>();
        }

        public RoNpcMarketItem[] Items { get; }
    }

    public readonly struct RoNpcMarketResultItem {
        public RoNpcMarketResultItem(uint itemId, ushort amount, uint price) {
            ItemId = itemId;
            Amount = amount;
            Price = price;
        }

        public uint ItemId { get; }
        public ushort Amount { get; }
        public uint Price { get; }
    }

    public readonly struct RoZcNpcMarketPurchaseResult {
        public RoZcNpcMarketPurchaseResult(ushort result, RoNpcMarketResultItem[] items) {
            Result = result;
            Items = items ?? Array.Empty<RoNpcMarketResultItem>();
        }

        public ushort Result { get; }
        public RoNpcMarketResultItem[] Items { get; }
        public bool IsSuccess => Result == 0;
    }

    public readonly struct RoNpcBarterCurrency {
        public RoNpcBarterCurrency(uint itemId, ushort refineLevel, uint amount, ushort itemTypeRaw) {
            ItemId = itemId;
            RefineLevel = refineLevel;
            Amount = amount;
            ItemTypeRaw = itemTypeRaw;
        }

        public uint ItemId { get; }
        public ushort RefineLevel { get; }
        public uint Amount { get; }
        public ushort ItemTypeRaw { get; }
    }

    public readonly struct RoNpcBarterItem {
        public RoNpcBarterItem(
            uint itemId,
            ushort itemTypeRaw,
            uint amount,
            uint stock,
            uint weight,
            uint shopIndex,
            uint location,
            uint price,
            ushort viewSprite,
            RoNpcBarterCurrency[] currencies) {
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            Amount = amount;
            Stock = stock;
            Weight = weight;
            ShopIndex = shopIndex;
            Location = location;
            Price = price;
            ViewSprite = viewSprite;
            Currencies = currencies ?? Array.Empty<RoNpcBarterCurrency>();
        }

        public uint ItemId { get; }
        public ushort ItemTypeRaw { get; }
        public uint Amount { get; }
        public uint Stock { get; }
        public uint Weight { get; }
        public uint ShopIndex { get; }
        public uint Location { get; }
        public uint Price { get; }
        public ushort ViewSprite { get; }
        public RoNpcBarterCurrency[] Currencies { get; }
    }

    public readonly struct RoZcNpcBarterList {
        public RoZcNpcBarterList(RoNpcBarterItem[] items) {
            Items = items ?? Array.Empty<RoNpcBarterItem>();
        }

        public RoNpcBarterItem[] Items { get; }
    }

    public readonly struct RoNpcShopPurchaseRequest {
        public RoNpcShopPurchaseRequest(ushort amount, uint itemId) {
            Amount = amount;
            ItemId = itemId;
        }

        public ushort Amount { get; }
        public uint ItemId { get; }
    }

    public readonly struct RoVendingOpenStoreItem {
        public RoVendingOpenStoreItem(ushort cartIndex, ushort amount, uint price) {
            CartIndex = cartIndex;
            Amount = amount;
            Price = price;
        }

        public ushort CartIndex { get; }
        public ushort Amount { get; }
        public uint Price { get; }
    }

    public readonly struct RoPlayerShopPurchaseRequest {
        public RoPlayerShopPurchaseRequest(ushort amount, ushort index) {
            Amount = amount;
            Index = index;
        }

        public ushort Amount { get; }
        public ushort Index { get; }
    }

    public readonly struct RoNpcShopSellRequest {
        public RoNpcShopSellRequest(ushort bagIndex, ushort amount) {
            BagIndex = bagIndex;
            Amount = amount;
        }

        public ushort BagIndex { get; }
        public ushort Amount { get; }
    }

    public readonly struct RoNpcMarketPurchaseRequest {
        public RoNpcMarketPurchaseRequest(uint itemId, int amount) {
            ItemId = itemId;
            Amount = amount;
        }

        public uint ItemId { get; }
        public int Amount { get; }
    }

    public readonly struct RoNpcBarterPurchaseRequest {
        public RoNpcBarterPurchaseRequest(uint itemId, uint amount, ushort inventoryIndex, uint shopIndex) {
            ItemId = itemId;
            Amount = amount;
            InventoryIndex = inventoryIndex;
            ShopIndex = shopIndex;
        }

        public uint ItemId { get; }
        public uint Amount { get; }
        public ushort InventoryIndex { get; }
        public uint ShopIndex { get; }
    }

    public readonly struct RoNpcExpandedBarterPurchaseRequest {
        public RoNpcExpandedBarterPurchaseRequest(uint itemId, uint shopIndex, uint amount) {
            ItemId = itemId;
            ShopIndex = shopIndex;
            Amount = amount;
        }

        public uint ItemId { get; }
        public uint ShopIndex { get; }
        public uint Amount { get; }
    }

    public readonly struct RoZcStorageAmount {
        public RoZcStorageAmount(ushort amount, ushort maxAmount) {
            Amount = amount;
            MaxAmount = maxAmount;
        }

        public ushort Amount { get; }
        public ushort MaxAmount { get; }
    }

    public readonly struct RoZcStorageItem {
        public RoZcStorageItem(
            short storageIndex,
            int amount,
            uint itemId,
            byte itemTypeRaw,
            bool isIdentified,
            bool isDamaged,
            byte refineLevel,
            int[] cardIds,
            byte enchantGrade = 0,
            RoPacketItemRandomOption[] randomOptions = null) {
            StorageIndex = storageIndex;
            Amount = amount;
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            IsIdentified = isIdentified;
            IsDamaged = isDamaged;
            RefineLevel = refineLevel;
            CardIds = CopyCardIds(cardIds);
            EnchantGrade = enchantGrade;
            RandomOptions = CopyRandomOptions(randomOptions);
        }

        public short StorageIndex { get; }
        public int Amount { get; }
        public uint ItemId { get; }
        public byte ItemTypeRaw { get; }
        public bool IsIdentified { get; }
        public bool IsDamaged { get; }
        public byte RefineLevel { get; }
        public int[] CardIds { get; }
        public byte EnchantGrade { get; }
        public RoPacketItemRandomOption[] RandomOptions { get; }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[4];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }

        private static RoPacketItemRandomOption[] CopyRandomOptions(RoPacketItemRandomOption[] source) {
            var copy = new RoPacketItemRandomOption[5];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoZcStorageItemDelete {
        public RoZcStorageItemDelete(ushort storageIndex, uint amount) {
            StorageIndex = storageIndex;
            Amount = amount;
        }

        public ushort StorageIndex { get; }
        public uint Amount { get; }
    }

    public readonly struct RoZcCartAmount {
        public RoZcCartAmount(ushort currentCount, ushort maxCount, int currentWeight, int maxWeight) {
            CurrentCount = currentCount;
            MaxCount = maxCount;
            CurrentWeight = currentWeight;
            MaxWeight = maxWeight;
        }

        public ushort CurrentCount { get; }
        public ushort MaxCount { get; }
        public int CurrentWeight { get; }
        public int MaxWeight { get; }
    }

    public readonly struct RoZcBankCheck {
        public RoZcBankCheck(long money, short reason) {
            Money = money;
            Reason = reason;
        }

        public long Money { get; }
        public short Reason { get; }
        public bool Success => Reason == 0;
    }

    public readonly struct RoZcBankTransactionAck {
        public RoZcBankTransactionAck(short reason, long bankMoney, int zeny) {
            Reason = reason;
            BankMoney = bankMoney;
            Zeny = zeny;
        }

        public short Reason { get; }
        public long BankMoney { get; }
        public int Zeny { get; }
        public bool Success => Reason == 0;
    }

    public readonly struct RoZcBankSimpleAck {
        public RoZcBankSimpleAck(ushort resultRaw) {
            ResultRaw = resultRaw;
        }

        public ushort ResultRaw { get; }
        public bool Success => ResultRaw == 0 || ResultRaw == 1;
    }

    public readonly struct RoZcTradeRequest {
        public RoZcTradeRequest(string partnerName, uint partnerId, short partnerLevel) {
            PartnerName = partnerName ?? string.Empty;
            PartnerId = partnerId;
            PartnerLevel = partnerLevel;
        }

        public string PartnerName { get; }
        public uint PartnerId { get; }
        public short PartnerLevel { get; }
    }

    public readonly struct RoZcTradeAck {
        public RoZcTradeAck(byte result, uint partnerId, short partnerLevel) {
            Result = result;
            PartnerId = partnerId;
            PartnerLevel = partnerLevel;
        }

        public byte Result { get; }
        public uint PartnerId { get; }
        public short PartnerLevel { get; }
    }

    public readonly struct RoZcTradeAddItemAck {
        public RoZcTradeAddItemAck(ushort inventoryIndex, byte result) {
            InventoryIndex = inventoryIndex;
            Result = result;
        }

        public ushort InventoryIndex { get; }
        public byte Result { get; }
    }

    public readonly struct RoZcTradeItem {
        public RoZcTradeItem(
            uint itemId,
            byte itemTypeRaw,
            int count,
            bool isIdentified,
            bool isDamaged,
            byte refineLevel,
            int[] cardIds) {
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            Count = Math.Max(0, count);
            IsIdentified = isIdentified;
            IsDamaged = isDamaged;
            RefineLevel = refineLevel;
            CardIds = CopyCardIds(cardIds);
        }

        public uint ItemId { get; }
        public byte ItemTypeRaw { get; }
        public int Count { get; }
        public bool IsIdentified { get; }
        public bool IsDamaged { get; }
        public byte RefineLevel { get; }
        public int[] CardIds { get; }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[4];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoZcTradeConclude {
        public RoZcTradeConclude(bool remoteSide) {
            RemoteSide = remoteSide;
        }

        public bool RemoteSide { get; }
    }

    public readonly struct RoZcTradeSubmitResult {
        public RoZcTradeSubmitResult(byte result) {
            Result = result;
        }

        public byte Result { get; }
    }

    public readonly struct RoZcInventoryItemDelete {
        public RoZcInventoryItemDelete(ushort deleteTypeRaw, ushort bagIndex, ushort amount) {
            DeleteTypeRaw = deleteTypeRaw;
            BagIndex = bagIndex;
            Amount = amount;
        }

        public ushort DeleteTypeRaw { get; }
        public ushort BagIndex { get; }
        public ushort Amount { get; }
    }

    public readonly struct RoZcChangeItemOption {
        public RoZcChangeItemOption(
            short bagIndex,
            bool isDamaged,
            int[] cardIds,
            RoPacketItemRandomOption[] randomOptions,
            byte refineLevel,
            byte enchantGrade
        ) {
            BagIndex = bagIndex;
            IsDamaged = isDamaged;
            CardIds = CopyCardIds(cardIds);
            RandomOptions = CopyRandomOptions(randomOptions);
            RefineLevel = refineLevel;
            EnchantGrade = enchantGrade;
        }

        public short BagIndex { get; }
        public bool IsDamaged { get; }
        public int[] CardIds { get; }
        public RoPacketItemRandomOption[] RandomOptions { get; }
        public byte RefineLevel { get; }
        public byte EnchantGrade { get; }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[4];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }

        private static RoPacketItemRandomOption[] CopyRandomOptions(RoPacketItemRandomOption[] source) {
            var copy = new RoPacketItemRandomOption[5];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoZcGuildBelongInfo {
        public RoZcGuildBelongInfo(uint guildId, int emblemVersion, int rights, bool isMaster, int interServerId, string guildName) {
            GuildId = guildId;
            EmblemVersion = emblemVersion;
            Rights = rights;
            IsMaster = isMaster;
            InterServerId = interServerId;
            GuildName = guildName ?? string.Empty;
        }

        public uint GuildId { get; }
        public int EmblemVersion { get; }
        public int Rights { get; }
        public bool IsMaster { get; }
        public int InterServerId { get; }
        public string GuildName { get; }
    }

    public readonly struct RoZcGuildNotice {
        public RoZcGuildNotice(string subject, string notice) {
            Subject = subject ?? string.Empty;
            Notice = notice ?? string.Empty;
        }

        public string Subject { get; }
        public string Notice { get; }
    }

    public readonly struct RoZcGuildMemberInfo {
        public RoZcGuildMemberInfo(
            uint accountId,
            uint characterId,
            short head,
            short headPalette,
            short sex,
            short job,
            short level,
            int contributionExp,
            int currentState,
            int positionId,
            string memo,
            string characterName) {
            AccountId = accountId;
            CharacterId = characterId;
            Head = head;
            HeadPalette = headPalette;
            Sex = sex;
            Job = job;
            Level = level;
            ContributionExp = contributionExp;
            CurrentState = currentState;
            PositionId = positionId;
            Memo = memo ?? string.Empty;
            CharacterName = characterName ?? string.Empty;
        }

        public uint AccountId { get; }
        public uint CharacterId { get; }
        public short Head { get; }
        public short HeadPalette { get; }
        public short Sex { get; }
        public short Job { get; }
        public short Level { get; }
        public int ContributionExp { get; }
        public int CurrentState { get; }
        public int PositionId { get; }
        public string Memo { get; }
        public string CharacterName { get; }
    }

    public readonly struct RoZcGuildInvite {
        public RoZcGuildInvite(uint guildId, string guildName) {
            GuildId = guildId;
            GuildName = guildName ?? string.Empty;
        }

        public uint GuildId { get; }
        public string GuildName { get; }
    }

    public readonly struct RoZcGuildInviteResult {
        public RoZcGuildInviteResult(byte result) {
            Result = result;
        }

        public byte Result { get; }
    }

    public readonly struct RoZcGuildLeaveResult {
        public RoZcGuildLeaveResult(string characterName, string reason) {
            CharacterName = characterName ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string CharacterName { get; }
        public string Reason { get; }
    }

    public readonly struct RoZcGuildChat {
        public RoZcGuildChat(string message) {
            Message = message ?? string.Empty;
        }

        public string Message { get; }
    }

    public readonly struct RoZcPartyChat {
        public RoZcPartyChat(string message, uint sourceActorId = 0) {
            Message = message ?? string.Empty;
            SourceActorId = sourceActorId;
        }

        public string Message { get; }

        /// <summary>The speaker's account id (0x0109 {len, AID, text}).</summary>
        public uint SourceActorId { get; }
    }

    public readonly struct RoZcClanChat {
        public RoZcClanChat(string sender, string message) {
            Sender = sender ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Sender { get; }
        public string Message { get; }
    }

    public readonly struct RoZcPetProperty {
        public RoZcPetProperty(string name, bool modified, short level, short hunger, short intimacy, uint itemId, short job) {
            Name = name ?? string.Empty;
            Modified = modified;
            Level = level;
            Hunger = hunger;
            Intimacy = intimacy;
            ItemId = itemId;
            Job = job;
        }

        public string Name { get; }
        public bool Modified { get; }
        public short Level { get; }
        public short Hunger { get; }
        public short Intimacy { get; }
        public uint ItemId { get; }
        public short Job { get; }
    }

    public readonly struct RoZcPetFeedResult {
        public RoZcPetFeedResult(sbyte result, uint itemId) {
            Result = result;
            ItemId = itemId;
        }

        public sbyte Result { get; }
        public uint ItemId { get; }
        public bool IsSuccess => Result != 0;
    }

    public readonly struct RoZcPetChangeState {
        public RoZcPetChangeState(sbyte type, uint actorId, int data) {
            Type = type;
            ActorId = actorId;
            Data = data;
        }

        public sbyte Type { get; }
        public uint ActorId { get; }
        public int Data { get; }
    }

    public readonly struct RoZcPetAction {
        public RoZcPetAction(uint actorId, int data) {
            ActorId = actorId;
            Data = data;
        }

        public uint ActorId { get; }
        public int Data { get; }
    }

    public readonly struct RoZcHomunculusProperty {
        public RoZcHomunculusProperty(
            string name,
            bool modified,
            short level,
            short hunger,
            short intimacy,
            uint itemId,
            short attack,
            short magicAttack,
            short hit,
            short critical,
            short defense,
            short magicDefense,
            short flee,
            short attackSpeed,
            int hp,
            int maxHp,
            short sp,
            short maxSp,
            int exp,
            int maxExp,
            short skillPoints,
            short attackRange) {
            Name = name ?? string.Empty;
            Modified = modified;
            Level = level;
            Hunger = hunger;
            Intimacy = intimacy;
            ItemId = itemId;
            Attack = attack;
            MagicAttack = magicAttack;
            Hit = hit;
            Critical = critical;
            Defense = defense;
            MagicDefense = magicDefense;
            Flee = flee;
            AttackSpeed = attackSpeed;
            Hp = hp;
            MaxHp = maxHp;
            Sp = sp;
            MaxSp = maxSp;
            Exp = exp;
            MaxExp = maxExp;
            SkillPoints = skillPoints;
            AttackRange = attackRange;
        }

        public string Name { get; }
        public bool Modified { get; }
        public short Level { get; }
        public short Hunger { get; }
        public short Intimacy { get; }
        public uint ItemId { get; }
        public short Attack { get; }
        public short MagicAttack { get; }
        public short Hit { get; }
        public short Critical { get; }
        public short Defense { get; }
        public short MagicDefense { get; }
        public short Flee { get; }
        public short AttackSpeed { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public short Sp { get; }
        public short MaxSp { get; }
        public int Exp { get; }
        public int MaxExp { get; }
        public short SkillPoints { get; }
        public short AttackRange { get; }
    }

    public readonly struct RoZcCompanionChangeState {
        public RoZcCompanionChangeState(sbyte type, sbyte state, uint actorId, int data) {
            Type = type;
            State = state;
            ActorId = actorId;
            Data = data;
        }

        public sbyte Type { get; }
        public sbyte State { get; }
        public uint ActorId { get; }
        public int Data { get; }
    }

    public readonly struct RoZcCompanionParameterChange {
        public RoZcCompanionParameterChange(ushort parameter, int value) {
            Parameter = parameter;
            Value = value;
        }

        public ushort Parameter { get; }
        public int Value { get; }
    }

    public readonly struct RoZcMercenaryInfo {
        public RoZcMercenaryInfo(
            uint actorId,
            short attack,
            short magicAttack,
            short hit,
            short critical,
            short defense,
            short magicDefense,
            short flee,
            short attackSpeed,
            string name,
            short level,
            int hp,
            int maxHp,
            int sp,
            int maxSp,
            int expireDate,
            short faith,
            int callCount,
            int killCount,
            short attackRange) {
            ActorId = actorId;
            Attack = attack;
            MagicAttack = magicAttack;
            Hit = hit;
            Critical = critical;
            Defense = defense;
            MagicDefense = magicDefense;
            Flee = flee;
            AttackSpeed = attackSpeed;
            Name = name ?? string.Empty;
            Level = level;
            Hp = hp;
            MaxHp = maxHp;
            Sp = sp;
            MaxSp = maxSp;
            ExpireDate = expireDate;
            Faith = faith;
            CallCount = callCount;
            KillCount = killCount;
            AttackRange = attackRange;
        }

        public uint ActorId { get; }
        public short Attack { get; }
        public short MagicAttack { get; }
        public short Hit { get; }
        public short Critical { get; }
        public short Defense { get; }
        public short MagicDefense { get; }
        public short Flee { get; }
        public short AttackSpeed { get; }
        public string Name { get; }
        public short Level { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Sp { get; }
        public int MaxSp { get; }
        public int ExpireDate { get; }
        public short Faith { get; }
        public int CallCount { get; }
        public int KillCount { get; }
        public short AttackRange { get; }
    }

    public readonly struct RoZcMapProperty {
        public RoZcMapProperty(short type, uint flags, ushort packetId = 0) {
            Type = type;
            Flags = flags;
            PacketId = packetId;
        }

        public short Type { get; }
        public uint Flags { get; }
        /// <summary>The packet it came in: 0x199, 0x1d6 or 0x99b (they set different client state).</summary>
        public ushort PacketId { get; }
    }

    /// <summary>ZC_NOTIFY_RANKING (0x19a): an actor's PvP rank among <see cref="Total"/> players.</summary>
    public readonly struct RoZcNotifyRanking {
        public RoZcNotifyRanking(uint aid, int rank, int total) {
            Aid = aid;
            Rank = rank;
            Total = total;
        }

        public uint Aid { get; }
        public int Rank { get; }
        public int Total { get; }
    }

    /// <summary>ZC_BATTLEFIELD_NOTIFY_CAMPINFO (0x2dd): the battleground team (camp) of a player; the client keeps its low byte.</summary>
    public readonly struct RoZcBattlefieldCampInfo {
        public RoZcBattlefieldCampInfo(uint accountId, string name, short camp) {
            AccountId = accountId;
            Name = name ?? string.Empty;
            Camp = camp;
        }

        public uint AccountId { get; }
        public string Name { get; }
        public short Camp { get; }
    }

    /// <summary>ZC_BATTLEFIELD_NOTIFY_POSITION (0x2df): a teammate's minimap position (−1 / −1 when it leaves).</summary>
    public readonly struct RoZcBattlefieldPosition {
        public RoZcBattlefieldPosition(uint accountId, string name, ushort job, int x, int y) {
            AccountId = accountId;
            Name = name ?? string.Empty;
            Job = job;
            X = x;
            Y = y;
        }

        public uint AccountId { get; }
        public string Name { get; }
        public ushort Job { get; }
        public int X { get; }
        public int Y { get; }
    }

    /// <summary>ZC_BATTLEFIELD_NOTIFY_HP (0x2e0) / HP2 (0xa0e): a teammate's HP for the bar over its head.</summary>
    public readonly struct RoZcBattlefieldHp {
        public RoZcBattlefieldHp(uint accountId, int hp, int maxHp) {
            AccountId = accountId;
            Hp = hp;
            MaxHp = maxHp;
        }

        public uint AccountId { get; }
        public int Hp { get; }
        public int MaxHp { get; }
    }

    /// <summary>ZC_BATTLEFIELD_NOTIFY_POINT (0x2de): the two battleground team scores.</summary>
    public readonly struct RoZcBattlefieldScore {
        public RoZcBattlefieldScore(short lion, short eagle) {
            Lion = lion;
            Eagle = eagle;
        }

        public short Lion { get; }
        public short Eagle { get; }
    }

    public readonly struct RoZcNotifyUnreadMail {
        public RoZcNotifyUnreadMail(byte unreadRaw) {
            UnreadRaw = unreadRaw;
        }

        public byte UnreadRaw { get; }
        public bool HasUnreadRodexMail => UnreadRaw != 0;
    }

    public readonly struct RoRodexMailHeader {
        public RoRodexMailHeader(byte openType, ulong mailId, bool isRead, byte type, string senderName, int expireDateTime, string title) {
            OpenType = openType;
            MailId = mailId;
            IsRead = isRead;
            Type = type;
            SenderName = senderName ?? string.Empty;
            ExpireDateTime = expireDateTime;
            Title = title ?? string.Empty;
        }

        public byte OpenType { get; }
        public ulong MailId { get; }
        public bool IsRead { get; }
        public byte Type { get; }
        public string SenderName { get; }
        public int ExpireDateTime { get; }
        public string Title { get; }
    }

    public readonly struct RoZcRodexMailList {
        public RoZcRodexMailList(bool isEnd, RoRodexMailHeader[] mails) {
            IsEnd = isEnd;
            Mails = mails ?? Array.Empty<RoRodexMailHeader>();
        }

        public bool IsEnd { get; }
        public RoRodexMailHeader[] Mails { get; }
    }

    public readonly struct RoZcRodexReadMail {
        public RoZcRodexReadMail(byte openType, ulong mailId, string body, long zeny, RoZcInventoryItem[] items) {
            OpenType = openType;
            MailId = mailId;
            Body = body ?? string.Empty;
            Zeny = Math.Max(0, zeny);
            Items = items ?? Array.Empty<RoZcInventoryItem>();
        }

        public byte OpenType { get; }
        public ulong MailId { get; }
        public string Body { get; }
        public long Zeny { get; }
        public RoZcInventoryItem[] Items { get; }
    }

    public readonly struct RoZcRodexSendAck {
        public RoZcRodexSendAck(byte result) {
            Result = result;
        }

        public byte Result { get; }
        public bool Success => Result == 0;
    }

    public readonly struct RoZcRodexAttachmentAck {
        public RoZcRodexAttachmentAck(ulong mailId, byte openType, byte result) {
            MailId = mailId;
            OpenType = openType;
            Result = result;
        }

        public ulong MailId { get; }
        public byte OpenType { get; }
        public byte Result { get; }
        public bool Success => Result == 0;
    }

    public readonly struct RoZcRodexDeleteAck {
        public RoZcRodexDeleteAck(byte openType, ulong mailId) {
            OpenType = openType;
            MailId = mailId;
        }

        public byte OpenType { get; }
        public ulong MailId { get; }
    }

    public readonly struct RoZcRodexAddItemAck {
        public RoZcRodexAddItemAck(byte result, ushort bagIndex, ushort count, RoZcInventoryItem item, ushort weight) {
            Result = result;
            BagIndex = bagIndex;
            Count = count;
            Item = item;
            Weight = weight;
        }

        public byte Result { get; }
        public ushort BagIndex { get; }
        public ushort Count { get; }
        public RoZcInventoryItem Item { get; }
        public ushort Weight { get; }
        public bool Success => Result == 0;
    }

    public readonly struct RoZcRodexRemoveItemAck {
        public RoZcRodexRemoveItemAck(byte result, ushort bagIndex, ushort count, ushort weight) {
            Result = result;
            BagIndex = bagIndex;
            Count = count;
            Weight = weight;
        }

        public byte Result { get; }
        public ushort BagIndex { get; }
        public ushort Count { get; }
        public ushort Weight { get; }
        public bool Success => Result != 0;
    }

    public readonly struct RoZcRodexOpenWriteAck {
        public RoZcRodexOpenWriteAck(string receiverName, byte result) {
            ReceiverName = receiverName ?? string.Empty;
            Result = result;
        }

        public string ReceiverName { get; }
        public byte Result { get; }
        public bool Success => Result != 0;
    }

    public readonly struct RoZcRodexCheckNameAck {
        public RoZcRodexCheckNameAck(uint characterId, short job, short level, string characterName) {
            CharacterId = characterId;
            Job = job;
            Level = level;
            CharacterName = characterName ?? string.Empty;
        }

        public uint CharacterId { get; }
        public short Job { get; }
        public short Level { get; }
        public string CharacterName { get; }
        public bool Success => CharacterId != 0;
    }

    public readonly struct RoZcAchievementEntry {
        public RoZcAchievementEntry(uint id, bool isCompleted, uint[] objectives, uint? completedAt, bool rewardClaimed) {
            Id = id;
            IsCompleted = isCompleted;
            Objectives = CopyObjectives(objectives);
            CompletedAt = completedAt;
            RewardClaimed = rewardClaimed;
        }

        public uint Id { get; }
        public bool IsCompleted { get; }
        public uint[] Objectives { get; }
        public uint? CompletedAt { get; }
        public bool RewardClaimed { get; }

        private static uint[] CopyObjectives(uint[] source) {
            var copy = new uint[RoMapPacketParser.AchievementObjectiveCount];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoZcAchievementList {
        public RoZcAchievementList(
            uint totalAchievements,
            uint totalPoints,
            ushort rank,
            uint currentRankPoints,
            uint nextRankPoints,
            RoZcAchievementEntry[] entries) {
            TotalAchievements = totalAchievements;
            TotalPoints = totalPoints;
            Rank = rank;
            CurrentRankPoints = currentRankPoints;
            NextRankPoints = nextRankPoints;
            Entries = entries ?? Array.Empty<RoZcAchievementEntry>();
        }

        public uint TotalAchievements { get; }
        public uint TotalPoints { get; }
        public ushort Rank { get; }
        public uint CurrentRankPoints { get; }
        public uint NextRankPoints { get; }
        public RoZcAchievementEntry[] Entries { get; }
    }

    public static class RoMapPacketParser {
        private const int ScNotifyBanLength = 3;
        private const int ZcAidLength = 6;
        private const int ZcRefuseEnterLength = 3;
        private const int ZcNotifyTimeLength = 6;
        private const int ZcNpcAckMapMoveLength = 22;
        private const int ZcNpcAckServerMoveLength = 28;
        private const int ZcNpcAckServerMoveDomainLength = 156;
        private const int ZcWaitDialogLength = 6;
        private const int ZcCloseDialogLength = 6;
        private const int ZcOpenEditDlgLength = 6;
        private const int ZcShowImageLength = 19;
        private const int ZcCompassLength = 23;
        private const int ZcOpenEditDlgStrLength = 6;
        private const int ZcShowImage2Length = 67;
        private const int ZcClearDialogLength = 6;
        private const int ZcSelectDealTypeLength = 6;
        private const int NpcShopBuyRecordLength20200401 = 13;
        private const int NpcShopBuyRecordLength20240619 = 19;
        private const int NpcShopSellRecordLength = 10;
        private const int ZcNpcShopResultLength = 3;
        private const int VendingShopNameLength = 80;
        private const int ZcOpenStoreLength = 4;
        private const int ZcStoreEntryLength = 86;
        private const int ZcDisappearEntryLength = 6;
        private const int PlayerShopBuyerLegacyRecordLength = 22;
        private const int PlayerShopBuyerRecordLength20200401 = 63;
        private const int PlayerShopBuyerRecordLengthWithGrade = 64;
        private const int PlayerShopOwnRecordLength20200401 = 57;
        private const int PlayerShopOwnRecordLengthWithGrade = 58;
        private const int ZcPlayerShopPurchaseResultLength = 7;
        private const int ZcDeleteItemFromMcStoreLength = 6;
        private const int ZcDeleteItemFromMcStore2Length = 18;
        private const int ZcAckOpenStore2Length = 3;
        private const int NpcMarketRecordLength20240619 = 15;
        private const int NpcMarketRecordLength20200401 = 13;
        private const int NpcMarketOpen2RecordLength20240619 = 19;
        private const int NpcMarketOpen2RecordLength20200401 = 17;
        private const int NpcMarketResultHeaderLength = 5;
        private const int NpcMarketResult2HeaderLength = 6;
        private const int NpcMarketResultRecordLength20240619 = 10;
        private const int NpcMarketResultRecordLength20200401 = 8;
        private const int NpcBarterRecordLength20240619 = 31;
        private const int NpcExpandedBarterHeaderLength = 8;
        private const int NpcExpandedBarterItemRecordBaseLength20240619 = 32;
        private const int NpcExpandedBarterCurrencyRecordLength20240619 = 12;
        private const int ZcNotifyStoreItemCountInfoLength = 6;
        private const int StorageListHeaderLength = 4;
        private const int StorageNamedListHeaderLength = 28;
        private const int StorageLegacyNormalRecordLength = 10;
        private const int StorageLegacyEquipmentRecordLength = 20;
        private const int StorageModernNormalRecordLength = 18;
        private const int StorageModernEquipmentRecordLength = 24;
        private const int StorageModernEquipment2RecordLength = 28;
        private const int StorageModernNormal2RecordLength = 22;
        private const int StorageLatestNormalRecordLength = 24;
        private const int StorageLatestEquipmentRecordLength = 31;
        private const int StorageLatestEquipment2RecordLength = 57;
        private const int ZcAddItemToStoreLength = 21;
        private const int ZcAddItemToStore2Length = 22;
        private const int ZcAddItemToStore3Length20200401 = 57;
        private const int ZcAddItemToStore4Length20240619 = 58;
        private const int ZcDeleteItemFromStoreLength = 8;
        private const int ZcCloseStoreLength = 2;
        private const int ZcNotifyCartItemCountInfoLength = 14;
        private const int CartListHeaderLength = 4;
        private const int CartLegacyNormalRecordLength = 10;
        private const int CartLegacyEquipmentRecordLength = 20;
        private const int CartModernNormalRecordLength = 22;
        private const int CartModernEquipmentRecordLength = 28;
        private const int CartLatestNormalRecordLength = 24;
        private const int CartLatestEquipmentRecordLength = 31;
        private const int CartLatestEquipment2RecordLength = 57;
        private const int ZcAddItemToCartLength = 21;
        private const int ZcAddItemToCart2Length = 22;
        private const int ZcAddItemToCart3Length = 57;
        private const int ZcAddItemToCart4Length = 58;
        private const int ZcDeleteItemFromCartLength = 8;
        private const int ZcBankingCheckLength = 12;
        private const int ZcAckBankingTransactionLength = 16;
        private const int ZcAckBankSimpleLength = 4;
        private const int ZcTradeRequestLength = 26;
        private const int ZcTradeRequest2Length = 32;
        private const int ZcTradeAckLength = 3;
        private const int ZcTradeAck2Length = 9;
        private const int ZcTradeAddItemAckLength = 5;
        private const int ZcTradeConcludeLength = 3;
        private const int ZcTradeCancelLength = 2;
        private const int ZcTradeSubmitResultLength = 3;
        private const int ZcAcceptEnter2Length = 13;
        private const int ZcAcceptEnterLength = 11;
        private const int ZcAcceptEnter3Length = 14;
        private const int ZcChangeDirectionLength = 9;
        private const int ZcSpriteChangeLength = 8;
        private const int ZcSpriteChange2Length = 15;
        private const int ZcSpriteChange2LegacyLength = 11;
        private const int ZcItemEntryLength = 19;
        private const int ZcItemFallEntryLength = 17;
        private const int ZcItemFallEntry2Length = 19;
        private const int ZcItemFallEntry5Length = 24;
        private const int ZcItemDisappearLength = 6;
        private const int ZcNotifyActLength = 29;
        private const int ZcNotifyAct2Length = 33;
        private const int ZcNotifyAct3Length = 34;
        private const int ZcEmotionLength = 7;
        private const int ZcStateChangeLength = 13;
        private const int ZcStateChange3Length = 15;
        private const int ZcMsgStateChangeLength = 9;
        private const int ZcMsgStateChange2Length = 25;
        private const int ZcMsgStateChangeTimedLength = 24;
        private const int ZcMsgStateChange3Length = 29;
        private const int ZcMsgStateChangeTimedWithTotalLength = 28;
        private const int ZcNotifyVanishLength = 7;
        private const int ZcResurrectionLength = 8;
        private const int ZcNotifyMoveLength = 16;
        private const int ZcNotifyPlayerMoveLength = 12;
        private const int ZcStopMoveLength = 10;
        private const int ActorNameLength = 24;
        private const int ZcAckReqNameLength = 30;
        private const int ZcAckReqNameAllLength = 102;
        private const int ZcAckReqNameAll2Length = 106;
        private const int ZcAckReqNameAllNpcLength = 58;
        private const int ZcHpInfoLength = 14;
        private const int ZcInventoryEndLength = 4;
        private const int ZcDeleteItemFromBodyLength = 8;
        private const int ZcUseItemAckLength = 7;
        private const int ZcUseItemAck2Length20200401 = 15;
        private const int ZcAckItemCompositionLength = 7;
        private const int ZcAckItemIdentifyLength = 5;
        private const int ZcWarpListLength = 68;
        private const int ZcNotifyMapInfoLength = 4;
        private const int WarpPointMapNameLength = 16;
        private const int LegacyWarpPointCount = 4;
        private const int NormalItemRecordLength4 = 24;
        private const int EquipmentItemRecordLength4 = 31;
        private const int EquipmentItemRecordLength5 = 57;
        private const int NormalItemRecordLength20200401 = 34;
        private const int EquipmentItemRecordLength20200401 = 67;
        private const int EquipmentItemRecordLength20240619 = 68;
        private const int EquipSwitchListRecordLength = 6;
        private const int ZcAckWearEquipV5Length = 11;
        private const int ZcReqTakeoffEquipAckLength = 7;
        private const int ZcReqTakeoffEquipAck2Length = 7;
        private const int ZcAckTakeoffEquipV5Length = 9;
        private const int ZcEquipArrowLength = 4;
        private const int ZcItemThrowAckLength = 6;
        private const int ZcItemPickupAck7Length = 69;
        private const int ZcItemPickupAck8Length = 70;
        private const int ZcParChangeLength = 8;
        private const int ZcRecovery2Length = 8;
        private const int ZcLongLongParChangeLength = 12;
        private const int ZcStatusChangeAckLength = 6;
        private const int ZcStatusLength = 44;
        private const int ZcCoupleStatusLength = 14;
        private const int ZcStatusChangeLength = 5;
        private const int ZcNotifyWeightPercentageLength = 6;
        private const int ZcAttackRangeLength = 4;
        private const int ZcDispelLength = 6;
        private const int ZcSpiritsLength = 8;
        private const int ZcNotifyEffect2Length = 10;
        private const int ZcNotifyEffect3Length = 14;
        private const int ZcNpcShowEfstUpdateLength = 18;
        private const int ZcAckToUseSkillLength = 14;
        private const int ZcAttackFailureForDistanceLength = 16;
        private const int ZcActionFailureLength = 4;
        private const int ZcUseSkillAckLegacyLength = 24;
        private const int ZcUseSkillAck2Length = 25;
        private const int ZcUseSkillAck3Length = 29;
        private const int ZcNotifySkillLegacyLength = 31;
        private const int ZcNotifySkill2Length = 33;
        private const int ZcUseSkillLength = 17;
        private const int ZcNotifyEffectLength = 10;
        private const int ZcNotifyGroundSkillLength = 18;
        private const int ZcSkillDisappearLength = 6;
        private const int ZcSkillPostDelayLength = 8;
        private const int ZcNotifySkillPositionLength = 35;
        private const int ZcSkillEntryLength = 16;
        private const int ZcSkillEntry2Length = 97;
        private const int ZcSkillEntry3MinimumLength = 21;
        private const int ZcSkillEntry4MinimumLength = 22;
        private const int ZcSkillEntry5MinimumLength = 23;
        private const int ZcSkillInfoUpdateLength = 11;
        private const int ZcAddSkillLength = 39;
        private const int ZcAutoRunSkillLength = 39;
        private const int ZcSkillInfoUpdate2Length = 15;
        private const int SkillInfoLegacyRecordLength = 37;
        private const int SkillInfoCurrentRecordLength = 15;
        private const int ZcShortcutKeyListLength = 271;
        private const int ZcMsgLength = 4;
        private const int ZcMsgValueLength = 8;
        private const int ZcMsgSkillLength = 8;
        private const int ZcMsgColorLength = 8;
        private const int ShortcutKeyCount = 38;
        private const int ShortcutKeyRecordLength = 7;
        private const int CardSlotCount = 4;
        public const int AchievementObjectiveCount = 10;
        private const int ZcNotifyUnreadMailLength = 3;
        private const int AchievementListHeaderLength = 22;
        private const int AchievementListEntryLength = 50;
        private const int RodexReadHeaderLength = 24;
        private const int RodexReadItemRecordLength = 60;
        private const int QuestObjectiveAddRecordLength = 46;
        private const int QuestObjectiveListRecordLength = 48;
        private const int QuestObjectiveCounterLegacyRecordLength = 12;
        private const int QuestObjectiveCounterModernRecordLength = 16;
        private const int PartyInfoLegacyRecordLength = 46;
        private const int PartyInfo2RecordLength = 50;
        private const int PartyInfoRecordLength = 54;
        private const int FriendListRecordLength = 32;
        private const int FriendListIdRecordLength = 8;
        private const int ZcAckReqNameByGidLength = 30;
        private const int ZcAckReqNameByGid2Length = 32;
        private const int ZcPartyCreateAckLength = 3;
        private const int ZcQuestDeleteLength = 6;
        private const int ZcQuestActiveLength = 7;
        private const int ZcQuestAddLength = 155;
        private const int ZcPartyOptionLegacyLength = 6;
        private const int ZcPartyOptionLength = 8;
        private const int ZcPartyConfigLength = 3;
        private const int ZcPartyLeaderChangedLength = 10;
        private const int ZcPartyMemberInfoLegacyLength = 79;
        private const int ZcPartyMemberInfo2Length = 81;
        private const int ZcPartyMemberInfo3Length = 85;
        private const int ZcPartyMemberInfoLength = 89;
        private const int ZcPartyMemberLeaveLength = 31;
        private const int ZcPartyPositionLength = 10;
        private const int ZcPartyHpLegacyLength = 10;
        private const int ZcPartyHpLength = 14;
        private const int ZcPartyAliveLength = 7;
        private const int ZcPartyJobAndLevelLength = 10;
        private const int ZcPartyInviteLength = 30;
        private const int ZcPartyInviteAckLength = 27;
        private const int ZcPartyInviteAck2Length = 30;
        private const int ZcFriendStateLength = 35;
        private const int ZcFriendRequestLength = 34;
        private const int ZcFriendAddResultLength = 36;
        private const int ZcFriendDeleteLength = 10;
        private const int ZcGuildLeaveAckLength = 66;
        private const int ZcGuildInviteResultLength = 3;
        private const int ZcGuildInviteLength = 30;
        private const int ZcGuildBelongInfoLength = 43;
        private const int ZcGuildNoticeLength = 182;
        private const int ZcGuildMemberInfoLength = 106;
        private const int ZcPetPropertyLength = 37;
        private const int ZcFeedPetLength = 7;
        private const int ZcPetChangeStateLength = 11;
        private const int ZcPetActLength = 10;
        private const int ZcHomunculusPropertyLength = 77;
        private const int ZcHomunculusChangeStateLength = 12;
        private const int ZcHomunculusParameterChangeLength = 8;
        private const int ZcMercenaryInfoLength = 80;
        private const int ZcMapPropertyLength = 4;
        private const int ZcMapProperty2Length = 8;

        public static RoScNotifyBan ParseScNotifyBan(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ScNotifyBan, ScNotifyBanLength);
            return new RoScNotifyBan(reader.ReadByte());
        }

        public static RoZcAid ParseZcAid(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAid, ZcAidLength);
            return new RoZcAid(reader.ReadInt32());
        }

        public static RoZcRefuseEnter ParseZcRefuseEnter(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcRefuseEnter, ZcRefuseEnterLength);
            return new RoZcRefuseEnter(reader.ReadByte());
        }

        public static RoZcNotifyTime ParseZcNotifyTime(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyTime, ZcNotifyTimeLength);
            return new RoZcNotifyTime(reader.ReadUInt32());
        }

        public static RoZcNpcMapMove ParseZcNpcAckMapMove(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNpcAckMapMove, ZcNpcAckMapMoveLength);
            string mapName = reader.ReadFixedString(16, encoding);
            ushort x = reader.ReadUInt16();
            ushort y = reader.ReadUInt16();
            return new RoZcNpcMapMove(mapName, x, y);
        }

        public static RoZcNpcServerMove ParseZcNpcAckServerMove(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNpcAckServerMove, ZcNpcAckServerMoveLength);
            string mapName = reader.ReadFixedString(16, encoding);
            ushort x = reader.ReadUInt16();
            ushort y = reader.ReadUInt16();
            string address = FormatIpv4(reader.ReadBytes(4));
            ushort port = reader.ReadUInt16();
            return new RoZcNpcServerMove(mapName, x, y, address, port, string.Empty);
        }

        public static RoZcNpcServerMove ParseZcNpcAckServerMoveDomain(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNpcAckServerMoveDomain, ZcNpcAckServerMoveDomainLength);
            string mapName = reader.ReadFixedString(16, encoding);
            ushort x = reader.ReadUInt16();
            ushort y = reader.ReadUInt16();
            string address = FormatIpv4(reader.ReadBytes(4));
            ushort port = reader.ReadUInt16();
            string domain = reader.ReadFixedString(128, encoding);
            return new RoZcNpcServerMove(mapName, x, y, address, port, domain);
        }

        public static RoZcNpcDialogText ParseZcSayDialog(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcSayDialog);
            uint npcId = reader.ReadUInt32();
            string message = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcNpcDialogText(npcId, message);
        }

        public static RoZcNpcDialogSignal ParseZcWaitDialog(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcWaitDialog, ZcWaitDialogLength);
            return new RoZcNpcDialogSignal(reader.ReadUInt32());
        }

        public static RoZcNpcDialogSignal ParseZcCloseDialog(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcCloseDialog, ZcCloseDialogLength);
            return new RoZcNpcDialogSignal(reader.ReadUInt32());
        }

        public static RoZcNpcDialogText ParseZcMenuList(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcMenuList);
            uint npcId = reader.ReadUInt32();
            string menu = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcNpcDialogText(npcId, menu);
        }

        public static RoZcShowScript ParseZcShowScript(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcShowScript);
            uint entityId = reader.ReadUInt32();
            string message = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcShowScript(entityId, message);
        }

        public static RoZcNpcDialogSignal ParseZcOpenEditDlg(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcOpenEditDlg, ZcOpenEditDlgLength);
            return new RoZcNpcDialogSignal(reader.ReadUInt32());
        }

        public static RoZcNpcCutin ParseZcShowImage(byte[] frame, Encoding encoding = null) {
            return ParseZcShowImage(frame, RoPacketIds.ZcShowImage, ZcShowImageLength, 16, encoding);
        }

        public static RoZcCompass ParseZcCompass(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcCompass, ZcCompassLength);
            uint npcAid = reader.ReadUInt32();
            int typeRaw = reader.ReadInt32();
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            byte markerId = reader.ReadByte();
            uint colorRaw = reader.ReadUInt32();
            return new RoZcCompass(npcAid, typeRaw, x, y, markerId, colorRaw);
        }

        public static RoZcUiOpen ParseZcUiOpen(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcUiOpen, 7);
            byte uiType = reader.ReadByte();
            int data = reader.ReadInt32();
            return new RoZcUiOpen(uiType, data);
        }

        public static RoZcAttendanceResponse ParseZcAttendanceResponse(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAttendanceResponse, 10);
            int unknown = reader.ReadInt32();
            int data = reader.ReadInt32();
            return new RoZcAttendanceResponse(unknown, data);
        }

        public static RoZcNpcDialogSignal ParseZcOpenEditDlgStr(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcOpenEditDlgStr, ZcOpenEditDlgStrLength);
            return new RoZcNpcDialogSignal(reader.ReadUInt32());
        }

        public static RoZcNpcCutin ParseZcShowImage2(byte[] frame, Encoding encoding = null) {
            return ParseZcShowImage(frame, RoPacketIds.ZcShowImage2, ZcShowImage2Length, 64, encoding);
        }

        public static RoZcNpcDialogSignal ParseZcClearDialog(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcClearDialog, ZcClearDialogLength);
            return new RoZcNpcDialogSignal(reader.ReadUInt32());
        }

        public static RoZcSelectDealType ParseZcSelectDealType(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcSelectDealType, ZcSelectDealTypeLength);
            return new RoZcSelectDealType(reader.ReadUInt32());
        }

        public static RoZcNpcShopBuyList ParseZcPcPurchaseItemList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPcPurchaseItemList);
            RequireRecordAlignment(reader, NpcShopBuyRecordLength20200401, RoPacketIds.ZcPcPurchaseItemList);
            var items = new List<RoNpcShopBuyItem>();
            while (!reader.EndOfPacket) {
                uint price = reader.ReadUInt32();
                uint discountPrice = reader.ReadUInt32();
                byte itemType = reader.ReadByte();
                uint itemId = reader.ReadUInt32();
                items.Add(new RoNpcShopBuyItem(itemId, price, discountPrice, itemType));
            }

            return new RoZcNpcShopBuyList(items.ToArray());
        }

        public static RoZcNpcShopBuyList ParseZcPcPurchaseItemList2(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPcPurchaseItemList2);
            RequireRecordAlignment(reader, NpcShopBuyRecordLength20240619, RoPacketIds.ZcPcPurchaseItemList2);
            var items = new List<RoNpcShopBuyItem>();
            while (!reader.EndOfPacket) {
                uint itemId = reader.ReadUInt32();
                uint price = unchecked((uint) reader.ReadInt32());
                uint discountPrice = unchecked((uint) reader.ReadInt32());
                byte itemType = reader.ReadByte();
                ushort viewSprite = reader.ReadUInt16();
                uint location = reader.ReadUInt32();
                items.Add(new RoNpcShopBuyItem(itemId, price, discountPrice, itemType, viewSprite, location));
            }

            return new RoZcNpcShopBuyList(items.ToArray());
        }

        public static RoZcNpcShopSellList ParseZcPcSellItemList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPcSellItemList);
            RequireRecordAlignment(reader, NpcShopSellRecordLength, RoPacketIds.ZcPcSellItemList);
            var items = new List<RoNpcShopSellItem>();
            while (!reader.EndOfPacket) {
                items.Add(new RoNpcShopSellItem(reader.ReadUInt16(), reader.ReadUInt32(), reader.ReadUInt32()));
            }

            return new RoZcNpcShopSellList(items.ToArray());
        }

        public static RoZcNpcShopResult ParseZcPcPurchaseResult(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPcPurchaseResult, ZcNpcShopResultLength);
            return new RoZcNpcShopResult(reader.ReadByte());
        }

        public static RoZcNpcShopResult ParseZcPcSellResult(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPcSellResult, ZcNpcShopResultLength);
            return new RoZcNpcShopResult(reader.ReadByte());
        }

        public static RoZcOpenStore ParseZcOpenStore(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcOpenStore, ZcOpenStoreLength);
            return new RoZcOpenStore(reader.ReadUInt16());
        }

        public static RoZcStoreEntry ParseZcStoreEntry(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcStoreEntry, ZcStoreEntryLength);
            uint ownerAid = reader.ReadUInt32();
            string storeName = reader.ReadFixedString(VendingShopNameLength, encoding);
            return new RoZcStoreEntry(ownerAid, storeName);
        }

        public static RoZcDisappearEntry ParseZcDisappearEntry(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcDisappearEntry, ZcDisappearEntryLength);
            return new RoZcDisappearEntry(reader.ReadUInt32());
        }

        public static RoZcPlayerVendingItemList ParseZcPcPurchaseItemListFromMc(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPcPurchaseItemListFromMc);
            uint ownerAid = reader.ReadUInt32();
            RequireRecordAlignment(reader, PlayerShopBuyerLegacyRecordLength, RoPacketIds.ZcPcPurchaseItemListFromMc);
            var items = new List<RoPlayerVendingItem>();
            while (!reader.EndOfPacket) {
                uint price = reader.ReadUInt32();
                ushort amount = reader.ReadUInt16();
                short index = reader.ReadInt16();
                byte itemType = reader.ReadByte();
                uint itemId = reader.ReadUInt16();
                bool isIdentified = reader.ReadByte() != 0;
                bool isDamaged = reader.ReadByte() != 0;
                byte refineLevel = reader.ReadByte();
                int[] cards = ReadCardIds16(reader);
                items.Add(new RoPlayerVendingItem(price, amount, index, itemType, itemId, isIdentified, isDamaged, refineLevel, cards));
            }

            return new RoZcPlayerVendingItemList(ownerAid, 0, hasUniqueId: false, items.ToArray());
        }

        public static RoZcPlayerVendingItemList ParseZcPcPurchaseItemListFromMc2(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPcPurchaseItemListFromMc2);
            uint ownerAid = reader.ReadUInt32();
            uint uniqueId = reader.ReadUInt32();
            RequireRecordAlignment(reader, PlayerShopBuyerRecordLength20200401, RoPacketIds.ZcPcPurchaseItemListFromMc2);
            return ParsePlayerShopBuyerItems(reader, ownerAid, uniqueId, hasUniqueId: true, hasGrade: false);
        }

        public static RoZcPlayerVendingItemList ParseZcPcPurchaseItemListFromMc3(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPcPurchaseItemListFromMc3);
            uint ownerAid = reader.ReadUInt32();
            uint uniqueId = reader.ReadUInt32();
            RequireRecordAlignment(reader, PlayerShopBuyerRecordLengthWithGrade, RoPacketIds.ZcPcPurchaseItemListFromMc3);
            return ParsePlayerShopBuyerItems(reader, ownerAid, uniqueId, hasUniqueId: true, hasGrade: true);
        }

        public static RoZcPlayerVendingItemList ParseZcPcPurchaseMyItemList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPcPurchaseMyItemList);
            uint ownerAid = reader.ReadUInt32();
            RequireRecordAlignment(reader, PlayerShopOwnRecordLength20200401, RoPacketIds.ZcPcPurchaseMyItemList);
            return ParsePlayerShopOwnItems(reader, ownerAid, hasGrade: false);
        }

        public static RoZcPlayerVendingItemList ParseZcPcPurchaseMyItemList2(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPcPurchaseMyItemList2);
            uint ownerAid = reader.ReadUInt32();
            RequireRecordAlignment(reader, PlayerShopOwnRecordLengthWithGrade, RoPacketIds.ZcPcPurchaseMyItemList2);
            return ParsePlayerShopOwnItems(reader, ownerAid, hasGrade: true);
        }

        public static RoZcPlayerVendingPurchaseResult ParseZcPcPurchaseResultFromMc(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPcPurchaseResultFromMc, ZcPlayerShopPurchaseResultLength);
            ushort index = reader.ReadUInt16();
            ushort currentCount = reader.ReadUInt16();
            byte result = reader.ReadByte();
            return new RoZcPlayerVendingPurchaseResult(index, currentCount, result);
        }

        public static RoZcDeleteItemFromMcStore ParseZcDeleteItemFromMcStore(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcDeleteItemFromMcStore, ZcDeleteItemFromMcStoreLength);
            ushort index = reader.ReadUInt16();
            ushort amount = reader.ReadUInt16();
            return new RoZcDeleteItemFromMcStore(index, amount);
        }

        public static RoZcDeleteItemFromMcStore ParseZcDeleteItemFromMcStore2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcDeleteItemFromMcStore2, ZcDeleteItemFromMcStore2Length);
            ushort index = reader.ReadUInt16();
            ushort amount = reader.ReadUInt16();
            uint buyerCid = reader.ReadUInt32();
            uint date = reader.ReadUInt32();
            uint zeny = reader.ReadUInt32();
            return new RoZcDeleteItemFromMcStore(index, amount, buyerCid, date, zeny, hasBuyerDetails: true);
        }

        public static RoZcAckOpenStore2 ParseZcAckOpenStore2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckOpenStore2, ZcAckOpenStore2Length);
            return new RoZcAckOpenStore2(reader.ReadByte());
        }

        public static RoZcNpcMarketList ParseZcNpcMarketOpen(byte[] frame) {
            return ParseZcNpcMarketOpen(frame, RoPacketIds.ZcNpcMarketOpen, NpcMarketRecordLength20240619, hasLocation: false);
        }

        public static RoZcNpcMarketList ParseZcNpcMarketOpen2(byte[] frame) {
            return ParseZcNpcMarketOpen(frame, RoPacketIds.ZcNpcMarketOpen2, NpcMarketOpen2RecordLength20240619, hasLocation: true);
        }

        public static RoZcNpcMarketPurchaseResult ParseZcNpcMarketPurchaseResult(byte[] frame) {
            return ParseZcNpcMarketPurchaseResult(frame, RoPacketIds.ZcNpcMarketPurchaseResult, resultLength: 1);
        }

        public static RoZcNpcMarketPurchaseResult ParseZcNpcMarketPurchaseResult2(byte[] frame) {
            return ParseZcNpcMarketPurchaseResult(frame, RoPacketIds.ZcNpcMarketPurchaseResult2, resultLength: 2);
        }

        public static RoZcNpcBarterList ParseZcNpcBarterMarketItemInfo(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcNpcBarterMarketItemInfo);
            RequireRecordAlignment(reader, NpcBarterRecordLength20240619, RoPacketIds.ZcNpcBarterMarketItemInfo);
            var items = new List<RoNpcBarterItem>();
            while (!reader.EndOfPacket) {
                uint itemId = reader.ReadUInt32();
                byte itemType = reader.ReadByte();
                uint amount = reader.ReadUInt32();
                uint currencyId = reader.ReadUInt32();
                uint currencyAmount = reader.ReadUInt32();
                uint weight = reader.ReadUInt32();
                uint shopIndex = reader.ReadUInt32();
                ushort viewSprite = reader.ReadUInt16();
                uint location = reader.ReadUInt32();
                // the client draws the price item's icon even when its amount is 0 (UIBartermarketWnd::OnDraw 0x7a5540)
                var currencies = currencyId == 0
                    ? Array.Empty<RoNpcBarterCurrency>()
                    : new[] { new RoNpcBarterCurrency(currencyId, 0, currencyAmount, 0) };
                items.Add(new RoNpcBarterItem(
                    itemId,
                    itemType,
                    amount,
                    amount,
                    weight,
                    shopIndex,
                    location,
                    0,
                    viewSprite,
                    currencies));
            }

            return new RoZcNpcBarterList(items.ToArray());
        }

        public static RoZcNpcBarterList ParseZcNpcExpandedBarterMarketItemInfo(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcNpcExpandedBarterMarketItemInfo);
            if (reader.Remaining < 4) {
                throw new ArgumentException(
                    $"Packet 0x{RoPacketIds.ZcNpcExpandedBarterMarketItemInfo:x4} must include an expanded barter item count.",
                    nameof(frame));
            }

            int itemCount = reader.ReadInt32();
            if (itemCount < 0) {
                throw new ArgumentException(
                    $"Packet 0x{RoPacketIds.ZcNpcExpandedBarterMarketItemInfo:x4} reported negative item count {itemCount}.",
                    nameof(frame));
            }

            var items = new List<RoNpcBarterItem>(itemCount);
            for (int i = 0; i < itemCount; i++) {
                RequireRemaining(reader, NpcExpandedBarterItemRecordBaseLength20240619, RoPacketIds.ZcNpcExpandedBarterMarketItemInfo);
                uint itemId = reader.ReadUInt32();
                ushort itemType = reader.ReadUInt16();
                uint amount = reader.ReadUInt32();
                uint weight = reader.ReadUInt32();
                uint shopIndex = reader.ReadUInt32();
                uint price = reader.ReadUInt32();
                ushort viewSprite = reader.ReadUInt16();
                uint location = reader.ReadUInt32();
                uint currencyCount = reader.ReadUInt32();
                if (currencyCount > int.MaxValue) {
                    throw new ArgumentException(
                        $"Packet 0x{RoPacketIds.ZcNpcExpandedBarterMarketItemInfo:x4} reported unsupported currency count {currencyCount}.",
                        nameof(frame));
                }

                var currencies = new RoNpcBarterCurrency[(int) currencyCount];
                for (int currencyIndex = 0; currencyIndex < currencies.Length; currencyIndex++) {
                    RequireRemaining(reader, NpcExpandedBarterCurrencyRecordLength20240619, RoPacketIds.ZcNpcExpandedBarterMarketItemInfo);
                    currencies[currencyIndex] = new RoNpcBarterCurrency(
                        reader.ReadUInt32(),
                        reader.ReadUInt16(),
                        reader.ReadUInt32(),
                        reader.ReadUInt16());
                }

                items.Add(new RoNpcBarterItem(
                    itemId,
                    itemType,
                    amount,
                    amount,
                    weight,
                    shopIndex,
                    location,
                    price,
                    viewSprite,
                    currencies));
            }

            if (!reader.EndOfPacket) {
                throw new ArgumentException(
                    $"Packet 0x{RoPacketIds.ZcNpcExpandedBarterMarketItemInfo:x4} has {reader.Remaining} trailing payload byte(s).",
                    nameof(frame));
            }

            return new RoZcNpcBarterList(items.ToArray());
        }

        public static RoZcStorageAmount ParseZcNotifyStoreItemCountInfo(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyStoreItemCountInfo, ZcNotifyStoreItemCountInfoLength);
            return new RoZcStorageAmount(reader.ReadUInt16(), reader.ReadUInt16());
        }

        public static RoZcInventoryItemList ParseZcStorageItemList(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame);
            ushort packetId = reader.ReadUInt16();
            ushort packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException(
                    $"Packet 0x{packetId:x4} expected length {packetLength} but frame has {frame.Length} byte(s).",
                    nameof(frame));
            }

            string storageName = string.Empty;
            int headerLength = StorageListHeaderLength;
            switch (packetId) {
                case RoPacketIds.ZcStorageItemListNormal4:
                case RoPacketIds.ZcStorageItemListEquip4:
                case RoPacketIds.ZcStorageItemListEquip5:
                    storageName = reader.ReadFixedString(24, encoding);
                    headerLength = StorageNamedListHeaderLength;
                    break;
            }

            int recordLength = ResolveStorageListRecordLength(packetId);
            RequirePacketRecordAlignment(packetLength, headerLength, recordLength, packetId);
            int count = (packetLength - headerLength) / recordLength;
            var items = new RoZcInventoryItem[count];
            for (int i = 0; i < count; i++) {
                items[i] = ParseStorageListItem(reader, packetId);
            }

            return new RoZcInventoryItemList((byte) RoInventoryType.Storage, IsStorageEquipmentList(packetId), items, storageName);
        }

        public static RoZcStorageItem ParseZcAddItemToStore3(byte[] frame) {
            return ParseZcAddItemToStore(frame);
        }

        public static RoZcStorageItem ParseZcAddItemToStore(byte[] frame) {
            var reader = CreateFrameReader(frame);
            ushort packetId = reader.ReadUInt16();
            switch (packetId) {
                case RoPacketIds.ZcAddItemToStore:
                    EnsureFixedLength(frame, packetId, ZcAddItemToStoreLength);
                    return ParseLegacyStorageAddedItem(reader);
                case RoPacketIds.ZcAddItemToStore2:
                    EnsureFixedLength(frame, packetId, ZcAddItemToStore2Length);
                    return ParseModernStorageAddedItem(reader);
                case RoPacketIds.ZcAddItemToStore3:
                    EnsureFixedLength(frame, packetId, ZcAddItemToStore3Length20200401);
                    return ParseAdvancedStorageAddedItem(reader);
                case RoPacketIds.ZcAddItemToStore4:
                    EnsureFixedLength(frame, packetId, ZcAddItemToStore4Length20240619);
                    return ParseLatestStorageAddedItem(reader);
                default:
                    throw new InvalidOperationException($"Opcode 0x{packetId:x4} is not a supported storage item-add packet.");
            }
        }

        public static RoZcStorageItemDelete ParseZcDeleteItemFromStore(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcDeleteItemFromStore, ZcDeleteItemFromStoreLength);
            return new RoZcStorageItemDelete(reader.ReadUInt16(), reader.ReadUInt32());
        }

        public static void ParseZcCloseStore(byte[] frame) {
            CreateFixedFrameReader(frame, RoPacketIds.ZcCloseStore, ZcCloseStoreLength);
        }

        public static RoZcCartAmount ParseZcNotifyCartItemCountInfo(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyCartItemCountInfo, ZcNotifyCartItemCountInfoLength);
            return new RoZcCartAmount(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static RoZcInventoryItemList ParseZcCartItemList(byte[] frame) {
            var reader = CreateFrameReader(frame);
            ushort packetId = reader.ReadUInt16();
            ushort packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException(
                    $"Packet 0x{packetId:x4} expected length {packetLength} but frame has {frame.Length} byte(s).",
                    nameof(frame));
            }

            int recordLength = ResolveCartListRecordLength(packetId);
            RequirePacketRecordAlignment(packetLength, CartListHeaderLength, recordLength, packetId);
            int count = (packetLength - CartListHeaderLength) / recordLength;
            var items = new RoZcInventoryItem[count];
            for (int i = 0; i < count; i++) {
                items[i] = ParseCartListItem(reader, packetId);
            }

            return new RoZcInventoryItemList((byte) RoInventoryType.Cart, IsCartEquipmentList(packetId), items);
        }

        public static RoZcStorageItem ParseZcAddItemToCart(byte[] frame) {
            var reader = CreateFrameReader(frame);
            ushort packetId = reader.ReadUInt16();
            switch (packetId) {
                case RoPacketIds.ZcAddItemToCart:
                    EnsureFixedLength(frame, packetId, ZcAddItemToCartLength);
                    return ParseLegacyStorageAddedItem(reader);
                case RoPacketIds.ZcAddItemToCart2:
                    EnsureFixedLength(frame, packetId, ZcAddItemToCart2Length);
                    return ParseModernStorageAddedItem(reader);
                case RoPacketIds.ZcAddItemToCart3:
                    EnsureFixedLength(frame, packetId, ZcAddItemToCart3Length);
                    return ParseAdvancedStorageAddedItem(reader);
                case RoPacketIds.ZcAddItemToCart4:
                    EnsureFixedLength(frame, packetId, ZcAddItemToCart4Length);
                    return ParseLatestStorageAddedItem(reader);
                default:
                    throw new InvalidOperationException($"Opcode 0x{packetId:x4} is not a supported cart item-add packet.");
            }
        }

        public static RoZcStorageItemDelete ParseZcDeleteItemFromCart(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcDeleteItemFromCart, ZcDeleteItemFromCartLength);
            return new RoZcStorageItemDelete(reader.ReadUInt16(), reader.ReadUInt32());
        }

        public static void ParseZcCartOff(byte[] frame) {
            CreateFixedFrameReader(frame, RoPacketIds.ZcCartOff, 2);
        }

        public static RoZcBankCheck ParseZcBankingCheck(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcBankingCheck, ZcBankingCheckLength);
            return new RoZcBankCheck(reader.ReadInt64(), reader.ReadInt16());
        }

        public static RoZcBankTransactionAck ParseZcAckBankingDeposit(byte[] frame) {
            return ParseZcAckBankingTransaction(frame, RoPacketIds.ZcAckBankingDeposit);
        }

        public static RoZcBankTransactionAck ParseZcAckBankingWithdraw(byte[] frame) {
            return ParseZcAckBankingTransaction(frame, RoPacketIds.ZcAckBankingWithdraw);
        }

        public static RoZcBankSimpleAck ParseZcAckBankOpen(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckBankOpen, ZcAckBankSimpleLength);
            return new RoZcBankSimpleAck(reader.ReadUInt16());
        }

        public static RoZcBankSimpleAck ParseZcAckBankClose(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckBankClose, ZcAckBankSimpleLength);
            return new RoZcBankSimpleAck(reader.ReadUInt16());
        }

        private static RoZcBankTransactionAck ParseZcAckBankingTransaction(byte[] frame, ushort packetId) {
            var reader = CreateFixedFrameReader(frame, packetId, ZcAckBankingTransactionLength);
            return new RoZcBankTransactionAck(reader.ReadInt16(), reader.ReadInt64(), reader.ReadInt32());
        }

        public static RoZcTradeRequest ParseZcReqExchangeItem(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame);
            ushort packetId = reader.ReadUInt16();
            switch (packetId) {
                case RoPacketIds.ZcReqExchangeItem:
                    EnsureFixedLength(frame, RoPacketIds.ZcReqExchangeItem, ZcTradeRequestLength);
                    return new RoZcTradeRequest(reader.ReadFixedString(24, encoding ?? Encoding.ASCII), 0, 0);
                case RoPacketIds.ZcReqExchangeItem2:
                    EnsureFixedLength(frame, RoPacketIds.ZcReqExchangeItem2, ZcTradeRequest2Length);
                    return new RoZcTradeRequest(
                        reader.ReadFixedString(24, encoding ?? Encoding.ASCII),
                        reader.ReadUInt32(),
                        reader.ReadInt16());
                default:
                    throw new InvalidOperationException($"Opcode 0x{packetId:x4} is not a supported trade request packet.");
            }
        }

        public static RoZcTradeAck ParseZcAckExchangeItem(byte[] frame) {
            var reader = CreateFrameReader(frame);
            ushort packetId = reader.ReadUInt16();
            switch (packetId) {
                case RoPacketIds.ZcAckExchangeItem:
                    EnsureFixedLength(frame, RoPacketIds.ZcAckExchangeItem, ZcTradeAckLength);
                    return new RoZcTradeAck(reader.ReadByte(), 0, 0);
                case RoPacketIds.ZcAckExchangeItem2:
                    EnsureFixedLength(frame, RoPacketIds.ZcAckExchangeItem2, ZcTradeAck2Length);
                    return new RoZcTradeAck(reader.ReadByte(), reader.ReadUInt32(), reader.ReadInt16());
                default:
                    throw new InvalidOperationException($"Opcode 0x{packetId:x4} is not a supported trade ACK packet.");
            }
        }

        public static RoZcTradeAddItemAck ParseZcAckAddExchangeItem(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckAddExchangeItem, ZcTradeAddItemAckLength);
            return new RoZcTradeAddItemAck(reader.ReadUInt16(), reader.ReadByte());
        }

        public static RoZcTradeItem ParseZcAddExchangeItem(byte[] frame) {
            var reader = CreateFrameReader(frame);
            ushort packetId = reader.ReadUInt16();
            switch (packetId) {
                case RoPacketIds.ZcAddExchangeItem:
                    return ParseTradeItemLegacy(reader);
                case RoPacketIds.ZcAddExchangeItem2:
                    return ParseTradeItemTyped(reader, hasOptions: false, wideIds: false, hasLocationAndView: false, hasGrade: false);
                case RoPacketIds.ZcAddExchangeItem3:
                    return ParseTradeItemTyped(reader, hasOptions: true, wideIds: false, hasLocationAndView: false, hasGrade: false);
                case RoPacketIds.ZcAddExchangeItem4:
                    // packetver 2018-11..2020-09: the refine still precedes the four 32-bit cards (61 bytes)
                    return ParseTradeItemTyped(reader, hasOptions: true, wideIds: true, hasLocationAndView: true, hasGrade: false, refineBeforeCards: true);
                case RoPacketIds.ZcAddExchangeItem5:
                    return ParseTradeItemTyped(reader, hasOptions: true, wideIds: true, hasLocationAndView: true, hasGrade: true);
                default:
                    throw new InvalidOperationException($"Opcode 0x{packetId:x4} is not a supported trade item packet.");
            }
        }

        public static RoZcTradeConclude ParseZcConcludeExchangeItem(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcConcludeExchangeItem, ZcTradeConcludeLength);
            return new RoZcTradeConclude(reader.ReadByte() != 0);
        }

        public static void ParseZcCancelExchangeItem(byte[] frame) {
            CreateFixedFrameReader(frame, RoPacketIds.ZcCancelExchangeItem, ZcTradeCancelLength);
        }

        public static RoZcTradeSubmitResult ParseZcExecExchangeItem(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcExecExchangeItem, ZcTradeSubmitResultLength);
            return new RoZcTradeSubmitResult(reader.ReadByte());
        }

        public static RoZcInventoryItemDelete ParseZcDeleteItemFromBody(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcDeleteItemFromBody, ZcDeleteItemFromBodyLength);
            return new RoZcInventoryItemDelete(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16());
        }

        public static RoZcChangeDirection ParseZcChangeDirection(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcChangeDirection, ZcChangeDirectionLength);
            uint aid = reader.ReadUInt32();
            ushort headDirection = reader.ReadUInt16();
            byte directionRaw = reader.ReadByte();
            return new RoZcChangeDirection(aid, headDirection, directionRaw);
        }

        public static RoZcSpriteChange ParseZcSpriteChange(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcSpriteChange, ZcSpriteChangeLength);
            uint gid = reader.ReadUInt32();
            byte typeRaw = reader.ReadByte();
            byte value = reader.ReadByte();
            return new RoZcSpriteChange(gid, typeRaw, value, packsWeaponAndShield: false);
        }

        public static RoZcSpriteChange ParseZcSpriteChange2(byte[] frame) {
            var reader = CreateZcSpriteChange2Reader(frame);
            uint gid = reader.ReadUInt32();
            byte typeRaw = reader.ReadByte();
            uint value = reader.ReadUInt32();
            return new RoZcSpriteChange(gid, typeRaw, value, packsWeaponAndShield: true);
        }

        public static RoZcItemEntry ParseZcItemEntry(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcItemEntry, ZcItemEntryLength);
            uint itemAid = reader.ReadUInt32();
            uint itemId = reader.ReadUInt32();
            bool isIdentified = reader.ReadByte() != 0;
            ushort x = reader.ReadUInt16();
            ushort y = reader.ReadUInt16();
            short count = reader.ReadInt16();
            byte subX = reader.ReadByte();
            byte subY = reader.ReadByte();
            return new RoZcItemEntry(
                itemAid,
                itemId,
                itemTypeRaw: -1,
                isIdentified,
                x,
                y,
                subX,
                subY,
                count,
                isFalling: false,
                showDropEffect: false,
                dropEffectMode: 0);
        }

        public static RoZcItemEntry ParseZcItemFallEntry(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcItemFallEntry, ZcItemFallEntryLength);
            return ParseItemFallEntryPayload(reader, itemIdIsUInt32: false, hasItemType: false, hasDropEffect: false);
        }

        public static RoZcItemEntry ParseZcItemFallEntry2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcItemFallEntry2, ZcItemFallEntry2Length);
            return ParseItemFallEntryPayload(reader, itemIdIsUInt32: false, hasItemType: true, hasDropEffect: false);
        }

        public static RoZcItemEntry ParseZcItemFallEntry5(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcItemFallEntry5, ZcItemFallEntry5Length);
            return ParseItemFallEntryPayload(reader, itemIdIsUInt32: true, hasItemType: true, hasDropEffect: true);
        }

        public static RoZcItemDisappear ParseZcItemDisappear(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcItemDisappear, ZcItemDisappearLength);
            return new RoZcItemDisappear(reader.ReadUInt32());
        }

        public static RoZcNotifyAct ParseZcNotifyAct(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyAct, ZcNotifyActLength);
            return new RoZcNotifyAct(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadUInt16(),
                reader.ReadByte(),
                reader.ReadInt16(),
                isSpDamage: false);
        }

        public static RoZcNotifyAct ParseZcNotifyAct2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyAct2, ZcNotifyAct2Length);
            return new RoZcNotifyAct(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadUInt16(),
                reader.ReadByte(),
                reader.ReadInt32(),
                isSpDamage: false);
        }

        public static RoZcNotifyAct ParseZcNotifyAct3(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyAct3, ZcNotifyAct3Length);
            uint sourceId = reader.ReadUInt32();
            uint targetId = reader.ReadUInt32();
            uint serverTick = reader.ReadUInt32();
            int sourceMotionTime = reader.ReadInt32();
            int targetMotionTime = reader.ReadInt32();
            int damage = reader.ReadInt32();
            bool isSpDamage = reader.ReadByte() != 0;
            return new RoZcNotifyAct(
                sourceId,
                targetId,
                serverTick,
                sourceMotionTime,
                targetMotionTime,
                damage,
                reader.ReadUInt16(),
                reader.ReadByte(),
                reader.ReadInt32(),
                isSpDamage);
        }

        public static RoZcEmotion ParseZcEmotion(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcEmotion, ZcEmotionLength);
            return new RoZcEmotion(reader.ReadUInt32(), reader.ReadByte());
        }

        public static RoZcStateChange ParseZcStateChange(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcStateChange, ZcStateChangeLength);
            return new RoZcStateChange(
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadByte() != 0,
                hasLongEffectState: false);
        }

        public static RoZcStateChange ParseZcStateChange3(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcStateChange3, ZcStateChange3Length);
            return new RoZcStateChange(
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt32(),
                reader.ReadByte() != 0,
                hasLongEffectState: true);
        }

        public static RoZcMsgStateChange ParseZcMsgStateChange(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMsgStateChange, ZcMsgStateChangeLength);
            return new RoZcMsgStateChange(
                reader.ReadUInt16(),
                reader.ReadUInt32(),
                reader.ReadByte() != 0,
                0,
                0,
                0,
                0,
                0, packetId: RoPacketIds.ZcMsgStateChange);
        }

        public static RoZcMsgStateChange ParseZcMsgStateChange2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMsgStateChange2, ZcMsgStateChange2Length);
            ushort statusId = reader.ReadUInt16();
            uint aid = reader.ReadUInt32();
            bool active = reader.ReadByte() != 0;
            uint remainingMilliseconds = reader.ReadUInt32();
            return new RoZcMsgStateChange(
                statusId,
                aid,
                active,
                0,
                remainingMilliseconds,
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(), packetId: RoPacketIds.ZcMsgStateChange2);
        }

        public static RoZcMsgStateChange ParseZcMsgStateChangeTimed(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMsgStateChangeTimed, ZcMsgStateChangeTimedLength);
            uint aid = reader.ReadUInt32();
            ushort statusId = reader.ReadUInt16();
            uint remainingMilliseconds = reader.ReadUInt32();
            return new RoZcMsgStateChange(
                statusId,
                aid,
                remainingMilliseconds > 0u,
                remainingMilliseconds,
                remainingMilliseconds,
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(), packetId: RoPacketIds.ZcMsgStateChangeTimed);
        }

        public static RoZcMsgStateChange ParseZcMsgStateChange3(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMsgStateChange3, ZcMsgStateChange3Length);
            ushort statusId = reader.ReadUInt16();
            uint aid = reader.ReadUInt32();
            bool active = reader.ReadByte() != 0;
            return new RoZcMsgStateChange(
                statusId,
                aid,
                active,
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(), packetId: RoPacketIds.ZcMsgStateChange3);
        }

        public static RoZcMsgStateChange ParseZcMsgStateChangeTimedWithTotal(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMsgStateChangeTimedWithTotal, ZcMsgStateChangeTimedWithTotalLength);
            uint aid = reader.ReadUInt32();
            ushort statusId = reader.ReadUInt16();
            uint totalMilliseconds = reader.ReadUInt32();
            uint remainingMilliseconds = reader.ReadUInt32();
            return new RoZcMsgStateChange(
                statusId,
                aid,
                remainingMilliseconds > 0u || totalMilliseconds > 0u,
                totalMilliseconds,
                remainingMilliseconds,
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(), packetId: RoPacketIds.ZcMsgStateChangeTimedWithTotal);
        }

        public static RoZcNotifyVanish ParseZcNotifyVanish(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyVanish, ZcNotifyVanishLength);
            uint gid = reader.ReadUInt32();
            byte typeRaw = reader.ReadByte();
            return new RoZcNotifyVanish(gid, typeRaw, (RoMapActorVanishType) typeRaw);
        }

        public static RoZcResurrection ParseZcResurrection(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcResurrection, ZcResurrectionLength);
            uint gid = reader.ReadUInt32();
            ushort typeRaw = reader.ReadUInt16();
            return new RoZcResurrection(gid, typeRaw);
        }

        public static RoZcNotifyMove ParseZcNotifyMove(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyMove, ZcNotifyMoveLength);
            uint gid = reader.ReadUInt32();
            RoPacketPositionPair movePath = reader.ReadPositionPair();
            reader.Skip(1);
            uint moveStartTime = reader.ReadUInt32();
            return new RoZcNotifyMove(gid, movePath, moveStartTime);
        }

        public static RoZcNotifyPlayerMove ParseZcNotifyPlayerMove(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyPlayerMove, ZcNotifyPlayerMoveLength);
            uint moveStartTime = reader.ReadUInt32();
            RoPacketPositionPair movePath = reader.ReadPositionPair();
            reader.Skip(1);
            return new RoZcNotifyPlayerMove(moveStartTime, movePath);
        }

        public static RoZcStopMove ParseZcStopMove(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcStopMove, ZcStopMoveLength);
            uint aid = reader.ReadUInt32();
            ushort x = reader.ReadUInt16();
            ushort y = reader.ReadUInt16();
            return new RoZcStopMove(aid, x, y);
        }

        public static RoZcActorIdentity ParseZcAckReqName(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckReqName, ZcAckReqNameLength);
            return new RoZcActorIdentity(
                reader.ReadUInt32(),
                reader.ReadFixedString(ActorNameLength, encoding));
        }

        public static RoZcActorIdentity ParseZcAckReqNameAll(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckReqNameAll, ZcAckReqNameAllLength);
            return new RoZcActorIdentity(
                reader.ReadUInt32(),
                reader.ReadFixedString(ActorNameLength, encoding),
                reader.ReadFixedString(ActorNameLength, encoding),
                reader.ReadFixedString(ActorNameLength, encoding),
                reader.ReadFixedString(ActorNameLength, encoding));
        }

        public static RoZcActorIdentity ParseZcAckReqNameAll2(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckReqNameAll2, ZcAckReqNameAll2Length);
            uint actorId = reader.ReadUInt32();
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            string partyName = reader.ReadFixedString(ActorNameLength, encoding);
            string guildName = reader.ReadFixedString(ActorNameLength, encoding);
            string guildPositionName = reader.ReadFixedString(ActorNameLength, encoding);
            int titleId = reader.ReadInt32();
            return new RoZcActorIdentity(
                actorId,
                name,
                partyName,
                guildName,
                guildPositionName,
                titleId: titleId,
                hasTitleId: true);
        }

        public static RoZcActorIdentity ParseZcAckReqNameAllNpc(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckReqNameAllNpc, ZcAckReqNameAllNpcLength);
            uint actorId = reader.ReadUInt32();
            int groupId = reader.ReadInt32();
            return new RoZcActorIdentity(
                actorId,
                reader.ReadFixedString(ActorNameLength, encoding),
                titleName: reader.ReadFixedString(ActorNameLength, encoding),
                groupId: groupId,
                hasGroupId: true);
        }

        public static RoZcHpInfo ParseZcHpInfo(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcHpInfo, ZcHpInfoLength);
            return new RoZcHpInfo(reader.ReadUInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static RoZcNotifyPlayerChat ParseZcNotifyPlayerChat(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcNotifyPlayerChat);
            string message = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcNotifyPlayerChat(message);
        }

        public static RoZcNotifyPlayerChat ParseZcNotifyChat(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcNotifyChat);
            uint sourceActorId = reader.ReadUInt32();
            string message = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcNotifyPlayerChat(message, sourceActorId);
        }

        public static RoZcNpcChat ParseZcNpcChat(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcNpcChat);
            uint sourceId = reader.ReadUInt32();
            uint colorBgr = reader.ReadUInt32();
            string message = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcNpcChat(sourceId, colorBgr, message);
        }

        public static RoZcSystemText ParseZcBroadcast(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcBroadcast);
            string message = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcSystemText(StripBroadcastMarker(message), packetId: RoPacketIds.ZcBroadcast);
        }

        public static RoZcSystemText ParseZcBroadcast2(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcBroadcast2);
            uint colorRgb = reader.ReadUInt32();
            reader.Skip(8);
            string message = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcSystemText(message, colorRgb, hasColor: true, packetId: RoPacketIds.ZcBroadcast2);
        }

        public static RoZcSystemText ParseZcDebugMsg(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcDebugMsg);
            uint colorRgb = BgrToRgb(reader.ReadUInt32());
            string message = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcSystemText(message, colorRgb, hasColor: true, packetId: RoPacketIds.ZcDebugMsg);
        }

        public static RoZcWhisper ParseZcWhisper(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcWhisper);
            string sender = reader.ReadFixedString(ActorNameLength, encoding);
            byte isAdmin = 0;
            if (reader.Remaining >= 4) {
                int marker = reader.ReadInt32();
                if (marker == 0 || marker == 1) {
                    isAdmin = (byte) marker;
                } else {
                    reader = CreateVariableFrameReader(frame, RoPacketIds.ZcWhisper);
                    sender = reader.ReadFixedString(ActorNameLength, encoding);
                }
            }

            string message = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcWhisper(sender, message, 0, isAdmin);
        }

        public static RoZcWhisper ParseZcWhisper2(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcWhisper2);
            uint senderActorId = reader.ReadUInt32();
            string sender = reader.ReadFixedString(ActorNameLength, encoding);
            byte isAdmin = reader.Remaining > 0 ? reader.ReadByte() : (byte) 0;
            string message = reader.Remaining > 0 ? reader.ReadFixedString(reader.Remaining, encoding) : string.Empty;
            return new RoZcWhisper(sender, message, senderActorId, isAdmin);
        }

        public static RoZcWhisperAck ParseZcAckWhisper(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckWhisper, 3);
            return new RoZcWhisperAck(reader.ReadByte());
        }

        public static RoZcWhisperAck ParseZcAckWhisper2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckWhisper2, 7);
            byte result = reader.ReadByte();
            return new RoZcWhisperAck(result, reader.ReadUInt32());
        }

        public static RoZcMessage ParseZcMsg(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMsg, ZcMsgLength);
            return new RoZcMessage(reader.ReadUInt16());
        }

        public static RoZcMessage ParseZcMsgValue(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMsgValue, ZcMsgValueLength);
            ushort messageId = reader.ReadUInt16();
            int value = reader.ReadInt32();
            return new RoZcMessage(messageId, numberValue: value, hasNumberValue: true);
        }

        public static RoZcMessage ParseZcMsgSkill(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMsgSkill, ZcMsgSkillLength);
            ushort skillId = reader.ReadUInt16();
            int messageId = unchecked((int) reader.ReadUInt32());
            return new RoZcMessage(messageId, skillId: skillId, hasSkillId: true);
        }

        public static RoZcMessage ParseZcFormatStringMsg(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcFormatStringMsg);
            ushort messageId = reader.ReadUInt16();
            string value = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcMessage(messageId, stringValue: value);
        }

        public static RoZcMessage ParseZcMsgColor(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMsgColor, ZcMsgColorLength);
            ushort messageId = reader.ReadUInt16();
            uint colorRgb = BgrToRgb(reader.ReadUInt32());
            return new RoZcMessage(messageId, colorRgb: colorRgb, hasColor: true);
        }

        public static RoZcMessage ParseZcFormatStringMsgColor(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcFormatStringMsgColor);
            ushort messageId = reader.ReadUInt16();
            uint colorRgb = reader.ReadUInt32();
            string value = reader.ReadFixedString(reader.Remaining, encoding);
            return new RoZcMessage(messageId, stringValue: value, colorRgb: colorRgb, hasColor: true);
        }

        private static string StripBroadcastMarker(string message) {
            if (string.IsNullOrEmpty(message) || message.Length < 4) {
                return message ?? string.Empty;
            }

            if (message.StartsWith("blue", StringComparison.Ordinal) ||
                message.StartsWith("ssss", StringComparison.Ordinal) ||
                message.StartsWith("micc", StringComparison.Ordinal)) {
                return message.Substring(4).TrimStart('\0');
            }

            return message;
        }

        private static uint BgrToRgb(uint colorBgr) {
            return ((colorBgr & 0x0000ffu) << 16) |
                (colorBgr & 0x00ff00u) |
                ((colorBgr & 0xff0000u) >> 16);
        }

        public static RoZcNotifyActorEntry ParseZcNotifyNewEntry11(byte[] frame, Encoding encoding = null) {
            return ParseActorEntry(frame, RoPacketIds.ZcNotifyNewEntry11, RoMapActorEntryKind.New, encoding);
        }

        public static RoZcNotifyActorEntry ParseZcNotifyStandEntry11(byte[] frame, Encoding encoding = null) {
            return ParseActorEntry(frame, RoPacketIds.ZcNotifyStandEntry11, RoMapActorEntryKind.Standing, encoding);
        }

        public static RoZcNotifyActorEntry ParseZcNotifyMoveEntry11(byte[] frame, Encoding encoding = null) {
            return ParseActorEntry(frame, RoPacketIds.ZcNotifyMoveEntry11, RoMapActorEntryKind.Moving, encoding);
        }

        public static RoZcAcceptEnter2 ParseZcAcceptEnter2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAcceptEnter2, ZcAcceptEnter2Length);
            int startTime = reader.ReadInt32();
            RoPacketPosition position = reader.ReadPosition();
            byte xSize = reader.ReadByte();
            byte ySize = reader.ReadByte();
            ushort font = reader.ReadUInt16();
            return new RoZcAcceptEnter2(startTime, position, xSize, ySize, font);
        }

        public static RoZcAcceptEnter2 ParseZcAcceptEnter(byte[] frame) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length < 2) {
                throw new ArgumentException("Packet frame must include the 2-byte packet id.", nameof(frame));
            }

            ushort packetId = (ushort) (frame[0] | (frame[1] << 8));
            int expectedLength;
            switch (packetId) {
                case RoPacketIds.ZcAcceptEnter:
                    expectedLength = ZcAcceptEnterLength;
                    break;
                case RoPacketIds.ZcAcceptEnter2:
                    expectedLength = ZcAcceptEnter2Length;
                    break;
                case RoPacketIds.ZcAcceptEnter3:
                    expectedLength = ZcAcceptEnter3Length;
                    break;
                default:
                    throw new ArgumentException($"Packet 0x{packetId:x4} is not a supported map-enter accept packet.", nameof(frame));
            }

            var reader = CreateFixedFrameReader(frame, packetId, expectedLength);
            int startTime = reader.ReadInt32();
            RoPacketPosition position = reader.ReadPosition();
            byte xSize = reader.ReadByte();
            byte ySize = reader.ReadByte();
            ushort font = 0;
            if (packetId == RoPacketIds.ZcAcceptEnter2 || packetId == RoPacketIds.ZcAcceptEnter3) {
                font = reader.ReadUInt16();
            }

            if (packetId == RoPacketIds.ZcAcceptEnter3) {
                reader.ReadByte();
            }

            return new RoZcAcceptEnter2(startTime, position, xSize, ySize, font);
        }

        public static RoZcInventoryStart ParseZcInventoryStart(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcInventoryStart);
            byte inventoryType = reader.Remaining > 0 ? reader.ReadByte() : (byte) 0;
            string name = reader.Remaining > 0 ? reader.ReadFixedString(reader.Remaining, encoding) : string.Empty;
            return new RoZcInventoryStart(inventoryType, name);
        }

        public static RoZcInventoryEnd ParseZcInventoryEnd(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcInventoryEnd, ZcInventoryEndLength);
            return new RoZcInventoryEnd(reader.ReadByte(), reader.ReadByte());
        }

        public static RoZcInventoryItemList ParseZcInventoryListNormal(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcInventoryListNormal);
            byte inventoryType = reader.ReadByte();
            int recordLength = ResolveInventoryRecordLength(
                reader.Remaining,
                NormalItemRecordLength20200401,
                NormalItemRecordLength4,
                RoPacketIds.ZcInventoryListNormal);
            RequireRecordAlignment(reader, recordLength, RoPacketIds.ZcInventoryListNormal);
            var items = new List<RoZcInventoryItem>();
            while (!reader.EndOfPacket) {
                items.Add(recordLength == NormalItemRecordLength20200401
                    ? ParseNormalInventoryItem(reader)
                    : ParseNormalInventoryItem4(reader));
            }

            return new RoZcInventoryItemList(inventoryType, isEquipmentList: false, items.ToArray());
        }

        public static RoZcInventoryItemList ParseZcInventoryListNormal4(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcInventoryListNormal4);
            byte inventoryType = (byte) RoInventoryType.Inventory;
            if ((reader.Remaining - 1) >= 0 && (reader.Remaining - 1) % NormalItemRecordLength4 == 0) {
                inventoryType = reader.ReadByte();
            }

            RequireRecordAlignment(reader, NormalItemRecordLength4, RoPacketIds.ZcInventoryListNormal4);
            var items = new List<RoZcInventoryItem>();
            while (!reader.EndOfPacket) {
                items.Add(ParseNormalInventoryItem4(reader));
            }

            return new RoZcInventoryItemList(inventoryType, isEquipmentList: false, items.ToArray());
        }

        public static RoZcInventoryItemList ParseZcInventoryListEquip(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcInventoryListEquip);
            byte inventoryType = reader.ReadByte();
            int recordLength = ResolveInventoryRecordLength(
                reader.Remaining,
                EquipmentItemRecordLength20200401,
                EquipmentItemRecordLength5,
                RoPacketIds.ZcInventoryListEquip);
            RequireRecordAlignment(reader, recordLength, RoPacketIds.ZcInventoryListEquip);
            var items = new List<RoZcInventoryItem>();
            while (!reader.EndOfPacket) {
                items.Add(recordLength == EquipmentItemRecordLength20200401
                    ? ParseEquipmentInventoryItem(reader)
                    : ParseEquipmentInventoryItem4(reader, hasRandomOptions: true));
            }

            return new RoZcInventoryItemList(inventoryType, isEquipmentList: true, items.ToArray());
        }

        public static RoZcInventoryItemList ParseZcInventoryListEquip4(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcInventoryListEquip4);
            byte inventoryType = (byte) RoInventoryType.Inventory;
            if ((reader.Remaining - 1) >= 0 && (reader.Remaining - 1) % EquipmentItemRecordLength4 == 0) {
                inventoryType = reader.ReadByte();
            }

            RequireRecordAlignment(reader, EquipmentItemRecordLength4, RoPacketIds.ZcInventoryListEquip4);
            var items = new List<RoZcInventoryItem>();
            while (!reader.EndOfPacket) {
                items.Add(ParseEquipmentInventoryItem4(reader, hasRandomOptions: false));
            }

            return new RoZcInventoryItemList(inventoryType, isEquipmentList: true, items.ToArray());
        }

        public static RoZcInventoryItemList ParseZcInventoryListEquip5(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcInventoryListEquip5);
            byte inventoryType = (byte) RoInventoryType.Inventory;
            if ((reader.Remaining - 1) >= 0 && (reader.Remaining - 1) % EquipmentItemRecordLength5 == 0) {
                inventoryType = reader.ReadByte();
            }

            RequireRecordAlignment(reader, EquipmentItemRecordLength5, RoPacketIds.ZcInventoryListEquip5);
            var items = new List<RoZcInventoryItem>();
            while (!reader.EndOfPacket) {
                items.Add(ParseEquipmentInventoryItem4(reader, hasRandomOptions: true));
            }

            return new RoZcInventoryItemList(inventoryType, isEquipmentList: true, items.ToArray());
        }

        public static RoZcInventoryItemList ParseZcInventoryListEquip2(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcInventoryListEquip2);
            byte inventoryType = reader.ReadByte();
            RequireRecordAlignment(reader, EquipmentItemRecordLength20240619, RoPacketIds.ZcInventoryListEquip2);
            var items = new List<RoZcInventoryItem>();
            while (!reader.EndOfPacket) {
                items.Add(ParseEquipmentInventoryItemWithGrade(reader));
            }

            return new RoZcInventoryItemList(inventoryType, isEquipmentList: true, items.ToArray());
        }

        /// <summary>
        /// ZC_EQUIPWIN_MICROSCOPE: 0x0b37 (68-byte records with the grade) or 0x0b03 (20200401, 67-byte records); the head
        /// views are bottom, mid, top (Hercules clif_viewequip_ack: accessory2 = mid, accessory3 = top).
        /// </summary>
        public static RoZcEquipWinMicroscope ParseZcEquipWinMicroscope(byte[] frame, Encoding encoding = null) {
            bool v6 = frame != null && frame.Length >= 2 && (frame[0] | (frame[1] << 8)) == RoPacketIds.ZcEquipWinMicroscopeV6;
            var reader = CreateVariableFrameReader(frame, v6 ? RoPacketIds.ZcEquipWinMicroscopeV6 : RoPacketIds.ZcEquipWinMicroscope);
            string characterName = reader.ReadFixedString(ActorNameLength, encoding);
            short job = reader.ReadInt16();
            short head = reader.ReadInt16();
            short headBottom = reader.ReadInt16();
            short headMid = reader.ReadInt16();
            short headTop = reader.ReadInt16();
            short robe = reader.ReadInt16();
            short headPalette = reader.ReadInt16();
            short bodyPalette = reader.ReadInt16();
            short bodyStyle = reader.ReadInt16();
            byte sex = reader.ReadByte();
            RequireRecordAlignment(reader, v6 ? EquipmentItemRecordLength20200401 : EquipmentItemRecordLength20240619, v6 ? RoPacketIds.ZcEquipWinMicroscopeV6 : RoPacketIds.ZcEquipWinMicroscope);
            var items = new List<RoZcInventoryItem>();
            while (!reader.EndOfPacket) {
                items.Add(v6 ? ParseEquipmentInventoryItem(reader) : ParseEquipmentInventoryItemWithGrade(reader));
            }

            return new RoZcEquipWinMicroscope(
                characterName,
                job,
                head,
                headBottom,
                headTop,
                headMid,
                robe,
                headPalette,
                bodyPalette,
                bodyStyle,
                sex,
                items.ToArray());
        }

        public static RoZcBattlefieldChat ParseZcBattlefieldChat(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcBattlefieldChat);
            uint aid = reader.ReadUInt32();
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            return new RoZcBattlefieldChat(aid, name, reader.ReadFixedString(reader.Remaining, encoding));
        }

        public static RoZcNavigationActive ParseZcNavigationActive(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNavigationActive, 27);
            byte type = reader.ReadByte();
            byte flag = reader.ReadByte();
            byte hide = reader.ReadByte();
            string map = reader.ReadFixedString(16, encoding);
            return new RoZcNavigationActive(type, flag, hide, map, reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
        }

        public static RoZcChangeItemOption ParseZcChangeItemOption(byte[] frame) {
            var reader = CreateFrameReader(frame);
            ushort packetId = reader.ReadUInt16();
            if (packetId != RoPacketIds.ZcChangeItemOption) {
                throw new ArgumentException($"Expected packet 0x{RoPacketIds.ZcChangeItemOption:x4}, got 0x{packetId:x4}.", nameof(frame));
            }

            if (frame.Length < 40) {
                throw new ArgumentException("ZC_CHANGE_ITEM_OPTION is too short.", nameof(frame));
            }

            short index = reader.ReadInt16();
            bool damaged = reader.ReadByte() != 0;
            bool wideCards = frame.Length >= 48;
            var cards = new int[CardSlotCount];
            for (int i = 0; i < cards.Length; i++) {
                cards[i] = wideCards
                    ? unchecked((int) reader.ReadUInt32())
                    : reader.ReadUInt16();
            }

            RoPacketItemRandomOption[] options = ReadRandomOptions(reader);
            while (reader.Remaining > 2) {
                reader.ReadByte();
            }

            byte refine = reader.Remaining > 0 ? reader.ReadByte() : (byte) 0;
            byte grade = reader.Remaining > 0 ? reader.ReadByte() : (byte) 0;
            return new RoZcChangeItemOption(index, damaged, cards, options, refine, grade);
        }

        public static RoZcEquipSwitchList ParseZcEquipSwitchList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcEquipSwitchList);
            RequireRecordAlignment(reader, EquipSwitchListRecordLength, RoPacketIds.ZcEquipSwitchList);
            var entries = new List<RoZcEquipSwitchListEntry>();
            while (!reader.EndOfPacket) {
                entries.Add(new RoZcEquipSwitchListEntry(reader.ReadInt16(), reader.ReadUInt32()));
            }

            return new RoZcEquipSwitchList(entries.ToArray());
        }

        public static RoZcWearEquipmentAck ParseZcAckWearEquipV5(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckWearEquipV5, ZcAckWearEquipV5Length);
            return new RoZcWearEquipmentAck(
                reader.ReadInt16(),
                reader.ReadUInt32(),
                reader.ReadUInt16(),
                reader.ReadByte());
        }

        public static RoZcTakeoffEquipmentAck ParseZcAckTakeoffEquipV5(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckTakeoffEquipV5, ZcAckTakeoffEquipV5Length);
            return new RoZcTakeoffEquipmentAck(
                reader.ReadInt16(),
                reader.ReadUInt32(),
                reader.ReadByte());
        }

        public static RoZcTakeoffEquipmentAck ParseZcReqTakeoffEquipAck(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcReqTakeoffEquipAck, ZcReqTakeoffEquipAckLength);
            short bagIndex = reader.ReadInt16();
            ushort wearLocation = reader.ReadUInt16();
            byte rawResult = reader.ReadByte();
            byte normalizedResult = rawResult == 0 ? (byte) 1 : (byte) 0;
            return new RoZcTakeoffEquipmentAck(bagIndex, wearLocation, normalizedResult);
        }

        public static RoZcTakeoffEquipmentAck ParseZcReqTakeoffEquipAck2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcReqTakeoffEquipAck2, ZcReqTakeoffEquipAck2Length);
            return new RoZcTakeoffEquipmentAck(
                reader.ReadInt16(),
                reader.ReadUInt16(),
                reader.ReadByte());
        }

        public static RoZcEquipArrow ParseZcEquipArrow(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcEquipArrow, ZcEquipArrowLength);
            return new RoZcEquipArrow(reader.ReadUInt16());
        }

        public static RoZcItemThrowAck ParseZcItemThrowAck(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcItemThrowAck, ZcItemThrowAckLength);
            return new RoZcItemThrowAck(reader.ReadInt16(), reader.ReadUInt16());
        }

        public static RoZcUseItemAck ParseZcUseItemAck(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcUseItemAck, ZcUseItemAckLength);
            return new RoZcUseItemAck(reader.ReadUInt16(), 0u, 0u, reader.ReadInt16(), reader.ReadByte());
        }

        public static RoZcUseItemAck ParseZcUseItemAck2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcUseItemAck2, ZcUseItemAck2Length20200401);
            ushort bagIndex = reader.ReadUInt16();
            uint itemId = reader.ReadUInt32();
            uint actorId = reader.ReadUInt32();
            short count = reader.ReadInt16();
            byte result = reader.ReadByte();
            return new RoZcUseItemAck(bagIndex, itemId, actorId, count, result);
        }

        public static RoZcItemCompositionList ParseZcItemCompositionList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcItemCompositionList);
            RequireRecordAlignment(reader, 2, RoPacketIds.ZcItemCompositionList);
            int count = reader.Remaining / 2;
            var indexes = new int[count];
            for (int i = 0; i < count; i++) {
                indexes[i] = reader.ReadUInt16();
            }

            return new RoZcItemCompositionList(indexes);
        }

        public static RoZcItemIdentifyList ParseZcItemIdentifyList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcItemIdentifyList);
            RequireRecordAlignment(reader, 2, RoPacketIds.ZcItemIdentifyList);
            int count = reader.Remaining / 2;
            var indexes = new int[count];
            for (int i = 0; i < count; i++) {
                indexes[i] = reader.ReadUInt16();
            }

            return new RoZcItemIdentifyList(indexes);
        }

        public static RoZcMakingArrowList ParseZcMakingArrowList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcMakingArrowList);
            RequireRecordAlignment(reader, 4, RoPacketIds.ZcMakingArrowList);
            int count = reader.Remaining / 4;
            var itemIds = new uint[count];
            for (int i = 0; i < count; i++) {
                itemIds[i] = reader.ReadUInt32();
            }

            return new RoZcMakingArrowList(itemIds);
        }

        public static RoZcSelectCart ParseZcSelectCart(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcSelectCart);
            if (reader.Remaining < 4) {
                throw new ArgumentException(
                    $"Packet 0x{RoPacketIds.ZcSelectCart:x4} must contain a 4-byte identity.",
                    nameof(frame));
            }

            uint identity = reader.ReadUInt32();
            int count = reader.Remaining;
            var cartTypes = new int[count];
            for (int i = 0; i < count; i++) {
                cartTypes[i] = reader.ReadByte();
            }

            return new RoZcSelectCart(identity, cartTypes);
        }

        public static RoZcWarpList ParseZcWarpList(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcWarpList, ZcWarpListLength);
            ushort skillId = reader.ReadUInt16();
            var mapNames = new string[LegacyWarpPointCount];
            for (int i = 0; i < mapNames.Length; i++) {
                mapNames[i] = reader.ReadFixedString(WarpPointMapNameLength, encoding ?? Encoding.ASCII);
            }

            return new RoZcWarpList(skillId, mapNames);
        }

        public static RoZcWarpList ParseZcWarpList2(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcWarpList2);
            ushort skillId = reader.ReadUInt16();
            RequireRecordAlignment(reader, WarpPointMapNameLength, RoPacketIds.ZcWarpList2);
            int count = reader.Remaining / WarpPointMapNameLength;
            var mapNames = new string[count];
            for (int i = 0; i < count; i++) {
                mapNames[i] = reader.ReadFixedString(WarpPointMapNameLength, encoding ?? Encoding.ASCII);
            }

            return new RoZcWarpList(skillId, mapNames);
        }

        public static RoZcNotifyMapInfo ParseZcNotifyMapInfo(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyMapInfo, ZcNotifyMapInfoLength);
            return new RoZcNotifyMapInfo(reader.ReadInt16());
        }

        public static RoZcItemCompositionAck ParseZcAckItemComposition(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckItemComposition, ZcAckItemCompositionLength);
            return new RoZcItemCompositionAck(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadByte());
        }

        public static RoZcItemIdentifyAck ParseZcAckItemIdentify(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckItemIdentify, ZcAckItemIdentifyLength);
            return new RoZcItemIdentifyAck(reader.ReadInt16(), reader.ReadByte());
        }

        public static RoZcItemPickupAck ParseZcItemPickupAck7(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcItemPickupAck7, ZcItemPickupAck7Length);
            short bagIndex = reader.ReadInt16();
            ushort count = reader.ReadUInt16();
            uint itemId = reader.ReadUInt32();
            bool isIdentified = reader.ReadByte() != 0;
            bool isDamaged = reader.ReadByte() != 0;
            byte refineLevel = reader.ReadByte();
            int[] cards = ReadCardIds(reader);
            uint equipLocationRaw = reader.ReadUInt32();
            byte itemTypeRaw = reader.ReadByte();
            byte result = reader.ReadByte();
            int hireExpireDate = reader.ReadInt32();
            ushort bindOnEquipType = reader.ReadUInt16();
            reader.Skip(25);
            byte favorite = reader.ReadByte();
            ushort viewIdHint = reader.ReadUInt16();

            return new RoZcItemPickupAck(
                bagIndex,
                count,
                itemId,
                isIdentified,
                isDamaged,
                refineLevel,
                cards,
                equipLocationRaw,
                itemTypeRaw,
                result,
                hireExpireDate,
                bindOnEquipType,
                favorite,
                viewIdHint);
        }

        public static RoZcItemPickupAck ParseZcItemPickupAck8(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcItemPickupAck8, ZcItemPickupAck8Length);
            short bagIndex = reader.ReadInt16();
            short rawCount = reader.ReadInt16();
            ushort count = rawCount > 0 ? unchecked((ushort) rawCount) : (ushort) 0;
            uint itemId = reader.ReadUInt32();
            bool isIdentified = reader.ReadByte() != 0;
            bool isDamaged = reader.ReadByte() != 0;
            int[] cards = ReadCardIds(reader);
            uint equipLocationRaw = reader.ReadUInt32();
            byte itemTypeRaw = reader.ReadByte();
            byte result = reader.ReadByte();
            int hireExpireDate = reader.ReadInt32();
            ushort bindOnEquipType = reader.ReadUInt16();
            reader.Skip(25);
            byte favorite = reader.ReadByte();
            ushort viewIdHint = reader.ReadUInt16();
            byte refineLevel = reader.ReadByte();
            reader.Skip(1);

            return new RoZcItemPickupAck(
                bagIndex,
                count,
                itemId,
                isIdentified,
                isDamaged,
                refineLevel,
                cards,
                equipLocationRaw,
                itemTypeRaw,
                result,
                hireExpireDate,
                bindOnEquipType,
                favorite,
                viewIdHint);
        }

        public static RoZcParameterChange ParseZcParChange(byte[] frame) {
            return ParseZcParameterChange(frame, RoPacketIds.ZcParChange, ZcParChangeLength, readsLongLongValue: false);
        }

        public static RoZcParameterChange ParseZcLongParChange(byte[] frame) {
            return ParseZcParameterChange(frame, RoPacketIds.ZcLongParChange, ZcParChangeLength, readsLongLongValue: false);
        }

        public static RoZcParameterChange ParseZcLongLongParChange(byte[] frame) {
            return ParseZcParameterChange(frame, RoPacketIds.ZcLongLongParChange, ZcLongLongParChangeLength, readsLongLongValue: true);
        }

        public static RoZcRecovery2 ParseZcRecovery2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcRecovery2, ZcRecovery2Length);
            return new RoZcRecovery2(reader.ReadUInt16(), reader.ReadUInt32());
        }

        public static RoZcStatusChangeAck ParseZcStatusChangeAck(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcStatusChangeAck, ZcStatusChangeAckLength);
            return new RoZcStatusChangeAck(reader.ReadUInt16(), reader.ReadByte(), reader.ReadByte());
        }

        public static RoZcCoupleStatus ParseZcCoupleStatus(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcCoupleStatus, ZcCoupleStatusLength);
            return new RoZcCoupleStatus(unchecked((ushort) reader.ReadUInt32()), reader.ReadInt32(), reader.ReadInt32());
        }

        public static RoZcStatusSnapshot ParseZcStatus(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcStatus, ZcStatusLength);
            return new RoZcStatusSnapshot(
                reader.ReadInt16(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16());
        }

        public static RoZcStatusPropertyChange ParseZcStatusChange(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcStatusChange, ZcStatusChangeLength);
            return new RoZcStatusPropertyChange(reader.ReadUInt16(), reader.ReadByte());
        }

        public static RoZcNotifyWeightPercentage ParseZcNotifyWeightPercentage(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyWeightPercentage, ZcNotifyWeightPercentageLength);
            return new RoZcNotifyWeightPercentage(reader.ReadInt32());
        }

        public static RoZcNotifyEffectById ParseZcNotifyEffect2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyEffect2, ZcNotifyEffect2Length);
            uint actorId = reader.ReadUInt32();
            return new RoZcNotifyEffectById(actorId, reader.ReadInt32(), 0, hasValue: false);
        }

        public static RoZcNotifyEffectById ParseZcNotifyEffect3(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyEffect3, ZcNotifyEffect3Length);
            uint actorId = reader.ReadUInt32();
            int effectId = reader.ReadInt32();
            return new RoZcNotifyEffectById(actorId, effectId, reader.ReadInt32(), hasValue: true);
        }

        public static RoZcOptionChange2 ParseZcNpcShowEfstUpdate(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNpcShowEfstUpdate, ZcNpcShowEfstUpdateLength);
            uint actorId = reader.ReadUInt32();
            int option = reader.ReadInt32();
            int level = reader.ReadInt32();
            return new RoZcOptionChange2(actorId, option, level, reader.ReadInt32());
        }

        /// <summary>ZC_SPIRITS / ZC_SPIRITS2 {u16 cmd, u32 aid, u16 num}: both carry the same layout.</summary>
        public static RoZcSpirits ParseZcSpirits(byte[] frame) {
            ushort packetId = frame != null && frame.Length >= 2 ? (ushort) (frame[0] | (frame[1] << 8)) : RoPacketIds.ZcSpirits;
            var reader = CreateFixedFrameReader(frame, packetId == RoPacketIds.ZcSpirits2 ? RoPacketIds.ZcSpirits2 : RoPacketIds.ZcSpirits, ZcSpiritsLength);
            uint actorId = reader.ReadUInt32();
            return new RoZcSpirits(actorId, reader.ReadInt16());   // short num (negative = none)
        }

        public static RoZcDispel ParseZcDispel(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcDispel, ZcDispelLength);
            return new RoZcDispel(reader.ReadUInt32());
        }

        public static RoZcAttackRange ParseZcAttackRange(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAttackRange, ZcAttackRangeLength);
            return new RoZcAttackRange(reader.ReadInt16());
        }

        public static RoZcQuestAdd ParseZcQuestAdd(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcQuestAdd, ZcQuestAddLength);
            return new RoZcQuestAdd(ReadQuestEntryAdd(reader, encoding));
        }

        public static RoZcQuestList ParseZcQuestList(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcQuestList);
            int count = reader.ReadInt32();
            var entries = new List<RoZcQuestEntry>(Math.Max(0, count));
            for (int i = 0; i < count && !reader.EndOfPacket; i++) {
                uint questId = reader.ReadUInt32();
                byte active = reader.ReadByte();
                int serverTime = reader.ReadInt32();
                int endTime = reader.ReadInt32();
                short objectiveCount = reader.ReadInt16();
                var objectives = new List<RoZcQuestObjective>(Math.Max(0, (int) objectiveCount));
                for (int objectiveIndex = 0; objectiveIndex < objectiveCount; objectiveIndex++) {
                    RequireRemaining(reader, QuestObjectiveListRecordLength, RoPacketIds.ZcQuestList);
                    objectives.Add(ReadQuestObjectiveList(reader, encoding));
                }

                entries.Add(new RoZcQuestEntry(questId, active, serverTime, endTime, objectives.ToArray()));
            }

            return new RoZcQuestList(entries.ToArray());
        }

        public static RoZcQuestDelete ParseZcQuestDelete(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcQuestDelete, ZcQuestDeleteLength);
            return new RoZcQuestDelete(reader.ReadUInt32());
        }

        public static RoZcQuestActive ParseZcQuestActive(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcQuestActive, ZcQuestActiveLength);
            return new RoZcQuestActive(reader.ReadUInt32(), reader.ReadByte());
        }

        public static RoZcQuestObjectiveUpdate ParseZcQuestObjectiveUpdateLegacy(byte[] frame) {
            return ParseQuestObjectiveUpdate(frame, RoPacketIds.ZcQuestObjectiveUpdateLegacy, hasCount: true, modern: false, hasHuntIdent2: false);
        }

        public static RoZcQuestObjectiveUpdate ParseZcQuestHuntInfo(byte[] frame) {
            return ParseQuestObjectiveUpdate(frame, RoPacketIds.ZcQuestHuntInfo, hasCount: false, modern: false, hasHuntIdent2: false);
        }

        public static RoZcQuestObjectiveUpdate ParseZcQuestObjectiveUpdate(byte[] frame) {
            return ParseQuestObjectiveUpdate(frame, RoPacketIds.ZcQuestObjectiveUpdate, hasCount: true, modern: true, hasHuntIdent2: false);
        }

        public static RoZcQuestObjectiveUpdate ParseZcQuestObjectiveUpdateModern(byte[] frame) {
            return ParseQuestObjectiveUpdate(frame, RoPacketIds.ZcQuestObjectiveUpdateModern, hasCount: true, modern: true, hasHuntIdent2: true);
        }

        public static RoZcPartyOption ParseZcPartyOptionLegacy(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyOptionLegacy, ZcPartyOptionLegacyLength);
            return new RoZcPartyOption(reader.ReadUInt32(), 0, 0, hasItemRules: false);
        }

        public static RoZcPartyCreateAck ParseZcPartyCreateAck(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyCreateAck, ZcPartyCreateAckLength);
            return new RoZcPartyCreateAck(reader.ReadByte());
        }

        public static RoZcPartyConfig ParseZcPartyConfig(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyConfig, ZcPartyConfigLength);
            return new RoZcPartyConfig(reader.ReadByte() != 0);
        }

        public static RoZcPartyLeaderChanged ParseZcPartyLeaderChanged(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyLeaderChanged, ZcPartyLeaderChangedLength);
            return new RoZcPartyLeaderChanged(reader.ReadUInt32(), reader.ReadUInt32());
        }

        public static RoZcPartyOption ParseZcPartyOption(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyOption, ZcPartyOptionLength);
            return new RoZcPartyOption(reader.ReadUInt32(), reader.ReadByte(), reader.ReadByte());
        }

        public static RoZcPartyInfo ParseZcPartyInfoLegacy(byte[] frame, Encoding encoding = null) {
            return ParsePartyInfo(frame, RoPacketIds.ZcPartyInfoLegacy, PartyInfoLegacyRecordLength, hasCharacterId: false, hasClassAndLevel: false, encoding);
        }

        public static RoZcPartyInfo ParseZcPartyInfo2(byte[] frame, Encoding encoding = null) {
            return ParsePartyInfo(frame, RoPacketIds.ZcPartyInfo2, PartyInfo2RecordLength, hasCharacterId: false, hasClassAndLevel: true, encoding);
        }

        public static RoZcPartyInfo ParseZcPartyInfo(byte[] frame, Encoding encoding = null) {
            return ParsePartyInfo(frame, RoPacketIds.ZcPartyInfo, PartyInfoRecordLength, hasCharacterId: true, hasClassAndLevel: true, encoding);
        }

        public static RoZcPartyMemberInfo ParseZcPartyMemberInfoLegacy(byte[] frame, Encoding encoding = null) {
            return ParsePartyMemberInfo(frame, RoPacketIds.ZcPartyMemberInfoLegacy, ZcPartyMemberInfoLegacyLength, hasCharacterId: false, hasClassAndLevel: false, hasPosition: true, hasRules: false, encoding);
        }

        public static RoZcPartyMemberInfo ParseZcPartyMemberInfo2(byte[] frame, Encoding encoding = null) {
            return ParsePartyMemberInfo(frame, RoPacketIds.ZcPartyMemberInfo2, ZcPartyMemberInfo2Length, hasCharacterId: false, hasClassAndLevel: false, hasPosition: true, hasRules: true, encoding);
        }

        public static RoZcPartyMemberInfo ParseZcPartyMemberInfo3(byte[] frame, Encoding encoding = null) {
            return ParsePartyMemberInfo(frame, RoPacketIds.ZcPartyMemberInfo3, ZcPartyMemberInfo3Length, hasCharacterId: true, hasClassAndLevel: false, hasPosition: true, hasRules: true, encoding);
        }

        public static RoZcPartyMemberInfo ParseZcPartyMemberInfo(byte[] frame, Encoding encoding = null) {
            return ParsePartyMemberInfo(frame, RoPacketIds.ZcPartyMemberInfo, ZcPartyMemberInfoLength, hasCharacterId: true, hasClassAndLevel: true, hasPosition: true, hasRules: true, encoding);
        }

        public static RoZcPartyMemberLeave ParseZcPartyMemberLeave(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyMemberLeave, ZcPartyMemberLeaveLength);
            uint accountId = reader.ReadUInt32();
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            byte result = reader.ReadByte();
            return new RoZcPartyMemberLeave(accountId, name, result);
        }

        public static RoZcPartyPosition ParseZcPartyPosition(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyPosition, ZcPartyPositionLength);
            return new RoZcPartyPosition(reader.ReadUInt32(), reader.ReadInt16(), reader.ReadInt16());
        }

        public static RoZcGuildPosition ParseZcGuildPosition(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcGuildPosition, 10);
            return new RoZcGuildPosition(reader.ReadUInt32(), reader.ReadInt16(), reader.ReadInt16());
        }

        public static RoZcQuestNotifyEffect ParseZcQuestNotifyEffect(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcQuestNotifyEffect, 14);
            return new RoZcQuestNotifyEffect(reader.ReadUInt32(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
        }

        public static RoZcBossInfo ParseZcBossInfo(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcBossInfo, 70);
            byte type = reader.ReadByte();
            int x = reader.ReadInt32(), y = reader.ReadInt32();
            reader.Skip(8);
            return new RoZcBossInfo(type, x, y, reader.ReadFixedString(51, null));
        }

        public static RoZcMinimapPing ParseZcMinimapPing(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMinimapPing, 10);
            return new RoZcMinimapPing(reader.ReadInt32(), reader.ReadInt16(), reader.ReadInt16());
        }

        public static RoZcPartyHp ParseZcPartyHpLegacy(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyHpLegacy, ZcPartyHpLegacyLength);
            return new RoZcPartyHp(reader.ReadUInt32(), reader.ReadUInt16(), reader.ReadUInt16());
        }

        public static RoZcPartyHp ParseZcPartyHp(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyHp, ZcPartyHpLength);
            return new RoZcPartyHp(reader.ReadUInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static RoZcPartyAlive ParseZcPartyAlive(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyAlive, ZcPartyAliveLength);
            return new RoZcPartyAlive(reader.ReadUInt32(), reader.ReadByte() != 0);
        }

        public static RoZcPartyJobAndLevel ParseZcPartyJobAndLevel(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyJobAndLevel, ZcPartyJobAndLevelLength);
            return new RoZcPartyJobAndLevel(reader.ReadUInt32(), reader.ReadInt16(), reader.ReadInt16());
        }

        public static RoZcPartyInvite ParseZcPartyInvite(byte[] frame, Encoding encoding = null) {
            return ParsePartyInvite(frame, RoPacketIds.ZcPartyInvite, encoding);
        }

        public static RoZcPartyInvite ParseZcPartyInvite2(byte[] frame, Encoding encoding = null) {
            return ParsePartyInvite(frame, RoPacketIds.ZcPartyInvite2, encoding);
        }

        public static RoZcPartyInviteAck ParseZcPartyInviteAck(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyInviteAck, ZcPartyInviteAckLength);
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            return new RoZcPartyInviteAck(name, reader.ReadByte());
        }

        public static RoZcPartyInviteAck ParseZcPartyInviteAck2(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPartyInviteAck2, ZcPartyInviteAck2Length);
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            return new RoZcPartyInviteAck(name, reader.ReadInt32());
        }

        /// <summary>
        /// ZC_FRIENDS_LIST: AID, GID and the 24-byte name per friend, except packetver 2018-03..2020-09 (20200401), whose
        /// records are AID + GID only; the names then come from ZC_FRIENDS_STATE (online) and CZ_REQNAME_BYGID.
        /// </summary>
        public static RoZcFriendsList ParseZcFriendsList(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcFriendsList);
            bool named = HasNamedFriendRecords(frame, reader.Remaining);
            RequireRecordAlignment(reader, named ? FriendListRecordLength : FriendListIdRecordLength, RoPacketIds.ZcFriendsList);
            var friends = new List<RoZcFriendEntry>();
            while (!reader.EndOfPacket) {
                uint accountId = reader.ReadUInt32();
                uint characterId = reader.ReadUInt32();
                string name = named ? reader.ReadFixedString(ActorNameLength, encoding) : string.Empty;
                friends.Add(new RoZcFriendEntry(accountId, characterId, name, 1));
            }

            return new RoZcFriendsList(friends.ToArray());
        }

        // Named (32-byte) records only when every record's name field is a non-empty, NUL-padded string: in the id-only
        // layout that space holds the next friends' ids, whose zero high bytes are followed by non-zero ones.
        private static bool HasNamedFriendRecords(byte[] frame, int payload) {
            if (payload == 0 || payload % FriendListRecordLength != 0) {
                return false;
            }

            for (int record = frame.Length - payload; record < frame.Length; record += FriendListRecordLength) {
                int name = record + 8;
                if (frame[name] == 0) {
                    return false;
                }

                bool ended = false;
                for (int i = name; i < record + FriendListRecordLength; i++) {
                    if (frame[i] == 0) {
                        ended = true;
                    } else if (ended) {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>ZC_ACK_CREATE_CHATROOM / ZC_REFUSE_ENTER_ROOM: one result byte.</summary>
        public static RoZcChatRoomResult ParseZcChatRoomResult(byte[] frame) {
            ushort id = BitConverter.ToUInt16(frame, 0);
            var reader = CreateFixedFrameReader(frame, id, 3);
            return new RoZcChatRoomResult(reader.ReadByte());
        }

        /// <summary>ZC_ROOM_NEWENTRY / ZC_CHANGE_CHATROOM: owner AID, chat id, limit, users, type, the raw title.</summary>
        public static RoChatRoomInfo ParseZcChatRoomInfo(byte[] frame, Encoding encoding = null) {
            ushort id = BitConverter.ToUInt16(frame, 0);
            var reader = CreateVariableFrameReader(frame, id);
            uint owner = reader.ReadUInt32();
            uint chatId = reader.ReadUInt32();
            ushort limit = reader.ReadUInt16();
            ushort users = reader.ReadUInt16();
            byte type = reader.ReadByte();
            string title = reader.Remaining > 0 ? reader.ReadFixedString(reader.Remaining, encoding) : string.Empty;
            return new RoChatRoomInfo(owner, chatId, limit, users, type, title);
        }

        /// <summary>ZC_DESTROY_ROOM: the chat id.</summary>
        public static uint ParseZcDestroyRoom(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcDestroyRoom, 6);
            return reader.ReadUInt32();
        }

        /// <summary>ZC_ENTER_ROOM: the chat id, then (role u32, name[24]) per member (role 0 = owner).</summary>
        public static RoZcChatRoomEnter ParseZcEnterRoom(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcEnterRoom);
            uint chatId = reader.ReadUInt32();
            RequireRecordAlignment(reader, 28, RoPacketIds.ZcEnterRoom);
            var members = new List<RoChatRoomMember>();
            while (!reader.EndOfPacket) {
                uint role = reader.ReadUInt32();
                members.Add(new RoChatRoomMember(reader.ReadFixedString(ActorNameLength, encoding), role == 0));
            }

            return new RoZcChatRoomEnter(chatId, members.ToArray());
        }

        /// <summary>ZC_MEMBER_NEWENTRY (users, name) / ZC_MEMBER_EXIT (users, name, kicked).</summary>
        public static RoZcChatRoomMemberChange ParseZcChatRoomMember(byte[] frame, Encoding encoding = null) {
            ushort id = BitConverter.ToUInt16(frame, 0);
            var reader = CreateFixedFrameReader(frame, id, id == RoPacketIds.ZcMemberExit ? 29 : 28);
            ushort users = reader.ReadUInt16();
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            bool kicked = id == RoPacketIds.ZcMemberExit && reader.ReadByte() != 0;
            return new RoZcChatRoomMemberChange(users, name, kicked);
        }

        /// <summary>ZC_ROLE_CHANGE: role (0 owner, 1 member), name.</summary>
        public static RoZcChatRoomRole ParseZcRoleChange(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcRoleChange, 30);
            uint role = reader.ReadUInt32();
            return new RoZcChatRoomRole(role, reader.ReadFixedString(ActorNameLength, encoding));
        }

        /// <summary>
        /// ZC_ACK_OPEN_BUYING_STORE (slots.B) / ZC_FAILED_OPEN_BUYING_STORE (result.W, weight.L) /
        /// ZC_FAILED_TRADE_BUYING_STORE_TO_BUYER (result.W) / ZC_FAILED_TRADE_BUYING_STORE_TO_SELLER (result.W, item id.L).
        /// </summary>
        public static RoZcBuyingStoreResult ParseZcBuyingStoreResult(byte[] frame) {
            ushort id = BitConverter.ToUInt16(frame, 0);
            switch (id) {
                case RoPacketIds.ZcAckOpenBuyingStore: {
                    var reader = CreateFixedFrameReader(frame, id, 3);
                    return new RoZcBuyingStoreResult(reader.ReadByte(), 0);
                }
                case RoPacketIds.ZcFailedTradeBuyingStoreToBuyer: {
                    var reader = CreateFixedFrameReader(frame, id, 4);
                    return new RoZcBuyingStoreResult(reader.ReadUInt16(), 0);
                }
                default: {
                    var reader = CreateFixedFrameReader(frame, id, 8);
                    ushort result = reader.ReadUInt16();
                    return new RoZcBuyingStoreResult(result, reader.ReadUInt32());
                }
            }
        }

        /// <summary>
        /// ZC_MYITEMLIST_BUYING_STORE (AID, Zeny limit) / ZC_ACK_ITEMLIST_BUYING_STORE (AID, store id, Zeny limit), then
        /// (price.L, amount.W, type.B, item id.L) per wanted item (20181121+ item ids).
        /// </summary>
        public static RoZcBuyingStoreList ParseZcBuyingStoreList(byte[] frame) {
            ushort id = BitConverter.ToUInt16(frame, 0);
            var reader = CreateVariableFrameReader(frame, id);
            uint aid = reader.ReadUInt32();
            uint storeId = id == RoPacketIds.ZcAckItemListBuyingStore ? reader.ReadUInt32() : 0;
            uint limit = reader.ReadUInt32();
            RequireRecordAlignment(reader, 11, id);
            var items = new List<RoBuyingStoreItem>();
            while (!reader.EndOfPacket) {
                uint price = reader.ReadUInt32();
                ushort amount = reader.ReadUInt16();
                byte type = reader.ReadByte();
                items.Add(new RoBuyingStoreItem(reader.ReadUInt32(), amount, price, type));
            }

            return new RoZcBuyingStoreList(aid, storeId, limit, items.ToArray());
        }

        /// <summary>ZC_BUYING_STORE_ENTRY (AID, title[80]) / ZC_DISAPPEAR_BUYING_STORE_ENTRY (AID).</summary>
        public static RoZcBuyingStoreEntry ParseZcBuyingStoreEntry(byte[] frame, Encoding encoding = null) {
            ushort id = BitConverter.ToUInt16(frame, 0);
            if (id == RoPacketIds.ZcDisappearBuyingStoreEntry) {
                return new RoZcBuyingStoreEntry(CreateFixedFrameReader(frame, id, 6).ReadUInt32(), string.Empty);
            }

            var reader = CreateFixedFrameReader(frame, id, 86);
            uint aid = reader.ReadUInt32();
            return new RoZcBuyingStoreEntry(aid, reader.ReadFixedString(80, encoding));
        }

        /// <summary>ZC_DELETE_ITEM_FROM_BUYING_STORE: inventory index (as sent), amount, unit price.</summary>
        public static RoZcBuyingStoreSold ParseZcDeleteItemFromBuyingStore(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcDeleteItemFromBuyingStore, 10);
            ushort index = reader.ReadUInt16();
            ushort amount = reader.ReadUInt16();
            return new RoZcBuyingStoreSold(index, amount, reader.ReadUInt32());
        }

        /// <summary>ZC_UPDATE_ITEM_FROM_BUYING_STORE (20141016+): item id, amount, Zeny paid, Zeny limit left, seller GID, time.</summary>
        public static RoZcBuyingStoreUpdate ParseZcUpdateItemFromBuyingStore(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcUpdateItemFromBuyingStore, 24);
            uint itemId = reader.ReadUInt32();
            ushort amount = reader.ReadUInt16();
            uint zeny = reader.ReadUInt32();
            uint limit = reader.ReadUInt32();
            return new RoZcBuyingStoreUpdate(itemId, amount, zeny, limit, reader.ReadUInt32());
        }

        /// <summary>ZC_REPAIRITEMLIST 0x01fc (20200401): 23-byte records (index.W, item id.L, refine.B, four cards.L).</summary>
        public static RoZcRepairItemList ParseZcRepairItemList(byte[] frame) => ParseEquipSlotInfoList(frame, RoPacketIds.ZcRepairItemList);

        /// <summary>ZC_REPAIRITEMLIST 0x0b65 (20200916+): 24-byte records (index.W, item id.L, four cards.L, refine.B, grade.B).</summary>
        public static RoZcRepairItemList ParseZcRepairItemList2(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcRepairItemList2);
            RequireRecordAlignment(reader, 24, RoPacketIds.ZcRepairItemList2);
            var items = new List<RoRepairItem>();
            while (!reader.EndOfPacket) {
                short index = reader.ReadInt16();
                uint itemId = reader.ReadUInt32();
                var cards = new uint[4];
                for (int c = 0; c < 4; c++) cards[c] = reader.ReadUInt32();
                byte refine = reader.ReadByte();
                reader.ReadByte();   // grade
                items.Add(new RoRepairItem(index, itemId, refine, cards));
            }

            return new RoZcRepairItemList(items.ToArray());
        }

        /// <summary>ZC_NOTIFY_WEAPONITEMLIST 0x0221: the refinable weapons, the same 23-byte records as the repair list.</summary>
        public static RoZcRepairItemList ParseZcNotifyWeaponItemList(byte[] frame) => ParseEquipSlotInfoList(frame, RoPacketIds.ZcNotifyWeaponItemList);

        /// <summary>ZC_ACK_WEAPONREFINE 0x0223: result.L, item id.L.</summary>
        public static RoZcAckWeaponRefine ParseZcAckWeaponRefine(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckWeaponRefine, 10);
            int result = reader.ReadInt32();
            return new RoZcAckWeaponRefine(result, reader.ReadUInt32());
        }

        /// <summary>ZC_PETEGG_LIST 0x01a6: inventory indexes.W.</summary>
        public static RoZcPetEggList ParseZcPetEggList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPetEggList);
            RequireRecordAlignment(reader, 2, RoPacketIds.ZcPetEggList);
            var indexes = new List<short>();
            while (!reader.EndOfPacket) indexes.Add(reader.ReadInt16());
            return new RoZcPetEggList(indexes.ToArray());
        }

        /// <summary>ZC_PET_EVOLUTION_RESULT 0x09fc: result.L (0..5 the failures 0xa0b..0xa10, 6 the Hercules success, 7 egg reset).</summary>
        public static uint ParseZcPetEvolutionResult(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPetEvolutionResult, 6);
            return reader.ReadUInt32();
        }

        /// <summary>ZC_TRYCAPTURE_MONSTER 0x01a0: result.B (0 failed, 1 caught).</summary>
        public static byte ParseZcTryCaptureMonster(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcTryCaptureMonster, 3);
            return reader.ReadByte();
        }

        /// <summary>ZC_MAKABLEITEMLIST 0x018d: 16-byte records (item id.L, three material ids.L).</summary>
        public static RoZcMakableItemList ParseZcMakableItemList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcMakableItemList);
            RequireRecordAlignment(reader, 16, RoPacketIds.ZcMakableItemList);
            var items = new List<uint>();
            while (!reader.EndOfPacket) {
                items.Add(reader.ReadUInt32());
                for (int m = 0; m < 3; m++) reader.ReadUInt32();
            }

            return new RoZcMakableItemList(items.ToArray());
        }

        /// <summary>ZC_ACK_REQMAKINGITEM 0x018f: result.W, item id.L.</summary>
        public static RoZcAckReqMakingItem ParseZcAckReqMakingItem(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckReqMakingItem, 8);
            short result = reader.ReadInt16();
            return new RoZcAckReqMakingItem(result, reader.ReadUInt32());
        }

        /// <summary>ZC_MERGE_ITEM_OPEN 0x096d: inventory indexes.W.</summary>
        public static RoZcMergeItemOpen ParseZcMergeItemOpen(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcMergeItemOpen);
            RequireRecordAlignment(reader, 2, RoPacketIds.ZcMergeItemOpen);
            var indexes = new List<short>();
            while (!reader.EndOfPacket) indexes.Add(reader.ReadInt16());
            return new RoZcMergeItemOpen(indexes.ToArray());
        }

        /// <summary>ZC_ACK_MERGE_ITEM 0x096f: index.W, amount.W, reason.B.</summary>
        public static RoZcAckMergeItem ParseZcAckMergeItem(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckMergeItem, 7);
            short index = reader.ReadInt16();
            short amount = reader.ReadInt16();
            return new RoZcAckMergeItem(index, amount, reader.ReadByte());
        }

        /// <summary>ZC_AUTOSPELLLIST 0x0afb: skill ids.L.</summary>
        public static RoZcAutoSpellList ParseZcAutoSpellList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcAutoSpellList);
            RequireRecordAlignment(reader, 4, RoPacketIds.ZcAutoSpellList);
            var skills = new List<int>();
            while (!reader.EndOfPacket) skills.Add(reader.ReadInt32());
            return new RoZcAutoSpellList(skills.ToArray());
        }

        private static RoZcRepairItemList ParseEquipSlotInfoList(byte[] frame, ushort packetId) {
            var reader = CreateVariableFrameReader(frame, packetId);
            RequireRecordAlignment(reader, 23, packetId);
            var items = new List<RoRepairItem>();
            while (!reader.EndOfPacket) {
                short index = reader.ReadInt16();
                uint itemId = reader.ReadUInt32();
                byte refine = reader.ReadByte();
                var cards = new uint[4];
                for (int c = 0; c < 4; c++) cards[c] = reader.ReadUInt32();
                items.Add(new RoRepairItem(index, itemId, refine, cards));
            }

            return new RoZcRepairItemList(items.ToArray());
        }

        /// <summary>ZC_ACK_ITEMREPAIR: inventory index + 2, result (0 repaired, 1 failed).</summary>
        public static RoZcAckItemRepair ParseZcAckItemRepair(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckItemRepair, 5);
            ushort index = reader.ReadUInt16();
            return new RoZcAckItemRepair(index, reader.ReadByte());
        }

        /// <summary>ZC_ACK_REQNAME_BYGID: GID, name.</summary>
        public static RoZcAckReqNameByGid ParseZcAckReqNameByGid(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckReqNameByGid, ZcAckReqNameByGidLength);
            uint characterId = reader.ReadUInt32();
            return new RoZcAckReqNameByGid(characterId, reader.ReadFixedString(ActorNameLength, encoding));
        }

        /// <summary>ZC_ACK_REQNAME_BYGID2: flag (3 found, 2 not), GID, name.</summary>
        public static RoZcAckReqNameByGid ParseZcAckReqNameByGid2(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckReqNameByGid2, ZcAckReqNameByGid2Length);
            ushort flag = reader.ReadUInt16();
            uint characterId = reader.ReadUInt32();
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            return new RoZcAckReqNameByGid(characterId, flag == 2 ? string.Empty : name);
        }

        public static RoZcFriendState ParseZcFriendState(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcFriendState, ZcFriendStateLength);
            uint accountId = reader.ReadUInt32();
            uint characterId = reader.ReadUInt32();
            byte state = reader.ReadByte();
            string name = reader.Remaining >= ActorNameLength ? reader.ReadFixedString(ActorNameLength, encoding) : string.Empty;
            return new RoZcFriendState(accountId, characterId, state, name);
        }

        public static RoZcFriendRequest ParseZcFriendRequest(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcFriendRequest, ZcFriendRequestLength);
            return new RoZcFriendRequest(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadFixedString(ActorNameLength, encoding));
        }

        public static RoZcFriendAddResult ParseZcFriendAddResult(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcFriendAddResult, ZcFriendAddResultLength);
            return new RoZcFriendAddResult(
                reader.ReadInt16(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadFixedString(ActorNameLength, encoding));
        }

        public static RoZcFriendDelete ParseZcFriendDelete(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcFriendDelete, ZcFriendDeleteLength);
            return new RoZcFriendDelete(reader.ReadUInt32(), reader.ReadUInt32());
        }

        public static RoZcGuildBelongInfo ParseZcGuildBelongInfo(byte[] frame, Encoding encoding = null) {
            // ZC_UPDATE_GDID 0x016c (43) / 0x02f7 (47, 20220216+: the master's GID after the name, not kept)
            ushort id = frame != null && frame.Length >= 2 ? (ushort) (frame[0] | (frame[1] << 8)) : RoPacketIds.ZcGuildBelongInfo;
            var reader = id == RoPacketIds.ZcGuildBelongInfo2
                ? CreateFixedFrameReader(frame, RoPacketIds.ZcGuildBelongInfo2, ZcGuildBelongInfoLength + 4)
                : CreateFixedFrameReader(frame, RoPacketIds.ZcGuildBelongInfo, ZcGuildBelongInfoLength);
            return new RoZcGuildBelongInfo(
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadByte() != 0,
                reader.ReadInt32(),
                reader.ReadFixedString(ActorNameLength, encoding));
        }

        public static RoZcGuildNotice ParseZcGuildNotice(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcGuildNotice, ZcGuildNoticeLength);
            return new RoZcGuildNotice(
                reader.ReadFixedString(60, encoding),
                reader.ReadFixedString(120, encoding));
        }

        public static RoZcGuildMemberInfo ParseZcGuildMemberInfo(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcGuildMemberInfo, ZcGuildMemberInfoLength);
            return new RoZcGuildMemberInfo(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadFixedString(50, encoding),
                reader.ReadFixedString(ActorNameLength, encoding));
        }

        public static RoZcGuildInvite ParseZcGuildInvite(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcGuildInvite, ZcGuildInviteLength);
            return new RoZcGuildInvite(reader.ReadUInt32(), reader.ReadFixedString(ActorNameLength, encoding));
        }

        public static RoZcGuildInviteResult ParseZcGuildInviteResult(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcGuildInviteResult, ZcGuildInviteResultLength);
            return new RoZcGuildInviteResult(reader.ReadByte());
        }

        public static RoZcGuildLeaveResult ParseZcGuildLeaveAck(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcGuildLeaveAck, ZcGuildLeaveAckLength);
            return new RoZcGuildLeaveResult(
                reader.ReadFixedString(ActorNameLength, encoding),
                reader.ReadFixedString(40, encoding));
        }

        public static RoZcGuildChat ParseZcGuildChat(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcGuildChat);
            return new RoZcGuildChat(reader.ReadFixedString(reader.Remaining, encoding));
        }

        public static RoZcPartyChat ParseZcPartyChat(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcPartyChat);
            uint sourceActorId = reader.ReadUInt32();
            return new RoZcPartyChat(reader.ReadFixedString(reader.Remaining, encoding), sourceActorId);
        }

        public static RoZcClanChat ParseZcClanChat(byte[] frame, Encoding encoding = null) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcClanChat);
            string sender = reader.ReadFixedString(Math.Min(ActorNameLength, reader.Remaining), encoding);
            string message = reader.Remaining > 0 ? reader.ReadFixedString(reader.Remaining, encoding) : string.Empty;
            return new RoZcClanChat(sender, message);
        }

        public static RoZcPetProperty ParseZcPetProperty(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPetProperty, ZcPetPropertyLength);
            return new RoZcPetProperty(
                reader.ReadFixedString(ActorNameLength, encoding),
                reader.ReadByte() != 0,
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadUInt16(),
                reader.ReadInt16());
        }

        public static RoZcPetFeedResult ParseZcFeedPet(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcFeedPet, ZcFeedPetLength);
            return new RoZcPetFeedResult(unchecked((sbyte) reader.ReadByte()), reader.ReadUInt32());
        }

        public static RoZcPetChangeState ParseZcPetChangeState(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPetChangeState, ZcPetChangeStateLength);
            return new RoZcPetChangeState(unchecked((sbyte) reader.ReadByte()), reader.ReadUInt32(), reader.ReadInt32());
        }

        public static RoZcPetAction ParseZcPetAct(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcPetAct, ZcPetActLength);
            return new RoZcPetAction(reader.ReadUInt32(), reader.ReadInt32());
        }

        public static RoZcHomunculusProperty ParseZcHomunculusProperty(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcHomunculusProperty, ZcHomunculusPropertyLength);
            return new RoZcHomunculusProperty(
                reader.ReadFixedString(ActorNameLength, encoding),
                reader.ReadByte() != 0,
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadInt16());
        }

        /// <summary>
        /// ZC_PROPERTY_HOMUN 0x0b2f (20200401, 73: 16-bit SP, 32-bit EXP) / 0x0ba4 (20210303+, 85: 32-bit SP, 64-bit
        /// EXP): name, flags (bit 0 renamed, 1 vaporized, 2 dead), level, hunger, intimacy, atk, matk, hit, crit, def,
        /// mdef, flee, amotion, hp, max hp, sp, max sp, exp, next exp, skill points, range (handler 0xc5f010).
        /// </summary>
        public static RoZcHomunculusProperty ParseZcHomunculusPropertyV3(byte[] frame, Encoding encoding = null) {
            bool wide = frame != null && frame.Length >= 2 && BitConverter.ToUInt16(frame, 0) == RoPacketIds.ZcHomunculusProperty4;
            var reader = CreateFixedFrameReader(frame, wide ? RoPacketIds.ZcHomunculusProperty4 : RoPacketIds.ZcHomunculusProperty3, wide ? 85 : 73);
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            byte flags = reader.ReadByte();
            short level = reader.ReadInt16(), hunger = reader.ReadInt16(), intimacy = reader.ReadInt16();
            short atk = reader.ReadInt16(), matk = reader.ReadInt16(), hit = reader.ReadInt16(), crit = reader.ReadInt16();
            short def = reader.ReadInt16(), mdef = reader.ReadInt16(), flee = reader.ReadInt16(), amotion = reader.ReadInt16();
            int hp = reader.ReadInt32(), maxHp = reader.ReadInt32();
            int sp = wide ? reader.ReadInt32() : reader.ReadUInt16();
            int maxSp = wide ? reader.ReadInt32() : reader.ReadUInt16();
            long exp = wide ? reader.ReadInt64() : reader.ReadUInt32();
            long next = wide ? reader.ReadInt64() : reader.ReadUInt32();
            short points = reader.ReadInt16(), range = reader.ReadInt16();
            return new RoZcHomunculusProperty(name, (flags & 1) != 0, level, hunger, intimacy, flags, atk, matk, hit, crit, def, mdef, flee, amotion,
                hp, maxHp, unchecked((short) sp), unchecked((short) maxSp), (int) Math.Min(exp, int.MaxValue), (int) Math.Min(next, int.MaxValue), points, range);
        }

        /// <summary>ZC_HOSKILLINFO_UPDATE 0x0239: skill id.W, level.W, sp.W, range.W, upgradable.B.</summary>
        public static RoZcSkillInfo ParseZcHomunculusSkillUpdate(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcHomunculusSkillUpdate, 11);
            ushort id = reader.ReadUInt16(), level = reader.ReadUInt16(), sp = reader.ReadUInt16(), range = reader.ReadUInt16();
            return new RoZcSkillInfo(id, -1, level, sp, range, string.Empty, reader.ReadByte(), level);
        }

        /// <summary>ZC_MER_PAR_CHANGE 0x02a2: parameter.W, value.L.</summary>
        public static RoZcCompanionParameterChange ParseZcMercenaryParChange(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMercenaryParChange, 8);
            ushort parameter = reader.ReadUInt16();
            return new RoZcCompanionParameterChange(parameter, reader.ReadInt32());
        }

        public static RoZcCompanionChangeState ParseZcHomunculusChangeState(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcHomunculusChangeState, ZcHomunculusChangeStateLength);
            return new RoZcCompanionChangeState(
                unchecked((sbyte) reader.ReadByte()),
                unchecked((sbyte) reader.ReadByte()),
                reader.ReadUInt32(),
                reader.ReadInt32());
        }

        public static RoZcCompanionParameterChange ParseZcHomunculusParChange(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcHomunculusParChange, ZcHomunculusParameterChangeLength);
            return new RoZcCompanionParameterChange(reader.ReadUInt16(), reader.ReadInt32());
        }

        public static RoZcSkillInfoList ParseZcHomunculusSkillInfoList(byte[] frame, Encoding encoding = null) {
            return ParseSkillInfoList(frame, RoPacketIds.ZcHomunculusSkillInfoList, SkillInfoLegacyRecordLength, true, encoding);
        }

        public static RoZcMercenaryInfo ParseZcMercenaryInfo(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcMercenaryInfo, ZcMercenaryInfoLength);
            return new RoZcMercenaryInfo(
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadFixedString(ActorNameLength, encoding),
                reader.ReadInt16(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt16());
        }

        public static RoZcSkillInfoList ParseZcMercenarySkillInfoList(byte[] frame, Encoding encoding = null) {
            return ParseSkillInfoList(frame, RoPacketIds.ZcMercenarySkillInfoList, SkillInfoLegacyRecordLength, true, encoding);
        }

        public static RoZcMapProperty ParseZcNotifyMapProperty(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyMapProperty, ZcMapPropertyLength);
            return new RoZcMapProperty(reader.ReadInt16(), 0, RoPacketIds.ZcNotifyMapProperty);
        }

        public static RoZcMapProperty ParseZcNotifyMapPropertyPk(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyMapPropertyPk, ZcMapPropertyLength);
            return new RoZcMapProperty(reader.ReadInt16(), 0, RoPacketIds.ZcNotifyMapPropertyPk);
        }

        public static RoZcNotifyRanking ParseZcNotifyRanking(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyRanking, 14);
            return new RoZcNotifyRanking(reader.ReadUInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static RoZcBattlefieldScore ParseZcBattlefieldNotifyPoint(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcBattlefieldNotifyPoint, 6);
            return new RoZcBattlefieldScore(reader.ReadInt16(), reader.ReadInt16());
        }

        public static RoZcBattlefieldCampInfo ParseZcBattlefieldNotifyCampInfo(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcBattlefieldNotifyCampInfo, 32);
            uint aid = reader.ReadUInt32();
            string name = reader.ReadFixedString(24, encoding);
            return new RoZcBattlefieldCampInfo(aid, name, reader.ReadInt16());
        }

        public static RoZcBattlefieldPosition ParseZcBattlefieldNotifyPosition(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcBattlefieldNotifyPosition, 36);
            uint aid = reader.ReadUInt32();
            string name = reader.ReadFixedString(24, encoding);
            ushort job = reader.ReadUInt16();
            return new RoZcBattlefieldPosition(aid, name, job, reader.ReadInt16(), reader.ReadInt16());
        }

        public static RoZcBattlefieldHp ParseZcBattlefieldNotifyHpLegacy(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcBattlefieldNotifyHpLegacy, 34);
            uint aid = reader.ReadUInt32();
            reader.Skip(24);
            return new RoZcBattlefieldHp(aid, reader.ReadInt16(), reader.ReadInt16());
        }

        public static RoZcBattlefieldHp ParseZcBattlefieldNotifyHp(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcBattlefieldNotifyHp, 14);
            return new RoZcBattlefieldHp(reader.ReadUInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static RoZcMapProperty ParseZcNotifyMapProperty2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyMapProperty2, ZcMapProperty2Length);
            return new RoZcMapProperty(reader.ReadInt16(), reader.ReadUInt32(), RoPacketIds.ZcNotifyMapProperty2);
        }

        public static RoZcAckToUseSkill ParseZcAckToUseSkill(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckToUseSkill, ZcAckToUseSkillLength);
            return new RoZcAckToUseSkill(reader.ReadUInt16(), reader.ReadInt32(), reader.ReadUInt32(), reader.ReadByte(), reader.ReadByte());
        }

        public static RoZcAttackFailureForDistance ParseZcAttackFailureForDistance(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAttackFailureForDistance, ZcAttackFailureForDistanceLength);
            return new RoZcAttackFailureForDistance(
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16());
        }

        public static RoZcActionFailure ParseZcActionFailure(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcActionFailure, ZcActionFailureLength);
            return new RoZcActionFailure(reader.ReadUInt16());
        }

        public static RoZcUseSkillAck ParseZcUseSkillAckLegacy(byte[] frame) {
            return ParseZcUseSkillAck(frame, RoPacketIds.ZcUseSkillAckLegacy, ZcUseSkillAckLegacyLength, hasDisposable: false, hasAttackMotionTime: false);
        }

        public static RoZcUseSkillAck ParseZcUseSkillAck2(byte[] frame) {
            return ParseZcUseSkillAck(frame, RoPacketIds.ZcUseSkillAck2, ZcUseSkillAck2Length, hasDisposable: true, hasAttackMotionTime: false);
        }

        public static RoZcUseSkillAck ParseZcUseSkillAck3(byte[] frame) {
            return ParseZcUseSkillAck(frame, RoPacketIds.ZcUseSkillAck3, ZcUseSkillAck3Length, hasDisposable: true, hasAttackMotionTime: true);
        }

        public static RoZcNotifySkill ParseZcNotifySkillLegacy(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifySkillLegacy, ZcNotifySkillLegacyLength);
            return new RoZcNotifySkill(
                reader.ReadUInt16(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                unchecked((sbyte) reader.ReadByte()));
        }

        public static RoZcNotifySkill ParseZcNotifySkill2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifySkill2, ZcNotifySkill2Length);
            return new RoZcNotifySkill(
                reader.ReadUInt16(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                unchecked((sbyte) reader.ReadByte()));
        }

        public static RoZcUseSkillResult ParseZcUseSkill(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcUseSkill, ZcUseSkillLength);
            return new RoZcUseSkillResult(
                reader.ReadUInt16(),
                reader.ReadInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadByte());
        }

        public static RoZcNotifyEffect ParseZcNotifyEffect(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyEffect, ZcNotifyEffectLength);
            return new RoZcNotifyEffect(reader.ReadUInt32(), reader.ReadInt32());
        }

        public static RoZcNotifyGroundSkill ParseZcNotifyGroundSkill(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyGroundSkill, ZcNotifyGroundSkillLength);
            return new RoZcNotifyGroundSkill(
                reader.ReadUInt16(),
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadUInt32());
        }

        public static RoZcSkillDisappear ParseZcSkillDisappear(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcSkillDisappear, ZcSkillDisappearLength);
            return new RoZcSkillDisappear(reader.ReadUInt32());
        }

        public static RoZcSkillPostDelay ParseZcSkillPostDelay(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcSkillPostDelay, ZcSkillPostDelayLength);
            return new RoZcSkillPostDelay(reader.ReadUInt16(), reader.ReadUInt32());
        }

        public static RoZcNotifySkillPosition ParseZcNotifySkillPosition(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifySkillPosition, ZcNotifySkillPositionLength);
            return new RoZcNotifySkillPosition(
                reader.ReadUInt16(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                unchecked((sbyte) reader.ReadByte()));
        }

        public static RoZcSkillEntry ParseZcSkillEntry(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcSkillEntry, ZcSkillEntryLength);
            uint unitId = reader.ReadUInt32();
            uint creatorId = reader.ReadUInt32();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            ushort skillId = reader.ReadByte();
            byte visible = reader.ReadByte();
            return new RoZcSkillEntry(skillId, creatorId, x, y, unitId, range: 1, visible, skillLevel: 1);
        }

        public static RoZcSkillEntry ParseZcSkillEntry2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcSkillEntry2, ZcSkillEntry2Length);
            uint unitId = reader.ReadUInt32();
            uint creatorId = reader.ReadUInt32();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            ushort skillId = reader.ReadByte();
            byte visible = reader.ReadByte();
            reader.Skip(1);
            return new RoZcSkillEntry(skillId, creatorId, x, y, unitId, range: 1, visible, skillLevel: 1);
        }

        public static RoZcSkillEntry ParseZcSkillEntry3(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcSkillEntry3);
            if (frame.Length < ZcSkillEntry3MinimumLength) {
                throw new ArgumentException(
                    $"ZC_SKILL_ENTRY3 must be at least {ZcSkillEntry3MinimumLength} bytes, got {frame.Length}.",
                    nameof(frame));
            }

            uint unitId = reader.ReadUInt32();
            uint creatorId = reader.ReadUInt32();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            ushort skillId = reader.ReadByte();
            byte range = ClampToByte(reader.ReadInt16());
            byte visible = reader.ReadByte();
            return new RoZcSkillEntry(skillId, creatorId, x, y, unitId, range, visible, skillLevel: 1);
        }

        public static RoZcSkillEntry ParseZcSkillEntry4(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcSkillEntry4);
            if (frame.Length < ZcSkillEntry4MinimumLength) {
                throw new ArgumentException(
                    $"ZC_SKILL_ENTRY4 must be at least {ZcSkillEntry4MinimumLength} bytes, got {frame.Length}.",
                    nameof(frame));
            }

            uint unitId = reader.ReadUInt32();
            uint creatorId = reader.ReadUInt32();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            ushort skillId = unchecked((ushort) reader.ReadUInt32());
            byte range = ClampToByte(unchecked((sbyte) reader.ReadByte()));
            byte visible = reader.ReadByte();
            return new RoZcSkillEntry(skillId, creatorId, x, y, unitId, range, visible, skillLevel: 1);
        }

        public static RoZcSkillEntry ParseZcSkillEntry5(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcSkillEntry5);
            if (frame.Length < ZcSkillEntry5MinimumLength) {
                throw new ArgumentException(
                    $"ZC_SKILL_ENTRY5 must be at least {ZcSkillEntry5MinimumLength} bytes, got {frame.Length}.",
                    nameof(frame));
            }

            uint unitId = reader.ReadUInt32();
            uint creatorId = reader.ReadUInt32();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            ushort skillId = unchecked((ushort) reader.ReadUInt32());
            return new RoZcSkillEntry(
                skillId,
                creatorId,
                x,
                y,
                unitId,
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte());
        }

        public static RoZcSkillInfoList ParseZcSkillInfoListLegacy(byte[] frame, Encoding encoding = null) {
            return ParseSkillInfoList(frame, RoPacketIds.ZcSkillInfoListLegacy, SkillInfoLegacyRecordLength, true, encoding);
        }

        public static RoZcSkillInfoList ParseZcSkillInfoList(byte[] frame) {
            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcSkillInfoList);
            RequireRecordAlignment(reader, SkillInfoCurrentRecordLength, RoPacketIds.ZcSkillInfoList);
            var skills = new List<RoZcSkillInfo>();
            while (!reader.EndOfPacket) {
                ushort skillId = reader.ReadUInt16();
                int type = reader.ReadInt32();
                ushort level = reader.ReadUInt16();
                ushort spCost = reader.ReadUInt16();
                ushort attackRange = reader.ReadUInt16();
                byte upgradable = reader.ReadByte();
                ushort level2 = reader.ReadUInt16();
                skills.Add(new RoZcSkillInfo(skillId, type, level, spCost, attackRange, string.Empty, upgradable, level2));
            }

            return new RoZcSkillInfoList(skills.ToArray(), hasEmbeddedNames: false);
        }

        public static RoZcSkillInfo ParseZcSkillInfoUpdate(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcSkillInfoUpdate, ZcSkillInfoUpdateLength);
            ushort skillId = reader.ReadUInt16();
            ushort level = reader.ReadUInt16();
            ushort spCost = reader.ReadUInt16();
            ushort attackRange = reader.ReadUInt16();
            byte upgradable = reader.ReadByte();
            return new RoZcSkillInfo(skillId, 0, level, spCost, attackRange, string.Empty, upgradable, level);
        }

        public static RoZcSkillInfo ParseZcAddSkill(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAddSkill, ZcAddSkillLength);
            ushort skillId = reader.ReadUInt16();
            int type = reader.ReadInt32();
            ushort level = reader.ReadUInt16();
            ushort spCost = reader.ReadUInt16();
            ushort attackRange = reader.ReadUInt16();
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            byte upgradable = reader.ReadByte();
            return new RoZcSkillInfo(skillId, type, level, spCost, attackRange, name, upgradable, level);
        }

        public static RoZcSkillInfo ParseZcAutoRunSkill(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAutoRunSkill, ZcAutoRunSkillLength);
            ushort skillId = reader.ReadUInt16();
            int type = reader.ReadInt32();
            ushort level = reader.ReadUInt16();
            ushort spCost = reader.ReadUInt16();
            ushort attackRange = reader.ReadUInt16();
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            byte upgradable = reader.ReadByte();
            return new RoZcSkillInfo(skillId, type, level, spCost, attackRange, name, upgradable, level);
        }

        public static RoZcSkillInfo ParseZcSkillInfoUpdate2(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcSkillInfoUpdate2, ZcSkillInfoUpdate2Length);
            ushort skillId = reader.ReadUInt16();
            int type = reader.ReadInt32();
            ushort level = reader.ReadUInt16();
            ushort spCost = reader.ReadUInt16();
            ushort attackRange = reader.ReadUInt16();
            byte upgradable = reader.ReadByte();
            return new RoZcSkillInfo(skillId, type, level, spCost, attackRange, string.Empty, upgradable, level);
        }

        private static RoZcSkillInfoList ParseSkillInfoList(
            byte[] frame,
            ushort packetId,
            int recordLength,
            bool hasEmbeddedNames,
            Encoding encoding) {
            var reader = CreateVariableFrameReader(frame, packetId);
            RequireRecordAlignment(reader, recordLength, packetId);
            var skills = new List<RoZcSkillInfo>();
            while (!reader.EndOfPacket) {
                ushort skillId = reader.ReadUInt16();
                int type = reader.ReadInt32();
                ushort level = reader.ReadUInt16();
                ushort spCost = reader.ReadUInt16();
                ushort attackRange = reader.ReadUInt16();
                string name = hasEmbeddedNames ? reader.ReadFixedString(ActorNameLength, encoding) : string.Empty;
                byte upgradable = reader.ReadByte();
                ushort level2 = hasEmbeddedNames ? level : reader.ReadUInt16();
                skills.Add(new RoZcSkillInfo(skillId, type, level, spCost, attackRange, name, upgradable, level2));
            }

            return new RoZcSkillInfoList(skills.ToArray(), hasEmbeddedNames);
        }

        public static RoZcShortcutKeyList ParseZcShortcutKeyList(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcShortcutKeyList, ZcShortcutKeyListLength);
            byte rotate = reader.ReadByte();
            short tab = reader.ReadInt16();
            var shortcuts = new RoShortcutKeyEntry[ShortcutKeyCount];
            for (int i = 0; i < shortcuts.Length; i++) {
                sbyte type = unchecked((sbyte) reader.ReadByte());
                uint id = reader.ReadUInt32();
                short count = reader.ReadInt16();
                shortcuts[i] = new RoShortcutKeyEntry(type, id, count);
            }

            return new RoZcShortcutKeyList(rotate, tab, shortcuts);
        }

        public static RoZcNotifyUnreadMail ParseZcNotifyUnreadMail(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcNotifyUnreadMail, ZcNotifyUnreadMailLength);
            return new RoZcNotifyUnreadMail(reader.ReadByte());
        }

        public static RoZcRodexMailList ParseZcAckRodexList(byte[] frame, Encoding encoding = null) {
            return ParseRodexListWithSingleOpenType(frame, RoPacketIds.ZcAckRodexList, encoding);
        }

        public static RoZcRodexMailList ParseZcAckRodexList2(byte[] frame, Encoding encoding = null) {
            return ParseRodexListWithSingleOpenType(frame, RoPacketIds.ZcAckRodexList2, encoding);
        }

        public static RoZcRodexMailList ParseZcAckRodexList3(byte[] frame, Encoding encoding = null) {
            return ParseRodexListMixedOpenTypes(frame, RoPacketIds.ZcAckRodexList3, hasTrailingUnknown: false, encoding);
        }

        public static RoZcRodexMailList ParseZcAckRodexList4(byte[] frame, Encoding encoding = null) {
            return ParseRodexListMixedOpenTypes(frame, RoPacketIds.ZcAckRodexList4, hasTrailingUnknown: false, encoding);
        }

        public static void ParseZcAckFailedAllRodexList(byte[] frame) {
            CreateFixedFrameReader(frame, RoPacketIds.ZcAckFailedAllRodexList, 2);
        }

        public static RoZcRodexReadMail ParseZcAckReadRodex(byte[] frame, Encoding encoding = null) {
            return ParseRodexRead(frame, RoPacketIds.ZcAckReadRodex, hasGrade: false, encoding);
        }

        public static RoZcRodexReadMail ParseZcAckReadRodex2(byte[] frame, Encoding encoding = null) {
            return ParseRodexRead(frame, RoPacketIds.ZcAckReadRodex2, hasGrade: true, encoding);
        }

        public static RoZcRodexSendAck ParseZcAckSendRodex(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckSendRodex, 3);
            return new RoZcRodexSendAck(reader.ReadByte());
        }

        public static RoZcRodexAttachmentAck ParseZcAckZenyFromRodex(byte[] frame) {
            return ParseRodexAttachmentAck(frame, RoPacketIds.ZcAckZenyFromRodex);
        }

        public static RoZcRodexAttachmentAck ParseZcAckItemFromRodex(byte[] frame) {
            return ParseRodexAttachmentAck(frame, RoPacketIds.ZcAckItemFromRodex);
        }

        public static RoZcRodexDeleteAck ParseZcAckDeleteRodex(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckDeleteRodex, 11);
            byte openType = reader.ReadByte();
            ulong mailId = reader.ReadUInt64();
            return new RoZcRodexDeleteAck(openType, mailId);
        }

        public static RoZcRodexAddItemAck ParseZcAckAddItemRodex(byte[] frame) {
            return ParseRodexAddItemAck(frame, RoPacketIds.ZcAckAddItemRodex, hasGradeLayout: false);
        }

        public static RoZcRodexAddItemAck ParseZcAckAddItemRodex2(byte[] frame) {
            return ParseRodexAddItemAck(frame, RoPacketIds.ZcAckAddItemRodex2, hasGradeLayout: true);
        }

        public static RoZcRodexRemoveItemAck ParseZcAckRemoveRodexItem(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckRemoveRodexItem, 9);
            byte result = reader.ReadByte();
            ushort index = reader.ReadUInt16();
            ushort count = reader.ReadUInt16();
            ushort weight = reader.ReadUInt16();
            return new RoZcRodexRemoveItemAck(result, index, count, weight);
        }

        public static RoZcRodexOpenWriteAck ParseZcAckOpenWriteRodex(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcAckOpenWriteRodex, 27);
            string receiverName = reader.ReadFixedString(ActorNameLength, encoding);
            byte result = reader.ReadByte();
            return new RoZcRodexOpenWriteAck(receiverName, result);
        }

        public static RoZcRodexCheckNameAck ParseZcCheckReceiveCharacterName(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcCheckReceiveCharacterName, 10);
            uint characterId = reader.ReadUInt32();
            short job = reader.ReadInt16();
            short level = reader.ReadInt16();
            return new RoZcRodexCheckNameAck(characterId, job, level, string.Empty);
        }

        public static RoZcRodexCheckNameAck ParseZcCheckReceiveCharacterName2(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.ZcCheckReceiveCharacterName2, 34);
            uint characterId = reader.ReadUInt32();
            short job = reader.ReadInt16();
            short level = reader.ReadInt16();
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            return new RoZcRodexCheckNameAck(characterId, job, level, name);
        }

        private static RoZcRodexMailList ParseRodexListWithSingleOpenType(byte[] frame, ushort packetId, Encoding encoding) {
            var reader = CreateVariableFrameReader(frame, packetId);
            byte openType = reader.ReadByte();
            int count = reader.ReadByte();
            bool isEnd = reader.ReadByte() != 0;
            var mails = new List<RoRodexMailHeader>(Math.Max(0, count));
            for (int i = 0; i < count && reader.Remaining > 0; i++) {
                EnsureRodexListRemaining(reader, 44, packetId, i, "single-open-type header");
                ulong mailId = reader.ReadUInt64();
                bool isRead = reader.ReadByte() != 0;
                byte type = reader.ReadByte();
                string sender = reader.ReadFixedString(ActorNameLength, encoding);
                reader.ReadInt32();
                int expire = reader.ReadInt32();
                short titleLength = reader.ReadInt16();
                string title = ReadRodexTitle(reader, titleLength, packetId, i, encoding);
                mails.Add(new RoRodexMailHeader(openType, mailId, isRead, type, sender, expire, title));
            }

            return new RoZcRodexMailList(isEnd, mails.ToArray());
        }

        private static RoZcRodexMailList ParseRodexListMixedOpenTypes(byte[] frame, ushort packetId, bool hasTrailingUnknown, Encoding encoding) {
            var reader = CreateVariableFrameReader(frame, packetId);
            bool isEnd = reader.ReadByte() != 0;
            var mails = new List<RoRodexMailHeader>();
            int rowIndex = 0;
            while (reader.Remaining > 0) {
                EnsureRodexListRemaining(reader, 41, packetId, rowIndex, "mixed-open-type header");
                byte openType = reader.ReadByte();
                ulong mailId = reader.ReadUInt64();
                bool isRead = reader.ReadByte() != 0;
                byte type = reader.ReadByte();
                string sender = reader.ReadFixedString(ActorNameLength, encoding);
                int expire = reader.ReadInt32();
                short titleLength = reader.ReadInt16();
                string title = ReadRodexTitle(reader, titleLength, packetId, rowIndex, encoding);
                if (hasTrailingUnknown && reader.Remaining >= 4) {
                    reader.ReadInt32();
                }

                mails.Add(new RoRodexMailHeader(openType, mailId, isRead, type, sender, expire, title));
                rowIndex++;
            }

            return new RoZcRodexMailList(isEnd, mails.ToArray());
        }

        private static void EnsureRodexListRemaining(RoPacketReader reader, int required, ushort packetId, int rowIndex, string section) {
            if (reader.Remaining >= required) {
                return;
            }

            throw new ArgumentException(
                $"Packet 0x{packetId:x4} length {reader.Length} has malformed Rodex list row {rowIndex}: {section} requires {required} bytes, remaining {reader.Remaining}.");
        }

        private static string ReadRodexTitle(RoPacketReader reader, short titleLength, ushort packetId, int rowIndex, Encoding encoding = null) {
            if (titleLength <= 0) {
                return string.Empty;
            }

            if (reader.Remaining < titleLength) {
                throw new ArgumentException(
                    $"Packet 0x{packetId:x4} length {reader.Length} has malformed Rodex list row {rowIndex}: title length {titleLength} exceeds remaining {reader.Remaining}.");
            }

            return reader.ReadFixedString(titleLength, encoding);
        }

        private static RoZcRodexReadMail ParseRodexRead(byte[] frame, ushort packetId, bool hasGrade, Encoding encoding) {
            if (frame != null && frame.Length < RodexReadHeaderLength) {
                throw new ArgumentException($"Packet 0x{packetId:x4} is too short for a Rodex read header.", nameof(frame));
            }

            var reader = CreateVariableFrameReader(frame, packetId);
            byte openType = reader.ReadByte();
            ulong mailId = reader.ReadUInt64();
            short bodyLength = reader.ReadInt16();
            long zeny = reader.ReadInt64();
            byte itemCount = reader.ReadByte();
            string body = bodyLength > 0 && reader.Remaining >= bodyLength
                ? reader.ReadFixedString(bodyLength, encoding)
                : string.Empty;
            var items = new List<RoZcInventoryItem>(itemCount);
            for (int i = 0; i < itemCount && reader.Remaining > 0; i++) {
                items.Add(ParseRodexReadItem(reader, hasGrade, (short) (i + 1)));
            }

            return new RoZcRodexReadMail(openType, mailId, body, zeny, items.ToArray());
        }

        private static RoZcInventoryItem ParseRodexReadItem(RoPacketReader reader, bool hasGrade, short displayIndex) {
            short count = reader.ReadInt16();
            uint itemId = reader.ReadUInt32();
            byte identified = reader.ReadByte();
            reader.ReadByte();
            byte refine = 0;
            int[] cards;
            if (hasGrade) {
                cards = ReadCardIds(reader);
            } else {
                refine = reader.ReadByte();
                cards = ReadCardIds(reader);
            }

            uint location = reader.ReadUInt32();
            byte itemType = reader.ReadByte();
            ushort viewId = reader.ReadUInt16();
            reader.ReadUInt16();
            RoPacketItemRandomOption[] options = ReadRandomOptions(reader);
            byte grade = 0;
            if (hasGrade) {
                refine = reader.ReadByte();
                grade = reader.ReadByte();
            }

            byte flag = identified != 0 ? (byte) 0x01 : (byte) 0x00;
            return new RoZcInventoryItem(
                displayIndex,
                itemId,
                itemType,
                count,
                location,
                wearStateRaw: 0,
                refine,
                cards,
                viewId,
                flag,
                isEquipment: location != 0,
                enchantGrade: grade,
                randomOptions: options);
        }

        private static RoZcRodexAttachmentAck ParseRodexAttachmentAck(byte[] frame, ushort packetId) {
            var reader = CreateFixedFrameReader(frame, packetId, 12);
            ulong mailId = reader.ReadUInt64();
            byte openType = reader.ReadByte();
            byte result = reader.ReadByte();
            return new RoZcRodexAttachmentAck(mailId, openType, result);
        }

        private static RoZcRodexAddItemAck ParseRodexAddItemAck(byte[] frame, ushort packetId, bool hasGradeLayout) {
            int expectedLength = hasGradeLayout ? 64 : 63;
            var reader = CreateFixedFrameReader(frame, packetId, expectedLength);
            byte result = reader.ReadByte();
            if (result != 0) {
                return new RoZcRodexAddItemAck(result, 0, 0, default, 0);
            }

            ushort index = reader.ReadUInt16();
            ushort count = reader.ReadUInt16();
            uint itemId = reader.ReadUInt32();
            byte type = reader.ReadByte();
            byte identified = reader.ReadByte();
            reader.ReadByte();
            byte refine = 0;
            int[] cards;
            if (hasGradeLayout) {
                cards = ReadCardIds(reader);
            } else {
                refine = reader.ReadByte();
                cards = ReadCardIds(reader);
            }

            RoPacketItemRandomOption[] options = ReadRandomOptions(reader);
            ushort weight = reader.ReadUInt16();
            reader.ReadByte();
            uint location = reader.ReadUInt32();
            byte grade = 0;
            if (hasGradeLayout) {
                refine = reader.ReadByte();
                grade = reader.ReadByte();
            }

            byte flag = identified != 0 ? (byte) 0x01 : (byte) 0x00;
            var item = new RoZcInventoryItem(
                (short) index,
                itemId,
                type,
                (short) count,
                location,
                wearStateRaw: 0,
                refine,
                cards,
                viewIdHint: 0,
                flag,
                isEquipment: location != 0,
                enchantGrade: grade,
                randomOptions: options);
            return new RoZcRodexAddItemAck(result, index, count, item, weight);
        }

        public static RoZcAchievementList ParseZcAllAchievementList(byte[] frame) {
            if (frame != null && frame.Length < AchievementListHeaderLength) {
                throw new ArgumentException(
                    $"Packet 0x{RoPacketIds.ZcAllAchievementList:x4} must include a {AchievementListHeaderLength}-byte achievement header, got {frame.Length}.",
                    nameof(frame));
            }

            var reader = CreateVariableFrameReader(frame, RoPacketIds.ZcAllAchievementList);
            uint totalAchievements = reader.ReadUInt32();
            uint totalPoints = reader.ReadUInt32();
            ushort rank = reader.ReadUInt16();
            uint currentRankPoints = reader.ReadUInt32();
            uint nextRankPoints = reader.ReadUInt32();
            if (reader.Remaining % AchievementListEntryLength != 0) {
                throw new ArgumentException(
                    $"Packet 0x{RoPacketIds.ZcAllAchievementList:x4} has {reader.Remaining} achievement payload byte(s), which is not divisible by {AchievementListEntryLength}.",
                    nameof(frame));
            }

            var entries = new List<RoZcAchievementEntry>(reader.Remaining / AchievementListEntryLength);
            while (!reader.EndOfPacket) {
                uint id = reader.ReadUInt32();
                bool completed = reader.ReadByte() != 0;
                var objectives = new uint[AchievementObjectiveCount];
                for (int i = 0; i < objectives.Length; i++) {
                    objectives[i] = reader.ReadUInt32();
                }

                uint completedAtRaw = reader.ReadUInt32();
                bool rewardClaimed = reader.ReadByte() != 0;
                entries.Add(new RoZcAchievementEntry(
                    id,
                    completed,
                    objectives,
                    completedAtRaw == 0 ? (uint?) null : completedAtRaw,
                    rewardClaimed));
            }

            return new RoZcAchievementList(
                totalAchievements,
                totalPoints,
                rank,
                currentRankPoints,
                nextRankPoints,
                entries.ToArray());
        }

        private static RoZcQuestEntry ReadQuestEntryAdd(RoPacketReader reader, Encoding encoding) {
            uint questId = reader.ReadUInt32();
            byte active = reader.ReadByte();
            int serverTime = reader.ReadInt32();
            int endTime = reader.ReadInt32();
            short objectiveCount = reader.ReadInt16();
            var objectives = new List<RoZcQuestObjective>();
            int fixedRecords = Math.Min(3, Math.Max(0, (int) objectiveCount));
            for (int i = 0; i < 3; i++) {
                RoZcQuestObjective objective = ReadQuestObjectiveAdd(reader, encoding);
                if (i < fixedRecords) {
                    objectives.Add(objective);
                }
            }

            return new RoZcQuestEntry(questId, active, serverTime, endTime, objectives.ToArray());
        }

        /// <summary>
        /// ZC_ADD_QUEST_EX's 46-byte slot: the one counter is the current count (clif_quest_send's qd->count); the required
        /// count only comes with the ZC_HUNTING_QUEST_INFO that follows (FUN_00c43080 leaves it 0).
        /// </summary>
        private static RoZcQuestObjective ReadQuestObjectiveAdd(RoPacketReader reader, Encoding encoding) {
            return new RoZcQuestObjective(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                current: reader.ReadInt16(),
                total: 0,
                mobName: reader.ReadFixedString(ActorNameLength, encoding));
        }

        private static RoZcQuestObjective ReadQuestObjectiveList(RoPacketReader reader, Encoding encoding) {
            return new RoZcQuestObjective(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadFixedString(ActorNameLength, encoding));
        }

        private static RoZcQuestObjectiveUpdate ParseQuestObjectiveUpdate(
            byte[] frame,
            ushort packetId,
            bool hasCount,
            bool modern,
            bool hasHuntIdent2) {
            var reader = CreateVariableFrameReader(frame, packetId);
            int count = hasCount ? reader.ReadInt16() : reader.Remaining / QuestObjectiveCounterLegacyRecordLength;
            var updates = new List<RoZcQuestObjectiveCounter>(Math.Max(0, count));
            for (int i = 0; i < count && !reader.EndOfPacket; i++) {
                int recordLength = hasHuntIdent2 ? QuestObjectiveCounterModernRecordLength : QuestObjectiveCounterLegacyRecordLength;
                RequireRemaining(reader, recordLength, packetId);
                uint questId = reader.ReadUInt32();
                uint first = reader.ReadUInt32();
                uint second = hasHuntIdent2 ? reader.ReadUInt32() : 0u;
                ushort total = reader.ReadUInt16();
                ushort current = reader.ReadUInt16();
                updates.Add(modern
                    ? new RoZcQuestObjectiveCounter(questId, first, second, 0, current, total)
                    : new RoZcQuestObjectiveCounter(questId, 0, 0, first, current, total));
            }

            return new RoZcQuestObjectiveUpdate(updates.ToArray());
        }

        private static RoZcPartyInfo ParsePartyInfo(
            byte[] frame,
            ushort packetId,
            int recordLength,
            bool hasCharacterId,
            bool hasClassAndLevel,
            Encoding encoding) {
            var reader = CreateVariableFrameReader(frame, packetId);
            string partyName = reader.ReadFixedString(ActorNameLength, encoding);
            RequireRecordAlignment(reader, recordLength, packetId);
            var members = new List<RoZcPartyMember>();
            while (!reader.EndOfPacket) {
                uint accountId = reader.ReadUInt32();
                uint characterId = hasCharacterId ? reader.ReadUInt32() : 0u;
                string name = reader.ReadFixedString(ActorNameLength, encoding);
                string mapName = reader.ReadFixedString(16, encoding);
                byte role = reader.ReadByte();
                byte state = reader.ReadByte();
                int classId = 0;
                int baseLevel = 0;
                if (hasClassAndLevel) {
                    classId = reader.ReadInt16();
                    baseLevel = reader.ReadInt16();
                }

                members.Add(new RoZcPartyMember(accountId, characterId, name, mapName, role, state, classId, baseLevel, 0, 0));
            }

            return new RoZcPartyInfo(partyName, members.ToArray());
        }

        private static RoZcPartyMemberInfo ParsePartyMemberInfo(
            byte[] frame,
            ushort packetId,
            int expectedLength,
            bool hasCharacterId,
            bool hasClassAndLevel,
            bool hasPosition,
            bool hasRules,
            Encoding encoding) {
            var reader = CreateFixedFrameReader(frame, packetId, expectedLength);
            uint accountId = reader.ReadUInt32();
            uint characterId = hasCharacterId ? reader.ReadUInt32() : 0u;
            uint roleRaw = hasPosition ? reader.ReadUInt32() : 0u;
            int classId = 0;
            int baseLevel = 0;
            if (hasClassAndLevel) {
                classId = reader.ReadInt16();
                baseLevel = reader.ReadInt16();
            }

            int x = 0;
            int y = 0;
            byte state = 0;
            if (hasPosition) {
                x = reader.ReadInt16();
                y = reader.ReadInt16();
                state = reader.ReadByte();
            }

            string partyName = reader.ReadFixedString(ActorNameLength, encoding);
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            string mapName = reader.ReadFixedString(16, encoding);
            byte pickup = 0;
            byte division = 0;
            if (hasRules && reader.Remaining >= 2) {
                pickup = reader.ReadByte();
                division = reader.ReadByte();
            }

            var member = new RoZcPartyMember(
                accountId,
                characterId,
                name,
                mapName,
                unchecked((byte) roleRaw),
                state,
                classId,
                baseLevel,
                x,
                y);
            return new RoZcPartyMemberInfo(partyName, member, hasRules, pickup, division);
        }

        private static RoZcPartyInvite ParsePartyInvite(byte[] frame, ushort packetId, Encoding encoding) {
            var reader = CreateFixedFrameReader(frame, packetId, ZcPartyInviteLength);
            return new RoZcPartyInvite(reader.ReadUInt32(), reader.ReadFixedString(ActorNameLength, encoding));
        }

        private static RoZcNpcMarketList ParseZcNpcMarketOpen(byte[] frame, ushort packetId, int recordLength, bool hasLocation) {
            var reader = CreateVariableFrameReader(frame, packetId);
            RequireRecordAlignment(reader, recordLength, packetId);
            var items = new List<RoNpcMarketItem>();
            uint shopIndex = 0;
            while (!reader.EndOfPacket) {
                uint itemId = reader.ReadUInt32();
                byte itemType = reader.ReadByte();
                uint price = reader.ReadUInt32();
                uint stock = reader.ReadUInt32();
                uint weight = reader.ReadUInt16();
                uint location = hasLocation ? reader.ReadUInt32() : 0u;
                items.Add(new RoNpcMarketItem(itemId, itemType, price, stock, stock, weight, location, shopIndex++));
            }

            return new RoZcNpcMarketList(items.ToArray());
        }

        private static RoZcNpcMarketPurchaseResult ParseZcNpcMarketPurchaseResult(byte[] frame, ushort packetId, int resultLength) {
            var reader = CreateVariableFrameReader(frame, packetId);
            ushort result;
            if (resultLength == 1) {
                result = reader.ReadByte();
            } else if (resultLength == 2) {
                result = reader.ReadUInt16();
            } else {
                throw new ArgumentOutOfRangeException(nameof(resultLength));
            }

            RequireRecordAlignment(reader, NpcMarketResultRecordLength20240619, packetId);
            var items = new List<RoNpcMarketResultItem>();
            while (!reader.EndOfPacket) {
                items.Add(new RoNpcMarketResultItem(reader.ReadUInt32(), reader.ReadUInt16(), reader.ReadUInt32()));
            }

            return new RoZcNpcMarketPurchaseResult(result, items.ToArray());
        }

        private static void RequireRemaining(RoPacketReader reader, int byteCount, ushort packetId) {
            if (reader.Remaining < byteCount) {
                throw new ArgumentException(
                    $"Packet 0x{packetId:x4} ended while reading a {byteCount}-byte record; {reader.Remaining} byte(s) remain.");
            }
        }

        private static byte ClampToByte(int value) {
            if (value <= 0) {
                return 0;
            }

            return value >= byte.MaxValue ? byte.MaxValue : (byte) value;
        }

        private static RoZcUseSkillAck ParseZcUseSkillAck(
            byte[] frame,
            ushort expectedPacketId,
            int expectedLength,
            bool hasDisposable,
            bool hasAttackMotionTime) {
            var reader = CreateFixedFrameReader(frame, expectedPacketId, expectedLength);
            uint sourceId = reader.ReadUInt32();
            uint targetId = reader.ReadUInt32();
            ushort x = reader.ReadUInt16();
            ushort y = reader.ReadUInt16();
            ushort skillId = reader.ReadUInt16();
            uint element = reader.ReadUInt32();
            uint delayTime = reader.ReadUInt32();
            byte disposable = hasDisposable ? reader.ReadByte() : (byte) 0;
            uint attackMotionTime = hasAttackMotionTime ? reader.ReadUInt32() : 0u;
            return new RoZcUseSkillAck(sourceId, targetId, x, y, skillId, element, delayTime, disposable, attackMotionTime);
        }

        private static RoZcInventoryItem ParseStorageListItem(RoPacketReader reader, ushort packetId) {
            switch (packetId) {
                case RoPacketIds.ZcStorageItemListNormal:
                    return ParseLegacyListNormalItem(reader);
                case RoPacketIds.ZcStorageItemListEquip:
                    return ParseLegacyListEquipmentItem(reader);
                case RoPacketIds.ZcStorageItemListNormal2:
                    return ParseModernStorageListNormalItem(reader, hasHireExpireDate: false);
                case RoPacketIds.ZcStorageItemListEquip2:
                    return ParseModernStorageListEquipmentItem(reader, hasHireExpireDate: true, hasBindAndViewId: false);
                case RoPacketIds.ZcStorageItemListEquip3:
                    return ParseModernStorageListEquipmentItem(reader, hasHireExpireDate: true, hasBindAndViewId: true);
                case RoPacketIds.ZcStorageItemListNormal3:
                    return ParseModernStorageListNormalItem(reader, hasHireExpireDate: true);
                case RoPacketIds.ZcStorageItemListNormal4:
                    return ParseLatestListNormalItem(reader);
                case RoPacketIds.ZcStorageItemListEquip4:
                    return ParseLatestListEquipmentItem(reader, hasOptions: false);
                case RoPacketIds.ZcStorageItemListEquip5:
                    return ParseLatestListEquipmentItem(reader, hasOptions: true);
                default:
                    throw new InvalidOperationException($"Opcode 0x{packetId:x4} is not a supported storage list packet.");
            }
        }

        private static RoZcInventoryItem ParseCartListItem(RoPacketReader reader, ushort packetId) {
            switch (packetId) {
                case RoPacketIds.ZcNonstackableItemListCart:
                    return ParseLegacyListNormalItem(reader);
                case RoPacketIds.ZcEquipItemListCart:
                    return ParseLegacyListEquipmentItem(reader);
                case RoPacketIds.ZcNonstackableItemListCart2:
                    return ParseModernCartListNormalItem(reader);
                case RoPacketIds.ZcEquipItemListCart2:
                    return ParseModernStorageListEquipmentItem(reader, hasHireExpireDate: true, hasBindAndViewId: true);
                case RoPacketIds.ZcCartNormalItemList4:
                    return ParseLatestListNormalItem(reader);
                case RoPacketIds.ZcCartEquipItemList4:
                    return ParseLatestListEquipmentItem(reader, hasOptions: false);
                case RoPacketIds.ZcCartEquipItemList5:
                    return ParseLatestListEquipmentItem(reader, hasOptions: true);
                default:
                    throw new InvalidOperationException($"Opcode 0x{packetId:x4} is not a supported cart list packet.");
            }
        }

        private static RoZcInventoryItem ParseLegacyListNormalItem(RoPacketReader reader) {
            short index = reader.ReadInt16();
            uint itemId = reader.ReadUInt16();
            byte itemType = reader.ReadByte();
            bool identified = reader.ReadByte() != 0;
            short count = reader.ReadInt16();
            uint wearState = reader.ReadUInt16();
            return new RoZcInventoryItem(
                index,
                itemId,
                itemType,
                count,
                equipLocationRaw: 0,
                wearStateRaw: wearState,
                refineLevel: 0,
                cardIds: null,
                viewIdHint: 0,
                flag: identified ? (byte) 0x01 : (byte) 0x00,
                isEquipment: false);
        }

        private static RoZcInventoryItem ParseLegacyListEquipmentItem(RoPacketReader reader) {
            short index = reader.ReadInt16();
            uint itemId = reader.ReadUInt16();
            byte itemType = reader.ReadByte();
            bool identified = reader.ReadByte() != 0;
            uint location = reader.ReadUInt16();
            uint wearState = reader.ReadUInt16();
            bool damaged = reader.ReadByte() != 0;
            byte refine = reader.ReadByte();
            int[] cards = ReadCardIds16(reader);
            byte flag = identified ? (byte) 0x01 : (byte) 0x00;
            if (damaged) {
                flag |= 0x02;
            }

            return new RoZcInventoryItem(
                index,
                itemId,
                itemType,
                1,
                location,
                wearState,
                refine,
                cards,
                0,
                flag,
                isEquipment: true);
        }

        private static RoZcInventoryItem ParseModernStorageListNormalItem(RoPacketReader reader, bool hasHireExpireDate) {
            short index = reader.ReadInt16();
            uint itemId = reader.ReadUInt16();
            byte itemType = reader.ReadByte();
            bool identified = reader.ReadByte() != 0;
            short count = reader.ReadInt16();
            uint wearState = reader.ReadUInt16();
            int[] cards = ReadCardIds16(reader);
            if (hasHireExpireDate) {
                reader.ReadInt32();
            }

            return new RoZcInventoryItem(
                index,
                itemId,
                itemType,
                count,
                equipLocationRaw: 0,
                wearStateRaw: wearState,
                refineLevel: 0,
                cards,
                viewIdHint: 0,
                flag: identified ? (byte) 0x01 : (byte) 0x00,
                isEquipment: false);
        }

        private static RoZcInventoryItem ParseModernCartListNormalItem(RoPacketReader reader) {
            short index = reader.ReadInt16();
            uint itemId = reader.ReadUInt16();
            byte itemType = reader.ReadByte();
            bool identified = reader.ReadByte() != 0;
            short count = reader.ReadInt16();
            uint wearState = reader.ReadUInt16();
            int[] cards = ReadCardIds16(reader);
            reader.ReadInt32();
            return new RoZcInventoryItem(
                index,
                itemId,
                itemType,
                count,
                equipLocationRaw: 0,
                wearStateRaw: wearState,
                refineLevel: 0,
                cards,
                viewIdHint: 0,
                flag: identified ? (byte) 0x01 : (byte) 0x00,
                isEquipment: false);
        }

        private static RoZcInventoryItem ParseModernStorageListEquipmentItem(
            RoPacketReader reader,
            bool hasHireExpireDate,
            bool hasBindAndViewId) {
            RoZcInventoryItem item = ParseLegacyListEquipmentItem(reader);
            if (hasHireExpireDate) {
                reader.ReadInt32();
            }

            ushort viewId = 0;
            if (hasBindAndViewId) {
                reader.ReadUInt16();
                viewId = reader.ReadUInt16();
            }

            return new RoZcInventoryItem(
                item.BagIndex,
                item.ItemId,
                item.ItemTypeRaw,
                item.Count,
                item.EquipLocationRaw,
                item.WearStateRaw,
                item.RefineLevel,
                item.CardIds,
                viewId,
                item.Flag,
                isEquipment: true);
        }

        private static RoZcInventoryItem ParseLatestListNormalItem(RoPacketReader reader) {
            short index = reader.ReadInt16();
            uint itemId = reader.ReadUInt16();
            byte itemType = reader.ReadByte();
            short count = reader.ReadInt16();
            uint wearState = reader.ReadUInt32();
            int[] cards = ReadCardIds16(reader);
            reader.ReadInt32();
            byte flag = reader.ReadByte();
            return new RoZcInventoryItem(
                index,
                itemId,
                itemType,
                count,
                equipLocationRaw: 0,
                wearStateRaw: wearState,
                refineLevel: 0,
                cards,
                viewIdHint: 0,
                flag,
                isEquipment: false);
        }

        private static RoZcInventoryItem ParseLatestListEquipmentItem(RoPacketReader reader, bool hasOptions) {
            short index = reader.ReadInt16();
            uint itemId = reader.ReadUInt16();
            byte itemType = reader.ReadByte();
            uint location = reader.ReadUInt32();
            uint wearState = reader.ReadUInt32();
            byte refine = reader.ReadByte();
            int[] cards = ReadCardIds16(reader);
            reader.ReadInt32();
            reader.ReadUInt16();
            ushort viewId = reader.ReadUInt16();
            byte flag;
            if (hasOptions) {
                reader.ReadByte();
                reader.Skip(25);
                flag = reader.ReadByte();
            } else {
                flag = reader.ReadByte();
            }

            return new RoZcInventoryItem(
                index,
                itemId,
                itemType,
                1,
                location,
                wearState,
                refine,
                cards,
                viewId,
                flag,
                isEquipment: true);
        }

        private static RoZcStorageItem ParseLegacyStorageAddedItem(RoPacketReader reader) {
            short index = reader.ReadInt16();
            int count = reader.ReadInt32();
            uint itemId = reader.ReadUInt16();
            bool identified = reader.Remaining >= 1 && reader.ReadByte() != 0;
            bool damaged = reader.Remaining >= 1 && reader.ReadByte() != 0;
            byte refine = reader.Remaining >= 1 ? reader.ReadByte() : (byte) 0;
            int[] cards = ReadRemainingCardIds16(reader);
            return new RoZcStorageItem(index, count, itemId, 0, identified, damaged, refine, cards);
        }

        private static RoZcStorageItem ParseModernStorageAddedItem(RoPacketReader reader) {
            short index = reader.ReadInt16();
            int count = reader.ReadInt32();
            uint itemId = reader.ReadUInt16();
            byte type = reader.ReadByte();
            bool identified = reader.ReadByte() != 0;
            bool damaged = reader.ReadByte() != 0;
            byte refine = reader.ReadByte();
            int[] cards = ReadRemainingCardIds16(reader);
            return new RoZcStorageItem(index, count, itemId, type, identified, damaged, refine, cards);
        }

        private static RoZcStorageItem ParseAdvancedStorageAddedItem(RoPacketReader reader) {
            short index = reader.ReadInt16();
            int count = reader.ReadInt32();
            uint itemId = reader.ReadUInt32();
            byte type = reader.ReadByte();
            bool identified = reader.ReadByte() != 0;
            bool damaged = reader.ReadByte() != 0;
            byte refine = reader.ReadByte();
            int[] cards = ReadCardIds(reader);
            RoPacketItemRandomOption[] options = ReadRandomOptions(reader);
            return new RoZcStorageItem(index, count, itemId, type, identified, damaged, refine, cards, randomOptions: options);
        }

        private static RoZcStorageItem ParseLatestStorageAddedItem(RoPacketReader reader) {
            short index = reader.ReadInt16();
            int count = reader.ReadInt32();
            uint itemId = reader.ReadUInt32();
            byte type = reader.ReadByte();
            bool identified = reader.ReadByte() != 0;
            bool damaged = reader.ReadByte() != 0;
            int[] cards = ReadCardIds(reader);
            RoPacketItemRandomOption[] options = ReadRandomOptions(reader);
            byte refine = reader.ReadByte();
            byte grade = reader.ReadByte();
            return new RoZcStorageItem(index, count, itemId, type, identified, damaged, refine, cards, grade, options);
        }

        private static int ResolveStorageListRecordLength(ushort packetId) {
            switch (packetId) {
                case RoPacketIds.ZcStorageItemListNormal: return StorageLegacyNormalRecordLength;
                case RoPacketIds.ZcStorageItemListEquip: return StorageLegacyEquipmentRecordLength;
                case RoPacketIds.ZcStorageItemListNormal2: return StorageModernNormalRecordLength;
                case RoPacketIds.ZcStorageItemListEquip2: return StorageModernEquipmentRecordLength;
                case RoPacketIds.ZcStorageItemListEquip3: return StorageModernEquipment2RecordLength;
                case RoPacketIds.ZcStorageItemListNormal3: return StorageModernNormal2RecordLength;
                case RoPacketIds.ZcStorageItemListNormal4: return StorageLatestNormalRecordLength;
                case RoPacketIds.ZcStorageItemListEquip4: return StorageLatestEquipmentRecordLength;
                case RoPacketIds.ZcStorageItemListEquip5: return StorageLatestEquipment2RecordLength;
                default: throw new InvalidOperationException($"Opcode 0x{packetId:x4} is not a supported storage list packet.");
            }
        }

        private static int ResolveCartListRecordLength(ushort packetId) {
            switch (packetId) {
                case RoPacketIds.ZcNonstackableItemListCart: return CartLegacyNormalRecordLength;
                case RoPacketIds.ZcEquipItemListCart: return CartLegacyEquipmentRecordLength;
                case RoPacketIds.ZcNonstackableItemListCart2: return CartModernNormalRecordLength;
                case RoPacketIds.ZcEquipItemListCart2: return CartModernEquipmentRecordLength;
                case RoPacketIds.ZcCartNormalItemList4: return CartLatestNormalRecordLength;
                case RoPacketIds.ZcCartEquipItemList4: return CartLatestEquipmentRecordLength;
                case RoPacketIds.ZcCartEquipItemList5: return CartLatestEquipment2RecordLength;
                default: throw new InvalidOperationException($"Opcode 0x{packetId:x4} is not a supported cart list packet.");
            }
        }

        private static bool IsStorageEquipmentList(ushort packetId) {
            return packetId == RoPacketIds.ZcStorageItemListEquip ||
                packetId == RoPacketIds.ZcStorageItemListEquip2 ||
                packetId == RoPacketIds.ZcStorageItemListEquip3 ||
                packetId == RoPacketIds.ZcStorageItemListEquip4 ||
                packetId == RoPacketIds.ZcStorageItemListEquip5;
        }

        private static bool IsCartEquipmentList(ushort packetId) {
            return packetId == RoPacketIds.ZcEquipItemListCart ||
                packetId == RoPacketIds.ZcEquipItemListCart2 ||
                packetId == RoPacketIds.ZcCartEquipItemList4 ||
                packetId == RoPacketIds.ZcCartEquipItemList5;
        }

        private static void RequirePacketRecordAlignment(int packetLength, int headerLength, int recordLength, ushort packetId) {
            int payloadLength = packetLength - headerLength;
            if (payloadLength < 0 || payloadLength % recordLength != 0) {
                throw new ArgumentException(
                    $"Packet 0x{packetId:x4} payload length {payloadLength} is not divisible by record length {recordLength}.");
            }
        }

        private static int[] ReadRemainingCardIds16(RoPacketReader reader) {
            var cards = new int[CardSlotCount];
            for (int i = 0; i < cards.Length && reader.Remaining >= 2; i++) {
                cards[i] = reader.ReadUInt16();
            }

            return cards;
        }

        private static RoZcInventoryItem ParseNormalInventoryItem(RoPacketReader reader) {
            short bagIndex = reader.ReadInt16();
            uint itemId = reader.ReadUInt32();
            byte itemType = reader.ReadByte();
            short count = reader.ReadInt16();
            uint equipLocationRaw = reader.ReadUInt32();
            int[] cards = ReadCardIds(reader);
            reader.ReadInt32();
            byte flag = reader.ReadByte();

            return new RoZcInventoryItem(
                bagIndex,
                itemId,
                itemType,
                count,
                equipLocationRaw,
                wearStateRaw: 0,
                refineLevel: 0,
                cards,
                viewIdHint: 0,
                flag,
                isEquipment: false);
        }

        private static RoZcInventoryItem ParseNormalInventoryItem4(RoPacketReader reader) {
            short bagIndex = reader.ReadInt16();
            uint itemId = reader.ReadUInt16();
            byte itemType = reader.ReadByte();
            short count = reader.ReadInt16();
            uint wearStateRaw = reader.ReadUInt32();
            int[] cards = ReadCardIds16(reader);
            reader.ReadInt32();
            byte flag = reader.ReadByte();

            return new RoZcInventoryItem(
                bagIndex,
                itemId,
                itemType,
                count,
                equipLocationRaw: 0,
                wearStateRaw,
                refineLevel: 0,
                cards,
                viewIdHint: 0,
                flag,
                isEquipment: false);
        }

        private static RoZcInventoryItem ParseEquipmentInventoryItem(RoPacketReader reader) {
            short bagIndex = reader.ReadInt16();
            uint itemId = reader.ReadUInt32();
            byte itemType = reader.ReadByte();
            uint equipLocationRaw = reader.ReadUInt32();
            uint wearStateRaw = reader.ReadUInt32();
            byte refineLevel = reader.ReadByte();
            int[] cards = ReadCardIds(reader);
            reader.ReadInt32();
            reader.ReadUInt16();
            ushort viewIdHint = reader.ReadUInt16();
            reader.ReadByte();
            RoPacketItemRandomOption[] options = ReadRandomOptions(reader);
            byte flag = reader.ReadByte();

            return new RoZcInventoryItem(
                bagIndex,
                itemId,
                itemType,
                count: 1,
                equipLocationRaw,
                wearStateRaw,
                refineLevel,
                cards,
                viewIdHint,
                flag,
                isEquipment: true,
                randomOptions: options);
        }

        private static RoZcInventoryItem ParseEquipmentInventoryItem4(RoPacketReader reader, bool hasRandomOptions) {
            short bagIndex = reader.ReadInt16();
            uint itemId = reader.ReadUInt16();
            byte itemType = reader.ReadByte();
            uint equipLocationRaw = reader.ReadUInt32();
            uint wearStateRaw = reader.ReadUInt32();
            byte refineLevel = reader.ReadByte();
            int[] cards = ReadCardIds16(reader);
            reader.ReadInt32();
            reader.ReadUInt16();
            ushort viewIdHint = reader.ReadUInt16();
            RoPacketItemRandomOption[] options = null;
            if (hasRandomOptions) {
                reader.ReadByte();
                options = ReadRandomOptions(reader);
            }

            byte flag = reader.ReadByte();

            return new RoZcInventoryItem(
                bagIndex,
                itemId,
                itemType,
                count: 1,
                equipLocationRaw,
                wearStateRaw,
                refineLevel,
                cards,
                viewIdHint,
                flag,
                isEquipment: true,
                randomOptions: options);
        }

        private static RoZcInventoryItem ParseEquipmentInventoryItemWithGrade(RoPacketReader reader) {
            short bagIndex = reader.ReadInt16();
            uint itemId = reader.ReadUInt32();
            byte itemType = reader.ReadByte();
            uint equipLocationRaw = reader.ReadUInt32();
            uint wearStateRaw = reader.ReadUInt32();
            int[] cards = ReadCardIds(reader);
            reader.ReadInt32();
            reader.ReadUInt16();
            ushort viewIdHint = reader.ReadUInt16();
            reader.ReadByte();
            RoPacketItemRandomOption[] options = ReadRandomOptions(reader);
            byte refineLevel = reader.ReadByte();
            byte enchantGrade = reader.ReadByte();
            byte flag = reader.ReadByte();

            return new RoZcInventoryItem(
                bagIndex,
                itemId,
                itemType,
                count: 1,
                equipLocationRaw,
                wearStateRaw,
                refineLevel,
                cards,
                viewIdHint,
                flag,
                isEquipment: true,
                enchantGrade: enchantGrade,
                randomOptions: options);
        }

        private static int[] ReadCardIds(RoPacketReader reader) {
            var cards = new int[CardSlotCount];
            for (int i = 0; i < cards.Length; i++) {
                cards[i] = unchecked((int) reader.ReadUInt32());
            }

            return cards;
        }

        private static int[] ReadCardIds16(RoPacketReader reader) {
            var cards = new int[CardSlotCount];
            for (int i = 0; i < cards.Length; i++) {
                cards[i] = reader.ReadUInt16();
            }

            return cards;
        }

        private static RoPacketItemRandomOption[] ReadRandomOptions(RoPacketReader reader) {
            var options = new RoPacketItemRandomOption[5];
            for (int i = 0; i < options.Length && reader.Remaining >= 5; i++) {
                short index = reader.ReadInt16();
                short value = reader.ReadInt16();
                byte param = reader.ReadByte();
                options[i] = new RoPacketItemRandomOption(index, value, param);
            }

            return options;
        }

        private static RoZcPlayerVendingItemList ParsePlayerShopBuyerItems(
            RoPacketReader reader,
            uint ownerAid,
            uint uniqueId,
            bool hasUniqueId,
            bool hasGrade) {
            var items = new List<RoPlayerVendingItem>();
            while (!reader.EndOfPacket) {
                uint price = reader.ReadUInt32();
                ushort amount = reader.ReadUInt16();
                short index = reader.ReadInt16();
                byte itemType = reader.ReadByte();
                uint itemId = reader.ReadUInt32();
                bool isIdentified = reader.ReadByte() != 0;
                bool isDamaged = reader.ReadByte() != 0;
                byte refineLevel;
                int[] cards;
                RoPacketItemRandomOption[] options;
                uint location;
                ushort viewSprite;
                byte grade;
                if (hasGrade) {
                    cards = ReadCardIds(reader);
                    options = ReadRandomOptions(reader);
                    location = reader.ReadUInt32();
                    viewSprite = reader.ReadUInt16();
                    refineLevel = reader.ReadByte();
                    grade = reader.ReadByte();
                } else {
                    refineLevel = reader.ReadByte();
                    cards = ReadCardIds(reader);
                    options = ReadRandomOptions(reader);
                    location = reader.ReadUInt32();
                    viewSprite = reader.ReadUInt16();
                    grade = 0;
                }

                items.Add(new RoPlayerVendingItem(
                    price,
                    amount,
                    index,
                    itemType,
                    itemId,
                    isIdentified,
                    isDamaged,
                    refineLevel,
                    cards,
                    options,
                    location,
                    viewSprite,
                    grade));
            }

            return new RoZcPlayerVendingItemList(ownerAid, uniqueId, hasUniqueId, items.ToArray());
        }

        private static RoZcPlayerVendingItemList ParsePlayerShopOwnItems(RoPacketReader reader, uint ownerAid, bool hasGrade) {
            var items = new List<RoPlayerVendingItem>();
            while (!reader.EndOfPacket) {
                uint price = reader.ReadUInt32();
                short index = reader.ReadInt16();
                ushort amount = reader.ReadUInt16();
                byte itemType = reader.ReadByte();
                uint itemId = reader.ReadUInt32();
                bool isIdentified = reader.ReadByte() != 0;
                bool isDamaged = reader.ReadByte() != 0;
                byte refineLevel;
                int[] cards;
                RoPacketItemRandomOption[] options;
                byte grade;
                if (hasGrade) {
                    cards = ReadCardIds(reader);
                    options = ReadRandomOptions(reader);
                    refineLevel = reader.ReadByte();
                    grade = reader.ReadByte();
                } else {
                    refineLevel = reader.ReadByte();
                    cards = ReadCardIds(reader);
                    options = ReadRandomOptions(reader);
                    grade = 0;
                }

                items.Add(new RoPlayerVendingItem(
                    price,
                    amount,
                    index,
                    itemType,
                    itemId,
                    isIdentified,
                    isDamaged,
                    refineLevel,
                    cards,
                    options,
                    location: 0,
                    viewSprite: 0,
                    grade: grade));
            }

            return new RoZcPlayerVendingItemList(ownerAid, 0, hasUniqueId: false, items.ToArray());
        }

        private static RoZcNpcCutin ParseZcShowImage(
            byte[] frame,
            ushort expectedPacketId,
            int expectedLength,
            int imageNameLength,
            Encoding encoding) {
            var reader = CreateFixedFrameReader(frame, expectedPacketId, expectedLength);
            string imageName = reader.ReadFixedString(imageNameLength, encoding);
            byte type = reader.ReadByte();
            return new RoZcNpcCutin(imageName, type);
        }

        private static string FormatIpv4(byte[] addressBytes) {
            if (addressBytes == null || addressBytes.Length != 4) {
                throw new ArgumentException("IPv4 address must be exactly 4 bytes.", nameof(addressBytes));
            }

            return $"{addressBytes[0]}.{addressBytes[1]}.{addressBytes[2]}.{addressBytes[3]}";
        }

        private static RoZcParameterChange ParseZcParameterChange(
            byte[] frame,
            ushort expectedPacketId,
            int expectedLength,
            bool readsLongLongValue) {
            var reader = CreateFixedFrameReader(frame, expectedPacketId, expectedLength);
            ushort property = reader.ReadUInt16();
            long value = readsLongLongValue ? reader.ReadInt64() : reader.ReadInt32();
            return new RoZcParameterChange(property, value);
        }

        private static void RequireRecordAlignment(RoPacketReader reader, int recordLength, ushort packetId) {
            if (recordLength <= 0) {
                throw new ArgumentOutOfRangeException(nameof(recordLength));
            }

            if (reader.Remaining % recordLength != 0) {
                throw new ArgumentException(
                    $"Packet 0x{packetId:x4} has {reader.Remaining} payload byte(s), which is not divisible by {recordLength}.",
                    nameof(reader));
            }
        }

        private static int ResolveInventoryRecordLength(
            int remaining,
            int currentRecordLength,
            int legacyRecordLength,
            ushort packetId) {
            if (remaining == 0 || remaining % currentRecordLength == 0) {
                return currentRecordLength;
            }

            if (remaining % legacyRecordLength == 0) {
                return legacyRecordLength;
            }

            throw new ArgumentException(
                $"Packet 0x{packetId:x4} has {remaining} inventory payload byte(s), which matches neither {currentRecordLength} nor {legacyRecordLength} byte records.",
                nameof(remaining));
        }

        private static RoPacketReader CreateFrameReader(byte[] frame) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length < 2) {
                throw new ArgumentException("Packet frame must include a two-byte opcode.", nameof(frame));
            }

            return new RoPacketReader(frame);
        }

        private static void EnsureFixedLength(byte[] frame, ushort expectedPacketId, int expectedLength) {
            if (frame.Length != expectedLength) {
                throw new ArgumentException(
                    $"Packet 0x{expectedPacketId:x4} must be {expectedLength} bytes, got {frame.Length}.",
                    nameof(frame));
            }
        }

        private static RoPacketReader CreateFixedFrameReader(byte[] frame, ushort expectedPacketId, int expectedLength) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length != expectedLength) {
                throw new ArgumentException($"Packet 0x{expectedPacketId:x4} must be {expectedLength} bytes, got {frame.Length}.", nameof(frame));
            }

            var reader = new RoPacketReader(frame);
            ushort packetId = reader.ReadUInt16();
            if (packetId != expectedPacketId) {
                throw new ArgumentException($"Expected packet 0x{expectedPacketId:x4}, got 0x{packetId:x4}.", nameof(frame));
            }

            return reader;
        }

        private static RoZcTradeItem ParseTradeItemLegacy(RoPacketReader reader) {
            int count = reader.ReadInt32();
            uint itemId = reader.ReadUInt16();
            bool isIdentified = reader.ReadByte() != 0;
            bool isDamaged = reader.ReadByte() != 0;
            byte refineLevel = reader.ReadByte();
            int[] cards = ReadShortCardIds(reader);
            return new RoZcTradeItem(
                itemId,
                itemTypeRaw: 0,
                count,
                isIdentified,
                isDamaged,
                refineLevel,
                cards);
        }

        private static RoZcTradeItem ParseTradeItemTyped(
            RoPacketReader reader,
            bool hasOptions,
            bool wideIds,
            bool hasLocationAndView,
            bool hasGrade,
            bool refineBeforeCards = false) {
            uint itemId = wideIds ? reader.ReadUInt32() : reader.ReadUInt16();
            byte type = reader.ReadByte();
            int count = reader.ReadInt32();
            bool isIdentified = reader.ReadByte() != 0;
            bool isDamaged = reader.ReadByte() != 0;
            byte refineLevel = refineBeforeCards ? reader.ReadByte() : (byte) 0;
            int[] cards = wideIds ? ReadWideCardIds(reader) : ReadShortCardIdsWithLeadingRefine(reader, out refineLevel);
            if (hasOptions) {
                reader.Skip(25);
            }

            if (hasLocationAndView) {
                reader.Skip(6);
            }

            if (hasGrade) {
                refineLevel = reader.ReadByte();
                reader.Skip(1);
            }

            return new RoZcTradeItem(itemId, type, count, isIdentified, isDamaged, refineLevel, cards);
        }

        private static int[] ReadShortCardIdsWithLeadingRefine(RoPacketReader reader, out byte refineLevel) {
            refineLevel = reader.ReadByte();
            return ReadShortCardIds(reader);
        }

        private static int[] ReadShortCardIds(RoPacketReader reader) {
            var cards = new int[CardSlotCount];
            for (int i = 0; i < cards.Length; i++) {
                cards[i] = reader.ReadUInt16();
            }

            return cards;
        }

        private static int[] ReadWideCardIds(RoPacketReader reader) {
            var cards = new int[CardSlotCount];
            for (int i = 0; i < cards.Length; i++) {
                cards[i] = unchecked((int) reader.ReadUInt32());
            }

            return cards;
        }

        private static RoZcItemEntry ParseItemFallEntryPayload(
            RoPacketReader reader,
            bool itemIdIsUInt32,
            bool hasItemType,
            bool hasDropEffect) {
            uint itemAid = reader.ReadUInt32();
            uint itemId = itemIdIsUInt32 ? reader.ReadUInt32() : reader.ReadUInt16();
            int itemTypeRaw = hasItemType ? reader.ReadUInt16() : -1;
            bool isIdentified = reader.ReadByte() != 0;
            ushort x = reader.ReadUInt16();
            ushort y = reader.ReadUInt16();
            byte subX = reader.ReadByte();
            byte subY = reader.ReadByte();
            short count = reader.ReadInt16();
            bool showDropEffect = false;
            short dropEffectMode = 0;
            if (hasDropEffect) {
                showDropEffect = reader.ReadByte() != 0;
                dropEffectMode = reader.ReadInt16();
            }

            return new RoZcItemEntry(
                itemAid,
                itemId,
                itemTypeRaw,
                isIdentified,
                x,
                y,
                subX,
                subY,
                count,
                isFalling: true,
                showDropEffect,
                dropEffectMode);
        }

        private static RoPacketReader CreateZcSpriteChange2Reader(byte[] frame) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length != ZcSpriteChange2Length && frame.Length != ZcSpriteChange2LegacyLength) {
                throw new ArgumentException(
                    $"Packet 0x{RoPacketIds.ZcSpriteChange2:x4} must be {ZcSpriteChange2Length} bytes for the local profile or {ZcSpriteChange2LegacyLength} bytes for legacy captures, got {frame.Length}.",
                    nameof(frame));
            }

            var reader = new RoPacketReader(frame);
            ushort packetId = reader.ReadUInt16();
            if (packetId != RoPacketIds.ZcSpriteChange2) {
                throw new ArgumentException($"Expected packet 0x{RoPacketIds.ZcSpriteChange2:x4}, got 0x{packetId:x4}.", nameof(frame));
            }

            return reader;
        }

        private static RoPacketReader CreateVariableFrameReader(byte[] frame, ushort expectedPacketId) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length < 4) {
                throw new ArgumentException($"Packet 0x{expectedPacketId:x4} must include a 4-byte variable-length header.", nameof(frame));
            }

            var reader = new RoPacketReader(frame);
            ushort packetId = reader.ReadUInt16();
            if (packetId != expectedPacketId) {
                throw new ArgumentException($"Expected packet 0x{expectedPacketId:x4}, got 0x{packetId:x4}.", nameof(frame));
            }

            ushort declaredLength = reader.ReadUInt16();
            if (declaredLength != frame.Length) {
                throw new ArgumentException($"Packet 0x{expectedPacketId:x4} declared {declaredLength} bytes, got {frame.Length}.", nameof(frame));
            }

            return reader;
        }

        private static RoZcNotifyActorEntry ParseActorEntry(
            byte[] frame,
            ushort expectedPacketId,
            RoMapActorEntryKind kind,
            Encoding encoding) {
            var reader = CreateVariableFrameReader(frame, expectedPacketId);
            sbyte objectTypeRaw = unchecked((sbyte) reader.ReadByte());
            RoMapActorObjectType objectType = ResolveObjectType(objectTypeRaw);
            uint aid = reader.ReadUInt32();
            uint gid = reader.ReadUInt32();
            short speed = reader.ReadInt16();
            short bodyState = reader.ReadInt16();
            short healthState = reader.ReadInt16();
            int effectState = reader.ReadInt32();
            short job = reader.ReadInt16();
            ushort head = reader.ReadUInt16();
            uint weapon = reader.ReadUInt32();
            uint shield = reader.ReadUInt32();
            ushort accessory = reader.ReadUInt16();
            uint moveStartTime = 0;
            if (kind == RoMapActorEntryKind.Moving) {
                moveStartTime = reader.ReadUInt32();
            }

            ushort accessory2 = reader.ReadUInt16();
            ushort accessory3 = reader.ReadUInt16();
            short headPalette = reader.ReadInt16();
            short bodyPalette = reader.ReadInt16();
            short headDirection = reader.ReadInt16();
            ushort robe = reader.ReadUInt16();
            uint guildId = reader.ReadUInt32();
            short guildEmblemVersion = reader.ReadInt16();
            short honor = reader.ReadInt16();
            int virtue = reader.ReadInt32();
            bool isPkModeOn = reader.ReadByte() != 0;
            byte sex = reader.ReadByte();

            bool hasMovePath = kind == RoMapActorEntryKind.Moving;
            RoPacketPosition position;
            RoPacketPositionPair movePath = default;
            if (hasMovePath) {
                movePath = reader.ReadPositionPair();
                reader.Skip(1);
                position = new RoPacketPosition(movePath.StartX, movePath.StartY, 0);
            } else {
                position = reader.ReadPosition();
            }

            byte xSize = reader.ReadByte();
            byte ySize = reader.ReadByte();
            byte stateRaw = 0;
            if (kind == RoMapActorEntryKind.Standing) {
                stateRaw = reader.ReadByte();
            }

            var state = (RoMapActorSpawnState) stateRaw;
            short clientLevel = reader.ReadInt16();
            short font = reader.ReadInt16();
            int maxHp = reader.ReadInt32();
            int hp = reader.ReadInt32();
            bool isBoss = reader.ReadByte() != 0;
            ushort body = reader.ReadUInt16();
            string name = reader.ReadFixedString(ActorNameLength, encoding);
            if (!reader.EndOfPacket) {
                throw new ArgumentException($"Packet 0x{expectedPacketId:x4} has {reader.Remaining} unexpected trailing byte(s).", nameof(frame));
            }

            return new RoZcNotifyActorEntry(
                kind,
                objectTypeRaw,
                objectType,
                aid,
                gid,
                speed,
                bodyState,
                healthState,
                effectState,
                job,
                head,
                weapon,
                shield,
                accessory,
                accessory2,
                accessory3,
                headPalette,
                bodyPalette,
                headDirection,
                robe,
                guildId,
                guildEmblemVersion,
                honor,
                virtue,
                isPkModeOn,
                sex,
                hasMovePath,
                position,
                movePath,
                moveStartTime,
                xSize,
                ySize,
                stateRaw,
                state,
                clientLevel,
                font,
                maxHp,
                hp,
                isBoss,
                body,
                name);
        }

        private static RoMapActorObjectType ResolveObjectType(sbyte raw) {
            switch (raw) {
                case -3:
                    return RoMapActorObjectType.Effect;
                case -2:
                    return RoMapActorObjectType.Unknown;
                case -1:
                    return RoMapActorObjectType.Warp;
                case 0:
                    return RoMapActorObjectType.Player;
                case 1:
                    return RoMapActorObjectType.Disguised;
                case 5:
                    return RoMapActorObjectType.Monster;
                case 6:
                    return RoMapActorObjectType.Npc;
                case 7:
                    return RoMapActorObjectType.Pet;
                case 8:
                    return RoMapActorObjectType.Homunculus;
                case 9:
                    return RoMapActorObjectType.Mercenary;
                case 10:
                    return RoMapActorObjectType.Elemental;
                case 11:
                    return RoMapActorObjectType.Item;
                case 12:
                    return RoMapActorObjectType.Npc2;
                default:
                    return RoMapActorObjectType.Unknown;
            }
        }
    }
}
