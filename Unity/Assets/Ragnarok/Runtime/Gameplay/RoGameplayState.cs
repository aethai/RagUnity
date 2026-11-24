using System;
using System.Collections.Generic;
using Ragnarok.Data;
using Ragnarok.Network;

namespace Ragnarok.Gameplay {
    public readonly struct RoGameplaySkillInfo {
        public RoGameplaySkillInfo(RoZcSkillInfo source) {
            SkillId = source.SkillId;
            Type = source.Type;
            Level = source.Level;
            SpCost = source.SpCost;
            AttackRange = source.AttackRange;
            Name = source.Name ?? string.Empty;
            Upgradable = source.Upgradable;
            Level2 = source.Level2;
        }

        public ushort SkillId { get; }
        public int Type { get; }
        public ushort Level { get; }
        public ushort SpCost { get; }
        public ushort AttackRange { get; }
        public string Name { get; }
        public byte Upgradable { get; }
        public ushort Level2 { get; }
        public bool CanUpgrade => Upgradable != 0;
    }

    public sealed class RoPlayerStatusSnapshot {
        public static readonly RoPlayerStatusSnapshot Empty = new RoPlayerStatusSnapshot();

        internal RoPlayerStatusSnapshot() {
            WeightPercentage = -1;
        }

        internal RoPlayerStatusSnapshot(RoMutablePlayerStatus source) {
            if (source == null) {
                return;
            }

            BaseLevel = source.BaseLevel;
            JobLevel = source.JobLevel;
            JobId = source.JobId;
            HasKnownJobId = source.HasKnownJobId;
            BaseExperience = source.BaseExperience;
            NextBaseExperience = source.NextBaseExperience;
            JobExperience = source.JobExperience;
            NextJobExperience = source.NextJobExperience;
            Hp = source.Hp;
            MaxHp = source.MaxHp;
            Sp = source.Sp;
            MaxSp = source.MaxSp;
            StatusPoints = source.StatusPoints;
            HasKnownStatusPoints = source.HasKnownStatusPoints;
            SkillPoints = source.SkillPoints;
            Zeny = source.Zeny;
            Weight = source.Weight;
            MaxWeight = source.MaxWeight;
            WeightPercentage = source.WeightPercentage;
            Str = source.Str;
            StrBonus = source.StrBonus;
            NeedStr = source.NeedStr;
            Agi = source.Agi;
            AgiBonus = source.AgiBonus;
            NeedAgi = source.NeedAgi;
            Vit = source.Vit;
            VitBonus = source.VitBonus;
            NeedVit = source.NeedVit;
            Int = source.Int;
            IntBonus = source.IntBonus;
            NeedInt = source.NeedInt;
            Dex = source.Dex;
            DexBonus = source.DexBonus;
            NeedDex = source.NeedDex;
            Luk = source.Luk;
            LukBonus = source.LukBonus;
            NeedLuk = source.NeedLuk;
            Attack = source.Attack;
            RefiningPower = source.RefiningPower;
            MagicAttackMax = source.MagicAttackMax;
            MagicAttackMin = source.MagicAttackMin;
            Defense = source.Defense;
            PlusDefense = source.PlusDefense;
            MagicDefense = source.MagicDefense;
            PlusMagicDefense = source.PlusMagicDefense;
            Hit = source.Hit;
            Flee = source.Flee;
            PlusFlee = source.PlusFlee;
            Critical = source.Critical;
            MovementSpeed = source.MovementSpeed;
            AttackSpeed = source.AttackSpeed;
            PlusAttackSpeed = source.PlusAttackSpeed;
            AttackRange = source.AttackRange;
        }

        public int BaseLevel { get; }
        public int JobLevel { get; }
        public int JobId { get; }
        public bool HasKnownJobId { get; }
        public long BaseExperience { get; }
        public long NextBaseExperience { get; }
        public long JobExperience { get; }
        public long NextJobExperience { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Sp { get; }
        public int MaxSp { get; }
        public int StatusPoints { get; }
        public bool HasKnownStatusPoints { get; }
        public int SkillPoints { get; }
        public long Zeny { get; }
        public long Weight { get; }
        public long MaxWeight { get; }
        public int WeightPercentage { get; }
        public int Str { get; }
        public int StrBonus { get; }
        public int NeedStr { get; }
        public int Agi { get; }
        public int AgiBonus { get; }
        public int NeedAgi { get; }
        public int Vit { get; }
        public int VitBonus { get; }
        public int NeedVit { get; }
        public int Int { get; }
        public int IntBonus { get; }
        public int NeedInt { get; }
        public int Dex { get; }
        public int DexBonus { get; }
        public int NeedDex { get; }
        public int Luk { get; }
        public int LukBonus { get; }
        public int NeedLuk { get; }
        public int Attack { get; }
        public int RefiningPower { get; }
        public int MagicAttackMax { get; }
        public int MagicAttackMin { get; }
        public int Defense { get; }
        public int PlusDefense { get; }
        public int MagicDefense { get; }
        public int PlusMagicDefense { get; }
        public int Hit { get; }
        public int Flee { get; }
        public int PlusFlee { get; }
        public int Critical { get; }
        public int MovementSpeed { get; }
        public int AttackSpeed { get; }
        public int PlusAttackSpeed { get; }
        public int AttackRange { get; }

        public bool TryGetBaseExperiencePercent(out float percent) {
            return TryGetPercent(BaseExperience, NextBaseExperience, out percent);
        }

        public bool TryGetJobExperiencePercent(out float percent) {
            return TryGetPercent(JobExperience, NextJobExperience, out percent);
        }

        public bool TryGetWeightPercent(out float percent) {
            if (WeightPercentage >= 0) {
                percent = WeightPercentage;
                return true;
            }

            return TryGetPercent(Weight, MaxWeight, out percent);
        }

        private static bool TryGetPercent(long value, long max, out float percent) {
            if (max <= 0) {
                percent = 0f;
                return false;
            }

            percent = (float) value * 100f / max;
            if (percent < 0f) {
                percent = 0f;
            } else if (percent > 100f) {
                percent = 100f;
            }

            return true;
        }
    }

    public sealed class RoNpcDialogSnapshot {
        public static readonly RoNpcDialogSnapshot Empty = new RoNpcDialogSnapshot(
            0,
            Array.Empty<string>(),
            false,
            false,
            false,
            false,
            Array.Empty<string>(),
            false,
            false,
            false,
            string.Empty,
            0);

        internal RoNpcDialogSnapshot(
            uint npcId,
            string[] lines,
            bool canContinue,
            bool canClose,
            bool hasMenu,
            bool wantsInput,
            string[] menuChoices,
            bool wantsNumberInput,
            bool wantsTextInput,
            bool hasCutin,
            string cutinImageName,
            byte cutinTypeRaw,
            int waitCount = 0) {
            NpcId = npcId;
            Lines = lines ?? Array.Empty<string>();
            CanContinue = canContinue;
            CanClose = canClose;
            HasMenu = hasMenu;
            MenuChoices = menuChoices ?? Array.Empty<string>();
            WantsNumberInput = wantsNumberInput;
            WantsTextInput = wantsTextInput;
            WantsInput = wantsInput || wantsNumberInput || wantsTextInput;
            HasCutin = hasCutin;
            CutinImageName = cutinImageName ?? string.Empty;
            CutinTypeRaw = cutinTypeRaw;
            WaitCount = waitCount;
        }

        public uint NpcId { get; }
        public string[] Lines { get; }
        public bool CanContinue { get; }
        public bool CanClose { get; }
        public bool HasMenu { get; }
        public bool WantsInput { get; }
        public string[] MenuChoices { get; }
        public bool WantsNumberInput { get; }
        public bool WantsTextInput { get; }
        public bool HasCutin { get; }
        public string CutinImageName { get; }
        public byte CutinTypeRaw { get; }

        /// <summary>ZC_WAIT_DIALOG packets seen so far: a new value is a new Next, even when its page and the wait arrived in one drain.</summary>
        public int WaitCount { get; }
        public bool IsOpen => NpcId != 0 || Lines.Length > 0 || CanContinue || CanClose || HasMenu || WantsInput || MenuChoices.Length > 0 || HasCutin;
    }

    public sealed class RoTeleportMenuSnapshot {
        public static readonly RoTeleportMenuSnapshot Empty = new RoTeleportMenuSnapshot(0, Array.Empty<string>(), 0);

        public RoTeleportMenuSnapshot(ushort skillId, string[] mapNames, int sequence) {
            SkillId = skillId;
            MapNames = CopyNonEmpty(mapNames);
            Sequence = sequence;
        }

        public ushort SkillId { get; }
        public string[] MapNames { get; }
        public int Sequence { get; }
        public bool IsActive => SkillId != 0 && MapNames.Length > 0;

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

    public sealed class RoNpcShopSnapshot {
        public static readonly RoNpcShopSnapshot Empty = new RoNpcShopSnapshot(
            0,
            RoNpcShopMode.None,
            false,
            Array.Empty<RoNpcShopBuyItem>(),
            Array.Empty<RoNpcShopSellItem>(),
            Array.Empty<RoNpcMarketItem>(),
            Array.Empty<RoNpcBarterItem>(),
            Array.Empty<RoNpcBarterItem>(),
            false,
            0,
            Array.Empty<RoNpcMarketResultItem>(),
            false,
            0,
            false,
            0);

        internal RoNpcShopSnapshot(
            uint npcId,
            RoNpcShopMode mode,
            bool isDealTypePending,
            RoNpcShopBuyItem[] buyItems,
            RoNpcShopSellItem[] sellItems,
            RoNpcMarketItem[] marketItems,
            RoNpcBarterItem[] barterItems,
            RoNpcBarterItem[] expandedBarterItems,
            bool hasMarketResult,
            ushort marketResult,
            RoNpcMarketResultItem[] marketResultItems,
            bool hasBuyResult,
            byte buyResult,
            bool hasSellResult,
            byte sellResult,
            int barterSequence = 0,
            int expandedBarterSequence = 0,
            int marketSequence = 0,
            int marketResultSequence = 0) {
            NpcId = npcId;
            Mode = mode;
            IsDealTypePending = isDealTypePending;
            BuyItems = buyItems ?? Array.Empty<RoNpcShopBuyItem>();
            SellItems = sellItems ?? Array.Empty<RoNpcShopSellItem>();
            MarketItems = marketItems ?? Array.Empty<RoNpcMarketItem>();
            BarterItems = barterItems ?? Array.Empty<RoNpcBarterItem>();
            ExpandedBarterItems = expandedBarterItems ?? Array.Empty<RoNpcBarterItem>();
            HasMarketResult = hasMarketResult;
            MarketResult = marketResult;
            MarketResultItems = marketResultItems ?? Array.Empty<RoNpcMarketResultItem>();
            HasBuyResult = hasBuyResult;
            BuyResult = buyResult;
            HasSellResult = hasSellResult;
            SellResult = sellResult;
            BarterSequence = barterSequence;
            ExpandedBarterSequence = expandedBarterSequence;
            MarketSequence = marketSequence;
            MarketResultSequence = marketResultSequence;
        }

        public uint NpcId { get; }
        public RoNpcShopMode Mode { get; }
        public bool IsDealTypePending { get; }
        public RoNpcShopBuyItem[] BuyItems { get; }
        public RoNpcShopSellItem[] SellItems { get; }
        public RoNpcMarketItem[] MarketItems { get; }
        public RoNpcBarterItem[] BarterItems { get; }
        public RoNpcBarterItem[] ExpandedBarterItems { get; }
        public bool HasMarketResult { get; }
        public ushort MarketResult { get; }
        public RoNpcMarketResultItem[] MarketResultItems { get; }
        public bool HasBuyResult { get; }
        public byte BuyResult { get; }
        public bool HasSellResult { get; }
        public byte SellResult { get; }

        /// <summary>
        /// Counts of the barter / expanded barter item lists (ZC 0x0b78 / 0x0b79), the market lists (0x0b7a) and the market
        /// results (0x0b4e) received: the classic shop windows open on a new packet (the client's handlers MakeWindow),
        /// never because a list is still non-empty.
        /// </summary>
        public int BarterSequence { get; }
        public int ExpandedBarterSequence { get; }
        public int MarketSequence { get; }
        public int MarketResultSequence { get; }

        public bool IsOpen => NpcId != 0 ||
            Mode != RoNpcShopMode.None ||
            IsDealTypePending ||
            BuyItems.Length > 0 ||
            SellItems.Length > 0 ||
            MarketItems.Length > 0 ||
            BarterItems.Length > 0 ||
            ExpandedBarterItems.Length > 0;
    }

    public sealed class RoStorageSnapshot {
        public static readonly RoStorageSnapshot Empty = new RoStorageSnapshot(false, 0, 0, Array.Empty<RoItemInstance>(), "Storage", (byte) RoInventoryType.Storage);

        internal RoStorageSnapshot(bool isOpen, int amount, int maxAmount, RoItemInstance[] items, string name = "Storage", byte inventoryTypeRaw = (byte) RoInventoryType.Storage) {
            IsOpen = isOpen;
            Amount = Math.Max(0, amount);
            MaxAmount = Math.Max(0, maxAmount);
            Items = items ?? Array.Empty<RoItemInstance>();
            Name = string.IsNullOrWhiteSpace(name) ? ResolveDefaultName(inventoryTypeRaw) : name;
            InventoryTypeRaw = inventoryTypeRaw;
        }

        public bool IsOpen { get; }
        public int Amount { get; }
        public int MaxAmount { get; }
        public RoItemInstance[] Items { get; }
        public string Name { get; }
        public byte InventoryTypeRaw { get; }

        private static string ResolveDefaultName(byte inventoryTypeRaw) {
            return inventoryTypeRaw == (byte) RoInventoryType.GuildStorage ? "Guild Storage" : "Storage";
        }
    }

    public sealed class RoCartInventorySnapshot {
        public static readonly RoCartInventorySnapshot Empty = new RoCartInventorySnapshot(false, false, 0, 0, 0, 0, Array.Empty<RoItemInstance>());

        internal RoCartInventorySnapshot(
            bool hasCart,
            bool isOpen,
            int currentCount,
            int maxCount,
            int currentWeight,
            int maxWeight,
            RoItemInstance[] items) {
            HasCart = hasCart;
            IsOpen = isOpen;
            CurrentCount = Math.Max(0, currentCount);
            MaxCount = Math.Max(0, maxCount);
            CurrentWeight = Math.Max(0, currentWeight);
            MaxWeight = Math.Max(0, maxWeight);
            Items = items ?? Array.Empty<RoItemInstance>();
        }

        public bool HasCart { get; }
        public bool IsOpen { get; }
        public int CurrentCount { get; }
        public int MaxCount { get; }
        public int CurrentWeight { get; }
        public int MaxWeight { get; }
        public RoItemInstance[] Items { get; }
    }

    public enum RoBankOperation {
        None = 0,
        Open = 1,
        Check = 2,
        Deposit = 3,
        Withdraw = 4,
        Close = 5
    }

    public sealed class RoBankSnapshot {
        public static readonly RoBankSnapshot Empty = new RoBankSnapshot(false, false, 0, 0, 0, RoBankOperation.None);

        internal RoBankSnapshot(
            bool isOpen,
            bool hasSnapshot,
            long bankZeny,
            long onHandZeny,
            short lastReason,
            RoBankOperation lastOperation,
            int eventSequence = 0) {
            IsOpen = isOpen;
            EventSequence = eventSequence;
            HasSnapshot = hasSnapshot;
            BankZeny = Math.Max(0, bankZeny);
            OnHandZeny = Math.Max(0, onHandZeny);
            LastReason = lastReason;
            LastOperation = lastOperation;
        }

        public bool IsOpen { get; }
        public bool HasSnapshot { get; }
        public long BankZeny { get; }
        public long OnHandZeny { get; }
        public short LastReason { get; }
        public RoBankOperation LastOperation { get; }

        /// <summary>Counts the bank answers (0x09a6 / 0x09a8 / 0x09aa): each one, even with the same values, is an event.</summary>
        public int EventSequence { get; }
    }

    public sealed class RoMapMoveSnapshot {
        public static readonly RoMapMoveSnapshot Empty = new RoMapMoveSnapshot(false, string.Empty, 0, 0);

        internal RoMapMoveSnapshot(bool hasPendingMove, string mapName, int x, int y) {
            HasPendingMove = hasPendingMove;
            MapName = mapName ?? string.Empty;
            X = Math.Max(0, x);
            Y = Math.Max(0, y);
        }

        public bool HasPendingMove { get; }
        public string MapName { get; }
        public int X { get; }
        public int Y { get; }
    }

