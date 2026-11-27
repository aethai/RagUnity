using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    public sealed class RoActorAppearanceMetadata {
        private const int ActorAppearanceCacheMagic = 0x41504341; // ACPA
        private const int ActorAppearanceCacheVersion = 3; // 2: robe names from RobeNameTable only; 3: case-sensitive accessory ids
        private const int MaxCachedStringEntries = 1_000_000;
        private const int MaxCachedHairStyleRemaps = 100_000;

        private const int WeaponTypeNone = 0;
        private const int WeaponTypeShortSword = 1;
        private const int WeaponTypeSword = 2;
        private const int WeaponTypeTwoHandSword = 3;
        private const int WeaponTypeSpear = 4;
        private const int WeaponTypeTwoHandSpear = 5;
        private const int WeaponTypeAxe = 6;
        private const int WeaponTypeTwoHandAxe = 7;
        private const int WeaponTypeMace = 8;
        private const int WeaponTypeRod = 10;
        private const int WeaponTypeBow = 11;
        private const int WeaponTypeKnuckle = 12;
        private const int WeaponTypeInstrument = 13;
        private const int WeaponTypeWhip = 14;
        private const int WeaponTypeBook = 15;
        private const int WeaponTypeKatar = 16;
        private const int WeaponTypeGunHandgun = 17;
        private const int WeaponTypeGunRifle = 18;
        private const int WeaponTypeGunGatling = 19;
        private const int WeaponTypeGunShotgun = 20;
        private const int WeaponTypeGunGrenade = 21;
        private const int WeaponTypeShuriken = 22;
        private const int WeaponTypeTwoHandRod = 23;
        private const int WeaponTypeMax = 103;

        private static readonly Regex ConstantAssignmentPattern = new Regex(
            @"(?<key>[A-Za-z_][A-Za-z0-9_\.]*)\s*=\s*(?<value>-?(?:0x[0-9A-Fa-f]+|\d+))"
        );

        private static readonly Regex IndexedStringPattern = new Regex(
            @"\[\s*(?<key>[^\]]+)\s*\]\s*=\s*(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>"
        );

        private static readonly Regex BareStringPattern = new Regex(
            @"(?<![\w\.])(?<key>[A-Za-z_][A-Za-z0-9_\.]*)\s*=\s*(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>"
        );

        private static readonly Regex IndexedBlockPattern = new Regex(
            @"\[\s*(?<key>[^\]]+)\s*\]\s*=\s*\{(?<body>[^{}]*)\}",
            RegexOptions.Singleline
        );

        private readonly Dictionary<int, string> jobBodyNames;
        private readonly Dictionary<int, string> weaponJobBodyNames;
        private readonly Dictionary<int, GenderedName> genderedJobBodyNames;
        private readonly Dictionary<int, string> accessoryNames;
        private readonly Dictionary<int, string> robeNames;
        private readonly Dictionary<int, string> weaponSuffixes;
        private readonly Dictionary<int, string> shieldNames;
        private readonly int[] femaleHairStyleSpriteIds;
        private readonly int[] maleHairStyleSpriteIds;

        public static readonly RoActorAppearanceMetadata Empty = new RoActorAppearanceMetadata(
            new Dictionary<int, string>(),
            new Dictionary<int, string>(),
            new Dictionary<int, GenderedName>(),
            new Dictionary<int, string>(),
            new Dictionary<int, string>(),
            DefaultWeaponSuffixes(),
            DefaultShieldNames(),
            DefaultFemaleHairStyleSpriteIds(),
            DefaultMaleHairStyleSpriteIds(),
            false
        );

        private RoActorAppearanceMetadata(
            Dictionary<int, string> jobBodyNames,
            Dictionary<int, string> weaponJobBodyNames,
            Dictionary<int, GenderedName> genderedJobBodyNames,
            Dictionary<int, string> accessoryNames,
            Dictionary<int, string> robeNames,
            Dictionary<int, string> weaponSuffixes,
            Dictionary<int, string> shieldNames,
            int[] femaleHairStyleSpriteIds,
            int[] maleHairStyleSpriteIds,
            bool loadedFromDiskCache = false
        ) {
            this.jobBodyNames = jobBodyNames ?? new Dictionary<int, string>();
            this.weaponJobBodyNames = weaponJobBodyNames ?? new Dictionary<int, string>();
            this.genderedJobBodyNames = genderedJobBodyNames ?? new Dictionary<int, GenderedName>();
            this.accessoryNames = accessoryNames ?? new Dictionary<int, string>();
            this.robeNames = robeNames ?? new Dictionary<int, string>();
            this.weaponSuffixes = weaponSuffixes ?? DefaultWeaponSuffixes();
            this.shieldNames = shieldNames ?? DefaultShieldNames();
            this.femaleHairStyleSpriteIds = femaleHairStyleSpriteIds ?? DefaultFemaleHairStyleSpriteIds();
            this.maleHairStyleSpriteIds = maleHairStyleSpriteIds ?? DefaultMaleHairStyleSpriteIds();
            LoadedFromDiskCache = loadedFromDiskCache;
        }

        public int JobBodyNameCount => jobBodyNames.Count;
        public int WeaponJobBodyNameCount => weaponJobBodyNames.Count;
        public int GenderedJobBodyNameCount => genderedJobBodyNames.Count;
        public int AccessoryNameCount => accessoryNames.Count;
        public int RobeNameCount => robeNames.Count;
        public int WeaponSuffixCount => weaponSuffixes.Count;
        public int ShieldNameCount => shieldNames.Count;
        public int HairStyleRemapCount => femaleHairStyleSpriteIds.Length + maleHairStyleSpriteIds.Length;
        public bool HasJobBodyData => jobBodyNames.Count > 0 || weaponJobBodyNames.Count > 0 || genderedJobBodyNames.Count > 0;
        public bool HasAccessoryData => accessoryNames.Count > 0;
        public bool HasRobeData => robeNames.Count > 0;
        public bool HasWeaponData => weaponSuffixes.Count > 0;
        public bool HasShieldData => shieldNames.Count > 0;
        public bool LoadedFromDiskCache { get; }

        public static RoActorAppearanceMetadata Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            string jobIdentityText = TryReadFirstTextTable(assets, LuaDataInfo("jobidentity"), out string jobIdentityPath, out byte[] jobIdentityBytes);
            string jobNameText = TryReadFirstTextTable(assets, LuaDataInfo("jobname", "jobname_f"), out string jobNamePath, out byte[] jobNameBytes);
            string pcJobNameGenderText = TryReadFirstTextTable(assets, LuaDataInfo("pcjobnamegender", "pcjobnamegender_f"), out string pcJobNameGenderPath, out byte[] pcJobNameGenderBytes);
            string accessoryIdText = TryReadFirstTextTable(assets, LuaDataInfo("accessoryid"), out string accessoryIdPath, out byte[] accessoryIdBytes);
            string accNameText = TryReadFirstTextTable(assets, LuaDataInfo("accname", "accname_f"), out string accNamePath, out byte[] accNameBytes);
            string spriteRobeIdText = TryReadFirstTextTable(assets, LuaDataInfo("spriterobeid"), out string spriteRobeIdPath, out byte[] spriteRobeIdBytes);
            string spriteRobeNameText = TryReadFirstTextTable(assets, LuaDataInfo("spriterobename", "spriterobename_f"), out string spriteRobeNamePath, out byte[] spriteRobeNameBytes);

            if (TryReadActorAppearanceCache(
                    assets,
                    jobIdentityPath,
                    jobIdentityBytes,
                    jobNamePath,
                    jobNameBytes,
                    pcJobNameGenderPath,
                    pcJobNameGenderBytes,
                    accessoryIdPath,
                    accessoryIdBytes,
                    accNamePath,
                    accNameBytes,
                    spriteRobeIdPath,
                    spriteRobeIdBytes,
                    spriteRobeNamePath,
                    spriteRobeNameBytes,
                    out RoActorAppearanceMetadata cached)) {
                return cached;
            }

            RoActorAppearanceMetadata metadata = FromText(
                jobIdentityText,
                jobNameText,
                pcJobNameGenderText,
                accessoryIdText,
                accNameText,
                spriteRobeIdText,
                spriteRobeNameText
            );
            TryWriteActorAppearanceCache(
                assets,
                jobIdentityPath,
                jobIdentityBytes,
                jobNamePath,
                jobNameBytes,
                pcJobNameGenderPath,
                pcJobNameGenderBytes,
                accessoryIdPath,
                accessoryIdBytes,
                accNamePath,
                accNameBytes,
                spriteRobeIdPath,
                spriteRobeIdBytes,
                spriteRobeNamePath,
                spriteRobeNameBytes,
                metadata);
            return metadata;
        }

        public static RoActorAppearanceMetadata FromText(
            string jobIdentityText = null,
            string jobNameText = null,
            string pcJobNameGenderText = null,
            string accessoryIdText = null,
            string accNameText = null,
            string spriteRobeIdText = null,
            string spriteRobeNameText = null
        ) {
            var jobIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // Lua names are case-sensitive: accessoryid has ACCESSORY_SNAKE_HAT = 393 and ACCESSORY_Snake_Hat = 986
            var accessoryIds = new Dictionary<string, int>(StringComparer.Ordinal);
            var robeIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            ParseConstants(jobIdentityText, jobIds);
            ParseConstants(accessoryIdText, accessoryIds);
            ParseConstants(spriteRobeIdText, robeIds);

            var jobBodyNames = new Dictionary<int, string>();
            var genderedJobBodyNames = new Dictionary<int, GenderedName>();
            var accessoryNames = new Dictionary<int, string>();
            var robeNames = new Dictionary<int, string>();

            ParseIndexedStringTable(jobNameText, jobIds, jobBodyNames);
            ParseIndexedGenderTable(pcJobNameGenderText, jobIds, genderedJobBodyNames);
            ParseIndexedStringTable(accNameText, accessoryIds, accessoryNames);
            // the client asks ReqRobSprName (SpriteRobeName_F), which reads RobeNameTable only: the file's
            // RobeNameTable_Eng (other names for the same ids) and RobeTopLayer are not sprite names
            ParseIndexedStringTable(ExtractLuaTable(spriteRobeNameText, "RobeNameTable"), robeIds, robeNames);

            var weaponJobBodyNames = new Dictionary<int, string>(jobBodyNames);
            ApplyWeaponJobInheritance(jobIds, jobBodyNames, weaponJobBodyNames);

            return new RoActorAppearanceMetadata(
                jobBodyNames,
                weaponJobBodyNames,
                genderedJobBodyNames,
                accessoryNames,
                robeNames,
                DefaultWeaponSuffixes(),
                DefaultShieldNames(),
                DefaultFemaleHairStyleSpriteIds(),
                DefaultMaleHairStyleSpriteIds(),
                false
            );
        }

        public bool TryResolveBodyBasePath(RoAssetDatabase assets, int classId, bool isMale, out string basePath) {
            basePath = null;
            if (!TryGetBodyName(classId, isMale, out string bodyName)) {
                return false;
            }

            string sex = SexSegment(isMale);
            basePath = $"data/sprite/{HumanRaceSegment()}/{BodySegment()}/{sex}/{bodyName}_{sex}";
            return ValidateSpritePair(assets, basePath, "actor body sprite");
        }

        /// <summary>The Doram classes (Summoner, its rebirth and expanded jobs): their heads and hair palettes live under 도람족.</summary>
        public static bool IsDoramClass(int classId) {
            return (classId >= 4217 && classId <= 4220) || classId == 4308 || classId == 4315;
        }

        public bool TryResolveHeadBasePath(RoAssetDatabase assets, int headId, bool isMale, out string basePath, bool doram = false) {
            basePath = null;
            if (headId < 0) {
                return false;
            }

            string sex = SexSegment(isMale);
            int resolvedHeadId = NormalizeHairStyleSpriteId(headId);
            basePath = BuildHeadBasePath(sex, resolvedHeadId, doram);
            if (ValidateSpritePair(assets, basePath, "actor head sprite")) {
                return true;
            }

            // the legacy style remap is the human heads'
            if (doram) {
                return false;
            }

            int legacyHeadId = ResolveLegacyHairStyleSpriteId(headId, isMale);
            if (legacyHeadId != resolvedHeadId) {
                basePath = BuildHeadBasePath(sex, legacyHeadId);
                if (ValidateSpritePair(assets, basePath, "actor head sprite")) {
                    return true;
                }
            }

            return false;
        }

        public bool TryResolveHairBasePath(RoAssetDatabase assets, int hairId, bool isMale, out string basePath) {
            basePath = null;
            if (hairId < 0) {
                return false;
            }

            string sex = SexSegment(isMale);
            int resolvedHairId = NormalizeHairStyleSpriteId(hairId);
            basePath = BuildHairBasePath(sex, resolvedHairId);
            if (ValidateSpritePair(assets, basePath, "actor hair sprite")) {
                return true;
            }

            int legacyHairId = ResolveLegacyHairStyleSpriteId(hairId, isMale);
            if (legacyHairId != resolvedHairId) {
                basePath = BuildHairBasePath(sex, legacyHairId);
                if (ValidateSpritePair(assets, basePath, "actor hair sprite")) {
                    return true;
                }
            }

            return false;
        }

        public bool TryResolveHeadgearBasePath(RoAssetDatabase assets, int headgearId, bool isMale, out string basePath) {
            basePath = null;
            if (headgearId <= 0 || !accessoryNames.TryGetValue(headgearId, out string accessoryName)) {
                return false;
            }

            string sex = SexSegment(isMale);
            string fileName = PrefixSexIfNeeded(sex, NormalizeSpriteName(accessoryName));
            basePath = $"data/sprite/{AccessorySegment()}/{sex}/{fileName}";
            return ValidateSpritePair(assets, basePath, "actor headgear sprite");
        }

        /// <summary>
        /// A garment's sprite like the client (<see cref="RoRobeSpriteRules"/>): <c>로브/{name}/{name}.spr</c> when it exists,
        /// else the per-job .spr, with the per-job <c>로브/{name}/{sex}/{job}_{sex}.act</c>. <paramref name="basePath"/> is a
        /// <see cref="RoSpritePairPath"/> (two bases when the .spr is the shared one).
        /// </summary>
        /// <param name="bodyClassId">The sprite job: mounts, costumes and all-riding applied.</param>
        /// <param name="baseClassId">The actor's own job (no mount).</param>
        public bool TryResolveRobeBasePath(
            RoAssetDatabase assets,
            int robeId,
            int bodyClassId,
            int baseClassId,
            bool isMale,
            out string basePath
        ) {
            Func<string, bool> exists = assets != null ? assets.Exists : (Func<string, bool>) null;
            if (!TryResolveRobeBasePaths(robeId, bodyClassId, baseClassId, isMale, exists, out string spriteBasePath, out string actionBasePath)) {
                basePath = null;
                return false;
            }

            basePath = RoSpritePairPath.Compose(actionBasePath, spriteBasePath);
            return ValidateFile(assets, spriteBasePath + ".spr", "actor robe sprite") &
                ValidateFile(assets, actionBasePath + ".act", "actor robe sprite");
        }

        /// <summary>The robe's .spr and .act base paths, <paramref name="exists"/> answering the shared .spr probe.</summary>
        public bool TryResolveRobeBasePaths(
            int robeId,
            int bodyClassId,
            int baseClassId,
            bool isMale,
            Func<string, bool> exists,
            out string spriteBasePath,
            out string actionBasePath
        ) {
            spriteBasePath = null;
            actionBasePath = null;
            if (robeId <= 0 ||
                RoRobeSpriteRules.IsRobeHidden(bodyClassId, isMale) ||
                !robeNames.TryGetValue(robeId, out string robeName)) {
                return false;
            }

            return RoRobeSpriteRules.TryResolveBasePaths(
                RobeSegment(),
                NormalizeSpriteName(robeName),
                SexSegment(isMale),
                bodyClassId,
                baseClassId,
                exists,
                out spriteBasePath,
                out actionBasePath);
        }

        public bool TryResolveWeaponBasePath(
            RoAssetDatabase assets,
            int classId,
            bool isMale,
            int weaponItemId,
            int weaponClass,
            out string basePath
        ) {
            basePath = null;
            int viewId = ResolveWeaponViewId(assets, weaponItemId, weaponClass);
            if (viewId <= 0 || !TryGetWeaponJobBodyName(classId, isMale, out string baseClass)) {
                return false;
            }

            string sex = SexSegment(isMale);
            string suffix = weaponSuffixes.TryGetValue(viewId, out string knownSuffix)
                ? knownSuffix
                : "_" + viewId.ToString(CultureInfo.InvariantCulture);
            basePath = $"data/sprite/{HumanRaceSegment()}/{baseClass}/{baseClass}_{sex}{suffix}";
            return ValidateSpritePair(assets, basePath, "actor weapon sprite");
        }

        public bool TryResolveWeaponTrailBasePath(
            RoAssetDatabase assets,
            int classId,
            bool isMale,
            int weaponItemId,
            int weaponClass,
            out string basePath
        ) {
            basePath = null;
            int viewId = ResolveWeaponViewId(assets, weaponItemId, weaponClass);
            if (viewId <= 0 ||
                !weaponSuffixes.TryGetValue(viewId, out string weaponSuffix) ||
                string.IsNullOrEmpty(weaponSuffix) ||
                !TryGetWeaponJobBodyName(classId, isMale, out string baseClass)) {
                return false;
            }

            string sex = SexSegment(isMale);
            // The weapon trail (검광 "blade glow") sprite mirrors the weapon sprite path, appending the
            // glow suffix to the weapon name — exactly like the original client / roBrowser. Its ACT only
            // has frames for the attack actions, so it renders the slash arc and nothing else.
            basePath = $"data/sprite/{HumanRaceSegment()}/{baseClass}/{baseClass}_{sex}{weaponSuffix}{WeaponTrailGlowSuffix()}";
            if (assets == null) {
                return true;
            }

            // A missing trail sprite is normal for weapons that have none; check quietly so it does not
            // pollute the missing-asset diagnostics.
            if (assets.Exists($"{basePath}.spr") && assets.Exists($"{basePath}.act")) {
                return true;
            }

            basePath = null;
            return false;
        }

        public bool TryResolveShieldBasePath(
            RoAssetDatabase assets,
            int classId,
            bool isMale,
            int shieldItemId,
            out string basePath
        ) {
            basePath = null;
            if (shieldItemId <= 0) {
                return false;
            }

            if (IsDualWeaponShieldSlotId(shieldItemId)) {
                return TryResolveWeaponBasePath(assets, classId, isMale, shieldItemId, 0, out basePath);
            }

            int viewId = ResolveItemViewId(assets, shieldItemId);
            if (viewId <= 0) {
                viewId = shieldItemId;
            }

            if (!TryGetWeaponJobBodyName(classId, isMale, out string baseClass)) {
                return false;
            }

            string shieldName = shieldNames.TryGetValue(viewId, out string knownName)
                ? knownName
                : shieldNames[1];
            string sex = SexSegment(isMale);
            basePath = $"data/sprite/{ShieldSegment()}/{baseClass}/{baseClass}_{sex}_{shieldName}";
            return ValidateSpritePair(assets, basePath, "actor shield sprite");
        }

        public bool TryResolveBodyPalettePath(
            RoAssetDatabase assets,
            int classId,
            bool isMale,
            int bodyPaletteId,
            out string palettePath
        ) {
            palettePath = null;
            if (bodyPaletteId <= 0 || !TryGetBodyName(classId, isMale, out string bodyName)) {
                return false;
            }

            string sex = SexSegment(isMale);
            int paletteId = Math.Max(0, bodyPaletteId);
            palettePath = $"data/palette/{BodyPaletteSegment()}/{bodyName}_{sex}_{paletteId.ToString(CultureInfo.InvariantCulture)}.pal";
            return ValidateFile(assets, palettePath, "actor body palette");
        }

        public bool TryResolveHairPalettePath(
            RoAssetDatabase assets,
            int hairId,
            bool isMale,
            int hairColorId,
            out string palettePath,
            bool doram = false
        ) {
            palettePath = null;
            if (hairColorId <= 0 || hairId < 0) {
                return false;
            }

            string sex = SexSegment(isMale);
            int resolvedHairId = NormalizeHairStyleSpriteId(hairId);
            int paletteId = Math.Max(0, hairColorId);
            palettePath = BuildHairPalettePath(sex, resolvedHairId, paletteId, doram);
            if (ValidateFile(assets, palettePath, "actor hair palette")) {
                return true;
            }

            if (doram) {
                return false;
            }

            int legacyHairId = ResolveLegacyHairStyleSpriteId(hairId, isMale);
            if (legacyHairId != resolvedHairId) {
                palettePath = BuildHairPalettePath(sex, legacyHairId, paletteId);
                if (ValidateFile(assets, palettePath, "actor hair palette")) {
                    return true;
                }
            }

            return false;
        }

        public int ResolveHairStyleSpriteId(int hairStyleId, bool isMale) {
            return NormalizeHairStyleSpriteId(hairStyleId);
        }

        private int ResolveLegacyHairStyleSpriteId(int hairStyleId, bool isMale) {
            if (hairStyleId < 0) {
                return -1;
            }

            int[] table = isMale ? maleHairStyleSpriteIds : femaleHairStyleSpriteIds;
            if (hairStyleId < table.Length && table[hairStyleId] > 0) {
                return table[hairStyleId];
            }

            return hairStyleId;
        }

        private static int NormalizeHairStyleSpriteId(int hairStyleId) {
            return Math.Max(0, hairStyleId);
        }

        private static string BuildHeadBasePath(string sex, int headId, bool doram = false) {
            return $"data/sprite/{(doram ? DoramRaceSegment() : HumanRaceSegment())}/{HeadSegment()}/{sex}/{headId.ToString(CultureInfo.InvariantCulture)}_{sex}";
        }

        private static string BuildHairBasePath(string sex, int hairId) {
            return $"data/sprite/{HumanRaceSegment()}/{HairSegment()}/{sex}/{hairId.ToString(CultureInfo.InvariantCulture)}_{sex}";
        }

        private static string BuildHairPalettePath(string sex, int hairId, int paletteId, bool doram = false) {
            // data\palette\도람족\머리\머리{id}_{sex}_{n}.pal for the Doram
            string race = doram ? DoramRaceSegment() + "/" : string.Empty;
            return $"data/palette/{race}{HairSegment()}/{HairSegment()}{hairId.ToString(CultureInfo.InvariantCulture)}_{sex}_{paletteId.ToString(CultureInfo.InvariantCulture)}.pal";
        }

        private bool TryGetBodyName(int classId, bool isMale, out string bodyName) {
            if (genderedJobBodyNames.TryGetValue(Math.Max(0, classId), out GenderedName genderedName)) {
                bodyName = NormalizeSpriteName(isMale ? genderedName.Male : genderedName.Female);
                if (!string.IsNullOrEmpty(bodyName)) {
                    return true;
                }
            }

            if (jobBodyNames.TryGetValue(Math.Max(0, classId), out string jobBodyName)) {
                bodyName = NormalizeSpriteName(jobBodyName);
                return !string.IsNullOrEmpty(bodyName);
            }

            bodyName = null;
            return false;
        }

        private bool TryGetWeaponJobBodyName(int classId, bool isMale, out string bodyName) {
            if (weaponJobBodyNames.TryGetValue(Math.Max(0, classId), out string weaponJobBodyName)) {
                bodyName = NormalizeSpriteName(weaponJobBodyName);
                if (!string.IsNullOrEmpty(bodyName)) {
                    return true;
                }
            }

            bodyName = KnownWeaponJobBodyName(classId);
            if (!string.IsNullOrEmpty(bodyName)) {
                return true;
            }

            if (weaponJobBodyNames.TryGetValue(0, out weaponJobBodyName)) {
                bodyName = NormalizeSpriteName(weaponJobBodyName);
                if (!string.IsNullOrEmpty(bodyName)) {
                    return true;
                }
            }

            bodyName = KnownWeaponJobBodyName(0);
            if (!string.IsNullOrEmpty(bodyName)) {
                return true;
            }

            bodyName = NoviceSegment();
            return true;
        }

        private static string KnownWeaponJobBodyName(int classId) {
            switch (classId) {
                case 0:
                case 4001:
                case 4023:
                    return NoviceSegment();
                case 1:
                case 4002:
                case 4024:
                    return LegacyKorean(0xB0, 0xCB, 0xBB, 0xE7);
                case 2:
                case 4003:
                case 4025:
                    return LegacyKorean(0xB8, 0xB6, 0xB9, 0xFD, 0xBB, 0xE7);
                case 3:
                case 4004:
                case 4026:
                    return LegacyKorean(0xB1, 0xC3, 0xBC, 0xF6);
                case 4:
                case 4005:
                case 4027:
                    return LegacyKorean(0xBC, 0xBA, 0xC1, 0xF7, 0xC0, 0xDA);
                case 5:
                case 4006:
                case 4028:
                    return LegacyKorean(0xBB, 0xF3, 0xC0, 0xCE);
                case 6:
                case 4007:
                case 4029:
                    return LegacyKorean(0xB5, 0xB5, 0xB5, 0xCF);
                case 7:
                case 4008:
                case 4030:
                case 4054:
                case 4060:
                case 4096:
                case 4252:
                case 4265:
                case 4332:
                    return LegacyKorean(0xB1, 0xE2, 0xBB, 0xE7);
                case 13:
                case 4014:
                case 4036:
                case 4080:
                case 4081:
                case 4109:
                case 4280:
                case 4345:
                    return LegacyKorean(0xC6, 0xE4, 0xC4, 0xDA, 0xC6, 0xE4, 0xC4, 0xDA, 0x5F, 0xB1, 0xE2, 0xBB, 0xE7);
                case 8:
                case 4009:
                case 4031:
                case 4057:
                case 4063:
                case 4099:
                case 4256:
                case 4269:
                case 4336:
                    return LegacyKorean(0xC7, 0xC1, 0xB8, 0xAE, 0xBD, 0xBA, 0xC6, 0xAE);
                case 9:
                case 4010:
                case 4032:
                case 4055:
                case 4061:
                case 4097:
                case 4255:
                case 4268:
                case 4335:
                    return LegacyKorean(0xC0, 0xA7, 0xC0, 0xFA, 0xB5, 0xE5);
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
                    return LegacyKorean(0xC1, 0xA6, 0xC3, 0xB6, 0xB0, 0xF8);
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
                    return LegacyKorean(0xC7, 0xE5, 0xC5, 0xCD);
                case 12:
                case 4013:
                case 4035:
                case 4059:
                case 4065:
                case 4101:
                case 4254:
                case 4267:
                case 4334:
                    return LegacyKorean(0xBE, 0xEE, 0xBC, 0xBC, 0xBD, 0xC5);
                case 14:
                case 4015:
                case 4037:
                case 4066:
                case 4073:
                case 4102:
                case 4258:
                case 4271:
                case 4338:
                    return LegacyKorean(0xC5, 0xA9, 0xB7, 0xE7, 0xBC, 0xBC, 0xC0, 0xCC, 0xB4, 0xF5);
                case 21:
                case 4022:
                case 4044:
                case 4082:
                case 4083:
                case 4110:
                case 4281:
                case 4348:
                    return LegacyKorean(0xBD, 0xC5, 0xC6, 0xE4, 0xC4, 0xDA, 0xC5, 0xA9, 0xB7, 0xE7, 0xBC, 0xBC, 0xC0, 0xCC, 0xB4, 0xF5);
                case 15:
                case 4016:
                case 4038:
                case 4070:
                case 4077:
                case 4106:
                case 4262:
                case 4275:
                case 4342:
                    return LegacyKorean(0xB8, 0xF9, 0xC5, 0xA9);
                case 16:
                case 4017:
                case 4039:
                case 4067:
                case 4074:
                case 4103:
                case 4261:
                case 4274:
                case 4341:
                    return LegacyKorean(0xBC, 0xBC, 0xC0, 0xCC, 0xC1, 0xF6);
                case 17:
                case 4018:
                case 4040:
                case 4072:
                case 4079:
                case 4108:
                case 4260:
                case 4273:
                case 4340:
                    return LegacyKorean(0xB7, 0xCE, 0xB1, 0xD7);
                case 18:
                case 4019:
                case 4041:
                case 4071:
                case 4078:
                case 4107:
                case 4259:
                case 4272:
                case 4339:
                    return LegacyKorean(0xBF, 0xAC, 0xB1, 0xDD, 0xBC, 0xFA, 0xBB, 0xE7);
                case 19:
                case 4020:
                case 4042:
                case 4068:
                case 4075:
                case 4104:
                case 4263:
                case 4276:
                case 4343:
                    return LegacyKorean(0xB9, 0xD9, 0xB5, 0xE5);
                case 20:
                case 4021:
                case 4043:
                case 4069:
                case 4076:
                case 4105:
                case 4264:
                case 4277:
                case 4344:
                    return LegacyKorean(0xB9, 0xAB, 0xC8, 0xF1);
                case 23:
                case 4045:
                case 4190:
                case 4191:
                    return LegacyKorean(0xBD, 0xB4, 0xC6, 0xDB, 0xB3, 0xEB, 0xBA, 0xF1, 0xBD, 0xBA);
                case 24:
                case 4228:
                    return LegacyKorean(0xB0, 0xC7, 0xB3, 0xCA);
                case 25:
                case 4222:
                    return LegacyKorean(0xB4, 0xD1, 0xC0, 0xDA);
                case 4211:
                    return "kagerou";
                case 4212:
                    return "oboro";
                case 4215:
                    return "rebellion";
                case 4218:
                case 4220:
                    return "summoner";
                default:
                    return null;
            }
        }

        private int ResolveWeaponViewId(RoAssetDatabase assets, int weaponItemId, int weaponClass) {
            int viewId = ResolveItemViewId(assets, weaponItemId);
            if (viewId > 0) {
                return viewId;
            }

            if (weaponClass > 0) {
                return weaponClass;
            }

            return ResolveWeaponViewIdFromItemRange(weaponItemId);
        }

        private static int ResolveItemViewId(RoAssetDatabase assets, int itemId) {
            if (itemId <= 0 || assets?.GameData?.Items == null) {
                return 0;
            }

            if (!assets.GameData.Items.TryGetItem(itemId, out RoItemMetadata item) || item == null) {
                return 0;
            }

            if (item.ClassNumber > 0) {
                return item.ClassNumber;
            }

            return item.ViewId;
        }

        private static int ResolveWeaponViewIdFromItemRange(int itemId) {
            if (itemId <= 0) {
                return 0;
            }

            if (itemId < WeaponTypeMax) {
                return itemId;
            }

            if (itemId < 1100) {
                return WeaponTypeNone;
            }

            if (itemId >= 1116 && itemId <= 1118) {
                return WeaponTypeTwoHandSword;
            }

            if (itemId >= 1314 && itemId <= 1315) {
                return WeaponTypeTwoHandAxe;
            }

            if (itemId >= 1410 && itemId <= 1412) {
                return WeaponTypeTwoHandSpear;
            }

            if (itemId >= 1472 && itemId <= 1473) {
                return WeaponTypeRod;
            }

            if (itemId == 1599) {
                return WeaponTypeMace;
            }

            if (IsOneOf(itemId, 13157, 13158, 13159, 13172, 13177)) {
                return WeaponTypeGunGatling;
            }

            if (IsOneOf(itemId, 13154, 13155, 13156, 13167, 13168, 13169, 13173, 13178)) {
                return WeaponTypeGunShotgun;
            }

            if (IsOneOf(itemId, 13160, 13161, 13162, 13174, 13179)) {
                return WeaponTypeGunGrenade;
            }

            if (itemId < 1150) {
                return WeaponTypeSword;
            }

            if (itemId < 1200) {
                return WeaponTypeTwoHandSword;
            }

            if (itemId < 1250) {
                return WeaponTypeShortSword;
            }

            if (itemId < 1300) {
                return WeaponTypeKatar;
            }

            if (itemId < 1350) {
                return WeaponTypeAxe;
            }

            if (itemId < 1400) {
                return WeaponTypeTwoHandAxe;
            }

            if (itemId < 1450) {
                return WeaponTypeSpear;
            }

            if (itemId < 1500) {
                return WeaponTypeTwoHandSpear;
            }

            if (itemId < 1550) {
                return WeaponTypeMace;
            }

            if (itemId < 1600) {
                return WeaponTypeBook;
            }

            if (itemId < 1700) {
                return WeaponTypeRod;
            }

            if (itemId < 1750) {
                return WeaponTypeBow;
            }

            if (itemId < 1800) {
                return WeaponTypeNone;
            }

            if (itemId < 1850) {
                return WeaponTypeKnuckle;
            }

            if (itemId < 1900) {
                return WeaponTypeNone;
            }

            if (itemId < 1950) {
                return WeaponTypeInstrument;
            }

            if (itemId < 2000) {
                return WeaponTypeWhip;
            }

            if (itemId < 2050) {
                return WeaponTypeTwoHandRod;
            }

            if (itemId >= 13000 && itemId < 13050) {
                return WeaponTypeShortSword;
            }

            if (itemId >= 13100 && itemId < 13150) {
                return WeaponTypeGunHandgun;
            }

            if (itemId >= 13150 && itemId < 13200) {
                return WeaponTypeGunRifle;
            }

            if (itemId >= 13300 && itemId < 13350) {
                return WeaponTypeShuriken;
            }

            if (itemId >= 13400 && itemId < 13450) {
                return WeaponTypeSword;
            }

            if (itemId >= 18100 && itemId < 18150) {
                return WeaponTypeBow;
            }

            if (itemId >= 20000 && itemId < 21000) {
                return WeaponTypeTwoHandRod;
            }

            if (itemId >= 21000 && itemId < 22000) {
                return WeaponTypeTwoHandSword;
            }

            return WeaponTypeNone;
        }

        private static bool IsDualWeaponShieldSlotId(int itemId) {
            if (itemId <= 500) {
                return false;
            }

            return !IsShieldItemId(itemId);
        }

        private static bool IsShieldItemId(int itemId) {
            return (itemId >= 2100 && itemId <= 2199) ||
                (itemId >= 28900 && itemId <= 28999) ||
                (itemId >= 460000 && itemId <= 460099);
        }

        private static bool IsOneOf(int value, params int[] values) {
            for (int i = 0; i < values.Length; i++) {
                if (value == values[i]) {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyWeaponJobInheritance(
            IReadOnlyDictionary<string, int> jobIds,
            IReadOnlyDictionary<int, string> sourceNames,
            Dictionary<int, string> outputNames
        ) {
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "NOVICE", "NOVICE_H", "NOVICE_B");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "SWORDMAN", "SWORDMAN_H", "SWORDMAN_B");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "MAGICIAN", "MAGICIAN_H", "MAGICIAN_B");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "ARCHER", "ARCHER_H", "ARCHER_B");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "ACOLYTE", "ACOLYTE_H", "ACOLYTE_B");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "MERCHANT", "MERCHANT_H", "MERCHANT_B");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "THIEF", "THIEF_H", "THIEF_B");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "KNIGHT", "KNIGHT_H", "KNIGHT_B", "RUNE_KNIGHT", "RUNE_KNIGHT_H", "RUNE_KNIGHT_B", "RUNE_KNIGHT_2ND", "DRAGON_KNIGHT", "DRAGON_KNIGHT_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "KNIGHT2", "KNIGHT2_H", "KNIGHT2_B", "RUNE_KNIGHT2", "RUNE_KNIGHT2_H", "RUNE_KNIGHT2_B", "RUNE_KNIGHT2_2ND", "DRAGON_KNIGHT2");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "PRIEST", "PRIEST_H", "PRIEST_B", "ARCHBISHOP", "ARCHBISHOP_H", "ARCHBISHOP_B", "ARCH_BISHOP_2ND", "CARDINAL", "CARDINAL_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "WIZARD", "WIZARD_H", "WIZARD_B", "WARLOCK", "WARLOCK_H", "WARLOCK_B", "WARLOCK_2ND", "ARCH_MAGE", "ARCH_MAGE_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "BLACKSMITH", "BLACKSMITH_H", "BLACKSMITH_B", "MECHANIC", "MECHANIC_H", "MECHANIC_B", "MECHANIC_2ND", "MECHANIC2_2ND", "MEISTER", "MEISTER2", "MEISTER_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "HUNTER", "HUNTER_H", "HUNTER_B", "RANGER", "RANGER_H", "RANGER_B", "RANGER_2ND", "RANGER2_2ND", "WINDHAWK", "WINDHAWK2", "WINDHAWK_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "ASSASSIN", "ASSASSIN_H", "ASSASSIN_B", "GUILLOTINE_CROSS", "GUILLOTINE_CROSS_H", "GUILLOTINE_CROSS_B", "GUILLOTINE_CROSS_2ND", "SHADOW_CROSS", "SHADOW_CROSS_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "CRUSADER", "CRUSADER_H", "CRUSADER_B", "ROYAL_GUARD", "ROYAL_GUARD_H", "ROYAL_GUARD_B", "ROYAL_GUARD_2ND", "IMPERIAL_GUARD", "IMPERIAL_GUARD_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "CRUSADER2", "CRUSADER2_H", "CRUSADER2_B", "ROYAL_GUARD2", "ROYAL_GUARD2_H", "ROYAL_GUARD2_B", "ROYAL_GUARD2_2ND", "IMPERIAL_GUARD2");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "MONK", "MONK_H", "MONK_B", "SURA", "SURA_H", "SURA_B", "SURA_2ND", "INQUISITOR", "INQUISITOR_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "SAGE", "SAGE_H", "SAGE_B", "SORCERER", "SORCERER_H", "SORCERER_B", "SORCERER_2ND", "ELEMENTAL_MASTER", "ELEMENTAL_MASTER_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "ROGUE", "ROGUE_H", "ROGUE_B", "SHADOW_CHASER", "SHADOW_CHASER_H", "SHADOW_CHASER_B", "SHADOW_CHASER_2ND", "ABYSS_CHASER", "ABYSS_CHASER_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "ALCHEMIST", "ALCHEMIST_H", "ALCHEMIST_B", "GENETIC", "GENETIC_H", "GENETIC_B", "GENETIC_2ND", "BIOLO", "BIOLO_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "BARD", "BARD_H", "BARD_B", "MINSTREL", "MINSTREL_H", "MINSTREL_B", "MINSTREL_2ND", "TROUBADOUR", "TROUBADOUR_RIDING");
            DuplicateWeaponJobEntry(jobIds, sourceNames, outputNames, "DANCER", "DANCER_H", "DANCER_B", "WANDERER", "WANDERER_H", "WANDERER_B", "WANDERER_2ND", "TROUVERE", "TROUVERE_RIDING");
        }

        private static void DuplicateWeaponJobEntry(
            IReadOnlyDictionary<string, int> jobIds,
            IReadOnlyDictionary<int, string> sourceNames,
            Dictionary<int, string> outputNames,
            string origin,
            params string[] aliases
        ) {
            if (!TryResolveJobConstant(jobIds, origin, out int originId) ||
                !sourceNames.TryGetValue(originId, out string originName) ||
                string.IsNullOrWhiteSpace(originName)) {
                return;
            }

            for (int i = 0; i < aliases.Length; i++) {
                if (TryResolveJobConstant(jobIds, aliases[i], out int aliasId)) {
                    outputNames[aliasId] = originName;
                }
            }
        }

        private static bool TryResolveJobConstant(IReadOnlyDictionary<string, int> jobIds, string name, out int id) {
            id = 0;
            if (jobIds == null || string.IsNullOrWhiteSpace(name)) {
                return false;
            }

            if (jobIds.TryGetValue(name, out id)) {
                return true;
            }

            return jobIds.TryGetValue("JT_" + name, out id);
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

        private static bool ValidateFile(RoAssetDatabase assets, string path, string expectedType) {
            if (assets == null) {
                return !string.IsNullOrWhiteSpace(path);
            }

            if (assets.Exists(path)) {
                return true;
            }

            RecordMissing(assets, path, expectedType);
            return false;
        }

        private static void RecordMissing(RoAssetDatabase assets, string path, string expectedType) {
            assets.Diagnostics.RecordMissing(path, expectedType, RoAssetPath.GetLookupCandidates(path));
        }

        private static void ParseConstants(string text, Dictionary<string, int> constants) {
            if (string.IsNullOrWhiteSpace(text) || constants == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match match in ConstantAssignmentPattern.Matches(stripped)) {
                string key = NormalizeLuaKey(match.Groups["key"].Value);
                if (string.IsNullOrEmpty(key) || !TryParseInteger(match.Groups["value"].Value, out int value)) {
                    continue;
                }

                constants[key] = value;
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

        private static void ParseIndexedGenderTable(string text, IReadOnlyDictionary<string, int> constants, Dictionary<int, GenderedName> output) {
            if (string.IsNullOrWhiteSpace(text) || output == null) {
                return;
            }

            string stripped = StripLineComments(text);
            foreach (Match block in IndexedBlockPattern.Matches(stripped)) {
                if (!TryResolveTableKey(block.Groups["key"].Value, constants, out int id)) {
                    continue;
                }

                string body = block.Groups["body"].Value;
                string female = null;
                string male = null;
                foreach (Match entry in IndexedStringPattern.Matches(body)) {
                    string key = NormalizeLuaKey(entry.Groups["key"].Value);
                    string value = UnescapeLuaString(entry.Groups["value"].Value);
                    if (IsFemaleKey(key)) {
                        female = value;
                    } else if (IsMaleKey(key)) {
                        male = value;
                    }
                }

                if (string.IsNullOrEmpty(female) || string.IsNullOrEmpty(male)) {
                    var values = new List<string>();
                    foreach (Match valueMatch in QuotedStringPattern().Matches(body)) {
                        values.Add(UnescapeLuaString(valueMatch.Groups["value"].Value));
                    }

                    if (values.Count >= 2) {
                        female = string.IsNullOrEmpty(female) ? values[0] : female;
                        male = string.IsNullOrEmpty(male) ? values[1] : male;
                    } else if (values.Count == 1) {
                        female = string.IsNullOrEmpty(female) ? values[0] : female;
                        male = string.IsNullOrEmpty(male) ? values[0] : male;
                    }
                }

                if (!string.IsNullOrEmpty(female) || !string.IsNullOrEmpty(male)) {
                    output[id] = new GenderedName(female ?? male, male ?? female);
                }
            }
        }

        private static Regex QuotedStringPattern() {
            return new Regex(@"(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>");
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

        private static string NormalizeSpriteName(string spriteName) {
            if (string.IsNullOrWhiteSpace(spriteName)) {
                return string.Empty;
            }

            spriteName = spriteName.Replace('\\', '/').Trim();
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(spriteName);
            if (!string.IsNullOrEmpty(decoded)) {
                spriteName = decoded;
            }

            if (spriteName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ||
                spriteName.EndsWith(".act", StringComparison.OrdinalIgnoreCase)) {
                spriteName = spriteName.Substring(0, spriteName.Length - 4);
            }

            int slashIndex = spriteName.LastIndexOf('/');
            return slashIndex >= 0 ? spriteName.Substring(slashIndex + 1) : spriteName;
        }

        private static string PrefixSexIfNeeded(string sex, string spriteName) {
            if (string.IsNullOrEmpty(spriteName)) {
                return string.Empty;
            }

            return spriteName.StartsWith(sex, StringComparison.Ordinal) ? spriteName : sex + spriteName;
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
                        chars.Add(next);
                        break;
                }
            }

            string unescaped = new string(chars.ToArray());
            string decoded = RoAssetPath.TryDecodeLegacyBytesAsKorean(unescaped);
            return string.IsNullOrEmpty(decoded) ? unescaped : decoded;
        }

        /// <summary>
        /// The <c>{ ... }</c> body of the global table <paramref name="tableName"/> (braces inside quoted strings skipped);
        /// the whole text when the file has no such table.
        /// </summary>
        private static string ExtractLuaTable(string text, string tableName) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }

            Match start = new Regex(@"(?<![\w\.])" + Regex.Escape(tableName) + @"\s*=\s*\{").Match(text);
            if (!start.Success) {
                return text;
            }

            int open = start.Index + start.Length - 1;
            int depth = 0;
            char quote = '\0';
            for (int i = open; i < text.Length; i++) {
                char c = text[i];
                if (quote != '\0') {
                    if (c == '\\') {
                        i++;
                    } else if (c == quote) {
                        quote = '\0';
                    }

                    continue;
                }

                if (c == '"' || c == '\'') {
                    quote = c;
                } else if (c == '-' && i + 1 < text.Length && text[i + 1] == '-') {
                    int end = text.IndexOf('\n', i);
                    i = end < 0 ? text.Length : end;
                } else if (c == '{') {
                    depth++;
                } else if (c == '}' && --depth == 0) {
                    return text.Substring(open, i - open + 1);
                }
            }

            return text.Substring(open);
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

        private static bool IsFemaleKey(string key) {
            if (string.IsNullOrEmpty(key)) {
                return false;
            }

            key = key.ToLowerInvariant();
            return key == "0" || key.Contains("female") || key.Contains("woman") || key.Contains("\uC5EC");
        }

        private static bool IsMaleKey(string key) {
            if (string.IsNullOrEmpty(key)) {
                return false;
            }

            key = key.ToLowerInvariant();
            return key == "1" || key.Contains("male") || key.Contains("man") || key.Contains("\uB0A8");
        }

        private static string TryReadFirstTextTable(
            RoAssetDatabase assets,
            IReadOnlyList<string> candidatePaths,
            out string sourcePath,
            out byte[] sourceBytes
        ) {
            sourcePath = null;
            sourceBytes = null;
            if (assets == null || candidatePaths == null) {
                return null;
            }

            for (int i = 0; i < candidatePaths.Count; i++) {
                string candidate = candidatePaths[i];
                if (!assets.Exists(candidate)) {
                    continue;
                }

                byte[] bytes = assets.ReadBytes(candidate);
                if (bytes == null || bytes.Length == 0) {
                    continue;
                }

                byte[] text = bytes;
                if (bytes[0] == 0x1B) {
                    text = RoLuaBytecodeSource.ToSource(assets, candidate, bytes, out _);
                    if (text == null) {
                        continue;
                    }
                }

                sourcePath = candidate;
                sourceBytes = bytes;
                return RoTextEncoding.Decode(text);
            }

            return null;
        }

        private static bool TryReadActorAppearanceCache(
            RoAssetDatabase assets,
            string jobIdentitySourcePath,
            byte[] jobIdentityBytes,
            string jobNameSourcePath,
            byte[] jobNameBytes,
            string pcJobNameGenderSourcePath,
            byte[] pcJobNameGenderBytes,
            string accessoryIdSourcePath,
            byte[] accessoryIdBytes,
            string accNameSourcePath,
            byte[] accNameBytes,
            string spriteRobeIdSourcePath,
            byte[] spriteRobeIdBytes,
            string spriteRobeNameSourcePath,
            byte[] spriteRobeNameBytes,
            out RoActorAppearanceMetadata metadata
        ) {
            metadata = null;
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null ||
                !HasAnySourceBytes(
                    jobIdentityBytes,
                    jobNameBytes,
                    pcJobNameGenderBytes,
                    accessoryIdBytes,
                    accNameBytes,
                    spriteRobeIdBytes,
                    spriteRobeNameBytes)) {
                return false;
            }

            string cacheKey = BuildActorAppearanceCacheKey(
                jobIdentitySourcePath,
                jobIdentityBytes,
                jobNameSourcePath,
                jobNameBytes,
                pcJobNameGenderSourcePath,
                pcJobNameGenderBytes,
                accessoryIdSourcePath,
                accessoryIdBytes,
                accNameSourcePath,
                accNameBytes,
                spriteRobeIdSourcePath,
                spriteRobeIdBytes,
                spriteRobeNameSourcePath,
                spriteRobeNameBytes);
            try {
                if (!cache.TryReadBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", out byte[] data)) {
                    return false;
                }

                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (reader.ReadInt32() != ActorAppearanceCacheMagic ||
                    reader.ReadInt32() != ActorAppearanceCacheVersion) {
                    return false;
                }

                if (!IsSameSourcePath(reader.ReadString(), jobIdentitySourcePath) ||
                    !IsSameSourcePath(reader.ReadString(), jobNameSourcePath) ||
                    !IsSameSourcePath(reader.ReadString(), pcJobNameGenderSourcePath) ||
                    !IsSameSourcePath(reader.ReadString(), accessoryIdSourcePath) ||
                    !IsSameSourcePath(reader.ReadString(), accNameSourcePath) ||
                    !IsSameSourcePath(reader.ReadString(), spriteRobeIdSourcePath) ||
                    !IsSameSourcePath(reader.ReadString(), spriteRobeNameSourcePath)) {
                    return false;
                }

                metadata = new RoActorAppearanceMetadata(
                    ReadStringDictionary(reader),
                    ReadStringDictionary(reader),
                    ReadGenderedNameDictionary(reader),
                    ReadStringDictionary(reader),
                    ReadStringDictionary(reader),
                    ReadStringDictionary(reader),
                    ReadStringDictionary(reader),
                    ReadIntArray(reader),
                    ReadIntArray(reader),
                    true);
                return true;
            } catch {
                metadata = null;
                return false;
            }
        }

        private static void TryWriteActorAppearanceCache(
            RoAssetDatabase assets,
            string jobIdentitySourcePath,
            byte[] jobIdentityBytes,
            string jobNameSourcePath,
            byte[] jobNameBytes,
            string pcJobNameGenderSourcePath,
            byte[] pcJobNameGenderBytes,
            string accessoryIdSourcePath,
            byte[] accessoryIdBytes,
            string accNameSourcePath,
            byte[] accNameBytes,
            string spriteRobeIdSourcePath,
            byte[] spriteRobeIdBytes,
            string spriteRobeNameSourcePath,
            byte[] spriteRobeNameBytes,
            RoActorAppearanceMetadata metadata
        ) {
            RoAssetDiskCache cache = assets?.DiskCache;
            if (cache == null ||
                metadata == null ||
                !HasAnySourceBytes(
                    jobIdentityBytes,
                    jobNameBytes,
                    pcJobNameGenderBytes,
                    accessoryIdBytes,
                    accNameBytes,
                    spriteRobeIdBytes,
                    spriteRobeNameBytes)) {
                return;
            }

            string cacheKey = BuildActorAppearanceCacheKey(
                jobIdentitySourcePath,
                jobIdentityBytes,
                jobNameSourcePath,
                jobNameBytes,
                pcJobNameGenderSourcePath,
                pcJobNameGenderBytes,
                accessoryIdSourcePath,
                accessoryIdBytes,
                accNameSourcePath,
                accNameBytes,
                spriteRobeIdSourcePath,
                spriteRobeIdBytes,
                spriteRobeNameSourcePath,
                spriteRobeNameBytes);
            try {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                    writer.Write(ActorAppearanceCacheMagic);
                    writer.Write(ActorAppearanceCacheVersion);
                    writer.Write(jobIdentitySourcePath ?? string.Empty);
                    writer.Write(jobNameSourcePath ?? string.Empty);
                    writer.Write(pcJobNameGenderSourcePath ?? string.Empty);
                    writer.Write(accessoryIdSourcePath ?? string.Empty);
                    writer.Write(accNameSourcePath ?? string.Empty);
                    writer.Write(spriteRobeIdSourcePath ?? string.Empty);
                    writer.Write(spriteRobeNameSourcePath ?? string.Empty);
                    WriteStringDictionary(writer, metadata.jobBodyNames);
                    WriteStringDictionary(writer, metadata.weaponJobBodyNames);
                    WriteGenderedNameDictionary(writer, metadata.genderedJobBodyNames);
                    WriteStringDictionary(writer, metadata.accessoryNames);
                    WriteStringDictionary(writer, metadata.robeNames);
                    WriteStringDictionary(writer, metadata.weaponSuffixes);
                    WriteStringDictionary(writer, metadata.shieldNames);
                    WriteIntArray(writer, metadata.femaleHairStyleSpriteIds);
                    WriteIntArray(writer, metadata.maleHairStyleSpriteIds);
                }

                cache.WriteBytes(RoAssetCacheCategory.GameData, cacheKey, ".bin", stream.ToArray());
            } catch {
                // Parsed actor appearance cache is an optimization only; source Lua remains authoritative.
            }
        }

        private static string BuildActorAppearanceCacheKey(
            string jobIdentitySourcePath,
            byte[] jobIdentityBytes,
            string jobNameSourcePath,
            byte[] jobNameBytes,
            string pcJobNameGenderSourcePath,
            byte[] pcJobNameGenderBytes,
            string accessoryIdSourcePath,
            byte[] accessoryIdBytes,
            string accNameSourcePath,
            byte[] accNameBytes,
            string spriteRobeIdSourcePath,
            byte[] spriteRobeIdBytes,
            string spriteRobeNameSourcePath,
            byte[] spriteRobeNameBytes
        ) {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                WriteSourceBytesForCacheKey(writer, jobIdentitySourcePath, jobIdentityBytes);
                WriteSourceBytesForCacheKey(writer, jobNameSourcePath, jobNameBytes);
                WriteSourceBytesForCacheKey(writer, pcJobNameGenderSourcePath, pcJobNameGenderBytes);
                WriteSourceBytesForCacheKey(writer, accessoryIdSourcePath, accessoryIdBytes);
                WriteSourceBytesForCacheKey(writer, accNameSourcePath, accNameBytes);
                WriteSourceBytesForCacheKey(writer, spriteRobeIdSourcePath, spriteRobeIdBytes);
                WriteSourceBytesForCacheKey(writer, spriteRobeNameSourcePath, spriteRobeNameBytes);
            }

            string sourcePathKey = string.Join(
                "|",
                jobIdentitySourcePath ?? string.Empty,
                jobNameSourcePath ?? string.Empty,
                pcJobNameGenderSourcePath ?? string.Empty,
                accessoryIdSourcePath ?? string.Empty,
                accNameSourcePath ?? string.Empty,
                spriteRobeIdSourcePath ?? string.Empty,
                spriteRobeNameSourcePath ?? string.Empty);
            return RoItemMetadataTable.BuildParsedSourceCacheKey("actor-appearance", sourcePathKey, stream.ToArray());
        }

        private static Dictionary<int, string> ReadStringDictionary(BinaryReader reader) {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxCachedStringEntries) {
                throw new InvalidDataException("Cached actor appearance string dictionary is too large.");
            }

            var values = new Dictionary<int, string>(count);
            for (int i = 0; i < count; i++) {
                values[reader.ReadInt32()] = reader.ReadString();
            }

            return values;
        }

        private static void WriteStringDictionary(BinaryWriter writer, IReadOnlyDictionary<int, string> values) {
            writer.Write(values?.Count ?? 0);
            if (values == null) {
                return;
            }

            foreach (KeyValuePair<int, string> value in values) {
                writer.Write(value.Key);
                writer.Write(value.Value ?? string.Empty);
            }
        }

        private static Dictionary<int, GenderedName> ReadGenderedNameDictionary(BinaryReader reader) {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxCachedStringEntries) {
                throw new InvalidDataException("Cached actor appearance gender dictionary is too large.");
            }

            var values = new Dictionary<int, GenderedName>(count);
            for (int i = 0; i < count; i++) {
                values[reader.ReadInt32()] = new GenderedName(reader.ReadString(), reader.ReadString());
            }

            return values;
        }

        private static void WriteGenderedNameDictionary(BinaryWriter writer, IReadOnlyDictionary<int, GenderedName> values) {
            writer.Write(values?.Count ?? 0);
            if (values == null) {
                return;
            }

            foreach (KeyValuePair<int, GenderedName> value in values) {
                writer.Write(value.Key);
                writer.Write(value.Value.Female ?? string.Empty);
                writer.Write(value.Value.Male ?? string.Empty);
            }
        }

        private static int[] ReadIntArray(BinaryReader reader) {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxCachedHairStyleRemaps) {
                throw new InvalidDataException("Cached actor appearance int array is too large.");
            }

            var values = new int[count];
            for (int i = 0; i < count; i++) {
                values[i] = reader.ReadInt32();
            }

            return values;
        }

        private static void WriteIntArray(BinaryWriter writer, int[] values) {
            writer.Write(values?.Length ?? 0);
            if (values == null) {
                return;
            }

            for (int i = 0; i < values.Length; i++) {
                writer.Write(values[i]);
            }
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

        private static string SexSegment(bool isMale) {
            return isMale ? MaleSegment() : FemaleSegment();
        }

        private static string HumanRaceSegment() {
            return LegacyKorean(0xC0, 0xCE, 0xB0, 0xA3, 0xC1, 0xB7);
        }

        private static string DoramRaceSegment() {
            return LegacyKorean(0xB5, 0xB5, 0xB6, 0xF7, 0xC1, 0xB7);
        }

        private static string BodySegment() {
            return LegacyKorean(0xB8, 0xF6, 0xC5, 0xEB);
        }

        private static string BodyPaletteSegment() {
            return LegacyKorean(0xB8, 0xF6);
        }

        private static string HeadSegment() {
            return LegacyKorean(0xB8, 0xD3, 0xB8, 0xAE, 0xC5, 0xEB);
        }

        private static string HairSegment() {
            return LegacyKorean(0xB8, 0xD3, 0xB8, 0xAE);
        }

        private static string MaleSegment() {
            return LegacyKorean(0xB3, 0xB2);
        }

        private static string FemaleSegment() {
            return LegacyKorean(0xBF, 0xA9);
        }

        private static string NoviceSegment() {
            return LegacyKorean(0xC3, 0xCA, 0xBA, 0xB8, 0xC0, 0xDA);
        }

        private static string AccessorySegment() {
            return LegacyKorean(0xBE, 0xC7, 0xBC, 0xBC, 0xBB, 0xE7, 0xB8, 0xAE);
        }

        private static string RobeSegment() {
            return LegacyKorean(0xB7, 0xCE, 0xBA, 0xEA);
        }

        private static string ShieldSegment() {
            return LegacyKorean(0xB9, 0xE6, 0xC6, 0xD0);
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

        private static Dictionary<int, string> DefaultWeaponSuffixes() {
            return new Dictionary<int, string> {
                [WeaponTypeNone] = string.Empty,
                [WeaponTypeShortSword] = WeaponSuffix(0xB4, 0xDC, 0xB0, 0xCB),
                [WeaponTypeSword] = WeaponSuffix(0xB0, 0xCB),
                [WeaponTypeTwoHandSword] = WeaponSuffix(0xB0, 0xCB),
                [WeaponTypeSpear] = WeaponSuffix(0xC3, 0xA2),
                [WeaponTypeTwoHandSpear] = WeaponSuffix(0xC3, 0xA2),
                [WeaponTypeAxe] = WeaponSuffix(0xB5, 0xB5, 0xB3, 0xA2),
                [WeaponTypeTwoHandAxe] = WeaponSuffix(0xB5, 0xB5, 0xB3, 0xA2),
                [WeaponTypeMace] = WeaponSuffix(0xC5, 0xAC, 0xB7, 0xB4),
                [9] = WeaponSuffix(0xC5, 0xAC, 0xB7, 0xB4),
                [WeaponTypeRod] = WeaponSuffix(0xB7, 0xD4, 0xB5, 0xE5),
                [WeaponTypeBow] = WeaponSuffix(0xC8, 0xB0),
                [WeaponTypeKnuckle] = WeaponSuffix(0xB3, 0xCA, 0xC5, 0xAC),
                [WeaponTypeInstrument] = WeaponSuffix(0xBE, 0xC7, 0xB1, 0xE2),
                [WeaponTypeWhip] = WeaponSuffix(0xC3, 0xA4, 0xC2, 0xEF),
                [WeaponTypeBook] = WeaponSuffix(0xC3, 0xA5),
                [WeaponTypeKatar] = WeaponSuffix(0xC4, 0xAB, 0xC5, 0xB8, 0xB8, 0xA3, 0x5F, 0xC4, 0xAB, 0xC5, 0xB8, 0xB8, 0xA3),
                [WeaponTypeGunHandgun] = WeaponSuffix(0xB1, 0xC7, 0xC3, 0xD1),
                [WeaponTypeGunRifle] = WeaponSuffix(0xB1, 0xE2, 0xB0, 0xFC, 0xC3, 0xD1),
                [WeaponTypeGunGatling] = WeaponSuffix(0xB1, 0xE2, 0xB0, 0xFC, 0xC3, 0xD1),
                [WeaponTypeGunShotgun] = WeaponSuffix(0xB1, 0xE2, 0xB0, 0xFC, 0xC3, 0xD1),
                [WeaponTypeGunGrenade] = WeaponSuffix(0xB1, 0xE2, 0xB0, 0xFC, 0xC3, 0xD1),
                [WeaponTypeShuriken] = WeaponSuffix(0xBC, 0xF6, 0xB8, 0xAE, 0xB0, 0xCB),
                [WeaponTypeTwoHandRod] = WeaponSuffix(0xB7, 0xD4, 0xB5, 0xE5),
                [25] = WeaponSuffix(0xB4, 0xDC, 0xB0, 0xCB, 0x5F, 0xB4, 0xDC, 0xB0, 0xCB),
                [26] = WeaponSuffix(0xB0, 0xCB, 0x5F, 0xB0, 0xCB),
                [27] = WeaponSuffix(0xB5, 0xB5, 0xB3, 0xA2, 0x5F, 0xB5, 0xB5, 0xB3, 0xA2),
                [28] = WeaponSuffix(0xB4, 0xDC, 0xB0, 0xCB, 0x5F, 0xB0, 0xCB),
                [29] = WeaponSuffix(0xB4, 0xDC, 0xB0, 0xCB, 0x5F, 0xB5, 0xB5, 0xB3, 0xA2),
                [30] = WeaponSuffix(0xB0, 0xCB, 0x5F, 0xB5, 0xB5, 0xB3, 0xA2)
            };
        }

        private static Dictionary<int, string> DefaultShieldNames() {
            return new Dictionary<int, string> {
                [1] = LegacyKorean(0xB0, 0xA1, 0xB5, 0xE5),
                [2] = LegacyKorean(0xB9, 0xF6, 0xC5, 0xAC, 0xB7, 0xAF),
                [3] = LegacyKorean(0xBD, 0xAF, 0xB5, 0xE5),
                [4] = LegacyKorean(0xB9, 0xCC, 0xB7, 0xAF, 0xBD, 0xAF, 0xB5, 0xE5)
            };
        }

        private static string WeaponSuffix(params int[] bytes) {
            return "_" + LegacyKorean(bytes);
        }

        private static string WeaponTrailGlowSuffix() {
            // "_검광" (geom-gwang, "blade glow") appended to the weapon name to form the trail sprite name.
            return WeaponSuffix(0xB0, 0xCB, 0xB1, 0xA4);
        }

        private static int[] DefaultFemaleHairStyleSpriteIds() {
            return new[] { 2, 2, 4, 7, 1, 5, 3, 6, 12, 10, 9, 11, 8 };
        }

        private static int[] DefaultMaleHairStyleSpriteIds() {
            return new[] { 2, 2, 1, 7, 5, 4, 3, 6, 8, 9, 10, 12, 11 };
        }

        private readonly struct GenderedName {
            public GenderedName(string female, string male) {
                Female = female;
                Male = male;
            }

            public string Female { get; }
            public string Male { get; }
        }
    }
}
