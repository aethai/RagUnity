using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public enum RoItemCommandStatus {
        Accepted = 0,
        InactiveMapSession = 1,
        InvalidBagIndex = 2,
        MissingItem = 3,
        InvalidCount = 4,
        ItemNotUsable = 5,
        ItemNotEquipable = 6,
        ItemNotEquipped = 7,
        MissingGroundItem = 8,
        SelfDead = 9,
        SendFailed = 10,
        Disposed = 11
    }

    public readonly struct RoItemCommandResult {
        public RoItemCommandResult(
            RoItemCommandStatus status,
            RoInventoryRuntimeItemSnapshot item,
            RoGroundItemRuntimeSnapshot groundItem,
            string message) {
            Status = status;
            Item = item;
            GroundItem = groundItem;
            Message = message ?? string.Empty;
        }

        public RoItemCommandStatus Status { get; }
        public RoInventoryRuntimeItemSnapshot Item { get; }
        public RoGroundItemRuntimeSnapshot GroundItem { get; }
        public string Message { get; }
        public bool Accepted => Status == RoItemCommandStatus.Accepted;

        public static RoItemCommandResult Failure(RoItemCommandStatus status, string message) {
            return new RoItemCommandResult(status, default, default, message);
        }
    }

    public readonly struct RoInventoryRuntimeItemSnapshot {
        public RoInventoryRuntimeItemSnapshot(
            int bagIndex,
            uint itemId,
            byte itemTypeRaw,
            int count,
            bool isIdentified,
            bool isDamaged,
            uint equipLocationRaw,
            uint wearStateRaw,
            byte refineLevel,
            int[] cardIds,
            ushort viewIdHint,
            byte flag,
            bool isEquipment) {
            BagIndex = bagIndex;
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            Count = Math.Max(0, count);
            IsIdentified = isIdentified;
            IsDamaged = isDamaged;
            EquipLocationRaw = equipLocationRaw;
            WearStateRaw = wearStateRaw;
            RefineLevel = refineLevel;
            CardIds = CopyCardIds(cardIds);
            ViewIdHint = viewIdHint;
            Flag = flag;
            IsEquipment = isEquipment;
        }

        public int BagIndex { get; }
        public uint ItemId { get; }
        public byte ItemTypeRaw { get; }
        public int Count { get; }
        public bool IsIdentified { get; }
        public bool IsDamaged { get; }
        public uint EquipLocationRaw { get; }
        public uint WearStateRaw { get; }
        public byte RefineLevel { get; }
        public int[] CardIds { get; }
        public ushort ViewIdHint { get; }
        public byte Flag { get; }
        public bool IsEquipment { get; }
        public bool IsEquipped => WearStateRaw != 0;
        public bool IsEquipable => IsEquipment || EquipLocationRaw != 0;
        public bool IsUsable => !IsEquipped && Count > 0;

        public int GetCardId(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= 4) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "RO item records expose exactly four card/socket fields.");
            }

            return CardIds[slotIndex];
        }

        public RoInventoryRuntimeItemSnapshot WithCount(int count) {
            return new RoInventoryRuntimeItemSnapshot(
                BagIndex,
                ItemId,
                ItemTypeRaw,
                count,
                IsIdentified,
                IsDamaged,
                EquipLocationRaw,
                WearStateRaw,
                RefineLevel,
                CardIds,
                ViewIdHint,
                Flag,
                IsEquipment);
        }

        public RoInventoryRuntimeItemSnapshot WithWearState(uint wearStateRaw, ushort viewIdHint) {
            return new RoInventoryRuntimeItemSnapshot(
                BagIndex,
                ItemId,
                ItemTypeRaw,
                Count,
                IsIdentified,
                IsDamaged,
                EquipLocationRaw,
                wearStateRaw,
                RefineLevel,
                CardIds,
                viewIdHint == 0 ? ViewIdHint : viewIdHint,
                Flag,
                IsEquipment);
        }

        public RoInventoryRuntimeItemSnapshot WithCardIds(int[] cardIds) {
            return new RoInventoryRuntimeItemSnapshot(
                BagIndex,
                ItemId,
                ItemTypeRaw,
                Count,
                IsIdentified,
                IsDamaged,
                EquipLocationRaw,
                WearStateRaw,
                RefineLevel,
                cardIds,
                ViewIdHint,
                Flag,
                IsEquipment);
        }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[4];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }
    }

    public readonly struct RoGroundItemRuntimeSnapshot {
        public RoGroundItemRuntimeSnapshot(
            uint itemAid,
            uint itemId,
            int itemTypeRaw,
            bool isIdentified,
            int x,
            int y,
            int count,
            bool isFalling,
            bool showDropEffect,
            short dropEffectMode) {
            ItemAid = itemAid;
            ItemId = itemId;
            ItemTypeRaw = itemTypeRaw;
            IsIdentified = isIdentified;
            X = x;
            Y = y;
            Count = Math.Max(0, count);
            IsFalling = isFalling;
            ShowDropEffect = showDropEffect;
            DropEffectMode = dropEffectMode;
        }

        public uint ItemAid { get; }
        public uint ItemId { get; }
        public int ItemTypeRaw { get; }
        public bool IsIdentified { get; }
        public int X { get; }
        public int Y { get; }
        public int Count { get; }
        public bool IsFalling { get; }
        public bool ShowDropEffect { get; }
        public short DropEffectMode { get; }
    }

    public readonly struct RoInventoryUseItemRequest {
        public RoInventoryUseItemRequest(int bagIndex, uint actorId) {
            BagIndex = bagIndex;
            ActorId = actorId;
        }

        public int BagIndex { get; }
        public uint ActorId { get; }
    }

    public readonly struct RoInventoryDropItemRequest {
        public RoInventoryDropItemRequest(int bagIndex, int count) {
            BagIndex = bagIndex;
            Count = count;
        }

        public int BagIndex { get; }
        public int Count { get; }
    }

    public readonly struct RoInventoryEquipItemRequest {
        public RoInventoryEquipItemRequest(int bagIndex, uint position) {
            BagIndex = bagIndex;
            Position = position;
        }

        public int BagIndex { get; }
        public uint Position { get; }
    }

    public readonly struct RoInventoryUnequipItemRequest {
        public RoInventoryUnequipItemRequest(int bagIndex) {
            BagIndex = bagIndex;
        }

        public int BagIndex { get; }
    }

    public readonly struct RoInventoryPickupItemRequest {
        public RoInventoryPickupItemRequest(uint itemAid) {
            ItemAid = itemAid;
        }

        public uint ItemAid { get; }
    }

    public readonly struct RoInventoryCardCompositionListRequest {
        public RoInventoryCardCompositionListRequest(int cardIndex) {
            CardIndex = cardIndex;
        }

        public int CardIndex { get; }
    }

    public readonly struct RoInventoryCardCompositionRequest {
        public RoInventoryCardCompositionRequest(int cardIndex, int equipIndex) {
            CardIndex = cardIndex;
            EquipIndex = equipIndex;
        }

        public int CardIndex { get; }
        public int EquipIndex { get; }
    }

    public readonly struct RoCardCompositionRuntimeSnapshot {
        public static readonly RoCardCompositionRuntimeSnapshot Empty = new RoCardCompositionRuntimeSnapshot(0, Array.Empty<int>(), 0);

        public RoCardCompositionRuntimeSnapshot(int cardBagIndex, int[] targetBagIndexes, int sequence) {
            CardBagIndex = Math.Max(0, cardBagIndex);
            TargetBagIndexes = Copy(targetBagIndexes);
            Sequence = Math.Max(0, sequence);
        }

        public int CardBagIndex { get; }
        public int[] TargetBagIndexes { get; }
        public int Sequence { get; }
        public bool IsActive => CardBagIndex > 0;

        private static int[] Copy(int[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<int>();
            }

            var copy = new int[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