    public sealed class RoGameplaySnapshot {
        public static readonly RoGameplaySnapshot Empty = new RoGameplaySnapshot(
            Array.Empty<RoItemInstance>(),
            Array.Empty<RoZcEquipSwitchListEntry>(),
            Array.Empty<RoGameplaySkillInfo>(),
            Array.Empty<RoShortcutKeyEntry>(),
            Array.Empty<RoQuestRuntimeEntrySnapshot>(),
            0,
            0,
            RoPlayerStatusSnapshot.Empty,
            RoNpcDialogSnapshot.Empty,
            RoNpcShopSnapshot.Empty,
            RoStorageSnapshot.Empty,
            RoCartInventorySnapshot.Empty,
            RoBankSnapshot.Empty,
            RoMapMoveSnapshot.Empty);

        public RoGameplaySnapshot(
            RoItemInstance[] inventoryItems,
            RoZcEquipSwitchListEntry[] equipSwitchEntries,
            RoGameplaySkillInfo[] skills,
            RoShortcutKeyEntry[] shortcutKeys,
            RoQuestRuntimeEntrySnapshot[] quests,
            int hotkeyRotate,
            int hotkeyTab,
            RoPlayerStatusSnapshot status,
            RoNpcDialogSnapshot npcDialog,
            RoNpcShopSnapshot npcShop,
            RoStorageSnapshot storage,
            RoCartInventorySnapshot cart,
            RoBankSnapshot bank,
            RoMapMoveSnapshot mapMove) {
            InventoryItems = inventoryItems ?? Array.Empty<RoItemInstance>();
            EquipSwitchEntries = equipSwitchEntries ?? Array.Empty<RoZcEquipSwitchListEntry>();
            Skills = skills ?? Array.Empty<RoGameplaySkillInfo>();
            ShortcutKeys = shortcutKeys ?? Array.Empty<RoShortcutKeyEntry>();
            Quests = quests ?? Array.Empty<RoQuestRuntimeEntrySnapshot>();
            HotkeyRotate = hotkeyRotate;
            HotkeyTab = hotkeyTab;
            Status = status ?? RoPlayerStatusSnapshot.Empty;
            NpcDialog = npcDialog ?? RoNpcDialogSnapshot.Empty;
            NpcShop = npcShop ?? RoNpcShopSnapshot.Empty;
            Storage = storage ?? RoStorageSnapshot.Empty;
            Cart = cart ?? RoCartInventorySnapshot.Empty;
            Bank = bank ?? RoBankSnapshot.Empty;
            MapMove = mapMove ?? RoMapMoveSnapshot.Empty;
        }

        public RoGameplaySnapshot(
            RoItemInstance[] inventoryItems,
            RoZcEquipSwitchListEntry[] equipSwitchEntries,
            RoGameplaySkillInfo[] skills,
            RoShortcutKeyEntry[] shortcutKeys,
            RoQuestRuntimeEntrySnapshot[] quests,
            int hotkeyRotate,
            int hotkeyTab,
            RoPlayerStatusSnapshot status,
            RoNpcDialogSnapshot npcDialog,
            RoNpcShopSnapshot npcShop,
            RoStorageSnapshot storage,
            RoMapMoveSnapshot mapMove)
            : this(
                inventoryItems,
                equipSwitchEntries,
                skills,
                shortcutKeys,
                quests,
                hotkeyRotate,
                hotkeyTab,
                status,
                npcDialog,
                npcShop,
                storage,
                RoCartInventorySnapshot.Empty,
                RoBankSnapshot.Empty,
                mapMove) {
        }

        public RoGameplaySnapshot(
            RoItemInstance[] inventoryItems,
            RoZcEquipSwitchListEntry[] equipSwitchEntries,
            RoGameplaySkillInfo[] skills,
            RoShortcutKeyEntry[] shortcutKeys,
            RoQuestRuntimeEntrySnapshot[] quests,
            int hotkeyRotate,
            int hotkeyTab,
            RoPlayerStatusSnapshot status,
            RoNpcDialogSnapshot npcDialog,
            RoNpcShopSnapshot npcShop,
            RoStorageSnapshot storage,
            RoCartInventorySnapshot cart,
            RoMapMoveSnapshot mapMove)
            : this(
                inventoryItems,
                equipSwitchEntries,
                skills,
                shortcutKeys,
                quests,
                hotkeyRotate,
                hotkeyTab,
                status,
                npcDialog,
                npcShop,
                storage,
                cart,
                RoBankSnapshot.Empty,
                mapMove) {
        }

        public RoItemInstance[] InventoryItems { get; }
        public RoZcEquipSwitchListEntry[] EquipSwitchEntries { get; }
        public RoGameplaySkillInfo[] Skills { get; }
        public RoShortcutKeyEntry[] ShortcutKeys { get; }
        public RoQuestRuntimeEntrySnapshot[] Quests { get; }
        public int HotkeyRotate { get; }
        public int HotkeyTab { get; }
        public RoPlayerStatusSnapshot Status { get; }
        public RoNpcDialogSnapshot NpcDialog { get; }
        public RoNpcShopSnapshot NpcShop { get; }
        public RoStorageSnapshot Storage { get; }
        public RoCartInventorySnapshot Cart { get; }
        public RoBankSnapshot Bank { get; }
        public RoMapMoveSnapshot MapMove { get; }
    }

    public sealed class RoGameplaySkillSnapshot {
        public static readonly RoGameplaySkillSnapshot Empty = new RoGameplaySkillSnapshot(
            Array.Empty<RoGameplaySkillInfo>(),
            RoPlayerStatusSnapshot.Empty);

        public RoGameplaySkillSnapshot(RoGameplaySkillInfo[] skills, RoPlayerStatusSnapshot status) {
            Skills = skills ?? Array.Empty<RoGameplaySkillInfo>();
            Status = status ?? RoPlayerStatusSnapshot.Empty;
        }

        public RoGameplaySkillInfo[] Skills { get; }
        public RoPlayerStatusSnapshot Status { get; }
    }

    public sealed class RoCardCompositionSnapshot {
        public static readonly RoCardCompositionSnapshot Empty = new RoCardCompositionSnapshot(0, Array.Empty<int>(), 0);

        public RoCardCompositionSnapshot(int cardBagIndex, int[] targetBagIndexes, int sequence) {
            CardBagIndex = Math.Max(0, cardBagIndex);
            TargetBagIndexes = CopyPositiveIndexes(targetBagIndexes);
            Sequence = sequence;
        }

        public int CardBagIndex { get; }
        public int[] TargetBagIndexes { get; }
        public int Sequence { get; }
        public bool IsActive => CardBagIndex > 0;

        private static int[] CopyPositiveIndexes(int[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<int>();
            }

            var copy = new List<int>(source.Length);
            for (int i = 0; i < source.Length; i++) {
                if (source[i] > 0) {
                    copy.Add(source[i]);
                }
            }

            return copy.Count == 0 ? Array.Empty<int>() : copy.ToArray();
        }
    }

    public sealed class RoItemIdentifySnapshot {
        public static readonly RoItemIdentifySnapshot Empty = new RoItemIdentifySnapshot(Array.Empty<int>(), 0);

        public RoItemIdentifySnapshot(int[] bagIndexes, int sequence) {
            BagIndexes = CopyPositiveIndexes(bagIndexes);
            Sequence = sequence;
        }

        public int[] BagIndexes { get; }
        public int Sequence { get; }
        public bool IsActive => BagIndexes.Length > 0;

        private static int[] CopyPositiveIndexes(int[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<int>();
            }

            var copy = new List<int>(source.Length);
            for (int i = 0; i < source.Length; i++) {
                if (source[i] > 0) {
                    copy.Add(source[i]);
                }
            }

            return copy.Count == 0 ? Array.Empty<int>() : copy.ToArray();
        }
    }

    public sealed class RoMakingArrowSnapshot {
        public static readonly RoMakingArrowSnapshot Empty = new RoMakingArrowSnapshot(Array.Empty<uint>(), 0);

        public RoMakingArrowSnapshot(uint[] itemIds, int sequence) {
            ItemIds = CopyPositiveItemIds(itemIds);
            Sequence = sequence;
        }

        public uint[] ItemIds { get; }
        public int Sequence { get; }
        public bool IsActive => ItemIds.Length > 0;

        private static uint[] CopyPositiveItemIds(uint[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<uint>();
            }

            var copy = new List<uint>(source.Length);
            for (int i = 0; i < source.Length; i++) {
                if (source[i] > 0) {
                    copy.Add(source[i]);
                }
            }

            return copy.Count == 0 ? Array.Empty<uint>() : copy.ToArray();
        }
    }

    public sealed class RoGameplayInventorySnapshot {
        public static readonly RoGameplayInventorySnapshot Empty = new RoGameplayInventorySnapshot(
            Array.Empty<RoItemInstance>(),
            RoPlayerStatusSnapshot.Empty);

        public RoGameplayInventorySnapshot(
            RoItemInstance[] inventoryItems,
            RoPlayerStatusSnapshot status,
            RoCardCompositionSnapshot cardComposition = null,
            RoItemIdentifySnapshot itemIdentify = null,
            RoMakingArrowSnapshot makingArrow = null) {
            InventoryItems = inventoryItems ?? Array.Empty<RoItemInstance>();
            Status = status ?? RoPlayerStatusSnapshot.Empty;
            CardComposition = cardComposition ?? RoCardCompositionSnapshot.Empty;
            ItemIdentify = itemIdentify ?? RoItemIdentifySnapshot.Empty;
            MakingArrow = makingArrow ?? RoMakingArrowSnapshot.Empty;
        }

        public RoItemInstance[] InventoryItems { get; }
        public RoPlayerStatusSnapshot Status { get; }
        public RoCardCompositionSnapshot CardComposition { get; }
        public RoItemIdentifySnapshot ItemIdentify { get; }
        public RoMakingArrowSnapshot MakingArrow { get; }
    }

    public sealed class RoGameplayEquipmentSnapshot {
        public static readonly RoGameplayEquipmentSnapshot Empty = new RoGameplayEquipmentSnapshot(
            Array.Empty<RoItemInstance>(),
            RoPlayerStatusSnapshot.Empty);

        public RoGameplayEquipmentSnapshot(RoItemInstance[] inventoryItems, RoPlayerStatusSnapshot status) {
            InventoryItems = inventoryItems ?? Array.Empty<RoItemInstance>();
            Status = status ?? RoPlayerStatusSnapshot.Empty;
        }

        public RoItemInstance[] InventoryItems { get; }
        public RoPlayerStatusSnapshot Status { get; }
    }

    public sealed class RoGameplayHotkeySnapshot {
        public static readonly RoGameplayHotkeySnapshot Empty = new RoGameplayHotkeySnapshot(
            Array.Empty<RoShortcutKeyEntry>(),
            Array.Empty<RoGameplaySkillInfo>(),
            Array.Empty<RoItemInstance>(),
            0,
            0);

        public RoGameplayHotkeySnapshot(
            RoShortcutKeyEntry[] shortcutKeys,
            RoGameplaySkillInfo[] skills,
            RoItemInstance[] inventoryItems,
            int hotkeyRotate,
            int hotkeyTab) {
            ShortcutKeys = shortcutKeys ?? Array.Empty<RoShortcutKeyEntry>();
            Skills = skills ?? Array.Empty<RoGameplaySkillInfo>();
            InventoryItems = inventoryItems ?? Array.Empty<RoItemInstance>();
            HotkeyRotate = hotkeyRotate;
            HotkeyTab = hotkeyTab;
        }

        public RoShortcutKeyEntry[] ShortcutKeys { get; }
        public RoGameplaySkillInfo[] Skills { get; }
        public RoItemInstance[] InventoryItems { get; }
        public int HotkeyRotate { get; }
        public int HotkeyTab { get; }
    }

    public sealed class RoGameplayQuestSnapshot {
        public static readonly RoGameplayQuestSnapshot Empty = new RoGameplayQuestSnapshot(Array.Empty<RoQuestRuntimeEntrySnapshot>());

        public RoGameplayQuestSnapshot(RoQuestRuntimeEntrySnapshot[] quests, int addedSerial = 0) {
            Quests = quests ?? Array.Empty<RoQuestRuntimeEntrySnapshot>();
            AddedSerial = addedSerial;
        }

        public RoQuestRuntimeEntrySnapshot[] Quests { get; }

        /// <summary>Counts ZC_ADD_QUEST_EX for quests not held before (the client raises its new-quest button for those).</summary>
        public int AddedSerial { get; }
    }

    public sealed class RoGameplayFunctionalSnapshot {
        public static readonly RoGameplayFunctionalSnapshot Empty = new RoGameplayFunctionalSnapshot(
            RoNpcDialogSnapshot.Empty,
            RoTeleportMenuSnapshot.Empty,
            RoNpcShopSnapshot.Empty,
            RoStorageSnapshot.Empty,
            RoCartInventorySnapshot.Empty,
            RoBankSnapshot.Empty,
            RoMapMoveSnapshot.Empty,
            Array.Empty<RoQuestRuntimeEntrySnapshot>());

        public RoGameplayFunctionalSnapshot(
            RoNpcDialogSnapshot npcDialog,
            RoTeleportMenuSnapshot teleportMenu,
            RoNpcShopSnapshot npcShop,
            RoStorageSnapshot storage,
            RoCartInventorySnapshot cart,
            RoBankSnapshot bank,
            RoMapMoveSnapshot mapMove,
            RoQuestRuntimeEntrySnapshot[] quests) {
            NpcDialog = npcDialog ?? RoNpcDialogSnapshot.Empty;
            TeleportMenu = teleportMenu ?? RoTeleportMenuSnapshot.Empty;
            NpcShop = npcShop ?? RoNpcShopSnapshot.Empty;
            Storage = storage ?? RoStorageSnapshot.Empty;
            Cart = cart ?? RoCartInventorySnapshot.Empty;
            Bank = bank ?? RoBankSnapshot.Empty;
            MapMove = mapMove ?? RoMapMoveSnapshot.Empty;
            Quests = quests ?? Array.Empty<RoQuestRuntimeEntrySnapshot>();
        }

        public RoNpcDialogSnapshot NpcDialog { get; }
        public RoTeleportMenuSnapshot TeleportMenu { get; }
        public RoNpcShopSnapshot NpcShop { get; }
        public RoStorageSnapshot Storage { get; }
        public RoCartInventorySnapshot Cart { get; }
        public RoBankSnapshot Bank { get; }
        public RoMapMoveSnapshot MapMove { get; }
        public RoQuestRuntimeEntrySnapshot[] Quests { get; }
    }

    public sealed class RoGameplayState {
        private const int ShortcutKeyCount = 38;

        private readonly Dictionary<int, RoItemInstance> inventoryByBagIndex = new Dictionary<int, RoItemInstance>();
        // the item each ZC_ITEM_THROW_ACK dropped from its bag index, as it was before the ack (the chat's "You dropped %s (%d).")
        private readonly Dictionary<int, RoItemInstance> thrownByBagIndex = new Dictionary<int, RoItemInstance>();
        private readonly Dictionary<int, RoItemInstance> storageByIndex = new Dictionary<int, RoItemInstance>();
        private readonly Dictionary<int, RoGameplaySkillInfo> skillsById = new Dictionary<int, RoGameplaySkillInfo>();
        private readonly Dictionary<uint, RoQuestRuntimeEntrySnapshot> questsById = new Dictionary<uint, RoQuestRuntimeEntrySnapshot>();
        private int questAddedSerial;
        private readonly RoMutablePlayerStatus status = new RoMutablePlayerStatus();
        private readonly RoStatusIconTracker statusIcons = new RoStatusIconTracker();
        private readonly List<string> npcDialogLines = new List<string>(8);
        private readonly List<string> npcDialogMenuChoices = new List<string>(8);

