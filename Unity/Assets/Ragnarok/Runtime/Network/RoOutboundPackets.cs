using System;
using System.Text;

namespace Ragnarok.Network {
    public static class RoOutboundPackets {
        public static byte[] BuildCaLogin(string account, string password, RoServerProfile profile) {
            var writer = new RoPacketWriter(55);
            writer.WriteUInt16(RoPacketIds.CaLogin);
            writer.WriteInt32(profile.ClientVersion);
            writer.WriteFixedString(account ?? string.Empty, 24);
            writer.WriteFixedString(password ?? string.Empty, 24);
            writer.WriteByte((byte) profile.LanguageType);
            return writer.ToArray();
        }

        public static byte[] BuildChEnter(int accountId, int loginId1, int loginId2, byte sex) {
            var writer = new RoPacketWriter(17);
            writer.WriteUInt16(RoPacketIds.ChEnter);
            writer.WriteInt32(accountId);
            writer.WriteInt32(loginId1);
            writer.WriteInt32(loginId2);
            writer.WriteInt16(0);
            writer.WriteByte(sex);
            return writer.ToArray();
        }

        public static byte[] BuildChSelectChar(int characterSlot) {
            if (characterSlot < 0 || characterSlot > byte.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(characterSlot));
            }

            var writer = new RoPacketWriter(3);
            writer.WriteUInt16(RoPacketIds.ChSelectChar);
            writer.WriteByte((byte) characterSlot);
            return writer.ToArray();
        }

        public static byte[] BuildChSelectAccessibleMapName(int characterSlot, int mapIndex) {
            if (characterSlot < 0 || characterSlot > byte.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(characterSlot));
            }

