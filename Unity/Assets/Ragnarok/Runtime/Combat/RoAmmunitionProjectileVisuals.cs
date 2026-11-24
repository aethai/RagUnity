using System;
using Ragnarok.Data;
using Ragnarok.Gameplay;

namespace Ragnarok.Combat {
    public interface IRoAmmunitionProjectileVisualContext {
        bool TryResolveEquippedAmmoProjectileSprite(uint actorId, out string spritePath);
    }

    public static class RoAmmunitionProjectileVisuals {
        public static bool TryResolveEquippedAmmoProjectileSpritePath(
            RoGameplayEquipmentSnapshot equipment,
            RoItemMetadataTable metadataTable,
            out string spritePath
        ) {
            return TryResolveEquippedAmmoProjectileSpritePath(
                equipment?.InventoryItems,
                metadataTable,
                out spritePath);
        }

        public static bool TryResolveEquippedAmmoProjectileSpritePath(
            RoItemInstance[] inventoryItems,
            RoItemMetadataTable metadataTable,
            out string spritePath
        ) {
            spritePath = null;
            if (!TryFindEquippedAmmo(inventoryItems, out RoItemInstance ammo)) {
                return false;
            }

            return TryResolveAmmoProjectileSpritePath(ammo, metadataTable, out spritePath);
        }

        public static bool TryFindEquippedAmmo(RoItemInstance[] inventoryItems, out RoItemInstance ammo) {
            if (inventoryItems != null) {
                for (int i = 0; i < inventoryItems.Length; i++) {
                    RoItemInstance item = inventoryItems[i];
                    if (item != null && (item.WearState & RoItemEquipLocation.Ammo) != 0) {
                        ammo = item;
                        return true;
                    }
                }
            }

            ammo = null;
            return false;
        }

        public static bool TryResolveAmmoProjectileSpritePath(
            RoItemInstance ammo,
            RoItemMetadataTable metadataTable,
            out string spritePath
        ) {
            spritePath = null;
            if (ammo == null) {
                return false;
            }

            RoItemMetadata metadata = ammo.ResolveMetadata(metadataTable ?? RoItemMetadataTable.Empty);
            string resourceName = metadata?.GetResourceName(ammo.IsIdentified);
            if (string.IsNullOrWhiteSpace(resourceName) ||
                string.Equals(resourceName, "unknown", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string basePath = ammo.GetWorldSpriteBasePath(metadata);
            if (string.IsNullOrWhiteSpace(basePath)) {
                return false;
            }

            spritePath = basePath.Trim();
            return true;
        }
    }
}