        private RoZcEquipSwitchListEntry[] equipSwitchEntries = Array.Empty<RoZcEquipSwitchListEntry>();
        private RoShortcutKeyEntry[] shortcutKeys = Array.Empty<RoShortcutKeyEntry>();
        private bool hasKnownSkillList;
        private int hotkeyRotate;
        private int hotkeyTab;
        private uint npcDialogId;
        private bool npcDialogCanContinue;
        private bool npcDialogCanClose;
        private bool npcDialogHasMenu;
        private bool npcDialogWantsInput;
        private bool npcDialogWantsNumberInput;
        private bool npcDialogWantsTextInput;
        private bool npcDialogClearOnNextText;
        private int npcDialogWaitCount;
        private bool npcDialogHasCutin;
        private string npcDialogCutinImageName = string.Empty;
        private byte npcDialogCutinTypeRaw;
        private ushort teleportMenuSkillId;
        private string[] teleportMenuMapNames = Array.Empty<string>();
        private int teleportMenuSequence;
        private RoNpcShopMode npcShopMode;
        private bool npcShopDealTypePending;
        private uint npcShopId;
        private RoNpcShopBuyItem[] npcShopBuyItems = Array.Empty<RoNpcShopBuyItem>();
        private RoNpcShopSellItem[] npcShopSellItems = Array.Empty<RoNpcShopSellItem>();
        private RoNpcMarketItem[] npcMarketItems = Array.Empty<RoNpcMarketItem>();
        private RoNpcBarterItem[] npcBarterItems = Array.Empty<RoNpcBarterItem>();
        private RoNpcBarterItem[] npcExpandedBarterItems = Array.Empty<RoNpcBarterItem>();
        private bool npcMarketHasResult;
        private ushort npcMarketResult;
        private RoNpcMarketResultItem[] npcMarketResultItems = Array.Empty<RoNpcMarketResultItem>();
        private bool npcShopHasBuyResult;
        private bool npcShopHasSellResult;
        private byte npcShopBuyResult;
        private byte npcShopSellResult;
        private int npcBarterSequence;
        private int npcExpandedBarterSequence;
        private int npcMarketSequence;
        private int npcMarketResultSequence;
        private bool storageOpen;
        private int storageAmount;
        private int storageMaxAmount;
        private string storageName = "Storage";
        private byte storageInventoryTypeRaw = (byte) RoInventoryType.Storage;
        private readonly Dictionary<int, RoItemInstance> cartByIndex = new Dictionary<int, RoItemInstance>();
        private bool cartHasCart;
        private bool cartOpen;
        private int cartCurrentCount;
        private int cartMaxCount;
        private int cartCurrentWeight;
        private int cartMaxWeight;
        private bool bankOpen;
        private bool bankHasSnapshot;
        private long bankZeny;
        private short bankLastReason;
        private RoBankOperation bankLastOperation;
        private int bankEventSequence;
        private bool hasPendingMapMove;
        private string pendingMapName = string.Empty;
        private int pendingMapX;
        private int pendingMapY;
        private int gameplayVersion;
        private int inventoryVersion;
        private int inventoryReloadVersion;
        private int equipmentVersion;
        private int skillVersion;
        private int hotkeyVersion;
        private int questVersion;
        private int functionalVersion;
        private int npcDialogVersion;
        private int npcShopVersion;
        private int storageVersion;
        private int cartVersion;
        private int bankVersion;
        private int mapMoveVersion;
        private int statusIconVersion;
        private int cachedGameplayVersion = -1;
        private int cachedInventoryVersion = -1;
        private int cachedEquipmentVersion = -1;
        private int cachedSkillVersion = -1;
        private int cachedHotkeyVersion = -1;
        private int cachedQuestVersion = -1;
        private int cachedFunctionalVersion = -1;
        private int cachedNpcDialogVersion = -1;
        private int cachedNpcShopVersion = -1;
        private int cachedStorageVersion = -1;
        private int cachedCartVersion = -1;
        private int cachedBankVersion = -1;
        private int cachedMapMoveVersion = -1;
        private RoGameplaySnapshot cachedGameplaySnapshot = RoGameplaySnapshot.Empty;
        private RoGameplayInventorySnapshot cachedInventorySnapshot = RoGameplayInventorySnapshot.Empty;
        private RoGameplayEquipmentSnapshot cachedEquipmentSnapshot = RoGameplayEquipmentSnapshot.Empty;
        private RoGameplaySkillSnapshot cachedSkillSnapshot = RoGameplaySkillSnapshot.Empty;
        private RoGameplayHotkeySnapshot cachedHotkeySnapshot = RoGameplayHotkeySnapshot.Empty;
        private RoGameplayQuestSnapshot cachedQuestSnapshot = RoGameplayQuestSnapshot.Empty;
        private RoGameplayFunctionalSnapshot cachedFunctionalSnapshot = RoGameplayFunctionalSnapshot.Empty;
        private RoNpcDialogSnapshot cachedNpcDialogSnapshot = RoNpcDialogSnapshot.Empty;
        private RoNpcShopSnapshot cachedNpcShopSnapshot = RoNpcShopSnapshot.Empty;
        private RoStorageSnapshot cachedStorageSnapshot = RoStorageSnapshot.Empty;
        private RoCartInventorySnapshot cachedCartSnapshot = RoCartInventorySnapshot.Empty;
        private RoBankSnapshot cachedBankSnapshot = RoBankSnapshot.Empty;
        private RoMapMoveSnapshot cachedMapMoveSnapshot = RoMapMoveSnapshot.Empty;
        private Dictionary<int, RoItemInstance> pendingInventoryByBagIndex;
        private bool pendingInventorySawList;
        private int pendingCardCompositionCardIndex;
        private int[] pendingCardCompositionTargetIndexes = Array.Empty<int>();
        private int cardCompositionSequence;
        private int[] pendingItemIdentifyIndexes = Array.Empty<int>();
        private int itemIdentifySequence;
        private uint[] pendingMakingArrowItemIds = Array.Empty<uint>();
        private int makingArrowSequence;

        public void Clear() {
            inventoryByBagIndex.Clear();
            pendingInventoryByBagIndex = null;
            pendingInventorySawList = false;
            ClearCardComposition();
            ClearItemIdentify();
            ClearMakingArrow();
            storageByIndex.Clear();
            cartByIndex.Clear();
            skillsById.Clear();
            equipSwitchEntries = Array.Empty<RoZcEquipSwitchListEntry>();
            shortcutKeys = Array.Empty<RoShortcutKeyEntry>();
            hasKnownSkillList = false;
            questsById.Clear();
            hotkeyRotate = 0;
            hotkeyTab = 0;
            ClearNpcDialog();
            ClearNpcShop();
            ClearStorage();
            ClearCart();
            ClearBank();
            ClearPendingMapMove();
            ClearTeleportMenu();
            status.Clear();
            statusIcons.ClearAll();
            MarkAllChanged();
        }

        public void ApplyMapEntry(RoLoginEnterMapResult result) {
            Clear();
            if (result == null) {
                return;
            }

            ApplyMapEntryData(result, ResolveLocalActorId(result), preserveKnownVitals: false);
        }

        public void ApplyMapTransitionEntry(RoLoginEnterMapResult result, bool preserveKnownVitals = false) {
            if (result == null) {
                return;
            }

            ClearMapScopedState();
            ApplyMapEntryData(result, ResolveLocalActorId(result), preserveKnownVitals);
        }

        public void ApplySameMapEntry(RoLoginEnterMapResult result) {
            if (result == null) {
                return;
            }

            ClearMapScopedState();
            ApplyMapEntryData(result, ResolveLocalActorId(result), preserveKnownVitals: true);
        }

        public void ApplyDrain(RoMapEntryDrainResult drain) {
            ApplyDrain(drain, 0u, long.MinValue);
        }

        public void ApplyDrain(RoMapEntryDrainResult drain, uint localActorId, long nowMilliseconds = long.MinValue) {
            if (drain == null) {
                return;
            }

            long resolvedNowMilliseconds = NormalizeStatusIconNow(nowMilliseconds);
            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) {
                ApplyEvent(events[i], localActorId, resolvedNowMilliseconds);
            }

            if (pendingInventoryByBagIndex != null && pendingInventorySawList && CommitInventoryReload()) {
                MarkInventoryChanged();
            }
        }

        private void ApplyMapEntryData(RoLoginEnterMapResult result, uint localActorId, bool preserveKnownVitals) {
            status.ApplyCharacter(result.CharacterResult?.SelectedCharacter, preserveKnownVitals);
            MarkStatusChanged();
            ApplyDrain(result.FollowUp, localActorId);
            ApplyDrain(result.InitialState, localActorId);
            ApplyDrain(result.MoveResponse, localActorId);
        }

        private void ClearMapScopedState() {
            ClearNpcDialog();
            ClearNpcShop();
            ClearStorage();
            ClearCart();
            ClearBank();
            ClearCardComposition();
            ClearMakingArrow();
            ClearPendingMapMove();
            ClearTeleportMenu();
            MarkFunctionalChanged();
        }

        public RoGameplaySnapshot Snapshot() {
            if (cachedGameplayVersion == gameplayVersion) {
                return cachedGameplaySnapshot;
            }

            cachedGameplaySnapshot = new RoGameplaySnapshot(
                BuildInventoryItems(),
                BuildEquipSwitchEntries(),
                BuildSkills(),
                Copy(shortcutKeys),
                BuildQuests(),
                hotkeyRotate,
                hotkeyTab,
                status.ToSnapshot(),
                BuildNpcDialogSnapshot(),
                BuildNpcShopSnapshot(),
                BuildStorageSnapshot(),
                BuildCartSnapshot(),
                BuildBankSnapshot(),
                BuildMapMoveSnapshot());
            cachedGameplayVersion = gameplayVersion;
            return cachedGameplaySnapshot;
        }

        public RoGameplaySkillSnapshot SkillSnapshot() {
            if (cachedSkillVersion == skillVersion) {
                return cachedSkillSnapshot;
            }

            cachedSkillSnapshot = new RoGameplaySkillSnapshot(BuildSkills(), status.ToSnapshot());
            cachedSkillVersion = skillVersion;
            return cachedSkillSnapshot;
        }

        public RoPlayerStatusSnapshot StatusSnapshot() {
            return status.ToSnapshot();
        }

        public int StatusIconVersion => statusIconVersion;

        // Bumps each time the full normal inventory list is (re)loaded wholesale (login, character
        // change, map re-entry) — as opposed to an incremental pickup/delete. Consumers use this to
        // re-baseline rather than mistaking the bulk list for freshly obtained items.
        public int InventoryReloadVersion => inventoryReloadVersion;

        public RoStatusIconSnapshot[] StatusIconSnapshot(long nowMilliseconds = long.MinValue) {
            return statusIcons.Snapshot(NormalizeStatusIconNow(nowMilliseconds));
        }

        public void StatusIconSnapshot(List<RoStatusIconSnapshot> snapshots, long nowMilliseconds = long.MinValue) {
            statusIcons.Snapshot(snapshots, NormalizeStatusIconNow(nowMilliseconds));
        }

        /// <summary>The item the last ZC_ITEM_THROW_ACK of this bag index dropped (0xc2d17f looks it up before the ack applies).</summary>
        public bool TryGetThrownItem(int bagIndex, out RoItemInstance item) => thrownByBagIndex.TryGetValue(bagIndex, out item) && item != null;

        public RoGameplayInventorySnapshot InventorySnapshot() {
            if (cachedInventoryVersion == inventoryVersion) {
                return cachedInventorySnapshot;
            }

            cachedInventorySnapshot = new RoGameplayInventorySnapshot(
                BuildInventoryItems(),
                status.ToSnapshot(),
                BuildCardCompositionSnapshot(),
                BuildItemIdentifySnapshot(),
                BuildMakingArrowSnapshot());
            cachedInventoryVersion = inventoryVersion;
            return cachedInventorySnapshot;
        }

        public RoGameplayEquipmentSnapshot EquipmentSnapshot() {
            if (cachedEquipmentVersion == equipmentVersion) {
                return cachedEquipmentSnapshot;
            }

            cachedEquipmentSnapshot = new RoGameplayEquipmentSnapshot(BuildInventoryItems(), status.ToSnapshot());
            cachedEquipmentVersion = equipmentVersion;
            return cachedEquipmentSnapshot;
        }

        public RoGameplayHotkeySnapshot HotkeySnapshot() {
            if (cachedHotkeyVersion == hotkeyVersion) {
                return cachedHotkeySnapshot;
            }

            cachedHotkeySnapshot = new RoGameplayHotkeySnapshot(
                Copy(shortcutKeys),
                BuildSkills(),
                BuildInventoryItems(),
                hotkeyRotate,
                hotkeyTab);
            cachedHotkeyVersion = hotkeyVersion;
            return cachedHotkeySnapshot;
        }

        public RoGameplayQuestSnapshot QuestSnapshot() {
            if (cachedQuestVersion == questVersion) {
                return cachedQuestSnapshot;
            }

            cachedQuestSnapshot = new RoGameplayQuestSnapshot(BuildQuests(), questAddedSerial);
            cachedQuestVersion = questVersion;
            return cachedQuestSnapshot;
        }

        public RoNpcDialogSnapshot NpcDialogSnapshot() {
            if (cachedNpcDialogVersion == npcDialogVersion) {
                return cachedNpcDialogSnapshot;
            }

            cachedNpcDialogSnapshot = BuildNpcDialogSnapshot();
            cachedNpcDialogVersion = npcDialogVersion;
            return cachedNpcDialogSnapshot;
        }

        public RoNpcShopSnapshot NpcShopSnapshot() {
            if (cachedNpcShopVersion == npcShopVersion) {
                return cachedNpcShopSnapshot;
            }

            cachedNpcShopSnapshot = BuildNpcShopSnapshot();
            cachedNpcShopVersion = npcShopVersion;
            return cachedNpcShopSnapshot;
        }

        public RoStorageSnapshot StorageSnapshot() {
            if (cachedStorageVersion == storageVersion) {
                return cachedStorageSnapshot;
            }

            cachedStorageSnapshot = BuildStorageSnapshot();
            cachedStorageVersion = storageVersion;
            return cachedStorageSnapshot;
        }

        public RoCartInventorySnapshot CartSnapshot() {
            if (cachedCartVersion == cartVersion) {
                return cachedCartSnapshot;
            }

            cachedCartSnapshot = BuildCartSnapshot();
            cachedCartVersion = cartVersion;
            return cachedCartSnapshot;
        }

        public RoBankSnapshot BankSnapshot() {
            if (cachedBankVersion == bankVersion) {
                return cachedBankSnapshot;
            }

            cachedBankSnapshot = BuildBankSnapshot();
            cachedBankVersion = bankVersion;
            return cachedBankSnapshot;
        }

        public RoMapMoveSnapshot MapMoveSnapshot() {
            if (cachedMapMoveVersion == mapMoveVersion) {
                return cachedMapMoveSnapshot;
            }

            cachedMapMoveSnapshot = BuildMapMoveSnapshot();
            cachedMapMoveVersion = mapMoveVersion;
            return cachedMapMoveSnapshot;
        }

        public RoGameplayFunctionalSnapshot FunctionalSnapshot() {
            if (cachedFunctionalVersion == functionalVersion) {
                return cachedFunctionalSnapshot;
            }

            cachedFunctionalSnapshot = new RoGameplayFunctionalSnapshot(
                NpcDialogSnapshot(),
                BuildTeleportMenuSnapshot(),
                NpcShopSnapshot(),
                StorageSnapshot(),
                CartSnapshot(),
                BankSnapshot(),
                MapMoveSnapshot(),
                QuestSnapshot().Quests);
            cachedFunctionalVersion = functionalVersion;
            return cachedFunctionalSnapshot;
        }

        public void ApplyShortcutKeyChange(int tab, int index, RoShortcutKeyEntry shortcut) {
            if (index < 0 || index >= ShortcutKeyCount) {
                return;
            }

            if (shortcutKeys == null || shortcutKeys.Length < ShortcutKeyCount) {
                var expanded = new RoShortcutKeyEntry[ShortcutKeyCount];
                if (shortcutKeys != null && shortcutKeys.Length > 0) {
                    Array.Copy(shortcutKeys, expanded, Math.Min(shortcutKeys.Length, expanded.Length));
                }

                shortcutKeys = expanded;
            }

            shortcutKeys[index] = shortcut;
            hotkeyTab = Math.Max(0, tab);
            MarkHotkeyChanged();
        }

        public void BeginCardComposition(int cardBagIndex) {
            pendingCardCompositionCardIndex = Math.Max(0, cardBagIndex);
            pendingCardCompositionTargetIndexes = Array.Empty<int>();
            cardCompositionSequence++;
            MarkInventoryChanged();
        }

        public void ClearCardComposition() {
            pendingCardCompositionCardIndex = 0;
            pendingCardCompositionTargetIndexes = Array.Empty<int>();
            cardCompositionSequence++;
            MarkInventoryChanged();
        }

        public void ClearItemIdentify() {
            pendingItemIdentifyIndexes = Array.Empty<int>();
            itemIdentifySequence++;
            MarkInventoryChanged();
        }

        public void ClearMakingArrow() {
            pendingMakingArrowItemIds = Array.Empty<uint>();
            makingArrowSequence++;
            MarkInventoryChanged();
        }

        public void ClearTeleportMenu() {
            teleportMenuSkillId = 0;
            teleportMenuMapNames = Array.Empty<string>();
            teleportMenuSequence++;
            MarkFunctionalChanged();
        }

        private RoItemInstance[] BuildInventoryItems() {
            RoItemInstance[] inventory = new RoItemInstance[inventoryByBagIndex.Count];
            inventoryByBagIndex.Values.CopyTo(inventory, 0);
            Array.Sort(inventory, (left, right) => left.BagIndex.CompareTo(right.BagIndex));
            return inventory;
        }

        private RoCardCompositionSnapshot BuildCardCompositionSnapshot() {
            return pendingCardCompositionCardIndex > 0
                ? new RoCardCompositionSnapshot(
                    pendingCardCompositionCardIndex,
                    pendingCardCompositionTargetIndexes,
                    cardCompositionSequence)
                : new RoCardCompositionSnapshot(0, Array.Empty<int>(), cardCompositionSequence);
        }

        private RoItemIdentifySnapshot BuildItemIdentifySnapshot() {
            return new RoItemIdentifySnapshot(pendingItemIdentifyIndexes, itemIdentifySequence);
        }