            if (mapIndex < 0 || mapIndex > byte.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(mapIndex));
            }

            var writer = new RoPacketWriter(4);
            writer.WriteUInt16(RoPacketIds.ChSelectAccessibleMapName);
            writer.WriteByte((byte) characterSlot);
            writer.WriteByte((byte) mapIndex);
            return writer.ToArray();
        }

        public static byte[] BuildChAvailableSecondPassword(int accountId) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.ChAvailableSecondPassword);
            writer.WriteInt32(accountId);
            return writer.ToArray();
        }

        public static byte[] BuildChCharListRequest() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.ChCharListRequest);
            return writer.ToArray();
        }

        public static byte[] BuildChMakeChar(
            string name,
            int characterSlot,
            ushort hairColor,
            ushort hairStyle,
            int startJob,
            byte sex,
            Encoding encoding = null) {
            if (characterSlot < 0 || characterSlot > byte.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(characterSlot));
            }

            var writer = new RoPacketWriter(36);
            writer.WriteUInt16(RoPacketIds.ChMakeChar);
            writer.WriteFixedString(name ?? string.Empty, 24, encoding);
            writer.WriteByte((byte) characterSlot);
            writer.WriteUInt16(hairColor);
            writer.WriteUInt16(hairStyle);
            writer.WriteInt32(startJob);
            writer.WriteByte(sex);
            return writer.ToArray();
        }

        public static byte[] BuildCzEnter2(int accountId, int characterId, int loginId1, int clientTime, byte sex) {
            var writer = new RoPacketWriter(19);
            writer.WriteUInt16(RoPacketIds.CzEnter2);
            writer.WriteInt32(accountId);
            writer.WriteInt32(characterId);
            writer.WriteInt32(loginId1);
            writer.WriteInt32(clientTime);
            writer.WriteByte(sex);
            return writer.ToArray();
        }

        public static byte[] BuildCzNotifyActorInit() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzNotifyActorInit);
            return writer.ToArray();
        }

        public static byte[] BuildCzRequestMove2(int x, int y, int direction = 0) {
            var writer = new RoPacketWriter(5);
            writer.WriteUInt16(RoPacketIds.CzRequestMove2);
            writer.WritePosition(new RoPacketPosition(x, y, direction));
            return writer.ToArray();
        }

        public static byte[] BuildCzRequestTime2(int clientTime) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzRequestTime2);
            writer.WriteInt32(clientTime);
            return writer.ToArray();
        }

        public static byte[] BuildCzPingLive() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzPingLive);
            return writer.ToArray();
        }

        public static byte[] BuildCzActionRequest2(int targetAid, byte actionType) {
            var writer = new RoPacketWriter(7);
            writer.WriteUInt16(RoPacketIds.CzActionRequest2);
            writer.WriteUInt32(RequireUInt32(nameof(targetAid), targetAid));
            writer.WriteByte(actionType);
            return writer.ToArray();
        }

        public static byte[] BuildCzRequestName2(uint actorId) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzRequestName2);
            writer.WriteUInt32(actorId);
            return writer.ToArray();
        }

        public static byte[] BuildCzRequestName2(int actorId) {
            return BuildCzRequestName2(RequireUInt32(nameof(actorId), actorId));
        }

        public static byte[] BuildCzRestart(byte type) {
            var writer = new RoPacketWriter(3);
            writer.WriteUInt16(RoPacketIds.CzRestart);
            writer.WriteByte(type);
            return writer.ToArray();
        }

        public static byte[] BuildCzStandingResurrection() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzStandingResurrection);
            return writer.ToArray();
        }

        public static byte[] BuildCzContactNpc(int npcId, byte type) {
            var writer = new RoPacketWriter(7);
            writer.WriteUInt16(RoPacketIds.CzContactNpc);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteByte(type);
            return writer.ToArray();
        }

        public static byte[] BuildCzNpcSelectMenu(int npcId, byte choice) {
            var writer = new RoPacketWriter(7);
            writer.WriteUInt16(RoPacketIds.CzNpcSelectMenu);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteByte(choice);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqNextScript(int npcId) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzReqNextScript);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            return writer.ToArray();
        }

        public static byte[] BuildCzInputEditDlg(int npcId, int value) {
            var writer = new RoPacketWriter(10);
            writer.WriteUInt16(RoPacketIds.CzInputEditDlg);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteInt32(value);
            return writer.ToArray();
        }

        public static byte[] BuildCzInputEditDlgStr(int npcId, string value, Encoding encoding = null) {
            byte[] payload = (encoding ?? Encoding.ASCII).GetBytes(value ?? string.Empty);
            int packetLength = 2 + 2 + 4 + payload.Length + 1;
            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzInputEditDlgStr);
            writer.WriteUInt16((ushort) packetLength);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteBytes(payload);
            writer.WriteByte(0);
            return writer.ToArray();
        }

        public static byte[] BuildCzCloseDialog(int npcId) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzCloseDialog);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            return writer.ToArray();
        }

        public static byte[] BuildCzRequestChat(string characterName, string message, Encoding encoding = null) {
            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            string payload = $"{characterName.Trim()} : {message.Trim()}";
            byte[] payloadBytes = (encoding ?? Encoding.ASCII).GetBytes(payload);
            int packetLength = 4 + payloadBytes.Length;
            if (packetLength > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(message), "Chat packet is too long.");
            }

            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzRequestChat);
            writer.WriteUInt16((ushort) packetLength);
            writer.WriteBytes(payloadBytes);
            return writer.ToArray();
        }

        public static byte[] BuildCzWhisper(string receiverName, string message, Encoding encoding = null) {
            if (string.IsNullOrWhiteSpace(receiverName)) {
                throw new ArgumentException("Whisper receiver is required.", nameof(receiverName));
            }

            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            Encoding resolvedEncoding = encoding ?? Encoding.ASCII;
            byte[] messageBytes = resolvedEncoding.GetBytes(message.Trim());
            int packetLength = 4 + 24 + messageBytes.Length + 1;
            if (packetLength > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(message), "Whisper packet is too long.");
            }

            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzWhisper);
            writer.WriteUInt16((ushort) packetLength);
            writer.WriteFixedString(receiverName.Trim(), 24, resolvedEncoding);
            writer.WriteBytes(messageBytes);
            writer.WriteByte(0);
            return writer.ToArray();
        }

        public static byte[] BuildCzGuildChat(string message, Encoding encoding = null) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            byte[] payloadBytes = (encoding ?? Encoding.ASCII).GetBytes(message.Trim());
            int packetLength = 4 + payloadBytes.Length + 1;
            if (packetLength > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(message), "Guild chat packet is too long.");
            }

            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzGuildChat);
            writer.WriteUInt16((ushort) packetLength);
            writer.WriteBytes(payloadBytes);
            writer.WriteByte(0);
            return writer.ToArray();
        }

        public static byte[] BuildCzPartyChat(string characterName, string message, Encoding encoding = null) {
            return BuildCzNamePrefixedVariableChat(RoPacketIds.CzPartyChat, characterName, message, "Party chat packet is too long.", encoding);
        }

        public static byte[] BuildCzGuildChat(string characterName, string message, Encoding encoding = null) {
            return BuildCzNamePrefixedVariableChat(RoPacketIds.CzGuildChat, characterName, message, "Guild chat packet is too long.", encoding);
        }

        public static byte[] BuildCzClanChat(string characterName, string message, Encoding encoding = null) {
            return BuildCzNamePrefixedVariableChat(RoPacketIds.CzClanChat, characterName, message, "Clan chat packet is too long.", encoding);
        }

        private static byte[] BuildCzNamePrefixedVariableChat(
            ushort packetId,
            string characterName,
            string message,
            string tooLongMessage,
            Encoding encoding = null) {
            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            string payload = $"{characterName.Trim()} : {message.Trim()}";
            byte[] payloadBytes = (encoding ?? Encoding.ASCII).GetBytes(payload);
            int packetLength = 4 + payloadBytes.Length + 1;
            if (packetLength > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(message), tooLongMessage);
            }

            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(packetId);
            writer.WriteUInt16((ushort) packetLength);
            writer.WriteBytes(payloadBytes);
            writer.WriteByte(0);
            return writer.ToArray();
        }

        public static byte[] BuildCzCommandPet(byte subCommand) {
            var writer = new RoPacketWriter(3);
            writer.WriteUInt16(RoPacketIds.CzCommandPet);
            writer.WriteByte(subCommand);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqItemCompositionList(int cardIndex) {
            var writer = new RoPacketWriter(4);
            writer.WriteUInt16(RoPacketIds.CzReqItemCompositionList);
            writer.WriteUInt16(RequireUInt16(nameof(cardIndex), cardIndex));
            return writer.ToArray();
        }

        public static byte[] BuildCzReqItemComposition(int cardIndex, int equipIndex) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzReqItemComposition);
            writer.WriteUInt16(RequireUInt16(nameof(cardIndex), cardIndex));
            writer.WriteUInt16(RequireUInt16(nameof(equipIndex), equipIndex));
            return writer.ToArray();
        }

        public static byte[] BuildCzReqItemIdentify(int bagIndex) {
            var writer = new RoPacketWriter(4);
            writer.WriteUInt16(RoPacketIds.CzReqItemIdentify);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            return writer.ToArray();
        }

        public static byte[] BuildCzCancelItemIdentify() {
            var writer = new RoPacketWriter(4);
            writer.WriteUInt16(RoPacketIds.CzReqItemIdentify);
            writer.WriteUInt16(ushort.MaxValue);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqMakingArrow(uint itemId) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzReqMakingArrow);
            writer.WriteUInt32(itemId);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqChangeCart(int cartNumber) {
            var writer = new RoPacketWriter(4);
            writer.WriteUInt16(RoPacketIds.CzReqChangeCart);
            writer.WriteUInt16(RequireUInt16(nameof(cartNumber), cartNumber));
            return writer.ToArray();
        }

        public static byte[] BuildCzSelectCart(uint identity, int cartType) {
            var writer = new RoPacketWriter(7);
            writer.WriteUInt16(RoPacketIds.CzSelectCart);
            writer.WriteUInt32(identity);
            writer.WriteByte((byte) RequireByteValue(nameof(cartType), cartType));
            return writer.ToArray();
        }

        public static byte[] BuildCzSelectWarpPoint(ushort skillId, string mapName, Encoding encoding = null) {
            var writer = new RoPacketWriter(20);
            writer.WriteUInt16(RoPacketIds.CzSelectWarpPoint);
            writer.WriteUInt16(skillId);
            writer.WriteFixedString(mapName ?? string.Empty, 16, encoding ?? Encoding.ASCII);
            return writer.ToArray();
        }

        public static byte[] BuildCzUseItem2(int bagIndex, int aid) {
            return BuildCzUseItem2(bagIndex, RequireUInt32(nameof(aid), aid));
        }

        public static byte[] BuildCzUseItem2(int bagIndex, uint aid) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzUseItem2);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteUInt32(aid);
            return writer.ToArray();
        }

        public static byte[] BuildCzItemThrow2(int bagIndex, int count) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzItemThrow2);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteUInt16(RequireUInt16(nameof(count), count));
            return writer.ToArray();
        }

        public static byte[] BuildCzItemPickup2(int itemAid) {
            return BuildCzItemPickup2(RequireUInt32(nameof(itemAid), itemAid));
        }

        public static byte[] BuildCzItemPickup2(uint itemAid) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzItemPickup2);
            writer.WriteUInt32(itemAid);
            return writer.ToArray();
        }

        public static byte[] BuildCzAckSelectDealType(int npcId, byte type) {
            var writer = new RoPacketWriter(7);
            writer.WriteUInt16(RoPacketIds.CzAckSelectDealType);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteByte(type);
            return writer.ToArray();
        }

        public static byte[] BuildCzPcPurchaseItemList(params RoNpcShopPurchaseRequest[] items) {
            items ??= Array.Empty<RoNpcShopPurchaseRequest>();
            int packetLength = 4 + (items.Length * 6);
            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzPcPurchaseItemList);
            writer.WriteUInt16((ushort) packetLength);
            for (int i = 0; i < items.Length; i++) {
                writer.WriteUInt16(RequireUInt16(nameof(RoNpcShopPurchaseRequest.Amount), items[i].Amount));
                writer.WriteUInt32(items[i].ItemId);
            }

            return writer.ToArray();
        }

        public static byte[] BuildCzPcSellItemList(params RoNpcShopSellRequest[] items) {
            items ??= Array.Empty<RoNpcShopSellRequest>();
            int packetLength = 4 + (items.Length * 4);
            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzPcSellItemList);
            writer.WriteUInt16((ushort) packetLength);
            for (int i = 0; i < items.Length; i++) {
                writer.WriteUInt16(RequireUInt16(nameof(RoNpcShopSellRequest.BagIndex), items[i].BagIndex));
                writer.WriteUInt16(RequireUInt16(nameof(RoNpcShopSellRequest.Amount), items[i].Amount));
            }

            return writer.ToArray();
        }

        public static byte[] BuildCzNpcTradeQuit() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzNpcTradeQuit);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqOpenStore2(string shopName, byte result, params RoVendingOpenStoreItem[] items) {
            items ??= Array.Empty<RoVendingOpenStoreItem>();
            if (result != 0 && result != 1) {
                throw new ArgumentOutOfRangeException(nameof(result), "Open store result must be 0 (cancel) or 1 (open).");
            }

            if (result == 1 && items.Length == 0) {
                throw new ArgumentException("Opening a vending store requires at least one store item.", nameof(items));
            }

            if (result == 0) {
                shopName = string.Empty;
                items = Array.Empty<RoVendingOpenStoreItem>();
            }

            int packetLength = 85 + (items.Length * 8);
            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzReqOpenStore2);
            writer.WriteUInt16((ushort) packetLength);
            WriteFixedVendingShopName(writer, shopName, Encoding.ASCII);
            writer.WriteByte(result);
            for (int i = 0; i < items.Length; i++) {
                if (items[i].Amount == 0) {
                    throw new ArgumentOutOfRangeException(nameof(items), "Vending item amount must be greater than zero.");
                }

                if (items[i].Price == 0) {
                    throw new ArgumentOutOfRangeException(nameof(items), "Vending item price must be greater than zero.");
                }

                writer.WriteUInt16(items[i].CartIndex);
                writer.WriteUInt16(items[i].Amount);
                writer.WriteUInt32(items[i].Price);
            }

            return writer.ToArray();
        }

        public static byte[] BuildCzReqCloseStore() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzReqCloseStore);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqBuyFromMc(uint ownerAid) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzReqBuyFromMc);
            writer.WriteUInt32(ownerAid);
            return writer.ToArray();
        }

        public static byte[] BuildCzPcPurchaseItemListFromMc(uint ownerAid, params RoPlayerShopPurchaseRequest[] items) {
            items ??= Array.Empty<RoPlayerShopPurchaseRequest>();
            int packetLength = 8 + (items.Length * 4);
            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzPcPurchaseItemListFromMc);
            writer.WriteUInt16((ushort) packetLength);
            writer.WriteUInt32(ownerAid);
            WritePlayerShopPurchaseItems(writer, items);
            return writer.ToArray();
        }

        public static byte[] BuildCzPcPurchaseItemListFromMc2(uint ownerAid, uint uniqueId, params RoPlayerShopPurchaseRequest[] items) {
            items ??= Array.Empty<RoPlayerShopPurchaseRequest>();
            int packetLength = 12 + (items.Length * 4);
            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzPcPurchaseItemListFromMc2);
            writer.WriteUInt16((ushort) packetLength);
            writer.WriteUInt32(ownerAid);
            writer.WriteUInt32(uniqueId);
            WritePlayerShopPurchaseItems(writer, items);
            return writer.ToArray();
        }

        public static byte[] BuildCzNpcMarketPurchase(params RoNpcMarketPurchaseRequest[] items) {
            items ??= Array.Empty<RoNpcMarketPurchaseRequest>();
            int packetLength = 4 + (items.Length * 8);
            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzNpcMarketPurchase);
            writer.WriteUInt16((ushort) packetLength);
            for (int i = 0; i < items.Length; i++) {
                writer.WriteUInt32(items[i].ItemId);
                writer.WriteInt32(Math.Max(1, items[i].Amount));
            }

            return writer.ToArray();
        }

        public static byte[] BuildCzNpcMarketClose() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzNpcMarketClose);
            return writer.ToArray();
        }

        public static byte[] BuildCzNpcBarterMarketPurchase(params RoNpcBarterPurchaseRequest[] items) {
            items ??= Array.Empty<RoNpcBarterPurchaseRequest>();
            int packetLength = 4 + (items.Length * 14);
            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzNpcBarterMarketPurchase);
            writer.WriteUInt16((ushort) packetLength);
            for (int i = 0; i < items.Length; i++) {
                writer.WriteUInt32(items[i].ItemId);
                writer.WriteUInt32(Math.Max(1u, items[i].Amount));
                writer.WriteUInt16(items[i].InventoryIndex);
                writer.WriteUInt32(items[i].ShopIndex);
            }

            return writer.ToArray();
        }

        public static byte[] BuildCzNpcBarterMarketClose() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzNpcBarterMarketClose);
            return writer.ToArray();
        }

        public static byte[] BuildCzNpcExpandedBarterMarketPurchase(params RoNpcExpandedBarterPurchaseRequest[] items) {
            items ??= Array.Empty<RoNpcExpandedBarterPurchaseRequest>();
            int packetLength = 4 + (items.Length * 12);
            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(RoPacketIds.CzNpcExpandedBarterMarketPurchase);
            writer.WriteUInt16((ushort) packetLength);
            for (int i = 0; i < items.Length; i++) {
                writer.WriteUInt32(items[i].ItemId);
                writer.WriteUInt32(items[i].ShopIndex);
                writer.WriteUInt32(Math.Max(1u, items[i].Amount));
            }

            return writer.ToArray();
        }

        public static byte[] BuildCzNpcExpandedBarterMarketClose() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzNpcExpandedBarterMarketClose);
            return writer.ToArray();
        }

        public static byte[] BuildCzMoveItemFromBodyToStore2(int bagIndex, int amount) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzMoveItemFromBodyToStore2);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzMoveItemFromStoreToBody2(int storageIndex, int amount) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzMoveItemFromStoreToBody2);
            writer.WriteUInt16(RequireUInt16(nameof(storageIndex), storageIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzCloseStore() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzCloseStore);
            return writer.ToArray();
        }

        public static byte[] BuildCzMoveItemFromBodyToCart(int bagIndex, int amount) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzMoveItemFromBodyToCart);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzMoveItemFromCartToBody(int cartIndex, int amount) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzMoveItemFromCartToBody);
            writer.WriteUInt16(RequireUInt16(nameof(cartIndex), cartIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzMoveItemFromStoreToCart(int storageIndex, int amount) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzMoveItemFromStoreToCart);
            writer.WriteUInt16(RequireUInt16(nameof(storageIndex), storageIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzMoveItemFromCartToStore(int cartIndex, int amount) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzMoveItemFromCartToStore);
            writer.WriteUInt16(RequireUInt16(nameof(cartIndex), cartIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzCartOff() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzCartOff);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqBankOpen(uint aid) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzReqBankOpen);
            writer.WriteUInt32(aid);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqBankOpen(int aid) {
            return BuildCzReqBankOpen(RequireUInt32(nameof(aid), aid));
        }

        public static byte[] BuildCzReqBankClose(uint aid) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzReqBankClose);
            writer.WriteUInt32(aid);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqBankClose(int aid) {
            return BuildCzReqBankClose(RequireUInt32(nameof(aid), aid));
        }

        public static byte[] BuildCzReqBankingCheck(uint aid) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzReqBankingCheck);
            writer.WriteUInt32(aid);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqBankingCheck(int aid) {
            return BuildCzReqBankingCheck(RequireUInt32(nameof(aid), aid));
        }

        public static byte[] BuildCzReqBankingDeposit(uint aid, int amount) {
            var writer = new RoPacketWriter(10);
            writer.WriteUInt16(RoPacketIds.CzReqBankingDeposit);
            writer.WriteUInt32(aid);
            writer.WriteInt32(Math.Max(1, amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzReqBankingDeposit(int aid, int amount) {
            return BuildCzReqBankingDeposit(RequireUInt32(nameof(aid), aid), amount);
        }

        public static byte[] BuildCzReqBankingWithdraw(uint aid, int amount) {
            var writer = new RoPacketWriter(10);
            writer.WriteUInt16(RoPacketIds.CzReqBankingWithdraw);
            writer.WriteUInt32(aid);
            writer.WriteInt32(Math.Max(1, amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzReqBankingWithdraw(int aid, int amount) {
            return BuildCzReqBankingWithdraw(RequireUInt32(nameof(aid), aid), amount);
        }

        public static byte[] BuildCzRequestExchangeItem(uint targetActorId) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzRequestExchangeItem);
            writer.WriteUInt32(targetActorId);
            return writer.ToArray();
        }

        public static byte[] BuildCzAckExchangeItem(bool accept) {
            var writer = new RoPacketWriter(3);
            writer.WriteUInt16(RoPacketIds.CzAckExchangeItem);
            writer.WriteByte(accept ? (byte) 3 : (byte) 4);
            return writer.ToArray();
        }

        public static byte[] BuildCzAddExchangeItem(int bagIndex, int amount) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzAddExchangeItem);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzConcludeExchangeItem() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzConcludeExchangeItem);
            return writer.ToArray();
        }

        public static byte[] BuildCzCancelExchangeItem() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzCancelExchangeItem);
            return writer.ToArray();
        }

        public static byte[] BuildCzExecExchangeItem() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzExecExchangeItem);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqWearEquipV5(int bagIndex, uint position) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzReqWearEquipV5);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteUInt32(position);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqTakeoffEquip(int bagIndex) {
            var writer = new RoPacketWriter(4);
            writer.WriteUInt16(RoPacketIds.CzReqTakeoffEquip);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            return writer.ToArray();
        }

        public static byte[] BuildCzConfig(int config, int value) {
            var writer = new RoPacketWriter(10);
            writer.WriteUInt16(RoPacketIds.CzConfig);
            writer.WriteInt32(config);
            writer.WriteInt32(value);
            return writer.ToArray();
        }

        public static byte[] BuildCzUseSkill2(int skillLevel, int skillId, int targetId) {
            return BuildCzUseSkill2(skillLevel, skillId, RequireUInt32(nameof(targetId), targetId));
        }

        /// <summary>The target of the last WZ_ESTIMATION (93) request, whose name UIMonsterInfoWnd shows ([0x1553558], 0xc1b541).</summary>
        public static volatile uint LastEstimationTargetAid;

        public static byte[] BuildCzUseSkill2(int skillLevel, int skillId, uint targetId) {
            if (skillId == 93) LastEstimationTargetAid = targetId;
            // mode msg 0x45 (0xc1b51c): GC_ROLLINGCUTTER goes out as CZ_USE_SKILL_START, held until a key goes up
            if (RoHeldSkillUse.IsHeldSkill(skillId)) return RoHeldSkillUse.BuildStart(skillLevel, skillId, targetId);
            var writer = new RoPacketWriter(10);
            writer.WriteUInt16(RoPacketIds.CzUseSkill2);
            writer.WriteUInt16(RequireUInt16(nameof(skillLevel), skillLevel));
            writer.WriteUInt16(RequireUInt16(nameof(skillId), skillId));
            writer.WriteUInt32(targetId);
            return writer.ToArray();
        }

        public static byte[] BuildCzUseSkillToGround2(int skillLevel, int skillId, int x, int y) {
            var writer = new RoPacketWriter(10);
            writer.WriteUInt16(RoPacketIds.CzUseSkillToGround2);
            writer.WriteUInt16(RequireUInt16(nameof(skillLevel), skillLevel));
            writer.WriteUInt16(RequireUInt16(nameof(skillId), skillId));
            writer.WriteUInt16(RequireUInt16(nameof(x), x));
            writer.WriteUInt16(RequireUInt16(nameof(y), y));
            return writer.ToArray();
        }

        public static byte[] BuildCzShortcutKeyChange2(int tab, int index, RoShortcutKeyEntry shortcut) {
            var writer = new RoPacketWriter(13);
            writer.WriteUInt16(RoPacketIds.CzShortcutKeyChange2);
            writer.WriteUInt16(RequireUInt16(nameof(tab), tab));
            writer.WriteUInt16(RequireUInt16(nameof(index), index));
            writer.WriteByte(unchecked((byte) shortcut.TypeRaw));
            writer.WriteUInt32(shortcut.Id);
            writer.WriteInt16(shortcut.Count);
            return writer.ToArray();
        }

        public static byte[] BuildCzShortcutKeyBarRotate2(int tab, byte rowShift) {
            var writer = new RoPacketWriter(5);
            writer.WriteUInt16(RoPacketIds.CzShortcutKeyBarRotate2);
            writer.WriteUInt16(RequireUInt16(nameof(tab), tab));
            writer.WriteByte(rowShift);
            return writer.ToArray();
        }

        public static byte[] BuildCzStatusUp(RoStatusStat stat, int amount) {
            var writer = new RoPacketWriter(5);
            writer.WriteUInt16(RoPacketIds.CzStatusUp);
            writer.WriteUInt16((ushort) stat);
            writer.WriteByte((byte) RequireByteValue(nameof(amount), amount));
            return writer.ToArray();
        }

        public static byte[] BuildCzUpgradeSkillLevel(int skillId) {
            var writer = new RoPacketWriter(4);
            writer.WriteUInt16(RoPacketIds.CzUpgradeSkillLevel);
            writer.WriteUInt16(RequireUInt16(nameof(skillId), skillId));
            return writer.ToArray();
        }

        public static byte[] BuildCzQuestActive(uint questId, bool active) {
            var writer = new RoPacketWriter(7);
            writer.WriteUInt16(RoPacketIds.CzQuestActive);
            writer.WriteUInt32(questId);
            writer.WriteByte(active ? (byte) 1 : (byte) 0);
            return writer.ToArray();
        }

        public static byte[] BuildCzMakeGroup2(string partyName, byte itemPickupRule = 0, byte itemDivisionRule = 0, Encoding encoding = null) {
            var writer = new RoPacketWriter(28);
            writer.WriteUInt16(RoPacketIds.CzMakeGroup2);
            writer.WriteFixedString(partyName ?? string.Empty, 24, encoding);
            writer.WriteByte(itemPickupRule);
            writer.WriteByte(itemDivisionRule);
            return writer.ToArray();
        }

        public static byte[] BuildCzReqExpelGroupMember(uint accountId, string characterName, Encoding encoding = null) {
            var writer = new RoPacketWriter(30);
            writer.WriteUInt16(RoPacketIds.CzReqExpelGroupMember);
            writer.WriteUInt32(accountId);
            writer.WriteFixedString(characterName ?? string.Empty, 24, encoding);
            return writer.ToArray();
        }

        public static byte[] BuildCzPartyJoinReq(string characterName, Encoding encoding = null) {
            var writer = new RoPacketWriter(26);
            writer.WriteUInt16(RoPacketIds.CzPartyJoinReq);
            writer.WriteFixedString(characterName ?? string.Empty, 24, encoding);
            return writer.ToArray();
        }

        public static byte[] BuildCzPartyJoinReqAck(uint invitationId, bool accept) {
            var writer = new RoPacketWriter(7);
            writer.WriteUInt16(RoPacketIds.CzPartyJoinReqAck);
            writer.WriteUInt32(invitationId);
            writer.WriteByte(accept ? (byte) 1 : (byte) 0);
            return writer.ToArray();
        }

        public static byte[] BuildCzLeaveGroup() {
            var writer = new RoPacketWriter(2);
            writer.WriteUInt16(RoPacketIds.CzLeaveGroup);
            return writer.ToArray();
        }

        public static byte[] BuildCzGroupInfoChangeV2(uint expOption, byte itemPickupRule, byte itemDivisionRule) {
            var writer = new RoPacketWriter(8);
            writer.WriteUInt16(RoPacketIds.CzGroupInfoChangeV2);
            writer.WriteUInt32(expOption);
            writer.WriteByte(itemPickupRule);
            writer.WriteByte(itemDivisionRule);
            return writer.ToArray();
        }

        public static byte[] BuildCzChangeGroupMaster(uint accountId) {
            var writer = new RoPacketWriter(6);
            writer.WriteUInt16(RoPacketIds.CzChangeGroupMaster);
            writer.WriteUInt32(accountId);
            return writer.ToArray();
        }

        public static byte[] BuildCzAddFriends(string characterName, Encoding encoding = null) {
            var writer = new RoPacketWriter(26);
            writer.WriteUInt16(RoPacketIds.CzAddFriends);
            writer.WriteFixedString(characterName ?? string.Empty, 24, encoding);
            return writer.ToArray();
        }

        public static byte[] BuildCzDeleteFriends(uint accountId, uint characterId) {
            var writer = new RoPacketWriter(10);
            writer.WriteUInt16(RoPacketIds.CzDeleteFriends);
            writer.WriteUInt32(accountId);
            writer.WriteUInt32(characterId);
            return writer.ToArray();
        }

        public static byte[] BuildCzAckReqAddFriends(uint accountId, uint characterId, bool accept) {
            var writer = new RoPacketWriter(14);
            writer.WriteUInt16(RoPacketIds.CzAckReqAddFriends);
            writer.WriteUInt32(accountId);
            writer.WriteUInt32(characterId);
            writer.WriteInt32(accept ? 1 : 0);
            return writer.ToArray();
        }

        private static void WriteFixedVendingShopName(RoPacketWriter writer, string value, Encoding encoding) {
            encoding ??= Encoding.ASCII;
            byte[] encoded = encoding.GetBytes(value ?? string.Empty);
            if (encoded.Length > 80) {
                throw new ArgumentOutOfRangeException(nameof(value), "Vending shop name must fit in the fixed 80-byte rAthena field.");
            }

            writer.WriteFixedString(value ?? string.Empty, 80, encoding);
        }

        private static void WritePlayerShopPurchaseItems(RoPacketWriter writer, RoPlayerShopPurchaseRequest[] items) {
            if (items.Length == 0) {
                throw new ArgumentException("Buying from a vending store requires at least one item.", nameof(items));
            }

            for (int i = 0; i < items.Length; i++) {
                if (items[i].Amount == 0) {
                    throw new ArgumentOutOfRangeException(nameof(items), "Vending purchase amount must be greater than zero.");
                }

                writer.WriteUInt16(items[i].Amount);
                writer.WriteUInt16(items[i].Index);
            }
        }

        private static ushort RequireUInt16(string paramName, int value) {
            if (value < 0 || value > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(paramName);
            }

            return (ushort) value;
        }

        private static uint RequireUInt32(string paramName, int value) {
            if (value < 0) {
                throw new ArgumentOutOfRangeException(paramName);
            }

            return unchecked((uint) value);
        }

        private static int RequireByteValue(string paramName, int value) {
            if (value < 0 || value > byte.MaxValue) {
                throw new ArgumentOutOfRangeException(paramName);
            }

            return value;
        }
    }
}
