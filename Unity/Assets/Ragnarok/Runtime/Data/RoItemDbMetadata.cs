using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public sealed class RoItemDbMetadata {
        public RoItemDbMetadata(
            int itemId,
            string aegisName,
            string displayName,
            RoItemType itemType,
            RoItemEquipLocation equipLocations,
            int weight,
            int buyPrice,
            int sellPrice,
            int slotCount,
            int viewId,
            string subType,
            RoItemEquipJob allowedJobs = RoItemEquipJob.None,
            bool allowsAllJobs = true,
            RoItemEquipClass allowedClasses = RoItemEquipClass.None,
            bool allowsAllClasses = true,
            RoItemGenderRestriction genderRestriction = RoItemGenderRestriction.Any,
            int equipLevelMin = 0,
            int equipLevelMax = 0,
            int weaponLevel = 0,
            bool isRefineable = false
        ) {
            ItemId = itemId;
            AegisName = NormalizeText(aegisName);
            DisplayName = NormalizeText(displayName);
            ItemType = itemType;
            EquipLocations = equipLocations;
            Weight = Math.Max(0, weight);
            BuyPrice = Math.Max(0, buyPrice);
            SellPrice = Math.Max(0, sellPrice);
            SlotCount = Math.Max(0, slotCount);
            ViewId = Math.Max(0, viewId);
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
        }

        public int ItemId { get; }
        public string AegisName { get; }
        public string DisplayName { get; }
        public RoItemType ItemType { get; }
        public RoItemEquipLocation EquipLocations { get; }
        public int Weight { get; }
        public int BuyPrice { get; }
        public int SellPrice { get; }
        public int SlotCount { get; }
        public int ViewId { get; }
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
        public bool HasJobRestriction => !AllowsAllJobs;
        public bool HasClassRestriction => !AllowsAllClasses;
        public bool HasGenderRestriction => GenderRestriction != RoItemGenderRestriction.Any;
        public bool HasLevelRestriction => EquipLevelMin > 0 || EquipLevelMax > 0;

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
            return AllowsJob(context.Job) &&
                AllowsClass(context.ClassGroup) &&
                AllowsGender(isMale) &&
                AllowsBaseLevel(context.BaseLevel);
        }

        private static string NormalizeText(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(value);
            return string.IsNullOrEmpty(decoded) ? value : decoded;
        }
    }

    public sealed class RoItemDbMetadataTable {
        private const int ItemDbCacheMagic = 0x494D4442; // IMDB
        private const int ItemDbCacheVersion = 1;
        private const int MaxCachedItems = 1_000_000;

        private static readonly Regex RecordStartPattern = new Regex(
            @"^\s*-\s+Id\s*:\s*(?<id>\d+)\s*$",
            RegexOptions.Compiled
        );

        private static readonly Regex TopLevelFieldPattern = new Regex(
            @"^(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.*)$",
            RegexOptions.Compiled
        );

        private static readonly Regex NestedBoolFieldPattern = new Regex(
            @"^(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>true|false)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private readonly Dictionary<int, RoItemDbMetadata> items;

        public static readonly RoItemDbMetadataTable Empty = new RoItemDbMetadataTable(
            new Dictionary<int, RoItemDbMetadata>(),
            null,
            null
        );

        private RoItemDbMetadataTable(
            Dictionary<int, RoItemDbMetadata> items,
            string sourcePath,
            string loadError
        ) {
            this.items = items ?? new Dictionary<int, RoItemDbMetadata>();
            SourcePath = sourcePath;
            LoadError = loadError;
        }

        public IReadOnlyDictionary<int, RoItemDbMetadata> Items => items;
        public int Count => items.Count;
        public string SourcePath { get; }
        public string LoadError { get; }
        public bool IsLoaded => Count > 0;

        public static RoItemDbMetadataTable Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            string lastError = null;
            string[][] candidateGroups = GetItemDbCandidatePathGroups();
            for (int groupIndex = 0; groupIndex < candidateGroups.Length; groupIndex++) {
                var parsed = new Dictionary<int, RoItemDbMetadata>();
                var loadedSources = new List<string>();
                bool foundAnyCandidate = false;

                string[] group = candidateGroups[groupIndex];
                for (int i = 0; i < group.Length; i++) {
                    string candidate = group[i];
                    if (!assets.Exists(candidate)) {
                        continue;
                    }

                    foundAnyCandidate = true;
                    try {
                        byte[] bytes = assets.ReadBytes(candidate);
                        if (bytes == null || bytes.Length == 0) {
                            continue;
                        }

                        RoItemDbMetadataTable table;
                        if (!TryReadItemDbCache(assets, candidate, bytes, out table)) {
                            table = FromYaml(RoTextEncoding.Decode(bytes), candidate);
                            TryWriteItemDbCache(assets, candidate, bytes, table);
                        }

                        foreach (KeyValuePair<int, RoItemDbMetadata> item in table.Items) {
                            parsed[item.Key] = item.Value;
                        }

                        loadedSources.Add(candidate);
                    } catch (Exception ex) {
                        lastError = ex.Message;
                    }
                }

                if (parsed.Count > 0) {
                    return new RoItemDbMetadataTable(parsed, string.Join("; ", loadedSources), null);
                }

                if (foundAnyCandidate && string.IsNullOrEmpty(lastError)) {
                    lastError = "no item db records parsed";
                }
            }

            return new RoItemDbMetadataTable(new Dictionary<int, RoItemDbMetadata>(), null, lastError);
        }

        public static RoItemDbMetadataTable FromYaml(string text, string sourcePath = null) {
            var parsed = new Dictionary<int, RoItemDbMetadata>();
            if (string.IsNullOrWhiteSpace(text)) {
                return new RoItemDbMetadataTable(parsed, sourcePath, null);
            }

            foreach (string record in EnumerateBodyRecords(text)) {
                RoItemDbMetadata item = ParseRecord(record);
                if (item != null) {
                    parsed[item.ItemId] = item;
                }
            }

            return new RoItemDbMetadataTable(parsed, sourcePath, null);
        }

        public bool TryGetItem(int itemId, out RoItemDbMetadata item) {
            return items.TryGetValue(itemId, out item);
        }

        public static string[][] GetItemDbCandidatePathGroups() {
            return new[] {
                new[] {
                    "db/pre-re/item_db_etc.yml",
                    "db/pre-re/item_db_usable.yml",
                    "db/pre-re/item_db_equip.yml",
                    "db/pre-re/item_db.yml",
                },
                new[] {
                    "db/item_db_etc.yml",
                    "db/item_db_usable.yml",
                    "db/item_db_equip.yml",
                    "db/item_db.yml",
                },
                new[] {
                    "item_db_etc.yml",
                    "item_db_usable.yml",
                    "item_db_equip.yml",
                    "item_db.yml",
                },
                new[] {
                    "db/re/item_db_etc.yml",
                    "db/re/item_db_usable.yml",
                    "db/re/item_db_equip.yml",
                    "db/re/item_db.yml",
                },
            };
        }

        private static bool TryReadItemDbCache(
            RoAssetDatabase assets,
            string sourcePath,
            byte[] sourceBytes,
            out RoItemDbMetadataTable table) {
            table = null;
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null || sourceBytes == null || sourceBytes.Length == 0) {
                return false;
            }

            string cacheKey = RoItemMetadataTable.BuildParsedSourceCacheKey("item-db", sourcePath, sourceBytes);
            try {
                if (!cache.TryReadBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", out byte[] data)) {
                    return false;
                }

                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadInt32() != ItemDbCacheMagic ||
                    reader.ReadInt32() != ItemDbCacheVersion) {
                    return false;
                }

                string cachedSourcePath = reader.ReadString();
                if (!string.Equals(cachedSourcePath, sourcePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }

                int count = reader.ReadInt32();
                if (count < 0 || count > MaxCachedItems) {
                    return false;
                }

                var parsed = new Dictionary<int, RoItemDbMetadata>(count);
                for (int i = 0; i < count; i++) {
                    RoItemDbMetadata item = ReadItemDbCacheEntry(reader);
                    parsed[item.ItemId] = item;
                }

                table = new RoItemDbMetadataTable(parsed, sourcePath, null);
                return true;
            } catch {
                table = null;
                return false;
            }
        }

        private static void TryWriteItemDbCache(
            RoAssetDatabase assets,
            string sourcePath,
            byte[] sourceBytes,
            RoItemDbMetadataTable table) {
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null || sourceBytes == null || sourceBytes.Length == 0 || table == null || table.Count == 0) {
                return;
            }

            string cacheKey = RoItemMetadataTable.BuildParsedSourceCacheKey("item-db", sourcePath, sourceBytes);
            try {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                    writer.Write(ItemDbCacheMagic);
                    writer.Write(ItemDbCacheVersion);
                    writer.Write(sourcePath ?? string.Empty);
                    writer.Write(table.items.Count);
                    foreach (RoItemDbMetadata item in table.items.Values) {
                        WriteItemDbCacheEntry(writer, item);
                    }
                }

                cache.WriteBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", stream.ToArray());
            } catch {
                // Parsed YAML cache is an optimization only; source bytes remain authoritative.
            }
        }

        private static RoItemDbMetadata ReadItemDbCacheEntry(BinaryReader reader) {
            return new RoItemDbMetadata(
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadString(),
                (RoItemType) reader.ReadInt32(),
                (RoItemEquipLocation) reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadString(),
                (RoItemEquipJob) reader.ReadUInt64(),
                reader.ReadBoolean(),
                (RoItemEquipClass) reader.ReadInt32(),
                reader.ReadBoolean(),
                (RoItemGenderRestriction) reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadBoolean()
            );
        }

        private static void WriteItemDbCacheEntry(BinaryWriter writer, RoItemDbMetadata item) {
            writer.Write(item.ItemId);
            writer.Write(item.AegisName ?? string.Empty);
            writer.Write(item.DisplayName ?? string.Empty);
            writer.Write((int) item.ItemType);
            writer.Write((int) item.EquipLocations);
            writer.Write(item.Weight);
            writer.Write(item.BuyPrice);
            writer.Write(item.SellPrice);
            writer.Write(item.SlotCount);
            writer.Write(item.ViewId);
            writer.Write(item.SubType ?? string.Empty);
            writer.Write((ulong) item.AllowedJobs);
            writer.Write(item.AllowsAllJobs);
            writer.Write((int) item.AllowedClasses);
            writer.Write(item.AllowsAllClasses);
            writer.Write((int) item.GenderRestriction);
            writer.Write(item.EquipLevelMin);
            writer.Write(item.EquipLevelMax);
            writer.Write(item.WeaponLevel);
            writer.Write(item.IsRefineable);
        }

        private static IEnumerable<string> EnumerateBodyRecords(string text) {
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            bool inBody = false;
            StringBuilder current = null;

            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];
                if (!inBody) {
                    if (string.Equals(line.Trim(), "Body:", StringComparison.Ordinal)) {
                        inBody = true;
                    }

                    continue;
                }

                if (RecordStartPattern.IsMatch(line)) {
                    if (current != null && current.Length > 0) {
                        yield return current.ToString();
                    }

                    current = new StringBuilder();
                    current.AppendLine(line);
                    continue;
                }

                if (current != null) {
                    current.AppendLine(line);
                }
            }

            if (current != null && current.Length > 0) {
                yield return current.ToString();
            }
        }

        private static RoItemDbMetadata ParseRecord(string record) {
            int id = 0;
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            RoItemEquipLocation locations = RoItemEquipLocation.None;
            RoItemEquipJob allowedJobs = RoItemEquipJob.None;
            RoItemEquipClass allowedClasses = RoItemEquipClass.None;
            bool sawJobsSection = false;
            bool sawClassesSection = false;
            bool allowsAllJobs = false;
            bool allowsAllClasses = false;
            string activeSection = null;
            int activeSectionIndent = -1;

            string[] lines = record.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                string rawLine = StripComment(lines[i]);
                if (string.IsNullOrWhiteSpace(rawLine)) {
                    continue;
                }

                int indent = CountLeadingSpaces(rawLine);
                string trimmed = rawLine.Trim();
                Match recordStart = RecordStartPattern.Match(rawLine);
                if (recordStart.Success) {
                    TryParseInteger(recordStart.Groups["id"].Value, out id);
                    activeSection = null;
                    activeSectionIndent = -1;
                    continue;
                }

                Match topLevel = TopLevelFieldPattern.Match(trimmed);
                if (topLevel.Success && indent <= 4) {
                    string field = topLevel.Groups["field"].Value;
                    string value = CleanYamlScalar(topLevel.Groups["value"].Value);
                    fields[field] = value;
                    if (string.Equals(field, "Jobs", StringComparison.OrdinalIgnoreCase)) {
                        sawJobsSection = true;
                    } else if (string.Equals(field, "Classes", StringComparison.OrdinalIgnoreCase)) {
                        sawClassesSection = true;
                    }

                    activeSection = string.IsNullOrEmpty(value) ? field : null;
                    activeSectionIndent = string.IsNullOrEmpty(value) ? indent : -1;
                    continue;
                }

                if (indent > activeSectionIndent) {
                    Match nested = NestedBoolFieldPattern.Match(trimmed);
                    if (nested.Success && string.Equals(nested.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase)) {
                        string nestedField = nested.Groups["field"].Value;
                        if (string.Equals(activeSection, "Locations", StringComparison.OrdinalIgnoreCase)) {
                            locations |= ParseEquipLocation(nestedField);
                        } else if (string.Equals(activeSection, "Jobs", StringComparison.OrdinalIgnoreCase)) {
                            if (TryParseEquipJob(nestedField, out RoItemEquipJob job, out bool allJobs)) {
                                if (allJobs) {
                                    allowsAllJobs = true;
                                } else {
                                    allowedJobs |= job;
                                }
                            }
                        } else if (string.Equals(activeSection, "Classes", StringComparison.OrdinalIgnoreCase)) {
                            if (TryParseEquipClass(nestedField, out RoItemEquipClass classGroup, out bool allClasses)) {
                                if (allClasses) {
                                    allowsAllClasses = true;
                                } else {
                                    allowedClasses |= classGroup;
                                }
                            }
                        }
                    }
                }
            }

            if (id <= 0) {
                return null;
            }

            int? buy = ReadNullableInt(fields, "Buy");
            int? sell = ReadNullableInt(fields, "Sell");
            int buyPrice = buy ?? (sell.HasValue ? sell.Value * 2 : 0);
            int sellPrice = sell ?? (buy.HasValue ? buy.Value / 2 : 0);

            return new RoItemDbMetadata(
                id,
                ReadString(fields, "AegisName"),
                ReadString(fields, "Name"),
                ParseItemType(ReadString(fields, "Type")),
                locations,
                ReadInt(fields, "Weight"),
                buyPrice,
                sellPrice,
                ReadInt(fields, "Slots"),
                ReadInt(fields, "View"),
                ReadString(fields, "SubType"),
                allowedJobs,
                !sawJobsSection || allowsAllJobs,
                allowedClasses,
                !sawClassesSection || allowsAllClasses,
                ParseGenderRestriction(ReadString(fields, "Gender")),
                ReadInt(fields, "EquipLevelMin"),
                ReadInt(fields, "EquipLevelMax"),
                ReadInt(fields, "WeaponLevel"),
                ReadBool(fields, "Refineable")
            );
        }

        private static RoItemType ParseItemType(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return RoItemType.Etc;
            }

            switch (NormalizeKey(value)) {
                case "healing":
                    return RoItemType.Healing;
                case "usable":
                    return RoItemType.Usable;
                case "etc":
                    return RoItemType.Etc;
                case "weapon":
                    return RoItemType.Weapon;
                case "armor":
                    return RoItemType.Armor;
                case "card":
                    return RoItemType.Card;
                case "petegg":
                    return RoItemType.PetEgg;
                case "petarmor":
                    return RoItemType.PetArmor;
                case "ammo":
                    return RoItemType.Ammo;
                case "delayconsume":
                    return RoItemType.DelayConsume;
                case "cash":
                    return RoItemType.Cash;
                case "shadowgear":
                    return RoItemType.ShadowGear;
                default:
                    return RoItemType.Unknown;
            }
        }

        private static RoItemEquipLocation ParseEquipLocation(string value) {
            switch (NormalizeKey(value)) {
                case "headlow":
                case "headlower":
                case "headbottom":
                    return RoItemEquipLocation.HeadBottom;
                case "costumeheadlow":
                case "costumeheadlower":
                case "costumeheadbottom":
                    return RoItemEquipLocation.CostumeHeadBottom;
                case "righthand":
                case "weapon":
                    return RoItemEquipLocation.Weapon;
                case "lefthand":
                case "shield":
                    return RoItemEquipLocation.Shield;
                case "bothhand":
                    return RoItemEquipLocation.BothHands;
                case "garment":
                    return RoItemEquipLocation.Garment;
                case "accessory":
                case "bothaccessory":
                    return RoItemEquipLocation.Accessory;
                case "accl":
                case "leftaccessory":
                case "accessory1":
                    return RoItemEquipLocation.Accessory1;
                case "accr":
                case "rightaccessory":
                case "accessory2":
                    return RoItemEquipLocation.Accessory2;
                case "armor":
                    return RoItemEquipLocation.Armor;
                case "shoes":
                case "footgear":
                    return RoItemEquipLocation.Shoes;
                case "costumeheadtop":
                    return RoItemEquipLocation.CostumeHeadTop;
                case "costumeheadmid":
                    return RoItemEquipLocation.CostumeHeadMid;
                case "costumerobe":
                case "costumegarment":
                    return RoItemEquipLocation.CostumeRobe;
                case "costumefloor":
                    return RoItemEquipLocation.CostumeFloor;
                case "shadowarmor":
                    return RoItemEquipLocation.ShadowArmor;
                case "shadowweapon":
                    return RoItemEquipLocation.ShadowWeapon;
                case "shadowshield":
                    return RoItemEquipLocation.ShadowShield;
                case "shadowshoes":
                case "shadowfootgear":
                    return RoItemEquipLocation.ShadowShoes;
                case "shadowaccessory":
                case "shadowbothaccessory":
                    return RoItemEquipLocation.ShadowAccessory;
                case "shadowaccessoryright":
                case "shadowrightaccessory":
                case "shadowraccessory":
                case "shadowraccessoryshadow":
                    return RoItemEquipLocation.ShadowAccessoryRight;
                case "shadowaccessoryleft":
                case "shadowleftaccessory":
                case "shadowlaccessory":
                case "shadowlaccessoryshadow":
                    return RoItemEquipLocation.ShadowAccessoryLeft;
                case "headtop":
                    return RoItemEquipLocation.HeadTop;
                case "headmid":
                    return RoItemEquipLocation.HeadMid;
                case "ammo":
                    return RoItemEquipLocation.Ammo;
                default:
                    return RoItemEquipLocation.None;
            }
        }

        private static bool TryParseEquipJob(string value, out RoItemEquipJob job, out bool allJobs) {
            job = RoItemEquipJob.None;
            allJobs = false;
            switch (NormalizeKey(value)) {
                case "all":
                    allJobs = true;
                    return true;
                case "novice":
                    job = RoItemEquipJob.Novice;
                    return true;
                case "swordman":
                case "swordsman":
                    job = RoItemEquipJob.Swordman;
                    return true;
                case "mage":
                case "magician":
                    job = RoItemEquipJob.Mage;
                    return true;
                case "archer":
                    job = RoItemEquipJob.Archer;
                    return true;
                case "acolyte":
                    job = RoItemEquipJob.Acolyte;
                    return true;
                case "merchant":
                    job = RoItemEquipJob.Merchant;
                    return true;
                case "thief":
                    job = RoItemEquipJob.Thief;
                    return true;
                case "knight":
                    job = RoItemEquipJob.Knight;
                    return true;
                case "priest":
                    job = RoItemEquipJob.Priest;
                    return true;
                case "wizard":
                    job = RoItemEquipJob.Wizard;
                    return true;
                case "blacksmith":
                    job = RoItemEquipJob.Blacksmith;
                    return true;
                case "hunter":
                    job = RoItemEquipJob.Hunter;
                    return true;
                case "assassin":
                    job = RoItemEquipJob.Assassin;
                    return true;
                case "crusader":
                    job = RoItemEquipJob.Crusader;
                    return true;
                case "monk":
                    job = RoItemEquipJob.Monk;
                    return true;
                case "sage":
                    job = RoItemEquipJob.Sage;
                    return true;
                case "rogue":
                    job = RoItemEquipJob.Rogue;
                    return true;
                case "alchemist":
                    job = RoItemEquipJob.Alchemist;
                    return true;
                case "bard":
                case "dancer":
                case "barddancer":
                    job = RoItemEquipJob.BardDancer;
                    return true;
                case "taekwon":
                case "taekwondo":
                case "taekwonkid":
                    job = RoItemEquipJob.Taekwon;
                    return true;
                case "stargladiator":
                    job = RoItemEquipJob.StarGladiator;
                    return true;
                case "soullinker":
                    job = RoItemEquipJob.SoulLinker;
                    return true;
                case "gunslinger":
                    job = RoItemEquipJob.Gunslinger;
                    return true;
                case "ninja":
                    job = RoItemEquipJob.Ninja;
                    return true;
                case "kagerou":
                case "oboro":
                case "kagerouoboro":
                    job = RoItemEquipJob.KagerouOboro;
                    return true;
                case "rebellion":
                    job = RoItemEquipJob.Rebellion;
                    return true;
                case "supernovice":
                    job = RoItemEquipJob.SuperNovice;
                    return true;
                case "summoner":
                    job = RoItemEquipJob.Summoner;
                    return true;
                case "spirithandler":
                    job = RoItemEquipJob.SpiritHandler;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseEquipClass(string value, out RoItemEquipClass classGroup, out bool allClasses) {
            classGroup = RoItemEquipClass.None;
            allClasses = false;
            switch (NormalizeKey(value)) {
                case "all":
                    allClasses = true;
                    return true;
                case "normal":
                    classGroup = RoItemEquipClass.Normal;
                    return true;
                case "upper":
                    classGroup = RoItemEquipClass.Upper;
                    return true;
                case "baby":
                    classGroup = RoItemEquipClass.Baby;
                    return true;
                case "third":
                    classGroup = RoItemEquipClass.Third;
                    return true;
                case "thirdupper":
                case "upperthird":
                    classGroup = RoItemEquipClass.ThirdUpper;
                    return true;
                case "thirdbaby":
                case "babythird":
                    classGroup = RoItemEquipClass.ThirdBaby;
                    return true;
                case "allthird":
                    classGroup = RoItemEquipClass.AllThird;
                    return true;
                case "fourth":
                    classGroup = RoItemEquipClass.Fourth;
                    return true;
                default:
                    return false;
            }
        }

        private static RoItemGenderRestriction ParseGenderRestriction(string value) {
            switch (NormalizeKey(value)) {
                case "male":
                    return RoItemGenderRestriction.Male;
                case "female":
                    return RoItemGenderRestriction.Female;
                default:
                    return RoItemGenderRestriction.Any;
            }
        }

        private static string NormalizeKey(string value) {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();
        }

        private static string ReadString(IReadOnlyDictionary<string, string> fields, string key) {
            return fields.TryGetValue(key, out string value) ? value : string.Empty;
        }

        private static int ReadInt(IReadOnlyDictionary<string, string> fields, string key) {
            return ReadNullableInt(fields, key) ?? 0;
        }

        private static bool ReadBool(IReadOnlyDictionary<string, string> fields, string key) {
            return fields.TryGetValue(key, out string value) &&
                bool.TryParse(value, out bool parsed) &&
                parsed;
        }

        private static int? ReadNullableInt(IReadOnlyDictionary<string, string> fields, string key) {
            return fields.TryGetValue(key, out string value) && TryParseInteger(value, out int parsed) ? parsed : (int?) null;
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

        private static string CleanYamlScalar(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            value = value.Trim();
            if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || value == "~") {
                return string.Empty;
            }

            if ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
                (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))) {
                value = value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static string StripComment(string line) {
            if (string.IsNullOrEmpty(line)) {
                return string.Empty;
            }

            bool inString = false;
            char quote = '\0';
            for (int i = 0; i < line.Length; i++) {
                char current = line[i];
                if (inString) {
                    if (current == '\\') {
                        i++;
                        continue;
                    }

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

        private static int CountLeadingSpaces(string value) {
            int count = 0;
            while (count < value.Length && value[count] == ' ') {
                count++;
            }

            return count;
        }
    }
}