        private RoMakingArrowSnapshot BuildMakingArrowSnapshot() {
            return new RoMakingArrowSnapshot(pendingMakingArrowItemIds, makingArrowSequence);
        }

        private RoTeleportMenuSnapshot BuildTeleportMenuSnapshot() {
            return new RoTeleportMenuSnapshot(teleportMenuSkillId, teleportMenuMapNames, teleportMenuSequence);
        }

        private RoGameplaySkillInfo[] BuildSkills() {
            RoGameplaySkillInfo[] skills = new RoGameplaySkillInfo[skillsById.Count];
            skillsById.Values.CopyTo(skills, 0);
            Array.Sort(skills, (left, right) => left.SkillId.CompareTo(right.SkillId));
            return skills;
        }

        private RoZcEquipSwitchListEntry[] BuildEquipSwitchEntries() {
            RoZcEquipSwitchListEntry[] equipSwitch = Copy(equipSwitchEntries);
            Array.Sort(equipSwitch, (left, right) => left.BagIndex.CompareTo(right.BagIndex));
            return equipSwitch;
        }

        private RoItemInstance[] BuildStorageItems() {
            RoItemInstance[] storageItems = new RoItemInstance[storageByIndex.Count];
            storageByIndex.Values.CopyTo(storageItems, 0);
            Array.Sort(storageItems, (left, right) => left.BagIndex.CompareTo(right.BagIndex));
            return storageItems;
        }

        private RoItemInstance[] BuildCartItems() {
            RoItemInstance[] cartItems = new RoItemInstance[cartByIndex.Count];
            cartByIndex.Values.CopyTo(cartItems, 0);
            Array.Sort(cartItems, (left, right) => left.BagIndex.CompareTo(right.BagIndex));
            return cartItems;
        }

        private RoQuestRuntimeEntrySnapshot[] BuildQuests() {
            RoQuestRuntimeEntrySnapshot[] quests = new RoQuestRuntimeEntrySnapshot[questsById.Count];
            questsById.Values.CopyTo(quests, 0);
            Array.Sort(quests, (left, right) => left.QuestId.CompareTo(right.QuestId));
            return quests;
        }

        private RoNpcDialogSnapshot BuildNpcDialogSnapshot() {
            return new RoNpcDialogSnapshot(
                npcDialogId,
                npcDialogLines.ToArray(),
                npcDialogCanContinue,
                npcDialogCanClose,
                npcDialogHasMenu,
                npcDialogWantsInput,
                npcDialogMenuChoices.ToArray(),
                npcDialogWantsNumberInput,
                npcDialogWantsTextInput,
                npcDialogHasCutin,
                npcDialogCutinImageName,
                npcDialogCutinTypeRaw,
                npcDialogWaitCount);
        }

        private RoNpcShopSnapshot BuildNpcShopSnapshot() {
            return new RoNpcShopSnapshot(
                npcShopId,
                npcShopMode,
                npcShopDealTypePending,
                Copy(npcShopBuyItems),
                Copy(npcShopSellItems),
                Copy(npcMarketItems),
                Copy(npcBarterItems),
                Copy(npcExpandedBarterItems),
                npcMarketHasResult,
                npcMarketResult,
                Copy(npcMarketResultItems),
                npcShopHasBuyResult,
                npcShopBuyResult,
                npcShopHasSellResult,
                npcShopSellResult,
                npcBarterSequence,
                npcExpandedBarterSequence,
                npcMarketSequence,
                npcMarketResultSequence);
        }

        private RoStorageSnapshot BuildStorageSnapshot() {
            return new RoStorageSnapshot(
                storageOpen,
                storageAmount,
                storageMaxAmount,
                BuildStorageItems(),
                storageName,
                storageInventoryTypeRaw);
        }

        private RoCartInventorySnapshot BuildCartSnapshot() {
            return new RoCartInventorySnapshot(
                cartHasCart,
                cartOpen,
                cartCurrentCount,
                cartMaxCount,
                cartCurrentWeight,
                cartMaxWeight,
                BuildCartItems());
        }

        private RoBankSnapshot BuildBankSnapshot() {
            return new RoBankSnapshot(
                bankOpen,
                bankHasSnapshot,
                bankZeny,
                status.Zeny,
                bankLastReason,
                bankLastOperation,
                bankEventSequence);
        }

        private RoMapMoveSnapshot BuildMapMoveSnapshot() {
            return new RoMapMoveSnapshot(
                hasPendingMapMove,
                pendingMapName,
                pendingMapX,
                pendingMapY);
        }

        private void MarkAllChanged() {
            gameplayVersion++;
            inventoryVersion++;
            equipmentVersion++;
            skillVersion++;
            hotkeyVersion++;
            questVersion++;
            functionalVersion++;
            npcDialogVersion++;
            npcShopVersion++;
            storageVersion++;
            cartVersion++;
            bankVersion++;
            mapMoveVersion++;
            statusIconVersion++;
        }

        private void MarkInventoryChanged() {
            gameplayVersion++;
            inventoryVersion++;
            equipmentVersion++;
            hotkeyVersion++;
        }

        private void MarkEquipmentChanged() {
            gameplayVersion++;
            inventoryVersion++;
            equipmentVersion++;
            hotkeyVersion++;
        }

        private void MarkStatusChanged() {
            gameplayVersion++;
            inventoryVersion++;
            equipmentVersion++;
            skillVersion++;
            hotkeyVersion++;
        }

        private void MarkStatusIconChanged() {
            gameplayVersion++;
            statusIconVersion++;
        }

        private void MarkSkillChanged() {
            gameplayVersion++;
            skillVersion++;
            hotkeyVersion++;
        }

        private void MarkHotkeyChanged() {
            gameplayVersion++;
            hotkeyVersion++;
        }

        private void MarkQuestChanged() {
            gameplayVersion++;
            questVersion++;
            functionalVersion++;
        }

        private void MarkFunctionalChanged() {
            MarkFunctionalSlicesChanged(
                npcDialog: true,
                npcShop: true,
                storage: true,
                cart: true,
                bank: true,
                mapMove: true);
        }

        private void MarkNpcDialogChanged() {
            MarkFunctionalSlicesChanged(npcDialog: true);
        }

        private void MarkNpcShopChanged() {
            MarkFunctionalSlicesChanged(npcShop: true);
        }

        private void MarkNpcDialogAndShopChanged() {
            MarkFunctionalSlicesChanged(npcDialog: true, npcShop: true);
        }

        private void MarkStorageChanged() {
            MarkFunctionalSlicesChanged(storage: true);
        }

        private void MarkCartChanged() {
            MarkFunctionalSlicesChanged(cart: true);
        }

        private void MarkBankChanged() {
            MarkFunctionalSlicesChanged(bank: true);
        }

        private void MarkMapMoveChanged() {
            MarkFunctionalSlicesChanged(mapMove: true);
        }

        private void MarkFunctionalSlicesChanged(
            bool npcDialog = false,
            bool npcShop = false,
            bool storage = false,
            bool cart = false,
            bool bank = false,
            bool mapMove = false
        ) {
            gameplayVersion++;
            functionalVersion++;
            if (npcDialog) {
                npcDialogVersion++;
            }

            if (npcShop) {
                npcShopVersion++;
            }

            if (storage) {
                storageVersion++;
            }

            if (cart) {
                cartVersion++;
            }

            if (bank) {
                bankVersion++;
            }

            if (mapMove) {
                mapMoveVersion++;
            }
        }

        private static uint ResolveLocalActorId(RoLoginEnterMapResult result) {
            return result != null && result.Aid.AccountId > 0
                ? unchecked((uint) result.Aid.AccountId)
                : 0u;
        }

        private static long NormalizeStatusIconNow(long nowMilliseconds) {
            return nowMilliseconds == long.MinValue
                ? RoStatusIconTracker.DefaultMonotonicMilliseconds()
                : nowMilliseconds;
        }

        private bool ApplyLocalStatusIcon(RoZcMsgStateChange change, uint localActorId, long nowMilliseconds) {
            uint actorId = change.Aid == 0 ? localActorId : change.Aid;
            if (localActorId == 0 || actorId != localActorId) {
                return false;
            }

            return statusIcons.Apply(change, localActorId, nowMilliseconds);
        }

        private void ClearStatusIconsIfLocalPlayerDead() {
            if (status.MaxHp > 0 && status.Hp <= 0 && statusIcons.ClearAll()) {
                MarkStatusIconChanged();
            }
        }

        private void ApplyEvent(RoMapEntryDrainEvent drainEvent, uint localActorId, long nowMilliseconds) {
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.ActorVanish:
                    if (localActorId != 0 && drainEvent.ActorVanish.Gid == localActorId && statusIcons.ClearAll()) {
                        MarkStatusIconChanged();
                    }

                    break;
                case RoMapEntryDrainEventKind.InventoryStart:
                    if (drainEvent.InventoryStart.InventoryTypeRaw == (byte) RoInventoryType.Inventory) {
                        BeginInventoryReload();
                    } else if (IsStorageInventoryType(drainEvent.InventoryStart.InventoryTypeRaw)) {
                        BeginStorageReload(drainEvent.InventoryStart.InventoryTypeRaw, drainEvent.InventoryStart.Name);
                        MarkStorageChanged();
                    } else if (drainEvent.InventoryStart.InventoryTypeRaw == (byte) RoInventoryType.Cart) {
                        BeginCartReload();
                        MarkCartChanged();
                    }

                    break;
                case RoMapEntryDrainEventKind.InventoryList:
                    bool inventoryChanged = ApplyInventoryList(drainEvent.InventoryList);
                    if (IsStorageInventoryType(drainEvent.InventoryList.InventoryTypeRaw)) {
                        MarkStorageChanged();
                    } else if (drainEvent.InventoryList.InventoryTypeRaw == (byte) RoInventoryType.Cart) {
                        MarkCartChanged();
                    } else if (inventoryChanged) {
                        MarkInventoryChanged();
                    }

                    break;
                case RoMapEntryDrainEventKind.InventoryEnd:
                    if (IsStorageInventoryType(drainEvent.InventoryEnd.InventoryTypeRaw)) {
                        MarkStorageChanged();
                    } else if (drainEvent.InventoryEnd.InventoryTypeRaw == (byte) RoInventoryType.Cart) {
                        MarkCartChanged();
                    } else if (drainEvent.InventoryEnd.InventoryTypeRaw == (byte) RoInventoryType.Inventory &&
                        CommitInventoryReload()) {
                        inventoryReloadVersion++;
                        MarkInventoryChanged();
                    }

                    break;
                case RoMapEntryDrainEventKind.EquipSwitchList:
                    equipSwitchEntries = Copy(drainEvent.EquipSwitchList.Entries);
                    MarkEquipmentChanged();
                    break;
                case RoMapEntryDrainEventKind.WearEquipmentAck:
                    ApplyWearEquipmentAck(drainEvent.WearEquipmentAck);
                    MarkEquipmentChanged();
                    break;
                case RoMapEntryDrainEventKind.TakeoffEquipmentAck:
                    ApplyTakeoffEquipmentAck(drainEvent.TakeoffEquipmentAck);
                    MarkEquipmentChanged();
                    break;
                case RoMapEntryDrainEventKind.EquipArrow:
                    ApplyEquipArrow(drainEvent.EquipArrow);
                    MarkEquipmentChanged();
                    break;
                case RoMapEntryDrainEventKind.ItemThrowAck:
                    ApplyItemThrowAck(drainEvent.ItemThrowAck);
                    MarkInventoryChanged();
                    break;
                case RoMapEntryDrainEventKind.ItemPickupAck:
                    ApplyItemPickupAck(drainEvent.ItemPickupAck);
                    MarkInventoryChanged();
                    break;
                case RoMapEntryDrainEventKind.InventoryItemDelete:
                    ApplyInventoryItemDelete(drainEvent.InventoryItemDelete);
                    MarkInventoryChanged();
                    break;
                case RoMapEntryDrainEventKind.ActorSpriteChange:
                    if (ApplyLocalActorJobSpriteChange(drainEvent.ActorSpriteChange, localActorId)) {
                        MarkStatusChanged();
                    }

                    break;
                case RoMapEntryDrainEventKind.UseItemAck:
                    ApplyUseItemAck(drainEvent.UseItemAck, localActorId);
                    MarkInventoryChanged();
                    break;
                case RoMapEntryDrainEventKind.ItemCompositionList:
                    ApplyItemCompositionList(drainEvent.ItemCompositionList);
                    MarkInventoryChanged();
                    break;
                case RoMapEntryDrainEventKind.ItemCompositionAck:
                    ApplyItemCompositionAck(drainEvent.ItemCompositionAck);
                    MarkInventoryChanged();
                    break;
                case RoMapEntryDrainEventKind.ItemIdentifyList:
                    ApplyItemIdentifyList(drainEvent.ItemIdentifyList);
                    MarkInventoryChanged();
                    break;
                case RoMapEntryDrainEventKind.ItemIdentifyAck:
                    ApplyItemIdentifyAck(drainEvent.ItemIdentifyAck);
                    MarkInventoryChanged();
                    break;
                case RoMapEntryDrainEventKind.MakingArrowList:
                    ApplyMakingArrowList(drainEvent.MakingArrowList);
                    MarkInventoryChanged();
                    break;
                case RoMapEntryDrainEventKind.TeleportWarpList:
                    ApplyTeleportWarpList(drainEvent.TeleportWarpList);
                    MarkFunctionalChanged();
                    break;
                case RoMapEntryDrainEventKind.ParameterChange:
                    ApplyParameterChange(drainEvent.ParameterChange);
                    MarkStatusChanged();
                    ClearStatusIconsIfLocalPlayerDead();
                    break;
                case RoMapEntryDrainEventKind.StatusSnapshot:
                    status.ApplyStatusSnapshot(drainEvent.StatusSnapshot);
                    MarkStatusChanged();
                    ClearStatusIconsIfLocalPlayerDead();
                    break;
                case RoMapEntryDrainEventKind.StatusPropertyChange:
                    ApplyStatusPropertyChange(drainEvent.StatusPropertyChange);
                    MarkStatusChanged();
                    ClearStatusIconsIfLocalPlayerDead();
                    break;
                case RoMapEntryDrainEventKind.StatusChangeAck:
                    if (ApplyStatusChangeAck(drainEvent.StatusChangeAck)) {
                        MarkStatusChanged();
                    }

                    break;
                case RoMapEntryDrainEventKind.StatusBaseBonus:
                    status.ApplyStatusBaseBonus(drainEvent.StatusBaseBonus);
                    MarkStatusChanged();
                    break;
                case RoMapEntryDrainEventKind.WeightPercentage:
                    status.WeightPercentage = drainEvent.WeightPercentage.WeightPercentage;
                    MarkStatusChanged();
                    break;
                case RoMapEntryDrainEventKind.AttackRange:
                    status.ApplyAttackRange(drainEvent.AttackRange);
                    MarkStatusChanged();
                    break;
                case RoMapEntryDrainEventKind.StatusChange:
                    if (ApplyLocalStatusIcon(drainEvent.StatusChange, localActorId, nowMilliseconds)) {
                        MarkStatusIconChanged();
                    }

                    break;
                case RoMapEntryDrainEventKind.SkillInfoList:
                    ApplySkillList(drainEvent.SkillInfoList);
                    MarkSkillChanged();
                    break;
                case RoMapEntryDrainEventKind.SkillInfoUpdate:
                case RoMapEntryDrainEventKind.SkillInfoAdd:
                case RoMapEntryDrainEventKind.SkillInfoUpdate2:
                    ApplySkillInfo(drainEvent.SkillInfo);
                    MarkSkillChanged();
                    break;
                case RoMapEntryDrainEventKind.ShortcutKeyList:
                    shortcutKeys = Copy(drainEvent.ShortcutKeyList.Shortcuts);
                    hotkeyRotate = drainEvent.ShortcutKeyList.Rotate;
                    hotkeyTab = drainEvent.ShortcutKeyList.Tab;
                    RemoveUnavailableSkillShortcuts();
                    MarkHotkeyChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcDialogText:
                    ApplyNpcDialogText(drainEvent.NpcDialogText);
                    MarkNpcDialogChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcDialogNext:
                    ApplyNpcDialogNext(drainEvent.NpcDialogSignal);
                    MarkNpcDialogChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcDialogClose:
                    ApplyNpcDialogClose(drainEvent.NpcDialogSignal);
                    MarkNpcDialogChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcDialogMenu:
                    ApplyNpcDialogMenu(drainEvent.NpcDialogText);
                    MarkNpcDialogChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcDialogNumberInput:
                    ApplyNpcDialogInput(drainEvent.NpcDialogSignal, wantsNumber: true);
                    MarkNpcDialogChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcDialogTextInput:
                    ApplyNpcDialogInput(drainEvent.NpcDialogSignal, wantsNumber: false);
                    MarkNpcDialogChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcCutin:
                    ApplyNpcCutin(drainEvent.NpcCutin);
                    MarkNpcDialogChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcDialogClear:
                    ClearNpcDialogPage(drainEvent.NpcDialogSignal.NpcId);
                    MarkNpcDialogChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcShopDealType:
                    ApplyNpcShopDealType(drainEvent.NpcShopDealType);
                    MarkNpcShopChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcShopBuyList:
                    ApplyNpcShopBuyList(drainEvent.NpcShopBuyList);
                    MarkNpcDialogAndShopChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcShopSellList:
                    ApplyNpcShopSellList(drainEvent.NpcShopSellList);
                    MarkNpcDialogAndShopChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcShopBuyResult:
                    npcShopHasBuyResult = true;
                    npcShopBuyResult = drainEvent.NpcShopResult.Result;
                    ClearActiveNpcCommerce();
                    MarkNpcShopChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcShopSellResult:
                    npcShopHasSellResult = true;
                    npcShopSellResult = drainEvent.NpcShopResult.Result;
                    ClearActiveNpcCommerce();
                    MarkNpcShopChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcMarketList:
                    ApplyNpcMarketList(drainEvent.NpcMarketList);
                    MarkNpcDialogAndShopChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcMarketResult:
                    ApplyNpcMarketResult(drainEvent.NpcMarketResult);
                    MarkNpcShopChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcBarterList:
                    ApplyNpcBarterList(drainEvent.NpcBarterList, expanded: false);
                    MarkNpcDialogAndShopChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcExpandedBarterList:
                    ApplyNpcBarterList(drainEvent.NpcBarterList, expanded: true);
                    MarkNpcDialogAndShopChanged();
                    break;
                case RoMapEntryDrainEventKind.StorageAmount:
                    ApplyStorageAmount(drainEvent.StorageAmount);
                    MarkStorageChanged();
                    break;
                case RoMapEntryDrainEventKind.StorageItemAdd:
                    ApplyStorageItem(drainEvent.StorageItem);
                    MarkStorageChanged();
                    break;
                case RoMapEntryDrainEventKind.StorageItemDelete:
                    ApplyStorageItemDelete(drainEvent.StorageItemDelete);
                    MarkStorageChanged();
                    break;
                case RoMapEntryDrainEventKind.StorageClose:
                    ClearStorage();
                    MarkStorageChanged();
                    break;
                case RoMapEntryDrainEventKind.CartAmount:
                    ApplyCartAmount(drainEvent.CartAmount);
                    MarkCartChanged();
                    break;
                case RoMapEntryDrainEventKind.CartItemAdd:
                    ApplyCartItem(drainEvent.StorageItem);
                    MarkCartChanged();
                    break;
                case RoMapEntryDrainEventKind.CartItemDelete:
                    ApplyCartItemDelete(drainEvent.StorageItemDelete);
                    MarkCartChanged();
                    break;
                case RoMapEntryDrainEventKind.CartOff:
                    ClearCart();
                    MarkCartChanged();
                    break;
                case RoMapEntryDrainEventKind.BankOpenAck:
                    ApplyBankOpenAck(drainEvent.BankSimpleAck);
                    MarkBankChanged();
                    break;
                case RoMapEntryDrainEventKind.BankCheck:
                    ApplyBankCheck(drainEvent.BankCheck);
                    MarkBankChanged();
                    break;
                case RoMapEntryDrainEventKind.BankDepositAck:
                    ApplyBankTransactionAck(drainEvent.BankTransactionAck, RoBankOperation.Deposit);
                    MarkStatusChanged();
                    MarkBankChanged();
                    break;
                case RoMapEntryDrainEventKind.BankWithdrawAck:
                    ApplyBankTransactionAck(drainEvent.BankTransactionAck, RoBankOperation.Withdraw);
                    MarkStatusChanged();
                    MarkBankChanged();
                    break;
                case RoMapEntryDrainEventKind.BankCloseAck:
                    ApplyBankCloseAck(drainEvent.BankSimpleAck);
                    MarkBankChanged();
                    break;
                case RoMapEntryDrainEventKind.ChangeItemOption:
                    ApplyChangeItemOption(drainEvent.ChangeItemOption);
                    MarkInventoryChanged();
                    MarkFunctionalChanged();
                    break;
                case RoMapEntryDrainEventKind.NpcMapMove:
                    ApplyNpcMapMove(drainEvent.NpcMapMove);
                    MarkMapMoveChanged();
                    break;
                case RoMapEntryDrainEventKind.QuestList:
                    ApplyQuestList(drainEvent.QuestList);
                    MarkQuestChanged();
                    break;
                case RoMapEntryDrainEventKind.QuestAdd:
                    if (!questsById.ContainsKey(drainEvent.QuestAdd.Entry.QuestId)) questAddedSerial++;
                    ApplyQuestEntry(drainEvent.QuestAdd.Entry);
                    MarkQuestChanged();
                    break;
                case RoMapEntryDrainEventKind.QuestDelete:
                    questsById.Remove(drainEvent.QuestDelete.QuestId);
                    MarkQuestChanged();
                    break;
                case RoMapEntryDrainEventKind.QuestActive:
                    ApplyQuestActive(drainEvent.QuestActive);
                    MarkQuestChanged();
                    break;
                case RoMapEntryDrainEventKind.QuestObjectiveUpdate:
                    ApplyQuestObjectiveUpdate(drainEvent.QuestObjectiveUpdate);
                    MarkQuestChanged();
                    break;
            }
        }

