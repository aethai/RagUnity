using System;
using System.Text;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal static class RoShellGameplayCommands {
        public static byte[] BuildHotkey(
            RoGameplaySnapshot snapshot,
            int hotkeyIndex,
            int localAid,
            int targetAid,
            out RoGameplayHotkeyAction action) {
            if (!RoGameplayActionResolver.TryResolveHotkey(snapshot, hotkeyIndex, out action) ||
                !RoGameplayActionResolver.TryBuildHotkeyPacket(snapshot, hotkeyIndex, localAid, targetAid, out byte[] packet)) {
                throw new InvalidOperationException($"Hotkey F{hotkeyIndex + 1} has no usable item or skill.");
            }

            return packet;
        }

        public static byte[] BuildSetHotkey(
            RoGameplaySnapshot snapshot,
            int hotkeyIndex,
            RoShortcutKeyEntry shortcut,
            out int tab) {
            if (hotkeyIndex < 0 || hotkeyIndex >= 38) {
                throw new ArgumentOutOfRangeException(nameof(hotkeyIndex));
            }

            tab = Math.Max(0, snapshot.HotkeyTab);
            return RoOutboundPackets.BuildCzShortcutKeyChange2(tab, hotkeyIndex, shortcut);
        }

        public static byte[] BuildUpgradeSkill(RoGameplaySnapshot snapshot, int skillId, out RoGameplaySkillInfo skill) {
            skill = default;
            if (skillId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(skillId));
            }

            snapshot ??= RoGameplaySnapshot.Empty;
            if (snapshot.Status == null || snapshot.Status.SkillPoints <= 0) {
                throw new InvalidOperationException("No skill points are available.");
            }

            RoGameplaySkillInfo[] skills = snapshot.Skills ?? Array.Empty<RoGameplaySkillInfo>();
            for (int i = 0; i < skills.Length; i++) {
                if (skills[i].SkillId != skillId) {
                    continue;
                }

                skill = skills[i];
                if (!skill.CanUpgrade) {
                    throw new InvalidOperationException($"Skill {skillId} is not currently upgradable.");
                }

                return RoOutboundPackets.BuildCzUpgradeSkillLevel(skillId);
            }

            throw new InvalidOperationException($"Skill {skillId} is not known by the client.");
        }

        public static byte[] BuildPlannedUpgradeSkill(int skillId) {
            if (skillId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(skillId));
            }

            return RoOutboundPackets.BuildCzUpgradeSkillLevel(skillId);
        }

        public static byte[] BuildAllocateStat(RoGameplaySnapshot snapshot, RoStatusStat stat, int amount) {
            if (!Enum.IsDefined(typeof(RoStatusStat), stat)) {
                throw new ArgumentOutOfRangeException(nameof(stat));
            }

            if (amount <= 0 || amount > byte.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(amount), "Stat allocation amount must fit in one byte.");
            }

            snapshot ??= RoGameplaySnapshot.Empty;
            RoPlayerStatusSnapshot status = snapshot.Status ?? RoPlayerStatusSnapshot.Empty;
            if (status.HasKnownStatusPoints && amount > status.StatusPoints) {
                throw new InvalidOperationException("Not enough status points.");
            }

            return RoOutboundPackets.BuildCzStatusUp(stat, amount);
        }

        public static byte[] BuildUseInventoryItem(
            RoGameplaySnapshot snapshot,
            int bagIndex,
            int localAid,
            out RoItemInstance item) {
            if (!RoGameplayActionResolver.TryBuildUseInventoryItemPacket(snapshot, bagIndex, localAid, out item, out byte[] packet)) {
                throw new InvalidOperationException($"Inventory bag index {bagIndex} has no usable item.");
            }

            return packet;
        }

        public static byte[] BuildDropInventoryItem(
            RoGameplaySnapshot snapshot,
            int bagIndex,
            int count,
            out RoItemInstance item) {
            if (!RoGameplayActionResolver.TryBuildDropInventoryItemPacket(snapshot, bagIndex, count, out item, out byte[] packet)) {
                throw new InvalidOperationException($"Inventory bag index {bagIndex} cannot be dropped.");
            }

            return packet;
        }

        public static byte[] BuildWearInventoryItem(
            RoGameplaySnapshot snapshot,
            int bagIndex,
            out RoItemInstance item) {
            if (!RoGameplayActionResolver.TryBuildWearInventoryItemPacket(snapshot, bagIndex, out item, out _, out byte[] packet)) {
                throw new InvalidOperationException($"Inventory bag index {bagIndex} has no wearable item.");
            }

            return packet;
        }

        public static byte[] BuildTakeoffInventoryItem(
            RoGameplaySnapshot snapshot,
            int bagIndex,
            out RoItemInstance item) {
            if (!RoGameplayActionResolver.TryBuildTakeoffInventoryItemPacket(snapshot, bagIndex, out item, out byte[] packet)) {
                throw new InvalidOperationException($"Inventory bag index {bagIndex} has no equipped item.");
            }

            return packet;
        }

        public static byte[] BuildRequestCardCompositionList(
            RoGameplaySnapshot snapshot,
            int cardBagIndex,
            out RoItemInstance card) {
            card = FindInventoryItem(snapshot, cardBagIndex);
            if (!IsCardItem(card)) {
                throw new InvalidOperationException($"Inventory bag index {cardBagIndex} has no card item.");
            }

            return RoOutboundPackets.BuildCzReqItemCompositionList(cardBagIndex);
        }

        public static byte[] BuildInsertCard(
            RoGameplaySnapshot snapshot,
            int cardBagIndex,
            int equipBagIndex,
            out RoItemInstance card,
            out RoItemInstance equipment) {
            card = FindInventoryItem(snapshot, cardBagIndex);
            if (!IsCardItem(card)) {
                throw new InvalidOperationException($"Inventory bag index {cardBagIndex} has no card item.");
            }

            equipment = FindInventoryItem(snapshot, equipBagIndex);
            if (equipment == null || (equipment.EquipLocationHint == RoItemEquipLocation.None && !IsEquipmentItemType(equipment.ItemTypeRaw))) {
                throw new InvalidOperationException($"Inventory bag index {equipBagIndex} has no card composition target.");
            }

            return RoOutboundPackets.BuildCzReqItemComposition(cardBagIndex, equipBagIndex);
        }

        public static byte[] BuildIdentifyItem(
            RoGameplaySnapshot snapshot,
            int bagIndex,
            out RoItemInstance item) {
            item = FindInventoryItem(snapshot, bagIndex);
            if (item == null) {
                throw new InvalidOperationException($"Inventory bag index {bagIndex} has no item to identify.");
            }

            return RoOutboundPackets.BuildCzReqItemIdentify(bagIndex);
        }

        public static byte[] BuildCancelIdentifyItem() {
            return RoOutboundPackets.BuildCzCancelItemIdentify();
        }

        public static byte[] BuildMakingArrow(RoServerProfile profile, uint itemId) {
            if (itemId == 0) {
                throw new ArgumentOutOfRangeException(nameof(itemId));
            }

            return CreateProfiledPackets(profile).BuildCzReqMakingArrow(itemId);
        }

        public static byte[] BuildChangeCart(RoServerProfile profile, int cartNumber) {
            if (cartNumber < 0 || cartNumber > 9) {
                throw new ArgumentOutOfRangeException(nameof(cartNumber), "Change Cart accepts RoBrowser/rAthena cart numbers 0 through 9.");
            }

            return CreateProfiledPackets(profile).BuildCzReqChangeCart(cartNumber);
        }

        public static byte[] BuildSelectCartDecoration(RoServerProfile profile, uint identity, int cartType) {
            if (cartType < 0 || cartType > byte.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(cartType), "Decorate Cart accepts byte cart type IDs advertised by rAthena.");
            }

            return CreateProfiledPackets(profile).BuildCzSelectCart(identity, cartType);
        }

        public static byte[] BuildSelectWarpPoint(RoServerProfile profile, ushort skillId, string mapName, Encoding encoding = null) {
            if (skillId == 0) {
                throw new ArgumentOutOfRangeException(nameof(skillId));
            }

            if (string.IsNullOrWhiteSpace(mapName)) {
                throw new ArgumentException("Warp point map name is required.", nameof(mapName));
            }

            return CreateProfiledPackets(profile).BuildCzSelectWarpPoint(skillId, mapName.Trim(), encoding ?? Encoding.ASCII);
        }

        public static byte[] BuildEquipmentConfig(int config, int value) {
            return RoOutboundPackets.BuildCzConfig(config, value);
        }

        public static byte[] BuildQuestActive(uint questId, bool active) {
            if (questId == 0) {
                throw new ArgumentOutOfRangeException(nameof(questId));
            }

            return RoOutboundPackets.BuildCzQuestActive(questId, active);
        }

        public static byte[] BuildChatMessage(string characterName, string message, Encoding encoding) {
            string chatText = (message ?? string.Empty).Trim();
            if (chatText.Length == 0) {
                throw new ArgumentException("Chat message is required.", nameof(message));
            }

            return RoOutboundPackets.BuildCzRequestChat(characterName, chatText, encoding);
        }

        public static byte[] BuildPrivateMessage(string receiverName, string message, Encoding encoding) {
            string chatText = (message ?? string.Empty).Trim();
            if (chatText.Length == 0) {
                throw new ArgumentException("Chat message is required.", nameof(message));
            }

            return RoOutboundPackets.BuildCzWhisper(receiverName, chatText, encoding);
        }

        public static byte[] BuildPartyChatMessage(string characterName, string message, Encoding encoding) {
            string chatText = (message ?? string.Empty).Trim();
            if (chatText.Length == 0) {
                throw new ArgumentException("Chat message is required.", nameof(message));
            }

            return RoOutboundPackets.BuildCzPartyChat(characterName, chatText, encoding);
        }

        public static byte[] BuildGuildChatMessage(string characterName, string message, Encoding encoding) {
            string chatText = (message ?? string.Empty).Trim();
            if (chatText.Length == 0) {
                throw new ArgumentException("Chat message is required.", nameof(message));
            }

            return RoOutboundPackets.BuildCzGuildChat(characterName, chatText, encoding);
        }

        public static byte[] BuildClanChatMessage(string characterName, string message, Encoding encoding) {
            string chatText = (message ?? string.Empty).Trim();
            if (chatText.Length == 0) {
                throw new ArgumentException("Chat message is required.", nameof(message));
            }

            return RoOutboundPackets.BuildCzClanChat(characterName, chatText, encoding);
        }

        public static byte[] BuildMoveRequest(int x, int y, int direction) {
            return RoOutboundPackets.BuildCzRequestMove2(x, y, direction);
        }

        public static byte[] BuildContactNpc(int npcId, byte type) {
            if (npcId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(npcId));
            }

            return RoOutboundPackets.BuildCzContactNpc(npcId, type);
        }

        public static byte[] BuildNpcDialogNext(RoGameplaySnapshot snapshot, out uint npcId) {
            npcId = snapshot.NpcDialog.NpcId;
            if (npcId == 0) {
                throw new InvalidOperationException("NPC dialog is not open.");
            }

            return RoOutboundPackets.BuildCzReqNextScript(RequireLegacyAid(npcId, nameof(npcId)));
        }

        public static byte[] BuildNpcDialogClose(RoGameplaySnapshot snapshot, out uint npcId) {
            npcId = snapshot.NpcDialog.NpcId;
            if (npcId == 0 && snapshot.NpcShop.IsDealTypePending) {
                npcId = snapshot.NpcShop.NpcId;
            }

            if (npcId == 0) {
                throw new InvalidOperationException("NPC dialog is not open.");
            }

            return RoOutboundPackets.BuildCzCloseDialog(RequireLegacyAid(npcId, nameof(npcId)));
        }

        public static byte[] BuildNpcDialogMenuChoice(RoGameplaySnapshot snapshot, int choice) {
            if (choice < 1 || choice > byte.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(choice));
            }

            uint npcId = snapshot.NpcDialog.NpcId;
            if (npcId == 0 || !snapshot.NpcDialog.HasMenu) {
                throw new InvalidOperationException("NPC menu is not open.");
            }

            return RoOutboundPackets.BuildCzNpcSelectMenu(RequireLegacyAid(npcId, nameof(npcId)), (byte) choice);
        }

        public static byte[] BuildNpcDialogNumberInput(RoGameplaySnapshot snapshot, int value) {
            uint npcId = snapshot.NpcDialog.NpcId;
            if (npcId == 0 || !snapshot.NpcDialog.WantsNumberInput) {
                throw new InvalidOperationException("NPC number input is not open.");
            }

            return RoOutboundPackets.BuildCzInputEditDlg(RequireLegacyAid(npcId, nameof(npcId)), value);
        }

        public static byte[] BuildNpcDialogTextInput(RoGameplaySnapshot snapshot, string value, Encoding encoding) {
            uint npcId = snapshot.NpcDialog.NpcId;
            if (npcId == 0 || !snapshot.NpcDialog.WantsTextInput) {
                throw new InvalidOperationException("NPC text input is not open.");
            }

            return RoOutboundPackets.BuildCzInputEditDlgStr(RequireLegacyAid(npcId, nameof(npcId)), value ?? string.Empty, encoding);
        }

        public static byte[] BuildPickupGroundItem(int itemAid) {
            if (itemAid <= 0) {
                throw new ArgumentOutOfRangeException(nameof(itemAid));
            }

            return RoOutboundPackets.BuildCzItemPickup2(itemAid);
        }

        public static byte[] BuildSitStand(bool sit) {
            return RoOutboundPackets.BuildCzActionRequest2(
                0,
                sit ? (byte) RoMapActorActionType.SitDown : (byte) RoMapActorActionType.StandUp);
        }

        public static byte[] BuildAttackTarget(int targetAid, bool repeatAttack) {
            if (targetAid <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetAid));
            }

            if (!RoGameplayActionResolver.TryBuildAttackPacket(targetAid, repeatAttack, out byte[] packet)) {
                throw new InvalidOperationException("Attack target is not valid.");
            }

            return packet;
        }

        public static byte[] BuildLegacyUseSkillOnTarget(RoGameplaySnapshot snapshot, int skillId, int skillLevel, int targetAid) {
            if (skillId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(skillId));
            }

            if (targetAid <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetAid));
            }

            if (!RoGameplayActionResolver.TryBuildUseSkillPacket(snapshot, skillId, skillLevel, targetAid, out _, out byte[] packet)) {
                throw new InvalidOperationException("Target skill request is not valid.");
            }

            return packet;
        }

        public static byte[] BuildLegacyUseSkillOnGround(RoGameplaySnapshot snapshot, int skillId, int skillLevel, int x, int y) {
            if (skillId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(skillId));
            }

            if (!RoGameplayActionResolver.TryBuildUseSkillToGroundPacket(snapshot, skillId, skillLevel, x, y, out _, out byte[] packet)) {
                throw new InvalidOperationException("Ground skill request is not valid.");
            }

            return packet;
        }

        public static byte[] BuildNpcShopSelectDealType(RoGameplaySnapshot snapshot, byte type) {
            if (type > 1) {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            uint npcId = snapshot.NpcShop.NpcId;
            if (npcId == 0) {
                throw new InvalidOperationException("NPC shop deal type is not pending.");
            }

            return RoOutboundPackets.BuildCzAckSelectDealType(RequireLegacyAid(npcId, nameof(npcId)), type);
        }

        public static byte[] BuildNpcShopBuyFirst(RoGameplaySnapshot snapshot, int amount) {
            return BuildNpcShopBuyFirst(snapshot, amount, default);
        }

        public static byte[] BuildNpcShopBuyFirst(RoGameplaySnapshot snapshot, int amount, RoServerProfile profile) {
            RoNpcShopBuyItem[] items = snapshot.NpcShop.BuyItems;
            if (items.Length == 0) {
                throw new InvalidOperationException("NPC buy list is empty.");
            }

            var request = new RoNpcShopPurchaseRequest(RequirePositiveUInt16(amount, nameof(amount)), items[0].ItemId);
            return BuildNpcShopBuy(profile, request);
        }

        public static byte[] BuildNpcShopBuy(uint itemId, int amount) {
            return BuildNpcShopBuy(itemId, amount, default);
        }

        public static byte[] BuildNpcShopBuy(uint itemId, int amount, RoServerProfile profile) {
            if (itemId == 0) {
                throw new ArgumentOutOfRangeException(nameof(itemId));
            }

            return BuildNpcShopBuy(profile, new RoNpcShopPurchaseRequest(RequirePositiveUInt16(amount, nameof(amount)), itemId));
        }

        public static byte[] BuildNpcShopBuy(params RoNpcShopPurchaseRequest[] items) {
            return BuildNpcShopBuy(default, items);
        }

        public static byte[] BuildNpcShopBuy(RoServerProfile profile, params RoNpcShopPurchaseRequest[] items) {
            if (items == null || items.Length == 0) {
                throw new ArgumentException("NPC shop buy request must contain at least one item.", nameof(items));
            }

            return CreateProfiledPackets(profile).BuildCzPcPurchaseItemList(NormalizeNpcShopPurchaseRequests(items));
        }

        public static byte[] BuildNpcShopSellFirst(RoGameplaySnapshot snapshot, int amount) {
            return BuildNpcShopSellFirst(snapshot, amount, default);
        }

        public static byte[] BuildNpcShopSellFirst(RoGameplaySnapshot snapshot, int amount, RoServerProfile profile) {
            RoNpcShopSellItem[] items = snapshot.NpcShop.SellItems;
            if (items.Length == 0) {
                throw new InvalidOperationException("NPC sell list is empty.");
            }

            var request = new RoNpcShopSellRequest(items[0].BagIndex, RequirePositiveUInt16(amount, nameof(amount)));
            return BuildNpcShopSell(profile, request);
        }

        public static byte[] BuildNpcShopSell(int bagIndex, int amount) {
            return BuildNpcShopSell(bagIndex, amount, default);
        }

        public static byte[] BuildNpcShopSell(int bagIndex, int amount, RoServerProfile profile) {
            return BuildNpcShopSell(profile, new RoNpcShopSellRequest(RequirePositiveUInt16(bagIndex, nameof(bagIndex)), RequirePositiveUInt16(amount, nameof(amount))));
        }

        public static byte[] BuildNpcShopSell(params RoNpcShopSellRequest[] items) {
            return BuildNpcShopSell(default, items);
        }

        public static byte[] BuildNpcShopSell(RoServerProfile profile, params RoNpcShopSellRequest[] items) {
            if (items == null || items.Length == 0) {
                throw new ArgumentException("NPC shop sell request must contain at least one item.", nameof(items));
            }

            return CreateProfiledPackets(profile).BuildCzPcSellItemList(NormalizeNpcShopSellRequests(items));
        }

        public static byte[] BuildNpcShopClose() {
            return BuildNpcShopClose(default);
        }

        public static byte[] BuildNpcShopClose(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzNpcTradeQuit();
        }

        public static byte[] BuildVendingOpenStore(string shopName, params RoVendingOpenStoreItem[] items) {
            return BuildVendingOpenStore(default, shopName, items);
        }

        public static byte[] BuildVendingOpenStore(RoServerProfile profile, string shopName, params RoVendingOpenStoreItem[] items) {
            if (items == null || items.Length == 0) {
                throw new ArgumentException("Vending open request must contain at least one cart item.", nameof(items));
            }

            return CreateProfiledPackets(profile).BuildCzReqOpenStore2(shopName, 1, NormalizeVendingOpenStoreItems(items));
        }

        public static byte[] BuildVendingCancelSetup(string shopName = "") {
            return BuildVendingCancelSetup(default, shopName);
        }

        public static byte[] BuildVendingCancelSetup(RoServerProfile profile, string shopName = "") {
            return CreateProfiledPackets(profile).BuildCzReqOpenStore2(shopName ?? string.Empty, 0, Array.Empty<RoVendingOpenStoreItem>());
        }

        public static byte[] BuildVendingCloseStore() {
            return BuildVendingCloseStore(default);
        }

        public static byte[] BuildVendingCloseStore(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqCloseStore();
        }

        public static byte[] BuildVendingRequestPlayerShop(uint ownerAid) {
            return BuildVendingRequestPlayerShop(ownerAid, default);
        }

        public static byte[] BuildVendingRequestPlayerShop(uint ownerAid, RoServerProfile profile) {
            if (ownerAid == 0) {
                throw new ArgumentOutOfRangeException(nameof(ownerAid));
            }

            return CreateProfiledPackets(profile).BuildCzReqBuyFromMc(ownerAid);
        }

        public static byte[] BuildVendingPurchaseFromPlayerShop(uint ownerAid, uint uniqueId, params RoPlayerShopPurchaseRequest[] items) {
            return BuildVendingPurchaseFromPlayerShop(ownerAid, uniqueId, default, items);
        }

        public static byte[] BuildVendingPurchaseFromPlayerShop(uint ownerAid, uint uniqueId, RoServerProfile profile, params RoPlayerShopPurchaseRequest[] items) {
            if (ownerAid == 0) {
                throw new ArgumentOutOfRangeException(nameof(ownerAid));
            }

            if (items == null || items.Length == 0) {
                throw new ArgumentException("Vending purchase request must contain at least one item.", nameof(items));
            }

            return CreateProfiledPackets(profile).BuildCzPcPurchaseItemListFromMc2(ownerAid, uniqueId, NormalizePlayerShopPurchaseRequests(items));
        }

        public static byte[] BuildNpcMarketPurchase(params RoNpcMarketPurchaseRequest[] items) {
            return BuildNpcMarketPurchase(default, items);
        }

        public static byte[] BuildNpcMarketPurchase(RoServerProfile profile, params RoNpcMarketPurchaseRequest[] items) {
            if (items == null || items.Length == 0) {
                throw new ArgumentException("NPC market request must contain at least one item.", nameof(items));
            }

            var normalized = new RoNpcMarketPurchaseRequest[items.Length];
            for (int i = 0; i < items.Length; i++) {
                if (items[i].ItemId == 0) {
                    throw new ArgumentOutOfRangeException(nameof(items));
                }

                normalized[i] = new RoNpcMarketPurchaseRequest(items[i].ItemId, RequirePositiveInt(items[i].Amount, nameof(RoNpcMarketPurchaseRequest.Amount)));
            }

            return CreateProfiledPackets(profile).BuildCzNpcMarketPurchase(normalized);
        }

        public static byte[] BuildNpcMarketClose() {
            return BuildNpcMarketClose(default);
        }

        public static byte[] BuildNpcMarketClose(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzNpcMarketClose();
        }

        public static byte[] BuildNpcBarterPurchase(params RoNpcBarterPurchaseRequest[] items) {
            return BuildNpcBarterPurchase(default, items);
        }

        public static byte[] BuildNpcBarterPurchase(RoServerProfile profile, params RoNpcBarterPurchaseRequest[] items) {
            if (items == null || items.Length == 0) {
                throw new ArgumentException("NPC barter request must contain at least one item.", nameof(items));
            }

            var normalized = new RoNpcBarterPurchaseRequest[items.Length];
            for (int i = 0; i < items.Length; i++) {
                if (items[i].ItemId == 0) {
                    throw new ArgumentOutOfRangeException(nameof(items));
                }

                normalized[i] = new RoNpcBarterPurchaseRequest(
                    items[i].ItemId,
                    Math.Max(1u, items[i].Amount),
                    items[i].InventoryIndex,
                    items[i].ShopIndex);
            }

            return CreateProfiledPackets(profile).BuildCzNpcBarterMarketPurchase(normalized);
        }

        public static byte[] BuildNpcBarterClose() {
            return BuildNpcBarterClose(default);
        }

        public static byte[] BuildNpcBarterClose(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzNpcBarterMarketClose();
        }

        public static byte[] BuildNpcExpandedBarterPurchase(params RoNpcExpandedBarterPurchaseRequest[] items) {
            return BuildNpcExpandedBarterPurchase(default, items);
        }

        public static byte[] BuildNpcExpandedBarterPurchase(RoServerProfile profile, params RoNpcExpandedBarterPurchaseRequest[] items) {
            if (items == null || items.Length == 0) {
                throw new ArgumentException("NPC expanded barter request must contain at least one item.", nameof(items));
            }

            var normalized = new RoNpcExpandedBarterPurchaseRequest[items.Length];
            for (int i = 0; i < items.Length; i++) {
                if (items[i].ItemId == 0) {
                    throw new ArgumentOutOfRangeException(nameof(items));
                }

                normalized[i] = new RoNpcExpandedBarterPurchaseRequest(items[i].ItemId, items[i].ShopIndex, Math.Max(1u, items[i].Amount));
            }

            return CreateProfiledPackets(profile).BuildCzNpcExpandedBarterMarketPurchase(normalized);
        }

        public static byte[] BuildNpcExpandedBarterClose() {
            return BuildNpcExpandedBarterClose(default);
        }

        public static byte[] BuildNpcExpandedBarterClose(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzNpcExpandedBarterMarketClose();
        }

        public static byte[] BuildMoveInventoryItemToStorage(int bagIndex, int amount) {
            return BuildMoveInventoryItemToStorage(bagIndex, amount, default);
        }

        public static byte[] BuildMoveInventoryItemToStorage(int bagIndex, int amount, RoServerProfile profile) {
            int validatedBagIndex = RequirePositiveInt(bagIndex, nameof(bagIndex));
            int validatedAmount = RequirePositiveInt(amount, nameof(amount));
            byte[] packet = CreateProfiledPackets(profile)
                .BuildCzMoveItemFromBodyToStore2(
                    validatedBagIndex,
                    validatedAmount);
            RoItemTransferDebugLog.Packet("CZ_MOVE_ITEM_FROM_BODY_TO_STORE2", validatedBagIndex, validatedAmount, packet);
            return packet;
        }

        public static byte[] BuildMoveStorageItemToInventory(int storageIndex, int amount) {
            return BuildMoveStorageItemToInventory(storageIndex, amount, default);
        }

        public static byte[] BuildMoveStorageItemToInventory(int storageIndex, int amount, RoServerProfile profile) {
            int validatedStorageIndex = RequirePositiveInt(storageIndex, nameof(storageIndex));
            int validatedAmount = RequirePositiveInt(amount, nameof(amount));
            byte[] packet = CreateProfiledPackets(profile)
                .BuildCzMoveItemFromStoreToBody2(
                    validatedStorageIndex,
                    validatedAmount);
            RoItemTransferDebugLog.Packet("CZ_MOVE_ITEM_FROM_STORE_TO_BODY2", validatedStorageIndex, validatedAmount, packet);
            return packet;
        }

        public static byte[] BuildMoveStorageItemToCart(int storageIndex, int amount) {
            return BuildMoveStorageItemToCart(storageIndex, amount, default);
        }

        public static byte[] BuildMoveStorageItemToCart(int storageIndex, int amount, RoServerProfile profile) {
            int validatedStorageIndex = RequirePositiveInt(storageIndex, nameof(storageIndex));
            int validatedAmount = RequirePositiveInt(amount, nameof(amount));
            byte[] packet = CreateProfiledPackets(profile)
                .BuildCzMoveItemFromStoreToCart(
                    validatedStorageIndex,
                    validatedAmount);
            RoItemTransferDebugLog.Packet("CZ_MOVE_ITEM_FROM_STORE_TO_CART", validatedStorageIndex, validatedAmount, packet);
            return packet;
        }

        public static byte[] BuildCloseStorage() {
            return BuildCloseStorage(default);
        }

        public static byte[] BuildCloseStorage(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzCloseStore();
        }

        public static byte[] BuildPartyInvite(string characterName, Encoding encoding) {
            return BuildPartyInvite(characterName, encoding, default);
        }

        public static byte[] BuildPartyInvite(string characterName, Encoding encoding, RoServerProfile profile) {
            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            return CreateProfiledPackets(profile).BuildCzPartyJoinReq(characterName.Trim(), encoding);
        }

        public static byte[] BuildPartyLeave() {
            return BuildPartyLeave(default);
        }

        public static byte[] BuildPartyLeave(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzLeaveGroup();
        }

        public static byte[] BuildPartyInviteResponse(uint invitationId, bool accept) {
            return BuildPartyInviteResponse(invitationId, accept, default);
        }

        public static byte[] BuildPartyInviteResponse(uint invitationId, bool accept, RoServerProfile profile) {
            if (invitationId == 0) {
                throw new ArgumentOutOfRangeException(nameof(invitationId));
            }

            return CreateProfiledPackets(profile).BuildCzPartyJoinReqAck(invitationId, accept);
        }

        public static byte[] BuildCreateParty(string partyName, Encoding encoding) {
            return BuildCreateParty(partyName, 0, 0, encoding, default);
        }

        public static byte[] BuildCreateParty(string partyName, byte itemPickupRule, byte itemDivisionRule, Encoding encoding, RoServerProfile profile) {
            if (string.IsNullOrWhiteSpace(partyName)) {
                throw new ArgumentException("Party name is required.", nameof(partyName));
            }

            return CreateProfiledPackets(profile).BuildCzMakeGroup2(partyName.Trim(), itemPickupRule, itemDivisionRule, encoding);
        }

        public static byte[] BuildExpelPartyMember(uint accountId, string characterName, Encoding encoding, RoServerProfile profile) {
            if (accountId == 0 || string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentOutOfRangeException(nameof(accountId), "Party member account id and character name are required.");
            }

            return CreateProfiledPackets(profile).BuildCzReqExpelGroupMember(accountId, characterName.Trim(), encoding);
        }

        public static byte[] BuildChangePartyLeader(uint accountId, RoServerProfile profile) {
            if (accountId == 0) {
                throw new ArgumentOutOfRangeException(nameof(accountId));
            }

            return CreateProfiledPackets(profile).BuildCzChangeGroupMaster(accountId);
        }

        public static byte[] BuildPartyOptions(uint expOption, byte itemPickupRule, byte itemDivisionRule, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzGroupInfoChangeV2(expOption, itemPickupRule, itemDivisionRule);
        }

        public static byte[] BuildAddFriend(string characterName, Encoding encoding) {
            return BuildAddFriend(characterName, encoding, default);
        }

        public static byte[] BuildAddFriend(string characterName, Encoding encoding, RoServerProfile profile) {
            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            return CreateProfiledPackets(profile).BuildCzAddFriends(characterName.Trim(), encoding);
        }

        public static byte[] BuildRemoveFriend(uint accountId, uint characterId) {
            return BuildRemoveFriend(accountId, characterId, default);
        }

        public static byte[] BuildRemoveFriend(uint accountId, uint characterId, RoServerProfile profile) {
            if (accountId == 0 || characterId == 0) {
                throw new ArgumentOutOfRangeException(nameof(accountId), "Friend account and character ids are required.");
            }

            return CreateProfiledPackets(profile).BuildCzDeleteFriends(accountId, characterId);
        }

        public static byte[] BuildChatRoomForm(bool change, int limit, bool isPublic, string password, string title, Encoding encoding, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzChatRoomForm(change, limit, isPublic, password, title, encoding);

        public static byte[] BuildEnterChatRoom(uint chatId, string password, Encoding encoding, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqEnterRoom(chatId, password, encoding);

        public static byte[] BuildChatRoomOwner(string name, Encoding encoding, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqRoleChange(name, encoding);

        public static byte[] BuildChatRoomExpel(string name, Encoding encoding, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqExpelMember(name, encoding);

        public static byte[] BuildExitChatRoom(RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzExitRoom();

        public static byte[] BuildEmotion(byte emotion, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzReqEmotion(emotion);

        /// <summary>
        /// The client's emotion packet (mode msg 0x1f): CZ_REQ_EMOTION_EXPANSION 0x0be9 (pack, emotion) when the profile has it
        /// (20240619), else CZ_REQ_EMOTION 0x00bf with the emotion byte — a server of the 20200401 profile does not know 0x0be9.
        /// </summary>
        public static byte[] BuildEmotion(ushort pack, ushort emotion, RoServerProfile profile) {
            RoProfiledOutboundPackets packets = CreateProfiledPackets(profile);
            return packets.TryResolveDefinition(RoOutboundPacketKind.CzReqEmotionExpansion, out _)
                ? packets.BuildCzReqEmotionExpansion(pack, emotion)
                : packets.BuildCzReqEmotion((byte) emotion);
        }

        public static byte[] BuildHomunculusCommand(byte command, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzCommandMer(command);

        public static byte[] BuildHomunculusRename(string name, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzRenameMer(name);

        public static byte[] BuildMercenaryCommand(byte command, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzMerCommand(command);

        public static byte[] BuildGuildNotice(uint guildId, string subject, string notice, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzGuildNotice(guildId, subject, notice);

        public static byte[] BuildGuildCreate(uint charId, string name, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqMakeGuild(charId, name);

        public static byte[] BuildGuildDisband(string key, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqDisorganizeGuild(key);

        public static byte[] BuildGuildInviteAnswer(uint guildId, bool accept, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzJoinGuild(guildId, accept);

        public static byte[] BuildGuildAllyAnswer(uint inviterAccountId, bool accept, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzAllyGuild(inviterAccountId, accept);

        public static byte[] BuildGuildInvite(uint targetAccountId, uint accountId, uint charId, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqJoinGuild(targetAccountId, accountId, charId);

        public static byte[] BuildGuildLeave(uint guildId, uint accountId, uint charId, string reason, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqLeaveGuild(guildId, accountId, charId, reason);

        public static byte[] BuildGuildExpel(uint guildId, uint accountId, uint charId, string reason, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqBanGuild(guildId, accountId, charId, reason);

        public static byte[] BuildGuildMenuInterface(RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzReqGuildMenuInterface();

        public static byte[] BuildGuildMenu(int type, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzReqGuildMenu(type);

        public static byte[] BuildGuildMemberPositions(System.Collections.Generic.IReadOnlyList<(uint AccountId, uint CharId, int PositionId)> members, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqChangeMemberPos(members);

        public static byte[] BuildGuildPositions(System.Collections.Generic.IReadOnlyList<RoGuildPosition> positions, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzRegChangeGuildPositionInfo(positions);

        public static byte[] BuildCompanionConfig(int config, int value, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzConfig(config, value);

        public static byte[] BuildPetCommand(byte subCommand, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzCommandPet(subCommand);

        public static byte[] BuildRenamePet(string name, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzRenamePet(name);

        public static byte[] BuildSelectPetEgg(short bagIndex, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzSelectPetEgg(bagIndex);

        public static byte[] BuildTryCaptureMonster(uint actorId, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzTryCaptureMonster(actorId);

        public static byte[] BuildPetEvolution(uint targetEggId, System.Collections.Generic.IReadOnlyList<(int Index, int Amount)> materials, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzPetEvolution(targetEggId, materials);

        public static byte[] BuildMakingItem(uint itemId, uint[] materials, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqMakingItem(itemId, materials);

        public static byte[] BuildMergeItem(System.Collections.Generic.IReadOnlyList<short> bagIndexes, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzReqMergeItem(bagIndexes);

        public static byte[] BuildCancelMergeItem(RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzCancelMergeItem();

        public static byte[] BuildWeaponRefine(int index, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzReqWeaponRefine(index);

        public static byte[] BuildSelectAutoSpell(int skillId, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzSelectAutoSpell(skillId);

        public static byte[] BuildItemRepair(short index, uint itemId, uint[] cards, byte refine, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqItemRepair2(index, itemId, cards, refine);

        public static byte[] BuildOpenBuyingStore(uint zenyLimit, bool open, string title, System.Collections.Generic.IReadOnlyList<RoBuyingStoreItem> items, Encoding encoding, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqOpenBuyingStore(zenyLimit, open, title, items, encoding);

        public static byte[] BuildCloseBuyingStore(RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzReqCloseBuyingStore();

        public static byte[] BuildClickBuyingStore(uint buyerAid, RoServerProfile profile) => CreateProfiledPackets(profile).BuildCzReqClickToBuyingStore(buyerAid);

        public static byte[] BuildTradeBuyingStore(uint buyerAid, uint storeId, System.Collections.Generic.IReadOnlyList<(ushort BagIndex, uint ItemId, int Amount)> items, RoServerProfile profile) =>
            CreateProfiledPackets(profile).BuildCzReqTradeBuyingStore(buyerAid, storeId, items);

        public static byte[] BuildRequestNameByGid(uint characterId, RoServerProfile profile) {
            if (characterId == 0) {
                throw new ArgumentOutOfRangeException(nameof(characterId), "A character id is required.");
            }

            return CreateProfiledPackets(profile).BuildCzReqNameByGid(characterId);
        }

        public static byte[] BuildRespondFriendRequest(uint accountId, uint characterId, bool accept) {
            return BuildRespondFriendRequest(accountId, characterId, accept, default);
        }

        public static byte[] BuildRespondFriendRequest(uint accountId, uint characterId, bool accept, RoServerProfile profile) {
            if (accountId == 0 || characterId == 0) {
                throw new ArgumentOutOfRangeException(nameof(accountId), "Friend account and character ids are required.");
            }

            return CreateProfiledPackets(profile).BuildCzAckReqAddFriends(accountId, characterId, accept);
        }

        public static byte[] BuildGuildChat(string message, Encoding encoding) {
            return BuildGuildChat(string.Empty, message, encoding, default);
        }

        public static byte[] BuildGuildChat(string message, Encoding encoding, RoServerProfile profile) {
            return BuildGuildChat(string.Empty, message, encoding, profile);
        }

        public static byte[] BuildGuildChat(string characterName, string message, Encoding encoding) {
            return BuildGuildChat(characterName, message, encoding, default);
        }

        public static byte[] BuildGuildChat(string characterName, string message, Encoding encoding, RoServerProfile profile) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Guild chat message is required.", nameof(message));
            }

            if (string.IsNullOrWhiteSpace(characterName)) {
                return CreateProfiledPackets(profile).BuildCzGuildChat(message.Trim(), encoding);
            }

            return CreateProfiledPackets(profile).BuildCzGuildChat(characterName.Trim(), message.Trim(), encoding);
        }

        public static byte[] BuildPartyChat(string characterName, string message, Encoding encoding, RoServerProfile profile) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Party chat message is required.", nameof(message));
            }

            return CreateProfiledPackets(profile).BuildCzPartyChat(characterName, message.Trim(), encoding);
        }

        public static byte[] BuildClanChat(string characterName, string message, Encoding encoding, RoServerProfile profile) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Clan chat message is required.", nameof(message));
            }

            return CreateProfiledPackets(profile).BuildCzClanChat(characterName, message.Trim(), encoding);
        }

        public static byte[] BuildTradeRequest(uint targetActorId) {
            return BuildTradeRequest(targetActorId, default);
        }

        public static byte[] BuildTradeRequest(uint targetActorId, RoServerProfile profile) {
            if (targetActorId == 0) {
                throw new ArgumentOutOfRangeException(nameof(targetActorId));
            }

            return CreateProfiledPackets(profile).BuildCzRequestExchangeItem(targetActorId);
        }

        public static byte[] BuildCheckPlayerEquipment(uint targetActorId, RoServerProfile profile) {
            if (targetActorId == 0) {
                throw new ArgumentOutOfRangeException(nameof(targetActorId));
            }

            return CreateProfiledPackets(profile).BuildCzEquipWinMicroscope(targetActorId);
        }

        public static byte[] BuildTradeAnswer(bool accept) {
            return BuildTradeAnswer(accept, default);
        }

        public static byte[] BuildTradeAnswer(bool accept, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzAckExchangeItem(accept);
        }

        public static byte[] BuildTradeAddItem(int bagIndex, int amount) {
            return BuildTradeAddItem(bagIndex, amount, default);
        }

        public static byte[] BuildTradeAddItem(int bagIndex, int amount, RoServerProfile profile) {
            return CreateProfiledPackets(profile)
                .BuildCzAddExchangeItem(
                    RequireNonNegativeInt(bagIndex, nameof(bagIndex)),
                    RequirePositiveInt(amount, nameof(amount)));
        }

        public static byte[] BuildTradeLock() {
            return BuildTradeLock(default);
        }

        public static byte[] BuildTradeLock(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzConcludeExchangeItem();
        }

        public static byte[] BuildTradeCancel() {
            return BuildTradeCancel(default);
        }

        public static byte[] BuildTradeCancel(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzCancelExchangeItem();
        }

        public static byte[] BuildTradeConfirm() {
            return BuildTradeConfirm(default);
        }

        public static byte[] BuildTradeConfirm(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzExecExchangeItem();
        }

        public static byte[] BuildMoveInventoryItemToCart(int bagIndex, int amount) {
            return BuildMoveInventoryItemToCart(bagIndex, amount, default);
        }

        public static byte[] BuildMoveInventoryItemToCart(int bagIndex, int amount, RoServerProfile profile) {
            int validatedBagIndex = RequirePositiveInt(bagIndex, nameof(bagIndex));
            int validatedAmount = RequirePositiveInt(amount, nameof(amount));
            byte[] packet = CreateProfiledPackets(profile)
                .BuildCzMoveItemFromBodyToCart(
                    validatedBagIndex,
                    validatedAmount);
            RoItemTransferDebugLog.Packet("CZ_MOVE_ITEM_FROM_BODY_TO_CART", validatedBagIndex, validatedAmount, packet);
            return packet;
        }

        public static byte[] BuildMoveCartItemToInventory(int cartIndex, int amount) {
            return BuildMoveCartItemToInventory(cartIndex, amount, default);
        }

        public static byte[] BuildMoveCartItemToInventory(int cartIndex, int amount, RoServerProfile profile) {
            int validatedCartIndex = RequirePositiveInt(cartIndex, nameof(cartIndex));
            int validatedAmount = RequirePositiveInt(amount, nameof(amount));
            byte[] packet = CreateProfiledPackets(profile)
                .BuildCzMoveItemFromCartToBody(
                    validatedCartIndex,
                    validatedAmount);
            RoItemTransferDebugLog.Packet("CZ_MOVE_ITEM_FROM_CART_TO_BODY", validatedCartIndex, validatedAmount, packet);
            return packet;
        }

        public static byte[] BuildMoveCartItemToStorage(int cartIndex, int amount) {
            return BuildMoveCartItemToStorage(cartIndex, amount, default);
        }

        public static byte[] BuildMoveCartItemToStorage(int cartIndex, int amount, RoServerProfile profile) {
            int validatedCartIndex = RequirePositiveInt(cartIndex, nameof(cartIndex));
            int validatedAmount = RequirePositiveInt(amount, nameof(amount));
            byte[] packet = CreateProfiledPackets(profile)
                .BuildCzMoveItemFromCartToStore(
                    validatedCartIndex,
                    validatedAmount);
            RoItemTransferDebugLog.Packet("CZ_MOVE_ITEM_FROM_CART_TO_STORE", validatedCartIndex, validatedAmount, packet);
            return packet;
        }

        public static byte[] BuildUseCartItem(RoGameplaySnapshot snapshot, int cartIndex, int localAid, RoServerProfile profile, out RoItemInstance item) {
            item = null;
            if (cartIndex <= 0) {
                throw new ArgumentOutOfRangeException(nameof(cartIndex));
            }

            snapshot ??= RoGameplaySnapshot.Empty;
            RoCartInventorySnapshot cart = snapshot.Cart ?? RoCartInventorySnapshot.Empty;
            RoItemInstance[] items = cart.Items ?? Array.Empty<RoItemInstance>();
            for (int i = 0; i < items.Length; i++) {
                RoItemInstance candidate = items[i];
                if (candidate == null || candidate.BagIndex != cartIndex) {
                    continue;
                }

                item = candidate;
                break;
            }

            if (item == null) {
                throw new InvalidOperationException($"Cart index {cartIndex} has no usable item.");
            }

            RoItemType itemType = item.ItemTypeRaw != 0 ? unchecked((RoItemType) item.ItemTypeRaw) : RoItemType.Unknown;
            if (itemType != RoItemType.Usable && itemType != RoItemType.Healing && itemType != RoItemType.DelayConsume) {
                throw new InvalidOperationException($"Cart index {cartIndex} is not a usable item.");
            }

            return CreateProfiledPackets(profile).BuildCzUseItem2(RequirePositiveInt(cartIndex, nameof(cartIndex)), localAid);
        }

        public static byte[] BuildCartOff() {
            return BuildCartOff(default);
        }

        public static byte[] BuildCartOff(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzCartOff();
        }

        public static byte[] BuildOpenBank(int aid, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqBankOpen(RequirePositiveAid(aid, nameof(aid)));
        }

        public static byte[] BuildCloseBank(int aid, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqBankClose(RequirePositiveAid(aid, nameof(aid)));
        }

        public static byte[] BuildCheckBank(int aid, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqBankingCheck(RequirePositiveAid(aid, nameof(aid)));
        }

        public static byte[] BuildDepositBankZeny(int aid, int amount, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqBankingDeposit(
                RequirePositiveAid(aid, nameof(aid)),
                RequirePositiveInt(amount, nameof(amount)));
        }

        public static byte[] BuildWithdrawBankZeny(int aid, int amount, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqBankingWithdraw(
                RequirePositiveAid(aid, nameof(aid)),
                RequirePositiveInt(amount, nameof(amount)));
        }

        public static byte[] BuildRodexOpen(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzOpenAllRodex();
        }

        public static byte[] BuildRodexRefresh(ulong mailId, ulong returnMailId, ulong accountMailId, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzUpdateAllRodex(mailId, returnMailId, accountMailId);
        }

        public static byte[] BuildRodexClose(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzCloseRodexBox();
        }

        public static byte[] BuildRodexRead(byte openType, ulong mailId, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqReadRodex(openType, mailId);
        }

        public static byte[] BuildRodexNext(byte openType, ulong mailId, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqNextRodex(openType, mailId);
        }

        public static byte[] BuildRodexDelete(byte openType, ulong mailId, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqDeleteRodex(openType, mailId);
        }

        public static byte[] BuildRodexGetZeny(byte openType, ulong mailId, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqZenyFromRodex(openType, mailId);
        }

        public static byte[] BuildRodexGetItems(byte openType, ulong mailId, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqItemFromRodex(openType, mailId);
        }

        public static byte[] BuildRodexOpenWrite(string receiverName, int codePage, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqOpenWriteRodex(receiverName, Encoding.GetEncoding(codePage));
        }

        public static byte[] BuildRodexCancelWrite(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqCancelWriteRodex();
        }

        public static byte[] BuildRodexCheckReceiver(string receiverName, int codePage, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzCheckRodexReceiverForProfile(receiverName, ownCharacterOnly: 1, Encoding.GetEncoding(codePage));
        }

        public static byte[] BuildRodexAddItem(int bagIndex, int amount, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqAddItemRodex(
                RequirePositiveInt(bagIndex, nameof(bagIndex)),
                RequirePositiveInt(amount, nameof(amount)));
        }

        public static byte[] BuildRodexRemoveItem(int bagIndex, int amount, RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqRemoveRodexItem(
                RequirePositiveInt(bagIndex, nameof(bagIndex)),
                RequirePositiveInt(amount, nameof(amount)));
        }

        public static byte[] BuildRodexSendMail(
            string receiverName,
            string senderName,
            long zeny,
            string title,
            string body,
            uint receiverCharacterId,
            int codePage,
            RoServerProfile profile) {
            if (zeny < 0) {
                throw new ArgumentOutOfRangeException(nameof(zeny));
            }

            return CreateProfiledPackets(profile).BuildCzReqSendRodex2(
                receiverName,
                senderName,
                zeny,
                title,
                body,
                receiverCharacterId,
                Encoding.GetEncoding(codePage));
        }

        public static byte[] BuildAttendanceOpen(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzOpenUi(RoAttendanceRuntime.ClientAttendanceUiType);
        }

        public static byte[] BuildAttendanceClaim(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzReqAttendance();
        }

        public static byte[] BuildPetFeed() {
            return BuildPetFeed(default);
        }

        public static byte[] BuildPetFeed(RoServerProfile profile) {
            return CreateProfiledPackets(profile).BuildCzCommandPet(1);
        }

        private static RoItemInstance FindInventoryItem(RoGameplaySnapshot snapshot, int bagIndex) {
            if (bagIndex <= 0) {
                throw new ArgumentOutOfRangeException(nameof(bagIndex));
            }

            snapshot ??= RoGameplaySnapshot.Empty;
            RoItemInstance[] items = snapshot.InventoryItems ?? Array.Empty<RoItemInstance>();
            for (int i = 0; i < items.Length; i++) {
                RoItemInstance item = items[i];
                if (item != null && item.BagIndex == bagIndex) {
                    return item;
                }
            }

            return null;
        }

        private static bool IsCardItem(RoItemInstance item) {
            return item != null && item.ItemTypeRaw == (byte) RoItemType.Card;
        }

        private static bool IsEquipmentItemType(byte itemTypeRaw) {
            RoItemType type = itemTypeRaw != 0 ? unchecked((RoItemType) itemTypeRaw) : RoItemType.Unknown;
            return type == RoItemType.Weapon ||
                type == RoItemType.Armor ||
                type == RoItemType.ShadowGear ||
                type == RoItemType.PetEgg ||
                type == RoItemType.PetArmor;
        }

        private static RoNpcShopPurchaseRequest[] NormalizeNpcShopPurchaseRequests(RoNpcShopPurchaseRequest[] items) {
            var normalized = new RoNpcShopPurchaseRequest[items.Length];
            for (int i = 0; i < items.Length; i++) {
                if (items[i].ItemId == 0) {
                    throw new ArgumentOutOfRangeException(nameof(items));
                }

                normalized[i] = new RoNpcShopPurchaseRequest(
                    RequirePositiveUInt16(items[i].Amount, nameof(RoNpcShopPurchaseRequest.Amount)),
                    items[i].ItemId);
            }

            return normalized;
        }

        private static RoNpcShopSellRequest[] NormalizeNpcShopSellRequests(RoNpcShopSellRequest[] items) {
            var normalized = new RoNpcShopSellRequest[items.Length];
            for (int i = 0; i < items.Length; i++) {
                normalized[i] = new RoNpcShopSellRequest(
                    RequirePositiveUInt16(items[i].BagIndex, nameof(RoNpcShopSellRequest.BagIndex)),
                    RequirePositiveUInt16(items[i].Amount, nameof(RoNpcShopSellRequest.Amount)));
            }

            return normalized;
        }

        private static RoVendingOpenStoreItem[] NormalizeVendingOpenStoreItems(RoVendingOpenStoreItem[] items) {
            var normalized = new RoVendingOpenStoreItem[items.Length];
            for (int i = 0; i < items.Length; i++) {
                normalized[i] = new RoVendingOpenStoreItem(
                    RequirePositiveUInt16(items[i].CartIndex, nameof(RoVendingOpenStoreItem.CartIndex)),
                    RequirePositiveUInt16(items[i].Amount, nameof(RoVendingOpenStoreItem.Amount)),
                    items[i].Price == 0 ? throw new ArgumentOutOfRangeException(nameof(RoVendingOpenStoreItem.Price)) : items[i].Price);
            }

            return normalized;
        }

        private static RoPlayerShopPurchaseRequest[] NormalizePlayerShopPurchaseRequests(RoPlayerShopPurchaseRequest[] items) {
            var normalized = new RoPlayerShopPurchaseRequest[items.Length];
            for (int i = 0; i < items.Length; i++) {
                normalized[i] = new RoPlayerShopPurchaseRequest(
                    RequirePositiveUInt16(items[i].Amount, nameof(RoPlayerShopPurchaseRequest.Amount)),
                    RequirePositiveUInt16(items[i].Index, nameof(RoPlayerShopPurchaseRequest.Index)));
            }

            return normalized;
        }

        private static RoProfiledOutboundPackets CreateProfiledPackets(RoServerProfile profile) {
            RoServerProfile normalized = profile.PacketVersion == 0
                ? RoServerProfile.LocalRathena20200401()
                : profile;
            return new RoProfiledOutboundPackets(RoPacketProfiles.ForServerProfile(normalized));
        }

        private static ushort RequirePositiveUInt16(int value, string paramName) {
            if (value <= 0 || value > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(paramName);
            }

            return (ushort) value;
        }

        private static int RequirePositiveInt(int value, string paramName) {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be positive.");
            }

            return value;
        }

        private static int RequireNonNegativeInt(int value, string paramName) {
            if (value < 0) {
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be non-negative.");
            }

            return value;
        }

        private static uint RequirePositiveAid(int value, string paramName) {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(paramName, value, "A positive account id is required.");
            }

            return unchecked((uint) value);
        }

        private static int RequireLegacyAid(uint value, string paramName) {
            if (value == 0 || value > int.MaxValue) {
                throw new ArgumentOutOfRangeException(paramName, value, "Legacy packet path requires a positive 31-bit actor id.");
            }

            return (int) value;
        }
    }
}
