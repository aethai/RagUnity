using System;
using System.Text;

namespace Ragnarok.Network {
    public enum RoOutboundPacketKind {
        CaLogin,
        ChEnter,
        ChSelectChar,
        ChSelectAccessibleMapName,
        ChAvailableSecondPassword,
        ChCharListRequest,
        ChMakeChar,
        ChDeleteChar3Reserved,
        ChDeleteChar3,
        ChDeleteChar3Cancel,
        ChDeleteChar2,
        CzEnter2,
        CzNotifyActorInit,
        CzRequestMove,
        CzRequestTime,
        CzPingLive,
        CzPing,
        CzActionRequest,
        CzRequestName,
        CzRestart,
        CzStandingResurrection,
        CzContactNpc,
        CzEquipWinMicroscope,
        CzConfig,
        CzNpcSelectMenu,
        CzReqNextScript,
        CzInputEditDlg,
        CzInputEditDlgStr,
        CzCloseDialog,
        CzWhisper,
        CzRequestChat,
        CzPartyChat,
        CzReqJoinGuild,
        CzJoinGuild,
        CzReqLeaveGuild,
        CzGuildChat,
        CzClanChat,
        CzCommandPet,
        CzReqItemIdentify,
        CzReqItemCompositionList,
        CzReqItemComposition,
        CzReqMakingArrow,
        CzReqChangeCart,
        CzSelectCart,
        CzSelectWarpPoint,
        CzUseItem,
        CzItemThrow,
        CzItemPickup,
        CzAckSelectDealType,
        CzPcPurchaseItemList,
        CzPcSellItemList,
        CzNpcTradeQuit,
        CzReqOpenStore2,
        CzReqCloseStore,
        CzReqBuyFromMc,
        CzPcPurchaseItemListFromMc,
        CzPcPurchaseItemListFromMc2,
        CzNpcMarketPurchase,
        CzNpcMarketClose,
        CzNpcBarterMarketPurchase,
        CzNpcBarterMarketClose,
        CzNpcExpandedBarterMarketPurchase,
        CzNpcExpandedBarterMarketClose,
        CzMoveItemFromBodyToStore,
        CzMoveItemFromStoreToBody,
        CzCloseStore,
        CzMoveItemFromBodyToCart,
        CzMoveItemFromCartToBody,
        CzMoveItemFromStoreToCart,
        CzMoveItemFromCartToStore,
        CzCartOff,
        CzReqBankOpen,
        CzReqBankClose,
        CzReqBankingCheck,
        CzReqBankingDeposit,
        CzReqBankingWithdraw,
        CzRequestExchangeItem,
        CzAckExchangeItem,
        CzAddExchangeItem,
        CzConcludeExchangeItem,
        CzCancelExchangeItem,
        CzExecExchangeItem,
        CzReqWearEquip,
        CzReqTakeoffEquip,
        CzUseSkill,
        CzUseSkillToGround,
        CzShortcutKeyChange,
        CzShortcutKeyBarRotate,
        CzStatusUp,
        CzQuestActive,
        CzMakeGroup,
        CzReqExpelGroupMember,
        CzPartyJoinReq,
        CzPartyJoinReqAck,
        CzLeaveGroup,
        CzGroupInfoChangeV2,
        CzChangeGroupMaster,
        CzAddFriends,
        CzDeleteFriends,
        CzReqNameByGid,
        CzCreateChatRoom,
        CzChangeChatRoom,
        CzReqEnterRoom,
        CzReqRoleChange,
        CzReqExpelMember,
        CzExitRoom,
        CzReqEmotion,
        CzReqEmotionExpansion,
        CzReqItemRepair2,
        CzReqWeaponRefine,
        CzSelectAutoSpell,
        CzReqMergeItem,
        CzCancelMergeItem,
        CzReqMakingItem,
        CzRenamePet,
        CzSelectPetEgg,
        CzTryCaptureMonster,
        CzPetEvolution,
        CzCommandMer,
        CzRenameMer,
        CzMerCommand,
        CzReqGuildMenuInterface,
        CzReqGuildMenu,
        CzReqChangeMemberPos,
        CzRegChangeGuildPositionInfo,
        CzReqBanGuild,
        CzReqDisorganizeGuild,
        CzReqMakeGuild,
        CzAllyGuild,
        CzGuildNotice,
        CzReqOpenBuyingStore,
        CzReqCloseBuyingStore,
        CzReqClickToBuyingStore,
        CzReqTradeBuyingStore,
        CzAckReqAddFriends,
        CzOpenAllRodex,
        CzUpdateAllRodex,
        CzCloseRodexBox,
        CzReqReadRodex,
        CzReqNextRodex,
        CzReqDeleteRodex,
        CzReqZenyFromRodex,
        CzReqItemFromRodex,
        CzReqCancelWriteRodex,
        CzReqAddItemRodex,
        CzReqRemoveRodexItem,
        CzReqOpenWriteRodex,
        CzCheckReceiveCharacterName,
        CzCheckRodexReceive,
        CzReqSendRodex2,
        CzOpenUi,
        CzReqAttendance,
        CzHwid
    }

    public sealed class RoUnsupportedPacketProfileException : InvalidOperationException {
        public RoUnsupportedPacketProfileException(IRoPacketProfile profile, RoOutboundPacketKind kind)
            : base($"Packet profile '{RequireProfile(profile).Name}' does not support outbound packet '{kind}'.") {
            ProfileName = profile.Name;
            PacketVersion = profile.PacketVersion;
            PacketKind = kind;
        }

        public string ProfileName { get; }
        public int PacketVersion { get; }
        public RoOutboundPacketKind PacketKind { get; }

        private static IRoPacketProfile RequireProfile(IRoPacketProfile profile) {
            return profile ?? throw new ArgumentNullException(nameof(profile));
        }
    }

    public sealed class RoProfiledOutboundPackets {
        private readonly IRoPacketProfile profile;
        private readonly RoPacketRegistry registry;

        public RoProfiledOutboundPackets(IRoPacketProfile profile)
            : this(profile, profile?.CreateRegistry()) {
        }

        public RoProfiledOutboundPackets(IRoPacketProfile profile, RoPacketRegistry registry) {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public IRoPacketProfile Profile => profile;
        public RoPacketRegistry Registry => registry;

        public RoPacketDefinition ResolveDefinition(RoOutboundPacketKind kind) {
            if (TryResolveDefinition(kind, out RoPacketDefinition definition)) {
                return definition;
            }

            throw new RoUnsupportedPacketProfileException(profile, kind);
        }

        public bool TryResolveDefinition(RoOutboundPacketKind kind, out RoPacketDefinition definition) {
            string[] names = GetPacketNames(kind);
            for (int i = 0; i < names.Length; i++) {
                if (registry.TryGet(names[i], RoPacketDirection.ClientToServer, out definition)) {
                    return true;
                }
            }

            definition = default;
            return false;
        }

        public byte[] BuildCaLogin(string account, string password) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CaLogin);
            var writer = CreateFixedWriter(definition);
            writer.WriteInt32(profile.ClientVersion);
            writer.WriteFixedString(account ?? string.Empty, 24);
            writer.WriteFixedString(password ?? string.Empty, 24);
            writer.WriteByte((byte) profile.LanguageType);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildChEnter(int accountId, int loginId1, int loginId2, byte sex) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChEnter);
            var writer = CreateFixedWriter(definition);
            writer.WriteInt32(accountId);
            writer.WriteInt32(loginId1);
            writer.WriteInt32(loginId2);
            writer.WriteInt16(0);
            writer.WriteByte(sex);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildChSelectChar(int characterSlot) {
            RequireByte(nameof(characterSlot), characterSlot);
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChSelectChar);
            var writer = CreateFixedWriter(definition);
            writer.WriteByte((byte) characterSlot);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildChSelectAccessibleMapName(int characterSlot, int mapIndex) {
            RequireByte(nameof(characterSlot), characterSlot);
            RequireByte(nameof(mapIndex), mapIndex);
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChSelectAccessibleMapName);
            var writer = CreateFixedWriter(definition);
            writer.WriteByte((byte) characterSlot);
            writer.WriteByte((byte) mapIndex);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildChAvailableSecondPassword(int accountId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChAvailableSecondPassword);
            var writer = CreateFixedWriter(definition);
            writer.WriteInt32(accountId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildChCharListRequest() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChCharListRequest);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildChMakeChar(
            string name,
            int characterSlot,
            ushort hairColor,
            ushort hairStyle,
            int startJob,
            byte sex,
            Encoding encoding = null) {
            RequireByte(nameof(characterSlot), characterSlot);
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChMakeChar);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(name ?? string.Empty, 24, encoding);
            writer.WriteByte((byte) characterSlot);
            writer.WriteUInt16(hairColor);
            writer.WriteUInt16(hairStyle);
            if (profile.PacketVersion == 20240619) {
                writer.WriteUInt16(RequireUInt16(nameof(startJob), startJob));
                writer.WriteByte(0);
                writer.WriteByte(0);
            } else {
                writer.WriteInt32(startJob);
            }