        private void ApplyQuestList(RoZcQuestList list) {
            questsById.Clear();
            RoZcQuestEntry[] entries = list.Entries ?? Array.Empty<RoZcQuestEntry>();
            for (int i = 0; i < entries.Length; i++) {
                ApplyQuestEntry(entries[i]);
            }
        }

        private void ApplyQuestEntry(RoZcQuestEntry entry) {
            var objectives = new RoQuestObjectiveRuntimeSnapshot[entry.Objectives.Length];
            for (int i = 0; i < entry.Objectives.Length; i++) {
                RoZcQuestObjective objective = entry.Objectives[i];
                objectives[i] = new RoQuestObjectiveRuntimeSnapshot(
                    objective.HuntIdent,
                    objective.HuntIdent2,
                    objective.MobId,
                    objective.Current,
                    objective.Total,
                    objective.MobName,
                    objective.MobType,
                    objective.LevelMin,
                    objective.LevelMax);
            }

            questsById[entry.QuestId] = new RoQuestRuntimeEntrySnapshot(
                entry.QuestId,
                entry.ActiveRaw,
                entry.ServerTime,
                entry.EndTime,
                objectives);
        }

        private void ApplyQuestActive(RoZcQuestActive active) {
            if (questsById.TryGetValue(active.QuestId, out RoQuestRuntimeEntrySnapshot entry)) {
                questsById[active.QuestId] = entry.WithActive(active.ActiveRaw);
                return;
            }

            questsById[active.QuestId] = new RoQuestRuntimeEntrySnapshot(
                active.QuestId,
                active.ActiveRaw,
                0,
                0,
                Array.Empty<RoQuestObjectiveRuntimeSnapshot>());
        }

        private void ApplyQuestObjectiveUpdate(RoZcQuestObjectiveUpdate update) {
            RoZcQuestObjectiveCounter[] counters = update.Updates ?? Array.Empty<RoZcQuestObjectiveCounter>();
            for (int i = 0; i < counters.Length; i++) {
                RoZcQuestObjectiveCounter counter = counters[i];
                if (!questsById.TryGetValue(counter.QuestId, out RoQuestRuntimeEntrySnapshot entry)) {
                    entry = new RoQuestRuntimeEntrySnapshot(
                        counter.QuestId,
                        0,
                        0,
                        0,
                        Array.Empty<RoQuestObjectiveRuntimeSnapshot>());
                }

                questsById[counter.QuestId] = entry.WithObjectives(MergeQuestCounter(entry.Objectives, counter));
            }
        }

        private static RoQuestObjectiveRuntimeSnapshot[] MergeQuestCounter(
            RoQuestObjectiveRuntimeSnapshot[] objectives,
            RoZcQuestObjectiveCounter counter) {
            objectives ??= Array.Empty<RoQuestObjectiveRuntimeSnapshot>();
            var merged = new List<RoQuestObjectiveRuntimeSnapshot>(objectives.Length + 1);
            bool replaced = false;
            for (int i = 0; i < objectives.Length; i++) {
                RoQuestObjectiveRuntimeSnapshot objective = objectives[i];
                if (MatchesQuestCounter(objective, counter)) {
                    merged.Add(new RoQuestObjectiveRuntimeSnapshot(
                        objective.HuntIdent,
                        objective.HuntIdent2,
                        objective.MobId,
                        counter.Current,
                        counter.Total,
                        objective.MobName,
                        objective.MobType,
                        objective.LevelMin,
                        objective.LevelMax));
                    replaced = true;
                } else {
                    merged.Add(objective);
                }
            }

            if (!replaced) {
                merged.Add(new RoQuestObjectiveRuntimeSnapshot(
                    counter.HuntIdent,
                    counter.HuntIdent2,
                    counter.MobId,
                    counter.Current,
                    counter.Total,
                    string.Empty));
            }

            return merged.ToArray();
        }

        private static bool MatchesQuestCounter(RoQuestObjectiveRuntimeSnapshot objective, RoZcQuestObjectiveCounter counter) {
            if (counter.HuntIdent != 0 || counter.HuntIdent2 != 0) {
                return objective.HuntIdent == counter.HuntIdent && objective.HuntIdent2 == counter.HuntIdent2;
            }

            return objective.MobId == counter.MobId;
        }

        private void ApplyNpcDialogText(RoZcNpcDialogText text) {
            if (npcDialogClearOnNextText || (text.NpcId != 0 && npcDialogId != 0 && text.NpcId != npcDialogId)) {
                ClearNpcDialogPage(text.NpcId);
            }

            if (text.NpcId != 0) {
                npcDialogId = text.NpcId;
            }

            if (!string.IsNullOrWhiteSpace(text.Message)) {
                npcDialogLines.Add(text.Message.Trim());
            }

            npcDialogCanContinue = false;
            npcDialogCanClose = false;
            npcDialogHasMenu = false;
            npcDialogWantsInput = false;
            npcDialogWantsNumberInput = false;
            npcDialogWantsTextInput = false;
            npcDialogMenuChoices.Clear();
            npcDialogClearOnNextText = false;
        }

        private void ApplyNpcDialogNext(RoZcNpcDialogSignal signal) {
            if (signal.NpcId != 0) {
                npcDialogId = signal.NpcId;
            }

            npcDialogCanContinue = true;
            npcDialogWaitCount++;
            npcDialogCanClose = false;
            npcDialogHasMenu = false;
            npcDialogWantsInput = false;
            npcDialogWantsNumberInput = false;
            npcDialogWantsTextInput = false;
            npcDialogMenuChoices.Clear();
            npcDialogClearOnNextText = true;
        }

        private void ApplyNpcDialogClose(RoZcNpcDialogSignal signal) {
            if (signal.NpcId != 0) {
                npcDialogId = signal.NpcId;
            }

            npcDialogCanContinue = false;
            npcDialogCanClose = true;
            npcDialogHasMenu = false;
            npcDialogWantsInput = false;
            npcDialogWantsNumberInput = false;
            npcDialogWantsTextInput = false;
            npcDialogMenuChoices.Clear();
        }

        private void ApplyNpcDialogMenu(RoZcNpcDialogText menu) {
            if (menu.NpcId != 0) {
                npcDialogId = menu.NpcId;
            }

            npcDialogMenuChoices.Clear();
            SplitMenuChoices(menu.Message, npcDialogMenuChoices);

            npcDialogCanContinue = false;
            npcDialogCanClose = false;
            npcDialogHasMenu = true;
            npcDialogWantsInput = false;
            npcDialogWantsNumberInput = false;
            npcDialogWantsTextInput = false;
        }

        private void ApplyNpcDialogInput(RoZcNpcDialogSignal signal, bool wantsNumber) {
            if (signal.NpcId != 0) {
                npcDialogId = signal.NpcId;
            }

            npcDialogCanContinue = false;
            npcDialogCanClose = false;
            npcDialogHasMenu = false;
            npcDialogWantsInput = true;
            npcDialogWantsNumberInput = wantsNumber;
            npcDialogWantsTextInput = !wantsNumber;
            npcDialogMenuChoices.Clear();
        }

        private void ClearNpcDialog() {
            npcDialogId = 0;
            ClearNpcDialogPage(0);
            npcDialogHasCutin = false;
            npcDialogCutinImageName = string.Empty;
            npcDialogCutinTypeRaw = 0;
        }

        internal void ClearNpcDialogPageLocally(uint npcId) {
            ClearNpcDialogPage(npcId);
            MarkNpcDialogChanged();
        }

        internal void AdvanceNpcDialogPageLocally(uint npcId) {
            if (npcId != 0) {
                npcDialogId = npcId;
            }

            npcDialogCanContinue = false;
            npcDialogCanClose = false;
            npcDialogHasMenu = false;
            npcDialogWantsInput = false;
            npcDialogWantsNumberInput = false;
            npcDialogWantsTextInput = false;
            npcDialogMenuChoices.Clear();
            npcDialogClearOnNextText = true;
            MarkNpcDialogChanged();
        }

        internal void ClearNpcDialogFullyLocally() {
            ClearNpcDialog();
            MarkNpcDialogChanged();
        }

        internal void CloseBankLocally() {
            bankOpen = false;
            bankLastReason = 0;
            bankLastOperation = RoBankOperation.Close;
            MarkBankChanged();
        }

        private void ClearNpcDialogPage(uint npcId) {
            if (npcId != 0) {
                npcDialogId = npcId;
            }

            npcDialogLines.Clear();
            npcDialogMenuChoices.Clear();
            npcDialogCanContinue = false;
            npcDialogCanClose = false;
            npcDialogHasMenu = false;
            npcDialogWantsInput = false;
            npcDialogWantsNumberInput = false;
            npcDialogWantsTextInput = false;
            npcDialogClearOnNextText = false;
        }

        private void ApplyNpcCutin(RoZcNpcCutin cutin) {
            if (cutin.IsClear || string.IsNullOrWhiteSpace(cutin.ImageName)) {
                npcDialogHasCutin = false;
                npcDialogCutinImageName = string.Empty;
                npcDialogCutinTypeRaw = cutin.TypeRaw;
                return;
            }

            npcDialogHasCutin = true;
            npcDialogCutinImageName = cutin.ImageName;
            npcDialogCutinTypeRaw = cutin.TypeRaw;
        }

        private void ApplyNpcShopDealType(RoZcSelectDealType dealType) {
            npcShopId = dealType.NpcId;
            npcShopMode = RoNpcShopMode.DealType;
            npcShopDealTypePending = true;
            npcShopBuyItems = Array.Empty<RoNpcShopBuyItem>();
            npcShopSellItems = Array.Empty<RoNpcShopSellItem>();
            npcMarketItems = Array.Empty<RoNpcMarketItem>();
            npcBarterItems = Array.Empty<RoNpcBarterItem>();
            npcExpandedBarterItems = Array.Empty<RoNpcBarterItem>();
            npcShopHasBuyResult = false;
            npcShopHasSellResult = false;
            npcMarketHasResult = false;
        }

        private void ApplyNpcShopBuyList(RoZcNpcShopBuyList buyList) {
            ClearNpcDialog();
            npcShopMode = RoNpcShopMode.Buy;
            npcShopDealTypePending = false;
            npcShopBuyItems = Copy(buyList.Items);
            npcShopSellItems = Array.Empty<RoNpcShopSellItem>();
            npcMarketItems = Array.Empty<RoNpcMarketItem>();
            npcBarterItems = Array.Empty<RoNpcBarterItem>();
            npcExpandedBarterItems = Array.Empty<RoNpcBarterItem>();
            npcShopHasBuyResult = false;
        }

        private void ApplyNpcShopSellList(RoZcNpcShopSellList sellList) {
            ClearNpcDialog();
            npcShopMode = RoNpcShopMode.Sell;
            npcShopDealTypePending = false;
            npcShopSellItems = Copy(sellList.Items);
            npcShopBuyItems = Array.Empty<RoNpcShopBuyItem>();
            npcMarketItems = Array.Empty<RoNpcMarketItem>();
            npcBarterItems = Array.Empty<RoNpcBarterItem>();
            npcExpandedBarterItems = Array.Empty<RoNpcBarterItem>();
            npcShopHasSellResult = false;
        }

        private void ApplyNpcMarketList(RoZcNpcMarketList list) {
            ClearNpcDialog();
            npcShopMode = RoNpcShopMode.Market;
            npcShopDealTypePending = false;
            npcShopBuyItems = Array.Empty<RoNpcShopBuyItem>();
            npcShopSellItems = Array.Empty<RoNpcShopSellItem>();
            npcMarketItems = Copy(list.Items);
            npcBarterItems = Array.Empty<RoNpcBarterItem>();
            npcExpandedBarterItems = Array.Empty<RoNpcBarterItem>();
            npcMarketHasResult = false;
            npcMarketSequence++;
        }

