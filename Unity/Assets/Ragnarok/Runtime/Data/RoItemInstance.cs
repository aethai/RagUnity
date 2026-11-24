using System;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Data {
    public enum RoItemInstanceKind {
        Unknown = 0,
        Regular = 1,
        Unique = 2
    }

    [Flags]
    public enum RoUniqueItemFlags : byte {
        None = 0,
        CraftedItem = 1 << 0
    }

    public sealed class RoItemInstance {
        public const int MaxCardSlots = 4;
        public const int MaxRandomOptions = 5;

        private static readonly string[] GradeNames = { string.Empty, "D", "C", "B", "A" };
        private static readonly string[] CardRepeatPrefixes = { string.Empty, string.Empty, "Double ", "Triple ", "Quadruple " };

        private readonly int[] cardIds;
        private readonly RoItemRandomOption[] randomOptions;

        private RoItemInstance(
            int bagIndex,
            int itemId,
            int count,
            RoItemInstanceKind kind,
            bool isIdentified,
            RoItemEquipLocation equipLocationHint,
            RoItemEquipLocation wearState,
            int refineLevel,
            IReadOnlyList<int> cardIds,
            RoUniqueItemFlags flags,
            Guid uniqueId,
            int viewIdHint,
            byte itemTypeRaw,
            byte inventoryFlagRaw,
            byte favoriteRaw,
            byte enchantGrade,
            bool isDamaged,
            int hireExpireDate,
            IReadOnlyList<RoItemRandomOption> randomOptions
        ) {
            BagIndex = bagIndex;
            ItemId = itemId;
            Count = count;
            Kind = kind;
            IsIdentified = isIdentified;
            EquipLocationHint = equipLocationHint;
            WearState = wearState;
            RefineLevel = Math.Max(0, refineLevel);
            Flags = flags;
            UniqueId = uniqueId;
            ViewIdHint = Math.Max(0, viewIdHint);
            ItemTypeRaw = itemTypeRaw;
            InventoryFlagRaw = inventoryFlagRaw;
            FavoriteRaw = favoriteRaw;
            EnchantGrade = enchantGrade;
            IsDamaged = isDamaged;
            HireExpireDate = Math.Max(0, hireExpireDate);
            this.cardIds = CopyCards(cardIds);
            this.randomOptions = CopyRandomOptions(randomOptions);
        }

        public int BagIndex { get; }
        public int ItemId { get; }
        public int Count { get; }
        public RoItemInstanceKind Kind { get; }
        public bool IsIdentified { get; }
        public RoItemEquipLocation EquipLocationHint { get; }
        public RoItemEquipLocation WearState { get; }
        public int RefineLevel { get; }
        public RoUniqueItemFlags Flags { get; }
        public Guid UniqueId { get; }
        public int ViewIdHint { get; }
        public byte ItemTypeRaw { get; }
        public byte InventoryFlagRaw { get; }
        public byte FavoriteRaw { get; }
        public byte EnchantGrade { get; }
        public bool IsDamaged { get; }
        public int HireExpireDate { get; }
        public bool IsEquipped => WearState != RoItemEquipLocation.None;
        public bool IsUnique => Kind == RoItemInstanceKind.Unique;
        public bool IsCraftedItem => (Flags & RoUniqueItemFlags.CraftedItem) != 0;
        public bool IsFavorite => FavoriteRaw != 0;
        public bool HasRandomOptions {
            get {
                for (int i = 0; i < randomOptions.Length; i++) {
                    if (!randomOptions[i].IsEmpty) {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasAnyCard {
            get {
                for (int i = 0; i < cardIds.Length; i++) {
                    if (cardIds[i] != 0) {
                        return true;
                    }
                }

                return false;
            }
        }

        public static RoItemInstance CreateRegular(
            int bagIndex,
            int itemId,
            int count,
            bool isIdentified = true,
            RoItemEquipLocation equipLocationHint = RoItemEquipLocation.None,
            RoItemEquipLocation wearState = RoItemEquipLocation.None,
            byte itemTypeRaw = 0,
            byte inventoryFlagRaw = 0,
            byte favoriteRaw = 0,
            byte enchantGrade = 0,
            bool isDamaged = false,
            int hireExpireDate = 0,
            IReadOnlyList<RoItemRandomOption> randomOptions = null
        ) {
            return new RoItemInstance(
                bagIndex,
                itemId,
                count,
                RoItemInstanceKind.Regular,
                isIdentified,
                equipLocationHint,
                wearState,
                0,
                null,
                RoUniqueItemFlags.None,
                Guid.Empty,
                0,
                itemTypeRaw,
                inventoryFlagRaw,
                favoriteRaw,
                enchantGrade,
                isDamaged,
                hireExpireDate,
                randomOptions
            );
        }

        public static RoItemInstance CreateEquipment(
            int bagIndex,
            int itemId,
            int count = 1,
            bool isIdentified = true,
            RoItemEquipLocation equipLocationHint = RoItemEquipLocation.None,
            RoItemEquipLocation wearState = RoItemEquipLocation.None,
            int refineLevel = 0,
            IReadOnlyList<int> cardIds = null,
            RoUniqueItemFlags flags = RoUniqueItemFlags.None,
            int viewIdHint = 0,
            byte itemTypeRaw = 0,
            byte inventoryFlagRaw = 0,
            byte favoriteRaw = 0,
            byte enchantGrade = 0,
            bool isDamaged = false,
            int hireExpireDate = 0,
            IReadOnlyList<RoItemRandomOption> randomOptions = null
        ) {
            return new RoItemInstance(
                bagIndex,
                itemId,
                count,
                RoItemInstanceKind.Unique,
                isIdentified,
                equipLocationHint,
                wearState,
                refineLevel,
                cardIds,
                flags,
                Guid.Empty,
                viewIdHint,
                itemTypeRaw,
                inventoryFlagRaw,
                favoriteRaw,
                enchantGrade,
                isDamaged,
                hireExpireDate,
                randomOptions
            );
        }

        public static RoItemInstance CreateUnique(
            int bagIndex,
            int itemId,
            int count,
            bool isIdentified,
            RoItemEquipLocation equipLocationHint,
            RoItemEquipLocation wearState,
            int refineLevel,
            IReadOnlyList<int> cardIds,
            RoUniqueItemFlags flags,
            Guid uniqueId,
            int viewIdHint = 0,
            byte itemTypeRaw = 0,
            byte inventoryFlagRaw = 0,
            byte favoriteRaw = 0,
            byte enchantGrade = 0,
            bool isDamaged = false,
            int hireExpireDate = 0,
            IReadOnlyList<RoItemRandomOption> randomOptions = null
        ) {
            return new RoItemInstance(
                bagIndex,
                itemId,
                count,
                RoItemInstanceKind.Unique,
                isIdentified,
                equipLocationHint,
                wearState,
                refineLevel,
                cardIds,
                flags,
                uniqueId,
                viewIdHint,
                itemTypeRaw,
                inventoryFlagRaw,
                favoriteRaw,
                enchantGrade,
                isDamaged,
                hireExpireDate,
                randomOptions
            );
        }

        public int GetCardId(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= MaxCardSlots) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "RO item instances expose exactly four card/socket fields.");
            }

            return cardIds[slotIndex];
        }

        public int[] CopyCardIds() {
            var copy = new int[MaxCardSlots];
            Array.Copy(cardIds, copy, MaxCardSlots);
            return copy;
        }

        public RoItemRandomOption GetRandomOption(int optionIndex) {
            if (optionIndex < 0 || optionIndex >= MaxRandomOptions) {
                throw new ArgumentOutOfRangeException(nameof(optionIndex), optionIndex, "RO item instances expose exactly five random option fields.");
            }

            return randomOptions[optionIndex];
        }

        public RoItemRandomOption[] CopyRandomOptions() {
            var copy = new RoItemRandomOption[MaxRandomOptions];
            Array.Copy(randomOptions, copy, MaxRandomOptions);
            return copy;
        }

        public RoItemMetadata ResolveMetadata(RoItemMetadataTable table) {
            RoItemMetadataTable source = table ?? RoItemMetadataTable.Empty;
            return source.GetItemOrFallback(ItemId);
        }

        public RoItemEquipLocation ResolveEquipLocations(RoItemMetadata metadata) {
            if (metadata != null && metadata.EquipLocations != RoItemEquipLocation.None) {
                return metadata.EquipLocations;
            }

            return EquipLocationHint;
        }

        public RoItemEquipLocation ResolveEquipLocations(RoItemMetadataTable table) {
            return ResolveEquipLocations(ResolveMetadata(table));
        }

        public int ResolveViewId(RoItemMetadata metadata) {
            if (metadata != null) {
                if (metadata.ClassNumber > 0) {
                    return metadata.ClassNumber;
                }

                if (metadata.ViewId > 0) {
                    return metadata.ViewId;
                }
            }

            return ViewIdHint;
        }

        public int ResolveViewId(RoItemMetadataTable table) {
            return ResolveViewId(ResolveMetadata(table));
        }

        public bool CanEquip(RoItemMetadata metadata) {
            return ResolveEquipLocations(metadata) != RoItemEquipLocation.None || (metadata != null && metadata.IsEquipable);
        }

        public bool CanEquip(RoItemMetadata metadata, RoItemEquipContext context) {
            return CanEquip(metadata) && (metadata == null || metadata.CanEquip(context));
        }

        public bool CanEquipIn(RoItemEquipLocation location, RoItemMetadata metadata) {
            if (location == RoItemEquipLocation.None) {
                return false;
            }

            RoItemEquipLocation allowed = ResolveEquipLocations(metadata);
            return allowed != RoItemEquipLocation.None && (allowed & location) == location;
        }

        public bool IsWearStateAllowed(RoItemMetadata metadata) {
            return WearState == RoItemEquipLocation.None || CanEquipIn(WearState, metadata);
        }

        public string GetDisplayName(RoItemMetadata metadata) {
            if (metadata == null) {
                metadata = RoItemMetadataTable.Empty.GetItemOrFallback(ItemId);
            }

            string name = metadata.GetDisplayName(IsIdentified);
            if (!IsIdentified) {
                return name;
            }

            string prefix = RefineLevel > 0 ? "+" + RefineLevel + " " : string.Empty;
            int slotCount = GetVisibleSlotCount(metadata);
            return slotCount > 0 ? prefix + name + "[" + slotCount + "]" : prefix + name;
        }

        public string GetDisplayName(RoItemMetadataTable table) {
            return GetDisplayName(ResolveMetadata(table));
        }

        public string GetFullDisplayName(RoItemMetadataTable table, bool includeRandomOptionCount = false) {
            RoItemMetadataTable source = table ?? RoItemMetadataTable.Empty;
            return GetFullDisplayName(ResolveMetadata(source), source, includeRandomOptionCount);
        }

        public string GetFullDisplayName(RoItemMetadata metadata, RoItemMetadataTable table, bool includeRandomOptionCount = false) {
            RoItemMetadataTable source = table ?? RoItemMetadataTable.Empty;
            metadata ??= source.GetItemOrFallback(ItemId);
            if (!IsIdentified) {
                return metadata.GetDisplayName(false);
            }

            var builder = new StringBuilder();
            if (RefineLevel > 0) {
                builder.Append('+').Append(RefineLevel).Append(' ');
            }

            if (EnchantGrade > 0 && EnchantGrade < GradeNames.Length) {
                builder.Append('[').Append(GradeNames[EnchantGrade]).Append("] ");
            }

            AppendCardAffix(builder, source, postfix: false);
            builder.Append(metadata.GetDisplayName(true));
            AppendCardAffix(builder, source, postfix: true);

            int slotCount = GetVisibleSlotCount(metadata);
            if (slotCount > 0) {
                builder.Append(" [").Append(slotCount).Append(']');
            }

            if (includeRandomOptionCount && HasRandomOptions) {
                int count = 0;
                for (int i = 0; i < MaxRandomOptions; i++) {
                    if (!GetRandomOption(i).IsEmpty) {
                        count++;
                    }
                }

                if (count > 0) {
                    builder.Append(" [").Append(count).Append(" Option]");
                }
            }

            return builder.ToString();
        }

        public string GetInventoryIconPath(RoItemMetadata metadata) {
            if (metadata == null) {
                metadata = RoItemMetadataTable.Empty.GetItemOrFallback(ItemId);
            }

            return metadata.GetInventoryIconPath(IsIdentified);
        }

        public string GetInventoryIconPath(RoItemMetadataTable table) {
            return GetInventoryIconPath(ResolveMetadata(table));
        }

        public string GetWorldSpriteBasePath(RoItemMetadata metadata) {
            if (metadata == null) {
                metadata = RoItemMetadataTable.Empty.GetItemOrFallback(ItemId);
            }

            return metadata.GetWorldSpriteBasePath(IsIdentified);
        }

        public string GetWorldSpriteBasePath(RoItemMetadataTable table) {
            return GetWorldSpriteBasePath(ResolveMetadata(table));
        }

        public RoItemInstance WithWearState(RoItemEquipLocation wearState) {
            return new RoItemInstance(
                BagIndex,
                ItemId,
                Count,
                Kind,
                IsIdentified,
                EquipLocationHint,
                wearState,
                RefineLevel,
                cardIds,
                Flags,
                UniqueId,
                ViewIdHint,
                ItemTypeRaw,
                InventoryFlagRaw,
                FavoriteRaw,
                EnchantGrade,
                IsDamaged,
                HireExpireDate,
                randomOptions
            );
        }

        public RoItemInstance WithCount(int count) {
            return new RoItemInstance(
                BagIndex,
                ItemId,
                count,
                Kind,
                IsIdentified,
                EquipLocationHint,
                WearState,
                RefineLevel,
                cardIds,
                Flags,
                UniqueId,
                ViewIdHint,
                ItemTypeRaw,
                InventoryFlagRaw,
                FavoriteRaw,
                EnchantGrade,
                IsDamaged,
                HireExpireDate,
                randomOptions
            );
        }

        public RoItemInstance WithIdentification(bool isIdentified) {
            return new RoItemInstance(
                BagIndex,
                ItemId,
                Count,
                Kind,
                isIdentified,
                EquipLocationHint,
                WearState,
                RefineLevel,
                cardIds,
                Flags,
                UniqueId,
                ViewIdHint,
                ItemTypeRaw,
                InventoryFlagRaw,
                FavoriteRaw,
                EnchantGrade,
                IsDamaged,
                HireExpireDate,
                randomOptions
            );
        }

        public RoItemInstance WithItemOptions(
            bool isDamaged,
            int refineLevel,
            IReadOnlyList<int> cardIds,
            IReadOnlyList<RoItemRandomOption> options,
            byte enchantGrade
        ) {
            return new RoItemInstance(
                BagIndex,
                ItemId,
                Count,
                Kind,
                IsIdentified,
                EquipLocationHint,
                WearState,
                refineLevel,
                cardIds,
                Flags,
                UniqueId,
                ViewIdHint,
                ItemTypeRaw,
                InventoryFlagRaw,
                FavoriteRaw,
                enchantGrade,
                isDamaged,
                HireExpireDate,
                options
            );
        }

        private int GetVisibleSlotCount(RoItemMetadata metadata) {
            if (metadata == null || IsCraftedItem) {
                return 0;
            }

            return Math.Max(0, metadata.SlotCount);
        }

        private void AppendCardAffix(StringBuilder builder, RoItemMetadataTable table, bool postfix) {
            if (builder == null || !HasAnyCard) {
                return;
            }

            RoItemMetadataTable source = table ?? RoItemMetadataTable.Empty;
            var counts = new Dictionary<int, int>();
            var order = new List<int>();
            for (int i = 0; i < MaxCardSlots; i++) {
                int cardId = GetCardId(i);
                if (cardId <= 0) {
                    continue;
                }

                if (!counts.ContainsKey(cardId)) {
                    counts[cardId] = 0;
                    order.Add(cardId);
                }

                counts[cardId]++;
            }

            for (int i = 0; i < order.Count; i++) {
                int cardId = order[i];
                RoItemMetadata card = source.GetItemOrFallback(cardId);
                if (card.IsCardPostfix != postfix || string.IsNullOrEmpty(card.CardPrefixName)) {
                    continue;
                }

                string repeatPrefix = CardRepeatPrefixes[Math.Min(counts[cardId], CardRepeatPrefixes.Length - 1)];
                if (postfix) {
                    builder.Append(' ').Append(repeatPrefix).Append(card.CardPrefixName);
                } else {
                    builder.Append(repeatPrefix).Append(card.CardPrefixName).Append(' ');
                }
            }
        }

        private static int[] CopyCards(IReadOnlyList<int> source) {
            var copy = new int[MaxCardSlots];
            if (source == null) {
                return copy;
            }

            int count = Math.Min(MaxCardSlots, source.Count);
            for (int i = 0; i < count; i++) {
                copy[i] = Math.Max(0, source[i]);
            }

            return copy;
        }

        private static RoItemRandomOption[] CopyRandomOptions(IReadOnlyList<RoItemRandomOption> source) {
            var copy = new RoItemRandomOption[MaxRandomOptions];
            if (source == null) {
                return copy;
            }

            int count = Math.Min(MaxRandomOptions, source.Count);
            for (int i = 0; i < count; i++) {
                copy[i] = source[i];
            }

            return copy;
        }
    }

    public readonly struct RoItemRandomOption {
        public RoItemRandomOption(short index, short value, byte param) {
            Index = index;
            Value = value;
            Param = param;
        }

        public short Index { get; }
        public short Value { get; }
        public byte Param { get; }
        public bool IsEmpty => Index == 0;
    }
}