            writer.WriteByte(sex);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildChDeleteChar3Reserved(int characterId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChDeleteChar3Reserved);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(characterId), characterId));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildChDeleteChar3(int characterId, string birthdateYyMmDd) {
            if (string.IsNullOrWhiteSpace(birthdateYyMmDd) || birthdateYyMmDd.Trim().Length != 6) {
                throw new ArgumentException("Birthdate must be exactly six digits in YYMMDD format.", nameof(birthdateYyMmDd));
            }

            string trimmedBirthdate = birthdateYyMmDd.Trim();
            for (int i = 0; i < trimmedBirthdate.Length; i++) {
                if (trimmedBirthdate[i] < '0' || trimmedBirthdate[i] > '9') {
                    throw new ArgumentException("Birthdate must contain only digits in YYMMDD format.", nameof(birthdateYyMmDd));
                }
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChDeleteChar3);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(characterId), characterId));
            writer.WriteFixedString(trimmedBirthdate, 6, Encoding.ASCII);
            return FinishFixed(writer, definition);
        }

        /// <summary>
        /// CH_DELETE_CHAR2 0x01fb {charId u32, key[50]}: the delete of langtype 1 / 10 / 12 (CLoginMode state 0xb,
        /// 0xcb4a61): the text of UIEmailAddressWnd copied 50 bytes, byte 49 cleared. Hercules compares it with the
        /// account's e-mail (any text passes for the default a@a.com).
        /// </summary>
        public byte[] BuildChDeleteChar2(int characterId, string key, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChDeleteChar2);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(characterId), characterId));
            string text = key ?? string.Empty;
            Encoding resolved = encoding ?? Encoding.ASCII;
            while (text.Length > 0 && resolved.GetByteCount(text) > 49) {
                text = text.Substring(0, text.Length - 1);
            }

            writer.WriteFixedString(text, 50, resolved);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildChDeleteChar3Cancel(int characterId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.ChDeleteChar3Cancel);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(characterId), characterId));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzEnter2(int accountId, int characterId, int loginId1, int clientTime, byte sex) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzEnter2);
            var writer = CreateFixedWriter(definition);
            writer.WriteInt32(accountId);
            writer.WriteInt32(characterId);
            writer.WriteInt32(loginId1);
            writer.WriteInt32(clientTime);
            if (definition.Length == 23) {
                writer.WriteUInt32(0);
            } else if (definition.Length != 19) {
                throw new InvalidOperationException(
                    $"Outbound packet '{definition.Name}' in profile '{profile.Name}' has unsupported length {definition.Length}.");
            }

            writer.WriteByte(sex);
            return FinishFixed(writer, definition);
        }

        /// <summary>Custom CZ_HWID 0x0be3: {id, 48, volume serial.L, identifier[40]} (see <see cref="RoDeviceIdentity"/>).</summary>
        public byte[] BuildCzHwid(uint volumeSerial, byte[] identifier) {
            if (identifier == null || identifier.Length != RoDeviceIdentity.IdentifierLength) {
                throw new ArgumentException("The device identifier must be 40 bytes.", nameof(identifier));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzHwid);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16((ushort)definition.Length);
            writer.WriteUInt32(volumeSerial);
            writer.WriteBytes(identifier);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzNotifyActorInit() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzNotifyActorInit);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzRequestMove2(int x, int y, int direction = 0) {
            return BuildCzRequestMove(x, y, direction);
        }

        public byte[] BuildCzRequestMove(int x, int y, int direction = 0) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzRequestMove);
            var writer = CreateFixedWriter(definition);
            int encodedDirection = string.Equals(definition.Name, "CZ_REQUEST_MOVE", StringComparison.Ordinal)
                ? 0
                : direction;
            writer.WritePosition(new RoPacketPosition(x, y, encodedDirection));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzRequestTime2(int clientTime) {
            return BuildCzRequestTime(clientTime);
        }

        public byte[] BuildCzRequestTime(int clientTime) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzRequestTime);
            var writer = CreateFixedWriter(definition);
            writer.WriteInt32(clientTime);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzPingLive() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzPingLive);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzPing(int aid) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzPing);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(aid), aid));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzActionRequest2(int targetAid, byte actionType) {
            return BuildCzActionRequest2(RequireUInt32(nameof(targetAid), targetAid), actionType);
        }

        public byte[] BuildCzActionRequest2(uint targetAid, byte actionType) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzActionRequest);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(targetAid);
            writer.WriteByte(actionType);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzRequestName2(uint actorId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzRequestName);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(actorId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzRequestName2(int actorId) {
            return BuildCzRequestName2(RequireUInt32(nameof(actorId), actorId));
        }

        public byte[] BuildCzRestart(byte type) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzRestart);
            var writer = CreateFixedWriter(definition);
            writer.WriteByte(type);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzStandingResurrection() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzStandingResurrection);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzContactNpc(int npcId, byte type) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzContactNpc);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteByte(type);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzEquipWinMicroscope(uint targetActorId) {
            if (targetActorId == 0) {
                throw new ArgumentOutOfRangeException(nameof(targetActorId));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzEquipWinMicroscope);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(targetActorId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzConfig(int config, int value) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzConfig);
            var writer = CreateFixedWriter(definition);
            writer.WriteInt32(config);
            writer.WriteInt32(value);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzNpcSelectMenu(int npcId, byte choice) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzNpcSelectMenu);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteByte(choice);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqNextScript(int npcId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqNextScript);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzInputEditDlg(int npcId, int value) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzInputEditDlg);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteInt32(value);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzInputEditDlgStr(int npcId, string value, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzInputEditDlgStr);
            byte[] payload = (encoding ?? Encoding.ASCII).GetBytes(value ?? string.Empty);
            int packetLength = 2 + 2 + 4 + payload.Length + 1;
            var writer = CreateVariableWriter(definition, packetLength);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteBytes(payload);
            writer.WriteByte(0);
            return writer.ToArray();
        }

        public byte[] BuildCzCloseDialog(int npcId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCloseDialog);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzRequestChat(string characterName, string message, Encoding encoding = null) {
            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzRequestChat);
            string payload = $"{characterName.Trim()} : {message.Trim()}";
            byte[] payloadBytes = (encoding ?? Encoding.ASCII).GetBytes(payload);
            int packetLength = 4 + payloadBytes.Length;
            var writer = CreateVariableWriter(definition, packetLength);
            writer.WriteBytes(payloadBytes);
            return writer.ToArray();
        }

        public byte[] BuildCzWhisper(string receiverName, string message, Encoding encoding = null) {
            if (string.IsNullOrWhiteSpace(receiverName)) {
                throw new ArgumentException("Whisper receiver is required.", nameof(receiverName));
            }

            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzWhisper);
            Encoding resolvedEncoding = encoding ?? Encoding.ASCII;
            byte[] messageBytes = resolvedEncoding.GetBytes(message.Trim());
            int packetLength = 4 + 24 + messageBytes.Length + 1;
            var writer = CreateVariableWriter(definition, packetLength);
            writer.WriteFixedString(receiverName.Trim(), 24, resolvedEncoding);
            writer.WriteBytes(messageBytes);
            writer.WriteByte(0);
            return writer.ToArray();
        }

        public byte[] BuildCzGuildChat(string message, Encoding encoding = null) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzGuildChat);
            byte[] payloadBytes = (encoding ?? Encoding.ASCII).GetBytes(message.Trim());
            int packetLength = 4 + payloadBytes.Length + 1;
            var writer = CreateVariableWriter(definition, packetLength);
            writer.WriteBytes(payloadBytes);
            writer.WriteByte(0);
            return writer.ToArray();
        }

        public byte[] BuildCzPartyChat(string characterName, string message, Encoding encoding = null) {
            return BuildNamePrefixedChat(RoOutboundPacketKind.CzPartyChat, characterName, message, encoding);
        }

        public byte[] BuildCzGuildChat(string characterName, string message, Encoding encoding = null) {
            return BuildNamePrefixedChat(RoOutboundPacketKind.CzGuildChat, characterName, message, encoding);
        }

        public byte[] BuildCzClanChat(string characterName, string message, Encoding encoding = null) {
            return BuildNamePrefixedChat(RoOutboundPacketKind.CzClanChat, characterName, message, encoding);
        }

        private byte[] BuildNamePrefixedChat(RoOutboundPacketKind kind, string characterName, string message, Encoding encoding = null) {
            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            RoPacketDefinition definition = ResolveDefinition(kind);
            string payload = $"{characterName.Trim()} : {message.Trim()}";
            byte[] payloadBytes = (encoding ?? Encoding.ASCII).GetBytes(payload);
            int packetLength = 4 + payloadBytes.Length + 1;
            var writer = CreateVariableWriter(definition, packetLength);
            writer.WriteBytes(payloadBytes);
            writer.WriteByte(0);
            return writer.ToArray();
        }

        public byte[] BuildCzReqJoinGuild(uint targetActorId, uint myActorId, uint myGuildId) {
            if (targetActorId == 0) {
                throw new ArgumentOutOfRangeException(nameof(targetActorId));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqJoinGuild);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(targetActorId);
            writer.WriteUInt32(myActorId);
            writer.WriteUInt32(myGuildId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzJoinGuild(uint guildId, bool accept) {
            if (guildId == 0) {
                throw new ArgumentOutOfRangeException(nameof(guildId));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzJoinGuild);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(guildId);
            writer.WriteInt32(accept ? 1 : 0);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqLeaveGuild(uint guildId, uint accountId, uint characterId, string reason, Encoding encoding = null) {
            if (guildId == 0) {
                throw new ArgumentOutOfRangeException(nameof(guildId));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqLeaveGuild);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(guildId);
            writer.WriteUInt32(accountId);
            writer.WriteUInt32(characterId);
            writer.WriteFixedString(reason ?? string.Empty, 40, encoding ?? Encoding.ASCII);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzCommandPet(byte subCommand) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCommandPet);
            var writer = CreateFixedWriter(definition);
            writer.WriteByte(subCommand);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqItemCompositionList(int cardIndex) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqItemCompositionList);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(cardIndex), cardIndex));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqItemComposition(int cardIndex, int equipIndex) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqItemComposition);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(cardIndex), cardIndex));
            writer.WriteUInt16(RequireUInt16(nameof(equipIndex), equipIndex));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqItemIdentify(int bagIndex) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqItemIdentify);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzCancelItemIdentify() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqItemIdentify);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(ushort.MaxValue);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqMakingArrow(uint itemId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqMakingArrow);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(itemId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqChangeCart(int cartNumber) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqChangeCart);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(cartNumber), cartNumber));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzSelectCart(uint identity, int cartType) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzSelectCart);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(identity);
            writer.WriteByte((byte) RequireByteValue(nameof(cartType), cartType));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzSelectWarpPoint(ushort skillId, string mapName, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzSelectWarpPoint);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(skillId);
            writer.WriteFixedString(mapName ?? string.Empty, 16, encoding ?? Encoding.ASCII);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzUseItem2(int bagIndex, int aid) {
            return BuildCzUseItem2(bagIndex, RequireUInt32(nameof(aid), aid));
        }

        public byte[] BuildCzUseItem2(int bagIndex, uint aid) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzUseItem);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteUInt32(aid);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzItemThrow2(int bagIndex, int count) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzItemThrow);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteUInt16(RequireUInt16(nameof(count), count));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzItemPickup2(int itemAid) {
            return BuildCzItemPickup2(RequireUInt32(nameof(itemAid), itemAid));
        }

        public byte[] BuildCzItemPickup2(uint itemAid) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzItemPickup);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(itemAid);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzAckSelectDealType(int npcId, byte type) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzAckSelectDealType);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(RequireUInt32(nameof(npcId), npcId));
            writer.WriteByte(type);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzPcPurchaseItemList(params RoNpcShopPurchaseRequest[] items) {
            items ??= Array.Empty<RoNpcShopPurchaseRequest>();
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzPcPurchaseItemList);
            // amount.W itemId.L per item (the server's PACKET_CZ_PC_PURCHASE_ITEMLIST_sub since 20181121; Custom
            // item ids go past 65535, e.g. 2137000): with 2-byte ids the server read no item and "sold" nothing
            int packetLength = 4 + (items.Length * 6);
            var writer = CreateVariableWriter(definition, packetLength);
            for (int i = 0; i < items.Length; i++) {
                writer.WriteUInt16(RequireUInt16(nameof(RoNpcShopPurchaseRequest.Amount), items[i].Amount));
                writer.WriteUInt32(items[i].ItemId);
            }

            return writer.ToArray();
        }

        public byte[] BuildCzPcSellItemList(params RoNpcShopSellRequest[] items) {
            items ??= Array.Empty<RoNpcShopSellRequest>();
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzPcSellItemList);
            int packetLength = 4 + (items.Length * 4);
            var writer = CreateVariableWriter(definition, packetLength);
            for (int i = 0; i < items.Length; i++) {
                writer.WriteUInt16(RequireUInt16(nameof(RoNpcShopSellRequest.BagIndex), items[i].BagIndex));
                writer.WriteUInt16(RequireUInt16(nameof(RoNpcShopSellRequest.Amount), items[i].Amount));
            }

            return writer.ToArray();
        }

        public byte[] BuildCzNpcTradeQuit() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzNpcTradeQuit);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqOpenStore2(string shopName, byte result, params RoVendingOpenStoreItem[] items) {
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

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqOpenStore2);
            int packetLength = 85 + (items.Length * 8);
            var writer = CreateVariableWriter(definition, packetLength);
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

        public byte[] BuildCzReqCloseStore() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqCloseStore);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqBuyFromMc(uint ownerAid) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqBuyFromMc);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(ownerAid);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzPcPurchaseItemListFromMc(uint ownerAid, params RoPlayerShopPurchaseRequest[] items) {
            items ??= Array.Empty<RoPlayerShopPurchaseRequest>();
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzPcPurchaseItemListFromMc);
            int packetLength = 8 + (items.Length * 4);
            var writer = CreateVariableWriter(definition, packetLength);
            writer.WriteUInt32(ownerAid);
            WritePlayerShopPurchaseItems(writer, items);
            return writer.ToArray();
        }

        public byte[] BuildCzPcPurchaseItemListFromMc2(uint ownerAid, uint uniqueId, params RoPlayerShopPurchaseRequest[] items) {
            items ??= Array.Empty<RoPlayerShopPurchaseRequest>();
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzPcPurchaseItemListFromMc2);
            int packetLength = 12 + (items.Length * 4);
            var writer = CreateVariableWriter(definition, packetLength);
            writer.WriteUInt32(ownerAid);
            writer.WriteUInt32(uniqueId);
            WritePlayerShopPurchaseItems(writer, items);
            return writer.ToArray();
        }

        public byte[] BuildCzNpcMarketPurchase(params RoNpcMarketPurchaseRequest[] items) {
            items ??= Array.Empty<RoNpcMarketPurchaseRequest>();
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzNpcMarketPurchase);
            bool shortItemIds = profile.PacketVersion < 20181121;
            int recordLength = shortItemIds ? 6 : 8;
            int packetLength = 4 + (items.Length * recordLength);
            var writer = CreateVariableWriter(definition, packetLength);
            for (int i = 0; i < items.Length; i++) {
                if (shortItemIds) {
                    writer.WriteUInt16(RequireUInt16(nameof(RoNpcMarketPurchaseRequest.ItemId), items[i].ItemId));
                } else {
                    writer.WriteUInt32(items[i].ItemId);
                }

                writer.WriteInt32(Math.Max(1, items[i].Amount));
            }

            return writer.ToArray();
        }

        public byte[] BuildCzNpcMarketClose() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzNpcMarketClose);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzNpcBarterMarketPurchase(params RoNpcBarterPurchaseRequest[] items) {
            items ??= Array.Empty<RoNpcBarterPurchaseRequest>();
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzNpcBarterMarketPurchase);
            bool shortItemIds = profile.PacketVersion < 20181121;
            int recordLength = shortItemIds ? 12 : 14;
            int packetLength = 4 + (items.Length * recordLength);
            var writer = CreateVariableWriter(definition, packetLength);
            for (int i = 0; i < items.Length; i++) {
                if (shortItemIds) {
                    writer.WriteUInt16(RequireUInt16(nameof(RoNpcBarterPurchaseRequest.ItemId), items[i].ItemId));
                } else {
                    writer.WriteUInt32(items[i].ItemId);
                }

                writer.WriteUInt32(Math.Max(1u, items[i].Amount));
                writer.WriteUInt16(items[i].InventoryIndex);
                writer.WriteUInt32(items[i].ShopIndex);
            }

            return writer.ToArray();
        }

        public byte[] BuildCzNpcBarterMarketClose() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzNpcBarterMarketClose);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzNpcExpandedBarterMarketPurchase(params RoNpcExpandedBarterPurchaseRequest[] items) {
            items ??= Array.Empty<RoNpcExpandedBarterPurchaseRequest>();
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzNpcExpandedBarterMarketPurchase);
            bool shortItemIds = profile.PacketVersion < 20181121;
            int recordLength = shortItemIds ? 10 : 12;
            int packetLength = 4 + (items.Length * recordLength);
            var writer = CreateVariableWriter(definition, packetLength);
            for (int i = 0; i < items.Length; i++) {
                if (shortItemIds) {
                    writer.WriteUInt16(RequireUInt16(nameof(RoNpcExpandedBarterPurchaseRequest.ItemId), items[i].ItemId));
                } else {
                    writer.WriteUInt32(items[i].ItemId);
                }

                writer.WriteUInt32(items[i].ShopIndex);
                writer.WriteUInt32(Math.Max(1u, items[i].Amount));
            }

            return writer.ToArray();
        }

        public byte[] BuildCzNpcExpandedBarterMarketClose() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzNpcExpandedBarterMarketClose);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzMoveItemFromBodyToStore2(int bagIndex, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzMoveItemFromBodyToStore);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzMoveItemFromStoreToBody2(int storageIndex, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzMoveItemFromStoreToBody);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(storageIndex), storageIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzCloseStore() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCloseStore);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzMoveItemFromBodyToCart(int bagIndex, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzMoveItemFromBodyToCart);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzMoveItemFromCartToBody(int cartIndex, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzMoveItemFromCartToBody);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(cartIndex), cartIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzMoveItemFromStoreToCart(int storageIndex, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzMoveItemFromStoreToCart);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(storageIndex), storageIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzMoveItemFromCartToStore(int cartIndex, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzMoveItemFromCartToStore);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(cartIndex), cartIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzCartOff() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCartOff);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqBankOpen(uint aid) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqBankOpen);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(aid);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqBankClose(uint aid) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqBankClose);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(aid);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqBankingCheck(uint aid) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqBankingCheck);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(aid);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqBankingDeposit(uint aid, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqBankingDeposit);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(aid);
            writer.WriteInt32(Math.Max(1, amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqBankingWithdraw(uint aid, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqBankingWithdraw);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(aid);
            writer.WriteInt32(Math.Max(1, amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzRequestExchangeItem(uint targetActorId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzRequestExchangeItem);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(targetActorId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzAckExchangeItem(bool accept) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzAckExchangeItem);
            var writer = CreateFixedWriter(definition);
            writer.WriteByte(accept ? (byte) 3 : (byte) 4);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzAddExchangeItem(int bagIndex, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzAddExchangeItem);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteInt32(Math.Max(1, amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzConcludeExchangeItem() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzConcludeExchangeItem);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzCancelExchangeItem() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCancelExchangeItem);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzExecExchangeItem() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzExecExchangeItem);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqWearEquipV5(int bagIndex, uint position) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqWearEquip);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteUInt32(position);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqTakeoffEquip(int bagIndex) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqTakeoffEquip);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzUseSkill2(int skillLevel, int skillId, int targetId) {
            return BuildCzUseSkill2(skillLevel, skillId, RequireUInt32(nameof(targetId), targetId));
        }

        public byte[] BuildCzUseSkill2(int skillLevel, int skillId, uint targetId) {
            if (skillId == 93) RoOutboundPackets.LastEstimationTargetAid = targetId;
            // mode msg 0x45 (0xc1b51c): GC_ROLLINGCUTTER goes out as CZ_USE_SKILL_START, held until a key goes up
            if (RoHeldSkillUse.IsHeldSkill(skillId)) return RoHeldSkillUse.BuildStart(skillLevel, skillId, targetId);
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzUseSkill);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(skillLevel), skillLevel));
            writer.WriteUInt16(RequireUInt16(nameof(skillId), skillId));
            writer.WriteUInt32(targetId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzUseSkillToGround2(int skillLevel, int skillId, int x, int y) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzUseSkillToGround);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(skillLevel), skillLevel));
            writer.WriteUInt16(RequireUInt16(nameof(skillId), skillId));
            writer.WriteUInt16(RequireUInt16(nameof(x), x));
            writer.WriteUInt16(RequireUInt16(nameof(y), y));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzShortcutKeyChange2(int tab, int index, RoShortcutKeyEntry shortcut) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzShortcutKeyChange);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(tab), tab));
            writer.WriteUInt16(RequireUInt16(nameof(index), index));
            writer.WriteByte(unchecked((byte) shortcut.TypeRaw));
            writer.WriteUInt32(shortcut.Id);
            writer.WriteInt16(shortcut.Count);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzShortcutKeyBarRotate2(int tab, byte rowShift) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzShortcutKeyBarRotate);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(tab), tab));
            writer.WriteByte(rowShift);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzStatusUp(RoStatusStat stat, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzStatusUp);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16((ushort) stat);
            writer.WriteByte((byte) RequireByteValue(nameof(amount), amount));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzQuestActive(uint questId, bool active) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzQuestActive);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(questId);
            writer.WriteByte(active ? (byte) 1 : (byte) 0);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzMakeGroup2(string partyName, byte itemPickupRule = 0, byte itemDivisionRule = 0, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzMakeGroup);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(partyName ?? string.Empty, 24, encoding);
            writer.WriteByte(itemPickupRule);
            writer.WriteByte(itemDivisionRule);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqExpelGroupMember(uint accountId, string characterName, Encoding encoding = null) {
            if (accountId == 0) {
                throw new ArgumentOutOfRangeException(nameof(accountId));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqExpelGroupMember);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(accountId);
            writer.WriteFixedString(characterName ?? string.Empty, 24, encoding);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzPartyJoinReq(string characterName, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzPartyJoinReq);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(characterName ?? string.Empty, 24, encoding);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzPartyJoinReqAck(uint invitationId, bool accept) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzPartyJoinReqAck);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(invitationId);
            writer.WriteByte(accept ? (byte) 1 : (byte) 0);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzLeaveGroup() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzLeaveGroup);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzGroupInfoChangeV2(uint expOption, byte itemPickupRule, byte itemDivisionRule) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzGroupInfoChangeV2);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(expOption);
            writer.WriteByte(itemPickupRule);
            writer.WriteByte(itemDivisionRule);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzChangeGroupMaster(uint accountId) {
            if (accountId == 0) {
                throw new ArgumentOutOfRangeException(nameof(accountId));
            }

            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzChangeGroupMaster);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(accountId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzAddFriends(string characterName, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzAddFriends);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(characterName ?? string.Empty, 24, encoding);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzDeleteFriends(uint accountId, uint characterId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzDeleteFriends);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(accountId);
            writer.WriteUInt32(characterId);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_CREATE_CHATROOM / CZ_CHANGE_CHATROOM: limit, public, password[8], the raw title (15 + title bytes).</summary>
        public byte[] BuildCzChatRoomForm(bool change, int limit, bool isPublic, string password, string title, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(change ? RoOutboundPacketKind.CzChangeChatRoom : RoOutboundPacketKind.CzCreateChatRoom);
            byte[] titleBytes = (encoding ?? Encoding.ASCII).GetBytes(title ?? string.Empty);
            var writer = CreateVariableWriter(definition, 15 + titleBytes.Length);
            writer.WriteUInt16((ushort) Math.Max(0, Math.Min(limit, ushort.MaxValue)));
            writer.WriteByte((byte) (isPublic ? 1 : 0));
            writer.WriteFixedString(password ?? string.Empty, 8, encoding);
            writer.WriteBytes(titleBytes);
            return writer.ToArray();
        }

        public byte[] BuildCzReqEnterRoom(uint chatId, string password, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqEnterRoom);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(chatId);
            writer.WriteFixedString(password ?? string.Empty, 8, encoding);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqRoleChange(string name, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqRoleChange);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(0);
            writer.WriteFixedString(name ?? string.Empty, 24, encoding);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqExpelMember(string name, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqExpelMember);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(name ?? string.Empty, 24, encoding);
            return FinishFixed(writer, definition);
        }

        /// <summary>
        /// CZ_REQ_OPEN_BUYING_STORE: Zeny limit.L, result.B (1 open, 0 cancel), title[80], then (item id.L, amount.W,
        /// price.L) per wanted item (89 + 10n; client mode 0x10f).
        /// </summary>
        public byte[] BuildCzReqOpenBuyingStore(uint zenyLimit, bool open, string title, System.Collections.Generic.IReadOnlyList<RoBuyingStoreItem> items, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqOpenBuyingStore);
            int count = items?.Count ?? 0;
            var writer = CreateVariableWriter(definition, 89 + 10 * count);
            writer.WriteUInt32(zenyLimit);
            writer.WriteByte((byte) (open ? 1 : 0));
            byte[] titleBytes = (encoding ?? Encoding.ASCII).GetBytes(title ?? string.Empty);
            var titleField = new byte[80];
            Array.Copy(titleBytes, titleField, Math.Min(79, titleBytes.Length));
            writer.WriteBytes(titleField);
            for (int i = 0; i < count; i++) {
                writer.WriteUInt32(items[i].ItemId);
                writer.WriteUInt16((ushort) Math.Max(0, Math.Min(items[i].Amount, ushort.MaxValue)));
                writer.WriteUInt32(items[i].Price);
            }

            return writer.ToArray();
        }

        public byte[] BuildCzReqCloseBuyingStore() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqCloseBuyingStore);
            return FinishFixed(CreateFixedWriter(definition), definition);
        }

        public byte[] BuildCzReqClickToBuyingStore(uint buyerAid) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqClickToBuyingStore);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(buyerAid);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_REQ_TRADE_BUYING_STORE: buyer AID, store id, then (inventory index.W, item id.L, amount.W) per item (12 + 8n).</summary>
        public byte[] BuildCzReqTradeBuyingStore(uint buyerAid, uint storeId, System.Collections.Generic.IReadOnlyList<(ushort BagIndex, uint ItemId, int Amount)> items) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqTradeBuyingStore);
            int count = items?.Count ?? 0;
            var writer = CreateVariableWriter(definition, 12 + 8 * count);
            writer.WriteUInt32(buyerAid);
            writer.WriteUInt32(storeId);
            for (int i = 0; i < count; i++) {
                writer.WriteUInt16(items[i].BagIndex);
                writer.WriteUInt32(items[i].ItemId);
                writer.WriteUInt16((ushort) Math.Max(0, Math.Min(items[i].Amount, ushort.MaxValue)));
            }

            return writer.ToArray();
        }

        /// <summary>CZ_REQ_EMOTION: the emotion type byte (Hercules clif_parse_Emotion at +2).</summary>
        public byte[] BuildCzReqEmotion(byte emotion) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqEmotion);
            var writer = CreateFixedWriter(definition);
            writer.WriteByte(emotion);
            return FinishFixed(writer, definition);
        }

        /// <summary>
        /// CZ_REQ_EMOTION_EXPANSION 0x0be9 (client 20240619, length 6): the emotion pack and the emotion, both words — what the
        /// client's mode msg 0x1f sends for every emotion (the basic pack is 0). Custom's Hercules maps it to clif_parse_Emotion
        /// reading the emotion byte at +4.
        /// </summary>
        public byte[] BuildCzReqEmotionExpansion(ushort pack, ushort emotion) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqEmotionExpansion);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(pack);
            writer.WriteUInt16(emotion);
            return FinishFixed(writer, definition);
        }

        /// <summary>
        /// CZ_REQ_ITEMREPAIR2 0x0b66 (20191224+): the listed record (index.W as listed, item id.L, four cards.L, refine.B,
        /// grade.B); index −1 cancels (the server reads only the index).
        /// </summary>
        public byte[] BuildCzReqItemRepair2(short index, uint itemId, uint[] cards, byte refine) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqItemRepair2);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(unchecked((ushort) index));
            writer.WriteUInt32(itemId);
            for (int c = 0; c < 4; c++) writer.WriteUInt32(cards != null && c < cards.Length ? cards[c] : 0);
            writer.WriteByte(refine);
            writer.WriteByte(0);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_REQ_WEAPONREFINE 0x0222: the listed index as a full u32 (0xffffffff cancels, as the client's Cancel sends).</summary>
        public byte[] BuildCzReqWeaponRefine(int index) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqWeaponRefine);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(unchecked((uint) index));
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_SELECTAUTOSPELL 0x01ce: the chosen skill id.L (0 = Cancel).</summary>
        public byte[] BuildCzSelectAutoSpell(int skillId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzSelectAutoSpell);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(unchecked((uint) skillId));
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_REQ_MERGE_ITEM 0x096e: length.W, then the chosen inventory indexes.W as listed.</summary>
        public byte[] BuildCzReqMergeItem(System.Collections.Generic.IReadOnlyList<short> bagIndexes) {
            int count = bagIndexes?.Count ?? 0;
            var packet = new byte[4 + 2 * count];
            packet[0] = (byte) (RoPacketIds.CzReqMergeItem & 0xff);
            packet[1] = (byte) (RoPacketIds.CzReqMergeItem >> 8);
            packet[2] = (byte) (packet.Length & 0xff);
            packet[3] = (byte) (packet.Length >> 8);
            for (int i = 0; i < count; i++) {
                packet[4 + 2 * i] = (byte) (bagIndexes[i] & 0xff);
                packet[5 + 2 * i] = (byte) ((bagIndexes[i] >> 8) & 0xff);
            }

            return packet;
        }

        /// <summary>CZ_RENAME_PET 0x01a5: the name in 24 bytes (23 + NUL).</summary>
        public byte[] BuildCzRenamePet(string name, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzRenamePet);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(name ?? string.Empty, 24, encoding);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_SELECT_PETEGG 0x01a7: the chosen egg's inventory index as listed.</summary>
        public byte[] BuildCzSelectPetEgg(short bagIndex) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzSelectPetEgg);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(unchecked((ushort) bagIndex));
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_COMMAND_MER 0x022d (mode 0xb8, 0xc1deac): type.W 0, command.B (1 feed, 2 delete).</summary>
        public byte[] BuildCzCommandMer(byte command) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCommandMer);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(0);
            writer.WriteByte(command);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_RENAME_MER 0x0231 (mode 0xb9): the homunculus name in 24 bytes.</summary>
        public byte[] BuildCzRenameMer(string name, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzRenameMer);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(name ?? string.Empty, 24, encoding);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_MER_COMMAND 0x029f (mode 0xba): command.B (2 fires the mercenary).</summary>
        /// <summary>CZ_REQ_GUILD_MENUINTERFACE 0x014d (mode 0x69, sent by FUN_008b97a0 while the tab mask is still -1).</summary>
        public byte[] BuildCzReqGuildMenuInterface() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqGuildMenuInterface);
            return FinishFixed(CreateFixedWriter(definition), definition);
        }

        /// <summary>CZ_REQ_GUILD_MENU 0x014f (mode 0x6b): type.L (0 info, 1 members, 2 positions, 3 skills, 4 expelled).</summary>
        public byte[] BuildCzReqGuildMenu(int type) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqGuildMenu);
            var writer = CreateFixedWriter(definition);
            writer.WriteInt32(type);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_REQ_CHANGE_MEMBERPOS 0x0155 (mode 0x67): { account.L, char.L, position.L }*.</summary>
        public byte[] BuildCzReqChangeMemberPos(System.Collections.Generic.IReadOnlyList<(uint AccountId, uint CharId, int PositionId)> members) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqChangeMemberPos);
            int count = members?.Count ?? 0;
            var writer = CreateVariableWriter(definition, 4 + 12 * count);
            for (int i = 0; i < count; i++) {
                writer.WriteUInt32(members[i].AccountId);
                writer.WriteUInt32(members[i].CharId);
                writer.WriteInt32(members[i].PositionId);
            }

            return writer.ToArray();
        }

        /// <summary>CZ_REG_CHANGE_GUILD_POSITIONINFO 0x0161 (mode 0x68): { position.L, rights.L, ranking.L, payRate.L, name.24B }*.</summary>
        public byte[] BuildCzRegChangeGuildPositionInfo(System.Collections.Generic.IReadOnlyList<RoGuildPosition> positions, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzRegChangeGuildPositionInfo);
            int count = positions?.Count ?? 0;
            var writer = CreateVariableWriter(definition, 4 + 40 * count);
            for (int i = 0; i < count; i++) {
                writer.WriteInt32(positions[i].Id);
                writer.WriteInt32(positions[i].Rights);
                writer.WriteInt32(positions[i].Ranking);
                writer.WriteInt32(positions[i].PayRate);
                writer.WriteFixedString(positions[i].Name, 24, encoding);
            }

            return writer.ToArray();
        }

        /// <summary>CZ_REQ_MAKE_GUILD 0x0165 (mode 0x70): char id.L, name.24B (at most 23 bytes and the NUL).</summary>
        public byte[] BuildCzReqMakeGuild(uint charId, string name, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqMakeGuild);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(charId);
            writer.WriteFixedString(name ?? string.Empty, 24, encoding ?? Encoding.ASCII);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_REQ_DISORGANIZE_GUILD 0x015d (mode 0x6a): key.40B (the guild name).</summary>
        public byte[] BuildCzReqDisorganizeGuild(string key, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqDisorganizeGuild);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(key ?? string.Empty, 40, encoding ?? Encoding.ASCII);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_GUILD_NOTICE 0x016e (mode 0x76): guild.L, subject.60B, notice.120B.</summary>
        public byte[] BuildCzGuildNotice(uint guildId, string subject, string notice, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzGuildNotice);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(guildId);
            writer.WriteFixedString(subject ?? string.Empty, 60, encoding ?? Encoding.ASCII);
            writer.WriteFixedString(notice ?? string.Empty, 120, encoding ?? Encoding.ASCII);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_ALLY_GUILD 0x0172 (mode 0x78): inviter account.L, answer.L (1 accept).</summary>
        public byte[] BuildCzAllyGuild(uint inviterAccountId, bool accept) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzAllyGuild);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(inviterAccountId);
            writer.WriteInt32(accept ? 1 : 0);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_REQ_BAN_GUILD 0x015b (mode 0x6f): guild.L, account.L, char.L, reason.40B.</summary>
        public byte[] BuildCzReqBanGuild(uint guildId, uint accountId, uint characterId, string reason, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqBanGuild);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(guildId);
            writer.WriteUInt32(accountId);
            writer.WriteUInt32(characterId);
            writer.WriteFixedString(reason ?? string.Empty, 40, encoding ?? Encoding.ASCII);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzMerCommand(byte command) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzMerCommand);
            var writer = CreateFixedWriter(definition);
            writer.WriteByte(command);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_TRYCAPTURE_MONSTER 0x019f: the target's actor id.</summary>
        public byte[] BuildCzTryCaptureMonster(uint actorId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzTryCaptureMonster);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(actorId);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_PET_EVOLUTION 0x09fb: the target egg id.L, then (inventory index.W, amount.W) per material.</summary>
        public byte[] BuildCzPetEvolution(uint targetEggId, System.Collections.Generic.IReadOnlyList<(int Index, int Amount)> materials) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzPetEvolution);
            int count = materials?.Count ?? 0;
            var writer = CreateVariableWriter(definition, 8 + 4 * count);
            writer.WriteUInt32(targetEggId);
            for (int i = 0; i < count; i++) {
                writer.WriteUInt16(unchecked((ushort) materials[i].Index));
                writer.WriteUInt16(unchecked((ushort) materials[i].Amount));
            }

            return writer.ToArray();
        }

        /// <summary>CZ_REQMAKINGITEM 0x018e: item id.L (0 = Cancel), three material item ids.L.</summary>
        public byte[] BuildCzReqMakingItem(uint itemId, uint[] materials) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqMakingItem);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(itemId);
            for (int m = 0; m < 3; m++) writer.WriteUInt32(materials != null && m < materials.Length ? materials[m] : 0);
            return FinishFixed(writer, definition);
        }

        /// <summary>CZ_CANCEL_MERGE_ITEM 0x0974.</summary>
        public byte[] BuildCzCancelMergeItem() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCancelMergeItem);
            return FinishFixed(CreateFixedWriter(definition), definition);
        }

        public byte[] BuildCzExitRoom() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzExitRoom);
            return FinishFixed(CreateFixedWriter(definition), definition);
        }

        public byte[] BuildCzReqNameByGid(uint characterId) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqNameByGid);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(characterId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzAckReqAddFriends(uint accountId, uint characterId, bool accept) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzAckReqAddFriends);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt32(accountId);
            writer.WriteUInt32(characterId);
            writer.WriteInt32(accept ? 1 : 0);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzOpenAllRodex(ulong mailId = 0, ulong returnMailId = 0, ulong accountMailId = 0) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzOpenAllRodex);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt64(mailId);
            writer.WriteUInt64(returnMailId);
            writer.WriteUInt64(accountMailId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzUpdateAllRodex(ulong mailId = 0, ulong returnMailId = 0, ulong accountMailId = 0) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzUpdateAllRodex);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt64(mailId);
            writer.WriteUInt64(returnMailId);
            writer.WriteUInt64(accountMailId);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzCloseRodexBox() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCloseRodexBox);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqReadRodex(byte openType, ulong mailId) {
            return BuildCzRodexOpenTypeMailId(RoOutboundPacketKind.CzReqReadRodex, openType, mailId);
        }

        public byte[] BuildCzReqNextRodex(byte openType, ulong mailId) {
            return BuildCzRodexOpenTypeMailId(RoOutboundPacketKind.CzReqNextRodex, openType, mailId);
        }

        public byte[] BuildCzReqDeleteRodex(byte openType, ulong mailId) {
            return BuildCzRodexOpenTypeMailId(RoOutboundPacketKind.CzReqDeleteRodex, openType, mailId);
        }

        public byte[] BuildCzReqZenyFromRodex(byte openType, ulong mailId) {
            return BuildCzRodexMailIdOpenType(RoOutboundPacketKind.CzReqZenyFromRodex, openType, mailId);
        }

        public byte[] BuildCzReqItemFromRodex(byte openType, ulong mailId) {
            return BuildCzRodexMailIdOpenType(RoOutboundPacketKind.CzReqItemFromRodex, openType, mailId);
        }

        public byte[] BuildCzReqCancelWriteRodex() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqCancelWriteRodex);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqAddItemRodex(int bagIndex, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqAddItemRodex);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteUInt16(RequireUInt16(nameof(amount), Math.Max(1, amount)));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqRemoveRodexItem(int bagIndex, int amount) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqRemoveRodexItem);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt16(RequireUInt16(nameof(bagIndex), bagIndex));
            writer.WriteUInt16(RequireUInt16(nameof(amount), Math.Max(1, amount)));
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqOpenWriteRodex(string characterName, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqOpenWriteRodex);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(characterName ?? string.Empty, 24, encoding);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzCheckReceiveCharacterName(string characterName, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCheckReceiveCharacterName);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(SanitizeRodexReceiverName(characterName), 24, encoding);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzCheckRodexReceive(string characterName, byte ownCharacterOnly = 1, Encoding encoding = null) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzCheckRodexReceive);
            var writer = CreateFixedWriter(definition);
            writer.WriteFixedString(SanitizeRodexReceiverName(characterName), 24, encoding);
            writer.WriteByte(ownCharacterOnly);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzCheckRodexReceiverForProfile(string characterName, byte ownCharacterOnly = 1, Encoding encoding = null) {
            return profile.PacketVersion >= 20201104
                ? BuildCzCheckRodexReceive(characterName, ownCharacterOnly, encoding)
                : BuildCzCheckReceiveCharacterName(characterName, encoding);
        }

        public byte[] BuildCzOpenUi(byte uiType) {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzOpenUi);
            var writer = CreateFixedWriter(definition);
            writer.WriteByte(uiType);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqAttendance() {
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqAttendance);
            var writer = CreateFixedWriter(definition);
            return FinishFixed(writer, definition);
        }

        public byte[] BuildCzReqSendRodex2(
            string receiverName,
            string senderName,
            long zeny,
            string title,
            string body,
            uint receiverCharacterId,
            Encoding encoding = null) {
            receiverName ??= string.Empty;
            senderName ??= string.Empty;
            title = Truncate(title ?? string.Empty, 40);
            body = Truncate(body ?? string.Empty, 500);
            byte[] titleBytes = (encoding ?? Encoding.ASCII).GetBytes(title + "\0");
            byte[] bodyBytes = (encoding ?? Encoding.ASCII).GetBytes(body + "\0");
            int packetLength = 2 + 2 + 24 + 24 + 8 + 2 + 2 + 4 + titleBytes.Length + bodyBytes.Length;
            RoPacketDefinition definition = ResolveDefinition(RoOutboundPacketKind.CzReqSendRodex2);
            var writer = CreateVariableWriter(definition, packetLength);
            writer.WriteFixedString(receiverName, 24, encoding);
            writer.WriteFixedString(senderName, 24, encoding);
            writer.WriteInt64(Math.Max(0, zeny));
            writer.WriteUInt16(RequireUInt16(nameof(title), titleBytes.Length));
            writer.WriteUInt16(RequireUInt16(nameof(body), bodyBytes.Length));
            writer.WriteUInt32(receiverCharacterId);
            writer.WriteBytes(titleBytes);
            writer.WriteBytes(bodyBytes);
            return writer.ToArray();
        }

        private byte[] BuildCzRodexOpenTypeMailId(RoOutboundPacketKind kind, byte openType, ulong mailId) {
            RoPacketDefinition definition = ResolveDefinition(kind);
            var writer = CreateFixedWriter(definition);
            writer.WriteByte(openType);
            writer.WriteUInt64(mailId);
            return FinishFixed(writer, definition);
        }

        private byte[] BuildCzRodexMailIdOpenType(RoOutboundPacketKind kind, byte openType, ulong mailId) {
            RoPacketDefinition definition = ResolveDefinition(kind);
            var writer = CreateFixedWriter(definition);
            writer.WriteUInt64(mailId);
            writer.WriteByte(openType);
            return FinishFixed(writer, definition);
        }

        private RoPacketWriter CreateFixedWriter(RoPacketDefinition definition) {
            if (definition.IsVariableLength) {
                throw new InvalidOperationException($"Outbound packet '{definition.Name}' must be fixed-length.");
            }

            var writer = new RoPacketWriter(definition.Length);
            writer.WriteUInt16(definition.PacketId);
            return writer;
        }

        private RoPacketWriter CreateVariableWriter(RoPacketDefinition definition, int packetLength) {
            if (!definition.IsVariableLength) {
                throw new InvalidOperationException($"Outbound packet '{definition.Name}' must be variable-length.");
            }

            if (packetLength < 4 || packetLength > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(packetLength), "Variable packet length must fit in a ushort header.");
            }

            var writer = new RoPacketWriter(packetLength);
            writer.WriteUInt16(definition.PacketId);
            writer.WriteUInt16((ushort) packetLength);
            return writer;
        }

        private byte[] FinishFixed(RoPacketWriter writer, RoPacketDefinition definition) {
            byte[] packet = writer.ToArray();
            if (packet.Length != definition.Length) {
                throw new InvalidOperationException(
                    $"Outbound packet '{definition.Name}' in profile '{profile.Name}' expected {definition.Length} bytes, got {packet.Length}.");
            }

            return packet;
        }

        private static string[] GetPacketNames(RoOutboundPacketKind kind) {
            switch (kind) {
                case RoOutboundPacketKind.CaLogin: return new[] { "CA_LOGIN" };
                case RoOutboundPacketKind.ChEnter: return new[] { "CH_ENTER" };
                case RoOutboundPacketKind.ChSelectChar: return new[] { "CH_SELECT_CHAR" };
                case RoOutboundPacketKind.ChSelectAccessibleMapName: return new[] { "CH_SELECT_ACCESSIBLE_MAPNAME" };
                case RoOutboundPacketKind.ChAvailableSecondPassword: return new[] { "CH_AVAILABLE_SECOND_PASSWD" };
                case RoOutboundPacketKind.ChCharListRequest: return new[] { "CH_CHARLIST_REQ" };
                case RoOutboundPacketKind.ChMakeChar: return new[] { "CH_MAKE_CHAR" };
                case RoOutboundPacketKind.ChDeleteChar3Reserved: return new[] { "CH_DELETE_CHAR3_RESERVED" };
                case RoOutboundPacketKind.ChDeleteChar3: return new[] { "CH_DELETE_CHAR3" };
                case RoOutboundPacketKind.ChDeleteChar3Cancel: return new[] { "CH_DELETE_CHAR3_CANCEL" };
                case RoOutboundPacketKind.ChDeleteChar2: return new[] { "CH_DELETE_CHAR2" };
                case RoOutboundPacketKind.CzEnter2: return new[] { "CZ_ENTER2" };
                case RoOutboundPacketKind.CzNotifyActorInit: return new[] { "CZ_NOTIFY_ACTORINIT" };
                case RoOutboundPacketKind.CzRequestMove: return new[] { "CZ_REQUEST_MOVE2", "CZ_REQUEST_MOVE" };
                case RoOutboundPacketKind.CzRequestTime: return new[] { "CZ_REQUEST_TIME" };
                case RoOutboundPacketKind.CzPingLive: return new[] { "CZ_PING_LIVE" };
                case RoOutboundPacketKind.CzPing: return new[] { "CZ_PING" };
                case RoOutboundPacketKind.CzActionRequest: return new[] { "CZ_REQUEST_ACT2", "CZ_REQUEST_ACT" };
                case RoOutboundPacketKind.CzRequestName: return new[] { "CZ_REQUEST_NAME2" };
                case RoOutboundPacketKind.CzRestart: return new[] { "CZ_RESTART" };
                case RoOutboundPacketKind.CzStandingResurrection: return new[] { "CZ_STANDING_RESURRECTION" };
                case RoOutboundPacketKind.CzContactNpc: return new[] { "CZ_CONTACTNPC" };
                case RoOutboundPacketKind.CzEquipWinMicroscope: return new[] { "CZ_EQUIPWIN_MICROSCOPE" };
                case RoOutboundPacketKind.CzConfig: return new[] { "CZ_CONFIG" };
                case RoOutboundPacketKind.CzNpcSelectMenu: return new[] { "CZ_NPC_SELECT_MENU" };
                case RoOutboundPacketKind.CzReqNextScript: return new[] { "CZ_REQ_NEXT_SCRIPT" };
                case RoOutboundPacketKind.CzInputEditDlg: return new[] { "CZ_INPUT_EDITDLG" };
                case RoOutboundPacketKind.CzInputEditDlgStr: return new[] { "CZ_INPUT_EDITDLGSTR" };
                case RoOutboundPacketKind.CzCloseDialog: return new[] { "CZ_CLOSE_DIALOG" };
                case RoOutboundPacketKind.CzWhisper: return new[] { "CZ_WHISPER", "ZC_UNK1" };
                case RoOutboundPacketKind.CzRequestChat: return new[] { "CZ_REQUEST_CHAT" };
                case RoOutboundPacketKind.CzPartyChat: return new[] { "CZ_PARTY_CHAT" };
                case RoOutboundPacketKind.CzReqJoinGuild: return new[] { "CZ_REQ_JOIN_GUILD" };
                case RoOutboundPacketKind.CzJoinGuild: return new[] { "CZ_JOIN_GUILD" };
                case RoOutboundPacketKind.CzReqLeaveGuild: return new[] { "CZ_REQ_LEAVE_GUILD" };
                case RoOutboundPacketKind.CzGuildChat: return new[] { "CZ_GUILD_CHAT" };
                case RoOutboundPacketKind.CzClanChat: return new[] { "CZ_CLAN_CHAT" };
                case RoOutboundPacketKind.CzCommandPet: return new[] { "CZ_COMMAND_PET" };
                case RoOutboundPacketKind.CzReqItemIdentify: return new[] { "CZ_REQ_ITEMIDENTIFY" };
                case RoOutboundPacketKind.CzReqItemCompositionList: return new[] { "CZ_REQ_ITEMCOMPOSITION_LIST" };
                case RoOutboundPacketKind.CzReqItemComposition: return new[] { "CZ_REQ_ITEMCOMPOSITION" };
                case RoOutboundPacketKind.CzReqMakingArrow: return new[] { "CZ_REQ_MAKINGARROW" };
                case RoOutboundPacketKind.CzReqChangeCart: return new[] { "CZ_REQ_CHANGECART" };
                case RoOutboundPacketKind.CzSelectCart: return new[] { "CZ_SELECTCART" };
                case RoOutboundPacketKind.CzSelectWarpPoint: return new[] { "CZ_SELECT_WARPPOINT" };
                case RoOutboundPacketKind.CzUseItem: return new[] { "CZ_USE_ITEM2" };
                case RoOutboundPacketKind.CzItemThrow: return new[] { "CZ_ITEM_THROW2" };
                case RoOutboundPacketKind.CzItemPickup: return new[] { "CZ_ITEM_PICKUP2" };
                case RoOutboundPacketKind.CzAckSelectDealType: return new[] { "CZ_ACK_SELECT_DEALTYPE" };
                case RoOutboundPacketKind.CzPcPurchaseItemList: return new[] { "CZ_PC_PURCHASE_ITEMLIST" };
                case RoOutboundPacketKind.CzPcSellItemList: return new[] { "CZ_PC_SELL_ITEMLIST" };
                case RoOutboundPacketKind.CzNpcTradeQuit: return new[] { "CZ_NPC_TRADE_QUIT" };
                case RoOutboundPacketKind.CzReqOpenStore2: return new[] { "CZ_REQ_OPENSTORE2" };
                case RoOutboundPacketKind.CzReqCloseStore: return new[] { "CZ_REQ_CLOSESTORE" };
                case RoOutboundPacketKind.CzReqBuyFromMc: return new[] { "CZ_REQ_BUY_FROMMC" };
                case RoOutboundPacketKind.CzPcPurchaseItemListFromMc: return new[] { "CZ_PC_PURCHASE_ITEMLIST_FROMMC" };
                case RoOutboundPacketKind.CzPcPurchaseItemListFromMc2: return new[] { "CZ_PC_PURCHASE_ITEMLIST_FROMMC2" };
                case RoOutboundPacketKind.CzNpcMarketPurchase: return new[] { "CZ_NPC_MARKET_PURCHASE" };
                case RoOutboundPacketKind.CzNpcMarketClose: return new[] { "CZ_NPC_MARKET_CLOSE" };
                case RoOutboundPacketKind.CzNpcBarterMarketPurchase: return new[] { "CZ_NPC_BARTER_MARKET_PURCHASE" };
                case RoOutboundPacketKind.CzNpcBarterMarketClose: return new[] { "CZ_NPC_BARTER_MARKET_CLOSE" };
                case RoOutboundPacketKind.CzNpcExpandedBarterMarketPurchase: return new[] { "CZ_NPC_EXPANDED_BARTER_MARKET_PURCHASE" };
                case RoOutboundPacketKind.CzNpcExpandedBarterMarketClose: return new[] { "CZ_NPC_EXPANDED_BARTER_MARKET_CLOSE" };
                case RoOutboundPacketKind.CzMoveItemFromBodyToStore: return new[] { "CZ_MOVE_ITEM_FROM_BODY_TO_STORE2" };
                case RoOutboundPacketKind.CzMoveItemFromStoreToBody: return new[] { "CZ_MOVE_ITEM_FROM_STORE_TO_BODY2" };
                case RoOutboundPacketKind.CzCloseStore: return new[] { "CZ_CLOSE_STORE" };
                case RoOutboundPacketKind.CzMoveItemFromBodyToCart: return new[] { "CZ_MOVE_ITEM_FROM_BODY_TO_CART" };
                case RoOutboundPacketKind.CzMoveItemFromCartToBody: return new[] { "CZ_MOVE_ITEM_FROM_CART_TO_BODY" };
                case RoOutboundPacketKind.CzMoveItemFromStoreToCart: return new[] { "CZ_MOVE_ITEM_FROM_STORE_TO_CART" };
                case RoOutboundPacketKind.CzMoveItemFromCartToStore: return new[] { "CZ_MOVE_ITEM_FROM_CART_TO_STORE" };
                case RoOutboundPacketKind.CzCartOff: return new[] { "CZ_CARTOFF" };
                case RoOutboundPacketKind.CzReqBankOpen: return new[] { "CZ_REQ_BANK_OPEN" };
                case RoOutboundPacketKind.CzReqBankClose: return new[] { "CZ_REQ_BANK_CLOSE" };
                case RoOutboundPacketKind.CzReqBankingCheck: return new[] { "CZ_REQ_BANKING_CHECK" };
                case RoOutboundPacketKind.CzReqBankingDeposit: return new[] { "CZ_REQ_BANKING_DEPOSIT" };
                case RoOutboundPacketKind.CzReqBankingWithdraw: return new[] { "CZ_REQ_BANKING_WITHDRAW" };
                case RoOutboundPacketKind.CzRequestExchangeItem: return new[] { "CZ_REQ_EXCHANGE_ITEM" };
                case RoOutboundPacketKind.CzAckExchangeItem: return new[] { "CZ_ACK_EXCHANGE_ITEM" };
                case RoOutboundPacketKind.CzAddExchangeItem: return new[] { "CZ_ADD_EXCHANGE_ITEM" };
                case RoOutboundPacketKind.CzConcludeExchangeItem: return new[] { "CZ_CONCLUDE_EXCHANGE_ITEM" };
                case RoOutboundPacketKind.CzCancelExchangeItem: return new[] { "CZ_CANCEL_EXCHANGE_ITEM" };
                case RoOutboundPacketKind.CzExecExchangeItem: return new[] { "CZ_EXEC_EXCHANGE_ITEM" };
                case RoOutboundPacketKind.CzReqWearEquip: return new[] { "CZ_REQ_WEAR_EQUIP_V5" };
                case RoOutboundPacketKind.CzReqTakeoffEquip: return new[] { "CZ_REQ_TAKEOFF_EQUIP" };
                case RoOutboundPacketKind.CzUseSkill: return new[] { "CZ_USE_SKILL2" };
                case RoOutboundPacketKind.CzUseSkillToGround: return new[] { "CZ_USE_SKILL_TOGROUND2" };
                case RoOutboundPacketKind.CzShortcutKeyChange: return new[] { "CZ_SHORTCUT_KEY_CHANGE2" };
                case RoOutboundPacketKind.CzShortcutKeyBarRotate: return new[] { "CZ_SHORTCUTKEYBAR_ROTATE2" };
                case RoOutboundPacketKind.CzStatusUp: return new[] { "CZ_STATUS_UP" };
                case RoOutboundPacketKind.CzQuestActive: return new[] { "CZ_ACTIVE_QUEST" };
                case RoOutboundPacketKind.CzMakeGroup: return new[] { "CZ_MAKE_GROUP2" };
                case RoOutboundPacketKind.CzReqExpelGroupMember: return new[] { "CZ_REQ_EXPEL_GROUP_MEMBER" };
                case RoOutboundPacketKind.CzPartyJoinReq: return new[] { "CZ_PARTY_JOIN_REQ" };
                case RoOutboundPacketKind.CzPartyJoinReqAck: return new[] { "CZ_PARTY_JOIN_REQ_ACK" };
                case RoOutboundPacketKind.CzLeaveGroup: return new[] { "CZ_LEAVE_GROUP" };
                case RoOutboundPacketKind.CzGroupInfoChangeV2: return new[] { "CZ_GROUPINFO_CHANGE_V2" };
                case RoOutboundPacketKind.CzChangeGroupMaster: return new[] { "CZ_CHANGE_GROUP_MASTER" };
                case RoOutboundPacketKind.CzAddFriends: return new[] { "CZ_ADD_FRIENDS" };
                case RoOutboundPacketKind.CzDeleteFriends: return new[] { "CZ_DELETE_FRIENDS" };
                case RoOutboundPacketKind.CzReqNameByGid: return new[] { "CZ_REQNAME_BYGID" };
                case RoOutboundPacketKind.CzCreateChatRoom: return new[] { "CZ_CREATE_CHATROOM" };
                case RoOutboundPacketKind.CzChangeChatRoom: return new[] { "CZ_CHANGE_CHATROOM" };
                case RoOutboundPacketKind.CzReqEnterRoom: return new[] { "CZ_REQ_ENTER_ROOM" };
                case RoOutboundPacketKind.CzReqRoleChange: return new[] { "CZ_REQ_ROLE_CHANGE" };
                case RoOutboundPacketKind.CzReqExpelMember: return new[] { "CZ_REQ_EXPEL_MEMBER" };
                case RoOutboundPacketKind.CzExitRoom: return new[] { "CZ_EXIT_ROOM" };
                case RoOutboundPacketKind.CzReqEmotion: return new[] { "CZ_REQ_EMOTION" };
                case RoOutboundPacketKind.CzReqEmotionExpansion: return new[] { "CZ_REQ_EMOTION_EXPANSION" };
                case RoOutboundPacketKind.CzReqItemRepair2: return new[] { "CZ_REQ_ITEMREPAIR2" };
                case RoOutboundPacketKind.CzReqWeaponRefine: return new[] { "CZ_REQ_WEAPONREFINE" };
                case RoOutboundPacketKind.CzSelectAutoSpell: return new[] { "CZ_SELECTAUTOSPELL" };
                case RoOutboundPacketKind.CzReqMergeItem: return new[] { "CZ_REQ_MERGE_ITEM" };
                case RoOutboundPacketKind.CzCancelMergeItem: return new[] { "CZ_CANCEL_MERGE_ITEM" };
                case RoOutboundPacketKind.CzReqMakingItem: return new[] { "CZ_REQMAKINGITEM" };
                case RoOutboundPacketKind.CzRenamePet: return new[] { "CZ_RENAME_PET" };
                case RoOutboundPacketKind.CzSelectPetEgg: return new[] { "CZ_SELECT_PETEGG" };
                case RoOutboundPacketKind.CzTryCaptureMonster: return new[] { "CZ_TRYCAPTURE_MONSTER" };
                case RoOutboundPacketKind.CzPetEvolution: return new[] { "CZ_PET_EVOLUTION" };
                case RoOutboundPacketKind.CzCommandMer: return new[] { "CZ_COMMAND_MER" };
                case RoOutboundPacketKind.CzRenameMer: return new[] { "CZ_RENAME_MER" };
                case RoOutboundPacketKind.CzMerCommand: return new[] { "CZ_MER_COMMAND" };
                case RoOutboundPacketKind.CzReqGuildMenuInterface: return new[] { "CZ_REQ_GUILD_MENUINTERFACE" };
                case RoOutboundPacketKind.CzReqGuildMenu: return new[] { "CZ_REQ_GUILD_MENU" };
                case RoOutboundPacketKind.CzReqChangeMemberPos: return new[] { "CZ_REQ_CHANGE_MEMBERPOS" };
                case RoOutboundPacketKind.CzRegChangeGuildPositionInfo: return new[] { "CZ_REG_CHANGE_GUILD_POSITIONINFO" };
                case RoOutboundPacketKind.CzReqBanGuild: return new[] { "CZ_REQ_BAN_GUILD" };
                case RoOutboundPacketKind.CzReqDisorganizeGuild: return new[] { "CZ_REQ_DISORGANIZE_GUILD" };
                case RoOutboundPacketKind.CzReqMakeGuild: return new[] { "CZ_REQ_MAKE_GUILD" };
                case RoOutboundPacketKind.CzAllyGuild: return new[] { "CZ_ALLY_GUILD" };
                case RoOutboundPacketKind.CzGuildNotice: return new[] { "CZ_GUILD_NOTICE" };
                case RoOutboundPacketKind.CzReqOpenBuyingStore: return new[] { "CZ_REQ_OPEN_BUYING_STORE" };
                case RoOutboundPacketKind.CzReqCloseBuyingStore: return new[] { "CZ_REQ_CLOSE_BUYING_STORE" };
                case RoOutboundPacketKind.CzReqClickToBuyingStore: return new[] { "CZ_REQ_CLICK_TO_BUYING_STORE" };
                case RoOutboundPacketKind.CzReqTradeBuyingStore: return new[] { "CZ_REQ_TRADE_BUYING_STORE" };
                case RoOutboundPacketKind.CzAckReqAddFriends: return new[] { "CZ_ACK_REQ_ADD_FRIENDS" };
                case RoOutboundPacketKind.CzOpenAllRodex: return new[] { "CZ_OPEN_ALL_RODEX", "CZ_OPEN_RODEXBOX" };
                case RoOutboundPacketKind.CzUpdateAllRodex: return new[] { "CZ_UPDATE_ALL_RODEX", "CZ_REQ_REFRESH_RODEX" };
                case RoOutboundPacketKind.CzCloseRodexBox: return new[] { "CZ_CLOSE_RODEXBOX" };
                case RoOutboundPacketKind.CzReqReadRodex: return new[] { "CZ_REQ_READ_RODEX" };
                case RoOutboundPacketKind.CzReqNextRodex: return new[] { "CZ_REQ_NEXT_RODEX" };
                case RoOutboundPacketKind.CzReqDeleteRodex: return new[] { "CZ_REQ_DELETE_RODEX" };
                case RoOutboundPacketKind.CzReqZenyFromRodex: return new[] { "CZ_REQ_ZENY_FROM_RODEX" };
                case RoOutboundPacketKind.CzReqItemFromRodex: return new[] { "CZ_REQ_ITEM_FROM_RODEX" };
                case RoOutboundPacketKind.CzReqCancelWriteRodex: return new[] { "CZ_REQ_CANCEL_WRITE_RODEX" };
                case RoOutboundPacketKind.CzReqAddItemRodex: return new[] { "CZ_REQ_ADD_ITEM_RODEX" };
                case RoOutboundPacketKind.CzReqRemoveRodexItem: return new[] { "CZ_REQ_REMOVE_RODEX_ITEM" };
                case RoOutboundPacketKind.CzReqOpenWriteRodex: return new[] { "CZ_REQ_OPEN_WRITE_RODEX" };
                case RoOutboundPacketKind.CzCheckReceiveCharacterName: return new[] { "CZ_CHECK_RECEIVE_CHARACTER_NAME" };
                case RoOutboundPacketKind.CzCheckRodexReceive: return new[] { "CZ_CHECK_RODEX_RECEIVE" };
                case RoOutboundPacketKind.CzReqSendRodex2: return new[] { "CZ_REQ_SEND_RODEX2", "CZ_RODEX_SEND_MAIL" };
                case RoOutboundPacketKind.CzOpenUi: return new[] { "CZ_OPEN_UI" };
                case RoOutboundPacketKind.CzReqAttendance: return new[] { "CZ_REQ_ATTENDANCE" };
                case RoOutboundPacketKind.CzHwid: return new[] { "CZ_HWID" };
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown outbound packet kind.");
            }
        }

        private static string SanitizeRodexReceiverName(string value) {
            value = (value ?? string.Empty).Trim().Replace("\t", string.Empty);
            while (value.Length > 0 && (value[0] == '$' || value[0] == '%')) {
                value = value.Substring(1).TrimStart();
            }

            return value;
        }

        private static string Truncate(string value, int maxLength) {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength);
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

        private static void RequireByte(string paramName, int value) {
            RequireByteValue(paramName, value);
        }

        private static int RequireByteValue(string paramName, int value) {
            if (value < 0 || value > byte.MaxValue) {
                throw new ArgumentOutOfRangeException(paramName);
            }

            return value;
        }

        private static ushort RequireUInt16(string paramName, int value) {
            if (value < 0 || value > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(paramName);
            }

            return (ushort) value;
        }

        private static ushort RequireUInt16(string paramName, uint value) {
            if (value > ushort.MaxValue) {
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
    }
}