        private void ApplyNpcMarketResult(RoZcNpcMarketPurchaseResult result) {
            npcMarketResultSequence++;
            npcMarketHasResult = true;
            npcMarketResult = result.Result;
            npcMarketResultItems = Copy(result.Items);
            ClearActiveNpcCommerce();
        }

        private void ApplyNpcBarterList(RoZcNpcBarterList list, bool expanded) {
            ClearNpcDialog();
            npcShopMode = expanded ? RoNpcShopMode.ExpandedBarter : RoNpcShopMode.Barter;
            npcShopDealTypePending = false;
            npcShopBuyItems = Array.Empty<RoNpcShopBuyItem>();
            npcShopSellItems = Array.Empty<RoNpcShopSellItem>();
            npcMarketItems = Array.Empty<RoNpcMarketItem>();
            // the barter results come as ZC_PC_PURCHASE_RESULT (0x00ca 11..14): a new list waits for a new one
            npcShopHasBuyResult = false;
            if (expanded) {
                npcBarterItems = Array.Empty<RoNpcBarterItem>();
                npcExpandedBarterItems = Copy(list.Items);
                npcExpandedBarterSequence++;
            } else {
                npcBarterItems = Copy(list.Items);
                npcExpandedBarterItems = Array.Empty<RoNpcBarterItem>();
                npcBarterSequence++;
            }
        }

        /// <summary>
        /// A barter / expanded barter / market window closed (Cancel, Buy, Esc): its item list goes like the client's
        /// mgr.Clear() 0x59cfd0 / 0x59fe80 and session.ClearShopLists() 0xce5220, so nothing shows it again later. The zeny
        /// shop the same: the deal choice's Cancel drops the pending deal, the buy / sell windows' Cancel or Buy / Sell their list.
        /// </summary>
        internal void ClearNpcCommerceLocally(RoNpcShopMode mode) {
            switch (mode) {
                case RoNpcShopMode.DealType:
                    npcShopDealTypePending = false;
                    break;
                case RoNpcShopMode.Buy:
                    npcShopBuyItems = Array.Empty<RoNpcShopBuyItem>();
                    break;
                case RoNpcShopMode.Sell:
                    npcShopSellItems = Array.Empty<RoNpcShopSellItem>();
                    break;
                case RoNpcShopMode.Barter:
                    npcBarterItems = Array.Empty<RoNpcBarterItem>();
                    break;
                case RoNpcShopMode.ExpandedBarter:
                    npcExpandedBarterItems = Array.Empty<RoNpcBarterItem>();
                    break;
                case RoNpcShopMode.Market:
                    npcMarketItems = Array.Empty<RoNpcMarketItem>();
                    break;
                default:
                    return;
            }

            if (npcShopMode == mode) {
                npcShopMode = RoNpcShopMode.None;
                npcShopId = 0;
            }

            MarkNpcShopChanged();
        }

        private void ClearNpcShop() {
            npcShopDealTypePending = false;
            npcShopId = 0;
            npcShopMode = RoNpcShopMode.None;
            npcShopBuyItems = Array.Empty<RoNpcShopBuyItem>();
            npcShopSellItems = Array.Empty<RoNpcShopSellItem>();
            npcMarketItems = Array.Empty<RoNpcMarketItem>();
            npcBarterItems = Array.Empty<RoNpcBarterItem>();
            npcExpandedBarterItems = Array.Empty<RoNpcBarterItem>();
            npcShopHasBuyResult = false;
            npcShopHasSellResult = false;
            npcShopBuyResult = 0;
            npcShopSellResult = 0;
            npcMarketHasResult = false;
            npcMarketResult = 0;
            npcMarketResultItems = Array.Empty<RoNpcMarketResultItem>();
        }

        private void ClearActiveNpcCommerce() {
            npcShopDealTypePending = false;
            npcShopId = 0;
            npcShopMode = RoNpcShopMode.None;
            npcShopBuyItems = Array.Empty<RoNpcShopBuyItem>();
            npcShopSellItems = Array.Empty<RoNpcShopSellItem>();
            npcMarketItems = Array.Empty<RoNpcMarketItem>();
            npcBarterItems = Array.Empty<RoNpcBarterItem>();
            npcExpandedBarterItems = Array.Empty<RoNpcBarterItem>();
        }

        private void ApplyStorageAmount(RoZcStorageAmount amount) {
            storageOpen = true;
            storageAmount = amount.Amount;
            storageMaxAmount = amount.MaxAmount;
        }

        private void ApplyStorageItem(RoZcStorageItem item) {
            storageOpen = true;
            RoItemInstance storageItem = ConvertStorageItem(item);
            if (storageByIndex.TryGetValue(storageItem.BagIndex, out RoItemInstance existing) && existing != null) {
                storageItem = storageItem.WithCount(SaturatingAdd(existing.Count, storageItem.Count));
            }

            storageByIndex[storageItem.BagIndex] = storageItem;
            storageAmount = Math.Max(storageAmount, storageByIndex.Count);
        }

        private void ApplyStorageItemDelete(RoZcStorageItemDelete delete) {
            int index = delete.StorageIndex;
            if (!storageByIndex.TryGetValue(index, out RoItemInstance existing) || existing == null) {
                return;
            }

            int removed = delete.Amount > int.MaxValue ? int.MaxValue : unchecked((int) delete.Amount);
            int remaining = Math.Max(0, existing.Count - removed);
            if (remaining <= 0) {
                storageByIndex.Remove(index);
                storageAmount = Math.Max(0, storageAmount - 1);
            } else {
                storageByIndex[index] = existing.WithCount(remaining);
            }
        }

        private void ClearStorage() {
            storageOpen = false;
            storageAmount = 0;
            storageMaxAmount = 0;
            storageByIndex.Clear();
            storageName = "Storage";
            storageInventoryTypeRaw = (byte) RoInventoryType.Storage;
        }

        private void BeginStorageReload(byte inventoryTypeRaw, string name) {
            storageOpen = true;
            storageInventoryTypeRaw = inventoryTypeRaw;
            storageName = !string.IsNullOrWhiteSpace(name)
                ? name
                : inventoryTypeRaw == (byte) RoInventoryType.GuildStorage ? "Guild Storage" : "Storage";
            storageByIndex.Clear();
        }

        private void BeginCartReload() {
            cartHasCart = true;
            cartOpen = true;
            cartByIndex.Clear();
        }

        private void ApplyCartAmount(RoZcCartAmount amount) {
            cartHasCart = true;
            cartOpen = true;
            cartCurrentCount = amount.CurrentCount;
            cartMaxCount = amount.MaxCount;
            cartCurrentWeight = amount.CurrentWeight;
            cartMaxWeight = amount.MaxWeight;
        }

        private void ApplyCartItem(RoZcStorageItem item) {
            cartHasCart = true;
            cartOpen = true;
            RoItemInstance cartItem = ConvertStorageItem(item);
            if (cartByIndex.TryGetValue(cartItem.BagIndex, out RoItemInstance existing) && existing != null) {
                cartItem = cartItem.WithCount(SaturatingAdd(existing.Count, cartItem.Count));
            }

            cartByIndex[cartItem.BagIndex] = cartItem;
            cartCurrentCount = Math.Max(cartCurrentCount, cartByIndex.Count);
        }

        private void ApplyCartItemDelete(RoZcStorageItemDelete delete) {
            int index = delete.StorageIndex;
            if (!cartByIndex.TryGetValue(index, out RoItemInstance existing) || existing == null) {
                return;
            }

            int removed = delete.Amount > int.MaxValue ? int.MaxValue : unchecked((int) delete.Amount);
            int remaining = Math.Max(0, existing.Count - removed);
            if (remaining <= 0) {
                cartByIndex.Remove(index);
                cartCurrentCount = Math.Max(0, cartCurrentCount - 1);
            } else {
                cartByIndex[index] = existing.WithCount(remaining);
            }
        }

        private void ClearCart() {
            cartHasCart = false;
            cartOpen = false;
            cartCurrentCount = 0;
            cartMaxCount = 0;
            cartCurrentWeight = 0;
            cartMaxWeight = 0;
            cartByIndex.Clear();
        }

        private void ApplyBankOpenAck(RoZcBankSimpleAck ack) {
            bankLastReason = unchecked((short) ack.ResultRaw);
            bankLastOperation = RoBankOperation.Open;
        }

        private void ApplyBankCheck(RoZcBankCheck check) {
            bankOpen = true;
            bankHasSnapshot = true;
            bankZeny = check.Money;
            bankLastReason = check.Reason;
            bankLastOperation = RoBankOperation.Check;
            bankEventSequence++;
        }

        private void ApplyBankTransactionAck(RoZcBankTransactionAck ack, RoBankOperation operation) {
            bankOpen = true;
            bankHasSnapshot = true;
            bankZeny = ack.BankMoney;
            bankLastReason = ack.Reason;
            bankLastOperation = operation;
            bankEventSequence++;
            status.SetZeny(ack.Zeny);
        }

        private void ApplyBankCloseAck(RoZcBankSimpleAck ack) {
            bankOpen = false;
            bankLastReason = unchecked((short) ack.ResultRaw);
            bankLastOperation = RoBankOperation.Close;
        }

        private void ClearBank() {
            bankOpen = false;
            bankHasSnapshot = false;
            bankZeny = 0;
            bankLastReason = 0;
            bankLastOperation = RoBankOperation.None;
        }

        private void ApplyNpcMapMove(RoZcNpcMapMove mapMove) {
            hasPendingMapMove = true;
            pendingMapName = mapMove.MapName;
            pendingMapX = mapMove.X;
            pendingMapY = mapMove.Y;
        }

        public void ClearPendingMapMove() {
            hasPendingMapMove = false;
            pendingMapName = string.Empty;
            pendingMapX = 0;
            pendingMapY = 0;
            MarkMapMoveChanged();
        }

        private void BeginInventoryReload() {
            pendingInventoryByBagIndex = new Dictionary<int, RoItemInstance>();
            pendingInventorySawList = false;
        }

        private bool CommitInventoryReload() {
            if (pendingInventoryByBagIndex == null) {
                return false;
            }

            inventoryByBagIndex.Clear();
            foreach (KeyValuePair<int, RoItemInstance> item in pendingInventoryByBagIndex) {
                inventoryByBagIndex[item.Key] = item.Value;
            }

            pendingInventoryByBagIndex = null;
            pendingInventorySawList = false;
            return true;
        }

        private bool ApplyInventoryList(RoZcInventoryItemList inventoryList) {
            if (IsStorageInventoryType(inventoryList.InventoryTypeRaw)) {
                storageOpen = true;
                storageInventoryTypeRaw = inventoryList.InventoryTypeRaw;
                if (!string.IsNullOrWhiteSpace(inventoryList.Name)) {
                    storageName = inventoryList.Name;
                }

                RoZcInventoryItem[] storageItems = inventoryList.Items;
                for (int i = 0; i < storageItems.Length; i++) {
                    RoItemInstance item = ConvertInventoryItem(storageItems[i]);
                    storageByIndex[item.BagIndex] = item;
                }

                return false;
            }

            if (inventoryList.InventoryTypeRaw == (byte) RoInventoryType.Cart) {
                cartHasCart = true;
                cartOpen = true;
                RoZcInventoryItem[] cartItems = inventoryList.Items;
                for (int i = 0; i < cartItems.Length; i++) {
                    RoItemInstance item = ConvertInventoryItem(cartItems[i]);
                    cartByIndex[item.BagIndex] = item;
                }

                cartCurrentCount = Math.Max(cartCurrentCount, cartByIndex.Count);
                return false;
            }

            if (inventoryList.InventoryTypeRaw != (byte) RoInventoryType.Inventory) {
                return false;
            }

            Dictionary<int, RoItemInstance> target = pendingInventoryByBagIndex ?? inventoryByBagIndex;
            if (pendingInventoryByBagIndex != null) {
                pendingInventorySawList = true;
            }

            RoZcInventoryItem[] items = inventoryList.Items;
            for (int i = 0; i < items.Length; i++) {
                RoItemInstance item = ConvertInventoryItem(items[i]);
                target[item.BagIndex] = item;
            }

            return pendingInventoryByBagIndex == null;
        }

        private void ApplyChangeItemOption(RoZcChangeItemOption change) {
            if (change.BagIndex <= 0) {
                return;
            }

            int bagIndex = change.BagIndex;
            if (inventoryByBagIndex.TryGetValue(bagIndex, out RoItemInstance inventoryItem) && inventoryItem != null) {
                inventoryByBagIndex[bagIndex] = inventoryItem.WithItemOptions(
                    change.IsDamaged,
                    change.RefineLevel,
                    change.CardIds,
                    CopyRandomOptions(change.RandomOptions),
                    change.EnchantGrade);
            }

            if (pendingInventoryByBagIndex != null &&
                pendingInventoryByBagIndex.TryGetValue(bagIndex, out RoItemInstance pendingItem) &&
                pendingItem != null) {
                pendingInventoryByBagIndex[bagIndex] = pendingItem.WithItemOptions(
                    change.IsDamaged,
                    change.RefineLevel,
                    change.CardIds,
                    CopyRandomOptions(change.RandomOptions),
                    change.EnchantGrade);
            }
        }

        private void ApplySkillList(RoZcSkillInfoList skillList) {
            skillsById.Clear();
            hasKnownSkillList = true;
            RoZcSkillInfo[] skills = skillList.Skills;
            for (int i = 0; i < skills.Length; i++) {
                ApplySkillInfo(skills[i]);
            }

            RemoveUnavailableSkillShortcuts();
        }

        private void ApplySkillInfo(RoZcSkillInfo skillInfo) {
            var skill = new RoGameplaySkillInfo(skillInfo);
            if (skill.SkillId == 0) {
                return;
            }

            if (skillsById.TryGetValue(skill.SkillId, out RoGameplaySkillInfo existing)) {
                skill = MergeSkillInfo(existing, skill);
            }

            skillsById[skill.SkillId] = skill;
        }

        private static RoGameplaySkillInfo MergeSkillInfo(RoGameplaySkillInfo existing, RoGameplaySkillInfo update) {
            if (existing.SkillId == 0 || existing.SkillId != update.SkillId) {
                return update;
            }

            int type = update.Type != 0 ? update.Type : existing.Type;
            string name = !string.IsNullOrWhiteSpace(update.Name) ? update.Name : existing.Name;
            ushort level2 = update.Level2 != 0 ? update.Level2 : update.Level;
            return new RoGameplaySkillInfo(new RoZcSkillInfo(
                update.SkillId,
                type,
                update.Level,
                update.SpCost,
                update.AttackRange,
                name,
                update.Upgradable,
                level2));
        }

        private void ApplyParameterChange(RoZcParameterChange change) {
            int previousJobId = status.JobId;
            status.ApplyParameter(change);
            ClearJobScopedStateIfJobChanged(change.Property, previousJobId);
        }

        private void ApplyStatusPropertyChange(RoZcStatusPropertyChange change) {
            int previousJobId = status.JobId;
            status.ApplyStatusPropertyChange(change);
            ClearJobScopedStateIfJobChanged(change.Property, previousJobId);
        }

        private bool ApplyStatusChangeAck(RoZcStatusChangeAck ack) {
            int previousJobId = status.JobId;
            bool changed = status.ApplyStatusChangeAck(ack);
            if (changed) {
                ClearJobScopedStateIfJobChanged((RoPlayerStatusProperty) ack.StatusId, previousJobId);
            }

            return changed;
        }

        private bool ApplyLocalActorJobSpriteChange(RoZcSpriteChange change, uint localActorId) {
            if (localActorId == 0 ||
                change.Gid != localActorId ||
                change.Type != RoMapActorSpriteChangeType.Job) {
                return false;
            }

            int previousJobId = status.JobId;
            bool hadKnownJobId = status.HasKnownJobId;
            status.ApplyJobId(ClampInt(change.Value));
            ClearJobScopedStateIfJobChanged(RoPlayerStatusProperty.Job, previousJobId);
            return !hadKnownJobId || status.JobId != previousJobId;
        }

        private void ClearJobScopedStateIfJobChanged(RoPlayerStatusProperty property, int previousJobId) {
            if (property != RoPlayerStatusProperty.Job || status.JobId == previousJobId) {
                return;
            }

            skillsById.Clear();
            hasKnownSkillList = true;
            ClearSkillShortcuts();
        }

        private bool ClearSkillShortcuts() {
            if (shortcutKeys == null || shortcutKeys.Length == 0) {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < shortcutKeys.Length; i++) {
                if (!shortcutKeys[i].IsSkill) {
                    continue;
                }

                shortcutKeys[i] = default;
                changed = true;
            }

            return changed;
        }

