using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Network;
using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.Entities {
    public enum RoActorAppearanceKind {
        Player,
        Monster,
        Npc,
        Pet
    }

    public enum RoActorHeadFacing {
        Center = 0,
        Right = 1,
        Left = 2
    }

    [Serializable]
    public sealed class RoActorAppearanceRequest {
        public RoActorAppearanceKind kind = RoActorAppearanceKind.Player;
        public int serverId;
        public int baseClassId;
        public int classId;
        public int bodyStyleId;
        public bool isMainCharacter;
        public bool isMale = true;
        public int headId = 1;
        public int hairId = 1;
        public int hairColorId;
        public int bodyPaletteId;
        public int weaponClass;
        public int weaponId;
        public int shieldId;
        public int robeId;
        public int headgearTopId;
        public int headgearMidId;
        public int headgearLowerId;
        public RoSpriteMotionProfile motionProfile = RoSpriteMotionProfile.Player;
        public RoSpriteMotion motion = RoSpriteMotion.Idle;
        public RoActorDirection facingDirection = RoActorDirection.South;
        public RoActorHeadFacing headFacing = RoActorHeadFacing.Center;
        public int initialActionIndex;
        public string bodyBasePath;
        public string bodyPalettePath;
        public string headBasePath;
        public string hairBasePath;
        public string hairPalettePath;
        public string weaponBasePath;
        public string shieldBasePath;
        public string garmentBasePath;
        public string headgearTopBasePath;
        public string headgearMidBasePath;
        public string headgearLowerBasePath;
        public bool hasCart;
        public int cartNum;

        public static RoActorAppearanceRequest CreateDefaultPlayerProbe(int bodyActionIndex = 0) {
            return new RoActorAppearanceRequest {
                kind = RoActorAppearanceKind.Player,
                motionProfile = RoSpriteMotionProfile.Player,
                initialActionIndex = Mathf.Max(0, bodyActionIndex),
                hairColorId = -1,
                bodyPaletteId = -1,
                headId = 1,
                hairId = 1,
                isMale = true
            };
        }
    }

    public static class RoActorSpritePathResolver {
        public static bool TryFindFirstPlayerSpritePair(RoAssetDatabase assets, out string basePath) {
            return TryFindPreferredPlayerBodySprite(assets, out basePath) ||
                TryFindBestScoredSpritePair(assets, "body", ScorePlayerBodySpritePath, out basePath) ||
                TryFindFirstSpritePair(assets, out basePath);
        }

        public static bool TryFindFirstSpritePair(RoAssetDatabase assets, out string basePath) {
            if (TryGetMemo(assets, "first-spr", out basePath)) {
                return basePath != null;
            }

            bool found = TryFindFirstSpritePair(assets, null, out basePath);
            Remember(assets, "first-spr", found ? basePath : null);
            return found;
        }

        public static bool TryFindPreferredPlayerBodySprite(RoAssetDatabase assets, out string basePath) {
            string humanRace = HumanRaceSegment();
            string body = BodySegment();
            string male = MaleSegment();
            string female = FemaleSegment();
            string novice = NoviceSegment();

            if (TryUseExistingSpritePair(assets, $"data/sprite/{humanRace}/{body}/{male}/{novice}_{male}", out basePath)) {
                return true;
            }

            if (TryUseExistingSpritePair(assets, $"data/sprite/{humanRace}/{body}/{female}/{novice}_{female}", out basePath)) {
                return true;
            }

            basePath = null;
            return false;
        }

        public static bool TryResolveKnownPlayerBodySpritePair(RoAssetDatabase assets, int classId, bool isMale, out string basePath) {
            basePath = null;
            if (!TryGetKnownPlayerBodyName(classId, out string bodyName)) {
                return false;
            }

            string sex = isMale ? MaleSegment() : FemaleSegment();
            string candidate = $"data/sprite/{BodyRaceSegment(classId)}/{BodySegment()}/{sex}/{bodyName}_{sex}";
            basePath = RoActorSpriteVisual.NormalizeBasePath(candidate);
            return assets == null || (assets.Exists($"{basePath}.spr") && assets.Exists($"{basePath}.act"));
        }

        public static bool TryFindPreferredPlayerHeadSprite(RoAssetDatabase assets, out string basePath) {
            string humanRace = HumanRaceSegment();
            string head = HeadSegment();
            string male = MaleSegment();
            string female = FemaleSegment();

            if (TryUseExistingSpritePair(assets, $"data/sprite/{humanRace}/{head}/{male}/2_{male}", out basePath)) {
                return true;
            }

            if (TryUseExistingSpritePair(assets, $"data/sprite/{humanRace}/{head}/{female}/2_{female}", out basePath)) {
                return true;
            }

            return TryFindBestScoredSpritePair(assets, "head", ScorePlayerHeadSpritePath, out basePath);
        }

        public static bool TryFindPreferredPlayerHairSprite(RoAssetDatabase assets, out string basePath) {
            string humanRace = HumanRaceSegment();
            string hair = HairSegment();
            string male = MaleSegment();
            string female = FemaleSegment();

            if (TryUseExistingSpritePair(assets, $"data/sprite/{humanRace}/{hair}/{male}/2_{male}", out basePath)) {
                return true;
            }

            if (TryUseExistingSpritePair(assets, $"data/sprite/{humanRace}/{hair}/{female}/2_{female}", out basePath)) {
                return true;
            }

            return TryFindBestScoredSpritePair(assets, "hair", ScorePlayerHairSpritePath, out basePath);
        }

        public static bool TryFindPreferredPlayerBodyPalette(RoAssetDatabase assets, out string path) {
            return TryFindPreferredPlayerBodyPalette(assets, 0, out path);
        }

        public static bool TryFindPreferredPlayerBodyPalette(RoAssetDatabase assets, int paletteId, out string path) {
            string body = BodyPaletteSegment();
            string male = MaleSegment();
            string female = FemaleSegment();
            string novice = NoviceSegment();
            if (paletteId < 0) {
                path = null;
                return false;
            }

            int resolvedPaletteId = Mathf.Max(0, paletteId);

            if (TryUseExistingFile(assets, $"data/palette/{body}/{novice}_{male}_{resolvedPaletteId}.pal", out path)) {
                return true;
            }

            if (TryUseExistingFile(assets, $"data/palette/{body}/{novice}_{female}_{resolvedPaletteId}.pal", out path)) {
                return true;
            }

            return TryFindBestScoredFile(assets, "pal", "body-pal:" + resolvedPaletteId, candidate => ScorePlayerBodyPalettePath(candidate, resolvedPaletteId), out path);
        }

        public static bool TryResolveKnownPlayerBodyPalette(RoAssetDatabase assets, int classId, bool isMale, int paletteId, out string path) {
            path = null;
            if (paletteId < 0 || !TryGetKnownPlayerBodyPaletteName(classId, out string bodyName)) {
                return false;
            }

            string sex = isMale ? MaleSegment() : FemaleSegment();
            string candidate = $"data/palette/{BodyPaletteSegment()}/{bodyName}_{sex}_{Mathf.Max(0, paletteId)}.pal";
            path = NormalizeOptionalAssetPath(candidate);
            return assets == null || assets.Exists(path);
        }

        public static bool TryFindPreferredPlayerHairPalette(RoAssetDatabase assets, out string path) {
            return TryFindPreferredPlayerHairPalette(assets, 0, out path);
        }

        public static bool TryFindPreferredPlayerHairPalette(RoAssetDatabase assets, int paletteId, out string path) {
            string hair = HairSegment();
            string male = MaleSegment();
            string female = FemaleSegment();
            if (paletteId < 0) {
                path = null;
                return false;
            }

            int resolvedPaletteId = Mathf.Max(0, paletteId);

            if (TryUseExistingFile(assets, $"data/palette/{hair}/{hair}2_{male}_{resolvedPaletteId}.pal", out path)) {
                return true;
            }

            if (TryUseExistingFile(assets, $"data/palette/{hair}/{hair}2_{female}_{resolvedPaletteId}.pal", out path)) {
                return true;
            }

            return TryFindBestScoredFile(assets, "pal", "hair-pal:" + resolvedPaletteId, candidate => ScorePlayerHairPalettePath(candidate, resolvedPaletteId), out path);
        }

        public static bool TryFindFirstPlayerHeadgearSprite(RoAssetDatabase assets, out string basePath) {
            return TryFindBestScoredSpritePair(assets, "headgear", ScorePlayerHeadgearSpritePath, out basePath);
        }

        public static RoActorSpriteLayerSpec[] BuildDefaultPlayerLayerSpecs(RoAssetDatabase assets, int bodyActionIndex = 0) {
            return BuildPlayerLayerSpecs(assets, RoActorAppearanceRequest.CreateDefaultPlayerProbe(bodyActionIndex));
        }

        public static RoActorSpriteLayerSpec[] BuildPlayerLayerSpecs(
            RoAssetDatabase assets,
            RoActorAppearanceRequest appearance,
            IReadOnlyList<RoActorSpriteLayerSpec> baseSpecs = null,
            UnityEngine.Object logContext = null,
            RoActorAppearanceTables appearanceTables = null
        ) {
            if (appearance == null) {
                appearance = RoActorAppearanceRequest.CreateDefaultPlayerProbe();
            }

            appearanceTables = appearanceTables ?? RoActorAppearanceTables.Load(assets);
            var specs = CopyUsableLayerSpecs(baseSpecs);
            RoSpriteMotionProfile profile = appearance.motionProfile;

            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.Body,
                appearance.bodyBasePath,
                resolverAssets => ResolvePlayerBodyBasePath(resolverAssets, appearance, appearanceTables),
                appearance.bodyPalettePath,
                resolverAssets => ResolvePlayerBodyPalettePath(resolverAssets, appearance, appearanceTables),
                appearance.initialActionIndex,
                profile,
                logContext
            );

            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.Head,
                appearance.headBasePath,
                resolverAssets => appearanceTables.TryResolveHeadBasePath(resolverAssets, appearance, out string resolved)
                    ? resolved
                    : TryFindPreferredPlayerHeadSprite(resolverAssets, out resolved)
                        ? resolved
                        : null,
                appearance.hairPalettePath,
                resolverAssets => ResolvePlayerHeadPalettePath(resolverAssets, appearance, appearanceTables),
                appearance.initialActionIndex,
                profile,
                logContext
            );

            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.Hair,
                appearance.hairBasePath,
                null,
                appearance.hairPalettePath,
                resolverAssets => ResolvePlayerHairPalettePath(resolverAssets, appearance, appearanceTables),
                appearance.initialActionIndex,
                profile,
                logContext
            );

            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.Weapon,
                appearance.weaponBasePath,
                appearance.weaponId > 0 || appearance.weaponClass > 0
                    ? (Func<RoAssetDatabase, string>) (resolverAssets => appearanceTables.TryResolveWeaponBasePath(resolverAssets, appearance, out string resolved) ? resolved : null)
                    : null,
                null,
                null,
                appearance.initialActionIndex,
                profile,
                logContext
            );
            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.WeaponTrail,
                null,
                appearance.weaponId > 0 || appearance.weaponClass > 0
                    ? (Func<RoAssetDatabase, string>) (resolverAssets => appearanceTables.TryResolveWeaponTrailBasePath(resolverAssets, appearance, out string resolved) ? resolved : null)
                    : null,
                null,
                null,
                appearance.initialActionIndex,
                profile,
                logContext
            );
            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.Shield,
                appearance.shieldBasePath,
                appearance.shieldId > 0
                    ? (Func<RoAssetDatabase, string>) (resolverAssets => appearanceTables.TryResolveShieldBasePath(resolverAssets, appearance, out string resolved) ? resolved : null)
                    : null,
                null,
                null,
                appearance.initialActionIndex,
                profile,
                logContext
            );
            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.Garment,
                appearance.garmentBasePath,
                appearance.robeId > 0
                    ? (Func<RoAssetDatabase, string>) (resolverAssets => appearanceTables.TryResolveRobeBasePath(resolverAssets, appearance, out string resolved) ? resolved : null)
                    : null,
                null,
                null,
                appearance.initialActionIndex,
                profile,
                logContext
            );
            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.HeadgearTop,
                appearance.headgearTopBasePath,
                appearance.headgearTopId > 0
                    ? (Func<RoAssetDatabase, string>) (resolverAssets => appearanceTables.TryResolveHeadgearBasePath(resolverAssets, appearance.headgearTopId, appearance.isMale, out string resolved)
                        ? resolved
                        : TryFindFirstPlayerHeadgearSprite(resolverAssets, out resolved)
                            ? resolved
                            : null)
                    : null,
                null,
                null,
                appearance.initialActionIndex,
                profile,
                logContext
            );
            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.HeadgearMid,
                appearance.headgearMidBasePath,
                appearance.headgearMidId > 0
                    ? (Func<RoAssetDatabase, string>) (resolverAssets => appearanceTables.TryResolveHeadgearBasePath(resolverAssets, appearance.headgearMidId, appearance.isMale, out string resolved) ? resolved : null)
                    : null,
                null,
                null,
                appearance.initialActionIndex,
                profile,
                logContext
            );
            EnsureLayer(
                specs,
                assets,
                RoActorSpriteLayer.HeadgearLower,
                appearance.headgearLowerBasePath,
                appearance.headgearLowerId > 0
                    ? (Func<RoAssetDatabase, string>) (resolverAssets => appearanceTables.TryResolveHeadgearBasePath(resolverAssets, appearance.headgearLowerId, appearance.isMale, out string resolved) ? resolved : null)
                    : null,
                null,
                null,
                appearance.initialActionIndex,
                profile,
                logContext
            );

            if (appearance.hasCart) {
                int cartIndex = RoMapRentalVisualSemantics.ResolveCartIndex(
                    (short) (appearance.baseClassId != 0 ? appearance.baseClassId : appearance.classId),
                    appearance.hasCart,
                    appearance.cartNum);
                EnsureLayer(
                    specs,
                    assets,
                    RoActorSpriteLayer.CartShadow,
                    null,
                    _ => RoMapRentalVisualSemantics.CartShadowBasePath,
                    null,
                    null,
                    0,
                    profile,
                    logContext
                );
                EnsureLayer(
                    specs,
                    assets,
                    RoActorSpriteLayer.Cart,
                    null,
                    _ => RoMapRentalVisualSemantics.ResolveCartBasePath(cartIndex),
                    null,
                    null,
                    0,
                    profile,
                    logContext
                );
            }

            return RoActorLayeredVisual.SortSpecsForRendering(specs);
        }

        private static string ResolvePlayerHeadPalettePath(
            RoAssetDatabase assets,
            RoActorAppearanceRequest appearance,
            RoActorAppearanceTables appearanceTables
        ) {
            if (appearance == null || appearanceTables == null) {
                return null;
            }

            if (appearance.hairColorId <= 0) {
                return null;
            }

            var paletteAppearance = new RoActorAppearanceRequest {
                kind = appearance.kind,
                classId = appearance.classId,
                isMale = appearance.isMale,
                hairId = appearance.headId,
                hairColorId = appearance.hairColorId
            };

            return appearanceTables.TryResolveHairPalettePath(assets, paletteAppearance, out string resolved)
                ? resolved
                : TryFindPreferredPlayerHairPalette(assets, appearance.hairColorId, out resolved)
                    ? resolved
                    : null;
        }

        private static string ResolvePlayerHairPalettePath(
            RoAssetDatabase assets,
            RoActorAppearanceRequest appearance,
            RoActorAppearanceTables appearanceTables
        ) {
            if (appearance == null || appearanceTables == null || appearance.hairColorId <= 0) {
                return null;
            }

            return appearanceTables.TryResolveHairPalettePath(assets, appearance, out string resolved)
                ? resolved
                : TryFindPreferredPlayerHairPalette(assets, appearance.hairColorId, out resolved)
                    ? resolved
                    : null;
        }

        public static string NormalizeOptionalAssetPath(string path) {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }

        private static bool TryFindFirstSpritePair(RoAssetDatabase assets, Predicate<string> predicate, out string basePath) {
            basePath = null;
            if (assets == null) {
                return false;
            }

            List<string> sprites = assets.ListFilesWithExtension("spr");
            for (int i = 0; i < sprites.Count; i++) {
                string candidate = RoActorSpriteVisual.NormalizeBasePath(sprites[i]);
                if (string.IsNullOrEmpty(candidate)) {
                    continue;
                }

                if (predicate != null && !predicate(candidate)) {
                    continue;
                }

                if (assets.Exists($"{candidate}.act")) {
                    basePath = candidate;
                    return true;
                }
            }

            return false;
        }

        private static List<RoActorSpriteLayerSpec> CopyUsableLayerSpecs(IReadOnlyList<RoActorSpriteLayerSpec> specs) {
            var usable = new List<RoActorSpriteLayerSpec>();
            if (specs == null) {
                return usable;
            }

            for (int i = 0; i < specs.Count; i++) {
                RoActorSpriteLayerSpec spec = specs[i];
                if (spec == null || string.IsNullOrWhiteSpace(spec.basePath)) {
                    continue;
                }

                usable.Add(new RoActorSpriteLayerSpec(spec.layer, RoActorSpriteVisual.NormalizeBasePath(spec.basePath)) {
                    palettePath = NormalizeOptionalAssetPath(spec.palettePath),
                    initialActionIndex = Mathf.Max(0, spec.initialActionIndex),
                    motionProfile = spec.motionProfile,
                    localOffset = spec.localOffset,
                    scale = spec.scale
                });
            }

            return usable;
        }

        private static void EnsureLayer(
            List<RoActorSpriteLayerSpec> specs,
            RoAssetDatabase assets,
            RoActorSpriteLayer layer,
            string configuredBasePath,
            Func<RoAssetDatabase, string> fallbackBasePathResolver,
            string configuredPalettePath,
            Func<RoAssetDatabase, string> fallbackPaletteResolver,
            int initialActionIndex,
            RoSpriteMotionProfile motionProfile,
            UnityEngine.Object logContext
        ) {
            RoActorSpriteLayerSpec spec = FindLayer(specs, layer);
            bool createdSpec = false;

            if (spec == null) {
                string basePath = ResolveSpritePairPath(assets, configuredBasePath, fallbackBasePathResolver, layer.ToString(), logContext);
                if (string.IsNullOrEmpty(basePath)) {
                    return;
                }

                spec = new RoActorSpriteLayerSpec(layer, basePath) {
                    initialActionIndex = Mathf.Max(0, initialActionIndex)
                };
                specs.Add(spec);
                createdSpec = true;
            }

            spec.motionProfile = motionProfile;
            if (createdSpec || string.IsNullOrWhiteSpace(spec.palettePath)) {
                spec.palettePath = ResolvePalettePath(assets, configuredPalettePath, fallbackPaletteResolver, layer.ToString(), logContext);
            }
        }

        private static RoActorSpriteLayerSpec FindLayer(IReadOnlyList<RoActorSpriteLayerSpec> specs, RoActorSpriteLayer layer) {
            if (specs == null) {
                return null;
            }

            for (int i = 0; i < specs.Count; i++) {
                RoActorSpriteLayerSpec spec = specs[i];
                if (spec != null && spec.layer == layer && !string.IsNullOrWhiteSpace(spec.basePath)) {
                    return spec;
                }
            }

            return null;
        }

        private static string ResolveSpritePairPath(
            RoAssetDatabase assets,
            string configuredBasePath,
            Func<RoAssetDatabase, string> fallbackResolver,
            string label,
            UnityEngine.Object logContext
        ) {
            string basePath = RoActorSpriteVisual.NormalizeBasePath(configuredBasePath);
            if (!string.IsNullOrEmpty(basePath)) {
                if (SpritePairExists(assets, basePath)) {
                    return basePath;
                }

                Debug.LogWarning($"Configured actor layer '{label}' path '{basePath}' was not found as a .spr/.act pair. Falling back to auto-resolution.", logContext);
            }

            if (fallbackResolver == null) {
                return string.Empty;
            }

            return RoActorSpriteVisual.NormalizeBasePath(fallbackResolver(assets));
        }

        private static string ResolvePlayerBodyBasePath(
            RoAssetDatabase assets,
            RoActorAppearanceRequest appearance,
            RoActorAppearanceTables appearanceTables
        ) {
            int bodyClassId = ResolveEffectiveBodyClassId(appearance);
            if (appearanceTables.Metadata.TryResolveBodyBasePath(assets, bodyClassId, appearance.isMale, out string resolved)) {
                return resolved;
            }

            if (TryResolveKnownPlayerBodySpritePair(assets, bodyClassId, appearance.isMale, out resolved)) {
                return resolved;
            }

            return TryFindFirstPlayerSpritePair(assets, out resolved) ? resolved : null;
        }

        private static string ResolvePlayerBodyPalettePath(
            RoAssetDatabase assets,
            RoActorAppearanceRequest appearance,
            RoActorAppearanceTables appearanceTables
        ) {
            if (appearance.bodyPaletteId <= 0) {
                return null;
            }

            int bodyClassId = ResolveEffectiveBodyClassId(appearance);
            if (appearanceTables.Metadata.TryResolveBodyPalettePath(assets, bodyClassId, appearance.isMale, appearance.bodyPaletteId, out string resolved)) {
                return resolved;
            }

            if (TryResolveKnownPlayerBodyPalette(assets, bodyClassId, appearance.isMale, appearance.bodyPaletteId, out resolved)) {
                return resolved;
            }

            return TryFindPreferredPlayerBodyPalette(assets, appearance.bodyPaletteId, out resolved) ? resolved : null;
        }

        private static string ResolvePalettePath(
            RoAssetDatabase assets,
            string configuredPath,
            Func<RoAssetDatabase, string> fallbackResolver,
            string label,
            UnityEngine.Object logContext
        ) {
            string path = NormalizeOptionalAssetPath(configuredPath);
            if (!string.IsNullOrEmpty(path)) {
                if (assets == null || assets.Exists(path)) {
                    return path;
                }

                Debug.LogWarning($"Configured actor palette '{label}' path '{path}' was not found. Falling back to auto-resolution.", logContext);
            }

            return fallbackResolver != null
                ? NormalizeOptionalAssetPath(fallbackResolver(assets))
                : string.Empty;
        }

        private static bool SpritePairExists(RoAssetDatabase assets, string basePath) {
            return assets == null ||
                (assets.Exists(RoSpritePairPath.SpritePath(basePath)) && assets.Exists(RoSpritePairPath.ActionPath(basePath)));
        }

        /// <summary>Results of the fallback searches below, per asset database and search (null = nothing found).</summary>
        private static readonly ConditionalWeakTable<RoAssetDatabase, ConcurrentDictionary<string, string>> SearchMemo =
            new ConditionalWeakTable<RoAssetDatabase, ConcurrentDictionary<string, string>>();

        private static bool TryGetMemo(RoAssetDatabase assets, string key, out string result) {
            result = null;
            return assets != null &&
                SearchMemo.TryGetValue(assets, out ConcurrentDictionary<string, string> memo) &&
                memo.TryGetValue(key, out result);
        }

        private static void Remember(RoAssetDatabase assets, string key, string result) {
            if (assets != null) {
                SearchMemo.GetOrCreateValue(assets)[key] = result;
            }
        }

        private static bool TryFindBestScoredSpritePair(RoAssetDatabase assets, string memoKey, Func<string, int> scorePath, out string basePath) {
            if (TryGetMemo(assets, "spr:" + memoKey, out basePath)) {
                return basePath != null;
            }

            bool found = TryFindBestScoredSpritePair(assets, scorePath, out basePath);
            Remember(assets, "spr:" + memoKey, found ? basePath : null);
            return found;
        }

        private static bool TryFindBestScoredFile(RoAssetDatabase assets, string extension, string memoKey, Func<string, int> scorePath, out string path) {
            if (TryGetMemo(assets, extension + ":" + memoKey, out path)) {
                return path != null;
            }

            bool found = TryFindBestScoredFile(assets, extension, scorePath, out path);
            Remember(assets, extension + ":" + memoKey, found ? path : null);
            return found;
        }

        private static bool TryFindBestScoredSpritePair(RoAssetDatabase assets, Func<string, int> scorePath, out string basePath) {
            basePath = null;
            if (assets == null) {
                return false;
            }

            int bestScore = 0;
            List<string> sprites = assets.ListFilesWithExtension("spr");
            for (int i = 0; i < sprites.Count; i++) {
                string candidate = RoActorSpriteVisual.NormalizeBasePath(sprites[i]);
                if (string.IsNullOrEmpty(candidate) || !assets.Exists($"{candidate}.act")) {
                    continue;
                }

                int score = scorePath(candidate);
                if (score > bestScore) {
                    bestScore = score;
                    basePath = candidate;
                }
            }

            return !string.IsNullOrEmpty(basePath);
        }

        private static bool TryFindBestScoredFile(RoAssetDatabase assets, string extension, Func<string, int> scorePath, out string path) {
            path = null;
            if (assets == null) {
                return false;
            }

            int bestScore = 0;
            List<string> files = assets.ListFilesWithExtension(extension);
            for (int i = 0; i < files.Count; i++) {
                string candidate = NormalizeOptionalAssetPath(files[i]);
                if (string.IsNullOrEmpty(candidate)) {
                    continue;
                }

                int score = scorePath(candidate);
                if (score > bestScore) {
                    bestScore = score;
                    path = candidate;
                }
            }

            return !string.IsNullOrEmpty(path);
        }

        private static bool TryUseExistingSpritePair(RoAssetDatabase assets, string basePath, out string resolvedBasePath) {
            resolvedBasePath = RoActorSpriteVisual.NormalizeBasePath(basePath);
            if (assets != null &&
                assets.Exists($"{resolvedBasePath}.spr") &&
                assets.Exists($"{resolvedBasePath}.act")) {
                return true;
            }

            resolvedBasePath = null;
            return false;
        }

        private static bool TryUseExistingFile(RoAssetDatabase assets, string path, out string resolvedPath) {
            resolvedPath = NormalizeOptionalAssetPath(path);
            if (assets != null && assets.Exists(resolvedPath)) {
                return true;
            }

            resolvedPath = null;
            return false;
        }

        private static int ScorePlayerBodySpritePath(string basePath) {
            string path = CanonicalPath(basePath);
            if (!IsCandidateSpritePath(path)) {
                return 0;
            }

            string accessory = AccessorySegment();
            string robe = RobeSegment();
            string humanRace = HumanRaceSegment();
            string body = BodySegment();
            string head = HeadSegment();
            string hair = HairSegment();
            string novice = NoviceSegment();
            string male = MaleSegment();

            if (path.Contains($"/{accessory}/") || path.Contains($"/{robe}/") || path.Contains($"/{hair}/")) {
                return 0;
            }

            int score = 1;
            if (path.Contains($"/{humanRace}/")) {
                score += 1000;
            }

            if (path.Contains($"/{body}/")) {
                score += 600;
            }

            if (path.Contains($"/{head}/")) {
                score -= 500;
            }

            if (path.Contains(novice)) {
                score += 300;
            }

            if (path.Contains($"/{male}/")) {
                score += 50;
            }

            if (path.Contains("/costume_")) {
                score -= 100;
            }

            if (path.Contains("_h_")) {
                score -= 100;
            }

            return Mathf.Max(0, score);
        }

        private static int ScorePlayerHeadSpritePath(string basePath) {
            string path = CanonicalPath(basePath);
            if (!IsCandidateSpritePath(path)) {
                return 0;
            }

            string accessory = AccessorySegment();
            string humanRace = HumanRaceSegment();
            string body = BodySegment();
            string head = HeadSegment();
            string hair = HairSegment();
            string male = MaleSegment();

            if (path.Contains($"/{accessory}/") || path.Contains($"/{body}/") || path.Contains($"/{hair}/")) {
                return 0;
            }

            if (!path.Contains($"/{head}/")) {
                return 0;
            }

            int score = 800;
            if (path.Contains($"/{humanRace}/")) {
                score += 1000;
            }

            if (path.EndsWith($"/2_{male}", StringComparison.OrdinalIgnoreCase)) {
                score += 200;
            }

            if (path.Contains($"/{male}/")) {
                score += 50;
            }

            if (path.Contains("/costume_")) {
                score -= 100;
            }

            return Mathf.Max(0, score);
        }

        private static int ScorePlayerHairSpritePath(string basePath) {
            string path = CanonicalPath(basePath);
            if (!IsCandidateSpritePath(path)) {
                return 0;
            }

            string accessory = AccessorySegment();
            string humanRace = HumanRaceSegment();
            string body = BodySegment();
            string head = HeadSegment();
            string hair = HairSegment();
            string male = MaleSegment();

            if (path.Contains($"/{accessory}/") || path.Contains($"/{body}/") || path.Contains($"/{head}/")) {
                return 0;
            }

            if (!path.Contains($"/{hair}/")) {
                return 0;
            }

            int score = 800;
            if (path.Contains($"/{humanRace}/")) {
                score += 1000;
            }

            if (path.EndsWith($"/2_{male}", StringComparison.OrdinalIgnoreCase)) {
                score += 200;
            }

            if (path.Contains($"/{male}/")) {
                score += 50;
            }

            if (path.Contains("/costume_")) {
                score -= 100;
            }

            return Mathf.Max(0, score);
        }

        private static int ScorePlayerBodyPalettePath(string palettePath) {
            return ScorePlayerBodyPalettePath(palettePath, 0);
        }

        private static int ScorePlayerBodyPalettePath(string palettePath, int paletteId) {
            string path = CanonicalPath(palettePath);
            if (!path.Contains("/palette/") || !path.EndsWith(".pal", StringComparison.OrdinalIgnoreCase)) {
                return 0;
            }

            string body = BodyPaletteSegment();
            string hair = HairSegment();
            string novice = NoviceSegment();

            if (path.Contains($"/{hair}/")) {
                return 0;
            }

            if (!path.Contains($"/{body}/")) {
                return 0;
            }

            int score = 800;
            if (path.Contains(novice)) {
                score += 300;
            }

            if (path.EndsWith($"_{Mathf.Max(0, paletteId)}.pal", StringComparison.OrdinalIgnoreCase)) {
                score += 200;
            }

            return Mathf.Max(0, score);
        }

        private static int ScorePlayerHairPalettePath(string palettePath) {
            return ScorePlayerHairPalettePath(palettePath, 0);
        }

        private static int ScorePlayerHairPalettePath(string palettePath, int paletteId) {
            string path = CanonicalPath(palettePath);
            if (!path.Contains("/palette/") || !path.EndsWith(".pal", StringComparison.OrdinalIgnoreCase)) {
                return 0;
            }

            string body = BodySegment();
            string head = HeadSegment();
            string hair = HairSegment();
            string male = MaleSegment();
            string female = FemaleSegment();

            if (path.Contains($"/{body}/") || path.Contains($"/{head}/")) {
                return 0;
            }

            if (!path.Contains($"/{hair}/")) {
                return 0;
            }

            int score = 800;
            if (path.Contains($"{hair}2_{male}") || path.Contains($"{hair}2_{female}")) {
                score += 200;
            }

            if (path.EndsWith($"_{Mathf.Max(0, paletteId)}.pal", StringComparison.OrdinalIgnoreCase)) {
                score += 200;
            }

            return Mathf.Max(0, score);
        }

        private static int ScorePlayerHeadgearSpritePath(string basePath) {
            string path = CanonicalPath(basePath);
            if (!IsCandidateSpritePath(path)) {
                return 0;
            }

            string accessory = AccessorySegment();
            if (!path.Contains($"/{accessory}/")) {
                return 0;
            }

            int score = 800;
            if (path.Contains("/costume_")) {
                score -= 100;
            }

            return Mathf.Max(0, score);
        }

        private static bool IsCandidateSpritePath(string path) {
            return path.Contains("/sprite/") &&
                !path.Contains("/book/") &&
                !path.Contains("/effect/") &&
                !path.Contains("/item/") &&
                !path.Contains("/texture/");
        }

        private static string CanonicalPath(string path) {
            return (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
        }

        private static string HumanRaceSegment() {
            return LegacyPathSegment(0xC0, 0xCE, 0xB0, 0xA3, 0xC1, 0xB7);
        }

        private static string BodySegment() {
            return LegacyPathSegment(0xB8, 0xF6, 0xC5, 0xEB);
        }

        private static string BodyPaletteSegment() {
            return LegacyPathSegment(0xB8, 0xF6);
        }

        private static string HeadSegment() {
            return LegacyPathSegment(0xB8, 0xD3, 0xB8, 0xAE, 0xC5, 0xEB);
        }

        private static string HairSegment() {
            return LegacyPathSegment(0xB8, 0xD3, 0xB8, 0xAE);
        }

        private static string MaleSegment() {
            return LegacyPathSegment(0xB3, 0xB2);
        }

        private static string FemaleSegment() {
            return LegacyPathSegment(0xBF, 0xA9);
        }

        private static string DoramRaceSegment() {
            return LegacyKoreanPathSegment("\uB3C4\uB78C\uC871");
        }

        private static string NoviceSegment() {
            return LegacyPathSegment(0xC3, 0xCA, 0xBA, 0xB8, 0xC0, 0xDA);
        }

        private static bool TryGetKnownPlayerBodyName(int classId, out string bodyName) {
            bodyName = KnownPlayerBodyName(classId);

            return !string.IsNullOrEmpty(bodyName);
        }

        private static bool TryGetKnownPlayerBodyPaletteName(int classId, out string bodyName) {
            bodyName = classId switch {
                14 => LegacyKoreanPathSegment("\uD06C\uB8E8"),
                20 => LegacyKoreanPathSegment("\uB304\uC11C"),
                4009 => LegacyKoreanPathSegment("\uD558\uC774\uD504\uB9AC\uC2A4\uD2B8"),
                4013 => LegacyKoreanPathSegment("\uC5B4\uC138\uC2E0\uD06C\uB85C\uC2A4"),
                4014 => LegacyKoreanPathSegment("\uD398\uCF54\uB85C\uB098"),
                4020 => LegacyKoreanPathSegment("\uD06C\uB77C\uC6B4"),
                4056 => LegacyKoreanPathSegment("\uB808\uC778\uC800"),
                4066 => LegacyKoreanPathSegment("\uB85C\uC584\uAC00\uB4DC"),
                4211 => "KAGEROU",
                4212 => "OBORO",
                4215 => LegacyKoreanPathSegment("\uB9AC\uBCA8\uB9AC\uC628"),
                4218 => LegacyKoreanPathSegment("\uBB18\uC871"),
                _ => KnownPlayerBodyName(classId)
            };

            return !string.IsNullOrEmpty(bodyName);
        }

        private static string KnownPlayerBodyName(int classId) {
            return classId switch {
                0 => LegacyKoreanPathSegment("\uCD08\uBCF4\uC790"),
                1 => LegacyKoreanPathSegment("\uAC80\uC0AC"),
                2 => LegacyKoreanPathSegment("\uB9C8\uBC95\uC0AC"),
                3 => LegacyKoreanPathSegment("\uAD81\uC218"),
                4 => LegacyKoreanPathSegment("\uC131\uC9C1\uC790"),
                5 => LegacyKoreanPathSegment("\uC0C1\uC778"),
                6 => LegacyKoreanPathSegment("\uB3C4\uB451"),
                7 => LegacyKoreanPathSegment("\uAE30\uC0AC"),
                8 => LegacyKoreanPathSegment("\uD504\uB9AC\uC2A4\uD2B8"),
                9 => LegacyKoreanPathSegment("\uC704\uC800\uB4DC"),
                10 => LegacyKoreanPathSegment("\uC81C\uCCA0\uACF5"),
                11 => LegacyKoreanPathSegment("\uD5CC\uD130"),
                12 => LegacyKoreanPathSegment("\uC5B4\uC138\uC2E0"),
                13 => LegacyKoreanPathSegment("\uD398\uCF54\uD398\uCF54_\uAE30\uC0AC"),
                14 => LegacyKoreanPathSegment("\uD06C\uB8E8\uC138\uC774\uB354"),
                15 => LegacyKoreanPathSegment("\uBABD\uD06C"),
                16 => LegacyKoreanPathSegment("\uC138\uC774\uC9C0"),
                17 => LegacyKoreanPathSegment("\uB85C\uADF8"),
                18 => LegacyKoreanPathSegment("\uC5F0\uAE08\uC220\uC0AC"),
                19 => LegacyKoreanPathSegment("\uBC14\uB4DC"),
                20 => LegacyKoreanPathSegment("\uBB34\uD76C"),
                21 => LegacyKoreanPathSegment("\uC2E0\uD398\uCF54\uD06C\uB8E8\uC138\uC774\uB354"),
                23 => LegacyKoreanPathSegment("\uC288\uD37C\uB178\uBE44\uC2A4"),
                24 => LegacyKoreanPathSegment("\uAC74\uB108"),
                25 => LegacyKoreanPathSegment("\uB2CC\uC790"),
                4001 => LegacyKoreanPathSegment("\uCD08\uBCF4\uC790"),
                4002 => LegacyKoreanPathSegment("\uAC80\uC0AC"),
                4003 => LegacyKoreanPathSegment("\uB9C8\uBC95\uC0AC"),
                4004 => LegacyKoreanPathSegment("\uAD81\uC218"),
                4005 => LegacyKoreanPathSegment("\uC131\uC9C1\uC790"),
                4006 => LegacyKoreanPathSegment("\uC0C1\uC778"),
                4007 => LegacyKoreanPathSegment("\uB3C4\uB451"),
                4008 => LegacyKoreanPathSegment("\uB85C\uB4DC\uB098\uC774\uD2B8"),
                4009 => LegacyKoreanPathSegment("\uD558\uC774\uD504\uB9AC"),
                4010 => LegacyKoreanPathSegment("\uD558\uC774\uC704\uC800\uB4DC"),
                4011 => LegacyKoreanPathSegment("\uD654\uC774\uD2B8\uC2A4\uBBF8\uC2A4"),
                4012 => LegacyKoreanPathSegment("\uC2A4\uB098\uC774\uD37C"),
                4013 => LegacyKoreanPathSegment("\uC5B4\uC314\uC2E0\uD06C\uB85C\uC2A4"),
                4014 => LegacyKoreanPathSegment("\uB85C\uB4DC\uD398\uCF54"),
                4015 => LegacyKoreanPathSegment("\uD314\uB77C\uB518"),
                4016 => LegacyKoreanPathSegment("\uCC54\uD53C\uC628"),
                4017 => LegacyKoreanPathSegment("\uD504\uB85C\uD398\uC11C"),
                4018 => LegacyKoreanPathSegment("\uC2A4\uD1A0\uCEE4"),
                4019 => LegacyKoreanPathSegment("\uD06C\uB9AC\uC5D0\uC774\uD130"),
                4020 => LegacyKoreanPathSegment("\uD074\uB77C\uC6B4"),
                4021 => LegacyKoreanPathSegment("\uC9D1\uC2DC"),
                4022 => LegacyKoreanPathSegment("\uD398\uCF54\uD314\uB77C\uB518"),
                4023 => LegacyKoreanPathSegment("\uCD08\uBCF4\uC790"),
                4024 => LegacyKoreanPathSegment("\uAC80\uC0AC"),
                4025 => LegacyKoreanPathSegment("\uB9C8\uBC95\uC0AC"),
                4026 => LegacyKoreanPathSegment("\uAD81\uC218"),
                4027 => LegacyKoreanPathSegment("\uC131\uC9C1\uC790"),
                4028 => LegacyKoreanPathSegment("\uC0C1\uC778"),
                4029 => LegacyKoreanPathSegment("\uB3C4\uB451"),
                4030 => LegacyKoreanPathSegment("\uAE30\uC0AC"),
                4031 => LegacyKoreanPathSegment("\uD504\uB9AC\uC2A4\uD2B8"),
                4032 => LegacyKoreanPathSegment("\uC704\uC800\uB4DC"),
                4033 => LegacyKoreanPathSegment("\uC81C\uCCA0\uACF5"),
                4034 => LegacyKoreanPathSegment("\uD5CC\uD130"),
                4035 => LegacyKoreanPathSegment("\uC5B4\uC138\uC2E0"),
                4036 => LegacyKoreanPathSegment("\uD398\uCF54\uD398\uCF54_\uAE30\uC0AC"),
                4037 => LegacyKoreanPathSegment("\uD06C\uB8E8\uC138\uC774\uB354"),
                4038 => LegacyKoreanPathSegment("\uBABD\uD06C"),
                4039 => LegacyKoreanPathSegment("\uC138\uC774\uC9C0"),
                4040 => LegacyKoreanPathSegment("\uB85C\uADF8"),
                4041 => LegacyKoreanPathSegment("\uC5F0\uAE08\uC220\uC0AC"),
                4042 => LegacyKoreanPathSegment("\uBC14\uB4DC"),
                4043 => LegacyKoreanPathSegment("\uBB34\uD76C"),
                4044 => LegacyKoreanPathSegment("\uC2E0\uD398\uCF54\uD06C\uB8E8\uC138\uC774\uB354"),
                4045 => LegacyKoreanPathSegment("\uC288\uD37C\uB178\uBE44\uC2A4"),
                4046 => LegacyKoreanPathSegment("\uD0DC\uAD8C\uC18C\uB144"),
                4047 => LegacyKoreanPathSegment("\uAD8C\uC131"),
                4049 => LegacyKoreanPathSegment("\uC18C\uC6B8\uB9C1\uCEE4"),
                4054 => LegacyKoreanPathSegment("\uB8EC\uB098\uC774\uD2B8"),
                4055 => LegacyKoreanPathSegment("\uC6CC\uB85D"),
                4056 => LegacyKoreanPathSegment("\uB808\uC778\uC838"),
                4057 => LegacyKoreanPathSegment("\uC544\uD06C\uBE44\uC20D"),
                4058 => LegacyKoreanPathSegment("\uBBF8\uCF00\uB2C9"),
                4059 => LegacyKoreanPathSegment("\uAE38\uB85C\uD2F4\uD06C\uB85C\uC2A4"),
                4060 => LegacyKoreanPathSegment("\uB8EC\uB098\uC774\uD2B8"),
                4061 => LegacyKoreanPathSegment("\uC6CC\uB85D"),
                4062 => LegacyKoreanPathSegment("\uB808\uC778\uC838"),
                4063 => LegacyKoreanPathSegment("\uC544\uD06C\uBE44\uC20D"),
                4064 => LegacyKoreanPathSegment("\uBBF8\uCF00\uB2C9"),
                4065 => LegacyKoreanPathSegment("\uAE38\uB85C\uD2F4\uD06C\uB85C\uC2A4"),
                4066 => LegacyKoreanPathSegment("\uAC00\uB4DC"),
                4067 => LegacyKoreanPathSegment("\uC18C\uC11C\uB7EC"),
                4068 => LegacyKoreanPathSegment("\uBBFC\uC2A4\uD2B8\uB7F4"),
                4069 => LegacyKoreanPathSegment("\uC6D0\uB354\uB7EC"),
                4070 => LegacyKoreanPathSegment("\uC288\uB77C"),
                4071 => LegacyKoreanPathSegment("\uC81C\uB124\uB9AD"),
                4072 => LegacyKoreanPathSegment("\uC250\uB3C4\uC6B0\uCCB4\uC774\uC11C"),
                4073 => LegacyKoreanPathSegment("\uAC00\uB4DC"),
                4074 => LegacyKoreanPathSegment("\uC18C\uC11C\uB7EC"),
                4075 => LegacyKoreanPathSegment("\uBBFC\uC2A4\uD2B8\uB7F4"),
                4076 => LegacyKoreanPathSegment("\uC6D0\uB354\uB7EC"),
                4077 => LegacyKoreanPathSegment("\uC288\uB77C"),
                4078 => LegacyKoreanPathSegment("\uC81C\uB124\uB9AD"),
                4079 => LegacyKoreanPathSegment("\uC250\uB3C4\uC6B0\uCCB4\uC774\uC11C"),
                4096 => LegacyKoreanPathSegment("\uB8EC\uB098\uC774\uD2B8"),
                4097 => LegacyKoreanPathSegment("\uC6CC\uB85D"),
                4098 => LegacyKoreanPathSegment("\uB808\uC778\uC838"),
                4099 => LegacyKoreanPathSegment("\uC544\uD06C\uBE44\uC20D"),
                4100 => LegacyKoreanPathSegment("\uBBF8\uCF00\uB2C9"),
                4101 => LegacyKoreanPathSegment("\uAE38\uB85C\uD2F4\uD06C\uB85C\uC2A4"),
                4102 => LegacyKoreanPathSegment("\uAC00\uB4DC"),
                4103 => LegacyKoreanPathSegment("\uC18C\uC11C\uB7EC"),
                4104 => LegacyKoreanPathSegment("\uBBFC\uC2A4\uD2B8\uB7F4"),
                4105 => LegacyKoreanPathSegment("\uC6D0\uB354\uB7EC"),
                4106 => LegacyKoreanPathSegment("\uC288\uB77C"),
                4107 => LegacyKoreanPathSegment("\uC81C\uB124\uB9AD"),
                4108 => LegacyKoreanPathSegment("\uC250\uB3C4\uC6B0\uCCB4\uC774\uC11C"),
                4190 => LegacyKoreanPathSegment("\uC288\uD37C\uB178\uBE44\uC2A4"),
                4191 => LegacyKoreanPathSegment("\uC288\uD37C\uB178\uBE44\uC2A4"),
                4211 => "kagerou",
                4212 => "oboro",
                4215 => "rebellion",
                4217 => "summoner",
                4218 => "summoner",
                4219 => "summoner",
                4220 => "summoner",
                4222 => LegacyKoreanPathSegment("\uB2CC\uC790"),
                4223 => "KAGEROU",
                4224 => "OBORO",
                4225 => LegacyKoreanPathSegment("\uD0DC\uAD8C\uC18C\uB144"),
                4226 => LegacyKoreanPathSegment("\uAD8C\uC131"),
                4227 => LegacyKoreanPathSegment("\uC18C\uC6B8\uB9C1\uCEE4"),
                4228 => LegacyKoreanPathSegment("\uAC74\uB108"),
                4229 => "REBELLION",
                4239 => LegacyKoreanPathSegment("\uC131\uC81C"),
                4240 => LegacyKoreanPathSegment("\uC18C\uC6B8\uB9AC\uD37C"),
                4241 => LegacyKoreanPathSegment("\uC131\uC81C"),
                4242 => LegacyKoreanPathSegment("\uC18C\uC6B8\uB9AC\uD37C"),
                4252 => "dragon_knight",
                4253 => "meister",
                4254 => "shadow_cross",
                4255 => "arch_mage",
                4256 => "cardinal",
                4257 => "windhawk",
                4258 => "imperial_guard",
                4259 => "biolo",
                4260 => "abyss_chaser",
                4261 => "elemetal_master",
                4262 => "inquisitor",
                4263 => "troubadour",
                4264 => "trouvere",
                4302 => "sky_emperor",
                4303 => "soul_ascetic",
                4304 => "shinkiro",
                4305 => "shiranui",
                4306 => "night_watch",
                4307 => "hyper_novice",
                4308 => "spirit_handler",
                4315 => "spirit_handler",
                _ => null
            };
        }

        private static int ResolveEffectiveBodyClassId(RoActorAppearanceRequest appearance) {
            if (appearance == null) {
                return 0;
            }

            if (IsMountedBodyClass(appearance.classId) ||
                (appearance.classId >= short.MinValue &&
                    appearance.classId <= short.MaxValue &&
                    RoMapRentalVisualSemantics.IsRentalVisualJob((short) appearance.classId))) {
                return appearance.classId;
            }

            return appearance.bodyStyleId > 0 &&
                appearance.bodyStyleId <= short.MaxValue &&
                appearance.bodyStyleId != appearance.classId
                    ? appearance.bodyStyleId
                    : appearance.classId;
        }

        private static bool IsMountedBodyClass(int classId) {
            return classId == 13 ||
                classId == 21 ||
                classId == 4014 ||
                classId == 4022 ||
                classId == 4036 ||
                classId == 4044;
        }

        private static string BodyRaceSegment(int classId) {
            return IsDoramBodyClass(classId) ? DoramRaceSegment() : HumanRaceSegment();
        }

        private static bool IsDoramBodyClass(int classId) {
            return (classId >= 4217 && classId <= 4220) ||
                classId == 4308 ||
                classId == 4315;
        }

        private static string AccessorySegment() {
            return LegacyPathSegment(0xBE, 0xC7, 0xBC, 0xBC, 0xBB, 0xE7, 0xB8, 0xAE);
        }

        private static string RobeSegment() {
            return LegacyPathSegment(0xB7, 0xCE, 0xBA, 0xEA);
        }

        private static string LegacyPathSegment(params int[] bytes) {
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) {
                chars[i] = (char) bytes[i];
            }

            return new string(chars);
        }

        private static string LegacyKoreanPathSegment(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            byte[] bytes = Encoding.GetEncoding(949).GetBytes(value);
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) {
                chars[i] = (char) bytes[i];
            }

            return new string(chars);
        }
    }
}
