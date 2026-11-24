using System;
using System.Threading.Tasks;
using Ragnarok.Data;
using Ragnarok.Entities;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.Sprites;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The item side of the classic inventory and equipment windows, without UI: the session and its item commands (use,
    /// wear, take off, drop, card compound, the equipment display option), the item kind rules the windows sort and
    /// activate by, and the character's look for the equipment preview.
    /// </summary>
    public sealed class RoInventoryModel {
        /// <summary>CZ_CONFIG type 0: other players may look at the character's equipment.</summary>
        public const int ShowEquipConfigType = 0;
        private const float WorldControllerLookupRetrySeconds = 0.5f;

        private RoLoginShellSession session;
        private IRoInventoryCommandExecutor commands;
        private RoOnlineWorldController worldController;
        private float nextWorldControllerLookupTime;

        public RoLoginShellSession Session => session;
        public RoItemMetadataTable Metadata { get; set; } = RoItemMetadataTable.Empty;

        /// <summary>The card compound list the player dismissed (its window does not come back for that list).</summary>
        public int DismissedCardCompositionSequence { get; set; } = int.MinValue;

        public void Bind(RoLoginShellSession loginSession) {
            if (ReferenceEquals(session, loginSession)) return;
            session = loginSession;
            commands = loginSession != null ? new RoLegacyInventoryCommandExecutor(loginSession) : null;
            DismissedCardCompositionSequence = int.MinValue;
        }

        /// <summary>Item requests go out only while the character is on a map.</summary>
        public bool CanSendCommand => session != null && commands != null && session.CommandStateSnapshot().CanSendMapCommand;

        // ---- item kinds ----

        /// <summary>The table's item type, else the server's (item list packets).</summary>
        public RoItemType ItemType(RoItemInstance item) {
            RoItemMetadata metadata = item.ResolveMetadata(Metadata);
            if (metadata != null && metadata.ItemType != RoItemType.Unknown) return metadata.ItemType;
            switch (item.ItemTypeRaw) {             // server item types
                case 0: return RoItemType.Healing;
                case 2: return RoItemType.Usable;
                case 3: return RoItemType.Etc;
                case 4: return RoItemType.Weapon;
                case 5: return RoItemType.Armor;
                case 6: return RoItemType.Card;
                case 7: return RoItemType.PetEgg;
                case 8: return RoItemType.PetArmor;
                case 10: return RoItemType.Ammo;
                case 11: return RoItemType.DelayConsume;
                case 12: return RoItemType.ShadowGear;
                case 18: return RoItemType.Cash;
                default: return RoItemType.Unknown;
            }
        }

        /// <summary>The inventory tab: Use = healing / usable / delayed / cash, Eqp = weapons and armour, Etc = the rest.</summary>
        public int TabOf(RoItemInstance item) {
            switch (ItemType(item)) {
                case RoItemType.Healing:
                case RoItemType.Usable:
                case RoItemType.DelayConsume:
                case RoItemType.Cash:
                    return 0;
                case RoItemType.Weapon:
                case RoItemType.Armor:
                case RoItemType.ShadowGear:
                case RoItemType.PetArmor:
                    return 1;
                default:
                    return 2;
            }
        }

        /// <summary>FUN_008f7aa0: usable 0, cash 1, armour 2, weapons 3, ammunition 4, cards 5, misc 6.</summary>
        public int SortRank(RoItemInstance item) {
            switch (ItemType(item)) {
                case RoItemType.Healing:
                case RoItemType.Usable:
                case RoItemType.DelayConsume: return 0;
                case RoItemType.Cash: return 1;
                case RoItemType.Armor:
                case RoItemType.ShadowGear:
                case RoItemType.PetArmor: return 2;
                case RoItemType.Weapon: return 3;
                case RoItemType.Ammo: return 4;
                case RoItemType.Card: return 5;
                case RoItemType.Etc:
                case RoItemType.PetEgg: return 6;
                default: return 10;
            }
        }

        /// <summary>A hatched pet egg (9001..9499, flagged damaged) is held but never drawn.</summary>
        public static bool IsHatchedEgg(RoItemInstance item) => item.ItemId >= 9001 && item.ItemId <= 9499 && item.IsDamaged;

        public static bool IsCardItem(RoItemInstance item, RoItemMetadata metadata) {
            if (item == null) return false;
            return metadata != null && metadata.ItemType == RoItemType.Card || item.ItemTypeRaw == (byte) RoItemType.Card;
        }

        private static bool CanUseItem(RoItemInstance item) {
            if (item == null || item.Count <= 0 || item.IsEquipped) return false;
            RoItemType rawType = item.ItemTypeRaw != 0 ? unchecked((RoItemType) item.ItemTypeRaw) : RoItemType.Unknown;
            if (rawType == RoItemType.Card) return false;
            return item.EquipLocationHint == RoItemEquipLocation.None &&
                (rawType == RoItemType.Unknown ||
                    rawType == RoItemType.Healing ||
                    rawType == RoItemType.Usable ||
                    rawType == RoItemType.DelayConsume ||
                    rawType == RoItemType.Cash);
        }

        private static bool CanWearItem(RoItemInstance item) {
            if (item == null || item.Count <= 0 || item.IsEquipped) return false;
            RoItemType rawType = item.ItemTypeRaw != 0 ? unchecked((RoItemType) item.ItemTypeRaw) : RoItemType.Unknown;
            if (rawType == RoItemType.Card) return false;
            if (item.EquipLocationHint != RoItemEquipLocation.None) return true;
            return rawType == RoItemType.Weapon ||
                rawType == RoItemType.Armor ||
                rawType == RoItemType.PetEgg ||
                rawType == RoItemType.PetArmor ||
                rawType == RoItemType.ShadowGear;
        }

        public bool CanUseInventoryItem(RoItemInstance item) =>
            item != null && !IsCardItem(item, item.ResolveMetadata(Metadata)) && CanUseItem(item);

        public bool CanWearInventoryItem(RoItemInstance item) =>
            item != null && !IsCardItem(item, item.ResolveMetadata(Metadata)) && CanWearItem(item);

        /// <summary>What the equipment window takes when dropped on it: weapons, armour, ammunition, shadow gear, anything with a location.</summary>
        private bool IsEquipmentDropItem(RoItemInstance item) {
            if (item == null) return false;
            RoItemMetadata metadata = item.ResolveMetadata(Metadata);
            RoItemType metadataType = metadata != null ? metadata.ItemType : RoItemType.Unknown;
            if (metadataType == RoItemType.Weapon ||
                metadataType == RoItemType.Armor ||
                metadataType == RoItemType.Ammo ||
                metadataType == RoItemType.ShadowGear) {
                return true;
            }

            RoItemType rawType = item.ItemTypeRaw != 0 ? unchecked((RoItemType) item.ItemTypeRaw) : RoItemType.Unknown;
            return rawType == RoItemType.Weapon ||
                rawType == RoItemType.Armor ||
                rawType == RoItemType.Ammo ||
                rawType == RoItemType.ShadowGear ||
                item.ResolveEquipLocations(metadata) != RoItemEquipLocation.None;
        }

        public static bool TryFindItemByBagIndex(RoItemInstance[] items, int bagIndex, out RoItemInstance item) {
            item = null;
            if (items == null) return false;
            for (int i = 0; i < items.Length; i++) {
                RoItemInstance candidate = items[i];
                if (candidate != null && candidate.BagIndex == bagIndex) {
                    item = candidate;
                    return true;
                }
            }

            return false;
        }

        // ---- commands ----

        /// <summary>The inventory's double click: a card asks for its compound targets, equipment is put on, the rest used.</summary>
        public async Task ActivateAsync(int bagIndex) {
            if (!CanSendCommand) return;
            RoGameplayInventorySnapshot inventory = session.InventorySnapshot();
            if (!TryFindItemByBagIndex(inventory.InventoryItems, bagIndex, out RoItemInstance item)) return;
            if (IsCardItem(item, item.ResolveMetadata(Metadata))) {
                await RequestCardCompositionListAsync(item);
            } else if (CanWearInventoryItem(item)) {
                await RunItemCommandAsync(commands.EquipInventoryItemAsync(item.BagIndex, unchecked((uint) item.ResolveEquipLocations(Metadata))), "equipped");
            } else if (CanUseInventoryItem(item)) {
                await RunItemCommandAsync(commands.UseInventoryItemAsync(item.BagIndex), "used");
            }
        }

        /// <summary>CZ_ITEM_THROW: <paramref name="amount"/> of the stack goes to the ground.</summary>
        public void Drop(int bagIndex, int amount) {
            if (bagIndex <= 0 || !CanSendCommand) return;
            amount = Mathf.Max(1, amount);
            RoItemTransferDebugLog.UiTransfer(nameof(RoClassicInventory), RoInventoryDragDropAction.DropToWorld, bagIndex, amount);
            _ = RunItemCommandAsync(commands.DropInventoryItemAsync(bagIndex, amount), "dropped");
        }

        /// <summary>CZ_REQ_TAKEOFF_EQUIP.</summary>
        public void Unequip(int bagIndex) {
            if (CanSendCommand) _ = RunItemCommandAsync(commands.UnequipInventoryItemAsync(bagIndex), "taken off");
        }

        /// <summary>
        /// An inventory item dropped on the equipment window at <paramref name="location"/>: put on there when the item is
        /// identified, not worn, wearable and allowed at that location (CZ_REQ_WEAR_EQUIP). False when it is not.
        /// </summary>
        public bool TryEquipAt(int bagIndex, RoItemEquipLocation location) {
            if (location == RoItemEquipLocation.None) return false;
            RoGameplayEquipmentSnapshot snapshot = session != null ? session.EquipmentSnapshot() : default;
            if (!TryFindItemByBagIndex(snapshot.InventoryItems, bagIndex, out RoItemInstance item) ||
                item == null ||
                !item.IsIdentified ||
                item.IsEquipped ||
                !IsEquipmentDropItem(item)) {
                return false;
            }

            RoItemMetadata metadata = item.ResolveMetadata(Metadata);
            RoItemEquipLocation allowed = item.ResolveEquipLocations(metadata);
            if (allowed == RoItemEquipLocation.None || (allowed & location) == 0) return false;
            if (CanSendCommand) _ = RunItemCommandAsync(commands.EquipInventoryItemAsync(item.BagIndex, unchecked((uint) location)), "equipped");
            return true;
        }

        private async Task RequestCardCompositionListAsync(RoItemInstance card) {
            if (card == null || commands == null) return;
            DismissedCardCompositionSequence = int.MinValue;
            RoItemCommandResult result = await commands.RequestCardCompositionListAsync(card.BagIndex);
            if (!result.Accepted) Debug.LogWarning("Inventory item was not requested card targets: " + result.Status + " " + result.Message);
        }

        /// <summary>CZ_REQ_ITEMCOMPOSITION: the card goes into the picked equipment.</summary>
        public void InsertCard(int cardBagIndex, int equipBagIndex) {
            if (CanSendCommand) _ = RunItemCommandAsync(commands.InsertCardAsync(cardBagIndex, equipBagIndex), "inserted card");
        }

        /// <summary>The compound list is closed without a pick: forgotten for its sequence and cleared in the session.</summary>
        public void CancelCardComposition(int sequence) {
            DismissedCardCompositionSequence = sequence;
            session?.ClearLegacyCardCompositionSelection();
        }

        /// <summary>CZ_CONFIG: true when the request went out.</summary>
        public async Task<bool> SetEquipmentConfigAsync(int config, int value) {
            if (!CanSendCommand) return false;
            try {
                RoItemCommandResult result = await commands.SetEquipmentConfigAsync(config, value);
                if (result.Accepted) return true;
                Debug.LogWarning("Equipment display config was not updated: " + result.Status + " " + result.Message);
            } catch (Exception ex) {
                Debug.LogWarning($"Equipment display config was not updated: {ex.Message}");
            }

            return false;
        }

        private static async Task RunItemCommandAsync(Task<RoItemCommandResult> command, string action) {
            if (command == null) return;
            RoItemCommandResult result = await command;
            if (!result.Accepted) Debug.LogWarning("Inventory item was not " + action + ": " + result.Status + " " + result.Message);
        }

        // ---- the character's look (equipment preview) ----

        /// <summary>
        /// The own character's appearance standing south: from the map actor when the world has it, else from the selected
        /// character's login record. False before either exists.
        /// </summary>
        public bool TryBuildPreviewAppearance(out RoActorAppearanceRequest request, out int hash) {
            request = null;
            hash = 0;

            RoOnlineWorldController controller = ResolveWorldController();
            if (controller != null) {
                if (controller.OwnActorId != 0 &&
                    controller.State.TryGetActor(controller.OwnActorId, out RoOnlineActorSnapshot actor)) {
                    request = RoOnlineActorPresenter.BuildPlayerAppearanceRequest(actor);
                    request.motion = RoSpriteMotion.Idle;
                    request.facingDirection = RoActorDirection.South;
                    request.headFacing = RoActorHeadFacing.Center;
                    hash = BuildPreviewHash(request);
                    return true;
                }
            }

            RoShellBasicInfoSnapshot basic = session != null ? session.BasicInfoSnapshot() : default;
            if (!basic.HasCharacter) return false;

            RoLoginShellCharacter character = basic.Character;
            int hairStyle = Mathf.Max(0, character.Head);
            int hairPalette = character.HeadPalette != 0 ? character.HeadPalette : character.HairColor;
            request = new RoActorAppearanceRequest {
                kind = RoActorAppearanceKind.Player,
                serverId = character.CharacterId,
                classId = character.JobId,
                bodyStyleId = character.Body,
                isMainCharacter = false,
                isMale = character.Sex != 0,
                headId = hairStyle,
                hairId = hairStyle,
                hairColorId = hairPalette,
                bodyPaletteId = character.BodyPalette,
                weaponId = character.Weapon,
                shieldId = character.Shield,
                robeId = 0,
                headgearTopId = character.Accessory2,
                headgearMidId = character.Accessory3,
                headgearLowerId = character.Accessory,
                motionProfile = RoSpriteMotionProfile.Player,
                motion = RoSpriteMotion.Idle,
                facingDirection = RoActorDirection.South,
                headFacing = RoActorHeadFacing.Center
            };
            hash = BuildPreviewHash(request);
            return true;
        }

        private static int BuildPreviewHash(RoActorAppearanceRequest request) {
            unchecked {
                int hash = 17;
                hash = hash * 397 ^ request.serverId;
                hash = hash * 397 ^ request.classId;
                hash = hash * 397 ^ request.bodyStyleId;
                hash = hash * 397 ^ request.isMale.GetHashCode();
                hash = hash * 397 ^ request.headId;
                hash = hash * 397 ^ request.hairColorId;
                hash = hash * 397 ^ request.bodyPaletteId;
                hash = hash * 397 ^ request.weaponId;
                hash = hash * 397 ^ request.shieldId;
                hash = hash * 397 ^ request.robeId;
                hash = hash * 397 ^ request.headgearTopId;
                hash = hash * 397 ^ request.headgearMidId;
                hash = hash * 397 ^ request.headgearLowerId;
                return hash;
            }
        }

        /// <summary>The map's world controller, looked up at most twice a second while there is none.</summary>
        private RoOnlineWorldController ResolveWorldController() {
            if (worldController != null) return worldController;
            float now = Time.unscaledTime;
            if (now < nextWorldControllerLookupTime) return null;
            nextWorldControllerLookupTime = now + WorldControllerLookupRetrySeconds;
            worldController = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            return worldController;
        }
    }
}