        private bool RemoveUnavailableSkillShortcuts() {
            if (!hasKnownSkillList || shortcutKeys == null || shortcutKeys.Length == 0) {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < shortcutKeys.Length; i++) {
                RoShortcutKeyEntry shortcut = shortcutKeys[i];
                if (!shortcut.IsSkill) {
                    continue;
                }

                int skillId = unchecked((int) shortcut.Id);
                if (skillsById.TryGetValue(skillId, out RoGameplaySkillInfo skill) &&
                    ResolveKnownSkillLevel(skill) > 0) {
                    continue;
                }

                shortcutKeys[i] = default;
                changed = true;
            }

            return changed;
        }

        private static int ResolveKnownSkillLevel(RoGameplaySkillInfo skill) {
            return skill.Level > 0 ? skill.Level : skill.Level2;
        }

        private static int ClampInt(uint value) {
            return value > int.MaxValue ? int.MaxValue : unchecked((int) value);
        }

        private void ApplyWearEquipmentAck(RoZcWearEquipmentAck ack) {
            if (!ack.IsSuccess || !inventoryByBagIndex.TryGetValue(ack.BagIndex, out RoItemInstance item) || item == null) {
                return;
            }

            var wearState = unchecked((RoItemEquipLocation) ack.WearLocationRaw);
            inventoryByBagIndex[ack.BagIndex] = item.WithWearState(wearState);
        }

        private void ApplyTakeoffEquipmentAck(RoZcTakeoffEquipmentAck ack) {
            if (!ack.IsSuccess || !inventoryByBagIndex.TryGetValue(ack.BagIndex, out RoItemInstance item) || item == null) {
                return;
            }

            inventoryByBagIndex[ack.BagIndex] = item.WithWearState(RoItemEquipLocation.None);
        }

        private void ApplyEquipArrow(RoZcEquipArrow arrow) {
            var previousAmmoIndexes = new List<int>();
            foreach (KeyValuePair<int, RoItemInstance> entry in inventoryByBagIndex) {
                RoItemInstance item = entry.Value;
                if (item != null && (item.WearState & RoItemEquipLocation.Ammo) != 0) {
                    previousAmmoIndexes.Add(entry.Key);
                }
            }

            for (int i = 0; i < previousAmmoIndexes.Count; i++) {
                int index = previousAmmoIndexes[i];
                if (inventoryByBagIndex.TryGetValue(index, out RoItemInstance item) && item != null) {
                    inventoryByBagIndex[index] = item.WithWearState(item.WearState & ~RoItemEquipLocation.Ammo);
                }
            }

            if (arrow.BagIndex <= 0 ||
                !inventoryByBagIndex.TryGetValue(arrow.BagIndex, out RoItemInstance equipped) ||
                equipped == null ||
                equipped.Count <= 0) {
                return;
            }

            inventoryByBagIndex[arrow.BagIndex] = equipped.WithWearState(RoItemEquipLocation.Ammo);
        }

        private void ApplyItemThrowAck(RoZcItemThrowAck ack) {
            if (!ack.IsSuccess || !inventoryByBagIndex.TryGetValue(ack.BagIndex, out RoItemInstance item) || item == null) {
                return;
            }

            thrownByBagIndex[ack.BagIndex] = item;

            int count = Math.Max(0, item.Count - ack.Count);
            if (count <= 0) {
                inventoryByBagIndex.Remove(ack.BagIndex);
                return;
            }

            inventoryByBagIndex[ack.BagIndex] = item.WithCount(count);
        }

        private void ApplyItemPickupAck(RoZcItemPickupAck ack) {
            if (!ack.IsSuccess || ack.BagIndex <= 0 || ack.ItemId == 0 || ack.Count == 0) {
                return;
            }

            int pickupCount = Math.Max(1, (int) ack.Count);
            if (inventoryByBagIndex.TryGetValue(ack.BagIndex, out RoItemInstance existing) &&
                existing != null &&
                existing.ItemId == unchecked((int) ack.ItemId) &&
                !existing.IsUnique) {
                inventoryByBagIndex[ack.BagIndex] = existing.WithCount(existing.Count + pickupCount);
                return;
            }

            inventoryByBagIndex[ack.BagIndex] = ConvertItemPickupAck(ack);
        }

        private void ApplyInventoryItemDelete(RoZcInventoryItemDelete delete) {
            int bagIndex = delete.BagIndex;
            if (bagIndex <= 0 || !inventoryByBagIndex.TryGetValue(bagIndex, out RoItemInstance item) || item == null) {
                return;
            }

            int remaining = Math.Max(0, item.Count - delete.Amount);
            if (remaining <= 0) {
                inventoryByBagIndex.Remove(bagIndex);
                return;
            }

            inventoryByBagIndex[bagIndex] = item.WithCount(remaining);
        }

        private void ApplyUseItemAck(RoZcUseItemAck ack, uint localActorId) {
            int bagIndex = ack.BagIndex;
            if (!ack.IsSuccess ||
                bagIndex <= 0 ||
                (ack.ActorId != 0 && localActorId != 0 && ack.ActorId != localActorId) ||
                !inventoryByBagIndex.TryGetValue(bagIndex, out RoItemInstance item) ||
                item == null) {
                return;
            }

            int remaining = Math.Max(0, (int) ack.Count);
            if (remaining <= 0) {
                inventoryByBagIndex.Remove(bagIndex);
                return;
            }

            inventoryByBagIndex[bagIndex] = item.WithCount(remaining);
        }

        private void ApplyItemCompositionList(RoZcItemCompositionList list) {
            if (pendingCardCompositionCardIndex <= 0 ||
                !inventoryByBagIndex.TryGetValue(pendingCardCompositionCardIndex, out RoItemInstance card) ||
                card == null) {
                pendingCardCompositionCardIndex = 0;
                pendingCardCompositionTargetIndexes = Array.Empty<int>();
                cardCompositionSequence++;
                return;
            }

            pendingCardCompositionTargetIndexes = CopyPositiveIndexes(list.EquipIndexes);
            cardCompositionSequence++;
        }

        private void ApplyMakingArrowList(RoZcMakingArrowList list) {
            pendingMakingArrowItemIds = CopyPositiveItemIds(list.ItemIds);
            makingArrowSequence++;
        }

        private void ApplyItemIdentifyList(RoZcItemIdentifyList list) {
            pendingItemIdentifyIndexes = CopyPositiveIndexes(list.BagIndexes);
            itemIdentifySequence++;
        }

        private void ApplyTeleportWarpList(RoZcWarpList list) {
            string[] mapNames = CopyNonEmptyStrings(list.MapNames);
            if (list.SkillId == 0 || mapNames.Length == 0) {
                return;
            }

            teleportMenuSkillId = list.SkillId;
            teleportMenuMapNames = mapNames;
            teleportMenuSequence++;
        }

        private void ApplyItemCompositionAck(RoZcItemCompositionAck ack) {
            if (ack.IsSuccess) {
                ApplySuccessfulItemComposition(ack.CardIndex, ack.EquipIndex);
            }

            pendingCardCompositionCardIndex = 0;
            pendingCardCompositionTargetIndexes = Array.Empty<int>();
            cardCompositionSequence++;
        }

        private void ApplyItemIdentifyAck(RoZcItemIdentifyAck ack) {
            if (ack.IsSuccess) {
                int bagIndex = ack.BagIndex;
                if (inventoryByBagIndex.TryGetValue(bagIndex, out RoItemInstance item) &&
                    item != null) {
                    inventoryByBagIndex[bagIndex] = item.WithIdentification(true);
                }
            }

            pendingItemIdentifyIndexes = Array.Empty<int>();
            itemIdentifySequence++;
        }

        private void ApplySuccessfulItemComposition(ushort cardIndexRaw, ushort equipIndexRaw) {
            int cardIndex = cardIndexRaw;
            int equipIndex = equipIndexRaw;
            if (!inventoryByBagIndex.TryGetValue(cardIndex, out RoItemInstance card) ||
                card == null ||
                !inventoryByBagIndex.TryGetValue(equipIndex, out RoItemInstance equipment) ||
                equipment == null) {
                return;
            }

            int[] cardIds = equipment.CopyCardIds();
            int slot = -1;
            for (int i = 0; i < cardIds.Length; i++) {
                if (cardIds[i] == 0) {
                    slot = i;
                    break;
                }
            }

            if (slot < 0) {
                return;
            }

            cardIds[slot] = card.ItemId;
            inventoryByBagIndex[equipIndex] = equipment.WithItemOptions(
                equipment.IsDamaged,
                equipment.RefineLevel,
                cardIds,
                equipment.CopyRandomOptions(),
                equipment.EnchantGrade);

            int remaining = Math.Max(0, card.Count - 1);
            if (remaining <= 0) {
                inventoryByBagIndex.Remove(cardIndex);
            } else {
                inventoryByBagIndex[cardIndex] = card.WithCount(remaining);
            }
        }

