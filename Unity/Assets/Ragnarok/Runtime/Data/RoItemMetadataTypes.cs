using System;

namespace Ragnarok.Data {
    public enum RoItemType {
        Unknown = 0,
        Healing,
        Usable,
        Etc,
        Weapon,
        Armor,
        Card,
        PetEgg,
        PetArmor,
        Ammo,
        DelayConsume,
        Cash,
        ShadowGear
    }

    [Flags]
    public enum RoItemEquipLocation {
        None = 0,
        HeadBottom = 1 << 0,
        Weapon = 1 << 1,
        Garment = 1 << 2,
        Accessory1 = 1 << 3,
        Armor = 1 << 4,
        Shield = 1 << 5,
        Shoes = 1 << 6,
        Accessory2 = 1 << 7,
        HeadTop = 1 << 8,
        HeadMid = 1 << 9,
        CostumeHeadTop = 1 << 10,
        CostumeHeadMid = 1 << 11,
        CostumeHeadBottom = 1 << 12,
        CostumeRobe = 1 << 13,
        CostumeFloor = 1 << 14,
        Ammo = 1 << 15,
        ShadowArmor = 1 << 16,
        ShadowWeapon = 1 << 17,
        ShadowShield = 1 << 18,
        ShadowShoes = 1 << 19,
        ShadowAccessoryRight = 1 << 20,
        ShadowAccessoryLeft = 1 << 21,

        RightHand = Weapon,
        LeftHand = Shield,
        BothHands = Weapon | Shield,
        Headgear = HeadTop | HeadMid | HeadBottom,
        Accessory = Accessory1 | Accessory2,
        CostumeHeadgear = CostumeHeadTop | CostumeHeadMid | CostumeHeadBottom,
        ShadowAccessory = ShadowAccessoryRight | ShadowAccessoryLeft
    }

    [Flags]
    public enum RoItemEquipJob : ulong {
        None = 0,
        Novice = 1UL << 0,
        Swordman = 1UL << 1,
        Mage = 1UL << 2,
        Archer = 1UL << 3,
        Acolyte = 1UL << 4,
        Merchant = 1UL << 5,
        Thief = 1UL << 6,
        Knight = 1UL << 7,
        Priest = 1UL << 8,
        Wizard = 1UL << 9,
        Blacksmith = 1UL << 10,
        Hunter = 1UL << 11,
        Assassin = 1UL << 12,
        Crusader = 1UL << 13,
        Monk = 1UL << 14,
        Sage = 1UL << 15,
        Rogue = 1UL << 16,
        Alchemist = 1UL << 17,
        BardDancer = 1UL << 18,
        Taekwon = 1UL << 19,
        StarGladiator = 1UL << 20,
        SoulLinker = 1UL << 21,
        Gunslinger = 1UL << 22,
        Ninja = 1UL << 23,
        KagerouOboro = 1UL << 24,
        Rebellion = 1UL << 25,
        SuperNovice = 1UL << 26,
        Summoner = 1UL << 27,
        SpiritHandler = 1UL << 28
    }

    [Flags]
    public enum RoItemEquipClass {
        None = 0,
        Normal = 1 << 0,
        Upper = 1 << 1,
        Baby = 1 << 2,
        Third = 1 << 3,
        ThirdUpper = 1 << 4,
        ThirdBaby = 1 << 5,
        Fourth = 1 << 6,

        AllThird = Third | ThirdUpper | ThirdBaby
    }

    public enum RoItemGenderRestriction {
        Any = 0,
        Male,
        Female
    }

    public readonly struct RoItemMoveInfo {
        public RoItemMoveInfo(
            bool drop,
            bool exchange,
            bool storage,
            bool cart,
            bool npcSale,
            bool mail,
            bool auction,
            bool guildStorage
        ) {
            Drop = drop;
            Exchange = exchange;
            Storage = storage;
            Cart = cart;
            NPCSale = npcSale;
            Mail = mail;
            Auction = auction;
            GuildStorage = guildStorage;
        }

        public bool Drop { get; }
        public bool Exchange { get; }
        public bool Storage { get; }
        public bool Cart { get; }
        public bool NPCSale { get; }
        public bool Mail { get; }
        public bool Auction { get; }
        public bool GuildStorage { get; }
        public bool HasAnyRestriction =>
            Drop ||
            Exchange ||
            Storage ||
            Cart ||
            NPCSale ||
            Mail ||
            Auction ||
            GuildStorage;

        public static RoItemMoveInfo Empty { get; } = new RoItemMoveInfo(false, false, false, false, false, false, false, false);
    }

    public readonly struct RoItemEquipContext {
        public RoItemEquipContext(
            RoItemEquipJob job,
            RoItemEquipClass classGroup,
            int baseLevel,
            RoItemGenderRestriction gender
        ) {
            Job = job;
            ClassGroup = classGroup;
            BaseLevel = Math.Max(0, baseLevel);
            Gender = gender;
        }

        public RoItemEquipJob Job { get; }
        public RoItemEquipClass ClassGroup { get; }
        public int BaseLevel { get; }
        public RoItemGenderRestriction Gender { get; }

        public static RoItemEquipContext ForJob(
            RoItemEquipJob job,
            int baseLevel = 0,
            bool isMale = true,
            RoItemEquipClass classGroup = RoItemEquipClass.Normal
        ) {
            return new RoItemEquipContext(
                job,
                classGroup,
                baseLevel,
                isMale ? RoItemGenderRestriction.Male : RoItemGenderRestriction.Female
            );
        }
    }
}
