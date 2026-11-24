using Ragnarok.Assets;
using Ragnarok.Data;

namespace Ragnarok.Entities {
    public sealed class RoActorAppearanceTables {
        public static readonly RoActorAppearanceTables Empty = new RoActorAppearanceTables(RoActorAppearanceMetadata.Empty);

        private readonly RoActorAppearanceMetadata metadata;

        private RoActorAppearanceTables(RoActorAppearanceMetadata metadata) {
            this.metadata = metadata ?? RoActorAppearanceMetadata.Empty;
        }

        public RoActorAppearanceMetadata Metadata => metadata;
        public int JobBodyNameCount => metadata.JobBodyNameCount;
        public int GenderedJobBodyNameCount => metadata.GenderedJobBodyNameCount;
        public int AccessoryNameCount => metadata.AccessoryNameCount;
        public int RobeNameCount => metadata.RobeNameCount;
        public int WeaponSuffixCount => metadata.WeaponSuffixCount;
        public int ShieldNameCount => metadata.ShieldNameCount;
        public int HairStyleRemapCount => metadata.HairStyleRemapCount;
        public bool HasJobBodyData => metadata.HasJobBodyData;
        public bool HasAccessoryData => metadata.HasAccessoryData;
        public bool HasRobeData => metadata.HasRobeData;
        public bool HasWeaponData => metadata.HasWeaponData;
        public bool HasShieldData => metadata.HasShieldData;

        public static RoActorAppearanceTables Load(RoAssetDatabase assets) {
            if (assets == null || !assets.IsInitialized) {
                return Empty;
            }

            return new RoActorAppearanceTables(assets.GameData?.ActorAppearance ?? RoActorAppearanceMetadata.Load(assets));
        }

        public static RoActorAppearanceTables FromText(
            string jobIdentityText = null,
            string jobNameText = null,
            string pcJobNameGenderText = null,
            string accessoryIdText = null,
            string accNameText = null,
            string spriteRobeIdText = null,
            string spriteRobeNameText = null
        ) {
            return new RoActorAppearanceTables(RoActorAppearanceMetadata.FromText(
                jobIdentityText,
                jobNameText,
                pcJobNameGenderText,
                accessoryIdText,
                accNameText,
                spriteRobeIdText,
                spriteRobeNameText
            ));
        }

        public bool TryResolveBodyBasePath(RoAssetDatabase assets, RoActorAppearanceRequest appearance, out string basePath) {
            basePath = null;
            return appearance != null &&
                appearance.kind == RoActorAppearanceKind.Player &&
                metadata.TryResolveBodyBasePath(assets, appearance.classId, appearance.isMale, out basePath);
        }

        public bool TryResolveHeadBasePath(RoAssetDatabase assets, RoActorAppearanceRequest appearance, out string basePath) {
            basePath = null;
            return appearance != null &&
                appearance.kind == RoActorAppearanceKind.Player &&
                metadata.TryResolveHeadBasePath(assets, appearance.headId, appearance.isMale, out basePath, RoActorAppearanceMetadata.IsDoramClass(appearance.classId));
        }

        public bool TryResolveHairBasePath(RoAssetDatabase assets, RoActorAppearanceRequest appearance, out string basePath) {
            basePath = null;
            return appearance != null &&
                appearance.kind == RoActorAppearanceKind.Player &&
                metadata.TryResolveHairBasePath(assets, appearance.hairId, appearance.isMale, out basePath);
        }

        public bool TryResolveHeadgearBasePath(RoAssetDatabase assets, int headgearId, bool isMale, out string basePath) {
            return metadata.TryResolveHeadgearBasePath(assets, headgearId, isMale, out basePath);
        }

        public bool TryResolveRobeBasePath(RoAssetDatabase assets, RoActorAppearanceRequest appearance, out string basePath) {
            basePath = null;
            return appearance != null &&
                metadata.TryResolveRobeBasePath(
                    assets,
                    appearance.robeId,
                    appearance.classId,
                    ResolveRobeBaseJob(appearance),
                    appearance.isMale,
                    out basePath
                );
        }

        /// <summary>
        /// The client's second job of FUN_00d27fc0 (the actor's own job, not the mount). Requests built without a base
        /// job (equipment previews) take the client's mount-to-base map of the visual job.
        /// </summary>
        private static int ResolveRobeBaseJob(RoActorAppearanceRequest appearance) {
            return appearance.baseClassId != 0
                ? appearance.baseClassId
                : RoRobeSpriteRules.ToUnmountedJob(appearance.classId);
        }

        public bool TryResolveWeaponBasePath(RoAssetDatabase assets, RoActorAppearanceRequest appearance, out string basePath) {
            basePath = null;
            return appearance != null &&
                appearance.kind == RoActorAppearanceKind.Player &&
                metadata.TryResolveWeaponBasePath(
                    assets,
                    appearance.classId,
                    appearance.isMale,
                    appearance.weaponId,
                    appearance.weaponClass,
                    out basePath
                );
        }

        public bool TryResolveWeaponTrailBasePath(RoAssetDatabase assets, RoActorAppearanceRequest appearance, out string basePath) {
            basePath = null;
            return appearance != null &&
                appearance.kind == RoActorAppearanceKind.Player &&
                metadata.TryResolveWeaponTrailBasePath(
                    assets,
                    appearance.classId,
                    appearance.isMale,
                    appearance.weaponId,
                    appearance.weaponClass,
                    out basePath
                );
        }

        public bool TryResolveShieldBasePath(RoAssetDatabase assets, RoActorAppearanceRequest appearance, out string basePath) {
            basePath = null;
            return appearance != null &&
                appearance.kind == RoActorAppearanceKind.Player &&
                metadata.TryResolveShieldBasePath(
                    assets,
                    appearance.classId,
                    appearance.isMale,
                    appearance.shieldId,
                    out basePath
                );
        }

        public bool TryResolveBodyPalettePath(RoAssetDatabase assets, RoActorAppearanceRequest appearance, out string palettePath) {
            palettePath = null;
            return appearance != null &&
                appearance.kind == RoActorAppearanceKind.Player &&
                metadata.TryResolveBodyPalettePath(
                    assets,
                    appearance.classId,
                    appearance.isMale,
                    appearance.bodyPaletteId,
                    out palettePath
                );
        }

        public bool TryResolveHairPalettePath(RoAssetDatabase assets, RoActorAppearanceRequest appearance, out string palettePath) {
            palettePath = null;
            return appearance != null &&
                appearance.kind == RoActorAppearanceKind.Player &&
                metadata.TryResolveHairPalettePath(
                    assets,
                    appearance.hairId,
                    appearance.isMale,
                    appearance.hairColorId,
                    out palettePath,
                    RoActorAppearanceMetadata.IsDoramClass(appearance.classId)
                );
        }

        public int ResolveHairStyleSpriteId(int hairStyleId, bool isMale) {
            return metadata.ResolveHairStyleSpriteId(hairStyleId, isMale);
        }
    }
}