        private static RoShortcutKeyEntry[] Copy(RoShortcutKeyEntry[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoShortcutKeyEntry>();
            }

            var copy = new RoShortcutKeyEntry[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static RoZcEquipSwitchListEntry[] Copy(RoZcEquipSwitchListEntry[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoZcEquipSwitchListEntry>();
            }

            var copy = new RoZcEquipSwitchListEntry[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        internal static RoItemInstance ConvertInventoryItem(RoZcInventoryItem source) {
            int itemId = unchecked((int) source.ItemId);
            int count = Math.Max(0, (int) source.Count);
            var equipLocation = unchecked((RoItemEquipLocation) source.EquipLocationRaw);
            var wearState = unchecked((RoItemEquipLocation) source.WearStateRaw);
            int[] cards = CopyCardIds(source.CardIds);
            if (source.IsEquipment) {
                return RoItemInstance.CreateEquipment(
                    source.BagIndex,
                    itemId,
                    count: Math.Max(1, count),
                    source.IsIdentified,
                    equipLocation,
                    wearState,
                    source.RefineLevel,
                    cards,
                    RoUniqueItemFlags.None,
                    source.ViewIdHint,
                    source.ItemTypeRaw,
                    source.Flag,
                    enchantGrade: source.EnchantGrade,
                    isDamaged: source.IsDamaged,
                    randomOptions: CopyRandomOptions(source.RandomOptions));
            }

            return HasAnyCard(cards)
                ? RoItemInstance.CreateUnique(
                    source.BagIndex,
                    itemId,
                    count,
                    source.IsIdentified,
                    equipLocation,
                    RoItemEquipLocation.None,
                    refineLevel: 0,
                    cards,
                    RoUniqueItemFlags.None,
                    Guid.Empty,
                    itemTypeRaw: source.ItemTypeRaw,
                    inventoryFlagRaw: source.Flag,
                    isDamaged: source.IsDamaged,
                    randomOptions: CopyRandomOptions(source.RandomOptions))
                : RoItemInstance.CreateRegular(
                    source.BagIndex,
                    itemId,
                    count,
                    source.IsIdentified,
                    equipLocation,
                    itemTypeRaw: source.ItemTypeRaw,
                    inventoryFlagRaw: source.Flag,
                    isDamaged: source.IsDamaged,
                    randomOptions: CopyRandomOptions(source.RandomOptions));
        }

        private static RoItemInstance ConvertItemPickupAck(RoZcItemPickupAck source) {
            int itemId = unchecked((int) source.ItemId);
            int count = Math.Max(1, (int) source.Count);
            var equipLocation = unchecked((RoItemEquipLocation) source.EquipLocationRaw);
            int[] cards = CopyCardIds(source.CardIds);
            if (equipLocation != RoItemEquipLocation.None || source.ViewIdHint > 0 || source.RefineLevel > 0) {
                return RoItemInstance.CreateEquipment(
                    source.BagIndex,
                    itemId,
                    count,
                    source.IsIdentified,
                    equipLocation,
                    RoItemEquipLocation.None,
                    source.RefineLevel,
                    cards,
                    RoUniqueItemFlags.None,
                    source.ViewIdHint,
                    source.ItemTypeRaw,
                    source.IsIdentified ? (byte) 0x01 : (byte) 0x00,
                    source.Favorite,
                    isDamaged: source.IsDamaged,
                    hireExpireDate: source.HireExpireDate);
            }

            return HasAnyCard(cards)
                ? RoItemInstance.CreateUnique(
                    source.BagIndex,
                    itemId,
                    count,
                    source.IsIdentified,
                    equipLocation,
                    RoItemEquipLocation.None,
                    source.RefineLevel,
                    cards,
                    RoUniqueItemFlags.None,
                    Guid.Empty,
                    source.ViewIdHint,
                    source.ItemTypeRaw,
                    source.IsIdentified ? (byte) 0x01 : (byte) 0x00,
                    source.Favorite,
                    isDamaged: source.IsDamaged,
                    hireExpireDate: source.HireExpireDate)
                : RoItemInstance.CreateRegular(
                    source.BagIndex,
                    itemId,
                    count,
                    source.IsIdentified,
                    equipLocation,
                    itemTypeRaw: source.ItemTypeRaw,
                    inventoryFlagRaw: source.IsIdentified ? (byte) 0x01 : (byte) 0x00,
                    favoriteRaw: source.Favorite,
                    isDamaged: source.IsDamaged,
                    hireExpireDate: source.HireExpireDate);
        }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[RoItemInstance.MaxCardSlots];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }

        private static int[] CopyPositiveIndexes(int[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<int>();
            }

            var filtered = new List<int>(source.Length);
            for (int i = 0; i < source.Length; i++) {
                if (source[i] > 0) {
                    filtered.Add(source[i]);
                }
            }

            return filtered.Count == 0 ? Array.Empty<int>() : filtered.ToArray();
        }

        private static uint[] CopyPositiveItemIds(uint[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<uint>();
            }

            var filtered = new List<uint>(source.Length);
            for (int i = 0; i < source.Length; i++) {
                if (source[i] > 0) {
                    filtered.Add(source[i]);
                }
            }

            return filtered.Count == 0 ? Array.Empty<uint>() : filtered.ToArray();
        }

        private static string[] CopyNonEmptyStrings(string[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<string>();
            }

            var filtered = new List<string>(source.Length);
            for (int i = 0; i < source.Length; i++) {
                string value = source[i];
                if (!string.IsNullOrWhiteSpace(value)) {
                    filtered.Add(value.Trim());
                }
            }

            return filtered.Count == 0 ? Array.Empty<string>() : filtered.ToArray();
        }

        private static RoItemRandomOption[] CopyRandomOptions(RoPacketItemRandomOption[] source) {
            var copy = new RoItemRandomOption[RoItemInstance.MaxRandomOptions];
            if (source == null) {
                return copy;
            }

            int count = Math.Min(source.Length, copy.Length);
            for (int i = 0; i < count; i++) {
                RoPacketItemRandomOption option = source[i];
                copy[i] = new RoItemRandomOption(option.Index, option.Value, option.Param);
            }

            return copy;
        }

        private static bool HasAnyCard(int[] cards) {
            if (cards == null) {
                return false;
            }

            for (int i = 0; i < cards.Length; i++) {
                if (cards[i] != 0) {
                    return true;
                }
            }

            return false;
        }

        private static void SplitMenuChoices(string menuText, List<string> choices) {
            if (choices == null) {
                return;
            }

            if (string.IsNullOrWhiteSpace(menuText)) {
                return;
            }

            string[] rawChoices = menuText.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < rawChoices.Length; i++) {
                string choice = rawChoices[i].Trim();
                if (choice.Length > 0) {
                    choices.Add(choice);
                }
            }
        }

        private static RoNpcShopBuyItem[] Copy(RoNpcShopBuyItem[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoNpcShopBuyItem>();
            }

            var copy = new RoNpcShopBuyItem[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static RoNpcShopSellItem[] Copy(RoNpcShopSellItem[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoNpcShopSellItem>();
            }

            var copy = new RoNpcShopSellItem[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static RoNpcMarketItem[] Copy(RoNpcMarketItem[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoNpcMarketItem>();
            }

            var copy = new RoNpcMarketItem[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static RoNpcMarketResultItem[] Copy(RoNpcMarketResultItem[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoNpcMarketResultItem>();
            }

            var copy = new RoNpcMarketResultItem[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static RoNpcBarterItem[] Copy(RoNpcBarterItem[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoNpcBarterItem>();
            }

            var copy = new RoNpcBarterItem[source.Length];
            for (int i = 0; i < source.Length; i++) {
                RoNpcBarterItem item = source[i];
                copy[i] = new RoNpcBarterItem(
                    item.ItemId,
                    item.ItemTypeRaw,
                    item.Amount,
                    item.Stock,
                    item.Weight,
                    item.ShopIndex,
                    item.Location,
                    item.Price,
                    item.ViewSprite,
                    Copy(item.Currencies));
            }

            return copy;
        }

        private static RoNpcBarterCurrency[] Copy(RoNpcBarterCurrency[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoNpcBarterCurrency>();
            }

            var copy = new RoNpcBarterCurrency[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static RoItemInstance ConvertStorageItem(RoZcStorageItem source) {
            int itemId = unchecked((int) source.ItemId);
            int count = Math.Max(0, source.Amount);
            int[] cards = CopyCardIds(source.CardIds);
            bool isEquipment = IsEquipmentItemType(source.ItemTypeRaw) || source.RefineLevel > 0 || HasAnyCard(cards);
            if (isEquipment) {
                return RoItemInstance.CreateEquipment(
                    source.StorageIndex,
                    itemId,
                    count: Math.Max(1, count),
                    source.IsIdentified,
                    RoItemEquipLocation.None,
                    RoItemEquipLocation.None,
                    source.RefineLevel,
                    cards,
                    itemTypeRaw: source.ItemTypeRaw,
                    inventoryFlagRaw: source.IsIdentified ? (byte) 0x01 : (byte) 0x00,
                    enchantGrade: source.EnchantGrade,
                    isDamaged: source.IsDamaged,
                    randomOptions: CopyRandomOptions(source.RandomOptions));
            }

            return RoItemInstance.CreateRegular(
                source.StorageIndex,
                itemId,
                count,
                source.IsIdentified,
                RoItemEquipLocation.None,
                itemTypeRaw: source.ItemTypeRaw,
                inventoryFlagRaw: source.IsIdentified ? (byte) 0x01 : (byte) 0x00,
                isDamaged: source.IsDamaged,
                randomOptions: CopyRandomOptions(source.RandomOptions));
        }

        private static bool IsEquipmentItemType(byte itemTypeRaw) {
            return itemTypeRaw == (byte) RoItemType.Weapon ||
                itemTypeRaw == (byte) RoItemType.Armor ||
                itemTypeRaw == (byte) RoItemType.PetArmor ||
                itemTypeRaw == (byte) RoItemType.ShadowGear;
        }

        private static bool IsStorageInventoryType(byte inventoryTypeRaw) {
            return inventoryTypeRaw == (byte) RoInventoryType.Storage ||
                inventoryTypeRaw == (byte) RoInventoryType.GuildStorage;
        }

        private static int SaturatingAdd(int left, int right) {
            long sum = (long) Math.Max(0, left) + Math.Max(0, right);
            return sum > int.MaxValue ? int.MaxValue : (int) sum;
        }
    }

    internal sealed class RoMutablePlayerStatus {
        public int BaseLevel { get; private set; }
        public int JobLevel { get; private set; }
        public int JobId { get; private set; }
        public bool HasKnownJobId { get; private set; }
        public long BaseExperience { get; private set; }
        public long NextBaseExperience { get; private set; }
        public long JobExperience { get; private set; }
        public long NextJobExperience { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int Sp { get; private set; }
        public int MaxSp { get; private set; }
        public int StatusPoints { get; private set; }
        public bool HasKnownStatusPoints { get; private set; }
        public int SkillPoints { get; private set; }
        public long Zeny { get; private set; }
        public long Weight { get; private set; }
        public long MaxWeight { get; private set; }
        public int WeightPercentage { get; set; } = -1;
        public int Str { get; private set; }
        public int StrBonus { get; private set; }
        public int NeedStr { get; private set; }
        public int Agi { get; private set; }
        public int AgiBonus { get; private set; }
        public int NeedAgi { get; private set; }
        public int Vit { get; private set; }
        public int VitBonus { get; private set; }
        public int NeedVit { get; private set; }
        public int Int { get; private set; }
        public int IntBonus { get; private set; }
        public int NeedInt { get; private set; }
        public int Dex { get; private set; }
        public int DexBonus { get; private set; }
        public int NeedDex { get; private set; }
        public int Luk { get; private set; }
        public int LukBonus { get; private set; }
        public int NeedLuk { get; private set; }
        public int Attack { get; private set; }
        public int RefiningPower { get; private set; }
        public int MagicAttackMax { get; private set; }
        public int MagicAttackMin { get; private set; }
        public int Defense { get; private set; }
        public int PlusDefense { get; private set; }
        public int MagicDefense { get; private set; }
        public int PlusMagicDefense { get; private set; }
        public int Hit { get; private set; }
        public int Flee { get; private set; }
        public int PlusFlee { get; private set; }
        public int Critical { get; private set; }
        public int MovementSpeed { get; private set; }
        public int AttackSpeed { get; private set; }
        public int PlusAttackSpeed { get; private set; }
        public int AttackRange { get; private set; }

        public void Clear() {
            BaseLevel = 0;
            JobLevel = 0;
            JobId = 0;
            HasKnownJobId = false;
            BaseExperience = 0;
            NextBaseExperience = 0;
            JobExperience = 0;
            NextJobExperience = 0;
            Hp = 0;
            MaxHp = 0;
            Sp = 0;
            MaxSp = 0;
            StatusPoints = 0;
            HasKnownStatusPoints = false;
            SkillPoints = 0;
            Zeny = 0;
            Weight = 0;
            MaxWeight = 0;
            WeightPercentage = -1;
            Str = 0;
            StrBonus = 0;
            NeedStr = 0;
            Agi = 0;
            AgiBonus = 0;
            NeedAgi = 0;
            Vit = 0;
            VitBonus = 0;
            NeedVit = 0;
            Int = 0;
            IntBonus = 0;
            NeedInt = 0;
            Dex = 0;
            DexBonus = 0;
            NeedDex = 0;
            Luk = 0;
            LukBonus = 0;
            NeedLuk = 0;
            Attack = 0;
            RefiningPower = 0;
            MagicAttackMax = 0;
            MagicAttackMin = 0;
            Defense = 0;
            PlusDefense = 0;
            MagicDefense = 0;
            PlusMagicDefense = 0;
            Hit = 0;
            Flee = 0;
            PlusFlee = 0;
            Critical = 0;
            MovementSpeed = 0;
            AttackSpeed = 0;
            PlusAttackSpeed = 0;
            AttackRange = 0;
        }

        public void ApplyCharacter(RoCharacterInfo character, bool preserveKnownVitals = false) {
            if (character == null) {
                return;
            }

            // A map move on the same map server carries the char-list snapshot from the login, and the server resends
            // neither level, exp, zeny nor job: the live values (level-ups, a job change, trades) are newer, keep them all.
            if (preserveKnownVitals && BaseLevel > 0) {
                return;
            }

            int previousHp = Hp;
            int previousMaxHp = MaxHp;
            int previousSp = Sp;
            int previousMaxSp = MaxSp;
            BaseLevel = character.BaseLevel;
            JobLevel = character.JobLevel;
            JobId = character.JobId;
            HasKnownJobId = true;
            BaseExperience = character.Experience;
            JobExperience = character.JobExperience;
            Hp = character.Hp;
            MaxHp = character.MaxHp;
            Sp = character.Sp;
            MaxSp = character.MaxSp;
            Zeny = character.Money;
            MovementSpeed = ToMovementSpeed(character.Speed);

            if (preserveKnownVitals && previousMaxHp > 0) {
                Hp = previousHp;
                MaxHp = previousMaxHp;
                Sp = previousSp;
                MaxSp = previousMaxSp;
            }
        }

        public void ApplyParameter(RoZcParameterChange change) {
            ApplyParameter(change.Property, change.Value);
        }

        public void SetZeny(long value) {
            Zeny = Math.Max(0, value);
        }

        public void ApplyStatusPropertyChange(RoZcStatusPropertyChange change) {
            ApplyParameter(change.Property, change.Value);
        }

        public void ApplyJobId(int jobId) {
            JobId = jobId;
            HasKnownJobId = true;
        }

        public bool ApplyStatusChangeAck(RoZcStatusChangeAck ack) {
            if (ack.Result == 0) {
                return false;
            }

            RoPlayerStatusProperty property = (RoPlayerStatusProperty) ack.StatusId;
            int previousValue = GetBaseStatValue(property);
            ApplyParameter(property, ack.Value);
            ApplyStatusPointCost(property, previousValue, ack.Value);
            return true;
        }

        public void ApplyStatusBaseBonus(RoZcCoupleStatus statusBaseBonus) {
            switch (statusBaseBonus.Property) {
                case RoPlayerStatusProperty.Str:
                    Str = statusBaseBonus.DefaultStatus;
                    StrBonus = statusBaseBonus.PlusStatus;
                    break;
                case RoPlayerStatusProperty.Agi:
                    Agi = statusBaseBonus.DefaultStatus;
                    AgiBonus = statusBaseBonus.PlusStatus;
                    break;
                case RoPlayerStatusProperty.Vit:
                    Vit = statusBaseBonus.DefaultStatus;
                    VitBonus = statusBaseBonus.PlusStatus;
                    break;
                case RoPlayerStatusProperty.Int:
                    Int = statusBaseBonus.DefaultStatus;
                    IntBonus = statusBaseBonus.PlusStatus;
                    break;
                case RoPlayerStatusProperty.Dex:
                    Dex = statusBaseBonus.DefaultStatus;
                    DexBonus = statusBaseBonus.PlusStatus;
                    break;
                case RoPlayerStatusProperty.Luk:
                    Luk = statusBaseBonus.DefaultStatus;
                    LukBonus = statusBaseBonus.PlusStatus;
                    break;
            }
        }

        public void ApplyStatusSnapshot(RoZcStatusSnapshot snapshot) {
            StatusPoints = snapshot.StatusPoints;
            HasKnownStatusPoints = true;
            Str = snapshot.Str;
            NeedStr = snapshot.NeedStr;
            Agi = snapshot.Agi;
            NeedAgi = snapshot.NeedAgi;
            Vit = snapshot.Vit;
            NeedVit = snapshot.NeedVit;
            Int = snapshot.Int;
            NeedInt = snapshot.NeedInt;
            Dex = snapshot.Dex;
            NeedDex = snapshot.NeedDex;
            Luk = snapshot.Luk;
            NeedLuk = snapshot.NeedLuk;
            Attack = snapshot.Attack;
            RefiningPower = snapshot.RefiningPower;
            MagicAttackMax = snapshot.MagicAttackMax;
            MagicAttackMin = snapshot.MagicAttackMin;
            Defense = snapshot.Defense;
            PlusDefense = snapshot.PlusDefense;
            MagicDefense = snapshot.MagicDefense;
            PlusMagicDefense = snapshot.PlusMagicDefense;
            Hit = snapshot.Hit;
            Flee = snapshot.Flee;
            PlusFlee = snapshot.PlusFlee;
            Critical = snapshot.Critical;
            AttackSpeed = snapshot.AttackSpeed;
            PlusAttackSpeed = snapshot.PlusAttackSpeed;
        }

        public void ApplyAttackRange(RoZcAttackRange attackRange) {
            AttackRange = Math.Max(0, (int) attackRange.CurrentAttackRange);
        }

        public RoPlayerStatusSnapshot ToSnapshot() {
            return new RoPlayerStatusSnapshot(this);
        }

        private void ApplyParameter(RoPlayerStatusProperty property, long value) {
            switch (property) {
                case RoPlayerStatusProperty.BaseExperience:
                    BaseExperience = value;
                    break;
                case RoPlayerStatusProperty.JobExperience:
                    JobExperience = value;
                    break;
                case RoPlayerStatusProperty.Hp:
                    Hp = ToInt(value);
                    break;
                case RoPlayerStatusProperty.MaxHp:
                    MaxHp = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Sp:
                    Sp = ToInt(value);
                    break;
                case RoPlayerStatusProperty.MaxSp:
                    MaxSp = ToInt(value);
                    break;
                case RoPlayerStatusProperty.StatusPoints:
                    StatusPoints = ToInt(value);
                    HasKnownStatusPoints = true;
                    break;
                case RoPlayerStatusProperty.BaseLevel:
                    BaseLevel = ToInt(value);
                    break;
                case RoPlayerStatusProperty.SkillPoints:
                    SkillPoints = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Str:
                    Str = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Agi:
                    Agi = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Vit:
                    Vit = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Int:
                    Int = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Dex:
                    Dex = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Luk:
                    Luk = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Job:
                    JobId = ToInt(value);
                    HasKnownJobId = true;
                    break;
                case RoPlayerStatusProperty.Zeny:
                    Zeny = value;
                    break;
                case RoPlayerStatusProperty.NextBaseExperience:
                    NextBaseExperience = value;
                    break;
                case RoPlayerStatusProperty.NextJobExperience:
                    NextJobExperience = value;
                    break;
                case RoPlayerStatusProperty.Weight:
                    Weight = value;
                    break;
                case RoPlayerStatusProperty.MaxWeight:
                    MaxWeight = value;
                    break;
                case RoPlayerStatusProperty.NeedStr:
                    NeedStr = ToInt(value);
                    break;
                case RoPlayerStatusProperty.NeedAgi:
                    NeedAgi = ToInt(value);
                    break;
                case RoPlayerStatusProperty.NeedVit:
                    NeedVit = ToInt(value);
                    break;
                case RoPlayerStatusProperty.NeedInt:
                    NeedInt = ToInt(value);
                    break;
                case RoPlayerStatusProperty.NeedDex:
                    NeedDex = ToInt(value);
                    break;
                case RoPlayerStatusProperty.NeedLuk:
                    NeedLuk = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Attack:
                    Attack = ToInt(value);
                    break;
                case RoPlayerStatusProperty.RefiningPower:
                    RefiningPower = ToInt(value);
                    break;
                case RoPlayerStatusProperty.MagicAttackMax:
                    MagicAttackMax = ToInt(value);
                    break;
                case RoPlayerStatusProperty.MagicAttackMin:
                    MagicAttackMin = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Defense:
                    Defense = ToInt(value);
                    break;
                case RoPlayerStatusProperty.PlusDefense:
                    PlusDefense = ToInt(value);
                    break;
                case RoPlayerStatusProperty.MagicDefense:
                    MagicDefense = ToInt(value);
                    break;
                case RoPlayerStatusProperty.PlusMagicDefense:
                    PlusMagicDefense = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Hit:
                    Hit = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Flee:
                    Flee = ToInt(value);
                    break;
                case RoPlayerStatusProperty.PlusFlee:
                    PlusFlee = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Critical:
                    Critical = ToInt(value);
                    break;
                case RoPlayerStatusProperty.Speed:
                    MovementSpeed = ToMovementSpeed(value);
                    break;
                case RoPlayerStatusProperty.AttackSpeed:
                    AttackSpeed = ToInt(value);
                    break;
                case RoPlayerStatusProperty.PlusAttackSpeed:
                    PlusAttackSpeed = ToInt(value);
                    break;
                case RoPlayerStatusProperty.JobLevel:
                    JobLevel = ToInt(value);
                    break;
            }
        }

        private void ApplyStatusPointCost(RoPlayerStatusProperty property, int previousValue, int nextValue) {
            if (!HasKnownStatusPoints || previousValue <= 0 || nextValue <= previousValue || !IsBaseStatProperty(property)) {
                return;
            }

            StatusPoints = Math.Max(0, StatusPoints - ResolveStatusPointCost(property, previousValue));
        }

        private int ResolveStatusPointCost(RoPlayerStatusProperty property, int statValue) {
            int serverRequirement = GetStatusPointRequirement(property);
            return serverRequirement > 0 ? serverRequirement : CalculateStatusPointCost(statValue);
        }

        private int GetBaseStatValue(RoPlayerStatusProperty property) {
            switch (property) {
                case RoPlayerStatusProperty.Str:
                    return Str;
                case RoPlayerStatusProperty.Agi:
                    return Agi;
                case RoPlayerStatusProperty.Vit:
                    return Vit;
                case RoPlayerStatusProperty.Int:
                    return Int;
                case RoPlayerStatusProperty.Dex:
                    return Dex;
                case RoPlayerStatusProperty.Luk:
                    return Luk;
                default:
                    return 0;
            }
        }

        private int GetStatusPointRequirement(RoPlayerStatusProperty property) {
            switch (property) {
                case RoPlayerStatusProperty.Str:
                    return NeedStr;
                case RoPlayerStatusProperty.Agi:
                    return NeedAgi;
                case RoPlayerStatusProperty.Vit:
                    return NeedVit;
                case RoPlayerStatusProperty.Int:
                    return NeedInt;
                case RoPlayerStatusProperty.Dex:
                    return NeedDex;
                case RoPlayerStatusProperty.Luk:
                    return NeedLuk;
                default:
                    return 0;
            }
        }

        private static bool IsBaseStatProperty(RoPlayerStatusProperty property) {
            switch (property) {
                case RoPlayerStatusProperty.Str:
                case RoPlayerStatusProperty.Agi:
                case RoPlayerStatusProperty.Vit:
                case RoPlayerStatusProperty.Int:
                case RoPlayerStatusProperty.Dex:
                case RoPlayerStatusProperty.Luk:
                    return true;
                default:
                    return false;
            }
        }

        private static int CalculateStatusPointCost(int statValue) {
            if (statValue <= 0) {
                return 0;
            }

            return ((Math.Max(1, statValue) - 1) / 10) + 2;
        }

        private static int ToInt(long value) {
            if (value > int.MaxValue) {
                return int.MaxValue;
            }

            if (value < int.MinValue) {
                return int.MinValue;
            }

            return (int) value;
        }

        private static int ToMovementSpeed(long value) {
            if (value <= 0) {
                return 0;
            }

            return value > short.MaxValue ? short.MaxValue : (int) value;
        }
    }
}
