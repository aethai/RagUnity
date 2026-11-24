using System;
using System.Collections.Generic;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public sealed class RoItemMetadata {
        public RoItemMetadata(
            int itemId,
            string unidentifiedDisplayName,
            string unidentifiedResourceName,
            IReadOnlyList<string> unidentifiedDescription,
            string identifiedDisplayName,
            string identifiedResourceName,
            IReadOnlyList<string> identifiedDescription,
            int slotCount,
            int classNumber,
            bool isCostume,
            RoItemType itemType = RoItemType.Unknown,
            RoItemEquipLocation equipLocations = RoItemEquipLocation.None,
            int weight = 0,
            int buyPrice = 0,
            int sellPrice = 0,
            int viewId = 0,
            string aegisName = null,
            string databaseDisplayName = null,
            string subType = null,
            RoItemEquipJob allowedJobs = RoItemEquipJob.None,
            bool allowsAllJobs = true,
            RoItemEquipClass allowedClasses = RoItemEquipClass.None,
            bool allowsAllClasses = true,
            RoItemGenderRestriction genderRestriction = RoItemGenderRestriction.Any,
            int equipLevelMin = 0,
            int equipLevelMax = 0,
            int weaponLevel = 0,
            bool isRefineable = false,
            string cardPrefixName = null,
            bool isCardPostfix = false,
            string cardIllustrationResourceName = null,
            RoItemMoveInfo moveInfo = default
        ) {
            ItemId = itemId;
            UnidentifiedDisplayName = NormalizeText(unidentifiedDisplayName);
            UnidentifiedResourceName = NormalizeText(unidentifiedResourceName);
            UnidentifiedDescription = unidentifiedDescription ?? Array.Empty<string>();
            IdentifiedDisplayName = NormalizeText(identifiedDisplayName);
            IdentifiedResourceName = NormalizeText(identifiedResourceName);
            IdentifiedDescription = identifiedDescription ?? Array.Empty<string>();
            SlotCount = Math.Max(0, slotCount);
            ClassNumber = Math.Max(0, classNumber);
            IsCostume = isCostume;
            ItemType = itemType;
            EquipLocations = equipLocations;
            Weight = Math.Max(0, weight);
            BuyPrice = Math.Max(0, buyPrice);
            SellPrice = Math.Max(0, sellPrice);
            ViewId = Math.Max(0, viewId);
            AegisName = NormalizeText(aegisName);
            DatabaseDisplayName = NormalizeText(databaseDisplayName);
            SubType = NormalizeText(subType);
            AllowedJobs = allowedJobs;
            AllowsAllJobs = allowsAllJobs;
            AllowedClasses = allowedClasses;
            AllowsAllClasses = allowsAllClasses;
            GenderRestriction = genderRestriction;
            EquipLevelMin = Math.Max(0, equipLevelMin);
            EquipLevelMax = Math.Max(0, equipLevelMax);
            WeaponLevel = Math.Max(0, weaponLevel);
            IsRefineable = isRefineable;
            CardPrefixName = NormalizeText(cardPrefixName);
            IsCardPostfix = isCardPostfix;
            CardIllustrationResourceName = NormalizeText(cardIllustrationResourceName);
            MoveInfo = moveInfo;
        }

        public int ItemId { get; }
        public string UnidentifiedDisplayName { get; }
        public string UnidentifiedResourceName { get; }
        public IReadOnlyList<string> UnidentifiedDescription { get; }
        public string IdentifiedDisplayName { get; }
        public string IdentifiedResourceName { get; }
        public IReadOnlyList<string> IdentifiedDescription { get; }
        public int SlotCount { get; }
        public int ClassNumber { get; }
        public bool IsCostume { get; }
        public RoItemType ItemType { get; }
        public RoItemEquipLocation EquipLocations { get; }
        public int Weight { get; }
        public int BuyPrice { get; }
        public int SellPrice { get; }
        public int ViewId { get; }
        public string AegisName { get; }
        public string DatabaseDisplayName { get; }
        public string SubType { get; }
        public RoItemEquipJob AllowedJobs { get; }
        public bool AllowsAllJobs { get; }
        public RoItemEquipClass AllowedClasses { get; }
        public bool AllowsAllClasses { get; }
        public RoItemGenderRestriction GenderRestriction { get; }
        public int EquipLevelMin { get; }
        public int EquipLevelMax { get; }
        public int WeaponLevel { get; }
        public bool IsRefineable { get; }
        public string CardPrefixName { get; }
        public bool IsCardPostfix { get; }
        public string CardIllustrationResourceName { get; }
        public RoItemMoveInfo MoveInfo { get; }
        public bool HasJobRestriction => !AllowsAllJobs;
        public bool HasClassRestriction => !AllowsAllClasses;
        public bool HasGenderRestriction => GenderRestriction != RoItemGenderRestriction.Any;
        public bool HasLevelRestriction => EquipLevelMin > 0 || EquipLevelMax > 0;
        public bool HasDatabaseMetadata =>
            ItemType != RoItemType.Unknown ||
            EquipLocations != RoItemEquipLocation.None ||
            Weight > 0 ||
            BuyPrice > 0 ||
            SellPrice > 0 ||
            ViewId > 0 ||
            !string.IsNullOrEmpty(AegisName) ||
            !string.IsNullOrEmpty(DatabaseDisplayName) ||
            !string.IsNullOrEmpty(SubType) ||
            HasJobRestriction ||
            HasClassRestriction ||
            HasGenderRestriction ||
            HasLevelRestriction ||
            WeaponLevel > 0 ||
            IsRefineable;
        public bool IsEquipable => EquipLocations != RoItemEquipLocation.None || ItemType == RoItemType.Weapon || ItemType == RoItemType.Armor || ItemType == RoItemType.Ammo;

        public string GetDisplayName(bool identified) {
            string name = identified ? IdentifiedDisplayName : UnidentifiedDisplayName;
            return string.IsNullOrEmpty(name) ? "Unknown Item" : name;
        }

        public string GetResourceName(bool identified) {
            string name = identified ? IdentifiedResourceName : UnidentifiedResourceName;
            return string.IsNullOrEmpty(name) ? "unknown" : name;
        }

        public IReadOnlyList<string> GetDescription(bool identified) {
            return identified ? IdentifiedDescription : UnidentifiedDescription;
        }

        public string GetWorldSpriteBasePath(bool identified) {
            return "data/sprite/" + ItemSpriteSegment() + "/" + GetResourceName(identified);
        }

        public string GetInventoryIconPath(bool identified) {
            return "data/texture/" + InterfaceSegment() + "/item/" + GetResourceName(identified) + ".png";
        }

        public string GetCollectionIconPath(bool identified) {
            return "data/texture/" + InterfaceSegment() + "/collection/" + GetResourceName(identified) + ".png";
        }

        public bool AllowsJob(RoItemEquipJob job) {
            return AllowsAllJobs || (job != RoItemEquipJob.None && (AllowedJobs & job) != RoItemEquipJob.None);
        }

        public bool AllowsClass(RoItemEquipClass classGroup) {
            return AllowsAllClasses || (classGroup != RoItemEquipClass.None && (AllowedClasses & classGroup) != RoItemEquipClass.None);
        }

        public bool AllowsGender(bool isMale) {
            if (GenderRestriction == RoItemGenderRestriction.Any) {
                return true;
            }

            return isMale
                ? GenderRestriction == RoItemGenderRestriction.Male
                : GenderRestriction == RoItemGenderRestriction.Female;
        }

        public bool AllowsBaseLevel(int baseLevel) {
            if (EquipLevelMin > 0 && baseLevel < EquipLevelMin) {
                return false;
            }

            return EquipLevelMax <= 0 || baseLevel <= EquipLevelMax;
        }

        public bool CanEquip(RoItemEquipContext context) {
            bool isMale = context.Gender != RoItemGenderRestriction.Female;
            return IsEquipable &&
                AllowsJob(context.Job) &&
                AllowsClass(context.ClassGroup) &&
                AllowsGender(isMale) &&
                AllowsBaseLevel(context.BaseLevel);
        }

        public RoItemMetadata WithDatabaseMetadata(RoItemDbMetadata metadata) {
            if (metadata == null || metadata.ItemId != ItemId) {
                return this;
            }

            return new RoItemMetadata(
                ItemId,
                UnidentifiedDisplayName,
                UnidentifiedResourceName,
                UnidentifiedDescription,
                IdentifiedDisplayName,
                IdentifiedResourceName,
                IdentifiedDescription,
                SlotCount > 0 ? SlotCount : metadata.SlotCount,
                ClassNumber > 0 ? ClassNumber : metadata.ViewId,
                IsCostume,
                metadata.ItemType,
                metadata.EquipLocations,
                metadata.Weight,
                metadata.BuyPrice,
                metadata.SellPrice,
                metadata.ViewId,
                metadata.AegisName,
                metadata.DisplayName,
                metadata.SubType,
                metadata.AllowedJobs,
                metadata.AllowsAllJobs,
                metadata.AllowedClasses,
                metadata.AllowsAllClasses,
                metadata.GenderRestriction,
                metadata.EquipLevelMin,
                metadata.EquipLevelMax,
                metadata.WeaponLevel,
                metadata.IsRefineable,
                CardPrefixName,
                IsCardPostfix,
                CardIllustrationResourceName,
                MoveInfo
            );
        }

        public RoItemMetadata WithClientItemTables(
            string cardPrefixName = null,
            bool? isCardPostfix = null,
            string cardIllustrationResourceName = null,
            RoItemMoveInfo? moveInfo = null
        ) {
            return new RoItemMetadata(
                ItemId,
                UnidentifiedDisplayName,
                UnidentifiedResourceName,
                UnidentifiedDescription,
                IdentifiedDisplayName,
                IdentifiedResourceName,
                IdentifiedDescription,
                SlotCount,
                ClassNumber,
                IsCostume,
                ItemType,
                EquipLocations,
                Weight,
                BuyPrice,
                SellPrice,
                ViewId,
                AegisName,
                DatabaseDisplayName,
                SubType,
                AllowedJobs,
                AllowsAllJobs,
                AllowedClasses,
                AllowsAllClasses,
                GenderRestriction,
                EquipLevelMin,
                EquipLevelMax,
                WeaponLevel,
                IsRefineable,
                cardPrefixName ?? CardPrefixName,
                isCardPostfix ?? IsCardPostfix,
                cardIllustrationResourceName ?? CardIllustrationResourceName,
                moveInfo ?? MoveInfo
            );
        }

        public static RoItemMetadata FromDatabaseMetadata(RoItemDbMetadata metadata) {
            if (metadata == null) {
                return null;
            }

            string displayName = string.IsNullOrEmpty(metadata.DisplayName) ? "Unknown Item" : metadata.DisplayName;
            return new RoItemMetadata(
                metadata.ItemId,
                displayName,
                "unknown",
                Array.Empty<string>(),
                displayName,
                "unknown",
                Array.Empty<string>(),
                metadata.SlotCount,
                metadata.ViewId,
                false,
                metadata.ItemType,
                metadata.EquipLocations,
                metadata.Weight,
                metadata.BuyPrice,
                metadata.SellPrice,
                metadata.ViewId,
                metadata.AegisName,
                metadata.DisplayName,
                metadata.SubType,
                metadata.AllowedJobs,
                metadata.AllowsAllJobs,
                metadata.AllowedClasses,
                metadata.AllowsAllClasses,
                metadata.GenderRestriction,
                metadata.EquipLevelMin,
                metadata.EquipLevelMax,
                metadata.WeaponLevel,
                metadata.IsRefineable
            );
        }

        private static string NormalizeText(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(value);
            return string.IsNullOrEmpty(decoded) ? value : decoded;
        }

        private static string ItemSpriteSegment() {
            return LegacyKorean(0xBE, 0xC6, 0xC0, 0xCC, 0xC5, 0xDB);
        }

        private static string InterfaceSegment() {
            return LegacyKorean(0xC0, 0xAF, 0xC0, 0xFA, 0xC0, 0xCE, 0xC5, 0xCD, 0xC6, 0xE4, 0xC0, 0xCC, 0xBD, 0xBA);
        }

        private static string LegacyKorean(params int[] bytes) {
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) {
                chars[i] = (char) bytes[i];
            }

            string legacy = new string(chars);
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(legacy);
            return string.IsNullOrEmpty(decoded) ? legacy : decoded;
        }
    }
}
